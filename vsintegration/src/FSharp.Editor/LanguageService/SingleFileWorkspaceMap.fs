// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Collections.Concurrent
open System.Collections.Immutable
open Microsoft.CodeAnalysis
open Microsoft.VisualStudio
open Microsoft.VisualStudio.FSharp.Editor
open Microsoft.VisualStudio.LanguageServices.Implementation.ProjectSystem
open Microsoft.VisualStudio.LanguageServices.ProjectSystem
open Microsoft.VisualStudio.Shell.Interop
open Microsoft.VisualStudio.LanguageServices
open FSharp.Compiler.CodeAnalysis

type internal IFSharpWorkspaceProjectContext =
    inherit IDisposable

    abstract Id : ProjectId

    abstract FilePath : string

    abstract ProjectReferenceCount : int

    abstract HasProjectReference : filePath: string -> bool

    abstract SetProjectReferences : IFSharpWorkspaceProjectContext seq -> unit

type internal IFSharpWorkspaceProjectContextFactory =

    abstract CreateProjectContext : filePath: string -> IFSharpWorkspaceProjectContext

type internal FSharpWorkspaceProjectContext(vsProjectContext: IWorkspaceProjectContext) =

    let mutable refs = ImmutableDictionary.Create(StringComparer.OrdinalIgnoreCase)

    member private _.VisualStudioProjectContext = vsProjectContext

    member private _.AddProjectReference(builder: ImmutableDictionary<_, _>.Builder, projectContext: IFSharpWorkspaceProjectContext) =
        match projectContext with
        | :? FSharpWorkspaceProjectContext as fsProjectContext ->
            vsProjectContext.AddProjectReference(fsProjectContext.VisualStudioProjectContext, MetadataReferenceProperties.Assembly)
            builder.Add(projectContext.FilePath, projectContext)
        | _ ->
            ()

    member private _.RemoveProjectReference(projectContext: IFSharpWorkspaceProjectContext) =
        match projectContext with
        | :? FSharpWorkspaceProjectContext as fsProjectContext ->
            vsProjectContext.RemoveProjectReference(fsProjectContext.VisualStudioProjectContext)
        | _ ->
            ()

    interface IFSharpWorkspaceProjectContext with

        member _.Id = vsProjectContext.Id

        member _.FilePath = vsProjectContext.ProjectFilePath

        member _.ProjectReferenceCount = refs.Count

        member _.HasProjectReference(filePath) = refs.ContainsKey(filePath)

        member this.SetProjectReferences(projRefs) =
            let builder = ImmutableDictionary.CreateBuilder()

            refs.Values
            |> Seq.iter (fun x ->
                this.RemoveProjectReference(x)
            )

            projRefs
            |> Seq.iter (fun x ->
                this.AddProjectReference(builder, x)
            )

            refs <- builder.ToImmutable()

        member _.Dispose() =
            vsProjectContext.Dispose()

type internal FSharpWorkspaceProjectContextFactory(projectContextFactory: IWorkspaceProjectContextFactory) =

    static let createSourceCodeKind (filePath: string) =
        if isScriptFile filePath then
            SourceCodeKind.Script
        else
            SourceCodeKind.Regular

    interface IFSharpWorkspaceProjectContextFactory with

        member _.CreateProjectContext filePath =
            let projectContext = projectContextFactory.CreateProjectContext(FSharpConstants.FSharpLanguageName, filePath, filePath, Guid.NewGuid(), null, null)
            projectContext.DisplayName <- FSharpConstants.FSharpMiscellaneousFilesName
            projectContext.AddSourceFile(filePath, sourceCodeKind = createSourceCodeKind filePath)
            new FSharpWorkspaceProjectContext(projectContext) :> IFSharpWorkspaceProjectContext

type internal FSharpMiscellaneousFileService(workspace: Workspace, 
                                             miscFilesWorkspace: Workspace, 
                                             projectContextFactory: IFSharpWorkspaceProjectContextFactory) =

    // We have a lock because the `ScriptUpdated` event may happen concurrently when a document opens or closes.
    let gate = obj()
    let files = ConcurrentDictionary(StringComparer.OrdinalIgnoreCase)
    let optionsManager = workspace.Services.GetRequiredService<IFSharpWorkspaceService>().FSharpProjectOptionsManager

    static let mustUpdateProject (refSourceFiles: string []) (projectContext: IFSharpWorkspaceProjectContext) =
        refSourceFiles.Length <> projectContext.ProjectReferenceCount ||
        (
             refSourceFiles 
             |> Seq.forall projectContext.HasProjectReference
             |> not
        )

    let tryRemove (document: Document) =
        let projIds = document.Project.Solution.GetDependentProjectIds(document.Project.Id)
        if projIds.Count = 0 then
            optionsManager.ClearSingleFileOptionsCache(document.Id)

            match files.TryRemove(document.FilePath) with
            | true, projectContext ->
                (projectContext :> IDisposable).Dispose()
            | _ ->
                ()

    do
        optionsManager.ScriptUpdated.Add(fun scriptProjectOptions ->
            if scriptProjectOptions.SourceFiles.Length > 0 then
                // The last file in the project options is the main script file.
                let filePath = scriptProjectOptions.SourceFiles.[scriptProjectOptions.SourceFiles.Length - 1]
                let refSourceFiles = scriptProjectOptions.SourceFiles |> Array.take (scriptProjectOptions.SourceFiles.Length - 1)

                match files.TryGetValue(filePath) with
                | true, (projectContext: IFSharpWorkspaceProjectContext) ->
                    if mustUpdateProject refSourceFiles projectContext then
                        lock gate (fun () ->
                            let newProjRefs =
                                refSourceFiles
                                |> Array.map (fun filePath ->
                                    match files.TryGetValue(filePath) with
                                    | true, refProjectContext -> refProjectContext
                                    | _ ->
                                        let refProjectContext = projectContextFactory.CreateProjectContext(filePath)
                                        files.[filePath] <- refProjectContext
                                        refProjectContext
                                )

                            projectContext.SetProjectReferences(newProjRefs)
                        )
                | _ ->
                    ()
        )

        miscFilesWorkspace.DocumentOpened.Add(fun args ->
            let document = args.Document

            // If the file does not exist in the current solution, then we can create new project in the VisualStudioWorkspace that represents
            // a F# miscellaneous project, which could be a script or not.
            if document.Project.IsFSharp && workspace.CurrentSolution.GetDocumentIdsWithFilePath(document.FilePath).Length = 0 then
                let filePath = document.FilePath
                lock gate (fun () ->
                    if files.ContainsKey(filePath) |> not then
                        files.[filePath] <- projectContextFactory.CreateProjectContext(filePath)
                )
        )

        workspace.DocumentOpened.Add(fun args ->
            let document = args.Document
            if not document.Project.IsFSharpMiscellaneousOrMetadata then
                if files.ContainsKey(document.FilePath) then
                    lock gate (fun () ->
                        tryRemove document
                    )
        )

        workspace.DocumentClosed.Add(fun args ->
            let document = args.Document
            if document.Project.IsFSharpMiscellaneousOrMetadata then
                lock gate (fun () ->
                    tryRemove document
                )
        )

        workspace.WorkspaceChanged.Add(fun args ->
            match args.Kind with
            | WorkspaceChangeKind.ProjectRemoved ->
                let proj = args.OldSolution.GetProject(args.ProjectId)
                if proj.IsFSharpMiscellaneousOrMetadata then
                    let projRefs = 
                        proj.GetAllProjectsThisProjectDependsOn()
                        |> Array.ofSeq

                    if projRefs.Length > 0 then
                        lock gate (fun () ->
                            projRefs
                            |> Array.iter (fun proj ->
                                let proj = args.NewSolution.GetProject(proj.Id)
                                match proj with
                                | null -> ()
                                | _ ->
                                    if proj.IsFSharpMiscellaneousOrMetadata then
                                        match proj.Documents |> Seq.tryExactlyOne with
                                        | Some doc when not (workspace.IsDocumentOpen(doc.Id)) ->
                                            tryRemove doc
                                        | _ ->
                                            ()
                            )
                        )
            | _ ->
                ()
        )

    member _.Workspace = workspace

    member _.ProjectContextFactory = projectContextFactory

    member _.ContainsFile filePath = files.ContainsKey(filePath)

    member _.RenameFile(filePath, newFilePath) =
        match files.TryRemove(filePath) with
        | true, projectContext ->
            let project = workspace.CurrentSolution.GetProject(projectContext.Id)
            if project <> null then
                let documentOpt =
                    project.Documents 
                    |> Seq.tryFind (fun x -> String.Equals(x.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                match documentOpt with
                | None -> ()
                | Some(document) ->                           
                    optionsManager.ClearSingleFileOptionsCache(document.Id)
                    projectContext.Dispose()
                    files.[newFilePath] <- projectContextFactory.CreateProjectContext(newFilePath)
            else
                projectContext.Dispose() // fallback, shouldn't happen, but in case it does let's dispose of the project context so we don't leak
        | _ -> ()

[<Sealed>]
type internal SingleFileWorkspaceMap(miscFileService: FSharpMiscellaneousFileService,
                                     rdt: IVsRunningDocumentTable) as this =

    do
        rdt.AdviseRunningDocTableEvents(this) |> ignore

    interface IVsRunningDocTableEvents with

        member _.OnAfterAttributeChange(_, _) = VSConstants.E_NOTIMPL

        member _.OnAfterDocumentWindowHide(_, _) = VSConstants.E_NOTIMPL

        member _.OnAfterFirstDocumentLock(_, _, _, _) = VSConstants.E_NOTIMPL

        member _.OnAfterSave(_) = VSConstants.E_NOTIMPL

        member _.OnBeforeDocumentWindowShow(_, _, _) = VSConstants.E_NOTIMPL

        member _.OnBeforeLastDocumentUnlock(_, _, _, _) = VSConstants.E_NOTIMPL

    interface IVsRunningDocTableEvents2 with

        member _.OnAfterAttributeChange(_, _) = VSConstants.E_NOTIMPL

        member _.OnAfterAttributeChangeEx(_, grfAttribs, _, _, pszMkDocumentOld, _, _, pszMkDocumentNew) = 
            // Handles renaming of a misc file
            if (grfAttribs &&& (uint32 __VSRDTATTRIB.RDTA_MkDocument)) <> 0u && miscFileService.ContainsFile(pszMkDocumentOld) then
                miscFileService.RenameFile(pszMkDocumentOld, pszMkDocumentNew)
            VSConstants.S_OK

        member _.OnAfterDocumentWindowHide(_, _) = VSConstants.E_NOTIMPL

        member _.OnAfterFirstDocumentLock(_, _, _, _) = VSConstants.E_NOTIMPL

        member _.OnAfterSave(_) = VSConstants.E_NOTIMPL

        member _.OnBeforeDocumentWindowShow(_, _, _) = VSConstants.E_NOTIMPL

        member _.OnBeforeLastDocumentUnlock(_, _, _, _) = VSConstants.E_NOTIMPL