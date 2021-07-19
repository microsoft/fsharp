// #Regression #Conformance #Sequences 
#if TESTS_AS_APP
module Core_seq
#endif

#nowarn "62"
#nowarn "44"

let failures = ref []

let reportFailure (s : string) = 
    stderr.Write" NO: "
    stderr.WriteLine s
    failures := !failures @ [s]


(* TEST SUITE FOR STANDARD LIBRARY *)

let check s e r = 
  if r = e then  stdout.WriteLine (s^": YES") 
  else (stdout.WriteLine ("\n***** "^s^": FAIL\n"); reportFailure s)

let test s b = 
  if b then ( (* stdout.WriteLine ("passed: " + s) *) ) 
  else (stderr.WriteLine ("failure: " + s); 
        reportFailure s)

  
check "rwfsjkla"
   (let mutable results = []
    let ys =
        seq {
            try
                try
                    failwith "foo"
                finally
                    results <- 1::results
                    failwith "bar"
            finally
                results <- 2::results
        }        
    try
        for _ in ys do ()
    with
        Failure "bar" -> results <- 3::results
    results)
    [3;2;1]
    
check "fgyeyrkerkl"
   (let mutable results = []
    let xs = 
        seq {
            try
                try
                    failwith "foo"
                finally
                    results <- "a"::results
                    failwith "bar"
            finally
              results <- "c"::results
              failwith "bar1"
        }

    let ys =
        seq {
            yield 1
            yield! xs
        }
    try
        for _ in ys do ()
    with
        Failure "bar1" -> results <- "with"::results
    results)
    ["with";"c";"a"]


check "rwfsfsdgba"
   (let results = ref []
    let ys =
        seq {
            try
                try
                    do ()
                finally
                    results := 1::!results
                    failwith "bar"
            finally
                results := 2::!results
                failwith "bar2"
        }        
    try
        for _ in ys do ()
    with
        Failure "bar2" -> results := 3::!results
    !results)
    [3;2;1]

check "fgwehyr1"
   (let mutable results = []
    let mutable outerFinallyCalled = false
    let mutable innerFinallyCalled = false
    let ys =
       seq {
          try 
             try
                yield 1
                failwith "kaboom"
                yield 2
             finally
                innerFinallyCalled <- true
          finally
             outerFinallyCalled <- true
       }
    // Capturing precisely when what happens
    let yIter = ys.GetEnumerator()
    yIter.MoveNext() |> ignore
    try
        yIter.MoveNext() |> ignore
    with
        Failure "kaboom" ->
            results <- "kaboom"::results
            
    match innerFinallyCalled, outerFinallyCalled with
    |   false,false -> 
            results <- "beforeFinallyOk"::results
    |   _ -> ()            
    yIter.Dispose()
    match innerFinallyCalled, outerFinallyCalled with
    |   true,true -> 
            results <- "afterFinallyOk"::results
    |   _ -> ()
    results)
    ["afterFinallyOk";"beforeFinallyOk";"kaboom"]

check "fgwehyr2"
   (let results = ref []
    let outerFinallyCalled = ref false
    let innerFinallyCalled = ref false
    let ys =
       seq {
          try 
             try
                yield 1
                yield 2
             finally
                innerFinallyCalled := true
          finally
             outerFinallyCalled := true
       }
    // Capturing precisely when what happens
    let yIter = ys.GetEnumerator()
    yIter.MoveNext() |> ignore
    yIter.MoveNext() |> ignore
    yIter.MoveNext() |> ignore            
    match !innerFinallyCalled, !outerFinallyCalled with
    |   true,true -> 
            results := "finallyOk"::!results
    |   _ -> ()            
    innerFinallyCalled := false
    outerFinallyCalled := false
    yIter.Dispose()
    match !innerFinallyCalled, !outerFinallyCalled with
    |   false,false -> 
            results := "disposeOk"::!results
    |   _ -> ()            
    
    !results)
    ["disposeOk";"finallyOk"]

check "fgwehyr3"
   (let results = ref []
    let outerFinallyCalled = ref false
    let innerFinallyCalled = ref false
    let ys =
       seq {
          try 
             try
                yield 1
                yield 2
             finally
                innerFinallyCalled := true
                failwith "Kaboom"
          finally
             outerFinallyCalled := true
       }
    // Capturing precisely when what happens
    let yIter = ys.GetEnumerator()
    yIter.MoveNext() |> ignore
    yIter.MoveNext() |> ignore
    try
        yIter.MoveNext() |> ignore            
    with
        Failure "Kaboom" ->
            match !innerFinallyCalled, !outerFinallyCalled with
            |   true,false -> 
                    results := "innerFinally@lastMoveNext"::!results
            |   _ -> ()            
    innerFinallyCalled := false
    outerFinallyCalled := false
    yIter.Dispose()        
    match !innerFinallyCalled, !outerFinallyCalled with
    |   false,true -> 
            results := "outerFinally@Dispose"::!results
    |   _ -> ()            
    
    !results)
    ["outerFinally@Dispose";"innerFinally@lastMoveNext"]

check "fgryerwfre"
   (let results = ref []
    let outerFinallyCalled = ref false
    let innerFinallyCalled = ref false
    let ys i =
        seq {
            match i with
            |   1 ->
                    try
                        try 
                            failwith "foo"
                        finally
                            innerFinallyCalled := true
                    finally
                        outerFinallyCalled := true
            |   _ ->
                    do ()
        }
    try
        for _ in ys 1 do ()
    with
        Failure "foo" -> 
            match !innerFinallyCalled, !outerFinallyCalled with
            |   true, true -> results := "ok1"::!results
            |   _ -> ()

    innerFinallyCalled := false
    outerFinallyCalled := false
    for _ in ys 2 do ()
    match !innerFinallyCalled, !outerFinallyCalled with
    |   false, false -> results := "ok2"::!results
    |   _ -> ()
    !results)
    ["ok2";"ok1"]          
                        
check "rt6we56qera"
   (let results = ref []
    let innerFinallyCalled = ref false
    let middleFinallyCalled = ref false
    let outerFinallyCalled = ref false
    let ys =
        seq {
            try
                try
                    try
                        yield 0
                        failwith "inner"
                        yield 1
                    finally
                        innerFinallyCalled := true
                        failwith "middle"
                    yield 2
                finally
                    middleFinallyCalled := true
                    failwith "outer"
                yield 3
            finally 
                outerFinallyCalled := true
                failwith "outermost"
        }
    let l = ref []
    try
        for y in ys do l := y::!l
    with
        Failure "outermost" ->
            results := "expected failure"::!results
    match !l,!innerFinallyCalled,!middleFinallyCalled,!outerFinallyCalled with
    |   [0],true,true,true -> results := "ok"::!results
    |   _ -> ()
    !results)
    ["ok";"expected failure"]  
    
check "dg676rd44t"
   (let results = ref []    
    let f1 = ref false
    let f2 = ref false 
    let ys xs =
        seq {
            try
            match xs with
            | 1,h
            | h,1 -> 
                try
                    yield h
                    failwith "1"
                finally
                    f1 := true
                    failwith "2"
            | _ -> ()
            finally
                f2 := true
        }
    try
        for _ in ys (1,1) do ()
    with
        Failure "2" -> 
            match !f1,!f2 with
            |   true,true -> results := "ok"::!results
            |   _ -> ()
    !results)
    ["ok"]
type t =
|   A of int
|   B of int
|   C of string

check "rt7we6djksagd"
   (let results = ref []
    let ys x = 
       seq {
         try
           try
              match x with
              | A y | B y -> 
                     yield y
                     failwith "1"
              | _ -> 
                     yield 27
                     failwith "2"
           finally
              results := "A"::!results
              failwith "3"
         finally
           results := "B"::!results
       }

    try
        for _ in ys (A 0) do ()
    with
        Failure "3" -> results := "catch"::!results
    !results)
    ["catch";"B";"A"]

check "f6er76r23784"
   (let results = ref []
    let ys xs =
       seq {
            match xs with
            | 1,h
            | h,1 ->
                try 
                  try
                    yield h
                    failwith "1"
                  finally
                    results := "inner"::!results
                    failwith "2"
                finally
                  results := "outer"::!results
                  failwith "3"
            | _ -> ()
       }

    try
        for _ in ys (1,1) do ()
    with
        Failure "3" -> results := "catch"::!results
    !results)
    ["catch";"outer";"inner"]

check "hdweuiyrfiwe"
   (let results = ref []
    let ys xs =
        seq {
            match xs with
            | 1,h
            | h,1 ->
                use a = { new System.IDisposable with member this.Dispose() = results :=  "A"::!results }
                use b = { new System.IDisposable with member this.Dispose() = results :=  "B"::!results }
                while true do
                        failwith "boom"
            | _ -> ()
        }
    try
        for _ in ys (1,1) do ()
    with
        Failure "boom" -> results := "boom"::!results
    !results)
    ["boom";"A";"B"]

check "hdweuiyrfiwe1"
   (let results = ref []
    let ys xs =
        seq {
            use a = { new System.IDisposable with member this.Dispose() = results :=  "A"::!results }
            use b = { new System.IDisposable with member this.Dispose() = results :=  "B"::!results }
            while true do
                    failwith "boom"
        }
    try
        for _ in ys (1,1) do ()
    with
        Failure "boom" -> results := "boom"::!results
    !results)
    ["boom";"A";"B"]

check "fhduweyf"
   (let s : seq<int> = 
        seq { 
            for i in 0..3 do
                failwith "74"
                ()
            }
    let e = s.GetEnumerator()

    e.Dispose()
    try
        if e.MoveNext() then "fail" else "ok"
    with
        _ -> "exn")
    "ok"


check "fderuy" 
   (let f1 = ref false
    let f2 = ref false
    let s f s =
        seq {
            try                
                yield s
            finally
                f := true
                failwith ("foo" + s)
        }
    let result =
        try
            for _ in Seq.map2 (fun x y -> ()) (s f1 "1") (s f2 "2") do ()
            "exception didnt propagate"
        with
            _ -> 
                match !f1,!f2 with
                |   true,true -> "ok"
                |   _ -> "not all finallies run"
    result)
    "ok"
                
check "hfhdfsjkfur34"
   (let results = ref []
    let e =
        let enum () = 
            {   new System.Collections.Generic.IEnumerator<int> with
                    member this.Current = invalidOp "current"
                interface System.IDisposable with
                    member this.Dispose() =
                        results := "eDispose"::!results
                        failwith "e!!!"
                interface System.Collections.IEnumerator with
                    member this.Current = invalidOp "current"
                    member this.MoveNext() = false
                    member this.Reset() = invalidOp "reset"
            }

        {   new System.Collections.Generic.IEnumerable<int> with
                member this.GetEnumerator() = enum ()
            interface System.Collections.IEnumerable with
                member this.GetEnumerator() = enum () :> System.Collections.IEnumerator
        }
    let ss = 
        seq {
            try
                yield! e
            finally
                results := "ssDispose"::!results
                failwith "ss!!!"
        }
    try
        for _ in ss do ()
    with
        Failure "ss!!!" -> results := "caught"::!results
    !results)
    ["caught";"ssDispose";"eDispose"]

// Check https://github.com/Microsoft/visualfsharp/pull/742

module Repro1 = 

    let configure () =
     let aSequence = seq { yield "" } 
     let aString = new string('a',3)
     for _ in aSequence do
       System.Console.WriteLine(aString)

    do configure ()
    /// The check is that the above code compiles OK

module Repro2 = 

    let configure () =
     let aSequence = Microsoft.FSharp.Core.Operators.(..) 3 4
     let aString = new string('a',3)
     for _ in aSequence do
       System.Console.WriteLine(aString)

    do configure ()
    /// The check is that the above code compiles OK

module InfiniteSequenceExpressionsExecuteWithFiniteResources = 
    let rec seqOneNonRecUnusedNonCapturing r = seq {
        if r > 0 then
            let recfun() = 1
            yield r
            yield! seqOneNonRecUnusedNonCapturing r
    }

    let rec seqOneNonRecNonCapturing r = seq {
        if r > 0 then
            let recfun x = if x > 0 then x else 2
            yield (recfun 3)
            yield! seqOneNonRecNonCapturing r
    }

    let rec seqOneNonRecCapturingOne r = seq {
        if r > 0 then
            let recfun x = if x > 0 then r else (x-1)
            yield (recfun 3)
            yield! seqOneNonRecCapturingOne r
    }
    let rec seqOneNonRecCapturingTwo r q = seq {
        if r > 0 && q > 0 then
            let recfun x = if x > 0 then (r,q) else (x-1, x-2)
            yield (recfun 3)
            yield! seqOneNonRecCapturingTwo r q
    }

    let rec seqOneRecUnusedNonCapturing r = seq {
        if r > 0 then
            let rec recfun() = recfun()
            yield r
            yield! seqOneRecUnusedNonCapturing r
    }

    let rec seqOneRecNonCapturing r = seq {
        if r > 0 then
            let rec recfun x = if x > 0 then x else recfun (x-1)
            yield (recfun 3)
            yield! seqOneRecNonCapturing r
    }

    let rec seqOneRecCapturingOne r = seq {
        if r > 0 then
            let rec recfun x = if x > 0 then r else recfun (x-1)
            yield (recfun 3)
            yield! seqOneRecCapturingOne r
    }
    let rec seqOneRecCapturingTwo r q = seq {
        if r > 0 && q > 0 then
            let rec recfun x = if x > 0 then (r,q) else recfun (x-1)
            yield (recfun 3)
            yield! seqOneRecCapturingTwo r q
    }
    let rec seqTwoRecCapturingOne r = seq {
        if r > 0 then
            let rec recfun x = if x > 0 then r else recfun2 (x-1)
            and recfun2 x = if x > 0 then r else recfun (x-1)
            yield (recfun 3)
            yield! seqTwoRecCapturingOne r
    }
    let rec seqThreeRecCapturingOne r = seq {
        if r > 0 then
            let rec recfun x = if x > 0 then r else recfun2 (x-1)
            and recfun2 x = if x > 0 then r else recfun3 (x-1)
            and recfun3 x = if x > 0 then r else recfun (x-1)
            yield (recfun 3)
            yield! seqThreeRecCapturingOne r
    }

    //
    // These tests will stackoverflow or out-of-memory if the above functions are not compiled to "sequence epression tailcalls",
    // i.e. by compiling them to a state machine
    let tests() = 
        printfn "starting seqOneUnusedNonCapturing"
        check "celkecwecmkl" (Seq.item 10000000 (seqOneNonRecUnusedNonCapturing 1)) 1

        printfn "starting seqOneRecNonCapturing"
        check "celkecwecmkl2" (Seq.item 10000000 (seqOneNonRecNonCapturing 2)) 3

        printfn "starting seqOneRecCapturingOne"
        check "celkecwecmkl3" (Seq.item 10000000 (seqOneNonRecCapturingOne 2)) 2

        printfn "starting seqOneRecCapturingTwo"
        check "celkecwecmkl4" (Seq.item 10000000 (seqOneNonRecCapturingTwo 2 2)) (2,2)


        printfn "starting seqOneUnusedNonCapturing"
        check "celkecwecmkl" (Seq.item 10000000 (seqOneRecUnusedNonCapturing 1)) 1

        printfn "starting seqOneRecNonCapturing"
        check "celkecwecmkl2" (Seq.item 10000000 (seqOneRecNonCapturing 2)) 3

        printfn "starting seqOneRecCapturingOne"
        check "celkecwecmkl3" (Seq.item 10000000 (seqOneRecCapturingOne 2)) 2

        printfn "starting seqOneRecCapturingTwo"
        check "celkecwecmkl4" (Seq.item 10000000 (seqOneRecCapturingTwo 2 2)) (2,2)

        printfn "starting seqTwoRecCapturingOne"
        check "celkecwecmkl5" (Seq.item 10000000 (seqTwoRecCapturingOne 2)) 2

        printfn "starting seqThreeRecCapturingOne"
        check "celkecwecmkl6" (Seq.item 10000000 (seqThreeRecCapturingOne 2)) 2


    // Note, recursively referential memoization is not compiled to use finite resources.  If someone is using a recursive memoization table in this position
    // of an infinite sequence expression then they are going to hit massive resource problems in any case...
    (*
    let memoize f = 
          let dict = System.Collections.Generic.Dictionary()
          fun x -> if dict.ContainsKey x then dict.[x] else let res = f x in dict.[x] <- res; res

    // Capture 1 recursive memoizations
    let rec seqOneRecCapturingOneWithOneMemoized r = seq {
        if r > 0 then
            let rec recfun = memoize (fun x -> if x > 0 then r else recfun (x-1))
            yield (recfun 3)
            yield! seqOneRecCapturingOneWithOneMemoized r
    }

    // Capture 1 recursive memoizations
    let rec seqTwoRecCapturingOneWithOneMemoized r = seq {
        if r > 0 then
            let rec recfun = memoize (fun x -> if x > 0 then r else recfun2 (x-1))
            and recfun2 x = if x > 0 then r else recfun (x-1)
            yield (recfun 3)
            yield! seqTwoRecCapturingOneWithOneMemoized r
    }


    // Capture 1 recursive memoizations
    let rec seqThreeRecCapturingOneWithOneMemoized r = seq {
        if r > 0 then
            let rec recfun = memoize (fun x -> if x > 0 then r else recfun2 (x-1))
            and recfun2 x = if x > 0 then r else recfun3 (x-1)
            and recfun3 x = if x > 0 then r else recfun (x-1)
            yield (recfun 3)
            yield! seqThreeRecCapturingOneWithOneMemoized r
    }

    // Capture 2 recursive memoizations
    let rec seqThreeRecCapturingOneWithTwoMemoized r = seq {
        if r > 0 then
            let rec recfun = memoize (fun x -> if x > 0 then r else recfun2 (x-1))
            and recfun2 x = if x > 0 then r else recfun3 (x-1)
            and recfun3 = memoize (fun x -> if x > 0 then r else recfun (x-1))
            yield (recfun 3)
            yield! seqThreeRecCapturingOneWithTwoMemoized r
    }

    // Capture 3 recursive memoizations
    let syncLoopThreeRecCapturingWithThreeMemoized n r = 
        let rec recfun = memoize (fun x -> if x > 0 then r else recfun2 (x-1))
        and recfun2 = memoize (fun x -> if x > 0 then r else recfun3 (x-1))
        and recfun3 = memoize (fun x -> if x > 0 then r else recfun (x-1))
        let rec loop n = 
            if n > 0 then
                recfun 3 |> ignore
                loop (n-1) 
            else 
                recfun r
        loop n
    


    let rec seqThreeRecCapturingOneWithThreeMemoized r = seq {
        if r > 0 then
            let rec recfun = memoize (fun x -> if x > 0 then r else recfun2 (x-1))
            and recfun2 = memoize (fun x -> if x > 0 then r else recfun3 (x-1))
            and recfun3 = memoize (fun x -> if x > 0 then r else recfun (x-1))
            yield (recfun 3)
            yield! seqThreeRecCapturingOneWithThreeMemoized r
    }

    printfn "starting seqOneRecCapturingOneWithOneMemoized"
    printfn "%i" (Seq.item 10000000 (seqOneRecCapturingOneWithOneMemoized 2))

    printfn "starting seqTwoRecCapturingOneWithOneMemoized"
    printfn "%i" (Seq.item 10000000 (seqTwoRecCapturingOneWithOneMemoized 2))

    printfn "starting seqThreeRecCapturingOneWithOneMemoized"
    printfn "%i" (Seq.item 10000000 (seqThreeRecCapturingOneWithOneMemoized 2))


    printfn "starting seqThreeRecCapturingOneWithTwoMemoized"
    printfn "%i" (Seq.item 10000000 (seqThreeRecCapturingOneWithTwoMemoized 2))

    printfn "starting syncLoopThreeRecCapturingWithThreeMemoized"
    printfn "%i" (syncLoopThreeRecCapturingWithThreeMemoized 10000000 2)

    printfn "starting seqThreeRecCapturingOneWithThreeMemoized"
    printfn "%i" (Seq.item 10000000 (seqThreeRecCapturingOneWithThreeMemoized 2))

    *)

// Tests disabled due to bug https://github.com/Microsoft/visualfsharp/issues/3743
//InfiniteSequenceExpressionsExecuteWithFiniteResources.tests()

    // This is the additional test case related to bug https://github.com/Microsoft/visualfsharp/issues/3743
    let TestRecFuncInSeq() = 
        let factorials =
            [ for x in 0..10 do
                let rec factorial x =
                    match x with
                    | 0 -> 1
                    | x -> x * factorial(x - 1)
                yield factorial x
            ]

        check "vlklmkkl" factorials [1;1;2;6;24;120;720;5040;40320;362880;3628800]
    TestRecFuncInSeq()

module TestCollectOnStructSeq = 
    open System

    [<Struct>]
    type S = 
        interface System.Collections.Generic.IEnumerable<int> with
            member x.GetEnumerator() = (seq { yield 1; yield 2}).GetEnumerator()
        interface System.Collections.IEnumerable with
            member x.GetEnumerator() = (seq { yield 1; yield 2} :> System.Collections.IEnumerable).GetEnumerator()

    let iterate (x: S) =
        seq { yield! Seq.collect (fun _ -> x) [1] }
 
    check "ccekecnwe" (iterate (Unchecked.defaultof<S>) |> Seq.length) 2

module CheckStateMachineCompilationOfMatchBindingVariables =
    let f xs =
        seq {
            match xs with
            | 1,h
            | h,1 ->
                 let stackTrace = new System.Diagnostics.StackTrace()
                 let methodBase = stackTrace.GetFrame(1).GetMethod()
                 System.Console.WriteLine(methodBase.Name)
                 // This checks we have a state machine.  In the combinator compilation
                 // we get 'Invoke'.
                 check "vwehoehwvo" methodBase.Name "MoveNextImpl" 
                 yield h
                 yield h
            | 2,h
            | h,2 ->
                 yield h
            | _ -> ()
        }

    check "ccekecnwevwe1" (f (1, 2) |> Seq.toList) [2;2]
    check "ccekecnwevwe2" (f (2, 1) |> Seq.toList) [2;2]
    check "ccekecnwevwe3" (f (2, 3) |> Seq.toList) [3]
    check "ccekecnwevwe4" (f (3, 2) |> Seq.toList) [3]
    check "ccekecnwevwe5" (f (3, 3) |> Seq.toList) []

(*---------------------------------------------------------------------------
!* wrap up
 *--------------------------------------------------------------------------- *)


#if TESTS_AS_APP
let RUN() = !failures
#else
let aa =
  match !failures with 
  | [] -> 
      stdout.WriteLine "Test Passed"
      System.IO.File.WriteAllText("test.ok","ok")
      exit 0
  | _ -> 
      stdout.WriteLine "Test Failed"
      exit 1
#endif

