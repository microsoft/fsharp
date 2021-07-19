// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.LowerStateMachines

open FSharp.Compiler.TypedTree
open FSharp.Compiler.TcGlobals

type LoweredStateMachine =
    LoweredStateMachine of 
         templateStructTy: TType *
         dataTy: TType *
         stateVars: ValRef list *
         thisVars: ValRef list *
         moveNext: (Val * Expr) * 
         setStateMachine: (Val * Val * Expr) *
         afterCode: (Val * Expr)

type LoweredStateMachineResult =
    /// A state machine was recognised and was compilable
    | Lowered of LoweredStateMachine

    /// A state machine was recognised and was not compilable and an alternative is available
    | UseAlternative of message: string * alternativeExpr: Expr

    /// A state machine was recognised and was not compilable and no alternative is available
    | NoAlternative of message: string

    /// The construct was not a state machine
    | NotAStateMachine

/// Analyze a TAST expression to detect the elaborated form of a state machine expression, a special kind
/// of object expression that uses special code generation constructs.
val LowerStateMachineExpr:
    g: TcGlobals -> 
    overallExpr: Expr -> 
        LoweredStateMachineResult

