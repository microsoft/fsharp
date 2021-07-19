
module Tests.TaskSeq

open System.Runtime.CompilerServices
open System.Threading.Tasks.Sources

#nowarn "42"
open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open FSharp.Core.CompilerServices
open FSharp.Core.CompilerServices.StateMachineHelpers

let verbose = false

let inline MoveNext(x: byref<'T> when 'T :> IAsyncStateMachine) = x.MoveNext()

type taskSeq<'T> = IAsyncEnumerable<'T>

type IPriority1 = interface end
type IPriority2 = interface end

[<NoComparison; NoEquality>]
type TaskSeqStateMachineData<'T>() =
    [<DefaultValue(false)>]
    val mutable cancellationToken : CancellationToken
    [<DefaultValue(false)>]
    val mutable disposalStack : ResizeArray<(unit -> Task)>
    [<DefaultValue(false)>]
    val mutable awaiter : ICriticalNotifyCompletion
    [<DefaultValue(false)>]
    val mutable promiseOfValueOrEnd: ManualResetValueTaskSourceCore<bool>
    [<DefaultValue(false)>]
    val mutable builder : AsyncIteratorMethodBuilder
    [<DefaultValue(false)>]
    val mutable taken : bool
    [<DefaultValue(false)>]
    val mutable current : ValueOption<'T>
    [<DefaultValue(false)>]
    val mutable boxed: TaskSeq<'T>
    // For tailcalls using 'return!'
    [<DefaultValue(false)>]
    val mutable tailcallTarget: TaskSeq<'T> option

    member data.PushDispose (f: unit -> Task) =
        match data.disposalStack with 
        | null -> data.disposalStack <- ResizeArray()
        | _ -> ()
        data.disposalStack.Add(f)

    member data.PopDispose () =
        match data.disposalStack with 
        | null -> ()
        | _ -> 
            data.disposalStack.RemoveAt(data.disposalStack.Count - 1)

and [<AbstractClass; NoEquality; NoComparison>] 
    TaskSeq<'T>() =
    abstract TailcallTarget: TaskSeq<'T> option
    abstract MoveNextAsyncResult: unit -> ValueTask<bool>

    // F# requires that we implement interfaces even on an abstract class
    interface IAsyncEnumerator<'T>  with
        member _.Current = failwith "abstract"
        member _.MoveNextAsync() = failwith "abstract"
    interface IAsyncDisposable with
        member _.DisposeAsync() = failwith "abstract"
    interface IAsyncEnumerable<'T> with
        member _.GetAsyncEnumerator(ct) = failwith "abstract"
    interface IAsyncStateMachine with 
        member _.MoveNext() = failwith "abstract"
        member _.SetStateMachine(_state) = failwith "abstract"
    interface IValueTaskSource with
        member _.GetResult(_token: int16) = failwith "abstract"
        member _.GetStatus(_token: int16) = failwith "abstract"
        member _.OnCompleted(_continuation, _state, _token, _flags) = failwith "abstract"
    interface IValueTaskSource<bool> with
        member _.GetStatus(_token: int16) = failwith "abstract"
        member _.GetResult(_token: int16) = failwith "abstract"
        member _.OnCompleted(_continuation, _state, _token, _flags) = failwith "abstract"

and [<NoComparison; NoEquality>]
    TaskSeq<'Machine, 'T  when 'Machine :> IAsyncStateMachine and 'Machine :> IResumableStateMachine<TaskSeqStateMachineData<'T>>>() =
    inherit TaskSeq<'T>()
    let initialThreadId = Environment.CurrentManagedThreadId
    
    [<DefaultValue(false)>]
    val mutable Machine : 'Machine

    member internal ts.hijack() =
        let res = ts.Machine.Data.tailcallTarget
        match res with 
        | Some tg -> 
            match tg.TailcallTarget with 
            | None -> 
                res
            | (Some tg2 as res2) -> 
                // Cut out chains of tailcalls
                ts.Machine.Data.tailcallTarget <- Some tg2
                res2
        | None -> 
            res

    // Note: Not entirely clear if this is needed, everything still compiles without it
    interface IValueTaskSource with
        member ts.GetResult(token: int16) = 
            match ts.hijack() with 
            | Some tg -> (tg :> IValueTaskSource).GetResult(token)
            | None -> ts.Machine.Data.promiseOfValueOrEnd.GetResult(token) |> ignore
        member ts.GetStatus(token: int16) =
            match ts.hijack() with 
            | Some tg -> (tg :> IValueTaskSource<bool>).GetStatus(token)
            | None -> ts.Machine.Data.promiseOfValueOrEnd.GetStatus(token)
        member ts.OnCompleted(continuation, state, token, flags) =
            match ts.hijack() with 
            | Some tg -> (tg :> IValueTaskSource).OnCompleted(continuation, state, token, flags)
            | None -> ts.Machine.Data.promiseOfValueOrEnd.OnCompleted(continuation, state, token, flags)

    // Needed for MoveNextAsync to return a ValueTask
    interface IValueTaskSource<bool> with
        member ts.GetStatus(token: int16) =
            match ts.hijack() with 
            | Some tg -> (tg :> IValueTaskSource<bool>).GetStatus(token)
            | None -> ts.Machine.Data.promiseOfValueOrEnd.GetStatus(token)
        member ts.GetResult(token: int16) = 
            match ts.hijack() with 
            | Some tg -> (tg :> IValueTaskSource<bool>).GetResult(token)
            | None -> ts.Machine.Data.promiseOfValueOrEnd.GetResult(token)
        member ts.OnCompleted(continuation, state, token, flags) =
            match ts.hijack() with 
            | Some tg -> (tg :> IValueTaskSource<bool>).OnCompleted(continuation, state, token, flags)
            | None -> ts.Machine.Data.promiseOfValueOrEnd.OnCompleted(continuation, state, token, flags)

    interface IAsyncStateMachine with 
        member ts.MoveNext() = 
            match ts.hijack() with 
            | Some tg -> (tg :> IAsyncStateMachine).MoveNext()
            | None -> MoveNext(&ts.Machine)
        
        member _.SetStateMachine(_state) = () // not needed for reference type

    interface IAsyncEnumerable<'T> with
        member ts.GetAsyncEnumerator(ct) = 
            let data = ts.Machine.Data
            if (not data.taken && initialThreadId = Environment.CurrentManagedThreadId) then
                data.taken <- true
                data.cancellationToken <- ct
                data.builder <- AsyncIteratorMethodBuilder.Create()
                (ts :> IAsyncEnumerator<_>)
            else
                if verbose then printfn "GetAsyncEnumerator, cloning..."
                let clone = ts.MemberwiseClone() :?> TaskSeq<'Machine, 'T>
                data.taken <- true
                clone.Machine.Data.cancellationToken <- ct
                (clone :> System.Collections.Generic.IAsyncEnumerator<'T>)

    interface IAsyncDisposable  with
        member ts.DisposeAsync() =
            match ts.hijack() with 
            | Some tg -> (tg :> IAsyncDisposable).DisposeAsync()
            | None -> 
            if verbose then printfn "DisposeAsync..." 
            task {
                match ts.Machine.Data.disposalStack with 
                | null -> ()
                | _ -> 
                let mutable exn = None
                for d in Seq.rev ts.Machine.Data.disposalStack do 
                    try 
                      do! d()
                    with e ->
                      if exn.IsNone then 
                          exn <- Some e
                match exn with 
                | None -> () 
                | Some e -> raise e 
            }
            |> ValueTask
       
    interface System.Collections.Generic.IAsyncEnumerator<'T> with
        member ts.Current =
            match ts.hijack() with 
            | Some tg -> (tg :> IAsyncEnumerator<'T>).Current
            | None -> 
            match ts.Machine.Data.current with
            | ValueSome x -> x
            | ValueNone -> failwith "no current value"
        
        member ts.MoveNextAsync() = 
            match ts.hijack() with 
            | Some tg -> (tg :> IAsyncEnumerator<'T>).MoveNextAsync()
            | None -> 
                if verbose then printfn "MoveNextAsync..."
                if ts.Machine.ResumptionPoint = -1 then // can't use as IAsyncEnumerator before IAsyncEnumerable
                    ValueTask<bool>()
                else
                    let data = ts.Machine.Data
                    data.promiseOfValueOrEnd.Reset()
                    let mutable ts = ts
                    data.builder.MoveNext(&ts)
                    
                    // If the move did a hijack then get the result from the final one
                    match ts.hijack() with 
                    | Some tg -> tg.MoveNextAsyncResult()
                    | None -> ts.MoveNextAsyncResult()

    override ts.MoveNextAsyncResult() = 
        let data = ts.Machine.Data
        let version = data.promiseOfValueOrEnd.Version
        let status = data.promiseOfValueOrEnd.GetStatus(version)
        if status = ValueTaskSourceStatus.Succeeded then
            let result = data.promiseOfValueOrEnd.GetResult(version)
            ValueTask<bool>(result)
        else
            if verbose then printfn "MoveNextAsync pending/faulted/cancelled..."
            ValueTask<bool>(ts, version) // uses IValueTaskSource<'T>

    override cr.TailcallTarget = 
        cr.hijack()

and TaskSeqCode<'T> = ResumableCode<TaskSeqStateMachineData<'T>, unit>
and TaskSeqStateMachine<'T> = ResumableStateMachine<TaskSeqStateMachineData<'T>>
and TaskSeqResumptionFunc<'T> = ResumptionFunc<TaskSeqStateMachineData<'T>>
and TaskSeqResumptionDynamicInfo<'T> = ResumptionDynamicInfo<TaskSeqStateMachineData<'T>>

type TaskSeqBuilder() =

    member inline _.Delay(f : unit -> TaskSeqCode<'T>) : TaskSeqCode<'T> =
        TaskSeqCode<'T>(fun sm -> f().Invoke(&sm))

    member inline _.Run(code : TaskSeqCode<'T>) : IAsyncEnumerable<'T> = 
        if __useResumableCode then
            // This is the static implementation.  A new struct type is created.
            __stateMachine<TaskSeqStateMachineData<'T>, IAsyncEnumerable<'T>>
                // IAsyncStateMachine.MoveNext
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
                                sm.Data.promiseOfValueOrEnd.SetResult(false)
                                sm.Data.builder.Complete()
                            elif sm.Data.current.IsSome then
                                //printfn $"at Run.MoveNext, yield"
                                sm.Data.promiseOfValueOrEnd.SetResult(true)
                            else
                               // Goto request
                                match sm.Data.tailcallTarget with 
                                | Some tg -> 
                                    //printfn $"at Run.MoveNext, hijack"
                                    let mutable tg = tg
                                    MoveNext(&tg) 
                                | None -> 
                                    //printfn $"at Run.MoveNext, await"
                                    let boxed = sm.Data.boxed
                                    sm.Data.awaiter.UnsafeOnCompleted(Action(fun () -> 
                                        let mutable boxed = boxed
                                        MoveNext(&boxed)))

                        with exn ->
                            //Console.WriteLine("[{0}] SetException {1}", sm.MethodBuilder.Task.Id, exn)
                            sm.Data.promiseOfValueOrEnd.SetException(exn)
                            sm.Data.builder.Complete()
                        //-- RESUMABLE CODE END
                    ))
                (SetStateMachineMethodImpl<_>(fun sm state -> ()))
                (AfterCode<_,_>(fun sm -> 
                    let ts = TaskSeq<TaskSeqStateMachine<'T>, 'T>()
                    ts.Machine <- sm
                    ts.Machine.Data <- TaskSeqStateMachineData()
                    ts.Machine.Data.boxed <- ts
                    ts :> IAsyncEnumerable<'T>))
        else
            failwith "no dynamic implementation as yet"
        //    let initialResumptionFunc = TaskSeqResumptionFunc<'T>(fun sm -> code.Invoke(&sm))
        //    let resumptionFuncExecutor = TaskSeqResumptionExecutor<'T>(fun sm f -> 
        //            // TODO: add exception handling?
        //            if f.Invoke(&sm) then 
        //                sm.ResumptionPoint <- -2)
        //    let setStateMachine = SetStateMachineMethodImpl<_>(fun sm f -> ())
        //    sm.Machine.ResumptionFuncInfo <- (initialResumptionFunc, resumptionFuncExecutor, setStateMachine)
        //sm.Start()


    member inline _.Zero() : TaskSeqCode<'T> =
        ResumableCode.Zero()

    member inline _.Combine(task1: TaskSeqCode<'T>, task2: TaskSeqCode<'T>) : TaskSeqCode<'T> =
        ResumableCode.Combine(task1, task2)
            
    member inline _.WhileAsync([<InlineIfLambda>] condition : unit -> ValueTask<bool>, body : TaskSeqCode<'T>) : TaskSeqCode<'T> =
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
                        //if verbose then printfn "calling AwaitUnsafeOnCompleted"
                        sm.Data.awaiter <- awaiter
                        sm.Data.current <- ValueNone

                if __stack_condition_fin then 
                    if condition_res then 
                        body.Invoke(&sm)
                    else
                        true
                else
                    false
                ))

    member inline b.While([<InlineIfLambda>] condition : unit -> bool, body : TaskSeqCode<'T>) : TaskSeqCode<'T> =
        b.WhileAsync((fun () -> ValueTask<bool>(condition())), body)

    member inline _.TryWith(body : TaskSeqCode<'T>, catch : exn -> TaskSeqCode<'T>) : TaskSeqCode<'T> =
        ResumableCode.TryWith(body, catch)

    member inline _.TryFinallyAsync(body: TaskSeqCode<'T>, compensation : unit -> Task) : TaskSeqCode<'T> =
        ResumableCode.TryFinallyAsync(
            TaskSeqCode<'T>(fun sm -> 
                sm.Data.PushDispose (fun () -> compensation())
                body.Invoke(&sm)), 
            ResumableCode<_,_>(fun sm -> 
                sm.Data.PopDispose();
                let mutable __stack_condition_fin = true
                let __stack_vtask = compensation()
                if not __stack_vtask.IsCompleted then
                    let mutable awaiter = __stack_vtask.GetAwaiter()
                    let __stack_yield_fin = ResumableCode.Yield().Invoke(&sm)
                    __stack_condition_fin <- __stack_yield_fin

                    if not __stack_condition_fin then 
                        sm.Data.awaiter <- awaiter

                __stack_condition_fin))

    member inline _.TryFinally(body: TaskSeqCode<'T>, compensation : unit -> unit) : TaskSeqCode<'T> =
        ResumableCode.TryFinally(
            TaskSeqCode<'T>(fun sm -> 
                sm.Data.PushDispose (fun () -> compensation(); Task.CompletedTask)
                body.Invoke(&sm)), 
            ResumableCode<_,_>(fun sm -> sm.Data.PopDispose(); compensation(); true))

    member inline this.Using(disp : #IDisposable, body : #IDisposable -> TaskSeqCode<'T>, ?priority: IPriority2) : TaskSeqCode<'T> = 
        ignore priority
        // A using statement is just a try/finally with the finally block disposing if non-null.
        this.TryFinally(
            (fun sm -> (body disp).Invoke(&sm)),
            (fun () -> if not (isNull (box disp)) then disp.Dispose()))

    member inline this.Using(disp : #IAsyncDisposable, body : #IAsyncDisposable -> TaskSeqCode<'T>, ?priority: IPriority1) : TaskSeqCode<'T> = 
        ignore priority
        // A using statement is just a try/finally with the finally block disposing if non-null.
        this.TryFinallyAsync(
            (fun sm -> (body disp).Invoke(&sm)),
            (fun () -> 
                if not (isNull (box disp)) then 
                    disp.DisposeAsync().AsTask()
                else 
                    Task.CompletedTask))

    member inline this.For(sequence : seq<'TElement>, body : 'TElement -> TaskSeqCode<'T>) : TaskSeqCode<'T> =
        // A for loop is just a using statement on the sequence's enumerator...
        this.Using (sequence.GetEnumerator(), 
            // ... and its body is a while loop that advances the enumerator and runs the body on each element.
            (fun e -> this.While((fun () -> e.MoveNext()), (fun sm -> (body e.Current).Invoke(&sm)))))

    member inline this.For(source: #IAsyncEnumerable<'TElement>, body : 'TElement -> TaskSeqCode<'T>) : TaskSeqCode<'T> =
        TaskSeqCode<'T>(fun sm -> 
            this.Using(source.GetAsyncEnumerator(sm.Data.cancellationToken), 
                (fun e -> this.WhileAsync((fun () -> e.MoveNextAsync()), 
                                          (fun sm -> (body e.Current).Invoke(&sm))))).Invoke(&sm))

    member inline _.Yield (v: 'T) : TaskSeqCode<'T>  =
        TaskSeqCode<'T>(fun sm -> 
            // This will yield with __stack_fin = false
            // This will resume with __stack_fin = true
            let __stack_fin = ResumableCode.Yield().Invoke(&sm)
            sm.Data.current <- ValueSome v
            sm.Data.awaiter <- null
            __stack_fin)

    member inline this.YieldFrom (source: IAsyncEnumerable<'T>) : TaskSeqCode<'T> =
        this.For(source, (fun v -> this.Yield(v)))

    member inline _.Bind (task: Task<'TResult1>, continuation: ('TResult1 -> TaskSeqCode<'T>)) : TaskSeqCode<'T> =
        TaskSeqCode<'T>(fun sm -> 
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
                if verbose then printfn "calling AwaitUnsafeOnCompleted"
                sm.Data.awaiter <- awaiter
                sm.Data.current <- ValueNone
                false)

    // TODO: using return! for tailcalls is wrong.  We should use yield! and have F#
    // desugar to a different builder method when in tailcall position
    //
    // Because of this using return! from non-tailcall position e.g. in a try-finally or try-with will
    // giv incorrect results (escaping the exception handler - 'close up shop and draw results from somewhere else')
    member inline b.ReturnFrom (other: IAsyncEnumerable<'T>) : TaskSeqCode<'T> = 
        TaskSeqCode<_>(fun sm -> 
            match other with 
            | :? TaskSeq<'T> as other -> 
                sm.Data.tailcallTarget <- Some other
                sm.Data.awaiter <- null
                sm.Data.current <- ValueNone
                // For tailcalls we return 'false' and re-run from the entry (trampoline)
                false 
            | _ -> 
                b.YieldFrom(other).Invoke(&sm)
            )

let taskSeq = TaskSeqBuilder()

module TaskSeq =
    let toList (t: taskSeq<'T>) =
        [ let e = t.GetAsyncEnumerator(CancellationToken())
          try 
              while (let vt = e.MoveNextAsync() in if vt.IsCompleted then vt.Result else vt.AsTask().Result) do 
                  yield e.Current
          finally
              e.DisposeAsync().AsTask().Wait() ]

    let toArray (t: taskSeq<'T>) =
        [| let e = t.GetAsyncEnumerator(CancellationToken())
           try 
              while (let vt = e.MoveNextAsync() in if vt.IsCompleted then vt.Result else vt.AsTask().Result) do 
                   yield e.Current
           finally 
               e.DisposeAsync().AsTask().Wait() |]

    let toArrayAsync (t: taskSeq<'T>) : Task<'T[]> =
       task { 
          let res = ResizeArray<'T>()
          let e = t.GetAsyncEnumerator(CancellationToken())
          let mutable go = true
          let! step = e.MoveNextAsync()
          go <- step
          while go do 
              res.Add e.Current
              if verbose then printfn "yield %A" e.Current
              let! step = e.MoveNextAsync()
              go <- step
          return res.ToArray()
       }

    let iter f (t: taskSeq<'T>) =
        let e = t.GetAsyncEnumerator(CancellationToken())
        try 
            while (let vt = e.MoveNextAsync() in if vt.IsCompleted then vt.Result else vt.AsTask().Result) do 
                f e.Current
        finally 
            e.DisposeAsync().AsTask().Wait()

module Examples =

    let t1 () = 
        taskSeq {
           printfn "in t1"
           yield "a"
           let x = 1
           let! v =
               task { 
                   printfn "hey"
                   do! Task.Delay(10) 
               }
           yield "b"
           let! v =
               task { 
                   printfn "hey yo"
                   do! Task.FromResult(())
               }
           yield "c"
           let! v =
               task { 
                   printfn "and a bottle of rum"
                   do! Task.Delay(0)
               }
           yield "d"
        }

    let testTailcallTiny () = 
        taskSeq {
           return! t1()
        }
    let rec testTailcall (n: int) = 
        taskSeq {
           if n % 100 = 0 then printfn $"in t1, n = {n}"
           yield n
           if n > 0 then
               return! testTailcall(n-1)
           //yield ()
        }

    //let t2 () = 
    //    taskSeq {
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
        taskSeq {
           yield 1
           yield 2
           if x >= 2 then 
               yield 3
               yield 4
        }

    let perf2 () = 
        taskSeq {
           for i1 in perf1 3 do
             for i2 in perf1 3 do
               for i3 in perf1 3 do
                 for i4 in perf1 3 do
                   for i5 in perf1 3 do
                      yield! perf1 i5
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

    let dumpTaskSeq (t: IAsyncEnumerable<_>) = 
        printfn "-----"
        let e = t.GetAsyncEnumerator(CancellationToken())
        while (let vt = e.MoveNextAsync() in if vt.IsCompleted then vt.Result else vt.AsTask().Result) do 
            printfn "yield %A" e.Current

    //dumpTaskSeq (t1())
    //dumpTaskSeq (testTailcallTiny())
    ////dumpTaskSeq (t2())

    //printfn "t1() = %A" (TaskSeq.toArray (t1()))
    //printfn "testTailcallTiny() = %A" (TaskSeq.toArray (testTailcallTiny()))
    //dumpTaskSeq (testTailcall(100000))
    //printfn "t2() = %A" (TaskSeq.toArray (t2()))

    printfn "perf2() = %A" (TaskSeq.toArray (perf2()) |> Array.sum)

