// Copyright (c) Microsoft Corporation. All Rights Reserved. See License.txt in the project root for license information.

//---------------------------------------------------------------------
// The big binary reader
//
//---------------------------------------------------------------------

module FSharp.Compiler.AbstractIL.ILBinaryReader

#nowarn "42" // This construct is deprecated: it is only for use in the F# library

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Collections.Immutable
open System.Diagnostics
open System.IO
open System.IO.MemoryMappedFiles
open System.Text
open Internal.Utilities.Collections
open FSharp.Compiler.AbstractIL.Diagnostics
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AbstractIL.BinaryConstants
open Internal.Utilities.Library
open FSharp.Compiler.AbstractIL.Support
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.IO
open FSharp.Compiler.Text.Range
open System.Reflection
open System.Reflection.PortableExecutable
open FSharp.NativeInterop

#nowarn "9"

let checking = false
let logging = false
let _ = if checking then dprintn "warning: ILBinaryReader.checking is on"
let noStableFileHeuristic = try (System.Environment.GetEnvironmentVariable("FSharp_NoStableFileHeuristic") <> null) with _ -> false
let alwaysMemoryMapFSC = try (System.Environment.GetEnvironmentVariable("FSharp_AlwaysMemoryMapCommandLineCompiler") <> null) with _ -> false
let stronglyHeldReaderCacheSizeDefault = 30
let stronglyHeldReaderCacheSize = try (match System.Environment.GetEnvironmentVariable("FSharp_StronglyHeldBinaryReaderCacheSize") with null -> stronglyHeldReaderCacheSizeDefault | s -> int32 s) with _ -> stronglyHeldReaderCacheSizeDefault

let singleOfBits (x: int32) = System.BitConverter.ToSingle(System.BitConverter.GetBytes x, 0)
let doubleOfBits (x: int64) = System.BitConverter.Int64BitsToDouble x

//---------------------------------------------------------------------
// Utilities.
//---------------------------------------------------------------------

let align alignment n = ((n + alignment - 0x1) / alignment) * alignment

let uncodedToken (tab: TableName) idx = ((tab.Index <<< 24) ||| idx)

let i32ToUncodedToken tok =
    let idx = tok &&& 0xffffff
    let tab = tok >>>& 24
    (TableName.FromIndex tab, idx)


[<Struct>]
type TaggedIndex<'T> =
    val tag: 'T
    val index: int32
    new(tag, index) = { tag=tag; index=index }

let uncodedTokenToTypeDefOrRefOrSpec (tab, tok) =
    let tag =
        if tab = TableNames.TypeDef then tdor_TypeDef
        elif tab = TableNames.TypeRef then tdor_TypeRef
        elif tab = TableNames.TypeSpec then tdor_TypeSpec
        else failwith "bad table in uncodedTokenToTypeDefOrRefOrSpec"
    TaggedIndex(tag, tok)

let uncodedTokenToMethodDefOrRef (tab, tok) =
    let tag =
        if tab = TableNames.Method then mdor_MethodDef
        elif tab = TableNames.MemberRef then mdor_MemberRef
        else failwith "bad table in uncodedTokenToMethodDefOrRef"
    TaggedIndex(tag, tok)

let (|TaggedIndex|) (x: TaggedIndex<'T>) = x.tag, x.index
let inline tokToTaggedIdx f nbits tok =
    let tagmask =
        if nbits = 1 then 1
        elif nbits = 2 then 3
        elif nbits = 3 then 7
        elif nbits = 4 then 15
           elif nbits = 5 then 31
           else failwith "too many nbits"
    let tag = tok &&& tagmask
    let idx = tok >>>& nbits
    TaggedIndex(f tag, idx)

type Statistics =
    { mutable rawMemoryFileCount: int
      mutable memoryMapFileOpenedCount: int
      mutable memoryMapFileClosedCount: int
      mutable weakByteFileCount: int
      mutable byteFileCount: int }

let stats =
    { rawMemoryFileCount = 0
      memoryMapFileOpenedCount = 0
      memoryMapFileClosedCount = 0
      weakByteFileCount = 0
      byteFileCount = 0 }

let GetStatistics() = stats

type private BinaryView = ReadOnlyByteMemory

/// An abstraction over how we access the contents of .NET binaries.
type BinaryFile =
    abstract GetView: unit -> BinaryView

/// Gives views over a raw chunk of memory, for example those returned to us by the memory manager in Roslyn's
/// Visual Studio integration. 'obj' must keep the memory alive. The object will capture it and thus also keep the memory alive for
/// the lifetime of this object.
type RawMemoryFile =
    val mutable private holder: obj
    val mutable private fileName: string
    val mutable private view: ReadOnlyByteMemory

    new (fileName: string, obj: obj, addr: nativeint, length: int) =
        stats.rawMemoryFileCount <- stats.rawMemoryFileCount + 1
        {
            holder = obj
            fileName = fileName
            view = ByteMemory.FromUnsafePointer(addr, length, obj).AsReadOnly()
        }

    new (fileName: string, holder: obj, bmem: ByteMemory) =
        {
            holder = holder // gonna be finalized due to how we pass the holder when create RawByteMemory
            fileName = fileName
            view = bmem.AsReadOnly()
        }

    member r.HoldObj() = r.holder // make sure we capture the holder.
    member r.FileName = r.fileName

    interface BinaryFile with
        override r.GetView() = r.view

/// Gives a view over any ByteMemory, can be stream-based, mmap-ed, or just byte array.
type ByteMemoryFile(fileName: string, view: ByteMemory) =
    member _.FileName = fileName
    interface BinaryFile with
        override _.GetView() = view.AsReadOnly()

/// A BinaryFile backed by an array of bytes held strongly as managed memory
[<DebuggerDisplay("{FileName}")>]
type ByteFile(fileName: string, bytes: byte[]) =
    let view = ByteMemory.FromArray(bytes).AsReadOnly()
    do stats.byteFileCount <- stats.byteFileCount + 1
    member _.FileName = fileName
    interface BinaryFile with
        override bf.GetView() = view

type PEFile(fileName: string, peReader: PEReader) as this =

    // We store a weak byte memory reference so we do not constantly create a lot of byte memory objects.
    // We could just have a single ByteMemory stored in the PEFile, but we need to dispose of the stream via the finalizer; we cannot have a cicular reference.
    let mutable weakMemory = new WeakReference<ByteMemory>(Unchecked.defaultof<_>)

    member _.FileName = fileName

    override _.Finalize() =
        peReader.Dispose()

    interface BinaryFile with
        override _.GetView() =
            match weakMemory.TryGetTarget() with
            | true, m -> m.AsReadOnly()
            | _ ->
                let block = peReader.GetEntireImage() // it's ok to call this everytime we do GetView as it is cached in the PEReader.
                let m = ByteMemory.FromUnsafePointer(block.Pointer |> NativePtr.toNativeInt, block.Length, this)
                weakMemory <- WeakReference<ByteMemory>(m)
                m.AsReadOnly()

/// Same as ByteFile but holds the bytes weakly. The bytes will be re-read from the backing file when a view is requested.
/// This is the default implementation used by F# Compiler Services when accessing "stable" binaries. It is not used
/// by Visual Studio, where tryGetMetadataSnapshot provides a RawMemoryFile backed by Roslyn data.
[<DebuggerDisplay("{FileName}")>]
type WeakByteFile(fileName: string, chunk: (int * int) option) =

    do stats.weakByteFileCount <- stats.weakByteFileCount + 1

    /// Used to check that the file hasn't changed
    let fileStamp = FileSystem.GetLastWriteTimeShim fileName

    /// The weak handle to the bytes for the file
    let weakBytes = new WeakReference<byte[]> (null)

    member _.FileName = fileName

    /// Get the bytes for the file
    interface BinaryFile with

        override this.GetView() =
            let strongBytes =
                let mutable tg = null
                if not (weakBytes.TryGetTarget(&tg)) then
                    if FileSystem.GetLastWriteTimeShim fileName <> fileStamp then
                        error (Error (FSComp.SR.ilreadFileChanged fileName, range0))

                    let bytes =
                        use stream = FileSystem.OpenFileForReadShim(fileName)
                        match chunk with
                        | None -> stream.ReadAllBytes()
                        | Some(start, length) -> stream.ReadBytes(start, length)

                    tg <- bytes

                    weakBytes.SetTarget bytes

                tg

            ByteMemory.FromArray(strongBytes).AsReadOnly()


let seekReadByte (mdv: BinaryView) addr = mdv.[addr]
let seekReadBytes (mdv: BinaryView) addr len = mdv.ReadBytes(addr, len)
let seekReadInt32 (mdv: BinaryView) addr = mdv.ReadInt32 addr
let seekReadUInt16 (mdv: BinaryView) addr = mdv.ReadUInt16 addr

let seekReadByteAsInt32 mdv addr = int32 (seekReadByte mdv addr)

let seekReadInt64 mdv addr =
    let b0 = seekReadByte mdv addr
    let b1 = seekReadByte mdv (addr+1)
    let b2 = seekReadByte mdv (addr+2)
    let b3 = seekReadByte mdv (addr+3)
    let b4 = seekReadByte mdv (addr+4)
    let b5 = seekReadByte mdv (addr+5)
    let b6 = seekReadByte mdv (addr+6)
    let b7 = seekReadByte mdv (addr+7)
    int64 b0 ||| (int64 b1 <<< 8) ||| (int64 b2 <<< 16) ||| (int64 b3 <<< 24) |||
    (int64 b4 <<< 32) ||| (int64 b5 <<< 40) ||| (int64 b6 <<< 48) ||| (int64 b7 <<< 56)

let seekReadUInt16AsInt32 mdv addr = int32 (seekReadUInt16 mdv addr)

let seekReadCompressedUInt32 mdv addr =
    let b0 = seekReadByte mdv addr
    if b0 <= 0x7Fuy then struct (int b0, addr+1)
    elif b0 <= 0xBFuy then
        let b0 = b0 &&& 0x7Fuy
        let b1 = seekReadByteAsInt32 mdv (addr+1)
        struct ((int b0 <<< 8) ||| int b1, addr+2)
    else
        let b0 = b0 &&& 0x3Fuy
        let b1 = seekReadByteAsInt32 mdv (addr+1)
        let b2 = seekReadByteAsInt32 mdv (addr+2)
        let b3 = seekReadByteAsInt32 mdv (addr+3)
        struct ((int b0 <<< 24) ||| (int b1 <<< 16) ||| (int b2 <<< 8) ||| int b3, addr+4)

let seekReadSByte mdv addr = sbyte (seekReadByte mdv addr)
let seekReadSingle mdv addr = singleOfBits (seekReadInt32 mdv addr)
let seekReadDouble mdv addr = doubleOfBits (seekReadInt64 mdv addr)

let rec seekCountUtf8String mdv addr n =
    let c = seekReadByteAsInt32 mdv addr
    if c = 0 then n
    else seekCountUtf8String mdv (addr+1) (n+1)

let seekReadUTF8String (mdv: BinaryView) addr =
    let n = seekCountUtf8String mdv addr 0
    mdv.ReadUtf8String (addr, n)

let seekReadBlob mdv addr =
    let struct (len, addr) = seekReadCompressedUInt32 mdv addr
    seekReadBytes mdv addr len

let seekReadUserString mdv addr =
    let struct (len, addr) = seekReadCompressedUInt32 mdv addr
    let bytes = seekReadBytes mdv addr (len - 1)
    Encoding.Unicode.GetString(bytes, 0, bytes.Length)

let seekReadGuid mdv addr = seekReadBytes mdv addr 0x10

let seekReadUncodedToken mdv addr =
    i32ToUncodedToken (seekReadInt32 mdv addr)


//---------------------------------------------------------------------
// Primitives to help read signatures. These do not use the file cursor
//---------------------------------------------------------------------

let sigptrCheck (bytes: byte[]) sigptr =
    if checking && sigptr >= bytes.Length then failwith "read past end of sig. "

// All this code should be moved to use a mutable index into the signature
//
//type SigPtr(bytes: byte[], sigptr: int) =
//    let mutable curr = sigptr
//    member x.GetByte() = let res = bytes.[curr] in curr <- curr + 1; res

let sigptrGetByte (bytes: byte[]) sigptr =
    sigptrCheck bytes sigptr
    bytes.[sigptr], sigptr + 1

let sigptrGetBool bytes sigptr =
    let b0, sigptr = sigptrGetByte bytes sigptr
    (b0 = 0x01uy), sigptr

let sigptrGetSByte bytes sigptr =
    let i, sigptr = sigptrGetByte bytes sigptr
    sbyte i, sigptr

let sigptrGetUInt16 bytes sigptr =
    let b0, sigptr = sigptrGetByte bytes sigptr
    let b1, sigptr = sigptrGetByte bytes sigptr
    uint16 (int b0 ||| (int b1 <<< 8)), sigptr

let sigptrGetInt16 bytes sigptr =
    let u, sigptr = sigptrGetUInt16 bytes sigptr
    int16 u, sigptr

let sigptrGetInt32 bytes sigptr =
    sigptrCheck bytes sigptr
    let b0 = bytes.[sigptr]
    let b1 = bytes.[sigptr+1]
    let b2 = bytes.[sigptr+2]
    let b3 = bytes.[sigptr+3]
    let res = int b0 ||| (int b1 <<< 8) ||| (int b2 <<< 16) ||| (int b3 <<< 24)
    res, sigptr + 4

let sigptrGetUInt32 bytes sigptr =
    let u, sigptr = sigptrGetInt32 bytes sigptr
    uint32 u, sigptr

let sigptrGetUInt64 bytes sigptr =
    let u0, sigptr = sigptrGetUInt32 bytes sigptr
    let u1, sigptr = sigptrGetUInt32 bytes sigptr
    (uint64 u0 ||| (uint64 u1 <<< 32)), sigptr

let sigptrGetInt64 bytes sigptr =
    let u, sigptr = sigptrGetUInt64 bytes sigptr
    int64 u, sigptr

let sigptrGetSingle bytes sigptr =
    let u, sigptr = sigptrGetInt32 bytes sigptr
    singleOfBits u, sigptr

let sigptrGetDouble bytes sigptr =
    let u, sigptr = sigptrGetInt64 bytes sigptr
    doubleOfBits u, sigptr

let sigptrGetZInt32 bytes sigptr =
    let b0, sigptr = sigptrGetByte bytes sigptr
    if b0 <= 0x7Fuy then struct (int b0, sigptr)
    elif b0 <= 0xBFuy then
        let b0 = b0 &&& 0x7Fuy
        let b1, sigptr = sigptrGetByte bytes sigptr
        struct ((int b0 <<< 8) ||| int b1, sigptr)
    else
        let b0 = b0 &&& 0x3Fuy
        let b1, sigptr = sigptrGetByte bytes sigptr
        let b2, sigptr = sigptrGetByte bytes sigptr
        let b3, sigptr = sigptrGetByte bytes sigptr
        struct ((int b0 <<< 24) ||| (int b1 <<< 16) ||| (int b2 <<< 8) ||| int b3, sigptr)

let rec sigptrFoldAcc f n (bytes: byte[]) (sigptr: int) i acc =
    if i < n then
        let x, sp = f bytes sigptr
        sigptrFoldAcc f n bytes sp (i+1) (x :: acc)
    else
        List.rev acc, sigptr

let sigptrFold f n (bytes: byte[]) (sigptr: int) =
    sigptrFoldAcc f n bytes sigptr 0 []

let sigptrFoldStruct f n (bytes: byte[]) (sigptr: int) =
    let rec sigptrFoldAcc f n (bytes: byte[]) (sigptr: int) i acc =
        if i < n then
            let struct (x, sp) = f bytes sigptr
            sigptrFoldAcc f n bytes sp (i+1) (x :: acc)
        else
            struct (List.rev acc, sigptr)
    sigptrFoldAcc f n bytes sigptr 0 []

let sigptrGetBytes n (bytes: byte[]) sigptr =
    if checking && sigptr + n >= bytes.Length then
        dprintn "read past end of sig. in sigptrGetString"
        Bytes.zeroCreate 0, sigptr
    else
        let res = Bytes.zeroCreate n
        for i = 0 to (n - 1) do
            res.[i] <- bytes.[sigptr + i]
        res, sigptr + n

let sigptrGetString n bytes sigptr =
    let bytearray, sigptr = sigptrGetBytes n bytes sigptr
    (System.Text.Encoding.UTF8.GetString(bytearray, 0, bytearray.Length)), sigptr


// --------------------------------------------------------------------
// Now the tables of instructions
// --------------------------------------------------------------------

[<NoEquality; NoComparison>]
type ILInstrPrefixesRegister =
   { mutable al: ILAlignment
     mutable tl: ILTailcall
     mutable vol: ILVolatility
     mutable ro: ILReadonly
     mutable constrained: ILType option}

let noPrefixes mk prefixes =
    if prefixes.al <> Aligned then failwith "an unaligned prefix is not allowed here"
    if prefixes.vol <> Nonvolatile then failwith "a volatile prefix is not allowed here"
    if prefixes.tl <> Normalcall then failwith "a tailcall prefix is not allowed here"
    if prefixes.ro <> NormalAddress then failwith "a readonly prefix is not allowed here"
    if prefixes.constrained <> None then failwith "a constrained prefix is not allowed here"
    mk

let volatileOrUnalignedPrefix mk prefixes =
    if prefixes.tl <> Normalcall then failwith "a tailcall prefix is not allowed here"
    if prefixes.constrained <> None then failwith "a constrained prefix is not allowed here"
    if prefixes.ro <> NormalAddress then failwith "a readonly prefix is not allowed here"
    mk (prefixes.al, prefixes.vol)

let volatilePrefix mk prefixes =
    if prefixes.al <> Aligned then failwith "an unaligned prefix is not allowed here"
    if prefixes.tl <> Normalcall then failwith "a tailcall prefix is not allowed here"
    if prefixes.constrained <> None then failwith "a constrained prefix is not allowed here"
    if prefixes.ro <> NormalAddress then failwith "a readonly prefix is not allowed here"
    mk prefixes.vol

let tailPrefix mk prefixes =
    if prefixes.al <> Aligned then failwith "an unaligned prefix is not allowed here"
    if prefixes.vol <> Nonvolatile then failwith "a volatile prefix is not allowed here"
    if prefixes.constrained <> None then failwith "a constrained prefix is not allowed here"
    if prefixes.ro <> NormalAddress then failwith "a readonly prefix is not allowed here"
    mk prefixes.tl

let constraintOrTailPrefix mk prefixes =
    if prefixes.al <> Aligned then failwith "an unaligned prefix is not allowed here"
    if prefixes.vol <> Nonvolatile then failwith "a volatile prefix is not allowed here"
    if prefixes.ro <> NormalAddress then failwith "a readonly prefix is not allowed here"
    mk (prefixes.constrained, prefixes.tl )

let readonlyPrefix mk prefixes =
    if prefixes.al <> Aligned then failwith "an unaligned prefix is not allowed here"
    if prefixes.vol <> Nonvolatile then failwith "a volatile prefix is not allowed here"
    if prefixes.tl <> Normalcall then failwith "a tailcall prefix is not allowed here"
    if prefixes.constrained <> None then failwith "a constrained prefix is not allowed here"
    mk prefixes.ro


[<NoEquality; NoComparison>]
type ILInstrDecoder =
    | I_u16_u8_instr of (ILInstrPrefixesRegister -> uint16 -> ILInstr)
    | I_u16_u16_instr of (ILInstrPrefixesRegister -> uint16 -> ILInstr)
    | I_none_instr of (ILInstrPrefixesRegister -> ILInstr)
    | I_i64_instr of (ILInstrPrefixesRegister -> int64 -> ILInstr)
    | I_i32_i32_instr of (ILInstrPrefixesRegister -> int32 -> ILInstr)
    | I_i32_i8_instr of (ILInstrPrefixesRegister -> int32 -> ILInstr)
    | I_r4_instr of (ILInstrPrefixesRegister -> single -> ILInstr)
    | I_r8_instr of (ILInstrPrefixesRegister -> double -> ILInstr)
    | I_field_instr of (ILInstrPrefixesRegister -> ILFieldSpec -> ILInstr)
    | I_method_instr of (ILInstrPrefixesRegister -> ILMethodSpec * ILVarArgs -> ILInstr)
    | I_unconditional_i32_instr of (ILInstrPrefixesRegister -> ILCodeLabel -> ILInstr)
    | I_unconditional_i8_instr of (ILInstrPrefixesRegister -> ILCodeLabel -> ILInstr)
    | I_conditional_i32_instr of (ILInstrPrefixesRegister -> ILCodeLabel -> ILInstr)
    | I_conditional_i8_instr of (ILInstrPrefixesRegister -> ILCodeLabel -> ILInstr)
    | I_string_instr of (ILInstrPrefixesRegister -> string -> ILInstr)
    | I_switch_instr of (ILInstrPrefixesRegister -> ILCodeLabel list -> ILInstr)
    | I_tok_instr of (ILInstrPrefixesRegister -> ILToken -> ILInstr)
    | I_sig_instr of (ILInstrPrefixesRegister -> ILCallingSignature * ILVarArgs -> ILInstr)
    | I_type_instr of (ILInstrPrefixesRegister -> ILType -> ILInstr)
    | I_invalid_instr

let mkStind dt = volatileOrUnalignedPrefix (fun (x, y) -> I_stind(x, y, dt))
let mkLdind dt = volatileOrUnalignedPrefix (fun (x, y) -> I_ldind(x, y, dt))

let instrs () =
 [ i_ldarg_s, I_u16_u8_instr (noPrefixes mkLdarg)
   i_starg_s, I_u16_u8_instr (noPrefixes I_starg)
   i_ldarga_s, I_u16_u8_instr (noPrefixes I_ldarga)
   i_stloc_s, I_u16_u8_instr (noPrefixes mkStloc)
   i_ldloc_s, I_u16_u8_instr (noPrefixes mkLdloc)
   i_ldloca_s, I_u16_u8_instr (noPrefixes I_ldloca)
   i_ldarg, I_u16_u16_instr (noPrefixes mkLdarg)
   i_starg, I_u16_u16_instr (noPrefixes I_starg)
   i_ldarga, I_u16_u16_instr (noPrefixes I_ldarga)
   i_stloc, I_u16_u16_instr (noPrefixes mkStloc)
   i_ldloc, I_u16_u16_instr (noPrefixes mkLdloc)
   i_ldloca, I_u16_u16_instr (noPrefixes I_ldloca)
   i_stind_i, I_none_instr (mkStind DT_I)
   i_stind_i1, I_none_instr (mkStind DT_I1)
   i_stind_i2, I_none_instr (mkStind DT_I2)
   i_stind_i4, I_none_instr (mkStind DT_I4)
   i_stind_i8, I_none_instr (mkStind DT_I8)
   i_stind_r4, I_none_instr (mkStind DT_R4)
   i_stind_r8, I_none_instr (mkStind DT_R8)
   i_stind_ref, I_none_instr (mkStind DT_REF)
   i_ldind_i, I_none_instr (mkLdind DT_I)
   i_ldind_i1, I_none_instr (mkLdind DT_I1)
   i_ldind_i2, I_none_instr (mkLdind DT_I2)
   i_ldind_i4, I_none_instr (mkLdind DT_I4)
   i_ldind_i8, I_none_instr (mkLdind DT_I8)
   i_ldind_u1, I_none_instr (mkLdind DT_U1)
   i_ldind_u2, I_none_instr (mkLdind DT_U2)
   i_ldind_u4, I_none_instr (mkLdind DT_U4)
   i_ldind_r4, I_none_instr (mkLdind DT_R4)
   i_ldind_r8, I_none_instr (mkLdind DT_R8)
   i_ldind_ref, I_none_instr (mkLdind DT_REF)
   i_cpblk, I_none_instr (volatileOrUnalignedPrefix I_cpblk)
   i_initblk, I_none_instr (volatileOrUnalignedPrefix I_initblk)
   i_ldc_i8, I_i64_instr (noPrefixes (fun x ->(AI_ldc (DT_I8, ILConst.I8 x))))
   i_ldc_i4, I_i32_i32_instr (noPrefixes mkLdcInt32)
   i_ldc_i4_s, I_i32_i8_instr (noPrefixes mkLdcInt32)
   i_ldc_r4, I_r4_instr (noPrefixes (fun x -> (AI_ldc (DT_R4, ILConst.R4 x))))
   i_ldc_r8, I_r8_instr (noPrefixes (fun x -> (AI_ldc (DT_R8, ILConst.R8 x))))
   i_ldfld, I_field_instr (volatileOrUnalignedPrefix(fun (x, y) fspec -> I_ldfld (x, y, fspec)))
   i_stfld, I_field_instr (volatileOrUnalignedPrefix(fun (x, y) fspec -> I_stfld (x, y, fspec)))
   i_ldsfld, I_field_instr (volatilePrefix (fun x fspec -> I_ldsfld (x, fspec)))
   i_stsfld, I_field_instr (volatilePrefix (fun x fspec -> I_stsfld (x, fspec)))
   i_ldflda, I_field_instr (noPrefixes I_ldflda)
   i_ldsflda, I_field_instr (noPrefixes I_ldsflda)
   i_call, I_method_instr (tailPrefix (fun tl (mspec, y) -> I_call (tl, mspec, y)))
   i_ldftn, I_method_instr (noPrefixes (fun (mspec, _y) -> I_ldftn mspec))
   i_ldvirtftn, I_method_instr (noPrefixes (fun (mspec, _y) -> I_ldvirtftn mspec))
   i_newobj, I_method_instr (noPrefixes I_newobj)
   i_callvirt, I_method_instr (constraintOrTailPrefix (fun (c, tl) (mspec, y) -> match c with Some ty -> I_callconstraint(tl, ty, mspec, y) | None -> I_callvirt (tl, mspec, y)))
   i_leave_s, I_unconditional_i8_instr (noPrefixes (fun x -> I_leave x))
   i_br_s, I_unconditional_i8_instr (noPrefixes I_br)
   i_leave, I_unconditional_i32_instr (noPrefixes (fun x -> I_leave x))
   i_br, I_unconditional_i32_instr (noPrefixes I_br)
   i_brtrue_s, I_conditional_i8_instr (noPrefixes (fun x -> I_brcmp (BI_brtrue, x)))
   i_brfalse_s, I_conditional_i8_instr (noPrefixes (fun x -> I_brcmp (BI_brfalse, x)))
   i_beq_s, I_conditional_i8_instr (noPrefixes (fun x -> I_brcmp (BI_beq, x)))
   i_blt_s, I_conditional_i8_instr (noPrefixes (fun x -> I_brcmp (BI_blt, x)))
   i_blt_un_s, I_conditional_i8_instr (noPrefixes (fun x -> I_brcmp (BI_blt_un, x)))
   i_ble_s, I_conditional_i8_instr (noPrefixes (fun x -> I_brcmp (BI_ble, x)))
   i_ble_un_s, I_conditional_i8_instr (noPrefixes (fun x -> I_brcmp (BI_ble_un, x)))
   i_bgt_s, I_conditional_i8_instr (noPrefixes (fun x -> I_brcmp (BI_bgt, x)))
   i_bgt_un_s, I_conditional_i8_instr (noPrefixes (fun x -> I_brcmp (BI_bgt_un, x)))
   i_bge_s, I_conditional_i8_instr (noPrefixes (fun x -> I_brcmp (BI_bge, x)))
   i_bge_un_s, I_conditional_i8_instr (noPrefixes (fun x -> I_brcmp (BI_bge_un, x)))
   i_bne_un_s, I_conditional_i8_instr (noPrefixes (fun x -> I_brcmp (BI_bne_un, x)))
   i_brtrue, I_conditional_i32_instr (noPrefixes (fun x -> I_brcmp (BI_brtrue, x)))
   i_brfalse, I_conditional_i32_instr (noPrefixes (fun x -> I_brcmp (BI_brfalse, x)))
   i_beq, I_conditional_i32_instr (noPrefixes (fun x -> I_brcmp (BI_beq, x)))
   i_blt, I_conditional_i32_instr (noPrefixes (fun x -> I_brcmp (BI_blt, x)))
   i_blt_un, I_conditional_i32_instr (noPrefixes (fun x -> I_brcmp (BI_blt_un, x)))
   i_ble, I_conditional_i32_instr (noPrefixes (fun x -> I_brcmp (BI_ble, x)))
   i_ble_un, I_conditional_i32_instr (noPrefixes (fun x -> I_brcmp (BI_ble_un, x)))
   i_bgt, I_conditional_i32_instr (noPrefixes (fun x -> I_brcmp (BI_bgt, x)))
   i_bgt_un, I_conditional_i32_instr (noPrefixes (fun x -> I_brcmp (BI_bgt_un, x)))
   i_bge, I_conditional_i32_instr (noPrefixes (fun x -> I_brcmp (BI_bge, x)))
   i_bge_un, I_conditional_i32_instr (noPrefixes (fun x -> I_brcmp (BI_bge_un, x)))
   i_bne_un, I_conditional_i32_instr (noPrefixes (fun x -> I_brcmp (BI_bne_un, x)))
   i_ldstr, I_string_instr (noPrefixes I_ldstr)
   i_switch, I_switch_instr (noPrefixes I_switch)
   i_ldtoken, I_tok_instr (noPrefixes I_ldtoken)
   i_calli, I_sig_instr (tailPrefix (fun tl (x, y) -> I_calli (tl, x, y)))
   i_mkrefany, I_type_instr (noPrefixes I_mkrefany)
   i_refanyval, I_type_instr (noPrefixes I_refanyval)
   i_ldelema, I_type_instr (readonlyPrefix (fun ro x -> I_ldelema (ro, false, ILArrayShape.SingleDimensional, x)))
   i_ldelem_any, I_type_instr (noPrefixes (fun x -> I_ldelem_any (ILArrayShape.SingleDimensional, x)))
   i_stelem_any, I_type_instr (noPrefixes (fun x -> I_stelem_any (ILArrayShape.SingleDimensional, x)))
   i_newarr, I_type_instr (noPrefixes (fun x -> I_newarr (ILArrayShape.SingleDimensional, x)))
   i_castclass, I_type_instr (noPrefixes I_castclass)
   i_isinst, I_type_instr (noPrefixes I_isinst)
   i_unbox_any, I_type_instr (noPrefixes I_unbox_any)
   i_cpobj, I_type_instr (noPrefixes I_cpobj)
   i_initobj, I_type_instr (noPrefixes I_initobj)
   i_ldobj, I_type_instr (volatileOrUnalignedPrefix (fun (x, y) z -> I_ldobj (x, y, z)))
   i_stobj, I_type_instr (volatileOrUnalignedPrefix (fun (x, y) z -> I_stobj (x, y, z)))
   i_sizeof, I_type_instr (noPrefixes I_sizeof)
   i_box, I_type_instr (noPrefixes I_box)
   i_unbox, I_type_instr (noPrefixes I_unbox) ]

// The tables are delayed to avoid building them unnecessarily at startup
// Many applications of AbsIL (e.g. a compiler) don't need to read instructions.
let oneByteInstrs = ref None
let twoByteInstrs = ref None
let fillInstrs () =
    let oneByteInstrTable = Array.create 256 I_invalid_instr
    let twoByteInstrTable = Array.create 256 I_invalid_instr
    let addInstr (i, f) =
        if i > 0xff then
            assert (i >>>& 8 = 0xfe)
            let i = (i &&& 0xff)
            match twoByteInstrTable.[i] with
            | I_invalid_instr -> ()
            | _ -> dprintn ("warning: duplicate decode entries for "+string i)
            twoByteInstrTable.[i] <- f
        else
            match oneByteInstrTable.[i] with
            | I_invalid_instr -> ()
            | _ -> dprintn ("warning: duplicate decode entries for "+string i)
            oneByteInstrTable.[i] <- f
    List.iter addInstr (instrs())
    List.iter (fun (x, mk) -> addInstr (x, I_none_instr (noPrefixes mk))) (noArgInstrs.Force())
    oneByteInstrs := Some oneByteInstrTable
    twoByteInstrs := Some twoByteInstrTable

let rec getOneByteInstr i =
    match !oneByteInstrs with
    | None -> fillInstrs(); getOneByteInstr i
    | Some t -> t.[i]

let rec getTwoByteInstr i =
    match !twoByteInstrs with
    | None -> fillInstrs(); getTwoByteInstr i
    | Some t -> t.[i]

//---------------------------------------------------------------------
//
//---------------------------------------------------------------------

type ImageChunk = { size: int32; addr: int32 }

let chunk sz next = ({addr=next; size=sz}, next + sz)
let nochunk next = ({addr= 0x0;size= 0x0; }, next)

type RowElementKind =
    | UShort
    | ULong
    | Byte
    | Data
    | GGuid
    | Blob
    | SString
    | SimpleIndex of TableName
    | TypeDefOrRefOrSpec
    | TypeOrMethodDef
    | HasConstant
    | HasCustomAttribute
    | HasFieldMarshal
    | HasDeclSecurity
    | MemberRefParent
    | HasSemantics
    | MethodDefOrRef
    | MemberForwarded
    | Implementation
    | CustomAttributeType
    | ResolutionScope

type RowKind = RowKind of RowElementKind list

let kindAssemblyRef = RowKind [ UShort; UShort; UShort; UShort; ULong; Blob; SString; SString; Blob; ]
let kindModuleRef = RowKind [ SString ]
let kindFileRef = RowKind [ ULong; SString; Blob ]
let kindTypeRef = RowKind [ ResolutionScope; SString; SString ]
let kindTypeSpec = RowKind [ Blob ]
let kindTypeDef = RowKind [ ULong; SString; SString; TypeDefOrRefOrSpec; SimpleIndex TableNames.Field; SimpleIndex TableNames.Method ]
let kindPropertyMap = RowKind [ SimpleIndex TableNames.TypeDef; SimpleIndex TableNames.Property ]
let kindEventMap = RowKind [ SimpleIndex TableNames.TypeDef; SimpleIndex TableNames.Event ]
let kindInterfaceImpl = RowKind [ SimpleIndex TableNames.TypeDef; TypeDefOrRefOrSpec ]
let kindNested = RowKind [ SimpleIndex TableNames.TypeDef; SimpleIndex TableNames.TypeDef ]
let kindCustomAttribute = RowKind [ HasCustomAttribute; CustomAttributeType; Blob ]
let kindDeclSecurity = RowKind [ UShort; HasDeclSecurity; Blob ]
let kindMemberRef = RowKind [ MemberRefParent; SString; Blob ]
let kindStandAloneSig = RowKind [ Blob ]
let kindFieldDef = RowKind [ UShort; SString; Blob ]
let kindFieldRVA = RowKind [ Data; SimpleIndex TableNames.Field ]
let kindFieldMarshal = RowKind [ HasFieldMarshal; Blob ]
let kindConstant = RowKind [ UShort;HasConstant; Blob ]
let kindFieldLayout = RowKind [ ULong; SimpleIndex TableNames.Field ]
let kindParam = RowKind [ UShort; UShort; SString ]
let kindMethodDef = RowKind [ ULong; UShort; UShort; SString; Blob; SimpleIndex TableNames.Param ]
let kindMethodImpl = RowKind [ SimpleIndex TableNames.TypeDef; MethodDefOrRef; MethodDefOrRef ]
let kindImplMap = RowKind [ UShort; MemberForwarded; SString; SimpleIndex TableNames.ModuleRef ]
let kindMethodSemantics = RowKind [ UShort; SimpleIndex TableNames.Method; HasSemantics ]
let kindProperty = RowKind [ UShort; SString; Blob ]
let kindEvent = RowKind [ UShort; SString; TypeDefOrRefOrSpec ]
let kindManifestResource = RowKind [ ULong; ULong; SString; Implementation ]
let kindClassLayout = RowKind [ UShort; ULong; SimpleIndex TableNames.TypeDef ]
let kindExportedType = RowKind [ ULong; ULong; SString; SString; Implementation ]
let kindAssembly = RowKind [ ULong; UShort; UShort; UShort; UShort; ULong; Blob; SString; SString ]
let kindGenericParam_v1_1 = RowKind [ UShort; UShort; TypeOrMethodDef; SString; TypeDefOrRefOrSpec ]
let kindGenericParam_v2_0 = RowKind [ UShort; UShort; TypeOrMethodDef; SString ]
let kindMethodSpec = RowKind [ MethodDefOrRef; Blob ]
let kindGenericParamConstraint = RowKind [ SimpleIndex TableNames.GenericParam; TypeDefOrRefOrSpec ]
let kindModule = RowKind [ UShort; SString; GGuid; GGuid; GGuid ]
let kindIllegal = RowKind [ ]

//---------------------------------------------------------------------
// Used for binary searches of sorted tables. Each function that reads
// a table row returns a tuple that contains the elements of the row.
// One of these elements may be a key for a sorted table. These
// keys can be compared using the functions below depending on the
// kind of element in that column.
//---------------------------------------------------------------------

let hcCompare (TaggedIndex((t1: HasConstantTag), (idx1: int))) (TaggedIndex((t2: HasConstantTag), idx2)) =
    if idx1 < idx2 then -1 elif idx1 > idx2 then 1 else compare t1.Tag t2.Tag

let hsCompare (TaggedIndex((t1: HasSemanticsTag), (idx1: int))) (TaggedIndex((t2: HasSemanticsTag), idx2)) =
    if idx1 < idx2 then -1 elif idx1 > idx2 then 1 else compare t1.Tag t2.Tag

let hcaCompare (TaggedIndex((t1: HasCustomAttributeTag), (idx1: int))) (TaggedIndex((t2: HasCustomAttributeTag), idx2)) =
    if idx1 < idx2 then -1 elif idx1 > idx2 then 1 else compare t1.Tag t2.Tag

let mfCompare (TaggedIndex((t1: MemberForwardedTag), (idx1: int))) (TaggedIndex((t2: MemberForwardedTag), idx2)) =
    if idx1 < idx2 then -1 elif idx1 > idx2 then 1 else compare t1.Tag t2.Tag

let hdsCompare (TaggedIndex((t1: HasDeclSecurityTag), (idx1: int))) (TaggedIndex((t2: HasDeclSecurityTag), idx2)) =
    if idx1 < idx2 then -1 elif idx1 > idx2 then 1 else compare t1.Tag t2.Tag

let hfmCompare (TaggedIndex((t1: HasFieldMarshalTag), idx1)) (TaggedIndex((t2: HasFieldMarshalTag), idx2)) =
    if idx1 < idx2 then -1 elif idx1 > idx2 then 1 else compare t1.Tag t2.Tag

let tomdCompare (TaggedIndex((t1: TypeOrMethodDefTag), idx1)) (TaggedIndex((t2: TypeOrMethodDefTag), idx2)) =
    if idx1 < idx2 then -1 elif idx1 > idx2 then 1 else compare t1.Tag t2.Tag

let simpleIndexCompare (idx1: int) (idx2: int) =
    compare idx1 idx2

//---------------------------------------------------------------------
// The various keys for the various caches.
//---------------------------------------------------------------------

type TypeDefAsTypIdx = TypeDefAsTypIdx of ILBoxity * ILGenericArgs * int
type TypeRefAsTypIdx = TypeRefAsTypIdx of ILBoxity * ILGenericArgs * int
type BlobAsMethodSigIdx = BlobAsMethodSigIdx of numtypars: int * blobIdx: int32
type BlobAsFieldSigIdx = BlobAsFieldSigIdx of numtypars: int * blobIdx: int32
type BlobAsPropSigIdx = BlobAsPropSigIdx of numtypars: int * blobIdx: int32
type BlobAsLocalSigIdx = BlobAsLocalSigIdx of numtypars: int * blobIdx: int32
type MemberRefAsMspecIdx = MemberRefAsMspecIdx of numtypars: int * idx: int
type MethodSpecAsMspecIdx = MethodSpecAsMspecIdx of numtypars: int * idx: int
type MemberRefAsFspecIdx = MemberRefAsFspecIdx of numtypars: int * idx: int
type CustomAttrIdx = CustomAttrIdx of CustomAttributeTypeTag * idx: int * valIdx: int32
type GenericParamsIdx = GenericParamsIdx of numtypars: int * TypeOrMethodDefTag * idx: int

//---------------------------------------------------------------------
// Polymorphic caches for row and heap readers
//---------------------------------------------------------------------

let mkCacheInt32 lowMem _inbase _nm _sz =
    if lowMem then (fun f x -> f x) else
    let cache = ref null
    let count = ref 0
#if STATISTICS
    addReport (fun oc -> if !count <> 0 then oc.WriteLine ((_inbase + string !count + " "+ _nm + " cache hits"): string))
#endif
    fun f (idx: int32) ->
        let cache =
            match !cache with
            | null -> cache := new ConcurrentDictionary<int32, _>(Environment.ProcessorCount, 11)
            | _ -> ()
            !cache
        match cache.TryGetValue idx with
        | true, res ->
            incr count
            res
        | _ ->
            let res = f idx
            cache.[idx] <- res
            res

let mkCacheGeneric lowMem _inbase _nm _sz =
    if lowMem then (fun f x -> f x) else
    let cache = ref null
    let count = ref 0
#if STATISTICS
    addReport (fun oc -> if !count <> 0 then oc.WriteLine ((_inbase + string !count + " " + _nm + " cache hits"): string))
#endif
    fun f (idx :'T) ->
        let cache =
            match !cache with
            | null -> cache := new ConcurrentDictionary<_, _>(Environment.ProcessorCount, 11 (* sz: int *) )
            | _ -> ()
            !cache
        match cache.TryGetValue idx with
        | true, v ->
            incr count
            v
        | _ ->
            let res = f idx
            cache.[idx] <- res
            res

//-----------------------------------------------------------------------
// Polymorphic general helpers for searching for particular rows.
// ----------------------------------------------------------------------

let seekFindRow numRows rowChooser =
    let mutable i = 1
    while (i <= numRows && not (rowChooser i)) do
        i <- i + 1
    if i > numRows then dprintn "warning: seekFindRow: row not found"
    i

// search for rows satisfying predicate
let seekReadIndexedRows (numRows, rowReader, keyFunc, keyComparer, binaryChop, rowConverter) =
    if binaryChop then
        let mutable low = 0
        let mutable high = numRows + 1
        begin
          let mutable fin = false
          while not fin do
              if high - low <= 1 then
                  fin <- true
              else
                  let mid = (low + high) / 2
                  let midrow = rowReader mid
                  let c = keyComparer (keyFunc midrow)
                  if c > 0 then
                      low <- mid
                  elif c < 0 then
                      high <- mid
                  else
                      fin <- true
        end
        let mutable res = []
        if high - low > 1 then
            // now read off rows, forward and backwards
            let mid = (low + high) / 2
            // read forward
            let mutable fin = false
            let mutable curr = mid
            while not fin do
                if curr > numRows then
                    fin <- true
                else
                    let currrow = rowReader curr
                    if keyComparer (keyFunc currrow) = 0 then
                        res <- rowConverter currrow :: res
                    else
                        fin <- true
                    curr <- curr + 1

            res <- List.rev res
            // read backwards
            let mutable fin = false
            let mutable curr = mid - 1
            while not fin do
                if curr = 0 then
                    fin <- true
                else
                    let currrow = rowReader curr
                    if keyComparer (keyFunc currrow) = 0 then
                        res <- rowConverter currrow :: res
                    else
                        fin <- true
                    curr <- curr - 1
        // sanity check
#if CHECKING
        if checking then
            let res2 =
                [ for i = 1 to numRows do
                    let rowinfo = rowReader i
                    if keyComparer (keyFunc rowinfo) = 0 then
                      yield rowConverter rowinfo ]
            if (res2 <> res) then
                failwith ("results of binary search did not match results of linear search: linear search produced "+string res2.Length+", binary search produced "+string res.Length)
#endif

        res
    else
        let res = ref []
        for i = 1 to numRows do
            let rowinfo = rowReader i
            if keyComparer (keyFunc rowinfo) = 0 then
              res := rowConverter rowinfo :: !res
        List.rev !res


let seekReadOptionalIndexedRow info =
    match seekReadIndexedRows info with
    | [k] -> Some k
    | [] -> None
    | h :: _ ->
        dprintn ("multiple rows found when indexing table")
        Some h

let seekReadIndexedRow info =
    match seekReadOptionalIndexedRow info with
    | Some row -> row
    | None -> failwith ("no row found for key when indexing table")

//---------------------------------------------------------------------
// IL Reading proper
//---------------------------------------------------------------------

type MethodData = MethodData of enclTy: ILType * ILCallingConv * name: string * argtys: ILTypes * retty: ILType * minst: ILTypes
type VarArgMethodData = VarArgMethodData of enclTy: ILType * ILCallingConv * name: string * argtys: ILTypes * ILVarArgs * retty: ILType * minst: ILTypes

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type PEReader =
  { fileName: string
#if FX_NO_PDB_READER
    pdb: obj option
#else
    pdb: (PdbReader * (string -> ILSourceDocument)) option
#endif
    entryPointToken: TableName * int
    pefile: BinaryFile
    textSegmentPhysicalLoc: int32
    textSegmentPhysicalSize: int32
    dataSegmentPhysicalLoc: int32
    dataSegmentPhysicalSize: int32
    anyV2P: (string * int32) -> int32
    metadataAddr: int32
    sectionHeaders: (int32 * int32 * int32) list
    nativeResourcesAddr: int32
    nativeResourcesSize: int32
    resourcesAddr: int32
    strongnameAddr: int32
    vtableFixupsAddr: int32
    noFileOnDisk: bool
}

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type ILMetadataReader =
  { sorted: int64
    mdfile: BinaryFile
    pectxtCaptured: PEReader option // only set when reading full PE including code etc. for static linking
    entryPointToken: TableName * int
    dataEndPoints: Lazy<int32 list>
    fileName: string
    getNumRows: TableName -> int
    userStringsStreamPhysicalLoc: int32
    stringsStreamPhysicalLoc: int32
    blobsStreamPhysicalLoc: int32
    blobsStreamSize: int32
    readUserStringHeap: (int32 -> string)
    memoizeString: string -> string
    readStringHeap: (int32 -> string)
    readBlobHeap: (int32 -> byte[])
    guidsStreamPhysicalLoc: int32
    rowAddr: (TableName -> int -> int32)
    tableBigness: bool []
    rsBigness: bool
    tdorBigness: bool
    tomdBigness: bool
    hcBigness: bool
    hcaBigness: bool
    hfmBigness: bool
    hdsBigness: bool
    mrpBigness: bool
    hsBigness: bool
    mdorBigness: bool
    mfBigness: bool
    iBigness: bool
    catBigness: bool
    stringsBigness: bool
    guidsBigness: bool
    blobsBigness: bool
    seekReadNestedRow: int -> int * int
    seekReadConstantRow: int -> uint16 * TaggedIndex<HasConstantTag> * int32
    seekReadMethodSemanticsRow: int -> int32 * int * TaggedIndex<HasSemanticsTag>
    seekReadTypeDefRow: int -> int32 * int32 * int32 * TaggedIndex<TypeDefOrRefTag> * int * int
    seekReadAssemblyRef: int -> ILAssemblyRef
    seekReadMethodSpecAsMethodData: MethodSpecAsMspecIdx -> VarArgMethodData
    seekReadMemberRefAsMethodData: MemberRefAsMspecIdx -> VarArgMethodData
    seekReadMemberRefAsFieldSpec: MemberRefAsFspecIdx -> ILFieldSpec
    seekReadCustomAttr: CustomAttrIdx -> ILAttribute
    seekReadTypeRef: int ->ILTypeRef
    seekReadTypeRefAsType: TypeRefAsTypIdx -> ILType
    readBlobHeapAsPropertySig: BlobAsPropSigIdx -> ILThisConvention * ILType * ILTypes
    readBlobHeapAsFieldSig: BlobAsFieldSigIdx -> ILType
    readBlobHeapAsMethodSig: BlobAsMethodSigIdx -> bool * int32 * ILCallingConv * ILType * ILTypes * ILVarArgs
    readBlobHeapAsLocalsSig: BlobAsLocalSigIdx -> ILLocal list
    seekReadTypeDefAsType: TypeDefAsTypIdx -> ILType
    seekReadMethodDefAsMethodData: int -> MethodData
    seekReadGenericParams: GenericParamsIdx -> ILGenericParameterDef list
    seekReadFieldDefAsFieldSpec: int -> ILFieldSpec
    customAttrsReader_Module: ILAttributesStored
    customAttrsReader_Assembly: ILAttributesStored
    customAttrsReader_TypeDef: ILAttributesStored
    customAttrsReader_GenericParam: ILAttributesStored
    customAttrsReader_FieldDef: ILAttributesStored
    customAttrsReader_MethodDef: ILAttributesStored
    customAttrsReader_ParamDef: ILAttributesStored
    customAttrsReader_Event: ILAttributesStored
    customAttrsReader_Property: ILAttributesStored
    customAttrsReader_ManifestResource: ILAttributesStored
    customAttrsReader_ExportedType: ILAttributesStored
    securityDeclsReader_TypeDef: ILSecurityDeclsStored
    securityDeclsReader_MethodDef: ILSecurityDeclsStored
    securityDeclsReader_Assembly: ILSecurityDeclsStored
    typeDefReader: ILTypeDefStored }

type ISeekReadIndexedRowReader<'RowT, 'KeyT, 'T when 'RowT : struct> =
    abstract GetRow: int * byref<'RowT> -> unit
    abstract GetKey: byref<'RowT> -> 'KeyT
    abstract CompareKey: 'KeyT -> int
    abstract ConvertRow: byref<'RowT> -> 'T

let seekReadIndexedRowsByInterface numRows binaryChop (reader: ISeekReadIndexedRowReader<'RowT, _, _>) =
    let mutable row = Unchecked.defaultof<'RowT>
    if binaryChop then
        let mutable low = 0
        let mutable high = numRows + 1

        let mutable fin = false
        while not fin do
            if high - low <= 1  then
                fin <- true
            else
                let mid = (low + high) / 2
                reader.GetRow(mid, &row)
                let c = reader.CompareKey(reader.GetKey(&row))
                if c > 0 then
                    low <- mid
                elif c < 0 then
                    high <- mid
                else
                    fin <- true

        let res = ImmutableArray.CreateBuilder()
        if high - low > 1 then
            // now read off rows, forward and backwards
            let mid = (low + high) / 2

            // read backwards
            let mutable fin = false
            let mutable curr = mid - 1
            while not fin do
                if curr = 0 then
                    fin <- true
                else
                    reader.GetRow(curr, &row)
                    if reader.CompareKey(reader.GetKey(&row)) = 0 then
                        res.Add(reader.ConvertRow(&row))
                    else
                        fin <- true
                curr <- curr - 1

            res.Reverse()

            // read forward
            let mutable fin = false
            let mutable curr = mid
            while not fin do
                if curr > numRows then
                    fin <- true
                else
                    reader.GetRow(curr, &row)
                    if reader.CompareKey(reader.GetKey(&row)) = 0 then
                        res.Add(reader.ConvertRow(&row))
                    else
                        fin <- true
                    curr <- curr + 1

        res.ToArray()
    else
        let res = ImmutableArray.CreateBuilder()
        for i = 1 to numRows do
            reader.GetRow(i, &row)
            if reader.CompareKey(reader.GetKey(&row)) = 0 then
              res.Add(reader.ConvertRow(&row))
        res.ToArray()

[<Struct>]
type CustomAttributeRow =
    val mutable parentIndex: TaggedIndex<HasCustomAttributeTag>
    val mutable typeIndex: TaggedIndex<CustomAttributeTypeTag>
    val mutable valueIndex: int

let seekReadUInt16Adv mdv (addr: byref<int>) =
    let res = seekReadUInt16 mdv addr
    addr <- addr + 2
    res

let seekReadInt32Adv mdv (addr: byref<int>) =
    let res = seekReadInt32 mdv addr
    addr <- addr+4
    res

let seekReadUInt16AsInt32Adv mdv (addr: byref<int>) =
    let res = seekReadUInt16AsInt32 mdv addr
    addr <- addr+2
    res

let inline seekReadTaggedIdx f nbits big mdv (addr: byref<int>) =
    let tok = if big then seekReadInt32Adv mdv &addr else seekReadUInt16AsInt32Adv mdv &addr
    tokToTaggedIdx f nbits tok

let seekReadIdx big mdv (addr: byref<int>) =
    if big then seekReadInt32Adv mdv &addr else seekReadUInt16AsInt32Adv mdv &addr

let seekReadUntaggedIdx (tab: TableName) (ctxt: ILMetadataReader) mdv (addr: byref<int>) =
    seekReadIdx ctxt.tableBigness.[tab.Index] mdv &addr

let seekReadResolutionScopeIdx (ctxt: ILMetadataReader) mdv (addr: byref<int>) = seekReadTaggedIdx mkResolutionScopeTag 2 ctxt.rsBigness mdv &addr
let seekReadTypeDefOrRefOrSpecIdx (ctxt: ILMetadataReader) mdv (addr: byref<int>) = seekReadTaggedIdx mkTypeDefOrRefOrSpecTag 2 ctxt.tdorBigness mdv &addr
let seekReadTypeOrMethodDefIdx (ctxt: ILMetadataReader) mdv (addr: byref<int>) = seekReadTaggedIdx mkTypeOrMethodDefTag 1 ctxt.tomdBigness mdv &addr
let seekReadHasConstantIdx (ctxt: ILMetadataReader) mdv (addr: byref<int>) = seekReadTaggedIdx mkHasConstantTag 2 ctxt.hcBigness mdv &addr
let seekReadHasCustomAttributeIdx (ctxt: ILMetadataReader) mdv (addr: byref<int>) = seekReadTaggedIdx mkHasCustomAttributeTag 5 ctxt.hcaBigness mdv &addr
let seekReadHasFieldMarshalIdx (ctxt: ILMetadataReader) mdv (addr: byref<int>) = seekReadTaggedIdx mkHasFieldMarshalTag 1 ctxt.hfmBigness mdv &addr
let seekReadHasDeclSecurityIdx (ctxt: ILMetadataReader) mdv (addr: byref<int>) = seekReadTaggedIdx mkHasDeclSecurityTag 2 ctxt.hdsBigness mdv &addr
let seekReadMemberRefParentIdx (ctxt: ILMetadataReader) mdv (addr: byref<int>) = seekReadTaggedIdx mkMemberRefParentTag 3 ctxt.mrpBigness mdv &addr
let seekReadHasSemanticsIdx (ctxt: ILMetadataReader) mdv (addr: byref<int>) = seekReadTaggedIdx mkHasSemanticsTag 1 ctxt.hsBigness mdv &addr
let seekReadMethodDefOrRefIdx (ctxt: ILMetadataReader) mdv (addr: byref<int>) = seekReadTaggedIdx mkMethodDefOrRefTag 1 ctxt.mdorBigness mdv &addr
let seekReadMemberForwardedIdx (ctxt: ILMetadataReader) mdv (addr: byref<int>) = seekReadTaggedIdx mkMemberForwardedTag 1 ctxt.mfBigness mdv &addr
let seekReadImplementationIdx (ctxt: ILMetadataReader) mdv (addr: byref<int>) = seekReadTaggedIdx mkImplementationTag 2 ctxt.iBigness mdv &addr
let seekReadCustomAttributeTypeIdx (ctxt: ILMetadataReader) mdv (addr: byref<int>) = seekReadTaggedIdx mkILCustomAttributeTypeTag 3 ctxt.catBigness mdv &addr
let seekReadStringIdx (ctxt: ILMetadataReader) mdv (addr: byref<int>) = seekReadIdx ctxt.stringsBigness mdv &addr
let seekReadGuidIdx (ctxt: ILMetadataReader) mdv (addr: byref<int>) = seekReadIdx ctxt.guidsBigness mdv &addr
let seekReadBlobIdx (ctxt: ILMetadataReader) mdv (addr: byref<int>) = seekReadIdx ctxt.blobsBigness mdv &addr

let seekReadModuleRow (ctxt: ILMetadataReader) mdv idx =
    if idx = 0 then failwith "cannot read Module table row 0"
    let mutable addr = ctxt.rowAddr TableNames.Module idx
    let generation = seekReadUInt16Adv mdv &addr
    let nameIdx = seekReadStringIdx ctxt mdv &addr
    let mvidIdx = seekReadGuidIdx ctxt mdv &addr
    let encidIdx = seekReadGuidIdx ctxt mdv &addr
    let encbaseidIdx = seekReadGuidIdx ctxt mdv &addr
    (generation, nameIdx, mvidIdx, encidIdx, encbaseidIdx)

/// Read Table ILTypeRef.
let seekReadTypeRefRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.TypeRef idx
    let scopeIdx = seekReadResolutionScopeIdx ctxt mdv &addr
    let nameIdx = seekReadStringIdx ctxt mdv &addr
    let namespaceIdx = seekReadStringIdx ctxt mdv &addr
    (scopeIdx, nameIdx, namespaceIdx)

/// Read Table ILTypeDef.
let seekReadTypeDefRow (ctxt: ILMetadataReader) idx = ctxt.seekReadTypeDefRow idx
let seekReadTypeDefRowUncached ctxtH idx =
    let (ctxt: ILMetadataReader) = getHole ctxtH
    let mdv = ctxt.mdfile.GetView()
    let mutable addr = ctxt.rowAddr TableNames.TypeDef idx
    let flags = seekReadInt32Adv mdv &addr
    let nameIdx = seekReadStringIdx ctxt mdv &addr
    let namespaceIdx = seekReadStringIdx ctxt mdv &addr
    let extendsIdx = seekReadTypeDefOrRefOrSpecIdx ctxt mdv &addr
    let fieldsIdx = seekReadUntaggedIdx TableNames.Field ctxt mdv &addr
    let methodsIdx = seekReadUntaggedIdx TableNames.Method ctxt mdv &addr
    (flags, nameIdx, namespaceIdx, extendsIdx, fieldsIdx, methodsIdx)

/// Read Table Field.
let seekReadFieldRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.Field idx
    let flags = seekReadUInt16AsInt32Adv mdv &addr
    let nameIdx = seekReadStringIdx ctxt mdv &addr
    let typeIdx = seekReadBlobIdx ctxt mdv &addr
    (flags, nameIdx, typeIdx)

/// Read Table Method.
let seekReadMethodRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.Method idx
    let codeRVA = seekReadInt32Adv mdv &addr
    let implflags = seekReadUInt16AsInt32Adv mdv &addr
    let flags = seekReadUInt16AsInt32Adv mdv &addr
    let nameIdx = seekReadStringIdx ctxt mdv &addr
    let typeIdx = seekReadBlobIdx ctxt mdv &addr
    let paramIdx = seekReadUntaggedIdx TableNames.Param ctxt mdv &addr
    (codeRVA, implflags, flags, nameIdx, typeIdx, paramIdx)

/// Read Table Param.
let seekReadParamRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.Param idx
    let flags = seekReadUInt16AsInt32Adv mdv &addr
    let seq = seekReadUInt16AsInt32Adv mdv &addr
    let nameIdx = seekReadStringIdx ctxt mdv &addr
    (flags, seq, nameIdx)

/// Read Table InterfaceImpl.
let seekReadInterfaceImplRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.InterfaceImpl idx
    let tidx = seekReadUntaggedIdx TableNames.TypeDef ctxt mdv &addr
    let intfIdx = seekReadTypeDefOrRefOrSpecIdx ctxt mdv &addr
    (tidx, intfIdx)

/// Read Table MemberRef.
let seekReadMemberRefRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.MemberRef idx
    let mrpIdx = seekReadMemberRefParentIdx ctxt mdv &addr
    let nameIdx = seekReadStringIdx ctxt mdv &addr
    let typeIdx = seekReadBlobIdx ctxt mdv &addr
    (mrpIdx, nameIdx, typeIdx)

/// Read Table Constant.
let seekReadConstantRow (ctxt: ILMetadataReader) idx = ctxt.seekReadConstantRow idx
let seekReadConstantRowUncached ctxtH idx =
    let (ctxt: ILMetadataReader) = getHole ctxtH
    let mdv = ctxt.mdfile.GetView()
    let mutable addr = ctxt.rowAddr TableNames.Constant idx
    let kind = seekReadUInt16Adv mdv &addr
    let parentIdx = seekReadHasConstantIdx ctxt mdv &addr
    let valIdx = seekReadBlobIdx ctxt mdv &addr
    (kind, parentIdx, valIdx)

/// Read Table CustomAttribute.
let seekReadCustomAttributeRow (ctxt: ILMetadataReader) mdv idx (attrRow: byref<CustomAttributeRow>) =
    let mutable addr = ctxt.rowAddr TableNames.CustomAttribute idx
    attrRow.parentIndex <- seekReadHasCustomAttributeIdx ctxt mdv &addr
    attrRow.typeIndex <- seekReadCustomAttributeTypeIdx ctxt mdv &addr
    attrRow.valueIndex <- seekReadBlobIdx ctxt mdv &addr

/// Read Table FieldMarshal.
let seekReadFieldMarshalRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.FieldMarshal idx
    let parentIdx = seekReadHasFieldMarshalIdx ctxt mdv &addr
    let typeIdx = seekReadBlobIdx ctxt mdv &addr
    (parentIdx, typeIdx)

/// Read Table Permission.
let seekReadPermissionRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.Permission idx
    let action = seekReadUInt16Adv mdv &addr
    let parentIdx = seekReadHasDeclSecurityIdx ctxt mdv &addr
    let typeIdx = seekReadBlobIdx ctxt mdv &addr
    (action, parentIdx, typeIdx)

/// Read Table ClassLayout.
let seekReadClassLayoutRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.ClassLayout idx
    let pack = seekReadUInt16Adv mdv &addr
    let size = seekReadInt32Adv mdv &addr
    let tidx = seekReadUntaggedIdx TableNames.TypeDef ctxt mdv &addr
    (pack, size, tidx)

/// Read Table FieldLayout.
let seekReadFieldLayoutRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.FieldLayout idx
    let offset = seekReadInt32Adv mdv &addr
    let fidx = seekReadUntaggedIdx TableNames.Field ctxt mdv &addr
    (offset, fidx)

//// Read Table StandAloneSig.
let seekReadStandAloneSigRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.StandAloneSig idx
    let sigIdx = seekReadBlobIdx ctxt mdv &addr
    sigIdx

/// Read Table EventMap.
let seekReadEventMapRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.EventMap idx
    let tidx = seekReadUntaggedIdx TableNames.TypeDef ctxt mdv &addr
    let eventsIdx = seekReadUntaggedIdx TableNames.Event ctxt mdv &addr
    (tidx, eventsIdx)

/// Read Table Event.
let seekReadEventRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.Event idx
    let flags = seekReadUInt16AsInt32Adv mdv &addr
    let nameIdx = seekReadStringIdx ctxt mdv &addr
    let typIdx = seekReadTypeDefOrRefOrSpecIdx ctxt mdv &addr
    (flags, nameIdx, typIdx)

/// Read Table PropertyMap.
let seekReadPropertyMapRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.PropertyMap idx
    let tidx = seekReadUntaggedIdx TableNames.TypeDef ctxt mdv &addr
    let propsIdx = seekReadUntaggedIdx TableNames.Property ctxt mdv &addr
    (tidx, propsIdx)

/// Read Table Property.
let seekReadPropertyRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.Property idx
    let flags = seekReadUInt16AsInt32Adv mdv &addr
    let nameIdx = seekReadStringIdx ctxt mdv &addr
    let typIdx = seekReadBlobIdx ctxt mdv &addr
    (flags, nameIdx, typIdx)

/// Read Table MethodSemantics.
let seekReadMethodSemanticsRow (ctxt: ILMetadataReader) idx = ctxt.seekReadMethodSemanticsRow idx
let seekReadMethodSemanticsRowUncached ctxtH idx =
    let (ctxt: ILMetadataReader) = getHole ctxtH
    let mdv = ctxt.mdfile.GetView()
    let mutable addr = ctxt.rowAddr TableNames.MethodSemantics idx
    let flags = seekReadUInt16AsInt32Adv mdv &addr
    let midx = seekReadUntaggedIdx TableNames.Method ctxt mdv &addr
    let assocIdx = seekReadHasSemanticsIdx ctxt mdv &addr
    (flags, midx, assocIdx)

/// Read Table MethodImpl.
let seekReadMethodImplRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.MethodImpl idx
    let tidx = seekReadUntaggedIdx TableNames.TypeDef ctxt mdv &addr
    let mbodyIdx = seekReadMethodDefOrRefIdx ctxt mdv &addr
    let mdeclIdx = seekReadMethodDefOrRefIdx ctxt mdv &addr
    (tidx, mbodyIdx, mdeclIdx)

/// Read Table ILModuleRef.
let seekReadModuleRefRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.ModuleRef idx
    let nameIdx = seekReadStringIdx ctxt mdv &addr
    nameIdx

/// Read Table ILTypeSpec.
let seekReadTypeSpecRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.TypeSpec idx
    let blobIdx = seekReadBlobIdx ctxt mdv &addr
    blobIdx

/// Read Table ImplMap.
let seekReadImplMapRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.ImplMap idx
    let flags = seekReadUInt16AsInt32Adv mdv &addr
    let forwrdedIdx = seekReadMemberForwardedIdx ctxt mdv &addr
    let nameIdx = seekReadStringIdx ctxt mdv &addr
    let scopeIdx = seekReadUntaggedIdx TableNames.ModuleRef ctxt mdv &addr
    (flags, forwrdedIdx, nameIdx, scopeIdx)

/// Read Table FieldRVA.
let seekReadFieldRVARow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.FieldRVA idx
    let rva = seekReadInt32Adv mdv &addr
    let fidx = seekReadUntaggedIdx TableNames.Field ctxt mdv &addr
    (rva, fidx)

/// Read Table Assembly.
let seekReadAssemblyRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.Assembly idx
    let hash = seekReadInt32Adv mdv &addr
    let v1 = seekReadUInt16Adv mdv &addr
    let v2 = seekReadUInt16Adv mdv &addr
    let v3 = seekReadUInt16Adv mdv &addr
    let v4 = seekReadUInt16Adv mdv &addr
    let flags = seekReadInt32Adv mdv &addr
    let publicKeyIdx = seekReadBlobIdx ctxt mdv &addr
    let nameIdx = seekReadStringIdx ctxt mdv &addr
    let localeIdx = seekReadStringIdx ctxt mdv &addr
    (hash, v1, v2, v3, v4, flags, publicKeyIdx, nameIdx, localeIdx)

/// Read Table ILAssemblyRef.
let seekReadAssemblyRefRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.AssemblyRef idx
    let v1 = seekReadUInt16Adv mdv &addr
    let v2 = seekReadUInt16Adv mdv &addr
    let v3 = seekReadUInt16Adv mdv &addr
    let v4 = seekReadUInt16Adv mdv &addr
    let flags = seekReadInt32Adv mdv &addr
    let publicKeyOrTokenIdx = seekReadBlobIdx ctxt mdv &addr
    let nameIdx = seekReadStringIdx ctxt mdv &addr
    let localeIdx = seekReadStringIdx ctxt mdv &addr
    let hashValueIdx = seekReadBlobIdx ctxt mdv &addr
    (v1, v2, v3, v4, flags, publicKeyOrTokenIdx, nameIdx, localeIdx, hashValueIdx)

/// Read Table File.
let seekReadFileRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.File idx
    let flags = seekReadInt32Adv mdv &addr
    let nameIdx = seekReadStringIdx ctxt mdv &addr
    let hashValueIdx = seekReadBlobIdx ctxt mdv &addr
    (flags, nameIdx, hashValueIdx)

/// Read Table ILExportedTypeOrForwarder.
let seekReadExportedTypeRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.ExportedType idx
    let flags = seekReadInt32Adv mdv &addr
    let tok = seekReadInt32Adv mdv &addr
    let nameIdx = seekReadStringIdx ctxt mdv &addr
    let namespaceIdx = seekReadStringIdx ctxt mdv &addr
    let implIdx = seekReadImplementationIdx ctxt mdv &addr
    (flags, tok, nameIdx, namespaceIdx, implIdx)

/// Read Table ManifestResource.
let seekReadManifestResourceRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.ManifestResource idx
    let offset = seekReadInt32Adv mdv &addr
    let flags = seekReadInt32Adv mdv &addr
    let nameIdx = seekReadStringIdx ctxt mdv &addr
    let implIdx = seekReadImplementationIdx ctxt mdv &addr
    (offset, flags, nameIdx, implIdx)

/// Read Table Nested.
let seekReadNestedRow (ctxt: ILMetadataReader) idx = ctxt.seekReadNestedRow idx
let seekReadNestedRowUncached ctxtH idx =
    let (ctxt: ILMetadataReader) = getHole ctxtH
    let mdv = ctxt.mdfile.GetView()
    let mutable addr = ctxt.rowAddr TableNames.Nested idx
    let nestedIdx = seekReadUntaggedIdx TableNames.TypeDef ctxt mdv &addr
    let enclIdx = seekReadUntaggedIdx TableNames.TypeDef ctxt mdv &addr
    (nestedIdx, enclIdx)

/// Read Table GenericParam.
let seekReadGenericParamRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.GenericParam idx
    let seq = seekReadUInt16Adv mdv &addr
    let flags = seekReadUInt16Adv mdv &addr
    let ownerIdx = seekReadTypeOrMethodDefIdx ctxt mdv &addr
    let nameIdx = seekReadStringIdx ctxt mdv &addr
    (idx, seq, flags, ownerIdx, nameIdx)

// Read Table GenericParamConstraint.
let seekReadGenericParamConstraintRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.GenericParamConstraint idx
    let pidx = seekReadUntaggedIdx TableNames.GenericParam ctxt mdv &addr
    let constraintIdx = seekReadTypeDefOrRefOrSpecIdx ctxt mdv &addr
    (pidx, constraintIdx)

/// Read Table ILMethodSpec.
let seekReadMethodSpecRow (ctxt: ILMetadataReader) mdv idx =
    let mutable addr = ctxt.rowAddr TableNames.MethodSpec idx
    let mdorIdx = seekReadMethodDefOrRefIdx ctxt mdv &addr
    let instIdx = seekReadBlobIdx ctxt mdv &addr
    (mdorIdx, instIdx)


let readUserStringHeapUncached ctxtH idx =
    let (ctxt: ILMetadataReader) = getHole ctxtH
    let mdv = ctxt.mdfile.GetView()
    seekReadUserString mdv (ctxt.userStringsStreamPhysicalLoc + idx)

let readUserStringHeap (ctxt: ILMetadataReader) idx = ctxt.readUserStringHeap idx

let readStringHeapUncached ctxtH idx =
    let (ctxt: ILMetadataReader) = getHole ctxtH
    let mdv = ctxt.mdfile.GetView()
    seekReadUTF8String mdv (ctxt.stringsStreamPhysicalLoc + idx)

let readStringHeap (ctxt: ILMetadataReader) idx = ctxt.readStringHeap idx

let readStringHeapOption (ctxt: ILMetadataReader) idx = if idx = 0 then None else Some (readStringHeap ctxt idx)

let readBlobHeapUncached ctxtH idx =
    let (ctxt: ILMetadataReader) = getHole ctxtH
    let mdv = ctxt.mdfile.GetView()
    // valid index lies in range [1..streamSize)
    // NOTE: idx cannot be 0 - Blob\String heap has first empty element that mdv one byte 0
    if idx <= 0 || idx >= ctxt.blobsStreamSize then [| |]
    else seekReadBlob mdv (ctxt.blobsStreamPhysicalLoc + idx)

let readBlobHeap (ctxt: ILMetadataReader) idx = ctxt.readBlobHeap idx

let readBlobHeapOption ctxt idx = if idx = 0 then None else Some (readBlobHeap ctxt idx)

//let readGuidHeap ctxt idx = seekReadGuid ctxt.mdv (ctxt.guidsStreamPhysicalLoc + idx)

// read a single value out of a blob heap using the given function
let readBlobHeapAsBool ctxt vidx = fst (sigptrGetBool (readBlobHeap ctxt vidx) 0)
let readBlobHeapAsSByte ctxt vidx = fst (sigptrGetSByte (readBlobHeap ctxt vidx) 0)
let readBlobHeapAsInt16 ctxt vidx = fst (sigptrGetInt16 (readBlobHeap ctxt vidx) 0)
let readBlobHeapAsInt32 ctxt vidx = fst (sigptrGetInt32 (readBlobHeap ctxt vidx) 0)
let readBlobHeapAsInt64 ctxt vidx = fst (sigptrGetInt64 (readBlobHeap ctxt vidx) 0)
let readBlobHeapAsByte ctxt vidx = fst (sigptrGetByte (readBlobHeap ctxt vidx) 0)
let readBlobHeapAsUInt16 ctxt vidx = fst (sigptrGetUInt16 (readBlobHeap ctxt vidx) 0)
let readBlobHeapAsUInt32 ctxt vidx = fst (sigptrGetUInt32 (readBlobHeap ctxt vidx) 0)
let readBlobHeapAsUInt64 ctxt vidx = fst (sigptrGetUInt64 (readBlobHeap ctxt vidx) 0)
let readBlobHeapAsSingle ctxt vidx = fst (sigptrGetSingle (readBlobHeap ctxt vidx) 0)
let readBlobHeapAsDouble ctxt vidx = fst (sigptrGetDouble (readBlobHeap ctxt vidx) 0)

//-----------------------------------------------------------------------
// Some binaries have raw data embedded their text sections, e.g. mscorlib, for
// field inits. And there is no information that definitively tells us the extent of
// the text section that may be interesting data. But we certainly don't want to duplicate
// the entire text section as data!
//
// So, we assume:
//   1. no part of the metadata is double-used for raw data
//   2. the data bits are all the bits of the text section
//      that stretch from a Field or Resource RVA to one of
//        (a) the next Field or resource RVA
//        (b) a MethodRVA
//        (c) the start of the metadata
//        (d) the end of a section
//        (e) the start of the native resources attached to the binary if any
// ----------------------------------------------------------------------*)

// noFileOnDisk indicates that the PE file was read from Memory using OpenILModuleReaderFromBytes
// For example the assembly came from a type provider
// In this case we eagerly read the native resources into memory
let readNativeResources (pectxt: PEReader) =
    [ if pectxt.nativeResourcesSize <> 0x0 && pectxt.nativeResourcesAddr <> 0x0 then
        let start = pectxt.anyV2P (pectxt.fileName + ": native resources", pectxt.nativeResourcesAddr)
        if pectxt.noFileOnDisk then
            let unlinkedResource =
                let linkedResource = seekReadBytes (pectxt.pefile.GetView()) start pectxt.nativeResourcesSize
                unlinkResource pectxt.nativeResourcesAddr linkedResource
            yield ILNativeResource.Out unlinkedResource
        else
            yield ILNativeResource.In (pectxt.fileName, pectxt.nativeResourcesAddr, start, pectxt.nativeResourcesSize ) ]


let getDataEndPointsDelayed (pectxt: PEReader) ctxtH =
    lazy
        let (ctxt: ILMetadataReader) = getHole ctxtH
        let mdv = ctxt.mdfile.GetView()
        let dataStartPoints =
            let res = ref []
            for i = 1 to ctxt.getNumRows TableNames.FieldRVA do
                let rva, _fidx = seekReadFieldRVARow ctxt mdv i
                res := ("field", rva) :: !res
            for i = 1 to ctxt.getNumRows TableNames.ManifestResource do
                let (offset, _, _, TaggedIndex(_tag, idx)) = seekReadManifestResourceRow ctxt mdv i
                if idx = 0 then
                  let rva = pectxt.resourcesAddr + offset
                  res := ("manifest resource", rva) :: !res
            !res
        if isNil dataStartPoints then []
        else
          let methodRVAs =
              let res = ref []
              for i = 1 to ctxt.getNumRows TableNames.Method do
                  let (rva, _, _, nameIdx, _, _) = seekReadMethodRow ctxt mdv i
                  if rva <> 0 then
                     let nm = readStringHeap ctxt nameIdx
                     res := (nm, rva) :: !res
              !res
          ([ pectxt.textSegmentPhysicalLoc + pectxt.textSegmentPhysicalSize
             pectxt.dataSegmentPhysicalLoc + pectxt.dataSegmentPhysicalSize ]
           @
           (List.map pectxt.anyV2P
              (dataStartPoints
                @ [for (virtAddr, _virtSize, _physLoc) in pectxt.sectionHeaders do yield ("section start", virtAddr) done]
                @ [("md", pectxt.metadataAddr)]
                @ (if pectxt.nativeResourcesAddr = 0x0 then [] else [("native resources", pectxt.nativeResourcesAddr) ])
                @ (if pectxt.resourcesAddr = 0x0 then [] else [("managed resources", pectxt.resourcesAddr) ])
                @ (if pectxt.strongnameAddr = 0x0 then [] else [("managed strongname", pectxt.strongnameAddr) ])
                @ (if pectxt.vtableFixupsAddr = 0x0 then [] else [("managed vtable_fixups", pectxt.vtableFixupsAddr) ])
                @ methodRVAs)))
           |> List.distinct
           |> List.sort


let rvaToData (ctxt: ILMetadataReader) (pectxt: PEReader) nm rva =
    if rva = 0x0 then failwith "rva is zero"
    let start = pectxt.anyV2P (nm, rva)
    let endPoints = (Lazy.force ctxt.dataEndPoints)
    let rec look l =
        match l with
        | [] ->
            failwithf "find_text_data_extent: none found for fileName=%s, name=%s, rva=0x%08x, start=0x%08x" ctxt.fileName nm rva start
        | e :: t ->
           if start < e then
             let pev = pectxt.pefile.GetView()
             seekReadBytes pev start (e - start)
           else look t
    look endPoints


//-----------------------------------------------------------------------
// Read the AbsIL structure (lazily) by reading off the relevant rows.
// ----------------------------------------------------------------------

let isSorted (ctxt: ILMetadataReader) (tab: TableName) = ((ctxt.sorted &&& (int64 1 <<< tab.Index)) <> int64 0x0)

// Note, pectxtEager and pevEager must not be captured by the results of this function
let rec seekReadModule (ctxt: ILMetadataReader) canReduceMemory (pectxtEager: PEReader) pevEager peinfo ilMetadataVersion idx =
    let (subsys, subsysversion, useHighEntropyVA, ilOnly, only32, is32bitpreferred, only64, platform, isDll, alignVirt, alignPhys, imageBaseReal) = peinfo
    let mdv = ctxt.mdfile.GetView()
    let (_generation, nameIdx, _mvidIdx, _encidIdx, _encbaseidIdx) = seekReadModuleRow ctxt mdv idx
    let ilModuleName = readStringHeap ctxt nameIdx
    let nativeResources = readNativeResources pectxtEager

    { Manifest =
         if ctxt.getNumRows TableNames.Assembly > 0 then Some (seekReadAssemblyManifest ctxt pectxtEager 1)
         else None
      CustomAttrsStored = ctxt.customAttrsReader_Module
      MetadataIndex = idx
      Name = ilModuleName
      NativeResources=nativeResources
      TypeDefs = mkILTypeDefsComputed (fun () -> seekReadTopTypeDefs ctxt)
      SubSystemFlags = int32 subsys
      IsILOnly = ilOnly
      SubsystemVersion = subsysversion
      UseHighEntropyVA = useHighEntropyVA
      Platform = platform
      StackReserveSize = None  // TODO
      Is32Bit = only32
      Is32BitPreferred = is32bitpreferred
      Is64Bit = only64
      IsDLL=isDll
      VirtualAlignment = alignVirt
      PhysicalAlignment = alignPhys
      ImageBase = imageBaseReal
      MetadataVersion = ilMetadataVersion
      Resources = seekReadManifestResources ctxt canReduceMemory mdv pectxtEager pevEager }

and seekReadAssemblyManifest (ctxt: ILMetadataReader) pectxt idx =
    let mdview = ctxt.mdfile.GetView()
    let (hash, v1, v2, v3, v4, flags, publicKeyIdx, nameIdx, localeIdx) = seekReadAssemblyRow ctxt mdview idx
    let name = readStringHeap ctxt nameIdx
    let pubkey = readBlobHeapOption ctxt publicKeyIdx
    { Name= name
      AuxModuleHashAlgorithm=hash
      SecurityDeclsStored= ctxt.securityDeclsReader_Assembly
      PublicKey= pubkey
      Version= Some (ILVersionInfo (v1, v2, v3, v4))
      Locale= readStringHeapOption ctxt localeIdx
      CustomAttrsStored = ctxt.customAttrsReader_Assembly
      MetadataIndex = idx
      AssemblyLongevity =
        let masked = flags &&& 0x000e
        if masked = 0x0000 then ILAssemblyLongevity.Unspecified
        elif masked = 0x0002 then ILAssemblyLongevity.Library
        elif masked = 0x0004 then ILAssemblyLongevity.PlatformAppDomain
        elif masked = 0x0006 then ILAssemblyLongevity.PlatformProcess
        elif masked = 0x0008 then ILAssemblyLongevity.PlatformSystem
        else ILAssemblyLongevity.Unspecified
      ExportedTypes= seekReadTopExportedTypes ctxt
      EntrypointElsewhere=
            let (tab, tok) = pectxt.entryPointToken
            if tab = TableNames.File then Some (seekReadFile ctxt mdview tok) else None
      Retargetable = 0 <> (flags &&& 0x100)
      DisableJitOptimizations = 0 <> (flags &&& 0x4000)
      JitTracking = 0 <> (flags &&& 0x8000)
      IgnoreSymbolStoreSequencePoints = 0 <> (flags &&& 0x2000) }

and seekReadAssemblyRef (ctxt: ILMetadataReader) idx = ctxt.seekReadAssemblyRef idx
and seekReadAssemblyRefUncached ctxtH idx =
    let (ctxt: ILMetadataReader) = getHole ctxtH
    let mdv = ctxt.mdfile.GetView()
    let (v1, v2, v3, v4, flags, publicKeyOrTokenIdx, nameIdx, localeIdx, hashValueIdx) = seekReadAssemblyRefRow ctxt mdv idx
    let nm = readStringHeap ctxt nameIdx
    let publicKey =
        match readBlobHeapOption ctxt publicKeyOrTokenIdx with
          | None -> None
          | Some blob -> Some (if (flags &&& 0x0001) <> 0x0 then PublicKey blob else PublicKeyToken blob)

    ILAssemblyRef.Create
        (name = nm,
         hash = readBlobHeapOption ctxt hashValueIdx,
         publicKey = publicKey,
         retargetable = ((flags &&& 0x0100) <> 0x0),
         version = Some (ILVersionInfo (v1, v2, v3, v4)),
         locale = readStringHeapOption ctxt localeIdx)

and seekReadModuleRef (ctxt: ILMetadataReader) mdv idx =
    let nameIdx = seekReadModuleRefRow ctxt mdv idx
    ILModuleRef.Create(name = readStringHeap ctxt nameIdx, hasMetadata=true, hash=None)

and seekReadFile (ctxt: ILMetadataReader) mdv idx =
    let (flags, nameIdx, hashValueIdx) = seekReadFileRow ctxt mdv idx
    ILModuleRef.Create(name = readStringHeap ctxt nameIdx, hasMetadata= ((flags &&& 0x0001) = 0x0), hash= readBlobHeapOption ctxt hashValueIdx)

and seekReadClassLayout (ctxt: ILMetadataReader) mdv idx =
    let res =
        seekReadOptionalIndexedRow (ctxt.getNumRows TableNames.ClassLayout,
            seekReadClassLayoutRow ctxt mdv,
            (fun (_, _, tidx) -> tidx),
            simpleIndexCompare idx,
            isSorted ctxt TableNames.ClassLayout,
            (fun (pack, size, _) -> pack, size))
    match res  with
    | None -> { Size = None; Pack = None }
    | Some (pack, size) -> { Size = Some size; Pack = Some pack }

and typeAccessOfFlags flags =
    let f = (flags &&& 0x00000007)
    if f = 0x00000001 then ILTypeDefAccess.Public
    elif f = 0x00000002 then ILTypeDefAccess.Nested ILMemberAccess.Public
    elif f = 0x00000003 then ILTypeDefAccess.Nested ILMemberAccess.Private
    elif f = 0x00000004 then ILTypeDefAccess.Nested ILMemberAccess.Family
    elif f = 0x00000006 then ILTypeDefAccess.Nested ILMemberAccess.FamilyAndAssembly
    elif f = 0x00000007 then ILTypeDefAccess.Nested ILMemberAccess.FamilyOrAssembly
    elif f = 0x00000005 then ILTypeDefAccess.Nested ILMemberAccess.Assembly
    else ILTypeDefAccess.Private

and typeLayoutOfFlags (ctxt: ILMetadataReader) mdv flags tidx =
    let f = (flags &&& 0x00000018)
    if f = 0x00000008 then ILTypeDefLayout.Sequential (seekReadClassLayout ctxt mdv tidx)
    elif f = 0x00000010 then ILTypeDefLayout.Explicit (seekReadClassLayout ctxt mdv tidx)
    else ILTypeDefLayout.Auto

and isTopTypeDef flags =
    (typeAccessOfFlags flags = ILTypeDefAccess.Private) ||
     typeAccessOfFlags flags = ILTypeDefAccess.Public

and seekIsTopTypeDefOfIdx ctxt idx =
    let (flags, _, _, _, _, _) = seekReadTypeDefRow ctxt idx
    isTopTypeDef flags

and readBlobHeapAsSplitTypeName ctxt (nameIdx, namespaceIdx) =
    let name = readStringHeap ctxt nameIdx
    let nspace = readStringHeapOption ctxt namespaceIdx
    match nspace with
    | Some nspace -> splitNamespace nspace, name
    | None -> [], name

and readBlobHeapAsTypeName ctxt (nameIdx, namespaceIdx) =
    let name = readStringHeap ctxt nameIdx
    let nspace = readStringHeapOption ctxt namespaceIdx
    match nspace with
    | None -> name
    | Some ns -> ctxt.memoizeString (ns+"."+name)

and seekReadTypeDefRowExtents (ctxt: ILMetadataReader) _info (idx: int) =
    if idx >= ctxt.getNumRows TableNames.TypeDef then
        struct (ctxt.getNumRows TableNames.Field + 1, ctxt.getNumRows TableNames.Method + 1)
    else
        let (_, _, _, _, fieldsIdx, methodsIdx) = seekReadTypeDefRow ctxt (idx + 1)
        struct (fieldsIdx, methodsIdx )

and seekReadTypeDefRowWithExtents ctxt (idx: int) =
    let info= seekReadTypeDefRow ctxt idx
    info, seekReadTypeDefRowExtents ctxt info idx

and seekReadPreTypeDef ctxt toponly (idx: int) =
    let (flags, nameIdx, namespaceIdx, _, _, _) = seekReadTypeDefRow ctxt idx
    if toponly && not (isTopTypeDef flags) then None
    else
     let ns, n = readBlobHeapAsSplitTypeName ctxt (nameIdx, namespaceIdx)
     // Return the ILPreTypeDef
     Some (mkILPreTypeDefRead (ns, n, idx, ctxt.typeDefReader))

and typeDefReader ctxtH: ILTypeDefStored =
  mkILTypeDefReader
    (fun idx ->
           let (ctxt: ILMetadataReader) = getHole ctxtH
           let mdv = ctxt.mdfile.GetView()
           // Re-read so as not to save all these in the lazy closure - this suspension ctxt.is the largest
           // heavily allocated one in all of AbsIL

           let ((flags, nameIdx, namespaceIdx, extendsIdx, fieldsIdx, methodsIdx) as info) = seekReadTypeDefRow ctxt idx
           let nm = readBlobHeapAsTypeName ctxt (nameIdx, namespaceIdx)
           let struct (endFieldsIdx, endMethodsIdx) = seekReadTypeDefRowExtents ctxt info idx
           let typars = seekReadGenericParams ctxt 0 (tomd_TypeDef, idx)
           let numtypars = typars.Length
           let super = seekReadOptionalTypeDefOrRef ctxt numtypars AsObject extendsIdx
           let layout = typeLayoutOfFlags ctxt mdv flags idx
           let hasLayout = (match layout with ILTypeDefLayout.Explicit _ -> true | _ -> false)
           let mdefs = seekReadMethods ctxt numtypars methodsIdx endMethodsIdx
           let fdefs = seekReadFields ctxt (numtypars, hasLayout) fieldsIdx endFieldsIdx
           let nested = seekReadNestedTypeDefs ctxt idx
           let impls = seekReadInterfaceImpls ctxt mdv numtypars idx
           let mimpls = seekReadMethodImpls ctxt numtypars idx
           let props = seekReadProperties ctxt numtypars idx
           let events = seekReadEvents ctxt numtypars idx
           ILTypeDef(name=nm,
                     genericParams=typars,
                     attributes= enum<TypeAttributes>(flags),
                     layout = layout,
                     nestedTypes= nested,
                     implements = impls,
                     extends = super,
                     methods = mdefs,
                     securityDeclsStored = ctxt.securityDeclsReader_TypeDef,
                     fields=fdefs,
                     methodImpls=mimpls,
                     events= events,
                     properties=props,
                     customAttrsStored=ctxt.customAttrsReader_TypeDef,
                     metadataIndex=idx)
    )

and seekReadTopTypeDefs (ctxt: ILMetadataReader) =
    [| for i = 1 to ctxt.getNumRows TableNames.TypeDef do
          match seekReadPreTypeDef ctxt true i with
          | None -> ()
          | Some td -> yield td |]

and seekReadNestedTypeDefs (ctxt: ILMetadataReader) tidx =
    mkILTypeDefsComputed (fun () ->
           let nestedIdxs = seekReadIndexedRows (ctxt.getNumRows TableNames.Nested, seekReadNestedRow ctxt, snd, simpleIndexCompare tidx, false, fst)
           [| for i in nestedIdxs do
                 match seekReadPreTypeDef ctxt false i with
                 | None -> ()
                 | Some td -> yield td |])

and seekReadInterfaceImpls (ctxt: ILMetadataReader) mdv numtypars tidx =
    seekReadIndexedRows (ctxt.getNumRows TableNames.InterfaceImpl,
                         seekReadInterfaceImplRow ctxt mdv,
                         fst,
                         simpleIndexCompare tidx,
                         isSorted ctxt TableNames.InterfaceImpl,
                         (snd >> seekReadTypeDefOrRef ctxt numtypars AsObject (*ok*) List.empty))

and seekReadGenericParams ctxt numtypars (a, b): ILGenericParameterDefs =
    ctxt.seekReadGenericParams (GenericParamsIdx(numtypars, a, b))

and seekReadGenericParamsUncached ctxtH (GenericParamsIdx(numtypars, a, b)) =
    let (ctxt: ILMetadataReader) = getHole ctxtH
    let mdv = ctxt.mdfile.GetView()
    let pars =
        seekReadIndexedRows
            (ctxt.getNumRows TableNames.GenericParam, seekReadGenericParamRow ctxt mdv,
             (fun (_, _, _, tomd, _) -> tomd),
             tomdCompare (TaggedIndex(a, b)),
             isSorted ctxt TableNames.GenericParam,
             (fun (gpidx, seq, flags, _, nameIdx) ->
                 let flags = int32 flags
                 let variance_flags = flags &&& 0x0003
                 let variance =
                     if variance_flags = 0x0000 then NonVariant
                     elif variance_flags = 0x0001 then CoVariant
                     elif variance_flags = 0x0002 then ContraVariant
                     else NonVariant
                 let constraints = seekReadGenericParamConstraints ctxt mdv numtypars gpidx
                 seq, {Name=readStringHeap ctxt nameIdx
                       Constraints = constraints
                       Variance=variance
                       CustomAttrsStored = ctxt.customAttrsReader_GenericParam
                       MetadataIndex=gpidx
                       HasReferenceTypeConstraint= (flags &&& 0x0004) <> 0
                       HasNotNullableValueTypeConstraint= (flags &&& 0x0008) <> 0
                       HasDefaultConstructorConstraint=(flags &&& 0x0010) <> 0 }))
    pars |> List.sortBy fst |> List.map snd

and seekReadGenericParamConstraints (ctxt: ILMetadataReader) mdv numtypars gpidx =
    seekReadIndexedRows
        (ctxt.getNumRows TableNames.GenericParamConstraint,
         seekReadGenericParamConstraintRow ctxt mdv,
         fst,
         simpleIndexCompare gpidx,
         isSorted ctxt TableNames.GenericParamConstraint,
         (snd >> seekReadTypeDefOrRef ctxt numtypars AsObject (*ok*) List.empty))

and seekReadTypeDefAsType (ctxt: ILMetadataReader) boxity (ginst: ILTypes) idx =
      ctxt.seekReadTypeDefAsType (TypeDefAsTypIdx (boxity, ginst, idx))

and seekReadTypeDefAsTypeUncached ctxtH (TypeDefAsTypIdx (boxity, ginst, idx)) =
    let ctxt = getHole ctxtH
    mkILTy boxity (ILTypeSpec.Create(seekReadTypeDefAsTypeRef ctxt idx, ginst))

and seekReadTypeDefAsTypeRef (ctxt: ILMetadataReader) idx =
     let enc =
       if seekIsTopTypeDefOfIdx ctxt idx then []
       else
         let enclIdx = seekReadIndexedRow (ctxt.getNumRows TableNames.Nested, seekReadNestedRow ctxt, fst, simpleIndexCompare idx, isSorted ctxt TableNames.Nested, snd)
         let tref = seekReadTypeDefAsTypeRef ctxt enclIdx
         tref.Enclosing@[tref.Name]
     let (_, nameIdx, namespaceIdx, _, _, _) = seekReadTypeDefRow ctxt idx
     let nm = readBlobHeapAsTypeName ctxt (nameIdx, namespaceIdx)
     ILTypeRef.Create(scope=ILScopeRef.Local, enclosing=enc, name = nm )

and seekReadTypeRef (ctxt: ILMetadataReader) idx = ctxt.seekReadTypeRef idx
and seekReadTypeRefUncached ctxtH idx =
     let (ctxt: ILMetadataReader) = getHole ctxtH
     let mdv = ctxt.mdfile.GetView()
     let scopeIdx, nameIdx, namespaceIdx = seekReadTypeRefRow ctxt mdv idx
     let scope, enc = seekReadTypeRefScope ctxt mdv scopeIdx
     let nm = readBlobHeapAsTypeName ctxt (nameIdx, namespaceIdx)
     ILTypeRef.Create(scope=scope, enclosing=enc, name = nm)

and seekReadTypeRefAsType (ctxt: ILMetadataReader) boxity ginst idx = ctxt.seekReadTypeRefAsType (TypeRefAsTypIdx (boxity, ginst, idx))
and seekReadTypeRefAsTypeUncached ctxtH (TypeRefAsTypIdx (boxity, ginst, idx)) =
     let ctxt = getHole ctxtH
     mkILTy boxity (ILTypeSpec.Create(seekReadTypeRef ctxt idx, ginst))

and seekReadTypeDefOrRef (ctxt: ILMetadataReader) numtypars boxity (ginst: ILTypes) (TaggedIndex(tag, idx) ) =
    let mdv = ctxt.mdfile.GetView()
    match tag with
    | tag when tag = tdor_TypeDef -> seekReadTypeDefAsType ctxt boxity ginst idx
    | tag when tag = tdor_TypeRef -> seekReadTypeRefAsType ctxt boxity ginst idx
    | tag when tag = tdor_TypeSpec ->
        if not (List.isEmpty ginst) then dprintn ("type spec used as type constructor for a generic instantiation: ignoring instantiation")
        readBlobHeapAsType ctxt numtypars (seekReadTypeSpecRow ctxt mdv idx)
    | _ -> failwith "seekReadTypeDefOrRef ctxt"

and seekReadTypeDefOrRefAsTypeRef (ctxt: ILMetadataReader) (TaggedIndex(tag, idx) ) =
    match tag with
    | tag when tag = tdor_TypeDef -> seekReadTypeDefAsTypeRef ctxt idx
    | tag when tag = tdor_TypeRef -> seekReadTypeRef ctxt idx
    | tag when tag = tdor_TypeSpec ->
        dprintn ("type spec used where a type ref or def is required")
        PrimaryAssemblyILGlobals.typ_Object.TypeRef
    | _ -> failwith "seekReadTypeDefOrRefAsTypeRef_readTypeDefOrRefOrSpec"

and seekReadMethodRefParent (ctxt: ILMetadataReader) mdv numtypars (TaggedIndex(tag, idx)) =
    match tag with
    | tag when tag = mrp_TypeRef -> seekReadTypeRefAsType ctxt AsObject (* not ok - no way to tell if a member ref parent is a value type or not *) List.empty idx
    | tag when tag = mrp_ModuleRef -> mkILTypeForGlobalFunctions (ILScopeRef.Module (seekReadModuleRef ctxt mdv idx))
    | tag when tag = mrp_MethodDef ->
        let (MethodData(enclTy, cc, nm, argtys, retty, minst)) = seekReadMethodDefAsMethodData ctxt idx
        let mspec = mkILMethSpecInTy (enclTy, cc, nm, argtys, retty, minst)
        mspec.DeclaringType
    | tag when tag = mrp_TypeSpec -> readBlobHeapAsType ctxt numtypars (seekReadTypeSpecRow ctxt mdv idx)
    | _ -> failwith "seekReadMethodRefParent"

and seekReadMethodDefOrRef (ctxt: ILMetadataReader) numtypars (TaggedIndex(tag, idx)) =
    match tag with
    | tag when tag = mdor_MethodDef ->
        let (MethodData(enclTy, cc, nm, argtys, retty, minst)) = seekReadMethodDefAsMethodData ctxt idx
        VarArgMethodData(enclTy, cc, nm, argtys, None, retty, minst)
    | tag when tag = mdor_MemberRef ->
        seekReadMemberRefAsMethodData ctxt numtypars idx
    | _ -> failwith "seekReadMethodDefOrRef"

and seekReadMethodDefOrRefNoVarargs (ctxt: ILMetadataReader) numtypars x =
     let (VarArgMethodData(enclTy, cc, nm, argtys, varargs, retty, minst)) = seekReadMethodDefOrRef ctxt numtypars x
     if varargs <> None then dprintf "ignoring sentinel and varargs in ILMethodDef token signature"
     MethodData(enclTy, cc, nm, argtys, retty, minst)

and seekReadCustomAttrType (ctxt: ILMetadataReader) (TaggedIndex(tag, idx) ) =
    match tag with
    | tag when tag = cat_MethodDef ->
        let (MethodData(enclTy, cc, nm, argtys, retty, minst)) = seekReadMethodDefAsMethodData ctxt idx
        mkILMethSpecInTy (enclTy, cc, nm, argtys, retty, minst)
    | tag when tag = cat_MemberRef ->
        let (MethodData(enclTy, cc, nm, argtys, retty, minst)) = seekReadMemberRefAsMethDataNoVarArgs ctxt 0 idx
        mkILMethSpecInTy (enclTy, cc, nm, argtys, retty, minst)
    | _ -> failwith "seekReadCustomAttrType ctxt"

and seekReadImplAsScopeRef (ctxt: ILMetadataReader) mdv (TaggedIndex(tag, idx) ) =
     if idx = 0 then ILScopeRef.Local
     else
       match tag with
       | tag when tag = i_File -> ILScopeRef.Module (seekReadFile ctxt mdv idx)
       | tag when tag = i_AssemblyRef -> ILScopeRef.Assembly (seekReadAssemblyRef ctxt idx)
       | tag when tag = i_ExportedType -> failwith "seekReadImplAsScopeRef"
       | _ -> failwith "seekReadImplAsScopeRef"

and seekReadTypeRefScope (ctxt: ILMetadataReader) mdv (TaggedIndex(tag, idx) ) =
    match tag with
    | tag when tag = rs_Module -> ILScopeRef.Local, []
    | tag when tag = rs_ModuleRef -> ILScopeRef.Module (seekReadModuleRef ctxt mdv idx), []
    | tag when tag = rs_AssemblyRef -> ILScopeRef.Assembly (seekReadAssemblyRef ctxt idx), []
    | tag when tag = rs_TypeRef ->
        let tref = seekReadTypeRef ctxt idx
        tref.Scope, (tref.Enclosing@[tref.Name])
    | _ -> failwith "seekReadTypeRefScope"

and seekReadOptionalTypeDefOrRef (ctxt: ILMetadataReader) numtypars boxity idx =
    if idx = TaggedIndex(tdor_TypeDef, 0) then None
    else Some (seekReadTypeDefOrRef ctxt numtypars boxity List.empty idx)

and seekReadField ctxt mdv (numtypars, hasLayout) (idx: int) =
    let (flags, nameIdx, typeIdx) = seekReadFieldRow ctxt mdv idx
    let nm = readStringHeap ctxt nameIdx
    let isStatic = (flags &&& 0x0010) <> 0
    ILFieldDef(name = nm,
               fieldType= readBlobHeapAsFieldSig ctxt numtypars typeIdx,
               attributes = enum<FieldAttributes>(flags),
               literalValue = (if (flags &&& 0x8000) = 0 then None else Some (seekReadConstant ctxt (TaggedIndex(hc_FieldDef, idx)))),
               marshal =
                   (if (flags &&& 0x1000) = 0 then
                       None
                    else
                       Some (seekReadIndexedRow (ctxt.getNumRows TableNames.FieldMarshal, seekReadFieldMarshalRow ctxt mdv,
                                                 fst, hfmCompare (TaggedIndex(hfm_FieldDef, idx)),
                                                 isSorted ctxt TableNames.FieldMarshal,
                                                 (snd >> readBlobHeapAsNativeType ctxt)))),
               data =
                   (if (flags &&& 0x0100) = 0 then
                       None
                    else
                        match ctxt.pectxtCaptured with
                        | None -> None // indicates metadata only, where Data is not available
                        | Some pectxt ->
                            let rva = seekReadIndexedRow (ctxt.getNumRows TableNames.FieldRVA, seekReadFieldRVARow ctxt mdv,
                                                          snd, simpleIndexCompare idx, isSorted ctxt TableNames.FieldRVA, fst)
                            Some (rvaToData ctxt pectxt "field" rva)),
               offset =
                   (if hasLayout && not isStatic then
                       Some (seekReadIndexedRow (ctxt.getNumRows TableNames.FieldLayout, seekReadFieldLayoutRow ctxt mdv,
                                               snd, simpleIndexCompare idx, isSorted ctxt TableNames.FieldLayout, fst)) else None),
               customAttrsStored=ctxt.customAttrsReader_FieldDef,
               metadataIndex = idx)

and seekReadFields (ctxt: ILMetadataReader) (numtypars, hasLayout) fidx1 fidx2 =
    mkILFieldsLazy
       (lazy
           let mdv = ctxt.mdfile.GetView()
           [ if fidx1 > 0 then
               for i = fidx1 to fidx2 - 1 do
                   yield seekReadField ctxt mdv (numtypars, hasLayout) i ])

and seekReadMethods (ctxt: ILMetadataReader) numtypars midx1 midx2 =
    mkILMethodsComputed (fun () ->
           let mdv = ctxt.mdfile.GetView()
           [| if midx1 > 0 then
                 for i = midx1 to midx2 - 1 do
                     yield seekReadMethod ctxt mdv numtypars i |])

and sigptrGetTypeDefOrRefOrSpecIdx bytes sigptr =
    let struct (n, sigptr) = sigptrGetZInt32 bytes sigptr
    if (n &&& 0x01) = 0x0 then (* Type Def *)
        TaggedIndex(tdor_TypeDef, (n >>>& 2)), sigptr
    else (* Type Ref *)
        TaggedIndex(tdor_TypeRef, (n >>>& 2)), sigptr

and sigptrGetTy (ctxt: ILMetadataReader) numtypars bytes sigptr =
    let b0, sigptr = sigptrGetByte bytes sigptr
    if b0 = et_OBJECT then PrimaryAssemblyILGlobals.typ_Object, sigptr
    elif b0 = et_STRING then PrimaryAssemblyILGlobals.typ_String, sigptr
    elif b0 = et_I1 then PrimaryAssemblyILGlobals.typ_SByte, sigptr
    elif b0 = et_I2 then PrimaryAssemblyILGlobals.typ_Int16, sigptr
    elif b0 = et_I4 then PrimaryAssemblyILGlobals.typ_Int32, sigptr
    elif b0 = et_I8 then PrimaryAssemblyILGlobals.typ_Int64, sigptr
    elif b0 = et_I then PrimaryAssemblyILGlobals.typ_IntPtr, sigptr
    elif b0 = et_U1 then PrimaryAssemblyILGlobals.typ_Byte, sigptr
    elif b0 = et_U2 then PrimaryAssemblyILGlobals.typ_UInt16, sigptr
    elif b0 = et_U4 then PrimaryAssemblyILGlobals.typ_UInt32, sigptr
    elif b0 = et_U8 then PrimaryAssemblyILGlobals.typ_UInt64, sigptr
    elif b0 = et_U then PrimaryAssemblyILGlobals.typ_UIntPtr, sigptr
    elif b0 = et_R4 then PrimaryAssemblyILGlobals.typ_Single, sigptr
    elif b0 = et_R8 then PrimaryAssemblyILGlobals.typ_Double, sigptr
    elif b0 = et_CHAR then PrimaryAssemblyILGlobals.typ_Char, sigptr
    elif b0 = et_BOOLEAN then PrimaryAssemblyILGlobals.typ_Bool, sigptr
    elif b0 = et_WITH then
        let b0, sigptr = sigptrGetByte bytes sigptr
        let tdorIdx, sigptr = sigptrGetTypeDefOrRefOrSpecIdx bytes sigptr
        let struct (n, sigptr) = sigptrGetZInt32 bytes sigptr
        let argtys, sigptr = sigptrFold (sigptrGetTy ctxt numtypars) n bytes sigptr
        seekReadTypeDefOrRef ctxt numtypars (if b0 = et_CLASS then AsObject else AsValue) argtys tdorIdx,
        sigptr

    elif b0 = et_CLASS then
        let tdorIdx, sigptr = sigptrGetTypeDefOrRefOrSpecIdx bytes sigptr
        seekReadTypeDefOrRef ctxt numtypars AsObject List.empty tdorIdx, sigptr
    elif b0 = et_VALUETYPE then
        let tdorIdx, sigptr = sigptrGetTypeDefOrRefOrSpecIdx bytes sigptr
        seekReadTypeDefOrRef ctxt numtypars AsValue List.empty tdorIdx, sigptr
    elif b0 = et_VAR then
        let struct (n, sigptr) = sigptrGetZInt32 bytes sigptr
        ILType.TypeVar (uint16 n), sigptr
    elif b0 = et_MVAR then
        let struct (n, sigptr) = sigptrGetZInt32 bytes sigptr
        ILType.TypeVar (uint16 (n + numtypars)), sigptr
    elif b0 = et_BYREF then
        let ty, sigptr = sigptrGetTy ctxt numtypars bytes sigptr
        ILType.Byref ty, sigptr
    elif b0 = et_PTR then
        let ty, sigptr = sigptrGetTy ctxt numtypars bytes sigptr
        ILType.Ptr ty, sigptr
    elif b0 = et_SZARRAY then
        let ty, sigptr = sigptrGetTy ctxt numtypars bytes sigptr
        mkILArr1DTy ty, sigptr
    elif b0 = et_ARRAY then
        let ty, sigptr = sigptrGetTy ctxt numtypars bytes sigptr
        let struct (rank, sigptr) = sigptrGetZInt32 bytes sigptr
        let struct (numSized, sigptr) = sigptrGetZInt32 bytes sigptr
        let struct (sizes, sigptr) = sigptrFoldStruct sigptrGetZInt32 numSized bytes sigptr
        let struct (numLoBounded, sigptr) = sigptrGetZInt32 bytes sigptr
        let struct (lobounds, sigptr) = sigptrFoldStruct sigptrGetZInt32 numLoBounded bytes sigptr
        let shape =
            let dim i =
              (if i < numLoBounded then Some (List.item i lobounds) else None),
              (if i < numSized then Some (List.item i sizes) else None)
            ILArrayShape (List.init rank dim)
        mkILArrTy (ty, shape), sigptr

    elif b0 = et_VOID then ILType.Void, sigptr
    elif b0 = et_TYPEDBYREF then
        PrimaryAssemblyILGlobals.typ_TypedReference, sigptr
    elif b0 = et_CMOD_REQD || b0 = et_CMOD_OPT then
        let tdorIdx, sigptr = sigptrGetTypeDefOrRefOrSpecIdx bytes sigptr
        let ty, sigptr = sigptrGetTy ctxt numtypars bytes sigptr
        ILType.Modified((b0 = et_CMOD_REQD), seekReadTypeDefOrRefAsTypeRef ctxt tdorIdx, ty), sigptr
    elif b0 = et_FNPTR then
        let ccByte, sigptr = sigptrGetByte bytes sigptr
        let generic, cc = byteAsCallConv ccByte
        if generic then failwith "fptr sig may not be generic"
        let struct (numparams, sigptr) = sigptrGetZInt32 bytes sigptr
        let retty, sigptr = sigptrGetTy ctxt numtypars bytes sigptr
        let argtys, sigptr = sigptrFold (sigptrGetTy ctxt numtypars) ( numparams) bytes sigptr
        let typ =
            ILType.FunctionPointer
              { CallingConv=cc
                ArgTypes = argtys
                ReturnType=retty }
        typ, sigptr
    elif b0 = et_SENTINEL then failwith "varargs NYI"
    else ILType.Void, sigptr

and sigptrGetVarArgTys (ctxt: ILMetadataReader) n numtypars bytes sigptr =
    sigptrFold (sigptrGetTy ctxt numtypars) n bytes sigptr

and sigptrGetArgTys (ctxt: ILMetadataReader) n numtypars bytes sigptr acc =
    if n <= 0 then (List.rev acc, None), sigptr
    else
      let b0, sigptr2 = sigptrGetByte bytes sigptr
      if b0 = et_SENTINEL then
        let varargs, sigptr = sigptrGetVarArgTys ctxt n numtypars bytes sigptr2
        (List.rev acc, Some varargs), sigptr
      else
        let x, sigptr = sigptrGetTy ctxt numtypars bytes sigptr
        sigptrGetArgTys ctxt (n-1) numtypars bytes sigptr (x :: acc)

and sigptrGetLocal (ctxt: ILMetadataReader) numtypars bytes sigptr =
    let pinned, sigptr =
        let b0, sigptr' = sigptrGetByte bytes sigptr
        if b0 = et_PINNED then
            true, sigptr'
        else
            false, sigptr
    let ty, sigptr = sigptrGetTy ctxt numtypars bytes sigptr
    let loc: ILLocal = { IsPinned = pinned; Type = ty; DebugInfo = None }
    loc, sigptr

and readBlobHeapAsMethodSig (ctxt: ILMetadataReader) numtypars blobIdx =
    ctxt.readBlobHeapAsMethodSig (BlobAsMethodSigIdx (numtypars, blobIdx))

and readBlobHeapAsMethodSigUncached ctxtH (BlobAsMethodSigIdx (numtypars, blobIdx)) =
    let (ctxt: ILMetadataReader) = getHole ctxtH
    let bytes = readBlobHeap ctxt blobIdx
    let sigptr = 0
    let ccByte, sigptr = sigptrGetByte bytes sigptr
    let generic, cc = byteAsCallConv ccByte
    let struct (genarity, sigptr) = if generic then sigptrGetZInt32 bytes sigptr else 0x0, sigptr
    let struct (numparams, sigptr) = sigptrGetZInt32 bytes sigptr
    let retty, sigptr = sigptrGetTy ctxt numtypars bytes sigptr
    let (argtys, varargs), _sigptr = sigptrGetArgTys ctxt numparams numtypars bytes sigptr []
    generic, genarity, cc, retty, argtys, varargs

and readBlobHeapAsType ctxt numtypars blobIdx =
    let bytes = readBlobHeap ctxt blobIdx
    let ty, _sigptr = sigptrGetTy ctxt numtypars bytes 0
    ty

and readBlobHeapAsFieldSig ctxt numtypars blobIdx =
    ctxt.readBlobHeapAsFieldSig (BlobAsFieldSigIdx (numtypars, blobIdx))

and readBlobHeapAsFieldSigUncached ctxtH (BlobAsFieldSigIdx (numtypars, blobIdx)) =
    let ctxt = getHole ctxtH
    let bytes = readBlobHeap ctxt blobIdx
    let sigptr = 0
    let ccByte, sigptr = sigptrGetByte bytes sigptr
    if ccByte <> e_IMAGE_CEE_CS_CALLCONV_FIELD then dprintn "warning: field sig was not CC_FIELD"
    let retty, _sigptr = sigptrGetTy ctxt numtypars bytes sigptr
    retty


and readBlobHeapAsPropertySig (ctxt: ILMetadataReader) numtypars blobIdx =
    ctxt.readBlobHeapAsPropertySig (BlobAsPropSigIdx (numtypars, blobIdx))

and readBlobHeapAsPropertySigUncached ctxtH (BlobAsPropSigIdx (numtypars, blobIdx)) =
    let ctxt = getHole ctxtH
    let bytes = readBlobHeap ctxt blobIdx
    let sigptr = 0
    let ccByte, sigptr = sigptrGetByte bytes sigptr
    let hasthis = byteAsHasThis ccByte
    let ccMaxked = (ccByte &&& 0x0Fuy)
    if ccMaxked <> e_IMAGE_CEE_CS_CALLCONV_PROPERTY then dprintn ("warning: property sig was "+string ccMaxked+" instead of CC_PROPERTY")
    let struct (numparams, sigptr) = sigptrGetZInt32 bytes sigptr
    let retty, sigptr = sigptrGetTy ctxt numtypars bytes sigptr
    let argtys, _sigptr = sigptrFold (sigptrGetTy ctxt numtypars) ( numparams) bytes sigptr
    hasthis, retty, argtys

and readBlobHeapAsLocalsSig (ctxt: ILMetadataReader) numtypars blobIdx =
    ctxt.readBlobHeapAsLocalsSig (BlobAsLocalSigIdx (numtypars, blobIdx))

and readBlobHeapAsLocalsSigUncached ctxtH (BlobAsLocalSigIdx (numtypars, blobIdx)) =
    let ctxt = getHole ctxtH
    let bytes = readBlobHeap ctxt blobIdx
    let sigptr = 0
    let ccByte, sigptr = sigptrGetByte bytes sigptr
    if ccByte <> e_IMAGE_CEE_CS_CALLCONV_LOCAL_SIG then dprintn "warning: local sig was not CC_LOCAL"
    let struct (numlocals, sigptr) = sigptrGetZInt32 bytes sigptr
    let localtys, _sigptr = sigptrFold (sigptrGetLocal ctxt numtypars) ( numlocals) bytes sigptr
    localtys

and byteAsHasThis b =
    let hasthis_masked = b &&& 0x60uy
    if hasthis_masked = e_IMAGE_CEE_CS_CALLCONV_INSTANCE then ILThisConvention.Instance
    elif hasthis_masked = e_IMAGE_CEE_CS_CALLCONV_INSTANCE_EXPLICIT then ILThisConvention.InstanceExplicit
    else ILThisConvention.Static

and byteAsCallConv b =
    let cc =
        let ccMaxked = b &&& 0x0Fuy
        if ccMaxked = e_IMAGE_CEE_CS_CALLCONV_FASTCALL then ILArgConvention.FastCall
        elif ccMaxked = e_IMAGE_CEE_CS_CALLCONV_STDCALL then ILArgConvention.StdCall
        elif ccMaxked = e_IMAGE_CEE_CS_CALLCONV_THISCALL then ILArgConvention.ThisCall
        elif ccMaxked = e_IMAGE_CEE_CS_CALLCONV_CDECL then ILArgConvention.CDecl
        elif ccMaxked = e_IMAGE_CEE_CS_CALLCONV_VARARG then ILArgConvention.VarArg
        else ILArgConvention.Default
    let generic = (b &&& e_IMAGE_CEE_CS_CALLCONV_GENERIC) <> 0x0uy
    generic, Callconv (byteAsHasThis b, cc)

and seekReadMemberRefAsMethodData ctxt numtypars idx: VarArgMethodData =
    ctxt.seekReadMemberRefAsMethodData (MemberRefAsMspecIdx (numtypars, idx))

and seekReadMemberRefAsMethodDataUncached ctxtH (MemberRefAsMspecIdx (numtypars, idx)) =
    let (ctxt: ILMetadataReader) = getHole ctxtH
    let mdv = ctxt.mdfile.GetView()
    let (mrpIdx, nameIdx, typeIdx) = seekReadMemberRefRow ctxt mdv idx
    let nm = readStringHeap ctxt nameIdx
    let enclTy = seekReadMethodRefParent ctxt mdv numtypars mrpIdx
    let _generic, genarity, cc, retty, argtys, varargs = readBlobHeapAsMethodSig ctxt enclTy.GenericArgs.Length typeIdx
    let minst = List.init genarity (fun n -> mkILTyvarTy (uint16 (numtypars+n)))
    (VarArgMethodData(enclTy, cc, nm, argtys, varargs, retty, minst))

and seekReadMemberRefAsMethDataNoVarArgs ctxt numtypars idx: MethodData =
   let (VarArgMethodData(enclTy, cc, nm, argtys, varargs, retty, minst)) = seekReadMemberRefAsMethodData ctxt numtypars idx
   if Option.isSome varargs then dprintf "ignoring sentinel and varargs in ILMethodDef token signature"
   (MethodData(enclTy, cc, nm, argtys, retty, minst))

and seekReadMethodSpecAsMethodData (ctxt: ILMetadataReader) numtypars idx =
    ctxt.seekReadMethodSpecAsMethodData (MethodSpecAsMspecIdx (numtypars, idx))

and seekReadMethodSpecAsMethodDataUncached ctxtH (MethodSpecAsMspecIdx (numtypars, idx)) =
    let (ctxt: ILMetadataReader) = getHole ctxtH
    let mdv = ctxt.mdfile.GetView()
    let (mdorIdx, instIdx) = seekReadMethodSpecRow ctxt mdv idx
    let (VarArgMethodData(enclTy, cc, nm, argtys, varargs, retty, _)) = seekReadMethodDefOrRef ctxt numtypars mdorIdx
    let minst =
        let bytes = readBlobHeap ctxt instIdx
        let sigptr = 0
        let ccByte, sigptr = sigptrGetByte bytes sigptr
        if ccByte <> e_IMAGE_CEE_CS_CALLCONV_GENERICINST then dprintn ("warning: method inst ILCallingConv was "+string ccByte+" instead of CC_GENERICINST")
        let struct (numgpars, sigptr) = sigptrGetZInt32 bytes sigptr
        let argtys, _sigptr = sigptrFold (sigptrGetTy ctxt numtypars) numgpars bytes sigptr
        argtys
    VarArgMethodData(enclTy, cc, nm, argtys, varargs, retty, minst)

and seekReadMemberRefAsFieldSpec (ctxt: ILMetadataReader) numtypars idx =
   ctxt.seekReadMemberRefAsFieldSpec (MemberRefAsFspecIdx (numtypars, idx))

and seekReadMemberRefAsFieldSpecUncached ctxtH (MemberRefAsFspecIdx (numtypars, idx)) =
   let (ctxt: ILMetadataReader) = getHole ctxtH
   let mdv = ctxt.mdfile.GetView()
   let (mrpIdx, nameIdx, typeIdx) = seekReadMemberRefRow ctxt mdv idx
   let nm = readStringHeap ctxt nameIdx
   let enclTy = seekReadMethodRefParent ctxt mdv numtypars mrpIdx
   let retty = readBlobHeapAsFieldSig ctxt numtypars typeIdx
   mkILFieldSpecInTy(enclTy, nm, retty)

// One extremely annoying aspect of the MD format is that given a
// ILMethodDef token it is non-trivial to find which ILTypeDef it belongs
// to. So we do a binary chop through the ILTypeDef table
// looking for which ILTypeDef has the ILMethodDef within its range.
// Although the ILTypeDef table is not "sorted", it is effectively sorted by
// method-range and field-range start/finish indexes
and seekReadMethodDefAsMethodData ctxt idx =
   ctxt.seekReadMethodDefAsMethodData idx

and seekReadMethodDefAsMethodDataUncached ctxtH idx =
   let (ctxt: ILMetadataReader) = getHole ctxtH
   let mdv = ctxt.mdfile.GetView()
   // Look for the method def parent.
   let tidx =
     seekReadIndexedRow (ctxt.getNumRows TableNames.TypeDef,
                            (fun i -> i, seekReadTypeDefRowWithExtents ctxt i),
                            (fun r -> r),
                            (fun (_, ((_, _, _, _, _, methodsIdx),
                                      (_, endMethodsIdx))) ->
                                        if endMethodsIdx <= idx then 1
                                        elif methodsIdx <= idx && idx < endMethodsIdx then 0
                                        else -1),
                            true, fst)
   // Create a formal instantiation if needed
   let typeGenericArgs = seekReadGenericParams ctxt 0 (tomd_TypeDef, tidx)
   let typeGenericArgsCount = typeGenericArgs.Length

   let methodGenericArgs = seekReadGenericParams ctxt typeGenericArgsCount (tomd_MethodDef, idx)

   let finst = mkILFormalGenericArgs 0 typeGenericArgs
   let minst = mkILFormalGenericArgs typeGenericArgsCount methodGenericArgs

   // Read the method def parent.
   let enclTy = seekReadTypeDefAsType ctxt AsObject (* not ok: see note *) finst tidx

   // Return the constituent parts: put it together at the place where this is called.
   let (_code_rva, _implflags, _flags, nameIdx, typeIdx, _paramIdx) = seekReadMethodRow ctxt mdv idx
   let nm = readStringHeap ctxt nameIdx

   // Read the method def signature.
   let _generic, _genarity, cc, retty, argtys, varargs = readBlobHeapAsMethodSig ctxt typeGenericArgsCount typeIdx
   if varargs <> None then dprintf "ignoring sentinel and varargs in ILMethodDef token signature"

   MethodData(enclTy, cc, nm, argtys, retty, minst)


and seekReadFieldDefAsFieldSpec (ctxt: ILMetadataReader) idx =
   ctxt.seekReadFieldDefAsFieldSpec idx

and seekReadFieldDefAsFieldSpecUncached ctxtH idx =
   let (ctxt: ILMetadataReader) = getHole ctxtH
   let mdv = ctxt.mdfile.GetView()
   let (_flags, nameIdx, typeIdx) = seekReadFieldRow ctxt mdv idx
   let nm = readStringHeap ctxt nameIdx
   (* Look for the field def parent. *)
   let tidx =
     seekReadIndexedRow (ctxt.getNumRows TableNames.TypeDef,
                            (fun i -> i, seekReadTypeDefRowWithExtents ctxt i),
                            (fun r -> r),
                            (fun (_, ((_, _, _, _, fieldsIdx, _), (endFieldsIdx, _))) ->
                                if endFieldsIdx <= idx then 1
                                elif fieldsIdx <= idx && idx < endFieldsIdx then 0
                                else -1),
                            true, fst)
   // Read the field signature.
   let retty = readBlobHeapAsFieldSig ctxt 0 typeIdx

   // Create a formal instantiation if needed
   let finst = mkILFormalGenericArgs 0 (seekReadGenericParams ctxt 0 (tomd_TypeDef, tidx))

   // Read the field def parent.
   let enclTy = seekReadTypeDefAsType ctxt AsObject (* not ok: see note *) finst tidx

   // Put it together.
   mkILFieldSpecInTy(enclTy, nm, retty)

and seekReadMethod (ctxt: ILMetadataReader) mdv numtypars (idx: int) =
     let (codeRVA, implflags, flags, nameIdx, typeIdx, paramIdx) = seekReadMethodRow ctxt mdv idx
     let nm = readStringHeap ctxt nameIdx
     let abstr = (flags &&& 0x0400) <> 0x0
     let pinvoke = (flags &&& 0x2000) <> 0x0
     let codetype = implflags &&& 0x0003
     let unmanaged = (implflags &&& 0x0004) <> 0x0
     let internalcall = (implflags &&& 0x1000) <> 0x0
     let noinline = (implflags &&& 0x0008) <> 0x0
     let aggressiveinline = (implflags &&& 0x0100) <> 0x0
     let _generic, _genarity, cc, retty, argtys, varargs = readBlobHeapAsMethodSig ctxt numtypars typeIdx
     if varargs <> None then dprintf "ignoring sentinel and varargs in ILMethodDef signature"

     let endParamIdx =
       if idx >= ctxt.getNumRows TableNames.Method then
         ctxt.getNumRows TableNames.Param + 1
       else
         let (_, _, _, _, _, paramIdx) = seekReadMethodRow ctxt mdv (idx + 1)
         paramIdx

     let ret, ilParams = seekReadParams ctxt mdv (retty, argtys) paramIdx endParamIdx

     let isEntryPoint =
         let (tab, tok) = ctxt.entryPointToken
         (tab = TableNames.Method && tok = idx)

     let body =
         if (codetype = 0x01) && pinvoke then
             methBodyNative
         elif pinvoke then
             seekReadImplMap ctxt nm idx
         elif internalcall || abstr || unmanaged || (codetype <> 0x00) then
             methBodyAbstract
         else
             match ctxt.pectxtCaptured with
             | None -> methBodyNotAvailable
             | Some pectxt -> seekReadMethodRVA pectxt ctxt (idx, nm, internalcall, noinline, aggressiveinline, numtypars) codeRVA

     ILMethodDef(name=nm,
                 attributes = enum<MethodAttributes>(flags),
                 implAttributes= enum<MethodImplAttributes>(implflags),
                 securityDeclsStored=ctxt.securityDeclsReader_MethodDef,
                 isEntryPoint=isEntryPoint,
                 genericParams=seekReadGenericParams ctxt numtypars (tomd_MethodDef, idx),
                 parameters= ilParams,
                 callingConv=cc,
                 ret=ret,
                 body=body,
                 customAttrsStored=ctxt.customAttrsReader_MethodDef,
                 metadataIndex=idx)


and seekReadParams (ctxt: ILMetadataReader) mdv (retty, argtys) pidx1 pidx2 =
    let retRes = ref (mkILReturn retty)
    let paramsRes = argtys |> List.toArray |> Array.map mkILParamAnon
    for i = pidx1 to pidx2 - 1 do
        seekReadParamExtras ctxt mdv (retRes, paramsRes) i
    !retRes, List.ofArray paramsRes

and seekReadParamExtras (ctxt: ILMetadataReader) mdv (retRes, paramsRes) (idx: int) =
   let (flags, seq, nameIdx) = seekReadParamRow ctxt mdv idx
   let inOutMasked = (flags &&& 0x00FF)
   let hasMarshal = (flags &&& 0x2000) <> 0x0
   let hasDefault = (flags &&& 0x1000) <> 0x0
   let fmReader idx = seekReadIndexedRow (ctxt.getNumRows TableNames.FieldMarshal, seekReadFieldMarshalRow ctxt mdv, fst, hfmCompare idx, isSorted ctxt TableNames.FieldMarshal, (snd >> readBlobHeapAsNativeType ctxt))
   if seq = 0 then
       retRes := { !retRes with
                        Marshal=(if hasMarshal then Some (fmReader (TaggedIndex(hfm_ParamDef, idx))) else None)
                        CustomAttrsStored = ctxt.customAttrsReader_ParamDef
                        MetadataIndex = idx}
   elif seq > Array.length paramsRes then dprintn "bad seq num. for param"
   else
       paramsRes.[seq - 1] <-
          { paramsRes.[seq - 1] with
               Marshal=(if hasMarshal then Some (fmReader (TaggedIndex(hfm_ParamDef, idx))) else None)
               Default = (if hasDefault then Some (seekReadConstant ctxt (TaggedIndex(hc_ParamDef, idx))) else None)
               Name = readStringHeapOption ctxt nameIdx
               IsIn = ((inOutMasked &&& 0x0001) <> 0x0)
               IsOut = ((inOutMasked &&& 0x0002) <> 0x0)
               IsOptional = ((inOutMasked &&& 0x0010) <> 0x0)
               CustomAttrsStored = ctxt.customAttrsReader_ParamDef
               MetadataIndex = idx }

and seekReadMethodImpls (ctxt: ILMetadataReader) numtypars tidx =
   mkILMethodImplsLazy
      (lazy
          let mdv = ctxt.mdfile.GetView()
          let mimpls = seekReadIndexedRows (ctxt.getNumRows TableNames.MethodImpl, seekReadMethodImplRow ctxt mdv, (fun (a, _, _) -> a), simpleIndexCompare tidx, isSorted ctxt TableNames.MethodImpl, (fun (_, b, c) -> b, c))
          mimpls |> List.map (fun (b, c) ->
              { OverrideBy=
                  let (MethodData(enclTy, cc, nm, argtys, retty, minst)) = seekReadMethodDefOrRefNoVarargs ctxt numtypars b
                  mkILMethSpecInTy (enclTy, cc, nm, argtys, retty, minst)
                Overrides=
                  let (MethodData(enclTy, cc, nm, argtys, retty, minst)) = seekReadMethodDefOrRefNoVarargs ctxt numtypars c
                  let mspec = mkILMethSpecInTy (enclTy, cc, nm, argtys, retty, minst)
                  OverridesSpec(mspec.MethodRef, mspec.DeclaringType) }))

and seekReadMultipleMethodSemantics (ctxt: ILMetadataReader) (flags, id) =
    seekReadIndexedRows
      (ctxt.getNumRows TableNames.MethodSemantics,
       seekReadMethodSemanticsRow ctxt,
       (fun (_flags, _, c) -> c),
       hsCompare id,
       isSorted ctxt TableNames.MethodSemantics,
       (fun (a, b, _c) ->
           let (MethodData(enclTy, cc, nm, argtys, retty, minst)) = seekReadMethodDefAsMethodData ctxt b
           a, (mkILMethSpecInTy (enclTy, cc, nm, argtys, retty, minst)).MethodRef))
    |> List.filter (fun (flags2, _) -> flags = flags2)
    |> List.map snd


and seekReadoptional_MethodSemantics ctxt id =
    match seekReadMultipleMethodSemantics ctxt id with
    | [] -> None
    | [h] -> Some h
    | h :: _ -> dprintn "multiple method semantics found"; Some h

and seekReadMethodSemantics ctxt id =
    match seekReadoptional_MethodSemantics ctxt id with
    | None -> failwith "seekReadMethodSemantics ctxt: no method found"
    | Some x -> x

and seekReadEvent ctxt mdv numtypars idx =
   let (flags, nameIdx, typIdx) = seekReadEventRow ctxt mdv idx
   ILEventDef(eventType = seekReadOptionalTypeDefOrRef ctxt numtypars AsObject typIdx,
              name = readStringHeap ctxt nameIdx,
              attributes = enum<EventAttributes>(flags),
              addMethod= seekReadMethodSemantics ctxt (0x0008, TaggedIndex(hs_Event, idx)),
              removeMethod=seekReadMethodSemantics ctxt (0x0010, TaggedIndex(hs_Event, idx)),
              fireMethod=seekReadoptional_MethodSemantics ctxt (0x0020, TaggedIndex(hs_Event, idx)),
              otherMethods = seekReadMultipleMethodSemantics ctxt (0x0004, TaggedIndex(hs_Event, idx)),
              customAttrsStored=ctxt.customAttrsReader_Event,
              metadataIndex = idx )

  (* REVIEW: can substantially reduce numbers of EventMap and PropertyMap reads by first checking if the whole table mdv sorted according to ILTypeDef tokens and then doing a binary chop *)
and seekReadEvents (ctxt: ILMetadataReader) numtypars tidx =
   mkILEventsLazy
      (lazy
           let mdv = ctxt.mdfile.GetView()
           match seekReadOptionalIndexedRow (ctxt.getNumRows TableNames.EventMap, (fun i -> i, seekReadEventMapRow ctxt mdv i), (fun (_, row) -> fst row), compare tidx, false, (fun (i, row) -> (i, snd row))) with
           | None -> []
           | Some (rowNum, beginEventIdx) ->
               let endEventIdx =
                   if rowNum >= ctxt.getNumRows TableNames.EventMap then
                       ctxt.getNumRows TableNames.Event + 1
                   else
                       let (_, endEventIdx) = seekReadEventMapRow ctxt mdv (rowNum + 1)
                       endEventIdx

               [ if beginEventIdx > 0 then
                   for i in beginEventIdx .. endEventIdx - 1 do
                     yield seekReadEvent ctxt mdv numtypars i ])

and seekReadProperty ctxt mdv numtypars idx =
   let (flags, nameIdx, typIdx) = seekReadPropertyRow ctxt mdv idx
   let cc, retty, argtys = readBlobHeapAsPropertySig ctxt numtypars typIdx
   let setter= seekReadoptional_MethodSemantics ctxt (0x0001, TaggedIndex(hs_Property, idx))
   let getter = seekReadoptional_MethodSemantics ctxt (0x0002, TaggedIndex(hs_Property, idx))
(* NOTE: the "ThisConv" value on the property is not reliable: better to look on the getter/setter *)
(* NOTE: e.g. tlbimp on Office msword.olb seems to set this incorrectly *)
   let cc2 =
       match getter with
       | Some mref -> mref.CallingConv.ThisConv
       | None ->
           match setter with
           | Some mref -> mref.CallingConv .ThisConv
           | None -> cc

   ILPropertyDef(name=readStringHeap ctxt nameIdx,
                 callingConv = cc2,
                 attributes = enum<PropertyAttributes>(flags),
                 setMethod=setter,
                 getMethod=getter,
                 propertyType=retty,
                 init= (if (flags &&& 0x1000) = 0 then None else Some (seekReadConstant ctxt (TaggedIndex(hc_Property, idx)))),
                 args=argtys,
                 customAttrsStored=ctxt.customAttrsReader_Property,
                 metadataIndex = idx )

and seekReadProperties (ctxt: ILMetadataReader) numtypars tidx =
   mkILPropertiesLazy
      (lazy
           let mdv = ctxt.mdfile.GetView()
           match seekReadOptionalIndexedRow (ctxt.getNumRows TableNames.PropertyMap, (fun i -> i, seekReadPropertyMapRow ctxt mdv i), (fun (_, row) -> fst row), compare tidx, false, (fun (i, row) -> (i, snd row))) with
           | None -> []
           | Some (rowNum, beginPropIdx) ->
               let endPropIdx =
                   if rowNum >= ctxt.getNumRows TableNames.PropertyMap then
                       ctxt.getNumRows TableNames.Property + 1
                   else
                       let (_, endPropIdx) = seekReadPropertyMapRow ctxt mdv (rowNum + 1)
                       endPropIdx
               [ if beginPropIdx > 0 then
                   for i in beginPropIdx .. endPropIdx - 1 do
                     yield seekReadProperty ctxt mdv numtypars i ])


and customAttrsReader ctxtH tag: ILAttributesStored =
    mkILCustomAttrsReader
        (fun idx ->
            let (ctxt: ILMetadataReader) = getHole ctxtH
            let mdv = ctxt.mdfile.GetView()
            let reader =
                { new ISeekReadIndexedRowReader<CustomAttributeRow, TaggedIndex<HasCustomAttributeTag>, ILAttribute> with
                    member _.GetRow(i, row) = seekReadCustomAttributeRow ctxt mdv i &row
                    member _.GetKey(attrRow) = attrRow.parentIndex
                    member _.CompareKey(key) = hcaCompare (TaggedIndex(tag, idx)) key
                    member _.ConvertRow(attrRow) = seekReadCustomAttr ctxt (attrRow.typeIndex, attrRow.valueIndex)
                }
            seekReadIndexedRowsByInterface (ctxt.getNumRows TableNames.CustomAttribute) (isSorted ctxt TableNames.CustomAttribute) reader)

and seekReadCustomAttr ctxt (TaggedIndex(cat, idx), b) =
    ctxt.seekReadCustomAttr (CustomAttrIdx (cat, idx, b))

and seekReadCustomAttrUncached ctxtH (CustomAttrIdx (cat, idx, valIdx)) =
    let ctxt = getHole ctxtH
    let method = seekReadCustomAttrType ctxt (TaggedIndex(cat, idx))
    let data =
        match readBlobHeapOption ctxt valIdx with
        | Some bytes -> bytes
        | None -> Bytes.ofInt32Array [| |]
    let elements = []
    ILAttribute.Encoded (method, data, elements)

and securityDeclsReader ctxtH tag =
    mkILSecurityDeclsReader
      (fun idx ->
         let (ctxt: ILMetadataReader) = getHole ctxtH
         let mdv = ctxt.mdfile.GetView()
         seekReadIndexedRows (ctxt.getNumRows TableNames.Permission,
                                 seekReadPermissionRow ctxt mdv,
                                 (fun (_, par, _) -> par),
                                 hdsCompare (TaggedIndex(tag,idx)),
                                 isSorted ctxt TableNames.Permission,
                                 (fun (act, _, ty) -> seekReadSecurityDecl ctxt (act, ty)))
          |> List.toArray)

and seekReadSecurityDecl ctxt (act, ty) =
    ILSecurityDecl ((if List.memAssoc (int act) (Lazy.force ILSecurityActionRevMap) then List.assoc (int act) (Lazy.force ILSecurityActionRevMap) else failwith "unknown security action"),
                    readBlobHeap ctxt ty)

and seekReadConstant (ctxt: ILMetadataReader) idx =
  let kind, vidx = seekReadIndexedRow (ctxt.getNumRows TableNames.Constant,
                                      seekReadConstantRow ctxt,
                                      (fun (_, key, _) -> key),
                                      hcCompare idx, isSorted ctxt TableNames.Constant, (fun (kind, _, v) -> kind, v))
  match kind with
  | x when x = uint16 et_STRING ->
    let blobHeap = readBlobHeap ctxt vidx
    let s = System.Text.Encoding.Unicode.GetString(blobHeap, 0, blobHeap.Length)
    ILFieldInit.String s
  | x when x = uint16 et_BOOLEAN -> ILFieldInit.Bool (readBlobHeapAsBool ctxt vidx)
  | x when x = uint16 et_CHAR -> ILFieldInit.Char (readBlobHeapAsUInt16 ctxt vidx)
  | x when x = uint16 et_I1 -> ILFieldInit.Int8 (readBlobHeapAsSByte ctxt vidx)
  | x when x = uint16 et_I2 -> ILFieldInit.Int16 (readBlobHeapAsInt16 ctxt vidx)
  | x when x = uint16 et_I4 -> ILFieldInit.Int32 (readBlobHeapAsInt32 ctxt vidx)
  | x when x = uint16 et_I8 -> ILFieldInit.Int64 (readBlobHeapAsInt64 ctxt vidx)
  | x when x = uint16 et_U1 -> ILFieldInit.UInt8 (readBlobHeapAsByte ctxt vidx)
  | x when x = uint16 et_U2 -> ILFieldInit.UInt16 (readBlobHeapAsUInt16 ctxt vidx)
  | x when x = uint16 et_U4 -> ILFieldInit.UInt32 (readBlobHeapAsUInt32 ctxt vidx)
  | x when x = uint16 et_U8 -> ILFieldInit.UInt64 (readBlobHeapAsUInt64 ctxt vidx)
  | x when x = uint16 et_R4 -> ILFieldInit.Single (readBlobHeapAsSingle ctxt vidx)
  | x when x = uint16 et_R8 -> ILFieldInit.Double (readBlobHeapAsDouble ctxt vidx)
  | x when x = uint16 et_CLASS || x = uint16 et_OBJECT -> ILFieldInit.Null
  | _ -> ILFieldInit.Null

and seekReadImplMap (ctxt: ILMetadataReader) nm midx =
 lazy
   MethodBody.PInvoke
      (lazy
            let mdv = ctxt.mdfile.GetView()
            let (flags, nameIdx, scopeIdx) = seekReadIndexedRow (ctxt.getNumRows TableNames.ImplMap,
                                                                seekReadImplMapRow ctxt mdv,
                                                                (fun (_, m, _, _) -> m),
                                                                mfCompare (TaggedIndex(mf_MethodDef, midx)),
                                                                isSorted ctxt TableNames.ImplMap,
                                                                (fun (a, _, c, d) -> a, c, d))
            let cc =
                let masked = flags &&& 0x0700
                if masked = 0x0000 then PInvokeCallingConvention.None
                elif masked = 0x0200 then PInvokeCallingConvention.Cdecl
                elif masked = 0x0300 then PInvokeCallingConvention.Stdcall
                elif masked = 0x0400 then PInvokeCallingConvention.Thiscall
                elif masked = 0x0500 then PInvokeCallingConvention.Fastcall
                elif masked = 0x0100 then PInvokeCallingConvention.WinApi
                else (dprintn "strange CallingConv"; PInvokeCallingConvention.None)

            let enc =
                let masked = flags &&& 0x0006
                if masked = 0x0000 then PInvokeCharEncoding.None
                elif masked = 0x0002 then PInvokeCharEncoding.Ansi
                elif masked = 0x0004 then PInvokeCharEncoding.Unicode
                elif masked = 0x0006 then PInvokeCharEncoding.Auto
                else (dprintn "strange CharEncoding"; PInvokeCharEncoding.None)

            let bestfit =
                let masked = flags &&& 0x0030
                if masked = 0x0000 then PInvokeCharBestFit.UseAssembly
                elif masked = 0x0010 then PInvokeCharBestFit.Enabled
                elif masked = 0x0020 then PInvokeCharBestFit.Disabled
                else (dprintn "strange CharBestFit"; PInvokeCharBestFit.UseAssembly)

            let unmap =
                let masked = flags &&& 0x3000
                if masked = 0x0000 then PInvokeThrowOnUnmappableChar.UseAssembly
                elif masked = 0x1000 then PInvokeThrowOnUnmappableChar.Enabled
                elif masked = 0x2000 then PInvokeThrowOnUnmappableChar.Disabled
                else (dprintn "strange ThrowOnUnmappableChar"; PInvokeThrowOnUnmappableChar.UseAssembly)

            { CallingConv = cc
              CharEncoding = enc
              CharBestFit=bestfit
              ThrowOnUnmappableChar=unmap
              NoMangle = (flags &&& 0x0001) <> 0x0
              LastError = (flags &&& 0x0040) <> 0x0
              Name =
                  (match readStringHeapOption ctxt nameIdx with
                   | None -> nm
                   | Some nm2 -> nm2)
              Where = seekReadModuleRef ctxt mdv scopeIdx })

and seekReadTopCode (ctxt: ILMetadataReader) pev mdv numtypars (sz: int) start seqpoints =
   let labelsOfRawOffsets = new Dictionary<_, _>(sz/2)
   let ilOffsetsOfLabels = new Dictionary<_, _>(sz/2)

   let rawToLabel rawOffset =
       match labelsOfRawOffsets.TryGetValue rawOffset with
       | true, l -> l
       | _ ->
           let lab = generateCodeLabel()
           labelsOfRawOffsets.[rawOffset] <- lab
           lab

   let markAsInstructionStart rawOffset ilOffset =
       let lab = rawToLabel rawOffset
       ilOffsetsOfLabels.[lab] <- ilOffset

   let ibuf = new ResizeArray<_>(sz/2)
   let curr = ref 0
   let prefixes = { al=Aligned; tl= Normalcall; vol= Nonvolatile;ro=NormalAddress;constrained=None }
   let lastb = ref 0x0
   let lastb2 = ref 0x0
   let b = ref 0x0
   let get () =
       lastb := seekReadByteAsInt32 pev (start + (!curr))
       incr curr
       b :=
         if !lastb = 0xfe && !curr < sz then
           lastb2 := seekReadByteAsInt32 pev (start + (!curr))
           incr curr
           !lastb2
         else
           !lastb

   let seqPointsRemaining = ref seqpoints

   while !curr < sz do
     // registering "+string !curr+" as start of an instruction")
     markAsInstructionStart !curr ibuf.Count

     // Insert any sequence points into the instruction sequence
     while
         (match !seqPointsRemaining with
          | (i, _tag) :: _rest when i <= !curr -> true
          | _ -> false)
        do
         // Emitting one sequence point
         let (_, tag) = List.head !seqPointsRemaining
         seqPointsRemaining := List.tail !seqPointsRemaining
         ibuf.Add (I_seqpoint tag)

     // Read the prefixes. Leave lastb and lastb2 holding the instruction byte(s)
     begin
       prefixes.al <- Aligned
       prefixes.tl <- Normalcall
       prefixes.vol <- Nonvolatile
       prefixes.ro<-NormalAddress
       prefixes.constrained<-None
       get ()
       while !curr < sz &&
         !lastb = 0xfe &&
         (!b = (i_constrained &&& 0xff) ||
          !b = (i_readonly &&& 0xff) ||
          !b = (i_unaligned &&& 0xff) ||
          !b = (i_volatile &&& 0xff) ||
          !b = (i_tail &&& 0xff)) do
         begin
             if !b = (i_unaligned &&& 0xff) then
               let unal = seekReadByteAsInt32 pev (start + (!curr))
               incr curr
               prefixes.al <-
                  if unal = 0x1 then Unaligned1
                  elif unal = 0x2 then Unaligned2
                  elif unal = 0x4 then Unaligned4
                  else (dprintn "bad alignment for unaligned"; Aligned)
             elif !b = (i_volatile &&& 0xff) then prefixes.vol <- Volatile
             elif !b = (i_readonly &&& 0xff) then prefixes.ro <- ReadonlyAddress
             elif !b = (i_constrained &&& 0xff) then
                 let uncoded = seekReadUncodedToken pev (start + (!curr))
                 curr := !curr + 4
                 let ty = seekReadTypeDefOrRef ctxt numtypars AsObject [] (uncodedTokenToTypeDefOrRefOrSpec uncoded)
                 prefixes.constrained <- Some ty
             else prefixes.tl <- Tailcall
         end
         get ()
     end

     // data for instruction begins at "+string !curr
     // Read and decode the instruction
     if (!curr <= sz) then
       let idecoder =
           if !lastb = 0xfe then getTwoByteInstr ( !lastb2)
           else getOneByteInstr ( !lastb)
       let instr =
         match idecoder with
         | I_u16_u8_instr f ->
             let x = seekReadByte pev (start + (!curr)) |> uint16
             curr := !curr + 1
             f prefixes x
         | I_u16_u16_instr f ->
             let x = seekReadUInt16 pev (start + (!curr))
             curr := !curr + 2
             f prefixes x
         | I_none_instr f ->
             f prefixes
         | I_i64_instr f ->
             let x = seekReadInt64 pev (start + (!curr))
             curr := !curr + 8
             f prefixes x
         | I_i32_i8_instr f ->
             let x = seekReadSByte pev (start + (!curr)) |> int32
             curr := !curr + 1
             f prefixes x
         | I_i32_i32_instr f ->
             let x = seekReadInt32 pev (start + (!curr))
             curr := !curr + 4
             f prefixes x
         | I_r4_instr f ->
             let x = seekReadSingle pev (start + (!curr))
             curr := !curr + 4
             f prefixes x
         | I_r8_instr f ->
             let x = seekReadDouble pev (start + (!curr))
             curr := !curr + 8
             f prefixes x
         | I_field_instr f ->
             let (tab, tok) = seekReadUncodedToken pev (start + (!curr))
             curr := !curr + 4
             let fspec =
               if tab = TableNames.Field then
                 seekReadFieldDefAsFieldSpec ctxt tok
               elif tab = TableNames.MemberRef then
                 seekReadMemberRefAsFieldSpec ctxt numtypars tok
               else failwith "bad table in FieldDefOrRef"
             f prefixes fspec
         | I_method_instr f ->
             // method instruction, curr = "+string !curr

             let (tab, idx) = seekReadUncodedToken pev (start + (!curr))
             curr := !curr + 4
             let (VarArgMethodData(enclTy, cc, nm, argtys, varargs, retty, minst)) =
               if tab = TableNames.Method then
                 seekReadMethodDefOrRef ctxt numtypars (TaggedIndex(mdor_MethodDef, idx))
               elif tab = TableNames.MemberRef then
                 seekReadMethodDefOrRef ctxt numtypars (TaggedIndex(mdor_MemberRef, idx))
               elif tab = TableNames.MethodSpec then
                 seekReadMethodSpecAsMethodData ctxt numtypars idx
               else failwith "bad table in MethodDefOrRefOrSpec"
             match enclTy with
             | ILType.Array (shape, ty) ->
               match nm with
               | "Get" -> I_ldelem_any(shape, ty)
               | "Set" -> I_stelem_any(shape, ty)
               | "Address" -> I_ldelema(prefixes.ro, false, shape, ty)
               | ".ctor" -> I_newarr(shape, ty)
               | _ -> failwith "bad method on array type"
             | _ ->
               let mspec = mkILMethSpecInTy (enclTy, cc, nm, argtys, retty, minst)
               f prefixes (mspec, varargs)
         | I_type_instr f ->
             let uncoded = seekReadUncodedToken pev (start + (!curr))
             curr := !curr + 4
             let ty = seekReadTypeDefOrRef ctxt numtypars AsObject [] (uncodedTokenToTypeDefOrRefOrSpec uncoded)
             f prefixes ty
         | I_string_instr f ->
             let (tab, idx) = seekReadUncodedToken pev (start + (!curr))
             curr := !curr + 4
             if tab <> TableNames.UserStrings then dprintn "warning: bad table in user string for ldstr"
             f prefixes (readUserStringHeap ctxt idx)

         | I_conditional_i32_instr f ->
             let offsDest = (seekReadInt32 pev (start + (!curr)))
             curr := !curr + 4
             let dest = !curr + offsDest
             f prefixes (rawToLabel dest)
         | I_conditional_i8_instr f ->
             let offsDest = int (seekReadSByte pev (start + (!curr)))
             curr := !curr + 1
             let dest = !curr + offsDest
             f prefixes (rawToLabel dest)
         | I_unconditional_i32_instr f ->
             let offsDest = (seekReadInt32 pev (start + (!curr)))
             curr := !curr + 4
             let dest = !curr + offsDest
             f prefixes (rawToLabel dest)
         | I_unconditional_i8_instr f ->
             let offsDest = int (seekReadSByte pev (start + (!curr)))
             curr := !curr + 1
             let dest = !curr + offsDest
             f prefixes (rawToLabel dest)
         | I_invalid_instr ->
             dprintn ("invalid instruction: "+string !lastb+ (if !lastb = 0xfe then ", "+string !lastb2 else ""))
             I_ret
         | I_tok_instr f ->
             let (tab, idx) = seekReadUncodedToken pev (start + (!curr))
             curr := !curr + 4
             (* REVIEW: this incorrectly labels all MemberRef tokens as ILMethod's: we should go look at the MemberRef sig to determine if it is a field or method *)
             let token_info =
               if tab = TableNames.Method || tab = TableNames.MemberRef (* REVIEW: generics or tab = TableNames.MethodSpec *) then
                 let (MethodData(enclTy, cc, nm, argtys, retty, minst)) = seekReadMethodDefOrRefNoVarargs ctxt numtypars (uncodedTokenToMethodDefOrRef (tab, idx))
                 ILToken.ILMethod (mkILMethSpecInTy (enclTy, cc, nm, argtys, retty, minst))
               elif tab = TableNames.Field then
                 ILToken.ILField (seekReadFieldDefAsFieldSpec ctxt idx)
               elif tab = TableNames.TypeDef || tab = TableNames.TypeRef || tab = TableNames.TypeSpec then
                 ILToken.ILType (seekReadTypeDefOrRef ctxt numtypars AsObject [] (uncodedTokenToTypeDefOrRefOrSpec (tab, idx)))
               else failwith "bad token for ldtoken"
             f prefixes token_info
         | I_sig_instr f ->
             let (tab, idx) = seekReadUncodedToken pev (start + (!curr))
             curr := !curr + 4
             if tab <> TableNames.StandAloneSig then dprintn "strange table for callsig token"
             let generic, _genarity, cc, retty, argtys, varargs = readBlobHeapAsMethodSig ctxt numtypars (seekReadStandAloneSigRow ctxt mdv idx)
             if generic then failwith "bad image: a generic method signature is begin used at a calli instruction"
             f prefixes (mkILCallSig (cc, argtys, retty), varargs)
         | I_switch_instr f ->
             let n = (seekReadInt32 pev (start + (!curr)))
             curr := !curr + 4
             let offsets =
               List.init n (fun _ ->
                   let i = (seekReadInt32 pev (start + (!curr)))
                   curr := !curr + 4
                   i)
             let dests = List.map (fun offs -> rawToLabel (!curr + offs)) offsets
             f prefixes dests
       ibuf.Add instr
   done
   // Finished reading instructions - mark the end of the instruction stream in case the PDB information refers to it.
   markAsInstructionStart !curr ibuf.Count
   // Build the function that maps from raw labels (offsets into the bytecode stream) to indexes in the AbsIL instruction stream
   let lab2pc = ilOffsetsOfLabels

   // Some offsets used in debug info refer to the end of an instruction, rather than the
   // start of the subsequent instruction. But all labels refer to instruction starts,
   // apart from a final label which refers to the end of the method. This function finds
   // the start of the next instruction referred to by the raw offset.
   let raw2nextLab rawOffset =
       let isInstrStart x =
           match labelsOfRawOffsets.TryGetValue x with
           | true, lab -> ilOffsetsOfLabels.ContainsKey lab
           | _ -> false
       if isInstrStart rawOffset then rawToLabel rawOffset
       elif isInstrStart (rawOffset+1) then rawToLabel (rawOffset+1)
       else failwith ("the bytecode raw offset "+string rawOffset+" did not refer either to the start or end of an instruction")
   let instrs = ibuf.ToArray()
   instrs, rawToLabel, lab2pc, raw2nextLab

#if FX_NO_PDB_READER
and seekReadMethodRVA (pectxt: PEReader) (ctxt: ILMetadataReader) (_idx, nm, _internalcall, noinline, aggressiveinline, numtypars) rva =
#else
and seekReadMethodRVA (pectxt: PEReader) (ctxt: ILMetadataReader) (idx, nm, _internalcall, noinline, aggressiveinline, numtypars) rva =
#endif
 lazy
  let pev = pectxt.pefile.GetView()
  let baseRVA = pectxt.anyV2P("method rva", rva)
  // ": reading body of method "+nm+" at rva "+string rva+", phys "+string baseRVA
  let b = seekReadByte pev baseRVA

  let isTinyFormat = (b &&& e_CorILMethod_FormatMask) = e_CorILMethod_TinyFormat
  let isFatFormat = (b &&& e_CorILMethod_FormatMask) = e_CorILMethod_FatFormat

  if not isTinyFormat && not isFatFormat then
    if logging then failwith "unknown format"
    MethodBody.Abstract
  else

  MethodBody.IL
   (lazy
       let pev = pectxt.pefile.GetView()
       let mdv = ctxt.mdfile.GetView()

       // Read any debug information for this method into temporary data structures
       //    -- a list of locals, marked with the raw offsets (actually closures which accept the resolution function that maps raw offsets to labels)
       //    -- an overall range for the method
       //    -- the sequence points for the method
       let localPdbInfos, methRangePdbInfo, seqpoints =
#if FX_NO_PDB_READER
           [], None, []
#else
           match pectxt.pdb with
           | None ->
               [], None, []
           | Some (pdbr, get_doc) ->
               try

                 let pdbm = pdbReaderGetMethod pdbr (uncodedToken TableNames.Method idx)
                 let sps = pdbMethodGetSequencePoints pdbm
                 (* let roota, rootb = pdbScopeGetOffsets rootScope in *)
                 let seqpoints =
                    let arr =
                       sps |> Array.map (fun sp ->
                           // It is VERY annoying to have to call GetURL for the document for
                           // each sequence point. This appears to be a short coming of the PDB
                           // reader API. They should return an index into the array of documents for the reader
                           let sourcedoc = get_doc (pdbDocumentGetURL sp.pdbSeqPointDocument)
                           let source =
                             ILSourceMarker.Create(document = sourcedoc,
                                                 line = sp.pdbSeqPointLine,
                                                 column = sp.pdbSeqPointColumn,
                                                 endLine = sp.pdbSeqPointEndLine,
                                                 endColumn = sp.pdbSeqPointEndColumn)
                           (sp.pdbSeqPointOffset, source))

                    Array.sortInPlaceBy fst arr

                    Array.toList arr
                 let rec scopes scp =
                       let a, b = pdbScopeGetOffsets scp
                       let lvs = pdbScopeGetLocals scp
                       let ilvs =
                         lvs
                         |> Array.toList
                         |> List.filter (fun l ->
                             let k, _idx = pdbVariableGetAddressAttributes l
                             k = 1 (* ADDR_IL_OFFSET *))
                       let ilinfos: ILLocalDebugMapping list =
                         ilvs |> List.map (fun ilv ->
                             let _k, idx = pdbVariableGetAddressAttributes ilv
                             let n = pdbVariableGetName ilv
                             { LocalIndex= idx
                               LocalName=n})

                       let thisOne =
                         (fun raw2nextLab ->
                           { Range= (raw2nextLab a, raw2nextLab b)
                             DebugMappings = ilinfos }: ILLocalDebugInfo )
                       let others = List.foldBack (scopes >> (@)) (Array.toList (pdbScopeGetChildren scp)) []
                       thisOne :: others
                 let localPdbInfos = [] (* <REVIEW> scopes fail for mscorlib </REVIEW> scopes rootScope *)
                 // REVIEW: look through sps to get ranges? Use GetRanges?? Change AbsIL??
                 (localPdbInfos, None, seqpoints)
               with e ->
                   // "* Warning: PDB info for method "+nm+" could not be read and will be ignored: "+e.Message
                   [], None, []
#endif

       if isTinyFormat then
           let codeBase = baseRVA + 1
           let codeSize = (int32 b >>>& 2)
           // tiny format for "+nm+", code size = " + string codeSize)
           let instrs, _, lab2pc, raw2nextLab = seekReadTopCode ctxt pev mdv numtypars codeSize codeBase seqpoints
           (* Convert the linear code format to the nested code format *)
           let localPdbInfos2 = List.map (fun f -> f raw2nextLab) localPdbInfos
           let code = buildILCode nm lab2pc instrs [] localPdbInfos2
           { IsZeroInit=false
             MaxStack= 8
             NoInlining=noinline
             AggressiveInlining=aggressiveinline
             Locals=List.empty
             SourceMarker=methRangePdbInfo
             Code=code }

       else
           let hasMoreSections = (b &&& e_CorILMethod_MoreSects) <> 0x0uy
           let initlocals = (b &&& e_CorILMethod_InitLocals) <> 0x0uy
           let maxstack = seekReadUInt16AsInt32 pev (baseRVA + 2)
           let codeSize = seekReadInt32 pev (baseRVA + 4)
           let localsTab, localToken = seekReadUncodedToken pev (baseRVA + 8)
           let codeBase = baseRVA + 12
           let locals =
             if localToken = 0x0 then []
             else
               if localsTab <> TableNames.StandAloneSig then dprintn "strange table for locals token"
               readBlobHeapAsLocalsSig ctxt numtypars (seekReadStandAloneSigRow ctxt pev localToken)

           // fat format for "+nm+", code size = " + string codeSize+", hasMoreSections = "+(if hasMoreSections then "true" else "false")+", b = "+string b)

           // Read the method body
           let instrs, rawToLabel, lab2pc, raw2nextLab = seekReadTopCode ctxt pev mdv numtypars ( codeSize) codeBase seqpoints

           // Read all the sections that follow the method body.
           // These contain the exception clauses.
           let nextSectionBase = ref (align 4 (codeBase + codeSize))
           let moreSections = ref hasMoreSections
           let seh = ref []
           while !moreSections do
             let sectionBase = !nextSectionBase
             let sectionFlag = seekReadByte pev sectionBase
             // fat format for "+nm+", sectionFlag = " + string sectionFlag)
             let sectionSize, clauses =
               if (sectionFlag &&& e_CorILMethod_Sect_FatFormat) <> 0x0uy then
                   let bigSize = (seekReadInt32 pev sectionBase) >>>& 8
                   // bigSize = "+string bigSize)
                   let clauses =
                       if (sectionFlag &&& e_CorILMethod_Sect_EHTable) <> 0x0uy then
                           // WORKAROUND: The ECMA spec says this should be
                           // let numClauses = ((bigSize - 4)  / 24) in
                           // but the CCI IL generator generates multiples of 24
                           let numClauses = (bigSize  / 24)

                           List.init numClauses (fun i ->
                               let clauseBase = sectionBase + 4 + (i * 24)
                               let kind = seekReadInt32 pev (clauseBase + 0)
                               let st1 = seekReadInt32 pev (clauseBase + 4)
                               let sz1 = seekReadInt32 pev (clauseBase + 8)
                               let st2 = seekReadInt32 pev (clauseBase + 12)
                               let sz2 = seekReadInt32 pev (clauseBase + 16)
                               let extra = seekReadInt32 pev (clauseBase + 20)
                               (kind, st1, sz1, st2, sz2, extra))
                       else []
                   bigSize, clauses
               else
                 let smallSize = seekReadByteAsInt32 pev (sectionBase + 0x01)
                 let clauses =
                   if (sectionFlag &&& e_CorILMethod_Sect_EHTable) <> 0x0uy then
                       // WORKAROUND: The ECMA spec says this should be
                       // let numClauses = ((smallSize - 4)  / 12) in
                       // but the C# compiler (or some IL generator) generates multiples of 12
                       let numClauses = (smallSize  / 12)
                       // dprintn (nm+" has " + string numClauses + " tiny seh clauses")
                       List.init numClauses (fun i ->
                           let clauseBase = sectionBase + 4 + (i * 12)
                           let kind = seekReadUInt16AsInt32 pev (clauseBase + 0)
                           if logging then dprintn ("One tiny SEH clause, kind = "+string kind)
                           let st1 = seekReadUInt16AsInt32 pev (clauseBase + 2)
                           let sz1 = seekReadByteAsInt32 pev (clauseBase + 4)
                           let st2 = seekReadUInt16AsInt32 pev (clauseBase + 5)
                           let sz2 = seekReadByteAsInt32 pev (clauseBase + 7)
                           let extra = seekReadInt32 pev (clauseBase + 8)
                           (kind, st1, sz1, st2, sz2, extra))
                   else
                       []
                 smallSize, clauses

             // Morph together clauses that cover the same range
             let sehClauses =
                let sehMap = Dictionary<_, _>(clauses.Length, HashIdentity.Structural)

                List.iter
                  (fun (kind, st1, sz1, st2, sz2, extra) ->
                    let tryStart = rawToLabel st1
                    let tryFinish = rawToLabel (st1 + sz1)
                    let handlerStart = rawToLabel st2
                    let handlerFinish = rawToLabel (st2 + sz2)
                    let clause =
                      if kind = e_COR_ILEXCEPTION_CLAUSE_EXCEPTION then
                        ILExceptionClause.TypeCatch(seekReadTypeDefOrRef ctxt numtypars AsObject List.empty (uncodedTokenToTypeDefOrRefOrSpec (i32ToUncodedToken extra)), (handlerStart, handlerFinish) )
                      elif kind = e_COR_ILEXCEPTION_CLAUSE_FILTER then
                        let filterStart = rawToLabel extra
                        let filterFinish = handlerStart
                        ILExceptionClause.FilterCatch((filterStart, filterFinish), (handlerStart, handlerFinish))
                      elif kind = e_COR_ILEXCEPTION_CLAUSE_FINALLY then
                        ILExceptionClause.Finally(handlerStart, handlerFinish)
                      elif kind = e_COR_ILEXCEPTION_CLAUSE_FAULT then
                        ILExceptionClause.Fault(handlerStart, handlerFinish)
                      else begin
                        dprintn (ctxt.fileName + ": unknown exception handler kind: "+string kind)
                        ILExceptionClause.Finally(handlerStart, handlerFinish)
                      end

                    let key = (tryStart, tryFinish)
                    match sehMap.TryGetValue key with
                    | true, prev -> sehMap.[key] <- prev @ [clause]
                    | _ -> sehMap.[key] <- [clause])
                  clauses
                ([], sehMap) ||> Seq.fold (fun acc (KeyValue(key, bs)) -> [ for b in bs -> {Range=key; Clause=b}: ILExceptionSpec ] @ acc)
             seh := sehClauses
             moreSections := (sectionFlag &&& e_CorILMethod_Sect_MoreSects) <> 0x0uy
             nextSectionBase := sectionBase + sectionSize
           done (* while *)

           (* Convert the linear code format to the nested code format *)
           if logging then dprintn ("doing localPdbInfos2")
           let localPdbInfos2 = List.map (fun f -> f raw2nextLab) localPdbInfos
           if logging then dprintn ("done localPdbInfos2, checking code...")
           let code = buildILCode nm lab2pc instrs !seh localPdbInfos2
           if logging then dprintn ("done checking code.")
           { IsZeroInit=initlocals
             MaxStack= maxstack
             NoInlining=noinline
             AggressiveInlining=aggressiveinline
             Locals = locals
             Code=code
             SourceMarker=methRangePdbInfo})

and int32AsILVariantType (ctxt: ILMetadataReader) (n: int32) =
    if List.memAssoc n (Lazy.force ILVariantTypeRevMap) then
      List.assoc n (Lazy.force ILVariantTypeRevMap)
    elif (n &&& vt_ARRAY) <> 0x0 then ILNativeVariant.Array (int32AsILVariantType ctxt (n &&& (~~~ vt_ARRAY)))
    elif (n &&& vt_VECTOR) <> 0x0 then ILNativeVariant.Vector (int32AsILVariantType ctxt (n &&& (~~~ vt_VECTOR)))
    elif (n &&& vt_BYREF) <> 0x0 then ILNativeVariant.Byref (int32AsILVariantType ctxt (n &&& (~~~ vt_BYREF)))
    else (dprintn (ctxt.fileName + ": int32AsILVariantType ctxt: unexpected variant type, n = "+string n) ; ILNativeVariant.Empty)

and readBlobHeapAsNativeType ctxt blobIdx =
    // reading native type blob "+string blobIdx)
    let bytes = readBlobHeap ctxt blobIdx
    let res, _ = sigptrGetILNativeType ctxt bytes 0
    res

and sigptrGetILNativeType ctxt bytes sigptr =
    // reading native type blob, sigptr= "+string sigptr)
    let ntbyte, sigptr = sigptrGetByte bytes sigptr
    if List.memAssoc ntbyte (Lazy.force ILNativeTypeMap) then
        List.assoc ntbyte (Lazy.force ILNativeTypeMap), sigptr
    elif ntbyte = 0x0uy then ILNativeType.Empty, sigptr
    elif ntbyte = nt_CUSTOMMARSHALER then
        // reading native type blob CM1, sigptr= "+string sigptr+ ", bytes.Length = "+string bytes.Length)
        let struct (guidLen, sigptr) = sigptrGetZInt32 bytes sigptr
        // reading native type blob CM2, sigptr= "+string sigptr+", guidLen = "+string ( guidLen))
        let guid, sigptr = sigptrGetBytes ( guidLen) bytes sigptr
        // reading native type blob CM3, sigptr= "+string sigptr)
        let struct (nativeTypeNameLen, sigptr) = sigptrGetZInt32 bytes sigptr
        // reading native type blob CM4, sigptr= "+string sigptr+", nativeTypeNameLen = "+string ( nativeTypeNameLen))
        let nativeTypeName, sigptr = sigptrGetString ( nativeTypeNameLen) bytes sigptr
        // reading native type blob CM4, sigptr= "+string sigptr+", nativeTypeName = "+nativeTypeName)
        // reading native type blob CM5, sigptr= "+string sigptr)
        let struct (custMarshallerNameLen, sigptr) = sigptrGetZInt32 bytes sigptr
        // reading native type blob CM6, sigptr= "+string sigptr+", custMarshallerNameLen = "+string ( custMarshallerNameLen))
        let custMarshallerName, sigptr = sigptrGetString ( custMarshallerNameLen) bytes sigptr
        // reading native type blob CM7, sigptr= "+string sigptr+", custMarshallerName = "+custMarshallerName)
        let struct (cookieStringLen, sigptr) = sigptrGetZInt32 bytes sigptr
        // reading native type blob CM8, sigptr= "+string sigptr+", cookieStringLen = "+string ( cookieStringLen))
        let cookieString, sigptr = sigptrGetBytes ( cookieStringLen) bytes sigptr
        // reading native type blob CM9, sigptr= "+string sigptr)
        ILNativeType.Custom (guid, nativeTypeName, custMarshallerName, cookieString), sigptr
    elif ntbyte = nt_FIXEDSYSSTRING then
      let struct (i, sigptr) = sigptrGetZInt32 bytes sigptr
      ILNativeType.FixedSysString i, sigptr
    elif ntbyte = nt_FIXEDARRAY then
      let struct (i, sigptr) = sigptrGetZInt32 bytes sigptr
      ILNativeType.FixedArray i, sigptr
    elif ntbyte = nt_SAFEARRAY then
      (if sigptr >= bytes.Length then
         ILNativeType.SafeArray(ILNativeVariant.Empty, None), sigptr
       else
         let struct (i, sigptr) = sigptrGetZInt32 bytes sigptr
         if sigptr >= bytes.Length then
           ILNativeType.SafeArray (int32AsILVariantType ctxt i, None), sigptr
         else
           let struct (len, sigptr) = sigptrGetZInt32 bytes sigptr
           let s, sigptr = sigptrGetString ( len) bytes sigptr
           ILNativeType.SafeArray (int32AsILVariantType ctxt i, Some s), sigptr)
    elif ntbyte = nt_ARRAY then
       if sigptr >= bytes.Length then
         ILNativeType.Array(None, None), sigptr
       else
         let nt, sigptr =
           let struct (u, sigptr') = sigptrGetZInt32 bytes sigptr
           if (u = int nt_MAX) then
             ILNativeType.Empty, sigptr'
           else
             // NOTE: go back to start and read native type
             sigptrGetILNativeType ctxt bytes sigptr
         if sigptr >= bytes.Length then
           ILNativeType.Array (Some nt, None), sigptr
         else
           let struct (pnum, sigptr) = sigptrGetZInt32 bytes sigptr
           if sigptr >= bytes.Length then
             ILNativeType.Array (Some nt, Some(pnum, None)), sigptr
           else
             let struct (additive, sigptr) =
               if sigptr >= bytes.Length then 0, sigptr
               else sigptrGetZInt32 bytes sigptr
             ILNativeType.Array (Some nt, Some(pnum, Some additive)), sigptr
    else (ILNativeType.Empty, sigptr)

// Note, pectxtEager and pevEager must not be captured by the results of this function
// As a result, reading the resource offsets in the physical file is done eagerly to avoid holding on to any resources
and seekReadManifestResources (ctxt: ILMetadataReader) canReduceMemory (mdv: BinaryView) (pectxtEager: PEReader) (pevEager: BinaryView) =
    mkILResources
        [ for i = 1 to ctxt.getNumRows TableNames.ManifestResource do
             let (offset, flags, nameIdx, implIdx) = seekReadManifestResourceRow ctxt mdv i

             let scoref = seekReadImplAsScopeRef ctxt mdv implIdx

             let location =
                match scoref with
                | ILScopeRef.Local ->
                    let start = pectxtEager.anyV2P ("resource", offset + pectxtEager.resourcesAddr)
                    let resourceLength = seekReadInt32 pevEager start
                    let offsetOfBytesFromStartOfPhysicalPEFile = start + 4
                    let byteStorage =
                        let bytes = pevEager.Slice(offsetOfBytesFromStartOfPhysicalPEFile, resourceLength)
                        ByteStorage.FromByteMemoryAndCopy(bytes, useBackingMemoryMappedFile = canReduceMemory)
                    ILResourceLocation.Local(byteStorage)

                | ILScopeRef.Module mref -> ILResourceLocation.File (mref, offset)
                | ILScopeRef.Assembly aref -> ILResourceLocation.Assembly aref
                | _ -> failwith "seekReadManifestResources: Invalid ILScopeRef"

             let r =
               { Name= readStringHeap ctxt nameIdx
                 Location = location
                 Access = (if (flags &&& 0x01) <> 0x0 then ILResourceAccess.Public else ILResourceAccess.Private)
                 CustomAttrsStored = ctxt.customAttrsReader_ManifestResource
                 MetadataIndex = i }
             yield r ]

and seekReadNestedExportedTypes ctxt (exported: _ []) (nested: Lazy<_ []>) parentIdx =
    mkILNestedExportedTypesLazy
      (lazy
            nested.Force().[parentIdx-1]
            |> List.map (fun i ->
                let (flags, _tok, nameIdx, namespaceIdx, _implIdx) = exported.[i-1]
                { Name = readBlobHeapAsTypeName ctxt (nameIdx, namespaceIdx)
                  Access = (match typeAccessOfFlags flags with
                            | ILTypeDefAccess.Nested n -> n
                            | _ -> failwith "non-nested access for a nested type described as being in an auxiliary module")
                  Nested = seekReadNestedExportedTypes ctxt exported nested i
                  CustomAttrsStored = ctxt.customAttrsReader_ExportedType
                  MetadataIndex = i }
            ))

and seekReadTopExportedTypes (ctxt: ILMetadataReader) =
    mkILExportedTypesLazy
      (lazy
            let mdv = ctxt.mdfile.GetView()
            let numRows = ctxt.getNumRows TableNames.ExportedType
            let exported = [| for i in 1..numRows -> seekReadExportedTypeRow ctxt mdv i |]

            // add each nested type id to their parent's children list
            let nested = lazy (
                let nested = [| for _i in 1..numRows -> [] |]
                for i = 1 to numRows do
                    let (flags,_,_,_,TaggedIndex(tag, idx)) = exported.[i-1]
                    if not (isTopTypeDef flags) && (tag = i_ExportedType) then
                        nested.[idx-1] <- i :: nested.[idx-1]
                nested)

            // return top exported types
            [ for i = 1 to numRows do
                let (flags, _tok, nameIdx, namespaceIdx, implIdx) = exported.[i-1]
                let (TaggedIndex(tag, _idx)) = implIdx

                // if not a nested type
                if (isTopTypeDef flags) && (tag <> i_ExportedType) then
                    yield
                      { ScopeRef = seekReadImplAsScopeRef ctxt mdv implIdx
                        Name = readBlobHeapAsTypeName ctxt (nameIdx, namespaceIdx)
                        Attributes = enum<TypeAttributes>(flags)
                        Nested = seekReadNestedExportedTypes ctxt exported nested i
                        CustomAttrsStored = ctxt.customAttrsReader_ExportedType
                        MetadataIndex = i }
            ])

#if !FX_NO_PDB_READER
let getPdbReader pdbDirPath fileName =
    match pdbDirPath with
    | None -> None
    | Some pdbpath ->
         try
              let pdbr = pdbReadOpen fileName pdbpath
              let pdbdocs = pdbReaderGetDocuments pdbr

              let tab = new Dictionary<_, _>(Array.length pdbdocs)
              pdbdocs |> Array.iter (fun pdbdoc ->
                  let url = pdbDocumentGetURL pdbdoc
                  tab.[url] <-
                      ILSourceDocument.Create(language=Some (pdbDocumentGetLanguage pdbdoc),
                                            vendor = Some (pdbDocumentGetLanguageVendor pdbdoc),
                                            documentType = Some (pdbDocumentGetType pdbdoc),
                                            file = url))

              let docfun url =
                  match tab.TryGetValue url with
                  | true, doc -> doc
                  | _ -> failwith ("Document with URL " + url + " not found in list of documents in the PDB file")
              Some (pdbr, docfun)
          with e -> dprintn ("* Warning: PDB file could not be read and will be ignored: "+e.Message); None
#endif

// Note, pectxtEager and pevEager must not be captured by the results of this function
let openMetadataReader (fileName, mdfile: BinaryFile, metadataPhysLoc, peinfo, pectxtEager: PEReader, pevEager, pectxtCaptured, reduceMemoryUsage) =
    let mdv = mdfile.GetView()
    let magic = seekReadUInt16AsInt32 mdv metadataPhysLoc
    if magic <> 0x5342 then failwith (fileName + ": bad metadata magic number: " + string magic)
    let magic2 = seekReadUInt16AsInt32 mdv (metadataPhysLoc + 2)
    if magic2 <> 0x424a then failwith "bad metadata magic number"
    let _majorMetadataVersion = seekReadUInt16 mdv (metadataPhysLoc + 4)
    let _minorMetadataVersion = seekReadUInt16 mdv (metadataPhysLoc + 6)

    let versionLength = seekReadInt32 mdv (metadataPhysLoc + 12)
    let ilMetadataVersion = seekReadBytes mdv (metadataPhysLoc + 16) versionLength |> Array.filter (fun b -> b <> 0uy)
    let x = align 0x04 (16 + versionLength)
    let numStreams = seekReadUInt16AsInt32 mdv (metadataPhysLoc + x + 2)
    let streamHeadersStart = (metadataPhysLoc + x + 4)

    let tryFindStream name =
      let rec look i pos =
        if i >= numStreams then None
        else
          let offset = seekReadInt32 mdv (pos + 0)
          let length = seekReadInt32 mdv (pos + 4)
          let res = ref true
          let fin = ref false
          let n = ref 0
          // read and compare the stream name byte by byte
          while (not !fin) do
              let c= seekReadByteAsInt32 mdv (pos + 8 + (!n))
              if c = 0 then
                  fin := true
              elif !n >= Array.length name || c <> name.[!n] then
                  res := false
              incr n
          if !res then Some(offset + metadataPhysLoc, length)
          else look (i+1) (align 0x04 (pos + 8 + (!n)))
      look 0 streamHeadersStart

    let findStream name =
        match tryFindStream name with
        | None -> (0x0, 0x0)
        | Some positions -> positions

    let (tablesStreamPhysLoc, _tablesStreamSize) =
      match tryFindStream [| 0x23; 0x7e |] (* #~ *) with
      | Some res -> res
      | None ->
        match tryFindStream [| 0x23; 0x2d |] (* #-: at least one DLL I've seen uses this! *) with
        | Some res -> res
        | None ->
         let firstStreamOffset = seekReadInt32 mdv (streamHeadersStart + 0)
         let firstStreamLength = seekReadInt32 mdv (streamHeadersStart + 4)
         firstStreamOffset, firstStreamLength

    let (stringsStreamPhysicalLoc, stringsStreamSize) = findStream [| 0x23; 0x53; 0x74; 0x72; 0x69; 0x6e; 0x67; 0x73; |] (* #Strings *)
    let (userStringsStreamPhysicalLoc, userStringsStreamSize) = findStream [| 0x23; 0x55; 0x53; |] (* #US *)
    let (guidsStreamPhysicalLoc, _guidsStreamSize) = findStream [| 0x23; 0x47; 0x55; 0x49; 0x44; |] (* #GUID *)
    let (blobsStreamPhysicalLoc, blobsStreamSize) = findStream [| 0x23; 0x42; 0x6c; 0x6f; 0x62; |] (* #Blob *)

    let tableKinds =
        [|kindModule (* Table 0 *)
          kindTypeRef (* Table 1 *)
          kindTypeDef (* Table 2 *)
          kindIllegal (* kindFieldPtr *) (* Table 3 *)
          kindFieldDef (* Table 4 *)
          kindIllegal (* kindMethodPtr *) (* Table 5 *)
          kindMethodDef (* Table 6 *)
          kindIllegal (* kindParamPtr *) (* Table 7 *)
          kindParam (* Table 8 *)
          kindInterfaceImpl (* Table 9 *)
          kindMemberRef (* Table 10 *)
          kindConstant (* Table 11 *)
          kindCustomAttribute (* Table 12 *)
          kindFieldMarshal (* Table 13 *)
          kindDeclSecurity (* Table 14 *)
          kindClassLayout (* Table 15 *)
          kindFieldLayout (* Table 16 *)
          kindStandAloneSig (* Table 17 *)
          kindEventMap (* Table 18 *)
          kindIllegal (* kindEventPtr *) (* Table 19 *)
          kindEvent (* Table 20 *)
          kindPropertyMap (* Table 21 *)
          kindIllegal (* kindPropertyPtr *) (* Table 22 *)
          kindProperty (* Table 23 *)
          kindMethodSemantics (* Table 24 *)
          kindMethodImpl (* Table 25 *)
          kindModuleRef (* Table 26 *)
          kindTypeSpec (* Table 27 *)
          kindImplMap (* Table 28 *)
          kindFieldRVA (* Table 29 *)
          kindIllegal (* kindENCLog *) (* Table 30 *)
          kindIllegal (* kindENCMap *) (* Table 31 *)
          kindAssembly (* Table 32 *)
          kindIllegal (* kindAssemblyProcessor *) (* Table 33 *)
          kindIllegal (* kindAssemblyOS *) (* Table 34 *)
          kindAssemblyRef (* Table 35 *)
          kindIllegal (* kindAssemblyRefProcessor *) (* Table 36 *)
          kindIllegal (* kindAssemblyRefOS *) (* Table 37 *)
          kindFileRef (* Table 38 *)
          kindExportedType (* Table 39 *)
          kindManifestResource (* Table 40 *)
          kindNested (* Table 41 *)
          kindGenericParam_v2_0 (* Table 42 *)
          kindMethodSpec (* Table 43 *)
          kindGenericParamConstraint (* Table 44 *)
          kindIllegal (* Table 45 *)
          kindIllegal (* Table 46 *)
          kindIllegal (* Table 47 *)
          kindIllegal (* Table 48 *)
          kindIllegal (* Table 49 *)
          kindIllegal (* Table 50 *)
          kindIllegal (* Table 51 *)
          kindIllegal (* Table 52 *)
          kindIllegal (* Table 53 *)
          kindIllegal (* Table 54 *)
          kindIllegal (* Table 55 *)
          kindIllegal (* Table 56 *)
          kindIllegal (* Table 57 *)
          kindIllegal (* Table 58 *)
          kindIllegal (* Table 59 *)
          kindIllegal (* Table 60 *)
          kindIllegal (* Table 61 *)
          kindIllegal (* Table 62 *)
          kindIllegal (* Table 63 *)
        |]

    let heapSizes = seekReadByteAsInt32 mdv (tablesStreamPhysLoc + 6)
    let valid = seekReadInt64 mdv (tablesStreamPhysLoc + 8)
    let sorted = seekReadInt64 mdv (tablesStreamPhysLoc + 16)
    let tablesPresent, tableRowCount, startOfTables =
        let present = ref []
        let numRows = Array.create 64 0
        let prevNumRowIdx = ref (tablesStreamPhysLoc + 24)
        for i = 0 to 63 do
            if (valid &&& (int64 1 <<< i)) <> int64 0 then
                present := i :: !present
                numRows.[i] <- (seekReadInt32 mdv !prevNumRowIdx)
                prevNumRowIdx := !prevNumRowIdx + 4
        List.rev !present, numRows, !prevNumRowIdx

    let getNumRows (tab: TableName) = tableRowCount.[tab.Index]
    let numTables = tablesPresent.Length
    let stringsBigness = (heapSizes &&& 1) <> 0
    let guidsBigness = (heapSizes &&& 2) <> 0
    let blobsBigness = (heapSizes &&& 4) <> 0

    if logging then dprintn (fileName + ": numTables = "+string numTables)
    if logging && stringsBigness then dprintn (fileName + ": strings are big")
    if logging && blobsBigness then dprintn (fileName + ": blobs are big")

    let tableBigness = Array.map (fun n -> n >= 0x10000) tableRowCount

    let codedBigness nbits tab =
      let rows = getNumRows tab
      rows >= (0x10000 >>>& nbits)

    let tdorBigness =
      codedBigness 2 TableNames.TypeDef ||
      codedBigness 2 TableNames.TypeRef ||
      codedBigness 2 TableNames.TypeSpec

    let tomdBigness =
      codedBigness 1 TableNames.TypeDef ||
      codedBigness 1 TableNames.Method

    let hcBigness =
      codedBigness 2 TableNames.Field ||
      codedBigness 2 TableNames.Param ||
      codedBigness 2 TableNames.Property

    let hcaBigness =
      codedBigness 5 TableNames.Method ||
      codedBigness 5 TableNames.Field ||
      codedBigness 5 TableNames.TypeRef ||
      codedBigness 5 TableNames.TypeDef ||
      codedBigness 5 TableNames.Param ||
      codedBigness 5 TableNames.InterfaceImpl ||
      codedBigness 5 TableNames.MemberRef ||
      codedBigness 5 TableNames.Module ||
      codedBigness 5 TableNames.Permission ||
      codedBigness 5 TableNames.Property ||
      codedBigness 5 TableNames.Event ||
      codedBigness 5 TableNames.StandAloneSig ||
      codedBigness 5 TableNames.ModuleRef ||
      codedBigness 5 TableNames.TypeSpec ||
      codedBigness 5 TableNames.Assembly ||
      codedBigness 5 TableNames.AssemblyRef ||
      codedBigness 5 TableNames.File ||
      codedBigness 5 TableNames.ExportedType ||
      codedBigness 5 TableNames.ManifestResource ||
      codedBigness 5 TableNames.GenericParam ||
      codedBigness 5 TableNames.GenericParamConstraint ||
      codedBigness 5 TableNames.MethodSpec


    let hfmBigness =
      codedBigness 1 TableNames.Field ||
      codedBigness 1 TableNames.Param

    let hdsBigness =
      codedBigness 2 TableNames.TypeDef ||
      codedBigness 2 TableNames.Method ||
      codedBigness 2 TableNames.Assembly

    let mrpBigness =
      codedBigness 3 TableNames.TypeDef ||
      codedBigness 3 TableNames.TypeRef ||
      codedBigness 3 TableNames.ModuleRef ||
      codedBigness 3 TableNames.Method ||
      codedBigness 3 TableNames.TypeSpec

    let hsBigness =
      codedBigness 1 TableNames.Event ||
      codedBigness 1 TableNames.Property

    let mdorBigness =
      codedBigness 1 TableNames.Method ||
      codedBigness 1 TableNames.MemberRef

    let mfBigness =
      codedBigness 1 TableNames.Field ||
      codedBigness 1 TableNames.Method

    let iBigness =
      codedBigness 2 TableNames.File ||
      codedBigness 2 TableNames.AssemblyRef ||
      codedBigness 2 TableNames.ExportedType

    let catBigness =
      codedBigness 3 TableNames.Method ||
      codedBigness 3 TableNames.MemberRef

    let rsBigness =
      codedBigness 2 TableNames.Module ||
      codedBigness 2 TableNames.ModuleRef ||
      codedBigness 2 TableNames.AssemblyRef ||
      codedBigness 2 TableNames.TypeRef

    let rowKindSize (RowKind kinds) =
      kinds |> List.sumBy (fun x ->
            match x with
            | UShort -> 2
            | ULong -> 4
            | Byte -> 1
            | Data -> 4
            | GGuid -> (if guidsBigness then 4 else 2)
            | Blob -> (if blobsBigness then 4 else 2)
            | SString -> (if stringsBigness then 4 else 2)
            | SimpleIndex tab -> (if tableBigness.[tab.Index] then 4 else 2)
            | TypeDefOrRefOrSpec -> (if tdorBigness then 4 else 2)
            | TypeOrMethodDef -> (if tomdBigness then 4 else 2)
            | HasConstant -> (if hcBigness then 4 else 2)
            | HasCustomAttribute -> (if hcaBigness then 4 else 2)
            | HasFieldMarshal -> (if hfmBigness then 4 else 2)
            | HasDeclSecurity -> (if hdsBigness then 4 else 2)
            | MemberRefParent -> (if mrpBigness then 4 else 2)
            | HasSemantics -> (if hsBigness then 4 else 2)
            | MethodDefOrRef -> (if mdorBigness then 4 else 2)
            | MemberForwarded -> (if mfBigness then 4 else 2)
            | Implementation -> (if iBigness then 4 else 2)
            | CustomAttributeType -> (if catBigness then 4 else 2)
            | ResolutionScope -> (if rsBigness then 4 else 2))

    let tableRowSizes = tableKinds |> Array.map rowKindSize

    let tablePhysLocations =
         let res = Array.create 64 0x0
         let mutable prevTablePhysLoc = startOfTables
         for i = 0 to 63 do
             res.[i] <- prevTablePhysLoc
             prevTablePhysLoc <- prevTablePhysLoc + (tableRowCount.[i] * tableRowSizes.[i])
         res

    let inbase = FileSystemUtils.fileNameOfPath fileName + ": "

    // All the caches. The sizes are guesstimates for the rough sharing-density of the assembly
    let cacheAssemblyRef = mkCacheInt32 false inbase "ILAssemblyRef" (getNumRows TableNames.AssemblyRef)
    let cacheMethodSpecAsMethodData = mkCacheGeneric reduceMemoryUsage inbase "MethodSpecAsMethodData" (getNumRows TableNames.MethodSpec / 20 + 1)
    let cacheMemberRefAsMemberData = mkCacheGeneric reduceMemoryUsage inbase "MemberRefAsMemberData" (getNumRows TableNames.MemberRef / 20 + 1)
    let cacheCustomAttr = mkCacheGeneric reduceMemoryUsage inbase "CustomAttr" (getNumRows TableNames.CustomAttribute / 50 + 1)
    let cacheTypeRef = mkCacheInt32 false inbase "ILTypeRef" (getNumRows TableNames.TypeRef / 20 + 1)
    let cacheTypeRefAsType = mkCacheGeneric reduceMemoryUsage inbase "TypeRefAsType" (getNumRows TableNames.TypeRef / 20 + 1)
    let cacheBlobHeapAsPropertySig = mkCacheGeneric reduceMemoryUsage inbase "BlobHeapAsPropertySig" (getNumRows TableNames.Property / 20 + 1)
    let cacheBlobHeapAsFieldSig = mkCacheGeneric reduceMemoryUsage inbase "BlobHeapAsFieldSig" (getNumRows TableNames.Field / 20 + 1)
    let cacheBlobHeapAsMethodSig = mkCacheGeneric reduceMemoryUsage inbase "BlobHeapAsMethodSig" (getNumRows TableNames.Method / 20 + 1)
    let cacheTypeDefAsType = mkCacheGeneric reduceMemoryUsage inbase "TypeDefAsType" (getNumRows TableNames.TypeDef / 20 + 1)
    let cacheMethodDefAsMethodData = mkCacheInt32 reduceMemoryUsage inbase "MethodDefAsMethodData" (getNumRows TableNames.Method / 20 + 1)
    let cacheGenericParams = mkCacheGeneric reduceMemoryUsage inbase "GenericParams" (getNumRows TableNames.GenericParam / 20 + 1)
    let cacheFieldDefAsFieldSpec = mkCacheInt32 reduceMemoryUsage inbase "FieldDefAsFieldSpec" (getNumRows TableNames.Field / 20 + 1)
    let cacheUserStringHeap = mkCacheInt32 reduceMemoryUsage inbase "UserStringHeap" ( userStringsStreamSize / 20 + 1)
    // nb. Lots and lots of cache hits on this cache, hence never optimize cache away
    let cacheStringHeap = mkCacheInt32 false inbase "string heap" ( stringsStreamSize / 50 + 1)
    let cacheBlobHeap = mkCacheInt32 reduceMemoryUsage inbase "blob heap" ( blobsStreamSize / 50 + 1)

     // These tables are not required to enforce sharing fo the final data
     // structure, but are very useful as searching these tables gives rise to many reads
     // in standard applications.

    let cacheNestedRow = mkCacheInt32 reduceMemoryUsage inbase "Nested Table Rows" (getNumRows TableNames.Nested / 20 + 1)
    let cacheConstantRow = mkCacheInt32 reduceMemoryUsage inbase "Constant Rows" (getNumRows TableNames.Constant / 20 + 1)
    let cacheMethodSemanticsRow = mkCacheInt32 reduceMemoryUsage inbase "MethodSemantics Rows" (getNumRows TableNames.MethodSemantics / 20 + 1)
    let cacheTypeDefRow = mkCacheInt32 reduceMemoryUsage inbase "ILTypeDef Rows" (getNumRows TableNames.TypeDef / 20 + 1)

    let rowAddr (tab: TableName) idx = tablePhysLocations.[tab.Index] + (idx - 1) * tableRowSizes.[tab.Index]

    // Build the reader context
    // Use an initialization hole
    let ctxtH = ref None
    let ctxt: ILMetadataReader =
        { sorted=sorted
          getNumRows=getNumRows
          mdfile=mdfile
          dataEndPoints = match pectxtCaptured with None -> notlazy [] | Some pectxt -> getDataEndPointsDelayed pectxt ctxtH
          pectxtCaptured=pectxtCaptured
          entryPointToken=pectxtEager.entryPointToken
          fileName=fileName
          userStringsStreamPhysicalLoc = userStringsStreamPhysicalLoc
          stringsStreamPhysicalLoc = stringsStreamPhysicalLoc
          blobsStreamPhysicalLoc = blobsStreamPhysicalLoc
          blobsStreamSize = blobsStreamSize
          memoizeString = Tables.memoize id
          readUserStringHeap = cacheUserStringHeap (readUserStringHeapUncached ctxtH)
          readStringHeap = cacheStringHeap (readStringHeapUncached ctxtH)
          readBlobHeap = cacheBlobHeap (readBlobHeapUncached ctxtH)
          seekReadNestedRow = cacheNestedRow (seekReadNestedRowUncached ctxtH)
          seekReadConstantRow = cacheConstantRow (seekReadConstantRowUncached ctxtH)
          seekReadMethodSemanticsRow = cacheMethodSemanticsRow (seekReadMethodSemanticsRowUncached ctxtH)
          seekReadTypeDefRow = cacheTypeDefRow (seekReadTypeDefRowUncached ctxtH)
          seekReadAssemblyRef = cacheAssemblyRef (seekReadAssemblyRefUncached ctxtH)
          seekReadMethodSpecAsMethodData = cacheMethodSpecAsMethodData (seekReadMethodSpecAsMethodDataUncached ctxtH)
          seekReadMemberRefAsMethodData = cacheMemberRefAsMemberData (seekReadMemberRefAsMethodDataUncached ctxtH)
          seekReadMemberRefAsFieldSpec = seekReadMemberRefAsFieldSpecUncached ctxtH
          seekReadCustomAttr = cacheCustomAttr (seekReadCustomAttrUncached ctxtH)
          seekReadTypeRef = cacheTypeRef (seekReadTypeRefUncached ctxtH)
          readBlobHeapAsPropertySig = cacheBlobHeapAsPropertySig (readBlobHeapAsPropertySigUncached ctxtH)
          readBlobHeapAsFieldSig = cacheBlobHeapAsFieldSig (readBlobHeapAsFieldSigUncached ctxtH)
          readBlobHeapAsMethodSig = cacheBlobHeapAsMethodSig (readBlobHeapAsMethodSigUncached ctxtH)
          readBlobHeapAsLocalsSig = readBlobHeapAsLocalsSigUncached ctxtH
          seekReadTypeDefAsType = cacheTypeDefAsType (seekReadTypeDefAsTypeUncached ctxtH)
          seekReadTypeRefAsType = cacheTypeRefAsType (seekReadTypeRefAsTypeUncached ctxtH)
          seekReadMethodDefAsMethodData = cacheMethodDefAsMethodData (seekReadMethodDefAsMethodDataUncached ctxtH)
          seekReadGenericParams = cacheGenericParams (seekReadGenericParamsUncached ctxtH)
          seekReadFieldDefAsFieldSpec = cacheFieldDefAsFieldSpec (seekReadFieldDefAsFieldSpecUncached ctxtH)
          customAttrsReader_Module = customAttrsReader ctxtH hca_Module
          customAttrsReader_Assembly = customAttrsReader ctxtH hca_Assembly
          customAttrsReader_TypeDef = customAttrsReader ctxtH hca_TypeDef
          customAttrsReader_GenericParam= customAttrsReader ctxtH hca_GenericParam
          customAttrsReader_FieldDef= customAttrsReader ctxtH hca_FieldDef
          customAttrsReader_MethodDef= customAttrsReader ctxtH hca_MethodDef
          customAttrsReader_ParamDef= customAttrsReader ctxtH hca_ParamDef
          customAttrsReader_Event= customAttrsReader ctxtH hca_Event
          customAttrsReader_Property= customAttrsReader ctxtH hca_Property
          customAttrsReader_ManifestResource= customAttrsReader ctxtH hca_ManifestResource
          customAttrsReader_ExportedType= customAttrsReader ctxtH hca_ExportedType
          securityDeclsReader_TypeDef = securityDeclsReader ctxtH hds_TypeDef
          securityDeclsReader_MethodDef = securityDeclsReader ctxtH hds_MethodDef
          securityDeclsReader_Assembly = securityDeclsReader ctxtH hds_Assembly
          typeDefReader = typeDefReader ctxtH
          guidsStreamPhysicalLoc = guidsStreamPhysicalLoc
          rowAddr=rowAddr
          rsBigness=rsBigness
          tdorBigness=tdorBigness
          tomdBigness=tomdBigness
          hcBigness=hcBigness
          hcaBigness=hcaBigness
          hfmBigness=hfmBigness
          hdsBigness=hdsBigness
          mrpBigness=mrpBigness
          hsBigness=hsBigness
          mdorBigness=mdorBigness
          mfBigness=mfBigness
          iBigness=iBigness
          catBigness=catBigness
          stringsBigness=stringsBigness
          guidsBigness=guidsBigness
          blobsBigness=blobsBigness
          tableBigness=tableBigness }
    ctxtH := Some ctxt

    let ilModule = seekReadModule ctxt reduceMemoryUsage pectxtEager pevEager peinfo (System.Text.Encoding.UTF8.GetString (ilMetadataVersion, 0, ilMetadataVersion.Length)) 1
    let ilAssemblyRefs = lazy [ for i in 1 .. getNumRows TableNames.AssemblyRef do yield seekReadAssemblyRef ctxt i ]

    ilModule, ilAssemblyRefs

//-----------------------------------------------------------------------
// Crack the binary headers, build a reader context and return the lazy
// read of the AbsIL module.
// ----------------------------------------------------------------------

let openPEFileReader (fileName, pefile: BinaryFile, pdbDirPath, noFileOnDisk) =
    let pev = pefile.GetView()
    (* MSDOS HEADER *)
    let peSignaturePhysLoc = seekReadInt32 pev 0x3c

    (* PE HEADER *)
    let peFileHeaderPhysLoc = peSignaturePhysLoc + 0x04
    let peOptionalHeaderPhysLoc = peFileHeaderPhysLoc + 0x14
    let peSignature = seekReadInt32 pev (peSignaturePhysLoc + 0)
    if peSignature <> 0x4550 then failwithf "not a PE file - bad magic PE number 0x%08x, is = %A" peSignature pev


    (* PE SIGNATURE *)
    let machine = seekReadUInt16AsInt32 pev (peFileHeaderPhysLoc + 0)
    let numSections = seekReadUInt16AsInt32 pev (peFileHeaderPhysLoc + 2)
    let optHeaderSize = seekReadUInt16AsInt32 pev (peFileHeaderPhysLoc + 16)
    if optHeaderSize <> 0xe0 &&
       optHeaderSize <> 0xf0 then failwith "not a PE file - bad optional header size"
    let x64adjust = optHeaderSize - 0xe0
    let only64 = (optHeaderSize = 0xf0) (* May want to read in the optional header Magic number and check that as well... *)
    let platform = match machine with | 0x8664 -> Some AMD64 | 0x200 -> Some IA64 | _ -> Some X86
    let sectionHeadersStartPhysLoc = peOptionalHeaderPhysLoc + optHeaderSize

    let flags = seekReadUInt16AsInt32 pev (peFileHeaderPhysLoc + 18)
    let isDll = (flags &&& 0x2000) <> 0x0

   (* OPTIONAL PE HEADER *)
    let _textPhysSize = seekReadInt32 pev (peOptionalHeaderPhysLoc + 4) (* Size of the code (text) section, or the sum of all code sections if there are multiple sections. *)
     (* x86: 000000a0 *)
    let _initdataPhysSize = seekReadInt32 pev (peOptionalHeaderPhysLoc + 8) (* Size of the initialized data section, or the sum of all such sections if there are multiple data sections. *)
    let _uninitdataPhysSize = seekReadInt32 pev (peOptionalHeaderPhysLoc + 12) (* Size of the uninitialized data section, or the sum of all such sections if there are multiple data sections. *)
    let _entrypointAddr = seekReadInt32 pev (peOptionalHeaderPhysLoc + 16) (* RVA of entry point, needs to point to bytes 0xFF 0x25 followed by the RVA+!0x4000000 in a section marked execute/read for EXEs or 0 for DLLs e.g. 0x0000b57e *)
    let _textAddr = seekReadInt32 pev (peOptionalHeaderPhysLoc + 20) (* e.g. 0x0002000 *)
     (* x86: 000000b0 *)
    let dataSegmentAddr = seekReadInt32 pev (peOptionalHeaderPhysLoc + 24) (* e.g. 0x0000c000 *)
    (* REVIEW: For now, we'll use the DWORD at offset 24 for x64. This currently ok since fsc doesn't support true 64-bit image bases,
        but we'll have to fix this up when such support is added. *)
    let imageBaseReal = if only64 then dataSegmentAddr else seekReadInt32 pev (peOptionalHeaderPhysLoc + 28)  // Image Base Always 0x400000 (see Section 23.1).
    let alignVirt = seekReadInt32 pev (peOptionalHeaderPhysLoc + 32)   //  Section Alignment Always 0x2000 (see Section 23.1).
    let alignPhys = seekReadInt32 pev (peOptionalHeaderPhysLoc + 36)  // File Alignment Either 0x200 or 0x1000.
     (* x86: 000000c0 *)
    let _osMajor = seekReadUInt16 pev (peOptionalHeaderPhysLoc + 40)   //  OS Major Always 4 (see Section 23.1).
    let _osMinor = seekReadUInt16 pev (peOptionalHeaderPhysLoc + 42)   // OS Minor Always 0 (see Section 23.1).
    let _userMajor = seekReadUInt16 pev (peOptionalHeaderPhysLoc + 44)   // User Major Always 0 (see Section 23.1).
    let _userMinor = seekReadUInt16 pev (peOptionalHeaderPhysLoc + 46)   // User Minor Always 0 (see Section 23.1).
    let subsysMajor = seekReadUInt16AsInt32 pev (peOptionalHeaderPhysLoc + 48)   // SubSys Major Always 4 (see Section 23.1).
    let subsysMinor = seekReadUInt16AsInt32 pev (peOptionalHeaderPhysLoc + 50)   // SubSys Minor Always 0 (see Section 23.1).
     (* x86: 000000d0 *)
    let _imageEndAddr = seekReadInt32 pev (peOptionalHeaderPhysLoc + 56)  // Image Size: Size, in bytes, of image, including all headers and padding
    let _headerPhysSize = seekReadInt32 pev (peOptionalHeaderPhysLoc + 60)  // Header Size Combined size of MS-DOS Header, PE Header, PE Optional Header and padding
    let subsys = seekReadUInt16 pev (peOptionalHeaderPhysLoc + 68)   // SubSystem Subsystem required to run this image.
    let useHighEnthropyVA =
        let n = seekReadUInt16 pev (peOptionalHeaderPhysLoc + 70)
        let highEnthropyVA = 0x20us
        (n &&& highEnthropyVA) = highEnthropyVA

     (* x86: 000000e0 *)

    (* WARNING: THESE ARE 64 bit ON x64/ia64 *)
    (* REVIEW: If we ever decide that we need these values for x64, we'll have to read them in as 64bit and fix up the rest of the offsets.
        Then again, it should suffice to just use the defaults, and still not bother... *)
    (* let stackReserve = seekReadInt32 is (peOptionalHeaderPhysLoc + 72) in *) (* Stack Reserve Size Always 0x100000 (1Mb) (see Section 23.1). *)
    (* let stackCommit = seekReadInt32 is (peOptionalHeaderPhysLoc + 76) in *) (* Stack Commit Size Always 0x1000 (4Kb) (see Section 23.1). *)
    (* let heapReserve = seekReadInt32 is (peOptionalHeaderPhysLoc + 80) in *) (* Heap Reserve Size Always 0x100000 (1Mb) (see Section 23.1). *)
    (* let heapCommit = seekReadInt32 is (peOptionalHeaderPhysLoc + 84) in *) (* Heap Commit Size Always 0x1000 (4Kb) (see Section 23.1). *)

     (* x86: 000000f0, x64: 00000100 *)
    let _numDataDirectories = seekReadInt32 pev (peOptionalHeaderPhysLoc + 92 + x64adjust) (* Number of Data Directories: Always 0x10 (see Section 23.1). *)
     (* 00000100 - these addresses are for x86 - for the x64 location, add x64adjust (0x10) *)
    let _importTableAddr = seekReadInt32 pev (peOptionalHeaderPhysLoc + 104 + x64adjust) (* Import Table RVA of Import Table, (see clause 24.3.1). e.g. 0000b530 *)
    let _importTableSize = seekReadInt32 pev (peOptionalHeaderPhysLoc + 108 + x64adjust) (* Size of Import Table, (see clause 24.3.1). *)
    let nativeResourcesAddr = seekReadInt32 pev (peOptionalHeaderPhysLoc + 112 + x64adjust)
    let nativeResourcesSize = seekReadInt32 pev (peOptionalHeaderPhysLoc + 116 + x64adjust)
     (* 00000110 *)
     (* 00000120 *)
  (* let base_relocTableNames.addr = seekReadInt32 is (peOptionalHeaderPhysLoc + 136)
    let base_relocTableNames.size = seekReadInt32 is (peOptionalHeaderPhysLoc + 140) in *)
     (* 00000130 *)
     (* 00000140 *)
     (* 00000150 *)
    let _importAddrTableAddr = seekReadInt32 pev (peOptionalHeaderPhysLoc + 192 + x64adjust) (* RVA of Import Addr Table, (see clause 24.3.1). e.g. 0x00002000 *)
    let _importAddrTableSize = seekReadInt32 pev (peOptionalHeaderPhysLoc + 196 + x64adjust) (* Size of Import Addr Table, (see clause 24.3.1). e.g. 0x00002000 *)
     (* 00000160 *)
    let cliHeaderAddr = seekReadInt32 pev (peOptionalHeaderPhysLoc + 208 + x64adjust)
    let _cliHeaderSize = seekReadInt32 pev (peOptionalHeaderPhysLoc + 212 + x64adjust)
     (* 00000170 *)


    (* Crack section headers *)

    let sectionHeaders =
      [ for i in 0 .. numSections-1 do
          let pos = sectionHeadersStartPhysLoc + i * 0x28
          let virtSize = seekReadInt32 pev (pos + 8)
          let virtAddr = seekReadInt32 pev (pos + 12)
          let physLoc = seekReadInt32 pev (pos + 20)
          yield (virtAddr, virtSize, physLoc) ]

    let findSectionHeader addr =
      let rec look i pos =
        if i >= numSections then 0x0
        else
          let virtSize = seekReadInt32 pev (pos + 8)
          let virtAddr = seekReadInt32 pev (pos + 12)
          if (addr >= virtAddr && addr < virtAddr + virtSize) then pos
          else look (i+1) (pos + 0x28)
      look 0 sectionHeadersStartPhysLoc

    let textHeaderStart = findSectionHeader cliHeaderAddr
    let dataHeaderStart = findSectionHeader dataSegmentAddr
  (* let relocHeaderStart = findSectionHeader base_relocTableNames.addr in *)

    let _textSize = if textHeaderStart = 0x0 then 0x0 else seekReadInt32 pev (textHeaderStart + 8)
    let _textAddr = if textHeaderStart = 0x0 then 0x0 else seekReadInt32 pev (textHeaderStart + 12)
    let textSegmentPhysicalSize = if textHeaderStart = 0x0 then 0x0 else seekReadInt32 pev (textHeaderStart + 16)
    let textSegmentPhysicalLoc = if textHeaderStart = 0x0 then 0x0 else seekReadInt32 pev (textHeaderStart + 20)

    //let dataSegmentSize = if dataHeaderStart = 0x0 then 0x0 else seekReadInt32 pev (dataHeaderStart + 8)
    //let dataSegmentAddr = if dataHeaderStart = 0x0 then 0x0 else seekReadInt32 pev (dataHeaderStart + 12)
    let dataSegmentPhysicalSize = if dataHeaderStart = 0x0 then 0x0 else seekReadInt32 pev (dataHeaderStart + 16)
    let dataSegmentPhysicalLoc = if dataHeaderStart = 0x0 then 0x0 else seekReadInt32 pev (dataHeaderStart + 20)

    let anyV2P (n, v) =
      let pev = pefile.GetView()
      let rec look i pos =
        if i >= numSections then (failwith (fileName + ": bad "+n+", rva "+string v); 0x0)
        else
          let virtSize = seekReadInt32 pev (pos + 8)
          let virtAddr = seekReadInt32 pev (pos + 12)
          let physLoc = seekReadInt32 pev (pos + 20)
          if (v >= virtAddr && (v < virtAddr + virtSize)) then (v - virtAddr) + physLoc
          else look (i+1) (pos + 0x28)
      look 0 sectionHeadersStartPhysLoc

    let cliHeaderPhysLoc = anyV2P ("cli header", cliHeaderAddr)

    let _majorRuntimeVersion = seekReadUInt16 pev (cliHeaderPhysLoc + 4)
    let _minorRuntimeVersion = seekReadUInt16 pev (cliHeaderPhysLoc + 6)
    let metadataAddr = seekReadInt32 pev (cliHeaderPhysLoc + 8)
    let metadataSize = seekReadInt32 pev (cliHeaderPhysLoc + 12)
    let cliFlags = seekReadInt32 pev (cliHeaderPhysLoc + 16)

    let ilOnly = (cliFlags &&& 0x01) <> 0x00
    let only32 = (cliFlags &&& 0x02) <> 0x00
    let is32bitpreferred = (cliFlags &&& 0x00020003) <> 0x00
    let _strongnameSigned = (cliFlags &&& 0x08) <> 0x00
    let _trackdebugdata = (cliFlags &&& 0x010000) <> 0x00

    let entryPointToken = seekReadUncodedToken pev (cliHeaderPhysLoc + 20)
    let resourcesAddr = seekReadInt32 pev (cliHeaderPhysLoc + 24)
    let resourcesSize = seekReadInt32 pev (cliHeaderPhysLoc + 28)
    let strongnameAddr = seekReadInt32 pev (cliHeaderPhysLoc + 32)
    let _strongnameSize = seekReadInt32 pev (cliHeaderPhysLoc + 36)
    let vtableFixupsAddr = seekReadInt32 pev (cliHeaderPhysLoc + 40)
    let _vtableFixupsSize = seekReadInt32 pev (cliHeaderPhysLoc + 44)

    if logging then dprintn (fileName + ": metadataAddr = "+string metadataAddr)
    if logging then dprintn (fileName + ": resourcesAddr = "+string resourcesAddr)
    if logging then dprintn (fileName + ": resourcesSize = "+string resourcesSize)
    if logging then dprintn (fileName + ": nativeResourcesAddr = "+string nativeResourcesAddr)
    if logging then dprintn (fileName + ": nativeResourcesSize = "+string nativeResourcesSize)

    let metadataPhysLoc = anyV2P ("metadata", metadataAddr)
   //-----------------------------------------------------------------------
   // Set up the PDB reader so we can read debug info for methods.
   // ----------------------------------------------------------------------
#if FX_NO_PDB_READER
    let pdb = ignore pdbDirPath; None
#else
    let pdb =
        if runningOnMono then
            None
        else
            getPdbReader pdbDirPath fileName
#endif

    let pectxt: PEReader =
        { pdb=pdb
          textSegmentPhysicalLoc=textSegmentPhysicalLoc
          textSegmentPhysicalSize=textSegmentPhysicalSize
          dataSegmentPhysicalLoc=dataSegmentPhysicalLoc
          dataSegmentPhysicalSize=dataSegmentPhysicalSize
          anyV2P=anyV2P
          metadataAddr=metadataAddr
          sectionHeaders=sectionHeaders
          nativeResourcesAddr=nativeResourcesAddr
          nativeResourcesSize=nativeResourcesSize
          resourcesAddr=resourcesAddr
          strongnameAddr=strongnameAddr
          vtableFixupsAddr=vtableFixupsAddr
          pefile=pefile
          fileName=fileName
          entryPointToken=entryPointToken
          noFileOnDisk=noFileOnDisk
        }
    let peinfo = (subsys, (subsysMajor, subsysMinor), useHighEnthropyVA, ilOnly, only32, is32bitpreferred, only64, platform, isDll, alignVirt, alignPhys, imageBaseReal)
    (metadataPhysLoc, metadataSize, peinfo, pectxt, pev, pdb)

let openPE (fileName, pefile, pdbDirPath, reduceMemoryUsage, noFileOnDisk) =
    let (metadataPhysLoc, _metadataSize, peinfo, pectxt, pev, pdb) = openPEFileReader (fileName, pefile, pdbDirPath, noFileOnDisk)
    let ilModule, ilAssemblyRefs = openMetadataReader (fileName, pefile, metadataPhysLoc, peinfo, pectxt, pev, Some pectxt, reduceMemoryUsage)
    ilModule, ilAssemblyRefs, pdb

let openPEMetadataOnly (fileName, peinfo, pectxtEager, pevEager, mdfile: BinaryFile, reduceMemoryUsage) =
    openMetadataReader (fileName, mdfile, 0, peinfo, pectxtEager, pevEager, None, reduceMemoryUsage)

let ClosePdbReader pdb =
#if FX_NO_PDB_READER
    ignore pdb
    ()
#else
    match pdb with
    | Some (pdbr, _) -> pdbReadClose pdbr
    | None -> ()
#endif

type ILReaderMetadataSnapshot = (obj * nativeint * int)
type ILReaderTryGetMetadataSnapshot = (* path: *) string * (* snapshotTimeStamp: *) System.DateTime -> ILReaderMetadataSnapshot option

[<RequireQualifiedAccess>]
type MetadataOnlyFlag = Yes | No

[<RequireQualifiedAccess>]
type ReduceMemoryFlag = Yes | No

type ILReaderOptions =
    { pdbDirPath: string option
      reduceMemoryUsage: ReduceMemoryFlag
      metadataOnly: MetadataOnlyFlag
      tryGetMetadataSnapshot: ILReaderTryGetMetadataSnapshot }

type ILModuleReader =
    abstract ILModuleDef: ILModuleDef
    abstract ILAssemblyRefs: ILAssemblyRef list

    /// ILModuleReader objects only need to be explicitly disposed if memory mapping is used, i.e. reduceMemoryUsage = false
    inherit System.IDisposable

[<Sealed>]
type ILModuleReaderImpl(ilModule: ILModuleDef, ilAssemblyRefs: Lazy<ILAssemblyRef list>, dispose: unit -> unit) =
    interface ILModuleReader with
        member x.ILModuleDef = ilModule
        member x.ILAssemblyRefs = ilAssemblyRefs.Force()
        member x.Dispose() = dispose()

// ++GLOBAL MUTABLE STATE (concurrency safe via locking)
type ILModuleReaderCacheKey = ILModuleReaderCacheKey of string * DateTime * bool * ReduceMemoryFlag * MetadataOnlyFlag

// Cache to extend the lifetime of a limited number of readers that are otherwise eligible for GC
type ILModuleReaderCache1LockToken() = interface LockToken
let ilModuleReaderCache1 =
    new AgedLookup<ILModuleReaderCache1LockToken, ILModuleReaderCacheKey, ILModuleReader>
           (stronglyHeldReaderCacheSize,
            keepMax=stronglyHeldReaderCacheSize, // only strong entries
            areSimilar=(fun (x, y) -> x = y))
let ilModuleReaderCache1Lock = Lock()

// // Cache to reuse readers that have already been created and are not yet GC'd
let ilModuleReaderCache2 = new ConcurrentDictionary<ILModuleReaderCacheKey, System.WeakReference<ILModuleReader>>(HashIdentity.Structural)

let stableFileHeuristicApplies fileName =
    not noStableFileHeuristic && try FileSystem.IsStableFileHeuristic fileName with _ -> false

let createByteFileChunk opts fileName chunk =
    // If we're trying to reduce memory usage then we are willing to go back and re-read the binary, so we can use
    // a weakly-held handle to an array of bytes.
    if opts.reduceMemoryUsage = ReduceMemoryFlag.Yes && stableFileHeuristicApplies fileName then
        WeakByteFile(fileName, chunk) :> BinaryFile
    else
        let bytes =
            use stream = FileSystem.OpenFileForReadShim(fileName)
            match chunk with
            | None -> stream.ReadAllBytes()
            | Some(start, length) -> stream.ReadBytes(start, length)

        ByteFile(fileName, bytes) :> BinaryFile

let getBinaryFile fileName useMemoryMappedFile =
    let stream = FileSystem.OpenFileForReadShim(fileName, useMemoryMappedFile = useMemoryMappedFile)
    let byteMem = stream.AsByteMemory()

    let safeHolder =
        { new obj() with
            override x.Finalize() =
                (x :?> IDisposable).Dispose()
          interface IDisposable with
            member x.Dispose() =
                GC.SuppressFinalize x
                stream.Dispose()
                stats.memoryMapFileClosedCount <- stats.memoryMapFileClosedCount + 1 }

    stats.memoryMapFileOpenedCount <- stats.memoryMapFileOpenedCount + 1

    safeHolder, RawMemoryFile(fileName, safeHolder, byteMem) :> BinaryFile

let OpenILModuleReaderFromBytes fileName assemblyContents options =
    let pefile = ByteFile(fileName, assemblyContents) :> BinaryFile
    let ilModule, ilAssemblyRefs, pdb = openPE (fileName, pefile, options.pdbDirPath, (options.reduceMemoryUsage = ReduceMemoryFlag.Yes), true)
    new ILModuleReaderImpl(ilModule, ilAssemblyRefs, (fun () -> ClosePdbReader pdb)) :> ILModuleReader

let OpenILModuleReaderFromStream fileName (peStream: Stream) options =
    let peReader = new System.Reflection.PortableExecutable.PEReader(peStream, PEStreamOptions.PrefetchEntireImage)
    let pefile = PEFile(fileName, peReader) :> BinaryFile
    let ilModule, ilAssemblyRefs, pdb = openPE (fileName, pefile, options.pdbDirPath, (options.reduceMemoryUsage = ReduceMemoryFlag.Yes), true)
    new ILModuleReaderImpl(ilModule, ilAssemblyRefs, (fun () -> ClosePdbReader pdb)) :> ILModuleReader

let ClearAllILModuleReaderCache() =
    ilModuleReaderCache1.Clear(ILModuleReaderCache1LockToken())
    ilModuleReaderCache2.Clear()

let OpenILModuleReader fileName opts =
    // Pseudo-normalize the paths.
    let (ILModuleReaderCacheKey (fullPath,writeStamp,_,_,_) as key), keyOk =
        try
           let fullPath = FileSystem.GetFullPathShim fileName
           let writeTime = FileSystem.GetLastWriteTimeShim fileName
           let key = ILModuleReaderCacheKey (fullPath, writeTime, opts.pdbDirPath.IsSome, opts.reduceMemoryUsage, opts.metadataOnly)
           key, true
        with exn ->
            System.Diagnostics.Debug.Assert(false, sprintf "Failed to compute key in OpenILModuleReader cache for '%s'. Falling back to uncached. Error = %s" fileName (exn.ToString()))
            let fakeKey = ILModuleReaderCacheKey(fileName, System.DateTime.UtcNow, false, ReduceMemoryFlag.Yes, MetadataOnlyFlag.Yes)
            fakeKey, false

    let cacheResult1 =
        // can't used a cached entry when reading PDBs, since it makes the returned object IDisposable
        if keyOk && opts.pdbDirPath.IsNone then
            ilModuleReaderCache1Lock.AcquireLock (fun ltok -> ilModuleReaderCache1.TryGet(ltok, key))
        else
            None

    match cacheResult1 with
    | Some ilModuleReader -> ilModuleReader
    | None ->

    let cacheResult2 =
        // can't used a cached entry when reading PDBs, since it makes the returned object IDisposable
        if keyOk && opts.pdbDirPath.IsNone then
            ilModuleReaderCache2.TryGetValue key
        else
            false, Unchecked.defaultof<_>

    let mutable res = Unchecked.defaultof<_>
    match cacheResult2 with
    | true, weak when weak.TryGetTarget(&res) -> res
    | _ ->

    let reduceMemoryUsage = (opts.reduceMemoryUsage = ReduceMemoryFlag.Yes)
    let metadataOnly = (opts.metadataOnly = MetadataOnlyFlag.Yes)

    if reduceMemoryUsage && opts.pdbDirPath.IsNone then

        // This case is used in FCS applications, devenv.exe and fsi.exe
        //
        let ilModuleReader =
            // Check if we are doing metadataOnly reading (the most common case in both the compiler and IDE)
            if not runningOnMono && metadataOnly then

                // See if tryGetMetadata gives us a BinaryFile for the metadata section alone.
                let mdfileOpt =
                    match opts.tryGetMetadataSnapshot (fullPath, writeStamp) with
                    | Some (obj, start, len) -> Some (RawMemoryFile(fullPath, obj, start, len) :> BinaryFile)
                    | None -> None

                // For metadata-only, always use a temporary, short-lived PE file reader, preferably over a memory mapped file.
                // Then use the metadata blob as the long-lived memory resource.
                let disposer, pefileEager = getBinaryFile fullPath false
                use _disposer = disposer
                let (metadataPhysLoc, metadataSize, peinfo, pectxtEager, pevEager, _pdb) = openPEFileReader (fullPath, pefileEager, None, false)
                let mdfile =
                    match mdfileOpt with
                    | Some mdfile -> mdfile
                    | None ->
                        // If tryGetMetadata doesn't give anything, then just read the metadata chunk out of the binary
                        createByteFileChunk opts fullPath (Some (metadataPhysLoc, metadataSize))

                let ilModule, ilAssemblyRefs = openPEMetadataOnly (fullPath, peinfo, pectxtEager, pevEager, mdfile, reduceMemoryUsage)
                new ILModuleReaderImpl(ilModule, ilAssemblyRefs, ignore)
            else
                // If we are not doing metadata-only, then just go ahead and read all the bytes and hold them either strongly or weakly
                // depending on the heuristic
                let pefile = createByteFileChunk opts fullPath None
                let ilModule, ilAssemblyRefs, _pdb = openPE (fullPath, pefile, None, reduceMemoryUsage, false)
                new ILModuleReaderImpl(ilModule, ilAssemblyRefs, ignore)

        let ilModuleReader = ilModuleReader :> ILModuleReader
        if keyOk then
            ilModuleReaderCache1Lock.AcquireLock (fun ltok -> ilModuleReaderCache1.Put(ltok, key, ilModuleReader))
            ilModuleReaderCache2.[key] <- System.WeakReference<_>(ilModuleReader)
        ilModuleReader


    else
        // This case is primarily used in fsc.exe.
        //
        // In fsc.exe, we're not trying to reduce memory usage, nor do we really care if we leak memory.
        //
        // Note we ignore the "metadata only" flag as it's generally OK to read in the
        // whole binary for the command-line compiler: address space is rarely an issue.
        //
        // We do however care about avoiding locks on files that prevent their deletion during a
        // multi-proc build. So use memory mapping, but only for stable files. Other files
        // still use an in-memory ByteFile
        let pefile =
            if not runningOnMono && (alwaysMemoryMapFSC || stableFileHeuristicApplies fullPath) then
                let _, pefile = getBinaryFile fullPath false
                pefile
            else
                createByteFileChunk opts fullPath None

        let ilModule, ilAssemblyRefs, pdb = openPE (fullPath, pefile, opts.pdbDirPath, reduceMemoryUsage, false)
        let ilModuleReader = new ILModuleReaderImpl(ilModule, ilAssemblyRefs, (fun () -> ClosePdbReader pdb))

        let ilModuleReader = ilModuleReader :> ILModuleReader

        // Readers with PDB reader disposal logic don't go in the cache. Note the PDB reader is only used in static linking.
        if keyOk && opts.pdbDirPath.IsNone then
            ilModuleReaderCache1Lock.AcquireLock (fun ltok -> ilModuleReaderCache1.Put(ltok, key, ilModuleReader))
            ilModuleReaderCache2.[key] <- WeakReference<_>(ilModuleReader)

        ilModuleReader

[<AutoOpen>]
module Shim =

    type IAssemblyReader =
        abstract GetILModuleReader: filename: string * readerOptions: ILReaderOptions -> ILModuleReader

    [<Sealed>]
    type DefaultAssemblyReader() =
        interface IAssemblyReader with
            member _.GetILModuleReader(filename, readerOptions) =
                OpenILModuleReader filename readerOptions

    let mutable AssemblyReader = DefaultAssemblyReader() :> IAssemblyReader
