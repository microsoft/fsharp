// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Logic associated with resolving method calls.
module internal FSharp.Compiler.MethodCalls

open Internal.Utilities

open FSharp.Compiler 
open FSharp.Compiler.AccessibilityLogic
open FSharp.Compiler.Import
open FSharp.Compiler.InfoReader
open FSharp.Compiler.Infos
open FSharp.Compiler.NameResolution
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.TypedTree

#if !NO_EXTENSIONTYPING
open FSharp.Compiler.ExtensionTyping
#endif

/// In the following, 'T gets instantiated to: 
///   1. the expression being supplied for an argument 
///   2. "unit", when simply checking for the existence of an overload that satisfies 
///      a signature, or when finding the corresponding witness. 
/// Note the parametricity helps ensure that overload resolution doesn't depend on the 
/// expression on the callside (though it is in some circumstances allowed 
/// to depend on some type information inferred syntactically from that 
/// expression, e.g. a lambda expression may be converted to a delegate as 
/// an adhoc conversion. 
///
/// The bool indicates if named using a '?', making the caller argument explicit-optional
type CallerArg<'T> =
    | CallerArg of ty: TType * range: range * isOpt: bool * exprInfo: 'T

    member CallerArgumentType: TType

    member Expr: 'T

    member IsExplicitOptional: bool

    member Range: range
  
type CalledArg =
    { Position: struct (int * int)
      IsParamArray: bool
      OptArgInfo: OptionalArgInfo
      CallerInfo: CallerInfo
      IsInArg: bool
      IsOutArg: bool
      ReflArgInfo: ReflectedArgInfo
      NameOpt: Ident option
      CalledArgumentType: TType }

val CalledArg: pos:struct (int * int) * isParamArray:bool * optArgInfo:OptionalArgInfo * callerInfo:CallerInfo * isInArg:bool * isOutArg:bool * nameOpt:Ident option * reflArgInfo:ReflectedArgInfo * calledArgTy:TType -> CalledArg

/// Represents a match between a caller argument and a called argument, arising from either
/// a named argument or an unnamed argument.
type AssignedCalledArg<'T> =
    { /// The identifier for a named argument, if any
      NamedArgIdOpt : Ident option

      /// The called argument in the method
      CalledArg: CalledArg 

      /// The argument on the caller side
      CallerArg: CallerArg<'T> }
    member Position: struct (int * int)
  
/// Represents the possibilities for a named-setter argument (a property, field, or a record field setter)
type AssignedItemSetterTarget =
    | AssignedPropSetter of PropInfo * MethInfo * TypeInst
    | AssignedILFieldSetter of ILFieldInfo
    | AssignedRecdFieldSetter of RecdFieldInfo

/// Represents the resolution of a caller argument as a named-setter argument
type AssignedItemSetter<'T> =
    | AssignedItemSetter of
      Ident * AssignedItemSetterTarget * CallerArg<'T>

type CallerNamedArg<'T> =
    | CallerNamedArg of Ident * CallerArg<'T>
    member CallerArg: CallerArg<'T>
    member Ident: Ident
    member Name: string
  
/// Represents the list of unnamed / named arguments at method call site
/// remark: The usage of list list is due to tupling and currying of arguments,
/// stemming from SynValInfo in the AST.
[<StructAttribute>]
type CallerArgs<'T> =
    { Unnamed: CallerArg<'T> list list
      Named: CallerNamedArg<'T> list list }
    member ArgumentNamesAndTypes: (string option * TType) list
    member CallerArgCounts: int * int
    member CurriedCallerArgs: (CallerArg<'T> list * CallerNamedArg<'T> list) list
    static member Empty: CallerArgs<'T>
  
/// F# supports some adhoc conversions at method callsites
val AdjustCalledArgType: infoReader:InfoReader -> isConstraint:bool -> enforceNullableOptionalsKnownTypes:bool -> calledArg:CalledArg -> callerArg:CallerArg<'a> -> TType

type CalledMethArgSet<'T> =
    { /// The called arguments corresponding to "unnamed" arguments
      UnnamedCalledArgs : CalledArg list

      /// Any unnamed caller arguments not otherwise assigned 
      UnnamedCallerArgs :  CallerArg<'T> list

      /// The called "ParamArray" argument, if any
      ParamArrayCalledArgOpt : CalledArg option 

      /// Any unnamed caller arguments assigned to a "param array" argument
      ParamArrayCallerArgs : CallerArg<'T> list

      /// Named args
      AssignedNamedArgs: AssignedCalledArg<'T> list  }

    member NumAssignedNamedArgs: int

    member NumUnnamedCalledArgs: int

    member NumUnnamedCallerArgs: int
  
/// Represents the syntactic matching between a caller of a method and the called method.
///
/// The constructor takes all the information about the caller and called side of a method, match up named arguments, property setters etc.,
/// and returns a CalledMeth object for further analysis.
type CalledMeth<'T> =

    new: infoReader:InfoReader *
         nameEnv:NameResolutionEnv option *
         isCheckingAttributeCall:bool *
         freshenMethInfo:(range -> MethInfo -> TypeInst) *
         m:range *
         ad:AccessorDomain *
         minfo:MethInfo *
         calledTyArgs:TType list *
         callerTyArgs:TType list *
         pinfoOpt:PropInfo option *
         callerObjArgTys:TType list *
         callerArgs:CallerArgs<'T> *
         allowParamArgs:bool *
         allowOutAndOptArgs:bool *
         tyargsOpt:TType option -> CalledMeth<'T>
    static member GetMethod: x:CalledMeth<'T> -> MethInfo
    member CalledObjArgTys: m:range -> TType list
    member GetParamArrayElementType: unit -> TType
    member HasCorrectObjArgs: m:range -> bool
    member IsAccessible: m:range * ad:AccessorDomain -> bool
    member IsCandidate: m:range * ad:AccessorDomain -> bool
    member AllCalledArgs: CalledArg list list
    member AllUnnamedCalledArgs: CalledArg list

    /// The argument analysis for each set of curried arguments
    member ArgSets: CalledMethArgSet<'T> list

    /// Named setters
    member AssignedItemSetters: AssignedItemSetter<'T> list
    member AssignedNamedArgs: AssignedCalledArg<'T> list list
    member AssignedUnnamedArgs: AssignedCalledArg<'T> list list
    member AssignsAllNamedArgs: bool

    /// The property related to the method we're attempting to call, if any  
    member AssociatedPropertyInfo: PropInfo option

    /// Args assigned to specify values for attribute fields and properties (these are not necessarily "property sets")
    member AttributeAssignedNamedArgs: CallerNamedArg<'T> list

    /// The return type after implicit deference of byref returns is taken into account
    member CalledReturnTypeAfterByrefDeref: TType

    /// Return type after tupling of out args is taken into account
    member CalledReturnTypeAfterOutArgTupling: TType

    /// The instantiation of the method we're attempting to call 
    member CalledTyArgs: TType list

    /// The instantiation of the method we're attempting to call 
    member CalledTyparInst: TypedTreeOps.TyparInst

    /// The types of the actual object arguments, if any
    member CallerObjArgTys: TType list

    /// The formal instantiation of the method we're attempting to call 
    member CallerTyArgs: TType list
    member HasCorrectArity: bool
    member HasCorrectGenericArity: bool
    member HasOptArgs: bool
    member HasOutArgs: bool

    /// The method we're attempting to call 
    member Method: MethInfo
    member NumArgSets: int
    member NumAssignedProps: int
    member NumCalledTyArgs: int
    member NumCallerTyArgs: int
    member ParamArrayCalledArgOpt: CalledArg option
    member ParamArrayCallerArgs: CallerArg<'T> list option
    member TotalNumAssignedNamedArgs: int
    member TotalNumUnnamedCalledArgs: int
    member TotalNumUnnamedCallerArgs: int

    /// Unassigned args
    member UnassignedNamedArgs: CallerNamedArg<'T> list

    /// Unnamed called optional args: pass defaults for these
    member UnnamedCalledOptArgs: CalledArg list

    /// Unnamed called out args: return these as part of the return tuple
    member UnnamedCalledOutArgs: CalledArg list
    member UsesParamArrayConversion: bool
    member amap: ImportMap
    member infoReader: InfoReader
  
val NamesOfCalledArgs: calledArgs:CalledArg list -> Ident list

type ArgumentAnalysis =
    | NoInfo
    | ArgDoesNotMatch
    | CallerLambdaHasArgTypes of TType list
    | CalledArgMatchesType of TType

val ExamineMethodForLambdaPropagation: x:CalledMeth<SynExpr> -> (ArgumentAnalysis list list * (Ident * ArgumentAnalysis) list list) option

/// Is this a 'base' call
val IsBaseCall: objArgs:Expr list -> bool

val BuildILMethInfoCall: g:TcGlobals -> amap:ImportMap -> m:range -> isProp:bool -> minfo:ILMethInfo -> valUseFlags:ValUseFlag -> minst:TType list -> direct:bool -> args:Exprs -> Expr * TType

/// Make a call to a method info. Used by the optimizer and code generator to build 
/// calls to the type-directed solutions to member constraints.
val MakeMethInfoCall: amap:ImportMap -> m:range -> minfo:MethInfo -> minst:TType list -> args:Exprs -> Expr

/// Build an expression that calls a given method info. 
/// This is called after overload resolution, and also to call other 
/// methods such as 'setters' for properties. 
//   tcVal: used to convert an F# value into an expression. See tc.fs. 
//   isProp: is it a property get? 
//   minst: the instantiation to apply for a generic method 
//   objArgs: the 'this' argument, if any 
//   args: the arguments, if any 
val BuildMethodCall: 
    tcVal:(ValRef -> ValUseFlag -> TType list -> range -> Expr * TType) ->
    g:TcGlobals ->
    amap:ImportMap ->
    isMutable:TypedTreeOps.Mutates ->
    m:range ->
    isProp:bool ->
    minfo:MethInfo ->
    valUseFlags:ValUseFlag ->
    minst:TType list ->
    objArgs:Expr list ->
    args:Expr list
        -> Expr * TType

/// Build a call to the System.Object constructor taking no arguments,
val BuildObjCtorCall: g:TcGlobals -> m:range -> Expr

/// Implements the elaborated form of adhoc conversions from functions to delegates at member callsites
val BuildNewDelegateExpr: eventInfoOpt:EventInfo option * g:TcGlobals * amap:ImportMap * traitCtxt: ITraitContext option * delegateTy:TType * invokeMethInfo:MethInfo * delArgTys:TType list * f:Expr * fty:TType * m:range -> Expr

val CoerceFromFSharpFuncToDelegate: g:TcGlobals -> amap:ImportMap -> traitCtxt: ITraitContext option -> infoReader:InfoReader -> ad:AccessorDomain -> callerArgTy:TType -> m:range -> callerArgExpr:Expr -> delegateTy:TType -> Expr

val AdjustCallerArgExprForCoercions: g:TcGlobals -> amap:ImportMap -> traitCtxt: ITraitContext option -> infoReader:InfoReader -> ad:AccessorDomain -> isOutArg:bool -> calledArgTy:TType -> reflArgInfo:ReflectedArgInfo -> callerArgTy:TType -> m:range -> callerArgExpr:Expr -> 'a option * Expr

/// Build the argument list for a method call. Adjust for param array, optional arguments, byref arguments and coercions.
/// For example, if you pass an F# reference cell to a byref then we must get the address of the 
/// contents of the ref. Likewise lots of adjustments are made for optional arguments etc.
val AdjustCallerArgs:
    tcFieldInit:(range -> AbstractIL.IL.ILFieldInit -> Const) ->
    eCallerMemberName:string option ->
    infoReader:InfoReader ->
    ad:AccessorDomain ->
    traitCtxt: ITraitContext option ->
    calledMeth:CalledMeth<Expr> ->
    objArgs:Expr list ->
    lambdaVars:'a option ->
    mItem:range ->
    mMethExpr:range ->
       (Expr -> Expr) * Expr list *
       'b option list * AssignedCalledArg<Expr> list *
       Expr list * (Expr -> Expr) *
       'c option list * Expr list *
       Binding list

val RecdFieldInstanceChecks: g:TcGlobals -> amap:ImportMap -> ad:AccessorDomain -> m:range -> rfinfo:RecdFieldInfo -> unit

val ILFieldStaticChecks: g:TcGlobals -> amap:ImportMap -> infoReader:InfoReader -> ad:AccessorDomain -> m:range -> finfo:ILFieldInfo -> unit

val ILFieldInstanceChecks: g:TcGlobals -> amap:ImportMap -> ad:AccessorDomain -> m:range -> finfo:ILFieldInfo -> unit

val MethInfoChecks: g:TcGlobals -> amap:ImportMap -> isInstance:bool -> tyargsOpt:'a option -> objArgs:Expr list -> ad:AccessorDomain -> m:range -> minfo:MethInfo -> unit

exception FieldNotMutable of TypedTreeOps.DisplayEnv * RecdFieldRef * range

val CheckRecdFieldMutation: m:range -> denv:TypedTreeOps.DisplayEnv -> rfinfo:RecdFieldInfo -> unit

/// Generate a witness for the given (solved) constraint.  Five possiblilities are taken
/// into account.
///   1. The constraint is solved by a .NET-declared method or an F#-declared method
///   2. The constraint is solved by an F# record field
///   3. The constraint is solved by an F# anonymous record field
///   4. The constraint is considered solved by a "built in" solution
///   5. The constraint is solved by a closed expression given by a provided method from a type provider
/// 
/// In each case an expression is returned where the method is applied to the given arguments, or the
/// field is dereferenced.
/// 
/// None is returned in the cases where the trait has not been solved (e.g. is part of generic code)
/// or there is an unexpected mismatch of some kind.
val GenWitnessExpr: amap:ImportMap -> g:TcGlobals -> m:range -> traitInfo:TraitConstraintInfo -> argExprs:Expr list -> Expr option

/// Generate a lambda expression for the given solved trait.
val GenWitnessExprLambda: amap:ImportMap -> g:TcGlobals -> m:range -> traitInfo:TraitConstraintInfo -> Choice<TraitConstraintInfo,Expr>

/// Generate the arguments passed for a set of (solved) traits in non-generic code
val GenWitnessArgs: amap:ImportMap -> g:TcGlobals -> m:range -> traitInfos:TraitConstraintInfo list -> Choice<TraitConstraintInfo,Expr> list

#if !NO_EXTENSIONTYPING
module ProvidedMethodCalls =
  val BuildInvokerExpressionForProvidedMethodCall:
      tcVal:(ValRef -> ValUseFlag -> TType list -> range -> Expr * TType) ->
      g:TcGlobals *
      amap:ImportMap *
      traitCtxt: ITraitContext option *
      mi:Tainted<ProvidedMethodBase> *
      objArgs:Expr list *
      mut:TypedTreeOps.Mutates *
      isProp:bool *
      isSuperInit:ValUseFlag *
      allArgs:Exprs *
      m:range ->
          Tainted<ProvidedMethodInfo> option * Expr * TType
#endif
