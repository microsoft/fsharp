// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.UnitTests.CodeGen.EmittedIL

open System.IO
open System.Reflection
open System.Reflection.Metadata
open System.Reflection.PortableExecutable
open System.Collections.Immutable
open FSharp.Test.Utilities
open FSharp.Test.Utilities.Compiler
open NUnit.Framework

[<TestFixture>]
module DeterministicTests =

    [<Test>]
    let ``Simple assembly should be deterministic``() =
        let inputFilePath = CompilerAssert.GenerateFsInputPath()
        let outputFilePath = CompilerAssert.GenerateDllOutputPath()
        let src =
            """
module Assembly

open System

let test() =
    Console.WriteLine("Hello World!")
            """

        File.WriteAllText(inputFilePath, src)

        let mvid1 = 
            FSharpWithInputAndOutputPath inputFilePath outputFilePath 
            |> withOptions ["--deterministic"] 
            |> compileGuid
        let mvid2 = 
            FSharpWithInputAndOutputPath inputFilePath outputFilePath 
            |> withOptions ["--deterministic"] 
            |> compileGuid

        // Two identical compilations should produce the same MVID
        Assert.AreEqual(mvid1, mvid2)

    [<Test>]
    let ``Simple assembly with different platform should not be deterministic``() =
        let inputFilePath = CompilerAssert.GenerateFsInputPath()
        let outputFilePath = CompilerAssert.GenerateDllOutputPath()
        let src =
            """
module Assembly

open System

let test() =
    Console.WriteLine("Hello World!")
            """

        File.WriteAllText(inputFilePath, src)

        let mvid1 = 
            FSharpWithInputAndOutputPath inputFilePath outputFilePath 
            |> withOptions ["--deterministic"] 
            |> compileGuid
        let mvid2 = 
            FSharpWithInputAndOutputPath inputFilePath outputFilePath 
            |> withOptions ["--deterministic";"--platform:Itanium"] 
            |> compileGuid

        // No two platforms should produce the same MVID
        Assert.AreNotEqual(mvid1, mvid2)

    [<Test>]
    let ``Simple reference assembly should be deterministic``() =
        let inputFilePath = CompilerAssert.GenerateFsInputPath()
        let outputFilePath = CompilerAssert.GenerateDllOutputPath()
        let src =
            """
module ReferenceAssembly

open System

let test() =
    Console.WriteLine("Hello World!")
            """

        File.WriteAllText(inputFilePath, src)

        let mvid1 = 
            FSharpWithInputAndOutputPath inputFilePath outputFilePath 
            |> withOptions ["--refonly";"--deterministic"] 
            |> compileGuid
        let mvid2 = 
            FSharpWithInputAndOutputPath inputFilePath outputFilePath 
            |> withOptions ["--refonly";"--deterministic"] 
            |> compileGuid

        // Two identical compilations should produce the same MVID
        Assert.AreEqual(mvid1, mvid2)

    [<Test>]
    let ``Simple reference assembly with different platform should not be deterministic``() =
        let inputFilePath = CompilerAssert.GenerateFsInputPath()
        let outputFilePath = CompilerAssert.GenerateDllOutputPath()
        let src =
            """
module ReferenceAssembly

open System

let test() =
    Console.WriteLine("Hello World!")
            """

        File.WriteAllText(inputFilePath, src)

        let mvid1 = 
            FSharpWithInputAndOutputPath inputFilePath outputFilePath 
            |> withOptions ["--refonly";"--deterministic"] 
            |> compileGuid
        let mvid2 = 
            FSharpWithInputAndOutputPath inputFilePath outputFilePath 
            |> withOptions ["--refonly";"--deterministic";"--platform:Itanium"] 
            |> compileGuid

        // No two platforms should produce the same MVID
        Assert.AreNotEqual(mvid1, mvid2)
