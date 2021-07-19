
module Tests.ArrayBuilders

open System
open System.Collections.Generic

[<AutoOpen>]
module UsingInlinedCodeAndCollector =

    [<Struct; NoEquality; NoComparison>]
    type ArrayBuilderCollector<'T> =
        [<DefaultValue(false)>]
        val mutable Result : ResizeArray<'T>

        member sm.Yield (value: 'T) = 
            match sm.Result with 
            | null -> 
                let ra = ResizeArray()
                sm.Result <- ra
                ra.Add(value)
            | ra -> ra.Add(value)

        member sm.ToResizeArray() = 
            match sm.Result with 
            | null -> ResizeArray()
            | ra -> ra
    
        member sm.ToArray() = 
            match sm.Result with 
            | null -> Array.empty
            | ra -> ra.ToArray()
    
    type ArrayBuilderCode<'T> = delegate of byref<ArrayBuilderCollector<'T>> -> unit

    type ArrayBuilderViaCollector() =

        member inline _.Delay([<InlineIfLambda>] f: unit -> ArrayBuilderCode<'T>) : ArrayBuilderCode<'T> =
            ArrayBuilderCode<_>(fun sm -> (f()).Invoke &sm)

        member inline _.Zero() : ArrayBuilderCode<'T> =
            ArrayBuilderCode<_>(fun _sm -> ())

        member inline _.Combine([<InlineIfLambda>] part1: ArrayBuilderCode<'T>, [<InlineIfLambda>] part2: ArrayBuilderCode<'T>) : ArrayBuilderCode<'T> =
            ArrayBuilderCode<_>(fun sm -> 
                part1.Invoke &sm
                part2.Invoke &sm)
            
        member inline _.While([<InlineIfLambda>] condition : unit -> bool, [<InlineIfLambda>] body : ArrayBuilderCode<'T>) : ArrayBuilderCode<'T> =
            ArrayBuilderCode<_>(fun sm -> 
                while condition() do
                    body.Invoke &sm)

        member inline _.TryWith([<InlineIfLambda>] body: ArrayBuilderCode<'T>, [<InlineIfLambda>] handler: exn -> ArrayBuilderCode<'T>) : ArrayBuilderCode<'T> =
            ArrayBuilderCode<_>(fun sm -> 
                try
                    body.Invoke &sm
                with exn -> 
                    (handler exn).Invoke &sm)

        member inline _.TryFinally([<InlineIfLambda>] body: ArrayBuilderCode<'T>, compensation : unit -> unit) : ArrayBuilderCode<'T> =
            ArrayBuilderCode<_>(fun sm -> 
                try
                    body.Invoke &sm
                with _ ->
                    compensation()
                    reraise()

                compensation())

        member inline b.Using(disp : #IDisposable, [<InlineIfLambda>] body: #IDisposable -> ArrayBuilderCode<'T>) : ArrayBuilderCode<'T> = 
            // A using statement is just a try/finally with the finally block disposing if non-null.
            b.TryFinally(
                (fun sm -> (body disp).Invoke &sm),
                (fun () -> if not (isNull (box disp)) then disp.Dispose()))

        member inline b.For(sequence: seq<'TElement>, [<InlineIfLambda>] body: 'TElement -> ArrayBuilderCode<'T>) : ArrayBuilderCode<'T> =
            b.Using (sequence.GetEnumerator(), 
                (fun e -> b.While((fun () -> e.MoveNext()), (fun sm -> (body e.Current).Invoke &sm))))

        member inline _.Yield (v: 'T) : ArrayBuilderCode<'T> =
            ArrayBuilderCode<_>(fun sm ->
                sm.Yield v)

        member inline b.YieldFrom (source: IEnumerable<'T>) : ArrayBuilderCode<'T> =
            b.For(source, (fun value -> b.Yield(value)))

        member inline _.Run([<InlineIfLambda>] code: ArrayBuilderCode<'T>) : 'T[] = 
            let mutable sm = ArrayBuilderCollector<'T>()
            code.Invoke &sm
            sm.ToArray()

    let arrayNew = ArrayBuilderViaCollector()

module Examples =

    let tinyVariableSizeNew () = 
        for i in 1 .. 1000000 do
            arrayNew {
               if i % 3 = 0 then 
                   yield "b"
            } |> Array.length |> ignore

    let tinyVariableSizeBuiltin () = 
        for i in 1 .. 1000000 do
            [|
               if i % 3 = 0 then 
                   yield "b"
            |] |> Array.length |> ignore

    let variableSizeNew () = 
        for i in 1 .. 1000000 do
            arrayNew {
               yield "a"
               yield "b"
               yield "b"
               yield "b"
               yield "b"
               if i % 3 = 0 then 
                   yield "b"
                   yield "b"
                   yield "b"
                   yield "b"
               yield "c"
            } |> Array.length |> ignore

    let variableSizeBuiltin () = 
        for i in 1 .. 1000000 do
            [|
               yield "a"
               yield "b"
               yield "b"
               yield "b"
               yield "b"
               if i % 3 = 0 then 
                   yield "b"
                   yield "b"
                   yield "b"
                   yield "b"
               yield "c"
            |] |> Array.length |> ignore

    let fixedSizeC () = 
        for i in 1 .. 1000000 do
            arrayNew {
               "a"
               "b"
               "b"
               "b"
               "b"
               "b"
               "b"
               "b"
               "b"
               "c"
             } |> Array.length |> ignore

    let fixedSizeBase () = 
        for i in 1 .. 1000000 do
            [|
               "a"
               "b"
               "b"
               "b"
               "b"
               "b"
               "b"
               "b"
               "b"
               "c"
            |] |> Array.length |> ignore

    let perf s f = 
        let t = System.Diagnostics.Stopwatch()
        t.Start()
        f()
        t.Stop()
        printfn "PERF: %s : %d" s t.ElapsedMilliseconds

    perf "tinyVariableSizeBuiltin" tinyVariableSizeBuiltin
    perf "tinyVariableSizeNew " tinyVariableSizeNew

    perf "variableSizeBuiltin" variableSizeBuiltin
    perf "variableSizeNew" variableSizeNew

    perf "fixedSizeBase" fixedSizeBase
    perf "fixedSizeC" fixedSizeC
    // let dumpSeq (t: IEnumerable<_>) = 
    //     let e = t.GetEnumerator()
    //     while e.MoveNext() do 
    //         printfn "yield %A" e.Current
    // dumpSeq (t1())
    // dumpSeq (t2())
