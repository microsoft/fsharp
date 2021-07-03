// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace rec Microsoft.VisualStudio.FSharp.Editor

open System
open System.ComponentModel.Design
open System.Runtime.InteropServices
open System.Threading
open System.IO
open System.Collections.Concurrent
open System.Collections.Immutable
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Options
open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis
open FSharp.NativeInterop
open Microsoft.VisualStudio
open Microsoft.VisualStudio.Editor
open Microsoft.VisualStudio.FSharp.Editor
open Microsoft.VisualStudio.LanguageServices
open Microsoft.VisualStudio.LanguageServices.Implementation.LanguageService
open Microsoft.VisualStudio.LanguageServices.Implementation.ProjectSystem
open Microsoft.VisualStudio.LanguageServices.ProjectSystem
open Microsoft.VisualStudio.Shell
open Microsoft.VisualStudio.Shell.Interop
open Microsoft.VisualStudio.Text.Outlining
open Microsoft.CodeAnalysis.ExternalAccess.FSharp
open Microsoft.CodeAnalysis.Host
open Microsoft.CodeAnalysis.Host.Mef
open System.Runtime.CompilerServices
open Microsoft.VisualStudio.Threading

#nowarn "9" // NativePtr.toNativeInt

[<RequireQualifiedAccess>]
module FSharpProjectCaches =

    // This is used to not constantly emit the same compilation.
    let weakPEReferences = ConditionalWeakTable<Compilation, FSharpReferencedProject>()
    let lastSuccessfulCompilations = ConcurrentDictionary<ProjectId, Compilation>()

    /// This is a cache to maintain a FSharpProject per Roslyn Project.
    /// The Roslyn Project is held weakly meaning when it is cleaned up by the GC, the FSharpProject will be cleaned up by the GC.
    let Projects = ConditionalWeakTable<Project, AsyncLazy<FSharpProject>>()
    let CurrentProjects = ConcurrentDictionary<ProjectId, Project * FSharpProject>()

    /// This is a cache to maintain a script or misc file as a FSharpProject per Roslyn Document.
    /// The Roslyn Document is held weakly meaning when it is cleaned up by the GC, the FSharpProject will be cleaned up by the GC.
    let ScriptOrSingleFiles = ConditionalWeakTable<Document, AsyncLazy<FSharpProject>>()
    let CurrentScriptOrSingleFiles = ConcurrentDictionary<DocumentId, Project * FSharpProject>()

    let Clear() =
        lastSuccessfulCompilations.Clear()
        CurrentProjects.Clear()
        CurrentScriptOrSingleFiles.Clear()

type internal RoamingProfileStorageLocation(keyName: string) =
    inherit OptionStorageLocation()
    
    member _.GetKeyNameForLanguage(languageName: string) =
        let unsubstitutedKeyName = keyName
        match languageName with
        | null -> unsubstitutedKeyName
        | _ ->
            let substituteLanguageName = if languageName = FSharpConstants.FSharpLanguageName then "FSharp" else languageName
            unsubstitutedKeyName.Replace("%LANGUAGE%", substituteLanguageName)
 
[<System.Composition.Shared>]
[<ExportWorkspaceServiceFactory(typeof<IFSharpWorkspaceService>, ServiceLayer.Default)>]
type internal FSharpWorkspaceServiceFactory
    [<System.Composition.ImportingConstructor>]
    (
    ) =

    interface IWorkspaceServiceFactory with
        member _.CreateService(_workspaceServices) =

            // Store command line options
            let commandLineOptions = ConcurrentDictionary<ProjectId, string[] * string[]>()

            let legacyProjectSites = ConcurrentDictionary<ProjectId, IProjectSite>()

            let scriptUpdatedEvent = Event<FSharpProjectOptions>()

            { new IFSharpWorkspaceService with
                member _.CommandLineOptions = commandLineOptions
                member _.LegacyProjectSites = legacyProjectSites
                member _.ScriptUpdatedEvent = scriptUpdatedEvent } :> _

[<Sealed>]
type private FSharpSolutionEvents(workspaceService: IFSharpWorkspaceService, metadataAsSource: FSharpMetadataAsSourceService) =

    interface IVsSolutionEvents with

        member _.OnAfterCloseSolution(_) =
            metadataAsSource.ClearGeneratedFiles()
            workspaceService.CommandLineOptions.Clear()
            workspaceService.LegacyProjectSites.Clear()
            FSharpProjectCaches.Clear()
            VSConstants.S_OK

        member _.OnAfterLoadProject(_, _) = VSConstants.E_NOTIMPL

        member _.OnAfterOpenProject(_, _) = VSConstants.E_NOTIMPL

        member _.OnAfterOpenSolution(_, _) = VSConstants.E_NOTIMPL

        member _.OnBeforeCloseProject(_, _) = VSConstants.E_NOTIMPL

        member _.OnBeforeCloseSolution(_) = VSConstants.E_NOTIMPL

        member _.OnBeforeUnloadProject(_, _) = VSConstants.E_NOTIMPL

        member _.OnQueryCloseProject(_, _, _) = VSConstants.E_NOTIMPL

        member _.OnQueryCloseSolution(_, _) = VSConstants.E_NOTIMPL

        member _.OnQueryUnloadProject(_, _) = VSConstants.E_NOTIMPL       

[<Microsoft.CodeAnalysis.Host.Mef.ExportWorkspaceServiceFactory(typeof<EditorOptions>, Microsoft.CodeAnalysis.Host.Mef.ServiceLayer.Default)>]
type internal FSharpSettingsFactory
    [<Composition.ImportingConstructor>] (settings: EditorOptions) =
    interface Microsoft.CodeAnalysis.Host.Mef.IWorkspaceServiceFactory with
        member _.CreateService(_) = upcast settings

[<Guid(FSharpConstants.packageGuidString)>]
[<ProvideOptionPage(typeof<Microsoft.VisualStudio.FSharp.Interactive.FsiPropertyPage>,
                    "F# Tools", "F# Interactive",   // category/sub-category on Tools>Options...
                    6000s,      6001s,              // resource id for localisation of the above
                    true)>]                         // true = supports automation
[<ProvideKeyBindingTable("{dee22b65-9761-4a26-8fb2-759b971d6dfc}", 6001s)>] // <-- resource ID for localised name
[<ProvideToolWindow(typeof<Microsoft.VisualStudio.FSharp.Interactive.FsiToolWindow>, 
                    // The following should place the ToolWindow with the OutputWindow by default.
                    Orientation=ToolWindowOrientation.Bottom,
                    Style=VsDockStyle.Tabbed,
                    PositionX = 0,
                    PositionY = 0,
                    Width = 360,
                    Height = 120,
                    Window="34E76E81-EE4A-11D0-AE2E-00A0C90FFFC3")>]
[<ProvideLanguageEditorOptionPage(typeof<OptionsUI.IntelliSenseOptionPage>, "F#", null, "IntelliSense", "6008")>]
[<ProvideLanguageEditorOptionPage(typeof<OptionsUI.QuickInfoOptionPage>, "F#", null, "QuickInfo", "6009")>]
[<ProvideLanguageEditorOptionPage(typeof<OptionsUI.CodeFixesOptionPage>, "F#", null, "Code Fixes", "6010")>]
[<ProvideLanguageEditorOptionPage(typeof<OptionsUI.LanguageServicePerformanceOptionPage>, "F#", null, "Performance", "6011")>]
[<ProvideLanguageEditorOptionPage(typeof<OptionsUI.AdvancedSettingsOptionPage>, "F#", null, "Advanced", "6012")>]
[<ProvideLanguageEditorOptionPage(typeof<OptionsUI.CodeLensOptionPage>, "F#", null, "CodeLens", "6013")>]
[<ProvideLanguageEditorOptionPage(typeof<OptionsUI.FormattingOptionPage>, "F#", null, "Formatting", "6014")>]
[<ProvideFSharpVersionRegistration(FSharpConstants.projectPackageGuidString, "Microsoft Visual F#")>]
// 64 represents a hex number. It needs to be greater than 37 so the TextMate editor will not be chosen as higher priority.
[<ProvideEditorExtension(typeof<FSharpEditorFactory>, ".fs", 64)>]
[<ProvideEditorExtension(typeof<FSharpEditorFactory>, ".fsi", 64)>]
[<ProvideEditorExtension(typeof<FSharpEditorFactory>, ".fsscript", 64)>]
[<ProvideEditorExtension(typeof<FSharpEditorFactory>, ".fsx", 64)>]
[<ProvideEditorExtension(typeof<FSharpEditorFactory>, ".ml", 64)>]
[<ProvideEditorExtension(typeof<FSharpEditorFactory>, ".mli", 64)>]
[<ProvideEditorFactory(typeof<FSharpEditorFactory>, 101s, CommonPhysicalViewAttributes = Constants.FSharpEditorFactoryPhysicalViewAttributes)>]
[<ProvideLanguageExtension(typeof<FSharpLanguageService>, ".fs")>]
[<ProvideLanguageExtension(typeof<FSharpLanguageService>, ".fsi")>]
[<ProvideLanguageExtension(typeof<FSharpLanguageService>, ".fsx")>]
[<ProvideLanguageExtension(typeof<FSharpLanguageService>, ".fsscript")>]
[<ProvideLanguageExtension(typeof<FSharpLanguageService>, ".ml")>]
[<ProvideLanguageExtension(typeof<FSharpLanguageService>, ".mli")>]
[<ProvideBraceCompletion(FSharpConstants.FSharpLanguageName)>]
[<ProvideLanguageService(languageService = typeof<FSharpLanguageService>,
                            strLanguageName = FSharpConstants.FSharpLanguageName,
                            languageResourceID = 100,
                            MatchBraces = true,
                            MatchBracesAtCaret = true,
                            ShowCompletion = true,
                            ShowMatchingBrace = true,
                            ShowSmartIndent = true,
                            EnableAsyncCompletion = true,
                            QuickInfo = true,
                            DefaultToInsertSpaces = true,
                            CodeSense = true,
                            DefaultToNonHotURLs = true,
                            RequestStockColors = true,
                            EnableCommenting = true,
                            CodeSenseDelay = 100,
                            ShowDropDownOptions = true)>]
type internal FSharpPackage() as this =
    inherit AbstractPackage<FSharpPackage, FSharpLanguageService>()

    let mutable vfsiToolWindow = Unchecked.defaultof<Microsoft.VisualStudio.FSharp.Interactive.FsiToolWindow>
    let GetToolWindowAsITestVFSI() =
        if vfsiToolWindow = Unchecked.defaultof<_> then
            vfsiToolWindow <- this.FindToolWindow(typeof<Microsoft.VisualStudio.FSharp.Interactive.FsiToolWindow>, 0, true) :?> Microsoft.VisualStudio.FSharp.Interactive.FsiToolWindow
        vfsiToolWindow :> Microsoft.VisualStudio.FSharp.Interactive.ITestVFSI

    let mutable solutionEventsOpt = None

    // FSI-LINKAGE-POINT: unsited init
    do 
        Microsoft.VisualStudio.FSharp.Interactive.Hooks.fsiConsoleWindowPackageCtorUnsited (this :> Package)

    override this.InitializeAsync(cancellationToken: CancellationToken, progress: IProgress<ServiceProgressData>) : Tasks.Task =
        // `base.` methods can't be called in the `async` builder, so we have to cache it
        let baseInitializeAsync = base.InitializeAsync(cancellationToken, progress)
        let task =
            async {
                do! baseInitializeAsync |> Async.AwaitTask

                let! commandService = this.GetServiceAsync(typeof<IMenuCommandService>) |> Async.AwaitTask // FSI-LINKAGE-POINT
                let commandService = commandService :?> OleMenuCommandService
                let packageInit () =
                    // FSI-LINKAGE-POINT: sited init
                    Microsoft.VisualStudio.FSharp.Interactive.Hooks.fsiConsoleWindowPackageInitalizeSited (this :> Package) commandService

                    // FSI-LINKAGE-POINT: private method GetDialogPage forces fsi options to be loaded
                    let _fsiPropertyPage = this.GetDialogPage(typeof<Microsoft.VisualStudio.FSharp.Interactive.FsiPropertyPage>)

                    
                    let workspace = this.ComponentModel.GetService<VisualStudioWorkspace>()
                    let _ = this.ComponentModel.DefaultExportProvider.GetExport<HackCpsCommandLineChanges>()
                    let workspaceService = workspace.Services.GetService<IFSharpWorkspaceService>()
                    let metadataAsSource = this.ComponentModel.DefaultExportProvider.GetExport<FSharpMetadataAsSourceService>().Value
                    let solution = this.GetServiceAsync(typeof<SVsSolution>).Result
                    let solution = solution :?> IVsSolution
                    let solutionEvents = FSharpSolutionEvents(workspaceService, metadataAsSource)
                    let rdt = this.GetServiceAsync(typeof<SVsRunningDocumentTable>).Result
                    let rdt = rdt :?> IVsRunningDocumentTable

                    solutionEventsOpt <- Some(solutionEvents)
                    solution.AdviseSolutionEvents(solutionEvents) |> ignore
                    
                    let projectContextFactory = this.ComponentModel.GetService<IWorkspaceProjectContextFactory>()
                    let miscFilesWorkspace = this.ComponentModel.GetService<MiscellaneousFilesWorkspace>()
                    let _singleFileWorkspaceMap = 
                        new SingleFileWorkspaceMap(
                            FSharpMiscellaneousFileService(
                                workspace,
                                miscFilesWorkspace,
                                FSharpWorkspaceProjectContextFactory(projectContextFactory)
                            ),
                            rdt)
                    let _legacyProjectWorkspaceMap = new LegacyProjectWorkspaceMap(solution, workspaceService, projectContextFactory)
                    ()
                let awaiter = this.JoinableTaskFactory.SwitchToMainThreadAsync().GetAwaiter()
                if awaiter.IsCompleted then
                    packageInit() // already on the UI thread
                else
                    awaiter.OnCompleted(fun () -> packageInit())

            } |> Async.StartAsTask
        upcast task // convert Task<unit> to Task

    override this.RoslynLanguageName = FSharpConstants.FSharpLanguageName
    override this.CreateWorkspace() = this.ComponentModel.GetService<VisualStudioWorkspaceImpl>()
    override this.CreateLanguageService() = FSharpLanguageService(this)
    override this.CreateEditorFactories() = seq { yield FSharpEditorFactory(this) :> IVsEditorFactory }
    override this.RegisterMiscellaneousFilesWorkspaceInformation(miscFilesWorkspace) =
        miscFilesWorkspace.RegisterLanguage(Guid(FSharpConstants.languageServiceGuidString), FSharpConstants.FSharpLanguageName, ".fsx")

    interface Microsoft.VisualStudio.FSharp.Interactive.ITestVFSI with
        member this.SendTextInteraction(s:string) =
            GetToolWindowAsITestVFSI().SendTextInteraction(s)
        member this.GetMostRecentLines(n:int) : string[] =
            GetToolWindowAsITestVFSI().GetMostRecentLines(n)

[<Guid(FSharpConstants.languageServiceGuidString)>]
type internal FSharpLanguageService(package : FSharpPackage) =
    inherit AbstractLanguageService<FSharpPackage, FSharpLanguageService>(package)

    override this.Initialize() = 
        base.Initialize()

        this.Workspace.Options <- this.Workspace.Options.WithChangedOption(Completion.FSharpCompletionOptions.BlockForCompletionItems, FSharpConstants.FSharpLanguageName, false)
        this.Workspace.Options <- this.Workspace.Options.WithChangedOption(Shared.Options.FSharpServiceFeatureOnOffOptions.ClosedFileDiagnostic, FSharpConstants.FSharpLanguageName, Nullable false)

        let theme = package.ComponentModel.DefaultExportProvider.GetExport<ISetThemeColors>().Value
        theme.SetColors()

    override _.ContentTypeName = FSharpConstants.FSharpContentTypeName
    override _.LanguageName = FSharpConstants.FSharpLanguageName
    override _.RoslynLanguageName = FSharpConstants.FSharpLanguageName

    override _.LanguageServiceId = new Guid(FSharpConstants.languageServiceGuidString)
    override _.DebuggerLanguageId = CompilerEnvironment.GetDebuggerLanguageID()

    override _.CreateContext(_,_,_,_,_) = raise(System.NotImplementedException())

    override this.SetupNewTextView(textView) =
        base.SetupNewTextView(textView)

        // Toggles outlining (or code folding) based on settings
        let outliningManagerService = this.Package.ComponentModel.GetService<IOutliningManagerService>()
        let wpfTextView = this.EditorAdaptersFactoryService.GetWpfTextView(textView)
        let outliningManager = outliningManagerService.GetOutliningManager(wpfTextView)
        if not (isNull outliningManager) then
            let settings = this.Workspace.Services.GetService<EditorOptions>()
            outliningManager.Enabled <- settings.Advanced.IsOutliningEnabled

[<Composition.Shared>]
[<System.ComponentModel.Composition.Export(typeof<HackCpsCommandLineChanges>)>]
type internal HackCpsCommandLineChanges
    [<System.ComponentModel.Composition.ImportingConstructor>]
    (
        [<System.ComponentModel.Composition.Import(typeof<VisualStudioWorkspace>)>] workspace: VisualStudioWorkspace
    ) =

    static let projectDisplayNameOf projectFileName =
        if String.IsNullOrWhiteSpace projectFileName then projectFileName
        else Path.GetFileNameWithoutExtension projectFileName

    [<System.ComponentModel.Composition.Export>]
    /// This handles commandline change notifications from the Dotnet Project-system
    /// Prior to VS 15.7 path contained path to project file, post 15.7 contains target binpath
    /// binpath is more accurate because a project file can have multiple in memory projects based on configuration
    member _.HandleCommandLineChanges(path:string, sources:ImmutableArray<CommandLineSourceFile>, _references:ImmutableArray<CommandLineReference>, options:ImmutableArray<string>) =
        use _logBlock = Logger.LogBlock(LogEditorFunctionId.LanguageService_HandleCommandLineArgs)

        let projectId =
            match Microsoft.CodeAnalysis.ExternalAccess.FSharp.LanguageServices.FSharpVisualStudioWorkspaceExtensions.TryGetProjectIdByBinPath(workspace, path) with
            | true, projectId -> projectId
            | false, _ -> Microsoft.CodeAnalysis.ExternalAccess.FSharp.LanguageServices.FSharpVisualStudioWorkspaceExtensions.GetOrCreateProjectIdForPath(workspace, path, projectDisplayNameOf path)
        let path = Microsoft.CodeAnalysis.ExternalAccess.FSharp.LanguageServices.FSharpVisualStudioWorkspaceExtensions.GetProjectFilePath(workspace, projectId)

        let getFullPath p =
            let p' =
                if Path.IsPathRooted(p) || path = null then p
                else Path.Combine(Path.GetDirectoryName(path), p)
            Path.GetFullPathSafe(p')

        let sourcePaths = sources |> Seq.map(fun s -> getFullPath s.Path) |> Seq.toArray

        let workspaceService = workspace.Services.GetRequiredService<IFSharpWorkspaceService>()
        workspaceService.CommandLineOptions.[projectId] <- (sourcePaths, options |> Array.ofSeq)
