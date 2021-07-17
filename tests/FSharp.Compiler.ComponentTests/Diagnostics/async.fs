// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.Diagnostics

open Xunit
open FSharp.Test.Compiler
open FSharp.Test.Xunit.Attributes

module async =

    // This test was automatically generated (moved from FSharpQA suite - Diagnostics/async)
    //<Expects status="error" span="(6,33-6,39)" id="FS0001">Type mismatch\. Expecting a.+''a'.+but given a.+'Async<'a>'.*The types ''a' and 'Async<'a>' cannot be unified.</Expects>
    [<Theory; Directory(__SOURCE_DIRECTORY__ + "/../resources/tests/Diagnostics/async", Includes=[|"MissingBangForLoop01.fs"|])>]
    let ``async - MissingBangForLoop01.fs - --warnaserror+ --test:ErrorRanges --flaterrors`` compilation =
        compilation
        |> asFsx
        |> withOptions ["--warnaserror+"; "--test:ErrorRanges"; "--flaterrors"]
        |> compile
        |> shouldFail
        |> withErrorCode 0001
        |> withDiagnosticMessageMatches "' cannot be unified."
        |> ignore

    // This test was automatically generated (moved from FSharpQA suite - Diagnostics/async)
    //<Expects status="error" span="(7,54-7,61)" id="FS0001">Type mismatch\. Expecting a.    ''a'    .but given a.    'Async<'a>'    .The types ''a' and 'Async<'a>' cannot be unified.</Expects>
    [<Theory; Directory(__SOURCE_DIRECTORY__ + "/../resources/tests/Diagnostics/async", Includes=[|"MissingBangForLoop02.fs"|])>]
    let ``async - MissingBangForLoop02.fs - --warnaserror+ --test:ErrorRanges --flaterrors`` compilation =
        compilation
        |> asFsx
        |> withOptions ["--warnaserror+"; "--test:ErrorRanges"; "--flaterrors"]
        |> compile
        |> shouldFail
        |> withErrorCode 0001
        |> withDiagnosticMessageMatches "' cannot be unified."
        |> ignore

    // This test was automatically generated (moved from FSharpQA suite - Diagnostics/async)
    //<Expects status="error" span="(7,18-7,19)" id="FS0001">All branches of an 'if' expression must return values implicitly convertible to the type of the first branch</Expects>
    [<Theory; Directory(__SOURCE_DIRECTORY__ + "/../resources/tests/Diagnostics/async", Includes=[|"ReturnBangNonAsync_IfThenElse.fs"|])>]
    let ``async - ReturnBangNonAsync_IfThenElse.fs - --warnaserror+ --test:ErrorRanges --flaterrors`` compilation =
        compilation
        |> asFsx
        |> withOptions ["--warnaserror+"; "--test:ErrorRanges"; "--flaterrors"]
        |> compile
        |> shouldFail
        |> withErrorCode 0001
        |> withDiagnosticMessageMatches "All branches of an 'if' expression must return values implicitly convertible to the type of the first branch"
        |> ignore

    // This test was automatically generated (moved from FSharpQA suite - Diagnostics/async)
    //<Expects status="error" span="(14,16-14,28)" id="FS1228">'use!' bindings must be of the form 'use! <var> = <expr>'$</Expects>
    [<Theory; Directory(__SOURCE_DIRECTORY__ + "/../resources/tests/Diagnostics/async", Includes=[|"UseBindingWrongForm01.fs"|])>]
    let ``async - UseBindingWrongForm01.fs - --warnaserror+ --test:ErrorRanges --flaterrors`` compilation =
        compilation
        |> asFsx
        |> withOptions ["--warnaserror+"; "--test:ErrorRanges"; "--flaterrors"]
        |> compile
        |> shouldFail
        |> withErrorCode 1228
        |> withDiagnosticMessageMatches "'$"
        |> ignore

