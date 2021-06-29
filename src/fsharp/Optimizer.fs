// Copyright (c) Microsoft Corporation. All Rights Reserved. See License.txt in the project root for license information.

/// The F# expression simplifier. The main aim is to inline simple, known functions
/// and constant values, and to eliminate non-side-affecting bindings that 
/// are never used.
module internal FSharp.Compiler.Optimizer

open Internal.Utilities
open Internal.Utilities.Collections
open Internal.Utilities.Library
open Internal.Utilities.Library.Extras
open FSharp.Compiler
open FSharp.Compiler.AbstractIL.Diagnostics
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AttributeChecking
open FSharp.Compiler.CompilerGlobalState
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Infos
open FSharp.Compiler.Syntax
open FSharp.Compiler.SyntaxTreeOps
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Layout
open FSharp.Compiler.Text.LayoutRender
open FSharp.Compiler.Text.TaggedText
open FSharp.Compiler.TypedTree 
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.TypedTreeOps.DebugPrint
open FSharp.Compiler.TypedTreePickle
open FSharp.Compiler.TypeRelations

open System.Collections.Generic
open System.Collections.ObjectModel

#if DEBUG
let verboseOptimizationInfo = 
    try not (System.String.IsNullOrEmpty (System.Environment.GetEnvironmentVariable "FSHARP_verboseOptimizationInfo")) with _ -> false
let verboseOptimizations = 
    try not (System.String.IsNullOrEmpty (System.Environment.GetEnvironmentVariable "FSHARP_verboseOptimizations")) with _ -> false
#else
let [<Literal>] verboseOptimizationInfo = false
let [<Literal>] verboseOptimizations = false
#endif

let i_ldlen = [ I_ldlen; (AI_conv DT_I4) ] 

/// size of a function call 
let [<Literal>] callSize = 1  

/// size of a for/while loop
let [<Literal>] forAndWhileLoopSize = 5 

/// size of a try/with
let [<Literal>] tryWithSize = 5

/// size of a try/finally
let [<Literal>] tryFinallySize = 5 

/// Total cost of a closure. Each closure adds a class definition
let [<Literal>] closureTotalSize = 10 

/// Total cost of a method definition
let [<Literal>] methodDefnTotalSize = 1  

type TypeValueInfo =
  | UnknownTypeValue 

/// Partial information about an expression.
///
/// We store one of these for each value in the environment, including values 
/// which we know little or nothing about. 
type ExprValueInfo =
  | UnknownValue

  /// SizeValue(size, value)
  /// 
  /// Records size info (maxDepth) for an ExprValueInfo 
  | SizeValue of size: int * ExprValueInfo        

  /// ValValue(vref, value)
  ///
  /// Records that a value is equal to another value, along with additional
  /// information.
  | ValValue of ValRef * ExprValueInfo    

  | TupleValue of ExprValueInfo[]
  
  /// RecdValue(tycon, values)
  ///
  /// INVARIANT: values are in field definition order .
  | RecdValue of TyconRef * ExprValueInfo[]      

  | UnionCaseValue of UnionCaseRef * ExprValueInfo[]

  | ConstValue of Const * TType

  /// CurriedLambdaValue(id, arity, size, lambdaExpression, ty)
  ///    
  ///    arities: The number of bunches of untupled args and type args, and 
  ///             the number of args in each bunch. NOTE: This include type arguments.
  ///    expr: The value, a lambda term.
  ///    ty: The type of lambda term
  | CurriedLambdaValue of id: Unique * arity: int * size: int * value: Expr * TType

  /// ConstExprValue(size, value)
  | ConstExprValue of size: int * value: Expr

type ValInfo =
    { ValMakesNoCriticalTailcalls: bool

      ValExprInfo: ExprValueInfo 
    }

//-------------------------------------------------------------------------
// Partial information about entire namespace fragments or modules
//
// This is a somewhat nasty data structure since 
//   (a) we need the lookups to be very efficient
//   (b) we need to be able to merge these efficiently while building up the overall data for a module
//   (c) we pickle these to the binary optimization data format
//   (d) we don't want the process of unpickling the data structure to 
//       dereference/resolve all the ValRef's in the data structure, since
//       this would be slow on startup and a potential failure point should
//       any of the destination values not dereference correctly.
//
// It doesn't yet feel like we've got this data structure as good as it could be
//------------------------------------------------------------------------- 

/// Table of the values contained in one module
type ValInfos(entries) =

    let valInfoTable = 
        lazy (let t = ValHash.Create () 
              for (vref: ValRef, x) in entries do 
                   t.Add (vref.Deref, (vref, x))
              t)

    // The compiler's ValRef's in TcGlobals.fs that refer to things in FSharp.Core break certain invariants that hold elsewhere, 
    // because they dereference to point to Val's from signatures rather than Val's from implementations.
    // Thus a backup alternative resolution technique is needed for these when processing the FSharp.Core implementation files
    // holding these items. This resolution must be able to distinguish between overloaded methods, so we use
    // XmlDocSigOfVal as a cheap hack to get a unique item of data for a value.
    let valInfosForFslib = 
        LazyWithContext<_, TcGlobals>.Create ((fun g -> 
            let dict = 
                Dictionary<(ValRef * ValLinkageFullKey), (ValRef * ValInfo)>
                    (HashIdentity.FromFunctions
                         (fun (_: ValRef, k: ValLinkageFullKey) -> hash k.PartialKey)
                         (fun (v1, k1) (v2, k2) -> 
                             k1.PartialKey = k2.PartialKey && 
                             // dismbiguate overloads, somewhat low-perf but only use for a handful of overloads in FSharp.Core
                             match k1.TypeForLinkage, k2.TypeForLinkage with
                             | Some _, Some _ -> 
                                 let sig1 = XmlDocSigOfVal g true "" v1.Deref 
                                 let sig2 = XmlDocSigOfVal g true "" v2.Deref
                                 (sig1 = sig2) 
                             | None, None -> true
                             | _ -> false))
            for (vref, _x) as p in entries do 
                let vkey = (vref, vref.Deref.GetLinkageFullKey())
                if dict.ContainsKey vkey then 
                    failwithf "dictionary already contains key %A" vkey
                dict.Add(vkey, p) |> ignore
            ReadOnlyDictionary dict), id)

    member x.Entries = valInfoTable.Force().Values

    member x.Map f = ValInfos(Seq.map f x.Entries)

    member x.Filter f = ValInfos(Seq.filter f x.Entries)

    member x.TryFind (v: ValRef) = valInfoTable.Force().TryFind v.Deref

    member x.TryFindForFslib (g, vref: ValRef) =
        valInfosForFslib.Force(g).TryGetValue((vref, vref.Deref.GetLinkageFullKey()))

type ModuleInfo = 
    { ValInfos: ValInfos
      ModuleOrNamespaceInfos: NameMap<LazyModuleInfo> }
          
and LazyModuleInfo = Lazy<ModuleInfo>

type ImplFileOptimizationInfo = LazyModuleInfo

type CcuOptimizationInfo = LazyModuleInfo

#if DEBUG
let braceL x = leftL (tagText "{") ^^ x ^^ rightL (tagText "}")
let seqL xL xs = Seq.fold (fun z x -> z @@ xL x) emptyL xs
let namemapL xL xmap = NameMap.foldBack (fun nm x z -> xL nm x @@ z) xmap emptyL

let rec exprValueInfoL g exprVal =
    let exprL expr = exprL g expr
    match exprVal with
    | ConstValue (x, ty) -> NicePrint.layoutConst g ty x
    | UnknownValue -> wordL (tagText "?")
    | SizeValue (_, vinfo) -> exprValueInfoL g vinfo
    | ValValue (vr, vinfo) -> bracketL ((valRefL vr ^^ wordL (tagText "alias")) --- exprValueInfoL g vinfo)
    | TupleValue vinfos -> bracketL (exprValueInfosL g vinfos)
    | RecdValue (_, vinfos) -> braceL (exprValueInfosL g vinfos)
    | UnionCaseValue (ucr, vinfos) -> unionCaseRefL ucr ^^ bracketL (exprValueInfosL g vinfos)
    | CurriedLambdaValue(_lambdaId, _arities, _bsize, expr, _ety) -> wordL (tagText "lam") ++ exprL expr (* (sprintf "lam(size=%d)" bsize) *)
    | ConstExprValue (_size, x) -> exprL x

and exprValueInfosL g vinfos = commaListL (List.map (exprValueInfoL g) (Array.toList vinfos))

and moduleInfoL g (x: LazyModuleInfo) = 
    let x = x.Force()
    braceL ((wordL (tagText "Modules: ") @@ (x.ModuleOrNamespaceInfos |> namemapL (fun nm x -> wordL (tagText nm) ^^ moduleInfoL g x) ) )
            @@ (wordL (tagText "Values:") @@ (x.ValInfos.Entries |> seqL (fun (vref, x) -> valRefL vref ^^ valInfoL g x) )))

and valInfoL g (x: ValInfo) = 
    braceL ((wordL (tagText "ValExprInfo: ") @@ exprValueInfoL g x.ValExprInfo) 
            @@ (wordL (tagText "ValMakesNoCriticalTailcalls:") @@ wordL (tagText (if x.ValMakesNoCriticalTailcalls then "true" else "false"))))
#endif

type Summary<'Info> =
    { Info: 'Info 
      
      /// What's the contribution to the size of this function?
      FunctionSize: int 
      
      /// What's the total contribution to the size of the assembly, including closure classes etc.?
      TotalSize: int 
      
      /// Meaning: could mutate, could non-terminate, could raise exception 
      /// One use: an effect expr can not be eliminated as dead code (e.g. sequencing)
      /// One use: an effect=false expr can not throw an exception? so try-with is removed.
      HasEffect: bool  
      
      /// Indicates that a function may make a useful tailcall, hence when called should itself be tailcalled
      MightMakeCriticalTailcall: bool  
    }

//-------------------------------------------------------------------------
// BoundValueInfoBySize
// Note, this is a different notion of "size" to the one used for inlining heuristics
//------------------------------------------------------------------------- 

let rec SizeOfValueInfos (arr:_[]) =
    if arr.Length <= 0 then 0 else max 0 (SizeOfValueInfo arr.[0])

and SizeOfValueInfo x =
    match x with
    | SizeValue (vdepth, _v) -> vdepth // terminate recursion at CACHED size nodes
    | ConstValue (_x, _) -> 1
    | UnknownValue -> 1
    | ValValue (_vr, vinfo) -> SizeOfValueInfo vinfo + 1
    | TupleValue vinfos
    | RecdValue (_, vinfos)
    | UnionCaseValue (_, vinfos) -> 1 + SizeOfValueInfos vinfos
    | CurriedLambdaValue(_lambdaId, _arities, _bsize, _expr, _ety) -> 1
    | ConstExprValue (_size, _) -> 1

let [<Literal>] minDepthForASizeNode = 5  // for small vinfos do not record size info, save space

let rec MakeValueInfoWithCachedSize vdepth v =
    match v with
    | SizeValue(_, v) -> MakeValueInfoWithCachedSize vdepth v
    | _ -> if vdepth > minDepthForASizeNode then SizeValue(vdepth, v) else v (* add nodes to stop recursion *)
    
let MakeSizedValueInfo v =
    let vdepth = SizeOfValueInfo v
    MakeValueInfoWithCachedSize vdepth v

let BoundValueInfoBySize vinfo =
    let rec bound depth x =
        if depth < 0 then 
            UnknownValue
        else
            match x with
            | SizeValue (vdepth, vinfo) -> if vdepth < depth then x else MakeSizedValueInfo (bound depth vinfo)
            | ValValue (vr, vinfo) -> ValValue (vr, bound (depth-1) vinfo)
            | TupleValue vinfos -> TupleValue (Array.map (bound (depth-1)) vinfos)
            | RecdValue (tcref, vinfos) -> RecdValue (tcref, Array.map (bound (depth-1)) vinfos)
            | UnionCaseValue (ucr, vinfos) -> UnionCaseValue (ucr, Array.map (bound (depth-1)) vinfos)
            | ConstValue _ -> x
            | UnknownValue -> x
            | CurriedLambdaValue(_lambdaId, _arities, _bsize, _expr, _ety) -> x
            | ConstExprValue (_size, _) -> x
    let maxDepth = 6 (* beware huge constants! *)
    let trimDepth = 3
    let vdepth = SizeOfValueInfo vinfo
    if vdepth > maxDepth 
    then MakeSizedValueInfo (bound trimDepth vinfo)
    else MakeValueInfoWithCachedSize vdepth vinfo

//-------------------------------------------------------------------------
// Settings and optimizations
//------------------------------------------------------------------------- 

let [<Literal>] jitOptDefault = true

let [<Literal>] localOptDefault = true

let [<Literal>] crossModuleOptDefault = true

type OptimizationSettings = 
    { abstractBigTargets : bool
      
      jitOptUser : bool option
      
      localOptUser : bool option
      
      crossModuleOptUser : bool option 
      
      /// size after which we start chopping methods in two, though only at match targets 
      bigTargetSize : int   
      
      /// size after which we start enforcing splitting sub-expressions to new methods, to avoid hitting .NET IL limitations 
      veryBigExprSize : int 
      
      /// The size after which we don't inline
      lambdaInlineThreshold : int  
      
      /// For unit testing
      reportingPhase : bool
      
      reportNoNeedToTailcall: bool
      
      reportFunctionSizes : bool 
      
      reportHasEffect : bool 
      
      reportTotalSizes : bool
    }

    static member Defaults = 
        { abstractBigTargets = false
          jitOptUser = None
          localOptUser = None
          bigTargetSize = 100  
          veryBigExprSize = 3000 
          crossModuleOptUser = None
          lambdaInlineThreshold = 6
          reportingPhase = false
          reportNoNeedToTailcall = false
          reportFunctionSizes = false
          reportHasEffect = false
          reportTotalSizes = false
        }

    member x.jitOpt() = match x.jitOptUser with Some f -> f | None -> jitOptDefault

    member x.localOpt () = match x.localOptUser with Some f -> f | None -> localOptDefault

    member x.crossModuleOpt () = x.localOpt () && (match x.crossModuleOptUser with Some f -> f | None -> crossModuleOptDefault)

    member x.KeepOptimizationValues() = x.crossModuleOpt ()

    /// inline calls?
    member x.InlineLambdas () = x.localOpt ()  

    /// eliminate unused bindings with no effect 
    member x.EliminateUnusedBindings () = x.localOpt () 

    /// eliminate try around expr with no effect 
    member x.EliminateTryWithAndTryFinally () = false // deemed too risky, given tiny overhead of including try/with. See https://github.com/Microsoft/visualfsharp/pull/376

    /// eliminate first part of seq if no effect 
    member x.EliminateSequential () = x.localOpt () 

    /// determine branches in pattern matching
    member x.EliminateSwitch () = x.localOpt () 

    member x.EliminateRecdFieldGet () = x.localOpt () 

    member x.EliminateTupleFieldGet () = x.localOpt () 

    member x.EliminateUnionCaseFieldGet () = x.localOpt () 

    /// eliminate non-compiler generated immediate bindings 
    member x.EliminateImmediatelyConsumedLocals() = x.localOpt () 

    /// expand "let x = (exp1, exp2, ...)" bindings as prior tmps 
    /// expand "let x = Some exp1" bindings as prior tmps 
    member x.ExpandStructuralValues() = x.localOpt () 

type cenv =
    { g: TcGlobals
      
      TcVal : ConstraintSolver.TcValF
      
      amap: Import.ImportMap
      
      optimizing: bool
      
      scope: CcuThunk 
      
      localInternalVals: System.Collections.Generic.Dictionary<Stamp, ValInfo> 
      
      settings: OptimizationSettings
      
      emitTailcalls: bool
      
      /// cache methods with SecurityAttribute applied to them, to prevent unnecessary calls to ExistsInEntireHierarchyOfType
      casApplied: Dictionary<Stamp, bool>
    }

    override x.ToString() = "<cenv>"

type IncrementalOptimizationEnv =
    { /// An identifier to help with name generation
      latestBoundId: Ident option

      /// The set of lambda IDs we've inlined to reach this point. Helps to prevent recursive inlining 
      dontInline: Zset<Unique>  

      /// Recursively bound vars. If an sub-expression that is a candidate for method splitting
      /// contains any of these variables then don't split it, for fear of mucking up tailcalls.
      /// See FSharp 1.0 bug 2892
      dontSplitVars: ValMap<unit>

      /// Disable method splitting in loops
      disableMethodSplitting: bool

      /// The Val for the function binding being generated, if any. 
      functionVal: (Val * ValReprInfo) option

      typarInfos: (Typar * TypeValueInfo) list 

      localExternalVals: LayeredMap<Stamp, ValInfo>

      globalModuleInfos: LayeredMap<string, LazyModuleInfo>   
    }

    static member Empty = 
        { latestBoundId = None 
          dontInline = Zset.empty Int64.order
          typarInfos = []
          functionVal = None 
          dontSplitVars = ValMap.Empty
          disableMethodSplitting = false
          localExternalVals = LayeredMap.Empty 
          globalModuleInfos = LayeredMap.Empty }

    override x.ToString() = "<IncrementalOptimizationEnv>"

//-------------------------------------------------------------------------
// IsPartialExprVal - is the expr fully known?
//------------------------------------------------------------------------- 

/// IsPartialExprVal indicates the cases where we cant rebuild an expression
let rec IsPartialExprVal x =
    match x with
    | UnknownValue -> true
    | TupleValue args | RecdValue (_, args) | UnionCaseValue (_, args) -> Array.exists IsPartialExprVal args
    | ConstValue _ | CurriedLambdaValue _ | ConstExprValue _ -> false
    | ValValue (_, a) 
    | SizeValue(_, a) -> IsPartialExprVal a

let CheckInlineValueIsComplete (v: Val) res =
    if v.MustInline && IsPartialExprVal res then
        errorR(Error(FSComp.SR.optValueMarkedInlineButIncomplete(v.DisplayName), v.Range))
        //System.Diagnostics.Debug.Assert(false, sprintf "Break for incomplete inline value %s" v.DisplayName)

let check (vref: ValRef) (res: ValInfo) =
    CheckInlineValueIsComplete vref.Deref res.ValExprInfo
    (vref, res)

//-------------------------------------------------------------------------
// Bind information about values 
//------------------------------------------------------------------------- 

let EmptyModuleInfo = 
    notlazy { ValInfos = ValInfos([]); ModuleOrNamespaceInfos = Map.empty }

let rec UnionOptimizationInfos (minfos : seq<LazyModuleInfo>) = 
    notlazy
       { ValInfos =  
             ValInfos(seq { for minfo in minfos do yield! minfo.Force().ValInfos.Entries })

         ModuleOrNamespaceInfos = 
             minfos 
             |> Seq.map (fun m -> m.Force().ModuleOrNamespaceInfos) 
             |> NameMap.union UnionOptimizationInfos }

let FindOrCreateModuleInfo n (ss: Map<_, _>) = 
    match ss.TryFind n with 
    | Some res -> res
    | None -> EmptyModuleInfo

let FindOrCreateGlobalModuleInfo n (ss: LayeredMap<_, _>) = 
    match ss.TryFind n with 
    | Some res -> res
    | None -> EmptyModuleInfo

let rec BindValueInSubModuleFSharpCore (mp: string[]) i (v: Val) vval ss =
    if i < mp.Length then 
        {ss with ModuleOrNamespaceInfos = BindValueInModuleForFslib mp.[i] mp (i+1) v vval ss.ModuleOrNamespaceInfos }
    else 
        // REVIEW: this line looks quadratic for performance when compiling FSharp.Core
        {ss with ValInfos = ValInfos(Seq.append ss.ValInfos.Entries (Seq.singleton (mkLocalValRef v, vval))) }

and BindValueInModuleForFslib n mp i v vval (ss: NameMap<_>) =
    let old = FindOrCreateModuleInfo n ss
    Map.add n (notlazy (BindValueInSubModuleFSharpCore mp i v vval (old.Force()))) ss

and BindValueInGlobalModuleForFslib n mp i v vval (ss: LayeredMap<_, _>) =
    let old = FindOrCreateGlobalModuleInfo n ss
    ss.Add(n, notlazy (BindValueInSubModuleFSharpCore mp i v vval (old.Force())))

let BindValueForFslib (nlvref : NonLocalValOrMemberRef) v vval env =
    {env with globalModuleInfos = BindValueInGlobalModuleForFslib nlvref.AssemblyName nlvref.EnclosingEntity.nlr.Path 0 v vval env.globalModuleInfos }

let UnknownValInfo = { ValExprInfo=UnknownValue; ValMakesNoCriticalTailcalls=false }

let mkValInfo info (v: Val) = { ValExprInfo=info.Info; ValMakesNoCriticalTailcalls= v.MakesNoCriticalTailcalls }

(* Bind a value *)
let BindInternalLocalVal cenv (v: Val) vval env = 
    let vval = if v.IsMutable then UnknownValInfo else vval

    match vval.ValExprInfo with 
    | UnknownValue -> env
    | _ ->
        cenv.localInternalVals.[v.Stamp] <- vval
        env
        
let BindExternalLocalVal cenv (v: Val) vval env = 
    let vval = if v.IsMutable then {vval with ValExprInfo=UnknownValue } else vval
    let env =
        match vval.ValExprInfo with 
        | UnknownValue -> env  
        | _ ->
            { env with localExternalVals=env.localExternalVals.Add (v.Stamp, vval) }
    // If we're compiling fslib then also bind the value as a non-local path to 
    // allow us to resolve the compiler-non-local-references that arise from env.fs
    //
    // Do this by generating a fake "looking from the outside in" non-local value reference for
    // v, dereferencing it to find the corresponding signature Val, and adding an entry for the signature val.
    //
    // A similar code path exists in ilxgen.fs for the tables of "representations" for values
    let env = 
        if cenv.g.compilingFslib then 
            // Passing an empty remap is sufficient for FSharp.Core.dll because it turns out the remapped type signature can
            // still be resolved.
            match tryRescopeVal cenv.g.fslibCcu Remap.Empty v with 
            | ValueSome vref -> BindValueForFslib vref.nlr v vval env 
            | _ -> env
        else env
    env

let rec BindValsInModuleOrNamespace cenv (mval: LazyModuleInfo) env =
    let mval = mval.Force()
    // do all the sub modules
    let env = (mval.ModuleOrNamespaceInfos, env) ||> NameMap.foldBackRange (BindValsInModuleOrNamespace cenv) 
    let env = (env, mval.ValInfos.Entries) ||> Seq.fold (fun env (v: ValRef, vval) -> BindExternalLocalVal cenv v.Deref vval env) 
    env

let inline BindInternalValToUnknown cenv v env = 
    ignore cenv 
    ignore v
    env

let inline BindInternalValsToUnknown cenv vs env = 
    ignore cenv
    ignore vs
    env

let BindTypeVar tyv typeinfo env = { env with typarInfos= (tyv, typeinfo) :: env.typarInfos } 

let BindTypeVarsToUnknown (tps: Typar list) env = 
    if isNil tps then env else
    // The optimizer doesn't use the type values it could track. 
    // However here we mutate to provide better names for generalized type parameters 
    // The names chosen are 'a', 'b' etc. These are also the compiled names in the IL code
    let nms = PrettyTypes.PrettyTyparNames (fun _ -> true) (env.typarInfos |> List.map (fun (tp, _) -> tp.Name) ) tps
    (tps, nms) ||> List.iter2 (fun tp nm -> 
            if PrettyTypes.NeedsPrettyTyparName tp then 
                tp.typar_id <- ident (nm, tp.Range))      
    List.fold (fun sofar arg -> BindTypeVar arg UnknownTypeValue sofar) env tps 

let BindCcu (ccu: CcuThunk) mval env (_g: TcGlobals) = 
    { env with globalModuleInfos=env.globalModuleInfos.Add(ccu.AssemblyName, mval) }

/// Lookup information about values 
let GetInfoForLocalValue cenv env (v: Val) m = 
    // Abstract slots do not have values 
    if v.IsDispatchSlot then UnknownValInfo 
    else
        match cenv.localInternalVals.TryGetValue v.Stamp with
        | true, res -> res
        | _ ->
            match env.localExternalVals.TryFind v.Stamp with 
            | Some vval -> vval
            | None -> 
                if v.MustInline then
                    errorR(Error(FSComp.SR.optValueMarkedInlineButWasNotBoundInTheOptEnv(fullDisplayTextOfValRef (mkLocalValRef v)), m))
                UnknownValInfo 

let TryGetInfoForCcu env (ccu: CcuThunk) = env.globalModuleInfos.TryFind(ccu.AssemblyName)

let TryGetInfoForEntity sv n = 
    match sv.ModuleOrNamespaceInfos.TryFind n with 
    | Some info -> Some (info.Force())
    | None -> None

let rec TryGetInfoForPath sv (p:_[]) i = 
    if i >= p.Length then Some sv else 
    match TryGetInfoForEntity sv p.[i] with 
    | Some info -> TryGetInfoForPath info p (i+1)
    | None -> None

let TryGetInfoForNonLocalEntityRef env (nleref: NonLocalEntityRef) = 
    match TryGetInfoForCcu env nleref.Ccu with 
    | Some ccuinfo -> TryGetInfoForPath (ccuinfo.Force()) nleref.Path 0
    | None -> None
              
let GetInfoForNonLocalVal cenv env (vref: ValRef) =
    if vref.IsDispatchSlot then 
        UnknownValInfo
    // REVIEW: optionally turn x-module on/off on per-module basis or  
    elif cenv.settings.crossModuleOpt () || vref.MustInline then 
        match TryGetInfoForNonLocalEntityRef env vref.nlr.EnclosingEntity.nlr with
        | Some structInfo ->
            match structInfo.ValInfos.TryFind vref with 
            | Some ninfo -> snd ninfo
            | None -> 
                  //dprintn ("\n\n*** Optimization info for value "+n+" from module "+(full_name_of_nlpath smv)+" not found, module contains values: "+String.concat ", " (NameMap.domainL structInfo.ValInfos))  
                  //System.Diagnostics.Debug.Assert(false, sprintf "Break for module %s, value %s" (full_name_of_nlpath smv) n)
                  if cenv.g.compilingFslib then 
                      match structInfo.ValInfos.TryFindForFslib (cenv.g, vref) with 
                      | true, ninfo -> snd ninfo
                      | _ -> UnknownValInfo
                  else
                      UnknownValInfo
        | None -> 
            //dprintf "\n\n*** Optimization info for module %s from ccu %s not found." (full_name_of_nlpath smv) (ccu_of_nlpath smv).AssemblyName  
            //System.Diagnostics.Debug.Assert(false, sprintf "Break for module %s, ccu %s" (full_name_of_nlpath smv) (ccu_of_nlpath smv).AssemblyName)
            UnknownValInfo
    else 
        UnknownValInfo

let GetInfoForVal cenv env m (vref: ValRef) =  
    let res = 
        if vref.IsLocalRef then
            GetInfoForLocalValue cenv env vref.binding m
        else
            GetInfoForNonLocalVal cenv env vref

    check vref res |> ignore
    res

//-------------------------------------------------------------------------
// Try to get information about values of particular types
//------------------------------------------------------------------------- 

let rec stripValue = function
  | ValValue(_, details) -> stripValue details (* step through ValValue "aliases" *) 
  | SizeValue(_, details) -> stripValue details (* step through SizeValue "aliases" *) 
  | vinfo -> vinfo

let (|StripConstValue|_|) ev = 
  match stripValue ev with
  | ConstValue(c, _) -> Some c
  | _ -> None

let (|StripLambdaValue|_|) ev = 
  match stripValue ev with 
  | CurriedLambdaValue (id, arity, sz, expr, ty) -> Some (id, arity, sz, expr, ty)
  | _ -> None

let destTupleValue ev = 
  match stripValue ev with 
  | TupleValue info -> Some info
  | _ -> None

let destRecdValue ev = 
  match stripValue ev with 
  | RecdValue (_tcref, info) -> Some info
  | _ -> None

let (|StripUnionCaseValue|_|) ev = 
  match stripValue ev with 
  | UnionCaseValue (c, info) -> Some (c, info)
  | _ -> None

let mkBoolVal (g: TcGlobals) n = ConstValue(Const.Bool n, g.bool_ty)

let mkInt8Val (g: TcGlobals) n = ConstValue(Const.SByte n, g.sbyte_ty)

let mkInt16Val (g: TcGlobals) n = ConstValue(Const.Int16 n, g.int16_ty)

let mkInt32Val (g: TcGlobals) n = ConstValue(Const.Int32 n, g.int32_ty)

let mkInt64Val (g: TcGlobals) n = ConstValue(Const.Int64 n, g.int64_ty)

let mkUInt8Val (g: TcGlobals) n = ConstValue(Const.Byte n, g.byte_ty)

let mkUInt16Val (g: TcGlobals) n = ConstValue(Const.UInt16 n, g.uint16_ty)

let mkUInt32Val (g: TcGlobals) n = ConstValue(Const.UInt32 n, g.uint32_ty)

let mkUInt64Val (g: TcGlobals) n = ConstValue(Const.UInt64 n, g.uint64_ty)

let (|StripInt32Value|_|) = function StripConstValue(Const.Int32 n) -> Some n | _ -> None
      
let MakeValueInfoForValue g m vref vinfo = 
#if DEBUG
    let rec check x = 
        match x with 
        | ValValue (vref2, detail) -> if valRefEq g vref vref2 then error(Error(FSComp.SR.optRecursiveValValue(showL(exprValueInfoL g vinfo)), m)) else check detail
        | SizeValue (_n, detail) -> check detail
        | _ -> ()
    check vinfo
#else
    ignore g; ignore m
#endif
    ValValue (vref, vinfo) |> BoundValueInfoBySize

let MakeValueInfoForRecord tcref argvals = 
    RecdValue (tcref, argvals) |> BoundValueInfoBySize

let MakeValueInfoForTuple argvals = 
    TupleValue argvals |> BoundValueInfoBySize

let MakeValueInfoForUnionCase cspec argvals =
    UnionCaseValue (cspec, argvals) |> BoundValueInfoBySize

let MakeValueInfoForConst c ty = ConstValue(c, ty)

/// Helper to evaluate a unary integer operation over known values
let inline IntegerUnaryOp g f8 f16 f32 f64 fu8 fu16 fu32 fu64 a = 
     match a with
     | StripConstValue c -> 
         match c with 
         | Const.Bool a -> Some(mkBoolVal g (f32 (if a then 1 else 0) <> 0))
         | Const.Int32 a -> Some(mkInt32Val g (f32 a))
         | Const.Int64 a -> Some(mkInt64Val g (f64 a))
         | Const.Int16 a -> Some(mkInt16Val g (f16 a))
         | Const.SByte a -> Some(mkInt8Val g (f8 a))
         | Const.Byte a -> Some(mkUInt8Val g (fu8 a))
         | Const.UInt32 a -> Some(mkUInt32Val g (fu32 a))
         | Const.UInt64 a -> Some(mkUInt64Val g (fu64 a))
         | Const.UInt16 a -> Some(mkUInt16Val g (fu16 a))
         | _ -> None
     | _ -> None

/// Helper to evaluate a unary signed integer operation over known values
let inline SignedIntegerUnaryOp g f8 f16 f32 f64 a = 
     match a with
     | StripConstValue c -> 
         match c with 
         | Const.Int32 a -> Some(mkInt32Val g (f32 a))
         | Const.Int64 a -> Some(mkInt64Val g (f64 a))
         | Const.Int16 a -> Some(mkInt16Val g (f16 a))
         | Const.SByte a -> Some(mkInt8Val g (f8 a))
         | _ -> None
     | _ -> None
         
/// Helper to evaluate a binary integer operation over known values
let inline IntegerBinaryOp g f8 f16 f32 f64 fu8 fu16 fu32 fu64 a b = 
     match a, b with
     | StripConstValue c1, StripConstValue c2 -> 
         match c1, c2 with 
         | (Const.Bool a), (Const.Bool b) -> Some(mkBoolVal g (f32 (if a then 1 else 0) (if b then 1 else 0) <> 0))
         | (Const.Int32 a), (Const.Int32 b) -> Some(mkInt32Val g (f32 a b))
         | (Const.Int64 a), (Const.Int64 b) -> Some(mkInt64Val g (f64 a b))
         | (Const.Int16 a), (Const.Int16 b) -> Some(mkInt16Val g (f16 a b))
         | (Const.SByte a), (Const.SByte b) -> Some(mkInt8Val g (f8 a b))
         | (Const.Byte a), (Const.Byte b) -> Some(mkUInt8Val g (fu8 a b))
         | (Const.UInt16 a), (Const.UInt16 b) -> Some(mkUInt16Val g (fu16 a b))
         | (Const.UInt32 a), (Const.UInt32 b) -> Some(mkUInt32Val g (fu32 a b))
         | (Const.UInt64 a), (Const.UInt64 b) -> Some(mkUInt64Val g (fu64 a b))
         | _ -> None
     | _ -> None

module Unchecked = Microsoft.FSharp.Core.Operators
         
/// Evaluate primitives based on interpretation of IL instructions. 
///
/// The implementation utilizes F# arithmetic extensively, so a mistake in the implementation of F# arithmetic  
/// in the core library used by the F# compiler will propagate to be a mistake in optimization. 
/// The IL instructions appear in the tree through inlining.
let mkAssemblyCodeValueInfo g instrs argvals tys =
  match instrs, argvals, tys with
    | [ AI_add ], [t1;t2], _ -> 
         // Note: each use of Unchecked.(+) gets instantiated at a different type and inlined
         match IntegerBinaryOp g Unchecked.(+) Unchecked.(+) Unchecked.(+) Unchecked.(+) Unchecked.(+) Unchecked.(+) Unchecked.(+) Unchecked.(+) t1 t2 with 
         | Some res -> res
         | _ -> UnknownValue
    | [ AI_sub ], [t1;t2], _ -> 
         // Note: each use of Unchecked.(+) gets instantiated at a different type and inlined
         match IntegerBinaryOp g Unchecked.(-) Unchecked.(-) Unchecked.(-) Unchecked.(-) Unchecked.(-) Unchecked.(-) Unchecked.(-) Unchecked.(-) t1 t2 with 
         | Some res -> res
         | _ -> UnknownValue
    | [ AI_mul ], [a;b], _ -> (match IntegerBinaryOp g Unchecked.( * ) Unchecked.( * ) Unchecked.( * ) Unchecked.( * ) Unchecked.( * ) Unchecked.( * ) Unchecked.( * ) Unchecked.( * ) a b with Some res -> res | None -> UnknownValue)
    | [ AI_and ], [a;b], _ -> (match IntegerBinaryOp g (&&&) (&&&) (&&&) (&&&) (&&&) (&&&) (&&&) (&&&) a b with Some res -> res | None -> UnknownValue)
    | [ AI_or ], [a;b], _ -> (match IntegerBinaryOp g (|||) (|||) (|||) (|||) (|||) (|||) (|||) (|||) a b with Some res -> res | None -> UnknownValue)
    | [ AI_xor ], [a;b], _ -> (match IntegerBinaryOp g (^^^) (^^^) (^^^) (^^^) (^^^) (^^^) (^^^) (^^^) a b with Some res -> res | None -> UnknownValue)
    | [ AI_not ], [a], _ -> (match IntegerUnaryOp g (~~~) (~~~) (~~~) (~~~) (~~~) (~~~) (~~~) (~~~) a with Some res -> res | None -> UnknownValue)
    | [ AI_neg ], [a], _ -> (match SignedIntegerUnaryOp g (~-) (~-) (~-) (~-) a with Some res -> res | None -> UnknownValue)

    | [ AI_ceq ], [a;b], _ -> 
       match stripValue a, stripValue b with
       | ConstValue(Const.Bool a1, _), ConstValue(Const.Bool a2, _) -> mkBoolVal g (a1 = a2)
       | ConstValue(Const.SByte a1, _), ConstValue(Const.SByte a2, _) -> mkBoolVal g (a1 = a2)
       | ConstValue(Const.Int16 a1, _), ConstValue(Const.Int16 a2, _) -> mkBoolVal g (a1 = a2)
       | ConstValue(Const.Int32 a1, _), ConstValue(Const.Int32 a2, _) -> mkBoolVal g (a1 = a2)
       | ConstValue(Const.Int64 a1, _), ConstValue(Const.Int64 a2, _) -> mkBoolVal g (a1 = a2)
       | ConstValue(Const.Char a1, _), ConstValue(Const.Char a2, _) -> mkBoolVal g (a1 = a2)
       | ConstValue(Const.Byte a1, _), ConstValue(Const.Byte a2, _) -> mkBoolVal g (a1 = a2)
       | ConstValue(Const.UInt16 a1, _), ConstValue(Const.UInt16 a2, _) -> mkBoolVal g (a1 = a2)
       | ConstValue(Const.UInt32 a1, _), ConstValue(Const.UInt32 a2, _) -> mkBoolVal g (a1 = a2)
       | ConstValue(Const.UInt64 a1, _), ConstValue(Const.UInt64 a2, _) -> mkBoolVal g (a1 = a2)
       | _ -> UnknownValue
    | [ AI_clt ], [a;b], _ -> 
       match stripValue a, stripValue b with
       | ConstValue(Const.Bool a1, _), ConstValue(Const.Bool a2, _) -> mkBoolVal g (a1 < a2)
       | ConstValue(Const.Int32 a1, _), ConstValue(Const.Int32 a2, _) -> mkBoolVal g (a1 < a2)
       | ConstValue(Const.Int64 a1, _), ConstValue(Const.Int64 a2, _) -> mkBoolVal g (a1 < a2)
       | ConstValue(Const.SByte a1, _), ConstValue(Const.SByte a2, _) -> mkBoolVal g (a1 < a2)
       | ConstValue(Const.Int16 a1, _), ConstValue(Const.Int16 a2, _) -> mkBoolVal g (a1 < a2)
       | _ -> UnknownValue
    | [ (AI_conv(DT_U1))], [a], [ty] when typeEquiv g ty g.byte_ty -> 
       match stripValue a with
       | ConstValue(Const.SByte a, _) -> mkUInt8Val g (Unchecked.byte a)
       | ConstValue(Const.Int16 a, _) -> mkUInt8Val g (Unchecked.byte a)
       | ConstValue(Const.Int32 a, _) -> mkUInt8Val g (Unchecked.byte a)
       | ConstValue(Const.Int64 a, _) -> mkUInt8Val g (Unchecked.byte a)
       | ConstValue(Const.Byte a, _) -> mkUInt8Val g (Unchecked.byte a)
       | ConstValue(Const.UInt16 a, _) -> mkUInt8Val g (Unchecked.byte a)
       | ConstValue(Const.UInt32 a, _) -> mkUInt8Val g (Unchecked.byte a)
       | ConstValue(Const.UInt64 a, _) -> mkUInt8Val g (Unchecked.byte a)
       | _ -> UnknownValue
    | [ (AI_conv(DT_U2))], [a], [ty] when typeEquiv g ty g.uint16_ty -> 
       match stripValue a with
       | ConstValue(Const.SByte a, _) -> mkUInt16Val g (Unchecked.uint16 a)
       | ConstValue(Const.Int16 a, _) -> mkUInt16Val g (Unchecked.uint16 a)
       | ConstValue(Const.Int32 a, _) -> mkUInt16Val g (Unchecked.uint16 a)
       | ConstValue(Const.Int64 a, _) -> mkUInt16Val g (Unchecked.uint16 a)
       | ConstValue(Const.Byte a, _) -> mkUInt16Val g (Unchecked.uint16 a)
       | ConstValue(Const.UInt16 a, _) -> mkUInt16Val g (Unchecked.uint16 a)
       | ConstValue(Const.UInt32 a, _) -> mkUInt16Val g (Unchecked.uint16 a)
       | ConstValue(Const.UInt64 a, _) -> mkUInt16Val g (Unchecked.uint16 a)
       | _ -> UnknownValue
    | [ (AI_conv(DT_U4))], [a], [ty] when typeEquiv g ty g.uint32_ty -> 
       match stripValue a with
       | ConstValue(Const.SByte a, _) -> mkUInt32Val g (Unchecked.uint32 a)
       | ConstValue(Const.Int16 a, _) -> mkUInt32Val g (Unchecked.uint32 a)
       | ConstValue(Const.Int32 a, _) -> mkUInt32Val g (Unchecked.uint32 a)
       | ConstValue(Const.Int64 a, _) -> mkUInt32Val g (Unchecked.uint32 a)
       | ConstValue(Const.Byte a, _) -> mkUInt32Val g (Unchecked.uint32 a)
       | ConstValue(Const.UInt16 a, _) -> mkUInt32Val g (Unchecked.uint32 a)
       | ConstValue(Const.UInt32 a, _) -> mkUInt32Val g (Unchecked.uint32 a)
       | ConstValue(Const.UInt64 a, _) -> mkUInt32Val g (Unchecked.uint32 a)
       | _ -> UnknownValue
    | [ (AI_conv(DT_U8))], [a], [ty] when typeEquiv g ty g.uint64_ty -> 
       match stripValue a with
       | ConstValue(Const.SByte a, _) -> mkUInt64Val g (Unchecked.uint64 a)
       | ConstValue(Const.Int16 a, _) -> mkUInt64Val g (Unchecked.uint64 a)
       | ConstValue(Const.Int32 a, _) -> mkUInt64Val g (Unchecked.uint64 a)
       | ConstValue(Const.Int64 a, _) -> mkUInt64Val g (Unchecked.uint64 a)
       | ConstValue(Const.Byte a, _) -> mkUInt64Val g (Unchecked.uint64 a)
       | ConstValue(Const.UInt16 a, _) -> mkUInt64Val g (Unchecked.uint64 a)
       | ConstValue(Const.UInt32 a, _) -> mkUInt64Val g (Unchecked.uint64 a)
       | ConstValue(Const.UInt64 a, _) -> mkUInt64Val g (Unchecked.uint64 a)
       | _ -> UnknownValue
    | [ (AI_conv(DT_I1))], [a], [ty] when typeEquiv g ty g.sbyte_ty -> 
       match stripValue a with
       | ConstValue(Const.SByte a, _) -> mkInt8Val g (Unchecked.sbyte a)
       | ConstValue(Const.Int16 a, _) -> mkInt8Val g (Unchecked.sbyte a)
       | ConstValue(Const.Int32 a, _) -> mkInt8Val g (Unchecked.sbyte a)
       | ConstValue(Const.Int64 a, _) -> mkInt8Val g (Unchecked.sbyte a)
       | ConstValue(Const.Byte a, _) -> mkInt8Val g (Unchecked.sbyte a)
       | ConstValue(Const.UInt16 a, _) -> mkInt8Val g (Unchecked.sbyte a)
       | ConstValue(Const.UInt32 a, _) -> mkInt8Val g (Unchecked.sbyte a)
       | ConstValue(Const.UInt64 a, _) -> mkInt8Val g (Unchecked.sbyte a)
       | _ -> UnknownValue
    | [ (AI_conv(DT_I2))], [a], [ty] when typeEquiv g ty g.int16_ty -> 
       match stripValue a with
       | ConstValue(Const.Int32 a, _) -> mkInt16Val g (Unchecked.int16 a)
       | ConstValue(Const.Int16 a, _) -> mkInt16Val g (Unchecked.int16 a)
       | ConstValue(Const.SByte a, _) -> mkInt16Val g (Unchecked.int16 a)
       | ConstValue(Const.Int64 a, _) -> mkInt16Val g (Unchecked.int16 a)
       | ConstValue(Const.UInt32 a, _) -> mkInt16Val g (Unchecked.int16 a)
       | ConstValue(Const.UInt16 a, _) -> mkInt16Val g (Unchecked.int16 a)
       | ConstValue(Const.Byte a, _) -> mkInt16Val g (Unchecked.int16 a)
       | ConstValue(Const.UInt64 a, _) -> mkInt16Val g (Unchecked.int16 a)
       | _ -> UnknownValue
    | [ (AI_conv(DT_I4))], [a], [ty] when typeEquiv g ty g.int32_ty -> 
       match stripValue a with
       | ConstValue(Const.Int32 a, _) -> mkInt32Val g (Unchecked.int32 a)
       | ConstValue(Const.Int16 a, _) -> mkInt32Val g (Unchecked.int32 a)
       | ConstValue(Const.SByte a, _) -> mkInt32Val g (Unchecked.int32 a)
       | ConstValue(Const.Int64 a, _) -> mkInt32Val g (Unchecked.int32 a)
       | ConstValue(Const.UInt32 a, _) -> mkInt32Val g (Unchecked.int32 a)
       | ConstValue(Const.UInt16 a, _) -> mkInt32Val g (Unchecked.int32 a)
       | ConstValue(Const.Byte a, _) -> mkInt32Val g (Unchecked.int32 a)
       | ConstValue(Const.UInt64 a, _) -> mkInt32Val g (Unchecked.int32 a)
       | _ -> UnknownValue
    | [ (AI_conv(DT_I8))], [a], [ty] when typeEquiv g ty g.int64_ty -> 
       match stripValue a with
       | ConstValue(Const.Int32 a, _) -> mkInt64Val g (Unchecked.int64 a)
       | ConstValue(Const.Int16 a, _) -> mkInt64Val g (Unchecked.int64 a)
       | ConstValue(Const.SByte a, _) -> mkInt64Val g (Unchecked.int64 a)
       | ConstValue(Const.Int64 a, _) -> mkInt64Val g (Unchecked.int64 a)
       | ConstValue(Const.UInt32 a, _) -> mkInt64Val g (Unchecked.int64 a)
       | ConstValue(Const.UInt16 a, _) -> mkInt64Val g (Unchecked.int64 a)
       | ConstValue(Const.Byte a, _) -> mkInt64Val g (Unchecked.int64 a)
       | ConstValue(Const.UInt64 a, _) -> mkInt64Val g (Unchecked.int64 a)
       | _ -> UnknownValue
    | [ AI_clt_un ], [a;b], [ty] when typeEquiv g ty g.bool_ty -> 
       match stripValue a, stripValue b with
       | ConstValue(Const.Char a1, _), ConstValue(Const.Char a2, _) -> mkBoolVal g (a1 < a2)
       | ConstValue(Const.Byte a1, _), ConstValue(Const.Byte a2, _) -> mkBoolVal g (a1 < a2)
       | ConstValue(Const.UInt16 a1, _), ConstValue(Const.UInt16 a2, _) -> mkBoolVal g (a1 < a2)
       | ConstValue(Const.UInt32 a1, _), ConstValue(Const.UInt32 a2, _) -> mkBoolVal g (a1 < a2)
       | ConstValue(Const.UInt64 a1, _), ConstValue(Const.UInt64 a2, _) -> mkBoolVal g (a1 < a2)
       | _ -> UnknownValue
    | [ AI_cgt ], [a;b], [ty] when typeEquiv g ty g.bool_ty -> 
       match stripValue a, stripValue b with
       | ConstValue(Const.SByte a1, _), ConstValue(Const.SByte a2, _) -> mkBoolVal g (a1 > a2)
       | ConstValue(Const.Int16 a1, _), ConstValue(Const.Int16 a2, _) -> mkBoolVal g (a1 > a2)
       | ConstValue(Const.Int32 a1, _), ConstValue(Const.Int32 a2, _) -> mkBoolVal g (a1 > a2)
       | ConstValue(Const.Int64 a1, _), ConstValue(Const.Int64 a2, _) -> mkBoolVal g (a1 > a2)
       | _ -> UnknownValue
    | [ AI_cgt_un ], [a;b], [ty] when typeEquiv g ty g.bool_ty -> 
       match stripValue a, stripValue b with
       | ConstValue(Const.Char a1, _), ConstValue(Const.Char a2, _) -> mkBoolVal g (a1 > a2)
       | ConstValue(Const.Byte a1, _), ConstValue(Const.Byte a2, _) -> mkBoolVal g (a1 > a2)
       | ConstValue(Const.UInt16 a1, _), ConstValue(Const.UInt16 a2, _) -> mkBoolVal g (a1 > a2)
       | ConstValue(Const.UInt32 a1, _), ConstValue(Const.UInt32 a2, _) -> mkBoolVal g (a1 > a2)
       | ConstValue(Const.UInt64 a1, _), ConstValue(Const.UInt64 a2, _) -> mkBoolVal g (a1 > a2)
       | _ -> UnknownValue
    | [ AI_shl ], [a;n], _ -> 
       match stripValue a, stripValue n with
       | ConstValue(Const.Int64 a, _), ConstValue(Const.Int32 n, _) when n >= 0 && n <= 63 -> (mkInt64Val g (a <<< n))
       | ConstValue(Const.Int32 a, _), ConstValue(Const.Int32 n, _) when n >= 0 && n <= 31 -> (mkInt32Val g (a <<< n))
       | ConstValue(Const.Int16 a, _), ConstValue(Const.Int32 n, _) when n >= 0 && n <= 15 -> (mkInt16Val g (a <<< n))
       | ConstValue(Const.SByte a, _), ConstValue(Const.Int32 n, _) when n >= 0 && n <= 7 -> (mkInt8Val g (a <<< n))
       | ConstValue(Const.UInt64 a, _), ConstValue(Const.Int32 n, _) when n >= 0 && n <= 63 -> (mkUInt64Val g (a <<< n))
       | ConstValue(Const.UInt32 a, _), ConstValue(Const.Int32 n, _) when n >= 0 && n <= 31 -> (mkUInt32Val g (a <<< n))
       | ConstValue(Const.UInt16 a, _), ConstValue(Const.Int32 n, _) when n >= 0 && n <= 15 -> (mkUInt16Val g (a <<< n))
       | ConstValue(Const.Byte a, _), ConstValue(Const.Int32 n, _) when n >= 0 && n <= 7 -> (mkUInt8Val g (a <<< n))
       | _ -> UnknownValue

    | [ AI_shr ], [a;n], _ -> 
       match stripValue a, stripValue n with
       | ConstValue(Const.SByte a, _), ConstValue(Const.Int32 n, _) when n >= 0 && n <= 7 -> (mkInt8Val g (a >>> n))
       | ConstValue(Const.Int16 a, _), ConstValue(Const.Int32 n, _) when n >= 0 && n <= 15 -> (mkInt16Val g (a >>> n))
       | ConstValue(Const.Int32 a, _), ConstValue(Const.Int32 n, _) when n >= 0 && n <= 31 -> (mkInt32Val g (a >>> n))
       | ConstValue(Const.Int64 a, _), ConstValue(Const.Int32 n, _) when n >= 0 && n <= 63 -> (mkInt64Val g (a >>> n))
       | _ -> UnknownValue
    | [ AI_shr_un ], [a;n], _ -> 
       match stripValue a, stripValue n with
       | ConstValue(Const.Byte a, _), ConstValue(Const.Int32 n, _) when n >= 0 && n <= 7 -> (mkUInt8Val g (a >>> n))
       | ConstValue(Const.UInt16 a, _), ConstValue(Const.Int32 n, _) when n >= 0 && n <= 15 -> (mkUInt16Val g (a >>> n))
       | ConstValue(Const.UInt32 a, _), ConstValue(Const.Int32 n, _) when n >= 0 && n <= 31 -> (mkUInt32Val g (a >>> n))
       | ConstValue(Const.UInt64 a, _), ConstValue(Const.Int32 n, _) when n >= 0 && n <= 63 -> (mkUInt64Val g (a >>> n))
       | _ -> UnknownValue
       
    // Retypings using IL asm "" are quite common in prim-types.fs
    // Sometimes these are only to get the primitives to pass the type checker.
    // Here we check for retypings from know values to known types.
    // We're conservative not to apply any actual data-changing conversions here.
    | [ ], [v], [ty] -> 
       match stripValue v with
       | ConstValue(Const.Bool a, _) ->
            if typeEquiv g ty g.bool_ty then v
            elif typeEquiv g ty g.sbyte_ty then mkInt8Val g (if a then 1y else 0y)
            elif typeEquiv g ty g.int16_ty then mkInt16Val g (if a then 1s else 0s)
            elif typeEquiv g ty g.int32_ty then mkInt32Val g (if a then 1 else 0)
            elif typeEquiv g ty g.byte_ty then mkUInt8Val g (if a then 1uy else 0uy)
            elif typeEquiv g ty g.uint16_ty then mkUInt16Val g (if a then 1us else 0us)
            elif typeEquiv g ty g.uint32_ty then mkUInt32Val g (if a then 1u else 0u)
            else UnknownValue
       | ConstValue(Const.SByte a, _) ->
            if typeEquiv g ty g.sbyte_ty then v
            elif typeEquiv g ty g.int16_ty then mkInt16Val g (Unchecked.int16 a)
            elif typeEquiv g ty g.int32_ty then mkInt32Val g (Unchecked.int32 a)
            else UnknownValue
       | ConstValue(Const.Byte a, _) ->
            if typeEquiv g ty g.byte_ty then v
            elif typeEquiv g ty g.uint16_ty then mkUInt16Val g (Unchecked.uint16 a)
            elif typeEquiv g ty g.uint32_ty then mkUInt32Val g (Unchecked.uint32 a)
            else UnknownValue
       | ConstValue(Const.Int16 a, _) ->
            if typeEquiv g ty g.int16_ty then v
            elif typeEquiv g ty g.int32_ty then mkInt32Val g (Unchecked.int32 a)
            else UnknownValue
       | ConstValue(Const.UInt16 a, _) ->
            if typeEquiv g ty g.uint16_ty then v
            elif typeEquiv g ty g.uint32_ty then mkUInt32Val g (Unchecked.uint32 a)
            else UnknownValue
       | ConstValue(Const.Int32 a, _) ->
            if typeEquiv g ty g.int32_ty then v
            elif typeEquiv g ty g.uint32_ty then mkUInt32Val g (Unchecked.uint32 a)
            else UnknownValue
       | ConstValue(Const.UInt32 a, _) ->
            if typeEquiv g ty g.uint32_ty then v
            elif typeEquiv g ty g.int32_ty then mkInt32Val g (Unchecked.int32 a)
            else UnknownValue
       | ConstValue(Const.Int64 a, _) ->
            if typeEquiv g ty g.int64_ty then v
            elif typeEquiv g ty g.uint64_ty then mkUInt64Val g (Unchecked.uint64 a)
            else UnknownValue
       | ConstValue(Const.UInt64 a, _) ->
            if typeEquiv g ty g.uint64_ty then v
            elif typeEquiv g ty g.int64_ty then mkInt64Val g (Unchecked.int64 a)
            else UnknownValue
       | _ -> UnknownValue
    | _ -> UnknownValue


//-------------------------------------------------------------------------
// Size constants and combinators
//------------------------------------------------------------------------- 

let [<Literal>] localVarSize = 1

let AddTotalSizes l = l |> List.sumBy (fun x -> x.TotalSize) 

let AddFunctionSizes l = l |> List.sumBy (fun x -> x.FunctionSize) 

/// list/array combinators - zipping (_, _) return type
let OrEffects l = List.exists (fun x -> x.HasEffect) l

let OrTailcalls l = List.exists (fun x -> x.MightMakeCriticalTailcall) l
        
let OptimizeList f l = l |> List.map f |> List.unzip 

let NoExprs : (Expr list * list<Summary<ExprValueInfo>>) = [], []

/// Common ways of building new value infos
let CombineValueInfos einfos res = 
      { TotalSize = AddTotalSizes einfos
        FunctionSize = AddFunctionSizes einfos
        HasEffect = OrEffects einfos 
        MightMakeCriticalTailcall = OrTailcalls einfos 
        Info = res }

let CombineValueInfosUnknown einfos = CombineValueInfos einfos UnknownValue

/// Hide information because of a signature
let AbstractLazyModulInfoByHiding isAssemblyBoundary mhi =

    // The freevars and FreeTyvars can indicate if the non-public (hidden) items have been used.
    // Under those checks, the further hidden* checks may be subsumed (meaning, not required anymore).

    let hiddenTycon, hiddenTyconRepr, hiddenVal, hiddenRecdField, hiddenUnionCase = 
        Zset.memberOf mhi.HiddenTycons, 
        Zset.memberOf mhi.HiddenTyconReprs, 
        Zset.memberOf mhi.HiddenVals, 
        Zset.memberOf mhi.HiddenRecdFields, 
        Zset.memberOf mhi.HiddenUnionCases

    let rec abstractExprInfo ivalue = 
        match ivalue with 
        // Check for escaping value. Revert to old info if possible
        | ValValue (vref2, detail) ->
            let detailR = abstractExprInfo detail 
            let v2 = vref2.Deref 
            let tyvars = freeInVal CollectAll v2 
            if  
                (isAssemblyBoundary && not (freeTyvarsAllPublic tyvars)) || 
                Zset.exists hiddenTycon tyvars.FreeTycons || 
                hiddenVal v2
            then detailR
            else ValValue (vref2, detailR)

        // Check for escape in lambda 
        | CurriedLambdaValue (_, _, _, expr, _) | ConstExprValue(_, expr) when            
            (let fvs = freeInExpr CollectAll expr
             (isAssemblyBoundary && not (freeVarsAllPublic fvs)) || 
             Zset.exists hiddenVal fvs.FreeLocals ||
             Zset.exists hiddenTycon fvs.FreeTyvars.FreeTycons ||
             Zset.exists hiddenTyconRepr fvs.FreeLocalTyconReprs ||
             Zset.exists hiddenRecdField fvs.FreeRecdFields ||
             Zset.exists hiddenUnionCase fvs.FreeUnionCases ) ->
                UnknownValue

        // Check for escape in constant 
        | ConstValue(_, ty) when 
            (let ftyvs = freeInType CollectAll ty
             (isAssemblyBoundary && not (freeTyvarsAllPublic ftyvs)) || 
             Zset.exists hiddenTycon ftyvs.FreeTycons) ->
                UnknownValue

        | TupleValue vinfos -> 
            TupleValue (Array.map abstractExprInfo vinfos)

        | RecdValue (tcref, vinfos) -> 
            if hiddenTyconRepr tcref.Deref || Array.exists (tcref.MakeNestedRecdFieldRef >> hiddenRecdField) tcref.AllFieldsArray
            then UnknownValue 
            else RecdValue (tcref, Array.map abstractExprInfo vinfos)

        | UnionCaseValue(ucref, vinfos) -> 
            let tcref = ucref.TyconRef
            if hiddenTyconRepr ucref.Tycon || tcref.UnionCasesArray |> Array.exists (tcref.MakeNestedUnionCaseRef >> hiddenUnionCase) 
            then UnknownValue 
            else UnionCaseValue (ucref, Array.map abstractExprInfo vinfos)

        | SizeValue(_vdepth, vinfo) ->
            MakeSizedValueInfo (abstractExprInfo vinfo)

        | UnknownValue  
        | ConstExprValue _   
        | CurriedLambdaValue _ 
        | ConstValue _ -> ivalue

    and abstractValInfo v = 
        { ValExprInfo=abstractExprInfo v.ValExprInfo 
          ValMakesNoCriticalTailcalls=v.ValMakesNoCriticalTailcalls }

    and abstractModulInfo ss =
         { ModuleOrNamespaceInfos = NameMap.map abstractLazyModulInfo ss.ModuleOrNamespaceInfos
           ValInfos = 
               ValInfos(ss.ValInfos.Entries 
                         |> Seq.filter (fun (vref, _) -> not (hiddenVal vref.Deref))
                         |> Seq.map (fun (vref, e) -> check (* "its implementation uses a binding hidden by a signature" m *) vref (abstractValInfo e) )) } 

    and abstractLazyModulInfo (ss: LazyModuleInfo) = 
          ss.Force() |> abstractModulInfo |> notlazy

    abstractLazyModulInfo

/// Hide all information except what we need for "must inline". We always save this optimization information
let AbstractOptimizationInfoToEssentials =

    let rec abstractModulInfo (ss: ModuleInfo) =
         { ModuleOrNamespaceInfos = NameMap.map (Lazy.force >> abstractModulInfo >> notlazy) ss.ModuleOrNamespaceInfos
           ValInfos = ss.ValInfos.Filter (fun (v, _) -> v.MustInline) }

    and abstractLazyModulInfo ss = ss |> Lazy.force |> abstractModulInfo |> notlazy
      
    abstractLazyModulInfo

/// Hide information because of a "let ... in ..." or "let rec ... in ... "
let AbstractExprInfoByVars (boundVars: Val list, boundTyVars) ivalue =
  // Module and member bindings can be skipped when checking abstraction, since abstraction of these values has already been done when 
  // we hit the end of the module and called AbstractLazyModulInfoByHiding. If we don't skip these then we end up quadratically retraversing  
  // the inferred optimization data, i.e. at each binding all the way up a sequences of 'lets' in a module. 
  let boundVars = boundVars |> List.filter (fun v -> not v.IsMemberOrModuleBinding)

  match boundVars, boundTyVars with 
  | [], [] -> ivalue
  | _ -> 

      let rec abstractExprInfo ivalue =
          match ivalue with 
          // Check for escaping value. Revert to old info if possible  
          | ValValue (VRefLocal v2, detail) when  
            (not (isNil boundVars) && List.exists (valEq v2) boundVars) || 
            (not (isNil boundTyVars) &&
             let ftyvs = freeInVal CollectTypars v2
             List.exists (Zset.memberOf ftyvs.FreeTypars) boundTyVars) -> 

             // hiding value when used in expression 
              abstractExprInfo detail

          | ValValue (v2, detail) -> 
              let detailR = abstractExprInfo detail
              ValValue (v2, detailR)
        
          // Check for escape in lambda 
          | CurriedLambdaValue (_, _, _, expr, _) | ConstExprValue(_, expr) when 
            (let fvs = freeInExpr (if isNil boundTyVars then CollectLocals else CollectTyparsAndLocals) expr
             (not (isNil boundVars) && List.exists (Zset.memberOf fvs.FreeLocals) boundVars) ||
             (not (isNil boundTyVars) && List.exists (Zset.memberOf fvs.FreeTyvars.FreeTypars) boundTyVars) ||
             (fvs.UsesMethodLocalConstructs )) ->
              
              // Trimming lambda
              UnknownValue

          // Check for escape in generic constant
          | ConstValue(_, ty) when 
            (not (isNil boundTyVars) && 
             (let ftyvs = freeInType CollectTypars ty
              List.exists (Zset.memberOf ftyvs.FreeTypars) boundTyVars)) ->
              UnknownValue

          // Otherwise check all sub-values 
          | TupleValue vinfos -> TupleValue (Array.map abstractExprInfo vinfos)
          | RecdValue (tcref, vinfos) -> RecdValue (tcref, Array.map abstractExprInfo vinfos)
          | UnionCaseValue (cspec, vinfos) -> UnionCaseValue(cspec, Array.map abstractExprInfo vinfos)
          | CurriedLambdaValue _ 
          | ConstValue _ 
          | ConstExprValue _ 
          | UnknownValue -> ivalue
          | SizeValue (_vdepth, vinfo) -> MakeSizedValueInfo (abstractExprInfo vinfo)

      and abstractValInfo v = 
          { ValExprInfo=abstractExprInfo v.ValExprInfo 
            ValMakesNoCriticalTailcalls=v.ValMakesNoCriticalTailcalls }

      and abstractModulInfo ss =
         { ModuleOrNamespaceInfos = ss.ModuleOrNamespaceInfos |> NameMap.map (Lazy.force >> abstractModulInfo >> notlazy) 
           ValInfos = ss.ValInfos.Map (fun (vref, e) -> 
               check vref (abstractValInfo e) ) }

      abstractExprInfo ivalue

/// Remap optimization information, e.g. to use public stable references so we can pickle it
/// to disk.
let RemapOptimizationInfo g tmenv =

    let rec remapExprInfo ivalue = 
        match ivalue with 
        | ValValue (v, detail) -> ValValue (remapValRef tmenv v, remapExprInfo detail)
        | TupleValue vinfos -> TupleValue (Array.map remapExprInfo vinfos)
        | RecdValue (tcref, vinfos) -> RecdValue (remapTyconRef tmenv.tyconRefRemap tcref, Array.map remapExprInfo vinfos)
        | UnionCaseValue(cspec, vinfos) -> UnionCaseValue (remapUnionCaseRef tmenv.tyconRefRemap cspec, Array.map remapExprInfo vinfos)
        | SizeValue(_vdepth, vinfo) -> MakeSizedValueInfo (remapExprInfo vinfo)
        | UnknownValue -> UnknownValue
        | CurriedLambdaValue (uniq, arity, sz, expr, ty) -> CurriedLambdaValue (uniq, arity, sz, remapExpr g CloneAll tmenv expr, remapPossibleForallTy g tmenv ty)  
        | ConstValue (c, ty) -> ConstValue (c, remapPossibleForallTy g tmenv ty)
        | ConstExprValue (sz, expr) -> ConstExprValue (sz, remapExpr g CloneAll tmenv expr)

    let remapValInfo v = 
         { ValExprInfo=remapExprInfo v.ValExprInfo
           ValMakesNoCriticalTailcalls=v.ValMakesNoCriticalTailcalls }

    let rec remapModulInfo ss =
         { ModuleOrNamespaceInfos = ss.ModuleOrNamespaceInfos |> NameMap.map remapLazyModulInfo
           ValInfos = 
              ss.ValInfos.Map (fun (vref, vinfo) -> 
                   let vrefR = remapValRef tmenv vref 
                   let vinfo = remapValInfo vinfo
                   // Propagate any inferred ValMakesNoCriticalTailcalls flag from implementation to signature information
                   if vinfo.ValMakesNoCriticalTailcalls then vrefR.Deref.SetMakesNoCriticalTailcalls() 
                   (vrefR, vinfo)) } 

    and remapLazyModulInfo ss =
         ss |> Lazy.force |> remapModulInfo |> notlazy
           
    remapLazyModulInfo

/// Hide information when a value is no longer visible
let AbstractAndRemapModulInfo msg g m (repackage, hidden) info =
    let mrpi = mkRepackageRemapping repackage
#if DEBUG
    if verboseOptimizationInfo then dprintf "%s - %a - Optimization data prior to trim: \n%s\n" msg outputRange m (showL (Display.squashTo 192 (moduleInfoL g info)))
#else
    ignore (msg, m)
#endif
    let info = info |> AbstractLazyModulInfoByHiding false hidden
#if DEBUG
    if verboseOptimizationInfo then dprintf "%s - %a - Optimization data after trim:\n%s\n" msg outputRange m (showL (Display.squashTo 192 (moduleInfoL g info)))
#endif
    let info = info |> RemapOptimizationInfo g mrpi
#if DEBUG
    if verboseOptimizationInfo then dprintf "%s - %a - Optimization data after remap:\n%s\n" msg outputRange m (showL (Display.squashTo 192 (moduleInfoL g info)))
#endif
    info

//-------------------------------------------------------------------------
// Misc helpers
//------------------------------------------------------------------------- 

/// Mark some variables (the ones we introduce via abstractBigTargets) as don't-eliminate 
let [<Literal>] suffixForVariablesThatMayNotBeEliminated = "$cont"

/// Indicates a ValRef generated to facilitate tuple eliminations
let [<Literal>] suffixForTupleElementAssignmentTarget = "$tupleElem"

/// Type applications of F# "type functions" may cause side effects, e.g. 
/// let x<'a> = printfn "hello"; typeof<'a> 
/// In this case do not treat them as constants. 
let IsTyFuncValRefExpr = function 
    | Expr.Val (fv, _, _) -> fv.IsTypeFunction
    | _ -> false

/// Type applications of existing functions are always simple constants, with the exception of F# 'type functions' 
let rec IsSmallConstExpr x =
    match x with
    | Expr.Op (TOp.LValueOp (LAddrOf _, _), [], [], _) -> true // &x is always a constant
    | Expr.Val (v, _, _m) -> not v.IsMutable
    | Expr.App (fe, _, _tyargs, args, _) -> isNil args && not (IsTyFuncValRefExpr fe) && IsSmallConstExpr fe
    | _ -> false

let ValueOfExpr expr = 
    if IsSmallConstExpr expr then 
      ConstExprValue(0, expr)
    else UnknownValue

let IsMutableStructuralBindingForTupleElement (vref: ValRef) =
    vref.IsCompilerGenerated &&
    vref.LogicalName.EndsWith suffixForTupleElementAssignmentTarget

let IsMutableForOutArg (vref: ValRef) =
    vref.IsCompilerGenerated &&
    vref.LogicalName.StartsWith(PrettyNaming.outArgCompilerGeneratedName)

let IsKnownOnlyMutableBeforeUse (vref: ValRef) =
    IsMutableStructuralBindingForTupleElement vref || 
    IsMutableForOutArg vref

//-------------------------------------------------------------------------
// Dead binding elimination 
//------------------------------------------------------------------------- 
 
// Allow discard of "let v = *byref" if "v" is unused anywhere. The read effect
// can be discarded because it is always assumed that reading byref pointers (without using 
// the value of the read) doesn't raise exceptions or cause other "interesting" side effects.
//
// This allows discarding the implicit deref when matching on struct unions, e.g.
//    
//    [<Struct; NoComparison; NoEquality>]
//    type SingleRec =  
//        | SingleUnion of int
//        member x.Next = let (SingleUnion i) = x in SingleUnion (i+1)
//
// See https://github.com/Microsoft/visualfsharp/issues/5136
//
//
// note: allocating an object with observable identity (i.e. a name) 
// or reading from a mutable field counts as an 'effect', i.e.
// this context 'effect' has it's usual meaning in the effect analysis literature of 
//   read-from-mutable 
//   write-to-mutable 
//   name-generation
//   arbitrary-side-effect (e.g. 'non-termination' or 'fire the missiles')

let IsDiscardableEffectExpr expr = 
    match expr with 
    | Expr.Op (TOp.LValueOp (LByrefGet _, _), [], [], _) -> true 
    | _ -> false

/// Checks is a value binding is non-discardable
let ValueIsUsedOrHasEffect cenv fvs (b: Binding, binfo) =
    let v = b.Var
    not (cenv.settings.EliminateUnusedBindings()) ||
    Option.isSome v.MemberInfo ||
    (binfo.HasEffect && not (IsDiscardableEffectExpr b.Expr)) ||
    v.IsFixed ||
    Zset.contains v (fvs())

let rec SplitValuesByIsUsedOrHasEffect cenv fvs x = 
    x |> List.filter (ValueIsUsedOrHasEffect cenv fvs) |> List.unzip

let IlAssemblyCodeInstrHasEffect i = 
    match i with 
    | ( AI_nop | AI_ldc _ | AI_add | AI_sub | AI_mul | AI_xor | AI_and | AI_or 
               | AI_ceq | AI_cgt | AI_cgt_un | AI_clt | AI_clt_un | AI_conv _ | AI_shl 
               | AI_shr | AI_shr_un | AI_neg | AI_not | AI_ldnull )
    | I_ldstr _ | I_ldtoken _ -> false
    | _ -> true
  
let IlAssemblyCodeHasEffect instrs = List.exists IlAssemblyCodeInstrHasEffect instrs

let rec ExprHasEffect g expr = 
    match expr with 
    | Expr.Val (vref, _, _) -> vref.IsTypeFunction || (vref.IsMutable)
    | Expr.Quote _ 
    | Expr.Lambda _
    | Expr.TyLambda _ 
    | Expr.Const _ -> false
    /// type applications do not have effects, with the exception of type functions
    | Expr.App (f0, _, _, [], _) -> (IsTyFuncValRefExpr f0) || ExprHasEffect g f0
    | Expr.Op (op, _, args, m) -> ExprsHaveEffect g args || OpHasEffect g m op
    | Expr.LetRec (binds, body, _, _) -> BindingsHaveEffect g binds || ExprHasEffect g body
    | Expr.Let (bind, body, _, _) -> BindingHasEffect g bind || ExprHasEffect g body
    // REVIEW: could add Expr.Obj on an interface type - these are similar to records of lambda expressions 
    | _ -> true

and ExprsHaveEffect g exprs = List.exists (ExprHasEffect g) exprs

and BindingsHaveEffect g binds = List.exists (BindingHasEffect g) binds

and BindingHasEffect g bind = bind.Expr |> ExprHasEffect g

and OpHasEffect g m op = 
    match op with 
    | TOp.Tuple _ -> false
    | TOp.AnonRecd _ -> false
    | TOp.Recd (ctor, tcref) -> 
        match ctor with 
        | RecdExprIsObjInit -> true
        | RecdExpr -> not (isRecdOrStructTyconRefReadOnly g m tcref)
    | TOp.UnionCase ucref -> isRecdOrUnionOrStructTyconRefDefinitelyMutable ucref.TyconRef
    | TOp.ExnConstr ecref -> isExnDefinitelyMutable ecref
    | TOp.Bytes _ | TOp.UInt16s _ | TOp.Array -> true // mutable
    | TOp.UnionCaseTagGet _ -> false
    | TOp.UnionCaseProof _ -> false
    | TOp.UnionCaseFieldGet (ucref, n) -> isUnionCaseFieldMutable g ucref n 
    | TOp.ILAsm (instrs, _) -> IlAssemblyCodeHasEffect instrs
    | TOp.TupleFieldGet (_) -> false
    | TOp.ExnFieldGet (ecref, n) -> isExnFieldMutable ecref n 
    | TOp.RefAddrGet _ -> false
    | TOp.AnonRecdGet _ -> true // conservative
    | TOp.ValFieldGet rfref -> rfref.RecdField.IsMutable || (TryFindTyconRefBoolAttribute g Range.range0 g.attrib_AllowNullLiteralAttribute rfref.TyconRef = Some true)
    | TOp.ValFieldGetAddr (rfref, _readonly) -> rfref.RecdField.IsMutable
    | TOp.UnionCaseFieldGetAddr _ -> false // union case fields are immutable
    | TOp.LValueOp (LAddrOf _, _) -> false // addresses of values are always constants
    | TOp.UnionCaseFieldSet _
    | TOp.ExnFieldSet _
    | TOp.Coerce
    | TOp.Reraise
    | TOp.For _ 
    | TOp.While _
    | TOp.TryWith _ (* conservative *)
    | TOp.TryFinally _ (* conservative *)
    | TOp.TraitCall _
    | TOp.Goto _
    | TOp.Label _
    | TOp.Return
    | TOp.ILCall _ (* conservative *)
    | TOp.LValueOp _ (* conservative *)
    | TOp.ValFieldSet _ -> true


let TryEliminateBinding cenv _env (TBind(vspec1, e1, spBind)) e2 _m =
    // don't eliminate bindings if we're not optimizing AND the binding is not a compiler generated variable
    if not (cenv.optimizing && cenv.settings.EliminateImmediatelyConsumedLocals()) && 
       not vspec1.IsCompilerGenerated then 
       None 
    elif vspec1.IsFixed then None 
    else
        // Peephole on immediate consumption of single bindings, e.g. "let x = e in x" --> "e" 
        // REVIEW: enhance this by general elimination of bindings to 
        // non-side-effecting expressions that are used only once. 
        // But note the cases below cover some instances of side-effecting expressions as well.... 
        let IsUniqueUse vspec2 args = 
              valEq vspec1 vspec2  
           && (not (vspec2.LogicalName.Contains suffixForVariablesThatMayNotBeEliminated))
           // REVIEW: this looks slow. Look only for one variable instead 
           && (let fvs = accFreeInExprs CollectLocals args emptyFreeVars
               not (Zset.contains vspec1 fvs.FreeLocals))

        // Immediate consumption of value as 2nd or subsequent argument to a construction or projection operation 
        let rec GetImmediateUseContext rargsl argsr = 
              match argsr with 
              | (Expr.Val (VRefLocal vspec2, _, _)) :: argsr2
                 when valEq vspec1 vspec2 && IsUniqueUse vspec2 (List.rev rargsl@argsr2) -> Some(List.rev rargsl, argsr2)
              | argsrh :: argsrt when not (ExprHasEffect cenv.g argsrh) -> GetImmediateUseContext (argsrh :: rargsl) argsrt 
              | _ -> None

        match stripExpr e2 with 

         // Immediate consumption of value as itself 'let x = e in x'
         | Expr.Val (VRefLocal vspec2, _, _) 
             when IsUniqueUse vspec2 [] -> 
               Some e1

         // Immediate consumption of value by a pattern match 'let x = e in match x with ...'
         | Expr.Match (spMatch, _exprm, TDSwitch(Expr.Val (VRefLocal vspec2, _, _), cases, dflt, _), targets, m, ty2)
             when (valEq vspec1 vspec2 && 
                   let fvs = accFreeInTargets CollectLocals targets (accFreeInSwitchCases CollectLocals cases dflt emptyFreeVars)
                   not (Zset.contains vspec1 fvs.FreeLocals)) -> 

              let spMatch = spBind.Combine spMatch
              Some (Expr.Match (spMatch, e1.Range, TDSwitch(e1, cases, dflt, m), targets, m, ty2))
               
         // Immediate consumption of value as a function 'let f = e in f ...' and 'let x = e in f ... x ...'
         // Note functions are evaluated before args 
         // Note: do not include functions with a single arg of unit type, introduced by abstractBigTargets 
         | Expr.App (f, f0ty, tyargs, args, m) 
               when not (vspec1.LogicalName.Contains suffixForVariablesThatMayNotBeEliminated) ->
             match GetImmediateUseContext [] (f :: args) with 
             | Some([], rargs) -> Some (MakeApplicationAndBetaReduce cenv.g (e1, f0ty, [tyargs], rargs, m))
             | Some(f :: largs, rargs) -> Some (MakeApplicationAndBetaReduce cenv.g (f, f0ty, [tyargs], largs @ (e1 :: rargs), m))
             | None -> None

         // Bug 6311: a special case of nested elimination of locals (which really should be handled more generally)
         // 'let x = e in op[op[x;arg2];arg3]' --> op[op[e;arg2];arg3]
         // 'let x = e in op[op[arg1;x];arg3]' --> op[op[arg1;e];arg3] when arg1 has no side effects etc.
         // 'let x = e in op[op[arg1;arg2];x]' --> op[op[arg1;arg2];e] when arg1, arg2 have no side effects etc.
         | Expr.Op (c1, tyargs1, [Expr.Op (c2, tyargs2, [arg1;arg2], m2);arg3], m1) -> 
             match GetImmediateUseContext [] [arg1;arg2;arg3] with 
             | Some([], [arg2;arg3]) -> Some (Expr.Op (c1, tyargs1, [Expr.Op (c2, tyargs2, [e1;arg2], m2);arg3], m1))
             | Some([arg1], [arg3]) -> Some (Expr.Op (c1, tyargs1, [Expr.Op (c2, tyargs2, [arg1;e1], m2);arg3], m1))
             | Some([arg1;arg2], []) -> Some (Expr.Op (c1, tyargs1, [Expr.Op (c2, tyargs2, [arg1;arg2], m2);e1], m1))
             | Some _ -> error(InternalError("unexpected return pattern from GetImmediateUseContext", m1))
             | None -> None

         // Immediate consumption of value as first non-effectful argument to a construction or projection operation 
         // 'let x = e in op[x;....]'
         | Expr.Op (c, tyargs, args, m) -> 
             match GetImmediateUseContext [] args with 
             | Some(largs, rargs) -> Some (Expr.Op (c, tyargs, (largs @ (e1 :: rargs)), m))
             | None -> None

         | _ ->  
            None

let TryEliminateLet cenv env bind e2 m = 
    match TryEliminateBinding cenv env bind e2 m with 
    | Some e2R -> e2R, -localVarSize (* eliminated a let, hence reduce size estimate *)
    | None -> mkLetBind m bind e2, 0

/// Detect the application of a value to an arbitrary number of arguments
let rec (|KnownValApp|_|) expr = 
    match stripExpr expr with
    | Expr.Val (vref, _, _) -> Some(vref, [], [])
    | Expr.App (KnownValApp(vref, typeArgs1, otherArgs1), _, typeArgs2, otherArgs2, _) -> Some(vref, typeArgs1@typeArgs2, otherArgs1@otherArgs2)
    | _ -> None

/// Matches boolean decision tree:
/// check single case with bool const.
let (|TDBoolSwitch|_|) dtree =
    match dtree with
    | TDSwitch( expr, [TCase (DecisionTreeTest.Const(Const.Bool testBool), caseTree )], Some defaultTree, range) ->
        Some (expr, testBool, caseTree, defaultTree, range)
    | _ -> 
        None

/// Check target that have a constant bool value
let (|ConstantBoolTarget|_|) target =
    match target with
    | TTarget([], Expr.Const (Const.Bool b,_,_),_) -> Some b
    | _ -> None

/// Is this a tree, where each decision is a two-way switch (to prevent later duplication of trees), and each branch returns or true/false,
/// apart from one branch which defers to another expression
let rec CountBoolLogicTree ((targets: DecisionTreeTarget[], costOuterCaseTree, costOuterDefaultTree, testBool) as data) tree =
    match tree with 
    | TDSwitch (_expr, [case], Some defaultTree, _range) -> 
        let tc1,ec1 = CountBoolLogicTree data case.CaseTree 
        let tc2, ec2 = CountBoolLogicTree data defaultTree 
        tc1 + tc2, ec1 + ec2
    | TDSuccess([], idx) -> 
        match targets.[idx] with
        | ConstantBoolTarget result -> (if result = testBool then costOuterCaseTree else costOuterDefaultTree), 0
        | TTarget([], _exp, _) -> costOuterCaseTree + costOuterDefaultTree, 10
        | _ -> 100, 100 
    | _ -> 100, 100

/// Rewrite a decision tree for which CountBoolLogicTree returned a low number (see below). Produce a new decision
/// tree where at each ConstantBoolSuccessTree tip we replace with either outerCaseTree or outerDefaultTree
/// depending on whether the target result was true/false
let rec RewriteBoolLogicTree ((targets: DecisionTreeTarget[], outerCaseTree, outerDefaultTree, testBool) as data) tree =
    match tree with 
    | TDSwitch (expr, cases, defaultTree, range) -> 
        let cases2 = cases |> List.map (RewriteBoolLogicCase data)
        let defaultTree2 = defaultTree |> Option.map (RewriteBoolLogicTree data)
        TDSwitch (expr, cases2, defaultTree2, range)
    | TDSuccess([], idx) -> 
        match targets.[idx] with 
        | ConstantBoolTarget result -> if result = testBool then outerCaseTree else outerDefaultTree
        | TTarget([], exp, _) -> mkBoolSwitch exp.Range exp (if testBool then outerCaseTree else outerDefaultTree) (if testBool then outerDefaultTree else outerCaseTree)
        | _ -> failwith "CountBoolLogicTree should exclude this case"
    | _ -> failwith "CountBoolLogicTree should exclude this case"

and RewriteBoolLogicCase data (TCase(test, tree)) =
    TCase(test, RewriteBoolLogicTree data tree)

/// Repeatedly combine switch-over-match decision trees, see https://github.com/Microsoft/visualfsharp/issues/635.
/// The outer decision tree is doing a switch over a boolean result, the inner match is producing only
/// constant boolean results in its targets.  
let rec CombineBoolLogic expr = 

    // try to find nested boolean switch
    match expr with
    | Expr.Match (outerSP, outerMatchRange, 
                  TDBoolSwitch(Expr.Match (_innerSP, _innerMatchRange, innerTree, innerTargets, _innerDefaultRange, _innerMatchTy),
                               outerTestBool, outerCaseTree, outerDefaultTree, _outerSwitchRange ), 
                  outerTargets, outerDefaultRange, outerMatchTy) ->
       
        let costOuterCaseTree = match outerCaseTree with TDSuccess _ -> 0 | _ -> 1
        let costOuterDefaultTree = match outerDefaultTree with TDSuccess _ -> 0 | _ -> 1
        let tc, ec = CountBoolLogicTree (innerTargets, costOuterCaseTree, costOuterDefaultTree, outerTestBool) innerTree
        // At most one expression, no overall duplication of TSwitch nodes
        if tc <= costOuterCaseTree + costOuterDefaultTree && ec <= 10 then 
            let newExpr = 
                Expr.Match (outerSP, outerMatchRange, 
                           RewriteBoolLogicTree (innerTargets, outerCaseTree, outerDefaultTree, outerTestBool) innerTree,
                           outerTargets, outerDefaultRange, outerMatchTy)

            CombineBoolLogic newExpr
        else
            expr
    | _ -> 
        expr

//-------------------------------------------------------------------------
// ExpandStructuralBinding
//
// Expand bindings to tuple expressions by factoring sub-expression out as prior bindings.
// Similarly for other structural constructions, like records...
// If the item is only projected from then the construction (allocation) can be eliminated.
// This transform encourages that by allowing projections to be simplified.
//
// Apply the same to 'Some(x)' constructions
//------------------------------------------------------------------------- 

let CanExpandStructuralBinding (v: Val) =
    not v.IsCompiledAsTopLevel &&
    not v.IsMember &&
    not v.IsTypeFunction &&
    not v.IsMutable

let ExprIsValue = function Expr.Val _ -> true | _ -> false

let MakeStructuralBindingTemp (v: Val) i (arg: Expr) argTy =
    let name = v.LogicalName + "_" + string i
    let v, ve = mkCompGenLocal arg.Range name argTy
    ve, mkCompGenBind v arg

let MakeMutableStructuralBindingForTupleElement (v: Val) i (arg: Expr) argTy =
    let name = sprintf "%s_%d%s" v.LogicalName i suffixForTupleElementAssignmentTarget
    let v, ve = mkMutableCompGenLocal arg.Range name argTy
    ve, mkCompGenBind v arg

let ExpandStructuralBindingRaw cenv expr =
    assert cenv.settings.ExpandStructuralValues()
    match expr with
    | Expr.Let (TBind(v, rhs, tgtSeqPtOpt), body, m, _) 
        when (isRefTupleExpr rhs &&
              CanExpandStructuralBinding v) ->
          let args = tryDestRefTupleExpr rhs
          if List.forall ExprIsValue args then
              expr (* avoid re-expanding when recursion hits original binding *)
          else
              let argTys = destRefTupleTy cenv.g v.Type
              let ves, binds = List.mapi2 (MakeStructuralBindingTemp v) args argTys |> List.unzip
              let tuple = mkRefTupled cenv.g m ves argTys
              mkLetsBind m binds (mkLet tgtSeqPtOpt m v tuple body)
    | expr -> expr

// Moves outer tuple binding inside near the tupled expression:
//   let t = (let a0=v0 in let a1=v1 in ... in let an=vn in e0, e1, ..., em) in body
// becomes
//   let a0=v0 in let a1=v1 in ... in let an=vn in (let t = e0, e1, ..., em in body)
//
// This way ExpandStructuralBinding can replace expressions in constants, t is directly bound
// to a tuple expression so that other optimizations such as OptimizeTupleFieldGet work, 
// and the tuple allocation can be eliminated.
// Most importantly, this successfully eliminates tuple allocations for implicitly returned
// formal arguments in method calls.
let rec RearrangeTupleBindings expr fin =
    match expr with
    | Expr.Let (bind, body, m, _) ->
        match RearrangeTupleBindings body fin with
        | Some b -> Some (mkLetBind m bind b)
        | None -> None
    | Expr.Op (TOp.Tuple tupInfo, _, _, _) when not (evalTupInfoIsStruct tupInfo) -> Some (fin expr)
    | Expr.Sequential (e1, e2, kind, sp, m) ->
        match RearrangeTupleBindings e2 fin with
        | Some b -> Some (Expr.Sequential (e1, b, kind, sp, m))
        | None -> None
    | _ -> None

// Attempts to rewrite tuple bindings containing ifs/matches by introducing a mutable local for each tuple element.
// These are assigned to exactly once from each branch in order to eliminate tuple allocations. The tuple binding
// is also rearranged such that OptimizeTupleFieldGet may kick in (see RearrangeTupleBindings comment above).
// First class use of a tuple at the end of any branch prevents this rewrite.
//
// Roughly speaking, the following expression:
//
//    let a, b =
//        if cond () then
//            1, 2
//        elif cond2 () then
//            3, 4
//        else
//            5, 6
//    in ...
//
// becomes
//
//    let mutable a = Unchecked.defaultof<_>
//    let mutable b = Unchecked.defaultof<_>
//
//    if cond () then
//        a <- 1
//        b <- 2
//    elif cond2 () then
//        a <- 3
//        b <- 4
//    else
//        a <- 5
//        b <- 6
//    in ...
let TryRewriteBranchingTupleBinding g (v: Val) rhs tgtSeqPtOpt body m =
    let rec dive g m (requisites: Lazy<_>) expr =
        match expr with
        | Expr.Match (sp, inputRange, decision, targets, fullRange, ty) ->
            // Recurse down every if/match branch
            let rewrittenTargets = targets |> Array.choose (fun (TTarget (vals, targetExpr, sp)) ->
                match dive g m requisites targetExpr with
                | Some rewritten -> TTarget (vals, rewritten, sp) |> Some
                | _ -> None)

            // If not all branches can be rewritten, keep the original expression as it is
            if rewrittenTargets.Length <> targets.Length then
                None
            else
                Expr.Match (sp, inputRange, decision, rewrittenTargets, fullRange, ty) |> Some
        | Expr.Op (TOp.Tuple tupInfo, _, tupleElements, m) when not (evalTupInfoIsStruct tupInfo) ->
            // Replace tuple allocation with mutations of locals
            let _, _, _, vrefs = requisites.Value
            List.map2 (mkValSet m) vrefs tupleElements
            |> mkSequentials DebugPointAtSequential.StmtOnly g m
            |> Some
        | Expr.Sequential (e1, e2, kind, sp, m) ->
            match dive g m requisites e2 with
            | Some rewritten -> Expr.Sequential (e1, rewritten, kind, sp, m) |> Some
            | _ -> None
        | Expr.Let (bind, body, m, _) ->
            match dive g m requisites body with
            | Some rewritten -> mkLetBind m bind rewritten |> Some
            | _ -> None
        | _ -> None

    let requisites = lazy (
        let argTys = destRefTupleTy g v.Type
        let inits = argTys |> List.map (mkNull m)
        let ves, binds = List.mapi2 (MakeMutableStructuralBindingForTupleElement v) inits argTys |> List.unzip
        let vrefs = binds |> List.map (fun (TBind (v, _, _)) -> mkLocalValRef v)
        argTys, ves, binds, vrefs)

    match dive g m requisites rhs with
    | Some rewrittenRhs ->
        let argTys, ves, binds, _ = requisites.Value
        let rhsAndTupleBinding = mkCompGenSequential m rewrittenRhs (mkLet tgtSeqPtOpt m v (mkRefTupled g m ves argTys) body)
        mkLetsBind m binds rhsAndTupleBinding |> Some
    | _ -> None

let ExpandStructuralBinding cenv expr =
    assert cenv.settings.ExpandStructuralValues()
    match expr with
    | Expr.Let (TBind(v, rhs, tgtSeqPtOpt), body, m, _)
        when (isRefTupleTy cenv.g v.Type &&
              not (isRefTupleExpr rhs) &&
              CanExpandStructuralBinding v) ->
        match RearrangeTupleBindings rhs (fun top -> mkLet tgtSeqPtOpt m v top body) with
        | Some e -> ExpandStructuralBindingRaw cenv e
        | None ->
            // RearrangeTupleBindings could have failed because the rhs branches
            TryRewriteBranchingTupleBinding cenv.g v rhs tgtSeqPtOpt body m |> Option.defaultValue expr

    // Expand 'let v = Some arg in ...' to 'let tmp = arg in let v = Some tp in ...'
    // Used to give names to values of optional arguments prior as we inline.
    | Expr.Let (TBind(v, Expr.Op(TOp.UnionCase uc, _, [arg], _), tgtSeqPtOpt), body, m, _)
        when isOptionTy cenv.g v.Type && 
             not (ExprIsValue arg) && 
             cenv.g.unionCaseRefEq uc (mkSomeCase cenv.g) &&
             CanExpandStructuralBinding v ->
            let argTy = destOptionTy cenv.g v.Type 
            let ve, bind = MakeStructuralBindingTemp v 0 arg argTy
            let newExpr = mkSome cenv.g argTy ve m
            mkLetBind m bind (mkLet tgtSeqPtOpt m v newExpr body)
    | e ->
        ExpandStructuralBindingRaw cenv e

/// Detect a query { ... }
let (|QueryRun|_|) g expr = 
    match expr with
    | Expr.App (Expr.Val (vref, _, _), _, _, [_builder; arg], _) when valRefEq g vref g.query_run_value_vref ->  
        Some (arg, None)
    | Expr.App (Expr.Val (vref, _, _), _, [ elemTy ], [_builder; arg], _) when valRefEq g vref g.query_run_enumerable_vref ->  
        Some (arg, Some elemTy)
    | _ -> 
        None

let (|MaybeRefTupled|) e = tryDestRefTupleExpr e 

let (|AnyInstanceMethodApp|_|) e = 
    match e with 
    | Expr.App (Expr.Val (vref, _, _), _, tyargs, [obj; MaybeRefTupled args], _) -> Some (vref, tyargs, obj, args)
    | _ -> None

let (|InstanceMethodApp|_|) g (expectedValRef: ValRef) e = 
    match e with 
    | AnyInstanceMethodApp (vref, tyargs, obj, args) when valRefEq g vref expectedValRef -> Some (tyargs, obj, args)
    | _ -> None

let (|QuerySourceEnumerable|_|) g = function
    | InstanceMethodApp g g.query_source_vref ([resTy], _builder, [res]) -> Some (resTy, res)
    | _ -> None

let (|QueryFor|_|) g = function
    | InstanceMethodApp g g.query_for_vref ([srcTy;qTy;resTy;_qInnerTy], _builder, [src;selector]) -> Some (qTy, srcTy, resTy, src, selector)
    | _ -> None

let (|QueryYield|_|) g = function
    | InstanceMethodApp g g.query_yield_vref ([resTy;qTy], _builder, [res]) -> Some (qTy, resTy, res)
    | _ -> None

let (|QueryYieldFrom|_|) g = function
    | InstanceMethodApp g g.query_yield_from_vref ([resTy;qTy], _builder, [res]) -> Some (qTy, resTy, res)
    | _ -> None

let (|QuerySelect|_|) g = function
    | InstanceMethodApp g g.query_select_vref ([srcTy;qTy;resTy], _builder, [src;selector]) -> Some (qTy, srcTy, resTy, src, selector)
    | _ -> None

let (|QueryZero|_|) g = function
    | InstanceMethodApp g g.query_zero_vref ([resTy;qTy], _builder, _) -> Some (qTy, resTy)
    | _ -> None

/// Look for a possible tuple and transform
let (|AnyRefTupleTrans|) e = 
    match e with 
    | Expr.Op (TOp.Tuple tupInfo, tys, es, m) when not (evalTupInfoIsStruct tupInfo) -> (es, (fun es -> Expr.Op (TOp.Tuple tupInfo, tys, es, m)))  
    | _ -> [e], (function [e] -> e | _ -> assert false; failwith "unreachable")

/// Look for any QueryBuilder.* operation and transform
let (|AnyQueryBuilderOpTrans|_|) g = function
    | Expr.App ((Expr.Val (vref, _, _) as v), vty, tyargs, [builder; AnyRefTupleTrans( (src :: rest), replaceArgs) ], m) when 
          (match vref.ApparentEnclosingEntity with Parent tcref -> tyconRefEq g tcref g.query_builder_tcref | ParentNone -> false) ->  
         Some (src, (fun newSource -> Expr.App (v, vty, tyargs, [builder; replaceArgs(newSource :: rest)], m)))
    | _ -> None

/// If this returns "Some" then the source is not IQueryable.
//  <qexprInner> := 
//     | query.Select(<qexprInner>, <other-arguments>) --> Seq.map(qexprInner', ...)
//     | query.For(<qexprInner>, <other-arguments>) --> IQueryable if qexprInner is IQueryable, otherwise Seq.collect(qexprInner', ...)
//     | query.Yield <expr> --> not IQueryable
//     | query.YieldFrom <qexpr> --> not IQueryable
//     | query.Op(<qexprInner>, <other-arguments>) --> IQueryable if qexprInner is IQueryable, otherwise query.Op(qexprInner', <other-arguments>)   
//     | <qexprInner> :> seq<_> --> IQueryable if qexprInner is IQueryable
//
//  <qexprOuter> := 
//     | query.Select(<qexprInner>, <other-arguments>) --> IQueryable if qexprInner is IQueryable, otherwise seq { qexprInner' } 
//     | query.For(<qexprInner>, <other-arguments>) --> IQueryable if qexprInner is IQueryable, otherwise seq { qexprInner' } 
//     | query.Yield <expr> --> not IQueryable, seq { <expr> } 
//     | query.YieldFrom <expr> --> not IQueryable, seq { yield! <expr> } 
//     | query.Op(<qexprOuter>, <other-arguments>) --> IQueryable if qexprOuter is IQueryable, otherwise query.Op(qexprOuter', <other-arguments>)   
let rec tryRewriteToSeqCombinators g (e: Expr) = 
    let m = e.Range
    match e with 
    //  query.Yield --> Seq.singleton
    | QueryYield g (_, resultElemTy, vExpr) -> Some (mkCallSeqSingleton g m resultElemTy vExpr)

    //  query.YieldFrom (query.Source s) --> s
    | QueryYieldFrom g (_, _, QuerySourceEnumerable g (_, resExpr)) -> Some resExpr

    //  query.Select --> Seq.map
    | QuerySelect g (_qTy, sourceElemTy, resultElemTy, source, resultSelector) -> 
   
        match tryRewriteToSeqCombinators g source with 
        | Some newSource -> Some (mkCallSeqMap g m sourceElemTy resultElemTy resultSelector newSource)
        | None -> None

    //  query.Zero -> Seq.empty
    | QueryZero g (_qTy, sourceElemTy) -> 
        Some (mkCallSeqEmpty g m sourceElemTy)

    //  query.For --> Seq.collect
    | QueryFor g (_qTy, sourceElemTy, resultElemTy, QuerySourceEnumerable g (_, source), Expr.Lambda (_, _, _, [resultSelectorVar], resultSelector, mLambda, _)) -> 
        match tryRewriteToSeqCombinators g resultSelector with
        | Some newResultSelector ->
            Some (mkCallSeqCollect g m sourceElemTy resultElemTy (mkLambda mLambda resultSelectorVar (newResultSelector, tyOfExpr g newResultSelector)) source)
        | _ -> None


    //  let --> let
    | Expr.Let (bind, bodyExpr, m, _) -> 
        match tryRewriteToSeqCombinators g bodyExpr with 
        | Some newBodyExpr ->    
            Some (Expr.Let (bind, newBodyExpr, m, newCache()))
        | None -> None

    // match --> match
    | Expr.Match (spBind, exprm, pt, targets, m, _ty) ->
        let targets = targets |> Array.map (fun (TTarget(vs, e, spTarget)) -> match tryRewriteToSeqCombinators g e with None -> None | Some e -> Some(TTarget(vs, e, spTarget)))
        if targets |> Array.forall Option.isSome then 
            let targets = targets |> Array.map Option.get
            let ty = targets |> Array.pick (fun (TTarget(_, e, _)) -> Some(tyOfExpr g e))
            Some (Expr.Match (spBind, exprm, pt, targets, m, ty))
        else
            None

    | _ -> 
        None

    
/// This detects forms arising from query expressions, i.e.
///    query.Run <@ query.Op(<seqSource>, <other-arguments>) @>  
///
/// We check if the combinators are marked with tag IEnumerable - if do, we optimize the "Run" and quotation away, since RunQueryAsEnumerable simply performs
/// an eval.
let TryDetectQueryQuoteAndRun cenv (expr: Expr) = 
    let g = cenv.g
    match expr with
    | QueryRun g (bodyOfRun, reqdResultInfo) -> 
        //printfn "found Query.Run"
        match bodyOfRun with 
        | Expr.Quote (quotedExpr, _, true, _, _) ->  // true = isFromQueryExpression


            // This traverses uses of query operators like query.Where and query.AverageBy until we're left with something familiar.
            // All these operators take the input IEnumerable 'seqSource' as the first argument.
            //
            // When we find the 'core' of the query expression, then if that is using IEnumerable execution, 
            // try to rewrite the core into combinators approximating the compiled form of seq { ... }, which in turn
            // are eligible for state-machine representation. If that fails, we still rewrite to combinator form.
            let rec loopOuter (e: Expr) = 
                match e with 

                | QueryFor g (qTy, _, resultElemTy, _, _)  
                | QuerySelect g (qTy, _, resultElemTy, _, _) 
                | QueryYield g (qTy, resultElemTy, _) 
                | QueryYieldFrom g (qTy, resultElemTy, _) 
                     when typeEquiv cenv.g qTy (mkAppTy cenv.g.tcref_System_Collections_IEnumerable []) -> 

                    match tryRewriteToSeqCombinators cenv.g e with 
                    | Some newSource -> 
                        //printfn "Eliminating because source is not IQueryable"
                        Some (mkCallSeq cenv.g newSource.Range resultElemTy (mkCallSeqDelay cenv.g newSource.Range resultElemTy (mkUnitDelayLambda cenv.g newSource.Range newSource) ), 
                              Some(resultElemTy, qTy) )
                    | None -> 
                        //printfn "Not compiling to state machines, but still optimizing the use of quotations away"
                        Some (e, None)

                | AnyQueryBuilderOpTrans g (seqSource, replace) -> 
                    match loopOuter seqSource with
                    | Some (newSeqSource, newSeqSourceIsEnumerableInfo) -> 
                        let newSeqSourceAsQuerySource = 
                            match newSeqSourceIsEnumerableInfo with 
                            | Some (resultElemTy, qTy) -> mkCallNewQuerySource cenv.g newSeqSource.Range resultElemTy qTy newSeqSource 
                            | None -> newSeqSource
                        Some (replace newSeqSourceAsQuerySource, None)
                    | None -> None

                | _ -> 
                    None

            let resultExprInfo = loopOuter quotedExpr

            match resultExprInfo with
            | Some (resultExpr, exprIsEnumerableInfo) ->
                let resultExprAfterConvertToResultTy = 
                    match reqdResultInfo, exprIsEnumerableInfo with 
                    | Some _, Some _ | None, None -> resultExpr // the expression is a QuerySource, the result is a QuerySource, nothing to do
                    | Some resultElemTy, None -> mkCallGetQuerySourceAsEnumerable cenv.g expr.Range resultElemTy (TType_app(cenv.g.tcref_System_Collections_IEnumerable, [])) resultExpr
                    | None, Some (resultElemTy, qTy) -> mkCallNewQuerySource cenv.g expr.Range resultElemTy qTy resultExpr 
                Some resultExprAfterConvertToResultTy
            | None -> 
                None
                
                
        | _ -> 
            //printfn "Not eliminating because no Quote found"
            None
    | _ -> 
        //printfn "Not eliminating because no Run found"
        None

let IsILMethodRefSystemStringConcat (mref: ILMethodRef) =
    mref.Name = "Concat" &&
    mref.DeclaringTypeRef.Name = "System.String" &&
    (mref.ReturnType.IsNominal && mref.ReturnType.TypeRef.Name = "System.String") &&
    (mref.ArgCount >= 2 && mref.ArgCount <= 4 &&
        mref.ArgTypes 
        |> List.forall (fun ilTy ->
            ilTy.IsNominal && ilTy.TypeRef.Name = "System.String"))

let IsILMethodRefSystemStringConcatArray (mref: ILMethodRef) =
    mref.Name = "Concat" &&
    mref.DeclaringTypeRef.Name = "System.String" &&
    (mref.ReturnType.IsNominal && mref.ReturnType.TypeRef.Name = "System.String") &&
    (mref.ArgCount = 1 && 
        mref.ArgTypes
        |> List.forall (fun ilTy ->          
            match ilTy with
            | ILType.Array (shape, ilTy) when shape = ILArrayShape.SingleDimensional &&
                                              ilTy.IsNominal &&
                                              ilTy.TypeRef.Name = "System.String" -> true
            | _ -> false))
    
/// Optimize/analyze an expression
let rec OptimizeExpr cenv (env: IncrementalOptimizationEnv) expr =

    // Eliminate subsumption coercions for functions. This must be done post-typechecking because we need
    // complete inference types.
    let expr = NormalizeAndAdjustPossibleSubsumptionExprs cenv.g expr

    let expr = stripExpr expr

    match expr with
    // treat the common linear cases to avoid stack overflows, using an explicit continuation 
    | LinearOpExpr _
    | LinearMatchExpr _
    | Expr.Sequential _ 
    | Expr.Let _ ->  
        OptimizeLinearExpr cenv env expr (fun x -> x)

    | Expr.Const (c, m, ty) -> 
        OptimizeConst cenv env expr (c, m, ty)

    | Expr.Val (v, _vFlags, m) -> 
        OptimizeVal cenv env expr (v, m)

    | Expr.Quote (ast, splices, isFromQueryExpression, m, ty) -> 
          let doData data = map3Of4 (List.map (OptimizeExpr cenv env >> fst)) data
          let splices =
              match splices.Value with
              | Some (data1, data2opt) -> Some (doData data1, doData data2opt)
              | None -> None
          Expr.Quote (ast, ref splices, isFromQueryExpression, m, ty), 
          { TotalSize = 10
            FunctionSize = 1
            HasEffect = false  
            MightMakeCriticalTailcall=false
            Info=UnknownValue }

    | Expr.Obj (_, ty, basev, createExpr, overrides, iimpls, m) -> 
        OptimizeObjectExpr cenv env (ty, basev, createExpr, overrides, iimpls, m)

    | Expr.Op (op, tyargs, args, m) -> 
        OptimizeExprOp cenv env (op, tyargs, args, m)

    | Expr.App (f, fty, tyargs, argsl, m) -> 
        // eliminate uses of query
        match TryDetectQueryQuoteAndRun cenv expr with 
        | Some newExpr -> OptimizeExpr cenv env newExpr
        | None -> OptimizeApplication cenv env (f, fty, tyargs, argsl, m) 

    | Expr.Lambda (_lambdaId, _, _, argvs, _body, m, rty) -> 
        let topValInfo = ValReprInfo ([], [argvs |> List.map (fun _ -> ValReprInfo.unnamedTopArg1)], ValReprInfo.unnamedRetVal)
        let ty = mkMultiLambdaTy m argvs rty
        OptimizeLambdas None cenv env topValInfo expr ty

    | Expr.TyLambda (_lambdaId, tps, _body, _m, rty) -> 
        let topValInfo = ValReprInfo (ValReprInfo.InferTyparInfo tps, [], ValReprInfo.unnamedRetVal)
        let ty = mkForallTyIfNeeded tps rty
        OptimizeLambdas None cenv env topValInfo expr ty

    | Expr.TyChoose _ -> 
        OptimizeExpr cenv env (TypeRelations.ChooseTyparSolutionsForFreeChoiceTypars cenv.g cenv.amap expr)

    | Expr.Match (spMatch, exprm, dtree, targets, m, ty) -> 
        OptimizeMatch cenv env (spMatch, exprm, dtree, targets, m, ty)

    | Expr.LetRec (binds, bodyExpr, m, _) ->  
        OptimizeLetRec cenv env (binds, bodyExpr, m)

    | Expr.StaticOptimization (constraints, expr2, expr3, m) ->
        let expr2R, e2info = OptimizeExpr cenv env expr2
        let expr3R, e3info = OptimizeExpr cenv env expr3
        Expr.StaticOptimization (constraints, expr2R, expr3R, m), 
        { TotalSize = min e2info.TotalSize e3info.TotalSize
          FunctionSize = min e2info.FunctionSize e3info.FunctionSize
          HasEffect = e2info.HasEffect || e3info.HasEffect
          MightMakeCriticalTailcall=e2info.MightMakeCriticalTailcall || e3info.MightMakeCriticalTailcall // seems conservative
          Info= UnknownValue }

    | Expr.Link _eref -> 
        assert ("unexpected reclink" = "")
        failwith "Unexpected reclink"

    | Expr.WitnessArg _ ->
        expr, 
        { TotalSize = 10
          FunctionSize = 1
          HasEffect = false  
          MightMakeCriticalTailcall=false
          Info=UnknownValue }

/// Optimize/analyze an object expression
and OptimizeObjectExpr cenv env (ty, baseValOpt, basecall, overrides, iimpls, m) =
    let basecallR, basecallinfo = OptimizeExpr cenv env basecall
    let overridesR, overrideinfos = OptimizeMethods cenv env baseValOpt overrides
    let iimplsR, iimplsinfos = OptimizeInterfaceImpls cenv env baseValOpt iimpls
    let exprR=mkObjExpr(ty, baseValOpt, basecallR, overridesR, iimplsR, m)
    exprR, { TotalSize=closureTotalSize + basecallinfo.TotalSize + AddTotalSizes overrideinfos + AddTotalSizes iimplsinfos
             FunctionSize=1 (* a newobj *) 
             HasEffect=true
             MightMakeCriticalTailcall=false // creating an object is not a useful tailcall
             Info=UnknownValue}

/// Optimize/analyze the methods that make up an object expression
and OptimizeMethods cenv env baseValOpt methods = 
    OptimizeList (OptimizeMethod cenv env baseValOpt) methods

and OptimizeMethod cenv env baseValOpt (TObjExprMethod(slotsig, attribs, tps, vs, e, m) as tmethod) = 
    let env = {env with latestBoundId=Some tmethod.Id; functionVal = None}
    let env = BindTypeVarsToUnknown tps env
    let env = BindInternalValsToUnknown cenv vs env
    let env = Option.foldBack (BindInternalValToUnknown cenv) baseValOpt env
    let eR, einfo = OptimizeExpr cenv env e
    // Note: if we ever change this from being UnknownValue then we should call AbstractExprInfoByVars
    TObjExprMethod(slotsig, attribs, tps, vs, eR, m), 
    { TotalSize = einfo.TotalSize
      FunctionSize = 0
      HasEffect = false
      MightMakeCriticalTailcall=false
      Info=UnknownValue}

/// Optimize/analyze the interface implementations that form part of an object expression
and OptimizeInterfaceImpls cenv env baseValOpt iimpls = 
    OptimizeList (OptimizeInterfaceImpl cenv env baseValOpt) iimpls

/// Optimize/analyze the interface implementations that form part of an object expression
and OptimizeInterfaceImpl cenv env baseValOpt (ty, overrides) = 
    let overridesR, overridesinfos = OptimizeMethods cenv env baseValOpt overrides
    (ty, overridesR), 
    { TotalSize = AddTotalSizes overridesinfos
      FunctionSize = 1
      HasEffect = false
      MightMakeCriticalTailcall=false
      Info=UnknownValue}

/// Make and optimize String.Concat calls
and MakeOptimizedSystemStringConcatCall cenv env m args =
    let rec optimizeArg argExpr accArgs =
        match argExpr, accArgs with
        | Expr.Op(TOp.ILCall(_, _, _, _, _, _, _, ilMethRef, _, _, _), _, [ Expr.Op(TOp.Array, _, args, _) ], _), _ 
          when IsILMethodRefSystemStringConcatArray ilMethRef ->
            optimizeArgs args accArgs

        | Expr.Op(TOp.ILCall(_, _, _, _, _, _, _, ilMethRef, _, _, _), _, args, _), _ 
          when IsILMethodRefSystemStringConcat ilMethRef ->
            optimizeArgs args accArgs

// String constant folding requires a bit more work as we cannot quadratically concat strings at compile time.
#if STRING_CONSTANT_FOLDING
        // Optimize string constants, e.g. "1" + "2" will turn into "12"
        | Expr.Const (Const.String str1, _, _), Expr.Const (Const.String str2, _, _) :: accArgs ->
            mkString cenv.g m (str1 + str2) :: accArgs
#endif

        | arg, _ -> arg :: accArgs

    and optimizeArgs args accArgs =
        (args, accArgs)
        ||> List.foldBack (fun arg accArgs -> optimizeArg arg accArgs)

    let args = optimizeArgs args []

    let expr =
        match args with
        | [ arg ] ->
            arg
        | [ arg1; arg2 ] -> 
            mkStaticCall_String_Concat2 cenv.g m arg1 arg2
        | [ arg1; arg2; arg3 ] ->
            mkStaticCall_String_Concat3 cenv.g m arg1 arg2 arg3
        | [ arg1; arg2; arg3; arg4 ] ->
            mkStaticCall_String_Concat4 cenv.g m arg1 arg2 arg3 arg4
        | args ->
            let arg = mkArray (cenv.g.string_ty, args, m)
            mkStaticCall_String_Concat_Array cenv.g m arg

    match expr with
    | Expr.Op(TOp.ILCall(_, _, _, _, _, _, _, ilMethRef, _, _, _) as op, tyargs, args, m) 
      when IsILMethodRefSystemStringConcat ilMethRef || IsILMethodRefSystemStringConcatArray ilMethRef ->
        OptimizeExprOpReductions cenv env (op, tyargs, args, m)
    | _ ->
        OptimizeExpr cenv env expr

/// Optimize/analyze an application of an intrinsic operator to arguments
and OptimizeExprOp cenv env (op, tyargs, args, m) =

    // Special cases 
    match op, tyargs, args with 
    | TOp.Coerce, [toty;fromty], [arg] -> 
        let argR, einfo = OptimizeExpr cenv env arg
        if typeEquiv cenv.g toty fromty then argR, einfo 
        else 
          mkCoerceExpr(argR, toty, m, fromty), 
          { TotalSize=einfo.TotalSize + 1
            FunctionSize=einfo.FunctionSize + 1
            HasEffect = true  
            MightMakeCriticalTailcall=false
            Info=UnknownValue }

    // Handle address-of 
    | TOp.LValueOp ((LAddrOf _ as lop), lv), _, _ ->
        let newVal, _ = OptimizeExpr cenv env (exprForValRef m lv)
        let newOp =
            match newVal with
            // Do not optimize if it's a top level static binding.
            | Expr.Val (v, _, _) when not v.IsCompiledAsTopLevel -> TOp.LValueOp (lop, v)
            | _ -> op
        let newExpr = Expr.Op (newOp, tyargs, args, m)
        newExpr,
        { TotalSize = 1
          FunctionSize = 1
          HasEffect = OpHasEffect cenv.g m newOp
          MightMakeCriticalTailcall = false
          Info = ValueOfExpr newExpr }

    // Handle these as special cases since mutables are allowed inside their bodies 
    | TOp.While (spWhile, marker), _, [Expr.Lambda (_, _, _, [_], e1, _, _);Expr.Lambda (_, _, _, [_], e2, _, _)] ->
        OptimizeWhileLoop cenv { env with disableMethodSplitting=true } (spWhile, marker, e1, e2, m) 

    | TOp.For (spStart, dir), _, [Expr.Lambda (_, _, _, [_], e1, _, _);Expr.Lambda (_, _, _, [_], e2, _, _);Expr.Lambda (_, _, _, [v], e3, _, _)] -> 
        OptimizeFastIntegerForLoop cenv { env with disableMethodSplitting=true } (spStart, v, e1, dir, e2, e3, m) 

    | TOp.TryFinally (spTry, spFinally), [resty], [Expr.Lambda (_, _, _, [_], e1, _, _); Expr.Lambda (_, _, _, [_], e2, _, _)] -> 
        OptimizeTryFinally cenv env (spTry, spFinally, e1, e2, m, resty)

    | TOp.TryWith (spTry, spWith), [resty], [Expr.Lambda (_, _, _, [_], e1, _, _); Expr.Lambda (_, _, _, [vf], ef, _, _); Expr.Lambda (_, _, _, [vh], eh, _, _)] ->
        OptimizeTryWith cenv env (e1, vf, ef, vh, eh, m, resty, spTry, spWith)

    | TOp.TraitCall traitInfo, [], args ->
        OptimizeTraitCall cenv env (traitInfo, args, m) 

   // This code hooks arr.Length. The idea is to ensure loops end up in the "same shape"as the forms of loops that the .NET JIT
   // guarantees to optimize.
  
    | TOp.ILCall (_, _, _, _, _, _, _, ilMethRef, _, _, _), _, [arg]
        when (ilMethRef.DeclaringTypeRef.Name = cenv.g.ilg.typ_Array.TypeRef.Name &&
              ilMethRef.Name = "get_Length" &&
              isArray1DTy cenv.g (tyOfExpr cenv.g arg)) -> 
         OptimizeExpr cenv env (Expr.Op (TOp.ILAsm (i_ldlen, [cenv.g.int_ty]), [], [arg], m))

    // Empty IL instruction lists are used as casts in prim-types.fs. But we can get rid of them 
    // if the types match up. 
    | TOp.ILAsm ([], [ty]), _, [a] when typeEquiv cenv.g (tyOfExpr cenv.g a) ty -> OptimizeExpr cenv env a

    // Optimize calls when concatenating strings, e.g. "1" + "2" + "3" + "4" .. etc.
    | TOp.ILCall(_, _, _, _, _, _, _, ilMethRef, _, _, _), _, [ Expr.Op(TOp.Array, _, args, _) ] 
      when IsILMethodRefSystemStringConcatArray ilMethRef ->
        MakeOptimizedSystemStringConcatCall cenv env m args
    | TOp.ILCall(_, _, _, _, _, _, _, ilMethRef, _, _, _), _, args 
      when IsILMethodRefSystemStringConcat ilMethRef ->
        MakeOptimizedSystemStringConcatCall cenv env m args

    | _ -> 
        // Reductions
        OptimizeExprOpReductions cenv env (op, tyargs, args, m)

and OptimizeExprOpReductions cenv env (op, tyargs, args, m) =
    let argsR, arginfos = OptimizeExprsThenConsiderSplits cenv env args
    OptimizeExprOpReductionsAfter cenv env (op, tyargs, argsR, arginfos, m)

and OptimizeExprOpReductionsAfter cenv env (op, tyargs, argsR, arginfos, m) =
    let knownValue = 
        match op, arginfos with 
        | TOp.ValFieldGet rf, [e1info] -> TryOptimizeRecordFieldGet cenv env (e1info, rf, tyargs, m) 
        | TOp.TupleFieldGet (tupInfo, n), [e1info] -> TryOptimizeTupleFieldGet cenv env (tupInfo, e1info, tyargs, n, m)
        | TOp.UnionCaseFieldGet (cspec, n), [e1info] -> TryOptimizeUnionCaseGet cenv env (e1info, cspec, tyargs, n, m)
        | _ -> None
    match knownValue with 
    | Some valu -> 
        match TryOptimizeVal cenv env (None, false, valu, m) with 
        | Some res -> OptimizeExpr cenv env res (* discard e1 since guard ensures it has no effects *)
        | None -> OptimizeExprOpFallback cenv env (op, tyargs, argsR, m) arginfos valu
    | None -> OptimizeExprOpFallback cenv env (op, tyargs, argsR, m) arginfos UnknownValue

and OptimizeExprOpFallback cenv env (op, tyargs, argsR, m) arginfos valu =
    // The generic case - we may collect information, but the construction/projection doesn't disappear 
    let argsTSize = AddTotalSizes arginfos
    let argsFSize = AddFunctionSizes arginfos
    let argEffects = OrEffects arginfos
    let argValues = List.map (fun x -> x.Info) arginfos
    let effect = OpHasEffect cenv.g m op
    let cost, valu = 
      match op with
      | TOp.UnionCase c -> 2, MakeValueInfoForUnionCase c (Array.ofList argValues)
      | TOp.ExnConstr _ -> 2, valu (* REVIEW: information collection possible here *)
      | TOp.Tuple tupInfo -> 
          let isStruct = evalTupInfoIsStruct tupInfo 
          if isStruct then 0, valu 
          else 1,MakeValueInfoForTuple (Array.ofList argValues)
      | TOp.AnonRecd anonInfo -> 
          let isStruct = evalAnonInfoIsStruct anonInfo 
          if isStruct then 0, valu 
          else 1, valu
      | TOp.AnonRecdGet _ 
      | TOp.ValFieldGet _     
      | TOp.TupleFieldGet _    
      | TOp.UnionCaseFieldGet _   
      | TOp.ExnFieldGet _
      | TOp.UnionCaseTagGet _ -> 
          // REVIEW: reduction possible here, and may be very effective
          1, valu 
      | TOp.UnionCaseProof _ -> 
          // We count the proof as size 0
          // We maintain the value of the source of the proof-cast if it is known to be a UnionCaseValue
          let valu = 
              match argValues.[0] with 
              | StripUnionCaseValue (uc, info) -> UnionCaseValue(uc, info) 
              | _ -> valu
          0, valu
      | TOp.ILAsm (instrs, retTypes) -> 
          min instrs.Length 1, 
          mkAssemblyCodeValueInfo cenv.g instrs argValues retTypes
      | TOp.Bytes bytes -> bytes.Length/10, valu
      | TOp.UInt16s bytes -> bytes.Length/10, valu
      | TOp.ValFieldGetAddr _     
      | TOp.Array | TOp.For _ | TOp.While _ | TOp.TryWith _ | TOp.TryFinally _
      | TOp.ILCall _ | TOp.TraitCall _ | TOp.LValueOp _ | TOp.ValFieldSet _
      | TOp.UnionCaseFieldSet _ | TOp.RefAddrGet _ | TOp.Coerce | TOp.Reraise
      | TOp.UnionCaseFieldGetAddr _   
      | TOp.ExnFieldSet _ -> 1, valu
      | TOp.Recd (ctorInfo, tcref) ->
          let finfos = tcref.AllInstanceFieldsAsList
          // REVIEW: this seems a little conservative: Allocating a record with a mutable field 
          // is not an effect - only reading or writing the field is. 
          let valu = 
              match ctorInfo with 
              | RecdExprIsObjInit -> UnknownValue
              | RecdExpr -> 
                   if argValues.Length <> finfos.Length then valu 
                   else MakeValueInfoForRecord tcref (Array.ofList ((argValues, finfos) ||> List.map2 (fun x f -> if f.IsMutable then UnknownValue else x) ))
          2, valu  
      | TOp.Goto _ | TOp.Label _ | TOp.Return -> assert false; error(InternalError("unexpected goto/label/return in optimization", m))

    // Indirect calls to IL code are always taken as tailcalls
    let mayBeCriticalTailcall = 
        match op with
        | TOp.ILCall (isVirtual, _, isCtor, _, _, _, _, _, _, _, _) -> not isCtor && isVirtual
        | _ -> false
    
    let vinfo = { TotalSize=argsTSize + cost
                  FunctionSize=argsFSize + cost
                  HasEffect=argEffects || effect                  
                  MightMakeCriticalTailcall= mayBeCriticalTailcall // discard tailcall info for args - these are not in tailcall position
                  Info=valu } 

    // Replace entire expression with known value? 
    match TryOptimizeValInfo cenv env m vinfo with 
    | Some res -> res, vinfo
    | None ->
          Expr.Op (op, tyargs, argsR, m), 
          { TotalSize=argsTSize + cost
            FunctionSize=argsFSize + cost
            HasEffect=argEffects || effect
            MightMakeCriticalTailcall= mayBeCriticalTailcall // discard tailcall info for args - these are not in tailcall position
            Info=valu }

/// Optimize/analyze a constant node
and OptimizeConst cenv env expr (c, m, ty) = 
    match TryEliminateDesugaredConstants cenv.g m c with 
    | Some e -> 
        OptimizeExpr cenv env e
    | None ->
        expr, { TotalSize=(match c with 
                           | Const.String b -> b.Length/10 
                           | _ -> 0)
                FunctionSize=0
                HasEffect=false
                MightMakeCriticalTailcall=false
                Info=MakeValueInfoForConst c ty}

/// Optimize/analyze a record lookup. 
and TryOptimizeRecordFieldGet cenv _env (e1info, (RecdFieldRef (rtcref, _) as r), _tinst, m) =
    match destRecdValue e1info.Info with
    | Some finfos when cenv.settings.EliminateRecdFieldGet() && not e1info.HasEffect ->
        match TryFindFSharpAttribute cenv.g cenv.g.attrib_CLIMutableAttribute rtcref.Attribs with
        | Some _ -> None
        | None ->
            let n = r.Index
            if n >= finfos.Length then errorR(InternalError( "TryOptimizeRecordFieldGet: term argument out of range", m))
            Some finfos.[n]
    | _ -> None
  
and TryOptimizeTupleFieldGet cenv _env (_tupInfo, e1info, tys, n, m) =
    match destTupleValue e1info.Info with
    | Some tups when cenv.settings.EliminateTupleFieldGet() && not e1info.HasEffect ->
        let len = tups.Length 
        if len <> tys.Length then errorR(InternalError("error: tuple lengths don't match", m))
        if n >= len then errorR(InternalError("TryOptimizeTupleFieldGet: tuple index out of range", m))
        Some tups.[n]
    | _ -> None
      
and TryOptimizeUnionCaseGet cenv _env (e1info, cspec, _tys, n, m) =
    match e1info.Info with
    | StripUnionCaseValue(cspec2, args) when cenv.settings.EliminateUnionCaseFieldGet() && not e1info.HasEffect && cenv.g.unionCaseRefEq cspec cspec2 ->
        if n >= args.Length then errorR(InternalError( "TryOptimizeUnionCaseGet: term argument out of range", m))
        Some args.[n]
    | _ -> None

/// Optimize/analyze a for-loop
and OptimizeFastIntegerForLoop cenv env (spStart, v, e1, dir, e2, e3, m) =
    let e1R, e1info = OptimizeExpr cenv env e1 
    let e2R, e2info = OptimizeExpr cenv env e2 
    let env = BindInternalValToUnknown cenv v env 
    let e3R, e3info = OptimizeExpr cenv env e3 
    // Try to replace F#-style loops with C# style loops that recompute their bounds but which are compiled more efficiently by the JITs, e.g.
    //  F# "for x = 0 to arr.Length - 1 do ..." --> C# "for (int x = 0; x < arr.Length; x++) { ... }"
    //  F# "for x = 0 to 10 do ..." --> C# "for (int x = 0; x < 11; x++) { ... }"
    let e2R, dir = 
        match dir, e2R with 
        // detect upwards for loops with bounds of the form "arr.Length - 1" and convert them to a C#-style for loop
        | FSharpForLoopUp, Expr.Op (TOp.ILAsm ([ (AI_sub | AI_sub_ovf)], _), _, [Expr.Op (TOp.ILAsm ([ I_ldlen; (AI_conv DT_I4)], _), _, [arre], _); Expr.Const (Const.Int32 1, _, _)], _) 
                  when not (snd(OptimizeExpr cenv env arre)).HasEffect -> 

            mkLdlen cenv.g (e2R.Range) arre, CSharpForLoopUp

        | FSharpForLoopUp, Expr.Op (TOp.ILAsm ([ (AI_sub | AI_sub_ovf)], _), _, [Expr.Op (TOp.ILCall(_,_,_,_,_,_,_, mth, _,_,_), _, [arre], _) as lenOp; Expr.Const (Const.Int32 1, _, _)], _) 
                  when 
                        mth.Name = "get_Length" && (mth.DeclaringTypeRef.FullName = "System.Span`1" || mth.DeclaringTypeRef.FullName = "System.ReadOnlySpan`1") 
                        && not (snd(OptimizeExpr cenv env arre)).HasEffect -> 

            lenOp, CSharpForLoopUp


        // detect upwards for loops with constant bounds, but not MaxValue!
        | FSharpForLoopUp, Expr.Const (Const.Int32 n, _, _) 
                  when n < System.Int32.MaxValue -> 
            mkIncr cenv.g (e2R.Range) e2R, CSharpForLoopUp

        | _ ->
            e2R, dir
 
    let einfos = [e1info;e2info;e3info] 
    let eff = OrEffects einfos 
    (* neither bounds nor body has an effect, and loops always terminate, hence eliminate the loop *)
    if not eff then 
        mkUnit cenv.g m, { TotalSize=0; FunctionSize=0; HasEffect=false; MightMakeCriticalTailcall=false; Info=UnknownValue }
    else
        let exprR = mkFor cenv.g (spStart, v, e1R, dir, e2R, e3R, m) 
        exprR, { TotalSize=AddTotalSizes einfos + forAndWhileLoopSize
                 FunctionSize=AddFunctionSizes einfos + forAndWhileLoopSize
                 HasEffect=eff
                 MightMakeCriticalTailcall=false
                 Info=UnknownValue }

/// Optimize/analyze a set of recursive bindings
and OptimizeLetRec cenv env (binds, bodyExpr, m) =
    let vs = binds |> List.map (fun v -> v.Var) 
    let env = BindInternalValsToUnknown cenv vs env 
    let bindsR, env = OptimizeBindings cenv true env binds 
    let bodyExprR, einfo = OptimizeExpr cenv env bodyExpr 
    // REVIEW: graph analysis to determine which items are unused 
    // Eliminate any unused bindings, as in let case 
    let bindsRR, bindinfos = 
        let fvs0 = freeInExpr CollectLocals bodyExprR 
        let fvs = List.fold (fun acc x -> unionFreeVars acc (fst x |> freeInBindingRhs CollectLocals)) fvs0 bindsR
        SplitValuesByIsUsedOrHasEffect cenv (fun () -> fvs.FreeLocals) bindsR
    // Trim out any optimization info that involves escaping values 
    let evalueR = AbstractExprInfoByVars (vs, []) einfo.Info 
    // REVIEW: size of constructing new closures - should probably add #freevars + #recfixups here 
    let bodyExprR = Expr.LetRec (bindsRR, bodyExprR, m, Construct.NewFreeVarsCache()) 
    let info = CombineValueInfos (einfo :: bindinfos) evalueR 
    bodyExprR, info

/// Optimize/analyze a linear sequence of sequential execution or RletR bindings.
and OptimizeLinearExpr cenv env expr contf =

    // Eliminate subsumption coercions for functions. This must be done post-typechecking because we need
    // complete inference types.
    let expr = DetectAndOptimizeForExpression cenv.g OptimizeAllForExpressions expr
    let expr = if cenv.settings.ExpandStructuralValues() then ExpandStructuralBinding cenv expr else expr 
    let expr = stripExpr expr

    match expr with 
    | Expr.Sequential (e1, e2, flag, spSeq, m) -> 
      let e1R, e1info = OptimizeExpr cenv env e1 
      OptimizeLinearExpr cenv env e2 (contf << (fun (e2R, e2info) -> 
        if (flag = NormalSeq) && 
           // Always eliminate '(); expr' sequences, even in debug code, to ensure that 
           // conditional method calls don't leave a dangling breakpoint (see FSharp 1.0 bug 6034)
           (cenv.settings.EliminateSequential () || (match e1R with Expr.Const (Const.Unit, _, _) -> true | _ -> false)) && 
           not e1info.HasEffect then 
            e2R, e2info
        else 
            Expr.Sequential (e1R, e2R, flag, spSeq, m), 
            { TotalSize = e1info.TotalSize + e2info.TotalSize
              FunctionSize = e1info.FunctionSize + e2info.FunctionSize
              HasEffect = flag <> NormalSeq || e1info.HasEffect || e2info.HasEffect
              MightMakeCriticalTailcall = 
                  (if flag = NormalSeq then e2info.MightMakeCriticalTailcall 
                   else e1info.MightMakeCriticalTailcall || e2info.MightMakeCriticalTailcall)
              // can't propagate value: must access result of computation for its effects 
              Info = UnknownValue }))

    | Expr.Let (bind, body, m, _) ->  
      let (bindR, bindingInfo), env = OptimizeBinding cenv false env bind 
      OptimizeLinearExpr cenv env body (contf << (fun (bodyR, bodyInfo) ->  
        // PERF: This call to ValueIsUsedOrHasEffect/freeInExpr amounts to 9% of all optimization time.
        // Is it quadratic or quasi-quadratic?
        if ValueIsUsedOrHasEffect cenv (fun () -> (freeInExpr CollectLocals bodyR).FreeLocals) (bindR, bindingInfo) then
            // Eliminate let bindings on the way back up
            let exprR, adjust = TryEliminateLet cenv env bindR bodyR m 
            exprR, 
            { TotalSize = bindingInfo.TotalSize + bodyInfo.TotalSize + adjust 
              FunctionSize = bindingInfo.FunctionSize + bodyInfo.FunctionSize + adjust 
              HasEffect=bindingInfo.HasEffect || bodyInfo.HasEffect
              MightMakeCriticalTailcall = bodyInfo.MightMakeCriticalTailcall // discard tailcall info from binding - not in tailcall position
              Info = UnknownValue }
        else 
            // On the way back up: Trim out any optimization info that involves escaping values on the way back up
            let evalueR = AbstractExprInfoByVars ([bindR.Var], []) bodyInfo.Info 
            bodyR, 
            { TotalSize = bindingInfo.TotalSize + bodyInfo.TotalSize - localVarSize // eliminated a local var
              FunctionSize = bindingInfo.FunctionSize + bodyInfo.FunctionSize - localVarSize (* eliminated a local var *) 
              HasEffect=bindingInfo.HasEffect || bodyInfo.HasEffect
              MightMakeCriticalTailcall = bodyInfo.MightMakeCriticalTailcall // discard tailcall info from binding - not in tailcall position
              Info = evalueR } ))

    | LinearMatchExpr (spMatch, exprm, dtree, tg1, e2, spTarget2, m, ty) ->
         let dtreeR, dinfo = OptimizeDecisionTree cenv env m dtree
         let tg1, tg1info = OptimizeDecisionTreeTarget cenv env m tg1
         // tailcall
         OptimizeLinearExpr cenv env e2 (contf << (fun (e2, e2info) ->
             // This ConsiderSplitToMethod is performed because it is present in OptimizeDecisionTreeTarget
             let e2, e2info = ConsiderSplitToMethod cenv.settings.abstractBigTargets cenv.settings.bigTargetSize cenv env (e2, e2info) 
             let tinfos = [tg1info; e2info]
             let targetsR = [tg1; TTarget([], e2, spTarget2)]
             OptimizeMatchPart2 cenv (spMatch, exprm, dtreeR, targetsR, dinfo, tinfos, m, ty)))

    | LinearOpExpr (op, tyargs, argsHead, argLast, m) ->
         let argsHeadR, argsHeadInfosR = OptimizeList (OptimizeExprThenConsiderSplit cenv env) argsHead
         // tailcall
         OptimizeLinearExpr cenv env argLast (contf << (fun (argLastR, argLastInfo) ->
             OptimizeExprOpReductionsAfter cenv env (op, tyargs, argsHeadR @ [argLastR], argsHeadInfosR @ [argLastInfo], m)))

    | _ -> contf (OptimizeExpr cenv env expr)

/// Optimize/analyze a try/finally construct.
and OptimizeTryFinally cenv env (spTry, spFinally, e1, e2, m, ty) =
    let e1R, e1info = OptimizeExpr cenv env e1 
    let e2R, e2info = OptimizeExpr cenv env e2 
    let info = 
        { TotalSize = e1info.TotalSize + e2info.TotalSize + tryFinallySize
          FunctionSize = e1info.FunctionSize + e2info.FunctionSize + tryFinallySize
          HasEffect = e1info.HasEffect || e2info.HasEffect
          MightMakeCriticalTailcall = false // no tailcalls from inside in try/finally
          Info = UnknownValue } 
    // try-finally, so no effect means no exception can be raised, so just sequence the finally
    if cenv.settings.EliminateTryWithAndTryFinally () && not e1info.HasEffect then 
        let sp = 
            match spTry with 
            | DebugPointAtTry.Yes _ -> DebugPointAtSequential.Both 
            | DebugPointAtTry.Body -> DebugPointAtSequential.Both 
            | DebugPointAtTry.No -> DebugPointAtSequential.StmtOnly
        Expr.Sequential (e1R, e2R, ThenDoSeq, sp, m), info 
    else
        mkTryFinally cenv.g (e1R, e2R, m, ty, spTry, spFinally), 
        info

/// Optimize/analyze a try/with construct.
and OptimizeTryWith cenv env (e1, vf, ef, vh, eh, m, ty, spTry, spWith) =
    let e1R, e1info = OptimizeExpr cenv env e1    
    // try-with, so no effect means no exception can be raised, so discard the with 
    if cenv.settings.EliminateTryWithAndTryFinally () && not e1info.HasEffect then 
        e1R, e1info 
    else
        let envinner = BindInternalValToUnknown cenv vf (BindInternalValToUnknown cenv vh env)
        let efR, efinfo = OptimizeExpr cenv envinner ef 
        let ehR, ehinfo = OptimizeExpr cenv envinner eh 
        let info = 
            { TotalSize = e1info.TotalSize + efinfo.TotalSize+ ehinfo.TotalSize + tryWithSize
              FunctionSize = e1info.FunctionSize + efinfo.FunctionSize+ ehinfo.FunctionSize + tryWithSize
              HasEffect = e1info.HasEffect || efinfo.HasEffect || ehinfo.HasEffect
              MightMakeCriticalTailcall = false
              Info = UnknownValue } 
        mkTryWith cenv.g (e1R, vf, efR, vh, ehR, m, ty, spTry, spWith), 
        info

/// Optimize/analyze a while loop
and OptimizeWhileLoop cenv env (spWhile, marker, e1, e2, m) =
    let e1R, e1info = OptimizeExpr cenv env e1 
    let e2R, e2info = OptimizeExpr cenv env e2 
    mkWhile cenv.g (spWhile, marker, e1R, e2R, m), 
    { TotalSize = e1info.TotalSize + e2info.TotalSize + forAndWhileLoopSize
      FunctionSize = e1info.FunctionSize + e2info.FunctionSize + forAndWhileLoopSize
      HasEffect = true // may not terminate
      MightMakeCriticalTailcall = false
      Info = UnknownValue }

/// Optimize/analyze a call to a 'member' constraint. Try to resolve the call to 
/// a witness (should always be possible due to compulsory inlining of any
/// code that contains calls to member constraints, except when analyzing 
/// not-yet-inlined generic code)
and OptimizeTraitCall cenv env (traitInfo, args, m) =

    // Resolve the static overloading early (during the compulsory rewrite phase) so we can inline. 
    match ConstraintSolver.CodegenWitnessExprForTraitConstraint cenv.TcVal cenv.g cenv.amap m traitInfo args with

    | OkResult (_, Some expr) -> OptimizeExpr cenv env expr

    // Resolution fails when optimizing generic code, ignore the failure
    | _ -> 
        let argsR, arginfos = OptimizeExprsThenConsiderSplits cenv env args 
        OptimizeExprOpFallback cenv env (TOp.TraitCall traitInfo, [], argsR, m) arginfos UnknownValue 

/// Make optimization decisions once we know the optimization information
/// for a value
and TryOptimizeVal cenv env (vOpt: ValRef option, mustInline, valInfoForVal, m) = 

    match valInfoForVal with 
    // Inline all constants immediately 
    | ConstValue (c, ty) -> 
        Some (Expr.Const (c, m, ty))

    | SizeValue (_, detail) ->
        TryOptimizeVal cenv env (vOpt, mustInline, detail, m) 

    | ValValue (vR, detail) -> 
         // Inline values bound to other values immediately 
         // Prefer to inline using the more specific info if possible 
         // If the more specific info didn't reveal an inline then use the value 
         match TryOptimizeVal cenv env (vOpt, mustInline, detail, m) with 
          | Some e -> Some e
          | None -> 
              // If we have proven 'v = compilerGeneratedValue'
              // and 'v' is being eliminated in favour of 'compilerGeneratedValue'
              // then replace the name of 'compilerGeneratedValue'
              // by 'v' and mark it not compiler generated so we preserve good debugging and names
              match vOpt with 
              | Some v when not v.IsCompilerGenerated && vR.IsCompilerGenerated -> 
                  vR.Deref.SetIsCompilerGenerated(false)
                  vR.Deref.SetLogicalName(v.LogicalName)
              | _ -> ()
              Some(exprForValRef m vR)

    | ConstExprValue(_size, expr) ->
        Some (remarkExpr m (copyExpr cenv.g CloneAllAndMarkExprValsAsCompilerGenerated expr))

    | CurriedLambdaValue (_, _, _, expr, _) when mustInline ->
        Some (remarkExpr m (copyExpr cenv.g CloneAllAndMarkExprValsAsCompilerGenerated expr))

    | TupleValue _ | UnionCaseValue _ | RecdValue _ when mustInline ->
        failwith "tuple, union and record values cannot be marked 'inline'"

    | UnknownValue when mustInline ->
        warning(Error(FSComp.SR.optValueMarkedInlineHasUnexpectedValue(), m)); None

    | _ when mustInline ->
        warning(Error(FSComp.SR.optValueMarkedInlineCouldNotBeInlined(), m)); None
    | _ -> None 
  
and TryOptimizeValInfo cenv env m vinfo = 
    if vinfo.HasEffect then None else TryOptimizeVal cenv env (None, false, vinfo.Info, m)

/// Add 'v1 = v2' information into the information stored about a value
and AddValEqualityInfo g m (v: ValRef) info =
    // ValValue is information that v = v2, where v2 does not change 
    // So we can't record this information for mutable values. An exception can be made
    // for "outArg" values arising from method calls since they are only temporarily mutable
    // when their address is passed to the method call. Another exception are mutable variables
    // created for tuple elimination in branching tuple bindings because they are assigned to
    // exactly once.
    if not v.IsMutable || IsKnownOnlyMutableBeforeUse v then 
        { info with Info = MakeValueInfoForValue g m v info.Info }
    else
        info 

/// Optimize/analyze a use of a value
and OptimizeVal cenv env expr (v: ValRef, m) =
    let valInfoForVal = GetInfoForVal cenv env m v 

    match TryOptimizeVal cenv env (Some v, v.MustInline, valInfoForVal.ValExprInfo, m) with
    | Some e -> 
       // don't reoptimize inlined lambdas until they get applied to something
       match e with 
       | Expr.TyLambda _ 
       | Expr.Lambda _ ->
           e, (AddValEqualityInfo cenv.g m v 
                    { Info=valInfoForVal.ValExprInfo 
                      HasEffect=false 
                      MightMakeCriticalTailcall = false
                      FunctionSize=10 
                      TotalSize=10})
       | _ -> 
           let e, einfo = OptimizeExpr cenv env e 
           e, AddValEqualityInfo cenv.g m v einfo 

    | None -> 
       if v.MustInline then error(Error(FSComp.SR.optFailedToInlineValue(v.DisplayName), m))
       expr, (AddValEqualityInfo cenv.g m v 
                    { Info=valInfoForVal.ValExprInfo 
                      HasEffect=false 
                      MightMakeCriticalTailcall = false
                      FunctionSize=1 
                      TotalSize=1})

/// Attempt to replace an application of a value by an alternative value.
and StripToNominalTyconRef cenv ty = 
    match tryAppTy cenv.g ty with
    | ValueSome x -> x
    | _ ->
        if isRefTupleTy cenv.g ty then
            let tyargs = destRefTupleTy cenv.g ty
            mkCompiledTupleTyconRef cenv.g false (List.length tyargs), tyargs 
        else failwith "StripToNominalTyconRef: unreachable" 

and CanDevirtualizeApplication cenv v vref ty args = 
     valRefEq cenv.g v vref
     && not (isUnitTy cenv.g ty)
     && isAppTy cenv.g ty 
     // Exclusion: Some unions have null as representations 
     && not (IsUnionTypeWithNullAsTrueValue cenv.g (fst(StripToNominalTyconRef cenv ty)).Deref)  
     // If we de-virtualize an operation on structs then we have to take the address of the object argument
     // Hence we have to actually have the object argument available to us, 
     && (not (isStructTy cenv.g ty) || not (isNil args)) 

and TakeAddressOfStructArgumentIfNeeded cenv (vref: ValRef) ty args m =
    if vref.IsInstanceMember && isStructTy cenv.g ty then 
        match args with 
        | objArg :: rest -> 
            // We set NeverMutates here, allowing more address-taking. This is valid because we only ever use DevirtualizeApplication to transform 
            // known calls to known generated F# code for CompareTo, Equals and GetHashCode.
            // If we ever reuse DevirtualizeApplication to transform an arbitrary virtual call into a 
            // direct call then this assumption is not valid.
            let wrap, objArgAddress, _readonly, _writeonly = mkExprAddrOfExpr cenv.g true false NeverMutates objArg None m
            wrap, (objArgAddress :: rest)
        | _ -> 
            // no wrapper, args stay the same 
            id, args
    else
        id, args

and DevirtualizeApplication cenv env (vref: ValRef) ty tyargs args m =
    let wrap, args = TakeAddressOfStructArgumentIfNeeded cenv vref ty args m
    let transformedExpr = wrap (MakeApplicationAndBetaReduce cenv.g (exprForValRef m vref, vref.Type, (if isNil tyargs then [] else [tyargs]), args, m))
    OptimizeExpr cenv env transformedExpr
  
and TryDevirtualizeApplication cenv env (f, tyargs, args, m) =
    match f, tyargs, args with 

    // Optimize/analyze calls to LanguagePrimitives.HashCompare.GenericComparisonIntrinsic when type is known 
    // to be augmented with a visible comparison value. 
    //
    // e.g rewrite 
    //      'LanguagePrimitives.HashCompare.GenericComparisonIntrinsic (x: C) (y: C)' 
    //  --> 'x.CompareTo(y: C)' where this is a direct call to the implementation of CompareTo, i.e.
    //        C :: CompareTo(C)
    //    not C :: CompareTo(obj)
    //
    // If C is a struct type then we have to take the address of 'c'
    
    | Expr.Val (v, _, _), [ty], _ when CanDevirtualizeApplication cenv v cenv.g.generic_comparison_inner_vref ty args ->
         
        let tcref, tyargs = StripToNominalTyconRef cenv ty
        match tcref.GeneratedCompareToValues with 
        | Some (_, vref) -> Some (DevirtualizeApplication cenv env vref ty tyargs args m)
        | _ -> None
        
    | Expr.Val (v, _, _), [ty], _ when CanDevirtualizeApplication cenv v cenv.g.generic_comparison_withc_inner_vref ty args ->
         
        let tcref, tyargs = StripToNominalTyconRef cenv ty
        match tcref.GeneratedCompareToWithComparerValues, args with 
        | Some vref, [comp; x; y] -> 
            // the target takes a tupled argument, so we need to reorder the arg expressions in the
            // arg list, and create a tuple of y & comp
            // push the comparer to the end and box the argument
            let args2 = [x; mkRefTupledNoTypes cenv.g m [mkCoerceExpr(y, cenv.g.obj_ty, m, ty) ; comp]]
            Some (DevirtualizeApplication cenv env vref ty tyargs args2 m)
        | _ -> None
        
    // Optimize/analyze calls to LanguagePrimitives.HashCompare.GenericEqualityIntrinsic when type is known 
    // to be augmented with a visible equality-without-comparer value. 
    //   REVIEW: GenericEqualityIntrinsic (which has no comparer) implements PER semantics (5537: this should be ER semantics)
    //           We are devirtualizing to a Equals(T) method which also implements PER semantics (5537: this should be ER semantics)
    | Expr.Val (v, _, _), [ty], _ when CanDevirtualizeApplication cenv v cenv.g.generic_equality_er_inner_vref ty args ->
         
        let tcref, tyargs = StripToNominalTyconRef cenv ty 
        match tcref.GeneratedHashAndEqualsValues with 
        | Some (_, vref) -> Some (DevirtualizeApplication cenv env vref ty tyargs args m)
        | _ -> None
        
    // Optimize/analyze calls to LanguagePrimitives.HashCompare.GenericEqualityWithComparerFast
    | Expr.Val (v, _, _), [ty], _ when CanDevirtualizeApplication cenv v cenv.g.generic_equality_withc_inner_vref ty args ->
        let tcref, tyargs = StripToNominalTyconRef cenv ty
        match tcref.GeneratedHashAndEqualsWithComparerValues, args with
        | Some (_, _, withcEqualsVal), [comp; x; y] -> 
            // push the comparer to the end and box the argument
            let args2 = [x; mkRefTupledNoTypes cenv.g m [mkCoerceExpr(y, cenv.g.obj_ty, m, ty) ; comp]]
            Some (DevirtualizeApplication cenv env withcEqualsVal ty tyargs args2 m)
        | _ -> None 
      
    // Optimize/analyze calls to LanguagePrimitives.HashCompare.GenericEqualityWithComparer
    | Expr.Val (v, _, _), [ty], _ when CanDevirtualizeApplication cenv v cenv.g.generic_equality_per_inner_vref ty args && not(isRefTupleTy cenv.g ty) ->
       let tcref, tyargs = StripToNominalTyconRef cenv ty
       match tcref.GeneratedHashAndEqualsWithComparerValues, args with
       | Some (_, _, withcEqualsVal), [x; y] -> 
           let args2 = [x; mkRefTupledNoTypes cenv.g m [mkCoerceExpr(y, cenv.g.obj_ty, m, ty); (mkCallGetGenericPEREqualityComparer cenv.g m)]]
           Some (DevirtualizeApplication cenv env withcEqualsVal ty tyargs args2 m)
       | _ -> None     
    
    // Optimize/analyze calls to LanguagePrimitives.HashCompare.GenericHashIntrinsic
    | Expr.Val (v, _, _), [ty], _ when CanDevirtualizeApplication cenv v cenv.g.generic_hash_inner_vref ty args ->
        let tcref, tyargs = StripToNominalTyconRef cenv ty
        match tcref.GeneratedHashAndEqualsWithComparerValues, args with
        | Some (_, withcGetHashCodeVal, _), [x] -> 
            let args2 = [x; mkCallGetGenericEREqualityComparer cenv.g m]
            Some (DevirtualizeApplication cenv env withcGetHashCodeVal ty tyargs args2 m)
        | _ -> None 
        
    // Optimize/analyze calls to LanguagePrimitives.HashCompare.GenericHashWithComparerIntrinsic
    | Expr.Val (v, _, _), [ty], _ when CanDevirtualizeApplication cenv v cenv.g.generic_hash_withc_inner_vref ty args ->
        let tcref, tyargs = StripToNominalTyconRef cenv ty
        match tcref.GeneratedHashAndEqualsWithComparerValues, args with
        | Some (_, withcGetHashCodeVal, _), [comp; x] -> 
            let args2 = [x; comp]
            Some (DevirtualizeApplication cenv env withcGetHashCodeVal ty tyargs args2 m)
        | _ -> None 

    // Optimize/analyze calls to LanguagePrimitives.HashCompare.GenericComparisonWithComparerIntrinsic for tuple types
    | Expr.Val (v, _, _), [ty], _ when valRefEq cenv.g v cenv.g.generic_comparison_inner_vref && isRefTupleTy cenv.g ty ->
        let tyargs = destRefTupleTy cenv.g ty 
        let vref = 
            match tyargs.Length with 
            | 2 -> Some cenv.g.generic_compare_withc_tuple2_vref 
            | 3 -> Some cenv.g.generic_compare_withc_tuple3_vref 
            | 4 -> Some cenv.g.generic_compare_withc_tuple4_vref 
            | 5 -> Some cenv.g.generic_compare_withc_tuple5_vref 
            | _ -> None
        match vref with 
        | Some vref -> Some (DevirtualizeApplication cenv env vref ty tyargs (mkCallGetGenericComparer cenv.g m :: args) m)            
        | None -> None
        
    // Optimize/analyze calls to LanguagePrimitives.HashCompare.GenericHashWithComparerIntrinsic for tuple types
    | Expr.Val (v, _, _), [ty], _ when valRefEq cenv.g v cenv.g.generic_hash_inner_vref && isRefTupleTy cenv.g ty ->
        let tyargs = destRefTupleTy cenv.g ty 
        let vref = 
            match tyargs.Length with 
            | 2 -> Some cenv.g.generic_hash_withc_tuple2_vref 
            | 3 -> Some cenv.g.generic_hash_withc_tuple3_vref 
            | 4 -> Some cenv.g.generic_hash_withc_tuple4_vref 
            | 5 -> Some cenv.g.generic_hash_withc_tuple5_vref 
            | _ -> None
        match vref with 
        | Some vref -> Some (DevirtualizeApplication cenv env vref ty tyargs (mkCallGetGenericEREqualityComparer cenv.g m :: args) m)            
        | None -> None
        
    // Optimize/analyze calls to LanguagePrimitives.HashCompare.GenericEqualityIntrinsic for tuple types
    //  REVIEW (5537): GenericEqualityIntrinsic implements PER semantics, and we are replacing it to something also
    //                 implementing PER semantics. However GenericEqualityIntrinsic should implement ER semantics.
    | Expr.Val (v, _, _), [ty], _ when valRefEq cenv.g v cenv.g.generic_equality_per_inner_vref && isRefTupleTy cenv.g ty ->
        let tyargs = destRefTupleTy cenv.g ty 
        let vref = 
            match tyargs.Length with 
            | 2 -> Some cenv.g.generic_equals_withc_tuple2_vref 
            | 3 -> Some cenv.g.generic_equals_withc_tuple3_vref 
            | 4 -> Some cenv.g.generic_equals_withc_tuple4_vref 
            | 5 -> Some cenv.g.generic_equals_withc_tuple5_vref 
            | _ -> None
        match vref with 
        | Some vref -> Some (DevirtualizeApplication cenv env vref ty tyargs (mkCallGetGenericPEREqualityComparer cenv.g m :: args) m)            
        | None -> None
        
    // Optimize/analyze calls to LanguagePrimitives.HashCompare.GenericComparisonWithComparerIntrinsic for tuple types
    | Expr.Val (v, _, _), [ty], _ when valRefEq cenv.g v cenv.g.generic_comparison_withc_inner_vref && isRefTupleTy cenv.g ty ->
        let tyargs = destRefTupleTy cenv.g ty 
        let vref = 
            match tyargs.Length with 
            | 2 -> Some cenv.g.generic_compare_withc_tuple2_vref 
            | 3 -> Some cenv.g.generic_compare_withc_tuple3_vref 
            | 4 -> Some cenv.g.generic_compare_withc_tuple4_vref 
            | 5 -> Some cenv.g.generic_compare_withc_tuple5_vref 
            | _ -> None
        match vref with 
        | Some vref -> Some (DevirtualizeApplication cenv env vref ty tyargs args m)            
        | None -> None
        
    // Optimize/analyze calls to LanguagePrimitives.HashCompare.GenericHashWithComparerIntrinsic for tuple types
    | Expr.Val (v, _, _), [ty], _ when valRefEq cenv.g v cenv.g.generic_hash_withc_inner_vref && isRefTupleTy cenv.g ty ->
        let tyargs = destRefTupleTy cenv.g ty 
        let vref = 
            match tyargs.Length with 
            | 2 -> Some cenv.g.generic_hash_withc_tuple2_vref 
            | 3 -> Some cenv.g.generic_hash_withc_tuple3_vref 
            | 4 -> Some cenv.g.generic_hash_withc_tuple4_vref 
            | 5 -> Some cenv.g.generic_hash_withc_tuple5_vref 
            | _ -> None
        match vref with 
        | Some vref -> Some (DevirtualizeApplication cenv env vref ty tyargs args m)            
        | None -> None
        
    // Optimize/analyze calls to LanguagePrimitives.HashCompare.GenericEqualityWithComparerIntrinsic for tuple types
    | Expr.Val (v, _, _), [ty], _ when valRefEq cenv.g v cenv.g.generic_equality_withc_inner_vref && isRefTupleTy cenv.g ty ->
        let tyargs = destRefTupleTy cenv.g ty 
        let vref = 
            match tyargs.Length with 
            | 2 -> Some cenv.g.generic_equals_withc_tuple2_vref 
            | 3 -> Some cenv.g.generic_equals_withc_tuple3_vref 
            | 4 -> Some cenv.g.generic_equals_withc_tuple4_vref 
            | 5 -> Some cenv.g.generic_equals_withc_tuple5_vref 
            | _ -> None
        match vref with 
        | Some vref -> Some (DevirtualizeApplication cenv env vref ty tyargs args m)            
        | None -> None
        
    // Calls to LanguagePrimitives.IntrinsicFunctions.UnboxGeneric can be optimized to calls to UnboxFast when we know that the 
    // target type isn't 'NullNotLiked', i.e. that the target type is not an F# union, record etc. 
    // Note UnboxFast is just the .NET IL 'unbox.any' instruction. 
    | Expr.Val (v, _, _), [ty], _ when valRefEq cenv.g v cenv.g.unbox_vref && 
                                   canUseUnboxFast cenv.g m ty ->

        Some(DevirtualizeApplication cenv env cenv.g.unbox_fast_vref ty tyargs args m)
        
    // Calls to LanguagePrimitives.IntrinsicFunctions.TypeTestGeneric can be optimized to calls to TypeTestFast when we know that the 
    // target type isn't 'NullNotTrueValue', i.e. that the target type is not an F# union, record etc. 
    // Note TypeTestFast is just the .NET IL 'isinst' instruction followed by a non-null comparison 
    | Expr.Val (v, _, _), [ty], _ when valRefEq cenv.g v cenv.g.istype_vref && 
                                   canUseTypeTestFast cenv.g ty ->

        Some(DevirtualizeApplication cenv env cenv.g.istype_fast_vref ty tyargs args m)
        
    // Don't fiddle with 'methodhandleof' calls - just remake the application
    | Expr.Val (vref, _, _), _, _ when valRefEq cenv.g vref cenv.g.methodhandleof_vref ->
        Some( MakeApplicationAndBetaReduce cenv.g (exprForValRef m vref, vref.Type, (if isNil tyargs then [] else [tyargs]), args, m), 
              { TotalSize=1
                FunctionSize=1
                HasEffect=false
                MightMakeCriticalTailcall = false
                Info=UnknownValue})

    | _ -> None

/// Attempt to inline an application of a known value at callsites
and TryInlineApplication cenv env finfo (tyargs: TType list, args: Expr list, m) =
    // Considering inlining app 
    match finfo.Info with 
    | StripLambdaValue (lambdaId, arities, size, f2, f2ty) when
       (// Considering inlining lambda 
        cenv.optimizing &&
        cenv.settings.InlineLambdas () &&
        not finfo.HasEffect &&
        // Don't inline recursively! 
        not (Zset.contains lambdaId env.dontInline) &&
        (// Check the number of argument groups is enough to saturate the lambdas of the target. 
         (if tyargs |> List.exists (fun t -> match t with TType_measure _ -> false | _ -> true) then 1 else 0) + args.Length = arities &&
          (// Enough args
           (if size > cenv.settings.lambdaInlineThreshold + args.Length then
              // Not inlining lambda near, size too big
              false
            else true)))) ->
            
        let isBaseCall = not (List.isEmpty args) &&
                              match args.[0] with
                              | Expr.Val (vref, _, _) when vref.BaseOrThisInfo = BaseVal -> true
                              | _ -> false
        
        if isBaseCall then None else

        // Since Lazy`1 moved from FSharp.Core to mscorlib on .NET 4.0, inlining Lazy values from 2.0 will
        // confuse the optimizer if the assembly is referenced on 4.0, since there will be no value to tie back
        // to FSharp.Core                              
        let isValFromLazyExtensions =
            if cenv.g.compilingFslib then
                false
            else
                match finfo.Info with
                | ValValue(vref, _) ->
                    match vref.ApparentEnclosingEntity with
                    | Parent tcr when (tyconRefEq cenv.g cenv.g.lazy_tcr_canon tcr) ->
                            match tcr.CompiledRepresentation with
                            | CompiledTypeRepr.ILAsmNamed(iltr, _, _) -> 
                                match iltr.Scope with
                                | ILScopeRef.Assembly aref -> aref.Name = "FSharp.Core"
                                | _ -> false
                            | _ -> false
                    | _ -> false
                | _ -> false                                          
        
        if isValFromLazyExtensions then None else

        let isSecureMethod =
          match finfo.Info with
          | ValValue(vref, _) ->
                vref.Attribs |> List.exists (fun a -> (IsSecurityAttribute cenv.g cenv.amap cenv.casApplied a m) || (IsSecurityCriticalAttribute cenv.g a))
          | _ -> false                              

        if isSecureMethod then None else

        let isGetHashCode =
            match finfo.Info with
            | ValValue(vref, _) -> vref.DisplayName = "GetHashCode" && vref.IsCompilerGenerated
            | _ -> false

        if isGetHashCode then None else

        // Inlining lambda 
  (* ---------- printf "Inlining lambda near %a = %s\n" outputRange m (showL (exprL f2)) (* JAMES: *) ----------*)
        let f2R = remarkExpr m (copyExpr cenv.g CloneAllAndMarkExprValsAsCompilerGenerated f2)
        // Optimizing arguments after inlining

        // REVIEW: this is a cheapshot way of optimizing the arg expressions as well without the restriction of recursive  
        // inlining kicking into effect 
        let argsR = args |> List.map (fun e -> let eR, _einfo = OptimizeExpr cenv env e in eR) 
        // Beta reduce. MakeApplicationAndBetaReduce cenv.g does all the hard work. 
        // Inlining: beta reducing 
        let exprR = MakeApplicationAndBetaReduce cenv.g (f2R, f2ty, [tyargs], argsR, m)
        // Inlining: reoptimizing
        Some(OptimizeExpr cenv {env with dontInline= Zset.add lambdaId env.dontInline} exprR)
          
    | _ -> None

/// Optimize/analyze an application of a function to type and term arguments
and OptimizeApplication cenv env (f0, f0ty, tyargs, args, m) =
    // trying to devirtualize
    match TryDevirtualizeApplication cenv env (f0, tyargs, args, m) with 
    | Some res -> 
        // devirtualized
        res
    | None -> 
    let newf0, finfo = OptimizeExpr cenv env f0
    match TryInlineApplication cenv env finfo (tyargs, args, m) with 
    | Some res -> 
        // inlined
        res
    | None -> 

    let shapes = 
        match newf0 with 
        | Expr.Val (vref, _, _) ->
            match vref.ValReprInfo with
            | Some(ValReprInfo(_, detupArgsL, _)) ->
                let nargs = args.Length
                let nDetupArgsL = detupArgsL.Length
                let nShapes = min nargs nDetupArgsL 
                let detupArgsShapesL = 
                    List.truncate nShapes detupArgsL 
                    |> List.map (fun detupArgs -> 
                        match detupArgs with 
                        | [] | [_] -> UnknownValue
                        | _ -> TupleValue(Array.ofList (List.map (fun _ -> UnknownValue) detupArgs))) 
                List.zip (detupArgsShapesL @ List.replicate (nargs - nShapes) UnknownValue) args
            | _ -> args |> List.map (fun arg -> UnknownValue, arg)     
        | _ -> args |> List.map (fun arg -> UnknownValue, arg) 

    let newArgs, arginfos = OptimizeExprsThenReshapeAndConsiderSplits cenv env shapes
    // beta reducing
    let newExpr = MakeApplicationAndBetaReduce cenv.g (newf0, f0ty, [tyargs], newArgs, m) 
    
    match newf0, newExpr with 
    | (Expr.Lambda _ | Expr.TyLambda _), Expr.Let _ -> 
       // we beta-reduced, hence reoptimize 
        OptimizeExpr cenv env newExpr
    | _ -> 
        // regular

        // Determine if this application is a critical tailcall
        let mayBeCriticalTailcall = 
            match newf0 with 
            | KnownValApp(vref, _typeArgs, otherArgs) ->

                 // Check if this is a call to a function of known arity that has been inferred to not be a critical tailcall when used as a direct call
                 // This includes recursive calls to the function being defined (in which case we get a non-critical, closed-world tailcall).
                 // Note we also have to check the argument count to ensure this is a direct call (or a partial application).
                 let doesNotMakeCriticalTailcall = 
                     vref.MakesNoCriticalTailcalls || 
                     (let valInfoForVal = GetInfoForVal cenv env m vref in valInfoForVal.ValMakesNoCriticalTailcalls) ||
                     (match env.functionVal with | None -> false | Some (v, _) -> valEq vref.Deref v)
                 if doesNotMakeCriticalTailcall then
                    let numArgs = otherArgs.Length + newArgs.Length
                    match vref.ValReprInfo with 
                    | Some i -> numArgs > i.NumCurriedArgs 
                    | None -> 
                    match env.functionVal with 
                    | Some (_v, i) -> numArgs > i.NumCurriedArgs
                    | None -> true // over-application of a known function, which presumably returns a function. This counts as an indirect call
                 else
                    true // application of a function that may make a critical tailcall
                
            | _ -> 
                // All indirect calls (calls to unknown functions) are assumed to be critical tailcalls 
                true

        newExpr, { TotalSize=finfo.TotalSize + AddTotalSizes arginfos
                   FunctionSize=finfo.FunctionSize + AddFunctionSizes arginfos
                   HasEffect=true
                   MightMakeCriticalTailcall = mayBeCriticalTailcall
                   Info=ValueOfExpr newExpr }

/// Optimize/analyze a lambda expression
and OptimizeLambdas (vspec: Val option) cenv env topValInfo e ety = 
    match e with 
    | Expr.Lambda (lambdaId, _, _, _, _, m, _)  
    | Expr.TyLambda (lambdaId, _, _, m, _) ->
        let tps, ctorThisValOpt, baseValOpt, vsl, body, bodyty = IteratedAdjustArityOfLambda cenv.g cenv.amap topValInfo e
        let env = { env with functionVal = (match vspec with None -> None | Some v -> Some (v, topValInfo)) }
        let env = Option.foldBack (BindInternalValToUnknown cenv) ctorThisValOpt env
        let env = Option.foldBack (BindInternalValToUnknown cenv) baseValOpt env
        let env = BindTypeVarsToUnknown tps env
        let env = List.foldBack (BindInternalValsToUnknown cenv) vsl env
        let bodyR, bodyinfo = OptimizeExpr cenv env body
        let exprR = mkMemberLambdas m tps ctorThisValOpt baseValOpt vsl (bodyR, bodyty)
        let arities = vsl.Length
        let arities = if isNil tps then arities else 1+arities
        let bsize = bodyinfo.TotalSize
        
        /// Set the flag on the value indicating that direct calls can avoid a tailcall (which are expensive on .NET x86)
        /// MightMakeCriticalTailcall is true whenever the body of the method may itself do a useful tailcall, e.g. has
        /// an application in the last position.
        match vspec with 
        | Some v -> 
            if not bodyinfo.MightMakeCriticalTailcall then 
                v.SetMakesNoCriticalTailcalls() 
            
            // UNIT TEST HOOK: report analysis results for the first optimization phase 
            if cenv.settings.reportingPhase && not v.IsCompilerGenerated then 
                if cenv.settings.reportNoNeedToTailcall then 
                    if bodyinfo.MightMakeCriticalTailcall then
                        printfn "value %s at line %d may make a critical tailcall" v.DisplayName v.Range.StartLine 
                    else 
                        printfn "value %s at line %d does not make a critical tailcall" v.DisplayName v.Range.StartLine 
                if cenv.settings.reportTotalSizes then 
                    printfn "value %s at line %d has total size %d" v.DisplayName v.Range.StartLine bodyinfo.TotalSize 
                if cenv.settings.reportFunctionSizes then 
                    printfn "value %s at line %d has method size %d" v.DisplayName v.Range.StartLine bodyinfo.FunctionSize
                if cenv.settings.reportHasEffect then 
                    if bodyinfo.HasEffect then
                        printfn "function %s at line %d causes side effects or may not terminate" v.DisplayName v.Range.StartLine 
                    else 
                        printfn "function %s at line %d causes no side effects" v.DisplayName v.Range.StartLine 
        | _ -> 
            () 

        // can't inline any values with semi-recursive object references to self or base 
        let valu =   
          match baseValOpt with 
          | None -> CurriedLambdaValue (lambdaId, arities, bsize, exprR, ety) 
          | Some baseVal -> 
              let fvs = freeInExpr CollectLocals bodyR
              if fvs.UsesMethodLocalConstructs || fvs.FreeLocals.Contains baseVal then 
                  UnknownValue
              else 
                  let expr2 = mkMemberLambdas m tps ctorThisValOpt None vsl (bodyR, bodyty)
                  CurriedLambdaValue (lambdaId, arities, bsize, expr2, ety) 
                  
        let estimatedSize = 
            match vspec with
            | Some v when v.IsCompiledAsTopLevel -> methodDefnTotalSize
            | _ -> closureTotalSize

        exprR, { TotalSize=bsize + estimatedSize (* estimate size of new syntactic closure - expensive, in contrast to a method *)
                 FunctionSize=1 
                 HasEffect=false
                 MightMakeCriticalTailcall = false
                 Info= valu }
    | _ -> OptimizeExpr cenv env e 
      
/// Recursive calls that first try to make an expression "fit" the a shape
/// where it is about to be consumed.
and OptimizeExprsThenReshapeAndConsiderSplits cenv env exprs = 
    match exprs with 
    | [] -> NoExprs 
    | _ -> OptimizeList (OptimizeExprThenReshapeAndConsiderSplit cenv env) exprs

and OptimizeExprsThenConsiderSplits cenv env exprs = 
    match exprs with 
    | [] -> NoExprs 
    | _ -> OptimizeList (OptimizeExprThenConsiderSplit cenv env) exprs

and OptimizeExprThenReshapeAndConsiderSplit cenv env (shape, e) = 
    OptimizeExprThenConsiderSplit cenv env (ReshapeExpr cenv (shape, e))

and OptimizeDecisionTreeTargets cenv env m targets = 
    OptimizeList (OptimizeDecisionTreeTarget cenv env m) (Array.toList targets)

and ReshapeExpr cenv (shape, e) = 
  match shape, e with 
  | TupleValue subshapes, Expr.Val (_vref, _vFlags, m) ->
      let tinst = destRefTupleTy cenv.g (tyOfExpr cenv.g e)
      let subshapes = Array.toList subshapes
      mkRefTupled cenv.g m (List.mapi (fun i subshape -> ReshapeExpr cenv (subshape, mkTupleFieldGet cenv.g (tupInfoRef, e, tinst, i, m))) subshapes) tinst
  | _ ->  
      e

and OptimizeExprThenConsiderSplit cenv env e = 
  let eR, einfo = OptimizeExpr cenv env e
  // ALWAYS consider splits for enormous sub terms here - otherwise we will create invalid .NET programs  
  ConsiderSplitToMethod true cenv.settings.veryBigExprSize cenv env (eR, einfo) 

/// Decide whether to List.unzip a sub-expression into a new method
and ComputeSplitToMethodCondition flag threshold cenv env (e: Expr, einfo) = 
    flag &&
    // NOTE: The method splitting optimization is completely disabled if we are not taking tailcalls.
    cenv.emitTailcalls &&
    not env.disableMethodSplitting &&
    einfo.FunctionSize >= threshold &&

     // We can only split an expression out as a method if certain conditions are met. 
     // It can't use any protected or base calls, rethrow(), byrefs etc.
    let m = e.Range
    (let fvs = freeInExpr CollectLocals e
     not fvs.UsesUnboundRethrow &&
     not fvs.UsesMethodLocalConstructs &&
     fvs.FreeLocals |> Zset.forall (fun v -> 
          // no direct-self-recursive references
          not (env.dontSplitVars.ContainsVal v) &&
          (v.ValReprInfo.IsSome ||
            // All the free variables (apart from things with an arity, i.e. compiled as methods) should be normal, i.e. not base/this etc. 
            (v.BaseOrThisInfo = NormalVal && 
             // None of them should be byrefs 
             not (isByrefLikeTy cenv.g m v.Type) && 
             //  None of them should be local polymorphic constrained values 
             not (IsGenericValWithGenericConstraints cenv.g v) &&
             // None of them should be mutable 
             not v.IsMutable)))) &&
    not (isByrefLikeTy cenv.g m (tyOfExpr cenv.g e)) 

and ConsiderSplitToMethod flag threshold cenv env (e, einfo) = 
    if ComputeSplitToMethodCondition flag threshold cenv env (e, einfo) then
        let m = e.Range
        let uv, _ue = mkCompGenLocal m "unitVar" cenv.g.unit_ty
        let ty = tyOfExpr cenv.g e
        let nm = 
            match env.latestBoundId with 
            | Some id -> id.idText+suffixForVariablesThatMayNotBeEliminated 
            | None -> suffixForVariablesThatMayNotBeEliminated 
        let fv, fe = mkCompGenLocal m nm (cenv.g.unit_ty --> ty)
        mkInvisibleLet m fv (mkLambda m uv (e, ty)) 
          (primMkApp (fe, (cenv.g.unit_ty --> ty)) [] [mkUnit cenv.g m] m), 
        {einfo with FunctionSize=callSize }
    else
        e, einfo 

/// Optimize/analyze a pattern matching expression
and OptimizeMatch cenv env (spMatch, exprm, dtree, targets, m, ty) =
    // REVIEW: consider collecting, merging and using information flowing through each line of the decision tree to each target 
    let dtreeR, dinfo = OptimizeDecisionTree cenv env m dtree 
    let targetsR, tinfos = OptimizeDecisionTreeTargets cenv env m targets 
    OptimizeMatchPart2 cenv (spMatch, exprm, dtreeR, targetsR, dinfo, tinfos, m, ty)

and OptimizeMatchPart2 cenv (spMatch, exprm, dtreeR, targetsR, dinfo, tinfos, m, ty) =
    let newExpr, newInfo = RebuildOptimizedMatch (spMatch, exprm, m, ty, dtreeR, targetsR, dinfo, tinfos)
    let newExpr2 = if not (cenv.settings.localOpt()) then newExpr else CombineBoolLogic newExpr
    newExpr2, newInfo

and CombineMatchInfos dinfo tinfo = 
    { TotalSize = dinfo.TotalSize + tinfo.TotalSize
      FunctionSize = dinfo.FunctionSize + tinfo.FunctionSize
      HasEffect = dinfo.HasEffect || tinfo.HasEffect
      MightMakeCriticalTailcall=tinfo.MightMakeCriticalTailcall // discard tailcall info from decision tree since it's not in tailcall position
      Info= UnknownValue }

and RebuildOptimizedMatch (spMatch, exprm, m, ty, dtree, tgs, dinfo, tinfos) = 
     let tinfo = CombineValueInfosUnknown tinfos
     let expr = mkAndSimplifyMatch spMatch exprm m ty dtree tgs
     let einfo = CombineMatchInfos dinfo tinfo
     expr, einfo

/// Optimize/analyze a target of a decision tree
and OptimizeDecisionTreeTarget cenv env _m (TTarget(vs, expr, spTarget)) = 
    let env = BindInternalValsToUnknown cenv vs env 
    let exprR, einfo = OptimizeExpr cenv env expr 
    let exprR, einfo = ConsiderSplitToMethod cenv.settings.abstractBigTargets cenv.settings.bigTargetSize cenv env (exprR, einfo) 
    let evalueR = AbstractExprInfoByVars (vs, []) einfo.Info 
    TTarget(vs, exprR, spTarget), 
    { TotalSize=einfo.TotalSize 
      FunctionSize=einfo.FunctionSize
      HasEffect=einfo.HasEffect
      MightMakeCriticalTailcall = einfo.MightMakeCriticalTailcall 
      Info=evalueR }

/// Optimize/analyze a decision tree
and OptimizeDecisionTree cenv env m x =
    match x with 
    | TDSuccess (es, n) -> 
        let esR, einfos = OptimizeExprsThenConsiderSplits cenv env es 
        TDSuccess(esR, n), CombineValueInfosUnknown einfos
    | TDBind(bind, rest) -> 
        let (bind, binfo), envinner = OptimizeBinding cenv false env bind 
        let rest, rinfo = OptimizeDecisionTree cenv envinner m rest 

        if ValueIsUsedOrHasEffect cenv (fun () -> (accFreeInDecisionTree CollectLocals rest emptyFreeVars).FreeLocals) (bind, binfo) then

            let info = CombineValueInfosUnknown [rinfo;binfo]
            // try to fold the let-binding into a single result expression
            match rest with 
            | TDSuccess([e], n) ->
                let e, _adjust = TryEliminateLet cenv env bind e m 
                TDSuccess([e], n), info
            | _ -> 
                TDBind(bind, rest), info

        else 
            rest, rinfo

    | TDSwitch (e, cases, dflt, m) -> 
        // We always duplicate boolean-typed guards prior to optimizing. This is work which really should be done in patcompile.fs
        // where we must duplicate "when" expressions to ensure uniqueness of bound variables.
        //
        // However, we are not allowed to copy expressions in patcompile.fs because type checking is not complete (see FSharp 1.0 bug 4821).
        // Hence we do it here. There is no doubt a better way to do this.
        let e = if typeEquiv cenv.g (tyOfExpr cenv.g e) cenv.g.bool_ty then copyExpr cenv.g CloneAll e else e

        OptimizeSwitch cenv env (e, cases, dflt, m)

and TryOptimizeDecisionTreeTest cenv test vinfo = 
    match test, vinfo with 
    | DecisionTreeTest.UnionCase (c1, _), StripUnionCaseValue(c2, _) -> Some(cenv.g.unionCaseRefEq c1 c2)
    | DecisionTreeTest.ArrayLength (_, _), _ -> None
    | DecisionTreeTest.Const c1, StripConstValue c2 -> if c1 = Const.Zero || c2 = Const.Zero then None else Some(c1=c2)
    | DecisionTreeTest.IsNull, StripConstValue c2 -> Some(c2=Const.Zero)
    | DecisionTreeTest.IsInst (_srcty1, _tgty1), _ -> None
    // These should not occur in optimization
    | DecisionTreeTest.ActivePatternCase _, _ -> None
    | _ -> None

/// Optimize/analyze a switch construct from pattern matching 
and OptimizeSwitch cenv env (e, cases, dflt, m) =
    let eR, einfo = OptimizeExpr cenv env e 

    let cases, dflt = 
        if cenv.settings.EliminateSwitch() && not einfo.HasEffect then
            // Attempt to find a definite success, i.e. the first case where there is definite success
            match (List.tryFind (function (TCase(d2, _)) when TryOptimizeDecisionTreeTest cenv d2 einfo.Info = Some true -> true | _ -> false) cases) with 
            | Some(TCase(_, case)) -> [], Some case
            | _ -> 
                // Filter definite failures
                cases |> List.filter (function (TCase(d2, _)) when TryOptimizeDecisionTreeTest cenv d2 einfo.Info = Some false -> false | _ -> true), 
                dflt
        else
            cases, dflt 
    // OK, see what weRre left with and continue
    match cases, dflt with 
    | [], Some case -> OptimizeDecisionTree cenv env m case
    | _ -> OptimizeSwitchFallback cenv env (eR, einfo, cases, dflt, m)

and OptimizeSwitchFallback cenv env (eR, einfo, cases, dflt, m) =
    let casesR, cinfos =
        cases 
        |> List.map (fun (TCase(discrim, e)) -> let eR, einfo = OptimizeDecisionTree cenv env m e in TCase(discrim, eR), einfo)
        |> List.unzip
    let dfltR, dinfos =
        match dflt with
        | None -> None, [] 
        | Some df -> let dfR, einfo = OptimizeDecisionTree cenv env m df in Some dfR, [einfo] 
    let size = (dinfos.Length + cinfos.Length) * 2
    let info = CombineValueInfosUnknown (einfo :: cinfos @ dinfos)
    let info = { info with TotalSize = info.TotalSize + size; FunctionSize = info.FunctionSize + size; }
    TDSwitch (eR, casesR, dfltR, m), info

and OptimizeBinding cenv isRec env (TBind(vref, expr, spBind)) =
    try 
        
        // The aim here is to stop method splitting for direct-self-tailcalls. We do more than that: if an expression
        // occurs in the body of recursively defined values RVS, then we refuse to split
        // any expression that contains a reference to any value in RVS.
        // This doesn't prevent splitting for mutually recursive references. See FSharp 1.0 bug 2892.
        let env = 
            if isRec then { env with dontSplitVars = env.dontSplitVars.Add vref () } 
            else env
        
        let exprOptimized, einfo = 
            let env = if vref.IsCompilerGenerated && Option.isSome env.latestBoundId then env else {env with latestBoundId=Some vref.Id} 
            let cenv = if vref.InlineInfo = ValInline.PseudoVal then { cenv with optimizing=false} else cenv 
            let arityInfo = InferArityOfExprBinding cenv.g AllowTypeDirectedDetupling.No vref expr
            let exprOptimized, einfo = OptimizeLambdas (Some vref) cenv env arityInfo expr vref.Type 
            let size = localVarSize 
            exprOptimized, {einfo with FunctionSize=einfo.FunctionSize+size; TotalSize = einfo.TotalSize+size} 

        // Trim out optimization information for large lambdas we'll never inline
        // Trim out optimization information for expressions that call protected members 
        let rec cut ivalue = 
            match ivalue with
            | CurriedLambdaValue (_, arities, size, body, _) -> 
                if size > (cenv.settings.lambdaInlineThreshold + arities + 2) then 
                    // Discarding lambda for large binding 
                    UnknownValue 
                else
                    let fvs = freeInExpr CollectLocals body
                    if fvs.UsesMethodLocalConstructs then
                        // Discarding lambda for binding because uses protected members
                        UnknownValue 
                    else
                        ivalue

            | ValValue(v, x) -> ValValue(v, cut x)
            | TupleValue a -> TupleValue(Array.map cut a)
            | RecdValue (tcref, a) -> RecdValue(tcref, Array.map cut a)       
            | UnionCaseValue (a, b) -> UnionCaseValue (a, Array.map cut b)
            | UnknownValue | ConstValue _ | ConstExprValue _ -> ivalue
            | SizeValue(_, a) -> MakeSizedValueInfo (cut a) 

        let einfo = if vref.MustInline then einfo else {einfo with Info = cut einfo.Info } 

        let einfo = 
            if (not vref.MustInline && not (cenv.settings.KeepOptimizationValues())) ||
               
               // Bug 4916: do not record inline data for initialization trigger expressions
               // Note: we can't eliminate these value infos at the file boundaries because that would change initialization
               // order
               IsCompiledAsStaticPropertyWithField cenv.g vref ||
               
               (vref.InlineInfo = ValInline.Never) ||
               // MarshalByRef methods may not be inlined
               (match vref.DeclaringEntity with 
                | Parent tcref -> 
                    match cenv.g.system_MarshalByRefObject_tcref with
                    | None -> false
                    | Some mbrTyconRef ->
                    // Check we can deref system_MarshalByRefObject_tcref. When compiling against the Silverlight mscorlib we can't
                    if mbrTyconRef.TryDeref.IsSome then
                        // Check if this is a subtype of MarshalByRefObject
                        assert (cenv.g.system_MarshalByRefObject_ty.IsSome)
                        ExistsSameHeadTypeInHierarchy cenv.g cenv.amap vref.Range (generalizedTyconRef tcref) cenv.g.system_MarshalByRefObject_ty.Value
                    else 
                        false
                | ParentNone -> false) ||

               // These values are given a special going-over by the optimizer and 
               // ilxgen.fs, hence treat them as if no-inline (when preparing the inline information for 
               // FSharp.Core).
               (let nvref = mkLocalValRef vref 
                cenv.g.compilingFslib &&
                   (valRefEq cenv.g nvref cenv.g.seq_vref ||
                    valRefEq cenv.g nvref cenv.g.seq_generated_vref ||
                    valRefEq cenv.g nvref cenv.g.seq_finally_vref ||
                    valRefEq cenv.g nvref cenv.g.seq_using_vref ||
                    valRefEq cenv.g nvref cenv.g.seq_append_vref ||
                    valRefEq cenv.g nvref cenv.g.seq_empty_vref ||
                    valRefEq cenv.g nvref cenv.g.seq_delay_vref ||
                    valRefEq cenv.g nvref cenv.g.seq_singleton_vref ||
                    valRefEq cenv.g nvref cenv.g.seq_map_vref ||
                    valRefEq cenv.g nvref cenv.g.seq_collect_vref ||
                    valRefEq cenv.g nvref cenv.g.reference_equality_inner_vref ||
                    valRefEq cenv.g nvref cenv.g.generic_comparison_inner_vref ||
                    valRefEq cenv.g nvref cenv.g.generic_comparison_withc_inner_vref ||
                    valRefEq cenv.g nvref cenv.g.generic_equality_er_inner_vref ||
                    valRefEq cenv.g nvref cenv.g.generic_equality_per_inner_vref ||
                    valRefEq cenv.g nvref cenv.g.generic_equality_withc_inner_vref ||
                    valRefEq cenv.g nvref cenv.g.generic_hash_inner_vref))
            then {einfo with Info=UnknownValue} 
            else einfo 
        if vref.MustInline && IsPartialExprVal einfo.Info then 
            errorR(InternalError("the mustinline value '"+vref.LogicalName+"' was not inferred to have a known value", vref.Range))
        
        let env = BindInternalLocalVal cenv vref (mkValInfo einfo vref) env 
        (TBind(vref, exprOptimized, spBind), einfo), env
    with exn -> 
        errorRecovery exn vref.Range 
        raise (ReportedError (Some exn))
          
and OptimizeBindings cenv isRec env xs = List.mapFold (OptimizeBinding cenv isRec) env xs
    
and OptimizeModuleExpr cenv env x = 
    match x with   
    | ModuleOrNamespaceExprWithSig(mty, def, m) -> 
        // Optimize the module implementation
        let (def, info), (_env, bindInfosColl) = OptimizeModuleDef cenv (env, []) def  
        let bindInfosColl = List.concat bindInfosColl 
        
        // Compute the elements truly hidden by the module signature.
        // The hidden set here must contain NOT MORE THAN the set of values made inaccessible by 
        // the application of the signature. If it contains extra elements we'll accidentally eliminate
        // bindings.
         
        let (_renaming, hidden) as rpi = ComputeRemappingFromImplementationToSignature cenv.g def mty 
        let def = 
            if not (cenv.settings.localOpt()) then def else 

            let fvs = freeInModuleOrNamespace CollectLocals def 
            let dead = 
                bindInfosColl |> List.filter (fun (bind, binfo) -> 

                    // Check the expression has no side effect, e.g. is a lambda expression (a function definition)
                    not (ValueIsUsedOrHasEffect cenv (fun () -> fvs.FreeLocals) (bind, binfo)) &&

                    // Check the thing is hidden by the signature (if any)
                    hidden.HiddenVals.Contains bind.Var && 

                    // Check the thing is not compiled as a static field or property, since reflected definitions and other reflective stuff might need it
                    not (IsCompiledAsStaticProperty cenv.g bind.Var))

            let deadSet = Zset.addList (dead |> List.map (fun (bind, _) -> bind.Var)) (Zset.empty valOrder)

            // Eliminate dead private bindings from a module type by mutation. Note that the optimizer doesn't
            // actually copy the entire term - it copies the expression portions of the term and leaves the 
            // value_spec and entity_specs in place. However this means that the value_specs and entity specs 
            // need to be updated when a change is made that affects them, e.g. when a binding is eliminated. 
            // We'd have to do similar tricks if the type of variable is changed (as happens in TLR, which also
            // uses mutation), or if we eliminated a type constructor.
            //
            // It may be wise to move to a non-mutating implementation at some point here. Copying expressions is
            // probably more costly than copying specs anyway.
            let rec elimModTy (mtyp: ModuleOrNamespaceType) =                  
                let mty = 
                    new ModuleOrNamespaceType(kind=mtyp.ModuleOrNamespaceKind, 
                                              vals= (mtyp.AllValsAndMembers |> QueueList.filter (Zset.memberOf deadSet >> not)), 
                                              entities= mtyp.AllEntities)
                mtyp.ModuleAndNamespaceDefinitions |> List.iter elimModSpec
                mty

            and elimModSpec (mspec: ModuleOrNamespace) = 
                let mtyp = elimModTy mspec.ModuleOrNamespaceType 
                mspec.entity_modul_contents <- MaybeLazy.Strict mtyp

            let rec elimModDef x =                  
                match x with 
                | TMDefRec(isRec, tycons, mbinds, m) -> 
                    let mbinds = mbinds |> List.choose elimModuleBinding
                    TMDefRec(isRec, tycons, mbinds, m)
                | TMDefLet(bind, m) -> 
                    if Zset.contains bind.Var deadSet then TMDefRec(false, [], [], m) else x
                | TMDefDo _ -> x
                | TMDefs defs -> TMDefs(List.map elimModDef defs) 
                | TMAbstract _ -> x 

            and elimModuleBinding x = 
                match x with 
                | ModuleOrNamespaceBinding.Binding bind -> 
                     if bind.Var |> Zset.memberOf deadSet then None
                     else Some x
                | ModuleOrNamespaceBinding.Module(mspec, d) ->
                    // Clean up the ModuleOrNamespaceType by mutation
                    elimModSpec mspec
                    Some (ModuleOrNamespaceBinding.Module(mspec, elimModDef d))
            
            elimModDef def 

        let info = AbstractAndRemapModulInfo "defs" cenv.g m rpi info

        ModuleOrNamespaceExprWithSig(mty, def, m), info 

and mkValBind (bind: Binding) info =
    (mkLocalValRef bind.Var, info)

and OptimizeModuleDef cenv (env, bindInfosColl) x = 
    match x with 
    | TMDefRec(isRec, tycons, mbinds, m) -> 
        let env = if isRec then BindInternalValsToUnknown cenv (allValsOfModDef x) env else env
        let mbindInfos, (env, bindInfosColl) = OptimizeModuleBindings cenv (env, bindInfosColl) mbinds
        let mbinds, minfos = List.unzip mbindInfos
        let binds = minfos |> List.choose (function Choice1Of2 (x, _) -> Some x | _ -> None)
        let binfos = minfos |> List.choose (function Choice1Of2 (_, x) -> Some x | _ -> None)
        let minfos = minfos |> List.choose (function Choice2Of2 x -> Some x | _ -> None)
        
        (TMDefRec(isRec, tycons, mbinds, m), 
         notlazy { ValInfos = ValInfos(List.map2 (fun bind binfo -> mkValBind bind (mkValInfo binfo bind.Var)) binds binfos) 
                   ModuleOrNamespaceInfos = NameMap.ofList minfos}), 
        (env, bindInfosColl)

    | TMAbstract mexpr -> 
        let mexpr, info = OptimizeModuleExpr cenv env mexpr
        let env = BindValsInModuleOrNamespace cenv info env
        (TMAbstract mexpr, info), (env, bindInfosColl)

    | TMDefLet(bind, m) ->
        let ((bindR, binfo) as bindInfo), env = OptimizeBinding cenv false env bind
        (TMDefLet(bindR, m), 
         notlazy { ValInfos=ValInfos [mkValBind bind (mkValInfo binfo bind.Var)] 
                   ModuleOrNamespaceInfos = NameMap.empty }), 
        (env, ([bindInfo] :: bindInfosColl))

    | TMDefDo(e, m) ->
        let (e, _einfo) = OptimizeExpr cenv env e
        (TMDefDo(e, m), EmptyModuleInfo), 
        (env, bindInfosColl)

    | TMDefs defs -> 
        let (defs, info), (env, bindInfosColl) = OptimizeModuleDefs cenv (env, bindInfosColl) defs 
        (TMDefs defs, info), (env, bindInfosColl)

and OptimizeModuleBindings cenv (env, bindInfosColl) xs = List.mapFold (OptimizeModuleBinding cenv) (env, bindInfosColl) xs

and OptimizeModuleBinding cenv (env, bindInfosColl) x = 
    match x with
    | ModuleOrNamespaceBinding.Binding bind -> 
        let ((bindR, binfo) as bindInfo), env = OptimizeBinding cenv true env bind
        (ModuleOrNamespaceBinding.Binding bindR, Choice1Of2 (bindR, binfo)), (env, [ bindInfo ] :: bindInfosColl)
    | ModuleOrNamespaceBinding.Module(mspec, def) ->
        let id = mspec.Id
        let (def, info), (_, bindInfosColl) = OptimizeModuleDef cenv (env, bindInfosColl) def 
        let env = BindValsInModuleOrNamespace cenv info env
        (ModuleOrNamespaceBinding.Module(mspec, def), Choice2Of2 (id.idText, info)), 
        (env, bindInfosColl)

and OptimizeModuleDefs cenv (env, bindInfosColl) defs = 
    let defs, (env, bindInfosColl) = List.mapFold (OptimizeModuleDef cenv) (env, bindInfosColl) defs
    let defs, minfos = List.unzip defs
    (defs, UnionOptimizationInfos minfos), (env, bindInfosColl)
   
and OptimizeImplFileInternal cenv env isIncrementalFragment hidden (TImplFile (qname, pragmas, mexpr, hasExplicitEntryPoint, isScript, anonRecdTypes)) =
    let env, mexprR, minfo = 
        match mexpr with 
        // FSI: FSI compiles everything as if you're typing incrementally into one module 
        // This means the fragment is not truly a constrained module as later fragments will be typechecked 
        // against the internals of the module rather than the externals. Furthermore it would be wrong to apply 
        // optimizations that did lots of reorganizing stuff to the internals of a module should we ever implement that. 
        | ModuleOrNamespaceExprWithSig(mty, def, m) when isIncrementalFragment -> 
            let (def, minfo), (env, _bindInfosColl) = OptimizeModuleDef cenv (env, []) def 
            env, ModuleOrNamespaceExprWithSig(mty, def, m), minfo
        | _ -> 
            let mexprR, minfo = OptimizeModuleExpr cenv env mexpr
            let env = BindValsInModuleOrNamespace cenv minfo env
            let env = { env with localExternalVals=env.localExternalVals.MarkAsCollapsible() } // take the chance to flatten to a dictionary
            env, mexprR, minfo

    let hidden = ComputeHidingInfoAtAssemblyBoundary mexpr.Type hidden

    let minfo = AbstractLazyModulInfoByHiding true hidden minfo
    env, TImplFile (qname, pragmas, mexprR, hasExplicitEntryPoint, isScript, anonRecdTypes), minfo, hidden

/// Entry point
let OptimizeImplFile (settings, ccu, tcGlobals, tcVal, importMap, optEnv, isIncrementalFragment, emitTailcalls, hidden, mimpls) =
    let cenv = 
        { settings=settings
          scope=ccu 
          TcVal = tcVal
          g=tcGlobals 
          amap=importMap
          optimizing=true
          localInternalVals=Dictionary<Stamp, ValInfo>(10000)
          emitTailcalls=emitTailcalls
          casApplied=new Dictionary<Stamp, bool>() }

    let (env, _, _, _ as results) = OptimizeImplFileInternal cenv optEnv isIncrementalFragment hidden mimpls  

    let optimizeDuringCodeGen disableMethodSplitting expr =
        let env = { env with disableMethodSplitting = env.disableMethodSplitting || disableMethodSplitting }
        OptimizeExpr cenv env expr |> fst

    results, optimizeDuringCodeGen


/// Pickle to stable format for cross-module optimization data
let rec p_ExprValueInfo x st =
    match x with 
    | ConstValue (c, ty) ->
        p_byte 0 st
        p_tup2 p_const p_ty (c, ty) st 
    | UnknownValue ->
        p_byte 1 st
    | ValValue (a, b) ->
        p_byte 2 st
        p_tup2 (p_vref "optval") p_ExprValueInfo (a, b) st
    | TupleValue a ->
        p_byte 3 st
        p_array p_ExprValueInfo a st
    | UnionCaseValue (a, b) ->
        p_byte 4 st
        p_tup2 p_ucref (p_array p_ExprValueInfo) (a, b) st
    | CurriedLambdaValue (_, b, c, d, e) ->
        p_byte 5 st
        p_tup4 p_int p_int p_expr p_ty (b, c, d, e) st
    | ConstExprValue (a, b) ->
        p_byte 6 st
        p_tup2 p_int p_expr (a, b) st
    | RecdValue (tcref, a) -> 
        p_byte 7 st
        p_tcref "opt data" tcref st
        p_array p_ExprValueInfo a st
    | SizeValue (_adepth, a) ->
        p_ExprValueInfo a st

and p_ValInfo (v: ValInfo) st = 
    p_ExprValueInfo v.ValExprInfo st
    p_bool v.ValMakesNoCriticalTailcalls st

and p_ModuleInfo x st = 
    p_array (p_tup2 (p_vref "opttab") p_ValInfo) (x.ValInfos.Entries |> Seq.toArray) st
    p_namemap p_LazyModuleInfo x.ModuleOrNamespaceInfos st

and p_LazyModuleInfo x st = 
    p_lazy p_ModuleInfo x st

let p_CcuOptimizationInfo x st = p_LazyModuleInfo x st

let rec u_ExprInfo st =
    let rec loop st =
        let tag = u_byte st
        match tag with
        | 0 -> u_tup2 u_const u_ty st |> (fun (c, ty) -> ConstValue(c, ty))
        | 1 -> UnknownValue
        | 2 -> u_tup2 u_vref loop st |> (fun (a, b) -> ValValue (a, b))
        | 3 -> u_array loop st |> (fun a -> TupleValue a)
        | 4 -> u_tup2 u_ucref (u_array loop) st |> (fun (a, b) -> UnionCaseValue (a, b))
        | 5 -> u_tup4 u_int u_int u_expr u_ty st |> (fun (b, c, d, e) -> CurriedLambdaValue (newUnique(), b, c, d, e))
        | 6 -> u_tup2 u_int u_expr st |> (fun (a, b) -> ConstExprValue (a, b))
        | 7 -> u_tup2 u_tcref (u_array loop) st |> (fun (a, b) -> RecdValue (a, b))
        | _ -> failwith "loop"
    MakeSizedValueInfo (loop st) (* calc size of unpicked ExprValueInfo *)

and u_ValInfo st = 
    let a, b = u_tup2 u_ExprInfo u_bool st
    { ValExprInfo=a; ValMakesNoCriticalTailcalls = b } 

and u_ModuleInfo st = 
    let a, b = u_tup2 (u_array (u_tup2 u_vref u_ValInfo)) (u_namemap u_LazyModuleInfo) st
    { ValInfos= ValInfos a; ModuleOrNamespaceInfos=b}

and u_LazyModuleInfo st = u_lazy u_ModuleInfo st

let u_CcuOptimizationInfo st = u_LazyModuleInfo st
