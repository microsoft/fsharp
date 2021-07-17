// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.


//-------------------------------------------------------------------------
// Incremental type inference constraint solving.  
//
// Primary constraints are:
//   - type equations        ty1 = ty2
//   - subtype inequations   ty1 :> ty2
//   - trait constraints     tyname: (static member op_Addition: 'a * 'b -> 'c)
//
// Plus some other constraints inherited from .NET generics.
// 
// The constraints are immediately processed into a normal form, in particular
//   - type equations on inference parameters: 'tp = ty
//   - type inequations on inference parameters: 'tp :> ty
//   - other constraints on inference parameters
//
// The state of the inference engine is kept in imperative mutations to inference
// type variables.
//
// The use of the normal form allows the state of the inference engine to 
// be queried for type-directed name resolution, type-directed overload 
// resolution and when generating warning messages.
//
// The inference engine can be used in 'undo' mode to implement
// can-unify predicates used in method overload resolution and trait constraint
// satisfaction.
//
// The two main principles are:
//   1. Ensure any solution that is found is sound (no logic is skipped), 
//   2. Because of method overloading and SRTP constraints and other constructs, processing of
//      constraints is algorithmic and must proceed in a definite, fixed order.
//      Once we start doing resolutions in a particular order we must keep doing them
//      in the same order.
//
// There is little use of back-tracking/undo or "retry" in the constraint solver, except in the
// limited case ofs of SRTP solving and method overloading, and some other adhoc limited cases
// like checking for "printf" format strings.  As a result there are cases involving
// method overloading and SRTP that the solver "can't solve". This is intentional and by-design.
//------------------------------------------------------------------------- 

module internal FSharp.Compiler.ConstraintSolver

open Internal.Utilities.Collections
open Internal.Utilities.Library
open Internal.Utilities.Library.Extras
open Internal.Utilities.Rational

open FSharp.Compiler 
open FSharp.Compiler.AbstractIL 
open FSharp.Compiler.AccessibilityLogic
open FSharp.Compiler.AttributeChecking
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Features
open FSharp.Compiler.Import
open FSharp.Compiler.InfoReader
open FSharp.Compiler.Infos
open FSharp.Compiler.MethodCalls
open FSharp.Compiler.NameResolution
open FSharp.Compiler.Syntax
open FSharp.Compiler.Syntax.PrettyNaming
open FSharp.Compiler.SyntaxTreeOps
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.TypeRelations

//-------------------------------------------------------------------------
// Generate type variables and record them in within the scope of the
// compilation environment, which currently corresponds to the scope
// of the constraint resolution carried out by type checking.
//------------------------------------------------------------------------- 
   
let compgenId = mkSynId range0 unassignedTyparName

let NewCompGenTypar (kind, rigid, staticReq, dynamicReq, error) = 
    Construct.NewTypar(kind, rigid, SynTypar(compgenId, staticReq, true), error, dynamicReq, [], false, false) 
    
let AnonTyparId m = mkSynId m unassignedTyparName

let NewAnonTypar (kind, m, rigid, var, dyn) = 
    Construct.NewTypar (kind, rigid, SynTypar(AnonTyparId m, var, true), false, dyn, [], false, false)
    
let NewNamedInferenceMeasureVar (_m, rigid, var, id) = 
    Construct.NewTypar(TyparKind.Measure, rigid, SynTypar(id, var, false), false, TyparDynamicReq.No, [], false, false) 

let NewInferenceMeasurePar () =
    NewCompGenTypar (TyparKind.Measure, TyparRigidity.Flexible, TyparStaticReq.None, TyparDynamicReq.No, false)

let NewErrorTypar () =
    NewCompGenTypar (TyparKind.Type, TyparRigidity.Flexible, TyparStaticReq.None, TyparDynamicReq.No, true)

let NewErrorMeasureVar () =
    NewCompGenTypar (TyparKind.Measure, TyparRigidity.Flexible, TyparStaticReq.None, TyparDynamicReq.No, true)

let NewInferenceType () =
    mkTyparTy (Construct.NewTypar (TyparKind.Type, TyparRigidity.Flexible, SynTypar(compgenId, TyparStaticReq.None, true), false, TyparDynamicReq.No, [], false, false))

let NewErrorType () =
    mkTyparTy (NewErrorTypar ())

let NewErrorMeasure () = Measure.Var (NewErrorMeasureVar ())

let NewByRefKindInferenceType (g: TcGlobals) m = 
    let tp = Construct.NewTypar (TyparKind.Type, TyparRigidity.Flexible, SynTypar(compgenId, TyparStaticReq.HeadType, true), false, TyparDynamicReq.No, [], false, false)
    if g.byrefkind_InOut_tcr.CanDeref then
        tp.SetConstraints [TyparConstraint.DefaultsTo(10, TType_app(g.byrefkind_InOut_tcr, []), m)]
    mkTyparTy tp

let NewInferenceTypes l = l |> List.map (fun _ -> NewInferenceType ()) 

let FreshenAndFixupTypars (traitCtxt: ITraitContext option) m rigid fctps tinst tpsorig = 
    let copy_tyvar (tp: Typar) = NewCompGenTypar (tp.Kind, rigid, tp.StaticReq, (if rigid=TyparRigidity.Rigid then TyparDynamicReq.Yes else TyparDynamicReq.No), false)
    let tps = tpsorig |> List.map copy_tyvar 
    let renaming, tinst = FixupNewTypars traitCtxt m fctps tinst tpsorig tps
    tps, renaming, tinst

let FreshenTypeInst traitCtxt m tpsorig =
    FreshenAndFixupTypars traitCtxt m TyparRigidity.Flexible [] [] tpsorig 

let FreshenMethInst traitCtxt m fctps tinst tpsorig =
    FreshenAndFixupTypars traitCtxt m TyparRigidity.Flexible fctps tinst tpsorig 

let FreshenTypars traitCtxt m tpsorig = 
    match tpsorig with 
    | [] -> []
    | _ -> 
        let _, _, tptys = FreshenTypeInst traitCtxt m tpsorig
        tptys

let FreshenMethInfo traitCtxt m (minfo: MethInfo) =
    let _, _, tptys = FreshenMethInst traitCtxt m (minfo.GetFormalTyparsOfDeclaringType m) minfo.DeclaringTypeInst minfo.FormalMethodTypars
    tptys

//-------------------------------------------------------------------------
// Unification of types: solve/record equality constraints
// Subsumption of types: solve/record subtyping constraints
//------------------------------------------------------------------------- 

[<RequireQualifiedAccess>] 
/// Information about the context of a type equation.
type ContextInfo =

    /// No context was given.
    | NoContext

    /// The type equation comes from an IF expression.
    | IfExpression of range

    /// The type equation comes from an omitted else branch.
    | OmittedElseBranch of range

    /// The type equation comes from a type check of the result of an else branch.
    | ElseBranchResult of range

    /// The type equation comes from the verification of record fields.
    | RecordFields

    /// The type equation comes from the verification of a tuple in record fields.
    | TupleInRecordFields

    /// The type equation comes from a list or array constructor
    | CollectionElement of bool * range

    /// The type equation comes from a return in a computation expression.

    | ReturnInComputationExpression

    /// The type equation comes from a yield in a computation expression.
    | YieldInComputationExpression

    /// The type equation comes from a runtime type test.
    | RuntimeTypeTest of bool

    /// The type equation comes from an downcast where a upcast could be used.
    | DowncastUsedInsteadOfUpcast of bool

    /// The type equation comes from a return type of a pattern match clause (not the first clause).
    | FollowingPatternMatchClause of range

    /// The type equation comes from a pattern match guard.
    | PatternMatchGuard of range

    /// The type equation comes from a sequence expression.
    | SequenceExpression of TType

/// Captures relevant information for a particular failed overload resolution.
type OverloadInformation = 
    {
        methodSlot: CalledMeth<Expr>
        infoReader : InfoReader
        error: exn
    }

/// Cases for overload resolution failure that exists in the implementation of the compiler.
type OverloadResolutionFailure =
  | NoOverloadsFound   of methodName: string
                        * candidates: OverloadInformation list 
                        * cx: TraitConstraintInfo option
  | PossibleCandidates of methodName: string 
                        * candidates: OverloadInformation list // methodNames may be different (with operators?), this is refactored from original logic to assemble overload failure message
                        * cx: TraitConstraintInfo option

exception ConstraintSolverTupleDiffLengths of displayEnv: DisplayEnv * TType list * TType list * range * range

exception ConstraintSolverInfiniteTypes of displayEnv: DisplayEnv * contextInfo: ContextInfo * TType * TType * range * range

exception ConstraintSolverTypesNotInEqualityRelation of displayEnv: DisplayEnv * TType * TType * range * range * ContextInfo

exception ConstraintSolverTypesNotInSubsumptionRelation of displayEnv: DisplayEnv * argTy: TType * paramTy: TType * callRange: range * parameterRange: range

exception ConstraintSolverMissingConstraint of displayEnv: DisplayEnv * Typar * TyparConstraint * range  * range 

exception ConstraintSolverError of string * range * range

exception ConstraintSolverRelatedInformation of string option * range * exn 

exception ErrorFromApplyingDefault of tcGlobals: TcGlobals * displayEnv: DisplayEnv * Typar * TType * exn * range

exception ErrorFromAddingTypeEquation of tcGlobals: TcGlobals * displayEnv: DisplayEnv * actualTy: TType * expectedTy: TType * exn * range

exception ErrorsFromAddingSubsumptionConstraint of tcGlobals: TcGlobals * displayEnv: DisplayEnv * actualTy: TType * expectedTy: TType * exn * ContextInfo * parameterRange: range

exception ErrorFromAddingConstraint of displayEnv: DisplayEnv * exn * range

exception UnresolvedOverloading of displayEnv: DisplayEnv * callerArgs: CallerArgs<Expr> * failure: OverloadResolutionFailure * range

exception UnresolvedConversionOperator of displayEnv: DisplayEnv * TType * TType * range

type TcValF = (ValRef -> ValUseFlag -> TType list -> range -> Expr * TType)

type ConstraintSolverState = 
    { 
      g: TcGlobals

      amap: Import.ImportMap 

      InfoReader: InfoReader

      /// The function used to freshen values we encounter during trait constraint solving
      TcVal: TcValF

      /// This table stores all unsolved, ungeneralized trait constraints, indexed by free type variable.
      /// That is, there will be one entry in this table for each free type variable in 
      /// each outstanding, unsolved, ungeneralized trait constraint. Constraints are removed from the table and resolved 
      /// each time a solution to an index variable is found. 
      mutable ExtraCxs: HashMultiMap<Stamp, (TraitConstraintInfo * range)>
    }

    static member New(g, amap, infoReader, tcVal) = 
        { g = g 
          amap = amap 
          ExtraCxs = HashMultiMap(10, HashIdentity.Structural)
          InfoReader = infoReader
          TcVal = tcVal } 

type ConstraintSolverEnv = 
    { 
      SolverState: ConstraintSolverState

      eContextInfo: ContextInfo

      MatchingOnly: bool

      m: range

      EquivEnv: TypeEquivEnv

      DisplayEnv: DisplayEnv
    }

    member csenv.InfoReader = csenv.SolverState.InfoReader

    member csenv.g = csenv.SolverState.g

    member csenv.amap = csenv.SolverState.amap
    
    override csenv.ToString() = "<ConstraintSolverEnv> @ " + csenv.m.ToString()

let MakeConstraintSolverEnv contextInfo css m denv = 
    { SolverState = css
      m = m
      eContextInfo = contextInfo
      // Indicates that when unifying ty1 = ty2, only type variables in ty1 may be solved 
      MatchingOnly = false
      EquivEnv = TypeEquivEnv.Empty 
      DisplayEnv = denv }

/// Check whether a type variable occurs in the r.h.s. of a type, e.g. to catch
/// infinite equations such as 
///    'a = list<'a>
let rec occursCheck g un ty = 
    match stripTyEqns g ty with 
    | TType_ucase(_, l)
    | TType_app (_, l) 
    | TType_anon(_, l)
    | TType_tuple (_, l) -> List.exists (occursCheck g un) l
    | TType_fun (d, r) -> occursCheck g un d || occursCheck g un r
    | TType_var r   ->  typarEq un r 
    | TType_forall (_, tau) -> occursCheck g un tau
    | _ -> false 


//-------------------------------------------------------------------------
// Predicates on types
//------------------------------------------------------------------------- 

/// Some additional solutions are forced prior to generalization (permitWeakResolution=true).  These are, roughly speaking, rules
/// for binary-operand constraints arising from constructs such as "1.0 + x" where "x" is an unknown type. THe constraint here
/// involves two type parameters - one for the left, and one for the right.  The left is already known to be Double.
/// In this situation (and in the absence of other evidence prior to generalization), constraint solving forces an assumption that 
/// the right is also Double - this is "weak" because there is only weak evidence for it.
///
/// permitWeakResolution also applies to resolutions of multi-type-variable constraints via method overloads.  Method overloading gets applied even if
/// only one of the two type variables is known.
///
/// During code gen we run with permitWeakResolution on, but we only apply it where one of the argument types for the built-in constraint resolution is
/// a variable type.
type PermitWeakResolution = 

    /// Represents the point where we are generalizing inline code
    | LegacyYesAtInlineGeneralization
    
    /// Represents points where we are choosing a default solution to trait constraints
    | YesAtChooseSolution

    /// Represents invocations of the constraint solver during codegen or inlining to determine witnesses
    | LegacyYesAtCodeGen

    /// No weak resolution allowed
    | No

    /// Determine if the weak resolution flag means we perform overload resolution
    /// based on weak information.
    member x.PerformWeakOverloadResolution (g: TcGlobals) =
        if g.langVersion.SupportsFeature LanguageFeature.ExtensionConstraintSolutions then 
            match x with
            | YesAtChooseSolution -> true
            | LegacyYesAtCodeGen
            | LegacyYesAtInlineGeneralization
            | No -> false
        else
            //legacy 
            match x with
            | YesAtChooseSolution
            | LegacyYesAtCodeGen
            | LegacyYesAtInlineGeneralization -> true
            | No -> false

// Get measure of type, float<_> or float32<_> or decimal<_> but not float=float<1> or float32=float32<1> or decimal=decimal<1> 
let GetMeasureOfType g ty =
    match ty with 
    | AppTy g (tcref, [tyarg]) ->
        match stripTyEqns g tyarg with  
        | TType_measure ms when not (measureEquiv g ms Measure.One) -> Some (tcref, ms)
        | _ -> None
    | _ -> None

let rec isNativeIntegerTy g ty =
    typeEquivAux EraseMeasures g g.nativeint_ty ty || 
    typeEquivAux EraseMeasures g g.unativeint_ty ty ||
    (isEnumTy g ty && isNativeIntegerTy g (underlyingTypeOfEnumTy g ty))

let isSignedIntegerTy g ty =
    typeEquivAux EraseMeasures g g.sbyte_ty ty || 
    typeEquivAux EraseMeasures g g.int16_ty ty || 
    typeEquivAux EraseMeasures g g.int32_ty ty || 
    typeEquivAux EraseMeasures g g.nativeint_ty ty || 
    typeEquivAux EraseMeasures g g.int64_ty ty 

let isUnsignedIntegerTy g ty =
    typeEquivAux EraseMeasures g g.byte_ty ty || 
    typeEquivAux EraseMeasures g g.uint16_ty ty || 
    typeEquivAux EraseMeasures g g.uint32_ty ty || 
    typeEquivAux EraseMeasures g g.unativeint_ty ty || 
    typeEquivAux EraseMeasures g g.uint64_ty ty 

let rec IsIntegerOrIntegerEnumTy g ty =
    isSignedIntegerTy g ty || 
    isUnsignedIntegerTy g ty || 
    (isEnumTy g ty && IsIntegerOrIntegerEnumTy g (underlyingTypeOfEnumTy g ty))
    
let isIntegerTy g ty =
    isSignedIntegerTy g ty || 
    isUnsignedIntegerTy g ty 
    
let isStringTy g ty = typeEquiv g g.string_ty ty 

let isCharTy g ty = typeEquiv g g.char_ty ty 

let isBoolTy g ty = typeEquiv g g.bool_ty ty 

/// float or float32 or float<_> or float32<_> 
let isFpTy g ty =
    typeEquivAux EraseMeasures g g.float_ty ty || 
    typeEquivAux EraseMeasures g g.float32_ty ty 

/// decimal or decimal<_>
let isDecimalTy g ty = 
    typeEquivAux EraseMeasures g g.decimal_ty ty 

// int*, int*<_> float*, float<_>*, enums
//
// Addition is supported on these via built-in resolution.  Note even on enums, so
//    System.DayOfWeek.Monday + System.DayOfWeek.Tuesday
// is allowed 
let IsNonDecimalNumericOrIntegralEnumType g ty = IsIntegerOrIntegerEnumTy g ty || isFpTy g ty

// int*, int*<_> float*, float<_>*, enums, decimal, decimal<_>
//
// This is used for one side of multiplication supported via built-in resolution
let IsNumericOrIntegralEnumType g ty = IsNonDecimalNumericOrIntegralEnumType g ty || isDecimalTy g ty

// decimal<_> but not decimal
let IsUnitizedDecimalType g ty = 
    Option.isSome (GetMeasureOfType g ty) && isDecimalTy g ty

// int*, int*<_> float*, float<_>*, enums, decimal<_> but NOT plain decimal
//
// This is used for other side of multiplication supported via built-in resolution

let IsNonDecimalNumericOrIntegralEnumOrUnitizedDecimalType g ty = 
    IsNonDecimalNumericOrIntegralEnumType g ty || IsUnitizedDecimalType g ty

let IsNonDecimalNumericType g ty = isIntegerTy g ty || isFpTy g ty

let IsNumericType g ty = IsNonDecimalNumericType g ty || isDecimalTy g ty

let IsRelationalType g ty = IsNumericType g ty || isStringTy g ty || isCharTy g ty || isBoolTy g ty

let IsCharOrStringType g ty = isCharTy g ty || isStringTy g ty

/// Checks the argument type for a built-in solution to an op_Addition, op_Subtraction or op_Modulus constraint.
let IsAddSubModType nm g ty =
    IsNonDecimalNumericOrIntegralEnumOrUnitizedDecimalType g ty || (nm = "op_Addition" && IsCharOrStringType g ty)

/// Checks the argument type for a built-in solution to a bitwise operator constraint
let IsBitwiseOpType g ty = IsIntegerOrIntegerEnumTy g ty || (isEnumTy g ty)

// For weak resolution, require a relevant primitive on one side
// For strong resolution
//    - if there are relevant methods require an exact primitive on the other side.
//    - if there are no relevant methods just require a non-variable type on the other side.
let IsBinaryOpArgTypePair p1 p2 permitWeakResolution minfos g ty1 ty2 = 
    p1 ty1 && 
    
    match permitWeakResolution with 
    // During regular inference we apply a builtin resolution if either there are no relevant methods to solve traits (and the type is nominal), or if
    // there are relevant methods we check that the type is precisely correct
    | PermitWeakResolution.No -> 
        if isNil minfos then 
            // compat path
            not (isTyparTy g ty2) &&
            // All built-in rules only apply in cases where left and right operator types are equal (after
            // erasing units)
            typeEquivAux EraseMeasures g ty1 ty2
        else
            // normal path - for builtin binary op solutions we check the underlying types are equivalent
            p2 ty2 &&
            // all built-in rules only apply in cases where left and right operator types are equal (after
            // erasing units)
            typeEquivAux EraseMeasures g ty1 ty2

    // During regular canonicalization (weak resolution) we don't do any check on the other type at all - we 
    // ignore the possibility that method overloads may resolve the constraint
    | PermitWeakResolution.LegacyYesAtInlineGeneralization
    | PermitWeakResolution.YesAtChooseSolution -> 
        // weak resolution lets the other type be a variable type
        isTyparTy g ty2 || 
        // If the other type is not a variable type, it is nominal,
        // and all built-in rules only apply in cases where left and right operator types are equal (after
        // erasing units)
        typeEquivAux EraseMeasures g ty1 ty2
    
    // During codegen we only apply a builtin resolution if both the types are correct
    | PermitWeakResolution.LegacyYesAtCodeGen ->
        p2 ty2 &&
        // All built-in rules only apply in cases where left and right operator types are equal (after
        // erasing units)
        typeEquivAux EraseMeasures g ty1 ty2
    
let IsSymmetricBinaryOpArgTypePair p permitWeakResolution minfos g ty1 ty2 = 
    IsBinaryOpArgTypePair p p permitWeakResolution minfos g ty1 ty2 ||
    IsBinaryOpArgTypePair p p permitWeakResolution minfos g ty2 ty1
    
/// Checks if the knowledge we have of the argument types is enough to commit to a path that simulates that a
/// type supports the op_Addition, op_Subtraction or op_Modulus static members
let IsAddSubModTypePair nm permitWeakResolution minfos g ty1 ty2 =
    IsSymmetricBinaryOpArgTypePair (IsAddSubModType nm g) permitWeakResolution minfos g ty1 ty2  

/// Checks if the knowledge we have of the argument types is enough to commit to a path that simulates that
/// a type supports the op_LessThan, op_LessThanOrEqual, op_GreaterThan, op_GreaterThanOrEqual, op_Equality or op_Inequality static members
let IsRelationalOpArgTypePair permitWeakResolution minfos g ty1 ty2 =
    IsSymmetricBinaryOpArgTypePair (IsRelationalType g) permitWeakResolution minfos g ty1 ty2  

/// Checks if the knowledge we have of the argument types is enough to commit to a path that simulates that
/// a type supports the op_BitwiseAnd, op_BitwiseOr or op_ExclusiveOr static members
let IsBitwiseOpArgTypePair permitWeakResolution minfos g ty1 ty2 =
    IsSymmetricBinaryOpArgTypePair (IsBitwiseOpType g) permitWeakResolution minfos g ty1 ty2  

// So 
//    decimal<_> * decimal<_>
//    decimal<_> * decimal
//    decimal * decimal<_>
// are supported via built-in resolution, but
//    decimal * decimal
// is not since there is an op_Multiply call available for that.
//
// Note 
//     System.DayOfWeek.Monday * System.DayOfWeek.Tuesday
// is allowed for enums, somewhat weirdly but that's how it is
//
let IsMulDivTypeArgPairOneWay permitWeakResolution minfos g ty1 ty2 =
    IsBinaryOpArgTypePair 
        (IsNumericOrIntegralEnumType g) 
        (IsNonDecimalNumericOrIntegralEnumOrUnitizedDecimalType g)
        permitWeakResolution 
        minfos 
        g 
        ty1 ty2  

let IsMulDivTypeArgPair permitWeakResolution minfos g ty1 ty2 =
    IsMulDivTypeArgPairOneWay permitWeakResolution minfos g ty1 ty2 || 
    IsMulDivTypeArgPairOneWay permitWeakResolution minfos g ty2 ty1

/// Checks the argument type for a built-in solution to a get_Sign constraint.
let IsSignType g ty =
    isSignedIntegerTy g ty || isFpTy g ty || isDecimalTy g ty

type TraitConstraintSolution = 
    | TTraitUnsolved
    | TTraitBuiltIn
    | TTraitSolved of MethInfo * TypeInst
    | TTraitSolvedRecdProp of RecdFieldInfo * bool
    | TTraitSolvedAnonRecdProp of AnonRecdTypeInfo * TypeInst * int 

let BakedInTraitConstraintNames =
    [ "op_Division" ; "op_Multiply"; "op_Addition" 
      "op_Equality" ; "op_Inequality"; "op_GreaterThan" ; "op_LessThan"; "op_LessThanOrEqual"; "op_GreaterThanOrEqual"
      "op_Subtraction"; "op_Modulus"
      "get_Zero"; "get_One"
      "DivideByInt";"get_Item"; "set_Item"
      "op_BitwiseAnd"; "op_BitwiseOr"; "op_ExclusiveOr"; "op_LeftShift"
      "op_RightShift"; "op_UnaryPlus"; "op_UnaryNegation"; "get_Sign"; "op_LogicalNot"
      "op_OnesComplement"; "Abs"; "Sqrt"; "Sin"; "Cos"; "Tan"
      "Sinh";  "Cosh"; "Tanh"; "Atan"; "Acos"; "Asin"; "Exp"; "Ceiling"; "Floor"; "Round"; "Log10"; "Log"; "Sqrt"
      "Truncate"; "op_Explicit"
      "Pow"; "Atan2" ]
    |> set
    
//-------------------------------------------------------------------------
// Run the constraint solver with undo (used during method overload resolution)

type Trace = 
    { mutable actions: ((unit -> unit) * (unit -> unit)) list }
    
    static member New () =  { actions = [] }

    member t.Undo () = List.iter (fun (_, a) -> a ()) t.actions
    member t.Push f undo = t.actions <- (f, undo) :: t.actions

type OptionalTrace = 
    | NoTrace
    | WithTrace of Trace

    member x.HasTrace = match x with NoTrace -> false | WithTrace _ -> true

    member t.Exec f undo = 
        match t with        
        | WithTrace trace -> trace.Push f undo; f()
        | NoTrace -> f()

    member t.AddFromReplay source =
        source.actions |> List.rev |>
            match t with        
            | WithTrace trace -> List.iter (fun (action, undo) -> trace.Push action undo; action())
            | NoTrace         -> List.iter (fun (action, _   ) -> action())

    member t.CollectThenUndoOrCommit predicate f =
        let newTrace = Trace.New()
        let res = f newTrace
        match predicate res, t with
        | false, _           -> newTrace.Undo()
        | true, WithTrace t -> t.actions <- newTrace.actions @ t.actions
        | true, NoTrace     -> ()
        res

let CollectThenUndo f = 
    let trace = Trace.New()
    let res = f trace
    trace.Undo()
    res

let FilterEachThenUndo f meths = 
    meths 
    |> List.choose (fun calledMeth -> 
        let trace = Trace.New()        
        let res = f trace calledMeth
        trace.Undo()
        match CheckNoErrorsAndGetWarnings res with 
        | None -> None 
        | Some warns -> Some (calledMeth, warns, trace))

let ShowAccessDomain ad =
    match ad with 
    | AccessibleFromEverywhere -> "public" 
    | AccessibleFrom(_, _) -> "accessible"
    | AccessibleFromSomeFSharpCode -> "public, protected or internal" 
    | AccessibleFromSomewhere -> ""

//-------------------------------------------------------------------------
// Solve

exception NonRigidTypar of displayEnv: DisplayEnv * string option * range * TType * TType * range

/// Signal that there is still an unresolved overload in the constraint problem. The
/// unresolved overload constraint remains in the constraint state, and we skip any
/// further processing related to whichever overall adjustment to constraint solver state
/// is being processed.
///
// NOTE: The addition of this abort+skip appears to be a mistake which has crept into F# type inference,
// and its status is currently under review. See https://github.com/dotnet/fsharp/pull/8294 and others.
//
// Here is the history:
//    1. The local abort was added as part of an attempted performance optimization https://github.com/dotnet/fsharp/pull/1650
//       This change was released in the VS2017 GA release.
//
//    2. However, it also impacts the logic of type inference, by skipping checking.
//       Because of this an attempt was made to revert it in https://github.com/dotnet/fsharp/pull/4173.
//
//       Unfortunately, existing code had begun to depend on the new behaviours enabled by the
//       change, and the revert was abandoned before release in https://github.com/dotnet/fsharp/pull/4348
//
// Comments on soundness:
//    The use of the abort is normally sound because the SRTP constraint
//    will be subject to further processing at a later point.
//
//    However, it seems likely that the abort may result in other processing associated
//    with an overall constraint being skipped (e.g. the processing related to subsequent elements
//    of a tuple constraint).
exception AbortForFailedOverloadResolution

/// This is used at (nearly all) entry points into the constraint solver to make sure that the
/// AbortForFailedOverloadResolution is caught and processing continues.
let inline TryD_IgnoreAbortForFailedOverloadResolution f1 f2 =
    TryD f1 (function AbortForFailedOverloadResolution -> CompleteD | exn -> f2 exn)

/// used to provide detail about non matched argument in overload resolution error message
exception ArgDoesNotMatchError of error: ErrorsFromAddingSubsumptionConstraint * calledMeth: CalledMeth<Expr> * calledArg: CalledArg * callerArg: CallerArg<Expr>

/// Represents a very local condition where we prefer to report errors before stripping type abbreviations.
exception LocallyAbortOperationThatLosesAbbrevs 

let localAbortD = ErrorD LocallyAbortOperationThatLosesAbbrevs

/// Return true if we would rather unify this variable v1 := v2 than vice versa
let PreferUnifyTypar (v1: Typar) (v2: Typar) =
    match v1.Rigidity, v2.Rigidity with 
    // Rigid > all
    | TyparRigidity.Rigid, _ -> false
    // Prefer to unify away WillBeRigid in favour of Rigid
    | TyparRigidity.WillBeRigid, TyparRigidity.Rigid -> true
    | TyparRigidity.WillBeRigid, TyparRigidity.WillBeRigid -> true
    | TyparRigidity.WillBeRigid, TyparRigidity.WarnIfNotRigid -> false
    | TyparRigidity.WillBeRigid, TyparRigidity.Anon -> false
    | TyparRigidity.WillBeRigid, TyparRigidity.Flexible -> false
    // Prefer to unify away WarnIfNotRigid in favour of Rigid
    | TyparRigidity.WarnIfNotRigid, TyparRigidity.Rigid -> true
    | TyparRigidity.WarnIfNotRigid, TyparRigidity.WillBeRigid -> true
    | TyparRigidity.WarnIfNotRigid, TyparRigidity.WarnIfNotRigid -> true
    | TyparRigidity.WarnIfNotRigid, TyparRigidity.Anon -> false
    | TyparRigidity.WarnIfNotRigid, TyparRigidity.Flexible -> false
    // Prefer to unify away anonymous variables in favour of Rigid, WarnIfNotRigid 
    | TyparRigidity.Anon, TyparRigidity.Rigid -> true
    | TyparRigidity.Anon, TyparRigidity.WillBeRigid -> true
    | TyparRigidity.Anon, TyparRigidity.WarnIfNotRigid -> true
    | TyparRigidity.Anon, TyparRigidity.Anon -> true
    | TyparRigidity.Anon, TyparRigidity.Flexible -> false
    // Prefer to unify away Flexible in favour of Rigid, WarnIfNotRigid or Anon
    | TyparRigidity.Flexible, TyparRigidity.Rigid -> true
    | TyparRigidity.Flexible, TyparRigidity.WillBeRigid -> true
    | TyparRigidity.Flexible, TyparRigidity.WarnIfNotRigid -> true
    | TyparRigidity.Flexible, TyparRigidity.Anon -> true
    | TyparRigidity.Flexible, TyparRigidity.Flexible -> 

      // Prefer to unify away compiler generated type vars
      match v1.IsCompilerGenerated, v2.IsCompilerGenerated with
      | true, false -> true
      | false, true -> false
      | _ -> 
         // Prefer to unify away non-error vars - gives better error recovery since we keep
         // error vars lying around, and can avoid giving errors about illegal polymorphism 
         // if they occur 
         match v1.IsFromError, v2.IsFromError with
         | true, false -> false
         | _ -> true

/// Reorder a list of (variable, exponent) pairs so that a variable that is Preferred
/// is at the head of the list, if possible
let FindPreferredTypar vs =
    let rec find vs = 
        match vs with
        | [] -> vs
        | (v: Typar, e) :: vs ->
            match find vs with
            | [] -> [(v, e)]
            | (v', e') :: vs' -> 
                if PreferUnifyTypar v v'
                then (v, e) :: vs
                else (v', e') :: (v, e) :: vs'
    find vs
  
let SubstMeasure (r: Typar) ms = 
    if r.Rigidity = TyparRigidity.Rigid then error(InternalError("SubstMeasure: rigid", r.Range))
    if r.Kind = TyparKind.Type then error(InternalError("SubstMeasure: kind=type", r.Range))

    match r.typar_solution with
    | None -> r.typar_solution <- Some (TType_measure ms)
    | Some _ -> error(InternalError("already solved", r.Range))

let rec TransactStaticReq (csenv: ConstraintSolverEnv) (trace: OptionalTrace) (tpr: Typar) req = 
    let m = csenv.m
    if tpr.Rigidity.ErrorIfUnified && tpr.StaticReq <> req then 
        ErrorD(ConstraintSolverError(FSComp.SR.csTypeCannotBeResolvedAtCompileTime(tpr.Name), m, m)) 
    else
        let orig = tpr.StaticReq
        trace.Exec (fun () -> tpr.SetStaticReq req) (fun () -> tpr.SetStaticReq orig)
        CompleteD

and SolveTypStaticReqTypar (csenv: ConstraintSolverEnv) trace req (tpr: Typar) =
    let orig = tpr.StaticReq
    let req2 = JoinTyparStaticReq req orig
    if orig <> req2 then TransactStaticReq csenv trace tpr req2 else CompleteD

and SolveTypStaticReq (csenv: ConstraintSolverEnv) trace req ty =
    match req with 
    | TyparStaticReq.None -> CompleteD
    | TyparStaticReq.HeadType -> 
        // requires that a type constructor be known at compile time 
        match stripTyparEqns ty with
        | TType_measure ms ->
            let vs = ListMeasureVarOccsWithNonZeroExponents ms
            trackErrors {
                for (tpr, _) in vs do 
                    return! SolveTypStaticReqTypar csenv trace req tpr
            }
        | _ -> 
            match tryAnyParTy csenv.g ty with
            | ValueSome tpr -> SolveTypStaticReqTypar csenv trace req tpr
            | ValueNone -> CompleteD
      
let TransactDynamicReq (trace: OptionalTrace) (tpr: Typar) req = 
    let orig = tpr.DynamicReq
    trace.Exec (fun () -> tpr.SetDynamicReq req) (fun () -> tpr.SetDynamicReq orig)
    CompleteD

let SolveTypDynamicReq (csenv: ConstraintSolverEnv) trace req ty =
    match req with 
    | TyparDynamicReq.No -> CompleteD
    | TyparDynamicReq.Yes -> 
        match tryAnyParTy csenv.g ty with
        | ValueSome tpr when tpr.DynamicReq <> TyparDynamicReq.Yes ->
            TransactDynamicReq trace tpr TyparDynamicReq.Yes
        | _ -> CompleteD

let TransactIsCompatFlex (trace: OptionalTrace) (tpr: Typar) req = 
    let orig = tpr.IsCompatFlex
    trace.Exec (fun () -> tpr.SetIsCompatFlex req) (fun () -> tpr.SetIsCompatFlex orig)
    CompleteD

let SolveTypIsCompatFlex (csenv: ConstraintSolverEnv) trace req ty =
    if req then 
        match tryAnyParTy csenv.g ty with
        | ValueSome tpr when not tpr.IsCompatFlex -> TransactIsCompatFlex trace tpr req
        | _ -> CompleteD
    else
        CompleteD

let SubstMeasureWarnIfRigid (csenv: ConstraintSolverEnv) trace (v: Typar) ms = trackErrors {
    if v.Rigidity.WarnIfUnified && not (isAnyParTy csenv.g (TType_measure ms)) then         
        // NOTE: we grab the name eagerly to make sure the type variable prints as a type variable 
        let tpnmOpt = if v.IsCompilerGenerated then None else Some v.Name
        do! SolveTypStaticReq csenv trace v.StaticReq (TType_measure ms)
        SubstMeasure v ms
        return! WarnD(NonRigidTypar(csenv.DisplayEnv, tpnmOpt, v.Range, TType_measure (Measure.Var v), TType_measure ms, csenv.m))
    else 
        // Propagate static requirements from 'tp' to 'ty'
        do! SolveTypStaticReq csenv trace v.StaticReq (TType_measure ms)
        SubstMeasure v ms
        if v.Rigidity = TyparRigidity.Anon && measureEquiv csenv.g ms Measure.One then 
            return! WarnD(Error(FSComp.SR.csCodeLessGeneric(), v.Range))
        else 
            ()
  }

/// Imperatively unify the unit-of-measure expression ms against 1.
/// There are three cases
/// - ms is (equivalent to) 1
/// - ms contains no non-rigid unit variables, and so cannot be unified with 1
/// - ms has the form v^e * ms' for some non-rigid variable v, non-zero exponent e, and measure expression ms'
///   the most general unifier is then simply v := ms' ^ -(1/e)
let UnifyMeasureWithOne (csenv: ConstraintSolverEnv) trace ms = 
    // Gather the rigid and non-rigid unit variables in this measure expression together with their exponents
    let rigidVars, nonRigidVars = 
        ListMeasureVarOccsWithNonZeroExponents ms
        |> List.partition (fun (v, _) -> v.Rigidity = TyparRigidity.Rigid) 

    // If there is at least one non-rigid variable v with exponent e, then we can unify 
    match FindPreferredTypar nonRigidVars with
    | (v, e) :: vs ->
        let unexpandedCons = ListMeasureConOccsWithNonZeroExponents csenv.g false ms
        let newms = ProdMeasures (List.map (fun (c, e') -> Measure.RationalPower (Measure.Con c, NegRational (DivRational e' e))) unexpandedCons 
                                @ List.map (fun (v, e') -> Measure.RationalPower (Measure.Var v, NegRational (DivRational e' e))) (vs @ rigidVars))

        SubstMeasureWarnIfRigid csenv trace v newms

    // Otherwise we require ms to be 1
    | [] -> if measureEquiv csenv.g ms Measure.One then CompleteD else localAbortD
    
/// Imperatively unify unit-of-measure expression ms1 against ms2
let UnifyMeasures (csenv: ConstraintSolverEnv) trace ms1 ms2 = 
    UnifyMeasureWithOne csenv trace (Measure.Prod(ms1, Measure.Inv ms2))

/// Simplify a unit-of-measure expression ms that forms part of a type scheme. 
/// We make substitutions for vars, which are the (remaining) bound variables
///   in the scheme that we wish to simplify. 
let SimplifyMeasure g vars ms =
    let rec simp vars = 
        match FindPreferredTypar (List.filter (fun (_, e) -> SignRational e<>0) (List.map (fun v -> (v, MeasureVarExponent v ms)) vars)) with
        | [] -> 
          (vars, None)

        | (v, e) :: vs -> 
          let newvar = if v.IsCompilerGenerated then NewAnonTypar (TyparKind.Measure, v.Range, TyparRigidity.Flexible, v.StaticReq, v.DynamicReq)
                                                else NewNamedInferenceMeasureVar (v.Range, TyparRigidity.Flexible, v.StaticReq, v.Id)
          let remainingvars = ListSet.remove typarEq v vars
          let newvarExpr = if SignRational e < 0 then Measure.Inv (Measure.Var newvar) else Measure.Var newvar
          let newms = (ProdMeasures (List.map (fun (c, e') -> Measure.RationalPower (Measure.Con c, NegRational (DivRational e' e))) (ListMeasureConOccsWithNonZeroExponents g false ms)
                                   @ List.map (fun (v', e') -> if typarEq v v' then newvarExpr else Measure.RationalPower (Measure.Var v', NegRational (DivRational e' e))) (ListMeasureVarOccsWithNonZeroExponents ms)))
          SubstMeasure v newms
          match vs with 
          | [] -> (remainingvars, Some newvar) 
          | _ -> simp (newvar :: remainingvars)
    simp vars

// Normalize a type ty that forms part of a unit-of-measure-polymorphic type scheme. 
//  Generalizable are the unit-of-measure variables that remain to be simplified. Generalized
// is a list of unit-of-measure variables that have already been generalized. 
let rec SimplifyMeasuresInType g resultFirst ((generalizable, generalized) as param) ty =
    match stripTyparEqns ty with 
    | TType_ucase(_, l)
    | TType_app (_, l) 
    | TType_anon (_,l)
    | TType_tuple (_, l) -> SimplifyMeasuresInTypes g param l

    | TType_fun (d, r) -> if resultFirst then SimplifyMeasuresInTypes g param [r;d] else SimplifyMeasuresInTypes g param [d;r]        
    | TType_var _   -> param
    | TType_forall (_, tau) -> SimplifyMeasuresInType g resultFirst param tau
    | TType_measure unt -> 
        let generalizable', newlygeneralized = SimplifyMeasure g generalizable unt   
        match newlygeneralized with
        | None -> (generalizable', generalized)
        | Some v -> (generalizable', v :: generalized)

and SimplifyMeasuresInTypes g param tys = 
    match tys with
    | [] -> param
    | ty :: tys -> 
        let param' = SimplifyMeasuresInType g false param ty 
        SimplifyMeasuresInTypes g param' tys

let SimplifyMeasuresInConstraint g param c =
    match c with
    | TyparConstraint.DefaultsTo (_, ty, _) 
    | TyparConstraint.CoercesTo(ty, _) -> SimplifyMeasuresInType g false param ty
    | TyparConstraint.SimpleChoice (tys, _) -> SimplifyMeasuresInTypes g param tys
    | TyparConstraint.IsDelegate (ty1, ty2, _) -> SimplifyMeasuresInTypes g param [ty1;ty2]
    | _ -> param

let rec SimplifyMeasuresInConstraints g param cs = 
    match cs with
    | [] -> param
    | c :: cs ->
        let param' = SimplifyMeasuresInConstraint g param c
        SimplifyMeasuresInConstraints g param' cs

let rec GetMeasureVarGcdInType v ty =
    match stripTyparEqns ty with 
    | TType_ucase(_, l)
    | TType_app (_, l) 
    | TType_anon (_,l)
    | TType_tuple (_, l) -> GetMeasureVarGcdInTypes v l

    | TType_fun (d, r) -> GcdRational (GetMeasureVarGcdInType v d) (GetMeasureVarGcdInType v r)
    | TType_var _   -> ZeroRational
    | TType_forall (_, tau) -> GetMeasureVarGcdInType v tau
    | TType_measure unt -> MeasureVarExponent v unt

and GetMeasureVarGcdInTypes v tys =
    match tys with
    | [] -> ZeroRational
    | ty :: tys -> GcdRational (GetMeasureVarGcdInType v ty) (GetMeasureVarGcdInTypes v tys)
  
// Normalize the exponents on generalizable variables in a type
// by dividing them by their "rational gcd". For example, the type
// float<'u^(2/3)> -> float<'u^(4/3)> would be normalized to produce
// float<'u> -> float<'u^2> by dividing the exponents by 2/3.
let NormalizeExponentsInTypeScheme uvars ty =
  uvars |> List.map (fun v ->
    let expGcd = AbsRational (GetMeasureVarGcdInType v ty)
    if expGcd = OneRational || expGcd = ZeroRational then
        v 
    else
        let v' = NewAnonTypar (TyparKind.Measure, v.Range, TyparRigidity.Flexible, v.StaticReq, v.DynamicReq)
        SubstMeasure v (Measure.RationalPower (Measure.Var v', DivRational OneRational expGcd))
        v')
    
// We normalize unit-of-measure-polymorphic type schemes. There  
// are three reasons for doing this:
//   (1) to present concise and consistent type schemes to the programmer
//   (2) so that we can compute equivalence of type schemes in signature matching
//   (3) in order to produce a list of type parameters ordered as they appear in the (normalized) scheme.
//
// Representing the normal form as a matrix, with a row for each variable or base unit, 
// and a column for each unit-of-measure expression in the "skeleton" of the type. 
// Entries for generalizable variables are integers; other rows may contain non-integer exponents.
//  
// ( 0...0  a1  as1    b1  bs1    c1  cs1    ...)
// ( 0...0  0   0...0  b2  bs2    c2  cs2    ...)
// ( 0...0  0   0...0  0   0...0  c3  cs3    ...)
//...
// ( 0...0  0   0...0  0   0...0  0   0...0  ...)
//
// The normal form is unique; what's more, it can be used to force a variable ordering 
// because the first occurrence of a variable in a type is in a unit-of-measure expression with no 
// other "new" variables (a1, b2, c3, above). 
//
// The corner entries a1, b2, c3 are all positive. Entries lying above them (b1, c1, c2, etc) are
// non-negative and smaller than the corresponding corner entry. Entries as1, bs1, bs2, etc are arbitrary.
//
// Essentially this is the *reduced row echelon* matrix from linear algebra, with adjustment to ensure that
// exponents are integers where possible (in the reduced row echelon form, a1, b2, etc. would be 1, possibly
// forcing other entries to be non-integers).
let SimplifyMeasuresInTypeScheme g resultFirst (generalizable: Typar list) ty constraints =
    // Only bother if we're generalizing over at least one unit-of-measure variable 
    let uvars, vars = 
        generalizable
        |> List.partition (fun v -> v.Kind = TyparKind.Measure && v.Rigidity <> TyparRigidity.Rigid) 
 
    match uvars with
    | [] -> generalizable
    | _ :: _ ->
    let (_, generalized) = SimplifyMeasuresInType g resultFirst (SimplifyMeasuresInConstraints g (uvars, []) constraints) ty
    let generalized' = NormalizeExponentsInTypeScheme generalized ty 
    vars @ List.rev generalized'

let freshMeasure () = Measure.Var (NewInferenceMeasurePar ())

let CheckWarnIfRigid (csenv: ConstraintSolverEnv) ty1 (r: Typar) ty =
    let g = csenv.g
    let denv = csenv.DisplayEnv
    if not r.Rigidity.WarnIfUnified then CompleteD else
    let needsWarning =
        match tryAnyParTy g ty with
        | ValueNone -> true
        | ValueSome tp2 ->
            not tp2.IsCompilerGenerated &&
                (r.IsCompilerGenerated ||
                 // exclude this warning for two identically named user-specified type parameters, e.g. from different mutually recursive functions or types
                 r.DisplayName <> tp2.DisplayName)

    if needsWarning then
        // NOTE: we grab the name eagerly to make sure the type variable prints as a type variable 
        let tpnmOpt = if r.IsCompilerGenerated then None else Some r.Name 
        WarnD(NonRigidTypar(denv, tpnmOpt, r.Range, ty1, ty, csenv.m)) 
    else 
        CompleteD

/// Add the constraint "ty1 = ty" to the constraint problem, where ty1 is a type variable. 
/// Propagate all effects of adding this constraint, e.g. to solve other variables 
let rec SolveTyparEqualsTypePart1 (csenv: ConstraintSolverEnv) m2 (trace: OptionalTrace) ty1 r ty = trackErrors {
    // The types may still be equivalent due to abbreviations, which we are trying not to eliminate 
    if typeEquiv csenv.g ty1 ty then () else
    // The famous 'occursCheck' check to catch "infinite types" like 'a = list<'a> - see also https://github.com/Microsoft/visualfsharp/issues/1170
    if occursCheck csenv.g r ty then return! ErrorD (ConstraintSolverInfiniteTypes(csenv.DisplayEnv, csenv.eContextInfo, ty1, ty, csenv.m, m2)) else
    // Note: warn _and_ continue! 
    do! CheckWarnIfRigid csenv ty1 r ty
    // Record the solution before we solve the constraints, since 
    // We may need to make use of the equation when solving the constraints. 
    // Record a entry in the undo trace if one is provided 
    trace.Exec (fun () -> r.typar_solution <- Some ty) (fun () -> r.typar_solution <- None)
 }  

and SolveTyparEqualsTypePart2 (csenv: ConstraintSolverEnv) ndeep m2 (trace: OptionalTrace) (r: Typar) ty = trackErrors {
    // Only solve constraints if this is not an error var 
    if r.IsFromError then () else

    // Check to see if this type variable is relevant to any trait constraints. 
    // If so, re-solve the relevant constraints. 
    if csenv.SolverState.ExtraCxs.ContainsKey r.Stamp then 
        do! RepeatWhileD ndeep (fun ndeep -> SolveRelevantMemberConstraintsForTypar csenv ndeep PermitWeakResolution.No trace r)

    // Re-solve the other constraints associated with this type variable 
    return! solveTypMeetsTyparConstraints csenv ndeep m2 trace ty r

  }

/// Apply the constraints on 'typar' to the type 'ty'
and solveTypMeetsTyparConstraints (csenv: ConstraintSolverEnv) ndeep m2 trace ty (r: Typar) = trackErrors {
    let g = csenv.g

    // Propagate compat flex requirements from 'tp' to 'ty'
    do! SolveTypIsCompatFlex csenv trace r.IsCompatFlex ty

    // Propagate dynamic requirements from 'tp' to 'ty'
    do! SolveTypDynamicReq csenv trace r.DynamicReq ty

    // Propagate static requirements from 'tp' to 'ty' 
    do! SolveTypStaticReq csenv trace r.StaticReq ty
    
    // Solve constraints on 'tp' w.r.t. 'ty' 
    for e in r.Constraints do
      do!
      match e with
      | TyparConstraint.DefaultsTo (priority, dty, m) -> 
          if typeEquiv g ty dty then 
              CompleteD
          else
              match tryDestTyparTy g ty with
              | ValueNone -> CompleteD
              | ValueSome destTypar ->
                  AddConstraint csenv ndeep m2 trace destTypar (TyparConstraint.DefaultsTo(priority, dty, m))
          
      | TyparConstraint.SupportsNull m2                -> SolveTypeSupportsNull               csenv ndeep m2 trace ty
      | TyparConstraint.IsEnum(underlying, m2)         -> SolveTypeIsEnum                     csenv ndeep m2 trace ty underlying
      | TyparConstraint.SupportsComparison(m2)         -> SolveTypeSupportsComparison         csenv ndeep m2 trace ty
      | TyparConstraint.SupportsEquality(m2)           -> SolveTypeSupportsEquality           csenv ndeep m2 trace ty
      | TyparConstraint.IsDelegate(aty, bty, m2)       -> SolveTypeIsDelegate                 csenv ndeep m2 trace ty aty bty
      | TyparConstraint.IsNonNullableStruct m2         -> SolveTypeIsNonNullableValueType     csenv ndeep m2 trace ty
      | TyparConstraint.IsUnmanaged m2                 -> SolveTypeIsUnmanaged                csenv ndeep m2 trace ty
      | TyparConstraint.IsReferenceType m2             -> SolveTypeIsReferenceType            csenv ndeep m2 trace ty
      | TyparConstraint.RequiresDefaultConstructor m2  -> SolveTypeRequiresDefaultConstructor csenv ndeep m2 trace ty
      | TyparConstraint.SimpleChoice(tys, m2)          -> SolveTypeChoice                     csenv ndeep m2 trace ty tys
      | TyparConstraint.CoercesTo(ty2, m2)             -> SolveTypeSubsumesTypeKeepAbbrevs    csenv ndeep m2 trace None ty2 ty
      | TyparConstraint.MayResolveMember(traitInfo, m2) -> 
          SolveMemberConstraint csenv false PermitWeakResolution.No ndeep m2 trace traitInfo |> OperationResult.ignore
  }

        
and SolveTyparEqualsType (csenv: ConstraintSolverEnv) ndeep m2 (trace: OptionalTrace) ty1 ty = trackErrors {
    let m = csenv.m
    do! DepthCheck ndeep m
    match ty1 with 
    | TType_var r | TType_measure (Measure.Var r) ->
        do! SolveTyparEqualsTypePart1 csenv m2 trace ty1 r ty 
        do! SolveTyparEqualsTypePart2 csenv ndeep m2 trace r ty 
    | _ -> failwith "SolveTyparEqualsType"
    }

// Like SolveTyparEqualsType but asserts all typar equalities simultaneously instead of one by one
and SolveTyparsEqualTypes (csenv: ConstraintSolverEnv) ndeep m2 (trace: OptionalTrace) tptys tys = trackErrors {
    do! (tptys, tys) ||> Iterate2D (fun tpty ty -> 
            match tpty with 
            | TType_var r | TType_measure (Measure.Var r) -> SolveTyparEqualsTypePart1 csenv m2 trace tpty r ty 
            | _ -> failwith "SolveTyparsEqualTypes")
    do! (tptys, tys) ||> Iterate2D (fun tpty ty -> 
            match tpty with 
            | TType_var r | TType_measure (Measure.Var r) -> SolveTyparEqualsTypePart2 csenv ndeep m2 trace r ty 
            | _ -> failwith "SolveTyparsEqualTypes")
 }

and SolveAnonInfoEqualsAnonInfo (csenv: ConstraintSolverEnv) m2 (anonInfo1: AnonRecdTypeInfo) (anonInfo2: AnonRecdTypeInfo) = 
    if evalTupInfoIsStruct anonInfo1.TupInfo <> evalTupInfoIsStruct anonInfo2.TupInfo then ErrorD (ConstraintSolverError(FSComp.SR.tcTupleStructMismatch(), csenv.m,m2)) else
    (match anonInfo1.Assembly, anonInfo2.Assembly with 
        | ccu1, ccu2 -> if not (ccuEq ccu1 ccu2) then ErrorD (ConstraintSolverError(FSComp.SR.tcAnonRecdCcuMismatch(ccu1.AssemblyName, ccu2.AssemblyName), csenv.m,m2)) else ResultD ()
        ) ++ (fun () -> 

    if not (anonInfo1.SortedNames = anonInfo2.SortedNames) then 
        let (|Subset|Superset|Overlap|CompletelyDifferent|) (first, second) =
            let first = Set first
            let second = Set second
            let secondOnly = Set.toList (second - first)
            let firstOnly = Set.toList (first - second)

            if second.IsSubsetOf first then
                Subset firstOnly
            elif second.IsSupersetOf first then
                Superset secondOnly
            elif Set.intersect first second <> Set.empty then
                Overlap(firstOnly, secondOnly)
            else
                CompletelyDifferent(Seq.toList first)
        
        let message =
            match anonInfo1.SortedNames, anonInfo2.SortedNames with
            | Subset missingFields ->
                FSComp.SR.tcAnonRecdFieldNameSubset(string missingFields)
            | Superset extraFields ->
                FSComp.SR.tcAnonRecdFieldNameSuperset(string extraFields)
            | Overlap (missingFields, extraFields) ->
                FSComp.SR.tcAnonRecdFieldNameMismatch(string missingFields, string extraFields)
            | CompletelyDifferent missingFields ->
                FSComp.SR.tcAnonRecdFieldNameDifferent(string missingFields)
        
        ErrorD (ConstraintSolverError(message, csenv.m,m2)) 
    else 
        ResultD ())

/// Add the constraint "ty1 = ty2" to the constraint problem. 
/// Propagate all effects of adding this constraint, e.g. to solve type variables 
and SolveTypeEqualsType (csenv: ConstraintSolverEnv) ndeep m2 (trace: OptionalTrace) (cxsln:(TraitConstraintInfo * TraitConstraintSln) option) ty1 ty2 = 
    let ndeep = ndeep + 1
    let aenv = csenv.EquivEnv
    let g = csenv.g

    match cxsln with
    | Some (traitInfo, traitSln) when traitInfo.Solution.IsNone -> 
        // If this is an overload resolution at this point it's safe to assume the candidate member being evaluated solves this member constraint.
        TransactMemberConstraintSolution traitInfo trace traitSln
    | _ -> ()

    if ty1 === ty2 then CompleteD else

    let canShortcut = not trace.HasTrace
    let sty1 = stripTyEqnsA csenv.g canShortcut ty1
    let sty2 = stripTyEqnsA csenv.g canShortcut ty2

    match sty1, sty2 with 
    // type vars inside forall-types may be alpha-equivalent 
    | TType_var tp1, TType_var tp2 when typarEq tp1 tp2 || (match aenv.EquivTypars.TryFind tp1 with | Some v when typeEquiv g v ty2 -> true | _ -> false) -> CompleteD

    | TType_var tp1, TType_var tp2 when PreferUnifyTypar tp1 tp2 -> SolveTyparEqualsType csenv ndeep m2 trace sty1 ty2
    | TType_var tp1, TType_var tp2 when not csenv.MatchingOnly && PreferUnifyTypar tp2 tp1 -> SolveTyparEqualsType csenv ndeep m2 trace sty2 ty1

    | TType_var r, _ when (r.Rigidity <> TyparRigidity.Rigid) -> SolveTyparEqualsType csenv ndeep m2 trace sty1 ty2
    | _, TType_var r when (r.Rigidity <> TyparRigidity.Rigid) && not csenv.MatchingOnly -> SolveTyparEqualsType csenv ndeep m2 trace sty2 ty1

    // Catch float<_>=float<1>, float32<_>=float32<1> and decimal<_>=decimal<1> 
    | (_, TType_app (tc2, [ms])) when (tc2.IsMeasureableReprTycon && typeEquiv csenv.g sty1 (reduceTyconRefMeasureableOrProvided csenv.g tc2 [ms]))
        -> SolveTypeEqualsType csenv ndeep m2 trace None ms (TType_measure Measure.One)
    | (TType_app (tc2, [ms]), _) when (tc2.IsMeasureableReprTycon && typeEquiv csenv.g sty2 (reduceTyconRefMeasureableOrProvided csenv.g tc2 [ms]))
        -> SolveTypeEqualsType csenv ndeep m2 trace None ms (TType_measure Measure.One)

    | TType_app (tc1, l1), TType_app (tc2, l2) when tyconRefEq g tc1 tc2  -> SolveTypeEqualsTypeEqns csenv ndeep m2 trace None l1 l2
    | TType_app (_, _), TType_app (_, _)   ->  localAbortD
    | TType_tuple (tupInfo1, l1), TType_tuple (tupInfo2, l2)      -> 
        if evalTupInfoIsStruct tupInfo1 <> evalTupInfoIsStruct tupInfo2 then ErrorD (ConstraintSolverError(FSComp.SR.tcTupleStructMismatch(), csenv.m, m2)) else
        SolveTypeEqualsTypeEqns csenv ndeep m2 trace None l1 l2
    | TType_anon (anonInfo1, l1),TType_anon (anonInfo2, l2)      -> 
        SolveAnonInfoEqualsAnonInfo csenv m2 anonInfo1 anonInfo2 ++ (fun () -> 
        SolveTypeEqualsTypeEqns csenv ndeep m2 trace None l1 l2)
    | TType_fun (d1, r1), TType_fun (d2, r2)   -> SolveFunTypeEqn csenv ndeep m2 trace None d1 d2 r1 r2
    | TType_measure ms1, TType_measure ms2   -> UnifyMeasures csenv trace ms1 ms2
    | TType_forall(tps1, rty1), TType_forall(tps2, rty2) -> 
        if tps1.Length <> tps2.Length then localAbortD else
        let aenv = aenv.BindEquivTypars tps1 tps2 
        let csenv = {csenv with EquivEnv = aenv }
        if not (typarsAEquiv g aenv tps1 tps2) then localAbortD else
        SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty1 rty2 

    | TType_ucase (uc1, l1), TType_ucase (uc2, l2) when g.unionCaseRefEq uc1 uc2  -> SolveTypeEqualsTypeEqns csenv ndeep m2 trace None l1 l2
    | _  -> localAbortD


and SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace ty1 ty2 = SolveTypeEqualsTypeKeepAbbrevsWithCxsln csenv ndeep m2 trace None ty1 ty2

and private SolveTypeEqualsTypeKeepAbbrevsWithCxsln csenv ndeep m2 trace cxsln ty1 ty2 = 
   // Back out of expansions of type abbreviations to give improved error messages. 
   // Note: any "normalization" of equations on type variables must respect the trace parameter
   TryD (fun () -> SolveTypeEqualsType csenv ndeep m2 trace cxsln ty1 ty2)
        (function
        | LocallyAbortOperationThatLosesAbbrevs -> ErrorD(ConstraintSolverTypesNotInEqualityRelation(csenv.DisplayEnv, ty1, ty2, csenv.m, m2, csenv.eContextInfo))
        | err -> ErrorD err)

and SolveTypeEqualsTypeEqns csenv ndeep m2 trace cxsln origl1 origl2 = 
   match origl1, origl2 with 
   | [], [] -> CompleteD 
   | _ -> 
       // We unwind Iterate2D by hand here for performance reasons.
       let rec loop l1 l2 = 
           match l1, l2 with 
           | [], [] -> CompleteD 
           | h1 :: t1, h2 :: t2 -> 
               SolveTypeEqualsTypeKeepAbbrevsWithCxsln csenv ndeep m2 trace cxsln h1 h2 ++ (fun () -> loop t1 t2) 
           | _ -> 
               ErrorD(ConstraintSolverTupleDiffLengths(csenv.DisplayEnv, origl1, origl2, csenv.m, m2)) 
       loop origl1 origl2

and SolveFunTypeEqn csenv ndeep m2 trace cxsln d1 d2 r1 r2 = trackErrors {
    do! SolveTypeEqualsTypeKeepAbbrevsWithCxsln csenv ndeep m2 trace cxsln d1 d2
    return! SolveTypeEqualsTypeKeepAbbrevsWithCxsln csenv ndeep m2 trace cxsln r1 r2
  }

// ty1: expected
// ty2: actual
//
// "ty2 casts to ty1"
// "a value of type ty2 can be used where a value of type ty1 is expected"
and SolveTypeSubsumesType (csenv: ConstraintSolverEnv) ndeep m2 (trace: OptionalTrace) cxsln ty1 ty2 = 
    // 'a :> obj ---> <solved> 
    let ndeep = ndeep + 1
    let g = csenv.g
    if isObjTy g ty1 then CompleteD else 
    let canShortcut = not trace.HasTrace
    let sty1 = stripTyEqnsA csenv.g canShortcut ty1
    let sty2 = stripTyEqnsA csenv.g canShortcut ty2

    let amap = csenv.amap
    let aenv = csenv.EquivEnv
    let denv = csenv.DisplayEnv

    match sty1, sty2 with 
    | TType_var tp1, _ ->
        match aenv.EquivTypars.TryFind tp1 with
        | Some v -> SolveTypeSubsumesType csenv ndeep m2 trace cxsln v ty2
        | _ ->
        match sty2 with
        | TType_var r2 when typarEq tp1 r2 -> CompleteD
        | TType_var r when not csenv.MatchingOnly -> SolveTyparSubtypeOfType csenv ndeep m2 trace r ty1
        | _ ->  SolveTypeEqualsTypeKeepAbbrevsWithCxsln csenv ndeep m2 trace cxsln ty1 ty2

    | _, TType_var r when not csenv.MatchingOnly -> SolveTyparSubtypeOfType csenv ndeep m2 trace r ty1

    | TType_tuple (tupInfo1, l1), TType_tuple (tupInfo2, l2)      -> 
        if evalTupInfoIsStruct tupInfo1 <> evalTupInfoIsStruct tupInfo2 then ErrorD (ConstraintSolverError(FSComp.SR.tcTupleStructMismatch(), csenv.m, m2)) else
        SolveTypeEqualsTypeEqns csenv ndeep m2 trace cxsln l1 l2 (* nb. can unify since no variance *)

    | TType_anon (anonInfo1, l1), TType_anon (anonInfo2, l2)      -> 
        SolveAnonInfoEqualsAnonInfo csenv m2 anonInfo1 anonInfo2 ++ (fun () -> 
        SolveTypeEqualsTypeEqns csenv ndeep m2 trace cxsln l1 l2) (* nb. can unify since no variance *)

    | TType_fun (d1, r1), TType_fun (d2, r2)   -> SolveFunTypeEqn csenv ndeep m2 trace cxsln d1 d2 r1 r2 (* nb. can unify since no variance *)

    | TType_measure ms1, TType_measure ms2    -> UnifyMeasures csenv trace ms1 ms2

    // Enforce the identities float=float<1>, float32=float32<1> and decimal=decimal<1> 
    | (_, TType_app (tc2, [ms])) when (tc2.IsMeasureableReprTycon && typeEquiv csenv.g sty1 (reduceTyconRefMeasureableOrProvided csenv.g tc2 [ms]))
        -> SolveTypeEqualsTypeKeepAbbrevsWithCxsln csenv ndeep m2 trace cxsln ms (TType_measure Measure.One)

    | (TType_app (tc2, [ms]), _) when (tc2.IsMeasureableReprTycon && typeEquiv csenv.g sty2 (reduceTyconRefMeasureableOrProvided csenv.g tc2 [ms]))
        -> SolveTypeEqualsTypeKeepAbbrevsWithCxsln csenv ndeep m2 trace cxsln ms (TType_measure Measure.One)

    // Special subsumption rule for byref tags
    | TType_app (tc1, l1), TType_app (tc2, l2) when tyconRefEq g tc1 tc2  && g.byref2_tcr.CanDeref && tyconRefEq g g.byref2_tcr tc1 ->
        match l1, l2 with 
        | [ h1; tag1 ], [ h2; tag2 ] -> trackErrors {
            do! SolveTypeEqualsType csenv ndeep m2 trace None h1 h2
            match stripTyEqnsA csenv.g canShortcut tag1, stripTyEqnsA csenv.g canShortcut tag2 with 
            | TType_app(tagc1, []), TType_app(tagc2, []) 
                when (tyconRefEq g tagc2 g.byrefkind_InOut_tcr && 
                      (tyconRefEq g tagc1 g.byrefkind_In_tcr || tyconRefEq g tagc1 g.byrefkind_Out_tcr) ) -> ()
            | _ -> return! SolveTypeEqualsType csenv ndeep m2 trace cxsln tag1 tag2
           }
        | _ -> SolveTypeEqualsTypeEqns csenv ndeep m2 trace cxsln l1 l2

    | TType_app (tc1, l1), TType_app (tc2, l2) when tyconRefEq g tc1 tc2  -> 
        SolveTypeEqualsTypeEqns csenv ndeep m2 trace cxsln l1 l2

    | TType_ucase (uc1, l1), TType_ucase (uc2, l2) when g.unionCaseRefEq uc1 uc2  -> 
        SolveTypeEqualsTypeEqns csenv ndeep m2 trace cxsln l1 l2

    | _ ->  
        // By now we know the type is not a variable type 

        // C :> obj ---> <solved> 
        if isObjTy g ty1 then CompleteD else
        
        let m = csenv.m

        // 'a[] :> IList<'b>   ---> 'a = 'b  
        // 'a[] :> ICollection<'b>   ---> 'a = 'b  
        // 'a[] :> IEnumerable<'b>   ---> 'a = 'b  
        // 'a[] :> IReadOnlyList<'b>   ---> 'a = 'b  
        // 'a[] :> IReadOnlyCollection<'b>   ---> 'a = 'b  
        // Note we don't support co-variance on array types nor 
        // the special .NET conversions for these types 
        match ty1 with
        | AppTy g (tcr1, tinst) when
            isArray1DTy g ty2 &&
                (tyconRefEq g tcr1 g.tcref_System_Collections_Generic_IList || 
                 tyconRefEq g tcr1 g.tcref_System_Collections_Generic_ICollection || 
                 tyconRefEq g tcr1 g.tcref_System_Collections_Generic_IReadOnlyList || 
                 tyconRefEq g tcr1 g.tcref_System_Collections_Generic_IReadOnlyCollection || 
                 tyconRefEq g tcr1 g.tcref_System_Collections_Generic_IEnumerable) ->
            match tinst with 
            | [ty1arg] -> 
                let ty2arg = destArrayTy g ty2
                SolveTypeEqualsTypeKeepAbbrevsWithCxsln csenv ndeep m2 trace cxsln ty1arg ty2arg
            | _ -> error(InternalError("destArrayTy", m))

        | _ ->
            // D<inst> :> Head<_> --> C<inst'> :> Head<_> for the 
            // first interface or super-class C supported by D which 
            // may feasibly convert to Head. 
            match FindUniqueFeasibleSupertype g amap m ty1 ty2 with 
            | None -> ErrorD(ConstraintSolverTypesNotInSubsumptionRelation(denv, ty1, ty2, m, m2))
            | Some t -> SolveTypeSubsumesType csenv ndeep m2 trace cxsln ty1 t

and SolveTypeSubsumesTypeKeepAbbrevs csenv ndeep m2 trace cxsln ty1 ty2 = 
   let denv = csenv.DisplayEnv
   TryD (fun () -> SolveTypeSubsumesType csenv ndeep m2 trace cxsln ty1 ty2)
        (function 
         | LocallyAbortOperationThatLosesAbbrevs -> ErrorD(ConstraintSolverTypesNotInSubsumptionRelation(denv, ty1, ty2, csenv.m, m2))
         | err -> ErrorD err)

//-------------------------------------------------------------------------
// Solve and record non-equality constraints
//------------------------------------------------------------------------- 

and SolveTyparSubtypeOfType (csenv: ConstraintSolverEnv) ndeep m2 trace tp ty1 = 
    let g = csenv.g
    if isObjTy g ty1 then CompleteD
    elif typeEquiv g ty1 (mkTyparTy tp) then CompleteD
    elif isSealedTy g ty1 then 
        SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace (mkTyparTy tp) ty1
    else
        AddConstraint csenv ndeep m2 trace tp (TyparConstraint.CoercesTo(ty1, csenv.m))

and DepthCheck ndeep m = 
  if ndeep > 300 then error(Error(FSComp.SR.csTypeInferenceMaxDepth(), m)) else CompleteD

// If this is a type that's parameterized on a unit-of-measure (expected to be numeric), unify its measure with 1
and SolveDimensionlessNumericType (csenv: ConstraintSolverEnv) ndeep m2 trace ty =
    match getMeasureOfType csenv.g ty with
    | Some (tcref, _) -> 
        SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace ty (mkAppTy tcref [TType_measure Measure.One])
    | None ->
        CompleteD

/// Attempt to solve a statically resolved member constraint.
///
/// 1. We do a bunch of fakery to pretend that primitive types have certain members. 
///    We pretend int and other types support a number of operators.  In the actual IL for mscorlib they 
///    don't. The type-directed static optimization rules in the library code that makes use of this 
///    will deal with the problem. 
///
/// 2. Some additional solutions are forced prior to generalization (permitWeakResolution= Yes or YesDuringCodeGen). See above
and SolveMemberConstraint (csenv: ConstraintSolverEnv) ignoreUnresolvedOverload (permitWeakResolution: PermitWeakResolution) ndeep m2 trace traitInfo : OperationResult<bool> = trackErrors {
    let (TTrait(tys, nm, memFlags, traitObjAndArgTys, rty, sln, traitCtxt)) = traitInfo
    let traitAD = match traitCtxt with None -> AccessorDomain.AccessibleFromEverywhere | Some c -> (c.AccessRights :?> AccessorDomain)
    // Do not re-solve if already solved
    if sln.Value.IsSome then return true else
    let g = csenv.g
    let m = csenv.m
    let amap = csenv.amap
    let aenv = csenv.EquivEnv
    let denv = csenv.DisplayEnv
    let ndeep = ndeep + 1
    do! DepthCheck ndeep m

    // Remove duplicates from the set of types in the support 
    let tys = ListSet.setify (typeAEquiv g aenv) tys

    // Rebuild the trait info after removing duplicates 
    let traitInfo = TTrait(tys, nm, memFlags, traitObjAndArgTys, rty, sln, traitCtxt)
    let rty = GetFSharpViewOfReturnType g rty    
    
    // Assert the object type if the constraint is for an instance member    
    if memFlags.IsInstance then 
        match tys, traitObjAndArgTys with 
        | [ty], (h :: _) -> do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace h ty 
        | _ -> do! ErrorD (ConstraintSolverError(FSComp.SR.csExpectedArguments(), m, m2))

    // Trait calls are only supported on pseudo type (variables) 
    for e in tys do
        do! SolveTypStaticReq csenv trace TyparStaticReq.HeadType e
    
    let argtys = if memFlags.IsInstance then List.tail traitObjAndArgTys else traitObjAndArgTys 

    let minfos = GetRelevantMethodsForTrait csenv permitWeakResolution nm traitInfo
        
    let! res = 
     trackErrors {
      match tys, memFlags.IsInstance, nm, argtys with 
      | _, false, ("op_Division" | "op_Multiply"), [argty1;argty2]
          when 
               IsMulDivTypeArgPair permitWeakResolution minfos g argty1 argty2 ->
                   
          match getMeasureOfType g argty1 with
          | Some (tcref, ms1) -> 
            let ms2 = freshMeasure ()
            do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace argty2 (mkAppTy tcref [TType_measure ms2])
            do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty (mkAppTy tcref [TType_measure (Measure.Prod(ms1, if nm = "op_Multiply" then ms2 else Measure.Inv ms2))])
            return TTraitBuiltIn

          | _ ->

            match getMeasureOfType g argty2 with
            | Some (tcref, ms2) -> 
              let ms1 = freshMeasure ()
              do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace argty1 (mkAppTy tcref [TType_measure ms1]) 
              do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty (mkAppTy tcref [TType_measure (Measure.Prod(ms1, if nm = "op_Multiply" then ms2 else Measure.Inv ms2))])
              return TTraitBuiltIn

            | _ -> 

              do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace argty2 argty1
              do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty argty1
              return TTraitBuiltIn

      | _, false, ("op_Addition" | "op_Subtraction" | "op_Modulus"), [argty1;argty2] 
          when  IsAddSubModTypePair nm permitWeakResolution minfos g argty1 argty2 -> 

          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace argty2 argty1
          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty argty1
          return TTraitBuiltIn

      | _, false, ("op_LessThan" | "op_LessThanOrEqual" | "op_GreaterThan" | "op_GreaterThanOrEqual" | "op_Equality" | "op_Inequality" ), [argty1;argty2] 
          when IsRelationalOpArgTypePair permitWeakResolution minfos g argty1 argty2 ->

          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace argty2 argty1 
          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty g.bool_ty
          return TTraitBuiltIn

      // We pretend for uniformity that the numeric types have a static property called Zero and One 
      // As with constants, only zero is polymorphic in its units
      | [ty], false, "get_Zero", [] 
          when IsNumericType g ty || isCharTy g ty -> 
          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty ty
          return TTraitBuiltIn

      | [ty], false, "get_One", [] 
          when IsNumericType g ty || isCharTy g ty -> 
          do! SolveDimensionlessNumericType csenv ndeep m2 trace ty 
          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty ty
          return TTraitBuiltIn

      | _, false, "DivideByInt", [argty1;argty2] 
          when isFpTy g argty1 || isDecimalTy g argty1 -> 
          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace argty2 g.int_ty 
          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty argty1
          return TTraitBuiltIn

      // We pretend for uniformity that the 'string' and 'array' types have an indexer property called 'Item' 
      | [ty], true, "get_Item", [argty1] 
          when isStringTy g ty -> 

          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace argty1 g.int_ty 
          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty g.char_ty
          return TTraitBuiltIn

      | [ty], true, "get_Item", argtys
          when isArrayTy g ty -> 

          if rankOfArrayTy g ty <> argtys.Length then do! ErrorD(ConstraintSolverError(FSComp.SR.csIndexArgumentMismatch((rankOfArrayTy g ty), argtys.Length), m, m2))
          for argty in argtys do
              do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace argty g.int_ty
          let ety = destArrayTy g ty
          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty ety
          return TTraitBuiltIn

      | [ty], true, "set_Item", argtys
          when isArrayTy g ty -> 
          
          if rankOfArrayTy g ty <> argtys.Length - 1 then do! ErrorD(ConstraintSolverError(FSComp.SR.csIndexArgumentMismatch((rankOfArrayTy g ty), (argtys.Length - 1)), m, m2))
          let argtys, ety = List.frontAndBack argtys
          for argty in argtys do
              do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace argty g.int_ty
          let etys = destArrayTy g ty
          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace ety etys
          return TTraitBuiltIn

      | _, false, ("op_BitwiseAnd" | "op_BitwiseOr" | "op_ExclusiveOr"), [argty1;argty2] 
          when IsBitwiseOpArgTypePair permitWeakResolution minfos g argty1 argty2 -> 

          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace argty2 argty1
          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty argty1
          do! SolveDimensionlessNumericType csenv ndeep m2 trace argty1
          return TTraitBuiltIn

      | _, false, ("op_LeftShift" | "op_RightShift"), [argty1;argty2] 
          when IsIntegerOrIntegerEnumTy g argty1  -> 

          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace argty2 g.int_ty
          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty argty1
          do! SolveDimensionlessNumericType csenv ndeep m2 trace argty1
          return TTraitBuiltIn

      | _, false, "op_UnaryPlus", [argty] 
          when IsNumericOrIntegralEnumType g argty -> 

          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty argty
          return TTraitBuiltIn

      | _, false, "op_UnaryNegation", [argty] 
          when isSignedIntegerTy g argty || isFpTy g argty || isDecimalTy g argty -> 

          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty argty
          return TTraitBuiltIn

      | _, true, "get_Sign", [] 
          when IsSignType g tys.Head -> 

          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty g.int32_ty
          return TTraitBuiltIn

      | _, false, ("op_LogicalNot" | "op_OnesComplement"), [argty] 
          when IsIntegerOrIntegerEnumTy g argty  -> 

          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty argty
          do! SolveDimensionlessNumericType csenv ndeep m2 trace argty
          return TTraitBuiltIn

      | _, false, "Abs", [argty] 
          when isSignedIntegerTy g argty || isFpTy g argty || isDecimalTy g argty -> 

          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty argty
          return TTraitBuiltIn

      | _, false, "Sqrt", [argty1] 
          when isFpTy g argty1 ->
          match getMeasureOfType g argty1 with
            | Some (tcref, _) -> 
              let ms1 = freshMeasure () 
              do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace argty1 (mkAppTy tcref [TType_measure (Measure.Prod (ms1, ms1))])
              do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty (mkAppTy tcref [TType_measure ms1])
              return TTraitBuiltIn
            | None -> 
              do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty argty1
              return TTraitBuiltIn

      | _, false, ("Sin" | "Cos" | "Tan" | "Sinh" | "Cosh" | "Tanh" | "Atan" | "Acos" | "Asin" | "Exp" | "Ceiling" | "Floor" | "Round" | "Truncate" | "Log10" | "Log" | "Sqrt"), [argty] 
          when isFpTy g argty -> 

          do! SolveDimensionlessNumericType csenv ndeep m2 trace argty
          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty argty
          return TTraitBuiltIn

      // Simulate solutions to op_Implicit and op_Explicit
      | _, false, "op_Explicit", [argty] 
          when (// The input type. 
                (IsNonDecimalNumericOrIntegralEnumType g argty || isStringTy g argty || isCharTy g argty) &&
                // The output type
                (IsNonDecimalNumericOrIntegralEnumType g rty || isCharTy g rty) && 
                // Exclusion: IntPtr and UIntPtr do not support .Parse() from string 
                not (isStringTy g argty && isNativeIntegerTy g rty) &&
                // Exclusion: No conversion from char to decimal
                not (isCharTy g argty && isDecimalTy g rty)) -> 

          return TTraitBuiltIn


      | _, false, "op_Explicit", [argty] 
          when (// The input type. 
                (IsNumericOrIntegralEnumType g argty || isStringTy g argty) &&
                // The output type
                (isDecimalTy g rty)) -> 

          return TTraitBuiltIn

      | _, false, "Pow", [argty1; argty2] 
          when isFpTy g argty1 -> 
          
          do! SolveDimensionlessNumericType csenv ndeep m2 trace argty1
          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace argty2 argty1
          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty argty1 
          return TTraitBuiltIn

      | _, false, "Atan2", [argty1; argty2] 
          when isFpTy g argty1 -> 

          do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace argty2 argty1
          match getMeasureOfType g argty1 with
          | None -> do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty argty1
          | Some (tcref, _) -> do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty (mkAppTy tcref [TType_measure Measure.One])
          return TTraitBuiltIn

      | _ -> 
          // OK, this is not solved by a built-in constraint.
          // Now look for real solutions

          // First look for a solution by a record property
          let recdPropSearch = 
              let isGetProp = nm.StartsWithOrdinal("get_") 
              let isSetProp = nm.StartsWithOrdinal("set_") 
              if argtys.IsEmpty && isGetProp || isSetProp then 
                  let propName = nm.[4..]
                  let props = 
                    tys |> List.choose (fun ty -> 
                        match TryFindIntrinsicNamedItemOfType csenv.InfoReader (propName, traitAD) FindMemberFlag.IgnoreOverrides m ty with
                        | Some (RecdFieldItem rfinfo) 
                              when (isGetProp || rfinfo.RecdField.IsMutable) && 
                                   (rfinfo.IsStatic = not memFlags.IsInstance) && 
                                   IsRecdFieldAccessible amap m traitAD rfinfo.RecdFieldRef &&
                                   not rfinfo.LiteralValue.IsSome && 
                                   not rfinfo.RecdField.IsCompilerGenerated -> 
                            Some (rfinfo, isSetProp)
                        | _ -> None)
                  match props with 
                  | [ prop ] -> Some prop
                  | _ -> None
              else
                  None

          let anonRecdPropSearch = 
              let isGetProp = nm.StartsWith "get_" 
              if isGetProp && memFlags.IsInstance  then 
                  let propName = nm.[4..]
                  let props = 
                    tys |> List.choose (fun ty -> 
                        match NameResolution.TryFindAnonRecdFieldOfType g ty propName with
                        | Some (NameResolution.Item.AnonRecdField(anonInfo, tinst, i, _)) -> Some (anonInfo, tinst, i)
                        | _ -> None)
                  match props with 
                  | [ prop ] -> Some prop
                  | _ -> None
              else
                  None

          // Now check if there are no feasible solutions at all
          match minfos, recdPropSearch, anonRecdPropSearch with 
          | [], None, None when MemberConstraintIsReadyForStrongResolution csenv traitInfo ->
              if tys |> List.exists (isFunTy g) then 
                  return! ErrorD (ConstraintSolverError(FSComp.SR.csExpectTypeWithOperatorButGivenFunction(DecompileOpName nm), m, m2)) 
              elif tys |> List.exists (isAnyTupleTy g) then 
                  return! ErrorD (ConstraintSolverError(FSComp.SR.csExpectTypeWithOperatorButGivenTuple(DecompileOpName nm), m, m2)) 
              else
                  match nm, argtys with 
                  | "op_Explicit", [argty] -> return! ErrorD (ConstraintSolverError(FSComp.SR.csTypeDoesNotSupportConversion((NicePrint.prettyStringOfTy denv argty), (NicePrint.prettyStringOfTy denv rty)), m, m2))
                  | _ -> 
                      let tyString = 
                         match tys with 
                         | [ty] -> NicePrint.minimalStringOfType denv ty
                         | _ -> tys |> List.map (NicePrint.minimalStringOfType denv) |> String.concat ", "
                      let opName = DecompileOpName nm
                      let err = 
                          match opName with 
                          | "?>="  | "?>"  | "?<="  | "?<"  | "?="  | "?<>" 
                          | ">=?"  | ">?"  | "<=?"  | "<?"  | "=?"  | "<>?" 
                          | "?>=?" | "?>?" | "?<=?" | "?<?" | "?=?" | "?<>?" ->
                             if tys.Length = 1 then FSComp.SR.csTypeDoesNotSupportOperatorNullable(tyString, opName)
                             else FSComp.SR.csTypesDoNotSupportOperatorNullable(tyString, opName)
                          | _ -> 
                             if tys.Length = 1 then FSComp.SR.csTypeDoesNotSupportOperator(tyString, opName)
                             else FSComp.SR.csTypesDoNotSupportOperator(tyString, opName)
                      return! ErrorD(ConstraintSolverError(err, m, m2))

          | _ -> 
              let dummyExpr = mkUnit g m
              let calledMethGroup = 
                  minfos 
                    // curried members may not be used to satisfy constraints
                    |> List.choose (fun minfo ->
                          if minfo.IsCurried then 
                              None 
                          else
                              let callerArgs = 
                                { Unnamed = [ (argtys |> List.map (fun argty -> CallerArg(argty, m, false, dummyExpr))) ]
                                  Named = [ [ ] ] }
                              let minst = FreshenMethInfo traitCtxt m minfo
                              let callerObjTys = if memFlags.IsInstance then [ List.head traitObjAndArgTys  ] else []
                              Some(CalledMeth<Expr>(csenv.InfoReader, None, false, FreshenMethInfo traitCtxt, m, traitAD, minfo, minst, minst, None, callerObjTys, callerArgs, false, false, None)))
              
              let methOverloadResult, errors = 
                  trace.CollectThenUndoOrCommit
                      (fun (a, _) -> Option.isSome a)
                      (fun trace -> ResolveOverloading csenv (WithTrace trace) nm ndeep (Some traitInfo) CallerArgs.Empty traitAD calledMethGroup false (Some rty))

              match anonRecdPropSearch, recdPropSearch, methOverloadResult with 
              | Some (anonInfo, tinst, i), None, None -> 
                  // OK, the constraint is solved by a record property. Assert that the return types match.
                  let rty2 = List.item i tinst
                  do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty rty2
                  return TTraitSolvedAnonRecdProp(anonInfo, tinst, i)

              | None, Some (rfinfo, isSetProp), None -> 
                  // OK, the constraint is solved by a record property. Assert that the return types match.
                  let rty2 = if isSetProp then g.unit_ty else rfinfo.FieldType
                  do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty rty2
                  return TTraitSolvedRecdProp(rfinfo, isSetProp)

              | None, None, Some (calledMeth: CalledMeth<_>) -> 
                  // OK, the constraint is solved.
                  let minfo = calledMeth.Method

                  do! errors
                  let isInstance = minfo.IsInstance
                  if isInstance <> memFlags.IsInstance then 
                      return!
                          if isInstance then
                              ErrorD(ConstraintSolverError(FSComp.SR.csMethodFoundButIsNotStatic((NicePrint.minimalStringOfType denv minfo.ApparentEnclosingType), (DecompileOpName nm), nm), m, m2 ))
                          else
                              ErrorD(ConstraintSolverError(FSComp.SR.csMethodFoundButIsStatic((NicePrint.minimalStringOfType denv minfo.ApparentEnclosingType), (DecompileOpName nm), nm), m, m2 ))
                  else 
                      do! CheckMethInfoAttributes g m None minfo
                      return TTraitSolved (minfo, calledMeth.CalledTyArgs)
                          
              | _ -> 
                  let support = GetSupportOfMemberConstraint csenv traitInfo
                  let frees = GetFreeTyparsOfMemberConstraint csenv traitInfo

                  // If there's nothing left to learn then raise the errors.
                  // Note: we should likely call MemberConstraintIsReadyForResolution here when permitWeakResolution=false but for stability
                  // reasons we use the more restrictive isNil frees.
                  if (permitWeakResolution.PerformWeakOverloadResolution g && MemberConstraintIsReadyForWeakResolution csenv traitInfo) || isNil frees then 
                      do! errors  
                  else
                      do! AddMemberConstraint csenv ndeep m2 trace traitInfo support frees
                  
                  if g.langVersion.SupportsFeature LanguageFeature.ExtensionConstraintSolutions then 
                      return TTraitUnsolved
                  else
                      match errors with
                      | ErrorResult (_, UnresolvedOverloading _) when not ignoreUnresolvedOverload && (not (nm = "op_Explicit" || nm = "op_Implicit")) ->
                          return! ErrorD AbortForFailedOverloadResolution
                      | _ -> 
                          return TTraitUnsolved
     }
    return! RecordMemberConstraintSolution csenv.SolverState m trace traitInfo res
  }

/// Record the solution to a member constraint in the mutable reference cell attached to 
/// each member constraint.
and RecordMemberConstraintSolution css m trace traitInfo res =
    match res with 
    | TTraitUnsolved -> 
        ResultD false

    | TTraitSolved (minfo, minst) -> 
        let sln = MemberConstraintSolutionOfMethInfo css m traitInfo.TraitContext minfo minst
        TransactMemberConstraintSolution traitInfo trace sln
        ResultD true

    | TTraitBuiltIn -> 
        TransactMemberConstraintSolution traitInfo trace BuiltInSln
        ResultD true

    | TTraitSolvedRecdProp (rfinfo, isSet) -> 
        let sln = FSRecdFieldSln(rfinfo.TypeInst,rfinfo.RecdFieldRef,isSet)
        TransactMemberConstraintSolution traitInfo trace sln
        ResultD true

    | TTraitSolvedAnonRecdProp (anonInfo, tinst, i) -> 
        let sln = FSAnonRecdFieldSln(anonInfo, tinst, i)
        TransactMemberConstraintSolution traitInfo trace sln
        ResultD true

/// Convert a MethInfo into the data we save in the TAST
and MemberConstraintSolutionOfMethInfo css m traitCtxt minfo minst = 
#if !NO_EXTENSIONTYPING
#else
    // to prevent unused parameter warning
    ignore css
#endif
    match minfo with 
    | ILMeth(_, ilMeth, _) ->
       let mref = IL.mkRefToILMethod (ilMeth.DeclaringTyconRef.CompiledRepresentationForNamedType, ilMeth.RawMetadata)
       let iltref = ilMeth.ILExtensionMethodDeclaringTyconRef |> Option.map (fun tcref -> tcref.CompiledRepresentationForNamedType)
       ILMethSln(ilMeth.ApparentEnclosingType, iltref, mref, minst)

    | FSMeth(_, ty, vref, extInfo) ->  
       FSMethSln(ty, vref, minst, extInfo.IsSome)

    | MethInfo.DefaultStructCtor _ -> 
       error(InternalError("the default struct constructor was the unexpected solution to a trait constraint", m))

#if !NO_EXTENSIONTYPING
    | ProvidedMeth(amap, mi, _, m) -> 
        let g = amap.g
        let minst = []   // GENERIC TYPE PROVIDERS: for generics, we would have an minst here
        let allArgVars, allArgs = minfo.GetParamTypes(amap, m, minst) |> List.concat |> List.mapi (fun i ty -> mkLocal m ("arg"+string i) ty) |> List.unzip
        let objArgVars, objArgs = (if minfo.IsInstance then [mkLocal m "this" minfo.ApparentEnclosingType] else []) |> List.unzip
        let callMethInfoOpt, callExpr, callExprTy = ProvidedMethodCalls.BuildInvokerExpressionForProvidedMethodCall css.TcVal (g, amap, traitCtxt, mi, objArgs, NeverMutates, false, ValUseFlag.NormalValUse, allArgs, m) 
        let closedExprSln = ClosedExprSln (mkLambdas m [] (objArgVars@allArgVars) (callExpr, callExprTy) )

        // If the call is a simple call to an IL method with all the arguments in the natural order, then revert to use ILMethSln.
        // This is important for calls to operators on generated provided types. There is an (unchecked) condition
        // that generative providers do not re=order arguments or insert any more information into operator calls.
        match callMethInfoOpt, callExpr with 
        | Some methInfo, Expr.Op (TOp.ILCall (_, _, _, _, NormalValUse, _, _, ilMethRef, _, methInst, _), [], args, m)
             when (args, (objArgVars@allArgVars)) ||> List.lengthsEqAndForall2 (fun a b -> match a with Expr.Val (v, _, _) -> valEq v.Deref b | _ -> false) ->
                let declaringType = Import.ImportProvidedType amap m (methInfo.PApply((fun x -> x.DeclaringType), m))
                if isILAppTy g declaringType then 
                    let extOpt = None  // EXTENSION METHODS FROM TYPE PROVIDERS: for extension methods coming from the type providers we would have something here.
                    ILMethSln(declaringType, extOpt, ilMethRef, methInst)
                else
                    closedExprSln
        | _ -> 
                closedExprSln

#endif

/// Write into the reference cell stored in the TAST and add to the undo trace if necessary
and TransactMemberConstraintSolution traitInfo (trace: OptionalTrace) sln  =
    let prev = traitInfo.Solution 
    trace.Exec (fun () -> traitInfo.Solution <- Some sln) (fun () -> traitInfo.Solution <- prev)

and GetRelevantExtensionMethodsForTrait m (infoReader: InfoReader) (traitInfo: TraitConstraintInfo) =
    match traitInfo.TraitContext with 
    | None -> []
    | Some traitCtxt -> 
        [ for extMethInfo in traitCtxt.SelectExtensionMethods(traitInfo, m, infoReader=infoReader) do
              yield (extMethInfo :?> MethInfo) ]

/// Only consider overload resolution if canonicalizing or all the types are now nominal. 
/// That is, don't perform resolution if more nominal information may influence the set of available overloads 
and GetRelevantMethodsForTrait (csenv: ConstraintSolverEnv) (permitWeakResolution: PermitWeakResolution) nm (TTrait(tys, _, memFlags, argtys, rty, soln, traitCtxt) as traitInfo) : MethInfo list =
    //let traitAD = traitCtxt.AccessRights :?> AccessorDomain
    
    let results = 
        if permitWeakResolution.PerformWeakOverloadResolution csenv.g || MemberConstraintSupportIsReadyForDeterminingOverloads csenv traitInfo then
            let m = csenv.m
            let minfos = 
                match memFlags.MemberKind with
                | SynMemberKind.Constructor ->
                    tys |> List.map (GetIntrinsicConstructorInfosOfType csenv.SolverState.InfoReader m)
                | _ ->
                    tys |> List.map (GetIntrinsicMethInfosOfType csenv.SolverState.InfoReader (Some nm) AccessibleFromSomeFSharpCode AllowMultiIntfInstantiations.Yes IgnoreOverrides m)

            // Merge the sets so we don't get the same minfo from each side 
            // We merge based on whether minfos use identical metadata or not. 
            let minfos = List.reduce (ListSet.unionFavourLeft MethInfo.MethInfosUseIdenticalDefinitions) minfos
            
            // Get the extension method that may be relevant to solving the constraint as MethInfo objects.
            // Extension members are not used when canonicalizing prior to generalization (permitWeakResolution=true)
            let extMInfos = 
                if MemberConstraintSupportIsReadyForDeterminingOverloads csenv traitInfo then 
                    GetRelevantExtensionMethodsForTrait csenv.m csenv.InfoReader traitInfo
                else []

            let extMInfos = extMInfos |> ListSet.setify MethInfo.MethInfosUseIdenticalDefinitions 

            let minfos = minfos @ extMInfos

            /// Check that the available members aren't hiding a member from the parent (depth 1 only)
            let relevantMinfos = minfos |> List.filter(fun minfo -> not minfo.IsDispatchSlot && not minfo.IsVirtual && minfo.IsInstance)
            minfos
            |> List.filter(fun minfo1 ->
                not(minfo1.IsDispatchSlot && 
                    relevantMinfos
                    |> List.exists (fun minfo2 -> MethInfosEquivByNameAndSig EraseAll true csenv.g csenv.amap m minfo2 minfo1)))
        else 
            []

    // The trait name "op_Explicit" also covers "op_Implicit", so look for that one too.
    if nm = "op_Explicit" then 
        results @ GetRelevantMethodsForTrait csenv permitWeakResolution "op_Implicit" (TTrait(tys, "op_Implicit", memFlags, argtys, rty, soln, traitCtxt))
    else
        results


/// The nominal support of the member constraint 
and GetSupportOfMemberConstraint (csenv: ConstraintSolverEnv) (TTrait(tys, _, _, _, _, _, _)) =
    tys |> List.choose (tryAnyParTyOption csenv.g)
    
/// Check if the support is fully solved.  
and SupportOfMemberConstraintIsFullySolved (csenv: ConstraintSolverEnv) (TTrait(tys, _, _, _, _, _, _)) =
    tys |> List.forall (isAnyParTy csenv.g >> not)

// This may be relevant to future bug fixes, see https://github.com/Microsoft/visualfsharp/issues/3814
// /// Check if some part of the support is solved.  
// and SupportOfMemberConstraintIsPartiallySolved (csenv: ConstraintSolverEnv) (TTrait(tys, _, _, _, _, _)) =
//     tys |> List.exists (isAnyParTy csenv.g >> not)
    
/// Get all the unsolved typars (statically resolved or not) relevant to the member constraint
and GetFreeTyparsOfMemberConstraint (csenv: ConstraintSolverEnv) (TTrait(tys, _, _, argtys, rty, _, _)) =
    freeInTypesLeftToRightSkippingConstraints csenv.g (tys@argtys@ Option.toList rty)

and MemberConstraintIsReadyForWeakResolution csenv traitInfo =
   SupportOfMemberConstraintIsFullySolved csenv traitInfo

and MemberConstraintIsReadyForStrongResolution csenv traitInfo =
   SupportOfMemberConstraintIsFullySolved csenv traitInfo

and MemberConstraintSupportIsReadyForDeterminingOverloads csenv traitInfo =
   SupportOfMemberConstraintIsFullySolved csenv traitInfo

/// Re-solve the global constraints involving any of the given type variables. 
/// Trait constraints can't always be solved using the pessimistic rules. We only canonicalize 
/// them forcefully (permitWeakResolution=true) prior to generalization. 
and SolveRelevantMemberConstraints (csenv: ConstraintSolverEnv) ndeep permitWeakResolution trace tps =
    RepeatWhileD ndeep
        (fun ndeep -> 
            tps 
            |> AtLeastOneD (fun tp -> 
                /// Normalize the typar 
                let ty = mkTyparTy tp
                match tryAnyParTy csenv.g ty with
                | ValueSome tp ->
                    SolveRelevantMemberConstraintsForTypar csenv ndeep permitWeakResolution trace tp
                | ValueNone -> 
                    ResultD false)) 

and SolveRelevantMemberConstraintsForTypar (csenv:ConstraintSolverEnv) ndeep (permitWeakResolution: PermitWeakResolution) (trace:OptionalTrace) tp =
    let cxst = csenv.SolverState.ExtraCxs
    let tpn = tp.Stamp
    let cxs = cxst.FindAll tpn
    if isNil cxs then ResultD false else
    
    trace.Exec (fun () -> cxs |> List.iter (fun _ -> cxst.Remove tpn)) (fun () -> cxs |> List.iter (fun cx -> cxst.Add(tpn, cx)))
    assert (isNil (cxst.FindAll tpn)) 

    cxs 
    |> AtLeastOneD (fun (traitInfo, m2) -> 
        let csenv = { csenv with m = m2 }
        SolveMemberConstraint csenv true permitWeakResolution (ndeep+1) m2 trace traitInfo)

and CanonicalizeRelevantMemberConstraints (csenv: ConstraintSolverEnv) ndeep trace tps isInline =
    let permitWeakResolution = (if isInline then PermitWeakResolution.LegacyYesAtInlineGeneralization else PermitWeakResolution.YesAtChooseSolution)
    SolveRelevantMemberConstraints csenv ndeep permitWeakResolution trace tps
  
and AddMemberConstraint (csenv: ConstraintSolverEnv) ndeep m2 (trace: OptionalTrace) traitInfo support (frees: Typar list) =
    let g = csenv.g
    let aenv = csenv.EquivEnv
    let cxst = csenv.SolverState.ExtraCxs

    // Write the constraint into the global table. That is, 
    // associate the constraint with each type variable in the free variables of the constraint.
    // This will mean the constraint gets resolved whenever one of these free variables gets solved.
    frees 
    |> List.iter (fun tp -> 
        let tpn = tp.Stamp

        let cxs = cxst.FindAll tpn

        // check the constraint is not already listed for this type variable
        if not (cxs |> List.exists (fun (traitInfo2, _valRefs) -> traitsAEquiv g aenv traitInfo traitInfo2)) then 
            trace.Exec (fun () -> csenv.SolverState.ExtraCxs.Add (tpn, (traitInfo, m2))) (fun () -> csenv.SolverState.ExtraCxs.Remove tpn)
    )

    // Associate the constraint with each type variable in the support, so if the type variable
    // gets generalized then this constraint is attached at the binding site.
    trackErrors {
        for tp in support do
            do! AddConstraint csenv ndeep m2 trace tp (TyparConstraint.MayResolveMember(traitInfo, m2))
    }

    
/// Record a constraint on an inference type variable. 
and AddConstraint (csenv: ConstraintSolverEnv) ndeep m2 trace tp newConstraint  =
    let g = csenv.g
    let aenv = csenv.EquivEnv
    let amap = csenv.amap
    let denv = csenv.DisplayEnv
    let m = csenv.m

    // Type variable sets may not have two trait constraints with the same name, nor
    // be constrained by different instantiations of the same interface type.
    //
    // This results in limitations on generic code, especially "inline" code, which 
    // may require type annotations. See FSharp 1.0 bug 6477.
    let consistent tpc1 tpc2 =
        match tpc1, tpc2 with           
        | (TyparConstraint.MayResolveMember(TTrait(tys1, nm1, memFlags1, argtys1, rty1, _, _), _), 
           TyparConstraint.MayResolveMember(TTrait(tys2, nm2, memFlags2, argtys2, rty2, _, _), _))  
              when (memFlags1 = memFlags2 &&
                    nm1 = nm2 &&
                    // Multiple op_Explicit and op_Implicit constraints can exist for the same type variable.
                    // See FSharp 1.0 bug 6477.
                    not (nm1 = "op_Explicit" || nm1 = "op_Implicit") &&
                    argtys1.Length = argtys2.Length &&
                    List.lengthsEqAndForall2 (typeEquiv g) tys1 tys2) -> 

                  let rty1 = GetFSharpViewOfReturnType g rty1
                  let rty2 = GetFSharpViewOfReturnType g rty2
                  trackErrors {
                      do! Iterate2D (SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace) argtys1 argtys2
                      do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace rty1 rty2
                      ()
                  }
          
        | (TyparConstraint.CoercesTo(ty1, _), 
           TyparConstraint.CoercesTo(ty2, _)) ->
              // Record at most one subtype constraint for each head type. 
              // That is, we forbid constraints by both I<string> and I<int>. 
              // This works because the types on the r.h.s. of subtype 
              // constraints are head-types and so any further inferences are equational. 
              let collect ty = 
                  let mutable res = [] 
                  IterateEntireHierarchyOfType (fun x -> res <- x :: res) g amap m AllowMultiIntfInstantiations.No ty
                  List.rev res
              let parents1 = collect ty1
              let parents2 = collect ty2
              trackErrors {
                  for ty1Parent in parents1 do
                      for ty2Parent in parents2 do
                          do! if not (HaveSameHeadType g ty1Parent ty2Parent) then CompleteD else
                              SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace ty1Parent ty2Parent
              }

        | (TyparConstraint.IsEnum (u1, _), 
           TyparConstraint.IsEnum (u2, m2)) ->   
            SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace u1 u2
            
        | (TyparConstraint.IsDelegate (aty1, bty1, _), 
           TyparConstraint.IsDelegate (aty2, bty2, m2)) -> trackErrors {
            do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace aty1 aty2
            return! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace bty1 bty2
          }

        | TyparConstraint.SupportsComparison _, TyparConstraint.IsDelegate _  
        | TyparConstraint.IsDelegate _, TyparConstraint.SupportsComparison _
        | TyparConstraint.IsNonNullableStruct _, TyparConstraint.IsReferenceType _     
        | TyparConstraint.IsReferenceType _, TyparConstraint.IsNonNullableStruct _   ->
            ErrorD (Error(FSComp.SR.csStructConstraintInconsistent(), m))


        | TyparConstraint.SupportsComparison _, TyparConstraint.SupportsComparison _  
        | TyparConstraint.SupportsEquality _, TyparConstraint.SupportsEquality _  
        | TyparConstraint.SupportsNull _, TyparConstraint.SupportsNull _  
        | TyparConstraint.IsNonNullableStruct _, TyparConstraint.IsNonNullableStruct _     
        | TyparConstraint.IsUnmanaged _, TyparConstraint.IsUnmanaged _
        | TyparConstraint.IsReferenceType _, TyparConstraint.IsReferenceType _ 
        | TyparConstraint.RequiresDefaultConstructor _, TyparConstraint.RequiresDefaultConstructor _ 
        | TyparConstraint.SimpleChoice (_, _), TyparConstraint.SimpleChoice (_, _) -> 
            CompleteD
            
        | _ -> CompleteD

    // See when one constraint implies implies another. 
    // 'a :> ty1  implies 'a :> 'ty2 if the head type name of ty2 (say T2) occursCheck anywhere in the hierarchy of ty1 
    // If it does occur, e.g. at instantiation T2<inst2>, then the check above will have enforced that 
    // T2<inst2> = ty2 
    let implies tpc1 tpc2 = 
        match tpc1, tpc2 with           
        | TyparConstraint.MayResolveMember(trait1, _), 
          TyparConstraint.MayResolveMember(trait2, _) -> 
            traitsAEquiv g aenv trait1 trait2

        | TyparConstraint.CoercesTo(ty1, _), TyparConstraint.CoercesTo(ty2, _) -> 
            ExistsSameHeadTypeInHierarchy g amap m ty1 ty2

        | TyparConstraint.IsEnum(u1, _), TyparConstraint.IsEnum(u2, _) -> typeEquiv g u1 u2

        | TyparConstraint.IsDelegate(aty1, bty1, _), TyparConstraint.IsDelegate(aty2, bty2, _) -> 
            typeEquiv g aty1 aty2 && typeEquiv g bty1 bty2 

        | TyparConstraint.SupportsComparison _, TyparConstraint.SupportsComparison _
        | TyparConstraint.SupportsEquality _, TyparConstraint.SupportsEquality _
        // comparison implies equality
        | TyparConstraint.SupportsComparison _, TyparConstraint.SupportsEquality _
        | TyparConstraint.SupportsNull _, TyparConstraint.SupportsNull _
        | TyparConstraint.IsNonNullableStruct _, TyparConstraint.IsNonNullableStruct _
        | TyparConstraint.IsUnmanaged _, TyparConstraint.IsUnmanaged _
        | TyparConstraint.IsReferenceType _, TyparConstraint.IsReferenceType _
        | TyparConstraint.RequiresDefaultConstructor _, TyparConstraint.RequiresDefaultConstructor _ -> true
        | TyparConstraint.SimpleChoice (tys1, _), TyparConstraint.SimpleChoice (tys2, _) -> ListSet.isSubsetOf (typeEquiv g) tys1 tys2
        | TyparConstraint.DefaultsTo (priority1, dty1, _), TyparConstraint.DefaultsTo (priority2, dty2, _) -> 
             (priority1 = priority2) && typeEquiv g dty1 dty2
        | _ -> false
        
    
    // First ensure constraint conforms with existing constraints 
    // NOTE: QUADRATIC 
    let existingConstraints = tp.Constraints

    let allCxs = newConstraint :: List.rev existingConstraints
    trackErrors {
        let rec enforceMutualConsistency i cxs = 
            match cxs with 
            | [] ->  CompleteD
            | cx :: rest -> 
                trackErrors {
                    do! IterateIdxD (fun j cx2 -> if i = j then CompleteD else consistent cx cx2) allCxs 
                    return! enforceMutualConsistency (i+1) rest
                }
        do! enforceMutualConsistency 0 allCxs
    
        let impliedByExistingConstraints = existingConstraints |> List.exists (fun tpc2 -> implies tpc2 newConstraint)
    
        if impliedByExistingConstraints then ()
        // "Default" constraints propagate softly and can be omitted from explicit declarations of type parameters
        elif (match tp.Rigidity, newConstraint with 
              | (TyparRigidity.Rigid | TyparRigidity.WillBeRigid), TyparConstraint.DefaultsTo _ -> true
              | _ -> false) then 
            ()
        elif tp.Rigidity = TyparRigidity.Rigid then
            return! ErrorD (ConstraintSolverMissingConstraint(denv, tp, newConstraint, m, m2)) 
        else
            // It is important that we give a warning if a constraint is missing from a 
            // will-be-made-rigid type variable. This is because the existence of these warnings
            // is relevant to the overload resolution rules (see 'candidateWarnCount' in the overload resolution
            // implementation).
            if tp.Rigidity.WarnIfMissingConstraint then
                do! WarnD (ConstraintSolverMissingConstraint(denv, tp, newConstraint, m, m2))

            let newConstraints = 
                  // Eliminate any constraints where one constraint implies another 
                  // Keep constraints in the left-to-right form according to the order they are asserted. 
                  // NOTE: QUADRATIC 
                  let rec eliminateRedundant cxs acc = 
                      match cxs with 
                      | [] -> acc
                      | cx :: rest -> 
                          let acc = 
                              if List.exists (fun cx2 -> implies cx2 cx) acc then acc
                              else (cx :: acc)
                          eliminateRedundant rest acc
                  
                  eliminateRedundant allCxs []

            // Write the constraint into the type variable 
            // Record a entry in the undo trace if one is provided 
            let orig = tp.Constraints
            trace.Exec (fun () -> tp.SetConstraints newConstraints) (fun () -> tp.SetConstraints orig)
            ()
    }


and SolveTypeSupportsNull (csenv: ConstraintSolverEnv) ndeep m2 trace ty =
    let g = csenv.g
    let m = csenv.m
    let denv = csenv.DisplayEnv
    match tryDestTyparTy g ty with
    | ValueSome destTypar ->
        AddConstraint csenv ndeep m2 trace destTypar (TyparConstraint.SupportsNull m)
    | ValueNone ->
        if TypeSatisfiesNullConstraint g m ty then CompleteD else 
        match ty with 
        | NullableTy g _ ->
            ErrorD (ConstraintSolverError(FSComp.SR.csNullableTypeDoesNotHaveNull(NicePrint.minimalStringOfType denv ty), m, m2))
        | _ -> 
            ErrorD (ConstraintSolverError(FSComp.SR.csTypeDoesNotHaveNull(NicePrint.minimalStringOfType denv ty), m, m2))

and SolveTypeSupportsComparison (csenv: ConstraintSolverEnv) ndeep m2 trace ty =
    let g = csenv.g
    let m = csenv.m
    let amap = csenv.amap
    let denv = csenv.DisplayEnv
    match tryDestTyparTy g ty with
    | ValueSome destTypar ->
        AddConstraint csenv ndeep m2 trace destTypar (TyparConstraint.SupportsComparison m)
    | ValueNone ->
        // Check it isn't ruled out by the user
        match tryTcrefOfAppTy g ty with 
        | ValueSome tcref when HasFSharpAttribute g g.attrib_NoComparisonAttribute tcref.Attribs ->
            ErrorD (ConstraintSolverError(FSComp.SR.csTypeDoesNotSupportComparison1(NicePrint.minimalStringOfType denv ty), m, m2))
        | _ ->
            match ty with 
            | SpecialComparableHeadType g tinst -> 
                tinst |> IterateD (SolveTypeSupportsComparison (csenv: ConstraintSolverEnv) ndeep m2 trace)
            | _ -> 
               // Check the basic requirement - IComparable or IStructuralComparable or assumed
               if ExistsSameHeadTypeInHierarchy g amap m2 ty g.mk_IComparable_ty  ||
                  ExistsSameHeadTypeInHierarchy g amap m2 ty g.mk_IStructuralComparable_ty
               then 
                   // The type is comparable because it implements IComparable
                    match ty with
                    | AppTy g (tcref, tinst) ->
                        // Check the (possibly inferred) structural dependencies
                        (tinst, tcref.TyparsNoRange) ||> Iterate2D (fun ty tp -> 
                            if tp.ComparisonConditionalOn then 
                                SolveTypeSupportsComparison (csenv: ConstraintSolverEnv) ndeep m2 trace ty 
                            else 
                                CompleteD) 
                    | _ ->
                        CompleteD

               // Give a good error for structural types excluded from the comparison relation because of their fields
               elif (isAppTy g ty && 
                     let tcref = tcrefOfAppTy g ty 
                     AugmentWithHashCompare.TyconIsCandidateForAugmentationWithCompare g tcref.Deref && 
                     Option.isNone tcref.GeneratedCompareToWithComparerValues) then
 
                   ErrorD (ConstraintSolverError(FSComp.SR.csTypeDoesNotSupportComparison3(NicePrint.minimalStringOfType denv ty), m, m2))

               else 
                   ErrorD (ConstraintSolverError(FSComp.SR.csTypeDoesNotSupportComparison2(NicePrint.minimalStringOfType denv ty), m, m2))

and SolveTypeSupportsEquality (csenv: ConstraintSolverEnv) ndeep m2 trace ty =
    let g = csenv.g
    let m = csenv.m
    let denv = csenv.DisplayEnv
    match tryDestTyparTy g ty with
    | ValueSome destTypar ->
        AddConstraint csenv ndeep m2 trace destTypar (TyparConstraint.SupportsEquality m)
    | _ ->
        match tryTcrefOfAppTy g ty with 
        | ValueSome tcref when HasFSharpAttribute g g.attrib_NoEqualityAttribute tcref.Attribs ->
            ErrorD (ConstraintSolverError(FSComp.SR.csTypeDoesNotSupportEquality1(NicePrint.minimalStringOfType denv ty), m, m2))
        | _ ->
            match ty with 
            | SpecialEquatableHeadType g tinst -> 
                tinst |> IterateD (SolveTypeSupportsEquality (csenv: ConstraintSolverEnv) ndeep m2 trace)
            | SpecialNotEquatableHeadType g _ -> 
                ErrorD (ConstraintSolverError(FSComp.SR.csTypeDoesNotSupportEquality2(NicePrint.minimalStringOfType denv ty), m, m2))
            | _ -> 
               // The type is equatable because it has Object.Equals(...)
               match ty with
               | AppTy g (tcref, tinst) ->
                   // Give a good error for structural types excluded from the equality relation because of their fields
                   if AugmentWithHashCompare.TyconIsCandidateForAugmentationWithEquals g tcref.Deref && 
                       Option.isNone tcref.GeneratedHashAndEqualsWithComparerValues 
                   then
                       ErrorD (ConstraintSolverError(FSComp.SR.csTypeDoesNotSupportEquality3(NicePrint.minimalStringOfType denv ty), m, m2))
                   else
                       // Check the (possibly inferred) structural dependencies
                       (tinst, tcref.TyparsNoRange) ||> Iterate2D (fun ty tp -> 
                           if tp.EqualityConditionalOn then 
                               SolveTypeSupportsEquality (csenv: ConstraintSolverEnv) ndeep m2 trace ty 
                           else 
                               CompleteD) 
               | _ ->
                   CompleteD
           
and SolveTypeIsEnum (csenv: ConstraintSolverEnv) ndeep m2 trace ty underlying =
    trackErrors {
        let g = csenv.g
        let m = csenv.m
        let denv = csenv.DisplayEnv
        match tryDestTyparTy g ty with
        | ValueSome destTypar ->
            return! AddConstraint csenv ndeep m2 trace destTypar (TyparConstraint.IsEnum(underlying, m))
        | _ ->
            if isEnumTy g ty then 
                do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace underlying (underlyingTypeOfEnumTy g ty) 
                return! CompleteD
            else 
                return! ErrorD (ConstraintSolverError(FSComp.SR.csTypeIsNotEnumType(NicePrint.minimalStringOfType denv ty), m, m2))
    }

and SolveTypeIsDelegate (csenv: ConstraintSolverEnv) ndeep m2 trace ty aty bty =
    trackErrors {
        let g = csenv.g
        let m = csenv.m
        let denv = csenv.DisplayEnv
        match tryDestTyparTy g ty with
        | ValueSome destTypar ->
            return! AddConstraint csenv ndeep m2 trace destTypar (TyparConstraint.IsDelegate(aty, bty, m))
        | _ ->
            if isDelegateTy g ty then 
                match TryDestStandardDelegateType csenv.InfoReader m AccessibleFromSomewhere ty with 
                | Some (tupledArgTy, rty) ->
                    do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace aty tupledArgTy 
                    do! SolveTypeEqualsTypeKeepAbbrevs csenv ndeep m2 trace bty rty 
                | None ->
                    return! ErrorD (ConstraintSolverError(FSComp.SR.csTypeHasNonStandardDelegateType(NicePrint.minimalStringOfType denv ty), m, m2))
            else 
                return! ErrorD (ConstraintSolverError(FSComp.SR.csTypeIsNotDelegateType(NicePrint.minimalStringOfType denv ty), m, m2))
    }
    
and SolveTypeIsNonNullableValueType (csenv: ConstraintSolverEnv) ndeep m2 trace ty =
    trackErrors {
        let g = csenv.g
        let m = csenv.m
        let denv = csenv.DisplayEnv
        match tryDestTyparTy g ty with
        | ValueSome destTypar ->
            return! AddConstraint csenv ndeep m2 trace destTypar (TyparConstraint.IsNonNullableStruct m)
        | _ ->
            let underlyingTy = stripTyEqnsAndMeasureEqns g ty
            if isStructTy g underlyingTy then
                if isNullableTy g underlyingTy then
                    return! ErrorD (ConstraintSolverError(FSComp.SR.csTypeParameterCannotBeNullable(), m, m))
                else
                    return! CompleteD
            else
                return! ErrorD (ConstraintSolverError(FSComp.SR.csGenericConstructRequiresStructType(NicePrint.minimalStringOfType denv ty), m, m2))
    }            

and SolveTypeIsUnmanaged (csenv: ConstraintSolverEnv) ndeep m2 trace ty =
    let g = csenv.g
    let m = csenv.m
    let denv = csenv.DisplayEnv
    match tryDestTyparTy g ty with
    | ValueSome destTypar ->
        AddConstraint csenv ndeep m2 trace destTypar (TyparConstraint.IsUnmanaged m)
    | _ ->
        if isUnmanagedTy g ty then
            CompleteD
        else
            ErrorD (ConstraintSolverError(FSComp.SR.csGenericConstructRequiresUnmanagedType(NicePrint.minimalStringOfType denv ty), m, m2))


and SolveTypeChoice (csenv: ConstraintSolverEnv) ndeep m2 trace ty tys =
    let g = csenv.g
    let m = csenv.m
    let denv = csenv.DisplayEnv
    match tryDestTyparTy g ty with
    | ValueSome destTypar ->
        AddConstraint csenv ndeep m2 trace destTypar (TyparConstraint.SimpleChoice(tys, m)) 
    | _ ->
        if List.exists (typeEquivAux Erasure.EraseMeasures g ty) tys then CompleteD
        else ErrorD (ConstraintSolverError(FSComp.SR.csTypeNotCompatibleBecauseOfPrintf((NicePrint.minimalStringOfType denv ty), (String.concat "," (List.map (NicePrint.prettyStringOfTy denv) tys))), m, m2))


and SolveTypeIsReferenceType (csenv: ConstraintSolverEnv) ndeep m2 trace ty =
    let g = csenv.g
    let m = csenv.m
    let denv = csenv.DisplayEnv
    match tryDestTyparTy g ty with
    | ValueSome destTypar ->
        AddConstraint csenv ndeep m2 trace destTypar (TyparConstraint.IsReferenceType m)
    | _ ->
        if isRefTy g ty then CompleteD
        else ErrorD (ConstraintSolverError(FSComp.SR.csGenericConstructRequiresReferenceSemantics(NicePrint.minimalStringOfType denv ty), m, m))

and SolveTypeRequiresDefaultConstructor (csenv: ConstraintSolverEnv) ndeep m2 trace origTy =
    let g = csenv.g
    let amap = csenv.amap
    let m = csenv.m
    let denv = csenv.DisplayEnv
    let ty = stripTyEqnsAndMeasureEqns g origTy
    match tryDestTyparTy g ty with
    | ValueSome tp ->
        AddConstraint csenv ndeep m2 trace tp (TyparConstraint.RequiresDefaultConstructor m)
    | _ ->
        if isStructTy g ty then
            if isStructTupleTy g ty then 
                destStructTupleTy g ty |> IterateD (SolveTypeRequiresDefaultValue csenv ndeep m trace)
            elif isStructAnonRecdTy g ty then 
                match tryDestAnonRecdTy g ty with
                | ValueNone -> CompleteD
                | ValueSome (_, ptys) -> ptys |> IterateD (SolveTypeRequiresDefaultValue csenv ndeep m trace)
            elif TypeHasDefaultValue g m ty then
                CompleteD
            else
                ErrorD (ConstraintSolverError(FSComp.SR.csGenericConstructRequiresPublicDefaultConstructor(NicePrint.minimalStringOfType denv origTy), m, m2))
        else
            if GetIntrinsicConstructorInfosOfType csenv.InfoReader m ty 
               |> List.exists (fun x -> x.IsNullary && IsMethInfoAccessible amap m AccessibleFromEverywhere x)
            then 
                match tryTcrefOfAppTy g ty with
                | ValueSome tcref when HasFSharpAttribute g g.attrib_AbstractClassAttribute tcref.Attribs ->
                    ErrorD (ConstraintSolverError(FSComp.SR.csGenericConstructRequiresNonAbstract(NicePrint.minimalStringOfType denv origTy), m, m2))
                | _ ->
                    CompleteD
            else
                match tryTcrefOfAppTy g ty with
                | ValueSome tcref when
                    tcref.PreEstablishedHasDefaultConstructor || 
                    // F# 3.1 feature: records with CLIMutable attribute should satisfy 'default constructor' constraint
                    (tcref.IsRecordTycon && HasFSharpAttribute g g.attrib_CLIMutableAttribute tcref.Attribs) ->
                    CompleteD
                | _ -> 
                    ErrorD (ConstraintSolverError(FSComp.SR.csGenericConstructRequiresPublicDefaultConstructor(NicePrint.minimalStringOfType denv origTy), m, m2))

// Note, this constraint arises structurally when processing the element types of struct tuples and struct anonymous records.
//
// In the case of type variables, it requires that the type variable already have been pre-established to be either a (non-nullable) struct
// or a reference type.
and SolveTypeRequiresDefaultValue (csenv: ConstraintSolverEnv) ndeep m2 trace origTy =
    let g = csenv.g
    let m = csenv.m
    let ty = stripTyEqnsAndMeasureEqns g origTy
    if isTyparTy g ty then
        if isNonNullableStructTyparTy g ty then
            SolveTypeRequiresDefaultConstructor csenv ndeep m2 trace ty 
        elif isReferenceTyparTy g ty then
            SolveTypeSupportsNull csenv ndeep m2 trace ty
        else
            ErrorD (ConstraintSolverError(FSComp.SR.csGenericConstructRequiresStructOrReferenceConstraint(), m, m2))
    else
        if isStructTy g ty then
             SolveTypeRequiresDefaultConstructor csenv ndeep m2 trace ty 
        else
             SolveTypeSupportsNull csenv ndeep m2 trace ty

// Parameterized compatibility relation between member signatures.  The real work
// is done by "equateTypes" and "subsumeTypes" and "subsumeArg"
and CanMemberSigsMatchUpToCheck 
      (csenv: ConstraintSolverEnv) 
      permitOptArgs // are we allowed to supply optional and/or "param" arguments?
      alwaysCheckReturn // always check the return type?
      unifyTypes   // used to equate the formal method instantiation with the actual method instantiation for a generic method, and the return types
      subsumeTypes  // used to compare the "obj" type 
      (subsumeArg: CalledArg -> CallerArg<_> -> OperationResult<unit>)    // used to compare the arguments for compatibility
      reqdRetTyOpt 
      (calledMeth: CalledMeth<_>): ImperativeOperationResult =
        trackErrors {
            let g    = csenv.g
            let amap = csenv.amap
            let m    = csenv.m
    
            let minfo = calledMeth.Method
            let minst = calledMeth.CalledTyArgs
            let uminst = calledMeth.CallerTyArgs
            let callerObjArgTys = calledMeth.CallerObjArgTys
            let assignedItemSetters = calledMeth.AssignedItemSetters
            let unnamedCalledOptArgs = calledMeth.UnnamedCalledOptArgs
            let unnamedCalledOutArgs = calledMeth.UnnamedCalledOutArgs

            // First equate the method instantiation (if any) with the method type parameters 
            if minst.Length <> uminst.Length then 
                return! ErrorD(Error(FSComp.SR.csTypeInstantiationLengthMismatch(), m))
            else
                do! Iterate2D unifyTypes minst uminst
                if not (permitOptArgs || isNil unnamedCalledOptArgs) then 
                    return! ErrorD(Error(FSComp.SR.csOptionalArgumentNotPermittedHere(), m)) 
                else
                    let calledObjArgTys = calledMeth.CalledObjArgTys(m)
    
                    // Check all the argument types. 

                    if calledObjArgTys.Length <> callerObjArgTys.Length then 
                        if calledObjArgTys.Length <> 0 then
                            return! ErrorD(Error (FSComp.SR.csMemberIsNotStatic(minfo.LogicalName), m))
                        else
                            return! ErrorD(Error (FSComp.SR.csMemberIsNotInstance(minfo.LogicalName), m))
                    else
                        do! Iterate2D subsumeTypes calledObjArgTys callerObjArgTys
                for argSet in calledMeth.ArgSets do
                    if argSet.UnnamedCalledArgs.Length <> argSet.UnnamedCallerArgs.Length then 
                        return! ErrorD(Error(FSComp.SR.csArgumentLengthMismatch(), m))
                    else
                        do! Iterate2D subsumeArg argSet.UnnamedCalledArgs argSet.UnnamedCallerArgs
                match calledMeth.ParamArrayCalledArgOpt with
                | Some calledArg ->
                    if isArray1DTy g calledArg.CalledArgumentType then 
                        let paramArrayElemTy = destArrayTy g calledArg.CalledArgumentType
                        let reflArgInfo = calledArg.ReflArgInfo // propagate the reflected-arg info to each param array argument
                        match calledMeth.ParamArrayCallerArgs with
                        | Some args ->
                            for callerArg in args do
                                do! subsumeArg (CalledArg((0, 0), false, NotOptional, NoCallerInfo, false, false, None, reflArgInfo, paramArrayElemTy)) callerArg
                        | _ -> ()
                | _ -> ()
                for argSet in calledMeth.ArgSets do
                    for arg in argSet.AssignedNamedArgs do
                        do! subsumeArg arg.CalledArg arg.CallerArg
                for (AssignedItemSetter(_, item, caller)) in assignedItemSetters do
                    let name, calledArgTy = 
                        match item with
                        | AssignedPropSetter(_, pminfo, pminst) -> 
                            let calledArgTy = List.head (List.head (pminfo.GetParamTypes(amap, m, pminst)))
                            pminfo.LogicalName, calledArgTy

                        | AssignedILFieldSetter(finfo) ->
                            (* Get or set instance IL field *)
                            let calledArgTy = finfo.FieldType(amap, m)
                            finfo.FieldName, calledArgTy
                
                        | AssignedRecdFieldSetter(rfinfo) ->
                            let calledArgTy = rfinfo.FieldType
                            rfinfo.Name, calledArgTy
            
                    do! subsumeArg (CalledArg((-1, 0), false, NotOptional, NoCallerInfo, false, false, Some (mkSynId m name), ReflectedArgInfo.None, calledArgTy)) caller
                // - Always take the return type into account for
                //      -- op_Explicit, op_Implicit
                //      -- methods using tupling of unfilled out args
                // - Never take into account return type information for constructors 
                match reqdRetTyOpt with
                | Some _  when (minfo.IsConstructor || not alwaysCheckReturn && isNil unnamedCalledOutArgs) -> ()
                | Some reqdRetTy ->
                    let methodRetTy = calledMeth.CalledReturnTypeAfterOutArgTupling
                    return! unifyTypes reqdRetTy methodRetTy
                | _ -> ()
        }

// Assert a subtype constraint, and wrap an ErrorsFromAddingSubsumptionConstraint error around any failure 
// to allow us to report the outer types involved in the constraint 
//
// ty1: expected
// ty2: actual
//
// "ty2 casts to ty1"
// "a value of type ty2 can be used where a value of type ty1 is expected"
and private SolveTypeSubsumesTypeWithWrappedContextualReport (csenv: ConstraintSolverEnv) ndeep m trace cxsln ty1 ty2 wrapper =
    TryD_IgnoreAbortForFailedOverloadResolution
        (fun () -> SolveTypeSubsumesTypeKeepAbbrevs csenv ndeep m trace cxsln ty1 ty2)
        (fun res ->
            match csenv.eContextInfo with
            | ContextInfo.RuntimeTypeTest isOperator ->
                // test if we can cast other way around
                match CollectThenUndo (fun newTrace -> SolveTypeSubsumesTypeKeepAbbrevs csenv ndeep m (OptionalTrace.WithTrace newTrace) cxsln ty2 ty1) with 
                | OkResult _ -> ErrorD (wrapper (ErrorsFromAddingSubsumptionConstraint(csenv.g, csenv.DisplayEnv, ty1, ty2, res, ContextInfo.DowncastUsedInsteadOfUpcast isOperator, m)))
                | _ -> ErrorD (wrapper (ErrorsFromAddingSubsumptionConstraint(csenv.g, csenv.DisplayEnv, ty1, ty2, res, ContextInfo.NoContext, m)))
            | _ -> ErrorD (wrapper (ErrorsFromAddingSubsumptionConstraint(csenv.g, csenv.DisplayEnv, ty1, ty2, res, csenv.eContextInfo, m))))

and private SolveTypeSubsumesTypeWithReport (csenv: ConstraintSolverEnv) ndeep m trace cxsln ty1 ty2 =
        SolveTypeSubsumesTypeWithWrappedContextualReport (csenv) ndeep m trace cxsln ty1 ty2 id
        
// ty1: actual
// ty2: expected
and private SolveTypeEqualsTypeWithReport (csenv: ConstraintSolverEnv) ndeep  m trace cxsln actual expected = 
    TryD_IgnoreAbortForFailedOverloadResolution
        (fun () -> SolveTypeEqualsTypeKeepAbbrevsWithCxsln csenv ndeep m trace cxsln actual expected)
        (fun res -> ErrorD (ErrorFromAddingTypeEquation(csenv.g, csenv.DisplayEnv, actual, expected, res, m)))
  
and ArgsMustSubsumeOrConvert 
        (csenv: ConstraintSolverEnv)
        ndeep
        trace
        cxsln
        isConstraint
        enforceNullableOptionalsKnownTypes // use known types from nullable optional args?
        (calledArg: CalledArg) 
        (callerArg: CallerArg<'T>)  = trackErrors {
        
    let g = csenv.g
    let m = callerArg.Range
    let calledArgTy = AdjustCalledArgType csenv.InfoReader isConstraint enforceNullableOptionalsKnownTypes calledArg callerArg    
    do! SolveTypeSubsumesTypeWithReport csenv ndeep m trace cxsln calledArgTy callerArg.CallerArgumentType
    if calledArg.IsParamArray && isArray1DTy g calledArgTy && not (isArray1DTy g callerArg.CallerArgumentType) then 
        return! ErrorD(Error(FSComp.SR.csMethodExpectsParams(), m))
    else ()
  }

and MustUnify csenv ndeep trace cxsln ty1 ty2 = 
    SolveTypeEqualsTypeWithReport csenv ndeep csenv.m trace cxsln ty1 ty2

and MustUnifyInsideUndo csenv ndeep trace cxsln ty1 ty2 = 
    SolveTypeEqualsTypeWithReport csenv ndeep csenv.m (WithTrace trace) cxsln ty1 ty2

and ArgsMustSubsumeOrConvertInsideUndo (csenv: ConstraintSolverEnv) ndeep trace cxsln isConstraint calledMeth calledArg (CallerArg(callerArgTy, m, _, _) as callerArg) = 
    let calledArgTy = AdjustCalledArgType csenv.InfoReader isConstraint true calledArg callerArg
    SolveTypeSubsumesTypeWithWrappedContextualReport csenv ndeep  m (WithTrace trace) cxsln calledArgTy callerArgTy (fun e -> ArgDoesNotMatchError(e :?> _, calledMeth, calledArg, callerArg))  

and TypesMustSubsumeOrConvertInsideUndo (csenv: ConstraintSolverEnv) ndeep trace cxsln m calledArgTy callerArgTy = 
    SolveTypeSubsumesTypeWithReport csenv ndeep m trace cxsln calledArgTy callerArgTy 

and ArgsEquivInsideUndo (csenv: ConstraintSolverEnv) isConstraint calledArg (CallerArg(callerArgTy, m, _, _) as callerArg) = 
    let calledArgTy = AdjustCalledArgType csenv.InfoReader isConstraint true calledArg callerArg
    if typeEquiv csenv.g calledArgTy callerArgTy then CompleteD else ErrorD(Error(FSComp.SR.csArgumentTypesDoNotMatch(), m))

and ReportNoCandidatesError (csenv: ConstraintSolverEnv) (nUnnamedCallerArgs, nNamedCallerArgs) methodName ad (calledMethGroup: CalledMeth<_> list) isSequential =

    let amap = csenv.amap
    let m    = csenv.m
    let denv = csenv.DisplayEnv
    let infoReader = csenv.InfoReader

    match (calledMethGroup |> List.partition (CalledMeth.GetMethod >> IsMethInfoAccessible amap m ad)), 
          (calledMethGroup |> List.partition (fun cmeth -> cmeth.HasCorrectObjArgs(m))), 
          (calledMethGroup |> List.partition (fun cmeth -> cmeth.HasCorrectArity)), 
          (calledMethGroup |> List.partition (fun cmeth -> cmeth.HasCorrectGenericArity)), 
          (calledMethGroup |> List.partition (fun cmeth -> cmeth.AssignsAllNamedArgs)) with

    // No version accessible 
    | ([], others), _, _, _, _ ->  
        if isNil others then
            Error (FSComp.SR.csMemberIsNotAccessible(methodName, (ShowAccessDomain ad)), m)
        else
            Error (FSComp.SR.csMemberIsNotAccessible2(methodName, (ShowAccessDomain ad)), m)
    | _, ([], (cmeth :: _)), _, _, _ ->  
    
        // Check all the argument types.
        if cmeth.CalledObjArgTys(m).Length <> 0 then
            Error (FSComp.SR.csMethodIsNotAStaticMethod(methodName), m)
        else
            Error (FSComp.SR.csMethodIsNotAnInstanceMethod(methodName), m)

    // One method, incorrect name/arg assignment 
    | _, _, _, _, ([], [cmeth]) -> 
        let minfo = cmeth.Method
        let msgNum, msgText = FSComp.SR.csRequiredSignatureIs(NicePrint.stringOfMethInfo infoReader m denv minfo)
        match cmeth.UnassignedNamedArgs with 
        | CallerNamedArg(id, _) :: _ -> 
            if minfo.IsConstructor then
                let suggestFields (addToBuffer: string -> unit) =
                    for p in minfo.DeclaringTyconRef.AllInstanceFieldsAsList do
                        addToBuffer(p.Name.Replace("@", ""))

                ErrorWithSuggestions((msgNum, FSComp.SR.csCtorHasNoArgumentOrReturnProperty(methodName, id.idText, msgText)), id.idRange, id.idText, suggestFields)
            else
                Error((msgNum, FSComp.SR.csMemberHasNoArgumentOrReturnProperty(methodName, id.idText, msgText)), id.idRange)
        | [] -> Error((msgNum, msgText), m)

    // One method, incorrect number of arguments provided by the user
    | _, _, ([], [cmeth]), _, _ when not cmeth.HasCorrectArity ->  
        let minfo = cmeth.Method
        let nReqd = cmeth.TotalNumUnnamedCalledArgs
        let nActual = cmeth.TotalNumUnnamedCallerArgs
        let signature = NicePrint.stringOfMethInfo infoReader m denv minfo
        if nActual = nReqd then 
            let nreqdTyArgs = cmeth.NumCalledTyArgs
            let nactualTyArgs = cmeth.NumCallerTyArgs
            Error (FSComp.SR.csMemberSignatureMismatchArityType(methodName, nreqdTyArgs, nactualTyArgs, signature), m)
        else
            let nReqdNamed = cmeth.TotalNumAssignedNamedArgs

            if nReqdNamed = 0 && cmeth.NumAssignedProps = 0 then
                if minfo.IsConstructor then
                    let couldBeNameArgs =
                        cmeth.ArgSets
                        |> List.exists (fun argSet ->
                            argSet.UnnamedCallerArgs 
                            |> List.exists (fun c -> isSequential c.Expr))

                    if couldBeNameArgs then
                        Error (FSComp.SR.csCtorSignatureMismatchArityProp(methodName, nReqd, nActual, signature), m)
                    else
                        Error (FSComp.SR.csCtorSignatureMismatchArity(methodName, nReqd, nActual, signature), m)
                else
                    Error (FSComp.SR.csMemberSignatureMismatchArity(methodName, nReqd, nActual, signature), m)
            else
                if nReqd > nActual then
                    let diff = nReqd - nActual
                    let missingArgs = List.skip nReqd cmeth.AllUnnamedCalledArgs
                    match NamesOfCalledArgs missingArgs with 
                    | [] ->
                        if nActual = 0 then 
                            Error (FSComp.SR.csMemberSignatureMismatch(methodName, diff, signature), m)
                        else 
                            Error (FSComp.SR.csMemberSignatureMismatch2(methodName, diff, signature), m)
                    | names -> 
                        let str = String.concat ";" (pathOfLid names)
                        if nActual = 0 then 
                            Error (FSComp.SR.csMemberSignatureMismatch3(methodName, diff, signature, str), m)
                        else 
                            Error (FSComp.SR.csMemberSignatureMismatch4(methodName, diff, signature, str), m)
                else 
                    Error (FSComp.SR.csMemberSignatureMismatchArityNamed(methodName, (nReqd+nReqdNamed), nActual, nReqdNamed, signature), m)

    // One or more accessible, all the same arity, none correct 
    | ((cmeth :: cmeths2), _), _, _, _, _ when not cmeth.HasCorrectArity && cmeths2 |> List.forall (fun cmeth2 -> cmeth.TotalNumUnnamedCalledArgs = cmeth2.TotalNumUnnamedCalledArgs) -> 
        Error (FSComp.SR.csMemberNotAccessible(methodName, nUnnamedCallerArgs, methodName, cmeth.TotalNumUnnamedCalledArgs), m)
    // Many methods, all with incorrect number of generic arguments
    | _, _, _, ([], (cmeth :: _)), _ -> 
        let msg = FSComp.SR.csIncorrectGenericInstantiation((ShowAccessDomain ad), methodName, cmeth.NumCallerTyArgs)
        Error (msg, m)
    // Many methods of different arities, all incorrect 
    | _, _, ([], (cmeth :: _)), _, _ -> 
        let minfo = cmeth.Method
        Error (FSComp.SR.csMemberOverloadArityMismatch(methodName, cmeth.TotalNumUnnamedCallerArgs, (List.sum minfo.NumArgs)), m)
    | _ -> 
        let msg = 
            if nNamedCallerArgs = 0 then 
                FSComp.SR.csNoMemberTakesTheseArguments((ShowAccessDomain ad), methodName, nUnnamedCallerArgs)
            else 
                let s = calledMethGroup |> List.map (fun cmeth -> cmeth.UnassignedNamedArgs |> List.map (fun na -> na.Name)|> Set.ofList) |> Set.intersectMany
                if s.IsEmpty then 
                    FSComp.SR.csNoMemberTakesTheseArguments2((ShowAccessDomain ad), methodName, nUnnamedCallerArgs, nNamedCallerArgs)
                else 
                    let sample = s.MinimumElement
                    FSComp.SR.csNoMemberTakesTheseArguments3((ShowAccessDomain ad), methodName, nUnnamedCallerArgs, sample)
        Error (msg, m)
    |> ErrorD

and ReportNoCandidatesErrorExpr csenv callerArgCounts methodName ad calledMethGroup =
    let isSequential e = match e with | Expr.Sequential (_, _, _, _, _) -> true | _ -> false
    ReportNoCandidatesError csenv callerArgCounts methodName ad calledMethGroup isSequential

and ReportNoCandidatesErrorSynExpr csenv callerArgCounts methodName ad calledMethGroup =
    let isSequential e = match e with | SynExpr.Sequential (_, _, _, _, _) -> true | _ -> false
    ReportNoCandidatesError csenv callerArgCounts methodName ad calledMethGroup isSequential

// Resolve the overloading of a method 
// This is used after analyzing the types of arguments 
and ResolveOverloading 
         (csenv: ConstraintSolverEnv) 
         trace           // The undo trace, if any
         methodName      // The name of the method being called, for error reporting
         ndeep           // Depth of inference
         cx              // We're doing overload resolution as part of constraint solving, where special rules apply for op_Explicit and op_Implicit constraints.
         (callerArgs: CallerArgs<Expr>)
         ad              // The access domain of the caller, e.g. a module, type etc. 
         calledMethGroup // The set of methods being called 
         permitOptArgs   // Can we supply optional arguments?
         reqdRetTyOpt    // The expected return type, if known 
     =
    let g = csenv.g
    let infoReader = csenv.InfoReader
    let m    = csenv.m
    let denv = csenv.DisplayEnv
    let isOpConversion = methodName = "op_Explicit" || methodName = "op_Implicit"
    // See what candidates we have based on name and arity 
    let candidates = calledMethGroup |> List.filter (fun cmeth -> cmeth.IsCandidate(m, ad))
    let calledMethOpt, errors, calledMethTrace = 

        match calledMethGroup, candidates with 
        | _, [calledMeth] when not isOpConversion -> 
            Some calledMeth, CompleteD, NoTrace

        | [], _ when not isOpConversion -> 
            None, ErrorD (Error (FSComp.SR.csMethodNotFound(methodName), m)), NoTrace

        | _, [] when not isOpConversion -> 
            None, ReportNoCandidatesErrorExpr csenv callerArgs.CallerArgCounts methodName ad calledMethGroup, NoTrace
            
        | _, _ -> 

          // - Always take the return type into account for
          //      -- op_Explicit, op_Implicit
          //      -- candidate method sets that potentially use tupling of unfilled out args or overloading on return type
          let alwaysCheckReturn =
              isOpConversion ||
              candidates |> List.exists (fun cmeth -> 
                 cmeth.HasOutArgs ||
                 (g.langVersion.SupportsFeature LanguageFeature.ExtensionConstraintSolutions &&
                  AttributeChecking.MethInfoHasAttribute g m g.attrib_AllowOverloadByReturnTypeAttribute cmeth.Method))

          // Exact match rule.
          //
          // See what candidates we have based on current inferred type information 
          // and _exact_ matches of argument types. 
          match candidates |> FilterEachThenUndo (fun newTrace calledMeth -> 
                     let cxsln = cx |> Option.map (fun traitInfo -> (traitInfo, MemberConstraintSolutionOfMethInfo csenv.SolverState m traitInfo.TraitContext calledMeth.Method calledMeth.CalledTyArgs)) 
                     CanMemberSigsMatchUpToCheck 
                         csenv 
                         permitOptArgs 
                         alwaysCheckReturn
                         (MustUnifyInsideUndo csenv ndeep newTrace cxsln) 
                         (TypesMustSubsumeOrConvertInsideUndo csenv ndeep (WithTrace newTrace) cxsln m)
                         (ArgsEquivInsideUndo csenv cx.IsSome) 
                         reqdRetTyOpt 
                         calledMeth) with
          | [(calledMeth, warns, _)] ->
              Some calledMeth, OkResult (warns, ()), NoTrace // Can't re-play the trace since ArgsEquivInsideUndo was used

          | _ -> 
            // Now determine the applicable methods.
            // Subsumption on arguments is allowed.
            let applicable = candidates |> FilterEachThenUndo (fun newTrace candidate -> 
                               let cxsln = cx |> Option.map (fun traitInfo -> (traitInfo, MemberConstraintSolutionOfMethInfo csenv.SolverState m traitInfo.TraitContext candidate.Method candidate.CalledTyArgs))
                               CanMemberSigsMatchUpToCheck 
                                   csenv 
                                   permitOptArgs
                                   alwaysCheckReturn
                                   (MustUnifyInsideUndo csenv ndeep newTrace cxsln) 
                                   (TypesMustSubsumeOrConvertInsideUndo csenv ndeep (WithTrace newTrace) cxsln m)
                                   (ArgsMustSubsumeOrConvertInsideUndo csenv ndeep newTrace cxsln cx.IsSome candidate) 
                                   reqdRetTyOpt 
                                   candidate)

            let failOverloading overloadResolutionFailure = 
                // Try to extract information to give better error for ambiguous op_Explicit and op_Implicit 
                let convOpData = 
                    if isOpConversion then 
                        match calledMethGroup, reqdRetTyOpt with 
                        | h :: _, Some rty -> 
                            Some (h.Method.ApparentEnclosingType, rty)
                        | _ -> None 
                    else
                        None

                match convOpData with 
                | Some (fromTy, toTy) -> 
                    UnresolvedConversionOperator (denv, fromTy, toTy, m)
                | None -> 
                    // Otherwise pass the overload resolution failure for error printing in CompileOps
                    UnresolvedOverloading (denv, callerArgs, overloadResolutionFailure, m)

            match applicable with 
            | [] ->
                // OK, we failed. Collect up the errors from overload resolution and the possible overloads
                let errors = 
                    candidates 
                    |> List.choose (fun calledMeth -> 
                            match CollectThenUndo (fun newTrace -> 
                                         let cxsln = cx |> Option.map (fun traitInfo -> (traitInfo, MemberConstraintSolutionOfMethInfo csenv.SolverState m traitInfo.TraitContext calledMeth.Method calledMeth.CalledTyArgs))
                                         CanMemberSigsMatchUpToCheck 
                                             csenv 
                                             permitOptArgs
                                             alwaysCheckReturn
                                             (MustUnifyInsideUndo csenv ndeep newTrace cxsln) 
                                             (TypesMustSubsumeOrConvertInsideUndo csenv ndeep (WithTrace newTrace) cxsln m)
                                             (ArgsMustSubsumeOrConvertInsideUndo csenv ndeep newTrace cxsln cx.IsSome calledMeth) 
                                             reqdRetTyOpt 
                                             calledMeth) with 
                            | OkResult _ -> None
                            | ErrorResult(_warnings, exn) ->
                                Some {methodSlot = calledMeth; infoReader = infoReader; error = exn })

                None, ErrorD (failOverloading (NoOverloadsFound (methodName, errors, cx))), NoTrace

            | [(calledMeth, warns, t)] ->
                Some calledMeth, OkResult (warns, ()), WithTrace t

            | applicableMeths -> 
                
                /// Compare two things by the given predicate. 
                /// If the predicate returns true for x1 and false for x2, then x1 > x2
                /// If the predicate returns false for x1 and true for x2, then x1 < x2
                /// Otherwise x1 = x2
                
                // Note: Relies on 'compare' respecting true > false
                let compareCond (p: 'T -> 'T -> bool) x1 x2 = 
                    compare (p x1 x2) (p x2 x1)

                /// Compare types under the feasibly-subsumes ordering
                let compareTypes ty1 ty2 = 
                    (ty1, ty2) ||> compareCond (fun x1 x2 -> TypeFeasiblySubsumesType ndeep csenv.g csenv.amap m x2 CanCoerce x1) 
                    
                /// Compare arguments under the feasibly-subsumes ordering and the adhoc Func-is-better-than-other-delegates rule
                let compareArg (calledArg1: CalledArg) (calledArg2: CalledArg) =
                    let c = compareTypes calledArg1.CalledArgumentType calledArg2.CalledArgumentType
                    if c <> 0 then c else

                    let c = 
                        (calledArg1.CalledArgumentType, calledArg2.CalledArgumentType) ||> compareCond (fun ty1 ty2 -> 

                            // Func<_> is always considered better than any other delegate type
                            match tryTcrefOfAppTy csenv.g ty1 with 
                            | ValueSome tcref1 when 
                                tcref1.DisplayName = "Func" &&  
                                (match tcref1.PublicPath with Some p -> p.EnclosingPath = [| "System" |] | _ -> false) && 
                                isDelegateTy g ty1 &&
                                isDelegateTy g ty2 -> true

                            // T is always better than inref<T>
                            | _ when isInByrefTy csenv.g ty2 && typeEquiv csenv.g ty1 (destByrefTy csenv.g ty2) -> 
                                true

                            // T is always better than Nullable<T> from F# 5.0 onwards
                            | _ when g.langVersion.SupportsFeature(Features.LanguageFeature.NullableOptionalInterop) &&
                                     isNullableTy csenv.g ty2 &&
                                     typeEquiv csenv.g ty1 (destNullableTy csenv.g ty2) -> 
                                true

                            | _ -> false)
                                 
                    if c <> 0 then c else
                    0

                /// Check whether one overload is better than another
                let better (candidate: CalledMeth<_>, candidateWarnings, _) (other: CalledMeth<_>, otherWarnings, _) =
                    let candidateWarnCount = List.length candidateWarnings
                    let otherWarnCount = List.length otherWarnings
                    // Prefer methods that don't give "this code is less generic" warnings
                    // Note: Relies on 'compare' respecting true > false
                    let c = compare (candidateWarnCount = 0) (otherWarnCount = 0)
                    if c <> 0 then c else
                    
                    // Prefer methods that don't use param array arg
                    // Note: Relies on 'compare' respecting true > false
                    let c =  compare (not candidate.UsesParamArrayConversion) (not other.UsesParamArrayConversion) 
                    if c <> 0 then c else

                    // Prefer methods with more precise param array arg type
                    let c = 
                        if candidate.UsesParamArrayConversion && other.UsesParamArrayConversion then
                            compareTypes (candidate.GetParamArrayElementType()) (other.GetParamArrayElementType())
                        else
                            0
                    if c <> 0 then c else
                    
                    // Prefer methods that don't use out args
                    // Note: Relies on 'compare' respecting true > false
                    let c = compare (not candidate.HasOutArgs) (not other.HasOutArgs)
                    if c <> 0 then c else

                    // Prefer methods that don't use optional args
                    // Note: Relies on 'compare' respecting true > false
                    let c = compare (not candidate.HasOptArgs) (not other.HasOptArgs)
                    if c <> 0 then c else

                    // check regular unnamed args. The argument counts will only be different if one is using param args
                    let c = 
                        if candidate.TotalNumUnnamedCalledArgs = other.TotalNumUnnamedCalledArgs then
                           // For extension members, we also include the object argument type, if any in the comparison set
                           // This matches C#, where all extension members are treated and resolved as "static" methods calls
                           let cs = 
                               (if candidate.Method.IsExtensionMember && other.Method.IsExtensionMember then 
                                   let objArgTys1 = candidate.CalledObjArgTys(m) 
                                   let objArgTys2 = other.CalledObjArgTys(m) 
                                   if objArgTys1.Length = objArgTys2.Length then 
                                       List.map2 compareTypes objArgTys1 objArgTys2
                                   else
                                       []
                                else 
                                    []) @
                               ((candidate.AllUnnamedCalledArgs, other.AllUnnamedCalledArgs) ||> List.map2 compareArg) 
                           // "all args are at least as good, and one argument is actually better"
                           if cs |> List.forall (fun x -> x >= 0) && cs |> List.exists (fun x -> x > 0) then 
                               1
                           // "all args are at least as bad, and one argument is actually worse"
                           elif cs |> List.forall (fun x -> x <= 0) && cs |> List.exists (fun x -> x < 0) then 
                               -1
                           // "argument lists are incomparable"
                           else
                               0
                        else
                            0
                    if c <> 0 then c else

                    // prefer non-extension methods 
                    let c = compare (not candidate.Method.IsExtensionMember) (not other.Method.IsExtensionMember)
                    if c <> 0 then c else

                    // between extension methods, prefer most recently opened
                    let c = 
                        if candidate.Method.IsExtensionMember && other.Method.IsExtensionMember then 
                            compare candidate.Method.ExtensionMemberPriority other.Method.ExtensionMemberPriority 
                        else 
                            0
                    if c <> 0 then c else

                    // Prefer non-generic methods 
                    // Note: Relies on 'compare' respecting true > false
                    let c = compare candidate.CalledTyArgs.IsEmpty other.CalledTyArgs.IsEmpty
                    if c <> 0 then c else
                    
                    // F# 5.0 rule - prior to F# 5.0 named arguments (on the caller side) were not being taken 
                    // into account when comparing overloads.  So adding a name to an argument might mean 
                    // overloads ould no longer be distinguished.  We thus look at *all* arguments (whether
                    // optional or not) as an additional comparison technique.
                    let c = 
                        if g.langVersion.SupportsFeature(Features.LanguageFeature.NullableOptionalInterop) then
                            let cs = 
                                let args1 = candidate.AllCalledArgs |> List.concat
                                let args2 = other.AllCalledArgs |> List.concat
                                if args1.Length = args2.Length then 
                                    (args1, args2) ||> List.map2 compareArg
                                else
                                    []
                            // "all args are at least as good, and one argument is actually better"
                            if cs |> List.forall (fun x -> x >= 0) && cs |> List.exists (fun x -> x > 0) then 
                                1
                            // "all args are at least as bad, and one argument is actually worse"
                            elif cs |> List.forall (fun x -> x <= 0) && cs |> List.exists (fun x -> x < 0) then 
                                -1
                            // "argument lists are incomparable"
                            else
                                0
                        else
                            0
                    if c <> 0 then c else

                    0
                    

                let bestMethods =
                    let indexedApplicableMeths = applicableMeths |> List.indexed
                    indexedApplicableMeths |> List.choose (fun (i, candidate) -> 
                        if indexedApplicableMeths |> List.forall (fun (j, other) -> 
                             i = j ||
                             let res = better candidate other
                             //eprintfn "\n-------\nCandidate: %s\nOther: %s\nResult: %d\n" (NicePrint.stringOfMethInfo amap m denv (fst candidate).Method) (NicePrint.stringOfMethInfo amap m denv (fst other).Method) res
                             res > 0) then 
                           Some candidate
                        else 
                           None) 
                match bestMethods with 
                | [(calledMeth, warns, t)] -> Some calledMeth, OkResult (warns, ()), WithTrace t
                | bestMethods -> 
                    let methods = 
                        let getMethodSlotsAndErrors methodSlot errors =
                            [ match errors with
                              | [] -> yield { methodSlot = methodSlot; error = Unchecked.defaultof<exn>; infoReader = infoReader }
                              | errors -> for error in errors do yield { methodSlot = methodSlot; error = error; infoReader = infoReader } ]


                        // use the most precise set
                        // - if after filtering bestMethods still contains something - use it
                        // - otherwise use applicableMeths or initial set of candidate methods
                        [ match bestMethods with
                          | [] -> 
                              match applicableMeths with
                              | [] -> for methodSlot in candidates do yield getMethodSlotsAndErrors methodSlot []
                              | m -> for (methodSlot, errors, _) in m do yield getMethodSlotsAndErrors methodSlot errors
                          | m -> for (methodSlot, errors, _) in m do yield getMethodSlotsAndErrors methodSlot errors ]

                    let methods = List.concat methods

                    None, ErrorD (failOverloading (PossibleCandidates(methodName, methods,cx))), NoTrace

    // If we've got a candidate solution: make the final checks - no undo here! 
    // Allow subsumption on arguments. Include the return type.
    // Unify return types.
    match calledMethOpt with 
    | Some calledMeth ->
    
        // Static IL interfaces methods are not supported in lower F# versions.
        if calledMeth.Method.IsILMethod && not calledMeth.Method.IsInstance && isInterfaceTy g calledMeth.Method.ApparentEnclosingType then
            checkLanguageFeatureRuntimeErrorRecover csenv.InfoReader LanguageFeature.DefaultInterfaceMemberConsumption m
            checkLanguageFeatureErrorRecover g.langVersion LanguageFeature.DefaultInterfaceMemberConsumption m

        calledMethOpt, 
        trackErrors {
                        do! errors
                        let cxsln = cx |> Option.map (fun traitInfo -> (traitInfo, MemberConstraintSolutionOfMethInfo csenv.SolverState m traitInfo.TraitContext calledMeth.Method calledMeth.CalledTyArgs))
                        match calledMethTrace with
                        | NoTrace ->
                           return!
                            // No trace available for CanMemberSigsMatchUpToCheck with ArgsMustSubsumeOrConvert
                            CanMemberSigsMatchUpToCheck 
                                 csenv 
                                 permitOptArgs
                                 true
                                 (MustUnify csenv ndeep trace cxsln) 
                                 (TypesMustSubsumeOrConvertInsideUndo csenv ndeep trace cxsln m)// REVIEW: this should not be an "InsideUndo" operation
                                 (ArgsMustSubsumeOrConvert csenv ndeep trace cxsln cx.IsSome true) 
                                 reqdRetTyOpt 
                                 calledMeth
                        | WithTrace calledMethTrc ->

                            // Re-play existing trace
                            trace.AddFromReplay calledMethTrc

                            // Unify return type
                            match reqdRetTyOpt with 
                            | None -> () 
                            | Some _  when calledMeth.Method.IsConstructor -> ()
                            | Some reqdRetTy ->
                                let actualRetTy = calledMeth.CalledReturnTypeAfterOutArgTupling
                                if isByrefTy g reqdRetTy then 
                                    return! ErrorD(Error(FSComp.SR.tcByrefReturnImplicitlyDereferenced(), m))
                                else
                                    return! MustUnify csenv ndeep trace cxsln reqdRetTy actualRetTy
        }

    | None -> 
        None, errors        

let ResolveOverloadingForCall denv css m  methodName ndeep cx callerArgs ad calledMethGroup permitOptArgs reqdRetTyOpt =
    let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
    ResolveOverloading csenv NoTrace methodName ndeep cx callerArgs ad calledMethGroup permitOptArgs reqdRetTyOpt

/// This is used before analyzing the types of arguments in a single overload resolution
let UnifyUniqueOverloading 
         denv
         css 
         m 
         callerArgCounts 
         methodName 
         ad 
         (calledMethGroup: CalledMeth<SynExpr> list) 
         reqdRetTy    // The expected return type, if known 
   =
    let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
    let m = csenv.m
    // See what candidates we have based on name and arity 
    let candidates = calledMethGroup |> List.filter (fun cmeth -> cmeth.IsCandidate(m, ad)) 
    let ndeep = 0
    match calledMethGroup, candidates with 
    | _, [calledMeth] ->  trackErrors {
      do! 
        // Only one candidate found - we thus know the types we expect of arguments 
        CanMemberSigsMatchUpToCheck 
            csenv 
            true // permitOptArgs
            true // always check return type
            (MustUnify csenv ndeep NoTrace None) 
            (TypesMustSubsumeOrConvertInsideUndo csenv ndeep NoTrace None m)
            (ArgsMustSubsumeOrConvert csenv ndeep NoTrace None false false) // UnifyUniqueOverloading is not called in case of trait call - pass isConstraint=false 
            (Some reqdRetTy)
            calledMeth
      return true
     }
        
    | [], _ -> 
        ErrorD (Error (FSComp.SR.csMethodNotFound(methodName), m))
    | _, [] -> trackErrors {
        do! ReportNoCandidatesErrorSynExpr csenv callerArgCounts methodName ad calledMethGroup 
        return false
      }
    | _ -> 
        ResultD false

/// Remove the global constraints where these type variables appear in the support of the constraint 
let EliminateConstraintsForGeneralizedTypars denv css m (trace: OptionalTrace) (generalizedTypars: Typars) =
    let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
    for tp in generalizedTypars do
        let tpn = tp.Stamp
        let cxst = csenv.SolverState.ExtraCxs
        let cxs = cxst.FindAll tpn
        for cx in cxs do 
            trace.Exec
                (fun () -> cxst.Remove tpn)
                (fun () -> (csenv.SolverState.ExtraCxs.Add (tpn, cx)))


//-------------------------------------------------------------------------
// Main entry points to constraint solver (some backdoors are used for 
// some constructs)
//
// No error recovery here: we do that on a per-expression basis.
//------------------------------------------------------------------------- 

let AddCxTypeEqualsType contextInfo denv css m actual expected  = 
    let csenv = MakeConstraintSolverEnv contextInfo css m denv
    SolveTypeEqualsTypeWithReport csenv 0 m NoTrace None actual expected
    |> RaiseOperationResult

let UndoIfFailed f =
    let trace = Trace.New()
    let res = 
        try 
            f trace 
            |> CheckNoErrorsAndGetWarnings
        with e -> None
    match res with 
    | None -> 
        // Don't report warnings if we failed
        trace.Undo()
        false
    | Some warns -> 
        // Report warnings if we succeeded
        ReportWarnings warns
        true

let UndoIfFailedOrWarnings f =
    let trace = Trace.New()
    let res = 
        try 
            f trace 
            |> CheckNoErrorsAndGetWarnings
        with e -> None
    match res with 
    | Some [] -> 
        true
    | _ -> 
        trace.Undo()
        false

let AddCxTypeEqualsTypeUndoIfFailed denv css m ty1 ty2 =
    UndoIfFailed (fun trace -> 
     let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
     SolveTypeEqualsTypeKeepAbbrevs csenv 0 m (WithTrace trace) ty1 ty2)

let AddCxTypeEqualsTypeUndoIfFailedOrWarnings denv css m ty1 ty2 =
    UndoIfFailedOrWarnings (fun trace -> 
        let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
        SolveTypeEqualsTypeKeepAbbrevs csenv 0 m (WithTrace trace) ty1 ty2)

let AddCxTypeEqualsTypeMatchingOnlyUndoIfFailed denv css m ty1 ty2 =
    let csenv = { MakeConstraintSolverEnv ContextInfo.NoContext css m denv with MatchingOnly = true }
    UndoIfFailed (fun trace -> SolveTypeEqualsTypeKeepAbbrevs csenv 0 m (WithTrace trace) ty1 ty2)

let AddCxTypeMustSubsumeTypeUndoIfFailed denv css m ty1 ty2 = 
    UndoIfFailed (fun trace ->
        let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
        SolveTypeSubsumesTypeKeepAbbrevs csenv 0 m (WithTrace trace) None ty1 ty2)

let AddCxTypeMustSubsumeTypeMatchingOnlyUndoIfFailed denv css m ty1 ty2 = 
    let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
    let csenv = { csenv with MatchingOnly = true }
    UndoIfFailed (fun trace -> SolveTypeSubsumesTypeKeepAbbrevs csenv 0 m (WithTrace trace) None ty1 ty2)

let AddCxTypeMustSubsumeType contextInfo denv css m trace ty1 ty2 = 
    let csenv = MakeConstraintSolverEnv contextInfo css m denv
    SolveTypeSubsumesTypeWithReport csenv 0 m trace None ty1 ty2
    |> RaiseOperationResult

let AddCxMethodConstraint denv css m trace traitInfo  =
    let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
    TryD_IgnoreAbortForFailedOverloadResolution
        (fun () ->
            trackErrors {
                do! 
                    SolveMemberConstraint csenv true PermitWeakResolution.No 0 m trace traitInfo
                    |> OperationResult.ignore
            })
        (fun res -> ErrorD (ErrorFromAddingConstraint(denv, res, m)))
    |> RaiseOperationResult

let AddCxTypeMustSupportNull denv css m trace ty =
    let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
    TryD_IgnoreAbortForFailedOverloadResolution
        (fun () -> SolveTypeSupportsNull csenv 0 m trace ty)
        (fun res -> ErrorD (ErrorFromAddingConstraint(denv, res, m)))
    |> RaiseOperationResult

let AddCxTypeMustSupportComparison denv css m trace ty =
    let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
    TryD_IgnoreAbortForFailedOverloadResolution
        (fun () -> SolveTypeSupportsComparison csenv 0 m trace ty)
        (fun res -> ErrorD (ErrorFromAddingConstraint(denv, res, m)))
    |> RaiseOperationResult

let AddCxTypeMustSupportEquality denv css m trace ty =
    let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
    TryD_IgnoreAbortForFailedOverloadResolution
        (fun () -> SolveTypeSupportsEquality csenv 0 m trace ty)
        (fun res -> ErrorD (ErrorFromAddingConstraint(denv, res, m)))
    |> RaiseOperationResult

let AddCxTypeMustSupportDefaultCtor denv css m trace ty =
    let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
    TryD_IgnoreAbortForFailedOverloadResolution
        (fun () -> SolveTypeRequiresDefaultConstructor csenv 0 m trace ty)
        (fun res -> ErrorD (ErrorFromAddingConstraint(denv, res, m)))
    |> RaiseOperationResult

let AddCxTypeIsReferenceType denv css m trace ty =
    let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
    TryD_IgnoreAbortForFailedOverloadResolution
        (fun () -> SolveTypeIsReferenceType csenv 0 m trace ty)
        (fun res -> ErrorD (ErrorFromAddingConstraint(denv, res, m)))
    |> RaiseOperationResult

let AddCxTypeIsValueType denv css m trace ty =
    let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
    TryD_IgnoreAbortForFailedOverloadResolution
        (fun () -> SolveTypeIsNonNullableValueType csenv 0 m trace ty)
        (fun res -> ErrorD (ErrorFromAddingConstraint(denv, res, m)))
    |> RaiseOperationResult
    
let AddCxTypeIsUnmanaged denv css m trace ty =
    let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
    TryD_IgnoreAbortForFailedOverloadResolution
        (fun () -> SolveTypeIsUnmanaged csenv 0 m trace ty)
        (fun res -> ErrorD (ErrorFromAddingConstraint(denv, res, m)))
    |> RaiseOperationResult

let AddCxTypeIsEnum denv css m trace ty underlying =
    let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
    TryD_IgnoreAbortForFailedOverloadResolution
        (fun () -> SolveTypeIsEnum csenv 0 m trace ty underlying)
        (fun res -> ErrorD (ErrorFromAddingConstraint(denv, res, m)))
    |> RaiseOperationResult

let AddCxTypeIsDelegate denv css m trace ty aty bty =
    let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
    TryD_IgnoreAbortForFailedOverloadResolution
        (fun () -> SolveTypeIsDelegate csenv 0 m trace ty aty bty)
        (fun res -> ErrorD (ErrorFromAddingConstraint(denv, res, m)))
    |> RaiseOperationResult

let AddCxTyparDefaultsTo denv css m ctxtInfo tp ridx ty =
    let csenv = MakeConstraintSolverEnv ctxtInfo css m denv
    TryD_IgnoreAbortForFailedOverloadResolution
        (fun () -> AddConstraint csenv 0 m NoTrace tp (TyparConstraint.DefaultsTo(ridx, ty, m)))
        (fun res -> ErrorD (ErrorFromAddingConstraint(denv, res, m)))
    |> RaiseOperationResult

let SolveTypeAsError denv css m ty =
    let ty2 = NewErrorType ()
    assert((destTyparTy css.g ty2).IsFromError)
    let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
    SolveTypeEqualsTypeKeepAbbrevs csenv 0 m NoTrace ty ty2 |> ignore
    
let ApplyTyparDefaultAtPriority denv css priority (tp: Typar) =
    tp.Constraints |> List.iter (fun tpc -> 
        match tpc with 
        | TyparConstraint.DefaultsTo(priority2, ty2, m) when priority2 = priority -> 
            let ty1 = mkTyparTy tp
            if not tp.IsSolved && not (typeEquiv css.g ty1 ty2) then
                let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
                TryD_IgnoreAbortForFailedOverloadResolution
                    (fun () ->
                        SolveTyparEqualsType csenv 0 m NoTrace ty1 ty2)
                    (fun res -> 
                        SolveTypeAsError denv css m ty1
                        ErrorD(ErrorFromApplyingDefault(css.g, denv, tp, ty2, res, m)))
                |> RaiseOperationResult
        | _ -> ())

let CreateCodegenState tcVal g amap = 
    { g = g
      amap = amap
      TcVal = tcVal
      ExtraCxs = HashMultiMap(10, HashIdentity.Structural)
      InfoReader = new InfoReader(g, amap) }

/// For some code like "let f() = ([] = [])", a free choice is made for a type parameter
/// for an interior type variable.  This chooses a solution for a type parameter subject
/// to its constraints and applies that solution by using a constraint.
let ChooseTyparSolutionAndSolve css denv tp =
    let g = css.g
    let amap = css.amap
    let max, m = ChooseTyparSolutionAndRange g amap tp 
    let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
    TryD_IgnoreAbortForFailedOverloadResolution
        (fun () -> SolveTyparEqualsType csenv 0 m NoTrace (mkTyparTy tp) max)
        (fun err -> ErrorD(ErrorFromApplyingDefault(g, denv, tp, max, err, m)))
    |> RaiseOperationResult

let CheckDeclaredTypars denv css m typars1 typars2 = 
    let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
    TryD_IgnoreAbortForFailedOverloadResolution
        (fun () -> 
            CollectThenUndo (fun trace -> 
               SolveTypeEqualsTypeEqns csenv 0 m (WithTrace trace) None 
                   (List.map mkTyparTy typars1) 
                   (List.map mkTyparTy typars2)))
        (fun res ->
            ErrorD (ErrorFromAddingConstraint(denv, res, m)))
    |> RaiseOperationResult

let CanonicalizePartialInferenceProblem css denv m tps isInline =
    // Canonicalize constraints prior to generalization 
    let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
    TryD_IgnoreAbortForFailedOverloadResolution
        (fun () -> CanonicalizeRelevantMemberConstraints csenv 0 NoTrace tps isInline)
        (fun res -> ErrorD (ErrorFromAddingConstraint(denv, res, m))) 
    |> RaiseOperationResult

/// Apply defaults arising from 'default' constraints in FSharp.Core
/// for any unsolved free inference type variables.
/// Defaults get applied before the module signature is checked and before the implementation conditions on virtuals/overrides. 
/// Defaults get applied in priority order. Defaults listed last get priority 0 (lowest), 2nd last priority 1 etc. 
let ApplyDefaultsForUnsolved css denv (unsolved: Typar list) =

    // The priority order comes from the order of declaration of the defaults in FSharp.Core.
    for priority = 10 downto 0 do
        unsolved |> List.iter (fun tp -> 
            if not tp.IsSolved then 
                // Apply the first default. If we're defaulting one type variable to another then 
                // the defaults will be propagated to the new type variable. 
                ApplyTyparDefaultAtPriority denv css priority tp)

    // OK, now apply defaults for any unsolved HeadTypeStaticReq 
    unsolved |> List.iter (fun tp ->     
        if not tp.IsSolved then 
            if (tp.StaticReq <> TyparStaticReq.None) then
                ChooseTyparSolutionAndSolve css denv tp)

/// Choose solutions for any remaining unsolved free inference type variables.
let ChooseSolutionsForUnsolved css denv (unsolved: Typar list) =
    unsolved |> List.iter (fun tp -> 
        if (tp.Rigidity <> TyparRigidity.Rigid) && not tp.IsSolved then 
            ChooseTyparSolutionAndSolve css denv tp)

let ApplyDefaultsAfterWitnessGeneration (g: TcGlobals) amap css denv sln = 
    // The process of generating witnesses can cause free inference type variables to arise from use
    // of generic methods as extension constraint solutions. We eliminate these using the same
    // sequence (ApplyDefault, ChooseSolutions) used at the end of type inference.
    if g.langVersion.SupportsFeature LanguageFeature.ExtensionConstraintSolutions then 
        sln |> Option.iter (fun slnExpr -> 
            // Apply all defaults
            let unsolved = FSharp.Compiler.FindUnsolved.UnsolvedTyparsOfExpr g amap denv slnExpr
            ApplyDefaultsForUnsolved css denv unsolved

            // Search again and choose solutions
            let unsolved2 = FSharp.Compiler.FindUnsolved.UnsolvedTyparsOfExpr g amap denv slnExpr
            ChooseSolutionsForUnsolved css denv unsolved2)

/// Generate a witness expression if none is otherwise available, e.g. in legacy non-witness-passing code
let CodegenWitnessExprForTraitConstraint tcVal g amap m (traitInfo:TraitConstraintInfo) argExprs = trackErrors {
    let css = CreateCodegenState tcVal g amap
    let denv = DisplayEnv.Empty g
    let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
    let! _res = SolveMemberConstraint csenv true PermitWeakResolution.LegacyYesAtCodeGen 0 m NoTrace traitInfo
    let sln = GenWitnessExpr amap g m traitInfo argExprs
    ApplyDefaultsAfterWitnessGeneration g amap css denv sln
    return sln
  }

/// Generate the lambda argument passed for a use of a generic construct that accepts trait witnesses
let CodegenWitnessesForTyparInst tcVal g amap m typars tyargs = trackErrors {
    let css = CreateCodegenState tcVal g amap
    let denv = DisplayEnv.Empty g
    let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
     // TODO: check traitCtxtNone here, feels wrong. We should simply not be re-freshening here during codegen.
     // This is invoked every time codegen or quotation calls a witness-accepting
     // method. TBH the witnesses need to be generated during type checking and passed all the way down,
     // or at least be left in the tree as WitnessArg nodes.
    let ftps, _renaming, tinst = FreshenTypeInst traitCtxtNone m typars
    let traitInfos = GetTraitConstraintInfosOfTypars g ftps 
    do! SolveTyparsEqualTypes csenv 0 m NoTrace tinst tyargs
    let witnessArgs = MethodCalls.GenWitnessArgs amap g m traitInfos
    for witnessArg in witnessArgs do
        match witnessArg with 
        | Choice1Of2 _traitInfo -> ()
        | Choice2Of2 sln -> ApplyDefaultsAfterWitnessGeneration g amap css denv (Some sln)
    return witnessArgs
  }

/// Generate the lambda argument passed for a use of a generic construct that accepts trait witnesses
let CodegenWitnessArgForTraitConstraint tcVal g amap m traitInfo = trackErrors {
    let css = CreateCodegenState tcVal g amap
    let denv = DisplayEnv.Empty g
    let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m denv
    let! _res = SolveMemberConstraint csenv true PermitWeakResolution.LegacyYesAtCodeGen 0 m NoTrace traitInfo
    let witnessLambda = MethodCalls.GenWitnessExprLambda amap g m traitInfo
    match witnessLambda with 
    | Choice1Of2 _traitInfo -> ()
    | Choice2Of2 sln -> ApplyDefaultsAfterWitnessGeneration g amap css denv (Some sln)
    return witnessLambda
  }

/// An approximation used during name resolution for intellisense to eliminate extension members which will not
/// apply to a particular object argument. This is given as the isApplicableMeth argument to the partial name resolution
/// functions in nameres.fs.
let IsApplicableMethApprox g amap m traitCtxt (minfo: MethInfo) availObjTy = 
    // Prepare an instance of a constraint solver
    // If it's an instance method, then try to match the object argument against the required object argument
    if minfo.IsExtensionMember then 
        let css = 
            { g = g
              amap = amap
              TcVal = (fun _ -> failwith "should not be called")
              ExtraCxs = HashMultiMap(10, HashIdentity.Structural)
              InfoReader = new InfoReader(g, amap) }
        let csenv = MakeConstraintSolverEnv ContextInfo.NoContext css m (DisplayEnv.Empty g)
        let minst = FreshenMethInfo traitCtxt m minfo
        match minfo.GetObjArgTypes(amap, m, minst) with
        | [reqdObjTy] -> 
            let reqdObjTy = if isByrefTy g reqdObjTy then destByrefTy g reqdObjTy else reqdObjTy // This is to support byref extension methods.
            TryD (fun () -> SolveTypeSubsumesType csenv 0 m NoTrace None reqdObjTy availObjTy ++ (fun () -> ResultD true))
                 (fun _err -> ResultD false)
            |> CommitOperationResult
        | _ -> true
    else
        true

