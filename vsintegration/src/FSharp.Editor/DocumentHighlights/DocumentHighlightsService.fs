// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Composition
open System.Collections.Immutable
open System.Threading.Tasks

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.DocumentHighlighting

open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text

type internal FSharpHighlightSpan =
    { IsDefinition: bool
      TextSpan: TextSpan }
    override this.ToString() = sprintf "%+A" this

[<Export(typeof<IFSharpDocumentHighlightsService>)>]
type internal FSharpDocumentHighlightsService [<ImportingConstructor>] () =

    /// Fix invalid spans if they appear to have redundant suffix and prefix.
    static let fixInvalidSymbolSpans (sourceText: SourceText) (lastIdent: string) (spans: FSharpHighlightSpan []) =
        spans
        |> Seq.choose (fun (span: FSharpHighlightSpan) ->
            let newLastIdent = sourceText.GetSubText(span.TextSpan).ToString()
            let index = newLastIdent.LastIndexOf(lastIdent, StringComparison.Ordinal)
            if index > 0 then 
                // Sometimes FCS returns a composite identifier for a short symbol, so we truncate the prefix
                // Example: newLastIdent --> "x.Length", lastIdent --> "Length"
                Some { span with TextSpan = TextSpan(span.TextSpan.Start + index, span.TextSpan.Length - index) }
            elif index = 0 && newLastIdent.Length > lastIdent.Length then
                // The returned symbol use is too long; we truncate its redundant suffix
                // Example: newLastIdent --> "Length<'T>", lastIdent --> "Length"
                Some { span with TextSpan = TextSpan(span.TextSpan.Start, lastIdent.Length) }
            elif index = 0 then
                Some span
            else
                // In the case of attributes, a returned symbol use may be a part of original text
                // Example: newLastIdent --> "Sample", lastIdent --> "SampleAttribute"
                let index = lastIdent.LastIndexOf(newLastIdent, StringComparison.Ordinal)
                if index >= 0 then
                    Some span
                else None)
        |> Seq.distinctBy (fun span -> span.TextSpan.Start)
        |> Seq.toArray

    static member GetDocumentHighlights(document: Document, position: int) : Async<FSharpHighlightSpan[] option> =
        asyncMaybe {
            let! symbol = document.TryFindFSharpLexerSymbolAsync(position, SymbolLookupKind.Greedy, false, false, nameof(FSharpDocumentHighlightsService.GetDocumentHighlights))

            let! ct = Async.CancellationToken |> liftAsync
            let! sourceText = document.GetTextAsync(ct)
            let textLine = sourceText.Lines.GetLineFromPosition(position)
            let textLinePos = sourceText.Lines.GetLinePosition(position)
            let fcsTextLineNumber = Line.fromZ textLinePos.Line

            let! _, checkFileResults = document.GetFSharpParseAndCheckResultsAsync(nameof(FSharpDocumentHighlightsService)) |> liftAsync
            let! symbolUse = checkFileResults.GetSymbolUseAtLocation(fcsTextLineNumber, symbol.Ident.idRange.EndColumn, textLine.ToString(), symbol.FullIsland)
            let symbolUses = checkFileResults.GetUsesOfSymbolInFile(symbolUse.Symbol)
            return 
                [| for symbolUse in symbolUses do
                     match RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, symbolUse.Range) with 
                     | None -> ()
                     | Some span -> 
                         yield { IsDefinition = symbolUse.IsFromDefinition
                                 TextSpan = span } |]
                |> fixInvalidSymbolSpans sourceText symbol.Ident.idText
        }

    interface IFSharpDocumentHighlightsService with
        member _.GetDocumentHighlightsAsync(document, position, _documentsToSearch, cancellationToken) : Task<ImmutableArray<FSharpDocumentHighlights>> =
            asyncMaybe {
                let! spans = FSharpDocumentHighlightsService.GetDocumentHighlights(document, position)
                let highlightSpans = 
                    spans 
                    |> Array.map (fun span ->
                        let kind = if span.IsDefinition then FSharpHighlightSpanKind.Definition else FSharpHighlightSpanKind.Reference
                        FSharpHighlightSpan(span.TextSpan, kind))
                    |> Seq.toImmutableArray
                
                return ImmutableArray.Create(FSharpDocumentHighlights(document, highlightSpans))
            }   
            |> Async.map (Option.defaultValue ImmutableArray<FSharpDocumentHighlights>.Empty)
            |> RoslynHelpers.StartAsyncAsTask(cancellationToken)
