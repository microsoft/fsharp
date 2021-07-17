// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

// Driver for F# compiler.
//
// Roughly divides into:
//    - Parsing
//    - Flags
//    - Importing IL assemblies
//    - Compiling (including optimizing)
//    - Linking (including ILX-IL transformation)

module internal FSharp.Compiler.Driver

open System
open System.Collections.Generic
open System.Diagnostics
open System.Globalization
open System.IO
open System.Reflection
open System.Text
open System.Threading

open Internal.Utilities
open Internal.Utilities.Library
open Internal.Utilities.Library.Extras

open FSharp.Compiler
open FSharp.Compiler.AbstractIL
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AbstractIL.ILBinaryReader
open FSharp.Compiler.AccessibilityLogic
open FSharp.Compiler.CheckExpressions
open FSharp.Compiler.CheckDeclarations
open FSharp.Compiler.CompilerConfig
open FSharp.Compiler.CompilerDiagnostics
open FSharp.Compiler.CompilerImports
open FSharp.Compiler.CompilerOptions
open FSharp.Compiler.CompilerGlobalState
open FSharp.Compiler.CreateILModule
open FSharp.Compiler.DependencyManager
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.IlxGen
open FSharp.Compiler.InfoReader
open FSharp.Compiler.IO
open FSharp.Compiler.NameResolution
open FSharp.Compiler.ParseAndCheckInputs
open FSharp.Compiler.OptimizeInputs
open FSharp.Compiler.ScriptClosure
open FSharp.Compiler.Syntax
open FSharp.Compiler.Syntax.PrettyNaming
open FSharp.Compiler.StaticLinking
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Compiler.Text
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.XmlDocFileWriter
open FSharp.Compiler.BuildGraph

//----------------------------------------------------------------------------
// Reporting - warnings, errors
//----------------------------------------------------------------------------

/// An error logger that reports errors up to some maximum, notifying the exiter when that maximum is reached
[<AbstractClass>]
type ErrorLoggerUpToMaxErrors(tcConfigB: TcConfigBuilder, exiter: Exiter, nameForDebugging) =
    inherit ErrorLogger(nameForDebugging)

    let mutable errors = 0

    /// Called when an error or warning occurs
    abstract HandleIssue: tcConfigB: TcConfigBuilder * error: PhasedDiagnostic * severity: FSharpDiagnosticSeverity -> unit

    /// Called when 'too many errors' has occurred
    abstract HandleTooManyErrors: text: string -> unit

    override x.ErrorCount = errors

    override x.DiagnosticSink(err, severity) =
      if severity = FSharpDiagnosticSeverity.Error || ReportWarningAsError tcConfigB.errorSeverityOptions err then
        if errors >= tcConfigB.maxErrors then
            x.HandleTooManyErrors(FSComp.SR.fscTooManyErrors())
            exiter.Exit 1

        x.HandleIssue(tcConfigB, err, FSharpDiagnosticSeverity.Error)

        errors <- errors + 1

        match err.Exception, tcConfigB.simulateException with
        | InternalError (msg, _), None
        | Failure msg, None -> Debug.Assert(false, sprintf "Bug in compiler: %s\n%s" msg (err.Exception.ToString()))
        | :? KeyNotFoundException, None -> Debug.Assert(false, sprintf "Lookup exception in compiler: %s" (err.Exception.ToString()))
        | _ ->  ()

      elif ReportWarning tcConfigB.errorSeverityOptions err then
          x.HandleIssue(tcConfigB, err, severity)


/// Create an error logger that counts and prints errors
let ConsoleErrorLoggerUpToMaxErrors (tcConfigB: TcConfigBuilder, exiter : Exiter) =
    { new ErrorLoggerUpToMaxErrors(tcConfigB, exiter, "ConsoleErrorLoggerUpToMaxErrors") with

            member _.HandleTooManyErrors(text : string) =
                DoWithDiagnosticColor FSharpDiagnosticSeverity.Warning (fun () -> Printf.eprintfn "%s" text)

            member _.HandleIssue(tcConfigB, err, severity) =
                DoWithDiagnosticColor severity (fun () ->
                    let diag = OutputDiagnostic (tcConfigB.implicitIncludeDir, tcConfigB.showFullPaths, tcConfigB.flatErrors, tcConfigB.errorStyle, severity)
                    writeViaBuffer stderr diag err
                    stderr.WriteLine())
    } :> ErrorLogger

/// This error logger delays the messages it receives. At the end, call ForwardDelayedDiagnostics
/// to send the held messages.
type DelayAndForwardErrorLogger(exiter: Exiter, errorLoggerProvider: ErrorLoggerProvider) =
    inherit CapturingErrorLogger("DelayAndForwardErrorLogger")

    member x.ForwardDelayedDiagnostics(tcConfigB: TcConfigBuilder) =
        let errorLogger =  errorLoggerProvider.CreateErrorLoggerUpToMaxErrors(tcConfigB, exiter)
        x.CommitDelayedDiagnostics errorLogger

and [<AbstractClass>]
    ErrorLoggerProvider() =

    member this.CreateDelayAndForwardLogger exiter = DelayAndForwardErrorLogger(exiter, this)

    abstract CreateErrorLoggerUpToMaxErrors : tcConfigBuilder : TcConfigBuilder * exiter : Exiter -> ErrorLogger


/// Part of LegacyHostedCompilerForTesting
///
/// Yet another ErrorLogger implementation, capturing the messages but only up to the maxerrors maximum
type InProcErrorLoggerProvider() =
    let errors = ResizeArray()
    let warnings = ResizeArray()

    member _.Provider =
        { new ErrorLoggerProvider() with

            member log.CreateErrorLoggerUpToMaxErrors(tcConfigBuilder, exiter) =

                { new ErrorLoggerUpToMaxErrors(tcConfigBuilder, exiter, "InProcCompilerErrorLoggerUpToMaxErrors") with

                    member this.HandleTooManyErrors text =
                        warnings.Add(Diagnostic.Short(FSharpDiagnosticSeverity.Warning, text))

                    member this.HandleIssue(tcConfigBuilder, err, severity) =
                        // 'true' is passed for "suggestNames", since we want to suggest names with fsc.exe runs and this doesn't affect IDE perf
                        let diagnostics =
                            CollectDiagnostic
                                (tcConfigBuilder.implicitIncludeDir, tcConfigBuilder.showFullPaths,
                                 tcConfigBuilder.flatErrors, tcConfigBuilder.errorStyle, severity, err, true)
                        match severity with
                        | FSharpDiagnosticSeverity.Error ->
                           errors.AddRange(diagnostics)
                        | FSharpDiagnosticSeverity.Warning ->
                            warnings.AddRange(diagnostics)
                        | _ -> ()}
                :> ErrorLogger }

    member _.CapturedErrors = errors.ToArray()

    member _.CapturedWarnings = warnings.ToArray()

/// The default ErrorLogger implementation, reporting messages to the Console up to the maxerrors maximum
type ConsoleLoggerProvider() =

    inherit ErrorLoggerProvider()

    override this.CreateErrorLoggerUpToMaxErrors(tcConfigBuilder, exiter) = ConsoleErrorLoggerUpToMaxErrors(tcConfigBuilder, exiter)

/// Notify the exiter if any error has occurred
let AbortOnError (errorLogger: ErrorLogger, exiter : Exiter) =
    if errorLogger.ErrorCount > 0 then
        exiter.Exit 1

let TypeCheck (ctok, tcConfig, tcImports, tcGlobals, errorLogger: ErrorLogger, assemblyName, niceNameGen, tcEnv0, inputs, exiter: Exiter) =
    try
        if isNil inputs then error(Error(FSComp.SR.fscNoImplementationFiles(), Range.rangeStartup))
        let ccuName = assemblyName
        let tcInitialState = GetInitialTcState (rangeStartup, ccuName, tcConfig, tcGlobals, tcImports, niceNameGen, tcEnv0)
        TypeCheckClosedInputSet (ctok, (fun () -> errorLogger.ErrorCount > 0), tcConfig, tcImports, tcGlobals, None, tcInitialState, inputs)
    with e ->
        errorRecovery e rangeStartup
        exiter.Exit 1

/// Check for .fsx and, if present, compute the load closure for of #loaded files.
///
/// This is the "script compilation" feature that has always been present in the F# compiler, that allows you to compile scripts
/// and get the load closure and references from them. This applies even if the script is in a project (with 'Compile' action), for example.
///
/// Any DLL references implied by package references are also retrieved from the script.
///
/// When script compilation is invoked, the outputs are not necessarily a functioning application - the referenced DLLs are not
/// copied to the output folder, for example (except perhaps FSharp.Core.dll).
///
/// NOTE: there is similar code in IncrementalBuilder.fs and this code should really be reconciled with that
let AdjustForScriptCompile(tcConfigB: TcConfigBuilder, commandLineSourceFiles, lexResourceManager, dependencyProvider) =

    let combineFilePath file =
        try
            if FileSystem.IsPathRootedShim file then file
            else Path.Combine(tcConfigB.implicitIncludeDir, file)
        with _ ->
            error (Error(FSComp.SR.pathIsInvalid file, rangeStartup))

    let commandLineSourceFiles =
        commandLineSourceFiles
        |> List.map combineFilePath

    // Script compilation is active if the last item being compiled is a script and --noframework has not been specified
    let mutable allSources = []

    let tcConfig = TcConfig.Create(tcConfigB, validate=false)

    let AddIfNotPresent(filename: string) =
        if not(allSources |> List.contains filename) then
            allSources <- filename :: allSources

    let AppendClosureInformation filename =
        if IsScript filename then
            let closure =
                LoadClosure.ComputeClosureOfScriptFiles
                   (tcConfig, [filename, rangeStartup], CodeContext.Compilation,
                    lexResourceManager, dependencyProvider)

            // Record the new references (non-framework) references from the analysis of the script. (The full resolutions are recorded
            // as the corresponding #I paths used to resolve them are local to the scripts and not added to the tcConfigB - they are
            // added to localized clones of the tcConfigB).
            let references =
                closure.References
                |> List.collect snd
                |> List.filter (fun r -> not (Range.equals r.originalReference.Range range0) && not (Range.equals r.originalReference.Range rangeStartup))

            references |> List.iter (fun r -> tcConfigB.AddReferencedAssemblyByPath(r.originalReference.Range, r.resolvedPath))

            // Also record the other declarations from the script.
            closure.NoWarns |> List.collect (fun (n, ms) -> ms|>List.map(fun m->m, n)) |> List.iter (fun (x,m) -> tcConfigB.TurnWarningOff(x, m))
            closure.SourceFiles |> List.map fst |> List.iter AddIfNotPresent
            closure.AllRootFileDiagnostics |> List.iter diagnosticSink

            // If there is a target framework for the script then push that as a requirement into the overall compilation and add all the framework references implied
            // by the script too.
            tcConfigB.SetPrimaryAssembly (if closure.UseDesktopFramework then PrimaryAssembly.Mscorlib else PrimaryAssembly.System_Runtime)
            if tcConfigB.framework then
                let references = closure.References |> List.collect snd
                references |> List.iter (fun r -> tcConfigB.AddReferencedAssemblyByPath(r.originalReference.Range, r.resolvedPath))

        else AddIfNotPresent filename

    // Find closure of .fsx files.
    commandLineSourceFiles |> List.iter AppendClosureInformation

    List.rev allSources

let SetProcessThreadLocals tcConfigB =
    match tcConfigB.preferredUiLang with
    | Some s -> Thread.CurrentThread.CurrentUICulture <- new CultureInfo(s)
    | None -> ()
    if tcConfigB.utf8output then
        Console.OutputEncoding <- Encoding.UTF8

let ProcessCommandLineFlags (tcConfigB: TcConfigBuilder, lcidFromCodePage, argv) =
    let mutable inputFilesRef = []
    let collect name =
        let lower = String.lowercase name
        if List.exists (FileSystemUtils.checkSuffix lower) [".resx"]  then
            error(Error(FSComp.SR.fscResxSourceFileDeprecated name, rangeStartup))
        else
            inputFilesRef <- name :: inputFilesRef
    let abbrevArgs = GetAbbrevFlagSet tcConfigB true

    // This is where flags are interpreted by the command line fsc.exe.
    ParseCompilerOptions (collect, GetCoreFscCompilerOptions tcConfigB, List.tail (PostProcessCompilerArgs abbrevArgs argv))

    if not (tcConfigB.portablePDB || tcConfigB.embeddedPDB) then
        if tcConfigB.embedAllSource || (tcConfigB.embedSourceList |> isNil |> not) then
            error(Error(FSComp.SR.optsEmbeddedSourceRequirePortablePDBs(), rangeCmdArgs))
        if not (String.IsNullOrEmpty(tcConfigB.sourceLink)) then
            error(Error(FSComp.SR.optsSourceLinkRequirePortablePDBs(), rangeCmdArgs))

    if tcConfigB.debuginfo && not tcConfigB.portablePDB then
        if tcConfigB.deterministic then
            error(Error(FSComp.SR.fscDeterministicDebugRequiresPortablePdb(), rangeCmdArgs))

        if tcConfigB.pathMap <> PathMap.empty then
            error(Error(FSComp.SR.fscPathMapDebugRequiresPortablePdb(), rangeCmdArgs))

    let inputFiles = List.rev inputFilesRef

    // Check if we have a codepage from the console
    match tcConfigB.lcid with
    | Some _ -> ()
    | None -> tcConfigB.lcid <- lcidFromCodePage

    SetProcessThreadLocals tcConfigB

    (* step - get dll references *)
    let dllFiles, sourceFiles = inputFiles |> List.map(fun p -> FileSystemUtils.trimQuotes p) |> List.partition FileSystemUtils.isDll
    match dllFiles with
    | [] -> ()
    | h :: _ -> errorR (Error(FSComp.SR.fscReferenceOnCommandLine h, rangeStartup))

    dllFiles |> List.iter (fun f->tcConfigB.AddReferencedAssemblyByPath(rangeStartup, f))
    sourceFiles

/// Write a .fsi file for the --sig option
module InterfaceFileWriter =
    let WriteInterfaceFile (tcGlobals, tcConfig: TcConfig, infoReader, declaredImpls: TypedImplFile list) =
        // there are two modes here:
        // * write one unified sig file to a given path, or
        // * write individual sig files to paths matching their impl files
        let denv = DisplayEnv.InitialForSigFileGeneration tcGlobals
        let denv = { denv with shrinkOverloads = false; printVerboseSignatures = true }

        let writeToFile os (TImplFile (_, _, mexpr, _, _, _)) =
          writeViaBuffer os (fun os s -> Printf.bprintf os "%s\n\n" s)
            (NicePrint.layoutInferredSigOfModuleExpr true denv infoReader AccessibleFromSomewhere range0 mexpr |> Display.squashTo 80 |> LayoutRender.showL)

        let writeHeader filePath os =
            if filePath <> "" && not (List.exists (FileSystemUtils.checkSuffix filePath) FSharpLightSyntaxFileSuffixes) then
                fprintfn os "#light"
                fprintfn os ""

        let writeAllToSameFile declaredImpls =
            /// Use a UTF-8 Encoding with no Byte Order Mark
            let os =
                if tcConfig.printSignatureFile = "" then
                    Console.Out
                else
                    FileSystem.OpenFileForWriteShim(tcConfig.printSignatureFile, FileMode.OpenOrCreate).GetWriter()

            writeHeader tcConfig.printSignatureFile os

            for impl in declaredImpls do
                writeToFile os impl

            if tcConfig.printSignatureFile <> "" then os.Dispose()

        let extensionForFile (filePath: string) =
            if (List.exists (FileSystemUtils.checkSuffix filePath) mlCompatSuffixes) then
                ".mli"
            else
                ".fsi"

        let writeToSeparateFiles (declaredImpls: TypedImplFile list) =
            for (TImplFile (name, _, _, _, _, _) as impl) in declaredImpls do
                let filename = System.IO.Path.ChangeExtension(name.Range.FileName, extensionForFile name.Range.FileName)
                printfn "writing impl file to %s" filename
                use os = FileSystem.OpenFileForWriteShim(filename, FileMode.OpenOrCreate).GetWriter()
                writeHeader filename os
                writeToFile os impl

        if tcConfig.printSignature then
            writeAllToSameFile declaredImpls
        else if tcConfig.printAllSignatureFiles then
            writeToSeparateFiles declaredImpls

//----------------------------------------------------------------------------
// CopyFSharpCore
//----------------------------------------------------------------------------

// If the --nocopyfsharpcore switch is not specified, this will:
// 1) Look into the referenced assemblies, if FSharp.Core.dll is specified, it will copy it to output directory.
// 2) If not, but FSharp.Core.dll exists beside the compiler binaries, it will copy it to output directory.
// 3) If not, it will produce an error.
let CopyFSharpCore(outFile: string, referencedDlls: AssemblyReference list) =
    let outDir = Path.GetDirectoryName outFile
    let fsharpCoreAssemblyName = GetFSharpCoreLibraryName() + ".dll"
    let fsharpCoreDestinationPath = Path.Combine(outDir, fsharpCoreAssemblyName)
    let copyFileIfDifferent src dest =
        if not (FileSystem.FileExistsShim dest) || (FileSystem.GetCreationTimeShim src <> FileSystem.GetCreationTimeShim dest) then
            FileSystem.CopyShim(src, dest, true)

    match referencedDlls |> Seq.tryFind (fun dll -> String.Equals(Path.GetFileName(dll.Text), fsharpCoreAssemblyName, StringComparison.CurrentCultureIgnoreCase)) with
    | Some referencedFsharpCoreDll -> copyFileIfDifferent referencedFsharpCoreDll.Text fsharpCoreDestinationPath
    | None ->
        let executionLocation =
            Assembly.GetExecutingAssembly().Location
        let compilerLocation = Path.GetDirectoryName executionLocation
        let compilerFsharpCoreDllPath = Path.Combine(compilerLocation, fsharpCoreAssemblyName)
        if FileSystem.FileExistsShim compilerFsharpCoreDllPath then
            copyFileIfDifferent compilerFsharpCoreDllPath fsharpCoreDestinationPath
        else
            errorR(Error(FSComp.SR.fsharpCoreNotFoundToBeCopied(), rangeCmdArgs))

// Try to find an AssemblyVersion attribute
let TryFindVersionAttribute g attrib attribName attribs deterministic =
    match AttributeHelpers.TryFindStringAttribute g attrib attribs with
    | Some versionString ->
         if deterministic && versionString.Contains("*") then
             errorR(Error(FSComp.SR.fscAssemblyWildcardAndDeterminism(attribName, versionString), Range.rangeStartup))
         try Some (IL.parseILVersion versionString)
         with e ->
             // Warning will be reported by CheckExpressions.fs
             None
    | _ -> None

//----------------------------------------------------------------------------
// Main phases of compilation. These are written as separate functions with explicit argument passing
// to ensure transient objects are eligible for GC and only actual required information
// is propagated.
//-----------------------------------------------------------------------------

[<NoEquality; NoComparison>]
type Args<'T> = Args  of 'T

/// First phase of compilation.
///   - Set up console encoding and code page settings
///   - Process command line, flags and collect filenames
///   - Resolve assemblies
///   - Import assemblies
///   - Parse source files
///   - Check the inputs
let main1(ctok, argv, legacyReferenceResolver, bannerAlreadyPrinted,
          reduceMemoryUsage: ReduceMemoryFlag, defaultCopyFSharpCore: CopyFSharpCoreFlag,
          exiter: Exiter, errorLoggerProvider: ErrorLoggerProvider, disposables: DisposablesTracker) =

    // See Bug 735819
    let lcidFromCodePage =
        if (Console.OutputEncoding.CodePage <> 65001) &&
           (Console.OutputEncoding.CodePage <> Thread.CurrentThread.CurrentUICulture.TextInfo.OEMCodePage) &&
           (Console.OutputEncoding.CodePage <> Thread.CurrentThread.CurrentUICulture.TextInfo.ANSICodePage) then
                Thread.CurrentThread.CurrentUICulture <- new CultureInfo("en-US")
                Some 1033
        else
            None

    let directoryBuildingFrom = Directory.GetCurrentDirectory()

    let tryGetMetadataSnapshot = (fun _ -> None)

    let defaultFSharpBinariesDir = FSharpEnvironment.BinFolderOfDefaultFSharpCompiler(None).Value

    let tcConfigB =
       TcConfigBuilder.CreateNew(legacyReferenceResolver,
            defaultFSharpBinariesDir,
            reduceMemoryUsage=reduceMemoryUsage,
            implicitIncludeDir=directoryBuildingFrom,
            isInteractive=false,
            isInvalidationSupported=false,
            defaultCopyFSharpCore=defaultCopyFSharpCore,
            tryGetMetadataSnapshot=tryGetMetadataSnapshot,
            sdkDirOverride=None,
            rangeForErrors=range0)

    // Preset: --optimize+ -g --tailcalls+ (see 4505)
    SetOptimizeSwitch tcConfigB OptionSwitch.On
    SetDebugSwitch    tcConfigB None OptionSwitch.Off
    SetTailcallSwitch tcConfigB OptionSwitch.On

    // Now install a delayed logger to hold all errors from flags until after all flags have been parsed (for example, --vserrors)
    let delayForFlagsLogger =  errorLoggerProvider.CreateDelayAndForwardLogger exiter

    let _unwindEL_1 = PushErrorLoggerPhaseUntilUnwind (fun _ -> delayForFlagsLogger)

    // Share intern'd strings across all lexing/parsing
    let lexResourceManager = new Lexhelp.LexResourceManager()

    let dependencyProvider = new DependencyProvider()

    // Process command line, flags and collect filenames
    let sourceFiles =

        // The ParseCompilerOptions function calls imperative function to process "real" args
        // Rather than start processing, just collect names, then process them.
        try
            let files = ProcessCommandLineFlags (tcConfigB, lcidFromCodePage, argv)
            AdjustForScriptCompile(tcConfigB, files, lexResourceManager, dependencyProvider)
        with e ->
            errorRecovery e rangeStartup
            delayForFlagsLogger.ForwardDelayedDiagnostics tcConfigB
            exiter.Exit 1

    tcConfigB.conditionalCompilationDefines <- "COMPILED" :: tcConfigB.conditionalCompilationDefines

    // Display the banner text, if necessary
    if not bannerAlreadyPrinted then
        DisplayBannerText tcConfigB

    // Create tcGlobals and frameworkTcImports
    let outfile, pdbfile, assemblyName =
        try
            tcConfigB.DecideNames sourceFiles
        with e ->
            errorRecovery e rangeStartup
            delayForFlagsLogger.ForwardDelayedDiagnostics tcConfigB
            exiter.Exit 1

    // DecideNames may give "no inputs" error. Abort on error at this point. bug://3911
    if not tcConfigB.continueAfterParseFailure && delayForFlagsLogger.ErrorCount > 0 then
        delayForFlagsLogger.ForwardDelayedDiagnostics tcConfigB
        exiter.Exit 1

    // If there's a problem building TcConfig, abort
    let tcConfig =
        try
            TcConfig.Create(tcConfigB, validate=false)
        with e ->
            errorRecovery e rangeStartup
            delayForFlagsLogger.ForwardDelayedDiagnostics tcConfigB
            exiter.Exit 1

    let errorLogger =  errorLoggerProvider.CreateErrorLoggerUpToMaxErrors(tcConfigB, exiter)

    // Install the global error logger and never remove it. This logger does have all command-line flags considered.
    let _unwindEL_2 = PushErrorLoggerPhaseUntilUnwind (fun _ -> errorLogger)

    // Forward all errors from flags
    delayForFlagsLogger.CommitDelayedDiagnostics errorLogger

    if not tcConfigB.continueAfterParseFailure then
        AbortOnError(errorLogger, exiter)

    // Resolve assemblies
    ReportTime tcConfig "Import mscorlib and FSharp.Core.dll"
    let foundationalTcConfigP = TcConfigProvider.Constant tcConfig

    let sysRes, otherRes, knownUnresolved = TcAssemblyResolutions.SplitNonFoundationalResolutions(tcConfig)

    // Import basic assemblies
    let tcGlobals, frameworkTcImports = 
        TcImports.BuildFrameworkTcImports (foundationalTcConfigP, sysRes, otherRes)
        |> NodeCode.RunImmediateWithoutCancellation

    // Register framework tcImports to be disposed in future
    disposables.Register frameworkTcImports

    // Parse sourceFiles
    ReportTime tcConfig "Parse inputs"
    use unwindParsePhase = PushThreadBuildPhaseUntilUnwind BuildPhase.Parse

    let createErrorLogger = (fun exiter -> errorLoggerProvider.CreateDelayAndForwardLogger(exiter) :> CapturingErrorLogger)
    let inputs = ParseInputFiles(tcConfig, lexResourceManager, ["COMPILED"], sourceFiles, errorLogger, exiter, createErrorLogger, (*retryLocked*)false)

    let inputs, _ =
        (Map.empty, inputs) ||> List.mapFold (fun state (input, x) ->
            let inputT, stateT = DeduplicateParsedInputModuleName state input
            (inputT, x), stateT)

    // Print the AST if requested
    if tcConfig.printAst then
        for (input, _filename) in inputs do
            printf "AST:\n"
            printfn "%+A" input
            printf "\n"

    if tcConfig.parseOnly then exiter.Exit 0

    if not tcConfig.continueAfterParseFailure then
        AbortOnError(errorLogger, exiter)

    // Apply any nowarn flags
    let tcConfig =
        (tcConfig, inputs) ||> List.fold (fun z (input, sourceFileDirectory) ->
            ApplyMetaCommandsFromInputToTcConfig(z, input, sourceFileDirectory, dependencyProvider))

    let tcConfigP = TcConfigProvider.Constant tcConfig

    // Import other assemblies
    ReportTime tcConfig "Import non-system references"

    let tcImports =
        TcImports.BuildNonFrameworkTcImports(tcConfigP, frameworkTcImports, otherRes, knownUnresolved, dependencyProvider)
        |> NodeCode.RunImmediateWithoutCancellation

    // register tcImports to be disposed in future
    disposables.Register tcImports

    if not tcConfig.continueAfterParseFailure then
        AbortOnError(errorLogger, exiter)

    if tcConfig.importAllReferencesOnly then exiter.Exit 0

    // Build the initial type checking environment
    ReportTime tcConfig "Typecheck"

    use unwindParsePhase = PushThreadBuildPhaseUntilUnwind BuildPhase.TypeCheck

    let tcEnv0 = GetInitialTcEnv (assemblyName, rangeStartup, tcConfig, tcImports, tcGlobals)

    // Type check the inputs
    let inputs = inputs |> List.map fst

    let tcState, topAttrs, typedAssembly, _tcEnvAtEnd =
        TypeCheck(ctok, tcConfig, tcImports, tcGlobals, errorLogger, assemblyName, NiceNameGenerator(), tcEnv0, inputs, exiter)

    AbortOnError(errorLogger, exiter)
    ReportTime tcConfig "Typechecked"

    Args (ctok, tcGlobals, tcImports, frameworkTcImports, tcState.Ccu, typedAssembly, topAttrs, tcConfig, outfile, pdbfile, assemblyName, errorLogger, exiter)

/// Alternative first phase of compilation.  This is for the compile-from-AST feature of FCS.
///   - Import assemblies
///   - Check the inputs
let main1OfAst
       (ctok, legacyReferenceResolver, reduceMemoryUsage, assemblyName, target,
        outfile, pdbFile, dllReferences,
        noframework, exiter: Exiter,
        errorLoggerProvider: ErrorLoggerProvider,
        disposables: DisposablesTracker,
        inputs: ParsedInput list) =

    let tryGetMetadataSnapshot = (fun _ -> None)

    let directoryBuildingFrom = Directory.GetCurrentDirectory()

    let defaultFSharpBinariesDir = FSharpEnvironment.BinFolderOfDefaultFSharpCompiler(None).Value

    let tcConfigB =
        TcConfigBuilder.CreateNew(legacyReferenceResolver, defaultFSharpBinariesDir,
            reduceMemoryUsage=reduceMemoryUsage, implicitIncludeDir=directoryBuildingFrom,
            isInteractive=false, isInvalidationSupported=false,
            defaultCopyFSharpCore=CopyFSharpCoreFlag.No,
            tryGetMetadataSnapshot=tryGetMetadataSnapshot,
            sdkDirOverride=None,
            rangeForErrors=range0)

    let primaryAssembly =
        // temporary workaround until https://github.com/dotnet/fsharp/pull/8043 is merged:
        // pick a primary assembly based on whether the developer included System>Runtime in the list of reference assemblies.
        // It's an ugly compromise used to avoid exposing primaryAssembly in the public api for this function.
        let includesSystem_Runtime = dllReferences |> Seq.exists(fun f -> Path.GetFileName(f).Equals("system.runtime.dll",StringComparison.InvariantCultureIgnoreCase))
        if includesSystem_Runtime then
            PrimaryAssembly.System_Runtime
        else
            PrimaryAssembly.Mscorlib

    tcConfigB.target <- target
    tcConfigB.SetPrimaryAssembly primaryAssembly
    if noframework then
        tcConfigB.framework <- false
        tcConfigB.implicitlyResolveAssemblies <- false

    // Preset: --optimize+ -g --tailcalls+ (see 4505)
    SetOptimizeSwitch tcConfigB OptionSwitch.On
    SetDebugSwitch    tcConfigB None (
        match pdbFile with
        | Some _ -> OptionSwitch.On
        | None -> OptionSwitch.Off)
    SetTailcallSwitch tcConfigB OptionSwitch.On

    // Now install a delayed logger to hold all errors from flags until after all flags have been parsed (for example, --vserrors)
    let delayForFlagsLogger =  errorLoggerProvider.CreateDelayAndForwardLogger exiter
    let _unwindEL_1 = PushErrorLoggerPhaseUntilUnwind (fun _ -> delayForFlagsLogger)

    tcConfigB.conditionalCompilationDefines <- "COMPILED" :: tcConfigB.conditionalCompilationDefines

    // append assembly dependencies
    dllReferences |> List.iter (fun ref -> tcConfigB.AddReferencedAssemblyByPath(rangeStartup,ref))

    // If there's a problem building TcConfig, abort
    let tcConfig =
        try
            TcConfig.Create(tcConfigB,validate=false)
        with e ->
            delayForFlagsLogger.ForwardDelayedDiagnostics tcConfigB
            exiter.Exit 1

    let dependencyProvider = new DependencyProvider()
    let errorLogger =  errorLoggerProvider.CreateErrorLoggerUpToMaxErrors(tcConfigB, exiter)

    // Install the global error logger and never remove it. This logger does have all command-line flags considered.
    let _unwindEL_2 = PushErrorLoggerPhaseUntilUnwind (fun _ -> errorLogger)

    // Forward all errors from flags
    delayForFlagsLogger.CommitDelayedDiagnostics errorLogger

    // Resolve assemblies
    ReportTime tcConfig "Import mscorlib and FSharp.Core.dll"
    let foundationalTcConfigP = TcConfigProvider.Constant tcConfig
    let sysRes, otherRes, knownUnresolved = TcAssemblyResolutions.SplitNonFoundationalResolutions(tcConfig)

    // Import basic assemblies
    let tcGlobals, frameworkTcImports = 
        TcImports.BuildFrameworkTcImports (foundationalTcConfigP, sysRes, otherRes) 
        |> NodeCode.RunImmediateWithoutCancellation

    // Register framework tcImports to be disposed in future
    disposables.Register frameworkTcImports

    use unwindParsePhase = PushThreadBuildPhaseUntilUnwind (BuildPhase.Parse)

    let meta = Directory.GetCurrentDirectory()
    let tcConfig = (tcConfig,inputs) ||> List.fold (fun tcc inp -> ApplyMetaCommandsFromInputToTcConfig (tcc, inp, meta, dependencyProvider))
    let tcConfigP = TcConfigProvider.Constant tcConfig

    // Import other assemblies
    ReportTime tcConfig "Import non-system references"

    let tcImports = 
        TcImports.BuildNonFrameworkTcImports(tcConfigP, frameworkTcImports, otherRes, knownUnresolved, dependencyProvider) 
        |> NodeCode.RunImmediateWithoutCancellation

    // register tcImports to be disposed in future
    disposables.Register tcImports

    // Build the initial type checking environment
    ReportTime tcConfig "Typecheck"
    use unwindParsePhase = PushThreadBuildPhaseUntilUnwind (BuildPhase.TypeCheck)
    let tcEnv0 = GetInitialTcEnv (assemblyName, rangeStartup, tcConfig, tcImports, tcGlobals)

    // Type check the inputs
    let tcState, topAttrs, typedAssembly, _tcEnvAtEnd =
        TypeCheck(ctok, tcConfig, tcImports, tcGlobals, errorLogger, assemblyName, NiceNameGenerator(), tcEnv0, inputs, exiter)

    AbortOnError(errorLogger, exiter)
    ReportTime tcConfig "Typechecked"

    Args (ctok, tcGlobals, tcImports, frameworkTcImports, tcState.Ccu, typedAssembly, topAttrs, tcConfig, outfile, pdbFile, assemblyName, errorLogger, exiter)

/// Second phase of compilation.
///   - Write the signature file, check some attributes
let main2(Args (ctok, tcGlobals, tcImports: TcImports, frameworkTcImports, generatedCcu: CcuThunk, typedImplFiles, topAttrs, tcConfig: TcConfig, outfile, pdbfile, assemblyName, errorLogger, exiter: Exiter)) =

    if tcConfig.typeCheckOnly then exiter.Exit 0

    generatedCcu.Contents.SetAttribs(generatedCcu.Contents.Attribs @ topAttrs.assemblyAttrs)

    use unwindPhase = PushThreadBuildPhaseUntilUnwind BuildPhase.CodeGen
    let signingInfo = ValidateKeySigningAttributes (tcConfig, tcGlobals, topAttrs)

    AbortOnError(errorLogger, exiter)

    // Build an updated errorLogger that filters according to the scopedPragmas. Then install
    // it as the updated global error logger and never remove it
    let oldLogger = errorLogger
    let errorLogger =
        let scopedPragmas = [ for (TImplFile (_, pragmas, _, _, _, _)) in typedImplFiles do yield! pragmas ]
        GetErrorLoggerFilteringByScopedPragmas(true, scopedPragmas, oldLogger)

    let _unwindEL_3 = PushErrorLoggerPhaseUntilUnwind(fun _ -> errorLogger)

    // Try to find an AssemblyVersion attribute
    let assemVerFromAttrib =
        match TryFindVersionAttribute tcGlobals "System.Reflection.AssemblyVersionAttribute" "AssemblyVersionAttribute" topAttrs.assemblyAttrs tcConfig.deterministic with
        | Some v ->
           match tcConfig.version with
           | VersionNone -> Some v
           | _ -> warning(Error(FSComp.SR.fscAssemblyVersionAttributeIgnored(), Range.rangeStartup)); None
        | _ -> None

    // write interface, xmldoc
    ReportTime tcConfig ("Write Interface File")
    use unwindBuildPhase = PushThreadBuildPhaseUntilUnwind BuildPhase.Output
    if tcConfig.printSignature || tcConfig.printAllSignatureFiles then InterfaceFileWriter.WriteInterfaceFile (tcGlobals, tcConfig, InfoReader(tcGlobals, tcImports.GetImportMap()), typedImplFiles)

    ReportTime tcConfig ("Write XML document signatures")
    if tcConfig.xmlDocOutputFile.IsSome then
        XmlDocWriter.ComputeXmlDocSigs (tcGlobals, generatedCcu)

    ReportTime tcConfig ("Write XML docs")
    tcConfig.xmlDocOutputFile |> Option.iter (fun xmlFile ->
        let xmlFile = tcConfig.MakePathAbsolute xmlFile
        XmlDocWriter.WriteXmlDocFile (assemblyName, generatedCcu, xmlFile))

    // Pass on only the minimum information required for the next phase
    Args (ctok, tcConfig, tcImports, frameworkTcImports, tcGlobals, errorLogger, generatedCcu, outfile, typedImplFiles, topAttrs, pdbfile, assemblyName, assemVerFromAttrib, signingInfo, exiter)


/// Third phase of compilation.
///   - encode signature data
///   - optimize
///   - encode optimization data
let main3(Args (ctok, tcConfig, tcImports, frameworkTcImports: TcImports, tcGlobals,
                 errorLogger: ErrorLogger, generatedCcu: CcuThunk, outfile, typedImplFiles,
                 topAttrs, pdbfile, assemblyName, assemVerFromAttrib, signingInfo, exiter: Exiter)) =

    // Encode the signature data
    ReportTime tcConfig ("Encode Interface Data")
    let exportRemapping = MakeExportRemapping generatedCcu generatedCcu.Contents

    let sigDataAttributes, sigDataResources =
        try
            EncodeSignatureData(tcConfig, tcGlobals, exportRemapping, generatedCcu, outfile, false)
        with e ->
            errorRecoveryNoRange e
            exiter.Exit 1

    // Perform optimization
    use unwindBuildPhase = PushThreadBuildPhaseUntilUnwind BuildPhase.Optimize

    let optEnv0 = GetInitialOptimizationEnv (tcImports, tcGlobals)

    let importMap = tcImports.GetImportMap()

    let metadataVersion =
        match tcConfig.metadataVersion with
        | Some v -> v
        | _ ->
            match frameworkTcImports.DllTable.TryFind tcConfig.primaryAssembly.Name with
             | Some ib -> ib.RawMetadata.TryGetILModuleDef().Value.MetadataVersion
             | _ -> ""

    let optimizedImpls, optimizationData, _ = 
        ApplyAllOptimizations 
            (tcConfig, tcGlobals, (LightweightTcValForUsingInBuildMethodCall tcGlobals traitCtxtNone), outfile, 
             importMap, false, optEnv0, generatedCcu, typedImplFiles)

    AbortOnError(errorLogger, exiter)

    // Encode the optimization data
    ReportTime tcConfig ("Encoding OptData")

    let optDataResources = EncodeOptimizationData(tcGlobals, tcConfig, outfile, exportRemapping, (generatedCcu, optimizationData), false)

    // Pass on only the minimum information required for the next phase
    Args (ctok, tcConfig, tcImports, tcGlobals, errorLogger,
          generatedCcu, outfile, optimizedImpls, topAttrs, pdbfile, assemblyName,
          sigDataAttributes, sigDataResources, optDataResources, assemVerFromAttrib, signingInfo, metadataVersion, exiter)

/// Fourth phase of compilation.
///   -  Static linking
///   -  IL code generation
let main4
      (tcImportsCapture,dynamicAssemblyCreator)
      (Args (ctok, tcConfig: TcConfig, tcImports, tcGlobals: TcGlobals, errorLogger,
             generatedCcu: CcuThunk, outfile, optimizedImpls, topAttrs, pdbfile, assemblyName,
             sigDataAttributes, sigDataResources, optDataResources, assemVerFromAttrib, signingInfo, metadataVersion, exiter: Exiter)) =

    match tcImportsCapture with
    | None -> ()
    | Some f -> f tcImports

    // Compute a static linker, it gets called later.
    let ilGlobals = tcGlobals.ilg
    if tcConfig.standalone && generatedCcu.UsesFSharp20PlusQuotations then
        error(Error(FSComp.SR.fscQuotationLiteralsStaticLinking0(), rangeStartup))

    let staticLinker = StaticLink (ctok, tcConfig, tcImports, ilGlobals)

    ReportTime tcConfig "TAST -> IL"
    use unwindBuildPhase = PushThreadBuildPhaseUntilUnwind BuildPhase.IlxGen

    // Create the Abstract IL generator
    let ilxGenerator = CreateIlxAssemblyGenerator (tcConfig, tcImports, tcGlobals, (LightweightTcValForUsingInBuildMethodCall tcGlobals traitCtxtNone), generatedCcu)

    let codegenBackend = (if Option.isSome dynamicAssemblyCreator then IlReflectBackend else IlWriteBackend)

    // Generate the Abstract IL Code
    let codegenResults = GenerateIlxCode (codegenBackend, Option.isSome dynamicAssemblyCreator, false, tcConfig, topAttrs, optimizedImpls, generatedCcu.AssemblyName, ilxGenerator)

    // Build the Abstract IL view of the final main module, prior to static linking

    let topAssemblyAttrs = codegenResults.topAssemblyAttrs
    let topAttrs = {topAttrs with assemblyAttrs=topAssemblyAttrs}
    let permissionSets = codegenResults.permissionSets
    let secDecls = mkILSecurityDecls permissionSets

    let ilxMainModule =
        MainModuleBuilder.CreateMainModule
            (ctok, tcConfig, tcGlobals, tcImports,
             pdbfile, assemblyName, outfile, topAttrs,
             sigDataAttributes, sigDataResources, optDataResources,
             codegenResults, assemVerFromAttrib, metadataVersion, secDecls)

    AbortOnError(errorLogger, exiter)

    // Pass on only the minimum information required for the next phase
    Args (ctok, tcConfig, tcImports, tcGlobals, errorLogger, staticLinker, outfile, pdbfile, ilxMainModule, signingInfo, exiter)

/// Fifth phase of compilation.
///   -  static linking
let main5(Args (ctok, tcConfig, tcImports, tcGlobals, errorLogger: ErrorLogger, staticLinker, outfile, pdbfile, ilxMainModule, signingInfo, exiter: Exiter)) =

    use unwindBuildPhase = PushThreadBuildPhaseUntilUnwind BuildPhase.Output

    // Static linking, if any
    let ilxMainModule =
        try  staticLinker ilxMainModule
        with e ->
            errorRecoveryNoRange e
            exiter.Exit 1

    AbortOnError(errorLogger, exiter)

    // Pass on only the minimum information required for the next phase
    Args (ctok, tcConfig, tcImports, tcGlobals, errorLogger, ilxMainModule, outfile, pdbfile, signingInfo, exiter)

/// Sixth phase of compilation.
///   -  write the binaries
let main6 dynamicAssemblyCreator (Args (ctok, tcConfig,  tcImports: TcImports, tcGlobals: TcGlobals,
                                        errorLogger: ErrorLogger, ilxMainModule, outfile, pdbfile,
                                        signingInfo, exiter: Exiter)) =

    ReportTime tcConfig "Write .NET Binary"

    use unwindBuildPhase = PushThreadBuildPhaseUntilUnwind BuildPhase.Output
    let outfile = tcConfig.MakePathAbsolute outfile

    DoesNotRequireCompilerThreadTokenAndCouldPossiblyBeMadeConcurrent  ctok

    let pdbfile = pdbfile |> Option.map (tcConfig.MakePathAbsolute >> FileSystem.GetFullPathShim)

    let normalizeAssemblyRefs (aref: ILAssemblyRef) =
        match tcImports.TryFindDllInfo (ctok, Range.rangeStartup, aref.Name, lookupOnly=false) with
        | Some dllInfo ->
            match dllInfo.ILScopeRef with
            | ILScopeRef.Assembly ref -> ref
            | _ -> aref
        | None -> aref

    match dynamicAssemblyCreator with
    | None ->
        try
            try
                ILBinaryWriter.WriteILBinary
                 (outfile,
                  { ilg = tcGlobals.ilg
                    pdbfile=pdbfile
                    emitTailcalls = tcConfig.emitTailcalls
                    deterministic = tcConfig.deterministic
                    showTimes = tcConfig.showTimes
                    portablePDB = tcConfig.portablePDB
                    embeddedPDB = tcConfig.embeddedPDB
                    embedAllSource = tcConfig.embedAllSource
                    embedSourceList = tcConfig.embedSourceList
                    sourceLink = tcConfig.sourceLink
                    checksumAlgorithm = tcConfig.checksumAlgorithm
                    signer = GetStrongNameSigner signingInfo
                    dumpDebugInfo = tcConfig.dumpDebugInfo
                    pathMap = tcConfig.pathMap },
                  ilxMainModule,
                  normalizeAssemblyRefs
                  )
            with Failure msg ->
                error(Error(FSComp.SR.fscProblemWritingBinary(outfile, msg), rangeCmdArgs))
        with e ->
            errorRecoveryNoRange e
            exiter.Exit 1
    | Some da -> da (tcConfig, tcGlobals, outfile, ilxMainModule)

    AbortOnError(errorLogger, exiter)

    // Don't copy referenced FSharp.core.dll if we are building FSharp.Core.dll
    if (tcConfig.copyFSharpCore = CopyFSharpCoreFlag.Yes) && not tcConfig.compilingFslib && not tcConfig.standalone then
        CopyFSharpCore(outfile, tcConfig.referencedDLLs)

    ReportTime tcConfig "Exiting"

/// The main (non-incremental) compilation entry point used by fsc.exe
let mainCompile
       (ctok, argv, legacyReferenceResolver, bannerAlreadyPrinted, reduceMemoryUsage,
        defaultCopyFSharpCore, exiter: Exiter, loggerProvider, tcImportsCapture, dynamicAssemblyCreator) =

    use disposables = new DisposablesTracker()
    let savedOut = System.Console.Out
    use __ =
        { new IDisposable with
            member _.Dispose() =
                try
                    System.Console.SetOut(savedOut)
                with _ -> ()}

    main1(ctok, argv, legacyReferenceResolver, bannerAlreadyPrinted, reduceMemoryUsage, defaultCopyFSharpCore, exiter, loggerProvider, disposables)
    |> main2
    |> main3
    |> main4 (tcImportsCapture,dynamicAssemblyCreator)
    |> main5
    |> main6 dynamicAssemblyCreator

/// An additional compilation entry point used by FSharp.Compiler.Service taking syntax trees as input
let compileOfAst
       (ctok, legacyReferenceResolver, reduceMemoryUsage, assemblyName, target,
        targetDll, targetPdb, dependencies, noframework, exiter, loggerProvider, inputs, tcImportsCapture, dynamicAssemblyCreator) =

    use disposables = new DisposablesTracker()
    main1OfAst (ctok, legacyReferenceResolver, reduceMemoryUsage, assemblyName, target, targetDll, targetPdb,
                dependencies, noframework, exiter, loggerProvider, disposables, inputs)
    |> main2
    |> main3
    |> main4 (tcImportsCapture, dynamicAssemblyCreator)
    |> main5
    |> main6 dynamicAssemblyCreator
