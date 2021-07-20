// Copyright (c) Microsoft Corporation. All Rights Reserved. See License.txt in the project root for license information.

/// The typechecker. Left-to-right constrained type checking
/// with generalization at appropriate points.
module internal FSharp.Compiler.CheckExpressions

open System
open System.Collections.Generic

open Internal.Utilities
open Internal.Utilities.Collections
open Internal.Utilities.Library
open Internal.Utilities.Library.Extras
open Internal.Utilities.Library.ResultOrException
open Internal.Utilities.Rational
open FSharp.Compiler
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AbstractIL
open FSharp.Compiler.AccessibilityLogic
open FSharp.Compiler.AttributeChecking
open FSharp.Compiler.CompilerGlobalState
open FSharp.Compiler.ConstraintSolver
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Features
open FSharp.Compiler.Infos
open FSharp.Compiler.InfoReader
open FSharp.Compiler.MethodCalls
open FSharp.Compiler.MethodOverrides
open FSharp.Compiler.NameResolution
open FSharp.Compiler.PatternMatchCompilation
open FSharp.Compiler.Syntax
open FSharp.Compiler.Syntax.PrettyNaming
open FSharp.Compiler.SyntaxTreeOps
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Position
open FSharp.Compiler.Text.Range
open FSharp.Compiler.Xml
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.TypeRelations

#if !NO_EXTENSIONTYPING
open FSharp.Compiler.ExtensionTyping
#endif

//-------------------------------------------------------------------------
// Helpers that should be elsewhere
//-------------------------------------------------------------------------

let mkNilListPat (g: TcGlobals) m ty = TPat_unioncase(g.nil_ucref, [ty], [], m)

let mkConsListPat (g: TcGlobals) ty ph pt = TPat_unioncase(g.cons_ucref, [ty], [ph;pt], unionRanges ph.Range pt.Range)

//-------------------------------------------------------------------------
// Errors.
//-------------------------------------------------------------------------

exception BakedInMemberConstraintName of string * range
exception FunctionExpected of DisplayEnv * TType * range
exception NotAFunction of DisplayEnv * TType * range * range
exception NotAFunctionButIndexer of DisplayEnv * TType * string option * range * range
exception Recursion of DisplayEnv * Ident * TType * TType * range
exception RecursiveUseCheckedAtRuntime of DisplayEnv * ValRef * range
exception LetRecEvaluatedOutOfOrder of DisplayEnv * ValRef * ValRef * range
exception LetRecCheckedAtRuntime of range
exception LetRecUnsound of DisplayEnv * ValRef list * range
exception TyconBadArgs of DisplayEnv * TyconRef * int * range
exception UnionCaseWrongArguments of DisplayEnv * int * int * range
exception UnionCaseWrongNumberOfArgs of DisplayEnv * int * int * range
exception FieldsFromDifferentTypes of DisplayEnv * RecdFieldRef * RecdFieldRef * range
exception FieldGivenTwice of DisplayEnv * RecdFieldRef * range
exception MissingFields of string list * range
exception FunctionValueUnexpected of DisplayEnv * TType * range
exception UnitTypeExpected of DisplayEnv * TType * range
exception UnitTypeExpectedWithEquality of DisplayEnv * TType * range
exception UnitTypeExpectedWithPossibleAssignment of DisplayEnv * TType * bool * string * range
exception UnitTypeExpectedWithPossiblePropertySetter of DisplayEnv * TType * string * string * range
exception UnionPatternsBindDifferentNames of range
exception VarBoundTwice of Ident
exception ValueRestriction of DisplayEnv * InfoReader * bool * Val * Typar * range
exception ValNotMutable of DisplayEnv * ValRef * range
exception ValNotLocal of DisplayEnv * ValRef * range
exception InvalidRuntimeCoercion of DisplayEnv * TType * TType * range
exception IndeterminateRuntimeCoercion of DisplayEnv * TType * TType * range
exception IndeterminateStaticCoercion of DisplayEnv * TType * TType * range
exception RuntimeCoercionSourceSealed of DisplayEnv * TType * range
exception CoercionTargetSealed of DisplayEnv * TType * range
exception UpcastUnnecessary of range
exception TypeTestUnnecessary of range
exception StaticCoercionShouldUseBox of DisplayEnv * TType * TType * range
exception SelfRefObjCtor of bool * range
exception VirtualAugmentationOnNullValuedType of range
exception NonVirtualAugmentationOnNullValuedType of range
exception UseOfAddressOfOperator of range
exception DeprecatedThreadStaticBindingWarning of range
exception IntfImplInIntrinsicAugmentation of range
exception IntfImplInExtrinsicAugmentation of range
exception OverrideInIntrinsicAugmentation of range
exception OverrideInExtrinsicAugmentation of range
exception NonUniqueInferredAbstractSlot of TcGlobals * DisplayEnv * string * MethInfo * MethInfo * range
exception StandardOperatorRedefinitionWarning of string * range
exception InvalidInternalsVisibleToAssemblyName of (*badName*)string * (*fileName option*) string option

/// Represents information about the initialization field used to check that object constructors
/// have completed before fields are accessed.
type SafeInitData =
    | SafeInitField of RecdFieldRef * RecdField
    | NoSafeInitInfo

/// Represents information about object constructors
type CtorInfo =
    { /// Object model constructors have a very specific form to satisfy .NET limitations.
      /// For "new = \arg. { new C with ... }"
      ///     ctor = 3 indicates about to type check "\arg. (body)",
      ///     ctor = 2 indicates about to type check "body"
      ///     ctor = 1 indicates actually type checking the body expression
      /// 0 indicates everywhere else, including auxiliary expressions such e1 in "let x = e1 in { new ... }"
      /// REVIEW: clean up this rather odd approach ...
      ctorShapeCounter: int

      /// A handle to the ref cell to hold results of 'this' for 'type X() as x = ...' and 'new() as x = ...' constructs
      /// in case 'x' is used in the arguments to the 'inherits' call.
      safeThisValOpt: Val option

      /// A handle to the boolean ref cell to hold success of initialized 'this' for 'type X() as x = ...' constructs
      safeInitInfo: SafeInitData

      /// Is the an implicit constructor or an explicit one?
      ctorIsImplicit: bool
    }

/// Represents an item in the environment that may restrict the automatic generalization of later
/// declarations because it refers to type inference variables. As type inference progresses
/// these type inference variables may get solved.
[<NoEquality; NoComparison; Sealed>]
type UngeneralizableItem(computeFreeTyvars: (unit -> FreeTyvars)) =

    // Flag is for: have we determined that this item definitely has
    // no free type inference variables? This implies that
    //   (a) it will _never_ have any free type inference variables as further constraints are added to the system.
    //   (b) its set of FreeTycons will not change as further constraints are added to the system
    let mutable willNeverHaveFreeTypars = false

    // If WillNeverHaveFreeTypars then we can cache the computation of FreeTycons, since they are invariant.
    let mutable cachedFreeLocalTycons = emptyFreeTycons

    // If WillNeverHaveFreeTypars then we can cache the computation of FreeTraitSolutions, since they are invariant.
    let mutable cachedFreeTraitSolutions = emptyFreeLocals

    member item.GetFreeTyvars() =
        let fvs = computeFreeTyvars()
        if fvs.FreeTypars.IsEmpty then
            willNeverHaveFreeTypars <- true
            cachedFreeLocalTycons <- fvs.FreeTycons
            cachedFreeTraitSolutions <- fvs.FreeTraitSolutions
        fvs

    member item.WillNeverHaveFreeTypars = willNeverHaveFreeTypars

    member item.CachedFreeLocalTycons = cachedFreeLocalTycons

    member item.CachedFreeTraitSolutions = cachedFreeTraitSolutions

/// Represents the type environment at a particular scope. Includes the name
/// resolution environment, the ungeneralizable items from earlier in the scope
/// and other information about the scope.
[<NoEquality; NoComparison>]
type TcEnv =
    { /// Name resolution information
      eNameResEnv: NameResolutionEnv

      /// The list of items in the environment that may contain free inference
      /// variables (which may not be generalized). The relevant types may
      /// change as a result of inference equations being asserted, hence may need to
      /// be recomputed.
      eUngeneralizableItems: UngeneralizableItem list

      // Two (!) versions of the current module path
      // These are used to:
      //    - Look up the appropriate point in the corresponding signature
      //      see if an item is public or not
      //    - Change fslib canonical module type to allow compiler references to these items
      //    - Record the cpath for concrete modul_specs, tycon_specs and excon_specs so they can cache their generated IL representation where necessary
      //    - Record the pubpath of public, concrete {val, tycon, modul, excon}_specs.
      //      This information is used mainly when building non-local references
      //      to public items.
      //
      // Of the two, 'ePath' is the one that's barely used. It's only
      // used by UpdateAccModuleOrNamespaceType to modify the CCU while compiling FSharp.Core
      ePath: Ident list

      eCompPath: CompilationPath

      eAccessPath: CompilationPath

      /// This field is computed from other fields, but we amortize the cost of computing it.
      eAccessRights: AccessorDomain

      /// Internals under these should be accessible
      eInternalsVisibleCompPaths: CompilationPath list

      /// Mutable accumulator for the current module type
      eModuleOrNamespaceTypeAccumulator: ModuleOrNamespaceType ref

      /// Context information for type checker
      eContextInfo: ContextInfo

      /// Here Some tcref indicates we can access protected members in all super types
      eFamilyType: TyconRef option

      // Information to enforce special restrictions on valid expressions
      // for .NET constructors.
      eCtorInfo: CtorInfo option

      eCallerMemberName: string option

      // Active arg infos in iterated lambdas , allowing us to determine the attributes of arguments
      eLambdaArgInfos: ArgReprInfo list list
    }

    member tenv.DisplayEnv = tenv.eNameResEnv.DisplayEnv

    member tenv.NameEnv = tenv.eNameResEnv

    member tenv.AccessRights = tenv.eAccessRights

    override tenv.ToString() = "TcEnv(...)"

/// Compute the available access rights from a particular location in code
let ComputeAccessRights eAccessPath eInternalsVisibleCompPaths eFamilyType =
    AccessibleFrom (eAccessPath :: eInternalsVisibleCompPaths, eFamilyType)

//-------------------------------------------------------------------------
// Helpers related to determining if we're in a constructor and/or a class
// that may be able to access "protected" members.
//-------------------------------------------------------------------------

let InitialExplicitCtorInfo (safeThisValOpt, safeInitInfo) =
    { ctorShapeCounter = 3
      safeThisValOpt = safeThisValOpt
      safeInitInfo = safeInitInfo
      ctorIsImplicit = false}

let InitialImplicitCtorInfo () =
    { ctorShapeCounter = 0
      safeThisValOpt = None
      safeInitInfo = NoSafeInitInfo
      ctorIsImplicit = true }

let EnterFamilyRegion tcref env =
    let eFamilyType = Some tcref
    { env with
        eAccessRights = ComputeAccessRights env.eAccessPath env.eInternalsVisibleCompPaths eFamilyType // update this computed field
        eFamilyType = eFamilyType }

let ExitFamilyRegion env =
    let eFamilyType = None
    match env.eFamilyType with
    | None -> env // optimization to avoid reallocation
    | _ ->
        { env with
            eAccessRights = ComputeAccessRights env.eAccessPath env.eInternalsVisibleCompPaths eFamilyType // update this computed field
            eFamilyType = eFamilyType }

let AreWithinCtorShape env = match env.eCtorInfo with None -> false | Some ctorInfo -> ctorInfo.ctorShapeCounter > 0
let AreWithinImplicitCtor env = match env.eCtorInfo with None -> false | Some ctorInfo -> ctorInfo.ctorIsImplicit
let GetCtorShapeCounter env = match env.eCtorInfo with None -> 0 | Some ctorInfo -> ctorInfo.ctorShapeCounter
let GetRecdInfo env = match env.eCtorInfo with None -> RecdExpr | Some ctorInfo -> if ctorInfo.ctorShapeCounter = 1 then RecdExprIsObjInit else RecdExpr

let AdjustCtorShapeCounter f env = {env with eCtorInfo = Option.map (fun ctorInfo -> { ctorInfo with ctorShapeCounter = f ctorInfo.ctorShapeCounter }) env.eCtorInfo }
let ExitCtorShapeRegion env = AdjustCtorShapeCounter (fun _ -> 0) env

/// Add a type to the TcEnv, i.e. register it as ungeneralizable.
let addFreeItemOfTy ty eUngeneralizableItems =
    let fvs = freeInType CollectAllNoCaching ty
    if isEmptyFreeTyvars fvs then eUngeneralizableItems
    else UngeneralizableItem(fun () -> freeInType CollectAllNoCaching ty) :: eUngeneralizableItems

/// Add the contents of a module type to the TcEnv, i.e. register the contents as ungeneralizable.
/// Add a module type to the TcEnv, i.e. register it as ungeneralizable.
let addFreeItemOfModuleTy mtyp eUngeneralizableItems =
    let fvs = freeInModuleTy mtyp
    if isEmptyFreeTyvars fvs then eUngeneralizableItems
    else UngeneralizableItem(fun () -> freeInModuleTy mtyp) :: eUngeneralizableItems

/// Add a table of values to the name resolution environment.
let AddValMapToNameEnv g vs nenv =
    NameMap.foldBackRange (fun v nenv -> AddValRefToNameEnv g nenv (mkLocalValRef v)) vs nenv

/// Add a list of values to the name resolution environment.
let AddValListToNameEnv g vs nenv =
    List.foldBack (fun v nenv -> AddValRefToNameEnv g nenv (mkLocalValRef v)) vs nenv

/// Add a local value to TcEnv
let AddLocalValPrimitive g (v: Val) env =
    { env with
        eNameResEnv = AddValRefToNameEnv g env.eNameResEnv (mkLocalValRef v)
        eUngeneralizableItems = addFreeItemOfTy v.Type env.eUngeneralizableItems }

/// Add a table of local values to TcEnv
let AddLocalValMap g tcSink scopem (vals: Val NameMap) env =
    let env =
        if vals.IsEmpty then
            env
        else
            { env with
                eNameResEnv = AddValMapToNameEnv g vals env.eNameResEnv
                eUngeneralizableItems = NameMap.foldBackRange (typeOfVal >> addFreeItemOfTy) vals env.eUngeneralizableItems }
    CallEnvSink tcSink (scopem, env.NameEnv, env.AccessRights)
    env

/// Add a list of local values to TcEnv and report them to the sink
let AddLocalVals g tcSink scopem (vals: Val list) env =
    let env =
        if isNil vals then
            env
        else
            { env with
                eNameResEnv = AddValListToNameEnv g vals env.eNameResEnv
                eUngeneralizableItems = List.foldBack (typeOfVal >> addFreeItemOfTy) vals env.eUngeneralizableItems }
    CallEnvSink tcSink (scopem, env.NameEnv, env.AccessRights)
    env

/// Add a local value to TcEnv and report it to the sink
let AddLocalVal g tcSink scopem v env =
    let env = { env with
                    eNameResEnv = AddValRefToNameEnv g env.eNameResEnv (mkLocalValRef v)
                    eUngeneralizableItems = addFreeItemOfTy v.Type env.eUngeneralizableItems }
    CallEnvSink tcSink (scopem, env.NameEnv, env.eAccessRights)
    env

/// Add a set of explicitly declared type parameters as being available in the TcEnv
let AddDeclaredTypars check typars env =
    if isNil typars then env else
    let env = { env with eNameResEnv = AddDeclaredTyparsToNameEnv check env.eNameResEnv typars }
    { env with eUngeneralizableItems = List.foldBack (mkTyparTy >> addFreeItemOfTy) typars env.eUngeneralizableItems }

/// Environment of implicitly scoped type parameters, e.g. 'a in "(x: 'a)"

type UnscopedTyparEnv = UnscopedTyparEnv of NameMap<Typar>

let emptyUnscopedTyparEnv: UnscopedTyparEnv = UnscopedTyparEnv Map.empty

let AddUnscopedTypar n p (UnscopedTyparEnv tab) = UnscopedTyparEnv (Map.add n p tab)

let TryFindUnscopedTypar n (UnscopedTyparEnv tab) = Map.tryFind n tab

let HideUnscopedTypars typars (UnscopedTyparEnv tab) =
    UnscopedTyparEnv (List.fold (fun acc (tp: Typar) -> Map.remove tp.Name acc) tab typars)

/// Represents the compilation environment for typechecking a single file in an assembly.
[<NoEquality; NoComparison>]
type TcFileState =
    { g: TcGlobals

      /// Push an entry every time a recursive value binding is used,
      /// in order to be able to fix up recursive type applications as
      /// we infer type parameters
      mutable recUses: ValMultiMap<(Expr ref * range * bool)>

      /// Checks to run after all inference is complete.
      mutable postInferenceChecks: ResizeArray<unit -> unit>

      /// Set to true if this file causes the creation of generated provided types.
      mutable createsGeneratedProvidedTypes: bool

      /// Are we in a script? if so relax the reporting of discarded-expression warnings at the top level
      isScript: bool

      /// Environment needed to convert IL types to F# types in the importer.
      amap: Import.ImportMap

      /// Used to generate new syntactic argument names in post-parse syntactic processing
      synArgNameGenerator: SynArgNameGenerator

      tcSink: TcResultsSink

      /// Holds a reference to the component being compiled.
      /// This field is very rarely used (mainly when fixing up forward references to fslib.
      topCcu: CcuThunk

      /// Holds the current inference constraints
      css: ConstraintSolverState

      /// Are we compiling the signature of a module from fslib?
      compilingCanonicalFslibModuleType: bool

      /// Is this a .fsi file?
      isSig: bool

      /// Does this .fs file have a .fsi file?
      haveSig: bool

      /// Used to generate names
      niceNameGen: NiceNameGenerator

      /// Used to read and cache information about types and members
      infoReader: InfoReader

      /// Used to resolve names
      nameResolver: NameResolver

      /// The set of active conditional defines. The value is None when conditional erasure is disabled in tooling.
      conditionalDefines: string list option

      isInternalTestSpanStackReferring: bool
      // forward call
      TcSequenceExpressionEntry: TcFileState -> TcEnv -> TType -> UnscopedTyparEnv -> bool * bool ref * SynExpr -> range -> Expr * UnscopedTyparEnv
      // forward call
      TcArrayOrListSequenceExpression: TcFileState -> TcEnv -> TType -> UnscopedTyparEnv -> bool * SynExpr -> range -> Expr * UnscopedTyparEnv
      // forward call
      TcComputationExpression: TcFileState -> TcEnv -> TType -> UnscopedTyparEnv -> range * Expr * TType * SynExpr -> Expr * UnscopedTyparEnv
    }

    /// Create a new compilation environment
    static member Create
         (g, isScript, niceNameGen, amap, topCcu, isSig, haveSig, conditionalDefines, tcSink, tcVal, isInternalTestSpanStackReferring,
          tcSequenceExpressionEntry, tcArrayOrListSequenceExpression, tcComputationExpression) =
        let infoReader = new InfoReader(g, amap)
        let instantiationGenerator m tpsorig = ConstraintSolver.FreshenTypars m tpsorig
        let nameResolver = new NameResolver(g, amap, infoReader, instantiationGenerator)
        { g = g
          amap = amap
          recUses = ValMultiMap<_>.Empty
          postInferenceChecks = ResizeArray()
          createsGeneratedProvidedTypes = false
          topCcu = topCcu
          isScript = isScript
          css = ConstraintSolverState.New(g, amap, infoReader, tcVal)
          infoReader = infoReader
          tcSink = tcSink
          nameResolver = nameResolver
          niceNameGen = niceNameGen
          synArgNameGenerator = SynArgNameGenerator()
          isSig = isSig
          haveSig = haveSig
          compilingCanonicalFslibModuleType = (isSig || not haveSig) && g.compilingFslib
          conditionalDefines = conditionalDefines
          isInternalTestSpanStackReferring = isInternalTestSpanStackReferring
          TcSequenceExpressionEntry = tcSequenceExpressionEntry
          TcArrayOrListSequenceExpression = tcArrayOrListSequenceExpression
          TcComputationExpression = tcComputationExpression
        }

    override _.ToString() = "<cenv>"

type cenv = TcFileState

let CopyAndFixupTypars m rigid tpsorig =
    ConstraintSolver.FreshenAndFixupTypars m rigid [] [] tpsorig

let UnifyTypes cenv (env: TcEnv) m actualTy expectedTy =
    ConstraintSolver.AddCxTypeEqualsType env.eContextInfo env.DisplayEnv cenv.css m (tryNormalizeMeasureInType cenv.g actualTy) (tryNormalizeMeasureInType cenv.g expectedTy)

/// Make an environment suitable for a module or namespace. Does not create a new accumulator but uses one we already have/
let MakeInnerEnvWithAcc addOpenToNameEnv env nm mtypeAcc modKind =
    let path = env.ePath @ [nm]
    let cpath = env.eCompPath.NestedCompPath nm.idText modKind
    { env with
        ePath = path
        eCompPath = cpath
        eAccessPath = cpath
        eAccessRights = ComputeAccessRights cpath env.eInternalsVisibleCompPaths env.eFamilyType // update this computed field
        eNameResEnv =
            if addOpenToNameEnv then
                { env.NameEnv with eDisplayEnv = env.DisplayEnv.AddOpenPath (pathOfLid path) }
            else
                env.NameEnv
        eModuleOrNamespaceTypeAccumulator = mtypeAcc }

/// Make an environment suitable for a module or namespace, creating a new accumulator.
let MakeInnerEnv addOpenToNameEnv env nm modKind =
    // Note: here we allocate a new module type accumulator
    let mtypeAcc = ref (Construct.NewEmptyModuleOrNamespaceType modKind)
    MakeInnerEnvWithAcc addOpenToNameEnv env nm mtypeAcc modKind, mtypeAcc

/// Make an environment suitable for processing inside a type definition
let MakeInnerEnvForTyconRef env tcref isExtrinsicExtension =
    if isExtrinsicExtension then
        // Extension members don't get access to protected stuff
        env
    else
        // Regular members get access to protected stuff
        let env = EnterFamilyRegion tcref env
        // Note: assumes no nesting
        let eAccessPath = env.eCompPath.NestedCompPath tcref.LogicalName ModuleOrType
        { env with
             eAccessRights = ComputeAccessRights eAccessPath env.eInternalsVisibleCompPaths env.eFamilyType // update this computed field
             eAccessPath = eAccessPath }

/// Make an environment suitable for processing inside a member definition
let MakeInnerEnvForMember env (v: Val) =
    match v.MemberInfo with
    | None -> env
    | Some _ -> MakeInnerEnvForTyconRef env v.MemberApparentEntity v.IsExtensionMember

/// Get the current accumulator for the namespace/module we're in
let GetCurrAccumulatedModuleOrNamespaceType env = !(env.eModuleOrNamespaceTypeAccumulator)

/// Set the current accumulator for the namespace/module we're in, updating the inferred contents
let SetCurrAccumulatedModuleOrNamespaceType env x = env.eModuleOrNamespaceTypeAccumulator := x

/// Set up the initial environment accounting for the enclosing "namespace X.Y.Z" definition
let LocateEnv ccu env enclosingNamespacePath =
    let cpath = compPathOfCcu ccu
    let env =
        {env with
            ePath = []
            eCompPath = cpath
            eAccessPath = cpath
            // update this computed field
            eAccessRights = ComputeAccessRights cpath env.eInternalsVisibleCompPaths env.eFamilyType }
    let env = List.fold (fun env id -> MakeInnerEnv false env id Namespace |> fst) env enclosingNamespacePath
    let env = { env with eNameResEnv = { env.NameEnv with eDisplayEnv = env.DisplayEnv.AddOpenPath (pathOfLid env.ePath) } }
    env


//-------------------------------------------------------------------------
// Helpers for unification
//-------------------------------------------------------------------------

/// When the context is matching the oldRange then this function shrinks it to newRange.
/// This can be used to change context over no-op expressions like parens.
let ShrinkContext env oldRange newRange =
    match env.eContextInfo with
    | ContextInfo.NoContext
    | ContextInfo.RecordFields
    | ContextInfo.TupleInRecordFields
    | ContextInfo.ReturnInComputationExpression
    | ContextInfo.YieldInComputationExpression
    | ContextInfo.RuntimeTypeTest _
    | ContextInfo.DowncastUsedInsteadOfUpcast _
    | ContextInfo.SequenceExpression _ ->
        env
    | ContextInfo.CollectionElement (b,m) ->
        if not (Range.equals m oldRange) then env else
        { env with eContextInfo = ContextInfo.CollectionElement(b,newRange) }
    | ContextInfo.FollowingPatternMatchClause m ->
        if not (Range.equals m oldRange) then env else
        { env with eContextInfo = ContextInfo.FollowingPatternMatchClause newRange }
    | ContextInfo.PatternMatchGuard m ->
        if not (Range.equals m oldRange) then env else
        { env with eContextInfo = ContextInfo.PatternMatchGuard newRange }
    | ContextInfo.IfExpression m ->
        if not (Range.equals m oldRange) then env else
        { env with eContextInfo = ContextInfo.IfExpression newRange }
    | ContextInfo.OmittedElseBranch m ->
        if not (Range.equals m oldRange) then env else
        { env with eContextInfo = ContextInfo.OmittedElseBranch newRange }
    | ContextInfo.ElseBranchResult m ->
        if not (Range.equals m oldRange) then env else
        { env with eContextInfo = ContextInfo.ElseBranchResult newRange }

/// Optimized unification routine that avoids creating new inference
/// variables unnecessarily
let UnifyRefTupleType contextInfo cenv denv m ty ps =
    let ptys =
        if isRefTupleTy cenv.g ty then
            let ptys = destRefTupleTy cenv.g ty
            if List.length ps = List.length ptys then ptys
            else NewInferenceTypes ps
        else NewInferenceTypes ps

    let contextInfo =
        match contextInfo with
        | ContextInfo.RecordFields -> ContextInfo.TupleInRecordFields
        | _ -> contextInfo

    AddCxTypeEqualsType contextInfo denv cenv.css m ty (TType_tuple (tupInfoRef, ptys))
    ptys

/// Allow the inference of structness from the known type, e.g.
///    let (x: struct (int * int)) = (3,4)
let UnifyTupleTypeAndInferCharacteristics contextInfo cenv denv m knownTy isExplicitStruct ps =
    let tupInfo, ptys =
        if isAnyTupleTy cenv.g knownTy then
            let tupInfo, ptys = destAnyTupleTy cenv.g knownTy
            let tupInfo = (if isExplicitStruct then tupInfoStruct else tupInfo)
            let ptys =
                if List.length ps = List.length ptys then ptys
                else NewInferenceTypes ps
            tupInfo, ptys
        else
            mkTupInfo isExplicitStruct, NewInferenceTypes ps

    let contextInfo =
        match contextInfo with
        | ContextInfo.RecordFields -> ContextInfo.TupleInRecordFields
        | _ -> contextInfo

    let ty2 = TType_tuple (tupInfo, ptys)
    AddCxTypeEqualsType contextInfo denv cenv.css m knownTy ty2
    tupInfo, ptys

// Allow inference of assembly-affinity and structness from the known type - even from another assembly. This is a rule of
// the language design and allows effective cross-assembly use of anonymous types in some limited circumstances.
let UnifyAnonRecdTypeAndInferCharacteristics contextInfo cenv denv m ty isExplicitStruct unsortedNames =
    let anonInfo, ptys =
        match tryDestAnonRecdTy cenv.g ty with
        | ValueSome (anonInfo, ptys) ->
            // Note: use the assembly of the known type, not the current assembly
            // Note: use the structness of the known type, unless explicit
            // Note: use the names of our type, since they are always explicit
            let tupInfo = (if isExplicitStruct then tupInfoStruct else anonInfo.TupInfo)
            let anonInfo = AnonRecdTypeInfo.Create(anonInfo.Assembly, tupInfo, unsortedNames)
            let ptys =
                if List.length ptys = Array.length unsortedNames then ptys
                else NewInferenceTypes (Array.toList anonInfo.SortedNames)
            anonInfo, ptys
        | ValueNone ->
            // Note: no known anonymous record type - use our assembly
            let anonInfo = AnonRecdTypeInfo.Create(cenv.topCcu, mkTupInfo isExplicitStruct, unsortedNames)
            anonInfo, NewInferenceTypes (Array.toList anonInfo.SortedNames)
    let ty2 = TType_anon (anonInfo, ptys)
    AddCxTypeEqualsType contextInfo denv cenv.css m ty ty2
    anonInfo, ptys


/// Optimized unification routine that avoids creating new inference
/// variables unnecessarily
let UnifyFunctionTypeUndoIfFailed cenv denv m ty =
    match tryDestFunTy cenv.g ty with
    | ValueNone ->
        let domainTy = NewInferenceType ()
        let resultTy = NewInferenceType ()
        if AddCxTypeEqualsTypeUndoIfFailed denv cenv.css m ty (domainTy --> resultTy) then
            ValueSome(domainTy, resultTy)
        else
            ValueNone
    | r -> r

/// Optimized unification routine that avoids creating new inference
/// variables unnecessarily
let UnifyFunctionType extraInfo cenv denv mFunExpr ty =
    match UnifyFunctionTypeUndoIfFailed cenv denv mFunExpr ty with
    | ValueSome res -> res
    | ValueNone ->
        match extraInfo with
        | Some argm -> error (NotAFunction(denv, ty, mFunExpr, argm))
        | None -> error (FunctionExpected(denv, ty, mFunExpr))

let ReportImplicitlyIgnoredBoolExpression denv m ty expr =
    let checkExpr m expr =
        match expr with
        | Expr.App (Expr.Val (vf, _, _), _, _, exprs, _) when vf.LogicalName = opNameEquals ->
            match exprs with
            | Expr.App (Expr.Val (propRef, _, _), _, _, Expr.Val (vf, _, _) :: _, _) :: _ ->
                if propRef.IsPropertyGetterMethod then
                    let propertyName = propRef.PropertyName
                    let hasCorrespondingSetter =
                        match propRef.DeclaringEntity with
                        | Parent entityRef ->
                            entityRef.MembersOfFSharpTyconSorted
                            |> List.exists (fun valRef -> valRef.IsPropertySetterMethod && valRef.PropertyName = propertyName)
                        | _ -> false

                    if hasCorrespondingSetter then
                        UnitTypeExpectedWithPossiblePropertySetter (denv, ty, vf.DisplayName, propertyName, m)
                    else
                        UnitTypeExpectedWithEquality (denv, ty, m)
                else
                    UnitTypeExpectedWithEquality (denv, ty, m)
            | Expr.Op (TOp.ILCall (_, _, _, _, _, _, _, ilMethRef, _, _, _), _, Expr.Val (vf, _, _) :: _, _) :: _ when ilMethRef.Name.StartsWithOrdinal("get_") ->
                UnitTypeExpectedWithPossiblePropertySetter (denv, ty, vf.DisplayName, PrettyNaming.ChopPropertyName(ilMethRef.Name), m)
            | Expr.Val (vf, _, _) :: _ ->
                UnitTypeExpectedWithPossibleAssignment (denv, ty, vf.IsMutable, vf.DisplayName, m)
            | _ -> UnitTypeExpectedWithEquality (denv, ty, m)
        | _ -> UnitTypeExpected (denv, ty, m)

    match expr with
    | Expr.Let (_, Expr.Sequential (_, inner, _, _, _), _, _)
    | Expr.Sequential (_, inner, _, _, _) ->
        let rec extractNext expr =
            match expr with
            | Expr.Sequential (_, inner, _, _, _) -> extractNext inner
            | _ -> checkExpr expr.Range expr
        extractNext inner
    | expr -> checkExpr m expr

let UnifyUnitType cenv (env: TcEnv) m ty expr =
    let denv = env.DisplayEnv
    if AddCxTypeEqualsTypeUndoIfFailed denv cenv.css m ty cenv.g.unit_ty then
        true
    else
        let domainTy = NewInferenceType ()
        let resultTy = NewInferenceType ()
        if AddCxTypeEqualsTypeUndoIfFailed denv cenv.css m ty (domainTy --> resultTy) then
            warning (FunctionValueUnexpected(denv, ty, m))
        else
            let reportImplicitlyDiscardError() =
                if typeEquiv cenv.g cenv.g.bool_ty ty then
                    warning (ReportImplicitlyIgnoredBoolExpression denv m ty expr)
                else
                    warning (UnitTypeExpected (denv, ty, m))

            match env.eContextInfo with
            | ContextInfo.SequenceExpression seqTy ->
                let lifted = mkSeqTy cenv.g ty
                if typeEquiv cenv.g seqTy lifted then
                    warning (Error (FSComp.SR.implicitlyDiscardedInSequenceExpression(NicePrint.prettyStringOfTy denv ty), m))
                else
                    if isListTy cenv.g ty || isArrayTy cenv.g ty || typeEquiv cenv.g seqTy ty then
                        warning (Error (FSComp.SR.implicitlyDiscardedSequenceInSequenceExpression(NicePrint.prettyStringOfTy denv ty), m))
                    else
                        reportImplicitlyDiscardError()
            | _ ->
                reportImplicitlyDiscardError()

        false

let TryUnifyUnitTypeWithoutWarning cenv (env:TcEnv) m ty =
    let denv = env.DisplayEnv
    AddCxTypeEqualsTypeUndoIfFailedOrWarnings denv cenv.css m ty cenv.g.unit_ty

// Logically extends System.AttributeTargets
module AttributeTargets =
    let FieldDecl = AttributeTargets.Field ||| AttributeTargets.Property
    let FieldDeclRestricted = AttributeTargets.Field
    let UnionCaseDecl = AttributeTargets.Method ||| AttributeTargets.Property
    let TyconDecl = AttributeTargets.Class ||| AttributeTargets.Interface ||| AttributeTargets.Delegate ||| AttributeTargets.Struct ||| AttributeTargets.Enum
    let ExnDecl = AttributeTargets.Class
    let ModuleDecl = AttributeTargets.Class
    let Top = AttributeTargets.Assembly ||| AttributeTargets.Module ||| AttributeTargets.Method

let ForNewConstructors tcSink (env: TcEnv) mObjTy methodName meths =
    let origItem = Item.CtorGroup(methodName, meths)
    let callSink (item, minst) = CallMethodGroupNameResolutionSink tcSink (mObjTy, env.NameEnv, item, origItem, minst, ItemOccurence.Use, env.AccessRights)
    let sendToSink minst refinedMeths = callSink (Item.CtorGroup(methodName, refinedMeths), minst)
    match meths with
    | [] ->
        AfterResolution.DoNothing
    | [_] ->
        sendToSink emptyTyparInst meths
        AfterResolution.DoNothing
    | _ ->
        AfterResolution.RecordResolution (None, (fun tpinst -> callSink (origItem, tpinst)), (fun (minfo, _, minst) -> sendToSink minst [minfo]), (fun () -> callSink (origItem, emptyTyparInst)))


/// Typecheck rational constant terms in units-of-measure exponents
let rec TcSynRationalConst c =
  match c with
  | SynRationalConst.Integer i -> intToRational i
  | SynRationalConst.Negate c' -> NegRational (TcSynRationalConst c')
  | SynRationalConst.Rational(p, q, _) -> DivRational (intToRational p) (intToRational q)

/// Typecheck constant terms in expressions and patterns
let TcConst cenv ty m env c =
    let rec tcMeasure ms =
        match ms with
        | SynMeasure.One -> Measure.One
        | SynMeasure.Named(tc, m) ->
            let ad = env.eAccessRights
            let _, tcref = ForceRaise(ResolveTypeLongIdent cenv.tcSink cenv.nameResolver ItemOccurence.Use OpenQualified env.eNameResEnv ad tc TypeNameResolutionStaticArgsInfo.DefiniteEmpty PermitDirectReferenceToGeneratedType.No)
            match tcref.TypeOrMeasureKind with
            | TyparKind.Type -> error(Error(FSComp.SR.tcExpectedUnitOfMeasureNotType(), m))
            | TyparKind.Measure -> Measure.Con tcref

        | SynMeasure.Power(ms, exponent, _) -> Measure.RationalPower (tcMeasure ms, TcSynRationalConst exponent)
        | SynMeasure.Product(ms1, ms2, _) -> Measure.Prod(tcMeasure ms1, tcMeasure ms2)
        | SynMeasure.Divide(ms1, ((SynMeasure.Seq (_ :: (_ :: _), _)) as ms2), m) ->
            warning(Error(FSComp.SR.tcImplicitMeasureFollowingSlash(), m))
            Measure.Prod(tcMeasure ms1, Measure.Inv (tcMeasure ms2))
        | SynMeasure.Divide(ms1, ms2, _) ->
            Measure.Prod(tcMeasure ms1, Measure.Inv (tcMeasure ms2))
        | SynMeasure.Seq(mss, _) -> ProdMeasures (List.map tcMeasure mss)
        | SynMeasure.Anon _ -> error(Error(FSComp.SR.tcUnexpectedMeasureAnon(), m))
        | SynMeasure.Var(_, m) -> error(Error(FSComp.SR.tcNonZeroConstantCannotHaveGenericUnit(), m))

    let unif expected = UnifyTypes cenv env m ty expected

    let unifyMeasureArg iszero tcr c =
        let measureTy =
            match c with
            | SynConst.Measure(_, _, SynMeasure.Anon _) ->
              (mkAppTy tcr [TType_measure (Measure.Var (NewAnonTypar (TyparKind.Measure, m, TyparRigidity.Anon, (if iszero then TyparStaticReq.None else TyparStaticReq.HeadType), TyparDynamicReq.No)))])

            | SynConst.Measure(_, _, ms) -> mkAppTy tcr [TType_measure (tcMeasure ms)]
            | _ -> mkAppTy tcr [TType_measure Measure.One]
        unif measureTy

    let expandedMeasurablesEnabled =
        cenv.g.langVersion.SupportsFeature LanguageFeature.ExpandedMeasurables

    match c with
    | SynConst.Unit -> unif cenv.g.unit_ty; Const.Unit
    | SynConst.Bool i -> unif cenv.g.bool_ty; Const.Bool i
    | SynConst.Single f -> unif cenv.g.float32_ty; Const.Single f
    | SynConst.Double f -> unif cenv.g.float_ty; Const.Double f
    | SynConst.Decimal f -> unif (mkAppTy cenv.g.decimal_tcr []); Const.Decimal f
    | SynConst.SByte i -> unif cenv.g.sbyte_ty; Const.SByte i
    | SynConst.Int16 i -> unif cenv.g.int16_ty; Const.Int16 i
    | SynConst.Int32 i -> unif cenv.g.int_ty; Const.Int32 i
    | SynConst.Int64 i -> unif cenv.g.int64_ty; Const.Int64 i
    | SynConst.IntPtr i -> unif cenv.g.nativeint_ty; Const.IntPtr i
    | SynConst.Byte i -> unif cenv.g.byte_ty; Const.Byte i
    | SynConst.UInt16 i -> unif cenv.g.uint16_ty; Const.UInt16 i
    | SynConst.UInt32 i -> unif cenv.g.uint32_ty; Const.UInt32 i
    | SynConst.UInt64 i -> unif cenv.g.uint64_ty; Const.UInt64 i
    | SynConst.UIntPtr i -> unif cenv.g.unativeint_ty; Const.UIntPtr i
    | SynConst.Measure(SynConst.Single f, _, _) -> unifyMeasureArg (f=0.0f) cenv.g.pfloat32_tcr c; Const.Single f
    | SynConst.Measure(SynConst.Double f, _, _) -> unifyMeasureArg (f=0.0) cenv.g.pfloat_tcr c; Const.Double f
    | SynConst.Measure(SynConst.Decimal f, _, _) -> unifyMeasureArg false cenv.g.pdecimal_tcr c; Const.Decimal f
    | SynConst.Measure(SynConst.SByte i, _, _) -> unifyMeasureArg (i=0y) cenv.g.pint8_tcr c; Const.SByte i
    | SynConst.Measure(SynConst.Int16 i, _, _) -> unifyMeasureArg (i=0s) cenv.g.pint16_tcr c; Const.Int16 i
    | SynConst.Measure(SynConst.Int32 i, _, _) -> unifyMeasureArg (i=0) cenv.g.pint_tcr c; Const.Int32 i
    | SynConst.Measure(SynConst.Int64 i, _, _) -> unifyMeasureArg (i=0L) cenv.g.pint64_tcr c; Const.Int64 i
    | SynConst.Measure(SynConst.IntPtr i, _, _) when expandedMeasurablesEnabled -> unifyMeasureArg (i=0L) cenv.g.pnativeint_tcr c; Const.IntPtr i
    | SynConst.Measure(SynConst.Byte i, _, _) when expandedMeasurablesEnabled -> unifyMeasureArg (i=0uy) cenv.g.puint8_tcr c; Const.Byte i
    | SynConst.Measure(SynConst.UInt16 i, _, _) when expandedMeasurablesEnabled -> unifyMeasureArg (i=0us) cenv.g.puint16_tcr c; Const.UInt16 i
    | SynConst.Measure(SynConst.UInt32 i, _, _) when expandedMeasurablesEnabled -> unifyMeasureArg (i=0u) cenv.g.puint_tcr c; Const.UInt32 i
    | SynConst.Measure(SynConst.UInt64 i, _, _) when expandedMeasurablesEnabled -> unifyMeasureArg (i=0UL) cenv.g.puint64_tcr c; Const.UInt64 i
    | SynConst.Measure(SynConst.UIntPtr i, _, _) when expandedMeasurablesEnabled -> unifyMeasureArg (i=0UL) cenv.g.punativeint_tcr c; Const.UIntPtr i
    | SynConst.Char c -> unif cenv.g.char_ty; Const.Char c
    | SynConst.String (s, _, _)
    | SynConst.SourceIdentifier (_, s, _) -> unif cenv.g.string_ty; Const.String s
    | SynConst.UserNum _ -> error (InternalError(FSComp.SR.tcUnexpectedBigRationalConstant(), m))
    | SynConst.Measure _ -> error (Error(FSComp.SR.tcInvalidTypeForUnitsOfMeasure(), m))
    | SynConst.UInt16s _ -> error (InternalError(FSComp.SR.tcUnexpectedConstUint16Array(), m))
    | SynConst.Bytes _ -> error (InternalError(FSComp.SR.tcUnexpectedConstByteArray(), m))

/// Convert an Abstract IL ILFieldInit value read from .NET metadata to a TAST constant
let TcFieldInit (_m: range) lit = PatternMatchCompilation.ilFieldToTastConst lit

//-------------------------------------------------------------------------
// Arities. These serve two roles in the system:
// 1. syntactic arities come from the syntactic forms found
//     signature files and the syntactic forms of function and member definitions.
// 2. compiled arities representing representation choices w.r.t. internal representations of
//     functions and members.
//-------------------------------------------------------------------------

// Adjust the arities that came from the parsing of the toptyp (arities) to be a valSynData.
// This means replacing the "[unitArg]" arising from a "unit -> ty" with a "[]".
let AdjustValSynInfoInSignature g ty (SynValInfo(argsData, retData) as sigMD) =
    if argsData.Length = 1 && argsData.Head.Length = 1 && isFunTy g ty && typeEquiv g g.unit_ty (domainOfFunTy g ty) then
        SynValInfo(argsData.Head.Tail :: argsData.Tail, retData)
    else
        sigMD

/// The ValReprInfo for a value, except the number of typars is not yet inferred
type PartialValReprInfo =
    | PartialValReprInfo of
        curriedArgInfos: ArgReprInfo list list *
        returnInfo: ArgReprInfo

let TranslateTopArgSynInfo isArg m tcAttributes (SynArgInfo(Attributes attrs, isOpt, nm)) =
    // Synthesize an artificial "OptionalArgument" attribute for the parameter
    let optAttrs =
        if isOpt then
            [ ( { TypeName=LongIdentWithDots(pathToSynLid m ["Microsoft";"FSharp";"Core";"OptionalArgument"], [])
                  ArgExpr=mkSynUnit m
                  Target=None
                  AppliesToGetterAndSetter=false
                  Range=m} : SynAttribute) ]
         else
            []

    if isArg && not (isNil attrs) && Option.isNone nm then
        errorR(Error(FSComp.SR.tcParameterRequiresName(), m))

    if not isArg && Option.isSome nm then
        errorR(Error(FSComp.SR.tcReturnValuesCannotHaveNames(), m))

    // Call the attribute checking function
    let attribs = tcAttributes (optAttrs@attrs)
    ({ Attribs = attribs; Name = nm } : ArgReprInfo)

/// Members have an arity inferred from their syntax. This "valSynData" is not quite the same as the arities
/// used in the middle and backends of the compiler ("topValInfo").
/// "0" in a valSynData (see arity_of_pat) means a "unit" arg in a topValInfo
/// Hence remove all "zeros" from arity and replace them with 1 here.
/// Note we currently use the compiled form for choosing unique names, to distinguish overloads because this must match up
/// between signature and implementation, and the signature just has "unit".
let TranslateTopValSynInfo m tcAttributes (SynValInfo(argsData, retData)) =
    PartialValReprInfo (argsData |> List.mapSquared (TranslateTopArgSynInfo true m (tcAttributes AttributeTargets.Parameter)),
                       retData |> TranslateTopArgSynInfo false m (tcAttributes AttributeTargets.ReturnValue))

let TranslatePartialArity tps (PartialValReprInfo (argsData, retData)) =
    ValReprInfo(ValReprInfo.InferTyparInfo tps, argsData, retData)

//-------------------------------------------------------------------------
// Members
//-------------------------------------------------------------------------

let ComputeLogicalName (id: Ident) (memberFlags: SynMemberFlags) =
    match memberFlags.MemberKind with
    | SynMemberKind.ClassConstructor -> ".cctor"
    | SynMemberKind.Constructor -> ".ctor"
    | SynMemberKind.Member ->
        match id.idText with
        | (".ctor" | ".cctor") as r -> errorR(Error(FSComp.SR.tcInvalidMemberNameCtor(), id.idRange)); r
        | r -> r
    | SynMemberKind.PropertyGetSet -> error(InternalError(FSComp.SR.tcMemberKindPropertyGetSetNotExpected(), id.idRange))
    | SynMemberKind.PropertyGet -> "get_" + id.idText
    | SynMemberKind.PropertySet -> "set_" + id.idText

type PreValMemberInfo =
    | PreValMemberInfo of
        memberInfo: ValMemberInfo *
        logicalName: string *
        compiledName: string

/// Make the unique "name" for a member.
//
// optImplSlotTy = None (for classes) or Some ty (when implementing interface type ty)
let MakeMemberDataAndMangledNameForMemberVal(g, tcref, isExtrinsic, attrs, optImplSlotTys, memberFlags, valSynData, id, isCompGen) =
    let logicalName = ComputeLogicalName id memberFlags
    let optIntfSlotTys = if optImplSlotTys |> List.forall (isInterfaceTy g) then optImplSlotTys else []
    let memberInfo: ValMemberInfo =
        { ApparentEnclosingEntity=tcref
          MemberFlags=memberFlags
          IsImplemented=false
          // NOTE: This value is initially only set for interface implementations and those overrides
          // where we manage to pre-infer which abstract is overridden by the method. It is filled in
          // properly when we check the allImplemented implementation checks at the end of the inference scope.
          ImplementedSlotSigs=optImplSlotTys |> List.map (fun ity -> TSlotSig(logicalName, ity, [], [], [], None)) }
    let isInstance = MemberIsCompiledAsInstance g tcref isExtrinsic memberInfo attrs
    if (memberFlags.IsDispatchSlot || not (isNil optIntfSlotTys)) then
        if not isInstance then
          errorR(VirtualAugmentationOnNullValuedType(id.idRange))
    elif not memberFlags.IsOverrideOrExplicitImpl && memberFlags.IsInstance then
        if not isExtrinsic && not isInstance then
            warning(NonVirtualAugmentationOnNullValuedType(id.idRange))

    let compiledName =
        if isExtrinsic then
             let tname = tcref.LogicalName
             let text = tname + "." + logicalName
             let text = if memberFlags.MemberKind <> SynMemberKind.Constructor && memberFlags.MemberKind <> SynMemberKind.ClassConstructor && not memberFlags.IsInstance then text + ".Static" else text
             let text = if memberFlags.IsOverrideOrExplicitImpl then text + ".Override" else text
             text
        else if not optIntfSlotTys.IsEmpty then
            // interface implementation
            if optIntfSlotTys.Length > 1 then
                failwithf "unexpected: optIntfSlotTys.Length > 1 (== %i) in MakeMemberDataAndMangledNameForMemberVal for '%s'" optIntfSlotTys.Length logicalName
            qualifiedInterfaceImplementationName g optIntfSlotTys.Head logicalName
        else
            List.foldBack (fun x -> qualifiedMangledNameOfTyconRef (tcrefOfAppTy g x)) optIntfSlotTys logicalName

    if not isCompGen && IsMangledOpName id.idText && IsInfixOperator id.idText then
        let m = id.idRange
        let name = DecompileOpName id.idText
        // Check symbolic members. Expect valSynData implied arity to be [[2]].
        match SynInfo.AritiesOfArgs valSynData with
        | [] | [0] -> warning(Error(FSComp.SR.memberOperatorDefinitionWithNoArguments name, m))
        | n :: otherArgs ->
            let opTakesThreeArgs = PrettyNaming.IsTernaryOperator name
            if n<>2 && not opTakesThreeArgs then warning(Error(FSComp.SR.memberOperatorDefinitionWithNonPairArgument(name, n), m))
            if n<>3 && opTakesThreeArgs then warning(Error(FSComp.SR.memberOperatorDefinitionWithNonTripleArgument(name, n), m))
            if not (isNil otherArgs) then warning(Error(FSComp.SR.memberOperatorDefinitionWithCurriedArguments name, m))

    if isExtrinsic && IsMangledOpName id.idText then
        warning(Error(FSComp.SR.tcMemberOperatorDefinitionInExtrinsic(), id.idRange))

    PreValMemberInfo(memberInfo, logicalName, compiledName)

type OverridesOK =
    | OverridesOK
    | WarnOnOverrides
    | ErrorOnOverrides

/// A type to represent information associated with values to indicate what explicit (declared) type parameters
/// are given and what additional type parameters can be inferred, if any.
///
/// The declared type parameters, e.g. let f<'a> (x:'a) = x, plus an indication
/// of whether additional polymorphism may be inferred, e.g. let f<'a, ..> (x:'a) y = x
type ExplicitTyparInfo =
    | ExplicitTyparInfo of
        rigidCopyOfDeclaredTypars: Typars *
        declaredTypars: Typars *
        infer: bool

let permitInferTypars = ExplicitTyparInfo ([], [], true)
let dontInferTypars = ExplicitTyparInfo ([], [], false)

type ArgAndRetAttribs = ArgAndRetAttribs of Attribs list list * Attribs
let noArgOrRetAttribs = ArgAndRetAttribs ([], [])

/// A flag to represent the sort of bindings are we processing.
/// Processing "declaration" and "class" bindings that make up a module (such as "let x = 1 let y = 2")
/// shares the same code paths (e.g. TcLetBinding and TcLetrec) as processing expression bindings (such as "let x = 1 in ...")
/// Member bindings also use this path.
//
/// However there are differences in how different bindings get processed,
/// i.e. module bindings get published to the implicitly accumulated module type, but expression 'let' bindings don't.
type DeclKind =
    | ModuleOrMemberBinding

    /// Extensions to a type within the same assembly
    | IntrinsicExtensionBinding

    /// Extensions to a type in a different assembly
    | ExtrinsicExtensionBinding

    | ClassLetBinding of isStatic: bool

    | ObjectExpressionOverrideBinding

    | ExpressionBinding

    static member IsModuleOrMemberOrExtensionBinding x =
        match x with
        | ModuleOrMemberBinding -> true
        | IntrinsicExtensionBinding -> true
        | ExtrinsicExtensionBinding -> true
        | ClassLetBinding _ -> false
        | ObjectExpressionOverrideBinding -> false
        | ExpressionBinding -> false

    static member MustHaveArity x = DeclKind.IsModuleOrMemberOrExtensionBinding x

    member x.CanBeDllImport =
        match x with
        | ModuleOrMemberBinding -> true
        | IntrinsicExtensionBinding -> true
        | ExtrinsicExtensionBinding -> true
        | ClassLetBinding _ -> true
        | ObjectExpressionOverrideBinding -> false
        | ExpressionBinding -> false

    static member IsAccessModifierPermitted x = DeclKind.IsModuleOrMemberOrExtensionBinding x

    static member ImplicitlyStatic x = DeclKind.IsModuleOrMemberOrExtensionBinding x

    static member AllowedAttribTargets (memberFlagsOpt: SynMemberFlags option) x =
        match x with
        | ModuleOrMemberBinding | ObjectExpressionOverrideBinding ->
            match memberFlagsOpt with
            | Some flags when flags.MemberKind = SynMemberKind.Constructor -> AttributeTargets.Constructor
            | Some flags when flags.MemberKind = SynMemberKind.PropertyGetSet -> AttributeTargets.Event ||| AttributeTargets.Property
            | Some flags when flags.MemberKind = SynMemberKind.PropertyGet -> AttributeTargets.Event ||| AttributeTargets.Property ||| AttributeTargets.ReturnValue
            | Some flags when flags.MemberKind = SynMemberKind.PropertySet -> AttributeTargets.Property
            | Some _ -> AttributeTargets.Method ||| AttributeTargets.ReturnValue
            | None -> AttributeTargets.Field ||| AttributeTargets.Method ||| AttributeTargets.Property ||| AttributeTargets.ReturnValue
        | IntrinsicExtensionBinding -> AttributeTargets.Method ||| AttributeTargets.Property ||| AttributeTargets.ReturnValue
        | ExtrinsicExtensionBinding -> AttributeTargets.Method ||| AttributeTargets.Property ||| AttributeTargets.ReturnValue
        | ClassLetBinding _ -> AttributeTargets.Field ||| AttributeTargets.Method ||| AttributeTargets.ReturnValue
        | ExpressionBinding -> enum 0 // indicates attributes not allowed on expression 'let' bindings

    // Note: now always true
    static member CanGeneralizeConstrainedTypars x =
        match x with
        | ModuleOrMemberBinding -> true
        | IntrinsicExtensionBinding -> true
        | ExtrinsicExtensionBinding -> true
        | ClassLetBinding _ -> true
        | ObjectExpressionOverrideBinding -> true
        | ExpressionBinding -> true

    static member ConvertToLinearBindings x =
        match x with
        | ModuleOrMemberBinding -> true
        | IntrinsicExtensionBinding -> true
        | ExtrinsicExtensionBinding -> true
        | ClassLetBinding _ -> true
        | ObjectExpressionOverrideBinding -> true
        | ExpressionBinding -> false

    static member CanOverrideOrImplement x =
        match x with
        | ModuleOrMemberBinding -> OverridesOK
        | IntrinsicExtensionBinding -> WarnOnOverrides
        | ExtrinsicExtensionBinding -> ErrorOnOverrides
        | ClassLetBinding _ -> ErrorOnOverrides
        | ObjectExpressionOverrideBinding -> OverridesOK
        | ExpressionBinding -> ErrorOnOverrides

//-------------------------------------------------------------------------
// Data structures that track the gradual accumulation of information
// about values and members during inference.
//-------------------------------------------------------------------------

/// The results of preliminary pass over patterns to extract variables being declared.
// We should make this a record for cleaner code
type PrelimValScheme1 =
    | PrelimValScheme1 of
        id: Ident *
        explicitTyparInfo: ExplicitTyparInfo *
        TType *
        PartialValReprInfo option *
        PreValMemberInfo option *
        bool *
        ValInline *
        ValBaseOrThisInfo *
        ArgAndRetAttribs *
        SynAccess option *
        bool

    member x.Type = let (PrelimValScheme1(_, _, ty, _, _, _, _, _, _, _, _)) = x in ty

    member x.Ident = let (PrelimValScheme1(id, _, _, _, _, _, _, _, _, _, _)) = x in id

/// The results of applying let-style generalization after type checking.
// We should make this a record for cleaner code
type PrelimValScheme2 =
    PrelimValScheme2 of
        Ident *
        TypeScheme *
        PartialValReprInfo option *
        PreValMemberInfo option *
        bool *
        ValInline *
        ValBaseOrThisInfo *
        ArgAndRetAttribs *
        SynAccess option *
        bool *
        bool (* hasDeclaredTypars *)


/// The results of applying arity inference to PrelimValScheme2
type ValScheme =
    | ValScheme of
        id: Ident *
        typeScheme: TypeScheme *
        topValInfo: ValReprInfo option *
        memberInfo: PreValMemberInfo option *
        isMutable: bool *
        inlineInfo: ValInline *
        baseOrThisInfo: ValBaseOrThisInfo *
        visibility: SynAccess option *
        compgen: bool *
        isIncrClass: bool *
        isTyFunc: bool *
        hasDeclaredTypars: bool

    member x.GeneralizedTypars = let (ValScheme(_, TypeScheme(gtps, _), _, _, _, _, _, _, _, _, _, _)) = x in gtps

    member x.TypeScheme = let (ValScheme(_, ts, _, _, _, _, _, _, _, _, _, _)) = x in ts

    member x.ValReprInfo = let (ValScheme(_, _, topValInfo, _, _, _, _, _, _, _, _, _)) = x in topValInfo

/// Translation of patterns is split into three phases. The first collects names.
/// The second is run after val_specs have been created for those names and inference
/// has been resolved. The second phase is run by applying a function returned by the
/// first phase. The input to the second phase is a List.map that gives the Val and type scheme
/// for each value bound by the pattern.
type TcPatPhase2Input =
    | TcPatPhase2Input of (Val * TypeScheme) NameMap * bool
    // Get an input indicating we are no longer on the left-most path through a disjunctive "or" pattern
    member x.RightPath = (let (TcPatPhase2Input(a, _)) = x in TcPatPhase2Input(a, false))

/// The first phase of checking and elaborating a binding leaves a goop of information.
/// This is a bit of a mess: much of this information is also carried on a per-value basis by the
/// "NameMap<PrelimValScheme1>".
type CheckedBindingInfo =
    | CheckedBindingInfo of
       inlineFlag: ValInline *
       valAttribs: Attribs *
       xmlDoc: XmlDoc *
       tcPatPhase2: (TcPatPhase2Input -> PatternMatchCompilation.Pattern) *
       exlicitTyparInfo: ExplicitTyparInfo *
       nameToPrelimValSchemeMap: NameMap<PrelimValScheme1> *
       rhsExprChecked: Expr *
       argAndRetAttribs: ArgAndRetAttribs *
       overallPatTy: TType *
       mBinding: range *
       spBind: DebugPointAtBinding *
       isCompilerGenerated: bool *
       literalValue: Const option *
       isFixed: bool
    member x.Expr = let (CheckedBindingInfo(_, _, _, _, _, _, expr, _, _, _, _, _, _, _)) = x in expr
    member x.SeqPoint = let (CheckedBindingInfo(_, _, _, _, _, _, _, _, _, _, spBind, _, _, _)) = x in spBind

/// Return the generalized type for a type scheme
let GeneralizedTypeForTypeScheme typeScheme =
    let (TypeScheme(generalizedTypars, tau)) = typeScheme
    mkForallTyIfNeeded generalizedTypars tau

/// Create a type scheme for something that is not generic
let NonGenericTypeScheme ty = TypeScheme([], ty)

//-------------------------------------------------------------------------
// Helpers related to publishing values, types and members into the
// elaborated representation.
//-------------------------------------------------------------------------

let UpdateAccModuleOrNamespaceType cenv env f =
    // When compiling FSharp.Core, modify the fslib CCU to ensure forward stable references used by
    // the compiler can be resolved ASAP. Not at all pretty but it's hard to
    // find good ways to do references from the compiler into a term graph.
    if cenv.compilingCanonicalFslibModuleType then
        let nleref = mkNonLocalEntityRef cenv.topCcu (arrPathOfLid env.ePath)
        let modul = nleref.Deref
        modul.entity_modul_contents <- MaybeLazy.Strict (f true modul.ModuleOrNamespaceType)
    SetCurrAccumulatedModuleOrNamespaceType env (f false (GetCurrAccumulatedModuleOrNamespaceType env))

let PublishModuleDefn cenv env mspec =
    UpdateAccModuleOrNamespaceType cenv env (fun intoFslibCcu mty ->
       if intoFslibCcu then mty
       else mty.AddEntity mspec)
    let item = Item.ModuleOrNamespaces([mkLocalModRef mspec])
    CallNameResolutionSink cenv.tcSink (mspec.Range, env.NameEnv, item, emptyTyparInst, ItemOccurence.Binding, env.AccessRights)

let PublishTypeDefn cenv env mspec =
    UpdateAccModuleOrNamespaceType cenv env (fun _ mty ->
       mty.AddEntity mspec)

let PublishValueDefnPrim cenv env (vspec: Val) =
    UpdateAccModuleOrNamespaceType cenv env (fun _ mty ->
        mty.AddVal vspec)

let PublishValueDefn cenv env declKind (vspec: Val) =
    if (declKind = ModuleOrMemberBinding) &&
       ((GetCurrAccumulatedModuleOrNamespaceType env).ModuleOrNamespaceKind = Namespace) &&
       (Option.isNone vspec.MemberInfo) then
           errorR(Error(FSComp.SR.tcNamespaceCannotContainValues(), vspec.Range))

    if (declKind = ExtrinsicExtensionBinding) &&
       ((GetCurrAccumulatedModuleOrNamespaceType env).ModuleOrNamespaceKind = Namespace) then
           errorR(Error(FSComp.SR.tcNamespaceCannotContainExtensionMembers(), vspec.Range))

    // Publish the value to the module type being generated.
    match declKind with
    | ModuleOrMemberBinding
    | ExtrinsicExtensionBinding
    | IntrinsicExtensionBinding -> PublishValueDefnPrim cenv env vspec
    | _ -> ()

    match vspec.MemberInfo with
    | Some _ when
        (not vspec.IsCompilerGenerated &&
         // Extrinsic extensions don't get added to the tcaug
         not (declKind = ExtrinsicExtensionBinding)) ->
         // // Static initializers don't get published to the tcaug
         // not (memberInfo.MemberFlags.MemberKind = SynMemberKind.ClassConstructor)) ->

        let tcaug = vspec.MemberApparentEntity.TypeContents
        let vref = mkLocalValRef vspec
        tcaug.tcaug_adhoc <- NameMultiMap.add vspec.LogicalName vref tcaug.tcaug_adhoc
        tcaug.tcaug_adhoc_list.Add (ValRefIsExplicitImpl cenv.g vref, vref)
    | _ -> ()

let CombineVisibilityAttribs vis1 vis2 m =
    match vis1 with
    | Some _ ->
        if Option.isSome vis2 then
            errorR(Error(FSComp.SR.tcMultipleVisibilityAttributes(), m))
        vis1
    | _ -> vis2

let ComputeAccessAndCompPath env declKindOpt m vis overrideVis actualParent =
    let accessPath = env.eAccessPath
    let accessModPermitted =
        match declKindOpt with
        | None -> true
        | Some declKind -> DeclKind.IsAccessModifierPermitted declKind

    if Option.isSome vis && not accessModPermitted then
        errorR(Error(FSComp.SR.tcMultipleVisibilityAttributesWithLet(), m))

    let vis =
        match overrideVis, vis with
        | Some v, _ -> v
        | _, None -> taccessPublic (* a module or member binding defaults to "public" *)
        | _, Some SynAccess.Public -> taccessPublic
        | _, Some SynAccess.Private -> taccessPrivate accessPath
        | _, Some SynAccess.Internal -> taccessInternal

    let vis =
        match actualParent with
        | ParentNone -> vis
        | Parent tcref -> combineAccess vis tcref.Accessibility

    let cpath = if accessModPermitted then Some env.eCompPath else None
    vis, cpath

let CheckForAbnormalOperatorNames cenv (idRange: range) coreDisplayName (memberInfoOpt: ValMemberInfo option) =
    if (idRange.EndColumn - idRange.StartColumn <= 5) &&
        not cenv.g.compilingFslib
    then
        let opName = DecompileOpName coreDisplayName
        let isMember = memberInfoOpt.IsSome
        match opName with
        | PrettyNaming.Relational ->
            if isMember then
                warning(StandardOperatorRedefinitionWarning(FSComp.SR.tcInvalidMethodNameForRelationalOperator(opName, coreDisplayName), idRange))
            else
                warning(StandardOperatorRedefinitionWarning(FSComp.SR.tcInvalidOperatorDefinitionRelational opName, idRange))
        | PrettyNaming.Equality ->
            if isMember then
                warning(StandardOperatorRedefinitionWarning(FSComp.SR.tcInvalidMethodNameForEquality(opName, coreDisplayName), idRange))
            else
                warning(StandardOperatorRedefinitionWarning(FSComp.SR.tcInvalidOperatorDefinitionEquality opName, idRange))
        | PrettyNaming.Control ->
            if isMember then
                warning(StandardOperatorRedefinitionWarning(FSComp.SR.tcInvalidMemberName(opName, coreDisplayName), idRange))
            else
                warning(StandardOperatorRedefinitionWarning(FSComp.SR.tcInvalidOperatorDefinition opName, idRange))
        | PrettyNaming.Indexer ->
            if not isMember then
                error(StandardOperatorRedefinitionWarning(FSComp.SR.tcInvalidIndexOperatorDefinition opName, idRange))
        | PrettyNaming.FixedTypes ->
            if isMember then
                warning(StandardOperatorRedefinitionWarning(FSComp.SR.tcInvalidMemberNameFixedTypes opName, idRange))
        | PrettyNaming.Other -> ()

let MakeAndPublishVal cenv env (altActualParent, inSig, declKind, vrec, vscheme, attrs, doc, konst, isGeneratedEventVal) =

    let (ValScheme(id, typeScheme, topValData, memberInfoOpt, isMutable, inlineFlag, baseOrThis, vis, compgen, isIncrClass, isTyFunc, hasDeclaredTypars)) = vscheme

    let ty = GeneralizedTypeForTypeScheme typeScheme

    let m = id.idRange

    let isTopBinding =
        match declKind with
        | ModuleOrMemberBinding -> true
        | ExtrinsicExtensionBinding -> true
        | IntrinsicExtensionBinding -> true
        | _ -> false

    let isExtrinsic = (declKind = ExtrinsicExtensionBinding)

    let actualParent, overrideVis =
        // Use the parent of the member if it's available
        // If it's an extrinsic extension member or not a member then use the containing module.
        match memberInfoOpt with
        | Some (PreValMemberInfo(memberInfo, _, _)) when not isExtrinsic ->
            if memberInfo.ApparentEnclosingEntity.IsModuleOrNamespace then
                errorR(InternalError(FSComp.SR.tcExpectModuleOrNamespaceParent(id.idText), m))

            // Members of interface implementations have the accessibility of the interface
            // they are implementing.
            let vis =
                if MemberIsExplicitImpl cenv.g memberInfo then
                    let slotSig = List.head memberInfo.ImplementedSlotSigs
                    match slotSig.ImplementedType with
                    | TType_app (tyconref, _) -> Some tyconref.Accessibility
                    | _ -> None
                else
                    None
            Parent(memberInfo.ApparentEnclosingEntity), vis
        | _ -> altActualParent, None

    let vis, _ = ComputeAccessAndCompPath env (Some declKind) id.idRange vis overrideVis actualParent

    let inlineFlag = 
        if HasFSharpAttributeOpt cenv.g cenv.g.attrib_DllImportAttribute attrs then 
            if inlineFlag = ValInline.Always then 
              errorR(Error(FSComp.SR.tcDllImportStubsCannotBeInlined(), m)) 
            ValInline.Never 
        else 
            let implflags = 
                match TryFindFSharpAttribute cenv.g cenv.g.attrib_MethodImplAttribute attrs with
                | Some (Attrib(_, _, [ AttribInt32Arg flags ], _, _, _, _)) -> flags
                | _ -> 0x0
            // MethodImplOptions.NoInlining = 0x8
            let NO_INLINING = 0x8
            if (implflags &&& NO_INLINING) <> 0x0 then
                ValInline.Never
            else
                inlineFlag

    // CompiledName not allowed on virtual/abstract/override members
    let compiledNameAttrib = TryFindFSharpStringAttribute cenv.g cenv.g.attrib_CompiledNameAttribute attrs
    if Option.isSome compiledNameAttrib then
        match memberInfoOpt with
        | Some (PreValMemberInfo(memberInfo, _, _)) ->
            if memberInfo.MemberFlags.IsDispatchSlot || memberInfo.MemberFlags.IsOverrideOrExplicitImpl then
                errorR(Error(FSComp.SR.tcCompiledNameAttributeMisused(), m))
        | None ->
            match altActualParent with
            | ParentNone -> errorR(Error(FSComp.SR.tcCompiledNameAttributeMisused(), m))
            | _ -> ()

    let compiledNameIsOnProp =
        match memberInfoOpt with
        | Some (PreValMemberInfo(memberInfo, _, _)) ->
            memberInfo.MemberFlags.MemberKind = SynMemberKind.PropertyGet ||
            memberInfo.MemberFlags.MemberKind = SynMemberKind.PropertySet ||
            memberInfo.MemberFlags.MemberKind = SynMemberKind.PropertyGetSet
        | _ -> false

    let compiledName =
        match compiledNameAttrib with
        // We fix up CompiledName on properties during codegen
        | Some _ when not compiledNameIsOnProp -> compiledNameAttrib
        | _ ->
            match memberInfoOpt with
            | Some (PreValMemberInfo(_, _, compiledName)) ->
                Some compiledName
            | None ->
                None

    let logicalName =
        match memberInfoOpt with
        | Some (PreValMemberInfo(_, logicalName, _)) ->
            logicalName
        | None ->
            id.idText

    let memberInfoOpt =
        match memberInfoOpt with
        | Some (PreValMemberInfo(memberInfo, _, _)) ->
            Some memberInfo
        | None ->
            None

    let mut = if isMutable then Mutable else Immutable
    let vspec =
        Construct.NewVal
            (logicalName, id.idRange, compiledName, ty, mut,
             compgen, topValData, vis, vrec, memberInfoOpt, baseOrThis, attrs, inlineFlag,
             doc, isTopBinding, isExtrinsic, isIncrClass, isTyFunc,
             (hasDeclaredTypars || inSig), isGeneratedEventVal, konst, actualParent)


    CheckForAbnormalOperatorNames cenv id.idRange vspec.CoreDisplayName memberInfoOpt

    PublishValueDefn cenv env declKind vspec

    let shouldNotifySink (vspec: Val) =
        match vspec.MemberInfo with
        // `this` reference named `__`. It's either:
        // * generated by compiler for auto properties or
        // * provided by source code (i.e. `member _.Method = ...`)
        // We don't notify sink about it to prevent generating `FSharpSymbol` for it and appearing in completion list.
        | None when
            let baseOrThisInfo = vspec.BaseOrThisInfo
            baseOrThisInfo = ValBaseOrThisInfo.BaseVal || // visualfsharp#3699
            baseOrThisInfo = ValBaseOrThisInfo.MemberThisVal && vspec.LogicalName = "__" -> false
        | _ -> true

    match cenv.tcSink.CurrentSink with
    | Some _ when not vspec.IsCompilerGenerated && shouldNotifySink vspec ->
        let nenv = AddFakeNamedValRefToNameEnv vspec.DisplayName env.NameEnv (mkLocalValRef vspec)
        CallEnvSink cenv.tcSink (vspec.Range, nenv, env.eAccessRights)
        let item = Item.Value(mkLocalValRef vspec)
        CallNameResolutionSink cenv.tcSink (vspec.Range, nenv, item, emptyTyparInst, ItemOccurence.Binding, env.eAccessRights)
    | _ -> ()

    vspec

let MakeAndPublishVals cenv env (altActualParent, inSig, declKind, vrec, valSchemes, attrs, doc, literalValue) =
    Map.foldBack
        (fun name (valscheme: ValScheme) values ->
          Map.add name (MakeAndPublishVal cenv env (altActualParent, inSig, declKind, vrec, valscheme, attrs, doc, literalValue, false), valscheme.TypeScheme) values)
        valSchemes
        Map.empty

let MakeAndPublishBaseVal cenv env baseIdOpt ty =
    baseIdOpt
    |> Option.map (fun (id: Ident) ->
       let valscheme = ValScheme(id, NonGenericTypeScheme ty, None, None, false, ValInline.Never, BaseVal, None, false, false, false, false)
       MakeAndPublishVal cenv env (ParentNone, false, ExpressionBinding, ValNotInRecScope, valscheme, [], XmlDoc.Empty, None, false))

// Make the "delayed reference" value where the this pointer will reside after calling the base class constructor
// Make the value for the 'this' pointer for use within a constructor
let MakeAndPublishSafeThisVal cenv env (thisIdOpt: Ident option) thisTy =
    match thisIdOpt with
    | Some thisId ->
        // for structs, thisTy is a byref
        if not (isFSharpObjModelTy cenv.g thisTy) then
            errorR(Error(FSComp.SR.tcStructsCanOnlyBindThisAtMemberDeclaration(), thisId.idRange))

        let valScheme = ValScheme(thisId, NonGenericTypeScheme(mkRefCellTy cenv.g thisTy), None, None, false, ValInline.Never, CtorThisVal, None, false, false, false, false)
        Some(MakeAndPublishVal cenv env (ParentNone, false, ExpressionBinding, ValNotInRecScope, valScheme, [], XmlDoc.Empty, None, false))

    | None ->
        None


//-------------------------------------------------------------------------
// Helpers for type inference for recursive bindings
//-------------------------------------------------------------------------

/// Fixup the type instantiation at recursive references. Used after the bindings have been
/// checked. The fixups are applied by using mutation.
let AdjustAndForgetUsesOfRecValue cenv (vrefTgt: ValRef) (valScheme: ValScheme) =
    let (TypeScheme(generalizedTypars, _)) = valScheme.TypeScheme
    let fty = GeneralizedTypeForTypeScheme valScheme.TypeScheme
    let lvrefTgt = vrefTgt.Deref
    if not (isNil generalizedTypars) then
        // Find all the uses of this recursive binding and use mutation to adjust the expressions
        // at those points in order to record the inferred type parameters.
        let recUses = cenv.recUses.Find lvrefTgt
        recUses
        |> List.iter (fun (fixupPoint, m, isComplete) ->
              if not isComplete then
                  // Keep any values for explicit type arguments
                  let fixedUpExpr =
                      let vrefFlags, tyargs0 =
                          match fixupPoint.Value with
                          | Expr.App (Expr.Val (_, vrefFlags, _), _, tyargs0, [], _) -> vrefFlags, tyargs0
                          | Expr.Val (_, vrefFlags, _) -> vrefFlags, []
                          | _ ->
                              errorR(Error(FSComp.SR.tcUnexpectedExprAtRecInfPoint(), m))
                              NormalValUse, []

                      let ityargs = generalizeTypars (List.skip (List.length tyargs0) generalizedTypars)
                      primMkApp (Expr.Val (vrefTgt, vrefFlags, m), fty) (tyargs0 @ ityargs) [] m
                  fixupPoint.Value <- fixedUpExpr)

    vrefTgt.Deref.SetValRec ValNotInRecScope
    cenv.recUses <- cenv.recUses.Remove vrefTgt.Deref


/// Set the properties of recursive values that are only fully known after inference is complete
let AdjustRecType (vspec: Val) (ValScheme(_, typeScheme, topValData, _, _, _, _, _, _, _, _, _)) =
    let fty = GeneralizedTypeForTypeScheme typeScheme
    vspec.SetType fty
    vspec.SetValReprInfo topValData
    vspec.SetValRec (ValInRecScope true)

/// Record the generated value expression as a place where we will have to
/// adjust using AdjustAndForgetUsesOfRecValue at a letrec point. Every use of a value
/// under a letrec gets used at the _same_ type instantiation.
let RecordUseOfRecValue cenv vrec (vrefTgt: ValRef) vexp m =
    match vrec with
    | ValInRecScope isComplete ->
        let fixupPoint = ref vexp
        cenv.recUses <- cenv.recUses.Add (vrefTgt.Deref, (fixupPoint, m, isComplete))
        Expr.Link fixupPoint
    | ValNotInRecScope ->
        vexp

type RecursiveUseFixupPoints = RecursiveUseFixupPoints of (Expr ref * range) list

/// Get all recursive references, for fixing up delayed recursion using laziness
let GetAllUsesOfRecValue cenv vrefTgt =
    RecursiveUseFixupPoints (cenv.recUses.Find vrefTgt |> List.map (fun (fixupPoint, m, _) -> (fixupPoint, m)))


//-------------------------------------------------------------------------
// Helpers for Generalization
//-------------------------------------------------------------------------

let ChooseCanonicalDeclaredTyparsAfterInference g denv declaredTypars m =
    declaredTypars |> List.iter (fun tp ->
      let ty = mkTyparTy tp
      if not (isAnyParTy g ty) then
          error(Error(FSComp.SR.tcLessGenericBecauseOfAnnotation(tp.Name, NicePrint.prettyStringOfTy denv ty), tp.Range)))

    let declaredTypars = NormalizeDeclaredTyparsForEquiRecursiveInference g declaredTypars

    if ListSet.hasDuplicates typarEq declaredTypars then
        errorR(Error(FSComp.SR.tcConstrainedTypeVariableCannotBeGeneralized(), m))

    declaredTypars

let ChooseCanonicalValSchemeAfterInference g denv vscheme m =
    let (ValScheme(id, typeScheme, arityInfo, memberInfoOpt, isMutable, inlineFlag, baseOrThis, vis, compgen, isIncrClass, isTyFunc, hasDeclaredTypars)) = vscheme
    let (TypeScheme(generalizedTypars, ty)) = typeScheme
    let generalizedTypars = ChooseCanonicalDeclaredTyparsAfterInference g denv generalizedTypars m
    let typeScheme = TypeScheme(generalizedTypars, ty)
    let valscheme = ValScheme(id, typeScheme, arityInfo, memberInfoOpt, isMutable, inlineFlag, baseOrThis, vis, compgen, isIncrClass, isTyFunc, hasDeclaredTypars)
    valscheme

let PlaceTyparsInDeclarationOrder declaredTypars generalizedTypars =
    declaredTypars @ (generalizedTypars |> List.filter (fun tp -> not (ListSet.contains typarEq tp declaredTypars)))

let SetTyparRigid denv m (tp: Typar) =
    match tp.Solution with
    | None -> ()
    | Some ty ->
        if tp.IsCompilerGenerated then
            errorR(Error(FSComp.SR.tcGenericParameterHasBeenConstrained(NicePrint.prettyStringOfTy denv ty), m))
        else
            errorR(Error(FSComp.SR.tcTypeParameterHasBeenConstrained(NicePrint.prettyStringOfTy denv ty), tp.Range))
    tp.SetRigidity TyparRigidity.Rigid

let GeneralizeVal cenv denv enclosingDeclaredTypars generalizedTyparsForThisBinding
        (PrelimValScheme1(id, explicitTyparInfo, ty, partialValReprInfo, memberInfoOpt, isMutable, inlineFlag, baseOrThis, argAttribs, vis, compgen)) =

    let (ExplicitTyparInfo(_rigidCopyOfDeclaredTypars, declaredTypars, _)) = explicitTyparInfo

    let m = id.idRange

    let allDeclaredTypars = enclosingDeclaredTypars@declaredTypars
    let allDeclaredTypars = ChooseCanonicalDeclaredTyparsAfterInference cenv.g denv allDeclaredTypars m

    // Trim out anything not in type of the value (as opposed to the type of the r.h.s)
    // This is important when a single declaration binds
    // multiple generic items, where each item does not use all the polymorphism
    // of the r.h.s., e.g. let x, y = None, []
    let computeRelevantTypars thruFlag =
        let ftps = freeInTypeLeftToRight cenv.g thruFlag ty
        let generalizedTypars = generalizedTyparsForThisBinding |> List.filter (fun tp -> ListSet.contains typarEq tp ftps)
        // Put declared typars first
        let generalizedTypars = PlaceTyparsInDeclarationOrder allDeclaredTypars generalizedTypars
        generalizedTypars

    let generalizedTypars = computeRelevantTypars false

    // Check stability of existence and ordering of type parameters under erasure of type abbreviations
    let generalizedTyparsLookingThroughTypeAbbreviations = computeRelevantTypars true
    if not (generalizedTypars.Length = generalizedTyparsLookingThroughTypeAbbreviations.Length &&
            List.forall2 typarEq generalizedTypars generalizedTyparsLookingThroughTypeAbbreviations)
    then
        warning(Error(FSComp.SR.tcTypeParametersInferredAreNotStable(), m))

    let hasDeclaredTypars = not (isNil declaredTypars)
    // This is just about the only place we form a TypeScheme
    let tyScheme = TypeScheme(generalizedTypars, ty)
    PrelimValScheme2(id, tyScheme, partialValReprInfo, memberInfoOpt, isMutable, inlineFlag, baseOrThis, argAttribs, vis, compgen, hasDeclaredTypars)

let GeneralizeVals cenv denv enclosingDeclaredTypars generalizedTypars types =
    NameMap.map (GeneralizeVal cenv denv enclosingDeclaredTypars generalizedTypars) types

let DontGeneralizeVals types =
    let dontGeneralizeVal (PrelimValScheme1(id, _, ty, partialValReprInfoOpt, memberInfoOpt, isMutable, inlineFlag, baseOrThis, argAttribs, vis, compgen)) =
        PrelimValScheme2(id, NonGenericTypeScheme ty, partialValReprInfoOpt, memberInfoOpt, isMutable, inlineFlag, baseOrThis, argAttribs, vis, compgen, false)
    NameMap.map dontGeneralizeVal types

let InferGenericArityFromTyScheme (TypeScheme(generalizedTypars, _)) partialValReprInfo =
    TranslatePartialArity generalizedTypars partialValReprInfo

let ComputeIsTyFunc(id: Ident, hasDeclaredTypars, arityInfo: ValReprInfo option) =
    hasDeclaredTypars &&
    (match arityInfo with
     | None -> error(Error(FSComp.SR.tcExplicitTypeParameterInvalid(), id.idRange))
     | Some info -> info.NumCurriedArgs = 0)

let UseSyntacticArity declKind typeScheme partialValReprInfo =
    if DeclKind.MustHaveArity declKind then
        Some(InferGenericArityFromTyScheme typeScheme partialValReprInfo)
    else
        None

/// Combine the results of InferSynValData and InferArityOfExpr.
//
// The F# spec says that we infer arities from declaration forms and types.
//
// For example
//     let f (a, b) c = 1                  // gets arity [2;1]
//     let f (a: int*int) = 1              // gets arity [2], based on type
//     let f () = 1                       // gets arity [0]
//     let f = (fun (x: int) (y: int) -> 1) // gets arity [1;1]
//     let f = (fun (x: int*int) y -> 1)   // gets arity [2;1]
//
// Some of this arity inference is purely syntax directed and done in InferSynValData in ast.fs
// Some is done by InferArityOfExpr.
//
// However, there are some corner cases in this specification. In particular, consider
//   let f () () = 1             // [0;1] or [0;0]? Answer: [0;1]
//   let f (a: unit) = 1          // [0] or [1]? Answer: [1]
//   let f = (fun () -> 1)       // [0] or [1]? Answer: [0]
//   let f = (fun (a: unit) -> 1) // [0] or [1]? Answer: [1]
//
// The particular choice of [1] for
//   let f (a: unit) = 1
// is intended to give a disambiguating form for members that override methods taking a single argument
// instantiated to type "unit", e.g.
//    type Base<'a> =
//        abstract M: 'a -> unit
//
//    { new Base<int> with
//        member x.M(v: int) = () }
//
//    { new Base<unit> with
//        member x.M(v: unit) = () }
//
let CombineSyntacticAndInferredArities g declKind rhsExpr prelimScheme =
    let (PrelimValScheme2(_, typeScheme, partialValReprInfoOpt, memberInfoOpt, isMutable, _, _, ArgAndRetAttribs(argAttribs, retAttribs), _, _, _)) = prelimScheme
    match partialValReprInfoOpt, DeclKind.MustHaveArity declKind with
    | _, false -> None
    | None, true -> Some(PartialValReprInfo([], ValReprInfo.unnamedRetVal))
    // Don't use any expression information for members, where syntax dictates the arity completely
    | _ when memberInfoOpt.IsSome ->
        partialValReprInfoOpt
    // Don't use any expression information for 'let' bindings where return attributes are present
    | _ when retAttribs.Length > 0 -> 
        partialValReprInfoOpt
    | Some partialValReprInfoFromSyntax, true -> 
        let (PartialValReprInfo(curriedArgInfosFromSyntax, retInfoFromSyntax)) = partialValReprInfoFromSyntax
        let partialArityInfo =
            if isMutable then
                PartialValReprInfo ([], retInfoFromSyntax)
            else

                let (ValReprInfo (_, curriedArgInfosFromExpression, _)) =
                    InferArityOfExpr g AllowTypeDirectedDetupling.Yes (GeneralizedTypeForTypeScheme typeScheme) argAttribs retAttribs rhsExpr

                // Choose between the syntactic arity and the expression-inferred arity
                // If the syntax specifies an eliminated unit arg, then use that
                let choose ai1 ai2 =
                    match ai1, ai2 with
                    | [], _ -> []
                    // Dont infer eliminated unit args from the expression if they don't occur syntactically.
                    | ai, [] -> ai
                    // If we infer a tupled argument from the expression and/or type then use that
                    | _ when ai1.Length < ai2.Length -> ai2
                    | _ -> ai1
                let rec loop ais1 ais2 =
                    match ais1, ais2 with
                    // If the expression infers additional arguments then use those (this shouldn't happen, since the
                    // arity inference done on the syntactic form should give identical results)
                    | [], ais | ais, [] -> ais
                    | (h1 :: t1), (h2 :: t2) -> choose h1 h2 :: loop t1 t2
                let curriedArgInfos = loop curriedArgInfosFromSyntax curriedArgInfosFromExpression
                PartialValReprInfo (curriedArgInfos, retInfoFromSyntax)

        Some partialArityInfo

let BuildValScheme declKind partialArityInfoOpt prelimScheme =
    let (PrelimValScheme2(id, typeScheme, _, memberInfoOpt, isMutable, inlineFlag, baseOrThis, _, vis, compgen, hasDeclaredTypars)) = prelimScheme
    let topValInfo =
        if DeclKind.MustHaveArity declKind then
            Option.map (InferGenericArityFromTyScheme typeScheme) partialArityInfoOpt
        else
            None
    let isTyFunc = ComputeIsTyFunc(id, hasDeclaredTypars, topValInfo)
    ValScheme(id, typeScheme, topValInfo, memberInfoOpt, isMutable, inlineFlag, baseOrThis, vis, compgen, false, isTyFunc, hasDeclaredTypars)

let UseCombinedArity g declKind rhsExpr prelimScheme =
    let partialArityInfoOpt = CombineSyntacticAndInferredArities g declKind rhsExpr prelimScheme
    BuildValScheme declKind partialArityInfoOpt prelimScheme

let UseNoArity prelimScheme =
    BuildValScheme ExpressionBinding None prelimScheme

/// Make and publish the Val nodes for a collection of simple (non-generic) value specifications
let MakeAndPublishSimpleVals cenv env names =
    let tyschemes = DontGeneralizeVals names
    let valSchemes = NameMap.map UseNoArity tyschemes
    let values = MakeAndPublishVals cenv env (ParentNone, false, ExpressionBinding, ValNotInRecScope, valSchemes, [], XmlDoc.Empty, None)
    let vspecMap = NameMap.map fst values
    values, vspecMap

/// Make and publish the Val nodes for a collection of value specifications at Lambda and Match positions
///
/// We merge the additions to the name resolution environment into one using a merged range so all values are brought
/// into scope simultaneously. The technique used to do this is a disturbing and unfortunate hack that
/// intercepts `NotifyNameResolution` calls being emitted by `MakeAndPublishSimpleVals`

let MakeAndPublishSimpleValsForMergedScope cenv env m (names: NameMap<_>) =
    let values, vspecMap =
        if names.Count <= 1 then
            MakeAndPublishSimpleVals cenv env names
        else
            let nameResolutions = ResizeArray()

            let notifyNameResolution (pos, item, itemGroup, itemTyparInst, occurence, nenv, ad, (m: range), replacing) =
                if not m.IsSynthetic then
                    nameResolutions.Add(pos, item, itemGroup, itemTyparInst, occurence, nenv, ad, m, replacing)

            let values, vspecMap =
                let sink =
                    { new ITypecheckResultsSink with
                        member this.NotifyEnvWithScope(_, _, _) = () // ignore EnvWithScope reports

                        member this.NotifyNameResolution(pos, item, itemTyparInst, occurence, nenv, ad, m, replacing) =
                            notifyNameResolution (pos, item, item, itemTyparInst, occurence, nenv, ad, m, replacing)

                        member this.NotifyMethodGroupNameResolution(pos, item, itemGroup, itemTyparInst, occurence, nenv, ad, m, replacing) =
                            notifyNameResolution (pos, item, itemGroup, itemTyparInst, occurence, nenv, ad, m, replacing)

                        member this.NotifyExprHasType(_, _, _, _) = assert false // no expr typings in MakeAndPublishSimpleVals
                        member this.NotifyFormatSpecifierLocation(_, _) = ()
                        member this.NotifyOpenDeclaration(_) = ()
                        member this.CurrentSourceText = None
                        member this.FormatStringCheckContext = None }

                use _h = WithNewTypecheckResultsSink(sink, cenv.tcSink)
                MakeAndPublishSimpleVals cenv env names

            if nameResolutions.Count <> 0 then
                let (_, _, _, _, _, _, ad, m1, _replacing) = nameResolutions.[0]
                // mergedNameEnv - name resolution env that contains all names
                // mergedRange - union of ranges of names
                let mergedNameEnv, mergedRange =
                    ((env.NameEnv, m1), nameResolutions) ||> Seq.fold (fun (nenv, merged) (_, item, _, _, _, _, _, m, _) ->
                        // MakeAndPublishVal creates only Item.Value
                        let item = match item with Item.Value item -> item | _ -> failwith "impossible"
                        (AddFakeNamedValRefToNameEnv item.DisplayName nenv item), (unionRanges m merged)
                        )
                // send notification about mergedNameEnv
                CallEnvSink cenv.tcSink (mergedRange, mergedNameEnv, ad)
                // call CallNameResolutionSink for all captured name resolutions using mergedNameEnv
                for (_, item, itemGroup, itemTyparInst, occurence, _nenv, ad, m, _replacing) in nameResolutions do
                    CallMethodGroupNameResolutionSink cenv.tcSink (m, mergedNameEnv, item, itemGroup, itemTyparInst, occurence, ad)

            values, vspecMap

    let envinner = AddLocalValMap cenv.g cenv.tcSink m vspecMap env
    envinner, values, vspecMap

//-------------------------------------------------------------------------
// Helpers to freshen existing types and values, i.e. when a reference
// to C<_> occurs then generate C<?ty> for a fresh type inference variable ?ty.
//-------------------------------------------------------------------------

let FreshenTyconRef m rigid (tcref: TyconRef) declaredTyconTypars =
    let tpsorig = declaredTyconTypars
    let tps = copyTypars tpsorig
    if rigid <> TyparRigidity.Rigid then
      tps |> List.iter (fun tp -> tp.SetRigidity rigid)

    let renaming, tinst = FixupNewTypars m [] [] tpsorig tps
    (TType_app(tcref, List.map mkTyparTy tpsorig), tps, renaming, TType_app(tcref, tinst))

let FreshenPossibleForallTy g m rigid ty =
    let tpsorig, tau = tryDestForallTy g ty
    if isNil tpsorig then
        [], [], [], tau
    else
        // tps may be have been equated to other tps in equi-recursive type inference and units-of-measure type inference. Normalize them here
        let tpsorig = NormalizeDeclaredTyparsForEquiRecursiveInference g tpsorig
        let tps, renaming, tinst = CopyAndFixupTypars m rigid tpsorig
        tpsorig, tps, tinst, instType renaming tau

let FreshenTyconRef2 m (tcref: TyconRef) =
    let tps, renaming, tinst = FreshenTypeInst m (tcref.Typars m)
    tps, renaming, tinst, TType_app (tcref, tinst)


/// Given a abstract method, which may be a generic method, freshen the type in preparation
/// to apply it as a constraint to the method that implements the abstract slot
let FreshenAbstractSlot g amap m synTyparDecls absMethInfo =

    // Work out if an explicit instantiation has been given. If so then the explicit type
    // parameters will be made rigid and checked for generalization. If not then auto-generalize
    // by making the copy of the type parameters on the virtual being overridden rigid.

    let typarsFromAbsSlotAreRigid =

        match synTyparDecls with
        | ValTyparDecls(synTypars, _, infer) ->
            if infer && not (isNil synTypars) then
                errorR(Error(FSComp.SR.tcOverridingMethodRequiresAllOrNoTypeParameters(), m))

            isNil synTypars

    let (CompiledSig (argTys, retTy, fmtps, _)) = CompiledSigOfMeth g amap m absMethInfo

    // If the virtual method is a generic method then copy its type parameters
    let typarsFromAbsSlot, typarInstFromAbsSlot, _ =
        let ttps = absMethInfo.GetFormalTyparsOfDeclaringType m
        let ttinst = argsOfAppTy g absMethInfo.ApparentEnclosingType
        let rigid = if typarsFromAbsSlotAreRigid then TyparRigidity.Rigid else TyparRigidity.Flexible
        ConstraintSolver.FreshenAndFixupTypars m rigid ttps ttinst fmtps

    // Work out the required type of the member
    let argTysFromAbsSlot = argTys |> List.mapSquared (instType typarInstFromAbsSlot)
    let retTyFromAbsSlot = retTy |> GetFSharpViewOfReturnType g |> instType typarInstFromAbsSlot
    typarsFromAbsSlotAreRigid, typarsFromAbsSlot, argTysFromAbsSlot, retTyFromAbsSlot


//-------------------------------------------------------------------------
// Helpers to typecheck expressions and patterns
//-------------------------------------------------------------------------

let BuildFieldMap cenv env isPartial ty flds m =
    let ad = env.eAccessRights
    if isNil flds then invalidArg "flds" "BuildFieldMap"
    let fldCount = flds.Length

    let frefSets =
        let allFields = flds |> List.map (fun ((_, ident), _) -> ident)
        flds
        |> List.map (fun (fld, fldExpr) ->
            let frefSet = ResolveField cenv.tcSink cenv.nameResolver env.eNameResEnv ad ty fld allFields
            fld, frefSet, fldExpr)

    let relevantTypeSets =
        frefSets |> List.map (fun (_, frefSet, _) -> frefSet |> List.map (fun (FieldResolution(rfinfo, _)) -> rfinfo.TypeInst, rfinfo.TyconRef))

    let tinst, tcref =
        match List.fold (ListSet.intersect (fun (_, tcref1) (_, tcref2) -> tyconRefEq cenv.g tcref1 tcref2)) (List.head relevantTypeSets) (List.tail relevantTypeSets) with
        | [tinst, tcref] -> tinst, tcref
        | tcrefs ->
            if isPartial then
                warning (Error(FSComp.SR.tcFieldsDoNotDetermineUniqueRecordType(), m))

            // try finding a record type with the same number of fields as the ones that are given.
            match tcrefs |> List.tryFind (fun (_, tc) -> tc.TrueFieldsAsList.Length = fldCount) with
            | Some (tinst, tcref) -> tinst, tcref
            | _ ->
                // OK, there isn't a unique, good type dictated by the intersection for the field refs.
                // We're going to get an error of some kind below.
                // Just choose one field ref and let the error come later
                let (_, frefSet1, _) = List.head frefSets
                let (FieldResolution(rfinfo1, _)) = List.head frefSet1
                rfinfo1.TypeInst, rfinfo1.TyconRef

    let fldsmap, rfldsList =
        ((Map.empty, []), frefSets) ||> List.fold (fun (fs, rfldsList) (fld, frefs, fldExpr) ->
                match frefs |> List.filter (fun (FieldResolution(rfinfo2, _)) -> tyconRefEq cenv.g tcref rfinfo2.TyconRef) with
                | [FieldResolution(rfinfo2, showDeprecated)] ->

                    // Record the precise resolution of the field for intellisense
                    let item = Item.RecdField(rfinfo2)
                    CallNameResolutionSink cenv.tcSink ((snd fld).idRange, env.NameEnv, item, emptyTyparInst, ItemOccurence.Use, ad)

                    let fref2 = rfinfo2.RecdFieldRef
                    CheckRecdFieldAccessible cenv.amap m env.eAccessRights fref2 |> ignore
                    CheckFSharpAttributes cenv.g fref2.PropertyAttribs m |> CommitOperationResult
                    if Map.containsKey fref2.FieldName fs then
                        errorR (Error(FSComp.SR.tcFieldAppearsTwiceInRecord(fref2.FieldName), m))
                    if showDeprecated then
                        warning(Deprecated(FSComp.SR.nrRecordTypeNeedsQualifiedAccess(fref2.FieldName, fref2.Tycon.DisplayName) |> snd, m))

                    if not (tyconRefEq cenv.g tcref fref2.TyconRef) then
                        let (_, frefSet1, _) = List.head frefSets
                        let (FieldResolution(rfinfo1, _)) = List.head frefSet1
                        errorR (FieldsFromDifferentTypes(env.DisplayEnv, rfinfo1.RecdFieldRef, fref2, m))
                        fs, rfldsList
                    else
                        Map.add fref2.FieldName fldExpr fs, (fref2.FieldName, fldExpr) :: rfldsList

                | _ -> error(Error(FSComp.SR.tcRecordFieldInconsistentTypes(), m)))
    tinst, tcref, fldsmap, List.rev rfldsList

let rec ApplyUnionCaseOrExn (makerForUnionCase, makerForExnTag) m cenv env overallTy item =
    let ad = env.eAccessRights
    match item with
    | Item.ExnCase ecref ->
        CheckEntityAttributes cenv.g ecref m |> CommitOperationResult
        UnifyTypes cenv env m overallTy cenv.g.exn_ty
        CheckTyconAccessible cenv.amap m ad ecref |> ignore
        let mkf = makerForExnTag ecref
        mkf, recdFieldTysOfExnDefRef ecref, [ for f in (recdFieldsOfExnDefRef ecref) -> f.Id ]

    | Item.UnionCase(ucinfo, showDeprecated) ->
        if showDeprecated then
            warning(Deprecated(FSComp.SR.nrUnionTypeNeedsQualifiedAccess(ucinfo.Name, ucinfo.Tycon.DisplayName) |> snd, m))

        let ucref = ucinfo.UnionCaseRef
        CheckUnionCaseAttributes cenv.g ucref m |> CommitOperationResult
        CheckUnionCaseAccessible cenv.amap m ad ucref |> ignore
        let gtyp2 = actualResultTyOfUnionCase ucinfo.TypeInst ucref
        let inst = mkTyparInst ucref.TyconRef.TyparsNoRange ucinfo.TypeInst
        UnifyTypes cenv env m overallTy gtyp2
        let mkf = makerForUnionCase(ucref, ucinfo.TypeInst)
        mkf, actualTysOfUnionCaseFields inst ucref, [ for f in ucref.AllFieldsAsList -> f.Id ]
    | _ -> invalidArg "item" "not a union case or exception reference"

let ApplyUnionCaseOrExnTypes m cenv env overallTy c =
  ApplyUnionCaseOrExn ((fun (a, b) mArgs args -> mkUnionCaseExpr(a, b, args, unionRanges m mArgs)),
                       (fun a mArgs args -> mkExnExpr (a, args, unionRanges m mArgs))) m cenv env overallTy c

let ApplyUnionCaseOrExnTypesForPat m cenv env overallTy c =
  ApplyUnionCaseOrExn ((fun (a, b) mArgs args -> TPat_unioncase(a, b, args, unionRanges m mArgs)),
                       (fun a mArgs args -> TPat_exnconstr(a, args, unionRanges m mArgs))) m cenv env overallTy c

let UnionCaseOrExnCheck (env: TcEnv) numArgTys numArgs m =
  if numArgs <> numArgTys then error (UnionCaseWrongArguments(env.DisplayEnv, numArgTys, numArgs, m))

let TcUnionCaseOrExnField cenv (env: TcEnv) ty1 m c n funcs =
    let ad = env.eAccessRights
    let mkf, argTys, _argNames =
      match ResolvePatternLongIdent cenv.tcSink cenv.nameResolver AllIdsOK false m ad env.eNameResEnv TypeNameResolutionInfo.Default c with
      | (Item.UnionCase _ | Item.ExnCase _) as item ->
        ApplyUnionCaseOrExn funcs m cenv env ty1 item
      | _ -> error(Error(FSComp.SR.tcUnknownUnion(), m))
    let argstysLength = List.length argTys
    if n >= argstysLength then
      error (UnionCaseWrongNumberOfArgs(env.DisplayEnv, argstysLength, n, m))
    let ty2 = List.item n argTys
    mkf, ty2

//-------------------------------------------------------------------------
// Helpers for generalizing type variables
//-------------------------------------------------------------------------

type GeneralizeConstrainedTyparOptions =
    | CanGeneralizeConstrainedTypars
    | DoNotGeneralizeConstrainedTypars


module GeneralizationHelpers =
    let ComputeUngeneralizableTypars env =

        let acc = Collections.Generic.List()
        for item in env.eUngeneralizableItems do
            if not item.WillNeverHaveFreeTypars then
                let ftps = item.GetFreeTyvars().FreeTypars
                if not ftps.IsEmpty then
                    for ftp in ftps do
                        acc.Add ftp

        Zset.Create(typarOrder, acc)


    let ComputeUnabstractableTycons env =
        let accInFreeItem acc (item: UngeneralizableItem) =
            let ftycs =
                if item.WillNeverHaveFreeTypars then item.CachedFreeLocalTycons else
                let ftyvs = item.GetFreeTyvars()
                ftyvs.FreeTycons
            if ftycs.IsEmpty then acc else unionFreeTycons ftycs acc

        List.fold accInFreeItem emptyFreeTycons env.eUngeneralizableItems

    let ComputeUnabstractableTraitSolutions env =
        let accInFreeItem acc (item: UngeneralizableItem) =
            let ftycs =
                if item.WillNeverHaveFreeTypars then item.CachedFreeTraitSolutions else
                let ftyvs = item.GetFreeTyvars()
                ftyvs.FreeTraitSolutions
            if ftycs.IsEmpty then acc else unionFreeLocals ftycs acc

        List.fold accInFreeItem emptyFreeLocals env.eUngeneralizableItems

    let rec IsGeneralizableValue g t =
        match t with
        | Expr.Lambda _ | Expr.TyLambda _ | Expr.Const _ -> true

        // let f(x: byref<int>) = let v = &x in ... shouldn't generalize "v"
        | Expr.Val (vref, _, m) -> not (isByrefLikeTy g m vref.Type)

        // Look through coercion nodes corresponding to introduction of subsumption
        | Expr.Op (TOp.Coerce, [inputTy;actualTy], [e1], _) when isFunTy g actualTy && isFunTy g inputTy ->
            IsGeneralizableValue g e1

        | Expr.Op (op, _, args, _) ->

            let canGeneralizeOp =
                match op with
                | TOp.Tuple _ -> true
                | TOp.UnionCase uc -> not (isUnionCaseRefDefinitelyMutable uc)
                | TOp.Recd (ctorInfo, tcref) ->
                    match ctorInfo with
                    | RecdExpr -> not (isRecdOrUnionOrStructTyconRefDefinitelyMutable tcref)
                    | RecdExprIsObjInit -> false
                | TOp.Array -> isNil args
                | TOp.ExnConstr ec -> not (isExnDefinitelyMutable ec)
                | TOp.ILAsm ([], _) -> true
                | _ -> false

            canGeneralizeOp && List.forall (IsGeneralizableValue g) args

        | Expr.LetRec (binds, body, _, _) ->
            binds |> List.forall (fun b -> not b.Var.IsMutable) &&
            binds |> List.forall (fun b -> IsGeneralizableValue g b.Expr) &&
            IsGeneralizableValue g body

        | Expr.Let (bind, body, _, _) ->
            not bind.Var.IsMutable &&
            IsGeneralizableValue g bind.Expr &&
            IsGeneralizableValue g body

        // Applications of type functions are _not_ normally generalizable unless explicitly marked so
        | Expr.App (Expr.Val (vref, _, _), _, _, [], _) when vref.IsTypeFunction ->
            HasFSharpAttribute g g.attrib_GeneralizableValueAttribute vref.Attribs

        | Expr.App (e1, _, _, [], _) -> IsGeneralizableValue g e1
        | Expr.TyChoose (_, b, _) -> IsGeneralizableValue g b
        | Expr.Obj (_, ty, _, _, _, _, _) -> isInterfaceTy g ty || isDelegateTy g ty
        | Expr.Link eref -> IsGeneralizableValue g !eref

        | _ -> false

    let CanGeneralizeConstrainedTyparsForDecl declKind =
        if DeclKind.CanGeneralizeConstrainedTypars declKind
        then CanGeneralizeConstrainedTypars
        else DoNotGeneralizeConstrainedTypars

    /// Recursively knock out typars we can't generalize.
    /// For non-generalized type variables be careful to iteratively knock out
    /// both the typars and any typars free in the constraints of the typars
    /// into the set that are considered free in the environment. 
    let rec TrimUngeneralizableTypars genConstrainedTyparFlag inlineFlag (generalizedTypars: Typar list) freeInEnv = 
        // Do not generalize type variables with a static requirement unless function is marked 'inline' 
        let generalizedTypars, ungeneralizableTypars1 =  
            if inlineFlag = ValInline.Always then generalizedTypars, []
            else generalizedTypars |> List.partition (fun tp -> tp.StaticReq = TyparStaticReq.None) 

        // Do not generalize type variables which would escape their scope
        // because they are free in the environment
        let generalizedTypars, ungeneralizableTypars2 =
            List.partition (fun x -> not (Zset.contains x freeInEnv)) generalizedTypars

        // Some situations, e.g. implicit class constructions that represent functions as fields,
        // do not allow generalisation over constrained typars. (since they can not be represented as fields)
        //
        // Don't generalize IsCompatFlex type parameters to avoid changing inferred types.
        let generalizedTypars, ungeneralizableTypars3 =
            generalizedTypars
            |> List.partition (fun tp ->
                (genConstrainedTyparFlag = CanGeneralizeConstrainedTypars || tp.Constraints.IsEmpty) &&
                not tp.IsCompatFlex)

        if isNil ungeneralizableTypars1 && isNil ungeneralizableTypars2 && isNil ungeneralizableTypars3 then
            generalizedTypars, freeInEnv
        else
            let freeInEnv =
                unionFreeTypars
                    (accFreeInTypars CollectAllNoCaching ungeneralizableTypars1
                        (accFreeInTypars CollectAllNoCaching ungeneralizableTypars2
                            (accFreeInTypars CollectAllNoCaching ungeneralizableTypars3 emptyFreeTyvars))).FreeTypars
                    freeInEnv
            TrimUngeneralizableTypars genConstrainedTyparFlag inlineFlag generalizedTypars freeInEnv

    /// Condense type variables in positive position
    let CondenseTypars (cenv, denv: DisplayEnv, generalizedTypars: Typars, tauTy, m) =

        // The type of the value is ty11 * ... * ty1N -> ... -> tyM1 * ... * tyMM -> retTy
        // This is computed REGARDLESS of the arity of the expression.
        let curriedArgTys, retTy = stripFunTy cenv.g tauTy
        let allUntupledArgTys = curriedArgTys |> List.collect (tryDestRefTupleTy cenv.g)

        // Compute the type variables in 'retTy'
        let returnTypeFreeTypars = freeInTypeLeftToRight cenv.g false retTy
        let allUntupledArgTysWithFreeVars = allUntupledArgTys |> List.map (fun ty -> (ty, freeInTypeLeftToRight cenv.g false ty))

        let relevantUniqueSubtypeConstraint (tp: Typar) =
            // Find a single subtype constraint
            match tp.Constraints |> List.partition (function (TyparConstraint.CoercesTo _) -> true | _ -> false) with
            | [TyparConstraint.CoercesTo(cxty, _)], others ->
                 // Throw away null constraints if they are implied
                 if others |> List.exists (function (TyparConstraint.SupportsNull(_)) -> not (TypeSatisfiesNullConstraint cenv.g m cxty) | _ -> true)
                 then None
                 else Some cxty
            | _ -> None


        // Condensation typars can't be used in the constraints of any candidate condensation typars. So compute all the
        // typars free in the constraints of tyIJ

        let lhsConstraintTypars =
            allUntupledArgTys |> List.collect (fun ty ->
                match tryDestTyparTy cenv.g ty with
                | ValueSome tp ->
                    match relevantUniqueSubtypeConstraint tp with
                    | Some cxty -> freeInTypeLeftToRight cenv.g false cxty
                    | None -> []
                | _ -> [])

        let IsCondensationTypar (tp: Typar) =
            // A condensation typar may not a user-generated type variable nor has it been unified with any user type variable
            (tp.DynamicReq = TyparDynamicReq.No) &&
            // A condensation typar must have a single constraint "'a :> A"
            (Option.isSome (relevantUniqueSubtypeConstraint tp)) &&
            // This is type variable is not used on the r.h.s. of the type
            not (ListSet.contains typarEq tp returnTypeFreeTypars) &&
            // A condensation typar can't be used in the constraints of any candidate condensation typars
            not (ListSet.contains typarEq tp lhsConstraintTypars) &&
            // A condensation typar must occur precisely once in tyIJ, and must not occur free in any other tyIJ
            (match allUntupledArgTysWithFreeVars |> List.partition (fun (ty, _) -> match tryDestTyparTy cenv.g ty with ValueSome destTypar -> typarEq destTypar tp | _ -> false) with
             | [_], rest -> not (rest |> List.exists (fun (_, fvs) -> ListSet.contains typarEq tp fvs))
             | _ -> false)

        let condensationTypars, generalizedTypars = generalizedTypars |> List.partition IsCondensationTypar

        // Condensation solves type variables eagerly and removes them from the generalization set
        condensationTypars |> List.iter (fun tp ->
            ConstraintSolver.ChooseTyparSolutionAndSolve cenv.css denv tp)
        generalizedTypars

    let ComputeAndGeneralizeGenericTypars (cenv,
                                           denv: DisplayEnv,
                                           m,
                                           freeInEnv: FreeTypars,
                                           canInferTypars,
                                           genConstrainedTyparFlag,
                                           inlineFlag,
                                           exprOpt,
                                           allDeclaredTypars: Typars,
                                           maxInferredTypars: Typars,
                                           tauTy,
                                           resultFirst) =

        let allDeclaredTypars = NormalizeDeclaredTyparsForEquiRecursiveInference cenv.g allDeclaredTypars
        let typarsToAttemptToGeneralize =
            if (match exprOpt with None -> true | Some e -> IsGeneralizableValue cenv.g e)
            then (ListSet.unionFavourLeft typarEq allDeclaredTypars maxInferredTypars)
            else allDeclaredTypars

        let generalizedTypars, freeInEnv =
            TrimUngeneralizableTypars genConstrainedTyparFlag inlineFlag typarsToAttemptToGeneralize freeInEnv

        allDeclaredTypars
        |> List.iter (fun tp ->
            if Zset.memberOf freeInEnv tp then
                let ty = mkTyparTy tp
                error(Error(FSComp.SR.tcNotSufficientlyGenericBecauseOfScope(NicePrint.prettyStringOfTy denv ty), m)))

        let generalizedTypars = CondenseTypars(cenv, denv, generalizedTypars, tauTy, m)

        let generalizedTypars =
            if canInferTypars then generalizedTypars
            else generalizedTypars |> List.filter (fun tp -> ListSet.contains typarEq tp allDeclaredTypars)

        let allConstraints = List.collect (fun (tp: Typar) -> tp.Constraints) generalizedTypars
        let generalizedTypars = ConstraintSolver.SimplifyMeasuresInTypeScheme cenv.g resultFirst generalizedTypars tauTy allConstraints

        // Generalization turns inference type variables into rigid, quantified type variables,
        // (they may be rigid already)
        generalizedTypars |> List.iter (SetTyparRigid denv m)

        // Generalization removes constraints related to generalized type variables
        EliminateConstraintsForGeneralizedTypars denv cenv.css m NoTrace generalizedTypars

        generalizedTypars

    //-------------------------------------------------------------------------
    // Helpers to freshen existing types and values, i.e. when a reference
    // to C<_> occurs then generate C<?ty> for a fresh type inference variable ?ty.
    //-------------------------------------------------------------------------

    let CheckDeclaredTyparsPermitted (memFlagsOpt: SynMemberFlags option, declaredTypars, m) =
        match memFlagsOpt with
        | None -> ()
        | Some memberFlags ->
            match memberFlags.MemberKind with
            // can't infer extra polymorphism for properties
            | SynMemberKind.PropertyGet
            | SynMemberKind.PropertySet ->
                 if not (isNil declaredTypars) then
                     errorR(Error(FSComp.SR.tcPropertyRequiresExplicitTypeParameters(), m))
            | SynMemberKind.Constructor ->
                 if not (isNil declaredTypars) then
                     errorR(Error(FSComp.SR.tcConstructorCannotHaveTypeParameters(), m))
            | _ -> ()

    /// Properties and Constructors may only generalize the variables associated with the containing class (retrieved from the 'this' pointer)
    /// Also check they don't declare explicit typars.
    let ComputeCanInferExtraGeneralizableTypars (parentRef, canInferTypars, memFlagsOpt: SynMemberFlags option) =
        canInferTypars &&
        (match memFlagsOpt with
         | None -> true
         | Some memberFlags ->
            match memberFlags.MemberKind with
            // can't infer extra polymorphism for properties
            | SynMemberKind.PropertyGet | SynMemberKind.PropertySet -> false
            // can't infer extra polymorphism for class constructors
            | SynMemberKind.ClassConstructor -> false
            // can't infer extra polymorphism for constructors
            | SynMemberKind.Constructor -> false
            // feasible to infer extra polymorphism
            | _ -> true) &&
        (match parentRef with
         | Parent tcref -> not tcref.IsFSharpDelegateTycon
         | _ -> true) // no generic parameters inferred for 'Invoke' method



//-------------------------------------------------------------------------
// ComputeInlineFlag
//-------------------------------------------------------------------------

let ComputeInlineFlag (memFlagsOption: SynMemberFlags option) isInline isMutable m =
    let inlineFlag =
        // Mutable values may never be inlined
        // Constructors may never be inlined
        // Calls to virtual/abstract slots may never be inlined
        if isMutable ||
           (match memFlagsOption with
            | None -> false
            | Some x -> (x.MemberKind = SynMemberKind.Constructor) || x.IsDispatchSlot || x.IsOverrideOrExplicitImpl) 
        then ValInline.Never 
        elif isInline then ValInline.Always 
        else ValInline.Optional
    if isInline && (inlineFlag <> ValInline.Always) then 
        errorR(Error(FSComp.SR.tcThisValueMayNotBeInlined(), m))
    inlineFlag


//-------------------------------------------------------------------------
// Binding normalization.
//
// Determine what sort of value is being bound (normal value, instance
// member, normal function, static member etc.) and make some
// name-resolution-sensitive adjustments to the syntax tree.
//
// One part of this "normalization" ensures:
//        "let SynPat.LongIdent(f) = e" when f not a datatype constructor --> let Pat_var(f) = e"
//        "let SynPat.LongIdent(f) pat = e" when f not a datatype constructor --> let Pat_var(f) = \pat. e"
//        "let (SynPat.LongIdent(f) : ty) = e" when f not a datatype constructor --> let (Pat_var(f) : ty) = e"
//        "let (SynPat.LongIdent(f) : ty) pat = e" when f not a datatype constructor --> let (Pat_var(f) : ty) = \pat. e"
//
// This is because the first lambda in a function definition "let F x = e"
// now looks like a constructor application, i.e. let (F x) = e ...
// also let A.F x = e ...
// also let f x = e ...
//
// The other parts turn property definitions into method definitions.
//-------------------------------------------------------------------------


// NormalizedBindingRhs records the r.h.s. of a binding after some munging just before type checking.
// NOTE: This is a bit of a mess. In the early implementation of F# we decided
// to have the parser convert "let f x = e" into
// "let f = fun x -> e". This is called "pushing" a pattern across to the right hand side. Complex
// patterns (e.g. non-tuple patterns) result in a computation on the right.
// However, this approach really isn't that great - especially since
// the language is now considerably more complex, e.g. we use
// type information from the first (but not the second) form in
// type inference for recursive bindings, and the first form
// may specify .NET attributes for arguments. There are still many
// relics of this approach around, e.g. the expression in BindingRhs
// below is of the second form. However, to extract relevant information
// we keep a record of the pats and optional explicit return type already pushed
// into expression so we can use any user-given type information from these
type NormalizedBindingRhs =
    | NormalizedBindingRhs of
        simplePats: SynSimplePats list *
        returnTyOpt: SynBindingReturnInfo option *
        rhsExpr: SynExpr

let PushOnePatternToRhs (cenv: cenv) isMember p (NormalizedBindingRhs(spatsL, rtyOpt, rhsExpr)) =
    let spats, rhsExpr = PushPatternToExpr cenv.synArgNameGenerator isMember p rhsExpr
    NormalizedBindingRhs(spats :: spatsL, rtyOpt, rhsExpr)

type NormalizedBindingPatternInfo =
    NormalizedBindingPat of SynPat * NormalizedBindingRhs * SynValData * SynValTyparDecls

/// Represents a syntactic, unchecked binding after the resolution of the name resolution status of pattern
/// constructors and after "pushing" all complex patterns to the right hand side.
type NormalizedBinding =
  | NormalizedBinding of
      visibility: SynAccess option *
      kind: SynBindingKind *
      mustInline: bool *
      isMutable: bool *
      attribs: SynAttribute list *
      xmlDoc: XmlDoc *
      typars: SynValTyparDecls *
      valSynData: SynValData *
      pat: SynPat *
      rhsExpr: NormalizedBindingRhs *
      mBinding: range *
      spBinding: DebugPointAtBinding

type IsObjExprBinding =
    | ObjExprBinding
    | ValOrMemberBinding

module BindingNormalization =
    /// Push a bunch of pats at once. They may contain patterns, e.g. let f (A x) (B y) = ... 
    /// In this case the semantics is let f a b = let A x = a in let B y = b 
    let private PushMultiplePatternsToRhs (cenv: cenv) isMember pats (NormalizedBindingRhs(spatsL, rtyOpt, rhsExpr)) = 
        let spatsL2, rhsExpr = PushCurriedPatternsToExpr cenv.synArgNameGenerator rhsExpr.Range isMember pats rhsExpr
        NormalizedBindingRhs(spatsL2@spatsL, rtyOpt, rhsExpr)


    let private MakeNormalizedStaticOrValBinding cenv isObjExprBinding id vis typars args rhsExpr valSynData =
        let (SynValData(memberFlagsOpt, _, _)) = valSynData
        NormalizedBindingPat(mkSynPatVar vis id, PushMultiplePatternsToRhs cenv ((isObjExprBinding = ObjExprBinding) || Option.isSome memberFlagsOpt) args rhsExpr, valSynData, typars)

    let private MakeNormalizedInstanceMemberBinding cenv thisId memberId toolId vis m typars args rhsExpr valSynData =
        NormalizedBindingPat(SynPat.InstanceMember(thisId, memberId, toolId, vis, m), PushMultiplePatternsToRhs cenv true args rhsExpr, valSynData, typars)

    let private NormalizeStaticMemberBinding cenv (memberFlags: SynMemberFlags) valSynData id vis typars args m rhsExpr =
        let (SynValData(_, valSynInfo, thisIdOpt)) = valSynData
        if memberFlags.IsInstance then
            // instance method without adhoc "this" argument
            error(Error(FSComp.SR.tcInstanceMemberRequiresTarget(), m))
        match args, memberFlags.MemberKind with
        | _, SynMemberKind.PropertyGetSet -> error(Error(FSComp.SR.tcUnexpectedPropertyInSyntaxTree(), m))
        | [], SynMemberKind.ClassConstructor -> error(Error(FSComp.SR.tcStaticInitializerRequiresArgument(), m))
        | [], SynMemberKind.Constructor -> error(Error(FSComp.SR.tcObjectConstructorRequiresArgument(), m))
        | [_], SynMemberKind.ClassConstructor
        | [_], SynMemberKind.Constructor -> MakeNormalizedStaticOrValBinding cenv ValOrMemberBinding id vis typars args rhsExpr valSynData
        // Static property declared using 'static member P = expr': transformed to a method taking a "unit" argument
        // static property: these transformed into methods taking one "unit" argument
        | [], SynMemberKind.Member ->
            let memberFlags = {memberFlags with MemberKind = SynMemberKind.PropertyGet}
            let valSynData = SynValData(Some memberFlags, valSynInfo, thisIdOpt)
            NormalizedBindingPat(mkSynPatVar vis id,
                                 PushOnePatternToRhs cenv true (SynPat.Const(SynConst.Unit, m)) rhsExpr,
                                 valSynData,
                                 typars)
        | _ -> MakeNormalizedStaticOrValBinding cenv ValOrMemberBinding id vis typars args rhsExpr valSynData

    let private NormalizeInstanceMemberBinding cenv (memberFlags: SynMemberFlags) valSynData thisId memberId (toolId: Ident option) vis typars args m rhsExpr =
        let (SynValData(_, valSynInfo, thisIdOpt)) = valSynData
        if not memberFlags.IsInstance then
            // static method with adhoc "this" argument
            error(Error(FSComp.SR.tcStaticMemberShouldNotHaveThis(), m))
        match args, memberFlags.MemberKind with
        | _, SynMemberKind.ClassConstructor -> error(Error(FSComp.SR.tcExplicitStaticInitializerSyntax(), m))
        | _, SynMemberKind.Constructor -> error(Error(FSComp.SR.tcExplicitObjectConstructorSyntax(), m))
        | _, SynMemberKind.PropertyGetSet -> error(Error(FSComp.SR.tcUnexpectedPropertySpec(), m))
        // Instance property declared using 'x.Member': transformed to methods taking a "this" and a "unit" argument
        // We push across the 'this' arg in mk_rec_binds
        | [], SynMemberKind.Member ->
            let memberFlags = {memberFlags with MemberKind = SynMemberKind.PropertyGet}
            NormalizedBindingPat
                (SynPat.InstanceMember(thisId, memberId, toolId, vis, m),
                 PushOnePatternToRhs cenv true (SynPat.Const(SynConst.Unit, m)) rhsExpr,
                 // Update the member info to record that this is a SynMemberKind.PropertyGet
                 SynValData(Some memberFlags, valSynInfo, thisIdOpt),
                 typars)

        | _ -> MakeNormalizedInstanceMemberBinding cenv thisId memberId toolId vis m typars args rhsExpr valSynData

    let private NormalizeBindingPattern cenv nameResolver isObjExprBinding (env: TcEnv) valSynData pat rhsExpr =
        let ad = env.AccessRights
        let (SynValData(memberFlagsOpt, _, _)) = valSynData
        let rec normPattern pat =
            // One major problem with versions of F# prior to 1.9.x was that data constructors easily 'pollute' the namespace
            // of available items, to the point that you can't even define a function with the same name as an existing union case.
            match pat with
            | SynPat.FromParseError(p, _) -> normPattern p
            | SynPat.LongIdent (LongIdentWithDots(longId, _), toolId, tyargs, SynArgPats.Pats args, vis, m) ->
                let typars = match tyargs with None -> inferredTyparDecls | Some typars -> typars
                match memberFlagsOpt with
                | None ->
                    match ResolvePatternLongIdent cenv.tcSink nameResolver AllIdsOK true m ad env.NameEnv TypeNameResolutionInfo.Default longId with
                    | Item.NewDef id ->
                        if id.idText = opNameCons then
                            NormalizedBindingPat(pat, rhsExpr, valSynData, typars)
                        else
                            if isObjExprBinding = ObjExprBinding then
                                errorR(Deprecated(FSComp.SR.tcObjectExpressionFormDeprecated(), m))
                            MakeNormalizedStaticOrValBinding cenv isObjExprBinding id vis typars args rhsExpr valSynData
                    | _ ->
                        error(Error(FSComp.SR.tcInvalidDeclaration(), m))

                | Some memberFlags ->
                    match longId with
                    // x.Member in member binding patterns.
                    | [thisId;memberId] -> NormalizeInstanceMemberBinding cenv memberFlags valSynData thisId memberId toolId vis typars args m rhsExpr
                    | [memberId] ->
                        if memberFlags.IsInstance then
                            // instance method without adhoc "this" argument
                            errorR(Error(FSComp.SR.tcInstanceMemberRequiresTarget(), memberId.idRange))
                            let thisId = ident ("_", m)
                            NormalizeInstanceMemberBinding cenv memberFlags valSynData thisId memberId toolId vis typars args m rhsExpr
                        else
                            NormalizeStaticMemberBinding cenv memberFlags valSynData memberId vis typars args m rhsExpr
                    | _ -> NormalizedBindingPat(pat, rhsExpr, valSynData, typars)

            // Object constructors are normalized in TcLetrec
            // Here we are normalizing member definitions with simple (not long) ids,
            // e.g. "static member x = 3" and "member x = 3" (instance with missing "this." comes through here. It is trapped and generates a warning)
            | SynPat.Named(id, false, vis, m)
                when
                   (match memberFlagsOpt with
                    | None -> false
                    | Some memberFlags ->
                         memberFlags.MemberKind <> SynMemberKind.Constructor &&
                         memberFlags.MemberKind <> SynMemberKind.ClassConstructor) ->
                NormalizeStaticMemberBinding cenv (Option.get memberFlagsOpt) valSynData id vis inferredTyparDecls [] m rhsExpr

            | SynPat.Typed(pat', x, y) ->
                let (NormalizedBindingPat(pat'', e'', valSynData, typars)) = normPattern pat'
                NormalizedBindingPat(SynPat.Typed(pat'', x, y), e'', valSynData, typars)

            | SynPat.Attrib(_, _, m) ->
                error(Error(FSComp.SR.tcAttributesInvalidInPatterns(), m))

            | _ ->
                NormalizedBindingPat(pat, rhsExpr, valSynData, inferredTyparDecls)
        normPattern pat

    let NormalizeBinding isObjExprBinding cenv (env: TcEnv) binding =
        match binding with
        | SynBinding (vis, bkind, isInline, isMutable, Attributes attrs, doc, valSynData, p, retInfo, rhsExpr, mBinding, spBind) ->
            let (NormalizedBindingPat(pat, rhsExpr, valSynData, typars)) =
                NormalizeBindingPattern cenv cenv.nameResolver isObjExprBinding env valSynData p (NormalizedBindingRhs ([], retInfo, rhsExpr))
            let paramNames = Some valSynData.SynValInfo.ArgNames
            let doc = doc.ToXmlDoc(true, paramNames)
            NormalizedBinding(vis, bkind, isInline, isMutable, attrs, doc, typars, valSynData, pat, rhsExpr, mBinding, spBind)

//-------------------------------------------------------------------------
// input is:
//    [<CompileAsEvent>]
//    member x.P with get = fun () -> e
// -->
//    member x.add_P< >(argName) = (e).AddHandler(argName)
//    member x.remove_P< >(argName) = (e).RemoveHandler(argName)

module EventDeclarationNormalization =
    let ConvertSynInfo m (SynValInfo(argInfos, retInfo)) =
       // reconstitute valSynInfo by adding the argument
       let argInfos =
           match argInfos with
           | [[thisArgInfo];[]] -> [[thisArgInfo];SynInfo.unnamedTopArg] // instance property getter
           | [[]] -> [SynInfo.unnamedTopArg] // static property getter
           | _ -> error(BadEventTransformation m)

       // reconstitute valSynInfo
       SynValInfo(argInfos, retInfo)

    // The property x.P becomes methods x.add_P and x.remove_P
    let ConvertMemberFlags (memberFlags: SynMemberFlags) = { memberFlags with MemberKind = SynMemberKind.Member }

    let private ConvertMemberFlagsOpt m memberFlagsOpt =
        match memberFlagsOpt with
        | Some memberFlags -> Some (ConvertMemberFlags memberFlags)
        | _ -> error(BadEventTransformation m)

    let private ConvertSynData m valSynData =
        let (SynValData(memberFlagsOpt, valSynInfo, thisIdOpt)) = valSynData
        let memberFlagsOpt = ConvertMemberFlagsOpt m memberFlagsOpt
        let valSynInfo = ConvertSynInfo m valSynInfo
        SynValData(memberFlagsOpt, valSynInfo, thisIdOpt)

    let rec private RenameBindingPattern f declPattern =
        match declPattern with
        | SynPat.FromParseError(p, _) -> RenameBindingPattern f p
        | SynPat.Typed(pat', _, _) -> RenameBindingPattern f pat'
        | SynPat.Named (id, x2, vis2, m) -> SynPat.Named (ident(f id.idText, id.idRange), x2, vis2, m)
        | SynPat.InstanceMember(thisId, id, toolId, vis2, m) -> SynPat.InstanceMember(thisId, ident(f id.idText, id.idRange), toolId, vis2, m)
        | _ -> error(Error(FSComp.SR.tcOnlySimplePatternsInLetRec(), declPattern.Range))

    /// Some F# bindings syntactically imply additional bindings, notably properties
    /// annotated with [<CLIEvent>]
    let GenerateExtraBindings cenv (bindingAttribs, binding) =
        let (NormalizedBinding(vis1, bindingKind, isInline, isMutable, _, bindingXmlDoc, _synTyparDecls, valSynData, declPattern, bindingRhs, mBinding, spBind)) = binding
        if CompileAsEvent cenv.g bindingAttribs then

            let MakeOne (prefix, target) =
                let declPattern = RenameBindingPattern (fun s -> prefix + s) declPattern
                let argName = "handler"
                // modify the rhs and argument data
                let bindingRhs, valSynData =
                   let (NormalizedBindingRhs(_, _, rhsExpr)) = bindingRhs
                   let m = rhsExpr.Range
                   // reconstitute valSynInfo by adding the argument
                   let valSynData = ConvertSynData m valSynData

                   match rhsExpr with
                   // Detect 'fun () -> e' which results from the compilation of a property getter
                   | SynExpr.Lambda (_, _, SynSimplePats.SimplePats([], _), trueRhsExpr, _, m) ->
                       let rhsExpr = mkSynApp1 (SynExpr.DotGet (SynExpr.Paren (trueRhsExpr, range0, None, m), range0, LongIdentWithDots([ident(target, m)], []), m)) (SynExpr.Ident (ident(argName, m))) m

                       // reconstitute rhsExpr
                       let bindingRhs = NormalizedBindingRhs([], None, rhsExpr)

                       // add the argument to the expression
                       let bindingRhs = PushOnePatternToRhs cenv true (mkSynPatVar None (ident (argName, mBinding))) bindingRhs

                       bindingRhs, valSynData
                   | _ ->
                       error(BadEventTransformation m)

                // reconstitute the binding
                NormalizedBinding(vis1, bindingKind, isInline, isMutable, [], bindingXmlDoc, noInferredTypars, valSynData, declPattern, bindingRhs, mBinding, spBind)

            [ MakeOne ("add_", "AddHandler"); MakeOne ("remove_", "RemoveHandler") ]
        else
            []



/// Make a copy of the "this" type for a generic object type, e.g. List<'T> --> List<'?> for a fresh inference variable.
/// Also adjust the "this" type to take into account whether the type is a struct.
let FreshenObjectArgType cenv m rigid tcref isExtrinsic declaredTyconTypars =
#if EXTENDED_EXTENSION_MEMBERS // indicates if extension members can add additional constraints to type parameters
    let tcrefObjTy, enclosingDeclaredTypars, renaming, objTy = FreshenTyconRef m (if isExtrinsic then TyparRigidity.Flexible else rigid) tcref declaredTyconTypars
#else
    let tcrefObjTy, enclosingDeclaredTypars, renaming, objTy = FreshenTyconRef m rigid tcref declaredTyconTypars
#endif
    // Struct members have a byref 'this' type (unless they are extrinsic extension members)
    let thisTy =
        if not isExtrinsic && tcref.IsStructOrEnumTycon then
            if isRecdOrStructTyReadOnly cenv.g m objTy then
                mkInByrefTy cenv.g objTy
            else
                mkByrefTy cenv.g objTy
        else
            objTy
    tcrefObjTy, enclosingDeclaredTypars, renaming, objTy, thisTy


// The early generalization rule of F# 2.0 can be unsound for members in generic types (Bug DevDiv2 10649).
// It gives rise to types like "Forall T. ?X -> ?Y" where ?X and ?Y are later discovered to involve T.
//
// For example:
//      type C<'T>() =
//          let mutable x = Unchecked.defaultof<_> // unknown inference variable ?X
//          static member A() = x
//                     // At this point A is generalized early to "Forall T. unit -> ?X"
//          static member B1() = C<string>.A()
//                     // At this point during type inference, the return type of C<string>.A() is '?X'
//                     // After type inference, the return type of C<string>.A() is 'string'
//          static member B2() = C<int>.A()
//                     // At this point during type inference, the return type of C<int>.A() is '?X'
//                     // After type inference, the return type of C<int>.A() is 'int'
//          member this.C() = (x: 'T)
//                     // At this point during type inference the type of 'x' is inferred to be 'T'
//
// Here "A" is generalized too early.
//
// Ideally we would simply generalize "A" later, when it is known to be
// sound. However, that can lead to other problems (e.g. some programs that typecheck today would no longer
// be accepted). As a result, we deal with this unsoundness by an adhoc post-type-checking
// consistency check for recursive uses of "A" with explicit instantiations within the recursive
// scope of "A".
let TcValEarlyGeneralizationConsistencyCheck cenv (env: TcEnv) (v: Val, vrec, tinst, vty, tau, m) =
    match vrec with
    | ValInRecScope isComplete when isComplete && not (isNil tinst) ->
        //printfn "pushing post-inference check for '%s', vty = '%s'" v.DisplayName (DebugPrint.showType vty)
        cenv.postInferenceChecks.Add (fun () ->
            //printfn "running post-inference check for '%s'" v.DisplayName
            //printfn "tau = '%s'" (DebugPrint.showType tau)
            //printfn "vty = '%s'" (DebugPrint.showType vty)
            let tpsorig, tau2 = tryDestForallTy cenv.g vty
            //printfn "tau2 = '%s'" (DebugPrint.showType tau2)
            if not (isNil tpsorig) then
              let tpsorig = NormalizeDeclaredTyparsForEquiRecursiveInference cenv.g tpsorig
              let tau3 = instType (mkTyparInst tpsorig tinst) tau2
              //printfn "tau3 = '%s'" (DebugPrint.showType tau3)
              if not (AddCxTypeEqualsTypeUndoIfFailed env.DisplayEnv cenv.css m tau tau3) then
                  let txt = bufs (fun buf -> NicePrint.outputQualifiedValSpec env.DisplayEnv cenv.infoReader buf (mkLocalValRef v))
                  error(Error(FSComp.SR.tcInferredGenericTypeGivesRiseToInconsistency(v.DisplayName, txt), m)))
    | _ -> ()


/// TcVal. "Use" a value, normally at a fresh type instance (unless optInst is
/// given). optInst is set when an explicit type instantiation is given, e.g.
///     Seq.empty<string>
/// In this case the vrefFlags inside optInst are just NormalValUse.
///
/// optInst is is also set when building the final call for a reference to an
/// F# object model member, in which case the optInst is the type instantiation
/// inferred by member overload resolution, and vrefFlags indicate if the
/// member is being used in a special way, i.e. may be one of:
///    | CtorValUsedAsSuperInit "inherit Panel()"
///    | CtorValUsedAsSelfInit "new() = new OwnType(3)"
///    | VSlotDirectCall "base.OnClick(eventArgs)"
let TcVal checkAttributes cenv env tpenv (vref: ValRef) optInst optAfterResolution m =
    let (tpsorig, _, _, _, tinst, _) as res =
        let v = vref.Deref
        let vrec = v.RecursiveValInfo
        v.SetHasBeenReferenced()
        CheckValAccessible m env.eAccessRights vref
        if checkAttributes then
            CheckValAttributes cenv.g vref m |> CommitOperationResult
        let vty = vref.Type
        // byref-typed values get dereferenced
        if isByrefTy cenv.g vty then
            let isSpecial = true
            [], mkAddrGet m vref, isSpecial, destByrefTy cenv.g vty, [], tpenv
        else
          match v.LiteralValue with
          | Some c ->
              // Literal values go to constants
              let isSpecial = true
              // The value may still be generic, e.g.
              //   [<Literal>]
              //   let Null = null
              let tpsorig, _, tinst, tau = FreshenPossibleForallTy cenv.g m TyparRigidity.Flexible vty
              tpsorig, Expr.Const (c, m, tau), isSpecial, tau, tinst, tpenv

          | None ->
                // References to 'this' in classes get dereferenced from their implicit reference cell and poked
              if v.BaseOrThisInfo = CtorThisVal && isRefCellTy cenv.g vty then
                  let exprForVal = exprForValRef m vref
                  //if AreWithinCtorPreConstruct env then
                  //    warning(SelfRefObjCtor(AreWithinImplicitCtor env, m))

                  let ty = destRefCellTy cenv.g vty
                  let isSpecial = true
                  [], mkCallCheckThis cenv.g m ty (mkRefCellGet cenv.g m ty exprForVal), isSpecial, ty, [], tpenv
              else
                  // Instantiate the value
                  let tpsorig, vrefFlags, tinst, tau, tpenv =
                      // Have we got an explicit instantiation?
                      match optInst with
                      // No explicit instantiation (the normal case)
                      | None ->
                          if HasFSharpAttribute cenv.g cenv.g.attrib_RequiresExplicitTypeArgumentsAttribute v.Attribs then
                               errorR(Error(FSComp.SR.tcFunctionRequiresExplicitTypeArguments(v.DisplayName), m))

                          match vrec with
                          | ValInRecScope false ->
                              let tpsorig, tau = vref.TypeScheme
                              let tinst = tpsorig |> List.map mkTyparTy
                              tpsorig, NormalValUse, tinst, tau, tpenv
                          | ValInRecScope true
                          | ValNotInRecScope ->
                              let tpsorig, _, tinst, tau = FreshenPossibleForallTy cenv.g m TyparRigidity.Flexible vty
                              tpsorig, NormalValUse, tinst, tau, tpenv

                      // If we have got an explicit instantiation then use that
                      | Some(vrefFlags, checkTys) ->
                            let checkInst (tinst: TypeInst) =
                                if not v.IsMember && not v.PermitsExplicitTypeInstantiation && not (List.isEmpty tinst) && not (List.isEmpty v.Typars) then
                                    warning(Error(FSComp.SR.tcDoesNotAllowExplicitTypeArguments(v.DisplayName), m))
                            match vrec with
                            | ValInRecScope false ->
                                let tpsorig, tau = vref.TypeScheme
                                let (tinst: TypeInst), tpenv = checkTys tpenv (tpsorig |> List.map (fun tp -> tp.Kind))
                                checkInst tinst
                                if tpsorig.Length <> tinst.Length then error(Error(FSComp.SR.tcTypeParameterArityMismatch(tpsorig.Length, tinst.Length), m))
                                let tau2 = instType (mkTyparInst tpsorig tinst) tau
                                (tpsorig, tinst) ||> List.iter2 (fun tp ty ->
                                    try UnifyTypes cenv env m (mkTyparTy tp) ty
                                    with _ -> error (Recursion(env.DisplayEnv, v.Id, tau2, tau, m)))
                                tpsorig, vrefFlags, tinst, tau2, tpenv
                            | ValInRecScope true
                            | ValNotInRecScope ->
                                let tpsorig, tps, tptys, tau = FreshenPossibleForallTy cenv.g m TyparRigidity.Flexible vty
                                //dprintfn "After Freshen: tau = %s" (LayoutRender.showL (typeL tau))
                                let (tinst: TypeInst), tpenv = checkTys tpenv (tps |> List.map (fun tp -> tp.Kind))
                                checkInst tinst
                                //dprintfn "After Check: tau = %s" (LayoutRender.showL (typeL tau))
                                if tptys.Length <> tinst.Length then error(Error(FSComp.SR.tcTypeParameterArityMismatch(tps.Length, tinst.Length), m))
                                List.iter2 (UnifyTypes cenv env m) tptys tinst
                                TcValEarlyGeneralizationConsistencyCheck cenv env (v, vrec, tinst, vty, tau, m)

                                //dprintfn "After Unify: tau = %s" (LayoutRender.showL (typeL tau))
                                tpsorig, vrefFlags, tinst, tau, tpenv

                  let exprForVal = Expr.Val (vref, vrefFlags, m)
                  let exprForVal = mkTyAppExpr m (exprForVal, vty) tinst
                  let isSpecial =
                      (match vrefFlags with NormalValUse | PossibleConstrainedCall _ -> false | _ -> true) ||
                      valRefEq cenv.g vref cenv.g.splice_expr_vref ||
                      valRefEq cenv.g vref cenv.g.splice_raw_expr_vref

                  let exprForVal = RecordUseOfRecValue cenv vrec vref exprForVal m

                  tpsorig, exprForVal, isSpecial, tau, tinst, tpenv

    match optAfterResolution with
    | Some (AfterResolution.RecordResolution(_, callSink, _, _)) -> callSink (mkTyparInst tpsorig tinst)
    | Some AfterResolution.DoNothing | None -> ()
    res

/// simplified version of TcVal used in calls to BuildMethodCall (typrelns.fs)
/// this function is used on typechecking step for making calls to provided methods and on optimization step (for the same purpose).
let LightweightTcValForUsingInBuildMethodCall g (vref: ValRef) vrefFlags (vrefTypeInst: TTypes) m =
    let v = vref.Deref
    let vty = vref.Type
    // byref-typed values get dereferenced
    if isByrefTy g vty then
        mkAddrGet m vref, destByrefTy g vty
    else
      match v.LiteralValue with
      | Some c ->
          let _, _, _, tau = FreshenPossibleForallTy g m TyparRigidity.Flexible vty
          Expr.Const (c, m, tau), tau
      | None ->
              // Instantiate the value
              let tau =
                  // If we have got an explicit instantiation then use that
                  let _, tps, tptys, tau = FreshenPossibleForallTy g m TyparRigidity.Flexible vty
                  if tptys.Length <> vrefTypeInst.Length then error(Error(FSComp.SR.tcTypeParameterArityMismatch(tps.Length, vrefTypeInst.Length), m))
                  instType (mkTyparInst tps vrefTypeInst) tau

              let exprForVal = Expr.Val (vref, vrefFlags, m)
              let exprForVal = mkTyAppExpr m (exprForVal, vty) vrefTypeInst
              exprForVal, tau

/// Mark points where we decide whether an expression will support automatic
/// decondensation or not. This is somewhat a relic of a previous implementation of decondensation and could
/// be removed

type ApplicableExpr =
    | ApplicableExpr of
           // context
           cenv *
           // the function-valued expression
           Expr *
           // is this the first in an application series
           bool

    member x.Range =
        match x with
        | ApplicableExpr (_, e, _) -> e.Range

    member x.Type =
        match x with
        | ApplicableExpr (cenv, e, _) -> tyOfExpr cenv.g e

    member x.SupplyArgument(e2, m) =
        let (ApplicableExpr (cenv, fe, first)) = x
        let combinedExpr =
            match fe with
            | Expr.App (e1, e1ty, tyargs1, args1, e1m) when
                       (not first || isNil args1) &&
                       (not (isForallTy cenv.g e1ty) || isFunTy cenv.g (applyTys cenv.g e1ty (tyargs1, args1))) ->
                Expr.App (e1, e1ty, tyargs1, args1@[e2], unionRanges e1m m)
            | _ ->
                Expr.App (fe, tyOfExpr cenv.g fe, [], [e2], m)
        ApplicableExpr(cenv, combinedExpr, false)

    member x.Expr =
        match x with
        | ApplicableExpr(_, e, _) -> e

let MakeApplicableExprNoFlex cenv expr =
    ApplicableExpr (cenv, expr, true)

/// This function reverses the effect of condensation for a named function value (indeed it can
/// work for any expression, though we only invoke it immediately after a call to TcVal).
///
/// De-condensation is determined BEFORE any arguments are checked. Thus
///      let f (x:'a) (y:'a) = ()
///
///      f (new obj()) "string"
///
/// does not type check (the argument instantiates 'a to "obj" but there is no flexibility on the
/// second argument position.
///
/// De-condensation is applied AFTER taking into account an explicit type instantiation. This
///      let f<'a> (x:'a) = ()
///
///      f<obj>("string)"
///
/// will type check but
///
/// Sealed types and 'obj' do not introduce generic flexibility when functions are used as first class
/// values.
///
/// For 'obj' this is because introducing this flexibility would NOT be the reverse of condensation,
/// since we don't condense
///     f: 'a -> unit
/// to
///     f: obj -> unit
///
/// We represent the flexibility in the TAST by leaving a function-to-function coercion node in the tree
/// This "special" node is immediately eliminated by the use of IteratedFlexibleAdjustArityOfLambdaBody as soon as we
/// first transform the tree (currently in optimization)

let MakeApplicableExprWithFlex cenv (env: TcEnv) expr =
    let g = cenv.g
    let exprTy = tyOfExpr g expr
    let m = expr.Range

    let isNonFlexibleType ty = isSealedTy g ty

    let argTys, retTy = stripFunTy g exprTy
    let curriedActualTypes = argTys |> List.map (tryDestRefTupleTy g)
    if (curriedActualTypes.IsEmpty ||
        curriedActualTypes |> List.exists (List.exists (isByrefTy g)) ||
        curriedActualTypes |> List.forall (List.forall isNonFlexibleType)) then

        ApplicableExpr (cenv, expr, true)
    else
        let curriedFlexibleTypes =
            curriedActualTypes |> List.mapSquared (fun actualType ->
                if isNonFlexibleType actualType
                then actualType
                else
                   let flexibleType = NewInferenceType ()
                   AddCxTypeMustSubsumeType ContextInfo.NoContext env.DisplayEnv cenv.css m NoTrace actualType flexibleType
                   flexibleType)

        // Create a coercion to represent the expansion of the application
        let expr = mkCoerceExpr (expr, mkIteratedFunTy (List.map (mkRefTupledTy g) curriedFlexibleTypes) retTy, m, exprTy)
        ApplicableExpr (cenv, expr, true)


///  Checks, warnings and constraint assertions for downcasts
let TcRuntimeTypeTest isCast isOperator cenv denv m tgtTy srcTy =
    let g = cenv.g
    if TypeDefinitelySubsumesTypeNoCoercion 0 g cenv.amap m tgtTy srcTy then
      warning(TypeTestUnnecessary m)

    if isTyparTy g srcTy && not (destTyparTy g srcTy).IsCompatFlex then
        error(IndeterminateRuntimeCoercion(denv, srcTy, tgtTy, m))

    if isSealedTy g srcTy then
        error(RuntimeCoercionSourceSealed(denv, srcTy, m))

    if isSealedTy g tgtTy || isTyparTy g tgtTy || not (isInterfaceTy g srcTy) then
        if isCast then
            AddCxTypeMustSubsumeType (ContextInfo.RuntimeTypeTest isOperator) denv cenv.css m NoTrace srcTy tgtTy
        else
            AddCxTypeMustSubsumeType ContextInfo.NoContext denv cenv.css m NoTrace srcTy tgtTy

    if isErasedType g tgtTy then
        if isCast then
            warning(Error(FSComp.SR.tcTypeCastErased(NicePrint.minimalStringOfType denv tgtTy, NicePrint.minimalStringOfType denv (stripTyEqnsWrtErasure EraseAll g tgtTy)), m))
        else
            error(Error(FSComp.SR.tcTypeTestErased(NicePrint.minimalStringOfType denv tgtTy, NicePrint.minimalStringOfType denv (stripTyEqnsWrtErasure EraseAll g tgtTy)), m))
    else
        getErasedTypes g tgtTy |>
        List.iter (fun ety -> if isMeasureTy g ety
                              then warning(Error(FSComp.SR.tcTypeTestLosesMeasures(NicePrint.minimalStringOfType denv ety), m))
                              else warning(Error(FSComp.SR.tcTypeTestLossy(NicePrint.minimalStringOfType denv ety, NicePrint.minimalStringOfType denv (stripTyEqnsWrtErasure EraseAll g ety)), m)))

///  Checks, warnings and constraint assertions for upcasts
let TcStaticUpcast cenv denv m tgtTy srcTy =
    if isTyparTy cenv.g tgtTy then
        if not (destTyparTy cenv.g tgtTy).IsCompatFlex then
            error(IndeterminateStaticCoercion(denv, srcTy, tgtTy, m))
            //else warning(UpcastUnnecessary m)

    if isSealedTy cenv.g tgtTy && not (isTyparTy cenv.g tgtTy) then
        warning(CoercionTargetSealed(denv, tgtTy, m))

    if typeEquiv cenv.g srcTy tgtTy then
        warning(UpcastUnnecessary m)

    AddCxTypeMustSubsumeType ContextInfo.NoContext denv cenv.css m NoTrace tgtTy srcTy

let BuildPossiblyConditionalMethodCall cenv env isMutable m isProp minfo valUseFlags minst objArgs args =

    let conditionalCallDefineOpt = TryFindMethInfoStringAttribute cenv.g m cenv.g.attrib_ConditionalAttribute minfo

    match conditionalCallDefineOpt, cenv.conditionalDefines with
    | Some d, Some defines when not (List.contains d defines) ->

        // Methods marked with 'Conditional' must return 'unit'
        UnifyTypes cenv env m cenv.g.unit_ty (minfo.GetFSharpReturnTy(cenv.amap, m, minst))
        mkUnit cenv.g m, cenv.g.unit_ty

    | _ ->
#if !NO_EXTENSIONTYPING
        match minfo with
        | ProvidedMeth(_, mi, _, _) ->
            // BuildInvokerExpressionForProvidedMethodCall converts references to F# intrinsics back to values
            // and uses TcVal to do this. However we don't want to check attributes again for provided references to values,
            // so we pass 'false' for 'checkAttributes'.
            let tcVal = LightweightTcValForUsingInBuildMethodCall cenv.g
            let _, retExpt, retTy = ProvidedMethodCalls.BuildInvokerExpressionForProvidedMethodCall tcVal (cenv.g, cenv.amap, mi, objArgs, isMutable, isProp, valUseFlags, args, m)
            retExpt, retTy

        | _ ->
#endif
        let tcVal valref valUse ttypes m =
            let _, a, _, b, _, _ = TcVal true cenv env emptyUnscopedTyparEnv valref (Some (valUse, (fun x _ -> ttypes, x))) None m
            a, b
        BuildMethodCall tcVal cenv.g cenv.amap isMutable m isProp minfo valUseFlags minst objArgs args


let TryFindIntrinsicOrExtensionMethInfo collectionSettings (cenv: cenv) (env: TcEnv) m ad nm ty =
    AllMethInfosOfTypeInScope collectionSettings cenv.infoReader env.NameEnv (Some nm) ad IgnoreOverrides m ty

let TryFindFSharpSignatureInstanceGetterProperty (cenv: cenv) (env: TcEnv) m nm ty (sigTys: TType list) =
    TryFindPropInfo cenv.infoReader m env.AccessRights nm ty
    |> List.tryFind (fun propInfo ->
        not propInfo.IsStatic && propInfo.HasGetter &&
        (
            match propInfo.GetterMethod.GetParamTypes(cenv.amap, m, []) with
            | [] -> false
            | argTysList ->

                let argTys = (argTysList |> List.reduce (@)) @ [ propInfo.GetterMethod.GetFSharpReturnTy(cenv.amap, m, []) ] in
                if argTys.Length <> sigTys.Length then
                    false
                else
                    (argTys, sigTys)
                    ||> List.forall2 (typeEquiv cenv.g)
        )
    )

/// Build the 'test and dispose' part of a 'use' statement
let BuildDisposableCleanup cenv env m (v: Val) =
    v.SetHasBeenReferenced()
    let ad = env.eAccessRights
    let disposeMethod =
        match TryFindIntrinsicOrExtensionMethInfo ResultCollectionSettings.AllResults cenv env m ad "Dispose" cenv.g.system_IDisposable_ty with
        | [x] -> x
        | _ -> error(InternalError(FSComp.SR.tcCouldNotFindIDisposable(), m))


    // For struct types the test is simpler: we can determine if IDisposable is supported, and even when it is, we can avoid doing the type test
    // Note this affects the elaborated form seen by quotations etc.
    if isStructTy cenv.g v.Type then
        if TypeFeasiblySubsumesType 0 cenv.g cenv.amap m cenv.g.system_IDisposable_ty CanCoerce v.Type then
            // We can use NeverMutates here because the variable is going out of scope, there is no need to take a defensive
            // copy of it.
            let disposeExpr, _ = BuildPossiblyConditionalMethodCall cenv env NeverMutates m false disposeMethod NormalValUse [] [exprForVal v.Range v] []
            disposeExpr
        else
            mkUnit cenv.g m
    else
        let disposeObjVar, disposeObjExpr = mkCompGenLocal m "objectToDispose" cenv.g.system_IDisposable_ty
        let disposeExpr, _ = BuildPossiblyConditionalMethodCall cenv env PossiblyMutates m false disposeMethod NormalValUse [] [disposeObjExpr] []
        let inpe = mkCoerceExpr(exprForVal v.Range v, cenv.g.obj_ty, m, v.Type)
        mkIsInstConditional cenv.g m cenv.g.system_IDisposable_ty inpe disposeObjVar disposeExpr (mkUnit cenv.g m)

/// Build call to get_OffsetToStringData as part of 'fixed'
let BuildOffsetToStringData cenv env m =
    let ad = env.eAccessRights
    let offsetToStringDataMethod =
        match TryFindIntrinsicOrExtensionMethInfo ResultCollectionSettings.AllResults cenv env m ad "get_OffsetToStringData" cenv.g.system_RuntimeHelpers_ty with
        | [x] -> x
        | _ -> error(Error(FSComp.SR.tcCouldNotFindOffsetToStringData(), m))

    let offsetExpr, _ = BuildPossiblyConditionalMethodCall cenv env NeverMutates m false offsetToStringDataMethod NormalValUse [] [] []
    offsetExpr

let BuildILFieldGet g amap m objExpr (finfo: ILFieldInfo) =
    let fref = finfo.ILFieldRef
    let isValueType = finfo.IsValueType
    let valu = if isValueType then AsValue else AsObject
    let tinst = finfo.TypeInst
    let fieldType = finfo.FieldType (amap, m)
#if !NO_EXTENSIONTYPING
    let ty = tyOfExpr g objExpr
    match finfo with
    | ProvidedField _ when (isErasedType g ty) ->
        // we know it's accessible, and there are no attributes to check for now...
        match finfo.LiteralValue with
        | None ->
            error (Error(FSComp.SR.tcTPFieldMustBeLiteral(), m))
        | Some lit ->
            Expr.Const (TcFieldInit m lit, m, fieldType)
    | _ ->
#endif
    let wrap, objExpr, _readonly, _writeonly = mkExprAddrOfExpr g isValueType false NeverMutates objExpr None m
      // The empty instantiation on the AbstractIL fspec is OK, since we make the correct fspec in IlxGen.GenAsm
      // This ensures we always get the type instantiation right when doing this from
      // polymorphic code, after inlining etc. *
    let fspec = mkILFieldSpec(fref, mkILNamedTy valu fref.DeclaringTypeRef [])
    // Add an I_nop if this is an initonly field to make sure we never recognize it as an lvalue. See mkExprAddrOfExpr.
    wrap (mkAsmExpr (([ mkNormalLdfld fspec ] @ (if finfo.IsInitOnly then [ AI_nop ] else [])), tinst, [objExpr], [fieldType], m))

/// Checks that setting a field value does not set a literal or initonly field
let private CheckFieldLiteralArg (finfo: ILFieldInfo) argExpr m =
    finfo.LiteralValue |> Option.iter (fun _ ->
        match argExpr with
        | Expr.Const (v, _, _) ->
            let literalValue = string v
            error (Error(FSComp.SR.tcLiteralFieldAssignmentWithArg literalValue, m))
        | _ ->
            error (Error(FSComp.SR.tcLiteralFieldAssignmentNoArg(), m))
    )
    if finfo.IsInitOnly then error (Error (FSComp.SR.tcFieldIsReadonly(), m))

let BuildILFieldSet g m objExpr (finfo: ILFieldInfo) argExpr =
    let fref = finfo.ILFieldRef
    let isValueType = finfo.IsValueType
    let valu = if isValueType then AsValue else AsObject
    let tinst = finfo.TypeInst
      // The empty instantiation on the AbstractIL fspec is OK, since we make the correct fspec in IlxGen.GenAsm
      // This ensures we always get the type instantiation right when doing this from
      // polymorphic code, after inlining etc. *
    let fspec = mkILFieldSpec(fref, mkILNamedTy valu fref.DeclaringTypeRef [])
    CheckFieldLiteralArg finfo argExpr m
    let wrap, objExpr, _readonly, _writeonly = mkExprAddrOfExpr g isValueType false DefinitelyMutates objExpr None m
    wrap (mkAsmExpr ([ mkNormalStfld fspec ], tinst, [objExpr; argExpr], [], m))

let BuildILStaticFieldSet m (finfo: ILFieldInfo) argExpr =
    let fref = finfo.ILFieldRef
    let isValueType = finfo.IsValueType
    let valu = if isValueType then AsValue else AsObject
    let tinst = finfo.TypeInst
      // The empty instantiation on the AbstractIL fspec is OK, since we make the correct fspec in IlxGen.GenAsm
      // This ensures we always get the type instantiation right when doing this from
      // polymorphic code, after inlining etc.
    let fspec = mkILFieldSpec(fref, mkILNamedTy valu fref.DeclaringTypeRef [])
    CheckFieldLiteralArg finfo argExpr m
    mkAsmExpr ([ mkNormalStsfld fspec ], tinst, [argExpr], [], m)

let BuildRecdFieldSet g m objExpr (rfinfo: RecdFieldInfo) argExpr =
    let tgtTy = rfinfo.DeclaringType
    let valu = isStructTy g tgtTy
    let objExpr = if valu then objExpr else mkCoerceExpr(objExpr, tgtTy, m, tyOfExpr g objExpr)
    let wrap, objExpr, _readonly, _writeonly = mkExprAddrOfExpr g valu false DefinitelyMutates objExpr None m
    wrap (mkRecdFieldSetViaExprAddr (objExpr, rfinfo.RecdFieldRef, rfinfo.TypeInst, argExpr, m) )

//-------------------------------------------------------------------------
// Helpers dealing with named and optional args at callsites
//-------------------------------------------------------------------------

let (|BinOpExpr|_|) e =
    match e with
    | SynExpr.App (_, _, SynExpr.App (_, _, SingleIdent opId, a, _), b, _) -> Some (opId, a, b)
    | _ -> None

let (|SimpleEqualsExpr|_|) e =
    match e with
    | BinOpExpr(opId, a, b) when opId.idText = opNameEquals -> Some (a, b)
    | _ -> None

/// Detect a named argument at a callsite
let TryGetNamedArg e =
    match e with
    | SimpleEqualsExpr(LongOrSingleIdent(isOpt, LongIdentWithDots([a], _), None, _), b) -> Some(isOpt, a, b)
    | _ -> None

let inline IsNamedArg e =
    match e with
    | SimpleEqualsExpr(LongOrSingleIdent(_, LongIdentWithDots([_], _), None, _), _) -> true
    | _ -> false

/// Get the method arguments at a callsite, taking into account named and optional arguments
let GetMethodArgs arg =
    let args =
        match arg with
        | SynExpr.Const (SynConst.Unit, _) -> []
        | SynExprParen(SynExpr.Tuple (false, args, _, _), _, _, _) | SynExpr.Tuple (false, args, _, _) -> args
        | SynExprParen(arg, _, _, _) | arg -> [arg]
    let unnamedCallerArgs, namedCallerArgs =
        args |> List.takeUntil IsNamedArg
    let namedCallerArgs =
        namedCallerArgs
        |> List.choose (fun e ->
              match TryGetNamedArg e with
              | None ->
                  // ignore errors to avoid confusing error messages in cases like foo(a = 1, )
                  // do not abort overload resolution in case if named arguments are mixed with errors
                  match e with
                  | SynExpr.ArbitraryAfterError _ -> None
                  | _ -> error(Error(FSComp.SR.tcNameArgumentsMustAppearLast(), e.Range))
              | namedArg -> namedArg)
    unnamedCallerArgs, namedCallerArgs


//-------------------------------------------------------------------------
// Helpers dealing with pattern match compilation
//-------------------------------------------------------------------------

let CompilePatternForMatch cenv (env: TcEnv) mExpr matchm warnOnUnused actionOnFailure (inputVal, generalizedTypars, inputExprOpt) clauses inputTy resultTy =
    let dtree, targets = CompilePattern cenv.g env.DisplayEnv cenv.amap (LightweightTcValForUsingInBuildMethodCall cenv.g) cenv.infoReader mExpr matchm warnOnUnused actionOnFailure (inputVal, generalizedTypars, inputExprOpt) clauses inputTy resultTy
    mkAndSimplifyMatch DebugPointAtBinding.NoneAtInvisible mExpr matchm resultTy dtree targets

/// Compile a pattern
let CompilePatternForMatchClauses cenv env mExpr matchm warnOnUnused actionOnFailure inputExprOpt inputTy resultTy tclauses = 
    // Avoid creating a dummy in the common cases where we are about to bind a name for the expression 
    // CLEANUP: avoid code duplication with code further below, i.e.all callers should call CompilePatternForMatch 
    match tclauses with 
    | [TClause(TPat_as (pat1, PBind (asVal, TypeScheme(generalizedTypars, _)), _), None, TTarget(vs, e, spTarget, _), m2)] ->
        let vs2 = ListSet.remove valEq asVal vs
        let expr = CompilePatternForMatch cenv env mExpr matchm warnOnUnused actionOnFailure (asVal, generalizedTypars, None) [TClause(pat1, None, TTarget(vs2, e, spTarget, None), m2)] inputTy resultTy
        asVal, expr
    | _ ->
        let matchValueTmp, _ = mkCompGenLocal mExpr "matchValue" inputTy
        let expr = CompilePatternForMatch cenv env mExpr matchm warnOnUnused actionOnFailure (matchValueTmp, [], inputExprOpt) tclauses inputTy resultTy
        matchValueTmp, expr

//-------------------------------------------------------------------------
// Helpers dealing with sequence expressions
//-------------------------------------------------------------------------

/// Get the fragmentary expressions resulting from turning
/// an expression into an enumerable value, e.g. at 'for' loops

// localAlloc is relevant if the enumerator is a mutable struct and indicates
// if the enumerator can be allocated as a mutable local variable
let AnalyzeArbitraryExprAsEnumerable cenv (env: TcEnv) localAlloc m exprty expr =
    let ad = env.AccessRights

    let err k ty =
        let txt = NicePrint.minimalStringOfType env.DisplayEnv ty
        let msg = if k then FSComp.SR.tcTypeCannotBeEnumerated txt else FSComp.SR.tcEnumTypeCannotBeEnumerated txt
        Exception(Error(msg, m))

    let findMethInfo k m nm ty =
        match TryFindIntrinsicOrExtensionMethInfo ResultCollectionSettings.AtMostOneResult cenv env m ad nm ty with
        | [] -> err k ty
        | res :: _ -> Result res

    // Ensure there are no curried arguments, and indeed no arguments at all
    let hasArgs (minfo: MethInfo) minst =
        match minfo.GetParamTypes(cenv.amap, m, minst) with
        | [[]] -> false
        | _ -> true

    let tryType (exprToSearchForGetEnumeratorAndItem, tyToSearchForGetEnumeratorAndItem) =
        match findMethInfo true m "GetEnumerator" tyToSearchForGetEnumeratorAndItem with
        | Exception e -> Exception e
        | Result getEnumerator_minfo ->

        let getEnumerator_minst = FreshenMethInfo m getEnumerator_minfo
        let retTypeOfGetEnumerator = getEnumerator_minfo.GetFSharpReturnTy(cenv.amap, m, getEnumerator_minst)
        if hasArgs getEnumerator_minfo getEnumerator_minst then err true tyToSearchForGetEnumeratorAndItem else

        match findMethInfo false m "MoveNext" retTypeOfGetEnumerator with
        | Exception e -> Exception e
        | Result moveNext_minfo ->

        let moveNext_minst = FreshenMethInfo m moveNext_minfo
        let retTypeOfMoveNext = moveNext_minfo.GetFSharpReturnTy(cenv.amap, m, moveNext_minst)
        if not (typeEquiv cenv.g cenv.g.bool_ty retTypeOfMoveNext) then err false retTypeOfGetEnumerator else
        if hasArgs moveNext_minfo moveNext_minst then err false retTypeOfGetEnumerator else

        match findMethInfo false m "get_Current" retTypeOfGetEnumerator with
        | Exception e -> Exception e
        | Result get_Current_minfo ->

        let get_Current_minst = FreshenMethInfo m get_Current_minfo
        if hasArgs get_Current_minfo get_Current_minst then err false retTypeOfGetEnumerator else
        let enumElemTy = get_Current_minfo.GetFSharpReturnTy(cenv.amap, m, get_Current_minst)

        // Compute the element type of the strongly typed enumerator
        //
        // Like C#, we detect the 'GetEnumerator' pattern for .NET version 1.x abstractions that don't
        // support the correct generic interface. However unlike C# we also go looking for a 'get_Item' or 'Item' method
        // with a single integer indexer argument to try to get a strong type for the enumeration should the Enumerator
        // not provide anything useful. To enable interop with some legacy COM APIs,
        // the single integer indexer argument is allowed to have type 'object'.

        let enumElemTy =

            if isObjTy cenv.g enumElemTy then
                // Look for an 'Item' property, or a set of these with consistent return types
                let allEquivReturnTypes (minfo: MethInfo) (others: MethInfo list) =
                    let returnTy = minfo.GetFSharpReturnTy(cenv.amap, m, [])
                    others |> List.forall (fun other -> typeEquiv cenv.g (other.GetFSharpReturnTy(cenv.amap, m, [])) returnTy)

                let isInt32OrObjectIndexer (minfo: MethInfo) =
                    match minfo.GetParamTypes(cenv.amap, m, []) with
                    | [[ty]] ->
                        // e.g. MatchCollection
                        typeEquiv cenv.g cenv.g.int32_ty ty ||
                        // e.g. EnvDTE.Documents.Item
                        typeEquiv cenv.g cenv.g.obj_ty ty
                    | _ -> false

                match TryFindIntrinsicOrExtensionMethInfo ResultCollectionSettings.AllResults cenv env m ad "get_Item" tyToSearchForGetEnumeratorAndItem with
                | (minfo :: others) when (allEquivReturnTypes minfo others &&
                                          List.exists isInt32OrObjectIndexer (minfo :: others)) ->
                    minfo.GetFSharpReturnTy(cenv.amap, m, [])

                | _ ->

                // Some types such as XmlNodeList have only an Item method
                match TryFindIntrinsicOrExtensionMethInfo ResultCollectionSettings.AllResults cenv env m ad "Item" tyToSearchForGetEnumeratorAndItem with
                | (minfo :: others) when (allEquivReturnTypes minfo others &&
                                          List.exists isInt32OrObjectIndexer (minfo :: others)) ->
                    minfo.GetFSharpReturnTy(cenv.amap, m, [])

                | _ -> enumElemTy
            else
                enumElemTy

        let isEnumeratorTypeStruct = isStructTy cenv.g retTypeOfGetEnumerator
        let originalRetTypeOfGetEnumerator = retTypeOfGetEnumerator

        let (enumeratorVar, enumeratorExpr), retTypeOfGetEnumerator =
            if isEnumeratorTypeStruct then
               if localAlloc then
                  mkMutableCompGenLocal m "enumerator" retTypeOfGetEnumerator, retTypeOfGetEnumerator
               else
                  let refCellTyForRetTypeOfGetEnumerator = mkRefCellTy cenv.g retTypeOfGetEnumerator
                  let v, e = mkMutableCompGenLocal m "enumerator" refCellTyForRetTypeOfGetEnumerator
                  (v, mkRefCellGet cenv.g m retTypeOfGetEnumerator e), refCellTyForRetTypeOfGetEnumerator

            else
               mkCompGenLocal m "enumerator" retTypeOfGetEnumerator, retTypeOfGetEnumerator

        let getEnumExpr, getEnumTy =
            let (getEnumExpr, getEnumTy) as res = BuildPossiblyConditionalMethodCall cenv env PossiblyMutates m false getEnumerator_minfo NormalValUse getEnumerator_minst [exprToSearchForGetEnumeratorAndItem] []
            if not isEnumeratorTypeStruct || localAlloc then res
            else
                // wrap enumerators that are represented as mutable structs into ref cells
                let getEnumExpr = mkRefCell cenv.g m originalRetTypeOfGetEnumerator getEnumExpr
                let getEnumTy = mkRefCellTy cenv.g getEnumTy
                getEnumExpr, getEnumTy

        let guardExpr, guardTy = BuildPossiblyConditionalMethodCall cenv env DefinitelyMutates m false moveNext_minfo NormalValUse moveNext_minst [enumeratorExpr] []
        let currentExpr, currentTy = BuildPossiblyConditionalMethodCall cenv env DefinitelyMutates m true get_Current_minfo NormalValUse get_Current_minst [enumeratorExpr] []
        let currentExpr = mkCoerceExpr(currentExpr, enumElemTy, currentExpr.Range, currentTy)
        let currentExpr, enumElemTy =
            // Implicitly dereference byref for expr 'for x in ...'
            if isByrefTy cenv.g enumElemTy then
                let expr = mkDerefAddrExpr m currentExpr currentExpr.Range enumElemTy
                expr, destByrefTy cenv.g enumElemTy
            else
                currentExpr, enumElemTy

        Result(enumeratorVar, enumeratorExpr, retTypeOfGetEnumerator, enumElemTy, getEnumExpr, getEnumTy, guardExpr, guardTy, currentExpr)

    // First try the original known static type
    match (if isArray1DTy cenv.g exprty then Exception (Failure "") else tryType (expr, exprty)) with
    | Result res -> res
    | Exception e ->

    let probe ty =
        if (AddCxTypeMustSubsumeTypeUndoIfFailed env.DisplayEnv cenv.css m ty exprty) then
            match tryType (mkCoerceExpr(expr, ty, expr.Range, exprty), ty) with
            | Result res -> Some res
            | Exception e ->
                PreserveStackTrace e
                raise e
        else None

    // Next try to typecheck the thing as a sequence
    let enumElemTy = NewInferenceType ()
    let exprTyAsSeq = mkSeqTy cenv.g enumElemTy

    match probe exprTyAsSeq with
    | Some res -> res
    | None ->
    let ienumerable = mkAppTy cenv.g.tcref_System_Collections_IEnumerable []
    match probe ienumerable with
    | Some res -> res
    | None ->
    PreserveStackTrace e
    raise e

// Used inside sequence expressions
let ConvertArbitraryExprToEnumerable (cenv: cenv) ty (env: TcEnv) (expr: Expr) =
    let m = expr.Range
    let enumElemTy = NewInferenceType ()
    if AddCxTypeMustSubsumeTypeUndoIfFailed env.DisplayEnv cenv.css m ( mkSeqTy cenv.g enumElemTy) ty then
        expr, enumElemTy
    else
        let enumerableVar, enumerableExpr = mkCompGenLocal m "inputSequence" ty
        let enumeratorVar, _, retTypeOfGetEnumerator, enumElemTy, getEnumExpr, _, guardExpr, guardTy, betterCurrentExpr =
            AnalyzeArbitraryExprAsEnumerable cenv env false m ty enumerableExpr

        let expr =
           mkCompGenLet m enumerableVar expr
               (mkCallSeqOfFunctions cenv.g m retTypeOfGetEnumerator enumElemTy
                   (mkUnitDelayLambda cenv.g m getEnumExpr)
                   (mkLambda m enumeratorVar (guardExpr, guardTy))
                   (mkLambda m enumeratorVar (betterCurrentExpr, enumElemTy)))
        expr, enumElemTy

//-------------------------------------------------------------------------
// Post-transform initialization graphs using the 'lazy' interpretation.
// See ML workshop paper.
//-------------------------------------------------------------------------

type InitializationGraphAnalysisState =
    | Top
    | InnerTop
    | DefinitelyStrict
    | MaybeLazy
    | DefinitelyLazy

type PreInitializationGraphEliminationBinding =
    { FixupPoints: RecursiveUseFixupPoints
      Binding: Binding }

/// Check for safety and determine if we need to insert lazy thunks
let EliminateInitializationGraphs
      g
      mustHaveArity
      denv
      (bindings: 'Bindings list)
      (iterBindings: ((PreInitializationGraphEliminationBinding list -> unit) -> 'Bindings list -> unit))
      (buildLets: Binding list -> 'Result)
      (mapBindings: (PreInitializationGraphEliminationBinding list -> Binding list) -> 'Bindings list -> 'Result list)
      bindsm =

    let recursiveVals =
        let hash = ValHash<Val>.Create()
        let add (pgrbind: PreInitializationGraphEliminationBinding) = let c = pgrbind.Binding.Var in hash.Add(c, c)
        bindings |> iterBindings (List.iter add)
        hash

    // The output of the analysis
    let mutable outOfOrder = false
    let mutable runtimeChecks = false
    let mutable directRecursiveData = false
    let mutable reportedEager = false
    let mutable definiteDependencies = []

    let rec stripChooseAndExpr e =
        match stripExpr e with
        | Expr.TyChoose (_, b, _) -> stripChooseAndExpr b
        | e -> e

    let availIfInOrder = ValHash<_>.Create()
    let check boundv expr =
        let strict = function
            | MaybeLazy -> MaybeLazy
            | DefinitelyLazy -> DefinitelyLazy
            | Top | DefinitelyStrict | InnerTop -> DefinitelyStrict
        let lzy = function
            | Top | InnerTop | DefinitelyLazy -> DefinitelyLazy
            | MaybeLazy | DefinitelyStrict -> MaybeLazy
        let fixable = function
            | Top | InnerTop -> InnerTop
            | DefinitelyStrict -> DefinitelyStrict
            | MaybeLazy -> MaybeLazy
            | DefinitelyLazy -> DefinitelyLazy

        let rec CheckExpr st e =
            match stripChooseAndExpr e with
              // Expressions with some lazy parts
            | Expr.Lambda (_, _, _, _, b, _, _) -> checkDelayed st b

            // Type-lambdas are analyzed as if they are strict.
            //
            // This is a design decision (See bug 6496), so that generalized recursive bindings such as
            //   let rec x = x
            // are analyzed. Although we give type "x: 'T" to these, from the users point of view
            // any use of "x" will result in an infinite recursion. Type instantiation is implicit in F#
            // because of type inference, which makes it reasonable to check generic bindings strictly.
            | Expr.TyLambda (_, _, b, _, _) -> CheckExpr st b

            | Expr.Obj (_, ty, _, e, overrides, extraImpls, _) ->
                // NOTE: we can't fixup recursive references inside delegates since the closure delegee of a delegate is not accessible 
                // from outside. Object expressions implementing interfaces can, on the other hand, be fixed up. See FSharp 1.0 bug 1469 
                if isInterfaceTy g ty then 
                    List.iter (fun (TObjExprMethod(_, _, _, _, e, _)) -> checkDelayed st e) overrides
                    List.iter (snd >> List.iter (fun (TObjExprMethod(_, _, _, _, e, _)) -> checkDelayed st e)) extraImpls
                else
                    CheckExpr (strict st) e
                    List.iter (fun (TObjExprMethod(_, _, _, _, e, _)) -> CheckExpr (lzy (strict st)) e) overrides
                    List.iter (snd >> List.iter (fun (TObjExprMethod(_, _, _, _, e, _)) -> CheckExpr (lzy (strict st)) e)) extraImpls

              // Expressions where fixups may be needed
            | Expr.Val (v, _, m) -> CheckValRef st v m

             // Expressions where subparts may be fixable
            | Expr.Op ((TOp.Tuple _ | TOp.UnionCase _ | TOp.Recd _), _, args, _) ->
                List.iter (CheckExpr (fixable st)) args

              // Composite expressions
            | Expr.Const _ -> ()
            | Expr.LetRec (binds, e, _, _) ->
                binds |> List.iter (CheckBinding (strict st))
                CheckExpr (strict st) e
            | Expr.Let (bind, e, _, _) ->
                CheckBinding (strict st) bind
                CheckExpr (strict st) e
            | Expr.Match (_, _, pt, targets, _, _) ->
                CheckDecisionTree (strict st) pt
                Array.iter (CheckDecisionTreeTarget (strict st)) targets
            | Expr.App (e1, _, _, args, _) ->
                CheckExpr (strict st) e1
                List.iter (CheckExpr (strict st)) args
          // Binary expressions
            | Expr.Sequential (e1, e2, _, _, _)
            | Expr.StaticOptimization (_, e1, e2, _) ->
                 CheckExpr (strict st) e1; CheckExpr (strict st) e2
          // n-ary expressions
            | Expr.Op (op, _, args, m) -> CheckExprOp st op m; List.iter (CheckExpr (strict st)) args
          // misc
            | Expr.Link eref -> CheckExpr st !eref
            | Expr.TyChoose (_, b, _) -> CheckExpr st b
            | Expr.Quote _ -> ()
            | Expr.WitnessArg (_witnessInfo, _m) -> ()

        and CheckBinding st (TBind(_, e, _)) = CheckExpr st e 

        and CheckDecisionTree st = function
            | TDSwitch(e1, csl, dflt, _) -> CheckExpr st e1; List.iter (fun (TCase(_, d)) -> CheckDecisionTree st d) csl; Option.iter (CheckDecisionTree st) dflt
            | TDSuccess (es, _) -> es |> List.iter (CheckExpr st)
            | TDBind(bind, e) -> CheckBinding st bind; CheckDecisionTree st e

        and CheckDecisionTreeTarget st (TTarget(_, e, _, _)) = CheckExpr st e

        and CheckExprOp st op m =
            match op with
            | TOp.LValueOp (_, lvr) -> CheckValRef (strict st) lvr m
            | _ -> ()

        and CheckValRef st (v: ValRef) m =
            match st with
            | MaybeLazy ->
                if recursiveVals.TryFind v.Deref |> Option.isSome then
                    warning (RecursiveUseCheckedAtRuntime (denv, v, m))
                    if not reportedEager then
                      (warning (LetRecCheckedAtRuntime m); reportedEager <- true)
                    runtimeChecks <- true

            | Top | DefinitelyStrict ->
                if recursiveVals.TryFind v.Deref |> Option.isSome then
                    if availIfInOrder.TryFind v.Deref |> Option.isNone then
                        warning (LetRecEvaluatedOutOfOrder (denv, boundv, v, m))
                        outOfOrder <- true
                        if not reportedEager then
                          (warning (LetRecCheckedAtRuntime m); reportedEager <- true)
                    definiteDependencies <- (boundv, v) :: definiteDependencies
            | InnerTop ->
                if recursiveVals.TryFind v.Deref |> Option.isSome then
                    directRecursiveData <- true
            | DefinitelyLazy -> ()
        and checkDelayed st b =
            match st with
            | MaybeLazy | DefinitelyStrict -> CheckExpr MaybeLazy b
            | DefinitelyLazy | Top | InnerTop -> ()


        CheckExpr Top expr


    // Check the bindings one by one, each w.r.t. the previously available set of binding
    begin
        let checkBind (pgrbind: PreInitializationGraphEliminationBinding) =
            let (TBind(v, e, _)) = pgrbind.Binding
            check (mkLocalValRef v) e
            availIfInOrder.Add(v, 1)
        bindings |> iterBindings (List.iter checkBind)
    end

    // ddg = definiteDependencyGraph
    let ddgNodes = recursiveVals.Values |> Seq.toList |> List.map mkLocalValRef
    let ddg = Graph<ValRef, Stamp>((fun v -> v.Stamp), ddgNodes, definiteDependencies )
    ddg.IterateCycles (fun path -> error (LetRecUnsound (denv, path, path.Head.Range)))

    let requiresLazyBindings = runtimeChecks || outOfOrder
    if directRecursiveData && requiresLazyBindings then
        error(Error(FSComp.SR.tcInvalidMixtureOfRecursiveForms(), bindsm))

    if requiresLazyBindings then
        let morphBinding (pgrbind: PreInitializationGraphEliminationBinding) =
            let (RecursiveUseFixupPoints fixupPoints) = pgrbind.FixupPoints
            let (TBind(v, e, seqPtOpt)) = pgrbind.Binding
            match stripChooseAndExpr e with
            | Expr.Lambda _ | Expr.TyLambda _ ->
                [], [mkInvisibleBind v e]
            | _ ->
                let ty = v.Type
                let m = v.Range
                let vty = (mkLazyTy g ty)

                let fty = (g.unit_ty --> ty)
                let flazy, felazy = mkCompGenLocal m v.LogicalName fty
                let frhs = mkUnitDelayLambda g m e
                if mustHaveArity then flazy.SetValReprInfo (Some(InferArityOfExpr g AllowTypeDirectedDetupling.Yes fty [] [] frhs))

                let vlazy, velazy = mkCompGenLocal m v.LogicalName vty
                let vrhs = (mkLazyDelayed g m ty felazy)

                if mustHaveArity then vlazy.SetValReprInfo (Some(InferArityOfExpr g AllowTypeDirectedDetupling.Yes vty [] [] vrhs))
                fixupPoints |> List.iter (fun (fp, _) -> fp := mkLazyForce g (!fp).Range ty velazy)

                [mkInvisibleBind flazy frhs; mkInvisibleBind vlazy vrhs],
                [mkBind seqPtOpt v (mkLazyForce g m ty velazy)]

        let newTopBinds = ResizeArray<_>()
        let morphBindings pgrbinds = pgrbinds |> List.map morphBinding |> List.unzip |> (fun (a, b) -> newTopBinds.Add (List.concat a); List.concat b)

        let res = bindings |> mapBindings morphBindings
        if newTopBinds.Count = 0 then res
        else buildLets (List.concat newTopBinds) :: res
    else
        let noMorph (pgrbinds: PreInitializationGraphEliminationBinding list) = pgrbinds |> List.map (fun pgrbind -> pgrbind.Binding)
        bindings |> mapBindings noMorph

//-------------------------------------------------------------------------
// Check the shape of an object constructor and rewrite calls
//-------------------------------------------------------------------------

let CheckAndRewriteObjectCtor g env (ctorLambdaExpr: Expr) =

    let m = ctorLambdaExpr.Range
    let tps, vsl, body, returnTy = stripTopLambda (ctorLambdaExpr, tyOfExpr g ctorLambdaExpr)

    // Rewrite legitimate self-construction calls to CtorValUsedAsSelfInit
    let error (expr: Expr) =
        errorR(Error(FSComp.SR.tcInvalidObjectConstructionExpression(), expr.Range))
        expr

    // Build an assignment into the safeThisValOpt mutable reference cell that holds recursive references to 'this'
    // Build an assignment into the safeInitInfo mutable field that indicates that partial initialization is successful
    let rewriteConstruction recdExpr =
       match env.eCtorInfo with
       | None -> recdExpr
       | Some ctorInfo ->
           let recdExpr =
               match ctorInfo.safeThisValOpt with
               | None -> recdExpr
               | Some safeInitVal ->
                   let ty = tyOfExpr g recdExpr
                   let thisExpr = mkGetArg0 m ty
                   let setExpr = mkRefCellSet g m ty (exprForValRef m (mkLocalValRef safeInitVal)) thisExpr
                   Expr.Sequential (recdExpr, setExpr, ThenDoSeq, DebugPointAtSequential.SuppressStmt, m)
           let recdExpr =
               match ctorInfo.safeInitInfo with
               | NoSafeInitInfo -> recdExpr
               | SafeInitField (rfref, _) ->
                   let thisTy = tyOfExpr g recdExpr
                   let thisExpr = mkGetArg0 m thisTy
                   let thisTyInst = argsOfAppTy g thisTy
                   let setExpr = mkRecdFieldSetViaExprAddr (thisExpr, rfref, thisTyInst, mkOne g m, m)
                   Expr.Sequential (recdExpr, setExpr, ThenDoSeq, DebugPointAtSequential.SuppressStmt, m)
           recdExpr


    let rec checkAndRewrite (expr: Expr) =
        match expr with
        // <ctor-body> = { fields }
        // The constructor ends in an object initialization expression - good
        | Expr.Op (TOp.Recd (RecdExprIsObjInit, _), _, _, _) -> rewriteConstruction expr

        // <ctor-body> = "a; <ctor-body>"
        | Expr.Sequential (a, body, NormalSeq, spSeq, b) -> Expr.Sequential (a, checkAndRewrite body, NormalSeq, spSeq, b)

        // <ctor-body> = "<ctor-body> then <expr>"
        | Expr.Sequential (body, a, ThenDoSeq, spSeq, b) -> Expr.Sequential (checkAndRewrite body, a, ThenDoSeq, spSeq, b)

        // <ctor-body> = "let pat = expr in <ctor-body>"
        | Expr.Let (bind, body, m, _) -> mkLetBind m bind (checkAndRewrite body)

        // The constructor is a sequence "let pat = expr in <ctor-body>" 
        | Expr.Match (spBind, a, b, targets, c, d) ->
            let targets = targets |> Array.map (fun (TTarget(vs, body, spTarget, flags)) -> TTarget(vs, checkAndRewrite body, spTarget, flags))
            Expr.Match (spBind, a, b, targets, c, d)

        // <ctor-body> = "let rec binds in <ctor-body>"
        | Expr.LetRec (a, body, _, _) -> Expr.LetRec (a, checkAndRewrite body, m, Construct.NewFreeVarsCache())

        // <ctor-body> = "new C(...)"
        | Expr.App (f, b, c, d, m) ->
            // The application had better be an application of a ctor
            let f = checkAndRewriteCtorUsage f
            let expr = Expr.App (f, b, c, d, m)
            rewriteConstruction expr

        | _ ->
            error expr

    and checkAndRewriteCtorUsage expr =
         match expr with
         | Expr.Link eref ->
               let e = checkAndRewriteCtorUsage !eref
               eref := e
               expr

         // Type applications are ok, e.g.
         //     type C<'a>(x: int) =
         //         new() = C<'a>(3)
         | Expr.App (f, fty, tyargs, [], m) ->
             let f = checkAndRewriteCtorUsage f
             Expr.App (f, fty, tyargs, [], m)

         // Self-calls are OK and get rewritten.
         | Expr.Val (vref, NormalValUse, a) ->
           let isCtor =
               match vref.MemberInfo with
               | None -> false
               | Some memberInfo -> memberInfo.MemberFlags.MemberKind = SynMemberKind.Constructor

           if not isCtor then
               error expr
           else
               Expr.Val (vref, CtorValUsedAsSelfInit, a)
         | _ ->
            error expr

    let body = checkAndRewrite body
    mkMultiLambdas m tps vsl (body, returnTy)



/// Post-typechecking normalizations to enforce semantic constraints
/// lazy and, lazy or, rethrow, address-of
let buildApp cenv expr resultTy arg m =
    let g = cenv.g
    match expr, arg with

    // Special rule for building applications of the 'x && y' operator
    | ApplicableExpr(_, Expr.App (Expr.Val (vf, _, _), _, _, [x0], _), _), _
         when valRefEq g vf g.and_vref
           || valRefEq g vf g.and2_vref ->
        MakeApplicableExprNoFlex cenv (mkLazyAnd g m x0 arg), resultTy

    // Special rule for building applications of the 'x || y' operator
    | ApplicableExpr(_, Expr.App (Expr.Val (vf, _, _), _, _, [x0], _), _), _
         when valRefEq g vf g.or_vref
           || valRefEq g vf g.or2_vref ->
        MakeApplicableExprNoFlex cenv (mkLazyOr g m x0 arg ), resultTy

    // Special rule for building applications of the 'reraise' operator
    | ApplicableExpr(_, Expr.App (Expr.Val (vf, _, _), _, _, [], _), _), _
         when valRefEq g vf g.reraise_vref ->

        // exprty is of type: "unit -> 'a". Break it and store the 'a type here, used later as return type.
        MakeApplicableExprNoFlex cenv (mkCompGenSequential m arg (mkReraise m resultTy)), resultTy

    // Special rules for NativePtr.ofByRef to generalize result.
    // See RFC FS-1053.md
    | ApplicableExpr(_, Expr.App (Expr.Val (vf, _, _), _, _, [], _), _), _
         when (valRefEq g vf g.nativeptr_tobyref_vref) ->

        let argty = NewInferenceType()
        let resultTy = mkByrefTyWithInference g argty (NewByRefKindInferenceType g m)
        expr.SupplyArgument (arg, m), resultTy

    // Special rules for building applications of the '&expr' operator, which gets the
    // address of an expression.
    //
    // See also RFC FS-1053.md
    | ApplicableExpr(_, Expr.App (Expr.Val (vf, _, _), _, _, [], _), _), _
         when valRefEq g vf g.addrof_vref ->

        let wrap, e1a', readonly, _writeonly = mkExprAddrOfExpr g true false AddressOfOp arg (Some vf) m
        // Assert the result type to be readonly if we couldn't take the address
        let resultTy =
            let argTy = tyOfExpr g arg
            if readonly then
                mkInByrefTy g argTy

            // "`outref<'T>` types are never introduced implicitly by F#.", see rationale in RFC FS-1053
            //
            // We do _not_ introduce outref here, e.g. '&x' where 'x' is outref<_> is _not_ outref.
            // This effectively makes 'outref<_>' documentation-only. There is frequently a need to pass outref
            // pointers to .NET library functions whose signatures are not tagged with [<Out>]
            //elif writeonly then
            //    mkOutByrefTy g argTy

            else
                mkByrefTyWithInference g argTy (NewByRefKindInferenceType g m)

        MakeApplicableExprNoFlex cenv (wrap(e1a')), resultTy

    // Special rules for building applications of the &&expr' operators, which gets the
    // address of an expression.
    | ApplicableExpr(_, Expr.App (Expr.Val (vf, _, _), _, _, [], _), _), _
         when valRefEq g vf g.addrof2_vref ->

        warning(UseOfAddressOfOperator m)
        let wrap, e1a', _readonly, _writeonly = mkExprAddrOfExpr g true false AddressOfOp arg (Some vf) m
        MakeApplicableExprNoFlex cenv (wrap(e1a')), resultTy

    | _ when isByrefTy g resultTy ->
        // Handle byref returns, byref-typed returns get implicitly dereferenced
        let expr = expr.SupplyArgument (arg, m)
        let expr = mkDerefAddrExpr m expr.Expr m resultTy
        let resultTy = destByrefTy g resultTy
        MakeApplicableExprNoFlex cenv expr, resultTy

    | _ ->
        expr.SupplyArgument (arg, m), resultTy

//-------------------------------------------------------------------------
// Additional data structures used by type checking
//-------------------------------------------------------------------------

type DelayedItem =
  /// DelayedTypeApp (typeArgs, mTypeArgs, mExprAndTypeArgs)
  ///
  /// Represents the <tyargs> in "item<tyargs>"
  | DelayedTypeApp of SynType list * range * range

  /// DelayedApp (isAtomic, argExpr, mFuncAndArg)
  ///
  /// Represents the args in "item args", or "item.[args]".
  | DelayedApp of ExprAtomicFlag * SynExpr * range

  /// Represents the long identifiers in "item.Ident1", or "item.Ident1.Ident2" etc.
  | DelayedDotLookup of Ident list * range

  /// Represents an incomplete "item."
  | DelayedDot

  /// Represents the valueExpr in "item <- valueExpr", also "item.[indexerArgs] <- valueExpr" etc.
  | DelayedSet of SynExpr * range

let MakeDelayedSet(e: SynExpr, m) =
    // We have longId <- e. Wrap 'e' in another pair of parentheses to ensure it's never interpreted as
    // a named argument, e.g. for "el.Checked <- (el = el2)"
    DelayedSet (SynExpr.Paren (e, range0, None, e.Range), m)

/// Indicates if member declarations are allowed to be abstract members.
type NewSlotsOK =
    | NewSlotsOK
    | NoNewSlots

/// Indicates whether a syntactic type is allowed to include new type variables
/// not declared anywhere, e.g. `let f (x: 'T option) = x.Value`
type ImplicitlyBoundTyparsAllowed =
    | NewTyparsOKButWarnIfNotRigid
    | NewTyparsOK
    | NoNewTypars

/// Indicates whether constraints should be checked when checking syntactic types
type CheckConstraints =
    | CheckCxs
    | NoCheckCxs

/// Represents information about the module or type in which a member or value is declared.
type MemberOrValContainerInfo =
    | MemberOrValContainerInfo of
        tcref: TyconRef *
        optIntfSlotTy: (TType * SlotImplSet) option *
        baseValOpt: Val option *
        safeInitInfo: SafeInitData *
        declaredTyconTypars: Typars

/// Provides information about the context for a value or member definition
type ContainerInfo =
    | ContainerInfo of
          // The nearest containing module. Used as the 'actual' parent for extension members and values
          ParentRef *
          // For members:
          MemberOrValContainerInfo option
    member x.ParentRef =
        let (ContainerInfo(v, _)) = x
        v

/// Indicates a declaration is contained in an expression
let ExprContainerInfo = ContainerInfo(ParentNone, None)

type NormalizedRecBindingDefn =
    | NormalizedRecBindingDefn of
        containerInfo: ContainerInfo *
        newslotsOk: NewSlotsOK *
        declKind: DeclKind *
        binding: NormalizedBinding

type ValSpecResult =
    | ValSpecResult of
        altActualParent: ParentRef *
        memberInfoOpt: PreValMemberInfo option *
        id: Ident *
        enclosingDeclaredTypars: Typars *
        declaredTypars: Typars *
        ty: TType *
        partialValReprInfo: PartialValReprInfo *
        declKind: DeclKind

//-------------------------------------------------------------------------
// Additional data structures used by checking recursive bindings
//-------------------------------------------------------------------------

type RecDefnBindingInfo =
    | RecDefnBindingInfo of
        containerInfo: ContainerInfo *
        newslotsOk: NewSlotsOK *
        declKind: DeclKind *
        synBinding: SynBinding

/// RecursiveBindingInfo - flows through initial steps of TcLetrec
type RecursiveBindingInfo =
    | RecursiveBindingInfo of
          recBindIndex: int * // index of the binding in the recursive group
          containerInfo: ContainerInfo *
          enclosingDeclaredTypars: Typars *
          inlineFlag: ValInline *
          vspec: Val *
          explicitTyparInfo: ExplicitTyparInfo *
          partialValReprInfo: PartialValReprInfo *
          memberInfoOpt: PreValMemberInfo option *
          baseValOpt: Val option *
          safeThisValOpt: Val option *
          safeInitInfo: SafeInitData *
          visibility: SynAccess option *
          ty: TType *
          declKind: DeclKind

    member x.EnclosingDeclaredTypars = let (RecursiveBindingInfo(_, _, enclosingDeclaredTypars, _, _, _, _, _, _, _, _, _, _, _)) = x in enclosingDeclaredTypars
    member x.Val = let (RecursiveBindingInfo(_, _, _, _, vspec, _, _, _, _, _, _, _, _, _)) = x in vspec
    member x.ExplicitTyparInfo = let (RecursiveBindingInfo(_, _, _, _, _, explicitTyparInfo, _, _, _, _, _, _, _, _)) = x in explicitTyparInfo
    member x.DeclaredTypars = let (ExplicitTyparInfo(_, declaredTypars, _)) = x.ExplicitTyparInfo in declaredTypars
    member x.Index = let (RecursiveBindingInfo(i, _, _, _, _, _, _, _, _, _, _, _, _, _)) = x in i
    member x.ContainerInfo = let (RecursiveBindingInfo(_, c, _, _, _, _, _, _, _, _, _, _, _, _)) = x in c
    member x.DeclKind = let (RecursiveBindingInfo(_, _, _, _, _, _, _, _, _, _, _, _, _, declKind)) = x in declKind

type PreCheckingRecursiveBinding =
    { SyntacticBinding: NormalizedBinding
      RecBindingInfo: RecursiveBindingInfo }

type PreGeneralizationRecursiveBinding =
    { ExtraGeneralizableTypars: Typars
      CheckedBinding: CheckedBindingInfo
      RecBindingInfo: RecursiveBindingInfo }

type PostGeneralizationRecursiveBinding =
    { ValScheme: ValScheme
      CheckedBinding: CheckedBindingInfo
      RecBindingInfo: RecursiveBindingInfo }
    member x.GeneralizedTypars = x.ValScheme.GeneralizedTypars

type PostSpecialValsRecursiveBinding =
    { ValScheme: ValScheme
      Binding: Binding }

let CanInferExtraGeneralizedTyparsForRecBinding (pgrbind: PreGeneralizationRecursiveBinding) =
    let explicitTyparInfo = pgrbind.RecBindingInfo.ExplicitTyparInfo
    let (ExplicitTyparInfo(_, _, canInferTypars)) = explicitTyparInfo
    let memFlagsOpt = pgrbind.RecBindingInfo.Val.MemberInfo |> Option.map (fun memInfo -> memInfo.MemberFlags)
    let canInferTypars = GeneralizationHelpers.ComputeCanInferExtraGeneralizableTypars (pgrbind.RecBindingInfo.ContainerInfo.ParentRef, canInferTypars, memFlagsOpt)
    canInferTypars

/// Get the "this" variable from an instance member binding
let GetInstanceMemberThisVariable (vspec: Val, expr) =
    // Skip over LAM tps. Choose 'a.
    if vspec.IsInstanceMember then
        let rec firstArg e =
          match e with
            | Expr.TyLambda (_, _, b, _, _) -> firstArg b
            | Expr.TyChoose (_, b, _) -> firstArg b
            | Expr.Lambda (_, _, _, [v], _, _, _) -> Some v
            | _ -> failwith "GetInstanceMemberThisVariable: instance member did not have expected internal form"

        firstArg expr
    else
        None

//-------------------------------------------------------------------------
// Checking types and type constraints
//-------------------------------------------------------------------------
/// Check specifications of constraints on type parameters
let rec TcTyparConstraint ridx cenv newOk checkCxs occ (env: TcEnv) tpenv c =
    let checkSimpleConstraint tp m constraintAdder =
        let tp', tpenv = TcTypar cenv env newOk tpenv tp
        constraintAdder env.DisplayEnv cenv.css m NoTrace (mkTyparTy tp')
        tpenv

    match c with
    | SynTypeConstraint.WhereTyparDefaultsToType(tp, ty, m) ->
        let ty', tpenv = TcTypeAndRecover cenv newOk checkCxs occ env tpenv ty
        let tp', tpenv = TcTypar cenv env newOk tpenv tp
        AddCxTyparDefaultsTo env.DisplayEnv cenv.css m env.eContextInfo tp' ridx ty'
        tpenv

    | SynTypeConstraint.WhereTyparSubtypeOfType(tp, ty, m) ->
        let ty', tpenv = TcTypeAndRecover cenv newOk checkCxs ItemOccurence.UseInType env tpenv ty
        let tp', tpenv = TcTypar cenv env newOk tpenv tp
        if newOk = NoNewTypars && isSealedTy cenv.g ty' then
            errorR(Error(FSComp.SR.tcInvalidConstraintTypeSealed(), m))
        AddCxTypeMustSubsumeType ContextInfo.NoContext env.DisplayEnv cenv.css m NoTrace ty' (mkTyparTy tp')
        tpenv

    | SynTypeConstraint.WhereTyparSupportsNull(tp, m) -> checkSimpleConstraint tp m AddCxTypeMustSupportNull

    | SynTypeConstraint.WhereTyparIsComparable(tp, m) -> checkSimpleConstraint tp m AddCxTypeMustSupportComparison

    | SynTypeConstraint.WhereTyparIsEquatable(tp, m) -> checkSimpleConstraint tp m AddCxTypeMustSupportEquality

    | SynTypeConstraint.WhereTyparIsReferenceType(tp, m) -> checkSimpleConstraint tp m AddCxTypeIsReferenceType

    | SynTypeConstraint.WhereTyparIsValueType(tp, m) -> checkSimpleConstraint tp m AddCxTypeIsValueType

    | SynTypeConstraint.WhereTyparIsUnmanaged(tp, m) -> checkSimpleConstraint tp m AddCxTypeIsUnmanaged

    | SynTypeConstraint.WhereTyparIsEnum(tp, tyargs, m) ->
        let tp', tpenv = TcTypar cenv env newOk tpenv tp
        let tpenv =
            match tyargs with
            | [underlying] ->
                let underlying', tpenv = TcTypeAndRecover cenv newOk checkCxs ItemOccurence.UseInType env tpenv underlying
                AddCxTypeIsEnum env.DisplayEnv cenv.css m NoTrace (mkTyparTy tp') underlying'
                tpenv
            | _ ->
                errorR(Error(FSComp.SR.tcInvalidEnumConstraint(), m))
                tpenv
        tpenv

    | SynTypeConstraint.WhereTyparIsDelegate(tp, tyargs, m) ->
        let tp', tpenv = TcTypar cenv env newOk tpenv tp
        match tyargs with
        | [a;b] ->
            let a', tpenv = TcTypeAndRecover cenv newOk checkCxs occ env tpenv a
            let b', tpenv = TcTypeAndRecover cenv newOk checkCxs occ env tpenv b
            AddCxTypeIsDelegate env.DisplayEnv cenv.css m NoTrace (mkTyparTy tp') a' b'
            tpenv
        | _ ->
            errorR(Error(FSComp.SR.tcInvalidEnumConstraint(), m))
            tpenv

    | SynTypeConstraint.WhereTyparSupportsMember(tps, memSpfn, m) ->
        let traitInfo, tpenv = TcPseudoMemberSpec cenv newOk env tps tpenv memSpfn m
        match traitInfo with
        | TTrait(objtys, ".ctor", memberFlags, argTys, returnTy, _) when memberFlags.MemberKind = SynMemberKind.Constructor ->
            match objtys, argTys with
            | [ty], [] when typeEquiv cenv.g ty (GetFSharpViewOfReturnType cenv.g returnTy) ->
                AddCxTypeMustSupportDefaultCtor env.DisplayEnv cenv.css m NoTrace ty
                tpenv
            | _ ->
                errorR(Error(FSComp.SR.tcInvalidNewConstraint(), m))
                tpenv
        | _ ->
            AddCxMethodConstraint env.DisplayEnv cenv.css m NoTrace traitInfo
            tpenv

and TcPseudoMemberSpec cenv newOk env synTypes tpenv memSpfn m =
#if ALLOW_MEMBER_CONSTRAINTS_ON_MEASURES
    let tps, tpenv = List.mapFold (TcTyparOrMeasurePar None cenv env newOk) tpenv synTypars
#else
    let tys, tpenv = List.mapFold (TcTypeAndRecover cenv newOk CheckCxs ItemOccurence.UseInType env) tpenv synTypes
#endif
    match memSpfn with
    | SynMemberSig.Member (valSpfn, memberFlags, m) ->
        // REVIEW: Test pseudo constraints cannot refer to polymorphic methods.
        // REVIEW: Test pseudo constraints cannot be curried.
        let members, tpenv = TcValSpec cenv env ModuleOrMemberBinding newOk ExprContainerInfo (Some memberFlags) (Some (List.head tys)) tpenv valSpfn []
        match members with
        | [ValSpecResult(_, _, id, _, _, memberConstraintTy, partialValReprInfo, _)] ->
            let memberConstraintTypars, _ = tryDestForallTy cenv.g memberConstraintTy
            let topValInfo = TranslatePartialArity memberConstraintTypars partialValReprInfo
            let _, _, curriedArgInfos, returnTy, _ = GetTopValTypeInCompiledForm cenv.g topValInfo 0 memberConstraintTy m
            //if curriedArgInfos.Length > 1 then  error(Error(FSComp.SR.tcInvalidConstraint(), m))
            let argTys = List.concat curriedArgInfos
            let argTys = List.map fst argTys
            let logicalCompiledName = ComputeLogicalName id memberFlags

            let item = Item.ArgName (id, memberConstraintTy, None)
            CallNameResolutionSink cenv.tcSink (id.idRange, env.NameEnv, item, emptyTyparInst, ItemOccurence.Use, env.AccessRights)

            TTrait(tys, logicalCompiledName, memberFlags, argTys, returnTy, ref None), tpenv
        | _ -> error(Error(FSComp.SR.tcInvalidConstraint(), m))
    | _ -> error(Error(FSComp.SR.tcInvalidConstraint(), m))


/// Check a value specification, e.g. in a signature, interface declaration or a constraint
and TcValSpec cenv env declKind newOk containerInfo memFlagsOpt thisTyOpt tpenv valSpfn attrs =
    let (SynValSig(_, id, ValTyparDecls (synTypars, synTyparConstraints, _), ty, valSynInfo, _, _, _, _, _, m)) = valSpfn
    let declaredTypars = TcTyparDecls cenv env synTypars
    let (ContainerInfo(altActualParent, tcrefContainerInfo)) = containerInfo
    let enclosingDeclaredTypars, memberContainerInfo, thisTyOpt, declKind =
        match tcrefContainerInfo with
        | Some(MemberOrValContainerInfo(tcref, _, _, _, declaredTyconTypars)) ->
            let isExtrinsic = (declKind = ExtrinsicExtensionBinding)
            let _, enclosingDeclaredTypars, _, _, thisTy = FreshenObjectArgType cenv m TyparRigidity.Rigid tcref isExtrinsic declaredTyconTypars
            // An implemented interface type is in terms of the type's type parameters.
            // We need a signature in terms of the values' type parameters.
            // let optIntfSlotTy = Option.map (instType renaming) optIntfSlotTy in
            enclosingDeclaredTypars, Some tcref, Some thisTy, declKind
        | None ->
            [], None, thisTyOpt, ModuleOrMemberBinding
    let allDeclaredTypars = enclosingDeclaredTypars @ declaredTypars
    let envinner = AddDeclaredTypars NoCheckForDuplicateTypars allDeclaredTypars env
    let checkCxs = CheckCxs
    let tpenv = TcTyparConstraints cenv newOk checkCxs ItemOccurence.UseInType envinner tpenv synTyparConstraints

    // Treat constraints at the "end" of the type as if they are declared.
    // This is by far the most convenient place to locate the constraints.
    // e.g.
    //    val FastGenericComparer<'T>: IComparer<'T> when 'T: comparison
    let tpenv =
        match ty with
        | SynType.WithGlobalConstraints(_, wcs, _) ->
            TcTyparConstraints cenv newOk checkCxs ItemOccurence.UseInType envinner tpenv wcs
        | _ ->
            tpenv

    // Enforce "no undeclared constraints allowed on declared typars"
    allDeclaredTypars |> List.iter (SetTyparRigid env.DisplayEnv m)
    // Process the type, including any constraints
    let declaredTy, tpenv = TcTypeAndRecover cenv newOk checkCxs ItemOccurence.UseInType envinner tpenv ty

    match memFlagsOpt, thisTyOpt with
    | Some memberFlags, Some thisTy ->
        let generateOneMember (memberFlags: SynMemberFlags) =

            // Decode members in the signature
            let ty', valSynInfo =
                match memberFlags.MemberKind with
                | SynMemberKind.ClassConstructor
                | SynMemberKind.Constructor
                | SynMemberKind.Member ->
                    declaredTy, valSynInfo
                | SynMemberKind.PropertyGet
                | SynMemberKind.PropertySet ->
                    let fakeArgReprInfos = [ for n in SynInfo.AritiesOfArgs valSynInfo do yield [ for _ in 1 .. n do yield ValReprInfo.unnamedTopArg1 ] ]
                    let arginfos, returnTy = GetTopTauTypeInFSharpForm cenv.g fakeArgReprInfos declaredTy m
                    if arginfos.Length > 1 then error(Error(FSComp.SR.tcInvalidPropertyType(), m))
                    match memberFlags.MemberKind with
                    | SynMemberKind.PropertyGet ->
                        if SynInfo.HasNoArgs valSynInfo then
                          (cenv.g.unit_ty --> declaredTy), (SynInfo.IncorporateEmptyTupledArgForPropertyGetter valSynInfo)
                        else
                          declaredTy, valSynInfo
                    | _ ->
                        let setterTy = (mkRefTupledTy cenv.g (List.map fst (List.concat arginfos) @ [returnTy]) --> cenv.g.unit_ty)
                        let synInfo = SynInfo.IncorporateSetterArg valSynInfo
                        setterTy, synInfo
                | SynMemberKind.PropertyGetSet ->
                    error(InternalError("Unexpected SynMemberKind.PropertyGetSet from signature parsing", m))

            // Take "unit" into account in the signature
            let valSynInfo = AdjustValSynInfoInSignature cenv.g ty' valSynInfo

            let ty', valSynInfo =
                if memberFlags.IsInstance then
                  (thisTy --> ty'), (SynInfo.IncorporateSelfArg valSynInfo)
                else
                  ty', valSynInfo

            let reallyGenerateOneMember(id: Ident, valSynInfo, ty', memberFlags) =
                let (PartialValReprInfo(argsData, _)) as partialValReprInfo =
                    TranslateTopValSynInfo id.idRange (TcAttributes cenv env) valSynInfo


                // Fold in the optional argument information
                // Resort to using the syntactic argument information since that is what tells us
                // what is optional and what is not.
                let ty' =

                    if SynInfo.HasOptionalArgs valSynInfo then
                        let curriedArgTys, returnTy = GetTopTauTypeInFSharpForm cenv.g argsData ty' m
                        let curriedArgTys =
                            ((List.mapSquared fst curriedArgTys), valSynInfo.CurriedArgInfos)
                            ||> List.map2 (fun argTys argInfos ->
                                 (argTys, argInfos)
                                 ||> List.map2 (fun argty argInfo ->
                                     if SynInfo.IsOptionalArg argInfo then mkOptionTy cenv.g argty
                                     else argty))
                        mkIteratedFunTy (List.map (mkRefTupledTy cenv.g) curriedArgTys) returnTy
                    else ty'

                let memberInfoOpt =
                    match memberContainerInfo with
                    | Some tcref ->
                        let isExtrinsic = (declKind = ExtrinsicExtensionBinding)
                        let memberInfoTransient = MakeMemberDataAndMangledNameForMemberVal(cenv.g, tcref, isExtrinsic, attrs, [], memberFlags, valSynInfo, id, false)
                        Some memberInfoTransient
                    | None ->
                        None

                ValSpecResult(altActualParent, memberInfoOpt, id, enclosingDeclaredTypars, declaredTypars, ty', partialValReprInfo, declKind)

            [ yield reallyGenerateOneMember(id, valSynInfo, ty', memberFlags)
              if CompileAsEvent cenv.g attrs then
                    let valSynInfo = EventDeclarationNormalization.ConvertSynInfo id.idRange valSynInfo
                    let memberFlags = EventDeclarationNormalization.ConvertMemberFlags memberFlags
                    let delTy = FindDelegateTypeOfPropertyEvent cenv.g cenv.amap id.idText id.idRange declaredTy
                    let ty =
                       if memberFlags.IsInstance then
                         thisTy --> (delTy --> cenv.g.unit_ty)
                       else
                         (delTy --> cenv.g.unit_ty)
                    yield reallyGenerateOneMember(ident("add_" + id.idText, id.idRange), valSynInfo, ty, memberFlags)
                    yield reallyGenerateOneMember(ident("remove_" + id.idText, id.idRange), valSynInfo, ty, memberFlags) ]



        match memberFlags.MemberKind with
        | SynMemberKind.ClassConstructor
        | SynMemberKind.Constructor
        | SynMemberKind.Member
        | SynMemberKind.PropertyGet
        | SynMemberKind.PropertySet ->
            generateOneMember memberFlags, tpenv
        | SynMemberKind.PropertyGetSet ->
            [ yield! generateOneMember({memberFlags with MemberKind=SynMemberKind.PropertyGet})
              yield! generateOneMember({memberFlags with MemberKind=SynMemberKind.PropertySet}) ], tpenv
    | _ ->
        let valSynInfo = AdjustValSynInfoInSignature cenv.g declaredTy valSynInfo
        let partialValReprInfo = TranslateTopValSynInfo id.idRange (TcAttributes cenv env) valSynInfo
        [ ValSpecResult(altActualParent, None, id, enclosingDeclaredTypars, declaredTypars, declaredTy, partialValReprInfo, declKind) ], tpenv

//-------------------------------------------------------------------------
// Bind types
//-------------------------------------------------------------------------

/// Check and elaborate a type or measure parameter occurrence
/// If optKind=Some kind, then this is the kind we're expecting (we're in *analysis* mode)
/// If optKind=None, we need to determine the kind (we're in *synthesis* mode)
///
and TcTyparOrMeasurePar optKind cenv (env: TcEnv) newOk tpenv (SynTypar(id, _, _) as tp) =
    let checkRes (res: Typar) =
        match optKind, res.Kind with
        | Some TyparKind.Measure, TyparKind.Type -> error (Error(FSComp.SR.tcExpectedUnitOfMeasureMarkWithAttribute(), id.idRange)); res, tpenv
        | Some TyparKind.Type, TyparKind.Measure -> error (Error(FSComp.SR.tcExpectedTypeParameter(), id.idRange)); res, tpenv
        | _, _ ->
            let item = Item.TypeVar(id.idText, res)
            CallNameResolutionSink cenv.tcSink (id.idRange, env.NameEnv, item, emptyTyparInst, ItemOccurence.UseInType, env.AccessRights)
            res, tpenv
    let key = id.idText
    match env.eNameResEnv.eTypars.TryGetValue key with
    | true, res -> checkRes res
    | _ ->
    match TryFindUnscopedTypar key tpenv with
    | Some res -> checkRes res
    | None ->
        if newOk = NoNewTypars then
            let suggestTypeParameters (addToBuffer: string -> unit) =
                for p in env.eNameResEnv.eTypars do
                    addToBuffer ("'" + p.Key)

                match tpenv with
                | UnscopedTyparEnv elements ->
                    for p in elements do
                        addToBuffer ("'" + p.Key)

            let reportedId = Ident("'" + id.idText, id.idRange)
            error (UndefinedName(0, FSComp.SR.undefinedNameTypeParameter, reportedId, suggestTypeParameters))

        // OK, this is an implicit declaration of a type parameter
        // The kind defaults to Type
        let kind = match optKind with None -> TyparKind.Type | Some kind -> kind
        let tp' = Construct.NewTypar (kind, TyparRigidity.WarnIfNotRigid, tp, false, TyparDynamicReq.Yes, [], false, false)
        let item = Item.TypeVar(id.idText, tp')
        CallNameResolutionSink cenv.tcSink (id.idRange, env.NameEnv, item, emptyTyparInst, ItemOccurence.UseInType, env.AccessRights)
        tp', AddUnscopedTypar key tp' tpenv

and TcTypar cenv env newOk tpenv tp =
    TcTyparOrMeasurePar (Some TyparKind.Type) cenv env newOk tpenv tp

and TcTyparDecl cenv env (SynTyparDecl(Attributes synAttrs, (SynTypar(id, _, _) as stp))) =
    let attrs = TcAttributes cenv env AttributeTargets.GenericParameter synAttrs
    let hasMeasureAttr = HasFSharpAttribute cenv.g cenv.g.attrib_MeasureAttribute attrs
    let hasEqDepAttr = HasFSharpAttribute cenv.g cenv.g.attrib_EqualityConditionalOnAttribute attrs
    let hasCompDepAttr = HasFSharpAttribute cenv.g cenv.g.attrib_ComparisonConditionalOnAttribute attrs
    let attrs = attrs |> List.filter (IsMatchingFSharpAttribute cenv.g cenv.g.attrib_MeasureAttribute >> not)
    let kind = if hasMeasureAttr then TyparKind.Measure else TyparKind.Type
    let tp = Construct.NewTypar (kind, TyparRigidity.WarnIfNotRigid, stp, false, TyparDynamicReq.Yes, attrs, hasEqDepAttr, hasCompDepAttr)
    match TryFindFSharpStringAttribute cenv.g cenv.g.attrib_CompiledNameAttribute attrs with
    | Some compiledName ->
        tp.SetILName (Some compiledName)
    | None ->
        ()
    let item = Item.TypeVar(id.idText, tp)
    CallNameResolutionSink cenv.tcSink (id.idRange, env.NameEnv, item, emptyTyparInst, ItemOccurence.UseInType, env.eAccessRights)
    tp


and TcTyparDecls cenv env synTypars = List.map (TcTyparDecl cenv env) synTypars

/// Check and elaborate a syntactic type or measure
/// If optKind=Some kind, then this is the kind we're expecting (we're in *analysis* mode)
/// If optKind=None, we need to determine the kind (we're in *synthesis* mode)
///
and TcTypeOrMeasure optKind cenv newOk checkCxs occ env (tpenv: UnscopedTyparEnv) ty =
    let g = cenv.g

    match ty with
    | SynType.LongIdent(LongIdentWithDots([], _)) ->
        // special case when type name is absent - i.e. empty inherit part in type declaration
        g.obj_ty, tpenv

    | SynType.LongIdent(LongIdentWithDots(tc, _) as lidwd) ->
        let m = lidwd.Range
        let ad = env.eAccessRights
        let tinstEnclosing, tcref = ForceRaise(ResolveTypeLongIdent cenv.tcSink cenv.nameResolver occ OpenQualified env.NameEnv ad tc TypeNameResolutionStaticArgsInfo.DefiniteEmpty PermitDirectReferenceToGeneratedType.No)
        match optKind, tcref.TypeOrMeasureKind with
        | Some TyparKind.Type, TyparKind.Measure ->
            error(Error(FSComp.SR.tcExpectedTypeNotUnitOfMeasure(), m))
            NewErrorType (), tpenv
        | Some TyparKind.Measure, TyparKind.Type ->
            error(Error(FSComp.SR.tcExpectedUnitOfMeasureNotType(), m))
            TType_measure (NewErrorMeasure ()), tpenv
        | _, TyparKind.Measure ->
            TType_measure (Measure.Con tcref), tpenv
        | _, TyparKind.Type ->
            TcTypeApp cenv newOk checkCxs occ env tpenv m tcref tinstEnclosing []

    | SynType.App (StripParenTypes (SynType.LongIdent(LongIdentWithDots(tc, _))), _, args, _commas, _, postfix, m) ->
        let ad = env.eAccessRights

        let tinstEnclosing, tcref =
            let tyResInfo = TypeNameResolutionStaticArgsInfo.FromTyArgs args.Length
            ResolveTypeLongIdent cenv.tcSink cenv.nameResolver ItemOccurence.UseInType OpenQualified env.eNameResEnv ad tc tyResInfo PermitDirectReferenceToGeneratedType.No
            |> ForceRaise

        match optKind, tcref.TypeOrMeasureKind with
        | Some TyparKind.Type, TyparKind.Measure ->
            error(Error(FSComp.SR.tcExpectedTypeNotUnitOfMeasure(), m))
            NewErrorType (), tpenv

        | Some TyparKind.Measure, TyparKind.Type ->
            error(Error(FSComp.SR.tcExpectedUnitOfMeasureNotType(), m))
            TType_measure (NewErrorMeasure ()), tpenv

        | _, TyparKind.Type ->
            if postfix && tcref.Typars m |> List.exists (fun tp -> match tp.Kind with TyparKind.Measure -> true | _ -> false)
            then error(Error(FSComp.SR.tcInvalidUnitsOfMeasurePrefix(), m))
            TcTypeApp cenv newOk checkCxs occ env tpenv m tcref tinstEnclosing args
        | _, TyparKind.Measure ->
            match args, postfix with
            | [arg], true ->
                let ms, tpenv = TcMeasure cenv newOk checkCxs occ env tpenv arg m
                TType_measure (Measure.Prod(Measure.Con tcref, ms)), tpenv

            | _, _ ->
                errorR(Error(FSComp.SR.tcUnitsOfMeasureInvalidInTypeConstructor(), m))
                NewErrorType (), tpenv

    | SynType.LongIdentApp (ltyp, LongIdentWithDots(longId, _), _, args, _commas, _, m) ->
        let ad = env.eAccessRights
        let ltyp, tpenv = TcType cenv newOk checkCxs occ env tpenv ltyp
        match ltyp with
        | AppTy g (tcref, tinst) ->
            let tcref = ResolveTypeLongIdentInTyconRef cenv.tcSink cenv.nameResolver env.eNameResEnv (TypeNameResolutionInfo.ResolveToTypeRefs (TypeNameResolutionStaticArgsInfo.FromTyArgs args.Length)) ad m tcref longId
            TcTypeApp cenv newOk checkCxs occ env tpenv m tcref tinst args
        | _ -> error(Error(FSComp.SR.tcTypeHasNoNestedTypes(), m))

    | SynType.Tuple(isStruct, args, m) ->
        let tupInfo = mkTupInfo isStruct
        if isStruct then
            let args',tpenv = TcTypesAsTuple cenv newOk checkCxs occ env tpenv args m
            TType_tuple(tupInfo,args'),tpenv
        else
            let isMeasure = match optKind with Some TyparKind.Measure -> true | None -> List.exists (fun (isquot,_) -> isquot) args | _ -> false
            if isMeasure then
                let ms,tpenv = TcMeasuresAsTuple cenv newOk checkCxs occ env tpenv args m
                TType_measure ms,tpenv
            else
                let args',tpenv = TcTypesAsTuple cenv newOk checkCxs occ env tpenv args m
                TType_tuple(tupInfo,args'),tpenv

    | SynType.AnonRecd(_, [],m) ->
        error(Error((FSComp.SR.tcAnonymousTypeInvalidInDeclaration()), m))

    | SynType.AnonRecd(isStruct, args,m) ->
        let tupInfo = mkTupInfo isStruct
        let args',tpenv = TcTypesAsTuple cenv newOk checkCxs occ env tpenv (args |> List.map snd |> List.map (fun x -> (false,x))) m
        let unsortedFieldIds = args |> List.map fst |> List.toArray
        let anonInfo = AnonRecdTypeInfo.Create(cenv.topCcu, tupInfo, unsortedFieldIds)
        // Sort into canonical order
        let sortedFieldTys, sortedCheckedArgTys = List.zip args args' |> List.indexed |> List.sortBy (fun (i,_) -> unsortedFieldIds.[i].idText) |> List.map snd |> List.unzip
        sortedFieldTys |> List.iteri (fun i (x,_) ->
            let item = Item.AnonRecdField(anonInfo, sortedCheckedArgTys, i, x.idRange)
            CallNameResolutionSink cenv.tcSink (x.idRange,env.NameEnv,item,emptyTyparInst,ItemOccurence.UseInType,env.eAccessRights))
        TType_anon(anonInfo, sortedCheckedArgTys),tpenv

    | SynType.Fun(domainTy, resultTy, _) ->
        let domainTy', tpenv = TcTypeAndRecover cenv newOk checkCxs occ env tpenv domainTy
        let resultTy', tpenv = TcTypeAndRecover cenv newOk checkCxs occ env tpenv resultTy
        (domainTy' --> resultTy'), tpenv

    | SynType.Array (n, elemTy, m) ->
        let elemTy, tpenv = TcTypeAndRecover cenv newOk checkCxs occ env tpenv elemTy
        mkArrayTy g n elemTy m, tpenv

    | SynType.Var (tp, _) ->
        let tp', tpenv = TcTyparOrMeasurePar optKind cenv env newOk tpenv tp
        match tp'.Kind with
        | TyparKind.Measure -> TType_measure (Measure.Var tp'), tpenv
        | TyparKind.Type -> mkTyparTy tp', tpenv

    // _ types
    | SynType.Anon m ->
        let tp: Typar = TcAnonTypeOrMeasure optKind cenv TyparRigidity.Anon TyparDynamicReq.No newOk m
        match tp.Kind with
        | TyparKind.Measure -> TType_measure (Measure.Var tp), tpenv
        | TyparKind.Type -> mkTyparTy tp, tpenv

    | SynType.WithGlobalConstraints(ty, wcs, _) ->
        let cty, tpenv = TcTypeAndRecover cenv newOk checkCxs occ env tpenv ty
        let tpenv = TcTyparConstraints cenv newOk checkCxs occ env tpenv wcs
        cty, tpenv

    // #typ
    | SynType.HashConstraint(ty, m) ->
        let tp = TcAnonTypeOrMeasure (Some TyparKind.Type) cenv TyparRigidity.WarnIfNotRigid TyparDynamicReq.Yes newOk m
        let ty', tpenv = TcTypeAndRecover cenv newOk checkCxs occ env tpenv ty
        AddCxTypeMustSubsumeType ContextInfo.NoContext env.DisplayEnv cenv.css m NoTrace ty' (mkTyparTy tp)
        tp.AsType, tpenv

    | SynType.StaticConstant (c, m) ->
        match c, optKind with
        | _, Some TyparKind.Type ->
            errorR(Error(FSComp.SR.parsInvalidLiteralInType(), m))
            NewErrorType (), tpenv
        | SynConst.Int32 1, _ ->
            TType_measure Measure.One, tpenv
        | _ ->
            errorR(Error(FSComp.SR.parsInvalidLiteralInType(), m))
            NewErrorType (), tpenv

    | SynType.StaticConstantNamed (_, _, m)

    | SynType.StaticConstantExpr (_, m) ->
        errorR(Error(FSComp.SR.parsInvalidLiteralInType(), m))
        NewErrorType (), tpenv

    | SynType.MeasurePower(ty, exponent, m) ->
        match optKind with
        | Some TyparKind.Type ->
            errorR(Error(FSComp.SR.tcUnexpectedSymbolInTypeExpression("^"), m))
            NewErrorType (), tpenv
        | _ ->
            let ms, tpenv = TcMeasure cenv newOk checkCxs occ env tpenv ty m
            TType_measure (Measure.RationalPower (ms, TcSynRationalConst exponent)), tpenv

    | SynType.MeasureDivide(typ1, typ2, m) ->
        match optKind with
        | Some TyparKind.Type ->
            errorR(Error(FSComp.SR.tcUnexpectedSymbolInTypeExpression("/"), m))
            NewErrorType (), tpenv
        | _ ->
            let ms1, tpenv = TcMeasure cenv newOk checkCxs occ env tpenv typ1 m
            let ms2, tpenv = TcMeasure cenv newOk checkCxs occ env tpenv typ2 m
            TType_measure (Measure.Prod(ms1, Measure.Inv ms2)), tpenv

    | SynType.App(StripParenTypes (SynType.Var(_, m1) | (SynType.MeasurePower(_, _, m1))) as arg1, _, args, _commas, _, postfix, m) ->
        match optKind, args, postfix with
        | (None | Some TyparKind.Measure), [arg2], true ->
            let ms1, tpenv = TcMeasure cenv newOk checkCxs occ env tpenv arg1 m1
            let ms2, tpenv = TcMeasure cenv newOk checkCxs occ env tpenv arg2 m
            TType_measure (Measure.Prod(ms1, ms2)), tpenv

        | _ ->
            errorR(Error(FSComp.SR.tcTypeParameterInvalidAsTypeConstructor(), m))
            NewErrorType (), tpenv

    | SynType.App(_, _, _, _, _, _, m) ->
        errorR(Error(FSComp.SR.tcIllegalSyntaxInTypeExpression(), m))
        NewErrorType (), tpenv

    | SynType.Paren(innerType, _) ->
        TcTypeOrMeasure optKind cenv newOk checkCxs occ env (tpenv: UnscopedTyparEnv) innerType

and TcType cenv newOk checkCxs occ env (tpenv: UnscopedTyparEnv) ty =
    TcTypeOrMeasure (Some TyparKind.Type) cenv newOk checkCxs occ env tpenv ty

and TcMeasure cenv newOk checkCxs occ env (tpenv: UnscopedTyparEnv) (StripParenTypes ty) m =
    match ty with
    | SynType.Anon m ->
        error(Error(FSComp.SR.tcAnonymousUnitsOfMeasureCannotBeNested(), m))
        NewErrorMeasure (), tpenv
    | _ ->
        match TcTypeOrMeasure (Some TyparKind.Measure) cenv newOk checkCxs occ env tpenv ty with
        | TType_measure ms, tpenv -> ms, tpenv
        | _ ->
            error(Error(FSComp.SR.tcExpectedUnitOfMeasureNotType(), m))
            NewErrorMeasure (), tpenv

and TcAnonTypeOrMeasure optKind _cenv rigid dyn newOk m =
    if newOk = NoNewTypars then errorR (Error(FSComp.SR.tcAnonymousTypeInvalidInDeclaration(), m))
    let rigid = (if rigid = TyparRigidity.Anon && newOk = NewTyparsOKButWarnIfNotRigid then TyparRigidity.WarnIfNotRigid else rigid)
    let kind = match optKind with Some TyparKind.Measure -> TyparKind.Measure | _ -> TyparKind.Type
    NewAnonTypar (kind, m, rigid, TyparStaticReq.None, dyn)

and TcTypes cenv newOk checkCxs occ env tpenv args =
    List.mapFold (TcTypeAndRecover cenv newOk checkCxs occ env) tpenv args

and TcTypesAsTuple cenv newOk checkCxs occ env tpenv args m =
    match args with
    | [] -> error(InternalError("empty tuple type", m))
    | [(_, ty)] -> let ty, tpenv = TcTypeAndRecover cenv newOk checkCxs occ env tpenv ty in [ty], tpenv
    | (isquot, ty) :: args ->
        let ty, tpenv = TcTypeAndRecover cenv newOk checkCxs occ env tpenv ty
        let tys, tpenv = TcTypesAsTuple cenv newOk checkCxs occ env tpenv args m
        if isquot then errorR(Error(FSComp.SR.tcUnexpectedSlashInType(), m))
        ty :: tys, tpenv

// Type-check a list of measures separated by juxtaposition, * or /
and TcMeasuresAsTuple cenv newOk checkCxs occ env (tpenv: UnscopedTyparEnv) args m =
    let rec gather args tpenv isquot acc =
        match args with
        | [] -> acc, tpenv
        | (nextisquot, ty) :: args ->
            let ms1, tpenv = TcMeasure cenv newOk checkCxs occ env tpenv ty m
            gather args tpenv nextisquot (if isquot then Measure.Prod(acc, Measure.Inv ms1) else Measure.Prod(acc, ms1))
    gather args tpenv false Measure.One

and TcTypesOrMeasures optKinds cenv newOk checkCxs occ env tpenv args m =
    match optKinds with
    | None ->
        List.mapFold (TcTypeOrMeasure None cenv newOk checkCxs occ env) tpenv args
    | Some kinds ->
        if List.length kinds = List.length args then
            List.mapFold (fun tpenv (arg, kind) -> TcTypeOrMeasure (Some kind) cenv newOk checkCxs occ env tpenv arg) tpenv (List.zip args kinds)
        elif isNil kinds then error(Error(FSComp.SR.tcUnexpectedTypeArguments(), m))
        else error(Error(FSComp.SR.tcTypeParameterArityMismatch((List.length kinds), (List.length args)), m))

and TcTyparConstraints cenv newOk checkCxs occ env tpenv synConstraints =
    // Mark up default constraints with a priority in reverse order: last gets 0, second
    // last gets 1 etc. See comment on TyparConstraint.DefaultsTo
    let _, tpenv = List.fold (fun (ridx, tpenv) tc -> ridx - 1, TcTyparConstraint ridx cenv newOk checkCxs occ env tpenv tc) (List.length synConstraints - 1, tpenv) synConstraints
    tpenv

#if !NO_EXTENSIONTYPING
and TcStaticConstantParameter cenv (env: TcEnv) tpenv kind (StripParenTypes v) idOpt container =
    let g = cenv.g
    let fail() = error(Error(FSComp.SR.etInvalidStaticArgument(NicePrint.minimalStringOfType env.DisplayEnv kind), v.Range))
    let record ttype =
        match idOpt with
        | Some id ->
            let item = Item.ArgName (id, ttype, Some container)
            CallNameResolutionSink cenv.tcSink (id.idRange, env.NameEnv, item, emptyTyparInst, ItemOccurence.Use, env.AccessRights)
        | _ -> ()

    match v with
    | SynType.StaticConstant(sc, _) ->
        let v =
            match sc with
            | SynConst.Byte n when typeEquiv g g.byte_ty kind -> record(g.byte_ty); box (n: byte)
            | SynConst.Int16 n when typeEquiv g g.int16_ty kind -> record(g.int16_ty); box (n: int16)
            | SynConst.Int32 n when typeEquiv g g.int32_ty kind -> record(g.int32_ty); box (n: int)
            | SynConst.Int64 n when typeEquiv g g.int64_ty kind -> record(g.int64_ty); box (n: int64)
            | SynConst.SByte n when typeEquiv g g.sbyte_ty kind -> record(g.sbyte_ty); box (n: sbyte)
            | SynConst.UInt16 n when typeEquiv g g.uint16_ty kind -> record(g.uint16_ty); box (n: uint16)
            | SynConst.UInt32 n when typeEquiv g g.uint32_ty kind -> record(g.uint32_ty); box (n: uint32)
            | SynConst.UInt64 n when typeEquiv g g.uint64_ty kind -> record(g.uint64_ty); box (n: uint64)
            | SynConst.Decimal n when typeEquiv g g.decimal_ty kind -> record(g.decimal_ty); box (n: decimal)
            | SynConst.Single n when typeEquiv g g.float32_ty kind -> record(g.float32_ty); box (n: single)
            | SynConst.Double n when typeEquiv g g.float_ty kind -> record(g.float_ty); box (n: double)
            | SynConst.Char n when typeEquiv g g.char_ty kind -> record(g.char_ty); box (n: char)
            | SynConst.String (s, _, _)
            | SynConst.SourceIdentifier (_, s, _) when s <> null && typeEquiv g g.string_ty kind -> record(g.string_ty); box (s: string)
            | SynConst.Bool b when typeEquiv g g.bool_ty kind -> record(g.bool_ty); box (b: bool)
            | _ -> fail()
        v, tpenv
    | SynType.StaticConstantExpr(e, _ ) ->

        // If an error occurs, don't try to recover, since the constant expression will be nothing like what we need
        let te, tpenv' = TcExprNoRecover cenv kind env tpenv e

        // Evaluate the constant expression using static attribute argument rules
        let te = EvalLiteralExprOrAttribArg g te
        let v =
            match stripExpr te with
            // Check we have a residue constant. We know the type was correct because we checked the expression with this type.
            | Expr.Const (c, _, _) ->
                match c with
                | Const.Byte n -> record(g.byte_ty); box (n: byte)
                | Const.Int16 n -> record(g.int16_ty); box (n: int16)
                | Const.Int32 n -> record(g.int32_ty); box (n: int)
                | Const.Int64 n -> record(g.int64_ty); box (n: int64)
                | Const.SByte n -> record(g.sbyte_ty); box (n: sbyte)
                | Const.UInt16 n -> record(g.uint16_ty); box (n: uint16)
                | Const.UInt32 n -> record(g.uint32_ty); box (n: uint32)
                | Const.UInt64 n -> record(g.uint64_ty); box (n: uint64)
                | Const.Decimal n -> record(g.decimal_ty); box (n: decimal)
                | Const.Single n -> record(g.float32_ty); box (n: single)
                | Const.Double n -> record(g.float_ty); box (n: double)
                | Const.Char n -> record(g.char_ty); box (n: char)
                | Const.String null -> fail()
                | Const.String s -> record(g.string_ty); box (s: string)
                | Const.Bool b -> record(g.bool_ty); box (b: bool)
                | _ -> fail()
            | _ -> error(Error(FSComp.SR.tcInvalidConstantExpression(), v.Range))
        v, tpenv'
    | SynType.LongIdent lidwd ->
        let m = lidwd.Range
        TcStaticConstantParameter cenv env tpenv kind (SynType.StaticConstantExpr(SynExpr.LongIdent (false, lidwd, None, m), m)) idOpt container
    | _ ->
        fail()

and CrackStaticConstantArgs cenv env tpenv (staticParameters: Tainted<ProvidedParameterInfo>[], args: SynType list, container, containerName, m) =
    let args =
        args |> List.map (function
            | StripParenTypes (SynType.StaticConstantNamed(StripParenTypes (SynType.LongIdent(LongIdentWithDots([id], _))), v, _)) -> Some id, v
            | v -> None, v)

    let unnamedArgs = args |> Seq.takeWhile (fst >> Option.isNone) |> Seq.toArray |> Array.map snd
    let otherArgs = args |> List.skipWhile (fst >> Option.isNone)
    let namedArgs = otherArgs |> List.takeWhile (fst >> Option.isSome) |> List.map (map1Of2 Option.get)
    let otherArgs = otherArgs |> List.skipWhile (fst >> Option.isSome)
    if not otherArgs.IsEmpty then
        error (Error(FSComp.SR.etBadUnnamedStaticArgs(), m))

    let indexedStaticParameters = staticParameters |> Array.toList |> List.indexed
    for (n, _) in namedArgs do
         match indexedStaticParameters |> List.filter (fun (j, sp) -> j >= unnamedArgs.Length && n.idText = sp.PUntaint((fun sp -> sp.Name), m)) with
         | [] ->
             if staticParameters |> Array.exists (fun sp -> n.idText = sp.PUntaint((fun sp -> sp.Name), n.idRange)) then
                 error (Error(FSComp.SR.etStaticParameterAlreadyHasValue n.idText, n.idRange))
             else
                 error (Error(FSComp.SR.etNoStaticParameterWithName n.idText, n.idRange))
         | [_] -> ()
         | _ -> error (Error(FSComp.SR.etMultipleStaticParameterWithName n.idText, n.idRange))

    if staticParameters.Length < namedArgs.Length + unnamedArgs.Length then
        error (Error(FSComp.SR.etTooManyStaticParameters(staticParameters.Length, unnamedArgs.Length, namedArgs.Length), m))

    let argsInStaticParameterOrderIncludingDefaults =
        staticParameters |> Array.mapi (fun i sp ->
            let spKind = Import.ImportProvidedType cenv.amap m (sp.PApply((fun x -> x.ParameterType), m))
            let spName = sp.PUntaint((fun sp -> sp.Name), m)
            if i < unnamedArgs.Length then
                let v = unnamedArgs.[i]
                let v, _tpenv = TcStaticConstantParameter cenv env tpenv spKind v None container
                v
            else
                match namedArgs |> List.filter (fun (n, _) -> n.idText = spName) with
                | [(n, v)] ->
                    let v, _tpenv = TcStaticConstantParameter cenv env tpenv spKind v (Some n) container
                    v
                | [] ->
                    if sp.PUntaint((fun sp -> sp.IsOptional), m) then
                         match sp.PUntaint((fun sp -> sp.RawDefaultValue), m) with
                         | null -> error (Error(FSComp.SR.etStaticParameterRequiresAValue (spName, containerName, containerName, spName), m))
                         | v -> v
                    else
                      error (Error(FSComp.SR.etStaticParameterRequiresAValue (spName, containerName, containerName, spName), m))
                 | ps ->
                      error (Error(FSComp.SR.etMultipleStaticParameterWithName spName, (fst (List.last ps)).idRange)))

    argsInStaticParameterOrderIncludingDefaults

and TcProvidedTypeAppToStaticConstantArgs cenv env optGeneratedTypePath tpenv (tcref: TyconRef) (args: SynType list) m =
    let typeBeforeArguments =
        match tcref.TypeReprInfo with
        | TProvidedTypeExtensionPoint info -> info.ProvidedType
        | _ -> failwith "unreachable"

    let staticParameters = typeBeforeArguments.PApplyWithProvider((fun (typeBeforeArguments, provider) -> typeBeforeArguments.GetStaticParameters provider), range=m)
    let staticParameters = staticParameters.PApplyArray(id, "GetStaticParameters", m)

    let argsInStaticParameterOrderIncludingDefaults = CrackStaticConstantArgs cenv env tpenv (staticParameters, args, ArgumentContainer.Type tcref, tcref.DisplayName, m)

    // Take the static arguments (as SynType's) and convert them to objects of the appropriate type, based on the expected kind.
    let providedTypeAfterStaticArguments, checkTypeName =
        match ExtensionTyping.TryApplyProvidedType(typeBeforeArguments, optGeneratedTypePath, argsInStaticParameterOrderIncludingDefaults, m) with
        | None -> error(Error(FSComp.SR.etErrorApplyingStaticArgumentsToType(), m))
        | Some (ty, checkTypeName) -> (ty, checkTypeName)

    let hasNoArgs = (argsInStaticParameterOrderIncludingDefaults.Length = 0)
    hasNoArgs, providedTypeAfterStaticArguments, checkTypeName

and TryTcMethodAppToStaticConstantArgs cenv env tpenv (minfos: MethInfo list, argsOpt, mExprAndArg, mItem) =
    match minfos, argsOpt with
    | [minfo], Some (args, _) ->
        match minfo.ProvidedStaticParameterInfo with
        | Some (methBeforeArguments, staticParams) ->
            let providedMethAfterStaticArguments = TcProvidedMethodAppToStaticConstantArgs cenv env tpenv (minfo, methBeforeArguments, staticParams, args, mExprAndArg)
            let minfoAfterStaticArguments = ProvidedMeth(cenv.amap, providedMethAfterStaticArguments, minfo.ExtensionMemberPriorityOption, mItem)
            Some minfoAfterStaticArguments
        | _ -> None
    | _ -> None

and TcProvidedMethodAppToStaticConstantArgs cenv env tpenv (minfo, methBeforeArguments, staticParams, args, m) =

    let argsInStaticParameterOrderIncludingDefaults = CrackStaticConstantArgs cenv env tpenv (staticParams, args, ArgumentContainer.Method minfo, minfo.DisplayName, m)

    let providedMethAfterStaticArguments =
        match ExtensionTyping.TryApplyProvidedMethod(methBeforeArguments, argsInStaticParameterOrderIncludingDefaults, m) with
        | None -> error(Error(FSComp.SR.etErrorApplyingStaticArgumentsToMethod(), m))
        | Some meth -> meth

    providedMethAfterStaticArguments

and TcProvidedTypeApp cenv env tpenv tcref args m =
    let hasNoArgs, providedTypeAfterStaticArguments, checkTypeName = TcProvidedTypeAppToStaticConstantArgs cenv env None tpenv tcref args m

    let isGenerated = providedTypeAfterStaticArguments.PUntaint((fun st -> not st.IsErased), m)

    //printfn "adding entity for provided type '%s', isDirectReferenceToGenerated = %b, isGenerated = %b" (st.PUntaint((fun st -> st.Name), m)) isDirectReferenceToGenerated isGenerated
    let isDirectReferenceToGenerated = isGenerated && ExtensionTyping.IsGeneratedTypeDirectReference (providedTypeAfterStaticArguments, m)
    if isDirectReferenceToGenerated then
        error(Error(FSComp.SR.etDirectReferenceToGeneratedTypeNotAllowed(tcref.DisplayName), m))

    // We put the type name check after the 'isDirectReferenceToGenerated' check because we need the 'isDirectReferenceToGenerated' error to be shown for generated types
    checkTypeName()
    if hasNoArgs then
        mkAppTy tcref [], tpenv
    else
        let ty = Import.ImportProvidedType cenv.amap m providedTypeAfterStaticArguments
        ty, tpenv
#endif

/// Typecheck an application of a generic type to type arguments.
///
/// Note that the generic type may be a nested generic type List<T>.ListEnumerator<U>.
/// In this case, 'args' is only the instantiation of the suffix type arguments, and pathTypeArgs gives
/// the prefix of type arguments.
and TcTypeApp cenv newOk checkCxs occ env tpenv m tcref pathTypeArgs (synArgTys: SynType list) =
    let g = cenv.g
    CheckTyconAccessible cenv.amap m env.AccessRights tcref |> ignore
    CheckEntityAttributes g tcref m |> CommitOperationResult

#if !NO_EXTENSIONTYPING
    // Provided types are (currently) always non-generic. Their names may include mangled
    // static parameters, which are passed by the provider.
    if tcref.Deref.IsProvided then TcProvidedTypeApp cenv env tpenv tcref synArgTys m else
#endif

    let tps, _, tinst, _ = FreshenTyconRef2 m tcref

    // If we're not checking constraints, i.e. when we first assert the super/interfaces of a type definition, then just
    // clear the constraint lists of the freshly generated type variables. A little ugly but fairly localized.
    if checkCxs = NoCheckCxs then tps |> List.iter (fun tp -> tp.SetConstraints [])
    let synArgTysLength = synArgTys.Length
    let pathTypeArgsLength = pathTypeArgs.Length
    if tinst.Length <> pathTypeArgsLength + synArgTysLength then
        error (TyconBadArgs(env.DisplayEnv, tcref, pathTypeArgsLength + synArgTysLength, m))

    let argTys, tpenv =
        // Get the suffix of typars
        let tpsForArgs = List.skip (tps.Length - synArgTysLength) tps
        let kindsForArgs = tpsForArgs |> List.map (fun tp -> tp.Kind)
        TcTypesOrMeasures (Some kindsForArgs) cenv newOk checkCxs occ env tpenv synArgTys m

    // Add the types of the enclosing class for a nested type
    let actualArgTys = pathTypeArgs @ argTys

    if checkCxs = CheckCxs then
        List.iter2 (UnifyTypes cenv env m) tinst actualArgTys

    // Try to decode System.Tuple --> F~ tuple types etc.
    let ty = g.decompileType tcref actualArgTys

    ty, tpenv

and TcTypeOrMeasureAndRecover optKind cenv newOk checkCxs occ env tpenv ty =
    try TcTypeOrMeasure optKind cenv newOk checkCxs occ env tpenv ty
    with e ->
        errorRecovery e ty.Range
        let rty =
            match optKind, newOk with
            | Some TyparKind.Measure, NoNewTypars -> TType_measure Measure.One
            | Some TyparKind.Measure, _ -> TType_measure (NewErrorMeasure ())
            | _, NoNewTypars -> cenv.g.obj_ty
            | _ -> NewErrorType ()
        rty, tpenv


and TcTypeAndRecover cenv newOk checkCxs occ env tpenv ty =
    TcTypeOrMeasureAndRecover (Some TyparKind.Type) cenv newOk checkCxs occ env tpenv ty

and TcNestedTypeApplication cenv newOk checkCxs occ env tpenv mWholeTypeApp ty pathTypeArgs tyargs =
    let ty = convertToTypeWithMetadataIfPossible cenv.g ty
    if not (isAppTy cenv.g ty) then error(Error(FSComp.SR.tcTypeHasNoNestedTypes(), mWholeTypeApp))
    match ty with
    | TType_app(tcref, _) ->
        TcTypeApp cenv newOk checkCxs occ env tpenv mWholeTypeApp tcref pathTypeArgs tyargs
    | _ -> error(InternalError("TcNestedTypeApplication: expected type application", mWholeTypeApp))


and TryAdjustHiddenVarNameToCompGenName cenv env (id: Ident) altNameRefCellOpt =
    match altNameRefCellOpt with
    | Some ({contents = SynSimplePatAlternativeIdInfo.Undecided altId } as altNameRefCell) ->
        match ResolvePatternLongIdent cenv.tcSink cenv.nameResolver AllIdsOK false id.idRange env.eAccessRights env.eNameResEnv TypeNameResolutionInfo.Default [id] with
        | Item.NewDef _ -> None // the name is not in scope as a pattern identifier (e.g. union case), so do not use the alternate ID
        | _ -> altNameRefCell := SynSimplePatAlternativeIdInfo.Decided altId; Some altId // the name is in scope as a pattern identifier, so use the alternate ID
    | Some ({contents = SynSimplePatAlternativeIdInfo.Decided altId }) -> Some altId
    | None -> None

/// Bind the patterns used in a lambda. Not clear why we don't use TcPat.
and TcSimplePat optArgsOK checkCxs cenv ty env (tpenv, names, takenNames) p =
    match p with
    | SynSimplePat.Id (id, altNameRefCellOpt, compgen, isMemberThis, isOpt, m) ->
        // Check to see if pattern translation decides to use an alternative identifier.
        match TryAdjustHiddenVarNameToCompGenName cenv env id altNameRefCellOpt with
        | Some altId -> TcSimplePat optArgsOK checkCxs cenv ty env (tpenv, names, takenNames) (SynSimplePat.Id (altId, None, compgen, isMemberThis, isOpt, m) )
        | None ->
            if isOpt then
                if not optArgsOK then
                    errorR(Error(FSComp.SR.tcOptionalArgsOnlyOnMembers(), m))

                let tyarg = NewInferenceType ()
                UnifyTypes cenv env m ty (mkOptionTy cenv.g tyarg)

            let _, names, takenNames = TcPatBindingName cenv env id ty isMemberThis None None (ValInline.Optional, permitInferTypars, noArgOrRetAttribs, false, None, compgen) (names, takenNames)
            id.idText,
            (tpenv, names, takenNames)

    | SynSimplePat.Typed (p, cty, m) ->
        let cty', tpenv = TcTypeAndRecover cenv NewTyparsOK checkCxs ItemOccurence.UseInType env tpenv cty
        match p with
        // Optional arguments on members
        | SynSimplePat.Id(_, _, _, _, true, _) -> UnifyTypes cenv env m ty (mkOptionTy cenv.g cty')
        | _ -> UnifyTypes cenv env m ty cty'

        TcSimplePat optArgsOK checkCxs cenv ty env (tpenv, names, takenNames) p

    | SynSimplePat.Attrib (p, _, _) ->
        TcSimplePat optArgsOK checkCxs cenv ty env (tpenv, names, takenNames) p

// raise an error if any optional args precede any non-optional args
and ValidateOptArgOrder (spats: SynSimplePats) =

    let rec getPats spats =
        match spats with
        | SynSimplePats.SimplePats(p, m) -> p, m
        | SynSimplePats.Typed(p, _, _) -> getPats p

    let rec isOptArg pat =
        match pat with
        | SynSimplePat.Id (_, _, _, _, isOpt, _) -> isOpt
        | SynSimplePat.Typed (p, _, _) -> isOptArg p
        | SynSimplePat.Attrib (p, _, _) -> isOptArg p

    let pats, m = getPats spats

    let mutable hitOptArg = false

    List.iter (fun pat -> if isOptArg pat then hitOptArg <- true elif hitOptArg then error(Error(FSComp.SR.tcOptionalArgsMustComeAfterNonOptionalArgs(), m))) pats


/// Bind the patterns used in argument position for a function, method or lambda.
and TcSimplePats cenv optArgsOK checkCxs ty env (tpenv, names, takenNames: Set<_>) p =

    // validate optional argument declaration
    ValidateOptArgOrder p

    match p with
    | SynSimplePats.SimplePats ([], m) ->
        // Unit "()" patterns in argument position become SynSimplePats.SimplePats([], _) in the
        // syntactic translation when building bindings. This is done because the
        // use of "()" has special significance for arity analysis and argument counting.
        //
        // Here we give a name to the single argument implied by those patterns.
        // This is a little awkward since it would be nice if this was
        // uniform with the process where we give names to other (more complex)
        // patterns used in argument position, e.g. "let f (D(x)) = ..."
        let id = ident("unitVar" + string takenNames.Count, m)
        UnifyTypes cenv env m ty cenv.g.unit_ty
        let _, names, takenNames = TcPatBindingName cenv env id ty false None None (ValInline.Optional, permitInferTypars, noArgOrRetAttribs, false, None, true) (names, takenNames)
        [id.idText], (tpenv, names, takenNames)

    | SynSimplePats.SimplePats ([p], _) ->
        let v, (tpenv, names, takenNames) = TcSimplePat optArgsOK checkCxs cenv ty env (tpenv, names, takenNames) p
        [v], (tpenv, names, takenNames)

    | SynSimplePats.SimplePats (ps, m) ->
        let ptys = UnifyRefTupleType env.eContextInfo cenv env.DisplayEnv m ty ps
        let ps', (tpenv, names, takenNames) = List.mapFold (fun tpenv (ty, e) -> TcSimplePat optArgsOK checkCxs cenv ty env tpenv e) (tpenv, names, takenNames) (List.zip ptys ps)
        ps', (tpenv, names, takenNames)

    | SynSimplePats.Typed (p, cty, m) ->
        let cty', tpenv = TcTypeAndRecover cenv NewTyparsOK CheckCxs ItemOccurence.UseInType env tpenv cty

        match p with
        // Solitary optional arguments on members
        | SynSimplePats.SimplePats([SynSimplePat.Id(_, _, _, _, true, _)], _) -> UnifyTypes cenv env m ty (mkOptionTy cenv.g cty')
        | _ -> UnifyTypes cenv env m ty cty'

        TcSimplePats cenv optArgsOK checkCxs ty env (tpenv, names, takenNames) p

and TcSimplePatsOfUnknownType cenv optArgsOK checkCxs env tpenv spats =
    let argty = NewInferenceType ()
    TcSimplePats cenv optArgsOK checkCxs argty env (tpenv, NameMap.empty, Set.empty) spats

and TcPatBindingName cenv env id ty isMemberThis vis1 topValData (inlineFlag, declaredTypars, argAttribs, isMutable, vis2, compgen) (names, takenNames: Set<string>) =
    let vis = if Option.isSome vis1 then vis1 else vis2
    if takenNames.Contains id.idText then errorR (VarBoundTwice id)
    let compgen = compgen || IsCompilerGeneratedName id.idText
    let baseOrThis = if isMemberThis then MemberThisVal else NormalVal
    let names = Map.add id.idText (PrelimValScheme1(id, declaredTypars, ty, topValData, None, isMutable, inlineFlag, baseOrThis, argAttribs, vis, compgen)) names
    let takenNames = Set.add id.idText takenNames
    (fun (TcPatPhase2Input (values, isLeftMost)) ->
        let (vspec, typeScheme) =
            let name = id.idText
            match values.TryGetValue name with
            | true, value ->
                if not (String.IsNullOrEmpty name) && not (String.isLeadingIdentifierCharacterUpperCase name) then
                    match env.eNameResEnv.ePatItems.TryGetValue name with
                    | true, Item.Value vref when vref.LiteralValue.IsSome ->
                        warning(Error(FSComp.SR.checkLowercaseLiteralBindingInPattern name, id.idRange))
                    | _ -> ()
                value
            | _ -> error(Error(FSComp.SR.tcNameNotBoundInPattern name, id.idRange))

        // isLeftMost indicates we are processing the left-most path through a disjunctive or pattern.
        // For those binding locations, CallNameResolutionSink is called in MakeAndPublishValue, like all other bindings
        // For non-left-most paths, we register the name resolutions here
        if not isLeftMost && not vspec.IsCompilerGenerated && not (vspec.LogicalName.StartsWithOrdinal("_")) then
            let item = Item.Value(mkLocalValRef vspec)
            CallNameResolutionSink cenv.tcSink (id.idRange, env.NameEnv, item, emptyTyparInst, ItemOccurence.Binding, env.AccessRights)

        PBind(vspec, typeScheme)),
    names, takenNames

and TcPatAndRecover warnOnUpper cenv (env: TcEnv) topValInfo vFlags (tpenv, names, takenNames) ty (pat: SynPat) =
    try
       TcPat warnOnUpper cenv env topValInfo vFlags (tpenv, names, takenNames) ty pat
    with e ->
        // Error recovery - return some rubbish expression, but replace/annotate
        // the type of the current expression with a type variable that indicates an error
        let m = pat.Range
        errorRecovery e m
        //solveTypAsError cenv env.DisplayEnv m ty
        (fun _ -> TPat_error m), (tpenv, names, takenNames)

/// Typecheck a pattern. Patterns are type-checked in three phases:
/// 1. TcPat builds a List.map from simple variable names to inferred types for
///   those variables. It also returns a function to perform the second phase.
/// 2. The second phase assumes the caller has built the actual value_spec's
///    for the values being defined, and has decided if the types of these
///    variables are to be generalized. The caller hands this information to
///    the second-phase function in terms of a List.map from names to actual
///    value specifications.
and TcPat warnOnUpper cenv env topValInfo vFlags (tpenv, names, takenNames) ty pat =
    let ad = env.AccessRights
    match pat with
    | SynPat.As(_, SynPat.Named _, _) -> ()
    | SynPat.As (_, _, m) -> checkLanguageFeatureError cenv.g.langVersion LanguageFeature.NonVariablePatternsToRightOfAsPatterns m
    | _ -> ()
    match pat with
    | SynPat.Const (c, m) ->
        match c with
        | SynConst.Bytes (bytes, _, m) ->
            UnifyTypes cenv env m ty (mkByteArrayTy cenv.g)
            TcPat warnOnUpper cenv env None vFlags (tpenv, names, takenNames) ty (SynPat.ArrayOrList (true, [ for b in bytes -> SynPat.Const(SynConst.Byte b, m) ], m))

        | SynConst.UserNum _ ->
            errorR (Error (FSComp.SR.tcInvalidNonPrimitiveLiteralInPatternMatch (), m))
            (fun _ -> TPat_error m), (tpenv, names, takenNames)

        | _ ->
            try
                let c' = TcConst cenv ty m env c
                (fun _ -> TPat_const (c', m)), (tpenv, names, takenNames)
            with e ->
                errorRecovery e m
                (fun _ -> TPat_error m), (tpenv, names, takenNames)

    | SynPat.Wild m ->
        (fun _ -> TPat_wild m), (tpenv, names, takenNames)

    | SynPat.IsInst(cty, m)
    | SynPat.As (SynPat.IsInst(cty, m), _, _) ->
        let srcTy = ty
        let tgtTy, tpenv = TcTypeAndRecover cenv NewTyparsOKButWarnIfNotRigid CheckCxs ItemOccurence.UseInType env tpenv cty
        TcRuntimeTypeTest (*isCast*)false (*isOperator*)true cenv env.DisplayEnv m tgtTy srcTy
        match pat with
        | SynPat.IsInst(_, m) ->
            (fun _ -> TPat_isinst (srcTy, tgtTy, None, m)), (tpenv, names, takenNames)
        | SynPat.As (SynPat.IsInst _, p, m) ->
            let pat, acc = TcPat warnOnUpper cenv env topValInfo vFlags (tpenv, names, takenNames) tgtTy p
            (fun values -> TPat_isinst (srcTy, tgtTy, Some (pat values), m)), acc
        | _ -> failwith "TcPat"

    | SynPat.As (p, SynPat.Named (id, isMemberThis, vis, m), _)
    | SynPat.As (SynPat.Named (id, isMemberThis, vis, m), p, _) ->
        let bindf, names, takenNames = TcPatBindingName cenv env id ty isMemberThis vis topValInfo vFlags (names, takenNames)
        let pat', acc = TcPat warnOnUpper cenv env None vFlags (tpenv, names, takenNames) ty p
        (fun values -> TPat_as (pat' values, bindf values, m)),
        acc

    | SynPat.As (pat1, pat2, m) ->
        let pats = [pat1; pat2]
        let pats', acc = TcPatterns warnOnUpper cenv env vFlags (tpenv, names, takenNames) (List.map (fun _ -> ty) pats) pats
        (fun values -> TPat_conjs(List.map (fun f -> f values) pats', m)), acc
        
    | SynPat.Named (id, isMemberThis, vis, m) ->
        let bindf, names, takenNames = TcPatBindingName cenv env id ty isMemberThis vis topValInfo vFlags (names, takenNames)
        let pat', acc = TcPat warnOnUpper cenv env None vFlags (tpenv, names, takenNames) ty (SynPat.Wild m)
        (fun values -> TPat_as (pat' values, bindf values, m)),
        acc

    | SynPat.OptionalVal (id, m) ->
        errorR (Error (FSComp.SR.tcOptionalArgsOnlyOnMembers (), m))
        let bindf, names, takenNames = TcPatBindingName cenv env id ty false None topValInfo vFlags (names, takenNames)
        (fun values -> TPat_as (TPat_wild m, bindf values, m)), (tpenv, names, takenNames)

    | SynPat.Typed (p, cty, m) ->
        let cty', tpenv = TcTypeAndRecover cenv NewTyparsOK CheckCxs ItemOccurence.UseInType env tpenv cty
        UnifyTypes cenv env m ty cty'
        TcPat warnOnUpper cenv env topValInfo vFlags (tpenv, names, takenNames) ty p

    | SynPat.Attrib (p, attrs, _) ->
        errorR (Error (FSComp.SR.tcAttributesInvalidInPatterns (), rangeOfNonNilAttrs attrs))
        for attrList in attrs do
            TcAttributes cenv env Unchecked.defaultof<_> attrList.Attributes |> ignore
        TcPat warnOnUpper cenv env None vFlags (tpenv, names, takenNames) ty p

    | SynPat.Or (pat1, pat2, m) ->
        let pat1', (tpenv, names1, takenNames1) = TcPat warnOnUpper cenv env None vFlags (tpenv, names, takenNames) ty pat1
        let pat2', (tpenv, names2, takenNames2) = TcPat warnOnUpper cenv env None vFlags (tpenv, names, takenNames) ty pat2
        if not (takenNames1 = takenNames2) then
            errorR (UnionPatternsBindDifferentNames m)

        names1 |> Map.iter (fun _ (PrelimValScheme1 (id1, _, ty1, _, _, _, _, _, _, _, _)) ->
            match names2.TryGetValue id1.idText with
            | true, PrelimValScheme1 (id2, _, ty2, _, _, _, _, _, _, _, _) ->
                try UnifyTypes cenv env id2.idRange ty1 ty2
                with e -> errorRecovery e m
            | _ -> ())

        let names = NameMap.layer names1 names2
        let takenNames = Set.union takenNames1 takenNames2
        (fun values -> TPat_disjs ([pat1' values; pat2' values.RightPath], m)), (tpenv, names, takenNames)

    | SynPat.Ands (pats, m) ->
        let pats', acc = TcPatterns warnOnUpper cenv env vFlags (tpenv, names, takenNames) (List.map (fun _ -> ty) pats) pats
        (fun values -> TPat_conjs(List.map (fun f -> f values) pats', m)), acc

    | SynPat.LongIdent (LongIdentWithDots(longId, _), _, tyargs, args, vis, m) ->
        if Option.isSome tyargs then errorR(Error(FSComp.SR.tcInvalidTypeArgumentUsage(), m))
        let warnOnUpperForId =
            match args with
            | SynArgPats.Pats [] -> warnOnUpper
            | _ -> AllIdsOK

        let lidRange = rangeOfLid longId

        let checkNoArgsForLiteral () =
            match args with
            | SynArgPats.Pats []
            | SynArgPats.NamePatPairs ([], _) -> ()
            | _ -> errorR (Error (FSComp.SR.tcLiteralDoesNotTakeArguments (), m))

        let getArgPatterns () =
            match args with
            | SynArgPats.Pats args -> args
            | SynArgPats.NamePatPairs (pairs, _) -> List.map snd pairs

        let tcArgPatterns () =
            let args = getArgPatterns ()
            TcPatterns warnOnUpper cenv env vFlags (tpenv, names, takenNames) (NewInferenceTypes args) args

        // Note we parse arguments to parameterized pattern labels as patterns, not expressions.
        // This means the range of syntactic expression forms that can be used here is limited.
        let rec convSynPatToSynExpr x =
            match x with
            | SynPat.FromParseError(p, _) -> convSynPatToSynExpr p
            | SynPat.Const (c, m) -> SynExpr.Const (c, m)
            | SynPat.Named (id, _, None, _) -> SynExpr.Ident id
            | SynPat.Typed (p, cty, m) -> SynExpr.Typed (convSynPatToSynExpr p, cty, m)
            | SynPat.LongIdent (LongIdentWithDots(longId, dotms) as lidwd, _, _tyargs, args, None, m) ->
                let args = match args with SynArgPats.Pats args -> args | _ -> failwith "impossible: active patterns can be used only with SynConstructorArgs.Pats"
                let e =
                    if dotms.Length = longId.Length then
                        let e = SynExpr.LongIdent (false, LongIdentWithDots(longId, List.truncate (dotms.Length - 1) dotms), None, m)
                        SynExpr.DiscardAfterMissingQualificationAfterDot (e, unionRanges e.Range (List.last dotms))
                    else SynExpr.LongIdent (false, lidwd, None, m)
                List.fold (fun f x -> mkSynApp1 f (convSynPatToSynExpr x) m) e args
            | SynPat.Tuple (isStruct, args, m) -> SynExpr.Tuple (isStruct, List.map convSynPatToSynExpr args, [], m)
            | SynPat.Paren (p, _) -> convSynPatToSynExpr p
            | SynPat.ArrayOrList (isArray, args, m) -> SynExpr.ArrayOrList (isArray,List.map convSynPatToSynExpr args, m)
            | SynPat.QuoteExpr (e,_) -> e
            | SynPat.Null m -> SynExpr.Null m
            | _ -> error(Error(FSComp.SR.tcInvalidArgForParameterizedPattern(), x.Range))

        let isNameof (id: Ident) =
            id.idText = "nameof" &&
            try
                match ResolveExprLongIdent cenv.tcSink cenv.nameResolver m ad env.NameEnv TypeNameResolutionInfo.Default [id] with
                | Result (_, Item.Value vref, _) -> valRefEq cenv.g vref cenv.g.nameof_vref
                | _ -> false
            with _ -> false

        match ResolvePatternLongIdent cenv.tcSink cenv.nameResolver warnOnUpperForId false m ad env.NameEnv TypeNameResolutionInfo.Default longId with
        | Item.NewDef id ->
            match getArgPatterns () with
            | [] ->
                TcPat warnOnUpperForId cenv env topValInfo vFlags (tpenv, names, takenNames) ty (mkSynPatVar vis id)

            | [arg]
                when cenv.g.langVersion.SupportsFeature LanguageFeature.NameOf && isNameof id ->
                match TcNameOfExpr cenv env tpenv (convSynPatToSynExpr arg) with
                | Expr.Const(c, m, _) -> (fun _ -> TPat_const (c, m)), (tpenv, names, takenNames)
                | _ -> failwith "Impossible: TcNameOfExpr must return an Expr.Const"
            | _ ->
                let _, acc = tcArgPatterns ()
                errorR (UndefinedName (0, FSComp.SR.undefinedNamePatternDiscriminator, id, NoSuggestions))
                (fun _ -> TPat_error m), acc

        | Item.ActivePatternCase (APElemRef (apinfo, vref, idx, isStructRetTy)) as item ->
            // Report information about the 'active recognizer' occurrence to IDE
            CallNameResolutionSink cenv.tcSink (rangeOfLid longId, env.NameEnv, item, emptyTyparInst, ItemOccurence.Pattern, env.eAccessRights)

            match args with
            | SynArgPats.Pats _ -> ()
            | _ -> errorR (Error (FSComp.SR.tcNamedActivePattern (apinfo.ActiveTags.[idx]), m))

            let args = getArgPatterns ()

            // TOTAL/PARTIAL ACTIVE PATTERNS
            let _, vexp, _, _, tinst, _ = TcVal true cenv env tpenv vref None None m
            let vexp = MakeApplicableExprWithFlex cenv env vexp
            let vexpty = vexp.Type

            let activePatArgsAsSynPats, patarg =
                match args with
                | [] -> [], SynPat.Const(SynConst.Unit, m)
                | _ ->
                    // This bit of type-directed analysis ensures that parameterized partial active patterns returning unit do not need to take an argument
                    // See FSharp 1.0 3502
                    let dtys, rty = stripFunTy cenv.g vexpty

                    if dtys.Length = args.Length + 1 && isOptionTy cenv.g rty && isUnitTy cenv.g (destOptionTy cenv.g rty) then
                        args, SynPat.Const(SynConst.Unit, m)
                    else
                        List.frontAndBack args

            if not (isNil activePatArgsAsSynPats) && apinfo.ActiveTags.Length <> 1 then
                errorR (Error (FSComp.SR.tcRequireActivePatternWithOneResult (), m))

            let activePatArgsAsSynExprs = List.map convSynPatToSynExpr activePatArgsAsSynPats

            let activePatResTys = NewInferenceTypes apinfo.Names
            let activePatType = apinfo.OverallType cenv.g m ty activePatResTys isStructRetTy

            let delayed = activePatArgsAsSynExprs |> List.map (fun arg -> DelayedApp(ExprAtomicFlag.NonAtomic, arg, unionRanges (rangeOfLid longId) arg.Range))
            let activePatExpr, tpenv = PropagateThenTcDelayed cenv activePatType env tpenv m vexp vexpty ExprAtomicFlag.NonAtomic delayed

            if idx >= activePatResTys.Length then error(Error(FSComp.SR.tcInvalidIndexIntoActivePatternArray(), m))
            let argty = List.item idx activePatResTys

            let arg', acc = TcPat warnOnUpper cenv env None vFlags (tpenv, names, takenNames) argty patarg

            // The identity of an active pattern consists of its value and the types it is applied to.
            // If there are any expression args then we've lost identity.
            let activePatIdentity = if isNil activePatArgsAsSynExprs then Some (vref, tinst) else None
            (fun values ->
                TPat_query((activePatExpr, activePatResTys, isStructRetTy, activePatIdentity, idx, apinfo), arg' values, m)), acc

        | (Item.UnionCase _ | Item.ExnCase _) as item ->
            // Report information about the case occurrence to IDE
            CallNameResolutionSink cenv.tcSink (lidRange, env.NameEnv, item, emptyTyparInst, ItemOccurence.Pattern, env.eAccessRights)

            let mkf, argTys, argNames = ApplyUnionCaseOrExnTypesForPat m cenv env ty item
            let numArgTys = argTys.Length

            let args, extraPatternsFromNames =
                match args with
                | SynArgPats.Pats args -> args, []
                | SynArgPats.NamePatPairs (pairs, m) ->
                    // rewrite patterns from the form (name-N = pat-N; ...) to (..._, pat-N, _...)
                    // so type T = Case of name: int * value: int
                    // | Case(value = v)
                    // will become
                    // | Case(_, v)
                    let result = Array.zeroCreate numArgTys
                    let extraPatterns = List ()

                    for (id, pat) in pairs do
                        match argNames |> List.tryFindIndex (fun id2 -> id.idText = id2.idText) with
                        | None ->
                            extraPatterns.Add pat
                            match item with
                            | Item.UnionCase(uci, _) ->
                                errorR (Error (FSComp.SR.tcUnionCaseConstructorDoesNotHaveFieldWithGivenName (uci.Name, id.idText), id.idRange))
                            | Item.ExnCase tcref ->
                                errorR (Error (FSComp.SR.tcExceptionConstructorDoesNotHaveFieldWithGivenName (tcref.DisplayName, id.idText), id.idRange))
                            | _ ->
                                errorR (Error (FSComp.SR.tcConstructorDoesNotHaveFieldWithGivenName (id.idText), id.idRange))

                        | Some idx ->
                            let argItem =
                                match item with
                                | Item.UnionCase (uci, _) -> Item.UnionCaseField (uci, idx)
                                | Item.ExnCase tref -> Item.RecdField (RecdFieldInfo ([], RecdFieldRef (tref, id.idText)))
                                | _ -> failwithf "Expecting union case or exception item, got: %O" item

                            CallNameResolutionSink cenv.tcSink (id.idRange, env.NameEnv, argItem, emptyTyparInst, ItemOccurence.Pattern, ad)

                            match box result.[idx] with
                            | null -> result.[idx] <- pat
                            | _ ->
                                extraPatterns.Add pat
                                errorR (Error (FSComp.SR.tcUnionCaseFieldCannotBeUsedMoreThanOnce (id.idText), id.idRange))

                    for i = 0 to numArgTys - 1 do
                        if isNull (box result.[i]) then
                            result.[i] <- SynPat.Wild (m.MakeSynthetic())

                    let extraPatterns =
                        if isNull extraPatterns then [] else List.ofSeq extraPatterns

                    let args = List.ofArray result
                    if result.Length = 1 then args, extraPatterns
                    else [ SynPat.Tuple(false, args, m) ], extraPatterns

            let args, extraPatterns =
                match args with
                | [] -> [], []

                // note: the next will always be parenthesized
                | [SynPatErrorSkip(SynPat.Tuple (false, args, _)) | SynPatErrorSkip(SynPat.Paren(SynPatErrorSkip(SynPat.Tuple (false, args, _)), _))] when numArgTys > 1 -> args, []

                // note: we allow both 'C _' and 'C (_)' regardless of number of argument of the pattern
                | [SynPatErrorSkip(SynPat.Wild _ as e) | SynPatErrorSkip(SynPat.Paren(SynPatErrorSkip(SynPat.Wild _ as e), _))] -> List.replicate numArgTys e, []


                | args when numArgTys = 0 ->
                    errorR (Error (FSComp.SR.tcUnionCaseDoesNotTakeArguments (), m))
                    [], args

                | arg :: rest when numArgTys = 1 ->
                    if numArgTys = 1 && not (List.isEmpty rest) then
                        errorR (Error (FSComp.SR.tcUnionCaseRequiresOneArgument (), m))
                    [arg], rest

                | [arg] -> [arg], []

                | args ->
                    [], args

            let args, extraPatterns =
                let numArgs = args.Length
                if numArgs = numArgTys then
                    args, extraPatterns
                elif numArgs < numArgTys then
                    if numArgTys > 1 then
                        // Expects tuple without enough args
                        errorR (Error (FSComp.SR.tcUnionCaseExpectsTupledArguments numArgTys, m))
                    else
                        errorR (UnionCaseWrongArguments (env.DisplayEnv, numArgTys, numArgs, m))
                    args @ (List.init (numArgTys - numArgs) (fun _ -> SynPat.Wild (m.MakeSynthetic()))), extraPatterns
                else
                    let args, remaining = args |> List.splitAt numArgTys
                    for remainingArg in remaining do
                        errorR (UnionCaseWrongArguments (env.DisplayEnv, numArgTys, numArgs, remainingArg.Range))
                    args, extraPatterns @ remaining

            let extraPatterns = extraPatterns @ extraPatternsFromNames
            let args', acc = TcPatterns warnOnUpper cenv env vFlags (tpenv, names, takenNames) argTys args
            let _, acc = TcPatterns warnOnUpper cenv env vFlags acc (NewInferenceTypes extraPatterns) extraPatterns
            (fun values -> mkf m (List.map (fun f -> f values) args')), acc

        | Item.ILField finfo ->
            CheckILFieldInfoAccessible cenv.g cenv.amap lidRange env.AccessRights finfo
            if not finfo.IsStatic then
                errorR (Error (FSComp.SR.tcFieldIsNotStatic (finfo.FieldName), lidRange))
            CheckILFieldAttributes cenv.g finfo m
            match finfo.LiteralValue with
            | None -> error (Error (FSComp.SR.tcFieldNotLiteralCannotBeUsedInPattern (), lidRange))
            | Some lit ->
                checkNoArgsForLiteral ()
                let _, acc = tcArgPatterns ()

                UnifyTypes cenv env m ty (finfo.FieldType (cenv.amap, m))
                let c' = TcFieldInit lidRange lit
                let item = Item.ILField finfo
                CallNameResolutionSink cenv.tcSink (lidRange, env.NameEnv, item, emptyTyparInst, ItemOccurence.Pattern, env.AccessRights)
                (fun _ -> TPat_const (c', m)), acc

        | Item.RecdField rfinfo ->
            CheckRecdFieldInfoAccessible cenv.amap lidRange env.AccessRights rfinfo
            if not rfinfo.IsStatic then errorR (Error (FSComp.SR.tcFieldIsNotStatic(rfinfo.Name), lidRange))
            CheckRecdFieldInfoAttributes cenv.g rfinfo lidRange |> CommitOperationResult
            match rfinfo.LiteralValue with
            | None -> error (Error(FSComp.SR.tcFieldNotLiteralCannotBeUsedInPattern(), lidRange))
            | Some lit ->
                checkNoArgsForLiteral()
                let _, acc = tcArgPatterns ()

                UnifyTypes cenv env m ty rfinfo.FieldType
                let item = Item.RecdField rfinfo
                // FUTURE: can we do better than emptyTyparInst here, in order to display instantiations
                // of type variables in the quick info provided in the IDE.
                CallNameResolutionSink cenv.tcSink (lidRange, env.NameEnv, item, emptyTyparInst, ItemOccurence.Pattern, env.AccessRights)
                (fun _ -> TPat_const (lit, m)), acc

        | Item.Value vref ->
            match vref.LiteralValue with
            | None -> error (Error(FSComp.SR.tcNonLiteralCannotBeUsedInPattern(), m))
            | Some lit ->
                let _, _, _, vexpty, _, _ = TcVal true cenv env tpenv vref None None lidRange
                CheckValAccessible lidRange env.AccessRights vref
                CheckFSharpAttributes cenv.g vref.Attribs lidRange |> CommitOperationResult
                checkNoArgsForLiteral()
                let _, acc = tcArgPatterns ()

                UnifyTypes cenv env m ty vexpty
                let item = Item.Value vref
                CallNameResolutionSink cenv.tcSink (lidRange, env.NameEnv, item, emptyTyparInst, ItemOccurence.Pattern, env.AccessRights)
                (fun _ -> TPat_const (lit, m)), acc

        | _ -> error (Error(FSComp.SR.tcRequireVarConstRecogOrLiteral(), m))

    | SynPat.QuoteExpr(_, m) ->
        errorR (Error(FSComp.SR.tcInvalidPattern(), m))
        (fun _ -> TPat_error m), (tpenv, names, takenNames)

    | SynPat.Tuple (isExplicitStruct, args, m) ->
        try
            let tupInfo, argTys = UnifyTupleTypeAndInferCharacteristics env.eContextInfo cenv env.DisplayEnv m ty isExplicitStruct args
            let args', acc = TcPatterns warnOnUpper cenv env vFlags (tpenv, names, takenNames) argTys args
            (fun values -> TPat_tuple(tupInfo, List.map (fun f -> f values) args', argTys, m)), acc
        with e ->
            errorRecovery e m
            let _, acc = TcPatterns warnOnUpper cenv env vFlags (tpenv, names, takenNames) (NewInferenceTypes args) args
            (fun _ -> TPat_error m), acc

    | SynPat.Paren (p, _) ->
        TcPat warnOnUpper cenv env None vFlags (tpenv, names, takenNames) ty p

    | SynPat.ArrayOrList (isArray, args, m) ->
        let argty = NewInferenceType ()
        UnifyTypes cenv env m ty (if isArray then mkArrayType cenv.g argty else mkListTy cenv.g argty)
        let args', acc = TcPatterns warnOnUpper cenv env vFlags (tpenv, names, takenNames) (List.map (fun _ -> argty) args) args
        (fun values ->
            let args' = List.map (fun f -> f values) args'
            if isArray then TPat_array(args', argty, m)
            else List.foldBack (mkConsListPat cenv.g argty) args' (mkNilListPat cenv.g m argty)), acc

    | SynPat.Record (flds, m) ->
        let tinst, tcref, fldsmap, _fldsList = BuildFieldMap cenv env true ty flds m
        // REVIEW: use _fldsList to type check pattern in code order not field defn order
        let gtyp = mkAppTy tcref tinst
        let inst = List.zip (tcref.Typars m) tinst
        UnifyTypes cenv env m ty gtyp
        let fields = tcref.TrueInstanceFieldsAsList
        let ftys = fields |> List.map (fun fsp -> actualTyOfRecdField inst fsp, fsp)
        let fldsmap', acc =
          ((tpenv, names, takenNames), ftys) ||> List.mapFold (fun s (ty, fsp) ->
              match fldsmap.TryGetValue fsp.rfield_id.idText with
              | true, v -> TcPat warnOnUpper cenv env None vFlags s ty v
              | _ -> (fun _ -> TPat_wild m), s)
        (fun values -> TPat_recd (tcref, tinst, List.map (fun f -> f values) fldsmap', m)),
        acc

    | SynPat.DeprecatedCharRange (c1, c2, m) ->
        errorR(Deprecated(FSComp.SR.tcUseWhenPatternGuard(), m))
        UnifyTypes cenv env m ty (cenv.g.char_ty)
        (fun _ -> TPat_range(c1, c2, m)), (tpenv, names, takenNames)

    | SynPat.Null m ->
        try AddCxTypeMustSupportNull env.DisplayEnv cenv.css m NoTrace ty
        with e -> errorRecovery e m
        (fun _ -> TPat_null m), (tpenv, names, takenNames)

    | SynPat.InstanceMember (_, _, _, _, m) ->
        errorR(Error(FSComp.SR.tcIllegalPattern(), pat.Range))
        (fun _ -> TPat_wild m), (tpenv, names, takenNames)

    | SynPat.FromParseError (pat, _) ->
        suppressErrorReporting (fun () -> TcPatAndRecover warnOnUpper cenv env topValInfo vFlags (tpenv, names, takenNames) (NewErrorType()) pat)

and TcPatterns warnOnUpper cenv env vFlags s argTys args =
    assert (List.length args = List.length argTys)
    List.mapFold (fun s (ty, pat) -> TcPat warnOnUpper cenv env None vFlags s ty pat) s (List.zip argTys args)

and solveTypAsError cenv denv m ty = ConstraintSolver.SolveTypeAsError denv cenv.css m ty

and RecordNameAndTypeResolutions_IdeallyWithoutHavingOtherEffects cenv env tpenv expr =
    // This function is motivated by cases like
    //    query { for ... join(for x in f(). }
    // where there is incomplete code in a query, and we are current just dropping a piece of the AST on the floor (above, the bit inside the 'join').
    //
    // The problem with dropping the AST on the floor is that we get no captured resolutions, which means no Intellisense/QuickInfo/ParamHelp.
    //
    // The idea behind the fix is to semi-typecheck this AST-fragment, just to get resolutions captured.
    //
    // The tricky bit is to not also have any other effects from typechecking, namely producing error diagnostics (which may be spurious) or having
    // side-effects on the typecheck environment.
    //
    // REVIEW: We are yet to deal with the tricky bit. As it stands, we turn off error logging, but still have typechecking environment effects. As a result,
    // at the very least, you cannot call this function unless you're already reported a typechecking error (the 'worst' possible outcome would be
    // to incorrectly solve typecheck constraints as a result of effects in this function, and then have the code compile successfully and behave
    // in some weird way; so ensure the code can't possibly compile before calling this function as an expedient way to get better IntelliSense).
    suppressErrorReporting (fun () ->
        try ignore(TcExprOfUnknownType cenv env tpenv expr)
        with e -> ())

and RecordNameAndTypeResolutions_IdeallyWithoutHavingOtherEffects_Delayed cenv env tpenv delayed =

    let rec dummyCheckedDelayed delayed =
        match delayed with
        | DelayedApp (_hpa, arg, _mExprAndArg) :: otherDelayed ->
            RecordNameAndTypeResolutions_IdeallyWithoutHavingOtherEffects cenv env tpenv arg
            dummyCheckedDelayed otherDelayed
        | _ -> ()
    dummyCheckedDelayed delayed

// Calls UnifyTypes, but upon error only does the minimal error recovery
// so that IntelliSense information can continue to be collected.
and UnifyTypesAndRecover cenv env m expectedTy actualTy =
    try
        UnifyTypes cenv env m expectedTy actualTy
    with e ->
        errorRecovery e m

and TcExprOfUnknownType cenv env tpenv expr =
    let exprty = NewInferenceType ()
    let expr', tpenv = TcExpr cenv exprty env tpenv expr
    expr', exprty, tpenv

and TcExprFlex cenv flex compat ty (env: TcEnv) tpenv (e: SynExpr) =
    if flex then
        let argty = NewInferenceType ()
        if compat then
            (destTyparTy cenv.g argty).SetIsCompatFlex(true)
        AddCxTypeMustSubsumeType ContextInfo.NoContext env.DisplayEnv cenv.css e.Range NoTrace ty argty
        let e', tpenv = TcExpr cenv argty env tpenv e
        let e' = mkCoerceIfNeeded cenv.g ty argty e'
        e', tpenv
    else
        TcExpr cenv ty env tpenv e


and TcExpr cenv ty (env: TcEnv) tpenv (expr: SynExpr) =
    // Start an error recovery handler
    // Note the try/with can lead to tail-recursion problems for iterated constructs, e.g. let... in...
    // So be careful!
    try
        TcExprNoRecover cenv ty env tpenv expr
    with e ->
        let m = expr.Range
        // Error recovery - return some rubbish expression, but replace/annotate
        // the type of the current expression with a type variable that indicates an error
        errorRecovery e m
        solveTypAsError cenv env.DisplayEnv m ty
        mkThrow m ty (mkOne cenv.g m), tpenv

and TcExprNoRecover cenv ty (env: TcEnv) tpenv (expr: SynExpr) =

    // Count our way through the expression shape that makes up an object constructor
    // See notes at definition of "ctor" re. object model constructors.
    let env =
        if GetCtorShapeCounter env > 0 then AdjustCtorShapeCounter (fun x -> x - 1) env
        else env

    TcExprThen cenv ty env tpenv expr []


// This recursive entry is only used from one callsite (DiscardAfterMissingQualificationAfterDot)
// and has been added relatively late in F# 4.0 to preserve the structure of previous code. It pushes a 'delayed' parameter
// through TcExprOfUnknownType, TcExpr and TcExprNoRecover
and TcExprOfUnknownTypeThen cenv env tpenv expr delayed =
    let exprty = NewInferenceType ()
    let expr', tpenv =
      try
        TcExprThen cenv exprty env tpenv expr delayed
      with e ->
        let m = expr.Range
        errorRecovery e m
        solveTypAsError cenv env.DisplayEnv m exprty
        mkThrow m exprty (mkOne cenv.g m), tpenv
    expr', exprty, tpenv

/// This is used to typecheck legitimate 'main body of constructor' expressions
and TcExprThatIsCtorBody safeInitInfo cenv overallTy env tpenv expr =
    let env = {env with eCtorInfo = Some (InitialExplicitCtorInfo safeInitInfo) }
    let expr, tpenv = TcExpr cenv overallTy env tpenv expr
    let expr = CheckAndRewriteObjectCtor cenv.g env expr
    expr, tpenv

/// This is used to typecheck all ordinary expressions including constituent
/// parts of ctor.
and TcExprThatCanBeCtorBody cenv overallTy env tpenv expr =
    let env = if AreWithinCtorShape env then AdjustCtorShapeCounter (fun x -> x + 1) env else env
    TcExpr cenv overallTy env tpenv expr

/// This is used to typecheck legitimate 'non-main body of object constructor' expressions
and TcExprThatCantBeCtorBody cenv overallTy env tpenv expr =
    let env = if AreWithinCtorShape env then ExitCtorShapeRegion env else env
    TcExpr cenv overallTy env tpenv expr

/// This is used to typecheck legitimate 'non-main body of object constructor' expressions
and TcStmtThatCantBeCtorBody cenv env tpenv expr =
    let env = if AreWithinCtorShape env then ExitCtorShapeRegion env else env
    TcStmt cenv env tpenv expr

and TcStmt cenv env tpenv synExpr =
    let expr, ty, tpenv = TcExprOfUnknownType cenv env tpenv synExpr
    let m = synExpr.Range
    let wasUnit = UnifyUnitType cenv env m ty expr
    if wasUnit then
        expr, tpenv
    else
        mkCompGenSequential m expr (mkUnit cenv.g m), tpenv

and TryTcStmt cenv env tpenv synExpr =
    let expr, ty, tpenv = TcExprOfUnknownType cenv env tpenv synExpr
    let m = synExpr.Range
    let hasTypeUnit = TryUnifyUnitTypeWithoutWarning cenv env m ty
    hasTypeUnit, expr, tpenv

/// During checking of expressions of the form (x(y)).z(w1, w2)
/// keep a stack of things on the right. This lets us recognize
/// method applications and other item-based syntax.
and TcExprThen cenv overallTy env tpenv synExpr delayed =
    match synExpr with

    | LongOrSingleIdent (isOpt, longId, altNameRefCellOpt, mLongId) ->
        if isOpt then errorR(Error(FSComp.SR.tcSyntaxErrorUnexpectedQMark(), mLongId))
        // Check to see if pattern translation decided to use an alternative identifier.
        match altNameRefCellOpt with
        | Some {contents = SynSimplePatAlternativeIdInfo.Decided altId} -> TcExprThen cenv overallTy env tpenv (SynExpr.LongIdent (isOpt, LongIdentWithDots([altId], []), None, mLongId)) delayed
        | _ -> TcLongIdentThen cenv overallTy env tpenv longId delayed

    // f x
    | SynExpr.App (hpa, _, func, arg, mFuncAndArg) ->
        TcExprThen cenv overallTy env tpenv func ((DelayedApp (hpa, arg, mFuncAndArg)) :: delayed)

    // e<tyargs>
    | SynExpr.TypeApp (func, _, typeArgs, _, _, mTypeArgs, mFuncAndTypeArgs) ->
        TcExprThen cenv overallTy env tpenv func ((DelayedTypeApp (typeArgs, mTypeArgs, mFuncAndTypeArgs)) :: delayed)

    // e1.id1
    // e1.id1.id2
    // etc.
    | SynExpr.DotGet (e1, _, LongIdentWithDots(longId, _), _) ->
        TcExprThen cenv overallTy env tpenv e1 ((DelayedDotLookup (longId, synExpr.RangeWithoutAnyExtraDot)) :: delayed)

    // e1.[e2]
    // e1.[e21, ..., e2n]
    // etc.
    | SynExpr.DotIndexedGet (e1, e2, mDot, mWholeExpr) ->
        TcIndexerThen cenv env overallTy mWholeExpr mDot tpenv synExpr e1 e2 delayed

    // e1.[e2] <- e3
    // e1.[e21, ..., e2n] <- e3
    // etc.
    | SynExpr.DotIndexedSet (e1, e2, _, _, mDot, mWholeExpr) ->
        TcIndexerThen cenv env overallTy mWholeExpr mDot tpenv synExpr e1 e2 delayed

    | _ ->
        match delayed with
        | [] -> TcExprUndelayed cenv overallTy env tpenv synExpr
        | _ ->
            let expr, exprty, tpenv = TcExprUndelayedNoType cenv env tpenv synExpr
            PropagateThenTcDelayed cenv overallTy env tpenv synExpr.Range (MakeApplicableExprNoFlex cenv expr) exprty ExprAtomicFlag.NonAtomic delayed

and TcExprs cenv env m tpenv flexes argTys args =
    if List.length args <> List.length argTys then error(Error(FSComp.SR.tcExpressionCountMisMatch((List.length argTys), (List.length args)), m))
    (tpenv, List.zip3 flexes argTys args) ||> List.mapFold (fun tpenv (flex, ty, e) ->
         TcExprFlex cenv flex false ty env tpenv e)

and CheckSuperInit cenv objTy m =
    // Check the type is not abstract
    match tryTcrefOfAppTy cenv.g objTy with
    | ValueSome tcref when isAbstractTycon tcref.Deref ->
        errorR(Error(FSComp.SR.tcAbstractTypeCannotBeInstantiated(), m))
    | _ -> ()

//-------------------------------------------------------------------------
// TcExprUndelayed
//-------------------------------------------------------------------------

and TcExprUndelayedNoType cenv env tpenv synExpr: Expr * TType * _ =
    let overallTy = NewInferenceType ()
    let expr, tpenv = TcExprUndelayed cenv overallTy env tpenv synExpr
    expr, overallTy, tpenv

and TcExprUndelayed cenv overallTy env tpenv (synExpr: SynExpr) =

    match synExpr with
    | SynExpr.Paren (expr2, _, _, mWholeExprIncludingParentheses) ->
        // We invoke CallExprHasTypeSink for every construct which is atomic in the syntax, i.e. where a '.' immediately following the
        // construct is a dot-lookup for the result of the construct.
        CallExprHasTypeSink cenv.tcSink (mWholeExprIncludingParentheses, env.NameEnv, overallTy, env.AccessRights)
        let env = ShrinkContext env mWholeExprIncludingParentheses expr2.Range
        TcExpr cenv overallTy env tpenv expr2

    | SynExpr.DotIndexedGet _ | SynExpr.DotIndexedSet _
    | SynExpr.TypeApp _ | SynExpr.Ident _ | SynExpr.LongIdent _ | SynExpr.App _ | SynExpr.DotGet _ -> error(Error(FSComp.SR.tcExprUndelayed(), synExpr.Range))

    | SynExpr.Const (SynConst.String (s, _, m), _) ->
        CallExprHasTypeSink cenv.tcSink (m, env.NameEnv, overallTy, env.AccessRights)
        TcConstStringExpr cenv overallTy env m tpenv s

    | SynExpr.InterpolatedString (parts, _, m) ->
        checkLanguageFeatureError cenv.g.langVersion LanguageFeature.StringInterpolation m

        CallExprHasTypeSink cenv.tcSink (m, env.NameEnv, overallTy, env.AccessRights)

        TcInterpolatedStringExpr cenv overallTy env m tpenv parts

    | SynExpr.Const (synConst, m) ->
        CallExprHasTypeSink cenv.tcSink (m, env.NameEnv, overallTy, env.AccessRights)
        TcConstExpr cenv overallTy env m tpenv synConst

    | SynExpr.Lambda _ ->
        TcIteratedLambdas cenv true env overallTy Set.empty tpenv synExpr

    | SynExpr.Match (spMatch, synInputExpr, synClauses, _m) ->

        let inputExpr, inputTy, tpenv = TcExprOfUnknownType cenv env tpenv synInputExpr
        let mInputExpr = inputExpr.Range
        let matchVal, matchExpr, tpenv = TcAndPatternCompileMatchClauses mInputExpr mInputExpr ThrowIncompleteMatchException cenv (Some inputExpr) inputTy overallTy env tpenv synClauses
        let overallExpr = mkLet spMatch mInputExpr matchVal inputExpr matchExpr
        overallExpr, tpenv

    // (function[spMatch] pat1 -> expr1 ... | patN -> exprN)
    //
    //  -->
    //      (fun anonArg -> let[spMatch] anonVal = anonArg in pat1 -> expr1 ... | patN -> exprN)
    //
    // Note the presence of the "let" is visible in quotations regardless of the presence of sequence points, so
    //     <@ function x -> (x: int) @>
    // is
    //     Lambda (_arg2, Let (x, _arg2, x))

    | SynExpr.MatchLambda (isExnMatch, mArg, clauses, spMatch, m) ->

        let domainTy, resultTy = UnifyFunctionType None cenv env.DisplayEnv m overallTy
        let idv1, idve1 = mkCompGenLocal mArg (cenv.synArgNameGenerator.New()) domainTy
        let envinner = ExitFamilyRegion env
        let idv2, matchExpr, tpenv = TcAndPatternCompileMatchClauses m mArg (if isExnMatch then Throw else ThrowIncompleteMatchException) cenv None domainTy resultTy envinner tpenv clauses
        let overallExpr = mkMultiLambda m [idv1] ((mkLet spMatch m idv2 idve1 matchExpr), resultTy)
        overallExpr, tpenv

    | SynExpr.Assert (x, m) ->
        TcAssertExpr cenv overallTy env m tpenv x

    | SynExpr.Fixed (_, m) ->
        error(Error(FSComp.SR.tcFixedNotAllowed(), m))

    // e: ty
    | SynExpr.Typed (synBodyExpr, synType, m) ->
        let tgtTy, tpenv = TcTypeAndRecover cenv NewTyparsOK CheckCxs ItemOccurence.UseInType env tpenv synType
        UnifyTypes cenv env m overallTy tgtTy
        let expr, tpenv = TcExpr cenv overallTy env tpenv synBodyExpr
        expr, tpenv

    // e :? ty
    | SynExpr.TypeTest (synInnerExpr, tgtTy, m) ->
        let innerExpr, srcTy, tpenv = TcExprOfUnknownType cenv env tpenv synInnerExpr
        UnifyTypes cenv env m overallTy cenv.g.bool_ty
        let tgtTy, tpenv = TcType cenv NewTyparsOK CheckCxs ItemOccurence.UseInType env tpenv tgtTy
        TcRuntimeTypeTest (*isCast*)false (*isOperator*)true cenv env.DisplayEnv m tgtTy srcTy
        let expr = mkCallTypeTest cenv.g m tgtTy innerExpr
        expr, tpenv

    // SynExpr.AddressOf is noted in the syntax ast in order to recognize it as concrete type information
    // during type checking, in particular prior to resolving overloads. This helps distinguish
    // its use at method calls from the use of the conflicting 'ref' mechanism for passing byref parameters
    | SynExpr.AddressOf (byref, synInnerExpr, opm, m) ->
        TcExpr cenv overallTy env tpenv (mkSynPrefixPrim opm m (if byref then "~&" else "~&&") synInnerExpr)

    | SynExpr.Upcast (synInnerExpr, _, m) | SynExpr.InferredUpcast (synInnerExpr, m) ->
        let innerExpr, srcTy, tpenv = TcExprOfUnknownType cenv env tpenv synInnerExpr
        let tgtTy, tpenv =
          match synExpr with
          | SynExpr.Upcast (_, tgtTy, m) ->
              let tgtTy, tpenv = TcType cenv NewTyparsOK CheckCxs ItemOccurence.UseInType env tpenv tgtTy
              UnifyTypes cenv env m tgtTy overallTy
              tgtTy, tpenv
          | SynExpr.InferredUpcast _ ->
              overallTy, tpenv
          | _ -> failwith "upcast"
        TcStaticUpcast cenv env.DisplayEnv m tgtTy srcTy
        let expr = mkCoerceExpr(innerExpr, tgtTy, m, srcTy)
        expr, tpenv

    | SynExpr.Downcast (synInnerExpr, _, m) | SynExpr.InferredDowncast (synInnerExpr, m) ->
        let innerExpr, srcTy, tpenv = TcExprOfUnknownType cenv env tpenv synInnerExpr
        let tgtTy, tpenv, isOperator =
          match synExpr with
          | SynExpr.Downcast (_, tgtTy, m) ->
              let tgtTy, tpenv = TcType cenv NewTyparsOK CheckCxs ItemOccurence.UseInType env tpenv tgtTy
              UnifyTypes cenv env m tgtTy overallTy
              tgtTy, tpenv, true
          | SynExpr.InferredDowncast _ -> overallTy, tpenv, false
          | _ -> failwith "downcast"
        TcRuntimeTypeTest (*isCast*)true isOperator cenv env.DisplayEnv m tgtTy srcTy

        // TcRuntimeTypeTest ensures tgtTy is a nominal type. Hence we can insert a check here
        // based on the nullness semantics of the nominal type.
        let expr = mkCallUnbox cenv.g m tgtTy innerExpr
        expr, tpenv

    | SynExpr.Null m ->
        AddCxTypeMustSupportNull env.DisplayEnv cenv.css m NoTrace overallTy
        mkNull m overallTy, tpenv

    | SynExpr.Lazy (synInnerExpr, m) ->
        let innerTy = NewInferenceType ()
        UnifyTypes cenv env m overallTy (mkLazyTy cenv.g innerTy)
        let innerExpr, tpenv = TcExpr cenv innerTy env tpenv synInnerExpr
        let expr = mkLazyDelayed cenv.g m innerTy (mkUnitDelayLambda cenv.g m innerExpr)
        expr, tpenv

    | SynExpr.Tuple (isExplicitStruct, args, _, m) ->
        let tupInfo, argTys = UnifyTupleTypeAndInferCharacteristics env.eContextInfo cenv env.DisplayEnv m overallTy isExplicitStruct args
        // No subsumption at tuple construction
        let flexes = argTys |> List.map (fun _ -> false)
        let args', tpenv = TcExprs cenv env m tpenv flexes argTys args
        let expr = mkAnyTupled cenv.g m tupInfo args' argTys
        expr, tpenv

    | SynExpr.AnonRecd (isStruct, optOrigExpr, unsortedFieldExprs, mWholeExpr) ->
        TcAnonRecdExpr cenv overallTy env tpenv (isStruct, optOrigExpr, unsortedFieldExprs, mWholeExpr)

    | SynExpr.ArrayOrList (isArray, args, m) ->
        CallExprHasTypeSink cenv.tcSink (m, env.NameEnv, overallTy, env.AccessRights)

        let argty = NewInferenceType ()
        UnifyTypes cenv env m overallTy (if isArray then mkArrayType cenv.g argty else mkListTy cenv.g argty)

        // Always allow subsumption if a nominal type is known prior to type checking any arguments
        let flex = not (isTyparTy cenv.g argty)
        let mutable first = true
        let getInitEnv m =
            if first then
                first <- false
                env
            else
                { env with eContextInfo = ContextInfo.CollectionElement (isArray, m) }

        let args', tpenv = List.mapFold (fun tpenv (x: SynExpr) -> TcExprFlex cenv flex false argty (getInitEnv x.Range) tpenv x) tpenv args

        let expr =
            if isArray then Expr.Op (TOp.Array, [argty], args', m)
            else List.foldBack (mkCons cenv.g argty) args' (mkNil cenv.g m argty)
        expr, tpenv

    | SynExpr.New (superInit, synObjTy, arg, mNewExpr) ->
        let objTy, tpenv = TcType cenv NewTyparsOK CheckCxs ItemOccurence.Use env tpenv synObjTy
        UnifyTypes cenv env mNewExpr overallTy objTy
        TcNewExpr cenv env tpenv objTy (Some synObjTy.Range) superInit arg mNewExpr

    | SynExpr.ObjExpr (objTy, argopt, binds, extraImpls, mNewExpr, m) ->
        CallExprHasTypeSink cenv.tcSink (m, env.NameEnv, overallTy, env.eAccessRights)
        TcObjectExpr cenv overallTy env tpenv (objTy, argopt, binds, extraImpls, mNewExpr, m)

    | SynExpr.Record (inherits, optOrigExpr, flds, mWholeExpr) ->
        CallExprHasTypeSink cenv.tcSink (mWholeExpr, env.NameEnv, overallTy, env.AccessRights)
        TcRecdExpr cenv overallTy env tpenv (inherits, optOrigExpr, flds, mWholeExpr)

    | SynExpr.While (spWhile, synGuardExpr, synBodyExpr, m) ->
        UnifyTypes cenv env m overallTy cenv.g.unit_ty
        let guardExpr, tpenv = TcExpr cenv cenv.g.bool_ty env tpenv synGuardExpr
        let bodyExpr, tpenv = TcStmt cenv env tpenv synBodyExpr
        mkWhile cenv.g (spWhile, NoSpecialWhileLoopMarker, guardExpr, bodyExpr, m), tpenv

    | SynExpr.For (spBind, id, start, dir, finish, body, m) ->
        UnifyTypes cenv env m overallTy cenv.g.unit_ty
        let startExpr, tpenv = TcExpr cenv cenv.g.int_ty env tpenv start
        let finishExpr, tpenv = TcExpr cenv cenv.g.int_ty env tpenv finish
        let idv, _ = mkLocal id.idRange id.idText cenv.g.int_ty
        let envinner = AddLocalVal cenv.g cenv.tcSink m idv env

        // notify name resolution sink about loop variable
        let item = Item.Value(mkLocalValRef idv)
        CallNameResolutionSink cenv.tcSink (idv.Range, env.NameEnv, item, emptyTyparInst, ItemOccurence.Binding, env.AccessRights)

        let bodyExpr, tpenv = TcStmt cenv envinner tpenv body
        mkFastForLoop cenv.g (spBind, m, idv, startExpr, dir, finishExpr, bodyExpr), tpenv

    | SynExpr.ForEach (spForLoop, SeqExprOnly seqExprOnly, isFromSource, pat, enumSynExpr, bodySynExpr, m) ->
        assert isFromSource
        if seqExprOnly then warning (Error(FSComp.SR.tcExpressionRequiresSequence(), m))
        TcForEachExpr cenv overallTy env tpenv (pat, enumSynExpr, bodySynExpr, m, spForLoop)

    | SynExpr.CompExpr (isArrayOrList, isNotNakedRefCell, comp, m) ->
        let env = ExitFamilyRegion env
        cenv.TcSequenceExpressionEntry cenv env overallTy tpenv (isArrayOrList, isNotNakedRefCell, comp) m

    | SynExpr.ArrayOrListOfSeqExpr (isArray, comp, m) ->
        CallExprHasTypeSink cenv.tcSink (m, env.NameEnv, overallTy, env.eAccessRights)
        cenv.TcArrayOrListSequenceExpression cenv env overallTy tpenv (isArray, comp)  m

    | SynExpr.LetOrUse _ ->
        TcLinearExprs (TcExprThatCanBeCtorBody cenv) cenv env overallTy tpenv false synExpr (fun x -> x)

    | SynExpr.TryWith (synBodyExpr, _mTryToWith, synWithClauses, mWithToLast, mTryToLast, spTry, spWith) ->
        let bodyExpr, tpenv = TcExpr cenv overallTy env tpenv synBodyExpr
        // Compile the pattern twice, once as a List.filter with all succeeding targets returning "1", and once as a proper catch block.
        let filterClauses = synWithClauses |> List.map (function (SynMatchClause(pat, optWhenExpr, _, m, _)) -> SynMatchClause(pat, optWhenExpr, (SynExpr.Const (SynConst.Int32 1, m)), m, DebugPointForTarget.No))
        let checkedFilterClauses, tpenv = TcMatchClauses cenv cenv.g.exn_ty cenv.g.int_ty env tpenv filterClauses
        let checkedHandlerClauses, tpenv = TcMatchClauses cenv cenv.g.exn_ty overallTy env tpenv synWithClauses
        let v1, filterExpr = CompilePatternForMatchClauses cenv env mWithToLast mWithToLast true FailFilter None cenv.g.exn_ty cenv.g.int_ty checkedFilterClauses
        let v2, handlerExpr = CompilePatternForMatchClauses cenv env mWithToLast mWithToLast true Rethrow None cenv.g.exn_ty overallTy checkedHandlerClauses
        mkTryWith cenv.g (bodyExpr, v1, filterExpr, v2, handlerExpr, mTryToLast, overallTy, spTry, spWith), tpenv

    | SynExpr.TryFinally (synBodyExpr, synFinallyExpr, mTryToLast, spTry, spFinally) ->
        let bodyExpr, tpenv = TcExpr cenv overallTy env tpenv synBodyExpr
        let finallyExpr, tpenv = TcStmt cenv env tpenv synFinallyExpr
        mkTryFinally cenv.g (bodyExpr, finallyExpr, mTryToLast, overallTy, spTry, spFinally), tpenv

    | SynExpr.JoinIn (e1, mInToken, e2, mAll) ->
        errorR(Error(FSComp.SR.parsUnfinishedExpression("in"), mInToken))
        let _, _, tpenv = suppressErrorReporting (fun () -> TcExprOfUnknownType cenv env tpenv e1)
        let _, _, tpenv = suppressErrorReporting (fun () -> TcExprOfUnknownType cenv env tpenv e2)
        mkDefault(mAll, overallTy), tpenv

    | SynExpr.ArbitraryAfterError (_debugStr, m) ->
        //solveTypAsError cenv env.DisplayEnv m overallTy
        mkDefault(m, overallTy), tpenv

    // expr. (already reported as an error)
    | SynExpr.DiscardAfterMissingQualificationAfterDot (e1, m) ->
        let _, _, tpenv = suppressErrorReporting (fun () -> TcExprOfUnknownTypeThen cenv env tpenv e1 [DelayedDot])
        mkDefault(m, overallTy), tpenv

    | SynExpr.FromParseError (e1, m) ->
        //solveTypAsError cenv env.DisplayEnv m overallTy
        let _, tpenv = suppressErrorReporting (fun () -> TcExpr cenv overallTy env tpenv e1)
        mkDefault(m, overallTy), tpenv

    | SynExpr.Sequential (sp, dir, synExpr1, synExpr2, m) ->
        if dir then
            TcLinearExprs (TcExprThatCanBeCtorBody cenv) cenv env overallTy tpenv false synExpr (fun x -> x)
        else
            // Constructors using "new (...) = <ctor-expr> then <expr>"
            let expr1, tpenv = TcExprThatCanBeCtorBody cenv overallTy env tpenv synExpr1
            if (GetCtorShapeCounter env) <> 1 then
                errorR(Error(FSComp.SR.tcExpressionFormRequiresObjectConstructor(), m))
            let expr2, tpenv = TcStmtThatCantBeCtorBody cenv env tpenv synExpr2
            Expr.Sequential (expr1, expr2, ThenDoSeq, sp, m), tpenv

    // Used to implement the type-directed 'implicit yield' rule for computation expressions
    | SynExpr.SequentialOrImplicitYield (sp, synExpr1, synExpr2, otherExpr, m) ->
        let isStmt, expr1, tpenv = TryTcStmt cenv env tpenv synExpr1
        if isStmt then
            let env = ShrinkContext env m synExpr2.Range
            let expr2, tpenv = TcExprThatCanBeCtorBody cenv overallTy env tpenv synExpr2
            Expr.Sequential(expr1, expr2, NormalSeq, sp, m), tpenv
        else
            // The first expression wasn't unit-typed, so proceed to the alternative interpretation
            // Note a copy of the first expression is embedded in 'otherExpr' and thus
            // this will type-check the first expression over again.
            TcExpr cenv overallTy env tpenv otherExpr

    | SynExpr.Do (synInnerExpr, m) ->
        UnifyTypes cenv env m overallTy cenv.g.unit_ty
        TcStmtThatCantBeCtorBody cenv env tpenv synInnerExpr

    | SynExpr.IfThenElse _ ->
        TcLinearExprs (TcExprThatCanBeCtorBody cenv) cenv env overallTy tpenv false synExpr (fun x -> x)

    // This is for internal use in the libraries only
    | SynExpr.LibraryOnlyStaticOptimization (constraints, e2, e3, m) ->
        let constraints', tpenv = List.mapFold (TcStaticOptimizationConstraint cenv env) tpenv constraints
        // Do not force the types of the two expressions to be equal
        // This means uses of this construct have to be very carefully written
        let e2', _, tpenv = TcExprOfUnknownType cenv env tpenv e2
        let e3', tpenv = TcExpr cenv overallTy env tpenv e3
        Expr.StaticOptimization (constraints', e2', e3', m), tpenv

    /// e1.longId <- e2
    | SynExpr.DotSet (e1, (LongIdentWithDots(longId, _) as lidwd), e2, mStmt) ->
        if lidwd.ThereIsAnExtraDotAtTheEnd then
            // just drop rhs on the floor
            let mExprAndDotLookup = unionRanges e1.Range (rangeOfLid longId)
            TcExprThen cenv overallTy env tpenv e1 [DelayedDotLookup(longId, mExprAndDotLookup)]
        else
            let mExprAndDotLookup = unionRanges e1.Range (rangeOfLid longId)
            TcExprThen cenv overallTy env tpenv e1 [DelayedDotLookup(longId, mExprAndDotLookup); MakeDelayedSet(e2, mStmt)]

    /// e1 <- e2
    | SynExpr.Set (e1, e2, mStmt) ->
        TcExprThen cenv overallTy env tpenv e1 [MakeDelayedSet(e2, mStmt)]

    /// e1.longId(e2) <- e3, very rarely used named property setters
    | SynExpr.DotNamedIndexedPropertySet (e1, (LongIdentWithDots(longId, _) as lidwd), e2, e3, mStmt) ->
        if lidwd.ThereIsAnExtraDotAtTheEnd then
            // just drop rhs on the floor
            let mExprAndDotLookup = unionRanges e1.Range (rangeOfLid longId)
            TcExprThen cenv overallTy env tpenv e1 [DelayedDotLookup(longId, mExprAndDotLookup)]
        else
            let mExprAndDotLookup = unionRanges e1.Range (rangeOfLid longId)
            TcExprThen cenv overallTy env tpenv e1 [DelayedDotLookup(longId, mExprAndDotLookup); DelayedApp(ExprAtomicFlag.Atomic, e2, mStmt); MakeDelayedSet(e3, mStmt)]

    | SynExpr.LongIdentSet (lidwd, e2, m) ->
        if lidwd.ThereIsAnExtraDotAtTheEnd then
            // just drop rhs on the floor
            TcLongIdentThen cenv overallTy env tpenv lidwd [ ]
        else
            TcLongIdentThen cenv overallTy env tpenv lidwd [ MakeDelayedSet(e2, m) ]

    // Type.Items(e1) <- e2
    | SynExpr.NamedIndexedPropertySet (lidwd, e1, e2, mStmt) ->
        if lidwd.ThereIsAnExtraDotAtTheEnd then
            // just drop rhs on the floor
            TcLongIdentThen cenv overallTy env tpenv lidwd [ ]
        else
            TcLongIdentThen cenv overallTy env tpenv lidwd [ DelayedApp(ExprAtomicFlag.Atomic, e1, mStmt); MakeDelayedSet(e2, mStmt) ]

    | SynExpr.TraitCall (tps, memSpfn, arg, m) ->
        let synTypes = tps |> List.map (fun tp -> SynType.Var(tp, m))
        let traitInfo, tpenv = TcPseudoMemberSpec cenv NewTyparsOK env synTypes tpenv memSpfn m
        if BakedInTraitConstraintNames.Contains traitInfo.MemberName then
            warning(BakedInMemberConstraintName(traitInfo.MemberName, m))

        let argTys = traitInfo.ArgumentTypes
        let returnTy = GetFSharpViewOfReturnType cenv.g traitInfo.ReturnType
        let args, namedCallerArgs = GetMethodArgs arg
        if not (isNil namedCallerArgs) then errorR(Error(FSComp.SR.tcNamedArgumentsCannotBeUsedInMemberTraits(), m))
        // Subsumption at trait calls if arguments have nominal type prior to unification of any arguments or return type
        let flexes = argTys |> List.map (isTyparTy cenv.g >> not)
        let args', tpenv = TcExprs cenv env m tpenv flexes argTys args
        AddCxMethodConstraint env.DisplayEnv cenv.css m NoTrace traitInfo
        UnifyTypes cenv env m overallTy returnTy
        Expr.Op (TOp.TraitCall traitInfo, [], args', m), tpenv

    | SynExpr.LibraryOnlyUnionCaseFieldGet (e1, c, n, m) ->
        let e1', ty1, tpenv = TcExprOfUnknownType cenv env tpenv e1
        let mkf, ty2 = TcUnionCaseOrExnField cenv env ty1 m c n
                          ((fun (a, b) n -> mkUnionCaseFieldGetUnproven cenv.g (e1', a, b, n, m)),
                           (fun a n -> mkExnCaseFieldGet(e1', a, n, m)))
        UnifyTypes cenv env m overallTy ty2
        mkf n, tpenv

    | SynExpr.LibraryOnlyUnionCaseFieldSet (e1, c, n, e2, m) ->
        UnifyTypes cenv env m overallTy cenv.g.unit_ty
        let e1', ty1, tpenv = TcExprOfUnknownType cenv env tpenv e1
        let mkf, ty2 = TcUnionCaseOrExnField cenv env ty1 m c n
                          ((fun (a, b) n e2' ->
                             if not (isUnionCaseFieldMutable cenv.g a n) then errorR(Error(FSComp.SR.tcFieldIsNotMutable(), m))
                             mkUnionCaseFieldSet(e1', a, b, n, e2', m)),
                           (fun a n e2' ->
                             if not (isExnFieldMutable a n) then errorR(Error(FSComp.SR.tcFieldIsNotMutable(), m))
                             mkExnCaseFieldSet(e1', a, n, e2', m)))
        let e2', tpenv = TcExpr cenv ty2 env tpenv e2
        mkf n e2', tpenv

    | SynExpr.LibraryOnlyILAssembly (s, tyargs, args, rtys, m) ->
        let s = (s :?> ILInstr[])
        let argTys = NewInferenceTypes args
        let tyargs', tpenv = TcTypes cenv NewTyparsOK CheckCxs ItemOccurence.UseInType env tpenv tyargs
        // No subsumption at uses of IL assembly code
        let flexes = argTys |> List.map (fun _ -> false)
        let args', tpenv = TcExprs cenv env m tpenv flexes argTys args
        let rtys', tpenv = TcTypes cenv NewTyparsOK CheckCxs ItemOccurence.UseInType env tpenv rtys
        let returnTy =
            match rtys' with
            | [] -> cenv.g.unit_ty
            | [ returnTy ] -> returnTy
            | _ -> error(InternalError("Only zero or one pushed items are permitted in IL assembly code", m))
        UnifyTypes cenv env m overallTy returnTy
        mkAsmExpr (Array.toList s, tyargs', args', rtys', m), tpenv

    | SynExpr.Quote (oper, raw, ast, isFromQueryExpression, m) ->
        CallExprHasTypeSink cenv.tcSink (m, env.NameEnv, overallTy, env.AccessRights)
        TcQuotationExpr cenv overallTy env tpenv (oper, raw, ast, isFromQueryExpression, m)

    | SynExpr.YieldOrReturn ((isTrueYield, _), _, m)
    | SynExpr.YieldOrReturnFrom ((isTrueYield, _), _, m) when isTrueYield ->
        error(Error(FSComp.SR.tcConstructRequiresListArrayOrSequence(), m))

    | SynExpr.YieldOrReturn ((_, isTrueReturn), _, m)
    | SynExpr.YieldOrReturnFrom ((_, isTrueReturn), _, m) when isTrueReturn ->
        error(Error(FSComp.SR.tcConstructRequiresComputationExpressions(), m))

    | SynExpr.YieldOrReturn (_, _, m)
    | SynExpr.YieldOrReturnFrom (_, _, m)
    | SynExpr.ImplicitZero m ->
        error(Error(FSComp.SR.tcConstructRequiresSequenceOrComputations(), m))

    | SynExpr.DoBang (_, m)
    | SynExpr.LetOrUseBang  (range=m) ->
        error(Error(FSComp.SR.tcConstructRequiresComputationExpression(), m))

    | SynExpr.MatchBang (_, _, _, m) ->
        error(Error(FSComp.SR.tcConstructRequiresComputationExpression(), m))

/// Check lambdas as a group, to catch duplicate names in patterns
and TcIteratedLambdas cenv isFirst (env: TcEnv) overallTy takenNames tpenv e =
    match e with
    | SynExpr.Lambda (isMember, isSubsequent, spats, bodyExpr, _, m) when isMember || isFirst || isSubsequent ->
        let domainTy, resultTy = UnifyFunctionType None cenv env.DisplayEnv m overallTy
        let vs, (tpenv, names, takenNames) = TcSimplePats cenv isMember CheckCxs domainTy env (tpenv, Map.empty, takenNames) spats
        let envinner, _, vspecMap = MakeAndPublishSimpleValsForMergedScope cenv env m names
        let byrefs = vspecMap |> Map.map (fun _ v -> isByrefTy cenv.g v.Type, v)
        let envinner = if isMember then envinner else ExitFamilyRegion envinner
        let vspecs = vs |> List.map (fun nm -> NameMap.find nm vspecMap)
        
        // Match up the arginfos with the generated arguments and apply any information extracted from the attributes
        let envinner =
            match envinner.eLambdaArgInfos with 
            | infos :: rest -> 
                 if infos.Length = vspecs.Length then 
                    (vspecs, infos) ||> List.iter2 (fun v argInfo -> 
                        let inlineIfLambda = HasFSharpAttribute cenv.g cenv.g.attrib_InlineIfLambdaAttribute argInfo.Attribs
                        if inlineIfLambda then 
                            v.SetInlineIfLambda())
                 { envinner with eLambdaArgInfos = rest }
            | [] -> envinner
                   
        let bodyExpr, tpenv = TcIteratedLambdas cenv false envinner resultTy takenNames tpenv bodyExpr
        // See bug 5758: Non-monotonicity in inference: need to ensure that parameters are never inferred to have byref type, instead it is always declared
        byrefs |> Map.iter (fun _ (orig, v) ->
            if not orig && isByrefTy cenv.g v.Type then errorR(Error(FSComp.SR.tcParameterInferredByref v.DisplayName, v.Range)))

        mkMultiLambda m vspecs (bodyExpr, resultTy), tpenv 

    | e -> 
        // Dive into the expression to check for syntax errors and suppress them if they show.
        conditionallySuppressErrorReporting (not isFirst && synExprContainsError e) (fun () ->
            //TcExprFlex cenv true true overallTy env tpenv e)
            TcExpr cenv overallTy env tpenv e)


// Check expr.[idx]
// This is a little over complicated for my liking. Basically we want to interpret e1.[idx] as e1.Item(idx).
// However it's not so simple as all that. First "Item" can have a different name according to an attribute in
// .NET metadata. This means we manually typecheck 'e1' and look to see if it has a nominal type. We then
// do the right thing in each case.
and TcIndexerThen cenv env overallTy mWholeExpr mDot tpenv wholeExpr e1 indexArgs delayed =
    let ad = env.AccessRights
    let e1', e1ty, tpenv = TcExprOfUnknownType cenv env tpenv e1

    // Find the first type in the effective hierarchy that either has a DefaultMember attribute OR
    // has a member called 'Item'
    let isIndex = indexArgs |> List.forall( fun x -> match x with SynIndexerArg.One _ -> true | _ -> false)
    let propName =
        if isIndex then
            FoldPrimaryHierarchyOfType (fun ty acc ->
                match acc with
                | None ->
                    match tryTcrefOfAppTy cenv.g ty with
                    | ValueSome tcref ->
                        TryFindTyconRefStringAttribute cenv.g mWholeExpr cenv.g.attrib_DefaultMemberAttribute tcref
                    | _ ->
                        let item = Some "Item"
                        match AllPropInfosOfTypeInScope ResultCollectionSettings.AtMostOneResult cenv.infoReader env.NameEnv item ad IgnoreOverrides mWholeExpr ty with
                        | [] -> None
                        | _ -> item
                 | _ -> acc)
              cenv.g
              cenv.amap
              mWholeExpr
              AllowMultiIntfInstantiations.Yes
              e1ty
              None
        else Some "GetSlice"

    let isNominal = isAppTy cenv.g e1ty

    let isArray = isArrayTy cenv.g e1ty
    let isString = typeEquiv cenv.g cenv.g.string_ty e1ty

    let idxRange = indexArgs |> List.map (fun e -> e.Range) |> List.reduce unionRanges

    // xs.GetReverseIndex rank offset - 1
    let rewriteReverseExpr (rank: int) (offset: SynExpr) (range: range) =
        let rankExpr = SynExpr.Const(SynConst.Int32(rank), range)
        let sliceArgs = SynExpr.Paren(SynExpr.Tuple(false, [rankExpr; offset], [], range), range, Some range, range)
        let xsId = e1

        mkSynApp1
            (mkSynDot range range xsId (mkSynId range "GetReverseIndex"))
            sliceArgs
            range

    let rewriteReverseOption (app: SynExpr) (rank: int) (range: range) =
       match app with
       | SynExpr.App(atomicFlag, isInfix, funcExpr, e1, outerRange) -> SynExpr.App(atomicFlag, isInfix, funcExpr, rewriteReverseExpr rank e1 range, outerRange)
       | _ -> app

    let expandedIndexArgs =
        indexArgs
        |> List.mapi ( fun pos indexerArg ->
            match indexerArg with
            | SynIndexerArg.One(expr, fromEnd, range) ->
                [ if fromEnd then rewriteReverseExpr pos expr range else expr ]
            | SynIndexerArg.Two
                (
                    a1,
                    fromEnd1,
                    a2,
                    fromEnd2,
                    range1,
                    range2) ->
                [
                   if fromEnd1 then rewriteReverseOption a1 pos range1 else a1 ;
                   if fromEnd2 then rewriteReverseOption a2 pos range2 else a2
                ]
        )
        |> List.collect (id)

    let MakeIndexParam setSliceArrayOption =
       match indexArgs with
       | [] -> failwith "unexpected empty index list"
       | [SynIndexerArg.One _] -> SynExpr.Paren (expandedIndexArgs.Head, range0, None, idxRange)
       | _ -> SynExpr.Paren (SynExpr.Tuple (false, expandedIndexArgs @ Option.toList setSliceArrayOption, [], idxRange), range0, None, idxRange)

    let attemptArrayString =
        let indexOpPath = ["Microsoft";"FSharp";"Core";"LanguagePrimitives";"IntrinsicFunctions"]
        let sliceOpPath = ["Microsoft";"FSharp";"Core";"Operators";"OperatorIntrinsics"]

        let info =
            if isArray then
                let fixedIndex3d4dEnabled = cenv.g.langVersion.SupportsFeature LanguageFeature.FixedIndexSlice3d4d
                match wholeExpr with
                    | SynExpr.DotIndexedGet (_, [SynIndexerArg.One _; SynIndexerArg.One _], _, _)                                                   -> Some (indexOpPath, "GetArray2D", expandedIndexArgs)
                    | SynExpr.DotIndexedGet (_, [SynIndexerArg.One _; SynIndexerArg.One _; SynIndexerArg.One _;], _, _)                             -> Some (indexOpPath, "GetArray3D", expandedIndexArgs)
                    | SynExpr.DotIndexedGet (_, [SynIndexerArg.One _; SynIndexerArg.One _; SynIndexerArg.One _; SynIndexerArg.One _], _, _)         -> Some (indexOpPath, "GetArray4D", expandedIndexArgs)
                    | SynExpr.DotIndexedGet (_, [SynIndexerArg.One _], _, _)                                                                        -> Some (indexOpPath, "GetArray", expandedIndexArgs)
                    | SynExpr.DotIndexedSet (_, [SynIndexerArg.One _; SynIndexerArg.One _], e3, _, _, _)                                            -> Some (indexOpPath, "SetArray2D", (expandedIndexArgs @ [e3]))
                    | SynExpr.DotIndexedSet (_, [SynIndexerArg.One _; SynIndexerArg.One _; SynIndexerArg.One _;], e3, _, _, _)                      -> Some (indexOpPath, "SetArray3D", (expandedIndexArgs @ [e3]))
                    | SynExpr.DotIndexedSet (_, [SynIndexerArg.One _; SynIndexerArg.One _; SynIndexerArg.One _; SynIndexerArg.One _], e3, _, _, _)  -> Some (indexOpPath, "SetArray4D", (expandedIndexArgs @ [e3]))
                    | SynExpr.DotIndexedSet (_, [SynIndexerArg.One _], e3, _, _, _)                                                                 -> Some (indexOpPath, "SetArray", (expandedIndexArgs @ [e3]))
                    | SynExpr.DotIndexedGet (_, [SynIndexerArg.Two _], _, _)                                                                        -> Some (sliceOpPath, "GetArraySlice", expandedIndexArgs)
                    | SynExpr.DotIndexedGet (_, [SynIndexerArg.One _;SynIndexerArg.Two _], _, _)                                                    -> Some (sliceOpPath, "GetArraySlice2DFixed1", expandedIndexArgs)
                    | SynExpr.DotIndexedGet (_, [SynIndexerArg.Two _;SynIndexerArg.One _], _, _)                                                    -> Some (sliceOpPath, "GetArraySlice2DFixed2", expandedIndexArgs)
                    | SynExpr.DotIndexedGet (_, [SynIndexerArg.Two _;SynIndexerArg.Two _], _, _)                                                    -> Some (sliceOpPath, "GetArraySlice2D", expandedIndexArgs)
                    | SynExpr.DotIndexedGet (_, [SynIndexerArg.Two _;SynIndexerArg.Two _;SynIndexerArg.Two _], _, _)                                -> Some (sliceOpPath, "GetArraySlice3D", expandedIndexArgs)
                    | SynExpr.DotIndexedGet (_, [SynIndexerArg.Two _;SynIndexerArg.Two _;SynIndexerArg.Two _;SynIndexerArg.Two _], _, _)            -> Some (sliceOpPath, "GetArraySlice4D", expandedIndexArgs)
                    | SynExpr.DotIndexedSet (_, [SynIndexerArg.Two _], e3, _, _, _)                                                                 -> Some (sliceOpPath, "SetArraySlice", (expandedIndexArgs @ [e3]))
                    | SynExpr.DotIndexedSet (_, [SynIndexerArg.Two _;SynIndexerArg.Two _], e3, _, _, _)                                             -> Some (sliceOpPath, "SetArraySlice2D", (expandedIndexArgs @ [e3]))
                    | SynExpr.DotIndexedSet (_, [SynIndexerArg.One _;SynIndexerArg.Two _], e3, _, _, _)                                             -> Some (sliceOpPath, "SetArraySlice2DFixed1", (expandedIndexArgs @ [e3]))
                    | SynExpr.DotIndexedSet (_, [SynIndexerArg.Two _;SynIndexerArg.One _], e3, _, _, _)                                             -> Some (sliceOpPath, "SetArraySlice2DFixed2", (expandedIndexArgs @ [e3]))
                    | SynExpr.DotIndexedSet (_, [SynIndexerArg.Two _;SynIndexerArg.Two _;SynIndexerArg.Two _], e3, _, _, _)                         -> Some (sliceOpPath, "SetArraySlice3D", (expandedIndexArgs @ [e3]))
                    | SynExpr.DotIndexedSet (_, [SynIndexerArg.Two _;SynIndexerArg.Two _;SynIndexerArg.Two _;SynIndexerArg.Two _], e3, _, _, _)     -> Some (sliceOpPath, "SetArraySlice4D", (expandedIndexArgs @ [e3]))
                    | _ when fixedIndex3d4dEnabled ->
                        match wholeExpr with
                        | SynExpr.DotIndexedGet (_, [SynIndexerArg.One _;SynIndexerArg.Two _;SynIndexerArg.Two _], _, _)                            -> Some (sliceOpPath, "GetArraySlice3DFixedSingle1", expandedIndexArgs)
                        | SynExpr.DotIndexedGet (_, [SynIndexerArg.Two _;SynIndexerArg.One _;SynIndexerArg.Two _], _, _)                            -> Some (sliceOpPath, "GetArraySlice3DFixedSingle2", expandedIndexArgs)
                        | SynExpr.DotIndexedGet (_, [SynIndexerArg.Two _;SynIndexerArg.Two _;SynIndexerArg.One _], _, _)                            -> Some (sliceOpPath, "GetArraySlice3DFixedSingle3", expandedIndexArgs)
                        | SynExpr.DotIndexedGet (_, [SynIndexerArg.One _;SynIndexerArg.One _;SynIndexerArg.Two _], _, _)                            -> Some (sliceOpPath, "GetArraySlice3DFixedDouble1", expandedIndexArgs)
                        | SynExpr.DotIndexedGet (_, [SynIndexerArg.One _;SynIndexerArg.Two _;SynIndexerArg.One _], _, _)                            -> Some (sliceOpPath, "GetArraySlice3DFixedDouble2", expandedIndexArgs)
                        | SynExpr.DotIndexedGet (_, [SynIndexerArg.Two _;SynIndexerArg.One _;SynIndexerArg.One _], _, _)                            -> Some (sliceOpPath, "GetArraySlice3DFixedDouble3", expandedIndexArgs)
                        | SynExpr.DotIndexedGet (_, [SynIndexerArg.One _;SynIndexerArg.Two _;SynIndexerArg.Two _;SynIndexerArg.Two _], _, _)        -> Some (sliceOpPath, "GetArraySlice4DFixedSingle1", expandedIndexArgs)
                        | SynExpr.DotIndexedGet (_, [SynIndexerArg.Two _;SynIndexerArg.One _;SynIndexerArg.Two _;SynIndexerArg.Two _], _, _)        -> Some (sliceOpPath, "GetArraySlice4DFixedSingle2", expandedIndexArgs)
                        | SynExpr.DotIndexedGet (_, [SynIndexerArg.Two _;SynIndexerArg.Two _;SynIndexerArg.One _;SynIndexerArg.Two _], _, _)        -> Some (sliceOpPath, "GetArraySlice4DFixedSingle3", expandedIndexArgs)
                        | SynExpr.DotIndexedGet (_, [SynIndexerArg.Two _;SynIndexerArg.Two _;SynIndexerArg.Two _;SynIndexerArg.One _], _, _)        -> Some (sliceOpPath, "GetArraySlice4DFixedSingle4", expandedIndexArgs)
                        | SynExpr.DotIndexedGet (_, [SynIndexerArg.One _;SynIndexerArg.One _;SynIndexerArg.Two _;SynIndexerArg.Two _], _, _)        -> Some (sliceOpPath, "GetArraySlice4DFixedDouble1", expandedIndexArgs)
                        | SynExpr.DotIndexedGet (_, [SynIndexerArg.One _;SynIndexerArg.Two _;SynIndexerArg.One _;SynIndexerArg.Two _], _, _)        -> Some (sliceOpPath, "GetArraySlice4DFixedDouble2", expandedIndexArgs)
                        | SynExpr.DotIndexedGet (_, [SynIndexerArg.One _;SynIndexerArg.Two _;SynIndexerArg.Two _;SynIndexerArg.One _], _, _)        -> Some (sliceOpPath, "GetArraySlice4DFixedDouble3", expandedIndexArgs)
                        | SynExpr.DotIndexedGet (_, [SynIndexerArg.Two _;SynIndexerArg.One _;SynIndexerArg.One _;SynIndexerArg.Two _], _, _)        -> Some (sliceOpPath, "GetArraySlice4DFixedDouble4", expandedIndexArgs)
                        | SynExpr.DotIndexedGet (_, [SynIndexerArg.Two _;SynIndexerArg.One _;SynIndexerArg.Two _;SynIndexerArg.One _], _, _)        -> Some (sliceOpPath, "GetArraySlice4DFixedDouble5", expandedIndexArgs)
                        | SynExpr.DotIndexedGet (_, [SynIndexerArg.Two _;SynIndexerArg.Two _;SynIndexerArg.One _;SynIndexerArg.One _], _, _)        -> Some (sliceOpPath, "GetArraySlice4DFixedDouble6", expandedIndexArgs)
                        | SynExpr.DotIndexedGet (_, [SynIndexerArg.Two _;SynIndexerArg.One _;SynIndexerArg.One _;SynIndexerArg.One _], _, _)        -> Some (sliceOpPath, "GetArraySlice4DFixedTriple1", expandedIndexArgs)
                        | SynExpr.DotIndexedGet (_, [SynIndexerArg.One _;SynIndexerArg.Two _;SynIndexerArg.One _;SynIndexerArg.One _], _, _)        -> Some (sliceOpPath, "GetArraySlice4DFixedTriple2", expandedIndexArgs)
                        | SynExpr.DotIndexedGet (_, [SynIndexerArg.One _;SynIndexerArg.One _;SynIndexerArg.Two _;SynIndexerArg.One _], _, _)        -> Some (sliceOpPath, "GetArraySlice4DFixedTriple3", expandedIndexArgs)
                        | SynExpr.DotIndexedGet (_, [SynIndexerArg.One _;SynIndexerArg.One _;SynIndexerArg.One _;SynIndexerArg.Two _], _, _)        -> Some (sliceOpPath, "GetArraySlice4DFixedTriple4", expandedIndexArgs)
                        | SynExpr.DotIndexedSet (_, [SynIndexerArg.One _;SynIndexerArg.Two _;SynIndexerArg.Two _], e3, _, _, _)                     -> Some (sliceOpPath, "SetArraySlice3DFixedSingle1", (expandedIndexArgs @ [e3]))
                        | SynExpr.DotIndexedSet (_, [SynIndexerArg.Two _;SynIndexerArg.One _;SynIndexerArg.Two _], e3, _, _, _)                     -> Some (sliceOpPath, "SetArraySlice3DFixedSingle2", (expandedIndexArgs @ [e3]))
                        | SynExpr.DotIndexedSet (_, [SynIndexerArg.Two _;SynIndexerArg.Two _;SynIndexerArg.One _], e3, _, _, _)                     -> Some (sliceOpPath, "SetArraySlice3DFixedSingle3", (expandedIndexArgs @ [e3]))
                        | SynExpr.DotIndexedSet (_, [SynIndexerArg.One _;SynIndexerArg.One _;SynIndexerArg.Two _], e3, _, _, _)                     -> Some (sliceOpPath, "SetArraySlice3DFixedDouble1", (expandedIndexArgs @ [e3]))
                        | SynExpr.DotIndexedSet (_, [SynIndexerArg.One _;SynIndexerArg.Two _;SynIndexerArg.One _], e3, _, _, _)                     -> Some (sliceOpPath, "SetArraySlice3DFixedDouble2", (expandedIndexArgs @ [e3]))
                        | SynExpr.DotIndexedSet (_, [SynIndexerArg.Two _;SynIndexerArg.One _;SynIndexerArg.One _], e3, _, _, _)                     -> Some (sliceOpPath, "SetArraySlice3DFixedDouble3", (expandedIndexArgs @ [e3]))
                        | SynExpr.DotIndexedSet (_, [SynIndexerArg.One _;SynIndexerArg.Two _;SynIndexerArg.Two _;SynIndexerArg.Two _], e3, _, _, _) -> Some (sliceOpPath, "SetArraySlice4DFixedSingle1", expandedIndexArgs @ [e3])
                        | SynExpr.DotIndexedSet (_, [SynIndexerArg.Two _;SynIndexerArg.One _;SynIndexerArg.Two _;SynIndexerArg.Two _], e3, _, _, _) -> Some (sliceOpPath, "SetArraySlice4DFixedSingle2", expandedIndexArgs @ [e3])
                        | SynExpr.DotIndexedSet (_, [SynIndexerArg.Two _;SynIndexerArg.Two _;SynIndexerArg.One _;SynIndexerArg.Two _], e3, _, _, _) -> Some (sliceOpPath, "SetArraySlice4DFixedSingle3", expandedIndexArgs @ [e3])
                        | SynExpr.DotIndexedSet (_, [SynIndexerArg.Two _;SynIndexerArg.Two _;SynIndexerArg.Two _;SynIndexerArg.One _], e3, _, _, _) -> Some (sliceOpPath, "SetArraySlice4DFixedSingle4", expandedIndexArgs @ [e3])
                        | SynExpr.DotIndexedSet (_, [SynIndexerArg.One _;SynIndexerArg.One _;SynIndexerArg.Two _;SynIndexerArg.Two _], e3, _, _, _) -> Some (sliceOpPath, "SetArraySlice4DFixedDouble1", expandedIndexArgs @ [e3])
                        | SynExpr.DotIndexedSet (_, [SynIndexerArg.One _;SynIndexerArg.Two _;SynIndexerArg.One _;SynIndexerArg.Two _], e3, _, _, _) -> Some (sliceOpPath, "SetArraySlice4DFixedDouble2", expandedIndexArgs @ [e3])
                        | SynExpr.DotIndexedSet (_, [SynIndexerArg.One _;SynIndexerArg.Two _;SynIndexerArg.Two _;SynIndexerArg.One _], e3, _, _, _) -> Some (sliceOpPath, "SetArraySlice4DFixedDouble3", expandedIndexArgs @ [e3])
                        | SynExpr.DotIndexedSet (_, [SynIndexerArg.Two _;SynIndexerArg.One _;SynIndexerArg.One _;SynIndexerArg.Two _], e3, _, _, _) -> Some (sliceOpPath, "SetArraySlice4DFixedDouble4", expandedIndexArgs @ [e3])
                        | SynExpr.DotIndexedSet (_, [SynIndexerArg.Two _;SynIndexerArg.One _;SynIndexerArg.Two _;SynIndexerArg.One _], e3, _, _, _) -> Some (sliceOpPath, "SetArraySlice4DFixedDouble5", expandedIndexArgs @ [e3])
                        | SynExpr.DotIndexedSet (_, [SynIndexerArg.Two _;SynIndexerArg.Two _;SynIndexerArg.One _;SynIndexerArg.One _], e3, _, _, _) -> Some (sliceOpPath, "SetArraySlice4DFixedDouble6", expandedIndexArgs @ [e3])
                        | SynExpr.DotIndexedSet (_, [SynIndexerArg.Two _;SynIndexerArg.One _;SynIndexerArg.One _;SynIndexerArg.One _], e3, _, _, _) -> Some (sliceOpPath, "SetArraySlice4DFixedTriple1", expandedIndexArgs @ [e3])
                        | SynExpr.DotIndexedSet (_, [SynIndexerArg.One _;SynIndexerArg.Two _;SynIndexerArg.One _;SynIndexerArg.One _], e3, _, _, _) -> Some (sliceOpPath, "SetArraySlice4DFixedTriple2", expandedIndexArgs @ [e3])
                        | SynExpr.DotIndexedSet (_, [SynIndexerArg.One _;SynIndexerArg.One _;SynIndexerArg.Two _;SynIndexerArg.One _], e3, _, _, _) -> Some (sliceOpPath, "SetArraySlice4DFixedTriple3", expandedIndexArgs @ [e3])
                        | SynExpr.DotIndexedSet (_, [SynIndexerArg.One _;SynIndexerArg.One _;SynIndexerArg.One _;SynIndexerArg.Two _], e3, _, _, _) -> Some (sliceOpPath, "SetArraySlice4DFixedTriple4", expandedIndexArgs @ [e3])
                        | _                                                                                                                         -> None
                    | _ -> None

            elif isString then
                match wholeExpr with
                | SynExpr.DotIndexedGet (_, [SynIndexerArg.Two _], _, _) -> Some (sliceOpPath, "GetStringSlice", expandedIndexArgs)
                | SynExpr.DotIndexedGet (_, [SynIndexerArg.One _], _, _) -> Some (indexOpPath, "GetString", expandedIndexArgs)
                | _ -> None

            else None

        match info with
            | None -> None
            | Some (path, functionName, indexArgs) ->
                let operPath = mkSynLidGet mDot path (CompileOpName functionName)
                let f, fty, tpenv = TcExprOfUnknownType cenv env tpenv operPath
                let domainTy, resultTy = UnifyFunctionType (Some mWholeExpr) cenv env.DisplayEnv mWholeExpr fty
                UnifyTypes cenv env mWholeExpr domainTy e1ty
                let f', resultTy = buildApp cenv (MakeApplicableExprNoFlex cenv f) resultTy e1' mWholeExpr
                let delayed = List.foldBack (fun idx acc -> DelayedApp(ExprAtomicFlag.Atomic, idx, mWholeExpr) :: acc) indexArgs delayed // atomic, otherwise no ar.[1] <- xyz
                Some (PropagateThenTcDelayed cenv overallTy env tpenv mWholeExpr f' resultTy ExprAtomicFlag.Atomic delayed )

    match attemptArrayString with
    | Some res -> res
    | None ->
    if isNominal || Option.isSome propName then
        let nm =
            match propName with
            | None -> "Item"
            | Some nm -> nm
        let delayed =
            match wholeExpr with
            // e1.[e2]
            | SynExpr.DotIndexedGet _ ->
                DelayedDotLookup([ident(nm, mWholeExpr)], mWholeExpr) :: DelayedApp(ExprAtomicFlag.Atomic, MakeIndexParam None, mWholeExpr) :: delayed
            // e1.[e2] <- e3
            | SynExpr.DotIndexedSet (_, _, e3, mOfLeftOfSet, _, _) ->
                match isIndex with
                | true -> DelayedDotLookup([ident(nm, mOfLeftOfSet)], mOfLeftOfSet) :: DelayedApp(ExprAtomicFlag.Atomic, MakeIndexParam None, mOfLeftOfSet) :: MakeDelayedSet(e3, mWholeExpr) :: delayed
                | false -> DelayedDotLookup([ident("SetSlice", mOfLeftOfSet)], mOfLeftOfSet) :: DelayedApp(ExprAtomicFlag.Atomic, MakeIndexParam (Some e3), mWholeExpr) :: delayed

            | _ -> error(InternalError("unreachable", mWholeExpr))
        PropagateThenTcDelayed cenv overallTy env tpenv mDot (MakeApplicableExprNoFlex cenv e1') e1ty ExprAtomicFlag.Atomic delayed

    else
        // deprecated constrained lookup
        error(Error(FSComp.SR.tcObjectOfIndeterminateTypeUsedRequireTypeConstraint(), mWholeExpr))


/// Check a 'new Type(args)' expression, also an 'inheritedTys declaration in an implicit or explicit class
/// For 'new Type(args)', mWholeExprOrObjTy is the whole expression
/// For 'inherit Type(args)', mWholeExprOrObjTy is the whole expression
/// For an implicit inherit from System.Object or a default constructor, mWholeExprOrObjTy is the type name of the type being defined
and TcNewExpr cenv env tpenv objTy mObjTyOpt superInit arg mWholeExprOrObjTy =
    let ad = env.AccessRights
    // Handle the case 'new 'a()'
    if (isTyparTy cenv.g objTy) then
        if superInit then error(Error(FSComp.SR.tcCannotInheritFromVariableType(), mWholeExprOrObjTy))
        AddCxTypeMustSupportDefaultCtor env.DisplayEnv cenv.css mWholeExprOrObjTy NoTrace objTy

        match arg with
        | SynExpr.Const (SynConst.Unit, _) -> ()
        | _ -> errorR(Error(FSComp.SR.tcObjectConstructorsOnTypeParametersCannotTakeArguments(), mWholeExprOrObjTy))

        mkCallCreateInstance cenv.g mWholeExprOrObjTy objTy, tpenv
    else
        if not (isAppTy cenv.g objTy) && not (isAnyTupleTy cenv.g objTy) then error(Error(FSComp.SR.tcNamedTypeRequired(if superInit then "inherit" else "new"), mWholeExprOrObjTy))
        let item = ForceRaise (ResolveObjectConstructor cenv.nameResolver env.DisplayEnv mWholeExprOrObjTy ad objTy)

        TcCtorCall false cenv env tpenv objTy objTy mObjTyOpt item superInit [arg] mWholeExprOrObjTy [] None

/// Check an 'inheritedTys declaration in an implicit or explicit class
and TcCtorCall isNaked cenv env tpenv overallTy objTy mObjTyOpt item superInit args mWholeCall delayed afterTcOverloadResolutionOpt =
    let ad = env.AccessRights
    let isSuperInit = (if superInit then CtorValUsedAsSuperInit else NormalValUse)
    let mItem = match mObjTyOpt with Some m -> m | None -> mWholeCall

    if isInterfaceTy cenv.g objTy then
        error(Error((if superInit then FSComp.SR.tcInheritCannotBeUsedOnInterfaceType() else FSComp.SR.tcNewCannotBeUsedOnInterfaceType()), mWholeCall))

    match item, args with
    | Item.CtorGroup(methodName, minfos), _ ->
        let meths = List.map (fun minfo -> minfo, None) minfos
        if isNaked && TypeFeasiblySubsumesType 0 cenv.g cenv.amap mWholeCall cenv.g.system_IDisposable_ty NoCoerce objTy then
            warning(Error(FSComp.SR.tcIDisposableTypeShouldUseNew(), mWholeCall))

        // Check the type is not abstract
        // skip this check if this ctor call is either 'inherit(...)' or call is located within constructor shape
        if not (superInit || AreWithinCtorShape env)
            then CheckSuperInit cenv objTy mWholeCall

        let afterResolution =
            match mObjTyOpt, afterTcOverloadResolutionOpt with
            | _, Some action -> action
            | Some mObjTy, None -> ForNewConstructors cenv.tcSink env mObjTy methodName minfos
            | None, _ -> AfterResolution.DoNothing

        TcMethodApplicationThen cenv env overallTy (Some objTy) tpenv None [] mWholeCall mItem methodName ad PossiblyMutates false meths afterResolution isSuperInit args ExprAtomicFlag.NonAtomic delayed

    | Item.DelegateCtor ty, [arg] ->
        // Re-record the name resolution since we now know it's a constructor call
        match mObjTyOpt with
        | Some mObjTy -> CallNameResolutionSink cenv.tcSink (mObjTy, env.NameEnv, item, emptyTyparInst, ItemOccurence.Use, env.AccessRights)
        | None -> ()
        TcNewDelegateThen cenv objTy env tpenv mItem mWholeCall ty arg ExprAtomicFlag.NonAtomic delayed

    | _ ->
        error(Error(FSComp.SR.tcSyntaxCanOnlyBeUsedToCreateObjectTypes(if superInit then "inherit" else "new"), mWholeCall))


// Check a record construction expression
and TcRecordConstruction cenv overallTy env tpenv optOrigExprInfo objTy fldsList m =
    let tcref, tinst = destAppTy cenv.g objTy
    let tycon = tcref.Deref
    UnifyTypes cenv env m overallTy objTy

    // Types with implicit constructors can't use record or object syntax: all constructions must go through the implicit constructor
    if tycon.MembersOfFSharpTyconByName |> NameMultiMap.existsInRange (fun v -> v.IsIncrClassConstructor) then
        errorR(Error(FSComp.SR.tcConstructorRequiresCall(tycon.DisplayName), m))

    let fspecs = tycon.TrueInstanceFieldsAsList
    // Freshen types and work out their subtype flexibility
    let fldsList =
        [ for (fname, fexpr) in fldsList do
              let fspec =
                  try
                      fspecs |> List.find (fun fspec -> fspec.Name = fname)
                  with :? KeyNotFoundException ->
                      error (Error(FSComp.SR.tcUndefinedField(fname, NicePrint.minimalStringOfType env.DisplayEnv objTy), m))
              let fty = actualTyOfRecdFieldForTycon tycon tinst fspec
              let flex = not (isTyparTy cenv.g fty)
              yield (fname, fexpr, fty, flex) ]

    // Type check and generalize the supplied bindings
    let fldsList, tpenv =
        let env = { env with eContextInfo = ContextInfo.RecordFields }
        (tpenv, fldsList) ||> List.mapFold (fun tpenv (fname, fexpr, fty, flex) ->
              let fieldExpr, tpenv = TcExprFlex cenv flex false fty env tpenv fexpr
              (fname, fieldExpr), tpenv)

    // Add rebindings for unbound field when an "old value" is available
    // Effect order: mutable fields may get modified by other bindings...
    let oldFldsList =
        match optOrigExprInfo with
        | None -> []
        | Some (_, _, oldvaddre) ->
            let fieldNameUnbound nom = List.forall (fun (name, _) -> name <> nom) fldsList
            let flds =
                fspecs |> List.choose (fun rfld ->
                    if fieldNameUnbound rfld.Name && not rfld.IsZeroInit
                    then Some(rfld.Name, mkRecdFieldGetViaExprAddr (oldvaddre, tcref.MakeNestedRecdFieldRef rfld, tinst, m))
                    else None)
            flds

    let fldsList = fldsList @ oldFldsList

    // From now on only interested in fspecs that truly need values.
    let fspecs = fspecs |> List.filter (fun f -> not f.IsZeroInit)

    // Check all fields are bound
    fspecs |> List.iter (fun fspec ->
      if not (fldsList |> List.exists (fun (fname, _) -> fname = fspec.Name)) then
        error(Error(FSComp.SR.tcFieldRequiresAssignment(fspec.rfield_id.idText, fullDisplayTextOfTyconRef tcref), m)))

    // Other checks (overlap with above check now clear)
    let ns1 = NameSet.ofList (List.map fst fldsList)
    let ns2 = NameSet.ofList (List.map (fun x -> x.rfield_id.idText) fspecs)

    if optOrigExprInfo.IsNone && not (Zset.subset ns2 ns1) then
        error (MissingFields(Zset.elements (Zset.diff ns2 ns1), m))

    if not (Zset.subset ns1 ns2) then
        error (Error(FSComp.SR.tcExtraneousFieldsGivenValues(), m))

    // Build record
    let rfrefs = List.map (fst >> mkRecdFieldRef tcref) fldsList

    // Check accessibility: this is also done in BuildFieldMap, but also need to check
    // for fields in { new R with a=1 and b=2 } constructions and { r with a=1 } copy-and-update expressions
    rfrefs |> List.iter (fun rfref ->
        CheckRecdFieldAccessible cenv.amap m env.eAccessRights rfref |> ignore
        CheckFSharpAttributes cenv.g rfref.PropertyAttribs m |> CommitOperationResult)

    let args = List.map snd fldsList

    let expr = mkRecordExpr cenv.g (GetRecdInfo env, tcref, tinst, rfrefs, args, m)

    let expr =
      match optOrigExprInfo with
      | None ->
          // '{ recd fields }'. //
          expr

      | Some (old, oldvaddr, _) ->
          // '{ recd with fields }'.
          // Assign the first object to a tmp and then construct
          let wrap, oldaddr, _readonly, _writeonly = mkExprAddrOfExpr cenv.g tycon.IsStructOrEnumTycon false NeverMutates old None m
          wrap (mkCompGenLet m oldvaddr oldaddr expr)

    expr, tpenv

//-------------------------------------------------------------------------
// TcObjectExpr
//-------------------------------------------------------------------------

and GetNameAndArityOfObjExprBinding _cenv _env b =
    let (NormalizedBinding (_, _, _, _, _, _, _, valSynData, pat, rhsExpr, mBinding, _)) = b
    let (SynValData(memberFlagsOpt, valSynInfo, _)) = valSynData
    match pat, memberFlagsOpt with

    // This is the normal case for F# 'with member x.M(...) = ...'
    | SynPat.InstanceMember(_thisId, memberId, _, None, _), Some memberFlags ->
         let logicalMethId = ident (ComputeLogicalName memberId memberFlags, memberId.idRange)
         logicalMethId.idText, valSynInfo

    | _ ->
        // This is for the deprecated form 'with M(...) = ...'
        let rec lookPat pat =
            match pat with
            | SynPat.Typed(pat, _, _) -> lookPat pat
            | SynPat.FromParseError(pat, _) -> lookPat pat
            | SynPat.Named (id, _, None, _) ->
                let (NormalizedBindingRhs(pushedPats, _, _)) = rhsExpr
                let infosForExplicitArgs = pushedPats |> List.map SynInfo.InferSynArgInfoFromSimplePats
                let infosForExplicitArgs = SynInfo.AdjustMemberArgs SynMemberKind.Member infosForExplicitArgs
                let infosForExplicitArgs = SynInfo.AdjustArgsForUnitElimination infosForExplicitArgs
                let argInfos = [SynInfo.selfMetadata] @ infosForExplicitArgs
                let retInfo = SynInfo.unnamedRetVal //SynInfo.InferSynReturnData pushedRetInfoOpt
                let valSynData = SynValInfo(argInfos, retInfo)
                (id.idText, valSynData)
            | _ -> error(Error(FSComp.SR.tcObjectExpressionsCanOnlyOverrideAbstractOrVirtual(), mBinding))

        lookPat pat


and FreshenObjExprAbstractSlot cenv (env: TcEnv) (implty: TType) virtNameAndArityPairs (bind, bindAttribs, bindName, absSlots:(_ * MethInfo) list) =
    let (NormalizedBinding (_, _, _, _, _, _, synTyparDecls, _, _, _, mBinding, _)) = bind
    match absSlots with
    | [] when not (CompileAsEvent cenv.g bindAttribs) ->
        let absSlotsByName = List.filter (fst >> fst >> (=) bindName) virtNameAndArityPairs
        let getSignature absSlot = (NicePrint.stringOfMethInfo cenv.infoReader mBinding env.DisplayEnv absSlot).Replace("abstract ", "")
        let getDetails (absSlot: MethInfo) =
            if absSlot.GetParamTypes(cenv.amap, mBinding, []) |> List.existsSquared (isAnyTupleTy cenv.g) then
                FSComp.SR.tupleRequiredInAbstractMethod()
            else ""

        // Compute the argument counts of the member arguments
        let _, synValInfo = GetNameAndArityOfObjExprBinding cenv env bind
        let arity =
            match SynInfo.AritiesOfArgs synValInfo with
            | _ :: x :: _ -> x
            | _ -> 0

        match absSlotsByName with
        | [] ->
            let tcref = tcrefOfAppTy cenv.g implty
            let containsNonAbstractMemberWithSameName =
                tcref.MembersOfFSharpTyconByName
                |> Seq.exists (fun kv -> kv.Value |> List.exists (fun valRef -> valRef.DisplayName = bindName))

            let suggestVirtualMembers (addToBuffer: string -> unit) =
                for ((x,_),_) in virtNameAndArityPairs do
                    addToBuffer x

            if containsNonAbstractMemberWithSameName then
                errorR(ErrorWithSuggestions(FSComp.SR.tcMemberFoundIsNotAbstractOrVirtual(tcref.DisplayName, bindName), mBinding, bindName, suggestVirtualMembers))
            else
                errorR(ErrorWithSuggestions(FSComp.SR.tcNoAbstractOrVirtualMemberFound bindName, mBinding, bindName, suggestVirtualMembers))
        | [(_, absSlot: MethInfo)] ->
            errorR(Error(FSComp.SR.tcArgumentArityMismatch(bindName, List.sum absSlot.NumArgs, arity, getSignature absSlot, getDetails absSlot), mBinding))
        | (_, absSlot: MethInfo) :: _ ->
            errorR(Error(FSComp.SR.tcArgumentArityMismatchOneOverload(bindName, List.sum absSlot.NumArgs, arity, getSignature absSlot, getDetails absSlot), mBinding))

        None

    | [(_, absSlot)] ->

        let typarsFromAbsSlotAreRigid, typarsFromAbsSlot, argTysFromAbsSlot, retTyFromAbsSlot
           = FreshenAbstractSlot cenv.g cenv.amap mBinding synTyparDecls absSlot

        // Work out the required type of the member
        let bindingTy = implty --> (mkMethodTy cenv.g argTysFromAbsSlot retTyFromAbsSlot)

        Some(typarsFromAbsSlotAreRigid, typarsFromAbsSlot, bindingTy)

    | _ ->
        None


and TcObjectExprBinding cenv (env: TcEnv) implty tpenv (absSlotInfo, bind) =
    // 4a1. normalize the binding (note: needlessly repeating what we've done above)
    let (NormalizedBinding(vis, bkind, isInline, isMutable, attrs, doc, synTyparDecls, valSynData, p, bindingRhs, mBinding, spBind)) = bind
    let (SynValData(memberFlagsOpt, _, _)) = valSynData
    // 4a2. adjust the binding, especially in the "member" case, a subset of the logic of AnalyzeAndMakeAndPublishRecursiveValue
    let bindingRhs, logicalMethId, memberFlags =
        let rec lookPat p =
            match p, memberFlagsOpt with
            | SynPat.FromParseError(pat, _), _ -> lookPat pat
            | SynPat.Named (id, _, _, _), None ->
                let bindingRhs = PushOnePatternToRhs cenv true (mkSynThisPatVar (ident (CompilerGeneratedName "this", id.idRange))) bindingRhs
                let logicalMethId = id
                let memberFlags = OverrideMemberFlags SynMemberKind.Member
                bindingRhs, logicalMethId, memberFlags

            | SynPat.InstanceMember(thisId, memberId, _, _, _), Some memberFlags ->
                CheckMemberFlags None NewSlotsOK OverridesOK memberFlags mBinding
                let bindingRhs = PushOnePatternToRhs cenv true (mkSynThisPatVar thisId) bindingRhs
                let logicalMethId = ident (ComputeLogicalName memberId memberFlags, memberId.idRange)
                bindingRhs, logicalMethId, memberFlags
            | _ ->
                error(InternalError("unexpected member binding", mBinding))
        lookPat p
    let bind = NormalizedBinding (vis, bkind, isInline, isMutable, attrs, doc, synTyparDecls, valSynData, mkSynPatVar vis logicalMethId, bindingRhs, mBinding, spBind)

    // 4b. typecheck the binding
    let bindingTy =
        match absSlotInfo with
        | Some(_, _, memberTyFromAbsSlot) ->
            memberTyFromAbsSlot
        | _ ->
            implty --> NewInferenceType ()

    let (CheckedBindingInfo(inlineFlag, bindingAttribs, _, _, ExplicitTyparInfo(_, declaredTypars, _), nameToPrelimValSchemeMap, rhsExpr, _, _, m, _, _, _, _), tpenv) =
        let explicitTyparInfo, tpenv = TcNonrecBindingTyparDecls cenv env tpenv bind
        TcNormalizedBinding ObjectExpressionOverrideBinding cenv env tpenv bindingTy None NoSafeInitInfo ([], explicitTyparInfo) bind

    // 4c. generalize the binding - only relevant when implementing a generic virtual method

    match NameMap.range nameToPrelimValSchemeMap with
    | [PrelimValScheme1(id, _, _, _, _, _, _, _, _, _, _)] ->
        let denv = env.DisplayEnv

        let declaredTypars =
            match absSlotInfo with
            | Some(typarsFromAbsSlotAreRigid, typarsFromAbsSlot, _) ->
                if typarsFromAbsSlotAreRigid then typarsFromAbsSlot else declaredTypars
            | _ ->
                declaredTypars
        // Canonicalize constraints prior to generalization
        ConstraintSolver.CanonicalizePartialInferenceProblem cenv.css denv m declaredTypars

        let freeInEnv = GeneralizationHelpers.ComputeUngeneralizableTypars env

        let generalizedTypars = GeneralizationHelpers.ComputeAndGeneralizeGenericTypars(cenv, denv, m, freeInEnv, false, CanGeneralizeConstrainedTypars, inlineFlag, Some rhsExpr, declaredTypars, [], bindingTy, false)
        let declaredTypars = ChooseCanonicalDeclaredTyparsAfterInference cenv.g env.DisplayEnv declaredTypars m

        let generalizedTypars = PlaceTyparsInDeclarationOrder declaredTypars generalizedTypars

        (id, memberFlags, (generalizedTypars +-> bindingTy), bindingAttribs, rhsExpr), tpenv
    | _ ->
        error(Error(FSComp.SR.tcSimpleMethodNameRequired(), m))

and ComputeObjectExprOverrides cenv (env: TcEnv) tpenv impls =

    // Compute the method sets each implemented type needs to implement
    let slotImplSets = DispatchSlotChecking.GetSlotImplSets cenv.infoReader env.DisplayEnv env.AccessRights true (impls |> List.map (fun (m, ty, _) -> ty, m))

    let allImpls =
        (impls, slotImplSets) ||> List.map2 (fun (m, ty, binds) implTySet ->
            let binds = binds |> List.map (BindingNormalization.NormalizeBinding ObjExprBinding cenv env)
            m, ty, binds, implTySet)

    let overridesAndVirts, tpenv =
        (tpenv, allImpls) ||> List.mapFold (fun tpenv (m, implty, binds, SlotImplSet(reqdSlots, dispatchSlotsKeyed, availPriorOverrides, _) ) ->

            // Generate extra bindings fo object expressions with bindings using the CLIEvent attribute
            let binds, bindsAttributes =
               [ for binding in binds do
                     let (NormalizedBinding(_, _, _, _, bindingSynAttribs, _, _, valSynData, _, _, _, _)) = binding
                     let (SynValData(memberFlagsOpt, _, _)) = valSynData
                     let attrTgt = DeclKind.AllowedAttribTargets memberFlagsOpt ObjectExpressionOverrideBinding
                     let bindingAttribs = TcAttributes cenv env attrTgt bindingSynAttribs
                     yield binding, bindingAttribs
                     for extraBinding in EventDeclarationNormalization.GenerateExtraBindings cenv (bindingAttribs, binding) do
                         yield extraBinding, [] ]
               |> List.unzip

            // 2. collect all name/arity of all overrides
            let dispatchSlots = reqdSlots |> List.map (fun reqdSlot -> reqdSlot.MethodInfo)
            let virtNameAndArityPairs = dispatchSlots |> List.map (fun virt ->
                let vkey = (virt.LogicalName, virt.NumArgs)
                //dprintfn "vkey = %A" vkey
                (vkey, virt))
            let bindNameAndSynInfoPairs = binds |> List.map (GetNameAndArityOfObjExprBinding cenv env)
            let bindNames = bindNameAndSynInfoPairs |> List.map fst
            let bindKeys =
                bindNameAndSynInfoPairs |> List.map (fun (name, valSynData) ->
                    // Compute the argument counts of the member arguments
                    let argCounts = (SynInfo.AritiesOfArgs valSynData).Tail
                    //dprintfn "name = %A, argCounts = %A" name argCounts
                    (name, argCounts))

            // 3. infer must-have types by name/arity
            let preAssignedVirtsPerBinding =
                bindKeys |> List.map (fun bkey -> List.filter (fst >> (=) bkey) virtNameAndArityPairs)

            let absSlotInfo =
               (List.zip4 binds bindsAttributes bindNames preAssignedVirtsPerBinding)
               |> List.map (FreshenObjExprAbstractSlot cenv env implty virtNameAndArityPairs)

            // 4. typecheck/typeinfer/generalizer overrides using this information
            let overrides, tpenv = (tpenv, List.zip absSlotInfo binds) ||> List.mapFold (TcObjectExprBinding cenv env implty)

            // Convert the syntactic info to actual info
            let overrides =
                (overrides, bindNameAndSynInfoPairs) ||> List.map2 (fun (id: Ident, memberFlags, ty, bindingAttribs, bindingBody) (_, valSynData) ->
                    let partialValInfo = TranslateTopValSynInfo id.idRange (TcAttributes cenv env) valSynData
                    let tps, _ = tryDestForallTy cenv.g ty
                    let valInfo = TranslatePartialArity tps partialValInfo
                    DispatchSlotChecking.GetObjectExprOverrideInfo cenv.g cenv.amap (implty, id, memberFlags, ty, valInfo, bindingAttribs, bindingBody))

            (m, implty, reqdSlots, dispatchSlotsKeyed, availPriorOverrides, overrides), tpenv)

    overridesAndVirts, tpenv

and CheckSuperType cenv ty m =
    if typeEquiv cenv.g ty cenv.g.system_Value_ty ||
       typeEquiv cenv.g ty cenv.g.system_Enum_ty ||
       typeEquiv cenv.g ty cenv.g.system_Array_ty ||
       typeEquiv cenv.g ty cenv.g.system_MulticastDelegate_ty ||
       typeEquiv cenv.g ty cenv.g.system_Delegate_ty then
         error(Error(FSComp.SR.tcPredefinedTypeCannotBeUsedAsSuperType(), m))
    if isErasedType cenv.g ty then
        errorR(Error(FSComp.SR.tcCannotInheritFromErasedType(), m))


and TcObjectExpr cenv overallTy env tpenv (synObjTy, argopt, binds, extraImpls, mNewExpr, mWholeExpr) =
    let mObjTy = synObjTy.Range

    let objTy, tpenv = TcType cenv NewTyparsOK CheckCxs ItemOccurence.UseInType env tpenv synObjTy
    match tryTcrefOfAppTy cenv.g objTy with
    | ValueNone -> error(Error(FSComp.SR.tcNewMustBeUsedWithNamedType(), mNewExpr))
    | ValueSome tcref ->
    let isRecordTy = tcref.IsRecordTycon
    if not isRecordTy && not (isInterfaceTy cenv.g objTy) && isSealedTy cenv.g objTy then errorR(Error(FSComp.SR.tcCannotCreateExtensionOfSealedType(), mNewExpr))

    CheckSuperType cenv objTy synObjTy.Range

    // Add the object type to the ungeneralizable items
    let env = {env with eUngeneralizableItems = addFreeItemOfTy objTy env.eUngeneralizableItems }

    // Object expression members can access protected members of the implemented type
    let env = EnterFamilyRegion tcref env
    let ad = env.AccessRights

    if // record construction ?
       isRecordTy ||
       // object construction?
       (isFSharpObjModelTy cenv.g objTy && not (isInterfaceTy cenv.g objTy) && argopt.IsNone) then

        if argopt.IsSome then error(Error(FSComp.SR.tcNoArgumentsForRecordValue(), mWholeExpr))
        if not (isNil extraImpls) then error(Error(FSComp.SR.tcNoInterfaceImplementationForConstructionExpression(), mNewExpr))
        if isFSharpObjModelTy cenv.g objTy && GetCtorShapeCounter env <> 1 then
            error(Error(FSComp.SR.tcObjectConstructionCanOnlyBeUsedInClassTypes(), mNewExpr))
        let fldsList =
            binds |> List.map (fun b ->
                match BindingNormalization.NormalizeBinding ObjExprBinding cenv env b with
                | NormalizedBinding (_, _, _, _, [], _, _, _, SynPat.Named(id, _, _, _), NormalizedBindingRhs(_, _, rhsExpr), _, _) -> id.idText, rhsExpr
                | _ -> error(Error(FSComp.SR.tcOnlySimpleBindingsCanBeUsedInConstructionExpressions(), b.RangeOfBindingWithoutRhs)))

        TcRecordConstruction cenv overallTy env tpenv None objTy fldsList mWholeExpr
    else
        let item = ForceRaise (ResolveObjectConstructor cenv.nameResolver env.DisplayEnv mObjTy ad objTy)

        if isFSharpObjModelTy cenv.g objTy && GetCtorShapeCounter env = 1 then
            error(Error(FSComp.SR.tcObjectsMustBeInitializedWithObjectExpression(), mNewExpr))

      // Work out the type of any interfaces to implement
        let extraImpls, tpenv =
          (tpenv, extraImpls) ||> List.mapFold (fun tpenv (SynInterfaceImpl(synIntfTy, overrides, m)) ->
              let intfTy, tpenv = TcType cenv NewTyparsOK CheckCxs ItemOccurence.UseInType env tpenv synIntfTy
              if not (isInterfaceTy cenv.g intfTy) then
                error(Error(FSComp.SR.tcExpectedInterfaceType(), m))
              if isErasedType cenv.g intfTy then
                  errorR(Error(FSComp.SR.tcCannotInheritFromErasedType(), m))
              (m, intfTy, overrides), tpenv)

        let realObjTy = if isObjTy cenv.g objTy && not (isNil extraImpls) then (p23 (List.head extraImpls)) else objTy
        UnifyTypes cenv env mWholeExpr overallTy realObjTy

        let ctorCall, baseIdOpt, tpenv =
            match item, argopt with
            | Item.CtorGroup(methodName, minfos), Some (arg, baseIdOpt) ->
                let meths = minfos |> List.map (fun minfo -> minfo, None)
                let afterResolution = ForNewConstructors cenv.tcSink env synObjTy.Range methodName minfos
                let ad = env.AccessRights

                let expr, tpenv = TcMethodApplicationThen cenv env objTy None tpenv None [] mWholeExpr mObjTy methodName ad PossiblyMutates false meths afterResolution CtorValUsedAsSuperInit [arg] ExprAtomicFlag.Atomic []
                // The 'base' value is always bound
                let baseIdOpt = (match baseIdOpt with None -> Some(ident("base", mObjTy)) | Some id -> Some id)
                expr, baseIdOpt, tpenv
            | Item.FakeInterfaceCtor intfTy, None ->
                UnifyTypes cenv env mWholeExpr objTy intfTy
                let expr = BuildObjCtorCall cenv.g mWholeExpr
                expr, None, tpenv
            | Item.FakeInterfaceCtor _, Some _ ->
                error(Error(FSComp.SR.tcConstructorForInterfacesDoNotTakeArguments(), mNewExpr))
            | Item.CtorGroup _, None ->
                error(Error(FSComp.SR.tcConstructorRequiresArguments(), mNewExpr))
            | _ -> error(Error(FSComp.SR.tcNewRequiresObjectConstructor(), mNewExpr))

        let baseValOpt = MakeAndPublishBaseVal cenv env baseIdOpt objTy
        let env = Option.foldBack (AddLocalVal cenv.g cenv.tcSink mNewExpr) baseValOpt env
        let impls = (mWholeExpr, objTy, binds) :: extraImpls

        // 1. collect all the relevant abstract slots for each type we have to implement

        let overridesAndVirts, tpenv = ComputeObjectExprOverrides cenv env tpenv impls

        overridesAndVirts |> List.iter (fun (m, implty, dispatchSlots, dispatchSlotsKeyed, availPriorOverrides, overrides) ->
            let overrideSpecs = overrides |> List.map fst

            DispatchSlotChecking.CheckOverridesAreAllUsedOnce (env.DisplayEnv, cenv.g, cenv.infoReader, true, implty, dispatchSlotsKeyed, availPriorOverrides, overrideSpecs)

            DispatchSlotChecking.CheckDispatchSlotsAreImplemented (env.DisplayEnv, cenv.infoReader, m, env.NameEnv, cenv.tcSink, false, implty, dispatchSlots, availPriorOverrides, overrideSpecs) |> ignore)

        // 6c. create the specs of overrides
        let allTypeImpls =
          overridesAndVirts |> List.map (fun (m, implty, _, dispatchSlotsKeyed, _, overrides) ->
              let overrides' =
                  [ for overrideMeth in overrides do
                        let (Override(_, _, id, (mtps, _), _, _, isFakeEventProperty, _) as ovinfo), (_, thisVal, methodVars, bindingAttribs, bindingBody) = overrideMeth
                        if not isFakeEventProperty then
                            let searchForOverride =
                                dispatchSlotsKeyed
                                |> NameMultiMap.find id.idText
                                |> List.tryPick (fun reqdSlot ->
                                     let virt = reqdSlot.MethodInfo
                                     if DispatchSlotChecking.IsExactMatch cenv.g cenv.amap m virt ovinfo then
                                         Some virt
                                     else
                                         None)

                            let overridden =
                                match searchForOverride with
                                | Some x -> x
                                | None -> error(Error(FSComp.SR.tcAtLeastOneOverrideIsInvalid(), synObjTy.Range))

                            yield TObjExprMethod(overridden.GetSlotSig(cenv.amap, m), bindingAttribs, mtps, [thisVal] :: methodVars, bindingBody, id.idRange) ]
              (implty, overrides'))

        let (objTy', overrides') = allTypeImpls.Head
        let extraImpls = allTypeImpls.Tail

        // 7. Build the implementation
        let expr = mkObjExpr(objTy', baseValOpt, ctorCall, overrides', extraImpls, mWholeExpr)
        let expr = mkCoerceIfNeeded cenv.g realObjTy objTy' expr
        expr, tpenv



//-------------------------------------------------------------------------
// TcConstStringExpr
//-------------------------------------------------------------------------

/// Check a constant string expression. It might be a 'printf' format string
and TcConstStringExpr cenv overallTy env m tpenv s =

    if (AddCxTypeEqualsTypeUndoIfFailed env.DisplayEnv cenv.css m overallTy cenv.g.string_ty) then
        mkString cenv.g m s, tpenv
    else
        TcFormatStringExpr cenv overallTy env m tpenv s

and TcFormatStringExpr cenv overallTy env m tpenv (fmtString: string) =
    let g = cenv.g
    let aty = NewInferenceType ()
    let bty = NewInferenceType ()
    let cty = NewInferenceType ()
    let dty = NewInferenceType ()
    let ety = NewInferenceType ()
    let formatTy = mkPrintfFormatTy g aty bty cty dty ety

    // This might qualify as a format string - check via a type directed rule
    let ok = not (isObjTy g overallTy) && AddCxTypeMustSubsumeTypeUndoIfFailed env.DisplayEnv cenv.css m overallTy formatTy

    if ok then
        // Parse the format string to work out the phantom types
        let formatStringCheckContext = match cenv.tcSink.CurrentSink with None -> None | Some sink -> sink.FormatStringCheckContext
        let normalizedString = (fmtString.Replace("\r\n", "\n").Replace("\r", "\n"))

        let _argTys, atyRequired, etyRequired, _percentATys, specifierLocations, _dotnetFormatString =
            try CheckFormatStrings.ParseFormatString m [m] g false false formatStringCheckContext normalizedString bty cty dty
            with Failure errString -> error (Error(FSComp.SR.tcUnableToParseFormatString errString, m))

        match cenv.tcSink.CurrentSink with
        | None -> ()
        | Some sink ->
            for specifierLocation, numArgs in specifierLocations do
                sink.NotifyFormatSpecifierLocation(specifierLocation, numArgs)

        UnifyTypes cenv env m aty atyRequired
        UnifyTypes cenv env m ety etyRequired
        let fmtExpr = mkCallNewFormat g m aty bty cty dty ety (mkString g m fmtString)
        fmtExpr, tpenv

    else
        UnifyTypes cenv env m overallTy g.string_ty
        mkString g m fmtString, tpenv

/// Check an interpolated string expression
and TcInterpolatedStringExpr cenv overallTy env m tpenv (parts: SynInterpolatedStringPart list) =
    let g = cenv.g

    let synFillExprs =
        parts
        |> List.choose (function
            | SynInterpolatedStringPart.String _ -> None
            | SynInterpolatedStringPart.FillExpr (fillExpr, _)  ->
                match fillExpr with
                // Detect "x" part of "...{x,3}..."
                | SynExpr.Tuple (false, [e; SynExpr.Const (SynConst.Int32 _align, _)], _, _) -> Some e
                | e -> Some e)

    let stringFragmentRanges =
        parts
        |> List.choose (function
            | SynInterpolatedStringPart.String (_,m) -> Some m
            | SynInterpolatedStringPart.FillExpr _  -> None)

    let printerTy = NewInferenceType ()
    let printerArgTy = NewInferenceType ()
    let printerResidueTy = NewInferenceType ()
    let printerResultTy = NewInferenceType ()
    let printerTupleTy = NewInferenceType ()
    let formatTy = mkPrintfFormatTy g printerTy printerArgTy printerResidueTy printerResultTy printerTupleTy

    // Check the library support is available in the referenced FSharp.Core
    let newFormatMethod =
        match GetIntrinsicConstructorInfosOfType cenv.infoReader m formatTy |> List.filter (fun minfo -> minfo.NumArgs = [3]) with
        | [ctorInfo] -> ctorInfo
        | _ -> languageFeatureNotSupportedInLibraryError cenv.g.langVersion LanguageFeature.StringInterpolation m

    let stringKind =
        // If this is an interpolated string then try to force the result to be a string
        if (AddCxTypeEqualsTypeUndoIfFailed env.DisplayEnv cenv.css m overallTy g.string_ty) then

            // And if that succeeds, the result of printing is a string
            UnifyTypes cenv env m printerArgTy g.unit_ty
            UnifyTypes cenv env m printerResidueTy g.string_ty
            UnifyTypes cenv env m printerResultTy overallTy

            // And if that succeeds, the printerTy and printerResultTy must be the same (there are no curried arguments)
            UnifyTypes cenv env m printerTy printerResultTy

            Choice1Of2 (true, newFormatMethod)

        // ... or if that fails then may be a FormattableString by a type-directed rule....
        elif (not (isObjTy g overallTy) &&
              ((g.system_FormattableString_tcref.CanDeref && AddCxTypeMustSubsumeTypeUndoIfFailed env.DisplayEnv cenv.css m overallTy g.system_FormattableString_ty)
               || (g.system_IFormattable_tcref.CanDeref && AddCxTypeMustSubsumeTypeUndoIfFailed env.DisplayEnv cenv.css m overallTy g.system_IFormattable_ty))) then

            // And if that succeeds, the result of printing is a string
            UnifyTypes cenv env m printerArgTy g.unit_ty
            UnifyTypes cenv env m printerResidueTy g.string_ty
            UnifyTypes cenv env m printerResultTy overallTy

            // Find the FormattableStringFactor.Create method in the .NET libraries
            let ad = env.eAccessRights
            let createMethodOpt =
                match TryFindIntrinsicOrExtensionMethInfo ResultCollectionSettings.AllResults cenv env m ad "Create" g.system_FormattableStringFactory_ty with
                | [x] -> Some x
                | _ -> None

            match createMethodOpt with
            | Some createMethod -> Choice2Of2 createMethod
            | None -> languageFeatureNotSupportedInLibraryError cenv.g.langVersion LanguageFeature.StringInterpolation m

        // ... or if that fails then may be a PrintfFormat by a type-directed rule....
        elif not (isObjTy g overallTy) && AddCxTypeMustSubsumeTypeUndoIfFailed env.DisplayEnv cenv.css m overallTy formatTy then

            // And if that succeeds, the printerTy and printerResultTy must be the same (there are no curried arguments)
            UnifyTypes cenv env m printerTy printerResultTy
            Choice1Of2 (false, newFormatMethod)

        else
            // this should fail and produce an error
            UnifyTypes cenv env m overallTy g.string_ty
            Choice1Of2 (true, newFormatMethod)

    let isFormattableString = (match stringKind with Choice2Of2 _ -> true | _ -> false)

    // The format string used for checking in CheckFormatStrings. This replaces interpolation holes with %P
    let printfFormatString =
        parts
        |> List.map (function
            | SynInterpolatedStringPart.String (s, _) -> s
            | SynInterpolatedStringPart.FillExpr (fillExpr, format) ->
                let alignText =
                    match fillExpr with
                    // Validate and detect ",3" part of "...{x,3}..."
                    | SynExpr.Tuple (false, args, _, _) ->
                        match args with
                        | [_; SynExpr.Const (SynConst.Int32 align, _)] -> string align
                        | _ -> errorR(Error(FSComp.SR.tcInvalidAlignmentInInterpolatedString(), m)); ""
                    | _ -> ""
                let formatText = match format with None -> "()" | Some n -> "(" + n.idText + ")"
                "%" + alignText + "P" + formatText )
        |> String.concat ""

    // Parse the format string to work out the phantom types and check for absence of '%' specifiers in FormattableString
    //
    // If FormatStringCheckContext is set (i.e. we are doing foreground checking in the IDE)
    // then we check the string twice, once to collect % positions and once to get errors.
    // The process of getting % positions doesn't process the string in a semantically accurate way
    // (but is enough to report % locations correctly), as it fetched the pieces from the
    // original source and this may include some additional characters,
    // and also doesn't raise all necessary errors
    match cenv.tcSink.CurrentSink with
    | Some sink when sink.FormatStringCheckContext.IsSome ->
        try
            let _argTys, _printerTy, _printerTupleTyRequired, _percentATys, specifierLocations, _dotnetFormatString =
                CheckFormatStrings.ParseFormatString m stringFragmentRanges g true isFormattableString sink.FormatStringCheckContext printfFormatString printerArgTy printerResidueTy printerResultTy
            for specifierLocation, numArgs in specifierLocations do
                sink.NotifyFormatSpecifierLocation(specifierLocation, numArgs)
        with _err->
            ()
    | _ -> ()

    let argTys, _printerTy, printerTupleTyRequired, percentATys, _specifierLocations, dotnetFormatString =
        try
            CheckFormatStrings.ParseFormatString m stringFragmentRanges g true isFormattableString None printfFormatString printerArgTy printerResidueTy printerResultTy
        with Failure errString ->
            error (Error(FSComp.SR.tcUnableToParseInterpolatedString errString, m))

    // Check the expressions filling the holes
    if argTys.Length <> synFillExprs.Length then
        error (Error(FSComp.SR.tcInterpolationMixedWithPercent(), m))

    match stringKind with

    // The case for $"..." used as type string and $"...%d{x}..." used as type PrintfFormat - create a PrintfFormat that captures
    // is arguments
    | Choice1Of2 (isString, newFormatMethod) ->

        UnifyTypes cenv env m printerTupleTy printerTupleTyRequired

        if List.isEmpty synFillExprs then
            let str = mkString g m printfFormatString

            if isString then
                str, tpenv
            else
                mkCallNewFormat cenv.g m printerTy printerArgTy printerResidueTy printerResultTy printerTupleTy str, tpenv
        else
            // Type check the expressions filling the holes
            let flexes = argTys |> List.map (fun _ -> false)
            let fillExprs, tpenv = TcExprs cenv env m tpenv flexes argTys synFillExprs

            let fillExprsBoxed = (argTys, fillExprs) ||> List.map2 (mkCallBox g m)

            let argsExpr = mkArray (g.obj_ty, fillExprsBoxed, m)
            let percentATysExpr =
                if percentATys.Length = 0 then
                    mkNull m (mkArrayType g g.system_Type_ty)
                else
                    let tyExprs = percentATys |> Array.map (mkCallTypeOf g m) |> Array.toList
                    mkArray (g.system_Type_ty, tyExprs, m)

            let fmtExpr = MakeMethInfoCall cenv.amap m newFormatMethod [] [mkString g m printfFormatString; argsExpr; percentATysExpr]

            if isString then
                // Make the call to sprintf
                mkCall_sprintf g m printerTy fmtExpr [], tpenv
            else
                fmtExpr, tpenv

    // The case for $"..." used as type FormattableString or IFormattable
    | Choice2Of2 createFormattableStringMethod ->

        // Type check the expressions filling the holes
        let flexes = argTys |> List.map (fun _ -> false)
        let fillExprs, tpenv = TcExprs cenv env m tpenv flexes argTys synFillExprs

        let fillExprsBoxed = (argTys, fillExprs) ||> List.map2 (mkCallBox g m)

        let dotnetFormatStringExpr = mkString g m dotnetFormatString
        let argsExpr = mkArray (g.obj_ty, fillExprsBoxed, m)

        // FormattableString are *always* turned into FormattableStringFactory.Create calls, boxing each argument
        let createExpr, _ = BuildPossiblyConditionalMethodCall cenv env NeverMutates m false createFormattableStringMethod NormalValUse [] [dotnetFormatStringExpr; argsExpr] []

        let resultExpr =
            if typeEquiv g overallTy g.system_IFormattable_ty then
                mkCoerceIfNeeded g g.system_IFormattable_ty g.system_FormattableString_ty createExpr
            else
                createExpr
        resultExpr, tpenv

//-------------------------------------------------------------------------
// TcConstExpr
//-------------------------------------------------------------------------

/// Check a constant expression.
and TcConstExpr cenv overallTy env m tpenv c =
    match c with

    // NOTE: these aren't "really" constants
    | SynConst.Bytes (bytes, _, m) ->
       UnifyTypes cenv env m overallTy (mkByteArrayTy cenv.g)
       Expr.Op (TOp.Bytes bytes, [], [], m), tpenv

    | SynConst.UInt16s arr ->
       UnifyTypes cenv env m overallTy (mkArrayType cenv.g cenv.g.uint16_ty); Expr.Op (TOp.UInt16s arr, [], [], m), tpenv

    | SynConst.UserNum (s, suffix) ->
        let expr =
            let modName = "NumericLiteral" + suffix
            let ad = env.eAccessRights
            match ResolveLongIdentAsModuleOrNamespace cenv.tcSink ResultCollectionSettings.AtMostOneResult cenv.amap m true OpenQualified env.eNameResEnv ad (ident (modName, m)) [] false with
            | Result []
            | Exception _ -> error(Error(FSComp.SR.tcNumericLiteralRequiresModule modName, m))
            | Result ((_, mref, _) :: _) ->
                let expr =
                    try
                        match int32 s with
                        | 0 -> SynExpr.App (ExprAtomicFlag.Atomic, false, mkSynLidGet m [modName] "FromZero", SynExpr.Const (SynConst.Unit, m), m)
                        | 1 -> SynExpr.App (ExprAtomicFlag.Atomic, false, mkSynLidGet m [modName] "FromOne", SynExpr.Const (SynConst.Unit, m), m)
                        | i32 -> SynExpr.App (ExprAtomicFlag.Atomic, false, mkSynLidGet m [modName] "FromInt32", SynExpr.Const (SynConst.Int32 i32, m), m)
                    with _ ->
                        try
                            let i64 = int64 s
                            SynExpr.App (ExprAtomicFlag.Atomic, false, mkSynLidGet m [modName] "FromInt64", SynExpr.Const (SynConst.Int64 i64, m), m)
                        with _ ->
                            SynExpr.App (ExprAtomicFlag.Atomic, false, mkSynLidGet m [modName] "FromString", SynExpr.Const (SynConst.String (s, SynStringKind.Regular, m), m), m)

                if suffix <> "I" then
                    expr
                else
                    match ccuOfTyconRef mref with
                    | Some ccu when ccuEq ccu cenv.g.fslibCcu ->
                        SynExpr.Typed (expr, SynType.LongIdent(LongIdentWithDots(pathToSynLid m ["System";"Numerics";"BigInteger"], [])), m)
                    | _ ->
                        expr

        TcExpr cenv overallTy env tpenv expr

    | _ ->
        let c' = TcConst cenv overallTy m env c
        Expr.Const (c', m, overallTy), tpenv


//-------------------------------------------------------------------------
// TcAssertExpr
//-------------------------------------------------------------------------

// Check an 'assert x' expression.
and TcAssertExpr cenv overallTy env (m: range) tpenv x =
    let synm = m.MakeSynthetic() // Mark as synthetic so the language service won't pick it up.
    let callDiagnosticsExpr = SynExpr.App (ExprAtomicFlag.Atomic, false, mkSynLidGet synm ["System";"Diagnostics";"Debug"] "Assert",
                                           // wrap an extra parentheses so 'assert(x=1) isn't considered a named argument to a method call
                                           SynExpr.Paren (x, range0, None, synm), synm)

    TcExpr cenv overallTy env tpenv callDiagnosticsExpr


and TcRecdExpr cenv overallTy env tpenv (inherits, optOrigExpr, flds, mWholeExpr) =

    let requiresCtor = (GetCtorShapeCounter env = 1) // Get special expression forms for constructors
    let haveCtor = Option.isSome inherits

    let optOrigExpr, tpenv =
      match optOrigExpr with
      | None -> None, tpenv
      | Some (origExpr, _) ->
          match inherits with
          | Some (_, _, mInherits, _, _) -> error(Error(FSComp.SR.tcInvalidRecordConstruction(), mInherits))
          | None ->
              let olde, tpenv = TcExpr cenv overallTy env tpenv origExpr
              Some (olde), tpenv

    let hasOrigExpr = optOrigExpr.IsSome

    let fldsList =
        let flds =
            [
                // if we met at least one field that is not syntactically correct - raise ReportedError to transfer control to the recovery routine
                for ((lidwd, isOk), v, _) in flds do
                    if not isOk then
                        // raising ReportedError None transfers control to the closest errorRecovery point but do not make any records into log
                        // we assume that parse errors were already reported
                        raise (ReportedError None)

                    yield (List.frontAndBack lidwd.Lid, v)
            ]

        match flds with
        | [] -> []
        | _ ->
            let tinst, tcref, _, fldsList = BuildFieldMap cenv env hasOrigExpr overallTy flds mWholeExpr
            let gtyp = mkAppTy tcref tinst
            UnifyTypes cenv env mWholeExpr overallTy gtyp

            [ for n, v in fldsList do
                match v with
                | Some v -> yield n, v
                | None -> () ]

    let optOrigExprInfo =
        match optOrigExpr with
        | None -> None
        | Some(olde) ->
            let oldvaddr, oldvaddre = mkCompGenLocal mWholeExpr "inputRecord" (if isStructTy cenv.g overallTy then mkByrefTy cenv.g overallTy else overallTy)
            Some(olde, oldvaddr, oldvaddre)

    if hasOrigExpr && not (isRecdTy cenv.g overallTy) then
        errorR(Error(FSComp.SR.tcExpressionFormRequiresRecordTypes(), mWholeExpr))

    if requiresCtor || haveCtor then
        if not (isFSharpObjModelTy cenv.g overallTy) then
            // Deliberate no-recovery failure here to prevent cascading internal errors
            error(Error(FSComp.SR.tcInheritedTypeIsNotObjectModelType(), mWholeExpr))
        if not requiresCtor then
            errorR(Error(FSComp.SR.tcObjectConstructionExpressionCanOnlyImplementConstructorsInObjectModelTypes(), mWholeExpr))
    else
        if isNil flds then
            let errorInfo = if hasOrigExpr then FSComp.SR.tcEmptyCopyAndUpdateRecordInvalid() else FSComp.SR.tcEmptyRecordInvalid()
            error(Error(errorInfo, mWholeExpr))

        if isFSharpObjModelTy cenv.g overallTy then errorR(Error(FSComp.SR.tcTypeIsNotARecordTypeNeedConstructor(), mWholeExpr))
        elif not (isRecdTy cenv.g overallTy) then errorR(Error(FSComp.SR.tcTypeIsNotARecordType(), mWholeExpr))

    let superInitExprOpt , tpenv =
        match inherits, GetSuperTypeOfType cenv.g cenv.amap mWholeExpr overallTy with
        | Some (superTy, arg, m, _, _), Some realSuperTy ->
            // Constructor expression, with an explicit 'inheritedTys clause. Check the inherits clause.
            let e, tpenv = TcExpr cenv realSuperTy env tpenv (SynExpr.New (true, superTy, arg, m))
            Some e, tpenv
        | None, Some realSuperTy when requiresCtor ->
            // Constructor expression, No 'inherited' clause, hence look for a default constructor
            let e, tpenv = TcNewExpr cenv env tpenv realSuperTy None true (SynExpr.Const (SynConst.Unit, mWholeExpr)) mWholeExpr
            Some e, tpenv
        | None, _ ->
            None, tpenv
        | _, None ->
            errorR(InternalError("Unexpected failure in getting super type", mWholeExpr))
            None, tpenv

    let expr, tpenv = TcRecordConstruction cenv overallTy env tpenv optOrigExprInfo overallTy fldsList mWholeExpr

    let expr =
        match superInitExprOpt  with
        | _ when isStructTy cenv.g overallTy -> expr
        | Some superInitExpr  -> mkCompGenSequential mWholeExpr superInitExpr  expr
        | None -> expr
    expr, tpenv


// Check '{| .... |}'
and TcAnonRecdExpr cenv overallTy env tpenv (isStruct, optOrigSynExpr, unsortedFieldIdsAndSynExprsGiven, mWholeExpr) =
    let unsortedFieldSynExprsGiven = List.map snd unsortedFieldIdsAndSynExprsGiven

    match optOrigSynExpr with
    | None ->
        let unsortedFieldIds = unsortedFieldIdsAndSynExprsGiven |> List.map fst |> List.toArray
        let anonInfo, sortedFieldTys = UnifyAnonRecdTypeAndInferCharacteristics env.eContextInfo cenv env.DisplayEnv mWholeExpr overallTy isStruct unsortedFieldIds

        // Sort into canonical order
        let sortedIndexedArgs =
            unsortedFieldIdsAndSynExprsGiven
            |> List.indexed
            |> List.sortBy (fun (i,_) -> unsortedFieldIds.[i].idText)

        // Map from sorted indexes to unsorted indexes
        let sigma = List.map fst sortedIndexedArgs |> List.toArray
        let sortedFieldExprs = List.map snd sortedIndexedArgs

        sortedFieldExprs |> List.iteri (fun j (x, _) ->
            let item = Item.AnonRecdField(anonInfo, sortedFieldTys, j, x.idRange)
            CallNameResolutionSink cenv.tcSink (x.idRange, env.NameEnv, item, emptyTyparInst, ItemOccurence.Use, env.eAccessRights))

        let unsortedFieldTys =
            sortedFieldTys
            |> List.indexed
            |> List.sortBy (fun (sortedIdx, _) -> sigma.[sortedIdx])
            |> List.map snd

        let flexes = unsortedFieldTys |> List.map (fun _ -> true)

        let unsortedCheckedArgs, tpenv = TcExprs cenv env mWholeExpr tpenv flexes unsortedFieldTys unsortedFieldSynExprsGiven

        mkAnonRecd cenv.g mWholeExpr anonInfo unsortedFieldIds unsortedCheckedArgs unsortedFieldTys, tpenv

    | Some (origExpr, _) ->
        // The fairly complex case '{| origExpr with X = 1; Y = 2 |}'
        // The origExpr may be either a record or anonymous record.
        // The origExpr may be either a struct or not.
        // All the properties of origExpr are copied across except where they are overridden.
        // The result is a field-sorted anonymous record.
        //
        // Unlike in the case of record type copy-and-update we do _not_ assume that the origExpr has the same type as the overall expression.
        // Unlike in the case of record type copy-and-update {| a with X = 1 |} does not force a.X to exist or have had type 'int'

        let origExprTy = NewInferenceType()
        let origExprChecked, tpenv = TcExpr cenv origExprTy env tpenv origExpr
        let oldv, oldve = mkCompGenLocal mWholeExpr "inputRecord" origExprTy
        let mOrigExpr = origExpr.Range

        if not (isAppTy cenv.g origExprTy || isAnonRecdTy cenv.g origExprTy) then
            error (Error (FSComp.SR.tcCopyAndUpdateNeedsRecordType(), mOrigExpr))

        let origExprIsStruct =
            match tryDestAnonRecdTy cenv.g origExprTy with
            | ValueSome (anonInfo, _) -> evalTupInfoIsStruct anonInfo.TupInfo
            | ValueNone ->
                let tcref, _ = destAppTy cenv.g origExprTy
                tcref.IsStructOrEnumTycon

        let wrap, oldveaddr, _readonly, _writeonly = mkExprAddrOfExpr cenv.g origExprIsStruct false NeverMutates oldve None mOrigExpr

        // Put all the expressions in unsorted order. The new bindings come first. The origin of each is tracked using
        ///   - Choice1Of2 for a new binding
        ///   - Choice2Of2 for a binding coming from the original expression
        let unsortedIdAndExprsAll =
            [| for (id, e) in unsortedFieldIdsAndSynExprsGiven do
                    yield (id, Choice1Of2 e)
               match tryDestAnonRecdTy cenv.g origExprTy with
               | ValueSome (anonInfo, tinst) ->
                   for (i, id) in Array.indexed anonInfo.SortedIds do
                       yield id, Choice2Of2 (mkAnonRecdFieldGetViaExprAddr (anonInfo, oldveaddr, tinst, i, mOrigExpr))
               | ValueNone ->
                    match tryAppTy cenv.g origExprTy with
                    | ValueSome(tcref, tinst) when tcref.IsRecordTycon ->
                        let fspecs = tcref.Deref.TrueInstanceFieldsAsList
                        for fspec in fspecs do
                            yield fspec.Id, Choice2Of2 (mkRecdFieldGetViaExprAddr (oldveaddr, tcref.MakeNestedRecdFieldRef fspec, tinst, mOrigExpr))
                    | _ ->
                        error (Error (FSComp.SR.tcCopyAndUpdateNeedsRecordType(), mOrigExpr)) |]
            |> Array.distinctBy (fst >> textOfId)

        let unsortedFieldIdsAll = Array.map fst unsortedIdAndExprsAll

        let anonInfo, sortedFieldTysAll = UnifyAnonRecdTypeAndInferCharacteristics env.eContextInfo cenv env.DisplayEnv mWholeExpr overallTy isStruct unsortedFieldIdsAll

        let sortedIndexedFieldsAll = unsortedIdAndExprsAll |> Array.indexed |> Array.sortBy (snd >> fst >> textOfId)

        // map from sorted indexes to unsorted indexes
        let sigma = Array.map fst sortedIndexedFieldsAll

        let sortedFieldsAll = Array.map snd sortedIndexedFieldsAll

        // Report _all_ identifiers to name resolution. We should likely just report the ones
        // that are explicit in source code.
        sortedFieldsAll |> Array.iteri (fun j (x, expr) ->
            match expr with
            | Choice1Of2 _ ->
                let item = Item.AnonRecdField(anonInfo, sortedFieldTysAll, j, x.idRange)
                CallNameResolutionSink cenv.tcSink (x.idRange, env.NameEnv, item, emptyTyparInst, ItemOccurence.Use, env.eAccessRights)
            | Choice2Of2 _ -> ())

        let unsortedFieldTysAll =
            sortedFieldTysAll
            |> List.indexed
            |> List.sortBy (fun (sortedIdx, _) -> sigma.[sortedIdx])
            |> List.map snd

        let unsortedFieldTysGiven =
            unsortedFieldTysAll
            |> List.take unsortedFieldIdsAndSynExprsGiven.Length

        let flexes = unsortedFieldTysGiven |> List.map (fun _ -> true)

        // Check the expressions in unsorted order
        let unsortedFieldExprsGiven, tpenv =
            TcExprs cenv env mWholeExpr tpenv flexes unsortedFieldTysGiven unsortedFieldSynExprsGiven

        let unsortedFieldExprsGiven = unsortedFieldExprsGiven |> List.toArray

        let unsortedFieldIds =
            unsortedIdAndExprsAll
            |> Array.map fst

        let unsortedFieldExprs =
            unsortedIdAndExprsAll
            |> Array.mapi (fun unsortedIdx (_, expr) ->
                match expr with
                | Choice1Of2 _ -> unsortedFieldExprsGiven.[unsortedIdx]
                | Choice2Of2 subExpr -> UnifyTypes cenv env mOrigExpr (tyOfExpr cenv.g subExpr) unsortedFieldTysAll.[unsortedIdx]; subExpr)
            |> List.ofArray

        // Permute the expressions to sorted order in the TAST
        let expr = mkAnonRecd cenv.g mWholeExpr anonInfo unsortedFieldIds unsortedFieldExprs unsortedFieldTysAll
        let expr = wrap expr

        // Bind the original expression
        let expr = mkCompGenLet mOrigExpr oldv origExprChecked expr
        expr, tpenv


and TcForEachExpr cenv overallTy env tpenv (pat, enumSynExpr, bodySynExpr, mWholeExpr, spForLoop) =
    let tryGetOptimizeSpanMethodsAux g m ty isReadOnlySpan =
        match (if isReadOnlySpan then tryDestReadOnlySpanTy g m ty else tryDestSpanTy g m ty) with
        | ValueSome(struct(_, destTy)) ->
            match TryFindFSharpSignatureInstanceGetterProperty cenv env m "Item" ty [ g.int32_ty; (if isReadOnlySpan then mkInByrefTy g destTy else mkByrefTy g destTy) ],
                  TryFindFSharpSignatureInstanceGetterProperty cenv env m "Length" ty [ g.int32_ty ] with
            | Some(itemPropInfo), Some(lengthPropInfo) ->
                ValueSome(struct(itemPropInfo.GetterMethod, lengthPropInfo.GetterMethod, isReadOnlySpan))
            | _ ->
                ValueNone
        | _ ->
            ValueNone

    let tryGetOptimizeSpanMethods g m ty =
        let result = tryGetOptimizeSpanMethodsAux g m ty false
        if result.IsSome then
            result
        else
            tryGetOptimizeSpanMethodsAux g m ty true

    UnifyTypes cenv env mWholeExpr overallTy cenv.g.unit_ty

    let mPat = pat.Range
    //let mBodyExpr = bodySynExpr.Range
    let mEnumExpr = enumSynExpr.Range
    let mForLoopStart = match spForLoop with DebugPointAtFor.Yes mStart -> mStart | DebugPointAtFor.No -> mEnumExpr

    // Check the expression being enumerated
    let enumExpr, enumExprTy, tpenv = TcExprOfUnknownType cenv env tpenv enumSynExpr

    // Depending on its type we compile it in different ways
    let enumElemTy, bodyExprFixup, overallExprFixup, iterationTechnique =
        match enumExpr with

        // optimize 'for i in n .. m do'
        | Expr.App (Expr.Val (vf, _, _), _, [tyarg], [startExpr;finishExpr], _)
             when valRefEq cenv.g vf cenv.g.range_op_vref && typeEquiv cenv.g tyarg cenv.g.int_ty ->
               (cenv.g.int32_ty, (fun _ x -> x), id, Choice1Of3 (startExpr, finishExpr))

        // optimize 'for i in arr do'
        | _ when isArray1DTy cenv.g enumExprTy ->
            let arrVar, arrExpr = mkCompGenLocal mEnumExpr "arr" enumExprTy
            let idxVar, idxExpr = mkCompGenLocal mPat "idx" cenv.g.int32_ty
            let elemTy = destArrayTy cenv.g enumExprTy

            // Evaluate the array index lookup
            let bodyExprFixup elemVar bodyExpr = mkCompGenLet mForLoopStart elemVar (mkLdelem cenv.g mForLoopStart elemTy arrExpr idxExpr) bodyExpr

            // Evaluate the array expression once and put it in arrVar
            let overallExprFixup overallExpr = mkCompGenLet mForLoopStart arrVar enumExpr overallExpr

            // Ask for a loop over integers for the given range
            (elemTy, bodyExprFixup, overallExprFixup, Choice2Of3 (idxVar, mkZero cenv.g mForLoopStart, mkDecr cenv.g mForLoopStart (mkLdlen cenv.g mForLoopStart arrExpr)))

        | _ ->
            // try optimize 'for i in span do' for span or readonlyspan
            match tryGetOptimizeSpanMethods cenv.g mWholeExpr enumExprTy with
            | ValueSome(struct(getItemMethInfo, getLengthMethInfo, isReadOnlySpan)) ->
                let tcVal = LightweightTcValForUsingInBuildMethodCall cenv.g
                let spanVar, spanExpr = mkCompGenLocal mEnumExpr "span" enumExprTy
                let idxVar, idxExpr = mkCompGenLocal mPat "idx" cenv.g.int32_ty
                let struct(_, elemTy) = if isReadOnlySpan then destReadOnlySpanTy cenv.g mWholeExpr enumExprTy else destSpanTy cenv.g mWholeExpr enumExprTy
                let elemAddrTy = if isReadOnlySpan then mkInByrefTy cenv.g elemTy else mkByrefTy cenv.g elemTy

                // Evaluate the span index lookup
                let bodyExprFixup elemVar bodyExpr =
                    let elemAddrVar, _ = mkCompGenLocal mForLoopStart "addr" elemAddrTy
                    let e = mkCompGenLet mForLoopStart elemVar (mkAddrGet mForLoopStart (mkLocalValRef elemAddrVar)) bodyExpr
                    let getItemCallExpr, _ = BuildMethodCall tcVal cenv.g cenv.amap PossiblyMutates mWholeExpr true getItemMethInfo ValUseFlag.NormalValUse [] [ spanExpr ] [ idxExpr ]
                    mkCompGenLet mForLoopStart elemAddrVar getItemCallExpr e

                // Evaluate the span expression once and put it in spanVar
                let overallExprFixup overallExpr = mkCompGenLet mForLoopStart spanVar enumExpr overallExpr

                let getLengthCallExpr, _ = BuildMethodCall tcVal cenv.g cenv.amap PossiblyMutates mWholeExpr true getLengthMethInfo ValUseFlag.NormalValUse [] [ spanExpr ] []

                // Ask for a loop over integers for the given range
                (elemTy, bodyExprFixup, overallExprFixup, Choice2Of3 (idxVar, mkZero cenv.g mForLoopStart, mkDecr cenv.g mForLoopStart getLengthCallExpr))

            | _ ->
                let enumerableVar, enumerableExprInVar = mkCompGenLocal mEnumExpr "inputSequence" enumExprTy
                let enumeratorVar, enumeratorExpr, _, enumElemTy, getEnumExpr, getEnumTy, guardExpr, _, currentExpr =
                    AnalyzeArbitraryExprAsEnumerable cenv env true mEnumExpr enumExprTy enumerableExprInVar
                (enumElemTy, (fun _ x -> x), id, Choice3Of3(enumerableVar, enumeratorVar, enumeratorExpr, getEnumExpr, getEnumTy, guardExpr, currentExpr))

    let pat, _, vspecs, envinner, tpenv = TcMatchPattern cenv enumElemTy env tpenv (pat, None)
    let elemVar, pat =
        // nice: don't introduce awful temporary for r.h.s. in the 99% case where we know what we're binding it to
        match pat with
        | TPat_as (pat1, PBind(v, TypeScheme([], _)), _) ->
              v, pat1
        | _ ->
              let tmp, _ = mkCompGenLocal pat.Range "forLoopVar" enumElemTy
              tmp, pat

    // Check the body of the loop
    let bodyExpr, tpenv = TcStmt cenv envinner tpenv bodySynExpr

    // Add the pattern match compilation
    let bodyExpr =
        let valsDefinedByMatching = ListSet.remove valEq elemVar vspecs
        CompilePatternForMatch 
            cenv env enumSynExpr.Range pat.Range false IgnoreWithWarning (elemVar, [], None) 
            [TClause(pat, None, TTarget(valsDefinedByMatching, bodyExpr, DebugPointForTarget.Yes, None), mForLoopStart)] 
            enumElemTy 
            overallTy

    // Apply the fixup to bind the elemVar if needed
    let bodyExpr = bodyExprFixup elemVar bodyExpr

    // Build the overall loop
    let overallExpr =

        match iterationTechnique with

        // Build iteration as a for loop
        | Choice1Of3(startExpr, finishExpr) ->
            mkFastForLoop cenv.g (spForLoop, mWholeExpr, elemVar, startExpr, true, finishExpr, bodyExpr)

        // Build iteration as a for loop with a specific index variable that is not the same as the elemVar
        | Choice2Of3(idxVar, startExpr, finishExpr) ->
            mkFastForLoop cenv.g (spForLoop, mWholeExpr, idxVar, startExpr, true, finishExpr, bodyExpr)

        // Build iteration as a while loop with a try/finally disposal
        | Choice3Of3(enumerableVar, enumeratorVar, _, getEnumExpr, _, guardExpr, currentExpr) ->

            // This compiled for must be matched EXACTLY by CompiledForEachExpr in opt.fs and creflect.fs
            mkCompGenLet mForLoopStart enumerableVar enumExpr
              (let cleanupE = BuildDisposableCleanup cenv env mWholeExpr enumeratorVar
               let spBind = match spForLoop with DebugPointAtFor.Yes spStart -> DebugPointAtBinding.Yes spStart | DebugPointAtFor.No -> DebugPointAtBinding.NoneAtSticky
               (mkLet spBind mForLoopStart enumeratorVar getEnumExpr
                   (mkTryFinally cenv.g
                       (mkWhile cenv.g
                           (DebugPointAtWhile.No,
                            WhileLoopForCompiledForEachExprMarker, guardExpr,
                            mkCompGenLet mForLoopStart elemVar currentExpr bodyExpr,
                            mForLoopStart),
                        cleanupE, mForLoopStart, cenv.g.unit_ty, DebugPointAtTry.No, DebugPointAtFinally.No))))

    let overallExpr = overallExprFixup overallExpr
    overallExpr, tpenv

//-------------------------------------------------------------------------
// TcQuotationExpr
//-------------------------------------------------------------------------

and TcQuotationExpr cenv overallTy env tpenv (_oper, raw, ast, isFromQueryExpression, m) =
    let astTy = NewInferenceType ()

    // Assert the overall type for the domain of the quotation template
    UnifyTypes cenv env m overallTy (if raw then mkRawQuotedExprTy cenv.g else mkQuotedExprTy cenv.g astTy)

    // Check the expression
    let expr, tpenv = TcExpr cenv astTy env tpenv ast

    // Wrap the expression
    let expr = Expr.Quote (expr, ref None, isFromQueryExpression, m, overallTy)

    // Coerce it if needed
    let expr = if raw then mkCoerceExpr(expr, (mkRawQuotedExprTy cenv.g), m, (tyOfExpr cenv.g expr)) else expr

    // We serialize the quoted expression to bytes in IlxGen after type inference etc. is complete.
    expr, tpenv

/// When checking sequence of function applications, 
/// type applications and dot-notation projections, first extract known
/// type information from the applications.
///
/// 'overallTy' is the type expected for the entire chain of expr + lookups.
/// 'exprty' is the type of the expression on the left of the lookup chain.
///
/// We propagate information from the expected overall type 'overallTy'. The use
/// of function application syntax unambiguously implies that 'overallTy' is a function type.
and Propagate cenv overallTy env tpenv (expr: ApplicableExpr) exprty delayed =

    let rec propagate isAddrOf delayedList mExpr exprty =
        match delayedList with
        | [] ->

            if not (isNil delayed) then

                // We generate a tag inference parameter to the return type for "&x" and 'NativePtr.toByRef'
                // See RFC FS-1053.md
                let exprty =
                    if isAddrOf && isByrefTy cenv.g exprty then
                        mkByrefTyWithInference cenv.g (destByrefTy cenv.g exprty) (NewByRefKindInferenceType cenv.g mExpr)
                    elif isByrefTy cenv.g exprty then
                        // Implicit dereference on byref on return
                        if isByrefTy cenv.g overallTy then
                             errorR(Error(FSComp.SR.tcByrefReturnImplicitlyDereferenced(), mExpr))
                        destByrefTy cenv.g exprty
                    else
                        exprty

                UnifyTypesAndRecover cenv env mExpr overallTy exprty

        | DelayedDot :: _
        | DelayedSet _ :: _
        | DelayedDotLookup _ :: _ -> ()
        | DelayedTypeApp (_, _mTypeArgs, mExprAndTypeArgs) :: delayedList' ->
            // Note this case should not occur: would eventually give an "Unexpected type application" error in TcDelayed
            propagate isAddrOf delayedList' mExprAndTypeArgs exprty

        | DelayedApp (_, arg, mExprAndArg) :: delayedList' ->
            let denv = env.DisplayEnv
            match UnifyFunctionTypeUndoIfFailed cenv denv mExpr exprty with
            | ValueSome (_, resultTy) ->

                // We add tag parameter to the return type for "&x" and 'NativePtr.toByRef'
                // See RFC FS-1053.md
                let isAddrOf =
                    match expr with
                    | ApplicableExpr(_, Expr.App (Expr.Val (vf, _, _), _, _, [], _), _)
                        when (valRefEq cenv.g vf cenv.g.addrof_vref ||
                              valRefEq cenv.g vf cenv.g.nativeptr_tobyref_vref) -> true
                    | _ -> false

                propagate isAddrOf delayedList' mExprAndArg resultTy

            | _ ->
                let mArg = arg.Range
                match arg with
                | SynExpr.CompExpr _ -> ()
                | SynExpr.ArrayOrListOfSeqExpr (false, _, _) ->
                    // 'delayed' is about to be dropped on the floor, first do rudimentary checking to get name resolutions in its body
                    RecordNameAndTypeResolutions_IdeallyWithoutHavingOtherEffects_Delayed cenv env tpenv delayed
                    if IsIndexerType cenv.g cenv.amap expr.Type then
                        match expr.Expr with
                        | Expr.Val (d, _, _) ->
                            error (NotAFunctionButIndexer(denv, overallTy, Some d.DisplayName, mExpr, mArg))
                        | _ ->
                            error (NotAFunctionButIndexer(denv, overallTy, None, mExpr, mArg))
                    else
                        error (NotAFunction(denv, overallTy, mExpr, mArg))
                | _ ->
                    // 'delayed' is about to be dropped on the floor, first do rudimentary checking to get name resolutions in its body
                    RecordNameAndTypeResolutions_IdeallyWithoutHavingOtherEffects_Delayed cenv env tpenv delayed
                    error (NotAFunction(denv, overallTy, mExpr, mArg))

    propagate false delayed expr.Range exprty

and PropagateThenTcDelayed cenv overallTy env tpenv mExpr expr exprty (atomicFlag: ExprAtomicFlag) delayed =
    Propagate cenv overallTy env tpenv expr exprty delayed
    TcDelayed cenv overallTy env tpenv mExpr expr exprty atomicFlag delayed


/// Typecheck "expr ... " constructs where "..." is a sequence of applications,
/// type applications and dot-notation projections.
and TcDelayed cenv overallTy env tpenv mExpr expr exprty (atomicFlag: ExprAtomicFlag) delayed =

    // OK, we've typechecked the thing on the left of the delayed lookup chain.
    // We can now record for posterity the type of this expression and the location of the expression.
    if (atomicFlag = ExprAtomicFlag.Atomic) then
        CallExprHasTypeSink cenv.tcSink (mExpr, env.NameEnv, exprty, env.eAccessRights)

    match delayed with
    | []
    | DelayedDot :: _ ->
        UnifyTypes cenv env mExpr overallTy exprty
        expr.Expr, tpenv
    // Expr.M (args) where x.M is a .NET method or index property
    // expr.M<tyargs>(args) where x.M is a .NET method or index property
    // expr.M where x.M is a .NET method or index property
    | DelayedDotLookup (longId, mDotLookup) :: otherDelayed ->
        TcLookupThen cenv overallTy env tpenv mExpr expr.Expr exprty longId otherDelayed mDotLookup
    // f x
    | DelayedApp (hpa, arg, mExprAndArg) :: otherDelayed ->
        TcFunctionApplicationThen cenv overallTy env tpenv mExprAndArg expr exprty arg hpa otherDelayed
    // f<tyargs>
    | DelayedTypeApp (_, mTypeArgs, _mExprAndTypeArgs) :: _ ->
        error(Error(FSComp.SR.tcUnexpectedTypeArguments(), mTypeArgs))
    | DelayedSet (synExpr2, mStmt) :: otherDelayed ->
        if not (isNil otherDelayed) then error(Error(FSComp.SR.tcInvalidAssignment(), mExpr))
        UnifyTypes cenv env mExpr overallTy cenv.g.unit_ty
        let expr = expr.Expr
        let _wrap, exprAddress, _readonly, _writeonly = mkExprAddrOfExpr cenv.g true false DefinitelyMutates expr None mExpr
        let vty = tyOfExpr cenv.g expr
        // Always allow subsumption on assignment to fields
        let expr2, tpenv = TcExprFlex cenv true false vty env tpenv synExpr2
        let v, _ve = mkCompGenLocal mExpr "addr" (mkByrefTy cenv.g vty)
        mkCompGenLet mStmt v exprAddress (mkAddrSet mStmt (mkLocalValRef v) expr2), tpenv


/// Convert the delayed identifiers to a dot-lookup.
///
/// TcItemThen: For StaticItem [.Lookup], mPrior is the range of StaticItem
/// TcLookupThen: For expr.InstanceItem [.Lookup], mPrior is the range of expr.InstanceItem
and delayRest rest mPrior delayed =
    match rest with
    | [] -> delayed
    | longId ->
        let mPriorAndLongId = unionRanges mPrior (rangeOfLid longId)
        DelayedDotLookup (rest, mPriorAndLongId) :: delayed

/// Typecheck "nameof" expressions
and TcNameOfExpr cenv env tpenv (synArg: SynExpr) =

    let rec stripParens expr =
        match expr with
        | SynExpr.Paren(expr, _, _, _) -> stripParens expr
        | _ -> expr

    let cleanSynArg = stripParens synArg
    let m = cleanSynArg.Range
    let rec check overallTyOpt resultOpt expr (delayed: DelayedItem list) =
        match expr with
        | LongOrSingleIdent (false, (LongIdentWithDots(longId, _)), _, _) ->

            let ad = env.eAccessRights
            let result = defaultArg resultOpt (List.last longId)

            // Demangle back to source operator name if the lengths in the ranges indicate the
            // original source range matches exactly
            let result =
                if IsMangledOpName result.idText then
                    let demangled = DecompileOpName result.idText
                    if demangled.Length = result.idRange.EndColumn - result.idRange.StartColumn then
                        ident(demangled, result.idRange)
                    else result
                else result

            // Nameof resolution resolves to a symbol and in general we make that the same symbol as
            // would resolve if the long ident was used as an expression at the given location.
            //
            // So we first check if the first identifier resolves as an expression, if so commit and and resolve.
            //
            // However we don't commit for a type names - nameof allows 'naked' type names and thus all type name
            // resolutions are checked separately in the next step.
            let typeNameResInfo = GetLongIdentTypeNameInfo delayed
            let nameResolutionResult = ResolveLongIdentAsExprAndComputeRange cenv.tcSink cenv.nameResolver (rangeOfLid longId) ad env.eNameResEnv typeNameResInfo longId
            let resolvesAsExpr =
                match nameResolutionResult with
                | Result ((_, item, _, _, _) as res)
                    when
                         (match item with
                          | Item.Types _
                          | Item.DelegateCtor _
                          | Item.CtorGroup _
                          | Item.FakeInterfaceCtor _ -> false
                          | _ -> true) ->
                    let overallTy = match overallTyOpt with None -> NewInferenceType() | Some t -> t
                    let _, _ = TcItemThen cenv overallTy env tpenv res delayed
                    true
                | _ ->
                    false
            if resolvesAsExpr then result else

            // If it's not an expression then try to resolve it as a type name
            let resolvedToTypeName =
                if (match delayed with [DelayedTypeApp _] | [] -> true | _ -> false) then
                    let (TypeNameResolutionInfo(_, staticArgsInfo)) = GetLongIdentTypeNameInfo delayed
                    match ResolveTypeLongIdent cenv.tcSink cenv.nameResolver ItemOccurence.UseInAttribute OpenQualified env.eNameResEnv ad longId staticArgsInfo PermitDirectReferenceToGeneratedType.No with
                    | Result (tinstEnclosing, tcref) when IsEntityAccessible cenv.amap m ad tcref ->
                        match delayed with
                        | [DelayedTypeApp (tyargs, _, mExprAndTypeArgs)] ->
                            TcTypeApp cenv NewTyparsOK CheckCxs ItemOccurence.UseInType env tpenv mExprAndTypeArgs tcref tinstEnclosing tyargs |> ignore
                        | _ -> ()
                        true // resolved to a type name, done with checks
                    | _ ->
                        false
                else
                    false
            if resolvedToTypeName then result else

            // If it's not an expression or type name then resolve it as a module
            let resolvedToModuleOrNamespaceName =
                if delayed.IsEmpty then
                    let id,rest = List.headAndTail longId
                    match ResolveLongIdentAsModuleOrNamespace cenv.tcSink ResultCollectionSettings.AllResults cenv.amap m true OpenQualified env.eNameResEnv ad id rest true with
                    | Result modref when delayed.IsEmpty && modref |> List.exists (p23 >> IsEntityAccessible cenv.amap m ad) ->
                        true // resolved to a module or namespace, done with checks
                    | _ ->
                        false
                else
                    false
            if resolvedToModuleOrNamespaceName then result else

            ForceRaise nameResolutionResult |> ignore
            // If that didn't give aan exception then raise a generic error
            error (Error(FSComp.SR.expressionHasNoName(), m))

        // expr<tyargs> allowed, even with qualifications
        | SynExpr.TypeApp (hd, _, types, _, _, _, m) ->
            check overallTyOpt resultOpt hd (DelayedTypeApp(types, m, m) :: delayed)

        // expr.ID allowed
        | SynExpr.DotGet (hd, _, LongIdentWithDots(longId, _), _) ->
            let result = defaultArg resultOpt (List.last longId)
            check overallTyOpt (Some result) hd ((DelayedDotLookup (longId, expr.RangeWithoutAnyExtraDot)) :: delayed)

        // "(expr)" allowed with no subsequent qualifications
        | SynExpr.Paren(expr, _, _, _) when delayed.IsEmpty && overallTyOpt.IsNone ->
            check overallTyOpt resultOpt expr delayed

        // expr : type" allowed with no subsequent qualifications
        | SynExpr.Typed (synBodyExpr, synType, _m) when delayed.IsEmpty && overallTyOpt.IsNone ->
            let tgtTy, _tpenv = TcTypeAndRecover cenv NewTyparsOK CheckCxs ItemOccurence.UseInType  env tpenv synType
            check (Some tgtTy) resultOpt synBodyExpr delayed

        | _ ->
            error (Error(FSComp.SR.expressionHasNoName(), m))

    let lastIdent = check None None cleanSynArg []
    TcNameOfExprResult cenv lastIdent m

and TcNameOfExprResult cenv (lastIdent: Ident) m =
    let constRange = mkRange m.FileName m.Start (mkPos m.StartLine (m.StartColumn + lastIdent.idText.Length + 2)) // `2` are for quotes
    Expr.Const(Const.String(lastIdent.idText), constRange, cenv.g.string_ty)

//-------------------------------------------------------------------------
// TcFunctionApplicationThen: Typecheck "expr x" + projections
//-------------------------------------------------------------------------

and TcFunctionApplicationThen cenv overallTy env tpenv mExprAndArg expr exprty (synArg: SynExpr) atomicFlag delayed =
    let denv = env.DisplayEnv
    let mArg = synArg.Range
    let mFunExpr = expr.Range

    // If the type of 'synArg' unifies as a function type, then this is a function application, otherwise
    // it is an error or a computation expression
    match UnifyFunctionTypeUndoIfFailed cenv denv mFunExpr exprty with
    | ValueSome (domainTy, resultTy) ->
        match expr with
        | ApplicableExpr(_, NameOfExpr cenv.g _, _) when cenv.g.langVersion.SupportsFeature LanguageFeature.NameOf ->
            let replacementExpr = TcNameOfExpr cenv env tpenv synArg
            TcDelayed cenv overallTy env tpenv mExprAndArg (ApplicableExpr(cenv, replacementExpr, true)) cenv.g.string_ty ExprAtomicFlag.Atomic delayed
        | _ ->
            // Notice the special case 'seq { ... }'. In this case 'seq' is actually a function in the F# library.
            // Set a flag in the syntax tree to say we noticed a leading 'seq'
            match synArg with
            | SynExpr.CompExpr (false, isNotNakedRefCell, _comp, _m) ->
                isNotNakedRefCell :=
                    !isNotNakedRefCell
                    ||
                    (match expr with
                     | ApplicableExpr(_, Expr.Op(TOp.Coerce, _, [SeqExpr cenv.g], _), _) -> true
                     | _ -> false)
            | _ -> ()

            let arg, tpenv = TcExpr cenv domainTy env tpenv synArg
            let exprAndArg, resultTy = buildApp cenv expr resultTy arg mExprAndArg
            TcDelayed cenv overallTy env tpenv mExprAndArg exprAndArg resultTy atomicFlag delayed

    | ValueNone ->
        // OK, 'expr' doesn't have function type, but perhaps 'expr' is a computation expression builder, and 'arg' is '{ ... }'
        match synArg with
        | SynExpr.CompExpr (false, _isNotNakedRefCell, comp, _m) ->
            let bodyOfCompExpr, tpenv = cenv.TcComputationExpression cenv env overallTy tpenv (mFunExpr, expr.Expr, exprty, comp)
            TcDelayed cenv overallTy env tpenv mExprAndArg (MakeApplicableExprNoFlex cenv bodyOfCompExpr) (tyOfExpr cenv.g bodyOfCompExpr) ExprAtomicFlag.NonAtomic delayed
        | _ ->
            error (NotAFunction(denv, overallTy, mFunExpr, mArg))

//-------------------------------------------------------------------------
// TcLongIdentThen: Typecheck "A.B.C<D>.E.F ... " constructs
//-------------------------------------------------------------------------

and GetLongIdentTypeNameInfo delayed =
    // Given 'MyOverloadedType<int>.MySubType...' use the number of given type arguments to help
    // resolve type name lookup of 'MyOverloadedType'
    // Also determine if type names should resolve to Item.Types or Item.CtorGroup
    match delayed with
    | DelayedTypeApp (tyargs, _, _) :: (DelayedDot | DelayedDotLookup _) :: _ ->
        // cases like 'MyType<int>.Sth'
        TypeNameResolutionInfo(ResolveTypeNamesToTypeRefs, TypeNameResolutionStaticArgsInfo.FromTyArgs tyargs.Length)

    | DelayedTypeApp (tyargs, _, _) :: _ ->
        // Note, this also covers the case 'MyType<int>.' (without LValue_get), which is needed for VS (when typing)
        TypeNameResolutionInfo(ResolveTypeNamesToCtors, TypeNameResolutionStaticArgsInfo.FromTyArgs tyargs.Length)

    | _ ->
        TypeNameResolutionInfo.Default

and TcLongIdentThen cenv overallTy env tpenv (LongIdentWithDots(longId, _)) delayed =

    let ad = env.eAccessRights
    let typeNameResInfo = GetLongIdentTypeNameInfo delayed
    let nameResolutionResult =
        ResolveLongIdentAsExprAndComputeRange cenv.tcSink cenv.nameResolver (rangeOfLid longId) ad env.eNameResEnv typeNameResInfo longId
        |> ForceRaise
    TcItemThen cenv overallTy env tpenv nameResolutionResult delayed

//-------------------------------------------------------------------------
// Typecheck "item+projections"
//------------------------------------------------------------------------- *)
// mItem is the textual range covered by the long identifiers that make up the item
and TcItemThen cenv overallTy env tpenv (tinstEnclosing, item, mItem, rest, afterResolution) delayed =
    let g = cenv.g
    let delayed = delayRest rest mItem delayed
    let ad = env.eAccessRights
    match item with
    // x where x is a union case or active pattern result tag.
    | (Item.UnionCase _ | Item.ExnCase _ | Item.ActivePatternResult _) as item ->
        // ucaseAppTy is the type of the union constructor applied to its (optional) argument
        let ucaseAppTy = NewInferenceType ()
        let mkConstrApp, argTys, argNames =
          match item with
          | Item.ActivePatternResult(apinfo, _apOverallTy, n, _) ->
              let aparity = apinfo.Names.Length
              match aparity with
              | 0 | 1 ->
                  let mkConstrApp _mArgs = function [arg] -> arg | _ -> error(InternalError("ApplyUnionCaseOrExn", mItem))
                  mkConstrApp, [ucaseAppTy], [ for (s, m) in apinfo.ActiveTagsWithRanges -> mkSynId m s ]
              | _ ->
                  let ucref = mkChoiceCaseRef g mItem aparity n
                  let _, _, tinst, _ = FreshenTyconRef2 mItem ucref.TyconRef
                  let ucinfo = UnionCaseInfo (tinst, ucref)
                  ApplyUnionCaseOrExnTypes mItem cenv env ucaseAppTy (Item.UnionCase(ucinfo, false))
          | _ ->
              ApplyUnionCaseOrExnTypes mItem cenv env ucaseAppTy item
        let numArgTys = List.length argTys
        // Subsumption at data constructions if argument type is nominal prior to equations for any arguments or return types
        let flexes = argTys |> List.map (isTyparTy g >> not)

        let (|FittedArgs|_|) arg =
            match arg with
            | SynExprParen(SynExpr.Tuple (false, args, _, _), _, _, _)
            | SynExpr.Tuple (false, args, _, _) when numArgTys > 1 -> Some args
            | SynExprParen(arg, _, _, _)
            | arg when numArgTys = 1 -> Some [arg]
            | _ -> None

        match delayed with
        // This is where the constructor is applied to an argument
        | ((DelayedApp (atomicFlag, (FittedArgs args as origArg), mExprAndArg)) :: otherDelayed) ->
            // assert the overall result type if possible
            if isNil otherDelayed then
                UnifyTypes cenv env mExprAndArg overallTy ucaseAppTy

            let numArgs = List.length args
            UnionCaseOrExnCheck env numArgTys numArgs mExprAndArg

            // if we manage to get here - number of formal arguments = number of actual arguments
            // apply named parameters
            let args =
                // GetMethodArgs checks that no named parameters are located before positional
                let unnamedArgs, namedCallerArgs = GetMethodArgs origArg
                match namedCallerArgs with
                | [] ->
                    args
                | _ ->
                    let fittedArgs = Array.zeroCreate numArgTys

                    // first: put all positional arguments
                    let mutable currentIndex = 0
                    for arg in unnamedArgs do
                        fittedArgs.[currentIndex] <- arg
                        currentIndex <- currentIndex + 1

                    let SEEN_NAMED_ARGUMENT = -1

                    // dealing with named arguments is a bit tricky since prior to these changes we have an ambiguous situation:
                    // regular notation for named parameters Some(Value = 5) can mean either 1) create option<bool> with value - result of equality operation or 2) create option<int> using named arg syntax.
                    // so far we've used 1) so we cannot immediately switch to 2) since it will be a definite breaking change.

                    for (_, id, arg) in namedCallerArgs do
                        match argNames |> List.tryFindIndex (fun id2 -> id.idText = id2.idText) with
                        | Some i ->
                            if isNull(box fittedArgs.[i]) then
                                fittedArgs.[i] <- arg
                                let argItem =
                                    match item with
                                    | Item.UnionCase (uci, _) -> Item.UnionCaseField (uci, i)
                                    | Item.ExnCase tref -> Item.RecdField (RecdFieldInfo ([], RecdFieldRef (tref, id.idText)))
                                    | _ -> failwithf "Expecting union case or exception item, got: %O" item
                                CallNameResolutionSink cenv.tcSink (id.idRange, env.NameEnv, argItem, emptyTyparInst, ItemOccurence.Use, ad)
                            else error(Error(FSComp.SR.tcUnionCaseFieldCannotBeUsedMoreThanOnce(id.idText), id.idRange))
                            currentIndex <- SEEN_NAMED_ARGUMENT
                        | None ->
                            // ambiguity may appear only when if argument is boolean\generic.
                            // if
                            // - we didn't find argument with specified name AND
                            // - we have not seen any named arguments so far AND
                            // - type of current argument is bool\generic
                            // then we'll favor old behavior and treat current argument as positional.
                            let isSpecialCaseForBackwardCompatibility =
                                (currentIndex <> SEEN_NAMED_ARGUMENT) &&
                                (currentIndex < numArgTys) &&
                                match stripTyEqns g argTys.[currentIndex] with
                                | TType_app(tcref, _) -> tyconRefEq g g.bool_tcr tcref || tyconRefEq g g.system_Bool_tcref tcref
                                | TType_var(_) -> true
                                | _ -> false

                            if isSpecialCaseForBackwardCompatibility then
                                assert (isNull(box fittedArgs.[currentIndex]))
                                fittedArgs.[currentIndex] <- List.item currentIndex args // grab original argument, not item from the list of named parameters
                                currentIndex <- currentIndex + 1
                            else
                                match item with
                                | Item.UnionCase(uci, _) ->
                                    error(Error(FSComp.SR.tcUnionCaseConstructorDoesNotHaveFieldWithGivenName(uci.Name, id.idText), id.idRange))
                                | Item.ExnCase tcref ->
                                    error(Error(FSComp.SR.tcExceptionConstructorDoesNotHaveFieldWithGivenName(tcref.DisplayName, id.idText), id.idRange))
                                | Item.ActivePatternResult(_,_,_,_) ->
                                    error(Error(FSComp.SR.tcActivePatternsDoNotHaveFields(), id.idRange))
                                | _ ->
                                    error(Error(FSComp.SR.tcConstructorDoesNotHaveFieldWithGivenName(id.idText), id.idRange))

                    assert (Seq.forall (box >> ((<>) null) ) fittedArgs)
                    List.ofArray fittedArgs

            let args', tpenv = TcExprs cenv env mExprAndArg tpenv flexes argTys args
            PropagateThenTcDelayed cenv overallTy env tpenv mExprAndArg (MakeApplicableExprNoFlex cenv (mkConstrApp mExprAndArg args')) ucaseAppTy atomicFlag otherDelayed

        | DelayedTypeApp (_x, mTypeArgs, _mExprAndTypeArgs) :: _delayed' ->
            error(Error(FSComp.SR.tcUnexpectedTypeArguments(), mTypeArgs))
        | _ ->
            // Work out how many syntactic arguments we really expect. Also return a function that builds the overall
            // expression, but don't apply this function until after we've checked that the number of arguments is OK
            // (or else we would be building an invalid expression)

            // Unit-taking active pattern result can be applied to no args
            let numArgs, mkExpr =
                // This is where the constructor is an active pattern result applied to no argument
                // Unit-taking active pattern result can be applied to no args
                if (numArgTys = 1 && match item with Item.ActivePatternResult _ -> true | _ -> false) then
                    UnifyTypes cenv env mItem (List.head argTys) g.unit_ty
                    1, (fun () -> mkConstrApp mItem [mkUnit g mItem])

                // This is where the constructor expects no arguments and is applied to no argument
                elif numArgTys = 0 then
                    0, (fun () -> mkConstrApp mItem [])
                else
                    // This is where the constructor expects arguments but is not applied to arguments, hence build a lambda
                    numArgTys,
                    (fun () ->
                        let vs, args = argTys |> List.mapi (fun i ty -> mkCompGenLocal mItem ("arg" + string i) ty) |> List.unzip
                        let constrApp = mkConstrApp mItem args
                        let lam = mkMultiLambda mItem vs (constrApp, tyOfExpr g constrApp)
                        lam)
            UnionCaseOrExnCheck env numArgTys numArgs mItem
            let expr = mkExpr()
            let exprTy = tyOfExpr g expr
            PropagateThenTcDelayed cenv overallTy env tpenv mItem (MakeApplicableExprNoFlex cenv expr) exprTy ExprAtomicFlag.Atomic delayed

    | Item.Types(nm, (ty :: _)) ->

        match delayed with
        | ((DelayedTypeApp(tyargs, _mTypeArgs, mExprAndTypeArgs)) :: (DelayedDotLookup (longId, mLongId)) :: otherDelayed) ->
            // If Item.Types is returned then the ty will be of the form TType_app(tcref, genericTyargs) where tyargs
            // is a fresh instantiation for tcref. TcNestedTypeApplication will chop off precisely #genericTyargs args
            // and replace them by 'tyargs'
            let ty, tpenv = TcNestedTypeApplication cenv NewTyparsOK CheckCxs ItemOccurence.UseInType env tpenv mExprAndTypeArgs ty tinstEnclosing tyargs

            // Report information about the whole expression including type arguments to VS
            let item = Item.Types(nm, [ty])
            CallNameResolutionSink cenv.tcSink (mExprAndTypeArgs, env.NameEnv, item, emptyTyparInst, ItemOccurence.Use, env.eAccessRights)
            let typeNameResInfo = GetLongIdentTypeNameInfo otherDelayed
            let item, mItem, rest, afterResolution = ResolveExprDotLongIdentAndComputeRange cenv.tcSink cenv.nameResolver (unionRanges mExprAndTypeArgs mLongId) ad env.eNameResEnv ty longId typeNameResInfo IgnoreOverrides true
            TcItemThen cenv overallTy env tpenv ((argsOfAppTy g ty), item, mItem, rest, afterResolution) otherDelayed

        | ((DelayedTypeApp(tyargs, _mTypeArgs, mExprAndTypeArgs)) :: _delayed') ->
            // A case where we have an incomplete name e.g. 'Foo<int>.' - we still want to report it to VS!
            let ty, _ = TcNestedTypeApplication cenv NewTyparsOK CheckCxs ItemOccurence.UseInType env tpenv mExprAndTypeArgs ty tinstEnclosing tyargs
            let item = Item.Types(nm, [ty])
            CallNameResolutionSink cenv.tcSink (mExprAndTypeArgs, env.NameEnv, item, emptyTyparInst, ItemOccurence.Use, env.eAccessRights)

            // Same error as in the following case
            error(Error(FSComp.SR.tcInvalidUseOfTypeName(), mItem))

        | _ ->
            // In this case the type is not generic, and indeed we should never have returned Item.Types.
            // That's because ResolveTypeNamesToCtors should have been set at the original
            // call to ResolveLongIdentAsExprAndComputeRange
            error(Error(FSComp.SR.tcInvalidUseOfTypeName(), mItem))

    | Item.MethodGroup (methodName, minfos, _) ->
        // Static method calls Type.Foo(arg1, ..., argn)
        let meths = List.map (fun minfo -> minfo, None) minfos
        match delayed with
        | (DelayedApp (atomicFlag, arg, mExprAndArg) :: otherDelayed) ->
            TcMethodApplicationThen cenv env overallTy None tpenv None [] mExprAndArg mItem methodName ad NeverMutates false meths afterResolution NormalValUse [arg] atomicFlag otherDelayed

        | (DelayedTypeApp(tys, mTypeArgs, mExprAndTypeArgs) :: otherDelayed) ->

#if !NO_EXTENSIONTYPING
            match TryTcMethodAppToStaticConstantArgs cenv env tpenv (minfos, Some (tys, mTypeArgs), mExprAndTypeArgs, mItem) with
            | Some minfoAfterStaticArguments ->

              // Replace the resolution including the static parameters, plus the extra information about the original method info
              let item = Item.MethodGroup(methodName, [minfoAfterStaticArguments], Some minfos.[0])
              CallNameResolutionSinkReplacing cenv.tcSink (mItem, env.NameEnv, item, [], ItemOccurence.Use, env.eAccessRights)

              match otherDelayed with
              | DelayedApp(atomicFlag, arg, mExprAndArg) :: otherDelayed ->
                  TcMethodApplicationThen cenv env overallTy None tpenv None [] mExprAndArg mItem methodName ad NeverMutates false [(minfoAfterStaticArguments, None)] afterResolution NormalValUse [arg] atomicFlag otherDelayed
              | _ ->
                  TcMethodApplicationThen cenv env overallTy None tpenv None [] mExprAndTypeArgs mItem methodName ad NeverMutates false [(minfoAfterStaticArguments, None)] afterResolution NormalValUse [] ExprAtomicFlag.Atomic otherDelayed

            | None ->
#endif

            let tyargs, tpenv = TcTypesOrMeasures None cenv NewTyparsOK CheckCxs ItemOccurence.UseInType env tpenv tys mTypeArgs

            // FUTURE: can we do better than emptyTyparInst here, in order to display instantiations
            // of type variables in the quick info provided in the IDE? But note we haven't yet even checked if the
            // number of type arguments is correct...
            CallNameResolutionSink cenv.tcSink (mExprAndTypeArgs, env.NameEnv, item, emptyTyparInst, ItemOccurence.Use, env.eAccessRights)

            match otherDelayed with
            | DelayedApp(atomicFlag, arg, mExprAndArg) :: otherDelayed ->
                TcMethodApplicationThen cenv env overallTy None tpenv (Some tyargs) [] mExprAndArg mItem methodName ad NeverMutates false meths afterResolution NormalValUse [arg] atomicFlag otherDelayed
            | _ ->
                TcMethodApplicationThen cenv env overallTy None tpenv (Some tyargs) [] mExprAndTypeArgs mItem methodName ad NeverMutates false meths afterResolution NormalValUse [] ExprAtomicFlag.Atomic otherDelayed

        | _ ->
#if !NO_EXTENSIONTYPING
            if not minfos.IsEmpty && minfos.[0].ProvidedStaticParameterInfo.IsSome then
                error(Error(FSComp.SR.etMissingStaticArgumentsToMethod(), mItem))
#endif
            TcMethodApplicationThen cenv env overallTy None tpenv None [] mItem mItem methodName ad NeverMutates false meths afterResolution NormalValUse [] ExprAtomicFlag.Atomic delayed

    | Item.CtorGroup(nm, minfos) ->
        let objTy =
            match minfos with
            | (minfo :: _) -> minfo.ApparentEnclosingType
            | [] -> error(Error(FSComp.SR.tcTypeHasNoAccessibleConstructor(), mItem))
        match delayed with
        | ((DelayedApp (_, arg, mExprAndArg)) :: otherDelayed) ->

            CallExprHasTypeSink cenv.tcSink (mExprAndArg, env.NameEnv, objTy, env.eAccessRights)
            TcCtorCall true cenv env tpenv overallTy objTy (Some mItem) item false [arg] mExprAndArg otherDelayed (Some afterResolution)

        | ((DelayedTypeApp(tyargs, _mTypeArgs, mExprAndTypeArgs)) :: (DelayedApp (_, arg, mExprAndArg)) :: otherDelayed) ->

            let objTyAfterTyArgs, tpenv = TcNestedTypeApplication cenv NewTyparsOK CheckCxs ItemOccurence.UseInType env tpenv mExprAndTypeArgs objTy tinstEnclosing tyargs
            CallExprHasTypeSink cenv.tcSink (mExprAndArg, env.NameEnv, objTyAfterTyArgs, env.eAccessRights)
            let itemAfterTyArgs, minfosAfterTyArgs =
#if !NO_EXTENSIONTYPING
                // If the type is provided and took static arguments then the constructor will have changed
                // to a provided constructor on the statically instantiated type. Re-resolve that constructor.
                match objTyAfterTyArgs with
                | AppTy g (tcref, _) when tcref.Deref.IsProvided ->
                    let newItem = ForceRaise (ResolveObjectConstructor cenv.nameResolver env.DisplayEnv mExprAndArg ad objTyAfterTyArgs)
                    match newItem with
                    | Item.CtorGroup(_, newMinfos) -> newItem, newMinfos
                    | _ -> item, minfos
                | _ ->
#endif
                    item, minfos

            minfosAfterTyArgs |> List.iter (fun minfo -> UnifyTypes cenv env mExprAndTypeArgs minfo.ApparentEnclosingType objTyAfterTyArgs)
            TcCtorCall true cenv env tpenv overallTy objTyAfterTyArgs (Some mExprAndTypeArgs) itemAfterTyArgs false [arg] mExprAndArg otherDelayed (Some afterResolution)

        | ((DelayedTypeApp(tyargs, _mTypeArgs, mExprAndTypeArgs)) :: otherDelayed) ->

            let objTy, tpenv = TcNestedTypeApplication cenv NewTyparsOK CheckCxs ItemOccurence.UseInType env tpenv mExprAndTypeArgs objTy tinstEnclosing tyargs

            // A case where we have an incomplete name e.g. 'Foo<int>.' - we still want to report it to VS!
            let resolvedItem = Item.Types(nm, [objTy])
            CallNameResolutionSink cenv.tcSink (mExprAndTypeArgs, env.NameEnv, resolvedItem, emptyTyparInst, ItemOccurence.Use, env.eAccessRights)

            minfos |> List.iter (fun minfo -> UnifyTypes cenv env mExprAndTypeArgs minfo.ApparentEnclosingType objTy)
            TcCtorCall true cenv env tpenv overallTy objTy (Some mExprAndTypeArgs) item false [] mExprAndTypeArgs otherDelayed (Some afterResolution)

        | _ ->

            TcCtorCall true cenv env tpenv overallTy objTy (Some mItem) item false [] mItem delayed (Some afterResolution)

    | Item.FakeInterfaceCtor _ ->
        error(Error(FSComp.SR.tcInvalidUseOfInterfaceType(), mItem))

    | Item.ImplicitOp(id, sln) ->

        let isPrefix = PrettyNaming.IsPrefixOperator id.idText
        let isTernary = PrettyNaming.IsTernaryOperator id.idText

        let argData =
            if isPrefix then
                [ SynTypar(mkSynId mItem (cenv.synArgNameGenerator.New()), TyparStaticReq.HeadType, true) ]
            elif isTernary then
                [ SynTypar(mkSynId mItem (cenv.synArgNameGenerator.New()), TyparStaticReq.HeadType, true)
                  SynTypar(mkSynId mItem (cenv.synArgNameGenerator.New()), TyparStaticReq.HeadType, true)
                  SynTypar(mkSynId mItem (cenv.synArgNameGenerator.New()), TyparStaticReq.HeadType, true) ]
            else
                [ SynTypar(mkSynId mItem (cenv.synArgNameGenerator.New()), TyparStaticReq.HeadType, true)
                  SynTypar(mkSynId mItem (cenv.synArgNameGenerator.New()), TyparStaticReq.HeadType, true) ]

        let retTyData = SynTypar(mkSynId mItem (cenv.synArgNameGenerator.New()), TyparStaticReq.HeadType, true)
        let argTypars = argData |> List.map (fun d -> Construct.NewTypar (TyparKind.Type, TyparRigidity.Flexible, d, false, TyparDynamicReq.Yes, [], false, false))
        let retTypar = Construct.NewTypar (TyparKind.Type, TyparRigidity.Flexible, retTyData, false, TyparDynamicReq.Yes, [], false, false)
        let argTys = argTypars |> List.map mkTyparTy
        let retTy = mkTyparTy retTypar

        let vs, ves = argTys |> List.mapi (fun i ty -> mkCompGenLocal mItem ("arg" + string i) ty) |> List.unzip

        let memberFlags = StaticMemberFlags SynMemberKind.Member
        let logicalCompiledName = ComputeLogicalName id memberFlags
        let traitInfo = TTrait(argTys, logicalCompiledName, memberFlags, argTys, Some retTy, sln)

        let expr = Expr.Op (TOp.TraitCall traitInfo, [], ves, mItem)
        let expr = mkLambdas mItem [] vs (expr, retTy)

        let rec isSimpleArgument e =
            match e with
            | SynExpr.New (_, _, synExpr, _)
            | SynExpr.Paren (synExpr, _, _, _)
            | SynExpr.Typed (synExpr, _, _)
            | SynExpr.TypeApp (synExpr, _, _, _, _, _, _)
            | SynExpr.TypeTest (synExpr, _, _)
            | SynExpr.Upcast (synExpr, _, _)
            | SynExpr.DotGet (synExpr, _, _, _)
            | SynExpr.Downcast (synExpr, _, _)
            | SynExpr.InferredUpcast (synExpr, _)
            | SynExpr.InferredDowncast (synExpr, _)
            | SynExpr.AddressOf (_, synExpr, _, _)
            | SynExpr.Quote (_, _, synExpr, _, _) -> isSimpleArgument synExpr

            | SynExpr.InterpolatedString _
            | SynExpr.Null _
            | SynExpr.Ident _
            | SynExpr.Const _
            | SynExpr.LongIdent _ -> true

            | SynExpr.Tuple (_, synExprs, _, _)
            | SynExpr.ArrayOrList (_, synExprs, _) -> synExprs |> List.forall isSimpleArgument
            | SynExpr.Record (_, copyOpt, fields, _) -> copyOpt |> Option.forall (fst >> isSimpleArgument) && fields |> List.forall (p23 >> Option.forall isSimpleArgument)
            | SynExpr.App (_, _, synExpr, synExpr2, _) -> isSimpleArgument synExpr && isSimpleArgument synExpr2
            | SynExpr.IfThenElse (_, synExpr, _, synExpr2, _, synExprOpt, _, _, _, _) -> isSimpleArgument synExpr && isSimpleArgument synExpr2 && Option.forall isSimpleArgument synExprOpt
            | SynExpr.DotIndexedGet (synExpr, _, _, _) -> isSimpleArgument synExpr
            | SynExpr.ObjExpr _
            | SynExpr.AnonRecd _
            | SynExpr.While _
            | SynExpr.For _
            | SynExpr.ForEach _
            | SynExpr.ArrayOrListOfSeqExpr _
            | SynExpr.CompExpr _
            | SynExpr.Lambda _
            | SynExpr.MatchLambda _
            | SynExpr.Match _
            | SynExpr.Do _
            | SynExpr.Assert _
            | SynExpr.Fixed _
            | SynExpr.TryWith _
            | SynExpr.TryFinally _
            | SynExpr.Lazy _
            | SynExpr.Sequential _
            | SynExpr.SequentialOrImplicitYield _
            | SynExpr.LetOrUse _
            | SynExpr.DotSet _
            | SynExpr.DotIndexedSet _
            | SynExpr.LongIdentSet _
            | SynExpr.Set _
            | SynExpr.JoinIn _
            | SynExpr.NamedIndexedPropertySet _
            | SynExpr.DotNamedIndexedPropertySet _
            | SynExpr.LibraryOnlyILAssembly _
            | SynExpr.LibraryOnlyStaticOptimization _
            | SynExpr.LibraryOnlyUnionCaseFieldGet _
            | SynExpr.LibraryOnlyUnionCaseFieldSet _
            | SynExpr.ArbitraryAfterError (_, _)
            | SynExpr.FromParseError (_, _)
            | SynExpr.DiscardAfterMissingQualificationAfterDot (_, _)
            | SynExpr.ImplicitZero _
            | SynExpr.YieldOrReturn _
            | SynExpr.YieldOrReturnFrom _
            | SynExpr.MatchBang _
            | SynExpr.LetOrUseBang _
            | SynExpr.DoBang _
            | SynExpr.TraitCall _
                -> false


        // Propagate the known application structure into function types
        Propagate cenv overallTy env tpenv (MakeApplicableExprNoFlex cenv expr) (tyOfExpr g expr) delayed

        // Take all simple arguments and process them before applying the constraint.
        let delayed1, delayed2 =
            let pred = (function (DelayedApp (_, arg, _)) -> isSimpleArgument arg | _ -> false)
            List.takeWhile pred delayed, List.skipWhile pred delayed
        let intermediateTy = if isNil delayed2 then overallTy else NewInferenceType ()

        let resultExpr, tpenv = TcDelayed cenv intermediateTy env tpenv mItem (MakeApplicableExprNoFlex cenv expr) (tyOfExpr g expr) ExprAtomicFlag.NonAtomic delayed1

        // Add the constraint after the application arguments have been checked to allow annotations to kick in on rigid type parameters
        AddCxMethodConstraint env.DisplayEnv cenv.css mItem NoTrace traitInfo

        // Process all remaining arguments after the constraint is asserted
        let resultExpr2, tpenv2 = TcDelayed cenv overallTy env tpenv mItem (MakeApplicableExprNoFlex cenv resultExpr) intermediateTy ExprAtomicFlag.NonAtomic delayed2
        resultExpr2, tpenv2


    | Item.DelegateCtor ty ->
        match delayed with
        | ((DelayedApp (atomicFlag, arg, mItemAndArg)) :: otherDelayed) ->
            TcNewDelegateThen cenv overallTy env tpenv mItem mItemAndArg ty arg atomicFlag otherDelayed
        | ((DelayedTypeApp(tyargs, _mTypeArgs, mItemAndTypeArgs)) :: (DelayedApp (atomicFlag, arg, mItemAndArg)) :: otherDelayed) ->
            let ty, tpenv = TcNestedTypeApplication cenv NewTyparsOK CheckCxs ItemOccurence.UseInType env tpenv mItemAndTypeArgs ty tinstEnclosing tyargs

            // Report information about the whole expression including type arguments to VS
            let item = Item.DelegateCtor ty
            CallNameResolutionSink cenv.tcSink (mItemAndTypeArgs, env.NameEnv, item, emptyTyparInst, ItemOccurence.Use, env.eAccessRights)
            TcNewDelegateThen cenv overallTy env tpenv mItem mItemAndArg ty arg atomicFlag otherDelayed
        | _ ->
            error(Error(FSComp.SR.tcInvalidUseOfDelegate(), mItem))

    | Item.Value vref ->

        match delayed with
        // Mutable value set: 'v <- e'
        | DelayedSet(e2, mStmt) :: otherDelayed ->
            if not (isNil otherDelayed) then error(Error(FSComp.SR.tcInvalidAssignment(), mStmt))
            UnifyTypes cenv env mStmt overallTy g.unit_ty
            vref.Deref.SetHasBeenReferenced()
            CheckValAccessible mItem env.AccessRights vref
            CheckValAttributes g vref mItem |> CommitOperationResult
            let vty = vref.Type
            let vty2 =
                if isByrefTy g vty then
                    destByrefTy g vty
                else
                    if not vref.IsMutable then
                        errorR (ValNotMutable (env.DisplayEnv, vref, mStmt))
                    vty
            // Always allow subsumption on assignment to fields
            let e2', tpenv = TcExprFlex cenv true false vty2 env tpenv e2
            let vexp =
                if isInByrefTy g vty then
                    errorR(Error(FSComp.SR.writeToReadOnlyByref(), mStmt))
                    mkAddrSet mStmt vref e2'
                elif isByrefTy g vty then
                    mkAddrSet mStmt vref e2'
                else
                    mkValSet mStmt vref e2'

            PropagateThenTcDelayed cenv overallTy env tpenv mStmt (MakeApplicableExprNoFlex cenv vexp) (tyOfExpr g vexp) ExprAtomicFlag.NonAtomic otherDelayed

        // Value instantiation: v<tyargs> ...
        | (DelayedTypeApp(tys, _mTypeArgs, mExprAndTypeArgs) :: otherDelayed) ->
            // Note: we know this is a NormalValUse or PossibleConstrainedCall because:
            //   - it isn't a CtorValUsedAsSuperInit
            //   - it isn't a CtorValUsedAsSelfInit
            //   - it isn't a VSlotDirectCall (uses of base values do not take type arguments
            // Allow `nameof<'T>` for a generic parameter
            match vref with
            | _ when isNameOfValRef cenv.g vref && cenv.g.langVersion.SupportsFeature LanguageFeature.NameOf ->
                match tys with
                | [SynType.Var((SynTypar(id, _, false) as tp), _m)] ->
                    let _tp', tpenv = TcTyparOrMeasurePar None cenv env ImplicitlyBoundTyparsAllowed.NoNewTypars tpenv tp
                    let vexp = TcNameOfExprResult cenv id mExprAndTypeArgs
                    let vexpFlex = MakeApplicableExprNoFlex cenv vexp
                    PropagateThenTcDelayed cenv overallTy env tpenv mExprAndTypeArgs vexpFlex cenv.g.string_ty ExprAtomicFlag.Atomic otherDelayed
                | _ ->
                    error (Error(FSComp.SR.expressionHasNoName(), mExprAndTypeArgs))
            | _ ->
            let checkTys tpenv kinds = TcTypesOrMeasures (Some kinds) cenv NewTyparsOK CheckCxs ItemOccurence.UseInType env tpenv tys mItem
            let _, vexp, isSpecial, _, _, tpenv = TcVal true cenv env tpenv vref (Some (NormalValUse, checkTys)) (Some afterResolution) mItem

            let vexpFlex = (if isSpecial then MakeApplicableExprNoFlex cenv vexp else MakeApplicableExprWithFlex cenv env vexp)
            // We need to eventually record the type resolution for an expression, but this is done
            // inside PropagateThenTcDelayed, so we don't have to explicitly call 'CallExprHasTypeSink' here
            PropagateThenTcDelayed cenv overallTy env tpenv mExprAndTypeArgs vexpFlex vexpFlex.Type ExprAtomicFlag.Atomic otherDelayed

        // Value get
        | _ ->
            let _, vexp, isSpecial, _, _, tpenv = TcVal true cenv env tpenv vref None (Some afterResolution) mItem
            let vexpFlex = (if isSpecial then MakeApplicableExprNoFlex cenv vexp else MakeApplicableExprWithFlex cenv env vexp)
            PropagateThenTcDelayed cenv overallTy env tpenv mItem vexpFlex vexpFlex.Type ExprAtomicFlag.Atomic delayed

    | Item.Property (nm, pinfos) ->
        if isNil pinfos then error (InternalError ("Unexpected error: empty property list", mItem))
        // if there are both intrinsics and extensions in pinfos, intrinsics will be listed first.
        // by looking at List.Head we are letting the intrinsics determine indexed/non-indexed
        let pinfo = List.head pinfos
        let _, tyargsOpt, args, delayed, tpenv =
            if pinfo.IsIndexer
            then GetMemberApplicationArgs delayed cenv env tpenv
            else ExprAtomicFlag.Atomic, None, [mkSynUnit mItem], delayed, tpenv
        if not pinfo.IsStatic then error (Error (FSComp.SR.tcPropertyIsNotStatic nm, mItem))
        match delayed with
        | DelayedSet(e2, mStmt) :: otherDelayed ->
            if not (isNil otherDelayed) then error(Error(FSComp.SR.tcInvalidAssignment(), mStmt))
            // Static Property Set (possibly indexer)
            UnifyTypes cenv env mStmt overallTy g.unit_ty
            let meths = pinfos |> SettersOfPropInfos
            if meths.IsEmpty then
                let meths = pinfos |> GettersOfPropInfos
                let isByrefMethReturnSetter = meths |> List.exists (function (_,Some pinfo) -> isByrefTy g (pinfo.GetPropertyType(cenv.amap,mItem)) | _ -> false)
                if not isByrefMethReturnSetter then
                    errorR (Error (FSComp.SR.tcPropertyCannotBeSet1 nm, mItem))
                // x.P <- ... byref setter
                if isNil meths then error (Error (FSComp.SR.tcPropertyIsNotReadable nm, mItem))
                TcMethodApplicationThen cenv env overallTy None tpenv tyargsOpt [] mItem mItem nm ad NeverMutates true meths afterResolution NormalValUse args ExprAtomicFlag.Atomic delayed
            else
                let args = if pinfo.IsIndexer then args else []
                if isNil meths then
                    errorR (Error (FSComp.SR.tcPropertyCannotBeSet1 nm, mItem))
                // Note: static calls never mutate a struct object argument
                TcMethodApplicationThen cenv env overallTy None tpenv tyargsOpt [] mStmt mItem nm ad NeverMutates true meths afterResolution NormalValUse (args@[e2]) ExprAtomicFlag.NonAtomic otherDelayed
        | _ ->
            // Static Property Get (possibly indexer)
            let meths = pinfos |> GettersOfPropInfos
            if isNil meths then error (Error (FSComp.SR.tcPropertyIsNotReadable nm, mItem))
            // Note: static calls never mutate a struct object argument
            TcMethodApplicationThen cenv env overallTy None tpenv tyargsOpt [] mItem mItem nm ad NeverMutates true meths afterResolution NormalValUse args ExprAtomicFlag.Atomic delayed

    | Item.ILField finfo ->

        ILFieldStaticChecks g cenv.amap cenv.infoReader ad mItem finfo
        let fref = finfo.ILFieldRef
        let exprty = finfo.FieldType(cenv.amap, mItem)
        match delayed with
        | DelayedSet(e2, mStmt) :: _delayed' ->
            UnifyTypes cenv env mStmt overallTy g.unit_ty
            // Always allow subsumption on assignment to fields
            let e2', tpenv = TcExprFlex cenv true false exprty env tpenv e2
            let expr = BuildILStaticFieldSet mStmt finfo e2'
            expr, tpenv
        | _ ->
           // Get static IL field
            let expr =
              match finfo.LiteralValue with
              | Some lit ->
                  Expr.Const (TcFieldInit mItem lit, mItem, exprty)
              | None ->
                let isValueType = finfo.IsValueType
                let valu = if isValueType then AsValue else AsObject

                // The empty instantiation on the fspec is OK, since we make the correct fspec in IlxGen.GenAsm
                // This ensures we always get the type instantiation right when doing this from
                // polymorphic code, after inlining etc.
                let fspec = mkILFieldSpec(fref, mkILNamedTy valu fref.DeclaringTypeRef [])

                // Add an I_nop if this is an initonly field to make sure we never recognize it as an lvalue. See mkExprAddrOfExpr.
                mkAsmExpr ([ mkNormalLdsfld fspec ] @ (if finfo.IsInitOnly then [ AI_nop ] else []), finfo.TypeInst, [], [exprty], mItem)
            PropagateThenTcDelayed cenv overallTy env tpenv mItem (MakeApplicableExprWithFlex cenv env expr) exprty ExprAtomicFlag.Atomic delayed

    | Item.RecdField rfinfo ->
        // Get static F# field or literal
        CheckRecdFieldInfoAccessible cenv.amap mItem ad rfinfo
        if not rfinfo.IsStatic then error (Error (FSComp.SR.tcFieldIsNotStatic(rfinfo.Name), mItem))
        CheckRecdFieldInfoAttributes g rfinfo mItem |> CommitOperationResult
        let fref = rfinfo.RecdFieldRef
        let fieldTy = rfinfo.FieldType
        match delayed with
        | DelayedSet(e2, mStmt) :: otherDelayed ->
            if not (isNil otherDelayed) then error(Error(FSComp.SR.tcInvalidAssignment(), mStmt))

            // Set static F# field
            CheckRecdFieldMutation mItem env.DisplayEnv rfinfo
            UnifyTypes cenv env mStmt overallTy g.unit_ty
            let fieldTy = rfinfo.FieldType
            // Always allow subsumption on assignment to fields
            let e2', tpenv = TcExprFlex cenv true false fieldTy env tpenv e2
            let expr = mkStaticRecdFieldSet (rfinfo.RecdFieldRef, rfinfo.TypeInst, e2', mStmt)
            expr, tpenv
        | _ ->
            let exprty = fieldTy
            let expr =
              match rfinfo.LiteralValue with
              // Get literal F# field
              | Some lit -> Expr.Const (lit, mItem, exprty)
              // Get static F# field
              | None -> mkStaticRecdFieldGet (fref, rfinfo.TypeInst, mItem)
            PropagateThenTcDelayed cenv overallTy env tpenv mItem (MakeApplicableExprWithFlex cenv env expr) exprty ExprAtomicFlag.Atomic delayed

    | Item.Event einfo ->
        // Instance IL event (fake up event-as-value)
        TcEventValueThen cenv overallTy env tpenv mItem mItem None einfo delayed

    | Item.CustomOperation (nm, usageTextOpt, _) ->
        // 'delayed' is about to be dropped on the floor, first do rudimentary checking to get name resolutions in its body
        RecordNameAndTypeResolutions_IdeallyWithoutHavingOtherEffects_Delayed cenv env tpenv delayed
        match usageTextOpt() with
        | None -> error(Error(FSComp.SR.tcCustomOperationNotUsedCorrectly nm, mItem))
        | Some usageText -> error(Error(FSComp.SR.tcCustomOperationNotUsedCorrectly2(nm, usageText), mItem))
    | _ -> error(Error(FSComp.SR.tcLookupMayNotBeUsedHere(), mItem))


//-------------------------------------------------------------------------
// Typecheck "expr.A.B.C ... " constructs
//-------------------------------------------------------------------------

and GetSynMemberApplicationArgs delayed tpenv =
    match delayed with
    | DelayedApp (atomicFlag, arg, _) :: otherDelayed ->
        atomicFlag, None, [arg], otherDelayed, tpenv
    | DelayedTypeApp(tyargs, mTypeArgs, _) :: DelayedApp (atomicFlag, arg, _mExprAndArg) :: otherDelayed ->
        (atomicFlag, Some (tyargs, mTypeArgs), [arg], otherDelayed, tpenv)
    | DelayedTypeApp(tyargs, mTypeArgs, _) :: otherDelayed ->
        (ExprAtomicFlag.Atomic, Some (tyargs, mTypeArgs), [], otherDelayed, tpenv)
    | otherDelayed ->
        (ExprAtomicFlag.NonAtomic, None, [], otherDelayed, tpenv)


and TcMemberTyArgsOpt cenv env tpenv tyargsOpt =
    match tyargsOpt with
    | None -> None, tpenv
    | Some (tyargs, mTypeArgs) ->
        let tyargsChecked, tpenv = TcTypesOrMeasures None cenv NewTyparsOK CheckCxs ItemOccurence.UseInType env tpenv tyargs mTypeArgs
        Some tyargsChecked, tpenv

and GetMemberApplicationArgs delayed cenv env tpenv =
    let atomicFlag, tyargsOpt, args, delayed, tpenv = GetSynMemberApplicationArgs delayed tpenv
    let tyArgsOptChecked, tpenv = TcMemberTyArgsOpt cenv env tpenv tyargsOpt
    atomicFlag, tyArgsOptChecked, args, delayed, tpenv

and TcLookupThen cenv overallTy env tpenv mObjExpr objExpr objExprTy longId delayed mExprAndLongId =
    let objArgs = [objExpr]
    let ad = env.eAccessRights

    // 'base' calls use a different resolution strategy when finding methods.
    let findFlag =
        let baseCall = IsBaseCall objArgs
        (if baseCall then PreferOverrides else IgnoreOverrides)

    // Canonicalize inference problem prior to '.' lookup on variable types
    if isTyparTy cenv.g objExprTy then
        ConstraintSolver.CanonicalizePartialInferenceProblem cenv.css env.DisplayEnv mExprAndLongId (freeInTypeLeftToRight cenv.g false objExprTy)

    let item, mItem, rest, afterResolution = ResolveExprDotLongIdentAndComputeRange cenv.tcSink cenv.nameResolver mExprAndLongId ad env.NameEnv objExprTy longId TypeNameResolutionInfo.Default findFlag false
    let mExprAndItem = unionRanges mObjExpr mItem
    let delayed = delayRest rest mExprAndItem delayed

    match item with
    | Item.MethodGroup (methodName, minfos, _) ->
        let atomicFlag, tyargsOpt, args, delayed, tpenv = GetSynMemberApplicationArgs delayed tpenv
        // We pass PossiblyMutates here because these may actually mutate a value type object
        // To get better warnings we special case some of the few known mutate-a-struct method names
        let mutates = (if methodName = "MoveNext" || methodName = "GetNextArg" then DefinitelyMutates else PossiblyMutates)

#if !NO_EXTENSIONTYPING
        match TryTcMethodAppToStaticConstantArgs cenv env tpenv (minfos, tyargsOpt, mExprAndItem, mItem) with
        | Some minfoAfterStaticArguments ->
            // Replace the resolution including the static parameters, plus the extra information about the original method info
            let item = Item.MethodGroup(methodName, [minfoAfterStaticArguments], Some minfos.[0])
            CallNameResolutionSinkReplacing cenv.tcSink (mExprAndItem, env.NameEnv, item, [], ItemOccurence.Use, env.eAccessRights)

            TcMethodApplicationThen cenv env overallTy None tpenv None objArgs mExprAndItem mItem methodName ad mutates false [(minfoAfterStaticArguments, None)] afterResolution NormalValUse args atomicFlag delayed
        | None ->
        if not minfos.IsEmpty && minfos.[0].ProvidedStaticParameterInfo.IsSome then
            error(Error(FSComp.SR.etMissingStaticArgumentsToMethod(), mItem))
#endif

        let tyargsOpt, tpenv = TcMemberTyArgsOpt cenv env tpenv tyargsOpt
        let meths = minfos |> List.map (fun minfo -> minfo, None)

        TcMethodApplicationThen cenv env overallTy None tpenv tyargsOpt objArgs mExprAndItem mItem methodName ad mutates false meths afterResolution NormalValUse args atomicFlag delayed

    | Item.Property (nm, pinfos) ->
        // Instance property
        if isNil pinfos then error (InternalError ("Unexpected error: empty property list", mItem))
        // if there are both intrinsics and extensions in pinfos, intrinsics will be listed first.
        // by looking at List.Head we are letting the intrinsics determine indexed/non-indexed
        let pinfo = List.head pinfos
        let atomicFlag, tyargsOpt, args, delayed, tpenv =
            if pinfo.IsIndexer
            then GetMemberApplicationArgs delayed cenv env tpenv
            else ExprAtomicFlag.Atomic, None, [mkSynUnit mItem], delayed, tpenv
        if pinfo.IsStatic then error (Error (FSComp.SR.tcPropertyIsStatic nm, mItem))


        match delayed with
        | DelayedSet(e2, mStmt) :: otherDelayed ->
            if not (isNil otherDelayed) then error(Error(FSComp.SR.tcInvalidAssignment(), mStmt))
            // Instance property setter
            UnifyTypes cenv env mStmt overallTy cenv.g.unit_ty
            let meths = SettersOfPropInfos pinfos
            if meths.IsEmpty then
                let meths = pinfos |> GettersOfPropInfos
                let isByrefMethReturnSetter = meths |> List.exists (function (_,Some pinfo) -> isByrefTy cenv.g (pinfo.GetPropertyType(cenv.amap,mItem)) | _ -> false)
                if not isByrefMethReturnSetter then
                    errorR (Error (FSComp.SR.tcPropertyCannotBeSet1 nm, mItem))
                // x.P <- ... byref setter
                if isNil meths then error (Error (FSComp.SR.tcPropertyIsNotReadable nm, mItem))
                TcMethodApplicationThen cenv env overallTy None tpenv tyargsOpt objArgs mExprAndItem mItem nm ad PossiblyMutates true meths afterResolution NormalValUse args atomicFlag delayed
            else
                let args = if pinfo.IsIndexer then args else []
                let mut = (if isStructTy cenv.g (tyOfExpr cenv.g objExpr) then DefinitelyMutates else PossiblyMutates)
                TcMethodApplicationThen cenv env overallTy None tpenv tyargsOpt objArgs mStmt mItem nm ad mut true meths afterResolution NormalValUse (args @ [e2]) atomicFlag []
        | _ ->
            // Instance property getter
            let meths = GettersOfPropInfos pinfos
            if isNil meths then error (Error (FSComp.SR.tcPropertyIsNotReadable nm, mItem))
            TcMethodApplicationThen cenv env overallTy None tpenv tyargsOpt objArgs mExprAndItem mItem nm ad PossiblyMutates true meths afterResolution NormalValUse args atomicFlag delayed

    | Item.RecdField rfinfo ->
        // Get or set instance F# field or literal
        RecdFieldInstanceChecks cenv.g cenv.amap ad mItem rfinfo
        let tgtTy = rfinfo.DeclaringType
        let valu = isStructTy cenv.g tgtTy
        AddCxTypeMustSubsumeType ContextInfo.NoContext env.DisplayEnv cenv.css mItem NoTrace tgtTy objExprTy
        let objExpr = if valu then objExpr else mkCoerceExpr(objExpr, tgtTy, mExprAndItem, objExprTy)
        let fieldTy = rfinfo.FieldType
        match delayed with
        | DelayedSet(e2, mStmt) :: otherDelayed ->
            // Mutable value set: 'v <- e'
            if not (isNil otherDelayed) then error(Error(FSComp.SR.tcInvalidAssignment(), mItem))
            CheckRecdFieldMutation mItem env.DisplayEnv rfinfo
            UnifyTypes cenv env mStmt overallTy cenv.g.unit_ty
            // Always allow subsumption on assignment to fields
            let e2', tpenv = TcExprFlex cenv true false fieldTy env tpenv e2
            BuildRecdFieldSet cenv.g mStmt objExpr rfinfo e2', tpenv

        | _ ->

            // Instance F# Record or Class field
            let objExpr' = mkRecdFieldGet cenv.g (objExpr, rfinfo.RecdFieldRef, rfinfo.TypeInst, mExprAndItem)
            PropagateThenTcDelayed cenv overallTy env tpenv mExprAndItem (MakeApplicableExprWithFlex cenv env objExpr') fieldTy ExprAtomicFlag.Atomic delayed

    | Item.AnonRecdField (anonInfo, tinst, n, _) ->
        let tgty = TType_anon (anonInfo, tinst)
        AddCxTypeMustSubsumeType ContextInfo.NoContext env.DisplayEnv cenv.css mItem NoTrace tgty objExprTy
        let fieldTy = List.item n tinst
        match delayed with
        | DelayedSet _ :: _otherDelayed ->
            error(Error(FSComp.SR.tcInvalidAssignment(),mItem))
        | _ ->
            // Instance F# Anonymous Record
            let objExpr' = mkAnonRecdFieldGet cenv.g (anonInfo,objExpr,tinst,n,mExprAndItem)
            PropagateThenTcDelayed cenv overallTy env tpenv mExprAndItem (MakeApplicableExprWithFlex cenv env objExpr') fieldTy ExprAtomicFlag.Atomic delayed

    | Item.ILField finfo ->
        // Get or set instance IL field
        ILFieldInstanceChecks cenv.g cenv.amap ad mItem finfo
        let exprty = finfo.FieldType(cenv.amap, mItem)

        match delayed with
        // Set instance IL field
        | DelayedSet(e2, mStmt) :: _delayed' ->
            UnifyTypes cenv env mStmt overallTy cenv.g.unit_ty
            // Always allow subsumption on assignment to fields
            let e2', tpenv = TcExprFlex cenv true false exprty env tpenv e2
            let expr = BuildILFieldSet cenv.g mStmt objExpr finfo e2'
            expr, tpenv
        | _ ->
            let expr = BuildILFieldGet cenv.g cenv.amap mExprAndItem objExpr finfo
            PropagateThenTcDelayed cenv overallTy env tpenv mExprAndItem (MakeApplicableExprWithFlex cenv env expr) exprty ExprAtomicFlag.Atomic delayed

    | Item.Event einfo ->
        // Instance IL event (fake up event-as-value)
        TcEventValueThen cenv overallTy env tpenv mItem mExprAndItem (Some(objExpr, objExprTy)) einfo delayed

    | (Item.FakeInterfaceCtor _ | Item.DelegateCtor _) -> error (Error (FSComp.SR.tcConstructorsCannotBeFirstClassValues(), mItem))
    | _ -> error (Error (FSComp.SR.tcSyntaxFormUsedOnlyWithRecordLabelsPropertiesAndFields(), mItem))

and TcEventValueThen cenv overallTy env tpenv mItem mExprAndItem objDetails (einfo: EventInfo) delayed =
    // Instance IL event (fake up event-as-value)
    let nm = einfo.EventName
    let ad = env.eAccessRights
    match objDetails, einfo.IsStatic with
    | Some _, true -> error (Error (FSComp.SR.tcEventIsStatic nm, mItem))
    | None, false -> error (Error (FSComp.SR.tcEventIsNotStatic nm, mItem))
    | _ -> ()

    let delegateType = einfo.GetDelegateType(cenv.amap, mItem)
    let (SigOfFunctionForDelegate(invokeMethInfo, compiledViewOfDelArgTys, _, _)) = GetSigOfFunctionForDelegate cenv.infoReader delegateType mItem ad
    let objArgs = Option.toList (Option.map fst objDetails)
    MethInfoChecks cenv.g cenv.amap true None objArgs env.eAccessRights mItem invokeMethInfo

    // This checks for and drops the 'object' sender
    let argsTy = ArgsTypOfEventInfo cenv.infoReader mItem ad einfo
    if not (slotSigHasVoidReturnTy (invokeMethInfo.GetSlotSig(cenv.amap, mItem))) then errorR (nonStandardEventError einfo.EventName mItem)
    let delEventTy = mkIEventType cenv.g delegateType argsTy

    let bindObjArgs f =
        match objDetails with
        | None -> f []
        | Some (objExpr, objExprTy) -> mkCompGenLetIn mItem "eventTarget" objExprTy objExpr (fun (_, ve) -> f [ve])

    // Bind the object target expression to make sure we only run its side effects once, and to make
    // sure if it's a mutable reference then we dereference it - see FSharp 1.0 bug 942
    let expr =
        bindObjArgs (fun objVars ->
             //     EventHelper ((fun d -> e.add_X(d)), (fun d -> e.remove_X(d)), (fun f -> new 'Delegate(f)))
            mkCallCreateEvent cenv.g mItem delegateType argsTy
               (let dv, de = mkCompGenLocal mItem "eventDelegate" delegateType
                let callExpr, _ = BuildPossiblyConditionalMethodCall cenv env PossiblyMutates mItem false einfo.AddMethod NormalValUse [] objVars [de]
                mkLambda mItem dv (callExpr, cenv.g.unit_ty))
               (let dv, de = mkCompGenLocal mItem "eventDelegate" delegateType
                let callExpr, _ = BuildPossiblyConditionalMethodCall cenv env PossiblyMutates mItem false einfo.RemoveMethod NormalValUse [] objVars [de]
                mkLambda mItem dv (callExpr, cenv.g.unit_ty))
               (let fvty = (cenv.g.obj_ty --> (argsTy --> cenv.g.unit_ty))
                let fv, fe = mkCompGenLocal mItem "callback" fvty
                let createExpr = BuildNewDelegateExpr (Some einfo, cenv.g, cenv.amap, delegateType, invokeMethInfo, compiledViewOfDelArgTys, fe, fvty, mItem)
                mkLambda mItem fv (createExpr, delegateType)))

    let exprty = delEventTy
    PropagateThenTcDelayed cenv overallTy env tpenv mExprAndItem (MakeApplicableExprNoFlex cenv expr) exprty ExprAtomicFlag.Atomic delayed


//-------------------------------------------------------------------------
// Method uses can calls
//-------------------------------------------------------------------------

/// Typecheck method/member calls and uses of members as first-class values.
and TcMethodApplicationThen
       cenv
       env
       overallTy           // The type of the overall expression including "delayed". The method "application" may actually be a use of a member as
                    // a first-class function value, when this would be a function type.
       objTyOpt   // methodType
       tpenv
       callerTyArgs // The return type of the overall expression including "delayed"
       objArgs      // The 'obj' arguments in obj.M(...) and obj.M, if any
       m           // The range of the object argument or whole application. We immediately union this with the range of the arguments
       mItem       // The range of the item that resolved to the method name
       methodName // string, name of the method
       ad          // accessibility rights of the caller
       mut         // what do we know/assume about whether this method will mutate or not?
       isProp      // is this a property call? Used for better error messages and passed to BuildMethodCall
       meths       // the set of methods we may be calling
       afterResolution // do we need to notify sink after overload resolution
       isSuperInit // is this a special invocation, e.g. a super-class constructor call. Passed through to BuildMethodCall
       args        // the _syntactic_ method arguments, not yet type checked.
       atomicFlag // is the expression atomic or not?
       delayed     // further lookups and applications that follow this
     =

    // Nb. args is always of List.length <= 1 except for indexed setters, when it is 2
    let mWholeExpr = (m, args) ||> List.fold (fun m arg -> unionRanges m arg.Range)

    // Work out if we know anything about the return type of the overall expression. If there are any delayed
    // lookups then we don't know anything.
    let exprTy = if isNil delayed then overallTy else NewInferenceType ()

    // Call the helper below to do the real checking
    let (expr, attributeAssignedNamedItems, delayed), tpenv =
        TcMethodApplication false cenv env tpenv callerTyArgs objArgs mWholeExpr mItem methodName objTyOpt ad mut isProp meths afterResolution isSuperInit args exprTy delayed

    // Give errors if some things couldn't be assigned
    if not (isNil attributeAssignedNamedItems) then
        let (CallerNamedArg(id, _)) = List.head attributeAssignedNamedItems
        errorR(Error(FSComp.SR.tcNamedArgumentDidNotMatch(id.idText), id.idRange))


    // Resolve the "delayed" lookups
    let exprty = (tyOfExpr cenv.g expr)

    PropagateThenTcDelayed cenv overallTy env tpenv mWholeExpr (MakeApplicableExprNoFlex cenv expr) exprty atomicFlag delayed

/// Infer initial type information at the callsite from the syntax of an argument, prior to overload resolution.
and GetNewInferenceTypeForMethodArg cenv env tpenv x =
    match x with
    | SynExprParen(a, _, _, _) -> GetNewInferenceTypeForMethodArg cenv env tpenv a
    | SynExpr.AddressOf (true, a, _, m) -> mkByrefTyWithInference cenv.g (GetNewInferenceTypeForMethodArg cenv env tpenv a) (NewByRefKindInferenceType cenv.g m)
    | SynExpr.Lambda (_, _, _, a, _, _) -> mkFunTy (NewInferenceType ()) (GetNewInferenceTypeForMethodArg cenv env tpenv a)
    | SynExpr.Quote (_, raw, a, _, _) ->
        if raw then mkRawQuotedExprTy cenv.g
        else mkQuotedExprTy cenv.g (GetNewInferenceTypeForMethodArg cenv env tpenv a)
    | _ -> NewInferenceType ()

/// Method calls, property lookups, attribute constructions etc. get checked through here
and TcMethodApplication
        isCheckingAttributeCall
        cenv
        env
        tpenv
        tyargsOpt
        objArgs
        mMethExpr // range of the entire method expression
        mItem
        methodName
         (objTyOpt: TType option)
        ad
        mut
        isProp
        calledMethsAndProps
        afterResolution
        isSuperInit
        curriedCallerArgs
        exprTy
        delayed
    =

    let denv = env.DisplayEnv

    let isSimpleFormalArg (isParamArrayArg, _isInArg, isOutArg, optArgInfo: OptionalArgInfo, callerInfo: CallerInfo, _reflArgInfo: ReflectedArgInfo) =
        not isParamArrayArg && not isOutArg && not optArgInfo.IsOptional && callerInfo = NoCallerInfo

    let callerObjArgTys = objArgs |> List.map (tyOfExpr cenv.g)

    let calledMeths = calledMethsAndProps |> List.map fst

    // Uses of curried members are ALWAYS treated as if they are first class uses of members.
    // Curried members may not be overloaded (checked at use-site for curried members brought into scope through extension members)
    let curriedCallerArgs, exprTy, delayed =
        match calledMeths with
        | [calledMeth] when not isProp && calledMeth.NumArgs.Length > 1 ->
            [], NewInferenceType (), [ for x in curriedCallerArgs -> DelayedApp(ExprAtomicFlag.NonAtomic, x, x.Range) ] @ delayed
        | _ when not isProp && calledMeths |> List.exists (fun calledMeth -> calledMeth.NumArgs.Length > 1) ->
            // This condition should only apply when multiple conflicting curried extension members are brought into scope
            error(Error(FSComp.SR.tcOverloadsCannotHaveCurriedArguments(), mMethExpr))
        | _ ->
            curriedCallerArgs, exprTy, delayed

    let candidateMethsAndProps =
        match calledMethsAndProps |> List.filter (fun (meth, _prop) -> IsMethInfoAccessible cenv.amap mItem ad meth) with
        | [] -> calledMethsAndProps
        | accessibleMeths -> accessibleMeths

    let candidates = candidateMethsAndProps |> List.map fst


    // Split the syntactic arguments (if any) into named and unnamed parameters
    //
    // In one case (the second "single named item" rule) we delay the application of a
    // argument until we've produced a lambda that detuples an input tuple
    let curriedCallerArgsOpt, unnamedDelayedCallerArgExprOpt, exprTy =
        match curriedCallerArgs with
        | [] ->
            None, None, exprTy
        | _ ->
            let unnamedCurriedCallerArgs, namedCurriedCallerArgs = curriedCallerArgs |> List.map GetMethodArgs |> List.unzip

            // There is an mismatch when _uses_ of indexed property setters in the tc.fs code that calls this function.
            // The arguments are passed as if they are curried with arity [numberOfIndexParameters;1], however in the TAST, indexed property setters
            // are uncurried and have arity [numberOfIndexParameters+1].
            //
            // Here we work around this mismatch by crunching all property argument lists to uncurried form.
            // Ideally the problem needs to be solved at its root cause at the callsites to this function
            let unnamedCurriedCallerArgs, namedCurriedCallerArgs =
                if isProp then
                    [List.concat unnamedCurriedCallerArgs], [List.concat namedCurriedCallerArgs]
                else
                    unnamedCurriedCallerArgs, namedCurriedCallerArgs

            let MakeUnnamedCallerArgInfo x = (x, GetNewInferenceTypeForMethodArg cenv env tpenv x, x.Range)

            // "single named item" rule. This is where we have a single accessible method
            //      member x.M(arg1)
            // being used with
            //      x.M (x, y)
            // Without this rule this requires
            //      x.M ((x, y))
            match candidates with
            | [calledMeth]
                  when (namedCurriedCallerArgs |> List.forall isNil &&
                        let curriedCalledArgs = calledMeth.GetParamAttribs(cenv.amap, mItem)
                        curriedCalledArgs.Length = 1 &&
                        curriedCalledArgs.Head.Length = 1 &&
                        curriedCalledArgs.Head.Head |> isSimpleFormalArg) ->
                let unnamedCurriedCallerArgs = curriedCallerArgs |> List.map (MakeUnnamedCallerArgInfo >> List.singleton)
                let namedCurriedCallerArgs = namedCurriedCallerArgs |> List.map (fun _ -> [])
                (Some (unnamedCurriedCallerArgs, namedCurriedCallerArgs), None, exprTy)

            // "single named item" rule. This is where we have a single accessible method
            //      member x.M(arg1, arg2)
            // being used with
            //      x.M p
            // We typecheck this as if it has been written "(fun (v1, v2) -> x.M(v1, v2)) p"
            // Without this rule this requires
            //      x.M (fst p, snd p)
            | [calledMeth]
                  when (namedCurriedCallerArgs |> List.forall isNil &&
                        unnamedCurriedCallerArgs.Length = 1 &&
                        unnamedCurriedCallerArgs.Head.Length = 1 &&
                        let curriedCalledArgs = calledMeth.GetParamAttribs(cenv.amap, mItem)
                        curriedCalledArgs.Length = 1 &&
                        curriedCalledArgs.Head.Length > 1 &&
                        curriedCalledArgs.Head |> List.forall isSimpleFormalArg) ->

                // The call lambda has function type
                let exprTy = mkFunTy (NewInferenceType ()) exprTy

                (None, Some unnamedCurriedCallerArgs.Head.Head, exprTy)

            | _ ->
                let unnamedCurriedCallerArgs = unnamedCurriedCallerArgs |> List.mapSquared MakeUnnamedCallerArgInfo
                let namedCurriedCallerArgs = namedCurriedCallerArgs |> List.mapSquared (fun (isOpt, nm, x) ->
                    let ty = GetNewInferenceTypeForMethodArg cenv env tpenv x
                    // #435263: compiler crash with .net optional parameters and F# optional syntax
                    // named optional arguments should always have option type
                    // STRUCT OPTIONS: if we allow struct options as optional arguments then we should relax this and rely
                    // on later inference to work out if this is a struct option or ref option
                    let ty = if isOpt then mkOptionTy denv.g ty else ty
                    nm, isOpt, x, ty, x.Range)

                (Some (unnamedCurriedCallerArgs, namedCurriedCallerArgs), None, exprTy)

    let CalledMethHasSingleArgumentGroupOfThisLength n (calledMeth: MethInfo) =
       let curriedMethodArgAttribs = calledMeth.GetParamAttribs(cenv.amap, mItem)
       curriedMethodArgAttribs.Length = 1 &&
       curriedMethodArgAttribs.Head.Length = n

    let GenerateMatchingSimpleArgumentTypes (calledMeth: MethInfo) =
        let curriedMethodArgAttribs = calledMeth.GetParamAttribs(cenv.amap, mItem)
        curriedMethodArgAttribs
        |> List.map (List.filter isSimpleFormalArg >> NewInferenceTypes)

    let UnifyMatchingSimpleArgumentTypes exprTy (calledMeth: MethInfo) =
        let curriedArgTys = GenerateMatchingSimpleArgumentTypes calledMeth
        let returnTy =
            (exprTy, curriedArgTys) ||> List.fold (fun exprTy argTys ->
                let domainTy, resultTy = UnifyFunctionType None cenv denv mMethExpr exprTy
                UnifyTypes cenv env mMethExpr domainTy (mkRefTupledTy cenv.g argTys)
                resultTy)
        curriedArgTys, returnTy

    if isProp && Option.isNone curriedCallerArgsOpt then
        error(Error(FSComp.SR.parsIndexerPropertyRequiresAtLeastOneArgument(), mItem))

    // STEP 1. UnifyUniqueOverloading. This happens BEFORE we type check the arguments.
    // Extract what we know about the caller arguments, either type-directed if
    // no arguments are given or else based on the syntax of the arguments.
    let uniquelyResolved, preArgumentTypeCheckingCalledMethGroup =
        let dummyExpr = mkSynUnit mItem

        // Build the CallerArg values for the caller's arguments.
        // Fake up some arguments if this is the use of a method as a first class function
        let unnamedCurriedCallerArgs, namedCurriedCallerArgs, returnTy =

            match curriedCallerArgsOpt, candidates with
            // "single named item" rule. This is where we have a single accessible method
            //      member x.M(arg1, ..., argN)
            // being used in a first-class way, i.e.
            //      x.M
            // Because there is only one accessible method info available based on the name of the item
            // being accessed we know the number of arguments the first class use of this
            // method will take. Optional and out args are _not_ included, which means they will be resolved
            // to their default values (for optionals) and be part of the return tuple (for out args).
            | None, [calledMeth] ->
                let curriedArgTys, returnTy = UnifyMatchingSimpleArgumentTypes exprTy calledMeth
                let unnamedCurriedCallerArgs = curriedArgTys |> List.mapSquared (fun ty -> CallerArg(ty, mMethExpr, false, dummyExpr))
                let namedCurriedCallerArgs = unnamedCurriedCallerArgs |> List.map (fun _ -> [])
                unnamedCurriedCallerArgs, namedCurriedCallerArgs, returnTy

            // "type directed" rule for first-class uses of ambiguous methods.
            // By context we know a type for the input argument. If it's a tuple
            // this gives us the a potential number of arguments expected. Indeed even if it's a variable
            // type we assume the number of arguments is just "1".
            | None, _ ->

                let domainTy, returnTy = UnifyFunctionType None cenv denv mMethExpr exprTy
                let argTys = if isUnitTy cenv.g domainTy then [] else tryDestRefTupleTy cenv.g domainTy
                // Only apply this rule if a candidate method exists with this number of arguments
                let argTys =
                    if candidates |> List.exists (CalledMethHasSingleArgumentGroupOfThisLength argTys.Length) then
                       argTys
                    else
                       [domainTy]
                let unnamedCurriedCallerArgs = [argTys |> List.map (fun ty -> CallerArg(ty, mMethExpr, false, dummyExpr)) ]
                let namedCurriedCallerArgs = unnamedCurriedCallerArgs |> List.map (fun _ -> [])
                unnamedCurriedCallerArgs, namedCurriedCallerArgs, returnTy

            | Some (unnamedCurriedCallerArgs, namedCurriedCallerArgs), _ ->
                let unnamedCurriedCallerArgs = unnamedCurriedCallerArgs |> List.mapSquared (fun (argExpr, argTy, mArg) -> CallerArg(argTy, mArg, false, argExpr))
                let namedCurriedCallerArgs = namedCurriedCallerArgs |> List.mapSquared (fun (id, isOpt, argExpr, argTy, mArg) -> CallerNamedArg(id, CallerArg(argTy, mArg, isOpt, argExpr)))
                unnamedCurriedCallerArgs, namedCurriedCallerArgs, exprTy

        let callerArgCounts = (List.sumBy List.length unnamedCurriedCallerArgs, List.sumBy List.length namedCurriedCallerArgs)

        let callerArgs = { Unnamed = unnamedCurriedCallerArgs; Named = namedCurriedCallerArgs }

        let makeOneCalledMeth (minfo, pinfoOpt, usesParamArrayConversion) =
            let minst = FreshenMethInfo mItem minfo
            let callerTyArgs =
                match tyargsOpt with
                | Some tyargs -> minfo.AdjustUserTypeInstForFSharpStyleIndexedExtensionMembers tyargs
                | None -> minst
            CalledMeth<SynExpr>(cenv.infoReader, Some(env.NameEnv), isCheckingAttributeCall, FreshenMethInfo, mMethExpr, ad, minfo, minst, callerTyArgs, pinfoOpt, callerObjArgTys, callerArgs, usesParamArrayConversion, true, objTyOpt)

        let preArgumentTypeCheckingCalledMethGroup =
            [ for (minfo, pinfoOpt) in candidateMethsAndProps do
                let meth = makeOneCalledMeth (minfo, pinfoOpt, true)
                yield meth
                if meth.UsesParamArrayConversion then
                    yield makeOneCalledMeth (minfo, pinfoOpt, false) ]

        let uniquelyResolved =
            UnifyUniqueOverloading denv cenv.css mMethExpr callerArgCounts methodName ad preArgumentTypeCheckingCalledMethGroup returnTy

        uniquelyResolved, preArgumentTypeCheckingCalledMethGroup

    // STEP 2. Type check arguments
    let unnamedCurriedCallerArgs, namedCurriedCallerArgs, lambdaVars, returnTy, tpenv =

        // STEP 2a. First extract what we know about the caller arguments, either type-directed if
        // no arguments are given or else based on the syntax of the arguments.
        match curriedCallerArgsOpt with
        | None ->
            let curriedArgTys, returnTy =
                match candidates with
                // "single named item" rule. This is where we have a single accessible method
                //      member x.M(arg1, ..., argN)
                // being used in a first-class way, i.e.
                //      x.M
                // Because there is only one accessible method info available based on the name of the item
                // being accessed we know the number of arguments the first class use of this
                // method will take. Optional and out args are _not_ included, which means they will be resolved
                // to their default values (for optionals) and be part of the return tuple (for out args).
                | [calledMeth] ->
                    UnifyMatchingSimpleArgumentTypes exprTy calledMeth
                | _ ->
                    let domainTy, returnTy = UnifyFunctionType None cenv denv mMethExpr exprTy
                    let argTys = if isUnitTy cenv.g domainTy then [] else tryDestRefTupleTy cenv.g domainTy
                    // Only apply this rule if a candidate method exists with this number of arguments
                    let argTys =
                        if candidates |> List.exists (CalledMethHasSingleArgumentGroupOfThisLength argTys.Length) then
                            argTys
                        else
                            [domainTy]
                    [argTys], returnTy

            let lambdaVarsAndExprs = curriedArgTys |> List.mapiSquared (fun i j ty -> mkCompGenLocal mMethExpr ("arg"+string i+string j) ty)
            let unnamedCurriedCallerArgs = lambdaVarsAndExprs |> List.mapSquared (fun (_, e) -> CallerArg(tyOfExpr cenv.g e, e.Range, false, e))
            let namedCurriedCallerArgs = lambdaVarsAndExprs |> List.map (fun _ -> [])
            let lambdaVars = List.mapSquared fst lambdaVarsAndExprs
            unnamedCurriedCallerArgs, namedCurriedCallerArgs, Some lambdaVars, returnTy, tpenv

        | Some (unnamedCurriedCallerArgs, namedCurriedCallerArgs) ->
            // This is the case where some explicit arguments have been given.

            let unnamedCurriedCallerArgs = unnamedCurriedCallerArgs |> List.mapSquared (fun (argExpr, argTy, mArg) -> CallerArg(argTy, mArg, false, argExpr))
            let namedCurriedCallerArgs = namedCurriedCallerArgs |> List.mapSquared (fun (id, isOpt, argExpr, argTy, mArg) -> CallerNamedArg(id, CallerArg(argTy, mArg, isOpt, argExpr)))

            // Collect the information for F# 3.1 lambda propagation rule, and apply the caller's object type to the method's object type if the rule is relevant.
            let lambdaPropagationInfo =
                if preArgumentTypeCheckingCalledMethGroup.Length > 1 then
                    [| for meth in preArgumentTypeCheckingCalledMethGroup do
                        match ExamineMethodForLambdaPropagation meth with
                        | Some (unnamedInfo, namedInfo) ->
                            let calledObjArgTys = meth.CalledObjArgTys mMethExpr
                            if (calledObjArgTys, callerObjArgTys) ||> Seq.forall2 (fun calledTy callerTy -> AddCxTypeMustSubsumeTypeMatchingOnlyUndoIfFailed denv cenv.css mMethExpr calledTy callerTy) then
                                yield (List.toArraySquared unnamedInfo, List.toArraySquared namedInfo)
                        | None -> () |]
                else
                    [| |]

            // Now typecheck the argument expressions
            let unnamedCurriedCallerArgs, (lambdaPropagationInfo, tpenv) = TcUnnamedMethodArgs cenv env lambdaPropagationInfo tpenv unnamedCurriedCallerArgs
            let namedCurriedCallerArgs, (_, tpenv) = TcMethodNamedArgs cenv env lambdaPropagationInfo tpenv namedCurriedCallerArgs
            unnamedCurriedCallerArgs, namedCurriedCallerArgs, None, exprTy, tpenv

    let preArgumentTypeCheckingCalledMethGroup =
       preArgumentTypeCheckingCalledMethGroup |> List.map (fun cmeth -> (cmeth.Method, cmeth.CalledTyArgs, cmeth.AssociatedPropertyInfo, cmeth.UsesParamArrayConversion))

    let uniquelyResolved =
        match uniquelyResolved with
        | ErrorResult _ ->
            match afterResolution with
            | AfterResolution.DoNothing -> ()
            | AfterResolution.RecordResolution(_, _, _, onFailure) -> onFailure()
        | _ -> ()

        uniquelyResolved |> CommitOperationResult

    // STEP 3. Resolve overloading
    /// Select the called method that's the result of overload resolution
    let finalCalledMeth =

        let callerArgs = { Unnamed = unnamedCurriedCallerArgs ; Named = namedCurriedCallerArgs }

        let postArgumentTypeCheckingCalledMethGroup =
            preArgumentTypeCheckingCalledMethGroup |> List.map (fun (minfo: MethInfo, minst, pinfoOpt, usesParamArrayConversion) ->
                let callerTyArgs =
                    match tyargsOpt with
                    | Some tyargs -> minfo.AdjustUserTypeInstForFSharpStyleIndexedExtensionMembers tyargs
                    | None -> minst
                CalledMeth<Expr>(cenv.infoReader, Some(env.NameEnv), isCheckingAttributeCall, FreshenMethInfo, mMethExpr, ad, minfo, minst, callerTyArgs, pinfoOpt, callerObjArgTys, callerArgs, usesParamArrayConversion, true, objTyOpt))

        // Commit unassociated constraints prior to member overload resolution where there is ambiguity
        // about the possible target of the call.
        if not uniquelyResolved then
            ConstraintSolver.CanonicalizePartialInferenceProblem cenv.css denv mItem
                 (//freeInTypeLeftToRight cenv.g false returnTy @
                  (unnamedCurriedCallerArgs |> List.collectSquared (fun callerArg -> freeInTypeLeftToRight cenv.g false callerArg.CallerArgumentType)))

        let result, errors = ResolveOverloadingForCall denv cenv.css mMethExpr methodName 0 None callerArgs ad postArgumentTypeCheckingCalledMethGroup true (Some returnTy)

        match afterResolution, result with
        | AfterResolution.DoNothing, _ -> ()

        // Record the precise override resolution
        | AfterResolution.RecordResolution(Some unrefinedItem, _, callSink, _), Some result
             when result.Method.IsVirtual ->

            let overriding =
                match unrefinedItem with
                | Item.MethodGroup(_, overridenMeths, _) -> overridenMeths |> List.map (fun minfo -> minfo, None)
                | Item.Property(_, pinfos) ->
                    if result.Method.LogicalName.StartsWithOrdinal("set_") then
                        SettersOfPropInfos pinfos
                    else
                        GettersOfPropInfos pinfos
                | _ -> []

            let overridingInfo =
                overriding
                |> List.tryFind (fun (minfo, _) -> minfo.IsVirtual && MethInfosEquivByNameAndSig EraseNone true cenv.g cenv.amap range0 result.Method minfo)

            match overridingInfo with
            | Some (minfo, pinfoOpt) ->
                let tps = minfo.FormalMethodTypars
                let tyargs = result.CalledTyArgs
                let tpinst = if tps.Length = tyargs.Length then mkTyparInst tps tyargs else []
                (minfo, pinfoOpt, tpinst) |> callSink
            | None ->
                (result.Method, result.AssociatedPropertyInfo, result.CalledTyparInst) |> callSink

        // Record the precise overload resolution and the type instantiation
        | AfterResolution.RecordResolution(_, _, callSink, _), Some result ->
            (result.Method, result.AssociatedPropertyInfo, result.CalledTyparInst) |> callSink

        | AfterResolution.RecordResolution(_, _, _, onFailure), None ->
            onFailure()


        // Raise the errors from the constraint solving
        RaiseOperationResult errors
        match result with
        | None -> error(InternalError("at least one error should be returned by failed method overloading", mItem))
        | Some res -> res

    let finalCalledMethInfo = finalCalledMeth.Method
    let finalCalledMethInst = finalCalledMeth.CalledTyArgs
    let finalAssignedItemSetters = finalCalledMeth.AssignedItemSetters
    let finalAttributeAssignedNamedItems = finalCalledMeth.AttributeAssignedNamedArgs

    // STEP 4. Check the attributes on the method and the corresponding event/property, if any

    finalCalledMeth.AssociatedPropertyInfo |> Option.iter (fun pinfo -> CheckPropInfoAttributes pinfo mItem |> CommitOperationResult)

    let isInstance = not (isNil objArgs)
    MethInfoChecks cenv.g cenv.amap isInstance tyargsOpt objArgs ad mItem finalCalledMethInfo

    // Adhoc constraints on use of .NET methods
    begin
        // Uses of Object.GetHashCode and Object.Equals imply an equality constraint on the object argument
        //
        if (isInstance &&
            finalCalledMethInfo.IsInstance &&
            typeEquiv cenv.g finalCalledMethInfo.ApparentEnclosingType cenv.g.obj_ty &&
            (finalCalledMethInfo.LogicalName = "GetHashCode" || finalCalledMethInfo.LogicalName = "Equals")) then

            objArgs |> List.iter (fun expr -> ConstraintSolver.AddCxTypeMustSupportEquality env.DisplayEnv cenv.css mMethExpr NoTrace (tyOfExpr cenv.g expr))

        // Uses of a Dictionary() constructor without an IEqualityComparer argument imply an equality constraint
        // on the first type argument.
        if HasHeadType cenv.g cenv.g.tcref_System_Collections_Generic_Dictionary finalCalledMethInfo.ApparentEnclosingType &&
           finalCalledMethInfo.IsConstructor &&
           not (finalCalledMethInfo.GetParamDatas(cenv.amap, mItem, finalCalledMeth.CalledTyArgs)
                |> List.existsSquared (fun (ParamData(_, _, _, _, _, _, _, ty)) ->
                    HasHeadType cenv.g cenv.g.tcref_System_Collections_Generic_IEqualityComparer ty)) then

            match argsOfAppTy cenv.g finalCalledMethInfo.ApparentEnclosingType with
            | [dty; _] -> ConstraintSolver.AddCxTypeMustSupportEquality env.DisplayEnv cenv.css mMethExpr NoTrace dty
            | _ -> ()
    end

    if (finalCalledMeth.ArgSets |> List.existsi (fun i argSet -> argSet.UnnamedCalledArgs |> List.existsi (fun j ca -> ca.Position <> (i, j)))) then
        errorR(Deprecated(FSComp.SR.tcUnnamedArgumentsDoNotFormPrefix(), mMethExpr))

    /// STEP 5. Build the argument list. Adjust for optional arguments, byref arguments and coercions.

    let objArgPreBinder, objArgs, allArgsPreBinders, allArgs, allArgsCoerced, optArgPreBinder, paramArrayPreBinders, outArgExprs, outArgTmpBinds =
        AdjustCallerArgs TcFieldInit env.eCallerMemberName cenv.infoReader ad finalCalledMeth objArgs lambdaVars mItem mMethExpr

    // Record the resolution of the named argument for the Language Service
    allArgs |> List.iter (fun assignedArg ->
        match assignedArg.NamedArgIdOpt with
        | None -> ()
        | Some id ->
            let item = Item.ArgName (defaultArg assignedArg.CalledArg.NameOpt id, assignedArg.CalledArg.CalledArgumentType, Some(ArgumentContainer.Method finalCalledMethInfo))
            CallNameResolutionSink cenv.tcSink (id.idRange, env.NameEnv, item, emptyTyparInst, ItemOccurence.Use, ad))


    /// STEP 6. Build the call expression, then adjust for byref-returns, out-parameters-as-tuples, post-hoc property assignments, methods-as-first-class-value,
    ///

    let callExpr0, exprty =
        BuildPossiblyConditionalMethodCall cenv env mut mMethExpr isProp finalCalledMethInfo isSuperInit finalCalledMethInst objArgs allArgsCoerced

    // Handle byref returns
    let callExpr1 =
        // byref-typed returns get implicitly dereferenced
        let vty = tyOfExpr cenv.g callExpr0
        if isByrefTy cenv.g vty then
            mkDerefAddrExpr mMethExpr callExpr0 mMethExpr vty
        else
            callExpr0

    // Bind "out" parameters as part of the result tuple
    let callExpr2, exprty =
        let expr = callExpr1
        if isNil outArgTmpBinds then expr, exprty
        else
            let outArgTys = outArgExprs |> List.map (tyOfExpr cenv.g)
            let expr =
                if isUnitTy cenv.g exprty then
                    mkCompGenSequential mMethExpr expr (mkRefTupled cenv.g mMethExpr outArgExprs outArgTys)
                else
                    mkRefTupled cenv.g mMethExpr (expr :: outArgExprs) (exprty :: outArgTys)
            let expr = mkLetsBind mMethExpr outArgTmpBinds expr
            expr, tyOfExpr cenv.g expr

    // Handle post-hoc property assignments
    let setterExprPrebinders, callExpr3 =
        let expr = callExpr2
        if isCheckingAttributeCall then
            [], expr
        elif isNil finalAssignedItemSetters then
            [], expr
        else
            // This holds the result of the call
            let objv, objExpr = mkMutableCompGenLocal mMethExpr "returnVal" exprty // mutable in case it's a struct

            // Build the expression that mutates the properties on the result of the call
            let setterExprPrebinders, propSetExpr =
                (mkUnit cenv.g mMethExpr, finalAssignedItemSetters) ||> List.mapFold (fun acc assignedItemSetter ->
                        let argExprPrebinder, action, m = TcSetterArgExpr cenv env denv objExpr ad assignedItemSetter
                        argExprPrebinder, mkCompGenSequential m acc action)

            // now put them together
            let expr = mkCompGenLet mMethExpr objv expr (mkCompGenSequential mMethExpr propSetExpr objExpr)
            setterExprPrebinders, expr

    // Build the lambda expression if any, if the method is used as a first-class value
    let callExpr4 =
        let expr = callExpr3
        match lambdaVars with
        | None -> expr
        | Some curriedLambdaVars ->
            let mkLambda vs expr =
                match vs with
                | [] -> mkUnitDelayLambda cenv.g mMethExpr expr
                | _ -> mkMultiLambda mMethExpr vs (expr, tyOfExpr cenv.g expr)
            List.foldBack mkLambda curriedLambdaVars expr

    let callExpr5, tpenv =
        let expr = callExpr4
        match unnamedDelayedCallerArgExprOpt with
        | Some synArgExpr ->
            match lambdaVars with
            | Some [lambdaVars] ->
                let argExpr, tpenv = TcExpr cenv (mkRefTupledVarsTy cenv.g lambdaVars) env tpenv synArgExpr
                mkApps cenv.g ((expr, tyOfExpr cenv.g expr), [], [argExpr], mMethExpr), tpenv
            | _ ->
                error(InternalError("unreachable - expected some lambda vars for a tuple mismatch", mItem))
        | None ->
            expr, tpenv

    // Apply the PreBinders, if any
    let callExpr6 =
        let expr = callExpr5
        let expr = (expr, setterExprPrebinders) ||> List.fold (fun expr argPreBinder -> match argPreBinder with None -> expr | Some f -> f expr)
        let expr = (expr, paramArrayPreBinders) ||> List.fold (fun expr argPreBinder -> match argPreBinder with None -> expr | Some f -> f expr)
        let expr = (expr, allArgsPreBinders) ||> List.fold (fun expr argPreBinder -> match argPreBinder with None -> expr | Some f -> f expr)

        let expr = optArgPreBinder expr
        let expr = objArgPreBinder expr
        expr

    (callExpr6, finalAttributeAssignedNamedItems, delayed), tpenv

and TcSetterArgExpr cenv env denv objExpr ad (AssignedItemSetter(id, setter, CallerArg(callerArgTy, m, isOptCallerArg, argExpr))) =
    if isOptCallerArg then error(Error(FSComp.SR.tcInvalidOptionalAssignmentToPropertyOrField(), m))

    let argExprPrebinder, action, defnItem =
        match setter with
        | AssignedPropSetter (pinfo, pminfo, pminst) ->
            MethInfoChecks cenv.g cenv.amap true None [objExpr] ad m pminfo
            let calledArgTy = List.head (List.head (pminfo.GetParamTypes(cenv.amap, m, pminst)))
            let argExprPrebinder, argExpr = MethodCalls.AdjustCallerArgExprForCoercions cenv.g cenv.amap cenv.infoReader ad false calledArgTy ReflectedArgInfo.None callerArgTy m argExpr
            let mut = (if isStructTy cenv.g (tyOfExpr cenv.g objExpr) then DefinitelyMutates else PossiblyMutates)
            let action = BuildPossiblyConditionalMethodCall cenv env mut m true pminfo NormalValUse pminst [objExpr] [argExpr] |> fst
            argExprPrebinder, action, Item.Property (pinfo.PropertyName, [pinfo])

        | AssignedILFieldSetter finfo ->
            // Get or set instance IL field
            ILFieldInstanceChecks cenv.g cenv.amap ad m finfo
            let calledArgTy = finfo.FieldType (cenv.amap, m)
            let argExprPrebinder, argExpr = MethodCalls.AdjustCallerArgExprForCoercions cenv.g cenv.amap cenv.infoReader ad false calledArgTy ReflectedArgInfo.None callerArgTy m argExpr
            let action = BuildILFieldSet cenv.g m objExpr finfo argExpr
            argExprPrebinder, action, Item.ILField finfo

        | AssignedRecdFieldSetter rfinfo ->
            RecdFieldInstanceChecks cenv.g cenv.amap ad m rfinfo
            let calledArgTy = rfinfo.FieldType
            CheckRecdFieldMutation m denv rfinfo
            let argExprPrebinder, argExpr = MethodCalls.AdjustCallerArgExprForCoercions cenv.g cenv.amap cenv.infoReader ad false calledArgTy ReflectedArgInfo.None callerArgTy m argExpr
            let action = BuildRecdFieldSet cenv.g m objExpr rfinfo argExpr
            argExprPrebinder, action, Item.RecdField rfinfo

    // Record the resolution for the Language Service
    let item = Item.SetterArg (id, defnItem)
    CallNameResolutionSink cenv.tcSink (id.idRange, env.NameEnv, item, emptyTyparInst, ItemOccurence.Use, ad)

    argExprPrebinder, action, m

and TcUnnamedMethodArgs cenv env lambdaPropagationInfo tpenv args =
    List.mapiFoldSquared (TcUnnamedMethodArg cenv env) (lambdaPropagationInfo, tpenv) args

and TcUnnamedMethodArg cenv env (lambdaPropagationInfo, tpenv) (i, j, CallerArg(argTy, mArg, isOpt, argExpr)) =
    // Try to find the lambda propagation info for the corresponding unnamed argument at this position
    let lambdaPropagationInfoForArg =
        [| for (unnamedInfo, _) in lambdaPropagationInfo ->
             if i < unnamedInfo.Length && j < unnamedInfo.[i].Length then unnamedInfo.[i].[j] else NoInfo |]
    TcMethodArg cenv env (lambdaPropagationInfo, tpenv) (lambdaPropagationInfoForArg, CallerArg(argTy, mArg, isOpt, argExpr))

and TcMethodNamedArgs cenv env lambdaPropagationInfo tpenv args =
    List.mapFoldSquared (TcMethodNamedArg cenv env) (lambdaPropagationInfo, tpenv) args

and TcMethodNamedArg cenv env (lambdaPropagationInfo, tpenv) (CallerNamedArg(id, arg)) =
    // Try to find the lambda propagation info for the corresponding named argument
    let lambdaPropagationInfoForArg =
        [| for (_, namedInfo) in lambdaPropagationInfo ->
             namedInfo |> Array.tryPick (fun namedInfoForArgSet ->
                namedInfoForArgSet |> Array.tryPick (fun (nm, info) ->
                        if nm.idText = id.idText then Some info else None)) |]
        |> Array.map (fun x -> defaultArg x NoInfo)

    let arg', (lambdaPropagationInfo, tpenv) = TcMethodArg cenv env (lambdaPropagationInfo, tpenv) (lambdaPropagationInfoForArg, arg)
    CallerNamedArg(id, arg'), (lambdaPropagationInfo, tpenv)

and TcMethodArg cenv env (lambdaPropagationInfo, tpenv) (lambdaPropagationInfoForArg, CallerArg(argTy, mArg, isOpt, argExpr)) =

    // Apply the F# 3.1 rule for extracting information for lambdas
    //
    // Before we check the argument, check to see if we can propagate info from a called lambda expression into the arguments of a received lambda
    if lambdaPropagationInfoForArg.Length > 0 then
        let allOverloadsAreNotCalledArgMatchesForThisArg =
            lambdaPropagationInfoForArg
            |> Array.forall (function ArgDoesNotMatch | CallerLambdaHasArgTypes _ | NoInfo -> true | CalledArgMatchesType _ -> false)

        if allOverloadsAreNotCalledArgMatchesForThisArg then
            let overloadsWhichAreFuncAtThisPosition = lambdaPropagationInfoForArg |> Array.choose (function CallerLambdaHasArgTypes r -> Some (List.toArray r) | _ -> None)
            if overloadsWhichAreFuncAtThisPosition.Length > 0 then
                let minFuncArity = overloadsWhichAreFuncAtThisPosition |> Array.minBy Array.length |> Array.length
                let prefixOfLambdaArgsForEachOverload = overloadsWhichAreFuncAtThisPosition |> Array.map (Array.take minFuncArity)

                if prefixOfLambdaArgsForEachOverload.Length > 0 then
                    let numLambdaVars = prefixOfLambdaArgsForEachOverload.[0].Length
                    // Fold across the lambda var positions checking if all method overloads imply the same argument type for a lambda variable.
                    // If so, force the caller to have a function type that looks like the calledLambdaArgTy.
                    // The loop variable callerLambdaTyOpt becomes None if something failed.
                    let rec loop callerLambdaTy lambdaVarNum =
                        if lambdaVarNum < numLambdaVars then
                            let calledLambdaArgTy = prefixOfLambdaArgsForEachOverload.[0].[lambdaVarNum]
                            let allRowsGiveSameArgumentType =
                                prefixOfLambdaArgsForEachOverload
                                |> Array.forall (fun row -> typeEquiv cenv.g calledLambdaArgTy row.[lambdaVarNum])

                            if allRowsGiveSameArgumentType then
                                // Force the caller to be a function type.
                                match UnifyFunctionTypeUndoIfFailed cenv env.DisplayEnv mArg callerLambdaTy with
                                | ValueSome (callerLambdaDomainTy, callerLambdaRangeTy) ->
                                    if AddCxTypeEqualsTypeUndoIfFailed env.DisplayEnv cenv.css mArg calledLambdaArgTy callerLambdaDomainTy then
                                        loop callerLambdaRangeTy (lambdaVarNum + 1)
                                | _ -> ()
                    loop argTy 0

    let e', tpenv = TcExpr cenv argTy env tpenv argExpr

    // After we have checked, propagate the info from argument into the overloads that receive it.
    //
    // Filter out methods where an argument doesn't match. This just filters them from lambda propagation but not from
    // later method overload resolution.
    let lambdaPropagationInfo =
        [| for (info, argInfo) in Array.zip lambdaPropagationInfo lambdaPropagationInfoForArg do
              match argInfo with
              | ArgDoesNotMatch _ -> ()
              | NoInfo | CallerLambdaHasArgTypes _ ->
                  yield info
              | CalledArgMatchesType adjustedCalledTy ->
                  if AddCxTypeMustSubsumeTypeMatchingOnlyUndoIfFailed env.DisplayEnv cenv.css mArg adjustedCalledTy argTy then
                     yield info |]

    CallerArg(argTy, mArg, isOpt, e'), (lambdaPropagationInfo, tpenv)

/// Typecheck "new Delegate(fun x y z -> ...)" constructs
and TcNewDelegateThen cenv overallTy env tpenv mDelTy mExprAndArg delegateTy arg atomicFlag delayed =
    let ad = env.eAccessRights
    UnifyTypes cenv env mExprAndArg overallTy delegateTy
    let (SigOfFunctionForDelegate(invokeMethInfo, delArgTys, _, fty)) = GetSigOfFunctionForDelegate cenv.infoReader delegateTy mDelTy ad
    // We pass isInstance = true here because we're checking the rights to access the "Invoke" method
    MethInfoChecks cenv.g cenv.amap true None [] env.eAccessRights mExprAndArg invokeMethInfo
    let args = GetMethodArgs arg
    match args with
    | [farg], [] ->
        let m = arg.Range
        let callerArg, (_, tpenv) = TcMethodArg cenv env (Array.empty, tpenv) (Array.empty, CallerArg(fty, m, false, farg))
        let expr = BuildNewDelegateExpr (None, cenv.g, cenv.amap, delegateTy, invokeMethInfo, delArgTys, callerArg.Expr, fty, m)
        PropagateThenTcDelayed cenv overallTy env tpenv m (MakeApplicableExprNoFlex cenv expr) delegateTy atomicFlag delayed
    | _ ->
        error(Error(FSComp.SR.tcDelegateConstructorMustBePassed(), mExprAndArg))


and bindLetRec (binds: Bindings) m e =
    if isNil binds then
        e
    else
        Expr.LetRec (binds, e, m, Construct.NewFreeVarsCache())

/// Check for duplicate bindings in simple recursive patterns
and CheckRecursiveBindingIds binds =
    let hashOfBinds = new HashSet<string>()

    for (SynBinding.SynBinding(_, _, _, _, _, _, _, b, _, _, m, _)) in binds do
        let nm =
            match b with
            | SynPat.Named(id, _, _, _)
            | SynPat.As(_, SynPat.Named(id, _, _, _), _)
            | SynPat.LongIdent(LongIdentWithDots([id], _), _, _, _, _, _) -> id.idText
            | _ -> ""
        if nm <> "" && not (hashOfBinds.Add nm) then
            error(Duplicate("value", nm, m))

/// Process a sequence of sequentials mixed with iterated lets "let ... in let ... in ..." in a tail recursive way
/// This avoids stack overflow on really large "let" and "letrec" lists
and TcLinearExprs bodyChecker cenv env overallTy tpenv isCompExpr expr cont =
    match expr with
    | SynExpr.Sequential (sp, true, e1, e2, m) when not isCompExpr ->
        let e1', _ = TcStmtThatCantBeCtorBody cenv env tpenv e1
        // tailcall
        let env = ShrinkContext env m e2.Range
        // tailcall
        TcLinearExprs bodyChecker cenv env overallTy tpenv isCompExpr e2 (fun (e2', tpenv) ->
            cont (Expr.Sequential (e1', e2', NormalSeq, sp, m), tpenv))

    | SynExpr.LetOrUse (isRec, isUse, binds, body, m) when not (isUse && isCompExpr) ->
        if isRec then
            // TcLinearExprs processes at most one recursive binding, this is not tailcalling
            CheckRecursiveBindingIds binds
            let binds = List.map (fun x -> RecDefnBindingInfo(ExprContainerInfo, NoNewSlots, ExpressionBinding, x)) binds
            if isUse then errorR(Error(FSComp.SR.tcBindingCannotBeUseAndRec(), m))
            let binds, envinner, tpenv = TcLetrec ErrorOnOverrides cenv env tpenv (binds, m, m)
            let bodyExpr, tpenv = bodyChecker overallTy envinner tpenv body
            let bodyExpr = bindLetRec binds m bodyExpr
            cont (bodyExpr, tpenv)
        else
            // TcLinearExprs processes multiple 'let' bindings in a tail recursive way
            let mkf, envinner, tpenv = TcLetBinding cenv isUse env ExprContainerInfo ExpressionBinding tpenv (binds, m, body.Range)
            let envinner = ShrinkContext envinner m body.Range
            // tailcall
            TcLinearExprs bodyChecker cenv envinner overallTy tpenv isCompExpr body (fun (x, tpenv) ->
                cont (fst (mkf (x, overallTy)), tpenv))

    | SynExpr.IfThenElse (_, synBoolExpr, _, synThenExpr, _, synElseExprOpt, spIfToThen, isRecovery, mIfToThen, m) when not isCompExpr ->
        let boolExpr, tpenv = TcExprThatCantBeCtorBody cenv cenv.g.bool_ty env tpenv synBoolExpr
        let thenExpr, tpenv =
            let env =
                match env.eContextInfo with
                | ContextInfo.ElseBranchResult _ -> { env with eContextInfo = ContextInfo.ElseBranchResult synThenExpr.Range }
                | _ ->
                    match synElseExprOpt with
                    | None -> { env with eContextInfo = ContextInfo.OmittedElseBranch synThenExpr.Range }
                    | _ -> { env with eContextInfo = ContextInfo.IfExpression synThenExpr.Range }

            if not isRecovery && Option.isNone synElseExprOpt then
                UnifyTypes cenv env m cenv.g.unit_ty overallTy

            TcExprThatCanBeCtorBody cenv overallTy env tpenv synThenExpr

        match synElseExprOpt with
        | None ->
            let elseExpr = mkUnit cenv.g mIfToThen
            let spElse = DebugPointForTarget.No  // the fake 'unit' value gets exactly the same range as spIfToThen
            let overallExpr = primMkCond spIfToThen DebugPointForTarget.Yes spElse m overallTy boolExpr thenExpr elseExpr
            cont (overallExpr, tpenv)

        | Some synElseExpr ->
            let env = { env with eContextInfo = ContextInfo.ElseBranchResult synElseExpr.Range }
            // tailcall
            TcLinearExprs bodyChecker cenv env overallTy tpenv isCompExpr synElseExpr (fun (elseExpr, tpenv) ->
                let resExpr = primMkCond spIfToThen DebugPointForTarget.Yes DebugPointForTarget.Yes m overallTy boolExpr thenExpr elseExpr
                cont (resExpr, tpenv))

    | _ ->
        cont (bodyChecker overallTy env tpenv expr)

/// Typecheck and compile pattern-matching constructs
and TcAndPatternCompileMatchClauses mExpr matchm actionOnFailure cenv inputExprOpt inputTy resultTy env tpenv synClauses =
    let clauses, tpenv = TcMatchClauses cenv inputTy resultTy env tpenv synClauses
    let matchVal, expr = CompilePatternForMatchClauses cenv env mExpr matchm true actionOnFailure inputExprOpt inputTy resultTy clauses
    matchVal, expr, tpenv

and TcMatchPattern cenv inputTy env tpenv (pat: SynPat, optWhenExpr: SynExpr option) =
    let m = pat.Range
    let patf', (tpenv, names, _) = TcPat WarnOnUpperCase cenv env None (ValInline.Optional, permitInferTypars, noArgOrRetAttribs, false, None, false) (tpenv, Map.empty, Set.empty) inputTy pat
    let envinner, values, vspecMap = MakeAndPublishSimpleValsForMergedScope cenv env m names
    let optWhenExpr', tpenv =
        match optWhenExpr with
        | Some whenExpr ->
            let guardEnv = { envinner with eContextInfo = ContextInfo.PatternMatchGuard whenExpr.Range }
            let whenExpr', tpenv = TcExpr cenv cenv.g.bool_ty guardEnv tpenv whenExpr
            Some whenExpr', tpenv
        | None -> None, tpenv
    patf' (TcPatPhase2Input (values, true)), optWhenExpr', NameMap.range vspecMap, envinner, tpenv

and TcMatchClauses cenv inputTy resultTy env tpenv clauses =
    let mutable first = true
    let isFirst() = if first then first <- false; true else false
    List.mapFold (fun clause -> TcMatchClause cenv inputTy resultTy env (isFirst()) clause) tpenv clauses

and TcMatchClause cenv inputTy resultTy env isFirst tpenv (SynMatchClause(pat, optWhenExpr, e, patm, spTgt)) =
    let pat', optWhenExpr', vspecs, envinner, tpenv = TcMatchPattern cenv inputTy env tpenv (pat, optWhenExpr)
    let resultEnv = if isFirst then envinner else { envinner with eContextInfo = ContextInfo.FollowingPatternMatchClause e.Range }
    let e', tpenv = TcExprThatCanBeCtorBody cenv resultTy resultEnv tpenv e
    TClause(pat', optWhenExpr', TTarget(vspecs, e', spTgt, None), patm), tpenv

and TcStaticOptimizationConstraint cenv env tpenv c =
    match c with
    | SynStaticOptimizationConstraint.WhenTyparTyconEqualsTycon(tp, ty, m) ->
        if not cenv.g.compilingFslib then
            errorR(Error(FSComp.SR.tcStaticOptimizationConditionalsOnlyForFSharpLibrary(), m))
        let ty', tpenv = TcType cenv NewTyparsOK CheckCxs ItemOccurence.UseInType env tpenv ty
        let tp', tpenv = TcTypar cenv env NewTyparsOK tpenv tp
        TTyconEqualsTycon(mkTyparTy tp', ty'), tpenv
    | SynStaticOptimizationConstraint.WhenTyparIsStruct(tp, m) ->
        if not cenv.g.compilingFslib then
            errorR(Error(FSComp.SR.tcStaticOptimizationConditionalsOnlyForFSharpLibrary(), m))
        let tp', tpenv = TcTypar cenv env NewTyparsOK tpenv tp
        TTyconIsStruct(mkTyparTy tp'), tpenv

/// Emit a conv.i instruction
and mkConvToNativeInt (g: TcGlobals) e m = Expr.Op (TOp.ILAsm ([ AI_conv ILBasicType.DT_I], [ g.nativeint_ty ]), [], [e], m)

/// Fix up the r.h.s. of a 'use x = fixed expr'
and TcAndBuildFixedExpr cenv env (overallPatTy, fixedExpr, overallExprTy, mBinding) =
    warning(PossibleUnverifiableCode mBinding)
    match overallExprTy with
    | ty when isByrefTy cenv.g ty ->
        let okByRef =
            match stripExpr fixedExpr with
            | Expr.Op (op, tyargs, args, _) ->
                    match op, tyargs, args with
                    | TOp.ValFieldGetAddr (rfref, _), _, [_] -> not rfref.Tycon.IsStructOrEnumTycon
                    | TOp.ILAsm ([ I_ldflda fspec], _), _, _ -> fspec.DeclaringType.Boxity = ILBoxity.AsObject
                    | TOp.ILAsm ([ I_ldelema _], _), _, _ -> true
                    | TOp.RefAddrGet _, _, _ -> true
                    | _ -> false
            | _ -> false
        if not okByRef then
            error(Error(FSComp.SR.tcFixedNotAllowed(), mBinding))

        let elemTy = destByrefTy cenv.g overallExprTy
        UnifyTypes cenv env mBinding (mkNativePtrTy cenv.g elemTy) overallPatTy
        mkCompGenLetIn mBinding "pinnedByref" ty fixedExpr (fun (v, ve) ->
            v.SetIsFixed()
            mkConvToNativeInt cenv.g ve mBinding)

    | ty when isStringTy cenv.g ty ->
        let charPtrTy = mkNativePtrTy cenv.g cenv.g.char_ty
        UnifyTypes cenv env mBinding charPtrTy overallPatTy
        //
        //    let ptr: nativeptr<char> =
        //        let pinned s = str
        //        (nativeptr)s + get_OffsettoStringData()

        mkCompGenLetIn mBinding "pinnedString" cenv.g.string_ty fixedExpr (fun (v, ve) ->
            v.SetIsFixed()
            let addrOffset = BuildOffsetToStringData cenv env mBinding
            let stringAsNativeInt = mkConvToNativeInt cenv.g ve mBinding
            let plusOffset = Expr.Op (TOp.ILAsm ([ AI_add ], [ cenv.g.nativeint_ty ]), [], [stringAsNativeInt; addrOffset], mBinding)
            // check for non-null
            mkNullTest cenv.g mBinding ve plusOffset ve)

    | ty when isArray1DTy cenv.g ty ->
        let elemTy = destArrayTy cenv.g overallExprTy
        let elemPtrTy = mkNativePtrTy cenv.g elemTy
        UnifyTypes cenv env mBinding elemPtrTy overallPatTy

        // let ptr: nativeptr<elem> =
        //   let tmpArray: elem[] = arr
        //   if nonNull tmpArray then
        //      if tmpArray.Length <> 0 then
        //         let pinned tmpArrayByref: byref<elem> = &arr.[0]
        //         (nativeint) tmpArrayByref
        //      else
        //         (nativeint) 0
        //   else
        //      (nativeint) 0
        //
        mkCompGenLetIn mBinding "tmpArray" overallExprTy fixedExpr (fun (_, ve) ->
            // This is &arr.[0]
            let elemZeroAddress = mkArrayElemAddress cenv.g (false, ILReadonly.NormalAddress, false, ILArrayShape.SingleDimensional, elemTy, [ve; mkInt32 cenv.g mBinding 0], mBinding)
            // check for non-null and non-empty
            let zero = mkConvToNativeInt cenv.g (mkInt32 cenv.g mBinding 0) mBinding
            // This is arr.Length
            let arrayLengthExpr = mkCallArrayLength cenv.g mBinding elemTy ve
            mkNullTest cenv.g mBinding ve
                (mkNullTest cenv.g mBinding arrayLengthExpr
                    (mkCompGenLetIn mBinding "pinnedByref" (mkByrefTy cenv.g elemTy) elemZeroAddress (fun (v, ve) ->
                       v.SetIsFixed()
                       (mkConvToNativeInt cenv.g ve mBinding)))
                    zero)
                zero)

    | _ -> error(Error(FSComp.SR.tcFixedNotAllowed(), mBinding))


/// Binding checking code, for all bindings including let bindings, let-rec bindings, member bindings and object-expression bindings and
and TcNormalizedBinding declKind (cenv: cenv) env tpenv overallTy safeThisValOpt safeInitInfo (enclosingDeclaredTypars, (ExplicitTyparInfo(_, declaredTypars, _) as explicitTyparInfo)) bind =
    let envinner = AddDeclaredTypars NoCheckForDuplicateTypars (enclosingDeclaredTypars@declaredTypars) env

    match bind with

    | NormalizedBinding(vis, bkind, isInline, isMutable, attrs, doc, _, valSynData, pat, NormalizedBindingRhs(spatsL, rtyOpt, rhsExpr), mBinding, spBind) ->
        let (SynValData(memberFlagsOpt, _, _)) = valSynData

        let callerName =
            match declKind, bkind, pat with
            | ExpressionBinding, _, _ -> envinner.eCallerMemberName
            | _, _, (SynPat.Named(name, _, _, _) | SynPat.As(_, SynPat.Named(name, _, _, _), _)) ->
                match memberFlagsOpt with
                | Some memberFlags ->
                    match memberFlags.MemberKind with
                    | SynMemberKind.PropertyGet | SynMemberKind.PropertySet | SynMemberKind.PropertyGetSet -> Some(name.idText.Substring 4)
                    | SynMemberKind.ClassConstructor -> Some(".ctor")
                    | SynMemberKind.Constructor -> Some(".ctor")
                    | _ -> Some(name.idText)
                | _ -> Some(name.idText)
            | ClassLetBinding false, SynBindingKind.Do, _ -> Some(".ctor")
            | ClassLetBinding true, SynBindingKind.Do, _ -> Some(".cctor")
            | ModuleOrMemberBinding, SynBindingKind.StandaloneExpression, _ -> Some(".cctor")
            | _, _, _ -> envinner.eCallerMemberName

        let envinner = {envinner with eCallerMemberName = callerName }

        let attrTgt = DeclKind.AllowedAttribTargets memberFlagsOpt declKind

        let isFixed, rhsExpr, overallPatTy, overallExprTy =
            match rhsExpr with
            | SynExpr.Fixed (e, _) -> true, e, NewInferenceType(), overallTy
            | e -> false, e, overallTy, overallTy

        // Check the attributes of the binding, parameters or return value
        let TcAttrs tgt isRet attrs =
            // For all but attributes positioned at the return value, disallow implicitly
            // targeting the return value.
            let tgtEx = if isRet then enum 0 else AttributeTargets.ReturnValue
            let attrs, _ = TcAttributesMaybeFailEx false cenv envinner tgt tgtEx attrs
            if attrTgt = enum 0 && not (isNil attrs) then
                errorR(Error(FSComp.SR.tcAttributesAreNotPermittedOnLetBindings(), mBinding))
            attrs

        // Rotate [<return:...>] from binding to return value
        // Also patch the syntactic representation
        let retAttribs, valAttribs, valSynData =
            let attribs = TcAttrs attrTgt false attrs
            let rotRetSynAttrs, rotRetAttribs, valAttribs =
                // Do not rotate if some attrs fail to typecheck...
                if attribs.Length <> attrs.Length then [], [], attribs
                else attribs
                     |> List.zip attrs
                     |> List.partition(function | (_, Attrib(_, _, _, _, _, Some ts, _)) -> ts &&& AttributeTargets.ReturnValue <> enum 0 | _ -> false)
                     |> fun (r, v) -> (List.map fst r, List.map snd r, List.map snd v)
            let retAttribs =
                match rtyOpt with
                | Some (SynBindingReturnInfo(_, _, Attributes retAttrs)) ->
                    rotRetAttribs @ TcAttrs AttributeTargets.ReturnValue true retAttrs
                | None -> rotRetAttribs
            let valSynData =
                match rotRetSynAttrs with
                | [] -> valSynData
                | {Range=mHead} :: _ ->
                let (SynValData(valMf, SynValInfo(args, SynArgInfo(attrs, opt, retId)), valId)) = valSynData
                in SynValData(valMf, SynValInfo(args, SynArgInfo({Attributes=rotRetSynAttrs; Range=mHead} :: attrs, opt, retId)), valId)
            retAttribs, valAttribs, valSynData

        let isVolatile = HasFSharpAttribute cenv.g cenv.g.attrib_VolatileFieldAttribute valAttribs

        let inlineFlag = ComputeInlineFlag memberFlagsOpt isInline isMutable mBinding

        let argAttribs = 
            spatsL |> List.map (SynInfo.InferSynArgInfoFromSimplePats >> List.map (SynInfo.AttribsOfArgData >> TcAttrs AttributeTargets.Parameter false))

        // Assert the return type of an active pattern. A [<return:Struct>] attribute may be used on a partial active pattern.
        let isStructRetTy = HasFSharpAttribute cenv.g cenv.g.attrib_StructAttribute retAttribs

        let argAndRetAttribs = ArgAndRetAttribs(argAttribs, retAttribs)

        // See RFC FS-1087, the 'Zero' method of a builder may have 'DefaultValueAttribute' indicating it should
        // always be used for empty branches of if/then/else and others
        let isZeroMethod =
            match declKind, pat with
            | ModuleOrMemberBinding, SynPat.Named(id, _, _, _) when id.idText = "Zero" -> 
                match memberFlagsOpt with
                | Some memberFlags ->
                    match memberFlags.MemberKind with
                    | SynMemberKind.Member -> true
                    | _ -> false
                | _ -> false 
            | _ -> false

        if HasFSharpAttribute cenv.g cenv.g.attrib_DefaultValueAttribute valAttribs && not isZeroMethod then 
            errorR(Error(FSComp.SR.tcDefaultValueAttributeRequiresVal(), mBinding))

        let isThreadStatic = isThreadOrContextStatic cenv.g valAttribs
        if isThreadStatic then errorR(DeprecatedThreadStaticBindingWarning mBinding)

        if isVolatile then
            match declKind with
            | ClassLetBinding(_) -> ()
            | _ -> errorR(Error(FSComp.SR.tcVolatileOnlyOnClassLetBindings(), mBinding))

            if (not isMutable || isThreadStatic) then
                errorR(Error(FSComp.SR.tcVolatileFieldsMustBeMutable(), mBinding))

        if isFixed && (declKind <> ExpressionBinding || isInline || isMutable) then
            errorR(Error(FSComp.SR.tcFixedNotAllowed(), mBinding))

        if (not declKind.CanBeDllImport || (match memberFlagsOpt with Some memberFlags -> memberFlags.IsInstance | _ -> false)) &&
            HasFSharpAttributeOpt cenv.g cenv.g.attrib_DllImportAttribute valAttribs
        then
            errorR(Error(FSComp.SR.tcDllImportNotAllowed(), mBinding))

        if Option.isNone memberFlagsOpt && HasFSharpAttribute cenv.g cenv.g.attrib_ConditionalAttribute valAttribs then
            errorR(Error(FSComp.SR.tcConditionalAttributeRequiresMembers(), mBinding))

        if HasFSharpAttribute cenv.g cenv.g.attrib_EntryPointAttribute valAttribs then
            if Option.isSome memberFlagsOpt then
                errorR(Error(FSComp.SR.tcEntryPointAttributeRequiresFunctionInModule(), mBinding))
            else
                UnifyTypes cenv env mBinding overallPatTy (mkArrayType cenv.g cenv.g.string_ty --> cenv.g.int_ty)

        if isMutable && isInline then errorR(Error(FSComp.SR.tcMutableValuesCannotBeInline(), mBinding))

        if isMutable && not (isNil declaredTypars) then errorR(Error(FSComp.SR.tcMutableValuesMayNotHaveGenericParameters(), mBinding))

        let explicitTyparInfo = if isMutable then dontInferTypars else explicitTyparInfo

        if isMutable && not (isNil spatsL) then errorR(Error(FSComp.SR.tcMutableValuesSyntax(), mBinding))

        let isInline =
            if isInline && isNil spatsL && isNil declaredTypars then
                errorR(Error(FSComp.SR.tcOnlyFunctionsCanBeInline(), mBinding))
                false
            else
                isInline

        let compgen = false

        // Use the syntactic arity if we're defining a function
        let (SynValData(_, valSynInfo, _)) = valSynData
        let partialValReprInfo = TranslateTopValSynInfo mBinding (TcAttributes cenv env) valSynInfo

        // Check the pattern of the l.h.s. of the binding
        let tcPatPhase2, (tpenv, nameToPrelimValSchemeMap, _) =
            TcPat AllIdsOK cenv envinner (Some partialValReprInfo) (inlineFlag, explicitTyparInfo, argAndRetAttribs, isMutable, vis, compgen) (tpenv, NameMap.empty, Set.empty) overallPatTy pat

        // Add active pattern result names to the environment
        let apinfoOpt =
            match NameMap.range nameToPrelimValSchemeMap with
            | [PrelimValScheme1(id, _, ty, _, _, _, _, _, _, _, _) ] ->
                match ActivePatternInfoOfValName id.idText id.idRange with
                | Some apinfo -> Some (apinfo, ty, id.idRange)
                | None -> None
            | _ -> None

        // Add active pattern result names to the environment
        let envinner =
            match apinfoOpt with
            | Some (apinfo, apOverallTy, m) ->
                if Option.isSome memberFlagsOpt || (not apinfo.IsTotal && apinfo.ActiveTags.Length > 1) then
                    error(Error(FSComp.SR.tcInvalidActivePatternName(), mBinding))

                apinfo.ActiveTagsWithRanges |> List.iteri (fun i (_tag, tagRange) ->
                    let item = Item.ActivePatternResult(apinfo, apOverallTy, i, tagRange)
                    CallNameResolutionSink cenv.tcSink (tagRange, env.NameEnv, item, emptyTyparInst, ItemOccurence.Binding, env.AccessRights))

                { envinner with eNameResEnv = AddActivePatternResultTagsToNameEnv apinfo envinner.eNameResEnv apOverallTy m }
            | None ->
                envinner

        // Now tc the r.h.s.
        // If binding a ctor then set the ugly counter that permits us to write ctor expressions on the r.h.s.
        let isCtor = (match memberFlagsOpt with Some memberFlags -> memberFlags.MemberKind = SynMemberKind.Constructor | _ -> false)

        // At each module binding, dive into the expression to check for syntax errors and suppress them if they show.
        // Don't do this for lambdas, because we always check for suppression for all lambda bodies in TcIteratedLambdas
        let rhsExprChecked, tpenv =
            let atTopNonLambdaDefn =
                DeclKind.IsModuleOrMemberOrExtensionBinding declKind &&
                (match rhsExpr with SynExpr.Lambda _ -> false | _ -> true) &&
                synExprContainsError rhsExpr

            conditionallySuppressErrorReporting atTopNonLambdaDefn (fun () ->

                // Save the arginfos away to match them up in the lambda
                let (PartialValReprInfo(argInfos, _)) = partialValReprInfo
                let envinner = { envinner with eLambdaArgInfos = argInfos }

                if isCtor then TcExprThatIsCtorBody (safeThisValOpt, safeInitInfo) cenv overallExprTy envinner tpenv rhsExpr
                else TcExprThatCantBeCtorBody cenv overallExprTy envinner tpenv rhsExpr)

        if bkind = SynBindingKind.StandaloneExpression && not cenv.isScript then
            UnifyUnitType cenv env mBinding overallPatTy rhsExprChecked |> ignore<bool>

        // Fix up the r.h.s. expression for 'fixed'
        let rhsExprChecked =
            if isFixed then TcAndBuildFixedExpr cenv env (overallPatTy, rhsExprChecked, overallExprTy, mBinding)
            else rhsExprChecked

        match apinfoOpt with
        | Some (apinfo, apOverallTy, _) ->
            let activePatResTys = NewInferenceTypes apinfo.ActiveTags
            let _, apReturnTy = stripFunTy cenv.g apOverallTy
            if isStructRetTy && apinfo.IsTotal then
                errorR(Error(FSComp.SR.tcInvalidStructReturn(), mBinding))
            if isStructRetTy then
                checkLanguageFeatureError cenv.g.langVersion LanguageFeature.StructActivePattern mBinding
            UnifyTypes cenv env mBinding (apinfo.ResultType cenv.g rhsExpr.Range activePatResTys isStructRetTy) apReturnTy
        | None ->
            if isStructRetTy then
                errorR(Error(FSComp.SR.tcInvalidStructReturn(), mBinding))

        // Check other attributes
        let hasLiteralAttr, literalValue = TcLiteral cenv overallExprTy env tpenv (valAttribs, rhsExpr)

        if hasLiteralAttr then
            if isThreadStatic then
                errorR(Error(FSComp.SR.tcIllegalAttributesForLiteral(), mBinding))
            if isMutable then
                errorR(Error(FSComp.SR.tcLiteralCannotBeMutable(), mBinding))
            if isInline then
                errorR(Error(FSComp.SR.tcLiteralCannotBeInline(), mBinding))
            if not (isNil declaredTypars) then
                errorR(Error(FSComp.SR.tcLiteralCannotHaveGenericParameters(), mBinding))

        CheckedBindingInfo(inlineFlag, valAttribs, doc, tcPatPhase2, explicitTyparInfo, nameToPrelimValSchemeMap, rhsExprChecked, argAndRetAttribs, overallPatTy, mBinding, spBind, compgen, literalValue, isFixed), tpenv

and TcLiteral cenv overallTy env tpenv (attrs, synLiteralValExpr) =
    let hasLiteralAttr = HasFSharpAttribute cenv.g cenv.g.attrib_LiteralAttribute attrs
    if hasLiteralAttr then
        let literalValExpr, _ = TcExpr cenv overallTy env tpenv synLiteralValExpr
        match EvalLiteralExprOrAttribArg cenv.g literalValExpr with
        | Expr.Const (c, _, ty) ->
            if c = Const.Zero && isStructTy cenv.g ty then
                warning(Error(FSComp.SR.tcIllegalStructTypeForConstantExpression(), synLiteralValExpr.Range))
                false, None
            else
                true, Some c
        | _ ->
            errorR(Error(FSComp.SR.tcInvalidConstantExpression(), synLiteralValExpr.Range))
            true, Some Const.Unit

    else hasLiteralAttr, None

and TcBindingTyparDecls alwaysRigid cenv env tpenv (ValTyparDecls(synTypars, synTyparConstraints, infer)) =
    let declaredTypars = TcTyparDecls cenv env synTypars
    let envinner = AddDeclaredTypars CheckForDuplicateTypars declaredTypars env
    let tpenv = TcTyparConstraints cenv NoNewTypars CheckCxs ItemOccurence.UseInType envinner tpenv synTyparConstraints

    let rigidCopyOfDeclaredTypars =
        if alwaysRigid then
            declaredTypars |> List.iter (fun tp -> SetTyparRigid env.DisplayEnv tp.Range tp)
            declaredTypars
        else
            let rigidCopyOfDeclaredTypars = copyTypars declaredTypars
            // The type parameters used to check rigidity after inference are marked rigid straight away
            rigidCopyOfDeclaredTypars |> List.iter (fun tp -> SetTyparRigid env.DisplayEnv tp.Range tp)
            // The type parameters using during inference will be marked rigid after inference
            declaredTypars |> List.iter (fun tp -> tp.SetRigidity TyparRigidity.WillBeRigid)
            rigidCopyOfDeclaredTypars

    ExplicitTyparInfo(rigidCopyOfDeclaredTypars, declaredTypars, infer), tpenv

and TcNonrecBindingTyparDecls cenv env tpenv bind =
    let (NormalizedBinding(_, _, _, _, _, _, synTyparDecls, _, _, _, _, _)) = bind
    TcBindingTyparDecls true cenv env tpenv synTyparDecls

and TcNonRecursiveBinding declKind cenv env tpenv ty b =
    let b = BindingNormalization.NormalizeBinding ValOrMemberBinding cenv env b
    let explicitTyparInfo, tpenv = TcNonrecBindingTyparDecls cenv env tpenv b
    TcNormalizedBinding declKind cenv env tpenv ty None NoSafeInitInfo ([], explicitTyparInfo) b

//-------------------------------------------------------------------------
// TcAttribute*
// *Ex means the function accepts attribute targets that must be explicit
//------------------------------------------------------------------------

and TcAttributeEx canFail cenv (env: TcEnv) attrTgt attrEx (synAttr: SynAttribute) =
    let (LongIdentWithDots(tycon, _)) = synAttr.TypeName
    let arg = synAttr.ArgExpr
    let targetIndicator = synAttr.Target
    let isAppliedToGetterOrSetter = synAttr.AppliesToGetterAndSetter
    let mAttr = synAttr.Range
    let (typath, tyid) = List.frontAndBack tycon
    let tpenv = emptyUnscopedTyparEnv

    // if we're checking an attribute that was applied directly to a getter or a setter, then
    // what we're really checking against is a method, not a property
    let attrTgt = if isAppliedToGetterOrSetter then ((attrTgt ^^^ AttributeTargets.Property) ||| AttributeTargets.Method) else attrTgt
    let ty, tpenv =
        let try1 n =
            let tyid = mkSynId tyid.idRange n
            let tycon = (typath @ [tyid])
            let ad = env.eAccessRights
            match ResolveTypeLongIdent cenv.tcSink cenv.nameResolver ItemOccurence.UseInAttribute OpenQualified env.eNameResEnv ad tycon TypeNameResolutionStaticArgsInfo.DefiniteEmpty PermitDirectReferenceToGeneratedType.No with
            | Exception err -> raze err
            | _ -> success(TcTypeAndRecover cenv NoNewTypars CheckCxs ItemOccurence.UseInAttribute env tpenv (SynType.App(SynType.LongIdent(LongIdentWithDots(tycon, [])), None, [], [], None, false, mAttr)) )
        ForceRaise ((try1 (tyid.idText + "Attribute")) |> ResultOrException.otherwise (fun () -> (try1 tyid.idText)))

    let ad = env.eAccessRights

    if not (IsTypeAccessible cenv.g cenv.amap mAttr ad ty) then errorR(Error(FSComp.SR.tcTypeIsInaccessible(), mAttr))

    let tcref = tcrefOfAppTy cenv.g ty

    let conditionalCallDefineOpt = TryFindTyconRefStringAttribute cenv.g mAttr cenv.g.attrib_ConditionalAttribute tcref

    match conditionalCallDefineOpt, cenv.conditionalDefines with
    | Some d, Some defines when not (List.contains d defines) ->
        [], false
    | _ ->
         // REVIEW: take notice of inherited?
        let validOn, _inherited =
            let validOnDefault = 0x7fff
            let inheritedDefault = true
            if tcref.IsILTycon then
                let tdef = tcref.ILTyconRawMetadata
                let tref = cenv.g.attrib_AttributeUsageAttribute.TypeRef

                match TryDecodeILAttribute tref tdef.CustomAttrs with
                | Some ([ILAttribElem.Int32 validOn ], named) ->
                    let inherited =
                        match List.tryPick (function ("Inherited", _, _, ILAttribElem.Bool res) -> Some res | _ -> None) named with
                        | None -> inheritedDefault
                        | Some x -> x
                    (validOn, inherited)
                | Some ([ILAttribElem.Int32 validOn; ILAttribElem.Bool _allowMultiple; ILAttribElem.Bool inherited ], _) ->
                    (validOn, inherited)
                | _ ->
                    (validOnDefault, inheritedDefault)
            else
                match (TryFindFSharpAttribute cenv.g cenv.g.attrib_AttributeUsageAttribute tcref.Attribs) with
                | Some(Attrib(_, _, [ AttribInt32Arg validOn ], _, _, _, _)) ->
                    (validOn, inheritedDefault)
                | Some(Attrib(_, _, [ AttribInt32Arg validOn
                                      AttribBoolArg(_allowMultiple)
                                      AttribBoolArg inherited], _, _, _, _)) ->
                    (validOn, inherited)
                | Some _ ->
                    warning(Error(FSComp.SR.tcUnexpectedConditionInImportedAssembly(), mAttr))
                    (validOnDefault, inheritedDefault)
                | _ ->
                    (validOnDefault, inheritedDefault)
        let possibleTgts = enum validOn &&& attrTgt
        let directedTgts =
            match targetIndicator with
            | Some id when id.idText = "assembly" -> AttributeTargets.Assembly
            | Some id when id.idText = "module" -> AttributeTargets.Module
            | Some id when id.idText = "return" -> AttributeTargets.ReturnValue
            | Some id when id.idText = "field" -> AttributeTargets.Field
            | Some id when id.idText = "property" -> AttributeTargets.Property
            | Some id when id.idText = "method" -> AttributeTargets.Method
            | Some id when id.idText = "param" -> AttributeTargets.Parameter
            | Some id when id.idText = "type" -> AttributeTargets.TyconDecl
            | Some id when id.idText = "constructor" -> AttributeTargets.Constructor
            | Some id when id.idText = "event" -> AttributeTargets.Event
            | Some id ->
                errorR(Error(FSComp.SR.tcUnrecognizedAttributeTarget(), id.idRange))
                possibleTgts
            // mask explicit targets
            | _ -> possibleTgts &&& ~~~ attrEx
        let constrainedTgts = possibleTgts &&& directedTgts
        if constrainedTgts = enum 0 then
            if (directedTgts = AttributeTargets.Assembly || directedTgts = AttributeTargets.Module) then
                error(Error(FSComp.SR.tcAttributeIsNotValidForLanguageElementUseDo(), mAttr))
            else
                error(Error(FSComp.SR.tcAttributeIsNotValidForLanguageElement(), mAttr))

        match ResolveObjectConstructor cenv.nameResolver env.DisplayEnv mAttr ad ty with
        | Exception _ when canFail -> [ ], true
        | res ->
        let item = ForceRaise res
        if not (ExistsHeadTypeInEntireHierarchy cenv.g cenv.amap mAttr ty cenv.g.tcref_System_Attribute) then warning(Error(FSComp.SR.tcTypeDoesNotInheritAttribute(), mAttr))
        let attrib =
            match item with
            | Item.CtorGroup(methodName, minfos) ->
                let meths = minfos |> List.map (fun minfo -> minfo, None)
                let afterResolution = ForNewConstructors cenv.tcSink env tyid.idRange methodName minfos
                let (expr, attributeAssignedNamedItems, _), _ =
                  TcMethodApplication true cenv env tpenv None [] mAttr mAttr methodName None ad PossiblyMutates false meths afterResolution NormalValUse [arg] (NewInferenceType ()) []

                UnifyTypes cenv env mAttr ty (tyOfExpr cenv.g expr)

                let mkAttribExpr e =
                    AttribExpr(e, EvalLiteralExprOrAttribArg cenv.g e)

                let namedAttribArgMap =
                  attributeAssignedNamedItems |> List.map (fun (CallerNamedArg(id, CallerArg(argtyv, m, isOpt, callerArgExpr))) ->
                    if isOpt then error(Error(FSComp.SR.tcOptionalArgumentsCannotBeUsedInCustomAttribute(), m))
                    let m = callerArgExpr.Range
                    let setterItem, _ = ResolveLongIdentInType cenv.tcSink cenv.nameResolver env.NameEnv LookupKind.Expr m ad id IgnoreOverrides TypeNameResolutionInfo.Default ty
                    let nm, isProp, argty =
                      match setterItem with
                      | Item.Property (_, [pinfo]) ->
                          if not pinfo.HasSetter then
                            errorR(Error(FSComp.SR.tcPropertyCannotBeSet0(), m))
                          id.idText, true, pinfo.GetPropertyType(cenv.amap, m)
                      | Item.ILField finfo ->
                          CheckILFieldInfoAccessible cenv.g cenv.amap m ad finfo
                          CheckILFieldAttributes cenv.g finfo m
                          id.idText, false, finfo.FieldType(cenv.amap, m)
                      | Item.RecdField rfinfo when not rfinfo.IsStatic ->
                          CheckRecdFieldInfoAttributes cenv.g rfinfo m |> CommitOperationResult
                          CheckRecdFieldInfoAccessible cenv.amap m ad rfinfo
                          // This uses the F# backend name mangling of fields....
                          let nm = ComputeFieldName rfinfo.Tycon rfinfo.RecdField
                          nm, false, rfinfo.FieldType
                      | _ ->
                          errorR(Error(FSComp.SR.tcPropertyOrFieldNotFoundInAttribute(), m))
                          id.idText, false, cenv.g.unit_ty
                    let propNameItem = Item.SetterArg(id, setterItem)
                    CallNameResolutionSink cenv.tcSink (id.idRange, env.NameEnv, propNameItem, emptyTyparInst, ItemOccurence.Use, ad)

                    AddCxTypeMustSubsumeType ContextInfo.NoContext env.DisplayEnv cenv.css m NoTrace argty argtyv

                    AttribNamedArg(nm, argty, isProp, mkAttribExpr callerArgExpr))

                match expr with
                | Expr.Op (TOp.ILCall (_, _, isStruct, _, _, _, _, ilMethRef, [], [], _), [], args, m) ->
                    if isStruct then error (Error(FSComp.SR.tcCustomAttributeMustBeReferenceType(), m))
                    if args.Length <> ilMethRef.ArgTypes.Length then error (Error(FSComp.SR.tcCustomAttributeArgumentMismatch(), m))
                    let args = args |> List.map mkAttribExpr
                    Attrib(tcref, ILAttrib ilMethRef, args, namedAttribArgMap, isAppliedToGetterOrSetter, Some constrainedTgts, m)

                | Expr.App ((InnerExprPat(ExprValWithPossibleTypeInst(vref, _, _, _))), _, _, args, _) ->
                    let args = args |> List.collect (function Expr.Const (Const.Unit, _, _) -> [] | expr -> tryDestRefTupleExpr expr) |> List.map mkAttribExpr
                    Attrib(tcref, FSAttrib vref, args, namedAttribArgMap, isAppliedToGetterOrSetter, Some constrainedTgts, mAttr)

                | _ ->
                    error (Error(FSComp.SR.tcCustomAttributeMustInvokeConstructor(), mAttr))

            | _ ->
                error(Error(FSComp.SR.tcAttributeExpressionsMustBeConstructorCalls(), mAttr))

        [ (constrainedTgts, attrib) ], false

and TcAttributesWithPossibleTargetsEx canFail cenv env attrTgt attrEx synAttribs =

    (false, synAttribs) ||> List.collectFold (fun didFail synAttrib ->
        try
            let attribsAndTargets, didFail2 = TcAttributeEx canFail cenv env attrTgt attrEx synAttrib

            // This is where we place any checks that completely exclude the use of some particular
            // attributes from F#.
            let attribs = List.map snd attribsAndTargets
            if HasFSharpAttribute cenv.g cenv.g.attrib_TypeForwardedToAttribute attribs ||
               HasFSharpAttribute cenv.g cenv.g.attrib_CompilationArgumentCountsAttribute attribs ||
               HasFSharpAttribute cenv.g cenv.g.attrib_CompilationMappingAttribute attribs then
                errorR(Error(FSComp.SR.tcUnsupportedAttribute(), synAttrib.Range))

            attribsAndTargets, didFail || didFail2

        with e ->
            errorRecovery e synAttrib.Range
            [], false)

and TcAttributesMaybeFailEx canFail cenv env attrTgt attrEx synAttribs =
    let attribsAndTargets, didFail = TcAttributesWithPossibleTargetsEx canFail cenv env attrTgt attrEx synAttribs
    attribsAndTargets |> List.map snd, didFail

and TcAttributesWithPossibleTargets canFail cenv env attrTgt synAttribs =
    TcAttributesWithPossibleTargetsEx canFail cenv env attrTgt (enum 0) synAttribs

and TcAttribute canFail cenv (env: TcEnv) attrTgt (synAttr: SynAttribute) =
    TcAttributeEx canFail cenv env attrTgt (enum 0) synAttr

and TcAttributesMaybeFail canFail cenv env attrTgt synAttribs =
    TcAttributesMaybeFailEx canFail cenv env attrTgt (enum 0) synAttribs

and TcAttributesCanFail cenv env attrTgt synAttribs =
    let attrs, didFail = TcAttributesMaybeFail true cenv env attrTgt synAttribs
    attrs, (fun () -> if didFail then TcAttributes cenv env attrTgt synAttribs else attrs)

and TcAttributes cenv env attrTgt synAttribs =
    TcAttributesMaybeFail false cenv env attrTgt synAttribs |> fst

//-------------------------------------------------------------------------
// TcLetBinding
//------------------------------------------------------------------------

and TcLetBinding cenv isUse env containerInfo declKind tpenv (synBinds, synBindsRange, scopem) =

    // Typecheck all the bindings...
    let checkedBinds, tpenv = List.mapFold (fun tpenv b -> TcNonRecursiveBinding declKind cenv env tpenv (NewInferenceType ()) b) tpenv synBinds
    let (ContainerInfo(altActualParent, _)) = containerInfo

    // Canonicalize constraints prior to generalization
    let denv = env.DisplayEnv
    ConstraintSolver.CanonicalizePartialInferenceProblem cenv.css denv synBindsRange
        (checkedBinds |> List.collect (fun tbinfo ->
            let (CheckedBindingInfo(_, _, _, _, explicitTyparInfo, _, _, _, tauTy, _, _, _, _, _)) = tbinfo
            let (ExplicitTyparInfo(_, declaredTypars, _)) = explicitTyparInfo
            let maxInferredTypars = (freeInTypeLeftToRight cenv.g false tauTy)
            declaredTypars @ maxInferredTypars))

    let lazyFreeInEnv = lazy (GeneralizationHelpers.ComputeUngeneralizableTypars env)

    // Generalize the bindings...
    (((fun x -> x), env, tpenv), checkedBinds) ||> List.fold (fun (buildExpr, env, tpenv) tbinfo ->
        let (CheckedBindingInfo(inlineFlag, attrs, doc, tcPatPhase2, explicitTyparInfo, nameToPrelimValSchemeMap, rhsExpr, _, tauTy, m, spBind, _, literalValue, isFixed)) = tbinfo
        let enclosingDeclaredTypars = []
        let (ExplicitTyparInfo(_, declaredTypars, canInferTypars)) = explicitTyparInfo
        let allDeclaredTypars = enclosingDeclaredTypars @ declaredTypars
        let generalizedTypars, prelimValSchemes2 =
            let canInferTypars = GeneralizationHelpers. ComputeCanInferExtraGeneralizableTypars (containerInfo.ParentRef, canInferTypars, None)

            let maxInferredTypars = freeInTypeLeftToRight cenv.g false tauTy

            let generalizedTypars =
                if isNil maxInferredTypars && isNil allDeclaredTypars then
                   []
                else
                   let freeInEnv = lazyFreeInEnv.Force()
                   let canConstrain = GeneralizationHelpers.CanGeneralizeConstrainedTyparsForDecl declKind
                   GeneralizationHelpers.ComputeAndGeneralizeGenericTypars
                       (cenv, denv, m, freeInEnv, canInferTypars, canConstrain, inlineFlag, Some rhsExpr, allDeclaredTypars, maxInferredTypars, tauTy, false)

            let prelimValSchemes2 = GeneralizeVals cenv denv enclosingDeclaredTypars generalizedTypars nameToPrelimValSchemeMap

            generalizedTypars, prelimValSchemes2

        // REVIEW: this scopes generalized type variables. Ensure this is handled properly
        // on all other paths.
        let tpenv = HideUnscopedTypars generalizedTypars tpenv
        let valSchemes = NameMap.map (UseCombinedArity cenv.g declKind rhsExpr) prelimValSchemes2
        let values = MakeAndPublishVals cenv env (altActualParent, false, declKind, ValNotInRecScope, valSchemes, attrs, doc, literalValue)
        let checkedPat = tcPatPhase2 (TcPatPhase2Input (values, true))
        let prelimRecValues = NameMap.map fst values

        // Now bind the r.h.s. to the l.h.s.
        let rhsExpr = mkTypeLambda m generalizedTypars (rhsExpr, tauTy)

        match checkedPat with
        // Don't introduce temporary or 'let' for 'match against wild' or 'match against unit'

        | (TPat_wild _ | TPat_const (Const.Unit, _)) when not isUse && not isFixed && isNil generalizedTypars ->
            let mkSequentialBind (tm, tmty) = (mkSequential DebugPointAtSequential.SuppressNeither m rhsExpr tm, tmty)
            (buildExpr >> mkSequentialBind, env, tpenv)
        | _ ->

        // nice: don't introduce awful temporary for r.h.s. in the 99% case where we know what we're binding it to
        let patternInputTmp, checkedPat2 =
            match checkedPat with
            // nice: don't introduce awful temporary for r.h.s. in the 99% case where we know what we're binding it to
            | TPat_as (pat, PBind(v, TypeScheme(generalizedTypars', _)), _)
                when List.lengthsEqAndForall2 typarRefEq generalizedTypars generalizedTypars' ->

                    v, pat
                    //Op (LValueOp (LByrefGet,x),[],[],C:\GitHub\dsyme\visualfsharp\a.fs (15,42--15,43) IsSynthetic=false)

            | _ when inlineFlag.MustInline ->
                error(Error(FSComp.SR.tcInvalidInlineSpecification(), m))

            | TPat_query _ when HasFSharpAttribute cenv.g cenv.g.attrib_LiteralAttribute attrs ->
                error(Error(FSComp.SR.tcLiteralAttributeCannotUseActivePattern(), m))

            | _ ->

                let tmp, _ = mkCompGenLocal m "patternInput" (generalizedTypars +-> tauTy)

                if isUse then
                    let isDiscarded = match checkedPat with TPat_wild _ -> true | _ -> false
                    if not isDiscarded then
                        errorR(Error(FSComp.SR.tcInvalidUseBinding(), m))
                    else
                        ErrorLogger.checkLanguageFeatureError cenv.g.langVersion Features.LanguageFeature.UseBindingValueDiscard checkedPat.Range

                elif isFixed then
                    errorR(Error(FSComp.SR.tcInvalidUseBinding(), m))

                // If the overall declaration is declaring statics or a module value, then force the patternInputTmp to also
                // have representation as module value.
                if (DeclKind.MustHaveArity declKind) then
                    AdjustValToTopVal tmp altActualParent (InferArityOfExprBinding cenv.g AllowTypeDirectedDetupling.Yes tmp rhsExpr)

                tmp, checkedPat

        // Add the bind "let patternInputTmp = rhsExpr" to the bodyExpr we get from mkPatBind
        let mkRhsBind (bodyExpr, bodyExprTy) =
            let letExpr = mkLet spBind m patternInputTmp rhsExpr bodyExpr
            letExpr, bodyExprTy

        let allValsDefinedByPattern = NameMap.range prelimRecValues

        // Add the compilation of the pattern to the bodyExpr we get from mkCleanup
        let mkPatBind (bodyExpr, bodyExprTy) =
            let valsDefinedByMatching = ListSet.remove valEq patternInputTmp allValsDefinedByPattern
            let clauses = [TClause(checkedPat2, None, TTarget(valsDefinedByMatching, bodyExpr, DebugPointForTarget.No, None), m)]
            let matchx = CompilePatternForMatch cenv env m m true ThrowIncompleteMatchException (patternInputTmp, generalizedTypars, Some rhsExpr) clauses tauTy bodyExprTy
            let matchx = if (DeclKind.ConvertToLinearBindings declKind) then LinearizeTopMatch cenv.g altActualParent matchx else matchx
            matchx, bodyExprTy

        // Add the dispose of any "use x = ..." to bodyExpr
        let mkCleanup (bodyExpr, bodyExprTy) =
            if isUse && not isFixed then
                let isDiscarded = match checkedPat2 with TPat_wild _ -> true | _ -> false
                let allValsDefinedByPattern = if isDiscarded then [patternInputTmp] else allValsDefinedByPattern
                (allValsDefinedByPattern, (bodyExpr, bodyExprTy)) ||> List.foldBack (fun v (bodyExpr, bodyExprTy) ->
                    AddCxTypeMustSubsumeType ContextInfo.NoContext denv cenv.css v.Range NoTrace cenv.g.system_IDisposable_ty v.Type
                    let cleanupE = BuildDisposableCleanup cenv env m v
                    mkTryFinally cenv.g (bodyExpr, cleanupE, m, bodyExprTy, DebugPointAtTry.Body, DebugPointAtFinally.No), bodyExprTy)
            else
                (bodyExpr, bodyExprTy)

        let envInner = AddLocalValMap cenv.g cenv.tcSink scopem prelimRecValues env

        ((buildExpr >> mkCleanup >> mkPatBind >> mkRhsBind), envInner, tpenv))

/// Return binds corresponding to the linearised let-bindings.
/// This reveals the bound items, e.g. when the lets occur in incremental object defns.
/// RECAP:
///   The LHS of let-bindings are patterns.
///   These patterns could fail, e.g. "let Some x = ...".
///   So let bindings could contain a fork at a match construct, with one branch being the match failure.
///   If bindings are linearised, then this fork is pushed to the RHS.
///   In this case, the let bindings type check to a sequence of bindings.
and TcLetBindings cenv env containerInfo declKind tpenv (binds, bindsm, scopem) =
    assert(DeclKind.ConvertToLinearBindings declKind)
    let mkf, env, tpenv = TcLetBinding cenv false env containerInfo declKind tpenv (binds, bindsm, scopem)
    let unite = mkUnit cenv.g bindsm
    let expr, _ = mkf (unite, cenv.g.unit_ty)
    let rec stripLets acc = function
        | Expr.Let (bind, body, m, _) -> stripLets (TMDefLet(bind, m) :: acc) body
        | Expr.Sequential (e1, e2, NormalSeq, _, m) -> stripLets (TMDefDo(e1, m) :: acc) e2
        | Expr.Const (Const.Unit, _, _) -> List.rev acc
        | _ -> failwith "TcLetBindings: let sequence is non linear. Maybe a LHS pattern was not linearised?"
    let binds = stripLets [] expr
    binds, env, tpenv

and CheckMemberFlags optIntfSlotTy newslotsOK overridesOK memberFlags m =
    if newslotsOK = NoNewSlots && memberFlags.IsDispatchSlot then
      errorR(Error(FSComp.SR.tcAbstractMembersIllegalInAugmentation(), m))
    if overridesOK = ErrorOnOverrides && memberFlags.MemberKind = SynMemberKind.Constructor then
      errorR(Error(FSComp.SR.tcConstructorsIllegalInAugmentation(), m))
    if overridesOK = WarnOnOverrides && memberFlags.IsOverrideOrExplicitImpl && Option.isNone optIntfSlotTy then
      warning(OverrideInIntrinsicAugmentation m)
    if overridesOK = ErrorOnOverrides && memberFlags.IsOverrideOrExplicitImpl then
      error(Error(FSComp.SR.tcMethodOverridesIllegalHere(), m))

/// Apply the pre-assumed knowledge available to type inference prior to looking at
/// the _body_ of the binding. For example, in a letrec we may assume this knowledge
/// for each binding in the letrec prior to any type inference. This might, for example,
/// tell us the type of the arguments to a recursive function.
and ApplyTypesFromArgumentPatterns (cenv, env, optArgsOK, ty, m, tpenv, NormalizedBindingRhs (pushedPats, retInfoOpt, e), memberFlagsOpt: SynMemberFlags option) =
    match pushedPats with
    | [] ->
        match retInfoOpt with
        | None -> ()
        | Some (SynBindingReturnInfo (retInfoTy, m, _)) ->
            let retInfoTy, _ = TcTypeAndRecover cenv NewTyparsOK CheckCxs ItemOccurence.UseInType env tpenv retInfoTy
            UnifyTypes cenv env m ty retInfoTy
        // Property setters always have "unit" return type
        match memberFlagsOpt with
        | Some memFlags when memFlags.MemberKind = SynMemberKind.PropertySet ->
            UnifyTypes cenv env m ty cenv.g.unit_ty
        | _ -> ()

    | pushedPat :: morePushedPats ->
        let domainTy, resultTy = UnifyFunctionType None cenv env.DisplayEnv m ty
        // We apply the type information from the patterns by type checking the
        // "simple" patterns against 'domainTy'. They get re-typechecked later.
        ignore (TcSimplePats cenv optArgsOK CheckCxs domainTy env (tpenv, Map.empty, Set.empty) pushedPat)
        ApplyTypesFromArgumentPatterns (cenv, env, optArgsOK, resultTy, m, tpenv, NormalizedBindingRhs (morePushedPats, retInfoOpt, e), memberFlagsOpt)


/// Check if the type annotations and inferred type information in a value give a
/// full and complete generic type for a value. If so, enable generic recursion.
and ComputeIsComplete enclosingDeclaredTypars declaredTypars ty =
    Zset.isEmpty (List.fold (fun acc v -> Zset.remove v acc)
                                  (freeInType CollectAllNoCaching ty).FreeTypars
                                  (enclosingDeclaredTypars@declaredTypars))

/// Determine if a uniquely-identified-abstract-slot exists for an override member (or interface member implementation) based on the information available
/// at the syntactic definition of the member (i.e. prior to type inference). If so, we know the expected signature of the override, and the full slotsig
/// it implements. Apply the inferred slotsig.
and ApplyAbstractSlotInference (cenv: cenv) (envinner: TcEnv) (bindingTy, m, synTyparDecls, declaredTypars, memberId, tcrefObjTy, renaming, _objTy, optIntfSlotTy, valSynData, memberFlags: SynMemberFlags, attribs) =

    let ad = envinner.eAccessRights
    let typToSearchForAbstractMembers =
        match optIntfSlotTy with
        | Some (ty, abstractSlots) ->
            // The interface type is in terms of the type's type parameters.
            // We need a signature in terms of the values' type parameters.
            ty, Some abstractSlots
        | None ->
            tcrefObjTy, None

    // Determine if a uniquely-identified-override exists based on the information
    // at the member signature. If so, we know the type of this member, and the full slotsig
    // it implements. Apply the inferred slotsig.
    if memberFlags.IsOverrideOrExplicitImpl then

        // for error detection, we want to compare finality when testing for equivalence
        let methInfosEquivByNameAndSig meths =
            match meths with
            | [] -> false
            | head :: tail ->
                tail |> List.forall (MethInfosEquivByNameAndSig EraseNone false cenv.g cenv.amap m head)

        match memberFlags.MemberKind with
        | SynMemberKind.Member ->
             let dispatchSlots, dispatchSlotsArityMatch =
                 GetAbstractMethInfosForSynMethodDecl(cenv.infoReader, ad, memberId, m, typToSearchForAbstractMembers, valSynData)

             let uniqueAbstractMethSigs =
                 match dispatchSlots with
                 | [] ->
                     errorR(Error(FSComp.SR.tcNoMemberFoundForOverride(), memberId.idRange))
                     []

                 | slots ->
                     match dispatchSlotsArityMatch with
                     | meths when methInfosEquivByNameAndSig meths -> meths
                     | [] ->
                         let details =
                             slots
                             |> Seq.map (NicePrint.stringOfMethInfo cenv.infoReader m envinner.DisplayEnv)
                             |> Seq.map (sprintf "%s   %s" System.Environment.NewLine)
                             |> String.concat ""

                         errorR(Error(FSComp.SR.tcOverrideArityMismatch details, memberId.idRange))
                         []
                     | _ -> [] // check that method to override is sealed is located at CheckOverridesAreAllUsedOnce (typrelns.fs)
                      // We hit this case when it is ambiguous which abstract method is being implemented.



             // If we determined a unique member then utilize the type information from the slotsig
             let declaredTypars =
                 match uniqueAbstractMethSigs with
                 | uniqueAbstractMeth :: _ ->

                     let uniqueAbstractMeth = uniqueAbstractMeth.Instantiate(cenv.amap, m, renaming)

                     let typarsFromAbsSlotAreRigid, typarsFromAbsSlot, argTysFromAbsSlot, retTyFromAbsSlot =
                         FreshenAbstractSlot cenv.g cenv.amap m synTyparDecls uniqueAbstractMeth

                     let declaredTypars = (if typarsFromAbsSlotAreRigid then typarsFromAbsSlot else declaredTypars)

                     let absSlotTy = mkMethodTy cenv.g argTysFromAbsSlot retTyFromAbsSlot

                     UnifyTypes cenv envinner m bindingTy absSlotTy
                     declaredTypars
                 | _ -> declaredTypars

                 // Retained to ensure use of an FSComp.txt entry, can be removed at a later date: errorR(Error(FSComp.SR.tcDefaultAmbiguous(), memberId.idRange))

             // What's the type containing the abstract slot we're implementing? Used later on in MakeMemberDataAndMangledNameForMemberVal.
             // This type must be in terms of the enclosing type's formal type parameters, hence the application of revRenaming

             let optInferredImplSlotTys =
                 match optIntfSlotTy with
                 | Some (x, _) -> [x]
                 | None -> uniqueAbstractMethSigs |> List.map (fun x -> x.ApparentEnclosingType)

             optInferredImplSlotTys, declaredTypars

        | SynMemberKind.PropertyGet
        | SynMemberKind.PropertySet as k ->
           let dispatchSlots = GetAbstractPropInfosForSynPropertyDecl(cenv.infoReader, ad, memberId, m, typToSearchForAbstractMembers, k, valSynData)

           // Only consider those abstract slots where the get/set flags match the value we're defining
           let dispatchSlots =
               dispatchSlots
               |> List.filter (fun pinfo ->
                     (pinfo.HasGetter && k=SynMemberKind.PropertyGet) ||
                     (pinfo.HasSetter && k=SynMemberKind.PropertySet))

           // Find the unique abstract slot if it exists
           let uniqueAbstractPropSigs =
               match dispatchSlots with
               | [] when not (CompileAsEvent cenv.g attribs) ->
                   errorR(Error(FSComp.SR.tcNoPropertyFoundForOverride(), memberId.idRange))
                   []
               | [uniqueAbstractProp] -> [uniqueAbstractProp]
               | _ ->
                   // We hit this case when it is ambiguous which abstract property is being implemented.
                   []

           // If we determined a unique member then utilize the type information from the slotsig
           uniqueAbstractPropSigs |> List.iter (fun uniqueAbstractProp ->

               let kIsGet = (k = SynMemberKind.PropertyGet)

               if not (if kIsGet then uniqueAbstractProp.HasGetter else uniqueAbstractProp.HasSetter) then
                   error(Error(FSComp.SR.tcAbstractPropertyMissingGetOrSet(if kIsGet then "getter" else "setter"), memberId.idRange))

               let uniqueAbstractMeth = if kIsGet then uniqueAbstractProp.GetterMethod else uniqueAbstractProp.SetterMethod

               let uniqueAbstractMeth = uniqueAbstractMeth.Instantiate(cenv.amap, m, renaming)

               let _, typarsFromAbsSlot, argTysFromAbsSlot, retTyFromAbsSlot =
                    FreshenAbstractSlot cenv.g cenv.amap m synTyparDecls uniqueAbstractMeth

               if not (isNil typarsFromAbsSlot) then
                   errorR(InternalError("Unexpected generic property", memberId.idRange))

               let absSlotTy =
                   if (memberFlags.MemberKind = SynMemberKind.PropertyGet)
                   then mkMethodTy cenv.g argTysFromAbsSlot retTyFromAbsSlot
                   else
                     match argTysFromAbsSlot with
                     | [argTysFromAbsSlot] -> mkRefTupledTy cenv.g argTysFromAbsSlot --> cenv.g.unit_ty
                     | _ ->
                         error(Error(FSComp.SR.tcInvalidSignatureForSet(), memberId.idRange))
                         retTyFromAbsSlot --> cenv.g.unit_ty

               UnifyTypes cenv envinner m bindingTy absSlotTy)

           // What's the type containing the abstract slot we're implementing? Used later on in MakeMemberDataAndMangledNameForMemberVal.
           // This type must be in terms of the enclosing type's formal type parameters, hence the application of revRenaming.

           let optInferredImplSlotTys =
               match optIntfSlotTy with
               | Some (x, _) -> [ x ]
               | None -> uniqueAbstractPropSigs |> List.map (fun pinfo -> pinfo.ApparentEnclosingType)

           optInferredImplSlotTys, declaredTypars

        | _ ->
           match optIntfSlotTy with
           | Some (x, _) -> [x], declaredTypars
           | None -> [], declaredTypars

    else

       [], declaredTypars

and CheckForNonAbstractInterface declKind tcref (memberFlags: SynMemberFlags) m =
    if isInterfaceTyconRef tcref then
        if memberFlags.MemberKind = SynMemberKind.ClassConstructor then
            error(Error(FSComp.SR.tcStaticInitializersIllegalInInterface(), m))
        elif memberFlags.MemberKind = SynMemberKind.Constructor then
            error(Error(FSComp.SR.tcObjectConstructorsIllegalInInterface(), m))
        elif memberFlags.IsOverrideOrExplicitImpl then
            error(Error(FSComp.SR.tcMemberOverridesIllegalInInterface(), m))
        elif not (declKind=ExtrinsicExtensionBinding || memberFlags.IsDispatchSlot ) then
            error(Error(FSComp.SR.tcConcreteMembersIllegalInInterface(), m))

//-------------------------------------------------------------------------
// TcLetrec - AnalyzeAndMakeAndPublishRecursiveValue s
//------------------------------------------------------------------------

and AnalyzeRecursiveStaticMemberOrValDecl
       (cenv,
        envinner: TcEnv,
        tpenv,
        declKind,
        newslotsOK,
        overridesOK,
        tcrefContainerInfo,
        vis1,
        id: Ident,
        vis2,
        declaredTypars,
        memberFlagsOpt,
        thisIdOpt,
        bindingAttribs,
        valSynInfo,
        ty,
        bindingRhs,
        mBinding,
        explicitTyparInfo) =

    let vis = CombineVisibilityAttribs vis1 vis2 mBinding

    // Check if we're defining a member, in which case generate the internal unique
    // name for the member and the information about which type it is augmenting

    match tcrefContainerInfo, memberFlagsOpt with
    | (Some(MemberOrValContainerInfo(tcref, optIntfSlotTy, baseValOpt, _safeInitInfo, declaredTyconTypars)), Some memberFlags) ->
        assert (Option.isNone optIntfSlotTy)

        CheckMemberFlags None newslotsOK overridesOK memberFlags id.idRange
        CheckForNonAbstractInterface declKind tcref memberFlags id.idRange

        if memberFlags.MemberKind = SynMemberKind.Constructor && tcref.Deref.IsExceptionDecl then
            error(Error(FSComp.SR.tcConstructorsDisallowedInExceptionAugmentation(), id.idRange))

        let isExtrinsic = (declKind = ExtrinsicExtensionBinding)
        let _, enclosingDeclaredTypars, _, objTy, thisTy = FreshenObjectArgType cenv mBinding TyparRigidity.WillBeRigid tcref isExtrinsic declaredTyconTypars
        let envinner = AddDeclaredTypars CheckForDuplicateTypars enclosingDeclaredTypars envinner
        let envinner = MakeInnerEnvForTyconRef envinner tcref isExtrinsic

        let safeThisValOpt, baseValOpt =
            match memberFlags.MemberKind with

            // Explicit struct or class constructor
            | SynMemberKind.Constructor ->
                // A fairly adhoc place to put this check
                if tcref.IsStructOrEnumTycon && (match valSynInfo with SynValInfo([[]], _) -> true | _ -> false) then
                    errorR(Error(FSComp.SR.tcStructsCannotHaveConstructorWithNoArguments(), mBinding))

                if not tcref.IsFSharpObjectModelTycon then
                    errorR(Error(FSComp.SR.tcConstructorsIllegalForThisType(), id.idRange))

                let safeThisValOpt = MakeAndPublishSafeThisVal cenv envinner thisIdOpt thisTy

                // baseValOpt is the 'base' variable associated with the inherited portion of a class
                // It is declared once on the 'inheritedTys clause, but a fresh binding is made for
                // each member that may use it.
                let baseValOpt =
                    match GetSuperTypeOfType cenv.g cenv.amap mBinding objTy with
                    | Some superTy -> MakeAndPublishBaseVal cenv envinner (match baseValOpt with None -> None | Some v -> Some v.Id) superTy
                    | None -> None

                let domainTy = NewInferenceType ()

                // This is the type we pretend a constructor has, because its implementation must ultimately appear to return a value of the given type
                // This is somewhat awkward later in codegen etc.
                UnifyTypes cenv envinner mBinding ty (domainTy --> objTy)

                safeThisValOpt, baseValOpt

            | _ ->
                None, None

        let memberInfo =
            let isExtrinsic = (declKind = ExtrinsicExtensionBinding)
            MakeMemberDataAndMangledNameForMemberVal(cenv.g, tcref, isExtrinsic, bindingAttribs, [], memberFlags, valSynInfo, id, false)

        envinner, tpenv, id, None, Some memberInfo, vis, vis2, safeThisValOpt, enclosingDeclaredTypars, baseValOpt, explicitTyparInfo, bindingRhs, declaredTypars

    // non-member bindings. How easy.
    | _ ->
        envinner, tpenv, id, None, None, vis, vis2, None, [], None, explicitTyparInfo, bindingRhs, declaredTypars


and AnalyzeRecursiveInstanceMemberDecl
       (cenv,
        envinner: TcEnv,
        tpenv,
        declKind,
        synTyparDecls,
        valSynInfo,
        explicitTyparInfo: ExplicitTyparInfo,
        newslotsOK,
        overridesOK,
        vis1,
        thisId,
        memberId: Ident,
        toolId: Ident option,
        bindingAttribs,
        vis2,
        tcrefContainerInfo,
        memberFlagsOpt,
        ty,
        bindingRhs,
        mBinding) =

    let vis = CombineVisibilityAttribs vis1 vis2 mBinding
    let (ExplicitTyparInfo(_, declaredTypars, infer)) = explicitTyparInfo
    match tcrefContainerInfo, memberFlagsOpt with
     // Normal instance members.
     | Some(MemberOrValContainerInfo(tcref, optIntfSlotTy, baseValOpt, _safeInitInfo, declaredTyconTypars)), Some memberFlags ->

         CheckMemberFlags optIntfSlotTy newslotsOK overridesOK memberFlags mBinding

         if Option.isSome vis && memberFlags.IsOverrideOrExplicitImpl then
            errorR(Error(FSComp.SR.tcOverridesCannotHaveVisibilityDeclarations(), memberId.idRange))

         // Syntactically push the "this" variable across to be a lambda on the right
         let bindingRhs = PushOnePatternToRhs cenv true (mkSynThisPatVar thisId) bindingRhs

         // The type being augmented tells us the type of 'this'
         let isExtrinsic = (declKind = ExtrinsicExtensionBinding)
         let tcrefObjTy, enclosingDeclaredTypars, renaming, objTy, thisTy = FreshenObjectArgType cenv mBinding TyparRigidity.WillBeRigid tcref isExtrinsic declaredTyconTypars

         let envinner = AddDeclaredTypars CheckForDuplicateTypars enclosingDeclaredTypars envinner

         // If private, the member's accessibility is related to 'tcref'
         let envinner = MakeInnerEnvForTyconRef envinner tcref isExtrinsic

         let baseValOpt = if tcref.IsFSharpObjectModelTycon then baseValOpt else None

         // Apply the known type of 'this'
         let bindingTy = NewInferenceType ()
         UnifyTypes cenv envinner mBinding ty (thisTy --> bindingTy)

         CheckForNonAbstractInterface declKind tcref memberFlags memberId.idRange

         // Determine if a uniquely-identified-override List.exists based on the information
         // at the member signature. If so, we know the type of this member, and the full slotsig
         // it implements. Apply the inferred slotsig.
         let optInferredImplSlotTys, declaredTypars =
             ApplyAbstractSlotInference cenv envinner (bindingTy, mBinding, synTyparDecls, declaredTypars, memberId, tcrefObjTy, renaming, objTy, optIntfSlotTy, valSynInfo, memberFlags, bindingAttribs)

         // Update the ExplicitTyparInfo to reflect the declaredTypars inferred from the abstract slot
         let explicitTyparInfo = ExplicitTyparInfo(declaredTypars, declaredTypars, infer)

         // baseValOpt is the 'base' variable associated with the inherited portion of a class
         // It is declared once on the 'inheritedTys clause, but a fresh binding is made for
         // each member that may use it.
         let baseValOpt =
             match GetSuperTypeOfType cenv.g cenv.amap mBinding objTy with
             | Some superTy -> MakeAndPublishBaseVal cenv envinner (match baseValOpt with None -> None | Some v -> Some v.Id) superTy
             | None -> None

         let memberInfo = MakeMemberDataAndMangledNameForMemberVal(cenv.g, tcref, isExtrinsic, bindingAttribs, optInferredImplSlotTys, memberFlags, valSynInfo, memberId, false)
         // This line factored in the 'get' or 'set' as the identifier for a property declaration using "with get () = ... and set v = ..."
         // It has been removed from FSharp.Compiler.Service because we want the property name to be the location of
         // the definition of these symbols.
         //
         // See https://github.com/fsharp/FSharp.Compiler.Service/issues/79.
         //let memberId = match toolId with Some tid -> ident(memberId.idText, tid.idRange) | None -> memberId
         //ignore toolId

         envinner, tpenv, memberId, toolId, Some memberInfo, vis, vis2, None, enclosingDeclaredTypars, baseValOpt, explicitTyparInfo, bindingRhs, declaredTypars
     | _ ->
         error(Error(FSComp.SR.tcRecursiveBindingsWithMembersMustBeDirectAugmentation(), mBinding))

and AnalyzeRecursiveDecl
        (cenv,
         envinner,
         tpenv,
         declKind,
         synTyparDecls,
         declaredTypars,
         thisIdOpt,
         valSynInfo,
         explicitTyparInfo,
         newslotsOK,
         overridesOK,
         vis1,
         declPattern,
         bindingAttribs,
         tcrefContainerInfo,
         memberFlagsOpt,
         ty,
         bindingRhs,
         mBinding) =

    let rec analyzeRecursiveDeclPat tpenv p =
        match p with
        | SynPat.FromParseError(pat', _) -> analyzeRecursiveDeclPat tpenv pat'
        | SynPat.Typed(pat', cty, _) ->
            let cty', tpenv = TcTypeAndRecover cenv NewTyparsOK CheckCxs ItemOccurence.UseInType envinner tpenv cty
            UnifyTypes cenv envinner mBinding ty cty'
            analyzeRecursiveDeclPat tpenv pat'
        | SynPat.Attrib(_pat', _attribs, m) ->
            error(Error(FSComp.SR.tcAttributesInvalidInPatterns(), m))
            //analyzeRecursiveDeclPat pat'

        // This is for the construct 'let rec x = ... and do ... and y = ...' (DEPRECATED IN pars.mly )
        //
        // Also for
        //    module rec M =
        //        printfn "hello" // side effects in recursive modules
        //        let x = 1
        | SynPat.Const (SynConst.Unit, m) | SynPat.Wild m ->
             let id = ident (cenv.niceNameGen.FreshCompilerGeneratedName("doval", m), m)
             analyzeRecursiveDeclPat tpenv (SynPat.Named (id, false, None, m))

        | SynPat.Named (id, _, vis2, _) ->
            AnalyzeRecursiveStaticMemberOrValDecl
                (cenv, envinner, tpenv, declKind,
                 newslotsOK, overridesOK, tcrefContainerInfo,
                 vis1, id, vis2, declaredTypars,
                 memberFlagsOpt, thisIdOpt, bindingAttribs,
                 valSynInfo, ty, bindingRhs, mBinding, explicitTyparInfo)

        | SynPat.InstanceMember(thisId, memberId, toolId, vis2, _) ->
            AnalyzeRecursiveInstanceMemberDecl
                (cenv, envinner, tpenv, declKind,
                 synTyparDecls, valSynInfo, explicitTyparInfo, newslotsOK,
                 overridesOK, vis1, thisId, memberId, toolId,
                 bindingAttribs, vis2, tcrefContainerInfo,
                 memberFlagsOpt, ty, bindingRhs, mBinding)

        | _ -> error(Error(FSComp.SR.tcOnlySimplePatternsInLetRec(), mBinding))

    analyzeRecursiveDeclPat tpenv declPattern


/// This is a major routine that generates the Val for a recursive binding
/// prior to the analysis of the definition of the binding. This includes
/// members of all flavours (including properties, implicit class constructors
/// and overrides). At this point we perform override inference, to infer
/// which method we are overriding, in order to add constraints to the
/// implementation of the method.
and AnalyzeAndMakeAndPublishRecursiveValue
        overridesOK
        isGeneratedEventVal
        cenv
        (env: TcEnv)
        (tpenv, recBindIdx)
        (NormalizedRecBindingDefn(containerInfo, newslotsOK, declKind, binding)) =

    // Pull apart the inputs
    let (NormalizedBinding(vis1, bindingKind, isInline, isMutable, bindingSynAttribs, bindingXmlDoc, synTyparDecls, valSynData, declPattern, bindingRhs, mBinding, spBind)) = binding
    let (NormalizedBindingRhs(_, _, bindingExpr)) = bindingRhs
    let (SynValData(memberFlagsOpt, valSynInfo, thisIdOpt)) = valSynData
    let (ContainerInfo(altActualParent, tcrefContainerInfo)) = containerInfo

    let attrTgt = DeclKind.AllowedAttribTargets memberFlagsOpt declKind

    // Check the attributes on the declaration
    let bindingAttribs = TcAttributes cenv env attrTgt bindingSynAttribs

    // Allocate the type inference variable for the inferred type
    let ty = NewInferenceType ()


    let inlineFlag = ComputeInlineFlag memberFlagsOpt isInline isMutable mBinding
    if isMutable then errorR(Error(FSComp.SR.tcOnlyRecordFieldsAndSimpleLetCanBeMutable(), mBinding))


    // Typecheck the typar decls, if any
    let explicitTyparInfo, tpenv = TcBindingTyparDecls false cenv env tpenv synTyparDecls
    let (ExplicitTyparInfo(_, declaredTypars, _)) = explicitTyparInfo
    let envinner = AddDeclaredTypars CheckForDuplicateTypars declaredTypars env

    // OK, analyze the declaration and return lots of information about it
    let envinner, tpenv, bindingId, toolIdOpt, memberInfoOpt, vis, vis2, safeThisValOpt, enclosingDeclaredTypars, baseValOpt, explicitTyparInfo, bindingRhs, declaredTypars =

        AnalyzeRecursiveDecl (cenv, envinner, tpenv, declKind, synTyparDecls, declaredTypars, thisIdOpt, valSynInfo, explicitTyparInfo,
                              newslotsOK, overridesOK, vis1, declPattern, bindingAttribs, tcrefContainerInfo,
                              memberFlagsOpt, ty, bindingRhs, mBinding)

    let optArgsOK = Option.isSome memberFlagsOpt

    // Assert the types given in the argument patterns
    ApplyTypesFromArgumentPatterns(cenv, envinner, optArgsOK, ty, mBinding, tpenv, bindingRhs, memberFlagsOpt)

    // Do the type annotations give the full and complete generic type?
    // If so, generic recursion can be used when using this type.
    let isComplete = ComputeIsComplete enclosingDeclaredTypars declaredTypars ty

    // NOTE: The type scheme here is normally not 'complete'!!!! The type is more or less just a type variable at this point.
    // NOTE: top arity, type and typars get fixed-up after inference
    let prelimTyscheme = TypeScheme(enclosingDeclaredTypars@declaredTypars, ty)
    let partialValReprInfo = TranslateTopValSynInfo mBinding (TcAttributes cenv envinner) valSynInfo
    let topValInfo = UseSyntacticArity declKind prelimTyscheme partialValReprInfo
    let hasDeclaredTypars = not (List.isEmpty declaredTypars)
    let prelimValScheme = ValScheme(bindingId, prelimTyscheme, topValInfo, memberInfoOpt, false, inlineFlag, NormalVal, vis, false, false, false, hasDeclaredTypars)

    // Check the literal r.h.s., if any
    let _, literalValue = TcLiteral cenv ty envinner tpenv (bindingAttribs, bindingExpr)

    let extraBindings, extraValues, tpenv, recBindIdx =
       let extraBindings =
          [ for extraBinding in EventDeclarationNormalization.GenerateExtraBindings cenv (bindingAttribs, binding) do
               yield (NormalizedRecBindingDefn(containerInfo, newslotsOK, declKind, extraBinding)) ]
       let res, (tpenv, recBindIdx) = List.mapFold (AnalyzeAndMakeAndPublishRecursiveValue overridesOK true cenv env) (tpenv, recBindIdx) extraBindings
       let extraBindings, extraValues = List.unzip res
       List.concat extraBindings, List.concat extraValues, tpenv, recBindIdx

    // Create the value
    let vspec = MakeAndPublishVal cenv envinner (altActualParent, false, declKind, ValInRecScope isComplete, prelimValScheme, bindingAttribs, bindingXmlDoc, literalValue, isGeneratedEventVal)

    // Suppress hover tip for "get" and "set" at property definitions, where toolId <> bindingId
    match toolIdOpt with
    | Some tid when not tid.idRange.IsSynthetic && not (Range.equals tid.idRange bindingId.idRange) ->
        let item = Item.Value (mkLocalValRef vspec)
        CallNameResolutionSink cenv.tcSink (tid.idRange, env.NameEnv, item, emptyTyparInst, ItemOccurence.RelatedText, env.eAccessRights)
    | _ -> ()

    let mangledId = ident(vspec.LogicalName, vspec.Range)
    // Reconstitute the binding with the unique name
    let revisedBinding = NormalizedBinding (vis1, bindingKind, isInline, isMutable, bindingSynAttribs, bindingXmlDoc, synTyparDecls, valSynData, mkSynPatVar vis2 mangledId, bindingRhs, mBinding, spBind)

    // Create the RecursiveBindingInfo to use in later phases
    let rbinfo =
        let safeInitInfo =
            match tcrefContainerInfo with
            | Some(MemberOrValContainerInfo(_, _, _, safeInitInfo, _)) -> safeInitInfo
            | _ -> NoSafeInitInfo

        RecursiveBindingInfo(recBindIdx, containerInfo, enclosingDeclaredTypars, inlineFlag, vspec, explicitTyparInfo, partialValReprInfo, memberInfoOpt, baseValOpt, safeThisValOpt, safeInitInfo, vis, ty, declKind)

    let recBindIdx = recBindIdx + 1

    // Done - add the declared name to the List.map and return the bundle for use by TcLetrec
    let primaryBinding: PreCheckingRecursiveBinding =
        { SyntacticBinding = revisedBinding
          RecBindingInfo = rbinfo }

    ((primaryBinding :: extraBindings), (vspec :: extraValues)), (tpenv, recBindIdx)

and AnalyzeAndMakeAndPublishRecursiveValues overridesOK cenv env tpenv binds =
    let recBindIdx = 0
    let res, tpenv = List.mapFold (AnalyzeAndMakeAndPublishRecursiveValue overridesOK false cenv env) (tpenv, recBindIdx) binds
    let bindings, values = List.unzip res
    List.concat bindings, List.concat values, tpenv


//-------------------------------------------------------------------------
// TcLetrecBinding
//-------------------------------------------------------------------------

and TcLetrecBinding
         (cenv, envRec: TcEnv, scopem, extraGeneralizableTypars: Typars, reqdThisValTyOpt: TType option)

         // The state of the left-to-right iteration through the bindings
         (envNonRec: TcEnv,
          generalizedRecBinds: PostGeneralizationRecursiveBinding list,
          preGeneralizationRecBinds: PreGeneralizationRecursiveBinding list,
          tpenv,
          uncheckedRecBindsTable: Map<Stamp, PreCheckingRecursiveBinding>)

         // This is the actual binding to check
         (rbind: PreCheckingRecursiveBinding) =

    let (RecursiveBindingInfo(_, _, enclosingDeclaredTypars, _, vspec, explicitTyparInfo, _, _, baseValOpt, safeThisValOpt, safeInitInfo, _, tau, declKind)) = rbind.RecBindingInfo

    let allDeclaredTypars = enclosingDeclaredTypars @ rbind.RecBindingInfo.DeclaredTypars

    // Notes on FSharp 1.0, 3187:
    //    - Progressively collect the "eligible for early generalization" set of bindings -- DONE
    //    - After checking each binding, check this set to find generalizable bindings
    //    - The only reason we can't generalize is if a binding refers to type variables to which
    //      additional constraints may be applied as part of checking a later binding
    //    - Compute the set by iteratively knocking out bindings that refer to type variables free in later bindings
    //    - Implementation notes:
    //         - Generalize by remap/substitution
    //         - Pass in "free in later bindings" by passing in the set of inference variables for the bindings, i.e. the binding types
    //         - For classes the bindings will include all members in a recursive group of types
    //

    //  Example 1:
    //    let f() = g()   f: unit -> ?b
    //    and g() = 1     f: unit -> int, can generalize (though now monomorphic)

    //  Example 2:
    //    let f() = g()   f: unit -> ?b
    //    and g() = []    f: unit -> ?c list, can generalize

    //  Example 3:
    //    let f() = []   f: unit -> ?b, can generalize immediately
    //    and g() = []
    let envRec = Option.foldBack (AddLocalVal cenv.g cenv.tcSink scopem) baseValOpt envRec
    let envRec = Option.foldBack (AddLocalVal cenv.g cenv.tcSink scopem) safeThisValOpt envRec

    // Members can access protected members of parents of the type, and private members in the type
    let envRec = MakeInnerEnvForMember envRec vspec

    let checkedBind, tpenv =
        TcNormalizedBinding declKind cenv envRec tpenv tau safeThisValOpt safeInitInfo (enclosingDeclaredTypars, explicitTyparInfo) rbind.SyntacticBinding

    (try UnifyTypes cenv envRec vspec.Range (allDeclaredTypars +-> tau) vspec.Type
     with e -> error (Recursion(envRec.DisplayEnv, vspec.Id, tau, vspec.Type, vspec.Range)))

    // Inside the incremental class syntax we assert the type of the 'this' variable to be precisely the same type as the
    // this variable for the implicit class constructor. For static members, we assert the type variables associated
    // for the class to be identical to those used for the implicit class constructor and the static class constructor.
    match reqdThisValTyOpt with
    | None -> ()
    | Some reqdThisValTy ->
        let reqdThisValTy, actualThisValTy, rangeForCheck =
            match GetInstanceMemberThisVariable (vspec, checkedBind.Expr) with
            | None ->
                let reqdThisValTy = if isByrefTy cenv.g reqdThisValTy then destByrefTy cenv.g reqdThisValTy else reqdThisValTy
                let enclosingTyconRef = tcrefOfAppTy cenv.g reqdThisValTy
                reqdThisValTy, (mkAppTy enclosingTyconRef (List.map mkTyparTy enclosingDeclaredTypars)), vspec.Range
            | Some thisVal ->
                reqdThisValTy, thisVal.Type, thisVal.Range
        if not (AddCxTypeEqualsTypeUndoIfFailed envRec.DisplayEnv cenv.css rangeForCheck actualThisValTy reqdThisValTy) then
            errorR (Error(FSComp.SR.tcNonUniformMemberUse vspec.DisplayName, vspec.Range))

    let preGeneralizationRecBind =
        { RecBindingInfo = rbind.RecBindingInfo
          CheckedBinding= checkedBind
          ExtraGeneralizableTypars= extraGeneralizableTypars }

    // Remove one binding from the unchecked list
    let uncheckedRecBindsTable =
        assert (uncheckedRecBindsTable.ContainsKey rbind.RecBindingInfo.Val.Stamp)
        uncheckedRecBindsTable.Remove rbind.RecBindingInfo.Val.Stamp

    // Add one binding to the candidates eligible for generalization
    let preGeneralizationRecBinds = (preGeneralizationRecBind :: preGeneralizationRecBinds)

    // Incrementally generalize as many bindings as we can
    TcIncrementalLetRecGeneralization cenv scopem (envNonRec, generalizedRecBinds, preGeneralizationRecBinds, tpenv, uncheckedRecBindsTable)

and TcIncrementalLetRecGeneralization cenv scopem
         // The state of the left-to-right iteration through the bindings
         (envNonRec: TcEnv,
          generalizedRecBinds: PostGeneralizationRecursiveBinding list,
          preGeneralizationRecBinds: PreGeneralizationRecursiveBinding list,
          tpenv,
          uncheckedRecBindsTable: Map<Stamp, PreCheckingRecursiveBinding>) =

    let denv = envNonRec.DisplayEnv
    // recompute the free-in-environment in case any type variables have been instantiated
    let freeInEnv = GeneralizationHelpers.ComputeUngeneralizableTypars envNonRec

    // Attempt to actually generalize some of the candidates eligible for generalization.
    // Compute which bindings are now eligible for early generalization.
    // Do this by computing a greatest fixed point by iteratively knocking out bindings that refer
    // to type variables free in later bindings. Look for ones whose type doesn't involve any of the other types
    let newGeneralizedRecBinds, preGeneralizationRecBinds, tpenv =

        //printfn "\n---------------------\nConsidering early generalization after type checking binding %s" vspec.DisplayName

        // Get the type variables free in bindings that have not yet been checked.
        //
        // The naive implementation of this is to iterate all the forward bindings, but this is quadratic.
        //
        // It turns out we can remove the quadratic behaviour as follows.
        // -    During type checking we already keep a table of recursive uses of values, indexed by target value.
        // -    This table is usually much smaller than the number of remaining forward declarations ? e.g. in the pathological case you mentioned below this table is size 1.
        // -    If a forward declaration does not have an entry in this table then its type can't involve any inference variables from the declarations we have already checked.
        // -    So by scanning the domain of this table we can reduce the complexity down to something like O(n * average-number-of-forward-calls).
        // -    For a fully connected programs or programs where every forward declaration is subject to a forward call, this would be quadratic. However we do not expect call graphs to be like this in practice
        //
        // Hence we use the recursive-uses table to guide the process of scraping forward references for frozen types
        // If the is no entry in the recursive use table then a forward binding has never been used and
        // the type of a binding will not contain any inference variables.
        //
        // We do this lazily in case it is "obvious" that a binding can be generalized (e.g. its type doesn't
        // involve any type inference variables)
        //
        // The forward uses table will always be smaller than the number of potential forward bindings except in extremely
        // pathological situations
        let freeInUncheckedRecBinds =
            lazy ((emptyFreeTyvars, cenv.recUses.Contents) ||> Map.fold (fun acc vStamp _ ->
                       match uncheckedRecBindsTable.TryGetValue vStamp with
                       | true, fwdBind ->
                           accFreeInType CollectAllNoCaching fwdBind.RecBindingInfo.Val.Type acc
                       | _ ->
                           acc))

        let rec loop (preGeneralizationRecBinds: PreGeneralizationRecursiveBinding list,
                      frozenBindings: PreGeneralizationRecursiveBinding list) =

            let frozenBindingTypes = frozenBindings |> List.map (fun pgrbind -> pgrbind.RecBindingInfo.Val.Type)

            let freeInFrozenAndLaterBindings =
                if frozenBindingTypes.IsEmpty then
                    freeInUncheckedRecBinds
                else
                    lazy (accFreeInTypes CollectAllNoCaching frozenBindingTypes (freeInUncheckedRecBinds.Force()))

            let preGeneralizationRecBinds, newFrozenBindings =

                preGeneralizationRecBinds |> List.partition (fun pgrbind ->

                    //printfn "(testing binding %s)" pgrbind.RecBindingInfo.Val.DisplayName

                    // Get the free type variables in the binding
                    //
                    // We use the TauType here because the binding may already have been pre-generalized because it has
                    // a fully type-annotated type signature. We effectively want to generalize the binding
                    // again here, properly - for example this means adjusting the expression for the binding to include
                    // a Expr_tlambda. If we use Val.Type then the type will appear closed.
                    let freeInBinding = (freeInType CollectAllNoCaching pgrbind.RecBindingInfo.Val.TauType).FreeTypars

                    // Is the binding free of type inference variables? If so, it can be generalized immediately
                    if freeInBinding.IsEmpty then true else

                    //printfn "(failed generalization test 1 for binding for %s)" pgrbind.RecBindingInfo.Val.DisplayName
                    // Any declared type parameters in an type are always generalizable
                    let freeInBinding = Zset.diff freeInBinding (Zset.ofList typarOrder (NormalizeDeclaredTyparsForEquiRecursiveInference cenv.g pgrbind.ExtraGeneralizableTypars))

                    if freeInBinding.IsEmpty then true else

                    //printfn "(failed generalization test 2 for binding for %s)" pgrbind.RecBindingInfo.Val.DisplayName

                    // Any declared method parameters can always be generalized
                    let freeInBinding = Zset.diff freeInBinding (Zset.ofList typarOrder (NormalizeDeclaredTyparsForEquiRecursiveInference cenv.g pgrbind.RecBindingInfo.DeclaredTypars))

                    if freeInBinding.IsEmpty then true else

                    //printfn "(failed generalization test 3 for binding for %s)" pgrbind.RecBindingInfo.Val.DisplayName

                    // Type variables free in the non-recursive environment do not stop us generalizing the binding,
                    // since they can't be generalized anyway
                    let freeInBinding = Zset.diff freeInBinding freeInEnv

                    if freeInBinding.IsEmpty then true else

                    //printfn "(failed generalization test 4 for binding for %s)" pgrbind.RecBindingInfo.Val.DisplayName

                    // Type variables free in unchecked bindings do stop us generalizing
                    let freeInBinding = Zset.inter (freeInFrozenAndLaterBindings.Force().FreeTypars) freeInBinding

                    if freeInBinding.IsEmpty then true else

                    //printfn "(failed generalization test 5 for binding for %s)" pgrbind.RecBindingInfo.Val.DisplayName

                    false)
                    //if canGeneralize then
                    //    printfn "YES: binding for %s can be generalized early" pgrbind.RecBindingInfo.Val.DisplayName
                    //else
                    //    printfn "NO: binding for %s can't be generalized early" pgrbind.RecBindingInfo.Val.DisplayName

            // Have we reached a fixed point?
            if newFrozenBindings.IsEmpty then
                preGeneralizationRecBinds, frozenBindings
            else
                // if not, then repeat
                loop(preGeneralizationRecBinds, newFrozenBindings@frozenBindings)

        // start with no frozen bindings
        let newGeneralizableBindings, preGeneralizationRecBinds = loop(preGeneralizationRecBinds, [])

        // Some of the bindings may now have been marked as 'generalizable' (which means they now transition
        // from PreGeneralization --> PostGeneralization, since we won't get any more information on
        // these bindings by processing later bindings). But this doesn't mean we
        // actually generalize all the individual type variables occuring in these bindings - for example, some
        // type variables may be free in the environment, and some definitions
        // may be value definitions which can't be generalized, e.g.
        //   let rec f x = g x
        //   and g = id f
        // Here the type variables in 'g' can't be generalized because it's a computation on the right.
        //
        // Note that in the normal case each binding passes IsGeneralizableValue. Properties and
        // constructors do not pass CanInferExtraGeneralizedTyparsForRecBinding.

        let freeInEnv =
            (freeInEnv, newGeneralizableBindings) ||> List.fold (fun freeInEnv pgrbind ->
                if GeneralizationHelpers.IsGeneralizableValue cenv.g pgrbind.CheckedBinding.Expr then
                    freeInEnv
                else
                    let freeInBinding = (freeInType CollectAllNoCaching pgrbind.RecBindingInfo.Val.TauType).FreeTypars
                    let freeInBinding = Zset.diff freeInBinding (Zset.ofList typarOrder (NormalizeDeclaredTyparsForEquiRecursiveInference cenv.g pgrbind.ExtraGeneralizableTypars))
                    let freeInBinding = Zset.diff freeInBinding (Zset.ofList typarOrder (NormalizeDeclaredTyparsForEquiRecursiveInference cenv.g pgrbind.RecBindingInfo.DeclaredTypars))
                    Zset.union freeInBinding freeInEnv)

        // Process the bindings marked for transition from PreGeneralization --> PostGeneralization
        let newGeneralizedRecBinds, tpenv =
            if newGeneralizableBindings.IsEmpty then
                [], tpenv
            else

                let supportForBindings = newGeneralizableBindings |> List.collect (TcLetrecComputeSupportForBinding cenv)
                ConstraintSolver.CanonicalizePartialInferenceProblem cenv.css denv scopem supportForBindings

                let generalizedTyparsL = newGeneralizableBindings |> List.map (TcLetrecComputeAndGeneralizeGenericTyparsForBinding cenv denv freeInEnv)

                // Generalize the bindings.
                let newGeneralizedRecBinds = (generalizedTyparsL, newGeneralizableBindings) ||> List.map2 (TcLetrecGeneralizeBinding cenv denv )
                let tpenv = HideUnscopedTypars (List.concat generalizedTyparsL) tpenv
                newGeneralizedRecBinds, tpenv


        newGeneralizedRecBinds, preGeneralizationRecBinds, tpenv

    let envNonRec = envNonRec |> AddLocalVals cenv.g cenv.tcSink scopem (newGeneralizedRecBinds |> List.map (fun b -> b.RecBindingInfo.Val))
    let generalizedRecBinds = newGeneralizedRecBinds @ generalizedRecBinds

    (envNonRec, generalizedRecBinds, preGeneralizationRecBinds, tpenv, uncheckedRecBindsTable)

//-------------------------------------------------------------------------
// TcLetrecComputeAndGeneralizeGenericTyparsForBinding
//-------------------------------------------------------------------------

/// Compute the type variables which may be generalized and perform the generalization
and TcLetrecComputeAndGeneralizeGenericTyparsForBinding cenv denv freeInEnv (pgrbind: PreGeneralizationRecursiveBinding) =

    let freeInEnv = Zset.diff freeInEnv (Zset.ofList typarOrder (NormalizeDeclaredTyparsForEquiRecursiveInference cenv.g pgrbind.ExtraGeneralizableTypars))

    let rbinfo = pgrbind.RecBindingInfo
    let vspec = rbinfo.Val
    let (CheckedBindingInfo(inlineFlag, _, _, _, _, _, expr, _, _, m, _, _, _, _)) = pgrbind.CheckedBinding
    let (ExplicitTyparInfo(rigidCopyOfDeclaredTypars, declaredTypars, _)) = rbinfo.ExplicitTyparInfo
    let allDeclaredTypars = rbinfo.EnclosingDeclaredTypars @ declaredTypars

    // The declared typars were not marked rigid to allow equi-recursive type inference to unify
    // two declared type variables. So we now check that, for each binding, the declared
    // type variables can be unified with a rigid version of the same and undo the results
    // of this unification.
    ConstraintSolver.CheckDeclaredTypars denv cenv.css m rigidCopyOfDeclaredTypars declaredTypars

    let memFlagsOpt = vspec.MemberInfo |> Option.map (fun memInfo -> memInfo.MemberFlags)
    let isCtor = (match memFlagsOpt with None -> false | Some memberFlags -> memberFlags.MemberKind = SynMemberKind.Constructor)

    GeneralizationHelpers.CheckDeclaredTyparsPermitted(memFlagsOpt, declaredTypars, m)
    let canInferTypars = CanInferExtraGeneralizedTyparsForRecBinding pgrbind

    let tau = vspec.TauType
    let maxInferredTypars = freeInTypeLeftToRight cenv.g false tau

    let canGeneralizeConstrained = GeneralizationHelpers.CanGeneralizeConstrainedTyparsForDecl rbinfo.DeclKind
    let generalizedTypars = GeneralizationHelpers.ComputeAndGeneralizeGenericTypars (cenv, denv, m, freeInEnv, canInferTypars, canGeneralizeConstrained, inlineFlag, Some expr, allDeclaredTypars, maxInferredTypars, tau, isCtor)
    generalizedTypars

/// Compute the type variables which may have member constraints that need to be canonicalized prior to generalization
and TcLetrecComputeSupportForBinding cenv (pgrbind: PreGeneralizationRecursiveBinding) =
    let rbinfo = pgrbind.RecBindingInfo
    let allDeclaredTypars = rbinfo.EnclosingDeclaredTypars @ rbinfo.DeclaredTypars
    let maxInferredTypars = freeInTypeLeftToRight cenv.g false rbinfo.Val.TauType
    allDeclaredTypars @ maxInferredTypars

//-------------------------------------------------------------------------
// TcLetrecGeneralizeBinding
//------------------------------------------------------------------------

// Generalise generalizedTypars from checkedBind.
and TcLetrecGeneralizeBinding cenv denv generalizedTypars (pgrbind: PreGeneralizationRecursiveBinding) : PostGeneralizationRecursiveBinding =

    let (RecursiveBindingInfo(_, _, enclosingDeclaredTypars, _, vspec, explicitTyparInfo, partialValReprInfo, memberInfoOpt, _, _, _, vis, _, declKind)) = pgrbind.RecBindingInfo
    let (CheckedBindingInfo(inlineFlag, _, _, _, _, _, expr, argAttribs, _, _, _, compgen, _, isFixed)) = pgrbind.CheckedBinding

    if isFixed then
        errorR(Error(FSComp.SR.tcFixedNotAllowed(), expr.Range))

    let _, tau = vspec.TypeScheme

    let pvalscheme1 = PrelimValScheme1(vspec.Id, explicitTyparInfo, tau, Some partialValReprInfo, memberInfoOpt, false, inlineFlag, NormalVal, argAttribs, vis, compgen)
    let pvalscheme2 = GeneralizeVal cenv denv enclosingDeclaredTypars generalizedTypars pvalscheme1

    let valscheme = UseCombinedArity cenv.g declKind expr pvalscheme2
    AdjustRecType vspec valscheme

    { ValScheme = valscheme
      CheckedBinding = pgrbind.CheckedBinding
      RecBindingInfo = pgrbind.RecBindingInfo }


and TcLetrecComputeCtorSafeThisValBind cenv safeThisValOpt =
    match safeThisValOpt with
    | None -> None
    | Some (v: Val) ->
        let m = v.Range
        let ty = destRefCellTy cenv.g v.Type
        Some (mkCompGenBind v (mkRefCell cenv.g m ty (mkNull m ty)))

and MakeCheckSafeInitField g tinst thisValOpt rfref reqExpr (expr: Expr) =
    let m = expr.Range
    let availExpr =
        match thisValOpt with
        | None -> mkStaticRecdFieldGet (rfref, tinst, m)
        | Some thisVar ->
            // This is an instance method, it must have a 'this' var
            mkRecdFieldGetViaExprAddr (exprForVal m thisVar, rfref, tinst, m)
    let failureExpr = match thisValOpt with None -> mkCallFailStaticInit g m | Some _ -> mkCallFailInit g m
    mkCompGenSequential m (mkIfThen g m (mkILAsmClt g m availExpr reqExpr) failureExpr) expr

and MakeCheckSafeInit g tinst safeInitInfo reqExpr expr =
    match safeInitInfo with
    | SafeInitField (rfref, _) -> MakeCheckSafeInitField g tinst None rfref reqExpr expr
    | NoSafeInitInfo -> expr

// Given a method binding (after generalization)
//
//    method M = (fun <tyargs> <args> -> <body>)
//
// wrap the following around it if needed
//
//    method M = (fun <tyargs> baseVal <args> ->
//                      check ctorSafeInitInfo
//                      let ctorSafeThisVal = ref null
//                      <body>)
//
// The "check ctorSafeInitInfo" is only added for non-constructor instance members in a class where at least one type in the
// hierarchy has HasSelfReferentialConstructor
//
// The "let ctorSafeThisVal = ref null" is only added for explicit constructors with a self-reference parameter (Note: check later code for exact conditions)
// For implicit constructors the binding is added to the bindings of the implicit constructor

and TcLetrecAdjustMemberForSpecialVals cenv (pgrbind: PostGeneralizationRecursiveBinding) : PostSpecialValsRecursiveBinding =

    let (RecursiveBindingInfo(_, _, _, _, vspec, _, _, _, baseValOpt, safeThisValOpt, safeInitInfo, _, _, _)) = pgrbind.RecBindingInfo
    let expr = pgrbind.CheckedBinding.Expr
    let spBind = pgrbind.CheckedBinding.SeqPoint

    let expr =
        match TcLetrecComputeCtorSafeThisValBind cenv safeThisValOpt with
        | None -> expr
        | Some bind ->
            let m = expr.Range
            let tps, vsl, body, returnTy = stripTopLambda (expr, vspec.Type)
            mkMultiLambdas m tps vsl (mkLetBind m bind body, returnTy)

    // Add a call to CheckInit if necessary for instance members
    let expr =
        if vspec.IsInstanceMember && not vspec.IsExtensionMember && not vspec.IsConstructor then
            match safeInitInfo with
            | SafeInitField (rfref, _) ->
                let m = expr.Range
                let tps, vsl, body, returnTy = stripTopLambda (expr, vspec.Type)
                // This is an instance member, it must have a 'this'
                let thisVar = vsl.Head.Head
                let thisTypeInst = argsOfAppTy cenv.g thisVar.Type
                let newBody = MakeCheckSafeInitField cenv.g thisTypeInst (Some thisVar) rfref (mkOne cenv.g m) body
                mkMultiLambdas m tps vsl (newBody, returnTy)
            | NoSafeInitInfo ->
                expr

        else
            expr

    let expr =
        match baseValOpt with
        | None -> expr
        | _ ->
            let m = expr.Range
            let tps, vsl, body, returnTy = stripTopLambda (expr, vspec.Type)
            mkMemberLambdas m tps None baseValOpt vsl (body, returnTy)

    { ValScheme = pgrbind.ValScheme
      Binding = TBind(vspec, expr, spBind) }

and FixupLetrecBind cenv denv generalizedTyparsForRecursiveBlock (bind: PostSpecialValsRecursiveBinding) =
    let (TBind(vspec, expr, spBind)) = bind.Binding

    // Check coherence of generalization of variables for memberInfo members in generic classes
    match vspec.MemberInfo with
#if EXTENDED_EXTENSION_MEMBERS // indicates if extension members can add additional constraints to type parameters
    | Some _ when not vspec.IsExtensionMember ->
#else
    | Some _ ->
#endif
       match PartitionValTyparsForApparentEnclosingType cenv.g vspec with
       | Some(parentTypars, memberParentTypars, _, _, _) ->
          ignore(SignatureConformance.Checker(cenv.g, cenv.amap, denv, SignatureRepackageInfo.Empty, false).CheckTypars vspec.Range TypeEquivEnv.Empty memberParentTypars parentTypars)
       | None ->
          errorR(Error(FSComp.SR.tcMemberIsNotSufficientlyGeneric(), vspec.Range))
    | _ -> ()

    // Fixup recursive references...
    let fixupPoints = GetAllUsesOfRecValue cenv vspec

    AdjustAndForgetUsesOfRecValue cenv (mkLocalValRef vspec) bind.ValScheme

    let expr = mkGenericBindRhs cenv.g vspec.Range generalizedTyparsForRecursiveBlock bind.ValScheme.TypeScheme expr

    { FixupPoints = fixupPoints
      Binding = TBind(vspec, expr, spBind) }

//-------------------------------------------------------------------------
// TcLetrec - for both expressions and class-let-rec-declarations
//------------------------------------------------------------------------

and unionGeneralizedTypars typarSets = List.foldBack (ListSet.unionFavourRight typarEq) typarSets []

and TcLetrec overridesOK cenv env tpenv (binds, bindsm, scopem) =

    // Create prelimRecValues for the recursive items (includes type info from LHS of bindings) *)
    let binds = binds |> List.map (fun (RecDefnBindingInfo(a, b, c, bind)) -> NormalizedRecBindingDefn(a, b, c, BindingNormalization.NormalizeBinding ValOrMemberBinding cenv env bind))
    let uncheckedRecBinds, prelimRecValues, (tpenv, _) = AnalyzeAndMakeAndPublishRecursiveValues overridesOK cenv env tpenv binds

    let envRec = AddLocalVals cenv.g cenv.tcSink scopem prelimRecValues env

    // Typecheck bindings
    let uncheckedRecBindsTable = uncheckedRecBinds |> List.map (fun rbind -> rbind.RecBindingInfo.Val.Stamp, rbind) |> Map.ofList

    let (_, generalizedRecBinds, preGeneralizationRecBinds, tpenv, _) =
        ((env, [], [], tpenv, uncheckedRecBindsTable), uncheckedRecBinds) ||> List.fold (TcLetrecBinding (cenv, envRec, scopem, [], None))

    // There should be no bindings that have not been generalized since checking the vary last binding always
    // results in the generalization of all remaining ungeneralized bindings, since there are no remaining unchecked bindings
    // to prevent the generalization
    assert preGeneralizationRecBinds.IsEmpty

    let generalizedRecBinds = generalizedRecBinds |> List.sortBy (fun pgrbind -> pgrbind.RecBindingInfo.Index)
    let generalizedTyparsForRecursiveBlock =
         generalizedRecBinds
            |> List.map (fun pgrbind -> pgrbind.GeneralizedTypars)
            |> unionGeneralizedTypars


    let vxbinds = generalizedRecBinds |> List.map (TcLetrecAdjustMemberForSpecialVals cenv)

    // Now that we know what we've generalized we can adjust the recursive references
    let vxbinds = vxbinds |> List.map (FixupLetrecBind cenv env.DisplayEnv generalizedTyparsForRecursiveBlock)

    // Now eliminate any initialization graphs
    let binds =
        let bindsWithoutLaziness = vxbinds
        let mustHaveArity =
            match uncheckedRecBinds with
            | [] -> false
            | (rbind :: _) -> DeclKind.MustHaveArity rbind.RecBindingInfo.DeclKind

        let results =
           EliminateInitializationGraphs
             cenv.g mustHaveArity env.DisplayEnv
             bindsWithoutLaziness
             //(fun
             (fun doBindings bindings -> doBindings bindings)
             (fun bindings -> bindings)
             (fun doBindings bindings -> [doBindings bindings])
             bindsm
        List.concat results

    // Post letrec env
    let envbody = AddLocalVals cenv.g cenv.tcSink scopem prelimRecValues env
    binds, envbody, tpenv

//-------------------------------------------------------------------------
// Bind specifications of values
//-------------------------------------------------------------------------

let TcAndPublishValSpec (cenv, env, containerInfo: ContainerInfo, declKind, memFlagsOpt, tpenv, valSpfn) =

  let (SynValSig (Attributes synAttrs, _, ValTyparDecls (synTypars, _, synCanInferTypars), _, _, isInline, mutableFlag, doc, vis, literalExprOpt, m)) = valSpfn

  GeneralizationHelpers.CheckDeclaredTyparsPermitted(memFlagsOpt, synTypars, m)
  let canInferTypars = GeneralizationHelpers.ComputeCanInferExtraGeneralizableTypars (containerInfo.ParentRef, synCanInferTypars, memFlagsOpt)

  let attrTgt = DeclKind.AllowedAttribTargets memFlagsOpt declKind

  let attrs = TcAttributes cenv env attrTgt synAttrs
  let newOk = if canInferTypars then NewTyparsOK else NoNewTypars

  let valinfos, tpenv = TcValSpec cenv env declKind newOk containerInfo memFlagsOpt None tpenv valSpfn attrs
  let denv = env.DisplayEnv

  (tpenv, valinfos) ||> List.mapFold (fun tpenv valSpecResult ->

            let (ValSpecResult (altActualParent, memberInfoOpt, id, enclosingDeclaredTypars, declaredTypars, ty, partialValReprInfo, declKind)) = valSpecResult

            let inlineFlag = ComputeInlineFlag (memberInfoOpt |> Option.map (fun (PreValMemberInfo(memberInfo, _, _)) -> memberInfo.MemberFlags)) isInline mutableFlag m

            let freeInType = freeInTypeLeftToRight cenv.g false ty

            let allDeclaredTypars = enclosingDeclaredTypars @ declaredTypars

            let explicitTyparInfo = ExplicitTyparInfo(declaredTypars, declaredTypars, synCanInferTypars)

            let generalizedTypars =
                GeneralizationHelpers.ComputeAndGeneralizeGenericTypars(cenv, denv, id.idRange,
                    emptyFreeTypars, canInferTypars, CanGeneralizeConstrainedTypars, inlineFlag,
                    None, allDeclaredTypars, freeInType, ty, false)

            let valscheme1 = PrelimValScheme1(id, explicitTyparInfo, ty, Some partialValReprInfo, memberInfoOpt, mutableFlag, inlineFlag, NormalVal, noArgOrRetAttribs, vis, false)

            let valscheme2 = GeneralizeVal cenv denv enclosingDeclaredTypars generalizedTypars valscheme1

            let tpenv = HideUnscopedTypars generalizedTypars tpenv

            let valscheme = BuildValScheme declKind (Some partialValReprInfo) valscheme2

            let literalValue =
                match literalExprOpt with
                | None ->
                    let hasLiteralAttr = HasFSharpAttribute cenv.g cenv.g.attrib_LiteralAttribute attrs
                    if hasLiteralAttr then
                        errorR(Error(FSComp.SR.tcLiteralAttributeRequiresConstantValue(), m))
                    None

                | Some e ->
                    let hasLiteralAttr, literalValue = TcLiteral cenv ty env tpenv (attrs, e)
                    if not hasLiteralAttr then
                        errorR(Error(FSComp.SR.tcValueInSignatureRequiresLiteralAttribute(), e.Range))
                    literalValue

            let paramNames =
                match valscheme.ValReprInfo with
                | None -> None
                | Some topValInfo -> topValInfo.ArgNames

            let doc = doc.ToXmlDoc(true, paramNames)
            let vspec = MakeAndPublishVal cenv env (altActualParent, true, declKind, ValNotInRecScope, valscheme, attrs, doc, literalValue, false)
            assert(vspec.InlineInfo = inlineFlag)

            vspec, tpenv)
