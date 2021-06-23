// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System.Composition
open System.Threading
open System.Threading.Tasks

open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.CodeFixes

open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Text

[<ExportCodeFixProvider(FSharpConstants.FSharpLanguageName, Name = "MakeDeclarationMutable"); Shared>]
type internal FSharpMakeDeclarationMutableFixProvider
    [<ImportingConstructor>]
    (
        checkerProvider: FSharpCheckerProvider, 
        projectInfoManager: FSharpProjectOptionsManager
    ) =
    inherit CodeFixProvider()

    static let userOpName = "MakeDeclarationMutable"

    let fixableDiagnosticIds = set ["FS0027"]

    override _.FixableDiagnosticIds = Seq.toImmutableArray fixableDiagnosticIds

    override _.RegisterCodeFixesAsync context : Task =
        asyncMaybe {
            let diagnostics =
                context.Diagnostics
                |> Seq.filter (fun x -> fixableDiagnosticIds |> Set.contains x.Id)
                |> Seq.toImmutableArray

            let document = context.Document
            do! Option.guard (not(isSignatureFile document.FilePath))
            let position = context.Span.Start
            let checker = checkerProvider.Checker
            let! parsingOptions, projectOptions = projectInfoManager.TryGetOptionsForEditingDocumentOrProject(document, CancellationToken.None, userOpName)
            let! sourceText = document.GetTextAsync () |> liftTaskAsync
            let defines = CompilerEnvironment.GetCompilationDefinesForEditing parsingOptions
            let textLine = sourceText.Lines.GetLineFromPosition position
            let textLinePos = sourceText.Lines.GetLinePosition position
            let fcsTextLineNumber = Line.fromZ textLinePos.Line
            let! parseFileResults, checkFileResults = checker.CheckDocumentInProject(document, projectOptions) |> liftAsync
            let! lexerSymbol = Tokenizer.getSymbolAtPosition (document.Id, sourceText, position, document.FilePath, defines, SymbolLookupKind.Greedy, false, false)
            let decl = checkFileResults.GetDeclarationLocation (fcsTextLineNumber, lexerSymbol.Ident.idRange.EndColumn, textLine.ToString(), lexerSymbol.FullIsland, false)

            match decl with
            // Only do this for symbols in the same file. That covers almost all cases anyways.
            // We really shouldn't encourage making values mutable outside of local scopes anyways.
            | FindDeclResult.DeclFound declRange when declRange.FileName = document.FilePath ->
                let! span = RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, declRange)

                // Bail if it's a parameter, because like, that ain't allowed
                do! Option.guard (not (parseFileResults.IsPositionContainedInACurriedParameter declRange.Start))

                let title = SR.MakeDeclarationMutable()
                let codeFix =
                    CodeFixHelpers.createTextChangeCodeFix(
                        title,
                        context,
                        (fun () -> asyncMaybe.Return [| TextChange(TextSpan(span.Start, 0), "mutable ") |]))

                context.RegisterCodeFix(codeFix, diagnostics)
            | _ ->
                ()
        }
        |> Async.Ignore
        |> RoslynHelpers.StartAsyncUnitAsTask(context.CancellationToken) 