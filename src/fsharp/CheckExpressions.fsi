// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.CheckExpressions

open System
open Internal.Utilities.Collections
open Internal.Utilities.Library
open FSharp.Compiler 
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AccessibilityLogic
open FSharp.Compiler.CompilerGlobalState
open FSharp.Compiler.ConstraintSolver
open FSharp.Compiler.Import
open FSharp.Compiler.InfoReader
open FSharp.Compiler.Infos
open FSharp.Compiler.MethodOverrides
open FSharp.Compiler.NameResolution
open FSharp.Compiler.PatternMatchCompilation
open FSharp.Compiler.Syntax
open FSharp.Compiler.SyntaxTreeOps
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.Xml
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeOps

#if !NO_EXTENSIONTYPING
open FSharp.Compiler.ExtensionTyping
#endif

/// Represents information about the initialization field used to check that object constructors
/// have completed before fields are accessed.
type SafeInitData = 
    | SafeInitField of RecdFieldRef * RecdField
    | NoSafeInitInfo 

/// Represents information about object constructors
[<Sealed>]
type CtorInfo

val InitialImplicitCtorInfo: unit -> CtorInfo

/// Represents an item in the environment that may restrict the automatic generalization of later
/// declarations because it refers to type inference variables. As type inference progresses
/// these type inference variables may get solved.
[<NoEquality; NoComparison>]
type UngeneralizableItem

[<NoEquality; NoComparison>]
/// Represents the type environment at a particular scope. Includes the name
/// resolution environment, the ungeneralizable items from earlier in the scope
/// and other information about the scope.
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
    } 

    member DisplayEnv : DisplayEnv
    member NameEnv : NameResolution.NameResolutionEnv
    member AccessRights : AccessorDomain

//-------------------------------------------------------------------------
// Some of the exceptions arising from type checking. These should be moved to 
// use ErrorLogger.
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
exception UnitTypeExpected of DisplayEnv * TType * range
exception UnitTypeExpectedWithEquality of DisplayEnv * TType * range
exception UnitTypeExpectedWithPossiblePropertySetter of DisplayEnv * TType * string * string * range
exception UnitTypeExpectedWithPossibleAssignment of DisplayEnv * TType * bool * string * range
exception FunctionValueUnexpected of DisplayEnv * TType * range
exception UnionPatternsBindDifferentNames of range
exception VarBoundTwice of Ident
exception ValueRestriction of DisplayEnv * InfoReader * bool * Val * Typar * range
exception ValNotMutable of DisplayEnv * ValRef * range
exception ValNotLocal of DisplayEnv * ValRef * range
exception InvalidRuntimeCoercion of DisplayEnv * TType * TType * range
exception IndeterminateRuntimeCoercion of DisplayEnv * TType * TType * range
exception IndeterminateStaticCoercion of DisplayEnv * TType * TType * range
exception StaticCoercionShouldUseBox of DisplayEnv * TType * TType * range
exception RuntimeCoercionSourceSealed of DisplayEnv * TType * range
exception CoercionTargetSealed of DisplayEnv * TType * range
exception UpcastUnnecessary of range
exception TypeTestUnnecessary of range
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

val TcFieldInit : range -> ILFieldInit -> Const

val LightweightTcValForUsingInBuildMethodCall : g : TcGlobals -> vref:ValRef -> vrefFlags : ValUseFlag -> vrefTypeInst : TTypes -> m : range -> Expr * TType

//-------------------------------------------------------------------------
// The rest are all helpers needed for declaration checking (CheckDeclarations.fs)
//------------------------------------------------------------------------- 

/// Represents the current environment of type variables that have implicit scope
/// (i.e. are without explicit declaration).
type UnscopedTyparEnv

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
      TcSequenceExpressionEntry: TcFileState -> TcEnv -> TType -> UnscopedTyparEnv -> bool * bool ref *  SynExpr -> range -> Expr * UnscopedTyparEnv
      // forward call 
      TcArrayOrListSequenceExpression: TcFileState -> TcEnv -> TType -> UnscopedTyparEnv -> bool * SynExpr -> range -> Expr * UnscopedTyparEnv
      // forward call 
      TcComputationExpression: TcFileState -> TcEnv -> TType -> UnscopedTyparEnv -> range * Expr * TType * SynExpr -> Expr * UnscopedTyparEnv
    } 
    static member Create: 
        g: TcGlobals *
        isScript: bool *
        niceNameGen: NiceNameGenerator *
        amap: ImportMap *
        topCcu: CcuThunk *
        isSig: bool *
        haveSig: bool *
        conditionalDefines: string list option * 
        tcSink: TcResultsSink *
        tcVal: TcValF *
        isInternalTestSpanStackReferring: bool *
        // forward call to CheckComputationExpressions.fs
        tcSequenceExpressionEntry: (TcFileState -> TcEnv -> TType -> UnscopedTyparEnv -> bool * bool ref * SynExpr -> range -> Expr * UnscopedTyparEnv) *
        // forward call to CheckComputationExpressions.fs 
        tcArrayOrListSequenceExpression: (TcFileState -> TcEnv -> TType -> UnscopedTyparEnv -> bool * SynExpr -> range -> Expr * UnscopedTyparEnv) *
        // forward call to CheckComputationExpressions.fs
        tcComputationExpression: (TcFileState -> TcEnv -> TType -> UnscopedTyparEnv -> range * Expr * TType * SynExpr -> Expr * UnscopedTyparEnv) 
         -> TcFileState

/// Represents information about the module or type in which a member or value is declared.
type MemberOrValContainerInfo = 
    | MemberOrValContainerInfo of
        tcref: TyconRef *
        optIntfSlotTy: (TType * SlotImplSet) option *
        baseValOpt: Val option *
        safeInitInfo: SafeInitData *
        declaredTyconTypars: Typars

/// Provides information about the context for a value or member definition.
type ContainerInfo = 
    | ContainerInfo of 
        ParentRef *  
        MemberOrValContainerInfo option
    member ParentRef : ParentRef

val ExprContainerInfo: ContainerInfo

/// Indicates if member declarations are allowed to be abstract members.
type NewSlotsOK = 
    | NewSlotsOK 
    | NoNewSlots

/// Indicates if member declarations are allowed to be override members.
type OverridesOK = 
    | OverridesOK 
    | WarnOnOverrides
    | ErrorOnOverrides

/// A flag to represent the sort of bindings are we processing.
type DeclKind = 
    /// A binding in a module, or a member
    | ModuleOrMemberBinding 

    /// Extensions to a type within the same assembly
    | IntrinsicExtensionBinding 

    /// Extensions to a type in a different assembly
    | ExtrinsicExtensionBinding 

    /// A binding in a class
    | ClassLetBinding of isStatic: bool

    /// A binding in an object expression
    | ObjectExpressionOverrideBinding

    /// A binding in an expression
    | ExpressionBinding 

    static member IsModuleOrMemberOrExtensionBinding: DeclKind -> bool

    static member MustHaveArity: DeclKind -> bool

    member CanBeDllImport: bool

    static member IsAccessModifierPermitted: DeclKind -> bool

    static member ImplicitlyStatic: DeclKind -> bool

    static member AllowedAttribTargets: SynMemberFlags option -> DeclKind -> AttributeTargets

    // Note: now always true
    static member CanGeneralizeConstrainedTypars: DeclKind -> bool
        
    static member ConvertToLinearBindings: DeclKind -> bool

    static member CanOverrideOrImplement: DeclKind -> OverridesOK

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

/// Indicates if a member binding is an object expression binding
type IsObjExprBinding = 
    | ObjExprBinding 
    | ValOrMemberBinding

/// Represents the initial information about a recursive binding
type RecDefnBindingInfo =
    | RecDefnBindingInfo of 
        containerInfo: ContainerInfo *
        newslotsOk: NewSlotsOK *
        declKind: DeclKind *
        synBinding: SynBinding

/// Represents the ValReprInfo for a value, before the typars are fully inferred 
type PartialValReprInfo =
    | PartialValReprInfo of
        curriedArgInfos: ArgReprInfo list list *
        returnInfo: ArgReprInfo 

/// Holds the initial ValMemberInfo and other information before it is fully completed
type PreValMemberInfo =
    | PreValMemberInfo of
        memberInfo: ValMemberInfo *
        logicalName: string *
        compiledName: string 

/// The result of checking a value or member signature
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

/// An empty environment of type variables with implicit scope
val emptyUnscopedTyparEnv: UnscopedTyparEnv

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

/// NormalizedBindingRhs records the r.h.s. of a binding after some munging just before type checking.
type NormalizedBindingRhs = 
    | NormalizedBindingRhs of
        simplePats: SynSimplePats list *
        returnTyOpt: SynBindingReturnInfo option *
        rhsExpr: SynExpr 

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
    member Val: Val
    member EnclosingDeclaredTypars: Typar list
    member Index: int

/// Represents the results of the first phase of preparing simple values from a pattern
[<Sealed>]
type PrelimValScheme1 =
    member Ident: Ident
    member Type: TType

/// Represents the results of the first phase of preparing bindings
[<Sealed>]
type CheckedBindingInfo

/// Represnts the results of the second phase of checking simple values
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

/// Represents a recursive binding after it has been normalized but before it's info has been put together
type NormalizedRecBindingDefn =
    | NormalizedRecBindingDefn of
        containerInfo: ContainerInfo *
        newslotsOk: NewSlotsOK *
        declKind: DeclKind *
        binding: NormalizedBinding

/// Represents a recursive binding after it has been normalized but before it has been checked
type PreCheckingRecursiveBinding = 
    { SyntacticBinding: NormalizedBinding 
      RecBindingInfo: RecursiveBindingInfo }

/// Represents a recursive binding after it has been checked but prior to generalization
type PreGeneralizationRecursiveBinding = 
    { ExtraGeneralizableTypars: Typars
      CheckedBinding: CheckedBindingInfo
      RecBindingInfo: RecursiveBindingInfo }

/// Represents the usage points of a recursive binding that need later adjustment once the
/// type of the member of value is fully inferred.
[<Sealed>]
type RecursiveUseFixupPoints

/// Represents a recursive binding after it has been both checked and generalized
type PostGeneralizationRecursiveBinding = 
    { ValScheme: ValScheme
      CheckedBinding: CheckedBindingInfo
      RecBindingInfo: RecursiveBindingInfo }
    member GeneralizedTypars: Typar list

/// Represents a recursive binding after it has been both checked and generalized and after
/// the special adjustments for 'as this' class initialization checks have been inserted into members.
type PostSpecialValsRecursiveBinding = 
    { ValScheme: ValScheme
      Binding: Binding }

/// Represents a recursive binding after it has been both checked and generalized, but
/// before initialization recursion has been rewritten
type PreInitializationGraphEliminationBinding = 
    { FixupPoints: RecursiveUseFixupPoints
      Binding: Binding }

/// Record the entire contents of a module or namespace type as not-generalizable, that is
/// if any type variables occur free in the module or namespace type (because type inference
/// is not yet complete), then they can't be generalized.
val addFreeItemOfModuleTy: ModuleOrNamespaceType -> UngeneralizableItem list -> UngeneralizableItem list

/// Merge together lists of type variables to generalize, keeping canonical order
val unionGeneralizedTypars: typarSets:Typar list list -> Typar list    

/// Add a list of explicitly declared type variables to the environment, producing a new environment
val AddDeclaredTypars: check: CheckForDuplicateTyparFlag -> typars: Typar list -> env: TcEnv -> TcEnv

/// Add a value to the environment, producing a new environment. Report to the sink.
val AddLocalVal: g: TcGlobals -> NameResolution.TcResultsSink -> scopem: range -> v: Val -> TcEnv -> TcEnv

/// Add a value to the environment, producing a new environment
val AddLocalValPrimitive: g: TcGlobals -> v: Val -> TcEnv -> TcEnv

/// Add a list of values to the environment, producing a new environment. Report to the sink.
val AddLocalVals: g: TcGlobals -> tcSink: TcResultsSink -> scopem: range -> vals: Val list -> env: TcEnv -> TcEnv

/// Set the type of a 'Val' after it has been fully inferred.
val AdjustRecType: vspec: Val -> vscheme: ValScheme -> unit

/// Process a normalized recursive binding and prepare for progressive generalization
val AnalyzeAndMakeAndPublishRecursiveValue: overridesOK:OverridesOK -> isGeneratedEventVal:bool -> cenv:TcFileState -> env:TcEnv -> tpenv:UnscopedTyparEnv * recBindIdx:int -> NormalizedRecBindingDefn -> (PreCheckingRecursiveBinding list * Val list) * (UnscopedTyparEnv * int)

/// Check that a member can be included in an interface
val CheckForNonAbstractInterface: declKind:DeclKind -> tcref:TyconRef -> memberFlags:SynMemberFlags -> m:range -> unit    

/// Check the flags on a member definition for consistency
val CheckMemberFlags: optIntfSlotTy:'a option -> newslotsOK:NewSlotsOK -> overridesOK:OverridesOK -> memberFlags:SynMemberFlags -> m:range -> unit

/// Check a super type is valid
val CheckSuperType: cenv:TcFileState -> ty:TType -> m:range -> unit    

/// After inference, view a set of declared type parameters in a canonical way.
val ChooseCanonicalDeclaredTyparsAfterInference: g: TcGlobals -> denv: DisplayEnv -> declaredTypars: Typar list -> m: range -> Typar list

/// After inference, view a ValSchem in a canonical way.
val ChooseCanonicalValSchemeAfterInference: g: TcGlobals -> denv: DisplayEnv -> vscheme: ValScheme -> m: range -> ValScheme

/// Check if the type annotations and inferred type information in a value give a
/// full and complete generic type for a value. If so, enable generic recursion.
val ComputeIsComplete: enclosingDeclaredTypars:Typar list -> declaredTypars:Typar list -> ty:TType -> bool    

/// Compute the available access rights from a particular location in code
val ComputeAccessRights: eAccessPath: CompilationPath -> eInternalsVisibleCompPaths: CompilationPath list -> eFamilyType: TyconRef option -> AccessorDomain

/// Compute the available access rights and module/entity compilation path for a paricular location in code
val ComputeAccessAndCompPath: env: TcEnv -> declKindOpt:DeclKind option -> m: range -> vis: SynAccess option -> overrideVis: Accessibility option -> actualParent: ParentRef -> Accessibility * CompilationPath option

/// Get the expression resulting from turning an expression into an enumerable value, e.g. at 'for' loops 
val ConvertArbitraryExprToEnumerable: cenv:TcFileState -> ty:TType -> env:TcEnv -> expr:Expr -> Expr * TType    

/// Invoke pattern match compilation
val CompilePatternForMatchClauses: cenv:TcFileState -> env:TcEnv -> mExpr:range -> matchm:range -> warnOnUnused:bool -> actionOnFailure:ActionOnFailure -> inputExprOpt:Expr option -> inputTy:TType -> resultTy:TType -> tclauses:TypedMatchClause list -> Val * Expr

/// Process recursive bindings so that initialization is through laziness and is checked.
/// The bindings may be either plain 'let rec' bindings or mutually recursive nestings of modules and types.
/// The functions must iterate the actual bindings and process them to the overall result.
val EliminateInitializationGraphs:
    g: TcGlobals 
    -> mustHaveArity: bool 
    -> denv: DisplayEnv 
    -> bindings: 'Binding list 
    -> iterBindings:((PreInitializationGraphEliminationBinding list -> unit) -> 'Binding list -> unit)
    -> buildLets: (Binding list -> 'Result)
    -> mapBindings: ((PreInitializationGraphEliminationBinding list -> Binding list) -> 'Binding list -> 'Result list)
    -> bindsm: range
    -> 'Result list

/// Adjust a recursive binding after generalization
val FixupLetrecBind: cenv:TcFileState -> denv:DisplayEnv -> generalizedTyparsForRecursiveBlock:Typars -> bind:PostSpecialValsRecursiveBinding -> PreInitializationGraphEliminationBinding    

/// Produce a fresh view of an object type, e.g. 'List<T>' becomes 'List<?>' for new
/// inference variables with the given rigidity.
val FreshenObjectArgType: cenv: TcFileState -> m: range -> rigid: TyparRigidity -> tcref: TyconRef -> isExtrinsic: bool -> declaredTyconTypars: Typar list -> TType * Typar list * TyparInst * TType * TType

/// Get the accumulated module/namespace type for the current module/namespace being processed.
val GetCurrAccumulatedModuleOrNamespaceType: env: TcEnv -> ModuleOrNamespaceType

/// Get the "this" variable from the lambda for an instance member binding
val GetInstanceMemberThisVariable: vspec: Val * expr: Expr -> Val option

/// Build the full ValReprInfo one type inference is complete.
val InferGenericArityFromTyScheme: TypeScheme -> partialValReprInfo: PartialValReprInfo -> ValReprInfo

/// Locate the environment within a particular namespace path, used to process a
/// 'namespace' declaration.
val LocateEnv: ccu: CcuThunk -> env: TcEnv -> enclosingNamespacePath: Ident list -> TcEnv

/// Make the check for safe initialization of a member
val MakeCheckSafeInit: g: TcGlobals -> tinst: TypeInst -> safeInitInfo: SafeInitData -> reqExpr: Expr -> expr: Expr -> Expr

/// Make an initial 'Val' and publish it to the environment and mutable module type accumulator.
val MakeAndPublishVal: cenv: TcFileState -> env: TcEnv -> altActualParent: ParentRef * inSig: bool * declKind: DeclKind * vrec: ValRecursiveScopeInfo * vscheme: ValScheme * attrs: Attribs * doc: XmlDoc * konst: Const option * isGeneratedEventVal: bool -> Val

/// Make an initial 'base' value
val MakeAndPublishBaseVal: cenv: TcFileState -> env: TcEnv -> Ident option -> TType -> Val option

/// Make simple values (which are not recursive nor members)
val MakeAndPublishSimpleVals: cenv: TcFileState -> env: TcEnv -> names: NameMap<PrelimValScheme1> -> NameMap<(Val * TypeScheme)> * NameMap<Val>

/// Make an initial implicit safe initialization value
val MakeAndPublishSafeThisVal: cenv: TcFileState -> env: TcEnv -> thisIdOpt: Ident option -> thisTy: TType -> Val option

/// Make initial information for a member value
val MakeMemberDataAndMangledNameForMemberVal: g: TcGlobals * tcref: TyconRef * isExtrinsic: bool * attrs: Attribs * optImplSlotTys: TType list * memberFlags: SynMemberFlags * valSynData: SynValInfo * id: Ident * isCompGen: bool -> PreValMemberInfo

/// Return a new environment suitable for processing declarations in the interior of a type definition
val MakeInnerEnvForTyconRef: env: TcEnv -> tcref: TyconRef -> isExtrinsicExtension: bool -> TcEnv

/// Return a new environment suitable for processing declarations in the interior of a module definition
/// including creating an accumulator for the module type.
val MakeInnerEnv: addOpenToNameEnv: bool -> env: TcEnv -> nm: Ident -> modKind: ModuleOrNamespaceKind -> TcEnv * ModuleOrNamespaceType ref

/// Return a new environment suitable for processing declarations in the interior of a module definition
/// given that the accumulator for the module type already exisits.
val MakeInnerEnvWithAcc: addOpenToNameEnv: bool -> env: TcEnv -> nm: Ident -> mtypeAcc: ModuleOrNamespaceType ref -> modKind: ModuleOrNamespaceKind -> TcEnv

/// Produce a post-generalization type scheme for a simple type where no type inference generalization
/// is appplied.
val NonGenericTypeScheme: ty: TType -> TypeScheme

/// Publish a module definition to the module/namespace type accumulator.
val PublishModuleDefn: cenv: TcFileState -> env: TcEnv -> mspec: ModuleOrNamespace -> unit

/// Publish a type definition to the module/namespace type accumulator.
val PublishTypeDefn: cenv: TcFileState -> env: TcEnv -> mspec: Tycon -> unit

/// Publish a value definition to the module/namespace type accumulator.
val PublishValueDefn: cenv: TcFileState -> env: TcEnv -> declKind: DeclKind -> vspec: Val -> unit

/// Publish a value definition to the module/namespace type accumulator.
val PublishValueDefnMaybeInclCompilerGenerated: cenv: TcFileState -> env: TcEnv -> inclCompilerGenerated: bool -> declKind: DeclKind -> vspec: Val -> unit

/// Mark a typar as no longer being an inference type variable
val SetTyparRigid: DisplayEnv -> range -> Typar -> unit

/// Check and publish a value specification (in a signature or 'abstract' member) to the
/// module/namespace type accumulator and return the resulting Val(s).  Normally only one
/// 'Val' results but CLI events may produce both and add_Event and _remove_Event Val.
val TcAndPublishValSpec: cenv: TcFileState * env: TcEnv * containerInfo: ContainerInfo * declKind: DeclKind * memFlagsOpt: SynMemberFlags option * tpenv: UnscopedTyparEnv * valSpfn: SynValSig -> Val list * UnscopedTyparEnv

/// Check a set of attributes
val TcAttributes: cenv: TcFileState -> env: TcEnv -> attrTgt: AttributeTargets -> synAttribs: SynAttribute list -> Attrib list

/// Check a set of attributes and allow failure because a later phase of type realization
/// may successfully check the attributes (if the attribute type or its arguments is in the
/// same recursive group)
val TcAttributesCanFail: cenv:TcFileState -> env:TcEnv -> attrTgt:AttributeTargets -> synAttribs:SynAttribute list -> Attrib list * (unit -> Attribs)    

/// Check a set of attributes which can only target specific elements
val TcAttributesWithPossibleTargets: canFail: bool -> cenv: TcFileState -> env: TcEnv -> attrTgt: AttributeTargets -> synAttribs: SynAttribute list -> (AttributeTargets * Attrib) list * bool

/// Check a constant value, e.g. a literal
val TcConst: cenv: TcFileState -> ty: TType -> m: range -> env: TcEnv -> c: SynConst -> Const

/// Check a syntactic expression and convert it to a typed tree expression
val TcExpr: cenv:TcFileState -> ty:TType -> env:TcEnv -> tpenv:UnscopedTyparEnv -> expr:SynExpr -> Expr * UnscopedTyparEnv    

/// Check a syntactic expression and convert it to a typed tree expression
val TcExprOfUnknownType: cenv:TcFileState -> env:TcEnv -> tpenv:UnscopedTyparEnv -> expr:SynExpr -> Expr * TType * UnscopedTyparEnv    

/// Check a syntactic expression and convert it to a typed tree expression. Possibly allow for subsumption flexibility
/// and insert a coercion if necessary.
val TcExprFlex: cenv:TcFileState -> flex:bool -> compat:bool -> ty:TType -> env:TcEnv -> tpenv:UnscopedTyparEnv -> e:SynExpr -> Expr * UnscopedTyparEnv    

/// Check a syntactic statement and convert it to a typed tree expression.
val TcStmtThatCantBeCtorBody: cenv:TcFileState -> env:TcEnv -> tpenv:UnscopedTyparEnv -> expr:SynExpr -> Expr * UnscopedTyparEnv    

/// Check a syntactic expression and convert it to a typed tree expression
val TcExprUndelayed: cenv:TcFileState -> overallTy:TType -> env:TcEnv -> tpenv:UnscopedTyparEnv -> synExpr:SynExpr -> Expr * UnscopedTyparEnv    

/// Check a linear expression (e.g. a sequence of 'let') in a tail-recursive way
/// and convert it to a typed tree expression, using the bodyChecker to check the parts
/// that are not linear.
val TcLinearExprs: bodyChecker:(TType -> TcEnv -> UnscopedTyparEnv -> SynExpr -> Expr * UnscopedTyparEnv) -> cenv:TcFileState -> env:TcEnv -> overallTy:TType -> tpenv:UnscopedTyparEnv -> isCompExpr:bool -> expr:SynExpr -> cont:(Expr * UnscopedTyparEnv -> Expr * UnscopedTyparEnv) -> Expr * UnscopedTyparEnv

/// Try to check a syntactic statement and indicate if it's type is not unit without emitting a warning
val TryTcStmt: cenv:TcFileState -> env:TcEnv -> tpenv:UnscopedTyparEnv -> synExpr:SynExpr -> bool * Expr * UnscopedTyparEnv    

/// Check a pattern being used as a pattern match
val TcMatchPattern: cenv:TcFileState -> inputTy:TType -> env:TcEnv -> tpenv:UnscopedTyparEnv -> pat:SynPat * optWhenExpr:SynExpr option -> Pattern * Expr option * Val list * TcEnv * UnscopedTyparEnv    

val (|BinOpExpr|_|): SynExpr -> (Ident * SynExpr * SynExpr) option

/// Check a set of let bindings
val TcLetBindings: cenv:TcFileState -> env:TcEnv -> containerInfo:ContainerInfo -> declKind:DeclKind -> tpenv:UnscopedTyparEnv -> binds:SynBinding list * bindsm:range * scopem:range -> ModuleOrNamespaceExpr list * TcEnv * UnscopedTyparEnv

/// Check an individual `let rec` binding
val TcLetrecBinding: cenv:TcFileState * envRec:TcEnv * scopem:range * extraGeneralizableTypars:Typars * reqdThisValTyOpt:TType option -> envNonRec:TcEnv * generalizedRecBinds:PostGeneralizationRecursiveBinding list * preGeneralizationRecBinds:PreGeneralizationRecursiveBinding list * tpenv:UnscopedTyparEnv * uncheckedRecBindsTable:Map<Stamp,PreCheckingRecursiveBinding> -> rbind:PreCheckingRecursiveBinding -> TcEnv * PostGeneralizationRecursiveBinding list * PreGeneralizationRecursiveBinding list * UnscopedTyparEnv * Map<Stamp,PreCheckingRecursiveBinding>

/// Get the binding for the implicit safe initialziation check value if it is being used
val TcLetrecComputeCtorSafeThisValBind: cenv:TcFileState -> safeThisValOpt:Val option -> Binding option    

/// Check a collection of `let rec` bindings
val TcLetrec: overridesOK:OverridesOK -> cenv:TcFileState -> env:TcEnv -> tpenv:UnscopedTyparEnv -> binds:RecDefnBindingInfo list * bindsm:range * scopem:range -> Bindings * TcEnv * UnscopedTyparEnv

/// Part of check a collection of recursive bindings that might include members
val TcLetrecAdjustMemberForSpecialVals: cenv: TcFileState -> pgrbind: PostGeneralizationRecursiveBinding -> PostSpecialValsRecursiveBinding

/// Check an inheritance expression or other 'new XYZ()' expression
val TcNewExpr: cenv:TcFileState -> env:TcEnv -> tpenv:UnscopedTyparEnv -> objTy:TType -> mObjTyOpt:range option -> superInit:bool -> arg:SynExpr -> mWholeExprOrObjTy:range -> Expr * UnscopedTyparEnv    

#if !NO_EXTENSIONTYPING
/// Check the application of a provided type to static args
val TcProvidedTypeAppToStaticConstantArgs: cenv:TcFileState -> env:TcEnv -> optGeneratedTypePath:string list option -> tpenv:UnscopedTyparEnv -> tcref:TyconRef -> args:SynType list -> m:range -> bool * Tainted<ProvidedType> * (unit -> unit)
#endif

/// Check a set of simple patterns, e.g. the declarations of parameters for an implicit constructor.
val TcSimplePatsOfUnknownType: cenv: TcFileState -> optArgsOK: bool -> checkCxs: CheckConstraints -> env: TcEnv -> tpenv: UnscopedTyparEnv -> spats: SynSimplePats -> string list * (UnscopedTyparEnv * NameMap<PrelimValScheme1> * Set<string>)

/// Check a set of explicitly declared constraints on type parameters
val TcTyparConstraints: cenv: TcFileState -> newOk: ImplicitlyBoundTyparsAllowed -> checkCxs: CheckConstraints -> occ: ItemOccurence -> env: TcEnv -> tpenv: UnscopedTyparEnv -> synConstraints: SynTypeConstraint list -> UnscopedTyparEnv

/// Check a collection of type parameters declarations
val TcTyparDecls: cenv: TcFileState -> env: TcEnv -> synTypars: SynTyparDecl list -> Typar list

/// Check a syntactic type
val TcType: cenv: TcFileState -> newOk: ImplicitlyBoundTyparsAllowed -> checkCxs: CheckConstraints -> occ: ItemOccurence -> env: TcEnv -> tpenv: UnscopedTyparEnv -> ty: SynType -> TType * UnscopedTyparEnv

/// Check a syntactic type or unit of measure
val TcTypeOrMeasureAndRecover: optKind: TyparKind option -> cenv: TcFileState -> newOk: ImplicitlyBoundTyparsAllowed -> checkCxs: CheckConstraints -> occ: ItemOccurence -> env: TcEnv -> tpenv: UnscopedTyparEnv -> ty: SynType -> TType * UnscopedTyparEnv

/// Check a syntactic type (with error recovery)
val TcTypeAndRecover: cenv: TcFileState -> newOk: ImplicitlyBoundTyparsAllowed -> checkCxs: CheckConstraints -> occ: ItemOccurence -> env: TcEnv -> tpenv: UnscopedTyparEnv -> ty: SynType -> TType * UnscopedTyparEnv

/// Check a specification of a value or member in a signature or an abstract member
val TcValSpec: cenv: TcFileState -> TcEnv -> DeclKind -> ImplicitlyBoundTyparsAllowed -> ContainerInfo -> SynMemberFlags option -> thisTyOpt: TType option -> UnscopedTyparEnv -> SynValSig -> Attrib list -> ValSpecResult list * UnscopedTyparEnv

/// Given the declaration of a function or member, process it to produce the ValReprInfo
/// giving the names and attributes relevant to arguments and return, but before type
/// parameters have been fully inferred via generalization.
val TranslateTopValSynInfo: range -> tcAttributes: (AttributeTargets -> SynAttribute list -> Attrib list) -> synValInfo: SynValInfo -> PartialValReprInfo

/// Given the declaration of a function or member, complete the processing of its ValReprInfo
/// once type parameters have been fully inferred via generalization.
val TranslatePartialArity: tps: Typar list -> PartialValReprInfo -> ValReprInfo

/// Constrain two types to be equal within this type checking context
val UnifyTypes : cenv:TcFileState -> env:TcEnv -> m:range -> actualTy:TType -> expectedTy:TType -> unit    

module GeneralizationHelpers =
    
    /// Given an environment, compute the set of inference type variables which may not be
    /// generalised, because they appear somewhere in the types of the constructs availabe
    /// in the environment.
    val ComputeUngeneralizableTypars: env: TcEnv -> Zset<Typar>

    /// Given an environment, compute the set of trait solutions which must appear before
    /// the current location, not after (to prevent use-before definitiosn and
    /// forward calls via type inference filling in trait solutions).
    val ComputeUnabstractableTraitSolutions: env: TcEnv -> FreeLocals

    /// Given an environment, compute the set of type definitions which must appear before
    /// the current location, not after (to prevent use-before-definition of type definitions
    /// via type inference).
    val ComputeUnabstractableTycons: env: TcEnv -> FreeTycons

// Logically extends System.AttributeTargets for F# constructs
module AttributeTargets =
    /// The allowed attribute targets for an F# field declaration
    val FieldDecl: AttributeTargets

    /// The allowed attribute targets for an F# field declaration once it's known to be targeting
    /// a field not a property (see useGenuineField)
    val FieldDeclRestricted: AttributeTargets

    /// The allowed attribute targets for an F# union case declaration
    val UnionCaseDecl: AttributeTargets

    /// The allowed attribute targets for an F# type declaration
    val TyconDecl: AttributeTargets

    /// The allowed attribute targets for an F# exception declaration
    val ExnDecl: AttributeTargets

    /// The allowed attribute targets for an F# module declaration
    val ModuleDecl: AttributeTargets

    /// The allowed attribute targets for an F# top level 'do' expression
    val Top: AttributeTargets

module BindingNormalization =
    /// Take a syntactic binding and do the very first processing step to normalize it.
    val NormalizeBinding: isObjExprBinding: IsObjExprBinding -> cenv: TcFileState -> env: TcEnv -> binding: SynBinding -> NormalizedBinding

