// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.CodeAnalysis

open System
open System.Collections.Generic
open System.Collections.Immutable
open System.IO
open System.Threading
open Internal.Utilities.Library
open Internal.Utilities.Collections
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
open FSharp.Compiler.Xml
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.BuildGraph


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
                let canSkip = sigNameOpt.IsSome && FSharpImplFileSuffixes |> List.exists (FileSystemUtils.checkSuffix lower)
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
            SyntaxTree(tcConfig, fileParsed, lexResourceManager, sourceRange, filename, isLastCompiland)

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
      tcResolutions: TcResolutions
      tcSymbolUses: TcSymbolUses
      tcOpenDeclarations: OpenDeclaration[]

      /// Result of checking most recent file, if any
      latestImplFile: TypedImplFile option

      /// If enabled, stores a linear list of ranges and strings that identify an Item(symbol) in a file. Used for background find all references.
      itemKeyStore: ItemKeyStore option

      /// If enabled, holds semantic classification information for Item(symbol)s in a file.
      semanticClassificationKeyStore: SemanticClassificationKeyStore option
    }

    member x.TcSymbolUses =
        x.tcSymbolUses

[<AutoOpen>]
module TcInfoHelpers =

    let emptyTcInfoExtras =
        {
            tcResolutions = TcResolutions.Empty
            tcSymbolUses = TcSymbolUses.Empty
            tcOpenDeclarations = [||]
            latestImplFile = None
            itemKeyStore = None
            semanticClassificationKeyStore = None
        }

/// Accumulated results of type checking.
[<NoEquality; NoComparison>]
type TcInfoState =
    | PartialState of TcInfo
    | FullState of TcInfo * TcInfoExtras

    member x.TcInfo =
        match x with
        | PartialState tcInfo -> tcInfo
        | FullState (tcInfo, _) -> tcInfo

    member x.TcInfoExtras =
        match x with
        | PartialState _ -> None
        | FullState (_, tcInfoExtras) -> Some tcInfoExtras

[<NoEquality; NoComparison>]
type TcInfoNode =
    | TcInfoNode of partial: GraphNode<TcInfo> * full: GraphNode<TcInfo * TcInfoExtras>

    member this.HasFull =
        match this with
        | TcInfoNode(_, full) -> full.HasValue

    static member FromState(state: TcInfoState) =
        let tcInfo = state.TcInfo
        let tcInfoExtras = state.TcInfoExtras
        TcInfoNode(GraphNode(node { return tcInfo }), GraphNode(node { return tcInfo, defaultArg tcInfoExtras emptyTcInfoExtras }))

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
                         syntaxTreeOpt: SyntaxTree option,
                         tcInfoStateOpt: TcInfoState option) as this =

    let tcInfoNode = 
        match tcInfoStateOpt with
        | Some tcInfoState -> TcInfoNode.FromState(tcInfoState)
        | _ ->
            let fullGraphNode =
                GraphNode(node {
                    match! this.TypeCheck(false) with
                    | FullState(tcInfo, tcInfoExtras) -> return tcInfo, tcInfoExtras
                    | PartialState(tcInfo) -> return tcInfo, emptyTcInfoExtras
                })

            let partialGraphNode =              
                GraphNode(node {
                    if enablePartialTypeChecking then
                        // Optimization so we have less of a chance to duplicate work.
                        if fullGraphNode.IsComputing then
                            let! tcInfo, _ = fullGraphNode.GetOrComputeValue()
                            return tcInfo
                        else
                            match fullGraphNode.TryPeekValue() with
                            | ValueSome(tcInfo, _) -> return tcInfo
                            | _ ->
                                let! tcInfoState = this.TypeCheck(true)
                                return tcInfoState.TcInfo
                    else
                        let! tcInfo, _ = fullGraphNode.GetOrComputeValue()
                        return tcInfo
                    })

            TcInfoNode(partialGraphNode, fullGraphNode)

    let defaultTypeCheck () =
        node {
            return PartialState(prevTcInfo)
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

    /// If partial type-checking is enabled,
    ///     this will create a new bound-model that will only have the partial state if the
    ///     the current bound-model has the full state.
    member this.ClearTcInfoExtras() =
        let hasSig = this.BackingSignature.IsSome

        // If partial checking is enabled and we have a backing sig file, then use the partial state. The partial state contains the sig state.
        if tcInfoNode.HasFull && enablePartialTypeChecking && hasSig then
            // Always invalidate the syntax tree cache.
            let newSyntaxTreeOpt =
                syntaxTreeOpt
                |> Option.map (fun x -> x.Invalidate())

            let newTcInfoStateOpt =
                match tcInfoNode with
                | TcInfoNode(_, fullGraphNode) -> 
                    let tcInfo, _ = fullGraphNode.TryPeekValue().Value
                    Some(PartialState tcInfo)

            BoundModel(
                tcConfig,
                tcGlobals,
                tcImports,
                keepAssemblyContents, keepAllBackgroundResolutions,
                keepAllBackgroundSymbolUses,
                enableBackgroundItemKeyStoreAndSemanticClassification,
                enablePartialTypeChecking,
                beforeFileChecked,
                fileChecked,
                prevTcInfo,
                newSyntaxTreeOpt,
                newTcInfoStateOpt)
        else
            this

    member this.Next(syntaxTree, tcInfo) =
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
            tcInfo,
            Some syntaxTree,
            None)

    member this.Finish(finalTcErrorsRev, finalTopAttribs) =
        node {
            let createFinish tcInfo =
                { tcInfo  with tcErrorsRev = finalTcErrorsRev; topAttribs = finalTopAttribs }

            let! finishState =
                node {
                    match tcInfoNode with
                    | TcInfoNode(partialGraphNode, fullGraphNode) ->
                        if fullGraphNode.HasValue then
                            let! tcInfo, tcInfoExtras = fullGraphNode.GetOrComputeValue()
                            let finishTcInfo = createFinish tcInfo
                            return FullState(finishTcInfo, tcInfoExtras)
                        else
                            let! tcInfo = partialGraphNode.GetOrComputeValue()
                            let finishTcInfo = createFinish tcInfo
                            return PartialState(finishTcInfo)
                }

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
                    syntaxTreeOpt,
                    Some finishState)
        }

    member _.TryPeekTcInfo() =
        match tcInfoNode with
        | TcInfoNode(partialGraphNode, fullGraphNode) ->
            match partialGraphNode.TryPeekValue() with
            | ValueSome tcInfo -> Some tcInfo
            | _ ->
                match fullGraphNode.TryPeekValue() with
                | ValueSome(tcInfo, _) -> Some tcInfo
                | _ -> None

    member _.TryPeekTcInfoWithExtras() =
        match tcInfoNode with
        | TcInfoNode(_, fullGraphNode) ->
            match fullGraphNode.TryPeekValue() with
            | ValueSome(tcInfo, tcInfoExtras) -> Some(tcInfo, tcInfoExtras)
            | _ -> None

    member _.GetOrComputeTcInfo() =
        match tcInfoNode with
        | TcInfoNode(partialGraphNode, _) -> 
            partialGraphNode.GetOrComputeValue()

    member _.GetOrComputeTcInfoExtras() : NodeCode<TcInfoExtras> =
        match tcInfoNode with
        | TcInfoNode(_, fullGraphNode) ->
            node {
                let! _, tcInfoExtras = fullGraphNode.GetOrComputeValue()
                return tcInfoExtras
            }

    member _.GetOrComputeTcInfoWithExtras() =
        match tcInfoNode with
        | TcInfoNode(_, fullGraphNode) ->
            fullGraphNode.GetOrComputeValue()

    member private this.TypeCheck (partialCheck: bool) : NodeCode<TcInfoState> =
        match partialCheck, tcInfoStateOpt with
        | true, Some (PartialState _ as state)
        | true, Some (FullState _ as state) -> node { return state }
        | false, Some (FullState _ as state) -> node { return state }
        | _ ->

        node {
            match syntaxTreeOpt with
            | None -> 
                let! res = defaultTypeCheck ()
                return res
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
                    use _ = new CompilationGlobalsScope(errorLogger, BuildPhase.TypeCheck)

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
                        TypeCheckOneInput
                            ((fun () -> hadParseErrors || errorLogger.ErrorCount > 0),
                                tcConfig, tcImports,
                                tcGlobals,
                                None,
                                (if partialCheck then TcResultsSink.NoSink else TcResultsSink.WithSink sink),
                                prevTcState, input,
                                partialCheck)
                        |> NodeCode.FromCancellable        
                        
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
                                tcResolutions = (if keepAllBackgroundResolutions then sink.GetResolutions() else TcResolutions.Empty)
                                tcSymbolUses = (if keepAllBackgroundSymbolUses then sink.GetSymbolUses() else TcSymbolUses.Empty)
                                tcOpenDeclarations = sink.GetOpenDeclarations()
                                itemKeyStore = itemKeyStore
                                semanticClassificationKeyStore = semanticClassification
                            }
                        
                        return FullState(tcInfo, tcInfoExtras)
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
                         syntaxTreeOpt: SyntaxTree option) =
        BoundModel(tcConfig, tcGlobals, tcImports,
                      keepAssemblyContents, keepAllBackgroundResolutions,
                      keepAllBackgroundSymbolUses,
                      enableBackgroundItemKeyStoreAndSemanticClassification,
                      enablePartialTypeChecking,
                      beforeFileChecked,
                      fileChecked,
                      prevTcInfo,
                      syntaxTreeOpt,
                      None)

/// Global service state
type FrameworkImportsCacheKey = (*resolvedpath*)string list * string * (*TargetFrameworkDirectories*)string list * (*fsharpBinaries*)string * (*langVersion*)decimal

/// Represents a cache of 'framework' references that can be shared between multiple incremental builds
type FrameworkImportsCache(size) =

    let gate = obj()

    // Mutable collection protected via CompilationThreadToken
    let frameworkTcImportsCache = AgedLookup<AnyCallerThreadToken, FrameworkImportsCacheKey, GraphNode<(TcGlobals * TcImports)>>(size, areSimilar=(fun (x, y) -> x = y))

    /// Reduce the size of the cache in low-memory scenarios
    member _.Downsize() = frameworkTcImportsCache.Resize(AnyCallerThread, newKeepStrongly=0)

    /// Clear the cache
    member _.Clear() = frameworkTcImportsCache.Clear AnyCallerThread

    /// This function strips the "System" assemblies from the tcConfig and returns a age-cached TcImports for them.
    member _.GetNode(tcConfig: TcConfig, frameworkDLLs: AssemblyResolution list, nonFrameworkResolutions: AssemblyResolution list) =
        let frameworkDLLsKey =
            frameworkDLLs
            |> List.map (fun ar->ar.resolvedPath) // The cache key. Just the minimal data.
            |> List.sort  // Sort to promote cache hits.

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

        let node =
            lock gate (fun () ->
                match frameworkTcImportsCache.TryGet (AnyCallerThread, key) with
                | Some lazyWork -> lazyWork
                | None ->
                    let lazyWork = GraphNode(node {
                        let tcConfigP = TcConfigProvider.Constant tcConfig
                        return! TcImports.BuildFrameworkTcImports (tcConfigP, frameworkDLLs, nonFrameworkResolutions)
                    })
                    frameworkTcImportsCache.Put(AnyCallerThread, key, lazyWork)
                    lazyWork
            )
        node

    /// This function strips the "System" assemblies from the tcConfig and returns a age-cached TcImports for them.
    member this.Get(tcConfig: TcConfig) =
      node {
        // Split into installed and not installed.
        let frameworkDLLs, nonFrameworkResolutions, unresolved = TcAssemblyResolutions.SplitNonFoundationalResolutions(tcConfig)
        let node = this.GetNode(tcConfig, frameworkDLLs, nonFrameworkResolutions)
        let! tcGlobals, frameworkTcImports = node.GetOrComputeValue()
        return tcGlobals, frameworkTcImports, nonFrameworkResolutions, unresolved
      }

/// Represents the interim state of checking an assembly
[<Sealed>]
type PartialCheckResults (boundModel: BoundModel, timeStamp: DateTime) =

    member _.TcImports = boundModel.TcImports

    member _.TcGlobals = boundModel.TcGlobals

    member _.TcConfig = boundModel.TcConfig

    member _.TimeStamp = timeStamp

    member _.TryPeekTcInfo() = boundModel.TryPeekTcInfo()

    member _.TryPeekTcInfoWithExtras() = boundModel.TryPeekTcInfoWithExtras()

    member _.GetOrComputeTcInfo() = boundModel.GetOrComputeTcInfo()

    member _.GetOrComputeTcInfoWithExtras() = boundModel.GetOrComputeTcInfoWithExtras()

    member _.GetOrComputeItemKeyStoreIfEnabled() =
        node {
            let! info = boundModel.GetOrComputeTcInfoExtras()
            return info.itemKeyStore
        }

    member _.GetOrComputeSemanticClassificationIfEnabled() =
        node {
            let! info = boundModel.GetOrComputeTcInfoExtras()
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
type RawFSharpAssemblyDataBackedByLanguageService (tcConfig, tcGlobals, generatedCcu: CcuThunk, outfile, topAttrs, assemblyName, ilAssemRef) =

    let exportRemapping = MakeExportRemapping generatedCcu generatedCcu.Contents

    let sigData =
        let _sigDataAttributes, sigDataResources = EncodeSignatureData(tcConfig, tcGlobals, exportRemapping, generatedCcu, outfile, true)
        [ for r in sigDataResources  do
            let ccuName = GetSignatureDataResourceName r
            yield (ccuName, (fun () -> r.GetBytes())) ]

    let autoOpenAttrs = topAttrs.assemblyAttrs |> List.choose (List.singleton >> TryFindFSharpStringAttribute tcGlobals tcGlobals.attrib_AutoOpenAttribute)

    let ivtAttrs = topAttrs.assemblyAttrs |> List.choose (List.singleton >> TryFindFSharpStringAttribute tcGlobals tcGlobals.attrib_InternalsVisibleToAttribute)

    interface IRawFSharpAssemblyData with
        member _.GetAutoOpenAttributes() = autoOpenAttrs
        member _.GetInternalsVisibleToAttributes() =  ivtAttrs
        member _.TryGetILModuleDef() = None
        member _.GetRawFSharpSignatureData(_m, _ilShortAssemName, _filename) = sigData
        member _.GetRawFSharpOptimizationData(_m, _ilShortAssemName, _filename) = [ ]
        member _.GetRawTypeForwarders() = mkILExportedTypes []  // TODO: cross-project references with type forwarders
        member _.ShortAssemblyName = assemblyName
        member _.ILScopeRef = IL.ILScopeRef.Assembly ilAssemRef
        member _.ILAssemblyRefs = [] // These are not significant for service scenarios
        member _.HasAnyFSharpSignatureDataAttribute =  true
        member _.HasMatchingFSharpSignatureDataAttribute = true

type IncrementalBuilderState =
    {
        // stampedFileNames represent the real stamps of the files.
        // logicalStampedFileNames represent the stamps of the files that are used to calculate the project's logical timestamp.
        stampedFileNames: ImmutableArray<DateTime>
        logicalStampedFileNames: ImmutableArray<DateTime>
        stampedReferencedAssemblies: ImmutableArray<DateTime>
        initialBoundModel: GraphNode<BoundModel>
        boundModels: ImmutableArray<GraphNode<BoundModel>>
        finalizedBoundModel: GraphNode<((ILAssemblyRef * IRawFSharpAssemblyData option * TypedImplFile list option * BoundModel) * DateTime)>
    }

/// Manages an incremental build graph for the build of a single F# project
type IncrementalBuilder(
                        initialBoundModel: BoundModel,
                        tcGlobals,
                        nonFrameworkAssemblyInputs,
                        tcConfig: TcConfig,
                        outfile,
                        assemblyName,
                        lexResourceManager,
                        sourceFiles,
                        enablePartialTypeChecking,
                        beforeFileChecked: Event<string>,
                        fileChecked: Event<string>,
#if !NO_EXTENSIONTYPING
                        importsInvalidatedByTypeProvider: Event<unit>,
#endif
                        allDependencies) =

    let fileParsed = new Event<string>()
    let projectChecked = new Event<unit>()

    let defaultTimeStamp = DateTime.UtcNow

    let mutable isImportsInvalidated = false

#if !NO_EXTENSIONTYPING
    do importsInvalidatedByTypeProvider.Publish.Add(fun () -> isImportsInvalidated <- true)
#endif

    //----------------------------------------------------
    // START OF BUILD TASK FUNCTIONS

    /// Get the timestamp of the given file name.
    let StampFileNameTask (cache: TimeStampCache) (_m: range, filename: string, _isLastCompiland) =
        cache.GetFileTimeStamp filename

    /// Timestamps of referenced assemblies are taken from the file's timestamp.
    let StampReferencedAssemblyTask (cache: TimeStampCache) (_ref, timeStamper) =
        timeStamper cache

    // Link all the assemblies together and produce the input typecheck accumulator
    static let CombineImportedAssembliesTask (
                                              assemblyName, 
                                              tcConfig: TcConfig, 
                                              tcConfigP, 
                                              tcGlobals, 
                                              frameworkTcImports, 
                                              nonFrameworkResolutions, 
                                              unresolvedReferences, 
                                              dependencyProvider, 
                                              loadClosureOpt: LoadClosure option, 
                                              niceNameGen, 
                                              basicDependencies,
                                              keepAssemblyContents,
                                              keepAllBackgroundResolutions,
                                              keepAllBackgroundSymbolUses,
                                              enableBackgroundItemKeyStoreAndSemanticClassification,
                                              defaultPartialTypeChecking,
                                              beforeFileChecked,
                                              fileChecked,
                                              importsInvalidatedByTypeProvider: Event<unit>) : NodeCode<BoundModel> =
      node {
        let errorLogger = CompilationErrorLogger("CombineImportedAssembliesTask", tcConfig.errorSeverityOptions)
        use _ = new CompilationGlobalsScope(errorLogger, BuildPhase.Parameter)

        let! tcImports =
          node {
            try
                let! tcImports = TcImports.BuildNonFrameworkTcImports(tcConfigP, frameworkTcImports, nonFrameworkResolutions, unresolvedReferences, dependencyProvider)
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
                    ccu.Deref.InvalidateEvent.Add(fun _ ->
                        match capturedImportsInvalidated.TryGetTarget() with
                        | true, tg -> tg.Trigger()
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
                None) }

    /// Type check all files eagerly.
    let TypeCheckTask partialCheck (prevBoundModel: BoundModel) syntaxTree: NodeCode<BoundModel> =
        node {
            let! tcInfo = prevBoundModel.GetOrComputeTcInfo()
            let boundModel = prevBoundModel.Next(syntaxTree, tcInfo)

            // Eagerly type check
            // We need to do this to keep the expected behavior of events (namely fileChecked) when checking a file/project.
            if partialCheck then
                let! _ = boundModel.GetOrComputeTcInfo()
                ()
            else
                let! _ = boundModel.GetOrComputeTcInfoWithExtras()
                ()

            return boundModel
        }

    /// Finish up the typechecking to produce outputs for the rest of the compilation process
    let FinalizeTypeCheckTask (boundModels: ImmutableArray<BoundModel>) =
      node {
        let errorLogger = CompilationErrorLogger("FinalizeTypeCheckTask", tcConfig.errorSeverityOptions)
        use _ = new CompilationGlobalsScope(errorLogger, BuildPhase.TypeCheck)

        let! results =
            boundModels 
            |> Seq.map (fun boundModel -> node { 
                if enablePartialTypeChecking then
                    let! tcInfo = boundModel.GetOrComputeTcInfo()
                    return tcInfo, None
                else
                    let! tcInfo, tcInfoExtras = boundModel.GetOrComputeTcInfoWithExtras()
                    return tcInfo, tcInfoExtras.latestImplFile
            })
            |> Seq.map (fun work ->
                node {
                    let! tcInfo, latestImplFile = work
                    return (tcInfo.tcEnvAtEndOfFile, defaultArg tcInfo.topAttribs EmptyTopAttrs, latestImplFile, tcInfo.latestCcuSigForFile)
                }
            )
            |> NodeCode.Sequential

        let results = results |> List.ofSeq

        // Get the state at the end of the type-checking of the last file
        let finalBoundModel = boundModels.[boundModels.Length-1]

        let! finalInfo = finalBoundModel.GetOrComputeTcInfo()

        // Finish the checking
        let (_tcEnvAtEndOfLastFile, topAttrs, mimpls, _), tcState =
            TypeCheckMultipleInputsFinish (results, finalInfo.tcState)

        let ilAssemRef, tcAssemblyDataOpt, tcAssemblyExprOpt =
            try
                let tcState, tcAssemblyExpr, ccuContents = TypeCheckClosedInputSetFinish (mimpls, tcState)

                let generatedCcu = tcState.Ccu.CloneWithFinalizedContents(ccuContents)

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
                    let locale = TryFindFSharpStringAttribute tcGlobals (tcGlobals.FindSysAttrib "System.Reflection.AssemblyCultureAttribute") topAttrs.assemblyAttrs
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
                            Some  (RawFSharpAssemblyDataBackedByLanguageService (tcConfig, tcGlobals, generatedCcu, outfile, topAttrs, assemblyName, ilAssemRef) :> IRawFSharpAssemblyData)
                    with e ->
                        errorRecoveryNoRange e
                        None
                ilAssemRef, tcAssemblyDataOpt, Some tcAssemblyExpr
            with e ->
                errorRecoveryNoRange e
                mkSimpleAssemblyRef assemblyName, None, None

        let diagnostics = errorLogger.GetDiagnostics() :: finalInfo.tcErrorsRev
        let! finalBoundModelWithErrors = finalBoundModel.Finish(diagnostics, Some topAttrs)
        return ilAssemRef, tcAssemblyDataOpt, tcAssemblyExprOpt, finalBoundModelWithErrors
    }

    // END OF BUILD TASK FUNCTIONS
    // ---------------------------------------------------------------------------------------------

    // ---------------------------------------------------------------------------------------------
    // START OF BUILD DESCRIPTION

    let GetSyntaxTree (sourceRange: range, filename: string, isLastCompiland) =
        SyntaxTree(tcConfig, fileParsed, lexResourceManager, sourceRange, filename, isLastCompiland)

    // Inputs
    let fileNames = sourceFiles |> Array.ofList // TODO: This should be an immutable array.
    let referencedAssemblies =  nonFrameworkAssemblyInputs |> Array.ofList // TODO: This should be an immutable array.

    let createBoundModelGraphNode initialBoundModel (boundModels: ImmutableArray<GraphNode<BoundModel>>.Builder) i =
        let fileInfo = fileNames.[i]
        let prevBoundModelGraphNode =
            match i with
            | 0 (* first file *) -> initialBoundModel
            | _ -> boundModels.[i - 1]
        let syntaxTree = GetSyntaxTree fileInfo
        GraphNode(node {
            let! prevBoundModel = prevBoundModelGraphNode.GetOrComputeValue()
            return! TypeCheckTask enablePartialTypeChecking prevBoundModel syntaxTree
        })

    let rec createFinalizeBoundModelGraphNode (boundModels: ImmutableArray<GraphNode<BoundModel>>.Builder) =
        GraphNode(node {
            // Compute last bound model then get all the evaluated models.
            let! _ = boundModels.[boundModels.Count - 1].GetOrComputeValue()
            let boundModels =
                boundModels
                |> Seq.map (fun x -> x.TryPeekValue().Value)
                |> ImmutableArray.CreateRange

            let! result = FinalizeTypeCheckTask boundModels
            let result = (result, DateTime.UtcNow)
            return result
        })

    and computeStampedFileName (state: IncrementalBuilderState) (cache: TimeStampCache) slot fileInfo =
        let currentStamp = state.stampedFileNames.[slot]
        let stamp = StampFileNameTask cache fileInfo

        if currentStamp <> stamp then
            match state.boundModels.[slot].TryPeekValue() with
            // This prevents an implementation file that has a backing signature file from invalidating the rest of the build.
            | ValueSome(boundModel) when enablePartialTypeChecking && boundModel.BackingSignature.IsSome ->
                let newBoundModel = boundModel.ClearTcInfoExtras()
                { state with
                    boundModels = state.boundModels.RemoveAt(slot).Insert(slot, GraphNode(node { return newBoundModel }))
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
                    boundModels.[slot + j] <- createBoundModelGraphNode state.initialBoundModel boundModels (slot + j)

                { state with
                    // Something changed, the finalized view of the project must be invalidated.
                    finalizedBoundModel = createFinalizeBoundModelGraphNode boundModels

                    stampedFileNames = stampedFileNames.ToImmutable()
                    logicalStampedFileNames = logicalStampedFileNames.ToImmutable()
                    boundModels = boundModels.ToImmutable()
                }
        else
            state

    and computeStampedFileNames state (cache: TimeStampCache) =
        let mutable i = 0
        (state, fileNames)
        ||> Array.fold (fun state fileInfo ->
            let newState = computeStampedFileName state cache i fileInfo
            i <- i + 1
            newState
        )

    and computeStampedReferencedAssemblies state canTriggerInvalidation (cache: TimeStampCache) =
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
            // Build is invalidated. The build must be rebuilt with the newly updated references.
            if not isImportsInvalidated && canTriggerInvalidation then
                isImportsInvalidated <- true
            { state with
                stampedReferencedAssemblies = stampedReferencedAssemblies.ToImmutable()
            }
        else
            state

    let tryGetSlot (state: IncrementalBuilderState) slot =
        match state.boundModels.[slot].TryPeekValue() with
        | ValueSome boundModel ->
            (boundModel, state.stampedFileNames.[slot])
            |> Some
        | _ ->
            None

    let tryGetBeforeSlot (state: IncrementalBuilderState) slot =
        match slot with
        | 0 (* first file *) ->
            (initialBoundModel, DateTime.MinValue)
            |> Some
        | _ ->
            tryGetSlot state (slot - 1)

    let evalUpToTargetSlot (state: IncrementalBuilderState) targetSlot =
        node {
            if targetSlot < 0 then
                return Some(initialBoundModel, DateTime.MinValue)
            else
                let! boundModel = state.boundModels.[targetSlot].GetOrComputeValue()
                return Some(boundModel, state.stampedFileNames.[targetSlot])
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

    let gate = obj ()
    let mutable currentState =
        let cache = TimeStampCache(defaultTimeStamp)
        let initialBoundModel = GraphNode(node { return initialBoundModel })
        let boundModels = ImmutableArray.CreateBuilder(fileNames.Length)

        for slot = 0 to fileNames.Length - 1 do
            boundModels.Add(createBoundModelGraphNode initialBoundModel boundModels slot)

        let state =
            {
                stampedFileNames = Array.init fileNames.Length (fun _ -> DateTime.MinValue) |> ImmutableArray.CreateRange
                logicalStampedFileNames = Array.init fileNames.Length (fun _ -> DateTime.MinValue) |> ImmutableArray.CreateRange
                stampedReferencedAssemblies = Array.init referencedAssemblies.Length (fun _ -> DateTime.MinValue) |> ImmutableArray.CreateRange
                initialBoundModel = initialBoundModel
                boundModels = boundModels.ToImmutable()
                finalizedBoundModel = createFinalizeBoundModelGraphNode boundModels
            }
        let state = computeStampedReferencedAssemblies state false cache
        let state = computeStampedFileNames state cache
        state

    let computeProjectTimeStamp (state: IncrementalBuilderState) =
        let t1 = MaxTimeStampInDependencies state.stampedReferencedAssemblies
        let t2 = MaxTimeStampInDependencies state.logicalStampedFileNames
        max t1 t2

    let setCurrentState state cache (ct: CancellationToken) =
        lock gate (fun () ->
            ct.ThrowIfCancellationRequested()
            currentState <- computeStampedFileNames state cache
        )

    let checkFileTimeStamps (cache: TimeStampCache) =
        node {
            let! ct = NodeCode.CancellationToken
            setCurrentState currentState cache ct
        }

    do IncrementalBuilderEventTesting.MRU.Add(IncrementalBuilderEventTesting.IBECreated)

    member _.TcConfig = tcConfig

    member _.FileParsed = fileParsed.Publish

    member _.BeforeFileChecked = beforeFileChecked.Publish

    member _.FileChecked = fileChecked.Publish

    member _.ProjectChecked = projectChecked.Publish

#if !NO_EXTENSIONTYPING
    member _.ImportsInvalidatedByTypeProvider = importsInvalidatedByTypeProvider.Publish
#endif

    member _.IsReferencesInvalidated = 
        // fast path
        if isImportsInvalidated then true
        else 
            computeStampedReferencedAssemblies currentState true (TimeStampCache(defaultTimeStamp)) |> ignore
            isImportsInvalidated

    member _.AllDependenciesDeprecated = allDependencies

    member _.PopulatePartialCheckingResults () =
      node {
        let cache = TimeStampCache defaultTimeStamp // One per step
        do! checkFileTimeStamps cache
        let! _ = currentState.finalizedBoundModel.GetOrComputeValue()
        projectChecked.Trigger()
      }

    member builder.GetCheckResultsBeforeFileInProjectEvenIfStale filename: PartialCheckResults option  =
        let slotOfFile = builder.GetSlotOfFileName filename
        let result = tryGetBeforeSlot currentState slotOfFile

        match result with
        | Some (boundModel, timestamp) -> Some (PartialCheckResults (boundModel, timestamp))
        | _ -> None

    member builder.GetCheckResultsForFileInProjectEvenIfStale filename: PartialCheckResults option  =
        let slotOfFile = builder.GetSlotOfFileName filename
        let result = tryGetSlot currentState slotOfFile

        match result with
        | Some (boundModel, timestamp) -> Some (PartialCheckResults (boundModel, timestamp))
        | _ -> None

    member builder.TryGetCheckResultsBeforeFileInProject (filename) =
        let cache = TimeStampCache defaultTimeStamp
        let tmpState = computeStampedFileNames currentState cache

        let slotOfFile = builder.GetSlotOfFileName filename
        match tryGetBeforeSlot tmpState slotOfFile with
        | Some(boundModel, timestamp) -> PartialCheckResults(boundModel, timestamp) |> Some
        | _ -> None

    member builder.AreCheckResultsBeforeFileInProjectReady filename =
        (builder.TryGetCheckResultsBeforeFileInProject filename).IsSome

    member _.GetCheckResultsBeforeSlotInProject (slotOfFile) =
      node {
        let cache = TimeStampCache defaultTimeStamp
        do! checkFileTimeStamps cache
        let! result = evalUpToTargetSlot currentState (slotOfFile - 1)
        match result with
        | Some (boundModel, timestamp) -> return PartialCheckResults(boundModel, timestamp)
        | None -> return! failwith "Expected results to be ready. (GetCheckResultsBeforeSlotInProject)."
      }

    member _.GetFullCheckResultsBeforeSlotInProject (slotOfFile) =
      node {
        let cache = TimeStampCache defaultTimeStamp
        do! checkFileTimeStamps cache
        let! result = evalUpToTargetSlot currentState (slotOfFile - 1)
        match result with
        | Some (boundModel, timestamp) -> 
            let! _ = boundModel.GetOrComputeTcInfoExtras()
            return PartialCheckResults(boundModel, timestamp)
        | None -> return! failwith "Expected results to be ready. (GetFullCheckResultsBeforeSlotInProject)."
      }

    member builder.GetCheckResultsBeforeFileInProject (filename) =
        let slotOfFile = builder.GetSlotOfFileName filename
        builder.GetCheckResultsBeforeSlotInProject (slotOfFile)

    member builder.GetCheckResultsAfterFileInProject (filename) =
        let slotOfFile = builder.GetSlotOfFileName filename + 1
        builder.GetCheckResultsBeforeSlotInProject (slotOfFile)

    member builder.GetFullCheckResultsBeforeFileInProject (filename) =
        let slotOfFile = builder.GetSlotOfFileName filename
        builder.GetFullCheckResultsBeforeSlotInProject (slotOfFile)

    member builder.GetFullCheckResultsAfterFileInProject (filename) =
        node {
            let slotOfFile = builder.GetSlotOfFileName filename + 1
            let! result = builder.GetFullCheckResultsBeforeSlotInProject(slotOfFile)
            return result
        }

    member builder.GetCheckResultsAfterLastFileInProject () =
        builder.GetCheckResultsBeforeSlotInProject(builder.GetSlotsCount())

    member _.GetCheckResultsAndImplementationsForProject() =
      node {
        let cache = TimeStampCache(defaultTimeStamp)
        do! checkFileTimeStamps cache
        let! result = currentState.finalizedBoundModel.GetOrComputeValue()
        match result with
        | ((ilAssemRef, tcAssemblyDataOpt, tcAssemblyExprOpt, boundModel), timestamp) ->
            return PartialCheckResults (boundModel, timestamp), ilAssemRef, tcAssemblyDataOpt, tcAssemblyExprOpt
      }

    member builder.GetFullCheckResultsAndImplementationsForProject() =
        node {
            let! result = builder.GetCheckResultsAndImplementationsForProject()
            let results, _, _, _ = result
            let! _ = results.GetOrComputeTcInfoWithExtras() // Make sure we forcefully evaluate the info
            return result
        }

    member _.GetLogicalTimeStampForProject(cache) =
        let tmpState = computeStampedFileNames currentState cache
        computeProjectTimeStamp tmpState

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
        let fileInfo = fileNames.[slotOfFile]
        // re-parse on demand instead of retaining
        let syntaxTree = GetSyntaxTree fileInfo
        syntaxTree.Parse None

    member _.SourceFiles  = sourceFiles  |> List.map (fun (_, f, _) -> f)

    /// CreateIncrementalBuilder (for background type checking). Note that fsc.fs also
    /// creates an incremental builder used by the command line compiler.
    static member TryCreateIncrementalBuilderForProjectOptions
                      (legacyReferenceResolver, defaultFSharpBinariesDir,
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

      node {

        // Trap and report warnings and errors from creation.
        let delayedLogger = CapturingErrorLogger("IncrementalBuilderCreation")
        use _ = new CompilationGlobalsScope(delayedLogger, BuildPhase.Parameter)

        let! builderOpt =
         node {
          try

            // Create the builder.
            // Share intern'd strings across all lexing/parsing
            let resourceManager = new Lexhelp.LexResourceManager()

            /// Create a type-check configuration
            let tcConfigB, sourceFiles =

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

                tcConfigB.xmlDocInfoLoader <-
                    { new IXmlDocumentationInfoLoader with
                        /// Try to load xml documentation associated with an assembly by the same file path with the extension ".xml".
                        member _.TryLoad(assemblyFileName, _ilModule) =
                            let xmlFileName = Path.ChangeExtension(assemblyFileName, ".xml")

                            // REVIEW: File IO - Will eventually need to change this to use a file system interface of some sort.
                            XmlDocumentationInfo.TryCreateFromFile(xmlFileName)
                    }
                    |> Some

                tcConfigB, sourceFilesNew

            // If this is a builder for a script, re-apply the settings inferred from the
            // script and its load closure to the configuration.
            //
            // NOTE: it would probably be cleaner and more accurate to re-run the load closure at this point.
            let setupConfigFromLoadClosure () =
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

            setupConfigFromLoadClosure()

            let tcConfig = TcConfig.Create(tcConfigB, validate=true)
            let niceNameGen = NiceNameGenerator()
            let outfile, _, assemblyName = tcConfigB.DecideNames sourceFiles

            // Resolve assemblies and create the framework TcImports. This is done when constructing the
            // builder itself, rather than as an incremental task. This caches a level of "system" references. No type providers are
            // included in these references.
            let! (tcGlobals, frameworkTcImports, nonFrameworkResolutions, unresolvedReferences) = frameworkTcImportsCache.Get(tcConfig)

            // Note we are not calling errorLogger.GetDiagnostics() anywhere for this task.
            // This is ok because not much can actually go wrong here.
            let errorOptions = tcConfig.errorSeverityOptions
            let errorLogger = CompilationErrorLogger("nonFrameworkAssemblyInputs", errorOptions)
            use _ = new CompilationGlobalsScope(errorLogger, BuildPhase.Parameter)

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

            //
            //
            //
            //
            // Start importing

            let tcConfigP = TcConfigProvider.Constant tcConfig
            let beforeFileChecked = new Event<string>()
            let fileChecked = new Event<string>()

#if !NO_EXTENSIONTYPING
            let importsInvalidatedByTypeProvider = new Event<unit>()
#endif

            // Check for the existence of loaded sources and prepend them to the sources list if present.
            let sourceFiles = tcConfig.GetAvailableLoadedSources() @ (sourceFiles |>List.map (fun s -> rangeStartup, s))

            // Mark up the source files with an indicator flag indicating if they are the last source file in the project
            let sourceFiles =
                let flags, isExe = tcConfig.ComputeCanContainEntryPoint(sourceFiles |> List.map snd)
                ((sourceFiles, flags) ||> List.map2 (fun (m, nm) flag -> (m, nm, (flag, isExe))))

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
                match dependencyProvider with
                | None -> new DependencyProvider()
                | Some dependencyProvider -> dependencyProvider

            let! initialBoundModel = 
                CombineImportedAssembliesTask(
                    assemblyName,
                    tcConfig,
                    tcConfigP,
                    tcGlobals,
                    frameworkTcImports,
                    nonFrameworkResolutions,
                    unresolvedReferences,
                    dependencyProvider,
                    loadClosureOpt,
                    niceNameGen,
                    basicDependencies,
                    keepAssemblyContents,
                    keepAllBackgroundResolutions,
                    keepAllBackgroundSymbolUses,
                    enableBackgroundItemKeyStoreAndSemanticClassification,
                    enablePartialTypeChecking,
                    beforeFileChecked,
                    fileChecked,
                    importsInvalidatedByTypeProvider
                )

            let builder =
                new IncrementalBuilder(
                    initialBoundModel,
                    tcGlobals,
                    nonFrameworkAssemblyInputs,
                    tcConfig,
                    outfile,
                    assemblyName,
                    resourceManager,
                    sourceFiles,
                    enablePartialTypeChecking,
                    beforeFileChecked,
                    fileChecked,
#if !NO_EXTENSIONTYPING
                    importsInvalidatedByTypeProvider,
#endif
                    allDependencies)
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
