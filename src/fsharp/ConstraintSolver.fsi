// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Solves constraints using a mutable constraint-solver state
module internal FSharp.Compiler.ConstraintSolver

open FSharp.Compiler 
open FSharp.Compiler.AccessibilityLogic
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Import
open FSharp.Compiler.Infos
open FSharp.Compiler.InfoReader
open FSharp.Compiler.MethodCalls
open FSharp.Compiler.NameResolution
open FSharp.Compiler.Syntax
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeOps

/// Create a type variable representing the use of a "_" in F# code
val NewAnonTypar: TyparKind * range * TyparRigidity * TyparStaticReq * TyparDynamicReq -> Typar

/// Create an inference type variable 
val NewInferenceType: unit -> TType

/// Create an inference type variable for the kind of a byref pointer
val NewByRefKindInferenceType: TcGlobals -> range -> TType

/// Create an inference type variable representing an error condition when checking an expression
val NewErrorType: unit -> TType

/// Create an inference type variable representing an error condition when checking a measure
val NewErrorMeasure: unit -> Measure

/// Create a list of inference type variables, one for each element in the input list
val NewInferenceTypes: 'a list -> TType list

/// Given a set of formal type parameters and their constraints, make new inference type variables for
/// each and ensure that the constraints on the new type variables are adjusted to refer to these.
val FreshenAndFixupTypars : ITraitContext option -> range -> TyparRigidity -> Typars -> TType list -> Typars -> Typars * TyparInst * TType list

/// Make new type inference variables for the use of a generic construct at a particular location
val FreshenTypeInst : ITraitContext option -> range -> Typars -> Typars * TyparInst * TType list

/// Make new type inference variables for the use of a generic construct at a particular location
val FreshenTypars : ITraitContext option -> range -> Typars -> TType list

/// Make new type inference variables for the use of a method at a particular location
val FreshenMethInfo : ITraitContext option -> range -> MethInfo -> TType list

[<RequireQualifiedAccess>] 
/// Information about the context of a type equation, for better error reporting
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
        infoReader: InfoReader
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

exception ConstraintSolverTupleDiffLengths              of displayEnv: DisplayEnv * TType list * TType list * range * range
exception ConstraintSolverInfiniteTypes                 of displayEnv: DisplayEnv * contextInfo: ContextInfo * TType * TType * range * range
exception ConstraintSolverTypesNotInEqualityRelation    of displayEnv: DisplayEnv * TType * TType * range * range * ContextInfo
exception ConstraintSolverTypesNotInSubsumptionRelation of displayEnv: DisplayEnv * argTy: TType * paramTy: TType * callRange: range * parameterRange: range
exception ConstraintSolverMissingConstraint             of displayEnv: DisplayEnv * Typar * TyparConstraint * range * range
exception ConstraintSolverError                         of string * range * range
exception ConstraintSolverRelatedInformation            of string option * range * exn

exception ErrorFromApplyingDefault              of tcGlobals: TcGlobals * displayEnv: DisplayEnv * Typar * TType * exn * range
exception ErrorFromAddingTypeEquation           of tcGlobals: TcGlobals * displayEnv: DisplayEnv * actualTy: TType * expectedTy: TType * exn * range
exception ErrorsFromAddingSubsumptionConstraint of tcGlobals: TcGlobals * displayEnv: DisplayEnv * actualTy: TType * expectedTy: TType * exn * ContextInfo * parameterRange: range
exception ErrorFromAddingConstraint             of displayEnv: DisplayEnv * exn * range
exception UnresolvedConversionOperator          of displayEnv: DisplayEnv * TType * TType * range
exception UnresolvedOverloading                 of displayEnv: DisplayEnv * callerArgs: CallerArgs<Expr> * failure: OverloadResolutionFailure * range
exception NonRigidTypar                         of displayEnv: DisplayEnv * string option * range * TType * TType * range

exception ArgDoesNotMatchError                  of error: ErrorsFromAddingSubsumptionConstraint * calledMeth: CalledMeth<Expr> * calledArg: CalledArg * callerArg: CallerArg<Expr>
/// A function that denotes captured tcVal, Used in constraint solver and elsewhere to get appropriate expressions for a ValRef.
type TcValF = (ValRef -> ValUseFlag -> TType list -> range -> Expr * TType)

[<Sealed>]
type ConstraintSolverState =
    static member New: TcGlobals * Import.ImportMap * InfoReader * TcValF -> ConstraintSolverState

val BakedInTraitConstraintNames: Set<string>

[<Sealed; NoEquality; NoComparison>]
type Trace 

type OptionalTrace =
    | NoTrace
    | WithTrace of Trace

val SimplifyMeasuresInTypeScheme: TcGlobals -> bool -> Typars -> TType -> TyparConstraint list -> Typars

val ResolveOverloadingForCall: DisplayEnv -> ConstraintSolverState -> range -> methodName: string -> ndeep: int -> cx: TraitConstraintInfo option -> callerArgs: CallerArgs<Expr> -> AccessorDomain -> calledMethGroup: CalledMeth<Expr> list -> permitOptArgs: bool -> reqdRetTyOpt: TType option -> CalledMeth<Expr> option * OperationResult<unit>

val UnifyUniqueOverloading: DisplayEnv -> ConstraintSolverState -> range -> int * int -> string -> AccessorDomain -> CalledMeth<SynExpr> list -> TType -> OperationResult<bool> 

/// Remove the global constraints where these type variables appear in the support of the constraint 
val EliminateConstraintsForGeneralizedTypars: DisplayEnv -> ConstraintSolverState -> range -> OptionalTrace -> Typars -> unit 

val CheckDeclaredTypars: DisplayEnv -> ConstraintSolverState -> range -> Typars -> Typars -> unit 

val AddCxTypeEqualsType: ContextInfo -> DisplayEnv -> ConstraintSolverState -> range -> TType -> TType -> unit

val AddCxTypeEqualsTypeUndoIfFailed: DisplayEnv -> ConstraintSolverState -> range -> TType -> TType -> bool

val AddCxTypeEqualsTypeUndoIfFailedOrWarnings: DisplayEnv -> ConstraintSolverState -> range -> TType -> TType -> bool

val AddCxTypeEqualsTypeMatchingOnlyUndoIfFailed: DisplayEnv -> ConstraintSolverState -> range -> TType -> TType -> bool

val AddCxTypeMustSubsumeType: ContextInfo -> DisplayEnv -> ConstraintSolverState -> range -> OptionalTrace -> TType -> TType -> unit

val AddCxTypeMustSubsumeTypeUndoIfFailed: DisplayEnv -> ConstraintSolverState -> range -> TType -> TType -> bool

val AddCxTypeMustSubsumeTypeMatchingOnlyUndoIfFailed: DisplayEnv -> ConstraintSolverState -> range -> TType -> TType -> bool

val AddCxMethodConstraint: DisplayEnv -> ConstraintSolverState -> range -> OptionalTrace -> TraitConstraintInfo -> unit

val AddCxTypeMustSupportNull: DisplayEnv -> ConstraintSolverState -> range -> OptionalTrace -> TType -> unit

val AddCxTypeMustSupportComparison: DisplayEnv -> ConstraintSolverState -> range -> OptionalTrace -> TType -> unit

val AddCxTypeMustSupportEquality: DisplayEnv -> ConstraintSolverState -> range -> OptionalTrace -> TType -> unit

val AddCxTypeMustSupportDefaultCtor: DisplayEnv -> ConstraintSolverState -> range -> OptionalTrace -> TType -> unit

val AddCxTypeIsReferenceType: DisplayEnv -> ConstraintSolverState -> range -> OptionalTrace -> TType -> unit

val AddCxTypeIsValueType: DisplayEnv -> ConstraintSolverState -> range -> OptionalTrace -> TType -> unit

val AddCxTypeIsUnmanaged: DisplayEnv -> ConstraintSolverState -> range -> OptionalTrace -> TType -> unit

val AddCxTypeIsEnum: DisplayEnv -> ConstraintSolverState -> range -> OptionalTrace -> TType -> TType -> unit

val AddCxTypeIsDelegate: DisplayEnv -> ConstraintSolverState -> range -> OptionalTrace -> TType -> TType -> TType -> unit

val AddCxTyparDefaultsTo:  DisplayEnv -> ConstraintSolverState -> range -> ContextInfo -> Typar -> int -> TType -> unit

val SolveTypeAsError: DisplayEnv -> ConstraintSolverState -> range -> TType -> unit

val ApplyTyparDefaultAtPriority: DisplayEnv -> ConstraintSolverState -> priority: int -> Typar -> unit

/// Generate a witness expression if none is otherwise available, e.g. in legacy non-witness-passing code
val CodegenWitnessExprForTraitConstraint : TcValF -> TcGlobals -> ImportMap -> range -> TraitConstraintInfo -> Expr list -> OperationResult<Expr option>

/// Generate the arguments passed when using a generic construct that accepts traits witnesses
val CodegenWitnessesForTyparInst : TcValF -> TcGlobals -> ImportMap -> range -> Typars -> TType list -> OperationResult<Choice<TraitConstraintInfo, Expr> list>

/// Generate the lambda argument passed for a use of a generic construct that accepts trait witnesses
val CodegenWitnessArgForTraitConstraint : TcValF -> TcGlobals -> ImportMap -> range -> TraitConstraintInfo -> OperationResult<Choice<TraitConstraintInfo, Expr>>

/// For some code like "let f() = ([] = [])", a free choice is made for a type parameter
/// for an interior type variable.  This chooses a solution for a type parameter subject
/// to its constraints and applies that solution by using a constraint.
val ChooseTyparSolutionAndSolve : ConstraintSolverState -> DisplayEnv -> Typar -> unit

/// Apply defaults arising from 'default' constraints in FSharp.Core
/// for any unsolved free inference type variables.
val ApplyDefaultsForUnsolved: ConstraintSolverState -> DisplayEnv -> Typar list -> unit

/// Choose solutions for any remaining unsolved free inference type variables.
val ChooseSolutionsForUnsolved: ConstraintSolverState -> DisplayEnv -> Typar list -> unit

val IsApplicableMethApprox: TcGlobals -> ImportMap -> range -> ITraitContext option -> MethInfo -> TType -> bool

val CanonicalizePartialInferenceProblem:  ConstraintSolverState -> DisplayEnv -> range -> Typars -> bool -> unit
