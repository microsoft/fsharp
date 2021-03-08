[<AutoOpen>]
module internal Microsoft.VisualStudio.FSharp.Editor.FSharpCheckerExtensions

open System

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Text

open FSharp.Compiler.CodeAnalysis

type FSharpChecker with
    member checker.ParseDocument(document: Document, parsingOptions: FSharpParsingOptions, sourceText: SourceText, userOpName: string) =
        asyncMaybe {
            let! fileParseResults = checker.ParseFile(document.FilePath, sourceText.ToFSharpSourceText(), parsingOptions, userOpName=userOpName) |> liftAsync
            return fileParseResults.ParseTree
        }

    member checker.ParseAndCheckDocument(filePath: string, textVersionHash: int, sourceText: SourceText, options: FSharpProjectOptions, languageServicePerformanceOptions: LanguageServicePerformanceOptions, userOpName: string) =
        async {
            let parseAndCheckFile =
                async {
                    let! parseResults, checkFileAnswer = checker.ParseAndCheckFileInProject(filePath, textVersionHash, sourceText.ToFSharpSourceText(), options, userOpName=userOpName)
                    return
                        match checkFileAnswer with
                        | FSharpCheckFileAnswer.Aborted -> 
                            None
                        | FSharpCheckFileAnswer.Succeeded(checkFileResults) ->
                            Some (parseResults, checkFileResults)
                }

            let tryGetFreshResultsWithTimeout() =
                async {
                    let! worker = Async.StartChild(parseAndCheckFile, millisecondsTimeout=languageServicePerformanceOptions.TimeUntilStaleCompletion)
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
                            match checker.TryGetRecentCheckResultsForFile(filePath, options) with
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


    member checker.ParseAndCheckDocument(document: Document, options: FSharpProjectOptions, userOpName: string, ?allowStaleResults: bool, ?sourceText: SourceText) =
        async {
            let! cancellationToken = Async.CancellationToken
            let! sourceText =
                match sourceText with
                | Some x -> async.Return x
                | None -> document.GetTextAsync(cancellationToken)  |> Async.AwaitTask
            let! textVersion = document.GetTextVersionAsync(cancellationToken) |> Async.AwaitTask
            let perfOpts =
                match allowStaleResults with 
                | Some b -> { document.FSharpOptions.LanguageServicePerformance with AllowStaleCompletionResults = b } 
                | _ ->  document.FSharpOptions.LanguageServicePerformance
            return! checker.ParseAndCheckDocument(document.FilePath, textVersion.GetHashCode(), sourceText, options, perfOpts, userOpName=userOpName)
        }


    member checker.TryParseAndCheckFileInProject (projectOptions, fileName, sourceText: SourceText, userOpName) = async {
        let! (parseResults, checkAnswer) = checker.ParseAndCheckFileInProject (fileName,0,sourceText.ToFSharpSourceText(),projectOptions, userOpName=userOpName)
        match checkAnswer with
        | FSharpCheckFileAnswer.Aborted ->  return  None
        | FSharpCheckFileAnswer.Succeeded checkResults -> return Some (parseResults,checkResults)
    }
