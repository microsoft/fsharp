
#r "System.Xml.Linq.dll"
#r "System.Xml.XDocument.dll"

let mutable failures : string list = []

open System

module BasicTypeDirectedConversionsToObj =
    // Constant expression 
    let x1 : obj = 1

    // Field literal expressions
    let x2 : obj = System.Int32.MaxValue

    // Field literal expressions
    let x3 : obj = System.Int32.Parse("12")

    // Tuple expressions
    let x4 : obj = (1,2)

    // Arithmetic expressions
    //  These do NOT permit type-directed subsumption nor widening because a generic return type is involved
    //  the is not yet committed
    // let x : obj = 1 + 1

    // Values
    let x8 (s:string) : obj = s

module BasicTypeDirectedConversionsToTuple =

    // Tuple expressions
    let x2 : obj * obj = (1,2)

    // Let expressions
    let x3 : (obj * obj) = (let x = 1 in let y = 2 in (x,y))

    // Struct tuple expressions
    let x4 : struct (obj * obj) = struct (1,2)

module BasicTypeDirectedConversionsForFuncReturn =

    // Return in lambda expressions
    let x5 : (unit -> obj) = (fun () -> 1)
    let x6 : (unit -> obj * obj) = (fun () -> (1,2))

    // Return in function expressions
    let x7 () : obj * obj = (1,2)

type R = { mutable F1: (obj * obj) }
[<Struct>]
type SR = { mutable SF1: (obj * obj) }
type U = UnionCase0 | UnionCase1 of int
[<Struct>]
type SU = StructUnionCase0 | StructUnionCase1 of int
module IntegerWidening =
    let i1 = 0
    let x0 : int64 = i1 // integer value
    let x1 : int64 = 0 // integer constant
    let x2 : int64 list = [1;2;3;4] // within a list
    let x3 : float list = [1;2;3;4]
    let x4 : float32 list = [1;2;3;4]
    let x5 : int64 = System.Int32.MaxValue
    let x6 : int64 list = [System.Int32.MaxValue;System.Int32.MaxValue]
    let f () = 1

    let x7 : obj = f()
    let x8 : int64 = f()
    let x9 : int64 = id (f()) // generic does work 

    // Arithmetic expressions
    //  These do NOT permit type-directed subsumption nor widening because a generic return type is involved
    //  the is not yet committed
    // let x6 : int64 = 1 + 1
    // let x6 : int64 = id (1 + 1)

module Overloads1 =
    type C() = 
        static member M1(x:int) = 1
        static member M1(x:int64) = failwith "nope"
    let x = C.M1(2)

module Overloads2 =
    type C() = 
        static member M1(x:int) = failwith "nope"
        static member M1(x:int64) = printfn "ok at line %s" __LINE__
    let x = C.M1(2L)

module Overloads3 =
    type C() = 
        static member M1(x:string) = failwith "nope"
        static member M1(x:int64) = printfn "ok at line %s" __LINE__
    let x = C.M1(2)

module OverloadedOptionals1 =
    type C() = 
        static member M1(x:string) = failwith "nope"
        static member M1(?x:int64) = printfn "ok at line %s" __LINE__
    let x = C.M1(x=2)

module OverloadedOptionals2 =
    type C() = 
        static member M1(?x:int) = printfn "ok at line %s" __LINE__
        static member M1(?x:int64) = failwith "nope"
    let x = C.M1(x=2)

module OverloadedOptionals3 =
    type C() = 
        static member M1(?x:int) =  failwith "nope"
        static member M1(?x:int64) =  printfn "ok at line %s" __LINE__
    let x = C.M1(x=2L)

module Optionals1 =
    type C() = 
        static member M1(?x:int64) = 1
    let x = C.M1(x=2)

module ParamArray1 =
    type C() = 
        static member M1([<System.ParamArray>] x:int64[]) = Array.sum x
    let x1 = C.M1(2)
    let x2 = C.M1(2, 3, 5L)

module ParamArray2 =
    type C() = 
        static member M1([<System.ParamArray>] x:double[]) = Array.sum x
    let x1 = C.M1(2)
    let x2 = C.M1(2, 3, 5.0)

module ConvertToSealedViaOpImplicit =
    [<Sealed>]
    type C() = 
        static member op_Implicit(x:int) = C()
        static member M1(C:C) = 1
    let x = C.M1(2)

module ConvertNoOverloadin =
    type C() = 
        static member M1(x:int64) = 1
    let x = C.M1(2)

module ConvertNoOverloadingViaOpImplicit =
    type C() = 
        static member M1(x:decimal) = ()
    let x = C.M1(2)

let d: decimal = 3

let ns : System.Xml.Linq.XNamespace = ""

module ConvertViaOpImplicit2 =
    type C() = 
        static member M1(ns:System.Xml.Linq.XNamespace) = 1
    let x = C.M1("")

module ConvertViaOpImplicit3 =
    type C() = 
        static member M1(ns:System.Xml.Linq.XName) = 1
    let x = C.M1("a")

module ConvertViaOpImplicit4 =
    type C() = 
        static member op_Implicit(x:int) = C()
        static member M1(C:C) = 1
    let x = C.M1(2)

module ConvertViaOpImplicit5 =
    type X() = 
        static member M1(x:X) = 1
    type Y() =
        static member op_Implicit(y:Y) = X()
    let x = X.M1(Y())

module ConvertViaOpImplicitGeneric =
    type C<'T>() = 
        static member op_Implicit(x: 'T) = C<'T>()
    
    let c:C<int> = 1
    let f (c:C<int>) = 1
    let x = f 2

module ConvertViaNullable =
    type C<'T>() = 
        static member op_Implicit(x: 'T) = C<'T>()
    
    let c:Nullable<int> = 1
    let f (c:Nullable<int>) = 1
    let x = f 2

let annotations = 
    let v1 : obj = (1,2)
    let v2 : obj * obj = (1,2)

    // structure through let
    let v3 : (obj * obj) = (let x = 1 in let y = 2 in (x,y))

    // structure through let rec
    let v4 : (obj * obj) = (let rec f x = x in (3,4.0))

    // structure through sequence
    let v5 : (obj * obj) = (); (3,4.0)

    // struct tuple
    let v6 : struct (obj * obj) = struct (1,2)
    
    // record (both field and overall result)
    let v7 : obj = { F1 = (1, 2) }
    
    // record 
    let v7a : obj = ({ F1 = (1, 2) } : R)
    
    // struct record (both field and overall result)
    let v8 : obj = { SF1 = (1, 2) }
    
    // struct record (both field and overall result)
    let v8a : obj = ({ SF1 = (1, 2) } : SR)
    
    // union
    let v9 : obj = UnionCase1(3)
    
    // union
    let v10 : obj = UnionCase0
    
    // union as first-class
    let v11 : obj = UnionCase1
    
    // struct union
    let v12 : obj = StructUnionCase1(3)
    
    // struct union
    let v13 : obj = StructUnionCase0
    
    // struct union as first-class
    let v14 : obj = StructUnionCase1
    
    // record (both field and overall result)
    { F1 = (1, 2uy) }.F1 <- (3.0, 4)
    
    // anon record
    let v15 : {| A: obj |} = {| A = 1 |}
    
    // lambda return
    let v16 : (unit -> obj) = (fun () -> 1)

    // function lambda return
    let v17 : (int -> obj) = (function 1 -> 1 | 2 -> 3.0 | _ -> 4uy)

    let v18 : (unit -> obj * obj) = (fun () -> (1,2))

    // constants
    (1 :> System.IComparable) |> ignore

    let v19 : System.IComparable = 1 

    // array constants
    let v20 : System.Array = [| 1us |] 

    let v21 : System.Array = [| 1I |] 

    let v22 : System.IComparable = 1I
    
    // property
    let v23 : System.IComparable<string> = System.String.Empty 
    
    // method
    let v24 : System.IComparable<string> = System.String.Format("")

    let v25 : obj = System.String.Format("")

    let v26 : System.IComparable = System.String.Format("")
    
    // array constants

    let v27 : obj[] = [| 1 |] 
    let v28 : (obj * obj)[] = [| (1,1) |]
    let v29 : (obj * obj)[] = [| ("abc",1) |]
    let v30 : (string * obj)[] = [| ("abc",1) |]
    let v31 : (string * obj)[] = [| ("abc",1); ("abc",3.0) |]
    let v32 : (string * obj)[] = [| Unchecked.defaultof<_>; ("abc",3.0) |]
    let v33 : struct (string * obj)[] = [| Unchecked.defaultof<_>; struct ("abc",3.0) |]

    let v34 : obj = 1
    let v35 : obj = (1 : int)
    let v36 : obj = ""
    let v37 : obj = ("" : string)
    let v38 : obj = ("" : System.IComparable)
    let v39 : obj = ("" : _)
    let v40 : obj = ("" : obj)
    let v41 : obj = { new System.ICloneable with member x.Clone() = obj() }
    let v42 : obj = ""
    let v43 : obj = string ""
    let v44 : obj = id ""
    
    // conditional
    let v45 : obj = if true then 1 else 3.0
    let v46 : obj = (if true then 1 else 3.0)
    let v47 : obj = (if true then 1 elif true then 2uy else 3.0) 
    
    // try-with
    let v48 : obj = try 1 with _ -> 3.0
    
    // try-finally
    let v49 : obj = try 1 finally ()

    // match
    let v50 : obj = match true with true -> 1 | _ -> 3.0
    ()

let f1 () : obj = 1
let f2 () : obj = if true then 1 else 3.0

module TestComputedListExpressionsAtList = 
    let x1 : list<int64>  = [ yield 1 ]
    let x2 : list<int64>  = [ yield 1;
                              if true then yield 2L ]
    let x3 : list<int64>  = [ yield 1L;
                              if true then yield 2 ]
    let x4 : list<int64>  = [ yield 1L;
                              while false do yield 2 ]
    let x5 : list<int64>  = [ yield 1;
                              while false do yield 2L ]
    let x6 : list<int64>  = [ while false do yield 2L ]
    let x7 : list<int64>  = [ for i in 0 .. 10 do yield 2 ]
    let x8 : list<int64>  = [ yield 1L
                              for i in 0 .. 10 do yield 2 ]
    let x9 : list<int64>  = [ yield 1
                              for i in 0 .. 10 do yield 2L ]
    let x10 : list<int64>  = [ try yield 2 finally () ]
    let x11 : list<int64>  = [ yield 1L
                               try yield 2 finally ()  ]
    let x12 : list<int64>  = [ yield 1
                               try yield 2L finally ()  ]

module TestComputedListExpressionsAtSeq = 
    let x1 : seq<int64>  = [ yield 1 ]
    let x2 : seq<int64>  = 
        [ yield 1;
          if true then yield 2L ]
    let x3 : seq<int64>  = 
        [ yield 1L;
          if true then yield 2 ]
    let x4 : seq<int64>  = 
        [ yield 1L;
          while false do yield 2 ]
    let x5 : seq<int64>  = 
        [ yield 1;
          while false do yield 2L ]
    let x6 : seq<int64>  = 
        [ while false do yield 2L ]
    let x7 : seq<int64>  = 
        [ for i in 0 .. 10 do yield 2 ]
    let x8 : seq<int64>  = 
        [ yield 1L
          for i in 0 .. 10 do yield 2 ]
    let x9 : seq<int64>  = 
        [ yield 1
          for i in 0 .. 10 do yield 2L ]
    let x10 : seq<int64>  = 
        [ try yield 2 finally () ]
    let x11 : seq<int64>  = 
        [ yield 1L
          try yield 2 finally ()  ]
    let x12 : seq<int64>  = 
        [ yield 1
          try yield 2L finally ()  ]

module TestComputedArrayExpressionsAtArray = 
    let x1 : array<int64>  = [| yield 1 |]
    let x2 : array<int64>  = [| yield 1;
                                if true then yield 2L |]
    let x3 : array<int64>  = [| yield 1L;
                                if true then yield 2 |]
    let x4 : array<int64>  = [| yield 1L;
                                while false do yield 2 |]
    let x5 : array<int64>  = [| yield 1;
                                while false do yield 2L |]
    let x6 : array<int64>  = [| while false do yield 2L |]
    let x7 : array<int64>  = [| for i in 0 .. 10 do yield 2 |]
    let x8 : array<int64>  = [| yield 1L
                                for i in 0 .. 10 do yield 2 |]
    let x9 : array<int64>  = [| yield 1
                                for i in 0 .. 10 do yield 2L |]
    let x10 : array<int64>  = [| try yield 2 finally () |]
    let x11 : array<int64>  = [| yield 1L
                                 try yield 2 finally ()  |]
    let x12 : array<int64>  = [| yield 1
                                 try yield 2L finally ()  |]

module TestComputedArrayExpressionsAtSeq = 
    let x1 : seq<int64>  = [| yield 1 |]
    let x2 :   seq<int64>  = [| yield 1;
                                if true then yield 2L |]
    let x3 :   seq<int64>  = [| yield 1L;
                                if true then yield 2 |]
    let x4 :   seq<int64>  = [| yield 1L;
                                while false do yield 2 |]
    let x5 :   seq<int64>  = [| yield 1;
                                while false do yield 2L |]
    let x6 :   seq<int64>  = [| while false do yield 2L |]
    let x7 :   seq<int64>  = [| for i in 0 .. 10 do yield 2 |]
    let x8 :   seq<int64>  = [| yield 1L
                                for i in 0 .. 10 do yield 2 |]
    let x9 :   seq<int64>  = [| yield 1
                                for i in 0 .. 10 do yield 2L |]
    let x10 :   seq<int64>  = [| try yield 2 finally () |]
    let x11 :   seq<int64>  = [| yield 1L
                                 try yield 2 finally ()  |]
    let x12 :   seq<int64>  = [| yield 1
                                 try yield 2L finally ()  |]

module TestInferObjExprTypeParamFromKNownType = 
    // Check we are inferring type int64
    let x1 : seq<int64>  = 
         { new seq<_> with 
              member x.GetEnumerator() = 
                  // The 'ToString("4")' would not resolve if the type parameter is not inferred by this point
                  x.GetEnumerator().Current.ToString("4")  |> ignore<string>
                  failwith ""

           interface System.Collections.IEnumerable with 
              member x.GetEnumerator() = failwith ""
              }

    type OtherSeq<'T> =
        inherit seq<'T>

    // Check we are inferring type int64
    let x2 : seq<int64>  = 
         { new OtherSeq<_> with 
              member x.GetEnumerator() = 
                  // The 'ToString("4")' would not resolve if the type parameter is not inferred by this point
                  x.GetEnumerator().Current.ToString("4")  |> ignore<string>
                  failwith ""

           interface System.Collections.IEnumerable with 
              member x.GetEnumerator() = failwith ""
              }

    type OtherSeqImpl<'T>(f : 'T -> unit) =
        interface OtherSeq<'T> with 
              member x.GetEnumerator() =
                  failwith ""

        interface System.Collections.IEnumerable with 
            member x.GetEnumerator() = failwith ""

    let x3 : seq<int64>  = 
        new OtherSeqImpl<_>(fun x -> 
             // The 'ToString("4")' would not resolve if the type parameter is not inferred to be int64 by this point
             x.ToString("4")  |> ignore<string>)

    let x4 : int64 * int32  = (3, 3)

    let x5 : {| A: int64; B: int32 |} = {| A=3; B=3 |}

printfn "test done"

let aa =
  match failures with 
  | [] -> 
      stdout.WriteLine "Test Passed"
      System.IO.File.WriteAllText("test.ok","ok")
      exit 0
  | _ -> 
      printfn "Test Failed, failures = %A" failures
      exit 1

