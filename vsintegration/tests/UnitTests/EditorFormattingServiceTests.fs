// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
namespace Microsoft.VisualStudio.FSharp.Editor.Tests.Roslyn

open System

open NUnit.Framework

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Text
open Microsoft.VisualStudio.FSharp.Editor
open FSharp.Compiler.CodeAnalysis
open Microsoft.CodeAnalysis.Formatting

[<TestFixture>]
[<Category "Roslyn Services">]
type EditorFormattingServiceTests()  =
    let filePath = "C:\\test.fs"
    let projectOptions : FSharpProjectOptions = { 
        ProjectFileName = "C:\\test.fsproj"
        ProjectId = None
        SourceFiles =  [| filePath |]
        ReferencedProjects = [| |]
        OtherOptions = [| |]
        IsIncompleteTypeCheckEnvironment = true
        UseScriptResolutionRules = false
        LoadTime = DateTime.MaxValue
        OriginalLoadReferences = []
        UnresolvedReferences = None
        Stamp = None
    }

    let documentId = DocumentId.CreateNewId(ProjectId.CreateNewId())
    let indentStyle = FormattingOptions.IndentStyle.Smart
    
    let indentTemplate = """
let foo = [
    15
    ]marker1

async {
    return 10
    }marker2

let abc =
    [|
        10
        |]marker3

let def =
    (
        "hi"
        )marker4
"""

    let pasteTemplate = """

let foo =
    printfn "Something here"
    marker1

marker2

            marker3

marker4"""
    
    [<TestCase("marker1", "]")>]
    [<TestCase("marker2", "}")>]
    [<TestCase("marker3", "    |]")>]
    [<TestCase("marker4", "    )")>]
    member this.TestIndentation(marker: string, expectedLine: string) =
        let position = indentTemplate.IndexOf(marker)
        Assert.IsTrue(position >= 0, "Precondition failed: unable to find marker in template")

        let sourceText = SourceText.From(indentTemplate)
        let lineNumber = sourceText.Lines |> Seq.findIndex (fun line -> line.Span.Contains position)
        
        let doc = RoslynTestHelpers.CreateDocument(filePath, sourceText, projectOptions)
        let changesOpt = FSharpEditorFormattingService.GetFormattingChanges(doc, sourceText, filePath, indentStyle, position) |> Async.RunSynchronously
        match changesOpt with
        | None -> Assert.Fail("Expected a text change, but got None")
        | Some changes ->
            let changedText = sourceText.WithChanges(changes)
            let lineText = changedText.Lines.[lineNumber].ToString()
            Assert.IsTrue(lineText.StartsWith(expectedLine), "Changed line does not start with expected text")

    [<TestCase(true, "")>]
    [<TestCase(true, "        ")>]
    [<TestCase(false, "")>]
    member this.TestPasteChanges_PastingOntoIndentedLine(enabled: bool, prefix: string) =
        let checker = FSharpChecker.Create()
        let parsingOptions, _ = checker.GetParsingOptionsFromProjectOptions projectOptions

        let clipboard = prefix + """[<Class>]
        type SomeNameHere () =
            member _.Test ()"""

        let start = """

let foo =
    printfn "Something here"
    $

somethingElseHere
"""

        let expected = """

let foo =
    printfn "Something here"
    [<Class>]
    type SomeNameHere () =
        member _.Test ()

somethingElseHere
"""
        
        let sourceText = SourceText.From(start.Replace("$", clipboard))
        let span = TextSpan(start.IndexOf '$', clipboard.Length)

        let formattingOptions = { FormatOnPaste = enabled }

        let changesOpt =
            FSharpEditorFormattingService.GetPasteChanges(documentId, sourceText, filePath, formattingOptions, 4, parsingOptions, clipboard, span)
            |> Async.RunSynchronously
            |> Option.map List.ofSeq
        
        if enabled then
            match changesOpt with
            | Some changes ->
                let changedText = sourceText.WithChanges(changes).ToString()
                Assert.AreEqual(expected, changedText)
            | _ -> Assert.Fail (sprintf "Expected text changes, but got %+A" changesOpt)
        else
            Assert.AreEqual(None, changesOpt, "Expected no changes as FormatOnPaste is disabled")

    [<TestCase "">]
    [<TestCase "        ">]
    member this.TestPasteChanges_PastingOntoEmptyLine(prefix: string) =
        let checker = FSharpChecker.Create()
        let parsingOptions, _ = checker.GetParsingOptionsFromProjectOptions projectOptions

        let clipboard = prefix + """[<Class>]
        type SomeNameHere () =
            member _.Test ()"""

        let start = """
$

let foo =
    printfn "Something here"

somethingElseHere
"""

        let expected = """
[<Class>]
type SomeNameHere () =
    member _.Test ()

let foo =
    printfn "Something here"

somethingElseHere
"""
        
        let sourceText = SourceText.From(start.Replace("$", clipboard))
        let span = TextSpan(start.IndexOf '$', clipboard.Length)

        let formattingOptions = { FormatOnPaste = true }

        let changesOpt =
            FSharpEditorFormattingService.GetPasteChanges(documentId, sourceText, filePath, formattingOptions, 4, parsingOptions, clipboard, span)
            |> Async.RunSynchronously
            |> Option.map List.ofSeq

        match changesOpt with
        | Some changes ->
            let changedText = sourceText.WithChanges(changes).ToString()
            Assert.AreEqual(expected, changedText)
        | _ -> Assert.Fail (sprintf "Expected a changes, but got %+A" changesOpt)

    [<Test>]
    member this.TestPasteChanges_PastingWithAutoIndentationInPasteSpan() =
        let checker = FSharpChecker.Create()
        let parsingOptions, _ = checker.GetParsingOptionsFromProjectOptions projectOptions

        let clipboard = """[<Class>]
        type SomeNameHere () =
            member _.Test ()"""

        let start = """

let foo =
    printfn "Something here"
    $

somethingElseHere
"""

        let expected = """

let foo =
    printfn "Something here"
    [<Class>]
    type SomeNameHere () =
        member _.Test ()

somethingElseHere
"""
        
        let sourceText = SourceText.From(start.Replace("$", clipboard))

        // If we're pasting on an empty line which has been automatically indented,
        // then the pasted span includes this automatic indentation. Check that we
        // still format as expected
        let span = TextSpan(start.IndexOf '$' - 4, clipboard.Length + 4)

        let formattingOptions = { FormatOnPaste = true }

        let changesOpt =
            FSharpEditorFormattingService.GetPasteChanges(documentId, sourceText, filePath, formattingOptions, 4, parsingOptions, clipboard, span)
            |> Async.RunSynchronously
            |> Option.map List.ofSeq

        match changesOpt with
        | Some changes ->
            let changedText = sourceText.WithChanges(changes).ToString()
            Assert.AreEqual(expected, changedText)
        | _ -> Assert.Fail (sprintf "Expected a changes, but got %+A" changesOpt)
