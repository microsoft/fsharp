// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Composition
open System.Threading

open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open FSharp.Compiler.Syntax

open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.CodeRefactorings
open Microsoft.CodeAnalysis.CodeActions

[<ExportCodeRefactoringProvider(FSharpConstants.FSharpLanguageName, Name = "AddExplicitTypeToParameter"); Shared>]
type internal FSharpAddExplicitTypeToParameterRefactoring
    [<ImportingConstructor>]
    (
    ) =
    inherit CodeRefactoringProvider()

    override _.ComputeRefactoringsAsync context =
        asyncMaybe {
            let document = context.Document
            let position = context.Span.Start
            let! sourceText = document.GetTextAsync () |> liftTaskAsync
            let textLine = sourceText.Lines.GetLineFromPosition position
            let textLinePos = sourceText.Lines.GetLinePosition position
            let fcsTextLineNumber = Line.fromZ textLinePos.Line
            let! lexerSymbol = document.TryFindFSharpLexerSymbolAsync(position, SymbolLookupKind.Greedy, false, false, nameof(FSharpAddExplicitTypeToParameterRefactoring))

            let! parseFileResults, checkFileResults = document.GetFSharpParseAndCheckResultsAsync(nameof(FSharpAddExplicitTypeToParameterRefactoring)) |> liftAsync
            let! symbolUse = checkFileResults.GetSymbolUseAtLocation(fcsTextLineNumber, lexerSymbol.Ident.idRange.EndColumn, textLine.ToString(), lexerSymbol.FullIsland)

            let isValidParameterWithoutTypeAnnotation (funcOrValue: FSharpMemberOrFunctionOrValue) (symbolUse: FSharpSymbolUse) =
                let isLambdaIfFunction =
                    funcOrValue.IsFunction &&
                    parseFileResults.IsBindingALambdaAtPosition symbolUse.Range.Start

                (funcOrValue.IsValue || isLambdaIfFunction) &&
                parseFileResults.IsPositionContainedInACurriedParameter symbolUse.Range.Start &&
                not (parseFileResults.IsTypeAnnotationGivenAtPosition symbolUse.Range.Start) &&
                not funcOrValue.IsMember &&
                not funcOrValue.IsMemberThisValue &&
                not funcOrValue.IsConstructorThisValue &&
                not (PrettyNaming.IsOperatorName funcOrValue.DisplayName)

            match symbolUse.Symbol with
            | :? FSharpMemberOrFunctionOrValue as v when isValidParameterWithoutTypeAnnotation v symbolUse ->
                let typeString = v.FullType.FormatWithConstraints symbolUse.DisplayContext
                let title = SR.AddTypeAnnotation()

                let! symbolSpan = RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, symbolUse.Range)

                let alreadyWrappedInParens =
                    let rec leftLoop ch pos =
                        if not (Char.IsWhiteSpace(ch)) then
                            ch = '('
                        else
                            leftLoop sourceText.[pos - 1] (pos - 1)

                    let rec rightLoop ch pos =
                        if not (Char.IsWhiteSpace(ch)) then
                            ch = ')'
                        else
                            rightLoop sourceText.[pos + 1] (pos + 1)

                    let hasLeftParen = leftLoop sourceText.[symbolSpan.Start - 1] (symbolSpan.Start - 1)
                    let hasRightParen = rightLoop sourceText.[symbolSpan.End] symbolSpan.End
                    hasLeftParen && hasRightParen
                    
                let getChangedText (sourceText: SourceText) =
                    if alreadyWrappedInParens then
                        sourceText.WithChanges(TextChange(TextSpan(symbolSpan.End, 0), ": " + typeString))
                    else
                        sourceText.WithChanges(TextChange(TextSpan(symbolSpan.Start, 0), "("))
                                    .WithChanges(TextChange(TextSpan(symbolSpan.End + 1, 0), ": " + typeString + ")"))

                let codeAction =
                    CodeAction.Create(
                        title,
                        (fun (cancellationToken: CancellationToken) ->
                            async {
                                let! sourceText = context.Document.GetTextAsync(cancellationToken) |> Async.AwaitTask
                                return context.Document.WithText(getChangedText sourceText)
                            } |> RoslynHelpers.StartAsyncAsTask(cancellationToken)),
                        title)
                context.RegisterRefactoring(codeAction)
            | _ -> ()
        }
        |> Async.Ignore
        |> RoslynHelpers.StartAsyncUnitAsTask(context.CancellationToken)
