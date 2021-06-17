// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

module internal FSharp.Compiler.BuildGraph

open System
open System.Threading
open System.Threading.Tasks
open FSharp.Compiler.ErrorLogger
open Internal.Utilities.Library

/// This represents the global state established as each task function runs as part of the build.
///
/// Use to reset error and warning handlers.
type CompilationGlobalsScope =
    new : ErrorLogger * BuildPhase -> CompilationGlobalsScope
    interface IDisposable

/// Represents code that can be run as part of the build graph.
///
/// This is essentially cancellable async code where the only asynchronous waits are on nodes.
/// When a node is evaluated the evaluation is run synchronously on the thread of the
/// first requestor.
[<NoEquality;NoComparison;Sealed>]
type NodeCode<'T>

type Async<'T> with

    /// Asynchronously await code in the build graph
    static member AwaitNodeCode: node: NodeCode<'T> -> Async<'T>

/// A standard builder for node code.
[<Sealed>]
type NodeCodeBuilder =

    member Bind : NodeCode<'T> * ('T -> NodeCode<'U>) -> NodeCode<'U>

    member Zero : unit -> NodeCode<unit>

    member Delay : (unit -> NodeCode<'T>) -> NodeCode<'T>

    member Return : 'T -> NodeCode<'T> 

    member ReturnFrom : NodeCode<'T> -> NodeCode<'T>

    member TryWith : NodeCode<'T> * (exn -> NodeCode<'T>) -> NodeCode<'T>

    member TryFinally : NodeCode<'T> * (unit -> unit) -> NodeCode<'T>

    member For : xs: 'T seq * binder: ('T -> NodeCode<unit>) -> NodeCode<unit>

    member Combine : x1: NodeCode<unit> * x2: NodeCode<'T> -> NodeCode<'T>

    /// A limited form 'use' for establishing the compilation globals.  (Note
    /// that a proper generic 'use' could be implemented but has not currently been necessary)
    member Using : CompilationGlobalsScope * (CompilationGlobalsScope -> NodeCode<'T>) -> NodeCode<'T>

/// Specifies code that can be run as part of the build graph.
val node : NodeCodeBuilder

/// Contains helpers to specify code that can be run as part of the build graph.
[<AbstractClass;Sealed>]
type NodeCode =

    /// Only used for testing, do not use
    static member RunImmediate: computation: NodeCode<'T> * ct: CancellationToken -> 'T

    /// Used in places where we don't care about cancellation, e.g. the command line compiler
    /// and F# Interactive
    static member RunImmediateWithoutCancellation: computation: NodeCode<'T> -> 'T

    static member CancellationToken: NodeCode<CancellationToken>

    static member Sequential: computations: NodeCode<'T> seq -> NodeCode<'T []>

    /// Execute the cancellable computation synchronously using the ambient cancellation token of
    /// the NodeCode.
    static member FromCancellable: computation: Cancellable<'T> -> NodeCode<'T>

    /// Only used for testing, do not use
    static member StartAsTask_ForTesting: computation: NodeCode<'T> * ?ct: CancellationToken -> Task<'T>

    /// Only used for testing, do not use
    static member AwaitWaitHandle_ForTesting: waitHandle: WaitHandle -> NodeCode<bool>

/// Contains helpers related to the build graph
[<RequireQualifiedAccess>]
module internal GraphNode =

    /// Allows to specify the language for error messages
    val SetPreferredUILang: preferredUiLang: string option -> unit

/// Evaluate the computation, allowing asynchronous waits on existing ongoing evaluations of the
/// same node, and strongly cache the result.
///
/// Once the result has been cached, the computation function will also be removed, or 'null'ed out, 
/// as to prevent any references captured by the computation from being strongly held.
[<Sealed>]
type internal GraphNode<'T> =

    /// - retryCompute - When set to 'true', subsequent requesters will retry the computation if the first-in request cancels. Retrying computations will have better callstacks.
    /// - computation - The computation code to run.
    new: retryCompute: bool * computation: NodeCode<'T> -> GraphNode<'T>

    /// By default, 'retryCompute' is 'true'.
    new : computation: NodeCode<'T> -> GraphNode<'T>

    /// Return NodeCode which, when executed, will get the value of the computation if already computed, or
    /// await an existing in-progress computation for the node if one exists, or else will synchronously
    /// start the computation on the current thread.
    member GetOrComputeValue: unit -> NodeCode<'T>

    /// Return 'Some' if the computation has already been computed, else None if
    /// the computation is in-progress or has not yet been started.
    member TryPeekValue: unit -> 'T voption

    /// Return 'true' if the computation has already been computed.
    member HasValue: bool

    /// Return 'true' if the computation is in-progress.
    member IsComputing: bool