// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.AbstractIL.Support

open System
open System.IO
open System.Reflection
#if !FX_NO_SYMBOLSTORE
open System.Diagnostics.SymbolStore
#endif
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Internal.Utilities
open Internal.Utilities.Library
open FSharp.Compiler.AbstractIL.NativeRes
open FSharp.Compiler.IO
#if FX_NO_CORHOST_SIGNER
open FSharp.Compiler.AbstractIL.StrongNameSign
#endif

let DateTime1970Jan01 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc) (* ECMA Spec (Oct2002), Part II, 24.2.2 PE File Header. *)
let absilWriteGetTimeStamp () = (DateTime.UtcNow - DateTime1970Jan01).TotalSeconds |> int

// Force inline, so GetLastWin32Error calls are immediately after interop calls as seen by FxCop under Debug build.
let inline ignore _x = ()

// Native Resource linking/unlinking
type IStream = System.Runtime.InteropServices.ComTypes.IStream

let check _action (hresult) =
  if uint32 hresult >= 0x80000000ul then
    System.Runtime.InteropServices.Marshal.ThrowExceptionForHR hresult
  //printf "action = %s, hresult = 0x%nx \n" action hresult

let MAX_PATH = 260

let E_FAIL = 0x80004005

let bytesToWord ((b0: byte), (b1: byte)) =
    int16 b0 ||| (int16 b1 <<< 8)

let bytesToDWord ((b0: byte), (b1: byte), (b2: byte), (b3: byte)) =
    int b0 ||| (int b1 <<< 8) ||| (int b2 <<< 16) ||| (int b3 <<< 24)

let bytesToQWord ((b0: byte), (b1: byte), (b2: byte), (b3: byte), (b4: byte), (b5: byte), (b6: byte), (b7: byte)) =
    int64 b0 ||| (int64 b1 <<< 8) ||| (int64 b2 <<< 16) ||| (int64 b3 <<< 24) ||| (int64 b4 <<< 32) ||| (int64 b5 <<< 40) ||| (int64 b6 <<< 48) ||| (int64 b7 <<< 56)

let dwToBytes n = [| byte (n &&& 0xff) ; byte ((n >>> 8) &&& 0xff) ; byte ((n >>> 16) &&& 0xff) ; byte ((n >>> 24) &&& 0xff) |], 4
let wToBytes (n: int16) = [| byte (n &&& 0xffs) ; byte ((n >>> 8) &&& 0xffs) |], 2

// REVIEW: factor these classes under one hierarchy, use reflection for creation from buffer and toBytes()
// Though, everything I'd like to unify is static - metaclasses?
type IMAGE_FILE_HEADER (m: int16, secs: int16, tds: int32, ptst: int32, nos: int32, soh: int16, c: int16) =
        let mutable machine = m
        let mutable numberOfSections = secs
        let mutable timeDateStamp = tds
        let mutable pointerToSymbolTable = ptst
        let mutable numberOfSymbols = nos
        let mutable sizeOfOptionalHeader = soh
        let mutable characteristics = c

        member x.Machine
            with get() = machine
            and set value = machine <- value

        member x.NumberOfSections
            with get() = numberOfSections
            and set value = numberOfSections <- value

        member x.TimeDateStamp
            with get() = timeDateStamp
            and set value = timeDateStamp <- value

        member x.PointerToSymbolTable
            with get() = pointerToSymbolTable
            and set value = pointerToSymbolTable <- value

        member x.NumberOfSymbols
            with get() = numberOfSymbols
            and set value = numberOfSymbols <- value

        member x.SizeOfOptionalHeader
            with get() = sizeOfOptionalHeader
            and set value = sizeOfOptionalHeader <- value

        member x.Characteristics
            with get() = characteristics
            and set value = characteristics <- value

        static member Width
            with get() = 20

        member x.toBytes () =
            use buf = ByteBuffer.Create IMAGE_FILE_HEADER.Width
            buf.EmitUInt16 (uint16 machine)
            buf.EmitUInt16 (uint16 numberOfSections)
            buf.EmitInt32 timeDateStamp
            buf.EmitInt32 pointerToSymbolTable
            buf.EmitInt32 numberOfSymbols
            buf.EmitUInt16 (uint16 sizeOfOptionalHeader)
            buf.EmitUInt16 (uint16 characteristics)
            buf.AsMemory().ToArray()

let bytesToIFH (buffer: byte[]) (offset: int) =
    if (buffer.Length - offset) < IMAGE_FILE_HEADER.Width then
        invalidArg "buffer" "buffer too small to fit an IMAGE_FILE_HEADER"
    IMAGE_FILE_HEADER( bytesToWord(buffer.[offset], buffer.[offset+1]), // Machine
            bytesToWord(buffer.[offset+2], buffer.[offset+3]), // NumberOfSections
            bytesToDWord(buffer.[offset+4], buffer.[offset+5], buffer.[offset+6], buffer.[offset+7]), // TimeDateStamp
            bytesToDWord(buffer.[offset+8], buffer.[offset+9], buffer.[offset+10], buffer.[offset+11]), // PointerToSymbolTable
            bytesToDWord(buffer.[offset+12], buffer.[offset+13], buffer.[offset+14], buffer.[offset+15]), // NumberOfSymbols
            bytesToWord(buffer.[offset+16], buffer.[offset+17]), // SizeOfOptionalHeader
            bytesToWord(buffer.[offset+18], buffer.[offset+19])) // Characteristics

type IMAGE_SECTION_HEADER(n: int64, ai: int32, va: int32, srd: int32, prd: int32, pr: int32, pln: int32, nr: int16, nl: int16, c: int32) =
        let mutable name = n
        let mutable addressInfo = ai // PhysicalAddress / VirtualSize
        let mutable virtualAddress = va
        let mutable sizeOfRawData = srd
        let mutable pointerToRawData = prd
        let mutable pointerToRelocations = pr
        let mutable pointerToLineNumbers = pln
        let mutable numberOfRelocations = nr
        let mutable numberOfLineNumbers = nl
        let mutable characteristics = c

        member x.Name
            with get() = name
            and set value = name <- value

        member x.PhysicalAddress
            with get() = addressInfo
            and set value = addressInfo <- value

        member x.VirtualSize
            with get() = addressInfo
            and set value = addressInfo <- value

        member x.VirtualAddress
            with get() = virtualAddress
            and set value = virtualAddress <- value

        member x.SizeOfRawData
            with get() = sizeOfRawData
            and set value = sizeOfRawData <- value

        member x.PointerToRawData
            with get() = pointerToRawData
            and set value = pointerToRawData <- value

        member x.PointerToRelocations
            with get() = pointerToRelocations
            and set value = pointerToRelocations <- value

        member x.PointerToLineNumbers
            with get() = pointerToLineNumbers
            and set value = pointerToLineNumbers <- value

        member x.NumberOfRelocations
            with get() = numberOfRelocations
            and set value = numberOfRelocations <- value

        member x.NumberOfLineNumbers
            with get() = numberOfLineNumbers
            and set value = numberOfLineNumbers <- value

        member x.Characteristics
            with get() = characteristics
            and set value = characteristics <- value

        static member Width
            with get() = 40

        member x.toBytes () =
            use buf = ByteBuffer.Create IMAGE_SECTION_HEADER.Width
            buf.EmitInt64 name
            buf.EmitInt32 addressInfo
            buf.EmitInt32 virtualAddress
            buf.EmitInt32 sizeOfRawData
            buf.EmitInt32 pointerToRawData
            buf.EmitInt32 pointerToRelocations
            buf.EmitInt32 pointerToLineNumbers
            buf.EmitUInt16 (uint16 numberOfRelocations)
            buf.EmitUInt16 (uint16 numberOfLineNumbers)
            buf.EmitInt32 characteristics
            buf.AsMemory().ToArray()


let bytesToISH (buffer: byte[]) (offset: int) =
    if (buffer.Length - offset) < IMAGE_SECTION_HEADER.Width then
        invalidArg "buffer" "buffer too small to fit an IMAGE_SECTION_HEADER"
    IMAGE_SECTION_HEADER(bytesToQWord(buffer.[offset], buffer.[offset+1], buffer.[offset+2], buffer.[offset+3], buffer.[offset+4], buffer.[offset+5], buffer.[offset+6], buffer.[offset+7]), // Name
            bytesToDWord(buffer.[offset+8], buffer.[offset+9], buffer.[offset+10], buffer.[offset+11]), // AddressInfo
            bytesToDWord(buffer.[offset+12], buffer.[offset+13], buffer.[offset+14], buffer.[offset+15]), // VirtualAddress
            bytesToDWord(buffer.[offset+16], buffer.[offset+17], buffer.[offset+18], buffer.[offset+19]), // SizeOfRawData
            bytesToDWord(buffer.[offset+20], buffer.[offset+21], buffer.[offset+22], buffer.[offset+23]), // PointerToRawData
            bytesToDWord(buffer.[offset+24], buffer.[offset+25], buffer.[offset+26], buffer.[offset+27]), // PointerToRelocations
            bytesToDWord(buffer.[offset+28], buffer.[offset+29], buffer.[offset+30], buffer.[offset+31]), // PointerToLineNumbers
            bytesToWord(buffer.[offset+32], buffer.[offset+33]), // NumberOfRelocations
            bytesToWord(buffer.[offset+34], buffer.[offset+35]), // NumberOfLineNumbers
            bytesToDWord(buffer.[offset+36], buffer.[offset+37], buffer.[offset+38], buffer.[offset+39])) // Characteristics

type IMAGE_SYMBOL(n: int64, v: int32, sn: int16, t: int16, sc: byte, nas: byte) =
        let mutable name = n
        let mutable value = v
        let mutable sectionNumber = sn
        let mutable stype = t
        let mutable storageClass = sc
        let mutable numberOfAuxSymbols = nas

        member x.Name
            with get() = name
            and set v = name <- v

        member x.Value
            with get() = value
            and set v = value <- v

        member x.SectionNumber
            with get() = sectionNumber
            and set v = sectionNumber <- v

        member x.Type
            with get() = stype
            and set v = stype <- v

        member x.StorageClass
            with get() = storageClass
            and set v = storageClass <- v

        member x.NumberOfAuxSymbols
            with get() = numberOfAuxSymbols
            and set v = numberOfAuxSymbols <- v

        static member Width
            with get() = 18

        member x.toBytes() =
            use buf = ByteBuffer.Create IMAGE_SYMBOL.Width
            buf.EmitInt64 name
            buf.EmitInt32 value
            buf.EmitUInt16 (uint16 sectionNumber)
            buf.EmitUInt16 (uint16 stype)
            buf.EmitByte storageClass
            buf.EmitByte numberOfAuxSymbols
            buf.AsMemory().ToArray()

let bytesToIS (buffer: byte[]) (offset: int) =
    if (buffer.Length - offset) < IMAGE_SYMBOL.Width then
        invalidArg "buffer" "buffer too small to fit an IMAGE_SYMBOL"
    IMAGE_SYMBOL( bytesToQWord(buffer.[offset], buffer.[offset+1], buffer.[offset+2], buffer.[offset+3], buffer.[offset+4], buffer.[offset+5], buffer.[offset+6], buffer.[offset+7]), // Name
            bytesToDWord(buffer.[offset+8], buffer.[offset+9], buffer.[offset+10], buffer.[offset+11]), // Value
            bytesToWord(buffer.[offset+12], buffer.[offset+13]), // SectionNumber
            bytesToWord(buffer.[offset+14], buffer.[offset+15]), // Type
            buffer.[offset+16], // StorageClass
            buffer.[offset+17]) // NumberOfAuxSymbols

type IMAGE_RELOCATION(va: int32, sti: int32, t: int16) =
    let mutable virtualAddress = va // Also RelocCount
    let mutable symbolTableIndex = sti
    let mutable ty = t // type

    member x.VirtualAddress
        with get() = virtualAddress
        and set v = virtualAddress <- v

    member x.RelocCount
        with get() = virtualAddress
        and set v = virtualAddress <- v

    member x.SymbolTableIndex
        with get() = symbolTableIndex
        and set v = symbolTableIndex <- v

    member x.Type
        with get() = ty
        and set v = ty <- v

    static member Width
        with get() = 10

    member x.toBytes() =
        use buf = ByteBuffer.Create IMAGE_RELOCATION.Width
        buf.EmitInt32 virtualAddress
        buf.EmitInt32 symbolTableIndex
        buf.EmitUInt16 (uint16 ty)
        buf.AsMemory().ToArray()

let bytesToIR (buffer: byte[]) (offset: int) =
    if (buffer.Length - offset) < IMAGE_RELOCATION.Width then
        invalidArg "buffer" "buffer too small to fit an IMAGE_RELOCATION"
    IMAGE_RELOCATION( bytesToDWord(buffer.[offset], buffer.[offset+1], buffer.[offset+2], buffer.[offset+3]),
            bytesToDWord(buffer.[offset+4], buffer.[offset+5], buffer.[offset+6], buffer.[offset+7]),
            bytesToWord(buffer.[offset+8], buffer.[offset+9]))

type IMAGE_RESOURCE_DIRECTORY(c: int32, tds: int32, mjv: int16, mnv: int16, nne: int16, nie: int16) =
    let mutable characteristics = c
    let mutable timeDateStamp = tds
    let mutable majorVersion = mjv
    let mutable minorVersion = mnv
    let mutable numberOfNamedEntries = nne
    let mutable numberOfIdEntries = nie

    member x.Characteristics
        with get() = characteristics
        and set v = characteristics <- v

    member x.TimeDateStamp
        with get() = timeDateStamp
        and set v = timeDateStamp <- v

    member x.MajorVersion
        with get() = majorVersion
        and set v = majorVersion <- v

    member x.MinorVersion
        with get() = minorVersion
        and set v = minorVersion <- v

    member x.NumberOfNamedEntries
        with get() = numberOfNamedEntries
        and set v = numberOfNamedEntries <- v

    member x.NumberOfIdEntries
        with get() = numberOfIdEntries
        and set v = numberOfIdEntries <- v

    static member Width = 16

    member x.toBytes () =
        use buf = ByteBuffer.Create IMAGE_RESOURCE_DIRECTORY.Width
        buf.EmitInt32 characteristics
        buf.EmitInt32 timeDateStamp
        buf.EmitUInt16 (uint16 majorVersion)
        buf.EmitUInt16 (uint16 minorVersion)
        buf.EmitUInt16 (uint16 numberOfNamedEntries)
        buf.EmitUInt16 (uint16 numberOfIdEntries)
        buf.AsMemory().ToArray()

let bytesToIRD (buffer: byte[]) (offset: int) =
    if (buffer.Length - offset) < IMAGE_RESOURCE_DIRECTORY.Width then
        invalidArg "buffer" "buffer too small to fit an IMAGE_RESOURCE_DIRECTORY"
    IMAGE_RESOURCE_DIRECTORY( bytesToDWord(buffer.[offset], buffer.[offset+1], buffer.[offset+2], buffer.[offset+3]), // Characteristics
        bytesToDWord(buffer.[offset+4], buffer.[offset+5], buffer.[offset+6], buffer.[offset+7]), // TimeDateStamp
        bytesToWord(buffer.[offset+8], buffer.[offset+9]), // MajorVersion
        bytesToWord(buffer.[offset+10], buffer.[offset+11]), // MinorVersion
        bytesToWord(buffer.[offset+12], buffer.[offset+13]), // NumberOfNamedEntries
        bytesToWord(buffer.[offset+14], buffer.[offset+15])) // NumberOfIdEntries

type IMAGE_RESOURCE_DIRECTORY_ENTRY(n: int32, o: int32) =
    let mutable name = n
    let mutable offset = o

    member x.Name
        with get() = name
        and set v = name <- v

    member x.OffsetToData
        with get() = offset
        and set v = offset <- v

    member x.OffsetToDirectory
        with get() = offset &&& 0x7fffffff

    member x.DataIsDirectory
        with get() = (offset &&& 0x80000000) <> 0

    static member Width = 8

    member x.toBytes () =
        use buf = ByteBuffer.Create IMAGE_RESOURCE_DIRECTORY_ENTRY.Width
        buf.EmitInt32 name
        buf.EmitInt32 offset
        buf.AsMemory().ToArray()

let bytesToIRDE (buffer: byte[]) (offset: int) =
    if (buffer.Length - offset) < IMAGE_RESOURCE_DIRECTORY_ENTRY.Width then
        invalidArg "buffer" "buffer too small to fit an IMAGE_RESOURCE_DIRECTORY_ENTRY"
    IMAGE_RESOURCE_DIRECTORY_ENTRY( bytesToDWord(buffer.[offset], buffer.[offset+1], buffer.[offset+2], buffer.[offset+3]), // Name
        bytesToDWord(buffer.[offset+4], buffer.[offset+5], buffer.[offset+6], buffer.[offset+7])) // Offset

type IMAGE_RESOURCE_DATA_ENTRY(o: int32, s: int32, c: int32, r: int32) =
    let mutable offsetToData = o
    let mutable size = s
    let mutable codePage = c
    let mutable reserved = r

    member x.OffsetToData
        with get() = offsetToData
        and set v = offsetToData <- v
    member x.Size
        with get() = size
        and set v = size <- v
    member x.CodePage
        with get() = codePage
        and set v = codePage <- v
    member x.Reserved
        with get() = reserved
        and set v = reserved <- v

    static member Width = 16

    member x.toBytes() =
        use buf = ByteBuffer.Create IMAGE_RESOURCE_DATA_ENTRY.Width
        buf.EmitInt32 offsetToData
        buf.EmitInt32 size
        buf.EmitInt32 codePage
        buf.EmitInt32 reserved

let bytesToIRDataE (buffer: byte[]) (offset: int) =
    if (buffer.Length - offset) < IMAGE_RESOURCE_DATA_ENTRY.Width then
        invalidArg "buffer" "buffer too small to fit an IMAGE_RESOURCE_DATA_ENTRY"
    IMAGE_RESOURCE_DATA_ENTRY(bytesToDWord(buffer.[offset], buffer.[offset+1], buffer.[offset+2], buffer.[offset+3]), // OffsetToData
        bytesToDWord(buffer.[offset+4], buffer.[offset+5], buffer.[offset+6], buffer.[offset+7]), // Size
        bytesToDWord(buffer.[offset+8], buffer.[offset+9], buffer.[offset+10], buffer.[offset+11]), // CodePage
        bytesToDWord(buffer.[offset+12], buffer.[offset+13], buffer.[offset+14], buffer.[offset+15])) // Reserved


type ResFormatHeader() =
    let mutable dwDataSize = 0
    let mutable dwHeaderSize = 32 // The eventual supposed size of this structure in memory
    let mutable dwTypeID = 0xffff
    let mutable dwNameID = 0xffff
    let mutable dwDataVersion = 0
    let mutable wMemFlags = 0s
    let mutable wLangID = 0s
    let mutable dwVersion = 0
    let mutable dwCharacteristics = 0

    member x.DataSize
        with get() = dwDataSize
        and set v = dwDataSize <- v

    member x.HeaderSize
        with get() = dwHeaderSize
        and set v = dwHeaderSize <- v

    member x.TypeID
        with get() = dwTypeID
        and set v = dwTypeID <- v

    member x.NameID
        with get() = dwNameID
        and set v = dwNameID <- v

    member x.DataVersion
        with get() = dwDataVersion
        and set v = dwDataVersion <- v

    member x.MemFlags
        with get() = wMemFlags
        and set v = wMemFlags <- v

    member x.LangID
        with get() = wLangID
        and set v = wLangID <- v

    member x.Version
        with get() = dwVersion
        and set v = dwVersion <- v

    member x.Characteristics
        with get() = dwCharacteristics
        and set v = dwCharacteristics <- v

    static member Width = 32

    member x.toBytes() =
        use buf = ByteBuffer.Create ResFormatHeader.Width
        buf.EmitInt32 dwDataSize
        buf.EmitInt32 dwHeaderSize
        buf.EmitInt32 dwTypeID
        buf.EmitInt32 dwNameID
        buf.EmitInt32 dwDataVersion
        buf.EmitUInt16 (uint16 wMemFlags)
        buf.EmitUInt16 (uint16 wLangID)
        buf.EmitInt32 dwVersion
        buf.EmitInt32 dwCharacteristics
        buf.AsMemory().ToArray()

type ResFormatNode(tid: int32, nid: int32, lid: int32, dataOffset: int32, pbLinkedResource: byte[]) =
    let mutable resHdr = ResFormatHeader()
    let mutable dataEntry = Unchecked.defaultof<IMAGE_RESOURCE_DATA_ENTRY>
    let mutable cType = 0
    let mutable wzType = Unchecked.defaultof<byte[]>
    let mutable cName = 0
    let mutable wzName = Unchecked.defaultof<byte[]>

    do
        if (tid &&& 0x80000000) <> 0 then // REVIEW: Are names and types mutually exclusive?  The C++ code didn't seem to think so, but I can't find any documentation
            resHdr.TypeID <- 0
            let mtid = tid &&& 0x7fffffff
            cType <- bytesToDWord(pbLinkedResource.[mtid], pbLinkedResource.[mtid+1], pbLinkedResource.[mtid+2], pbLinkedResource.[mtid+3])
            wzType <- Bytes.zeroCreate ((cType + 1) * 2)
            Bytes.blit pbLinkedResource 4 wzType 0 (cType * 2)
        else
            resHdr.TypeID <- (0xffff ||| ((tid &&& 0xffff) <<< 16))

        if (nid &&& 0x80000000) <> 0 then
            resHdr.NameID <- 0
            let mnid = nid &&& 0x7fffffff
            cName <- bytesToDWord(pbLinkedResource.[mnid], pbLinkedResource.[mnid+1], pbLinkedResource.[mnid+2], pbLinkedResource.[mnid+3])
            wzName <- Bytes.zeroCreate ((cName + 1) * 2)
            Bytes.blit pbLinkedResource 4 wzName 0 (cName * 2)
        else
            resHdr.NameID <- (0xffff ||| ((nid &&& 0xffff) <<< 16))

        resHdr.LangID <- (int16)lid
        dataEntry <- bytesToIRDataE pbLinkedResource dataOffset
        resHdr.DataSize <- dataEntry.Size

    member x.ResHdr = resHdr
    member x.DataEntry = dataEntry
    member x.Type = wzType
    member x.Name = wzName

    member x.Save(ulLinkedResourceBaseRVA: int32, pbLinkedResource: byte[], pUnlinkedResource: byte[], offset: int) =
        // Dump them to pUnlinkedResource
        // For each resource write header and data
        let size = ref 0
        let unlinkedResourceOffset = ref 0
        //resHdr.HeaderSize <- 32
        if Unchecked.defaultof<byte[]> <> wzType then
            resHdr.HeaderSize <- resHdr.HeaderSize + ((cType + 1) * 2) - 4
        if Unchecked.defaultof<byte[]> <> wzName then
            resHdr.HeaderSize <- resHdr.HeaderSize + ((cName + 1) * 2) - 4

        let SaveChunk(p: byte[], sz: int) =
            if Unchecked.defaultof<byte[]> <>  pUnlinkedResource then
                Bytes.blit p 0 pUnlinkedResource (!unlinkedResourceOffset + offset) sz
                unlinkedResourceOffset := !unlinkedResourceOffset + sz
            size := !size + sz

            ()

        // ---- Constant part of the header: DWORD, DWORD
        SaveChunk(dwToBytes resHdr.DataSize)
        SaveChunk(dwToBytes resHdr.HeaderSize)

        let mutable dwFiller = 0

        if Unchecked.defaultof<byte[]> <> wzType then
            SaveChunk(wzType, ((cType + 1) * 2))
            dwFiller <- dwFiller + cType + 1
        else
            SaveChunk(dwToBytes resHdr.TypeID)
        if Unchecked.defaultof<byte[]> <> wzName then
            SaveChunk(wzName, ((cName + 1) * 2))
            dwFiller <- dwFiller + cName + 1
        else
            SaveChunk(dwToBytes resHdr.NameID)

        let bNil = Bytes.zeroCreate 3

        // Align remaining fields on DWORD (nb. poor bit twiddling code taken from ildasm's dres.cpp)
        if (dwFiller &&& 0x1) <> 0 then
            SaveChunk(bNil, 2)

        //---- Constant part of the header: DWORD, WORD, WORD, DWORD, DWORD
        SaveChunk(dwToBytes resHdr.DataVersion)
        SaveChunk(wToBytes resHdr.MemFlags)
        SaveChunk(wToBytes resHdr.LangID)
        SaveChunk(dwToBytes resHdr.Version)
        SaveChunk(dwToBytes resHdr.Characteristics)

        //---- Header done, now data
        // just copying to make the code a bit cleaner - can blit if this ends up being a liability
        let pbData = pbLinkedResource.[(dataEntry.OffsetToData - ulLinkedResourceBaseRVA) ..]
        SaveChunk(pbData, dataEntry.Size)

        dwFiller <- dataEntry.Size &&& 0x3
        if dwFiller <> 0 then
            SaveChunk(bNil, 4 - dwFiller)

        !size

let linkNativeResources (unlinkedResources: byte[] list)  (rva: int32) =
   let resources =
       unlinkedResources
       |> Seq.map (fun s -> new MemoryStream(s))
       |> Seq.map (fun s ->
           let res = CvtResFile.ReadResFile s
           s.Dispose()
           res)
       |> Seq.collect id
       // See MakeWin32ResourceList https://github.com/dotnet/roslyn/blob/f40b89234db51da1e1153c14af184e618504be41/src/Compilers/Core/Portable/Compilation/Compilation.cs
       |> Seq.map (fun r ->
           Win32Resource(data = r.data, codePage = 0u, languageId = uint32 r.LanguageId,
                               id = int (int16 r.pstringName.Ordinal), name = r.pstringName.theString,
                               typeId = int (int16 r.pstringType.Ordinal), typeName = r.pstringType.theString))
   let bb = new System.Reflection.Metadata.BlobBuilder()
   NativeResourceWriter.SerializeWin32Resources(bb, resources, rva)
   bb.ToArray()

let unlinkResource (ulLinkedResourceBaseRVA: int32) (pbLinkedResource: byte[]) =
    let mutable nResNodes = 0

    let pirdType = bytesToIRD pbLinkedResource 0
    let mutable pirdeType = Unchecked.defaultof<IMAGE_RESOURCE_DIRECTORY_ENTRY>
    let nEntries = pirdType.NumberOfNamedEntries + pirdType.NumberOfIdEntries

    // determine entry buffer size
    // TODO: coalesce these two loops
    for iEntry = 0 to (int nEntries - 1) do
        pirdeType <- bytesToIRDE pbLinkedResource (IMAGE_RESOURCE_DIRECTORY.Width + (iEntry * IMAGE_RESOURCE_DIRECTORY_ENTRY.Width))

        if pirdeType.DataIsDirectory then
            let nameBase = pirdeType.OffsetToDirectory
            let pirdName = bytesToIRD pbLinkedResource nameBase
            let mutable pirdeName = Unchecked.defaultof<IMAGE_RESOURCE_DIRECTORY_ENTRY>
            let nEntries2 = pirdName.NumberOfNamedEntries + pirdName.NumberOfIdEntries

            for iEntry2 = 0 to (int nEntries2 - 1) do
                pirdeName <- bytesToIRDE pbLinkedResource (nameBase + (iEntry2 * IMAGE_RESOURCE_DIRECTORY_ENTRY.Width))

                if pirdeName.DataIsDirectory then
                    let langBase = pirdeName.OffsetToDirectory
                    let pirdLang = bytesToIRD pbLinkedResource langBase
                    let nEntries3 = pirdLang.NumberOfNamedEntries + pirdLang.NumberOfIdEntries

                    nResNodes <- nResNodes + (int nEntries3)
                else
                    nResNodes <- nResNodes + 1
        else
            nResNodes <- nResNodes + 1

    let pResNodes: ResFormatNode [] = Array.zeroCreate nResNodes
    nResNodes <- 0

    // fill out the entry buffer
    for iEntry = 0 to (int nEntries - 1) do
        pirdeType <- bytesToIRDE pbLinkedResource (IMAGE_RESOURCE_DIRECTORY.Width + (iEntry * IMAGE_RESOURCE_DIRECTORY_ENTRY.Width))
        let dwTypeID = pirdeType.Name
        // Need to skip VERSION and RT_MANIFEST resources
        // REVIEW: ideally we shouldn't allocate space for these, or rename properly so we don't get the naming conflict
        let skipResource = (0x10 = dwTypeID) || (0x18 = dwTypeID)
        if pirdeType.DataIsDirectory then
            let nameBase = pirdeType.OffsetToDirectory
            let pirdName = bytesToIRD pbLinkedResource nameBase
            let mutable pirdeName = Unchecked.defaultof<IMAGE_RESOURCE_DIRECTORY_ENTRY>
            let nEntries2 = pirdName.NumberOfNamedEntries + pirdName.NumberOfIdEntries

            for iEntry2 = 0 to (int nEntries2 - 1) do
                pirdeName <- bytesToIRDE pbLinkedResource (nameBase + (iEntry2 * IMAGE_RESOURCE_DIRECTORY_ENTRY.Width))
                let dwNameID = pirdeName.Name

                if pirdeName.DataIsDirectory then
                    let langBase = pirdeName.OffsetToDirectory
                    let pirdLang = bytesToIRD pbLinkedResource langBase
                    let mutable pirdeLang = Unchecked.defaultof<IMAGE_RESOURCE_DIRECTORY_ENTRY>
                    let nEntries3 = pirdLang.NumberOfNamedEntries + pirdLang.NumberOfIdEntries

                    for iEntry3 = 0 to (int nEntries3 - 1) do
                        pirdeLang <- bytesToIRDE pbLinkedResource (langBase + (iEntry3 * IMAGE_RESOURCE_DIRECTORY_ENTRY.Width))
                        let dwLangID = pirdeLang.Name

                        if pirdeLang.DataIsDirectory then
                            // Resource hierarchy exceeds three levels
                            System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(E_FAIL)
                        else
                            if (not skipResource) then
                                let rfn = ResFormatNode(dwTypeID, dwNameID, dwLangID, pirdeLang.OffsetToData, pbLinkedResource)
                                pResNodes.[nResNodes] <- rfn
                                nResNodes <- nResNodes + 1
                else
                    if (not skipResource) then
                        let rfn = ResFormatNode(dwTypeID, dwNameID, 0, pirdeName.OffsetToData, pbLinkedResource)
                        pResNodes.[nResNodes] <- rfn
                        nResNodes <- nResNodes + 1
        else
            if (not skipResource) then
                let rfn = ResFormatNode(dwTypeID, 0, 0, pirdeType.OffsetToData, pbLinkedResource) // REVIEW: I believe these 0s are what's causing the duplicate res naming problems
                pResNodes.[nResNodes] <- rfn
                nResNodes <- nResNodes + 1

    // Ok, all tree leaves are in ResFormatNode structs, and nResNodes ptrs are in pResNodes
    let mutable size = 0
    if nResNodes <> 0 then
        size <- size + ResFormatHeader.Width ; // sizeof ResFormatHeader
        for i = 0 to (nResNodes - 1) do
            size <- size + pResNodes.[i].Save(ulLinkedResourceBaseRVA, pbLinkedResource, Unchecked.defaultof<byte[]>, 0)

    let pResBuffer = Bytes.zeroCreate size

    if nResNodes <> 0 then
        let mutable resBufferOffset = 0

        // Write a dummy header
        let rfh = ResFormatHeader()
        let rfhBytes = rfh.toBytes()
        Bytes.blit rfhBytes 0 pResBuffer 0 ResFormatHeader.Width
        resBufferOffset <- resBufferOffset + ResFormatHeader.Width

        for i = 0 to (nResNodes - 1) do
            resBufferOffset <- resBufferOffset + pResNodes.[i].Save(ulLinkedResourceBaseRVA, pbLinkedResource, pResBuffer, resBufferOffset)

    pResBuffer

#if !FX_NO_PDB_WRITER
// PDB Writing

[<ComImport; Interface>]
[<Guid("809c652e-7396-11d2-9771-00a0c9b4d50c") ; InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
type IMetaDataDispenser =
    abstract DefineScope: unit -> unit // need this here to fill the first vtable slot
    abstract OpenScope: [<In ; MarshalAs(UnmanagedType.LPWStr)>] szScope: string * [<In>] dwOpenFlags: Int32 * [<In>] riid: System.Guid byref * [<Out ; MarshalAs(UnmanagedType.IUnknown)>] punk: Object byref -> unit

[<ComImport; Interface>]
[<Guid("7DAC8207-D3AE-4c75-9B67-92801A497D44"); InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
[<CLSCompliant(true)>]
type IMetadataImport =
    abstract Placeholder: unit -> unit

[<ComImport; Interface>]
[<Guid("BA3FEE4C-ECB9-4E41-83B7-183FA41CD859"); InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
[<CLSCompliant(true)>]
type IMetadataEmit =
    abstract Placeholder: unit -> unit

[<ComImport; Interface>]
[< Guid("B01FAFEB-C450-3A4D-BEEC-B4CEEC01E006") ; InterfaceType(ComInterfaceType.InterfaceIsIUnknown) >]
[< ComVisible(false) >]
type ISymUnmanagedDocumentWriter =
    abstract SetSource: sourceSize: int *  [<MarshalAs(UnmanagedType.LPArray)>] source: byte[] -> unit
    abstract SetCheckSum: algorithmId: System.Guid * checkSumSize: int * [<MarshalAs(UnmanagedType.LPArray)>]  checkSum: byte [] -> unit

// Struct used to retrieve info on the debug output
[<Struct; StructLayout(LayoutKind.Sequential)>]
type ImageDebugDirectory =
    val Characteristics: int32
    val TimeDateStamp: int32
    val MajorVersion: int16
    val MinorVersion: int16
    val Type: int32
    val SizeOfData: int32
    val AddressOfRawData: int32
    val PointerToRawData: int32

[<ComImport; Interface>]
[<Guid("0B97726E-9E6D-4f05-9A26-424022093CAA"); InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
type ISymUnmanagedWriter2 =
    abstract DefineDocument: [<MarshalAs(UnmanagedType.LPWStr)>] url: string *
                              language: System.Guid byref *
                              languageVendor: System.Guid byref *
                              documentType: System.Guid byref *
                              [<MarshalAs(UnmanagedType.Interface)>] RetVal: ISymUnmanagedDocumentWriter byref -> unit
    abstract SetUserEntryPoint: entryMethod: uint32 -> unit
    abstract OpenMethod: meth: int -> unit
    abstract CloseMethod: unit -> unit
    abstract OpenScope: startOffset: int * pRetVal: int byref -> unit
    abstract CloseScope: endOffset: int -> unit
    abstract SetScopeRange: scopeID: int * startOffset: int * endOffset: int -> unit
    abstract DefineLocalVariable: [<MarshalAs(UnmanagedType.LPWStr)>] varName: string *
                                attributes: int *
                                cSig: int *
                                [<MarshalAs(UnmanagedType.LPArray, SizeParamIndex=2s)>]signature: byte[] *
                                addressKind: int *
                                addr1: int *
                                addr2: int *
                                addr3: int *
                                startOffset: int *
                                endOffset: int -> unit
    abstract DefineParameter: [<MarshalAs(UnmanagedType.LPWStr)>] paramName: string *
                            attributes: int *
                            sequence: int *
                            addressKind: int *
                            addr1: int *
                            addr2: int *
                            addr3: int -> unit
    abstract DefineField: parent: int *
                      [<MarshalAs(UnmanagedType.LPWStr)>] fieldName: string *
                      attributes: int *
                      cSig: int *
                      [<MarshalAs(UnmanagedType.LPArray, SizeParamIndex=3s)>]signature: byte[] *
                      addressKind: int *
                      addr1: int *
                      addr2: int *
                      addr3: int -> unit
    abstract DefineGlobalVariable: [<MarshalAs(UnmanagedType.LPWStr)>] globalVarName: string *
                                  attributes: int *
                                  cSig: int *
                                  [<MarshalAs(UnmanagedType.LPArray, SizeParamIndex=2s)>]signature: byte[] *
                                  addressKind: int *
                                  addr1: int *
                                  addr2: int *
                                  addr3: int -> unit
    abstract Close: unit -> unit
    abstract SetSymAttribute: parent: int *
                            [<MarshalAs(UnmanagedType.LPWStr)>] attName: string *
                            cData: int *
                            [<MarshalAs(UnmanagedType.LPArray, SizeParamIndex=2s)>]data: byte[] -> unit
    abstract OpenNamespace: [<MarshalAs(UnmanagedType.LPWStr)>] nsname: string -> unit
    abstract CloseNamespace: unit -> unit
    abstract UsingNamespace: [<MarshalAs(UnmanagedType.LPWStr)>] fullName: string -> unit
    abstract SetMethodSourceRange: startDoc: ISymUnmanagedDocumentWriter *
                                  startLine: int *
                                  startColumn: int *
                                  endDoc: ISymUnmanagedDocumentWriter *
                                  endLine: int *
                                  endColumn: int -> unit
    abstract Initialize: emitter: nativeint *
                    [<MarshalAs(UnmanagedType.LPWStr)>] filename: string *
                    stream: IStream *
                    fullBuild: bool -> unit
    abstract GetDebugInfo: iDD: ImageDebugDirectory byref *
                          cData: int *
                          pcData: int byref *
                          [<MarshalAs(UnmanagedType.LPArray, SizeParamIndex=1s)>]data: byte[] -> unit
    abstract DefineSequencePoints: document: ISymUnmanagedDocumentWriter *
                                  spCount: int *
                                  [<MarshalAs(UnmanagedType.LPArray, SizeParamIndex=1s)>]offsets: int [] *
                                  [<MarshalAs(UnmanagedType.LPArray, SizeParamIndex=1s)>]lines: int [] *
                                  [<MarshalAs(UnmanagedType.LPArray, SizeParamIndex=1s)>]columns: int [] *
                                  [<MarshalAs(UnmanagedType.LPArray, SizeParamIndex=1s)>]endLines: int [] *
                                  [<MarshalAs(UnmanagedType.LPArray, SizeParamIndex=1s)>]endColumns: int [] -> unit
    abstract RemapToken: oldToken: int * newToken: int -> unit
    abstract Initialize2: emitter: nativeint *
                    [<MarshalAs(UnmanagedType.LPWStr)>] tempFileName: string *
                    stream: IStream *
                    fullBuild: bool *
                    [<MarshalAs(UnmanagedType.LPWStr)>] finalFileName: string -> unit
    abstract DefineConstant: [<MarshalAs(UnmanagedType.LPWStr)>] constName: string *
                            value: Object *
                            cSig: int *
                            [<MarshalAs(UnmanagedType.LPArray, SizeParamIndex=2s)>]signature: byte[] -> unit
    abstract Abort: unit -> unit
    abstract DefineLocalVariable2: [<MarshalAs(UnmanagedType.LPWStr)>] localVarName2: string *
                                  attributes: int *
                                  sigToken: int *
                                  addressKind: int *
                                  addr1: int *
                                  addr2: int *
                                  addr3: int *
                                  startOffset: int *
                                  endOffset: int -> unit
    abstract DefineGlobalVariable2: [<MarshalAs(UnmanagedType.LPWStr)>] globalVarName2: string *
                                    attributes: int *
                                    sigToken: int *
                                    addressKind: int *
                                    addr1: int *
                                    addr2: int *
                                    addr3: int -> unit
    abstract DefineConstant2: [<MarshalAs(UnmanagedType.LPWStr)>] constantName2: string *
                              value: Object *
                              sigToken: int -> unit
    abstract OpenMethod2: method2: int *
                          isect: int *
                          offset: int -> unit

type PdbWriter = { symWriter: ISymUnmanagedWriter2 }
type PdbDocumentWriter = { symDocWriter: ISymUnmanagedDocumentWriter }  (* pointer to pDocumentWriter COM object *)
type idd =
    { iddCharacteristics: int32
      iddMajorVersion: int32; (* actually u16 in IMAGE_DEBUG_DIRECTORY *)
      iddMinorVersion: int32; (* actually u16 in IMAGE_DEBUG_DIRECTORY *)
      iddType: int32
      iddData: byte[] }
#endif

#if !FX_NO_PDB_WRITER
let pdbInitialize (binaryName: string) (pdbName: string) =
    // collect necessary COM types
    let CorMetaDataDispenser = System.Type.GetTypeFromProgID("CLRMetaData.CorMetaDataDispenser")

    // get the importer pointer
    let mdd = System.Activator.CreateInstance(CorMetaDataDispenser) :?> IMetaDataDispenser
    let mutable IID_IMetaDataEmit = new Guid("BA3FEE4C-ECB9-4E41-83B7-183FA41CD859")
    let mutable o = Object()
    mdd.OpenScope(binaryName, 0x1, &IID_IMetaDataEmit, &o) // 0x1 = ofWrite
    let emitterPtr = Marshal.GetComInterfaceForObject(o, typeof<IMetadataEmit>)
    let writer =
        try
            let writer = Activator.CreateInstance(System.Type.GetTypeFromProgID("CorSymWriter_SxS")) :?> ISymUnmanagedWriter2
            writer.Initialize(emitterPtr, pdbName, Unchecked.defaultof<IStream>, true)
            writer
        finally
            // Marshal.GetComInterfaceForObject adds an extra ref for emitterPtr
            if IntPtr.Zero <> emitterPtr then
                Marshal.Release emitterPtr |> ignore

    { symWriter = writer }


[<assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2001: AvoidCallingProblematicMethods", Scope="member", Target="FSharp.Compiler.AbstractIL.Support.#pdbClose(FSharp.Compiler.AbstractIL.Support+PdbWriter)", MessageId="System.GC.Collect")>]
do()

let pdbCloseDocument(documentWriter: PdbDocumentWriter) =
    Marshal.ReleaseComObject (documentWriter.symDocWriter)
    |> ignore

[<System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2001: AvoidCallingProblematicMethods", MessageId="System.GC.Collect")>]
let pdbClose (writer: PdbWriter) dllFilename pdbFilename =
    writer.symWriter.Close()
    // CorSymWriter objects (ISymUnmanagedWriter) lock the files they're operating
    // on (both the pdb and the binary).  The locks are released only when their ref
    // count reaches zero, but since we're dealing with RCWs, there's no telling when
    // that will be.  The result is that sometimes, the pdb and object files will
    // still be locked well after the call to this function.
    // The SymReader class gets around this problem  by implementing the ISymUnmanagedDispose
    // interface, which the SymWriter class, unfortunately, does not.
    // Right now, take the same approach as mdbg, and manually forcing a collection.
    let rc = Marshal.ReleaseComObject(writer.symWriter)
    for i = 0 to (rc - 1) do
      Marshal.ReleaseComObject(writer.symWriter) |> ignore

    let isLocked filename =
        try
            use x = FileSystem.OpenFileForWriteShim(filename, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
            false
        with
        | _ -> true

    let mutable attempts = 0
    while (isLocked dllFilename || isLocked pdbFilename)  && attempts < 3 do
        // Need to induce two full collections for finalizers to run
        System.GC.Collect()
        System.GC.Collect()
        System.GC.WaitForPendingFinalizers()
        attempts <- attempts + 1

let pdbSetUserEntryPoint (writer: PdbWriter) (entryMethodToken: int32) =
    writer.symWriter.SetUserEntryPoint((uint32)entryMethodToken)

// Document checksum algorithms

let guidSourceHashMD5 = System.Guid(0x406ea660u, 0x64cfus, 0x4c82us, 0xb6uy, 0xf0uy, 0x42uy, 0xd4uy, 0x81uy, 0x72uy, 0xa7uy, 0x99uy) //406ea660-64cf-4c82-b6f0-42d48172a799
let hashSizeOfMD5 = 16

// If the FIPS algorithm policy is enabled on the computer (e.g., for US government employees and contractors)
// then obtaining the MD5 implementation in BCL will throw.
// In this case, catch the failure, and not set a checksum.
let internal setCheckSum (url: string, writer: ISymUnmanagedDocumentWriter) =
    try
        use file = FileSystem.OpenFileForReadShim(url)
        use md5 = System.Security.Cryptography.MD5.Create()
        let checkSum = md5.ComputeHash file
        if (checkSum.Length = hashSizeOfMD5) then
            writer.SetCheckSum (guidSourceHashMD5, hashSizeOfMD5, checkSum)
    with _ -> ()

let pdbDefineDocument (writer: PdbWriter) (url: string) =
    //3F5162F8-07C6-11D3-9053-00C04FA302A1
    //let mutable corSymLanguageTypeCSharp = System.Guid(0x3F5162F8u, 0x07C6us, 0x11D3us, 0x90uy, 0x53uy, 0x00uy, 0xC0uy, 0x4Fuy, 0xA3uy, 0x02uy, 0xA1uy)
    let mutable corSymLanguageTypeFSharp = System.Guid(0xAB4F38C9u, 0xB6E6us, 0x43baus, 0xBEuy, 0x3Buy, 0x58uy, 0x08uy, 0x0Buy, 0x2Cuy, 0xCCuy, 0xE3uy)
    let mutable corSymLanguageVendorMicrosoft = System.Guid(0x994b45c4u, 0xe6e9us, 0x11d2us, 0x90uy, 0x3fuy, 0x00uy, 0xc0uy, 0x4fuy, 0xa3uy, 0x02uy, 0xa1uy)
    let mutable corSymDocumentTypeText = System.Guid(0x5a869d0bu, 0x6611us, 0x11d3us, 0xbduy, 0x2auy, 0x0uy, 0x0uy, 0xf8uy, 0x8uy, 0x49uy, 0xbduy)
    let mutable docWriter = Unchecked.defaultof<ISymUnmanagedDocumentWriter>
    writer.symWriter.DefineDocument(url, &corSymLanguageTypeFSharp, &corSymLanguageVendorMicrosoft, &corSymDocumentTypeText, &docWriter)
    setCheckSum (url, docWriter)
    { symDocWriter = docWriter }

let pdbOpenMethod (writer: PdbWriter) (methodToken: int32) =
    writer.symWriter.OpenMethod methodToken

let pdbCloseMethod (writer: PdbWriter) =
    writer.symWriter.CloseMethod()

let pdbOpenScope (writer: PdbWriter) (startOffset: int32) =
    let mutable retInt = 0
    writer.symWriter.OpenScope(startOffset, &retInt)
    check "action" (retInt)

let pdbCloseScope (writer: PdbWriter) (endOffset: int32) =
    writer.symWriter.CloseScope endOffset

let pdbDefineLocalVariable (writer: PdbWriter) (name: string) (signature: byte[]) (addr1: int32) =
    writer.symWriter.DefineLocalVariable(name, 0, signature.Length, signature, int System.Diagnostics.SymbolStore.SymAddressKind.ILOffset, addr1, 0, 0, 0, 0)

let pdbSetMethodRange (writer: PdbWriter) (docWriter1: PdbDocumentWriter) (startLine: int) (startCol: int) (docWriter2: PdbDocumentWriter) (endLine: int) (endCol: int) =
    writer.symWriter.SetMethodSourceRange(docWriter1.symDocWriter, startLine, startCol, docWriter2.symDocWriter, endLine, endCol)

let pdbDefineSequencePoints (writer: PdbWriter) (docWriter: PdbDocumentWriter) (pts: (int * int * int * int * int)[])  =
    let offsets = (Array.map (fun (x, _, _, _, _) -> x) pts)
    let lines = (Array.map (fun (_, x, _, _, _) -> x) pts)
    let columns = (Array.map (fun (_, _, x, _, _) -> x) pts)
    let endLines = (Array.map (fun (_, _, _, x, _) -> x) pts)
    let endColumns = (Array.map (fun (_, _, _, _, x) -> x) pts)
    writer.symWriter.DefineSequencePoints(docWriter.symDocWriter, pts.Length, offsets, lines, columns, endLines, endColumns)

let pdbWriteDebugInfo (writer: PdbWriter) =
    let mutable iDD = new ImageDebugDirectory()
    let mutable length = 0
    writer.symWriter.GetDebugInfo(&iDD, 0, &length, null)
    let mutable data: byte [] = Array.zeroCreate length
    writer.symWriter.GetDebugInfo(&iDD, length, &length, data)

    { iddCharacteristics = iDD.Characteristics
      iddMajorVersion = int32 iDD.MajorVersion
      iddMinorVersion = int32 iDD.MinorVersion
      iddType = iDD.Type
      iddData = data}
#endif


#if !FX_NO_PDB_WRITER
// PDB reading
type PdbReader  = { symReader: ISymbolReader }
type PdbDocument  = { symDocument: ISymbolDocument }
type PdbMethod  = { symMethod: ISymbolMethod }
type PdbVariable = { symVariable: ISymbolVariable }
type PdbMethodScope = { symScope: ISymbolScope }

type PdbSequencePoint =
    { pdbSeqPointOffset: int
      pdbSeqPointDocument: PdbDocument
      pdbSeqPointLine: int
      pdbSeqPointColumn: int
      pdbSeqPointEndLine: int
      pdbSeqPointEndColumn: int }

let pdbReadOpen (moduleName: string) (path: string) : PdbReader =
    let CorMetaDataDispenser = System.Type.GetTypeFromProgID("CLRMetaData.CorMetaDataDispenser")
    let mutable IID_IMetaDataImport = new Guid("7DAC8207-D3AE-4c75-9B67-92801A497D44")
    let mdd = System.Activator.CreateInstance(CorMetaDataDispenser) :?> IMetaDataDispenser
    let mutable o: Object = new Object()
    mdd.OpenScope(moduleName, 0, &IID_IMetaDataImport, &o)
    let importerPtr = Marshal.GetComInterfaceForObject(o, typeof<IMetadataImport>)
    try
#if ENABLE_MONO_SUPPORT
        // ISymWrapper.dll is not available as a compile-time dependency for the cross-platform compiler, since it is Windows-only
        // Access it via reflection instead.System.Diagnostics.SymbolStore.SymBinder
        try
            let isym = System.Reflection.Assembly.Load("ISymWrapper, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
            let symbolBinder = isym.CreateInstance("System.Diagnostics.SymbolStore.SymBinder")
            let symbolBinderTy = symbolBinder.GetType()
            let reader = symbolBinderTy.InvokeMember("GetReader", BindingFlags.Public ||| BindingFlags.InvokeMethod ||| BindingFlags.Instance,  null, symbolBinder, [| box importerPtr; box moduleName; box path |])
            { symReader = reader :?> ISymbolReader }
        with _ ->
            { symReader = null }
#else
        let symbolBinder = new System.Diagnostics.SymbolStore.SymBinder()
        { symReader = symbolBinder.GetReader(importerPtr, moduleName, path) }
#endif
    finally
        // Marshal.GetComInterfaceForObject adds an extra ref for importerPtr
        if IntPtr.Zero <> importerPtr then
          Marshal.Release importerPtr |> ignore

// The symbol reader's finalize method will clean up any unmanaged resources.
// If file locks persist, we may want to manually invoke finalize
let pdbReadClose (_reader: PdbReader) : unit = ()

let pdbReaderGetMethod (reader: PdbReader) (token: int32) : PdbMethod =
    { symMethod = reader.symReader.GetMethod(SymbolToken token) }

let pdbReaderGetMethodFromDocumentPosition (reader: PdbReader)  (document: PdbDocument) (line: int) (column: int) : PdbMethod =
    { symMethod = reader.symReader.GetMethodFromDocumentPosition(document.symDocument, line, column) }

let pdbReaderGetDocuments (reader: PdbReader) : PdbDocument[] =
    let arr = reader.symReader.GetDocuments()
    Array.map (fun i -> { symDocument=i }) arr

let pdbReaderGetDocument (reader: PdbReader) (url: string) (language: byte[]) (languageVendor: byte[]) (documentType: byte[]) : PdbDocument =
    { symDocument = reader.symReader.GetDocument(url, Guid language, Guid languageVendor, System.Guid documentType) }

let pdbDocumentGetURL (document: PdbDocument) : string =
    document.symDocument.URL

let pdbDocumentGetType (document: PdbDocument) : byte[] (* guid *) =
    let guid = document.symDocument.DocumentType
    guid.ToByteArray()

let pdbDocumentGetLanguage (document: PdbDocument) : byte[] (* guid *) =
    let guid = document.symDocument.Language
    guid.ToByteArray()

let pdbDocumentGetLanguageVendor (document: PdbDocument) : byte[] =
    let guid = document.symDocument.LanguageVendor
    guid.ToByteArray()

let pdbDocumentFindClosestLine (document: PdbDocument) (line: int) : int =
    document.symDocument.FindClosestLine line

let pdbMethodGetToken (meth: PdbMethod) : int32 =
    let token = meth.symMethod.Token
    token.GetToken()

let pdbMethodGetSequencePoints (meth: PdbMethod) : PdbSequencePoint[] =
    let  pSize = meth.symMethod.SequencePointCount
    let offsets = Array.zeroCreate pSize
    let docs = Array.zeroCreate pSize
    let lines = Array.zeroCreate pSize
    let cols = Array.zeroCreate pSize
    let endLines = Array.zeroCreate pSize
    let endColumns = Array.zeroCreate pSize

    meth.symMethod.GetSequencePoints(offsets, docs, lines, cols, endLines, endColumns)

    Array.init pSize (fun i ->
        { pdbSeqPointOffset = offsets.[i]
          pdbSeqPointDocument = { symDocument = docs.[i] }
          pdbSeqPointLine = lines.[i]
          pdbSeqPointColumn = cols.[i]
          pdbSeqPointEndLine = endLines.[i]
          pdbSeqPointEndColumn = endColumns.[i] })

let pdbScopeGetChildren (scope: PdbMethodScope) : PdbMethodScope[] =
    let arr = scope.symScope.GetChildren()
    Array.map (fun i -> { symScope=i }) arr

let pdbScopeGetOffsets (scope: PdbMethodScope) : int * int =
    (scope.symScope.StartOffset, scope.symScope.EndOffset)

let pdbScopeGetLocals (scope: PdbMethodScope) : PdbVariable[] =
    let arr = scope.symScope.GetLocals()
    Array.map (fun i -> { symVariable=i }) arr

let pdbVariableGetName (variable: PdbVariable) : string =
    variable.symVariable.Name

let pdbVariableGetSignature (variable: PdbVariable) : byte[] =
    variable.symVariable.GetSignature()

// The tuple is (AddressKind, AddressField1)
let pdbVariableGetAddressAttributes (variable: PdbVariable) : (int32 * int32) =
    (int32 variable.symVariable.AddressKind, variable.symVariable.AddressField1)
#endif
