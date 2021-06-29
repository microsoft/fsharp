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

[<ExportCodeRefactoringProvider(FSharpConstants.FSharpLanguageName, Name = "ChangeTypeofWithNameToNameofExpression"); Shared>]
type internal FSharpChangeTypeofWithNameToNameofExpressionRefactoring
    [<ImportingConstructor>]
    (
    ) =
    inherit CodeRefactoringProvider()

    override _.ComputeRefactoringsAsync context =
        asyncMaybe {
            let document = context.Document
            let! sourceText = context.Document.GetTextAsync(context.CancellationToken)
            let! parseResults = document.GetFSharpParseResultsAsync(nameof(FSharpChangeTypeofWithNameToNameofExpressionRefactoring)) |> liftAsync

            let selectionRange = RoslynHelpers.TextSpanToFSharpRange(document.FilePath, context.Span, sourceText)
            let! namedTypeOfResults = parseResults.TryRangeOfTypeofWithNameAndTypeExpr(selectionRange.Start)

            let! namedTypeSpan = RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, namedTypeOfResults.NamedIdentRange)
            let! typeofAndNameSpan = RoslynHelpers.TryFSharpRangeToTextSpan(sourceText,namedTypeOfResults.FullExpressionRange)
            let namedTypeName = sourceText.GetSubText(namedTypeSpan)
            let replacementString = $"nameof({namedTypeName})"

            let title = SR.UseNameof()

            let getChangedText (sourceText: SourceText) =
                sourceText.WithChanges(TextChange(typeofAndNameSpan, replacementString))

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
