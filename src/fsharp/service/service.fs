// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.CodeAnalysis

open System
open System.Collections.Concurrent
open System.Diagnostics
open System.IO
open System.Reflection
open System.Threading
open Internal.Utilities.Collections
open Internal.Utilities.Library
open Internal.Utilities.Library.Extras
open FSharp.Compiler 
open FSharp.Compiler.AbstractIL
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AbstractIL.ILBinaryReader
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.CompilerConfig
open FSharp.Compiler.CompilerDiagnostics
open FSharp.Compiler.CompilerImports
open FSharp.Compiler.CompilerOptions
open FSharp.Compiler.DependencyManager
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.Driver
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.IO
open FSharp.Compiler.ParseAndCheckInputs
open FSharp.Compiler.ScriptClosure
open FSharp.Compiler.Symbols
open FSharp.Compiler.Syntax
open FSharp.Compiler.Tokenization
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Compiler.TcGlobals 
open FSharp.Compiler.BuildGraph

[<AutoOpen>]
module EnvMisc =
    let braceMatchCacheSize = GetEnvInteger "FCS_BraceMatchCacheSize" 5
    let parseFileCacheSize = GetEnvInteger "FCS_ParseFileCacheSize" 2
    let checkFileInProjectCacheSize = GetEnvInteger "FCS_CheckFileInProjectCacheSize" 10

    let projectCacheSizeDefault   = GetEnvInteger "FCS_ProjectCacheSizeDefault" 3
    let frameworkTcImportsCacheStrongSize = GetEnvInteger "FCS_frameworkTcImportsCacheStrongSizeDefault" 8
    let maxMBDefault =  GetEnvInteger "FCS_MaxMB" 1000000 // a million MB = 1TB = disabled
    //let maxMBDefault = GetEnvInteger "FCS_maxMB" (if sizeof<int> = 4 then 1700 else 3400)

//----------------------------------------------------------------------------
// BackgroundCompiler
//

/// Callback that indicates whether a requested result has become obsolete.    
[<NoComparison;NoEquality>]
type IsResultObsolete = 
    | IsResultObsolete of (unit->bool)


[<AutoOpen>]
module Helpers = 

    /// Determine whether two (fileName,options) keys are identical w.r.t. affect on checking
    let AreSameForChecking2((fileName1: string, options1: FSharpProjectOptions), (fileName2, options2)) =
        (fileName1 = fileName2) 
        && FSharpProjectOptions.AreSameForChecking(options1,options2)
        
    /// Determine whether two (fileName,options) keys should be identical w.r.t. resource usage
    let AreSubsumable2((fileName1:string,o1:FSharpProjectOptions),(fileName2:string,o2:FSharpProjectOptions)) =
        (fileName1 = fileName2)
        && FSharpProjectOptions.UseSameProject(o1,o2)

    /// Determine whether two (fileName,sourceText,options) keys should be identical w.r.t. parsing
    let AreSameForParsing((fileName1: string, source1Hash: int64, options1), (fileName2, source2Hash, options2)) =
        fileName1 = fileName2 && options1 = options2 && source1Hash = source2Hash

    let AreSimilarForParsing((fileName1, _, _), (fileName2, _, _)) =
        fileName1 = fileName2
        
    /// Determine whether two (fileName,sourceText,options) keys should be identical w.r.t. checking
    let AreSameForChecking3((fileName1: string, source1Hash: int64, options1: FSharpProjectOptions), (fileName2, source2Hash, options2)) =
        (fileName1 = fileName2) 
        && FSharpProjectOptions.AreSameForChecking(options1,options2)
        && source1Hash = source2Hash

    /// Determine whether two (fileName,sourceText,options) keys should be identical w.r.t. resource usage
    let AreSubsumable3((fileName1:string,_,o1:FSharpProjectOptions),(fileName2:string,_,o2:FSharpProjectOptions)) =
        (fileName1 = fileName2)
        && FSharpProjectOptions.UseSameProject(o1,o2)

module CompileHelpers =
    let mkCompilationErrorHandlers() = 
        let errors = ResizeArray<_>()

        let errorSink isError exn = 
            let mainError, relatedErrors = SplitRelatedDiagnostics exn
            let oneError e = errors.Add(FSharpDiagnostic.CreateFromException (e, isError, Range.range0, true)) // Suggest names for errors
            oneError mainError
            List.iter oneError relatedErrors

        let errorLogger = 
            { new ErrorLogger("CompileAPI") with 
                member x.DiagnosticSink(exn, isError) = errorSink isError exn
                member x.ErrorCount = errors |> Seq.filter (fun e -> e.Severity = FSharpDiagnosticSeverity.Error) |> Seq.length }

        let loggerProvider = 
            { new ErrorLoggerProvider() with 
                member x.CreateErrorLoggerUpToMaxErrors(_tcConfigBuilder, _exiter) = errorLogger    }
        errors, errorLogger, loggerProvider

    let tryCompile errorLogger f = 
        use unwindParsePhase = PushThreadBuildPhaseUntilUnwind (BuildPhase.Parse)            
        use unwindEL_2 = PushErrorLoggerPhaseUntilUnwind (fun _ -> errorLogger)
        let exiter = { new Exiter with member x.Exit n = raise StopProcessing }
        try 
            f exiter
            0
        with e -> 
            stopProcessingRecovery e Range.range0
            1

    /// Compile using the given flags.  Source files names are resolved via the FileSystem API. The output file must be given by a -o flag. 
    let compileFromArgs (ctok, argv: string[], legacyReferenceResolver, tcImportsCapture, dynamicAssemblyCreator)  = 
    
        let errors, errorLogger, loggerProvider = mkCompilationErrorHandlers()
        let result = 
            tryCompile errorLogger (fun exiter -> 
                mainCompile (ctok, argv, legacyReferenceResolver, (*bannerAlreadyPrinted*)true, ReduceMemoryFlag.Yes, CopyFSharpCoreFlag.No, exiter, loggerProvider, tcImportsCapture, dynamicAssemblyCreator) )
    
        errors.ToArray(), result

    let compileFromAsts (ctok, legacyReferenceResolver, asts, assemblyName, outFile, dependencies, noframework, pdbFile, executable, tcImportsCapture, dynamicAssemblyCreator) =

        let errors, errorLogger, loggerProvider = mkCompilationErrorHandlers()
    
        let executable = defaultArg executable true
        let target = if executable then CompilerTarget.ConsoleExe else CompilerTarget.Dll
    
        let result = 
            tryCompile errorLogger (fun exiter -> 
                compileOfAst (ctok, legacyReferenceResolver, ReduceMemoryFlag.Yes, assemblyName, target, outFile, pdbFile, dependencies, noframework, exiter, loggerProvider, asts, tcImportsCapture, dynamicAssemblyCreator))

        errors.ToArray(), result

    let createDynamicAssembly (debugInfo: bool, tcImportsRef: TcImports option ref, execute: bool, assemblyBuilderRef: _ option ref) (tcConfig: TcConfig, tcGlobals:TcGlobals, outfile, ilxMainModule) =

        // Create an assembly builder
        let assemblyName = System.Reflection.AssemblyName(System.IO.Path.GetFileNameWithoutExtension outfile)
        let flags = System.Reflection.Emit.AssemblyBuilderAccess.Run
#if FX_NO_APP_DOMAINS
        let assemblyBuilder = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(assemblyName, flags)
        let moduleBuilder = assemblyBuilder.DefineDynamicModule("IncrementalModule")
#else
        let assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, flags)
        let moduleBuilder = assemblyBuilder.DefineDynamicModule("IncrementalModule", debugInfo)
#endif            
        // Omit resources in dynamic assemblies, because the module builder is constructed without a filename the module 
        // is tagged as transient and as such DefineManifestResource will throw an invalid operation if resources are present.
        // 
        // Also, the dynamic assembly creator can't currently handle types called "<Module>" from statically linked assemblies.
        let ilxMainModule = 
            { ilxMainModule with 
                TypeDefs = ilxMainModule.TypeDefs.AsList |> List.filter (fun td -> not (isTypeNameForGlobalFunctions td.Name)) |> mkILTypeDefs
                Resources=mkILResources [] }

        // The function used to resolve types while emitting the code
        let assemblyResolver s = 
            match tcImportsRef.Value.Value.TryFindExistingFullyQualifiedPathByExactAssemblyRef (s) with 
            | Some res -> Some (Choice1Of2 res)
            | None -> None

        // Emit the code
        let _emEnv,execs = ILRuntimeWriter.emitModuleFragment(tcGlobals.ilg, tcConfig.emitTailcalls, ILRuntimeWriter.emEnv0, assemblyBuilder, moduleBuilder, ilxMainModule, debugInfo, assemblyResolver, tcGlobals.TryFindSysILTypeRef)

        // Execute the top-level initialization, if requested
        if execute then 
            for exec in execs do 
                match exec() with 
                | None -> ()
                | Some exn -> 
                    PreserveStackTrace(exn)
                    raise exn

        // Register the reflected definitions for the dynamically generated assembly
        for resource in ilxMainModule.Resources.AsList do 
            if IsReflectedDefinitionsResource resource then 
                Quotations.Expr.RegisterReflectedDefinitions (assemblyBuilder, moduleBuilder.Name, resource.GetBytes().ToArray())

        // Save the result
        assemblyBuilderRef := Some assemblyBuilder
        
    let setOutputStreams execute = 
        // Set the output streams, if requested
        match execute with
        | Some (writer,error) -> 
            System.Console.SetOut writer
            System.Console.SetError error
        | None -> ()

type SourceTextHash = int64
type CacheStamp = int64
type FileName = string      
type FilePath = string
type ProjectPath = string
type FileVersion = int

type ParseCacheLockToken() = interface LockToken
type ScriptClosureCacheToken() = interface LockToken

type CheckFileCacheKey = FileName * SourceTextHash * FSharpProjectOptions
type CheckFileCacheValue = FSharpParseFileResults * FSharpCheckFileResults * SourceTextHash * DateTime

// There is only one instance of this type, held in FSharpChecker
type BackgroundCompiler(
                        legacyReferenceResolver, 
                        projectCacheSize, 
                        keepAssemblyContents, 
                        keepAllBackgroundResolutions, 
                        tryGetMetadataSnapshot, 
                        suggestNamesForErrors, 
                        keepAllBackgroundSymbolUses, 
                        enableBackgroundItemKeyStoreAndSemanticClassification, 
                        enablePartialTypeChecking) as self =

    let beforeFileChecked = Event<string * FSharpProjectOptions>()
    let fileParsed = Event<string * FSharpProjectOptions>()
    let fileChecked = Event<string * FSharpProjectOptions>()
    let projectChecked = Event<FSharpProjectOptions>()

    // STATIC ROOT: FSharpLanguageServiceTestable.FSharpChecker.backgroundCompiler.scriptClosureCache 
    /// Information about the derived script closure.
    let scriptClosureCache = 
        MruCache<AnyCallerThreadToken, FSharpProjectOptions, LoadClosure>(projectCacheSize, 
            areSame=FSharpProjectOptions.AreSameForChecking, 
            areSimilar=FSharpProjectOptions.UseSameProject)

    let frameworkTcImportsCache = FrameworkImportsCache(frameworkTcImportsCacheStrongSize)

    // We currently share one global dependency provider for all scripts for the FSharpChecker.
    // For projects, one is used per project.
    // 
    // Sharing one for all scripts is necessary for good performance from GetProjectOptionsFromScript,
    // which requires a dependency provider to process through the project options prior to working out
    // if the cached incremental builder can be used for the project.
    let dependencyProviderForScripts = new DependencyProvider()

    /// CreateOneIncrementalBuilder (for background type checking). Note that fsc.fs also
    /// creates an incremental builder used by the command line compiler.
    let CreateOneIncrementalBuilder (options:FSharpProjectOptions, userOpName) = 
      node {
        Trace.TraceInformation("FCS: {0}.{1} ({2})", userOpName, "CreateOneIncrementalBuilder", options.ProjectFileName)
        let projectReferences =  
            [ for r in options.ReferencedProjects do

               match r with
               | FSharpReferencedProject.FSharpReference(nm,opts) ->
                   // Don't use cross-project references for FSharp.Core, since various bits of code require a concrete FSharp.Core to exist on-disk.
                   // The only solutions that have these cross-project references to FSharp.Core are VisualFSharp.sln and FSharp.sln. The only ramification
                   // of this is that you need to build FSharp.Core to get intellisense in those projects.

                   if (try Path.GetFileNameWithoutExtension(nm) with _ -> "") <> GetFSharpCoreLibraryName() then

                     yield
                        { new IProjectReference with 
                            member x.EvaluateRawContents() = 
                              node {
                                Trace.TraceInformation("FCS: {0}.{1} ({2})", userOpName, "GetAssemblyData", nm)
                                return! self.GetAssemblyData(opts, userOpName + ".CheckReferencedProject("+nm+")")
                              }
                            member x.TryGetLogicalTimeStamp(cache) = 
                                self.TryGetLogicalTimeStampForProject(cache, opts)
                            member x.FileName = nm }
                            
                | FSharpReferencedProject.PEReference(nm,stamp,delayedReader) ->
                    yield
                        { new IProjectReference with 
                            member x.EvaluateRawContents() = 
                              node {
                                let! ilReaderOpt = delayedReader.TryGetILModuleReader() |> NodeCode.FromCancellable
                                match ilReaderOpt with
                                | Some ilReader ->
                                    let ilModuleDef, ilAsmRefs = ilReader.ILModuleDef, ilReader.ILAssemblyRefs
                                    return RawFSharpAssemblyData(ilModuleDef, ilAsmRefs) :> IRawFSharpAssemblyData |> Some
                                | _ ->
                                    return None
                              }
                            member x.TryGetLogicalTimeStamp(_) = stamp |> Some
                            member x.FileName = nm }

                | FSharpReferencedProject.ILModuleReference(nm,getStamp,getReader) ->
                    yield
                        { new IProjectReference with 
                            member x.EvaluateRawContents() = 
                              node {
                                let ilReader = getReader()
                                let ilModuleDef, ilAsmRefs = ilReader.ILModuleDef, ilReader.ILAssemblyRefs
                                return RawFSharpAssemblyData(ilModuleDef, ilAsmRefs) :> IRawFSharpAssemblyData |> Some
                              }
                            member x.TryGetLogicalTimeStamp(_) = getStamp() |> Some
                            member x.FileName = nm }
                ]

        let loadClosure = scriptClosureCache.TryGet(AnyCallerThread, options)

        let! builderOpt, diagnostics = 
            IncrementalBuilder.TryCreateIncrementalBuilderForProjectOptions
                  (legacyReferenceResolver, FSharpCheckerResultsSettings.defaultFSharpBinariesDir, frameworkTcImportsCache, loadClosure, Array.toList options.SourceFiles, 
                   Array.toList options.OtherOptions, projectReferences, options.ProjectDirectory, 
                   options.UseScriptResolutionRules, keepAssemblyContents, keepAllBackgroundResolutions,
                   tryGetMetadataSnapshot, suggestNamesForErrors, keepAllBackgroundSymbolUses,
                   enableBackgroundItemKeyStoreAndSemanticClassification,
                   enablePartialTypeChecking,
                   (if options.UseScriptResolutionRules then Some dependencyProviderForScripts else None))

        match builderOpt with 
        | None -> ()
        | Some builder -> 

#if !NO_EXTENSIONTYPING
            // Register the behaviour that responds to CCUs being invalidated because of type
            // provider Invalidate events. This invalidates the configuration in the build.
            builder.ImportsInvalidatedByTypeProvider.Add(fun () -> self.InvalidateConfiguration(options, userOpName))
#endif

            // Register the callback called just before a file is typechecked by the background builder (without recording
            // errors or intellisense information).
            //
            // This indicates to the UI that the file type check state is dirty. If the file is open and visible then 
            // the UI will sooner or later request a typecheck of the file, recording errors and intellisense information.
            builder.BeforeFileChecked.Add (fun file -> beforeFileChecked.Trigger(file, options))
            builder.FileParsed.Add (fun file -> fileParsed.Trigger(file, options))
            builder.FileChecked.Add (fun file -> fileChecked.Trigger(file, options))
            builder.ProjectChecked.Add (fun () -> projectChecked.Trigger (options))

        return (builderOpt, diagnostics)
      }

    let parseCacheLock = Lock<ParseCacheLockToken>()
    
    // STATIC ROOT: FSharpLanguageServiceTestable.FSharpChecker.parseFileInProjectCache. Most recently used cache for parsing files.
    let parseFileCache = MruCache<ParseCacheLockToken,(_ * SourceTextHash * _),_>(parseFileCacheSize, areSimilar = AreSimilarForParsing, areSame = AreSameForParsing)

    // STATIC ROOT: FSharpLanguageServiceTestable.FSharpChecker.checkFileInProjectCache
    //
    /// Cache which holds recently seen type-checks.
    /// This cache may hold out-of-date entries, in two senses
    ///    - there may be a more recent antecedent state available because the background build has made it available
    ///    - the source for the file may have changed

    // Also keyed on source. This can only be out of date if the antecedent is out of date
    let checkFileInProjectCache =
        MruCache<ParseCacheLockToken, CheckFileCacheKey, GraphNode<CheckFileCacheValue>>
            (keepStrongly=checkFileInProjectCacheSize,
             areSame=AreSameForChecking3,
             areSimilar=AreSubsumable3)

    // STATIC ROOT: FSharpLanguageServiceTestable.FSharpChecker.backgroundCompiler.incrementalBuildersCache. This root typically holds more 
    // live information than anything else in the F# Language Service, since it holds up to 3 (projectCacheStrongSize) background project builds
    // strongly.
    // 
    /// Cache of builds keyed by options.  
    let gate = obj()
    let incrementalBuildersCache = 
        MruCache<AnyCallerThreadToken, FSharpProjectOptions, GraphNode<(IncrementalBuilder option * FSharpDiagnostic[])>>
                (keepStrongly=projectCacheSize, keepMax=projectCacheSize, 
                 areSame =  FSharpProjectOptions.AreSameForChecking, 
                 areSimilar =  FSharpProjectOptions.UseSameProject)

    let tryGetBuilderNode options =
        incrementalBuildersCache.TryGet (AnyCallerThread, options)

    let tryGetBuilder options : NodeCode<(IncrementalBuilder option * FSharpDiagnostic[])> option =
        tryGetBuilderNode options
        |> Option.map (fun x -> x.GetOrComputeValue())

    let tryGetSimilarBuilder options : NodeCode<(IncrementalBuilder option * FSharpDiagnostic[])> option =
        incrementalBuildersCache.TryGetSimilar (AnyCallerThread, options)
        |> Option.map (fun x -> x.GetOrComputeValue())

    let tryGetAnyBuilder options : NodeCode<(IncrementalBuilder option * FSharpDiagnostic[])> option =
        incrementalBuildersCache.TryGetAny (AnyCallerThread, options)
        |> Option.map (fun x -> x.GetOrComputeValue())

    let createBuilderNode (options, userOpName, ct: CancellationToken) =
        lock gate (fun () ->
            if ct.IsCancellationRequested then
                GraphNode(node { return None, [||] })
            else
                let getBuilderNode = 
                    GraphNode(CreateOneIncrementalBuilder(options, userOpName))
                incrementalBuildersCache.Set (AnyCallerThread, options, getBuilderNode)
                getBuilderNode
        )

    let createAndGetBuilder (options, userOpName) =
        node {
            let! ct = NodeCode.CancellationToken
            let getBuilderNode = createBuilderNode (options, userOpName, ct)
            return! getBuilderNode.GetOrComputeValue()
        }

    let getOrCreateBuilder (options, userOpName) : NodeCode<(IncrementalBuilder option * FSharpDiagnostic[])> =
        match tryGetBuilder options with
        | Some getBuilder -> 
            node {
                match! getBuilder with
                | builderOpt, creationDiags when builderOpt.IsNone || not builderOpt.Value.IsReferencesInvalidated -> 
                    Logger.Log LogCompilerFunctionId.Service_IncrementalBuildersCache_GettingCache
                    return builderOpt,creationDiags
                | _ ->
                    // The builder could be re-created,
                    //    clear the check file caches that are associated with it.
                    //    We must do this in order to not return stale results when references
                    //    in the project get changed/added/removed.
                    parseCacheLock.AcquireLock(fun ltok -> 
                        options.SourceFiles
                        |> Array.iter (fun sourceFile ->
                            let key = (sourceFile, 0L, options)
                            checkFileInProjectCache.RemoveAnySimilar(ltok, key)
                        )
                    )
                    return! createAndGetBuilder (options, userOpName)
            }
        | _ -> 
            createAndGetBuilder (options, userOpName)

    let getSimilarOrCreateBuilder (options, userOpName) =
        match tryGetSimilarBuilder options with
        | Some res -> res
        // The builder does not exist at all. Create it.
        | None -> getOrCreateBuilder (options, userOpName)

    let getOrCreateBuilderWithInvalidationFlag (options, canInvalidateProject, userOpName) =
        if canInvalidateProject then
            getOrCreateBuilder (options, userOpName)
        else
            getSimilarOrCreateBuilder (options, userOpName)

    let getAnyBuilder (options, userOpName) =
        match tryGetAnyBuilder options with
        | Some getBuilder -> 
            Logger.Log LogCompilerFunctionId.Service_IncrementalBuildersCache_GettingCache
            getBuilder
        | _ ->
            getOrCreateBuilder (options, userOpName)

    /// Should be a fast operation. Ensures that we have only one async lazy object per file and its hash.
    let getCheckFileNode (parseResults,
                          sourceText,
                          fileName,
                          options,
                          _fileVersion,
                          builder,
                          tcPrior,
                          tcInfo,
                          creationDiags) (onComplete) =

        // Here we lock for the creation of the node, not its execution
        parseCacheLock.AcquireLock (fun ltok -> 
            let key = (fileName, sourceText.GetHashCode() |> int64, options)
            match checkFileInProjectCache.TryGet(ltok, key) with
            | Some res -> res
            | _ ->
                let res =
                    GraphNode(node {
                        let! res =
                            self.CheckOneFileImplAux(
                                parseResults,
                                sourceText,
                                fileName,
                                options,
                                builder,
                                tcPrior,
                                tcInfo,
                                creationDiags)
                        onComplete()
                        return res
                    })
                checkFileInProjectCache.Set(ltok, key, res)
                res
        )

    static let mutable actualParseFileCount = 0

    static let mutable actualCheckFileCount = 0

    member _.ParseFile(filename: string, sourceText: ISourceText, options: FSharpParsingOptions, cache: bool, userOpName: string) =
        async {
          if cache then
            let hash = sourceText.GetHashCode() |> int64
            match parseCacheLock.AcquireLock(fun ltok -> parseFileCache.TryGet(ltok, (filename, hash, options))) with
            | Some res -> return res
            | None ->
                Interlocked.Increment(&actualParseFileCount) |> ignore
                let parseDiags, parseTree, anyErrors = ParseAndCheckFile.parseFile(sourceText, filename, options, userOpName, suggestNamesForErrors)
                let res = FSharpParseFileResults(parseDiags, parseTree, anyErrors, options.SourceFiles)
                parseCacheLock.AcquireLock(fun ltok -> parseFileCache.Set(ltok, (filename, hash, options), res))
                return res
          else
            let parseDiags, parseTree, anyErrors = ParseAndCheckFile.parseFile(sourceText, filename, options, userOpName, false)
            return FSharpParseFileResults(parseDiags, parseTree, anyErrors, options.SourceFiles)
        }

    /// Fetch the parse information from the background compiler (which checks w.r.t. the FileSystem API)
    member _.GetBackgroundParseResultsForFileInProject(filename, options, userOpName) =
        node {
            let! builderOpt, creationDiags = getOrCreateBuilder (options, userOpName)
            match builderOpt with
            | None ->
                let parseTree = EmptyParsedInput(filename, (false, false))
                return FSharpParseFileResults(creationDiags, parseTree, true, [| |])
            | Some builder -> 
                let parseTree,_,_,parseDiags = builder.GetParseResultsForFile (filename)
                let diagnostics = [| yield! creationDiags; yield! DiagnosticHelpers.CreateDiagnostics (builder.TcConfig.errorSeverityOptions, false, filename, parseDiags, suggestNamesForErrors) |]
                return FSharpParseFileResults(diagnostics = diagnostics, input = parseTree, parseHadErrors = false, dependencyFiles = builder.AllDependenciesDeprecated)
        }

    member _.GetCachedCheckFileResult(builder: IncrementalBuilder, filename, sourceText: ISourceText, options) =
        node {
            let hash = sourceText.GetHashCode() |> int64
            let key = (filename, hash, options)
            let cachedResultsOpt = parseCacheLock.AcquireLock(fun ltok -> checkFileInProjectCache.TryGet(ltok, key))

            match cachedResultsOpt with
            | Some cachedResults ->
                match! cachedResults.GetOrComputeValue() with
                | (parseResults, checkResults,_,priorTimeStamp) 
                        when 
                        (match builder.GetCheckResultsBeforeFileInProjectEvenIfStale filename with 
                        | None -> false
                        | Some(tcPrior) -> 
                            tcPrior.TimeStamp = priorTimeStamp &&
                            builder.AreCheckResultsBeforeFileInProjectReady(filename)) -> 
                    return Some (parseResults,checkResults)
                | _ ->
                    parseCacheLock.AcquireLock(fun ltok -> checkFileInProjectCache.RemoveAnySimilar(ltok, key))
                    return None
            | _ ->
                return None
        }

    member private bc.CheckOneFileImplAux
        (parseResults: FSharpParseFileResults,
         sourceText: ISourceText,
         fileName: string,
         options: FSharpProjectOptions,
         builder: IncrementalBuilder,
         tcPrior: PartialCheckResults,
         tcInfo: TcInfo,
         creationDiags: FSharpDiagnostic[]) : NodeCode<CheckFileCacheValue> = 

        node {
            // Get additional script #load closure information if applicable.
            // For scripts, this will have been recorded by GetProjectOptionsFromScript.
            let tcConfig = tcPrior.TcConfig
            let loadClosure = scriptClosureCache.TryGet(AnyCallerThread, options)

            let! checkAnswer = 
                FSharpCheckFileResults.CheckOneFile
                    (parseResults,
                        sourceText,
                        fileName,
                        options.ProjectFileName, 
                        tcConfig,
                        tcPrior.TcGlobals,
                        tcPrior.TcImports, 
                        tcInfo.tcState,
                        tcInfo.moduleNamesDict,
                        loadClosure,
                        tcInfo.TcErrors,
                        options.IsIncompleteTypeCheckEnvironment, 
                        options, 
                        builder, 
                        Array.ofList tcInfo.tcDependencyFiles, 
                        creationDiags, 
                        parseResults.Diagnostics, 
                        keepAssemblyContents,
                        suggestNamesForErrors) |> NodeCode.FromCancellable
            GraphNode.SetPreferredUILang tcConfig.preferredUiLang
            return (parseResults, checkAnswer, sourceText.GetHashCode() |> int64, tcPrior.TimeStamp)
        }
        

    member private bc.CheckOneFileImpl
        (parseResults: FSharpParseFileResults,
         sourceText: ISourceText,
         fileName: string,
         options: FSharpProjectOptions,
         fileVersion: int,
         builder: IncrementalBuilder,
         tcPrior: PartialCheckResults,
         tcInfo: TcInfo,
         creationDiags: FSharpDiagnostic[]) =

         node {
            match! bc.GetCachedCheckFileResult(builder, fileName, sourceText, options) with
            | Some (_, results) -> return FSharpCheckFileAnswer.Succeeded results
            | _ ->
                let lazyCheckFile =
                    getCheckFileNode 
                        (parseResults, sourceText, fileName, options, fileVersion, builder, tcPrior, tcInfo, creationDiags)
                        (fun () ->
                            Interlocked.Increment(&actualCheckFileCount) |> ignore
                        )

                let! (_, results, _, _) = lazyCheckFile.GetOrComputeValue()
                return FSharpCheckFileAnswer.Succeeded results
         }

    /// Type-check the result obtained by parsing, but only if the antecedent type checking context is available. 
    member bc.CheckFileInProjectAllowingStaleCachedResults(parseResults: FSharpParseFileResults, filename, fileVersion, sourceText: ISourceText, options, userOpName) =
        node {
            let! cachedResults = 
                node {
                    let! builderOpt, creationDiags = getAnyBuilder (options, userOpName) 

                    match builderOpt with
                    | Some builder ->
                        match! bc.GetCachedCheckFileResult(builder, filename, sourceText, options) with
                        | Some (_, checkResults) -> return Some (builder, creationDiags, Some (FSharpCheckFileAnswer.Succeeded checkResults))
                        | _ -> return Some (builder, creationDiags, None)
                    | _ -> return None // the builder wasn't ready
                }
                        
            match cachedResults with
            | None -> return None
            | Some (_, _, Some x) -> return Some x
            | Some (builder, creationDiags, None) ->
                Trace.TraceInformation("FCS: {0}.{1} ({2})", userOpName, "CheckFileInProjectAllowingStaleCachedResults.CacheMiss", filename)
                match builder.GetCheckResultsBeforeFileInProjectEvenIfStale filename with
                | Some tcPrior -> 
                    match tcPrior.TryPeekTcInfo() with
                    | Some tcInfo -> 
                        let! checkResults = bc.CheckOneFileImpl(parseResults, sourceText, filename, options, fileVersion, builder, tcPrior, tcInfo, creationDiags)
                        return Some checkResults
                    | None ->
                        return None
                | None -> return None  // the incremental builder was not up to date
        }

    /// Type-check the result obtained by parsing. Force the evaluation of the antecedent type checking context if needed.
    member bc.CheckFileInProject(parseResults: FSharpParseFileResults, filename, fileVersion, sourceText: ISourceText, options, userOpName) =
        node {
            let! builderOpt,creationDiags = getOrCreateBuilder (options, userOpName)
            match builderOpt with
            | None -> return FSharpCheckFileAnswer.Succeeded (FSharpCheckFileResults.MakeEmpty(filename, creationDiags, keepAssemblyContents))
            | Some builder -> 
                // Check the cache. We can only use cached results when there is no work to do to bring the background builder up-to-date
                let! cachedResults = bc.GetCachedCheckFileResult(builder, filename, sourceText, options)

                match cachedResults with
                | Some (_, checkResults) -> return FSharpCheckFileAnswer.Succeeded checkResults
                | _ ->
                    let! tcPrior = builder.GetCheckResultsBeforeFileInProject (filename)
                    let! tcInfo = tcPrior.GetOrComputeTcInfo()
                    return! bc.CheckOneFileImpl(parseResults, sourceText, filename, options, fileVersion, builder, tcPrior, tcInfo, creationDiags)
        }

    /// Parses and checks the source file and returns untyped AST and check results.
    member bc.ParseAndCheckFileInProject (filename:string, fileVersion, sourceText: ISourceText, options:FSharpProjectOptions, userOpName) =
        node {
            let strGuid = "_ProjectId=" + (options.ProjectId |> Option.defaultValue "null")
            Logger.LogBlockMessageStart (filename + strGuid) LogCompilerFunctionId.Service_ParseAndCheckFileInProject

            let! builderOpt,creationDiags = getOrCreateBuilder (options, userOpName)
            match builderOpt with
            | None -> 
                Logger.LogBlockMessageStop (filename + strGuid + "-Failed_Aborted") LogCompilerFunctionId.Service_ParseAndCheckFileInProject

                let parseTree = EmptyParsedInput(filename, (false, false))
                let parseResults = FSharpParseFileResults(creationDiags, parseTree, true, [| |])
                return (parseResults, FSharpCheckFileAnswer.Aborted)

            | Some builder -> 
                let! cachedResults = bc.GetCachedCheckFileResult(builder, filename, sourceText, options)

                match cachedResults with 
                | Some (parseResults, checkResults) -> 
                    Logger.LogBlockMessageStop (filename + strGuid + "-Successful_Cached") LogCompilerFunctionId.Service_ParseAndCheckFileInProject

                    return (parseResults, FSharpCheckFileAnswer.Succeeded checkResults)
                | _ ->
                    let! tcPrior = builder.GetCheckResultsBeforeFileInProject (filename)
                    let! tcInfo = tcPrior.GetOrComputeTcInfo()
                    // Do the parsing.
                    let parsingOptions = FSharpParsingOptions.FromTcConfig(builder.TcConfig, Array.ofList (builder.SourceFiles), options.UseScriptResolutionRules)
                    GraphNode.SetPreferredUILang tcPrior.TcConfig.preferredUiLang
                    let parseDiags, parseTree, anyErrors = ParseAndCheckFile.parseFile (sourceText, filename, parsingOptions, userOpName, suggestNamesForErrors)
                    let parseResults = FSharpParseFileResults(parseDiags, parseTree, anyErrors, builder.AllDependenciesDeprecated)
                    let! checkResults = bc.CheckOneFileImpl(parseResults, sourceText, filename, options, fileVersion, builder, tcPrior, tcInfo, creationDiags)

                    Logger.LogBlockMessageStop (filename + strGuid + "-Successful") LogCompilerFunctionId.Service_ParseAndCheckFileInProject

                    return (parseResults, checkResults)
        }

    /// Fetch the check information from the background compiler (which checks w.r.t. the FileSystem API)
    member _.GetBackgroundCheckResultsForFileInProject(filename, options, userOpName) =
        node {
            let! builderOpt, creationDiags = getOrCreateBuilder (options, userOpName)
            match builderOpt with
            | None ->
                let parseTree = EmptyParsedInput(filename, (false, false))
                let parseResults = FSharpParseFileResults(creationDiags, parseTree, true, [| |])
                let typedResults = FSharpCheckFileResults.MakeEmpty(filename, creationDiags, true)
                return (parseResults, typedResults)
            | Some builder -> 
                let (parseTree, _, _, parseDiags) = builder.GetParseResultsForFile (filename)
                let! tcProj = builder.GetFullCheckResultsAfterFileInProject (filename)

                let! tcInfo, tcInfoExtras = tcProj.GetOrComputeTcInfoWithExtras()

                let tcResolutions = tcInfoExtras.tcResolutions
                let tcSymbolUses = tcInfoExtras.tcSymbolUses
                let tcOpenDeclarations = tcInfoExtras.tcOpenDeclarations
                let latestCcuSigForFile = tcInfo.latestCcuSigForFile
                let tcState = tcInfo.tcState
                let tcEnvAtEnd = tcInfo.tcEnvAtEndOfFile
                let latestImplementationFile = tcInfoExtras.latestImplFile
                let tcDependencyFiles = tcInfo.tcDependencyFiles
                let tcErrors = tcInfo.TcErrors
                let errorOptions = builder.TcConfig.errorSeverityOptions
                let parseDiags = [| yield! creationDiags; yield! DiagnosticHelpers.CreateDiagnostics (errorOptions, false, filename, parseDiags, suggestNamesForErrors) |]
                let tcErrors = [| yield! creationDiags; yield! DiagnosticHelpers.CreateDiagnostics (errorOptions, false, filename, tcErrors, suggestNamesForErrors) |]
                let parseResults = FSharpParseFileResults(diagnostics=parseDiags, input=parseTree, parseHadErrors=false, dependencyFiles=builder.AllDependenciesDeprecated)
                let loadClosure = scriptClosureCache.TryGet(AnyCallerThread, options)
                let typedResults = 
                    FSharpCheckFileResults.Make
                        (filename, 
                            options.ProjectFileName, 
                            tcProj.TcConfig, 
                            tcProj.TcGlobals, 
                            options.IsIncompleteTypeCheckEnvironment, 
                            builder, 
                            options,
                            Array.ofList tcDependencyFiles, 
                            creationDiags, 
                            parseResults.Diagnostics, 
                            tcErrors,
                            keepAssemblyContents,
                            Option.get latestCcuSigForFile, 
                            tcState.Ccu, 
                            tcProj.TcImports, 
                            tcEnvAtEnd.AccessRights,
                            tcResolutions, 
                            tcSymbolUses,
                            tcEnvAtEnd.NameEnv,
                            loadClosure, 
                            latestImplementationFile,
                            tcOpenDeclarations) 
                return (parseResults, typedResults)
          }

    member _.FindReferencesInFile(filename: string, options: FSharpProjectOptions, symbol: FSharpSymbol, canInvalidateProject: bool, userOpName: string) =
        node {
            let! builderOpt, _ = getOrCreateBuilderWithInvalidationFlag (options, canInvalidateProject, userOpName)
            match builderOpt with
            | None -> return Seq.empty
            | Some builder -> 
                if builder.ContainsFile filename then
                    let! checkResults = builder.GetFullCheckResultsAfterFileInProject (filename)
                    let! keyStoreOpt = checkResults.GetOrComputeItemKeyStoreIfEnabled()
                    match keyStoreOpt with
                    | None -> return Seq.empty
                    | Some reader -> return reader.FindAll symbol.Item
                else
                    return Seq.empty
        }


    member _.GetSemanticClassificationForFile(filename: string, options: FSharpProjectOptions, userOpName: string) =
        node {
            let! builderOpt, _ = getOrCreateBuilder (options, userOpName)
            match builderOpt with
            | None -> return None
            | Some builder -> 
                let! checkResults = builder.GetFullCheckResultsAfterFileInProject (filename)
                let! scopt = checkResults.GetOrComputeSemanticClassificationIfEnabled()
                match scopt with
                | None -> return None
                | Some sc -> return Some (sc.GetView ())
        }

    /// Try to get recent approximate type check results for a file. 
    member _.TryGetRecentCheckResultsForFile(filename: string, options:FSharpProjectOptions, sourceText: ISourceText option, _userOpName: string) =
        match sourceText with 
        | Some sourceText -> 
            let hash = sourceText.GetHashCode() |> int64
            let resOpt = parseCacheLock.AcquireLock(fun ltok -> checkFileInProjectCache.TryGet(ltok,(filename,hash,options)))
            match resOpt with
            | Some res ->
                match res.TryPeekValue() with
                | ValueSome(a,b,c,_) -> 
                    Some(a,b,c)
                | ValueNone ->
                    None
            | None ->
                None
        | None -> 
            None

    /// Parse and typecheck the whole project (the implementation, called recursively as project graph is evaluated)
    member private _.ParseAndCheckProjectImpl(options, userOpName) =
      node {
        let! builderOpt,creationDiags = getOrCreateBuilder (options, userOpName)
        match builderOpt with 
        | None -> 
            return FSharpCheckProjectResults (options.ProjectFileName, None, keepAssemblyContents, creationDiags, None)
        | Some builder -> 
            let! (tcProj, ilAssemRef, tcAssemblyDataOpt, tcAssemblyExprOpt) = builder.GetFullCheckResultsAndImplementationsForProject()
            let errorOptions = tcProj.TcConfig.errorSeverityOptions
            let fileName = TcGlobals.DummyFileNameForRangesWithoutASpecificLocation

            // Although we do not use 'tcInfoExtras', computing it will make sure we get an extra info.
            let! tcInfo, _tcInfoExtras = tcProj.GetOrComputeTcInfoWithExtras()

            let topAttribs = tcInfo.topAttribs
            let tcState = tcInfo.tcState
            let tcEnvAtEnd = tcInfo.tcEnvAtEndOfFile
            let tcErrors = tcInfo.TcErrors
            let tcDependencyFiles = tcInfo.tcDependencyFiles
            let diagnostics =
                [| yield! creationDiags;
                    yield! DiagnosticHelpers.CreateDiagnostics (errorOptions, true, fileName, tcErrors, suggestNamesForErrors) |]
            let results = 
                FSharpCheckProjectResults
                    (options.ProjectFileName,
                    Some tcProj.TcConfig,
                    keepAssemblyContents,
                    diagnostics, 
                    Some(tcProj.TcGlobals, tcProj.TcImports, tcState.Ccu, tcState.CcuSig, 
                        (Choice1Of2 builder), topAttribs, tcAssemblyDataOpt, ilAssemRef, 
                        tcEnvAtEnd.AccessRights, tcAssemblyExprOpt,
                        Array.ofList tcDependencyFiles,
                        options))
            return results
      }

    member _.GetAssemblyData(options, userOpName) =
        node {
            let! builderOpt,_ = getOrCreateBuilder (options, userOpName)
            match builderOpt with 
            | None -> 
                return None
            | Some builder -> 
                let! (_, _, tcAssemblyDataOpt, _) = builder.GetCheckResultsAndImplementationsForProject()
                return tcAssemblyDataOpt
        }

    /// Get the timestamp that would be on the output if fully built immediately
    member private _.TryGetLogicalTimeStampForProject(cache, options) =
        match tryGetBuilderNode options with
        | Some lazyWork -> 
            match lazyWork.TryPeekValue() with
            | ValueSome (Some builder, _) ->
                Some(builder.GetLogicalTimeStampForProject(cache))
            | _ ->
                None
        | _ -> 
            None

    /// Parse and typecheck the whole project.
    member bc.ParseAndCheckProject(options, userOpName) =
        bc.ParseAndCheckProjectImpl(options, userOpName)

    member _.GetProjectOptionsFromScript(filename, sourceText, previewEnabled, loadedTimeStamp, otherFlags, useFsiAuxLib: bool option, useSdkRefs: bool option, sdkDirOverride: string option, assumeDotNetFramework: bool option, optionsStamp: int64 option, _userOpName) = 
          cancellable {
            use errors = new ErrorScope()

            // Do we add a reference to FSharp.Compiler.Interactive.Settings by default?
            let useFsiAuxLib = defaultArg useFsiAuxLib true
            let useSdkRefs =  defaultArg useSdkRefs true
            let reduceMemoryUsage = ReduceMemoryFlag.Yes
            let previewEnabled = defaultArg previewEnabled false

            // Do we assume .NET Framework references for scripts?
            let assumeDotNetFramework = defaultArg assumeDotNetFramework true
            let extraFlags =
                if previewEnabled then
                    [| "--langversion:preview" |]
                else
                    [||]
            let otherFlags = defaultArg otherFlags extraFlags
            let useSimpleResolution = 
#if ENABLE_MONO_SUPPORT
                runningOnMono || otherFlags |> Array.exists (fun x -> x = "--simpleresolution")
#else
                true
#endif
            let loadedTimeStamp = defaultArg loadedTimeStamp DateTime.MaxValue // Not 'now', we don't want to force reloading
            let applyCompilerOptions tcConfigB  = 
                let fsiCompilerOptions = CompilerOptions.GetCoreFsiCompilerOptions tcConfigB 
                CompilerOptions.ParseCompilerOptions (ignore, fsiCompilerOptions, Array.toList otherFlags)

            let loadClosure =
                LoadClosure.ComputeClosureOfScriptText(legacyReferenceResolver, 
                    FSharpCheckerResultsSettings.defaultFSharpBinariesDir, filename, sourceText, 
                    CodeContext.Editing, useSimpleResolution, useFsiAuxLib, useSdkRefs, sdkDirOverride, new Lexhelp.LexResourceManager(), 
                    applyCompilerOptions, assumeDotNetFramework, 
                    tryGetMetadataSnapshot, reduceMemoryUsage, dependencyProviderForScripts)

            let otherFlags = 
                [| yield "--noframework"; yield "--warn:3";
                   yield! otherFlags 
                   for r in loadClosure.References do yield "-r:" + fst r
                   for (code,_) in loadClosure.NoWarns do yield "--nowarn:" + code
                |]

            let options = 
                {
                    ProjectFileName = filename + ".fsproj" // Make a name that is unique in this directory.
                    ProjectId = None
                    SourceFiles = loadClosure.SourceFiles |> List.map fst |> List.toArray
                    OtherOptions = otherFlags 
                    ReferencedProjects= [| |]  
                    IsIncompleteTypeCheckEnvironment = false
                    UseScriptResolutionRules = true 
                    LoadTime = loadedTimeStamp
                    UnresolvedReferences = Some (FSharpUnresolvedReferencesSet(loadClosure.UnresolvedReferences))
                    OriginalLoadReferences = loadClosure.OriginalLoadReferences
                    Stamp = optionsStamp
                }
            scriptClosureCache.Set(AnyCallerThread, options, loadClosure) // Save the full load closure for later correlation.
            let diags = loadClosure.LoadClosureRootFileDiagnostics |> List.map (fun (exn, isError) -> FSharpDiagnostic.CreateFromException(exn, isError, range.Zero, false))
            return options, (diags @ errors.Diagnostics)
          }
          |> Cancellable.toAsync
            
    member bc.InvalidateConfiguration(options: FSharpProjectOptions, userOpName) =
        if incrementalBuildersCache.ContainsSimilarKey (AnyCallerThread, options) then
            let _ = createBuilderNode (options, userOpName, CancellationToken.None)
            ()

    member bc.ClearCache(options: seq<FSharpProjectOptions>, _userOpName) =
        lock gate (fun () ->
            options
            |> Seq.iter (fun options -> incrementalBuildersCache.RemoveAnySimilar(AnyCallerThread, options))
        )

    member _.NotifyProjectCleaned (options: FSharpProjectOptions, userOpName) =
        async {
            let! ct = Async.CancellationToken
            // If there was a similar entry (as there normally will have been) then re-establish an empty builder .  This 
            // is a somewhat arbitrary choice - it will have the effect of releasing memory associated with the previous 
            // builder, but costs some time.
            if incrementalBuildersCache.ContainsSimilarKey (AnyCallerThread, options) then
                let _ = createBuilderNode (options, userOpName, ct)
                ()
        }

    member _.BeforeBackgroundFileCheck = beforeFileChecked.Publish

    member _.FileParsed = fileParsed.Publish

    member _.FileChecked = fileChecked.Publish

    member _.ProjectChecked = projectChecked.Publish

    member _.ClearCachesAsync (_userOpName) =
        async {
            return
                lock gate (fun () ->
                    parseCacheLock.AcquireLock (fun ltok -> 
                        checkFileInProjectCache.Clear(ltok)
                        parseFileCache.Clear(ltok))
                    incrementalBuildersCache.Clear(AnyCallerThread)
                    frameworkTcImportsCache.Clear()
                    scriptClosureCache.Clear (AnyCallerThread)
                )
        }

    member _.DownsizeCaches(_userOpName) =
        async {
            return
                lock gate (fun () ->
                    parseCacheLock.AcquireLock (fun ltok -> 
                        checkFileInProjectCache.Resize(ltok, newKeepStrongly=1)
                        parseFileCache.Resize(ltok, newKeepStrongly=1))
                    incrementalBuildersCache.Resize(AnyCallerThread, newKeepStrongly=1, newKeepMax=1)
                    frameworkTcImportsCache.Downsize()
                    scriptClosureCache.Resize(AnyCallerThread,newKeepStrongly=1, newKeepMax=1)
                )
        }
         
    member _.FrameworkImportsCache = frameworkTcImportsCache

    static member ActualParseFileCount = actualParseFileCount

    static member ActualCheckFileCount = actualCheckFileCount


[<Sealed; AutoSerializable(false)>]
// There is typically only one instance of this type in an IDE process.
type FSharpChecker(legacyReferenceResolver, 
                    projectCacheSize, 
                    keepAssemblyContents,
                    keepAllBackgroundResolutions,
                    tryGetMetadataSnapshot,
                    suggestNamesForErrors,
                    keepAllBackgroundSymbolUses,
                    enableBackgroundItemKeyStoreAndSemanticClassification,
                    enablePartialTypeChecking) =

    let backgroundCompiler =
        BackgroundCompiler(
            legacyReferenceResolver,
            projectCacheSize,
            keepAssemblyContents,
            keepAllBackgroundResolutions,
            tryGetMetadataSnapshot,
            suggestNamesForErrors,
            keepAllBackgroundSymbolUses,
            enableBackgroundItemKeyStoreAndSemanticClassification,
            enablePartialTypeChecking)

    static let globalInstance = lazy FSharpChecker.Create()
            
    // STATIC ROOT: FSharpLanguageServiceTestable.FSharpChecker.braceMatchCache. Most recently used cache for brace matching. Accessed on the
    // background UI thread, not on the compiler thread.
    //
    // This cache is safe for concurrent access.
    let braceMatchCache = MruCache<AnyCallerThreadToken,_,_>(braceMatchCacheSize, areSimilar = AreSimilarForParsing, areSame = AreSameForParsing) 

    let mutable maxMemoryReached = false

    let mutable maxMB = maxMBDefault

    let maxMemEvent = new Event<unit>()

    /// Instantiate an interactive checker.    
    static member Create(
                         ?projectCacheSize, 
                         ?keepAssemblyContents, 
                         ?keepAllBackgroundResolutions, 
                         ?legacyReferenceResolver, 
                         ?tryGetMetadataSnapshot, 
                         ?suggestNamesForErrors, 
                         ?keepAllBackgroundSymbolUses, 
                         ?enableBackgroundItemKeyStoreAndSemanticClassification, 
                         ?enablePartialTypeChecking) = 

        let legacyReferenceResolver = 
            match legacyReferenceResolver with
            | Some rr -> rr
            | None -> SimulatedMSBuildReferenceResolver.getResolver()

        let keepAssemblyContents = defaultArg keepAssemblyContents false
        let keepAllBackgroundResolutions = defaultArg keepAllBackgroundResolutions true
        let projectCacheSizeReal = defaultArg projectCacheSize projectCacheSizeDefault
        let tryGetMetadataSnapshot = defaultArg tryGetMetadataSnapshot (fun _ -> None)
        let suggestNamesForErrors = defaultArg suggestNamesForErrors false
        let keepAllBackgroundSymbolUses = defaultArg keepAllBackgroundSymbolUses true
        let enableBackgroundItemKeyStoreAndSemanticClassification = defaultArg enableBackgroundItemKeyStoreAndSemanticClassification false
        let enablePartialTypeChecking = defaultArg enablePartialTypeChecking false

        if keepAssemblyContents && enablePartialTypeChecking then
            invalidArg "enablePartialTypeChecking" "'keepAssemblyContents' and 'enablePartialTypeChecking' cannot be both enabled."

        FSharpChecker(legacyReferenceResolver,
            projectCacheSizeReal,
            keepAssemblyContents,
            keepAllBackgroundResolutions,
            tryGetMetadataSnapshot,
            suggestNamesForErrors,
            keepAllBackgroundSymbolUses,
            enableBackgroundItemKeyStoreAndSemanticClassification,
            enablePartialTypeChecking)

    member _.ReferenceResolver = legacyReferenceResolver

    member _.MatchBraces(filename, sourceText: ISourceText, options: FSharpParsingOptions, ?userOpName: string) =
        let userOpName = defaultArg userOpName "Unknown"
        let hash = sourceText.GetHashCode() |> int64
        async {
            match braceMatchCache.TryGet(AnyCallerThread, (filename, hash, options)) with
            | Some res -> return res
            | None ->
                let res = ParseAndCheckFile.matchBraces(sourceText, filename, options, userOpName, suggestNamesForErrors)
                braceMatchCache.Set(AnyCallerThread, (filename, hash, options), res)
                return res
        }

    member ic.MatchBraces(filename, source: string, options: FSharpProjectOptions, ?userOpName: string) =
        let userOpName = defaultArg userOpName "Unknown"
        let parsingOptions, _ = ic.GetParsingOptionsFromProjectOptions(options)
        ic.MatchBraces(filename, SourceText.ofString source, parsingOptions, userOpName)

    member ic.GetParsingOptionsFromProjectOptions(options): FSharpParsingOptions * _ =
        let sourceFiles = List.ofArray options.SourceFiles
        let argv = List.ofArray options.OtherOptions
        ic.GetParsingOptionsFromCommandLineArgs(sourceFiles, argv, options.UseScriptResolutionRules)

    member ic.ParseFile(filename, sourceText, options, ?cache, ?userOpName: string) =
        let cache = defaultArg cache true
        let userOpName = defaultArg userOpName "Unknown"
        ic.CheckMaxMemoryReached()
        backgroundCompiler.ParseFile(filename, sourceText, options, cache, userOpName)

    member ic.ParseFileInProject(filename, source: string, options, ?cache: bool, ?userOpName: string) =
        let parsingOptions, _ = ic.GetParsingOptionsFromProjectOptions(options)
        ic.ParseFile(filename, SourceText.ofString source, parsingOptions, ?cache=cache, ?userOpName=userOpName)

    member _.GetBackgroundParseResultsForFileInProject (filename,options, ?userOpName: string) =
        let userOpName = defaultArg userOpName "Unknown"
        backgroundCompiler.GetBackgroundParseResultsForFileInProject(filename, options, userOpName)
        |> Async.AwaitNodeCode
        
    member _.GetBackgroundCheckResultsForFileInProject (filename,options, ?userOpName: string) =
        let userOpName = defaultArg userOpName "Unknown"
        backgroundCompiler.GetBackgroundCheckResultsForFileInProject(filename,options, userOpName)
        |> Async.AwaitNodeCode
        
    /// Try to get recent approximate type check results for a file. 
    member _.TryGetRecentCheckResultsForFile(filename: string, options:FSharpProjectOptions, ?sourceText, ?userOpName: string) =
        let userOpName = defaultArg userOpName "Unknown"
        backgroundCompiler.TryGetRecentCheckResultsForFile(filename,options,sourceText,userOpName)

    member _.Compile(argv: string[], ?userOpName: string) =
        let _userOpName = defaultArg userOpName "Unknown"
        async {
            let ctok = CompilationThreadToken()
            return CompileHelpers.compileFromArgs (ctok, argv, legacyReferenceResolver, None, None)
        }

    member _.Compile (ast:ParsedInput list, assemblyName:string, outFile:string, dependencies:string list, ?pdbFile:string, ?executable:bool, ?noframework:bool, ?userOpName: string) =
      let _userOpName = defaultArg userOpName "Unknown"
      async {
        let ctok = CompilationThreadToken()
        let noframework = defaultArg noframework false
        return CompileHelpers.compileFromAsts (ctok, legacyReferenceResolver, ast, assemblyName, outFile, dependencies, noframework, pdbFile, executable, None, None)
      }

    member _.CompileToDynamicAssembly (otherFlags: string[], execute: (TextWriter * TextWriter) option, ?userOpName: string)  = 
      let _userOpName = defaultArg userOpName "Unknown"
      async {
        let ctok = CompilationThreadToken()
        CompileHelpers.setOutputStreams execute
        
        // References used to capture the results of compilation
        let tcImportsRef = ref None
        let assemblyBuilderRef = ref None
        let tcImportsCapture = Some (fun tcImports -> tcImportsRef := Some tcImports)

        // Function to generate and store the results of compilation 
        let debugInfo =  otherFlags |> Array.exists (fun arg -> arg = "-g" || arg = "--debug:+" || arg = "/debug:+")
        let dynamicAssemblyCreator = Some (CompileHelpers.createDynamicAssembly (debugInfo, tcImportsRef, execute.IsSome, assemblyBuilderRef))

        // Perform the compilation, given the above capturing function.
        let errorsAndWarnings, result = CompileHelpers.compileFromArgs (ctok, otherFlags, legacyReferenceResolver, tcImportsCapture, dynamicAssemblyCreator)

        // Retrieve and return the results
        let assemblyOpt = 
            match assemblyBuilderRef.Value with 
            | None -> None
            | Some a ->  Some (a :> System.Reflection.Assembly)

        return errorsAndWarnings, result, assemblyOpt
      }

    member _.CompileToDynamicAssembly (ast:ParsedInput list, assemblyName:string, dependencies:string list, execute: (TextWriter * TextWriter) option, ?debug:bool, ?noframework:bool, ?userOpName: string) =
      let _userOpName = defaultArg userOpName "Unknown"
      async {
        let ctok = CompilationThreadToken()
        CompileHelpers.setOutputStreams execute

        // References used to capture the results of compilation
        let tcImportsRef = ref (None: TcImports option)
        let assemblyBuilderRef = ref None
        let tcImportsCapture = Some (fun tcImports -> tcImportsRef := Some tcImports)

        let debugInfo = defaultArg debug false
        let noframework = defaultArg noframework false
        let location = Path.Combine(FileSystem.GetTempPathShim(),"test"+string(hash assemblyName))
        try Directory.CreateDirectory(location) |> ignore with _ -> ()

        let outFile = Path.Combine(location, assemblyName + ".dll")

        // Function to generate and store the results of compilation 
        let dynamicAssemblyCreator = Some (CompileHelpers.createDynamicAssembly (debugInfo, tcImportsRef, execute.IsSome, assemblyBuilderRef))

        // Perform the compilation, given the above capturing function.
        let errorsAndWarnings, result = 
            CompileHelpers.compileFromAsts (ctok, legacyReferenceResolver, ast, assemblyName, outFile, dependencies, noframework, None, Some execute.IsSome, tcImportsCapture, dynamicAssemblyCreator)

        // Retrieve and return the results
        let assemblyOpt = 
            match assemblyBuilderRef.Value with 
            | None -> None
            | Some a ->  Some (a :> System.Reflection.Assembly)

        return errorsAndWarnings, result, assemblyOpt
      }

    /// This function is called when the entire environment is known to have changed for reasons not encoded in the ProjectOptions of any project/compilation.
    /// For example, the type provider approvals file may have changed.
    member ic.InvalidateAll() =
        ic.ClearCaches()
            
    member _.ClearCachesAsync(?userOpName: string) =
        let utok = AnyCallerThread
        let userOpName = defaultArg userOpName "Unknown"
        braceMatchCache.Clear(utok)
        backgroundCompiler.ClearCachesAsync(userOpName) 

    member ic.ClearCaches(?userOpName) =
        ic.ClearCachesAsync(?userOpName=userOpName) |> Async.Start // this cache clearance is not synchronous, it will happen when the background op gets run

    member _.CheckMaxMemoryReached() =
        if not maxMemoryReached && System.GC.GetTotalMemory(false) > int64 maxMB * 1024L * 1024L then 
            Trace.TraceWarning("!!!!!!!! MAX MEMORY REACHED, DOWNSIZING F# COMPILER CACHES !!!!!!!!!!!!!!!")
            // If the maxMB limit is reached, drastic action is taken
            //   - reduce strong cache sizes to a minimum
            let userOpName = "MaxMemoryReached"
            maxMemoryReached <- true
            braceMatchCache.Resize(AnyCallerThread, newKeepStrongly=10)
            backgroundCompiler.DownsizeCaches(userOpName) |> Async.RunSynchronously
            maxMemEvent.Trigger( () )

    // This is for unit testing only
    member ic.ClearLanguageServiceRootCachesAndCollectAndFinalizeAllTransients() =
        ic.ClearCachesAsync() |> Async.RunSynchronously
        System.GC.Collect()
        System.GC.WaitForPendingFinalizers() 
        FxResolver.ClearStaticCaches()
            
    /// This function is called when the configuration is known to have changed for reasons not encoded in the ProjectOptions.
    /// For example, dependent references may have been deleted or created.
    member _.InvalidateConfiguration(options: FSharpProjectOptions, ?userOpName: string) =
        let userOpName = defaultArg userOpName "Unknown"
        backgroundCompiler.InvalidateConfiguration(options, userOpName)

    /// Clear the internal cache of the given projects.
    member _.ClearCache(options: FSharpProjectOptions seq, ?userOpName: string) =
        let userOpName = defaultArg userOpName "Unknown"
        backgroundCompiler.ClearCache(options, userOpName)

    /// This function is called when a project has been cleaned, and thus type providers should be refreshed.
    member _.NotifyProjectCleaned(options: FSharpProjectOptions, ?userOpName: string) =
        let userOpName = defaultArg userOpName "Unknown"
        backgroundCompiler.NotifyProjectCleaned (options, userOpName)
              
    /// Typecheck a source code file, returning a handle to the results of the 
    /// parse including the reconstructed types in the file.
    member _.CheckFileInProjectAllowingStaleCachedResults(parseResults:FSharpParseFileResults, filename:string, fileVersion:int, source:string, options:FSharpProjectOptions, ?userOpName: string) =        
        let userOpName = defaultArg userOpName "Unknown"
        backgroundCompiler.CheckFileInProjectAllowingStaleCachedResults(parseResults,filename,fileVersion,SourceText.ofString source,options,userOpName)
        |> Async.AwaitNodeCode

    /// Typecheck a source code file, returning a handle to the results of the 
    /// parse including the reconstructed types in the file.
    member ic.CheckFileInProject(parseResults:FSharpParseFileResults, filename:string, fileVersion:int, sourceText:ISourceText, options:FSharpProjectOptions, ?userOpName: string) =        
        let userOpName = defaultArg userOpName "Unknown"
        ic.CheckMaxMemoryReached()
        backgroundCompiler.CheckFileInProject(parseResults,filename,fileVersion,sourceText,options,userOpName)
        |> Async.AwaitNodeCode

    /// Typecheck a source code file, returning a handle to the results of the 
    /// parse including the reconstructed types in the file.
    member ic.ParseAndCheckFileInProject(filename:string, fileVersion:int, sourceText:ISourceText, options:FSharpProjectOptions, ?userOpName: string) =        
        let userOpName = defaultArg userOpName "Unknown"
        ic.CheckMaxMemoryReached()
        backgroundCompiler.ParseAndCheckFileInProject(filename, fileVersion, sourceText, options, userOpName)
        |> Async.AwaitNodeCode
            
    member ic.ParseAndCheckProject(options, ?userOpName: string) =
        let userOpName = defaultArg userOpName "Unknown"
        ic.CheckMaxMemoryReached()
        backgroundCompiler.ParseAndCheckProject(options, userOpName)
        |> Async.AwaitNodeCode

    member ic.FindBackgroundReferencesInFile(filename:string, options: FSharpProjectOptions, symbol: FSharpSymbol, ?canInvalidateProject: bool, ?userOpName: string) =
        let canInvalidateProject = defaultArg canInvalidateProject true
        let userOpName = defaultArg userOpName "Unknown"
        ic.CheckMaxMemoryReached()
        backgroundCompiler.FindReferencesInFile(filename, options, symbol, canInvalidateProject, userOpName)
        |> Async.AwaitNodeCode

    member ic.GetBackgroundSemanticClassificationForFile(filename:string, options: FSharpProjectOptions, ?userOpName) =
        let userOpName = defaultArg userOpName "Unknown"
        ic.CheckMaxMemoryReached()
        backgroundCompiler.GetSemanticClassificationForFile(filename, options, userOpName)
        |> Async.AwaitNodeCode

    /// For a given script file, get the ProjectOptions implied by the #load closure
    member _.GetProjectOptionsFromScript(filename, source, ?previewEnabled, ?loadedTimeStamp, ?otherFlags, ?useFsiAuxLib, ?useSdkRefs, ?assumeDotNetFramework, ?sdkDirOverride, ?optionsStamp: int64, ?userOpName: string) = 
        let userOpName = defaultArg userOpName "Unknown"
        backgroundCompiler.GetProjectOptionsFromScript(filename, source, previewEnabled, loadedTimeStamp, otherFlags, useFsiAuxLib, useSdkRefs, sdkDirOverride, assumeDotNetFramework, optionsStamp, userOpName)

    member _.GetProjectOptionsFromCommandLineArgs(projectFileName, argv, ?loadedTimeStamp, ?isInteractive, ?isEditing) = 
        let isEditing = defaultArg isEditing false
        let isInteractive = defaultArg isInteractive false
        let loadedTimeStamp = defaultArg loadedTimeStamp DateTime.MaxValue // Not 'now', we don't want to force reloading
        let argv = 
            let define = if isInteractive then "--define:INTERACTIVE" else "--define:COMPILED"
            Array.append argv [| define |]
        let argv = 
            if isEditing then Array.append argv [| "--define:EDITING" |] else argv
        { ProjectFileName = projectFileName
          ProjectId = None
          SourceFiles = [| |] // the project file names will be inferred from the ProjectOptions
          OtherOptions = argv 
          ReferencedProjects= [| |]  
          IsIncompleteTypeCheckEnvironment = false
          UseScriptResolutionRules = false
          LoadTime = loadedTimeStamp
          UnresolvedReferences = None
          OriginalLoadReferences=[]
          Stamp = None }

    member _.GetParsingOptionsFromCommandLineArgs(sourceFiles, argv, ?isInteractive, ?isEditing) =
        let isEditing = defaultArg isEditing false
        let isInteractive = defaultArg isInteractive false
        use errorScope = new ErrorScope()
        let tcConfigB = 
            TcConfigBuilder.CreateNew(legacyReferenceResolver,
                defaultFSharpBinariesDir=FSharpCheckerResultsSettings.defaultFSharpBinariesDir,
                reduceMemoryUsage=ReduceMemoryFlag.Yes,
                implicitIncludeDir="",
                isInteractive=isInteractive,
                isInvalidationSupported=false,
                defaultCopyFSharpCore=CopyFSharpCoreFlag.No,
                tryGetMetadataSnapshot=tryGetMetadataSnapshot,
                sdkDirOverride=None,
                rangeForErrors=range0)

        // These defines are implied by the F# compiler
        tcConfigB.conditionalCompilationDefines <- 
            let define = if isInteractive then "INTERACTIVE" else "COMPILED"
            define :: tcConfigB.conditionalCompilationDefines
        if isEditing then 
            tcConfigB.conditionalCompilationDefines <- "EDITING":: tcConfigB.conditionalCompilationDefines

        // Apply command-line arguments and collect more source files if they are in the arguments
        let sourceFilesNew = ApplyCommandLineArgs(tcConfigB, sourceFiles, argv)
        FSharpParsingOptions.FromTcConfigBuilder(tcConfigB, Array.ofList sourceFilesNew, isInteractive), errorScope.Diagnostics

    member ic.GetParsingOptionsFromCommandLineArgs(argv, ?isInteractive: bool, ?isEditing) =
        ic.GetParsingOptionsFromCommandLineArgs([], argv, ?isInteractive=isInteractive, ?isEditing=isEditing)

    member _.BeforeBackgroundFileCheck  = backgroundCompiler.BeforeBackgroundFileCheck

    member _.FileParsed  = backgroundCompiler.FileParsed

    member _.FileChecked  = backgroundCompiler.FileChecked

    member _.ProjectChecked = backgroundCompiler.ProjectChecked

    static member ActualParseFileCount = BackgroundCompiler.ActualParseFileCount

    static member ActualCheckFileCount = BackgroundCompiler.ActualCheckFileCount
          
    member _.MaxMemoryReached = maxMemEvent.Publish

    member _.MaxMemory with get() = maxMB and set v = maxMB <- v
    
    static member Instance with get() = globalInstance.Force()

    member internal _.FrameworkImportsCache = backgroundCompiler.FrameworkImportsCache

    /// Tokenize a single line, returning token information and a tokenization state represented by an integer
    member _.TokenizeLine (line: string, state: FSharpTokenizerLexState) = 
        let tokenizer = FSharpSourceTokenizer([], None)
        let lineTokenizer = tokenizer.CreateLineTokenizer line
        let mutable state = (None, state)
        let tokens = 
            [| while (state <- lineTokenizer.ScanToken (snd state); (fst state).IsSome) do
                    yield (fst state).Value |]
        tokens, snd state 

    /// Tokenize an entire file, line by line
    member x.TokenizeFile (source: string) : FSharpTokenInfo[][] = 
        let lines = source.Split('\n')
        let tokens = 
            [| let mutable state = FSharpTokenizerLexState.Initial
               for line in lines do 
                   let tokens, n = x.TokenizeLine(line, state) 
                   state <- n 
                   yield tokens |]
        tokens

namespace FSharp.Compiler

open System
open System.IO
open Internal.Utilities
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.CompilerConfig
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Text.Range
open FSharp.Compiler.ErrorLogger

type CompilerEnvironment() =
    /// Source file extensions
    static let compilableExtensions = FSharpSigFileSuffixes @ FSharpImplFileSuffixes @ FSharpScriptFileSuffixes

    /// Single file projects extensions
    static let singleFileProjectExtensions = FSharpScriptFileSuffixes

    static member BinFolderOfDefaultFSharpCompiler(?probePoint) =
        FSharpEnvironment.BinFolderOfDefaultFSharpCompiler(probePoint)

    // Legacy entry point, no longer used by FSharp.Editor
    static member DefaultReferencesForOrphanSources assumeDotNetFramework =
        let currentDirectory = Directory.GetCurrentDirectory()
        let fxResolver = FxResolver(assumeDotNetFramework, currentDirectory, rangeForErrors=range0, useSdkRefs=true, isInteractive=false, sdkDirOverride=None)
        let references, _ = fxResolver.GetDefaultReferences (useFsiAuxLib=false)
        references
    
    /// Publish compiler-flags parsing logic. Must be fast because its used by the colorizer.
    static member GetCompilationDefinesForEditing (parsingOptions: FSharpParsingOptions) =
        SourceFileImpl.AdditionalDefinesForUseInEditor(parsingOptions.IsInteractive) @
        parsingOptions.ConditionalCompilationDefines
            
    /// Return true if this is a subcategory of error or warning message that the language service can emit
    static member IsCheckerSupportedSubcategory(subcategory:string) =
        // Beware: This code logic is duplicated in DocumentTask.cs in the language service
        PhasedDiagnostic.IsSubcategoryOfCompile(subcategory)

    /// Return the language ID, which is the expression evaluator id that the
    /// debugger will use.
    static member GetDebuggerLanguageID() =
        System.Guid(0xAB4F38C9u, 0xB6E6us, 0x43baus, 0xBEuy, 0x3Buy, 0x58uy, 0x08uy, 0x0Buy, 0x2Cuy, 0xCCuy, 0xE3uy)
        
    static member IsScriptFile (fileName: string) = ParseAndCheckInputs.IsScript fileName

    /// Whether or not this file is compilable
    static member IsCompilable file =
        let ext = Path.GetExtension file
        compilableExtensions |> List.exists(fun e->0 = String.Compare(e, ext, StringComparison.OrdinalIgnoreCase))

    /// Whether or not this file should be a single-file project
    static member MustBeSingleFileProject file =
        let ext = Path.GetExtension file
        singleFileProjectExtensions |> List.exists(fun e-> 0 = String.Compare(e, ext, StringComparison.OrdinalIgnoreCase))

