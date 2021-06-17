// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.UnitTests

open System
open System.IO
open FSharp.Compiler.Diagnostics
open NUnit.Framework
open FSharp.Test.Utilities
open FSharp.Test.Utilities.Utilities
open FSharp.Test.Utilities.Compiler
open FSharp.Tests
open FSharp.Compiler.CodeAnalysis
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.CSharp
open FSharp.Compiler.Text

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
            |> Async.RunSynchronously


        match checkAnswer with
        | FSharpCheckFileAnswer.Aborted -> failwith "check file aborted"
        | FSharpCheckFileAnswer.Succeeded(checkResults) ->
            Assert.shouldBeEmpty(checkResults.Diagnostics)
            WeakReference(ms)

    [<Test>]
    let ``Using a CSharp reference project in-memory``() =
        AssertInMemoryCSharpReferenceIsValid() |> ignore

    [<Test;NonParallelizable>]
    let ``Using a CSharp reference project in-memory and it gets GCed``() =
        let weakRef = AssertInMemoryCSharpReferenceIsValid()
        CompilerAssert.Checker.ClearLanguageServiceRootCachesAndCollectAndFinalizeAllTransients()
        GC.Collect(2, GCCollectionMode.Forced, true)
        Assert.shouldBeFalse(weakRef.IsAlive)


        
