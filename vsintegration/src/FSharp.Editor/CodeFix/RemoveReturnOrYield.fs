// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System.Composition

open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.CodeFixes

[<ExportCodeFixProvider(FSharpConstants.FSharpLanguageName, Name = "RemoveReturnOrYield"); Shared>]
type internal FSharpRemoveReturnOrYieldCodeFixProvider
    [<ImportingConstructor>]
    (
    ) =
    inherit CodeFixProvider()

    let fixableDiagnosticIds = set ["FS0748"; "FS0747"]

    override _.FixableDiagnosticIds = Seq.toImmutableArray fixableDiagnosticIds

    override _.RegisterCodeFixesAsync context =
        asyncMaybe {
            let! parseResults = context.Document.GetFSharpParseResultsAsync(nameof(FSharpRemoveReturnOrYieldCodeFixProvider)) |> liftAsync

            let! sourceText = context.Document.GetTextAsync(context.CancellationToken)
            let errorRange = RoslynHelpers.TextSpanToFSharpRange(context.Document.FilePath, context.Span, sourceText)
            let! exprRange = parseResults.TryRangeOfExprInYieldOrReturn errorRange.Start
            let! exprSpan = RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, exprRange)

            let diagnostics =
                context.Diagnostics
                |> Seq.filter (fun x -> fixableDiagnosticIds |> Set.contains x.Id)
                |> Seq.toImmutableArray

            let title =
                let text = sourceText.GetSubText(context.Span).ToString()
                if text.StartsWith("return!") then
                    SR.RemoveReturnBang()
                elif text.StartsWith("return") then
                    SR.RemoveReturn()
                elif text.StartsWith("yield!") then
                    SR.RemoveYieldBang()
                else
                    SR.RemoveYield()

            let codeFix =
                CodeFixHelpers.createTextChangeCodeFix(
                    title,
                    context,
                    (fun () -> asyncMaybe.Return [| TextChange(context.Span, sourceText.GetSubText(exprSpan).ToString()) |]))

            context.RegisterCodeFix(codeFix, diagnostics)
        }
        |> Async.Ignore
        |> RoslynHelpers.StartAsyncUnitAsTask(context.CancellationToken) 
