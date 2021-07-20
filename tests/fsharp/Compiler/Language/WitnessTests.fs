// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.UnitTests

open NUnit.Framework
open FSharp.Test
open FSharp.Test.Utilities
open FSharp.Test.Compiler
open TestFramework

#if !NETCOREAPP

[<TestFixture>]
module WitnessTests =

    [<Test>]
    let ``Witness expressions are created as a result of compiling the type provider tests`` () =
        let dir = getTestsDirectory __SOURCE_DIRECTORY__ "../../typeProviders/helloWorld"
        Fsx (sprintf """
#load @"%s"
        """ (dir ++ "provider.fsx"))
        |> asExe
        |> ignoreWarnings
        |> withLangVersion50
        |> compile
        |> shouldSucceed
        |> ignore
#endif
        

