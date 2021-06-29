// To run the tests in this file:
//
// Technique 1: Compile VisualFSharp.UnitTests.dll and run it as a set of unit tests
//
// Technique 2:
//
//   Enable some tests in the #if EXE section at the end of the file, 
//   then compile this file as an EXE that has InternalsVisibleTo access into the
//   appropriate DLLs.  This can be the quickest way to get turnaround on updating the tests
//   and capturing large amounts of structured output.
(*
    cd Debug\net40\bin
    .\fsc.exe --define:EXE -r:.\Microsoft.Build.Utilities.Core.dll -o VisualFSharp.UnitTests.exe -g --optimize- -r .\FSharp.Compiler.Service.dll  -r .\FSharp.Editor.dll -r nunit.framework.dll ..\..\..\tests\service\FsUnit.fs ..\..\..\tests\service\Common.fs /delaysign /keyfile:..\..\..\src\fsharp\msft.pubkey ..\..\..\vsintegration\tests\UnitTests\FsxCompletionProviderTests.fs 
    .\VisualFSharp.UnitTests.exe 
*)
// Technique 3: 
// 
//    Use F# Interactive.  This only works for FSharp.Compiler.Service.dll which has a public API

// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.
namespace Microsoft.VisualStudio.FSharp.Editor.Tests.Roslyn

open System
open System.Collections.Generic
open System.IO
open System.Linq
open System.Reflection

open NUnit.Framework

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Completion
open Microsoft.CodeAnalysis.Text
open Microsoft.VisualStudio.FSharp.Editor

open FSharp.Compiler.CodeAnalysis
open UnitTests.TestLib.LanguageService

// AppDomain helper
type Worker () =

    let filePath = "C:\\test.fsx"
    let projectOptions = {
        ProjectFileName = "C:\\test.fsproj"
        ProjectId = None
        SourceFiles =  [| filePath |]
        ReferencedProjects = [| |]
        OtherOptions = [| |]
        IsIncompleteTypeCheckEnvironment = true
        UseScriptResolutionRules = true
        LoadTime = DateTime.MaxValue
        OriginalLoadReferences = []
        UnresolvedReferences = None
        Stamp = None
    }

    member _.VerifyCompletionListExactly(fileContents: string, marker: string, expected: List<string>) =
        let caretPosition = fileContents.IndexOf(marker) + marker.Length
        let document = RoslynTestHelpers.CreateDocument(filePath, SourceText.From(fileContents), options = projectOptions)
        let expected = expected |> Seq.toList
        let actual = 
            let x = FSharpCompletionProvider.ProvideCompletionsAsyncAux(document, caretPosition, (fun _ -> []), IntelliSenseOptions.Default) 
                    |> Async.RunSynchronously 
            x |> Option.defaultValue (ResizeArray())
            |> Seq.toList
            // sort items as Roslyn do - by `SortText`
            |> List.sortBy (fun x -> x.SortText)

        let actualNames = actual |> List.map (fun x -> x.DisplayText)

        if actualNames <> expected then
            Assert.Fail(sprintf "Expected:\n%s,\nbut was:\n%s\nactual with sort text:\n%s" 
                                (String.Join("; ", expected |> List.map (sprintf "\"%s\""))) 
                                (String.Join("; ", actualNames |> List.map (sprintf "\"%s\"")))
                                (String.Join("\n", actual |> List.map (fun x -> sprintf "%s => %s" x.DisplayText x.SortText))))

module FsxCompletionProviderTests =

    let getWorker () = Worker()

    [<Test>]
    let fsiShouldTriggerCompletionInFsxFile() =
        let fileContents = """
    fsi.
    """
        let expected = List<string>([
            "CommandLineArgs"; "EventLoop"; "FloatingPointFormat"; "FormatProvider"; "PrintDepth"; 
            "PrintLength"; "PrintSize"; "PrintWidth"; "ShowDeclarationValues"; "ShowIEnumerable"; 
            "ShowProperties"; "AddPrinter"; "AddPrintTransformer"; "Equals"; "GetHashCode"; 
            "GetType"; "ToString"; ])

        // We execute in a seperate appdomain so that we can set BaseDirectory to a non-existent location
        getWorker().VerifyCompletionListExactly(fileContents, "fsi.", expected)

#if EXE
ShouldTriggerCompletionInFsxFile()
#endif
