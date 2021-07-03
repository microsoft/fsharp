// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System.ComponentModel.Composition
open Microsoft.CodeAnalysis.Text
open FSharp.Compiler.CodeAnalysis
open System.Runtime.InteropServices
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Editor

[<Export(typeof<IFSharpBraceMatcher>)>]
type internal FSharpBraceMatchingService 
    [<ImportingConstructor>]
    (
    ) =

    static member GetBraceMatchingResult(parsingOptions: FSharpParsingOptions, sourceText: SourceText, fileName, position: int, [<Optional; DefaultParameterValue(false)>] forFormatting: bool) = 
        async {
            let matchedBraces = FSharpProject.MatchBraces(fileName, sourceText.ToFSharpSourceText(), parsingOptions)
            let isPositionInRange range = 
                match RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, range) with
                | None -> false
                | Some span ->
                    if forFormatting then
                        let length = position - span.Start
                        length >= 0 && length <= span.Length
                    else
                        span.Contains position
            return matchedBraces |> Array.tryFind(fun (left, right) -> isPositionInRange left || isPositionInRange right)
        }
        
    interface IFSharpBraceMatcher with
        member this.FindBracesAsync(document, position, cancellationToken) = 
            asyncMaybe {
                let parsingOptions = document.GetFSharpQuickParsingOptions()
                let! sourceText = document.GetTextAsync(cancellationToken)
                let! (left, right) = FSharpBraceMatchingService.GetBraceMatchingResult(parsingOptions, sourceText, document.Name, position)
                let! leftSpan = RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, left)
                let! rightSpan = RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, right)
                return FSharpBraceMatchingResult(leftSpan, rightSpan)
            } 
            |> Async.map Option.toNullable
            |> RoslynHelpers.StartAsyncAsTask cancellationToken
