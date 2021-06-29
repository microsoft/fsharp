// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System.Composition

open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.CodeFixes

[<ExportCodeFixProvider(FSharpConstants.FSharpLanguageName, Name = "ConvertCSharpLambdaToFSharpLambda"); Shared>]
type internal FSharpConvertCSharpLambdaToFSharpLambdaCodeFixProvider
    [<ImportingConstructor>]
    (
    ) =
    inherit CodeFixProvider()

    let fixableDiagnosticIds = set ["FS0039"; "FS0043"]

    override _.FixableDiagnosticIds = Seq.toImmutableArray fixableDiagnosticIds

    override _.RegisterCodeFixesAsync context =
        asyncMaybe {
            let! parseResults = context.Document.GetFSharpParseResultsAsync(nameof(FSharpConvertCSharpLambdaToFSharpLambdaCodeFixProvider)) |> liftAsync

            let! sourceText = context.Document.GetTextAsync(context.CancellationToken)
            let errorRange = RoslynHelpers.TextSpanToFSharpRange(context.Document.FilePath, context.Span, sourceText)

            let! fullParenRange, lambdaArgRange, lambdaBodyRange = parseResults.TryRangeOfParenEnclosingOpEqualsGreaterUsage errorRange.Start

            let! fullParenSpan = RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, fullParenRange)
            let! lambdaArgSpan = RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, lambdaArgRange)
            let! lambdaBodySpan = RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, lambdaBodyRange)

            let replacement =
                let argText = sourceText.GetSubText(lambdaArgSpan).ToString()
                let bodyText = sourceText.GetSubText(lambdaBodySpan).ToString()
                TextChange(fullParenSpan, "fun " + argText + " -> " + bodyText)

            let diagnostics =
                context.Diagnostics
                |> Seq.filter (fun x -> fixableDiagnosticIds |> Set.contains x.Id)
                |> Seq.toImmutableArray

            let title = SR.UseFSharpLambda()

            let codeFix =
                CodeFixHelpers.createTextChangeCodeFix(
                    title,
                    context,
                    (fun () -> asyncMaybe.Return [| replacement |]))

            context.RegisterCodeFix(codeFix, diagnostics)
        }
        |> Async.Ignore
        |> RoslynHelpers.StartAsyncUnitAsTask(context.CancellationToken) 
