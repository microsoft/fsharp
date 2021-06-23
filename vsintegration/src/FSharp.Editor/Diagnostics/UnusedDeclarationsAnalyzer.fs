// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace rec Microsoft.VisualStudio.FSharp.Editor

open System
open System.Composition
open System.Collections.Immutable
open System.Diagnostics

open Microsoft.CodeAnalysis
open FSharp.Compiler.EditorServices
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Diagnostics

[<Export(typeof<IFSharpUnusedDeclarationsDiagnosticAnalyzer>)>]
type internal UnusedDeclarationsAnalyzer
    [<ImportingConstructor>]
    (
        checkerProvider: FSharpCheckerProvider, 
        projectInfoManager: FSharpProjectOptionsManager
    ) =
    
    static let userOpName = "UnusedDeclarationsAnalyzer"

    interface IFSharpUnusedDeclarationsDiagnosticAnalyzer with

        member _.AnalyzeSemanticsAsync(descriptor, document, cancellationToken) =
            if document.Project.IsFSharpMiscellaneousOrMetadata && not document.IsFSharpScript then Threading.Tasks.Task.FromResult(ImmutableArray.Empty)
            else

            asyncMaybe {
                do! Option.guard document.FSharpOptions.CodeFixes.UnusedDeclarations

                do Trace.TraceInformation("{0:n3} (start) UnusedDeclarationsAnalyzer", DateTime.Now.TimeOfDay.TotalSeconds)
                match! projectInfoManager.TryGetOptionsForEditingDocumentOrProject(document, cancellationToken, userOpName) with
                | (_parsingOptions, projectOptions) ->
                    let! sourceText = document.GetTextAsync()
                    let checker = checkerProvider.Checker
                    let! _, checkResults = checker.CheckDocumentInProject(document, projectOptions) |> liftAsync
                    let! unusedRanges = UnusedDeclarations.getUnusedDeclarations( checkResults, (isScriptFile document.FilePath)) |> liftAsync
                    return
                        unusedRanges
                        |> Seq.map (fun m -> Diagnostic.Create(descriptor, RoslynHelpers.RangeToLocation(m, sourceText, document.FilePath)))
                        |> Seq.toImmutableArray
            }
            |> Async.map (Option.defaultValue ImmutableArray.Empty)
            |> RoslynHelpers.StartAsyncAsTask cancellationToken
