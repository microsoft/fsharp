// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Composition

open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.CodeFixes

open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis

[<ExportCodeFixProvider(FSharpConstants.FSharpLanguageName, Name = "AddMissingFunKeyword"); Shared>]
type internal FSharpAddMissingFunKeywordCodeFixProvider
    [<ImportingConstructor>]
    (
    ) =
    inherit CodeFixProvider()

    let fixableDiagnosticIds = set ["FS0010"]

    override _.FixableDiagnosticIds = Seq.toImmutableArray fixableDiagnosticIds

    override _.RegisterCodeFixesAsync context =
        asyncMaybe {
            let document = context.Document
            let! sourceText = context.Document.GetTextAsync(context.CancellationToken)
            let textOfError = sourceText.GetSubText(context.Span).ToString()

            // Only trigger when failing to parse `->`, which arises when `fun` is missing
            do! Option.guard (textOfError = "->")

            let! defines = document.GetFSharpCompilationDefinesAsync(nameof(FSharpAddMissingFunKeywordCodeFixProvider)) |> liftAsync

            let adjustedPosition =
                let rec loop ch pos =
                    if not (Char.IsWhiteSpace(ch)) then
                        pos
                    else
                        loop sourceText.[pos] (pos - 1)

                loop sourceText.[context.Span.Start - 1] context.Span.Start

            let! intendedArgLexerSymbol = Tokenizer.getSymbolAtPosition (document.Id, sourceText, adjustedPosition, document.FilePath, defines, SymbolLookupKind.Greedy, false, false)
            let! intendedArgSpan = RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, intendedArgLexerSymbol.Range)

            let diagnostics =
                context.Diagnostics
                |> Seq.filter (fun x -> fixableDiagnosticIds |> Set.contains x.Id)
                |> Seq.toImmutableArray

            let title = SR.AddMissingFunKeyword()

            let codeFix =
                CodeFixHelpers.createTextChangeCodeFix(
                    title,
                    context,
                    (fun () -> asyncMaybe.Return [| TextChange(TextSpan(intendedArgSpan.Start, 0), "fun ") |]))

            context.RegisterCodeFix(codeFix, diagnostics)
        }
        |> Async.Ignore
        |> RoslynHelpers.StartAsyncUnitAsTask(context.CancellationToken) 
