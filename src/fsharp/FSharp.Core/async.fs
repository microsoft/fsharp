// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.FSharp.Control

    #nowarn "40"
    #nowarn "52" // The value has been copied to ensure the original is not mutated by this operation

    open System
    open System.Diagnostics
    open System.Reflection
    open System.Runtime.CompilerServices
    open System.Runtime.ExceptionServices
    open System.Threading
    open System.Threading.Tasks
    open Microsoft.FSharp.Core
    open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicOperators
    open Microsoft.FSharp.Control
    open Microsoft.FSharp.Collections

    type LinkedSubSource(cancellationToken: CancellationToken) =

        let failureCTS = new CancellationTokenSource()

        let linkedCTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, failureCTS.Token)

        member _.Token = linkedCTS.Token

        member _.Cancel() = failureCTS.Cancel()

        member _.Dispose() =
            linkedCTS.Dispose()
            failureCTS.Dispose()

        interface IDisposable with
            member this.Dispose() = this.Dispose()

    /// Global mutable state used to associate Exception
    [<AutoOpen>]
    module ExceptionDispatchInfoHelpers =

        let associationTable = ConditionalWeakTable<exn, ExceptionDispatchInfo>()

        type ExceptionDispatchInfo with

            member edi.GetAssociatedSourceException() =
                let exn = edi.SourceException
                // Try to store the entry in the association table to allow us to recover it later.
                try associationTable.Add(exn, edi) with _ -> ()
                exn

            // Capture, but prefer the saved information if available
            [<DebuggerHidden>]
            static member RestoreOrCapture exn =
                match associationTable.TryGetValue exn with
                | true, edi -> edi
                | _ ->
                    ExceptionDispatchInfo.Capture exn

            member inline edi.ThrowAny() =
                edi.Throw()
                Unchecked.defaultof<'T> // Note, this line should not be reached, but gives a generic return type

    // F# don't always take tailcalls to functions returning 'unit' because this
    // is represented as type 'void' in the underlying IL.
    // Hence we don't use the 'unit' return type here, and instead invent our own type.
    [<NoEquality; NoComparison>]
    type AsyncReturn =
        | AsyncReturn
        with
            static member inline Fake() = Unchecked.defaultof<AsyncReturn>

    type cont<'T> = ('T -> AsyncReturn)
    type econt = (ExceptionDispatchInfo -> AsyncReturn)
    type ccont = (OperationCanceledException -> AsyncReturn)

    [<AllowNullLiteral>]
    type Trampoline() =

        [<Literal>]
        static let bindLimitBeforeHijack = 300

        [<ThreadStatic; DefaultValue>]
        static val mutable private thisThreadHasTrampoline: bool

        static member ThisThreadHasTrampoline =
            Trampoline.thisThreadHasTrampoline

        let mutable storedCont = None
        let mutable storedExnCont = None
        let mutable bindCount = 0

        /// Use this trampoline on the synchronous stack if none exists, and execute
        /// the given function. The function might write its continuation into the trampoline.
        [<DebuggerHidden>]
        member _.Execute (firstAction: unit -> AsyncReturn) =

            let thisThreadHadTrampoline = Trampoline.thisThreadHasTrampoline
            Trampoline.thisThreadHasTrampoline <- true
            try
                let mutable keepGoing = true
                let mutable action = firstAction
                while keepGoing do
                    try
                        action() |> ignore
                        match storedCont with
                        | None ->
                            keepGoing <- false
                        | Some cont ->
                            storedCont <- None
                            action <- cont
                    
                    // Catch exceptions at the trampoline to get a full .StackTrace entry
                    // This is because of this problem https://stackoverflow.com/questions/5301535/exception-call-stack-truncated-without-any-re-throwing
                    // where only a limited number of stack frames are included in the .StackTrace property 
                    // of a .NET exception when it is thrown, up to the first catch handler.
                    //
                    // So when running async code, there aren't any intermediate catch handlers (though there
                    // may be intermediate try/finally frames), there is just this one catch handler at the
                    // base of the stack.
                    //
                    // If an exception is thrown we must have storedExnCont via OnExceptionRaised.
                    with exn ->
                        match storedExnCont with
                        | None ->
                            // Here, the exception escapes the trampoline. This should not happen since all
                            // exception-generating code should use ProtectCode. However some
                            // direct uses of combinators (not using async {...}) may cause
                            // code to execute unprotected, e.g. async.While((fun () -> failwith ".."), ...) executes the first
                            // guardExpr unprotected.
                            reraise()

                        | Some econt ->
                            storedExnCont <- None
                            let edi = ExceptionDispatchInfo.RestoreOrCapture exn
                            action <- (fun () -> econt edi)

            finally
                Trampoline.thisThreadHasTrampoline <- thisThreadHadTrampoline
            AsyncReturn.Fake()

        /// Increment the counter estimating the size of the synchronous stack and
        /// return true if time to jump on trampoline.
        member _.IncrementBindCount() =
            bindCount <- bindCount + 1
            bindCount >= bindLimitBeforeHijack

        /// Prepare to abandon the synchronous stack of the current execution and save the continuation in the trampoline.
        member _.Set action =
            assert storedCont.IsNone
            bindCount <- 0
            storedCont <- Some action
            AsyncReturn.Fake()

        /// Save the exception continuation during propagation of an exception, or prior to raising an exception
        member _.OnExceptionRaised (action: econt) =
            assert storedExnCont.IsNone
            storedExnCont <- Some action

    type TrampolineHolder() as this =
        let mutable trampoline = null

        // Preallocate this delegate and keep it in the trampoline holder.
        let sendOrPostCallbackWithTrampoline =
            SendOrPostCallback (fun o ->
                let f = unbox<unit -> AsyncReturn> o
                // Reminder: the ignore below ignores an AsyncReturn.
                this.ExecuteWithTrampoline f |> ignore)

        // Preallocate this delegate and keep it in the trampoline holder.
        let waitCallbackForQueueWorkItemWithTrampoline =
            WaitCallback (fun o ->
                let f = unbox<unit -> AsyncReturn> o
                this.ExecuteWithTrampoline f |> ignore)

        // Preallocate this delegate and keep it in the trampoline holder.
        let threadStartCallbackForStartThreadWithTrampoline =
            ParameterizedThreadStart (fun o ->
                let f = unbox<unit -> AsyncReturn> o
                this.ExecuteWithTrampoline f |> ignore)

        /// Execute an async computation after installing a trampoline on its synchronous stack.
        [<DebuggerHidden>]
        member _.ExecuteWithTrampoline firstAction =
            trampoline <- Trampoline()
            trampoline.Execute firstAction

        member _.PostWithTrampoline (syncCtxt: SynchronizationContext)  (f: unit -> AsyncReturn) =
            syncCtxt.Post (sendOrPostCallbackWithTrampoline, state=(f |> box))
            AsyncReturn.Fake()

        member _.QueueWorkItemWithTrampoline (f: unit -> AsyncReturn) =
            if not (ThreadPool.QueueUserWorkItem(waitCallbackForQueueWorkItemWithTrampoline, f |> box)) then
                failwith "failed to queue user work item"
            AsyncReturn.Fake()

        member this.PostOrQueueWithTrampoline (syncCtxt: SynchronizationContext) f =
            match syncCtxt with
            | null -> this.QueueWorkItemWithTrampoline f
            | _ -> this.PostWithTrampoline syncCtxt f

        // This should be the only call to Thread.Start in this library. We must always install a trampoline.
        member _.StartThreadWithTrampoline (f: unit -> AsyncReturn) =
            Thread(threadStartCallbackForStartThreadWithTrampoline, IsBackground=true).Start(f|>box)
            AsyncReturn.Fake()

        /// Save the exception continuation during propagation of an exception, or prior to raising an exception
        member inline _.OnExceptionRaised econt =
            trampoline.OnExceptionRaised econt

        /// Call a continuation, but first check if an async computation should trampoline on its synchronous stack.
        member inline _.HijackCheckThenCall (cont: 'T -> AsyncReturn) res =
            if trampoline.IncrementBindCount() then
                trampoline.Set (fun () -> cont res)
            else
                // NOTE: this must be a tailcall
                cont res

    [<NoEquality; NoComparison>]
    [<AutoSerializable(false)>]
    /// Represents rarely changing components of an in-flight async computation
    type AsyncActivationAux =
        { /// The active cancellation token
          token: CancellationToken

          /// The exception continuation
          econt: econt

          /// The cancellation continuation
          ccont: ccont

          /// Holds some commonly-allocated callbacks and a mutable location to use for a trampoline
          trampolineHolder: TrampolineHolder }

    [<NoEquality; NoComparison>]
    [<AutoSerializable(false)>]
    /// Represents context for an in-flight async computation
    type AsyncActivationContents<'T> =
        { /// The success continuation
          cont: cont<'T>

          /// The rarely changing components
          aux: AsyncActivationAux }

    /// A struct wrapper around AsyncActivationContents. Using a struct wrapper allows us to change representation of the
    /// contents at a later point, e.g. to change the contents to a .NET Task or some other representation.
    [<Struct; NoEquality; NoComparison>]
    type AsyncActivation<'T>(contents: AsyncActivationContents<'T>) =

        /// Produce a new execution context for a composite async
        member ctxt.WithCancellationContinuation ccont = AsyncActivation<'T> { contents with aux = { ctxt.aux with ccont = ccont } }

        /// Produce a new execution context for a composite async
        member ctxt.WithExceptionContinuation econt = AsyncActivation<'T> { contents with aux = { ctxt.aux with econt = econt } }

        /// Produce a new execution context for a composite async
        member _.WithContinuation cont = AsyncActivation<'U> { cont = cont; aux = contents.aux }

        /// Produce a new execution context for a composite async
        member _.WithContinuations(cont, econt) = AsyncActivation<'U> { cont = cont; aux = { contents.aux with econt = econt } }

        /// Produce a new execution context for a composite async
        member ctxt.WithContinuations(cont, econt, ccont) = AsyncActivation<'T> { contents with cont = cont; aux = { ctxt.aux with econt = econt; ccont = ccont } }

        /// The extra information relevant to the execution of the async
        member _.aux = contents.aux

        /// The success continuation relevant to the execution of the async
        member _.cont = contents.cont

        /// The exception continuation relevant to the execution of the async
        member _.econt = contents.aux.econt

        /// The cancellation continuation relevant to the execution of the async
        member _.ccont = contents.aux.ccont

        /// The cancellation token relevant to the execution of the async
        member _.token = contents.aux.token

        /// The trampoline holder being used to protect execution of the async
        member _.trampolineHolder = contents.aux.trampolineHolder

        /// Check if cancellation has been requested
        member _.IsCancellationRequested = contents.aux.token.IsCancellationRequested

        /// Call the cancellation continuation of the active computation
        member _.OnCancellation () =
            contents.aux.ccont (OperationCanceledException (contents.aux.token))

        /// Check for trampoline hijacking.
        member inline _.HijackCheckThenCall cont arg =
            contents.aux.trampolineHolder.HijackCheckThenCall cont arg

        /// Call the success continuation of the asynchronous execution context after checking for
        /// cancellation and trampoline hijacking.
        //   - Cancellation check
        //   - Hijack check
        member ctxt.OnSuccess result =
            if ctxt.IsCancellationRequested then
                ctxt.OnCancellation ()
            else
                ctxt.HijackCheckThenCall ctxt.cont result

        /// Save the exception continuation during propagation of an exception, or prior to raising an exception
        member _.OnExceptionRaised() =
            contents.aux.trampolineHolder.OnExceptionRaised contents.aux.econt

        /// Make an initial async activation.
        static member Create cancellationToken trampolineHolder cont econt ccont : AsyncActivation<'T> =
            AsyncActivation { cont = cont; aux = { token = cancellationToken; econt = econt; ccont = ccont; trampolineHolder = trampolineHolder } }

        /// Queue the success continuation of the asynchronous execution context as a work item in the thread pool
        /// after installing a trampoline
        member ctxt.QueueContinuationWithTrampoline (result: 'T) =
            let cont = ctxt.cont
            ctxt.aux.trampolineHolder.QueueWorkItemWithTrampoline(fun () -> cont result)

        /// Ensure that any exceptions raised by the immediate execution of "userCode"
        /// are sent to the exception continuation. This is done by allowing the exception to propagate
        /// to the trampoline, and the saved exception continuation is called there.
        ///
        /// It is also valid for MakeAsync primitive code to call the exception continuation directly.
        [<DebuggerHidden>]
        member ctxt.ProtectCode userCode =
            let mutable ok = false
            try
                let res = userCode()
                ok <- true
                res
            finally
                if not ok then
                    ctxt.OnExceptionRaised()

        member ctxt.PostWithTrampoline (syncCtxt: SynchronizationContext)  (f: unit -> AsyncReturn) =
            let holder = contents.aux.trampolineHolder
            ctxt.ProtectCode (fun () -> holder.PostWithTrampoline syncCtxt f)

        /// Call the success continuation of the asynchronous execution context
        member ctxt.CallContinuation(result: 'T) =
            ctxt.cont result

    /// Represents an asynchronous computation
    [<NoEquality; NoComparison; CompiledName("FSharpAsync`1")>]
    type Async<'T> =
        { Invoke: (AsyncActivation<'T> -> AsyncReturn) }

    /// Mutable register to help ensure that code is only executed once
    [<Sealed>]
    type Latch() =
        let mutable i = 0

        /// Execute the latch
        member _.Enter() = Interlocked.CompareExchange(&i, 1, 0) = 0

    /// Represents the result of an asynchronous computation
    [<NoEquality; NoComparison>]
    type AsyncResult<'T>  =
        | Ok of 'T
        | Error of ExceptionDispatchInfo
        | Canceled of OperationCanceledException

        /// Get the result of an asynchronous computation
        [<DebuggerHidden>]
        member res.Commit () =
            match res with
            | AsyncResult.Ok res -> res
            | AsyncResult.Error edi -> edi.ThrowAny()
            | AsyncResult.Canceled exn -> raise exn

    /// Primitives to execute asynchronous computations
    module AsyncPrimitives =

        let inline fake () = Unchecked.defaultof<AsyncReturn>

        let inline unfake (_: AsyncReturn)  = ()

        /// The mutable global CancellationTokenSource, see Async.DefaultCancellationToken
        let mutable defaultCancellationTokenSource = new CancellationTokenSource()

        /// Primitive to invoke an async computation.
        //
        // Note: direct calls to this function may end up in user assemblies via inlining
        [<DebuggerHidden>]
        let Invoke (computation: Async<'T>) (ctxt: AsyncActivation<_>) : AsyncReturn =
            ctxt.HijackCheckThenCall computation.Invoke ctxt

        /// Apply 'userCode' to 'arg'. If no exception is raised then call the normal continuation.  Used to implement
        /// 'finally' and 'when cancelled'.
        ///
        /// - Apply 'userCode' to argument with exception protection
        /// - Hijack check before invoking the continuation
        [<DebuggerHidden>]
        let CallThenContinue userCode arg (ctxt: AsyncActivation<_>) : AsyncReturn =
            let mutable result = Unchecked.defaultof<_>
            let mutable ok = false

            try
                result <- userCode arg
                ok <- true
            finally
                if not ok then
                    ctxt.OnExceptionRaised()

            if ok then
                ctxt.HijackCheckThenCall ctxt.cont result
            else
                fake()

        /// Apply 'part2' to 'result1' and invoke the resulting computation.
        ///
        /// Note: direct calls to this function end up in user assemblies via inlining
        ///
        /// - Apply 'part2' to argument with exception protection
        /// - Hijack check before invoking the resulting computation
        [<DebuggerHidden>]
        let CallThenInvoke (ctxt: AsyncActivation<_>) result1 part2 : AsyncReturn =
            let mutable result = Unchecked.defaultof<_>
            let mutable ok = false

            try
                result <- part2 result1
                ok <- true
            finally
                if not ok then
                    ctxt.OnExceptionRaised()

            if ok then
                Invoke result ctxt
            else
                fake()

        /// Like `CallThenInvoke` but does not do a hijack check for historical reasons (exact code compat)
        [<DebuggerHidden>]
        let CallThenInvokeNoHijackCheck (ctxt: AsyncActivation<_>) result1 userCode =
            let mutable res = Unchecked.defaultof<_>
            let mutable ok = false

            try
                res <- userCode result1
                ok <- true
            finally
                if not ok then
                    ctxt.OnExceptionRaised()

            if ok then
                res.Invoke ctxt
            else
                fake()

        /// Apply 'filterFunction' to 'arg'. If the result is 'Some' invoke the resulting computation. If the result is 'None'
        /// then send 'result1' to the exception continuation.
        ///
        /// - Apply 'filterFunction' to argument with exception protection
        /// - Hijack check before invoking the resulting computation or exception continuation
        [<DebuggerHidden>]
        let CallFilterThenInvoke (ctxt: AsyncActivation<'T>) filterFunction (edi: ExceptionDispatchInfo) : AsyncReturn =
            let mutable resOpt = None
            let mutable ok = false

            try
                resOpt <- filterFunction (edi.GetAssociatedSourceException())
                ok <- true
            finally
                if not ok then
                    ctxt.OnExceptionRaised()

            if ok then
                match resOpt with
                | None ->
                    ctxt.HijackCheckThenCall ctxt.econt edi
                | Some res ->
                    Invoke res ctxt
            else
                fake()

        /// Build a primitive without any exception or resync protection
        [<DebuggerHidden>]
        let MakeAsync body = { Invoke = body }

        [<DebuggerHidden>]
        let MakeAsyncWithCancelCheck body =
            MakeAsync (fun ctxt ->
                if ctxt.IsCancellationRequested then
                    ctxt.OnCancellation ()
                else
                    body ctxt)

        /// Execute part1, then apply part2, then execute the result of that
        /// 
        /// Note: direct calls to this function end up in user assemblies via inlining
        ///   - Initial cancellation check
        ///   - Initial hijack check (see Invoke)
        ///   - No hijack check after applying 'part2' to argument (see CallThenInvoke)
        ///   - No cancellation check after applying 'part2' to argument (see CallThenInvoke)
        ///   - Apply 'part2' to argument with exception protection (see CallThenInvoke)
        [<DebuggerHidden>]
        let Bind (ctxt: AsyncActivation<'T>) (part1: Async<'U>) (part2: 'U -> Async<'T>) : AsyncReturn =
            if ctxt.IsCancellationRequested then
                ctxt.OnCancellation ()
            else
                // Note, no cancellation check is done before calling 'part2'.  This is
                // because part1 may bind a resource, while part2 is a try/finally, and, if
                // the resource creation completes, we want to enter part2 before cancellation takes effect.
                Invoke part1 (ctxt.WithContinuation(fun result1 -> CallThenInvokeNoHijackCheck ctxt result1 part2))

        /// Re-route all continuations to execute the finally function.
        ///   - Cancellation check after 'entering' the try/finally and before running the body
        ///   - Hijack check after 'entering' the try/finally and before running the body (see Invoke)
        ///   - Run 'finallyFunction' with exception protection (see CallThenContinue)
        ///   - Hijack check before any of the continuations (see CallThenContinue)
        [<DebuggerHidden>]
        let TryFinally (ctxt: AsyncActivation<'T>) (computation: Async<'T>) finallyFunction =
            // Note, we don't test for cancellation before entering a try/finally. This prevents
            // a resource being created without being disposed.

            // The new continuation runs the finallyFunction and resumes the old continuation
            // If an exception is thrown we continue with the previous exception continuation.
            let cont result =
                CallThenContinue finallyFunction () (ctxt.WithContinuation(fun () -> ctxt.cont result))

            // The new exception continuation runs the finallyFunction and then runs the previous exception continuation.
            // If an exception is thrown we continue with the previous exception continuation.
            let econt edi =
                CallThenContinue finallyFunction () (ctxt.WithContinuation(fun () -> ctxt.econt edi))

            // The cancellation continuation runs the finallyFunction and then runs the previous cancellation continuation.
            // If an exception is thrown we continue with the previous cancellation continuation (the exception is lost)
            let ccont cexn =
                CallThenContinue finallyFunction () (ctxt.WithContinuations(cont=(fun () -> ctxt.ccont cexn), econt = (fun _ -> ctxt.ccont cexn)))

            let ctxt = ctxt.WithContinuations(cont=cont, econt=econt, ccont=ccont)
            if ctxt.IsCancellationRequested then
                ctxt.OnCancellation ()
            else
                computation.Invoke ctxt

        /// Re-route the exception continuation to call to catchFunction. If catchFunction returns None then call the exception continuation.
        /// If it returns Some, invoke the resulting async.
        ///   - Cancellation check before entering the try
        ///   - No hijack check after 'entering' the try/with
        ///   - Cancellation check before applying the 'catchFunction'
        ///   - Apply `catchFunction' to argument with exception protection (see CallFilterThenInvoke)
        ///   - Hijack check before invoking the resulting computation or exception continuation (see CallFilterThenInvoke)
        [<DebuggerHidden>]
        let TryWith (ctxt: AsyncActivation<'T>) (computation: Async<'T>) catchFunction =
            if ctxt.IsCancellationRequested then
                ctxt.OnCancellation ()
            else
                let ctxt =
                    ctxt.WithExceptionContinuation(fun edi ->
                        if ctxt.IsCancellationRequested then
                            ctxt.OnCancellation ()
                        else
                            CallFilterThenInvoke ctxt catchFunction edi)

                computation.Invoke ctxt

        /// Make an async for an AsyncResult
        //   - No cancellation check
        //   - No hijack check
        let CreateAsyncResultAsync res =
            MakeAsync (fun ctxt ->
                match res with
                | AsyncResult.Ok r -> ctxt.cont r
                | AsyncResult.Error edi -> ctxt.econt edi
                | AsyncResult.Canceled cexn -> ctxt.ccont cexn)

        /// Generate async computation which calls its continuation with the given result
        ///   - Cancellation check (see OnSuccess)
        ///   - Hijack check (see OnSuccess)
        let inline CreateReturnAsync res =
            // Note: this code ends up in user assemblies via inlining
            MakeAsync (fun ctxt -> ctxt.OnSuccess res)

        /// Runs the first process, takes its result, applies f and then runs the new process produced.
        ///   - Initial cancellation check (see Bind)
        ///   - Initial hijack check (see Bind)
        ///   - No hijack check after applying 'part2' to argument (see Bind)
        ///   - No cancellation check after applying 'part2' to argument (see Bind)
        ///   - Apply 'part2' to argument with exception protection (see Bind)
        let inline CreateBindAsync part1 part2  =
            // Note: this code ends up in user assemblies via inlining
            MakeAsync (fun ctxt ->
                Bind ctxt part1 part2)

        /// Call the given function with exception protection.
        ///   - No initial cancellation check
        ///   - Hijack check after applying part2 to argument (see CallThenInvoke)
        let inline CreateCallAsync part2 result1 =
            // Note: this code ends up in user assemblies via inlining
            MakeAsync (fun ctxt ->
                CallThenInvoke ctxt result1 part2)

        /// Call the given function with exception protection.
        ///   - Initial cancellation check
        ///   - Hijack check after applying computation to argument (see CallThenInvoke)
        ///   - Apply 'computation' to argument with exception protection (see CallThenInvoke)
        let inline CreateDelayAsync computation =
            // Note: this code ends up in user assemblies via inlining
            MakeAsyncWithCancelCheck (fun ctxt ->
                CallThenInvoke ctxt () computation)

        /// Implements the sequencing construct of async computation expressions
        ///   - Initial cancellation check (see CreateBindAsync)
        ///   - Initial hijack check (see CreateBindAsync)
        ///   - No hijack check after applying 'part2' to argument (see CreateBindAsync)
        ///   - No cancellation check after applying 'part2' to argument (see CreateBindAsync)
        ///   - Apply 'part2' to argument with exception protection (see CreateBindAsync)
        let inline CreateSequentialAsync part1 part2 =
            // Note: this code ends up in user assemblies via inlining
            CreateBindAsync part1 (fun () -> part2)

        /// Create an async for a try/finally
        ///   - Cancellation check after 'entering' the try/finally and before running the body
        ///   - Hijack check after 'entering' the try/finally and before running the body (see TryFinally)
        ///   - Apply 'finallyFunction' with exception protection (see TryFinally)
        let inline CreateTryFinallyAsync finallyFunction computation =
            MakeAsync (fun ctxt -> TryFinally ctxt computation finallyFunction)

        /// Create an async for a try/with filtering exceptions through a pattern match
        ///   - Cancellation check before entering the try (see TryWith)
        ///   - Cancellation check before entering the with (see TryWith)
        ///   - Apply `filterFunction' to argument with exception protection (see TryWith)
        ///   - Hijack check before invoking the resulting computation or exception continuation
        let inline CreateTryWithFilterAsync filterFunction computation =
            MakeAsync (fun ctxt -> TryWith ctxt computation filterFunction)

        /// Create an async for a try/with filtering
        ///   - Cancellation check before entering the try (see TryWith)
        ///   - Cancellation check before entering the with (see TryWith)
        ///   - Apply `catchFunction' to argument with exception protection (see TryWith)
        ///   - Hijack check before invoking the resulting computation or exception continuation
        let inline CreateTryWithAsync catchFunction computation =
            MakeAsync (fun ctxt -> TryWith ctxt computation (fun exn -> Some (catchFunction exn)))

        /// Call the finallyFunction if the computation results in a cancellation, and then continue with cancellation.
        /// If the finally function gives an exception then continue with cancellation regardless.
        ///   - No cancellation check before entering the when-cancelled
        ///   - No hijack check before entering the when-cancelled
        ///   - Apply `finallyFunction' to argument with exception protection (see CallThenContinue)
        ///   - Hijack check before continuing with cancellation (see CallThenContinue)
        let CreateWhenCancelledAsync (finallyFunction: OperationCanceledException -> unit) computation =
            MakeAsync (fun ctxt ->
                let ccont = ctxt.ccont
                let ctxt =
                    ctxt.WithCancellationContinuation(fun cexn ->
                        CallThenContinue finallyFunction cexn (ctxt.WithContinuations(cont = (fun _ -> ccont cexn), econt = (fun _ -> ccont cexn))))
                computation.Invoke ctxt)

        /// A single pre-allocated computation that fetched the current cancellation token
        let cancellationTokenAsync  =
            MakeAsync (fun ctxt -> ctxt.cont ctxt.aux.token)

        /// A single pre-allocated computation that returns a unit result
        ///   - Cancellation check (see CreateReturnAsync)
        ///   - Hijack check (see CreateReturnAsync)
        let unitAsync =
            CreateReturnAsync()

        /// Implement use/Dispose
        ///
        ///   - No initial cancellation check before applying computation to its argument. See CreateTryFinallyAsync
        ///     and CreateCallAsync. We enter the try/finally before any cancel checks.
        ///   - Cancellation check after 'entering' the implied try/finally and before running the body  (see CreateTryFinallyAsync)
        ///   - Hijack check after 'entering' the implied try/finally and before running the body  (see CreateTryFinallyAsync)
        ///   - Run 'disposeFunction' with exception protection (see CreateTryFinallyAsync)
        let CreateUsingAsync (resource:'T :> IDisposable) (computation:'T -> Async<'a>) : Async<'a> =
            let disposeFunction () = Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicFunctions.Dispose resource
            CreateTryFinallyAsync disposeFunction (CreateCallAsync computation resource)

        ///   - Initial cancellation check (see CreateBindAsync)
        ///   - Initial hijack check (see CreateBindAsync)
        ///   - Cancellation check after (see unitAsync)
        ///   - No hijack check after (see unitAsync)
        let inline CreateIgnoreAsync computation =
            CreateBindAsync computation (fun _ -> unitAsync)

        /// Implement the while loop construct of async computation expressions
        ///   - No initial cancellation check before first execution of guard
        ///   - No initial hijack check before first execution of guard
        ///   - No cancellation check before each execution of guard (see CreateBindAsync)
        ///   - Hijack check before each execution of guard (see CreateBindAsync)
        ///   - Cancellation check before each execution of the body after guard (CreateBindAsync)
        ///   - No hijack check before each execution of the body after guard (see CreateBindAsync)
        ///   - Cancellation check after guard fails (see unitAsync)
        ///   - Hijack check after guard fails (see unitAsync)
        ///   - Apply 'guardFunc' with exception protection (see ProtectCode)
        //
        // Note: There are allocations during loop set up, but no allocations during iterations of the loop
        let CreateWhileAsync guardFunc computation =
            if guardFunc() then
                let mutable whileAsync = Unchecked.defaultof<_>
                whileAsync <- CreateBindAsync computation (fun () -> if guardFunc() then whileAsync else unitAsync)
                whileAsync
            else
                unitAsync

#if REDUCED_ALLOCATIONS_BUT_RUNS_SLOWER
        /// Implement the while loop construct of async computation expressions
        ///   - Initial cancellation check before each execution of guard
        ///   - No initial hijack check before each execution of guard
        ///   - No cancellation check before each execution of the body after guard
        ///   - Hijack check before each execution of the body after guard (see Invoke)
        ///   - Cancellation check after guard fails (see OnSuccess)
        ///   - Hijack check after guard fails (see OnSuccess)
        ///   - Apply 'guardFunc' with exception protection (see ProtectCode)
        //
        // Note: There are allocations during loop set up, but no allocations during iterations of the loop
            // One allocation for While async
            // One allocation for While async context function
            MakeAsync (fun ctxtGuard -> 
               // One allocation for ctxtLoop reference cell
               let mutable ctxtLoop = Unchecked.defaultof<_>
               // One allocation for While recursive closure
               let rec WhileLoop () =
                   if ctxtGuard.IsCancellationRequested then
                       ctxtGuard.OnCancellation ()
                   elif ctxtGuard.ProtectCode guardFunc then
                       Invoke computation ctxtLoop
                   else
                       ctxtGuard.OnSuccess ()
               // One allocation for While body activation context
               ctxtLoop <- ctxtGuard.WithContinuation(WhileLoop)
               WhileLoop ()) 
#endif

        /// Implement the for loop construct of async commputation expressions
        ///   - No initial cancellation check before GetEnumerator call.
        ///   - No initial cancellation check before entering protection of implied try/finally
        ///   - Cancellation check after 'entering' the implied try/finally and before loop
        ///   - Hijack check after 'entering' the implied try/finally and after MoveNext call
        ///   - Do not apply 'GetEnumerator' with exception protection. However for an 'async'
        ///     in an 'async { ... }' the exception protection will be provided by the enclosing
        //      Delay or Bind or similar construct.
        ///   - Apply 'MoveNext' with exception protection
        ///   - Apply 'Current' with exception protection

        // Note: No allocations during iterations of the loop apart from those from
        // applying the loop body to the element
        let CreateForLoopAsync (source: seq<_>) computation =
            CreateUsingAsync (source.GetEnumerator()) (fun ie ->
                CreateWhileAsync
                    (fun () -> ie.MoveNext())
                    (CreateDelayAsync (fun () -> computation ie.Current)))

#if REDUCED_ALLOCATIONS_BUT_RUNS_SLOWER
            CreateUsingAsync (source.GetEnumerator()) (fun ie ->
                // One allocation for While async
                // One allocation for While async context function
                MakeAsync (fun ctxtGuard -> 
                   // One allocation for ctxtLoop reference cell
                   let mutable ctxtLoop = Unchecked.defaultof<_>
                   // Two allocations for protected functions
                   let guardFunc() = ie.MoveNext()
                   let currentFunc() = ie.Current
                   // One allocation for ForLoop recursive closure
                   let rec ForLoop () =
                       if ctxtGuard.IsCancellationRequested then
                           ctxtGuard.OnCancellation ()
                       elif ctxtGuard.ProtectCode guardFunc then
                           let x = ctxtGuard.ProtectCode currentFunc
                           CallThenInvoke ctxtLoop x computation 
                       else
                           ctxtGuard.OnSuccess ()
                   // One allocation for loop activation context
                   ctxtLoop <- ctxtGuard.WithContinuation(ForLoop)
                   ForLoop ())) 
#endif

        ///   - Initial cancellation check
        ///   - Call syncCtxt.Post with exception protection. THis may fail as it is arbitrary user code
        let CreateSwitchToAsync (syncCtxt: SynchronizationContext) =
            MakeAsyncWithCancelCheck (fun ctxt ->
                ctxt.PostWithTrampoline syncCtxt ctxt.cont)

        ///   - Initial cancellation check
        ///   - Create Thread and call Start() with exception protection. We don't expect this
        ///     to fail but protect nevertheless.
        let CreateSwitchToNewThreadAsync() =
            MakeAsyncWithCancelCheck (fun ctxt ->
                ctxt.ProtectCode (fun () -> ctxt.trampolineHolder.StartThreadWithTrampoline ctxt.cont))

        ///   - Initial cancellation check
        ///   - Call ThreadPool.QueueUserWorkItem with exception protection. We don't expect this
        ///     to fail but protect nevertheless.
        let CreateSwitchToThreadPoolAsync() =
            MakeAsyncWithCancelCheck (fun ctxt ->
                ctxt.ProtectCode (fun () -> ctxt.trampolineHolder.QueueWorkItemWithTrampoline ctxt.cont))

        /// Post back to the sync context regardless of which continuation is taken
        ///   - Call syncCtxt.Post with exception protection
        let DelimitSyncContext (ctxt: AsyncActivation<_>) =
            match SynchronizationContext.Current with
            | null -> ctxt
            | syncCtxt ->
                ctxt.WithContinuations(cont = (fun x -> ctxt.PostWithTrampoline syncCtxt (fun () -> ctxt.cont x)),
                                       econt = (fun edi -> ctxt.PostWithTrampoline syncCtxt (fun () -> ctxt.econt edi)),
                                       ccont = (fun cexn -> ctxt.PostWithTrampoline syncCtxt (fun () -> ctxt.ccont cexn)))

        [<Sealed>]
        [<AutoSerializable(false)>]
        type SuspendedAsync<'T>(ctxt: AsyncActivation<'T>) =

            let syncCtxt = SynchronizationContext.Current

            let thread =
                match syncCtxt with
                | null -> null // saving a thread-local access
                | _ -> Thread.CurrentThread

            let trampolineHolder = ctxt.trampolineHolder

            member _.ContinueImmediate res =
                let action () = ctxt.cont res
                let inline executeImmediately () = trampolineHolder.ExecuteWithTrampoline action
                let currentSyncCtxt = SynchronizationContext.Current
                match syncCtxt, currentSyncCtxt with
                | null, null ->
                    executeImmediately ()
                // This logic was added in F# 2.0 though is incorrect from the perspective of
                // how SynchronizationContext is meant to work. However the logic works for
                // mainline scenarios (WinForms/WPF) and for compatibility reasons we won't change it.
                | _ when Object.Equals(syncCtxt, currentSyncCtxt) && thread.Equals Thread.CurrentThread ->
                    executeImmediately ()
                | _ ->
                    trampolineHolder.PostOrQueueWithTrampoline syncCtxt action

            member _.PostOrQueueWithTrampoline res =
                trampolineHolder.PostOrQueueWithTrampoline syncCtxt (fun () -> ctxt.cont res)

        /// A utility type to provide a synchronization point between an asynchronous computation
        /// and callers waiting on the result of that computation.
        ///
        /// Use with care!
        [<Sealed>]
        [<AutoSerializable(false)>]
        type ResultCell<'T>() =

            let mutable result = None

            // The continuations for the result
            let mutable savedConts: list<SuspendedAsync<'T>> = []

            // The WaitHandle event for the result. Only created if needed, and set to null when disposed.
            let mutable resEvent = null

            let mutable disposed = false

            // All writers of result are protected by lock on syncRoot.
            let syncRoot = obj()

            member x.GetWaitHandle() =
                lock syncRoot (fun () ->
                    if disposed then
                        raise (System.ObjectDisposedException("ResultCell"))
                    match resEvent with
                    | null ->
                        // Start in signalled state if a result is already present.
                        let ev = new ManualResetEvent(result.IsSome)
                        resEvent <- ev
                        (ev :> WaitHandle)
                    | ev ->
                        (ev :> WaitHandle))

            member x.Close() =
                lock syncRoot (fun () ->
                    if not disposed then
                        disposed <- true
                        match resEvent with
                        | null -> ()
                        | ev ->
                            ev.Close()
                            resEvent <- null)

            interface IDisposable with
                member x.Dispose() = x.Close()

            member x.GrabResult() =
                match result with
                | Some res -> res
                | None -> failwith "Unexpected no result"

            /// Record the result in the ResultCell.
            member x.RegisterResult (res:'T, reuseThread) =
                let grabbedConts =
                    lock syncRoot (fun () ->
                        // Ignore multiple sets of the result. This can happen, e.g. for a race between a cancellation and a success
                        if x.ResultAvailable then
                            [] // invalidOp "multiple results registered for asynchronous operation"
                        else
                            // In this case the ResultCell has already been disposed, e.g. due to a timeout.
                            // The result is dropped on the floor.
                            if disposed then
                                []
                            else
                                result <- Some res
                                // If the resEvent exists then set it. If not we can skip setting it altogether and it won't be
                                // created
                                match resEvent with
                                | null ->
                                    ()
                                | ev ->
                                    // Setting the event need to happen under lock so as not to race with Close()
                                    ev.Set () |> ignore
                                List.rev savedConts)

                // Run the action outside the lock
                match grabbedConts with
                | [] -> fake()
                | [cont] ->
                    if reuseThread then
                        cont.ContinueImmediate res
                    else
                        cont.PostOrQueueWithTrampoline res
                | otherwise ->
                    otherwise |> List.iter (fun cont -> cont.PostOrQueueWithTrampoline res |> unfake) |> fake

            member x.ResultAvailable = result.IsSome

            /// Await the result of a result cell, without a direct timeout or direct
            /// cancellation. That is, the underlying computation must fill the result
            /// if cancellation or timeout occurs.
            member x.AwaitResult_NoDirectCancelOrTimeout  =
                MakeAsync (fun ctxt ->
                    // Check if a result is available synchronously
                    let resOpt =
                        match result with
                        | Some _ -> result
                        | None ->
                            lock syncRoot (fun () ->
                                match result with
                                | Some _ ->
                                    result
                                | None ->
                                    // Otherwise save the continuation and call it in RegisterResult
                                    savedConts <- (SuspendedAsync<_>(ctxt)) :: savedConts
                                    None
                            )
                    match resOpt with
                    | Some res -> ctxt.cont res
                    | None -> fake()
                )

            member x.TryWaitForResultSynchronously (?timeout) : 'T option =
                // Check if a result is available.
                match result with
                | Some _ as r ->
                    r
                | None ->
                    // Force the creation of the WaitHandle
                    let resHandle = x.GetWaitHandle()
                    // Check again. While we were in GetWaitHandle, a call to RegisterResult may have set result then skipped the
                    // Set because the resHandle wasn't forced.
                    match result with
                    | Some _ as r ->
                        r
                    | None ->
                        // OK, let's really wait for the Set signal. This may block.
                        let timeout = defaultArg timeout Threading.Timeout.Infinite
                        let ok = resHandle.WaitOne(millisecondsTimeout= timeout, exitContext=true)
                        if ok then
                            // Now the result really must be available
                            result
                        else
                            // timed out
                            None


        /// Create an instance of an arbitrary delegate type delegating to the given F# function
        type FuncDelegate<'T>(f) =
            member _.Invoke(sender:obj, a:'T) : unit = ignore sender; f a
            static member Create<'Delegate when 'Delegate :> Delegate>(f) =
                let obj = FuncDelegate<'T>(f)
                let invokeMeth = (typeof<FuncDelegate<'T>>).GetMethod("Invoke", BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance)
                System.Delegate.CreateDelegate(typeof<'Delegate>, obj, invokeMeth) :?> 'Delegate

        [<DebuggerHidden>]
        let QueueAsync cancellationToken cont econt ccont computation =
            let trampolineHolder = TrampolineHolder()
            trampolineHolder.QueueWorkItemWithTrampoline (fun () ->
                let ctxt = AsyncActivation.Create cancellationToken trampolineHolder cont econt ccont
                computation.Invoke ctxt)

        /// Run the asynchronous workflow and wait for its result.
        [<DebuggerHidden>]
        let QueueAsyncAndWaitForResultSynchronously (token:CancellationToken) computation timeout =
            let token, innerCTS =
                // If timeout is provided, we govern the async by our own CTS, to cancel
                // when execution times out. Otherwise, the user-supplied token governs the async.
                match timeout with
                | None -> token, None
                | Some _ ->
                    let subSource = new LinkedSubSource(token)
                    subSource.Token, Some subSource

            use resultCell = new ResultCell<AsyncResult<_>>()
            QueueAsync
                token
                (fun res -> resultCell.RegisterResult(AsyncResult.Ok res, reuseThread=true))
                (fun edi -> resultCell.RegisterResult(AsyncResult.Error edi, reuseThread=true))
                (fun exn -> resultCell.RegisterResult(AsyncResult.Canceled exn, reuseThread=true))
                computation
            |> unfake

            let res = resultCell.TryWaitForResultSynchronously(?timeout = timeout)
            match res with
            | None -> // timed out
                // issue cancellation signal
                if innerCTS.IsSome then innerCTS.Value.Cancel()
                // wait for computation to quiesce; drop result on the floor
                resultCell.TryWaitForResultSynchronously() |> ignore
                // dispose the CancellationTokenSource
                if innerCTS.IsSome then innerCTS.Value.Dispose()
                raise (System.TimeoutException())
            | Some res ->
                match innerCTS with
                | Some subSource -> subSource.Dispose()
                | None -> ()
                res.Commit()

        [<DebuggerHidden>]
        let RunImmediate (cancellationToken:CancellationToken) computation =
            use resultCell = new ResultCell<AsyncResult<_>>()
            let trampolineHolder = TrampolineHolder()

            trampolineHolder.ExecuteWithTrampoline (fun () ->
                let ctxt =
                    AsyncActivation.Create
                        cancellationToken
                        trampolineHolder
                        (fun res -> resultCell.RegisterResult(AsyncResult.Ok res, reuseThread=true))
                        (fun edi -> resultCell.RegisterResult(AsyncResult.Error edi, reuseThread=true))
                        (fun exn -> resultCell.RegisterResult(AsyncResult.Canceled exn, reuseThread=true))
                computation.Invoke ctxt)
            |> unfake

            let res = resultCell.TryWaitForResultSynchronously().Value
            res.Commit()

        [<DebuggerHidden>]
        let RunSynchronously cancellationToken (computation: Async<'T>) timeout =
            // Reuse the current ThreadPool thread if possible.
            match Thread.CurrentThread.IsThreadPoolThread, timeout with
            | true, None -> RunImmediate cancellationToken computation
            | _ -> QueueAsyncAndWaitForResultSynchronously cancellationToken computation timeout

        [<DebuggerHidden>]
        let Start cancellationToken (computation:Async<unit>) =
            QueueAsync
                cancellationToken
                (fun () -> fake())   // nothing to do on success
                (fun edi -> edi.ThrowAny())   // raise exception in child
                (fun _ -> fake())    // ignore cancellation in child
                computation
            |> unfake

        [<DebuggerHidden>]
        let StartWithContinuations cancellationToken (computation:Async<'T>) cont econt ccont =
            let trampolineHolder = TrampolineHolder()
            trampolineHolder.ExecuteWithTrampoline (fun () ->
                let ctxt = AsyncActivation.Create cancellationToken trampolineHolder (cont >> fake) (econt >> fake) (ccont >> fake)
                computation.Invoke ctxt)
            |> unfake

        [<DebuggerHidden>]
        let StartAsTask cancellationToken (computation:Async<'T>) taskCreationOptions =
            let taskCreationOptions = defaultArg taskCreationOptions TaskCreationOptions.None
            let tcs = TaskCompletionSource<_>(taskCreationOptions)

            // The contract:
            //      a) cancellation signal should always propagate to the computation
            //      b) when the task IsCompleted -> nothing is running anymore
            let task = tcs.Task
            QueueAsync
                cancellationToken
                (fun r -> tcs.SetResult r |> fake)
                (fun edi -> tcs.SetException edi.SourceException |> fake)
                (fun _ -> tcs.SetCanceled() |> fake)
                computation
            |> unfake
            task

        // Call the appropriate continuation on completion of a task
        [<DebuggerHidden>]
        let OnTaskCompleted (completedTask: Task<'T>) (ctxt: AsyncActivation<'T>)  =
            assert completedTask.IsCompleted
            if completedTask.IsCanceled then
                let edi = ExceptionDispatchInfo.Capture(TaskCanceledException completedTask)
                ctxt.econt edi
            elif completedTask.IsFaulted then
                let edi = ExceptionDispatchInfo.RestoreOrCapture completedTask.Exception
                ctxt.econt edi
            else
                ctxt.cont completedTask.Result

        // Call the appropriate continuation on completion of a task.  A cancelled task
        // calls the exception continuation with TaskCanceledException, since it may not represent cancellation of
        // the overall async (they may be governed by different cancellation tokens, or
        // the task may not have a cancellation token at all).
        [<DebuggerHidden>]
        let OnUnitTaskCompleted (completedTask: Task) (ctxt: AsyncActivation<unit>)  =
            assert completedTask.IsCompleted
            if completedTask.IsCanceled then
                let edi = ExceptionDispatchInfo.Capture(TaskCanceledException(completedTask))
                ctxt.econt edi
            elif completedTask.IsFaulted then
                let edi = ExceptionDispatchInfo.RestoreOrCapture completedTask.Exception
                ctxt.econt edi
            else
                ctxt.cont ()

        // Helper to attach continuation to the given task, which is assumed not to be completed.
        // When the task completes the continuation will be run synchronously on the thread
        // completing the task. This will install a new trampoline on that thread and continue the
        // execution of the async there.
        [<DebuggerHidden>]
        let AttachContinuationToTask (task: Task<'T>) (ctxt: AsyncActivation<'T>)  =
            task.ContinueWith(Action<Task<'T>>(fun completedTask -> 
                ctxt.trampolineHolder.ExecuteWithTrampoline (fun () ->
                    OnTaskCompleted completedTask ctxt)
                |> unfake), TaskContinuationOptions.ExecuteSynchronously)
            |> ignore
            |> fake

        // Helper to attach continuation to the given task, which is assumed not to be completed
        // When the task completes the continuation will be run synchronously on the thread
        // completing the task. This will install a new trampoline on that thread and continue the
        // execution of the async there.
        [<DebuggerHidden>]
        let AttachContinuationToUnitTask (task: Task) (ctxt: AsyncActivation<unit>) =
            task.ContinueWith(Action<Task>(fun completedTask ->
                ctxt.trampolineHolder.ExecuteWithTrampoline (fun () ->
                    OnUnitTaskCompleted completedTask ctxt)
                |> unfake), TaskContinuationOptions.ExecuteSynchronously)
            |> ignore 
            |> fake

        /// Removes a registration places on a cancellation token
        let DisposeCancellationRegistration (registration: byref<CancellationTokenRegistration option>) =
            match registration with
            | Some r ->
                registration <- None
                r.Dispose()
            | None -> ()

        /// Cleans up a Timer, helper for Async.Sleep
        let DisposeTimer (timer: byref<Timer option>) =
            match timer with
            | None -> ()
            | Some t -> 
                timer <- None
                t.Dispose()

        /// Unregisters a RegisteredWaitHandle, helper for AwaitWaitHandle
        let UnregisterWaitHandle (rwh: byref<RegisteredWaitHandle option>) =
            match rwh with
            | None -> ()
            | Some r ->
                r.Unregister null |> ignore
                rwh <- None

        /// Unregisters a delegate handler, helper for AwaitEvent
        let RemoveHandler (event: IEvent<_, _>) (del: byref<'Delegate option>) =
            match del with
            | Some d -> 
                del <- None
                event.RemoveHandler d
            | None -> ()

        [<Sealed; AutoSerializable(false)>]
        type AsyncIAsyncResult<'T>(callback: System.AsyncCallback, state:obj) =
             // This gets set to false if the result is not available by the
             // time the IAsyncResult is returned to the caller of Begin
             let mutable completedSynchronously = true

             let mutable disposed = false

             let cts = new CancellationTokenSource()

             let result = new ResultCell<AsyncResult<'T>>()

             member s.SetResult(v: AsyncResult<'T>) =
                 result.RegisterResult(v, reuseThread=true) |> unfake
                 match callback with
                 | null -> ()
                 | d ->
                     // The IASyncResult becomes observable here
                     d.Invoke (s :> System.IAsyncResult)

             member s.GetResult() =
                 match result.TryWaitForResultSynchronously (-1) with
                 | Some (AsyncResult.Ok v) -> v
                 | Some (AsyncResult.Error edi) -> edi.ThrowAny()
                 | Some (AsyncResult.Canceled err) -> raise err
                 | None -> failwith "unreachable"

             member x.IsClosed = disposed

             member x.Close() =
                 if not disposed  then
                     disposed <- true
                     cts.Dispose()
                     result.Close()

             member x.Token = cts.Token

             member x.CancelAsync() = cts.Cancel()

             member x.CheckForNotSynchronous() =
                 if not result.ResultAvailable then
                     completedSynchronously <- false

             interface System.IAsyncResult with
                  member _.IsCompleted = result.ResultAvailable
                  member _.CompletedSynchronously = completedSynchronously
                  member _.AsyncWaitHandle = result.GetWaitHandle()
                  member _.AsyncState = state

             interface System.IDisposable with
                 member x.Dispose() = x.Close()

        module AsBeginEndHelpers =
            let beginAction (computation, callback, state) =
                let aiar = new AsyncIAsyncResult<'T>(callback, state)
                let cont res = aiar.SetResult (AsyncResult.Ok res)
                let econt edi = aiar.SetResult (AsyncResult.Error edi)
                let ccont cexn = aiar.SetResult (AsyncResult.Canceled cexn)
                StartWithContinuations aiar.Token computation cont econt ccont
                aiar.CheckForNotSynchronous()
                (aiar :> IAsyncResult)

            let endAction<'T> (iar:IAsyncResult) =
                match iar with
                | :? AsyncIAsyncResult<'T> as aiar ->
                    if aiar.IsClosed then
                        raise (System.ObjectDisposedException("AsyncResult"))
                    else
                        let res = aiar.GetResult()
                        aiar.Close ()
                        res
                | _ ->
                    invalidArg "iar" (SR.GetString(SR.mismatchIAREnd))

            let cancelAction<'T>(iar:IAsyncResult) =
                match iar with
                | :? AsyncIAsyncResult<'T> as aiar ->
                    aiar.CancelAsync()
                | _ ->
                    invalidArg "iar" (SR.GetString(SR.mismatchIARCancel))

    open AsyncPrimitives

    [<Sealed; CompiledName("FSharpAsyncBuilder")>]
    type AsyncBuilder() =
        member _.Zero () = unitAsync

        member _.Delay generator = CreateDelayAsync generator

        member inline _.Return value = CreateReturnAsync value

        member inline _.ReturnFrom (computation:Async<_>) = computation

        member inline _.Bind (computation, binder) = CreateBindAsync computation binder

        member _.Using (resource, binder) = CreateUsingAsync resource binder

        member _.While (guard, computation) = CreateWhileAsync guard computation

        member _.For (sequence, body) = CreateForLoopAsync sequence body

        member inline _.Combine (computation1, computation2) = CreateSequentialAsync computation1 computation2

        member inline _.TryFinally (computation, compensation) = CreateTryFinallyAsync compensation computation

        member inline _.TryWith (computation, catchHandler) = CreateTryWithAsync catchHandler computation

        // member inline _.TryWithFilter (computation, catchHandler) = CreateTryWithFilterAsync catchHandler computation

    [<AutoOpen>]
    module AsyncBuilderImpl =
        let async = AsyncBuilder()

    [<Sealed; CompiledName("FSharpAsync")>]
    type Async =

        static member CancellationToken = cancellationTokenAsync

        static member CancelCheck () = unitAsync

        static member FromContinuations (callback: ('T -> unit) * (exn -> unit) * (OperationCanceledException -> unit) -> unit) : Async<'T> =
            MakeAsyncWithCancelCheck (fun ctxt ->
                let mutable underCurrentThreadStack = true
                let mutable contToTailCall = None
                let thread = Thread.CurrentThread
                let latch = Latch()
                let once cont x =
                    if not(latch.Enter()) then invalidOp(SR.GetString(SR.controlContinuationInvokedMultipleTimes))
                    if Thread.CurrentThread.Equals thread && underCurrentThreadStack then
                        contToTailCall <- Some(fun () -> cont x)
                    elif Trampoline.ThisThreadHasTrampoline then
                        let syncCtxt = SynchronizationContext.Current
                        ctxt.trampolineHolder.PostOrQueueWithTrampoline syncCtxt (fun () -> cont x) |> unfake
                    else
                        ctxt.trampolineHolder.ExecuteWithTrampoline (fun () -> cont x ) |> unfake
                try
                    callback (once ctxt.cont, (fun exn -> once ctxt.econt (ExceptionDispatchInfo.RestoreOrCapture exn)), once ctxt.ccont)
                with exn ->
                    if not(latch.Enter()) then invalidOp(SR.GetString(SR.controlContinuationInvokedMultipleTimes))
                    let edi = ExceptionDispatchInfo.RestoreOrCapture exn
                    ctxt.econt edi |> unfake

                underCurrentThreadStack <- false

                match contToTailCall with
                | Some k -> k()
                | _ -> fake())

        static member DefaultCancellationToken = defaultCancellationTokenSource.Token

        static member CancelDefaultToken() =
            let cts = defaultCancellationTokenSource
            // set new CancellationTokenSource before calling Cancel - otherwise if Cancel throws token will stay unchanged
            defaultCancellationTokenSource <- new CancellationTokenSource()
            cts.Cancel()
            // we do not dispose the old default CTS - let GC collect it

        static member Catch (computation: Async<'T>) =
            MakeAsync (fun ctxt ->
                // Turn the success or exception into data
                let newCtxt = ctxt.WithContinuations(cont = (fun res -> ctxt.cont (Choice1Of2 res)),
                                                     econt = (fun edi -> ctxt.cont (Choice2Of2 (edi.GetAssociatedSourceException()))))
                computation.Invoke newCtxt)

        static member RunSynchronously (computation: Async<'T>, ?timeout, ?cancellationToken:CancellationToken) =
            let timeout, cancellationToken =
                match cancellationToken with
                | None -> timeout, defaultCancellationTokenSource.Token
                | Some token when not token.CanBeCanceled -> timeout, token
                | Some token -> None, token
            AsyncPrimitives.RunSynchronously cancellationToken computation timeout

        static member Start (computation, ?cancellationToken) =
            let cancellationToken = defaultArg cancellationToken defaultCancellationTokenSource.Token
            AsyncPrimitives.Start cancellationToken computation

        static member StartAsTask (computation, ?taskCreationOptions, ?cancellationToken)=
            let cancellationToken = defaultArg cancellationToken defaultCancellationTokenSource.Token
            AsyncPrimitives.StartAsTask cancellationToken computation taskCreationOptions

        static member StartChildAsTask (computation, ?taskCreationOptions) =
            async { 
                let! cancellationToken = cancellationTokenAsync
                return AsyncPrimitives.StartAsTask cancellationToken computation taskCreationOptions
            }

        static member Parallel (computations: seq<Async<'T>>) =
            Async.Parallel(computations, ?maxDegreeOfParallelism=None)

        static member Parallel (computations: seq<Async<'T>>, ?maxDegreeOfParallelism: int) =
            match maxDegreeOfParallelism with
            | Some x when x < 1 -> raise(System.ArgumentException(String.Format(SR.GetString(SR.maxDegreeOfParallelismNotPositive), x), "maxDegreeOfParallelism"))
            | _ -> ()

            MakeAsyncWithCancelCheck (fun ctxt ->
                // manually protect eval of seq
                let result =
                    try
                        Choice1Of2 (Seq.toArray computations)
                    with exn ->
                        Choice2Of2 (ExceptionDispatchInfo.RestoreOrCapture exn)

                match result with
                | Choice2Of2 edi -> ctxt.econt edi
                | Choice1Of2 [| |] -> ctxt.cont [| |]
                | Choice1Of2 computations ->
                  ctxt.ProtectCode (fun () ->
                    let ctxt = DelimitSyncContext ctxt  // manually resync
                    let mutable count = computations.Length
                    let mutable firstExn = None
                    let results = Array.zeroCreate computations.Length
                    // Attempt to cancel the individual operations if an exception happens on any of the other threads
                    let innerCTS = new LinkedSubSource(ctxt.token)

                    let finishTask remaining =
                        if (remaining = 0) then
                            innerCTS.Dispose()
                            match firstExn with
                            | None -> ctxt.trampolineHolder.ExecuteWithTrampoline (fun () -> ctxt.cont results)
                            | Some (Choice1Of2 exn) -> ctxt.trampolineHolder.ExecuteWithTrampoline (fun () -> ctxt.econt exn)
                            | Some (Choice2Of2 cexn) -> ctxt.trampolineHolder.ExecuteWithTrampoline (fun () -> ctxt.ccont cexn)
                        else
                            fake()

                    // recordSuccess and recordFailure between them decrement count to 0 and
                    // as soon as 0 is reached dispose innerCancellationSource

                    let recordSuccess i res =
                        results.[i] <- res
                        finishTask(Interlocked.Decrement &count)

                    let recordFailure exn =
                        // capture first exception and then decrement the counter to avoid race when
                        // - thread 1 decremented counter and preempted by the scheduler
                        // - thread 2 decremented counter and called finishTask
                        // since exception is not yet captured - finishtask will fall into success branch
                        match Interlocked.CompareExchange(&firstExn, Some exn, None) with
                        | None ->
                            // signal cancellation before decrementing the counter - this guarantees that no other thread can sneak to finishTask and dispose innerCTS
                            // NOTE: Cancel may introduce reentrancy - i.e. when handler registered for the cancellation token invokes cancel continuation that will call 'recordFailure'
                            // to correctly handle this we need to return decremented value, not the current value of 'count' otherwise we may invoke finishTask with value '0' several times
                            innerCTS.Cancel()
                        | _ -> ()
                        finishTask(Interlocked.Decrement &count)

                    // If maxDegreeOfParallelism is set but is higher then the number of tasks we have we set it back to None to fall into the simple
                    // queue all items branch
                    let maxDegreeOfParallelism =
                        match maxDegreeOfParallelism with
                        | None -> None
                        | Some x when x >= computations.Length -> None
                        | Some _ as x -> x

                    // Simple case (no maxDegreeOfParallelism) just queue all the work, if we have maxDegreeOfParallelism set we start that many workers
                    // which will make progress on the actual computations
                    match maxDegreeOfParallelism with
                    | None ->
                        computations |> Array.iteri (fun i p ->
                            QueueAsync
                                    innerCTS.Token
                                    // on success, record the result
                                    (fun res -> recordSuccess i res)
                                    // on exception...
                                    (fun edi -> recordFailure (Choice1Of2 edi))
                                    // on cancellation...
                                    (fun cexn -> recordFailure (Choice2Of2 cexn))
                                    p
                                |> unfake)
                    | Some maxDegreeOfParallelism ->
                        let mutable i = -1
                        let rec worker (trampolineHolder : TrampolineHolder) =
                            if i < computations.Length then
                                let j = Interlocked.Increment &i
                                if j < computations.Length then
                                    if innerCTS.Token.IsCancellationRequested then
                                        let cexn = OperationCanceledException (innerCTS.Token)
                                        recordFailure (Choice2Of2 cexn) |> unfake
                                        worker trampolineHolder |> unfake
                                    else
                                        let taskCtxt =
                                            AsyncActivation.Create
                                                innerCTS.Token
                                                trampolineHolder
                                                (fun res -> recordSuccess j res |> unfake; worker trampolineHolder)
                                                (fun edi -> recordFailure (Choice1Of2 edi) |> unfake; worker trampolineHolder)
                                                (fun cexn -> recordFailure (Choice2Of2 cexn) |> unfake; worker trampolineHolder)
                                        computations.[j].Invoke taskCtxt |> unfake
                            fake()
                        for x = 1 to maxDegreeOfParallelism do
                            let trampolineHolder = TrampolineHolder()
                            trampolineHolder.QueueWorkItemWithTrampoline (fun () ->
                                worker trampolineHolder)
                            |> unfake

                    fake()))

        static member Sequential (computations: seq<Async<'T>>) =
            Async.Parallel(computations, maxDegreeOfParallelism=1)

        static member Choice(computations: Async<'T option> seq) : Async<'T option> =
            MakeAsyncWithCancelCheck (fun ctxt ->
                // manually protect eval of seq
                let result =
                    try
                        Choice1Of2 (Seq.toArray computations)
                    with exn ->
                        Choice2Of2 (ExceptionDispatchInfo.RestoreOrCapture exn)

                match result with
                | Choice2Of2 edi -> ctxt.econt edi
                | Choice1Of2 [| |] -> ctxt.cont None
                | Choice1Of2 computations ->
                     let ctxt = DelimitSyncContext ctxt
                     ctxt.ProtectCode (fun () ->
                        let mutable count = computations.Length
                        let mutable noneCount = 0
                        let mutable someOrExnCount = 0
                        let innerCts = new LinkedSubSource(ctxt.token)

                        let scont (result: 'T option) =
                            let result =
                                match result with
                                | Some _ ->
                                    if Interlocked.Increment &someOrExnCount = 1 then
                                        innerCts.Cancel(); ctxt.trampolineHolder.ExecuteWithTrampoline (fun () -> ctxt.cont result)
                                    else
                                        fake()

                                | None ->
                                    if Interlocked.Increment &noneCount = computations.Length then
                                        innerCts.Cancel(); ctxt.trampolineHolder.ExecuteWithTrampoline (fun () -> ctxt.cont None)
                                    else
                                        fake()

                            if Interlocked.Decrement &count = 0 then
                                innerCts.Dispose()

                            result

                        let econt (exn: ExceptionDispatchInfo) =
                            let result =
                                if Interlocked.Increment &someOrExnCount = 1 then
                                    innerCts.Cancel(); ctxt.trampolineHolder.ExecuteWithTrampoline (fun () -> ctxt.econt exn)
                                else
                                    fake()

                            if Interlocked.Decrement &count = 0 then
                                innerCts.Dispose()

                            result

                        let ccont (cexn: OperationCanceledException) =
                            let result =
                                if Interlocked.Increment &someOrExnCount = 1 then
                                    innerCts.Cancel(); ctxt.trampolineHolder.ExecuteWithTrampoline (fun () -> ctxt.ccont cexn)
                                else
                                    fake()

                            if Interlocked.Decrement &count = 0 then
                                innerCts.Dispose()

                            result

                        for computation in computations do
                            QueueAsync innerCts.Token scont econt ccont computation |> unfake

                        fake()))

        /// StartWithContinuations, except the exception continuation is given an ExceptionDispatchInfo
        static member StartWithContinuationsUsingDispatchInfo(computation:Async<'T>, continuation, exceptionContinuation, cancellationContinuation, ?cancellationToken) : unit =
            let cancellationToken = defaultArg cancellationToken defaultCancellationTokenSource.Token
            AsyncPrimitives.StartWithContinuations cancellationToken computation continuation exceptionContinuation cancellationContinuation

        static member StartWithContinuations(computation:Async<'T>, continuation, exceptionContinuation, cancellationContinuation, ?cancellationToken) : unit =
            Async.StartWithContinuationsUsingDispatchInfo(computation, continuation, (fun edi -> exceptionContinuation (edi.GetAssociatedSourceException())), cancellationContinuation, ?cancellationToken=cancellationToken)

        static member StartImmediateAsTask (computation: Async<'T>, ?cancellationToken ) : Task<'T>=
            let cancellationToken = defaultArg cancellationToken defaultCancellationTokenSource.Token
            let ts = TaskCompletionSource<'T>()
            let task = ts.Task
            Async.StartWithContinuations(
                computation,
                (fun k -> ts.SetResult k),
                (fun exn -> ts.SetException exn),
                (fun _ -> ts.SetCanceled()),
                cancellationToken)
            task

        static member StartImmediate(computation:Async<unit>, ?cancellationToken) : unit =
            let cancellationToken = defaultArg cancellationToken defaultCancellationTokenSource.Token
            AsyncPrimitives.StartWithContinuations cancellationToken computation id (fun edi -> edi.ThrowAny()) ignore

        static member Sleep (millisecondsDueTime: int64) : Async<unit> =
            MakeAsyncWithCancelCheck (fun ctxt ->
                let ctxt = DelimitSyncContext ctxt
                let mutable edi = null
                let latch = Latch()
                let mutable timer: Timer option = None
                let mutable registration: CancellationTokenRegistration option = None
                registration <-
                    ctxt.token.Register(Action(fun () ->
                        if latch.Enter() then
                            // Make sure we're not cancelled again
                            DisposeCancellationRegistration &registration 
                            DisposeTimer &timer
                            ctxt.trampolineHolder.ExecuteWithTrampoline (fun () -> ctxt.ccont(OperationCanceledException(ctxt.token))) |> unfake)
                    ) |> Some
                try
                    timer <- new Timer(TimerCallback(fun _ ->
                                        if latch.Enter() then
                                            // Ensure cancellation is not possible beyond this point
                                            DisposeCancellationRegistration &registration 
                                            DisposeTimer &timer
                                            // Now we're done, so call the continuation
                                            ctxt.trampolineHolder.ExecuteWithTrampoline (fun () -> ctxt.cont()) |> unfake),
                                     null, dueTime=millisecondsDueTime, period = -1L) |> Some
                with exn ->
                    if latch.Enter() then
                        // Ensure cancellation is not possible beyond this point
                        DisposeCancellationRegistration &registration 
                        // Prepare to call exception continuation
                        edi <- ExceptionDispatchInfo.RestoreOrCapture exn

                // Call exception continuation if necessary
                match edi with
                | null ->
                    fake()
                | _ ->
                    ctxt.econt edi)

        static member Sleep (millisecondsDueTime: int32) : Async<unit> =
            Async.Sleep (millisecondsDueTime |> int64)

        static member Sleep (dueTime: TimeSpan) =
            if dueTime < TimeSpan.Zero then
                raise (ArgumentOutOfRangeException("dueTime"))
            else
                Async.Sleep (dueTime.TotalMilliseconds |> Checked.int64)

        /// Wait for a wait handle. Both timeout and cancellation are supported
        static member AwaitWaitHandle(waitHandle: WaitHandle, ?millisecondsTimeout:int) =
            MakeAsyncWithCancelCheck (fun ctxt ->
                let millisecondsTimeout = defaultArg millisecondsTimeout Threading.Timeout.Infinite
                if millisecondsTimeout = 0 then
                    let ok = waitHandle.WaitOne(0, exitContext=false)
                    ctxt.cont ok
                else
                    let ctxt = DelimitSyncContext ctxt
                    let mutable edi = null
                    let latch = Latch()
                    let mutable rwh: RegisteredWaitHandle option = None
                    let mutable registration: CancellationTokenRegistration option = None
                    registration <-
                        ctxt.token.Register(Action(fun () ->
                            if latch.Enter() then
                                // Make sure we're not cancelled again
                                DisposeCancellationRegistration &registration

                                UnregisterWaitHandle &rwh

                                // Call the cancellation continuation
                                ctxt.trampolineHolder.ExecuteWithTrampoline (fun () -> ctxt.ccont(OperationCanceledException(ctxt.token))) |> unfake))
                        |> Some

                    try
                         rwh <- ThreadPool.RegisterWaitForSingleObject(waitObject=waitHandle,
                                        callBack=WaitOrTimerCallback(fun _ timeOut ->
                                                    if latch.Enter() then
                                                        // Ensure cancellation is not possible beyond this point
                                                        DisposeCancellationRegistration &registration 
                                                        UnregisterWaitHandle &rwh
                                                        // Call the success continuation
                                                        ctxt.trampolineHolder.ExecuteWithTrampoline (fun () -> ctxt.cont (not timeOut)) |> unfake),
                                        state=null,
                                        millisecondsTimeOutInterval=millisecondsTimeout,
                                        executeOnlyOnce=true)
                                    |> Some
                    with exn ->
                        if latch.Enter() then
                            // Ensure cancellation is not possible beyond this point
                            DisposeCancellationRegistration &registration 
                            // Prepare to call exception continuation
                            edi <- ExceptionDispatchInfo.RestoreOrCapture exn

                    // Call exception continuation if necessary
                    match edi with
                    | null ->
                        fake()
                    | _ ->
                        // Call the exception continuation
                        ctxt.econt edi)

        static member AwaitIAsyncResult(iar: IAsyncResult, ?millisecondsTimeout) =
            async {
                if iar.CompletedSynchronously then
                    return true
                else
                    return! Async.AwaitWaitHandle(iar.AsyncWaitHandle, ?millisecondsTimeout=millisecondsTimeout)
            }

        /// Await and use the result of a result cell. The resulting async doesn't support cancellation
        /// or timeout directly, rather the underlying computation must fill the result if cancellation
        /// or timeout occurs.
        static member AwaitAndBindResult_NoDirectCancelOrTimeout(resultCell: ResultCell<AsyncResult<'T>>) =
            async {
                let! result = resultCell.AwaitResult_NoDirectCancelOrTimeout
                return! CreateAsyncResultAsync result
            }

        /// Await the result of a result cell belonging to a child computation.  The resulting async supports timeout and if
        /// it happens the child computation will be cancelled.   The resulting async doesn't support cancellation
        /// directly, rather the underlying computation must fill the result if cancellation occurs.
        static member AwaitAndBindChildResult(innerCTS: CancellationTokenSource, resultCell: ResultCell<AsyncResult<'T>>, millisecondsTimeout) : Async<'T> =
            match millisecondsTimeout with
            | None | Some -1 ->
                resultCell |> Async.AwaitAndBindResult_NoDirectCancelOrTimeout

            | Some 0 ->
                async { if resultCell.ResultAvailable then
                            let res = resultCell.GrabResult()
                            return res.Commit()
                        else
                            return raise (System.TimeoutException()) }
            | _ ->
                async { 
                    try
                        if resultCell.ResultAvailable then
                            let res = resultCell.GrabResult()
                            return res.Commit()
                        else
                            let! ok = Async.AwaitWaitHandle (resultCell.GetWaitHandle(), ?millisecondsTimeout=millisecondsTimeout)
                            if ok then
                                let res = resultCell.GrabResult()
                                return res.Commit()
                            else // timed out
                                // issue cancellation signal
                                innerCTS.Cancel()
                                // wait for computation to quiesce
                                let! _ = Async.AwaitWaitHandle (resultCell.GetWaitHandle())
                                return raise (System.TimeoutException())
                    finally
                        resultCell.Close() 
                }


        static member FromBeginEnd(beginAction, endAction, ?cancelAction): Async<'T> =
            async {
                let! ct = cancellationTokenAsync
                let resultCell = new ResultCell<_>()

                let latch = Latch()
                let mutable registration: CancellationTokenRegistration option = None
                registration <-
                    ct.Register(Action(fun () ->
                        if latch.Enter() then
                            // Make sure we're not cancelled again
                            DisposeCancellationRegistration &registration 

                            // Call the cancellation function. Ignore any exceptions from the
                            // cancellation function.
                            match cancelAction with
                            | None -> ()
                            | Some cancel ->
                                try cancel() with _ -> ()

                            // Register the cancellation result.
                            let canceledResult = Canceled (OperationCanceledException ct)
                            resultCell.RegisterResult(canceledResult, reuseThread=true) |> unfake))
                    |> Some

                let callback =
                    AsyncCallback(fun iar ->
                        if not iar.CompletedSynchronously then
                            if latch.Enter() then
                                // Ensure cancellation is not possible beyond this point
                                DisposeCancellationRegistration &registration 

                                // Run the endAction and collect its result.
                                let res =
                                    try
                                        Ok(endAction iar)
                                    with exn ->
                                        let edi = ExceptionDispatchInfo.RestoreOrCapture exn
                                        Error edi

                                // Register the result. 
                                resultCell.RegisterResult(res, reuseThread=true) |> unfake)

                let (iar:IAsyncResult) = beginAction (callback, (null:obj))
                if iar.CompletedSynchronously then
                    // Ensure cancellation is not possible beyond this point
                    DisposeCancellationRegistration &registration 
                    return endAction iar
                else
                    // Note: ok to use "NoDirectCancel" here because cancellation has been registered above
                    // Note: ok to use "NoDirectTimeout" here because no timeout parameter to this method
                    return! Async.AwaitAndBindResult_NoDirectCancelOrTimeout resultCell
            }


        static member FromBeginEnd(arg, beginAction, endAction, ?cancelAction): Async<'T> =
            Async.FromBeginEnd((fun (iar, state) -> beginAction(arg, iar, state)), endAction, ?cancelAction=cancelAction)

        static member FromBeginEnd(arg1, arg2, beginAction, endAction, ?cancelAction): Async<'T> =
            Async.FromBeginEnd((fun (iar, state) -> beginAction(arg1, arg2, iar, state)), endAction, ?cancelAction=cancelAction)

        static member FromBeginEnd(arg1, arg2, arg3, beginAction, endAction, ?cancelAction): Async<'T> =
            Async.FromBeginEnd((fun (iar, state) -> beginAction(arg1, arg2, arg3, iar, state)), endAction, ?cancelAction=cancelAction)

        static member AsBeginEnd<'Arg, 'T> (computation:('Arg -> Async<'T>)) :
                // The 'Begin' member
                ('Arg * System.AsyncCallback * obj -> System.IAsyncResult) *
                // The 'End' member
                (System.IAsyncResult -> 'T) *
                // The 'Cancel' member
                (System.IAsyncResult -> unit) =
                    let beginAction = fun (a1, callback, state) -> AsBeginEndHelpers.beginAction ((computation a1), callback, state)
                    beginAction, AsBeginEndHelpers.endAction<'T>, AsBeginEndHelpers.cancelAction<'T>

        static member AwaitEvent(event:IEvent<'Delegate, 'T>, ?cancelAction) : Async<'T> =
            async {
                let! ct = cancellationTokenAsync
                let resultCell = new ResultCell<_>()
                // Set up the handlers to listen to events and cancellation
                let latch = Latch()
                let mutable registration: CancellationTokenRegistration option = None
                let mutable del: 'Delegate option = None
                registration <- 
                    ct.Register(Action(fun () ->
                        if latch.Enter() then
                            // Make sure we're not cancelled again
                            DisposeCancellationRegistration &registration 

                            // Stop listening to events
                            RemoveHandler event &del

                            // Call the given cancellation routine if we've been given one
                            // Exceptions from a cooperative cancellation are ignored.
                            match cancelAction with
                            | None -> ()
                            | Some cancel ->
                                try cancel() with _ -> ()
                            
                            // Register the cancellation result.
                            resultCell.RegisterResult(Canceled (OperationCanceledException ct), reuseThread=true) |> unfake
                    )) |> Some

                let del =
                    FuncDelegate<'T>.Create<'Delegate>(fun eventArgs ->
                        if latch.Enter() then
                            // Ensure cancellation is not possible beyond this point
                            DisposeCancellationRegistration &registration 

                            // Stop listening to events
                            RemoveHandler event &del

                            // Register the successful result.
                            resultCell.RegisterResult(Ok eventArgs, reuseThread=true) |> unfake)

                // Start listening to events
                event.AddHandler del

                // Return the async computation that allows us to await the result
                // Note: ok to use "NoDirectCancel" here because cancellation has been registered above
                // Note: ok to use "NoDirectTimeout" here because no timeout parameter to this method
                return! Async.AwaitAndBindResult_NoDirectCancelOrTimeout resultCell }

        static member Ignore (computation: Async<'T>) = CreateIgnoreAsync computation

        static member SwitchToNewThread() = CreateSwitchToNewThreadAsync()

        static member SwitchToThreadPool() = CreateSwitchToThreadPoolAsync()

        static member StartChild (computation:Async<'T>, ?millisecondsTimeout) =
            async {
                let resultCell = new ResultCell<_>()
                let! ct = cancellationTokenAsync
                let innerCTS = new CancellationTokenSource() // innerCTS does not require disposal
                let mutable ctsRef = innerCTS
                let registration =
                    ct.Register(Action(fun () ->
                        match ctsRef with
                        | null -> ()
                        | otherwise -> otherwise.Cancel()))

                do QueueAsync
                       innerCTS.Token
                       // since innerCTS is not ever Disposed, can call reg.Dispose() without a safety Latch
                       (fun res -> ctsRef <- null; registration.Dispose(); resultCell.RegisterResult (Ok res, reuseThread=true))
                       (fun edi -> ctsRef <- null; registration.Dispose(); resultCell.RegisterResult (Error edi, reuseThread=true))
                       (fun err -> ctsRef <- null; registration.Dispose(); resultCell.RegisterResult (Canceled err, reuseThread=true))
                       computation
                     |> unfake

                return Async.AwaitAndBindChildResult(innerCTS, resultCell, millisecondsTimeout) }

        static member SwitchToContext syncContext =
            async { 
                match syncContext with
                | null ->
                    // no synchronization context, just switch to the thread pool
                    do! Async.SwitchToThreadPool()
                | syncCtxt ->
                    // post the continuation to the synchronization context
                    return! CreateSwitchToAsync syncCtxt 
            }

        static member OnCancel interruption =
            async { 
                let! ct = cancellationTokenAsync
                // latch protects cancellation and disposal contention
                let latch = Latch()
                let mutable registration: CancellationTokenRegistration option = None
                registration <-
                    ct.Register(Action(fun () ->
                            if latch.Enter() then
                                // Make sure we're not cancelled again
                                DisposeCancellationRegistration &registration 
                                try
                                    interruption ()
                                with _ -> ()))
                    |> Some
                let disposer =
                    { new System.IDisposable with
                        member _.Dispose() =
                            // dispose CancellationTokenRegistration only if cancellation was not requested.
                            // otherwise - do nothing, disposal will be performed by the handler itself
                            if not ct.IsCancellationRequested then
                                if latch.Enter() then
                                    // Ensure cancellation is not possible beyond this point
                                    DisposeCancellationRegistration &registration }
                return disposer
            }

        static member TryCancelled (computation: Async<'T>, compensation) =
            CreateWhenCancelledAsync compensation computation

        static member AwaitTask (task:Task<'T>) : Async<'T> =
            MakeAsyncWithCancelCheck (fun ctxt -> 
                if task.IsCompleted then
                    // Run synchronously without installing new trampoline
                    OnTaskCompleted task ctxt
                else
                    // Continue asynchronously, via syncContext if necessary, installing new trampoline
                    let ctxt = DelimitSyncContext ctxt
                    ctxt.ProtectCode (fun () -> AttachContinuationToTask task ctxt))

        static member AwaitTask (task:Task) : Async<unit> =
            MakeAsyncWithCancelCheck (fun ctxt -> 
                if task.IsCompleted then
                    // Continue synchronously without installing new trampoline
                    OnUnitTaskCompleted task ctxt
                else
                    // Continue asynchronously, via syncContext if necessary, installing new trampoline
                    let ctxt = DelimitSyncContext ctxt
                    ctxt.ProtectCode (fun () -> AttachContinuationToUnitTask task ctxt))

    module CommonExtensions =

        type System.IO.Stream with

            [<CompiledName("AsyncRead")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
            member stream.AsyncRead(buffer: byte[], ?offset, ?count) =
                let offset = defaultArg offset 0
                let count  = defaultArg count buffer.Length
                Async.FromBeginEnd (buffer, offset, count, stream.BeginRead, stream.EndRead)

            [<CompiledName("AsyncReadBytes")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
            member stream.AsyncRead count =
                async { 
                    let buffer = Array.zeroCreate count
                    let mutable i = 0
                    while i < count do
                        let! n = stream.AsyncRead(buffer, i, count - i)
                        i <- i + n
                        if n = 0 then
                            raise(System.IO.EndOfStreamException(SR.GetString(SR.failedReadEnoughBytes)))
                    return buffer 
                }

            [<CompiledName("AsyncWrite")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
            member stream.AsyncWrite(buffer:byte[], ?offset:int, ?count:int) =
                let offset = defaultArg offset 0
                let count  = defaultArg count buffer.Length
                Async.FromBeginEnd (buffer, offset, count, stream.BeginWrite, stream.EndWrite)

        type IObservable<'Args> with

            [<CompiledName("AddToObservable")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
            member x.Add(callback: 'Args -> unit) = x.Subscribe callback |> ignore

            [<CompiledName("SubscribeToObservable")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
            member x.Subscribe callback =
                x.Subscribe { new IObserver<'Args> with
                                  member x.OnNext args = callback args
                                  member x.OnError e = ()
                                  member x.OnCompleted() = () }

    module WebExtensions =

        type System.Net.WebRequest with
            [<CompiledName("AsyncGetResponse")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
            member req.AsyncGetResponse() : Async<System.Net.WebResponse>=

                let mutable canceled = false // WebException with Status = WebExceptionStatus.RequestCanceled  can be raised in other situations except cancellation, use flag to filter out false positives

                // Use CreateTryWithFilterAsync to allow propagation of exception without losing stack
                Async.FromBeginEnd(beginAction=req.BeginGetResponse,
                                   endAction = req.EndGetResponse,
                                   cancelAction = fun() -> canceled <- true; req.Abort())
                |> CreateTryWithFilterAsync (fun exn ->
                    match exn with
                    | :? System.Net.WebException as webExn
                            when webExn.Status = System.Net.WebExceptionStatus.RequestCanceled && canceled ->

                        Some (CreateAsyncResultAsync(AsyncResult.Canceled (OperationCanceledException webExn.Message)))
                    | _ ->
                        None)

        type System.Net.WebClient with
            member inline private this.Download(event: IEvent<'T, _>, handler: _ -> 'T, start, result) =
                let downloadAsync =
                    Async.FromContinuations (fun (cont, econt, ccont) ->
                        let userToken = obj()
                        let rec delegate' (_: obj) (args: #ComponentModel.AsyncCompletedEventArgs) =
                            // ensure we handle the completed event from correct download call
                            if userToken = args.UserState then
                                event.RemoveHandler handle
                                if args.Cancelled then
                                    ccont (OperationCanceledException())
                                elif isNotNull args.Error then
                                    econt args.Error
                                else
                                    cont (result args)
                        and handle = handler delegate'
                        event.AddHandler handle
                        start userToken
                    )

                async {
                    use! _holder = Async.OnCancel(fun _ -> this.CancelAsync())
                    return! downloadAsync
                }

            [<CompiledName("AsyncDownloadString")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
            member this.AsyncDownloadString (address:Uri) : Async<string> =
                this.Download(
                    event   = this.DownloadStringCompleted,
                    handler = (fun action    -> Net.DownloadStringCompletedEventHandler action),
                    start   = (fun userToken -> this.DownloadStringAsync(address, userToken)),
                    result  = (fun args      -> args.Result)
                )

            [<CompiledName("AsyncDownloadData")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
            member this.AsyncDownloadData (address:Uri) : Async<byte[]> =
                this.Download(
                    event   = this.DownloadDataCompleted,
                    handler = (fun action    -> Net.DownloadDataCompletedEventHandler action),
                    start   = (fun userToken -> this.DownloadDataAsync(address, userToken)),
                    result  = (fun args      -> args.Result)
                )

            [<CompiledName("AsyncDownloadFile")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
            member this.AsyncDownloadFile (address:Uri, fileName:string) : Async<unit> =
                this.Download(
                    event   = this.DownloadFileCompleted,
                    handler = (fun action    -> ComponentModel.AsyncCompletedEventHandler action),
                    start   = (fun userToken -> this.DownloadFileAsync(address, fileName, userToken)),
                    result  = (fun _         -> ())
                )
