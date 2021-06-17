[<AutoOpen>]
module internal Microsoft.VisualStudio.FSharp.Editor.FSharpCheckerExtensions

open System

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Text

open FSharp.Compiler.CodeAnalysis

type FSharpChecker with
    member checker.ParseDocument(document: Document, parsingOptions: FSharpParsingOptions, userOpName: string) =
        asyncMaybe {
            let! ct = Async.CancellationToken |> liftAsync

            let! sourceText = document.GetTextAsync(ct) |> liftTaskAsync

            let! fileParseResults = checker.ParseFile(document.FilePath, sourceText.ToFSharpSourceText(), parsingOptions, userOpName=userOpName) |> liftAsync
            return fileParseResults
        }

    member checker.CheckDocument(document: Document, parseResults: FSharpParseFileResults, options: FSharpProjectOptions, userOpName: string) =
        async {
            let! ct = Async.CancellationToken

            let! sourceText = document.GetTextAsync(ct) |> Async.AwaitTask
            let! textVersion = document.GetTextVersionAsync(ct) |> Async.AwaitTask

            let filePath = document.FilePath
            let textVersionHash = textVersion.GetHashCode()
            
            return! checker.CheckFileInProject(parseResults, filePath, textVersionHash, sourceText.ToFSharpSourceText(), options,userOpName=userOpName)
        }

    member checker.ParseAndCheckDocument(document: Document, options: FSharpProjectOptions, languageServicePerformanceOptions: LanguageServicePerformanceOptions, userOpName: string) =
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
                    let! worker = Async.StartChild(async { try return! parseAndCheckFile with | _ -> return None }, millisecondsTimeout=languageServicePerformanceOptions.TimeUntilStaleCompletion)
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

            if languageServicePerformanceOptions.AllowStaleCompletionResults then
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

    member checker.ParseAndCheckDocument(document: Document, options: FSharpProjectOptions, userOpName: string, ?allowStaleResults: bool) =
        async {
            let perfOpts =
                match allowStaleResults with 
                | Some b -> { document.FSharpOptions.LanguageServicePerformance with AllowStaleCompletionResults = b } 
                | _ ->  document.FSharpOptions.LanguageServicePerformance
            return! checker.ParseAndCheckDocument(document, options, perfOpts, userOpName=userOpName)
        }
