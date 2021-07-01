// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

#nowarn "40"

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Linq
open System.Runtime.CompilerServices
open Microsoft.CodeAnalysis
open Microsoft.VisualStudio
open Microsoft.VisualStudio.FSharp.Editor
open Microsoft.VisualStudio.LanguageServices.Implementation.ProjectSystem
open Microsoft.VisualStudio.LanguageServices.ProjectSystem
open Microsoft.VisualStudio.Shell.Interop

[<Sealed>]
type internal LegacyProjectWorkspaceMap(solution: IVsSolution, 
                                        projectInfoManager: FSharpProjectOptionsManager,
                                        projectContextFactory: IWorkspaceProjectContextFactory) as this =

    let invalidPathChars = set (Path.GetInvalidPathChars())
    let optionsAssociation = ConditionalWeakTable<IWorkspaceProjectContext, string[]>()
    let isPathWellFormed (path: string) = not (String.IsNullOrWhiteSpace path) && path |> Seq.forall (fun c -> not (Set.contains c invalidPathChars))

    let projectDisplayNameOf projectFileName =
        if String.IsNullOrWhiteSpace projectFileName then projectFileName
        else Path.GetFileNameWithoutExtension projectFileName
   
    let legacyProjectIdLookup = ConcurrentDictionary()
    let legacyProjectLookup = ConcurrentDictionary()
    let setupQueue = ConcurrentQueue()

    do
        solution.AdviseSolutionEvents(this) |> ignore

    /// Sync the Roslyn information for the project held in 'projectContext' to match the information given by 'site'.
    /// Also sync the info in ProjectInfoManager if necessary.
    member this.SyncLegacyProject(projectContext: IWorkspaceProjectContext, site: IProjectSite) =
        let wellFormedFilePathSetIgnoreCase (paths: seq<string>) =
            HashSet(paths |> Seq.filter isPathWellFormed |> Seq.map (fun s -> try Path.GetFullPath(s) with _ -> s), StringComparer.OrdinalIgnoreCase)

        let projectId = projectContext.Id

        projectInfoManager.SetLegacyProjectSite (projectId, site)

        // Sync the source files in projectContext.  Note that these source files are __not__ maintained in order in projectContext
        // as edits are made. It seems this is ok because the source file list is only used to drive roslyn per-file checking.
        let updatedFiles = site.CompilationSourceFiles |> wellFormedFilePathSetIgnoreCase
        let originalFiles =  
            match legacyProjectLookup.TryGetValue(projectId) with
            | true, (originalFiles, _) -> originalFiles
            | _ -> HashSet()
        
        for file in updatedFiles do
            if not(originalFiles.Contains(file)) then
                projectContext.AddSourceFile(file)

        for file in originalFiles do
            if not(updatedFiles.Contains(file)) then
                projectContext.RemoveSourceFile(file)

        let updatedRefs = site.CompilationReferences |> wellFormedFilePathSetIgnoreCase
        let originalRefs =
            match legacyProjectLookup.TryGetValue(projectId) with
            | true, (_, originalRefs) -> originalRefs
            | _ -> HashSet()

        for ref in updatedRefs do
            if not(originalRefs.Contains(ref)) then
                projectContext.AddMetadataReference(ref, MetadataReferenceProperties.Assembly)

        for ref in originalRefs do
            if not(updatedRefs.Contains(ref)) then
                projectContext.RemoveMetadataReference(ref)

        // Update the project options association
        let ok,originalOptions = optionsAssociation.TryGetValue(projectContext)
        let updatedOptions = site.CompilationOptions
        if not ok || originalOptions <> updatedOptions then 

            // OK, project options have changed, try to fake out Roslyn to convince it to reparse things.
            // Calling SetOptions fails because the CPS project system being used by the F# project system 
            // imlpementation at the moment has no command line parser installed, so we remove/add all the files 
            // instead.  A change of flags doesn't happen very often and the remove/add is fast in any case.
            //projectContext.SetOptions(String.concat " " updatedOptions)
            for file in updatedFiles do
                projectContext.RemoveSourceFile(file)
                projectContext.AddSourceFile(file)

            // Record the last seen options as an associated value
            if ok then optionsAssociation.Remove(projectContext) |> ignore
            optionsAssociation.Add(projectContext, updatedOptions)

            projectContext.BinOutputPath <- Option.toObj site.CompilationBinOutputPath

        let info = (updatedFiles, updatedRefs)
        legacyProjectLookup.AddOrUpdate(projectId, info, fun _ _ -> info) |> ignore

    member this.SetupLegacyProjectFile(siteProvider: IProvideProjectSite) =
        let rec setup (site: IProjectSite) =
            let projectGuid = Guid(site.ProjectGuid)
            let projectFileName = site.ProjectFileName
            let projectDisplayName = projectDisplayNameOf projectFileName

            let hierarchy =
                site.ProjectProvider
                |> Option.map (fun p -> p :?> IVsHierarchy)
                |> Option.toObj

            // Roslyn is expecting site to be an IVsHierarchy.
            // It just so happens that the object that implements IProvideProjectSite is also
            // an IVsHierarchy. This assertion is to ensure that the assumption holds true.
            Debug.Assert(not (isNull (box hierarchy)), "About to CreateProjectContext with a non-hierarchy site")

            let projectContext = 
                projectContextFactory.CreateProjectContext(
                    FSharpConstants.FSharpLanguageName,
                    projectDisplayName,
                    projectFileName,
                    projectGuid,
                    hierarchy,
                    Option.toObj site.CompilationBinOutputPath)

            legacyProjectIdLookup.[projectGuid] <- projectContext.Id

            // Sync IProjectSite --> projectContext, and IProjectSite --> ProjectInfoManage
            this.SyncLegacyProject(projectContext, site)

            site.BuildErrorReporter <- Some (projectContext :?> Microsoft.VisualStudio.Shell.Interop.IVsLanguageServiceBuildErrorReporter2)

            // TODO: consider forceUpdate = false here.  forceUpdate=true may be causing repeated computation?
            site.AdviseProjectSiteChanges(FSharpConstants.FSharpLanguageServiceCallbackName, 
                                            AdviseProjectSiteChanges(fun () -> this.SyncLegacyProject(projectContext, site)))

            site.AdviseProjectSiteClosed(FSharpConstants.FSharpLanguageServiceCallbackName, 
                                            AdviseProjectSiteChanges(fun () -> 
                                            projectInfoManager.ClearInfoForProject(projectContext.Id)
                                            optionsAssociation.Remove(projectContext) |> ignore
                                            projectContext.Dispose()))

        setup (siteProvider.GetProjectSite()) 

    interface IVsSolutionEvents with

        member _.OnAfterCloseSolution(_) = 
            // Clear
            let mutable setup = Unchecked.defaultof<_>
            while setupQueue.TryDequeue(&setup) do ()
            VSConstants.S_OK

        member _.OnAfterLoadProject(_, _) = VSConstants.S_OK

        member _.OnAfterOpenProject(hier, _) = 
            match hier with
            | :? IProvideProjectSite as siteProvider ->
                let setup = fun () -> this.SetupLegacyProjectFile(siteProvider)
                let _, o = solution.GetProperty(int __VSPROPID.VSPROPID_IsSolutionOpen)
                if (match o with | :? bool as isOpen -> isOpen | _ -> false) then
                    setup ()
                else
                    setupQueue.Enqueue(setup)
            | _ -> ()
            VSConstants.S_OK

        member _.OnAfterOpenSolution(_, _) =
            let mutable setup = Unchecked.defaultof<_>
            while setupQueue.TryDequeue(&setup) do
                setup ()
            VSConstants.S_OK

        member _.OnBeforeCloseProject(hier, _) = 
            match hier with
            | :? IProvideProjectSite as siteProvider ->
                let site = siteProvider.GetProjectSite()
                let projectGuid = Guid(site.ProjectGuid)
                match legacyProjectIdLookup.TryGetValue(projectGuid) with
                | true, projectId ->
                    legacyProjectIdLookup.TryRemove(projectGuid) |> ignore
                    legacyProjectLookup.TryRemove(projectId) |> ignore
                | _ -> ()
            | _ -> ()
            VSConstants.S_OK

        member _.OnBeforeCloseSolution(_) = VSConstants.S_OK

        member _.OnBeforeUnloadProject(_, _) = VSConstants.S_OK

        member _.OnQueryCloseProject(_, _, _) = VSConstants.S_OK

        member _.OnQueryCloseSolution(_, _) = VSConstants.S_OK

        member _.OnQueryUnloadProject(_, _) = VSConstants.S_OK