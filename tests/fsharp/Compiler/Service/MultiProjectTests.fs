// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.UnitTests

open System
open System.IO
open FSharp.Compiler.Diagnostics
open NUnit.Framework
open FSharp.Test
open FSharp.Test.Utilities
open FSharp.Test.Compiler
open FSharp.Compiler.CodeAnalysis
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.CSharp
open FSharp.Compiler.Text
open TestFramework

[<TestFixture>]
module MultiProjectTests =

    let AssertInMemoryCSharpReferenceIsValid () =
        let csSrc =
            """
namespace CSharpTest
{
    public class CSharpClass
    {
    }
}
            """

        let csOptions = CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        let csSyntax = CSharpSyntaxTree.ParseText(csSrc)
        let csReferences = TargetFrameworkUtil.getReferences TargetFramework.NetStandard20
        let cs = CSharpCompilation.Create("csharp_test.dll", references = csReferences.As<MetadataReference>(), syntaxTrees = [csSyntax], options = csOptions)

        let ms = new MemoryStream()
        let getStream =
            fun ct ->
                cs.Emit(ms, cancellationToken = ct) |> ignore
                ms.Position <- 0L
                ms :> Stream
                |> Some

        let csRefProj = FSharpReferencedProject.CreatePortableExecutable("""Z:\csharp_test.dll""", DateTime.UtcNow, getStream)

        let fsOptions = CompilerAssert.DefaultProjectOptions
        let fsOptions =
            { fsOptions with 
                ProjectId = Some(Guid.NewGuid().ToString())
                OtherOptions = Array.append fsOptions.OtherOptions [|"""-r:Z:\csharp_test.dll"""|] 
                ReferencedProjects = [|csRefProj|] }

        let fsText =
            """
module FSharpTest

open CSharpTest

let test() =
    CSharpClass()
            """
            |> SourceText.ofString
        let _, checkAnswer = 
            CompilerAssert.Checker.ParseAndCheckFileInProject("test.fs", 0, fsText, fsOptions)
            |> Async.RunImmediate


        match checkAnswer with
        | FSharpCheckFileAnswer.Aborted -> failwith "check file aborted"
        | FSharpCheckFileAnswer.Succeeded(checkResults) ->
            Assert.shouldBeEmpty(checkResults.Diagnostics)
            WeakReference(ms)

    let compileFileAsDll (checker: FSharpChecker) filePath outputFilePath =
        try
            let result, _ =
                checker.Compile([|"fsc.dll";filePath;$"-o:{ outputFilePath }";"--deterministic+";"--optimize+";"--target:library"|])
                |> Async.RunImmediate

            if result.Length > 0 then
                failwith "Compilation has errors."
        with
        | _ ->
            try File.Delete(outputFilePath) with | _ -> ()
            reraise()

    let createOnDisk src =
        let tmpFilePath = tryCreateTemporaryFileName ()
        let tmpRealFilePath = Path.ChangeExtension(tmpFilePath, ".fs")
        try File.Delete(tmpFilePath) with | _ -> ()
        File.WriteAllText(tmpRealFilePath, src)
        tmpRealFilePath

    let createOnDiskCompiledAsDll checker src =
        let tmpFilePath = tryCreateTemporaryFileName ()
        let tmpRealFilePath = Path.ChangeExtension(tmpFilePath, ".fs")
        try File.Delete(tmpFilePath) with | _ -> ()
        File.WriteAllText(tmpRealFilePath, src)

        let outputFilePath = Path.ChangeExtension(tmpRealFilePath, ".dll")

        try
            compileFileAsDll checker tmpRealFilePath outputFilePath
            outputFilePath
        finally
            try File.Delete(tmpRealFilePath) with | _ -> ()

    let updateFileOnDisk filePath src =
        File.WriteAllText(filePath, src)

    let updateCompiledDllOnDisk checker (dllPath: string) src =
        if not (File.Exists dllPath) then
            failwith $"File {dllPath} does not exist."

        let filePath = createOnDisk src

        try
            compileFileAsDll checker filePath dllPath
        finally
            try File.Delete(filePath) with | _ -> ()

    [<Test>]
    let ``Using a CSharp reference project in-memory``() =
        AssertInMemoryCSharpReferenceIsValid() |> ignore

    [<Test;NonParallelizable>]
    let ``Using a CSharp reference project in-memory and it gets GCed``() =
        let weakRef = AssertInMemoryCSharpReferenceIsValid()
        CompilerAssert.Checker.ClearLanguageServiceRootCachesAndCollectAndFinalizeAllTransients()
        GC.Collect(2, GCCollectionMode.Forced, true)
        Assert.shouldBeFalse(weakRef.IsAlive)

    [<Test>]
    let ``Using compiler service, file referencing a DLL will correctly update when the referenced DLL file changes``() =
        let checker = CompilerAssert.Checker

        // Create an assembly with the module Script1 and function x.
        let dllPath1 = 
            createOnDiskCompiledAsDll checker
                """
module Script1

let x = 1
                """

        // Create script with that uses Script1 and function x
        let filePath1 = 
            createOnDisk 
                """
module Script2

let x = Script1.x
                """
        
        try
            let fsOptions1 = CompilerAssert.DefaultProjectOptions
            let fsOptions1 =
                { fsOptions1 with 
                    ProjectId = Some(Guid.NewGuid().ToString())
                    OtherOptions = [|"-r:" + dllPath1|]
                    ReferencedProjects = [||]
                    SourceFiles = [|filePath1|] }              

            // Verify that a script using Script1.x works
            let checkProjectResults1 = 
                checker.ParseAndCheckProject(fsOptions1)
                |> Async.RunImmediate

            Assert.IsEmpty(checkProjectResults1.Diagnostics)

            // Create script with that uses Script1 and function x and function y
            updateFileOnDisk filePath1
                """
module Script2

let x = Script1.x
let y = Script1.y
                """

            // Verify that a script using Script1.x and Script1.y fails
            let checkProjectResults2 = 
                checker.ParseAndCheckProject(fsOptions1)
                |> Async.RunImmediate

            Assert.IsNotEmpty(checkProjectResults2.Diagnostics)

            // Create an assembly with the module Script1 and function x and function y
            updateCompiledDllOnDisk checker dllPath1
                """
module Script1

let x = 1
let y = 1
                """

            // Verify that a script using Script1.x and Script1.y fails
            let checkProjectResults3 = 
                checker.ParseAndCheckProject(fsOptions1)
                |> Async.RunImmediate

            Assert.IsEmpty(checkProjectResults3.Diagnostics)

        finally
            try File.Delete(dllPath1) with | _ -> ()
            try File.Delete(filePath1) with | _ -> ()



        
