// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.LowerCallsAndSeqs

open FSharp.Compiler.Import
open FSharp.Compiler.InfoReader
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.TypedTree
open FSharp.Compiler.Text

/// An "expr -> expr" pass that eta-expands under-applied values of
/// known arity to lambda expressions and beta-var-reduces to bind
/// any known arguments.  The results are later optimized by the peephole
/// optimizer in opt.fs
val LowerImplFile: g: TcGlobals -> assembly: TypedImplFile -> TypedImplFile

/// Analyze a TAST expression to detect the elaborated form of a sequence expression.
/// Then compile it to a state machine represented as a TAST containing goto, return and label nodes.
/// The returned state machine will also contain references to state variables (from internal 'let' bindings),
/// a program counter (pc) that records the current state, and a current generated value (current).
/// All these variables are then represented as fields in a hosting closure object along with any additional
/// free variables of the sequence expression.
val ConvertSequenceExprToObject: g: TcGlobals -> amap: ImportMap -> overallExpr: Expr -> (ValRef * ValRef * ValRef * ValRef list * Expr * Expr * Expr * TType * range) option

val IsPossibleSequenceExpr: g: TcGlobals -> overallExpr: Expr -> bool

val LowerComputedListOrArrayExpr: tcVal: ConstraintSolver.TcValF -> g: TcGlobals -> amap: ImportMap -> Expr -> Expr option
