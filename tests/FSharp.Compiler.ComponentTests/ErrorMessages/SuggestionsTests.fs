// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.ErrorMessages

open Xunit
open FSharp.Test.Utilities.Compiler

module Suggestions =

    [<Fact>]
    let ``Field Suggestion`` () =
        FSharp """
type Person = { Name : string; }

let x = { Person.Names = "Isaac" }
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 39, Line 4, Col 18, Line 4, Col 23,
                                 ("The type 'Person' does not define the field, constructor or member 'Names'. Maybe you want one of the following:" + System.Environment.NewLine + "   Name"))

    [<Fact>]
    let ``Suggest Array Module Functions`` () =
        FSharp "let f = Array.blt"
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 39, Line 1, Col 15, Line 1, Col 18,
                                 ("The value, constructor, namespace or type 'blt' is not defined. Maybe you want one of the following:" + System.Environment.NewLine + "   blit"))

    [<Fact>]
    let ``Suggest Async Module`` () =
        FSharp "let f = Asnc.Sleep 1000"
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic( Error 39, Line 1, Col 9, Line 1, Col 13,
                                 ("The value, namespace, type or module 'Asnc' is not defined. Maybe you want one of the following:" + System.Environment.NewLine + "   Async" + System.Environment.NewLine + "   async" + System.Environment.NewLine + "   asin" + System.Environment.NewLine + "   snd"))

    [<Fact>]
    let ``Suggest Attribute`` () =
        FSharp """
[<AbstractClas>]
type MyClass<'Bar>() =
   abstract M<'T> : 'T -> 'T
   abstract M2<'T> : 'T -> 'Bar
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 39, Line 2, Col 3, Line 2, Col 15,
                                 ("The type 'AbstractClas' is not defined. Maybe you want one of the following:" + System.Environment.NewLine + "   AbstractClass" + System.Environment.NewLine + "   AbstractClassAttribute"))

    [<Fact>]
    let ``Suggest Double Backtick Identifiers`` () =
        FSharp """
module N =
    let ``longer name`` = "hallo"

let x = N.``longe name``
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 39, Line 5, Col 11, Line 5, Col 25,
                                 ("The value, constructor, namespace or type 'longe name' is not defined. Maybe you want one of the following:" + System.Environment.NewLine + "   longer name"))

    [<Fact>]
    let ``Suggest Double Backtick Unions`` () =
        FSharp """
module N =
    type MyUnion =
    | ``My Case1``
    | Case2

open N

let x = N.MyUnion.``My Case2``
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 39, Line 9, Col 19, Line 9,Col 31,
                                 ("The type 'MyUnion' does not define the field, constructor or member 'My Case2'. Maybe you want one of the following:" + System.Environment.NewLine + "   My Case1" + System.Environment.NewLine + "   Case2"))


    [<Fact>]
    let ``Suggest Fields In Constructor`` () =
        FSharp """
type MyClass() =
    member val MyProperty = "" with get, set
    member val MyProperty2 = "" with get, set
    member val ABigProperty = "" with get, set

let c = MyClass(Property = "")
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 495, Line 7, Col 17, Line 7, Col 25,
                                 ("The object constructor 'MyClass' has no argument or settable return property 'Property'. The required signature is new : unit -> MyClass. Maybe you want one of the following:" + System.Environment.NewLine + "   MyProperty" + System.Environment.NewLine + "   MyProperty2" + System.Environment.NewLine + "   ABigProperty"))

    [<Fact>]
    let ``Suggest Generic Type`` () =
        FSharp """
type T = System.Collections.Generic.Dictionary<int11,int>
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 39, Line 2, Col 48, Line 2, Col 53,
                                 ("The type 'int11' is not defined. Maybe you want one of the following:" + System.Environment.NewLine + "   int16" + System.Environment.NewLine + "   int16`1" + System.Environment.NewLine + "   int8" + System.Environment.NewLine + "   uint16" + System.Environment.NewLine + "   int"))

    [<Fact>]
    let ``Suggest Methods`` () =
        FSharp """
module Test2 =
    type D() =

       static let x = 1

       member x.Method1() = 10

    D.Method2()
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 39, Line 9, Col 7, Line 9, Col 14,
                                 ("The type 'D' does not define the field, constructor or member 'Method2'. Maybe you want one of the following:" + System.Environment.NewLine + "   Method1"))

    [<Fact>]
    let ``Suggest Modules`` () =
        FSharp """
module Collections =

    let f () = printfn "%s" "Hello"

open Collectons
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 39, Line 6, Col 6, Line 6, Col 16,
                                 ("The namespace or module 'Collectons' is not defined. Maybe you want one of the following:" + System.Environment.NewLine + "   Collections"))

    [<Fact>]
    let ``Suggest Namespaces`` () =
        FSharp """
open System.Collectons
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 39, Line 2, Col 13, Line 2, Col 23,
                                 "The namespace 'Collectons' is not defined.")

    [<Fact>]
    let ``Suggest Record Labels`` () =
        FSharp """
type MyRecord = { Hello: int; World: bool}

let r = { Hello = 2 ; World = true}

let x = r.ello
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 39, Line 6, Col 11, Line 6, Col 15,
                                 ("The type 'MyRecord' does not define the field, constructor or member 'ello'. Maybe you want one of the following:" + System.Environment.NewLine + "   Hello"))

    [<Fact>]
    let ``Suggest Record Type for RequireQualifiedAccess Records`` () =
        FSharp """
[<RequireQualifiedAccess>]
type MyRecord = {
    Field1: string
    Field2: int
}

let r = { Field1 = "hallo"; Field2 = 1 }
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 39, Line 8, Col 11, Line 8, Col 17,
                                 ("The record label 'Field1' is not defined. Maybe you want one of the following:" + System.Environment.NewLine + "   MyRecord.Field1"))

    [<Fact>]
    let ``Suggest To Use Indexer`` () =
        FSharp """
let d = [1,1] |> dict
let y = d[1]

let z = d[|1|]

let f() = d
let a = (f())[1]
        """
        |> typecheck
        |> shouldFail
        |> withDiagnostics [
            (Error 3217, Line 3, Col 9,  Line 3, Col 10, "This value is not a function and cannot be applied. Did you intend to access the indexer via d.[index] instead?")
            (Error 3,    Line 5, Col 9,  Line 5, Col 10, "This value is not a function and cannot be applied.")
            (Error 3217, Line 8, Col 10, Line 8, Col 13, "This expression is not a function and cannot be applied. Did you intend to access the indexer via expr.[index] instead?")]


    [<Fact>]
    let ``Suggest Type Parameters`` () =
        FSharp """
[<AbstractClass>]
type MyClass<'Bar>() =
   abstract M<'T> : 'T -> 'T
   abstract M2<'T> : 'T -> 'Bar
   abstract M3<'T> : 'T -> 'B
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 39, Line 6, Col 28, Line 6, Col 30,
                                 "The type parameter 'B is not defined.")


    [<Fact>]
    let ``Suggest Types in Module`` () =
        FSharp """
let x : System.Collections.Generic.Lst = ResizeArray()
        """         |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 39, Line 2, Col 36, Line 2, Col 39,
                                 ("The type 'Lst' is not defined in 'System.Collections.Generic'. Maybe you want one of the following:" + System.Environment.NewLine + "   List" + System.Environment.NewLine + "   IList" + System.Environment.NewLine + "   List`1"))

    [<Fact>]
    let ``Suggest Types in Namespace`` () =
        FSharp """
let x = System.DateTie.MaxValue
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 39, Line 2, Col 16, Line 2, Col 23,
                                 ("The value, constructor, namespace or type 'DateTie' is not defined. Maybe you want one of the following:" + System.Environment.NewLine + "   DateTime" + System.Environment.NewLine + "   DateTimeKind" + System.Environment.NewLine + "   DateTimeOffset" + System.Environment.NewLine + "   Data"))

    [<Fact>]
    let ``Suggest Union Cases`` () =
        FSharp """
type MyUnion =
| ASimpleCase
| AnotherCase of int

let u = MyUnion.AntherCase
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 39, Line 6, Col 17, Line 6, Col 27,
                                 ("The type 'MyUnion' does not define the field, constructor or member 'AntherCase'. Maybe you want one of the following:" + System.Environment.NewLine + "   AnotherCase"))

    [<Fact>]
    let ``Suggest Union Type for RequireQualifiedAccess Unions`` () =
        FSharp """
[<RequireQualifiedAccess>]
type MyUnion =
| MyCase1
| MyCase2 of string

let x : MyUnion = MyCase1
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 39, Line 7, Col 19, Line 7, Col 26,
                                 ("The value or constructor 'MyCase1' is not defined. Maybe you want one of the following:" + System.Environment.NewLine + "   MyUnion.MyCase1"))


    [<Fact>]
    let ``Suggest Unions in PatternMatch`` () =
        FSharp """
[<RequireQualifiedAccess>]
type MyUnion =
| Case1
| Case2

let y = MyUnion.Case1

let x =
    match y with
    | MyUnion.Cas1 -> 1
    | _ -> 2
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 39, Line 11, Col 15, Line 11, Col 19,
                                 ("The type 'MyUnion' does not define the field, constructor or member 'Cas1'. Maybe you want one of the following:" + System.Environment.NewLine + "   Case1" + System.Environment.NewLine + "   Case2"))
