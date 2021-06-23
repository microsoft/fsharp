// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.Language

open Xunit
open FSharp.Test.Utilities.Compiler
open FSharp.Quotations.Patterns

module DiscriminatedUnionTests =

    [<Fact>]
    let ``Is* discriminated union properties are visible`` () =
        FSharp """
namespace rec Hello

[<Struct>]
type Foo =
    private
    | Foo of string
    | Bar

module Main =
    [<EntryPoint>]
    let main _ =
        let foo = Foo.Foo "hi"
        printfn "IsFoo: %b / IsBar: %b" foo.IsFoo foo.IsBar
        0
        """
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "IsFoo: true / IsBar: false"
