// Copyright (c) Microsoft Corporation. All Rights Reserved. See License.txt in the project root for license information.

/// The configuration of the compiler (TcConfig and TcConfigBuilder)
module internal FSharp.Compiler.CompilerConfig

open System
open System.Collections.Concurrent
open System.IO
open Internal.Utilities
open Internal.Utilities.FSharpEnvironment
open Internal.Utilities.Library
open Internal.Utilities.Library.Extras
open FSharp.Compiler
open FSharp.Compiler.AbstractIL
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AbstractIL.ILBinaryReader
open FSharp.Compiler.AbstractIL.ILPdbWriter
open FSharp.Compiler.DependencyManager
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Features
open FSharp.Compiler.IO
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Compiler.Xml
open FSharp.Compiler.TypedTree
open FSharp.Compiler.BuildGraph

#if !NO_EXTENSIONTYPING
open FSharp.Compiler.ExtensionTyping
open FSharp.Core.CompilerServices
#endif

let (++) x s = x @ [s]

//----------------------------------------------------------------------------
// Some Globals
//--------------------------------------------------------------------------

let FSharpSigFileSuffixes = [".mli";".fsi"]

let mlCompatSuffixes = [".mli";".ml"]

let FSharpImplFileSuffixes = [".ml";".fs";".fsscript";".fsx"]

let FSharpScriptFileSuffixes = [".fsscript";".fsx"]

let doNotRequireNamespaceOrModuleSuffixes = [".mli";".ml"] @ FSharpScriptFileSuffixes

let FSharpLightSyntaxFileSuffixes: string list = [ ".fs";".fsscript";".fsx";".fsi" ]

//--------------------------------------------------------------------------
// General file name resolver
//--------------------------------------------------------------------------

exception FileNameNotResolved of (*filename*) string * (*description of searched locations*) string * range
exception LoadedSourceNotFoundIgnoring of (*filename*) string * range

/// Will return None if the filename is not found.
let TryResolveFileUsingPaths(paths, m, name) =
    let () =
        try FileSystem.IsPathRootedShim name |> ignore
        with :? ArgumentException as e -> error(Error(FSComp.SR.buildProblemWithFilename(name, e.Message), m))
    if FileSystem.IsPathRootedShim name && FileSystem.FileExistsShim name
    then Some name
    else
        let res = paths |> List.tryPick (fun path ->
                    let n = Path.Combine (path, name)
                    if FileSystem.FileExistsShim n then Some n
                    else None)
        res

/// Will raise FileNameNotResolved if the filename was not found
let ResolveFileUsingPaths(paths, m, name) =
    match TryResolveFileUsingPaths(paths, m, name) with
    | Some res -> res
    | None ->
        let searchMessage = String.concat "\n " paths
        raise (FileNameNotResolved(name, searchMessage, m))

let GetWarningNumber(m, warningNumber: string) =
    try
        // Okay so ...
        //      #pragma strips FS of the #pragma "FS0004" and validates the warning number
        //      therefore if we have warning id that starts with a numeric digit we convert it to Some (int32)
        //      anything else is ignored None
        if Char.IsDigit(warningNumber.[0]) then Some (int32 warningNumber)
        elif warningNumber.StartsWithOrdinal("FS") = true then raise (new ArgumentException())
        else None
    with _ ->
        warning(Error(FSComp.SR.buildInvalidWarningNumber warningNumber, m))
        None

let ComputeMakePathAbsolute implicitIncludeDir (path: string) =
    try
        // remove any quotation marks from the path first
        let path = path.Replace("\"", "")
        if not (FileSystem.IsPathRootedShim path)
        then Path.Combine (implicitIncludeDir, path)
        else path
    with
        :? System.ArgumentException -> path

//----------------------------------------------------------------------------
// Configuration
//----------------------------------------------------------------------------

[<RequireQualifiedAccess>]
type CompilerTarget =
    | WinExe
    | ConsoleExe
    | Dll
    | Module
    member x.IsExe = (match x with ConsoleExe | WinExe -> true | _ -> false)

[<RequireQualifiedAccess>]
type ResolveAssemblyReferenceMode = Speculative | ReportErrors

[<RequireQualifiedAccess>]
type CopyFSharpCoreFlag = Yes | No

/// Represents the file or string used for the --version flag
type VersionFlag =
    | VersionString of string
    | VersionFile of string
    | VersionNone
    member x.GetVersionInfo implicitIncludeDir =
        let vstr = x.GetVersionString implicitIncludeDir
        try
            IL.parseILVersion vstr
        with _ -> errorR(Error(FSComp.SR.buildInvalidVersionString vstr, rangeStartup)); IL.parseILVersion "0.0.0.0"

    member x.GetVersionString implicitIncludeDir =
         match x with
         | VersionString s -> s
         | VersionFile s ->
             let s = if FileSystem.IsPathRootedShim s then s else Path.Combine(implicitIncludeDir, s)
             if not(FileSystem.FileExistsShim s) then
                 errorR(Error(FSComp.SR.buildInvalidVersionFile s, rangeStartup)); "0.0.0.0"
             else
                 use fs = FileSystem.OpenFileForReadShim(s)
                 use is = new StreamReader(fs)
                 is.ReadLine()
         | VersionNone -> "0.0.0.0"


/// Represents a reference to an assembly. May be backed by a real assembly on disk, or a cross-project
/// reference backed by information generated by the the compiler service.
type IRawFSharpAssemblyData =
    ///  The raw list AutoOpenAttribute attributes in the assembly
    abstract GetAutoOpenAttributes: unit -> string list

    ///  The raw list InternalsVisibleToAttribute attributes in the assembly
    abstract GetInternalsVisibleToAttributes: unit -> string list

    ///  The raw IL module definition in the assembly, if any. This is not present for cross-project references
    /// in the language service
    abstract TryGetILModuleDef: unit -> ILModuleDef option

    ///  The raw F# signature data in the assembly, if any
    abstract GetRawFSharpSignatureData: range * ilShortAssemName: string * fileName: string -> (string * (unit -> ReadOnlyByteMemory)) list

    ///  The raw F# optimization data in the assembly, if any
    abstract GetRawFSharpOptimizationData: range * ilShortAssemName: string * fileName: string -> (string * (unit -> ReadOnlyByteMemory)) list

    ///  The table of type forwarders in the assembly
    abstract GetRawTypeForwarders: unit -> ILExportedTypesAndForwarders

    /// The identity of the module
    abstract ILScopeRef: ILScopeRef

    abstract ILAssemblyRefs: ILAssemblyRef list

    abstract ShortAssemblyName: string

    abstract HasAnyFSharpSignatureDataAttribute: bool

    abstract HasMatchingFSharpSignatureDataAttribute: bool

/// Cache of time stamps as we traverse a project description
type TimeStampCache(defaultTimeStamp: DateTime) =
    let files = ConcurrentDictionary<string, DateTime>()
    let projects = ConcurrentDictionary<IProjectReference, DateTime>(HashIdentity.Reference)
    member cache.GetFileTimeStamp fileName =
        let ok, v = files.TryGetValue fileName
        if ok then v else
        let v =
            try
                FileSystem.GetLastWriteTimeShim fileName
            with
            | :? FileNotFoundException ->
                defaultTimeStamp
        files.[fileName] <- v
        v

    member cache.GetProjectReferenceTimeStamp (pr: IProjectReference) =
        let ok, v = projects.TryGetValue pr
        if ok then v else
        let v = defaultArg (pr.TryGetLogicalTimeStamp (cache)) defaultTimeStamp
        projects.[pr] <- v
        v

and IProjectReference =
    /// The name of the assembly file generated by the project
    abstract FileName: string

    /// Evaluate raw contents of the assembly file generated by the project
    abstract EvaluateRawContents: unit -> NodeCode<IRawFSharpAssemblyData option>

    /// Get the logical timestamp that would be the timestamp of the assembly file generated by the project
    ///
    /// For project references this is maximum of the timestamps of all dependent files.
    /// The project is not actually built, nor are any assemblies read, but the timestamps for each dependent file
    /// are read via the FileSystem. If the files don't exist, then a default timestamp is used.
    ///
    /// The operation returns None only if it is not possible to create an IncrementalBuilder for the project at all, e.g. if there
    /// are fatal errors in the options for the project.
    abstract TryGetLogicalTimeStamp: TimeStampCache -> System.DateTime option

type AssemblyReference =
    | AssemblyReference of range * string * IProjectReference option

    member x.Range = (let (AssemblyReference(m, _, _)) = x in m)

    member x.Text = (let (AssemblyReference(_, text, _)) = x in text)

    member x.ProjectReference = (let (AssemblyReference(_, _, contents)) = x in contents)

    member x.SimpleAssemblyNameIs name =
        (String.Compare(FileSystemUtils.fileNameWithoutExtensionWithValidate false x.Text, name, StringComparison.OrdinalIgnoreCase) = 0) ||
        not (x.Text.Contains "/") &&
        not (x.Text.Contains "\\") &&
        not (x.Text.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase)) &&
        not (x.Text.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase)) &&
        (try let aname = System.Reflection.AssemblyName x.Text in aname.Name = name
         with _ -> false)

    override x.ToString() = sprintf "AssemblyReference(%s)" x.Text

type UnresolvedAssemblyReference = UnresolvedAssemblyReference of string * AssemblyReference list
#if !NO_EXTENSIONTYPING
type ResolvedExtensionReference = ResolvedExtensionReference of string * AssemblyReference list * Tainted<ITypeProvider> list
#endif

type ImportedAssembly =
    { ILScopeRef: ILScopeRef
      FSharpViewOfMetadata: CcuThunk
      AssemblyAutoOpenAttributes: string list
      AssemblyInternalsVisibleToAttributes: string list
#if !NO_EXTENSIONTYPING
      IsProviderGenerated: bool
      mutable TypeProviders: Tainted<Microsoft.FSharp.Core.CompilerServices.ITypeProvider> list
#endif
      FSharpOptimizationData: Microsoft.FSharp.Control.Lazy<Option<Optimizer.LazyModuleInfo>> }

type AvailableImportedAssembly =
    | ResolvedImportedAssembly of ImportedAssembly
    | UnresolvedImportedAssembly of string

type CcuLoadFailureAction =
    | RaiseError
    | ReturnNone

type Directive =
    | Resolution
    | Include

type LStatus =
    | Unprocessed
    | Processed

type PackageManagerLine =
    { Directive: Directive
      LineStatus: LStatus
      Line: string
      Range: range }

    static member AddLineWithKey (packageKey: string) (directive:Directive) (line: string) (m: range) (packageManagerLines: Map<string, PackageManagerLine list>): Map<string, PackageManagerLine list>  =
        let path = PackageManagerLine.StripDependencyManagerKey packageKey line
        let map =
            let mutable found = false
            let result =
                packageManagerLines
                |> Map.map(fun key lines ->
                    if key = packageKey then
                        found <- true
                        lines |> List.append [{Directive=directive; LineStatus=LStatus.Unprocessed; Line=path; Range=m}]
                    else
                        lines)
            if found then
                result
            else
                result.Add(packageKey, [{Directive=directive; LineStatus=LStatus.Unprocessed; Line=path; Range=m}])
        map

    static member RemoveUnprocessedLines (packageKey: string) (packageManagerLines: Map<string, PackageManagerLine list>): Map<string, PackageManagerLine list> =
        let map =
            packageManagerLines
            |> Map.map(fun key lines ->
                if key = packageKey then
                    lines |> List.filter(fun line -> line.LineStatus=LStatus.Processed)
                else
                    lines)
        map

    static member SetLinesAsProcessed (packageKey:string) (packageManagerLines: Map<string, PackageManagerLine list>): Map<string, PackageManagerLine list> =
        let map =
            packageManagerLines
            |> Map.map(fun key lines ->
                if key = packageKey then
                    lines |> List.map(fun line -> {line with LineStatus = LStatus.Processed;})
                else
                    lines)
        map

    static member StripDependencyManagerKey (packageKey: string) (line: string): string =
        line.Substring(packageKey.Length + 1).Trim()

[<NoEquality; NoComparison>]
type TcConfigBuilder =
    {
      mutable primaryAssembly: PrimaryAssembly
      mutable noFeedback: bool
      mutable stackReserveSize: int32 option
      mutable implicitIncludeDir: string (* normally "." *)
      mutable openDebugInformationForLaterStaticLinking: bool (* only for --standalone *)
      defaultFSharpBinariesDir: string
      mutable compilingFslib: bool
      mutable useIncrementalBuilder: bool
      mutable includes: string list
      mutable implicitOpens: string list
      mutable useFsiAuxLib: bool
      mutable framework: bool
      mutable resolutionEnvironment: LegacyResolutionEnvironment
      mutable implicitlyResolveAssemblies: bool
      mutable light: bool option
      mutable conditionalCompilationDefines: string list
      mutable loadedSources: (range * string * string) list
      mutable compilerToolPaths: string list
      mutable referencedDLLs: AssemblyReference list
      mutable packageManagerLines: Map<string, PackageManagerLine list>
      mutable projectReferences: IProjectReference list
      mutable knownUnresolvedReferences: UnresolvedAssemblyReference list
      reduceMemoryUsage: ReduceMemoryFlag
      mutable subsystemVersion: int * int
      mutable useHighEntropyVA: bool
      mutable inputCodePage: int option
      mutable embedResources: string list
      mutable errorSeverityOptions: FSharpDiagnosticOptions
      mutable mlCompatibility: bool
      mutable checkOverflow: bool
      mutable showReferenceResolutions: bool
      mutable outputDir : string option
      mutable outputFile: string option
      mutable platform: ILPlatform option
      mutable prefer32Bit: bool
      mutable useSimpleResolution: bool
      mutable target: CompilerTarget
      mutable debuginfo: bool
      mutable testFlagEmitFeeFeeAs100001: bool
      mutable dumpDebugInfo: bool
      mutable debugSymbolFile: string option
      (* Backend configuration *)
      mutable typeCheckOnly: bool
      mutable parseOnly: bool
      mutable importAllReferencesOnly: bool
      mutable simulateException: string option
      mutable printAst: bool
      mutable tokenizeOnly: bool
      mutable testInteractionParser: bool
      mutable reportNumDecls: bool
      mutable printSignature: bool
      mutable printSignatureFile: string
      mutable printAllSignatureFiles: bool
      mutable xmlDocOutputFile: string option
      mutable stats: bool
      mutable generateFilterBlocks: bool (* don't generate filter blocks due to bugs on Mono *)

      mutable signer: string option
      mutable container: string option

      mutable delaysign: bool
      mutable publicsign: bool
      mutable version: VersionFlag
      mutable metadataVersion: string option
      mutable standalone: bool
      mutable extraStaticLinkRoots: string list
      mutable noSignatureData: bool
      mutable onlyEssentialOptimizationData: bool
      mutable useOptimizationDataFile: bool
      mutable jitTracking: bool
      mutable portablePDB: bool
      mutable embeddedPDB: bool
      mutable embedAllSource: bool
      mutable embedSourceList: string list
      mutable sourceLink: string

      mutable ignoreSymbolStoreSequencePoints: bool
      mutable internConstantStrings: bool
      mutable extraOptimizationIterations: int

      mutable win32icon: string
      mutable win32res: string
      mutable win32manifest: string
      mutable includewin32manifest: bool
      mutable linkResources: string list
      mutable legacyReferenceResolver: LegacyReferenceResolver

      mutable showFullPaths: bool
      mutable errorStyle: ErrorStyle
      mutable utf8output: bool
      mutable flatErrors: bool

      mutable maxErrors: int
      mutable abortOnError: bool (* intended for fsi scripts that should exit on first error *)
      mutable baseAddress: int32 option
      mutable checksumAlgorithm: HashAlgorithm
#if DEBUG
      mutable showOptimizationData: bool
#endif
      mutable showTerms: bool (* show terms between passes? *)
      mutable writeTermsToFiles: bool (* show terms to files? *)
      mutable doDetuple: bool (* run detuple pass? *)
      mutable doTLR: bool (* run TLR pass? *)
      mutable doFinalSimplify: bool (* do final simplification pass *)
      mutable optsOn: bool (* optimizations are turned on *)
      mutable optSettings: Optimizer.OptimizationSettings
      mutable emitTailcalls: bool
      mutable deterministic: bool
      mutable concurrentBuild: bool
      mutable preferredUiLang: string option
      mutable lcid: int option
      mutable productNameForBannerText: string
      /// show the MS (c) notice, e.g. with help or fsi?
      mutable showBanner: bool

      /// show times between passes?
      mutable showTimes: bool
      mutable showLoadedAssemblies: bool
      mutable continueAfterParseFailure: bool
#if !NO_EXTENSIONTYPING
      /// show messages about extension type resolution?
      mutable showExtensionTypeMessages: bool
#endif

      /// pause between passes?
      mutable pause: bool
      /// whenever possible, emit callvirt instead of call
      mutable alwaysCallVirt: bool

      /// if true, strip away data that would not be of use to end users, but is useful to us for debugging
      // REVIEW: "stripDebugData"?
      mutable noDebugData: bool

      /// if true, indicates all type checking and code generation is in the context of fsi.exe
      isInteractive: bool
      isInvalidationSupported: bool

      /// used to log sqm data

      /// if true - every expression in quotations will be augmented with full debug info (filename, location in file)
      mutable emitDebugInfoInQuotations: bool

      mutable exename: string option

      // If true - the compiler will copy FSharp.Core.dll along the produced binaries
      mutable copyFSharpCore: CopyFSharpCoreFlag

      /// When false FSI will lock referenced assemblies requiring process restart, false = disable Shadow Copy false (*default*)
      mutable shadowCopyReferences: bool
      mutable useSdkRefs: bool
      mutable fxResolver: FxResolver option

      /// specify the error range for FxResolver
      rangeForErrors: range

      /// Override the SDK directory used by FxResolver, used for FCS only
      sdkDirOverride: string option

      /// A function to call to try to get an object that acts as a snapshot of the metadata section of a .NET binary,
      /// and from which we can read the metadata. Only used when metadataOnly=true.
      mutable tryGetMetadataSnapshot: ILReaderTryGetMetadataSnapshot

      mutable internalTestSpanStackReferring: bool

      mutable noConditionalErasure: bool

      mutable pathMap: PathMap

      mutable langVersion: LanguageVersion

      mutable xmlDocInfoLoader: IXmlDocumentationInfoLoader option
      }


    // Directories to start probing in
    // Algorithm:
    //  Search for native libraries using:
    //  1. Include directories
    //  2. compilerToolPath directories
    //  3. reference dll's
    //  4. The implicit include directory
    //
    // NOTE: it is important this is a delayed IEnumerable sequence. It is recomputed
    // each time a resolution happens and additional paths may be added as a result.
    member tcConfigB.GetNativeProbingRoots () =
        seq {
            yield! tcConfigB.includes
            yield! tcConfigB.compilerToolPaths
            yield! (tcConfigB.referencedDLLs |> Seq.map(fun ref -> Path.GetDirectoryName(ref.Text)))
            yield tcConfigB.implicitIncludeDir
        }
        |> Seq.distinct

    static member CreateNew(legacyReferenceResolver,
                            defaultFSharpBinariesDir,
                            reduceMemoryUsage,
                            implicitIncludeDir,
                            isInteractive,
                            isInvalidationSupported,
                            defaultCopyFSharpCore,
                            tryGetMetadataSnapshot,
                            sdkDirOverride,
                            rangeForErrors) =

        if (String.IsNullOrEmpty defaultFSharpBinariesDir) then
            failwith "Expected a valid defaultFSharpBinariesDir"

        // These are all default values, many can be overridden using the command line switch
        {
          primaryAssembly = PrimaryAssembly.Mscorlib
          light = None
          noFeedback = false
          stackReserveSize = None
          conditionalCompilationDefines = []
          openDebugInformationForLaterStaticLinking = false
          compilingFslib = false
          useIncrementalBuilder = false
          implicitOpens = []
          includes = []
          resolutionEnvironment = LegacyResolutionEnvironment.EditingOrCompilation false
          framework = true
          implicitlyResolveAssemblies = true
          compilerToolPaths = []
          referencedDLLs = []
          packageManagerLines = Map.empty
          projectReferences = []
          knownUnresolvedReferences = []
          loadedSources = []
          errorSeverityOptions = FSharpDiagnosticOptions.Default
          embedResources = []
          inputCodePage = None
          subsystemVersion = 4, 0 // per spec for 357994
          useHighEntropyVA = false
          mlCompatibility = false
          checkOverflow = false
          showReferenceResolutions = false
          outputDir = None
          outputFile = None
          platform = None
          prefer32Bit = false
          useSimpleResolution = runningOnMono
          target = CompilerTarget.ConsoleExe
          debuginfo = false
          testFlagEmitFeeFeeAs100001 = false
          dumpDebugInfo = false
          debugSymbolFile = None

          (* Backend configuration *)
          typeCheckOnly = false
          parseOnly = false
          importAllReferencesOnly = false
          simulateException = None
          printAst = false
          tokenizeOnly = false
          testInteractionParser = false
          reportNumDecls = false
          printSignature = false
          printSignatureFile = ""
          printAllSignatureFiles = false
          xmlDocOutputFile = None
          stats = false
          generateFilterBlocks = false (* don't generate filter blocks *)

          signer = None
          container = None
          maxErrors = 100
          abortOnError = false
          baseAddress = None
          checksumAlgorithm = HashAlgorithm.Sha256

          delaysign = false
          publicsign = false
          version = VersionNone
          metadataVersion = None
          standalone = false
          extraStaticLinkRoots = []
          noSignatureData = false
          onlyEssentialOptimizationData = false
          useOptimizationDataFile = false
          jitTracking = true
          portablePDB = true
          embeddedPDB = false
          embedAllSource = false
          embedSourceList = []
          sourceLink = ""
          ignoreSymbolStoreSequencePoints = false
          internConstantStrings = true
          extraOptimizationIterations = 0

          win32icon = ""
          win32res = ""
          win32manifest = ""
          includewin32manifest = true
          linkResources = []
          showFullPaths = false
          errorStyle = ErrorStyle.DefaultErrors

          utf8output = false
          flatErrors = false

 #if DEBUG
          showOptimizationData = false
 #endif
          showTerms = false
          writeTermsToFiles = false

          doDetuple = false
          doTLR = false
          doFinalSimplify = false
          optsOn = false
          optSettings = Optimizer.OptimizationSettings.Defaults
          emitTailcalls = true
          deterministic = false
          concurrentBuild = true
          preferredUiLang = None
          lcid = None
          productNameForBannerText = FSharpProductName
          showBanner = true
          showTimes = false
          showLoadedAssemblies = false
          continueAfterParseFailure = false
#if !NO_EXTENSIONTYPING
          showExtensionTypeMessages = false
#endif
          pause = false
          alwaysCallVirt = true
          noDebugData = false
          emitDebugInfoInQuotations = false
          exename = None
          shadowCopyReferences = false
          useSdkRefs = true
          fxResolver = None
          internalTestSpanStackReferring = false
          noConditionalErasure = false
          pathMap = PathMap.empty
          langVersion = LanguageVersion("default")
          implicitIncludeDir = implicitIncludeDir
          defaultFSharpBinariesDir = defaultFSharpBinariesDir
          reduceMemoryUsage = reduceMemoryUsage
          legacyReferenceResolver = legacyReferenceResolver
          isInteractive = isInteractive
          isInvalidationSupported = isInvalidationSupported
          copyFSharpCore = defaultCopyFSharpCore
          tryGetMetadataSnapshot = tryGetMetadataSnapshot
          useFsiAuxLib = isInteractive
          rangeForErrors = rangeForErrors
          sdkDirOverride = sdkDirOverride
          xmlDocInfoLoader = None
        }

    member tcConfigB.FxResolver =
        // We compute the FxResolver on-demand.  It depends on some configuration parameters
        // which may be later adjusted.
        match tcConfigB.fxResolver with
        | None ->
            let useDotNetFramework = (tcConfigB.primaryAssembly = PrimaryAssembly.Mscorlib)
            let fxResolver = FxResolver(useDotNetFramework, tcConfigB.implicitIncludeDir, rangeForErrors=tcConfigB.rangeForErrors, useSdkRefs=tcConfigB.useSdkRefs, isInteractive=tcConfigB.isInteractive, sdkDirOverride=tcConfigB.sdkDirOverride)
            tcConfigB.fxResolver <- Some fxResolver
            fxResolver
        | Some fxResolver -> fxResolver

    member tcConfigB.SetPrimaryAssembly primaryAssembly =
        tcConfigB.primaryAssembly <- primaryAssembly
        tcConfigB.fxResolver <- None // this needs to be recreated when the primary assembly changes

    member tcConfigB.SetUseSdkRefs useSdkRefs =
        tcConfigB.useSdkRefs <- useSdkRefs
        tcConfigB.fxResolver <- None // this needs to be recreated when the primary assembly changes

    member tcConfigB.ResolveSourceFile(m, nm, pathLoadedFrom) =
        use unwindBuildPhase = PushThreadBuildPhaseUntilUnwind BuildPhase.Parameter
        ResolveFileUsingPaths(tcConfigB.includes @ [pathLoadedFrom], m, nm)

    /// Decide names of output file, pdb and assembly
    member tcConfigB.DecideNames (sourceFiles) =
        use unwindBuildPhase = PushThreadBuildPhaseUntilUnwind BuildPhase.Parameter
        if sourceFiles = [] then errorR(Error(FSComp.SR.buildNoInputsSpecified(), rangeCmdArgs))
        let ext() = match tcConfigB.target with CompilerTarget.Dll -> ".dll" | CompilerTarget.Module -> ".netmodule" | CompilerTarget.ConsoleExe | CompilerTarget.WinExe -> ".exe"
        let implFiles = sourceFiles |> List.filter (fun lower -> List.exists (FileSystemUtils.checkSuffix (String.lowercase lower)) FSharpImplFileSuffixes)
        let outfile =
            match tcConfigB.outputFile, List.rev implFiles with
            | None, [] -> "out" + ext()
            | None, h :: _ ->
                let basic = FileSystemUtils.fileNameOfPath h
                let modname = try FileSystemUtils.chopExtension basic with _ -> basic
                modname+(ext())
            | Some f, _ -> f
        let assemblyName =
            let baseName = FileSystemUtils.fileNameOfPath outfile
            (FileSystemUtils.fileNameWithoutExtension baseName)

        let pdbfile =
            if tcConfigB.debuginfo then
              Some (match tcConfigB.debugSymbolFile with
                    | None -> FSharp.Compiler.AbstractIL.ILPdbWriter.getDebugFileName outfile tcConfigB.portablePDB
#if ENABLE_MONO_SUPPORT
                    | Some _ when runningOnMono ->
                        // On Mono, the name of the debug file has to be "<assemblyname>.mdb" so specifying it explicitly is an error
                        warning(Error(FSComp.SR.ilwriteMDBFileNameCannotBeChangedWarning(), rangeCmdArgs))
                        FSharp.Compiler.AbstractIL.ILPdbWriter.getDebugFileName outfile tcConfigB.portablePDB
#endif
                    | Some f -> f)
            elif (tcConfigB.debugSymbolFile <> None) && (not (tcConfigB.debuginfo)) then
                error(Error(FSComp.SR.buildPdbRequiresDebug(), rangeStartup))
            else
                None
        tcConfigB.outputFile <- Some outfile
        outfile, pdbfile, assemblyName

    member tcConfigB.TurnWarningOff(m, s: string) =
        use unwindBuildPhase = PushThreadBuildPhaseUntilUnwind BuildPhase.Parameter
        match GetWarningNumber(m, s) with
        | None -> ()
        | Some n ->
            // nowarn:62 turns on mlCompatibility, e.g. shows ML compat items in intellisense menus
            if n = 62 then tcConfigB.mlCompatibility <- true
            tcConfigB.errorSeverityOptions <-
                { tcConfigB.errorSeverityOptions with WarnOff = ListSet.insert (=) n tcConfigB.errorSeverityOptions.WarnOff }

    member tcConfigB.TurnWarningOn(m, s: string) =
        use unwindBuildPhase = PushThreadBuildPhaseUntilUnwind BuildPhase.Parameter
        match GetWarningNumber(m, s) with
        | None -> ()
        | Some n ->
            // warnon 62 turns on mlCompatibility, e.g. shows ML compat items in intellisense menus
            if n = 62 then tcConfigB.mlCompatibility <- false
            tcConfigB.errorSeverityOptions <-
                { tcConfigB.errorSeverityOptions with WarnOn = ListSet.insert (=) n tcConfigB.errorSeverityOptions.WarnOn }

    member tcConfigB.AddIncludePath (m, path, pathIncludedFrom) =
        let absolutePath = ComputeMakePathAbsolute pathIncludedFrom path
        let ok =
            let existsOpt =
                try Some(FileSystem.DirectoryExistsShim absolutePath)
                with e -> warning(Error(FSComp.SR.buildInvalidSearchDirectory path, m)); None
            match existsOpt with
            | Some exists ->
                if not exists then warning(Error(FSComp.SR.buildSearchDirectoryNotFound absolutePath, m))
                exists
            | None -> false
        if ok && not (List.contains absolutePath tcConfigB.includes) then
           tcConfigB.includes <- tcConfigB.includes ++ absolutePath

    member tcConfigB.AddLoadedSource(m, originalPath, pathLoadedFrom) =
        if FileSystem.IsInvalidPathShim originalPath then
            warning(Error(FSComp.SR.buildInvalidFilename originalPath, m))
        else
            let path =
                match TryResolveFileUsingPaths(tcConfigB.includes @ [pathLoadedFrom], m, originalPath) with
                | Some path -> path
                | None ->
                        // File doesn't exist in the paths. Assume it will be in the load-ed from directory.
                        ComputeMakePathAbsolute pathLoadedFrom originalPath
            if not (List.contains path (List.map (fun (_, _, path) -> path) tcConfigB.loadedSources)) then
                tcConfigB.loadedSources <- tcConfigB.loadedSources ++ (m, originalPath, path)

    member tcConfigB.AddEmbeddedSourceFile (file) =
        tcConfigB.embedSourceList <- tcConfigB.embedSourceList ++ file

    member tcConfigB.AddEmbeddedResource filename =
        tcConfigB.embedResources <- tcConfigB.embedResources ++ filename

    member tcConfigB.AddCompilerToolsByPath (path) =
        if not (tcConfigB.compilerToolPaths  |> List.exists (fun text -> path = text)) then // NOTE: We keep same paths if range is different.
            let compilerToolPath = tcConfigB.compilerToolPaths |> List.tryPick (fun text -> if text = path then Some text else None)
            if compilerToolPath.IsNone then
                tcConfigB.compilerToolPaths <- tcConfigB.compilerToolPaths ++ path

    member tcConfigB.AddReferencedAssemblyByPath (m, path) =
        if FileSystem.IsInvalidPathShim path then
            warning(Error(FSComp.SR.buildInvalidAssemblyName(path), m))
        elif not (tcConfigB.referencedDLLs |> List.exists (fun ar2 -> Range.equals m ar2.Range && path=ar2.Text)) then // NOTE: We keep same paths if range is different.
             let projectReference = tcConfigB.projectReferences |> List.tryPick (fun pr -> if pr.FileName = path then Some pr else None)
             tcConfigB.referencedDLLs <- tcConfigB.referencedDLLs ++ AssemblyReference(m, path, projectReference)

    member tcConfigB.AddDependencyManagerText (packageManager: IDependencyManagerProvider, lt, m, path: string) =
        tcConfigB.packageManagerLines <- PackageManagerLine.AddLineWithKey packageManager.Key lt path m tcConfigB.packageManagerLines

    member tcConfigB.AddReferenceDirective (dependencyProvider: DependencyProvider, m, path: string, directive) =
        let output = tcConfigB.outputDir |> Option.defaultValue ""

        let reportError =
            ResolvingErrorReport (fun errorType err msg ->
                let error = err, msg
                match errorType with
                | ErrorReportType.Warning -> warning(Error(error, m))
                | ErrorReportType.Error -> errorR(Error(error, m)))

        let dm = dependencyProvider.TryFindDependencyManagerInPath(tcConfigB.compilerToolPaths, output , reportError, path)

        match dm with
        | _, dependencyManager when not(isNull dependencyManager) ->
            if tcConfigB.langVersion.SupportsFeature(LanguageFeature.PackageManagement) then
                tcConfigB.AddDependencyManagerText (dependencyManager, directive, m, path)
            else
                errorR(Error(FSComp.SR.packageManagementRequiresVFive(), m))

        | _, _ when directive = Directive.Include ->
            errorR(Error(FSComp.SR.poundiNotSupportedByRegisteredDependencyManagers(), m))

        // #r "Assembly"
        | path, _ ->
            tcConfigB.AddReferencedAssemblyByPath (m, path)

    member tcConfigB.RemoveReferencedAssemblyByPath (m, path) =
        tcConfigB.referencedDLLs <- tcConfigB.referencedDLLs |> List.filter (fun ar -> not (Range.equals ar.Range m) || ar.Text <> path)

    member tcConfigB.AddPathMapping (oldPrefix, newPrefix) =
        tcConfigB.pathMap <- tcConfigB.pathMap |> PathMap.addMapping oldPrefix newPrefix

    static member SplitCommandLineResourceInfo (ri: string) =
        let p = ri.IndexOf ','
        if p <> -1 then
            let file = String.sub ri 0 p
            let rest = String.sub ri (p+1) (String.length ri - p - 1)
            let p = rest.IndexOf ','
            if p <> -1 then
                let name = String.sub rest 0 p+".resources"
                let pubpri = String.sub rest (p+1) (rest.Length - p - 1)
                if pubpri = "public" then file, name, ILResourceAccess.Public
                elif pubpri = "private" then file, name, ILResourceAccess.Private
                else error(Error(FSComp.SR.buildInvalidPrivacy pubpri, rangeStartup))
            else
                file, rest, ILResourceAccess.Public
        else
            ri, FileSystemUtils.fileNameOfPath ri, ILResourceAccess.Public


//----------------------------------------------------------------------------
// TcConfig
//--------------------------------------------------------------------------

[<Sealed>]
/// This type is immutable and must be kept as such. Do not extract or mutate the underlying data except by cloning it.
type TcConfig private (data: TcConfigBuilder, validate: bool) =

    // Validate the inputs - this helps ensure errors in options are shown in visual studio rather than only when built
    // However we only validate a minimal number of options at the moment
    do if validate then try data.version.GetVersionInfo(data.implicitIncludeDir) |> ignore with e -> errorR e

    // clone the input builder to ensure nobody messes with it.
    let data = { data with pause = data.pause }

    let computeKnownDllReference libraryName =
        let defaultCoreLibraryReference = AssemblyReference(range0, libraryName+".dll", None)
        let nameOfDll(r: AssemblyReference) =
            let filename = ComputeMakePathAbsolute data.implicitIncludeDir r.Text
            if FileSystem.FileExistsShim filename then
                r, Some filename
            else
                // If the file doesn't exist, let reference resolution logic report the error later...
                defaultCoreLibraryReference, if Range.equals r.Range rangeStartup then Some(filename) else None
        match data.referencedDLLs |> List.filter (fun assemblyReference -> assemblyReference.SimpleAssemblyNameIs libraryName) with
        | [] -> defaultCoreLibraryReference, None
        | [r]
        | r :: _ -> nameOfDll r

    // Look for an explicit reference to mscorlib/netstandard.dll or System.Runtime.dll and use that to compute clrRoot and targetFrameworkVersion
    let primaryAssemblyReference, primaryAssemblyExplicitFilenameOpt = computeKnownDllReference(data.primaryAssembly.Name)
    let fslibReference =
        // Look for explicit FSharp.Core reference otherwise use version that was referenced by compiler
        let dllReference, fileNameOpt = computeKnownDllReference getFSharpCoreLibraryName
        match fileNameOpt with
        | Some _ -> dllReference
        | None -> AssemblyReference(range0, getDefaultFSharpCoreLocation(), None)

    // clrRoot: the location of the primary assembly (mscorlib.dll or netstandard.dll or System.Runtime.dll)
    //
    // targetFrameworkVersionValue: Normally just HighestInstalledNetFrameworkVersion()
    //
    // Note, when mscorlib.dll has been given explicitly the actual value of
    // targetFrameworkVersion shouldn't matter since resolution has already happened.
    // In those cases where it does matter (e.g. --noframework is not being used or we are processing further
    // resolutions for a script) then it is correct to just use HighestInstalledNetFrameworkVersion().
    let clrRootValue, targetFrameworkVersionValue =
        match primaryAssemblyExplicitFilenameOpt with
        | Some primaryAssemblyFilename ->
            let filename = ComputeMakePathAbsolute data.implicitIncludeDir primaryAssemblyFilename
            try
                let clrRoot = Some(Path.GetDirectoryName(FileSystem.GetFullPathShim filename))
                clrRoot, data.legacyReferenceResolver.Impl.HighestInstalledNetFrameworkVersion()
            with e ->
                // We no longer expect the above to fail but leaving this just in case
                error(Error(FSComp.SR.buildErrorOpeningBinaryFile(filename, e.Message), rangeStartup))
        | None ->
#if !ENABLE_MONO_SUPPORT
            // TODO: we have to get msbuild out of this
            if data.useSimpleResolution then
                None, ""
            else
#endif
                None, data.legacyReferenceResolver.Impl.HighestInstalledNetFrameworkVersion()

    member x.FxResolver = data.FxResolver
    member x.primaryAssembly = data.primaryAssembly
    member x.noFeedback = data.noFeedback
    member x.stackReserveSize = data.stackReserveSize
    member x.implicitIncludeDir = data.implicitIncludeDir
    member x.openDebugInformationForLaterStaticLinking = data.openDebugInformationForLaterStaticLinking
    member x.fsharpBinariesDir = data.defaultFSharpBinariesDir
    member x.compilingFslib = data.compilingFslib
    member x.useIncrementalBuilder = data.useIncrementalBuilder
    member x.includes = data.includes
    member x.implicitOpens = data.implicitOpens
    member x.useFsiAuxLib = data.useFsiAuxLib
    member x.framework = data.framework
    member x.implicitlyResolveAssemblies = data.implicitlyResolveAssemblies
    member x.resolutionEnvironment = data.resolutionEnvironment
    member x.light = data.light
    member x.conditionalCompilationDefines = data.conditionalCompilationDefines
    member x.loadedSources = data.loadedSources
    member x.compilerToolPaths = data.compilerToolPaths
    member x.referencedDLLs = data.referencedDLLs
    member x.knownUnresolvedReferences = data.knownUnresolvedReferences
    member x.clrRoot = clrRootValue
    member x.reduceMemoryUsage = data.reduceMemoryUsage
    member x.subsystemVersion = data.subsystemVersion
    member x.useHighEntropyVA = data.useHighEntropyVA
    member x.inputCodePage = data.inputCodePage
    member x.embedResources = data.embedResources
    member x.errorSeverityOptions = data.errorSeverityOptions
    member x.mlCompatibility = data.mlCompatibility
    member x.checkOverflow = data.checkOverflow
    member x.showReferenceResolutions = data.showReferenceResolutions
    member x.outputDir = data.outputDir
    member x.outputFile = data.outputFile
    member x.platform = data.platform
    member x.prefer32Bit = data.prefer32Bit
    member x.useSimpleResolution = data.useSimpleResolution
    member x.target = data.target
    member x.debuginfo = data.debuginfo
    member x.testFlagEmitFeeFeeAs100001 = data.testFlagEmitFeeFeeAs100001
    member x.dumpDebugInfo = data.dumpDebugInfo
    member x.debugSymbolFile = data.debugSymbolFile
    member x.typeCheckOnly = data.typeCheckOnly
    member x.parseOnly = data.parseOnly
    member x.importAllReferencesOnly = data.importAllReferencesOnly
    member x.simulateException = data.simulateException
    member x.printAst = data.printAst
    member x.targetFrameworkVersion = targetFrameworkVersionValue
    member x.tokenizeOnly = data.tokenizeOnly
    member x.testInteractionParser = data.testInteractionParser
    member x.reportNumDecls = data.reportNumDecls
    member x.printSignature = data.printSignature
    member x.printSignatureFile = data.printSignatureFile
    member x.printAllSignatureFiles = data.printAllSignatureFiles
    member x.xmlDocOutputFile = data.xmlDocOutputFile
    member x.stats = data.stats
    member x.generateFilterBlocks = data.generateFilterBlocks
    member x.signer = data.signer
    member x.container = data.container
    member x.delaysign = data.delaysign
    member x.publicsign = data.publicsign
    member x.version = data.version
    member x.metadataVersion = data.metadataVersion
    member x.standalone = data.standalone
    member x.extraStaticLinkRoots = data.extraStaticLinkRoots
    member x.noSignatureData = data.noSignatureData
    member x.onlyEssentialOptimizationData = data.onlyEssentialOptimizationData
    member x.useOptimizationDataFile = data.useOptimizationDataFile
    member x.jitTracking = data.jitTracking
    member x.portablePDB = data.portablePDB
    member x.embeddedPDB = data.embeddedPDB
    member x.embedAllSource = data.embedAllSource
    member x.embedSourceList = data.embedSourceList
    member x.sourceLink = data.sourceLink
    member x.packageManagerLines  = data.packageManagerLines
    member x.ignoreSymbolStoreSequencePoints = data.ignoreSymbolStoreSequencePoints
    member x.internConstantStrings = data.internConstantStrings
    member x.extraOptimizationIterations = data.extraOptimizationIterations
    member x.win32icon = data.win32icon
    member x.win32res = data.win32res
    member x.win32manifest = data.win32manifest
    member x.includewin32manifest = data.includewin32manifest
    member x.linkResources = data.linkResources
    member x.showFullPaths = data.showFullPaths
    member x.errorStyle = data.errorStyle
    member x.utf8output = data.utf8output
    member x.flatErrors = data.flatErrors
    member x.maxErrors = data.maxErrors
    member x.baseAddress = data.baseAddress
    member x.checksumAlgorithm = data.checksumAlgorithm
 #if DEBUG
    member x.showOptimizationData = data.showOptimizationData
#endif
    member x.showTerms = data.showTerms
    member x.writeTermsToFiles = data.writeTermsToFiles
    member x.doDetuple = data.doDetuple
    member x.doTLR = data.doTLR
    member x.doFinalSimplify = data.doFinalSimplify
    member x.optSettings = data.optSettings
    member x.emitTailcalls = data.emitTailcalls
    member x.deterministic = data.deterministic
    member x.concurrentBuild = data.concurrentBuild
    member x.pathMap = data.pathMap
    member x.langVersion = data.langVersion
    member x.preferredUiLang = data.preferredUiLang
    member x.lcid = data.lcid
    member x.optsOn = data.optsOn
    member x.productNameForBannerText = data.productNameForBannerText
    member x.showBanner = data.showBanner
    member x.showTimes = data.showTimes
    member x.showLoadedAssemblies = data.showLoadedAssemblies
    member x.continueAfterParseFailure = data.continueAfterParseFailure
#if !NO_EXTENSIONTYPING
    member x.showExtensionTypeMessages = data.showExtensionTypeMessages
#endif
    member x.pause = data.pause
    member x.alwaysCallVirt = data.alwaysCallVirt
    member x.noDebugData = data.noDebugData
    member x.isInteractive = data.isInteractive
    member x.isInvalidationSupported = data.isInvalidationSupported
    member x.emitDebugInfoInQuotations = data.emitDebugInfoInQuotations
    member x.copyFSharpCore = data.copyFSharpCore
    member x.shadowCopyReferences = data.shadowCopyReferences
    member x.useSdkRefs = data.useSdkRefs
    member x.sdkDirOverride = data.sdkDirOverride
    member x.tryGetMetadataSnapshot = data.tryGetMetadataSnapshot
    member x.internalTestSpanStackReferring = data.internalTestSpanStackReferring
    member x.noConditionalErasure = data.noConditionalErasure
    member x.xmlDocInfoLoader = data.xmlDocInfoLoader

    static member Create(builder, validate) =
        use unwindBuildPhase = PushThreadBuildPhaseUntilUnwind BuildPhase.Parameter
        TcConfig(builder, validate)

    member x.legacyReferenceResolver = data.legacyReferenceResolver

    member tcConfig.CloneToBuilder() =
        { data with conditionalCompilationDefines=data.conditionalCompilationDefines }

    member tcConfig.ComputeCanContainEntryPoint(sourceFiles: string list) =
        let n = sourceFiles.Length in
        (sourceFiles |> List.mapi (fun i _ -> (i = n-1)), tcConfig.target.IsExe)

    // This call can fail if no CLR is found (this is the path to mscorlib)
    member tcConfig.GetTargetFrameworkDirectories() =
        use unwindBuildPhase = PushThreadBuildPhaseUntilUnwind BuildPhase.Parameter
        try
          [
            // Check if we are given an explicit framework root - if so, use that
            match tcConfig.clrRoot with
            | Some x ->
                let clrRoot = tcConfig.MakePathAbsolute x
                yield clrRoot
                let clrFacades = Path.Combine(clrRoot, "Facades")
                if FileSystem.DirectoryExistsShim(clrFacades) then yield clrFacades

            | None ->
// "there is no really good notion of runtime directory on .NETCore"
#if NETSTANDARD
                let runtimeRoot = Path.GetDirectoryName(typeof<System.Object>.Assembly.Location)
#else
                let runtimeRoot = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory()
#endif
                let runtimeRootWithoutSlash = runtimeRoot.TrimEnd('/', '\\')
                let runtimeRootFacades = Path.Combine(runtimeRootWithoutSlash, "Facades")
                let runtimeRootWPF = Path.Combine(runtimeRootWithoutSlash, "WPF")

                match tcConfig.resolutionEnvironment with
                | LegacyResolutionEnvironment.CompilationAndEvaluation ->
                    // Default compilation-and-execution-time references on .NET Framework and Mono, e.g. for F# Interactive
                    //
                    // In the current way of doing things, F# Interactive refers to implementation assemblies.
                    yield runtimeRoot
                    if FileSystem.DirectoryExistsShim runtimeRootFacades then
                        yield runtimeRootFacades // System.Runtime.dll is in /usr/lib/mono/4.5/Facades
                    if FileSystem.DirectoryExistsShim runtimeRootWPF then
                        yield runtimeRootWPF // PresentationCore.dll is in C:\Windows\Microsoft.NET\Framework\v4.0.30319\WPF

                    match tcConfig.FxResolver.GetFrameworkRefsPackDirectory() with
                    | Some path when FileSystem.DirectoryExistsShim(path) ->
                        yield path
                    | _ -> ()

                | LegacyResolutionEnvironment.EditingOrCompilation _ ->
#if ENABLE_MONO_SUPPORT
                    if runningOnMono then
                        // Default compilation-time references on Mono
                        //
                        // On Mono, the default references come from the implementation assemblies.
                        // This is because we have had trouble reliably using MSBuild APIs to compute DotNetFrameworkReferenceAssembliesRootDirectory on Mono.
                        yield runtimeRoot
                        if FileSystem.DirectoryExistsShim runtimeRootFacades then
                            yield runtimeRootFacades // System.Runtime.dll is in /usr/lib/mono/4.5/Facades
                        if FileSystem.DirectoryExistsShim runtimeRootWPF then
                            yield runtimeRootWPF // PresentationCore.dll is in C:\Windows\Microsoft.NET\Framework\v4.0.30319\WPF
                        // On Mono we also add a default reference to the 4.5-api and 4.5-api/Facades directories.
                        let runtimeRootApi = runtimeRootWithoutSlash + "-api"
                        let runtimeRootApiFacades = Path.Combine(runtimeRootApi, "Facades")
                        if FileSystem.DirectoryExistsShim runtimeRootApi then
                            yield runtimeRootApi
                        if FileSystem.DirectoryExistsShim runtimeRootApiFacades then
                             yield runtimeRootApiFacades
                    else
#endif
                        // Default compilation-time references on .NET Framework
                        //
                        // This is the normal case for "fsc.exe a.fs". We refer to the reference assemblies folder.
                        let frameworkRoot = tcConfig.legacyReferenceResolver.Impl.DotNetFrameworkReferenceAssembliesRootDirectory
                        let frameworkRootVersion = Path.Combine(frameworkRoot, tcConfig.targetFrameworkVersion)
                        yield frameworkRootVersion
                        let facades = Path.Combine(frameworkRootVersion, "Facades")
                        if FileSystem.DirectoryExistsShim facades then
                            yield facades
                        match tcConfig.FxResolver.GetFrameworkRefsPackDirectory() with
                        | Some path when FileSystem.DirectoryExistsShim(path) ->
                            yield path
                        | _ -> ()
                  ]
        with e ->
            errorRecovery e range0; []

    member tcConfig.ComputeLightSyntaxInitialStatus filename =
        use unwindBuildPhase = PushThreadBuildPhaseUntilUnwind BuildPhase.Parameter
        let lower = String.lowercase filename
        let lightOnByDefault = List.exists (FileSystemUtils.checkSuffix lower) FSharpLightSyntaxFileSuffixes
        if lightOnByDefault then (tcConfig.light <> Some false) else (tcConfig.light = Some true )

    member tcConfig.GetAvailableLoadedSources() =
        use unwindBuildPhase = PushThreadBuildPhaseUntilUnwind BuildPhase.Parameter
        let resolveLoadedSource (m, originalPath, path) =
            try
                if not(FileSystem.FileExistsShim(path)) then
                    let secondTrial =
                        tcConfig.includes
                        |> List.tryPick (fun root ->
                            let path = ComputeMakePathAbsolute root originalPath
                            if FileSystem.FileExistsShim(path) then Some path else None)

                    match secondTrial with
                    | Some path -> Some(m,path)
                    | None ->
                        error(LoadedSourceNotFoundIgnoring(path,m))
                        None
                else Some(m,path)
            with e -> errorRecovery e m; None

        tcConfig.loadedSources
        |> List.choose resolveLoadedSource
        |> List.distinct

    // This is not the complete set of search paths, it is just the set
    // that is special to F# (as compared to MSBuild resolution)
    member tcConfig.GetSearchPathsForLibraryFiles() =
        [ yield! tcConfig.GetTargetFrameworkDirectories()
          yield! List.map (tcConfig.MakePathAbsolute) tcConfig.includes
          yield tcConfig.implicitIncludeDir
          yield tcConfig.fsharpBinariesDir ]

    member tcConfig.MakePathAbsolute path =
        let result = ComputeMakePathAbsolute tcConfig.implicitIncludeDir path
        result

    member _.ResolveSourceFile(m, filename, pathLoadedFrom) =
        data.ResolveSourceFile(m, filename, pathLoadedFrom)

    member _.PrimaryAssemblyDllReference() = primaryAssemblyReference

    member _.CoreLibraryDllReference() = fslibReference

    member _.GetNativeProbingRoots() = data.GetNativeProbingRoots()

    /// A closed set of assemblies where, for any subset S:
    ///    - the TcImports object built for S (and thus the F# Compiler CCUs for the assemblies in S)
    ///       is a resource that can be shared between any two IncrementalBuild objects that reference
    ///       precisely S
    ///
    /// Determined by looking at the set of assemblies referenced by f# .
    ///
    /// Returning true may mean that the file is locked and/or placed into the
    /// 'framework' reference set that is potentially shared across multiple compilations.
    member tcConfig.IsSystemAssembly (filename: string) =
        try
            FileSystem.FileExistsShim filename &&
            ((tcConfig.GetTargetFrameworkDirectories() |> List.exists (fun clrRoot -> clrRoot = Path.GetDirectoryName filename)) ||
             (tcConfig.FxResolver.GetSystemAssemblies().Contains (FileSystemUtils.fileNameWithoutExtension filename)) ||
             tcConfig.FxResolver.IsInReferenceAssemblyPackDirectory filename)
        with _ ->
            false

    member tcConfig.GenerateSignatureData =
        not tcConfig.standalone && not tcConfig.noSignatureData

    member tcConfig.GenerateOptimizationData =
        tcConfig.GenerateSignatureData

    member tcConfig.assumeDotNetFramework =
        tcConfig.primaryAssembly = PrimaryAssembly.Mscorlib

/// Represents a computation to return a TcConfig. Normally this is just a constant immutable TcConfig,
/// but for F# Interactive it may be based on an underlying mutable TcConfigBuilder.
type TcConfigProvider =
    | TcConfigProvider of (CompilationThreadToken -> TcConfig)
    member x.Get ctok = (let (TcConfigProvider f) = x in f ctok)

    /// Get a TcConfigProvider which will return only the exact TcConfig.
    static member Constant tcConfig = TcConfigProvider(fun _ctok -> tcConfig)

    /// Get a TcConfigProvider which will continue to respect changes in the underlying
    /// TcConfigBuilder rather than delivering snapshots.
    static member BasedOnMutableBuilder tcConfigB = TcConfigProvider(fun _ctok -> TcConfig.Create(tcConfigB, validate=false))

let GetFSharpCoreLibraryName () = getFSharpCoreLibraryName
