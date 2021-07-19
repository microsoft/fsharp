// Tests for TaskBuilder.fs
//
// Written in 2016 by Robert Peele (humbobst@gmail.com)
//
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights
// to this software to the public domain worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along with this software.
// If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.


// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

// Various tests for the:
// Microsoft.FSharp.Control.Async type

namespace FSharp.Core.UnitTests.Control.Tasks

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.Threading
open System.Threading.Tasks
open Microsoft.FSharp.Control
#if STANDALONE
[<AttributeUsage(AttributeTargets.Method, AllowMultiple=false)>]
type FactAttribute() = inherit Attribute()
#else
open Xunit
#endif


type ITaskThing =
    abstract member Taskify : 'a option -> 'a Task

#if NETCOREAPP
type SupportBothDisposables() =
    let mutable called = false
    interface IAsyncDisposable with 
        member __.DisposeAsync() = 
            task { 
                System.Console.WriteLine "incrementing"
                called <- true }
            |> ValueTask
    interface IDisposable with 
        member __.Dispose() =  failwith "dispose"
    member x.Disposed = called
#endif
type SmokeTestsForCompilation() =

    [<Fact>]
    member __.tinyTask() =
        task {
            return 1
        }
        |> fun t -> 
            t.Wait()
            if t.Result <> 1 then failwith "failed"

    [<Fact>]
    member __.tbind() =
        task {
            let! x = Task.FromResult(1)
            return 1 + x
        }
        |> fun t -> 
            t.Wait()
            if t.Result <> 2 then failwith "failed"

    [<Fact>]
    member __.tnested() =
        task {
            let! x = task { return 1 }
            return x
        }
        |> fun t -> 
            t.Wait()
            if t.Result <> 1 then failwith "failed"

    [<Fact>]
    member __.tcatch0() =
        task {
            try 
               return 1
            with e -> 
               return 2
        }
        |> fun t -> 
            t.Wait()
            if t.Result <> 1 then failwith "failed"

    [<Fact>]
    member __.tcatch1() =
        task {
            try 
               let! x = Task.FromResult 1
               return x
            with e -> 
               return 2
        }
        |> fun t -> 
            t.Wait()
            if t.Result <> 1 then failwith "failed"


    [<Fact>]
    member __.t3() =
        let t2() =
            task {
                System.Console.WriteLine("hello")
                return 1
            }
        task {
            System.Console.WriteLine("hello")
            let! x = t2()
            System.Console.WriteLine("world")
            return 1 + x
        }
        |> fun t -> 
            t.Wait()
            if t.Result <> 2 then failwith "failed"

    [<Fact>]
    member __.t3b() =
        task {
            System.Console.WriteLine("hello")
            let! x = Task.FromResult(1)
            System.Console.WriteLine("world")
            return 1 + x
        }
        |> fun t -> 
            t.Wait()
            if t.Result <> 2 then failwith "failed"

    [<Fact>]
    member __.t3c() =
        task {
            System.Console.WriteLine("hello")
            do! Task.Delay(100)
            System.Console.WriteLine("world")
            return 1 
        }
        |> fun t -> 
            t.Wait()
            if t.Result <> 1 then failwith "failed"

    [<Fact>]
    // This tests an exception match
    member __.t67() =
        task {
            try
                do! Task.Delay(0)
            with
            | :? ArgumentException -> 
                ()
            | _ -> 
                ()
        }
        |> fun t -> 
            t.Wait()
            if t.Result <> () then failwith "failed"

    [<Fact>]
    // This tests compiling an incomplete exception match
    member __.t68() =
        task {
            try
                do! Task.Delay(0)
            with
            | :? ArgumentException -> 
                ()
        }
        |> fun t -> 
            t.Wait()
            if t.Result <> () then failwith "failed"

    [<Fact>]
    member __.testCompileAsyncWhileLoop() =
        task {
            let mutable i = 0
            while i < 5 do
                i <- i + 1
                do! Task.Yield()
            return i
        }
        |> fun t -> 
            t.Wait()
            if t.Result <> 5 then failwith "failed"


exception TestException of string

[<AutoOpen>]
module Helpers = 
    let BIG = 10
    // let BIG = 10000
    let require x msg = if not x then failwith msg
    let failtest str = raise (TestException str)

type Basics() = 
    [<Fact>]
    member __.testShortCircuitResult() =
        printfn "Running testShortCircuitResult..."
        let t =
            task {
                let! x = Task.FromResult(1)
                let! y = Task.FromResult(2)
                return x + y
            }
        require t.IsCompleted "didn't short-circuit already completed tasks"
        printfn "t.Result = %A" t.Result
        require (t.Result = 3) "wrong result"

    [<Fact>]
    member __.testDelay() =
        printfn "Running testDelay..."
        let mutable x = 0
        let t =
            task {
                do! Task.Delay(50)
                x <- x + 1
            }
        printfn "task created and first step run...."
        require (x = 0) "task already ran"
        printfn "waiting...."
        t.Wait()

    [<Fact>]
    member __.testNoDelay() =
        printfn "Running testNoDelay..."
        let mutable x = 0
        let t =
            task {
                x <- x + 1
                do! Task.Delay(5)
                x <- x + 1
            }
        require (x = 1) "first part didn't run yet"
        t.Wait()

    [<Fact>]
    member __.testNonBlocking() =
        printfn "Running testNonBlocking..."
        let sw = Stopwatch()
        sw.Start()
        let t =
            task {
                do! Task.Yield()
                Thread.Sleep(100)
            }
        sw.Stop()
        require (sw.ElapsedMilliseconds < 50L) "sleep blocked caller"
        t.Wait()

    [<Fact>]
    member __.testCatching1() =
        printfn "Running testCatching1..."
        let mutable x = 0
        let mutable y = 0
        let t =
            task {
                try
                    do! Task.Delay(0)
                    failtest "hello"
                    x <- 1
                    do! Task.Delay(100)
                with
                | TestException msg ->
                    require (msg = "hello") "message tampered"
                | _ ->
                    require false "other exn type"
                    require false "other exn type"
                y <- 1
            }
        t.Wait()
        require (y = 1) "bailed after exn"
        require (x = 0) "ran past failure"

    [<Fact>]
    member __.testCatching2() =
        printfn "Running testCatching2..."
        let mutable x = 0
        let mutable y = 0
        let t =
            task {
                try
                    do! Task.Yield() // can't skip through this
                    failtest "hello"
                    x <- 1
                    do! Task.Delay(100)
                with
                | TestException msg ->
                    require (msg = "hello") "message tampered"
                | _ ->
                    require false "other exn type"
                y <- 1
            }
        t.Wait()
        require (y = 1) "bailed after exn"
        require (x = 0) "ran past failure"

    [<Fact>]
    member __.testNestedCatching() =
        printfn "Running testNestedCatching..."
        let mutable counter = 1
        let mutable caughtInner = 0
        let mutable caughtOuter = 0
        let t1() =
            task {
                try
                    do! Task.Yield()
                    failtest "hello"
                with
                | TestException msg as exn ->
                    caughtInner <- counter
                    counter <- counter + 1
                    raise exn
            }
        let t2 =
            task {
                try
                    do! t1()
                with
                | TestException msg as exn ->
                    caughtOuter <- counter
                    raise exn
                | e ->
                    require false (sprintf "invalid msg type %s" e.Message)
            }
        try
            t2.Wait()
            require false "ran past failed task wait"
        with
        | :? AggregateException as exn ->
            require (exn.InnerExceptions.Count = 1) "more than 1 exn"
        require (caughtInner = 1) "didn't catch inner"
        require (caughtOuter = 2) "didn't catch outer"

    [<Fact>]
    member __.testWhileLoopSync() =
        printfn "Running testWhileLoopSync..."
        let t =
            task {
                let mutable i = 0
                while i < 10 do
                    i <- i + 1
                return i
            }
        //t.Wait() no wait required for sync loop
        require (t.IsCompleted) "didn't do sync while loop properly - not completed"
        require (t.Result = 10) "didn't do sync while loop properly - wrong result"

    [<Fact>]
    member __.testWhileLoopAsyncZeroIteration() =
        printfn "Running testWhileLoopAsyncZeroIteration..."
        for i in 1 .. 5 do 
            let t =
                task {
                    let mutable i = 0
                    while i < 0 do
                        i <- i + 1
                        do! Task.Yield()
                    return i
                }
            t.Wait()
            require (t.Result = 0) "didn't do while loop properly"

    [<Fact>]
    member __.testWhileLoopAsyncOneIteration() =
        printfn "Running testWhileLoopAsyncOneIteration..."
        for i in 1 .. 5 do 
            let t =
                task {
                    let mutable i = 0
                    while i < 1 do
                        i <- i + 1
                        do! Task.Yield()
                    return i
                }
            t.Wait()
            require (t.Result = 1) "didn't do while loop properly"

    [<Fact>]
    member __.testWhileLoopAsync() =
        printfn "Running testWhileLoopAsync..."
        for i in 1 .. 5 do 
            let t =
                task {
                    let mutable i = 0
                    while i < 10 do
                        i <- i + 1
                        do! Task.Yield()
                    return i
                }
            t.Wait()
            require (t.Result = 10) "didn't do while loop properly"

    [<Fact>]
    member __.testTryFinallyHappyPath() =
        printfn "Running testTryFinallyHappyPath..."
        for i in 1 .. 5 do 
            let mutable ran = false
            let t =
                task {
                    try
                        require (not ran) "ran way early"
                        do! Task.Delay(100)
                        require (not ran) "ran kinda early"
                    finally
                        ran <- true
                }
            t.Wait()
            require ran "never ran"
    [<Fact>]
    member __.testTryFinallySadPath() =
        printfn "Running testTryFinallySadPath..."
        for i in 1 .. 5 do 
            let mutable ran = false
            let t =
                task {
                    try
                        require (not ran) "ran way early"
                        do! Task.Delay(100)
                        require (not ran) "ran kinda early"
                        failtest "uhoh"
                    finally
                        ran <- true
                }
            try
                t.Wait()
            with
            | _ -> ()
            require ran "never ran"

    [<Fact>]
    member __.testTryFinallyCaught() =
        printfn "Running testTryFinallyCaught..."
        for i in 1 .. 5 do 
            let mutable ran = false
            let t =
                task {
                    try
                        try
                            require (not ran) "ran way early"
                            do! Task.Delay(100)
                            require (not ran) "ran kinda early"
                            failtest "uhoh"
                        finally
                            ran <- true
                        return 1
                    with
                    | _ -> return 2
                }
            require (t.Result = 2) "wrong return"
            require ran "never ran"
    
    [<Fact>]
    member __.testUsing() =
        printfn "Running testUsing..."
        for i in 1 .. 5 do 
            let mutable disposed = false
            let t =
                task {
                    use d = { new IDisposable with member __.Dispose() = disposed <- true }
                    require (not disposed) "disposed way early"
                    do! Task.Delay(100)
                    require (not disposed) "disposed kinda early"
                }
            t.Wait()
            require disposed "never disposed B"

#if NETCOREAPP
    [<Fact>]
    member __.testUsingAsyncDisposableSync() =
        printfn "Running testUsingAsyncDisposableSync..."
        for i in 1 .. 5 do 
            let mutable disposed = 0
            let t =
                task {
                    use d = 
                        { new IAsyncDisposable with 
                            member __.DisposeAsync() = 
                                task { 
                                   System.Console.WriteLine "incrementing"
                                   disposed <- disposed + 1 }
                                |> ValueTask 
                        }
                    require (disposed = 0) "disposed way early"
                    System.Console.WriteLine "delaying"
                    do! Task.Delay(100)
                    System.Console.WriteLine "testing"
                    require (disposed = 0) "disposed kinda early"
                }
            t.Wait()
            require (disposed >= 1) "never disposed B"
            require (disposed <= 1) "too many dispose on B"

    [<Fact>]
    member __.testUsingAsyncDisposableAsync() =
        printfn "Running testUsingAsyncDisposableAsync..."
        for i in 1 .. 5 do 
            let mutable disposed = 0
            let t =
                task {
                    use d = 
                        { new IAsyncDisposable with 
                            member __.DisposeAsync() = 
                                task { 
                                    do! Task.Delay(10)
                                    disposed <- disposed + 1 
                                }
                                |> ValueTask 
                        }
                    require (disposed = 0) "disposed way early"
                    do! Task.Delay(100)
                    require (disposed = 0) "disposed kinda early"
                }
            t.Wait()
            require (disposed >= 1) "never disposed B"
            require (disposed <= 1) "too many dispose on B"

    [<Fact>]
    member __.testUsingAsyncDisposableExnAsync() =
        printfn "Running testUsingAsyncDisposableExnAsync..."
        for i in 1 .. 5 do 
            let mutable disposed = 0
            let t =
                task {
                    use d = 
                        { new IAsyncDisposable with 
                            member __.DisposeAsync() = 
                                task { 
                                    do! Task.Delay(10)
                                    disposed <- disposed + 1 
                                }
                                |> ValueTask 
                        }
                    require (disposed = 0) "disposed way early"
                    failtest "oops"
                    
                }
            try t.Wait()
            with | :? AggregateException -> 
                require (disposed >= 1) "never disposed B"
                require (disposed <= 1) "too many dispose on B"

    [<Fact>]
    member __.testUsingAsyncDisposableExnSync() =
        printfn "Running testUsingAsyncDisposableExnSync..."
        for i in 1 .. 5 do 
            let mutable disposed = 0
            let t =
                task {
                    use d = 
                        { new IAsyncDisposable with 
                            member __.DisposeAsync() = 
                                task { 
                                    disposed <- disposed + 1 
                                    do! Task.Delay(10)
                                }
                                |> ValueTask 
                        }
                    require (disposed = 0) "disposed way early"
                    failtest "oops"
                    
                }
            try t.Wait()
            with | :? AggregateException -> 
                require (disposed >= 1) "never disposed B"
                require (disposed <= 1) "too many dispose on B"

    [<Fact>]
    member __.testUsingAsyncDisposableDelayExnSync() =
        printfn "Running testUsingAsyncDisposableDelayExnSync..."
        for i in 1 .. 5 do 
            let mutable disposed = 0
            let t =
                task {
                    use d = 
                        { new IAsyncDisposable with 
                            member __.DisposeAsync() = 
                                task { 
                                    disposed <- disposed + 1 
                                    do! Task.Delay(10)
                                }
                                |> ValueTask 
                        }
                    require (disposed = 0) "disposed way early"
                    do! Task.Delay(10)
                    require (disposed = 0) "disposed kind of early"
                    failtest "oops"
                    
                }
            try t.Wait()
            with | :? AggregateException -> 
                require (disposed >= 1) "never disposed B"
                require (disposed <= 1) "too many dispose on B"

    [<Fact>]
    // Test use! resolves
    member __.testUsingBindAsyncDisposableSync() =
        printfn "Running testUsingBindAsyncDisposableSync..."
        for i in 1 .. 5 do 
            let mutable disposed = 0
            let t =
                task {
                    use! d = 
                        task {
                         do! Task.Delay(10)
                         return
                             { new IAsyncDisposable with 
                                member __.DisposeAsync() = 
                                    task { 
                                       System.Console.WriteLine "incrementing"
                                       disposed <- disposed + 1 }
                                    |> ValueTask 
                             }
                        }
                    require (disposed = 0) "disposed way early"
                    System.Console.WriteLine "delaying"
                    do! Task.Delay(100)
                    System.Console.WriteLine "testing"
                    require (disposed = 0) "disposed kinda early"
                }
            t.Wait()
            require (disposed >= 1) "never disposed B"
            require (disposed <= 1) "too many dispose on B"

    [<Fact>]
    member __.testUsingAsyncDisposableSyncSupportingBothDisposables() =
        printfn "Running testUsingAsyncDisposableSyncSupportingBothDisposables..."
        for i in 1 .. 5 do 
            let disp = new SupportBothDisposables()
            let t =
                task {
                    use d = disp
                    require (not disp.Disposed) "disposed way early"
                    System.Console.WriteLine "delaying"
                    do! Task.Delay(100)
                    System.Console.WriteLine "testing"
                    require (not disp.Disposed) "disposed kinda early"
                }
            t.Wait()
            require disp.Disposed "never disposed B"
#endif

    [<Fact>]
    member __.testUsingFromTask() =
        printfn "Running testUsingFromTask..."
        let mutable disposedInner = false
        let mutable disposed = false
        let t =
            task {
                use! d =
                    task {
                        do! Task.Delay(50)
                        use i = { new IDisposable with member __.Dispose() = disposedInner <- true }
                        require (not disposed && not disposedInner) "disposed inner early"
                        return { new IDisposable with member __.Dispose() = disposed <- true }
                    }
                require disposedInner "did not dispose inner after task completion"
                require (not disposed) "disposed way early"
                do! Task.Delay(50)
                require (not disposed) "disposed kinda early"
            }
        t.Wait()
        require disposed "never disposed C"

    [<Fact>]
    member __.testUsingSadPath() =
        printfn "Running testUsingSadPath..."
        let mutable disposedInner = false
        let mutable disposed = false
        let t =
            task {
                try
                    use! d =
                        task {
                            do! Task.Delay(50)
                            use i = { new IDisposable with member __.Dispose() = disposedInner <- true }
                            failtest "uhoh"
                            require (not disposed && not disposedInner) "disposed inner early"
                            return { new IDisposable with member __.Dispose() = disposed <- true }
                        }
                    ()
                with
                | TestException msg ->
                    printfn "caught TestException"
                    require disposedInner "did not dispose inner after task completion"
                    require (not disposed) "disposed way early"
                    do! Task.Delay(50)
                    printfn "resumed after delay"
                    require (not disposed) "disposed kinda early"
            }
        t.Wait()
        require (not disposed) "disposed thing that never should've existed"

    [<Fact>]
    member __.testForLoopA() =
        printfn "Running testForLoopA..."
        let list = ["a"; "b"; "c"] |> Seq.ofList
        let t =
            task {
                printfn "entering loop..." 
                let mutable x = Unchecked.defaultof<_>
                let e = list.GetEnumerator()
                while e.MoveNext() do 
                    x <- e.Current
                    printfn "x = %A" x 
                    do! Task.Yield()
                    printfn "x = %A" x 
            }
        t.Wait()

    [<Fact>]
    member __.testForLoopComplex() =
        printfn "Running testForLoopComplex..."
        let mutable disposed = false
        let wrapList =
            let raw = ["a"; "b"; "c"] |> Seq.ofList
            let getEnumerator() =
                let raw = raw.GetEnumerator()
                { new IEnumerator<string> with
                    member __.MoveNext() =
                        require (not disposed) "moved next after disposal"
                        raw.MoveNext()
                    member __.Current =
                        require (not disposed) "accessed current after disposal"
                        raw.Current
                    member __.Current =
                        require (not disposed) "accessed current (boxed) after disposal"
                        box raw.Current
                    member __.Dispose() =
                        require (not disposed) "disposed twice"
                        disposed <- true
                        raw.Dispose()
                    member __.Reset() =
                        require (not disposed) "reset after disposal"
                        raw.Reset()
                }
            { new IEnumerable<string> with
                member __.GetEnumerator() : IEnumerator<string> = getEnumerator()
                member __.GetEnumerator() : IEnumerator = upcast getEnumerator()
            }
        let t =
            task {
                let mutable index = 0
                do! Task.Yield()
                printfn "entering loop..." 
                for x in wrapList do
                    printfn "x = %A, index = %d" x index
                    do! Task.Yield()
                    printfn "back from yield" 
                    do! Task.Yield()
                    printfn "back from yield" 
                    match index with
                    | 0 -> require (x = "a") "wrong first value"
                    | 1 -> require (x = "b") "wrong second value"
                    | 2 -> require (x = "c") "wrong third value"
                    | _ -> require false "iterated too far!"
                    index <- index + 1
                    printfn "yield again" 
                    do! Task.Yield()
                    printfn "yield again again" 
                    do! Task.Yield()
                    printfn "looping again..." 
                do! Task.Yield()
                return 1
            }
        t.Wait()
        require disposed "never disposed D"
        require (t.Result = 1) "wrong result"

    [<Fact>]
    member __.testForLoopSadPath() =
        printfn "Running testForLoopSadPath..."
        for i in 1 .. 5 do 
            let wrapList = ["a"; "b"; "c"]
            let t =
                task {
                        let mutable index = 0
                        do! Task.Yield()
                        for x in wrapList do
                            do! Task.Yield()
                            index <- index + 1
                        return 1
                }
            require (t.Result = 1) "wrong result"

    [<Fact>]
    member __.testForLoopSadPathComplex() =
        printfn "Running testForLoopSadPathComplex..."
        for i in 1 .. 5 do 
            let mutable disposed = false
            let wrapList =
                let raw = ["a"; "b"; "c"] |> Seq.ofList
                let getEnumerator() =
                    let raw = raw.GetEnumerator()
                    { new IEnumerator<string> with
                        member __.MoveNext() =
                            require (not disposed) "moved next after disposal"
                            raw.MoveNext()
                        member __.Current =
                            require (not disposed) "accessed current after disposal"
                            raw.Current
                        member __.Current =
                            require (not disposed) "accessed current (boxed) after disposal"
                            box raw.Current
                        member __.Dispose() =
                            require (not disposed) "disposed twice"
                            disposed <- true
                            raw.Dispose()
                        member __.Reset() =
                            require (not disposed) "reset after disposal"
                            raw.Reset()
                    }
                { new IEnumerable<string> with
                    member __.GetEnumerator() : IEnumerator<string> = getEnumerator()
                    member __.GetEnumerator() : IEnumerator = upcast getEnumerator()
                }
            let mutable caught = false
            let t =
                task {
                    try
                        let mutable index = 0
                        do! Task.Yield()
                        for x in wrapList do
                            do! Task.Yield()
                            match index with
                            | 0 -> require (x = "a") "wrong first value"
                            | _ -> failtest "uhoh"
                            index <- index + 1
                            do! Task.Yield()
                        do! Task.Yield()
                        return 1
                    with
                    | TestException "uhoh" ->
                        caught <- true
                        return 2
                }
            require (t.Result = 2) "wrong result"
            require caught "didn't catch exception"
            require disposed "never disposed A"
    
    [<Fact>]
    member __.testExceptionAttachedToTaskWithoutAwait() =
        for i in 1 .. 5 do 
            let mutable ranA = false
            let mutable ranB = false
            let t =
                task {
                    ranA <- true
                    failtest "uhoh"
                    ranB <- true
                }
            require ranA "didn't run immediately"
            require (not ranB) "ran past exception"
            require (not (isNull t.Exception)) "didn't capture exception"
            require (t.Exception.InnerExceptions.Count = 1) "captured more exceptions"
            require (t.Exception.InnerException = TestException "uhoh") "wrong exception"
            let mutable caught = false
            let mutable ranCatcher = false
            let catcher =
                task {
                    try
                        ranCatcher <- true
                        let! result = t
                        return false
                    with
                    | TestException "uhoh" ->
                        caught <- true
                        return true
                }
            require ranCatcher "didn't run"
            require catcher.Result "didn't catch"
            require caught "didn't catch"

    [<Fact>]
    member __.testExceptionAttachedToTaskWithAwait() =
        printfn "running testExceptionAttachedToTaskWithAwait"
        for i in 1 .. 5 do 
            let mutable ranA = false
            let mutable ranB = false
            let t =
                task {
                    ranA <- true
                    failtest "uhoh"
                    do! Task.Delay(100)
                    ranB <- true
                }
            require ranA "didn't run immediately"
            require (not ranB) "ran past exception"
            require (not (isNull t.Exception)) "didn't capture exception"
            require (t.Exception.InnerExceptions.Count = 1) "captured more exceptions"
            require (t.Exception.InnerException = TestException "uhoh") "wrong exception"
            let mutable caught = false
            let mutable ranCatcher = false
            let catcher =
                task {
                    try
                        ranCatcher <- true
                        let! result = t
                        return false
                    with
                    | TestException "uhoh" ->
                        caught <- true
                        return true
                }
            require ranCatcher "didn't run"
            require catcher.Result "didn't catch"
            require caught "didn't catch"
    
    [<Fact>]
    member __.testExceptionThrownInFinally() =
        printfn "running testExceptionThrownInFinally"
        for i in 1 .. 5 do 
            let mutable ranInitial = false
            let mutable ranNext = false
            let mutable ranFinally = 0
            let t =
                task {
                    try
                        ranInitial <- true
                        do! Task.Yield()
                        Thread.Sleep(100) // shouldn't be blocking so we should get through to requires before this finishes
                        ranNext <- true
                    finally
                        ranFinally <- ranFinally + 1
                        failtest "finally exn!"
                }
            require ranInitial "didn't run initial"
            require (not ranNext) "ran next too early"
            try
                t.Wait()
                require false "shouldn't get here"
            with
            | _ -> ()
            require ranNext "didn't run next"
            require (ranFinally = 1) "didn't run finally exactly once"

    [<Fact>]
    member __.test2ndExceptionThrownInFinally() =
        printfn "running test2ndExceptionThrownInFinally"
        for i in 1 .. 5 do 
            let mutable ranInitial = false
            let mutable ranNext = false
            let mutable ranFinally = 0
            let t =
                task {
                    try
                        ranInitial <- true
                        do! Task.Yield()
                        Thread.Sleep(100) // shouldn't be blocking so we should get through to requires before this finishes
                        ranNext <- true
                        failtest "uhoh"
                    finally
                        ranFinally <- ranFinally + 1
                        failtest "2nd exn!"
                }
            require ranInitial "didn't run initial"
            require (not ranNext) "ran next too early"
            try
                t.Wait()
                require false "shouldn't get here"
            with
            | _ -> ()
            require ranNext "didn't run next"
            require (ranFinally = 1) "didn't run finally exactly once"
    
    [<Fact>]
    member __.testFixedStackWhileLoop() =
        printfn "running testFixedStackWhileLoop"
        for i in 1 .. 100 do 
            let t =
                task {
                    let mutable maxDepth = Nullable()
                    let mutable i = 0
                    while i < BIG do
                        i <- i + 1
                        do! Task.Yield()
                        if i % 100 = 0 then
                            let stackDepth = StackTrace().FrameCount
                            if maxDepth.HasValue && stackDepth > maxDepth.Value then
                                failwith "Stack depth increased!"
                            maxDepth <- Nullable(stackDepth)
                    return i
                }
            t.Wait()
            require (t.Result = BIG) "didn't get to big number"

    [<Fact>]
    member __.testFixedStackForLoop() =
        for i in 1 .. 100 do 
            printfn "running testFixedStackForLoop"
            let mutable ran = false
            let t =
                task {
                    let mutable maxDepth = Nullable()
                    for i in Seq.init BIG id do
                        do! Task.Yield()
                        if i % 100 = 0 then
                            let stackDepth = StackTrace().FrameCount
                            if maxDepth.HasValue && stackDepth > maxDepth.Value then
                                failwith "Stack depth increased!"
                            maxDepth <- Nullable(stackDepth)
                    ran <- true
                    return ()
                }
            t.Wait()
            require ran "didn't run all"

    [<Fact>]
    member __.testTypeInference() =
        let t1 : string Task =
            task {
                return "hello"
            }
        let t2 =
            task {
                let! s = t1
                return s.Length
            }
        t2.Wait()

    [<Fact>]
    member __.testNoStackOverflowWithImmediateResult() =
        printfn "running testNoStackOverflowWithImmediateResult"
        let longLoop =
            task {
                let mutable n = 0
                while n < BIG do
                    n <- n + 1
                    return! Task.FromResult(())
            }
        longLoop.Wait()
    
    [<Fact>]
    member __.testNoStackOverflowWithYieldResult() =
        printfn "running testNoStackOverflowWithYieldResult"
        let longLoop =
            task {
                let mutable n = 0
                while n < BIG do
                    let! _ =
                        task {
                            do! Task.Yield()
                            let! _ = Task.FromResult(0)
                            n <- n + 1
                        }
                    n <- n + 1
            }
        longLoop.Wait()

    [<Fact>]
    member __.testSmallTailRecursion() =
        printfn "running testSmallTailRecursion"
        let rec loop n =
            task {
                if n < 100 then
                    do! Task.Yield()
                    let! _ = Task.FromResult(0)
                    return! loop (n + 1)
                else
                    return ()
            }
        let shortLoop =
            task {
                return! loop 0
            }
        shortLoop.Wait()
    
    [<Fact>]
    member __.testTryOverReturnFrom() =
        printfn "running testTryOverReturnFrom"
        let inner() =
            task {
                do! Task.Yield()
                failtest "inner"
                return 1
            }
        let t =
            task {
                try
                    do! Task.Yield()
                    return! inner()
                with
                | TestException "inner" -> return 2
            }
        require (t.Result = 2) "didn't catch"

    [<Fact>]
    member __.testTryFinallyOverReturnFromWithException() =
        printfn "running testTryFinallyOverReturnFromWithException"
        let inner() =
            task {
                do! Task.Yield()
                failtest "inner"
                return 1
            }
        let mutable m = 0
        let t =
            task {
                try
                    do! Task.Yield()
                    return! inner()
                finally
                    m <- 1
            }
        try
            t.Wait()
        with
        | :? AggregateException -> ()
        require (m = 1) "didn't run finally"
    
    [<Fact>]
    member __.testTryFinallyOverReturnFromWithoutException() =
        printfn "running testTryFinallyOverReturnFromWithoutException"
        let inner() =
            task {
                do! Task.Yield()
                return 1
            }
        let mutable m = 0
        let t =
            task {
                try
                    do! Task.Yield()
                    return! inner()
                finally
                    m <- 1
            }
        try
            t.Wait()
        with
        | :? AggregateException -> ()
        require (m = 1) "didn't run finally"

    // no need to call this, we just want to check that it compiles w/o warnings
    member __.testTrivialReturnCompiles (x : 'a) : 'a Task =
        task {
            do! Task.Yield()
            return x
        }

    // no need to call this, we just want to check that it compiles w/o warnings
    member __.testTrivialTransformedReturnCompiles (x : 'a) (f : 'a -> 'b) : 'b Task =
        task {
            do! Task.Yield()
            return f x
        }

    [<Fact>]
    member __.testAsyncsMixedWithTasks() =
        let t =
            task {
                do! Task.Delay(1)
                do! Async.Sleep(1)
                let! x =
                    async {
                        do! Async.Sleep(1)
                        return 5
                    }
                return! async { return x + 3 }
            }
        let result = t.Result
        require (result = 8) "something weird happened"

    [<Fact>]
    // no need to call this, we just want to check that it compiles w/o warnings
    member __.testDefaultInferenceForReturnFrom() =
        let t = task { return Some "x" }
        task {
            let! r = t
            if r = None then
                return! failwithf "Could not find x" 
            else
                return r
        }
        |> ignore

    [<Fact>]
    // no need to call this, just check that it compiles
    member __.testCompilerInfersArgumentOfReturnFrom() =
        task {
            if true then return 1
            else return! failwith ""
        }
        |> ignore

[<CollectionDefinition("BasicsNotInParallel", DisableParallelization = true)>]
type BasicsNotInParallel() = 

    [<Fact; >]
    member __.testTaskUsesSyncContext() =
        printfn "Running testBackgroundTask..."
        for i in 1 .. 5 do 
            let mutable ran = false
            let mutable posted = false
            let oldSyncContext = SynchronizationContext.Current
            let syncContext = { new SynchronizationContext()  with member _.Post(d,state) = posted <- true; d.Invoke(state) }
            try 
                SynchronizationContext.SetSynchronizationContext syncContext
                let tid = System.Threading.Thread.CurrentThread.ManagedThreadId 
                require (not (isNull SynchronizationContext.Current)) "need sync context non null on foreground thread A"
                require (SynchronizationContext.Current = syncContext) "need sync context known on foreground thread A"
                let t =
                    task {
                        let tid2 = System.Threading.Thread.CurrentThread.ManagedThreadId 
                        require (not (isNull SynchronizationContext.Current)) "need sync context non null on foreground thread B"
                        require (SynchronizationContext.Current = syncContext) "need sync context known on foreground thread B"
                        require (tid = tid2) "expected synchronous start for task B2"
                        do! Task.Yield()
                        require (not (isNull SynchronizationContext.Current)) "need sync context non null on foreground thread C"
                        require (SynchronizationContext.Current = syncContext) "need sync context known on foreground thread C"
                        ran <- true
                    }
                t.Wait()
                require ran "never ran"
                require posted "never posted"
            finally
                SynchronizationContext.SetSynchronizationContext oldSyncContext
                 
    [<Fact; >]
    member __.testBackgroundTaskEscapesSyncContext() =
        printfn "Running testBackgroundTask..."
        for i in 1 .. 5 do 
            let mutable ran = false
            let mutable posted = false
            let oldSyncContext = SynchronizationContext.Current
            let syncContext = { new SynchronizationContext()  with member _.Post(d,state) = posted <- true; d.Invoke(state) }
            try 
                SynchronizationContext.SetSynchronizationContext syncContext
                let t =
                    backgroundTask {
                        require (System.Threading.Thread.CurrentThread.IsThreadPoolThread) "expect to be on background thread"
                        ran <- true
                    }
                t.Wait()
                require ran "never ran"
                require (not posted) "did not expect post to sync context"
            finally
                SynchronizationContext.SetSynchronizationContext oldSyncContext
                 
    [<Fact; >]
    member __.testBackgroundTaskStaysOnSameThreadIfAlreadyOnBackground() =
        printfn "Running testBackgroundTask..."
        for i in 1 .. 5 do 
            let mutable ran = false
            let taskOuter =
                Task.Run(fun () ->
                    let tid = System.Threading.Thread.CurrentThread.ManagedThreadId 
                    // In case other thread pool activities have polluted this one, sigh
                    SynchronizationContext.SetSynchronizationContext null
                    require (System.Threading.Thread.CurrentThread.IsThreadPoolThread) "expected thread pool thread (1)"
                    let t =
                        backgroundTask {
                            require (System.Threading.Thread.CurrentThread.IsThreadPoolThread) "expected thread pool thread (2)"
                            let tid2 = System.Threading.Thread.CurrentThread.ManagedThreadId 
                            require (tid = tid2) "expected synchronous starts when already on background thread"
                            do! Task.Delay(200)
                            ran <- true
                        }
                    t.Wait()
                    require ran "never ran")
            taskOuter.Wait()
                 

#if STANDALONE 
module M = 
  [<EntryPoint>]
  let main argv =
    printfn "Running tests..."
    try
        Basics().testShortCircuitResult()
        Basics().testDelay()
        Basics().testNoDelay()
        Basics().testNonBlocking()
        
        Basics().testCatching1()
        Basics().testCatching2()
        Basics().testNestedCatching()
        Basics().testWhileLoopSync()
        Basics().testWhileLoopAsyncZeroIteration()
        Basics().testWhileLoopAsyncOneIteration()
        Basics().testWhileLoopAsync()
        Basics().testTryFinallyHappyPath()
        Basics().testTryFinallySadPath()
        Basics().testTryFinallyCaught()
        Basics().testUsing()
        Basics().testUsingFromTask()
        Basics().testUsingSadPath()
        Basics().testForLoopA()
        Basics().testForLoopSadPath()
        Basics().testForLoopSadPathComplex()
        Basics().testExceptionAttachedToTaskWithoutAwait()
        Basics().testExceptionAttachedToTaskWithAwait()
        Basics().testExceptionThrownInFinally()
        Basics().test2ndExceptionThrownInFinally()
        Basics().testFixedStackWhileLoop()
        Basics().testFixedStackForLoop()
        Basics().testTypeInference()
        Basics().testNoStackOverflowWithImmediateResult()
        Basics().testNoStackOverflowWithYieldResult()
        ////// we don't support TCO, so large tail recursions will stack overflow
        ////// or at least use O(n) heap. but small ones should at least function OK.
        //testSmallTailRecursion()
        Basics().testTryOverReturnFrom()
        Basics().testTryFinallyOverReturnFromWithException()
        Basics().testTryFinallyOverReturnFromWithoutException()
        Basics().testAsyncsMixedWithTasks()
        printfn "Passed all tests!"
    with exn ->
        eprintfn "************************************"
        eprintfn "Exception: %O" exn
        printfn "Test failed... exiting..."
        eprintfn "************************************"
        exit 1
        
    printfn "Tests passed ok..., sleeping a bit in case there are background delayed exceptions"
    Thread.Sleep(500)
    printfn "Exiting..."
    //System.Console.ReadLine()
    0
#endif
