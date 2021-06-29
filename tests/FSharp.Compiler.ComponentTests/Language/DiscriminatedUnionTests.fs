// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.Language

open Xunit
open FSharp.Test.Utilities.Compiler
open FSharp.Quotations.Patterns

module DiscriminatedUnionTests =

    [<Fact>]
    let ``Simple Is* discriminated union properties are visible, proper values are returned`` () =
        Fsx """
type Foo = private | Foo of string | Bar
let foo = Foo.Foo "hi"
if not <| foo.IsFoo then failwith "Should be Foo"
if foo.IsBar then failwith "Should not be Bar"
        """
        |> withLangVersionPreview
        |> compileExeAndRun
        |> shouldSucceed

    [<Fact>]
    let ``Is* discriminated union properties are visible, proper values are returned in recursive namespace, before the definition`` () =
        FSharp """
namespace rec Hello

module Main =
    [<EntryPoint>]
    let main _ =
        let foo = Foo.Foo "hi"
        printf $"IsFoo: %b{foo.IsFoo} / IsBar: %b{foo.IsBar}"
        0

    [<Struct>]
    type Foo =
        private
        | Foo of string
        | Bar
        """
        |> withLangVersionPreview
        |> compileExeAndRun
        |> shouldSucceed