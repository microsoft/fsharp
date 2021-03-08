// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.CodeAnalysis

open System
open System.Collections.Generic
open System.Collections.Immutable
open System.IO
open System.Runtime.InteropServices
open System.Threading
open Internal.Utilities.Library 
open Internal.Utilities.Library.Extras 
open FSharp.Compiler
open FSharp.Compiler.AbstractIL
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AbstractIL.ILBinaryReader
open FSharp.Compiler.CheckExpressions
open FSharp.Compiler.CheckDeclarations
open FSharp.Compiler.CompilerConfig
open FSharp.Compiler.CompilerDiagnostics
open FSharp.Compiler.CompilerGlobalState
open FSharp.Compiler.CompilerImports
open FSharp.Compiler.CompilerOptions
open FSharp.Compiler.CreateILModule
open FSharp.Compiler.DependencyManager
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.EditorServices
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.IO
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.NameResolution
open FSharp.Compiler.ParseAndCheckInputs
open FSharp.Compiler.ScriptClosure
open FSharp.Compiler.Syntax
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Compiler.TypedTree 
open FSharp.Compiler.TypedTreeOps

open Internal.Utilities
open Internal.Utilities.Collections

[<AutoOpen>]
module internal IncrementalBuild =

    let mutable injectCancellationFault = false
    let LocallyInjectCancellationFault() = 
        injectCancellationFault <- true
        { new IDisposable with member _.Dispose() =  injectCancellationFault <- false }

// Record the most recent IncrementalBuilder events, so we can more easily unit test/debug the 
// 'incremental' behavior of the product.
module IncrementalBuilderEventTesting = 

    type internal FixedLengthMRU<'T>() =
        let MAX = 400   // Length of the MRU.  For our current unit tests, 400 is enough.
        let data = Array.create MAX None
        let mutable curIndex = 0
        let mutable numAdds = 0
        // called by the product, to note when a parse/typecheck happens for a file
        member this.Add(filename:'T) =
            numAdds <- numAdds + 1
            data.[curIndex] <- Some filename
            curIndex <- (curIndex + 1) % MAX
        member this.CurrentEventNum = numAdds
        // called by unit tests, returns 'n' most recent additions.
        member this.MostRecentList(n: int) : list<'T> =
            if n < 0 || n > MAX then
                raise <| new System.ArgumentOutOfRangeException("n", sprintf "n must be between 0 and %d, inclusive, but got %d" MAX n)
            let mutable remaining = n
            let mutable s = []
            let mutable i = curIndex - 1
            while remaining <> 0 do
                if i < 0 then
                    i <- MAX - 1
                match data.[i] with
                | None -> ()
                | Some x -> s <- x :: s
                i <- i - 1
                remaining <- remaining - 1
            List.rev s

    type IBEvent =
        | IBEParsed of string // filename
        | IBETypechecked of string // filename
        | IBECreated

    // ++GLOBAL MUTABLE STATE FOR TESTING++
    let MRU = new FixedLengthMRU<IBEvent>()  
    let GetMostRecentIncrementalBuildEvents n = MRU.MostRecentList n
    let GetCurrentIncrementalBuildEventNum() = MRU.CurrentEventNum 

module Tc = FSharp.Compiler.CheckExpressions

// This module is only here to contain the SyntaxTree type as to avoid amiguity with the module FSharp.Compiler.Syntax.
[<AutoOpen>]
module IncrementalBuildSyntaxTree =

    /// Information needed to lazily parse a file to get a ParsedInput. Internally uses a weak cache.
    [<Sealed>]
    type SyntaxTree (tcConfig: TcConfig, fileParsed: Event<string>, lexResourceManager, sourceRange: range, filename: string, isLastCompiland) =

        let mutable weakCache: WeakReference<_> option = None

        let parse(sigNameOpt: QualifiedNameOfFile option) =
            let errorLogger = CompilationErrorLogger("Parse", tcConfig.errorSeverityOptions)
            // Return the disposable object that cleans up
            use _holder = new CompilationGlobalsScope(errorLogger, BuildPhase.Parse)

            try  
                IncrementalBuilderEventTesting.MRU.Add(IncrementalBuilderEventTesting.IBEParsed filename)
                let lower = String.lowercase filename
                let canSkip = sigNameOpt.IsSome && FSharpImplFileSuffixes |> List.exists (Filename.checkSuffix lower)
                let input = 
                    if canSkip then
                        ParsedInput.ImplFile(
                            ParsedImplFileInput(
                                filename, 
                                false, 
                                sigNameOpt.Value,
                                [],
                                [],
                                [],
                                isLastCompiland
                            )
                        )
                    else
                        ParseOneInputFile(tcConfig, lexResourceManager, [], filename, isLastCompiland, errorLogger, (*retryLocked*)true)

                fileParsed.Trigger filename

                let res = input, sourceRange, filename, errorLogger.GetDiagnostics()
                // If we do not skip parsing the file, then we can cache the real result.
                if not canSkip then
                    weakCache <- Some(WeakReference<_>(res))
                res
            with exn -> 
                let msg = sprintf "unexpected failure in SyntaxTree.parse\nerror = %s" (exn.ToString())
                System.Diagnostics.Debug.Assert(false, msg)
                failwith msg

        /// Parse the given file and return the given input.
        member _.Parse sigNameOpt =
            match weakCache with
            | Some weakCache ->
                match weakCache.TryGetTarget() with
                | true, res -> res
                | _ -> parse sigNameOpt
            | _ -> parse sigNameOpt

        member _.Invalidate() =
            weakCache <- None

        member _.FileName = filename

/// Accumulated results of type checking. The minimum amount of state in order to continue type-checking following files.
[<NoEquality; NoComparison>]
type TcInfo =
    {
        tcState: TcState
        tcEnvAtEndOfFile: TcEnv

        /// Disambiguation table for module names
        moduleNamesDict: ModuleNamesDict

        topAttribs: TopAttribs option

        latestCcuSigForFile: ModuleOrNamespaceType option

        /// Accumulated errors, last file first
        tcErrorsRev:(PhasedDiagnostic * FSharpDiagnosticSeverity)[] list

        tcDependencyFiles: string list

        sigNameOpt: (string * QualifiedNameOfFile) option
    }

    member x.TcErrors = 
        Array.concat (List.rev x.tcErrorsRev)

/// Accumulated results of type checking. Optional data that isn't needed to type-check a file, but needed for more information for in tooling.
[<NoEquality; NoComparison>]
type TcInfoExtras =
    {
      /// Accumulated resolutions, last file first
      tcResolutionsRev: TcResolutions list

      /// Accumulated symbol uses, last file first
      tcSymbolUsesRev: TcSymbolUses list

      /// Accumulated 'open' declarations, last file first
      tcOpenDeclarationsRev: OpenDeclaration[] list

      /// Result of checking most recent file, if any
      latestImplFile: TypedImplFile option
      
      /// If enabled, stores a linear list of ranges and strings that identify an Item(symbol) in a file. Used for background find all references.
      itemKeyStore: ItemKeyStore option
      
      /// If enabled, holds semantic classification information for Item(symbol)s in a file.
      semanticClassificationKeyStore: SemanticClassificationKeyStore option
    }

    member x.TcSymbolUses = 
        List.rev x.tcSymbolUsesRev

/// Accumulated results of type checking.
[<NoEquality; NoComparison>]
type TcInfoState =
    | PartialState of TcInfo
    | FullState of TcInfo * TcInfoExtras

    member this.TcInfo =
        match this with
        | PartialState tcInfo -> tcInfo
        | FullState(tcInfo, _) -> tcInfo

/// Bound model of an underlying syntax and typed tree.
[<Sealed>]
type BoundModel private (tcConfig: TcConfig,
                         tcGlobals: TcGlobals,
                         tcImports: TcImports,
                         keepAssemblyContents, keepAllBackgroundResolutions,
                         keepAllBackgroundSymbolUses,
                         enableBackgroundItemKeyStoreAndSemanticClassification,
                         enablePartialTypeChecking,
                         beforeFileChecked: Event<string>,
                         fileChecked: Event<string>,
                         prevTcInfo: TcInfo,
                         prevTcInfoExtras: (unit -> Eventually<TcInfoExtras option>),
                         syntaxTreeOpt: SyntaxTree option,
                         tcInfoStateOpt: TcInfoState option) =

    let mutable lazyTcInfoState = tcInfoStateOpt
    let gate = obj()

    let defaultTypeCheck () =
        eventually {
            match prevTcInfoExtras() with
            | Eventually.Done(Some prevTcInfoExtras) ->
                return FullState(prevTcInfo, prevTcInfoExtras)
            | _ ->
                return PartialState prevTcInfo
        }

    member _.TcConfig = tcConfig

    member _.TcGlobals = tcGlobals

    member _.TcImports = tcImports

    member _.BackingSignature =
        match syntaxTreeOpt with
        | Some syntaxTree ->
            let sigFileName = Path.ChangeExtension(syntaxTree.FileName, ".fsi")
            match prevTcInfo.sigNameOpt with
            | Some (expectedSigFileName, sigName) when String.Equals(expectedSigFileName, sigFileName, StringComparison.OrdinalIgnoreCase) ->
                Some sigName
            | _ ->
                None
        | _ ->
            None

    member this.Invalidate() =
        lock gate (fun () ->
            let hasSig = this.BackingSignature.IsSome
            match lazyTcInfoState with
            // If partial checking is enabled and we have a backing sig file, then do nothing. The partial state contains the sig state.
            | Some(PartialState _) when enablePartialTypeChecking && hasSig -> ()
            // If partial checking is enabled and we have a backing sig file, then use the partial state. The partial state contains the sig state.
            | Some(FullState(tcInfo, _)) when enablePartialTypeChecking && hasSig -> lazyTcInfoState <- Some(PartialState tcInfo)
            | _ ->
                lazyTcInfoState <- None

            // Always invalidate the syntax tree cache.
            syntaxTreeOpt
            |> Option.iter (fun x -> x.Invalidate())
        )

    member this.GetState(partialCheck: bool) =
        eventually {
            let partialCheck =
                // Only partial check if we have enabled it.
                if enablePartialTypeChecking then partialCheck
                else false

            let mustCheck =
                match lazyTcInfoState, partialCheck with
                | None, _ -> true
                | Some(PartialState _), false -> true
                | _ -> false

            match lazyTcInfoState with
            | Some tcInfoState when not mustCheck -> return tcInfoState
            | _ -> 
                lazyTcInfoState <- None
                let! tcInfoState = this.TypeCheck(partialCheck)
                lazyTcInfoState <- Some tcInfoState
                return tcInfoState
        }

    member this.TryOptionalExtras() =
        eventually {
            let! prevState = this.GetState(false)
            match prevState with
            | FullState(_, prevTcInfoExtras) -> return Some prevTcInfoExtras
            | _ -> return None
        }

    member this.Next(syntaxTree) =
        eventually {
            let! prevState = this.GetState(true)
            return
                BoundModel(
                    tcConfig,
                    tcGlobals,
                    tcImports,
                    keepAssemblyContents, 
                    keepAllBackgroundResolutions, 
                    keepAllBackgroundSymbolUses, 
                    enableBackgroundItemKeyStoreAndSemanticClassification,
                    enablePartialTypeChecking,
                    beforeFileChecked, 
                    fileChecked, 
                    prevState.TcInfo, 
                    (fun () -> this.TryOptionalExtras()), 
                    Some syntaxTree,
                    None)
        }

    member this.Finish(finalTcErrorsRev, finalTopAttribs) =
        eventually {
            let! state = this.GetState(true)

            let finishTcInfo = { state.TcInfo  with tcErrorsRev = finalTcErrorsRev; topAttribs = finalTopAttribs }
            let finishState =
                match state with
                | PartialState(_) -> PartialState(finishTcInfo)
                | FullState(_, tcInfoExtras) -> FullState(finishTcInfo, tcInfoExtras)

            return
                BoundModel(
                    tcConfig,
                    tcGlobals,
                    tcImports,
                    keepAssemblyContents, 
                    keepAllBackgroundResolutions, 
                    keepAllBackgroundSymbolUses, 
                    enableBackgroundItemKeyStoreAndSemanticClassification,
                    enablePartialTypeChecking,
                    beforeFileChecked, 
                    fileChecked, 
                    prevTcInfo, 
                    prevTcInfoExtras, 
                    syntaxTreeOpt,
                    Some finishState)
        }

    member this.GetTcInfo() =
        eventually {
            let! state = this.GetState(true)
            return state.TcInfo
        }

    member this.TryTcInfo = 
        match lazyTcInfoState with
        | Some(state) ->
            match state with
            | FullState(tcInfo, _)
            | PartialState(tcInfo) -> Some tcInfo
        | _ -> None

    member this.GetTcInfoWithExtras() =
        eventually {
            let! state = this.GetState(false)
            match state with
            | FullState(tcInfo, tcInfoExtras) -> return tcInfo, tcInfoExtras
            | PartialState tcInfo ->
                return
                    tcInfo,
                    {
                        tcResolutionsRev = []
                        tcSymbolUsesRev = []
                        tcOpenDeclarationsRev = []
                        latestImplFile = None
                        itemKeyStore = None
                        semanticClassificationKeyStore = None
                    }
        }

    member private this.TypeCheck (partialCheck: bool) =  
        match partialCheck, lazyTcInfoState with
        | true, Some (PartialState _ as state)
        | true, Some (FullState _ as state) -> state |> Eventually.Done
        | false, Some (FullState _ as state) -> state |> Eventually.Done
        | _ ->

        eventually {
            match syntaxTreeOpt with 
            | None -> return! defaultTypeCheck ()
            | Some syntaxTree ->
                let sigNameOpt =
                    if partialCheck then
                        this.BackingSignature
                    else
                        None
                match syntaxTree.Parse sigNameOpt with
                | input, _sourceRange, filename, parseErrors ->

                    IncrementalBuilderEventTesting.MRU.Add(IncrementalBuilderEventTesting.IBETypechecked filename)
                    let capturingErrorLogger = CompilationErrorLogger("TypeCheck", tcConfig.errorSeverityOptions)
                    let errorLogger = GetErrorLoggerFilteringByScopedPragmas(false, GetScopedPragmasForInput input, capturingErrorLogger)

                    // This reinstalls the CompilationGlobalsScope each time the Eventually is restarted, potentially
                    // on a new thread. This is needed because CompilationGlobalsScope installs thread local variables.
                    return! Eventually.reusing (fun () -> new CompilationGlobalsScope (errorLogger, BuildPhase.TypeCheck) :> IDisposable)  <| eventually {

                        beforeFileChecked.Trigger filename
                        let prevModuleNamesDict = prevTcInfo.moduleNamesDict
                        let prevTcState = prevTcInfo.tcState
                        let prevTcErrorsRev = prevTcInfo.tcErrorsRev
                        let prevTcDependencyFiles = prevTcInfo.tcDependencyFiles

                        ApplyMetaCommandsFromInputToTcConfig (tcConfig, input, Path.GetDirectoryName filename, tcImports.DependencyProvider) |> ignore
                        let sink = TcResultsSinkImpl(tcGlobals)
                        let hadParseErrors = not (Array.isEmpty parseErrors)
                        let input, moduleNamesDict = DeduplicateParsedInputModuleName prevModuleNamesDict input

                        Logger.LogBlockMessageStart filename LogCompilerFunctionId.IncrementalBuild_TypeCheck
                        let! (tcEnvAtEndOfFile, topAttribs, implFile, ccuSigForFile), tcState = 
                            TypeCheckOneInputEventually 
                                ((fun () -> hadParseErrors || errorLogger.ErrorCount > 0), 
                                    tcConfig, tcImports, 
                                    tcGlobals, 
                                    None, 
                                    (if partialCheck then TcResultsSink.NoSink else TcResultsSink.WithSink sink), 
                                    prevTcState, input,
                                    partialCheck)
                        Logger.LogBlockMessageStop filename LogCompilerFunctionId.IncrementalBuild_TypeCheck

                        fileChecked.Trigger filename
                        let newErrors = Array.append parseErrors (capturingErrorLogger.GetDiagnostics())

                        let tcEnvAtEndOfFile = if keepAllBackgroundResolutions then tcEnvAtEndOfFile else tcState.TcEnvFromImpls

                        let tcInfo =
                            {
                                tcState = tcState
                                tcEnvAtEndOfFile = tcEnvAtEndOfFile
                                moduleNamesDict = moduleNamesDict
                                latestCcuSigForFile = Some ccuSigForFile
                                tcErrorsRev = newErrors :: prevTcErrorsRev
                                topAttribs = Some topAttribs
                                tcDependencyFiles = filename :: prevTcDependencyFiles
                                sigNameOpt =
                                    match input with
                                    | ParsedInput.SigFile(ParsedSigFileInput(fileName=fileName;qualifiedNameOfFile=qualName)) ->
                                        Some(fileName, qualName)
                                    | _ ->
                                        None
                            }

                        if partialCheck then
                            return PartialState tcInfo
                        else
                            match! prevTcInfoExtras() with
                            | None -> return PartialState tcInfo
                            | Some prevTcInfoOptional ->
                                // Build symbol keys
                                let itemKeyStore, semanticClassification =
                                    if enableBackgroundItemKeyStoreAndSemanticClassification then
                                        Logger.LogBlockMessageStart filename LogCompilerFunctionId.IncrementalBuild_CreateItemKeyStoreAndSemanticClassification
                                        let sResolutions = sink.GetResolutions()
                                        let builder = ItemKeyStoreBuilder()
                                        let preventDuplicates = HashSet({ new IEqualityComparer<struct(pos * pos)> with 
                                                                            member _.Equals((s1, e1): struct(pos * pos), (s2, e2): struct(pos * pos)) = Position.posEq s1 s2 && Position.posEq e1 e2
                                                                            member _.GetHashCode o = o.GetHashCode() })
                                        sResolutions.CapturedNameResolutions
                                        |> Seq.iter (fun cnr ->
                                            let r = cnr.Range
                                            if preventDuplicates.Add struct(r.Start, r.End) then
                                                builder.Write(cnr.Range, cnr.Item))
                                            
                                        let semanticClassification = sResolutions.GetSemanticClassification(tcGlobals, tcImports.GetImportMap(), sink.GetFormatSpecifierLocations(), None)

                                        let sckBuilder = SemanticClassificationKeyStoreBuilder()
                                        sckBuilder.WriteAll semanticClassification

                                        let res = builder.TryBuildAndReset(), sckBuilder.TryBuildAndReset()
                                        Logger.LogBlockMessageStop filename LogCompilerFunctionId.IncrementalBuild_CreateItemKeyStoreAndSemanticClassification
                                        res
                                    else
                                        None, None

                                let tcInfoExtras =
                                    {
                                        /// Only keep the typed interface files when doing a "full" build for fsc.exe, otherwise just throw them away
                                        latestImplFile = if keepAssemblyContents then implFile else None
                                        tcResolutionsRev = (if keepAllBackgroundResolutions then sink.GetResolutions() else TcResolutions.Empty) :: prevTcInfoOptional.tcResolutionsRev
                                        tcSymbolUsesRev = (if keepAllBackgroundSymbolUses then sink.GetSymbolUses() else TcSymbolUses.Empty) :: prevTcInfoOptional.tcSymbolUsesRev
                                        tcOpenDeclarationsRev = sink.GetOpenDeclarations() :: prevTcInfoOptional.tcOpenDeclarationsRev
                                        itemKeyStore = itemKeyStore
                                        semanticClassificationKeyStore = semanticClassification
                                    }

                                return FullState(tcInfo, tcInfoExtras)
                    }
            }

    static member Create(tcConfig: TcConfig,
                         tcGlobals: TcGlobals,
                         tcImports: TcImports,
                         keepAssemblyContents, keepAllBackgroundResolutions,
                         keepAllBackgroundSymbolUses,
                         enableBackgroundItemKeyStoreAndSemanticClassification,
                         enablePartialTypeChecking,
                         beforeFileChecked: Event<string>,
                         fileChecked: Event<string>,
                         prevTcInfo: TcInfo,
                         prevTcInfoExtras: (unit -> Eventually<TcInfoExtras option>),
                         syntaxTreeOpt: SyntaxTree option) =
        BoundModel(tcConfig, tcGlobals, tcImports, 
                      keepAssemblyContents, keepAllBackgroundResolutions, 
                      keepAllBackgroundSymbolUses,
                      enableBackgroundItemKeyStoreAndSemanticClassification,
                      enablePartialTypeChecking,
                      beforeFileChecked,
                      fileChecked,
                      prevTcInfo,
                      prevTcInfoExtras,
                      syntaxTreeOpt,
                      None)
      
/// Global service state
type FrameworkImportsCacheKey = (*resolvedpath*)string list * string * (*TargetFrameworkDirectories*)string list * (*fsharpBinaries*)string * (*langVersion*)decimal

/// Represents a cache of 'framework' references that can be shared between multiple incremental builds
type FrameworkImportsCache(size) = 

    // Mutable collection protected via CompilationThreadToken 
    let frameworkTcImportsCache = AgedLookup<CompilationThreadToken, FrameworkImportsCacheKey, (TcGlobals * TcImports)>(size, areSimilar=(fun (x, y) -> x = y)) 

    /// Reduce the size of the cache in low-memory scenarios
    member _.Downsize ctok = frameworkTcImportsCache.Resize(ctok, newKeepStrongly=0)

    /// Clear the cache
    member _.Clear ctok = frameworkTcImportsCache.Clear ctok

    /// This function strips the "System" assemblies from the tcConfig and returns a age-cached TcImports for them.
    member _.Get(ctok, tcConfig: TcConfig) =
      cancellable {
        // Split into installed and not installed.
        let frameworkDLLs, nonFrameworkResolutions, unresolved = TcAssemblyResolutions.SplitNonFoundationalResolutions(ctok, tcConfig)
        let frameworkDLLsKey = 
            frameworkDLLs 
            |> List.map (fun ar->ar.resolvedPath) // The cache key. Just the minimal data.
            |> List.sort  // Sort to promote cache hits.

        let! tcGlobals, frameworkTcImports = 
          cancellable {
            // Prepare the frameworkTcImportsCache
            //
            // The data elements in this key are very important. There should be nothing else in the TcConfig that logically affects
            // the import of a set of framework DLLs into F# CCUs. That is, the F# CCUs that result from a set of DLLs (including
            // FSharp.Core.dll and mscorlib.dll) must be logically invariant of all the other compiler configuration parameters.
            let key = (frameworkDLLsKey,
                        tcConfig.primaryAssembly.Name,
                        tcConfig.GetTargetFrameworkDirectories(),
                        tcConfig.fsharpBinariesDir,
                        tcConfig.langVersion.SpecifiedVersion)

            match frameworkTcImportsCache.TryGet (ctok, key) with
            | Some res -> return res
            | None -> 
                let tcConfigP = TcConfigProvider.Constant tcConfig
                let! ((tcGlobals, tcImports) as res) = TcImports.BuildFrameworkTcImports (ctok, tcConfigP, frameworkDLLs, nonFrameworkResolutions)
                frameworkTcImportsCache.Put(ctok, key, res)
                return tcGlobals, tcImports
          }
        return tcGlobals, frameworkTcImports, nonFrameworkResolutions, unresolved
      }

/// Represents the interim state of checking an assembly
[<Sealed>]
type PartialCheckResults (boundModel: BoundModel, timeStamp: DateTime) = 

    member _.TcImports = boundModel.TcImports

    member _.TcGlobals = boundModel.TcGlobals

    member _.TcConfig = boundModel.TcConfig

    member _.TimeStamp = timeStamp

    member _.TryTcInfo = boundModel.TryTcInfo

    member _.GetTcInfo() = boundModel.GetTcInfo()

    member _.GetTcInfoWithExtras() = boundModel.GetTcInfoWithExtras()

    member _.TryGetItemKeyStore() =
        eventually {
            let! _, info = boundModel.GetTcInfoWithExtras()
            return info.itemKeyStore
        }

    member _.GetSemanticClassification() =
        eventually {
            let! _, info = boundModel.GetTcInfoWithExtras()
            return info.semanticClassificationKeyStore
        }

[<AutoOpen>]
module Utilities = 
    let TryFindFSharpStringAttribute tcGlobals attribSpec attribs =
        match TryFindFSharpAttribute tcGlobals attribSpec attribs with
        | Some (Attrib(_, _, [ AttribStringArg s ], _, _, _, _))  -> Some s
        | _ -> None

/// The implementation of the information needed by TcImports in CompileOps.fs for an F# assembly reference.
//
/// Constructs the build data (IRawFSharpAssemblyData) representing the assembly when used 
/// as a cross-assembly reference.  Note the assembly has not been generated on disk, so this is
/// a virtualized view of the assembly contents as computed by background checking.
type RawFSharpAssemblyDataBackedByLanguageService (tcConfig, tcGlobals, tcState: TcState, outfile, topAttrs, assemblyName, ilAssemRef) = 

    let generatedCcu = tcState.Ccu
    let exportRemapping = MakeExportRemapping generatedCcu generatedCcu.Contents
                      
    let sigData = 
        let _sigDataAttributes, sigDataResources = EncodeSignatureData(tcConfig, tcGlobals, exportRemapping, generatedCcu, outfile, true)
        [ for r in sigDataResources  do
            let ccuName = GetSignatureDataResourceName r
            yield (ccuName, (fun () -> r.GetBytes())) ]

    let autoOpenAttrs = topAttrs.assemblyAttrs |> List.choose (List.singleton >> TryFindFSharpStringAttribute tcGlobals tcGlobals.attrib_AutoOpenAttribute)

    let ivtAttrs = topAttrs.assemblyAttrs |> List.choose (List.singleton >> TryFindFSharpStringAttribute tcGlobals tcGlobals.attrib_InternalsVisibleToAttribute)

    interface IRawFSharpAssemblyData with 
        member _.GetAutoOpenAttributes(_ilg) = autoOpenAttrs
        member _.GetInternalsVisibleToAttributes(_ilg) =  ivtAttrs
        member _.TryGetILModuleDef() = None
        member _.GetRawFSharpSignatureData(_m, _ilShortAssemName, _filename) = sigData
        member _.GetRawFSharpOptimizationData(_m, _ilShortAssemName, _filename) = [ ]
        member _.GetRawTypeForwarders() = mkILExportedTypes []  // TODO: cross-project references with type forwarders
        member _.ShortAssemblyName = assemblyName
        member _.ILScopeRef = IL.ILScopeRef.Assembly ilAssemRef
        member _.ILAssemblyRefs = [] // These are not significant for service scenarios
        member _.HasAnyFSharpSignatureDataAttribute =  true
        member _.HasMatchingFSharpSignatureDataAttribute _ilg = true

type IncrementalBuilderState =
    {
        // stampedFileNames represent the real stamps of the files.
        // logicalStampedFileNames represent the stamps of the files that are used to calculate the project's logical timestamp.
        stampedFileNames: ImmutableArray<DateTime>
        logicalStampedFileNames: ImmutableArray<DateTime>
        stampedReferencedAssemblies: ImmutableArray<DateTime>
        initialBoundModel: BoundModel option
        boundModels: ImmutableArray<BoundModel option>
        finalizedBoundModel: ((ILAssemblyRef * IRawFSharpAssemblyData option * TypedImplFile list option * BoundModel) * DateTime) option
        enablePartialTypeChecking: bool
    }

/// Manages an incremental build graph for the build of a single F# project
type IncrementalBuilder(tcGlobals,
        frameworkTcImports,
        nonFrameworkAssemblyInputs,
        nonFrameworkResolutions,
        unresolvedReferences,
        tcConfig: TcConfig,
        projectDirectory,
        outfile, 
        assemblyName,
        niceNameGen: NiceNameGenerator,
        lexResourceManager, 
        sourceFiles,
        loadClosureOpt: LoadClosure option, 
        keepAssemblyContents,
        keepAllBackgroundResolutions,
        keepAllBackgroundSymbolUses,
        enableBackgroundItemKeyStoreAndSemanticClassification,
        enablePartialTypeChecking,
        dependencyProviderOpt: DependencyProvider option) =

    let tcConfigP = TcConfigProvider.Constant tcConfig
    let fileParsed = new Event<string>()
    let beforeFileChecked = new Event<string>()
    let fileChecked = new Event<string>()
    let projectChecked = new Event<unit>()
#if !NO_EXTENSIONTYPING
    let importsInvalidatedByTypeProvider = new Event<string>()
#endif
    let defaultPartialTypeChecking = enablePartialTypeChecking

    // Check for the existence of loaded sources and prepend them to the sources list if present.
    let sourceFiles = tcConfig.GetAvailableLoadedSources() @ (sourceFiles |>List.map (fun s -> rangeStartup, s))

    // Mark up the source files with an indicator flag indicating if they are the last source file in the project
    let sourceFiles = 
        let flags, isExe = tcConfig.ComputeCanContainEntryPoint(sourceFiles |> List.map snd)
        ((sourceFiles, flags) ||> List.map2 (fun (m, nm) flag -> (m, nm, (flag, isExe))))

    let defaultTimeStamp = DateTime.UtcNow

    let basicDependencies = 
        [ for (UnresolvedAssemblyReference(referenceText, _))  in unresolvedReferences do
            // Exclude things that are definitely not a file name
            if not(FileSystem.IsInvalidPathShim referenceText) then 
                let file = if FileSystem.IsPathRootedShim referenceText then referenceText else Path.Combine(projectDirectory, referenceText) 
                yield file 

          for r in nonFrameworkResolutions do 
                yield  r.resolvedPath  ]

    let allDependencies =
        [| yield! basicDependencies
           for (_, f, _) in sourceFiles do
                yield f |]

    // For scripts, the dependency provider is already available.
    // For projects create a fresh one for the project.
    let dependencyProvider = 
        match dependencyProviderOpt with 
        | None -> new DependencyProvider()
        | Some dependencyProvider -> dependencyProvider

    //----------------------------------------------------
    // START OF BUILD TASK FUNCTIONS 
                
    /// Get the timestamp of the given file name.
    let StampFileNameTask (cache: TimeStampCache) (_m: range, filename: string, _isLastCompiland) =
        cache.GetFileTimeStamp filename

    /// Parse the given file and return the given input.
    let ParseTask (sourceRange: range, filename: string, isLastCompiland) =
        SyntaxTree(tcConfig, fileParsed, lexResourceManager, sourceRange, filename, isLastCompiland)
        
    /// Timestamps of referenced assemblies are taken from the file's timestamp.
    let StampReferencedAssemblyTask (cache: TimeStampCache) (_ref, timeStamper) =
        timeStamper cache
                
    // Link all the assemblies together and produce the input typecheck accumulator               
    let CombineImportedAssembliesTask ctok : Cancellable<BoundModel> =
      cancellable {
        let errorLogger = CompilationErrorLogger("CombineImportedAssembliesTask", tcConfig.errorSeverityOptions)
        // Return the disposable object that cleans up
        use _holder = new CompilationGlobalsScope(errorLogger, BuildPhase.Parameter)

        let! tcImports = 
          cancellable {
            try
                let! tcImports = TcImports.BuildNonFrameworkTcImports(ctok, tcConfigP, tcGlobals, frameworkTcImports, nonFrameworkResolutions, unresolvedReferences, dependencyProvider)  
#if !NO_EXTENSIONTYPING
                tcImports.GetCcusExcludingBase() |> Seq.iter (fun ccu -> 
                    // When a CCU reports an invalidation, merge them together and just report a 
                    // general "imports invalidated". This triggers a rebuild.
                    //
                    // We are explicit about what the handler closure captures to help reason about the
                    // lifetime of captured objects, especially in case the type provider instance gets leaked
                    // or keeps itself alive mistakenly, e.g. via some global state in the type provider instance.
                    //
                    // The handler only captures
                    //    1. a weak reference to the importsInvalidated event.  
                    //
                    // The IncrementalBuilder holds the strong reference the importsInvalidated event.
                    //
                    // In the invalidation handler we use a weak reference to allow the IncrementalBuilder to 
                    // be collected if, for some reason, a TP instance is not disposed or not GC'd.
                    let capturedImportsInvalidated = WeakReference<_>(importsInvalidatedByTypeProvider)
                    ccu.Deref.InvalidateEvent.Add(fun msg -> 
                        match capturedImportsInvalidated.TryGetTarget() with 
                        | true, tg -> tg.Trigger msg
                        | _ -> ()))  
#endif
                return tcImports
            with e -> 
                System.Diagnostics.Debug.Assert(false, sprintf "Could not BuildAllReferencedDllTcImports %A" e)
                errorLogger.Warning e
                return frameworkTcImports           
          }

        let tcInitial = GetInitialTcEnv (assemblyName, rangeStartup, tcConfig, tcImports, tcGlobals)
        let tcState = GetInitialTcState (rangeStartup, assemblyName, tcConfig, tcGlobals, tcImports, niceNameGen, tcInitial)
        let loadClosureErrors = 
           [ match loadClosureOpt with 
             | None -> ()
             | Some loadClosure -> 
                for inp in loadClosure.Inputs do
                    yield! inp.MetaCommandDiagnostics ]

        let initialErrors = Array.append (Array.ofList loadClosureErrors) (errorLogger.GetDiagnostics())
        let tcInfo = 
            {
              tcState=tcState
              tcEnvAtEndOfFile=tcInitial
              topAttribs=None
              latestCcuSigForFile=None
              tcErrorsRev = [ initialErrors ] 
              moduleNamesDict = Map.empty
              tcDependencyFiles = basicDependencies
              sigNameOpt = None
            }
        let tcInfoExtras =
            {
                tcResolutionsRev=[]
                tcSymbolUsesRev=[]
                tcOpenDeclarationsRev=[]
                latestImplFile=None
                itemKeyStore = None
                semanticClassificationKeyStore = None 
            }
        return 
            BoundModel.Create(
                tcConfig,
                tcGlobals,
                tcImports,
                keepAssemblyContents, 
                keepAllBackgroundResolutions, 
                keepAllBackgroundSymbolUses, 
                enableBackgroundItemKeyStoreAndSemanticClassification,
                defaultPartialTypeChecking,
                beforeFileChecked,
                fileChecked,
                tcInfo,
                (fun () -> Eventually.Done (Some tcInfoExtras)),
                None) }
                
    /// Type check all files.     
    let TypeCheckTask ctok enablePartialTypeChecking (prevBoundModel: BoundModel) syntaxTree: Eventually<BoundModel> =
        eventually {
            RequireCompilationThread ctok
            let! boundModel = prevBoundModel.Next(syntaxTree)
            // Eagerly type check
            // We need to do this to keep the expected behavior of events (namely fileChecked) when checking a file/project.
            let! _ = boundModel.GetState(enablePartialTypeChecking)
            return boundModel
        }

    /// Finish up the typechecking to produce outputs for the rest of the compilation process
    let FinalizeTypeCheckTask ctok enablePartialTypeChecking (boundModels: ImmutableArray<BoundModel>) = 
      eventually {
        DoesNotRequireCompilerThreadTokenAndCouldPossiblyBeMadeConcurrent  ctok

        let errorLogger = CompilationErrorLogger("CombineImportedAssembliesTask", tcConfig.errorSeverityOptions)
        // This reinstalls the CompilationGlobalsScope each time the Eventually is restarted, potentially
        // on a new thread. This is needed because CompilationGlobalsScope installs thread local variables.
        return! Eventually.reusing (fun () -> new CompilationGlobalsScope (errorLogger, BuildPhase.TypeCheck) :> IDisposable)  <| eventually {

        let! results = 
            boundModels |> Eventually.each (fun boundModel -> eventually {
                let! tcInfo, latestImplFile =
                  eventually {
                    if enablePartialTypeChecking then
                        let! tcInfo = boundModel.GetTcInfo()
                        return tcInfo, None
                    else
                        let! tcInfo, tcInfoExtras = boundModel.GetTcInfoWithExtras()
                        return tcInfo, tcInfoExtras.latestImplFile
                   }
                return (tcInfo.tcEnvAtEndOfFile, defaultArg tcInfo.topAttribs EmptyTopAttrs, latestImplFile, tcInfo.latestCcuSigForFile)
            })

        // Get the state at the end of the type-checking of the last file
        let finalBoundModel = boundModels.[boundModels.Length-1]

        let! finalInfo = finalBoundModel.GetTcInfo()

        // Finish the checking
        let (_tcEnvAtEndOfLastFile, topAttrs, mimpls, _), tcState = 
            TypeCheckMultipleInputsFinish (results, finalInfo.tcState)
  
        let ilAssemRef, tcAssemblyDataOpt, tcAssemblyExprOpt = 
            try
                // TypeCheckClosedInputSetFinish fills in tcState.Ccu but in incremental scenarios we don't want this, 
                // so we make this temporary here
                let oldContents = tcState.Ccu.Deref.Contents
                try
                    let tcState, tcAssemblyExpr = TypeCheckClosedInputSetFinish (mimpls, tcState)

                    // Compute the identity of the generated assembly based on attributes, options etc.
                    // Some of this is duplicated from fsc.fs
                    let ilAssemRef = 
                        let publicKey = 
                            try 
                                let signingInfo = ValidateKeySigningAttributes (tcConfig, tcGlobals, topAttrs)
                                match GetStrongNameSigner signingInfo with 
                                | None -> None
                                | Some s -> Some (PublicKey.KeyAsToken(s.PublicKey))
                            with e -> 
                                errorRecoveryNoRange e
                                None
                        let locale = TryFindFSharpStringAttribute tcGlobals (tcGlobals.FindSysAttrib  "System.Reflection.AssemblyCultureAttribute") topAttrs.assemblyAttrs
                        let assemVerFromAttrib = 
                            TryFindFSharpStringAttribute tcGlobals (tcGlobals.FindSysAttrib "System.Reflection.AssemblyVersionAttribute") topAttrs.assemblyAttrs 
                            |> Option.bind  (fun v -> try Some (parseILVersion v) with _ -> None)
                        let ver = 
                            match assemVerFromAttrib with 
                            | None -> tcConfig.version.GetVersionInfo(tcConfig.implicitIncludeDir)
                            | Some v -> v
                        ILAssemblyRef.Create(assemblyName, None, publicKey, false, Some ver, locale)
                
                    let tcAssemblyDataOpt = 
                        try

                          // Assemblies containing type provider components can not successfully be used via cross-assembly references.
                          // We return 'None' for the assembly portion of the cross-assembly reference 
                          let hasTypeProviderAssemblyAttrib = 
                              topAttrs.assemblyAttrs |> List.exists (fun (Attrib(tcref, _, _, _, _, _, _)) -> 
                                  let nm = tcref.CompiledRepresentationForNamedType.BasicQualifiedName 
                                  nm = typeof<Microsoft.FSharp.Core.CompilerServices.TypeProviderAssemblyAttribute>.FullName)

                          if tcState.CreatesGeneratedProvidedTypes || hasTypeProviderAssemblyAttrib then
                            None
                          else
                            Some  (RawFSharpAssemblyDataBackedByLanguageService (tcConfig, tcGlobals, tcState, outfile, topAttrs, assemblyName, ilAssemRef) :> IRawFSharpAssemblyData)

                        with e -> 
                            errorRecoveryNoRange e
                            None
                    ilAssemRef, tcAssemblyDataOpt, Some tcAssemblyExpr
                finally 
                    tcState.Ccu.Deref.Contents <- oldContents
            with e -> 
                errorRecoveryNoRange e
                mkSimpleAssemblyRef assemblyName, None, None

        let diagnostics = errorLogger.GetDiagnostics() :: finalInfo.tcErrorsRev
        let! finalBoundModelWithErrors = finalBoundModel.Finish(diagnostics, Some topAttrs)
        return ilAssemRef, tcAssemblyDataOpt, tcAssemblyExprOpt, finalBoundModelWithErrors
      }
    }

    // END OF BUILD TASK FUNCTIONS
    // ---------------------------------------------------------------------------------------------            

    // ---------------------------------------------------------------------------------------------            
    // START OF BUILD DESCRIPTION

    // Inputs
    let fileNames = sourceFiles |> Array.ofList // TODO: This should be an immutable array.
    let referencedAssemblies =  nonFrameworkAssemblyInputs |> Array.ofList // TODO: This should be an immutable array.

    let computeStampedFileName (state: IncrementalBuilderState) (cache: TimeStampCache) slot fileInfo =
        let currentStamp = state.stampedFileNames.[slot]
        let stamp = StampFileNameTask cache fileInfo

        if currentStamp <> stamp then
            match state.boundModels.[slot] with
            // This prevents an implementation file that has a backing signature file from invalidating the rest of the build.
            | Some(boundModel) when state.enablePartialTypeChecking && boundModel.BackingSignature.IsSome ->
                boundModel.Invalidate()
                { state with 
                    stampedFileNames = state.stampedFileNames.SetItem(slot, StampFileNameTask cache fileInfo) 
                }
            | _ ->
                
                let stampedFileNames = state.stampedFileNames.ToBuilder()
                let logicalStampedFileNames = state.logicalStampedFileNames.ToBuilder()
                let boundModels = state.boundModels.ToBuilder()

                // Invalidate the file and all files below it.
                for j = 0 to stampedFileNames.Count - slot - 1 do
                    let stamp = StampFileNameTask cache fileNames.[slot + j]
                    stampedFileNames.[slot + j] <- stamp
                    logicalStampedFileNames.[slot + j] <- stamp
                    boundModels.[slot + j] <- None

                { state with
                    // Something changed, the finalized view of the project must be invalidated.
                    finalizedBoundModel = None

                    stampedFileNames = stampedFileNames.ToImmutable()
                    logicalStampedFileNames = logicalStampedFileNames.ToImmutable()
                    boundModels = boundModels.ToImmutable()
                }
        else
            state

    let computeStampedFileNames state (cache: TimeStampCache) =
        let mutable i = 0
        (state, fileNames)
        ||> Array.fold (fun state fileInfo ->
            let newState = computeStampedFileName state cache i fileInfo
            i <- i + 1
            newState
        )

    let computeStampedReferencedAssemblies state (cache: TimeStampCache) =
        let stampedReferencedAssemblies = state.stampedReferencedAssemblies.ToBuilder()

        let mutable referencesUpdated = false
        referencedAssemblies
        |> Array.iteri (fun i asmInfo ->

            let currentStamp = state.stampedReferencedAssemblies.[i]
            let stamp = StampReferencedAssemblyTask cache asmInfo

            if currentStamp <> stamp then
                referencesUpdated <- true
                stampedReferencedAssemblies.[i] <- stamp
        )
        
        if referencesUpdated then
            // Something changed, the finalized view of the project must be invalidated.
            // This is the only place where the initial bound model will be invalidated.
            let count = state.stampedFileNames.Length
            { state with
                stampedReferencedAssemblies = stampedReferencedAssemblies.ToImmutable()
                initialBoundModel = None
                finalizedBoundModel = None
                stampedFileNames = Array.init count (fun _ -> DateTime.MinValue) |> ImmutableArray.CreateRange
                logicalStampedFileNames = Array.init count (fun _ -> DateTime.MinValue) |> ImmutableArray.CreateRange
                boundModels = Array.init count (fun _ -> None) |> ImmutableArray.CreateRange
            }
        else
            state

    let computeInitialBoundModel (state: IncrementalBuilderState) (ctok: CompilationThreadToken) =
        eventually {
            match state.initialBoundModel with
            | None ->
                // Note this is not time-sliced
                let! result = CombineImportedAssembliesTask ctok |> Eventually.ofCancellable
                return { state with initialBoundModel = Some result }, result
            | Some result ->
                return state, result
        }

    let computeBoundModel state (cache: TimeStampCache) (ctok: CompilationThreadToken) (slot: int) =
        if IncrementalBuild.injectCancellationFault then Eventually.canceled() else
        eventually {         

            let fileInfo = fileNames.[slot]

            let state = computeStampedFileName state cache slot fileInfo

            if state.boundModels.[slot].IsNone then
                let! (state, initial) = computeInitialBoundModel state ctok

                let prevBoundModel =
                    match slot with
                    | 0 (* first file *) -> initial
                    | _ ->
                        match state.boundModels.[slot - 1] with
                        | Some(prevBoundModel) -> prevBoundModel
                        | _ -> 
                            // This shouldn't happen, but on the off-chance, just grab the initial bound model.
                            initial

                let! boundModel = TypeCheckTask ctok state.enablePartialTypeChecking prevBoundModel (ParseTask fileInfo)
                
                let state =
                    { state with
                        boundModels = state.boundModels.SetItem(slot, Some boundModel)
                    }
                return state

            else 
                return state
        }

    let computeBoundModels state (cache: TimeStampCache) (ctok: CompilationThreadToken) =
        (state, [0..fileNames.Length-1]) ||> Eventually.fold (fun state slot -> computeBoundModel state cache ctok slot)

    let computeFinalizedBoundModel state (cache: TimeStampCache) (ctok: CompilationThreadToken) =
        eventually {
            let! state = computeBoundModels state cache ctok

            match state.finalizedBoundModel with
            | Some result -> return state, result
            | _ ->
                let boundModels = state.boundModels |> Seq.choose id |> ImmutableArray.CreateRange
            
                let! result = FinalizeTypeCheckTask ctok state.enablePartialTypeChecking boundModels
                let result = (result, DateTime.UtcNow)
                return { state with finalizedBoundModel = Some result }, result
        }

    let tryGetBeforeSlot (state: IncrementalBuilderState) slot =
        match slot with
        | 0 (* first file *) ->
            match state.initialBoundModel with
            | Some initial ->
                (initial, DateTime.MinValue)
                |> Some
            | _ ->
                None
        | _ ->
            match state.boundModels.[slot - 1] with
            | Some boundModel ->
                (boundModel, state.stampedFileNames.[slot - 1])
                |> Some
            | _ ->
                None
                
    let evalUpToTargetSlot state (cache: TimeStampCache) ctok targetSlot =
        cancellable {
            let state = computeStampedReferencedAssemblies state cache
            if targetSlot < 0 then
                let! state, result = computeInitialBoundModel state ctok |> Eventually.toCancellable
                return state, Some(result, DateTime.MinValue)
            else         
                let! state = (state, [0..targetSlot]) ||> Eventually.fold (fun state slot -> computeBoundModel state cache ctok slot) |> Eventually.toCancellable

                let result =
                    state.boundModels.[targetSlot]
                    |> Option.map (fun boundModel ->
                        (boundModel, state.stampedFileNames.[targetSlot])
                    )

                return state, result
        }

    let tryGetFinalized state cache ctok =
        cancellable {
            let state = computeStampedReferencedAssemblies state cache

            let! state, res = computeFinalizedBoundModel state cache ctok |> Eventually.toCancellable
            return state, Some res
        }

    let MaxTimeStampInDependencies stamps = 
        if Seq.isEmpty stamps then
            DateTime.MinValue
        else
            stamps
            |> Seq.max

    // END OF BUILD DESCRIPTION
    // ---------------------------------------------------------------------------------------------
    
    (*
        The data below represents a dependency graph.
        
        ReferencedAssembliesStamps => FileStamps => BoundModels => FinalizedBoundModel
    *)

    let mutable currentState =
        {
            stampedFileNames = Array.init fileNames.Length (fun _ -> DateTime.MinValue) |> ImmutableArray.CreateRange
            logicalStampedFileNames = Array.init fileNames.Length (fun _ -> DateTime.MinValue) |> ImmutableArray.CreateRange
            stampedReferencedAssemblies = Array.init referencedAssemblies.Length (fun _ -> DateTime.MinValue) |> ImmutableArray.CreateRange
            initialBoundModel = None
            boundModels = Array.zeroCreate<BoundModel option> fileNames.Length |> ImmutableArray.CreateRange
            finalizedBoundModel = None
            enablePartialTypeChecking = enablePartialTypeChecking
        }

    let setCurrentState (_ctok: CompilationThreadToken) state =
        currentState <- state

    do IncrementalBuilderEventTesting.MRU.Add(IncrementalBuilderEventTesting.IBECreated)

    member _.TcConfig = tcConfig

    member _.FileParsed = fileParsed.Publish

    member _.BeforeFileChecked = beforeFileChecked.Publish

    member _.FileChecked = fileChecked.Publish

    member _.ProjectChecked = projectChecked.Publish

#if !NO_EXTENSIONTYPING
    member _.ImportsInvalidatedByTypeProvider = importsInvalidatedByTypeProvider.Publish
#endif

    member _.AllDependenciesDeprecated = allDependencies

    member _.PopulatePartialCheckingResults (ctok: CompilationThreadToken) =  
      eventually {
        let cache = TimeStampCache defaultTimeStamp // One per step
        let state = currentState
        let state = computeStampedFileNames state cache
        setCurrentState ctok state
        do! Eventually.ret () // allow cancellation
        let state = computeStampedReferencedAssemblies state cache
        setCurrentState ctok state
        do! Eventually.ret () // allow cancellation
        let! state, _res = computeFinalizedBoundModel state cache ctok
        setCurrentState ctok state
        projectChecked.Trigger()
      }
    
    member builder.GetCheckResultsBeforeFileInProjectEvenIfStale filename: PartialCheckResults option  = 
        let slotOfFile = builder.GetSlotOfFileName filename
        let result = tryGetBeforeSlot currentState slotOfFile
        
        match result with
        | Some (boundModel, timestamp) -> Some (PartialCheckResults (boundModel, timestamp))
        | _ -> None
        
    
    member builder.AreCheckResultsBeforeFileInProjectReady filename = 
        let slotOfFile = builder.GetSlotOfFileName filename
        match tryGetBeforeSlot currentState slotOfFile with
        | Some _ -> true
        | _ -> false

    member builder.TryGetCheckResultsBeforeFileInProject (filename) =
        let cache = TimeStampCache defaultTimeStamp
        let state = currentState
        let state = computeStampedFileNames state cache
        let state = computeStampedReferencedAssemblies state cache

        let slotOfFile = builder.GetSlotOfFileName filename
        match tryGetBeforeSlot state slotOfFile with
        | Some(boundModel, timestamp) -> PartialCheckResults(boundModel, timestamp) |> Some
        | _ -> None

    member private _.GetCheckResultsBeforeSlotInProject (ctok: CompilationThreadToken, slotOfFile, enablePartialTypeChecking) = 
      cancellable {
        let cache = TimeStampCache defaultTimeStamp
        let! state, result = evalUpToTargetSlot { currentState with enablePartialTypeChecking = enablePartialTypeChecking } cache ctok (slotOfFile - 1)
        setCurrentState ctok { state with enablePartialTypeChecking = defaultPartialTypeChecking }
        match result with
        | Some (boundModel, timestamp) -> return PartialCheckResults(boundModel, timestamp)
        | None -> return! failwith "Build was not evaluated, expected the results to be ready after 'Eval' (GetCheckResultsBeforeSlotInProject)."
      }
        
    member builder.GetCheckResultsBeforeSlotInProject (ctok: CompilationThreadToken, slotOfFile) = 
        builder.GetCheckResultsBeforeSlotInProject(ctok, slotOfFile, defaultPartialTypeChecking)

    member builder.GetCheckResultsBeforeFileInProject (ctok: CompilationThreadToken, filename) = 
        let slotOfFile = builder.GetSlotOfFileName filename
        builder.GetCheckResultsBeforeSlotInProject (ctok, slotOfFile)

    member builder.GetCheckResultsAfterFileInProject (ctok: CompilationThreadToken, filename) = 
        let slotOfFile = builder.GetSlotOfFileName filename + 1
        builder.GetCheckResultsBeforeSlotInProject (ctok, slotOfFile)

    member builder.GetFullCheckResultsAfterFileInProject (ctok: CompilationThreadToken, filename) = 
        cancellable {
            let slotOfFile = builder.GetSlotOfFileName filename + 1
            let! result = builder.GetCheckResultsBeforeSlotInProject(ctok, slotOfFile, false)
            let! _ = result.GetTcInfoWithExtras() |> Eventually.toCancellable // Make sure we forcefully evaluate the info
            return result
        }

    member builder.GetCheckResultsAfterLastFileInProject (ctok: CompilationThreadToken) = 
        builder.GetCheckResultsBeforeSlotInProject(ctok, builder.GetSlotsCount()) 

    member private _.GetCheckResultsAndImplementationsForProject(ctok: CompilationThreadToken, enablePartialTypeChecking) = 
      cancellable {
        let cache = TimeStampCache defaultTimeStamp

        let! state, result = tryGetFinalized { currentState with enablePartialTypeChecking = enablePartialTypeChecking } cache ctok
        setCurrentState ctok { state with enablePartialTypeChecking = defaultPartialTypeChecking }
        match result with
        | Some ((ilAssemRef, tcAssemblyDataOpt, tcAssemblyExprOpt, boundModel), timestamp) -> 
            return PartialCheckResults (boundModel, timestamp), ilAssemRef, tcAssemblyDataOpt, tcAssemblyExprOpt
        | None -> 
            let msg = "Build was not evaluated, expected the results to be ready after 'tryGetFinalized')."
            return! failwith msg
      }

    member builder.GetCheckResultsAndImplementationsForProject(ctok: CompilationThreadToken) =
        builder.GetCheckResultsAndImplementationsForProject(ctok, defaultPartialTypeChecking)

    member builder.GetFullCheckResultsAndImplementationsForProject(ctok: CompilationThreadToken) = 
        cancellable {
            let! result = builder.GetCheckResultsAndImplementationsForProject(ctok, false)
            let results, _, _, _ = result
            let! _ = results.GetTcInfoWithExtras() |> Eventually.toCancellable // Make sure we forcefully evaluate the info
            return result
        }
        
    member _.GetLogicalTimeStampForProject(cache) = 
        let state = currentState
        let state = computeStampedFileNames state cache
        let state = computeStampedReferencedAssemblies state cache
        let t1 = MaxTimeStampInDependencies state.stampedReferencedAssemblies
        let t2 = MaxTimeStampInDependencies state.stampedFileNames
        max t1 t2
        
    member _.TryGetSlotOfFileName(filename: string) =
        // Get the slot of the given file and force it to build.
        let CompareFileNames (_, f2, _) = 
            let result = 
                   String.Compare(filename, f2, StringComparison.CurrentCultureIgnoreCase)=0
                || String.Compare(FileSystem.GetFullPathShim filename, FileSystem.GetFullPathShim f2, StringComparison.CurrentCultureIgnoreCase)=0
            result
        match fileNames |> Array.tryFindIndex CompareFileNames with
        | Some slot -> Some slot
        | None -> None
        
    member this.GetSlotOfFileName(filename: string) =
        match this.TryGetSlotOfFileName(filename) with
        | Some slot -> slot
        | None -> failwith (sprintf "The file '%s' was not part of the project. Did you call InvalidateConfiguration when the list of files in the project changed?" filename)
        
    member _.GetSlotsCount () = fileNames.Length

    member this.ContainsFile(filename: string) =
        (this.TryGetSlotOfFileName filename).IsSome
      
    member builder.GetParseResultsForFile (filename) =
        let slotOfFile = builder.GetSlotOfFileName filename
        let results = fileNames.[slotOfFile]
        // re-parse on demand instead of retaining
        let syntaxTree = ParseTask results
        syntaxTree.Parse None

    member _.SourceFiles  = sourceFiles  |> List.map (fun (_, f, _) -> f)

    /// CreateIncrementalBuilder (for background type checking). Note that fsc.fs also
    /// creates an incremental builder used by the command line compiler.
    static member TryCreateIncrementalBuilderForProjectOptions
                      (ctok, legacyReferenceResolver, defaultFSharpBinariesDir,
                       frameworkTcImportsCache: FrameworkImportsCache,
                       loadClosureOpt: LoadClosure option,
                       sourceFiles: string list,
                       commandLineArgs: string list,
                       projectReferences, projectDirectory,
                       useScriptResolutionRules, keepAssemblyContents,
                       keepAllBackgroundResolutions,
                       tryGetMetadataSnapshot, suggestNamesForErrors,
                       keepAllBackgroundSymbolUses,
                       enableBackgroundItemKeyStoreAndSemanticClassification,
                       enablePartialTypeChecking: bool,
                       dependencyProvider) =

      let useSimpleResolutionSwitch = "--simpleresolution"

      cancellable {

        // Trap and report warnings and errors from creation.
        let delayedLogger = CapturingErrorLogger("IncrementalBuilderCreation")
        use _unwindEL = PushErrorLoggerPhaseUntilUnwind (fun _ -> delayedLogger)
        use _unwindBP = PushThreadBuildPhaseUntilUnwind BuildPhase.Parameter

        let! builderOpt =
         cancellable {
          try

            // Create the builder.         
            // Share intern'd strings across all lexing/parsing
            let resourceManager = new Lexhelp.LexResourceManager() 

            /// Create a type-check configuration
            let tcConfigB, sourceFilesNew = 

                let getSwitchValue switchString =
                    match commandLineArgs |> Seq.tryFindIndex(fun s -> s.StartsWithOrdinal switchString) with
                    | Some idx -> Some(commandLineArgs.[idx].Substring(switchString.Length))
                    | _ -> None

                let sdkDirOverride =
                    match loadClosureOpt with 
                    | None -> None
                    | Some loadClosure -> loadClosure.SdkDirOverride

                // see also fsc.fs: runFromCommandLineToImportingAssemblies(), as there are many similarities to where the PS creates a tcConfigB
                let tcConfigB =
                    TcConfigBuilder.CreateNew(legacyReferenceResolver,
                         defaultFSharpBinariesDir,
                         implicitIncludeDir=projectDirectory,
                         reduceMemoryUsage=ReduceMemoryFlag.Yes,
                         isInteractive=useScriptResolutionRules,
                         isInvalidationSupported=true,
                         defaultCopyFSharpCore=CopyFSharpCoreFlag.No,
                         tryGetMetadataSnapshot=tryGetMetadataSnapshot,
                         sdkDirOverride=sdkDirOverride,
                         rangeForErrors=range0)

                tcConfigB.primaryAssembly <-
                    match loadClosureOpt with
                    | None -> PrimaryAssembly.Mscorlib
                    | Some loadClosure ->
                        if loadClosure.UseDesktopFramework then
                            PrimaryAssembly.Mscorlib
                        else
                            PrimaryAssembly.System_Runtime

                tcConfigB.resolutionEnvironment <- (LegacyResolutionEnvironment.EditingOrCompilation true)

                tcConfigB.conditionalCompilationDefines <- 
                    let define = if useScriptResolutionRules then "INTERACTIVE" else "COMPILED"
                    define :: tcConfigB.conditionalCompilationDefines

                tcConfigB.projectReferences <- projectReferences

                tcConfigB.useSimpleResolution <- (getSwitchValue useSimpleResolutionSwitch) |> Option.isSome

                // Apply command-line arguments and collect more source files if they are in the arguments
                let sourceFilesNew = ApplyCommandLineArgs(tcConfigB, sourceFiles, commandLineArgs)

                // Never open PDB files for the language service, even if --standalone is specified
                tcConfigB.openDebugInformationForLaterStaticLinking <- false

                tcConfigB, sourceFilesNew

            // If this is a builder for a script, re-apply the settings inferred from the
            // script and its load closure to the configuration.
            //
            // NOTE: it would probably be cleaner and more accurate to re-run the load closure at this point.
            match loadClosureOpt with
            | Some loadClosure ->
                let dllReferences =
                    [for reference in tcConfigB.referencedDLLs do
                        // If there's (one or more) resolutions of closure references then yield them all
                        match loadClosure.References  |> List.tryFind (fun (resolved, _)->resolved=reference.Text) with
                        | Some (resolved, closureReferences) -> 
                            for closureReference in closureReferences do
                                yield AssemblyReference(closureReference.originalReference.Range, resolved, None)
                        | None -> yield reference]
                tcConfigB.referencedDLLs <- []
                tcConfigB.primaryAssembly <- (if loadClosure.UseDesktopFramework then PrimaryAssembly.Mscorlib else PrimaryAssembly.System_Runtime)
                // Add one by one to remove duplicates
                dllReferences |> List.iter (fun dllReference ->
                    tcConfigB.AddReferencedAssemblyByPath(dllReference.Range, dllReference.Text))
                tcConfigB.knownUnresolvedReferences <- loadClosure.UnresolvedReferences
            | None -> ()

            let tcConfig = TcConfig.Create(tcConfigB, validate=true)
            let niceNameGen = NiceNameGenerator()
            let outfile, _, assemblyName = tcConfigB.DecideNames sourceFilesNew

            // Resolve assemblies and create the framework TcImports. This is done when constructing the
            // builder itself, rather than as an incremental task. This caches a level of "system" references. No type providers are 
            // included in these references. 
            let! (tcGlobals, frameworkTcImports, nonFrameworkResolutions, unresolvedReferences) = frameworkTcImportsCache.Get(ctok, tcConfig)

            // Note we are not calling errorLogger.GetDiagnostics() anywhere for this task. 
            // This is ok because not much can actually go wrong here.
            let errorOptions = tcConfig.errorSeverityOptions
            let errorLogger = CompilationErrorLogger("nonFrameworkAssemblyInputs", errorOptions)
            // Return the disposable object that cleans up
            use _holder = new CompilationGlobalsScope(errorLogger, BuildPhase.Parameter) 

            // Get the names and time stamps of all the non-framework referenced assemblies, which will act 
            // as inputs to one of the nodes in the build. 
            //
            // This operation is done when constructing the builder itself, rather than as an incremental task. 
            let nonFrameworkAssemblyInputs = 
                // Note we are not calling errorLogger.GetDiagnostics() anywhere for this task. 
                // This is ok because not much can actually go wrong here.
                let errorLogger = CompilationErrorLogger("nonFrameworkAssemblyInputs", errorOptions)
                // Return the disposable object that cleans up
                use _holder = new CompilationGlobalsScope(errorLogger, BuildPhase.Parameter) 

                [ for r in nonFrameworkResolutions do
                    let fileName = r.resolvedPath
                    yield (Choice1Of2 fileName, (fun (cache: TimeStampCache) -> cache.GetFileTimeStamp fileName))  

                  for pr in projectReferences  do
                    yield Choice2Of2 pr, (fun (cache: TimeStampCache) -> cache.GetProjectReferenceTimeStamp (pr)) ]
            
            let builder = 
                new IncrementalBuilder(tcGlobals,
                    frameworkTcImports,
                    nonFrameworkAssemblyInputs,
                    nonFrameworkResolutions,
                    unresolvedReferences, 
                    tcConfig,
                    projectDirectory,
                    outfile,
                    assemblyName,
                    niceNameGen, 
                    resourceManager,
                    sourceFilesNew,
                    loadClosureOpt, 
                    keepAssemblyContents, 
                    keepAllBackgroundResolutions, 
                    keepAllBackgroundSymbolUses,
                    enableBackgroundItemKeyStoreAndSemanticClassification,
                    enablePartialTypeChecking,
                    dependencyProvider)
            return Some builder
          with e -> 
            errorRecoveryNoRange e
            return None
         }

        let diagnostics =
            match builderOpt with
            | Some builder ->
                let errorSeverityOptions = builder.TcConfig.errorSeverityOptions
                let errorLogger = CompilationErrorLogger("IncrementalBuilderCreation", errorSeverityOptions)
                delayedLogger.CommitDelayedDiagnostics errorLogger
                errorLogger.GetDiagnostics()
            | _ ->
                Array.ofList delayedLogger.Diagnostics
            |> Array.map (fun (d, severity) -> FSharpDiagnostic.CreateFromException(d, severity, range.Zero, suggestNamesForErrors))

        return builderOpt, diagnostics
      }
