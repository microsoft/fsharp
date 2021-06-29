// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System.Collections.Immutable
open System.Composition

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.ExternalAccess.FSharp
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.FindUsages
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Editor.FindUsages

open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Text
open Microsoft.CodeAnalysis.Text

[<Export(typeof<IFSharpFindUsagesService>)>]
type internal FSharpFindUsagesService
    [<ImportingConstructor>]
    (
    ) =

    // File can be included in more than one project, hence single `range` may results with multiple `Document`s.
    let rangeToDocumentSpans (solution: Solution, range: range) =
        async {
            if range.Start = range.End then return []
            else 
                let! spans =
                    solution.GetDocumentIdsWithFilePath(range.FileName)
                    |> Seq.map (fun documentId ->
                        async {
                            let doc = solution.GetDocument(documentId)
                            let! cancellationToken = Async.CancellationToken
                            let! sourceText = doc.GetTextAsync(cancellationToken) |> Async.AwaitTask
                            match RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, range) with
                            | Some span ->
                                let span = Tokenizer.fixupSpan(sourceText, span)
                                return Some (FSharpDocumentSpan(doc, span))
                            | None -> return None
                        })
                    |> Async.Parallel
                return spans |> Array.choose id |> Array.toList
        }

    let findReferencedSymbolsAsync(document: Document, position: int, context: IFSharpFindUsagesContext, allReferences: bool) : Async<unit> =
        asyncMaybe {
            let! sourceText = document.GetTextAsync(context.CancellationToken) |> Async.AwaitTask |> liftAsync
            let textLine = sourceText.Lines.GetLineFromPosition(position).ToString()
            let lineNumber = sourceText.Lines.GetLinePosition(position).Line + 1
            let! symbol = document.TryFindFSharpLexerSymbolAsync(position, SymbolLookupKind.Greedy, false, false, "findReferencedSymbolsAsync")
            
            let! _, checkFileResults = document.GetFSharpParseAndCheckResultsAsync(nameof(FSharpFindUsagesService)) |> liftAsync
            let! symbolUse = checkFileResults.GetSymbolUseAtLocation(lineNumber, symbol.Ident.idRange.EndColumn, textLine, symbol.FullIsland)
            let declaration = checkFileResults.GetDeclarationLocation (lineNumber, symbol.Ident.idRange.EndColumn, textLine, symbol.FullIsland, false)
            let tags = FSharpGlyphTags.GetTags(Tokenizer.GetGlyphForSymbol (symbolUse.Symbol, symbol.Kind))
            
            let declarationRange = 
                match declaration with
                | FindDeclResult.DeclFound range -> Some range
                | _ -> None

            let! declarationSpans = async {
                match declarationRange with
                | Some range -> return! rangeToDocumentSpans(document.Project.Solution, range)
                | None -> return! async.Return [] } |> liftAsync

            let isExternal = declarationSpans |> List.isEmpty
            let displayParts = ImmutableArray.Create(Microsoft.CodeAnalysis.TaggedText(TextTags.Text, symbol.Ident.idText))
            let originationParts = ImmutableArray.Create(Microsoft.CodeAnalysis.TaggedText(TextTags.Assembly, symbolUse.Symbol.Assembly.SimpleName))
            let externalDefinitionItem = FSharpDefinitionItem.CreateNonNavigableItem(tags, displayParts, originationParts)
            let definitionItems =
                    declarationSpans
                    |> List.map (fun span -> FSharpDefinitionItem.Create(tags, displayParts, span), span.Document.Id)
            
            for definitionItem, _ in definitionItems do
                do! context.OnDefinitionFoundAsync(definitionItem) |> Async.AwaitTask |> liftAsync

            if isExternal then
                do! context.OnDefinitionFoundAsync(externalDefinitionItem) |> Async.AwaitTask |> liftAsync
            
            let onFound =
                fun (doc: Document) (textSpan: TextSpan) (symbolUse: range) ->
                    async {
                        match declarationRange with
                        | Some declRange when Range.equals declRange symbolUse -> ()
                        | _ ->
                            if allReferences then
                                let definitionItem =
                                    if isExternal then
                                        externalDefinitionItem
                                    else
                                        definitionItems
                                        |> List.tryFind (fun (_, docId) -> doc.Id = docId)
                                        |> Option.map (fun (definitionItem, _) -> definitionItem)
                                        |> Option.defaultValue externalDefinitionItem

                                let referenceItem = FSharpSourceReferenceItem(definitionItem, FSharpDocumentSpan(doc, textSpan))
                                // REVIEW: OnReferenceFoundAsync is throwing inside Roslyn, putting a try/with so find-all refs doesn't fail.
                                try do! context.OnReferenceFoundAsync(referenceItem) |> Async.AwaitTask with | _ -> () }
            
            match symbolUse.GetDeclarationLocation document with
            | Some SymbolDeclarationLocation.CurrentDocument ->
                let symbolUses = checkFileResults.GetUsesOfSymbolInFile(symbolUse.Symbol)
                for symbolUse in symbolUses do
                    match RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, symbolUse.Range) with
                    | Some textSpan ->
                        do! onFound document textSpan symbolUse.Range |> liftAsync
                    | _ ->
                        ()
            | scope ->
                let projectsToCheck =
                    match scope with
                    | Some (SymbolDeclarationLocation.Projects (declProjects, false)) ->
                        [ for declProject in declProjects do
                            yield declProject
                            yield! declProject.GetDependentProjects() ]
                        |> List.distinct
                    | Some (SymbolDeclarationLocation.Projects (declProjects, true)) -> declProjects
                    // The symbol is declared in .NET framework, an external assembly or in a C# project within the solution.
                    // In order to find all its usages we have to check all F# projects.
                    | _ -> Seq.toList document.Project.Solution.Projects
                
                let! _ = SymbolHelpers.getSymbolUsesInProjects (symbolUse.Symbol, projectsToCheck, onFound) |> liftAsync
                ()
        } |> Async.Ignore

    interface IFSharpFindUsagesService with
        member _.FindReferencesAsync(document, position, context) =
            findReferencedSymbolsAsync(document, position, context, true)
            |> RoslynHelpers.StartAsyncUnitAsTask(context.CancellationToken)
        member _.FindImplementationsAsync(document, position, context) =
            findReferencedSymbolsAsync(document, position, context, false)
            |> RoslynHelpers.StartAsyncUnitAsTask(context.CancellationToken)
 