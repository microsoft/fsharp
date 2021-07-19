
module Tests.ListBuilders

#if FSHARP_CORE_HAS_LIST_COLLECTOR
open System
open System.Collections.Generic
open FSharp.Core.CompilerServices

#nowarn "57"

[<AutoOpen>]
module UsingInlinedCodeAndCollector =
    [<Struct; NoEquality; NoComparison>]
    type ListBuilderCollector<'T> =
        [<DefaultValue(false)>]
        val mutable Collector : ListCollector<'T>

        member sm.Yield (value: 'T) = sm.Collector.Yield(value)
    
        member sm.ToList() = sm.Collector.ToList()
    
    type ListBuilderCode<'T> = delegate of byref<ListBuilderCollector<'T>> -> unit

    type ListBuilderViaCollector() =

        member inline _.Delay([<InlineIfLambda>] f: unit -> ListBuilderCode<'T>) : ListBuilderCode<'T> =
            ListBuilderCode<_>(fun sm -> (f()).Invoke &sm)

        member inline _.Zero() : ListBuilderCode<'T> =
            ListBuilderCode<_>(fun _sm -> ())

        member inline _.Combine([<InlineIfLambda>] part1: ListBuilderCode<'T>, [<InlineIfLambda>] part2: ListBuilderCode<'T>) : ListBuilderCode<'T> =
            ListBuilderCode<_>(fun sm -> 
                part1.Invoke &sm
                part2.Invoke &sm)
            
        member inline _.While([<InlineIfLambda>] condition : unit -> bool, [<InlineIfLambda>] body : ListBuilderCode<'T>) : ListBuilderCode<'T> =
            ListBuilderCode<_>(fun sm -> 
                while condition() do
                    body.Invoke &sm)

        member inline _.TryWith([<InlineIfLambda>] body: ListBuilderCode<'T>, [<InlineIfLambda>] handler: exn -> ListBuilderCode<'T>) : ListBuilderCode<'T> =
            ListBuilderCode<_>(fun sm -> 
                try
                    body.Invoke &sm
                with exn -> 
                    (handler exn).Invoke &sm)

        member inline _.TryFinally([<InlineIfLambda>] body: ListBuilderCode<'T>, compensation : unit -> unit) : ListBuilderCode<'T> =
            ListBuilderCode<_>(fun sm -> 
                try
                    body.Invoke &sm
                with _ ->
                    compensation()
                    reraise()

                compensation())

        member inline b.Using(disp : #IDisposable, [<InlineIfLambda>] body: #IDisposable -> ListBuilderCode<'T>) : ListBuilderCode<'T> = 
            // A using statement is just a try/finally with the finally block disposing if non-null.
            b.TryFinally(
                (fun sm -> (body disp).Invoke &sm),
                (fun () -> if not (isNull (box disp)) then disp.Dispose()))

        member inline b.For(sequence: seq<'TElement>, [<InlineIfLambda>] body: 'TElement -> ListBuilderCode<'T>) : ListBuilderCode<'T> =
            b.Using (sequence.GetEnumerator(), 
                (fun e -> b.While((fun () -> e.MoveNext()), (fun sm -> (body e.Current).Invoke &sm))))

        member inline _.Yield (v: 'T) : ListBuilderCode<'T> =
            ListBuilderCode<_>(fun sm ->
                sm.Yield v)

        member inline b.YieldFrom (source: IEnumerable<'T>) : ListBuilderCode<'T> =
            b.For(source, (fun value -> b.Yield(value)))

        member inline _.Run([<InlineIfLambda>] code: ListBuilderCode<'T>) : 'T list = 
            let mutable sm = ListBuilderCollector<'T>()
            code.Invoke &sm
            sm.ToList()

    let listc = ListBuilderViaCollector()

module Examples =
    let t1C () = 
        listc {
           printfn "in t1"
           yield "a"
           let x = "d"
           yield "b"
           yield "c" + x
        }

    let t2C () = 
        listc {
           printfn "in t2"
           yield "d"
           for x in t1C () do 
               printfn "t2 - got %A" x
               yield "e"
               yield "[T1]" + x
           yield "f"
        }

    let tinyVariableSizeNew () = 
        for i in 1 .. 1000000 do
            listc {
               if i % 3 = 0 then 
                   yield "b"
            } |> List.length |> ignore

    let tinyVariableSizeBuiltin () = 
        for i in 1 .. 1000000 do
            [
               if i % 3 = 0 then 
                   yield "b"
            ] |> List.length |> ignore

    let variableSizeNew () = 
        for i in 1 .. 1000000 do
            listc {
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
            } |> List.length |> ignore

    let variableSizeBuiltin () = 
        for i in 1 .. 1000000 do
            [
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
            ] |> List.length |> ignore

    let fixedSizeC () = 
        for i in 1 .. 1000000 do
            listc {
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
             } |> List.length |> ignore

    let fixedSizeBase () = 
        for i in 1 .. 1000000 do
            [
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
            ] |> List.length |> ignore

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
#endif