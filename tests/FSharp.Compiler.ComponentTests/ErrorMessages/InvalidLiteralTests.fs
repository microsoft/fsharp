// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.ErrorMessages

open Xunit
open FSharp.Test.Utilities.Compiler

module ``Invalid literals`` =

    [<Fact>]
    let ``Using Active Pattern``() =
        FSharp """
let (|A|) x = x + 1
let [<Literal>] (A x) = 1
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 3391, Line 3, Col 5, Line 3, Col 22, "A [<Literal>] declaration cannot use an active pattern for its identifier")
