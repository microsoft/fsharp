
namespace FSharp.Control.Async2

open System.Runtime.CompilerServices

#nowarn "42"
open System
open System.Threading
open System.Threading.Tasks
open FSharp.Core.CompilerServices
open FSharp.Core.CompilerServices.StateMachineHelpers

[<AutoOpen>]
module Utils =
    let verbose = false

    let inline MoveNext(x: byref<'T> when 'T :> IAsyncStateMachine) = x.MoveNext()

[<NoComparison; NoEquality>]
type Async2StateMachineData<'T>() =
    [<DefaultValue(false)>]
    val mutable cancellationToken : CancellationToken
    [<DefaultValue(false)>]
    val mutable result : 'T
    [<DefaultValue(false)>]
    val mutable builder : AsyncTaskMethodBuilder<'T>
    [<DefaultValue(false)>]
    val mutable taken : bool
    //// For tailcalls using 'return!'
    //[<DefaultValue(false)>]
    //val mutable tailcallTarget: IAsync2Invocation<'T> 

and IAsync2Invokable<'T> =
    abstract StartImmediate: CancellationToken -> IAsync2Invocation<'T>

and [<AllowNullLiteral>] IAsync2Invocation<'T> =
    inherit IAsyncStateMachine
    //abstract TailcallTarget: IAsync2Invocation<'T> 
    abstract CancellationToken: CancellationToken
    abstract Task: Task<'T>

and [<AbstractClass; NoEquality; NoComparison>] 
    Async2<'T>() =

    // F# requires that we implement interfaces even on an abstract class
    interface IAsync2Invokable<'T>  with
        member _.StartImmediate(ct) = failwith "abstract"

    interface IAsync2Invocation<'T>  with
        //member _.TailcallTarget = failwith "abstract"
        member _.CancellationToken = failwith "abstract"
        member _.Task = failwith "abstract"
    interface IAsyncStateMachine with 
        member _.MoveNext() = failwith "abstract"
        member _.SetStateMachine(_state) = failwith "abstract"

    member inline x.StartImmediate(ct) = (x :> IAsync2Invokable<'T>).StartImmediate(ct)

and [<NoComparison; NoEquality>]
    Async2<'Machine, 'T  when 'Machine :> IAsyncStateMachine and 'Machine :> IResumableStateMachine<Async2StateMachineData<'T>>>() =
    inherit Async2<'T>()
    let initialThreadId = Environment.CurrentManagedThreadId
    
    [<DefaultValue(false)>]
    val mutable Machine : 'Machine

    //member internal ts.hijack() = (ts :> IAsync2Invocation<_>)
        //let res = ts.Machine.Data.tailcallTarget
        //match res with 
        //| null -> (ts :> IAsync2Invocation<_>)
        //| tg -> 
        //    match (tg :> IAsync2Invocation<_>).TailcallTarget with 
        //    | null -> 
        //        res
        //    | res2 -> 
        //        // Cut out chains of tailcalls
        //        ts.Machine.Data.tailcallTarget <- res2
        //        res2

    interface IAsyncStateMachine with 
        member ts.MoveNext() = 
            MoveNext(&ts.Machine)
            
            //match ts.hijack() with 
            //| null -> 
            //| tg -> (tg :> IAsyncStateMachine).MoveNext()
        
        member ts.SetStateMachine(state) =
            //printfn "SetStateMachine"
            ()
            //ts.Machine.Data.builder.SetStateMachine(state)

    interface IAsync2Invokable<'T> with 
        member ts.StartImmediate(ct) = 
            let data = ts.Machine.Data
            if (not data.taken && initialThreadId = Environment.CurrentManagedThreadId) then
                data.taken <- true
                data.cancellationToken <- ct
                //printfn "creating"
                data.builder <- AsyncTaskMethodBuilder<'T>.Create()
                //printfn "starting"
                data.builder.Start(&ts.Machine)
                (ts :> IAsync2Invocation<_>)
            else
                let clone = ts.MemberwiseClone() :?> Async2<'Machine, 'T>
                data.taken <- true
                clone.Machine.Data.cancellationToken <- ct
                clone.Machine.MoveNext()
                (clone :> IAsync2Invocation<'T>)

    interface IAsync2Invocation<'T> with
        member ts.CancellationToken = ts.Machine.Data.cancellationToken
        member ts.Task = ts.Machine.Data.builder.Task
        //member ts.TailcallTarget = ts.hijack()

and Async2Code<'TOverall, 'T> = ResumableCode<Async2StateMachineData<'TOverall>, 'T>
and Async2StateMachine<'T> = ResumableStateMachine<Async2StateMachineData<'T>>
and Async2ResumptionFunc<'T> = ResumptionFunc<Async2StateMachineData<'T>>
and Async2ResumptionDynamicInfo<'T> = ResumptionDynamicInfo<Async2StateMachineData<'T>>

[<Sealed>]
type Async2Builder() =

    member inline _.Delay(f : unit -> Async2Code<'TOverall, 'T>) : Async2Code<'TOverall, 'T> =
        Async2Code<'TOverall, 'T>(fun sm -> f().Invoke(&sm))

    member inline _.Run(code : Async2Code<'T, 'T>) : Async2<'T> = 
        if __useResumableCode then
            // This is the static implementation.  A new struct type is created.
            __stateMachine<Async2StateMachineData<'T>, Async2<'T>>
                (MoveNextMethodImpl<_>(fun sm -> 
                        //-- RESUMABLE CODE START
                        __resumeAt sm.ResumptionPoint
                        try
                            //printfn "at Run.MoveNext start"
                            //Console.WriteLine("[{0}] resuming by invoking {1}....", sm.MethodBuilder.Task.Id, hashq sm.ResumptionFunc )
                            let __stack_code_fin = code.Invoke(&sm)
                            //printfn $"at Run.MoveNext, __stack_code_fin={__stack_code_fin}"
                            if __stack_code_fin then 
                                //printfn $"at Run.MoveNext, done"
                                sm.Data.builder.SetResult(sm.Data.result)

                        with exn ->
                            //Console.WriteLine("[{0}] SetException {1}", sm.MethodBuilder.Task.Id, exn)
                            sm.Data.builder.SetException(exn)

                        //// tailcall
                        //match sm.Data.tailcallTarget with 
                        //| null -> 
                        //    printfn $"at Run.MoveNext, await"
                        //| tg -> 
                        //    printfn $"at Run.MoveNext, hijack"
                        //    let mutable tg = tg
                        //    MoveNext(&tg) 
                        //-- RESUMABLE CODE END
                    ))
                (SetStateMachineMethodImpl<_>(fun sm state -> ()))
                (AfterCode<_,_>(fun sm -> 
                    let ts = Async2<Async2StateMachine<'T>, 'T>()
                    ts.Machine <- sm
                    ts.Machine.Data <- Async2StateMachineData()
                    ts :> Async2<'T>))
        else
            failwith "no dynamic implementation as yet"
        //    let initialResumptionFunc = Async2ResumptionFunc<'T>(fun sm -> code.Invoke(&sm))
        //    let resumptionFuncExecutor = Async2ResumptionExecutor<'T>(fun sm f -> 
        //            // TODO: add exception handling?
        //            if f.Invoke(&sm) then 
        //                sm.ResumptionPoint <- -2)
        //    let setStateMachine = SetStateMachineMethodImpl<_>(fun sm f -> ())
        //    sm.Machine.ResumptionFuncInfo <- (initialResumptionFunc, resumptionFuncExecutor, setStateMachine)
        //sm.Start()


    [<DefaultValue>]
    member inline _.Zero() : Async2Code<'TOverall, unit> =
        ResumableCode.Zero()

    member inline _.Combine(task1: Async2Code<'TOverall, unit>, task2: Async2Code<'TOverall, 'T>) : Async2Code<'TOverall, 'T> =
        ResumableCode.Combine(task1, task2)
            
    member inline _.WhileAsync([<InlineIfLambda>] condition : unit -> ValueTask<bool>, body : Async2Code<'TOverall, unit>) : Async2Code<'TOverall, unit> =
        let mutable condition_res = true
        ResumableCode.While((fun () -> condition_res), 
            ResumableCode<_,_>(fun sm -> 
                let mutable __stack_condition_fin = true
                let __stack_vtask = condition()
                if __stack_vtask.IsCompleted then
                    __stack_condition_fin <- true
                    condition_res <- __stack_vtask.Result
                else
                    let task = __stack_vtask.AsTask()
                    let mutable awaiter = task.GetAwaiter()
                    // This will yield with __stack_fin = false
                    // This will resume with __stack_fin = true
                    let __stack_yield_fin = ResumableCode.Yield().Invoke(&sm)
                    __stack_condition_fin <- __stack_yield_fin

                    if __stack_condition_fin then 
                        condition_res <- task.Result
                    else
                        sm.Data.builder.AwaitUnsafeOnCompleted(&awaiter, &sm)

                if __stack_condition_fin then 
                    if condition_res then 
                        body.Invoke(&sm)
                    else
                        true
                else
                    false
                ))

    member inline _.While([<InlineIfLambda>] condition : unit -> bool, body : Async2Code<'TOverall, unit>) : Async2Code<'TOverall, unit> =
        ResumableCode.While(condition, body)

    member inline _.TryWith(body : Async2Code<'TOverall, 'T>, catch : exn -> Async2Code<'TOverall, 'T>) : Async2Code<'TOverall, 'T> =
        ResumableCode.TryWith(body, catch)

    member inline internal _.TryFinallyAsync(body: Async2Code<'TOverall, 'T>, compensation : unit -> Task) : Async2Code<'TOverall, 'T> =
        ResumableCode.TryFinallyAsync(body, ResumableCode<_,_>(fun sm -> 
            let mutable __stack_condition_fin = true
            let __stack_vtask = compensation()
            if not __stack_vtask.IsCompleted then
                let mutable awaiter = __stack_vtask.GetAwaiter()
                let __stack_yield_fin = ResumableCode.Yield().Invoke(&sm)
                __stack_condition_fin <- __stack_yield_fin

                if not __stack_condition_fin then 
                    sm.Data.builder.AwaitUnsafeOnCompleted(&awaiter, &sm)

            __stack_condition_fin))

    member inline _.TryFinally(body: Async2Code<'TOverall, 'T>, compensation : unit -> unit) : Async2Code<'TOverall, 'T> =
        ResumableCode.TryFinally(body, ResumableCode<_,_>(fun sm -> compensation(); true))

    member inline this.Using(resource : #IAsyncDisposable, body : #IAsyncDisposable -> Async2Code<'TOverall, 'T>) : Async2Code<'TOverall, 'T> = 
        // A using statement is just a try/finally with the finally block disposing if non-null.
        this.TryFinallyAsync(
            (fun sm -> (body resource).Invoke(&sm)),
            (fun () -> 
                if not (isNull (box resource)) then 
                    resource.DisposeAsync().AsTask()
                else 
                    Task.CompletedTask))

    member inline _.Return (v: 'T) : Async2Code<'T, 'T>  =
        Async2Code<'T, 'T>(fun sm -> 
            sm.Data.result <- v
            true)

    member inline _.Bind (task: Task<'TResult1>, continuation: ('TResult1 -> Async2Code<'TOverall, 'T>)) : Async2Code<'TOverall, 'T> =
        Async2Code<'TOverall, 'T>(fun sm -> 
            let mutable awaiter = task.GetAwaiter()
            let mutable __stack_fin = true
            if not awaiter.IsCompleted then 
                // This will yield with __stack_fin2 = false
                // This will resume with __stack_fin2 = true
                let __stack_fin2 = ResumableCode.Yield().Invoke(&sm)
                __stack_fin <- __stack_fin2

            if __stack_fin then 
                let result = awaiter.GetResult()
                (continuation result).Invoke(&sm)
            else
                sm.Data.builder.AwaitUnsafeOnCompleted(&awaiter, &sm)
                false)

    member inline b.Bind (computation: Async2<'TResult1>, continuation: ('TResult1 -> Async2Code<'TOverall, 'T>)) : Async2Code<'TOverall, 'T> =
        Async2Code<'TOverall, 'T>(fun sm ->
            let ct = sm.Data.cancellationToken
            b.Bind(computation.StartImmediate(ct).Task, continuation).Invoke(&sm))

    member inline b.Bind (computation: Async<'TResult1>, continuation: ('TResult1 -> Async2Code<'TOverall, 'T>)) : Async2Code<'TOverall, 'T> =
        Async2Code<'TOverall, 'T>(fun sm ->
            let ct = sm.Data.cancellationToken
            b.Bind(Async.StartImmediateAsTask(computation, ct), continuation).Invoke(&sm))

    member inline b.ReturnFrom (task: Task<'T>) : Async2Code<'T, 'T> = 
        // No tailcalling to tasks
        b.Bind(task, (fun res -> b.Return(res)))

    member inline b.ReturnFrom (computation: Async<'T>) : Async2Code<'T, 'T> = 
        // No tailcalling to Async
        b.Bind(computation, (fun res -> b.Return(res)))

// TODO - implement RFC for ReturnFromTailcall to make this safe
    member inline b.ReturnFrom (other: Async2<'T>) : Async2Code<'T, 'T> = 
        b.Bind(other, (fun res -> b.Return(res)))
        //Async2Code<'T, _>(fun sm -> 
        //    printfn "setting hijack target and starting"
        //    sm.Data.tailcallTarget <- other
        //    // For tailcalls we return 'false' and re-run from the entry (trampoline)
        //    false 
        //)



[<AutoOpen>]
module Async2 =
    type Async2Builder with
        member inline this.Using(resource : ('TResource :> IDisposable), body : ('TResource -> Async2Code<'TOverall, 'T>)) : Async2Code<'TOverall, 'T> = 
            // A using statement is just a try/finally with the finally block disposing if non-null.
            this.TryFinally(
                (fun sm -> (body resource).Invoke(&sm)),
                (fun () -> if not (isNull (box resource)) then resource.Dispose()))

        member inline _.Bind< ^TaskLike, ^TResult1, 'TResult2, ^Awaiter , 'TOverall
                                            when  ^TaskLike: (member GetAwaiter:  unit ->  ^Awaiter)
                                            and ^Awaiter :> ICriticalNotifyCompletion
                                            and ^Awaiter: (member get_IsCompleted:  unit -> bool)
                                            and ^Awaiter: (member GetResult:  unit ->  ^TResult1)>
                    (task: ^TaskLike, continuation: (^TResult1 -> Async2Code<'TOverall, 'TResult2>)) : Async2Code<'TOverall, 'TResult2> =

            Async2Code<'TOverall, 'TResult2>(fun sm -> 
                if __useResumableCode then 
                    //-- RESUMABLE CODE START
                    // Get an awaiter from the awaitable
                    let mutable awaiter = (^TaskLike: (member GetAwaiter : unit -> ^Awaiter)(task)) 

                    let mutable __stack_fin = true
                    if not (^Awaiter : (member get_IsCompleted : unit -> bool)(awaiter)) then
                        // This will yield with __stack_yield_fin = false
                        // This will resume with __stack_yield_fin = true
                        let __stack_yield_fin = ResumableCode.Yield().Invoke(&sm)
                        __stack_fin <- __stack_yield_fin
                    
                    if __stack_fin then 
                        let result = (^Awaiter : (member GetResult : unit -> ^TResult1)(awaiter))
                        (continuation result).Invoke(&sm)
                    else
                        sm.Data.builder.AwaitUnsafeOnCompleted(&awaiter, &sm)
                        false
                else
                    failwith "dynamic" //TaskWitnesses.CanBindDynamic< ^TaskLike, ^TResult1, 'TResult2, ^Awaiter , 'TOverall>(&sm, priority, task, continuation)
                //-- RESUMABLE CODE END
            )

        member inline b.ReturnFrom< ^TaskLike, ^Awaiter, ^T
                                            when  ^TaskLike: (member GetAwaiter:  unit ->  ^Awaiter)
                                            and ^Awaiter :> ICriticalNotifyCompletion
                                            and ^Awaiter: (member get_IsCompleted: unit -> bool)
                                            and ^Awaiter: (member GetResult: unit ->  ^T)>
                (task: ^TaskLike) : Async2Code< ^T,  ^T> =
            b.Bind(task, (fun res -> b.Return(res)))

        member inline this.For(sequence : seq<'TElement>, body : 'TElement -> Async2Code<'TOverall, unit>) : Async2Code<'TOverall, unit> =
            // A for loop is just a using statement on the sequence's enumerator...
            this.Using (sequence.GetEnumerator(), 
                // ... and its body is a while loop that advances the enumerator and runs the body on each element.
                (fun e -> this.While((fun () -> e.MoveNext()), (fun sm -> (body e.Current).Invoke(&sm)))))

    let async2 = Async2Builder()

    let runSynchronously ct (t: Async2<'T>) =
        let e = t.StartImmediate(ct)
        e.Task.Result

    let cancellationTokenAsync = 
        async2.Run(Async2Code<CancellationToken, _>(fun sm -> 
            sm.Data.result <- sm.Data.cancellationToken
            true))

    let unitAsync = async2 { return () }

[<Sealed>]
type Async2 =

    static member CancellationToken = cancellationTokenAsync

    static member CancelCheck () = unitAsync

    static member DefaultCancellationToken = Async.DefaultCancellationToken

    static member CancelDefaultToken() = Async.CancelDefaultToken()

    static member Catch (computation: Async2<'T>) =
        async2 { try let! res = computation in return Choice1Of2 res with e -> return Choice2Of2 e }

    static member RunSynchronously (computation: Async2<'T>, ?timeout: int, ?cancellationToken:CancellationToken) =
        // TODO: timeout
        let cancellationToken = defaultArg cancellationToken Async.DefaultCancellationToken
        let e = computation.StartImmediate(cancellationToken)
        e.Task.Result

    static member StartImmediateAsTask (computation: Async2<'T>, ?cancellationToken ) : Task<'T> =
        let cancellationToken = defaultArg cancellationToken Async.DefaultCancellationToken
        let e = computation.StartImmediate(cancellationToken)
        e.Task

    static member StartImmediate(computation:Async2<unit>, ?cancellationToken) : unit =
        let cancellationToken = defaultArg cancellationToken Async.DefaultCancellationToken
        let e = computation.StartImmediate(cancellationToken)
        e |> ignore

    static member SwitchToNewThread() = 
       async2 { return! Task.CompletedTask.ConfigureAwait(false) }

    static member SwitchToThreadPool() : Async2<unit> = 
       async2 { return! Task.CompletedTask.ConfigureAwait(false) }

    static member Start (computation: Async2<unit>, ?cancellationToken) =
        let p = 
            async2 { 
                do! Async2.SwitchToThreadPool()
                return! computation
            }
        Async2.StartImmediate(p, ?cancellationToken=cancellationToken)

    static member StartAsTask (computation: Async2<'T>, ?taskCreationOptions: TaskCreationOptions, ?cancellationToken) =
        // TODO: taskCreationOptions
        let p = 
            async2 { 
                do! Async2.SwitchToThreadPool()
                return! computation
            }
        Async2.StartImmediateAsTask(p, ?cancellationToken=cancellationToken)

    static member StartChildAsTask (computation: Async2<'T>, ?taskCreationOptions) : Async2<Task<'T>> =
        async2 { 
            let! cancellationToken = cancellationTokenAsync
            return Async2.StartAsTask (computation, ?taskCreationOptions=taskCreationOptions, cancellationToken=cancellationToken)
        }


    static member Sleep (millisecondsDueTime: int64) : Async2<unit> =
        // TODO: int64 millisecondsDueTime?
        async2 { return! Task.Delay(int millisecondsDueTime)}

    static member Sleep (millisecondsDueTime: int32) : Async2<unit> =
        async2 { return! Task.Delay(millisecondsDueTime)}

    static member Sleep (dueTime: TimeSpan) =
        async2 { return! Task.Delay(dueTime)}

    static member Ignore (computation: Async2<'T>) = 
        async2 { let! _res = computation in return () }

    static member AwaitTask (task:Task<'T>) : Async2<'T> =
        async2 { return! task }

    static member AwaitTask (task:Task) : Async2<unit> =
        async2 { return! task }

    //static member FromContinuations (callback: ('T -> unit) * (exn -> unit) * (OperationCanceledException -> unit) -> unit) : Async2<'T> =
    //    MakeAsync (fun ctxt ->
    //        if ctxt.IsCancellationRequested then
    //            ctxt.OnCancellation ()
    //        else
    //            let mutable underCurrentThreadStack = true
    //            let mutable contToTailCall = None
    //            let thread = Thread.CurrentThread
    //            let latch = Latch()
    //            let once cont x =
    //                if not(latch.Enter()) then invalidOp(SR.GetString(SR.controlContinuationInvokedMultipleTimes))
    //                if Thread.CurrentThread.Equals thread && underCurrentThreadStack then
    //                    contToTailCall <- Some(fun () -> cont x)
    //                else if Trampoline.ThisThreadHasTrampoline then
    //                    let syncCtxt = SynchronizationContext.Current
    //                    ctxt.trampolineHolder.PostOrQueueWithTrampoline syncCtxt (fun () -> cont x) |> unfake
    //                else
    //                    ctxt.trampolineHolder.ExecuteWithTrampoline (fun () -> cont x ) |> unfake
    //            try
    //                callback (once ctxt.cont, (fun exn -> once ctxt.econt (ExceptionDispatchInfo.RestoreOrCapture exn)), once ctxt.ccont)
    //            with exn ->
    //                if not(latch.Enter()) then invalidOp(SR.GetString(SR.controlContinuationInvokedMultipleTimes))
    //                let edi = ExceptionDispatchInfo.RestoreOrCapture exn
    //                ctxt.econt edi |> unfake

    //            underCurrentThreadStack <- false

    //            match contToTailCall with
    //            | Some k -> k()
    //            | _ -> fake())

    //static member Parallel (computations: seq<Async2<'T>>) = Async.Parallel(computations, ?maxDegreeOfParallelism=None)

    //static member Parallel (computations: seq<Async2<'T>>, ?maxDegreeOfParallelism: int) =
    //    match maxDegreeOfParallelism with
    //    | Some x when x < 1 -> raise(System.ArgumentException(String.Format(SR.GetString(SR.maxDegreeOfParallelismNotPositive), x), "maxDegreeOfParallelism"))
    //    | _ -> ()

    //    MakeAsync (fun ctxt ->
    //        let tasks, result =
    //            try
    //                Seq.toArray computations, None   // manually protect eval of seq
    //            with exn ->
    //                let edi = ExceptionDispatchInfo.RestoreOrCapture exn
    //                null, Some (ctxt.econt edi)

    //        match result with
    //        | Some r -> r
    //        | None ->
    //        if tasks.Length = 0 then
    //            // must not be in a 'protect' if we call cont explicitly; if cont throws, it should unwind the stack, preserving Dev10 behavior
    //            ctxt.cont [| |]
    //        else
    //          ProtectedCode ctxt (fun ctxt ->
    //            let ctxtWithSync = DelimitSyncContext ctxt  // manually resync
    //            let mutable count = tasks.Length
    //            let mutable firstExn = None
    //            let results = Array.zeroCreate tasks.Length
    //            // Attempt to cancel the individual operations if an exception happens on any of the other threads
    //            let innerCTS = new LinkedSubSource(ctxtWithSync.token)

    //            let finishTask remaining =
    //                if (remaining = 0) then
    //                    innerCTS.Dispose()
    //                    match firstExn with
    //                    | None -> ctxtWithSync.trampolineHolder.ExecuteWithTrampoline (fun () -> ctxtWithSync.cont results)
    //                    | Some (Choice1Of2 exn) -> ctxtWithSync.trampolineHolder.ExecuteWithTrampoline (fun () -> ctxtWithSync.econt exn)
    //                    | Some (Choice2Of2 cexn) -> ctxtWithSync.trampolineHolder.ExecuteWithTrampoline (fun () -> ctxtWithSync.ccont cexn)
    //                else
    //                    fake()

    //            // recordSuccess and recordFailure between them decrement count to 0 and
    //            // as soon as 0 is reached dispose innerCancellationSource

    //            let recordSuccess i res =
    //                results.[i] <- res
    //                finishTask(Interlocked.Decrement &count)

    //            let recordFailure exn =
    //                // capture first exception and then decrement the counter to avoid race when
    //                // - thread 1 decremented counter and preempted by the scheduler
    //                // - thread 2 decremented counter and called finishTask
    //                // since exception is not yet captured - finishtask will fall into success branch
    //                match Interlocked.CompareExchange(&firstExn, Some exn, None) with
    //                | None ->
    //                    // signal cancellation before decrementing the counter - this guarantees that no other thread can sneak to finishTask and dispose innerCTS
    //                    // NOTE: Cancel may introduce reentrancy - i.e. when handler registered for the cancellation token invokes cancel continuation that will call 'recordFailure'
    //                    // to correctly handle this we need to return decremented value, not the current value of 'count' otherwise we may invoke finishTask with value '0' several times
    //                    innerCTS.Cancel()
    //                | _ -> ()
    //                finishTask(Interlocked.Decrement &count)

    //            // If maxDegreeOfParallelism is set but is higher then the number of tasks we have we set it back to None to fall into the simple
    //            // queue all items branch
    //            let maxDegreeOfParallelism =
    //                match maxDegreeOfParallelism with
    //                | None -> None
    //                | Some x when x >= tasks.Length -> None
    //                | Some _ as x -> x

    //            // Simple case (no maxDegreeOfParallelism) just queue all the work, if we have maxDegreeOfParallelism set we start that many workers
    //            // which will make progress on the actual computations
    //            match maxDegreeOfParallelism with
    //            | None ->
    //                tasks |> Array.iteri (fun i p ->
    //                    QueueAsync
    //                            innerCTS.Token
    //                            // on success, record the result
    //                            (fun res -> recordSuccess i res)
    //                            // on exception...
    //                            (fun edi -> recordFailure (Choice1Of2 edi))
    //                            // on cancellation...
    //                            (fun cexn -> recordFailure (Choice2Of2 cexn))
    //                            p
    //                        |> unfake)
    //            | Some maxDegreeOfParallelism ->
    //                let mutable i = -1
    //                let rec worker (trampolineHolder : TrampolineHolder) =
    //                    if i < tasks.Length then
    //                        let j = Interlocked.Increment &i
    //                        if j < tasks.Length then
    //                            if innerCTS.Token.IsCancellationRequested then
    //                                let cexn = OperationCanceledException (innerCTS.Token)
    //                                recordFailure (Choice2Of2 cexn) |> unfake
    //                                worker trampolineHolder |> unfake
    //                            else
    //                                let taskCtxt =
    //                                    AsyncActivation.Create
    //                                        innerCTS.Token
    //                                        trampolineHolder
    //                                        (fun res -> recordSuccess j res |> unfake; worker trampolineHolder)
    //                                        (fun edi -> recordFailure (Choice1Of2 edi) |> unfake; worker trampolineHolder)
    //                                        (fun cexn -> recordFailure (Choice2Of2 cexn) |> unfake; worker trampolineHolder)
    //                                tasks.[j].Invoke taskCtxt |> unfake
    //                    fake()
    //                for x = 1 to maxDegreeOfParallelism do
    //                    let trampolineHolder = TrampolineHolder()
    //                    trampolineHolder.QueueWorkItemWithTrampoline (fun () ->
    //                        worker trampolineHolder)
    //                    |> unfake

    //            fake()))

    //static member Sequential (computations: seq<Async2<'T>>) = Async.Parallel(computations, maxDegreeOfParallelism=1)

    //static member Choice(computations: Async2<'T option> seq) : Async2<'T option> =
    //    MakeAsync (fun ctxt ->
    //        let result =
    //            try Seq.toArray computations |> Choice1Of2
    //            with exn -> ExceptionDispatchInfo.RestoreOrCapture exn |> Choice2Of2

    //        match result with
    //        | Choice2Of2 edi -> ctxt.econt edi
    //        | Choice1Of2 [||] -> ctxt.cont None
    //        | Choice1Of2 computations ->
    //            ProtectedCode ctxt (fun ctxt ->
    //                let ctxtWithSync = DelimitSyncContext ctxt
    //                let mutable count = computations.Length
    //                let mutable noneCount = 0
    //                let mutable someOrExnCount = 0
    //                let innerCts = new LinkedSubSource(ctxtWithSync.token)

    //                let scont (result: 'T option) =
    //                    let result =
    //                        match result with
    //                        | Some _ ->
    //                            if Interlocked.Increment &someOrExnCount = 1 then
    //                                innerCts.Cancel(); ctxtWithSync.trampolineHolder.ExecuteWithTrampoline (fun () -> ctxtWithSync.cont result)
    //                            else
    //                                fake()

    //                        | None ->
    //                            if Interlocked.Increment &noneCount = computations.Length then
    //                                innerCts.Cancel(); ctxtWithSync.trampolineHolder.ExecuteWithTrampoline (fun () -> ctxtWithSync.cont None)
    //                            else
    //                                fake()

    //                    if Interlocked.Decrement &count = 0 then
    //                        innerCts.Dispose()

    //                    result

    //                let econt (exn: ExceptionDispatchInfo) =
    //                    let result =
    //                        if Interlocked.Increment &someOrExnCount = 1 then
    //                            innerCts.Cancel(); ctxtWithSync.trampolineHolder.ExecuteWithTrampoline (fun () -> ctxtWithSync.econt exn)
    //                        else
    //                            fake()

    //                    if Interlocked.Decrement &count = 0 then
    //                        innerCts.Dispose()

    //                    result

    //                let ccont (exn: OperationCanceledException) =
    //                    let result =
    //                        if Interlocked.Increment &someOrExnCount = 1 then
    //                            innerCts.Cancel(); ctxtWithSync.trampolineHolder.ExecuteWithTrampoline (fun () -> ctxtWithSync.ccont exn)
    //                        else
    //                            fake()

    //                    if Interlocked.Decrement &count = 0 then
    //                        innerCts.Dispose()

    //                    result

    //                for c in computations do
    //                    QueueAsync innerCts.Token scont econt ccont c |> unfake

    //                fake()))

    /// StartWithContinuations, except the exception continuation is given an ExceptionDispatchInfo
    //static member StartWithContinuationsUsingDispatchInfo(computation:Async2<'T>, continuation, exceptionContinuation, cancellationContinuation, ?cancellationToken) : unit =
    //    let cancellationToken = defaultArg cancellationToken Async.DefaultCancellationToken
    //    AsyncPrimitives.StartWithContinuations cancellationToken computation continuation exceptionContinuation cancellationContinuation

    //static member StartWithContinuations(computation:Async2<'T>, continuation, exceptionContinuation, cancellationContinuation, ?cancellationToken) : unit =
    //    Async.StartWithContinuationsUsingDispatchInfo(computation, continuation, (fun edi -> exceptionContinuation (edi.GetAssociatedSourceException())), cancellationContinuation, ?cancellationToken=cancellationToken)

    ///// Wait for a wait handle. Both timeout and cancellation are supported
    //static member AwaitWaitHandle(waitHandle: WaitHandle, ?millisecondsTimeout:int) =
    //    let millisecondsTimeout = defaultArg millisecondsTimeout Threading.Timeout.Infinite
    //    if millisecondsTimeout = 0 then
    //        async.Delay(fun () ->
    //            let ok = waitHandle.WaitOne(0, exitContext=false)
    //            async.Return ok)
    //    else
    //        CreateDelimitedUserCodeAsync(fun ctxt ->
    //            let aux = ctxt.aux
    //            let rwh = ref (None: RegisteredWaitHandle option)
    //            let latch = Latch()
    //            let rec cancelHandler =
    //                Action(fun () ->
    //                    if latch.Enter() then
    //                        // if we got here - then we need to unregister RegisteredWaitHandle + trigger cancellation
    //                        // entrance to TP callback is protected by latch - so savedCont will never be called
    //                        lock rwh (fun () ->
    //                            match !rwh with
    //                            | None -> ()
    //                            | Some rwh -> rwh.Unregister null |> ignore)
    //                        Async.Start (async2 { do (ctxt.ccont (OperationCanceledException(aux.token)) |> unfake) }))

    //            and registration: CancellationTokenRegistration = aux.token.Register(cancelHandler)

    //            let savedCont = ctxt.cont
    //            try
    //                lock rwh (fun () ->
    //                    rwh := Some(ThreadPool.RegisterWaitForSingleObject
    //                                  (waitObject=waitHandle,
    //                                   callBack=WaitOrTimerCallback(fun _ timeOut ->
    //                                                if latch.Enter() then
    //                                                    lock rwh (fun () -> rwh.Value.Value.Unregister null |> ignore)
    //                                                    rwh := None
    //                                                    registration.Dispose()
    //                                                    ctxt.trampolineHolder.ExecuteWithTrampoline (fun () -> savedCont (not timeOut)) |> unfake),
    //                                   state=null,
    //                                   millisecondsTimeOutInterval=millisecondsTimeout,
    //                                   executeOnlyOnce=true))
    //                    fake())
    //            with _ ->
    //                if latch.Enter() then
    //                    registration.Dispose()
    //                    reraise() // reraise exception only if we successfully enter the latch (no other continuations were called)
    //                else
    //                    fake()
    //            )

    //static member AwaitIAsyncResult(iar: IAsyncResult, ?millisecondsTimeout): Async2<bool> =
    //    async2 { if iar.CompletedSynchronously then
    //                return true
    //            else
    //                return! Async.AwaitWaitHandle(iar.AsyncWaitHandle, ?millisecondsTimeout=millisecondsTimeout)  }


    ///// Bind the result of a result cell, calling the appropriate continuation.
    //static member BindResult (result: AsyncResult<'T>) : Async2<'T> =
    //    MakeAsync (fun ctxt ->
    //           (match result with
    //            | Ok v -> ctxt.cont v
    //            | Error exn -> ctxt.econt exn
    //            | Canceled exn -> ctxt.ccont exn) )

    ///// Await and use the result of a result cell. The resulting async doesn't support cancellation
    ///// or timeout directly, rather the underlying computation must fill the result if cancellation
    ///// or timeout occurs.
    //static member AwaitAndBindResult_NoDirectCancelOrTimeout(resultCell: ResultCell<AsyncResult<'T>>) : Async2<'T> =
    //    async2 {
    //        let! result = resultCell.AwaitResult_NoDirectCancelOrTimeout
    //        return! Async.BindResult result
    //    }

    ///// Await the result of a result cell belonging to a child computation.  The resulting async supports timeout and if
    ///// it happens the child computation will be cancelled.   The resulting async doesn't support cancellation
    ///// directly, rather the underlying computation must fill the result if cancellation occurs.
    //static member AwaitAndBindChildResult(innerCTS: CancellationTokenSource, resultCell: ResultCell<AsyncResult<'T>>, millisecondsTimeout) : Async2<'T> =
    //    match millisecondsTimeout with
    //    | None | Some -1 ->
    //        resultCell |> Async.AwaitAndBindResult_NoDirectCancelOrTimeout

    //    | Some 0 ->
    //        async2 { if resultCell.ResultAvailable then
    //                    let res = resultCell.GrabResult()
    //                    return res.Commit()
    //                else
    //                    return raise (System.TimeoutException()) }
    //    | _ ->
    //        async2 { try
    //                   if resultCell.ResultAvailable then
    //                     let res = resultCell.GrabResult()
    //                     return res.Commit()
    //                   else
    //                     let! ok = Async.AwaitWaitHandle (resultCell.GetWaitHandle(), ?millisecondsTimeout=millisecondsTimeout)
    //                     if ok then
    //                        let res = resultCell.GrabResult()
    //                        return res.Commit()
    //                     else // timed out
    //                        // issue cancellation signal
    //                        innerCTS.Cancel()
    //                        // wait for computation to quiesce
    //                        let! _ = Async.AwaitWaitHandle (resultCell.GetWaitHandle())
    //                        return raise (System.TimeoutException())
    //                 finally
    //                   resultCell.Close() }


    //static member FromBeginEnd(beginAction, endAction, ?cancelAction): Async2<'T> =
    //    async2 { let! cancellationToken = cancellationTokenAsync
    //            let resultCell = new ResultCell<_>()

    //            let once = Once()

    //            let registration: CancellationTokenRegistration =

    //                let onCancel () =
    //                    // Call the cancellation routine
    //                    match cancelAction with
    //                    | None ->
    //                        // Register the result. This may race with a successful result, but
    //                        // ResultCell allows a race and throws away whichever comes last.
    //                        once.Do(fun () ->
    //                                    let canceledResult = Canceled (OperationCanceledException cancellationToken)
    //                                    resultCell.RegisterResult(canceledResult, reuseThread=true) |> unfake
    //                        )
    //                    | Some cancel ->
    //                        // If we get an exception from a cooperative cancellation function
    //                        // we assume the operation has already completed.
    //                        try cancel() with _ -> ()

    //                cancellationToken.Register(Action(onCancel))

    //            let callback =
    //                System.AsyncCallback(fun iar ->
    //                        if not iar.CompletedSynchronously then
    //                            // The callback has been activated, so ensure cancellation is not possible
    //                            // beyond this point.
    //                            match cancelAction with
    //                            | Some _ ->
    //                                    registration.Dispose()
    //                            | None ->
    //                                    once.Do(fun () -> registration.Dispose())

    //                            // Run the endAction and collect its result.
    //                            let res =
    //                                try
    //                                    Ok(endAction iar)
    //                                with exn ->
    //                                    let edi = ExceptionDispatchInfo.RestoreOrCapture exn
    //                                    Error edi

    //                            // Register the result. This may race with a cancellation result, but
    //                            // ResultCell allows a race and throws away whichever comes last.
    //                            resultCell.RegisterResult(res, reuseThread=true) |> unfake)

    //            let (iar:IAsyncResult) = beginAction (callback, (null:obj))
    //            if iar.CompletedSynchronously then
    //                registration.Dispose()
    //                return endAction iar
    //            else
    //                // Note: ok to use "NoDirectCancel" here because cancellation has been registered above
    //                // Note: ok to use "NoDirectTimeout" here because no timeout parameter to this method
    //                return! Async.AwaitAndBindResult_NoDirectCancelOrTimeout resultCell }


    //static member FromBeginEnd(arg, beginAction, endAction, ?cancelAction): Async2<'T> =
    //    Async.FromBeginEnd((fun (iar, state) -> beginAction(arg, iar, state)), endAction, ?cancelAction=cancelAction)

    //static member FromBeginEnd(arg1, arg2, beginAction, endAction, ?cancelAction): Async2<'T> =
    //    Async.FromBeginEnd((fun (iar, state) -> beginAction(arg1, arg2, iar, state)), endAction, ?cancelAction=cancelAction)

    //static member FromBeginEnd(arg1, arg2, arg3, beginAction, endAction, ?cancelAction): Async2<'T> =
    //    Async.FromBeginEnd((fun (iar, state) -> beginAction(arg1, arg2, arg3, iar, state)), endAction, ?cancelAction=cancelAction)

    //static member AsBeginEnd<'Arg, 'T> (computation:('Arg -> Async2<'T>)) :
    //        // The 'Begin' member
    //        ('Arg * System.AsyncCallback * obj -> System.IAsyncResult) *
    //        // The 'End' member
    //        (System.IAsyncResult -> 'T) *
    //        // The 'Cancel' member
    //        (System.IAsyncResult -> unit) =
    //            let beginAction = fun (a1, callback, state) -> AsBeginEndHelpers.beginAction ((computation a1), callback, state)
    //            beginAction, AsBeginEndHelpers.endAction<'T>, AsBeginEndHelpers.cancelAction<'T>

    //static member AwaitEvent(event:IEvent<'Delegate, 'T>, ?cancelAction) : Async2<'T> =
    //    async2 { let! cancellationToken = cancellationTokenAsync
    //            let resultCell = new ResultCell<_>()
    //            // Set up the handlers to listen to events and cancellation
    //            let once = Once()
    //            let rec registration: CancellationTokenRegistration=
    //                let onCancel () =
    //                    // We've been cancelled. Call the given cancellation routine
    //                    match cancelAction with
    //                    | None ->
    //                        // We've been cancelled without a cancel action. Stop listening to events
    //                        event.RemoveHandler del
    //                        // Register the result. This may race with a successful result, but
    //                        // ResultCell allows a race and throws away whichever comes last.
    //                        once.Do(fun () -> resultCell.RegisterResult(Canceled (OperationCanceledException cancellationToken), reuseThread=true) |> unfake)
    //                    | Some cancel ->
    //                        // If we get an exception from a cooperative cancellation function
    //                        // we assume the operation has already completed.
    //                        try cancel() with _ -> ()
    //                cancellationToken.Register(Action(onCancel))

    //            and del =
    //                FuncDelegate<'T>.Create<'Delegate>(fun eventArgs ->
    //                    // Stop listening to events
    //                    event.RemoveHandler del
    //                    // The callback has been activated, so ensure cancellation is not possible beyond this point
    //                    once.Do(fun () -> registration.Dispose())
    //                    let res = Ok eventArgs
    //                    // Register the result. This may race with a cancellation result, but
    //                    // ResultCell allows a race and throws away whichever comes last.
    //                    resultCell.RegisterResult(res, reuseThread=true) |> unfake)

    //            // Start listening to events
    //            event.AddHandler del

    //            // Return the async computation that allows us to await the result
    //            // Note: ok to use "NoDirectCancel" here because cancellation has been registered above
    //            // Note: ok to use "NoDirectTimeout" here because no timeout parameter to this method
    //            return! Async.AwaitAndBindResult_NoDirectCancelOrTimeout resultCell }

    //static member StartChild (computation:Async2<'T>, ?millisecondsTimeout) =
    //    async2 {
    //        let resultCell = new ResultCell<_>()
    //        let! cancellationToken = cancellationTokenAsync
    //        let innerCTS = new CancellationTokenSource() // innerCTS does not require disposal
    //        let mutable ctsRef = innerCTS
    //        let reg = cancellationToken.Register(
    //                                (fun () ->
    //                                    match ctsRef with
    //                                    | null -> ()
    //                                    | otherwise -> otherwise.Cancel()))
    //        do QueueAsync
    //               innerCTS.Token
    //               // since innerCTS is not ever Disposed, can call reg.Dispose() without a safety Latch
    //               (fun res -> ctsRef <- null; reg.Dispose(); resultCell.RegisterResult (Ok res, reuseThread=true))
    //               (fun edi -> ctsRef <- null; reg.Dispose(); resultCell.RegisterResult (Error edi, reuseThread=true))
    //               (fun err -> ctsRef <- null; reg.Dispose(); resultCell.RegisterResult (Canceled err, reuseThread=true))
    //               computation
    //             |> unfake

    //        return Async.AwaitAndBindChildResult(innerCTS, resultCell, millisecondsTimeout) }

    //static member SwitchToContext syncContext =
    //    let t  =
    //        Task.Factory.StartNew(
    //            (fun () -> ()), // this will use current synchronization context
    //            CancellationToken.None, 
    //            TaskCreationOptions.None, 
    //            TaskScheduler. .FromCurrentSynchronizationContext())
    //    async2 { match syncContext with
    //            | null ->
    //                // no synchronization context, just switch to the thread pool
    //                do! Async.SwitchToThreadPool()
    //            | syncCtxt ->
    //                // post the continuation to the synchronization context
    //                return! CreateSwitchToAsync syncCtxt }

    //static member OnCancel interruption =
    //    async2 { let! cancellationToken = cancellationTokenAsync
    //            // latch protects CancellationTokenRegistration.Dispose from being called twice
    //            let latch = Latch()
    //            let rec handler () =
    //                try
    //                    if latch.Enter() then registration.Dispose()
    //                    interruption ()
    //                with _ -> ()
    //            and registration: CancellationTokenRegistration = cancellationToken.Register(Action(handler))
    //            return { new System.IDisposable with
    //                        member this.Dispose() =
    //                            // dispose CancellationTokenRegistration only if cancellation was not requested.
    //                            // otherwise - do nothing, disposal will be performed by the handler itself
    //                            if not cancellationToken.IsCancellationRequested then
    //                                if latch.Enter() then registration.Dispose() } }

    //static member TryCancelled (computation: Async2<'T>, compensation) =
    //    CreateWhenCancelledAsync compensation computation

//module CommonExtensions =

//    type System.IO.Stream with

//        [<CompiledName("AsyncRead")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
//        member stream.AsyncRead(buffer: byte[], ?offset, ?count) =
//            let offset = defaultArg offset 0
//            let count  = defaultArg count buffer.Length
//            Async.FromBeginEnd (buffer, offset, count, stream.BeginRead, stream.EndRead)

//        [<CompiledName("AsyncReadBytes")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
//        member stream.AsyncRead count =
//            async2 { 
//                let buffer = Array.zeroCreate count
//                let mutable i = 0
//                while i < count do
//                    let! n = stream.AsyncRead(buffer, i, count - i)
//                    i <- i + n
//                    if n = 0 then
//                        raise(System.IO.EndOfStreamException())
//                return buffer }

//        [<CompiledName("AsyncWrite")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
//        member stream.AsyncWrite(buffer:byte[], ?offset:int, ?count:int) =
//            let offset = defaultArg offset 0
//            let count  = defaultArg count buffer.Length
//            Async.FromBeginEnd (buffer, offset, count, stream.BeginWrite, stream.EndWrite)


//module WebExtensions =

//    type System.Net.WebRequest with
//        [<CompiledName("AsyncGetResponse")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
//        member req.AsyncGetResponse() : Async2<System.Net.WebResponse> =

//            let mutable canceled = false // WebException with Status = WebExceptionStatus.RequestCanceled  can be raised in other situations except cancellation, use flag to filter out false positives

//            // Use CreateTryWithFilterAsync to allow propagation of exception without losing stack
//            Async.FromBeginEnd(beginAction=req.BeginGetResponse,
//                               endAction = req.EndGetResponse,
//                               cancelAction = fun() -> canceled <- true; req.Abort())
//            |> CreateTryWithFilterAsync (fun exn ->
//                match exn with
//                | :? System.Net.WebException as webExn
//                        when webExn.Status = System.Net.WebExceptionStatus.RequestCanceled && canceled ->

//                    Some (Async.BindResult(AsyncResult.Canceled (OperationCanceledException webExn.Message)))
//                | _ ->
//                    None)

//    type System.Net.WebClient with
//        member inline private this.Download(event: IEvent<'T, _>, handler: _ -> 'T, start, result) =
//            let downloadAsync =
//                Async.FromContinuations (fun (cont, econt, ccont) ->
//                    let userToken = obj()
//                    let rec delegate' (_: obj) (args: #ComponentModel.AsyncCompletedEventArgs) =
//                        // ensure we handle the completed event from correct download call
//                        if userToken = args.UserState then
//                            event.RemoveHandler handle
//                            if args.Cancelled then
//                                ccont (OperationCanceledException())
//                            elif isNotNull args.Error then
//                                econt args.Error
//                            else
//                                cont (result args)
//                    and handle = handler delegate'
//                    event.AddHandler handle
//                    start userToken
//                )

//            async2 {
//                use! _holder = Async.OnCancel(fun _ -> this.CancelAsync())
//                return! downloadAsync
//            }

//        [<CompiledName("AsyncDownloadString")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
//        member this.AsyncDownloadString (address:Uri) : Async2<string> =
//            this.Download(
//                event   = this.DownloadStringCompleted,
//                handler = (fun action    -> Net.DownloadStringCompletedEventHandler action),
//                start   = (fun userToken -> this.DownloadStringAsync(address, userToken)),
//                result  = (fun args      -> args.Result)
//            )

//        [<CompiledName("AsyncDownloadData")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
//        member this.AsyncDownloadData (address:Uri) : Async2<byte[]> =
//            this.Download(
//                event   = this.DownloadDataCompleted,
//                handler = (fun action    -> Net.DownloadDataCompletedEventHandler action),
//                start   = (fun userToken -> this.DownloadDataAsync(address, userToken)),
//                result  = (fun args      -> args.Result)
//            )

//        [<CompiledName("AsyncDownloadFile")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
//        member this.AsyncDownloadFile (address:Uri, fileName:string) : Async2<unit> =
//            this.Download(
//                event   = this.DownloadFileCompleted,
//                handler = (fun action    -> ComponentModel.AsyncCompletedEventHandler action),
//                start   = (fun userToken -> this.DownloadFileAsync(address, fileName, userToken)),
//                result  = (fun _         -> ())
//            )

module Examples =

    let t1 () = 
        async2 {
           printfn "in t1"
           do! Async2.Sleep 100
           printfn "resuming t1"

           return "a"
        }

    let testTailcallTiny () = 
        async2 {
           return! t1()
        }
    let rec testTailcall (n: int) = 
        async2 {
           if n % 100 = 0 then 
               printfn $"in t1, n = {n}"
           if n > 0 then
               return! testTailcall(n-1)
           //yield ()
        }

    //let t2 () = 
    //    async2 {
    //       printfn "in t2"
    //       yield "d"
    //       printfn "in t2 b"
    //       for x in t1 () do 
    //           printfn "t2 - got %A" x
    //           yield "e"
    //           let! v = 
    //               task {
    //                   printfn "hey yo"
    //                   do! Task.Delay(200) 
    //               }
    //           yield "[T1]" + x
    //       let! v =
    //           task {
    //               printfn "hey yo"
    //               do! Task.Delay(10)
    //           }
    //       yield "f"
    //    }

    let perf1 (x: int) = 
        async2 {
           return 1
        }

    //let perf1_AsyncSeq (x: int) = 
    //    FSharp.Control.AsyncSeqExtensions.asyncSeq {
    //       yield 1
    //       yield 2
    //       if x >= 2 then 
    //           yield 3
    //           yield 4
    //    }

    //let perf2_AsyncSeq () = 
    //    FSharp.Control.AsyncSeqExtensions.asyncSeq {
    //       for i1 in perf1_AsyncSeq 3 do
    //         for i2 in perf1_AsyncSeq 3 do
    //           for i3 in perf1_AsyncSeq 3 do
    //             for i4 in perf1_AsyncSeq 3 do
    //               for i5 in perf1_AsyncSeq 3 do
    //                 yield! perf1_AsyncSeq i5
    //    }

    let dumpAsync2 (t: Async2<_>) = 
        printfn "-----"
        let e = t.StartImmediate(CancellationToken())
        let res = e.Task.Result
        printfn "result: %A" res

    //dumpAsync2 (t1())
    dumpAsync2 (testTailcallTiny())
    ////dumpAsync2 (t2())

    //printfn "t1() = %A" (Async2.toArray (t1()))
    //printfn "testTailcallTiny() = %A" (Async2.toArray (testTailcallTiny()))
    //dumpAsync2 (testTailcall(100000))
    //printfn "t2() = %A" (Async2.toArray (t2()))

    //printfn "perf2() = %A" (Async2.toArray (perf2()) |> Array.sum)

