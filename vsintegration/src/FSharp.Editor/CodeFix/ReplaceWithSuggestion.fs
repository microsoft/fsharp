// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System.Composition
open System.Threading.Tasks

open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.CodeFixes

open Microsoft.FSharp.Compiler
open Microsoft.FSharp.Compiler.SourceCodeServices

module M =
    let foo() = ()

    let f = foo()


[<ExportCodeFixProvider(FSharpConstants.FSharpLanguageName, Name = "ReplaceWithSuggestion"); Shared>]
type internal FSharpReplaceWithSuggestionCodeFixProvider
    [<ImportingConstructor>]
    (
        checkerProvider: FSharpCheckerProvider, 
        projectInfoManager: FSharpProjectOptionsManager,
        settings: EditorOptions
    ) =
    inherit CodeFixProvider()
    static let userOpName = "ReplaceWithSuggestionCodeFix"
    let fixableDiagnosticIds = set ["FS0039"; "FS1129"; "FS0495"]
    let checker = checkerProvider.Checker
        
    override __.FixableDiagnosticIds = Seq.toImmutableArray fixableDiagnosticIds

    override __.RegisterCodeFixesAsync context : Task =
        asyncMaybe {
            do! Option.guard settings.CodeFixes.SuggestNamesForErrors

            let document = context.Document
            let! _, projectOptions = projectInfoManager.TryGetOptionsForEditingDocumentOrProject(document, context.CancellationToken)
            let! _, _, checkFileResults = checker.ParseAndCheckDocument(document, projectOptions, userOpName=userOpName)
            
            let! allSymbolUses = checkFileResults.GetAllUsesOfAllSymbolsInFile() |> liftAsync
            let! sourceText = document.GetTextAsync(context.CancellationToken)
            let unresolvedIdentifierText = sourceText.GetSubText(context.Span).ToString()
                
            let suggestedNames = ErrorResolutionHints.getSuggestedNames allSymbolUses unresolvedIdentifierText

            match suggestedNames with
            | None -> ()
            | Some suggestions ->
                let diagnostics =
                    context.Diagnostics
                    |> Seq.filter (fun x -> fixableDiagnosticIds |> Set.contains x.Id)
                    |> Seq.toImmutableArray

                for suggestion in suggestions do
                    let replacement = Keywords.QuoteIdentifierIfNeeded suggestion
                    let codeFix = 
                        CodeFixHelpers.createTextChangeCodeFix(
                            FSComp.SR.replaceWithSuggestion suggestion,
                            context,
                            (fun () -> asyncMaybe.Return [| TextChange(context.Span, replacement) |]))
                
                    context.RegisterCodeFix(codeFix, diagnostics)
        }
        |> Async.Ignore
        |> RoslynHelpers.StartAsyncUnitAsTask(context.CancellationToken)
