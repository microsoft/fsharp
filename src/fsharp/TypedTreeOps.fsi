// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Defines derived expression manipulation and construction functions.
module internal FSharp.Compiler.TypedTreeOps 

open System.Collections.Generic
open System.Collections.Immutable
open Internal.Utilities.Collections
open Internal.Utilities.Rational
open FSharp.Compiler.AbstractIL 
open FSharp.Compiler.AbstractIL.IL 
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text
open FSharp.Compiler.Xml
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TcGlobals

type Erasure = EraseAll | EraseMeasures | EraseNone

/// Check the equivalence of two types up to an erasure flag
val typeEquivAux: Erasure -> TcGlobals -> TType -> TType -> bool

/// Check the equivalence of two types 
val typeEquiv: TcGlobals -> TType -> TType -> bool

/// Check the equivalence of two units-of-measure
val measureEquiv: TcGlobals -> Measure -> Measure -> bool

/// Get the unit of measure for an annotated type
val getMeasureOfType: TcGlobals -> TType -> (TyconRef * Measure) option

/// Reduce a type to its more canonical form subject to an erasure flag, inference equations and abbreviations
val stripTyEqnsWrtErasure: Erasure -> TcGlobals -> TType -> TType

/// Build a function type
val mkFunTy: TType -> TType -> TType

/// Build a function type
val ( --> ): TType -> TType -> TType

/// Build a type-forall anonymous generic type if necessary
val mkForallTyIfNeeded: Typars -> TType -> TType

val ( +-> ): Typars -> TType -> TType

/// Build a curried function type
val mkIteratedFunTy: TTypes -> TType -> TType

/// Get the natural type of a single argument amongst a set of curried arguments
val typeOfLambdaArg: range -> Val list -> TType

/// Get the curried type corresponding to a lambda 
val mkMultiLambdaTy: range -> Val list -> TType -> TType

/// Get the curried type corresponding to a lambda 
val mkLambdaTy: Typars -> TTypes -> TType -> TType

/// Module publication, used while compiling fslib.
val ensureCcuHasModuleOrNamespaceAtPath: CcuThunk -> Ident list -> CompilationPath -> XmlDoc -> unit 

/// Ignore 'Expr.Link' in an expression
val stripExpr: Expr -> Expr

/// Get the values for a set of bindings
val valsOfBinds: Bindings -> Vals 

/// Look for a use of an F# value, possibly including application of a generic thing to a set of type arguments
val (|ExprValWithPossibleTypeInst|_|): Expr -> (ValRef * ValUseFlag * TType list * range) option

/// Build decision trees imperatively
type MatchBuilder =

    /// Create a new builder
    new: DebugPointAtBinding * range -> MatchBuilder

    /// Add a new destination target
    member AddTarget: DecisionTreeTarget -> int

    /// Add a new destination target that is an expression result
    member AddResultTarget: Expr * DebugPointForTarget -> DecisionTree

    /// Finish the targets
    member CloseTargets: unit -> DecisionTreeTarget list

    /// Build the overall expression
    member Close: DecisionTree * range * TType -> Expr

/// Add an if-then-else boolean conditional node into a decision tree
val mkBoolSwitch: range -> Expr -> DecisionTree -> DecisionTree -> DecisionTree

/// Build a conditional expression
val primMkCond: DebugPointAtBinding -> DebugPointForTarget -> DebugPointForTarget -> range -> TType -> Expr -> Expr -> Expr -> Expr

/// Build a conditional expression
val mkCond: DebugPointAtBinding -> DebugPointForTarget -> range -> TType -> Expr -> Expr -> Expr -> Expr

/// Build a conditional expression that checks for non-nullness
val mkNonNullCond: TcGlobals -> range -> TType -> Expr -> Expr -> Expr -> Expr

/// Build an if-then statement
val mkIfThen: TcGlobals -> range -> Expr -> Expr -> Expr 

/// Build an expression corresponding to the use of a value
/// Note: try to use exprForValRef or the expression returned from mkLocal instead of this. 
val exprForVal: range -> Val -> Expr

/// Build an expression corresponding to the use of a reference to a value
val exprForValRef: range -> ValRef -> Expr

/// Make a new local value and build an expression to reference it
val mkLocal: range -> string -> TType -> Val * Expr

/// Make a new compiler-generated local value and build an expression to reference it
val mkCompGenLocal: range -> string -> TType -> Val * Expr

/// Make a new mutable compiler-generated local value and build an expression to reference it
val mkMutableCompGenLocal: range -> string -> TType -> Val * Expr

/// Make a new mutable compiler-generated local value, 'let' bind it to an expression 
/// 'invisibly' (no sequence point etc.), and build an expression to reference it
val mkCompGenLocalAndInvisibleBind: TcGlobals -> string -> range -> Expr -> Val * Expr * Binding 

/// Build a lambda expression taking multiple values
val mkMultiLambda: range -> Val list -> Expr * TType -> Expr

/// Rebuild a lambda during an expression tree traversal
val rebuildLambda: range -> Val option -> Val option -> Val list -> Expr * TType -> Expr

/// Build a lambda expression taking a single value 
val mkLambda: range -> Val -> Expr * TType -> Expr

/// Build a generic lambda expression (type abstraction)
val mkTypeLambda: range -> Typars -> Expr * TType -> Expr

/// Build an object expression
val mkObjExpr: TType * Val option * Expr * ObjExprMethod list * (TType * ObjExprMethod list) list * range -> Expr

/// Build an type-chose expression, indicating that a local free choice of a type variable
val mkTypeChoose: range -> Typars -> Expr -> Expr

/// Build an iterated (curried) lambda expression
val mkLambdas: range -> Typars -> Val list -> Expr * TType -> Expr

/// Build an iterated (tupled+curried) lambda expression
val mkMultiLambdasCore: range -> Val list list -> Expr * TType -> Expr * TType

/// Build an iterated generic (type abstraction + tupled+curried) lambda expression
val mkMultiLambdas: range -> Typars -> Val list list -> Expr * TType -> Expr

/// Build a lambda expression that corresponds to the implementation of a member
val mkMemberLambdas: range -> Typars -> Val option -> Val option -> Val list list -> Expr * TType -> Expr

/// Build a 'while' loop expression
val mkWhile: TcGlobals -> DebugPointAtWhile * SpecialWhileLoopMarker * Expr * Expr * range -> Expr

/// Build a 'for' loop expression
val mkFor: TcGlobals -> DebugPointAtFor * Val * Expr * ForLoopStyle * Expr * Expr * range -> Expr

/// Build a 'try/with' expression
val mkTryWith: TcGlobals -> Expr * (* filter val *) Val * (* filter expr *) Expr * (* handler val *) Val * (* handler expr *) Expr * range * TType * DebugPointAtTry * DebugPointAtWith -> Expr

/// Build a 'try/finally' expression
val mkTryFinally: TcGlobals -> Expr * Expr * range * TType * DebugPointAtTry * DebugPointAtFinally -> Expr

/// Build a user-level value binding
val mkBind: DebugPointAtBinding -> Val -> Expr -> Binding

/// Build a user-level let-binding
val mkLetBind: range -> Binding -> Expr -> Expr

/// Build a user-level value sequence of let bindings
val mkLetsBind: range -> Binding list -> Expr -> Expr

/// Build a user-level value sequence of let bindings
val mkLetsFromBindings: range -> Bindings -> Expr -> Expr

/// Build a user-level let expression
val mkLet: DebugPointAtBinding -> range -> Val -> Expr -> Expr -> Expr

/// Make a binding that binds a function value to a lambda taking multiple arguments
val mkMultiLambdaBind: Val -> DebugPointAtBinding -> range -> Typars -> Val list list -> Expr * TType -> Binding

// Compiler generated bindings may involve a user variable.
// Compiler generated bindings may give rise to a sequence point if they are part of
// an SPAlways expression. Compiler generated bindings can arise from for example, inlining.
val mkCompGenBind: Val -> Expr -> Binding

/// Make a set of bindings that bind compiler generated values to corresponding expressions.
/// Compiler-generated bindings do not give rise to a sequence point in debugging.
val mkCompGenBinds: Val list -> Exprs -> Bindings

/// Make a let-expression that locally binds a compiler-generated value to an expression.
/// Compiler-generated bindings do not give rise to a sequence point in debugging.
val mkCompGenLet: range -> Val -> Expr -> Expr -> Expr

/// Make a let-expression that locally binds a compiler-generated value to an expression, where the expression
/// is returned by the given continuation. Compiler-generated bindings do not give rise to a sequence point in debugging.
val mkCompGenLetIn: range -> string -> TType -> Expr -> (Val * Expr -> Expr) -> Expr

/// Make a let-expression that locally binds a value to an expression in an "invisible" way.
/// Invisible bindings are not given a sequence point and should not have side effects.
val mkInvisibleLet: range -> Val -> Expr -> Expr -> Expr

/// Make a binding that binds a value to an expression in an "invisible" way.
/// Invisible bindings are not given a sequence point and should not have side effects.
val mkInvisibleBind: Val -> Expr -> Binding

/// Make a set of bindings that bind values to expressions in an "invisible" way.
/// Invisible bindings are not given a sequence point and should not have side effects.
val mkInvisibleBinds: Vals -> Exprs -> Bindings

/// Make a let-rec expression that locally binds values to expressions where self-reference back to the values is possible.
val mkLetRecBinds: range -> Bindings -> Expr -> Expr
 
/// TypeScheme (generalizedTypars, tauTy)
///
///    generalizedTypars -- the truly generalized type parameters 
///    tauTy  --  the body of the generalized type. A 'tau' type is one with its type parameters stripped off.
type TypeScheme = TypeScheme of Typars  * TType    

/// Make the right-hand side of a generalized binding, incorporating the generalized generic parameters from the type
/// scheme into the right-hand side as type generalizations.
val mkGenericBindRhs: TcGlobals -> range -> Typars -> TypeScheme -> Expr -> Expr

/// Test if the type parameter is one of those being generalized by a type scheme.
val isBeingGeneralized: Typar -> TypeScheme -> bool

/// Make the expression corresponding to 'expr1 && expr2'
val mkLazyAnd: TcGlobals -> range -> Expr -> Expr -> Expr

/// Make the expression corresponding to 'expr1 || expr2'
val mkLazyOr: TcGlobals -> range -> Expr -> Expr -> Expr

/// Make a byref type 
val mkByrefTy: TcGlobals -> TType -> TType

/// Make a byref type with a in/out kind inference parameter
val mkByrefTyWithInference: TcGlobals -> TType -> TType -> TType

/// Make a in-byref type with a in kind parameter
val mkInByrefTy: TcGlobals -> TType -> TType

/// Make an out-byref type with an out kind parameter
val mkOutByrefTy: TcGlobals -> TType -> TType

/// Make an expression that constructs a union case, e.g. 'Some(expr)'
val mkUnionCaseExpr: UnionCaseRef * TypeInst * Exprs * range -> Expr

/// Make an expression that constructs an exception value
val mkExnExpr: TyconRef * Exprs * range -> Expr

/// Make an expression that is IL assembly code
val mkAsmExpr: ILInstr list * TypeInst * Exprs * TTypes * range -> Expr

/// Make an expression that coerces one expression to another type
val mkCoerceExpr: Expr * TType * range * TType -> Expr

/// Make an expression that re-raises an exception
val mkReraise: range -> TType -> Expr

/// Make an expression that re-raises an exception via a library call
val mkReraiseLibCall: TcGlobals -> TType -> range -> Expr
 
/// Make an expression that gets an item from a tuple
val mkTupleFieldGet: TcGlobals -> TupInfo * Expr * TypeInst * int * range -> Expr

/// Make an expression that gets an item from an anonymous record
val mkAnonRecdFieldGet: TcGlobals -> AnonRecdTypeInfo * Expr * TypeInst * int * range -> Expr

/// Make an expression that gets an item from an anonymous record (via the address of the value if it is a struct)
val mkAnonRecdFieldGetViaExprAddr: AnonRecdTypeInfo * Expr * TypeInst * int * range -> Expr

/// Make an expression that gets an instance field from a record or class (via the address of the value if it is a struct)
val mkRecdFieldGetViaExprAddr: Expr * RecdFieldRef  * TypeInst * range -> Expr

/// Make an expression that gets the address of an instance field from a record or class (via the address of the value if it is a struct)
val mkRecdFieldGetAddrViaExprAddr: readonly: bool * Expr * RecdFieldRef * TypeInst * range -> Expr

/// Make an expression that gets a static field from a record or class 
val mkStaticRecdFieldGet: RecdFieldRef * TypeInst * range -> Expr

/// Make an expression that sets a static field in a record or class 
val mkStaticRecdFieldSet: RecdFieldRef * TypeInst * Expr * range -> Expr

/// Make an expression that gets the address of a static field in a record or class 
val mkStaticRecdFieldGetAddr: readonly: bool * RecdFieldRef  * TypeInst * range -> Expr

/// Make an expression that sets an instance the field of a record or class (via the address of the value if it is a struct)
val mkRecdFieldSetViaExprAddr: Expr * RecdFieldRef * TypeInst * Expr * range -> Expr

/// Make an expression that gets the tag of a union value (via the address of the value if it is a struct)
val mkUnionCaseTagGetViaExprAddr: Expr * TyconRef * TypeInst * range -> Expr

/// Make a 'TOp.UnionCaseTest' expression, which tests that a union value is of a particular union case.
val mkUnionCaseTest: Expr * UnionCaseRef  * TypeInst * range -> Expr

/// Make a 'TOp.UnionCaseProof' expression, which proves a union value is over a particular case (used only for ref-unions, not struct-unions)
val mkUnionCaseProof: Expr * UnionCaseRef  * TypeInst * range -> Expr

/// Build a 'TOp.UnionCaseFieldGet' expression for something we've already determined to be a particular union case. For ref-unions,
/// the input expression has 'TType_ucase', which is an F# compiler internal "type" corresponding to the union case. For struct-unions,
/// the input should be the address of the expression.
val mkUnionCaseFieldGetProvenViaExprAddr: Expr * UnionCaseRef  * TypeInst * int * range -> Expr

/// Build a 'TOp.UnionCaseFieldGetAddr' expression for a field of a union when we've already determined the value to be a particular union case. For ref-unions,
/// the input expression has 'TType_ucase', which is an F# compiler internal "type" corresponding to the union case. For struct-unions,
/// the input should be the address of the expression.
val mkUnionCaseFieldGetAddrProvenViaExprAddr: readonly: bool * Expr * UnionCaseRef  * TypeInst * int * range -> Expr

/// Build a 'TOp.UnionCaseFieldGetAddr' expression for a field of a union when we've already determined the value to be a particular union case. For ref-unions,
/// the input expression has 'TType_ucase', which is an F# compiler internal "type" corresponding to the union case. For struct-unions,
/// the input should be the address of the expression.
val mkUnionCaseFieldGetUnprovenViaExprAddr: Expr * UnionCaseRef  * TypeInst * int * range -> Expr

/// Build a 'TOp.UnionCaseFieldSet' expression. For ref-unions, the input expression has 'TType_ucase', which is 
/// an F# compiler internal "type" corresponding to the union case. For struct-unions,
/// the input should be the address of the expression.
val mkUnionCaseFieldSet: Expr * UnionCaseRef  * TypeInst * int  * Expr * range -> Expr

/// Like mkUnionCaseFieldGetUnprovenViaExprAddr, but for struct-unions, the input should be a copy of the expression.
val mkUnionCaseFieldGetUnproven: TcGlobals -> Expr * UnionCaseRef   * TypeInst * int * range -> Expr

/// Make an expression that gets an instance field from an F# exception value 
val mkExnCaseFieldGet: Expr * TyconRef * int * range -> Expr

/// Make an expression that sets an instance field in an F# exception value 
val mkExnCaseFieldSet: Expr * TyconRef * int  * Expr * range -> Expr

/// Make an expression that gets the address of an element in an array
val mkArrayElemAddress: TcGlobals -> readonly: bool * ILReadonly * bool * ILArrayShape * TType * Expr list * range -> Expr

/// The largest tuple before we start encoding, i.e. 7
val maxTuple: int

/// The number of fields in the largest tuple before we start encoding, i.e. 7
val goodTupleFields: int

/// Check if a TyconRef is for a .NET tuple type. Currently this includes Tuple`1 even though
/// that' not really part of the target set of TyconRef used to represent F# tuples.
val isCompiledTupleTyconRef: TcGlobals -> TyconRef -> bool

/// Get a TyconRef for a .NET tuple type
val mkCompiledTupleTyconRef: TcGlobals -> bool -> int -> TyconRef

/// Convert from F# tuple types to .NET tuple types.
val mkCompiledTupleTy: TcGlobals -> bool -> TTypes -> TType

/// Convert from F# tuple creation expression to .NET tuple creation expressions
val mkCompiledTuple: TcGlobals -> bool -> TTypes * Exprs * range -> TyconRef * TTypes * Exprs * range

/// Make a TAST expression representing getting an item fromm a tuple
val mkGetTupleItemN: TcGlobals -> range -> int -> ILType -> bool -> Expr -> TType -> Expr

/// Evaluate the TupInfo to work out if it is a struct or a ref.  Currently this is very simple
/// but TupInfo may later be used carry variables that infer structness.
val evalTupInfoIsStruct: TupInfo -> bool

/// Evaluate the AnonRecdTypeInfo to work out if it is a struct or a ref. 
val evalAnonInfoIsStruct: AnonRecdTypeInfo -> bool

/// If it is a tuple type, ensure it's outermost type is a .NET tuple type, otherwise leave unchanged
val convertToTypeWithMetadataIfPossible: TcGlobals -> TType -> TType

/// An exception representing a warning for a defensive copy of an immutable struct
exception DefensiveCopyWarning of string * range 

type Mutates = AddressOfOp | DefinitelyMutates | PossiblyMutates | NeverMutates

/// Helper to create an expression that dereferences an address.
val mkDerefAddrExpr: mAddrGet: range -> expr: Expr -> mExpr: range -> exprTy: TType -> Expr

/// Helper to take the address of an expression
val mkExprAddrOfExprAux: TcGlobals -> bool -> bool -> Mutates -> Expr -> ValRef option -> range -> (Val * Expr) option * Expr * bool * bool

/// Take the address of an expression, or force it into a mutable local. Any allocated
/// mutable local may need to be kept alive over a larger expression, hence we return
/// a wrapping function that wraps "let mutable loc = Expr in ..." around a larger
/// expression.
val mkExprAddrOfExpr: TcGlobals -> bool -> bool -> Mutates -> Expr -> ValRef option -> range -> (Expr -> Expr) * Expr * bool * bool

/// Maps Val to T, based on stamps
[<Struct;NoEquality; NoComparison>]
type ValMap<'T> = 

    member Contents: StampMap<'T>

    member Item: Val -> 'T with get

    member TryFind: Val -> 'T option

    member ContainsVal: Val -> bool

    member Add: Val -> 'T -> ValMap<'T>

    member Remove: Val -> ValMap<'T>

    member IsEmpty: bool

    static member Empty: ValMap<'T>

    static member OfList: (Val * 'T) list -> ValMap<'T>

/// Mutable data structure mapping Val's to T based on stamp keys
[<Sealed; NoEquality; NoComparison>]
type ValHash<'T> =

    member Values: seq<'T>

    member TryFind: Val -> 'T option

    member Add: Val * 'T -> unit

    static member Create: unit -> ValHash<'T>

/// Maps Val's to list of T based on stamp keys
[<Struct; NoEquality; NoComparison>]
type ValMultiMap<'T> =

    member ContainsKey: Val -> bool

    member Find: Val -> 'T list

    member Add: Val * 'T -> ValMultiMap<'T>

    member Remove: Val -> ValMultiMap<'T>

    member Contents: StampMap<'T list>

    static member Empty: ValMultiMap<'T>

/// Maps type parameters to entries based on stamp keys
[<Sealed>]
type TyparMap<'T>  =

    /// Get the entry for the given type parameter
    member Item: Typar -> 'T with get

    /// Determine is the map contains an entry for the given type parameter
    member ContainsKey: Typar -> bool

    /// Try to find the entry for the given type parameter
    member TryFind: Typar -> 'T option

    /// Make a new map, containing a new entry for the given type parameter
    member Add: Typar * 'T -> TyparMap<'T> 

    /// The empty map
    static member Empty: TyparMap<'T> 

/// Maps TyconRef to T based on stamp keys
[<NoEquality; NoComparison; Sealed>]
type TyconRefMap<'T> =

    /// Get the entry for the given type definition
    member Item: TyconRef -> 'T with get

    /// Try to find the entry for the given type definition
    member TryFind: TyconRef -> 'T option

    /// Determine is the map contains an entry for the given type definition
    member ContainsKey: TyconRef -> bool

    /// Make a new map, containing a new entry for the given type definition
    member Add: TyconRef -> 'T -> TyconRefMap<'T>

    /// Remove the entry for the given type definition, if any
    member Remove: TyconRef -> TyconRefMap<'T>

    /// Determine if the map is empty
    member IsEmpty: bool

    /// The empty map
    static member Empty: TyconRefMap<'T>

    /// Make a new map, containing entries for the given type definitions
    static member OfList: (TyconRef * 'T) list -> TyconRefMap<'T>

/// Maps TyconRef to list of T based on stamp keys
[<Struct; NoEquality; NoComparison>]
type TyconRefMultiMap<'T> =

    /// Fetch the entries for the given type definition
    member Find: TyconRef -> 'T list

    /// Make a new map, containing a new entry for the given type definition
    member Add: TyconRef * 'T -> TyconRefMultiMap<'T>

    /// The empty map
    static member Empty: TyconRefMultiMap<'T>

    /// Make a new map, containing a entries for the given type definitions
    static member OfList: (TyconRef * 'T) list -> TyconRefMultiMap<'T>

/// An ordering for value definitions, based on stamp
val valOrder: IComparer<Val>

/// An ordering for type definitions, based on stamp
val tyconOrder: IComparer<Tycon>

/// An ordering for record fields, based on stamp
val recdFieldRefOrder: IComparer<RecdFieldRef>

/// An ordering for type parameters, based on stamp
val typarOrder: IComparer<Typar>

/// Equality for type definition references
val tyconRefEq: TcGlobals -> TyconRef -> TyconRef -> bool

/// Equality for value references
val valRefEq: TcGlobals -> ValRef -> ValRef -> bool

//-------------------------------------------------------------------------
// Operations on types: substitution
//------------------------------------------------------------------------- 

/// Represents an instantiation where types replace type parameters
type TyparInst = (Typar * TType) list

/// Represents an instantiation where type definition references replace other type definition references
type TyconRefRemap = TyconRefMap<TyconRef>

/// Represents an instantiation where value references replace other value references
type ValRemap = ValMap<ValRef>

/// Represents a combination of substitutions/instantiations where things replace other things during remapping
[<NoEquality; NoComparison>]
type Remap =
    { tpinst: TyparInst
      valRemap: ValRemap
      tyconRefRemap: TyconRefRemap
      removeTraitSolutions: bool }

    static member Empty: Remap

val addTyconRefRemap: TyconRef -> TyconRef -> Remap -> Remap

val addValRemap: Val -> Val -> Remap -> Remap

val mkTyparInst: Typars -> TTypes -> TyparInst

val mkTyconRefInst: TyconRef -> TypeInst -> TyparInst

val emptyTyparInst: TyparInst

val instType: TyparInst -> TType -> TType

val instTypes: TyparInst -> TypeInst -> TypeInst

val instTyparConstraints: TyparInst -> TyparConstraint list -> TyparConstraint list 

val instTrait: TyparInst -> TraitConstraintInfo -> TraitConstraintInfo 

/// From typars to types 
val generalizeTypars: Typars -> TypeInst

val generalizeTyconRef: TyconRef -> TTypes * TType

val generalizedTyconRef: TyconRef -> TType

val mkTyparToTyparRenaming: Typars -> Typars -> TyparInst * TTypes

//-------------------------------------------------------------------------
// See through typar equations from inference and/or type abbreviation equations.
//------------------------------------------------------------------------- 

val reduceTyconRefAbbrev: TyconRef -> TypeInst -> TType

val reduceTyconRefMeasureableOrProvided: TcGlobals -> TyconRef -> TypeInst -> TType

val reduceTyconRefAbbrevMeasureable: TyconRef -> Measure

/// set bool to 'true' to allow shortcutting of type parameter equation chains during stripping 
val stripTyEqnsA: TcGlobals -> bool -> TType -> TType 

val stripTyEqns: TcGlobals -> TType -> TType

val stripTyEqnsAndMeasureEqns: TcGlobals -> TType -> TType

val tryNormalizeMeasureInType: TcGlobals -> TType -> TType

/// See through F# exception abbreviations
val stripExnEqns: TyconRef -> Tycon

val recdFieldsOfExnDefRef: TyconRef -> RecdField list

val recdFieldTysOfExnDefRef: TyconRef -> TType list

//-------------------------------------------------------------------------
// Analyze types.  These all look through type abbreviations and 
// inference equations, i.e. are "stripped"
//------------------------------------------------------------------------- 

val destForallTy: TcGlobals -> TType -> Typars * TType

val destFunTy: TcGlobals -> TType -> TType * TType

val destAnyTupleTy: TcGlobals -> TType -> TupInfo * TTypes

val destRefTupleTy: TcGlobals -> TType -> TTypes

val destStructTupleTy: TcGlobals -> TType -> TTypes

val destTyparTy: TcGlobals -> TType -> Typar

val destAnyParTy: TcGlobals -> TType -> Typar

val destMeasureTy: TcGlobals -> TType -> Measure

val tryDestForallTy: TcGlobals -> TType -> Typars * TType

val isFunTy: TcGlobals -> TType -> bool

val isForallTy: TcGlobals -> TType -> bool

val isAnyTupleTy: TcGlobals -> TType -> bool

val isRefTupleTy: TcGlobals -> TType -> bool

val isStructTupleTy: TcGlobals -> TType -> bool

val isStructAnonRecdTy: TcGlobals -> TType -> bool

val isAnonRecdTy: TcGlobals -> TType -> bool

val isUnionTy: TcGlobals -> TType -> bool

val isReprHiddenTy: TcGlobals -> TType -> bool

val isFSharpObjModelTy: TcGlobals -> TType -> bool

val isRecdTy: TcGlobals -> TType -> bool

val isFSharpStructOrEnumTy: TcGlobals -> TType -> bool

val isFSharpEnumTy: TcGlobals -> TType -> bool

val isTyparTy: TcGlobals -> TType -> bool

val isAnyParTy: TcGlobals -> TType -> bool

val tryAnyParTy: TcGlobals -> TType -> ValueOption<Typar>

val tryAnyParTyOption: TcGlobals -> TType -> Typar option

val isMeasureTy: TcGlobals -> TType -> bool

val mkAppTy: TyconRef -> TypeInst -> TType

val mkProvenUnionCaseTy: UnionCaseRef -> TypeInst -> TType

val isProvenUnionCaseTy: TType -> bool

val isAppTy: TcGlobals -> TType -> bool

val tryAppTy: TcGlobals -> TType -> ValueOption<TyconRef * TypeInst>

val destAppTy: TcGlobals -> TType -> TyconRef * TypeInst

val tcrefOfAppTy: TcGlobals -> TType -> TyconRef

val tryTcrefOfAppTy: TcGlobals -> TType -> ValueOption<TyconRef>

val tryDestTyparTy: TcGlobals -> TType -> ValueOption<Typar>

val tryDestFunTy: TcGlobals -> TType -> ValueOption<(TType * TType)>

val tryDestAnonRecdTy: TcGlobals -> TType -> ValueOption<AnonRecdTypeInfo * TType list>

val argsOfAppTy: TcGlobals -> TType -> TypeInst

val mkInstForAppTy: TcGlobals -> TType -> TyparInst

/// Try to get a TyconRef for a type without erasing type abbreviations
val tryNiceEntityRefOfTy: TType -> ValueOption<TyconRef>

val tryNiceEntityRefOfTyOption: TType -> TyconRef option

val domainOfFunTy: TcGlobals -> TType -> TType

val rangeOfFunTy: TcGlobals -> TType -> TType

val stripFunTy: TcGlobals -> TType -> TType list * TType

val stripFunTyN: TcGlobals -> int -> TType -> TType list * TType

val applyForallTy: TcGlobals -> TType -> TypeInst -> TType

val tryDestAnyTupleTy: TcGlobals -> TType -> TupInfo * TType list

val tryDestRefTupleTy: TcGlobals -> TType -> TType list

//-------------------------------------------------------------------------
// Compute actual types of union cases and fields given an instantiation 
// of the generic type parameters of the enclosing type.
//------------------------------------------------------------------------- 

val actualResultTyOfUnionCase: TypeInst -> UnionCaseRef -> TType

val actualTysOfUnionCaseFields: TyparInst -> UnionCaseRef -> TType list

val actualTysOfInstanceRecdFields: TyparInst -> TyconRef -> TType list

val actualTyOfRecdField: TyparInst -> RecdField -> TType

val actualTyOfRecdFieldRef: RecdFieldRef -> TypeInst -> TType

val actualTyOfRecdFieldForTycon: Tycon -> TypeInst -> RecdField -> TType

//-------------------------------------------------------------------------
// Top types: guaranteed to be compiled to .NET methods, and must be able to 
// have user-specified argument names (for stability w.r.t. reflection)
// and user-specified argument and return attributes.
//------------------------------------------------------------------------- 

type UncurriedArgInfos = (TType * ArgReprInfo) list 

type CurriedArgInfos = UncurriedArgInfos list

type TraitWitnessInfos = TraitWitnessInfo list

val destTopForallTy: TcGlobals -> ValReprInfo -> TType -> Typars * TType 

val GetTopTauTypeInFSharpForm: TcGlobals -> ArgReprInfo list list -> TType -> range -> CurriedArgInfos * TType

val GetTopValTypeInFSharpForm: TcGlobals -> ValReprInfo -> TType -> range -> Typars * CurriedArgInfos * TType * ArgReprInfo

val IsCompiledAsStaticProperty: TcGlobals -> Val -> bool

val IsCompiledAsStaticPropertyWithField: TcGlobals -> Val -> bool

val GetTopValTypeInCompiledForm: TcGlobals -> ValReprInfo -> int -> TType -> range -> Typars * TraitWitnessInfos * CurriedArgInfos * TType option * ArgReprInfo

val GetFSharpViewOfReturnType: TcGlobals -> TType option -> TType

val NormalizeDeclaredTyparsForEquiRecursiveInference: TcGlobals -> Typars -> Typars

//-------------------------------------------------------------------------
// Compute the return type after an application
//------------------------------------------------------------------------- 
 
val applyTys: TcGlobals -> TType -> TType list * 'T list -> TType

//-------------------------------------------------------------------------
// Compute free variables in types
//------------------------------------------------------------------------- 
 
val emptyFreeTypars: FreeTypars

val unionFreeTypars: FreeTypars -> FreeTypars -> FreeTypars

val emptyFreeTycons: FreeTycons

val unionFreeTycons: FreeTycons -> FreeTycons -> FreeTycons

val emptyFreeTyvars: FreeTyvars

val isEmptyFreeTyvars: FreeTyvars -> bool

val unionFreeTyvars: FreeTyvars -> FreeTyvars -> FreeTyvars

val emptyFreeLocals: FreeLocals

val unionFreeLocals: FreeLocals -> FreeLocals -> FreeLocals

type FreeVarOptions 

val CollectLocalsNoCaching: FreeVarOptions

val CollectTyparsNoCaching: FreeVarOptions

val CollectTyparsAndLocalsNoCaching: FreeVarOptions

val CollectTyparsAndLocals: FreeVarOptions

val CollectLocals: FreeVarOptions

val CollectTypars: FreeVarOptions

val CollectAllNoCaching: FreeVarOptions

val CollectAll: FreeVarOptions

val accFreeInTypes: FreeVarOptions -> TType list -> FreeTyvars -> FreeTyvars

val accFreeInType: FreeVarOptions -> TType -> FreeTyvars -> FreeTyvars

val accFreeInTypars: FreeVarOptions -> Typars -> FreeTyvars -> FreeTyvars

val freeInType: FreeVarOptions -> TType -> FreeTyvars

val freeInTypes: FreeVarOptions -> TType list -> FreeTyvars

val freeInVal: FreeVarOptions -> Val -> FreeTyvars

// This one puts free variables in canonical left-to-right order. 
val freeInTypeLeftToRight: TcGlobals -> bool -> TType -> Typars

val freeInTypesLeftToRight: TcGlobals -> bool -> TType list -> Typars

val freeInTypesLeftToRightSkippingConstraints: TcGlobals -> TType list -> Typars

val freeInModuleTy: ModuleOrNamespaceType -> FreeTyvars

val isDimensionless: TcGlobals -> TType -> bool

//---------------------------------------------------------------------------
// TType modifications and comparisons
//---------------------------------------------------------------------------

val stripMeasuresFromTType: TcGlobals -> TType -> TType

//-------------------------------------------------------------------------
// Equivalence of types (up to substitution of type variables in the left-hand type)
//------------------------------------------------------------------------- 

[<NoEquality; NoComparison>]
type TypeEquivEnv = 
    { EquivTypars: TyparMap<TType>
      EquivTycons: TyconRefRemap }

    static member Empty: TypeEquivEnv

    member BindEquivTypars: Typars -> Typars -> TypeEquivEnv

    static member FromTyparInst: TyparInst -> TypeEquivEnv

    static member FromEquivTypars: Typars -> Typars -> TypeEquivEnv

val traitsAEquivAux: Erasure -> TcGlobals -> TypeEquivEnv -> TraitConstraintInfo -> TraitConstraintInfo -> bool

val traitsAEquiv: TcGlobals -> TypeEquivEnv -> TraitConstraintInfo -> TraitConstraintInfo -> bool

val traitKeysAEquivAux: Erasure -> TcGlobals -> TypeEquivEnv -> TraitWitnessInfo -> TraitWitnessInfo -> bool

val traitKeysAEquiv: TcGlobals -> TypeEquivEnv -> TraitWitnessInfo -> TraitWitnessInfo -> bool

val typarConstraintsAEquivAux: Erasure -> TcGlobals -> TypeEquivEnv -> TyparConstraint -> TyparConstraint -> bool

val typarConstraintsAEquiv: TcGlobals -> TypeEquivEnv -> TyparConstraint -> TyparConstraint -> bool

val typarsAEquiv: TcGlobals -> TypeEquivEnv -> Typars -> Typars -> bool

val typeAEquivAux: Erasure -> TcGlobals -> TypeEquivEnv -> TType -> TType -> bool

val typeAEquiv: TcGlobals -> TypeEquivEnv -> TType -> TType -> bool

val returnTypesAEquivAux: Erasure -> TcGlobals -> TypeEquivEnv -> TType option -> TType option -> bool

val returnTypesAEquiv: TcGlobals -> TypeEquivEnv -> TType option -> TType option -> bool

val tcrefAEquiv: TcGlobals -> TypeEquivEnv -> TyconRef -> TyconRef -> bool

val valLinkageAEquiv: TcGlobals -> TypeEquivEnv -> Val -> Val -> bool

val anonInfoEquiv: AnonRecdTypeInfo -> AnonRecdTypeInfo -> bool

//-------------------------------------------------------------------------
// Erasure of types wrt units-of-measure and type providers
//-------------------------------------------------------------------------

// Return true if this type is a nominal type that is an erased provided type
val isErasedType: TcGlobals -> TType -> bool

// Return all components (units-of-measure, and types) of this type that would be erased
val getErasedTypes: TcGlobals -> TType -> TType list

//-------------------------------------------------------------------------
// Unit operations
//------------------------------------------------------------------------- 

val MeasurePower: Measure -> int -> Measure

val ListMeasureVarOccsWithNonZeroExponents: Measure -> (Typar * Rational) list

val ListMeasureConOccsWithNonZeroExponents: TcGlobals -> bool -> Measure -> (TyconRef * Rational) list

val ProdMeasures: Measure list -> Measure

val MeasureVarExponent: Typar -> Measure -> Rational

val MeasureExprConExponent: TcGlobals -> bool -> TyconRef -> Measure -> Rational

val normalizeMeasure: TcGlobals -> Measure -> Measure


//-------------------------------------------------------------------------
// Members 
//------------------------------------------------------------------------- 

val GetTypeOfMemberInFSharpForm: TcGlobals -> ValRef -> Typars * CurriedArgInfos * TType * ArgReprInfo

val GetTypeOfMemberInMemberForm: TcGlobals -> ValRef -> Typars * TraitWitnessInfos * CurriedArgInfos * TType option * ArgReprInfo

val GetTypeOfIntrinsicMemberInCompiledForm: TcGlobals -> ValRef -> Typars * TraitWitnessInfos * CurriedArgInfos * TType option * ArgReprInfo

val GetMemberTypeInMemberForm: TcGlobals -> SynMemberFlags -> ValReprInfo -> int -> TType -> range -> Typars * TraitWitnessInfos * CurriedArgInfos * TType option * ArgReprInfo

/// Returns (parentTypars,memberParentTypars,memberMethodTypars,memberToParentInst,tinst)
val PartitionValTyparsForApparentEnclosingType: TcGlobals -> Val -> (Typars * Typars * Typars * TyparInst * TType list) option

/// Returns (parentTypars,memberParentTypars,memberMethodTypars,memberToParentInst,tinst)
val PartitionValTypars: TcGlobals -> Val -> (Typars * Typars * Typars * TyparInst * TType list) option

/// Returns (parentTypars,memberParentTypars,memberMethodTypars,memberToParentInst,tinst)
val PartitionValRefTypars: TcGlobals -> ValRef -> (Typars * Typars * Typars * TyparInst * TType list) option

/// Count the number of type parameters on the enclosing type
val CountEnclosingTyparsOfActualParentOfVal: Val -> int

val ReturnTypeOfPropertyVal: TcGlobals -> Val -> TType

val ArgInfosOfPropertyVal: TcGlobals -> Val -> UncurriedArgInfos 

val ArgInfosOfMember: TcGlobals -> ValRef -> CurriedArgInfos 

val GetMemberCallInfo: TcGlobals -> ValRef * ValUseFlag -> int * bool * bool * bool * bool * bool * bool * bool

//-------------------------------------------------------------------------
// Printing
//------------------------------------------------------------------------- 
 
type TyparConstraintsWithTypars = (Typar * TyparConstraint) list

module PrettyTypes =

    val NeedsPrettyTyparName: Typar -> bool

    val NewPrettyTypars: TyparInst -> Typars -> string list -> Typars * TyparInst

    val PrettyTyparNames: (Typar -> bool) -> string list -> Typars -> string list

    val PrettifyType: TcGlobals -> TType -> TType * TyparConstraintsWithTypars

    val PrettifyInstAndTyparsAndType: TcGlobals -> TyparInst * Typars * TType -> (TyparInst * Typars * TType) * TyparConstraintsWithTypars

    val PrettifyTypePair: TcGlobals -> TType * TType -> (TType * TType) * TyparConstraintsWithTypars

    val PrettifyTypes: TcGlobals -> TTypes -> TTypes * TyparConstraintsWithTypars
    
    /// same as PrettifyTypes, but allows passing the types along with a discriminant value
    /// useful to prettify many types that need to be sorted out after prettifying operation
    /// took place.
    val PrettifyDiscriminantAndTypePairs: TcGlobals -> ('Discriminant * TType) list -> ('Discriminant * TType) list * TyparConstraintsWithTypars

    val PrettifyInst: TcGlobals -> TyparInst -> TyparInst * TyparConstraintsWithTypars

    val PrettifyInstAndType: TcGlobals -> TyparInst * TType -> (TyparInst * TType) * TyparConstraintsWithTypars

    val PrettifyInstAndTypes: TcGlobals -> TyparInst * TTypes -> (TyparInst * TTypes) * TyparConstraintsWithTypars

    val PrettifyInstAndSig: TcGlobals -> TyparInst * TTypes * TType -> (TyparInst * TTypes * TType) * TyparConstraintsWithTypars

    val PrettifyCurriedTypes: TcGlobals -> TType list list -> TType list list * TyparConstraintsWithTypars

    val PrettifyCurriedSigTypes: TcGlobals -> TType list list * TType -> (TType list list * TType) * TyparConstraintsWithTypars

    val PrettifyInstAndUncurriedSig: TcGlobals -> TyparInst * UncurriedArgInfos * TType -> (TyparInst * UncurriedArgInfos * TType) * TyparConstraintsWithTypars

    val PrettifyInstAndCurriedSig: TcGlobals -> TyparInst * TTypes * CurriedArgInfos * TType -> (TyparInst * TTypes * CurriedArgInfos * TType) * TyparConstraintsWithTypars

/// Describes how generic type parameters in a type will be formatted during printing
type GenericParameterStyle =
    /// Use the IsPrefixDisplay member of the TyCon to determine the style
    | Implicit
    /// Force the prefix style: List<int>
    | Prefix
    /// Force the suffix style: int List
    | Suffix

[<NoEquality; NoComparison>]
type DisplayEnv = 
    { includeStaticParametersInTypeNames: bool
      openTopPathsSorted: Lazy<string list list> 
      openTopPathsRaw: string list list
      shortTypeNames: bool
      suppressNestedTypes: bool
      maxMembers: int option
      showObsoleteMembers: bool 
      showHiddenMembers: bool
      showTyparBinding: bool
      showImperativeTyparAnnotations: bool
      suppressInlineKeyword:bool
      suppressMutableKeyword:bool
      showMemberContainers: bool
      shortConstraints:bool
      useColonForReturnType:bool
      showAttributes: bool
      showOverrides:bool
      showConstraintTyparAnnotations:bool
      abbreviateAdditionalConstraints: bool
      showTyparDefaultConstraints: bool
      /// If set, signatures will be rendered with XML documentation comments for members if they exist
      /// Defaults to false, expected use cases include things like signature file generation.
      showDocumentation: bool
      shrinkOverloads: bool
      printVerboseSignatures: bool
      escapeKeywordNames: bool
      g: TcGlobals
      contextAccessibility: Accessibility
      generatedValueLayout: (Val -> Layout option)
      genericParameterStyle: GenericParameterStyle }

    member SetOpenPaths: string list list -> DisplayEnv

    static member Empty: TcGlobals -> DisplayEnv

    member AddAccessibility: Accessibility -> DisplayEnv

    member AddOpenPath: string list -> DisplayEnv

    member AddOpenModuleOrNamespace: ModuleOrNamespaceRef -> DisplayEnv

    member UseGenericParameterStyle: GenericParameterStyle -> DisplayEnv

    static member InitialForSigFileGeneration: TcGlobals -> DisplayEnv

val tagEntityRefName: xref: EntityRef -> name: string -> TaggedText

/// Return the full text for an item as we want it displayed to the user as a fully qualified entity
val fullDisplayTextOfModRef: ModuleOrNamespaceRef -> string

val fullDisplayTextOfParentOfModRef: ModuleOrNamespaceRef -> ValueOption<string>

val fullDisplayTextOfValRef: ValRef -> string

val fullDisplayTextOfValRefAsLayout: ValRef -> Layout

val fullDisplayTextOfTyconRef: TyconRef -> string

val fullDisplayTextOfTyconRefAsLayout: TyconRef -> Layout

val fullDisplayTextOfExnRef: TyconRef -> string

val fullDisplayTextOfExnRefAsLayout: TyconRef -> Layout

val fullDisplayTextOfUnionCaseRef: UnionCaseRef -> string

val fullDisplayTextOfRecdFieldRef: RecdFieldRef -> string

val ticksAndArgCountTextOfTyconRef: TyconRef -> string

/// A unique qualified name for each type definition, used to qualify the names of interface implementation methods
val qualifiedMangledNameOfTyconRef: TyconRef -> string -> string

val qualifiedInterfaceImplementationName: TcGlobals -> TType -> string -> string

val trimPathByDisplayEnv: DisplayEnv -> string list -> string

val prefixOfStaticReq: TyparStaticReq -> string

val prefixOfRigidTypar: Typar -> string

/// Utilities used in simplifying types for visual presentation
module SimplifyTypes = 

    type TypeSimplificationInfo =
        { singletons: Typar Zset
          inplaceConstraints: Zmap<Typar,TType>
          postfixConstraints: TyparConstraintsWithTypars }

    val typeSimplificationInfo0: TypeSimplificationInfo

    val CollectInfo: bool -> TType list -> TyparConstraintsWithTypars -> TypeSimplificationInfo

val superOfTycon: TcGlobals -> Tycon -> TType

val abstractSlotValRefsOfTycons: Tycon list -> ValRef list

val abstractSlotValsOfTycons: Tycon list -> Val list

//-------------------------------------------------------------------------
// Free variables in expressions etc.
//------------------------------------------------------------------------- 

val emptyFreeVars: FreeVars

val unionFreeVars: FreeVars -> FreeVars -> FreeVars

val accFreeInTargets: FreeVarOptions -> DecisionTreeTarget array -> FreeVars -> FreeVars

val accFreeInExprs: FreeVarOptions -> Exprs -> FreeVars -> FreeVars

val accFreeInSwitchCases: FreeVarOptions -> DecisionTreeCase list -> DecisionTree option -> FreeVars -> FreeVars

val accFreeInDecisionTree: FreeVarOptions -> DecisionTree -> FreeVars -> FreeVars

/// Get the free variables in a module definition.
val freeInModuleOrNamespace: FreeVarOptions -> ModuleOrNamespaceExpr -> FreeVars

/// Get the free variables in an expression.
val freeInExpr: FreeVarOptions -> Expr -> FreeVars

/// Get the free variables in the right hand side of a binding.
val freeInBindingRhs: FreeVarOptions -> Binding -> FreeVars

/// Check if a set of free type variables are all public
val freeTyvarsAllPublic: FreeTyvars -> bool

/// Check if a set of free variables are all public
val freeVarsAllPublic: FreeVars -> bool

/// Get the mark/range/position information from an expression
type Expr with 
    member Range: range

/// Compute the type of an expression from the expression itself
val tyOfExpr: TcGlobals -> Expr -> TType 

/// A flag to govern whether arity inference should be type-directed or syntax-directed when 
/// inferring an arity from a lambda expression.
[<RequireQualifiedAccess>]
type AllowTypeDirectedDetupling = 
    | Yes 
    | No

/// Given a (curried) lambda expression, pull off its arguments
val stripTopLambda: Expr * TType -> Typars * Val list list * Expr * TType

/// Given a lambda expression, extract the ValReprInfo for its arguments and other details
val InferArityOfExpr: TcGlobals -> AllowTypeDirectedDetupling -> TType -> Attribs list list -> Attribs -> Expr -> ValReprInfo

/// Given a lambda binding, extract the ValReprInfo for its arguments and other details
val InferArityOfExprBinding: TcGlobals -> AllowTypeDirectedDetupling -> Val -> Expr -> ValReprInfo

/// Mutate a value to indicate it should be considered a local rather than a module-bound definition
// REVIEW: this mutation should not be needed 
val setValHasNoArity: Val -> Val

/// Indicate what should happen to value definitions when copying expressions
type ValCopyFlag = 
    | CloneAll
    | CloneAllAndMarkExprValsAsCompilerGenerated

    /// OnlyCloneExprVals is a nasty setting to reuse the cloning logic in a mode where all 
    /// Tycon and "module/member" Val objects keep their identity, but the Val objects for all Expr bindings
    /// are cloned. This is used to 'fixup' the TAST created by tlr.fs 
    ///
    /// This is a fragile mode of use. It's not really clear why TLR needs to create a "bad" expression tree that
    /// reuses Val objects as multiple value bindings, and its been the cause of several subtle bugs.
    | OnlyCloneExprVals

/// Remap a reference to a type definition using the given remapping substitution
val remapTyconRef: TyconRefRemap -> TyconRef -> TyconRef

/// Remap a reference to a union case using the given remapping substitution
val remapUnionCaseRef: TyconRefRemap -> UnionCaseRef -> UnionCaseRef

/// Remap a reference to a record field using the given remapping substitution
val remapRecdFieldRef: TyconRefRemap -> RecdFieldRef -> RecdFieldRef

/// Remap a reference to a value using the given remapping substitution
val remapValRef: Remap -> ValRef -> ValRef

/// Remap an expression using the given remapping substitution
val remapExpr: TcGlobals -> ValCopyFlag -> Remap -> Expr -> Expr

/// Remap an attribute using the given remapping substitution
val remapAttrib: TcGlobals -> Remap -> Attrib -> Attrib

/// Remap a (possible generic) type using the given remapping substitution
val remapPossibleForallTy: TcGlobals -> Remap -> TType -> TType

/// Copy an entire module or namespace type using the given copying flags
val copyModuleOrNamespaceType: TcGlobals -> ValCopyFlag -> ModuleOrNamespaceType -> ModuleOrNamespaceType

/// Copy an entire expression using the given copying flags
val copyExpr: TcGlobals -> ValCopyFlag -> Expr -> Expr

/// Copy an entire implementation file using the given copying flags
val copyImplFile: TcGlobals -> ValCopyFlag -> TypedImplFile -> TypedImplFile

/// Copy a method slot signature, including new generic type parameters if the slot signature represents a generic method
val copySlotSig: SlotSig -> SlotSig

/// Instantiate the generic type parameters in a method slot signature, building a new one
val instSlotSig: TyparInst -> SlotSig -> SlotSig

/// Instantiate the generic type parameters in an expression, building a new one
val instExpr: TcGlobals -> TyparInst -> Expr -> Expr

/// The remapping that corresponds to a module meeting its signature
/// and also report the set of tycons, tycon representations and values hidden in the process.
type SignatureRepackageInfo = 
    { /// The list of corresponding values
      RepackagedVals: (ValRef * ValRef) list

      /// The list of corresponding modules, namespaces and type definitions
      RepackagedEntities: (TyconRef * TyconRef) list  }

    /// The empty table
    static member Empty: SignatureRepackageInfo
      
/// A set of tables summarizing the items hidden by a signature
type SignatureHidingInfo = 
    { HiddenTycons: Zset<Tycon>
      HiddenTyconReprs: Zset<Tycon>  
      HiddenVals: Zset<Val>
      HiddenRecdFields: Zset<RecdFieldRef>
      HiddenUnionCases: Zset<UnionCaseRef> }

    /// The empty table representing no hiding
    static member Empty: SignatureHidingInfo

/// Compute the remapping information implied by a signature being inferred for a particular implementation
val ComputeRemappingFromImplementationToSignature: TcGlobals -> ModuleOrNamespaceExpr -> ModuleOrNamespaceType -> SignatureRepackageInfo * SignatureHidingInfo

/// Compute the remapping information implied by an explicit signature being given for an inferred signature
val ComputeRemappingFromInferredSignatureToExplicitSignature: TcGlobals -> ModuleOrNamespaceType -> ModuleOrNamespaceType -> SignatureRepackageInfo * SignatureHidingInfo

/// Compute the hiding information that corresponds to the hiding applied at an assembly boundary
val ComputeHidingInfoAtAssemblyBoundary: ModuleOrNamespaceType -> SignatureHidingInfo -> SignatureHidingInfo

val mkRepackageRemapping: SignatureRepackageInfo -> Remap 

/// Wrap one module or namespace implementation in a 'namespace N' outer wrapper
val wrapModuleOrNamespaceExprInNamespace: Ident -> CompilationPath -> ModuleOrNamespaceExpr -> ModuleOrNamespaceExpr

/// Wrap one module or namespace definition in a 'namespace N' outer wrapper
val wrapModuleOrNamespaceTypeInNamespace: Ident -> CompilationPath -> ModuleOrNamespaceType -> ModuleOrNamespaceType * ModuleOrNamespace  

/// Wrap one module or namespace definition in a 'module M = ..' outer wrapper
val wrapModuleOrNamespaceType: Ident -> CompilationPath -> ModuleOrNamespaceType -> ModuleOrNamespace

/// Given an implementation, fetch its recorded signature
val SigTypeOfImplFile: TypedImplFile -> ModuleOrNamespaceType

/// Given a namespace, module or type definition, try to produce a reference to that entity.
val tryRescopeEntity: CcuThunk -> Entity -> ValueOption<EntityRef>

/// Given a value definition, try to produce a reference to that value. Fails for local values.
val tryRescopeVal: CcuThunk -> Remap -> Val -> ValueOption<ValRef>

/// Make the substitution (remapping) table for viewing a module or namespace 'from the outside'
///
/// Given the top-most signatures constrains the public compilation units
/// of an assembly, compute a remapping that converts local references to non-local references.
/// This remapping must be applied to all pickled expressions and types 
/// exported from the assembly.
val MakeExportRemapping: CcuThunk -> ModuleOrNamespace -> Remap

/// Make a remapping table for viewing a module or namespace 'from the outside'
val ApplyExportRemappingToEntity: TcGlobals -> Remap -> ModuleOrNamespace -> ModuleOrNamespace 

/// Determine if a type definition is hidden by a signature
val IsHiddenTycon: (Remap * SignatureHidingInfo) list -> Tycon -> bool

/// Determine if the representation of a type definition is hidden by a signature
val IsHiddenTyconRepr: (Remap * SignatureHidingInfo) list -> Tycon -> bool

/// Determine if a member, function or value is hidden by a signature
val IsHiddenVal: (Remap * SignatureHidingInfo) list -> Val -> bool

/// Determine if a record field is hidden by a signature
val IsHiddenRecdField: (Remap * SignatureHidingInfo) list -> RecdFieldRef -> bool

/// Adjust marks in expressions, replacing all marks by the given mark.
/// Used when inlining.
val remarkExpr: range -> Expr -> Expr
 
/// Build the application of a (possibly generic, possibly curried) function value to a set of type and expression arguments
val primMkApp: (Expr * TType) -> TypeInst -> Exprs -> range -> Expr

/// Build the application of a (possibly generic, possibly curried) function value to a set of type and expression arguments.
/// Reduce the application via let-bindings if the function value is a lambda expression.
val mkApps: TcGlobals -> (Expr * TType) * TType list list * Exprs * range -> Expr

/// Build the application of a generic construct to a set of type arguments.
/// Reduce the application via substitution if the function value is a typed lambda expression.
val mkTyAppExpr: range -> Expr * TType -> TType list -> Expr

/// Build an expression to mutate a local
///   localv <- e      
val mkValSet: range -> ValRef -> Expr -> Expr

/// Build an expression to mutate the contents of a local pointer
///  *localv_ptr = e   
val mkAddrSet: range -> ValRef -> Expr -> Expr

/// Build an expression to dereference a local pointer
/// *localv_ptr        
val mkAddrGet: range -> ValRef -> Expr

/// Build an expression to take the address of a local 
/// &localv           
val mkValAddr: range -> readonly: bool -> ValRef -> Expr

/// Build an expression representing the read of an instance class or record field.
/// First take the address of the record expression if it is a struct.
val mkRecdFieldGet: TcGlobals -> Expr * RecdFieldRef * TypeInst * range -> Expr

///  Accumulate the targets actually used in a decision graph (for reporting warnings)
val accTargetsOfDecisionTree: DecisionTree -> int list -> int list

/// Make a 'match' expression applying some peep-hole optimizations along the way, e.g to 
/// pre-decide the branch taken at compile-time.
val mkAndSimplifyMatch: DebugPointAtBinding -> range -> range -> TType -> DecisionTree -> DecisionTreeTarget list -> Expr

/// Make a 'match' expression without applying any peep-hole optimizations.
val primMkMatch: DebugPointAtBinding * range * DecisionTree * DecisionTreeTarget array * range * TType -> Expr

///  Work out what things on the right-han-side of a 'let rec' recursive binding need to be fixed up
val IterateRecursiveFixups: 
   TcGlobals -> Val option -> 
   (Val option -> Expr -> (Expr -> Expr) -> Expr -> unit) -> 
   Expr * (Expr -> Expr) -> Expr -> unit

/// Given a lambda expression taking multiple variables, build a corresponding lambda taking a tuple
val MultiLambdaToTupledLambda: TcGlobals -> Val list -> Expr -> Val * Expr

/// Given a lambda expression, adjust it to have be one or two lambda expressions (fun a -> (fun b -> ...)) 
/// where the first has the given arity.
val AdjustArityOfLambdaBody: TcGlobals -> int -> Val list -> Expr -> Val list * Expr

/// Make an application expression, doing beta reduction by introducing let-bindings
val MakeApplicationAndBetaReduce: TcGlobals -> Expr * TType * TypeInst list * Exprs * range -> Expr

/// Combine two static-resolution requirements on a type parameter
val JoinTyparStaticReq: TyparStaticReq -> TyparStaticReq -> TyparStaticReq

/// Layout for internal compiler debugging purposes
module DebugPrint =

    /// A global flag indicating whether debug output should include ranges
    val layoutRanges: bool ref

    /// Convert a type to a string for debugging purposes
    val showType: TType -> string

    /// Convert an expression to a string for debugging purposes
    val showExpr: TcGlobals -> Expr -> string

    /// Debug layout for a reference to a value
    val valRefL: ValRef -> Layout

    /// Debug layout for a reference to a union case
    val unionCaseRefL: UnionCaseRef -> Layout

    /// Debug layout for an value definition at its binding site
    val valAtBindL: TcGlobals -> Val -> Layout

    /// Debug layout for an integer
    val intL: int -> Layout

    /// Debug layout for a value definition
    val valL: Val -> Layout

    /// Debug layout for a type parameter definition
    val typarDeclL: Typar -> Layout

    /// Debug layout for a trait constraint
    val traitL: TraitConstraintInfo -> Layout

    /// Debug layout for a type parameter
    val typarL: Typar -> Layout

    /// Debug layout for a set of type parameters
    val typarsL: Typars -> Layout

    /// Debug layout for a type
    val typeL: TType -> Layout

    /// Debug layout for a method slot signature
    val slotSigL: SlotSig -> Layout

    /// Debug layout for the type signature of a module or namespace definition
    val entityTypeL: TcGlobals -> ModuleOrNamespaceType -> Layout

    /// Debug layout for a module or namespace definition
    val entityL: TcGlobals -> ModuleOrNamespace -> Layout

    /// Debug layout for the type of a value
    val typeOfValL: Val -> Layout

    /// Debug layout for a binding of an expression to a value
    val bindingL: TcGlobals -> Binding -> Layout

    /// Debug layout for an expression
    val exprL: TcGlobals -> Expr -> Layout

    /// Debug layout for a type definition
    val tyconL: TcGlobals -> Tycon -> Layout

    /// Debug layout for a decision tree
    val decisionTreeL: TcGlobals -> DecisionTree -> Layout

    /// Debug layout for an implementation file
    val implFileL: TcGlobals -> TypedImplFile -> Layout

    /// Debug layout for a list of implementation files
    val implFilesL: TcGlobals -> TypedImplFile list -> Layout

    /// Debug layout for class and record fields
    val recdFieldRefL: RecdFieldRef -> Layout

/// A set of function parameters (visitor) for folding over expressions
type ExprFolder<'State> =
    { exprIntercept: (* recurseF *) ('State -> Expr -> 'State) -> (* noInterceptF *) ('State -> Expr -> 'State) -> 'State -> Expr -> 'State
      valBindingSiteIntercept: 'State -> bool * Val -> 'State
      nonRecBindingsIntercept: 'State -> Binding -> 'State         
      recBindingsIntercept: 'State -> Bindings -> 'State         
      dtreeIntercept: 'State -> DecisionTree -> 'State
      targetIntercept: ('State -> Expr -> 'State) -> 'State -> DecisionTreeTarget -> 'State option
      tmethodIntercept: ('State -> Expr -> 'State) -> 'State -> ObjExprMethod -> 'State option}

/// The empty set of actions for folding over expressions
val ExprFolder0: ExprFolder<'State>

/// Fold over all the expressions in an implementation file
val FoldImplFile: ExprFolder<'State> -> ('State -> TypedImplFile -> 'State) 

/// Fold over all the expressions in an expression
val FoldExpr: ExprFolder<'State> -> ('State -> Expr -> 'State) 

#if DEBUG
/// Extract some statistics from an expression
val ExprStats: Expr -> string
#endif

/// Build a nativeptr type
val mkNativePtrTy: TcGlobals -> TType -> TType

/// Build a 'voidptr' type
val mkVoidPtrTy: TcGlobals -> TType

/// Build a single-dimensional array type
val mkArrayType: TcGlobals -> TType -> TType

/// Determine if a type is an option type
val isOptionTy: TcGlobals -> TType -> bool

/// Take apart an option type
val destOptionTy: TcGlobals -> TType -> TType

/// Try to take apart an option type
val tryDestOptionTy: TcGlobals -> TType -> ValueOption<TType>

/// Determine is a type is a System.Nullable type
val isNullableTy: TcGlobals -> TType -> bool

/// Try to take apart a System.Nullable type
val tryDestNullableTy: TcGlobals -> TType -> ValueOption<TType>

/// Take apart a System.Nullable type
val destNullableTy: TcGlobals -> TType -> TType

/// Determine if a type is a System.Linq.Expression type
val isLinqExpressionTy: TcGlobals -> TType -> bool

/// Take apart a System.Linq.Expression type
val destLinqExpressionTy: TcGlobals -> TType -> TType

/// Try to take apart a System.Linq.Expression type
val tryDestLinqExpressionTy: TcGlobals -> TType -> TType option

/// Determine if a type is an IDelegateEvent type
val isIDelegateEventType: TcGlobals -> TType -> bool

/// Take apart an IDelegateEvent type
val destIDelegateEventType: TcGlobals -> TType -> TType 

/// Build an IEvent type
val mkIEventType: TcGlobals -> TType -> TType -> TType

/// Build an IObservable type
val mkIObservableType: TcGlobals -> TType -> TType

/// Build an IObserver type
val mkIObserverType: TcGlobals -> TType -> TType

/// Build an Lazy type
val mkLazyTy: TcGlobals -> TType -> TType

/// Build an PrintFormat type
val mkPrintfFormatTy: TcGlobals -> TType -> TType -> TType -> TType -> TType -> TType

//-------------------------------------------------------------------------
// Classify types
//------------------------------------------------------------------------- 

/// Represents metadata extracted from a nominal type
type TypeDefMetadata = 
     | ILTypeMetadata of TILObjectReprData
     | FSharpOrArrayOrByrefOrTupleOrExnTypeMetadata 
#if !NO_EXTENSIONTYPING
     | ProvidedTypeMetadata of  TProvidedTypeInfo
#endif

/// Extract metadata from a type definition
val metadataOfTycon: Tycon -> TypeDefMetadata

/// Extract metadata from a type
val metadataOfTy: TcGlobals -> TType -> TypeDefMetadata

/// Determine if a type is the System.String type
val isStringTy: TcGlobals -> TType -> bool

/// Determine if a type is an F# list type
val isListTy: TcGlobals -> TType -> bool

/// Determine if a type is a nominal .NET type
val isILAppTy: TcGlobals -> TType -> bool

/// Determine if a type is any kind of array type
val isArrayTy: TcGlobals -> TType -> bool

/// Determine if a type is a single-dimensional array type
val isArray1DTy: TcGlobals -> TType -> bool

/// Get the element type of an array type
val destArrayTy: TcGlobals -> TType -> TType

/// Get the element type of an F# list type
val destListTy: TcGlobals -> TType -> TType

/// Build an array type of the given rank
val mkArrayTy: TcGlobals -> int -> TType -> range -> TType

/// Check if a type definition is one of the artificial type definitions used for array types of different ranks 
val isArrayTyconRef: TcGlobals -> TyconRef -> bool

/// Determine the rank of one of the artificial type definitions used for array types
val rankOfArrayTyconRef: TcGlobals -> TyconRef -> int

/// Determine if a type is the F# unit type
val isUnitTy: TcGlobals -> TType -> bool

/// Determine if a type is the System.Object type
val isObjTy: TcGlobals -> TType -> bool

/// Determine if a type is the System.ValueType type
val isValueTypeTy: TcGlobals -> TType -> bool

/// Determine if a type is the System.Void type
val isVoidTy: TcGlobals -> TType -> bool

/// Get the element type of an array type
val destArrayTy: TcGlobals -> TType -> TType

/// Get the rank of an array type
val rankOfArrayTy: TcGlobals -> TType -> int

/// Determine if a reference to a type definition is an interface type
val isInterfaceTyconRef: TyconRef -> bool

/// Determine if a type is a delegate type
val isDelegateTy: TcGlobals -> TType -> bool

/// Determine if a type is an interface type
val isInterfaceTy: TcGlobals -> TType -> bool

/// Determine if a type is a FSharpRef type
val isRefTy: TcGlobals -> TType -> bool

/// Determine if a type is a function (including generic). Not the same as isFunTy.
val isForallFunctionTy: TcGlobals -> TType -> bool

/// Determine if a type is a sealed type
val isSealedTy: TcGlobals -> TType -> bool

/// Determine if a type is a ComInterop type
val isComInteropTy: TcGlobals -> TType -> bool

/// Determine the underlying type of an enum type (normally int32)
val underlyingTypeOfEnumTy: TcGlobals -> TType -> TType

/// If the input type is an enum type, then convert to its underlying type, otherwise return the input type
val normalizeEnumTy: TcGlobals -> TType -> TType

/// Determine if a type is a struct type
val isStructTy: TcGlobals -> TType -> bool

val isStructOrEnumTyconTy: TcGlobals -> TType -> bool

/// Determine if a type is a variable type with the ': struct' constraint.
///
/// Note, isStructTy does not include type parameters with the ': struct' constraint
/// This predicate is used to detect those type parameters.
val isNonNullableStructTyparTy: TcGlobals -> TType -> bool

/// Determine if a type is a variable type with the ': not struct' constraint.
///
/// Note, isRefTy does not include type parameters with the ': not struct' constraint
/// This predicate is used to detect those type parameters.
val isReferenceTyparTy: TcGlobals -> TType -> bool

/// Determine if a type is an unmanaged type
val isUnmanagedTy: TcGlobals -> TType -> bool

/// Determine if a type is a class type
val isClassTy: TcGlobals -> TType -> bool

/// Determine if a type is an enum type
val isEnumTy: TcGlobals -> TType -> bool

/// Determine if a type is a struct, record or union type
val isStructRecordOrUnionTyconTy: TcGlobals -> TType -> bool

/// For "type Class as self", 'self' is fixed up after initialization. To support this,
/// it is converted behind the scenes to a ref. This function strips off the ref and
/// returns the underlying type.
val StripSelfRefCell: TcGlobals * ValBaseOrThisInfo * TType -> TType

/// An active pattern to determine if a type is a nominal type, possibly instantiated
val (|AppTy|_|): TcGlobals -> TType -> (TyconRef * TType list) option

/// An active pattern to match System.Nullable types
val (|NullableTy|_|): TcGlobals -> TType -> TType option

/// An active pattern to transform System.Nullable types to their input, otherwise leave the input unchanged
val (|StripNullableTy|): TcGlobals -> TType -> TType 

/// Matches any byref type, yielding the target type
val (|ByrefTy|_|): TcGlobals -> TType -> TType option

//-------------------------------------------------------------------------
// Special semantic constraints
//------------------------------------------------------------------------- 

val IsUnionTypeWithNullAsTrueValue: TcGlobals -> Tycon -> bool

val TyconHasUseNullAsTrueValueAttribute: TcGlobals -> Tycon -> bool

val CanHaveUseNullAsTrueValueAttribute: TcGlobals -> Tycon -> bool

val MemberIsCompiledAsInstance: TcGlobals -> TyconRef -> bool -> ValMemberInfo -> Attribs -> bool

val ValSpecIsCompiledAsInstance: TcGlobals -> Val -> bool

val ValRefIsCompiledAsInstanceMember: TcGlobals -> ValRef -> bool

val ModuleNameIsMangled: TcGlobals -> Attribs -> bool

val CompileAsEvent: TcGlobals -> Attribs -> bool

val TypeNullIsExtraValue: TcGlobals -> range -> TType -> bool

val TypeNullIsTrueValue: TcGlobals -> TType -> bool

val TypeNullNotLiked: TcGlobals -> range -> TType -> bool

val TypeNullNever: TcGlobals -> TType -> bool

val TypeSatisfiesNullConstraint: TcGlobals -> range -> TType -> bool

val TypeHasDefaultValue: TcGlobals -> range -> TType -> bool

val isAbstractTycon: Tycon -> bool

val isUnionCaseRefDefinitelyMutable: UnionCaseRef -> bool

val isRecdOrUnionOrStructTyconRefDefinitelyMutable: TyconRef -> bool

val isExnDefinitelyMutable: TyconRef -> bool 

val isUnionCaseFieldMutable: TcGlobals -> UnionCaseRef -> int -> bool

val isExnFieldMutable: TyconRef -> int -> bool

val isRecdOrStructTyconRefReadOnly: TcGlobals -> range -> TyconRef -> bool

val isRecdOrStructTyconRefAssumedImmutable: TcGlobals -> TyconRef -> bool

val isRecdOrStructTyReadOnly: TcGlobals -> range -> TType -> bool

val useGenuineField: Tycon -> RecdField -> bool 

val ComputeFieldName: Tycon -> RecdField -> string

//-------------------------------------------------------------------------
// Destruct slotsigs etc.
//------------------------------------------------------------------------- 

val slotSigHasVoidReturnTy: SlotSig -> bool

val actualReturnTyOfSlotSig: TypeInst -> TypeInst -> SlotSig -> TType option

val returnTyOfMethod: TcGlobals -> ObjExprMethod -> TType option

//-------------------------------------------------------------------------
// Primitives associated with initialization graphs
//------------------------------------------------------------------------- 

val mkRefCell: TcGlobals -> range -> TType -> Expr -> Expr

val mkRefCellGet: TcGlobals -> range -> TType -> Expr -> Expr

val mkRefCellSet: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

val mkLazyDelayed: TcGlobals -> range -> TType -> Expr -> Expr

val mkLazyForce: TcGlobals -> range -> TType -> Expr -> Expr

val mkRefCellContentsRef: TcGlobals -> RecdFieldRef

/// Check if a type is an FSharpRef type 
val isRefCellTy: TcGlobals -> TType -> bool

/// Get the element type of an FSharpRef type 
val destRefCellTy: TcGlobals -> TType -> TType

/// Create the FSharpRef type for a given element type
val mkRefCellTy: TcGlobals -> TType -> TType

/// Create the IEnumerable (seq) type for a given element type
val mkSeqTy: TcGlobals -> TType -> TType

/// Create the IEnumerator type for a given element type
val mkIEnumeratorTy: TcGlobals -> TType -> TType

/// Create the list type for a given element type
val mkListTy: TcGlobals -> TType -> TType

/// Create the option type for a given element type
val mkOptionTy: TcGlobals -> TType -> TType

/// Create the voption type for a given element type
val mkValueOptionTy  : TcGlobals -> TType -> TType

/// Create the Nullable type for a given element type
val mkNullableTy: TcGlobals -> TType -> TType

/// Create the union case 'None' for an option type
val mkNoneCase: TcGlobals -> UnionCaseRef

/// Create the union case 'Some(expr)' for an option type
val mkSomeCase: TcGlobals -> UnionCaseRef

/// Create the struct union case 'ValueSome(expr)' for a voption type
val mkValueSomeCase: TcGlobals -> UnionCaseRef

/// Create the struct union case 'Some' or 'ValueSome(expr)' for a voption type
val mkAnySomeCase: TcGlobals -> isStruct: bool -> UnionCaseRef

/// Create the expression '[]' for a list type
val mkNil: TcGlobals -> range -> TType -> Expr

/// Create the expression 'headExpr:: tailExpr'
val mkCons: TcGlobals -> TType -> Expr -> Expr -> Expr

/// Create the expression 'Some(expr)'
val mkSome: TcGlobals -> TType -> Expr -> range -> Expr

/// Create the expression 'None' for an option-type
val mkNone: TcGlobals -> TType -> range -> Expr

val mkOptionToNullable: TcGlobals -> range -> TType -> Expr -> Expr

val mkOptionDefaultValue: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

//-------------------------------------------------------------------------
// Make a few more expressions
//------------------------------------------------------------------------- 

val mkSequential: DebugPointAtSequential -> range -> Expr -> Expr -> Expr

val mkCompGenSequential: range -> Expr -> Expr -> Expr

val mkSequentials: DebugPointAtSequential -> TcGlobals -> range -> Exprs -> Expr   

val mkRecordExpr: TcGlobals -> RecordConstructionInfo * TyconRef * TypeInst * RecdFieldRef list * Exprs * range -> Expr

val mkUnbox: TType -> Expr -> range -> Expr

val mkBox: TType -> Expr -> range -> Expr

val mkIsInst: TType -> Expr -> range -> Expr

val mkNull: range -> TType -> Expr

val mkNullTest: TcGlobals -> range -> Expr -> Expr -> Expr -> Expr

val mkNonNullTest: TcGlobals -> range -> Expr -> Expr

val mkIsInstConditional: TcGlobals -> range -> TType -> Expr -> Val -> Expr -> Expr -> Expr

val mkThrow: range -> TType -> Expr -> Expr

val mkGetArg0: range -> TType -> Expr

val mkDefault: range * TType -> Expr

val isThrow: Expr -> bool

val mkString: TcGlobals -> range -> string -> Expr

val mkBool: TcGlobals -> range -> bool -> Expr

val mkByte: TcGlobals -> range -> byte -> Expr

val mkUInt16: TcGlobals -> range -> uint16 -> Expr

val mkTrue: TcGlobals -> range -> Expr

val mkFalse: TcGlobals -> range -> Expr

val mkUnit: TcGlobals -> range -> Expr

val mkInt32: TcGlobals -> range -> int32 -> Expr

val mkInt: TcGlobals -> range -> int -> Expr

val mkZero: TcGlobals -> range -> Expr

val mkOne: TcGlobals -> range -> Expr

val mkTwo: TcGlobals -> range -> Expr

val mkMinusOne: TcGlobals -> range -> Expr

val destInt32: Expr -> int32 option

//-------------------------------------------------------------------------
// Primitives associated with quotations
//------------------------------------------------------------------------- 
 
val isQuotedExprTy: TcGlobals -> TType -> bool

val destQuotedExprTy: TcGlobals -> TType -> TType

val mkQuotedExprTy: TcGlobals -> TType -> TType

val mkRawQuotedExprTy: TcGlobals -> TType

//-------------------------------------------------------------------------
// Primitives associated with IL code gen
//------------------------------------------------------------------------- 

val mspec_Type_GetTypeFromHandle: TcGlobals ->  ILMethodSpec

val fspec_Missing_Value: TcGlobals ->  ILFieldSpec

val mkInitializeArrayMethSpec: TcGlobals -> ILMethodSpec 

val mkByteArrayTy: TcGlobals -> TType

val mkInvalidCastExnNewobj: TcGlobals -> ILInstr


//-------------------------------------------------------------------------
// Construct calls to some intrinsic functions
//------------------------------------------------------------------------- 

val mkCallNewFormat: TcGlobals -> range -> TType -> TType -> TType -> TType -> TType -> formatStringExpr: Expr -> Expr

val mkCallUnbox: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallGetGenericComparer: TcGlobals -> range -> Expr

val mkCallGetGenericEREqualityComparer: TcGlobals -> range -> Expr

val mkCallGetGenericPEREqualityComparer: TcGlobals -> range -> Expr

val mkCallUnboxFast: TcGlobals -> range -> TType -> Expr -> Expr

val canUseUnboxFast: TcGlobals -> range -> TType -> bool

val mkCallDispose: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallSeq: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallTypeTest: TcGlobals -> range -> TType -> Expr -> Expr

val canUseTypeTestFast: TcGlobals -> TType -> bool

val mkCallTypeOf: TcGlobals -> range -> TType -> Expr

val mkCallTypeDefOf: TcGlobals -> range -> TType -> Expr 

val mkCallCreateInstance: TcGlobals -> range -> TType -> Expr

val mkCallCreateEvent: TcGlobals -> range -> TType -> TType -> Expr -> Expr -> Expr -> Expr

val mkCallArrayLength: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallArrayGet: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

val mkCallArray2DGet: TcGlobals -> range -> TType -> Expr -> Expr -> Expr -> Expr 

val mkCallArray3DGet: TcGlobals -> range -> TType -> Expr -> Expr -> Expr -> Expr -> Expr

val mkCallArray4DGet: TcGlobals -> range -> TType -> Expr -> Expr -> Expr -> Expr -> Expr -> Expr

val mkCallArraySet: TcGlobals -> range -> TType -> Expr -> Expr -> Expr -> Expr

val mkCallArray2DSet: TcGlobals -> range -> TType -> Expr -> Expr -> Expr -> Expr -> Expr

val mkCallArray3DSet: TcGlobals -> range -> TType -> Expr -> Expr -> Expr -> Expr -> Expr -> Expr

val mkCallArray4DSet: TcGlobals -> range -> TType -> Expr -> Expr -> Expr -> Expr -> Expr -> Expr -> Expr

val mkCallHash: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallBox: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallIsNull: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallIsNotNull: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallRaise: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallGenericComparisonWithComparerOuter: TcGlobals -> range -> TType -> Expr -> Expr -> Expr -> Expr

val mkCallGenericEqualityEROuter: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

val mkCallGenericEqualityWithComparerOuter: TcGlobals -> range -> TType -> Expr -> Expr -> Expr -> Expr

val mkCallGenericHashWithComparerOuter: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

val mkCallEqualsOperator: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

val mkCallNotEqualsOperator: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

val mkCallLessThanOperator: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

val mkCallLessThanOrEqualsOperator: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

val mkCallGreaterThanOperator: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

val mkCallGreaterThanOrEqualsOperator: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

val mkCallAdditionOperator: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

val mkCallSubtractionOperator: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

val mkCallMultiplyOperator: TcGlobals -> range -> ty1: TType -> ty2: TType -> rty: TType -> Expr -> Expr -> Expr

val mkCallDivisionOperator: TcGlobals -> range -> ty1: TType -> ty2: TType -> rty: TType -> Expr -> Expr -> Expr

val mkCallModulusOperator: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

val mkCallDefaultOf: TcGlobals -> range -> TType -> Expr

val mkCallBitwiseAndOperator: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

val mkCallBitwiseOrOperator: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

val mkCallBitwiseXorOperator: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

val mkCallShiftLeftOperator: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

val mkCallShiftRightOperator: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

val mkCallUnaryNegOperator: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallUnaryNotOperator: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallAdditionChecked: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

val mkCallSubtractionChecked: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

val mkCallMultiplyChecked: TcGlobals -> range -> ty1: TType -> ty2: TType -> rty: TType -> Expr -> Expr -> Expr

val mkCallUnaryNegChecked: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToByteChecked: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToSByteChecked: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToInt16Checked: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToUInt16Checked: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToIntChecked: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToInt32Checked: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToUInt32Checked: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToInt64Checked: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToUInt64Checked: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToIntPtrChecked: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToUIntPtrChecked: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToByteOperator: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToSByteOperator: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToInt16Operator: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToUInt16Operator: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToIntOperator: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToInt32Operator: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToUInt32Operator: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToInt64Operator: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToUInt64Operator: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToSingleOperator: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToDoubleOperator: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToIntPtrOperator: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToUIntPtrOperator: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToCharOperator: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallToEnumOperator: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallDeserializeQuotationFSharp20Plus: TcGlobals -> range -> Expr -> Expr -> Expr -> Expr -> Expr

val mkCallDeserializeQuotationFSharp40Plus: TcGlobals -> range -> Expr -> Expr -> Expr -> Expr -> Expr -> Expr

val mkCallCastQuotation: TcGlobals -> range -> TType -> Expr -> Expr 

val mkCallLiftValueWithName: TcGlobals -> range -> TType -> string -> Expr -> Expr

val mkCallLiftValue: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallLiftValueWithDefn: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallSeqCollect: TcGlobals -> range -> TType -> TType -> Expr -> Expr -> Expr

val mkCallSeqUsing: TcGlobals -> range -> TType -> TType -> Expr -> Expr -> Expr

val mkCallSeqDelay: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallSeqAppend: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

val mkCallSeqFinally: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

val mkCallSeqGenerated: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

val mkCallSeqOfFunctions: TcGlobals -> range -> TType -> TType -> Expr -> Expr -> Expr -> Expr

val mkCallSeqToArray: TcGlobals -> range -> TType -> Expr -> Expr 

val mkCallSeqToList: TcGlobals -> range -> TType -> Expr -> Expr 

val mkCallSeqMap: TcGlobals -> range -> TType -> TType -> Expr -> Expr -> Expr

val mkCallSeqSingleton: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallSeqEmpty: TcGlobals -> range -> TType -> Expr

/// Make a call to the 'isprintf' function for string interpolation
val mkCall_sprintf: g: TcGlobals -> m: range -> funcTy: TType -> fmtExpr: Expr -> fillExprs: Expr list -> Expr

val mkILAsmCeq: TcGlobals -> range -> Expr -> Expr -> Expr

val mkILAsmClt: TcGlobals -> range -> Expr -> Expr -> Expr

val mkCallFailInit: TcGlobals -> range -> Expr 

val mkCallFailStaticInit: TcGlobals -> range -> Expr 

val mkCallCheckThis: TcGlobals -> range -> TType -> Expr -> Expr 

val mkCase: DecisionTreeTest * DecisionTree -> DecisionTreeCase

val mkCallQuoteToLinqLambdaExpression: TcGlobals -> range -> TType -> Expr -> Expr

val mkCallGetQuerySourceAsEnumerable: TcGlobals -> range -> TType -> TType -> Expr -> Expr

val mkCallNewQuerySource: TcGlobals -> range -> TType -> TType -> Expr -> Expr

val mkArray: TType * Exprs * range -> Expr

val mkStaticCall_String_Concat2: TcGlobals -> range -> Expr -> Expr -> Expr

val mkStaticCall_String_Concat3: TcGlobals -> range -> Expr -> Expr -> Expr -> Expr

val mkStaticCall_String_Concat4: TcGlobals -> range -> Expr -> Expr -> Expr -> Expr -> Expr

val mkStaticCall_String_Concat_Array: TcGlobals -> range -> Expr -> Expr

/// Use a witness in BuiltInWitnesses
val tryMkCallBuiltInWitness: TcGlobals -> TraitConstraintInfo -> Expr list -> range -> Expr option

/// Use an operator as a witness 
val tryMkCallCoreFunctionAsBuiltInWitness: TcGlobals -> IntrinsicValRef -> TType list -> Expr list -> range -> Expr option

//-------------------------------------------------------------------------
// operations primarily associated with the optimization to fix
// up loops to generate .NET code that does not include array bound checks
//------------------------------------------------------------------------- 

val mkDecr: TcGlobals -> range -> Expr -> Expr

val mkIncr: TcGlobals -> range -> Expr -> Expr

val mkLdlen: TcGlobals -> range -> Expr -> Expr

val mkLdelem: TcGlobals -> range -> TType -> Expr -> Expr -> Expr

//-------------------------------------------------------------------------
// Analyze attribute sets 
//------------------------------------------------------------------------- 

val TryDecodeILAttribute: ILTypeRef -> ILAttributes -> (ILAttribElem list * ILAttributeNamedArg list) option

val TryFindILAttribute: BuiltinAttribInfo -> ILAttributes -> bool

val TryFindILAttributeOpt: BuiltinAttribInfo option -> ILAttributes -> bool

val IsMatchingFSharpAttribute: TcGlobals -> BuiltinAttribInfo -> Attrib -> bool

val IsMatchingFSharpAttributeOpt: TcGlobals -> BuiltinAttribInfo option -> Attrib -> bool

val HasFSharpAttribute: TcGlobals -> BuiltinAttribInfo -> Attribs -> bool

val HasFSharpAttributeOpt: TcGlobals -> BuiltinAttribInfo option -> Attribs -> bool

val TryFindFSharpAttribute: TcGlobals -> BuiltinAttribInfo -> Attribs -> Attrib option

val TryFindFSharpAttributeOpt: TcGlobals -> BuiltinAttribInfo option -> Attribs -> Attrib option

val TryFindFSharpBoolAttribute: TcGlobals -> BuiltinAttribInfo -> Attribs -> bool option

val TryFindFSharpBoolAttributeAssumeFalse: TcGlobals -> BuiltinAttribInfo -> Attribs -> bool option

val TryFindFSharpStringAttribute: TcGlobals -> BuiltinAttribInfo -> Attribs -> string option

val TryFindFSharpInt32Attribute: TcGlobals -> BuiltinAttribInfo -> Attribs -> int32 option

/// Try to find a specific attribute on a type definition, where the attribute accepts a string argument.
///
/// This is used to detect the 'DefaultMemberAttribute' and 'ConditionalAttribute' attributes (on type definitions)
val TryFindTyconRefStringAttribute: TcGlobals -> range -> BuiltinAttribInfo -> TyconRef -> string option

/// Try to find a specific attribute on a type definition, where the attribute accepts a bool argument.
val TryFindTyconRefBoolAttribute: TcGlobals -> range -> BuiltinAttribInfo -> TyconRef -> bool option

/// Try to find a specific attribute on a type definition
val TyconRefHasAttribute: TcGlobals -> range -> BuiltinAttribInfo -> TyconRef -> bool

/// Try to find the AttributeUsage attribute, looking for the value of the AllowMultiple named parameter
val TryFindAttributeUsageAttribute: TcGlobals -> range -> TyconRef -> bool option

#if !NO_EXTENSIONTYPING
/// returns Some(assemblyName) for success
val TryDecodeTypeProviderAssemblyAttr: ILAttribute -> string option
#endif

val IsSignatureDataVersionAttr: ILAttribute -> bool

val TryFindAutoOpenAttr: ILAttribute -> string option 

val TryFindInternalsVisibleToAttr: ILAttribute -> string option 

val IsMatchingSignatureDataVersionAttr: ILVersionInfo -> ILAttribute -> bool

val mkCompilationMappingAttr: TcGlobals -> int -> ILAttribute
val mkCompilationMappingAttrWithSeqNum: TcGlobals -> int -> int -> ILAttribute


val mkCompilationMappingAttrWithVariantNumAndSeqNum: TcGlobals -> int -> int -> int -> ILAttribute

val mkCompilationMappingAttrForQuotationResource: TcGlobals -> string * ILTypeRef list -> ILAttribute

val mkCompilationArgumentCountsAttr: TcGlobals -> int list -> ILAttribute

val mkCompilationSourceNameAttr: TcGlobals -> string -> ILAttribute

val mkSignatureDataVersionAttr: TcGlobals -> ILVersionInfo -> ILAttribute

val mkCompilerGeneratedAttr: TcGlobals -> int -> ILAttribute

//-------------------------------------------------------------------------
// More common type construction
//------------------------------------------------------------------------- 

val isInByrefTy: TcGlobals -> TType -> bool

val isOutByrefTy: TcGlobals -> TType -> bool

val isByrefTy: TcGlobals -> TType -> bool

val isNativePtrTy: TcGlobals -> TType -> bool

val destByrefTy: TcGlobals -> TType -> TType

val destNativePtrTy: TcGlobals -> TType -> TType

val isByrefTyconRef: TcGlobals -> TyconRef -> bool

val isByrefLikeTyconRef: TcGlobals -> range -> TyconRef -> bool

val isSpanLikeTyconRef: TcGlobals -> range -> TyconRef -> bool

val isByrefLikeTy: TcGlobals -> range -> TType -> bool

/// Check if the type is a byref-like but not a byref.
val isSpanLikeTy: TcGlobals -> range -> TType -> bool

val isSpanTy: TcGlobals -> range -> TType -> bool

val tryDestSpanTy: TcGlobals -> range -> TType -> struct(TyconRef * TType) voption

val destSpanTy: TcGlobals -> range -> TType -> struct(TyconRef * TType)

val isReadOnlySpanTy: TcGlobals -> range -> TType -> bool

val tryDestReadOnlySpanTy: TcGlobals -> range -> TType -> struct(TyconRef * TType) voption

val destReadOnlySpanTy: TcGlobals -> range -> TType -> struct(TyconRef * TType)

//-------------------------------------------------------------------------
// Tuple constructors/destructors
//------------------------------------------------------------------------- 

val isRefTupleExpr: Expr -> bool

val tryDestRefTupleExpr: Expr -> Exprs

val mkAnyTupledTy: TcGlobals -> TupInfo -> TType list -> TType

val mkAnyTupled: TcGlobals -> range -> TupInfo -> Exprs -> TType list -> Expr 

val mkRefTupled: TcGlobals -> range -> Exprs -> TType list -> Expr 

val mkRefTupledNoTypes: TcGlobals -> range -> Exprs -> Expr 

val mkRefTupledTy: TcGlobals -> TType list -> TType

val mkRefTupledVarsTy: TcGlobals -> Val list -> TType

val mkRefTupledVars: TcGlobals -> range -> Val list -> Expr 

val mkMethodTy: TcGlobals -> TType list list -> TType -> TType

val mkAnyAnonRecdTy: TcGlobals -> AnonRecdTypeInfo -> TType list -> TType

val mkAnonRecd: TcGlobals -> range -> AnonRecdTypeInfo -> Ident[] -> Exprs -> TType list -> Expr 

val AdjustValForExpectedArity: TcGlobals -> range -> ValRef -> ValUseFlag -> ValReprInfo -> Expr * TType

val AdjustValToTopVal: Val -> ParentRef -> ValReprInfo -> unit

val LinearizeTopMatch: TcGlobals -> ParentRef -> Expr -> Expr

val AdjustPossibleSubsumptionExpr: TcGlobals -> Expr -> Exprs -> (Expr * Exprs) option

val NormalizeAndAdjustPossibleSubsumptionExprs: TcGlobals -> Expr -> Expr

//-------------------------------------------------------------------------
// XmlDoc signatures, used by both VS mode and XML-help emit
//------------------------------------------------------------------------- 

val buildAccessPath: CompilationPath option -> string

val XmlDocArgsEnc: TcGlobals -> Typars * Typars -> TType list -> string

val XmlDocSigOfVal: TcGlobals -> full: bool ->  string -> Val -> string

val XmlDocSigOfUnionCase: (string list -> string)

val XmlDocSigOfField: (string list -> string)

val XmlDocSigOfProperty: (string list -> string)

val XmlDocSigOfTycon: (string list -> string)

val XmlDocSigOfSubModul: (string list -> string)

val XmlDocSigOfEntity: EntityRef -> string

//---------------------------------------------------------------------------
// Resolve static optimizations
//------------------------------------------------------------------------- 

type StaticOptimizationAnswer = 
    | Yes = 1y
    | No = -1y
    | Unknown = 0y

val DecideStaticOptimizations: TcGlobals -> StaticOptimization list -> haveWitnesses: bool -> StaticOptimizationAnswer

val mkStaticOptimizationExpr: TcGlobals -> StaticOptimization list * Expr * Expr * range -> Expr

/// Build for loops
val mkFastForLoop: TcGlobals -> DebugPointAtFor * range * Val * Expr * bool * Expr * Expr -> Expr

//---------------------------------------------------------------------------
// Active pattern helpers
//------------------------------------------------------------------------- 

type ActivePatternElemRef with 
    member Name: string

val TryGetActivePatternInfo: ValRef -> PrettyNaming.ActivePatternInfo option

val mkChoiceCaseRef: g: TcGlobals -> m: range -> n: int -> i: int -> UnionCaseRef

type PrettyNaming.ActivePatternInfo with 

    member Names: string list 

    member ResultType: g: TcGlobals -> range -> TType list -> bool -> TType

    member OverallType: g: TcGlobals -> m: range -> dty: TType -> rtys: TType list -> isStruct: bool -> TType

val doesActivePatternHaveFreeTypars: TcGlobals -> ValRef -> bool

//---------------------------------------------------------------------------
// Structural rewrites
//------------------------------------------------------------------------- 

[<NoEquality; NoComparison>]
type ExprRewritingEnv = 
    { PreIntercept: ((Expr -> Expr) -> Expr -> Expr option) option
      PostTransform: Expr -> Expr option
      PreInterceptBinding: ((Expr -> Expr) -> Binding -> Binding option) option
      IsUnderQuotations: bool }    

val RewriteExpr: ExprRewritingEnv -> Expr -> Expr

val RewriteImplFile: ExprRewritingEnv -> TypedImplFile -> TypedImplFile

val IsGenericValWithGenericConstraints: TcGlobals -> Val -> bool

type Entity with 

    member HasInterface: TcGlobals -> TType -> bool

    member HasOverride: TcGlobals -> string -> TType list -> bool

    member HasMember: TcGlobals -> string -> TType list -> bool

type EntityRef with 

    member HasInterface: TcGlobals -> TType -> bool

    member HasOverride: TcGlobals -> string -> TType list -> bool

    member HasMember: TcGlobals -> string -> TType list -> bool

val (|AttribBitwiseOrExpr|_|): TcGlobals -> Expr -> (Expr * Expr) option

val (|EnumExpr|_|): TcGlobals -> Expr -> Expr option

val (|TypeOfExpr|_|): TcGlobals -> Expr -> TType option

val (|TypeDefOfExpr|_|): TcGlobals -> Expr -> TType option

val isNameOfValRef: TcGlobals -> ValRef -> bool

val (|NameOfExpr|_|): TcGlobals -> Expr -> TType option

val (|SeqExpr|_|): TcGlobals -> Expr -> unit option

val EvalLiteralExprOrAttribArg: TcGlobals -> Expr -> Expr

val EvaledAttribExprEquality: TcGlobals -> Expr -> Expr -> bool

val IsSimpleSyntacticConstantExpr: TcGlobals -> Expr -> bool

val (|ConstToILFieldInit|_|): Const -> ILFieldInit option

val (|ExtractAttribNamedArg|_|): string -> AttribNamedArg list -> AttribExpr option 

val (|AttribInt32Arg|_|): AttribExpr -> int32 option

val (|AttribInt16Arg|_|): AttribExpr -> int16 option

val (|AttribBoolArg|_|): AttribExpr -> bool option

val (|AttribStringArg|_|): AttribExpr -> string option

val (|Int32Expr|_|): Expr -> int32 option

/// Determines types that are potentially known to satisfy the 'comparable' constraint and returns
/// a set of residual types that must also satisfy the constraint
val (|SpecialComparableHeadType|_|): TcGlobals -> TType -> TType list option

val (|SpecialEquatableHeadType|_|): TcGlobals -> TType -> TType list option

val (|SpecialNotEquatableHeadType|_|): TcGlobals -> TType -> unit option

type OptimizeForExpressionOptions = OptimizeIntRangesOnly | OptimizeAllForExpressions

val DetectAndOptimizeForExpression: TcGlobals -> OptimizeForExpressionOptions -> Expr -> Expr

val TryEliminateDesugaredConstants: TcGlobals -> range -> Const -> Expr option

val MemberIsExplicitImpl: TcGlobals -> ValMemberInfo -> bool

val ValIsExplicitImpl: TcGlobals -> Val -> bool

val ValRefIsExplicitImpl: TcGlobals -> ValRef -> bool

val (|LinearMatchExpr|_|): Expr -> (DebugPointAtBinding * range * DecisionTree * DecisionTreeTarget * Expr * DebugPointForTarget * range * TType) option

val rebuildLinearMatchExpr: (DebugPointAtBinding * range * DecisionTree * DecisionTreeTarget * Expr * DebugPointForTarget * range * TType) -> Expr

val (|LinearOpExpr|_|): Expr -> (TOp * TypeInst * Expr list * Expr * range) option

val rebuildLinearOpExpr: (TOp * TypeInst * Expr list * Expr * range) -> Expr

val mkCoerceIfNeeded: TcGlobals -> tgtTy: TType -> srcTy: TType -> Expr -> Expr

val (|InnerExprPat|): Expr -> Expr

val allValsOfModDef: ModuleOrNamespaceExpr -> seq<Val>

val BindUnitVars: TcGlobals -> (Val list * ArgReprInfo list * Expr) -> Val list * Expr

val isThreadOrContextStatic: TcGlobals -> Attrib list -> bool

val mkUnitDelayLambda: TcGlobals -> range -> Expr -> Expr

val GenWitnessArgTys: TcGlobals -> TraitWitnessInfo -> TType list list

val GenWitnessTys: TcGlobals -> TraitWitnessInfos -> TType list

val GenWitnessTy: TcGlobals -> TraitWitnessInfo -> TType 

val GetTraitConstraintInfosOfTypars: TcGlobals -> Typars -> TraitConstraintInfo list

val GetTraitWitnessInfosOfTypars: TcGlobals -> numParentTypars: int -> typars: Typars -> TraitWitnessInfos

/// An immutable mappping from witnesses to some data.
///
/// Note: this uses an immutable HashMap/Dictionary with an IEqualityComparer that captures TcGlobals, see EmptyTraitWitnessInfoHashMap
type TraitWitnessInfoHashMap<'T> = ImmutableDictionary<TraitWitnessInfo, 'T>

/// Create an empty immutable mapping from witnesses to some data
val EmptyTraitWitnessInfoHashMap: TcGlobals -> TraitWitnessInfoHashMap<'T>

/// Match expressions that are an application of a particular F# function value
val (|ValApp|_|): TcGlobals -> ValRef -> Expr -> (TypeInst * Exprs * range) option

val CombineCcuContentFragments: range -> ModuleOrNamespaceType list -> ModuleOrNamespaceType

/// Recognise a while expression
val (|WhileExpr|_|): Expr -> (DebugPointAtWhile * SpecialWhileLoopMarker * Expr * Expr * range) option

/// Recognise a for-loop expression
val (|ForLoopExpr|_|): Expr -> (DebugPointAtFor * ForLoopStyle * Expr * Expr * Val * Expr * range) option

/// Recognise a try-with expression
val (|TryWithExpr|_|): Expr -> (DebugPointAtTry * DebugPointAtWith * TType * Expr * Val * Expr * Val * Expr * range) option

/// Recognise a try-finally expression
val (|TryFinallyExpr|_|): Expr -> (DebugPointAtTry * DebugPointAtFinally * TType * Expr * Expr * range) option

/// Add a label to use as the target for a goto
val mkLabelled: range -> ILCodeLabel -> Expr -> Expr 

/// Shared helper for binding attributes
val TryBindTyconRefAttribute:
    g:TcGlobals ->
    m:range -> 
    BuiltinAttribInfo ->
    tcref:TyconRef ->
    f1:(ILAttribElem list * ILAttributeNamedArg list -> 'a option) ->
    f2:(Attrib -> 'a option) ->
    f3:(obj option list * (string * obj option) list -> 'a option) 
    -> 'a option
