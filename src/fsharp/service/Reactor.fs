// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.CodeAnalysis

open System
open System.Diagnostics
open System.Globalization
open System.Threading

open Internal.Utilities.Library
open Internal.Utilities.Library.Extras

/// Represents the capability to schedule work in the compiler service operations queue for the compilation thread
type internal IReactorOperations = 
    abstract EnqueueAndAwaitOpAsync : userOpName:string * opName:string * opArg:string * (CompilationThreadToken -> Cancellable<'T>) -> Async<'T>
    abstract EnqueueOp: userOpName:string * opName:string * opArg:string * (CompilationThreadToken -> unit) -> unit

[<NoEquality; NoComparison>]
type internal ReactorCommands = 
    /// Kick off a build.
    | SetBackgroundOp of ( (* userOpName: *) string * (* opName: *) string * (* opArg: *) string * (CompilationThreadToken -> Eventually<unit>)) option

    /// Do some work not synchronized in the mailbox.
    | Op of userOpName: string * opName: string * opArg: string * CancellationToken * (CompilationThreadToken -> unit) * (unit -> unit)

    /// Finish the background building
    | WaitForBackgroundOpCompletion of AsyncReplyChannel<unit>            

    /// Finish all the queued ops
    | CompleteAllQueuedOps of AsyncReplyChannel<unit>            
        
[<AutoSerializable(false);Sealed>]
/// There is one global Reactor for the entire language service, no matter how many projects or files
/// are open. 
type Reactor() = 
    static let pauseBeforeBackgroundWorkDefault = GetEnvInteger "FCS_PauseBeforeBackgroundWorkMilliseconds" 10
    static let theReactor = Reactor()
    let mutable pauseBeforeBackgroundWork = pauseBeforeBackgroundWorkDefault

    // We need to store the culture for the VS thread that is executing now, 
    // so that when the reactor picks up a thread from the thread pool we can set the culture
    let mutable culture = CultureInfo(CultureInfo.CurrentUICulture.Name)

    let mutable bgOpCts = new CancellationTokenSource()

    let sw = new System.Diagnostics.Stopwatch()

    /// Maximum time share for a piece of background work before it should (cooperatively) yield
    /// to enable other requests to be serviced. Yielding means returning a continuation function
    /// (via an Eventually<_> value of case NotYetDone) that can be called as the next piece of work. 
    let maxTimeShareMilliseconds = 
        match System.Environment.GetEnvironmentVariable("FCS_MaxTimeShare") with 
        | null | "" -> 100L
        | s -> int64 s

    /// Mailbox dispatch function.
    let builder = 
        MailboxProcessor<_>.Start <| fun inbox ->

        // Async workflow which receives messages and dispatches to worker functions.
        let rec loop (bgOpOpt, onComplete, bg) = 
            async { //Trace.TraceInformation("Reactor: receiving..., remaining {0}", inbox.CurrentQueueLength)

                    // Explanation: The reactor thread acts as the compilation thread in hosted scenarios
                    let ctok = AssumeCompilationThreadWithoutEvidence()

                    // Messages always have priority over the background op.
                    let! msg = 
                        async { match bgOpOpt, onComplete with 
                                | None, None -> 
                                    let! msg = inbox.Receive() 
                                    return Some msg 
                                | _, Some _ -> 
                                    return! inbox.TryReceive(0) 
                                | Some _, _ -> 
                                    let timeout = 
                                        if bg then 0 
                                        else 
                                            Trace.TraceInformation("Reactor: {0:n3} pausing {1} milliseconds", DateTime.Now.TimeOfDay.TotalSeconds, pauseBeforeBackgroundWork)
                                            pauseBeforeBackgroundWork
                                    return! inbox.TryReceive(timeout) }
                    Thread.CurrentThread.CurrentUICulture <- culture
                    match msg with
                    | Some (SetBackgroundOp bgOpOpt) -> 
                        let bgOpOpt = 
                            match bgOpOpt with 
                            | None -> None
                            | Some (bgUserOpName, bgOpName, bgOpArg, bgOp) -> 
                                let oldBgOpCts = bgOpCts
                                bgOpCts <- new CancellationTokenSource()
                                oldBgOpCts.Dispose()
                                Some (bgUserOpName, bgOpName, bgOpArg, bgOp ctok)

                        //Trace.TraceInformation("Reactor: --> set background op, remaining {0}", inbox.CurrentQueueLength)
                        return! loop (bgOpOpt, onComplete, false)

                    | Some (Op (userOpName, opName, opArg, ct, op, ccont)) -> 
                        if ct.IsCancellationRequested then ccont() else
                        Trace.TraceInformation("Reactor: {0:n3} --> {1}.{2} ({3}), remaining {4}", DateTime.Now.TimeOfDay.TotalSeconds, userOpName, opName, opArg, inbox.CurrentQueueLength)
                        let time = Stopwatch()
                        time.Start()
                        op ctok
                        time.Stop()
                        let span = time.Elapsed
                        //if span.TotalMilliseconds > 100.0 then 
                        let taken = span.TotalMilliseconds
                        let msg = (if taken > 10000.0 then "BAD-OP: >10s " elif taken > 3000.0 then "BAD-OP: >3s " elif taken > 1000.0 then "BAD-OP: > 1s " elif taken > 500.0 then "BAD-OP: >0.5s " else "")
                        Trace.TraceInformation("Reactor: {0:n3} {1}<-- {2}.{3}, took {4} ms", DateTime.Now.TimeOfDay.TotalSeconds, msg, userOpName, opName, span.TotalMilliseconds)
                        return! loop (bgOpOpt, onComplete, false)

                    | Some (WaitForBackgroundOpCompletion channel) -> 
                        match bgOpOpt with 
                        | None -> ()
                        | Some (bgUserOpName, bgOpName, bgOpArg, bgOp) -> 
                            Trace.TraceInformation("Reactor: {0:n3} --> wait for background {1}.{2} ({3}), remaining {4}", DateTime.Now.TimeOfDay.TotalSeconds, bgUserOpName, bgOpName, bgOpArg, inbox.CurrentQueueLength)
                            let oldBgOpCts = bgOpCts
                            bgOpCts <- new CancellationTokenSource()
                            oldBgOpCts.Dispose()
                            
                            try 
                                Eventually.force bgOpCts.Token bgOp |> ignore
                            with :? OperationCanceledException -> ()

                            if bgOpCts.IsCancellationRequested then 
                                Trace.TraceInformation("FCS: <-- wait for background was cancelled {0}.{1}", bgUserOpName, bgOpName)

                        channel.Reply(())
                        return! loop (None, onComplete, false)

                    | Some (CompleteAllQueuedOps channel) -> 
                        Trace.TraceInformation("Reactor: {0:n3} --> stop background work and complete all queued ops, remaining {1}", DateTime.Now.TimeOfDay.TotalSeconds, inbox.CurrentQueueLength)
                        return! loop (None, Some channel, false)

                    | None -> 
                        match bgOpOpt, onComplete with 
                        | _, Some onComplete -> onComplete.Reply()
                        | Some  (bgUserOpName, bgOpName, bgOpArg, bgEv), None -> 
                            Trace.TraceInformation("Reactor: {0:n3} --> background step {1}.{2} ({3})", DateTime.Now.TimeOfDay.TotalSeconds, bgUserOpName, bgOpName, bgOpArg)

                            // Force for a timeslice. If cancellation occurs we abandon the background work.
                            let bgOpRes = 
                                match Eventually.forceForTimeSlice sw maxTimeShareMilliseconds bgOpCts.Token bgEv with
                                | ValueOrCancelled.Value cont -> cont
                                | ValueOrCancelled.Cancelled _ -> Eventually.Done ()

                            let bgOp2 = 
                                match bgOpRes with 
                                | _ when bgOpCts.IsCancellationRequested ->
                                    Trace.TraceInformation("FCS: <-- background step {0}.{1}, was cancelled", bgUserOpName, bgOpName)
                                    None
                                | Eventually.Done () -> None
                                | bgEv2 -> Some (bgUserOpName, bgOpName, bgOpArg, bgEv2)

                            //if span.TotalMilliseconds > 100.0 then 
                            //let msg = (if taken > 10000.0 then "BAD-BG-SLICE: >10s " elif taken > 3000.0 then "BAD-BG-SLICE: >3s " elif taken > 1000.0 then "BAD-BG-SLICE: > 1s " else "")
                            //Trace.TraceInformation("Reactor: {0:n3} {1}<-- background step, took {2}ms", DateTime.Now.TimeOfDay.TotalSeconds, msg, taken)
                            return! loop (bgOp2, onComplete, true)
                        | None, None -> failwith "unreachable, should have used inbox.Receive"
                    }
        async { 
            while true do 
                try 
                    do! loop (None, None, false)
                with e -> 
                    Debug.Assert(false, String.Format("unexpected failure in reactor loop {0}, restarting", e))
        }

    member _.SetPreferredUILang(preferredUiLang: string option) = 
        match preferredUiLang with
        | Some s -> 
            culture <- CultureInfo s
#if FX_RESHAPED_GLOBALIZATION
            CultureInfo.CurrentUICulture <- culture
#else
            Thread.CurrentThread.CurrentUICulture <- culture
#endif
        | None -> ()

    // [Foreground Mailbox Accessors] -----------------------------------------------------------                
    member _.SetBackgroundOp(bgOpOpt) = 
        Trace.TraceInformation("Reactor: {0:n3} enqueue start background, length {1}", DateTime.Now.TimeOfDay.TotalSeconds, builder.CurrentQueueLength)
        bgOpCts.Cancel()
        builder.Post(SetBackgroundOp bgOpOpt)

    member _.CancelBackgroundOp() = 
        Trace.TraceInformation("FCS: trying to cancel any active background work")
        bgOpCts.Cancel()

    member _.EnqueueOp(userOpName, opName, opArg, op) =
        Trace.TraceInformation("Reactor: {0:n3} enqueue {1}.{2} ({3}), length {4}", DateTime.Now.TimeOfDay.TotalSeconds, userOpName, opName, opArg, builder.CurrentQueueLength)
        builder.Post(Op(userOpName, opName, opArg, CancellationToken.None, op, (fun () -> ()))) 

    member _.EnqueueOpPrim(userOpName, opName, opArg, ct, op, ccont) =
        Trace.TraceInformation("Reactor: {0:n3} enqueue {1}.{2} ({3}), length {4}", DateTime.Now.TimeOfDay.TotalSeconds, userOpName, opName, opArg, builder.CurrentQueueLength)
        builder.Post(Op(userOpName, opName, opArg, ct, op, ccont)) 

    member _.CurrentQueueLength =
        builder.CurrentQueueLength

    // This is for testing only
    member _.WaitForBackgroundOpCompletion() =
        Trace.TraceInformation("Reactor: {0:n3} enqueue wait for background, length {1}", DateTime.Now.TimeOfDay.TotalSeconds, builder.CurrentQueueLength)
        builder.PostAndReply WaitForBackgroundOpCompletion 

    // This is for testing only
    member _.CompleteAllQueuedOps() =
        Trace.TraceInformation("Reactor: {0:n3} enqueue wait for all ops, length {1}", DateTime.Now.TimeOfDay.TotalSeconds, builder.CurrentQueueLength)
        builder.PostAndReply CompleteAllQueuedOps

    member r.EnqueueAndAwaitOpAsync (userOpName, opName, opArg, f) = 
        async { 
            let! ct = Async.CancellationToken
            let resultCell = AsyncUtil.AsyncResultCell<_>()
            r.EnqueueOpPrim(userOpName, opName, opArg, ct, 
                op=(fun ctok ->
                    let result =
                        try 
                          match Cancellable.run ct (f ctok) with 
                          | ValueOrCancelled.Value r -> AsyncUtil.AsyncOk r
                          | ValueOrCancelled.Cancelled e -> AsyncUtil.AsyncCanceled e
                        with e -> e |> AsyncUtil.AsyncException

                    resultCell.RegisterResult(result)), 
                    ccont=(fun () -> resultCell.RegisterResult (AsyncUtil.AsyncCanceled(OperationCanceledException(ct))) )

            )
            return! resultCell.AsyncResult 
        }

    member _.PauseBeforeBackgroundWork with get() = pauseBeforeBackgroundWork and set v = pauseBeforeBackgroundWork <- v

    static member Singleton = theReactor 

