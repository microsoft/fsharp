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

    static member GetBraceMatchingResult(checker: FSharpChecker, sourceText: SourceText, fileName, parsingOptions: FSharpParsingOptions, position: int, userOpName: string, [<Optional; DefaultParameterValue(false)>] forFormatting: bool) = 
        async {
            let! matchedBraces = checker.MatchBraces(fileName, sourceText.ToFSharpSourceText(), parsingOptions, userOpName)
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
                let! checker, _, parsingOptions, _ = document.GetFSharpCompilationOptionsAsync(nameof(FSharpBraceMatchingService)) |> liftAsync
                let! sourceText = document.GetTextAsync(cancellationToken)
                let! (left, right) = FSharpBraceMatchingService.GetBraceMatchingResult(checker, sourceText, document.Name, parsingOptions, position, nameof(FSharpBraceMatchingService))
                let! leftSpan = RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, left)
                let! rightSpan = RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, right)
                return FSharpBraceMatchingResult(leftSpan, rightSpan)
            } 
            |> Async.map Option.toNullable
            |> RoslynHelpers.StartAsyncAsTask cancellationToken
