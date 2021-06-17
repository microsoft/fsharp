// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.Detuple 

open Internal.Utilities.Collections
open Internal.Utilities.Library 
open Internal.Utilities.Library.Extras
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text
open FSharp.Compiler.Xml
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TypedTreeOps

// This pass has one aim.
// - to eliminate tuples allocated at call sites (due to uncurried style)
//
// After PASS,
//   Private, non-top-level functions fOrig which had explicit tuples at all callsites,
//   have been replaced by transformedVal taking the individual tuple fields,
//   subject to the type of the fOrig formal permitting the split.
// 
// The decisions are based on call site analysis 
//
//----------
// TUPLE COLLAPSE SIMPLIFIED.
//
// The aim of the optimization pass implemented in this module
// is to eliminate (redundant) tuple allocs arising due to calls.
// These typically arise from code written in uncurried form.
//
// Note that "top-level" functions and methods are automatically detupled in F#,
// by choice of representation. So this only applies to inner functions, and even
// then only to those not given "TLR" representation through lambda-lifting.
//
// Q: When is a tuple allocation at callsite redundant?
// A1: If the function called only wants the fields of the tuple.
// A2: If all call sites allocate a tuple argument,
//     then can factor that tuple creation into the function,
//     and hope the optimiser will eliminate it if possible.
//     e.g. if only the fields are required.
//
// The COLLAPSE transform is based on answer A2...
//
//   [[ let rec fOrig p = ... fOrig (a, b) ...
//      fOrig (x, y) ]]
//   ->
//      let rec transformedVal p1 p2 = let p = p1, p2
//                        ... (transformedVal a b) ...
//     
//      transformedVal x y
//
// Q: What about cases where some calls to fOrig provide just a tuple?
// A: If fOrig requires the original tuple argument, then this transform
//    would insert a tuple allocation inside fOrig, where none was before...
//
//----------
// IMPLEMENTATION OVERVIEW.
//
// 1. Require call-pattern info about callsites of each function, e.g.
//
//      [ (_, _) ; (_, (_, _, _)) ; _ ]
//      [ (_, _) ; (_, _)       ]
//      [ (_, _) ]
//
//    Detailing the number of arguments applied and their explicit tuple structure.
//
//    ASIDE: Efficiency note.
//           The rw pass does not change the call-pattern info,
//           so call-pattern info can be collected for all ids in pre-pass.
//
// 2. Given the above, can *CHOOSE* a call-pattern for the transformed function.
//    Informally,
//      Collapse any tuple structure if it is known at ALL call sites.
//    Formally,
//      - n = max length of call-pattern args.
//      - extend call patterns to length n with _ (no tuple info known)
//      - component-wise intersect argument tuple-structures over call patterns.
//      - gives least known call-pattern of length n.
//      - can trim to minimum non-trivial length.
//
//    [Used to] have INVARIANT on this chosen call pattern:
//
//      Have: For each argi with non-trivial tuple-structure,
//            at every call have an explicit tuple argument,
//            with (at least) that structure.
//            ----
//            Note, missing args in partial application will always
//            have trivial tuple structure in chosen call-pattern.
//
//    [PS: now defn arg projection info can override call site info]
//
// 2b.Choosing CallPattern also needs to check type of formals for the function.
//    If function is not expecting a tuple (according to types) do not split them.
//
// 3. Given CallPattern for selected fOrig,
//    (a) Can choose replacement formals, ybi where needed. (b, bar, means vector of formals).
//
//     cpi                | xi    | ybi
//    --------------------|-------|----------
//     UnknownTS          | xi    | SameArg xi
//     TupleTS []         | []    | SameArg []     // unit case, special case for now.
//     TupleTS ts1...tsN  | xi    | NewArgs (List.collect createFringeFormals [ts1..tsN])
//
//    (b) Can define transformedVal replacement function id.
//
// 4. Fixup defn bindings.
//
//    [[DEFN: fOrig  = LAM tps. lam x1 ...xp xq...xN. body ]]
//    ->
//           transformedVal = LAM tps. lam [[FORMALS: yb1...ybp]] xq...xN. [[REBINDS x1, yb1 ... xp, ybp]] [[FIX: body]]
//
//    [[FORMAL: SameArg xi]] -> xi
//    [[FORMAL: NewArgs vs]] -> [ [v1] ... [vN] ]                // list up individual args for Expr.Lambda
//
//    [[REBIND: xi, SameArg xi]] -> // no binding needed
//    [[REBIND: [u], NewArgs vs]] -> u = "rebuildTuple(cpi, vs)"
//    [[REBIND: us, NewArgs vs]] -> "rebuildTuple(cpi, vs)" then bind us to buildProjections. // for Expr.Lambda
//
//    rebuildTuple - create tuple based on vs fringe according to cpi tuple structure.
//
//    Note, fixup body...
//
// 5. Fixup callsites.
//
//    [[FIXCALL: APP fOrig tps args]] -> when fOrig is transformed, APP fOrig tps [[collapse args wrt cpf]]
//                                   otherwise, unchanged, APP fOrig tps args.
//
// 6. Overview.
//    - pre-pass to find callPatterns.
//    - choose CallPattern (tuple allocs on all callsites)
//    - create replacement formals and transformedVal where needed.
//    - rw pass over expr - fixing defns and applications as required.
//    - sanity checks and done.

// Note:  ids can occur in several ways in expr at this point in compiler.
//      val id                                        - freely
//      app (val id) tys args                         - applied to tys/args (if no args, then free occurrence)
//      app (reclink (val id)) tys args               - applied (recursive case)
//      app (reclink (app (val id) tys' []) tys args  - applied (recursive type instanced case)
// So, taking care counting callpatterns.
//
// Note: now considering defn projection requirements in decision.
//       no longer can assume that all call sites have explicit tuples if collapsing.
//       in these new cases, take care to have let binding sequence (eval order...)
 

// Merge a tyapp node and and app node.
let (|TyappAndApp|_|) e = 
    match e with 
    | Expr.App (f, fty, tys, args, m)       -> 
        match stripExpr f with
        | Expr.App (f2, fty2, tys2, [], m2) -> Some(f2, fty2, tys2 @ tys, args, m2)
        | Expr.App _                   -> Some(f, fty, tys, args, m) (* has args, so not combine ty args *)
        | f                             -> Some(f, fty, tys, args, m)
    | _ -> None

[<AutoOpen>]
module GlobalUsageAnalysis = 
    let bindAccBounds vals (_isInDTree, v) =  Zset.add v vals

    let GetValsBoundInExpr expr =
       let folder = {ExprFolder0 with valBindingSiteIntercept = bindAccBounds}
       let z0 = Zset.empty valOrder
       let z  = FoldExpr folder z0 expr
       z

    type accessor = TupleGet of int * TType list

    /// Expr information.
    /// For each v,
    ///  (a) log it's usage site context = accessors // APP type-inst args
    ///      where first accessor in list applies first to the v/app.
    ///   (b) log it's binding site representation.
    type Results =
       { ///  v -> context / APP inst args 
         Uses     : Zmap<Val, (accessor list * TType list * Expr list) list>
         /// v -> binding repr 
         Defns     : Zmap<Val, Expr>                                        
         /// bound in a decision tree? 
         DecisionTreeBindings    : Zset<Val>                                    
         ///  v -> v list * recursive? -- the others in the mutual binding 
         RecursiveBindings  : Zmap<Val, bool * Vals>
         TopLevelBindings : Zset<Val>
         IterationIsAtTopLevel      : bool }

    let z0 =
       { Uses     = Zmap.empty valOrder
         Defns     = Zmap.empty valOrder
         RecursiveBindings  = Zmap.empty valOrder
         DecisionTreeBindings    = Zset.empty valOrder
         TopLevelBindings = Zset.empty valOrder
         IterationIsAtTopLevel      = true }

    /// Log the use of a value with a particular tuple shape at a callsite
    /// Note: this routine is called very frequently
    let logUse (f: Val) tup z =
       {z with Uses = 
                  match Zmap.tryFind f z.Uses with
                  | Some sites -> Zmap.add f (tup :: sites) z.Uses
                  | None    -> Zmap.add f [tup] z.Uses }

    /// Log the definition of a binding
    let logBinding z (isInDTree, v) =
        let z = if isInDTree then {z with DecisionTreeBindings = Zset.add v z.DecisionTreeBindings} else z
        let z = if z.IterationIsAtTopLevel then {z with TopLevelBindings = Zset.add v z.TopLevelBindings} else z
        z

    /// Log the definition of a non-recursive binding
    let logNonRecBinding z (bind: Binding) =
        let v = bind.Var
        let vs = [v]
        {z with RecursiveBindings = Zmap.add v (false, vs) z.RecursiveBindings
                Defns = Zmap.add v bind.Expr z.Defns } 

    /// Log the definition of a recursive binding
    let logRecBindings z binds =
        let vs = valsOfBinds binds
        {z with RecursiveBindings = (z.RecursiveBindings, vs) ||> List.fold (fun mubinds v -> Zmap.add v (true, vs) mubinds)
                Defns    = (z.Defns, binds) ||> List.fold (fun eqns bind -> Zmap.add bind.Var bind.Expr eqns)  } 

    /// Work locally under a lambda of some kind
    let foldUnderLambda f z x =
        let saved = z.IterationIsAtTopLevel
        let z = {z with IterationIsAtTopLevel=false}
        let z = f z x
        let z = {z with IterationIsAtTopLevel=saved}
        z

    //-------------------------------------------------------------------------
    // GlobalUsageAnalysis - FoldExpr, foldBind collectors
    //-------------------------------------------------------------------------

    // Fold expr, intercepts selected exprs.
    //   "val v"        - count []     callpattern of v
    //   "app (f, args)" - count <args> callpattern of f
    //---
    // On intercepted nodes, must continue exprF fold over any subexpressions, e.g. args.
    //------
    // Also, noting top-level bindings,
    // so must cancel top-level "foldUnderLambda" whenever step under loop/lambda:
    //   - lambdas
    //   - try/with and try/finally
    //   - for body
    //   - match targets
    //   - tmethods
    let UsageFolders (g: TcGlobals) =
      let foldLocalVal f z (vref: ValRef) = 
          if valRefInThisAssembly g.compilingFslib vref then f z vref.Deref
          else z

      let exprUsageIntercept exprF noInterceptF z origExpr =

          let rec recognise context expr = 
             match expr with
             | Expr.Val (v, _, _) -> 
                 // YES: count free occurrence 
                 foldLocalVal (fun z v -> logUse v (context, [], []) z) z v

             | TyappAndApp(f, _, tys, args, _) -> 
                 match f with
                  | Expr.Val (fOrig, _, _) ->
                    // app where function is val 
                    // YES: count instance/app (app when have term args), and then
                    //      collect from args (have intercepted this node) 
                    let collect z f = logUse f (context, tys, args) z
                    let z = foldLocalVal collect z fOrig
                    List.fold exprF z args
                  | _ ->
                     // NO: app but function is not val 
                     noInterceptF z origExpr 

             | Expr.Op (TOp.TupleFieldGet (tupInfo, n), ts, [x], _) when not (evalTupInfoIsStruct tupInfo)  -> 
                 let context = TupleGet (n, ts) :: context
                 recognise context x
                 
             // lambdas end top-level status 
             | Expr.Lambda (_id, _ctorThisValOpt, _baseValOpt, _vs, body, _, _)   -> 
                 foldUnderLambda exprF z body

             | Expr.TyLambda (_id, _tps, body, _, _) -> 
                 foldUnderLambda exprF z body

             | _  -> 
                 noInterceptF z origExpr
          
          let context = []
          recognise context origExpr

      let targetIntercept exprF z = function TTarget(_argvs, body, _) -> Some (foldUnderLambda exprF z body)
      let tmethodIntercept exprF z = function TObjExprMethod(_, _, _, _, e, _m) -> Some (foldUnderLambda exprF z e)
      
      {ExprFolder0 with
         exprIntercept    = exprUsageIntercept
         nonRecBindingsIntercept = logNonRecBinding
         recBindingsIntercept    = logRecBindings
         valBindingSiteIntercept = logBinding
         targetIntercept  = targetIntercept
         tmethodIntercept = tmethodIntercept
      }


    //-------------------------------------------------------------------------
    // GlobalUsageAnalysis - entry point
    //-------------------------------------------------------------------------

    let GetUsageInfoOfImplFile g expr =
        let folder = UsageFolders g
        let z = FoldImplFile folder z0 expr
        z


let internalError str = raise(Failure(str))

let mkLocalVal m name ty topValInfo =
    let compgen    = false
    Construct.NewVal(name, m, None, ty, Immutable, compgen, topValInfo, taccessPublic, ValNotInRecScope, None, NormalVal, [], ValInline.Optional, XmlDoc.Empty, false, false, false, false, false, false, None, ParentNone) 

/// Represents inferred information about a tuple value
type TupleStructure = 
    | UnknownTS
    | TupleTS   of TupleStructure list

let rec ValReprInfoForTS ts = 
    match ts with 
    | UnknownTS  -> [ValReprInfo.unnamedTopArg]
    | TupleTS ts -> ts |> List.collect ValReprInfoForTS 

let rec andTS ts tsB =
    match ts, tsB with
    | _, UnknownTS -> UnknownTS
    | UnknownTS, _ -> UnknownTS
    | TupleTS ss, TupleTS ssB  -> 
        if ss.Length <> ssB.Length then UnknownTS (* different tuple instances *)
        else TupleTS (List.map2 andTS ss ssB)

let checkTS = function
    | TupleTS [] -> internalError "exprTS: Tuple[]  not expected. (units not done that way)."
    | TupleTS [_] -> internalError "exprTS: Tuple[x] not expected. (singleton tuples should not exist."
    | ts -> ts   
          
/// explicit tuple-structure in expr 
let rec uncheckedExprTS expr = 
    match expr with 
    | Expr.Op (TOp.Tuple tupInfo, _tys, args, _) when not (evalTupInfoIsStruct tupInfo) -> 
        TupleTS (List.map uncheckedExprTS args)
    | _ -> 
        UnknownTS

let rec uncheckedTypeTS g ty =
    if isRefTupleTy g ty then 
        let tys = destRefTupleTy g ty 
        TupleTS (List.map (uncheckedTypeTS g) tys)
    else 
        UnknownTS

let exprTS exprs = exprs |> uncheckedExprTS |> checkTS
let typeTS g tys = tys |> uncheckedTypeTS g |> checkTS

let rebuildTS g m ts vs =
    let rec rebuild vs ts = 
      match vs, ts with
      | [], UnknownTS   -> internalError "rebuildTS: not enough fringe to build tuple"
      | v :: vs, UnknownTS   -> (exprForVal m v, v.Type), vs
      | vs, TupleTS tss -> 
          let xtys, vs = List.mapFold rebuild vs tss
          let xs, tys  = List.unzip xtys
          let x  = mkRefTupled g m xs tys
          let ty = mkRefTupledTy g tys
          (x, ty), vs
   
    let (x, _ty), vs = rebuild vs ts
    if vs.Length <> 0 then internalError "rebuildTS: had more fringe vars than fringe. REPORT BUG" 
    x

/// CallPattern is tuple-structure for each argument position.
/// - callsites have a CallPattern (possibly instancing fOrig at tuple types...).
/// - the definition lambdas may imply a one-level CallPattern
/// - the definition formal projection info suggests a CallPattern
type CallPattern = TupleStructure list 
      
let callPatternOrder = (compare : CallPattern -> CallPattern -> int)
let argsCP exprs = List.map exprTS exprs
let noArgsCP = []
let inline isTrivialCP xs = isNil xs

let rec minimalCallPattern callPattern =
    match callPattern with 
    | []                -> []
    | UnknownTS :: tss    -> 
        match minimalCallPattern tss with
        | []  -> []              (* drop trailing UnknownTS *)
        | tss -> UnknownTS :: tss (* non triv tss tail *)
    | (TupleTS ts) :: tss -> TupleTS ts :: minimalCallPattern tss

/// Combines a list of callpatterns into one common callpattern.
let commonCallPattern callPatterns =
    let rec andCPs cpA cpB =
      match cpA, cpB with
      | [], []        -> []
      | tsA :: tsAs, tsB :: tsBs -> andTS tsA tsB :: andCPs tsAs tsBs
      | _tsA :: _tsAs, []        -> [] (* now trim to shortest - UnknownTS     :: andCPs tsAs []   *)
      | [], _tsB :: _tsBs -> [] (* now trim to shortest - UnknownTS     :: andCPs []   tsBs *)
   
    List.reduce andCPs callPatterns

let siteCP (_accessors, _inst, args) = argsCP args
let sitesCPs sites = List.map siteCP sites

//-------------------------------------------------------------------------
// transform
//-------------------------------------------------------------------------

type TransformedFormal =
  // Indicates that
  //    - the actual arg in this position is unchanged
  //    - also means that we keep the original formal arg
  | SameArg                          

  // Indicates 
  //    - the new formals for the transform
  //    - expr is tuple of the formals
  | NewArgs of Val list * Expr  

/// Info needed to convert f to curried form.
/// - yb1..ybp - replacement formal choices for x1...xp.
/// - transformedVal       - replaces f.
type Transform =
   { transformCallPattern : CallPattern
     transformedFormals   : TransformedFormal list 
     transformedVal       : Val }


//-------------------------------------------------------------------------
// transform - mkTransform - decided, create necessary stuff
//-------------------------------------------------------------------------

let mkTransform g (f: Val) m tps x1Ntys rty (callPattern, tyfringes: (TType list * Val list) list) =
    // Create formal choices for x1...xp under callPattern  
    let transformedFormals = 
        (callPattern, tyfringes) ||>  List.map2 (fun cpi (tyfringe, vs) -> 
            match cpi with
            | UnknownTS  -> SameArg
            | TupleTS [] -> SameArg  
            | TupleTS _ -> 
                // Try to keep the same names for the arguments if possible
                let vs = 
                    if vs.Length = tyfringe.Length then 
                        vs |> List.map (fun v -> mkCompGenLocal v.Range v.LogicalName v.Type |> fst)
                    else
                        let baseName = match vs with [v] -> v.LogicalName | _ -> "arg"
                        let baseRange = match vs with [v] -> v.Range | _ -> m
                        tyfringe |> List.mapi (fun i ty -> 
                            let name = baseName + string i
                            mkCompGenLocal baseRange name ty |> fst)
                        
                NewArgs (vs, rebuildTS g m cpi vs))
       
    // Create transformedVal replacement for f 
    // Mark the arity of the value 
    let topValInfo = 
        match f.ValReprInfo with 
        | None -> None 
        | _ -> Some(ValReprInfo (ValReprInfo.InferTyparInfo tps, List.collect ValReprInfoForTS callPattern, ValReprInfo.unnamedRetVal))
    (* type(transformedVal) tyfringes types replace initial arg types of f *)
    let tys1r = List.collect fst tyfringes  (* types for collapsed initial r args *)
    let tysrN = List.skip tyfringes.Length x1Ntys    (* types for remaining args *)
    let argtys = tys1r @ tysrN
    let fCty  = mkLambdaTy tps argtys rty
    let transformedVal =
        // Ensure that we have an g.CompilerGlobalState
        assert(g.CompilerGlobalState |> Option.isSome)
        mkLocalVal f.Range (g.CompilerGlobalState.Value.NiceNameGenerator.FreshCompilerGeneratedName (f.LogicalName, f.Range)) fCty topValInfo
    { transformCallPattern = callPattern
      transformedFormals      = transformedFormals
      transformedVal         = transformedVal }


//-------------------------------------------------------------------------
// transform - vTransforms - support
//-------------------------------------------------------------------------

let rec zipTupleStructureAndType g ts ty =
    // match a tuple-structure and type, yields:
    //  (a) (restricted) tuple-structure, and
    //  (b) type fringe for each arg position.
    match ts with
    | TupleTS tss when isRefTupleTy g ty ->
        let tys = destRefTupleTy g ty 
        let tss, tyfringe = zipTupleStructuresAndTypes g tss tys
        TupleTS tss, tyfringe
    | _ -> 
        UnknownTS, [ty] (* trim back CallPattern, function more general *)

and zipTupleStructuresAndTypes g tss tys =
    let tstys = List.map2 (zipTupleStructureAndType g) tss tys  // assumes tss tys same length 
    let tss  = List.map fst tstys         
    let tys = List.collect snd tstys       // link fringes 
    tss, tys

let zipCallPatternArgTys m g (callPattern : TupleStructure list) (vss : Val list list) =
    let vss = List.take callPattern.Length vss    // drop excessive tys if callPattern shorter 
    let tstys = List.map2 (fun ts vs -> let ts, tyfringe = zipTupleStructureAndType g ts (typeOfLambdaArg m vs) in ts, (tyfringe, vs)) callPattern vss
    List.unzip tstys   

//-------------------------------------------------------------------------
// transform - vTransforms - defnSuggestedCP
//-------------------------------------------------------------------------

/// v = LAM tps. lam vs1: ty1 ... vsN: tyN. body.
/// The types suggest a tuple structure CallPattern.
/// The buildProjections of the vsi trim this down,
/// since do not want to take as components any tuple that is required (projected to).
let decideFormalSuggestedCP g z tys vss =

    let rec trimTsByAccess accessors ts =
        match ts, accessors with
        | UnknownTS, _                       -> UnknownTS
        | TupleTS _tss, []                     -> UnknownTS (* trim it, require the val at this point *)
        | TupleTS tss, TupleGet (i, _ty) :: accessors -> 
            let tss = List.mapNth i (trimTsByAccess accessors) tss
            TupleTS tss

    let trimTsByVal z ts v =
        match Zmap.tryFind v z.Uses with
        | None       -> UnknownTS (* formal has no usage info, it is unused *)
        | Some sites -> 
            let trim ts (accessors, _inst, _args) = trimTsByAccess accessors ts
            List.fold trim ts sites

    let trimTsByFormal z ts vss = 
        match vss with 
        | [v]  -> trimTsByVal z ts v
        | vs   -> 
            let tss = match ts with TupleTS tss -> tss | _ -> internalError "trimByFormal: ts must be tuple?? PLEASE REPORT\n"
            let tss = List.map2 (trimTsByVal z) tss vs
            TupleTS tss

    let tss = List.map (typeTS g) tys (* most general TS according to type *)
    let tss = List.map2 (trimTsByFormal z) tss vss
    tss

//-------------------------------------------------------------------------
// transform - decideTransform
//-------------------------------------------------------------------------

let decideTransform g z v callPatterns (m, tps, vss: Val list list, rty) =
    let tys = List.map (typeOfLambdaArg m) vss       (* arg types *)
    (* NOTE: 'a in arg types may have been instanced at different tuples... *)
    (*       commonCallPattern has to handle those cases. *)
    let callPattern           = commonCallPattern callPatterns                   // common CallPattern 
    let callPattern           = List.truncate vss.Length callPattern            // restricted to max nArgs 
    // Get formal callPattern by defn usage of formals 
    let formalCallPattern     = decideFormalSuggestedCP g z tys vss 
    let callPattern           = List.truncate callPattern.Length formalCallPattern
    // Zip with information about known args 
    let callPattern, tyfringes = zipCallPatternArgTys m g callPattern vss
    // Drop trivial tail AND 
    let callPattern           = minimalCallPattern callPattern                     
    // Shorten tyfringes (zippable) 
    let tyfringes    = List.truncate callPattern.Length tyfringes       
    if isTrivialCP callPattern then
        None // no transform 
    else
        Some (v, mkTransform g v m tps tys rty (callPattern, tyfringes))


//-------------------------------------------------------------------------
// transform - determineTransforms
//-------------------------------------------------------------------------
      
// Public f could be used beyond assembly.
// For now, suppressing any transforms on these.
// Later, could transform f and fix up local calls and provide an f wrapper for beyond. 
let eligibleVal g m (v: Val) =
    let dllImportStubOrOtherNeverInline = (v.InlineInfo = ValInline.Never)
    let mutableVal = v.IsMutable
    let byrefVal = isByrefLikeTy g m v.Type
    not dllImportStubOrOtherNeverInline &&
    not byrefVal &&
    not mutableVal &&
    not v.IsMemberOrModuleBinding && //  .IsCompiledAsTopLevel &&
    not v.IsCompiledAsTopLevel 

let determineTransforms g (z : GlobalUsageAnalysis.Results) =
   let selectTransform (f: Val) sites =
     if not (eligibleVal g f.Range f) then None else
     // Consider f, if it has top-level lambda (meaning has term args) 
     match Zmap.tryFind f z.Defns with
     | None   -> None // no binding site, so no transform 
     | Some e -> 
        let tps, vss, _b, rty = stripTopLambda (e, f.Type)
        match List.concat vss with
        | []      -> None // defn has no term args 
        | arg1 :: _ -> // consider f 
          let m   = arg1.Range                       // mark of first arg, mostly for error reporting 
          let callPatterns = sitesCPs sites                   // callPatterns from sites 
          decideTransform g z f callPatterns (m, tps, vss, rty) // make transform (if required) 
  
   let vtransforms = Zmap.chooseL selectTransform z.Uses
   let vtransforms = Zmap.ofList valOrder vtransforms
   vtransforms

//-------------------------------------------------------------------------
// pass - penv - env of pass
//-------------------------------------------------------------------------

type penv =
    { // The planned transforms 
      transforms: Zmap<Val, Transform>
      ccu: CcuThunk
      g: TcGlobals
    }

let hasTransfrom penv f = Zmap.tryFind f penv.transforms

//-------------------------------------------------------------------------
// pass - app fixup - collapseArgs
//-------------------------------------------------------------------------

(* collapseArgs:
   - the args may not be tuples (decision made on defn projection).
   - need to factor any side-effecting args out into a let binding sequence.
   - also factor buildProjections, so they share common tmps.
*)

type env = 
    { 
      eg: TcGlobals

      prefix: string

      m: range
    }

    override _.ToString() = "<env>"

let suffixE env s = {env with prefix = env.prefix + s}

let rangeE  env m = {env with m = m}

let push  b  bs = b :: bs

let pushL xs bs = xs@bs

let newLocal  env   ty = mkCompGenLocal env.m env.prefix ty

let newLocalN env i ty = mkCompGenLocal env.m (env.prefix + string i) ty

let noEffectExpr env bindings x =
    match x with
    | Expr.Val (_v, _, _m) -> bindings, x
    | x                 -> 
        let tmp, xtmp = newLocal env (tyOfExpr env.eg x)
        let bind = mkCompGenBind tmp x
        push bind bindings, xtmp

// Given 'e', build 
//     let v1 = e#1
//     let v2 = e#N
let buildProjections env bindings x xtys =

    let binds, vixs = 
        xtys 
        |> List.mapi (fun i xty ->
            let vi, vix = newLocalN env i xty
            let bind = mkBind DebugPointAtBinding.NoneAtInvisible vi (mkTupleFieldGet env.eg (tupInfoRef, x, xtys, i, env.m))
            bind, vix)
        |> List.unzip

    // Why are we reversing here? Because we end up reversing once more later
    let bindings = pushL (List.rev binds) bindings
    bindings, vixs

let rec collapseArg env bindings ts (x: Expr) =
    let m = x.Range
    let env = rangeE env m
    match ts, x with
    | UnknownTS, x -> 
        let bindings, vx = noEffectExpr env bindings x
        bindings, [vx]
    | TupleTS tss, Expr.Op (TOp.Tuple tupInfo, _xtys, xs, _) when not (evalTupInfoIsStruct tupInfo) -> 
        let env = suffixE env "'"
        collapseArgs env bindings 1 tss xs
    | TupleTS tss, x                      -> 
        // project components 
        let bindings, x = noEffectExpr env bindings x
        let env  = suffixE env "_p" 
        let xty = tyOfExpr env.eg x
        let xtys = destRefTupleTy env.eg xty
        let bindings, xs = buildProjections env bindings x xtys
        collapseArg env bindings (TupleTS tss) (mkRefTupled env.eg m xs xtys)

and collapseArgs env bindings n (callPattern) args =
    match callPattern, args with
    | [], args        -> bindings, args
    | ts :: tss, arg :: args -> 
        let env1 = suffixE env (string n)
        let bindings, xty  = collapseArg  env1 bindings ts    arg     
        let bindings, xtys = collapseArgs env  bindings (n+1) tss args
        bindings, xty @ xtys
    | _ts :: _tss, []            -> 
        internalError "collapseArgs: CallPattern longer than callsite args. REPORT BUG"

//-------------------------------------------------------------------------
// pass - app fixup
//-------------------------------------------------------------------------

// REVIEW: use mkLet etc. 
let mkLets binds (body: Expr) = 
    (binds, body) ||> List.foldBack (fun b acc -> mkLetBind acc.Range b acc) 

let fixupApp (penv: penv) (fx, fty, tys, args, m) =

    // Is it a val app, where the val has a transform? 
    match fx with
    | Expr.Val (vref, _, m) -> 
        let f = vref.Deref
        match hasTransfrom penv f with
        | Some trans -> 
            // fix it 
            let callPattern       = trans.transformCallPattern 
            let transformedVal       = trans.transformedVal         
            let fCty     = transformedVal.Type
            let fCx      = exprForVal m transformedVal
            (* [[f tps args ]] -> transformedVal tps [[COLLAPSED: args]] *)
            let env      = {prefix = "arg";m = m;eg=penv.g}
            let bindings = []
            let bindings, args = collapseArgs env bindings 0 callPattern args
            let bindings = List.rev bindings
            mkLets bindings (Expr.App (fCx, fCty, tys, args, m))
        | None       -> 
            Expr.App (fx, fty, tys, args, m) (* no change, f untransformed val *)
    | _ -> 
        Expr.App (fx, fty, tys, args, m)                      (* no change, f is expr *)

//-------------------------------------------------------------------------
// pass - mubinds - translation support
//-------------------------------------------------------------------------

let transFormal ybi xi =
    match ybi with
    | SameArg         -> [xi]                          // one arg   - where arg=vpsecs 
    | NewArgs (vs, _x) -> vs |> List.map List.singleton // many args 

let transRebind ybi xi =
    match xi, ybi with
    | _, SameArg        -> []                    (* no rebinding, reused original formal *)
    | [u], NewArgs (_vs, x) -> [mkCompGenBind u x]
    | us, NewArgs (_vs, x) -> List.map2 mkCompGenBind us (tryDestRefTupleExpr x)


//-------------------------------------------------------------------------
// pass - mubinds
//-------------------------------------------------------------------------

// Foreach (f, repr) where
//   If f has trans, then
//   repr = LAM tps. lam x1...xN . body
//
//   transformedVal, yb1...ybp in trans.
//
// New binding:
//
//   transformedVal = LAM tps. lam [[FORMALS: yb1 ... ybp]] xq...xN = let [[REBINDS: x1, yb1 ...]]
//                                                        body
//
// Does not fix calls/defns in binding rhs, that is done by caller.
//

let passBind penv (TBind(fOrig, repr, letSeqPtOpt) as bind) =
     let m = fOrig.Range
     match hasTransfrom penv fOrig with
     | None ->
         // fOrig no transform 
         bind
     | Some trans ->
         // fOrig has transform 
         let tps, vss, body, rty = stripTopLambda (repr, fOrig.Type) 
         // transformedVal is curried version of fOrig 
         let transformedVal    = trans.transformedVal
         // fCBody - parts - formals 
         let transformedFormals = trans.transformedFormals 
         let p     = transformedFormals.Length
         if (vss.Length < p) then internalError "passBinds: |vss|<p - detuple pass" 
         let xqNs  = List.skip p vss  
         let x1ps  = List.truncate p vss  
         let y1Ps  = List.concat (List.map2 transFormal transformedFormals x1ps)
         let formals = y1Ps @ xqNs
         // fCBody - parts 
         let rebinds = List.concat (List.map2 transRebind transformedFormals x1ps)
         // fCBody - rebuild 
         // fCBody = TLambda tps. Lam formals. let rebinds in body 
         let rbody, rt  = mkLetsBind            m rebinds body, rty   
         let bind      = mkMultiLambdaBind transformedVal letSeqPtOpt m tps formals (rbody, rt)
         // result 
         bind

let passBinds penv binds = binds |> List.map (passBind penv) 

//-------------------------------------------------------------------------
// pass - passBindRhs
//
// At bindings (letrec/let),
//   0. run pass of bodies first.
//   1. transform bindings (as required),
//      yields new bindings and fixup data for callsites.
//   2. required to fixup any recursive calls in the bodies (beware O(n^2) cost)
//   3. run pass over following code.
//-------------------------------------------------------------------------

let passBindRhs conv (TBind (v, repr, letSeqPtOpt)) = TBind(v, conv repr, letSeqPtOpt)

let preInterceptExpr (penv: penv) conv expr =
  match expr with
  | Expr.LetRec (binds, e, m, _) ->
     let binds = List.map (passBindRhs conv) binds
     let binds = passBinds penv binds
     Some (mkLetRecBinds m binds (conv e))
  | Expr.Let (bind, e, m, _) ->  
     let bind = passBindRhs conv bind
     let bind = passBind penv bind
     Some (mkLetBind m bind (conv e))
  | TyappAndApp(f, fty, tys, args, m) ->
     // match app, and fixup if needed 
     let args = List.map conv args
     let f = conv f
     Some (fixupApp penv (f, fty, tys, args, m) )
  | _ -> None
  
let postTransformExpr (penv: penv) expr =
    match expr with
    | Expr.LetRec (binds, e, m, _) ->
        let binds = passBinds penv binds
        Some (mkLetRecBinds m binds e)
    | Expr.Let (bind, e, m, _) ->  
        let bind = passBind penv bind
        Some (mkLetBind m bind e)
    | TyappAndApp(f, fty, tys, args, m) ->
        // match app, and fixup if needed 
        Some (fixupApp penv (f, fty, tys, args, m) )
    | _ -> None
  
let passImplFile penv assembly = 
    assembly |> RewriteImplFile { PreIntercept =None
                                  PreInterceptBinding=None
                                  PostTransform= postTransformExpr penv
                                  IsUnderQuotations=false } 

//-------------------------------------------------------------------------
// entry point
//-------------------------------------------------------------------------

let DetupleImplFile ccu g expr =
   // Collect expr info - wanting usage contexts and bindings 
   let z = GetUsageInfoOfImplFile g expr

   // For each Val, decide Some "transform", or None if not changing
   let vtrans = determineTransforms g z

   // Pass over term, rewriting bindings and fixing up call sites, under penv 
   let penv = {g=g; transforms = vtrans; ccu = ccu}
   let expr = passImplFile penv expr
   expr
