[<AutoOpen>]
module internal Microsoft.VisualStudio.FSharp.Editor.FSharpCheckerExtensions

open Microsoft.CodeAnalysis
open FSharp.Compiler.CodeAnalysis

type FSharpChecker with
    member checker.ParseDocument(document: Document, options: FSharpParsingOptions) =
        async {
            let! ct = Async.CancellationToken
            let! sourceText = document.GetTextAsync(ct) |> Async.AwaitTask
            return! checker.ParseFile(document.FilePath, sourceText.ToFSharpSourceText(), options, cache = true)
        }

    member checker.CheckDocumentInProject(document: Document, options: FSharpProjectOptions) =
        async {
            return! checker.GetBackgroundCheckResultsForFileInProject(document.FilePath, options)
        }
