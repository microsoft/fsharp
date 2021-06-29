// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System.Composition
open System.Threading
open System.Threading.Tasks

open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.CodeFixes
open Microsoft.CodeAnalysis.CodeActions

[<ExportCodeFixProvider(FSharpConstants.FSharpLanguageName, Name = "ConvertToAnonymousRecord"); Shared>]
type internal FSharpConvertToAnonymousRecordCodeFixProvider
    [<ImportingConstructor>]
    (
    ) =
    inherit CodeFixProvider()

    let fixableDiagnosticIds = set ["FS0039"]

    override _.FixableDiagnosticIds = Seq.toImmutableArray fixableDiagnosticIds

    override _.RegisterCodeFixesAsync context : Task =
        asyncMaybe {
            let document = context.Document
            let! parseResults = document.GetFSharpParseResultsAsync(nameof(FSharpConvertToAnonymousRecordCodeFixProvider)) |> liftAsync

            let! sourceText = context.Document.GetTextAsync(context.CancellationToken)
            let errorRange = RoslynHelpers.TextSpanToFSharpRange(document.FilePath, context.Span, sourceText)
            let! recordRange = parseResults.TryRangeOfRecordExpressionContainingPos errorRange.Start
            let! recordSpan = RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, recordRange)

            let getChangedText () =
                sourceText.WithChanges(TextChange(TextSpan(recordSpan.Start + 1, 0), "|"))
                          .WithChanges(TextChange(TextSpan(recordSpan.End, 0), "|"))

            let diagnostics =
                context.Diagnostics
                |> Seq.filter (fun x -> fixableDiagnosticIds |> Set.contains x.Id)
                |> Seq.toImmutableArray

            let title = SR.ConvertToAnonymousRecord()

            let codeFix =
                CodeAction.Create(
                    title,
                    (fun (cancellationToken: CancellationToken) ->
                        async {
                            return context.Document.WithText(getChangedText())
                        } |> RoslynHelpers.StartAsyncAsTask(cancellationToken)),
                    title)
                    
            context.RegisterCodeFix(codeFix, diagnostics)
        }
        |> Async.Ignore
        |> RoslynHelpers.StartAsyncUnitAsTask(context.CancellationToken)
