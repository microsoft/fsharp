// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor
 
open System
open System.Collections.Generic
open System.Collections.Concurrent
open System.Collections.Immutable
open System.IO
open System.Linq
open Microsoft.CodeAnalysis
open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis
open Microsoft.VisualStudio.FSharp.Editor
open System.Threading
open Microsoft.VisualStudio.FSharp.Interactive.Session
open System.Runtime.CompilerServices

[<AutoOpen>]
module private FSharpProjectOptionsHelpers =

    let mapCpsProjectToSite(project:Project, cpsCommandLineOptions: IDictionary<ProjectId, string[] * string[]>) =
        let sourcePaths, referencePaths, options =
            match cpsCommandLineOptions.TryGetValue(project.Id) with
            | true, (sourcePaths, options) -> sourcePaths, [||], options
            | false, _ -> [||], [||], [||]
        let mutable errorReporter = Unchecked.defaultof<_>
        {
            new IProjectSite with
                member _.Description = project.Name
                member _.CompilationSourceFiles = sourcePaths
                member _.CompilationOptions =
                    Array.concat [options; referencePaths |> Array.map(fun r -> "-r:" + r)]
                member _.CompilationReferences = referencePaths
                member site.CompilationBinOutputPath = site.CompilationOptions |> Array.tryPick (fun s -> if s.StartsWith("-o:") then Some s.[3..] else None)
                member _.ProjectFileName = project.FilePath
                member _.AdviseProjectSiteChanges(_,_) = ()
                member _.AdviseProjectSiteCleaned(_,_) = ()
                member _.AdviseProjectSiteClosed(_,_) = ()
                member _.IsIncompleteTypeCheckEnvironment = false
                member _.TargetFrameworkMoniker = ""
                member _.ProjectGuid =  project.Id.Id.ToString()
                member _.LoadTime = System.DateTime.Now
                member _.ProjectProvider = None
                member _.BuildErrorReporter with get () = errorReporter and set (v) = errorReporter <- v
        }

    let hasProjectVersionChanged (oldProject: Project) (newProject: Project) =
        oldProject.Version <> newProject.Version

    let hasDependentVersionChanged (oldProject: Project) (newProject: Project) (ct: CancellationToken) =
        let oldProjectMetadataRefs = oldProject.MetadataReferences
        let newProjectMetadataRefs = newProject.MetadataReferences

        if oldProjectMetadataRefs.Count <> newProjectMetadataRefs.Count then true
        else

        let oldProjectRefs = oldProject.ProjectReferences
        let newProjectRefs = newProject.ProjectReferences

        oldProjectRefs.Count() <> newProjectRefs.Count() ||
        (oldProjectRefs, newProjectRefs)
        ||> Seq.exists2 (fun p1 p2 ->
            ct.ThrowIfCancellationRequested()
            let doesProjectIdDiffer = p1.ProjectId <> p2.ProjectId
            let p1 = oldProject.Solution.GetProject(p1.ProjectId)
            let p2 = newProject.Solution.GetProject(p2.ProjectId)
            doesProjectIdDiffer || 
            (
                if p1.IsFSharp then
                    p1.Version <> p2.Version
                else
                    let v1 = p1.GetDependentVersionAsync(ct).Result
                    let v2 = p2.GetDependentVersionAsync(ct).Result
                    v1 <> v2
            )
        )

    let isProjectInvalidated (oldProject: Project) (newProject: Project) ct =
        let hasProjectVersionChanged = hasProjectVersionChanged oldProject newProject
        if newProject.AreFSharpInMemoryCrossProjectReferencesEnabled then
            hasProjectVersionChanged || hasDependentVersionChanged oldProject newProject ct
        else
            hasProjectVersionChanged

[<RequireQualifiedAccess>]
type private FSharpProjectOptionsMessage =
    | TryGetOptionsByDocument of Document * AsyncReplyChannel<(FSharpParsingOptions * FSharpProjectOptions) option> * CancellationToken * userOpName: string
    | TryGetOptionsByProject of Project * AsyncReplyChannel<(FSharpParsingOptions * FSharpProjectOptions) option> * CancellationToken
    | ClearOptions of ProjectId
    | ClearSingleFileOptionsCache of DocumentId

[<Sealed>]
type private FSharpProjectOptionsReactor (checker: FSharpChecker) =
    let cancellationTokenSource = new CancellationTokenSource()

    // Store command line options
    let commandLineOptions = ConcurrentDictionary<ProjectId, string[] * string[]>()

    let legacyProjectSites = ConcurrentDictionary<ProjectId, IProjectSite>()

    let cache = ConcurrentDictionary<ProjectId, Project * FSharpParsingOptions * FSharpProjectOptions>()
    let singleFileCache = ConcurrentDictionary<DocumentId, Project * VersionStamp * FSharpParsingOptions * FSharpProjectOptions>()

    // This is used to not constantly emit the same compilation.
    let weakPEReferences = ConditionalWeakTable<Compilation, FSharpReferencedProject>()
    let lastSuccessfulCompilations = ConcurrentDictionary<ProjectId, Compilation>()

    let scriptUpdatedEvent = Event<FSharpProjectOptions>()

    let createPEReference (referencedProject: Project) (comp: Compilation) =
        let projectId = referencedProject.Id

        match weakPEReferences.TryGetValue comp with
        | true, fsRefProj -> fsRefProj
        | _ ->
            let weakComp = WeakReference<Compilation>(comp)
            let getStream =
                fun ct ->
                    let tryStream (comp: Compilation) =
                        let ms = new MemoryStream() // do not dispose the stream as it will be owned on the reference.
                        let emitOptions = Emit.EmitOptions(metadataOnly = true, includePrivateMembers = false, tolerateErrors = true)
                        try
                            let result = comp.Emit(ms, options = emitOptions, cancellationToken = ct)

                            if result.Success then
                                lastSuccessfulCompilations.[projectId] <- comp
                                ms.Position <- 0L
                                ms :> Stream
                                |> Some
                            else
                                ms.Dispose() // it failed, dispose of stream
                                None
                        with
                        | _ ->
                            ms.Dispose() // it failed, dispose of stream
                            None

                    let resultOpt =
                        match weakComp.TryGetTarget() with
                        | true, comp -> tryStream comp
                        | _ -> None

                    match resultOpt with
                    | Some _ -> resultOpt
                    | _ ->
                        match lastSuccessfulCompilations.TryGetValue(projectId) with
                        | true, comp -> tryStream comp
                        | _ -> None                           

            let fsRefProj =
                FSharpReferencedProject.CreatePortableExecutable(
                    referencedProject.OutputFilePath, 
                    DateTime.UtcNow,
                    getStream
                )
            weakPEReferences.Add(comp, fsRefProj)
            fsRefProj

    let rec tryComputeOptionsBySingleScriptOrFile (document: Document) (ct: CancellationToken) userOpName =
        async {
            let! fileStamp = document.GetTextVersionAsync(ct) |> Async.AwaitTask
            match singleFileCache.TryGetValue(document.Id) with
            | false, _ ->
                let! sourceText = document.GetTextAsync(ct) |> Async.AwaitTask
                
                let! scriptProjectOptions, _ =
                    checker.GetProjectOptionsFromScript(document.FilePath,
                        sourceText.ToFSharpSourceText(),
                        SessionsProperties.fsiPreview,
                        assumeDotNetFramework=not SessionsProperties.fsiUseNetCore,
                        userOpName=userOpName)

                let project = document.Project

                let otherOptions =
                    if project.IsFSharpMetadata then
                        project.ProjectReferences
                        |> Seq.map (fun x -> "-r:" + project.Solution.GetProject(x.ProjectId).OutputFilePath)
                        |> Array.ofSeq
                        |> Array.append (
                                project.MetadataReferences.OfType<PortableExecutableReference>()
                                |> Seq.map (fun x -> "-r:" + x.FilePath)
                                |> Array.ofSeq)
                    else
                        [||]

                let projectOptions =
                    if isScriptFile document.FilePath then
                        scriptUpdatedEvent.Trigger(scriptProjectOptions)
                        scriptProjectOptions
                    else
                        {
                            ProjectFileName = document.FilePath
                            ProjectId = None
                            SourceFiles = [|document.FilePath|]
                            OtherOptions = otherOptions
                            ReferencedProjects = [||]
                            IsIncompleteTypeCheckEnvironment = false
                            UseScriptResolutionRules = CompilerEnvironment.MustBeSingleFileProject (Path.GetFileName(document.FilePath))
                            LoadTime = DateTime.Now
                            UnresolvedReferences = None
                            OriginalLoadReferences = []
                            Stamp = Some(int64 (fileStamp.GetHashCode()))
                        }

                let parsingOptions, _ = checker.GetParsingOptionsFromProjectOptions(projectOptions)

                singleFileCache.[document.Id] <- (document.Project, fileStamp, parsingOptions, projectOptions)

                return Some(parsingOptions, projectOptions)

            | true, (oldProject, oldFileStamp, parsingOptions, projectOptions) ->
                if fileStamp <> oldFileStamp || isProjectInvalidated document.Project oldProject ct then
                    singleFileCache.TryRemove(document.Id) |> ignore
                    return! tryComputeOptionsBySingleScriptOrFile document ct userOpName
                else
                    return Some(parsingOptions, projectOptions)
        }

    let tryGetProjectSite (project: Project) =
        // Cps
        if commandLineOptions.ContainsKey project.Id then
            Some (mapCpsProjectToSite(project, commandLineOptions))
        else
            // Legacy
            match legacyProjectSites.TryGetValue project.Id with
            | true, site -> Some site
            | _ -> None
    
    let rec tryComputeOptions (project: Project) ct =
        async {
            let projectId = project.Id
            match cache.TryGetValue(projectId) with
            | false, _ ->

                // Because this code can be kicked off before the hack, HandleCommandLineChanges, occurs,
                //     the command line options will not be available and we should bail if one of the project references does not give us anything.
                let mutable canBail = false
            
                let referencedProjects = ResizeArray()

                if project.AreFSharpInMemoryCrossProjectReferencesEnabled then
                    for projectReference in project.ProjectReferences do
                        let referencedProject = project.Solution.GetProject(projectReference.ProjectId)
                        if referencedProject.Language = FSharpConstants.FSharpLanguageName then
                            match! tryComputeOptions referencedProject ct with
                            | None -> canBail <- true
                            | Some(_, projectOptions) -> referencedProjects.Add(FSharpReferencedProject.CreateFSharp(referencedProject.OutputFilePath, projectOptions))
                        elif referencedProject.SupportsCompilation then
                            let! comp = referencedProject.GetCompilationAsync(ct) |> Async.AwaitTask
                            let peRef = createPEReference referencedProject comp
                            referencedProjects.Add(peRef)

                if canBail then
                    return None
                else

                match tryGetProjectSite project with
                | None -> return None
                | Some projectSite ->             

                let otherOptions =
                    project.ProjectReferences
                    |> Seq.map (fun x -> "-r:" + project.Solution.GetProject(x.ProjectId).OutputFilePath)
                    |> Array.ofSeq
                    |> Array.append (
                            project.MetadataReferences.OfType<PortableExecutableReference>()
                            |> Seq.map (fun x -> "-r:" + x.FilePath)
                            |> Array.ofSeq
                            |> Array.append (
                                    // Clear any references from CompilationOptions. 
                                    // We get the references from Project.ProjectReferences/Project.MetadataReferences.
                                    projectSite.CompilationOptions
                                    |> Array.filter (fun x -> not (x.Contains("-r:")))
                                )
                        )

                let! ver = project.GetDependentVersionAsync(ct) |> Async.AwaitTask

                let projectOptions =
                    {
                        ProjectFileName = projectSite.ProjectFileName
                        ProjectId = Some(projectId.ToFSharpProjectIdString())
                        SourceFiles = projectSite.CompilationSourceFiles
                        OtherOptions = otherOptions
                        ReferencedProjects = referencedProjects.ToArray()
                        IsIncompleteTypeCheckEnvironment = projectSite.IsIncompleteTypeCheckEnvironment
                        UseScriptResolutionRules = CompilerEnvironment.MustBeSingleFileProject (Path.GetFileName(project.FilePath))
                        LoadTime = projectSite.LoadTime
                        UnresolvedReferences = None
                        OriginalLoadReferences = []
                        Stamp = Some(int64 (ver.GetHashCode()))
                    }

                // This can happen if we didn't receive the callback from HandleCommandLineChanges yet.
                if Array.isEmpty projectOptions.SourceFiles then
                    return None
                else
                    // Clear any caches that need clearing and invalidate the project.
                    let currentSolution = project.Solution.Workspace.CurrentSolution
                    let projectsToClearCache =
                        cache
                        |> Seq.filter (fun pair -> not (currentSolution.ContainsProject pair.Key))

                    if not (Seq.isEmpty projectsToClearCache) then
                        projectsToClearCache
                        |> Seq.iter (fun pair -> cache.TryRemove pair.Key |> ignore)
                        let options =
                            projectsToClearCache
                            |> Seq.map (fun pair ->
                                let _, _, projectOptions = pair.Value
                                projectOptions)
                        checker.ClearCache(options, userOpName = "tryComputeOptions")

                    lastSuccessfulCompilations.ToArray()
                    |> Array.iter (fun pair ->
                        if not (currentSolution.ContainsProject(pair.Key)) then
                            lastSuccessfulCompilations.TryRemove(pair.Key) |> ignore
                    )

                    checker.InvalidateConfiguration(projectOptions, userOpName = "tryComputeOptions")

                    let parsingOptions, _ = checker.GetParsingOptionsFromProjectOptions(projectOptions)

                    cache.[projectId] <- (project, parsingOptions, projectOptions)

                    return Some(parsingOptions, projectOptions)
  
            | true, (oldProject, parsingOptions, projectOptions) ->
                if isProjectInvalidated oldProject project ct then
                    cache.TryRemove(projectId) |> ignore
                    return! tryComputeOptions project ct
                else
                    return Some(parsingOptions, projectOptions)
        }

    let loop (agent: MailboxProcessor<FSharpProjectOptionsMessage>) =
        async {
            while true do
                match! agent.Receive() with
                | FSharpProjectOptionsMessage.TryGetOptionsByDocument(document, reply, ct, userOpName) ->
                    if ct.IsCancellationRequested then
                        reply.Reply None
                    else
                        try
                            // For now, disallow miscellaneous workspace since we are using the hacky F# miscellaneous files project.
                            if document.Project.Solution.Workspace.Kind = WorkspaceKind.MiscellaneousFiles then
                                reply.Reply None
                            elif document.Project.IsFSharpMiscellaneousOrMetadata then
                                let! options = tryComputeOptionsBySingleScriptOrFile document ct userOpName
                                if ct.IsCancellationRequested then
                                    reply.Reply None
                                else
                                    reply.Reply options
                            else
                                // We only care about the latest project in the workspace's solution.
                                // We do this to prevent any possible cache thrashing in FCS.
                                let project = document.Project.Solution.Workspace.CurrentSolution.GetProject(document.Project.Id)
                                if not (isNull project) then
                                    let! options = tryComputeOptions project ct
                                    if ct.IsCancellationRequested then
                                        reply.Reply None
                                    else
                                        reply.Reply options
                                else
                                    reply.Reply None
                        with
                        | _ ->
                            reply.Reply None

                | FSharpProjectOptionsMessage.TryGetOptionsByProject(project, reply, ct) ->
                    if ct.IsCancellationRequested then
                        reply.Reply None
                    else
                        try
                            if project.Solution.Workspace.Kind = WorkspaceKind.MiscellaneousFiles || project.IsFSharpMiscellaneousOrMetadata then
                                reply.Reply None
                            else
                                // We only care about the latest project in the workspace's solution.
                                // We do this to prevent any possible cache thrashing in FCS.
                                let project = project.Solution.Workspace.CurrentSolution.GetProject(project.Id)
                                if not (isNull project) then
                                    let! options = tryComputeOptions project ct
                                    if ct.IsCancellationRequested then
                                        reply.Reply None
                                    else
                                        reply.Reply options
                                else
                                    reply.Reply None
                        with
                        | _ ->
                            reply.Reply None

                | FSharpProjectOptionsMessage.ClearOptions(projectId) ->
                    match cache.TryRemove(projectId) with
                    | true, (_, _, projectOptions) ->
                        lastSuccessfulCompilations.TryRemove(projectId) |> ignore
                        checker.ClearCache([projectOptions])
                    | _ ->
                        ()
                    legacyProjectSites.TryRemove(projectId) |> ignore
                | FSharpProjectOptionsMessage.ClearSingleFileOptionsCache(documentId) ->
                    match singleFileCache.TryRemove(documentId) with
                    | true, (_, _, _, projectOptions) ->
                        lastSuccessfulCompilations.TryRemove(documentId.ProjectId) |> ignore
                        checker.ClearCache([projectOptions])
                    | _ ->
                        ()
        }

    let agent = MailboxProcessor.Start((fun agent -> loop agent), cancellationToken = cancellationTokenSource.Token)

    member _.TryGetOptionsByProjectAsync(project, ct) =
        agent.PostAndAsyncReply(fun reply -> FSharpProjectOptionsMessage.TryGetOptionsByProject(project, reply, ct))

    member _.TryGetOptionsByDocumentAsync(document, ct, userOpName) =
        agent.PostAndAsyncReply(fun reply -> FSharpProjectOptionsMessage.TryGetOptionsByDocument(document, reply, ct, userOpName))

    member _.ClearOptionsByProjectId(projectId) =
        agent.Post(FSharpProjectOptionsMessage.ClearOptions(projectId))

    member _.ClearSingleFileOptionsCache(documentId) =
        agent.Post(FSharpProjectOptionsMessage.ClearSingleFileOptionsCache(documentId))

    member _.SetCommandLineOptions(projectId, sourcePaths, options) =
        commandLineOptions.[projectId] <- (sourcePaths, options)

    member _.SetLegacyProjectSite (projectId, projectSite) =
        legacyProjectSites.[projectId] <- projectSite

    member _.TryGetCachedOptionsByProjectId(projectId) =
        match cache.TryGetValue(projectId) with
        | true, result -> Some(result)
        | _ -> None

    member _.ClearAllCaches() =
        commandLineOptions.Clear()
        legacyProjectSites.Clear()
        cache.Clear()
        singleFileCache.Clear()
        lastSuccessfulCompilations.Clear()

    member _.ScriptUpdated = scriptUpdatedEvent.Publish

    interface IDisposable with
        member _.Dispose() = 
            cancellationTokenSource.Cancel()
            cancellationTokenSource.Dispose() 
            (agent :> IDisposable).Dispose()

/// Manages mappings of Roslyn workspace Projects/Documents to FCS.
type internal FSharpProjectOptionsManager
    (
        checker: FSharpChecker,
        workspace: Workspace
    ) =

    let reactor = new FSharpProjectOptionsReactor(checker)

    do
        // We need to listen to this event for lifecycle purposes.
        workspace.WorkspaceChanged.Add(fun args ->
            match args.Kind with
            | WorkspaceChangeKind.ProjectRemoved -> reactor.ClearOptionsByProjectId(args.ProjectId)
            | _ -> ()
        )

        workspace.DocumentClosed.Add(fun args ->
            let doc = args.Document
            let proj = doc.Project
            if proj.IsFSharp && proj.IsFSharpMiscellaneousOrMetadata then
                reactor.ClearSingleFileOptionsCache(doc.Id)
        )

    member _.ScriptUpdated = reactor.ScriptUpdated

    member _.SetLegacyProjectSite (projectId, projectSite) =
        reactor.SetLegacyProjectSite (projectId, projectSite)

    /// Clear a project from the project table
    member this.ClearInfoForProject(projectId:ProjectId) = 
        reactor.ClearOptionsByProjectId(projectId)

    member this.ClearSingleFileOptionsCache(documentId) =
        reactor.ClearSingleFileOptionsCache(documentId)

    /// Get compilation defines relevant for syntax processing.  
    /// Quicker then TryGetOptionsForDocumentOrProject as it doesn't need to recompute the exact project 
    /// options for a script.
    member this.GetCompilationDefinesForEditingDocument(document:Document) = 
        let parsingOptions =
            match reactor.TryGetCachedOptionsByProjectId(document.Project.Id) with
            | Some (_, parsingOptions, _) -> parsingOptions
            | _ -> { FSharpParsingOptions.Default with IsInteractive = CompilerEnvironment.IsScriptFile document.Name }
        CompilerEnvironment.GetCompilationDefinesForEditing parsingOptions     

    member this.TryGetOptionsByProject(project) =
        reactor.TryGetOptionsByProjectAsync(project)

    /// Get the exact options for a document or project
    member this.TryGetOptionsForDocumentOrProject(document: Document, cancellationToken, userOpName) =
        async { 
            match! reactor.TryGetOptionsByDocumentAsync(document, cancellationToken, userOpName) with
            | Some(parsingOptions, projectOptions) ->
                return Some(parsingOptions, None, projectOptions)
            | _ ->
                return None
        }

    /// Get the exact options for a document or project relevant for syntax processing.
    member this.TryGetOptionsForEditingDocumentOrProject(document:Document, cancellationToken, userOpName) = 
        async {
            let! result = this.TryGetOptionsForDocumentOrProject(document, cancellationToken, userOpName) 
            return result |> Option.map(fun (parsingOptions, _, projectOptions) -> parsingOptions, projectOptions)
        }

    /// Get the options for a document or project relevant for syntax processing.
    /// Quicker it doesn't need to recompute the exact project options for a script.
    member this.TryGetQuickParsingOptionsForEditingDocumentOrProject(document:Document) = 
        match reactor.TryGetCachedOptionsByProjectId(document.Project.Id) with
        | Some (_, parsingOptions, _) -> parsingOptions
        | _ -> { FSharpParsingOptions.Default with IsInteractive = CompilerEnvironment.IsScriptFile document.Name }

    member this.SetCommandLineOptions(projectId, sourcePaths, options: ImmutableArray<string>) =
        reactor.SetCommandLineOptions(projectId, sourcePaths, options.ToArray())

    member this.ClearAllCaches() =
        reactor.ClearAllCaches()

    member _.Checker = checker
