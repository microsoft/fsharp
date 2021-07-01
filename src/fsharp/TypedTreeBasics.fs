// Copyright (c) Microsoft Corporation. All Rights Reserved. See License.txt in the project root for license information.
  
//-------------------------------------------------------------------------
// Defines the typed abstract syntax trees used throughout the F# compiler.
//------------------------------------------------------------------------- 

module internal FSharp.Compiler.TypedTreeBasics

open Internal.Utilities.Library
open FSharp.Compiler.AbstractIL.IL 
open FSharp.Compiler.CompilerGlobalState
open FSharp.Compiler.Text
open FSharp.Compiler.Syntax
open FSharp.Compiler.TypedTree

#if DEBUG
assert (sizeof<ValFlags> = 8)
assert (sizeof<EntityFlags> = 8)
assert (sizeof<TyparFlags> = 4)
#endif

let getNameOfScopeRef sref = 
    match sref with 
    | ILScopeRef.Local -> "<local>"
    | ILScopeRef.Module mref -> mref.Name
    | ILScopeRef.Assembly aref -> aref.Name
    | ILScopeRef.PrimaryAssembly -> "<primary>"

/// Metadata on values (names of arguments etc. 
module ValReprInfo = 

    let unnamedTopArg1: ArgReprInfo = { Attribs=[]; Name=None }

    let unnamedTopArg = [unnamedTopArg1]

    let unitArgData: ArgReprInfo list list = [[]]

    let unnamedRetVal: ArgReprInfo = { Attribs = []; Name=None }

    let selfMetadata = unnamedTopArg

    let emptyValData = ValReprInfo([], [], unnamedRetVal)

    let InferTyparInfo (tps: Typar list) = tps |> List.map (fun tp -> TyparReprInfo(tp.Id, tp.Kind))

    let InferArgReprInfo (v: Val) : ArgReprInfo = { Attribs = []; Name= Some v.Id }

    let InferArgReprInfos (vs: Val list list) = ValReprInfo([], List.mapSquared InferArgReprInfo vs, unnamedRetVal)

    let HasNoArgs (ValReprInfo(n, args, _)) = n.IsEmpty && args.IsEmpty

//---------------------------------------------------------------------------
// Basic properties via functions (old style)
//---------------------------------------------------------------------------

let typeOfVal (v: Val) = v.Type

let typesOfVals (v: Val list) = v |> List.map (fun v -> v.Type)

let nameOfVal (v: Val) = v.LogicalName

let arityOfVal (v: Val) = (match v.ValReprInfo with None -> ValReprInfo.emptyValData | Some arities -> arities)

let tupInfoRef = TupInfo.Const false

let tupInfoStruct = TupInfo.Const true

let mkTupInfo b = if b then tupInfoStruct else tupInfoRef

let structnessDefault = false

let mkRawRefTupleTy tys = TType_tuple (tupInfoRef, tys)

let mkRawStructTupleTy tys = TType_tuple (tupInfoStruct, tys)

//---------------------------------------------------------------------------
// Aggregate operations to help transform the components that 
// make up the entire compilation unit
//---------------------------------------------------------------------------

let mapTImplFile f (TImplFile (fragName, pragmas, moduleExpr, hasExplicitEntryPoint, isScript, anonRecdTypes)) =
    TImplFile (fragName, pragmas, f moduleExpr, hasExplicitEntryPoint, isScript, anonRecdTypes)

let mapAccImplFile f z (TImplFile (fragName, pragmas, moduleExpr, hasExplicitEntryPoint, isScript, anonRecdTypes)) =
    let moduleExpr, z = f z moduleExpr
    TImplFile (fragName, pragmas, moduleExpr, hasExplicitEntryPoint, isScript, anonRecdTypes), z

let foldTImplFile f z (TImplFile (_, _, moduleExpr, _, _, _)) = f z moduleExpr

//---------------------------------------------------------------------------
// Equality relations on locally defined things 
//---------------------------------------------------------------------------

let typarEq (lv1: Typar) (lv2: Typar) = (lv1.Stamp = lv2.Stamp)

/// Equality on type variables, implemented as reference equality. This should be equivalent to using typarEq.
let typarRefEq (tp1: Typar) (tp2: Typar) = (tp1 === tp2)

/// Equality on value specs, implemented as reference equality
let valEq (lv1: Val) (lv2: Val) = (lv1 === lv2)

/// Equality on CCU references, implemented as reference equality except when unresolved
let ccuEq (mv1: CcuThunk) (mv2: CcuThunk) = 
    (mv1 === mv2) || 
    (if mv1.IsUnresolvedReference || mv2.IsUnresolvedReference then 
        mv1.AssemblyName = mv2.AssemblyName
     else 
        mv1.Contents === mv2.Contents)

/// For dereferencing in the middle of a pattern
let (|ValDeref|) (vr: ValRef) = vr.Deref

//--------------------------------------------------------------------------
// Make references to TAST items
//--------------------------------------------------------------------------

let mkRecdFieldRef tcref f = RecdFieldRef(tcref, f)

let mkUnionCaseRef tcref c = UnionCaseRef(tcref, c)

let ERefLocal x: EntityRef = { binding=x; nlr=Unchecked.defaultof<_> }      

let ERefNonLocal x: EntityRef = { binding=Unchecked.defaultof<_>; nlr=x }      

let ERefNonLocalPreResolved x xref: EntityRef = { binding=x; nlr=xref }      

let (|ERefLocal|ERefNonLocal|) (x: EntityRef) = 
    match box x.nlr with 
    | null -> ERefLocal x.binding
    | _ -> ERefNonLocal x.nlr

//--------------------------------------------------------------------------
// Construct local references
//-------------------------------------------------------------------------- 

let mkLocalTyconRef x = ERefLocal x

let mkNonLocalEntityRef ccu mp = NonLocalEntityRef(ccu, mp)

let mkNestedNonLocalEntityRef (nleref: NonLocalEntityRef) id =
    mkNonLocalEntityRef nleref.Ccu (Array.append nleref.Path [| id |])

let mkNonLocalTyconRef nleref id = ERefNonLocal (mkNestedNonLocalEntityRef nleref id)

let mkNonLocalTyconRefPreResolved x nleref id =
    ERefNonLocalPreResolved x (mkNestedNonLocalEntityRef nleref id)

type EntityRef with 

    member tcref.NestedTyconRef (x: Entity) = 
        match tcref with 
        | ERefLocal _ -> mkLocalTyconRef x
        | ERefNonLocal nlr -> mkNonLocalTyconRefPreResolved x nlr x.LogicalName

    member tcref.RecdFieldRefInNestedTycon tycon (id: Ident) = RecdFieldRef (tcref.NestedTyconRef tycon, id.idText)

/// Make a reference to a union case for type in a module or namespace
let mkModuleUnionCaseRef (modref: ModuleOrNamespaceRef) tycon uc = 
    (modref.NestedTyconRef tycon).MakeNestedUnionCaseRef uc

let VRefLocal x: ValRef = { binding=x; nlr=Unchecked.defaultof<_> }      

let VRefNonLocal x: ValRef = { binding=Unchecked.defaultof<_>; nlr=x }      

let VRefNonLocalPreResolved x xref: ValRef = { binding=x; nlr=xref }      

let (|VRefLocal|VRefNonLocal|) (x: ValRef) = 
    match box x.nlr with 
    | null -> VRefLocal x.binding
    | _ -> VRefNonLocal x.nlr

let mkNonLocalValRef mp id = VRefNonLocal {EnclosingEntity = ERefNonLocal mp; ItemKey=id }

let mkNonLocalValRefPreResolved x mp id = VRefNonLocalPreResolved x {EnclosingEntity = ERefNonLocal mp; ItemKey=id }

let ccuOfValRef vref =  
    match vref with 
    | VRefLocal _ -> None
    | VRefNonLocal nlr -> Some nlr.Ccu

let ccuOfTyconRef eref =  
    match eref with 
    | ERefLocal _ -> None
    | ERefNonLocal nlr -> Some nlr.Ccu

//--------------------------------------------------------------------------
// Type parameters and inference unknowns
//-------------------------------------------------------------------------

let NewNullnessVar() = Nullness.Variable (NullnessVar()) // we don't known (and if we never find out then it's non-null)

let KnownAmbivalentToNull = Nullness.Known NullnessInfo.AmbivalentToNull
let KnownWithNull = Nullness.Known NullnessInfo.WithNull
let KnownWithoutNull = Nullness.Known NullnessInfo.WithoutNull

let mkTyparTy (tp:Typar) = 
    match tp.Kind with 
    | TyparKind.Type -> tp.AsType KnownWithoutNull // TODO NULLNESS: check various callers
    | TyparKind.Measure -> TType_measure (Measure.Var tp)

let copyTypar (tp: Typar) = 
    let optData = tp.typar_opt_data |> Option.map (fun tg -> { typar_il_name = tg.typar_il_name; typar_xmldoc = tg.typar_xmldoc; typar_constraints = tg.typar_constraints; typar_attribs = tg.typar_attribs })
    Typar.New { typar_id = tp.typar_id
                typar_flags = tp.typar_flags
                typar_stamp = newStamp()
                typar_solution = tp.typar_solution
                typar_astype = Unchecked.defaultof<_>
                // Be careful to clone the mutable optional data too
                typar_opt_data = optData } 

let copyTypars tps = List.map copyTypar tps

//--------------------------------------------------------------------------
// Inference variables
//-------------------------------------------------------------------------- 
    
let tryShortcutSolvedUnitPar canShortcut (r: Typar) = 
    if r.Kind = TyparKind.Type then failwith "tryShortcutSolvedUnitPar: kind=type"
    match r.Solution with
    | Some (TType_measure unt) -> 
        if canShortcut then 
            match unt with 
            | Measure.Var r2 -> 
               match r2.Solution with
               | None -> ()
               | Some _ as soln -> 
                  r.typar_solution <- soln
            | _ -> () 
        unt
    | _ -> 
        failwith "tryShortcutSolvedUnitPar: unsolved"
      
let rec stripUnitEqnsAux canShortcut unt = 
    match unt with 
    | Measure.Var r when r.IsSolved -> stripUnitEqnsAux canShortcut (tryShortcutSolvedUnitPar canShortcut r)
    | _ -> unt

let combineNullness (nullnessOrig: Nullness) (nullnessNew: Nullness) = 
    match nullnessOrig.Evaluate() with
    | NullnessInfo.WithoutNull -> nullnessNew
    | NullnessInfo.AmbivalentToNull -> nullnessOrig
    | NullnessInfo.WithNull -> 
        match nullnessNew.Evaluate() with
        | NullnessInfo.WithoutNull -> nullnessOrig
        | NullnessInfo.AmbivalentToNull -> nullnessNew
        | NullnessInfo.WithNull -> nullnessOrig

let tryAddNullnessToTy nullnessNew (ty:TType) = 
    match ty with
    | TType_var (tp, nullnessOrig) -> 
        // TODO NULLNESS: make this avoid allocation if no change
        Some (TType_var (tp, combineNullness nullnessOrig nullnessNew))
    | TType_app (tcr, tinst, nullnessOrig) -> 
        // TODO NULLNESS: make this avoid allocation if no change
        Some (TType_app (tcr, tinst, combineNullness nullnessOrig nullnessNew))
    | TType_ucase _ -> None // TODO NULLNESS
    | TType_tuple _ -> None // TODO NULLNESS
    | TType_anon _ -> None // TODO NULLNESS
    | TType_fun (d, r, nullnessOrig) ->
        // TODO NULLNESS: make this avoid allocation if no change
        Some (TType_fun (d, r, combineNullness nullnessOrig nullnessNew))
    | TType_forall _ -> None
    | TType_measure _ -> None

let addNullnessToTy (nullness: Nullness) (ty:TType) =
    match nullness.Evaluate() with
    | NullnessInfo.WithoutNull -> ty
    | _ -> 
    match ty with
    | TType_var (tp, nullnessOrig) -> TType_var (tp, combineNullness nullnessOrig nullness)
    | TType_app (tcr, tinst, nullnessOrig) -> TType_app (tcr, tinst, combineNullness nullnessOrig nullness)
    | TType_fun (d, r, nullnessOrig) -> TType_fun (d, r, combineNullness nullnessOrig nullness)
    //| TType_ucase _ -> None // TODO NULLNESS
    //| TType_tuple _ -> None // TODO NULLNESS
    //| TType_anon _ -> None // TODO NULLNESS
    | _ -> ty

let rec stripTyparEqnsAux nullness0 canShortcut ty = 
    match ty with 
    | TType_var (r, nullness) -> 
        match r.Solution with
        | Some soln -> 
            if canShortcut then 
                match soln with 
                // We avoid shortcutting when there are additional constraints on the type variable we're trying to cut out
                // This is only because IterType likes to walk _all_ the constraints _everywhere_ in a type, including
                // those attached to _solved_ type variables. In an ideal world this would never be needed - see the notes
                // on IterType.
                | TType_var (r2, nullness2) when r2.Constraints.IsEmpty -> 
                   match nullness2.Evaluate() with 
                   | NullnessInfo.WithoutNull -> 
                       match r2.Solution with
                       | None -> ()
                       | Some _ as soln2 -> 
                          r.typar_solution <- soln2
                   | _ -> ()
                | _ -> () 
            stripTyparEqnsAux (combineNullness nullness0 nullness) canShortcut soln
        | None -> 
            addNullnessToTy nullness0 ty
    | TType_measure unt -> 
        TType_measure (stripUnitEqnsAux canShortcut unt)
    | _ -> ty

let stripTyparEqns ty = stripTyparEqnsAux KnownWithoutNull false ty

let stripUnitEqns unt = stripUnitEqnsAux false unt

let replaceNullnessOfTy nullness (ty:TType) =
    match stripTyparEqns ty with
    | TType_var (tp, _nullnessOrig) -> TType_var (tp, nullness)
    | TType_app (tcr, tinst, _nullnessOrig) -> TType_app (tcr, tinst, nullness)
    | TType_fun (d, r, _nullnessOrig) -> TType_fun (d, r, nullness)
    //| TType_ucase _ -> None // TODO NULLNESS
    //| TType_tuple _ -> None // TODOTODO NULLNESS
    //| TType_anon _ -> None // TODO NULLNESS
    | sty -> sty

//---------------------------------------------------------------------------
// These make local/non-local references to values according to whether
// the item is globally stable ("published") or not.
//---------------------------------------------------------------------------

let mkLocalValRef (v: Val) = VRefLocal v
let mkLocalModRef (v: ModuleOrNamespace) = ERefLocal v
let mkLocalEntityRef (v: Entity) = ERefLocal v

let mkNonLocalCcuRootEntityRef ccu (x: Entity) = mkNonLocalTyconRefPreResolved x (mkNonLocalEntityRef ccu [| |]) x.LogicalName

let mkNestedValRef (cref: EntityRef) (v: Val) : ValRef = 
    match cref with 
    | ERefLocal _ -> mkLocalValRef v
    | ERefNonLocal nlr -> 
        let key = v.GetLinkageFullKey()
        mkNonLocalValRefPreResolved v nlr key

/// From Ref_private to Ref_nonlocal when exporting data.
let rescopePubPathToParent viewedCcu (PubPath p) = NonLocalEntityRef(viewedCcu, p.[0..p.Length-2])

/// From Ref_private to Ref_nonlocal when exporting data.
let rescopePubPath viewedCcu (PubPath p) = NonLocalEntityRef(viewedCcu, p)

//---------------------------------------------------------------------------
// Equality between TAST items.
//---------------------------------------------------------------------------

let valRefInThisAssembly compilingFslib (x: ValRef) = 
    match x with 
    | VRefLocal _ -> true
    | VRefNonLocal _ -> compilingFslib

let tyconRefUsesLocalXmlDoc compilingFslib (x: TyconRef) = 
    match x with 
    | ERefLocal _ -> true
    | ERefNonLocal _ ->
#if !NO_EXTENSIONTYPING
        match x.TypeReprInfo with
        | TProvidedTypeExtensionPoint _ -> true
        | _ -> 
#endif
        compilingFslib
    
let entityRefInThisAssembly compilingFslib (x: EntityRef) = 
    match x with 
    | ERefLocal _ -> true
    | ERefNonLocal _ -> compilingFslib

let arrayPathEq (y1: string[]) (y2: string[]) =
    let len1 = y1.Length 
    let len2 = y2.Length 
    (len1 = len2) && 
    (let rec loop i = (i >= len1) || (y1.[i] = y2.[i] && loop (i+1)) 
     loop 0)

let nonLocalRefEq (NonLocalEntityRef(x1, y1) as smr1) (NonLocalEntityRef(x2, y2) as smr2) = 
    smr1 === smr2 || (ccuEq x1 x2 && arrayPathEq y1 y2)

/// This predicate tests if non-local resolution paths are definitely known to resolve
/// to different entities. All references with different named paths always resolve to 
/// different entities. Two references with the same named paths may resolve to the same 
/// entities even if they reference through different CCUs, because one reference
/// may be forwarded to another via a .NET TypeForwarder.
let nonLocalRefDefinitelyNotEq (NonLocalEntityRef(_, y1)) (NonLocalEntityRef(_, y2)) = 
    not (arrayPathEq y1 y2)

let pubPathEq (PubPath path1) (PubPath path2) = arrayPathEq path1 path2

let fslibRefEq (nlr1: NonLocalEntityRef) (PubPath path2) =
    arrayPathEq nlr1.Path path2

// Compare two EntityRef's for equality when compiling fslib (FSharp.Core.dll)
//
// Compiler-internal references to items in fslib are Ref_nonlocals even when compiling fslib.
// This breaks certain invariants that hold elsewhere, because they dereference to point to 
// Entity's from signatures rather than Entity's from implementations. This means backup, alternative 
// equality comparison techniques are needed when compiling fslib itself.
let fslibEntityRefEq fslibCcu (eref1: EntityRef) (eref2: EntityRef) =
    match eref1, eref2 with 
    | (ERefNonLocal nlr1, ERefLocal x2)
    | (ERefLocal x2, ERefNonLocal nlr1) ->
        ccuEq nlr1.Ccu fslibCcu &&
        match x2.PublicPath with 
        | Some pp2 -> fslibRefEq nlr1 pp2
        | None -> false
    | (ERefLocal e1, ERefLocal e2) ->
        match e1.PublicPath, e2.PublicPath with 
        | Some pp1, Some pp2 -> pubPathEq pp1 pp2
        | _ -> false
    | _ -> false

// Compare two ValRef's for equality when compiling fslib (FSharp.Core.dll)
//
// Compiler-internal references to items in fslib are Ref_nonlocals even when compiling fslib.
// This breaks certain invariants that hold elsewhere, because they dereference to point to 
// Val's from signatures rather than Val's from implementations. This means backup, alternative 
// equality comparison techniques are needed when compiling fslib itself.
let fslibValRefEq fslibCcu vref1 vref2 =
    match vref1, vref2 with 
    | (VRefNonLocal nlr1, VRefLocal x2)
    | (VRefLocal x2, VRefNonLocal nlr1) ->
        ccuEq nlr1.Ccu fslibCcu &&
        match x2.PublicPath with 
        | Some (ValPubPath(pp2, nm2)) -> 
            // Note: this next line is just comparing the values by name, and not even the partial linkage data
            // This relies on the fact that the compiler doesn't use any references to
            // entities in fslib that are overloaded, or, if they are overloaded, then value identity
            // is not significant
            nlr1.ItemKey.PartialKey = nm2.PartialKey &&
            fslibRefEq nlr1.EnclosingEntity.nlr pp2
        | _ -> 
            false
    // Note: I suspect this private-to-private reference comparison is not needed
    | (VRefLocal e1, VRefLocal e2) ->
        match e1.PublicPath, e2.PublicPath with 
        | Some (ValPubPath(pp1, nm1)), Some (ValPubPath(pp2, nm2)) -> 
            pubPathEq pp1 pp2 && 
            (nm1 = nm2)
        | _ -> false
    | _ -> false
  
/// Primitive routine to compare two EntityRef's for equality
/// This takes into account the possibility that they may have type forwarders
let primEntityRefEq compilingFslib fslibCcu (x: EntityRef) (y: EntityRef) = 
    x === y ||
    
    if x.IsResolved && y.IsResolved && not compilingFslib then
        x.ResolvedTarget === y.ResolvedTarget 
    elif not x.IsLocalRef && not y.IsLocalRef &&
        (// Two tcrefs with identical paths are always equal
         nonLocalRefEq x.nlr y.nlr || 
         // The tcrefs may have forwarders. If they may possibly be equal then resolve them to get their canonical references
         // and compare those using pointer equality.
         (not (nonLocalRefDefinitelyNotEq x.nlr y.nlr) && 
            match x.TryDeref with
            | ValueSome v1 -> match y.TryDeref with ValueSome v2 -> v1 === v2 | _ -> false
            | _ -> match y.TryDeref with ValueNone -> true | _ -> false)) then
        true
    else
        compilingFslib && fslibEntityRefEq fslibCcu x y  

/// Primitive routine to compare two UnionCaseRef's for equality
let primUnionCaseRefEq compilingFslib fslibCcu (UnionCaseRef(tcr1, c1) as uc1) (UnionCaseRef(tcr2, c2) as uc2) = 
    uc1 === uc2 || (primEntityRefEq compilingFslib fslibCcu tcr1 tcr2 && c1 = c2)

/// Primitive routine to compare two ValRef's for equality. On the whole value identity is not particularly
/// significant in F#. However it is significant for
///    (a) Active Patterns 
///    (b) detecting uses of "special known values" from FSharp.Core.dll, such as 'seq' 
///        and quotation splicing 
///
/// Note this routine doesn't take type forwarding into account
let primValRefEq compilingFslib fslibCcu (x: ValRef) (y: ValRef) =
    x === y
    || (x.IsResolved && y.IsResolved && x.ResolvedTarget === y.ResolvedTarget)
    || (x.IsLocalRef && y.IsLocalRef && valEq x.ResolvedTarget y.ResolvedTarget)
    || // Use TryDeref to guard against the platforms/times when certain F# language features aren't available
       match x.TryDeref with
       | ValueSome v1 -> match y.TryDeref with ValueSome v2 -> v1 === v2 | ValueNone -> false
       | ValueNone -> match y.TryDeref with ValueNone -> true | ValueSome _ -> false
    || (compilingFslib && fslibValRefEq fslibCcu x y)

//---------------------------------------------------------------------------
// pubpath/cpath mess
//---------------------------------------------------------------------------

let fullCompPathOfModuleOrNamespace (m: ModuleOrNamespace) = 
    let (CompPath(scoref, cpath)) = m.CompilationPath
    CompPath(scoref, cpath@[(m.LogicalName, m.ModuleOrNamespaceType.ModuleOrNamespaceKind)])

// Can cpath2 be accessed given a right to access cpath1. That is, is cpath2 a nested type or namespace of cpath1. Note order of arguments.
let inline canAccessCompPathFrom (CompPath(scoref1, cpath1)) (CompPath(scoref2, cpath2)) =
    let rec loop p1 p2 = 
        match p1, p2 with 
        | (a1, k1) :: rest1, (a2, k2) :: rest2 -> (a1=a2) && (k1=k2) && loop rest1 rest2
        | [], _ -> true 
        | _ -> false // cpath1 is longer
    loop cpath1 cpath2 &&
    (scoref1 = scoref2)

let canAccessFromOneOf cpaths cpathTest =
    cpaths |> List.exists (fun cpath -> canAccessCompPathFrom cpath cpathTest) 

let canAccessFrom (TAccess x) cpath = 
    x |> List.forall (fun cpath1 -> canAccessCompPathFrom cpath1 cpath)

let canAccessFromEverywhere (TAccess x) = x.IsEmpty
let canAccessFromSomewhere (TAccess _) = true
let isLessAccessible (TAccess aa) (TAccess bb) = 
    not (aa |> List.forall(fun a -> bb |> List.exists (fun b -> canAccessCompPathFrom a b)))

/// Given (newPath, oldPath) replace oldPath by newPath in the TAccess.
let accessSubstPaths (newPath, oldPath) (TAccess paths) =
    let subst cpath = if cpath=oldPath then newPath else cpath
    TAccess (List.map subst paths)

let compPathOfCcu (ccu: CcuThunk) = CompPath(ccu.ILScopeRef, []) 
let taccessPublic = TAccess []
let taccessPrivate accessPath = TAccess [accessPath]
let compPathInternal = CompPath(ILScopeRef.Local, [])
let taccessInternal = TAccess [compPathInternal]
let combineAccess (TAccess a1) (TAccess a2) = TAccess(a1@a2)

exception Duplicate of string * string * range
exception NameClash of string * string * string * range * string * string * range

