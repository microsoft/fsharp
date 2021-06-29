// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.
namespace Microsoft.VisualStudio.FSharp.Editor.Tests.Roslyn

open System
open System.Threading

open NUnit.Framework

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Classification
open Microsoft.CodeAnalysis.Editor
open Microsoft.CodeAnalysis.Text

open Microsoft.VisualStudio.FSharp.Editor
open Microsoft.VisualStudio.FSharp.LanguageService

open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text

open UnitTests.TestLib.LanguageService

[<TestFixture>][<Category "Roslyn Services">]
type DocumentDiagnosticAnalyzerTests()  =
    let filePath = "C:\\test.fs"
    let startMarker = "(*start*)"
    let endMarker = "(*end*)"
    let projectOptions: FSharpProjectOptions = { 
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

    let getDiagnostics (fileContents: string) = 
        async {
            let document, _ = RoslynTestHelpers.CreateDocument(filePath, fileContents)
            let! syntacticDiagnostics = FSharpDocumentDiagnosticAnalyzer.GetDiagnostics(document, DiagnosticsType.Syntax) 
            let! semanticDiagnostics = FSharpDocumentDiagnosticAnalyzer.GetDiagnostics(document, DiagnosticsType.Semantic) 
            return syntacticDiagnostics.AddRange(semanticDiagnostics)
        } |> Async.RunSynchronously

    member private this.VerifyNoErrors(fileContents: string, ?additionalFlags: string[]) =
        let parsingOptions, _ = checker.GetParsingOptionsFromProjectOptions projectOptions
        let additionalOptions = match additionalFlags with
                                | None -> projectOptions
                                | Some(flags) -> {projectOptions with OtherOptions = Array.append projectOptions.OtherOptions flags}

        let errors = getDiagnostics fileContents
        if not errors.IsEmpty then
            Assert.Fail("There should be no errors generated", errors)

    member private this.VerifyErrorAtMarker(fileContents: string, expectedMarker: string, ?expectedMessage: string) =
        let errors = getDiagnostics fileContents |> Seq.filter(fun e -> e.Severity = DiagnosticSeverity.Error) |> Seq.toArray
        Assert.AreEqual(1, errors.Length, "There should be exactly one error generated")
        let actualError = errors.[0]
        if expectedMessage.IsSome then
            Assert.AreEqual(expectedMessage.Value, actualError.GetMessage(), "Error messages should match")
        Assert.AreEqual(DiagnosticSeverity.Error, actualError.Severity)
        let expectedStart = fileContents.IndexOf(expectedMarker)
        Assert.AreEqual(expectedStart, actualError.Location.SourceSpan.Start, "Error start positions should match")
        let expectedEnd = expectedStart + expectedMarker.Length
        Assert.AreEqual(expectedEnd, actualError.Location.SourceSpan.End, "Error end positions should match")

    member private this.VerifyDiagnosticBetweenMarkers(fileContents: string, expectedMessage: string, expectedSeverity: DiagnosticSeverity) =
        let errors = getDiagnostics fileContents |> Seq.filter(fun e -> e.Severity = expectedSeverity) |> Seq.toArray
        Assert.AreEqual(1, errors.Length, "There should be exactly one error generated")
        let actualError = errors.[0]
        Assert.AreEqual(expectedSeverity, actualError.Severity)
        Assert.AreEqual(expectedMessage, actualError.GetMessage(), "Error messages should match")
        let expectedStart = fileContents.IndexOf(startMarker) + startMarker.Length
        Assert.AreEqual(expectedStart, actualError.Location.SourceSpan.Start, "Error start positions should match")
        let expectedEnd = fileContents.IndexOf(endMarker)
        Assert.AreEqual(expectedEnd, actualError.Location.SourceSpan.End, "Error end positions should match")
        
    member private this.VerifyErrorBetweenMarkers(fileContents: string, expectedMessage: string) =
        this.VerifyDiagnosticBetweenMarkers(fileContents, expectedMessage, DiagnosticSeverity.Error)
        
    member private this.VerifyWarningBetweenMarkers(fileContents: string, expectedMessage: string) =
        this.VerifyDiagnosticBetweenMarkers(fileContents, expectedMessage, DiagnosticSeverity.Warning)


    [<Test>]
    member public this.Error_Expression_IllegalIntegerLiteral() = 
        this.VerifyErrorBetweenMarkers(
            fileContents = """
let _ = 1
let a = 0.1(*start*).(*end*)0
                """,
            expectedMessage = "Missing qualification after '.'") 
                     
    [<Test>]
    member public this.Error_Expression_IncompleteDefine() = 
        this.VerifyErrorBetweenMarkers(
            fileContents = """
let a = (*start*);(*end*)
                """,
            expectedMessage = "Unexpected symbol ';' in binding")
                     
    [<Test>]
    member public this.Error_Expression_KeywordAsValue() = 
        this.VerifyErrorBetweenMarkers(
            fileContents = """
let b =
(*start*)type(*end*)
                """,
            expectedMessage = "Incomplete structured construct at or before this point in binding") 
                     
    [<Test>]
    member public this.Error_Type_WithoutName() = 
        this.VerifyErrorBetweenMarkers(
            fileContents = """
type (*start*)=(*end*)
                """,
            expectedMessage = "Unexpected symbol '=' in type name")
                     
    [<Test>]
    member public this.AbstractClasses_Constructors_PositiveTests_1() = 
        this.VerifyNoErrors("""
[<AbstractClass>]
type C(a : int) = 
    new(a : string) = C(int a)
    new(b) = match b with Some _ -> C(1) | _ -> C("")
            """)

    [<Test>]
    member public this.AbstractClasses_Constructors_PositiveTests_2() = 
        this.VerifyNoErrors("""
[<AbstractClass>]
type C(a : int) = 
    new(a : string) = new C(int a)
    new(b) = match b with Some _ -> new C(1) | _ -> new C("")
            """)

    [<Test>]
    member public this.AbstractClasses_Constructors_PositiveTests_3() = 
        this.VerifyNoErrors("""
[<AbstractClass>]
type O(o : int) = 
    new() = O(1)
            """)

    [<Test>]
    member public this.AbstractClasses_Constructors_PositiveTests_4() = 
        this.VerifyNoErrors("""
[<AbstractClass>]
type O(o : int) = 
    new() = O() then printfn "A"
            """)

    [<Test>]
    member public this.AbstractClasses_Constructors_PositiveTests_5() = 
        this.VerifyNoErrors("""
[<AbstractClass>]
type O(o : int) = 
    new() = new O(1) then printfn "A"
            """)

    [<Test>]
    member public this.AbstractClasses_Constructors_PositiveTests_6() = 
        this.VerifyNoErrors("""
[<AbstractClass>]
type D() = class end
[<AbstractClass>]
type E = 
    inherit D
    new() = { inherit D(); }
            """)
            
    [<Test>]
    member public this.AbstractClasses_Constructors_NegativeTests_1() = 
        this.VerifyErrorAtMarker(
            fileContents = """
[<AbstractClass>]
type D = 
    val d : D
    new() = {d = D()}
            """,
            expectedMarker = "D()")
            
    [<Test>]
    member public this.AbstractClasses_Constructors_NegativeTests_2() = 
        this.VerifyErrorAtMarker(
            fileContents = """
[<AbstractClass>]
type Z () = 
    new(a : int) = 
        Z(10) then ignore(Z())
            """,
            expectedMarker = "Z()")
            
    [<Test>]
    member public this.AbstractClasses_Constructors_NegativeTests_3() = 
        this.VerifyErrorAtMarker(
            fileContents = """
[<AbstractClass>]
type X() = 
    member val V : bool = true
    abstract member Test: int -> unit

type Y() =
    member val M : bool = (new X()).V
            """,
            expectedMarker = "new X()")
                     
    [<Test>]
    member public this.Waring_Construct_TypeMatchWithoutAnnotation() = 
        this.VerifyWarningBetweenMarkers(
            fileContents = """
let f () = 
    let g1 (x:'a) = x
    let g2 (y:'a) = ((*start*)y(*end*):string)
    g1 3, g1 "3", g2 "4" 
                """,
            expectedMessage = "This construct causes code to be less generic " +
                              "than indicated by the type annotations. The " +
                              "type variable 'a has been constrained to be " +
                              "type 'string'.")

    [<Test>]
    member public this.Error_Identifer_IllegalFloatPointLiteral() = 
        this.VerifyErrorBetweenMarkers(
            fileContents = """
let x: float = 1.2(*start*).(*end*)3
            """,
            expectedMessage = "Missing qualification after '.'")
            
    [<Test>]
    member public this.Error_TypeCheck_ParseError_Bug67133() = 
        this.VerifyErrorBetweenMarkers(
            fileContents = """
let gDateTime (arr: (*start*)DateTime(*end*)[]) =
    arr.[0]
            """,
            expectedMessage = "The type 'DateTime' is not defined.")
            
    [<Test>]
    member public this.Error_CyclicalDeclarationDoesNotCrash() = 
        this.VerifyErrorBetweenMarkers(
            fileContents = """
type (*start*)A(*end*) = int * A
            """,
            expectedMessage = "This type definition involves an immediate cyclic reference through an abbreviation")
            
    [<Test>]
    member public this.Warning_FlagsAndSettings_TargetOptionsRespected() = 
        this.VerifyWarningBetweenMarkers(
            fileContents = """
[<System.Obsolete("x")>]
let fn x = 0
let y = (*start*)fn(*end*) 1
            """,
            expectedMessage = "This construct is deprecated. x")

    [<Test>]
    member public this.Basic_Case() =
        this.VerifyErrorBetweenMarkers(
            fileContents = """
let x = 3
let y = (*start*)x(*end*) 4
let arr = [| 1; 2; 3 |]
            """,
            expectedMessage = "This value is not a function and cannot be applied.")

    [<Test>]
    member public this.Multiline_Bug5449() =
        this.VerifyErrorBetweenMarkers(
            fileContents = """
let f x = x + 1
let r = (*start*)f 3(*end*) 4
            """,
            expectedMessage = "This value is not a function and cannot be applied.")

    [<Test>]
    member public this.InComputationExpression_Bug6095_A() =
        this.VerifyWarningBetweenMarkers(
            fileContents = """
let a = async {
    let! (*start*)[| r1; r2 |](*end*) = Async.Parallel [| async.Return(1); async.Return(2) |]
    let yyyy = 4
    return r1,r2
}
            """,
            expectedMessage = "Incomplete pattern matches on this expression. For example, the value '[|_; _; _|]' may indicate a case not covered by the pattern(s).")

    [<Test>]
    member public this.InComputationExpression_Bug6095_B() =
        this.VerifyWarningBetweenMarkers(
            fileContents = """
let f = (*start*)function(*end*) | [| a;b |] -> ()
            """,
            expectedMessage = "Incomplete pattern matches on this expression. For example, the value '[|_; _; _|]' may indicate a case not covered by the pattern(s).")

    [<Test>]
    member public this.InComputationExpression_Bug6095_C() =
        this.VerifyWarningBetweenMarkers(
            fileContents = """
for (*start*)[|a;b|](*end*) in [| [|42|] |] do ()
            """,
            expectedMessage = "Incomplete pattern matches on this expression. For example, the value '[|_; _; _|]' may indicate a case not covered by the pattern(s). Unmatched elements will be ignored.")
            
    [<Test>]
    member public this.InComputationExpression_Bug914685() =
        this.VerifyErrorAtMarker(
            fileContents = """
async { if true then return 1 } |> ignore
            """,
            expectedMarker = "if true then")

    [<Test>]
    member public this.ExtraEndif() =
        this.VerifyErrorBetweenMarkers(
            fileContents = """
#if UNDEFINED
1
#else
0
#endif
(*start*)#endif(*end*)
            """,
            expectedMessage = "#endif has no matching #if in implementation file")
            
    [<Test>]
    member public this.Squiggles_HashNotFirstSymbol_If() =
        this.VerifyErrorAtMarker(
            fileContents = """
(*comment*) #if UNDEFINED
1
#else
0
#endif
            """,
            expectedMarker = " #if UNDEFINED",
            expectedMessage = "#if directive must appear as the first non-whitespace character on a line")
            
    [<Test>]
    member public this.Squiggles_HashNotFirstSymbol_Endif() =
        this.VerifyErrorAtMarker(
            fileContents = """
#if DEBUG
1
#else
0
(*comment*) #endif
            """,
            expectedMarker = " #endif",
            expectedMessage = "#endif directive must appear as the first non-whitespace character on a line")
            
    [<Test>]
    member public this.Squiggles_HashIfWithMultilineComment() =
        this.VerifyErrorAtMarker(
            fileContents = """
#if DEBUG (*comment*)
#endif
            """,
            expectedMarker = "(*comment*)",
            expectedMessage = "Expected single line comment or end of line")
            
    [<Test>]
    member public this.Squiggles_HashIfWithUnexpected() =
        this.VerifyErrorAtMarker(
            fileContents = """
#if DEBUG TEST
#endif
            """,
            expectedMarker = "TEST",
            expectedMessage = "Incomplete preprocessor expression")
            
    [<Test>]
    member public this.OverloadsAndExtensionMethodsForGenericTypes() =
        this.VerifyNoErrors(
            fileContents = """
open System.Linq

type T = 
    abstract Count : int -> bool
    default this.Count(_ : int) = true

    interface System.Collections.Generic.IEnumerable<int> with
        member this.GetEnumerator() : System.Collections.Generic.IEnumerator<int> = failwith "not implemented"
    interface System.Collections.IEnumerable with
        member this.GetEnumerator() : System.Collections.IEnumerator = failwith "not implemented"

let g (t : T) = t.Count()
            """)
            
    [<Test>]
    member public this.DocumentDiagnosticsDontReportProjectErrors_Bug1596() =
        // https://github.com/Microsoft/visualfsharp/issues/1596
        this.VerifyNoErrors(
            fileContents = """
let x = 3
printf "%d" x
            """,
            additionalFlags = [| "--times" |])
