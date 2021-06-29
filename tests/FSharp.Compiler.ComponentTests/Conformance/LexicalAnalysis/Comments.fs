// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.Conformance.LexicalAnalysis

open Xunit
open FSharp.Test.Utilities.Compiler
open FSharp.Test.Utilities.Xunit.Attributes

module Comments =

    // This test was automatically generated (moved from FSharpQA suite - Conformance/LexicalAnalysis/Comments)
    //<Expects status="success"></Expects>
    [<Theory; Directory(__SOURCE_DIRECTORY__ + "/../../resources/tests/Conformance/LexicalAnalysis/Comments", Includes=[|"star01.fs"|])>]
    let ``Comments - star01.fs - --test:ErrorRanges`` compilation =
        compilation
        |> asFsx
        |> withOptions ["--test:ErrorRanges"]
        |> compile
        |> shouldSucceed
        |> ignore

    // This test was automatically generated (moved from FSharpQA suite - Conformance/LexicalAnalysis/Comments)
    
    [<Theory; Directory(__SOURCE_DIRECTORY__ + "/../../resources/tests/Conformance/LexicalAnalysis/Comments", Includes=[|"E_star02.fs"|])>]
    let ``Comments - E_star02.fs - --test:ErrorRanges`` compilation =
        compilation
        |> asFsx
        |> withOptions ["--test:ErrorRanges"]
        |> compile
        |> shouldFail
        |> withErrorCode 0010
        |> withDiagnosticMessageMatches @"Unexpected symbol '\)' in implementation file$"
        |> ignore

    // This test was automatically generated (moved from FSharpQA suite - Conformance/LexicalAnalysis/Comments)
    //<Expects status="success"></Expects>
    [<Theory; Directory(__SOURCE_DIRECTORY__ + "/../../resources/tests/Conformance/LexicalAnalysis/Comments", Includes=[|"star03.fs"|])>]
    let ``Comments - star03.fs - --test:ErrorRanges`` compilation =
        compilation
        |> asFsx
        |> withOptions ["--test:ErrorRanges"]
        |> compile
        |> shouldSucceed
        |> ignore

