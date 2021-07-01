// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.AbstractIL.ILPdbWriter

open System
open System.Collections.Generic
open System.Collections.Immutable
open System.IO
open System.IO.Compression
open System.Reflection
open System.Reflection.Metadata
open System.Reflection.Metadata.Ecma335
open System.Text
open Internal.Utilities
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AbstractIL.Support
open Internal.Utilities.Library
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.IO
open FSharp.Compiler.Text.Range

type BlobBuildingStream () =
    inherit Stream()

    static let chunkSize = 32 * 1024
    let builder = new BlobBuilder(chunkSize)

    override _.CanWrite = true
    override _.CanRead  = false
    override _.CanSeek  = false
    override _.Length   = int64 (builder.Count)

    override _.Write(buffer: byte array, offset: int, count: int) = builder.WriteBytes(buffer, offset, count)
    override _.WriteByte(value: byte) = builder.WriteByte value
    member   _.WriteInt32(value: int) = builder.WriteInt32 value
    member   _.ToImmutableArray() = builder.ToImmutableArray()
    member   _.TryWriteBytes(stream: Stream, length: int) = builder.TryWriteBytes(stream, length)

    override _.Flush() = ()
    override _.Dispose(_disposing: bool) = ()
    override _.Seek(_offset: int64, _origin: SeekOrigin) = raise (new NotSupportedException())
    override _.Read(_buffer: byte array, _offset: int, _count: int) = raise (new NotSupportedException())
    override _.SetLength(_value: int64) = raise (new NotSupportedException())
    override val Position = 0L with get, set

// --------------------------------------------------------------------
// PDB types
// --------------------------------------------------------------------
type PdbDocumentData = ILSourceDocument

type PdbLocalVar =
    { Name: string
      Signature: byte[]
      /// the local index the name corresponds to
      Index: int32  }

type PdbMethodScope =
    { Children: PdbMethodScope array
      StartOffset: int
      EndOffset: int
      Locals: PdbLocalVar array
      (* REVIEW open_namespaces: pdb_namespace array *) }

type PdbSourceLoc =
    { Document: int
      Line: int
      Column: int }

type PdbSequencePoint =
    { Document: int
      Offset: int
      Line: int
      Column: int
      EndLine: int
      EndColumn: int }
    override x.ToString() = sprintf "(%d,%d)-(%d,%d)" x.Line x.Column x.EndLine x.EndColumn

type PdbMethodData =
    { MethToken: int32
      MethName: string
      LocalSignatureToken: int32
      Params: PdbLocalVar array
      RootScope: PdbMethodScope option
      Range: (PdbSourceLoc * PdbSourceLoc) option
      SequencePoints: PdbSequencePoint array }

module SequencePoint =
    let orderBySource sp1 sp2 =
        let c1 = compare sp1.Document sp2.Document
        if c1 <> 0 then
            c1
        else
            let c1 = compare sp1.Line sp2.Line
            if c1 <> 0 then
                c1
            else
                compare sp1.Column sp2.Column

    let orderByOffset sp1 sp2 =
        compare sp1.Offset sp2.Offset

/// 28 is the size of the IMAGE_DEBUG_DIRECTORY in ntimage.h
let sizeof_IMAGE_DEBUG_DIRECTORY = 28

[<NoEquality; NoComparison>]
type PdbData =
    { EntryPoint: int32 option
      Timestamp: int32
      ModuleID: byte[]
      Documents: PdbDocumentData[]
      Methods: PdbMethodData[]
      TableRowCounts: int[] }

type BinaryChunk =
    { size: int32
      addr: int32 }

type idd =
    { iddCharacteristics: int32
      iddMajorVersion: int32; (* actually u16 in IMAGE_DEBUG_DIRECTORY *)
      iddMinorVersion: int32; (* actually u16 in IMAGE_DEBUG_DIRECTORY *)
      iddType: int32
      iddTimestamp: int32
      iddData: byte[]
      iddChunk: BinaryChunk }

/// The specified Hash algorithm to use on portable pdb files.
type HashAlgorithm =
    | Sha1
    | Sha256

// Document checksum algorithms
let guidSha1 = Guid("ff1816ec-aa5e-4d10-87f7-6f4963833460")
let guidSha2 = Guid("8829d00f-11b8-4213-878b-770e8597ac16")

let checkSum (url: string) (checksumAlgorithm: HashAlgorithm) =
    try
        use file = FileSystem.OpenFileForReadShim(url)
        let guid, alg =
            match checksumAlgorithm with
            | HashAlgorithm.Sha1 -> guidSha1, System.Security.Cryptography.SHA1.Create() :> System.Security.Cryptography.HashAlgorithm
            | HashAlgorithm.Sha256 -> guidSha2, System.Security.Cryptography.SHA256.Create() :> System.Security.Cryptography.HashAlgorithm

        let checkSum = alg.ComputeHash file
        Some (guid, checkSum)
    with _ -> None

//---------------------------------------------------------------------
// Portable PDB Writer
//---------------------------------------------------------------------
let cvMagicNumber = 0x53445352L
let pdbGetCvDebugInfo (mvid: byte[]) (timestamp: int32) (filepath: string) (cvChunk: BinaryChunk) =
    let iddCvBuffer =
        // Debug directory entry
        let path = (System.Text.Encoding.UTF8.GetBytes filepath)
        let buffer = Array.zeroCreate (sizeof<int32> + mvid.Length + sizeof<int32> + path.Length + 1)
        let (offset, size) = (0, sizeof<int32>)                    // Magic Number RSDS dword: 0x53445352L
        Buffer.BlockCopy(BitConverter.GetBytes cvMagicNumber, 0, buffer, offset, size)
        let (offset, size) = (offset + size, mvid.Length)         // mvid Guid
        Buffer.BlockCopy(mvid, 0, buffer, offset, size)
        let (offset, size) = (offset + size, sizeof<int32>)       // # of pdb files generated (1)
        Buffer.BlockCopy(BitConverter.GetBytes 1, 0, buffer, offset, size)
        let (offset, size) = (offset + size, path.Length)         // Path to pdb string
        Buffer.BlockCopy(path, 0, buffer, offset, size)
        buffer
    { iddCharacteristics = 0                                                    // Reserved
      iddMajorVersion = 0x0100                                                  // VersionMajor should be 0x0100
      iddMinorVersion = 0x504d                                                  // VersionMinor should be 0x504d
      iddType = 2                                                               // IMAGE_DEBUG_TYPE_CODEVIEW
      iddTimestamp = timestamp
      iddData = iddCvBuffer                                                     // Path name to the pdb file when built
      iddChunk = cvChunk
    }

let pdbMagicNumber= 0x4244504dL
let pdbGetEmbeddedPdbDebugInfo (embeddedPdbChunk: BinaryChunk) (uncompressedLength: int64) (stream: MemoryStream) =
    let iddPdbBuffer =
        let buffer = Array.zeroCreate (sizeof<int32> + sizeof<int32> + int(stream.Length))
        let (offset, size) = (0, sizeof<int32>)                    // Magic Number dword: 0x4244504dL
        Buffer.BlockCopy(BitConverter.GetBytes pdbMagicNumber, 0, buffer, offset, size)
        let (offset, size) = (offset + size, sizeof<int32>)        // Uncompressed size
        Buffer.BlockCopy(BitConverter.GetBytes((int uncompressedLength)), 0, buffer, offset, size)
        let (offset, size) = (offset + size, int(stream.Length))   // Uncompressed size
        Buffer.BlockCopy(stream.ToArray(), 0, buffer, offset, size)
        buffer
    { iddCharacteristics = 0                                                    // Reserved
      iddMajorVersion = 0x0100                                                  // VersionMajor should be 0x0100
      iddMinorVersion = 0x0100                                                  // VersionMinor should be 0x0100
      iddType = 17                                                              // IMAGE_DEBUG_TYPE_EMBEDDEDPDB
      iddTimestamp = 0
      iddData = iddPdbBuffer                                                    // Path name to the pdb file when built
      iddChunk = embeddedPdbChunk
    }

let pdbChecksumDebugInfo timestamp (checksumPdbChunk: BinaryChunk) (algorithmName:string) (checksum: byte[]) =
    let iddBuffer =
        let alg = Encoding.UTF8.GetBytes(algorithmName)
        let buffer = Array.zeroCreate (alg.Length + 1 + checksum.Length)
        Buffer.BlockCopy(alg, 0, buffer, 0, alg.Length)
        Buffer.BlockCopy(checksum, 0, buffer, alg.Length + 1, checksum.Length)
        buffer
    { iddCharacteristics = 0                                                    // Reserved
      iddMajorVersion = 1                                                       // VersionMajor should be 1
      iddMinorVersion = 0                                                       // VersionMinor should be 0
      iddType = 19                                                              // IMAGE_DEBUG_TYPE_CHECKSUMPDB
      iddTimestamp = timestamp
      iddData = iddBuffer                                                       // Path name to the pdb file when built
      iddChunk = checksumPdbChunk
    }

let pdbGetPdbDebugDeterministicInfo (deterministicPdbChunk: BinaryChunk) =
    { iddCharacteristics = 0                                                    // Reserved
      iddMajorVersion = 0                                                       // VersionMajor should be 0
      iddMinorVersion = 0                                                       // VersionMinor should be 00
      iddType = 16                                                              // IMAGE_DEBUG_TYPE_DETERMINISTIC
      iddTimestamp = 0
      iddData = Array.empty                                                     // No DATA
      iddChunk = deterministicPdbChunk
    }

let pdbGetDebugInfo (contentId: byte[]) (timestamp: int32) (filepath: string)
                    (cvChunk: BinaryChunk)
                    (embeddedPdbChunk: BinaryChunk option)
                    (deterministicPdbChunk: BinaryChunk)
                    (checksumPdbChunk: BinaryChunk) (algorithmName:string) (checksum: byte [])
                    (uncompressedLength: int64) (stream: MemoryStream option)
                    (embeddedPdb: bool) (deterministic: bool) =
    [|  yield pdbGetCvDebugInfo contentId timestamp filepath cvChunk
        yield pdbChecksumDebugInfo timestamp checksumPdbChunk algorithmName checksum
        if embeddedPdb then
            match stream, embeddedPdbChunk with
            | None, _ | _, None -> ()
            | Some s, Some chunk ->
                yield pdbGetEmbeddedPdbDebugInfo chunk uncompressedLength s
        if deterministic then
            yield pdbGetPdbDebugDeterministicInfo deterministicPdbChunk
    |]

//------------------------------------------------------------------------------
// PDB Writer.  The function [WritePdbInfo] abstracts the
// imperative calls to the Symbol Writer API.
//------------------------------------------------------------------------------

// This function takes output file name and returns debug file name.
let getDebugFileName outfile (portablePDB: bool) =
#if ENABLE_MONO_SUPPORT
  if runningOnMono && not portablePDB then
      outfile + ".mdb"
  else
#else
      ignore portablePDB
#endif
      (FileSystemUtils.chopExtension outfile) + ".pdb"

let sortMethods showTimes info =
    reportTime showTimes (sprintf "PDB: Defined %d documents" info.Documents.Length)
    Array.sortInPlaceBy (fun x -> x.MethToken) info.Methods
    reportTime showTimes (sprintf "PDB: Sorted %d methods" info.Methods.Length)
    ()

let getRowCounts tableRowCounts =
    let builder = ImmutableArray.CreateBuilder<int>(tableRowCounts |> Array.length)
    tableRowCounts |> Seq.iter(fun x -> builder.Add x)
    builder.MoveToImmutable()

let generatePortablePdb (embedAllSource: bool) (embedSourceList: string list) (sourceLink: string) checksumAlgorithm showTimes (info: PdbData) (pathMap: PathMap) =
    sortMethods showTimes info
    let externalRowCounts = getRowCounts info.TableRowCounts
    let docs = info.Documents

    let metadata = MetadataBuilder()
    let serializeDocumentName (name: string) =
        let name = PathMap.apply pathMap name
        let count s c = s |> Seq.filter(fun ch -> if c = ch then true else false) |> Seq.length

        let s1, s2 = '/', '\\'
        let separator = if (count name s1) >= (count name s2) then s1 else s2

        let writer = new BlobBuilder()
        writer.WriteByte(byte separator)

        for part in name.Split( [| separator |] ) do
            let partIndex = MetadataTokens.GetHeapOffset(BlobHandle.op_Implicit(metadata.GetOrAddBlobUTF8 part))
            writer.WriteCompressedInteger(int partIndex)

        metadata.GetOrAddBlob writer

    let corSymLanguageTypeId = System.Guid(0xAB4F38C9u, 0xB6E6us, 0x43baus, 0xBEuy, 0x3Buy, 0x58uy, 0x08uy, 0x0Buy, 0x2Cuy, 0xCCuy, 0xE3uy)
    let embeddedSourceId     = System.Guid(0x0e8a571bu, 0x6926us, 0x466eus, 0xb4uy, 0xaduy, 0x8auy, 0xb0uy, 0x46uy, 0x11uy, 0xf5uy, 0xfeuy)
    let sourceLinkId         = System.Guid(0xcc110556u, 0xa091us, 0x4d38us, 0x9fuy, 0xecuy, 0x25uy, 0xabuy, 0x9auy, 0x35uy, 0x1auy, 0x6auy)

    /// <summary>
    /// The maximum number of bytes in to write out uncompressed.
    ///
    /// This prevents wasting resources on compressing tiny files with little to negative gain
    /// in PDB file size.
    ///
    /// Chosen as the point at which we start to see > 10% blob size reduction using all
    /// current source files in corefx and roslyn as sample data.
    /// </summary>
    let sourceCompressionThreshold = 200

    let documentIndex =
        let includeSource file =
            let isInList = embedSourceList |> List.exists (fun f -> String.Compare(file, f, StringComparison.OrdinalIgnoreCase ) = 0)

            if not embedAllSource && not isInList || not (FileSystem.FileExistsShim file) then
                None
            else
                use stream = FileSystem.OpenFileForReadShim(file)

                let length64 = stream.Length
                if length64 > int64 (Int32.MaxValue) then raise (new IOException("File is too long"))

                let builder = new BlobBuildingStream()
                let length = int length64
                if length < sourceCompressionThreshold then
                    builder.WriteInt32 0
                    builder.TryWriteBytes(stream, length) |> ignore
                else
                    builder.WriteInt32 length |>ignore
                    use deflater = new DeflateStream(builder, CompressionMode.Compress, true)
                    stream.CopyTo deflater |> ignore
                Some (builder.ToImmutableArray())

        let mutable index = new Dictionary<string, DocumentHandle>(docs.Length)
        let docLength = docs.Length + if String.IsNullOrEmpty sourceLink then 1 else 0
        metadata.SetCapacity(TableIndex.Document, docLength)
        for doc in docs do
            let handle =
                match checkSum doc.File checksumAlgorithm with
                | Some (hashAlg, checkSum) ->
                    let dbgInfo =
                        (serializeDocumentName doc.File,
                         metadata.GetOrAddGuid hashAlg,
                         metadata.GetOrAddBlob(checkSum.ToImmutableArray()),
                         metadata.GetOrAddGuid corSymLanguageTypeId) |> metadata.AddDocument
                    match includeSource doc.File with
                    | None -> ()
                    | Some blob ->
                        metadata.AddCustomDebugInformation(DocumentHandle.op_Implicit dbgInfo,
                                                           metadata.GetOrAddGuid embeddedSourceId,
                                                           metadata.GetOrAddBlob blob) |> ignore
                    dbgInfo
                | None ->
                    let dbgInfo =
                        (serializeDocumentName doc.File,
                         metadata.GetOrAddGuid(System.Guid.Empty),
                         metadata.GetOrAddBlob(ImmutableArray<byte>.Empty),
                         metadata.GetOrAddGuid corSymLanguageTypeId) |> metadata.AddDocument
                    dbgInfo
            index.Add(doc.File, handle)

        if not (String.IsNullOrWhiteSpace sourceLink) then
            use fs = FileSystem.OpenFileForReadShim(sourceLink)
            use ms = new MemoryStream()
            fs.CopyTo ms
            metadata.AddCustomDebugInformation(
                ModuleDefinitionHandle.op_Implicit(EntityHandle.ModuleDefinition),
                metadata.GetOrAddGuid sourceLinkId,
                metadata.GetOrAddBlob(ms.ToArray())) |> ignore
        index

    let mutable lastLocalVariableHandle = Unchecked.defaultof<LocalVariableHandle>
    metadata.SetCapacity(TableIndex.MethodDebugInformation, info.Methods.Length)
    info.Methods |> Array.iter (fun minfo ->
        let docHandle, sequencePointBlob =
            let sps =
                match minfo.Range with
                | None -> Array.empty
                | Some (_,_) -> minfo.SequencePoints

            let builder = new BlobBuilder()
            builder.WriteCompressedInteger(minfo.LocalSignatureToken)

            if sps.Length = 0 then
                builder.WriteCompressedInteger( 0 )
                builder.WriteCompressedInteger( 0 )
                Unchecked.defaultof<DocumentHandle>, Unchecked.defaultof<BlobHandle>
            else
                let getDocumentHandle d =
                    if docs.Length = 0 || d < 0 || d > docs.Length then
                        Unchecked.defaultof<DocumentHandle>
                    else
                        match documentIndex.TryGetValue(docs.[d].File) with
                        | false, _ -> Unchecked.defaultof<DocumentHandle>
                        | true, h -> h

                // Return a document that the entire method body is declared within.
                // If part of the method body is in another document returns nil handle.
                let tryGetSingleDocumentIndex =
                    let mutable singleDocumentIndex = sps.[0].Document
                    for i in 1 .. sps.Length - 1 do
                        if sps.[i].Document <> singleDocumentIndex then
                            singleDocumentIndex <- -1
                    singleDocumentIndex

                // Initial document:  When sp's spread over more than one document we put the initial document here.
                let singleDocumentIndex = tryGetSingleDocumentIndex
                if singleDocumentIndex = -1 then
                    builder.WriteCompressedInteger( MetadataTokens.GetRowNumber(DocumentHandle.op_Implicit(getDocumentHandle (sps.[0].Document))) )

                let mutable previousNonHiddenStartLine = -1
                let mutable previousNonHiddenStartColumn = 0

                for i in 0 .. (sps.Length - 1) do

                    if singleDocumentIndex <> -1 && sps.[i].Document <> singleDocumentIndex then
                        builder.WriteCompressedInteger( 0 )
                        builder.WriteCompressedInteger( MetadataTokens.GetRowNumber(DocumentHandle.op_Implicit(getDocumentHandle (sps.[i].Document))) )
                    else
                        //=============================================================================================================================================
                        // Sequence-point-record
                        // Validate these with magic numbers according to the portable pdb spec Sequence point dexcription:
                        // https://github.com/dotnet/corefx/blob/master/src/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#methoddebuginformation-table-0x31
                        //
                        // So the spec is actually bit iffy!!!!! (More like guidelines really.  )
                        //  It uses code similar to this to validate the values
                        //    if (result < 0 || result >= ushort.MaxValue)  // be errorfull
                        // Spec Says 0x10000 and value max = 0xFFFF but it can't even be = to maxvalue, and so the range is 0 .. 0xfffe inclusive
                        //=============================================================================================================================================

                        let capValue v maxValue =
                            if v < 0 then 0
                            elif v > maxValue then maxValue
                            else v

                        let capOffset v = capValue v 0xfffe
                        let capLine v =  capValue v 0x1ffffffe
                        let capColumn v = capValue v 0xfffe

                        let offset = capOffset sps.[i].Offset
                        let startLine = capLine sps.[i].Line
                        let endLine = capLine sps.[i].EndLine
                        let startColumn = capColumn sps.[i].Column
                        let endColumn = capColumn sps.[i].EndColumn

                        let offsetDelta =                                                               // delta from previous offset
                            if i > 0 then offset - capOffset sps.[i - 1].Offset
                            else offset

                        if i < 1 || offsetDelta > 0 then
                            builder.WriteCompressedInteger offsetDelta

                            // Check for hidden-sequence-point-record
                            if startLine = 0xfeefee ||
                               endLine = 0xfeefee ||
                               (startColumn = 0 && endColumn = 0) ||
                               ((endLine - startLine) = 0 && (endColumn - startColumn)  = 0)
                            then
                                // Hidden-sequence-point-record
                                builder.WriteCompressedInteger 0
                                builder.WriteCompressedInteger 0
                            else
                                // Non-hidden-sequence-point-record
                                let deltaLines = endLine - startLine                                    // lines
                                builder.WriteCompressedInteger deltaLines

                                let deltaColumns = endColumn - startColumn                              // Columns
                                if deltaLines = 0 then
                                    builder.WriteCompressedInteger deltaColumns
                                else
                                    builder.WriteCompressedSignedInteger deltaColumns

                                if previousNonHiddenStartLine < 0 then                                  // delta Start Line & Column:
                                    builder.WriteCompressedInteger startLine
                                    builder.WriteCompressedInteger startColumn
                                else
                                    builder.WriteCompressedSignedInteger(startLine - previousNonHiddenStartLine)
                                    builder.WriteCompressedSignedInteger(startColumn - previousNonHiddenStartColumn)

                                previousNonHiddenStartLine <- startLine
                                previousNonHiddenStartColumn <- startColumn

                getDocumentHandle singleDocumentIndex, metadata.GetOrAddBlob builder

        metadata.AddMethodDebugInformation(docHandle, sequencePointBlob) |> ignore

        // Write the scopes
        let nextHandle handle = MetadataTokens.LocalVariableHandle(MetadataTokens.GetRowNumber(LocalVariableHandle.op_Implicit handle) + 1)
        let writeMethodScope scope =
            let scopeSorter (scope1: PdbMethodScope) (scope2: PdbMethodScope) =
                if scope1.StartOffset > scope2.StartOffset then 1
                elif scope1.StartOffset < scope2.StartOffset then -1
                elif (scope1.EndOffset - scope1.StartOffset) > (scope2.EndOffset - scope2.StartOffset) then -1
                elif (scope1.EndOffset - scope1.StartOffset) < (scope2.EndOffset - scope2.StartOffset) then 1
                else 0

            let collectScopes scope =
                let list = new List<PdbMethodScope>()
                let rec toList scope parent =
                    let nested =
                        match parent with
                        | Some p -> scope.StartOffset <> p.StartOffset || scope.EndOffset <> p.EndOffset
                        | None -> true

                    if nested then list.Add scope
                    scope.Children |> Seq.iter(fun s -> toList s (if nested then Some scope else parent))

                toList scope None
                list.ToArray() |> Array.sortWith<PdbMethodScope> scopeSorter

            collectScopes scope |> Seq.iter(fun s ->
                                   metadata.AddLocalScope(MetadataTokens.MethodDefinitionHandle(minfo.MethToken),
                                                          Unchecked.defaultof<ImportScopeHandle>,
                                                          nextHandle lastLocalVariableHandle,
                                                          Unchecked.defaultof<LocalConstantHandle>,
                                                          s.StartOffset, s.EndOffset - s.StartOffset ) |>ignore

                                   for localVariable in s.Locals do
                                       lastLocalVariableHandle <- metadata.AddLocalVariable(LocalVariableAttributes.None, localVariable.Index, metadata.GetOrAddString(localVariable.Name))
                                   )

        match minfo.RootScope with
        | None -> ()
        | Some scope -> writeMethodScope scope )

    let entryPoint =
        match info.EntryPoint with
        | None -> MetadataTokens.MethodDefinitionHandle 0
        | Some x -> MetadataTokens.MethodDefinitionHandle x

    // Compute the contentId for the pdb. Always do it deterministically, since we have to compute the anyway.
    // The contentId is the hash of the ID using whichever algorithm has been specified to the compiler
    let mutable contentHash = Array.empty<byte>

    let algorithmName, hashAlgorithm =
        match checksumAlgorithm with
        | HashAlgorithm.Sha1 -> "SHA1", System.Security.Cryptography.SHA1.Create() :> System.Security.Cryptography.HashAlgorithm
        | HashAlgorithm.Sha256 -> "SHA256", System.Security.Cryptography.SHA256.Create() :> System.Security.Cryptography.HashAlgorithm
    let idProvider: System.Func<IEnumerable<Blob>, BlobContentId> =
        let convert (content: IEnumerable<Blob>) =
            let contentBytes = content |> Seq.collect (fun c -> c.GetBytes()) |> Array.ofSeq
            contentHash <- contentBytes |> hashAlgorithm.ComputeHash
            BlobContentId.FromHash contentHash
        System.Func<IEnumerable<Blob>, BlobContentId>(convert)

    let serializer = PortablePdbBuilder(metadata, externalRowCounts, entryPoint, idProvider)
    let blobBuilder = new BlobBuilder()
    let contentId= serializer.Serialize blobBuilder
    let portablePdbStream = new MemoryStream()
    blobBuilder.WriteContentTo portablePdbStream
    reportTime showTimes "PDB: Created"
    (portablePdbStream.Length, contentId, portablePdbStream, algorithmName, contentHash)

let compressPortablePdbStream (uncompressedLength: int64) (contentId: BlobContentId) (stream: MemoryStream) =
    let compressedStream = new MemoryStream()
    use compressionStream = new DeflateStream(compressedStream, CompressionMode.Compress,true)
    stream.WriteTo compressionStream
    (uncompressedLength, contentId, compressedStream)

let writePortablePdbInfo (contentId: BlobContentId) (stream: MemoryStream) showTimes fpdb pathMap cvChunk deterministicPdbChunk checksumPdbChunk algorithmName checksum embeddedPdb deterministic =
    try FileSystem.FileDeleteShim fpdb with _ -> ()
    use fs = FileSystem.OpenFileForWriteShim(fpdb, fileMode = FileMode.Create, fileAccess = FileAccess.ReadWrite)
    stream.WriteTo fs
    reportTime showTimes "PDB: Closed"
    pdbGetDebugInfo (contentId.Guid.ToByteArray()) (int32 (contentId.Stamp)) (PathMap.apply pathMap fpdb) cvChunk None deterministicPdbChunk checksumPdbChunk algorithmName checksum 0L None embeddedPdb deterministic

let embedPortablePdbInfo (uncompressedLength: int64)  (contentId: BlobContentId) (stream: MemoryStream) showTimes fpdb cvChunk pdbChunk deterministicPdbChunk checksumPdbChunk algorithmName checksum embeddedPdb deterministic =
    reportTime showTimes "PDB: Closed"
    let fn = Path.GetFileName fpdb
    pdbGetDebugInfo (contentId.Guid.ToByteArray()) (int32 (contentId.Stamp)) fn cvChunk (Some pdbChunk) deterministicPdbChunk checksumPdbChunk algorithmName checksum uncompressedLength (Some stream) embeddedPdb deterministic

#if !FX_NO_PDB_WRITER
//---------------------------------------------------------------------
// PDB Writer.  The function [WritePdbInfo] abstracts the
// imperative calls to the Symbol Writer API.
//---------------------------------------------------------------------
let writePdbInfo showTimes f fpdb info cvChunk =

    try FileSystem.FileDeleteShim fpdb with _ -> ()

    let pdbw = ref Unchecked.defaultof<PdbWriter>

    try
        pdbw := pdbInitialize f fpdb
    with _ -> error(Error(FSComp.SR.ilwriteErrorCreatingPdb fpdb, rangeCmdArgs))

    match info.EntryPoint with
    | None -> ()
    | Some x -> pdbSetUserEntryPoint !pdbw x

    let docs = info.Documents |> Array.map (fun doc -> pdbDefineDocument !pdbw doc.File)
    let getDocument i =
      if i < 0 || i > docs.Length then failwith "getDocument: bad doc number"
      docs.[i]
    reportTime showTimes (sprintf "PDB: Defined %d documents" info.Documents.Length)
    Array.sortInPlaceBy (fun x -> x.MethToken) info.Methods
    reportTime showTimes (sprintf "PDB: Sorted %d methods" info.Methods.Length)

    let spCounts = info.Methods |> Array.map (fun x -> x.SequencePoints.Length)
    let allSps = Array.collect (fun x -> x.SequencePoints) info.Methods |> Array.indexed

    let spOffset = ref 0
    info.Methods |> Array.iteri (fun i minfo ->

          let sps = Array.sub allSps !spOffset spCounts.[i]
          spOffset := !spOffset + spCounts.[i]
          begin match minfo.Range with
          | None -> ()
          | Some (a,b) ->
              pdbOpenMethod !pdbw minfo.MethToken

              pdbSetMethodRange !pdbw
                (getDocument a.Document) a.Line a.Column
                (getDocument b.Document) b.Line b.Column

              // Partition the sequence points by document
              let spsets =
                  let res = Dictionary<int,PdbSequencePoint list ref>()
                  for (_,sp) in sps do
                      let k = sp.Document
                      let mutable xsR = Unchecked.defaultof<_>
                      if res.TryGetValue(k,&xsR) then
                          xsR := sp :: !xsR
                      else
                          res.[k] <- ref [sp]

                  res

              spsets
              |> Seq.iter (fun kv ->
                  let spset = !kv.Value
                  if not spset.IsEmpty then
                    let spset = Array.ofList spset
                    Array.sortInPlaceWith SequencePoint.orderByOffset spset
                    let sps =
                        spset |> Array.map (fun sp ->
                            // Ildiag.dprintf "token 0x%08lx has an sp at offset 0x%08x\n" minfo.MethToken sp.Offset
                            (sp.Offset, sp.Line, sp.Column,sp.EndLine, sp.EndColumn))
                    // Use of alloca in implementation of pdbDefineSequencePoints can give stack overflow here
                    if sps.Length < 5000 then
                        pdbDefineSequencePoints !pdbw (getDocument spset.[0].Document) sps)

              // Write the scopes
              let rec writePdbScope parent sco =
                  if parent = None || sco.Locals.Length <> 0 || sco.Children.Length <> 0 then
                      // Only nest scopes if the child scope is a different size from
                      let nested =
                          match parent with
                          | Some p -> sco.StartOffset <> p.StartOffset || sco.EndOffset <> p.EndOffset
                          | None -> true
                      if nested then pdbOpenScope !pdbw sco.StartOffset
                      sco.Locals |> Array.iter (fun v -> pdbDefineLocalVariable !pdbw v.Name v.Signature v.Index)
                      sco.Children |> Array.iter (writePdbScope (if nested then Some sco else parent))
                      if nested then pdbCloseScope !pdbw sco.EndOffset

              match minfo.RootScope with
              | None -> ()
              | Some rootscope -> writePdbScope None rootscope
              pdbCloseMethod !pdbw
          end)
    reportTime showTimes "PDB: Wrote methods"

    let res = pdbWriteDebugInfo !pdbw
    for pdbDoc in docs do pdbCloseDocument pdbDoc
    pdbClose !pdbw f fpdb

    reportTime showTimes "PDB: Closed"
    [| { iddCharacteristics = res.iddCharacteristics
         iddMajorVersion = res.iddMajorVersion
         iddMinorVersion = res.iddMinorVersion
         iddType = res.iddType
         iddTimestamp = info.Timestamp
         iddData = res.iddData
         iddChunk = cvChunk } |]
#endif

#if ENABLE_MONO_SUPPORT
//---------------------------------------------------------------------
// Support functions for calling 'Mono.CompilerServices.SymbolWriter'
// assembly dynamically if it is available to the compiler
//---------------------------------------------------------------------
open Microsoft.FSharp.Reflection

// Dynamic invoke operator. Implements simple overload resolution based
// on the name and number of parameters only.
// Supports the following cases:
//   obj?Foo()        // call with no arguments
//   obj?Foo(1, "a")  // call with two arguments (extracted from tuple)
// NOTE: This doesn't actually handle all overloads.  It just picks first entry with right
// number of arguments.
let (?) this memb (args:'Args) : 'R =
    // Get array of 'obj' arguments for the reflection call
    let args =
        if typeof<'Args> = typeof<unit> then [| |]
        elif FSharpType.IsTuple typeof<'Args> then Microsoft.FSharp.Reflection.FSharpValue.GetTupleFields args
        else [| box args |]

    // Get methods and perform overload resolution
    let methods = this.GetType().GetMethods()
    let bestMatch = methods |> Array.tryFind (fun mi -> mi.Name = memb && mi.GetParameters().Length = args.Length)
    match bestMatch with
    | Some mi -> unbox(mi.Invoke(this, args))
    | None -> error(Error(FSComp.SR.ilwriteMDBMemberMissing memb, rangeCmdArgs))

// Creating instances of needed classes from 'Mono.CompilerServices.SymbolWriter' assembly

let monoCompilerSvc = new AssemblyName("Mono.CompilerServices.SymbolWriter, Version=2.0.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756")
let ctor (asmName: AssemblyName) clsName (args: obj[]) =
    let asm = Assembly.Load asmName
    let ty = asm.GetType clsName
    System.Activator.CreateInstance(ty, args)

let createSourceMethodImpl (name: string) (token: int) (namespaceID: int) =
    ctor monoCompilerSvc "Mono.CompilerServices.SymbolWriter.SourceMethodImpl" [| box name; box token; box namespaceID |]

let createWriter (f: string) =
    ctor monoCompilerSvc "Mono.CompilerServices.SymbolWriter.MonoSymbolWriter" [| box f |]

//---------------------------------------------------------------------
// MDB Writer.  Generate debug symbols using the MDB format
//---------------------------------------------------------------------
let writeMdbInfo fmdb f info =
    // Note, if we can't delete it code will fail later
    try FileSystem.FileDeleteShim fmdb with _ -> ()

    // Try loading the MDB symbol writer from an assembly available on Mono dynamically
    // Report an error if the assembly is not available.
    let wr =
        try createWriter f
        with e -> error(Error(FSComp.SR.ilwriteErrorCreatingMdb(), rangeCmdArgs))

    // NOTE: MonoSymbolWriter doesn't need information about entrypoints, so 'info.EntryPoint' is unused here.
    // Write information about Documents. Returns '(SourceFileEntry*CompileUnitEntry)[]'
    let docs =
        [| for doc in info.Documents do
             let doc = wr?DefineDocument(doc.File)
             let unit = wr?DefineCompilationUnit doc
             yield doc, unit |]

    let getDocument i =
        if i < 0 || i >= Array.length docs then failwith "getDocument: bad doc number" else docs.[i]

    // Sort methods and write them to the MDB file
    Array.sortInPlaceBy (fun x -> x.MethToken) info.Methods
    for meth in info.Methods do
        // Creates an instance of 'SourceMethodImpl' which is a private class that implements 'IMethodDef' interface
        // We need this as an argument to 'OpenMethod' below. Using private class is ugly, but since we don't reference
        // the assembly, the only way to implement 'IMethodDef' interface would be dynamically using Reflection.Emit...
        let sm = createSourceMethodImpl meth.MethName meth.MethToken 0
        match meth.Range with
        | Some(mstart, _) ->
            // NOTE: 'meth.Params' is not needed, Mono debugger apparently reads this from meta-data
            let _, cue = getDocument mstart.Document
            wr?OpenMethod(cue, 0, sm) |> ignore

            // Write sequence points
            for sp in meth.SequencePoints do
                wr?MarkSequencePoint(sp.Offset, cue?get_SourceFile(), sp.Line, sp.Column, false)

            // Walk through the tree of scopes and write all variables
            let rec writeScope (scope: PdbMethodScope) =
                wr?OpenScope(scope.StartOffset) |> ignore
                for local in scope.Locals do
                    wr?DefineLocalVariable(local.Index, local.Name)
                for child in scope.Children do
                    writeScope child
                wr?CloseScope(scope.EndOffset)
            match meth.RootScope with
            | None -> ()
            | Some rootscope -> writeScope rootscope


            // Finished generating debug information for the curretn method
            wr?CloseMethod()
        | _ -> ()

    // Finalize - MDB requires the MVID of the generated .NET module
    let moduleGuid = new System.Guid(info.ModuleID |> Array.map byte)
    wr?WriteSymbolFile moduleGuid
#endif

//---------------------------------------------------------------------
// Dumps debug info into a text file for testing purposes
//---------------------------------------------------------------------
open Printf

let logDebugInfo (outfile: string) (info: PdbData) =
    use sw = new StreamWriter(new FileStream(outfile + ".debuginfo", FileMode.Create))

    fprintfn sw "ENTRYPOINT\r\n  %b\r\n" info.EntryPoint.IsSome
    fprintfn sw "DOCUMENTS"
    for i, doc in Seq.zip [0 .. info.Documents.Length-1] info.Documents do
      fprintfn sw " [%d] %s" i doc.File
      fprintfn sw "     Type: %A" doc.DocumentType
      fprintfn sw "     Language: %A" doc.Language
      fprintfn sw "     Vendor: %A" doc.Vendor

    // Sort methods (because they are sorted in PDBs/MDBs too)
    fprintfn sw "\r\nMETHODS"
    Array.sortInPlaceBy (fun x -> x.MethToken) info.Methods
    for meth in info.Methods do
      fprintfn sw " %s" meth.MethName
      fprintfn sw "     Params: %A" [ for p in meth.Params -> sprintf "%d: %s" p.Index p.Name ]
      fprintfn sw "     Range: %A" (meth.Range |> Option.map (fun (f, t) ->
                                      sprintf "[%d,%d:%d] - [%d,%d:%d]" f.Document f.Line f.Column t.Document t.Line t.Column))
      fprintfn sw "     Points:"

      for sp in meth.SequencePoints do
        fprintfn sw "      - Doc: %d Offset:%d [%d:%d]-[%d-%d]" sp.Document sp.Offset sp.Line sp.Column sp.EndLine sp.EndColumn

      // Walk through the tree of scopes and write all variables
      fprintfn sw "     Scopes:"
      let rec writeScope offs (scope: PdbMethodScope) =
        fprintfn sw "      %s- [%d-%d]" offs scope.StartOffset scope.EndOffset
        if scope.Locals.Length > 0 then
          fprintfn sw "      %s  Locals: %A" offs [ for p in scope.Locals -> sprintf "%d: %s" p.Index p.Name ]
        for child in scope.Children do writeScope (offs + "  ") child

      match meth.RootScope with
      | None -> ()
      | Some rootscope -> writeScope "" rootscope
      fprintfn sw ""
