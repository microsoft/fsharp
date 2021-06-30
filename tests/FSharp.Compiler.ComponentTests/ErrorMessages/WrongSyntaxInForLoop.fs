// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.ErrorMessages

open Xunit
open FSharp.Test.Utilities.Compiler


module ``Wrong syntax in for loop`` =

    [<Fact(Skip="disabled after changes to range syntax processing")>]
    let ``Equals instead of in``() =
        FSharp """
module X
for i = 0 .. 100 do
    ()
        """
        |> parse
        |> shouldFail
        |> withSingleDiagnostic (Error 3215, Line 3, Col 7, Line 3, Col 8,
                                 "Unexpected symbol '=' in expression. Did you intend to use 'for x in y .. z do' instead?")
