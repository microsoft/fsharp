// Copyright (c) Microsoft Corporation. All Rights Reserved. See License.txt in the project root for license information.
namespace FSharp.Compiler.IO
open System
open System.IO
open System.IO.MemoryMappedFiles
open System.Buffers
open System.Reflection
open System.Threading
open System.Runtime.InteropServices
open FSharp.NativeInterop
open Internal.Utilities.Library

open System.Text

exception IllegalFileNameChar of string * char

#nowarn "9"
module internal Bytes =
    let b0 n =  (n &&& 0xFF)
    let b1 n =  ((n >>> 8) &&& 0xFF)
    let b2 n =  ((n >>> 16) &&& 0xFF)
    let b3 n =  ((n >>> 24) &&& 0xFF)

    let dWw1 n = int32 ((n >>> 32) &&& 0xFFFFFFFFL)
    let dWw0 n = int32 (n          &&& 0xFFFFFFFFL)

    let get (b:byte[]) n = int32 (Array.get b n)
    let zeroCreate n : byte[] = Array.zeroCreate n

    let sub ( b:byte[]) s l = Array.sub b s l
    let blit (a:byte[]) b c d e = Array.blit a b c d e

    let ofInt32Array (arr:int[]) = Array.init arr.Length (fun i -> byte arr.[i])

    let stringAsUtf8NullTerminated (s:string) =
        Array.append (Encoding.UTF8.GetBytes s) (ofInt32Array [| 0x0 |])

    let stringAsUnicodeNullTerminated (s:string) =
        Array.append (Encoding.Unicode.GetBytes s) (ofInt32Array [| 0x0;0x0 |])

[<Experimental("This FCS API/Type is experimental and subject to change.")>]
[<AbstractClass>]
type ByteMemory() =
    abstract Item: int -> byte with get, set
    abstract Length: int
    abstract ReadAllBytes: unit -> byte[]
    abstract ReadBytes: pos: int * count: int -> byte[]
    abstract ReadInt32: pos: int -> int
    abstract ReadUInt16: pos: int -> uint16
    abstract ReadUtf8String: pos: int * count: int -> string
    abstract Slice: pos: int * count: int -> ByteMemory
    abstract CopyTo: Stream -> unit
    abstract Copy: srcOffset: int * dest: byte[] * destOffset: int * count: int -> unit
    abstract ToArray: unit -> byte[]
    abstract AsStream: unit -> Stream
    abstract AsReadOnlyStream: unit -> Stream

[<Experimental("This FCS API/Type is experimental and subject to change.")>]
[<Sealed>]
type ByteArrayMemory(bytes: byte[], offset, length) =
    inherit ByteMemory()

    let checkCount count =
        if count < 0 then
            raise (ArgumentOutOfRangeException("count", "Count is less than zero."))

    do
        if length < 0 || length > bytes.Length then
            raise (ArgumentOutOfRangeException("length"))

        if offset < 0 || (offset + length) > bytes.Length then
            raise (ArgumentOutOfRangeException("offset"))

    override _.Item
        with get i = bytes.[offset + i]
        and set i v = bytes.[offset + i] <- v

    override _.Length = length

    override _.ReadAllBytes () = bytes

    override _.ReadBytes(pos, count) =
        checkCount count
        if count > 0 then
            Array.sub bytes (offset + pos) count
        else
            Array.empty

    override _.ReadInt32 pos =
        let finalOffset = offset + pos
        (uint32 bytes.[finalOffset]) |||
        ((uint32 bytes.[finalOffset + 1]) <<< 8) |||
        ((uint32 bytes.[finalOffset + 2]) <<< 16) |||
        ((uint32 bytes.[finalOffset + 3]) <<< 24)
        |> int

    override _.ReadUInt16 pos =
        let finalOffset = offset + pos
        (uint16 bytes.[finalOffset]) |||
        ((uint16 bytes.[finalOffset + 1]) <<< 8)

    override _.ReadUtf8String(pos, count) =
        checkCount count
        if count > 0 then
            Encoding.UTF8.GetString(bytes, offset + pos, count)
        else
            String.Empty

    override _.Slice(pos, count) =
        checkCount count
        if count > 0 then
            ByteArrayMemory(bytes, offset + pos, count) :> ByteMemory
        else
            ByteArrayMemory(Array.empty, 0, 0) :> ByteMemory

    override _.CopyTo stream =
        if length > 0 then
            stream.Write(bytes, offset, length)

    override _.Copy(srcOffset, dest, destOffset, count) =
        checkCount count
        if count > 0 then
            Array.blit bytes (offset + srcOffset) dest destOffset count

    override _.ToArray() =
        if length > 0 then
            Array.sub bytes offset length
        else
            Array.empty

    override _.AsStream() =
        if length > 0 then
            new MemoryStream(bytes, offset, length) :> Stream
        else
            new MemoryStream([||], 0, 0, false) :> Stream

    override _.AsReadOnlyStream() =
        if length > 0 then
            new MemoryStream(bytes, offset, length, false) :> Stream
        else
            new MemoryStream([||], 0, 0, false) :> Stream

[<Experimental("This FCS API/Type is experimental and subject to change.")>]
[<Sealed>]
type SafeUnmanagedMemoryStream =
    inherit UnmanagedMemoryStream

    val mutable private holder: obj
    val mutable private isDisposed: bool

    new (addr, length, holder) =
        {
            inherit UnmanagedMemoryStream(addr, length)
            holder = holder
            isDisposed = false
        }

    new (addr: nativeptr<byte>, length: int64, capacity: int64, access: FileAccess, holder) =
        {
            inherit UnmanagedMemoryStream(addr, length, capacity, access)
            holder = holder
            isDisposed = false
        }

    override x.Dispose disposing =
        base.Dispose disposing
        x.holder <- null // Null out so it can be collected.

type internal MemoryMappedStream(mmf: MemoryMappedFile, length: int64) = 
    inherit Stream()

    let viewStream = mmf.CreateViewStream(0L, length, MemoryMappedFileAccess.Read)

    member _.ViewStream = viewStream

    override x.CanRead = viewStream.CanRead
    override x.CanWrite = viewStream.CanWrite
    override x.CanSeek = viewStream.CanSeek
    override x.Position with get() = viewStream.Position and set v = (viewStream.Position <- v)
    override x.Length = viewStream.Length
    override x.Flush() = viewStream.Flush()
    override x.Seek(offset, origin) = viewStream.Seek(offset, origin)
    override x.SetLength(value) = viewStream.SetLength(value)
    override x.Write(buffer, offset, count) = viewStream.Write(buffer, offset, count)
    override x.Read(buffer, offset, count) = viewStream.Read(buffer, offset, count)

    override x.Finalize() =
        x.Dispose()

    interface IDisposable with
        override x.Dispose() =
            GC.SuppressFinalize x
            mmf.Dispose()
            viewStream.Dispose()


[<Experimental("This FCS API/Type is experimental and subject to change.")>]
type RawByteMemory(addr: nativeptr<byte>, length: int, holder: obj) =
    inherit ByteMemory ()

    let check i =
        if i < 0 || i >= length then
            raise (ArgumentOutOfRangeException(nameof i))

    let checkCount count =
        if count < 0 then
            raise (ArgumentOutOfRangeException(nameof count, "Count is less than zero."))

    do
        if length < 0 then
            raise (ArgumentOutOfRangeException(nameof length))

    override _.Item
        with get i =
            check i
            NativePtr.add addr i
            |> NativePtr.read
        and set i v =
            check i
            NativePtr.set addr i v

    override _.Length = length

    override this.ReadAllBytes() =
        this.ReadBytes(0, length)

    override _.ReadBytes(pos, count) =
        checkCount count
        if count > 0 then
            check pos
            check (pos + count - 1)
            let res = Bytes.zeroCreate count
            Marshal.Copy(NativePtr.toNativeInt addr + nativeint pos, res, 0, count)
            res
        else
            Array.empty

    override _.ReadInt32 pos =
        check pos
        check (pos + 3)
        let finalAddr = NativePtr.toNativeInt addr + nativeint pos
        uint32(Marshal.ReadByte(finalAddr, 0)) |||
        (uint32(Marshal.ReadByte(finalAddr, 1)) <<< 8) |||
        (uint32(Marshal.ReadByte(finalAddr, 2)) <<< 16) |||
        (uint32(Marshal.ReadByte(finalAddr, 3)) <<< 24)
        |> int

    override _.ReadUInt16 pos =
        check pos
        check (pos + 1)
        let finalAddr = NativePtr.toNativeInt addr + nativeint pos
        uint16(Marshal.ReadByte(finalAddr, 0)) |||
        (uint16(Marshal.ReadByte(finalAddr, 1)) <<< 8)

    override _.ReadUtf8String(pos, count) =
        checkCount count
        if count > 0 then
            check pos
            check (pos + count - 1)
            Encoding.UTF8.GetString(NativePtr.add addr pos, count)
        else
            String.Empty

    override _.Slice(pos, count) =
        checkCount count
        if count > 0 then
            check pos
            check (pos + count - 1)
            RawByteMemory(NativePtr.add addr pos, count, holder) :> ByteMemory
        else
            ByteArrayMemory(Array.empty, 0, 0) :> ByteMemory

    override x.CopyTo stream =
        if length > 0 then
            use stream2 = x.AsStream()
            stream2.CopyTo stream

    override _.Copy(srcOffset, dest, destOffset, count) =
        checkCount count
        if count > 0 then
            check srcOffset
            Marshal.Copy(NativePtr.toNativeInt addr + nativeint srcOffset, dest, destOffset, count)

    override _.ToArray() =
        if length > 0 then
            let res = Array.zeroCreate<byte> length
            Marshal.Copy(NativePtr.toNativeInt addr, res, 0, res.Length)
            res
        else
            Array.empty

    override _.AsStream() =
        if length > 0 then
            new SafeUnmanagedMemoryStream(addr, int64 length, holder) :> Stream
        else
            new MemoryStream([||], 0, 0, false) :> Stream

    override _.AsReadOnlyStream() =
        if length > 0 then
            new SafeUnmanagedMemoryStream(addr, int64 length, int64 length, FileAccess.Read, holder) :> Stream
        else
            new MemoryStream([||], 0, 0, false) :> Stream

[<Struct;NoEquality;NoComparison>]
type ReadOnlyByteMemory(bytes: ByteMemory) =
    member _.Item with get i = bytes.[i]
    member _.Length with get () = bytes.Length
    member _.ReadAllBytes() = bytes.ReadAllBytes()
    member _.ReadBytes(pos, count) = bytes.ReadBytes(pos, count)
    member _.ReadInt32 pos = bytes.ReadInt32 pos
    member _.ReadUInt16 pos = bytes.ReadUInt16 pos
    member _.ReadUtf8String(pos, count) = bytes.ReadUtf8String(pos, count)
    member _.Slice(pos, count) = bytes.Slice(pos, count) |> ReadOnlyByteMemory
    member _.CopyTo stream = bytes.CopyTo stream
    member _.Copy(srcOffset, dest, destOffset, count) = bytes.Copy(srcOffset, dest, destOffset, count)
    member _.ToArray() = bytes.ToArray()
    member _.AsStream() = bytes.AsReadOnlyStream()
    member _.Underlying = bytes

[<AutoOpen>]
module MemoryMappedFileExtensions =

    let private trymmf length copyTo =
        let length = int64 length
        if length = 0L then
            None
        else
            if runningOnMono then
                // mono's MemoryMappedFile implementation throws with null `mapName`, so we use byte arrays instead: https://github.com/mono/mono/issues/1024
                None
            else
                // Try to create a memory mapped file and copy the contents of the given bytes to it.
                // If this fails, then we clean up and return None.
                try
                    let mmf = MemoryMappedFile.CreateNew(null, length, MemoryMappedFileAccess.ReadWrite, MemoryMappedFileOptions.None, HandleInheritability.None)
                    try
                        use stream = mmf.CreateViewStream(0L, length, MemoryMappedFileAccess.ReadWrite)
                        copyTo stream
                        Some mmf
                    with
                    | _ ->
                        mmf.Dispose()
                        None
                with
                | _ ->
                    None

    type MemoryMappedFile with
        static member TryFromByteMemory(bytes: ReadOnlyByteMemory) =
            trymmf (int64 bytes.Length) bytes.CopyTo

        static member TryFromMemory(bytes: ReadOnlyMemory<byte>) =
            let length = int64 bytes.Length
            trymmf length
                (fun stream ->
                    stream.SetLength(stream.Length + length)
                    let span = Span<byte>(stream.PositionPointer |> NativePtr.toVoidPtr, int length)
                    bytes.Span.CopyTo(span)
                    stream.Position <- stream.Position + length
                )

[<RequireQualifiedAccess>]
module internal FileSystemUtils =
    let checkPathForIllegalChars  =
        let chars = new System.Collections.Generic.HashSet<_>(Path.GetInvalidPathChars())
        (fun (path:string) ->
            for c in path do
                if chars.Contains c then raise(IllegalFileNameChar(path, c)))

    let checkSuffix (x:string) (y:string) = x.EndsWithOrdinal(y)

    let hasExtensionWithValidate (validate:bool) (s:string) =
        if validate then (checkPathForIllegalChars s) |> ignore
        let sLen = s.Length
        (sLen >= 1 && s.[sLen - 1] = '.' && s <> ".." && s <> ".")
        || Path.HasExtension(s)

    let hasExtension (s:string) = hasExtensionWithValidate true s

    let chopExtension (s:string) =
        checkPathForIllegalChars s
        if s = "." then "" else // for OCaml compatibility
        if not (hasExtensionWithValidate false s) then
            raise (System.ArgumentException("chopExtension")) // message has to be precisely this, for OCaml compatibility, and no argument name can be set
        Path.Combine (Path.GetDirectoryName s, Path.GetFileNameWithoutExtension(s))

    let fileNameOfPath s =
        checkPathForIllegalChars s
        Path.GetFileName(s)

    let fileNameWithoutExtensionWithValidate (validate:bool) s =
        if validate then checkPathForIllegalChars s |> ignore
        Path.GetFileNameWithoutExtension(s)

    let fileNameWithoutExtension s = fileNameWithoutExtensionWithValidate true s

    let trimQuotes (s:string) =
        s.Trim( [|' '; '\"'|] )

    let hasSuffixCaseInsensitive suffix filename = (* case-insensitive *)
        checkSuffix (String.lowercase filename) (String.lowercase suffix)

    let isDll file = hasSuffixCaseInsensitive ".dll" file

[<Experimental("This FCS API/Type is experimental and subject to change.")>]
type IAssemblyLoader =
    abstract AssemblyLoadFrom: fileName: string -> Assembly
    abstract AssemblyLoad: assemblyName: AssemblyName -> Assembly

[<Experimental("This FCS API/Type is experimental and subject to change.")>]
type DefaultAssemblyLoader() =
    interface IAssemblyLoader with
        member _.AssemblyLoadFrom(fileName: string) = Assembly.UnsafeLoadFrom fileName
        member _.AssemblyLoad(assemblyName: AssemblyName) = Assembly.Load assemblyName

[<Experimental("This FCS API/Type is experimental and subject to change.")>]
type IFileSystem =
    abstract AssemblyLoader: IAssemblyLoader
    abstract OpenFileForReadShim: filePath: string * ?useMemoryMappedFile: bool * ?shouldShadowCopy: bool -> Stream
    abstract OpenFileForWriteShim: filePath: string * ?fileMode: FileMode * ?fileAccess: FileAccess * ?fileShare: FileShare -> Stream
    abstract GetFullPathShim: fileName: string -> string
    abstract GetFullFilePathInDirectoryShim: dir: string -> fileName: string -> string
    abstract IsPathRootedShim: path: string -> bool
    abstract NormalizePathShim: path: string -> string
    abstract IsInvalidPathShim: path: string -> bool
    abstract GetTempPathShim: unit -> string
    abstract GetDirectoryNameShim: path: string -> string
    abstract GetLastWriteTimeShim: fileName: string -> DateTime
    abstract GetCreationTimeShim: path: string -> DateTime
    abstract CopyShim: src: string * dest: string * overwrite: bool -> unit
    abstract FileExistsShim: fileName: string -> bool
    abstract FileDeleteShim: fileName: string -> unit
    abstract DirectoryCreateShim: path: string -> DirectoryInfo
    abstract DirectoryExistsShim: path: string -> bool
    abstract DirectoryDeleteShim: path: string -> unit
    abstract EnumerateFilesShim: path: string * pattern: string -> string seq
    abstract EnumerateDirectoriesShim: path: string -> string seq
    abstract IsStableFileHeuristic: fileName: string -> bool

[<Experimental("This FCS API/Type is experimental and subject to change.")>]
type DefaultFileSystem() as this =
    abstract AssemblyLoader : IAssemblyLoader
    default _.AssemblyLoader = DefaultAssemblyLoader() :> IAssemblyLoader
    
    abstract OpenFileForReadShim: filePath: string * ?useMemoryMappedFile: bool * ?shouldShadowCopy: bool -> Stream
    default _.OpenFileForReadShim(filePath: string, ?useMemoryMappedFile: bool, ?shouldShadowCopy: bool) : Stream =
        let fileMode = FileMode.Open
        let fileAccess = FileAccess.Read
        let fileShare = FileShare.Delete ||| FileShare.ReadWrite
        let shouldShadowCopy = defaultArg shouldShadowCopy false
        let useMemoryMappedFile = defaultArg useMemoryMappedFile false
        let fileStream = new FileStream(filePath, fileMode, fileAccess, fileShare)
        let length = fileStream.Length
        
        // We want to use mmaped files only when:
        //   -  Opening large binary files (no need to use for source or resource files really)
        //   -  Running on mono, since its MemoryMappedFile implementation throws when "mapName" is not provided (is null).
        //      (See: https://github.com/mono/mono/issues/10245)
        
        if runningOnMono || (not useMemoryMappedFile) then
            fileStream :> Stream
        else
            let mmf =
                if shouldShadowCopy then
                    let mmf =
                        MemoryMappedFile.CreateNew(
                            null,
                            length,
                            MemoryMappedFileAccess.Read,
                            MemoryMappedFileOptions.None,
                            HandleInheritability.None)
                    use stream = mmf.CreateViewStream(0L, length, MemoryMappedFileAccess.Read)
                    fileStream.CopyTo(stream)
                    fileStream.Dispose()
                    mmf
                else
                    MemoryMappedFile.CreateFromFile(
                        fileStream,
                        null,
                        length,
                        MemoryMappedFileAccess.Read,
                        HandleInheritability.None,
                        leaveOpen=false)

            let stream = new MemoryMappedStream(mmf, length)

            if not stream.CanRead then
                invalidOp "Cannot read file"
            stream :> Stream


    abstract OpenFileForWriteShim: filePath: string * ?fileMode: FileMode * ?fileAccess: FileAccess * ?fileShare: FileShare -> Stream
    default _.OpenFileForWriteShim(filePath: string, ?fileMode: FileMode, ?fileAccess: FileAccess, ?fileShare: FileShare) : Stream =
        let fileMode = defaultArg fileMode FileMode.OpenOrCreate
        let fileAccess = defaultArg fileAccess FileAccess.ReadWrite
        let fileShare = defaultArg fileShare FileShare.Delete ||| FileShare.ReadWrite

        new FileStream(filePath, fileMode, fileAccess, fileShare) :> Stream

    abstract GetFullPathShim: fileName: string -> string
    default _.GetFullPathShim (fileName: string) = Path.GetFullPath fileName

    abstract GetFullFilePathInDirectoryShim: dir: string -> fileName: string -> string
    default this.GetFullFilePathInDirectoryShim (dir: string) (fileName: string) =
        let p = if (this :> IFileSystem).IsPathRootedShim(fileName) then fileName else Path.Combine(dir, fileName)
        try (this :> IFileSystem).GetFullPathShim(p) with
        | :? ArgumentException
        | :? ArgumentNullException
        | :? NotSupportedException
        | :? PathTooLongException
        | :? System.Security.SecurityException -> p
    
    abstract IsPathRootedShim: path: string -> bool
    default _.IsPathRootedShim (path: string) = Path.IsPathRooted path

    abstract NormalizePathShim: path: string -> string
    default _.NormalizePathShim (path: string) =
        try
            let ifs = this :> IFileSystem
            if ifs.IsPathRootedShim path then
                ifs.GetFullPathShim path
            else
                path
        with _ -> path

    abstract IsInvalidPathShim: path: string -> bool
    default _.IsInvalidPathShim(path: string) =
        let isInvalidPath(p: string) =
            String.IsNullOrEmpty p || p.IndexOfAny(Path.GetInvalidPathChars()) <> -1

        let isInvalidFilename(p: string) =
            String.IsNullOrEmpty p || p.IndexOfAny(Path.GetInvalidFileNameChars()) <> -1

        let isInvalidDirectory(d: string) =
            d=null || d.IndexOfAny(Path.GetInvalidPathChars()) <> -1

        isInvalidPath path ||
        let directory = Path.GetDirectoryName path
        let filename = Path.GetFileName path
        isInvalidDirectory directory || isInvalidFilename filename

    abstract GetTempPathShim: unit -> string
    default _.GetTempPathShim() = Path.GetTempPath()

    abstract GetDirectoryNameShim: path: string -> string   
    default _.GetDirectoryNameShim(path:string) =
        FileSystemUtils.checkPathForIllegalChars path
        if path = "" then "."
        else
          match Path.GetDirectoryName(path) with
          | null -> if (this :> IFileSystem).IsPathRootedShim(path) then path else "."
          | res -> if res = "" then "." else res

    abstract GetLastWriteTimeShim: fileName: string -> DateTime
    default _.GetLastWriteTimeShim (fileName: string) = File.GetLastWriteTimeUtc fileName

    abstract GetCreationTimeShim: path: string -> DateTime
    default _.GetCreationTimeShim (path: string) = File.GetCreationTimeUtc path

    abstract CopyShim: src: string * dest: string * overwrite: bool -> unit
    default _.CopyShim (src: string, dest: string, overwrite: bool) = File.Copy(src, dest, overwrite)

    abstract FileExistsShim: fileName: string -> bool
    default _.FileExistsShim (fileName: string) = File.Exists fileName

    abstract FileDeleteShim: fileName: string -> unit
    default _.FileDeleteShim (fileName: string) = File.Delete fileName

    abstract DirectoryCreateShim: path: string -> DirectoryInfo
    default _.DirectoryCreateShim (path: string) = Directory.CreateDirectory path

    abstract DirectoryExistsShim: path: string -> bool
    default _.DirectoryExistsShim (path: string) = Directory.Exists path

    abstract DirectoryDeleteShim: path: string -> unit
    default _.DirectoryDeleteShim (path: string) = Directory.Delete path

    abstract EnumerateFilesShim: path: string * pattern: string -> string seq
    default _.EnumerateFilesShim(path: string, pattern: string) = Directory.EnumerateFiles(path, pattern)

    abstract EnumerateDirectoriesShim: path: string -> string seq    
    default _.EnumerateDirectoriesShim(path: string) = Directory.EnumerateDirectories(path)
    
    abstract IsStableFileHeuristic: fileName: string -> bool
    default _.IsStableFileHeuristic (fileName: string) =
        let directory = Path.GetDirectoryName fileName
        directory.Contains("Reference Assemblies/") ||
        directory.Contains("Reference Assemblies\\") ||
        directory.Contains("packages/") ||
        directory.Contains("packages\\") ||
        directory.Contains("lib/mono/")
        
    interface IFileSystem with
        member _.AssemblyLoader = this.AssemblyLoader

        member _.OpenFileForReadShim(filePath: string, ?useMemoryMappedFile: bool, ?shouldShadowCopy: bool) : Stream =
            let shouldShadowCopy = defaultArg shouldShadowCopy false
            let useMemoryMappedFile = defaultArg useMemoryMappedFile false
            this.OpenFileForReadShim(filePath, useMemoryMappedFile, shouldShadowCopy)

        member _.OpenFileForWriteShim(filePath: string, ?fileMode: FileMode, ?fileAccess: FileAccess, ?fileShare: FileShare) : Stream =
            let fileMode = defaultArg fileMode FileMode.OpenOrCreate
            let fileAccess = defaultArg fileAccess FileAccess.ReadWrite
            let fileShare = defaultArg fileShare FileShare.Delete ||| FileShare.ReadWrite
            this.OpenFileForWriteShim(filePath, fileMode, fileAccess, fileShare)

        member _.GetFullPathShim (fileName: string) = this.GetFullPathShim fileName
        member _.GetFullFilePathInDirectoryShim (dir: string) (fileName: string) = this.GetFullFilePathInDirectoryShim dir fileName
        member _.IsPathRootedShim (path: string) = this.IsPathRootedShim path
        member _.NormalizePathShim (path: string) = this.NormalizePathShim path
        member _.IsInvalidPathShim(path: string) = this.IsInvalidPathShim path
        member _.GetTempPathShim() = this.GetTempPathShim()
        member _.GetDirectoryNameShim(s:string) = this.GetDirectoryNameShim s
        member _.GetLastWriteTimeShim (fileName: string) = this.GetLastWriteTimeShim fileName
        member _.GetCreationTimeShim (path: string) = this.GetCreationTimeShim path
        member _.CopyShim (src: string, dest: string, overwrite: bool) = this.CopyShim(src, dest, overwrite)
        member _.FileExistsShim (fileName: string) = this.FileExistsShim fileName
        member _.FileDeleteShim (fileName: string) = this.FileDeleteShim fileName
        member _.DirectoryCreateShim (path: string) = this.DirectoryCreateShim path
        member _.DirectoryExistsShim (path: string) = this.DirectoryExistsShim path
        member _.DirectoryDeleteShim (path: string) = this.DirectoryDeleteShim path
        member _.EnumerateFilesShim(path: string, pattern: string) = this.EnumerateFilesShim(path, pattern)
        member _.EnumerateDirectoriesShim(path: string) = this.EnumerateDirectoriesShim path
        member _.IsStableFileHeuristic (fileName: string) = this.IsStableFileHeuristic fileName

[<AutoOpen>]
module public StreamExtensions =
    let utf8noBOM = new UTF8Encoding(false, true) :> Encoding
    type System.IO.Stream with
        member s.GetWriter(?encoding: Encoding) : TextWriter =
            let encoding = defaultArg encoding utf8noBOM
            new StreamWriter(s, encoding) :> TextWriter

        member s.WriteAllLines(contents: string seq, ?encoding: Encoding) =
            let encoding = defaultArg encoding utf8noBOM
            use writer = s.GetWriter(encoding)
            for l in contents do
                writer.WriteLine(l)

        member s.Write (data: 'a) : unit =
            use sw = s.GetWriter()
            sw.Write(data)

        member s.GetReader(codePage: int option, ?retryLocked: bool) =
            let retryLocked = defaultArg retryLocked false
            let retryDelayMilliseconds = 50
            let numRetries = 60
            let rec getSource retryNumber =
              try
                // Use the .NET functionality to auto-detect the unicode encoding
                match codePage with
                | None -> new StreamReader(s, true)
                | Some n -> new StreamReader(s, Encoding.GetEncoding(n))
              with
                  // We can get here if the file is locked--like when VS is saving a file--we don't have direct
                  // access to the HRESULT to see that this is EONOACCESS.
                  | :? IOException as err when retryLocked && err.GetType() = typeof<IOException> ->
                       // This second check is to make sure the exception is exactly IOException and none of these for example:
                       //   DirectoryNotFoundException
                       //   EndOfStreamException
                       //   FileNotFoundException
                       //   FileLoadException
                       //   PathTooLongException
                       if retryNumber < numRetries then
                           System.Threading.Thread.Sleep (retryDelayMilliseconds)
                           getSource (retryNumber + 1)
                       else
                           reraise()
            getSource 0

        member s.ReadBytes (start, len) = 
            s.Seek(int64 start, SeekOrigin.Begin) |> ignore
            let buffer = Array.zeroCreate len 
            let mutable n = 0
            while n < len do 
                n <- n + s.Read(buffer, n, len-n)
            buffer
        
        member s.ReadAllBytes() =
            use reader = new BinaryReader(s)
            let count = (int s.Length)
            reader.ReadBytes(count)
        
        member s.ReadAllText(?encoding: Encoding) =
            let encoding = defaultArg encoding Encoding.UTF8
            use sr = new StreamReader(s, encoding, true)
            sr.ReadToEnd()

        member s.ReadLines(?encoding: Encoding) : string seq =
            let encoding = defaultArg encoding Encoding.UTF8
            seq {
                use sr = new StreamReader(s, encoding, true)
                while not <| sr.EndOfStream do
                    yield sr.ReadLine()
            }
        member s.ReadAllLines(?encoding: Encoding) : string array =
            let encoding = defaultArg encoding Encoding.UTF8
            s.ReadLines(encoding) |> Seq.toArray

        /// If we are working with the view stream from mmf, we wrap it in RawByteMemory (which does zero copy, bu just using handle from the views stream).
        /// However, when we use any other stream (FileStream, MemoryStream, etc) - we just read everything from it and expose via ByteArrayMemory.
        member s.AsByteMemory() : ByteMemory =
            match s with
            | :? MemoryMappedStream as mmfs ->
                let length = mmfs.Length
                RawByteMemory(
                    NativePtr.ofNativeInt (mmfs.ViewStream.SafeMemoryMappedViewHandle.DangerousGetHandle()),
                    int length,
                    mmfs) :> ByteMemory

            | _ ->
                let bytes = s.ReadAllBytes()
                let byteArrayMemory = if bytes.Length = 0 then ByteArrayMemory([||], 0, 0) else ByteArrayMemory(bytes, 0, bytes.Length)
                byteArrayMemory :> ByteMemory

[<AutoOpen>]
module public FileSystemAutoOpens =
    /// The global hook into the file system
    let mutable FileSystem: IFileSystem = DefaultFileSystem() :> IFileSystem

type ByteMemory with

    member x.AsReadOnly() = ReadOnlyByteMemory x

    static member Empty = ByteArrayMemory([||], 0, 0) :> ByteMemory

    static member FromMemoryMappedFile(mmf: MemoryMappedFile) =
        let accessor = mmf.CreateViewAccessor()
        RawByteMemory.FromUnsafePointer(accessor.SafeMemoryMappedViewHandle.DangerousGetHandle(), int accessor.Capacity, (mmf, accessor))

    static member FromUnsafePointer(addr, length, holder: obj) =
        RawByteMemory(NativePtr.ofNativeInt addr, length, holder) :> ByteMemory

    static member FromArray(bytes, offset, length) =
        ByteArrayMemory(bytes, offset, length) :> ByteMemory

    static member FromArray (bytes: byte array) =
        if bytes.Length = 0 then
            ByteMemory.Empty
        else
            ByteArrayMemory.FromArray(bytes, 0, bytes.Length)

type internal ByteStream =
    { bytes: ReadOnlyByteMemory
      mutable pos: int
      max: int }
    member b.ReadByte() =
        if b.pos >= b.max then failwith "end of stream"
        let res = b.bytes.[b.pos]
        b.pos <- b.pos + 1
        res
    member b.ReadUtf8String n =
        let res = b.bytes.ReadUtf8String(b.pos,n)
        b.pos <- b.pos + n; res

    static member FromBytes (b: ReadOnlyByteMemory,start,length) =
        if start < 0 || (start+length) > b.Length then failwith "FromBytes"
        { bytes = b; pos = start; max = start+length }

    member b.ReadBytes n  =
        if b.pos + n > b.max then failwith "ReadBytes: end of stream"
        let res = b.bytes.Slice(b.pos, n)
        b.pos <- b.pos + n
        res

    member b.Position = b.pos
#if LAZY_UNPICKLE
    member b.CloneAndSeek = { bytes=b.bytes; pos=pos; max=b.max }
    member b.Skip = b.pos <- b.pos + n
#endif


type internal ByteBuffer =
    { useArrayPool: bool
      mutable isDisposed: bool
      mutable bbArray: byte[]
      mutable bbCurrent: int }

    member inline private buf.CheckDisposed() =
        if buf.isDisposed then
            raise(ObjectDisposedException(nameof(ByteBuffer)))

    member private buf.Ensure newSize =
        let oldBufSize = buf.bbArray.Length
        if newSize > oldBufSize then
            let old = buf.bbArray
            buf.bbArray <- 
                if buf.useArrayPool then
                    ArrayPool.Shared.Rent (max newSize (oldBufSize * 2))
                else
                    Bytes.zeroCreate (max newSize (oldBufSize * 2))
            Bytes.blit old 0 buf.bbArray 0 buf.bbCurrent
            if buf.useArrayPool then
                ArrayPool.Shared.Return old

    member buf.AsMemory() = 
        buf.CheckDisposed()
        ReadOnlyMemory(buf.bbArray, 0, buf.bbCurrent)

    member buf.EmitIntAsByte (i:int) =
        buf.CheckDisposed()
        let newSize = buf.bbCurrent + 1
        buf.Ensure newSize
        buf.bbArray.[buf.bbCurrent] <- byte i
        buf.bbCurrent <- newSize

    member buf.EmitByte (b:byte) = 
        buf.CheckDisposed()
        buf.EmitIntAsByte (int b)

    member buf.EmitIntsAsBytes (arr:int[]) =
        buf.CheckDisposed()
        let n = arr.Length
        let newSize = buf.bbCurrent + n
        buf.Ensure newSize
        let bbArr = buf.bbArray
        let bbBase = buf.bbCurrent
        for i = 0 to n - 1 do
            bbArr.[bbBase + i] <- byte arr.[i]
        buf.bbCurrent <- newSize

    member bb.FixupInt32 pos value =
        bb.CheckDisposed()
        bb.bbArray.[pos] <- (Bytes.b0 value |> byte)
        bb.bbArray.[pos + 1] <- (Bytes.b1 value |> byte)
        bb.bbArray.[pos + 2] <- (Bytes.b2 value |> byte)
        bb.bbArray.[pos + 3] <- (Bytes.b3 value |> byte)

    member buf.EmitInt32 n =
        buf.CheckDisposed()
        let newSize = buf.bbCurrent + 4
        buf.Ensure newSize
        buf.FixupInt32 buf.bbCurrent n
        buf.bbCurrent <- newSize

    member buf.EmitBytes (i:byte[]) =
        buf.CheckDisposed()
        let n = i.Length
        let newSize = buf.bbCurrent + n
        buf.Ensure newSize
        Bytes.blit i 0 buf.bbArray buf.bbCurrent n
        buf.bbCurrent <- newSize

    member buf.EmitMemory (i:ReadOnlyMemory<byte>) =
        buf.CheckDisposed()
        let n = i.Length
        let newSize = buf.bbCurrent + n
        buf.Ensure newSize
        i.CopyTo(Memory(buf.bbArray, buf.bbCurrent, n))
        buf.bbCurrent <- newSize

    member buf.EmitByteMemory (i:ReadOnlyByteMemory) =
        buf.CheckDisposed()
        let n = i.Length
        let newSize = buf.bbCurrent + n
        buf.Ensure newSize
        i.Copy(0, buf.bbArray, buf.bbCurrent, n)
        buf.bbCurrent <- newSize

    member buf.EmitInt32AsUInt16 n =
        buf.CheckDisposed()
        let newSize = buf.bbCurrent + 2
        buf.Ensure newSize
        buf.bbArray.[buf.bbCurrent] <- (Bytes.b0 n |> byte)
        buf.bbArray.[buf.bbCurrent + 1] <- (Bytes.b1 n |> byte)
        buf.bbCurrent <- newSize

    member buf.EmitBoolAsByte (b:bool) = 
        buf.CheckDisposed()
        buf.EmitIntAsByte (if b then 1 else 0)

    member buf.EmitUInt16 (x:uint16) = 
        buf.CheckDisposed()
        buf.EmitInt32AsUInt16 (int32 x)

    member buf.EmitInt64 x =
        buf.CheckDisposed()
        buf.EmitInt32 (Bytes.dWw0 x)
        buf.EmitInt32 (Bytes.dWw1 x)

    member buf.Position =
        buf.CheckDisposed()
        buf.bbCurrent

    static member Create(capacity, useArrayPool) =
        let useArrayPool = defaultArg useArrayPool false
        { useArrayPool = useArrayPool
          isDisposed = false
          bbArray = if useArrayPool then ArrayPool.Shared.Rent capacity else Bytes.zeroCreate capacity
          bbCurrent = 0 }

    interface IDisposable with

        member this.Dispose() =
            if not this.isDisposed then
                this.isDisposed <- true
                if this.useArrayPool then
                    ArrayPool.Shared.Return this.bbArray

[<Sealed>]
type ByteStorage(getByteMemory: unit -> ReadOnlyByteMemory) =

    let mutable cached = Unchecked.defaultof<WeakReference<ByteMemory>>

    let getAndCache () =
        let byteMemory = getByteMemory ()
        cached <- WeakReference<ByteMemory>(byteMemory.Underlying)
        byteMemory

    member _.GetByteMemory() =
        match cached with
        | null -> getAndCache ()
        | _ ->
            match cached.TryGetTarget() with
            | true, byteMemory -> byteMemory.AsReadOnly()
            | _ -> getAndCache ()

    static member FromByteArray(bytes: byte []) =
        ByteStorage.FromByteMemory(ByteMemory.FromArray(bytes).AsReadOnly())

    static member FromByteMemory(bytes: ReadOnlyByteMemory) =
        ByteStorage(fun () -> bytes)

    static member FromByteMemoryAndCopy(bytes: ReadOnlyByteMemory, useBackingMemoryMappedFile: bool) =
        if useBackingMemoryMappedFile then
            match MemoryMappedFile.TryFromByteMemory(bytes) with
            | Some mmf ->
                ByteStorage(fun () -> ByteMemory.FromMemoryMappedFile(mmf).AsReadOnly())
            | _ ->
                let copiedBytes = ByteMemory.FromArray(bytes.ToArray()).AsReadOnly()
                ByteStorage.FromByteMemory(copiedBytes)
        else
            let copiedBytes = ByteMemory.FromArray(bytes.ToArray()).AsReadOnly()
            ByteStorage.FromByteMemory(copiedBytes)

    static member FromMemoryAndCopy(bytes: ReadOnlyMemory<byte>, useBackingMemoryMappedFile: bool) =
        if useBackingMemoryMappedFile then
            match MemoryMappedFile.TryFromMemory(bytes) with
            | Some mmf ->
                ByteStorage(fun () -> ByteMemory.FromMemoryMappedFile(mmf).AsReadOnly())
            | _ ->
                let copiedBytes = ByteMemory.FromArray(bytes.ToArray()).AsReadOnly()
                ByteStorage.FromByteMemory(copiedBytes)
        else
            let copiedBytes = ByteMemory.FromArray(bytes.ToArray()).AsReadOnly()
            ByteStorage.FromByteMemory(copiedBytes)

    static member FromByteArrayAndCopy(bytes: byte [], useBackingMemoryMappedFile: bool) =
        ByteStorage.FromByteMemoryAndCopy(ByteMemory.FromArray(bytes).AsReadOnly(), useBackingMemoryMappedFile)
