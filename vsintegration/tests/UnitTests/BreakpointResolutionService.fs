// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.
namespace Microsoft.VisualStudio.FSharp.Editor.Tests.Roslyn

open System
open System.Threading

open NUnit.Framework

open Microsoft.CodeAnalysis.Classification
open Microsoft.CodeAnalysis.Editor
open Microsoft.CodeAnalysis.Text

open Microsoft.VisualStudio.FSharp.Editor
open Microsoft.VisualStudio.FSharp.LanguageService

open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text

open UnitTests.TestLib.LanguageService

[<TestFixture>][<Category "Roslyn Services">]
type BreakpointResolutionServiceTests()  =

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
    let code = "
// This is a comment

type exampleType(parameter: int) =
    member this.exampleMember = parameter

[<EntryPoint>]
let main argv = 
    let integerValue = 123456
    let stringValue = \"This is a string\"
    let objectValue = exampleType(789)

    printfn \"%d %s %A\" integerValue stringValue objectValue

    let booleanValue = true
    match booleanValue with
    | true -> printfn \"correct\"
    | false -> printfn \"wrong\"

    0 // return an integer exit code
    "
    
    static member private testCases: Object[][] = [|
        [| "This is a comment";         None |]
        [| "123456";                    Some("let integerValue = 123456") |]
        [| "stringValue";               Some("let stringValue = \"This is a string\"") |]
        [| "789";                       Some("let objectValue = exampleType(789)") |]
        [| "correct";                   Some("printfn \"correct\"") |]
        [| "wrong";                     Some("printfn \"wrong\"") |]
        [| "0";                         Some("0") |]
    |]

    [<TestCaseSource("testCases")>]
    member this.TestBreakpointResolution(searchToken: string, expectedResolution: string option) = 
        let searchPosition = code.IndexOf(searchToken)
        Assert.IsTrue(searchPosition >= 0, "SearchToken '{0}' is not found in code", searchToken)
        
        let document, sourceText = RoslynTestHelpers.CreateDocument(fileName, code)
        let searchSpan = TextSpan.FromBounds(searchPosition, searchPosition + searchToken.Length)

        let actualResolutionOption = FSharpBreakpointResolutionService.GetBreakpointLocation(document, searchSpan) |> Async.RunSynchronously
        
        match actualResolutionOption with
        | None -> Assert.IsTrue(expectedResolution.IsNone, "BreakpointResolutionService failed to resolve breakpoint position")
        | Some(actualResolutionRange) ->
            let actualResolution = sourceText.GetSubText(RoslynHelpers.FSharpRangeToTextSpan(sourceText, actualResolutionRange)).ToString()
            Assert.IsTrue(expectedResolution.IsSome, "BreakpointResolutionService resolved a breakpoint while it shouldn't at: {0}", actualResolution)
            Assert.AreEqual(expectedResolution.Value, actualResolution, "Expected and actual resolutions should match")
    