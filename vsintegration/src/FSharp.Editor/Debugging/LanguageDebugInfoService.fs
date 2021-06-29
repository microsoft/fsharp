// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Composition
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Classification
open FSharp.Compiler.EditorServices
open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Editor.Implementation.Debugging

[<Export(typeof<IFSharpLanguageDebugInfoService>)>]
type internal FSharpLanguageDebugInfoService [<ImportingConstructor>]() =

    static member GetDataTipInformation(sourceText: SourceText, position: int, tokens: List<ClassifiedSpan>): TextSpan option =
        let tokenIndex = tokens |> Seq.tryFindIndex(fun t -> t.TextSpan.Contains(position))

        if tokenIndex.IsNone then None
        else
            let token = tokens.[tokenIndex.Value]
        
            match token.ClassificationType with
        
            | ClassificationTypeNames.StringLiteral ->
                Some(token.TextSpan)
        
            | ClassificationTypeNames.Identifier ->
                let textLine = sourceText.Lines.GetLineFromPosition(position)
                let textLinePos = sourceText.Lines.GetLinePosition(position)
                let textLineColumn = textLinePos.Character
                match QuickParse.GetCompleteIdentifierIsland false (textLine.ToString()) textLineColumn with
                | None -> None
                | Some(island, islandEnd, _) ->
                    let islandDocumentStart = textLine.Start + islandEnd - island.Length
                    Some(TextSpan.FromBounds(islandDocumentStart, islandDocumentStart + island.Length))
        
            | _ -> None

    interface IFSharpLanguageDebugInfoService with
        
        // FSROSLYNTODO: This is used to get function names in breakpoint window. It should return fully qualified function name and line offset from the start of the function.
        member this.GetLocationInfoAsync(_, _, _): Task<FSharpDebugLocationInfo> =
            Task.FromResult(Unchecked.defaultof<FSharpDebugLocationInfo>)

        member this.GetDataTipInfoAsync(document: Document, position: int, cancellationToken: CancellationToken): Task<FSharpDebugDataTipInfo> =
            async {
                let defines = document.GetFSharpQuickDefines() 
                let! cancellationToken = Async.CancellationToken
                let! sourceText = document.GetTextAsync(cancellationToken) |> Async.AwaitTask
                let textSpan = TextSpan.FromBounds(0, sourceText.Length)
                let classifiedSpans = Tokenizer.getClassifiedSpans(document.Id, sourceText, textSpan, Some(document.Name), defines, cancellationToken)
                let result = 
                     match FSharpLanguageDebugInfoService.GetDataTipInformation(sourceText, position, classifiedSpans) with
                     | None -> FSharpDebugDataTipInfo()
                     | Some textSpan -> FSharpDebugDataTipInfo(textSpan, sourceText.GetSubText(textSpan).ToString())
                return result
            }
            |> RoslynHelpers.StartAsyncAsTask(cancellationToken)
            
            