// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Composition
open System.Linq
open System.Threading
open System.Threading.Tasks

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.ExternalAccess.FSharp
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Editor

open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open FSharp.Compiler.Tokenization
open Symbols

type internal FailureInlineRenameInfo private () =
    interface IFSharpInlineRenameInfo with
        member _.CanRename = false
        member _.LocalizedErrorMessage = FSharpEditorFeaturesResources.You_cannot_rename_this_element
        member _.TriggerSpan = Unchecked.defaultof<_>
        member _.HasOverloads = false
        member _.ForceRenameOverloads = true
        member _.DisplayName = ""
        member _.FullDisplayName = ""
        member _.Glyph = Glyph.MethodPublic
        member _.GetFinalSymbolName _ = ""
        member _.GetReferenceEditSpan(_, _) = Unchecked.defaultof<_>
        member _.GetConflictEditSpan(_, _, _) = Nullable()
        member _.FindRenameLocationsAsync(_, _) = Task<IFSharpInlineRenameLocationSet>.FromResult null
        member _.TryOnBeforeGlobalSymbolRenamed(_, _, _) = false
        member _.TryOnAfterGlobalSymbolRenamed(_, _, _) = false
    static member Instance = FailureInlineRenameInfo() :> IFSharpInlineRenameInfo

type internal InlineRenameLocationSet(locations: FSharpInlineRenameLocation [], originalSolution: Solution, symbolKind: LexerSymbolKind, symbol: FSharpSymbol) =
    interface IFSharpInlineRenameLocationSet with
        member _.Locations = upcast locations.ToList()
        
        member _.GetReplacementsAsync(replacementText, _optionSet, cancellationToken) : Task<IFSharpInlineRenameReplacementInfo> =
            let rec applyChanges (solution: Solution) (locationsByDocument: (Document * FSharpInlineRenameLocation list) list) =
                async {
                    match locationsByDocument with
                    | [] -> return solution
                    | (document, locations) :: rest ->
                        let! oldSource = document.GetTextAsync(cancellationToken) |> Async.AwaitTask
                        let newSource = oldSource.WithChanges(locations |> List.map (fun l -> TextChange(l.TextSpan, replacementText)))
                        return! applyChanges (solution.WithDocumentText(document.Id, newSource)) rest
                }
        
            async {
                let! newSolution = applyChanges originalSolution (locations |> Array.toList |> List.groupBy (fun x -> x.Document))
                let replacementText =
                    match symbolKind with
                    | LexerSymbolKind.GenericTypeParameter
                    | LexerSymbolKind.StaticallyResolvedTypeParameter -> replacementText
                    | _ -> FSharpKeywords.NormalizeIdentifierBackticks replacementText
                return 
                    { new IFSharpInlineRenameReplacementInfo with
                        member _.NewSolution = newSolution
                        member _.ReplacementTextValid = Tokenizer.isValidNameForSymbol(symbolKind, symbol, replacementText)
                        member _.DocumentIds = locations |> Seq.map (fun doc -> doc.Document.Id) |> Seq.distinct
                        member _.GetReplacements _ = Seq.empty }
            }
            |> RoslynHelpers.StartAsyncAsTask(cancellationToken)

type internal InlineRenameInfo
    (
        document: Document,
        triggerSpan: TextSpan, 
        lexerSymbol: LexerSymbol,
        symbolUse: FSharpSymbolUse,
        declLoc: SymbolDeclarationLocation,
        checkFileResults: FSharpCheckFileResults
    ) =

    let getDocumentText (document: Document) cancellationToken =
        match document.TryGetText() with
        | true, text -> text
        | _ -> document.GetTextAsync(cancellationToken).Result

    let symbolUses =
        SymbolHelpers.getSymbolUsesInSolution(symbolUse.Symbol, declLoc, checkFileResults, document.Project.Solution)
        |> Async.cache

    interface IFSharpInlineRenameInfo with
        member _.CanRename = true
        member _.LocalizedErrorMessage = null
        member _.TriggerSpan = triggerSpan
        member _.HasOverloads = false
        member _.ForceRenameOverloads = false
        member _.DisplayName = symbolUse.Symbol.DisplayName
        member _.FullDisplayName = try symbolUse.Symbol.FullName with _ -> symbolUse.Symbol.DisplayName
        member _.Glyph = Glyph.MethodPublic
        member _.GetFinalSymbolName replacementText = replacementText

        member _.GetReferenceEditSpan(location, cancellationToken) =
            let text = getDocumentText location.Document cancellationToken
            Tokenizer.fixupSpan(text, location.TextSpan)
        
        member _.GetConflictEditSpan(location, replacementText, cancellationToken) = 
            let text = getDocumentText location.Document cancellationToken
            let spanText = text.ToString(location.TextSpan)
            let position = spanText.LastIndexOf(replacementText, StringComparison.Ordinal)
            if position < 0 then Nullable()
            else Nullable(TextSpan(location.TextSpan.Start + position, replacementText.Length))
        
        member _.FindRenameLocationsAsync(_optionSet, cancellationToken) =
            async {
                let! symbolUsesByDocumentId = symbolUses
                let! locations =
                    symbolUsesByDocumentId
                    |> Seq.map (fun (KeyValue(documentId, symbolUses)) ->
                        async {
                            let document = document.Project.Solution.GetDocument(documentId)
                            let! sourceText = document.GetTextAsync(cancellationToken) |> Async.AwaitTask
                            return 
                                [| for symbolUse in symbolUses do
                                     match RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, symbolUse) with
                                     | Some span ->
                                         let textSpan = Tokenizer.fixupSpan(sourceText, span)
                                         yield FSharpInlineRenameLocation(document, textSpan) 
                                     | None -> () |]
                        })
                    |> Async.Parallel
                    |> Async.map Array.concat

                return InlineRenameLocationSet(locations, document.Project.Solution, lexerSymbol.Kind, symbolUse.Symbol) :> IFSharpInlineRenameLocationSet
            } |> RoslynHelpers.StartAsyncAsTask(cancellationToken)
        
        member _.TryOnBeforeGlobalSymbolRenamed(_workspace, _changedDocumentIDs, _replacementText) = true
        member _.TryOnAfterGlobalSymbolRenamed(_workspace, _changedDocumentIDs, _replacementText) = true

[<Export(typeof<IFSharpEditorInlineRenameService>); Shared>]
type internal InlineRenameService 
    [<ImportingConstructor>]
    (
    ) =

    static member GetInlineRenameInfo(document: Document, position: int) : Async<IFSharpInlineRenameInfo option> = 
        asyncMaybe {
            let! ct = Async.CancellationToken |> liftAsync
            let! sourceText = document.GetTextAsync(ct)
            let textLine = sourceText.Lines.GetLineFromPosition(position)
            let textLinePos = sourceText.Lines.GetLinePosition(position)
            let fcsTextLineNumber = Line.fromZ textLinePos.Line
            let! symbol = document.TryFindFSharpLexerSymbolAsync(position, SymbolLookupKind.Greedy, false, false, nameof(InlineRenameService))

            let! _, checkFileResults = document.GetFSharpParseAndCheckResultsAsync(nameof(InlineRenameService)) |> liftAsync
            let! symbolUse = checkFileResults.GetSymbolUseAtLocation(fcsTextLineNumber, symbol.Ident.idRange.EndColumn, textLine.Text.ToString(), symbol.FullIsland)
            let! declLoc = symbolUse.GetDeclarationLocation(document)

            let! span = RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, symbolUse.Range)
            let triggerSpan = Tokenizer.fixupSpan(sourceText, span)

            return InlineRenameInfo(document, triggerSpan, symbol, symbolUse, declLoc, checkFileResults) :> IFSharpInlineRenameInfo
        }
    
    interface IFSharpEditorInlineRenameService with
        member _.GetRenameInfoAsync(document: Document, position: int, cancellationToken: CancellationToken) : Task<IFSharpInlineRenameInfo> =
            asyncMaybe {
                return! InlineRenameService.GetInlineRenameInfo(document, position)
            }
            |> Async.map (Option.defaultValue FailureInlineRenameInfo.Instance)
            |> RoslynHelpers.StartAsyncAsTask(cancellationToken)
