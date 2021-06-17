// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor
 
open System
open System.ComponentModel.Composition
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Diagnostics
open Microsoft.VisualStudio.FSharp.Editor
open Microsoft.VisualStudio.FSharp.Editor.Logging
open Microsoft.VisualStudio.Text.Editor.Commanding.Commands
open Microsoft.VisualStudio.Commanding
open Microsoft.VisualStudio.Utilities

// This causes re-analysis to happen when a F# document is saved.
// We do this because FCS relies on the file system and existing open documents
// need to be re-analyzed so the changes are propogated.
// We only re-analyze F# documents that are dependent on the document that was just saved.
// We ignore F# script documents here.
// REVIEW: This could be removed when Roslyn workspaces becomes the source of truth for FCS instead of the file system.
[<Export>]
[<Export(typeof<ICommandHandler>)>]
[<ContentType(Constants.FSharpContentType)>]
[<Name(Constants.FSharpAnalysisSaveFileHandler)>]
type internal FSharpAnalysisSaveFileCommandHandler
    [<ImportingConstructor>]
    (analyzerService: IFSharpDiagnosticAnalyzerService) =
    
    interface IChainedCommandHandler<SaveCommandArgs> with

        member _.DisplayName = Constants.FSharpAnalysisSaveFileHandler

        member _.ExecuteCommand(args: SaveCommandArgs, nextCommandHandler: Action, _) = 
            let textContainer = args.SubjectBuffer.AsTextContainer()
            match textContainer with
            | null -> ()
            | _ ->
                let mutable workspace = Unchecked.defaultof<_>
                if Workspace.TryGetWorkspace(textContainer, &workspace) then
                    let solution = workspace.CurrentSolution
                    let documentId = workspace.GetDocumentIdInCurrentContext(textContainer)
                    match box documentId with
                    | null -> ()
                    | _ -> 
                        let document = solution.GetDocument(documentId)
                        async {
                            try
                                if document.Project.Language = LanguageNames.FSharp then
                                    let openDocIds = workspace.GetOpenDocumentIds()

                                    let docIdsToReanalyze =
                                        if document.IsFSharpScript then
                                            openDocIds
                                            |> Seq.filter (fun x -> 
                                                x <> document.Id &&
                                                (
                                                    let doc = solution.GetDocument(x)
                                                    match doc with
                                                    | null -> false
                                                    | _ -> doc.IsFSharpScript
                                                )
                                            )
                                            |> Array.ofSeq
                                        else
                                            let depProjIds = document.Project.GetDependentProjectIds().Add(document.Project.Id)
                                            openDocIds
                                            |> Seq.filter (fun x ->
                                                depProjIds.Contains(x.ProjectId) && x <> document.Id &&
                                                (
                                                    let doc = solution.GetDocument(x)
                                                    match box doc with
                                                    | null -> false
                                                    | _ -> doc.Project.Language = LanguageNames.FSharp
                                                )
                                            )
                                            |> Array.ofSeq

                                    if docIdsToReanalyze.Length > 0 then
                                        analyzerService.Reanalyze(workspace, documentIds=docIdsToReanalyze)
                            with
                            | ex -> logException ex
                        }
                        |> Async.Start // fire and forget

            nextCommandHandler.Invoke()

        member _.GetCommandState(_, nextCommandHandler: Func<CommandState>) = 
            nextCommandHandler.Invoke()