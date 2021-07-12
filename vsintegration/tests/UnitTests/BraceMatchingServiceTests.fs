// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.
namespace Microsoft.VisualStudio.FSharp.Editor.Tests.Roslyn

open System
open System.Threading

open NUnit.Framework

open Microsoft.CodeAnalysis.Classification
open Microsoft.CodeAnalysis.Editor
open Microsoft.CodeAnalysis.Text
open FSharp.Compiler.CodeAnalysis
open Microsoft.VisualStudio.FSharp.Editor
open Microsoft.VisualStudio.FSharp.LanguageService
open UnitTests.TestLib.LanguageService

[<TestFixture>][<Category "Roslyn Services">]
type BraceMatchingServiceTests()  =
    let fileName = "C:\\test.fs"
    let projectOptions: FSharpProjectOptions = { 
        ProjectFileName = "C:\\test.fsproj"
        ProjectId = None
        SourceFiles =  [| fileName |]
        ReferencedProjects = [| |]
        OtherOptions = [| |]
        IsIncompleteTypeCheckEnvironment = true
        UseScriptResolutionRules = false
        LoadTime = DateTime.MaxValue
        OriginalLoadReferences = []
        UnresolvedReferences = None
        Stamp = None
    }

    member private this.VerifyNoBraceMatch(fileContents: string, marker: string) =
        let sourceText = SourceText.From(fileContents)
        let position = fileContents.IndexOf(marker)
        Assert.IsTrue(position >= 0, "Cannot find marker '{0}' in file contents", marker)

        let parsingOptions, _ = checker.GetParsingOptionsFromProjectOptions projectOptions
        match FSharpBraceMatchingService.GetBraceMatchingResult(checker, sourceText, fileName, parsingOptions, position, "UnitTest") |> Async.RunImmediate with
        | None -> ()
        | Some(left, right) -> Assert.Fail("Found match for brace '{0}'", marker)
        
    member private this.VerifyBraceMatch(fileContents: string, startMarker: string, endMarker: string) =
        let sourceText = SourceText.From(fileContents)
        let startMarkerPosition = fileContents.IndexOf(startMarker)
        let endMarkerPosition = fileContents.IndexOf(endMarker)

        Assert.IsTrue(startMarkerPosition >= 0, "Cannot find start marker '{0}' in file contents", startMarkerPosition)
        Assert.IsTrue(endMarkerPosition >= 0, "Cannot find end marker '{0}' in file contents", endMarkerPosition)
        
        let parsingOptions, _ = checker.GetParsingOptionsFromProjectOptions projectOptions
        match FSharpBraceMatchingService.GetBraceMatchingResult(checker, sourceText, fileName, parsingOptions, startMarkerPosition, "UnitTest") |> Async.RunImmediate with
        | None -> Assert.Fail("Didn't find a match for start brace at position '{0}", startMarkerPosition)
        | Some(left, right) ->
            let endPositionInRange(range) = 
                let span = RoslynHelpers.FSharpRangeToTextSpan(sourceText, range)
                span.Start <= endMarkerPosition && endMarkerPosition <= span.End
            Assert.IsTrue(endPositionInRange(left) || endPositionInRange(right), "Found end match at incorrect position")
        

    // Starting Brace
    [<TestCase("(marker1", ")marker1")>]
    [<TestCase("{marker2", "}marker2")>]
    [<TestCase("(marker3", ")marker3")>]
    [<TestCase("[marker4", "]marker4")>]
    // Ending Brace
    [<TestCase(")marker1", "(marker1")>]
    [<TestCase("}marker2", "{marker2")>]
    [<TestCase(")marker3", "(marker3")>]
    [<TestCase("]marker4", "[marker4")>]
    member this.NestedBrackets(startMarker: string, endMarker: string) = 
        let code = "
            (marker1
            {marker2
            (marker3
            [marker4
            ]marker4
            )marker3
            }marker2
            )marker1"
        this.VerifyBraceMatch(code, startMarker, endMarker)

    [<Test>]
    member this.BracketInExpression() = 
        this.VerifyBraceMatch("let x = (3*5)-1", "(3*", ")-1")

    [<Test>]
    member this.BraceInInterpolatedStringSimple() = 
        this.VerifyBraceMatch("let x = $\"abc{1}def\"", "{1", "}def")

    [<Test>]
    member this.BraceInInterpolatedStringTwoHoles() = 
        this.VerifyBraceMatch("let x = $\"abc{1}def{2+3}hij\"", "{2", "}hij")

    [<Test>]
    member this.BraceInInterpolatedStringNestedRecord() = 
        this.VerifyBraceMatch("let x = $\"abc{ id{contents=3}.contents }\"", "{contents", "}.contents")
        this.VerifyBraceMatch("let x = $\"abc{ id{contents=3}.contents }\"", "{ id", "}\"")

    [<TestCase("[start")>]
    [<TestCase("]end")>]
    member this.BraceInMultiLineCommentShouldNotBeMatched(startMarker: string) = 
        let code = "
            let x = 3
            (* This [start
            is a multiline
            comment ]end
            *)
            printf \"%d\" x"
        this.VerifyNoBraceMatch(code, startMarker)
        
    [<Test>]
    member this.BraceInAttributesMatch() = 
        let code = "
            [<Attribute>]
            module internal name"
        this.VerifyBraceMatch(code, "[<", ">]")
        
    [<Test>]
    member this.BraceEncapsulatingACommentShouldBeMatched() = 
        let code = "
            let x = 3 + (start
            (* this  is a comment *)
            )end"
        this.VerifyBraceMatch(code, "(start", ")end")
        
    [<TestCase("(endsInComment")>]
    [<TestCase(")endsInComment")>]
    [<TestCase("<startsInComment")>]
    [<TestCase(">startsInComment")>]
    member this.BraceStartingOrEndingInCommentShouldNotBeMatched(startMarker: string) = 
        let code = "
            let x = 123 + (endsInComment
            (* )endsInComment <startsInComment *)
            >startsInComment"
        this.VerifyNoBraceMatch(code, startMarker)
        
    [<TestCase("(endsInDisabledCode")>]
    [<TestCase(")endsInDisabledCode")>]
    [<TestCase("<startsInDisabledCode")>]
    [<TestCase(">startsInDisabledCode")>]
    member this.BraceStartingOrEndingInDisabledCodeShouldNotBeMatched(startMarker: string) = 
        let code = "
            let x = 123 + (endsInDisabledCode
            #if UNDEFINED
            )endsInDisabledCode <startsInDisabledCode
            #endif
            >startsInDisabledCode"
        this.VerifyNoBraceMatch(code, startMarker)
        
    [<TestCase("(endsInString")>]
    [<TestCase(")endsInString")>]
    [<TestCase("<startsInString")>]
    [<TestCase(">startsInString")>]
    member this.BraceStartingOrEndingInStringShouldNotBeMatched(startMarker: string) = 
        let code = "
            let x = \"stringValue\" + (endsInString +
            \" )endsInString <startsInString \" +
            + >startsInString"
        this.VerifyNoBraceMatch(code, startMarker)
        
    [<Test>]
    member this.BraceMatchingAtEndOfLine_Bug1597() = 
        // https://github.com/Microsoft/visualfsharp/issues/1597
        let code = """
[<EntryPoint>]
let main argv = 
    let arg1 = ""
    let arg2 = ""
    let arg3 = ""
    (printfn "%A '%A' '%A'" (arg1) (arg2) (arg3))endBrace
    0 // return an integer exit code"""
        this.VerifyBraceMatch(code, "(printfn", ")endBrace")
        
    [<TestCase ("let a1 = [ 0 .. 100 ]", [|9;20|])>]
    [<TestCase ("let a2 = [| 0 .. 100 |]", [|9;10;22|])>]
    [<TestCase ("let a3 = <@ 0 @>", [|9;10;15|])>]
    [<TestCase ("let a4 = <@@ 0 @@>", [|9;10;11;15;16|])>]
    [<TestCase ("let a6 = (  ()  )", [|9;16|])>]
    [<TestCase ("[<ReflectedDefinition>]\nlet a7 = 70", [|0;1;22|])>]
    [<TestCase ("let a8 = seq { yield() }", [|13;23|])>]
    member this.DoNotMatchOnInnerSide(fileContents: string, matchingPositions: int[]) =
        let sourceText = SourceText.From(fileContents)
        let parsingOptions, _ = checker.GetParsingOptionsFromProjectOptions projectOptions
        
        for position in matchingPositions do
            match FSharpBraceMatchingService.GetBraceMatchingResult(checker, sourceText, fileName, parsingOptions, position, "UnitTest") |> Async.RunSynchronously with
            | Some _ -> ()
            | None ->
                match position with
                | 0 -> ""
                | _ -> fileContents.[position - 1] |> sprintf " (previous character '%c')"
                |> sprintf "Didn't find a matching brace at position '%d' %s" position
                |> Assert.Fail
