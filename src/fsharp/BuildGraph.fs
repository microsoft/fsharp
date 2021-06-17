// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

module FSharp.Compiler.BuildGraph

open System
open System.Threading
open System.Threading.Tasks
open System.Diagnostics
open System.Globalization
open FSharp.Compiler.ErrorLogger
open Internal.Utilities.Library

/// This represents the thread-local state established as each task function runs as part of the build.
///
/// Use to reset error and warning handlers.
type CompilationGlobalsScope(errorLogger: ErrorLogger, phase: BuildPhase) = 
    let unwindEL = PushErrorLoggerPhaseUntilUnwind(fun _ -> errorLogger)
    let unwindBP = PushThreadBuildPhaseUntilUnwind phase

    member _.ErrorLogger = errorLogger
    member _.Phase = phase

    // Return the disposable object that cleans up
    interface IDisposable with
        member d.Dispose() =
            unwindBP.Dispose()         
            unwindEL.Dispose()

[<NoEquality;NoComparison>]
type NodeCode<'T> = Node of Async<'T>

let wrapThreadStaticInfo computation =
    async {
        let errorLogger = CompileThreadStatic.ErrorLogger
        let phase = CompileThreadStatic.BuildPhase
        try
            return! computation 
        finally
            CompileThreadStatic.ErrorLogger <- errorLogger
            CompileThreadStatic.BuildPhase <- phase 
    }

type Async<'T> with

    static member AwaitNodeCode(node: NodeCode<'T>) =
        match node with
        | Node(computation) -> wrapThreadStaticInfo computation

[<Sealed>]
type NodeCodeBuilder() =

    static let zero = Node(async.Zero())

    [<DebuggerHidden;DebuggerStepThrough>]
    member _.Zero () : NodeCode<unit> = zero

    [<DebuggerHidden;DebuggerStepThrough>]
    member _.Delay (f: unit -> NodeCode<'T>) = 
        Node(async.Delay(fun () -> match f() with Node(p) -> p))    

    [<DebuggerHidden;DebuggerStepThrough>]
    member _.Return value = Node(async.Return(value))

    [<DebuggerHidden;DebuggerStepThrough>]
    member _.ReturnFrom (computation: NodeCode<_>) = computation

    [<DebuggerHidden;DebuggerStepThrough>]
    member _.Bind (Node(p): NodeCode<'a>, binder: 'a -> NodeCode<'b>) : NodeCode<'b> =
        Node(async.Bind(p, fun x -> match binder x with Node p -> p))

    [<DebuggerHidden;DebuggerStepThrough>]
    member _.TryWith(Node(p): NodeCode<'T>, binder: exn -> NodeCode<'T>) : NodeCode<'T> =
        Node(async.TryWith(p, fun ex -> match binder ex with Node p -> p))

    [<DebuggerHidden;DebuggerStepThrough>]
    member _.TryFinally(Node(p): NodeCode<'T>, binder: unit -> unit) : NodeCode<'T> =
        Node(async.TryFinally(p, binder))

    [<DebuggerHidden;DebuggerStepThrough>]
    member _.For(xs: 'T seq, binder: 'T -> NodeCode<unit>) : NodeCode<unit> =
        Node(async.For(xs, fun x -> match binder x with Node p -> p))

    [<DebuggerHidden;DebuggerStepThrough>]
    member _.Combine(Node(p1): NodeCode<unit>, Node(p2): NodeCode<'T>) : NodeCode<'T> =
        Node(async.Combine(p1, p2))

    [<DebuggerHidden;DebuggerStepThrough>]
    member _.Using(value: CompilationGlobalsScope, binder: CompilationGlobalsScope -> NodeCode<'U>) =
        Node(
            async {
                CompileThreadStatic.ErrorLogger <- value.ErrorLogger
                CompileThreadStatic.BuildPhase <- value.Phase
                try
                    return! binder value |> Async.AwaitNodeCode
                finally
                    (value :> IDisposable).Dispose()
            }
        )

let node = NodeCodeBuilder()

[<AbstractClass;Sealed>]
type NodeCode private () =

    static let cancellationToken =
        Node(wrapThreadStaticInfo Async.CancellationToken)

    static member RunImmediate (computation: NodeCode<'T>, ct: CancellationToken) =
        let errorLogger = CompileThreadStatic.ErrorLogger
        let phase = CompileThreadStatic.BuildPhase
        try
            try
                let work =
                    async {
                        CompileThreadStatic.ErrorLogger <- errorLogger
                        CompileThreadStatic.BuildPhase <- phase
                        return! computation |> Async.AwaitNodeCode
                    }
                Async.StartImmediateAsTask(work, cancellationToken=ct).Result
            finally
                CompileThreadStatic.ErrorLogger <- errorLogger
                CompileThreadStatic.BuildPhase <- phase
        with
        | :? AggregateException as ex when ex.InnerExceptions.Count = 1 ->
            raise(ex.InnerExceptions.[0])

    static member RunImmediateWithoutCancellation (computation: NodeCode<'T>) =
        NodeCode.RunImmediate(computation, CancellationToken.None)

    static member StartAsTask_ForTesting (computation: NodeCode<'T>, ?ct: CancellationToken) =
        let errorLogger = CompileThreadStatic.ErrorLogger
        let phase = CompileThreadStatic.BuildPhase
        try
            let work =
                async {
                    CompileThreadStatic.ErrorLogger <- errorLogger
                    CompileThreadStatic.BuildPhase <- phase
                    return! computation |> Async.AwaitNodeCode
                }
            Async.StartAsTask(work, cancellationToken=defaultArg ct CancellationToken.None)
        finally
            CompileThreadStatic.ErrorLogger <- errorLogger
            CompileThreadStatic.BuildPhase <- phase

    static member CancellationToken = cancellationToken

    static member FromCancellable(computation: Cancellable<'T>) = 
        Node(wrapThreadStaticInfo (Cancellable.toAsync computation))

    static member AwaitAsync(computation: Async<'T>) = 
        Node(wrapThreadStaticInfo computation)

    static member AwaitTask(task: Task<'T>) =
        Node(wrapThreadStaticInfo(Async.AwaitTask task))

    static member AwaitTask(task: Task) =
        Node(wrapThreadStaticInfo(Async.AwaitTask task))

    static member AwaitWaitHandle_ForTesting(waitHandle: WaitHandle) =
        Node(wrapThreadStaticInfo (Async.AwaitWaitHandle(waitHandle)))

    static member Sleep(ms: int) =
        Node(wrapThreadStaticInfo (Async.Sleep(ms)))

    static member Sequential(computations: NodeCode<'T> seq) =
        node {
            let results = ResizeArray()
            for computation in computations do
                let! res = computation
                results.Add(res)
            return results.ToArray()
        }

type private AgentMessage<'T> =
    | GetValue of AsyncReplyChannel<Result<'T, Exception>> * callerCancellationToken: CancellationToken

type private Agent<'T> = (MailboxProcessor<AgentMessage<'T>> * CancellationTokenSource)

[<RequireQualifiedAccess>]
type private GraphNodeAction<'T> =
    | GetValueByAgent
    | GetValue
    | CachedValue of 'T

[<RequireQualifiedAccess>]
module GraphNode =

    // We need to store the culture for the VS thread that is executing now, 
    // so that when the agent in the async lazy object picks up thread from the thread pool we can set the culture
    let mutable culture = CultureInfo(CultureInfo.CurrentUICulture.Name)

    let SetPreferredUILang (preferredUiLang: string option) = 
        match preferredUiLang with
        | Some s -> 
            culture <- CultureInfo s
#if FX_RESHAPED_GLOBALIZATION
            CultureInfo.CurrentUICulture <- culture
#else
            Thread.CurrentThread.CurrentUICulture <- culture
#endif
        | None -> ()

[<Sealed>]
type GraphNode<'T> (retryCompute: bool, computation: NodeCode<'T>) =

    let gate = obj ()
    let mutable computation = computation
    let mutable requestCount = 0
    let mutable cachedResult: Task<'T> = Unchecked.defaultof<_>
    let mutable cachedResultNode: NodeCode<'T> = Unchecked.defaultof<_>

    let isCachedResultNodeNotNull() =
        not (obj.ReferenceEquals(cachedResultNode, null))

    let isCachedResultNotNull() =
        cachedResult <> null

    // retryCompute indicates that we abandon computations when the originator is
    // cancelled. 
    //
    // If retryCompute is 'true', the computation is run directly in the originating requestor's 
    // thread.  If cancelled, other awaiting computations must restart the computation from scratch.
    //
    // If retryCompute is 'false', a MailboxProcessor is used to allow the cancelled originator
    // to detach from the computation, while other awaiting computations continue to wait on the result.
    //
    // Currently, 'retryCompute' = true for all graph nodes. However, the code for we include the 
    // code to allow 'retryCompute' = false in case it's needed in the future, and ensure it is under independent
    // unit test.
    let loop (agent: MailboxProcessor<AgentMessage<'T>>) =
        async {
            assert (not retryCompute)
            try
                while true do
                    match! agent.Receive() with
                    | GetValue (replyChannel, callerCancellationToken) ->

                        Thread.CurrentThread.CurrentUICulture <- GraphNode.culture
                        try
                            use _reg = 
                                // When a cancellation has occured, notify the reply channel to let the requester stop waiting for a response.
                                callerCancellationToken.Register (fun () -> 
                                    let ex = OperationCanceledException() :> exn
                                    replyChannel.Reply (Result.Error ex)
                                )

                            callerCancellationToken.ThrowIfCancellationRequested ()

                            if isCachedResultNotNull() then
                                replyChannel.Reply(Ok cachedResult.Result)
                            else
                                // This computation can only be canceled if the requestCount reaches zero.
                                let! result = computation |> Async.AwaitNodeCode
                                cachedResult <- Task.FromResult(result)
                                cachedResultNode <- node { return result }
                                computation <- Unchecked.defaultof<_>
                                if not callerCancellationToken.IsCancellationRequested then
                                    replyChannel.Reply(Ok result)
                        with
                        | ex ->
                            if not callerCancellationToken.IsCancellationRequested then
                                replyChannel.Reply(Result.Error ex)
            with
            | _ -> 
                ()
        }

    let mutable agent: Agent<'T> = Unchecked.defaultof<_>

    let semaphore: SemaphoreSlim = 
        if retryCompute then
            new SemaphoreSlim(1, 1)
        else
            Unchecked.defaultof<_>

    member _.GetOrComputeValue() =
        // fast path
        if isCachedResultNodeNotNull() then
            cachedResultNode
        else
            node {
                if isCachedResultNodeNotNull() then
                    return! cachedResult |> NodeCode.AwaitTask
                else
                    let action =
                        lock gate <| fun () ->
                            // We try to get the cached result after the lock so we don't spin up a new mailbox processor.
                            if isCachedResultNodeNotNull() then
                                GraphNodeAction<'T>.CachedValue cachedResult.Result
                            else
                                requestCount <- requestCount + 1
                                if retryCompute then
                                    GraphNodeAction<'T>.GetValue
                                else
                                    match box agent with
                                    | null ->
                                        try
                                            let cts = new CancellationTokenSource()
                                            let mbp = new MailboxProcessor<_>(loop, cancellationToken = cts.Token)
                                            let newAgent = (mbp, cts)
                                            agent <- newAgent
                                            mbp.Start()
                                            GraphNodeAction<'T>.GetValueByAgent
                                        with
                                        | ex ->
                                            agent <- Unchecked.defaultof<_>
                                            raise ex
                                    | _ -> 
                                        GraphNodeAction<'T>.GetValueByAgent

                    match action with
                    | GraphNodeAction.CachedValue result -> return result
                    | GraphNodeAction.GetValue ->
                        try
                            let! ct = NodeCode.CancellationToken
                            
                            // We must set 'taken' before any implicit cancellation checks
                            // occur, making sure we are under the protection of the 'try'.
                            // For example, NodeCode's 'try/finally' (TryFinally) uses async.TryFinally which does
                            // implicit cancellation checks even before the try is entered, as do the
                            // de-sugaring of 'do!' and other CodeCode constructs.
                            let mutable taken = false
                            try
                                do! 
                                    semaphore.WaitAsync(ct)
                                     .ContinueWith(
                                        (fun _ -> taken <- true),
                                        (TaskContinuationOptions.NotOnCanceled ||| TaskContinuationOptions.NotOnFaulted ||| TaskContinuationOptions.ExecuteSynchronously)
                                     ) 
                                     |> NodeCode.AwaitTask

                                if isCachedResultNotNull() then
                                    return cachedResult.Result
                                else
                                    let tcs = TaskCompletionSource<'T>()
                                    let (Node(p)) = computation
                                    Async.StartWithContinuations(
                                        async {
                                            Thread.CurrentThread.CurrentUICulture <- GraphNode.culture
                                            return! p
                                        },
                                        (fun res ->
                                            cachedResult <- Task.FromResult(res)
                                            cachedResultNode <- node { return res }
                                            computation <- Unchecked.defaultof<_>
                                            tcs.SetResult(res)
                                        ),
                                        (fun ex ->
                                            tcs.SetException(ex)
                                        ),
                                        (fun _ ->
                                            tcs.SetCanceled()
                                        ),
                                        ct
                                    )
                                    return! tcs.Task |> NodeCode.AwaitTask
                            finally
                                if taken then
                                    semaphore.Release() |> ignore
                        finally
                            lock gate <| fun () ->
                                requestCount <- requestCount - 1

                    | GraphNodeAction.GetValueByAgent -> 
                        assert (not retryCompute)
                        let mbp, cts = agent
                        try
                            let! ct = NodeCode.CancellationToken
                            let! res = mbp.PostAndAsyncReply(fun replyChannel -> GetValue(replyChannel, ct)) |> NodeCode.AwaitAsync
                            match res with
                            | Ok result -> return result
                            | Result.Error ex -> return raise ex
                        finally
                            lock gate <| fun () ->
                                requestCount <- requestCount - 1
                                if requestCount = 0 then
                                        cts.Cancel() // cancel computation when all requests are cancelled
                                        try (mbp :> IDisposable).Dispose () with | _ -> ()
                                        cts.Dispose()
                                        agent <- Unchecked.defaultof<_>
            }

        member _.TryPeekValue() = 
            match cachedResult with
            | null -> ValueNone
            | _ -> ValueSome cachedResult.Result

        member _.HasValue = cachedResult <> null

        member _.IsComputing = requestCount > 0

    new(computation) =
        GraphNode(retryCompute=true, computation=computation)