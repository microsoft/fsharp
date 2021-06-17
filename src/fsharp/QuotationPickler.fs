// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.QuotationPickler

open System
open System.Text
open FSharp.Compiler.IO
open Internal.Utilities
open Internal.Utilities.Collections
open Internal.Utilities.Library.Extras

let mkRLinear mk (vs, body) = List.foldBack (fun v acc -> mk (v, acc)) vs body

type TypeVarData = { tvName: string }

type NamedTypeData =
    | Idx of int
    | Named of tcName: string * tcAssembly: string

type TypeCombOp =
    | ArrayTyOp of int (* rank *)
    | FunTyOp
    | NamedTyOp of NamedTypeData

type TypeData =
    | VarType of int
    | AppType of TypeCombOp * TypeData list

let mkVarTy v = VarType v

let mkFunTy (x1, x2) = AppType(FunTyOp, [x1; x2])

let mkArrayTy (n, x) = AppType(ArrayTyOp n, [x])

let mkILNamedTy (r, l) = AppType(NamedTyOp r, l)

type CtorData =
    { ctorParent: NamedTypeData
      ctorArgTypes: TypeData list; }

type MethodData =
    { methParent: NamedTypeData
      methName: string
      methArgTypes: TypeData list
      methRetType: TypeData
      numGenericArgs: int }

type VarData =
    { vText: string
      vType: TypeData
      vMutable: bool }

type PropInfoData = NamedTypeData * string * TypeData * TypeData list

type CombOp =
    | AppOp
    | CondOp
    | ModuleValueOp of NamedTypeData * string * bool
    | ModuleValueWOp of NamedTypeData * string * bool * string * int
    | LetRecOp
    | LetRecCombOp
    | LetOp
    | RecdMkOp of NamedTypeData
    | RecdGetOp of NamedTypeData * string
    | RecdSetOp of NamedTypeData * string
    | SumMkOp of NamedTypeData * string
    | SumFieldGetOp of NamedTypeData * string * int
    | SumTagTestOp of NamedTypeData * string
    | TupleMkOp
    | TupleGetOp of int
    | UnitOp
    | BoolOp of bool
    | StringOp of string
    | SingleOp of float32
    | DoubleOp of float
    | CharOp of char
    | SByteOp of sbyte
    | ByteOp of byte
    | Int16Op of int16
    | UInt16Op of uint16
    | Int32Op of int32
    | UInt32Op of uint32
    | Int64Op of int64
    | UInt64Op of uint64
    | PropGetOp of PropInfoData
    | FieldGetOp of NamedTypeData * string
    | CtorCallOp of CtorData
    | MethodCallOp of MethodData
    | MethodCallWOp of MethodData * MethodData * int
    | CoerceOp
    | NewArrayOp
    | DelegateOp
    | SeqOp
    | ForLoopOp
    | WhileLoopOp
    | NullOp
    | DefaultValueOp
    | PropSetOp of PropInfoData
    | FieldSetOp of NamedTypeData * string
    | AddressOfOp
    | ExprSetOp
    | AddressSetOp
    | TypeTestOp
    | TryFinallyOp
    | TryWithOp

/// Represents specifications of a subset of F# expressions
type ExprData =
    | AttrExpr of ExprData * ExprData list
    | CombExpr of CombOp * TypeData list * ExprData list
    | VarExpr of int
    | QuoteExpr of ExprData
    | LambdaExpr of VarData * ExprData
    | HoleExpr of TypeData * int
    | ThisVarExpr of TypeData
    | QuoteRawExpr of ExprData

let mkVar v = VarExpr v

let mkHole (v, idx) = HoleExpr (v, idx)

let mkApp (a, b) = CombExpr(AppOp, [], [a; b])

let mkLambda (a, b) = LambdaExpr (a, b)

let mkQuote (a) = QuoteExpr (a)

let mkQuoteRaw40 (a) = QuoteRawExpr (a)

let mkCond (x1, x2, x3)          = CombExpr(CondOp, [], [x1;x2;x3])

let mkModuleValueApp (tcref, nm, isProp, tyargs, args: ExprData list) =
    CombExpr(ModuleValueOp(tcref, nm, isProp), tyargs, args)

let mkModuleValueWApp (tcref, nm, isProp, nmW, nWitnesses, tyargs, args: ExprData list) =
    CombExpr(ModuleValueWOp(tcref, nm, isProp, nmW, nWitnesses), tyargs, args)

let mkTuple (ty, x)             = CombExpr(TupleMkOp, [ty], x)

let mkLet ((v, e), b)            = CombExpr(LetOp, [], [e;mkLambda (v, b)])  (* nb. order preserves source order *)

let mkUnit ()                  = CombExpr(UnitOp, [], [])

let mkNull ty                  = CombExpr(NullOp, [ty], [])

let mkLetRecRaw e1 = CombExpr(LetRecOp, [], [e1])

let mkLetRecCombRaw args = CombExpr(LetRecCombOp, [], args)

let mkLetRec (ves, body) =
     let vs, es = List.unzip ves
     mkLetRecRaw(mkRLinear mkLambda  (vs, mkLetRecCombRaw (body :: es)))

let mkRecdMk      (n, tys, args)            = CombExpr(RecdMkOp n, tys, args)

let mkRecdGet     (d1, d2, tyargs, args)   = CombExpr(RecdGetOp(d1, d2), tyargs, args)

let mkRecdSet     (d1, d2, tyargs, args)   = CombExpr(RecdSetOp(d1, d2), tyargs, args)

let mkUnion         (d1, d2, tyargs, args)   = CombExpr(SumMkOp(d1, d2), tyargs, args)

let mkUnionFieldGet (d1, d2, d3, tyargs, arg) = CombExpr(SumFieldGetOp(d1, d2, d3), tyargs, [arg])

let mkUnionCaseTagTest  (d1, d2, tyargs, arg)    = CombExpr(SumTagTestOp(d1, d2), tyargs, [arg])

let mkTupleGet    (ty, n, e)                = CombExpr(TupleGetOp n, [ty], [e])

let mkCoerce  (ty, arg)        = CombExpr(CoerceOp, [ty], [arg])

let mkTypeTest  (ty, arg)      = CombExpr(TypeTestOp, [ty], [arg])

let mkAddressOf  (arg)        = CombExpr(AddressOfOp, [], [arg])

let mkAddressSet  (arg1, arg2) = CombExpr(AddressSetOp, [], [arg1;arg2])

let mkVarSet  (arg1, arg2)     = CombExpr(ExprSetOp, [], [arg1;arg2])

let mkDefaultValue (ty)       = CombExpr(DefaultValueOp, [ty], [])

let mkThisVar (ty)       = ThisVarExpr(ty)

let mkNewArray     (ty, args)  = CombExpr(NewArrayOp, [ty], args)

let mkBool   (v, ty) = CombExpr(BoolOp   v, [ty], [])

let mkString (v, ty) = CombExpr(StringOp v, [ty], [])

let mkSingle (v, ty) = CombExpr(SingleOp v, [ty], [])

let mkDouble (v, ty) = CombExpr(DoubleOp v, [ty], [])

let mkChar   (v, ty) = CombExpr(CharOp   v, [ty], [])

let mkSByte  (v, ty) = CombExpr(SByteOp  v, [ty], [])

let mkByte   (v, ty) = CombExpr(ByteOp   v, [ty], [])

let mkInt16  (v, ty) = CombExpr(Int16Op  v, [ty], [])

let mkUInt16 (v, ty) = CombExpr(UInt16Op v, [ty], [])

let mkInt32  (v, ty) = CombExpr(Int32Op  v, [ty], [])

let mkUInt32 (v, ty) = CombExpr(UInt32Op v, [ty], [])

let mkInt64  (v, ty) = CombExpr(Int64Op  v, [ty], [])

let mkUInt64 (v, ty) = CombExpr(UInt64Op v, [ty], [])

let mkSequential (e1, e2)       = CombExpr(SeqOp, [], [e1;e2])

let mkForLoop (x1, x2, x3)       = CombExpr(ForLoopOp, [], [x1;x2;x3])

let mkWhileLoop (e1, e2)        = CombExpr(WhileLoopOp, [], [e1;e2])

let mkTryFinally(e1, e2)        = CombExpr(TryFinallyOp, [], [e1;e2])

let mkTryWith(e1, vf, ef, vh, eh)  = CombExpr(TryWithOp, [], [e1;mkLambda(vf, ef);mkLambda(vh, eh)])

let mkDelegate (ty, e)          = CombExpr(DelegateOp, [ty], [e])

let mkPropGet  (d, tyargs, args) = CombExpr(PropGetOp(d), tyargs, args)

let mkPropSet  (d, tyargs, args) = CombExpr(PropSetOp(d), tyargs, args)

let mkFieldGet (d1, d2, tyargs, args) = CombExpr(FieldGetOp(d1, d2), tyargs, args)

let mkFieldSet (d1, d2, tyargs, args) = CombExpr(FieldSetOp(d1, d2), tyargs, args)

let mkCtorCall   (d, tyargs, args) = CombExpr(CtorCallOp(d), tyargs, args)

let mkMethodCall (d, tyargs, args) = CombExpr(MethodCallOp(d), tyargs, args)

let mkMethodCallW (d1, d2, d3, tyargs, args) = CombExpr(MethodCallWOp(d1, d2, d3), tyargs, args)

let mkAttributedExpression(e, attr) = AttrExpr(e, [attr])

let isAttributedExpression e = match e with AttrExpr(_, _) -> true | _ -> false

//---------------------------------------------------------------------------
// Pickle/unpickle expression and type specifications in a stable format
// compatible with those read by Microsoft.FSharp.Quotations
//---------------------------------------------------------------------------

let SerializedReflectedDefinitionsResourceNameBase = "ReflectedDefinitions"

let freshVar (n, ty, mut) = { vText=n; vType=ty; vMutable=mut }

/// Arbitrary value
[<Literal>]
let PickleBufferCapacity = 100000

module SimplePickle =

    type Table<'T> =
        { tbl: HashMultiMap<'T, int> // This should be "Dictionary"
          mutable rows: 'T list
          mutable count: int }

        static member Create () =
            { tbl = HashMultiMap(20, HashIdentity.Structural)
              rows=[]
              count=0; }

        member tbl.AsList = List.rev tbl.rows

        member tbl.Count = tbl.rows.Length

        member tbl.Add x =
            let n = tbl.count
            tbl.count <- tbl.count + 1
            tbl.tbl.Add(x, n)
            tbl.rows <- x :: tbl.rows
            n

        member tbl.FindOrAdd x =
            if tbl.tbl.ContainsKey x then tbl.tbl.[x]
            else tbl.Add x

        member tbl.Find x = tbl.tbl.[x]

        member tbl.ContainsKey x = tbl.tbl.ContainsKey x

    type QuotationPickleOutState =
        { os: ByteBuffer
          ostrings: Table<string> }

    let p_byte b st = st.os.EmitIntAsByte b

    let p_bool b st = p_byte (if b then 1 else 0) st

    let p_void (_os: QuotationPickleOutState) = ()

    let p_unit () (_os: QuotationPickleOutState) = ()

    let prim_pint32 i st =
        p_byte (Bits.b0 i) st
        p_byte (Bits.b1 i) st
        p_byte (Bits.b2 i) st
        p_byte (Bits.b3 i) st

    // compress integers according to the same scheme used by CLR metadata
    // This halves the size of pickled data
    let p_int32 n st =
        if n >= 0 &&  n <= 0x7F then
            p_byte (Bits.b0 n) st
        else if n >= 0x80 && n <= 0x3FFF then
            p_byte  (0x80 ||| (n >>> 8)) st
            p_byte (n &&& 0xFF) st
        else
            p_byte 0xFF st
            prim_pint32 n st

    let p_bytes (s:byte[]) st =
        let len = s.Length
        p_int32 (len) st
        st.os.EmitBytes s

    let p_memory (s:ReadOnlyMemory<byte>) st =
        let len = s.Length
        p_int32 (len) st
        st.os.EmitMemory s

    let prim_pstring (s:string) st =
        let bytes = Encoding.UTF8.GetBytes s
        let len = bytes.Length
        p_int32 (len) st
        st.os.EmitBytes bytes

    let p_int (c:int) st = p_int32 c st

    let p_int8 (i:int8) st = p_int32 (int32 i) st

    let p_uint8 (i:uint8) st = p_byte (int i) st

    let p_int16 (i:int16) st = p_int32 (int32 i) st

    let p_uint16 (x:uint16) st = p_int32 (int32 x) st

    let puint32 (x:uint32) st = p_int32 (int32 x) st

    let p_int64 i st =
        p_int32 (int32 (i &&& 0xFFFFFFFFL)) st
        p_int32 (int32 (i >>> 32)) st

    let bits_of_float32 (x:float32) = System.BitConverter.ToInt32(System.BitConverter.GetBytes(x), 0)

    let bits_of_float (x:float) = System.BitConverter.ToInt64(System.BitConverter.GetBytes(x), 0)

    let p_uint64 x st = p_int64 (int64 x) st

    let p_double i st = p_int64 (bits_of_float i) st

    let p_single i st = p_int32 (bits_of_float32 i) st

    let p_char i st = p_uint16 (uint16 (int32 i)) st

    let inline p_tup2 p1 p2 (a, b) (st:QuotationPickleOutState) = (p1 a st : unit); (p2 b st : unit)

    let inline p_tup3 p1 p2 p3 (a, b, c) st = (p1 a st : unit); (p2 b st : unit); (p3 c st : unit)

    let inline p_tup4 p1 p2 p3 p4 (a, b, c, d) st = (p1 a st : unit); (p2 b st : unit); (p3 c st : unit); (p4 d st : unit)

    let inline p_tup5 p1 p2 p3 p4 p5 (a, b, c, d, e) st = (p1 a st : unit); (p2 b st : unit); (p3 c st : unit); (p4 d st : unit); (p5 e st : unit)

    let puniq (tbl: Table<_>) key st = p_int (tbl.FindOrAdd key) st

    let p_string s st = puniq st.ostrings s st

    let rec p_list f x st =
        match x with
        | [] -> p_byte 0 st
        | h :: t -> p_byte 1 st; f h st; p_list f t st

    let pickle_obj p x =
        let st1 =
            { os = ByteBuffer.Create(PickleBufferCapacity, useArrayPool = true)
              ostrings=Table<_>.Create() }
        let stringTab, phase1bytes =
            p x st1
            st1.ostrings.AsList, st1.os.AsMemory()

        let phase2data = (stringTab, phase1bytes)

        let st2 =
           { os = ByteBuffer.Create(PickleBufferCapacity, useArrayPool = true)
             ostrings=Table<_>.Create() }
        let phase2bytes =
            p_tup2 (p_list prim_pstring) p_memory phase2data st2
            st2.os.AsMemory()

        let finalBytes = phase2bytes.ToArray()
        (st1.os :> IDisposable).Dispose()
        (st2.os :> IDisposable).Dispose()
        finalBytes

open SimplePickle

let p_assemblyref x st = p_string x st

let p_NamedType x st =
    match x with
    | Idx n -> p_tup2 p_string p_assemblyref (string n, "") st
    | Named (nm, a) -> p_tup2 p_string p_assemblyref (nm, a) st

let p_tycon x st =
    match x with
    | FunTyOp     -> p_byte 1 st
    | NamedTyOp a -> p_byte 2 st; p_NamedType a st
    | ArrayTyOp a -> p_byte 3 st; p_int a st

let rec p_type x st =
    match x with
    | VarType v     -> p_byte 0 st; p_int v st
    | AppType(c, ts) -> p_byte 1 st; p_tup2 p_tycon p_types (c, ts) st

and p_types x st = p_list p_type x st

let p_varDecl v st   = p_tup3 p_string p_type p_bool (v.vText, v.vType, v.vMutable) st

let p_recdFieldSpec v st = p_tup2 p_NamedType p_string v st

let p_ucaseSpec v st   = p_tup2 p_NamedType p_string v st

let p_MethodData a st =
    p_tup5 p_NamedType p_types p_type p_string p_int (a.methParent, a.methArgTypes, a.methRetType, a.methName, a.numGenericArgs) st

let p_CtorData a st =
    p_tup2 p_NamedType p_types (a.ctorParent, a.ctorArgTypes) st

let p_PropInfoData a st =
    p_tup4 p_NamedType p_string p_type p_types a st

let p_CombOp x st =
    match x with
    | CondOp        -> p_byte 0 st
    | ModuleValueOp (x, y, z) ->
        p_byte 1 st
        p_NamedType x st
        p_string y st
        p_bool z st
    | LetRecOp      -> p_byte 2 st
    | RecdMkOp  a   -> p_byte 3 st; p_NamedType a st
    | RecdGetOp  (x, y)  -> p_byte 4 st; p_recdFieldSpec (x, y) st
    | SumMkOp  (x, y)  -> p_byte 5 st; p_ucaseSpec (x, y) st
    | SumFieldGetOp (a, b, c) -> p_byte 6 st; p_tup2 p_ucaseSpec p_int ((a, b), c) st
    | SumTagTestOp (x, y) -> p_byte 7 st; p_ucaseSpec (x, y) st
    | TupleMkOp     -> p_byte 8 st
    | TupleGetOp a  -> p_byte 9 st; p_int a st
    | BoolOp   a    -> p_byte 11 st; p_bool a st
    | StringOp a    -> p_byte 12 st; p_string a st
    | SingleOp a    -> p_byte 13 st; p_single a st
    | DoubleOp a    -> p_byte 14 st; p_double a st
    | CharOp   a    -> p_byte 15 st; p_char a st
    | SByteOp  a    -> p_byte 16 st; p_int8 a st
    | ByteOp   a    -> p_byte 17 st; p_uint8 a st
    | Int16Op  a    -> p_byte 18 st; p_int16 a st
    | UInt16Op a    -> p_byte 19 st; p_uint16 a st
    | Int32Op  a    -> p_byte 20 st; p_int32 a st
    | UInt32Op a    -> p_byte 21 st; puint32 a st
    | Int64Op  a    -> p_byte 22 st; p_int64 a st
    | UInt64Op a    -> p_byte 23 st; p_uint64 a st
    | UnitOp        -> p_byte 24 st
    | PropGetOp d   -> p_byte 25 st; p_PropInfoData d st
    | CtorCallOp a  -> p_byte 26 st; p_CtorData a st
    | CoerceOp      -> p_byte 28 st
    | SeqOp         -> p_byte 29 st
    | ForLoopOp        -> p_byte 30 st
    | MethodCallOp a   -> p_byte 31 st; p_MethodData a st
    | NewArrayOp       -> p_byte 32 st
    | DelegateOp       -> p_byte 33 st
    | WhileLoopOp      -> p_byte 34 st
    | LetOp            -> p_byte 35 st
    | RecdSetOp  (x, y) -> p_byte 36 st; p_recdFieldSpec (x, y) st
    | FieldGetOp (a, b) -> p_byte 37 st; p_tup2 p_NamedType p_string (a, b) st
    | LetRecCombOp     -> p_byte 38 st
    | AppOp            -> p_byte 39 st
    | NullOp           -> p_byte 40 st
    | DefaultValueOp   -> p_byte 41 st
    | PropSetOp d      -> p_byte 42 st; p_PropInfoData d st
    | FieldSetOp (a, b) -> p_byte 43 st; p_tup2 p_NamedType p_string (a, b) st
    | AddressOfOp      -> p_byte 44 st
    | AddressSetOp     -> p_byte 45 st
    | TypeTestOp       -> p_byte 46 st
    | TryFinallyOp     -> p_byte 47 st
    | TryWithOp        -> p_byte 48 st
    | ExprSetOp        -> p_byte 49 st
    | MethodCallWOp (a, b, c) ->
        p_byte 50 st
        p_MethodData a st
        p_MethodData b st
        p_int c st
    | ModuleValueWOp (x, y, z, nmW, nWitnesses) ->
        p_byte 51 st
        p_string nmW st
        p_int nWitnesses st
        p_NamedType x st
        p_string y st
        p_bool z st

let rec p_expr x st =
    match x with
    | CombExpr(c, ts, args) -> p_byte 0 st; p_tup3 p_CombOp p_types (p_list p_expr) (c, ts, args) st
    | VarExpr v           -> p_byte 1 st; p_int v st
    | LambdaExpr(v, e)     -> p_byte 2 st; p_tup2 p_varDecl p_expr (v, e) st
    | HoleExpr(ty, idx)    -> p_byte 3 st; p_type ty st; p_int idx st
    | QuoteExpr(tm)       -> p_byte 4 st; p_expr tm st
    | AttrExpr(e, attrs)   -> p_byte 5 st; p_tup2 p_expr (p_list p_expr) (e, attrs) st
    | ThisVarExpr(ty)     -> p_byte 6 st; p_type ty st
    | QuoteRawExpr(tm)    -> p_byte 7 st; p_expr tm st

type ModuleDefnData =
    { Module: NamedTypeData
      Name: string
      IsProperty: bool }

type MethodBaseData =
    | ModuleDefn of ModuleDefnData * (string * int) option
    | Method of MethodData
    | Ctor of CtorData

let pickle = pickle_obj p_expr

let p_MethodBase x st =
    match x with
    | ModuleDefn (md, None) ->
        p_byte 0 st
        p_NamedType md.Module st
        p_string md.Name st
        p_bool md.IsProperty st
    | ModuleDefn (md, Some (nmW, nWitnesses)) ->
        p_byte 3 st
        p_string nmW st
        p_int nWitnesses st
        p_NamedType md.Module st
        p_string md.Name st
        p_bool md.IsProperty st
    | Method md ->
        p_byte 1 st
        p_MethodData md st
    | Ctor md ->
        p_byte 2 st
        p_CtorData md st

let PickleDefns   = pickle_obj (p_list (p_tup2 p_MethodBase p_expr))


