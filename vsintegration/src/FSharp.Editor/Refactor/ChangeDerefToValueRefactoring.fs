// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Composition
open System.Threading

open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open FSharp.Compiler.Syntax

open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.CodeRefactorings
open Microsoft.CodeAnalysis.CodeActions

[<ExportCodeRefactoringProvider(FSharpConstants.FSharpLanguageName, Name = "ChangeDerefToValue"); Shared>]
type internal FSharpChangeDerefToValueRefactoring
    [<ImportingConstructor>]
    (
    ) =
    inherit CodeRefactoringProvider()

    override _.ComputeRefactoringsAsync context =
        asyncMaybe {
            let document = context.Document
            let! sourceText = context.Document.GetTextAsync(context.CancellationToken)
            let! parseResults = document.GetFSharpParseResultsAsync(nameof(FSharpChangeDerefToValueRefactoring)) |> liftAsync

            let selectionRange = RoslynHelpers.TextSpanToFSharpRange(document.FilePath, context.Span, sourceText)
            let! derefRange = parseResults.TryRangeOfRefCellDereferenceContainingPos selectionRange.Start
            let! exprRange = parseResults.TryRangeOfExpressionBeingDereferencedContainingPos selectionRange.Start

            let combinedRange = Range.unionRanges derefRange exprRange
            let! combinedSpan = RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, combinedRange)
            let replacementString =
                // Trim off the `!`
                sourceText.GetSubText(combinedSpan).ToString().[1..] + ".Value"

            let title = SR.UseValueInsteadOfDeref()

            let getChangedText (sourceText: SourceText) =
                sourceText.WithChanges(TextChange(combinedSpan, replacementString))

            let codeAction =
                CodeAction.Create(
                    title,
                    (fun (cancellationToken: CancellationToken) ->
                        async {
                            let! sourceText = context.Document.GetTextAsync(cancellationToken) |> Async.AwaitTask
                            return context.Document.WithText(getChangedText sourceText)
                        } |> RoslynHelpers.StartAsyncAsTask(cancellationToken)),
                    title)
            context.RegisterRefactoring(codeAction)
        }
        |> Async.Ignore
        |> RoslynHelpers.StartAsyncUnitAsTask(context.CancellationToken)
