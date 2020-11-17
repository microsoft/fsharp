// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.
namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Collections.Immutable
open System.Threading
open System.ComponentModel.Composition

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.InlineHints

open FSharp.Compiler
open FSharp.Compiler.SourceCodeServices
open FSharp.Compiler.Range

[<Export(typeof<IFSharpInlineHintsService>)>]
type internal FSharpInlineHintsService
    [<ImportingConstructor>]
    (
        checkerProvider: FSharpCheckerProvider,
        projectInfoManager: FSharpProjectOptionsManager
    ) =

    static let userOpName = "FSharpInlineHints"

    static let getFirstPositionAfterParen (str: string) startPos =
        match str with
        | null -> -1
        | str when startPos > str.Length -> -1
        | str ->
            str.IndexOf('(') + 1

    interface IFSharpInlineHintsService with
        member _.GetInlineHintsAsync(document: Document, textSpan: TextSpan, cancellationToken: CancellationToken) =
            asyncMaybe {
                do! Option.guard (not (isSignatureFile document.FilePath))

                let! _, projectOptions = projectInfoManager.TryGetOptionsForEditingDocumentOrProject(document, cancellationToken, userOpName)
                let! sourceText = document.GetTextAsync(cancellationToken)
                let! parseFileResults, _, checkFileResults = checkerProvider.Checker.ParseAndCheckDocument(document, projectOptions, userOpName)
                let range = RoslynHelpers.TextSpanToFSharpRange(document.FilePath, textSpan, sourceText)
                let symbolUses =
                    checkFileResults.GetAllUsesOfAllSymbolsInFile(cancellationToken)
                    |> Seq.filter (fun su -> rangeContainsRange range su.RangeAlternate)

                let typeHints = ImmutableArray.CreateBuilder()
                let parameterHints = ImmutableArray.CreateBuilder()

                let isValidForTypeHint (funcOrValue: FSharpMemberOrFunctionOrValue) (symbolUse: FSharpSymbolUse) =
                    let isLambdaIfFunction =
                        funcOrValue.IsFunction &&
                        parseFileResults.IsBindingALambdaAtPosition symbolUse.RangeAlternate.Start

                    (funcOrValue.IsValue || isLambdaIfFunction) &&
                    not (parseFileResults.IsTypeAnnotationGivenAtPosition symbolUse.RangeAlternate.Start) &&
                    symbolUse.IsFromDefinition &&
                    not funcOrValue.IsMember &&
                    not funcOrValue.IsMemberThisValue &&
                    not funcOrValue.IsConstructorThisValue &&
                    not (PrettyNaming.IsOperatorName funcOrValue.DisplayName)

                for symbolUse in symbolUses do
                    match symbolUse.Symbol with
                    | :? FSharpMemberOrFunctionOrValue as funcOrValue when isValidForTypeHint funcOrValue symbolUse ->
                        let typeInfo = ResizeArray()

                        let layout =
                            funcOrValue.GetReturnTypeLayout symbolUse.DisplayContext
                            |> Option.defaultValue Layout.emptyL
                        
                        layout
                        |> Layout.renderL (Layout.taggedTextListR typeInfo.Add)
                        |> ignore
                            
                        let displayParts = ImmutableArray.CreateBuilder()
                        displayParts.Add(TaggedText(TextTags.Text, ": "))

                        for tt in typeInfo do
                            displayParts.Add(TaggedText(RoslynHelpers.roslynTag tt.Tag, tt.Text))

                        let symbolSpan = RoslynHelpers.FSharpRangeToTextSpan(sourceText, symbolUse.RangeAlternate)

                        let hint = FSharpInlineHint(TextSpan(symbolSpan.End, 0), displayParts.ToImmutableArray())
                        typeHints.Add(hint)

                    | :? FSharpMemberOrFunctionOrValue as func when func.IsFunction && not symbolUse.IsFromDefinition ->
                        let appliedArgRangesOpt = parseFileResults.GetAllArgumentsForFunctionApplicationAtPostion  symbolUse.RangeAlternate.Start
                        match appliedArgRangesOpt with
                        | None -> ()
                        | Some [] -> ()
                        | Some appliedArgRanges ->
                            let parameters = func.CurriedParameterGroups |> Seq.concat
                            let appliedArgRanges = appliedArgRanges |> Array.ofList
                            let definitionArgs = parameters |> Array.ofSeq

                            for idx = 0 to appliedArgRanges.Length - 1 do
                                let appliedArgRange = appliedArgRanges.[idx]
                                let definitionArgName = definitionArgs.[idx].DisplayName
                                if not (String.IsNullOrWhiteSpace(definitionArgName)) then
                                    let appliedArgSpan = RoslynHelpers.FSharpRangeToTextSpan(sourceText, appliedArgRange)
                                    let displayParts = ImmutableArray.Create(TaggedText(TextTags.Text, definitionArgName + " ="))
                                    let hint = FSharpInlineHint(TextSpan(appliedArgSpan.Start, 0), displayParts)
                                    parameterHints.Add(hint)

                    | :? FSharpMemberOrFunctionOrValue as methodOrConstructor when methodOrConstructor.IsMethod || methodOrConstructor.IsConstructor ->
                        let endPosForMethod = symbolUse.RangeAlternate.End
                        let line, _ = Pos.toZ endPosForMethod
                        let afterParenPosInLine = getFirstPositionAfterParen (sourceText.Lines.[line].ToString()) (endPosForMethod.Column)
                        let tupledParamInfos = parseFileResults.FindNoteworthyParamInfoLocations(Pos.fromZ line afterParenPosInLine)
                        let appliedArgRanges = parseFileResults.GetAllArgumentsForFunctionApplicationAtPostion  symbolUse.RangeAlternate.Start
                        match tupledParamInfos, appliedArgRanges with
                        | None, None -> ()

                        // Prefer looking at the "tupled" view if it exists, even if the other ranges exist.
                        // M(1, 2) can give results for both, but in that case we want the "tupled" view.
                        | Some tupledParamInfos, _ ->
                            let parameters = methodOrConstructor.CurriedParameterGroups |> Seq.concat |> Array.ofSeq
                            for idx = 0 to parameters.Length - 1 do
                                let paramLocationInfo = tupledParamInfos.ArgumentLocations.[idx]
                                let paramName = parameters.[idx].DisplayName
                                if not paramLocationInfo.IsNamedArgument && not (String.IsNullOrWhiteSpace(paramName)) then
                                    let appliedArgSpan = RoslynHelpers.FSharpRangeToTextSpan(sourceText, paramLocationInfo.ArgumentRange)
                                    let displayParts = ImmutableArray.Create(TaggedText(TextTags.Text, paramName + " ="))
                                    let hint = FSharpInlineHint(TextSpan(appliedArgSpan.Start, 0), displayParts)
                                    parameterHints.Add(hint)

                        // This will only happen for curried methods defined in F#.
                        | _, Some appliedArgRanges ->
                            let parameters = methodOrConstructor.CurriedParameterGroups |> Seq.concat
                            let appliedArgRanges = appliedArgRanges |> Array.ofList
                            let definitionArgs = parameters |> Array.ofSeq

                            for idx = 0 to appliedArgRanges.Length - 1 do
                                let appliedArgRange = appliedArgRanges.[idx]
                                let definitionArgName = definitionArgs.[idx].DisplayName
                                if not (String.IsNullOrWhiteSpace(definitionArgName)) then
                                    let appliedArgSpan = RoslynHelpers.FSharpRangeToTextSpan(sourceText, appliedArgRange)
                                    let displayParts = ImmutableArray.Create(TaggedText(TextTags.Text, definitionArgName + " ="))
                                    let hint = FSharpInlineHint(TextSpan(appliedArgSpan.Start, 0), displayParts)
                                    parameterHints.Add(hint)
                    | _ -> ()

                let typeHints = typeHints.ToImmutableArray()
                let parameterHints = parameterHints.ToImmutableArray()

                return typeHints.AddRange(parameterHints)
            }
            |> Async.map (Option.defaultValue ImmutableArray<_>.Empty)
            |> RoslynHelpers.StartAsyncAsTask(cancellationToken)
