
module Tests.Seq2

#nowarn "42"
open System
open System.Collections
open System.Collections.Generic
open System.Runtime.CompilerServices
open FSharp.Core.CompilerServices
open FSharp.Core.CompilerServices.StateMachineHelpers

[<AbstractClass>]
type SeqStateMachine<'T>() =
    let disposalStack = ResizeArray<(unit -> unit)>()

    /// Proceed to the next state or raise an exception. Returns true if completed
    abstract Step : unit -> bool

    member val Current : 'T voption = ValueNone with get, set

    member val ResumptionPoint: int = 0 with get, set

    member val ResumptionFunc: (SeqStateMachine<'T> -> bool) = Unchecked.defaultof<_> with get, set

    interface IEnumerable with
        member this.GetEnumerator() = 
            // TODO: make new object if needed
            (this :> IEnumerator)

    interface IEnumerable<'T> with
        member this.GetEnumerator() = 
            // TODO: make new object if needed
            (this :> IEnumerator<'T>)

    interface IDisposable with
        member __.Dispose() = 
            let mutable exn = None
            for d in Seq.rev disposalStack do 
                try 
                    d()
                with e ->
                    exn <- Some e // keep the last exception - TODO - check this
            match exn with 
            | None -> () 
            | Some e -> raise e 

    interface IEnumerator with
        
        member __.Reset() = failwith "no reset supported"
        member sm.Current = box sm.Current        
        member sm.MoveNext() = sm.Step()
        
    interface IEnumerator<'T> with
        member sm.Current = match sm.Current with ValueNone -> failwith "no value available yet" | ValueSome x -> x

    member __.PushDispose (f: unit -> unit) = disposalStack.Add(f)

    member __.PopDispose () = disposalStack.RemoveAt(disposalStack.Count - 1)
    
    [<MethodImpl(MethodImplOptions.NoInlining)>]
    member sm.Start() = (sm :> IEnumerable<'T>)

type SeqCode<'T> = SeqStateMachine<'T> -> bool

type SeqBuilder() =
    
    [<NoDynamicInvocation>]
    member inline __.Delay(__expand_f : unit -> SeqCode<'T>) : SeqCode<'T> = (fun sm -> __expand_f () sm)

    [<NoDynamicInvocation>]
    member inline __.Run(__expand_code : SeqCode<'T>) : IEnumerable<'T> = 
        if __useResumableCode then
            (__resumableStateMachine
                { new SeqStateMachine<'T>() with 
                    member sm.Step () =
                     __resumeAt sm.ResumptionPoint
                     __expand_code sm }).Start()
        else
            let sm = 
                { new SeqStateMachine<'T>() with 
                    member sm.Step () = 
                       sm.ResumptionFunc sm }
            sm.ResumptionFunc <- __expand_code
            sm.Start()

    [<NoDynamicInvocation>]
    member inline __.Zero() : SeqCode<'T> =
        (fun _sm -> true)

    [<NoDynamicInvocation>]
    member inline __.Combine(__expand_task1: SeqCode<'T>, __expand_task2: SeqCode<'T>) : SeqCode<'T> =
        (fun sm -> 
            if __useResumableCode then
                let __stack_step = __expand_task1 sm
                if __stack_step then 
                    __expand_task2 sm
                else
                    false
            else
                let completed = __expand_task1 sm
                if completed then
                    __expand_task2 sm
                else 
                    // If state machines are not supported, then we must adjust the resumption to also run __expand_task2 on completion
                    let rec resume rf =
                        (fun (sm: SeqStateMachine<_>) -> 
                            let completed = rf sm
                            if completed then 
                                __expand_task2 sm
                            else
                                sm.ResumptionFunc <- resume sm.ResumptionFunc
                                false)

                    sm.ResumptionFunc <- resume sm.ResumptionFunc
                    false)
            
    [<NoDynamicInvocation>]
    member inline __.While(__expand_condition : unit -> bool, __expand_body : SeqCode<'T>) : SeqCode<'T> =
        (fun sm -> 
            if __useResumableCode then
                let mutable __stack_completed = false 
                while __stack_completed && __expand_condition() do
                    // NOTE: The body of the 'while' may contain await points, resuming may branch directly into the while loop
                    let __stack_step = __expand_body sm
                    // If we make it to the assignment we prove we've made a step 
                    __stack_completed <- __stack_step
                __stack_completed
            else
                let rec repeat sm = 
                    if __expand_condition() then 
                        let step = __expand_body sm
                        if step then
                            repeat sm
                        else
                            //Console.WriteLine("[{0}] rebinding ResumptionFunc for While", sm.MethodBuilder.Task.Id)
                            sm.ResumptionFunc <- resume sm.ResumptionFunc
                            false
                    else
                        true
                and resume mf sm =
                    //Console.WriteLine("[{0}] resume WhileLoop body", sm.MethodBuilder.Task.Id)
                    let step = mf sm
                    if step then 
                        repeat sm
                    else
                        //Console.WriteLine("[{0}] rebinding ResumptionFunc for While", sm.MethodBuilder.Task.Id)
                        sm.ResumptionFunc <- resume sm.ResumptionFunc
                        false

                repeat sm)

    [<NoDynamicInvocation>]
    member inline __.TryWith(__expand_body : SeqCode<'T>, __expand_catch : exn -> SeqCode<'T>) : SeqCode<'T> =
        (fun sm -> 
            if __useResumableCode then 
                let mutable __stack_completed = false
                let mutable __stack_caught = false
                let mutable __stack_savedExn = Unchecked.defaultof<_>
                try
                    // The try block may contain await points.
                    let __stack_step = __expand_body sm
                    // If we make it to the assignment we prove we've made a step
                    __stack_completed <- __stack_step
                with exn -> 
                    __stack_caught <- true
                    __stack_savedExn <- exn

                if __stack_caught then 
                    // Place the catch code outside the catch block 
                    __expand_catch __stack_savedExn sm
                else
                    __stack_completed
            else
                failwith "tbd")

    [<NoDynamicInvocation>]
    member inline __.TryFinally(__expand_body: SeqCode<'T>, compensation : unit -> unit) : SeqCode<'T> =
        (fun sm -> 
            let mutable completed = false
            sm.PushDispose compensation
            try
                let __stack_step = __expand_body sm
                // If we make it to the assignment we prove we've made a step without an exception
                completed <- __stack_step
            with _ ->
                sm.PopDispose()
                compensation()
                reraise()

            if completed then 
                sm.PopDispose()
                compensation()
            completed)

    [<NoDynamicInvocation>]
    member inline this.Using(disp : #IDisposable, __expand_body : #IDisposable -> SeqCode<'T>) = 
        // A using statement is just a try/finally with the finally block disposing if non-null.
        this.TryFinally(
            (fun sm -> __expand_body disp sm),
            (fun () -> if not (isNull (box disp)) then disp.Dispose()))

    [<NoDynamicInvocation>]
    member inline this.For(sequence : seq<'TElement>, __expand_body : 'TElement -> SeqCode<'T>) : SeqCode<'T> =
        // A for loop is just a using statement on the sequence's enumerator...
        this.Using (sequence.GetEnumerator(), 
            // ... and its body is a while loop that advances the enumerator and runs the body on each element.
            (fun e -> this.While((fun () -> e.MoveNext()), (fun sm -> __expand_body e.Current sm))))

    [<NoDynamicInvocation>]
    member inline __.Yield (v: 'T) : SeqCode<'T> =
        (fun sm ->
            if __useResumableCode then 
                match __resumableEntry() with
                | Some contID ->
                    sm.ResumptionPoint <- contID
                    sm.Current <- ValueSome v
                    false
                | None -> 
                    sm.Current <- ValueNone
                    true
            else
                let cont (sm: SeqStateMachine<'T>) =
                    sm.Current <- ValueNone
                    true
                sm.ResumptionFunc <- cont
                sm.Current <- ValueSome v
                false)

    [<NoDynamicInvocation>]
    member inline this.YieldFrom (source: IEnumerable<'T>) : SeqCode<'T> =
        this.For(source, (fun v -> this.Yield v))

let seq2 = SeqBuilder()

module Examples =

    let t1 () = 
        seq2 {
           printfn "in t1"
           yield "a"
           let x = 1
           yield "b"
           yield "c"
        }

    let t2 () = 
        seq2 {
           printfn "in t2"
           yield "d"
           for x in t1 () do 
               printfn "t2 - got %A" x
               yield "e"
               yield "[T1]" + x
           yield "f"
        }

    let dumpSeq (t: IEnumerable<_>) = 
        let e = t.GetEnumerator()
        while e.MoveNext() do 
            printfn "yield %A" e.Current
    dumpSeq (t1())
    dumpSeq (t2())
