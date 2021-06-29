// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.PatternMatchCompilation

open System.Collections.Generic
open Internal.Utilities.Library
open Internal.Utilities.Library.Extras
open FSharp.Compiler
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AbstractIL.Diagnostics
open FSharp.Compiler.AccessibilityLogic
open FSharp.Compiler.CompilerGlobalState
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.InfoReader
open FSharp.Compiler.MethodCalls
open FSharp.Compiler.Syntax
open FSharp.Compiler.Syntax.PrettyNaming
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Compiler.Text
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.TypedTreeOps.DebugPrint
open FSharp.Compiler.TypeRelations

exception MatchIncomplete of bool * (string * bool) option * range
exception RuleNeverMatched of range
exception EnumMatchIncomplete of bool * (string * bool) option * range

type ActionOnFailure =
    | ThrowIncompleteMatchException
    | IgnoreWithWarning
    | Throw
    | Rethrow
    | FailFilter

[<NoEquality; NoComparison>]
type Pattern =
    | TPat_const of Const * range
    | TPat_wild of range  (* note = TPat_disjs([], m), but we haven't yet removed that duplication *)
    | TPat_as of  Pattern * PatternValBinding * range (* note: can be replaced by TPat_var, i.e. equals TPat_conjs([TPat_var; pat]) *)
    | TPat_disjs of  Pattern list * range
    | TPat_conjs of  Pattern list * range
    | TPat_query of (Expr * TType list * bool * (ValRef * TypeInst) option * int * ActivePatternInfo) * Pattern * range
    | TPat_unioncase of UnionCaseRef * TypeInst * Pattern list * range
    | TPat_exnconstr of TyconRef * Pattern list * range
    | TPat_tuple of  TupInfo * Pattern list * TType list * range
    | TPat_array of  Pattern list * TType * range
    | TPat_recd of TyconRef * TypeInst * Pattern list * range
    | TPat_range of char * char * range
    | TPat_null of range
    | TPat_isinst of TType * TType * Pattern option * range
    | TPat_error of range

    member this.Range =
        match this with
        |   TPat_const(_, m) -> m
        |   TPat_wild m -> m
        |   TPat_as(_, _, m) -> m
        |   TPat_disjs(_, m) -> m
        |   TPat_conjs(_, m) -> m
        |   TPat_query(_, _, m) -> m
        |   TPat_unioncase(_, _, _, m) -> m
        |   TPat_exnconstr(_, _, m) -> m
        |   TPat_tuple(_, _, _, m) -> m
        |   TPat_array(_, _, m) -> m
        |   TPat_recd(_, _, _, m) -> m
        |   TPat_range(_, _, m) -> m
        |   TPat_null m -> m
        |   TPat_isinst(_, _, _, m) -> m
        |   TPat_error m -> m

and PatternValBinding = PBind of Val * TypeScheme

and TypedMatchClause =
    | TClause of Pattern * Expr option * DecisionTreeTarget * range
    member c.GuardExpr = let (TClause(_, whenOpt, _, _)) = c in whenOpt
    member c.Pattern = let (TClause(p, _, _, _)) = c in p
    member c.Range = let (TClause(_, _, _, m)) = c in m
    member c.Target = let (TClause(_, _, tg, _)) = c in tg
    member c.BoundVals = let (TClause(_p, _whenOpt, TTarget(vs, _, _), _m)) = c in vs

let debug = false

//---------------------------------------------------------------------------
// Nasty stuff to permit obscure generic bindings such as
//     let x, y = [], []
//
// BindSubExprOfInput actually produces the binding
// e.g. let v2 = \Gamma ['a, 'b]. ([] : 'a, [] : 'b)
//      let (x, y) = p.
// When v = x, gtvs = 'a, 'b.  We must bind:
//     x --> \Gamma A. fst (v2[A, <dummy>])
//     y --> \Gamma A. snd (v2[<dummy>, A]).
//
// GetSubExprOfInput is just used to get a concrete value from a type
// function in the middle of the "test" part of pattern matching.
// For example, e.g.  let [x; y] = [ (\x.x); (\x.x) ]
// Here the constructor test needs a real list, even though the
// r.h.s. is actually a polymorphic type function.  To do the
// test, we apply the r.h.s. to a dummy type - it doesn't matter
// which (unless the r.h.s. actually looks at it's type argument...)
//---------------------------------------------------------------------------

type SubExprOfInput =
    | SubExpr of (TyparInst -> Expr -> Expr) * (Expr * Val)

let BindSubExprOfInput g amap gtps (PBind(v, tyscheme)) m (SubExpr(accessf, (ve2, v2))) =
    let e' =
        if isNil gtps then
            accessf [] ve2
        else
            let tyargs =
                let mutable someSolved = false
                let freezeVar gtp =
                    if isBeingGeneralized gtp tyscheme then
                        mkTyparTy gtp
                    else
                        someSolved <- true
                        TypeRelations.ChooseTyparSolution g amap gtp

                let solutions = List.map freezeVar gtps
                if someSolved then
                    TypeRelations.IterativelySubstituteTyparSolutions g gtps solutions
                else
                    solutions

            let tinst = mkTyparInst gtps tyargs
            accessf tinst (mkApps g ((ve2, v2.Type), [tyargs], [], v2.Range))

    v, mkGenericBindRhs g m [] tyscheme e'

let GetSubExprOfInput g (gtps, tyargs, tinst) (SubExpr(accessf, (ve2, v2))) =
    if isNil gtps then accessf [] ve2 else
    accessf tinst (mkApps g ((ve2, v2.Type), [tyargs], [], v2.Range))

//---------------------------------------------------------------------------
// path, frontier
//---------------------------------------------------------------------------

// A path reaches into a pattern.
// The ints record which choices taken, e.g. tuple/record fields.
type Path =
    | PathQuery of Path * Unique
    | PathConj of Path * int
    | PathTuple of Path * TypeInst * int
    | PathRecd of Path * TyconRef * TypeInst * int
    | PathUnionConstr of Path * UnionCaseRef * TypeInst * int
    | PathArray of Path * TType * int * int
    | PathExnConstr of Path * TyconRef * int
    | PathEmpty of TType

let rec pathEq p1 p2 =
    match p1, p2 with
    | PathQuery(p1, n1), PathQuery(p2, n2) -> (n1 = n2) && pathEq p1 p2
    | PathConj(p1, n1), PathConj(p2, n2) -> (n1 = n2) && pathEq p1 p2
    | PathTuple(p1, _, n1), PathTuple(p2, _, n2) -> (n1 = n2) && pathEq p1 p2
    | PathRecd(p1, _, _, n1), PathRecd(p2, _, _, n2) -> (n1 = n2) && pathEq p1 p2
    | PathUnionConstr(p1, _, _, n1), PathUnionConstr(p2, _, _, n2) -> (n1 = n2) && pathEq p1 p2
    | PathArray(p1, _, _, n1), PathArray(p2, _, _, n2) -> (n1 = n2) && pathEq p1 p2
    | PathExnConstr(p1, _, n1), PathExnConstr(p2, _, n2) -> (n1 = n2) && pathEq p1 p2
    | PathEmpty _, PathEmpty _ -> true
    | _ -> false


//---------------------------------------------------------------------------
// Counter example generation
//---------------------------------------------------------------------------

type RefutedSet =
    /// A value RefutedInvestigation(path, discrim) indicates that the value at the given path is known
    /// to NOT be matched by the given discriminator
    | RefutedInvestigation of Path * DecisionTreeTest list
    /// A value RefutedWhenClause indicates that a 'when' clause failed
    | RefutedWhenClause

let notNullText = "some-non-null-value"
let otherSubtypeText = "some-other-subtype"

/// Create a TAST const value from an IL-initialized field read from .NET metadata
// (Originally moved from TcFieldInit in CheckExpressions.fs -- feel free to move this somewhere more appropriate)
let ilFieldToTastConst lit =
    match lit with
    | ILFieldInit.String s -> Const.String s
    | ILFieldInit.Null -> Const.Zero
    | ILFieldInit.Bool b -> Const.Bool b
    | ILFieldInit.Char c -> Const.Char (char (int c))
    | ILFieldInit.Int8 x -> Const.SByte x
    | ILFieldInit.Int16 x -> Const.Int16 x
    | ILFieldInit.Int32 x -> Const.Int32 x
    | ILFieldInit.Int64 x -> Const.Int64 x
    | ILFieldInit.UInt8 x -> Const.Byte x
    | ILFieldInit.UInt16 x -> Const.UInt16 x
    | ILFieldInit.UInt32 x -> Const.UInt32 x
    | ILFieldInit.UInt64 x -> Const.UInt64 x
    | ILFieldInit.Single f -> Const.Single f
    | ILFieldInit.Double f -> Const.Double f

exception CannotRefute
let RefuteDiscrimSet g m path discrims =
    let mkUnknown ty = snd(mkCompGenLocal m "_" ty)
    let rec go path tm =
        match path with
        | PathQuery _ -> raise CannotRefute
        | PathConj (p, _j) ->
            go p tm
        | PathTuple (p, tys, j) ->
            let k, eCoversVals = mkOneKnown tm j tys
            go p (fun _ -> mkRefTupled g m k tys, eCoversVals)
        | PathRecd (p, tcref, tinst, j) ->
            let flds, eCoversVals = tcref |> actualTysOfInstanceRecdFields (mkTyconRefInst tcref tinst) |> mkOneKnown tm j
            go p (fun _ -> Expr.Op (TOp.Recd (RecdExpr, tcref), tinst, flds, m), eCoversVals)

        | PathUnionConstr (p, ucref, tinst, j) ->
            let flds, eCoversVals = ucref |> actualTysOfUnionCaseFields (mkTyconRefInst ucref.TyconRef tinst)|> mkOneKnown tm j
            go p (fun _ -> Expr.Op (TOp.UnionCase ucref, tinst, flds, m), eCoversVals)

        | PathArray (p, ty, len, n) ->
            let flds, eCoversVals = mkOneKnown tm n (List.replicate len ty)
            go p (fun _ -> Expr.Op (TOp.Array, [ty], flds, m), eCoversVals)

        | PathExnConstr (p, ecref, n) ->
            let flds, eCoversVals = ecref |> recdFieldTysOfExnDefRef |> mkOneKnown tm n
            go p (fun _ -> Expr.Op (TOp.ExnConstr ecref, [], flds, m), eCoversVals)

        | PathEmpty ty -> tm ty

    and mkOneKnown tm n tys =
        let flds = List.mapi (fun i ty -> if i = n then tm ty else (mkUnknown ty, false)) tys
        List.map fst flds, List.fold (fun acc (_, eCoversVals) -> eCoversVals || acc) false flds
    and mkUnknowns tys = List.map (fun x -> mkUnknown x) tys

    let tm ty =
        match discrims with
        | [DecisionTreeTest.IsNull] ->
            snd(mkCompGenLocal m notNullText ty), false
        | [DecisionTreeTest.IsInst (_, _)] ->
            snd(mkCompGenLocal m otherSubtypeText ty), false
        | (DecisionTreeTest.Const c :: rest) ->
            let consts = Set.ofList (c :: List.choose (function DecisionTreeTest.Const c -> Some c | _ -> None) rest)
            let c' =
                Seq.tryFind (fun c -> not (consts.Contains c))
                   (match c with
                    | Const.Bool _ -> [ true; false ] |> List.toSeq |> Seq.map (fun v -> Const.Bool v)
                    | Const.SByte _ ->  Seq.append (seq { 0y .. System.SByte.MaxValue }) (seq { System.SByte.MinValue .. 0y })|> Seq.map (fun v -> Const.SByte v)
                    | Const.Int16 _ -> Seq.append (seq { 0s .. System.Int16.MaxValue }) (seq { System.Int16.MinValue .. 0s }) |> Seq.map (fun v -> Const.Int16 v)
                    | Const.Int32 _ ->  Seq.append (seq { 0 .. System.Int32.MaxValue }) (seq { System.Int32.MinValue .. 0 })|> Seq.map (fun v -> Const.Int32 v)
                    | Const.Int64 _ ->  Seq.append (seq { 0L .. System.Int64.MaxValue }) (seq { System.Int64.MinValue .. 0L })|> Seq.map (fun v -> Const.Int64 v)
                    | Const.IntPtr _ ->  Seq.append (seq { 0L .. System.Int64.MaxValue }) (seq { System.Int64.MinValue .. 0L })|> Seq.map (fun v -> Const.IntPtr v)
                    | Const.Byte _ -> seq { 0uy .. System.Byte.MaxValue } |> Seq.map (fun v -> Const.Byte v)
                    | Const.UInt16 _ -> seq { 0us .. System.UInt16.MaxValue } |> Seq.map (fun v -> Const.UInt16 v)
                    | Const.UInt32 _ -> seq { 0u .. System.UInt32.MaxValue } |> Seq.map (fun v -> Const.UInt32 v)
                    | Const.UInt64 _ -> seq { 0UL .. System.UInt64.MaxValue } |> Seq.map (fun v -> Const.UInt64 v)
                    | Const.UIntPtr _ -> seq { 0UL .. System.UInt64.MaxValue } |> Seq.map (fun v -> Const.UIntPtr v)
                    | Const.Double _ -> seq { 0 .. System.Int32.MaxValue } |> Seq.map (fun v -> Const.Double(float v))
                    | Const.Single _ -> seq { 0 .. System.Int32.MaxValue } |> Seq.map (fun v -> Const.Single(float32 v))
                    | Const.Char _ -> seq { 32us .. System.UInt16.MaxValue } |> Seq.map (fun v -> Const.Char(char v))
                    | Const.String _ -> seq { 1 .. System.Int32.MaxValue } |> Seq.map (fun v -> Const.String(new System.String('a', v)))
                    | Const.Decimal _ -> seq { 1 .. System.Int32.MaxValue } |> Seq.map (fun v -> Const.Decimal(decimal v))
                    | _ ->
                        raise CannotRefute)

            match c' with
            | None -> raise CannotRefute
            | Some c ->
                match tryTcrefOfAppTy g ty with
                | ValueSome tcref when tcref.IsEnumTycon ->
                    // We must distinguish between F#-defined enums and other .NET enums, as they are represented differently in the TAST
                    let enumValues =
                        if tcref.IsILEnumTycon then
                            let (TILObjectReprData(_, _, tdef)) = tcref.ILTyconInfo
                            tdef.Fields.AsList
                            |> Seq.choose (fun ilField ->
                                if ilField.IsStatic then
                                    ilField.LiteralValue |> Option.map (fun ilValue ->
                                        ilField.Name, ilFieldToTastConst ilValue)
                                else None)
                        else
                            tcref.AllFieldsArray |> Seq.choose (fun fsField ->
                                match fsField.rfield_const, fsField.rfield_static with
                                | Some fsFieldValue, true -> Some (fsField.rfield_id.idText, fsFieldValue)
                                | _ -> None)

                    let nonCoveredEnumValues = Seq.tryFind (fun (_, fldValue) -> not (consts.Contains fldValue)) enumValues

                    match nonCoveredEnumValues with
                    | None -> Expr.Const (c, m, ty), true
                    | Some (fldName, _) ->
                        let v = RecdFieldRef.RecdFieldRef(tcref, fldName)
                        Expr.Op (TOp.ValFieldGet v, [ty], [], m), false
                | _ -> Expr.Const (c, m, ty), false

        | (DecisionTreeTest.UnionCase (ucref1, tinst) :: rest) ->
            let ucrefs = ucref1 :: List.choose (function DecisionTreeTest.UnionCase(ucref, _) -> Some ucref | _ -> None) rest
            let tcref = ucref1.TyconRef
            (* Choose the first ucref based on ordering of names *)
            let others =
                tcref.UnionCasesAsRefList
                |> List.filter (fun ucref -> not (List.exists (g.unionCaseRefEq ucref) ucrefs))
                |> List.sortBy (fun ucref -> ucref.CaseName)
            match others with
            | [] -> raise CannotRefute
            | ucref2 :: _ ->
                let flds = ucref2 |> actualTysOfUnionCaseFields (mkTyconRefInst tcref tinst) |> mkUnknowns
                Expr.Op (TOp.UnionCase ucref2, tinst, flds, m), false

        | [DecisionTreeTest.ArrayLength (n, ty)] ->
            Expr.Op (TOp.Array, [ty], mkUnknowns (List.replicate (n+1) ty), m), false

        | _ ->
            raise CannotRefute
    go path tm

let rec CombineRefutations g r1 r2 =
    match r1, r2 with
    | Expr.Val (vref, _, _), other | other, Expr.Val (vref, _, _) when vref.LogicalName = "_" -> other
    | Expr.Val (vref, _, _), other | other, Expr.Val (vref, _, _) when vref.LogicalName = notNullText -> other
    | Expr.Val (vref, _, _), other | other, Expr.Val (vref, _, _) when vref.LogicalName = otherSubtypeText -> other

    | Expr.Op ((TOp.ExnConstr ecref1 as op1), tinst1, flds1, m1), Expr.Op (TOp.ExnConstr ecref2, _, flds2, _) when tyconRefEq g ecref1 ecref2 ->
        Expr.Op (op1, tinst1, List.map2 (CombineRefutations g) flds1 flds2, m1)

    | Expr.Op ((TOp.UnionCase ucref1 as op1), tinst1, flds1, m1), Expr.Op (TOp.UnionCase ucref2, _, flds2, _) ->
        if g.unionCaseRefEq ucref1 ucref2 then
            Expr.Op (op1, tinst1, List.map2 (CombineRefutations g) flds1 flds2, m1)
        (* Choose the greater of the two ucrefs based on name ordering *)
        elif ucref1.CaseName < ucref2.CaseName then
            r2
        else
            r1

    | Expr.Op (op1, tinst1, flds1, m1), Expr.Op (_, _, flds2, _) ->
        Expr.Op (op1, tinst1, List.map2 (CombineRefutations g) flds1 flds2, m1)

    | Expr.Const (c1, m1, ty1), Expr.Const (c2, _, _) ->
        let c12 =

            // Make sure longer strings are greater, not the case in the default ordinal comparison
            // This is needed because the individual counter examples make longer strings
            let MaxStrings s1 s2 =
                let c = compare (String.length s1) (String.length s2)
                if c < 0 then s2
                elif c > 0 then s1
                elif s1 < s2 then s2
                else s1

            match c1, c2 with
            | Const.String s1, Const.String s2 -> Const.String(MaxStrings s1 s2)
            | Const.Decimal s1, Const.Decimal s2 -> Const.Decimal(max s1 s2)
            | _ -> max c1 c2

        Expr.Const (c12, m1, ty1)

    | _ -> r1

let ShowCounterExample g denv m refuted =
    try
        let exprL expr = exprL g expr
        let refutations = refuted |> List.collect (function RefutedWhenClause -> [] | (RefutedInvestigation(path, discrim)) -> [RefuteDiscrimSet g m path discrim])
        let counterExample, enumCoversKnown =
            match refutations with
            | [] -> raise CannotRefute
            | (r, eck) :: t ->
                if verbose then dprintf "r = %s (enumCoversKnownValue = %b)\n" (LayoutRender.showL (exprL r)) eck
                List.fold (fun (rAcc, eckAcc) (r, eck) ->
                    CombineRefutations g rAcc r, eckAcc || eck) (r, eck) t
        let text = LayoutRender.showL (NicePrint.dataExprL denv counterExample)
        let failingWhenClause = refuted |> List.exists (function RefutedWhenClause -> true | _ -> false)
        Some(text, failingWhenClause, enumCoversKnown)

    with
    | CannotRefute ->
        None
    | e ->
        warning(InternalError(sprintf "<failure during counter example generation: %s>" (e.ToString()), m))
        None

//---------------------------------------------------------------------------
// Basic problem specification
//---------------------------------------------------------------------------

type RuleNumber = int

type Active = Active of Path * SubExprOfInput * Pattern

type Actives = Active list

type Frontier = Frontier of RuleNumber * Actives * ValMap<Expr>

type InvestigationPoint = Investigation of RuleNumber * DecisionTreeTest * Path

// Note: actives must be a SortedDictionary
// REVIEW: improve these data structures, though surprisingly these functions don't tend to show up
// on profiling runs
let rec isMemOfActives p1 actives =
    match actives with
    | [] -> false
    | (Active(p2, _, _)) :: rest -> pathEq p1 p2 || isMemOfActives p1 rest

let rec lookupActive x l =
    match l with
    | [] -> raise (KeyNotFoundException())
    | (Active(h, r1, r2) :: t) -> if pathEq x h then (r1, r2) else lookupActive x t

let rec removeActive x l =
    match l with
    | [] -> []
    | ((Active(h, _, _) as p) :: t) -> if pathEq x h then t else p :: removeActive x t

//---------------------------------------------------------------------------
// Utilities
//---------------------------------------------------------------------------

// tpinst is required because the pattern is specified w.r.t. generalized type variables.
let getDiscrimOfPattern (g: TcGlobals) tpinst t =
    match t with
    | TPat_null _m ->
        Some(DecisionTreeTest.IsNull)
    | TPat_isinst (srcty, tgty, _, _m) ->
        Some(DecisionTreeTest.IsInst (instType tpinst srcty, instType tpinst tgty))
    | TPat_exnconstr(tcref, _, _m) ->
        Some(DecisionTreeTest.IsInst (g.exn_ty, mkAppTy tcref []))
    | TPat_const (c, _m) ->
        Some(DecisionTreeTest.Const c)
    | TPat_unioncase (c, tyargs', _, _m) ->
        Some(DecisionTreeTest.UnionCase (c, instTypes tpinst tyargs'))
    | TPat_array (args, ty, _m) ->
        Some(DecisionTreeTest.ArrayLength (args.Length, ty))
    | TPat_query ((activePatExpr, resTys, isStructRetTy, apatVrefOpt, idx, apinfo), _, _m) ->
        Some (DecisionTreeTest.ActivePatternCase (activePatExpr, instTypes tpinst resTys, isStructRetTy, apatVrefOpt, idx, apinfo))

    | TPat_error range ->
        Some (DecisionTreeTest.Error range)

    | _ -> None

let constOfDiscrim discrim =
    match discrim with
    | DecisionTreeTest.Const x -> x
    | _ -> failwith "not a const case"

let constOfCase (c: DecisionTreeCase) = constOfDiscrim c.Discriminator

/// Compute pattern identity
let discrimsEq (g: TcGlobals) d1 d2 =
  match d1, d2 with
  | DecisionTreeTest.UnionCase (c1, _),    DecisionTreeTest.UnionCase(c2, _) -> g.unionCaseRefEq c1 c2
  | DecisionTreeTest.ArrayLength (n1, _),   DecisionTreeTest.ArrayLength(n2, _) -> (n1=n2)
  | DecisionTreeTest.Const c1,              DecisionTreeTest.Const c2 -> (c1=c2)
  | DecisionTreeTest.IsNull,               DecisionTreeTest.IsNull -> true
  | DecisionTreeTest.IsInst (srcty1, tgty1), DecisionTreeTest.IsInst (srcty2, tgty2) -> typeEquiv g srcty1 srcty2 && typeEquiv g tgty1 tgty2
  | DecisionTreeTest.ActivePatternCase (_, _, _, vrefOpt1, n1, _), DecisionTreeTest.ActivePatternCase (_, _, _, vrefOpt2, n2, _) ->
      match vrefOpt1, vrefOpt2 with
      | Some (vref1, tinst1), Some (vref2, tinst2) -> valRefEq g vref1 vref2 && n1 = n2  && not (doesActivePatternHaveFreeTypars g vref1) && List.lengthsEqAndForall2 (typeEquiv g) tinst1 tinst2
      | _ -> false (* for equality purposes these are considered unequal! This is because adhoc computed patterns have no identity. *)

  | _ -> false

/// Redundancy of 'isinst' patterns
let isDiscrimSubsumedBy g amap m d1 d2 =
    (discrimsEq g d1 d2)
    ||
    (match d1, d2 with
     | DecisionTreeTest.IsInst (_, tgty1), DecisionTreeTest.IsInst (_, tgty2) ->
        TypeDefinitelySubsumesTypeNoCoercion 0 g amap m tgty2 tgty1
     | _ -> false)

/// Choose a set of investigations that can be performed simultaneously
let rec chooseSimultaneousEdgeSet prevOpt f l =
    match l with
    | [] -> [], []
    | h :: t ->
        match f prevOpt h with
        | Some x ->
             let l, r = chooseSimultaneousEdgeSet (Some x) f t
             x :: l, r
        | None ->
             let l, r = chooseSimultaneousEdgeSet prevOpt f t
             l, h :: r

/// Can we represent a integer discrimination as a 'switch'
let canCompactConstantClass c =
    match c with
    | Const.SByte _ | Const.Int16 _ | Const.Int32 _
    | Const.Byte _ | Const.UInt16 _ | Const.UInt32 _
    | Const.Char _ -> true
    | _ -> false

/// Can two discriminators in a 'column' be decided simultaneously?
let discrimsHaveSameSimultaneousClass g d1 d2 =
    match d1, d2 with
    | DecisionTreeTest.Const _, DecisionTreeTest.Const _
    | DecisionTreeTest.IsNull, DecisionTreeTest.IsNull
    | DecisionTreeTest.ArrayLength _, DecisionTreeTest.ArrayLength _
    | DecisionTreeTest.UnionCase _, DecisionTreeTest.UnionCase _  -> true
    | DecisionTreeTest.IsInst _, DecisionTreeTest.IsInst _ -> false
    | DecisionTreeTest.ActivePatternCase (_, _, _, apatVrefOpt1, _, _), DecisionTreeTest.ActivePatternCase (_, _, _, apatVrefOpt2, _, _) ->
        match apatVrefOpt1, apatVrefOpt2 with
        | Some (vref1, tinst1), Some (vref2, tinst2) -> valRefEq g vref1 vref2  && not (doesActivePatternHaveFreeTypars g vref1) && List.lengthsEqAndForall2 (typeEquiv g) tinst1 tinst2
        | _ -> false (* for equality purposes these are considered different classes of discriminators! This is because adhoc computed patterns have no identity! *)

    | _ -> false

let canInvestigate (pat: Pattern) =
    match pat with
    | TPat_null _ | TPat_isinst _ | TPat_exnconstr _ | TPat_unioncase _
    | TPat_array _ | TPat_const _ | TPat_query _ | TPat_range _ | TPat_error _ -> true
    | _ -> false

/// Decide the next pattern to investigate
let ChooseInvestigationPointLeftToRight frontiers =
    match frontiers with
    | Frontier (_i, actives, _) :: _t ->
        let rec choose l =
            match l with
            | [] -> failwith "ChooseInvestigationPointLeftToRight: no non-immediate patterns in first rule"
            | Active (_, _, pat) as active :: _ when canInvestigate pat -> active
            | _ :: t -> choose t
        choose actives
    | [] -> failwith "ChooseInvestigationPointLeftToRight: no frontiers!"



#if OPTIMIZE_LIST_MATCHING
// This is an initial attempt to remove extra typetests/castclass for simple list pattern matching "match x with h :: t -> ... | [] -> ..."
// The problem with this technique is that it creates extra locals which inhibit the process of converting pattern matches into linear let bindings.

let (|ListConsDiscrim|_|) g = function
     | (DecisionTreeTest.UnionCase (ucref, tinst))
                (* check we can use a simple 'isinst' instruction *)
                when tyconRefEq g ucref.TyconRef g.list_tcr_canon & ucref.CaseName = "op_ColonColon" -> Some tinst
     | _ -> None

let (|ListEmptyDiscrim|_|) g = function
     | (DecisionTreeTest.UnionCase (ucref, tinst))
                (* check we can use a simple 'isinst' instruction *)
                when tyconRefEq g ucref.TyconRef g.list_tcr_canon & ucref.CaseName = "op_Nil" -> Some tinst
     | _ -> None
#endif

let (|ConstNeedsDefaultCase|_|) c =
    match c with
    | Const.Decimal _
    | Const.String _
    | Const.Single _
    | Const.Double _
    | Const.Int16 _
    | Const.UInt16 _
    | Const.Int32 _
    | Const.UInt32 _
    | Const.Int64 _
    | Const.UInt64 _
    | Const.IntPtr _
    | Const.UIntPtr _
    | Const.Char _ -> Some ()
    | _ -> None

/// Build a dtree, equivalent to: TDSwitch("expr", edges, default, m)
///
/// Once we've chosen a particular active to investigate, we compile the
/// set of edges affected by this investigation into a switch.
///
///   - For DecisionTreeTest.ActivePatternCase(..., None, ...) there is only one edge
///
///   - For DecisionTreeTest.IsInst there are multiple edges, which we can't deal with
///     one switch, so we make an iterated if-then-else to cover the cases. We
///     should probably adjust the code to only choose one edge in this case.
///
///   - Compact integer switches become a single switch.  Non-compact integer
///     switches, string switches and floating point switches are treated in the
///     same way as DecisionTreeTest.IsInst.
let rec BuildSwitch inpExprOpt g expr edges dflt m =
    if verbose then dprintf "--> BuildSwitch@%a, #edges = %A, dflt.IsSome = %A\n" outputRange m (List.length edges) (Option.isSome dflt)
    match edges, dflt with
    | [], None      -> failwith "internal error: no edges and no default"
    | [], Some dflt -> dflt      (* NOTE: first time around, edges<>[] *)

    // Optimize the case where the match always succeeds
    | [TCase(_, tree)], None -> tree

    // 'isinst' tests where we have stored the result of the 'isinst' in a variable
    // In this case the 'expr' already holds the result of the 'isinst' test.

    | (TCase(DecisionTreeTest.IsInst _, success)) :: edges, dflt  when Option.isSome inpExprOpt ->
        TDSwitch(expr, [TCase(DecisionTreeTest.IsNull, BuildSwitch None g expr edges dflt m)], Some success, m)

    // isnull and isinst tests
    | (TCase((DecisionTreeTest.IsNull | DecisionTreeTest.IsInst _), _) as edge) :: edges, dflt  ->
        TDSwitch(expr, [edge], Some (BuildSwitch inpExprOpt g expr edges dflt m), m)

#if OPTIMIZE_LIST_MATCHING
    // 'cons/nil' tests where we have stored the result of the cons test in an 'isinst' in a variable
    // In this case the 'expr' already holds the result of the 'isinst' test.
    | [TCase(ListConsDiscrim g tinst, consCase)], Some emptyCase
    | [TCase(ListEmptyDiscrim g tinst, emptyCase)], Some consCase
    | [TCase(ListEmptyDiscrim g _, emptyCase); TCase(ListConsDiscrim g tinst, consCase)], None
    | [TCase(ListConsDiscrim g tinst, consCase); TCase(ListEmptyDiscrim g _, emptyCase)], None
                     when Option.isSome inpExprOpt ->
        TDSwitch(expr, [TCase(DecisionTreeTest.IsNull, emptyCase)], Some consCase, m)
#endif

    // All these should also always have default cases
    | (TCase(DecisionTreeTest.Const ConstNeedsDefaultCase, _) :: _), None ->
        error(InternalError("inexhaustive match - need a default case!", m))

    // Split string, float, uint64, int64, unativeint, nativeint matches into serial equality tests
    | TCase((DecisionTreeTest.ArrayLength _ | DecisionTreeTest.Const (Const.Single _ | Const.Double _ | Const.String _ | Const.Decimal _ | Const.Int64 _ | Const.UInt64 _ | Const.IntPtr _ | Const.UIntPtr _)), _) :: _, Some dflt ->
        List.foldBack
            (fun (TCase(discrim, tree)) sofar ->
                let testexpr = expr
                let testexpr =
                    match discrim with
                    | DecisionTreeTest.ArrayLength(n, _)       ->
                        let _v, vExpr, bind = mkCompGenLocalAndInvisibleBind g "testExpr" m testexpr
                        mkLetBind m bind (mkLazyAnd g m (mkNonNullTest g m vExpr) (mkILAsmCeq g m (mkLdlen g m vExpr) (mkInt g m n)))
                    | DecisionTreeTest.Const (Const.String _ as c)  ->
                        mkCallEqualsOperator g m g.string_ty testexpr (Expr.Const (c, m, g.string_ty))
                    | DecisionTreeTest.Const (Const.Decimal _ as c)  ->
                        mkCallEqualsOperator g m g.decimal_ty testexpr (Expr.Const (c, m, g.decimal_ty))
                    | DecisionTreeTest.Const ((Const.Double _ | Const.Single _ | Const.Int64 _ | Const.UInt64 _ | Const.IntPtr _ | Const.UIntPtr _) as c)   ->
                        mkILAsmCeq g m testexpr (Expr.Const (c, m, tyOfExpr g testexpr))
                    | _ -> error(InternalError("strange switch", m))
                mkBoolSwitch m testexpr tree sofar)
          edges
          dflt

    // Split integer and char matches into compact fragments which will themselves become switch statements.
    | TCase(DecisionTreeTest.Const c, _) :: _, Some dflt when canCompactConstantClass c ->
        let edgeCompare c1 c2 =
            match constOfCase c1, constOfCase c2 with
            | (Const.SByte i1), (Const.SByte i2) -> compare i1 i2
            | (Const.Int16 i1), (Const.Int16 i2) -> compare i1 i2
            | (Const.Int32 i1), (Const.Int32 i2) -> compare i1 i2
            | (Const.Byte i1), (Const.Byte i2) -> compare i1 i2
            | (Const.UInt16 i1), (Const.UInt16 i2) -> compare i1 i2
            | (Const.UInt32 i1), (Const.UInt32 i2) -> compare i1 i2
            | (Const.Char c1), (Const.Char c2) -> compare c1 c2
            | _ -> failwith "illtyped term during pattern compilation"
        let edges' = List.sortWith edgeCompare edges
        let rec compactify curr edges =
            match curr, edges with
            | None, [] -> []
            | Some last, [] -> [List.rev last]
            | None, h :: t -> compactify (Some [h]) t
            | Some (prev :: moreprev), h :: t ->
                match constOfCase prev, constOfCase h with
                | Const.SByte iprev, Const.SByte inext when int32 iprev + 1 = int32 inext ->
                    compactify (Some (h :: prev :: moreprev)) t
                | Const.Int16 iprev, Const.Int16 inext when int32 iprev + 1 = int32 inext ->
                    compactify (Some (h :: prev :: moreprev)) t
                | Const.Int32 iprev, Const.Int32 inext when iprev+1 = inext ->
                    compactify (Some (h :: prev :: moreprev)) t
                | Const.Byte iprev, Const.Byte inext when int32 iprev + 1 = int32 inext ->
                    compactify (Some (h :: prev :: moreprev)) t
                | Const.UInt16 iprev, Const.UInt16 inext when int32 iprev+1 = int32 inext ->
                    compactify (Some (h :: prev :: moreprev)) t
                | Const.UInt32 iprev, Const.UInt32 inext when int32 iprev+1 = int32 inext ->
                    compactify (Some (h :: prev :: moreprev)) t
                | Const.Char cprev, Const.Char cnext when (int32 cprev + 1 = int32 cnext) ->
                    compactify (Some (h :: prev :: moreprev)) t
                |       _ ->  (List.rev (prev :: moreprev)) :: compactify None edges

            | _ -> failwith "internal error: compactify"
        let edgeGroups = compactify None edges'
        (edgeGroups, dflt) ||> List.foldBack (fun edgeGroup sofar ->  TDSwitch(expr, edgeGroup, Some sofar, m))

    // For a total pattern match, run the active pattern, bind the result and
    // recursively build a switch in the choice type
    | (TCase(DecisionTreeTest.ActivePatternCase _, _) :: _), _ ->
       error(InternalError("DecisionTreeTest.ActivePatternCase should have been eliminated", m))

    // For a complete match, optimize one test to be the default
    | (TCase(_, tree) :: rest), None -> TDSwitch (expr, rest, Some tree, m)

    // Otherwise let codegen make the choices
    | _ -> TDSwitch (expr, edges, dflt, m)

#if DEBUG
let rec layoutPat pat =
    match pat with
    | TPat_query (_, pat, _) -> Layout.(--) (Layout.wordL (TaggedText.tagText "query")) (layoutPat pat)
    | TPat_wild _ -> Layout.wordL (TaggedText.tagText "wild")
    | TPat_as _ -> Layout.wordL (TaggedText.tagText "var")
    | TPat_tuple (_, pats, _, _)
    | TPat_array (pats, _, _) -> Layout.bracketL (Layout.tupleL (List.map layoutPat pats))
    | _ -> Layout.wordL (TaggedText.tagText "?")

let layoutPath _p = Layout.wordL (TaggedText.tagText "<path>")

let layoutActive (Active (path, _subexpr, pat)) =
    Layout.(--) (Layout.wordL (TaggedText.tagText "Active")) (Layout.tupleL [layoutPath path; layoutPat pat])

let layoutFrontier (Frontier (i, actives, _)) =
    Layout.(--) (Layout.wordL (TaggedText.tagText "Frontier ")) (Layout.tupleL [intL i; Layout.listL layoutActive actives])
#endif

let mkFrontiers investigations i =
    List.map (fun (actives, valMap) -> Frontier(i, actives, valMap)) investigations

let getRuleIndex (Frontier (i, _active, _valMap)) = i

/// Is a pattern a partial pattern?
let rec isPatternPartial p =
    match p with
    | TPat_query ((_, _, _, _, _, apinfo), p, _m) -> not apinfo.IsTotal || isPatternPartial p
    | TPat_const _ -> false
    | TPat_wild _ -> false
    | TPat_as (p, _, _) -> isPatternPartial p
    | TPat_disjs (ps, _) | TPat_conjs(ps, _)
    | TPat_tuple (_, ps, _, _) | TPat_exnconstr(_, ps, _)
    | TPat_array (ps, _, _) | TPat_unioncase (_, _, ps, _)
    | TPat_recd (_, _, ps, _) -> List.exists isPatternPartial ps
    | TPat_range _ -> false
    | TPat_null _ -> false
    | TPat_isinst _ -> false
    | TPat_error _ -> false

let rec erasePartialPatterns inpp =
    match inpp with
    | TPat_query ((expr, resTys, isStructRetTy, apatVrefOpt, idx, apinfo), p, m) ->
         if apinfo.IsTotal then TPat_query ((expr, resTys, isStructRetTy, apatVrefOpt, idx, apinfo), erasePartialPatterns p, m)
         else TPat_disjs ([], m) (* always fail *)
    | TPat_as (p, x, m) -> TPat_as (erasePartialPatterns p, x, m)
    | TPat_disjs (ps, m) -> TPat_disjs(erasePartials ps, m)
    | TPat_conjs(ps, m) -> TPat_conjs(erasePartials ps, m)
    | TPat_tuple (tupInfo, ps, x, m) -> TPat_tuple(tupInfo, erasePartials ps, x, m)
    | TPat_exnconstr(x, ps, m) -> TPat_exnconstr(x, erasePartials ps, m)
    | TPat_array (ps, x, m) -> TPat_array (erasePartials ps, x, m)
    | TPat_unioncase (x, y, ps, m) -> TPat_unioncase (x, y, erasePartials ps, m)
    | TPat_recd (x, y, ps, m) -> TPat_recd (x, y, List.map erasePartialPatterns ps, m)
    | TPat_const _
    | TPat_wild _
    | TPat_range _
    | TPat_null _
    | TPat_isinst _
    | TPat_error _ -> inpp

and erasePartials inps =
    List.map erasePartialPatterns inps


//---------------------------------------------------------------------------
// The algorithm
//---------------------------------------------------------------------------

type EdgeDiscrim = EdgeDiscrim of int * DecisionTreeTest * range
let getDiscrim (EdgeDiscrim(_, discrim, _)) = discrim


let CompilePatternBasic
        (g: TcGlobals) denv amap tcVal infoReader exprm matchm
        warnOnUnused
        warnOnIncomplete
        actionOnFailure
        (origInputVal, origInputValTypars, _origInputExprOpt: Expr option)
        (typedClauses: TypedMatchClause list)
        inputTy
        resultTy =
    // Add the targets to a match builder.
    // Note the input expression has already been evaluated and saved into a variable,
    // hence no need for a new sequence point.
    let matchBuilder = MatchBuilder (DebugPointAtBinding.NoneAtInvisible, exprm)
    typedClauses |> List.iter (fun c -> matchBuilder.AddTarget c.Target |> ignore)

    // Add the incomplete or rethrow match clause on demand,
    // printing a warning if necessary (only if it is ever exercised).
    let mutable incompleteMatchClauseOnce = None
    let getIncompleteMatchClause refuted =
        // This is lazy because emit a warning when the lazy thunk gets evaluated.
        match incompleteMatchClauseOnce with
        | None ->
            // Emit the incomplete match warning. 
            if warnOnIncomplete then
                match actionOnFailure with
                | ThrowIncompleteMatchException | IgnoreWithWarning ->
                    let ignoreWithWarning = (actionOnFailure = IgnoreWithWarning)
                    match ShowCounterExample g denv matchm refuted with
                    | Some(text, failingWhenClause, true) ->
                        warning (EnumMatchIncomplete(ignoreWithWarning, Some(text, failingWhenClause), matchm))
                    | Some(text, failingWhenClause, false) ->
                        warning (MatchIncomplete(ignoreWithWarning, Some(text, failingWhenClause), matchm))
                    | None ->
                        warning (MatchIncomplete(ignoreWithWarning, None, matchm))
                | _ ->
                     ()

            let throwExpr =
                match actionOnFailure with
                | FailFilter  ->
                    // Return 0 from the .NET exception filter.
                    mkInt g matchm 0

                | Rethrow ->
                    // Rethrow unmatched try-with exn. No sequence point at the target since its not real code.
                    mkReraise matchm resultTy

                | Throw ->
                    let findMethInfo ty isInstance name (sigTys: TType list) =
                        TryFindIntrinsicMethInfo infoReader matchm (AccessorDomain.AccessibleFromEverywhere) name ty
                        |> List.tryFind (fun methInfo ->
                            methInfo.IsInstance = isInstance &&
                            (
                                match methInfo.GetParamTypes(amap, matchm, []) with
                                | [] -> false
                                | argTysList ->
                                    let argTys = (argTysList |> List.reduce (@)) @ [ methInfo.GetFSharpReturnTy (amap, matchm, []) ]
                                    if argTys.Length <> sigTys.Length then
                                        false
                                    else
                                        (argTys, sigTys)
                                        ||> List.forall2 (typeEquiv g)
                            )
                        )

                    // We use throw, or EDI.Capture(exn).Throw() when EDI is supported, instead of rethrow on unmatched try-with in a computation expression.
                    // But why? Because this isn't a real .NET exception filter/handler but just a function we're passing
                    // to a computation expression builder to simulate one.
                    let ediCaptureMethInfo, ediThrowMethInfo =
                        // EDI.Capture: exn -> EDI
                        g.system_ExceptionDispatchInfo_ty
                        |> Option.bind (fun ty -> findMethInfo ty false "Capture" [ g.exn_ty; ty ]),
                        // edi.Throw: unit -> unit
                        g.system_ExceptionDispatchInfo_ty
                        |> Option.bind (fun ty -> findMethInfo ty true "Throw" [ g.unit_ty ])

                    match Option.map2 (fun x y -> x,y) ediCaptureMethInfo ediThrowMethInfo with
                    | None ->
                        mkThrow matchm resultTy (exprForVal matchm origInputVal)
                    | Some (ediCaptureMethInfo, ediThrowMethInfo) ->
                        let (edi, _) =
                            BuildMethodCall tcVal g amap NeverMutates matchm false
                               ediCaptureMethInfo ValUseFlag.NormalValUse [] [] [ (exprForVal matchm origInputVal) ]

                        let (e, _) =
                            BuildMethodCall tcVal g amap NeverMutates matchm false
                                ediThrowMethInfo ValUseFlag.NormalValUse [] [edi] [ ]

                        mkCompGenSequential matchm e (mkDefault (matchm, resultTy))

                | ThrowIncompleteMatchException ->
                    mkThrow matchm resultTy
                        (mkExnExpr(mk_MFCore_tcref g.fslibCcu "MatchFailureException",
                                   [ mkString g matchm matchm.FileName
                                     mkInt g matchm matchm.StartLine
                                     mkInt g matchm matchm.StartColumn], matchm))

                | IgnoreWithWarning ->
                    mkUnit g matchm

            // We don't emit a sequence point at any of the above cases because they don't correspond to user code.
            //
            // Note we don't emit sequence points at either the succeeding or failing targets of filters since if
            // the exception is filtered successfully then we will run the handler and hit the sequence point there.
            // That sequence point will have the pattern variables bound, which is exactly what we want.
            let tg = TTarget(List.empty, throwExpr, DebugPointForTarget.No)
            let _ = matchBuilder.AddTarget tg
            let clause = TClause(TPat_wild matchm, None, tg, matchm)
            incompleteMatchClauseOnce <- Some clause
            clause

        | Some c -> c

    // Helpers to get the variables bound at a target.
    // We conceptually add a dummy clause that will always succeed with a "throw".
    let clausesA = Array.ofList typedClauses
    let nClauses = clausesA.Length
    let GetClause i refuted =
        if i < nClauses then
            clausesA.[i]
        elif i = nClauses then getIncompleteMatchClause refuted
        else failwith "GetClause"
    let GetValsBoundByClause i refuted = (GetClause i refuted).BoundVals
    let GetWhenGuardOfClause i refuted = (GetClause i refuted).GuardExpr

    // Different uses of parameterized active patterns have different identities as far as paths are concerned.
    // Here we generate unique numbers that are completely different to any stamp by using negative numbers.
    let genUniquePathId() = - (newUnique())

    // Build versions of these functions which apply a dummy instantiation to the overall type arguments.
    let GetSubExprOfInput, getDiscrimOfPattern =
        let tyargs = List.map (fun _ -> g.unit_ty) origInputValTypars
        let unit_tpinst = mkTyparInst origInputValTypars tyargs
        GetSubExprOfInput g (origInputValTypars, tyargs, unit_tpinst),
        getDiscrimOfPattern g unit_tpinst

    // The main recursive loop of the pattern match compiler.
    let rec InvestigateFrontiers refuted frontiers =
        match frontiers with
        | [] -> failwith "CompilePattern: compile - empty clauses: at least the final clause should always succeed"
        | Frontier (i, active, valMap) :: rest ->

            // Check to see if we've got a succeeding clause. There may still be a 'when' condition for the clause.
            match active with
            | [] -> CompileSuccessPointAndGuard i refuted valMap rest

            | _ ->
                 // Otherwise choose a point (i.e. a path) to investigate.
                let (Active(path, subexpr, pat))  = ChooseInvestigationPointLeftToRight frontiers
                if not (canInvestigate pat) then
                    // All these constructs should have been eliminated in BindProjectionPattern
                    failwith "Unexpected pattern"
                else
                    let simulSetOfEdgeDiscrims, fallthroughPathFrontiers = ChooseSimultaneousEdges frontiers path

                    let inpExprOpt, bindOpt =     ChoosePreBinder simulSetOfEdgeDiscrims subexpr

                    // For each case, recursively compile the residue decision trees that result if that case successfully matches
                    let simulSetOfCases, _ = CompileSimultaneousSet frontiers path refuted subexpr simulSetOfEdgeDiscrims inpExprOpt

                    assert (not (isNil simulSetOfCases))

                    // Work out what the default/fall-through tree looks like, is any
                    // Check if match is complete, if so optimize the default case away.
                    let defaultTreeOpt = CompileFallThroughTree fallthroughPathFrontiers path refuted  simulSetOfCases

                    // OK, build the whole tree and whack on the binding if any
                    let finalDecisionTree =
                        let inpExprToSwitch = (match inpExprOpt with Some vExpr -> vExpr | None -> GetSubExprOfInput subexpr)
                        let tree = BuildSwitch inpExprOpt g inpExprToSwitch simulSetOfCases defaultTreeOpt matchm
                        match bindOpt with
                        | None -> tree
                        | Some bind -> TDBind (bind, tree)

                    finalDecisionTree

    and CompileSuccessPointAndGuard i refuted valMap rest =
        let vs2 = GetValsBoundByClause i refuted
        let es2 =
            vs2 |> List.map (fun v ->
                match valMap.TryFind v with
                | None -> mkUnit g v.Range
                | Some res -> res)
        let rhs' = TDSuccess(es2, i)
        match GetWhenGuardOfClause i refuted with
        | Some whenExpr ->

            let m = whenExpr.Range

            // SEQUENCE POINTS: REVIEW: Build a sequence point at 'when'
            let whenExpr = mkLetsFromBindings m (mkInvisibleBinds vs2 es2) whenExpr

            // We must duplicate both the bindings and the guard expression to ensure uniqueness of bound variables.
            // This is because guards and bindings can end up being compiled multiple times when "or" patterns are used.
            //
            // let whenExpr = copyExpr g CloneAll whenExpr
            //
            // However, we are not allowed to copy expressions until type checking is complete, because this
            // would lose recursive fixup points within the expressions (see FSharp 1.0 bug 4821).

            mkBoolSwitch m whenExpr rhs' (InvestigateFrontiers (RefutedWhenClause :: refuted) rest)

        | None -> rhs'

    /// Select the set of discriminators which we can handle in one test, or as a series of iterated tests,
    /// e.g. in the case of TPat_isinst. Ensure we only take at most one class of `TPat_query` at a time.
    /// Record the rule numbers so we know which rule the TPat_query cam from, so that when we project through
    /// the frontier we only project the right rule.
    and ChooseSimultaneousEdges frontiers path =
        frontiers |> chooseSimultaneousEdgeSet None (fun prevOpt (Frontier (i', active', _)) ->
            if isMemOfActives path active' then
                let _, p = lookupActive path active'
                match getDiscrimOfPattern p with
                | Some discrim ->
                    if (match prevOpt with None -> true | Some (EdgeDiscrim(_, discrimPrev, _)) -> discrimsHaveSameSimultaneousClass g discrim discrimPrev) then
                        Some (EdgeDiscrim(i', discrim, p.Range))
                    else
                        None

                | None ->
                    None
            else
                None)

    and IsCopyableInputExpr origInputExpr =
        match origInputExpr with
        | Expr.Op (TOp.LValueOp (LByrefGet, v), [], [], _) when not v.IsMutable -> true
        | _ -> false

    and ChoosePreBinder simulSetOfEdgeDiscrims subexpr =
         match simulSetOfEdgeDiscrims with
          // Very simple 'isinst' tests: put the result of 'isinst' in a local variable
          //
          // That is, transform
          //     'if istype e then ...unbox e .... '
          // into
          //     'let v = isinst e in .... if nonnull v then ...v .... '
          //
          // This is really an optimization that could be done more effectively in opt.fs
          // if we flowed a bit of information through


         | EdgeDiscrim(_i', (DecisionTreeTest.IsInst (_srcty, tgty)), m) :: _rest
                    (* check we can use a simple 'isinst' instruction *)
                    when canUseTypeTestFast g tgty && isNil origInputValTypars ->

             let v, vExpr = mkCompGenLocal m "typeTestResult" tgty
             if origInputVal.IsMemberOrModuleBinding then
                 AdjustValToTopVal v origInputVal.DeclaringEntity ValReprInfo.emptyValData
             let argExpr = GetSubExprOfInput subexpr
             let appExpr = mkIsInst tgty argExpr matchm
             Some vExpr, Some(mkInvisibleBind v appExpr)

          // Any match on a struct union must take the address of its input.
          // We can shortcut the addrOf when the original input is a deref of a byref value.
         | EdgeDiscrim(_i', (DecisionTreeTest.UnionCase (ucref, _)), _) :: _rest
                 when isNil origInputValTypars && ucref.Tycon.IsStructRecordOrUnionTycon ->

             let argExpr = GetSubExprOfInput subexpr
             let argExpr =
                 match argExpr, _origInputExprOpt with
                 | Expr.Val (v1, _, _), Some origInputExpr when valEq origInputVal v1.Deref && IsCopyableInputExpr origInputExpr -> origInputExpr
                 | _ -> argExpr
             let vOpt, addrExp, _readonly, _writeonly = mkExprAddrOfExprAux g true false NeverMutates argExpr None matchm
             match vOpt with
             | None -> Some addrExp, None
             | Some (v, e) ->
                 if origInputVal.IsMemberOrModuleBinding then
                     AdjustValToTopVal v origInputVal.DeclaringEntity ValReprInfo.emptyValData
                 Some addrExp, Some (mkInvisibleBind v e)



#if OPTIMIZE_LIST_MATCHING
         | [EdgeDiscrim(_, ListConsDiscrim g tinst, m); EdgeDiscrim(_, ListEmptyDiscrim g _, _)]
         | [EdgeDiscrim(_, ListEmptyDiscrim g _, _); EdgeDiscrim(_, ListConsDiscrim g tinst, m)]
         | [EdgeDiscrim(_, ListConsDiscrim g tinst, m)]
         | [EdgeDiscrim(_, ListEmptyDiscrim g tinst, m)]
                    (* check we can use a simple 'isinst' instruction *)
                    when isNil origInputValTypars ->

             let ucaseTy = (mkProvenUnionCaseTy g.cons_ucref tinst)
             let v, vExpr = mkCompGenLocal m "unionTestResult" ucaseTy
             if origInputVal.IsMemberOrModuleBinding then
                 AdjustValToTopVal v origInputVal.DeclaringEntity ValReprInfo.emptyValData
             let argExpr = GetSubExprOfInput subexpr
             let appExpr = mkIsInst ucaseTy argExpr matchm
             Some vExpr, Some (mkInvisibleBind v appExpr)
#endif

         // Active pattern matches: create a variable to hold the results of executing the active pattern.
         // If a struct return we continue with an expression for taking the address of that location.
         | (EdgeDiscrim(_, (DecisionTreeTest.ActivePatternCase(activePatExpr, resTys, isStructRetTy, _apatVrefOpt, _, apinfo)), m) :: _) ->

             if not (isNil origInputValTypars) then error(InternalError("Unexpected generalized type variables when compiling an active pattern", m))

             let resTy = apinfo.ResultType g m resTys isStructRetTy
             let argExpr = GetSubExprOfInput subexpr
             let appExpr = mkApps g ((activePatExpr, tyOfExpr g activePatExpr), [], [argExpr], m)

             let vOpt, addrExp, _readonly, _writeonly = mkExprAddrOfExprAux g isStructRetTy false NeverMutates appExpr None matchm
             match vOpt with
             | None -> 
                let v, vExpr = mkCompGenLocal m ("activePatternResult" + string (newUnique())) resTy
                if origInputVal.IsMemberOrModuleBinding then
                    AdjustValToTopVal v origInputVal.DeclaringEntity ValReprInfo.emptyValData
                Some vExpr, Some(mkInvisibleBind v addrExp)
             | Some (v, e) ->
                 if origInputVal.IsMemberOrModuleBinding then
                     AdjustValToTopVal v origInputVal.DeclaringEntity ValReprInfo.emptyValData
                 Some addrExp, Some (mkInvisibleBind v e)

          | _ -> None, None


    and CompileSimultaneousSet frontiers path refuted subexpr simulSetOfEdgeDiscrims (inpExprOpt: Expr option) =

        ([], simulSetOfEdgeDiscrims) ||> List.collectFold (fun taken (EdgeDiscrim(i', discrim, m)) ->
             // Check to see if we've already collected the edge for this case, in which case skip it.
             if List.exists (isDiscrimSubsumedBy g amap m discrim) taken  then
                 // Skip this edge: it is refuted
                 ([], taken)
             else
                 // Make a resVar to hold the results of the successful "proof" that a union value is
                 // a successful union case. That is, transform
                 //     'match v with
                 //        | A _ -> ...
                 //        | B _ -> ...'
                 // into
                 //     'match v with
                 //        | A _ -> let vA = (v ~~> A)  in ....
                 //        | B _ -> let vB = (v ~~> B)  in .... '
                 //
                 // Only do this for union cases that actually have some fields and with more than one case
                 let resPostBindOpt, ucaseBindOpt =
                     match discrim with
                     | DecisionTreeTest.UnionCase (ucref, tinst) when
#if OPTIMIZE_LIST_MATCHING
                                                           isNone inpExprOpt &&
#endif
                                                          (isNil origInputValTypars &&
                                                           not origInputVal.IsMemberOrModuleBinding &&
                                                           not ucref.Tycon.IsStructRecordOrUnionTycon  &&
                                                           ucref.UnionCase.RecdFieldsArray.Length >= 1 &&
                                                           ucref.Tycon.UnionCasesArray.Length > 1) ->

                       let v, vExpr = mkCompGenLocal m "unionCase" (mkProvenUnionCaseTy ucref tinst)
                       let argExpr = GetSubExprOfInput subexpr
                       let appExpr = mkUnionCaseProof (argExpr, ucref, tinst, m)
                       Some vExpr, Some(mkInvisibleBind v appExpr)
                     | _ ->
                       None, None

                 // Convert active pattern edges to tests on results data
                 let discrim' =
                     match discrim with
                     | DecisionTreeTest.ActivePatternCase(_pexp, resTys, isStructRetTy, _apatVrefOpt, idx, apinfo) ->
                         let aparity = apinfo.Names.Length
                         let total = apinfo.IsTotal
                         if not total && aparity > 1 then
                             error(Error(FSComp.SR.patcPartialActivePatternsGenerateOneResult(), m))

                         if not total then DecisionTreeTest.UnionCase(mkAnySomeCase g isStructRetTy, resTys)
                         elif aparity <= 1 then DecisionTreeTest.Const(Const.Unit)
                         else DecisionTreeTest.UnionCase(mkChoiceCaseRef g m aparity idx, resTys)
                     | _ -> discrim

                 // Project a successful edge through the frontiers.
                 let investigation = Investigation(i', discrim, path)

                 let frontiers = frontiers |> List.collect (GenerateNewFrontiersAfterSuccessfulInvestigation inpExprOpt resPostBindOpt investigation)
                 let tree = InvestigateFrontiers refuted frontiers
                 // Bind the resVar for the union case, if we have one
                 let tree =
                     match ucaseBindOpt with
                     | None -> tree
                     | Some bind -> TDBind (bind, tree)
                 // Return the edge
                 let edge = TCase(discrim', tree)
                 [edge], (discrim :: taken) )

    and CompileFallThroughTree fallthroughPathFrontiers path refuted (simulSetOfCases: DecisionTreeCase list) =

        let simulSetOfDiscrims = simulSetOfCases |> List.map (fun c -> c.Discriminator)

        let isRefuted (Frontier (_i', active', _)) =
            isMemOfActives path active' &&
            let p = lookupActive path active' |> snd
            match getDiscrimOfPattern p with
            | Some discrim -> List.exists (isDiscrimSubsumedBy g amap exprm discrim) simulSetOfDiscrims
            | None -> false

        match simulSetOfDiscrims with
        | DecisionTreeTest.Const (Const.Bool _b) :: _ when simulSetOfCases.Length = 2 ->  None
        | DecisionTreeTest.Const (Const.Byte _) :: _  when simulSetOfCases.Length = 256 ->  None
        | DecisionTreeTest.Const (Const.SByte _) :: _  when simulSetOfCases.Length = 256 ->  None
        | DecisionTreeTest.Const (Const.Unit) :: _  ->  None
        | DecisionTreeTest.UnionCase (ucref, _) :: _ when  simulSetOfCases.Length = ucref.TyconRef.UnionCasesArray.Length -> None
        | DecisionTreeTest.ActivePatternCase _ :: _ -> error(InternalError("DecisionTreeTest.ActivePatternCase should have been eliminated", matchm))
        | _ ->
            let fallthroughPathFrontiers = List.filter (isRefuted >> not) fallthroughPathFrontiers

            (* Add to the refuted set *)
            let refuted = (RefutedInvestigation(path, simulSetOfDiscrims)) :: refuted

            match fallthroughPathFrontiers with
            | [] ->
                None
            | _ ->
                Some(InvestigateFrontiers refuted fallthroughPathFrontiers)

    // Build a new frontier that represents the result of a successful investigation
    // at rule point (i', discrim, path)
    and GenerateNewFrontiersAfterSuccessfulInvestigation inpExprOpt resPostBindOpt (Investigation(i', discrim, path)) (Frontier (i, active, valMap) as frontier) =

        if (isMemOfActives path active) then
            let (SubExpr(accessf, ve)), pat = lookupActive path active

            let mkSubFrontiers path accessf' active' argpats pathBuilder =
                let mkSubActive j p =
                    let newSubExpr = SubExpr(accessf' j, ve)
                    let newPath = pathBuilder path j
                    Active(newPath, newSubExpr, p)
                let newActives = List.mapi mkSubActive argpats
                let investigations = BindProjectionPatterns newActives (active', valMap)
                mkFrontiers investigations i

            let active' = removeActive path active
            match pat with
            | TPat_wild _ | TPat_as _ | TPat_tuple _ | TPat_disjs _ | TPat_conjs _ | TPat_recd _ -> failwith "Unexpected projection pattern"
            | TPat_query ((_, resTys, isStructRetTy, apatVrefOpt, idx, apinfo), p, m) ->
                if apinfo.IsTotal then
                    // Total active patterns always return choice values
                    let hasParam = (match apatVrefOpt with None -> true | Some (vref, _) -> doesActivePatternHaveFreeTypars g vref)
                    if (hasParam && i = i') || (discrimsEq g discrim (Option.get (getDiscrimOfPattern pat))) then
                        let aparity = apinfo.Names.Length
                        let accessf' j tpinst _e' =
                            assert inpExprOpt.IsSome
                            if aparity <= 1 then
                                Option.get inpExprOpt
                            else
                                let ucref = mkChoiceCaseRef g m aparity idx
                                // TODO: In the future we will want active patterns to be able to return struct-unions
                                //       In that eventuality, we need to check we are taking the address correctly
                                mkUnionCaseFieldGetUnprovenViaExprAddr (Option.get inpExprOpt, ucref, instTypes tpinst resTys, j, exprm)
                        mkSubFrontiers path accessf' active' [p] (fun path j -> PathQuery(path, int64 j))

                    elif hasParam then

                        // Successful active patterns  don't refute other patterns
                        [frontier]
                    else
                        []
                else
                    // Partial active patterns always return options or value-options
                    if i = i' then
                        let accessf' _j tpinst _ =
                            let expr = Option.get inpExprOpt
                            if isStructRetTy then 
                                // In this case, the inpExprOpt is already an address-of expression
                                mkUnionCaseFieldGetProvenViaExprAddr (expr, mkValueSomeCase g, instTypes tpinst resTys, 0, exprm)
                            else
                                mkUnionCaseFieldGetUnprovenViaExprAddr (expr, mkSomeCase g, instTypes tpinst resTys, 0, exprm)
                        mkSubFrontiers path accessf' active' [p] (fun path j -> PathQuery(path, int64 j))
                    else
                        // Successful active patterns  don't refute other patterns
                        [frontier]

            | TPat_unioncase (ucref1, tyargs, argpats, _) ->
                match discrim with
                | DecisionTreeTest.UnionCase (ucref2, tinst) when g.unionCaseRefEq ucref1 ucref2 ->
                    let accessf' j tpinst exprIn =
                        match resPostBindOpt with
                        | Some e -> mkUnionCaseFieldGetProvenViaExprAddr (e, ucref1, tinst, j, exprm)
                        | None ->
                            let exprIn =
                                match inpExprOpt with
                                | Some addrExp -> addrExp
                                | None -> accessf tpinst exprIn
                            mkUnionCaseFieldGetUnprovenViaExprAddr (exprIn, ucref1, instTypes tpinst tyargs, j, exprm)

                    mkSubFrontiers path accessf' active' argpats (fun path j -> PathUnionConstr(path, ucref1, tyargs, j))
                | DecisionTreeTest.UnionCase _ ->
                    // Successful union case tests DO refute all other union case tests (no overlapping union cases)
                    []
                | _ ->
                    // Successful union case tests don't refute any other patterns
                    [frontier]

            | TPat_array (argpats, ty, _) ->
                match discrim with
                | DecisionTreeTest.ArrayLength (n, _) when List.length argpats = n ->
                    let accessf' j tpinst exprIn = mkCallArrayGet g exprm ty (accessf tpinst exprIn) (mkInt g exprm j)
                    mkSubFrontiers path accessf' active' argpats (fun path j -> PathArray(path, ty, List.length argpats, j))
                // Successful length tests refute all other lengths
                | DecisionTreeTest.ArrayLength _ ->
                    []
                | _ ->
                    [frontier]

            | TPat_exnconstr (ecref, argpats, _) ->
                match discrim with
                | DecisionTreeTest.IsInst (_srcTy, tgtTy) when typeEquiv g (mkAppTy ecref []) tgtTy ->
                    let accessf' j tpinst exprIn = mkExnCaseFieldGet(accessf tpinst exprIn, ecref, j, exprm)
                    mkSubFrontiers path accessf' active' argpats (fun path j -> PathExnConstr(path, ecref, j))
                | _ ->
                    // Successful type tests against one sealed type refute all other sealed types
                    // REVIEW: Successful type tests against one sealed type should refute all other sealed types
                    [frontier]

            | TPat_isinst (_srcty, tgtTy1, pbindOpt, _) ->
                match discrim with
                | DecisionTreeTest.IsInst (_srcTy, tgtTy2) when typeEquiv g tgtTy1 tgtTy2  ->
                    match pbindOpt with
                    | Some pbind ->
                        let accessf' tpinst exprIn =
                            // Fetch the result from the place where we saved it, if possible
                            match inpExprOpt with
                            | Some e -> e
                            | _ ->
                                // Otherwise call the helper
                               mkCallUnboxFast g exprm (instType tpinst tgtTy1) (accessf tpinst exprIn)
                        BindProjectionPattern (Active(path, SubExpr(accessf', ve), pbind)) (active', valMap)
                        |> mkFrontiers <| i
                    | None ->
                        [Frontier (i, active', valMap)]

                | _ ->
                    // Successful type tests against other types don't refute anything
                    // REVIEW: Successful type tests against one sealed type should refute all other sealed types
                    [frontier]

            | TPat_null _ ->
                match discrim with
                | DecisionTreeTest.IsNull ->
                    [Frontier (i, active', valMap)]
                | _ ->
                    // Successful null tests don't refute any other patterns
                    [frontier]

            | TPat_const (c1, _) ->
                match discrim with
                | DecisionTreeTest.Const c2 when (c1=c2) ->
                    [Frontier (i, active', valMap)]
                | DecisionTreeTest.Const _ ->
                    // All constants refute all other constants (no overlapping between constants!)
                    []
                | _ ->
                    [frontier]

            | TPat_error range ->
                match discrim with
                | DecisionTreeTest.Error testRange when range = testRange ->
                    [Frontier (i, active', valMap)]
                | _ ->
                    [frontier]

            | _ -> failwith "pattern compilation: GenerateNewFrontiersAfterSuccessfulInvestigation"

        else
            [frontier]

    and BindProjectionPattern (Active(path, subExpr, p) as inp) ((accActive, accValMap) as s) =
        let (SubExpr(accessf, ve)) = subExpr
        let mkSubActive pathBuilder accessf'  j p'  =
            Active(pathBuilder path j, SubExpr(accessf' j, ve), p')

        match p with
        | TPat_wild _ ->
            BindProjectionPatterns [] s
        | TPat_as(p', pbind, m) ->
            let (v, subExpr') =  BindSubExprOfInput g amap origInputValTypars pbind m subExpr
            BindProjectionPattern (Active(path, subExpr, p')) (accActive, accValMap.Add v subExpr' )
        | TPat_tuple(tupInfo, ps, tyargs, _m) ->
            let accessf' j tpinst subExpr' = mkTupleFieldGet g (tupInfo, accessf tpinst subExpr', instTypes tpinst tyargs, j, exprm)
            let pathBuilder path j = PathTuple(path, tyargs, j)
            let newActives = List.mapi (mkSubActive pathBuilder accessf') ps
            BindProjectionPatterns newActives s
        | TPat_recd(tcref, tinst, ps, _m) ->
            let newActives =
                (ps, tcref.TrueInstanceFieldsAsRefList) ||> List.mapi2 (fun j p fref ->
                    let accessf' fref _j tpinst exprIn = mkRecdFieldGet g (accessf tpinst exprIn, fref, instTypes tpinst tinst, exprm)
                    let pathBuilder path j = PathRecd(path, tcref, tinst, j)
                    mkSubActive pathBuilder (accessf' fref) j p)
            BindProjectionPatterns newActives s
        | TPat_disjs(ps, _m) ->
            List.collect (fun p -> BindProjectionPattern (Active(path, subExpr, p)) s)  ps
        | TPat_conjs(ps, _m) ->
            let newActives = List.mapi (mkSubActive (fun path j -> PathConj(path, j)) (fun _j -> accessf)) ps
            BindProjectionPatterns newActives s

        | TPat_range (c1, c2, m) ->
            let mutable res = []
            for i = int c1 to int c2 do
                res <- BindProjectionPattern (Active(path, subExpr, TPat_const(Const.Char(char i), m))) s @ res
            res
        // Assign an identifier to each TPat_query based on our knowledge of the 'identity' of the active pattern, if any
        | TPat_query ((_, _, _, apatVrefOpt, _, _), _, _) ->
            let uniqId =
                match apatVrefOpt with
                | Some (vref, _) when not (doesActivePatternHaveFreeTypars g vref) -> vref.Stamp
                | _ -> genUniquePathId()
            let inp = Active(PathQuery(path, uniqId), subExpr, p)
            [(inp :: accActive, accValMap)]
        | _ ->
            [(inp :: accActive, accValMap)]

    and BindProjectionPatterns ps s =
        List.foldBack (fun p sofar -> List.collect (BindProjectionPattern p) sofar) ps [s]

    // The setup routine of the match compiler.
    let frontiers =
        ((typedClauses
          |> List.mapi (fun i c ->
                let initialSubExpr = SubExpr((fun _ x -> x), (exprForVal origInputVal.Range origInputVal, origInputVal))
                let investigations = BindProjectionPattern (Active(PathEmpty inputTy, initialSubExpr, c.Pattern)) ([], ValMap<_>.Empty)
                mkFrontiers investigations i)
          |> List.concat)
          @
          mkFrontiers [([], ValMap<_>.Empty)] nClauses)
    let dtree =
      InvestigateFrontiers
        []
        frontiers

    let targets = matchBuilder.CloseTargets()


    // Report unused targets
    if warnOnUnused then
        let used = HashSet<_>(accTargetsOfDecisionTree dtree [], HashIdentity.Structural)

        typedClauses |> List.iteri (fun i c ->
            if not (used.Contains i) then warning (RuleNeverMatched c.Range))

    dtree, targets

let isPartialOrWhenClause (c: TypedMatchClause) = isPatternPartial c.Pattern || c.GuardExpr.IsSome


let rec CompilePattern  g denv amap tcVal infoReader exprm matchm warnOnUnused actionOnFailure (origInputVal, origInputValTypars, origInputExprOpt) (clausesL: TypedMatchClause list) inputTy resultTy =
    match clausesL with
    | _ when List.exists isPartialOrWhenClause clausesL ->
        // Partial clauses cause major code explosion if treated naively
        // Hence treat any pattern matches with any partial clauses clause-by-clause

        // First make sure we generate at least some of the obvious incomplete match warnings.
        let warnOnUnused = false // we can't turn this on since we're pretending all partials fail in order to control the complexity of this.
        let warnOnIncomplete = true
        let clausesPretendAllPartialFail = List.collect (fun (TClause(p, whenOpt, tg, m)) -> [TClause(erasePartialPatterns p, whenOpt, tg, m)]) clausesL
        let _ = CompilePatternBasic g denv amap tcVal infoReader exprm matchm warnOnUnused warnOnIncomplete actionOnFailure (origInputVal, origInputValTypars, origInputExprOpt) clausesPretendAllPartialFail inputTy resultTy
        let warnOnIncomplete = false

        let rec atMostOnePartialAtATime clauses =
            match List.takeUntil isPartialOrWhenClause clauses with
            | l, [] ->
                CompilePatternBasic g denv amap tcVal infoReader exprm matchm warnOnUnused warnOnIncomplete actionOnFailure (origInputVal, origInputValTypars, origInputExprOpt) l inputTy resultTy
            | l, (h :: t) ->
                // Add the partial clause.
                doGroupWithAtMostOnePartial (l @ [h]) t

        and doGroupWithAtMostOnePartial group rest =
            // Compile the remaining clauses.
            let decisionTree, targets = atMostOnePartialAtATime rest

            // Make the expression that represents the remaining cases of the pattern match.
            let expr = mkAndSimplifyMatch DebugPointAtBinding.NoneAtInvisible exprm matchm resultTy decisionTree targets

            // If the remainder of the match boiled away to nothing interesting.
            // We measure this simply by seeing if the range of the resulting expression is identical to matchm.
            let spTarget =
                if Range.equals expr.Range matchm then DebugPointForTarget.No
                else DebugPointForTarget.Yes

            // Make the clause that represents the remaining cases of the pattern match
            let clauseForRestOfMatch = TClause(TPat_wild matchm, None, TTarget(List.empty, expr, spTarget), matchm)

            CompilePatternBasic g denv amap tcVal infoReader exprm matchm warnOnUnused warnOnIncomplete actionOnFailure (origInputVal, origInputValTypars, origInputExprOpt) (group @ [clauseForRestOfMatch]) inputTy resultTy


        atMostOnePartialAtATime clausesL

    | _ ->
        CompilePatternBasic g denv amap tcVal infoReader exprm matchm warnOnUnused true actionOnFailure (origInputVal, origInputValTypars, origInputExprOpt) clausesL inputTy resultTy
