// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Salsa

open System
open System.IO
open System.Collections.Generic
open System.Configuration
open System.Globalization
open System.Runtime.InteropServices
open Microsoft.VisualStudio
open Microsoft.VisualStudio.Shell
open Microsoft.VisualStudio.Shell.Interop 
open Microsoft.VisualStudio.TextManager.Interop 
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.OLE.Interop
open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Tokenization
open Microsoft.VisualStudio.FSharp.LanguageService
open Microsoft.VisualStudio.FSharp.LanguageService.SiteProvider

type internal FSharpLanguageServiceTestable() as this =
    static let colorizerGuid = new Guid("{A2976312-7D71-4BB4-A5F8-66A08EBF46C8}") // Guid for colorized user data on IVsTextBuffer
    let mutable checkerContainerOpt : FSharpChecker option = None
    let mutable artifacts : ProjectSitesAndFiles option = None
    let mutable serviceProvider : System.IServiceProvider option = None
    let mutable preferences : LanguagePreferences option = None
    let mutable documentationBuilder : Microsoft.VisualStudio.FSharp.LanguageService.IDocumentationBuilder_DEPRECATED option = None
    let mutable sourceFactory : (IVsTextLines -> IFSharpSource_DEPRECATED) option = None
    let mutable dirtyForTypeCheckFiles : Set<string> = Set.empty
    let mutable isInitialized = false
    let mutable unhooked = false
    let getColorizer (view:IVsTextView) = 
        let buffer = Com.ThrowOnFailure1(view.GetBuffer())
        this.GetColorizer(buffer)                         

    let bgRequests = new FSharpLanguageServiceBackgroundRequests_DEPRECATED(getColorizer,(fun () -> this.FSharpChecker),(fun () -> this.ProjectSitesAndFiles),(fun () -> this.ServiceProvider),(fun () -> this.DocumentationBuilder))
    
    member this.FSharpChecker = 
        if this.Unhooked then raise Error.UseOfUnhookedLanguageServiceState
        if not this.IsInitialized then raise Error.UseOfUninitializedLanguageServiceState
        checkerContainerOpt.Value

    member this.ServiceProvider = 
        if this.Unhooked then raise Error.UseOfUnhookedLanguageServiceState
        if not this.IsInitialized then raise Error.UseOfUninitializedLanguageServiceState
        serviceProvider.Value 

    member this.ProjectSitesAndFiles = 
        if not this.IsInitialized then raise Error.UseOfUninitializedLanguageServiceState
        artifacts.Value
        
    member this.Preferences = 
        if this.Unhooked then raise Error.UseOfUnhookedLanguageServiceState
        if not this.IsInitialized then raise Error.UseOfUninitializedLanguageServiceState
        preferences.Value
        
    member this.SourceFactory = 
        if this.Unhooked then raise Error.UseOfUnhookedLanguageServiceState
        if not this.IsInitialized then raise Error.UseOfUninitializedLanguageServiceState
        sourceFactory.Value
        
    member this.IsInitialized = isInitialized
    member this.Unhooked = unhooked
    member this.DocumentationBuilder = documentationBuilder.Value
    
    /// Handle late intialization pieces
    member this.Initialize (sp, dp, prefs, sourceFact) = 
        if this.Unhooked then raise Error.UseOfUnhookedLanguageServiceState        
        artifacts <- Some (ProjectSitesAndFiles())
        let checker = FSharpChecker.Create(legacyReferenceResolver=LegacyMSBuildReferenceResolver.getResolver())
        checker.BeforeBackgroundFileCheck.Add (fun (filename,_) -> UIThread.Run(fun () -> this.NotifyFileTypeCheckStateIsDirty(filename)))
        checkerContainerOpt <- Some (checker)
        serviceProvider <- Some sp
        isInitialized <- true
        unhooked <- false
        documentationBuilder <- Some dp
        preferences <- Some prefs
        sourceFactory <- Some sourceFact

    
    member this.NotifyFileTypeCheckStateIsDirty(filename) = 
        dirtyForTypeCheckFiles <- dirtyForTypeCheckFiles.Add filename

    /// Clear all language service caches and finalize all transient references to compiler objects
    member this.ClearLanguageServiceRootCachesAndCollectAndFinalizeAllTransients() = 
        if this.Unhooked then raise Error.UseOfUnhookedLanguageServiceState
        if this.IsInitialized then
            this.FSharpChecker.ClearLanguageServiceRootCachesAndCollectAndFinalizeAllTransients()

    /// Unhook the object. These are the held resources that need to be disposed.
    member this.Unhook() =
        if this.Unhooked then raise Error.UseOfUnhookedLanguageServiceState
        if this.IsInitialized then
            // Dispose the preferences.
            if this.Preferences <> null then this.Preferences.Dispose()
            // Stop the background compile.
            // here we refer to checkerContainerOpt directly to avoid triggering its creation
            match checkerContainerOpt with
            | Some container -> 
                let checker = container
                checker.ClearLanguageServiceRootCachesAndCollectAndFinalizeAllTransients()
            | None -> ()
            
            checkerContainerOpt <- None 
            artifacts <- None
            preferences <- None
            documentationBuilder <- None
            unhooked <- true
            sourceFactory <- None
            serviceProvider <- None
    
    /// Respond to project settings changes
    member this.OnProjectSettingsChanged(site:IProjectSite) = 
        // The project may have changed its references.  These would be represented as 'dependency files' of each source file.  Each source file will eventually start listening
        // for changes to those dependencies, at which point we'll get OnDependencyFileCreateOrDelete notifications.  Until then, though, we just 'make a note' that this project is out of date.
        bgRequests.AddOutOfDateProjectFileName(site.ProjectFileName) 
        for filename in site.CompilationSourceFiles do
            let rdt = this.ServiceProvider.RunningDocumentTable
            match this.ProjectSitesAndFiles.TryGetSourceOfFile_DEPRECATED(rdt,filename) with
            | Some source -> 
                source.RecolorizeWholeFile()
                source.RecordChangeToView()
            | None -> ()

    /// Respond to project being cleaned/rebuilt (any live type providers in the project should be refreshed)
    member this.OnProjectCleaned(projectSite:IProjectSite) = 
        let enableInMemoryCrossProjectReferences = true
        let _, checkOptions = ProjectSitesAndFiles.GetProjectOptionsForProjectSite(enableInMemoryCrossProjectReferences, (fun _ -> None), projectSite, serviceProvider.Value, "" , false)
        this.FSharpChecker.NotifyProjectCleaned(checkOptions) |> Async.RunImmediate

    member this.OnActiveViewChanged(textView) =
        bgRequests.OnActiveViewChanged(textView)

    member this.BackgroundRequests = bgRequests
    
    /// Unittestable complement to LanguageServce.CreateSource_DEPRECATED
    member this.CreateSource_DEPRECATED(buffer:IVsTextLines) : IFSharpSource_DEPRECATED =
    
        // Each time a source is created, also verify that the IProjectSite has been initialized to listen to changes to the project.
        // We can't listen to OnProjectLoaded because the language service is not guaranteed to be loaded when this is called.
        let filename = VsTextLines.GetFilename buffer
        let rdt = this.ServiceProvider.RunningDocumentTable
        let result = VsRunningDocumentTable.FindDocumentWithoutLocking(rdt,filename)
        match result with 
        | Some(hier,_) -> 
            match hier with 
            | :? IProvideProjectSite as siteProvider ->
                let site = siteProvider.GetProjectSite()
                site.AdviseProjectSiteChanges(LanguageServiceConstants.FSharpLanguageServiceCallbackName, 
                                              new AdviseProjectSiteChanges(fun () -> this.OnProjectSettingsChanged(site))) 
                site.AdviseProjectSiteCleaned(LanguageServiceConstants.FSharpLanguageServiceCallbackName, 
                                              new AdviseProjectSiteChanges(fun () -> this.OnProjectCleaned(site))) 
            | _ -> 
                // This can happen when the file is in a solution folder or in, say, a C# project.
                ()
        | _ ->
            // This can happen when renaming a file from a different language service into .fs or fsx.
            // This naturally won't have an associated project.
            ()
        
        // Create the source and register file change callbacks there.       
        let source = this.SourceFactory(buffer)
        this.ProjectSitesAndFiles.SetSource_DEPRECATED(buffer, source)
        source
    
    // For each change in dependency files, notify the language service of the change and propagate the update
    interface IDependencyFileChangeNotify_DEPRECATED with
        member this.DependencyFileCreated projectSite = 
            let enableInMemoryCrossProjectReferences = true
            // Invalidate the configuration if we notice any add for any DependencyFiles 
            let _, checkOptions = ProjectSitesAndFiles.GetProjectOptionsForProjectSite(enableInMemoryCrossProjectReferences, (fun _ -> None), projectSite, serviceProvider.Value, "" , false)
            this.FSharpChecker.InvalidateConfiguration(checkOptions)

        member this.DependencyFileChanged (filename) = 
            this.NotifyFileTypeCheckStateIsDirty filename


    /// Do OnIdle processing for the whole language service. dirtyForTypeCheckFiles can be set by events 
    /// raised on the background compilation thread.  
    member this.OnIdle() = 
        for file in dirtyForTypeCheckFiles do
            let rdt = this.ServiceProvider.RunningDocumentTable
            match this.ProjectSitesAndFiles.TryGetSourceOfFile_DEPRECATED(rdt, file) with
            | Some source -> source.RecordChangeToView()
            | None ->  ()
        dirtyForTypeCheckFiles <- Set.empty
        

    /// Remove a colorizer.
    member this.CloseColorizer(colorizer:FSharpColorizer_DEPRECATED) = 
        let buffer = colorizer.Buffer
        let mutable guid = colorizerGuid
        (buffer :?> IVsUserData).SetData(&guid, null) |> ErrorHandler.ThrowOnFailure |> ignore

    /// Get a colorizer for a particular buffer.
    member this.GetColorizer(buffer:IVsTextLines) : FSharpColorizer_DEPRECATED = 
        let mutable guid = colorizerGuid
        let mutable colorizerObj = null
        
        (buffer :?> IVsUserData).GetData(&guid, &colorizerObj) |> ignore
        match colorizerObj with
        | null ->
            let scanner = 
                new FSharpScanner_DEPRECATED(fun source ->
                    // Note: in theory, the next few lines do not need to be recomputed every line.  Instead we could just cache the tokenizer
                    // and only update it when e.g. the project system notifies us there is an important change (e.g. a file rename, etc).
                    // In practice we have been there, and always screwed up some non-unit-tested/testable corner-cases.
                    // So this is not ideal from a perf perspective, but it is easy to reason about the correctness.
                    let filename = VsTextLines.GetFilename buffer
                    let rdt = this.ServiceProvider.RunningDocumentTable
                    let defines = this.ProjectSitesAndFiles.GetDefinesForFile_DEPRECATED(rdt, filename, this.FSharpChecker)
                    let sourceTokenizer = FSharpSourceTokenizer(defines,Some(filename))
                    sourceTokenizer.CreateLineTokenizer(source))

            let colorizer = new FSharpColorizer_DEPRECATED(this.CloseColorizer, buffer, scanner) 
            (buffer :?> IVsUserData).SetData(&guid, colorizer) |> ErrorHandler.ThrowOnFailure |> ignore
            colorizer
        | _ -> colorizerObj :?> FSharpColorizer_DEPRECATED
    
    /// Block until the background compile finishes.
    //
    // This is for unit testing only
    member this.WaitForBackgroundCompile() =
        ()
