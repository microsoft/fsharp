// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.ErrorMessages

open Xunit
open FSharp.Test.Compiler

module ``Else branch has wrong type`` =

    [<Fact>]
    let ``Else branch is int while if branch is string``() =
        FSharp """
let test = 100
let y =
    if test > 10 then "test"
    else 123
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 1, Line 5, Col 10, Line 5, Col 13,
                                 "All branches of an 'if' expression must return values implicitly convertible to the type of the first branch, which here is 'string'. This branch returns a value of type 'int'.")

    [<Fact>]
    let ``Else branch is a function that returns int while if branch is string``() =
        FSharp """
let test = 100
let f x = test
let y =
    if test > 10 then "test"
    else f 10
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 1, Line 6, Col 10, Line 6, Col 14,
                                 "All branches of an 'if' expression must return values implicitly convertible to the type of the first branch, which here is 'string'. This branch returns a value of type 'int'.")


    [<Fact>]
    let ``Else branch is a sequence of expressions that returns int while if branch is string``() =
        FSharp """
let f x = x + 4

let y =
    if true then
        ""
    else
        "" |> ignore
        (f 5)
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 1, Line 9, Col 10, Line 9, Col 13,
                                 "All branches of an 'if' expression must return values implicitly convertible to the type of the first branch, which here is 'string'. This branch returns a value of type 'int'.")


    [<Fact>]
    let ``Else branch is a longer sequence of expressions that returns int while if branch is string``() =
        FSharp """
let f x = x + 4

let y =
    if true then
        ""
    else
        "" |> ignore
        let z = f 4
        let a = 3 * z
        (f a)
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 1, Line 11, Col 10, Line 11, Col 13,
                                 "All branches of an 'if' expression must return values implicitly convertible to the type of the first branch, which here is 'string'. This branch returns a value of type 'int'.")


    [<Fact>]
    let ``Else branch context doesn't propagate into function application``() =
        FSharp """
let test = 100
let f x : string = x
let y =
    if test > 10 then "test"
    else
        f 123
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 1, Line 7, Col 11, Line 7, Col 14,
                                 "This expression was expected to have type\n    'string'    \nbut here has type\n    'int'    ")

    [<Fact>]
    let ``Else branch context doesn't propagate into function application even if not last expr``() =
        FSharp """
let test = 100
let f x = printfn "%s" x
let y =
    if test > 10 then "test"
    else
        f 123
        "test"
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 1, Line 7, Col 11, Line 7, Col 14,
                                 "This expression was expected to have type\n    'string'    \nbut here has type\n    'int'    ")

    [<Fact>]
    let ``Else branch context doesn't propagate into for loop``() =
        FSharp """
let test = 100
let list = [1..10]
let y =
    if test > 10 then "test"
    else
        for (x:string) in list do
            printfn "%s" x

        "test"
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 1, Line 7, Col 14, Line 7, Col 22,
                                 "This expression was expected to have type\n    'int'    \nbut here has type\n    'string'    ")

    [<Fact>]
    let ``Else branch context doesn't propagate to lines before last line``() =
        FSharp """
let test = 100
let list = [1..10]
let y =
    if test > 10 then "test"
    else
        printfn "%s" 1

        "test"
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 1, Line 7, Col 22, Line 7, Col 23,
                                 "This expression was expected to have type\n    'string'    \nbut here has type\n    'int'    ")

    [<Fact>]
    let ``Else branch should not have wrong context type``() =
        FSharp """
let x = 1
let y : bool =
    if x = 2 then "A"
    else "B"
        """
        |> typecheck
        |> shouldFail
        |> withDiagnostics [
            (Error 1, Line 4, Col 19, Line 4, Col 22, "The 'if' expression needs to have type 'bool' to satisfy context type requirements. It currently has type 'string'.")
            (Error 1, Line 5, Col 10, Line 5, Col 13, "All branches of an 'if' expression must return values implicitly convertible to the type of the first branch, which here is 'bool'. This branch returns a value of type 'string'.")]


    [<Fact>]
    let ``Else branch has wrong type in nested if``() =
        FSharp """
let x = 1
if x = 1 then true
else
    if x = 2 then "A"
    else "B"
        """
        |> typecheck
        |> shouldFail
        |> withDiagnostics [
            (Error   1,  Line 5, Col 19, Line 5, Col 22, "All branches of an 'if' expression must return values implicitly convertible to the type of the first branch, which here is 'bool'. This branch returns a value of type 'string'.")
            (Error   1,  Line 6, Col 10, Line 6, Col 13, "All branches of an 'if' expression must return values implicitly convertible to the type of the first branch, which here is 'bool'. This branch returns a value of type 'string'.")
            (Warning 20, Line 3, Col 1,  Line 6, Col 13, "The result of this expression has type 'bool' and is implicitly ignored. Consider using 'ignore' to discard this value explicitly, e.g. 'expr |> ignore', or 'let' to bind the result to a name, e.g. 'let result = expr'.")]
