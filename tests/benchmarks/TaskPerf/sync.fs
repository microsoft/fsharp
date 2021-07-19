
module Tests.SyncBuilder

open System

type SyncCode<'T> = unit -> 'T

type SyncBuilder() =
    
    member inline _.Delay([<InlineIfLambda>] f: unit -> SyncCode<'T>) :  SyncCode<'T> =
        (fun () -> (f())())

    member inline _.Run([<InlineIfLambda>] code : SyncCode<'T>) : 'T = 
        code()

#if PREVIEW
    [<DefaultValue>]
#endif
    member inline _.Zero() : SyncCode< unit> = 
        (fun () -> ())

    member inline _.Return (x: 'T) : SyncCode<'T> =
        (fun () -> x)

    member inline _.Combine([<InlineIfLambda>] code1: SyncCode<unit>, [<InlineIfLambda>] code2: SyncCode<'T>) : SyncCode<'T> =
        (fun () -> 
            code1()
            code2())

    member inline _.While([<InlineIfLambda>] condition: unit -> bool, [<InlineIfLambda>] body: SyncCode<unit>) : SyncCode<unit> =
       (fun () -> 
            while condition() do
                body())

    member inline _.TryWith([<InlineIfLambda>] body: SyncCode<'T>, [<InlineIfLambda>] catch: exn -> 'T) : SyncCode<'T> =
        (fun () -> 
            try
                body()
            with exn -> 
                catch exn)

    member inline _.TryFinally([<InlineIfLambda>] body: SyncCode<'T>, compensation: unit -> unit) : SyncCode<'T> =
        (fun () -> 
            let __stack_step = 
                try
                    body()
                with _ ->
                    compensation()
                    reraise()
            compensation()
            __stack_step)

    member inline this.Using(disp : #IDisposable, [<InlineIfLambda>] body: #IDisposable -> SyncCode<'T>) : SyncCode<'T> = 
        this.TryFinally(
            (fun () -> (body disp)()),
            (fun () -> if not (isNull (box disp)) then disp.Dispose()))

    member inline this.For(sequence : seq<'T>, [<InlineIfLambda>] body : 'T -> SyncCode<unit>) : SyncCode<unit> =
        this.Using (sequence.GetEnumerator(), 
            (fun e -> this.While((fun () -> e.MoveNext()), (fun () -> (body e.Current)()))))

    member inline _.ReturnFrom (value: 'T) : SyncCode<'T> =
        (fun () -> 
              value)

    member inline _.Bind (v: 'TResult1, [<InlineIfLambda>] continuation: 'TResult1 -> SyncCode<'TResult2>) : SyncCode<'TResult2> =
        (fun () -> 
             (continuation v)())

let sync = SyncBuilder()

module Examples =

     let t1 y = 
         sync {
            let x = 4 + 5 + y
            return x
         }

     let t2 y = 
         sync {
            printfn "in t2"
            let! x = t1 y
            return x + y
         }


     //printfn "t2 6 = %d" (t2 6)
