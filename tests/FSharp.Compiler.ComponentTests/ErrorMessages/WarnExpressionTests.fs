// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.ErrorMessages

open Xunit
open FSharp.Test.Utilities.Compiler


module ``Warn Expression`` =

    [<Fact>]
    let ``Warn If Expression Result Unused``() =
        FSharp """
1 + 2
printfn "%d" 3
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Warning 20, Line 2, Col 1, Line 2, Col 6,
                                 "The result of this expression has type 'int' and is implicitly ignored. Consider using 'ignore' to discard this value explicitly, e.g. 'expr |> ignore', or 'let' to bind the result to a name, e.g. 'let result = expr'.")

    [<Fact>]
    let ``Warn If Possible Assignment``() =
        FSharp """
let x = 10
let y = "hello"

let changeX() =
    x = 20
    y = "test"
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Warning 20, Line 6, Col 5, Line 6, Col 11,
                                 "The result of this equality expression has type 'bool' and is implicitly discarded. Consider using 'let' to bind the result to a name, e.g. 'let result = expression'. If you intended to mutate a value, then mark the value 'mutable' and use the '<-' operator e.g. 'x <- expression'.")

    [<Fact>]
    let ``Warn If Possible Assignment To Mutable``() =
        FSharp """
let mutable x = 10
let y = "hello"

let changeX() =
    x = 20
    y = "test"
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Warning 20, Line 6, Col 5, Line 6, Col 11,
                                 "The result of this equality expression has type 'bool' and is implicitly discarded. Consider using 'let' to bind the result to a name, e.g. 'let result = expression'. If you intended to mutate a value, then use the '<-' operator e.g. 'x <- expression'.")

    [<Fact>]
    let ``Warn If Possible dotnet Property Setter``() =
        FSharp """
open System

let z = System.Timers.Timer()
let y = "hello"

let changeProperty() =
    z.Enabled = true
    y = "test"
        """
        |> typecheck
        |> shouldFail
        |> withDiagnostics [
            (Warning 760, Line 4, Col 9, Line 4, Col 30, "It is recommended that objects supporting the IDisposable interface are created using the syntax 'new Type(args)', rather than 'Type(args)' or 'Type' as a function value representing the constructor, to indicate that resources may be owned by the generated value")
            (Warning 20,  Line 8, Col 5, Line 8, Col 21, "The result of this equality expression has type 'bool' and is implicitly discarded. Consider using 'let' to bind the result to a name, e.g. 'let result = expression'. If you intended to set a value to a property, then use the '<-' operator e.g. 'z.Enabled <- expression'.")]

    [<Fact>]
    let ``Don't Warn If Property Without Setter``() =
        FSharp """
type MyClass(_property1 : int) =
    member val Property2 = "" with get

let x = MyClass(1)
let y = "hello"

let changeProperty() =
    x.Property2 = "22"
    y = "test"
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Warning 20, Line 9, Col 5, Line 9, Col 23,
                                 "The result of this equality expression has type 'bool' and is implicitly discarded. Consider using 'let' to bind the result to a name, e.g. 'let result = expression'.")

    [<Fact>]
    let ``Warn If Implicitly Discarded``() =
        FSharp """
let x = 10
let y = 20

let changeX() =
    y * x = 20
    y = 30
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Warning 20, Line 6, Col 5, Line 6, Col 15,
                                 "The result of this equality expression has type 'bool' and is implicitly discarded. Consider using 'let' to bind the result to a name, e.g. 'let result = expression'.")

    [<Fact>]
    let ``Warn If Discarded In List``() =
        FSharp """
let div _ _ = 1
let subView _ _ = [1; 2]

// elmish view
let view model dispatch =
   [
       yield! subView model dispatch
       div [] []
   ]
        """
        |> withLangVersion46
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Warning 3221, Line 9, Col 8, Line 9, Col 17,
                                 "This expression returns a value of type 'int' but is implicitly discarded. Consider using 'let' to bind the result to a name, e.g. 'let result = expression'. If you intended to use the expression as a value in the sequence then use an explicit 'yield'.")

    [<Fact>]
    let ``Warn If Discarded In List 2``() =
        FSharp """
// stupid things to make the sample compile
let div _ _ = 1
let subView _ _ = [1; 2]
let y = 1

// elmish view
let view model dispatch =
   [
        div [] [
           match y with
           | 1 -> yield! subView model dispatch
           | _ -> subView model dispatch
        ]
   ]
        """
        |> withLangVersion46
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Warning 3222, Line 13, Col 19, Line 13, Col 41,
                                 "This expression returns a value of type 'int list' but is implicitly discarded. Consider using 'let' to bind the result to a name, e.g. 'let result = expression'. If you intended to use the expression as a value in the sequence then use an explicit 'yield!'.")

    [<Fact>]
    let ``Warn If Discarded In List 3``() =
        FSharp """
// stupid things to make the sample compile
let div _ _ = 1
let subView _ _ = true
let y = 1

// elmish view
let view model dispatch =
   [
        div [] [
           match y with
           | 1 -> ()
           | _ -> subView model dispatch
        ]
   ]
        """
        |> withLangVersion46
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Warning 20, Line 13, Col 19, Line 13, Col 41,
                                 "The result of this expression has type 'bool' and is implicitly ignored. Consider using 'ignore' to discard this value explicitly, e.g. 'expr |> ignore', or 'let' to bind the result to a name, e.g. 'let result = expr'.")

    [<Fact>]
    let ``Warn Only On Last Expression``() =
        FSharp """
let mutable x = 0
while x < 1 do
    printfn "unneeded"
    x <- x + 1
    true
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Warning 20, Line 6, Col 5, Line 6, Col 9,
                                 "The result of this expression has type 'bool' and is implicitly ignored. Consider using 'ignore' to discard this value explicitly, e.g. 'expr |> ignore', or 'let' to bind the result to a name, e.g. 'let result = expr'.")

    [<Fact>]
    let ``Warn If Possible Property Setter``() =
        FSharp """
type MyClass(property1 : int) =
    member val Property1 = property1
    member val Property2 = "" with get, set

let x = MyClass(1)
let y = "hello"

let changeProperty() =
    x.Property2 = "20"
    y = "test"
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Warning 20, Line 10, Col 5, Line 10, Col 23,
                                 "The result of this equality expression has type 'bool' and is implicitly discarded. Consider using 'let' to bind the result to a name, e.g. 'let result = expression'. If you intended to set a value to a property, then use the '<-' operator e.g. 'x.Property2 <- expression'.")


    [<Fact>]
    let ``Dont warn external function as unused``() =
        FSharp """
open System
open System.Runtime.InteropServices

module Test =

    [<DllImport("shell32.dll", CharSet=CharSet.Auto)>]
    extern int32 ExtractIconEx(string szFileName, int nIconIndex,IntPtr[] phiconLarge, IntPtr[] phiconSmall,uint32 nIcons)

    [<DllImport("user32.dll", EntryPoint="DestroyIcon", SetLastError=true)>]
    extern int DestroyIcon(IntPtr hIcon)

[<EntryPoint>]
let main _argv =
    let _ = Test.DestroyIcon IntPtr.Zero

    let _ = Test.ExtractIconEx("", 0, [| |], [| |], 0u)

    0
        """
        |> typecheck
        |> shouldSucceed
