// Instructions: 
//    artifacts\bin\fsc\Debug\net472\fsc.exe --debug+ --tailcalls- --optimize- --langversion:preview tests\walkthroughs\DebugStepping\TheBigFileOfDebugStepping.fsx
//    devenv /debugexe TheBigFileOfDebugStepping.exe
//
//   Repeat the above with
//      - Just My Code on/off
//      - 32-bit or 64-bit
//      - .NET Core and .NET Framework
//      - Linux and Windows
//      - Different visual debuggers

open System
open System.Threading.Tasks

type U2 = U2 of int * int


let InnerRecursiveFunction (str: string) =
    let rec even n = if n = 0 then str else odd (n-1)
    and odd n = even (n-1)

    even 6

let rec TailcallRecursionTest1 n = 
    if n = 0 then 
        5
    else 
        TailcallRecursionTest1 (n-1) // check the 'n' updates correctly

let rec TailcallRecursionTest2 (U2(a,b)) = 
    if a = 0 then 
        5
    else 
        TailcallRecursionTest2 (U2(a-1,b-1)) // check the 'a' and 'b' update correctly

let AsyncExpressionSteppingTest1 () = 
    async { Console.WriteLine "hello"
            Console.WriteLine "stuck in the middle"
            Console.WriteLine "goodbye"}

let AsyncExpressionSteppingTest2 () = 
    let x = ref 0 
    async { while !x < 4 do 
                incr x
                Console.WriteLine "hello" }
                   

let AsyncExpressionSteppingTest3 () = 
    async { let x = ref 0 
            incr x
            let y = ref 0 
            incr y
            let z = !x + !y
            return z }

let AsyncExpressionSteppingTest4 () = 
    async { let x = ref 0 
            try 
                let y = ref 0 
                incr y
                let z = !x + !y
                return z 
            finally 
                incr x
                Console.WriteLine "done" }


let es = [3;4;5]
let AsyncExpressionSteppingTest5 () = 
    async { for x in es do
                Console.WriteLine "hello"
                Console.WriteLine "hello 2"
            for x in es do
                Console.WriteLine "goodbye"
                Console.WriteLine "goodbye 2" }


let f2 () = 
    async { let x = ref 0 
            incr x
            let y = ref 0 
            incr y
            let z = !x + !y
            return z }

let AsyncExpressionSteppingTest6b () = 
    async { let! x1 = f2()
            let! x2 = f2()
            let! x3 = f2()
            let y = ref 0 
            incr y
            let! x4 = f2()
            let z = x1 + !y + x4
            return z }



let AsyncBreakpoints1 () = 
    async { let! res1 = 
                f2()
            let! res2 = 
                match 1 with 
                | 1 -> f2()
                | _ -> f2()
            let! res3 = 
                match 1 with 
                | 1 -> 
                    let x = 
                        match 4 with 
                        | 2 -> f2()
                        | _ -> f2()
                    f2()
                | _ -> 
                    let x = 
                        match 4 with 
                        | 2 -> f2()
                        | _ -> f2()
                    f2()
            return () }


let ListExpressionSteppingTest1 () = 
    [ yield 1 ]

let ListExpressionSteppingTest2 () = 
    [ Console.WriteLine "hello"
      yield 1
      Console.WriteLine "goodbye"
      yield 2]


let ListExpressionSteppingTest3 () = 
    let x = ref 0 
    [ while !x < 4 do 
            incr x
            Console.WriteLine "hello"
            yield x ]


let ListExpressionSteppingTest4 () = 
    [ let x = ref 0 
      incr x
      let y = ref 0 
      incr y
      yield !x
      let z = !x + !y
      yield z ]


let ListExpressionSteppingTest5 () = 
    [ let x = ref 0 
      try 
            let y = ref 0 
            incr y
            yield !x
            let z = !x + !y
            yield z 
      finally 
            incr x
            Console.WriteLine "done" ]


let ListExpressionSteppingTest6 () = 
    [ for x in es do
            Console.WriteLine "hello"
            yield x 
      for x in es do
            Console.WriteLine "goodbye"
            yield x ]

let ListExpressionSteppingTest7 () = 
    [ for x in 1..4 do
            printfn "hello"
            yield x ]

let ListExpressionSteppingTest8 () = 
    [ for x in 1..4 do
        match x with 
        | 1 -> 
            printfn "hello"
            yield x 
        | 2 -> 
            printfn "hello"
            yield x 
        | _ -> 
            yield x 
        ]

let SeqExpressionSteppingTest1 () = 
    seq { yield 1 }


let SeqExpressionSteppingTest2 () = 
    seq { Console.WriteLine "hello"
          yield 1
          Console.WriteLine "goodbye"
          yield 2 }



let SeqExpressionSteppingTest3 () = 
    let x = ref 0 
    seq { while !x < 4 do 
                incr x
                Console.WriteLine "hello"
                yield x }


let SeqExpressionSteppingTest4 () = 
    seq { let x = ref 0 
          incr x
          let y = ref 0 
          incr y
          yield !x
          let z = !x + !y
          yield z }



let SeqExpressionSteppingTest5 () = 
    seq { let x = ref 0 
          try 
                let y = ref 0 
                incr y
                yield !x
                let z = !x + !y
                yield z 
          finally 
                incr x
                Console.WriteLine "done" }


let SeqExpressionSteppingTest6 () = 
    seq { for x in es do
                Console.WriteLine "hello"
                yield x 
          for x in es do
                Console.WriteLine "goodbye"
                yield x }



let SeqExpressionSteppingTest7() =
    // "Stepping into sequence expression pops up a dialog trying to located an unknown file"
    let r = ref 0
    let f () = [ if (incr r; true) then yield! failwith "" ]
    (try f () with Failure _ -> [!r])

let rec rwalk x = seq { yield x; yield! rwalk (x+1) }

let SeqExpressionTailCalls01() = rwalk 3 |> Seq.truncate 3 |> Seq.length

// Same as SeqExpressionTailCalls01.fs, but with MUTUALLY RECURSIVE PAIR OF SEQUENCES
let rec rwalk1 x = seq { yield x; yield! rwalk2 (x+1) }
and rwalk2 x = seq { yield x; yield! rwalk1 (x+1) }

let SeqExpressionTailCalls02() = rwalk2 3 |> Seq.truncate 3 |> Seq.length

let SteppingMatch01 (n) =   
        match n with                                    
        | Choice2Of2 _ ->
            Console.WriteLine("A")
        | Choice1Of2 _ ->            
            Console.WriteLine("B")
    


let SteppingMatch03 (n) =   
        match n with                                    
        | Choice1Of2 _ ->            
            Console.WriteLine("B")
        | Choice2Of2 _ ->
            Console.WriteLine("A")

let SteppingMatch03b (n) =   
        match n with                                    
        | Choice1Of3 _ ->            
            Console.WriteLine("A")
        | Choice2Of3 _ ->
            Console.WriteLine("B")
        | Choice3Of3 _ ->
            Console.WriteLine("C")


let SteppingMatch04 (n) =   
        match n with                                    
        | Choice2Of3 _ ->
            Console.WriteLine("B")
        | Choice3Of3 _ ->
            Console.WriteLine("C")
        | Choice1Of3 _ ->            
            Console.WriteLine("A")

let SteppingMatch05 (n) =   
        match n with                                    
        | Choice3Of3 _ ->
            Console.WriteLine("C")
        | Choice2Of3 _ ->
            Console.WriteLine("B")
        | Choice1Of3 _ ->            
            Console.WriteLine("A")

type Discr = CaseA | CaseB
let SteppingMatch06 (n) =
        match n with                                    
        | CaseB ->            
            Console.WriteLine("B")
        | CaseA->
            Console.WriteLine("A")

let SteppingMatch07 (n) =
        match n with                                    
        | CaseA->
            Console.WriteLine("A")
        | CaseB ->            
            Console.WriteLine("B")

let SteppingMatch08 (x) =
    let b = 
        match x with
        | 0 -> 2
        | _   -> 0

    System.Diagnostics.Debug.Write(b)
    System.Diagnostics.Debug.Write(b)

let SteppingMatch09 n =   
        match n with                                    
        | 1  ->
            Some(10)  // debug range should cover all of "Some(10)"
        | 2  ->            
            None
        | _ ->
                   Some(   22   )  // debug range should cover all of "Some(   22   )"

// Test case from https://github.com/Microsoft/visualfsharp/issues/105
let OuterWithGenericInner list =
  let GenericInner (list: 'T list) = 
     match list with 
     | [] -> 1 
     | _ -> 2

  GenericInner list

// Test case from https://github.com/Microsoft/visualfsharp/issues/105
let OuterWithNonGenericInner list =
  let NonGenericInner (list: int list) = 
     match list with 
     | [] -> 1 
     | _ -> 2

  NonGenericInner list

// Test case from https://github.com/Microsoft/visualfsharp/issues/105
let OuterWithNonGenericInnerWithCapture x list =
  let NonGenericInnerWithCapture (list: int list) = 
     match list with 
     | [] -> 1 
     | _ -> x

  NonGenericInnerWithCapture list


let TestFunction1() =
    Console.WriteLine "Hello";
    Console.WriteLine "World";
    3+4


let TestFunction10(p) =
    let (x,y) = p 
    x+y


let TestFunction11(p) =
    [p; p + p; p + p + p ]


let TestFunction12(p) =
    { contents = p+p }


let TestFunction13(x) =
    [x;x+x], [] 

              
let TestFunction14() =
    List.map (fun f -> f 2) [(fun x -> x + 1)] 

              
let TestFunction15(inp) =
    let x = inp+1
    [1;2;3] |> List.map (fun x -> x + 1)

              
type U = U of int * int

let TestFunction16(inp) =
    let x = U(inp,inp)
    x,x


type R = { x:int; y:int }

let TestFunction17(inp) =
    let x = {x=3;y=inp}
    x,x


let TestFunction18(inp) =
    System.Console.WriteLine("hello")


type C(x:int,y:int) = 
    member this.X = x
    member this.Y = y
    
let TestFunction19(inp) =
    let c1 = C(inp,inp)
    let c2 = C(inp,inp)
    Console.WriteLine $"c1 = {c1}, c2 = {c2}"

let TestFunction1_0() =
    Console.WriteLine "Hello";
    Console.WriteLine "World";
    3+4
    
let TestFunction2() =
    let x = TestFunction1_0()
    Console.WriteLine "Hello";
    Console.WriteLine "World"    
    

type D(x:int,y:int) = 
    let z = x + y
    let f a = x + a
    let w = f z + z
    member this.X = x
    member this.Y = y
    
let TestFunction20(inp) =
    let d1 = D(inp,inp)
    let d2 = D(inp,inp)
    Console.WriteLine $"done d1 = {d1}, d2 = {d2}"


let TestFunction21(U2(a,b)) =
    Console.WriteLine $"a = {a}, b = {b}"

let TestFunction1_1() =
    Console.WriteLine "Hello";
    Console.WriteLine "World";
    3+4
    
let TestFunction3() =
    try 
       let x = TestFunction1_1()
       Console.WriteLine "Hello";
    with _ -> 
       Console.WriteLine "World"        

let TestFunction1_2() =
    Console.WriteLine "Hello";
    Console.WriteLine "World";
    3+4
    
let TestFunction3b() =
    try 
       let x = TestFunction1_2()
       failwith "hello"
    with Failure _ -> 
       Console.WriteLine "World"  

let TestFunction1_3() =
    Console.WriteLine "Hello";
    Console.WriteLine "World";
    3+4
    
let TestFunction3c() =
    try 
       let x = TestFunction1_3()
       failwith "hello"
    with Failure msg when msg = "hello" -> 
       Console.WriteLine "World"        


let TestFunction1_4() =
    Console.WriteLine "Hello";
    Console.WriteLine "World";
    3+4
    
let TestFunction4() =
    try 
       let x = TestFunction1_4()
       Console.WriteLine "Hello";
    finally 
       Console.WriteLine "World"

let TestFunction1_5() =
    Console.WriteLine "Hello";
    Console.WriteLine "World";
    3+4
    
let TestFunction5() =
    let x = 
       let y = TestFunction1_5()
       Console.WriteLine "Hello";
       y + y
    x + x


let TestFunction1_6() =
    Console.WriteLine "Hello";
    Console.WriteLine "World";
    3+4
    
let TestFunction6() =
    let f() = 
       let y = TestFunction1_6()
       Console.WriteLine "Hello";
       y + y
    f() + f()


let TestFunction7() =
    let mutable r = 0
    while r < 3 do
       r <- r + 1

let TestFunction8(x) =
    if x > 3 then 
        x+4 
    else x-4


let TestFunction9(x) =
    match x with 
    | 3 -> "three"
    | 4 -> "four"
    | _ -> "five"


let TestFunction9b(x) =
    match x with 
    | [1;2] -> "three"
    | [3;4] -> "seven"
    | [a;b] when a+b = 4 -> "four"
    | _ -> "big"


let TestFunction9c(x) =
    match x with 
    | [1;2] -> "three"
    | [3;4] -> "seven"
    | [a;b] when a+b = 4 -> "four"
    | _ -> "big"
// #NoMono #NoMT #CodeGen #EmittedIL 


let TestFunction9d(x) =
    match x with 
    | [1;2] -> "three"
    | [3;4] -> "seven"
    | [a;b] when a+b = 4 -> "four"
    | _ -> "big"
// #NoMono #NoMT #CodeGen #EmittedIL 


let TestFunction9e(x) =
    match x with 
    | [1;2] -> "three"
    | [3;4] -> "seven"
    | [a;b] when a+b = 4 -> "four"
    | _ -> "big"

let TestFunction22() =
    let x1 = if DateTime.Now.Day = 0 then DateTime.Now.Day else DateTime.MinValue.Day
    let x2 = if DateTime.Now.Day = 1 then DateTime.Now.Day else DateTime.MinValue.Day
    let x3 = if DateTime.Now.Day > 1 then DateTime.Now.Day else DateTime.MinValue.Day
    (x1,x2,x3)


let InnerFunctionDefinitionHadTwoBreakpoints (str: string) =
    let isVowel (ch: char) =
        let c = Char.ToUpper(ch)
        c = 'A' || c = 'E' || c = 'I' || c = 'O' || c = 'U'

    let firstChar = str.[0]

    if isVowel firstChar then
        str + "way"
    else
        str.[1..str.Length-1] + string(firstChar) + "ay"

let InnerRecursiveFunctionDefinitionHadTwoBreakpoints (str: string) =
    let firstChar = str.[0]

    let rec isVowel (ch: char) =
        let c = Char.ToUpper(ch)
        c = 'A' || c = 'E' || c = 'I' || c = 'O' || c = 'U'

    let firstChar = str.[0]

    if isVowel firstChar then
        str + "way"
    else
        str.[1..str.Length-1] + string(firstChar) + "ay"



let LocalValueShadowsArgument1 x =
    let x = // quick watch 1
        if isNull(x) then 
            printf "value is null" // quick watch 2
            2
        else 
            printf "value is not null" // breakpoint 1
            3
    ()

let LocalValueShadowsArgument2 x =
    let x = // quick watch 3
        if isNull(x) then 
            null // quick watch 4
        else 
            null // breakpoint 2
    ()

let TaskExpressionSteppingTest0 () = 
    task  { Console.WriteLine "hello"
            Console.WriteLine "stuck in the middle"
            Console.WriteLine "goodbye"}

let TaskExpressionSteppingTest1 () = 
    task  { Console.WriteLine "hello"
            Console.WriteLine "stuck in the middle"
            do! Task.Delay 100
            Console.WriteLine "goodbye"}

let TaskExpressionSteppingTest2 () = 
    let x = ref 0 
    task  { while !x < 4 do 
                incr x
                Console.WriteLine "hello" }

let TaskExpressionSteppingTest3 () = 
    task  { let x = ref 0 
            incr x
            let y = ref 0 
            incr y
            let z = !x + !y
            return z }

let TaskExpressionSteppingTest4 () = 
    task  { let x = ref 0 
            try 
                let y = ref 0 
                incr y
                let z = !x + !y
                return z 
            finally 
                incr x
                Console.WriteLine "done" }

let TaskExpressionSteppingTest5 () = 
    task  { for x in es do
                Console.WriteLine "hello"
                Console.WriteLine "hello 2"
            for x in es do
                Console.WriteLine "goodbye"
                Console.WriteLine "goodbye 2" }

let tf2 () = 
    task  { let x = ref 0 
            incr x
            let y = ref 0 
            incr y
            let z = !x + !y
            return z }

let TaskExpressionSteppingTest6b () = 
    task  { let! x1 = tf2()
            let!    x2 = tf2()
            let! x3 = tf2()
            let y = ref 0 
            incr y
            let! x4 = tf2()
            let z = x1 + !y + x4
            return z }

let TaskExpressionSteppingTest7a () = 
    task  { let x = ref 0 
            try 
                let y = ref 0 
                incr y
                let z = !x + !y
                return z 
            with exn ->
                incr x
                Console.WriteLine "done"
                return 0 }

let TaskExpressionSteppingTest7b () = 
    task  { let x = ref 0 
            try 
                let y = ref 0 
                incr y
                failwith "fail"
                let z = !x + !y
                return z 
            with exn ->
                incr x
                Console.WriteLine "done"
                return 0 }

let TaskBreakpoints1 () = 
    task  { let! res1 = 
                tf2()
            let! res2 = 
                match 1 with 
                | 1 -> tf2()
                | _ -> tf2()
            let! res3 = 
                match 1 with 
                | 1 -> 
                    let x = 
                        match 4 with 
                        | 2 -> tf2()
                        | _ -> tf2()
                    tf2()
                | _ -> 
                    let x = 
                        match 4 with 
                        | 2 -> tf2()
                        | _ -> tf2()
                    tf2()
            return () }

module InlinedCode =
    // NOTE: you can't place breakpoints in this method and hit them in either Debug and Release code
    let inline bodyRunner z body =
        let x = 1 + z
        printfn "running"
        body x
        body x

    let test() =
        let bodyWrapper =
            let x = 1 + System.Random().Next()
            bodyRunner 3 (fun n ->
                // MANUAL TEST: check you can place breakpoint here and hit it in both Debug and Release code
                printfn "line1, x = %d" x
                // MANUAL TEST: check you can place breakpoint here and hit it in both Debug and Release code
                printfn "line2, n = %d" n)

        let bodyWrapper2 =
            // TEST: check you can place breakpoint here and hit it in both Debug and Release code
            let x = 1 + System.Random().Next()
            bodyRunner 3 <| (fun n ->
                // MANUAL TEST: check you can place breakpoint here and hit it in both Debug and Release code
                printfn "line1, x = %d" x
                // MANUAL TEST: check you can place breakpoint here and hit it in both Debug and Release code
                printfn "line2, n = %d" n)
        ()

module Pipelined =
    let testListPipeline() =
        let data = [ 1 .. 5 ]
    
        // MANUAL TEST: check stepping through this looks ok
        let newData =
            data
            |> List.filter (fun x -> 
                  // MANUAL TEST: check you can place breakpoint here and hit it in both Debug and Release code
                  x > 3)
            |> List.map (fun x -> 
                  // MANUAL TEST: check you can place breakpoint here and hit it in both Debug and Release code
                  x * x)
        
        printfn "%A" newData

    let testArrayPipeline() =
            let data = [| 1 .. 5 |]
    
            let newData =
                data
                |> Array.filter (fun x -> 
                    // MANUAL TEST: check you can place breakpoint here and hit it in both Debug and Release code
                    x > 3)
                |> Array.map (fun x -> 
                    // MANUAL TEST: check you can place breakpoint here and hit it in both Debug code
                    // TODO: surprisingly no breakpoint hit here in release code
                    x * x)
        
            printfn "%A" newData

TailcallRecursionTest1 3
TailcallRecursionTest2 (U2(3,4))
SteppingMatch01 (Choice1Of2 3)
SteppingMatch01 (Choice2Of2 3)
SteppingMatch03 (Choice1Of2 3)
SteppingMatch03 (Choice2Of2 "3")
SteppingMatch03b (Choice1Of3 3)
SteppingMatch03b (Choice2Of3 "3")
SteppingMatch03b (Choice3Of3 5.0)
SteppingMatch04 (Choice1Of3 3)
SteppingMatch04 (Choice2Of3 "3")
SteppingMatch04 (Choice3Of3 5.0)
SteppingMatch05 (Choice1Of3 3)
SteppingMatch05 (Choice2Of3 "3")
SteppingMatch05 (Choice3Of3 5.0)
SteppingMatch06 CaseA
SteppingMatch06 CaseB
SteppingMatch07 CaseA
SteppingMatch07 CaseB
SteppingMatch08 0
SteppingMatch08 1
SteppingMatch09 1
SteppingMatch09 2
SteppingMatch09 3

OuterWithGenericInner [1;2;3;4;5;6]
OuterWithNonGenericInner [1;2;3;4;5;6]
OuterWithNonGenericInnerWithCapture 5 [1;2;3;4;5;6]
TestFunction1()
TestFunction2()
TestFunction3()
TestFunction4()
TestFunction5()
TestFunction6()
TestFunction7()
TestFunction8 3
TestFunction8 4
TestFunction9 3
TestFunction9 4
TestFunction9 5
TestFunction10 (5,4)
TestFunction11 5
TestFunction12 5
TestFunction13 5
TestFunction14 ()
TestFunction15 3
TestFunction16 3
TestFunction17 3
TestFunction18 3
TestFunction19 3
TestFunction21(U2(3,4))
TestFunction22()

AsyncBreakpoints1() |> Async.RunSynchronously
AsyncExpressionSteppingTest1() |> Async.RunSynchronously
AsyncExpressionSteppingTest2() |> Async.RunSynchronously
AsyncExpressionSteppingTest3() |> Async.RunSynchronously
AsyncExpressionSteppingTest4() |> Async.RunSynchronously
AsyncExpressionSteppingTest5() |> Async.RunSynchronously
AsyncExpressionSteppingTest6b() |> Async.RunSynchronously
ListExpressionSteppingTest1()
ListExpressionSteppingTest2()
ListExpressionSteppingTest3()
ListExpressionSteppingTest4()
ListExpressionSteppingTest5()
ListExpressionSteppingTest6()
ListExpressionSteppingTest7()
ListExpressionSteppingTest8()
SeqExpressionSteppingTest1()|> Seq.length
SeqExpressionSteppingTest2()|> Seq.length
SeqExpressionSteppingTest3()|> Seq.length
SeqExpressionSteppingTest4()|> Seq.length
SeqExpressionSteppingTest5()|> Seq.length
SeqExpressionSteppingTest6() |> Seq.length
InnerRecursiveFunction "cajcek" |> ignore
InnerFunctionDefinitionHadTwoBreakpoints "aaaa" |> ignore
InnerRecursiveFunctionDefinitionHadTwoBreakpoints "aaaa" |> ignore
LocalValueShadowsArgument1 "123"
LocalValueShadowsArgument2 "123"

module Task =
    let RunSynchronously (t: System.Threading.Tasks.Task<_>) = t.Wait(); t.Result

TaskExpressionSteppingTest0() |> Task.RunSynchronously
TaskExpressionSteppingTest1() |> Task.RunSynchronously
TaskExpressionSteppingTest2() |> Task.RunSynchronously
TaskExpressionSteppingTest3() |> Task.RunSynchronously
TaskExpressionSteppingTest4() |> Task.RunSynchronously
TaskExpressionSteppingTest5() |> Task.RunSynchronously
TaskExpressionSteppingTest6b() |> Task.RunSynchronously
TaskExpressionSteppingTest7a() |> Task.RunSynchronously
TaskExpressionSteppingTest7b() |> Task.RunSynchronously
TaskBreakpoints1() |> Task.RunSynchronously
InlinedCode.test()
Pipelined.testListPipeline()
Pipelined.testArrayPipeline()
