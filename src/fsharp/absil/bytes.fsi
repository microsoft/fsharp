// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Blobs of bytes, cross-compiling
namespace FSharp.Compiler.IO

open System.IO
open System.IO.MemoryMappedFiles

module internal Bytes =
    /// returned int will be 0 <= x <= 255
    val get: byte[] -> int -> int
    val zeroCreate: int -> byte[]
    /// each int must be 0 <= x <= 255
    val ofInt32Array: int[] ->  byte[]
    /// each int will be 0 <= x <= 255

    val blit: byte[] -> int -> byte[] -> int -> int -> unit

    val stringAsUnicodeNullTerminated: string -> byte[]
    val stringAsUtf8NullTerminated: string -> byte[]

/// A view over bytes.
/// May be backed by managed or unmanaged memory, or memory mapped file.
[<AbstractClass>]
type ByteMemory =

    abstract Item: int -> byte with get

    abstract Length: int

    abstract ReadBytes: pos: int * count: int -> byte[]

    abstract ReadInt32: pos: int -> int

    abstract ReadUInt16: pos: int -> uint16

    abstract ReadUtf8String: pos: int * count: int -> string

    abstract Slice: pos: int * count: int -> ByteMemory

    abstract CopyTo: Stream -> unit

    abstract Copy: srcOffset: int * dest: byte[] * destOffset: int * count: int -> unit

    abstract ToArray: unit -> byte[]

    /// Get a stream representation of the backing memory.
    /// Disposing this will not free up any of the backing memory.
    abstract AsStream: unit -> Stream

    /// Get a stream representation of the backing memory.
    /// Disposing this will not free up any of the backing memory.
    /// Stream cannot be written to.
    abstract AsReadOnlyStream: unit -> Stream

[<Struct;NoEquality;NoComparison>]
type ReadOnlyByteMemory =

    new: ByteMemory -> ReadOnlyByteMemory

    member Item: int -> byte with get

    member Length: int

    member ReadBytes: pos: int * count: int -> byte[]

    member ReadInt32: pos: int -> int

    member ReadUInt16: pos: int -> uint16

    member ReadUtf8String: pos: int * count: int -> string

    member Slice: pos: int * count: int -> ReadOnlyByteMemory

    member CopyTo: Stream -> unit

    member Copy: srcOffset: int * dest: byte[] * destOffset: int * count: int -> unit

    member ToArray: unit -> byte[]

    member AsStream: unit -> Stream

[<AutoOpen>]
module MemoryMappedFileExtensions =

    type MemoryMappedFile with

        /// Create a memory mapped file based on the given ByteMemory's contents.
        /// If the given ByteMemory's length is zero or a memory mapped file is not supported, the result will be None.
        static member TryFromByteMemory : bytes: ReadOnlyByteMemory -> MemoryMappedFile option

type ByteMemory with

    member AsReadOnly: unit -> ReadOnlyByteMemory

    /// Empty byte memory.
    static member Empty: ByteMemory

    /// Create a ByteMemory object that has a backing memory mapped file.
    static member FromMemoryMappedFile: MemoryMappedFile -> ByteMemory

    /// Creates a ByteMemory object that has a backing memory mapped file from a file on-disk.
    static member FromFile: path: string * FileAccess * ?canShadowCopy: bool -> ByteMemory

    /// Creates a ByteMemory object that is backed by a raw pointer.
    /// Use with care.
    static member FromUnsafePointer: addr: nativeint * length: int * holder: obj -> ByteMemory

    /// Creates a ByteMemory object that is backed by a byte array with the specified offset and length.
    static member FromArray: bytes: byte[] * offset: int * length: int -> ByteMemory

    /// Creates a ByteMemory object that is backed by a byte array.
    static member FromArray: bytes: byte[] -> ByteMemory

/// Imperative buffers and streams of byte[]
[<Sealed>]
type internal ByteBuffer =
    member Close : unit -> byte[]
    member EmitIntAsByte : int -> unit
    member EmitIntsAsBytes : int[] -> unit
    member EmitByte : byte -> unit
    member EmitBytes : byte[] -> unit
    member EmitByteMemory : ReadOnlyByteMemory -> unit
    member EmitInt32 : int32 -> unit
    member EmitInt64 : int64 -> unit
    member FixupInt32 : pos: int -> value: int32 -> unit
    member EmitInt32AsUInt16 : int32 -> unit
    member EmitBoolAsByte : bool -> unit
    member EmitUInt16 : uint16 -> unit
    member Position : int
    static member Create : int -> ByteBuffer


[<Sealed>]
type internal ByteStream =
    member ReadByte : unit -> byte
    member ReadBytes : int -> ReadOnlyByteMemory
    member ReadUtf8String : int -> string
    member Position : int
    static member FromBytes : ReadOnlyByteMemory * start:int * length:int -> ByteStream

#if LAZY_UNPICKLE
    member CloneAndSeek : int -> ByteStream
    member Skip : int -> unit
#endif

[<Sealed>]
type internal ByteStorage =

    member GetByteMemory : unit -> ReadOnlyByteMemory

    /// Creates a ByteStorage whose backing bytes are the given ByteMemory. Does not make a copy.
    static member FromByteMemory : ReadOnlyByteMemory -> ByteStorage

    /// Creates a ByteStorage whose backing bytes are the given byte array. Does not make a copy.
    static member FromByteArray : byte [] -> ByteStorage

    /// Creates a ByteStorage that has a copy of the given ByteMemory.
    static member FromByteMemoryAndCopy : ReadOnlyByteMemory * useBackingMemoryMappedFile: bool -> ByteStorage

    /// Creates a ByteStorage that has a copy of the given byte array.
    static member FromByteArrayAndCopy : byte [] * useBackingMemoryMappedFile: bool -> ByteStorage