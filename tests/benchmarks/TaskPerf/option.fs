
module Tests.OptionBuilders

open System

type OptionCode<'T> = unit -> 'T voption

type OptionBuilderUsingInlineIfLambdaBase() =

    member inline _.Delay([<InlineIfLambda>] f : unit -> OptionCode<'T>) : OptionCode<'T> =
        (fun () -> (f())())
        // Note, not "f()()" - the F# compiler optimzier likes arguments to match lamdas in order to preserve
        // argument evaluation order, so for "(f())()" the optimizer reduces one lambda then another, while "f()()" doesn't

    member inline _.Combine([<InlineIfLambda>] task1: OptionCode<unit>, [<InlineIfLambda>] task2: OptionCode<'T>) : OptionCode<'T> =
        (fun () -> 
            match task1() with
            | ValueNone -> ValueNone
            | ValueSome() -> task2())

    member inline _.Bind(res1: 'T1 option, [<InlineIfLambda>] task2: ('T1 -> OptionCode<'T>)) : OptionCode<'T> =
        (fun () -> 
            match res1 with 
            | None -> ValueNone
            | Some v -> (task2 v)())

    member inline _.Bind(res1: 'T1 voption, [<InlineIfLambda>] task2: ('T1 -> OptionCode<'T>)) : OptionCode<'T> =
        (fun () -> 
            match res1 with 
            | ValueNone -> ValueNone
            | ValueSome v -> (task2 v)())
            
    member inline _.While([<InlineIfLambda>] condition : unit -> bool, [<InlineIfLambda>] body : OptionCode<unit>) : OptionCode<unit> =
        (fun () -> 
            let mutable proceed = true
            while proceed && condition() do
                match body() with 
                | ValueNone -> proceed <- false
                | ValueSome () -> ()
            ValueSome(()))

    member inline _.TryWith([<InlineIfLambda>] body : OptionCode<'T>, [<InlineIfLambda>] catch : exn -> OptionCode<'T>) : OptionCode<'T> =
        (fun () -> 
            try
                body()
            with exn -> 
                (catch exn)())

    member inline _.TryFinally([<InlineIfLambda>] body: OptionCode<'T>, [<InlineIfLambda>] compensation : unit -> unit) : OptionCode<'T> =
        (fun () -> 
            let res =
                try
                    body()
                with _ ->
                    compensation()
                    reraise()

            compensation()
            res)

    member inline this.Using(disp: #IDisposable, [<InlineIfLambda>] body: #IDisposable -> OptionCode<'T>) : OptionCode<'T> = 
        // A using statement is just a try/finally with the finally block disposing if non-null.
        this.TryFinally(
            (fun () -> (body disp)()),
            (fun () -> if not (isNull (box disp)) then disp.Dispose()))

    member inline this.For(sequence : seq<'TElement>, [<InlineIfLambda>] body : 'TElement -> OptionCode<unit>) : OptionCode<unit> =
        this.Using (sequence.GetEnumerator(), 
            (fun e -> this.While((fun () -> e.MoveNext()), (fun () -> (body e.Current)()))))

    member inline _.Return (value: 'T) : OptionCode<'T> =
        (fun () ->
            ValueSome value)

    member inline this.ReturnFrom (source: option<'T>) : OptionCode<'T> =
        (fun () ->
            match source with Some x -> ValueOption.Some x | None -> ValueOption.None)

    member inline this.ReturnFrom (source: voption<'T>) : OptionCode<'T> =
        (fun () -> source)

type OptionBuilderUsingInlineIfLambda() =
    inherit OptionBuilderUsingInlineIfLambdaBase()
   
    member inline _.Run([<InlineIfLambda>] code : OptionCode<'T>) : 'T option = 
         match code () with 
         | ValueNone -> None
         | ValueSome v -> Some v

type ValueOptionBuilderUsingInlineIfLambda() =
    inherit OptionBuilderUsingInlineIfLambdaBase()

    member inline _.Run([<InlineIfLambda>] code : OptionCode<'T>) : 'T voption = 
        code()

let optionNew = OptionBuilderUsingInlineIfLambda()
let voptionNew = ValueOptionBuilderUsingInlineIfLambda()


type SlowOptionBuilder() =
    member inline _.Zero() = None

    member inline _.Return(x: 'T) = Some x

    member inline _.ReturnFrom(m: 'T option) = m

    member inline _.Bind(m: 'T option, f) = Option.bind f m

    member inline _.Delay(f: unit -> _) = f

    member inline _.Run(f) = f()

    member this.TryWith(delayedExpr, handler) =
        try this.Run(delayedExpr)
        with exn -> handler exn

    member this.TryFinally(delayedExpr, compensation) =
        try this.Run(delayedExpr)
        finally compensation()

    member this.Using(resource:#IDisposable, body) =
        this.TryFinally(this.Delay(fun ()->body resource), fun () -> match box resource with null -> () | _ -> resource.Dispose())

let optionOld = SlowOptionBuilder()

type SlowValueOptionBuilder() =
    member inline _.Zero() = ValueNone

    member inline _.Return(x: 'T) = ValueSome x

    member inline _.ReturnFrom(m: 'T voption) = m

    member inline _.Bind(m: 'T voption, f) = ValueOption.bind f m

    member inline _.Delay(f: unit -> _) = f

    member inline _.Run(f) = f()

    member inline this.TryWith(delayedExpr, handler) =
        try this.Run(delayedExpr)
        with exn -> handler exn

    member inline this.TryFinally(delayedExpr, compensation) =
        try this.Run(delayedExpr)
        finally compensation()

    member inline this.Using(resource:#IDisposable, body) =
        this.TryFinally(this.Delay(fun ()->body resource), fun () -> match box resource with null -> () | _ -> resource.Dispose())

let voptionOld = SlowValueOptionBuilder()

module Examples =


    let multiStepOldBuilder () = 
        let mutable res = 0
        for i in 1 .. 1000000 do
            let v = 
                optionOld {
                   try 
                      let! x1 = (if i % 5 <> 2 then Some i else None)
                      let! x2 = (if i % 3 <> 1 then Some i else None)
                      let! x3 = (if i % 3 <> 1 then Some i else None)
                      let! x4 = (if i % 3 <> 1 then Some i else None)
                      res <- res + 1 
                      return x1 + x2 + x3 + x4
                   with e -> 
                      return failwith "unexpected"
                } 
            v |> ignore
        res

    let multiStepOldBuilderV () = 
        let mutable res = 0
        for i in 1 .. 1000000 do
            let v = 
                voptionOld {
                   try 
                      let! x1 = (if i % 5 <> 2 then ValueSome i else ValueNone)
                      let! x2 = (if i % 3 <> 1 then ValueSome i else ValueNone)
                      let! x3 = (if i % 3 <> 1 then ValueSome i else ValueNone)
                      let! x4 = (if i % 3 <> 1 then ValueSome i else ValueNone)
                      res <- res + 1 
                      return x1 + x2 + x3 + x4
                   with e -> 
                      return failwith "unexpected"
                } 
            v |> ignore
        res

    let multiStepNoBuilder () = 
        let mutable res = 0
        for i in 1 .. 1000000 do
            let v = 
                try 
                    match (if i % 5 <> 2 then Some i else None) with
                    | None -> None
                    | Some x1 -> 
                    match (if i % 3 <> 1 then Some i else None) with
                    | None -> None
                    | Some x2 -> 
                    match (if i % 3 <> 1 then Some i else None) with
                    | None -> None
                    | Some x3 -> 
                    match (if i % 3 <> 1 then Some i else None) with
                    | None -> None
                    | Some x4 -> 
                    res <- res + 1 
                    Some (x1 + x2 + x3 + x4)
                with e -> 
                    failwith "unexpected"
            v |> ignore
        res

    let multiStepNoBuilderV () = 
        let mutable res = 0
        for i in 1 .. 1000000 do
            let v = 
                try 
                    match (if i % 5 <> 2 then ValueSome i else ValueNone) with
                    | ValueNone -> ValueNone
                    | ValueSome x1 -> 
                    match (if i % 3 <> 1 then ValueSome i else ValueNone) with
                    | ValueNone -> ValueNone
                    | ValueSome x2 -> 
                    match (if i % 3 <> 1 then ValueSome i else ValueNone) with
                    | ValueNone -> ValueNone
                    | ValueSome x3 -> 
                    match (if i % 3 <> 1 then ValueSome i else ValueNone) with
                    | ValueNone -> ValueNone
                    | ValueSome x4 -> 
                    res <- res + 1 
                    ValueSome (x1 + x2 + x3 + x4)
                with e -> 
                    failwith "unexpected"
            v |> ignore
        res

    let multiStepNewBuilder () = 
        let mutable res = 0
        for i in 1 .. 1000000 do
            let v = 
                optionNew {
                   try 
                      let! x1 = (if i % 5 <> 2 then Some i else None)
                      let! x2 = (if i % 3 <> 1 then Some i else None)
                      let! x3 = (if i % 3 <> 1 then Some i else None)
                      let! x4 = (if i % 3 <> 1 then Some i else None)
                      res <- res + 1 
                      return x1 + x2 + x3 + x4
                   with e -> 
                      return failwith "unexpected"
                } 
            v |> ignore
        res


    let multiStepNewBuilderV () = 
        let mutable res = 0
        for i in 1 .. 1000000 do
            let v = 
                voptionNew {
                   try 
                      let! x1 = (if i % 5 <> 2 then ValueSome i else ValueNone)
                      let! x2 = (if i % 3 <> 1 then ValueSome i else ValueNone)
                      let! x3 = (if i % 3 <> 1 then ValueSome i else ValueNone)
                      let! x4 = (if i % 3 <> 1 then ValueSome i else ValueNone)
                      res <- res + 1 
                      return x1 + x2 + x3 + x4
                   with e -> 
                      return failwith "unexpected"
                } 
            v |> ignore
        res

    // let perf s f = 
    //     let t = System.Diagnostics.Stopwatch()
    //     t.Start()
    //     for i in 1 .. 100 do 
    //         f() |> ignore
    //     t.Stop()
    //     printfn "PERF: %s : %d" s t.ElapsedMilliseconds

    // printfn "check %d = %d = %d"(multiStepStateMachineBuilder()) (multiStepNoBuilder()) (multiStepOldBuilder())

    // perf "perf (state mechine option)" multiStepStateMachineBuilder 
    // perf "perf (no builder option)" multiStepNoBuilder 
    // perf "perf (slow builder option)" multiStepOldBuilder 

    // printfn "check %d = %d = %d" (multiStepStateMachineBuilderV()) (multiStepNoBuilder()) (multiStepOldBuilder())
    // perf "perf (state mechine voption)" multiStepStateMachineBuilderV
    // perf "perf (no builder voption)" multiStepNoBuilderV
    // perf "perf (slow builder voption)" multiStepOldBuilderV

module A =

    let multiStepNewBuilder (i) = 
        let mutable res = 0
        optionNew {
            try 
                let! x1 = (if i % 5 <> 2 then Some i else None)
                let! x2 = (if i % 3 <> 1 then Some i else None)
                let! x3 = (if i % 3 <> 1 then Some i else None)
                let! x4 = (if i % 3 <> 1 then Some i else None)
                res <- res + 1 
                return x1 + x2 + x3 + x4
            with e -> 
                return failwith "unexpected"
        } 

