// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.TypedTreePickle

open System.Collections.Generic
open System.Text

open FSharp.Compiler.IO
open Internal.Utilities
open Internal.Utilities.Collections
open Internal.Utilities.Library
open Internal.Utilities.Library.Extras
open Internal.Utilities.Library.Extras.Bits
open Internal.Utilities.Rational

open FSharp.Compiler
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AbstractIL.Diagnostics
open FSharp.Compiler.CompilerGlobalState
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Text.Position
open FSharp.Compiler.Text.Range
open FSharp.Compiler.Syntax
open FSharp.Compiler.SyntaxTreeOps
open FSharp.Compiler.Text
open FSharp.Compiler.Xml
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.TcGlobals

let verbose = false

let ffailwith fileName str =
    let msg = FSComp.SR.pickleErrorReadingWritingMetadata(fileName, str)
    System.Diagnostics.Debug.Assert(false, msg)
    failwith msg

// Fixup pickled data w.r.t. a set of CCU thunks indexed by name
[<NoEquality; NoComparison>]
type PickledDataWithReferences<'rawData> =
    { /// The data that uses a collection of CcuThunks internally
      RawData: 'rawData
      /// The assumptions that need to be fixed up
      FixupThunks: CcuThunk [] }

    member x.Fixup loader =
        x.FixupThunks |> Array.iter (fun reqd -> reqd.Fixup(loader reqd.AssemblyName))
        x.RawData

    /// Like Fixup but loader may return None, in which case there is no fixup.
    member x.OptionalFixup loader =
        x.FixupThunks
        |> Array.iter(fun reqd->
            // Only fixup what needs fixing up
            if reqd.IsUnresolvedReference then
                match loader reqd.AssemblyName with
                | Some loaded ->
                    if reqd.IsUnresolvedReference then reqd.Fixup loaded
                | _ -> () )
        x.RawData

//---------------------------------------------------------------------------
// Basic pickle/unpickle state
//---------------------------------------------------------------------------

[<NoEquality; NoComparison>]
type Table<'T> =
    { name: string
      tbl: Dictionary<'T, int>
      mutable rows: ResizeArray<'T>
      mutable count: int }
    member tbl.AsArray = Seq.toArray tbl.rows
    member tbl.Size = tbl.rows.Count
    member tbl.Add x =
        let n = tbl.count
        tbl.count <- tbl.count + 1
        tbl.tbl.[x] <- n
        tbl.rows.Add x
        n
    member tbl.FindOrAdd x =
        match tbl.tbl.TryGetValue x with
        | true, res -> res
        | _ -> tbl.Add x


    static member Create n =
      { name = n
        tbl = new System.Collections.Generic.Dictionary<_, _>(1000, HashIdentity.Structural)
        rows= new ResizeArray<_>(1000)
        count=0 }

[<NoEquality; NoComparison>]
type InputTable<'T> =
    { itbl_name: string
      itbl_rows: 'T array }

let new_itbl n r = { itbl_name=n; itbl_rows=r }

[<NoEquality; NoComparison>]
type NodeOutTable<'Data, 'Node> =
    { NodeStamp : ('Node -> Stamp)
      NodeName : ('Node -> string)
      GetRange : ('Node -> range)
      Deref: ('Node -> 'Data)
      Name: string
      Table: Table<Stamp> }
    member x.Size = x.Table.Size

    // inline this to get known-type-information through to the HashMultiMap constructor
    static member inline Create (stampF, nameF, rangeF, derefF, nm) =
        { NodeStamp = stampF
          NodeName = nameF
          GetRange = rangeF
          Deref = derefF
          Name = nm
          Table = Table<_>.Create nm }

[<NoEquality; NoComparison>]
type WriterState =
  { os: ByteBuffer
    oscope: CcuThunk
    occus: Table<CcuReference>
    oentities: NodeOutTable<EntityData, Entity>
    otypars: NodeOutTable<TyparData, Typar>
    ovals: NodeOutTable<ValData, Val>
    oanoninfos: NodeOutTable<AnonRecdTypeInfo, AnonRecdTypeInfo>
    ostrings: Table<string>
    opubpaths: Table<int[]>
    onlerefs: Table<int * int[]>
    osimpletys: Table<int>
    oglobals : TcGlobals
    mutable isStructThisArgPos : bool
    ofile : string
    /// Indicates if we are using in-memory format, where we store XML docs as well
    oInMem : bool
  }
let pfailwith st str = ffailwith st.ofile str

[<NoEquality; NoComparison>]
type NodeInTable<'Data, 'Node> =
    { LinkNode : ('Node -> 'Data -> unit)
      IsLinked : ('Node -> bool)
      Name : string
      Nodes : 'Node[] }
    member x.Get n = x.Nodes.[n]
    member x.Count = x.Nodes.Length

    static member Create (mkEmpty, lnk, isLinked, nm, n) =
        { LinkNode = lnk; IsLinked = isLinked; Name = nm; Nodes = Array.init n (fun _i -> mkEmpty() ) }

[<NoEquality; NoComparison>]
type ReaderState =
  { is: ByteStream
    iilscope: ILScopeRef
    iccus: InputTable<CcuThunk>
    ientities: NodeInTable<EntityData, Tycon>
    itypars: NodeInTable<TyparData, Typar>
    ivals: NodeInTable<ValData, Val>
    ianoninfos: NodeInTable<AnonRecdTypeInfo, AnonRecdTypeInfo>
    istrings: InputTable<string>
    ipubpaths: InputTable<PublicPath>
    inlerefs: InputTable<NonLocalEntityRef>
    isimpletys: InputTable<TType>
    ifile: string
    iILModule : ILModuleDef option // the Abstract IL metadata for the DLL being read
  }

let ufailwith st str = ffailwith st.ifile str

//---------------------------------------------------------------------------
// Basic pickle/unpickle operations
//---------------------------------------------------------------------------

type 'T pickler = 'T -> WriterState -> unit

let p_byte b st = st.os.EmitIntAsByte b

let p_bool b st = p_byte (if b then 1 else 0) st

let prim_p_int32 i st =
    p_byte (b0 i) st
    p_byte (b1 i) st
    p_byte (b2 i) st
    p_byte (b3 i) st

/// Compress integers according to the same scheme used by CLR metadata
/// This halves the size of pickled data
let p_int32 n st =
    if n >= 0 && n <= 0x7F then
        p_byte (b0 n) st
    else if n >= 0x80 && n <= 0x3FFF then
        p_byte ( (0x80 ||| (n >>> 8))) st
        p_byte ( (n &&& 0xFF)) st
    else
        p_byte 0xFF st
        prim_p_int32 n st

let space = ()
let p_space n () st =
    for i = 0 to n - 1 do
        p_byte 0 st

/// Represents space that was reserved but is now possibly used
let p_used_space1 f st =
    p_byte 1 st
    f st
    // leave more space
    p_space 1 space st

let p_bytes (s: byte[]) st =
    let len = s.Length
    p_int32 len st
    st.os.EmitBytes s

let p_memory (s: System.ReadOnlyMemory<byte>) st =
    let len = s.Length
    p_int32 len st
    st.os.EmitMemory s

let p_prim_string (s: string) st =
    let bytes = Encoding.UTF8.GetBytes s
    let len = bytes.Length
    p_int32 len st
    st.os.EmitBytes bytes

let p_int c st = p_int32 c st
let p_int8 (i: sbyte) st = p_int32 (int32 i) st
let p_uint8 (i: byte) st = p_byte (int i) st
let p_int16 (i: int16) st = p_int32 (int32 i) st
let p_uint16 (x: uint16) st = p_int32 (int32 x) st
let p_uint32 (x: uint32) st = p_int32 (int32 x) st
let p_int64 (i: int64) st =
    p_int32 (int32 (i &&& 0xFFFFFFFFL)) st
    p_int32 (int32 (i >>> 32)) st

let p_uint64 (x: uint64) st = p_int64 (int64 x) st

let bits_of_float32 (x: float32) = System.BitConverter.ToInt32(System.BitConverter.GetBytes x, 0)
let bits_of_float (x: float) = System.BitConverter.DoubleToInt64Bits x

let p_single i st = p_int32 (bits_of_float32 i) st
let p_double i st = p_int64 (bits_of_float i) st
let p_ieee64 i st = p_int64 (bits_of_float i) st
let p_char i st = p_uint16 (uint16 (int32 i)) st

let inline p_tup2 p1 p2 (a, b) (st: WriterState) =
    (p1 a st : unit); (p2 b st : unit)

let inline p_tup3 p1 p2 p3 (a, b, c) (st: WriterState) =
    (p1 a st : unit); (p2 b st : unit); (p3 c st : unit)

let inline  p_tup4 p1 p2 p3 p4 (a, b, c, d) (st: WriterState) =
    (p1 a st : unit); (p2 b st : unit); (p3 c st : unit); (p4 d st : unit)

let inline  p_tup5 p1 p2 p3 p4 p5 (a, b, c, d, e) (st: WriterState) =
    (p1 a st : unit); (p2 b st : unit); (p3 c st : unit); (p4 d st : unit); (p5 e st : unit)

let inline  p_tup6 p1 p2 p3 p4 p5 p6 (a, b, c, d, e, f) (st: WriterState) =
    (p1 a st : unit); (p2 b st : unit); (p3 c st : unit); (p4 d st : unit); (p5 e st : unit); (p6 f st : unit)

let inline  p_tup7 p1 p2 p3 p4 p5 p6 p7 (a, b, c, d, e, f, x7) (st: WriterState) =
    (p1 a st : unit); (p2 b st : unit); (p3 c st : unit); (p4 d st : unit); (p5 e st : unit); (p6 f st : unit); (p7 x7 st : unit)

let inline  p_tup8 p1 p2 p3 p4 p5 p6 p7 p8 (a, b, c, d, e, f, x7, x8) (st: WriterState) =
    (p1 a st : unit); (p2 b st : unit); (p3 c st : unit); (p4 d st : unit); (p5 e st : unit); (p6 f st : unit); (p7 x7 st : unit); (p8 x8 st : unit)

let inline  p_tup9 p1 p2 p3 p4 p5 p6 p7 p8 p9 (a, b, c, d, e, f, x7, x8, x9) (st: WriterState) =
    (p1 a st : unit); (p2 b st : unit); (p3 c st : unit); (p4 d st : unit); (p5 e st : unit); (p6 f st : unit); (p7 x7 st : unit); (p8 x8 st : unit); (p9 x9 st : unit)

let inline  p_tup10 p1 p2 p3 p4 p5 p6 p7 p8 p9 p10 (a, b, c, d, e, f, x7, x8, x9, x10) (st: WriterState) =
    (p1 a st : unit); (p2 b st : unit); (p3 c st : unit); (p4 d st : unit); (p5 e st : unit); (p6 f st : unit); (p7 x7 st : unit); (p8 x8 st : unit); (p9 x9 st : unit); (p10 x10 st : unit)

let inline  p_tup11 p1 p2 p3 p4 p5 p6 p7 p8 p9 p10 p11 (a, b, c, d, e, f, x7, x8, x9, x10, x11) (st: WriterState) =
    (p1 a st : unit); (p2 b st : unit); (p3 c st : unit); (p4 d st : unit); (p5 e st : unit); (p6 f st : unit); (p7 x7 st : unit); (p8 x8 st : unit); (p9 x9 st : unit); (p10 x10 st : unit); (p11 x11 st : unit)

let u_byte st = int (st.is.ReadByte())

type unpickler<'T> = ReaderState -> 'T

let u_bool st = let b = u_byte st in (b = 1)



let prim_u_int32 st =
    let b0 =  (u_byte st)
    let b1 =  (u_byte st)
    let b2 =  (u_byte st)
    let b3 =  (u_byte st)
    b0 ||| (b1 <<< 8) ||| (b2 <<< 16) ||| (b3 <<< 24)

let u_int32 st =
    let b0 = u_byte st
    if b0 <= 0x7F then b0
    else if b0 <= 0xbf then
        let b0 = b0 &&& 0x7F
        let b1 = (u_byte st)
        (b0 <<< 8) ||| b1
    else
        assert(b0 = 0xFF)
        prim_u_int32 st

let u_byte_memory st =
    let n =  (u_int32 st)
    st.is.ReadBytes n

let u_bytes st =
    (u_byte_memory st).ToArray()

let u_prim_string st =
    let len =  (u_int32 st)
    st.is.ReadUtf8String len

let u_int st = u_int32 st
let u_int8 st = sbyte (u_int32 st)
let u_uint8 st = byte (u_byte st)
let u_int16 st = int16 (u_int32 st)
let u_uint16 st = uint16 (u_int32 st)
let u_uint32 st = uint32 (u_int32 st)
let u_int64 st =
    let b1 = (int64 (u_int32 st)) &&& 0xFFFFFFFFL
    let b2 = int64 (u_int32 st)
    b1 ||| (b2 <<< 32)

let u_uint64 st = uint64 (u_int64 st)
let float32_of_bits (x: int32) = System.BitConverter.ToSingle(System.BitConverter.GetBytes x, 0)
let float_of_bits (x: int64) = System.BitConverter.Int64BitsToDouble x

let u_single st = float32_of_bits (u_int32 st)
let u_double st = float_of_bits (u_int64 st)

let u_ieee64 st = float_of_bits (u_int64 st)

let u_char st = char (int32 (u_uint16 st))
let u_space n st =
    for i = 0 to n - 1 do
        let b = u_byte st
        if b <> 0 then
            warning(Error(FSComp.SR.pickleUnexpectedNonZero st.ifile, range0))

/// Represents space that was reserved but is now possibly used
let u_used_space1 f st =
    let b = u_byte st
    match b with
    | 0 -> None
    | 1 ->
        let x = f st
        u_space 1 st
        Some x
    | _ ->
        warning(Error(FSComp.SR.pickleUnexpectedNonZero st.ifile, range0)); None


let inline  u_tup2 p1 p2 (st: ReaderState) = let a = p1 st in let b = p2 st in (a, b)

let inline  u_tup3 p1 p2 p3 (st: ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in (a, b, c)

let inline u_tup4 p1 p2 p3 p4 (st: ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in (a, b, c, d)

let inline u_tup5 p1 p2 p3 p4 p5 (st: ReaderState) =
  let a = p1 st
  let b = p2 st
  let c = p3 st
  let d = p4 st
  let e = p5 st
  (a, b, c, d, e)

let inline u_tup6 p1 p2 p3 p4 p5 p6 (st: ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in let e = p5 st in let f = p6 st in (a, b, c, d, e, f)

let inline u_tup7 p1 p2 p3 p4 p5 p6 p7 (st: ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in let e = p5 st in let f = p6 st in let x7 = p7 st in (a, b, c, d, e, f, x7)

let inline u_tup8 p1 p2 p3 p4 p5 p6 p7 p8 (st: ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in let e = p5 st in let f = p6 st in let x7 = p7 st in let x8 = p8 st in  (a, b, c, d, e, f, x7, x8)

let inline u_tup9 p1 p2 p3 p4 p5 p6 p7 p8 p9 (st: ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in let e = p5 st in let f = p6 st in let x7 = p7 st in let x8 = p8 st in let x9 = p9 st in (a, b, c, d, e, f, x7, x8, x9)

let inline u_tup10 p1 p2 p3 p4 p5 p6 p7 p8 p9 p10 (st: ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in
  let e = p5 st in let f = p6 st in let x7 = p7 st in let x8 = p8 st in
  let x9 = p9 st in let x10 = p10 st in (a, b, c, d, e, f, x7, x8, x9, x10)

let inline u_tup11 p1 p2 p3 p4 p5 p6 p7 p8 p9 p10 p11 (st: ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in
  let e = p5 st in let f = p6 st in let x7 = p7 st in let x8 = p8 st in
  let x9 = p9 st in let x10 = p10 st in let x11 = p11 st in (a, b, c, d, e, f, x7, x8, x9, x10, x11)

let inline u_tup12 p1 p2 p3 p4 p5 p6 p7 p8 p9 p10 p11 p12 (st: ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in
  let e = p5 st in let f = p6 st in let x7 = p7 st in let x8 = p8 st in
  let x9 = p9 st in let x10 = p10 st in let x11 = p11 st in let x12 = p12 st in
  (a, b, c, d, e, f, x7, x8, x9, x10, x11, x12)

let inline u_tup13 p1 p2 p3 p4 p5 p6 p7 p8 p9 p10 p11 p12 p13 (st: ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in
  let e = p5 st in let f = p6 st in let x7 = p7 st in let x8 = p8 st in
  let x9 = p9 st in let x10 = p10 st in let x11 = p11 st in let x12 = p12 st in let x13 = p13 st in
  (a, b, c, d, e, f, x7, x8, x9, x10, x11, x12, x13)

let inline u_tup14 p1 p2 p3 p4 p5 p6 p7 p8 p9 p10 p11 p12 p13 p14 (st: ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in
  let e = p5 st in let f = p6 st in let x7 = p7 st in let x8 = p8 st in
  let x9 = p9 st in let x10 = p10 st in let x11 = p11 st in let x12 = p12 st in let x13 = p13 st in
  let x14 = p14 st in
  (a, b, c, d, e, f, x7, x8, x9, x10, x11, x12, x13, x14)
let inline u_tup15 p1 p2 p3 p4 p5 p6 p7 p8 p9 p10 p11 p12 p13 p14 p15 (st: ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in
  let e = p5 st in let f = p6 st in let x7 = p7 st in let x8 = p8 st in
  let x9 = p9 st in let x10 = p10 st in let x11 = p11 st in let x12 = p12 st in let x13 = p13 st in
  let x14 = p14 st in let x15 = p15 st in
  (a, b, c, d, e, f, x7, x8, x9, x10, x11, x12, x13, x14, x15)

let inline u_tup16 p1 p2 p3 p4 p5 p6 p7 p8 p9 p10 p11 p12 p13 p14 p15 p16 (st: ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in
  let e = p5 st in let f = p6 st in let x7 = p7 st in let x8 = p8 st in
  let x9 = p9 st in let x10 = p10 st in let x11 = p11 st in let x12 = p12 st in let x13 = p13 st in
  let x14 = p14 st in let x15 = p15 st in let x16 = p16 st in
  (a, b, c, d, e, f, x7, x8, x9, x10, x11, x12, x13, x14, x15, x16)

let inline u_tup17 p1 p2 p3 p4 p5 p6 p7 p8 p9 p10 p11 p12 p13 p14 p15 p16 p17 (st: ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in
  let e = p5 st in let f = p6 st in let x7 = p7 st in let x8 = p8 st in
  let x9 = p9 st in let x10 = p10 st in let x11 = p11 st in let x12 = p12 st in let x13 = p13 st in
  let x14 = p14 st in let x15 = p15 st in let x16 = p16 st in let x17 = p17 st in
  (a, b, c, d, e, f, x7, x8, x9, x10, x11, x12, x13, x14, x15, x16, x17)


//---------------------------------------------------------------------------
// Pickle/unpickle operations for observably shared graph nodes
//---------------------------------------------------------------------------

// exception Nope

// ctxt is for debugging
let p_osgn_ref (_ctxt: string) (outMap : NodeOutTable<_, _>) x st =
    let idx = outMap.Table.FindOrAdd (outMap.NodeStamp x)
    //if ((idx = 0) && outMap.Name = "oentities") then
    //    let msg =
    //        sprintf "idx %d#%d in table %s has name '%s', was defined at '%s' and is referenced from context %s\n"
    //            idx (outMap.NodeStamp x)
    //            outMap.Name (outMap.NodeName x)
    //            (stringOfRange (outMap.GetRange x))
    //            _ctxt
    //    System.Diagnostics.Debug.Assert(false, msg )
    p_int idx st

let p_osgn_decl (outMap : NodeOutTable<_, _>) p x st =
    let stamp = outMap.NodeStamp x
    let idx = outMap.Table.FindOrAdd stamp
    //dprintf "decl %d#%d in table %s has name %s\n" idx (outMap.NodeStamp x) outMap.Name (outMap.NodeName x)
    p_tup2 p_int p (idx, outMap.Deref x) st

let u_osgn_ref (inMap: NodeInTable<_, _>) st =
    let n = u_int st
    if n < 0 || n >= inMap.Count then ufailwith st ("u_osgn_ref: out of range, table = "+inMap.Name+", n = "+string n)
    inMap.Get n

let u_osgn_decl (inMap: NodeInTable<_, _>) u st =
    let idx, data = u_tup2 u_int u st
    //   dprintf "unpickling osgn %d in table %s\n" idx nm
    let res = inMap.Get idx
    inMap.LinkNode res data
    res

//---------------------------------------------------------------------------
// Pickle/unpickle operations for interned nodes
//---------------------------------------------------------------------------

let encode_uniq (tbl: Table<_>) key = tbl.FindOrAdd key
let lookup_uniq st tbl n =
    let arr = tbl.itbl_rows
    if n < 0 || n >= arr.Length then ufailwith st ("lookup_uniq in table "+tbl.itbl_name+" out of range, n = "+string n+ ", sizeof(tab) = " + string (Array.length arr))
    arr.[n]

//---------------------------------------------------------------------------
// Pickle/unpickle arrays and lists. For lists use the same binary format as arrays so we can switch
// between internal representations relatively easily
//-------------------------------------------------------------------------

let p_array_core f (x: 'T[]) st =
    for i = 0 to x.Length-1 do
        f x.[i] st

let p_array f (x: 'T[]) st =
    p_int x.Length st
    p_array_core f x st

// Optionally encode an extra item using a marker bit.
// When extraf is None, the marker bit is not set, and this is identical to p_array.
let p_array_ext extraf f (x: 'T[]) st =
    let n = x.Length
    let n = if Option.isSome extraf then n ||| 0x80000000 else n
    p_int n st
    match extraf with
    | None -> ()
    | Some f -> f st
    p_array_core f x st

let p_list_core f (xs: 'T list) st =
    for x in xs do
        f x st

let p_list f x st =
    p_int (List.length x) st
    p_list_core f x st
let p_list_ext extraf f x st =
    let n = List.length x
    let n = if Option.isSome extraf then n ||| 0x80000000 else n
    p_int n st
    match extraf with
    | None -> ()
    | Some f -> f st
    p_list_core f x st

let p_List f (x: 'T list) st = p_list f x st

let p_wrap (f: 'T -> 'U) (p : 'U pickler) : 'T pickler = (fun x st -> p (f x) st)
let p_option f x st =
    match x with
    | None -> p_byte 0 st
    | Some h -> p_byte 1 st; f h st

// Pickle lazy values in such a way that they can, in some future F# compiler version, be read back
// lazily. However, a lazy reader is not used in this version because the value may contain the definitions of some
// OSGN nodes.
let private p_lazy_impl p v st =
    let fixupPos1 = st.os.Position
    // We fix these up after
    prim_p_int32 0 st
    let fixupPos2 = st.os.Position
    prim_p_int32 0 st
    let fixupPos3 = st.os.Position
    prim_p_int32 0 st
    let fixupPos4 = st.os.Position
    prim_p_int32 0 st
    let fixupPos5 = st.os.Position
    prim_p_int32 0 st
    let fixupPos6 = st.os.Position
    prim_p_int32 0 st
    let fixupPos7 = st.os.Position
    prim_p_int32 0 st
    let idx1 = st.os.Position
    let otyconsIdx1 = st.oentities.Size
    let otyparsIdx1 = st.otypars.Size
    let ovalsIdx1 = st.ovals.Size
    // Run the pickler
    p v st
    // Determine and fixup the length of the pickled data
    let idx2 = st.os.Position
    st.os.FixupInt32 fixupPos1 (idx2-idx1)
    // Determine and fixup the ranges of OSGN nodes defined within the lazy portion
    let otyconsIdx2 = st.oentities.Size
    let otyparsIdx2 = st.otypars.Size
    let ovalsIdx2 = st.ovals.Size
    st.os.FixupInt32 fixupPos2 otyconsIdx1
    st.os.FixupInt32 fixupPos3 otyconsIdx2
    st.os.FixupInt32 fixupPos4 otyparsIdx1
    st.os.FixupInt32 fixupPos5 otyparsIdx2
    st.os.FixupInt32 fixupPos6 ovalsIdx1
    st.os.FixupInt32 fixupPos7 ovalsIdx2

let p_lazy p x st =
    p_lazy_impl p (Lazy.force x) st

let p_maybe_lazy p (x: MaybeLazy<_>) st =
    p_lazy_impl p x.Value st

let p_hole () =
    let mutable h = None
    (fun f -> h <- Some f), (fun x st -> match h with Some f -> f x st | None -> pfailwith st "p_hole: unfilled hole")

let p_hole2 () =
    let mutable h = None
    (fun f -> h <- Some f), (fun arg x st -> match h with Some f -> f arg x st | None -> pfailwith st "p_hole2: unfilled hole")

let u_array_core f n st =
    let res = Array.zeroCreate n
    for i = 0 to n-1 do
        res.[i] <- f st
    res

let u_array f st =
    let n = u_int st
    u_array_core f n st

// Optionally decode an extra item if a marker bit is present.
// When the marker bit is not set this is identical to u_array, and extraf is not called
let u_array_ext extraf f st =
    let n = u_int st
    let extraItem =
        if n &&& 0x80000000 = 0x80000000 then
            Some (extraf st)
        else
            None
    let arr = u_array_core f (n &&& 0x7FFFFFFF) st
    extraItem, arr

let u_list_core f n st =
    List.init n (fun _ -> f st)

let u_list f st =
    let n = u_int st
    u_list_core f n st
let u_list_ext extra f st =
    let n = u_int st
    let extraItem =
        if n &&& 0x80000000 = 0x80000000 then
            Some (extra st)
        else
            None
    let list = u_list_core f (n &&& 0x7FFFFFFF) st
    extraItem, list

#if FLAT_LIST_AS_LIST
#else
let u_List f st = u_list f st // new List<_> (u_array f st)
#endif
#if FLAT_LIST_AS_ARRAY_STRUCT
//#else
let u_List f st = List(u_array f st)
#endif
#if FLAT_LIST_AS_ARRAY
//#else
let u_List f st = u_array f st
#endif

let u_array_revi f st =
    let n = u_int st
    let res = Array.zeroCreate n
    for i = 0 to n-1 do
        res.[i] <- f st (n-1-i)
    res

// Mark up default constraints with a priority in reverse order: last gets 0 etc. See comment on TyparConstraint.DefaultsTo
let u_list_revi f st =
    let n = u_int st
    [ for i = 0 to n-1 do
         yield f st (n-1-i) ]


let u_wrap (f: 'U -> 'T) (u : 'U unpickler) : 'T unpickler = (fun st -> f (u st))

let u_option f st =
    let tag = u_byte st
    match tag with
    | 0 -> None
    | 1 -> Some (f st)
    | n -> ufailwith st ("u_option: found number " + string n)

// Boobytrap an OSGN node with a force of a lazy load of a bunch of pickled data
#if LAZY_UNPICKLE
let wire (x: osgn<_>) (res: Lazy<_>) =
    x.osgnTripWire <- Some(fun () -> res.Force() |> ignore)
#endif

let u_lazy u st =

    // Read the number of bytes in the record
    let len         = prim_u_int32 st // fixupPos1
    // These are the ranges of OSGN nodes defined within the lazily read portion of the graph
    let otyconsIdx1 = prim_u_int32 st // fixupPos2
    let otyconsIdx2 = prim_u_int32 st // fixupPos3
    let otyparsIdx1 = prim_u_int32 st // fixupPos4
    let otyparsIdx2 = prim_u_int32 st // fixupPos5
    let ovalsIdx1   = prim_u_int32 st // fixupPos6
    let ovalsIdx2   = prim_u_int32 st // fixupPos7

#if LAZY_UNPICKLE
    // Record the position in the bytestream to use when forcing the read of the data
    let idx1 = st.is.Position
    // Skip the length of data
    st.is.Skip len
    // This is the lazy computation that wil force the unpickling of the term.
    // This term must contain OSGN definitions of the given nodes.
    let res =
        lazy (let st = { st with is = st.is.CloneAndSeek idx1 }
              u st)
    /// Force the reading of the data as a "tripwire" for each of the OSGN thunks
    for i = otyconsIdx1 to otyconsIdx2-1 do wire (st.ientities.Get i) res done
    for i = ovalsIdx1   to ovalsIdx2-1   do wire (st.ivals.Get i)   res done
    for i = otyparsIdx1 to otyparsIdx2-1 do wire (st.itypars.Get i) res done
    res
#else
    ignore (len, otyconsIdx1, otyconsIdx2, otyparsIdx1, otyparsIdx2, ovalsIdx1, ovalsIdx2)
    Lazy.CreateFromValue(u st)
#endif


let u_hole () =
    let mutable h = None
    (fun f -> h <- Some f), (fun st -> match h with Some f -> f st | None -> ufailwith st "u_hole: unfilled hole")

//---------------------------------------------------------------------------
// Pickle/unpickle F# interface data
//---------------------------------------------------------------------------

// Strings
// A huge number of these occur in pickled F# data, so make them unique
let encode_string stringTab x = encode_uniq stringTab x
let decode_string x = x
let lookup_string st stringTab x = lookup_uniq st stringTab x
let u_encoded_string = u_prim_string
let u_string st   = lookup_uniq st st.istrings (u_int st)
let u_strings = u_list u_string
let u_ints = u_list u_int


let p_encoded_string = p_prim_string
let p_string s st = p_int (encode_string st.ostrings s) st
let p_strings = p_list p_string
let p_ints = p_list p_int

// CCU References
// A huge number of these occur in pickled F# data, so make them unique
let encode_ccuref ccuTab (x: CcuThunk) = encode_uniq ccuTab x.AssemblyName
let decode_ccuref x = x
let lookup_ccuref st ccuTab x = lookup_uniq st ccuTab x
let u_encoded_ccuref st =
    match u_byte st with
    | 0 -> u_prim_string st
    | n -> ufailwith st ("u_encoded_ccuref: found number " + string n)
let u_ccuref st   = lookup_uniq st st.iccus (u_int st)

let p_encoded_ccuref x st =
    p_byte 0 st // leave a dummy tag to make room for future encodings of ccurefs
    p_prim_string x st

let p_ccuref s st = p_int (encode_ccuref st.occus s) st

// References to public items in this module
// A huge number of these occur in pickled F# data, so make them unique
let decode_pubpath st stringTab a = PubPath(Array.map (lookup_string st stringTab) a)
let lookup_pubpath st pubpathTab x = lookup_uniq st pubpathTab x
let u_encoded_pubpath = u_array u_int
let u_pubpath st = lookup_uniq st st.ipubpaths (u_int st)

let encode_pubpath stringTab pubpathTab (PubPath a) = encode_uniq pubpathTab (Array.map (encode_string stringTab) a)
let p_encoded_pubpath = p_array p_int
let p_pubpath x st = p_int (encode_pubpath st.ostrings st.opubpaths x) st

// References to other modules
// A huge number of these occur in pickled F# data, so make them unique
let decode_nleref st ccuTab stringTab (a, b) = mkNonLocalEntityRef (lookup_ccuref st ccuTab a) (Array.map (lookup_string st stringTab) b)
let lookup_nleref st nlerefTab x = lookup_uniq st nlerefTab x
let u_encoded_nleref = u_tup2 u_int (u_array u_int)
let u_nleref st = lookup_uniq st st.inlerefs (u_int st)

let encode_nleref ccuTab stringTab nlerefTab thisCcu (nleref: NonLocalEntityRef) =
#if !NO_EXTENSIONTYPING
    // Remap references to statically-linked Entity nodes in provider-generated entities to point to the current assembly.
    // References to these nodes _do_ appear in F# assembly metadata, because they may be public.
    let nleref =
        match nleref.Deref.PublicPath with
        | Some pubpath when nleref.Deref.IsProvidedGeneratedTycon ->
            if verbose then dprintfn "remapping pickled reference to provider-generated type %s"  nleref.Deref.DisplayNameWithStaticParameters
            rescopePubPath thisCcu pubpath
        | _ -> nleref
#else
    ignore thisCcu
#endif

    let (NonLocalEntityRef(a, b)) = nleref
    encode_uniq nlerefTab (encode_ccuref ccuTab a, Array.map (encode_string stringTab) b)
let p_encoded_nleref = p_tup2 p_int (p_array p_int)
let p_nleref x st = p_int (encode_nleref st.occus st.ostrings st.onlerefs st.oscope x) st

// Simple types are types like "int", represented as TType(Ref_nonlocal(..., "int"), []).
// A huge number of these occur in pickled F# data, so make them unique.
let decode_simpletyp st _ccuTab _stringTab nlerefTab a = TType_app(ERefNonLocal (lookup_nleref st nlerefTab a), [])
let lookup_simpletyp st simpleTyTab x = lookup_uniq st simpleTyTab x
let u_encoded_simpletyp st = u_int  st
let u_encoded_anoninfo st = u_int  st
let u_simpletyp st = lookup_uniq st st.isimpletys (u_int st)
let encode_simpletyp ccuTab stringTab nlerefTab simpleTyTab thisCcu a = encode_uniq simpleTyTab (encode_nleref ccuTab stringTab nlerefTab thisCcu a)
let p_encoded_simpletyp x st = p_int x st
let p_encoded_anoninfo x st = p_int x st
let p_simpletyp x st = p_int (encode_simpletyp st.occus st.ostrings st.onlerefs st.osimpletys st.oscope x) st

/// Arbitrary value
[<Literal>]
let PickleBufferCapacity = 100000

let pickleObjWithDanglingCcus inMem file g scope p x =
  let st1 =
      { os = ByteBuffer.Create(PickleBufferCapacity, useArrayPool = true)
        oscope=scope
        occus= Table<_>.Create "occus"
        oentities=NodeOutTable<_, _>.Create((fun (tc: Tycon) -> tc.Stamp), (fun tc -> tc.LogicalName), (fun tc -> tc.Range), (fun osgn -> osgn), "otycons")
        otypars=NodeOutTable<_, _>.Create((fun (tp: Typar) -> tp.Stamp), (fun tp -> tp.DisplayName), (fun tp -> tp.Range), (fun osgn -> osgn), "otypars")
        ovals=NodeOutTable<_, _>.Create((fun (v: Val) -> v.Stamp), (fun v -> v.LogicalName), (fun v -> v.Range), (fun osgn -> osgn), "ovals")
        oanoninfos=NodeOutTable<_, _>.Create((fun (v: AnonRecdTypeInfo) -> v.Stamp), (fun v -> string v.Stamp), (fun _ -> range0), id, "oanoninfos")
        ostrings=Table<_>.Create "ostrings"
        onlerefs=Table<_>.Create "onlerefs"
        opubpaths=Table<_>.Create "opubpaths"
        osimpletys=Table<_>.Create "osimpletys"
        oglobals=g
        ofile=file
        oInMem=inMem
        isStructThisArgPos = false}
  let ccuNameTab, (ntycons, ntypars, nvals, nanoninfos), stringTab, pubpathTab, nlerefTab, simpleTyTab, phase1bytes =
    p x st1
    let sizes =
      st1.oentities.Size,
      st1.otypars.Size,
      st1.ovals.Size,
      st1.oanoninfos.Size
    st1.occus, sizes, st1.ostrings, st1.opubpaths, st1.onlerefs, st1.osimpletys, st1.os.AsMemory()

  let st2 =
   { os = ByteBuffer.Create(PickleBufferCapacity, useArrayPool = true)
     oscope=scope
     occus= Table<_>.Create "occus (fake)"
     oentities=NodeOutTable<_, _>.Create((fun (tc: Tycon) -> tc.Stamp), (fun tc -> tc.LogicalName), (fun tc -> tc.Range), (fun osgn -> osgn), "otycons")
     otypars=NodeOutTable<_, _>.Create((fun (tp: Typar) -> tp.Stamp), (fun tp -> tp.DisplayName), (fun tp -> tp.Range), (fun osgn -> osgn), "otypars")
     ovals=NodeOutTable<_, _>.Create((fun (v: Val) -> v.Stamp), (fun v -> v.LogicalName), (fun v -> v.Range), (fun osgn -> osgn), "ovals")
     oanoninfos=NodeOutTable<_, _>.Create((fun (v: AnonRecdTypeInfo) -> v.Stamp), (fun v -> string v.Stamp), (fun _ -> range0), id, "oanoninfos")
     ostrings=Table<_>.Create "ostrings (fake)"
     opubpaths=Table<_>.Create "opubpaths (fake)"
     onlerefs=Table<_>.Create "onlerefs (fake)"
     osimpletys=Table<_>.Create "osimpletys (fake)"
     oglobals=g
     ofile=file
     oInMem=inMem
     isStructThisArgPos = false }
  let phase2bytes =
    p_array p_encoded_ccuref ccuNameTab.AsArray st2
    // Add a 4th integer indicated by a negative 1st integer
    let z1 = if nanoninfos > 0 then  -ntycons-1 else ntycons
    p_int z1 st2
    p_tup2 p_int p_int (ntypars, nvals) st2
    if nanoninfos > 0 then
        p_int nanoninfos st2
    p_tup5
        (p_array p_encoded_string)
        (p_array p_encoded_pubpath)
        (p_array p_encoded_nleref)
        (p_array p_encoded_simpletyp)
        p_memory
        (stringTab.AsArray, pubpathTab.AsArray, nlerefTab.AsArray, simpleTyTab.AsArray, phase1bytes)
        st2
    st2.os

  let finalBytes = phase2bytes
  (st1.os :> System.IDisposable).Dispose()
  finalBytes

let check (ilscope: ILScopeRef) (inMap : NodeInTable<_, _>) =
    for i = 0 to inMap.Count - 1 do
      let n = inMap.Get i
      if not (inMap.IsLinked n) then
        warning(Error(FSComp.SR.pickleMissingDefinition (i, inMap.Name, ilscope.QualifiedName), range0))
        // Note for compiler developers: to get information about which item this index relates to,
        // enable the conditional in Pickle.p_osgn_ref to refer to the given index number and recompile
        // an identical copy of the source for the DLL containing the data being unpickled.  A message will
        // then be printed indicating the name of the item.

let unpickleObjWithDanglingCcus file viewedScope (ilModule: ILModuleDef option) u (phase2bytes: ReadOnlyByteMemory) =
    let st2 =
       { is = ByteStream.FromBytes (phase2bytes, 0, phase2bytes.Length)
         iilscope= viewedScope
         iccus= new_itbl "iccus (fake)" [| |]
         ientities= NodeInTable<_, _>.Create (Tycon.NewUnlinked, (fun osgn tg -> osgn.Link tg), (fun osgn -> osgn.IsLinked), "itycons", 0)
         itypars= NodeInTable<_, _>.Create (Typar.NewUnlinked, (fun osgn tg -> osgn.Link tg), (fun osgn -> osgn.IsLinked), "itypars", 0)
         ivals  = NodeInTable<_, _>.Create (Val.NewUnlinked, (fun osgn tg -> osgn.Link tg), (fun osgn -> osgn.IsLinked), "ivals", 0)
         ianoninfos=NodeInTable<_, _>.Create(AnonRecdTypeInfo.NewUnlinked, (fun osgn tg -> osgn.Link tg), (fun osgn -> osgn.IsLinked), "ianoninfos", 0)
         istrings = new_itbl "istrings (fake)" [| |]
         inlerefs = new_itbl "inlerefs (fake)" [| |]
         ipubpaths = new_itbl "ipubpaths (fake)" [| |]
         isimpletys = new_itbl "isimpletys (fake)" [| |]
         ifile=file
         iILModule = ilModule }
    let ccuNameTab = u_array u_encoded_ccuref st2
    let z1 = u_int st2
    let ntycons = if z1 < 0 then -z1-1 else z1
    let ntypars, nvals = u_tup2 u_int u_int st2
    let nanoninfos = if z1 < 0 then u_int st2 else 0
    let stringTab, pubpathTab, nlerefTab, simpleTyTab, phase1bytes =
        u_tup5
            (u_array u_encoded_string)
            (u_array u_encoded_pubpath)
            (u_array u_encoded_nleref)
            (u_array u_encoded_simpletyp)
            u_byte_memory
            st2
    let ccuTab       = new_itbl "iccus"       (Array.map (CcuThunk.CreateDelayed) ccuNameTab)
    let stringTab    = new_itbl "istrings"    (Array.map decode_string stringTab)
    let pubpathTab   = new_itbl "ipubpaths"   (Array.map (decode_pubpath st2 stringTab) pubpathTab)
    let nlerefTab    = new_itbl "inlerefs"    (Array.map (decode_nleref st2 ccuTab stringTab) nlerefTab)
    let simpletypTab = new_itbl "simpleTyTab" (Array.map (decode_simpletyp st2 ccuTab stringTab nlerefTab) simpleTyTab)
    let data =
        let st1 =
           { is = ByteStream.FromBytes (phase1bytes, 0, phase1bytes.Length)
             iccus=  ccuTab
             iilscope= viewedScope
             ientities= NodeInTable<_, _>.Create(Tycon.NewUnlinked, (fun osgn tg -> osgn.Link tg), (fun osgn -> osgn.IsLinked), "itycons", ntycons)
             itypars= NodeInTable<_, _>.Create(Typar.NewUnlinked, (fun osgn tg -> osgn.Link tg), (fun osgn -> osgn.IsLinked), "itypars", ntypars)
             ivals=   NodeInTable<_, _>.Create(Val.NewUnlinked, (fun osgn tg -> osgn.Link tg), (fun osgn -> osgn.IsLinked), "ivals", nvals)
             ianoninfos=NodeInTable<_, _>.Create(AnonRecdTypeInfo.NewUnlinked, (fun osgn tg -> osgn.Link tg), (fun osgn -> osgn.IsLinked), "ianoninfos", nanoninfos)
             istrings = stringTab
             ipubpaths = pubpathTab
             inlerefs = nlerefTab
             isimpletys = simpletypTab
             ifile=file
             iILModule = ilModule }
        let res = u st1
#if !LAZY_UNPICKLE
        check viewedScope st1.ientities
        check viewedScope st1.ivals
        check viewedScope st1.itypars
#endif
        res

    {RawData=data; FixupThunks=ccuTab.itbl_rows }


//=========================================================================
// PART II
//=========================================================================

//---------------------------------------------------------------------------
// Pickle/unpickle for Abstract IL data, up to IL instructions
//---------------------------------------------------------------------------

let p_ILPublicKey x st =
    match x with
    | PublicKey b      -> p_byte 0 st; p_bytes b st
    | PublicKeyToken b -> p_byte 1 st; p_bytes b st

let p_ILVersion (x: ILVersionInfo) st = p_tup4 p_uint16 p_uint16 p_uint16 p_uint16 (x.Major, x.Minor, x.Build, x.Revision) st

let p_ILModuleRef (x: ILModuleRef) st =
    p_tup3 p_string p_bool (p_option p_bytes) (x.Name, x.HasMetadata, x.Hash) st

let p_ILAssemblyRef (x: ILAssemblyRef) st =
    p_byte 0 st // leave a dummy tag to make room for future encodings of assembly refs
    p_tup6 p_string (p_option p_bytes) (p_option p_ILPublicKey) p_bool (p_option p_ILVersion) (p_option p_string)
      ( x.Name, x.Hash, x.PublicKey, x.Retargetable, x.Version, x.Locale) st

let p_ILScopeRef x st =
    match x with
    | ILScopeRef.Local           -> p_byte 0 st
    | ILScopeRef.Module mref     -> p_byte 1 st; p_ILModuleRef mref st
    | ILScopeRef.Assembly aref   -> p_byte 2 st; p_ILAssemblyRef aref st
    // Encode primary assembly as a normal assembly ref
    | ILScopeRef.PrimaryAssembly -> p_byte 2 st; p_ILAssemblyRef st.oglobals.ilg.primaryAssemblyRef st

let u_ILPublicKey st =
    let tag = u_byte st
    match tag with
    | 0 -> u_bytes st |> PublicKey
    | 1 -> u_bytes st |> PublicKeyToken
    | _ -> ufailwith st "u_ILPublicKey"

let u_ILVersion st =
    let (major, minor, build, revision) = u_tup4 u_uint16 u_uint16 u_uint16 u_uint16 st
    ILVersionInfo(major, minor, build, revision)

let u_ILModuleRef st =
    let (a, b, c) = u_tup3 u_string u_bool (u_option u_bytes) st
    ILModuleRef.Create(a, b, c)

let u_ILAssemblyRef st =
    let tag = u_byte st
    match tag with
    | 0 ->
        let a, b, c, d, e, f = u_tup6 u_string (u_option u_bytes) (u_option u_ILPublicKey) u_bool (u_option u_ILVersion) (u_option u_string) st
        ILAssemblyRef.Create(a, b, c, d, e, f)
    | _ -> ufailwith st "u_ILAssemblyRef"

// IL scope references are rescoped as they are unpickled.  This means
// the pickler accepts IL fragments containing ILScopeRef.Local and adjusts them
// to be absolute scope references.
let u_ILScopeRef st =
    let res =
        let tag = u_byte st
        match tag with
        | 0 -> ILScopeRef.Local
        | 1 -> u_ILModuleRef st |> ILScopeRef.Module
        | 2 -> u_ILAssemblyRef st |> ILScopeRef.Assembly
        | _ -> ufailwith st "u_ILScopeRef"
    let res = rescopeILScopeRef st.iilscope res
    res

let p_ILHasThis x st =
    p_byte (match x with
            | ILThisConvention.Instance -> 0
            | ILThisConvention.InstanceExplicit -> 1
            | ILThisConvention.Static -> 2) st

let p_ILArrayShape = p_wrap (fun (ILArrayShape x) -> x) (p_list (p_tup2 (p_option p_int32) (p_option p_int32)))

let rec p_ILType ty st =
    match ty with
    | ILType.Void                   -> p_byte 0 st
    | ILType.Array (shape, ty)       -> p_byte 1 st; p_tup2 p_ILArrayShape p_ILType (shape, ty) st
    | ILType.Value tspec            -> p_byte 2 st; p_ILTypeSpec tspec st
    | ILType.Boxed tspec            -> p_byte 3 st; p_ILTypeSpec tspec st
    | ILType.Ptr ty                 -> p_byte 4 st; p_ILType ty st
    | ILType.Byref ty               -> p_byte 5 st; p_ILType ty st
    | ILType.FunctionPointer csig   -> p_byte 6 st; p_ILCallSig csig st
    | ILType.TypeVar n              -> p_byte 7 st; p_uint16 n st
    | ILType.Modified (req, tref, ty) -> p_byte 8 st; p_tup3 p_bool p_ILTypeRef p_ILType (req, tref, ty) st

and p_ILTypes tys = p_list p_ILType tys

and p_ILBasicCallConv x st =
    p_byte (match x with
            | ILArgConvention.Default -> 0
            | ILArgConvention.CDecl  -> 1
            | ILArgConvention.StdCall -> 2
            | ILArgConvention.ThisCall -> 3
            | ILArgConvention.FastCall -> 4
            | ILArgConvention.VarArg -> 5) st

and p_ILCallConv (Callconv(x, y)) st = p_tup2 p_ILHasThis p_ILBasicCallConv (x, y) st

and p_ILCallSig x st = p_tup3 p_ILCallConv p_ILTypes p_ILType (x.CallingConv, x.ArgTypes, x.ReturnType) st

and p_ILTypeRef (x: ILTypeRef) st = p_tup3 p_ILScopeRef p_strings p_string (x.Scope, x.Enclosing, x.Name) st

and p_ILTypeSpec (a: ILTypeSpec) st = p_tup2 p_ILTypeRef p_ILTypes (a.TypeRef, a.GenericArgs) st

let u_ILBasicCallConv st =
    match u_byte st with
    | 0 -> ILArgConvention.Default
    | 1 -> ILArgConvention.CDecl
    | 2 -> ILArgConvention.StdCall
    | 3 -> ILArgConvention.ThisCall
    | 4 -> ILArgConvention.FastCall
    | 5 -> ILArgConvention.VarArg
    | _ -> ufailwith st "u_ILBasicCallConv"

let u_ILHasThis st =
    match u_byte st with
    | 0 -> ILThisConvention.Instance
    | 1 -> ILThisConvention.InstanceExplicit
    | 2 -> ILThisConvention.Static
    | _ -> ufailwith st "u_ILHasThis"

let u_ILCallConv st = let a, b = u_tup2 u_ILHasThis u_ILBasicCallConv st in Callconv(a, b)
let u_ILTypeRef st = let a, b, c = u_tup3 u_ILScopeRef u_strings u_string st in ILTypeRef.Create(a, b, c)
let u_ILArrayShape = u_wrap (fun x -> ILArrayShape x) (u_list (u_tup2 (u_option u_int32) (u_option u_int32)))


let rec u_ILType st =
    let tag = u_byte st
    match tag with
    | 0 -> ILType.Void
    | 1 -> u_tup2 u_ILArrayShape u_ILType  st     |> ILType.Array
    | 2 -> u_ILTypeSpec st                        |> ILType.Value
    | 3 -> u_ILTypeSpec st                        |> mkILBoxedType
    | 4 -> u_ILType st                            |> ILType.Ptr
    | 5 -> u_ILType st                            |> ILType.Byref
    | 6 -> u_ILCallSig st                         |> ILType.FunctionPointer
    | 7 -> u_uint16 st                            |> mkILTyvarTy
    | 8 -> u_tup3 u_bool u_ILTypeRef u_ILType  st |> ILType.Modified
    | _ -> ufailwith st "u_ILType"

and u_ILTypes st = u_list u_ILType st

and u_ILCallSig = u_wrap (fun (a, b, c) -> {CallingConv=a; ArgTypes=b; ReturnType=c}) (u_tup3 u_ILCallConv u_ILTypes u_ILType)

and u_ILTypeSpec st = let a, b = u_tup2 u_ILTypeRef u_ILTypes st in ILTypeSpec.Create(a, b)


let p_ILMethodRef (x: ILMethodRef) st = p_tup6 p_ILTypeRef p_ILCallConv p_int p_string p_ILTypes p_ILType (x.DeclaringTypeRef, x.CallingConv, x.GenericArity, x.Name, x.ArgTypes, x.ReturnType) st

let p_ILFieldRef (x: ILFieldRef) st = p_tup3 p_ILTypeRef p_string p_ILType (x.DeclaringTypeRef, x.Name, x.Type) st

let p_ILMethodSpec (x: ILMethodSpec) st = p_tup3 p_ILMethodRef p_ILType p_ILTypes (x.MethodRef, x.DeclaringType, x.GenericArgs) st

let p_ILFieldSpec (x : ILFieldSpec) st = p_tup2 p_ILFieldRef p_ILType (x.FieldRef, x.DeclaringType) st

let p_ILBasicType x st =
    p_int (match x with
           | DT_R   -> 0
           | DT_I1  -> 1
           | DT_U1  -> 2
           | DT_I2  -> 3
           | DT_U2  -> 4
           | DT_I4  -> 5
           | DT_U4  -> 6
           | DT_I8  -> 7
           | DT_U8  -> 8
           | DT_R4  -> 9
           | DT_R8  -> 10
           | DT_I   -> 11
           | DT_U   -> 12
           | DT_REF -> 13) st

let p_ILVolatility x st = p_int (match x with Volatile -> 0 | Nonvolatile -> 1) st
let p_ILReadonly   x st = p_int (match x with ReadonlyAddress -> 0 | NormalAddress -> 1) st

let u_ILMethodRef st =
    let x1, x2, x3, x4, x5, x6 = u_tup6 u_ILTypeRef u_ILCallConv u_int u_string u_ILTypes u_ILType st
    ILMethodRef.Create(x1, x2, x4, x3, x5, x6)

let u_ILFieldRef st =
    let x1, x2, x3 = u_tup3 u_ILTypeRef u_string u_ILType st
    {DeclaringTypeRef=x1;Name=x2;Type=x3}

let u_ILMethodSpec st =
    let x1, x2, x3 = u_tup3 u_ILMethodRef u_ILType u_ILTypes st
    ILMethodSpec.Create(x2, x1, x3)

let u_ILFieldSpec st =
    let x1, x2 = u_tup2 u_ILFieldRef u_ILType st
    {FieldRef=x1;DeclaringType=x2}

let u_ILBasicType st =
    match u_int st with
    | 0 -> DT_R
    | 1 -> DT_I1
    | 2 -> DT_U1
    | 3 -> DT_I2
    | 4 -> DT_U2
    | 5 -> DT_I4
    | 6 -> DT_U4
    | 7 -> DT_I8
    | 8 -> DT_U8
    | 9 -> DT_R4
    | 10 -> DT_R8
    | 11 -> DT_I
    | 12 -> DT_U
    | 13 -> DT_REF
    | _ -> ufailwith st "u_ILBasicType"

let u_ILVolatility st = (match u_int st with  0 -> Volatile | 1 -> Nonvolatile | _ -> ufailwith st "u_ILVolatility" )
let u_ILReadonly   st = (match u_int st with  0 -> ReadonlyAddress | 1 -> NormalAddress | _ -> ufailwith st "u_ILReadonly" )

let [<Literal>] itag_nop           = 0
let [<Literal>] itag_ldarg         = 1
let [<Literal>] itag_ldnull        = 2
let [<Literal>] itag_ilzero        = 3
let [<Literal>] itag_call          = 4
let [<Literal>] itag_add           = 5
let [<Literal>] itag_sub           = 6
let [<Literal>] itag_mul           = 7
let [<Literal>] itag_div           = 8
let [<Literal>] itag_div_un        = 9
let [<Literal>] itag_rem           = 10
let [<Literal>] itag_rem_un        = 11
let [<Literal>] itag_and           = 12
let [<Literal>] itag_or            = 13
let [<Literal>] itag_xor           = 14
let [<Literal>] itag_shl           = 15
let [<Literal>] itag_shr           = 16
let [<Literal>] itag_shr_un        = 17
let [<Literal>] itag_neg           = 18
let [<Literal>] itag_not           = 19
let [<Literal>] itag_conv          = 20
let [<Literal>] itag_conv_un       = 21
let [<Literal>] itag_conv_ovf      = 22
let [<Literal>] itag_conv_ovf_un   = 23
let [<Literal>] itag_callvirt      = 24
let [<Literal>] itag_ldobj         = 25
let [<Literal>] itag_ldstr         = 26
let [<Literal>] itag_castclass     = 27
let [<Literal>] itag_isinst        = 28
let [<Literal>] itag_unbox         = 29
let [<Literal>] itag_throw         = 30
let [<Literal>] itag_ldfld         = 31
let [<Literal>] itag_ldflda        = 32
let [<Literal>] itag_stfld         = 33
let [<Literal>] itag_ldsfld        = 34
let [<Literal>] itag_ldsflda       = 35
let [<Literal>] itag_stsfld        = 36
let [<Literal>] itag_stobj         = 37
let [<Literal>] itag_box           = 38
let [<Literal>] itag_newarr        = 39
let [<Literal>] itag_ldlen         = 40
let [<Literal>] itag_ldelema       = 41
let [<Literal>] itag_ckfinite      = 42
let [<Literal>] itag_ldtoken       = 43
let [<Literal>] itag_add_ovf       = 44
let [<Literal>] itag_add_ovf_un    = 45
let [<Literal>] itag_mul_ovf       = 46
let [<Literal>] itag_mul_ovf_un    = 47
let [<Literal>] itag_sub_ovf       = 48
let [<Literal>] itag_sub_ovf_un    = 49
let [<Literal>] itag_ceq           = 50
let [<Literal>] itag_cgt           = 51
let [<Literal>] itag_cgt_un        = 52
let [<Literal>] itag_clt           = 53
let [<Literal>] itag_clt_un        = 54
let [<Literal>] itag_ldvirtftn     = 55
let [<Literal>] itag_localloc      = 56
let [<Literal>] itag_rethrow       = 57
let [<Literal>] itag_sizeof        = 58
let [<Literal>] itag_ldelem_any    = 59
let [<Literal>] itag_stelem_any    = 60
let [<Literal>] itag_unbox_any     = 61
let [<Literal>] itag_ldlen_multi   = 62
let [<Literal>] itag_initobj       = 63   // currently unused, added for forward compat, see https://visualfsharp.codeplex.com/SourceControl/network/forks/jackpappas/fsharpcontrib/contribution/7134
let [<Literal>] itag_initblk       = 64   // currently unused, added for forward compat
let [<Literal>] itag_cpobj         = 65   // currently unused, added for forward compat
let [<Literal>] itag_cpblk         = 66   // currently unused, added for forward compat

let simple_instrs =
    [ itag_add, AI_add
      itag_add_ovf, AI_add_ovf
      itag_add_ovf_un, AI_add_ovf_un
      itag_and, AI_and
      itag_div, AI_div
      itag_div_un, AI_div_un
      itag_ceq, AI_ceq
      itag_cgt, AI_cgt
      itag_cgt_un, AI_cgt_un
      itag_clt, AI_clt
      itag_clt_un, AI_clt_un
      itag_mul, AI_mul
      itag_mul_ovf, AI_mul_ovf
      itag_mul_ovf_un, AI_mul_ovf_un
      itag_rem, AI_rem
      itag_rem_un, AI_rem_un
      itag_shl, AI_shl
      itag_shr, AI_shr
      itag_shr_un, AI_shr_un
      itag_sub, AI_sub
      itag_sub_ovf, AI_sub_ovf
      itag_sub_ovf_un, AI_sub_ovf_un
      itag_xor, AI_xor
      itag_or, AI_or
      itag_neg, AI_neg
      itag_not, AI_not
      itag_ldnull, AI_ldnull
      itag_ckfinite, AI_ckfinite
      itag_nop, AI_nop
      itag_localloc, I_localloc
      itag_throw, I_throw
      itag_ldlen, I_ldlen
      itag_rethrow, I_rethrow
      itag_rethrow, I_rethrow
      itag_initblk, I_initblk (Aligned, Nonvolatile)
      itag_cpblk, I_cpblk (Aligned, Nonvolatile)
    ]

let encode_table = Dictionary<_, _>(300, HashIdentity.Structural)
let _ = List.iter (fun (icode, i) -> encode_table.[i] <- icode) simple_instrs
let encode_instr si = encode_table.[si]
let isNoArgInstr s = encode_table.ContainsKey s

let decoders =
   [ itag_ldarg, u_uint16                            >> mkLdarg
     itag_call, u_ILMethodSpec                      >> (fun a -> I_call (Normalcall, a, None))
     itag_callvirt, u_ILMethodSpec                      >> (fun a -> I_callvirt (Normalcall, a, None))
     itag_ldvirtftn, u_ILMethodSpec                      >> I_ldvirtftn
     itag_conv, u_ILBasicType                       >> (fun a -> (AI_conv a))
     itag_conv_ovf, u_ILBasicType                       >> (fun a -> (AI_conv_ovf a))
     itag_conv_ovf_un, u_ILBasicType                       >> (fun a -> (AI_conv_ovf_un a))
     itag_ldfld, u_tup2 u_ILVolatility u_ILFieldSpec >> (fun (b, c) -> I_ldfld (Aligned, b, c))
     itag_ldflda, u_ILFieldSpec                       >> I_ldflda
     itag_ldsfld, u_tup2 u_ILVolatility u_ILFieldSpec >> (fun (a, b) -> I_ldsfld (a, b))
     itag_ldsflda, u_ILFieldSpec                       >> I_ldsflda
     itag_stfld, u_tup2 u_ILVolatility u_ILFieldSpec >> (fun (b, c) -> I_stfld (Aligned, b, c))
     itag_stsfld, u_tup2 u_ILVolatility u_ILFieldSpec >> (fun (a, b) -> I_stsfld (a, b))
     itag_ldtoken, u_ILType                            >> (fun a -> I_ldtoken (ILToken.ILType a))
     itag_ldstr, u_string                            >> I_ldstr
     itag_box, u_ILType                            >> I_box
     itag_unbox, u_ILType                            >> I_unbox
     itag_unbox_any, u_ILType                            >> I_unbox_any
     itag_newarr, u_tup2 u_ILArrayShape u_ILType      >> (fun (a, b) -> I_newarr(a, b))
     itag_stelem_any, u_tup2 u_ILArrayShape u_ILType      >> (fun (a, b) -> I_stelem_any(a, b))
     itag_ldelem_any, u_tup2 u_ILArrayShape u_ILType      >> (fun (a, b) -> I_ldelem_any(a, b))
     itag_ldelema, u_tup3 u_ILReadonly u_ILArrayShape u_ILType >> (fun (a, b, c) -> I_ldelema(a, false, b, c))
     itag_castclass, u_ILType                            >> I_castclass
     itag_isinst, u_ILType                            >> I_isinst
     itag_ldobj, u_ILType                            >> (fun c -> I_ldobj (Aligned, Nonvolatile, c))
     itag_stobj, u_ILType                            >> (fun c -> I_stobj (Aligned, Nonvolatile, c))
     itag_sizeof, u_ILType                            >> I_sizeof
     itag_ldlen_multi, u_tup2 u_int32 u_int32              >> (fun (a, b) -> EI_ldlen_multi (a, b))
     itag_ilzero, u_ILType                            >> EI_ilzero
     itag_ilzero, u_ILType                            >> EI_ilzero
     itag_initobj, u_ILType                            >> I_initobj
     itag_cpobj, u_ILType                            >> I_cpobj
   ]

let decode_tab =
    let tab = Array.init 256 (fun n -> (fun st -> ufailwith st ("no decoder for instruction "+string n)))
    let add_instr (icode, f) =  tab.[icode] <- f
    List.iter add_instr decoders
    List.iter (fun (icode, mk) -> add_instr (icode, (fun _ -> mk))) simple_instrs
    tab

let p_ILInstr x st =
    match x with
    | si when isNoArgInstr si         -> p_byte (encode_instr si) st
    | I_call(Normalcall, mspec, None)   -> p_byte itag_call st; p_ILMethodSpec mspec st
    | I_callvirt(Normalcall, mspec, None) -> p_byte itag_callvirt st;    p_ILMethodSpec mspec st
    | I_ldvirtftn mspec               -> p_byte itag_ldvirtftn st;   p_ILMethodSpec mspec st
    | I_ldarg x                       -> p_byte itag_ldarg st;       p_uint16 x st
    | AI_conv a                       -> p_byte itag_conv st;        p_ILBasicType a st
    | AI_conv_ovf a                   -> p_byte itag_conv_ovf st;    p_ILBasicType a st
    | AI_conv_ovf_un a                -> p_byte itag_conv_ovf_un st; p_ILBasicType a st
    | I_ldfld (Aligned, b, c)           -> p_byte itag_ldfld st;       p_tup2 p_ILVolatility p_ILFieldSpec (b, c) st
    | I_ldsfld (a, b)                  -> p_byte itag_ldsfld st;      p_tup2 p_ILVolatility p_ILFieldSpec (a, b) st
    | I_stfld (Aligned, b, c)           -> p_byte itag_stfld st;       p_tup2 p_ILVolatility p_ILFieldSpec (b, c) st
    | I_stsfld (a, b)                  -> p_byte itag_stsfld st;      p_tup2 p_ILVolatility p_ILFieldSpec (a, b) st
    | I_ldflda c                      -> p_byte itag_ldflda st;      p_ILFieldSpec c st
    | I_ldsflda a                     -> p_byte itag_ldsflda st;     p_ILFieldSpec a st
    | I_ldtoken (ILToken.ILType ty)   -> p_byte itag_ldtoken st;     p_ILType ty st
    | I_ldstr     s                   -> p_byte itag_ldstr st;       p_string s st
    | I_box  ty                       -> p_byte itag_box st;         p_ILType ty st
    | I_unbox  ty                     -> p_byte itag_unbox st;       p_ILType ty st
    | I_unbox_any  ty                 -> p_byte itag_unbox_any st;   p_ILType ty st
    | I_newarr (a, b)                  -> p_byte itag_newarr st;      p_tup2 p_ILArrayShape p_ILType (a, b) st
    | I_stelem_any (a, b)              -> p_byte itag_stelem_any st;  p_tup2 p_ILArrayShape p_ILType (a, b) st
    | I_ldelem_any (a, b)              -> p_byte itag_ldelem_any st;  p_tup2 p_ILArrayShape p_ILType (a, b) st
    | I_ldelema (a, _, b, c)             -> p_byte itag_ldelema st;     p_tup3 p_ILReadonly p_ILArrayShape p_ILType (a, b, c) st
    | I_castclass ty                  -> p_byte itag_castclass st;   p_ILType ty st
    | I_isinst  ty                    -> p_byte itag_isinst st;      p_ILType ty st
    | I_ldobj (Aligned, Nonvolatile, c) -> p_byte itag_ldobj st;       p_ILType c st
    | I_stobj (Aligned, Nonvolatile, c) -> p_byte itag_stobj st;       p_ILType c st
    | I_sizeof  ty                    -> p_byte itag_sizeof st;      p_ILType ty st
    | EI_ldlen_multi (n, m)            -> p_byte itag_ldlen_multi st; p_tup2 p_int32 p_int32 (n, m) st
    | EI_ilzero a                     -> p_byte itag_ilzero st;      p_ILType a st
    | I_initobj c                     -> p_byte itag_initobj st;     p_ILType c st
    | I_cpobj c                       -> p_byte itag_cpobj st;       p_ILType c st
    | i -> pfailwith st (sprintf "the IL instruction '%+A' cannot be emitted" i)

let u_ILInstr st =
    let n = u_byte st
    decode_tab.[n] st



//---------------------------------------------------------------------------
// Pickle/unpickle for F# types and module signatures
//---------------------------------------------------------------------------

let p_Map_core pk pv xs st =
    xs |> Map.iter (fun k v -> pk k st; pv v st)

let p_Map pk pv x st =
    p_int (Map.count x) st
    p_Map_core pk pv x st

let p_qlist pv = p_wrap QueueList.toList (p_list pv)
let p_namemap p = p_Map p_string p

let u_Map_core uk uv n st =
    Map.ofSeq (seq { for _ in 1..n -> (uk st, uv st) })

let u_Map uk uv st =
    let n = u_int st
    u_Map_core uk uv n st

let u_qlist uv = u_wrap QueueList.ofList (u_list uv)
let u_namemap u = u_Map u_string u

let p_pos (x: pos) st = p_tup2 p_int p_int (x.Line, x.Column) st

let p_range (x: range) st =
    let fileName = PathMap.apply st.oglobals.pathMap x.FileName
    p_tup3 p_string p_pos p_pos (fileName, x.Start, x.End) st

let p_dummy_range : range pickler   = fun _x _st -> ()
let p_ident (x: Ident) st = p_tup2 p_string p_range (x.idText, x.idRange) st
let p_xmldoc (doc: XmlDoc) st = p_array p_string doc.UnprocessedLines st

let u_pos st = let a = u_int st in let b = u_int st in mkPos a b
let u_range st = let a = u_string st in let b = u_pos st in let c = u_pos st in mkRange a b c

// Most ranges (e.g. on optimization expressions) can be elided from stored data
let u_dummy_range : range unpickler = fun _st -> range0
let u_ident st = let a = u_string st in let b = u_range st in ident(a, b)
let u_xmldoc st = XmlDoc (u_array u_string st, range0)

let p_local_item_ref ctxt tab st = p_osgn_ref ctxt tab st

let p_tcref ctxt (x: EntityRef) st =
    match x with
    | ERefLocal x -> p_byte 0 st; p_local_item_ref ctxt st.oentities x st
    | ERefNonLocal x -> p_byte 1 st; p_nleref x st

let p_ucref (UnionCaseRef(a, b)) st = p_tup2 (p_tcref "ucref") p_string (a, b) st
let p_rfref (RecdFieldRef(a, b)) st = p_tup2 (p_tcref "rfref") p_string (a, b) st
let p_tpref x st = p_local_item_ref "typar" st.otypars  x st

let u_local_item_ref tab st = u_osgn_ref tab st

let u_tcref st =
    let tag = u_byte st
    match tag with
    | 0 -> u_local_item_ref st.ientities  st |> ERefLocal
    | 1 -> u_nleref                     st |> ERefNonLocal
    | _ -> ufailwith st "u_item_ref"

let u_ucref st  = let a, b = u_tup2 u_tcref u_string st in UnionCaseRef(a, b)

let u_rfref st = let a, b = u_tup2 u_tcref u_string st in RecdFieldRef(a, b)

let u_tpref st = u_local_item_ref st.itypars st

// forward reference
let fill_p_ty2, p_ty2 = p_hole2()

let p_ty = p_ty2 false
let p_tys = (p_list p_ty)

let fill_p_attribs, p_attribs = p_hole()

// In F# 4.5, the type of the "this" pointer for structs is considered to be inref for the purposes of checking the implementation
// of the struct.  However for backwards compat reasons we can't serialize this as the type.
let checkForInRefStructThisArg st ty =
    let g = st.oglobals
    let _, tauTy = tryDestForallTy g ty
    isFunTy g tauTy && isFunTy g (rangeOfFunTy g tauTy) && isInByrefTy g (domainOfFunTy g tauTy)

let p_nonlocal_val_ref (nlv: NonLocalValOrMemberRef) st =
    let a = nlv.EnclosingEntity
    let key = nlv.ItemKey
    let pkey = key.PartialKey
    p_tcref "nlvref" a st
    p_option p_string pkey.MemberParentMangledName st
    p_bool pkey.MemberIsOverride st
    p_string pkey.LogicalName st
    p_int pkey.TotalArgCount st
    let isStructThisArgPos =
        match key.TypeForLinkage with
        | None -> false
        | Some ty -> checkForInRefStructThisArg st ty
    p_option (p_ty2 isStructThisArgPos) key.TypeForLinkage st

let rec p_vref ctxt x st =
    match x with
    | VRefLocal x    -> p_byte 0 st; p_local_item_ref ctxt st.ovals x st
    | VRefNonLocal x -> p_byte 1 st; p_nonlocal_val_ref x st

let p_vrefs ctxt = p_list (p_vref ctxt)

let fill_u_ty, u_ty = u_hole()
let u_tys = (u_list u_ty)
let fill_u_attribs, u_attribs = u_hole()

let u_nonlocal_val_ref st : NonLocalValOrMemberRef =
    let a = u_tcref st
    let b1 = u_option u_string st
    let b2 = u_bool st
    let b3 = u_string st
    let c = u_int st
    let d = u_option u_ty st
    { EnclosingEntity = a
      ItemKey=ValLinkageFullKey({ MemberParentMangledName=b1; MemberIsOverride=b2;LogicalName=b3; TotalArgCount=c }, d) }

let u_vref st =
    let tag = u_byte st
    match tag with
    | 0 -> u_local_item_ref st.ivals st |> (fun x -> VRefLocal x)
    | 1 -> u_nonlocal_val_ref st |> (fun x -> VRefNonLocal x)
    | _ -> ufailwith st "u_item_ref"

let u_vrefs = u_list u_vref

let p_kind x st =
    p_byte (match x with
            | TyparKind.Type -> 0
            | TyparKind.Measure -> 1) st

let p_member_kind x st =
    p_byte (match x with
            | SynMemberKind.Member -> 0
            | SynMemberKind.PropertyGet  -> 1
            | SynMemberKind.PropertySet -> 2
            | SynMemberKind.Constructor -> 3
            | SynMemberKind.ClassConstructor -> 4
            | SynMemberKind.PropertyGetSet -> pfailwith st "pickling: SynMemberKind.PropertyGetSet only expected in parse trees") st

let u_kind st =
    match u_byte st with
    | 0 -> TyparKind.Type
    | 1 -> TyparKind.Measure
    | _ -> ufailwith st "u_kind"

let u_member_kind st =
    match u_byte st with
    | 0 -> SynMemberKind.Member
    | 1 -> SynMemberKind.PropertyGet
    | 2 -> SynMemberKind.PropertySet
    | 3 -> SynMemberKind.Constructor
    | 4 -> SynMemberKind.ClassConstructor
    | _ -> ufailwith st "u_member_kind"

let p_MemberFlags (x: SynMemberFlags) st =
    p_tup6 p_bool p_bool p_bool p_bool p_bool p_member_kind
        (x.IsInstance,
         false (* _x3UnusedBoolInFormat *),
         x.IsDispatchSlot,
         x.IsOverrideOrExplicitImpl,
         x.IsFinal,
         x.MemberKind) st
let u_MemberFlags st : SynMemberFlags=
    let x2, _x3UnusedBoolInFormat, x4, x5, x6, x7 = u_tup6 u_bool u_bool u_bool u_bool u_bool u_member_kind st
    { IsInstance=x2
      IsDispatchSlot=x4
      IsOverrideOrExplicitImpl=x5
      IsFinal=x6
      MemberKind=x7}

let fill_u_Expr_hole, u_expr_fwd = u_hole()
let fill_p_Expr_hole, p_expr_fwd = p_hole()

let p_anonInfo_data (anonInfo: AnonRecdTypeInfo) st =
    p_tup3 p_ccuref p_bool (p_array p_ident) (anonInfo.Assembly, evalTupInfoIsStruct anonInfo.TupInfo, anonInfo.SortedIds) st

let p_anonInfo x st =
    p_osgn_decl st.oanoninfos p_anonInfo_data x st

let p_trait_sln sln st =
    match sln with
    | ILMethSln(a, b, c, d) ->
         p_byte 0 st; p_tup4 p_ty (p_option p_ILTypeRef) p_ILMethodRef p_tys (a, b, c, d) st
    | FSMethSln(a, b, c) ->
         p_byte 1 st; p_tup3 p_ty (p_vref "trait") p_tys (a, b, c) st
    | BuiltInSln ->
         p_byte 2 st
    | ClosedExprSln expr ->
         p_byte 3 st; p_expr_fwd expr st
    | FSRecdFieldSln(a, b, c) ->
         p_byte 4 st; p_tup3 p_tys p_rfref p_bool (a, b, c) st
    | FSAnonRecdFieldSln(a, b, c) ->
         p_byte 5 st; p_tup3 p_anonInfo p_tys p_int (a, b, c) st


let p_trait (TTrait(a, b, c, d, e, f)) st  =
    p_tup6 p_tys p_string p_MemberFlags p_tys (p_option p_ty) (p_option p_trait_sln) (a, b, c, d, e, !f) st

let u_anonInfo_data st =
    let (ccu, info, nms) = u_tup3 u_ccuref u_bool (u_array u_ident) st
    AnonRecdTypeInfo.Create (ccu, mkTupInfo info, nms)

let u_anonInfo st =
    u_osgn_decl st.ianoninfos u_anonInfo_data st

// We have to store trait solutions since they can occur in optimization data
let u_trait_sln st =
    let tag = u_byte st
    match tag with
    | 0 ->
        let (a, b, c, d) = u_tup4 u_ty (u_option u_ILTypeRef) u_ILMethodRef u_tys st
        ILMethSln(a, b, c, d)
    | 1 ->
        let (a, b, c) = u_tup3 u_ty u_vref u_tys st
        FSMethSln(a, b, c)
    | 2 ->
        BuiltInSln
    | 3 ->
        ClosedExprSln (u_expr_fwd st)
    | 4 ->
        let (a, b, c) = u_tup3 u_tys u_rfref u_bool st
        FSRecdFieldSln(a, b, c)
    | 5 ->
         let (a, b, c) = u_tup3 u_anonInfo u_tys u_int st
         FSAnonRecdFieldSln(a, b, c)
    | _ -> ufailwith st "u_trait_sln"

let u_trait st =
    let a, b, c, d, e, f = u_tup6 u_tys u_string u_MemberFlags u_tys (u_option u_ty) (u_option u_trait_sln) st
    TTrait (a, b, c, d, e, ref f)


let p_rational q st = p_int32 (GetNumerator q) st; p_int32 (GetDenominator q) st

let p_measure_con tcref st = p_byte 0 st; p_tcref "measure" tcref st

let p_measure_var v st = p_byte 3 st; p_tpref v st

let p_measure_one = p_byte 4

// Pickle a unit-of-measure variable or constructor
let p_measure_varcon unt st =
     match unt with
     | Measure.Con tcref   -> p_measure_con tcref st
     | Measure.Var v       -> p_measure_var v st
     | _                  -> pfailwith st ("p_measure_varcon: expected measure variable or constructor")

// Pickle a positive integer power of a unit-of-measure variable or constructor
let rec p_measure_pospower unt n st =
  if n = 1
  then p_measure_varcon unt st
  else p_byte 2 st; p_measure_varcon unt st; p_measure_pospower unt (n-1) st

// Pickle a non-zero integer power of a unit-of-measure variable or constructor
let p_measure_intpower unt n st =
  if n < 0
  then p_byte 1 st; p_measure_pospower unt (-n) st
  else p_measure_pospower unt n st

// Pickle a rational power of a unit-of-measure variable or constructor
let rec p_measure_power unt q st =
  if q = ZeroRational then p_measure_one st
  elif GetDenominator q = 1
  then p_measure_intpower unt (GetNumerator q) st
  else p_byte 5 st; p_measure_varcon unt st; p_rational q st

// Pickle a normalized unit-of-measure expression
// Normalized means of the form cv1 ^ q1 * ... * cvn ^ qn
// where q1, ..., qn are non-zero, and cv1, ..., cvn are distinct unit-of-measure variables or constructors
let rec p_normalized_measure unt st =
     let unt = stripUnitEqnsAux false unt
     match unt with
     | Measure.Con tcref   -> p_measure_con tcref st
     | Measure.Inv x       -> p_byte 1 st; p_normalized_measure x st
     | Measure.Prod(x1, x2) -> p_byte 2 st; p_normalized_measure x1 st; p_normalized_measure x2 st
     | Measure.Var v       -> p_measure_var v st
     | Measure.One         -> p_measure_one st
     | Measure.RationalPower(x, q) -> p_measure_power x q st

// By normalizing the unit-of-measure and treating integer powers as a special case,
// we ensure that the pickle format for rational powers of units (byte 5 followed by
// numerator and denominator) is used only when absolutely necessary, maintaining
// compatibility of formats with versions prior to F# 4.0.
//
// See https://github.com/Microsoft/visualfsharp/issues/69
let p_measure_expr unt st = p_normalized_measure (normalizeMeasure st.oglobals unt) st

let u_rational st =
  let a, b = u_tup2 u_int32 u_int32 st in DivRational (intToRational a) (intToRational b)

let rec u_measure_expr st =
    let tag = u_byte st
    match tag with
    | 0 -> let a = u_tcref st in Measure.Con a
    | 1 -> let a = u_measure_expr st in Measure.Inv a
    | 2 -> let a, b = u_tup2 u_measure_expr u_measure_expr st in Measure.Prod (a, b)
    | 3 -> let a = u_tpref st in Measure.Var a
    | 4 -> Measure.One
    | 5 -> let a = u_measure_expr st in let b = u_rational st in Measure.RationalPower (a, b)
    | _ -> ufailwith st "u_measure_expr"

let p_tyar_constraint x st =
    match x with
    | TyparConstraint.CoercesTo (a, _)               -> p_byte 0 st; p_ty a st
    | TyparConstraint.MayResolveMember(traitInfo, _) -> p_byte 1 st; p_trait traitInfo st
    | TyparConstraint.DefaultsTo(_, rty, _)           -> p_byte 2 st; p_ty rty st
    | TyparConstraint.SupportsNull _                -> p_byte 3 st
    | TyparConstraint.IsNonNullableStruct _         -> p_byte 4 st
    | TyparConstraint.IsReferenceType _             -> p_byte 5 st
    | TyparConstraint.RequiresDefaultConstructor _  -> p_byte 6 st
    | TyparConstraint.SimpleChoice(tys, _)           -> p_byte 7 st; p_tys tys st
    | TyparConstraint.IsEnum(ty, _)                  -> p_byte 8 st; p_ty ty st
    | TyparConstraint.IsDelegate(aty, bty, _)         -> p_byte 9 st; p_ty aty st; p_ty bty st
    | TyparConstraint.SupportsComparison _          -> p_byte 10 st
    | TyparConstraint.SupportsEquality _            -> p_byte 11 st
    | TyparConstraint.IsUnmanaged _                 -> p_byte 12 st
let p_tyar_constraints = (p_list p_tyar_constraint)

let u_tyar_constraint st =
    let tag = u_byte st
    match tag with
    | 0 -> u_ty  st             |> (fun a     _ -> TyparConstraint.CoercesTo (a, range0) )
    | 1 -> u_trait st            |> (fun a     _ -> TyparConstraint.MayResolveMember(a, range0))
    | 2 -> u_ty st              |> (fun a  ridx -> TyparConstraint.DefaultsTo(ridx, a, range0))
    | 3 ->                          (fun       _ -> TyparConstraint.SupportsNull range0)
    | 4 ->                          (fun       _ -> TyparConstraint.IsNonNullableStruct range0)
    | 5 ->                          (fun       _ -> TyparConstraint.IsReferenceType range0)
    | 6 ->                          (fun       _ -> TyparConstraint.RequiresDefaultConstructor range0)
    | 7 -> u_tys st             |> (fun a     _ -> TyparConstraint.SimpleChoice(a, range0))
    | 8 -> u_ty  st             |> (fun a     _ -> TyparConstraint.IsEnum(a, range0))
    | 9 -> u_tup2 u_ty u_ty st |> (fun (a, b) _ -> TyparConstraint.IsDelegate(a, b, range0))
    | 10 ->                         (fun       _ -> TyparConstraint.SupportsComparison range0)
    | 11 ->                         (fun       _ -> TyparConstraint.SupportsEquality range0)
    | 12 ->                         (fun       _ -> TyparConstraint.IsUnmanaged range0)
    | _ -> ufailwith st "u_tyar_constraint"


let u_tyar_constraints = (u_list_revi u_tyar_constraint)


let p_tyar_spec_data (x: Typar) st =
    p_tup5
      p_ident
      p_attribs
      p_int64
      p_tyar_constraints
      p_xmldoc
      (x.typar_id, x.Attribs, int64 x.typar_flags.PickledBits, x.Constraints, x.XmlDoc) st

let p_tyar_spec (x: Typar) st =
    //Disabled, workaround for bug 2721: if x.Rigidity <> TyparRigidity.Rigid then warning(Error(sprintf "p_tyar_spec: typar#%d is not rigid" x.Stamp, x.Range))
    if x.IsFromError then warning(Error((0, "p_tyar_spec: from error"), x.Range))
    p_osgn_decl st.otypars p_tyar_spec_data x st

let p_tyar_specs = (p_list p_tyar_spec)

let u_tyar_spec_data st =
    let a, c, d, e, g = u_tup5 u_ident u_attribs u_int64 u_tyar_constraints u_xmldoc st
    { typar_id=a
      typar_stamp=newStamp()
      typar_flags=TyparFlags(int32 d)
      typar_solution=None
      typar_astype= Unchecked.defaultof<_>
      typar_opt_data=
        match g, e, c with
        | doc, [], [] when doc.IsEmpty -> None
        | _ -> Some { typar_il_name = None; typar_xmldoc = g; typar_constraints = e; typar_attribs = c } }

let u_tyar_spec st =
    u_osgn_decl st.itypars u_tyar_spec_data st

let u_tyar_specs = (u_list u_tyar_spec)

let _ = fill_p_ty2 (fun isStructThisArgPos ty st ->
    let ty = stripTyparEqns ty

    // See comment on 'checkForInRefStructThisArg'
    let ty =
        if isInByrefTy st.oglobals ty && isStructThisArgPos then
            // Convert the inref to a byref
            mkByrefTy st.oglobals (destByrefTy st.oglobals ty)
        else
            ty

    match ty with
    | TType_tuple (tupInfo, l) ->
          if evalTupInfoIsStruct tupInfo then
              p_byte 8 st; p_tys l st
          else
              p_byte 0 st; p_tys l st
    | TType_app(ERefNonLocal nleref, []) -> p_byte 1 st; p_simpletyp nleref st
    | TType_app (tc, tinst)              -> p_byte 2 st; p_tup2 (p_tcref "typ") p_tys (tc, tinst) st
    | TType_fun (d, r)                   ->
        p_byte 3 st
        // Note, the "this" argument may be found in the domain position of a function type, so propagate the isStructThisArgPos value
        p_ty2 isStructThisArgPos d st
        p_ty r st
    | TType_var r                       -> p_byte 4 st; p_tpref r st
    | TType_forall (tps, r)              ->
        p_byte 5 st
        p_tyar_specs tps st
        // Note, the "this" argument may be found in the body of a generic forall type, so propagate the isStructThisArgPos value
        p_ty2 isStructThisArgPos r st
    | TType_measure unt                 -> p_byte 6 st; p_measure_expr unt st
    | TType_ucase (uc, tinst)            -> p_byte 7 st; p_tup2 p_ucref p_tys (uc, tinst) st
    // p_byte 8 taken by TType_tuple above
    | TType_anon (anonInfo, l) ->
         p_byte 9 st
         p_anonInfo anonInfo st
         p_tys l st)

let _ = fill_u_ty (fun st ->
    let tag = u_byte st
    match tag with
    | 0 -> let l = u_tys st                               in TType_tuple (tupInfoRef, l)
    | 1 -> u_simpletyp st
    | 2 -> let tc = u_tcref st in let tinst = u_tys st    in TType_app (tc, tinst)
    | 3 -> let d = u_ty st    in let r = u_ty st         in TType_fun (d, r)
    | 4 -> let r = u_tpref st                              in r.AsType
    | 5 -> let tps = u_tyar_specs st in let r = u_ty st  in TType_forall (tps, r)
    | 6 -> let unt = u_measure_expr st                     in TType_measure unt
    | 7 -> let uc = u_ucref st in let tinst = u_tys st    in TType_ucase (uc, tinst)
    | 8 -> let l = u_tys st                               in TType_tuple (tupInfoStruct, l)
    | 9 -> let anonInfo = u_anonInfo st in let l = u_tys st  in TType_anon (anonInfo, l)
    | _ -> ufailwith st "u_typ")


let fill_p_binds, p_binds = p_hole()
let fill_p_targets, p_targets = p_hole()
let fill_p_Exprs, p_Exprs = p_hole()
let fill_p_constraints, p_constraints = p_hole()
let fill_p_Vals, p_Vals = p_hole()

let fill_u_binds, u_binds = u_hole()
let fill_u_targets, u_targets = u_hole()
let fill_u_Exprs, u_Exprs = u_hole()
let fill_u_constraints, u_constraints = u_hole()
let fill_u_Vals, u_Vals = u_hole()

let p_ArgReprInfo (x: ArgReprInfo) st =
    p_attribs x.Attribs st
    p_option p_ident x.Name st

let p_TyparReprInfo (TyparReprInfo(a, b)) st =
    p_ident a st
    p_kind b st

let p_ValReprInfo (ValReprInfo (a, args, ret)) st =
    p_list p_TyparReprInfo a st
    p_list (p_list p_ArgReprInfo) args st
    p_ArgReprInfo ret st

let u_ArgReprInfo st =
    let a = u_attribs st
    let b = u_option u_ident st
    match a, b with
    | [], None -> ValReprInfo.unnamedTopArg1
    | _ -> { Attribs = a; Name = b }

let u_TyparReprInfo st =
    let a = u_ident st
    let b = u_kind st
    TyparReprInfo(a, b)

let u_ValReprInfo st =
    let a = u_list u_TyparReprInfo st
    let b = u_list (u_list u_ArgReprInfo) st
    let c = u_ArgReprInfo st
    ValReprInfo (a, b, c)

let p_ranges x st =
    p_option (p_tup2 p_range p_range) x st

let p_istype x st =
    match x with
    | FSharpModuleWithSuffix -> p_byte 0 st
    | ModuleOrType           -> p_byte 1 st
    | Namespace              -> p_byte 2 st

let p_cpath (CompPath(a, b)) st =
    p_tup2 p_ILScopeRef (p_list (p_tup2 p_string p_istype)) (a, b) st

let u_ranges st = u_option (u_tup2 u_range u_range) st

let u_istype st =
    let tag = u_byte st
    match tag with
    | 0 -> FSharpModuleWithSuffix
    | 1 -> ModuleOrType
    | 2 -> Namespace
    | _ -> ufailwith st "u_istype"

let u_cpath  st = let a, b = u_tup2 u_ILScopeRef (u_list (u_tup2 u_string u_istype)) st in (CompPath(a, b))


let rec dummy x = x
and p_tycon_repr x st =
    // The leading "p_byte 1" and "p_byte 0" come from the F# 2.0 format, which used an option value at this point.
    match x with
    | TRecdRepr fs         -> p_byte 1 st; p_byte 0 st; p_rfield_table fs st; false
    | TUnionRepr x         -> p_byte 1 st; p_byte 1 st; p_array p_unioncase_spec (x.CasesTable.CasesByIndex) st; false
    | TAsmRepr ilty        -> p_byte 1 st; p_byte 2 st; p_ILType ilty st; false
    | TFSharpObjectRepr r  -> p_byte 1 st; p_byte 3 st; p_tycon_objmodel_data r st; false
    | TMeasureableRepr ty  -> p_byte 1 st; p_byte 4 st; p_ty ty st; false
    | TNoRepr              -> p_byte 0 st; false
#if !NO_EXTENSIONTYPING
    | TProvidedTypeExtensionPoint info ->
        if info.IsErased then
            // Pickle erased type definitions as a NoRepr
            p_byte 0 st; false
        else
            // Pickle generated type definitions as a TAsmRepr
            p_byte 1 st; p_byte 2 st; p_ILType (mkILBoxedType(ILTypeSpec.Create(ExtensionTyping.GetILTypeRefOfProvidedType(info.ProvidedType, range0), []))) st; true
    | TProvidedNamespaceExtensionPoint _ -> p_byte 0 st; false
#endif
    | TILObjectRepr (TILObjectReprData (_, _, td)) -> error (Failure("Unexpected IL type definition"+td.Name))

and p_tycon_objmodel_data x st =
  p_tup3 p_tycon_objmodel_kind (p_vrefs "vslots") p_rfield_table
    (x.fsobjmodel_kind, x.fsobjmodel_vslots, x.fsobjmodel_rfields) st

and p_attribs_ext f x st = p_list_ext f p_attrib x st

and p_unioncase_spec x st =
    p_rfield_table x.FieldTable st
    p_ty x.ReturnType st
    p_string x.CompiledName st
    p_ident x.Id st
    // The XmlDoc are only written for the extended in-memory format. We encode their presence using a marker bit here
    p_attribs_ext (if st.oInMem then Some (p_xmldoc x.XmlDoc) else None)  x.Attribs st
    p_string x.XmlDocSig st
    p_access x.Accessibility st

and p_exnc_spec_data x st = p_entity_spec_data x st

and p_exnc_repr x st =
    match x with
    | TExnAbbrevRepr x -> p_byte 0 st; (p_tcref "exn abbrev") x st
    | TExnAsmRepr x    -> p_byte 1 st; p_ILTypeRef x st
    | TExnFresh x      -> p_byte 2 st; p_rfield_table x st
    | TExnNone         -> p_byte 3 st

and p_exnc_spec x st = p_entity_spec x st

and p_access (TAccess n) st = p_list p_cpath n st

and p_recdfield_spec x st =
    p_bool x.rfield_mutable st
    p_bool x.rfield_volatile st
    p_ty x.rfield_type st
    p_bool x.rfield_static st
    p_bool x.rfield_secret st
    p_option p_const x.rfield_const st
    p_ident x.rfield_id st
    p_attribs_ext (if st.oInMem then Some (p_xmldoc x.XmlDoc) else None) x.rfield_pattribs st
    p_attribs x.rfield_fattribs st
    p_string x.rfield_xmldocsig st
    p_access x.rfield_access st

and p_rfield_table x st =
    p_array p_recdfield_spec (x.FieldsByIndex) st

and p_entity_spec_data (x: Entity) st =
    p_tyar_specs (x.entity_typars.Force(x.entity_range)) st
    p_string x.entity_logical_name st
    p_option p_string x.EntityCompiledName st
    p_range  x.entity_range st
    p_option p_pubpath x.entity_pubpath st
    p_access x.Accessibility st
    p_access  x.TypeReprAccessibility st
    p_attribs x.entity_attribs st
    let flagBit = p_tycon_repr x.entity_tycon_repr st
    p_option p_ty x.TypeAbbrev st
    p_tcaug x.entity_tycon_tcaug st
    p_string System.String.Empty st
    p_kind x.TypeOrMeasureKind st
    p_int64 (x.entity_flags.PickledBits ||| (if flagBit then EntityFlags.ReservedBitForPickleFormatTyconReprFlag else 0L)) st
    p_option p_cpath x.entity_cpath st
    p_maybe_lazy p_modul_typ x.entity_modul_contents st
    p_exnc_repr x.ExceptionInfo st
    if st.oInMem then
        p_used_space1 (p_xmldoc x.XmlDoc) st
    else
        p_space 1 () st


and p_tcaug p st =
    p_tup9
      (p_option (p_tup2 (p_vref "compare_obj") (p_vref "compare")))
      (p_option (p_vref "compare_withc"))
      (p_option (p_tup3 (p_vref "hash_obj") (p_vref "hash_withc") (p_vref "equals_withc")))
      (p_option (p_tup2 (p_vref "hash") (p_vref "equals")))
      (p_list (p_tup2 p_string (p_vref "adhoc")))
      (p_list (p_tup3 p_ty p_bool p_dummy_range))
      (p_option p_ty)
      p_bool
      (p_space 1)
      (p.tcaug_compare,
       p.tcaug_compare_withc,
       p.tcaug_hash_and_equals_withc,
       p.tcaug_equals,
       (p.tcaug_adhoc_list
           |> ResizeArray.toList
           // Explicit impls of interfaces only get kept in the adhoc list
           // in order to get check the well-formedness of an interface.
           // Keeping them across assembly boundaries is not valid, because relinking their ValRefs
           // does not work correctly (they may get incorrectly relinked to a default member)
           |> List.filter (fun (isExplicitImpl, _) -> not isExplicitImpl)
           |> List.map (fun (_, vref) -> vref.LogicalName, vref)),
       p.tcaug_interfaces,
       p.tcaug_super,
       p.tcaug_abstract,
       space) st

and p_entity_spec x st = p_osgn_decl st.oentities p_entity_spec_data x st

and p_parentref x st =
    match x with
    | ParentNone -> p_byte 0 st
    | Parent x -> p_byte 1 st; p_tcref "parent tycon" x st

and p_attribkind x st =
    match x with
    | ILAttrib x -> p_byte 0 st; p_ILMethodRef x st
    | FSAttrib x -> p_byte 1 st; p_vref "attrib" x st

and p_attrib (Attrib (a, b, c, d, e, _targets, f)) st = // AttributeTargets are not preserved
    p_tup6 (p_tcref "attrib") p_attribkind (p_list p_attrib_expr) (p_list p_attrib_arg) p_bool p_dummy_range (a, b, c, d, e, f) st

and p_attrib_expr (AttribExpr(e1, e2)) st =
    p_tup2 p_expr p_expr (e1, e2) st

and p_attrib_arg (AttribNamedArg(a, b, c, d)) st =
    p_tup4 p_string p_ty p_bool p_attrib_expr (a, b, c, d) st

and p_member_info (x: ValMemberInfo) st =
    p_tup4 (p_tcref "member_info")  p_MemberFlags (p_list p_slotsig) p_bool
        (x.ApparentEnclosingEntity, x.MemberFlags, x.ImplementedSlotSigs, x.IsImplemented) st

and p_tycon_objmodel_kind x st =
    match x with
    | TTyconClass       -> p_byte 0 st
    | TTyconInterface   -> p_byte 1 st
    | TTyconStruct      -> p_byte 2 st
    | TTyconDelegate ss -> p_byte 3 st; p_slotsig ss st
    | TTyconEnum        -> p_byte 4 st

and p_mustinline x st =
    p_byte (match x with
            | ValInline.PseudoVal -> 0
            | ValInline.Always  -> 1
            | ValInline.Optional -> 2
            | ValInline.Never -> 3) st

and p_basethis x st =
    p_byte (match x with
            | BaseVal -> 0
            | CtorThisVal  -> 1
            | NormalVal -> 2
            | MemberThisVal -> 3) st

and p_vrefFlags x st =
    match x with
    | NormalValUse -> p_byte 0 st
    | CtorValUsedAsSuperInit  -> p_byte 1 st
    | CtorValUsedAsSelfInit  -> p_byte 2 st
    | PossibleConstrainedCall ty  -> p_byte 3 st; p_ty ty st
    | VSlotDirectCall -> p_byte 4 st

and p_ValData x st =
    p_string x.val_logical_name st
    p_option p_string x.ValCompiledName st
    // only keep range information on published values, not on optimization data
    p_ranges (x.ValReprInfo |> Option.map (fun _ -> x.val_range, x.DefinitionRange)) st

    let isStructThisArgPos = x.IsMember && checkForInRefStructThisArg st x.Type
    p_ty2 isStructThisArgPos x.val_type st

    p_int64 x.val_flags.PickledBits st
    p_option p_member_info x.MemberInfo st
    p_attribs x.Attribs st
    p_option p_ValReprInfo x.ValReprInfo st
    p_string x.XmlDocSig st
    p_access x.Accessibility st
    p_parentref x.DeclaringEntity st
    p_option p_const x.LiteralValue st
    if st.oInMem then
        p_used_space1 (p_xmldoc x.XmlDoc) st
    else
        p_space 1 () st

and p_Val x st =
    p_osgn_decl st.ovals p_ValData x st

and p_modul_typ (x: ModuleOrNamespaceType) st =
    p_tup3
      p_istype
      (p_qlist p_Val)
      (p_qlist p_entity_spec)
      (x.ModuleOrNamespaceKind, x.AllValsAndMembers, x.AllEntities)
      st

and u_tycon_repr st =
    let tag1 = u_byte st
    match tag1 with
    | 0 -> (fun _flagBit -> TNoRepr)
    | 1 ->
        let tag2 = u_byte st
        match tag2 with
        | 0 ->
            let v = u_rfield_table st
            (fun _flagBit -> TRecdRepr v)
        | 1 ->
            let v = u_list u_unioncase_spec  st
            (fun _flagBit -> Construct.MakeUnionRepr v)
        | 2 ->
            let v = u_ILType st
            // This is the F# 3.0 extension to the format used for F# provider-generated types, which record an ILTypeRef in the format
            // You can think of an F# 2.0 reader as always taking the path where 'flagBit' is false. Thus the F# 2.0 reader will
            // interpret provider-generated types as TAsmRepr.
            (fun flagBit ->
                if flagBit then
                    let iltref = v.TypeRef
                    match st.iILModule with
                    | None -> TNoRepr
                    | Some iILModule ->
                    try
                        let rec find acc enclosingTypeNames (tdefs: ILTypeDefs) =
                            match enclosingTypeNames with
                            | [] -> List.rev acc, tdefs.FindByName iltref.Name
                            | h :: t ->
                                let nestedTypeDef = tdefs.FindByName h
                                find (tdefs.FindByName h :: acc) t nestedTypeDef.NestedTypes
                        let nestedILTypeDefs, ilTypeDef = find [] iltref.Enclosing iILModule.TypeDefs
                        TILObjectRepr(TILObjectReprData(st.iilscope, nestedILTypeDefs, ilTypeDef))
                    with _ ->
                        System.Diagnostics.Debug.Assert(false, sprintf "failed to find IL backing metadata for cross-assembly generated type %s" iltref.FullName)
                        TNoRepr
                else
                    TAsmRepr v)
        | 3 ->
            let v = u_tycon_objmodel_data  st
            (fun _flagBit -> TFSharpObjectRepr v)
        | 4 ->
            let v = u_ty st
            (fun _flagBit -> TMeasureableRepr v)
        | _ -> ufailwith st "u_tycon_repr"
    | _ -> ufailwith st "u_tycon_repr"

and u_tycon_objmodel_data st =
    let x1, x2, x3 = u_tup3 u_tycon_objmodel_kind u_vrefs u_rfield_table st
    {fsobjmodel_kind=x1; fsobjmodel_vslots=x2; fsobjmodel_rfields=x3 }

and u_attribs_ext extraf st = u_list_ext extraf u_attrib st
and u_unioncase_spec st =
    let a = u_rfield_table  st
    let b = u_ty st

    // The union case compiled name is now computed from Id field when needed and is not stored in UnionCase record.
    let _c = u_string st
    let d = u_ident  st
    // The XmlDoc is only present in the extended in-memory format. We detect its presence using a marker bit here
    let xmldoc, e = u_attribs_ext u_xmldoc st
    let f = u_string st
    let i = u_access st
    { FieldTable=a
      ReturnType=b
      Id=d
      Attribs=e
      XmlDoc= defaultArg xmldoc XmlDoc.Empty
      XmlDocSig=f
      Accessibility=i
      OtherRangeOpt=None }

and u_exnc_spec_data st = u_entity_spec_data st

and u_exnc_repr st =
    let tag = u_byte st
    match tag with
    | 0 -> u_tcref        st |> TExnAbbrevRepr
    | 1 -> u_ILTypeRef    st |> TExnAsmRepr
    | 2 -> u_rfield_table st |> TExnFresh
    | 3 -> TExnNone
    | _ -> ufailwith st "u_exnc_repr"

and u_exnc_spec st = u_entity_spec st

and u_access st =
    match u_list u_cpath st with
    | [] -> taccessPublic // save unnecessary allocations
    | res -> TAccess res

and u_recdfield_spec st =
    let a = u_bool st
    let b = u_bool st
    let c1 = u_ty st
    let c2 = u_bool st
    let c2b = u_bool st
    let c3 = u_option u_const st
    let d = u_ident st
    // The XmlDoc is only present in the extended in-memory format. We detect its presence using a marker bit here
    let xmldoc, e1 = u_attribs_ext u_xmldoc st
    let e2 = u_attribs st
    let f = u_string st
    let g = u_access st
    { rfield_mutable=a
      rfield_volatile=b
      rfield_type=c1
      rfield_static=c2
      rfield_secret=c2b
      rfield_const=c3
      rfield_id=d
      rfield_pattribs=e1
      rfield_fattribs=e2
      rfield_xmldoc= defaultArg xmldoc XmlDoc.Empty
      rfield_xmldocsig=f
      rfield_access=g
      rfield_name_generated = false
      rfield_other_range = None }

and u_rfield_table st = Construct.MakeRecdFieldsTable (u_list u_recdfield_spec st)

and u_entity_spec_data st : Entity =
    let x1, x2a, x2b, x2c, x3, (x4a, x4b), x6, x7f, x8, x9, _x10, x10b, x11, x12, x13, x14, x15 =
       u_tup17
          u_tyar_specs
          u_string
          (u_option u_string)
          u_range
          (u_option u_pubpath)
          (u_tup2 u_access u_access)
          u_attribs
          u_tycon_repr
          (u_option u_ty)
          u_tcaug
          u_string
          u_kind
          u_int64
          (u_option u_cpath )
          (u_lazy u_modul_typ)
          u_exnc_repr
          (u_used_space1 u_xmldoc)
          st
    // We use a bit that was unused in the F# 2.0 format to indicate two possible representations in the F# 3.0 tycon_repr format
    let x7 = x7f (x11 &&& EntityFlags.ReservedBitForPickleFormatTyconReprFlag <> 0L)
    let x11 = x11 &&& ~~~EntityFlags.ReservedBitForPickleFormatTyconReprFlag

    { entity_typars=LazyWithContext.NotLazy x1
      entity_stamp=newStamp()
      entity_logical_name=x2a
      entity_range=x2c
      entity_pubpath=x3
      entity_attribs=x6
      entity_tycon_repr=x7
      entity_tycon_tcaug=x9
      entity_flags=EntityFlags x11
      entity_cpath=x12
      entity_modul_contents=MaybeLazy.Lazy x13
      entity_il_repr_cache=newCache()
      entity_opt_data=
        match x2b, x10b, x15, x8, x4a, x4b, x14 with
        | None, TyparKind.Type, None, None, TAccess [], TAccess [], TExnNone -> None
        | _ ->
            Some { Entity.NewEmptyEntityOptData() with
                       entity_compiled_name = x2b
                       entity_kind = x10b
                       entity_xmldoc= defaultArg x15 XmlDoc.Empty
                       entity_xmldocsig = System.String.Empty
                       entity_tycon_abbrev = x8
                       entity_accessibility = x4a
                       entity_tycon_repr_accessibility = x4b
                       entity_exn_info = x14 }
    }

and u_tcaug st =
    let a1, a2, a3, b2, c, d, e, g, _space =
      u_tup9
        (u_option (u_tup2 u_vref u_vref))
        (u_option u_vref)
        (u_option (u_tup3 u_vref u_vref u_vref))
        (u_option (u_tup2 u_vref u_vref))
        (u_list (u_tup2 u_string u_vref))
        (u_list (u_tup3 u_ty u_bool u_dummy_range))
        (u_option u_ty)
        u_bool
        (u_space 1)
        st
    {tcaug_compare=a1
     tcaug_compare_withc=a2
     tcaug_hash_and_equals_withc=a3
     tcaug_equals=b2
     // only used for code generation and checking - hence don't care about the values when reading back in
     tcaug_hasObjectGetHashCode=false
     tcaug_adhoc_list= new ResizeArray<_> (c |> List.map (fun (_, vref) -> (false, vref)))
     tcaug_adhoc=NameMultiMap.ofList c
     tcaug_interfaces=d
     tcaug_super=e
     // pickled type definitions are always closed (i.e. no more intrinsic members allowed)
     tcaug_closed=true
     tcaug_abstract=g}

and u_entity_spec st =
    u_osgn_decl st.ientities u_entity_spec_data st

and u_parentref st =
    let tag = u_byte st
    match tag with
    | 0 -> ParentNone
    | 1 -> u_tcref st |> Parent
    | _ -> ufailwith st "u_attribkind"

and u_attribkind st =
    let tag = u_byte st
    match tag with
    | 0 -> u_ILMethodRef st |> ILAttrib
    | 1 -> u_vref        st |> FSAttrib
    | _ -> ufailwith st "u_attribkind"

and u_attrib st : Attrib =
    let a, b, c, d, e, f = u_tup6 u_tcref u_attribkind (u_list u_attrib_expr) (u_list u_attrib_arg) u_bool u_dummy_range st
    Attrib(a, b, c, d, e, None, f)  // AttributeTargets are not preserved

and u_attrib_expr st =
    let a, b = u_tup2 u_expr u_expr st
    AttribExpr(a, b)

and u_attrib_arg st  =
    let a, b, c, d = u_tup4 u_string u_ty u_bool u_attrib_expr st
    AttribNamedArg(a, b, c, d)

and u_member_info st : ValMemberInfo =
    let x2, x3, x4, x5 = u_tup4 u_tcref u_MemberFlags (u_list u_slotsig) u_bool st
    { ApparentEnclosingEntity=x2
      MemberFlags=x3
      ImplementedSlotSigs=x4
      IsImplemented=x5  }

and u_tycon_objmodel_kind st =
    let tag = u_byte st
    match tag with
    | 0 -> TTyconClass
    | 1 -> TTyconInterface
    | 2 -> TTyconStruct
    | 3 -> u_slotsig st |> TTyconDelegate
    | 4 -> TTyconEnum
    | _ -> ufailwith st "u_tycon_objmodel_kind"

and u_mustinline st =
    match u_byte st with
    | 0 -> ValInline.PseudoVal
    | 1 -> ValInline.Always
    | 2 -> ValInline.Optional
    | 3 -> ValInline.Never
    | _ -> ufailwith st "u_mustinline"

and u_basethis st =
    match u_byte st with
    | 0 -> BaseVal
    | 1 -> CtorThisVal
    | 2 -> NormalVal
    | 3 -> MemberThisVal
    | _ -> ufailwith st "u_basethis"

and u_vrefFlags st =
    match u_byte st with
    | 0 -> NormalValUse
    | 1 -> CtorValUsedAsSuperInit
    | 2 -> CtorValUsedAsSelfInit
    | 3 -> PossibleConstrainedCall (u_ty st)
    | 4 -> VSlotDirectCall
    | _ -> ufailwith st "u_vrefFlags"

and u_ValData st =
    let x1, x1z, x1a, x2, x4, x8, x9, x10, x12, x13, x13b, x14, x15 =
      u_tup13
        u_string
        (u_option u_string)
        u_ranges
        u_ty
        u_int64
        (u_option u_member_info)
        u_attribs
        (u_option u_ValReprInfo)
        u_string
        u_access
        u_parentref
        (u_option u_const)
        (u_used_space1 u_xmldoc)
        st

    { val_logical_name = x1
      val_range        = (match x1a with None -> range0 | Some(a, _) -> a)
      val_type         = x2
      val_stamp        = newStamp()
      val_flags        = ValFlags x4
      val_opt_data     =
          match x1z, x1a, x10, x14, x13, x15, x8, x13b, x12, x9 with
          | None, None, None, None, TAccess [], None, None, ParentNone, "", [] -> None
          | _ ->
              Some { val_compiled_name    = x1z
                     val_other_range      = (match x1a with None -> None | Some(_, b) -> Some(b, true))
                     val_defn             = None
                     val_repr_info        = x10
                     val_const            = x14
                     val_access           = x13
                     val_xmldoc           = defaultArg x15 XmlDoc.Empty
                     val_member_info      = x8
                     val_declaring_entity = x13b
                     val_xmldocsig        = x12
                     val_attribs          = x9 }
    }

and u_Val st = u_osgn_decl st.ivals u_ValData st


and u_modul_typ st =
    let x1, x3, x5 =
        u_tup3
          u_istype
          (u_qlist u_Val)
          (u_qlist u_entity_spec) st
    ModuleOrNamespaceType(x1, x3, x5)


//---------------------------------------------------------------------------
// Pickle/unpickle for F# expressions (for optimization data)
//---------------------------------------------------------------------------

and p_const x st =
    match x with
    | Const.Bool x    -> p_byte 0  st; p_bool x st
    | Const.SByte x   -> p_byte 1  st; p_int8 x st
    | Const.Byte x    -> p_byte 2  st; p_uint8 x st
    | Const.Int16 x   -> p_byte 3  st; p_int16 x st
    | Const.UInt16 x  -> p_byte 4  st; p_uint16 x st
    | Const.Int32 x   -> p_byte 5  st; p_int32 x st
    | Const.UInt32 x  -> p_byte 6  st; p_uint32 x st
    | Const.Int64 x   -> p_byte 7  st; p_int64 x st
    | Const.UInt64 x  -> p_byte 8  st; p_uint64 x st
    | Const.IntPtr x  -> p_byte 9  st; p_int64 x st
    | Const.UIntPtr x -> p_byte 10 st; p_uint64 x st
    | Const.Single x  -> p_byte 11 st; p_single x st
    | Const.Double x  -> p_byte 12 st; p_int64 (bits_of_float x) st
    | Const.Char c    -> p_byte 13 st; p_char c st
    | Const.String s  -> p_byte 14 st; p_string s st
    | Const.Unit      -> p_byte 15 st
    | Const.Zero      -> p_byte 16 st
    | Const.Decimal s -> p_byte 17 st; p_array p_int32 (System.Decimal.GetBits s) st

and u_const st =
    let tag = u_byte st
    match tag with
    | 0 -> u_bool st           |> Const.Bool
    | 1 -> u_int8 st           |> Const.SByte
    | 2 -> u_uint8 st          |> Const.Byte
    | 3 -> u_int16 st          |> Const.Int16
    | 4 -> u_uint16 st         |> Const.UInt16
    | 5 -> u_int32 st          |> Const.Int32
    | 6 -> u_uint32 st         |> Const.UInt32
    | 7 -> u_int64 st          |> Const.Int64
    | 8 -> u_uint64 st         |> Const.UInt64
    | 9 -> u_int64 st          |> Const.IntPtr
    | 10 -> u_uint64 st        |> Const.UIntPtr
    | 11 -> u_single st        |> Const.Single
    | 12 -> u_int64 st         |> float_of_bits |> Const.Double
    | 13 -> u_char st          |> Const.Char
    | 14 -> u_string st        |> Const.String
    | 15 -> Const.Unit
    | 16 -> Const.Zero
    | 17 -> u_array u_int32 st |> (fun bits -> Const.Decimal (System.Decimal bits))
    | _ -> ufailwith st "u_const"


and p_dtree x st =
    match x with
    | TDSwitch (a, b, c, d) -> p_byte 0 st; p_tup4 p_expr (p_list p_dtree_case) (p_option p_dtree) p_dummy_range (a, b, c, d) st
    | TDSuccess (a, b)    -> p_byte 1 st; p_tup2 p_Exprs p_int (a, b) st
    | TDBind (a, b)       -> p_byte 2 st; p_tup2 p_bind p_dtree (a, b) st

and p_dtree_case (TCase(a, b)) st = p_tup2 p_dtree_discrim p_dtree (a, b) st

and p_dtree_discrim x st =
    match x with
    | DecisionTreeTest.UnionCase (ucref, tinst) -> p_byte 0 st; p_tup2 p_ucref p_tys (ucref, tinst) st
    | DecisionTreeTest.Const c                   -> p_byte 1 st; p_const c st
    | DecisionTreeTest.IsNull                    -> p_byte 2 st
    | DecisionTreeTest.IsInst (srcty, tgty)       -> p_byte 3 st; p_ty srcty st; p_ty tgty st
    | DecisionTreeTest.ArrayLength (n, ty)       -> p_byte 4 st; p_tup2 p_int p_ty (n, ty) st
    | DecisionTreeTest.ActivePatternCase _ -> pfailwith st "DecisionTreeTest.ActivePatternCase: only used during pattern match compilation"
    | DecisionTreeTest.Error _ -> pfailwith st "DecisionTreeTest.Error: only used during pattern match compilation"

and p_target (TTarget(a, b, _)) st = p_tup2 p_Vals p_expr (a, b) st
and p_bind (TBind(a, b, _)) st = p_tup2 p_Val p_expr (a, b) st

and p_lval_op_kind x st =
    p_byte (match x with LAddrOf _ -> 0 | LByrefGet -> 1 | LSet -> 2 | LByrefSet -> 3) st

and p_recdInfo x st =
    match x with
    | RecdExpr -> ()
    | RecdExprIsObjInit -> pfailwith st "explicit object constructors can't be inlined and should not have optimization information"

and u_dtree st =
    let tag = u_byte st
    match tag with
    | 0 -> u_tup4 u_expr (u_list u_dtree_case) (u_option u_dtree) u_dummy_range st |> TDSwitch
    | 1 -> u_tup2 u_Exprs u_int                                             st |> TDSuccess
    | 2 -> u_tup2 u_bind u_dtree                                                st |> TDBind
    | _ -> ufailwith st "u_dtree"

and u_dtree_case st = let a, b = u_tup2 u_dtree_discrim u_dtree st in (TCase(a, b))

and u_dtree_discrim st =
    let tag = u_byte st
    match tag with
    | 0 -> u_tup2 u_ucref u_tys st |> DecisionTreeTest.UnionCase
    | 1 -> u_const st               |> DecisionTreeTest.Const
    | 2 ->                             DecisionTreeTest.IsNull
    | 3 -> u_tup2 u_ty u_ty st    |> DecisionTreeTest.IsInst
    | 4 -> u_tup2 u_int u_ty st    |> DecisionTreeTest.ArrayLength
    | _ -> ufailwith st "u_dtree_discrim"

and u_target st = let a, b = u_tup2 u_Vals u_expr st in (TTarget(a, b, DebugPointForTarget.No))

and u_bind st = let a = u_Val st in let b = u_expr st in TBind(a, b, DebugPointAtBinding.NoneAtSticky)

and u_lval_op_kind st =
    match u_byte st with
    | 0 -> LAddrOf false
    | 1 -> LByrefGet
    | 2 -> LSet
    | 3 -> LByrefSet
    | _ -> ufailwith st "uval_op_kind"


and p_op x st =
    match x with
    | TOp.UnionCase c               -> p_byte 0 st; p_ucref c st
    | TOp.ExnConstr c               -> p_byte 1 st; p_tcref "op"  c st
    | TOp.Tuple tupInfo             ->
         if evalTupInfoIsStruct tupInfo then
              p_byte 29 st
         else
              p_byte 2 st
    | TOp.Recd (a, b)                 -> p_byte 3 st; p_tup2 p_recdInfo (p_tcref "recd op") (a, b) st
    | TOp.ValFieldSet a            -> p_byte 4 st; p_rfref a st
    | TOp.ValFieldGet a            -> p_byte 5 st; p_rfref a st
    | TOp.UnionCaseTagGet a        -> p_byte 6 st; p_tcref "cnstr op" a st
    | TOp.UnionCaseFieldGet (a, b)    -> p_byte 7 st; p_tup2 p_ucref p_int (a, b) st
    | TOp.UnionCaseFieldSet (a, b)    -> p_byte 8 st; p_tup2 p_ucref p_int (a, b) st
    | TOp.ExnFieldGet (a, b)          -> p_byte 9 st; p_tup2 (p_tcref "exn op") p_int (a, b) st
    | TOp.ExnFieldSet (a, b)          -> p_byte 10 st; p_tup2 (p_tcref "exn op")  p_int (a, b) st
    | TOp.TupleFieldGet (tupInfo, a)       ->
         if evalTupInfoIsStruct tupInfo then
              p_byte 30 st; p_int a st
         else
              p_byte 11 st; p_int a st
    | TOp.ILAsm (a, b)      -> p_byte 12 st; p_tup2 (p_list p_ILInstr) p_tys (a, b) st
    | TOp.RefAddrGet _               -> p_byte 13 st
    | TOp.UnionCaseProof a         -> p_byte 14 st; p_ucref a st
    | TOp.Coerce                     -> p_byte 15 st
    | TOp.TraitCall b              -> p_byte 16 st; p_trait b st
    | TOp.LValueOp (a, b)             -> p_byte 17 st; p_tup2 p_lval_op_kind (p_vref "lval") (a, b) st
    | TOp.ILCall (a1, a2, a3, a4, a5, a7, a8, a9, b, c, d)
                                     -> p_byte 18 st; p_tup11 p_bool p_bool p_bool p_bool p_vrefFlags p_bool p_bool p_ILMethodRef p_tys p_tys p_tys (a1, a2, a3, a4, a5, a7, a8, a9, b, c, d) st
    | TOp.Array                      -> p_byte 19 st
    | TOp.While _                    -> p_byte 20 st
    | TOp.For (_, dir)                 -> p_byte 21 st; p_int (match dir with FSharpForLoopUp -> 0 | CSharpForLoopUp -> 1 | FSharpForLoopDown -> 2) st
    | TOp.Bytes bytes                -> p_byte 22 st; p_bytes bytes st
    | TOp.TryWith _                 -> p_byte 23 st
    | TOp.TryFinally _               -> p_byte 24 st
    | TOp.ValFieldGetAddr (a, _)     -> p_byte 25 st; p_rfref a st
    | TOp.UInt16s arr                -> p_byte 26 st; p_array p_uint16 arr st
    | TOp.Reraise                    -> p_byte 27 st
    | TOp.UnionCaseFieldGetAddr (a, b, _) -> p_byte 28 st; p_tup2 p_ucref p_int (a, b) st
       // Note tag byte 29 is taken for struct tuples, see above
       // Note tag byte 30 is taken for struct tuples, see above
    (* 29: TOp.Tuple when evalTupInfoIsStruct tupInfo = true *)
    (* 30: TOp.TupleFieldGet  when evalTupInfoIsStruct tupInfo = true *)
    | TOp.AnonRecd info   -> p_byte 31 st; p_anonInfo info st
    | TOp.AnonRecdGet (info, n)   -> p_byte 32 st; p_anonInfo info st; p_int n st
    | TOp.Goto _ | TOp.Label _ | TOp.Return -> failwith "unexpected backend construct in pickled TAST"

and u_op st =
    let tag = u_byte st
    match tag with
    | 0 -> let a = u_ucref st
           TOp.UnionCase a
    | 1 -> let a = u_tcref st
           TOp.ExnConstr a
    | 2 -> TOp.Tuple tupInfoRef
    | 3 -> let b = u_tcref st
           TOp.Recd (RecdExpr, b)
    | 4 -> let a = u_rfref st
           TOp.ValFieldSet a
    | 5 -> let a = u_rfref st
           TOp.ValFieldGet a
    | 6 -> let a = u_tcref st
           TOp.UnionCaseTagGet a
    | 7 -> let a = u_ucref st
           let b = u_int st
           TOp.UnionCaseFieldGet (a, b)
    | 8 -> let a = u_ucref st
           let b = u_int st
           TOp.UnionCaseFieldSet (a, b)
    | 9 -> let a = u_tcref st
           let b = u_int st
           TOp.ExnFieldGet (a, b)
    | 10 -> let a = u_tcref st
            let b = u_int st
            TOp.ExnFieldSet (a, b)
    | 11 -> let a = u_int st
            TOp.TupleFieldGet (tupInfoRef, a)
    | 12 -> let a = (u_list u_ILInstr) st
            let b = u_tys st
            TOp.ILAsm (a, b)
    | 13 -> TOp.RefAddrGet false // ok to set the 'readonly' flag on these operands to false on re-read since the flag is only used for typechecking purposes
    | 14 -> let a = u_ucref st
            TOp.UnionCaseProof a
    | 15 -> TOp.Coerce
    | 16 -> let a = u_trait st
            TOp.TraitCall a
    | 17 -> let a = u_lval_op_kind st
            let b = u_vref st
            TOp.LValueOp (a, b)
    | 18 -> let (a1, a2, a3, a4, a5, a7, a8, a9) = (u_tup8 u_bool u_bool u_bool u_bool u_vrefFlags u_bool u_bool  u_ILMethodRef) st
            let b = u_tys st
            let c = u_tys st
            let d = u_tys st
            TOp.ILCall (a1, a2, a3, a4, a5, a7, a8, a9, b, c, d)
    | 19 -> TOp.Array
    | 20 -> TOp.While (DebugPointAtWhile.No, NoSpecialWhileLoopMarker)
    | 21 -> let dir = match u_int st with 0 -> FSharpForLoopUp | 1 -> CSharpForLoopUp | 2 -> FSharpForLoopDown | _ -> failwith "unknown for loop"
            TOp.For (DebugPointAtFor.No, dir)
    | 22 -> TOp.Bytes (u_bytes st)
    | 23 -> TOp.TryWith (DebugPointAtTry.No, DebugPointAtWith.No)
    | 24 -> TOp.TryFinally (DebugPointAtTry.No, DebugPointAtFinally.No)
    | 25 -> let a = u_rfref st
            TOp.ValFieldGetAddr (a, false)
    | 26 -> TOp.UInt16s (u_array u_uint16 st)
    | 27 -> TOp.Reraise
    | 28 -> let a = u_ucref st
            let b = u_int st
            TOp.UnionCaseFieldGetAddr (a, b, false)
    | 29 -> TOp.Tuple tupInfoStruct
    | 30 -> let a = u_int st
            TOp.TupleFieldGet (tupInfoStruct, a)
    | 31 -> let info = u_anonInfo st
            TOp.AnonRecd info
    | 32 -> let info = u_anonInfo st
            let n = u_int st
            TOp.AnonRecdGet (info, n)
    | _ -> ufailwith st "u_op"

and p_expr expr st =
    match expr with
    | Expr.Link e -> p_expr !e st
    | Expr.Const (x, m, ty)              -> p_byte 0 st; p_tup3 p_const p_dummy_range p_ty (x, m, ty) st
    | Expr.Val (a, b, m)                 -> p_byte 1 st; p_tup3 (p_vref "val") p_vrefFlags p_dummy_range (a, b, m) st
    | Expr.Op (a, b, c, d)                 -> p_byte 2 st; p_tup4 p_op  p_tys p_Exprs p_dummy_range (a, b, c, d) st
    | Expr.Sequential (a, b, c, _, d)      -> p_byte 3 st; p_tup4 p_expr p_expr p_int p_dummy_range (a, b, (match c with NormalSeq -> 0 | ThenDoSeq -> 1), d) st
    | Expr.Lambda (_, a1, b0, b1, c, d, e)   -> p_byte 4 st; p_tup6 (p_option p_Val) (p_option p_Val) p_Vals p_expr p_dummy_range p_ty (a1, b0, b1, c, d, e) st
    | Expr.TyLambda (_, b, c, d, e)        -> p_byte 5 st; p_tup4 p_tyar_specs p_expr p_dummy_range p_ty (b, c, d, e) st
    | Expr.App (a1, a2, b, c, d)           -> p_byte 6 st; p_tup5 p_expr p_ty p_tys p_Exprs p_dummy_range (a1, a2, b, c, d) st
    | Expr.LetRec (a, b, c, _)            -> p_byte 7 st; p_tup3 p_binds p_expr p_dummy_range (a, b, c) st
    | Expr.Let (a, b, c, _)               -> p_byte 8 st; p_tup3 p_bind p_expr p_dummy_range (a, b, c) st
    | Expr.Match (_, a, b, c, d, e)         -> p_byte 9 st; p_tup5 p_dummy_range p_dtree p_targets p_dummy_range p_ty (a, b, c, d, e) st
    | Expr.Obj (_, b, c, d, e, f, g)          -> p_byte 10 st; p_tup6 p_ty (p_option p_Val) p_expr p_methods p_intfs p_dummy_range (b, c, d, e, f, g) st
    | Expr.StaticOptimization (a, b, c, d) -> p_byte 11 st; p_tup4 p_constraints p_expr p_expr p_dummy_range (a, b, c, d) st
    | Expr.TyChoose (a, b, c)            -> p_byte 12 st; p_tup3 p_tyar_specs p_expr p_dummy_range (a, b, c) st
    | Expr.Quote (ast, _, _, m, ty)         -> p_byte 13 st; p_tup3 p_expr p_dummy_range p_ty (ast, m, ty) st
    | Expr.WitnessArg (traitInfo, m) -> p_byte 14 st; p_trait traitInfo st; p_dummy_range m st

and u_expr st =
    let tag = u_byte st
    match tag with
    | 0 -> let a = u_const st
           let b = u_dummy_range st
           let c = u_ty st
           Expr.Const (a, b, c)
    | 1 -> let a = u_vref st
           let b = u_vrefFlags st
           let c = u_dummy_range st
           Expr.Val (a, b, c)
    | 2 -> let a = u_op st
           let b = u_tys st
           let c = u_Exprs st
           let d = u_dummy_range st
           Expr.Op (a, b, c, d)
    | 3 -> let a = u_expr st
           let b = u_expr st
           let c = u_int st
           let d = u_dummy_range  st
           Expr.Sequential (a, b, (match c with 0 -> NormalSeq | 1 -> ThenDoSeq | _ -> ufailwith st "specialSeqFlag"), DebugPointAtSequential.StmtOnly, d)
    | 4 -> let a0 = u_option u_Val st
           let b0 = u_option u_Val st
           let b1 = u_Vals st
           let c = u_expr st
           let d = u_dummy_range st
           let e = u_ty st
           Expr.Lambda (newUnique(), a0, b0, b1, c, d, e)
    | 5  -> let b = u_tyar_specs st
            let c = u_expr st
            let d = u_dummy_range st
            let e = u_ty st
            Expr.TyLambda (newUnique(), b, c, d, e)
    | 6 ->  let a1 = u_expr st
            let a2 = u_ty st
            let b = u_tys st
            let c = u_Exprs st
            let d = u_dummy_range st
            Expr.App (a1, a2, b, c, d)
    | 7 ->  let a = u_binds st
            let b = u_expr st
            let c = u_dummy_range st
            Expr.LetRec (a, b, c, Construct.NewFreeVarsCache())
    | 8 ->  let a = u_bind st
            let b = u_expr st
            let c = u_dummy_range st
            Expr.Let (a, b, c, Construct.NewFreeVarsCache())
    | 9 ->  let a = u_dummy_range st
            let b = u_dtree st
            let c = u_targets st
            let d = u_dummy_range st
            let e = u_ty st
            Expr.Match (DebugPointAtBinding.NoneAtSticky, a, b, c, d, e)
    | 10 -> let b = u_ty st
            let c = (u_option u_Val) st
            let d = u_expr st
            let e = u_methods st
            let f = u_intfs st
            let g = u_dummy_range st
            Expr.Obj (newUnique(), b, c, d, e, f, g)
    | 11 -> let a = u_constraints st
            let b = u_expr st
            let c = u_expr st
            let d = u_dummy_range st
            Expr.StaticOptimization (a, b, c, d)
    | 12 -> let a = u_tyar_specs st
            let b = u_expr st
            let c = u_dummy_range st
            Expr.TyChoose (a, b, c)
    | 13 -> let b = u_expr st
            let c = u_dummy_range st
            let d = u_ty st
            Expr.Quote (b, ref None, false, c, d) // isFromQueryExpression=false
    | 14 ->
        let traitInfo = u_trait st
        let m = u_dummy_range st
        Expr.WitnessArg (traitInfo, m)
    | _ -> ufailwith st "u_expr"

and p_static_optimization_constraint x st =
    match x with
    | TTyconEqualsTycon (a, b) -> p_byte 0 st; p_tup2 p_ty p_ty (a, b) st
    | TTyconIsStruct a -> p_byte 1 st; p_ty a st

and p_slotparam (TSlotParam (a, b, c, d, e, f)) st = p_tup6 (p_option p_string) p_ty p_bool p_bool p_bool p_attribs (a, b, c, d, e, f) st
and p_slotsig (TSlotSig (a, b, c, d, e, f)) st = p_tup6 p_string p_ty p_tyar_specs p_tyar_specs (p_list (p_list p_slotparam)) (p_option p_ty) (a, b, c, d, e, f) st
and p_method (TObjExprMethod (a, b, c, d, e, f)) st = p_tup6 p_slotsig p_attribs p_tyar_specs (p_list p_Vals) p_expr p_dummy_range (a, b, c, d, e, f) st
and p_methods x st = p_list p_method x st
and p_intf x st = p_tup2 p_ty p_methods x st
and p_intfs x st = p_list p_intf x st

and u_static_optimization_constraint st =
    let tag = u_byte st
    match tag with
    | 0 -> u_tup2 u_ty u_ty st |> TTyconEqualsTycon
    | 1 -> u_ty              st |> TTyconIsStruct
    | _ -> ufailwith st "u_static_optimization_constraint"

and u_slotparam st =
    let a, b, c, d, e, f = u_tup6 (u_option u_string) u_ty u_bool u_bool u_bool u_attribs st
    TSlotParam(a, b, c, d, e, f)

and u_slotsig st =
    let a, b, c, d, e, f = u_tup6 u_string u_ty u_tyar_specs u_tyar_specs (u_list (u_list u_slotparam)) (u_option u_ty) st
    TSlotSig(a, b, c, d, e, f)

and u_method st =
    let a, b, c, d, e, f = u_tup6 u_slotsig u_attribs u_tyar_specs (u_list u_Vals) u_expr u_dummy_range st
    TObjExprMethod(a, b, c, d, e, f)

and u_methods st = u_list u_method st

and u_intf st = u_tup2 u_ty u_methods st

and u_intfs st = u_list u_intf st

let _ = fill_p_binds (p_List p_bind)
let _ = fill_p_targets (p_array p_target)
let _ = fill_p_constraints (p_list p_static_optimization_constraint)
let _ = fill_p_Exprs (p_list p_expr)
let _ = fill_p_Expr_hole p_expr
let _ = fill_p_Exprs (p_List p_expr)
let _ = fill_p_attribs (p_list p_attrib)
let _ = fill_p_Vals (p_list p_Val)

let _ = fill_u_binds (u_List u_bind)
let _ = fill_u_targets (u_array u_target)
let _ = fill_u_constraints (u_list u_static_optimization_constraint)
let _ = fill_u_Exprs (u_list u_expr)
let _ = fill_u_Expr_hole u_expr
let _ = fill_u_attribs (u_list u_attrib)
let _ = fill_u_Vals (u_list u_Val)

//---------------------------------------------------------------------------
// Pickle/unpickle F# interface data
//---------------------------------------------------------------------------

let pickleModuleOrNamespace mspec st = p_entity_spec mspec st

let pickleCcuInfo (minfo: PickledCcuInfo) st =
    p_tup4 pickleModuleOrNamespace p_string p_bool (p_space 3) (minfo.mspec, minfo.compileTimeWorkingDir, minfo.usesQuotations, ()) st

let unpickleModuleOrNamespace st = u_entity_spec st

let unpickleCcuInfo st =
    let a, b, c, _space = u_tup4 unpickleModuleOrNamespace u_string u_bool (u_space 3) st
    { mspec=a; compileTimeWorkingDir=b; usesQuotations=c }
