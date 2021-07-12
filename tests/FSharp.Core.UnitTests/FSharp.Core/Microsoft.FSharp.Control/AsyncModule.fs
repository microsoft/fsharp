// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

// Various tests for the:
// Microsoft.FSharp.Control.Async module

namespace FSharp.Core.UnitTests.Control

open System
open System.Threading
open FSharp.Core.UnitTests.LibraryTestFx
open Xunit
open FsCheck

module Utils =
    let internal memoizeAsync f =
        let cache = System.Collections.Concurrent.ConcurrentDictionary<'a, System.Threading.Tasks.Task<'b>>()
        fun (x: 'a) -> // task.Result serialization to sync after done.
            cache.GetOrAdd(x, fun x -> f(x) |> Async.StartAsTask) |> Async.AwaitTask

type [<Struct>] Dummy (x: int) =
  member this.X = x
  interface IDisposable with
    member this.Dispose () = ()


[<AutoOpen>]
module ChoiceUtils =

    // FsCheck driven Async.Choice specification test

    exception ChoiceExn of index:int

    /// represents a child computation of a choice workflow
    type ChoiceOp =
        | NoneResultAfter of timeout:int
        | SomeResultAfter of timeout:int
        | ExceptionAfter of timeout:int

        member c.Timeout =
            match c with
            | NoneResultAfter t -> t
            | SomeResultAfter t -> t
            | ExceptionAfter t -> t

    /// represent a choice workflow
    type ChoiceWorkflow = ChoiceWorkflow of children:ChoiceOp list * cancelAfter:int option

    /// normalizes random timeout arguments
    let normalize (ChoiceWorkflow(ops, cancelAfter)) =
        let ms t = 2000 * (abs t % 15) // timeouts only positive multiples of 2 seconds, up to 30 seconds
        let mkOp op =
            match op with
            | NoneResultAfter t -> NoneResultAfter (ms t)
            | SomeResultAfter t -> SomeResultAfter (ms t)
            | ExceptionAfter t -> ExceptionAfter (ms t)

        let ops = ops |> List.map mkOp
        let cancelAfter = cancelAfter |> Option.map ms
        ChoiceWorkflow(ops, cancelAfter)

    /// runs specified choice workflow and checks that
    /// Async.Choice spec is satisfied
    let runChoice (ChoiceWorkflow(ops, cancelAfter)) =
        // Step 1. build a choice workflow from the abstract representation
        let completed = ref 0
        let returnAfter (time: int) f = async {
            do! Async.Sleep time
            let _ = Interlocked.Increment completed
            return f ()
        }

        let mkOp (index : int) = function
            | NoneResultAfter t -> returnAfter t (fun () ->  None)
            | SomeResultAfter t -> returnAfter t (fun () -> Some index)
            | ExceptionAfter t -> returnAfter t (fun () -> raise (ChoiceExn index))

        let choiceWorkflow = ops |> List.mapi mkOp |> Async.Choice

        // Step 2. run the choice workflow and keep the results
        let result =
            let cancellationToken =
                match cancelAfter with
                | Some ca -> 
                    let cts = new CancellationTokenSource()
                    cts.CancelAfter(ca)
                    Some cts.Token
                | None -> None

            try Async.RunSynchronously(choiceWorkflow, ?cancellationToken = cancellationToken) |> Choice1Of2 
            with e -> Choice2Of2 e

        // Step 3. check that results are up to spec
        let getMinTime() =
            seq {
                yield Int32.MaxValue // "infinity": avoid exceptions if list is empty

                for op in ops do 
                    match op with
                    | NoneResultAfter _ -> ()
                    | op -> yield op.Timeout

                match cancelAfter with Some t -> yield t | None -> ()
            } |> Seq.min

        let verifyIndex index =
            if index < 0 || index >= ops.Length then
                Assert.Fail "Returned choice index is out of bounds."
        
        // Step 3a. check that output is up to spec
        match result with
        | Choice1Of2 (Some index) ->
            verifyIndex index
            match ops.[index] with
            | SomeResultAfter timeout -> Assert.AreEqual(getMinTime(), timeout)
            | op -> Assert.Fail <| sprintf "Should be 'Some' but got %A" op

        | Choice1Of2 None ->
            Assert.True(ops |> List.forall (function NoneResultAfter _ -> true | _ -> false))

        | Choice2Of2 (:? OperationCanceledException) ->
            match cancelAfter with
            | None -> Assert.Fail "Got unexpected cancellation exception."
            | Some ca -> Assert.AreEqual(getMinTime(), ca)

        | Choice2Of2 (ChoiceExn index) ->
            verifyIndex index
            match ops.[index] with
            | ExceptionAfter timeout -> Assert.AreEqual(getMinTime(), timeout)
            | op -> Assert.Fail <| sprintf "Should be 'Exception' but got %A" op

        | Choice2Of2 e -> Assert.Fail(sprintf "Unexpected exception %O" e)

        // Step 3b. check that nested cancellation happens as expected
        if not <| List.isEmpty ops then
            let minTimeout = getMinTime()
            let minTimeoutOps = ops |> Seq.filter (fun op -> op.Timeout <= minTimeout) |> Seq.length
            Assert.True(!completed <= minTimeoutOps)

module LeakUtils =
    // when testing for liveness, the things that we want to observe must always be created in
    // a nested function call to avoid the GC (possibly) treating them as roots past the last use in the block.
    // We also need something non trivial to dissuade the compiler from inlining in Release builds.
    type ToRun<'a>(f : unit -> 'a) =
        member this.Invoke() = f()
   
    let run (toRun : ToRun<'a>) = toRun.Invoke()

// ---------------------------------------------------

type AsyncModule() =
    
    /// Simple asynchronous task that delays 200ms and returns a list of the current tick count
    let getTicksTask =
        async {
            do! Async.SwitchToThreadPool()
            let tickstamps = ref [] // like timestamps but for ticks :)
            
            for i = 1 to 10 do
                tickstamps := DateTime.UtcNow.Ticks :: !tickstamps
                do! Async.Sleep(20)
                
            return !tickstamps
        }

    let wait (wh : System.Threading.WaitHandle) (timeoutMilliseconds : int) = 
        wh.WaitOne(timeoutMilliseconds, exitContext=false)

    let dispose(d : #IDisposable) = d.Dispose()

    let testErrorAndCancelRace testCaseName computation = 
        for i in 1..20 do
            let cts = new System.Threading.CancellationTokenSource()
            use barrier = new System.Threading.ManualResetEvent(false)
            async { cts.Cancel() } 
            |> Async.Start

            let c = ref 0
            let incr () = System.Threading.Interlocked.Increment(c) |> ignore

            Async.StartWithContinuations(
                computation,
                (fun _ -> failwith (sprintf "Testcase: %s  --- success not expected iterations 1 .. 20 - failed on iteration %d" testCaseName i)),
                (fun _ -> incr()),
                (fun _ -> incr()),
                cts.Token
            )

            wait barrier 100
            |> ignore
            if !c = 2 then Assert.Fail("both error and cancel continuations were called")

    [<Fact>]
    member this.AwaitIAsyncResult() =

        let beginOp, endOp, cancelOp = Async.AsBeginEnd(fun() -> getTicksTask)

        // Begin the async operation and wait
        let operationIAR = beginOp ((), new AsyncCallback(fun iar -> ()), null)
        match Async.AwaitIAsyncResult(operationIAR) |> Async.RunSynchronously with
        | true  -> ()
        | false -> Assert.Fail("Timed out. Expected to succeed.")

        // When the operation has already completed
        let operationIAR = beginOp ((), new AsyncCallback(fun iar -> ()), null)
        sleep(250)
        
        let result = Async.AwaitIAsyncResult(operationIAR) |> Async.RunSynchronously        
        match result with
        | true  -> ()
        | false -> Assert.Fail("Timed out. Expected to succeed.")

        // Now with a timeout
        let operationIAR = beginOp ((), new AsyncCallback(fun iar -> ()), null)
        let result = Async.AwaitIAsyncResult(operationIAR, 1) |> Async.RunSynchronously
        match result with
        | true  -> Assert.Fail("Timeout expected")
        | false -> ()

    [<Fact>]
    member this.``AwaitWaitHandle.Timeout``() = 
        use waitHandle = new System.Threading.ManualResetEvent(false)
        let startTime = DateTime.UtcNow

        let r = 
            Async.AwaitWaitHandle(waitHandle, 500)
            |> Async.RunSynchronously

        Assert.False(r, "Timeout expected")

        let endTime = DateTime.UtcNow
        let delta = endTime - startTime
        Assert.True(delta.TotalMilliseconds < 1100.0, sprintf "Expected faster timeout than %.0f ms" delta.TotalMilliseconds)

    [<Fact>]
    member this.``AwaitWaitHandle.TimeoutWithCancellation``() = 
        use barrier = new System.Threading.ManualResetEvent(false)
        use waitHandle = new System.Threading.ManualResetEvent(false)
        let cts = new System.Threading.CancellationTokenSource()

        Async.AwaitWaitHandle(waitHandle, 5000)
        |> Async.Ignore
        |> fun c -> 
                    Async.StartWithContinuations(
                        c, 
                        (failwithf "Unexpected success %A"), 
                        (failwithf "Unexpected error %A"), 
                        (fun _ -> barrier.Set() |> ignore), 
                        cts.Token
                    )

        // wait a bit then signal cancellation
        let timeout = wait barrier 500
        Assert.False(timeout, "timeout=true is not expected")

        cts.Cancel()

        // wait 10 seconds for completion
        let ok = wait barrier 10000
        if not ok then Assert.Fail("Async computation was not completed in given time")

    [<Fact>]
    member this.``AwaitWaitHandle.DisposedWaitHandle1``() = 
        let wh = new System.Threading.ManualResetEvent(false)
        
        dispose wh
        let test = async {
            try
                let! timeout = Async.AwaitWaitHandle wh
                Assert.Fail(sprintf "Unexpected success %A" timeout)
            with
                | :? ObjectDisposedException -> ()
                | e -> Assert.Fail(sprintf "Unexpected error %A" e)
            }
        Async.RunSynchronously test

    // test is flaky: https://github.com/dotnet/fsharp/issues/11586
    //[<Fact>]
    member this.``OnCancel.RaceBetweenCancellationHandlerAndDisposingHandlerRegistration``() = 
        let test() = 
            use flag = new ManualResetEvent(false)
            use cancelHandlerRegistered = new ManualResetEvent(false)
            let cts = new System.Threading.CancellationTokenSource()
            let go = async {
                use! holder = Async.OnCancel(fun() -> lock flag (fun() -> flag.Set()) |> ignore)
                let _ = cancelHandlerRegistered.Set()
                while true do
                    do! Async.Sleep 50
                }

            Async.Start (go, cancellationToken = cts.Token)
            //wait until we are sure the Async.OnCancel has run:
            Assert.True(cancelHandlerRegistered.WaitOne(TimeSpan.FromSeconds 5.))
            //now cancel:
            cts.Cancel()
            //cancel handler should have run:
            Assert.True(flag.WaitOne(TimeSpan.FromSeconds 5.))

        for _i = 1 to 300 do test()

    // test is flaky: https://github.com/dotnet/fsharp/issues/11586
    //[<Fact>]
    member this.``OnCancel.RaceBetweenCancellationAndDispose``() = 
        let flag = ref 0
        let cts = new System.Threading.CancellationTokenSource()
        let go = async {
            use disp =
                cts.Cancel()
                { new IDisposable with
                    override _.Dispose() = incr flag }
            while true do
                do! Async.Sleep 50
            }
        try
            Async.RunSynchronously (go, cancellationToken = cts.Token)
        with
            :? System.OperationCanceledException -> ()
        Assert.AreEqual(1, !flag)

    // test is flaky: https://github.com/dotnet/fsharp/issues/11586
    //[<Fact>]
    member this.``OnCancel.CancelThatWasSignalledBeforeRunningTheComputation``() = 
        let test() = 
            let cts = new System.Threading.CancellationTokenSource()
            let go e (flag : bool ref) = async {
                let! _ = Async.AwaitWaitHandle e
                use! _holder = Async.OnCancel(fun () -> flag := true)
                while true do
                    do! Async.Sleep 100
                }

            let evt = new System.Threading.ManualResetEvent(false)
            let finish = new System.Threading.ManualResetEvent(false)
            let cancelledWasCalled = ref false
            Async.StartWithContinuations(go evt cancelledWasCalled, ignore, ignore, (fun _ -> finish.Set() |> ignore),  cancellationToken = cts.Token)
            evt.Set() |> ignore
            cts.Cancel()

            let ok = wait finish 3000
            Assert.True(ok, "Computation should be completed")
            Assert.False(!cancelledWasCalled, "Cancellation handler should not be called")

        for _i = 1 to 3 do test()

#if EXPENSIVE
    [<Test; Category("Expensive"); Explicit>]
    member this.``Async.AwaitWaitHandle does not leak memory`` () =
        // This test checks that AwaitWaitHandle does not leak continuations (described in #131),
        // We only test the worst case - when the AwaitWaitHandle is already set.
        use manualResetEvent = new System.Threading.ManualResetEvent(true)
        
        let tryToLeak() = 
            let resource = 
                LeakUtils.ToRun (fun () ->
                    let resource = obj()
                    let work = 
                        async { 
                            let! _ = Async.AwaitWaitHandle manualResetEvent
                            GC.KeepAlive(resource)
                            return ()
                        }

                    work |> Async.RunSynchronously |> ignore
                    WeakReference(resource))
                  |> LeakUtils.run

            Assert.True(resource.IsAlive)

            GC.Collect()
            GC.WaitForPendingFinalizers()
            GC.Collect()
            GC.WaitForPendingFinalizers()
            GC.Collect()

            Assert.False(resource.IsAlive)
        
        // The leak hangs on a race condition which is really hard to trigger in F# 3.0, hence the 100000 runs...
        for _ in 1..10 do tryToLeak()
#endif

    [<Fact>]
    member this.``AwaitWaitHandle.DisposedWaitHandle2``() = 
        let wh = new System.Threading.ManualResetEvent(false)
        let barrier = new System.Threading.ManualResetEvent(false)

        let test = async {
            let! timeout = Async.AwaitWaitHandle(wh, 10000)
            Assert.False(timeout, "Timeout expected")
            barrier.Set() |> ignore
            }
        Async.Start test

        // await 3 secs then dispose waithandle - nothing should happen
        let timeout = wait barrier 3000
        Assert.False(timeout, "Barrier was reached too early")
        dispose wh
        
        let ok = wait barrier 10000
        if not ok then Assert.Fail("Async computation was not completed in given time")

    [<Fact>]
    member this.``RunSynchronously.NoThreadJumpsAndTimeout``() = 
            let longRunningTask = async { sleep(5000) }
            try
                Async.RunSynchronously(longRunningTask, timeout = 500)
                Assert.Fail("TimeoutException expected")
            with
                :? System.TimeoutException -> ()

    [<Fact>]
    member this.``RunSynchronously.NoThreadJumpsAndTimeout.DifferentSyncContexts``() = 
        let run syncContext =
            let old = System.Threading.SynchronizationContext.Current
            System.Threading.SynchronizationContext.SetSynchronizationContext(syncContext)
            let longRunningTask = async { sleep(5000) }
            let failed = ref false
            try
                Async.RunSynchronously(longRunningTask, timeout = 500)
                failed := true
            with
                :? System.TimeoutException -> ()
            System.Threading.SynchronizationContext.SetSynchronizationContext(old)
            if !failed then Assert.Fail("TimeoutException expected")
        run null
        run (System.Threading.SynchronizationContext())

    [<Fact>]
    member this.``RaceBetweenCancellationAndError.AwaitWaitHandle``() = 
        let disposedEvent = new System.Threading.ManualResetEvent(false)
        dispose disposedEvent
        testErrorAndCancelRace "RaceBetweenCancellationAndError.AwaitWaitHandle" (Async.AwaitWaitHandle disposedEvent)

    [<Fact>]
    member this.``RaceBetweenCancellationAndError.Sleep``() =
        testErrorAndCancelRace "RaceBetweenCancellationAndError.Sleep" (Async.Sleep (-5))

#if EXPENSIVE
#if NET46
    [<Test; Category("Expensive"); Explicit>] // takes 3 minutes!
    member this.``Async.Choice specification test``() =
        ThreadPool.SetMinThreads(100,100) |> ignore
        Check.One ({Config.QuickThrowOnFailure with EndSize = 20}, normalize >> runChoice)
#endif
#endif

    [<Fact>]
    member this.``dispose should not throw when called on null``() =
        let result = async { use x = null in return () } |> Async.RunSynchronously

        Assert.AreEqual((), result)

    [<Fact>]
    member this.``dispose should not throw when called on null struct``() =
        let result = async { use x = new Dummy(1) in return () } |> Async.RunSynchronously

        Assert.AreEqual((), result)

    [<Fact>]
    member this.``error on one workflow should cancel all others``() =
        let counter = 
            async {
                let counter = ref 0
                let job i = async { 
                    if i = 55 then failwith "boom" 
                    else 
                        do! Async.Sleep 1000 
                        incr counter 
                }

                let! _ = Async.Parallel [ for i in 1 .. 100 -> job i ] |> Async.Catch
                do! Async.Sleep 5000
                return !counter
            } |> Async.RunSynchronously

        Assert.AreEqual(0, counter)

    [<Fact>]
    member this.``AwaitWaitHandle.ExceptionsAfterTimeout``() = 
        let wh = new System.Threading.ManualResetEvent(false)
        let test = async {
            try
                let! timeout = Async.AwaitWaitHandle(wh, 1000)
                do! Async.Sleep 500
                raise (new InvalidOperationException("EXPECTED"))
                return Assert.Fail("Should not get here")
            with
                :? InvalidOperationException as e when e.Message = "EXPECTED" -> return ()
            }
        Async.RunSynchronously(test)
        
    [<Fact>]
    member this.``FromContinuationsCanTailCallCurrentThread``() = 
        let cnt = ref 0
        let origTid = System.Threading.Thread.CurrentThread.ManagedThreadId 
        let finalTid = ref -1
        let rec f n =
            if n = 0 then
                async { 
                    finalTid := System.Threading.Thread.CurrentThread.ManagedThreadId
                    return () }
            else
                async {
                    incr cnt
                    do! Async.FromContinuations(fun (k,_,_) -> k())
                    do! f (n-1) 
                }
        // 5000 is big enough that does-not-stackoverflow means we are tailcalling thru FromContinuations
        f 5000 |> Async.StartImmediate 
        Assert.AreEqual(origTid, !finalTid)
        Assert.AreEqual(5000, !cnt)

    [<Fact>]
    member this.``AwaitWaitHandle With Cancellation``() = 
        let run wh = async {
            let! r = Async.AwaitWaitHandle wh
            Assert.True(r, "Timeout not expected")
            return() 
            }
        let test () = 
            let wh = new System.Threading.ManualResetEvent(false)
            let cts = new System.Threading.CancellationTokenSource()
            let asyncs =
                [ 
                  yield! List.init 100 (fun _ -> run wh)
                  yield async { cts.Cancel() }
                  yield async { wh.Set() |> ignore }
                ]
            try
                asyncs
                  |> Async.Parallel
                  |> fun c -> Async.RunSynchronously(c, cancellationToken = cts.Token)
                  |> ignore
            with
                :? System.OperationCanceledException -> () // OK
        for _ in 1..1000 do test()

    [<Fact>]
    member this.``StartWithContinuationsVersusDoBang``() = 
        // worthwhile to note these three
        // case 1
        let r = ref ""
        async {
            try
                do! Async.FromContinuations(fun (s, _, _) -> s())
                return failwith "boom"
            with
                e-> r := e.Message 
            } |> Async.RunSynchronously 
        Assert.AreEqual("boom", !r)
        // case 2
        r := ""
        try
            Async.StartWithContinuations(Async.FromContinuations(fun (s, _, _) -> s()), (fun () -> failwith "boom"), (fun e -> r := e.Message), (fun oce -> ()))
        with
            e -> r := "EX: " + e.Message
        Assert.AreEqual("EX: boom", !r)
        // case 3
        r := ""
        Async.StartWithContinuations(async { return! failwith "boom" }, (fun x -> ()), (fun e -> r := e.Message), (fun oce -> ()))
        Assert.AreEqual("boom", !r)


#if IGNORED
    [<Test; Ignore("See https://github.com/Microsoft/visualfsharp/issues/4887")>]
    member this.``SleepContinuations``() = 
        let okCount = ref 0
        let errCount = ref 0
        let test() = 
            let cts = new System.Threading.CancellationTokenSource() 
 
            System.Threading.ThreadPool.QueueUserWorkItem(fun _-> 
                System.Threading.Thread.Sleep 50 
                try 
                    Async.StartWithContinuations( 
                        Async.Sleep(1000), 
                        (fun _ -> printfn "ok"; incr okCount), 
                        (fun _ -> printfn "error"; incr errCount), 
                        (fun _ -> printfn "cancel"; failwith "BOOM!"), 
                        cancellationToken = cts.Token 
                    ) 
                with _ -> () 
            ) |> ignore 
            System.Threading.Thread.Sleep 50 
            try cts.Cancel() with _ -> () 
            System.Threading.Thread.Sleep 1500 
            printfn "====" 
        for i = 1 to 3 do test()
        Assert.AreEqual(0, !okCount)
        Assert.AreEqual(0, !errCount)
#endif

    [<Fact>]
    member this.``Async caching should work``() = 
        let x = ref 0
        let someSlowFunc _mykey = async { 
            Console.WriteLine "Simulated downloading..."
            do! Async.Sleep 400
            Console.WriteLine "Simulated downloading Done."
            x := !x + 1 // Side effect!
            return "" }

        let memFunc : string -> Async<string> = Utils.memoizeAsync <| someSlowFunc

        async {
            Console.WriteLine "Do the same memoized thing many ways...."
            do! memFunc "a" |> Async.Ignore
            do! memFunc "a" |> Async.Ignore
            do! memFunc "a" |> Async.Ignore
            do! [|1 .. 30|] |> Seq.map(fun _ -> (memFunc "a")) 
                |> Async.Parallel |> Async.Ignore

            Console.WriteLine "Still more ways...."
            for _i = 1 to 30 do
                Async.Start( memFunc "a" |> Async.Ignore )
                Async.Start( memFunc "a" |> Async.Ignore )
            do! Async.Sleep 500
            do! memFunc "a" |> Async.Ignore
            do! memFunc "a" |> Async.Ignore
            Console.WriteLine "Still more ways again...."
            for _i = 1 to 30 do
                Async.Start( memFunc "a" |> Async.Ignore )

            Console.WriteLine "Still more ways again again...."
            do! [|1 .. 30|] |> Seq.map(fun _ -> (memFunc "a")) 
                |> Async.Parallel |> Async.Ignore
        } |> Async.RunSynchronously
        Console.WriteLine "Checking result...."
        Assert.AreEqual(1, !x)

    [<Fact>]
    member this.``Parallel with maxDegreeOfParallelism`` () =
        let mutable i = 1
        let action j = async {
            do! Async.Sleep 1
            Assert.Equal(j, i)
            i <- i + 1
        }
        let computation =
            [| for i in 1 .. 1000 -> action i |]
            |> fun cs -> Async.Parallel(cs, 1)
        Async.RunSynchronously(computation) |> ignore

    [<Fact>]
    member this.``maxDegreeOfParallelism can not be 0`` () =
        try
            [| for i in 1 .. 10 -> async { return i } |]
            |> fun cs -> Async.Parallel(cs, 0)
            |> ignore
            Assert.Fail("Unexpected success")
        with
        | :? System.ArgumentException as exc ->
            Assert.AreEqual("maxDegreeOfParallelism", exc.ParamName)
            Assert.True(exc.Message.Contains("maxDegreeOfParallelism must be positive, was 0"))

    [<Fact>]
    member this.``maxDegreeOfParallelism can not be negative`` () =
        try
            [| for i in 1 .. 10 -> async { return i } |]
            |> fun cs -> Async.Parallel(cs, -1)
            |> ignore
            Assert.Fail("Unexpected success")
        with
        | :? System.ArgumentException as exc ->
            Assert.AreEqual("maxDegreeOfParallelism", exc.ParamName)
            Assert.True(exc.Message.Contains("maxDegreeOfParallelism must be positive, was -1"))

    [<Fact>]
    member this.``RaceBetweenCancellationAndError.Parallel(maxDegreeOfParallelism)``() =
        [| for i in 1 .. 1000 -> async { failwith "boom" } |]
        |> fun cs -> Async.Parallel(cs, 1)
        |> testErrorAndCancelRace "RaceBetweenCancellationAndError.Parallel(maxDegreeOfParallelism)"

    [<Fact>]
    member this.``RaceBetweenCancellationAndError.Parallel``() =
        [| for i in 1 .. 1000 -> async { failwith "boom" } |]
        |> fun cs -> Async.Parallel(cs)
        |> testErrorAndCancelRace "RaceBetweenCancellationAndError.Parallel"

    [<Fact>]
    member this.``error on one workflow should cancel all others with maxDegreeOfParallelism``() =
        let counter =
            async {
                let counter = ref 0
                let job i = async {
                    if i = 55 then failwith "boom"
                    else
                        incr counter
                }

                let! _ = Async.Parallel ([ for i in 1 .. 100 -> job i ], 1) |> Async.Catch
                return !counter
            } |> Async.RunSynchronously

        Assert.AreEqual(54, counter)

    [<Fact>]
    member this.``async doesn't do cancel check between do! and try-finally``() =
        let gate = obj()
        for i in 0..10 do
            let procCount = 3
            use semaphore = new SemaphoreSlim(procCount-1)
            printfn "Semaphore count available: %i" semaphore.CurrentCount
            let mutable acquiredCount = 0
            let mutable releaseCount = 0
            try
                List.init procCount (fun index ->
                    async {
                        lock gate <| fun () -> printfn "[%i] Waiting to enter semaphore" index
                        let! cancellationToken = Async.CancellationToken

                        // The semaphore lets two threads through at a time
                        do! semaphore.WaitAsync(cancellationToken) |> Async.AwaitTask

                        // No implicit cancellation checks should take place between a do! and a try
                        // if there are no other async control constructs present.  If there is synchronous code
                        // it runs without cancellation checks
                        //
                        // index 1 will enter the try/finally quickly, call failwith and cancel the other tasks
                        // One of index 2 and index 3 will be stuck here before the try/finally. But having got
                        // this far it should enter the try/finally before cancellation takes effect
                        do 
                          lock gate <| fun () -> printfn "[%i] Acquired semaphore" index
                          Interlocked.Increment(&acquiredCount) |> ignore
                          if index <> 0 then 
                              lock gate <| fun () -> printfn "[%i] Slowly entering try/finally" index
                              System.Threading.Thread.Sleep(100)

                        try
                            lock gate <| fun () -> printfn "[%i] Within try-finally" index
                            if index = 0 then 
                                lock gate <| fun () -> printfn "[%i] Error" index
                                // The failure will cause others to cancel
                                failwith "Something bad happened!"
                        finally
                            semaphore.Release() |> ignore
                            // This should always get executed
                            Interlocked.Increment(&releaseCount) |> ignore
                            lock gate <| fun () -> printfn "[%i] Semaphore released" index
                    })
                |> Async.Parallel
                |> Async.Ignore
                |> Async.RunSynchronously
            with
            | exn ->
                lock gate <| fun () -> printfn "Unhandled exception: %s" exn.Message
                lock gate <| fun () -> printfn "Semaphore count available: %i" semaphore.CurrentCount
            Assert.AreEqual(acquiredCount, releaseCount)
