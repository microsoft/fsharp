// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System.Composition
open System.Collections.Immutable
open System.Threading.Tasks

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Structure

open FSharp.Compiler.EditorServices
open FSharp.Compiler.EditorServices.Structure
open FSharp.Compiler.Syntax

module internal BlockStructure =
    let scopeToBlockType = function
        | Scope.Open -> FSharpBlockTypes.Imports
        | Scope.Namespace
        | Scope.Module -> FSharpBlockTypes.Namespace 
        | Scope.Record
        | Scope.Interface
        | Scope.TypeExtension
        | Scope.RecordDefn
        | Scope.CompExpr
        | Scope.ObjExpr
        | Scope.UnionDefn
        | Scope.Attribute
        | Scope.Type -> FSharpBlockTypes.Type
        | Scope.New
        | Scope.RecordField
        | Scope.Member -> FSharpBlockTypes.Member
        | Scope.LetOrUse
        | Scope.Match
        | Scope.MatchBang
        | Scope.MatchClause
        | Scope.EnumCase
        | Scope.UnionCase
        | Scope.MatchLambda
        | Scope.ThenInIfThenElse
        | Scope.ElseInIfThenElse
        | Scope.TryWith
        | Scope.TryInTryWith
        | Scope.WithInTryWith
        | Scope.TryFinally
        | Scope.TryInTryFinally
        | Scope.FinallyInTryFinally
        | Scope.IfThenElse-> FSharpBlockTypes.Conditional
        | Scope.Tuple
        | Scope.ArrayOrList
        | Scope.CompExprInternal
        | Scope.Quote
        | Scope.SpecialFunc
        | Scope.Lambda
        | Scope.LetOrUseBang
        | Scope.Val
        | Scope.YieldOrReturn
        | Scope.YieldOrReturnBang
        | Scope.TryWith -> FSharpBlockTypes.Expression
        | Scope.Do -> FSharpBlockTypes.Statement
        | Scope.While
        | Scope.For -> FSharpBlockTypes.Loop
        | Scope.HashDirective -> FSharpBlockTypes.PreprocessorRegion
        | Scope.Comment
        | Scope.XmlDocComment -> FSharpBlockTypes.Comment

    let isAutoCollapsible = function
        | Scope.New
        | Scope.Attribute
        | Scope.Member
        | Scope.LetOrUse
        | Scope.EnumCase
        | Scope.UnionCase
        | Scope.SpecialFunc
        | Scope.HashDirective
        | Scope.Comment
        | Scope.Open
        | Scope.XmlDocComment -> true

        | Scope.Namespace
        | Scope.Module
        | Scope.Record
        | Scope.Interface
        | Scope.TypeExtension
        | Scope.RecordDefn
        | Scope.CompExpr
        | Scope.ObjExpr
        | Scope.UnionDefn
        | Scope.Type
        | Scope.RecordField
        | Scope.Match
        | Scope.MatchClause
        | Scope.MatchLambda
        | Scope.MatchBang
        | Scope.ThenInIfThenElse
        | Scope.ElseInIfThenElse
        | Scope.TryWith
        | Scope.TryInTryWith
        | Scope.WithInTryWith
        | Scope.TryFinally
        | Scope.TryInTryFinally
        | Scope.FinallyInTryFinally
        | Scope.IfThenElse
        | Scope.Tuple
        | Scope.ArrayOrList
        | Scope.CompExprInternal
        | Scope.Quote
        | Scope.Lambda
        | Scope.LetOrUseBang
        | Scope.Val
        | Scope.YieldOrReturn
        | Scope.YieldOrReturnBang
        | Scope.TryWith
        | Scope.Do
        | Scope.While
        | Scope.For -> false

    let createBlockSpans isBlockStructureEnabled (sourceText:SourceText) (parsedInput:ParsedInput) =
        let linetext = sourceText.Lines |> Seq.map (fun x -> x.ToString()) |> Seq.toArray
        
        Structure.getOutliningRanges linetext parsedInput
        |> Seq.distinctBy (fun x -> x.Range.StartLine)
        |> Seq.choose (fun scopeRange -> 
            // the range of text to collapse
            let textSpan = RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, scopeRange.CollapseRange)
            // the range of the entire expression
            let hintSpan = RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, scopeRange.Range)
            match textSpan,hintSpan with
            | Some textSpan, Some hintSpan ->
                let line = sourceText.Lines.GetLineFromPosition textSpan.Start
                let bannerText =
                    match Option.ofNullable (line.Span.Intersection textSpan) with
                    | Some span -> sourceText.GetSubText(span).ToString()+"..."
                    | None -> "..."
                let blockType = if isBlockStructureEnabled then scopeToBlockType scopeRange.Scope else FSharpBlockTypes.Nonstructural
                Some (FSharpBlockSpan(blockType, true, textSpan, hintSpan, bannerText, autoCollapse = isAutoCollapsible scopeRange.Scope))
            | _, _ -> None
        )

open BlockStructure
 
[<Export(typeof<IFSharpBlockStructureService>)>]
type internal FSharpBlockStructureService [<ImportingConstructor>] () =

    interface IFSharpBlockStructureService with
 
        member _.GetBlockStructureAsync(document, cancellationToken) : Task<FSharpBlockStructure> =
            asyncMaybe {
                let! sourceText = document.GetTextAsync(cancellationToken)
                let! parseResults = document.GetFSharpParseResultsAsync(nameof(FSharpBlockStructureService)) |> liftAsync
                return createBlockSpans document.Project.IsFSharpBlockStructureEnabled sourceText parseResults.ParseTree |> Seq.toImmutableArray
            } 
            |> Async.map (Option.defaultValue ImmutableArray<_>.Empty)
            |> Async.map FSharpBlockStructure
            |> RoslynHelpers.StartAsyncAsTask(cancellationToken)
