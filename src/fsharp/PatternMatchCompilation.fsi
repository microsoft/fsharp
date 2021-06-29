// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.PatternMatchCompilation

open FSharp.Compiler
open FSharp.Compiler.AbstractIL.IL 
open FSharp.Compiler.InfoReader
open FSharp.Compiler.Syntax.PrettyNaming
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeOps

/// What should the decision tree contain for any incomplete match? 
type ActionOnFailure = 
    | ThrowIncompleteMatchException 
    | IgnoreWithWarning 
    | Throw 
    | Rethrow 
    | FailFilter

/// Represents the typechecked, elaborated form of a pattern, prior to pattern-match compilation.
[<NoEquality; NoComparison>]
type Pattern =
    | TPat_const of Const * range
    | TPat_wild of range
    | TPat_as of Pattern * PatternValBinding * range
    | TPat_disjs of Pattern list * range
    | TPat_conjs of Pattern list * range
    | TPat_query of (Expr * TType list * bool * (ValRef * TypeInst) option * int * ActivePatternInfo) * Pattern * range
    | TPat_unioncase of UnionCaseRef * TypeInst * Pattern list * range
    | TPat_exnconstr of TyconRef * Pattern list * range
    | TPat_tuple of TupInfo * Pattern list * TType list * range
    | TPat_array of Pattern list * TType * range
    | TPat_recd of TyconRef * TypeInst * Pattern list * range
    | TPat_range of char * char * range
    | TPat_null of range
    | TPat_isinst of TType * TType * Pattern option * range
    | TPat_error of range

    member Range: range

and PatternValBinding = 
    | PBind of Val * TypeScheme

and TypedMatchClause =  
    | TClause of Pattern * Expr option * DecisionTreeTarget * range

val ilFieldToTastConst: ILFieldInit -> Const

/// Compile a pattern into a decision tree and a set of targets.
val internal CompilePattern: 
    TcGlobals ->
    DisplayEnv ->
    Import.ImportMap ->
    // tcVal
    (ValRef -> ValUseFlag -> TTypes -> range -> Expr * TType) ->
    InfoReader ->
    // range of the expression we are matching on 
    range ->  
    // range to report "incomplete match" on
    range ->  
    // warn on unused? 
    bool ->   
    ActionOnFailure -> 
    // the value being matched against, perhaps polymorphic. Optionally includes the
    // input expression, only for the case of immediate matching on a byref pointer
    Val * Typars * Expr option -> 
    // input type-checked syntax of pattern matching
    TypedMatchClause list ->
    // input type 
    TType -> 
    // result type
    TType ->
        // produce TAST nodes
        DecisionTree * DecisionTreeTarget list

exception internal MatchIncomplete of bool * (string * bool) option * range

exception internal RuleNeverMatched of range

exception internal EnumMatchIncomplete of bool * (string * bool) option * range