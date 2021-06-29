// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.Conformance.PatternMatching

open Xunit
open FSharp.Test.Utilities.Compiler
open FSharp.Test.Utilities.Xunit.Attributes

module Simple =

    // This test was automatically generated (moved from FSharpQA suite - Conformance/PatternMatching/Simple)
    //<Expects id="FS0025" span="(92,13-92,14)" status="warning">Incomplete pattern matches on this expression. For example, the value 'Result \(_\)' may indicate a case not covered by the pattern\(s\)</Expects>
    [<Theory; Directory(__SOURCE_DIRECTORY__ + "/../../resources/tests/Conformance/PatternMatching/Simple", Includes=[|"W_Incomplete01.fs"|])>]
    let ``Simple - W_Incomplete01.fs - --test:ErrorRanges`` compilation =
        compilation
        |> asFs
        |> withOptions ["--test:ErrorRanges"]
        |> typecheck
        |> shouldFail
        |> withWarningCode 0025
        |> withDiagnosticMessageMatches "Incomplete pattern matches on this expression. For example, the value 'Result \(_\)' may indicate a case not covered by the pattern\(s\)"
        |> ignore

    // This test was automatically generated (moved from FSharpQA suite - Conformance/PatternMatching/Simple)
    //<Expects id="FS0026" span="(32,11-32,13)" status="warning">This rule will never be matched</Expects>
    [<Theory; Directory(__SOURCE_DIRECTORY__ + "/../../resources/tests/Conformance/PatternMatching/Simple", Includes=[|"W_Incomplete02.fs"|])>]
    let ``Simple - W_Incomplete02.fs - --test:ErrorRanges`` compilation =
        compilation
        |> asFsx
        |> withOptions ["--test:ErrorRanges"]
        |> compile
        |> shouldFail
        |> withWarningCode 0026
        |> withDiagnosticMessageMatches "This rule will never be matched"
        |> ignore

    // This test was automatically generated (moved from FSharpQA suite - Conformance/PatternMatching/Simple)
    //<Expects id="FS0049" span="(10,16-10,19)" status="warning">Uppercase variable identifiers should not generally be used in patterns, and may indicate a missing open declaration or a misspelt pattern name</Expects>
    [<Theory; Directory(__SOURCE_DIRECTORY__ + "/../../resources/tests/Conformance/PatternMatching/Simple", Includes=[|"W_BindCaptialIdent.fs"|])>]
    let ``Simple - W_BindCaptialIdent.fs - --test:ErrorRanges`` compilation =
        compilation
        |> asFsx
        |> withOptions ["--test:ErrorRanges"]
        |> compile
        |> shouldFail
        |> withWarningCode 0049
        |> withDiagnosticMessageMatches "Uppercase variable identifiers should not generally be used in patterns, and may indicate a missing open declaration or a misspelt pattern name"
        |> ignore

    [<Fact>]
    let ``As patterns``() =
        Fsx """
        let (|Id|) = id
        let a = [1..4]
        match a with
        | 1 | 1 as b::(Id 2 as c as c2)::[d as 3; Id e & Id _ as Id 4] as Id f when b = 1 && c = 2 && c2 = 2 && d = 3 && e = 4 && a = f -> ()
        | _ -> failwith "Match failed"
        """
        |> asExe
        |> withLangVersionPreview
        |> compileExeAndRun
        |> shouldSucceed