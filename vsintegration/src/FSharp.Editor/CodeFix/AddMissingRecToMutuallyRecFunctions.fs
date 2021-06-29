// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Collections.Immutable
open System.Composition
open System.Threading

open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.CodeFixes

open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.CodeActions

[<ExportCodeFixProvider(FSharpConstants.FSharpLanguageName, Name = "AddMissingRecToMutuallyRecFunctions"); Shared>]
type internal FSharpAddMissingRecToMutuallyRecFunctionsCodeFixProvider
    [<ImportingConstructor>]
    (
    ) =
    inherit CodeFixProvider()

    let fixableDiagnosticIds = set ["FS0576"]

    let createCodeFix (context: CodeFixContext, symbolName: string, titleFormat: string, textChange: TextChange, diagnostics: ImmutableArray<Diagnostic>) =
        let title = String.Format(titleFormat, symbolName)
        let codeAction =
            CodeAction.Create(
                title,
                (fun (cancellationToken: CancellationToken) ->
                    async {
                        let cancellationToken = context.CancellationToken
                        let! sourceText = context.Document.GetTextAsync(cancellationToken) |> Async.AwaitTask
                        return context.Document.WithText(sourceText.WithChanges(textChange))
                    } |> RoslynHelpers.StartAsyncAsTask(cancellationToken)),
                title)
        context.RegisterCodeFix(codeAction, diagnostics)

    override _.FixableDiagnosticIds = Seq.toImmutableArray fixableDiagnosticIds

    override _.RegisterCodeFixesAsync context =
        asyncMaybe {
            let! defines = context.Document.GetFSharpCompilationDefinesAsync(nameof(FSharpAddMissingRecToMutuallyRecFunctionsCodeFixProvider)) |> liftAsync
            let! sourceText = context.Document.GetTextAsync(context.CancellationToken)

            let funcStartPos =
                let rec loop ch pos =
                    if not (Char.IsWhiteSpace(ch)) then
                        pos
                    else
                        loop sourceText.[pos + 1] (pos + 1)

                loop sourceText.[context.Span.End + 1] (context.Span.End  + 1)

            let! funcLexerSymbol = Tokenizer.getSymbolAtPosition (context.Document.Id, sourceText, funcStartPos, context.Document.FilePath, defines, SymbolLookupKind.Greedy, false, false)
            let! funcNameSpan = RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, funcLexerSymbol.Range)
            let funcName = sourceText.GetSubText(funcNameSpan).ToString()

            let diagnostics =
                context.Diagnostics
                |> Seq.filter (fun x -> fixableDiagnosticIds |> Set.contains x.Id)
                |> Seq.toImmutableArray

            createCodeFix(context, funcName, SR.MakeOuterBindingRecursive(), TextChange(TextSpan(context.Span.End, 0), " rec"), diagnostics)
        }
        |> Async.Ignore
        |> RoslynHelpers.StartAsyncUnitAsTask(context.CancellationToken) 
