[<AutoOpen>]
module internal Microsoft.VisualStudio.FSharp.Editor.WorkspaceExtensions

open System
open System.Runtime.CompilerServices
open System.Threading
open Microsoft.CodeAnalysis
open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis

[<AutoOpen>]
module private CheckerExtensions =

    type FSharpChecker with
        /// Parse the source text from the Roslyn document.
        member checker.ParseDocument(document: Document, parsingOptions: FSharpParsingOptions, userOpName: string) =
            async {
                let! ct = Async.CancellationToken
                let! sourceText = document.GetTextAsync(ct) |> Async.AwaitTask

                return! checker.ParseFile(document.FilePath, sourceText.ToFSharpSourceText(), parsingOptions, userOpName=userOpName)
            }

        /// Parse and check the source text from the Roslyn document with possible stale results.
        member checker.ParseAndCheckDocumentWithPossibleStaleResults(document: Document, options: FSharpProjectOptions, allowStaleResults: bool, userOpName: string) =
            async {
                let! ct = Async.CancellationToken

                let! sourceText = document.GetTextAsync(ct) |> Async.AwaitTask
                let! textVersion = document.GetTextVersionAsync(ct) |> Async.AwaitTask

                let filePath = document.FilePath
                let textVersionHash = textVersion.GetHashCode()

                let parseAndCheckFile =
                    async {
                        let! (parseResults, checkFileAnswer) = checker.ParseAndCheckFileInProject(filePath, textVersionHash, sourceText.ToFSharpSourceText(), options, userOpName=userOpName)
                        return
                            match checkFileAnswer with
                            | FSharpCheckFileAnswer.Aborted -> 
                                None
                            | FSharpCheckFileAnswer.Succeeded(checkFileResults) ->
                                Some (parseResults, checkFileResults)
                    }

                let tryGetFreshResultsWithTimeout() =
                    async {
                        let! worker = Async.StartChild(async { try return! parseAndCheckFile with | _ -> return None }, millisecondsTimeout=document.Project.FSharpTimeUntilStaleCompletion)
                        try
                            return! worker
                        with :? TimeoutException ->
                            return None // worker is cancelled at this point, we cannot return it and wait its completion anymore
                    }

                let bindParsedInput(results: (FSharpParseFileResults * FSharpCheckFileResults) option) =
                    match results with
                    | Some(parseResults, checkResults) ->
                        Some (parseResults, parseResults.ParseTree, checkResults)
                    | None -> None

                if allowStaleResults then
                    let! freshResults = tryGetFreshResultsWithTimeout()
                    
                    let! results =
                        match freshResults with
                        | Some x -> async.Return (Some x)
                        | None ->
                            async {
                                match checker.TryGetRecentCheckResultsForFile(filePath, options, userOpName=userOpName) with
                                | Some (parseResults, checkFileResults, _) ->
                                    return Some (parseResults, checkFileResults)
                                | None ->
                                    return! parseAndCheckFile
                            }
                    return bindParsedInput results
                else 
                    let! results = parseAndCheckFile
                    return bindParsedInput results
            }

        /// Parse and check the source text from the Roslyn document.
        member checker.ParseAndCheckDocument(document: Document, options: FSharpProjectOptions, userOpName: string, ?allowStaleResults: bool) =
            async {
                let allowStaleResults =
                    match allowStaleResults with 
                    | Some b -> b
                    | _ ->  document.Project.IsFSharpStaleCompletionResultsEnabled
                return! checker.ParseAndCheckDocumentWithPossibleStaleResults(document, options, allowStaleResults, userOpName=userOpName)
            }

[<RequireQualifiedAccess>]
module private ProjectCache =

    /// This is a cache to maintain FSharpParsingOptions and FSharpProjectOptions per Roslyn Project.
    /// The Roslyn Project is held weakly meaning when it is cleaned up by the GC, the FSharParsingOptions and FSharpProjectOptions will be cleaned up by the GC.
    /// At some point, this will be the main caching mechanism for FCS projects instead of FCS itself.
    let Projects = ConditionalWeakTable<Project, FSharpChecker * FSharpProjectOptionsManager * FSharpParsingOptions * FSharpProjectOptions>()    

type Solution with

    /// Get the instance of IFSharpWorkspaceService.
    member private this.GetFSharpWorkspaceService() =
        this.Workspace.Services.GetRequiredService<IFSharpWorkspaceService>()

type Document with

    /// Get the FSharpParsingOptions and FSharpProjectOptions from the F# project that is associated with the given F# document.
    member this.GetFSharpCompilationOptionsAsync(userOpName) =
        async {
            if this.Project.IsFSharp then
                match ProjectCache.Projects.TryGetValue(this.Project) with
                | true, result -> return result
                | _ ->
                    let service = this.Project.Solution.GetFSharpWorkspaceService()
                    let projectOptionsManager = service.FSharpProjectOptionsManager
                    let! ct = Async.CancellationToken
                    match! projectOptionsManager.TryGetOptionsForDocumentOrProject(this, ct, userOpName) with
                    | None -> return raise(System.OperationCanceledException("FSharp project options not found."))
                    | Some(parsingOptions, _, projectOptions) ->
                        let result = (service.Checker, projectOptionsManager, parsingOptions, projectOptions)
                        return ProjectCache.Projects.GetValue(this.Project, Runtime.CompilerServices.ConditionalWeakTable<_,_>.CreateValueCallback(fun _ -> result))
            else
                return raise(System.OperationCanceledException("Document is not a FSharp document."))
        }

    /// Get the compilation defines from F# project that is associated with the given F# document.
    member this.GetFSharpCompilationDefinesAsync(userOpName) =
        async {
            let! _, _, parsingOptions, _ = this.GetFSharpCompilationOptionsAsync(userOpName)
            return CompilerEnvironment.GetCompilationDefinesForEditing parsingOptions
        }

    /// Get the instance of the FSharpChecker from the workspace by the given F# document.
    member this.GetFSharpChecker() =
        let workspaceService = this.Project.Solution.GetFSharpWorkspaceService()
        workspaceService.Checker

    /// A non-async call that quickly gets FSharpParsingOptions of the given F# document.
    /// This tries to get the FSharpParsingOptions by looking at an internal cache; if it doesn't exist in the cache it will create an inaccurate but usable form of the FSharpParsingOptions.
    member this.GetFSharpQuickParsingOptions() =
        let workspaceService = this.Project.Solution.GetFSharpWorkspaceService()
        workspaceService.FSharpProjectOptionsManager.TryGetQuickParsingOptionsForEditingDocumentOrProject(this)

    /// A non-async call that quickly gets the defines of the given F# document.
    /// This tries to get the defines by looking at an internal cache; if it doesn't exist in the cache it will create an inaccurate but usable form of the defines.
    member this.GetFSharpQuickDefines() =
        let workspaceService = this.Project.Solution.GetFSharpWorkspaceService()
        workspaceService.FSharpProjectOptionsManager.GetCompilationDefinesForEditingDocument(this)
    
    /// Parses the given F# document.
    member this.GetFSharpParseResultsAsync(userOpName) =
        async {
            let! checker, _, parsingOptions, _ = this.GetFSharpCompilationOptionsAsync(userOpName)
            return! checker.ParseDocument(this, parsingOptions, userOpName)
        }

    /// Parses and checks the given F# document.
    member this.GetFSharpParseAndCheckResultsAsync(userOpName) =
        async {
            let! checker, _, _, projectOptions = this.GetFSharpCompilationOptionsAsync(userOpName)
            match! checker.ParseAndCheckDocument(this, projectOptions, userOpName, allowStaleResults = false) with
            | Some(parseResults, _, checkResults) ->
                return (parseResults, checkResults)
            | _ ->
                return raise(System.OperationCanceledException("Unable to get FSharp parse and check results."))
        }

    /// Get the semantic classifications of the given F# document.
    member this.GetFSharpSemanticClassificationAsync(userOpName) =
        async {
            let! checker, _, _, projectOptions = this.GetFSharpCompilationOptionsAsync(userOpName)
            match! checker.GetBackgroundSemanticClassificationForFile(this.FilePath, projectOptions) with
            | Some results -> return results
            | _ -> return raise(System.OperationCanceledException("Unable to get FSharp semantic classification."))
        }

    /// Find F# references in the given F# document.
    member this.FindFSharpReferencesAsync(symbol, onFound, userOpName) =
        async {
            let! checker, _, _, projectOptions = this.GetFSharpCompilationOptionsAsync(userOpName)
            let! symbolUses = checker.FindBackgroundReferencesInFile(this.FilePath, projectOptions, symbol, canInvalidateProject = false)
            let! ct = Async.CancellationToken
            let! sourceText = this.GetTextAsync ct |> Async.AwaitTask
            for symbolUse in symbolUses do 
                match RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, symbolUse) with
                | Some textSpan ->
                    do! onFound textSpan symbolUse
                | _ ->
                    ()
        }

    /// Try to find a F# lexer/token symbol of the given F# document and position.
    member this.TryFindFSharpLexerSymbolAsync(position, lookupKind, wholeActivePattern, allowStringToken, userOpName) =
        async {
            let! defines = this.GetFSharpCompilationDefinesAsync(userOpName)
            let! ct = Async.CancellationToken
            let! sourceText = this.GetTextAsync(ct) |> Async.AwaitTask
            return Tokenizer.getSymbolAtPosition(this.Id, sourceText, position, this.FilePath, defines, lookupKind, wholeActivePattern, allowStringToken)
        }

    /// This is only used for testing purposes. It sets the ProjectCache.Projects with the given FSharpProjectOptions and F# document's project.
    member this.SetFSharpProjectOptionsForTesting(projectOptions: FSharpProjectOptions) =
        let workspaceService = this.Project.Solution.GetFSharpWorkspaceService()
        let parsingOptions, _, _ = 
            workspaceService.FSharpProjectOptionsManager.TryGetOptionsForDocumentOrProject(this, CancellationToken.None, nameof(this.SetFSharpProjectOptionsForTesting))
            |> Async.RunImmediateExceptOnUI
            |> Option.get
        ProjectCache.Projects.Add(this.Project, (workspaceService.Checker, workspaceService.FSharpProjectOptionsManager, parsingOptions, projectOptions))

type Project with

    /// Find F# references in the given project.
    member this.FindFSharpReferencesAsync(symbol, onFound, userOpName) =
        async {
            for doc in this.Documents do
                do! doc.FindFSharpReferencesAsync(symbol, (fun textSpan range -> onFound doc textSpan range), userOpName)
        }
