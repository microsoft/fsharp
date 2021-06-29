// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System.Collections.Immutable
open System.Composition
open System.Threading.Tasks

open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.CodeFixes

open Microsoft.VisualStudio.FSharp.Editor.SymbolHelpers
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.Tokenization.FSharpKeywords

[<ExportCodeFixProvider(FSharpConstants.FSharpLanguageName, Name = "FSharpRenameParamToMatchSignature"); Shared>]
type internal FSharpRenameParamToMatchSignature
    [<ImportingConstructor>]
    (
    ) =
    
    inherit CodeFixProvider()

    let fixableDiagnosticIds = ["FS3218"]
        
        
    override _.FixableDiagnosticIds = Seq.toImmutableArray fixableDiagnosticIds

    override _.RegisterCodeFixesAsync context : Task =
        asyncMaybe {
            match context.Diagnostics |> Seq.filter (fun x -> fixableDiagnosticIds |> List.contains x.Id) |> Seq.toList with 
            | [diagnostic] -> 
                    let message = diagnostic.GetMessage()
                    let parts = System.Text.RegularExpressions.Regex.Match(message, ".+'(.+)'.+'(.+)'.+")
                    if parts.Success then
                    
                        let diagnostics = ImmutableArray.Create diagnostic
                        let suggestion = parts.Groups.[1].Value
                        let replacement = QuoteIdentifierIfNeeded suggestion
                        let computeChanges() = 
                            asyncMaybe {
                                let document = context.Document
                                let! cancellationToken = Async.CancellationToken |> liftAsync
                                let! sourceText = document.GetTextAsync(cancellationToken)
                                let! symbolUses = getSymbolUsesOfSymbolAtLocationInDocument (document, context.Span.Start) 
                                let changes = 
                                    [| for symbolUse in symbolUses do
                                            match RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, symbolUse.Range) with 
                                            | None -> ()
                                            | Some span -> 
                                                let textSpan = Tokenizer.fixupSpan(sourceText, span)
                                                yield TextChange(textSpan, replacement) |]
                                return changes 
                            }
                        let title = CompilerDiagnostics.GetErrorMessage (FSharpDiagnosticKind.ReplaceWithSuggestion suggestion)
                        let codefix = CodeFixHelpers.createTextChangeCodeFix(title, context, computeChanges)
                        context.RegisterCodeFix(codefix, diagnostics)
            | _ -> ()
        } |> Async.Ignore |> RoslynHelpers.StartAsyncUnitAsTask(context.CancellationToken)
