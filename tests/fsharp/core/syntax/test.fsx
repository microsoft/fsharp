#!/bin/usr/env fsharpi --exec
//The shebang above should be treated like a line comment. #! is only allowed in the first byte of a file.

// #Regression #Conformance #Operators #SyntacticSugar #Exceptions #ControlFlow #Arrays #Tuples #Lists #Classes #Constants #Records 
#if TESTS_AS_APP
module Core_syntax
#endif
#light
let failures = ref false
let report_failure () = 
  stderr.WriteLine " NO"; failures := true
let test s b = stderr.Write(s:string);  if b then stderr.WriteLine " OK" else report_failure() 

// Test the __LINE__ directive
test "line number test" (__LINE__ = "15")
#line 100
test "line number test" (__LINE__ = "100")

#line 102 "file.fs"
test "line number test" (__LINE__ = "102")
test "line number test" (__SOURCE_FILE__ = "file.fs")

#line 18 "original-test-file.fs"
test "line number test" (__LINE__ = "18")
test "line number test" (__SOURCE_FILE__ = "original-test-file.fs")

# 100
test "line number test" (__LINE__ = "100")

# 102 "file.fs"
test "line number test" (__LINE__ = "102")
test "line number test" (__SOURCE_FILE__ = "file.fs")

# 35 "original-test-file.fs"
test "line number test" (__LINE__ = "35")
test "line number test" (__SOURCE_FILE__ = "original-test-file.fs")


let SimpleArithmetic( )
    = let x = 10 + 12 - 3
       in let y = x * 2 + 1 in
          let r1,r2 = x/3, x%3
           in printf "x = %d, y = %d, x/3 = %d, x%%3 = %d\n"
                           x       y        r1         r2;;

module CheckDynamicOperatorsOnTypes = 
    type Foo =
         val s : string
         new(s) = { s = s }
         static member (?) (foo : Foo, name : string) = foo.s + name
         static member (?<-) (foo : Foo, name : string, v : string) = ()

    let foo = Foo("hello, ")
    let hw  : string = foo ? world
    let hw2  : unit = foo ? world <- "3"
        
module CheckDynamicOperatorsOnTypesUnconstrained = 
    type OpDynamic() =
      static member ( ? ) (x, n) = x
      member x.Prop = 1

    let f()  = 
      let op  = OpDynamic ()
      op?Hello.Prop

module MoreDynamicOpTests  =
    module Test1 = 
     type 'a Doge () = class end
     with
        static member (|~>) (_ : 'b Doge, _ : 'b -> 'c) : 'c Doge = Doge ()

     let x : System.DateTime Doge = Doge ()

     let y = x |~> (fun dt -> dt.Year) // error on this line around 'dt.Year'


    module Test2 = 
     type OpDynamic() =
      static member ( ? ) (x, n) = x
      member x.Prop = 1

     let f()  = 
      let op  = OpDynamic ()
      op?Hello.Prop

    module Test3 = 
     type M() =
        static member ($) (x:string, M) = ""
        static member ($) (x:int   , M) = 0
        static member ($) (x:float , M) = 0.0

     let inline empty< ^R, ^M when (^R or ^M) : (static member ($) : ^R * M ->  ^R) and ^M :> M> =
        let m = M()
        ((^R or ^M) : (static member ($): ^R * M -> ^R ) (Unchecked.defaultof<'R>, m))

     let a :int    = empty<  _ , M >
     let b :string = empty<  _ , M >

    module Test4 = 
     type M() =
        static member ($) (x:string, M) = ""
        static member ($) (x:int   , M) = 0
        static member ($) (x:float , M) = 0.0

     let inline empty< ^R when ( ^R or  M) : (static member ( $ ) :  ^R *  M ->  ^R)> =        
        let m = M()
        Unchecked.defaultof< ^R> $ m: ^R

// Copyright (c) Microsoft Corporation 2005-2006.  .


open System

//--------------------------------------------------------

let _ = stdout.WriteLine "Test Starting"

let SampleArithmetic1() =
    let x = 10 + 12 - 3 
    let y = x * 2 + 1 
    let r1,r2 = x/3, x%3
    printf "x = %d, y = %d, r1 = %d, r2 = %d\n" x y r1 r2
  

let SampleArithmetic2() =
    let x = 10.0 + 12.0 - 3.0 
    let y = x * 2.0 + 1.0 
    let r1 = x/3.0
    printf "x = %g, y = %g, r1 = %g\n" x y r1

  
let SampleArithmetic3() =
    // Manipulating double-precision (64-bit) floating point numbers
    let pi1 = float   3 + 0.1415 
    let pi2 = float 3 + 0.1415   // identical - int32 and int are the same
    let pi3 = float 3 + 0.1415   // identical - A.to_B and B.of_A often both exist
    let pi4 = float 3          + 0.1415   // identical - 'float' is an addiitonal ahortcuts
    printf "pi1 = %f, pi2 = %f, pi3 = %f\n" pi1 pi2 pi3;
  
    let i1 = int   3.1415 
    let i2 = int32 3.1415   // identical - int32 and int are the same
    let i3 = int32 3.1415   // identical - A.to_B and B.of_A often both exist
    let i4 = float 3.1415         // identical 
    printf "pi1 = %f, pi2 = %f, pi3 = %f\n" pi1 pi2 pi3;
  
    // Manipulating single-precision (32-bit) floating point numbers
    let f32a = 2.1415f + 1.0f             // float32 (System.Single)
    let f32b = 2.1415f + float32 1 // float32 - identical 
    printf "f32a = %f, f32b = %G\n" (float f32a) (float f32b)
  
    // Manipulating bytes
    let byteA = byte (3+4)         // byte 
    let byteB = byte 0xFF          // byte 
    printf "byteA = %d, byteB = %d\n" (int byteA) (int byteB)
  

let Sample4() =
    // Operators over integers:
    let x1 = 0xAB7F3456 &&& 0xFFFF0000 
    let x2 = 0xAB7F3456 ||| 0xFFFF0000 
    let x3 = 0x12343456 ^^^ 0xFFFF0000 
    let x4 = 0x1234ABCD <<< 1 
    let x5 = 0x1234ABCD >>> 16 
    // Also over other integral types, e.g. Int64:
    let x6 = 0x0A0A0A0A012343456L &&& 0x00000000FFFF0000L 
    // Also over other integral types, e.g. unsigned Int64:
    let x6u = 0x0A0A0A0A012343456UL &&& 0x0000FFFF00000000UL 
    // And bytes:
    let x7 = 0x13uy &&& 0x11uy 
    // Now print the results:
    printf "x1 = 0x%08x\n" x1;
    printf "x2 = 0x%08x\n" x2;
    printf "x3 = 0x%08x\n" x3;
    printf "x4 = 0x%08x\n" x4;
    printf "x5 = 0x%08x\n" x5;
    printf "x6 = 0x%016x\n" x6;
    printf "x6u = 0x%016u\n" x6u;
    printf "x7 = 0x%02x\n" (int x7)
  
  
  
//--------------------------------------------------------


let FunctionSample1() =
    let twice x = x + x 
    printf "twice 2 = %d\n" (twice 2);
    printf "twice 4 = %d\n" (twice 4);
    printf "twice (twice 2) = %d\n" (twice (twice 2))

let FunctionSample2() =
    let even n = (n%2 = 0) 
    let tick x = printf "tick %d\n" x 
    let tock x = printf "tock %d\n" x 
    let choose f g h x = if f x then g x else h x 
    let ticktock = choose even tick tock  // ticktock is a function built out of other functions using 'choose'
    for i = 0 to 10 do
        ticktock i
    

let FunctionSample3() =
    let tick x = printf "tick %d\n" x 
    let tock x = printf "tock %d\n" x 
    let choose f g h x = if f x then g x else h x 
    for i = 0 to 10 do
        // This is like the previous sample, but uses an anonymous lambda expression for 
        // the function that decides whether to tick or tock.
        choose (fun n -> n%2 = 0) tick tock i 
    

//--------------------------------------------------------

let ExceptionSample1() : unit =
    failwith "Here's how to raise a simple 'Failure' exception"
  

let ExceptionSample2() =
    try 
        printf "About to raise a simple 'Failure' exception...\n"
        failwith "Whoa!"
    with 
        Failure msg -> 
            printf "Caught a simple 'Failure' exception, msg = '%s'\n" msg
  
let ExceptionSample3() =
    try 
        printf "About to raise an exception...\n"
        match DateTime.Now.DayOfWeek with 
        | DayOfWeek.Monday -> raise (System.Collections.Generic.KeyNotFoundException())
        | _                -> raise (Failure "it's not Monday")
    with 
        | :? System.Collections.Generic.KeyNotFoundException -> 
            printf "Caught a 'Not_found' exception, it must be Monday\n"
        | Failure msg -> 
            printf "Caught a 'Failure' exception: %s\n" msg

let ExceptionSample4() =
    try 
        printf "About to raise an ArgumentException exception...\n"
        if DateTime.Now.DayOfWeek = DayOfWeek.Tuesday then 
            raise (new System.ArgumentException("Not today, it's Tuesday"))
        else
            raise (new System.InvalidOperationException("Hey, it's not Tuesday..."))
    with 
        | :? System.ArgumentException as e -> 
            printf "Caught an ArgumentException, e.Message = %s\n" e.Message
        | :? System.InvalidOperationException as e -> 
            printf "Caught an InvalidOperationException, e.Message = %s\n" e.Message
        | _ -> 
            printf "Some other exception was caught\n"
  
//--------------------------------------------------------

  
let SampleForLoop1() =
    for i = 1 to 10 do 
        printf "In a for-loop, i = %d\n" i
    
  
let SampleForLoop2() =
    for i = 0 to 9 do 
        for j = 0 to i-1 do 
            printf " "
        for j = i to 9 do 
            printf "%d" j
        printf "\n"
    
  

let SampleWhileLoop1() =
    let count = ref 0
    while (!count < 10) do 
        printf "Counting, skipping by 2, count = %d...\n" !count;
        count := !count + 2
    printf "Done counting!\n"
  


let SampleWhileLoop2() =
    let start = DateTime.Now 
    let duration = System.TimeSpan.FromMilliseconds(8.0)
    let diff (a:DateTime) (b:DateTime) = System.DateTime.op_Subtraction(System.DateTime.Now,b) 
    printf "Waiting...\n"
    // Here's the loop
    while diff DateTime.Now start < duration do
        printf "."
    // OK, we're ...
    let span = diff DateTime.Now start 
    printf "\nAttempted to busy-wait 8ms, actually waited %dms\n" span.Milliseconds
  

//--------------------------------------------------------


let SampleRec1() =
    let rec fib n = if n < 2 then 1 else fib (n-1) + fib (n-2) 
    for i = 1 to 10 do
        printf "fib %d = %d\n" i (fib i)

//--------------------------------------------------------



let SampleArray1() =
    let size = 1000 
    let arr = Array.create size 0 
    for i = 1 to size - 1 do 
        arr.[i] <- i + arr.[i-1]
    for i = 1 to size - 1 do 
        printf "arr.[%4d] = %d\n" i arr.[i]
    
  


let SampleArray2() =
    let numLetters = 26 
    let results = Array.create numLetters 0 
    let data = "The quick brown fox jumps over the lazy dog" 
    for i = 0 to data.Length - 1 do 
        let c = data.Chars(i) 
        let c = Char.ToUpper(c)  
        if c >= 'A' && c <= 'Z' then 
            let i = int c - int 'A' 
            results.[i] <- results.[i] + 1
    for i = 0 to numLetters - 1 do 
        printf "Number of '%c' characters = %d\n" (char (i + int 'A')) results.[i]
    
  


let SampleHashtbl3() =
    let tab = new System.Collections.Generic.Dictionary<char,int>(30) 
    let data = "The quick brown fox jumps over the lazy dog" 
    for i = 0 to data.Length - 1 do 
        let c = data.Chars(i) 
        if tab.ContainsKey(c) then 
            let v = tab.[c] 
            let _ = tab.Remove(c) 
            tab.Add(c,v+1)
        else 
            tab.Add(c,1)
    ;
    tab |> Seq.iter (fun kvp -> printf "Number of '%c' characters = %d\n" kvp.Key kvp.Value ) 

  

//--------------------------------------------------------


let TupleSample1() =
    let data = 1,2,3
    printf "data = %A\n" data;
    let f (a,b,c) = (a+b,b+c,c+a) 
    let res = f(f(f(data))) 
    printf "res = %A\n" res;
    let r1,r2,r3 = res 
    printf "r1 = %d, r2 = %d, r3 = %d\n" r1 r2 r3;
    let r4,r5,r6 = f(res) 
    printf "r4 = %d, r5 = %d, r6 = %d\n" r4 r5 r6
  


let ListSample1() =
    let data = [1;2;3;4]
    printf "data = %A\n" data;
    printf "head data = %d\n" (List.head data);
    printf "tail data = %A\n" (List.tail data);
    printf "length data = %d\n" (List.length data);
    printf "nonempty data = %b\n" (List.isEmpty data);
    let consume data = 
        match data with 
        | 1::rest    -> printf "matched a 1\n";       rest
        | 2::3::rest -> printf "matched a 2 and 3\n"; rest 
        | [4]        -> printf "matched a 4\n";       []
        | _          -> printf "unexpected!";         [] 
    let data = consume data 
    let data = consume data 
    let data = consume data 
    printf "At end of list? %b\n" (data = [])
  

let ListSample2() =
    let data = [1;2;3;4]
    let r1 = List.map (fun x -> x + 1) data 
    printf "Adding '1' using map = %A\n" r1
    let r2 = List.map string data 
    printf "Converting to strings using map = %A\n" r2
    let r3 = List.map (fun x -> (x,x)) data 
    printf "Tupling up using map = %A\n" r3
 

let ListSample3() =
    let data = ["Cats";"Dogs";"Mice";"Elephants"]
    data |> List.iter (fun x -> printf "item: %s\n" x)
 

let ListSample4() =
    let data = ["Cats";"Dogs";"Mice";"Elephants"]
    data |> List.iteri (fun i x -> printf "item %d: %s\n" i x)
 


let ListSample5() =
    let data = [("Cats",4);
                ("Dogs",5);
                ("Mice",3);
                ("Elephants",2)]
    let count = List.fold (fun acc (nm,x) -> acc+x) 0 data
    printf "Total number of animals: %d\n" count
 

let ListSample6() =
    let data = [("Cats",4);
                ("Dogs",5);
                ("Mice",3);
                ("Elephants",2)]
    let res = List.filter (fun (nm,x) -> String.length nm <= 4) data
    printf "Animals with short names: %A\n" res
 


let ListSample7() =
    let data = [("Cats",4);
                ("Dogs",5);
                ("Mice",3);
                ("Elephants",2)]
    let res = List.choose (fun (nm,x) -> if String.length nm <= 4 then Some(x) else None) data
    printf "Counts of animals with short names: %A\n" res
 
  

let OptionsSample2() =
    let data = Some(1,3)
    printf "data = %A\n" data;
    printf "Option.isSome data = %b\n" (Option.isSome data);
    printf "Option.isNone data = %b\n" (Option.isNone data);
    //printf "Option.length data = %d\n"  (Option.length data);
    printf "Option.get data = %A\n" (Option.get data);
    let data2 = None
    printf "Option.isSome data = %b\n" (Option.isSome data2);
    printf "Option.isNone data = %b\n" (Option.isNone data2);
    //printf "Option.length data = %d\n"  (Option.length data2)


let OptionsSample3() =
    let openingHours day = 
        match day with 
        | DayOfWeek.Monday 
        | DayOfWeek.Tuesday 
        | DayOfWeek.Thursday 
        | DayOfWeek.Friday    -> Some(9,17)
        | DayOfWeek.Wednesday -> Some(9,19) // extended hours on Wednesday
        | _ -> None 
    let today = DateTime.Now.DayOfWeek 
    match openingHours today with 
    | None -> printf "The shop's not open today\n"
    | Some(s,f) -> printf "The shop's open today from %02d:00-%d:00\n" s f

//--------------------------------------------------------

  

let ComparisonSample1() =
    let show a b = 
        printf "%A < %A: %b\n" a b (a < b)
        printf "%A = %A: %b\n" a b (a = b)
        printf "%A > %A: %b\n" a b (a > b)
    
    show 1 2;
    show 2 2;
    show "1" "2"
    show "abb" "abc" 
    show "aBc" "ABB" // case-sensitive
    show None (Some 1);
    show None None;
    show (Some 0) (Some 1);
    show (Some 1) (Some 1);
    show [1;2;3] [1;2;2];
    show [] [1;2;2]
  

let HashingSample2() =
    let show a = printf "hash(%A) : %d\n" a (hash a) 
    show 1;
    show 2;
    show "1"
    show "2"
    show "abb" 
    show "aBc" // case-sensitive
    show None;
    show (Some 1);
    show (Some 0);
    show [1;2;3];
    show [1;2;3;4;5;6;7;8];
    show [1;2;3;4;5;6;7;8;9;10;11];
    show [1;2;3;4;5;6;7;8;9;10;11;12;13;14;15]

  
//--------------------------------------------------------  


let dummy() = ()
type wheel = Wheel of float  // radius of wheel, inches
type cycle = 
    | Unicycle of wheel
    | Bicycle of wheel * wheel 

let veryBigWheel = Wheel(26.0)
let bigWheel     = Wheel(13.0)
let smallWheel   = Wheel(6.0)

let pennyFarthing = Bicycle(veryBigWheel,smallWheel)
let racer         = Bicycle(bigWheel    ,bigWheel)
let kidsBike      = Bicycle(smallWheel  ,smallWheel)

let UnionSample1() =
    let show bike = 
        match bike with 
        | Unicycle (Wheel r) -> printf "Unicycle, one wheel, radius = %f\n" r
        | Bicycle (Wheel r1,Wheel r2) -> printf "Bicycle, two wheels, front = %f, back = %f\n" r1 r2 
    show pennyFarthing;
    show racer;
    show kidsBike



let dummy2() = ()
type point = { x: float; y: float}
type triangle = { p1: point; p2: point; p3: point }
type vector = { dx: float; dy: float}

let origin = { x = 0.0; y = 0.0 }
let onex = { x = 1.0; y = 0.0 }
let oney = { x = 0.0; y = 1.0 }
let diff p1 p2 = { dx = p2.x - p1.x; dy = p2.y - p1.y }
let sides tri = 
    diff tri.p2 tri.p1, 
    diff tri.p3 tri.p2, 
    diff tri.p1 tri.p3


let RecordSample1() =
    let triangle1 = { p1=origin;p2=onex;p3=oney } 
    printf "triangle1 = %A\n" triangle1;
    printf "sides(triangle1) = %A\n" (sides triangle1)
  
  

let dummy3() = ()
type Point = { x: float; y: float}
    with 
        member p.VectorFromOrigin = { dx = p.x; dy = p.y }
        static member Origin = { x = 0.0; y = 0.0 }
        static member (+) ((p:Point),(v:Vector)) = { x = p.x + v.dx; y = p.y + v.dy }
    end
 
and Vector = { dx: float; dy: float}
    with 
        static member Zero = { dx = 0.0; dy = 0.0 }
        static member OneX = { dx = 1.0; dy = 0.0 }
        static member OneY = { dx = 0.0; dy = 1.0 }
        static member (+) ((v1:Vector),(v2:Vector)) = { dx = v1.dx + v2.dx; dy = v1.dy + v2.dy }
    end



let MemberSample1() =
    printf "Point.Origin = %A\n" Point.Origin;
    printf "Point.Origin + Vector.OneX = %A\n" (Point.Origin + Vector.OneX);
    printf "Vector.OneX + Vector.OneY = %A\n" (Vector.OneX + Vector.OneY)

  


let InterfaceSample1() =
    let disposableObject1 = 
        { new IDisposable  with 
              member __.Dispose() = printf "disposed!\n" } 
    let disposableObject2 = 
        let disposed = ref false 
        { new IDisposable with 
            member __.Dispose() = if not !disposed then (disposed := true; printf "disposed!\n") } 
    disposableObject1.Dispose();
    disposableObject1.Dispose();
    disposableObject2.Dispose();
    disposableObject2.Dispose()

let dummy5() = ()
type WrapOneStream = 
    // some owned objects that need to be disposed
    { myManagedResource: IO.MemoryStream; }
    with 
        // Here is the standard machinery to implement disposability.
        // This tends to be replicated each type that must support the
        // disposal paradigm.
        interface IDisposable with
            member x.Dispose() = 
                 x.Dispose(true); 
                 GC.SuppressFinalize(x)
        end
        // We override Finalize the case this class has native resources of its own.
        // In this sample it doesn't, but we have included the full pattern 
        // here for completeness.
        override x.Finalize() = x.Dispose(false)
        member x.Dispose(deep: bool) =  
            printf "disposing, deep = %b!\n" deep;
            if deep then x.myManagedResource.Dispose()
  end 

let dummy4() = ()
type IPoint = 
    interface
        abstract X : float
        abstract Y : float
    end

/// This interface is really just a function, but we give it a name
/// here as an example. It represents a function from some variable to (X,Y)
type ILine = 
    interface
        abstract GetY : float -> IPoint
    end

/// A function representing a line, where the parameter governing the line is the
/// X coordinate itself
type Line = 
    class
        val a: float
        val c: float
        new (a,c) = { a=a;c=c}
        interface ILine with 
            member l.GetY(x) = { new IPoint with 
                                    member __.X=x 
                                    member __.Y= l.a * x + l.c }
        end
end

let InterfaceSample3() =
    let line = new Line(2.0,1.5) :> ILine 
    let origin =  { new IPoint with 
                         member __.X=0.0 
                         member __.Y= 0.0 }
    let point1 = line.GetY(-1.0)
    let point2 = line.GetY(0.0)
    let point3 = line.GetY(1.0)
    let output_point os (p:IPoint) = fprintf os "(%f,%f)" p.X p.Y 
    printf "origin = %a\n" output_point origin;
    printf "point1 = %a\n" output_point point1;
    printf "point2 = %a\n" output_point point2;
    printf "point3 = %a\n" output_point point3
  
let LineDirectedInputSample1() = 
    
    // Write a test file
    let outputChannel = System.IO.File.CreateText @"test.txt" 
    outputChannel.Write "This is a test file.\r\nIt is easy to read.";
    outputChannel.Dispose();
    
    // Now read the test file.
    let inputChannel = System.IO.File.OpenText @"test.txt" 
    let line1 = inputChannel.ReadLine() 
    let line2 = inputChannel.ReadLine() 
    // Don't forget to close the channel
    inputChannel.Dispose();
    printf "line1=%s\nline2=%s\n" line1 line2

module InfixTokenIndentationExamples = begin


    let f x =
       let y = 1
       -1

end

let LineDirectedInputSample2() = 
    // Write a test file
    System.IO.File.WriteAllLines(@"test.txt", [| "This is a test file."; 
                                                 "It is easy to read." |]);

    // Now read it.  We use using to ensure the file is closed even if an exception occurs
    // during reading.  
    let line1,line2 = 
        using (System.IO.File.OpenText @"test.txt") (fun sr -> 
            let line1 = sr.ReadLine() 
            let line2 = sr.ReadLine() 
            (line1,line2))  
    printf "line1=%s\nline2=%s\n" line1 line2

let LineDirectedInputSample3() = 
    // Write a test file
    System.IO.File.WriteAllLines(@"test.txt", [| "This is a test file."; 
                                                 "It is easy to read." |]);
    // Now read it 
    let lines = System.IO.File.ReadAllLines @"test.txt"  
    printf "%s" (sprintf "%A" lines)

let EntireFileInputSample2() = 
    // Write a test file
    System.IO.File.WriteAllLines(@"test.txt", [| "This is a test file."; 
                                                 "It is easy to read." |]);
    // Now read it 
    let res = System.IO.File.ReadAllText(@"test.txt")  
    printf "%s" res

module String = 
    let split (c : char list) =
        let ca = Array.ofList c 
        fun (s:string) ->
            Array.toList(s.Split(ca, System.StringSplitOptions.RemoveEmptyEntries))

let ReadCSVFile1() = 
    // Write a test file
    System.IO.File.WriteAllLines(@"test.csv", [| "Desmond, Barrow, Market Place, 2"; 
                                                 "Molly, Singer, Band, 12" |]);
    // Now read it 
    let linesSplitIntoWords = 
        System.IO.File.ReadAllLines(@"test.csv")
        |> Array.map (String.split [',';' ';'\t'])
        |> Array.map List.toArray  
    printf "%A"  linesSplitIntoWords
        
let EnumerateCSVFile1() = 

    // Write a test file
    System.IO.File.WriteAllLines(@"test.csv", [| "Desmond, Barrow, Market Place, 2"; 
                                                 "Molly, Singer, Band, 12" |]);

    /// This function builds an IEnumerable<string list> object that enumerates the CSV-split
    /// lines of the given file on-demand 
    let CSVFileEnumerator(fileName) = 
        // The function is implemented using RuntimeHelpers.GenerateUsing, which is the standard
        // function for building enumerators for sources such as files and database
        // connections.  This function takes two function parameters: one function to generate 
        // a handle to the resource, and one to incrementally read new results from the handle.  
        // The function guarantees to cleanup the resources associated with the handle when 
        // each individual enumeration is complete. Multiple concurrent enumerations can be 
        // in progress since each will use a different stream handle and/or database connection.
        seq { use sr = System.IO.File.OpenText(fileName)
              while not sr.EndOfStream  do
                yield sr.ReadLine() |> String.split [',';' ';'\t'] }

    // Now test this out on our test file, iterating the entire file
    let test = CSVFileEnumerator(@"test.csv")  
    printf "-------Enumeration 1------\n";
    test |> Seq.iter (sprintf "%A" >> printf "line %s\n");
    // Now do it again, this time determining the numer of entries on each line.
    // Note how the file is read from the start again, since each enumeration is 
    // independent.
    printf "-------Enumeration 2------\n";
    test |> Seq.iter (List.length >> printf "line has %d entries\n");
    // Now do it again, this time determining the numer of entries on each line.
    // Note how the file is read from the start again, since each enumeration is 
    // independent.
    printf "-------Enumeration 3------\n";
    test |> Seq.iter (List.map String.length >> sprintf "%A" >> printf "lengths of entries: %s\n")

let test27832() = 
    if true then
      3
    else 
      if true then 
        4
      else 
        if true then
          5
        else 
          6

let test27834() = 
    if true then
      3
    else 
      if true then printf "hello";
      4 

let test278343() = 
    if true then
      3
    else if true then 
      4
    else if true then
      5
    else 
      6
 
let test273343() = 
    if true then
      3
    else 
      if true then printf "hello";
      4
 
let test278342() = 
    if true then
      3
    elif true then 
      4
    elif true then
      5
    else 
      6

open System.IO
let findDLLs (dir:string) =
    if (Directory.Exists dir) then
        let files = Directory.GetFiles(dir, "*.dll") 
        Array.toList files
    else
        if true then 
            eprintf "Directory %s does not exist!\n" dir
        []
 
do SimpleArithmetic( );;

type recd = {x : int; y:int; z:int }
let r = { x = 3; y = 4;z=6 }
let r2 = { r with 
             x = 4 }

let r3 = { r with 
             x = 4;
             y = 6 }
let r4 = { r with 
            x = 4;
            y = 6 }
let r4b = { r with x = 4;
                   y = 6 }
let r4c = { r with   x = 4;
                     y = 6 }
let r5 = { new recd 
             with x = 4 
             and  y = 5
             and  z = 6}

let testTryFinallySyntaxOnOneLine () = 
    try () finally ()


#if !TESTS_AS_APP && !NETCOREAPP
type SampleForm = 
  class
    inherit System.Windows.Forms.Form
    new () as this =
       { }
       
       then 
        ()
  end
#endif

(* check do introduces a #light block *)
begin 
   [ for x in [1;2;3] do
       let z = x + 1 
       let y = x + 1 
       yield y ]
end

(* check do introduces a #light block *)
begin 
   [ for x in [1;2;3] do
       let z = x + 1 
       let y = x + 1 
       yield (let x = 3 
              x+x) ]
end 


begin 
   [ for x in [1;2;3] do 
        let z = x + 1 
        let y = x + 1 
        yield y ]
end

begin 
   [ for x in [1;2;3] do 
        let z = x + 1 in 
        let y = x + 1 in 
        yield! [y] ]
end

begin 
   [ for x in [1;2;3] do
       let z = x + 1  
       let y = x + 1 
       yield y ]
end

(* check do introduces a #light block *)
begin 
   [ for x in [1;2;3] do
       let z = x + 1 in 
       let y = x + 1 
       yield y ]
end

(* check do introduces a #light block *)
begin 
   [ for x in [1;2;3] do
       if (x > x + 1) then
         let y = x + 1 
         yield y ]
end

(* check d introduces a #light block *)
begin 
   [ for x in [1;2;3] do
       if (x > x + 1) then
         yield (let y = x + 1 
                y) ]
end

begin 
   [ for x in [1;2;3]  
       -> let y = x + 1 
          y ]
end
begin 
   [ for x in [1;2;3]  
       -> let y = x + 1 
          y ]
end
begin 
   [ while false  do
       yield (let y = 2 + 1 
              y) ]
end
begin 
   [ while false  do
       yield 3 ]
end
begin 
   [ while false  do
       if false then
         yield 4 ]
end
begin 
   [ while false  do
       if false then
         do printfn "hello"
         yield 5 ]
end
begin 
   [ while false  do
       let x = 1
       if false then
         do printfn "hello"
         yield 5 ]
end
begin 
   [ let x = 1
     while false  do
       let x = 1
       if false then
         do printfn "hello"
         yield 5 ]
end
begin 
   [ let x = 1
     while false  do
       let x = 1
       while false  do
         if false then
           do printfn "hello"
           yield 5 ]
end
begin 
   [ for x in [1;2;3] 
       -> 1 ]
end

begin 
   [ for x in [1;2;3] do 
      if (x > x + 1) then
       let z = x + 1 in 
       let y = x + 1 
       yield y ]
end

begin 
   [ for x in [1;2;3] do 
        if (x > x + 1) then
         let z = x + 1 in 
         let y = x + 1 
         yield y ]
end
begin 
   [ for x in [1;2;3] do 
       if (x > x + 1) then
         let z = x + 1 
         let y = x + 1 
         yield y ]
end

let rand = new Random()

type EasyOptionInt =
| None
| Some of int
with
    member x.ToNullableInt =
        match x with
        | EasyOptionInt.None -> new Nullable<int>(-1) 
        | EasyOptionInt.Some x -> new Nullable<int>(x)
end

let getPoistiveInt2() = 
    let i = rand.Next(10)
    match i with
    | 0 -> EasyOptionInt.None
    | i -> EasyOptionInt.Some(i)

do test "vliwe9a" (-1-2 = -3)
do test "vliwe9a" (2 - -5 = 7)
do test "vliwe9a" (2- - 5 = 7)
do test "vliwe9a" (2 - - 5 = 7)
do test "vliwe9a" (2- -5 = 7)
do test "vliwe9s" (let n = -1 in let m = -2 in -n-m = 3)
do test "vliwe9s" (let n = -1 in let m = -2 in -n-m = +3)
do test "vliwe9s" (let n = -1 in let m = -2 in -n - m = +3)

do test "vliwe9d" ((1)-2 = -1)

do test "vliwe9f" ((match 1,1 with -1,-1 -> 1 | _ -> 2) = 2)
do test "vliwe9g" ((match -1,-1 with -1,-1 -> 1 | _ -> 2) = 1)

let f x = x

do test "vliwe91"   (f -2y = - 2y)
do test "vliwe92"   (f -2s = - 2s)
do test "vliwe93"   (f -2 = - 2)
do test "vliwe94"   (f -2L = - 2L)
do test "vliwe95"   (f -2n = - 2n)
do test "vliwe96"   (f -2.0 = - 2.0)
do test "vliwe97"   (f -2.0f = - 2.0f)
#if !NETCOREAPP
do test "vliwe99"   (f -2I = - 2I)
#endif

do test "vliwe9q"   ((function -2y -> true | _ -> false) (- 2y))
do test "vliwe9w"   ((function -2s -> true | _ -> false) (- 2s))
do test "vliwe9e"   ((function -2 -> true | _ -> false) (- 2))
do test "vliwe9r"   ((function -2L -> true | _ -> false) (- 2L))
do test "vliwe9t"   ((function -2n -> true | _ -> false) (- 2n))
do test "vliwe9y"   ((function -2.0 -> true | _ -> false) (- 2.0))
do test "vliwe9u"   ((function -2.0f -> true | _ -> false) (- 2.0f))
//do test "vliwe9i"   ((function -2I -> true | _ -> false) (- 2I))
//do test "vliwe9o"   ((function -2N -> true | _ -> false) (- 2N))

module NegativePrecedence = begin
    let x = 1
    let R x = x
    let R2 x y = x
    let v1 = R 3
    let v2 = -x
    let v3 = R -x
    let v3b = R2 -x -x
    let v3c = R2 x -x
    let v3d = R2 -(R x) -x
    let v3e = R2 -(R -x) -x
    let v4 = -R(3)
    let v5 = -R (3)
    let v6 = -R 3
end

module UnicodeChars = begin

    let some_unicode_char = '\u00D6' 

    let another_unicode_char = '\U000007FF'
end



module EscapeChars = begin
    do test "cwjnecew90" ("\a" = "\007")
    do test "cwjnecew91" ('\a' = '\007')
    do test "cwjnecew91b" ('\a'B = '\007'B)
    do test "cwjnecew92" ("\f" = "\012")
    do test "cwjnecew93b" ('\f' = '\012')
    do test "cwjnecew93" ('\f'B = '\012'B)
    do test "cwjnecew94" ("\v" = "\011")
    do test "cwjnecew95" ('\v' = '\011')
    do test "cwjnecew95b" ('\v'B = '\011'B)
end

module NoParensNeededForHighPrecedenceTypeApplications = begin

    let f x = x

    let ty = f typeof<int>
end

module TypeArgsCanContainRightArrow = begin
    let (f_ok : unit -> int) = Unchecked.defaultof<(unit->int)>
    let (f : unit -> int) = Unchecked.defaultof<unit->int>
    let (g : unit -> int) = if true then Unchecked.defaultof<unit->int> else fun() -> 0

end

module QuoteDotParsing = begin
  let _ = <@ 1 @>.Raw 
  let _ = <@@ 1 @@>.ToString() 
end
  
module MappableNumericLiteralsG = begin
    module NumericLiteralG =
        let inline FromZero() =  LanguagePrimitives.GenericZero<_>
        let inline FromOne() = LanguagePrimitives.GenericOne<_>
    do test "Gvrer90a" (0G + 1G + 1G = 2)
    do test "Gvrer90b" (0G + 1G + 1G = 2.0)
    do test "Gvrer90c" (0G + 1G + 1G = 2.0f)
    do test "Gvrer90d" (0G + 1G + 1G = 2uy)
end
module MappableNumericLiteralsN = begin

    module NumericLiteralN =
        let FromZero() =  0
        let FromOne() = 1
        let FromInt32(x:int32) =  x
        let FromInt64(x:int64) =  int32 x
        let FromString(s:string) =  -2

    do test "Nvrer90e" (0N = 0)
    do test "Nvrer90f" (1N = 1)
    do test "Nvrer90g" (-1N = -1)
    do test "Nvrer90h" (2N = 2)
    do test "Nvrer90i" (2147483647N = System.Int32.MaxValue)
    do test "Nvrer90j" (-2147483648N = System.Int32.MinValue)
    do test "Nvrer90k" (2147483648N = int32 2147483648L)
    do test "Nvrer90m" (-2147483649N = int32 -2147483649L)
    do test "Nvrer90m" (9223372036854775807N = int32 9223372036854775807L)
    do test "Nvrer90m" (-9223372036854775808N = int32 -9223372036854775808L)
    do test "Nvrer90m" (9223372036854775808N = -2)
    do test "Nvrer90m" (-9223372036854775809N = -2)
    do test "Nvrer90m" (10000000000000000000000000000000000000000000000000000000000000000000000N = -2)

end

module MappableNumericLiteralsNToString = begin

    module NumericLiteralN =
        let FromZero() =  "0"
        let FromOne() = "1"
        let FromInt32(x:int32) =  string x
        let FromInt64(x:int64) =  string x
        let FromString(s:string) =  s

    do test "N2vrer90e" (0N = "0")
    do test "N2vrer90f" (1N = "1")
    do test "N2vrer90g" (-1N = "-1")
    do test "N2vrer90h" (2N = "2")
    do test "N2vrer90i" (2147483647N = "2147483647")
    do test "N2vrer90j" (-2147483648N = "-2147483648")
    do test "N2vrer90k" (2147483648N = "2147483648")
    do test "N2vrer90m" (-2147483649N = "-2147483649")
    do test "N2vrer90m" (9223372036854775807N = "9223372036854775807")
    do test "N2vrer90m" (-9223372036854775808N = "-9223372036854775808")
    do test "N2vrer90m" (9223372036854775808N = "9223372036854775808")
    do test "N2vrer90m" (-9223372036854775809N = "-9223372036854775809")
    do test "N2vrer90m" (10000000000000000000000000000000000000000000000000000000000000000000000N = "10000000000000000000000000000000000000000000000000000000000000000000000")

end

module MappableNumericLiteralsZToString = begin

    module NumericLiteralZ =
        let FromZero() =  "0"
        let FromOne() = "1"
        let FromInt32(x:int32) =  string x
        let FromInt64(x:int64) =  string x
        let FromString(s:string) =  s

    do test "Zvrer90e" (0Z = "0")
    do test "Zvrer90f" (1Z = "1")
    do test "Zvrer90g" (-1Z = "-1")
    do test "Zvrer90h" (2Z = "2")
    do test "Zvrer90i" (2147483647Z = "2147483647")
    do test "Zvrer90j" (-2147483648Z = "-2147483648")
    do test "Zvrer90k" (2147483648Z = "2147483648")
    do test "Zvrer90m" (-2147483649Z = "-2147483649")
    do test "Zvrer90m" (9223372036854775807Z = "9223372036854775807")
    do test "Zvrer90m" (-9223372036854775808Z = "-9223372036854775808")
    do test "Zvrer90m" (9223372036854775808Z = "9223372036854775808")
    do test "Zvrer90m" (-9223372036854775809Z = "-9223372036854775809")
    do test "Zvrer90m" (10000000000000000000000000000000000000000000000000000000000000000000000Z = "10000000000000000000000000000000000000000000000000000000000000000000000")

end

module MappableNumericLiteralsQToString = begin

    module NumericLiteralQ =
        let FromZero() =  "0"
        let FromOne() = "1"
        let FromInt32(x:int32) =  string x
        let FromInt64(x:int64) =  string x
        let FromString(s:string) =  s

    do test "Qvrer90e" (0Q = "0")
    do test "Qvrer90f" (1Q = "1")
    do test "Qvrer90g" (-1Q = "-1")
    do test "Qvrer90h" (2Q = "2")
    do test "Qvrer90i" (2147483647Q = "2147483647")
    do test "Qvrer90j" (-2147483648Q = "-2147483648")
    do test "Qvrer90k" (2147483648Q = "2147483648")
    do test "Qvrer90m" (-2147483649Q = "-2147483649")
    do test "Qvrer90m" (9223372036854775807Q = "9223372036854775807")
    do test "Qvrer90m" (-9223372036854775808Q = "-9223372036854775808")
    do test "Qvrer90m" (9223372036854775808Q = "9223372036854775808")
    do test "Qvrer90m" (-9223372036854775809Q = "-9223372036854775809")
    do test "Qvrer90m" (10000000000000000000000000000000000000000000000000000000000000000000000Q = "10000000000000000000000000000000000000000000000000000000000000000000000")

end

module MappableNumericLiteralsRToString = begin

    module NumericLiteralR =
        let FromZero() =  "0"
        let FromOne() = "1"
        let FromInt32(x:int32) =  string x
        let FromInt64(x:int64) =  string x
        let FromString(s:string) =  s

    do test "Rvrer90e" (0R = "0")
    do test "Rvrer90f" (1R = "1")
    do test "Rvrer90g" (-1R = "-1")
    do test "Rvrer90h" (2R = "2")
    do test "Rvrer90i" (2147483647R = "2147483647")
    do test "Rvrer90j" (-2147483648R = "-2147483648")
    do test "Rvrer90k" (2147483648R = "2147483648")
    do test "Rvrer90m" (-2147483649R = "-2147483649")
    do test "Rvrer90m" (9223372036854775807R = "9223372036854775807")
    do test "Rvrer90m" (-9223372036854775808R = "-9223372036854775808")
    do test "Rvrer90m" (9223372036854775808R = "9223372036854775808")
    do test "Rvrer90m" (-9223372036854775809R = "-9223372036854775809")
    do test "Rvrer90m" (10000000000000000000000000000000000000000000000000000000000000000000000R = "10000000000000000000000000000000000000000000000000000000000000000000000")

end

module MappableNumericLiteralsIToString = begin

    module NumericLiteralI =
        let FromZero() =  "0"
        let FromOne() = "1"
        let FromInt32(x:int32) =  string x
        let FromInt64(x:int64) =  string x
        let FromString(s:string) =  s

    do test "Ivrer90e" (0I = "0")
    do test "Ivrer90f" (1I = "1")
    do test "Ivrer90g" (-1I = "-1")
    do test "Ivrer90h" (2I = "2")
    do test "Ivrer90i" (2147483647I = "2147483647")
    do test "Ivrer90j" (-2147483648I = "-2147483648")
    do test "Ivrer90k" (2147483648I = "2147483648")
    do test "Ivrer90m" (-2147483649I = "-2147483649")
    do test "Ivrer90m" (9223372036854775807I = "9223372036854775807")
    do test "Ivrer90m" (-9223372036854775808I = "-9223372036854775808")
    do test "Ivrer90m" (9223372036854775808I = "9223372036854775808")
    do test "Ivrer90m" (-9223372036854775809I = "-9223372036854775809")
    do test "Ivrer90m" (10000000000000000000000000000000000000000000000000000000000000000000000I = "10000000000000000000000000000000000000000000000000000000000000000000000")

end

module OperatorNamesAddressOf = begin

    let op_AddressOf (x:int) = string x
    do test "vrnoe09" (&3 = "3")

end

module OperatorNamesIntegerAddressOf = begin

    let op_IntegerAddressOf (x:int) = string x
    do test "vrnoe09" (&&3 = "3")

end
    
module OperatorBooleanAnd = begin

    let op_BooleanAnd (x:string) (y:string) = string x + string y
    do test "vrnoe09" ( ("3" && "4") =  "34")

end
    
module OperatorBooleanOr = begin

    let op_BooleanOr (x:string) (y:string) = string x + string y
    do test "vrnoe09" ( ("3" || "4") =  "34")

end


module StartInCOmments = begin
    //This works
    (*
    let a1 = ( * )
    *)
    let b1 = ()


    //This should work
    (*
    let a3 = (*)
    *)
    let b3 = ()
end


module PrecedenceOfTypeOperators = begin
    // Check :> operator has precedence lower than '|>'
    let _ : obj  = ([1] |> List.map id) :> obj // this is how it should parse
    let _ : obj = [1] |> List.map id :> obj
    // Check :?> operator has precedence lower than '|>'
    let _ : list<int> = ([1] |> box) :?> list<int>  // this is how it should parse
    let _ : list<int> = [1] |> box :?> list<int>   // this is the real test
    let _ : list<int> = [1] |> box :?> int list  // this is the real test

    // Check :> operator has LEFT precedence 
    let _ : obj  = (([1] |> List.map id) :> System.IComparable) :> obj // this is how it should parse
    let _ : obj = [1] |> List.map id :> System.IComparable :> obj  // this is the real test


    // Check :?> operator has LEFT precedence 
    let _ : list<int> = ([1] |> box) :?> System.IComparable :?> list<int>  // this is how it should parse
    let _ : list<int> = [1] |> box :?> System.IComparable :?> list<int>  // this is the real test
    let _ : list<int> = [1] |> box :?> System.IComparable :?> int list  // this is the real test

    // Check :> operator has precedence greater than ','
    let _, _ = (([1] |> List.map id) :> obj), (([1] |> List.map id) :> obj) // this is how it should parse
    let _, _ = [1] |> List.map id :> obj, [1] |> List.map id :> obj  // this is the real test
    // Check :?> operator has precedence greater than ','
    let _, _ = (([1] |> box) :?> list<int>), (([1] |> box) :?> list<int>) // this is how it should parse
    let _, _ = [1] |> box :?> list<int>, [1] |> box :?> list<int>  // this is the real test


    // Check :? operator has precedence lower than '++'
    let _ = 
       let (++) a b = box (List.length (a @ b))
       let (_ : bool)  = ([1] ++ [1]) :? int  // this is how it should parse
       let (_ : bool)  = [1] ++ [1] :? int  // this is the real test
       1

    // Check :? operator has precedence greater than '|>'
    let _ = 
       let (|>) a b = (a,b)
       let b1 = box 1
       let (_ : int list), (_ : bool) = [1] |> (b1 :? int) // this is how it should parse
       let (_ : int list), (_ : bool) = [1] |> b1 :? int  // this is the real test
       1



end


module MultiLineTypeParameterTests = 

    type C<'T,
           'U>() = 
         static let x =  1

    type C2<[<System.CLSCompliantAttribute(true)>] 'T,
            [<System.CLSCompliantAttribute(true)>] 'U>() = 
         static let x =  1

    type C3<[<System.CLSCompliantAttribute(true)>] 'T,
               [<System.CLSCompliantAttribute(true)>] 'U>() = 
         static let x =  1

    type C4<[<System.CLSCompliantAttribute(true)>] 'T1,
            [<System.CLSCompliantAttribute(true)>] 'T2,
            [<System.CLSCompliantAttribute(true)>] 'T3,
            [<System.CLSCompliantAttribute(true)>] 'T4,
            [<System.CLSCompliantAttribute(true)>] 'T5,
               [<System.CLSCompliantAttribute(true)>] 'U>() = 
         static let x =  1

    type C5<[<System.CLSCompliantAttribute(true)>] 'T1,
              [<System.CLSCompliantAttribute(true)>] 'T2,
                [<System.CLSCompliantAttribute(true)>] 'T3,
                  [<System.CLSCompliantAttribute(true)>] 'T4,
                      [<System.CLSCompliantAttribute(true)>] 'T5,
                      [<System.CLSCompliantAttribute(true)>] 'U>() = 
         static let x =  1

module AdhocIndentationTests = 

    let fffffffffffffffff x = 3
    let mutable z = 3
    let f0 = 
        z <- fffffffffffffffff (3,
                         3
                      )

    let f1() = 
         fffffffffffffffff (false &&
           true && 
           false)

    let f2() = 
         fffffffffffffffff (
           printf "hello"
           printf "hello"
           printf "hello"
           true)



module TypeApplicationDisambiguation = 
    let (||) a b = 1 // this gives a warning - ignore
    let (&&) a b = 1 // this gives a warning - ignore
    let (<) a b = 1 // this gives a warning - ignore
    let (>) a b = 1 // this gives a warning - ignore
    let (@) a b = 1
    let (@@) a b = 1
    let (^^) a b = 1
    let ( ** ) a b = 1
    let ( *** ) a b = 1
    let ( % ) a b = 1
    let ( %%% ) a b = 1
    let ( <<< ) a b = 1
    let ( >>> ) a b = 1
    let ( &&& ) a b = 1
    let ( => ) a b = 1
    let ( ? ) a b = 1
    
    let f0 x = id<int> // this is considered a type application
    
    // Check that the following expressions are NOT considered type applications
    let f1 x = x<x ||| x>x
    let f2 x = x<x &&& x>x
    let f3 x = x<x @ x>x
    let f3b x = x<x @@ x>x
    let f4 x = x<x ^^ x>x
 //   let f5 x = x<x = x>x
    let f6 x = x<x ** x>x
    let f7 x = x<x *** x>x
    let f8 x = x<x % x>x
    let f9 x = x<x %%% x>x
    let f10 x = x<x <<< x>x
 //   let f13 x = x<x ? x, x>x
    let f14 x = x<x &&& x>x
    let f16 x = x<x => x>x
    let f17 x = x<x || x>x
    let f18 x = x<x && x>x
    // We explicitly DO consider these to be type applications 
    //      f<x >>> x>x
    //      f<int>x
    //      f<x * x>x
    //      f<x , x>x
    //      f<x ^ x>x
    //      f<x ^- x>x
    //      f<x / x>x
    //      f<x -> x>x
    //      f<x # x>x
    //      f<x ' x>x

module MultiLineWhenInPatternMatchSyntaxTests = 
    let test1 x =
        match x with
        | _ when true &&
                 false -> 
                 let x = 1 
                 x


    let test2 x =
        match x with
        | _ when true &&
                 false ->    
         let x = 1 
         x



    let test3 x =
        match x with
        | _ when true &&
                 false ->    
                    1
        | _ when (false &&
                  true) ->   
             0
        | _ -> failwith "Shouldn't get here."

     
    type IntType = Negative | Zero | Positive

    let test x =
        match x with
        | _ when x = 0 -> 
         Zero
        | _ when x < 0 &&
                 x < 0 -> 
         Negative
        | _ when (x > 0 &&
                  x > 0) ->
             Positive
        | _ -> failwith "Shouldn't get here."


module PostiveWithWithoutSemicolonTests =

    type r = { a : int; b : int } 
    let r = { a = 1; b = 2 }

    let v1 = { r with a = 1
                      b = 3 } 
                     
    let v1b = { r with 
                    a = 1
                    b = 3 } 
                     
                     
                     
    let v2 = { r with a = 
                        let x = 1
                        x + x;
                      } 
    let v3 = {v1 with a=30
                      r.b=40}

    let v4 = {v1 with r.a=30
                      b=0}


module LeftAssignmentWithStructuredRightHandSide =

    let mutable x = 1

    let f1() = 
      x <- 
          let y = 1
          y + y

      x <- let y = 1
           y + y


      x <- while false do
             ()
           1 + 1

      x <- try 
             3
           with _ -> 2

      x <- try 
             3
           finally 
             ()

      x <- use x = { new System.IDisposable with member __.Dispose() = () }
           3

      x <- if true then () else ()
           4

      x <- 
          printfn "hello"
          2 + 2

module RecordExpressionsWithStructuredRightHandSide =
    let f3a() = 
      { contents = 
          printfn "hello"
          1 + 1 }

    let f3b() = 
      { contents = let y = 1
                   y + y }

    let f3c() = 
      { contents = while false do
                     ()
                   1 + 1 }

    let f3d() = 
      { contents = try 
                     3
                   with _ -> 
                     2 }

    let f3e() = 
      { contents = try 
                     3
                   finally () }

    let f3f() = 
      { contents = use x = { new System.IDisposable with member __.Dispose() = () }
                   3 }

    let f3g() = 
      { contents = if true then () else ()
                   3 }

module RecordExpressionsWithStructuredRightHandSide2 =

    type r = { contents : int; name : string }
    let f3a() = 
      { name = "1" 
        contents = 
          printfn "hello"
          1 + 1 }

    let f3b() = 
      { name = "1" 
        contents = let y = 1
                   y + y }

    let f3c() = 
      { name = "1" 
        contents = while false do
                     ()
                   1 + 1 }

    let f3d() = 
      { name = "1" 
        contents = try 
                     3
                   with _ -> 
                     2 }

    let f3e() = 
      { name = "1" 
        contents = try 
                     3
                   finally () }

    let f3f() = 
      { name = "1" 
        contents = use x = { new System.IDisposable with member __.Dispose() = () }
                   3 }

    let f3g() = 
      { name = "1" 
        contents = if true then () else ()
                   3 }

module RecordExpressionsWithStructuredRightHandSide3 =

    type r = { contents : int; name : string }
    let f3a() = 
      { contents = 
          printfn "hello"
          1 + 1
        name = "1" }

    let f3b() = 
      { contents = let y = 1
                   y + y
        name = "1" }

    let f3c() = 
      { contents = while false do
                     ()
                   1 + 1 
        name = "1"}

    let f3d() = 
      { contents = try 
                     3
                   with _ -> 
                     2
        name = "1" }

    let f3e() = 
      { contents = try 
                     3
                   finally ()
        name = "1" }

    let f3f() = 
      { contents = use x = { new System.IDisposable with member __.Dispose() = () }
                   3
        name = "1" }

    let f3g() = 
      { contents = if true then () else ()
                   3 
        name = "1" }
  
module ActualReproAndVariations = 
     // problem?? See feat binding...

    type fields = { docId  : int64;
                    rating : int;
                    qid:     int;
                    feat:    float[];
                  }

    let mkFields1 (cols:string[]) =
      {
        docId  = cols.[0] |> int64;
        rating = cols.[1] |> int32;
        qid    = cols.[2] |> int32;
        feat   = let arr = Array.create 25 0.0
                 for i = 0 to 25 do
                   arr.[i] <- (cols.[i+3] |> float)
                 arr                          
      }

    let mkFields2 (cols:string[]) =
      {
        docId  = cols.[0] |> int64;
        rating = cols.[1] |> int32;
        feat   = let arr = Array.create 25 0.0
                 for i = 0 to 25 do
                   arr.[i] <- (cols.[i+3] |> float)
                 arr                          
        qid    = cols.[2] |> int32;
      }

    let mkFields3 (cols:string[]) =
      {
        docId  = cols.[0] |> int64;
        rating = cols.[1] |> int32;
        feat   = 
           let arr = Array.create 25 0.0
           for i = 0 to 25 do
             arr.[i] <- (cols.[i+3] |> float)
           arr                          
        qid    = cols.[2] |> int32;
      }

    let mkFields4 (cols:string[]) =
      {
        docId  = 
          cols.[0] 
          |> int64;
        rating = 
          cols.[1] 
          |> int32;
        feat   
         = 
           let arr = Array.create 25 0.0
           for i = 0 to 25 do
             arr.[i] <- (cols.[i+3] |> float)
           arr                          
        qid    = 
          cols.[2] 
          |> int32;
      }

module MiscRecordTEsts = 
    let _ = 
        { contents = 
            printfn "hello"
            1 + 1 }

    let rrrrrr = ref 1
    let _ = 
        { rrrrrr with 
           contents = 
            let x = 1
            x + x }

module AdHocTests = 
    let f4() = 
      { contents = fun _ ->
          printfn "hello"
          1 + 1 }


    let f6() = 
        let fffffffffffffffffffffffff x = x
        let g1 () = fffffffffffffffffffffffff (
                     3)
        let g1 () = async { 
            let x = 2 + 1
            return x
        }
        ()
        
    let f7() = 
        let r1 = ref 1
        { r1 with contents = match 1 with 1 -> 2 | _ -> 3 }



    let f9 = function
      | [] -> 0
      | _ -> 1 
    let f10() = 
        let y = ref 0
        y :=
            let (b, _, _) = (1,2,3)
            b      

    let f12() = 
        let y = 0
        y ::
            let (b, _, _) = (1,2,3)
            [b]      

    let f12b() = 
        let y = ref [0]
        y := 
            1 ::
                let (b, _, _) = (1,2,3)
                [b]      

    let f13() = 
        let y = ref [0]
        y := 
            1 ::
             let (b, _, _) = (1,2,3)
             [b]      

    let f14() = 
        let y = ref [0]
        y := 
         1 ::
          let (b, _, _) = (1,2,3)
          [b]      
    
    let f15() = 
        let y = ref [0]
        y 
         := 
           1 ::
            let (b, _, _) = (1,2,3)
            [b]      

    let f16() = 
        let y = ref [0]
        y 
         := 
           1 
            ::
              let (b, _, _) = (1,2,3)
              [b]      
    
#if TESTS_AS_APP
let RUN() = !failures
#else
let aa =
  match !failures with 
  | false -> 
      stdout.WriteLine "Test Passed"
      System.IO.File.WriteAllText("test.ok","ok")
      exit 0
  | _ -> 
      stdout.WriteLine "Test Failed"
      exit 1
#endif

