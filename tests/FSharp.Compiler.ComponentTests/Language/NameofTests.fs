// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.Language

open Xunit
open FSharp.Test.Utilities.Compiler

module NameofTests =

    [<Theory>]
    [<InlineData("+")>]
    [<InlineData("-")>]
    [<InlineData("/")>]
    [<InlineData("*")>]
    let ``nameof() with operator should return demangled name`` operator =
        let source = $"""
let expected = "{operator}"
let actual = nameof({operator})
if actual <> expected then failwith $"Expected nameof({{expected}}) to be '{{expected}}', but got '{{actual}}'"
        """
        Fsx source
        |> asExe
        |> withLangVersion50
        |> compileAndRun
        |> shouldSucceed