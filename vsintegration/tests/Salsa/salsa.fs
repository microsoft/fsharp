// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

(*
    Simplified abstraction over visual studio.
        Stand
        Alone
        Language 
        Service
        Acronym
*)

#nowarn "40" // let rec for recursive values
namespace Salsa

open System
open System.IO
open System.Text
open System.Collections.Generic 
open System.Runtime.InteropServices
open System.Threading
open Microsoft.VisualStudio
open Microsoft.VisualStudio.Shell.Interop
open Microsoft.VisualStudio.FSharp.ProjectSystem
open Microsoft.VisualStudio.FSharp.LanguageService
open Microsoft.VisualStudio.TextManager.Interop
open UnitTests.TestLib.Utils.FilesystemHelpers
open Microsoft.Build.Framework
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.EditorServices

open Microsoft.Build.Evaluation

module internal Salsa = 

    exception MarkerNotFoundException of string

    type HostCompile() =
        let mutable capturedFlags = null
        let mutable capturedSources = null
        let mutable actuallyBuild = true
        member th.CaptureSourcesAndFlagsWithoutBuildingForABit() =
            actuallyBuild <- false
            { new System.IDisposable with
                    member this.Dispose() = actuallyBuild <- true }
        member th.Results = capturedFlags, capturedSources
        member th.Compile(compile:System.Func<int>, flags:string[], sources:string[]) =
            capturedFlags <- flags 
            capturedSources <- sources
            if actuallyBuild then
                compile.Invoke()
            else
                0
        interface ITaskHost
        
    type BuildResult = {
        ExecutableOutput : string
        BuildSucceeded : bool
    }                     

    /// Methods for dealing with MSBuild project files. 
    module (*internal*) MSBuild =
        let mutable private hasAttachedLogger = false
        let mutable theAttachedLogger = null

        /// Use the global build engine if it knows the path to MSBuild.exe
        /// Otherwise, make a new engine and point it at well-known version of MSBuild
        let GlobalEngine() = 
            let engine = Utilities.InitializeMsBuildEngine(null)
            if not hasAttachedLogger then 
                hasAttachedLogger<-true
                let l = new Microsoft.Build.Logging.ConsoleLogger(LoggerVerbosity.Detailed)
                // For Dev10 build we pass the logger to the Build call on the project object.
                theAttachedLogger <- l
            engine
            
        /// Set a global property on the given project.    
        let SetGlobalProperty(project:Project, name, value) = 
            let _ = project.SetGlobalProperty(name,value)
            ()
            
        let hostObjectCachePerFilename = new System.Collections.Generic.Dictionary<_,_>()  // REVIEW: this will leak, but hopefully only a small amount (e.g. maybe about 1K per project, and thus maybe just a few megs total for all 2000 unit tests)

        /// Get the MSBuild project for the given project file.
        let GetProject (projectFileName:string, configuration:string, platform:string) = 
            let project, justCreated, theHostObject =
                try
                    let projects = GlobalEngine().GetLoadedProjects(projectFileName) |> Seq.toList
                    let project = match projects with
                                  | [] -> null
                                  | [x] -> x
                                  | _ -> failwith "multiple projects found"
                    match project with
                    | null ->
                        let project = GlobalEngine().LoadProject(projectFileName)
                        // Set global properties.
                        SetGlobalProperty(project, "AssemblySearchPaths", "{HintPathFromItem};{TargetFrameworkDirectory};{RawFileName}")
                        SetGlobalProperty(project, "BuildingInsideVisualStudio", "true")
                        SetGlobalProperty(project, "Configuration", configuration)
                        SetGlobalProperty(project, "Platform", platform)
                        let prjColl = project.ProjectCollection
                        let hostSvc = prjColl.HostServices
                        let theHostObject = HostCompile()   
                        hostSvc.RegisterHostObject(projectFileName, "CoreCompile", "Fsc", theHostObject)
                        hostObjectCachePerFilename.[projectFileName] <- theHostObject
                        project, true, theHostObject 
                    | project-> 
                        match hostObjectCachePerFilename.TryGetValue(projectFileName) with
                        | true, theHostObject ->
                            project, false, theHostObject
                        | false, _ ->
                            project, false, Unchecked.defaultof<_>  // this code path is hit when unit-testing the project system, which uses its own HostObject
                with e->
                    printfn "Failed in MSBuild GetProject getting '%s'.\n" projectFileName
                    raise e
            project, justCreated, theHostObject

        /// Interesting properties and item lists that from typical MSBuild project files.
        type BuildFlags = {
            flags:string list
            sources:string list
        }

        let prop (project:Project) propertyName : string=
            let p = project.GetPropertyValue(propertyName)
            if p = null then "" else p

        let items (project:Project) name =  
            project.GetItems(name) |> Seq.map (fun i -> i.EvaluatedInclude) |> Seq.toList

        let oneItem (project:Project) name = 
            match (items project name) with
                  head::tail -> head
                | _ -> ""

        let splitProperty (project:Project) propertyName =
            (prop project propertyName).Split([|';'|])|>Array.toList
            
        let boolProperty (project:Project) name =
            let p = prop project name
            true
            
        /// Build the given target on the given project. Return the name of the main output assembly.   
        let Build(projectFileName, target:string, configuration, platform) : BuildResult =         
            let project,_,_ = GetProject(projectFileName, configuration, platform)
            let projectInstance = project.CreateProjectInstance()
            let buildResult = projectInstance.Build(target, Seq.append project.ProjectCollection.Loggers (if theAttachedLogger=null then [] else [theAttachedLogger]))
            printfn "build succeeded? %A" buildResult
            let mainassembly = 
                try
                    (projectInstance.GetItems("TargetFileName") |> Seq.head).EvaluatedInclude
                with e ->
                    ""  // TODO it seems like Dev10 "Clean" target does not produce this output, but this result is not consumed by those tests in an interesting way anyway
            printfn "mainAssembly: %A" mainassembly 
            {ExecutableOutput = mainassembly; BuildSucceeded = buildResult}
            
        /// Return the name of the main output assembly but don't build
        let GetMainOutputAssembly(projectFileName, configuration, platform) : string =         
            let project,_,_ = GetProject(projectFileName, configuration, platform)
            let baseName = Path.GetFileNameWithoutExtension(projectFileName)+".exe"
            let projectInstance = project.CreateProjectInstance()
            let outdir : string = projectInstance.GetProperty("OutDir").EvaluatedValue
            let mainassembly = Path.Combine(outdir,baseName)
            printfn "mainAssembly: %A" mainassembly
            mainassembly            
        
        let CreateFSharpManifestResourceName(projectFileName,configuration, platform) : (string * string) list=
            let targetName = "CreateManifestResourceNames"
            let project,_,_ = GetProject(projectFileName, configuration, platform)
            SetGlobalProperty(project, "CreateManifestResourceNamesDependsOn", "SplitResourcesByCulture")
            let projectInstance = project.CreateProjectInstance()
            let buildResult = projectInstance.Build(targetName, project.ProjectCollection.Loggers)
            let items = projectInstance.GetItems("EmbeddedResource") |> Seq.map (fun i -> i.EvaluatedInclude, i.GetMetadata("ManifestResourceName").EvaluatedValue) |> Seq.toList
            items
        
        /// Fallback for flags and sources. This is to handle the case in which the user has customized
        /// the MSBuild so much that host compilation doesn't work/
        let GetFlagsAndSourcesFallback(project:Project) : BuildFlags =
            project.Build("ResolveReferences") |> ignore
            let sources = items project "Compile"
            let references = items project "ReferencePath"
            let defineConstants=splitProperty project "DefineConstants"
            let flags = (defineConstants |> List.map (sprintf "--define:%s")) @
                            (references |> List.map(sprintf "-r:%s"))
            {flags=flags
             sources = sources}

        /// Compute the Flags and Sources 
        let GetFlagsAndSources(project:Project, host:HostCompile) : BuildFlags = 
            let result =
                use xx = host.CaptureSourcesAndFlagsWithoutBuildingForABit()
                project.IsBuildEnabled <- true
                    
                let loggers = seq { yield (new Microsoft.Build.Logging.ConsoleLogger(LoggerVerbosity.Detailed) :> ILogger) }
                            
                let r = project.Build("Compile", loggers)
                if not(r) then
                    printfn "MSBuild result: %A" r
                    printfn "%s" project.FullPath
                    System.Diagnostics.Debug.Assert(false, "things are about to fail, as MSBuild failed; it would behoove you to turn on MSBuild tracing")
                let capturedFlags, capturedSources = host.Results
                {flags = capturedFlags |> Array.toList 
                 sources = capturedSources |> Array.toList }
            let Canonicalize (filename:string) = 
                if System.IO.Path.IsPathRooted(filename) then
                    System.IO.Path.GetFullPath(filename)
                else
                    System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(project.FullPath),filename))
            { flags = result.flags 
              sources = result.sources |> List.map Canonicalize }
            
        let CrackProject(projectFileName, configuration, platform) =
            
            let project,created,host = GetProject(projectFileName, configuration, platform)

            try        
                try 
                    let flagsAndSources = GetFlagsAndSources(project,host)
                    (project, flagsAndSources)
                with e -> 
                    System.Diagnostics.Debug.Assert(false, sprintf "Bug seen in MSBuild CrackProject: %s %s %s\n" (e.GetType().Name) e.Message (e.StackTrace))
                    reraise()
            finally
                if created then
                    MSBuildProject.FullyUnloadProject(GlobalEngine(), project)
   
    let CreateFSharpManifestResourceName  projectFileName configuration platform =
        MSBuild.CreateFSharpManifestResourceName(projectFileName,configuration,platform)

    module Filenames = 
        /// Compare two file names to eachother.
        let AreSame f1 f2 = 
            let result = 
                   System.String.Compare(f1,f2,StringComparison.CurrentCultureIgnoreCase)=0
                || System.String.Compare(System.IO.Path.GetFullPath(f1),System.IO.Path.GetFullPath(f2),StringComparison.CurrentCultureIgnoreCase)=0
            result                                    

    type MSBuildProjectSite(projectfile,configurationFunc,platformFunc) = 
        let projectPath = Path.GetDirectoryName(projectfile)
        let mutable timestamp = new DateTime()
        let mutable flags = None
        let mutable prevConfig = ""
        let mutable prevPlatform = ""
        let GetFlags() = 
            let newtimestamp = File.GetLastWriteTimeUtc(projectfile)
            let curConfig = configurationFunc()
            let curPlatform = platformFunc()
            if timestamp <> newtimestamp 
                   || flags = None 
                   || prevConfig <> curConfig
                   || prevPlatform <> curPlatform then
                timestamp <- newtimestamp
                prevConfig <- curConfig
                prevPlatform <- curPlatform
                let projectObj, projectObjFlags = MSBuild.CrackProject(projectfile, prevConfig, prevPlatform)
                flags <- Some(projectObjFlags)
            match flags with
            | Some flags -> flags
            | _ -> raise Error.Bug
        let changeHandlers =  new System.Collections.Generic.Dictionary<string,Microsoft.VisualStudio.FSharp.LanguageService.AdviseProjectSiteChanges>()
        member x.TriggerChanges() = 
            for handler in changeHandlers do 
                handler.Value.Invoke()

        override this.ToString() = projectfile

        interface IProjectSite with

          member this.CompilationSourceFiles = 
              GetFlags().sources |> List.map(fun s->Path.Combine(projectPath, s)) |> List.toArray 

          member this.Description = 
              let flags = GetFlags()
              try sprintf "MSBuild Flags:%A" flags
              with e -> sprintf "%A" e                    

          member this.CompilationOptions = GetFlags().flags |> List.toArray 

          member this.ProjectFileName = projectfile

          member this.BuildErrorReporter with get() = None and set _v = ()
          member this.AdviseProjectSiteChanges(callbackOwnerKey,callback) = changeHandlers.[callbackOwnerKey] <- callback
          member this.AdviseProjectSiteCleaned(callbackOwnerKey,callback) = () // no unit testing support here
          member this.AdviseProjectSiteClosed(callbackOwnerKey,callback) = () // no unit testing support here
          member this.IsIncompleteTypeCheckEnvironment = false
          member this.TargetFrameworkMoniker = ""
          member this.LoadTime = System.DateTime(2000,1,1)

          member this.ProjectGuid = 
                let projectObj, projectObjFlags = MSBuild.CrackProject(projectfile, configurationFunc(), platformFunc())
                projectObj.GetProperty(ProjectFileConstants.ProjectGuid).EvaluatedValue

          member this.ProjectProvider = None
          member this.CompilationReferences = [||]
          member this.CompilationBinOutputPath = GetFlags().flags |> List.tryPick (fun s -> if s.StartsWith("-o:") then Some s.[3..] else None)

    // Attempt to treat as MSBuild project.
    let internal NewMSBuildProjectSite(configurationFunc, platformFunc, msBuildProjectName) = 
        let newProjectSite = new MSBuildProjectSite(msBuildProjectName,configurationFunc,platformFunc)
        newProjectSite
        
    /// Token types.
    type TokenType = Text | Keyword | Comment | Identifier | String | Number | InactiveCode | PreprocessorKeyword | Operator
        with override this.ToString() =
            match this with
            | Text                  -> "Text"
            | Keyword               -> "Keyword"
            | Comment               -> "Comment"
            | Identifier            -> "Identifier"
            | String                -> "String"
            | Number                -> "Number"
            | InactiveCode          -> "InactiveCode"
            | PreprocessorKeyword   -> "PreprocessorKeyword"
            | Operator              -> "Operator"

    /// Declaration types.
    type DeclarationType = 
        | Class =0
        | Constant = 6
        | Enum = 18
        | EnumMember = 24
        | Event =30
        | Exception = 36
        | Interface = 48
        | Method = 72
        | Module = 84
        | Namespace = 90
        | Property = 102
        | ValueType = 108               // Like 'type ValueType=int*int' 
        | RareType = 120                // Bucket for unusual types like 'type AsmType = (# "!0[]" #)'
        | DiscriminatedUnion = 132
        
    type BuildAction =
        | Compile = 0
        | EmbeddedResource = 1
        | None = 2  

    /// A file on disk.
    type File = interface
        end    
    /// An error
    [<Sealed>]
    type Error(path: string, subcategory:string, msg: string, context: Microsoft.VisualStudio.TextManager.Interop.TextSpan, sev: Microsoft.VisualStudio.FSharp.LanguageService.Severity) = 
        member e.Path = path
        member e.Subcategory = subcategory
        member e.Message = msg
        member e.Context = context
        member e.Severity = sev
        override e.ToString() = 
            sprintf "%s(%d,%d): %s %A : %s\n" path context.iStartLine context.iStartIndex subcategory sev msg
    
    type ChangeCallBack = IVsHierarchy * string -> unit
    
    /// Hooks for controlling behaviors
    type ProjectBehaviorHooks = 
        /// Create an MSBuild project at the given location with the given files and options.
        abstract CreateProjectHook:  projectFilename:string * files:(string*BuildAction*string option) list * references:(string*bool) list * projReferences: string list * disabledWarnings:string list * defines: string list * versionFile: string * otherFlags:string * preImportXml:string  * targetFrameworkVersion: string->unit
        abstract InitializeProjectHook : OpenProject -> unit
        abstract MakeHierarchyHook : string * string * string * ChangeCallBack  * OleServiceProvider->IVsHierarchy
        abstract AddFileToHierarchyHook : string * IVsHierarchy -> unit
        abstract BuildHook : basename:string * target:string * IVsOutputWindowPane -> BuildResult
        abstract GetMainOutputAssemblyHook : string -> string
        abstract SaveHook : unit -> unit
        abstract DestroyHook : unit->unit
        abstract ModifyConfigurationAndPlatformHook : string->unit
    
    /// A file open in VS.
    and OpenFile = 
        // host VS
        abstract VS : VisualStudio

    /// A project open in VS.
    and OpenProject = 
        // host VS
        abstract VS : VisualStudio

    /// Private part of OpenProject
    and IOpenProject = 
        /// Add a file to this project.
        abstract AddFileFromText : string * string * BuildAction * string list -> File
        /// Add a file to this project as a linked file.
        abstract AddLinkedFileFromText : string * string * string * BuildAction * string list -> File
        /// Open a file that is a member of this project.
        abstract OpenFile : string->OpenFile
        /// Errors (task list) associated with this project
        abstract Errors : Error list with get
        /// Add an assembly reference to the project
        abstract AddAssemblyReference : string * bool -> unit        
        /// Add a project reference to the project        
        abstract AddProjectReference : OpenProject -> unit
        /// Set the version file for this project
        abstract SetVersionFile : string -> unit
        /// Set other flags for this project
        abstract SetOtherFlags : string -> unit
        /// Set the defines for this project.
        abstract SetProjectDefines : string list -> unit
        /// Simulate typing into a project file right before the Import
        abstract PlaceIntoProjectFileBeforeImport : string -> unit
        /// Add a new disabled warning.
        abstract AddDisabledWarning : string -> unit
        /// Build the project. As CTRL+SHIFT+B in VS.  Can pass null for default target (build).
        abstract Build : target:string -> BuildResult
        /// Get the name to the main output assembly for the current configuration.
        abstract GetMainOutputAssembly : unit -> string        
        /// Save the project.
        abstract Save : unit -> unit
        /// Close the project.
        abstract Close : unit -> unit
        /// The project directory.
        abstract Directory : string with get
        /// The project file (e.g. foo.fsproj).
        abstract ProjectFile : string with get
        /// List of already opened files.
        abstract GetOpenFiles : unit->OpenFile list
        /// MSBuild '$(Configuration)|$(Platform)'
        abstract ConfigurationAndPlatform : string with get
        abstract ConfigurationAndPlatform : string with set

    /// A solution open in VS.
    and OpenSolution = 
        // host VS
        abstract VS : VisualStudio
    
    // Private half of the OpenSolution interface
    and IOpenSolution = 
        
        /// Create a new project of the given behaviorHooks that is open in VS.
        abstract CreateProjectFlavor : ProjectBehaviorHooks->string->OpenProject
        /// Close the solution.
        abstract Close : unit -> unit


    /// General purpose methods. Loosely represents running instance of VS.
    and VisualStudio = 
        abstract VsOps : VsOps
     
    /// Private half of the Visual Studio interface   
    and IVisualStudio =
        abstract CreateSolution : unit -> OpenSolution
        abstract OnIdle : unit->unit
        abstract ShiftKeyUp : unit->unit
        abstract ShiftKeyDown : unit->unit
        // Wait long enough for background compile to complete.
        abstract TakeCoffeeBreak : unit->unit
        abstract CleanUp : unit->unit
        abstract ClearLanguageServiceRootCachesAndCollectAndFinalizeAllTransients : unit -> unit
        /// Open a previously-created project
        abstract OpenExistingProject : ProjectBehaviorHooks * dir:string * projName:string -> OpenProject*OpenSolution
        abstract CleanInvisibleProject : unit -> unit
        
    and TextSpan       = Microsoft.VisualStudio.TextManager.Interop.TextSpan
    and GotoDefnResult = Microsoft.VisualStudio.FSharp.LanguageService.GotoDefinitionResult_DEPRECATED
    

    // Result of querying the completion list
    and CompletionItem = CompletionItem of name: string * displayText: string * nameInCode: string * (unit -> string) * DeclarationType

    /// Representes the information that is displayed in the navigation bar
    and NavigationBarResult = 
      { TypesAndModules : DropDownMember[]
        Members : DropDownMember[]
        SelectedType : int
        SelectedMember : int }
        
    /// Methods for simulating VisualStudio    
    and [<NoEquality; NoComparison>] VsOps = 
        abstract BehaviourHooks: ProjectBehaviorHooks
        abstract CreateVisualStudio: unit -> VisualStudio
        abstract CreateSolution: VisualStudio -> OpenSolution
        abstract GetOutputWindowPaneLines: VisualStudio -> string list
        abstract CloseSolution: OpenSolution ->unit
        abstract CreateProject: OpenSolution * string -> OpenProject
        abstract CreateProjectWithHooks: OpenSolution * ProjectBehaviorHooks * string -> OpenProject
        abstract NewFile: VisualStudio * string * BuildAction * string list -> File
        abstract DeleteFileFromDisk: File -> unit
        abstract AddFileFromText: OpenProject * string * string * BuildAction * string list -> File
        abstract AddLinkedFileFromText: OpenProject * string * string * string * BuildAction * string list -> File
        abstract AddAssemblyReference: OpenProject * string * bool -> unit 
        abstract AddProjectReference: OpenProject * OpenProject -> unit 
        abstract ProjectDirectory: OpenProject -> string
        abstract ProjectFile: OpenProject -> string
        abstract SetVersionFile: OpenProject * string -> unit
        abstract SetOtherFlags: OpenProject * string -> unit
        abstract SetConfigurationAndPlatform: OpenProject * string -> unit
        abstract AddDisabledWarning: OpenProject * string -> unit
        abstract GetErrors: OpenProject -> Error list 
        abstract BuildProject: OpenProject * string -> BuildResult 
        abstract GetMainOutputAssembly: OpenProject -> string
        abstract SaveProject: OpenProject -> unit        
        abstract OpenFileViaOpenFile: VisualStudio * string -> OpenFile
        abstract OpenFile: OpenProject * string -> OpenFile 
        abstract GetOpenFiles: OpenProject -> OpenFile list
        abstract SetProjectDefines: OpenProject * string list -> unit
        abstract PlaceIntoProjectFileBeforeImport: OpenProject * string -> unit
        abstract OpenExistingProject: VisualStudio * string * string -> OpenProject * OpenSolution
        abstract MoveCursorTo: OpenFile * int * int -> unit
        abstract GetCursorLocation: OpenFile -> int * int
        abstract GetLineNumber: OpenFile * int -> string
        abstract GetAllLines: OpenFile -> string list
        abstract SwitchToFile: VisualStudio * OpenFile -> unit
        abstract OnIdle: VisualStudio -> unit
        abstract ShiftKeyDown: VisualStudio -> unit
        abstract ShiftKeyUp: VisualStudio -> unit
        abstract TakeCoffeeBreak: VisualStudio -> unit 
        abstract ReplaceFileInMemory: OpenFile * string list * bool -> unit
        abstract SaveFileToDisk: OpenFile -> unit
        abstract CleanUp: VisualStudio -> unit
        abstract CleanInvisibleProject: VisualStudio -> unit
        abstract ClearLanguageServiceRootCachesAndCollectAndFinalizeAllTransients: VisualStudio -> unit
        abstract GetSquiggleAtCursor: OpenFile -> (Microsoft.VisualStudio.FSharp.LanguageService.Severity * string) option
        abstract GetSquigglesAtCursor: OpenFile -> (Microsoft.VisualStudio.FSharp.LanguageService.Severity * string) list
        /// does a BackgroundRequestReason.MemberSelect at the cursor
        abstract AutoCompleteAtCursor: OpenFile -> CompletionItem[]
        /// like AutoCompleteAtCursor, but can pass e.g. BackgroundRequestReason.CompleteWord to do Ctrl-space rather than auto-dot-popup-completion
        abstract CompleteAtCursorForReason: OpenFile * Microsoft.VisualStudio.FSharp.LanguageService.BackgroundRequestReason -> CompletionItem[]
        abstract CompletionBestMatchAtCursorFor: OpenFile * string * string option -> (string * bool * bool) option
        abstract MoveCursorToEndOfMarker: OpenFile * string -> unit
        abstract MoveCursorToStartOfMarker: OpenFile * string -> unit
        abstract GetQuickInfoAtCursor: OpenFile -> string  
        abstract GetQuickInfoAndSpanAtCursor: OpenFile -> string*TextSpan
        abstract GetMatchingBracesForPositionAtCursor: OpenFile -> (TextSpan * TextSpan)[]
        abstract GetNameOfOpenFile: OpenFile -> string
        abstract GetProjectOptionsOfScript: OpenFile -> FSharpProjectOptions
        abstract GetParameterInfoAtCursor: OpenFile -> MethodListForAMethodTip_DEPRECATED option
        abstract GetTokenTypeAtCursor: OpenFile -> TokenType
        abstract GetIdentifierAtCursor: OpenFile -> (string * int) option
        abstract GetF1KeywordAtCursor: OpenFile -> string option
        abstract GotoDefinitionAtCursor: OpenFile * bool -> GotoDefnResult
        abstract CreatePhysicalProjectFileInMemory : files:(string*BuildAction*string option) list * references:(string*bool) list * projectReferences:string list * disabledWarnings:string list * defines:string list * versionFile: string * otherFlags:string * otherProjMisc:string * targetFrameworkVersion:string -> string
                
        /// True if files outside of the project cone are added as links.
        abstract AutoCompleteMemberDataTipsThrowsScope : string -> System.IDisposable
        
        // VsOps capabilities.
        abstract OutOfConeFilesAreAddedAsLinks : bool
        abstract SupportsOutputWindowPane : bool   

    [<AutoOpen>]
    module GotoDefnResultExtensions = 
        type Microsoft.VisualStudio.FSharp.LanguageService.GotoDefinitionResult_DEPRECATED with
            member this.ToOption() = if this.Success then Some(this.Span, this.Url) else None


    let maxErrors = 25
    
    /// Private implementation details.
    module (*private*) Privates = 
        let mutable private cookie = 0u;
        let private nextRdtID() = 
            cookie<-cookie+1u
            cookie
        let private nextItemId() = 
            cookie<-cookie+1u
            cookie
            
        type UndoAction =
            | DeleteFile of string
            | RemoveFolder of string
            
        type Point = {line:int; col:int}
            

        /// Find the given marker and return the line and column.
        let private IsolateMarkerSite (tl:IVsTextLines) (marker:string) : Point= 
            let _, linecount = tl.GetLineCount()
            let mutable returnLine = -1
            let mutable returnCol = -1
            let mutable i = 1   
            while i <= linecount do
                let _, len = tl.GetLengthOfLine(i-1)
                let _, text = tl.GetLineText(i-1,0,i-1,len)
                let markerPos = text.IndexOf(marker)
                if -1 <> markerPos then
                    returnLine <- i
                    returnCol <- markerPos + marker.Length + 1
                    i <- linecount
                    ()
                i <- i + 1
                
            if returnLine = -1 then 
                raise <| MarkerNotFoundException(marker)
            else
                {line = returnLine; col = returnCol}
        
        /// Colorize a single line of text.
        let ColorizeLine (colorizer:FSharpColorizer_DEPRECATED) lineNumber lineText oldState attrs = 
            let marshaled = Marshal.StringToCoTaskMemUni(lineText)
            let newState = colorizer.ColorizeLine(lineNumber, lineText.Length, marshaled, oldState, attrs)
            Marshal.FreeCoTaskMem(marshaled)
            newState

        /// Recolorize a set of lines
        let RecolorizeLines (view:IVsTextView) (getColorizer:IVsTextView->FSharpColorizer_DEPRECATED) (lines:string[]) (linestarts:int[]) (top:int) (bottom:int) = 
            let colorizer = getColorizer(view)
            for i in top..bottom do 
                // let attrs = Array.create fileline.Length 0u 
                linestarts.[i+1] <- ColorizeLine colorizer i lines.[i] linestarts.[i] null            
           
        /// A constant value needed by the colorizer. 
        let humanTextAttribute = ((uint32) (int32 COLORIZER_ATTRIBUTE.HUMAN_TEXT_ATTR))
        
        /// Remove the bits given bits from the original.
        let Mask orig remove = orig &&& (0xffffffffu-remove)
        
        /// Create text of an MSBuild project with the given files and options.
        let CreateMsBuildProjectText 
                (useInstalledTargets : bool)
                (files:(string*BuildAction*string option) list, 
                 references:(string*bool) list,
                 projectReferences:string list,
                 disabledWarnings:string list,
                 defines:string list,
                 versionFile,
                 otherFlags:string,
                 otherProjMisc:string,
                 targetFrameworkVersion:string) =

            // Determine which FSharp.targets file to use. If we use the installed
            // targets file then we check the registry for F#'s install path. Otherwise
            // we look in the same directory as the Unit Tests assembly.
            let targetsFileFolder =
                if useInstalledTargets 
                then Internal.Utilities.FSharpEnvironment.BinFolderOfDefaultFSharpCompiler(None).Value
                else System.AppDomain.CurrentDomain.BaseDirectory
            
            let sb = new System.Text.StringBuilder()
            let Append (text:string) = 
                sb.Append(text+"\r\n") |> ignore
            Append "<Project ToolsVersion='4.0' DefaultTargets='Build' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>"
            Append "    <PropertyGroup>"
//            The salsa layer does Configuration/Platform in a kind of hacky way
//            Append "        <Configuration Condition=\" '$(Configuration)' == '' \">Debug</Configuration>"
//            Append "        <Platform Condition=\" '$(Platform)' == '' \">AnyCPU</Platform>"
            Append "        <OutputPath>bin\Debug\</OutputPath>"
            if versionFile<>null then Append (sprintf "        <VersionFile>%s</VersionFile>" versionFile)
            if otherFlags<>null then Append (sprintf "        <OtherFlags>%s --resolutions</OtherFlags>" otherFlags)
//            if targetFrameworkVersion<>null then
//                Append(sprintf "       <AllowCrossTargeting>true</AllowCrossTargeting>")
//                Append(sprintf "       <TargetFrameworkVersion>%s</TargetFrameworkVersion>" targetFrameworkVersion)
//            else
            Append(sprintf "       <TargetFrameworkVersion>%s</TargetFrameworkVersion>" "4.7.2")
            Append "        <NoWarn>"
            for disabledWarning in disabledWarnings do
                Append (sprintf "            %s;" disabledWarning)                            
            Append "        </NoWarn>"
            Append "        <DefineConstants>"
            for define in defines do
                Append (sprintf "            %s;" define)                            
            Append "        </DefineConstants>"            
            
            Append "    </PropertyGroup>"
//            Append "    <PropertyGroup Condition=\" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' \">"
//            Append "        <OutputPath>bin\Debug\</OutputPath>"
//            Append "    </PropertyGroup>"
//            Append "    <PropertyGroup Condition=\" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' \">"
//            Append "        <OutputPath>bin\Release\</OutputPath>"
//            Append "    </PropertyGroup>"

            Append "    <ItemGroup>"
            
            for (reference,specificVersion) in references do
                Append (sprintf "        <Reference Include=\"%s\">" reference)                
                if specificVersion then
                    Append (sprintf "            <SpecificVersion>true</SpecificVersion>")                
                Append (sprintf "        </Reference>")                

            for projectReference in projectReferences do
                Append (sprintf "        <ProjectReference Include=\"%s\">" projectReference)                
                Append "            <Name>SomeReferencedProject</Name>"     
                Append "            <Project>{45636601-CA91-4382-B8BB-3DBD03BF3F56}</Project>"                
                Append "        </ProjectReference>"

            for (file,buildAction,link) in files do
                let operation = 
                    match buildAction with
                    | BuildAction.None -> "None"
                    | BuildAction.EmbeddedResource -> "EmbeddedResource"
                    | BuildAction.Compile -> "Compile"
                    | _ -> failwith "unexpected"
                    

                    
                match link with 
                | None ->
                    Append (sprintf "        <%s Include='%s'/>" operation file)
                | Some link ->
                    Append (sprintf "        <%s Include='%s'>" operation file)
                    Append (sprintf "            <Link>%s</Link>" link)
                    Append (sprintf "        </%s>" operation)
                
            Append "    </ItemGroup>"
            Append otherProjMisc

            let t = targetsFileFolder.TrimEnd([|'\\'|])
            Append (sprintf "    <Import Project=\"%s\\Microsoft.FSharp.Targets\"/>" t)
            Append "</Project>"
            sb.ToString()

        type MSBuildBehaviorHooks(useInstalledTargets) = 
            let mutable openProject : IOpenProject option = None
            let ConfPlat() = 
                let s = openProject.Value.ConfigurationAndPlatform
                let i = s.IndexOf('|')
                if i = -1 then
                    s,""
                else
                    s.Substring(0,i), s.Substring(i+1)
            let Conf() = let c,_ = ConfPlat() in c
            let Plat() = let _,p = ConfPlat() in p
            interface ProjectBehaviorHooks with 
                member x.CreateProjectHook (projectName, files, references, projectReferences, disabledWarnings, defines, versionFile, otherFlags, preImportXml, targetFrameworkVersion : string) =
                    if File.Exists(projectName) then File.Delete(projectName)
                    let text = CreateMsBuildProjectText useInstalledTargets (files, references, projectReferences, disabledWarnings, defines, versionFile, otherFlags, preImportXml, targetFrameworkVersion)
                    File.WriteAllText(projectName,text+"\r\n")

                member x.InitializeProjectHook op = openProject <- Some(op:?>IOpenProject)
                member x.MakeHierarchyHook (projdir, fullname, projectname, configChangeNotifier, serviceProvider) = 
                    let projectSite = NewMSBuildProjectSite(Conf, Plat, fullname)
                    let projectSiteFactory = { new IProvideProjectSite with member x.GetProjectSite() = (projectSite :> IProjectSite) }
                    let hier = VsMocks.createHier(projectSiteFactory)
                    VsMocks.setHierRoot hier projdir projectname
                    hier
                member x.AddFileToHierarchyHook(filename, hier)  = 
                    let itemid = nextItemId()
                    VsMocks.addRootChild hier itemid filename
                member x.BuildHook (baseName, target, outputWindowPane) = MSBuild.Build(baseName, (if target = null then "Build" else target), Conf(), Plat())
                member x.GetMainOutputAssemblyHook baseName = MSBuild.GetMainOutputAssembly(baseName, Conf(), Plat())
                member x.SaveHook() = ()
                member x.DestroyHook() = ()
                member x.ModifyConfigurationAndPlatformHook (_) = ()

        type SimpleVisualStudio(configChangeNotifier,serviceProvider, ops : VsOps) =                 
            let mutable shiftKeyDown = false
            let mutable languageService : FSharpLanguageServiceTestable option = None
            let mutable undoStack:UndoAction list = []
            let mutable focusFile : SimpleOpenFile option = None
            let mutable solution : SimpleOpenSolution option = None
            let mutable prevSolutions : Map<string,SimpleOpenSolution> = Map.empty
            let mutable bufferToSource = new Dictionary<IVsTextBuffer,IFSharpSource_DEPRECATED>()
            let mutable invisibleSolution : SimpleOpenSolution option = None
            let mutable invisibleProjectFolder : string = null
            let mutable invisibleProject : SimpleOpenProject option = None
            let mutable configChangeNotifier : IVsHierarchy * string -> unit = configChangeNotifier
            let mutable serviceProvider : OleServiceProvider = serviceProvider
            let currentOutputWindowLines = ref []
            let outputWindowPane = VsMocks.vsOutputWindowPane(currentOutputWindowLines)
            
            let vsFileWatch = VsMocks.VsFileChangeEx()
            
            let ReopenSolution fullname =
                match prevSolutions.TryFind fullname with
                | Some(s) -> prevSolutions <- prevSolutions.Remove fullname
                             s
                | None -> failwith "solution with that project does not exist"
                
            member vs.FileChangeEx = vsFileWatch     
            
            member vs.ConfigChangeNotifier = configChangeNotifier           
            member vs.ServiceProvider = serviceProvider    
            member vs.OutputWindowPane = outputWindowPane       
                
            member private vs.InvisibleSolution() = 
                match invisibleSolution with
                | None ->
                    let s = SimpleOpenSolution(vs)
                    invisibleSolution <- Some(s)
                    s
                | Some(s) -> s
                
            member private vs.InvisibleProject(behaviorHooks:ProjectBehaviorHooks) = 
                match invisibleProject with
                | None ->
                    let projdir = NewTempDirectory "salsa-invis"
                    invisibleProjectFolder <- projdir
                    vs.PushUndo(RemoveFolder(projdir))
                    
                    let p = SimpleOpenProject(vs.InvisibleSolution(),null,projdir,"invisible.fsproj",behaviorHooks)
                    invisibleProject <- Some(p)
                    p
                | Some(p) -> p
                
            member vs.NewFile(filename:string,buildAction:BuildAction,lines:string list,behaviorHooks:ProjectBehaviorHooks) =
                let p : IOpenProject = upcast vs.InvisibleProject(behaviorHooks)
                p.AddFileFromText(filename,filename,buildAction,lines)
                
            member vs.OpenFileViaOpenFile(filename:string,behaviorHooks:ProjectBehaviorHooks) =
                let p : IOpenProject = upcast vs.InvisibleProject(behaviorHooks)
                p.OpenFile(filename)
                
            member vs.LanguageService 
                with get() = 
                    match languageService with
                    | Some(languageService) -> languageService
                    | None -> failwith "No salsa language service available."  
                and set(value) =
                    languageService <- Some(value)
                 
            member vs.IsShiftKeyDown = shiftKeyDown
            member vs.PushUndo(u) = 
                undoStack<-u::undoStack
            member vs.GetColorizer(view:IVsTextView) =
                    let _,buffer = view.GetBuffer()
                    vs.LanguageService.GetColorizer(buffer)
            member vs.FocusOpenFile(fileToFocus:SimpleOpenFile) =
                focusFile<-Some(fileToFocus)
            member vs.CloseSolution(fullname) =
                match solution with
                | Some(s) ->
                    prevSolutions <- prevSolutions.Add(fullname, s)
                    solution <- None
                | None -> failwith "there is no open solution"
            
            member vs.AddSourceForBuffer(buffer:IVsTextBuffer,source:IFSharpSource_DEPRECATED) =
                bufferToSource.Add(buffer,source)

            member vs.GetSourceForBuffer(buffer:IVsTextBuffer) =
                bufferToSource.[buffer]
                
            member vs.GetOutputWindowPaneLines() = 
                List.rev !currentOutputWindowLines
                
            // ---------------------------------------------------------
            
            interface VisualStudio with
                member vs.VsOps = ops
            
            // ---------------------------------------------------------
            
            interface IVisualStudio with 
                member vs.CleanInvisibleProject() = 
                    if invisibleProjectFolder <> null then
                        try Directory.Delete(invisibleProjectFolder, true) with _ -> ()
                        invisibleProject <- None
                member vs.CreateSolution() = 
                    match solution with
                    | Some solution -> (solution :> IOpenSolution).Close()
                    | None -> ()
                    let s = SimpleOpenSolution(vs) 
                    solution <- Some(s)
                    s :> OpenSolution
                member solution.OpenExistingProject (behaviorHooks, projdir, projectname) =
                    let fullname = Path.Combine(projdir,projectname+".fsproj")
                    let soln = ReopenSolution fullname
                    let proj = soln.OpenExistingProject behaviorHooks projdir projectname
                    ((proj:>OpenProject),(soln:>OpenSolution))
                member vs.OnIdle() = 
                    vs.LanguageService.OnIdle()
                    match focusFile with
                    | Some(focusFile) -> focusFile.OnIdle()
                    | None -> ()
                member vs.ShiftKeyDown() = shiftKeyDown <- true
                member vs.ShiftKeyUp() = shiftKeyDown <- false
                member vs.TakeCoffeeBreak() = 
                    vs.LanguageService.WaitForBackgroundCompile()
                    (vs :> IVisualStudio).OnIdle()
                    vs.LanguageService.WaitForBackgroundCompile()
                    (vs :> IVisualStudio).OnIdle()
                    vs.LanguageService.WaitForBackgroundCompile()
                    (vs :> IVisualStudio).OnIdle()
                    
                member vs.ClearLanguageServiceRootCachesAndCollectAndFinalizeAllTransients() =
                    vs.LanguageService.ClearLanguageServiceRootCachesAndCollectAndFinalizeAllTransients()
                member vs.CleanUp() = 
                    vs.LanguageService.Unhook()
                    match solution with
                    | Some(s) -> s.CleanUp()
                    | _ -> ()
                    prevSolutions |> Map.toList |> List.iter (fun (_,s) -> s.CleanUp())
                    if true then
                        let undoActions = undoStack
                        undoStack<-[]
                        undoActions |>
                            List.iter(function
                                      DeleteFile f -> 
                                        try
                                            File.Delete(f)
                                        with e->
                                            printf "Failed to Delete file '%s'" f
                                            raise e
                                    | RemoveFolder f -> 
                                        try 
                                            if Directory.Exists(f) then Directory.Delete(f,true)
                                        with 
                                            | :? System.IO.IOException -> printf "Failed to Remove folder '%s'" f
                                            | e->
                                                printf "Failed to Remove folder '%s'" f
                                                raise e)
        and internal SimpleOpenSolution(vs:SimpleVisualStudio) as this = 
            let mutable curProjects : (string*SimpleOpenProject) list = []
            let mutable prevProjects : Map<string,SimpleOpenProject> = Map.empty
            member solution.Vs = vs
            member solution.CleanUp() = 
                curProjects |> List.iter (fun (_,p) -> p.CleanUp())
                prevProjects |> Map.toList |> List.iter (fun (_,p) -> p.CleanUp())
            member solution.OpenExistingProject behaviorHooks projdir projectname =
                let fullname = Path.Combine(projdir,projectname+".fsproj")
                match prevProjects.TryFind fullname with
                | Some(p) -> prevProjects <- prevProjects.Remove fullname
                             p
                | None -> failwith "project does not exist"
            
            interface OpenSolution with
                member solution.VS = vs :> _
            
            interface IOpenSolution with 
                member solution.Close() =
                    curProjects |> List.iter (fun (fullname,p) ->
                        (p :> IOpenProject).Close()
                        vs.CloseSolution fullname
                        prevProjects <- prevProjects.Add(fullname, p)
                    )
                    curProjects <- []
                member solution.CreateProjectFlavor behaviorHooks projectname =
                    // Create the physical project directory.
                    let projdir = NewTempDirectory "salsa"
                    vs.PushUndo(RemoveFolder(projdir))
                    
                    // Put project in there.
                    let fullname = Path.Combine(projdir, projectname+".fsproj")
                    behaviorHooks.CreateProjectHook(fullname, [], [], [], [], [], null, null, "", null)
                    vs.PushUndo(DeleteFile(fullname))
                    
                    // Schedule obj\Debug and bin\Debug to be removed.
                    vs.PushUndo(RemoveFolder(Path.Combine(projdir, "obj")))
                    vs.PushUndo(RemoveFolder(Path.Combine(projdir, "bin")))
                    
                    // Create the hierarchy to go with this project.
                    let hier = behaviorHooks.MakeHierarchyHook(projdir, fullname, projectname, vs.ConfigChangeNotifier, vs.ServiceProvider)

                    // The rest.
                    let p = new SimpleOpenProject(this,hier,projdir,fullname,behaviorHooks)
                    curProjects <- (fullname,p) :: curProjects
                    p :> OpenProject
                    
        and internal SimpleOpenProject(solution:SimpleOpenSolution,hier:IVsHierarchy,directory:string,projectName:string,behaviorHooks:ProjectBehaviorHooks) as this = 
            let mutable configuration = ""
            let mutable preImportXml = ""
            let mutable errors:Error list = []
            let mutable files:SimpleOpenFile list = []
            let mutable filenames:(string*BuildAction*string option) list = []
            let mutable references:(string*bool) list = []
            let mutable projectReferences:string list = []
            let mutable disabledWarnings:string list = []
            let mutable defines:string list = []
            let mutable versionFile : string = null
            let mutable otherFlags : string = null
            let CreateProjectFile() = 
                behaviorHooks.CreateProjectHook(projectName , List.rev filenames, List.rev references, List.rev projectReferences, List.rev disabledWarnings, List.rev defines, versionFile, otherFlags, preImportXml, null)

                // Trigger the AdviseProjectSiteChanges callbacks on our project sites
                match hier with 
                | :? IProvideProjectSite as f -> 
                    match f.GetProjectSite() with 
                    | :? MSBuildProjectSite as m -> 
                        m.TriggerChanges()
                    | _ -> 
                        ()
                | _ -> 
                    ()
                                        
            do
                behaviorHooks.InitializeProjectHook(this :> OpenProject)
            member project.Solution =solution
            member project.Errors 
                with get() = errors
                and set(e) = errors <- e
            member project.CleanUp() =
                behaviorHooks.DestroyHook()

            interface OpenProject with
                member project.VS = solution.Vs :> _

            interface IOpenProject with
                member project.Close() =
                    List.iter (fun (f : SimpleOpenFile) -> f.Close()) files
                    behaviorHooks.DestroyHook()
                member project.Directory 
                    with get() = directory
                member project.ProjectFile = Path.Combine(directory,projectName)
                member project.AddFileFromText(filenameOnDisk, filenameInProject, buildAction, lines) = 
                    // Record the filename without path.
                    filenames <- (filenameInProject,buildAction,None)::filenames
                    // Create the physical file.
                    let filename = Path.Combine(directory, filenameOnDisk)
                    File.WriteAllLines(filename, Array.ofList lines)
                    CreateProjectFile()
                    solution.Vs.PushUndo(DeleteFile(filename))
                    SimpleFile(filename) :> File
                member project.AddLinkedFileFromText(filenameOnDisk, includeFilenameInProject, linkFilenameInProject, buildAction, lines) = 
                    // Record the filename without path.
                    filenames <- (includeFilenameInProject,buildAction, Some linkFilenameInProject)::filenames
                    // Create the physical file.
                    let filename = Path.Combine(directory, filenameOnDisk)
                    File.WriteAllLines(filename, Array.ofList lines)
                    CreateProjectFile()
                    solution.Vs.PushUndo(DeleteFile(filename))
                    SimpleFile(filename) :> File
                member project.Build(target) = 
                    let outputWindowPane = solution.Vs.OutputWindowPane
                    outputWindowPane.Clear() |> ignore
                    let buildResult = behaviorHooks.BuildHook(projectName, target, outputWindowPane)
                    let executableOutput = Path.Combine(directory,buildResult.ExecutableOutput)
                    if target = "Clean" then
                        project.Solution.Vs.FileChangeEx.DeletedFile(executableOutput) // Notify clients of IVsFileChangeEx
                    else
                        project.Solution.Vs.FileChangeEx.AddedFile(executableOutput) // Notify clients of IVsFileChangeEx
                    {buildResult with ExecutableOutput=executableOutput}
                member project.GetMainOutputAssembly() =
                    Path.Combine(directory,behaviorHooks.GetMainOutputAssemblyHook(projectName))                
                member project.Save() = 
                    behaviorHooks.SaveHook()
                member project.AddAssemblyReference(reference,specificVersion) = 
                    references <- (reference,specificVersion)::references
                    CreateProjectFile()
                member project.AddDisabledWarning(code) = 
                    disabledWarnings <- code::disabledWarnings
                    CreateProjectFile()
                member project.SetProjectDefines(definedConstants) = 
                    defines <- definedConstants
                    CreateProjectFile()
                member project.PlaceIntoProjectFileBeforeImport(xml) = 
                    preImportXml <- preImportXml + xml
                    CreateProjectFile()
                
                member project.AddProjectReference(referencedProject) = 
                    projectReferences <- (referencedProject:?>IOpenProject).ProjectFile :: projectReferences
                    CreateProjectFile()
                member project.SetVersionFile(file) = 
                    versionFile <- file
                    CreateProjectFile()
                member project.SetOtherFlags(flags) = 
                    otherFlags <- flags
                    CreateProjectFile()                    
                member project.OpenFile(filename) = 
                    let filename = Path.Combine(directory, filename)
                    
                    // Opening a file that is already open does not create a new file it just opens that same file.
                    match files |> List.tryFind(fun (opf:SimpleOpenFile)->opf.Filename = filename) with
                    | Some(opf) -> 
                        let file = opf :> OpenFile
                        opf.EnsureInitiallyFocusedInVs()
                        file
                    | None ->   
                        // Create the file with IVsTextView
                        let lines = File.ReadAllLines(filename)
                        let view = VsMocks.createTextView()
                        let linestarts = Array.create (lines.Length+1) 0 // One extra to save the state at the end of the file.
                        VsMocks.setFileText filename view lines (RecolorizeLines view solution.Vs.GetColorizer lines linestarts) (fun line->linestarts.[line])
                        
                        // The invisible project does not have a hiearchy.
                        if hier <> null then 
                            // Put the file in the hierarchy
                            behaviorHooks.AddFileToHierarchyHook(filename, hier)
                        
                        // Put the file in the text manager
                        VsMocks.setActiveView (solution.Vs.LanguageService.ServiceProvider.TextManager) view                    
                        
                        // We no longer need the RDT, but keeping it compiling in Salsa/VsMocks in case we ever need it again
                        // Put the document in the RDT
                        let rdtId = nextRdtID()
                        VsMocks.openDocumentInRdt (solution.Vs.LanguageService.ServiceProvider.RunningDocumentTable) rdtId filename view hier
                        // product no longer uses RDT
                        // solution.Vs.LanguageService.OnAfterFirstDocumentLock rdtId 1u 1u

                        // Create the 'Source'
                        let file = SimpleOpenFile(project,filename,lines,view,linestarts,rdtId) 

                        let source = Source.CreateSourceTestable_DEPRECATED(file.RecolorizeWholeFile,file.RecolorizeLine,(fun () -> filename),file.IsClosed,project.Solution.Vs.FileChangeEx, solution.Vs.LanguageService :> IDependencyFileChangeNotify_DEPRECATED)
                        let _,buf = view.GetBuffer()
                        solution.Vs.AddSourceForBuffer(buf,source)                 
                        let source = solution.Vs.LanguageService.CreateSource_DEPRECATED(buf)
                        
                        // Scan all lines with the colorizer
                        let tcs:IVsTextColorState = downcast box(buf)
                        let _ = tcs.ReColorizeLines(0,lines.Length-1)
                        
                        // dprintf "ScanStates=%A\n" linestarts
                        // Return the file.
                        files <- file :: files
                        file.EnsureInitiallyFocusedInVs()
                        // Idle a bit to here (allows the Language Service to do any background processing)
                        (solution.Vs :> IVisualStudio).TakeCoffeeBreak()
                        file :> OpenFile
                member project.Errors 
                    with get() = errors
                member project.GetOpenFiles() =
                    files |> List.map (fun f -> f :> OpenFile)
                member project.ConfigurationAndPlatform with get() = configuration
                                                        and set(s) = 
                                                            configuration <- s
                                                            behaviorHooks.ModifyConfigurationAndPlatformHook(s)
        and internal SimpleFile(filename:string) =
            interface File
            member file.DeleteFileFromDisk() =
                File.Delete(filename)
        and internal SimpleOpenFile(project:SimpleOpenProject,filename:string,lines:string array,view:IVsTextView,scanlines:int[],rdtId) = 
            let mutable lines  = lines
            let mutable scanlines = scanlines
            let mutable cursor:Point = {line=1;col=1}
            let mutable isClosed = false
            let mutable combinedLines:string = null
            
            member file.GetFileName() = filename
            member file.GetProjectOptionsOfScript() = 
                project.Solution.Vs.LanguageService.FSharpChecker.GetProjectOptionsFromScript(filename, FSharp.Compiler.Text.SourceText.ofString file.CombinedLines, false, System.DateTime(2000,1,1), [| |]) 
                |> Async.RunImmediate
                |> fst // drop diagnostics
                 
            member file.RecolorizeWholeFile() = ()
            member file.RecolorizeLine (_line:int) = ()
            member file.IsClosed() = isClosed
            member file.Filename = 
                VsTextLines.GetFilename(Com.ThrowOnFailure1(view.GetBuffer()))
            member file.OnIdle() =
                while file.Source.NeedsVisualRefresh do
                    file.OnIdleTypeCheck()
            member file.CombinedLines : string =
                if combinedLines = null then 
                    combinedLines<-String.Join("\n",lines)
                combinedLines   
            member file.Source : IFSharpSource_DEPRECATED = 
                let _,buf = view.GetBuffer()
                project.Solution.Vs.GetSourceForBuffer(buf)                                       
            
            /// When a file is opened, focus it as the topmost file in VS.
            member file.EnsureInitiallyFocusedInVs() =
                project.Solution.Vs.FocusOpenFile(file)                    
                                     
            member file.TryExecuteBackgroundRequest(pr) = 
                let ls = project.Solution.Vs.LanguageService
                ls.BackgroundRequests.ExecuteBackgroundRequest(pr, file.Source) 
                if pr.ResultClearsDirtinessOfFile then 
                    file.Source.RecordViewRefreshed()
                pr.ResultIntellisenseInfo 

            member file.ExecuteBackgroundRequestForScope(pr,canRetryAfterWaiting) = 
                match file.TryExecuteBackgroundRequest(pr) with 
                | null when canRetryAfterWaiting -> 
                    // OK, no scope is available. Try once more after waiting. The background compile should notify us that the
                    // file becomes dirty again
                    (project.Solution.Vs :> IVisualStudio).TakeCoffeeBreak()
                    file.TryExecuteBackgroundRequest(pr) 
                | res -> 
                    res
                    
            /// This is the periodic check that VS                                     
            member file.OnIdleTypeCheck() = 
                // Remove errors for this file only
                project.Errors <- project.Errors |> List.filter(fun err->err.Path <> file.Filename)
                let ls = project.Solution.Vs.LanguageService
                
                // Full check.                    
                let sink = new AuthoringSink(BackgroundRequestReason.FullTypeCheck, 0, 0, maxErrors) 
                let snapshot = VsActual.createTextBuffer(file.CombinedLines).CurrentSnapshot 
                let pr = project.Solution.Vs.LanguageService.BackgroundRequests.CreateBackgroundRequest(0,0,new TokenInfo(),file.CombinedLines, snapshot, MethodTipMiscellany_DEPRECATED.Typing, System.IO.Path.GetFullPath(file.Filename), BackgroundRequestReason.FullTypeCheck, view,sink,null,file.Source.ChangeCount,false)
                pr.ResultSink.add_OnErrorAdded(
                    OnErrorAddedHandler(fun path subcategory msg context severity -> 
                                project.Errors <- new Error(path, subcategory, msg, context, severity) :: project.Errors))
                file.ExecuteBackgroundRequestForScope(pr,canRetryAfterWaiting=false) |> ignore
                    
            member file.DoIntellisenseRequest(parseReason) =
                if parseReason = BackgroundRequestReason.MemberSelect then
                    // In the actual product, the only thing that can trigger MemberSelect is the auto-popup caused by the "." or ".." tokens (see service.fs:TokenClassifications.tokenInfo)
                    // Thus, let's try to ensure that unit tests are only testing code paths from the actual product, and assert/fail if not.  Best effort.
                    let lineIndex, colIndex = cursor.line-1, cursor.col-1
                    System.Diagnostics.Debug.Assert(colIndex > 0, "hm, how did we invoke at start of line?")
                    let colIndex = colIndex - 1 // cursor is just right of the char we want to inspect
                    if lines.[lineIndex].[colIndex] <> '.' then
                        // there could legally be whitespace or comments to the left, with a '.' left of that, and we have unit tests that do this, so accomodate that case as well, 
                        // at least in the approximate way unit tests do it
                        if lines.[lineIndex].Substring(0,colIndex+1).EndsWith("*)") && lines.[lineIndex].Contains(".(*") then
                            // ok, this is probably fine, have cases like "System.(*marker*)"
                            ()
                        else
                            //System.Diagnostics.Debug.Assert(false, "unit test is doing an AutoComplete MemberSelect at a non-dot location")
                            failwith "unit test is probably doing an AutoComplete MemberSelect at a non-dot location, maybe should be CtrlSpaceComplete instead?"
                file.EnsureInitiallyFocusedInVs()
                let currentAuthoringScope =
                    let ti = new TokenInfo()
                    let sink = new AuthoringSink(parseReason, cursor.line-1, cursor.col-1, maxErrors)
                    let snapshot = VsActual.createTextBuffer(file.CombinedLines).CurrentSnapshot 
                    let pr = project.Solution.Vs.LanguageService.BackgroundRequests.CreateBackgroundRequest(
                                                    cursor.line-1, cursor.col-1, ti, file.CombinedLines, snapshot, MethodTipMiscellany_DEPRECATED.Typing,
                                                    System.IO.Path.GetFullPath(file.Filename),
                                                    parseReason, view, sink, null, file.Source.ChangeCount, false)
                                                   
                    file.ExecuteBackgroundRequestForScope(pr,canRetryAfterWaiting=true)

                match currentAuthoringScope with
                | null -> 
                    System.Diagnostics.Debug.Assert(false, "No Authoring Scope was returned by ExecuteBackgroundRequest, even after waiting")
                    failwith "No Authoring Scope" 
                | _ -> 
                    currentAuthoringScope


            member file.GetCursorLocation() =
                (cursor.line, cursor.col)
            member file.MoveCursorTo(line,col) = 
                file.EnsureInitiallyFocusedInVs()
                if line=0 then failwith "Cursor points are 1-relative. Wrong line value."
                if col=0 then failwith "Cursor points are 1-relative. Wrong col value."
                cursor<-{line=line;col=col}
            member file.MoveCursorToEndOfMarker(marker) = 
                file.EnsureInitiallyFocusedInVs()
                let _,tl = view.GetBuffer()
                cursor <- IsolateMarkerSite tl marker
            member file.MoveCursorToStartOfMarker(marker) = 
                file.EnsureInitiallyFocusedInVs()
                let _,tl = view.GetBuffer()
                let c = IsolateMarkerSite tl marker
                cursor <- {line=c.line;col=c.col-marker.Length}
                // dprintf "Moved cursor to %A\n" cursor
            
            member file.GetQuickInfoAtCursor () = 
                let (result, _) = file.GetQuickInfoAndSpanAtCursor()
                result

            member file.GetQuickInfoAndSpanAtCursor () = 
                let currentAuthoringScope = file.DoIntellisenseRequest BackgroundRequestReason.QuickInfo
                let textspan = new TextSpan ()
                let result,textspan = currentAuthoringScope.GetDataTipText (cursor.line - 1, cursor.col - 1)
                let currentLineLength = lines.[cursor.line-1].Length
                // The new editor is less tolerant of values out of range. Enforce rigor in unittests here.
                if textspan.iEndIndex<0 || textspan.iEndIndex>currentLineLength then failwith (sprintf "GetDataTipText returned iEndIndex out of range. iEndIndex=%d, Line length=%d" textspan.iEndIndex currentLineLength)
                if textspan.iStartIndex<0 || textspan.iStartIndex>currentLineLength then failwith (sprintf "GetDataTipText returned iStartIndex out of range. iStartIndex=%d, Line length=%d" textspan.iStartIndex currentLineLength)
                if textspan.iStartIndex > textspan.iEndIndex then failwith (sprintf "GetDataTipText returned iStartIndex (%d) greater than iEndIndex (%d)" textspan.iStartIndex textspan.iEndIndex)
                result, textspan

            member file.GetMatchingBracesForPositionAtCursor() = 
                file.EnsureInitiallyFocusedInVs()
                let sink =
                    let ti = new TokenInfo()
                    let sink = new AuthoringSink(BackgroundRequestReason.MatchBraces, cursor.line-1, cursor.col-1, maxErrors)
                    let snapshot = VsActual.createTextBuffer(file.CombinedLines).CurrentSnapshot 
                    let pr = project.Solution.Vs.LanguageService.BackgroundRequests.CreateBackgroundRequest(
                                                    cursor.line-1, cursor.col-1, ti, file.CombinedLines, snapshot, MethodTipMiscellany_DEPRECATED.Typing,
                                                    System.IO.Path.GetFullPath(file.Filename),
                                                    BackgroundRequestReason.MatchBraces, view, sink, null, file.Source.ChangeCount, false)
                                                   
                    file.ExecuteBackgroundRequestForScope(pr,canRetryAfterWaiting=false)
                    |> ignore
                    sink
                [|
                    for o in sink.Braces do
                        match o with
                        | (:? Microsoft.VisualStudio.FSharp.LanguageService.BraceMatch_DEPRECATED as m) -> 
                            yield (m.a, m.b)
                        | x -> failwithf "Microsoft.VisualStudio.FSharp.LanguageService.BraceMatch expected, but got %A" (if box x = null then "null" else (x.GetType()).FullName)
                |]

                


            member file.GetParameterInfoAtCursor() = 
                let currentAuthoringScope = 
                    file.EnsureInitiallyFocusedInVs()
                    let currentAuthoringScope =
                        let ti = new TokenInfo()
                        let sink = new AuthoringSink(BackgroundRequestReason.MethodTip, cursor.line-1, cursor.col-1, maxErrors)
                        let snapshot = VsActual.createTextBuffer(file.CombinedLines).CurrentSnapshot 
                        let pr = project.Solution.Vs.LanguageService.BackgroundRequests.CreateBackgroundRequest(
                                                        cursor.line-1, cursor.col-1, ti, file.CombinedLines, snapshot, MethodTipMiscellany_DEPRECATED.ExplicitlyInvokedViaCtrlShiftSpace,
                                                        System.IO.Path.GetFullPath(file.Filename),
                                                        BackgroundRequestReason.MethodTip, view, sink, null, file.Source.ChangeCount, false)
                                                   
                        file.ExecuteBackgroundRequestForScope(pr,canRetryAfterWaiting=true)

                    match currentAuthoringScope with
                    | null -> 
                        System.Diagnostics.Debug.Assert(false, "No Authoring Scope was returned by ExecuteBackgroundRequest, even after waiting")
                        failwith "No Authoring Scope" 
                    | _ -> 
                        currentAuthoringScope

                let methods = currentAuthoringScope.GetMethodListForAMethodTip()
                methods 

            member file.GetTokenTypeAtCursor() = 
                file.EnsureInitiallyFocusedInVs()
                let line = cursor.line-1 // Cursor is 1-relative
                let text = lines.[line]

                let colorizer = project.Solution.Vs.GetColorizer(view)
                let attrs = Array.create text.Length 0u
                let result = ColorizeLine colorizer line text scanlines.[line] attrs
                if result <> scanlines.[line+1] then raise (new Exception("Retokenization of same line gave different results."))
                
                let tokenColor = (int) (Mask attrs.[cursor.col-1] humanTextAttribute)
                match tokenColor with
                    | 0 -> TokenType.Text
                    | 1 -> TokenType.Keyword
                    | 2 -> TokenType.Comment
                    | 3 -> TokenType.Identifier
                    | 4 -> TokenType.String
                    | 5 -> TokenType.Number
                    | 6 -> TokenType.InactiveCode
                    | 7 -> TokenType.PreprocessorKeyword
                    | 8 -> TokenType.Operator
                    | x -> raise (new Exception(sprintf "Unknown token type: %A" x))
            member file.GetSquigglesAtCursor() =
                file.EnsureInitiallyFocusedInVs()
                let IsCursorWithinSpan (cursor:Point) (span:TextSpan) =
                    let cursor = { line=cursor.line-1; col=cursor.col-1 }  // re-adjust to zero-based
                    (span.iStartLine < cursor.line || (span.iStartLine = cursor.line && span.iStartIndex <= cursor.col))
                        && (cursor.line < span.iEndLine || (cursor.line = span.iEndLine && cursor.col <= span.iEndIndex))
                let errors = (project:>IOpenProject).Errors
                errors |> List.filter (fun e -> IsCursorWithinSpan cursor e.Context) |> List.map (fun e -> (e.Severity, e.Message))
                       |> Set.ofList |> Set.toList  // VS ignores duplicates
            member file.GetSquiggleAtCursor() =
                match file.GetSquigglesAtCursor() with
                | [] -> None
                | h::t -> Some h  // arbitrarily pick one
            member file.AutoCompleteAtCursorImpl(reason, ?filterText) =
                let filterText = defaultArg filterText ""
                let currentAuthoringScope = file.DoIntellisenseRequest(reason)
                
                let declarations = 
                    let snapshot = VsActual.createTextBuffer(file.CombinedLines).CurrentSnapshot 
                    currentAuthoringScope.GetDeclarations(snapshot, cursor.line-1, cursor.col-1, reason) |> Async.RunImmediate
                match declarations with 
                | null -> [||]
                | declarations ->
                    let count = declarations.GetCount(filterText)
                    let result = Array.zeroCreate count
                    for i in 0..count-1 do 
                        let glyph = enum<DeclarationType> (declarations.GetGlyph(filterText,i))
                        result.[i] <- CompletionItem (declarations.GetDisplayText(filterText,i), declarations.GetName(filterText,i), declarations.GetNameInCode(filterText,i), (fun () -> declarations.GetDescription(filterText,i)), glyph)
                    result

            member file.AutoCompleteAtCursor(?filterText) = file.AutoCompleteAtCursorImpl(BackgroundRequestReason.MemberSelect, ?filterText=filterText)
            member file.CompleteAtCursorForReason(reason) = file.AutoCompleteAtCursorImpl(reason)
            
            member file.CompletionBestMatchAtCursorFor(text, ?filterText) = 
                let filterText = defaultArg filterText ""
                let currentAuthoringScope = file.DoIntellisenseRequest(BackgroundRequestReason.MemberSelect)
                let declarations = 
                    let snapshot = VsActual.createTextBuffer(file.CombinedLines).CurrentSnapshot 
                    currentAuthoringScope.GetDeclarations(snapshot, cursor.line-1,cursor.col-1, BackgroundRequestReason.MemberSelect) |> Async.RunImmediate
                match declarations with 
                | null -> None
                | declarations -> 
                    let (index, uniqueMatch, prefixMatch) = declarations.GetBestMatch(filterText, text)
                    Some (declarations.GetName(filterText,index), uniqueMatch, prefixMatch)
            
            member file.GotoDefinitionAtCursor (forceGen : bool) =
              file.EnsureInitiallyFocusedInVs ()
              let row = cursor.line - 1
              let col = cursor.col - 1
              let currentAuthoringScope =
                  let ti   = new TokenInfo ()
                  let sink = new AuthoringSink (BackgroundRequestReason.Goto, row, col, maxErrors)
                  let snapshot = VsActual.createTextBuffer(file.CombinedLines).CurrentSnapshot 
                  let pr   = project.Solution.Vs.LanguageService.BackgroundRequests.CreateBackgroundRequest(row, col, ti, file.CombinedLines, snapshot, MethodTipMiscellany_DEPRECATED.Typing, System.IO.Path.GetFullPath file.Filename, BackgroundRequestReason.Goto, view, sink, null, file.Source.ChangeCount, false)
                  file.ExecuteBackgroundRequestForScope(pr,canRetryAfterWaiting=true)
              (currentAuthoringScope :?> FSharpIntellisenseInfo_DEPRECATED).GotoDefinition (view, row, col)
                 
            member file.GetF1KeywordAtCursor() =
              file.EnsureInitiallyFocusedInVs()
              let row = cursor.line - 1
              let col = cursor.col - 1
              let currentAuthoringScope =
                let ti   = new TokenInfo ()
                let sink = new AuthoringSink (BackgroundRequestReason.Goto, row, col, maxErrors)
                let snapshot = VsActual.createTextBuffer(file.CombinedLines).CurrentSnapshot 
                let pr   = project.Solution.Vs.LanguageService.BackgroundRequests.CreateBackgroundRequest(row, col, ti, file.CombinedLines, snapshot, MethodTipMiscellany_DEPRECATED.Typing, System.IO.Path.GetFullPath file.Filename, BackgroundRequestReason.QuickInfo, view, sink, null, file.Source.ChangeCount, false)
                file.ExecuteBackgroundRequestForScope(pr,canRetryAfterWaiting=true)
              let keyword = ref None
              let span = new Microsoft.VisualStudio.TextManager.Interop.TextSpan(iStartIndex=col,iStartLine=row,iEndIndex=col,iEndLine=row)
              let context = Salsa.VsMocks.Vs.VsUserContext (fun (_,key,value) -> (if key = "keyword" then keyword := Some value); VSConstants.S_OK)
                
              currentAuthoringScope.GetF1KeywordString(span, context) 
              !keyword

            /// grab a particular line from a file
            member file.GetLineNumber n =
              file.EnsureInitiallyFocusedInVs ()
              lines.[n - 1]

            /// Get full file contents.
            member file.GetAllLines () = Array.toList lines

            /// get the GotoDefinition-style identifier at a particular location
            member file.GetIdentifierAtCursor () =
              file.EnsureInitiallyFocusedInVs ()
              match QuickParse.GetCompleteIdentifierIsland true lines.[cursor.line - 1] (cursor.col - 1) with
              | Some (s, col, _) -> Some (s, col)
              | None -> None
            
            member file.ReplaceAllText(replacementLines, takeCoffeeBreak) =
                file.EnsureInitiallyFocusedInVs()
                lines <- replacementLines|>List.toArray
                combinedLines <- null
                // Update Scanlines. One extra to save the state at the end of the file.
                scanlines <- Array.create (lines.Length + 1) 0
                let _, priorbuf = view.GetBuffer()
                let source = project.Solution.Vs.GetSourceForBuffer(priorbuf)
                // Update View.
                let projSolution = project.Solution
                VsMocks.setFileText file.Filename view lines (RecolorizeLines view projSolution.Vs.GetColorizer lines scanlines) (fun line->scanlines.[line])
                // Scan all lines with the colorizer
                let _, newbuf = view.GetBuffer()
                if priorbuf<>newbuf then
                    project.Solution.Vs.AddSourceForBuffer(newbuf,source)
                let tcs : IVsTextColorState = downcast box(newbuf)
                let _ = tcs.ReColorizeLines(0,lines.Length-1)
                file.Source.RecordChangeToView()
                // Check the file via OnIdle
                if takeCoffeeBreak then 
                    (projSolution.Vs :> IVisualStudio).TakeCoffeeBreak()
                ()
               
            member file.SaveFileToDisk() =
                file.EnsureInitiallyFocusedInVs()
                let fullFileName = Path.Combine((project:>IOpenProject).Directory, file.Filename)
                File.WriteAllLines(fullFileName, lines)               
                project.Solution.Vs.FileChangeEx.ChangedFile(fullFileName) // Notify clients of IVsFileChangeEx
                
                // In Visual Studio, OnAfterFirstDocumentLock is called for "Save" and "Save All"
                // Model this here. This ultimately causes a ProjectSystem --> LanguageService
                // "project change" notification which starts the background build for a
                // project
                // Product no longer uses RDT, so below is commented out
                // project.Solution.Vs.LanguageService.OnAfterFirstDocumentLock rdtId 1u 1u


            member file.Close () = isClosed <- true
            interface OpenFile with
                member file.VS = project.Solution.Vs :> _

                    
    /// Create a simple Salsa API.
    let CreateSimple(ops) = 
        try 
            if SynchronizationContext.Current = null then
                let context = new SynchronizationContext() // This executes on the threadpool, but I can't figure out how to get the context for the form.
                SynchronizationContext.SetSynchronizationContext(context) 
        
            let sp,configChangeNotifier = VsMocks.MakeMockServiceProviderAndConfigChangeNotifier()
        
            let ls = FSharpLanguageServiceTestable()
            let rdt = box (VsMocks.createRdt())
            let tm = box (VsMocks.createTextManager())
            let documentationProvider = 
                { new IDocumentationBuilder_DEPRECATED with
                    override doc.AppendDocumentationFromProcessedXML(appendTo,processedXml:string,showExceptions, showReturns, paramName) = 
                        appendTo.Add(FSharp.Compiler.Text.TaggedText.tagText processedXml)
                        appendTo.Add(FSharp.Compiler.Text.TaggedText.lineBreak)
                    override doc.AppendDocumentation(appendTo,filename:string,signature:string, showExceptions, showReturns, paramName) = 
                        appendTo.Add(FSharp.Compiler.Text.TaggedText.tagText (sprintf "[Filename:%s]" filename))
                        appendTo.Add(FSharp.Compiler.Text.TaggedText.lineBreak)
                        appendTo.Add(FSharp.Compiler.Text.TaggedText.tagText (sprintf "[Signature:%s]" signature))
                        appendTo.Add(FSharp.Compiler.Text.TaggedText.lineBreak)
                        if paramName.IsSome then
                            appendTo.Add(FSharp.Compiler.Text.TaggedText.tagText (sprintf "[ParamName: %s]" paramName.Value))
                            appendTo.Add(FSharp.Compiler.Text.TaggedText.lineBreak)
                } 

            let sp2 = 
               { new System.IServiceProvider with 
                   member _.GetService(serviceType:Type) : obj = 
                        if serviceType = typeof<SVsRunningDocumentTable> then rdt
                        else if serviceType = typeof<SVsTextManager> then tm
                        else raise (new Exception(sprintf "Salsa did not create service %A"  serviceType)) }
                
            let vs = Privates.SimpleVisualStudio(configChangeNotifier,sp, ops)
            ls.Initialize (sp2,documentationProvider,VsMocks.createLanguagePreferences(),vs.GetSourceForBuffer)
            vs.LanguageService <- ls
            vs :> VisualStudio
        with e -> 
            // Need to just print the error because NUnit has not fully initialized the exception at this point.
            printf "Error in createSimple: %A" e
            reraise() 
    
    // ------------------------------------------------------------------------------
    
    let VsSimpl(vs:VisualStudio) = vs :?> Privates.SimpleVisualStudio
    let VsImpl(vs:VisualStudio)         = vs :?> IVisualStudio
    let SolutionImpl(sol:OpenSolution)  = sol :?> IOpenSolution
    let ProjectImpl(proj:OpenProject)   = proj :?> IOpenProject
    let OpenFileSimpl(openfile:OpenFile)   = openfile :?> Privates.SimpleOpenFile
    let FileSimpl(file:File) = file :?> Privates.SimpleFile


    /// Salsa tests which create .fsproj files for projects.
    type MSBuildTestFlavor(useInstalledTargets) = 
        let behaviorHooks = Privates.MSBuildBehaviorHooks(useInstalledTargets) :> ProjectBehaviorHooks
        interface VsOps with
            member ops.BehaviourHooks = behaviorHooks
            member ops.CreateVisualStudio () = CreateSimple(ops)
            member ops.CreateSolution vs = VsImpl(vs).CreateSolution()
            member ops.GetOutputWindowPaneLines vs = VsSimpl(vs).GetOutputWindowPaneLines()
            member ops.CloseSolution (solution) = SolutionImpl(solution).Close()
            member ops.CreateProject (solution,projectBaseName) = SolutionImpl(solution).CreateProjectFlavor behaviorHooks projectBaseName
            member ops.CreateProjectWithHooks (solution,hooks,projectBaseName) = SolutionImpl(solution).CreateProjectFlavor hooks projectBaseName
            member ops.NewFile (vs,filename,buildAction,lines) = VsSimpl(vs).NewFile(filename,buildAction,lines,behaviorHooks)
            member ops.DeleteFileFromDisk (file:File) = FileSimpl(file).DeleteFileFromDisk()
            member ops.AddFileFromText (project:OpenProject,filenameOnDisk,filenameInProject,buildAction,lines) = ProjectImpl(project).AddFileFromText(filenameOnDisk,filenameInProject,buildAction,lines)
            member ops.AddLinkedFileFromText (project:OpenProject,filenameOnDisk,includeFilenameInProject,linkFilenameInProject,buildAction,lines)=ProjectImpl(project).AddLinkedFileFromText(filenameOnDisk,includeFilenameInProject,linkFilenameInProject,buildAction,lines)
            member ops.AddAssemblyReference (project,reference,specificVersion) = ProjectImpl(project).AddAssemblyReference(reference,specificVersion)
            member ops.AddProjectReference (project1,project2) = ProjectImpl(project1).AddProjectReference(project2)
            member ops.ProjectDirectory project = ProjectImpl(project).Directory            
            member ops.ProjectFile project = ProjectImpl(project).ProjectFile
            member ops.SetVersionFile (project,file) = ProjectImpl(project).SetVersionFile(file)
            member ops.SetOtherFlags (project,flags) = ProjectImpl(project).SetOtherFlags(flags)
            member ops.SetConfigurationAndPlatform (project,configAndPlatform) = ProjectImpl(project).ConfigurationAndPlatform <- configAndPlatform
            member ops.AddDisabledWarning (project,code) = ProjectImpl(project).AddDisabledWarning(code)
            member ops.GetErrors project = ProjectImpl(project).Errors
            member ops.BuildProject (project,target) = ProjectImpl(project).Build(target)
            member ops.GetMainOutputAssembly project = ProjectImpl(project).GetMainOutputAssembly()
            member ops.SaveProject project = ProjectImpl(project).Save()                
            member ops.OpenFileViaOpenFile (vs,filename) = VsSimpl(vs).OpenFileViaOpenFile(filename,behaviorHooks)
            member ops.OpenFile (project,filename) = ProjectImpl(project).OpenFile(filename)
            member ops.SetProjectDefines (project,defines) = ProjectImpl(project).SetProjectDefines(defines)
            member ops.PlaceIntoProjectFileBeforeImport (project,xml) = ProjectImpl(project).PlaceIntoProjectFileBeforeImport(xml)
            member ops.GetOpenFiles project = ProjectImpl(project).GetOpenFiles()
            member ops.MoveCursorTo (file,line,col) = OpenFileSimpl(file).MoveCursorTo(line,col)
            member ops.GetCursorLocation (file) = OpenFileSimpl(file).GetCursorLocation()
            member ops.OpenExistingProject (vs,dir,projname) = VsImpl(vs).OpenExistingProject(behaviorHooks, dir, projname)
            member ops.MoveCursorToEndOfMarker (file,marker) = OpenFileSimpl(file).MoveCursorToEndOfMarker(marker)
            member ops.MoveCursorToStartOfMarker (file,marker) = OpenFileSimpl(file).MoveCursorToStartOfMarker(marker)
            member ops.GetNameOfOpenFile (file) = OpenFileSimpl(file).GetFileName()
            member ops.GetProjectOptionsOfScript (file) = OpenFileSimpl(file).GetProjectOptionsOfScript()
            member ops.GetQuickInfoAtCursor file = OpenFileSimpl(file).GetQuickInfoAtCursor()
            member ops.GetQuickInfoAndSpanAtCursor file = OpenFileSimpl(file).GetQuickInfoAndSpanAtCursor()
            member ops.GetMatchingBracesForPositionAtCursor file = OpenFileSimpl(file).GetMatchingBracesForPositionAtCursor()
            member ops.GetParameterInfoAtCursor file = OpenFileSimpl(file).GetParameterInfoAtCursor()
            member ops.GetTokenTypeAtCursor file = OpenFileSimpl(file).GetTokenTypeAtCursor()
            member ops.GetSquiggleAtCursor file = OpenFileSimpl(file).GetSquiggleAtCursor()
            member ops.GetSquigglesAtCursor file = OpenFileSimpl(file).GetSquigglesAtCursor()
            member ops.AutoCompleteAtCursor file = OpenFileSimpl(file).AutoCompleteAtCursor()
            member ops.CompleteAtCursorForReason (file,reason) = OpenFileSimpl(file).CompleteAtCursorForReason(reason)
            member ops.CompletionBestMatchAtCursorFor (file, value, filterText) = (OpenFileSimpl(file)).CompletionBestMatchAtCursorFor(value, ?filterText=filterText)
            member ops.GotoDefinitionAtCursor (file, forceGen) = (OpenFileSimpl file).GotoDefinitionAtCursor forceGen
            member ops.GetIdentifierAtCursor file = OpenFileSimpl(file).GetIdentifierAtCursor ()
            member ops.GetF1KeywordAtCursor file = OpenFileSimpl(file).GetF1KeywordAtCursor ()
            member ops.GetLineNumber (file, n) = OpenFileSimpl(file).GetLineNumber n
            member ops.GetAllLines file = (OpenFileSimpl file).GetAllLines ()
            member ops.SwitchToFile (vs,file) = VsSimpl(vs).FocusOpenFile(OpenFileSimpl(file))
            member ops.OnIdle vs = VsImpl(vs).OnIdle()
            member ops.ShiftKeyDown vs = VsImpl(vs).ShiftKeyDown()
            member ops.ShiftKeyUp vs = VsImpl(vs).ShiftKeyUp()
            member ops.TakeCoffeeBreak vs = VsImpl(vs).TakeCoffeeBreak() 
            member ops.ReplaceFileInMemory (file,contents,takeCoffeeBreak) = OpenFileSimpl(file).ReplaceAllText(contents, takeCoffeeBreak)
            member ops.SaveFileToDisk file = OpenFileSimpl(file).SaveFileToDisk()
            member ops.CreatePhysicalProjectFileInMemory (files, references, projectReferences, disabledWarnings, defines, versionFile, otherFlags, otherProjMisc, targetFrameworkVersion) = Privates.CreateMsBuildProjectText useInstalledTargets (files, references, projectReferences, disabledWarnings, defines, versionFile, otherFlags, otherProjMisc, targetFrameworkVersion)
            member ops.CleanUp vs = VsImpl(vs).CleanUp()
            member ops.ClearLanguageServiceRootCachesAndCollectAndFinalizeAllTransients vs = VsImpl(vs).ClearLanguageServiceRootCachesAndCollectAndFinalizeAllTransients()
            member ops.AutoCompleteMemberDataTipsThrowsScope message = 
                DeclarationListHelpers.ToolTipFault <- Some message
                { new System.IDisposable with member x.Dispose() = DeclarationListHelpers.ToolTipFault <- None }
            member ops.OutOfConeFilesAreAddedAsLinks = false                
            member ops.SupportsOutputWindowPane = false
            member ops.CleanInvisibleProject vs = VsImpl(vs).CleanInvisibleProject()

    let BuiltMSBuildBehaviourHooks() = Privates.MSBuildBehaviorHooks(false) :> ProjectBehaviorHooks
            
    /// Salsa tests which create .fsproj files using the freshly built version of Microsoft.FSharp.Targets and FSharp.Build
    let BuiltMSBuildTestFlavour() = MSBuildTestFlavor(false) :> VsOps

    /// Salsa tests which create .fsproj files using the installed version of Microsoft.FSharp.Targets.
    let InstalledMSBuildTestFlavour() = MSBuildTestFlavor(true) :> VsOps
