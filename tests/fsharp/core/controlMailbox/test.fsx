// #Regression #Conformance #ComputationExpressions #Async #Regression #Events #Stress 
#if TESTS_AS_APP
module Core_controlMailBox
#endif
#light

#nowarn "40" // recursive references

#if NETCOREAPP
open System.Threading.Tasks
#endif

let biggerThanTrampoliningLimit = 10000

let failuresFile =
   let f = System.Environment.GetEnvironmentVariable("CONTROL_FAILURES_LOG")
   match f with
   | "" | null -> "failures.log"
   | _ -> f

let log msg = 
  printfn "%s" msg
  System.IO.File.AppendAllText(failuresFile, sprintf "%A: %s\r\n" System.DateTime.Now msg)

let mutable failures = []
let syncObj = new obj()
let report_failure s = 
  stderr.WriteLine " NO"; 
  lock syncObj (fun () ->
     failures <- s :: failures;
     log (sprintf "FAILURE: %s failed" s)
  )

#if !NETCOREAPP
System.AppDomain.CurrentDomain.UnhandledException.AddHandler(
       fun _ (args:System.UnhandledExceptionEventArgs) ->
          lock syncObj (fun () ->
                let e = args.ExceptionObject :?> System.Exception
                printfn "Exception: %s at %s" (e.ToString()) e.StackTrace
                failures <- (args.ExceptionObject :?> System.Exception).ToString() :: failures
             )
)
#endif

let test s b = stderr.Write(s:string);  if b then stderr.WriteLine " OK" else report_failure s 

let checkQuiet s x1 x2 = 
    if x1 <> x2 then 
        (test s false; 
         log (sprintf "expected: %A, got %A" x2 x1))

let check s x1 x2 = 
    if x1 = x2 then test s true
    else (test s false; log (sprintf "expected: %A, got %A" x2 x1))

open Microsoft.FSharp.Control
open Microsoft.FSharp.Control.WebExtensions

module MailboxProcessorBasicTests = 
    let test() =
        check 
            "c32398u6: MailboxProcessor null"
            (let mb1 = new MailboxProcessor<int>(fun inbox -> async { return () })
             mb1.Start();
             100)
            100


        check 
            "c32398u6: MailboxProcessor null"
            (let mb1 = new MailboxProcessor<int>(fun inbox -> async { return () })
             mb1.StartImmediate();
             100)
            100


        check 
            "c32398u7: MailboxProcessor Receive/PostAndReply"
            (let mb1 = new MailboxProcessor<AsyncReplyChannel<int>>(fun inbox -> async { let! msg = inbox.Receive() 
                                                                                         do msg.Reply(100) })
             mb1.Start();
             mb1.PostAndReply(fun replyChannel -> replyChannel))
            100


        for timeout in [-1;0;10] do
            check 
                (sprintf "c32398u7: MailboxProcessor Receive/TryPostAndReply, timeout = %d" timeout)
                (let mb1 = new MailboxProcessor<AsyncReplyChannel<int>>(fun inbox -> async { let! msg = inbox.Receive() 
                                                                                             do msg.Reply(100) })
                 mb1.Start();
                 mb1.PostAndReply(fun replyChannel -> replyChannel))
                100

        check 
            "c32398u8: MailboxProcessor Receive/PostAndReply"
            (let mb1 = new MailboxProcessor<AsyncReplyChannel<int>>(fun inbox -> async { let! replyChannel1 = inbox.Receive() 
                                                                                         do replyChannel1.Reply(100)
                                                                                         let! replyChannel2 = inbox.Receive() 
                                                                                         do replyChannel2.Reply(200)  })
             mb1.Start();
             let reply1 = mb1.PostAndReply(fun replyChannel -> replyChannel)
             let reply2 = mb1.PostAndReply(fun replyChannel -> replyChannel)
             reply1, reply2)
            (100,200)

        check 
            "c32398u9: MailboxProcessor TryReceive/PostAndReply"
            (let mb1 = new MailboxProcessor<AsyncReplyChannel<int>>(fun inbox -> 
                              async { let! msgOpt = inbox.TryReceive()
                                      match msgOpt with
                                      | Some(replyChannel) -> 
                                         do replyChannel.Reply(100)
                                         let! msgOpt = inbox.TryReceive() 
                                         match msgOpt with 
                                         | Some(replyChannel2) ->  
                                             do replyChannel2.Reply(200) 
                                         | None -> 
                                            do failwith "failure"
                                      | None -> 
                                         do failwith "failure" })
             mb1.Start();
             let reply1 = mb1.PostAndReply(fun replyChannel -> replyChannel)
             let reply2 = mb1.PostAndReply(fun replyChannel-> replyChannel)
             reply1, reply2)
            (100,200)

        for timeout in [0;10] do
            check 
                (sprintf "c32398u10: MailboxProcessor TryReceive/PostAndReply with TryReceive timeout, timeout=%d" timeout)
                (let mb1 = new MailboxProcessor<AsyncReplyChannel<int>>(fun inbox -> 
                                  async { let! msgOpt = inbox.TryReceive()
                                          match msgOpt with
                                          | Some(replyChannel) -> 
                                             let! msgOpt = inbox.TryReceive(timeout=timeout) 
                                             match msgOpt with 
                                             | Some(_) ->  
                                                 do replyChannel.Reply(200) 
                                             | None -> 
                                                 do replyChannel.Reply(100)
                                          | None -> 
                                             do failwith "failure" })
                 mb1.Start();
                 let reply1 = mb1.PostAndReply(fun replyChannel -> replyChannel)
                 reply1)
                100


        for timeout in [-1;0;10] do
            check 
                (sprintf "c32398u: MailboxProcessor TryReceive/PostAndReply with Receive timeout, timeout=%d" timeout)
                (let mb1 = new MailboxProcessor<AsyncReplyChannel<int>>(fun inbox -> 
                                  async { let! msgOpt = inbox.TryReceive()
                                          match msgOpt with
                                          | Some(replyChannel) -> 
                                              try let! _ = inbox.Receive(timeout=10) 
                                                  do failwith "Receive should have timed out"
                                              with _ -> 
                                                  do replyChannel.Reply(200) 
                                          | None -> 
                                              do failwith "failure" })
                 mb1.Start();
                 let reply1 = mb1.PostAndReply(fun replyChannel -> replyChannel)
                 reply1)
                200


        for timeout in [-1;0;10] do
            check 
                (sprintf "c32398u: MailboxProcessor TryReceive/PostAndReply with Scan timeout, timeout=%d" timeout)
                (let mb1 = new MailboxProcessor<AsyncReplyChannel<int>>(fun inbox -> 
                                  async { let! msgOpt = inbox.TryReceive()
                                          match msgOpt with
                                          | Some(replyChannel) -> 
                                              try 
                                                  do! inbox.Scan(timeout=10,
                                                                 scanner=(fun _ -> failwith "Scan should have timed out"))
                                              with _ -> 
                                                  do replyChannel.Reply(200) 
                                          | None -> 
                                              do failwith "failure" })
                 mb1.Start();
                 let reply1 = mb1.PostAndReply(fun replyChannel -> replyChannel)
                 reply1)
                200

        for timeout in [-1;0;10] do
            check 
                (sprintf "c32398u: MailboxProcessor TryReceive/PostAndReply with TryScan timeout, timeout=%d" timeout)
                (let mb1 = new MailboxProcessor<AsyncReplyChannel<int>>(fun inbox -> 
                                  async { let! msgOpt = inbox.TryReceive()
                                          match msgOpt with
                                          | Some(replyChannel) -> 
                                              let! scanRes = 
                                                  inbox.TryScan(timeout=10,
                                                                scanner=(fun _ -> failwith "TryScan should have timed out"))
                                              match scanRes with
                                              | None -> do replyChannel.Reply(200) 
                                              | Some _ -> do failwith "TryScan should have failed"
                                          | None -> 
                                              do failwith "failure" })
                 mb1.Start();
                 let reply1 = mb1.PostAndReply(fun replyChannel -> replyChannel)
                 reply1)
                200

        for n in [0; 1; 100; 1000; 100000 ] do
            check 
                (sprintf "c32398u: MailboxProcessor Post/Receive, n=%d" n)
                (let received = ref 0
                 let mb1 = new MailboxProcessor<int>(fun inbox -> 
                    async { for i in 0 .. n-1 do 
                                let! _ = inbox.Receive()
                                do incr received })
                 mb1.Start();
                 for i in 0 .. n-1 do
                     mb1.Post(i)
                 while !received < n do
                     if !received % 100 = 0 then 
                         printfn "received = %d" !received
#if NETCOREAPP
                     Task.Delay(1).Wait()
#else
                     System.Threading.Thread.Sleep(1)
#endif
                 !received)
                n

        for timeout in [0; 10] do
          for n in [0; 1; 100] do
            check 
                (sprintf "c32398u: MailboxProcessor Post/TryReceive, n=%d, timeout=%d" n timeout)
                (let received = ref 0
                 let mb1 = new MailboxProcessor<int>(fun inbox -> 
                    async { while !received < n do 
                                let! msgOpt = inbox.TryReceive(timeout=timeout)
                                match msgOpt with 
                                | None -> 
                                    do if !received % 100 = 0 then 
                                           printfn "timeout!, received = %d" !received
                                | Some _ -> do incr received })
                 mb1.Start();
                 for i in 0 .. n-1 do
#if NETCOREAPP
                     Task.Delay(1).Wait();
#else
                     System.Threading.Thread.Sleep(1)
#endif
                     mb1.Post(i)
                 while !received < n do
                     if !received % 100 = 0 then 
                         printfn "main thread: received = %d" !received
#if NETCOREAPP
                     Task.Delay(1).Wait();
#else
                     System.Threading.Thread.Sleep(1)
#endif
                 !received)
                n

        for i in 1..10 do
            for sleep in [0;1;10] do
                for timeout in [10;1;0] do
                    check 
                       (sprintf "cf72361: MailboxProcessor TryScan w/timeout=%d sleep=%d iteration=%d" timeout sleep i) 
                       (let timedOut = ref None
                        let mb = new MailboxProcessor<int>(fun inbox ->
                                async {
                                        let result = ref None
                                        let count = ref 0
                                        while (!result).IsNone && !count < 5 do
                                            let! curResult = inbox.TryScan((fun i -> if i >= 0 then async { return i } |> Some  else None), timeout=timeout)
                                            result := curResult
                                            count := !count + 1
                                        match !result with
                                        |   None -> 
                                                timedOut := Some true
                                        |   Some i -> 
                                                timedOut := Some false
                                })
                        mb.Start()
                        let w = System.Diagnostics.Stopwatch()
                        w.Start()
                        while w.ElapsedMilliseconds < 1000L && (!timedOut).IsNone do
                            mb.Post(-1)
#if NETCOREAPP
                            Task.Delay(1).Wait();
#else
                            System.Threading.Thread.Sleep(1)
#endif
                        mb.Post(0)
                        !timedOut)
                       (Some true)

        check "cf72361: MailboxProcessor TryScan wo/timeout" 
           (let timedOut = ref None
            let mb = new MailboxProcessor<bool>(fun inbox ->
                    async {
                        let! result = inbox.TryScan((fun i -> if i then async { return () } |> Some  else None))
                        match result with
                        |   None -> timedOut := Some true
                        |   Some () -> timedOut := Some false
                    })
            mb.Start()
            let w = System.Diagnostics.Stopwatch()
            w.Start()
            while w.ElapsedMilliseconds < 100L do
                mb.Post(false)
#if NETCOREAPP
                Task.Delay(0).Wait();
#else
                System.Threading.Thread.Sleep(0)
#endif
            let r = !timedOut
            mb.Post(true)
            r)
            None

module MailboxProcessorErrorEventTests =
    exception Err of int
    let test() =
        // Make sure the event doesn't get raised if no error
        check 
            "c32398u9330: MailboxProcessor Error (0)"
            (let mb1 = new MailboxProcessor<int>(fun inbox -> async { return () })
             let res = ref 100
             mb1.Error.Add(fun _ -> res := 0)
             mb1.Start();
#if NETCOREAPP
             Task.Delay(200).Wait();
#else
             System.Threading.Thread.Sleep(200)
#endif
             !res)
            100

        // Make sure the event does get raised if error
        check 
            "c32398u9331: MailboxProcessor Error (1)"
            (let mb1 = new MailboxProcessor<int>(fun inbox -> async { failwith "fail" })
             let res = ref 0
             mb1.Error.Add(fun _ -> res := 100)
             mb1.Start();
#if NETCOREAPP
             Task.Delay(200).Wait();
#else
             System.Threading.Thread.Sleep(200)
#endif
             !res)
            100

        // Make sure the event does get raised after message receive 
        check 
            "c32398u9332: MailboxProcessor Error (2)"
            (let mb1 = new MailboxProcessor<int>(fun inbox -> 
                                    async { let! msg = inbox.Receive() 
                                            raise (Err msg) })
             let res = ref 0
             mb1.Error.Add(function Err n -> res := n | _ -> check "rwe90r - unexpected error" 0 1)
             mb1.Start();
             mb1.Post 100
#if NETCOREAPP
             Task.Delay(200).Wait();
#else
             System.Threading.Thread.Sleep(200)
#endif
             !res)
            100
        
type msg = Increment of int | Fetch of AsyncReplyChannel<int> | Reset
let mailboxes() = new MailboxProcessor<msg>(fun inbox ->
    let rec loop n =
         async { let! msg = inbox.Receive()
                 match msg with
                 | Increment m -> return! loop (n + m)
                 | Reset -> return! loop 0
                 | Fetch chan -> do chan.Reply(n)
                                 return! loop n }
    loop 0)

// multiple calls to Mailbox.Start
let test5() =
    let mailbox = mailboxes()
    mailbox.Start()
    let res =
        try
            mailbox.Start()
            false
        with _ -> true
    test "test5 - Mailbox.Start" res

let test6() =
    let mailbox = mailboxes()
    mailbox.Start()

    let rec compute n =
        async { do mailbox.Post (Increment n)
                if n <> 0 then
                    let! res = compute (n - 1)
                    return n + res
                else
                    return 0 }

    // asynchronous computations send messages to the mailbox and compute a value
    // the mailbox computes the same thing (using messages)
    // they should get the same result
    let test6a() =
       mailbox.Post(Reset)
       let computations =
           let arr = Async.RunSynchronously (Async.Parallel (List.map compute [1 .. 50]))
           Seq.fold (+) 0 arr

       let mails = mailbox.PostAndReply(fun chan -> Fetch chan)
       test "test6a - mailbox & parallel" (computations = mails)

    // PostAndReply
    let test6b() =
        mailbox.Post(Reset)
        let computations = async {
            let! arr = Async.Parallel (List.map compute [1 .. 100])
            let res = Seq.fold (+) 0 arr
            let mails = mailbox.PostAndReply(fun chan -> Fetch chan)
            do test "test6b - mailbox & PostAndAsyncReply" (res = mails)
        }
        Async.RunSynchronously computations

    // TryPostAndReply
    let test6c() =
        printfn "starting test6c"
        mailbox.Post(Reset)
        let computations = async {
            let! arr = Async.Parallel (List.map compute [1 .. 100])
            let res = Seq.fold (+) 0 arr
            let mails = mailbox.TryPostAndReply(fun chan -> Fetch chan)
            do test "test6c - mailbox & TryPostAndReply" (res = Option.get mails)
        }
        Async.RunSynchronously computations

    // PostAndTryAsyncReply
    let test6d() =
        printfn "starting test6d"
        mailbox.Post(Reset)
        let computations =
            let arr = Async.RunSynchronously (Async.Parallel (List.map compute [1 .. 50]))
            Seq.fold (+) 0 arr

        printfn "running test6d: sent messages OK"
        let mails = mailbox.PostAndTryAsyncReply(fun chan -> Fetch chan) |> Async.RunSynchronously
        test "test6d - mailbox & TryPostAndReply" (computations = Option.get mails)

    // PostAndAsyncReply
    let test6e() =
        printfn "starting test6e"
        mailbox.Post(Reset)
        let computations =
            let arr = Async.RunSynchronously (Async.Parallel (List.map compute [1 .. 50]))
            Seq.fold (+) 0 arr

        let mails = mailbox.PostAndAsyncReply(fun chan -> Fetch chan) |> Async.RunSynchronously
        test "test6d - mailbox & PostAndReply" (computations = mails)

    test6a()
    test6b()
    test6c()
    test6d()
    test6e()

(*
// like 6, with futures
let test7() =
    printfn "starting test7"
    mailbox.Post(Reset)
    let computations =
        let arr = Array.init 50 (fun i -> Async.SpawnFuture (compute i))
        arr |> Seq.fold (fun n f -> n + f.Value) 0

    let mails = mailbox.PostAndReply(fun chan -> Fetch chan)
    test "test7 - mailbox & futures" (computations = mails)
*)


let timeoutboxes str = new MailboxProcessor<'b>(fun inbox ->
    async { for i in 1 .. 10 do
#if NETCOREAPP
                Task.Delay(200).Wait()
#else
                do System.Threading.Thread.Sleep 200
#endif
                })

// Timeout
let timeout_tpar() =
    printfn "started timeout_tpar"
    let tbox = timeoutboxes "timeout_tpar"
    tbox.Start()
    let mails = tbox.TryPostAndReply((fun chan -> Fetch chan), 50)
    test "default timeout & TryPostAndReply" (mails = None)

// Timeout
let timeout_tpar_def() =
    printfn "started timeout_tpar_def"
    let tbox = timeoutboxes "timeout_tpar"
    tbox.Start()
    tbox.DefaultTimeout <- 50
    let mails = tbox.TryPostAndReply((fun chan -> Fetch chan))
    test "timeout & TryPostAndReply" (mails = None)

// Timeout
let timeout_tpara() =
    printfn "started timeout_tpara"
    let tbox = timeoutboxes "timeout_tpara"
    tbox.Start()
    let mails = tbox.PostAndTryAsyncReply((fun chan -> Fetch chan), 50) |> Async.RunSynchronously
    test "timeout & PostAndTryAsyncReply" (mails = None)

// Timeout
let timeout_tpara_def() =
    printfn "started timeout_tpara_def"
    let tbox = timeoutboxes "timeout_tpara_def"
    tbox.Start()
    tbox.DefaultTimeout <- 50
    let mails = tbox.PostAndTryAsyncReply((fun chan -> Fetch chan)) |> Async.RunSynchronously
    test "default timeout & PostAndTryAsyncReply" (mails = None)


// Timeout
let timeout_par() =
    printfn "started timeout_para"
    let tbox = timeoutboxes "timeout_par"
    tbox.Start()
    try tbox.PostAndReply(ignore, 50)
        test "default timeout & PostAndReply" false
    with _ -> test "default timeout & PostAndReply" true

// Timeout
let timeout_par_def() =
    printfn "started timeout_par"
    let tbox = timeoutboxes "timeout_par"
    tbox.Start()
    tbox.DefaultTimeout <- 50
    try tbox.PostAndReply(ignore)
        test "timeout & PostAndReply" false
    with _ -> test "timeout & PostAndReply" true

// Timeout
let timeout_para() =
    printfn "started timeout_para"
    let tbox = timeoutboxes "timeout_para"
    tbox.Start()
    try tbox.PostAndAsyncReply(ignore, 50) |> Async.RunSynchronously |> ignore
        test "timeout & PostAndAsyncReply" false
    with _ -> test "timeout & PostAndAsyncReply" true

// Timeout
let timeout_para_def() =
    printfn "started timeout_para_def"
    let tbox = timeoutboxes "timeout_para_def"
    tbox.Start()
    tbox.DefaultTimeout <- 50
    try tbox.PostAndAsyncReply(ignore) |> Async.RunSynchronously |> ignore
        test "default timeout & PostAndAsyncReply" false
    with _ -> test "default timeout & PostAndAsyncReply" true

// Useful class: put "checkpoints" in the code.
// Check they are called in the right order.
type Path(str) =
    let mutable current = 0
    member p.Check n = check (str + " #" + string (current+1)) n (current+1)
                       current <- n



module LotsOfMessages = 
    let test () =
        let N = 200000
        let count = ref N

        let logger = MailboxProcessor<_>.Start(fun inbox ->
            let rec run i = 
                async { let! s = inbox.Receive()
                        decr count
                        return! run (i + 1) }
            run 0)
    
        printfn "filling queue with 200K messages..."
        
        for i = 0 to 200000 do
            logger.Post("Message!")

        let mutable queueLength = logger.CurrentQueueLength 
    
        while !count > 0  do 
            printfn "waiting for queue to drain... Done when %d reaches 0" !count
            check "celrv09ervkn" (queueLength >= logger.CurrentQueueLength) true
            queueLength <- logger.CurrentQueueLength 
    
#if NETCOREAPP
            Task.Delay(10).Wait()
#else
            System.Threading.Thread.Sleep(10)
#endif
        check "celrv09ervknf3ew" logger.CurrentQueueLength 0

let RunAll() = 
    MailboxProcessorBasicTests.test()
    MailboxProcessorErrorEventTests.test()
    test5()
    test6()
    timeout_para()
    timeout_par()
    timeout_para_def()
    timeout_par_def()
    timeout_tpara()
    timeout_tpar()
    timeout_tpara_def()
    timeout_tpar_def()
    // ToDo: 7/31/2008: Disabled because of probable timing issue.  QA needs to re-enable post-CTP.
    // Tracked by bug FSharp 1.0:2891
    //test15()
    // ToDo: 7/31/2008: Disabled because of probable timing issue.  QA needs to re-enable post-CTP.
    // Tracked by bug FSharp 1.0:2891
    //test15b()
    LotsOfMessages.test()

#if TESTS_AS_APP
let RUN() = RunAll(); failures
#else
RunAll()
let aa =
  if not failures.IsEmpty then 
      stdout.WriteLine "Test Failed"
      exit 1
  else   
      stdout.WriteLine "Test Passed"
      System.IO.File.WriteAllText("test.ok","ok")
      exit 0
#endif
