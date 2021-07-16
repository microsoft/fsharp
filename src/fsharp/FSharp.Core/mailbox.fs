// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.FSharp.Control

    open System
    open System.Threading
    open System.Collections.Generic
    open Microsoft.FSharp.Core
    open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicOperators
    open Microsoft.FSharp.Control
    open Microsoft.FSharp.Control.AsyncBuilderImpl
    open Microsoft.FSharp.Control.AsyncPrimitives
    open Microsoft.FSharp.Collections

    module AsyncHelpers =

        let awaitEither a1 a2 =
            async {
                let resultCell = new ResultCell<_>()
                let! cancellationToken = Async.CancellationToken
                let start a f =
                    Async.StartWithContinuationsUsingDispatchInfo(a,
                        (fun res -> resultCell.RegisterResult(f res |> AsyncResult.Ok, reuseThread=false) |> ignore),
                        (fun edi -> resultCell.RegisterResult(edi |> AsyncResult.Error, reuseThread=false) |> ignore),
                        (fun oce -> resultCell.RegisterResult(oce |> AsyncResult.Canceled, reuseThread=false) |> ignore),
                        cancellationToken = cancellationToken
                        )
                start a1 Choice1Of2
                start a2 Choice2Of2
                // Note: It is ok to use "NoDirectCancel" here because the started computations use the same
                //       cancellation token and will register a cancelled result if cancellation occurs.
                // Note: It is ok to use "NoDirectTimeout" here because there is no specific timeout log to this routine.
                let! result = resultCell.AwaitResult_NoDirectCancelOrTimeout
                return! CreateAsyncResultAsync result
            }

        let timeout msec cancellationToken =
            assert (msec >= 0)
            let resultCell = new ResultCell<_>()
            Async.StartWithContinuations(
                computation=Async.Sleep msec,
                continuation=(fun () -> resultCell.RegisterResult((), reuseThread = false) |> ignore),
                exceptionContinuation=ignore,
                cancellationContinuation=ignore,
                cancellationToken = cancellationToken)
            // Note: It is ok to use "NoDirectCancel" here because the started computations use the same
            //       cancellation token and will register a cancelled result if cancellation occurs.
            // Note: It is ok to use "NoDirectTimeout" here because the child compuation above looks after the timeout.
            resultCell.AwaitResult_NoDirectCancelOrTimeout

    [<Sealed>]
    [<AutoSerializable(false)>]
    type Mailbox<'Msg>(cancellationSupported: bool) =
        let mutable inboxStore  = null
        let arrivals = Queue<'Msg>()
        let syncRoot = arrivals

        // Control elements indicating the state of the reader. When the reader is "blocked" at an
        // asynchronous receive, either
        //     -- "cont" is non-null and the reader is "activated" by re-scheduling cont in the thread pool; or
        //     -- "pulse" is non-null and the reader is "activated" by setting this event
        let mutable savedCont : (bool -> AsyncReturn) option = None

        // Readers who have a timeout use this event
        let mutable pulse : AutoResetEvent = null

        // Make sure that the "pulse" value is created
        let ensurePulse() =
            match pulse with
            | null ->
                pulse <- new AutoResetEvent(false)
            | _ ->
                ()
            pulse

        let waitOneNoTimeoutOrCancellation =
            MakeAsync (fun ctxt ->
                match savedCont with
                | None ->
                    let descheduled =
                        // An arrival may have happened while we're preparing to deschedule
                        lock syncRoot (fun () ->
                            if arrivals.Count = 0 then
                                // OK, no arrival so deschedule
                                savedCont <- Some(fun res -> ctxt.QueueContinuationWithTrampoline res)
                                true
                            else
                                false)
                    if descheduled then
                        Unchecked.defaultof<_>
                    else
                        // If we didn't deschedule then run the continuation immediately
                        ctxt.CallContinuation true
                | Some _ ->
                    failwith "multiple waiting reader continuations for mailbox")

        let waitOneWithCancellation timeout =
            Async.AwaitWaitHandle(ensurePulse(), millisecondsTimeout=timeout)

        let waitOne timeout =
            if timeout < 0 && not cancellationSupported then
                waitOneNoTimeoutOrCancellation
            else
                waitOneWithCancellation timeout

        member _.inbox =
            match inboxStore with
            | null -> inboxStore <- new System.Collections.Generic.List<'Msg>(1)
            | _ -> ()
            inboxStore

        member x.CurrentQueueLength =
            lock syncRoot (fun () -> x.inbox.Count + arrivals.Count)

        member x.ScanArrivalsUnsafe f =
            if arrivals.Count = 0 then
                None
            else
                let msg = arrivals.Dequeue()
                match f msg with
                | None ->
                    x.inbox.Add msg
                    x.ScanArrivalsUnsafe f
                | res -> res

        // Lock the arrivals queue while we scan that
        member x.ScanArrivals f =
            lock syncRoot (fun () -> x.ScanArrivalsUnsafe f)

        member x.ScanInbox(f, n) =
            match inboxStore with
            | null -> None
            | inbox ->
                if n >= inbox.Count
                then None
                else
                    let msg = inbox.[n]
                    match f msg with
                    | None -> x.ScanInbox (f, n+1)
                    | res -> inbox.RemoveAt n; res

        member x.ReceiveFromArrivalsUnsafe() =
            if arrivals.Count = 0 then
                None
            else
                Some(arrivals.Dequeue())

        member x.ReceiveFromArrivals() =
            lock syncRoot (fun () -> x.ReceiveFromArrivalsUnsafe())

        member x.ReceiveFromInbox() =
            match inboxStore with
            | null -> None
            | inbox ->
                if inbox.Count = 0 then
                    None
                else
                    let x = inbox.[0]
                    inbox.RemoveAt 0
                    Some x

        member x.Post msg =
            lock syncRoot (fun () ->

                // Add the message to the arrivals queue
                arrivals.Enqueue msg

                // Cooperatively unblock any waiting reader. If there is no waiting
                // reader we just leave the message in the incoming queue
                match savedCont with
                | None ->
                    match pulse with
                    | null ->
                        () // no one waiting, leaving the message in the queue is sufficient
                    | ev ->
                        // someone is waiting on the wait handle
                        ev.Set() |> ignore

                | Some action ->
                    savedCont <- None
                    action true |> ignore)

        member x.TryScan ((f: 'Msg -> (Async<'T>) option), timeout) : Async<'T option> =
            let rec scan timeoutAsync (timeoutCts:CancellationTokenSource) =
                async {
                    match x.ScanArrivals f with
                    | None ->
                        // Deschedule and wait for a message. When it comes, rescan the arrivals
                        let! ok = AsyncHelpers.awaitEither waitOneNoTimeoutOrCancellation timeoutAsync
                        match ok with
                        | Choice1Of2 true ->
                            return! scan timeoutAsync timeoutCts
                        | Choice1Of2 false ->
                            return failwith "should not happen - waitOneNoTimeoutOrCancellation always returns true"
                        | Choice2Of2 () ->
                            lock syncRoot (fun () ->
                                // Cancel the outstanding wait for messages installed by waitOneWithCancellation
                                //
                                // HERE BE DRAGONS. This is bestowed on us because we only support
                                // a single mailbox reader at any one time.
                                // If awaitEither returned control because timeoutAsync has terminated, waitOneNoTimeoutOrCancellation
                                // might still be in-flight. In practical terms, it means that the push-to-async-result-cell
                                // continuation that awaitEither registered on it is still pending, i.e. it is still in savedCont.
                                // That continuation is a no-op now, but it is still a registered reader for arriving messages.
                                // Therefore we just abandon it - a brutal way of canceling.
                                // This ugly non-compositionality is only needed because we only support a single mailbox reader
                                // (i.e. the user is not allowed to run several Receive/TryReceive/Scan/TryScan in parallel) - otherwise
                                // we would just have an extra no-op reader in the queue.
                                savedCont <- None)

                            return None
                    | Some resP ->
                        timeoutCts.Cancel() // cancel the timeout watcher
                        let! res = resP
                        return Some res
                }
            let rec scanNoTimeout () =
                async {
                    match x.ScanArrivals f with
                    | None ->
                        let! ok = waitOne Timeout.Infinite
                        if ok then
                            return! scanNoTimeout()
                        else
                            return (failwith "Timed out with infinite timeout??")
                    | Some resP ->
                        let! res = resP
                        return Some res
                }

            // Look in the inbox first
            async {
                match x.ScanInbox(f, 0) with
                | None  when timeout < 0 ->
                    return! scanNoTimeout()
                | None ->
                    let! cancellationToken = Async.CancellationToken
                    let timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, CancellationToken.None)
                    let timeoutAsync = AsyncHelpers.timeout timeout timeoutCts.Token
                    return! scan timeoutAsync timeoutCts
                | Some resP ->
                    let! res = resP
                    return Some res
            }

        member x.Scan((f: 'Msg -> (Async<'T>) option), timeout) =
            async {
                let! resOpt = x.TryScan(f, timeout)
                match resOpt with
                | None -> return raise(TimeoutException(SR.GetString(SR.mailboxScanTimedOut)))
                | Some res -> return res
            }

        member x.TryReceive timeout =
            let rec processFirstArrival() =
                async {
                    match x.ReceiveFromArrivals() with
                    | None ->
                        // Make sure the pulse is created if it is going to be needed.
                        // If it isn't, then create it, and go back to the start to
                        // check arrivals again.
                        match pulse with
                        | null when timeout >= 0 || cancellationSupported ->
                            ensurePulse() |> ignore
                            return! processFirstArrival()
                        | _ ->
                            // Wait until we have been notified about a message. When that happens, rescan the arrivals
                            let! ok = waitOne timeout
                            if ok then
                                return! processFirstArrival()
                            else
                                return None
                    | res -> return res
                }

            // look in the inbox first
            async {
                match x.ReceiveFromInbox() with
                | None -> return! processFirstArrival()
                | res -> return res
            }

        member x.Receive timeout =

            let rec processFirstArrival() =
                async {
                    match x.ReceiveFromArrivals() with
                    | None ->
                        // Make sure the pulse is created if it is going to be needed.
                        // If it isn't, then create it, and go back to the start to
                        // check arrivals again.
                        match pulse with
                        | null when timeout >= 0 || cancellationSupported ->
                            ensurePulse() |> ignore
                            return! processFirstArrival()
                        | _ ->
                            // Wait until we have been notified about a message. When that happens, rescan the arrivals
                            let! ok = waitOne timeout
                            if ok then
                                return! processFirstArrival()
                            else
                                return raise(TimeoutException(SR.GetString(SR.mailboxReceiveTimedOut)))
                    | Some res -> return res
                }

            // look in the inbox first
            async {
                match x.ReceiveFromInbox() with
                | None -> return! processFirstArrival()
                | Some res -> return res
            }

        interface System.IDisposable with
            member _.Dispose() =
                if isNotNull pulse then (pulse :> IDisposable).Dispose()

#if DEBUG
        member x.UnsafeContents =
            (x.inbox, arrivals, pulse, savedCont) |> box
#endif


    [<Sealed>]
    [<CompiledName("FSharpAsyncReplyChannel`1")>]
    type AsyncReplyChannel<'Reply>(replyf : 'Reply -> unit) =
        member x.Reply value = replyf value

    [<Sealed>]
    [<AutoSerializable(false)>]
    [<CompiledName("FSharpMailboxProcessor`1")>]
    type MailboxProcessor<'Msg>(body, ?cancellationToken) =

        let cancellationSupported = cancellationToken.IsSome
        let cancellationToken = defaultArg cancellationToken Async.DefaultCancellationToken
        let mailbox = new Mailbox<'Msg>(cancellationSupported)
        let mutable defaultTimeout = Threading.Timeout.Infinite
        let mutable started = false
        let errorEvent = new Event<Exception>()

        member _.CurrentQueueLength = mailbox.CurrentQueueLength // nb. unprotected access gives an approximation of the queue length

        member _.DefaultTimeout
            with get() = defaultTimeout
            and set v = defaultTimeout <- v

        [<CLIEvent>]
        member _.Error = errorEvent.Publish

#if DEBUG
        member _.UnsafeMessageQueueContents = mailbox.UnsafeContents
#endif

        member private x.PrepareStart() =
            if started then
                raise (new InvalidOperationException(SR.GetString(SR.mailboxProcessorAlreadyStarted)))
            else
                started <- true

                // Protect the execution and send errors to the event.
                // Note that exception stack traces are lost in this design - in an extended design
                // the event could propagate an ExceptionDispatchInfo instead of an Exception.
                async { try
                            do! body x
                        with exn ->
                            errorEvent.Trigger exn }

        member x.Start() =
            let p = x.PrepareStart()
            Async.Start(computation=p, cancellationToken=cancellationToken)

        member x.StartImmediate() =
            let p = x.PrepareStart()
            Async.StartImmediate(computation=p, cancellationToken=cancellationToken)

        member _.Post message = mailbox.Post message

        member _.TryPostAndReply(buildMessage : (_ -> 'Msg), ?timeout) : 'Reply option =
            let timeout = defaultArg timeout defaultTimeout
            use resultCell = new ResultCell<_>()
            let msg = buildMessage (new AsyncReplyChannel<_>(fun reply ->
                                    // Note the ResultCell may have been disposed if the operation
                                    // timed out. In this case RegisterResult drops the result on the floor.
                                    resultCell.RegisterResult(reply, reuseThread=false) |> ignore))
            mailbox.Post msg
            resultCell.TryWaitForResultSynchronously(timeout=timeout)

        member x.PostAndReply(buildMessage, ?timeout) : 'Reply =
            match x.TryPostAndReply(buildMessage, ?timeout=timeout) with
            | None ->  raise (TimeoutException(SR.GetString(SR.mailboxProcessorPostAndReplyTimedOut)))
            | Some res -> res

        member _.PostAndTryAsyncReply(buildMessage, ?timeout) : Async<'Reply option> =
            let timeout = defaultArg timeout defaultTimeout
            let resultCell = new ResultCell<_>()
            let msg = buildMessage (new AsyncReplyChannel<_>(fun reply ->
                                    // Note the ResultCell may have been disposed if the operation
                                    // timed out. In this case RegisterResult drops the result on the floor.
                                    resultCell.RegisterResult(reply, reuseThread=false) |> ignore))
            mailbox.Post msg
            match timeout with
            | Threading.Timeout.Infinite when not cancellationSupported ->
                async { let! result = resultCell.AwaitResult_NoDirectCancelOrTimeout
                        return Some result }

            | _ ->
                async { use _disposeCell = resultCell
                        let! ok =  Async.AwaitWaitHandle(resultCell.GetWaitHandle(), millisecondsTimeout=timeout)
                        let res = (if ok then Some(resultCell.GrabResult()) else None)
                        return res }

        member x.PostAndAsyncReply(buildMessage, ?timeout:int) =
            let timeout = defaultArg timeout defaultTimeout
            match timeout with
            | Threading.Timeout.Infinite when not cancellationSupported ->
                // Nothing to dispose, no wait handles used
                let resultCell = new ResultCell<_>()
                let msg = buildMessage (new AsyncReplyChannel<_>(fun reply -> resultCell.RegisterResult(reply, reuseThread=false) |> ignore))
                mailbox.Post msg
                resultCell.AwaitResult_NoDirectCancelOrTimeout
            | _ ->
                let asyncReply = x.PostAndTryAsyncReply(buildMessage, timeout=timeout)
                async { let! res = asyncReply
                        match res with
                        | None ->  return! raise (TimeoutException(SR.GetString(SR.mailboxProcessorPostAndAsyncReplyTimedOut)))
                        | Some res -> return res }

        member _.Receive(?timeout) =
            mailbox.Receive(timeout=defaultArg timeout defaultTimeout)

        member _.TryReceive(?timeout) =
            mailbox.TryReceive(timeout=defaultArg timeout defaultTimeout)

        member _.Scan(scanner: 'Msg -> (Async<'T>) option, ?timeout) =
            mailbox.Scan(scanner, timeout=defaultArg timeout defaultTimeout)

        member _.TryScan(scanner: 'Msg -> (Async<'T>) option, ?timeout) =
            mailbox.TryScan(scanner, timeout=defaultArg timeout defaultTimeout)

        interface System.IDisposable with
            member _.Dispose() = (mailbox :> IDisposable).Dispose()

        static member Start(body, ?cancellationToken) =
            let mailboxProcessor = new MailboxProcessor<'Msg>(body, ?cancellationToken=cancellationToken)
            mailboxProcessor.Start()
            mailboxProcessor

        static member StartImmediate(body, ?cancellationToken) =
            let mailboxProcessor = new MailboxProcessor<'Msg>(body, ?cancellationToken=cancellationToken)
            mailboxProcessor.StartImmediate()
            mailboxProcessor
