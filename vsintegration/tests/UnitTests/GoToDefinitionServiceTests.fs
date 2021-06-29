// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.
//
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
    .\fsc.exe --define:EXE -r:.\Microsoft.Build.Utilities.Core.dll -o VisualFSharp.UnitTests.exe -g --optimize- -r .\FSharp.Compiler.Service.dll  -r .\FSharp.Editor.dll -r nunit.framework.dll ..\..\..\tests\service\FsUnit.fs ..\..\..\tests\service\Common.fs /delaysign /keyfile:..\..\..\src\fsharp\msft.pubkey ..\..\..\vsintegration\tests\UnitTests\GoToDefinitionServiceTests.fs 
    .\VisualFSharp.UnitTests.exe 
*)
// Technique 3: 
// 
//    Use F# Interactive.  This only works for FSharp.Compiler.Service.dll which has a public API

namespace Microsoft.VisualStudio.FSharp.Editor.Tests.Roslyn

open System
open System.IO
open NUnit.Framework

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Text
open Microsoft.VisualStudio.FSharp.Editor
open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Text
open UnitTests.TestLib.LanguageService

[<TestFixture>][<Category "Roslyn Services">]
module GoToDefinitionServiceTests =

    let userOpName = "GoToDefinitionServiceTests"

    let private findDefinition
        (
            document: Document,
            sourceText: SourceText,
            position: int,
            defines: string list 
        ) : range option = 
        maybe {
            let textLine = sourceText.Lines.GetLineFromPosition position
            let textLinePos = sourceText.Lines.GetLinePosition position
            let fcsTextLineNumber = Line.fromZ textLinePos.Line
            let! lexerSymbol = Tokenizer.getSymbolAtPosition(document.Id, sourceText, position, document.FilePath, defines, SymbolLookupKind.Greedy, false, false)
            let _, checkFileResults = document.GetFSharpParseAndCheckResultsAsync(nameof(userOpName)) |> Async.RunSynchronously

            let declarations = checkFileResults.GetDeclarationLocation (fcsTextLineNumber, lexerSymbol.Ident.idRange.EndColumn, textLine.ToString(), lexerSymbol.FullIsland, false)
            
            match declarations with
            | FindDeclResult.DeclFound range -> return range
            | _ -> return! None
        }

    let makeOptions filePath args = 
        { 
            ProjectFileName = "C:\\test.fsproj"
            ProjectId = None
            SourceFiles =  [| filePath |]
            ReferencedProjects = [| |]
            OtherOptions = args
            IsIncompleteTypeCheckEnvironment = true
            UseScriptResolutionRules = false
            LoadTime = DateTime.MaxValue
            OriginalLoadReferences = []
            UnresolvedReferences = None
            Stamp = None
        }

    let GoToDefinitionTest (fileContents: string, caretMarker: string, expected) =

        let filePath = Path.GetTempFileName() + ".fs"
        File.WriteAllText(filePath, fileContents)

        let caretPosition = fileContents.IndexOf(caretMarker) + caretMarker.Length - 1 // inside the marker
        let document, sourceText = RoslynTestHelpers.CreateDocument(filePath, fileContents)
        let actual = 
           findDefinition(document, sourceText, caretPosition, []) 
           |> Option.map (fun range -> (range.StartLine, range.EndLine, range.StartColumn, range.EndColumn))

        if actual <> expected then 
            Assert.Fail(sprintf "Incorrect information returned for fileContents=<<<%s>>>, caretMarker=<<<%s>>>, expected =<<<%A>>>, actual = <<<%A>>>" fileContents caretMarker expected actual)

    [<Test>]
    let VerifyDefinition() =

      let manyTestCases = 
        [ 
// Test1
          ("""
type TestType() =
    member this.Member1(par1: int) =
        printf "%d" par1
    member this.Member2(par2: string) =
        printf "%s" par2

[<EntryPoint>]
let main argv =
    let obj = TestType()
    obj.Member1(5)
    obj.Member2("test")""",
                [ ("printf \"%d\" par1", Some(3, 3, 24, 28));
                  ("printf \"%s\" par2", Some(5, 5, 24, 28));
                  ("let obj = TestType", Some(2, 2, 5, 13));
                  ("let obj", Some(10, 10, 8, 11));
                  ("obj.Member1", Some(3, 3, 16, 23));
                  ("obj.Member2", Some(5, 5, 16, 23)); ]);
// Test2
          ("""
module Module1 =
    let foo x = x

let _ = Module1.foo 1
""",
                [ ("let _ = Module", Some (2, 2, 7, 14)) ]) 
          ]

      for fileContents, testCases in manyTestCases do
       for caretMarker, expected in testCases do
        
        printfn "Test case: caretMarker=<<<%s>>>" caretMarker 
        GoToDefinitionTest (fileContents, caretMarker, expected)

    [<Test>]
    let VerifyDefinitionStringInterpolation() =

        let fileContents = """
let xxxxx = 1
let yyyy = $"{abc{xxxxx}def}" """
        let caretMarker = "xxxxx"
        let expected = Some(2, 2, 4, 9)

        GoToDefinitionTest (fileContents, caretMarker, expected)

#if EXE
    VerifyDefinition()
    VerifyDefinitionStringInterpolation()
#endif