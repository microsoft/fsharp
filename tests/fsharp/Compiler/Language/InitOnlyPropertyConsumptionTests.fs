// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.UnitTests

open NUnit.Framework
open FSharp.Test.Utilities.Compiler
open FSharp.Test.Utilities
open FSharp.Test.Utilities.Utilities
open FSharp.Tests

#if NETCOREAPP

[<TestFixture>]
module InitOnlyPropertyConsumptionTests =

    [<Test>]
    let ``Should be able to set an init-only property from a C# record`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public record Test
    {
        public int X { get; init; }
    }
}
            """

        let fsharpSource =
            """
open System
open System.Runtime.CompilerServices
open CSharpTest

let test() =
    Test(X = 123)

[<EntryPoint>]
let main _ =
    let x = test()
    Console.Write(x.X)
    0
            """

        let csCmpl =
            CompilationUtil.CreateCSharpCompilation(csharpSource, CSharpLanguageVersion.CSharp9, TargetFramework.Current)
            |> CompilationReference.Create

        let fsCmpl =
            Compilation.Create(fsharpSource, Fs, Exe, options = [|"--langversion:5.0"|], cmplRefs = [csCmpl])

        CompilerAssert.ExecutionHasOutput(fsCmpl, "123")

#endif

