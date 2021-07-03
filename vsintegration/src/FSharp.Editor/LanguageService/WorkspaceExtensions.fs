[<AutoOpen>]
module internal Microsoft.VisualStudio.FSharp.Editor.WorkspaceExtensions

open System
open System.IO
open System.Collections.Immutable
open System.Collections.Generic
open System.Collections.Concurrent
open System.Linq
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks
open Microsoft.CodeAnalysis
open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis
open Microsoft.VisualStudio.LanguageServices
open Microsoft.VisualStudio.Threading
open FSharp.NativeInterop
open Microsoft.VisualStudio.FSharp.Interactive.Session

#nowarn "9" // NativePtr.toNativeInt

[<AutoOpen>]
module private Helpers =
    
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

type Solution with

    /// Get the instance of IFSharpWorkspaceService.
    member private this.GetFSharpWorkspaceService() =
        this.Workspace.Services.GetRequiredService<IFSharpWorkspaceService>()
    
let private createPEReference (referencedProject: Project) (comp: Compilation) =
    let projectId = referencedProject.Id

    match FSharpProjectCaches.weakPEReferences.TryGetValue comp with
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
                            FSharpProjectCaches.lastSuccessfulCompilations.[projectId] <- comp
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
                    match FSharpProjectCaches.lastSuccessfulCompilations.TryGetValue(projectId) with
                    | true, comp -> tryStream comp
                    | _ -> None                           

        let fsRefProj =
            FSharpReferencedProject.CreatePortableExecutable(
                referencedProject.OutputFilePath, 
                DateTime.UtcNow,
                getStream
            )
        FSharpProjectCaches.weakPEReferences.Add(comp, fsRefProj)
        fsRefProj

let tryGetProjectSite (project: Project) =
    let workspaceService = project.Solution.GetFSharpWorkspaceService()
    // Cps
    if workspaceService.CommandLineOptions.ContainsKey project.Id then
        Some (mapCpsProjectToSite(project, workspaceService.CommandLineOptions))
    else
        // Legacy
        match workspaceService.LegacyProjectSites.TryGetValue project.Id with
        | true, site -> Some site
        | _ -> None

let private legacyReferenceResolver = LegacyMSBuildReferenceResolver.getResolver()

type Workspace with

    member this.ClearFSharpCaches() =
        FSharpProjectCaches.CurrentScriptOrSingleFiles.Clear()
        FSharpProjectCaches.CurrentProjects.Clear()
        FSharpProjectCaches.lastSuccessfulCompilations.Clear()

    member this.GetFSharpTryGetMetadataSnapshotFunction() =
        let tryGetMetadataSnapshot (path, timeStamp) = 
            match this with
            | :? VisualStudioWorkspace as workspace ->
                try
                    let md = Microsoft.CodeAnalysis.ExternalAccess.FSharp.LanguageServices.FSharpVisualStudioWorkspaceExtensions.GetMetadata(workspace, path, timeStamp)
                    let amd = (md :?> AssemblyMetadata)
                    let mmd = amd.GetModules().[0]
                    let mmr = mmd.GetMetadataReader()

                    // "lifetime is timed to Metadata you got from the GetMetadata(...). As long as you hold it strongly, raw 
                    // memory we got from metadata reader will be alive. Once you are done, just let everything go and 
                    // let finalizer handle resource rather than calling Dispose from Metadata directly. It is shared metadata. 
                    // You shouldn't dispose it directly."

                    let objToHold = box md

                    // We don't expect any ilread WeakByteFile to be created when working in Visual Studio
                    // Debug.Assert((FSharp.Compiler.AbstractIL.ILBinaryReader.GetStatistics().weakByteFileCount = 0), "Expected weakByteFileCount to be zero when using F# in Visual Studio. Was there a problem reading a .NET binary?")

                    Some (objToHold, NativePtr.toNativeInt mmr.MetadataPointer, mmr.MetadataLength)
                with ex -> 
                    // We catch all and let the backup routines in the F# compiler find the error
                    Assert.Exception(ex)
                    None 
            | _ ->
                None
        tryGetMetadataSnapshot

type Project with

    member private this.TryCreateFSharpProjectOptionsAsync() =
        async {
            let! ct = Async.CancellationToken
            
            let referencedProjects = ResizeArray()

            if this.AreFSharpInMemoryCrossProjectReferencesEnabled then
                for projectReference in this.ProjectReferences do
                    let referencedProject = this.Solution.GetProject(projectReference.ProjectId)
                    if referencedProject.Language = FSharpConstants.FSharpLanguageName then
                        let! fsProjectRef = referencedProject.GetOrCreateFSharpProjectAsync()
                        referencedProjects.Add(fsProjectRef.ToReferencedProject())
                    elif referencedProject.SupportsCompilation then
                        let! comp = referencedProject.GetCompilationAsync(ct) |> Async.AwaitTask
                        let peRef = createPEReference referencedProject comp
                        referencedProjects.Add(peRef)

            match tryGetProjectSite this with
            | None -> return None
            | Some projectSite ->             

            let otherOptions =
                this.ProjectReferences
                |> Seq.map (fun x -> "-r:" + this.Solution.GetProject(x.ProjectId).OutputFilePath)
                |> Array.ofSeq
                |> Array.append (
                        this.MetadataReferences.OfType<PortableExecutableReference>()
                        |> Seq.map (fun x -> "-r:" + x.FilePath)
                        |> Array.ofSeq
                        |> Array.append (
                                // Clear any references from CompilationOptions. 
                                // We get the references from Project.ProjectReferences/Project.MetadataReferences.
                                projectSite.CompilationOptions
                                |> Array.filter (fun x -> not (x.Contains("-r:")))
                            )
                    )

            let! ver = this.GetDependentVersionAsync(ct) |> Async.AwaitTask

            let projectOptions =
                {
                    ProjectFileName = projectSite.ProjectFileName
                    ProjectId = Some(this.Id.ToFSharpProjectIdString())
                    SourceFiles = projectSite.CompilationSourceFiles
                    OtherOptions = otherOptions
                    ReferencedProjects = referencedProjects.ToArray()
                    IsIncompleteTypeCheckEnvironment = projectSite.IsIncompleteTypeCheckEnvironment
                    UseScriptResolutionRules = CompilerEnvironment.MustBeSingleFileProject (Path.GetFileName(this.FilePath))
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
                let currentSolution = this.Solution.Workspace.CurrentSolution

                FSharpProjectCaches.lastSuccessfulCompilations.ToArray()
                |> Array.iter (fun pair ->
                    if not (currentSolution.ContainsProject(pair.Key)) then
                        FSharpProjectCaches.lastSuccessfulCompilations.TryRemove(pair.Key) |> ignore
                )

                return Some(projectOptions)
        }

    member private this.CreateFSharpProjectTask() =
        let computation =
            async {
                if not this.IsFSharp then
                    raise(System.OperationCanceledException("Roslyn Project is not FSharp."))


                match! this.TryCreateFSharpProjectOptionsAsync() with
                | None ->
                    return raise(System.OperationCanceledException("FSharp project options not found."))
                | Some projectOptions ->

                let! ct = Async.CancellationToken

                let oldFsProjectOpt, changedDocs = 
                    match FSharpProjectCaches.CurrentProjects.TryGetValue this.Id with
                    | true, (oldProj, oldFsProject) ->
                        let isNotAbleToDiffSnapshot = isProjectInvalidated oldProj this ct
                        if isNotAbleToDiffSnapshot then
                            None, Seq.empty
                        else
                            Some oldFsProject, this.GetChanges(oldProj).GetChangedDocuments()
                    | _ ->
                        None, Seq.empty

                match oldFsProjectOpt with
                | Some oldFsProject ->
                    // Able to make a diff snapshot, only on updated documents.
                    let files =
                        changedDocs
                        |> Seq.map (fun x ->
                            let doc = this.GetDocument(x)
                            let getSourceText =
                                (fun () -> doc.GetTextAsync().Result.ToFSharpSourceText())
                            let isOpen = this.Solution.Workspace.IsDocumentOpen(x)
                            (doc.FilePath, getSourceText, isOpen)
                        )
                        |> Array.ofSeq
                    let fsProject = oldFsProject.UpdateFiles(files)
                    FSharpProjectCaches.CurrentProjects.[this.Id] <- (this, fsProject)
                    return fsProject
                | _ ->
                    // Unable to make a diff snapshot for the project; re-create it.
                    let tryGetMetadataSnapshot = this.Solution.Workspace.GetFSharpTryGetMetadataSnapshotFunction()

                    let! result =
                        FSharpProject.CreateAsync(projectOptions, 
                            legacyReferenceResolver = legacyReferenceResolver,
                            tryGetMetadataSnapshot = tryGetMetadataSnapshot)

                    match result with
                    | Ok fsProject ->
                        FSharpProjectCaches.CurrentProjects.[this.Id] <- (this, fsProject)
                        return fsProject
                    | Error(diags) ->
                        return raise(System.OperationCanceledException($"Unable to create a FSharpProject:\n%A{ diags }"))

            }
        let tcs = TaskCompletionSource<_>()
        Async.StartWithContinuations(
            computation,
            (fun res ->
                tcs.SetResult(res)
            ),
            (fun ex ->
                tcs.SetException(ex)
            ),
            (fun _ ->
                tcs.SetCanceled()
            ),
            CancellationToken.None
        )
        tcs.Task

    member private this.GetOrCreateFSharpProjectAsync() : Async<FSharpProject> =
        async {
            let! ct = Async.CancellationToken
            let create = 
                FSharpProjectCaches.Projects.GetValue(
                    this, 
                    Runtime.CompilerServices.ConditionalWeakTable<_,_>.CreateValueCallback(fun _ ->
                        AsyncLazy((fun () -> this.CreateFSharpProjectTask()))
                    )
                )
            return! create.GetValueAsync(ct) |> Async.AwaitTask
        }

type Document with

    member private this.CreateFSharpScriptOrSingleFileProjectTask() =
        let computation =
            async {
                let workspaceService = this.Project.Solution.GetFSharpWorkspaceService()

                let! ct = Async.CancellationToken
                let! fileStamp = this.GetTextVersionAsync(ct) |> Async.AwaitTask

                let oldFsProjectOpt, changedDocs = 
                    match FSharpProjectCaches.CurrentScriptOrSingleFiles.TryGetValue this.Id with
                    | true, (oldProj, oldFsProject) ->
                        let isNotAbleToDiffSnapshot = isProjectInvalidated oldProj this.Project ct
                        if isNotAbleToDiffSnapshot then
                            None, Seq.empty
                        else
                            let _v1 = this.Project.GetDependentVersionAsync().Result
                            let _v2 = oldProj.GetDependentVersionAsync().Result
                            Some oldFsProject, this.Project.GetChanges(oldProj).GetChangedDocuments()
                    | _ ->
                        None, Seq.empty

                match oldFsProjectOpt with
                | Some oldFsProject ->
                    // Able to make a diff snapshot, only on updated documents.
                    let files =
                        changedDocs
                        |> Seq.map (fun x ->
                            let doc = this.Project.GetDocument(x)
                            let getSourceText =
                                (fun () -> doc.GetTextAsync().Result.ToFSharpSourceText())
                            let isOpen = this.Project.Solution.Workspace.IsDocumentOpen(x)
                            (doc.FilePath, getSourceText, isOpen)
                        )
                        |> Array.ofSeq
                    let fsProject = oldFsProject.UpdateFiles(files)
                    FSharpProjectCaches.CurrentScriptOrSingleFiles.[this.Id] <- (this.Project, fsProject)
                    return fsProject
                | _ ->
                    let! sourceText = this.GetTextAsync(ct) |> Async.AwaitTask

                    let tryGetMetadataSnapshot = this.Project.Solution.Workspace.GetFSharpTryGetMetadataSnapshotFunction()

                    let! scriptProjectOptions, _ =
                        FSharpProject.GetProjectOptionsFromScript(this.FilePath,
                            sourceText.ToFSharpSourceText(),
                            SessionsProperties.fsiPreview,
                            assumeDotNetFramework=not SessionsProperties.fsiUseNetCore,
                            legacyReferenceResolver = legacyReferenceResolver,
                            tryGetMetadataSnapshot = tryGetMetadataSnapshot)

                    let project = this.Project

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
                        if isScriptFile this.FilePath then
                            workspaceService.ScriptUpdatedEvent.Trigger(scriptProjectOptions)
                            scriptProjectOptions
                        else
                            {
                                ProjectFileName = this.FilePath
                                ProjectId = None
                                SourceFiles = [|this.FilePath|]
                                OtherOptions = otherOptions
                                ReferencedProjects = [||]
                                IsIncompleteTypeCheckEnvironment = false
                                UseScriptResolutionRules = CompilerEnvironment.MustBeSingleFileProject (Path.GetFileName(this.FilePath))
                                LoadTime = DateTime.Now
                                UnresolvedReferences = None
                                OriginalLoadReferences = []
                                Stamp = Some(int64 (fileStamp.GetHashCode()))
                            }

                    let! result =
                        FSharpProject.CreateAsync(projectOptions, 
                            legacyReferenceResolver = legacyReferenceResolver,
                            tryGetMetadataSnapshot = tryGetMetadataSnapshot)

                    match result with
                    | Ok fsProject ->
                        FSharpProjectCaches.CurrentScriptOrSingleFiles.[this.Id] <- (this.Project, fsProject)
                        return fsProject
                    | Error(diags) ->
                        return raise(System.OperationCanceledException($"Unable to create a FSharpProject:\n%A{ diags }"))
            }
        let tcs = TaskCompletionSource<_>()
        Async.StartWithContinuations(
            computation,
            (fun res ->
                tcs.SetResult(res)
            ),
            (fun ex ->
                tcs.SetException(ex)
            ),
            (fun _ ->
                tcs.SetCanceled()
            ),
            CancellationToken.None
        )
        tcs.Task

    member private this.GetOrCreateFSharpScriptOrSingleFileProjectAsync() =
        async {
            let! ct = Async.CancellationToken
            let create = 
                FSharpProjectCaches.ScriptOrSingleFiles.GetValue(
                    this, 
                    Runtime.CompilerServices.ConditionalWeakTable<_,_>.CreateValueCallback(fun _ ->
                        AsyncLazy((fun () -> this.CreateFSharpScriptOrSingleFileProjectTask()))
                    )
                )
            return! create.GetValueAsync(ct) |> Async.AwaitTask
        }

    /// Get the FSharpParsingOptions and FSharpProjectOptions from the F# project that is associated with the given F# document.
    member this.GetFSharpProjectAsync(_userOpName: string) : Async<FSharpProject> =
        async {
            if this.Project.IsFSharp then
                // For now, disallow miscellaneous workspace since we are using the hacky F# miscellaneous files project.
                if this.Project.Solution.Workspace.Kind = WorkspaceKind.MiscellaneousFiles then
                    raise(System.OperationCanceledException("Roslyn Document is FSharp but in the Roslyn MiscellaneousFilesWorkspace."))
                
                if this.Project.IsFSharpMiscellaneousOrMetadata then
                    return! this.GetOrCreateFSharpScriptOrSingleFileProjectAsync()
                else
                    return! this.Project.GetOrCreateFSharpProjectAsync()
            else
                return raise(System.OperationCanceledException("Roslyn Document is not FSharp."))
        }

    /// Get the compilation defines from F# project that is associated with the given F# document.
    member this.GetFSharpCompilationDefinesAsync(userOpName) =
        async {
            let! fsProject = this.GetFSharpProjectAsync(userOpName)
            return CompilerEnvironment.GetCompilationDefinesForEditing fsProject.ParsingOptions
        }

    /// A non-async call that quickly gets FSharpParsingOptions of the given F# document.
    /// This tries to get the FSharpParsingOptions by looking at an internal cache; if it doesn't exist in the cache it will create an inaccurate but usable form of the FSharpParsingOptions.
    member this.GetFSharpQuickParsingOptions() =
        match FSharpProjectCaches.Projects.TryGetValue this.Project with
        | true, fsProject when fsProject.IsValueCreated -> fsProject.GetValue().ParsingOptions
        | _ -> { FSharpParsingOptions.Default with IsInteractive = CompilerEnvironment.IsScriptFile this.Name }

    /// A non-async call that quickly gets the defines of the given F# document.
    /// This tries to get the defines by looking at an internal cache; if it doesn't exist in the cache it will create an inaccurate but usable form of the defines.
    member this.GetFSharpQuickDefines() =
        let parsingOptions = this.GetFSharpQuickParsingOptions()
        CompilerEnvironment.GetCompilationDefinesForEditing parsingOptions
    
    /// Parses the given F# document.
    member this.GetFSharpParseResultsAsync(userOpName) =
        async {
            let! fsProject = this.GetFSharpProjectAsync(userOpName)
            return fsProject.GetParseFileResults(this.FilePath)
        }

    /// Parses and checks the given F# document.
    member this.GetFSharpParseAndCheckResultsAsync(userOpName) =
        async {
            let! fsProject = this.GetFSharpProjectAsync(userOpName)
            return! fsProject.GetParseAndCheckFileResultsAsync(this.FilePath)
        }

    /// Get the semantic classifications of the given F# document.
    member this.GetFSharpSemanticClassificationAsync(userOpName) =
        async {
            let! fsProject = this.GetFSharpProjectAsync(userOpName)
            match! fsProject.TryGetSemanticClassificationForFileAsync(this.FilePath) with
            | Some results -> return results
            | _ -> return raise(System.OperationCanceledException("Unable to get FSharp semantic classification."))
        }

    /// Find F# references in the given F# document.
    member this.FindFSharpReferencesAsync(symbol, onFound, userOpName) =
        async {
            let! fsProject = this.GetFSharpProjectAsync(userOpName)
            let! symbolUses = fsProject.FindReferencesInFileAsync(this.FilePath, symbol)
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
        let result = FSharpProject.CreateAsync(projectOptions) |> Async.RunSynchronously
        match result with
        | Ok fsProject ->
            FSharpProjectCaches.Projects.Add(this.Project, AsyncLazy(fun () -> Task.FromResult(fsProject)))
        | Error diags ->
            failwithf $"Unable to create a FSharpProject for testing: %A{ diags }"

type Project with

    /// Find F# references in the given project.
    member this.FindFSharpReferencesAsync(symbol, onFound, userOpName) =
        async {
            for doc in this.Documents do
                do! doc.FindFSharpReferencesAsync(symbol, (fun textSpan range -> onFound doc textSpan range), userOpName)
        }
