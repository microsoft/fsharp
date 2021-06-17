// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.Conformance.DeclarationElements.LetBindings

open Xunit
open FSharp.Test.Utilities.Compiler
open FSharp.Test.Utilities.Xunit.Attributes

module UseBindings =

    [<Theory; Directory(__SOURCE_DIRECTORY__ + "/../../../resources/tests/Conformance/DeclarationElements/LetBindings", Includes=[|"UseBindingDiscard01.fs"|])>]
    let ``UseBindings - UseBindingDiscard01.fs - Compiles`` compilation =
        compilation
        |> asFsx
        |> withOptions ["--langversion:preview"]
        |> compile
        |> shouldSucceed
        |> ignore

    [<Theory; Directory(__SOURCE_DIRECTORY__ + "/../../../resources/tests/Conformance/DeclarationElements/LetBindings", Includes=[|"UseBindingDiscard01.fs"|])>]
    let ``UseBindings - UseBindingDiscard01.fs - Bad LangVersion`` compilation =
        compilation
        |> asFsx
        |> withOptions ["--langversion:5.0"]
        |> compile
        |> shouldFail
        |> withErrorCode 3350
        |> withDiagnosticMessageMatches "Feature 'discard pattern in use binding' is not available.*"

    [<Fact>]
    let ``Dispose called for discarded value of use binding`` () =
        Fsx """
type private Disposable() =
    [<DefaultValue>] static val mutable private disposedTimes: int
    [<DefaultValue>] static val mutable private constructedTimes: int

    do Disposable.constructedTimes <- Disposable.constructedTimes + 1

    static member DisposeCallCount() = Disposable.disposedTimes
    static member ConsturctorCallCount() = Disposable.constructedTimes

    interface System.IDisposable with
        member _.Dispose() =
            Disposable.disposedTimes <- Disposable.disposedTimes + 1

let _scope =
    use _ = new Disposable()
    ()

let disposeCalls = Disposable.DisposeCallCount()
if disposeCalls <> 1 then
    failwith "was not disposed or disposed too many times"

let ctorCalls = Disposable.ConsturctorCallCount()
if ctorCalls <> 1 then
    failwithf "unexpected constructor call count: %i" ctorCalls

        """
        |> asExe
        |> withLangVersionPreview
        |> compileAndRun
        |> shouldSucceed