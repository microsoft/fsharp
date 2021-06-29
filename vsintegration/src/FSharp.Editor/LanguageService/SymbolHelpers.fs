// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Collections.Immutable
open System.Threading
open System.Threading.Tasks

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Text

open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open Microsoft.VisualStudio.FSharp.Editor.Symbols 

module internal SymbolHelpers =
    /// Used for local code fixes in a document, e.g. to rename local parameters
    let getSymbolUsesOfSymbolAtLocationInDocument (document: Document, position: int) =
        asyncMaybe {
            let userOpName = "getSymbolUsesOfSymbolAtLocationInDocument"
            let! _, checkFileResults = document.GetFSharpParseAndCheckResultsAsync(userOpName) |> liftAsync
            let! defines = document.GetFSharpCompilationDefinesAsync(userOpName) |> liftAsync

            let! cancellationToken = Async.CancellationToken |> liftAsync
            let! sourceText = document.GetTextAsync(cancellationToken)
            let textLine = sourceText.Lines.GetLineFromPosition(position)
            let textLinePos = sourceText.Lines.GetLinePosition(position)
            let fcsTextLineNumber = Line.fromZ textLinePos.Line
            let! symbol = Tokenizer.getSymbolAtPosition(document.Id, sourceText, position, document.FilePath, defines, SymbolLookupKind.Greedy, false, false)
            let! symbolUse = checkFileResults.GetSymbolUseAtLocation(fcsTextLineNumber, symbol.Ident.idRange.EndColumn, textLine.ToString(), symbol.FullIsland)
            let! ct = Async.CancellationToken |> liftAsync
            let symbolUses = checkFileResults.GetUsesOfSymbolInFile(symbolUse.Symbol, cancellationToken=ct)
            return symbolUses
        }

    let getSymbolUsesInProjects (symbol: FSharpSymbol, projects: Project list, onFound: Document -> TextSpan -> range -> Async<unit>) =
        projects
        |> Seq.map (fun project -> project.FindFSharpReferencesAsync(symbol, onFound, "getSymbolUsesInProjects"))
        |> Async.Sequential

    let getSymbolUsesInSolution (symbol: FSharpSymbol, declLoc: SymbolDeclarationLocation, checkFileResults: FSharpCheckFileResults, solution: Solution) =
        async {
            let toDict (symbolUseRanges: range seq) =
                let groups =
                    symbolUseRanges
                     |> Seq.collect (fun symbolUse -> 
                          solution.GetDocumentIdsWithFilePath(symbolUse.FileName) |> Seq.map (fun id -> id, symbolUse))
                     |> Seq.groupBy fst
                groups.ToImmutableDictionary(
                    (fun (id, _) -> id), 
                    fun (_, xs) -> xs |> Seq.map snd |> Seq.toArray)

            match declLoc with
            | SymbolDeclarationLocation.CurrentDocument ->
                let! ct = Async.CancellationToken
                let symbolUses = checkFileResults.GetUsesOfSymbolInFile(symbol, ct)
                return toDict (symbolUses |> Seq.map (fun symbolUse -> symbolUse.Range))
            | SymbolDeclarationLocation.Projects (projects, isInternalToProject) -> 
                let symbolUseRanges = ImmutableArray.CreateBuilder()
                    
                let projects =
                    if isInternalToProject then projects
                    else 
                        [ for project in projects do
                            yield project
                            yield! project.GetDependentProjects() ]
                        |> List.distinctBy (fun x -> x.Id)

                let onFound =
                    fun _ _ symbolUseRange ->
                        async { symbolUseRanges.Add symbolUseRange }

                let! _ = getSymbolUsesInProjects (symbol, projects, onFound)
                    
                // Distinct these down because each TFM will produce a new 'project'.
                // Unless guarded by a #if define, symbols with the same range will be added N times
                let symbolUseRanges = symbolUseRanges.ToArray() |> Array.distinct
                return toDict symbolUseRanges
        }
 
    type OriginalText = string

    // Note, this function is broken and shouldn't be used because the source text ranges to replace are applied sequentially,
    // breaking the position computations as changes progress, especially if two changes are made on the same line.  
    //
    // However, it is only currently used by ProposeUpperCaseLabel code fix, where the changes to code will rarely be on the same line.
    //
    // A better approach is to use something like createTextChangeCodeFix below, with a delayed function to compute a set of changes to be applied
    // simultaneously.  But that doesn't work for this case, as we want a set of changes to apply acrosss the whole solution.

    let changeAllSymbolReferences (document: Document, symbolSpan: TextSpan, textChanger: string -> string)
        : Async<(Func<CancellationToken, Task<Solution>> * OriginalText) option> =
        asyncMaybe {
            let userOpName = "changeAllSymbolReferences"
            do! Option.guard (symbolSpan.Length > 0)
            let! cancellationToken = liftAsync Async.CancellationToken
            let! sourceText = document.GetTextAsync(cancellationToken)
            let originalText = sourceText.ToString(symbolSpan)
            do! Option.guard (originalText.Length > 0)

            let! symbol = document.TryFindFSharpLexerSymbolAsync(symbolSpan.Start, SymbolLookupKind.Greedy, false, false, userOpName)
            let textLine = sourceText.Lines.GetLineFromPosition(symbolSpan.Start)
            let textLinePos = sourceText.Lines.GetLinePosition(symbolSpan.Start)
            let fcsTextLineNumber = Line.fromZ textLinePos.Line

            let! _, checkFileResults = document.GetFSharpParseAndCheckResultsAsync(userOpName) |> liftAsync
            let! symbolUse = checkFileResults.GetSymbolUseAtLocation(fcsTextLineNumber, symbol.Ident.idRange.EndColumn, textLine.ToString(), symbol.FullIsland)
            let! declLoc = symbolUse.GetDeclarationLocation(document)
            let newText = textChanger originalText
            // defer finding all symbol uses throughout the solution
            return 
                Func<_,_>(fun (cancellationToken: CancellationToken) ->
                    async {
                        let! symbolUsesByDocumentId = 
                            getSymbolUsesInSolution(symbolUse.Symbol, declLoc, checkFileResults, document.Project.Solution)
                        
                        let mutable solution = document.Project.Solution
                            
                        for KeyValue(documentId, symbolUses) in symbolUsesByDocumentId do
                            let document = document.Project.Solution.GetDocument(documentId)
                            let! sourceText = document.GetTextAsync(cancellationToken) |> Async.AwaitTask
                            let mutable sourceText = sourceText
                            for symbolUse in symbolUses do
                                match RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, symbolUse) with 
                                | None -> ()
                                | Some span -> 
                                    let textSpan = Tokenizer.fixupSpan(sourceText, span)
                                    sourceText <- sourceText.Replace(textSpan, newText)
                                    solution <- solution.WithDocumentText(documentId, sourceText)
                        return solution
                    } |> RoslynHelpers.StartAsyncAsTask cancellationToken),
                originalText
        }
