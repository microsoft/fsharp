// Copyright (c) Microsoft Corporation. All Rights Reserved. See License.txt in the project root for license information.

/// Contains logic to coordinate assembly resolution and manage the TcImports table of referenced
/// assemblies.
module internal FSharp.Compiler.CompilerImports

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Diagnostics
open System.IO

open Internal.Utilities
open Internal.Utilities.Collections
open Internal.Utilities.FSharpEnvironment
open Internal.Utilities.Library
open Internal.Utilities.Library.Extras

open FSharp.Compiler
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AbstractIL.ILBinaryReader
open FSharp.Compiler.AbstractIL.ILX
open FSharp.Compiler.AbstractIL.Diagnostics
open FSharp.Compiler.CheckDeclarations
open FSharp.Compiler.CompilerGlobalState
open FSharp.Compiler.CompilerConfig
open FSharp.Compiler.DependencyManager
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Import
open FSharp.Compiler.IO
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Syntax.PrettyNaming
open FSharp.Compiler.SyntaxTreeOps
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Compiler.Xml
open FSharp.Compiler.TypedTreePickle
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.BuildGraph

#if !NO_EXTENSIONTYPING
open FSharp.Compiler.ExtensionTyping
open FSharp.Core.CompilerServices
#endif

let (++) x s = x @ [s]

//----------------------------------------------------------------------------
// Signature and optimization data blobs
//--------------------------------------------------------------------------

let IsSignatureDataResource (r: ILResource) =
    r.Name.StartsWithOrdinal FSharpSignatureDataResourceName ||
    r.Name.StartsWithOrdinal FSharpSignatureDataResourceName2

let IsOptimizationDataResource (r: ILResource) =
    r.Name.StartsWithOrdinal FSharpOptimizationDataResourceName||
    r.Name.StartsWithOrdinal FSharpOptimizationDataResourceName2

let GetSignatureDataResourceName (r: ILResource) =
    if r.Name.StartsWithOrdinal FSharpSignatureDataResourceName then
        String.dropPrefix r.Name FSharpSignatureDataResourceName
    elif r.Name.StartsWithOrdinal FSharpSignatureDataResourceName2 then
        String.dropPrefix r.Name FSharpSignatureDataResourceName2
    else failwith "GetSignatureDataResourceName"

let GetOptimizationDataResourceName (r: ILResource) =
    if r.Name.StartsWithOrdinal FSharpOptimizationDataResourceName then
        String.dropPrefix r.Name FSharpOptimizationDataResourceName
    elif r.Name.StartsWithOrdinal FSharpOptimizationDataResourceName2 then
        String.dropPrefix r.Name FSharpOptimizationDataResourceName2
    else failwith "GetOptimizationDataResourceName"

let IsReflectedDefinitionsResource (r: ILResource) =
    r.Name.StartsWithOrdinal(QuotationPickler.SerializedReflectedDefinitionsResourceNameBase)

let MakeILResource rName bytes =
    { Name = rName
      Location = ILResourceLocation.Local(ByteStorage.FromByteArray(bytes))
      Access = ILResourceAccess.Public
      CustomAttrsStored = storeILCustomAttrs emptyILCustomAttrs
      MetadataIndex = NoMetadataIdx }

let PickleToResource inMem file (g: TcGlobals) scope rName p x =
    let file = PathMap.apply g.pathMap file

    let bytes = pickleObjWithDanglingCcus inMem file g scope p x
    let byteStorage =
        if inMem then
            ByteStorage.FromMemoryAndCopy(bytes.AsMemory(), useBackingMemoryMappedFile = true)
        else
            ByteStorage.FromByteArray(bytes.AsMemory().ToArray())

    (bytes :> IDisposable).Dispose()

    { Name = rName
      Location = ILResourceLocation.Local(byteStorage)
      Access = ILResourceAccess.Public
      CustomAttrsStored = storeILCustomAttrs emptyILCustomAttrs
      MetadataIndex = NoMetadataIdx }

let GetSignatureData (file, ilScopeRef, ilModule, byteReader) : PickledDataWithReferences<PickledCcuInfo> =
    unpickleObjWithDanglingCcus file ilScopeRef ilModule unpickleCcuInfo (byteReader())

let WriteSignatureData (tcConfig: TcConfig, tcGlobals, exportRemapping, ccu: CcuThunk, filename, inMem) : ILResource =
    let mspec = ccu.Contents
    let mspec = ApplyExportRemappingToEntity tcGlobals exportRemapping mspec
    // For historical reasons, we use a different resource name for FSharp.Core, so older F# compilers
    // don't complain when they see the resource.
    let rName = if ccu.AssemblyName = getFSharpCoreLibraryName then FSharpSignatureDataResourceName2 else FSharpSignatureDataResourceName

    let includeDir =
        if String.IsNullOrEmpty tcConfig.implicitIncludeDir then ""
        else
            tcConfig.implicitIncludeDir
            |> FileSystem.GetFullPathShim
            |> PathMap.applyDir tcGlobals.pathMap

    PickleToResource inMem filename tcGlobals ccu (rName+ccu.AssemblyName) pickleCcuInfo
        { mspec=mspec
          compileTimeWorkingDir=includeDir
          usesQuotations = ccu.UsesFSharp20PlusQuotations }

let GetOptimizationData (file, ilScopeRef, ilModule, byteReader) =
    unpickleObjWithDanglingCcus file ilScopeRef ilModule Optimizer.u_CcuOptimizationInfo (byteReader())

let WriteOptimizationData (tcGlobals, filename, inMem, ccu: CcuThunk, modulInfo) =
    // For historical reasons, we use a different resource name for FSharp.Core, so older F# compilers
    // don't complain when they see the resource.
    let rName = if ccu.AssemblyName = getFSharpCoreLibraryName then FSharpOptimizationDataResourceName2 else FSharpOptimizationDataResourceName
    PickleToResource inMem filename tcGlobals ccu (rName+ccu.AssemblyName) Optimizer.p_CcuOptimizationInfo modulInfo

let EncodeSignatureData(tcConfig: TcConfig, tcGlobals, exportRemapping, generatedCcu, outfile, isIncrementalBuild) =
    if tcConfig.GenerateSignatureData then
        let resource = WriteSignatureData (tcConfig, tcGlobals, exportRemapping, generatedCcu, outfile, isIncrementalBuild)
        // The resource gets written to a file for FSharp.Core
        let useDataFiles = (tcConfig.useOptimizationDataFile || tcGlobals.compilingFslib) && not isIncrementalBuild

        if useDataFiles then
            let sigDataFileName = (FileSystemUtils.chopExtension outfile)+".sigdata"
            let bytes = resource.GetBytes()
            use fileStream = FileSystem.OpenFileForWriteShim(sigDataFileName, FileMode.Create, FileAccess.ReadWrite, FileShare.None)

            bytes.CopyTo fileStream
        let resources =
            [ resource ]
        let sigAttr = mkSignatureDataVersionAttr tcGlobals (parseILVersion Internal.Utilities.FSharpEnvironment.FSharpBinaryMetadataFormatRevision)
        [sigAttr], resources
      else
        [], []

let EncodeOptimizationData(tcGlobals, tcConfig: TcConfig, outfile, exportRemapping, data, isIncrementalBuild) =
    if tcConfig.GenerateOptimizationData then
        let data = map2Of2 (Optimizer.RemapOptimizationInfo tcGlobals exportRemapping) data
        // As with the sigdata file, the optdata gets written to a file for FSharp.Core
        let useDataFiles = (tcConfig.useOptimizationDataFile || tcGlobals.compilingFslib) && not isIncrementalBuild

        if useDataFiles then
            let ccu, modulInfo = data
            let bytes = TypedTreePickle.pickleObjWithDanglingCcus isIncrementalBuild outfile tcGlobals ccu Optimizer.p_CcuOptimizationInfo modulInfo
            let optDataFileName = (FileSystemUtils.chopExtension outfile)+".optdata"
            use fileStream = FileSystem.OpenFileForWriteShim(optDataFileName, FileMode.Create, FileAccess.ReadWrite, FileShare.None)
            fileStream.Write(bytes)

        let (ccu, optData) =
            if tcConfig.onlyEssentialOptimizationData then
                map2Of2 Optimizer.AbstractOptimizationInfoToEssentials data
            else
                data
        [ WriteOptimizationData (tcGlobals, outfile, isIncrementalBuild, ccu, optData) ]
    else
        [ ]

exception AssemblyNotResolved of (*originalName*) string * range

exception MSBuildReferenceResolutionWarning of (*MSBuild warning code*)string * (*Message*)string * range

exception MSBuildReferenceResolutionError of (*MSBuild warning code*)string * (*Message*)string * range

let OpenILBinary(filename, reduceMemoryUsage, pdbDirPath, shadowCopyReferences, tryGetMetadataSnapshot) =
    let opts: ILReaderOptions =
        { metadataOnly = MetadataOnlyFlag.Yes
          reduceMemoryUsage = reduceMemoryUsage
          pdbDirPath = pdbDirPath
          tryGetMetadataSnapshot = tryGetMetadataSnapshot }

    let location =
#if FX_NO_APP_DOMAINS
        // In order to use memory mapped files on the shadow copied version of the Assembly, we `preload the assembly
        // We swallow all exceptions so that we do not change the exception contract of this API
        if shadowCopyReferences then
          try
            System.Reflection.Assembly.ReflectionOnlyLoadFrom(filename).Location
          with e -> filename
        else
#else
          ignore shadowCopyReferences
#endif
          filename
    AssemblyReader.GetILModuleReader(location, opts)

[<RequireQualifiedAccess>]
type ResolveAssemblyReferenceMode = Speculative | ReportErrors

#if !NO_EXTENSIONTYPING
type ResolvedExtensionReference = ResolvedExtensionReference of string * AssemblyReference list * Tainted<ITypeProvider> list
#endif

#if DEBUG
[<System.Diagnostics.DebuggerDisplayAttribute("AssemblyResolution({resolvedPath})")>]
#endif
type AssemblyResolution =
    {  /// The original reference to the assembly.
       originalReference: AssemblyReference

       /// Path to the resolvedFile
       resolvedPath: string

       /// Create the tooltip text for the assembly reference
       prepareToolTip: unit -> string

       /// Whether or not this is an installed system assembly (for example, System.dll)
       sysdir: bool

       /// Lazily populated ilAssemblyRef for this reference.
       mutable ilAssemblyRef: ILAssemblyRef option
     }
    override this.ToString() = sprintf "%s%s" (if this.sysdir then "[sys]" else "") this.resolvedPath

    member this.ProjectReference = this.originalReference.ProjectReference

    /// Compute the ILAssemblyRef for a resolved assembly. This is done by reading the binary if necessary. The result
    /// is cached.
    ///
    /// Only used in F# Interactive
    member this.GetILAssemblyRef(reduceMemoryUsage, tryGetMetadataSnapshot) =
        match this.ilAssemblyRef with
        | Some assemblyRef -> assemblyRef
        | None ->
            match this.ProjectReference with
            | Some _ -> failwith "IProjectReference is not allowed to be used in GetILAssemblyRef"
            | None -> ()

            let assemblyRef =
                let readerSettings: ILReaderOptions =
                    { pdbDirPath=None
                      reduceMemoryUsage = reduceMemoryUsage
                      metadataOnly = MetadataOnlyFlag.Yes
                      tryGetMetadataSnapshot = tryGetMetadataSnapshot }
                use reader = OpenILModuleReader this.resolvedPath readerSettings
                mkRefToILAssembly reader.ILModuleDef.ManifestOfAssembly
            this.ilAssemblyRef <- Some assemblyRef
            assemblyRef

type ImportedBinary =
    { FileName: string
      RawMetadata: IRawFSharpAssemblyData
#if !NO_EXTENSIONTYPING
      ProviderGeneratedAssembly: System.Reflection.Assembly option
      IsProviderGenerated: bool
      ProviderGeneratedStaticLinkMap: ProvidedAssemblyStaticLinkingMap option
#endif
      ILAssemblyRefs: ILAssemblyRef list
      ILScopeRef: ILScopeRef }

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

type TcImportsLockToken() =
   interface LockToken

type TcImportsLock = Lock<TcImportsLockToken>   

let RequireTcImportsLock (_tcitok: TcImportsLockToken, _thingProtected: 'T) = ()

type TcConfig with

    member tcConfig.TryResolveLibWithDirectories (r: AssemblyReference) =
        let m, nm = r.Range, r.Text
        use unwindBuildPhase = PushThreadBuildPhaseUntilUnwind BuildPhase.Parameter
        // Only want to resolve certain extensions (otherwise, 'System.Xml' is ambiguous).
        // MSBuild resolution is limited to .exe and .dll so do the same here.
        let ext = System.IO.Path.GetExtension nm
        let isNetModule = String.Compare(ext, ".netmodule", StringComparison.OrdinalIgnoreCase)=0

        // See if the language service has already produced the contents of the assembly for us, virtually
        match r.ProjectReference with
        | Some _ ->
            let resolved = r.Text
            let sysdir = tcConfig.IsSystemAssembly resolved
            Some
                { originalReference = r
                  resolvedPath = resolved
                  prepareToolTip = (fun () -> resolved)
                  sysdir = sysdir
                  ilAssemblyRef = None }
        | None ->

        if String.Compare(ext, ".dll", StringComparison.OrdinalIgnoreCase)=0
           || String.Compare(ext, ".exe", StringComparison.OrdinalIgnoreCase)=0
           || isNetModule then

            let searchPaths =
                // if this is a #r reference (not from dummy range), make sure the directory of the declaring
                // file is included in the search path. This should ideally already be one of the search paths, but
                // during some global checks it won't be. We append to the end of the search list so that this is the last
                // place that is checked.
                let isPoundRReference (r: range) =
                    not (Range.equals r range0) &&
                    not (Range.equals r rangeStartup) &&
                    not (Range.equals r rangeCmdArgs) &&
                    FileSystem.IsPathRootedShim r.FileName

                if isPoundRReference m then
                    tcConfig.GetSearchPathsForLibraryFiles() @ [Path.GetDirectoryName(m.FileName)]
                else
                    tcConfig.GetSearchPathsForLibraryFiles()

            let resolved = TryResolveFileUsingPaths(searchPaths, m, nm)
            match resolved with
            | Some resolved ->
                let sysdir = tcConfig.IsSystemAssembly resolved
                Some
                    { originalReference = r
                      resolvedPath = resolved
                      prepareToolTip = (fun () ->
                            let fusionName = System.Reflection.AssemblyName.GetAssemblyName(resolved).ToString()
                            let line(append: string) = append.Trim([|' '|])+"\n"
                            line resolved + line fusionName)
                      sysdir = sysdir
                      ilAssemblyRef = None }
            | None -> None
        else None

    member tcConfig.ResolveLibWithDirectories (ccuLoadFailureAction, r: AssemblyReference) =
        let m, nm = r.Range, r.Text
        use unwindBuildPhase = PushThreadBuildPhaseUntilUnwind BuildPhase.Parameter
        // test for both libraries and executables
        let ext = System.IO.Path.GetExtension nm
        let isExe = (String.Compare(ext, ".exe", StringComparison.OrdinalIgnoreCase) = 0)
        let isDLL = (String.Compare(ext, ".dll", StringComparison.OrdinalIgnoreCase) = 0)
        let isNetModule = (String.Compare(ext, ".netmodule", StringComparison.OrdinalIgnoreCase) = 0)

        let rs =
            if isExe || isDLL || isNetModule then
                [r]
            else
                [AssemblyReference(m, nm+".dll", None);AssemblyReference(m, nm+".exe", None);AssemblyReference(m, nm+".netmodule", None)]

        match rs |> List.tryPick (fun r -> tcConfig.TryResolveLibWithDirectories(r)) with
        | Some res -> Some res
        | None ->
            match ccuLoadFailureAction with
            | CcuLoadFailureAction.RaiseError ->
                let searchMessage = String.concat "\n " (tcConfig.GetSearchPathsForLibraryFiles())
                raise (FileNameNotResolved(nm, searchMessage, m))
            | CcuLoadFailureAction.ReturnNone -> None

    member tcConfig.MsBuildResolve (references, mode, errorAndWarningRange, showMessages) =
        let logMessage showMessages =
            if showMessages && tcConfig.showReferenceResolutions then (fun (message: string)->dprintf "%s\n" message)
            else ignore

        let logDiagnostic showMessages =
            (fun isError code message->
                if showMessages && mode = ResolveAssemblyReferenceMode.ReportErrors then
                  if isError then
                    errorR(MSBuildReferenceResolutionError(code, message, errorAndWarningRange))
                  else
                    match code with
                    // These are warnings that mean 'not resolved' for some assembly.
                    // Note that we don't get to know the name of the assembly that couldn't be resolved.
                    // Ignore these and rely on the logic below to emit an error for each unresolved reference.
                    | "MSB3246" // Resolved file has a bad image, no metadata, or is otherwise inaccessible.
                    | "MSB3106"
                        -> ()
                    | _ ->
                        if code = "MSB3245" then
                            errorR(MSBuildReferenceResolutionWarning(code, message, errorAndWarningRange))
                        else
                            warning(MSBuildReferenceResolutionWarning(code, message, errorAndWarningRange)))

        let targetProcessorArchitecture =
            match tcConfig.platform with
            | None -> "MSIL"
            | Some X86 -> "x86"
            | Some AMD64 -> "amd64"
            | Some IA64 -> "ia64"

        try
            tcConfig.legacyReferenceResolver.Impl.Resolve
                (tcConfig.resolutionEnvironment,
                references,
                tcConfig.targetFrameworkVersion,
                tcConfig.GetTargetFrameworkDirectories(),
                targetProcessorArchitecture,
                tcConfig.fsharpBinariesDir, // FSharp binaries directory
                tcConfig.includes, // Explicit include directories
                tcConfig.implicitIncludeDir, // Implicit include directory (likely the project directory)
                logMessage showMessages, logDiagnostic showMessages)
        with
            | LegacyResolutionFailure -> error(Error(FSComp.SR.buildAssemblyResolutionFailed(), errorAndWarningRange))


    // NOTE!! if mode=Speculative then this method must not report ANY warnings or errors through 'warning' or 'error'. Instead
    // it must return warnings and errors as data
    //
    // NOTE!! if mode=ReportErrors then this method must not raise exceptions. It must just report the errors and recover
    static member TryResolveLibsUsingMSBuildRules (tcConfig: TcConfig,
            originalReferences: AssemblyReference list,
            errorAndWarningRange: range,
            mode: ResolveAssemblyReferenceMode) : AssemblyResolution list * UnresolvedAssemblyReference list =

        use unwindBuildPhase = PushThreadBuildPhaseUntilUnwind BuildPhase.Parameter
        if tcConfig.useSimpleResolution then
            failwith "MSBuild resolution is not supported."
        if originalReferences=[] then [], []
        else
            // Group references by name with range values in the grouped value list.
            // In the grouped reference, store the index of the last use of the reference.
            let groupedReferences =
                originalReferences
                |> List.indexed
                |> Seq.groupBy(fun (_, reference) -> reference.Text)
                |> Seq.map(fun (assemblyName, assemblyAndIndexGroup)->
                    let assemblyAndIndexGroup = assemblyAndIndexGroup |> List.ofSeq
                    let highestPosition = assemblyAndIndexGroup |> List.maxBy fst |> fst
                    let assemblyGroup = assemblyAndIndexGroup |> List.map snd
                    assemblyName, highestPosition, assemblyGroup)
                |> Array.ofSeq

            // First, try to resolve everything as a file using simple resolution
            let resolvedAsFile =
                groupedReferences
                |> Array.map(fun (_filename, maxIndexOfReference, references)->
                                let assemblyResolution = references |> List.choose (fun r -> tcConfig.TryResolveLibWithDirectories r)
                                (maxIndexOfReference, assemblyResolution))
                |> Array.filter(fun (_, refs)->refs |> isNil |> not)

            let toMsBuild = [|0..groupedReferences.Length-1|]
                             |> Array.map(fun i->(p13 groupedReferences.[i]), (p23 groupedReferences.[i]), i)
                             |> Array.filter (fun (_, i0, _)->resolvedAsFile|>Array.exists(fun (i1, _) -> i0=i1)|>not)
                             |> Array.map(fun (ref, _, i)->ref, string i)

            let resolutions = tcConfig.MsBuildResolve(toMsBuild, mode, errorAndWarningRange, (*showMessages*)true)

            // Map back to original assembly resolutions.
            let resolvedByMsbuild =
                resolutions
                    |> Array.map(fun resolvedFile ->
                                    let i = int resolvedFile.baggage
                                    let _, maxIndexOfReference, ms = groupedReferences.[i]
                                    let assemblyResolutions =
                                        ms|>List.map(fun originalReference ->
                                                    System.Diagnostics.Debug.Assert(FileSystem.IsPathRootedShim(resolvedFile.itemSpec), sprintf "msbuild-resolved path is not absolute: '%s'" resolvedFile.itemSpec)
                                                    let canonicalItemSpec = FileSystem.GetFullPathShim(resolvedFile.itemSpec)
                                                    { originalReference=originalReference
                                                      resolvedPath=canonicalItemSpec
                                                      prepareToolTip = (fun () -> resolvedFile.prepareToolTip (originalReference.Text, canonicalItemSpec))
                                                      sysdir= tcConfig.IsSystemAssembly canonicalItemSpec
                                                      ilAssemblyRef = None })
                                    (maxIndexOfReference, assemblyResolutions))

            // When calculating the resulting resolutions, we're going to use the index of the reference
            // in the original specification and resort it to match the ordering that we had.
            let resultingResolutions =
                    [resolvedByMsbuild;resolvedAsFile]
                    |> Array.concat
                    |> Array.sortBy fst
                    |> Array.map snd
                    |> List.ofArray
                    |> List.concat

            // O(N^2) here over a small set of referenced assemblies.
            let IsResolved(originalName: string) =
                if resultingResolutions |> List.exists(fun resolution -> resolution.originalReference.Text = originalName) then true
                else
                    // MSBuild resolution may have unified the result of two duplicate references. Try to re-resolve now.
                    // If re-resolution worked then this was a removed duplicate.
                    tcConfig.MsBuildResolve([|originalName, ""|], mode, errorAndWarningRange, (*showMessages*)false).Length<>0

            let unresolvedReferences =
                    groupedReferences
                    //|> Array.filter(p13 >> IsNotFileOrIsAssembly)
                    |> Array.filter(p13 >> IsResolved >> not)
                    |> List.ofArray

            // If mode=Speculative, then we haven't reported any errors.
            // We report the error condition by returning an empty list of resolutions
            if mode = ResolveAssemblyReferenceMode.Speculative && (List.length unresolvedReferences) > 0 then
                [], (List.ofArray groupedReferences) |> List.map (fun (name, _, r) -> (name, r)) |> List.map UnresolvedAssemblyReference
            else
                resultingResolutions, unresolvedReferences |> List.map (fun (name, _, r) -> (name, r)) |> List.map UnresolvedAssemblyReference

[<Sealed>]
type TcAssemblyResolutions(tcConfig: TcConfig, results: AssemblyResolution list, unresolved: UnresolvedAssemblyReference list) =

    let originalReferenceToResolution = results |> List.map (fun r -> r.originalReference.Text, r) |> Map.ofList
    let resolvedPathToResolution = results |> List.map (fun r -> r.resolvedPath, r) |> Map.ofList

    /// Add some resolutions to the map of resolution results.
    member _.AddResolutionResults newResults = TcAssemblyResolutions(tcConfig, results @ newResults, unresolved)

    /// Add some unresolved results.
    member _.AddUnresolvedReferences newUnresolved = TcAssemblyResolutions(tcConfig, results, unresolved @ newUnresolved)

    /// Get information about referenced DLLs
    member _.GetAssemblyResolutions() = results

    member _.GetUnresolvedReferences() = unresolved

    member _.TryFindByOriginalReference(assemblyReference: AssemblyReference) = originalReferenceToResolution.TryFind assemblyReference.Text

    /// Only used by F# Interactive
    member _.TryFindByExactILAssemblyRef (assemblyRef) =
        results |> List.tryFind (fun ar->
            let r = ar.GetILAssemblyRef(tcConfig.reduceMemoryUsage, tcConfig.tryGetMetadataSnapshot)
            r = assemblyRef)

    /// Only used by F# Interactive
    member _.TryFindBySimpleAssemblyName (simpleAssemName) =
        results |> List.tryFind (fun ar->
            let r = ar.GetILAssemblyRef(tcConfig.reduceMemoryUsage, tcConfig.tryGetMetadataSnapshot)
            r.Name = simpleAssemName)

    member _.TryFindByResolvedPath nm = resolvedPathToResolution.TryFind nm

    member _.TryFindByOriginalReferenceText nm = originalReferenceToResolution.TryFind nm

    static member ResolveAssemblyReferences (tcConfig: TcConfig, assemblyList: AssemblyReference list, knownUnresolved: UnresolvedAssemblyReference list) : TcAssemblyResolutions =
        let resolved, unresolved =
            if tcConfig.useSimpleResolution then
                let resolutions =
                    assemblyList
                    |> List.map (fun assemblyReference ->
                           try
                               Choice1Of2 (tcConfig.ResolveLibWithDirectories (CcuLoadFailureAction.RaiseError, assemblyReference) |> Option.get)
                           with e ->
                               errorRecovery e assemblyReference.Range
                               Choice2Of2 assemblyReference)
                let successes = resolutions |> List.choose (function Choice1Of2 x -> Some x | _ -> None)
                let failures = resolutions |> List.choose (function Choice2Of2 x -> Some (UnresolvedAssemblyReference(x.Text, [x])) | _ -> None)
                successes, failures
            else
                // we don't want to do assembly resolution concurrently, we assume MSBuild doesn't handle this
                TcConfig.TryResolveLibsUsingMSBuildRules (tcConfig, assemblyList, rangeStartup, ResolveAssemblyReferenceMode.ReportErrors)
        TcAssemblyResolutions(tcConfig, resolved, unresolved @ knownUnresolved)

    static member GetAllDllReferences (tcConfig: TcConfig) = [
            let primaryReference = tcConfig.PrimaryAssemblyDllReference()

            let assumeDotNetFramework = primaryReference.SimpleAssemblyNameIs("mscorlib")

            if not tcConfig.compilingFslib then
                yield tcConfig.CoreLibraryDllReference()
                if assumeDotNetFramework then
                    // When building desktop then we need these additional dependencies
                    yield AssemblyReference(rangeStartup, "System.Numerics.dll", None)
                    yield AssemblyReference(rangeStartup, "System.dll", None)
                    let asm = AssemblyReference(rangeStartup, "netstandard.dll", None)
                    let found =
                        if tcConfig.useSimpleResolution then
                            match tcConfig.ResolveLibWithDirectories (CcuLoadFailureAction.ReturnNone, asm) with
                            | Some _ -> true
                            | None -> false
                        else
                            let resolutions = tcConfig.MsBuildResolve([|asm.Text, ""|], ResolveAssemblyReferenceMode.Speculative, rangeStartup, (*showMessages*)false)
                            resolutions.Length = 1
                    if found then yield asm

            if tcConfig.framework then
                let references, _useDotNetFramework = tcConfig.FxResolver.GetDefaultReferences(tcConfig.useFsiAuxLib)
                for s in references do
                    yield AssemblyReference(rangeStartup, (if s.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) then s else s+".dll"), None)

            yield! tcConfig.referencedDLLs
        ]

    static member SplitNonFoundationalResolutions (tcConfig: TcConfig) =
        let assemblyList = TcAssemblyResolutions.GetAllDllReferences tcConfig
        let resolutions = TcAssemblyResolutions.ResolveAssemblyReferences (tcConfig, assemblyList, tcConfig.knownUnresolvedReferences)
        let frameworkDLLs, nonFrameworkReferences = resolutions.GetAssemblyResolutions() |> List.partition (fun r -> r.sysdir)
        let unresolved = resolutions.GetUnresolvedReferences()
#if DEBUG
        let mutable itFailed = false
        let addedText = "\nIf you want to debug this right now, attach a debugger, and put a breakpoint in 'CompileOps.fs' near the text '!itFailed', and you can re-step through the assembly resolution logic."

        for (UnresolvedAssemblyReference(referenceText, _ranges)) in unresolved do
            if referenceText.Contains("mscorlib") then
                System.Diagnostics.Debug.Assert(false, sprintf "whoops, did not resolve mscorlib: '%s'%s" referenceText addedText)
                itFailed <- true

        for x in frameworkDLLs do
            if not(FileSystem.IsPathRootedShim(x.resolvedPath)) then
                System.Diagnostics.Debug.Assert(false, sprintf "frameworkDLL should be absolute path: '%s'%s" x.resolvedPath addedText)
                itFailed <- true

        for x in nonFrameworkReferences do
            if not(FileSystem.IsPathRootedShim(x.resolvedPath)) then
                System.Diagnostics.Debug.Assert(false, sprintf "nonFrameworkReference should be absolute path: '%s'%s" x.resolvedPath addedText)
                itFailed <- true

        if itFailed then
            // idea is, put a breakpoint here and then step through
            let assemblyList = TcAssemblyResolutions.GetAllDllReferences tcConfig
            let resolutions = TcAssemblyResolutions.ResolveAssemblyReferences (tcConfig, assemblyList, [])
            let _frameworkDLLs, _nonFrameworkReferences = resolutions.GetAssemblyResolutions() |> List.partition (fun r -> r.sysdir)
            ()
#endif
        frameworkDLLs, nonFrameworkReferences, unresolved

    static member BuildFromPriorResolutions (tcConfig: TcConfig, resolutions, knownUnresolved) =
        let references = resolutions |> List.map (fun r -> r.originalReference)
        TcAssemblyResolutions.ResolveAssemblyReferences (tcConfig, references, knownUnresolved)

    static member GetAssemblyResolutionInformation(tcConfig: TcConfig) =
        use unwindBuildPhase = PushThreadBuildPhaseUntilUnwind BuildPhase.Parameter
        let assemblyList = TcAssemblyResolutions.GetAllDllReferences tcConfig
        let resolutions = TcAssemblyResolutions.ResolveAssemblyReferences (tcConfig, assemblyList, [])
        resolutions.GetAssemblyResolutions(), resolutions.GetUnresolvedReferences()

//----------------------------------------------------------------------------
// Abstraction for project reference

let GetNameOfILModule (m: ILModuleDef) =
    match m.Manifest with
    | Some manifest -> manifest.Name
    | None -> m.Name

let MakeScopeRefForILModule (ilModule: ILModuleDef) =
    match ilModule.Manifest with
    | Some m -> ILScopeRef.Assembly (mkRefToILAssembly m)
    | None -> ILScopeRef.Module (mkRefToILModule ilModule)

let GetCustomAttributesOfILModule (ilModule: ILModuleDef) =
    (match ilModule.Manifest with Some m -> m.CustomAttrs | None -> ilModule.CustomAttrs).AsList

let GetAutoOpenAttributes ilModule =
    ilModule |> GetCustomAttributesOfILModule |> List.choose TryFindAutoOpenAttr

let GetInternalsVisibleToAttributes ilModule =
    ilModule |> GetCustomAttributesOfILModule |> List.choose TryFindInternalsVisibleToAttr

type RawFSharpAssemblyDataBackedByFileOnDisk (ilModule: ILModuleDef, ilAssemblyRefs) =
    let externalSigAndOptData = ["FSharp.Core"]
    interface IRawFSharpAssemblyData with

         member _.GetAutoOpenAttributes() = GetAutoOpenAttributes ilModule

         member _.GetInternalsVisibleToAttributes() = GetInternalsVisibleToAttributes ilModule

         member _.TryGetILModuleDef() = Some ilModule

         member _.GetRawFSharpSignatureData(m, ilShortAssemName, filename) =
            let resources = ilModule.Resources.AsList
            let sigDataReaders =
                [ for iresource in resources do
                    if IsSignatureDataResource iresource then
                        let ccuName = GetSignatureDataResourceName iresource
                        yield (ccuName, fun () -> iresource.GetBytes()) ]

            let sigDataReaders =
                if sigDataReaders.IsEmpty && List.contains ilShortAssemName externalSigAndOptData then
                    let sigFileName = Path.ChangeExtension(filename, "sigdata")
                    if not (FileSystem.FileExistsShim sigFileName) then
                        error(Error(FSComp.SR.buildExpectedSigdataFile (FileSystem.GetFullPathShim sigFileName), m))
                    [ (ilShortAssemName, fun () -> FileSystem.OpenFileForReadShim(sigFileName, useMemoryMappedFile=true, shouldShadowCopy=true).AsByteMemory().AsReadOnly())]
                else
                    sigDataReaders
            sigDataReaders

         member _.GetRawFSharpOptimizationData(m, ilShortAssemName, filename) =
            let optDataReaders =
                ilModule.Resources.AsList
                |> List.choose (fun r -> if IsOptimizationDataResource r then Some(GetOptimizationDataResourceName r, (fun () -> r.GetBytes())) else None)

            // Look for optimization data in a file
            let optDataReaders =
                if optDataReaders.IsEmpty && List.contains ilShortAssemName externalSigAndOptData then
                    let optDataFile = Path.ChangeExtension(filename, "optdata")
                    if not (FileSystem.FileExistsShim optDataFile) then
                        error(Error(FSComp.SR.buildExpectedFileAlongSideFSharpCore(optDataFile, FileSystem.GetFullPathShim optDataFile), m))
                    [ (ilShortAssemName, (fun () -> FileSystem.OpenFileForReadShim(optDataFile, useMemoryMappedFile=true, shouldShadowCopy=true).AsByteMemory().AsReadOnly()))]
                else
                    optDataReaders
            optDataReaders

         member _.GetRawTypeForwarders() =
            match ilModule.Manifest with
            | Some manifest -> manifest.ExportedTypes
            | None -> mkILExportedTypes []

         member _.ShortAssemblyName = GetNameOfILModule ilModule

         member _.ILScopeRef = MakeScopeRefForILModule ilModule

         member _.ILAssemblyRefs = ilAssemblyRefs

         member _.HasAnyFSharpSignatureDataAttribute =
            let attrs = GetCustomAttributesOfILModule ilModule
            List.exists IsSignatureDataVersionAttr attrs

         member _.HasMatchingFSharpSignatureDataAttribute =
            let attrs = GetCustomAttributesOfILModule ilModule
            List.exists (IsMatchingSignatureDataVersionAttr (parseILVersion Internal.Utilities.FSharpEnvironment.FSharpBinaryMetadataFormatRevision)) attrs

[<Sealed>]
type RawFSharpAssemblyData (ilModule: ILModuleDef, ilAssemblyRefs) =

    interface IRawFSharpAssemblyData with

         member _.GetAutoOpenAttributes() = GetAutoOpenAttributes ilModule

         member _.GetInternalsVisibleToAttributes() = GetInternalsVisibleToAttributes ilModule

         member _.TryGetILModuleDef() = Some ilModule

         member _.GetRawFSharpSignatureData(_, _, _) =
            let resources = ilModule.Resources.AsList
            [ for iresource in resources do
                if IsSignatureDataResource iresource then
                    let ccuName = GetSignatureDataResourceName iresource
                    yield (ccuName, fun () -> iresource.GetBytes()) ]

         member _.GetRawFSharpOptimizationData(_, _, _) =
            ilModule.Resources.AsList
            |> List.choose (fun r -> if IsOptimizationDataResource r then Some(GetOptimizationDataResourceName r, (fun () -> r.GetBytes())) else None)

         member _.GetRawTypeForwarders() =
            match ilModule.Manifest with
            | Some manifest -> manifest.ExportedTypes
            | None -> mkILExportedTypes []

         member _.ShortAssemblyName = GetNameOfILModule ilModule

         member _.ILScopeRef = MakeScopeRefForILModule ilModule

         member _.ILAssemblyRefs = ilAssemblyRefs

         member _.HasAnyFSharpSignatureDataAttribute =
            let attrs = GetCustomAttributesOfILModule ilModule
            List.exists IsSignatureDataVersionAttr attrs

         member _.HasMatchingFSharpSignatureDataAttribute =
            let attrs = GetCustomAttributesOfILModule ilModule
            List.exists (IsMatchingSignatureDataVersionAttr (parseILVersion Internal.Utilities.FSharpEnvironment.FSharpBinaryMetadataFormatRevision)) attrs

//----------------------------------------------------------------------------
// TcImports
//--------------------------------------------------------------------------

[<Sealed>]
type TcImportsSafeDisposal(tciLock: TcImportsLock, disposeActions: ResizeArray<unit -> unit>,disposeTypeProviderActions: ResizeArray<unit -> unit>) =

    let mutable isDisposed = false

    let dispose () =
      tciLock.AcquireLock (fun tcitok -> 

        RequireTcImportsLock (tcitok, isDisposed)
        RequireTcImportsLock (tcitok, disposeTypeProviderActions)
        RequireTcImportsLock (tcitok, disposeActions)

        // disposing deliberately only closes this tcImports, not the ones up the chain
        isDisposed <- true
        if verbose then
            dprintf "disposing of TcImports, %d binaries\n" disposeActions.Count
        
        let actions1 = disposeTypeProviderActions |> Seq.toArray
        let actions2 = disposeActions |> Seq.toArray

        disposeTypeProviderActions.Clear()
        disposeActions.Clear()

        for action in actions1 do action()
        for action in actions2 do action()
      )

    override _.Finalize() =
        dispose ()

    interface IDisposable with

        member this.Dispose()  =
            if not isDisposed then
                GC.SuppressFinalize this
                dispose ()

#if !NO_EXTENSIONTYPING
// These are hacks in order to allow TcImports to be held as a weak reference inside a type provider.
// The reason is due to older type providers compiled using an older TypeProviderSDK, that SDK used reflection on fields and properties to determine the contract.
// The reflection code has now since been removed, see here: https://github.com/fsprojects/FSharp.TypeProviders.SDK/pull/305. But we still need to work on older type providers.
// One day we can remove these hacks when we deemed most if not all type providers were re-compiled using the newer TypeProviderSDK.
// Yuck.
type TcImportsDllInfoHack =
    {
        FileName: string
    }

and TcImportsWeakHack (tciLock: TcImportsLock, tcImports: WeakReference<TcImports>) =
    let mutable dllInfos: TcImportsDllInfoHack list = []

    member _.SetDllInfos (value: ImportedBinary list) =
      tciLock.AcquireLock <| fun tcitok ->
        RequireTcImportsLock(tcitok, dllInfos)
        dllInfos <- value |> List.map (fun x -> { FileName = x.FileName })

    member _.Base: TcImportsWeakHack option =
        match tcImports.TryGetTarget() with
        | true, strong ->
            match strong.Base with
            | Some (baseTcImports: TcImports) ->
                Some baseTcImports.Weak
            | _ ->
                None
        | _ ->
            None

    member _.SystemRuntimeContainsType typeName =
        match tcImports.TryGetTarget () with
        | true, strong -> strong.SystemRuntimeContainsType typeName
        | _ -> false
#endif
/// Represents a table of imported assemblies with their resolutions.
/// Is a disposable object, but it is recommended not to explicitly call Dispose unless you absolutely know nothing will be using its contents after the disposal.
/// Otherwise, simply allow the GC to collect this and it will properly call Dispose from the finalizer.
and [<Sealed>] TcImports(tcConfigP: TcConfigProvider, initialResolutions: TcAssemblyResolutions, importsBase: TcImports option, dependencyProviderOpt: DependencyProvider option)
#if !NO_EXTENSIONTYPING
                         as this
#endif
       =

    let tciLock = TcImportsLock()

    //---- Start protected by tciLock -------
    let mutable resolutions = initialResolutions
    let mutable dllInfos: ImportedBinary list = []
    let mutable dllTable: NameMap<ImportedBinary> = NameMap.empty
    let mutable ccuInfos: ImportedAssembly list = []
    let mutable ccuTable: NameMap<ImportedAssembly> = NameMap.empty
    let mutable ccuThunks = ResizeArray<CcuThunk * (unit -> unit)>()
    let disposeActions = ResizeArray()
    let disposeTypeProviderActions = ResizeArray()

#if !NO_EXTENSIONTYPING
    let mutable generatedTypeRoots = new Dictionary<ILTypeRef, int * ProviderGeneratedType>()
    let tcImportsWeak = TcImportsWeakHack (tciLock, WeakReference<_> this)
#endif

    let disposal = new TcImportsSafeDisposal(tciLock, disposeActions, disposeTypeProviderActions)
    //---- End protected by tciLock -------

    let mutable disposed = false // this doesn't need locking, it's only for debugging
    let mutable tcGlobals = None // this doesn't need locking, it's set during construction of the TcImports

    let CheckDisposed() =
        if disposed then assert false

    let dispose () =
        CheckDisposed()
        (disposal :> IDisposable).Dispose()

    //   Get a snapshot of the current unFixedUp ccuThunks.
    //   for each of those thunks, remove them from the dictionary, so any parallel threads can't do this work
    //      If it successfully removed it from the dictionary then do the fixup
    //          If the thunk remains unresolved add it back to the ccuThunks dictionary for further processing
    //      If not then move on to the next thunk
    let fixupOrphanCcus () =
      tciLock.AcquireLock <| fun tcitok ->
        RequireTcImportsLock(tcitok, ccuThunks)
        let contents = ccuThunks |> Seq.toArray
        let unsuccessful =
            [ for (ccuThunk, func) in contents do
                if ccuThunk.IsUnresolvedReference then
                    func()
                if ccuThunk.IsUnresolvedReference then
                    yield (ccuThunk, func) ]
        ccuThunks <- ResizeArray (unsuccessful)

    let availableToOptionalCcu = function
        | ResolvedCcu ccu -> Some ccu
        | UnresolvedCcu _ -> None

    static let ccuHasType (ccu: CcuThunk) (nsname: string list) (tname: string) =
        let matchNameSpace (entityOpt: Entity option) n =
            match entityOpt with
            | None -> None
            | Some entity ->
                entity.ModuleOrNamespaceType.AllEntitiesByCompiledAndLogicalMangledNames.TryFind n

        match (Some ccu.Contents, nsname) ||> List.fold matchNameSpace with
        | Some ns ->
                match Map.tryFind tname ns.ModuleOrNamespaceType.TypesByMangledName with
                | Some _ -> true
                | None -> false
        | None -> false

    member internal tcImports.Base =
        CheckDisposed()
        importsBase

    member tcImports.CcuTable =
      tciLock.AcquireLock <| fun tcitok ->
        RequireTcImportsLock(tcitok, ccuTable)
        CheckDisposed()
        ccuTable

    member tcImports.DllTable =
      tciLock.AcquireLock <| fun tcitok ->
        RequireTcImportsLock(tcitok, dllTable)
        CheckDisposed()
        dllTable

#if !NO_EXTENSIONTYPING
    member tcImports.Weak =
            CheckDisposed()
            tcImportsWeak
#endif

    member tcImports.RegisterCcu ccuInfo =
      tciLock.AcquireLock <| fun tcitok ->
        CheckDisposed()
        RequireTcImportsLock(tcitok, ccuInfos)
        RequireTcImportsLock(tcitok, ccuTable)
        ccuInfos <- ccuInfos ++ ccuInfo
        // Assembly Ref Resolution: remove this use of ccu.AssemblyName
        ccuTable <- NameMap.add (ccuInfo.FSharpViewOfMetadata.AssemblyName) ccuInfo ccuTable

    member tcImports.RegisterDll dllInfo =
      tciLock.AcquireLock <| fun tcitok ->
        CheckDisposed()
        RequireTcImportsLock(tcitok, dllInfos)
        RequireTcImportsLock(tcitok, dllTable)
        dllInfos <- dllInfos ++ dllInfo
#if !NO_EXTENSIONTYPING
        tcImportsWeak.SetDllInfos dllInfos
#endif
        dllTable <- NameMap.add (getNameOfScopeRef dllInfo.ILScopeRef) dllInfo dllTable

    member tcImports.GetDllInfos() : ImportedBinary list =
      tciLock.AcquireLock <| fun tcitok ->
        CheckDisposed()
        RequireTcImportsLock(tcitok, dllInfos)
        match importsBase with
        | Some importsBase -> importsBase.GetDllInfos() @ dllInfos
        | None -> dllInfos

    member tcImports.AllAssemblyResolutions() =
      tciLock.AcquireLock <| fun tcitok ->
        CheckDisposed()
        RequireTcImportsLock(tcitok, resolutions)
        let ars = resolutions.GetAssemblyResolutions()
        match importsBase with
        | Some importsBase-> importsBase.AllAssemblyResolutions() @ ars
        | None -> ars

    member tcImports.TryFindDllInfo (ctok: CompilationThreadToken, m, assemblyName, lookupOnly) =
        CheckDisposed()
        let rec look (t: TcImports) =
            match NameMap.tryFind assemblyName t.DllTable with
            | Some res -> Some res
            | None ->
                match t.Base with
                | Some t2 -> look t2
                | None -> None
        match look tcImports with
        | Some res -> Some res
        | None ->
            tcImports.ImplicitLoadIfAllowed(ctok, m, assemblyName, lookupOnly)
            look tcImports

    member tcImports.FindDllInfo (ctok, m, assemblyName) =
        match tcImports.TryFindDllInfo (ctok, m, assemblyName, lookupOnly=false) with
        | Some res -> res
        | None -> error(Error(FSComp.SR.buildCouldNotResolveAssembly assemblyName, m))

    member tcImports.GetImportedAssemblies() =
      tciLock.AcquireLock <| fun tcitok ->
        CheckDisposed()
        RequireTcImportsLock(tcitok, ccuInfos)
        match importsBase with
        | Some importsBase -> List.append (importsBase.GetImportedAssemblies()) ccuInfos
        | None -> ccuInfos

    member tcImports.GetCcusExcludingBase() =
      tciLock.AcquireLock <| fun tcitok ->
        CheckDisposed()
        RequireTcImportsLock(tcitok, ccuInfos)
        ccuInfos |> List.map (fun x -> x.FSharpViewOfMetadata)

    member tcImports.GetCcusInDeclOrder() =
        CheckDisposed()
        List.map (fun x -> x.FSharpViewOfMetadata) (tcImports.GetImportedAssemblies())

    // This is the main "assembly reference --> assembly" resolution routine.
    member tcImports.FindCcuInfo (ctok, m, assemblyName, lookupOnly) =
        CheckDisposed()
        let rec look (t: TcImports) =
            match NameMap.tryFind assemblyName t.CcuTable with
            | Some res -> Some res
            | None ->
                 match t.Base with
                 | Some t2 -> look t2
                 | None -> None

        match look tcImports with
        | Some res -> ResolvedImportedAssembly res
        | None ->
            tcImports.ImplicitLoadIfAllowed(ctok, m, assemblyName, lookupOnly)
            match look tcImports with
            | Some res -> ResolvedImportedAssembly res
            | None -> UnresolvedImportedAssembly assemblyName

    member tcImports.FindCcu (ctok, m, assemblyName, lookupOnly) =
        CheckDisposed()
        match tcImports.FindCcuInfo(ctok, m, assemblyName, lookupOnly) with
        | ResolvedImportedAssembly importedAssembly -> ResolvedCcu(importedAssembly.FSharpViewOfMetadata)
        | UnresolvedImportedAssembly assemblyName -> UnresolvedCcu assemblyName

    member tcImports.FindCcuFromAssemblyRef(ctok, m, assemblyRef: ILAssemblyRef) =
        CheckDisposed()
        match tcImports.FindCcuInfo(ctok, m, assemblyRef.Name, lookupOnly=false) with
        | ResolvedImportedAssembly importedAssembly -> ResolvedCcu(importedAssembly.FSharpViewOfMetadata)
        | UnresolvedImportedAssembly _ -> UnresolvedCcu(assemblyRef.QualifiedName)

    member tcImports.TryFindXmlDocumentationInfo(assemblyName: string) =
        CheckDisposed()
        let rec look (t: TcImports) =
            match NameMap.tryFind assemblyName t.CcuTable with
            | Some res -> Some res
            | None ->
                 match t.Base with
                 | Some t2 -> look t2
                 | None -> None

        match look tcImports with
        | Some res -> res.FSharpViewOfMetadata.Deref.XmlDocumentationInfo
        | _ -> None

#if !NO_EXTENSIONTYPING
    member tcImports.GetProvidedAssemblyInfo(ctok, m, assembly: Tainted<ProvidedAssembly>) =
        let anameOpt = assembly.PUntaint((fun assembly -> match assembly with null -> None | a -> Some (a.GetName())), m)
        match anameOpt with
        | None -> false, None
        | Some aname ->
        let ilShortAssemName = aname.Name
        match tcImports.FindCcu (ctok, m, ilShortAssemName, lookupOnly=true) with
        | ResolvedCcu ccu ->
            if ccu.IsProviderGenerated then
                let dllinfo = tcImports.FindDllInfo(ctok, m, ilShortAssemName)
                true, dllinfo.ProviderGeneratedStaticLinkMap
            else
                false, None

        | UnresolvedCcu _ ->
            let g = tcImports.GetTcGlobals()
            let ilScopeRef = ILScopeRef.Assembly (ILAssemblyRef.FromAssemblyName aname)
            let fileName = aname.Name + ".dll"
            let bytes = assembly.PApplyWithProvider((fun (assembly, provider) -> assembly.GetManifestModuleContents provider), m).PUntaint(id, m)
            let tcConfig = tcConfigP.Get ctok
            let ilModule, ilAssemblyRefs =
                let opts: ILReaderOptions =
                    { reduceMemoryUsage = tcConfig.reduceMemoryUsage
                      pdbDirPath = None
                      metadataOnly = MetadataOnlyFlag.Yes
                      tryGetMetadataSnapshot = tcConfig.tryGetMetadataSnapshot }
                let reader = OpenILModuleReaderFromBytes fileName bytes opts
                reader.ILModuleDef, reader.ILAssemblyRefs

            let theActualAssembly = assembly.PUntaint((fun x -> x.Handle), m)
            let dllinfo =
                { RawMetadata= RawFSharpAssemblyDataBackedByFileOnDisk (ilModule, ilAssemblyRefs)
                  FileName=fileName
                  ProviderGeneratedAssembly=Some theActualAssembly
                  IsProviderGenerated=true
                  ProviderGeneratedStaticLinkMap= if g.isInteractive then None else Some (ProvidedAssemblyStaticLinkingMap.CreateNew())
                  ILScopeRef = ilScopeRef
                  ILAssemblyRefs = ilAssemblyRefs }
            tcImports.RegisterDll dllinfo

            let ccuContents = Construct.NewCcuContents ilScopeRef m ilShortAssemName (Construct.NewEmptyModuleOrNamespaceType Namespace)

            let ccuData: CcuData =
              { IsFSharp=false
                UsesFSharp20PlusQuotations=false
                InvalidateEvent=(new Event<_>()).Publish
                IsProviderGenerated = true
                QualifiedName= Some (assembly.PUntaint((fun a -> a.FullName), m))
                Contents = ccuContents
                ILScopeRef = ilScopeRef
                Stamp = newStamp()
                SourceCodeDirectory = ""
                FileName = Some fileName
                MemberSignatureEquality = (fun ty1 ty2 -> typeEquivAux EraseAll g ty1 ty2)
                ImportProvidedType = (fun ty -> Import.ImportProvidedType (tcImports.GetImportMap()) m ty)
                TryGetILModuleDef = (fun () -> Some ilModule)
                TypeForwarders = Map.empty
                XmlDocumentationInfo =
                    match tcConfig.xmlDocInfoLoader with
                    | Some xmlDocInfoLoader -> xmlDocInfoLoader.TryLoad(fileName, ilModule)
                    | _ -> None
              }

            let ccu = CcuThunk.Create(ilShortAssemName, ccuData)
            let ccuinfo =
                { FSharpViewOfMetadata=ccu
                  ILScopeRef = ilScopeRef
                  AssemblyAutoOpenAttributes = []
                  AssemblyInternalsVisibleToAttributes = []
                  IsProviderGenerated = true
                  TypeProviders=[]
                  FSharpOptimizationData = notlazy None }
            tcImports.RegisterCcu ccuinfo
            // Yes, it is generative
            true, dllinfo.ProviderGeneratedStaticLinkMap

    member tcImports.RecordGeneratedTypeRoot root =
      tciLock.AcquireLock <| fun tcitok ->
        // checking if given ProviderGeneratedType was already recorded before (probably for another set of static parameters)
        let (ProviderGeneratedType(_, ilTyRef, _)) = root
        let index =
            RequireTcImportsLock(tcitok, generatedTypeRoots)
            match generatedTypeRoots.TryGetValue ilTyRef with
            | true, (index, _) -> index
            | false, _ -> generatedTypeRoots.Count
        generatedTypeRoots.[ilTyRef] <- (index, root)

    member tcImports.ProviderGeneratedTypeRoots =
      tciLock.AcquireLock <| fun tcitok ->
        RequireTcImportsLock(tcitok, generatedTypeRoots)
        generatedTypeRoots.Values
        |> Seq.sortBy fst
        |> Seq.map snd
        |> Seq.toList
#endif

    member private tcImports.AttachDisposeAction action =
      tciLock.AcquireLock <| fun tcitok ->
        CheckDisposed()
        RequireTcImportsLock(tcitok, disposeActions)
        disposeActions.Add action

#if !NO_EXTENSIONTYPING
    member private tcImports.AttachDisposeTypeProviderAction action =
        CheckDisposed()
        disposeTypeProviderActions.Add action
#endif

    // Note: the returned binary reader is associated with the tcImports, i.e. when the tcImports are closed
    // then the reader is closed.
    member tcImports.OpenILBinaryModule(ctok, filename, m) =
      try
        CheckDisposed()
        let tcConfig = tcConfigP.Get ctok
        let pdbDirPath =
            // We open the pdb file if one exists parallel to the binary we
            // are reading, so that --standalone will preserve debug information.
            if tcConfig.openDebugInformationForLaterStaticLinking then
                let pdbDir = try FileSystem.GetDirectoryNameShim filename with _ -> "."
                let pdbFile = (try FileSystemUtils.chopExtension filename with _ -> filename) + ".pdb"

                if FileSystem.FileExistsShim pdbFile then
                    if verbose then dprintf "reading PDB file %s from directory %s\n" pdbFile pdbDir
                    Some pdbDir
                else
                    None
            else
                None

        let ilILBinaryReader =
            OpenILBinary (filename, tcConfig.reduceMemoryUsage, pdbDirPath, tcConfig.shadowCopyReferences, tcConfig.tryGetMetadataSnapshot)

        tcImports.AttachDisposeAction(fun _ -> (ilILBinaryReader :> IDisposable).Dispose())
        ilILBinaryReader.ILModuleDef, ilILBinaryReader.ILAssemblyRefs
      with e ->
        error(Error(FSComp.SR.buildErrorOpeningBinaryFile(filename, e.Message), m))

    (* auxModTable is used for multi-module assemblies *)
    member tcImports.MkLoaderForMultiModuleILAssemblies ctok m =
        CheckDisposed()
        let auxModTable = HashMultiMap(10, HashIdentity.Structural)
        fun viewedScopeRef ->

            let tcConfig = tcConfigP.Get ctok
            match viewedScopeRef with
            | ILScopeRef.Module modref ->
                let key = modref.Name
                if not (auxModTable.ContainsKey key) then
                    let resolution = tcConfig.ResolveLibWithDirectories (CcuLoadFailureAction.RaiseError, AssemblyReference(m, key, None)) |> Option.get
                    let ilModule, _ = tcImports.OpenILBinaryModule(ctok, resolution.resolvedPath, m)
                    auxModTable.[key] <- ilModule
                auxModTable.[key]

            | _ ->
                error(InternalError("Unexpected ILScopeRef.Local or ILScopeRef.Assembly in exported type table", m))

    member tcImports.IsAlreadyRegistered nm =
        CheckDisposed()
        tcImports.GetDllInfos() |> List.exists (fun dll ->
            match dll.ILScopeRef with
            | ILScopeRef.Assembly a -> a.Name = nm
            | _ -> false)

    member tcImports.DependencyProvider =
        CheckDisposed()
        match dependencyProviderOpt with
        | None ->
            Debug.Assert(false, "this should never be called on FrameworkTcImports")
            new DependencyProvider(null, null)
        | Some dependencyProvider -> dependencyProvider

    member tcImports.GetImportMap() =
        CheckDisposed()
        let loaderInterface =
            { new Import.AssemblyLoader with
                 member x.FindCcuFromAssemblyRef (ctok, m, ilAssemblyRef) =
                     tcImports.FindCcuFromAssemblyRef (ctok, m, ilAssemblyRef)

                 member x.TryFindXmlDocumentationInfo (assemblyName) =
                    tcImports.TryFindXmlDocumentationInfo(assemblyName)

#if !NO_EXTENSIONTYPING
                 member x.GetProvidedAssemblyInfo (ctok, m, assembly) = tcImports.GetProvidedAssemblyInfo (ctok, m, assembly)
                 member x.RecordGeneratedTypeRoot root = tcImports.RecordGeneratedTypeRoot root
#endif
             }
        new Import.ImportMap (tcImports.GetTcGlobals(), loaderInterface)

    // Note the tcGlobals are only available once mscorlib and fslib have been established. For TcImports,
    // they are logically only needed when converting AbsIL data structures into F# data structures, and
    // when converting AbsIL types in particular, since these types are normalized through the tables
    // in the tcGlobals (E.g. normalizing 'System.Int32' to 'int'). On the whole ImportILAssembly doesn't
    // actually convert AbsIL types - it only converts the outer shell of type definitions - the vast majority of
    // types such as those in method signatures are currently converted on-demand. However ImportILAssembly does have to
    // convert the types that are constraints in generic parameters, which was the original motivation for making sure that
    // ImportILAssembly had a tcGlobals available when it really needs it.
    member tcImports.GetTcGlobals() : TcGlobals =
        CheckDisposed()
        match tcGlobals with
        | Some g -> g
        | None ->
            match importsBase with
            | Some b -> b.GetTcGlobals()
            | None -> failwith "unreachable: GetGlobals - are the references to mscorlib.dll and FSharp.Core.dll valid?"

    member private tcImports.SetTcGlobals g =
        CheckDisposed()
        tcGlobals <- Some g

#if !NO_EXTENSIONTYPING
    member private tcImports.InjectProvidedNamespaceOrTypeIntoEntity
            (typeProviderEnvironment,
             tcConfig: TcConfig,
             m, entity: Entity,
             injectedNamespace, remainingNamespace,
             provider,
             st: Tainted<ProvidedType> option) =
        match remainingNamespace with
        | next :: rest ->
            // Inject the namespace entity
            match entity.ModuleOrNamespaceType.ModulesAndNamespacesByDemangledName.TryFind next with
            | Some childEntity ->
                tcImports.InjectProvidedNamespaceOrTypeIntoEntity (typeProviderEnvironment, tcConfig, m, childEntity, next :: injectedNamespace, rest, provider, st)
            | None ->
                // Build up the artificial namespace if there is not a real one.
                let cpath = CompPath(ILScopeRef.Local, injectedNamespace |> List.rev |> List.map (fun n -> (n, ModuleOrNamespaceKind.Namespace)) )
                let mid = ident (next, rangeStartup)
                let mty = Construct.NewEmptyModuleOrNamespaceType Namespace
                let newNamespace = Construct.NewModuleOrNamespace (Some cpath) taccessPublic mid XmlDoc.Empty [] (MaybeLazy.Strict mty)
                entity.ModuleOrNamespaceType.AddModuleOrNamespaceByMutation newNamespace
                tcImports.InjectProvidedNamespaceOrTypeIntoEntity (typeProviderEnvironment, tcConfig, m, newNamespace, next :: injectedNamespace, rest, provider, st)
        | [] ->
            match st with
            | Some st ->
                // Inject the wrapper type into the provider assembly.
                //
                // Generated types get properly injected into the provided (i.e. generated) assembly CCU in tc.fs

                let importProvidedType t = Import.ImportProvidedType (tcImports.GetImportMap()) m t
                let isSuppressRelocate = tcConfig.isInteractive || st.PUntaint((fun st -> st.IsSuppressRelocate), m)
                let newEntity = Construct.NewProvidedTycon(typeProviderEnvironment, st, importProvidedType, isSuppressRelocate, m)
                entity.ModuleOrNamespaceType.AddProvidedTypeEntity newEntity
            | None -> ()

            entity.entity_tycon_repr <-
                match entity.TypeReprInfo with
                // This is the first extension
                | TNoRepr ->
                    TProvidedNamespaceExtensionPoint(typeProviderEnvironment, [provider])

                // Add to the existing list of extensions
                | TProvidedNamespaceExtensionPoint(resolutionFolder, prior) as repr ->
                    if not(prior |> List.exists(fun r->Tainted.EqTainted r provider)) then
                        TProvidedNamespaceExtensionPoint(resolutionFolder, provider :: prior)
                    else
                        repr

                | _ -> failwith "Unexpected representation in namespace entity referred to by a type provider"

    member tcImportsStrong.ImportTypeProviderExtensions
               (ctok, tcConfig: TcConfig,
                fileNameOfRuntimeAssembly,
                ilScopeRefOfRuntimeAssembly,
                runtimeAssemblyAttributes: ILAttribute list,
                entityToInjectInto, invalidateCcu: Event<_>, m) =

        let startingErrorCount = CompileThreadStatic.ErrorLogger.ErrorCount

        // Find assembly level TypeProviderAssemblyAttributes. These will point to the assemblies that
        // have class which implement ITypeProvider and which have TypeProviderAttribute on them.
        let designTimeAssemblyNames =
            runtimeAssemblyAttributes
            |> List.choose (TryDecodeTypeProviderAssemblyAttr)
            // If no design-time assembly is specified, use the runtime assembly
            |> List.map (function null -> fileNameOfRuntimeAssembly | s -> s)
            // For each simple name of a design-time assembly, we take the first matching one in the order they are
            // specified in the attributes
            |> List.distinctBy (fun s -> try Path.GetFileNameWithoutExtension s with _ -> s)

        if not (List.isEmpty designTimeAssemblyNames) then

            // Find the SystemRuntimeAssemblyVersion value to report in the TypeProviderConfig.
            let primaryAssemblyVersion =
                let primaryAssemblyRef = tcConfig.PrimaryAssemblyDllReference()
                let resolution = tcConfig.ResolveLibWithDirectories (CcuLoadFailureAction.RaiseError, primaryAssemblyRef) |> Option.get
                 // MSDN: this method causes the file to be opened and closed, but the assembly is not added to this domain
                let name = System.Reflection.AssemblyName.GetAssemblyName(resolution.resolvedPath)
                name.Version

            let typeProviderEnvironment =
                 { resolutionFolder = tcConfig.implicitIncludeDir
                   outputFile = tcConfig.outputFile
                   showResolutionMessages = tcConfig.showExtensionTypeMessages
                   referencedAssemblies = Array.distinct [| for r in tcImportsStrong.AllAssemblyResolutions() -> r.resolvedPath |]
                   temporaryFolder = FileSystem.GetTempPathShim() }

            // The type provider should not hold strong references to disposed
            // TcImport objects. So the callbacks provided in the type provider config
            // dispatch via a thunk which gets set to a non-resource-capturing
            // failing function when the object is disposed.
            let systemRuntimeContainsType =
                // NOTE: do not touch this, edit: but we did, we had no choice - TPs cannot hold a strong reference on TcImports "ever".
                let tcImports = tcImportsWeak
                let mutable systemRuntimeContainsTypeRef = fun typeName -> tcImports.SystemRuntimeContainsType typeName
                tcImportsStrong.AttachDisposeTypeProviderAction(fun () -> systemRuntimeContainsTypeRef <- fun _ -> raise (System.ObjectDisposedException("The type provider has been disposed")))
                fun arg -> systemRuntimeContainsTypeRef arg

            let providers = [
                for designTimeAssemblyName in designTimeAssemblyNames do
                    yield! ExtensionTyping.GetTypeProvidersOfAssembly(fileNameOfRuntimeAssembly,
                                                                      ilScopeRefOfRuntimeAssembly,
                                                                      designTimeAssemblyName,
                                                                      typeProviderEnvironment,
                                                                      tcConfig.isInvalidationSupported,
                                                                      tcConfig.isInteractive,
                                                                      systemRuntimeContainsType,
                                                                      primaryAssemblyVersion,
                                                                      tcConfig.compilerToolPaths,
                                                                      m) ]
            // Note, type providers are disposable objects. The TcImports owns the provider objects - when/if it is disposed, the providers are disposed.
            // We ignore all exceptions from provider disposal.
            for provider in providers do
                tcImportsStrong.AttachDisposeTypeProviderAction(fun () ->
                    try
                        provider.PUntaintNoFailure(fun x -> x.Dispose())
                    with e ->
                        ())

            // Add the invalidation signal handlers to each provider
            for provider in providers do
                provider.PUntaint((fun tp ->

                    // Register the type provider invalidation handler.
                    //
                    // We are explicit about what the handler closure captures to help reason about the
                    // lifetime of captured objects, especially in case the type provider instance gets leaked
                    // or keeps itself alive mistakenly, e.g. via some global state in the type provider instance.
                    //
                    // The closure captures
                    //   1. an Event value, ultimately this is made available in all CCus as ccu.InvalidateEvent
                    //   2. any handlers registered to ccu.InvalidateEvent
                    //   3. a message string
                    //
                    // Note that the invalidation handler does not explicitly capture the TcImports.
                    // The only place where handlers are registered is to ccu.InvalidateEvent is in IncrementalBuilder.fs.

                    let capturedInvalidateCcu = invalidateCcu
                    let capturedMessage = "The provider '" + fileNameOfRuntimeAssembly + "' reported a change"
                    let handler = tp.Invalidate.Subscribe(fun _ -> capturedInvalidateCcu.Trigger (capturedMessage))

                    // When the TcImports is disposed we detach the invalidation callback
                    tcImportsStrong.AttachDisposeTypeProviderAction(fun () -> try handler.Dispose() with _ -> ())), m)

            match providers with
            | [] ->
                warning(Error(FSComp.SR.etHostingAssemblyFoundWithoutHosts(fileNameOfRuntimeAssembly, typeof<Microsoft.FSharp.Core.CompilerServices.TypeProviderAssemblyAttribute>.FullName), m))
            | _ ->

#if DEBUG
                if typeProviderEnvironment.showResolutionMessages then
                    dprintfn "Found extension type hosting hosting assembly '%s' with the following extensions:" fileNameOfRuntimeAssembly
                    providers |> List.iter(fun provider ->dprintfn " %s" (ExtensionTyping.DisplayNameOfTypeProvider(provider.TypeProvider, m)))
#endif

                for provider in providers do
                    try
                        // Inject an entity for the namespace, or if one already exists, then record this as a provider
                        // for that namespace.
                        let rec loop (providedNamespace: Tainted<IProvidedNamespace>) =
                            let path = ExtensionTyping.GetProvidedNamespaceAsPath(m, provider, providedNamespace.PUntaint((fun r -> r.NamespaceName), m))
                            tcImportsStrong.InjectProvidedNamespaceOrTypeIntoEntity (typeProviderEnvironment, tcConfig, m, entityToInjectInto, [], path, provider, None)

                            // Inject entities for the types returned by provider.GetTypes().
                            //
                            // NOTE: The types provided by GetTypes() are available for name resolution
                            // when the namespace is "opened". This is part of the specification of the language
                            // feature.
                            let tys = providedNamespace.PApplyArray((fun provider -> provider.GetTypes()), "GetTypes", m)
                            let ptys = [| for ty in tys -> ty.PApply((fun ty -> ty |> ProvidedType.CreateNoContext), m) |]
                            for st in ptys do
                                tcImportsStrong.InjectProvidedNamespaceOrTypeIntoEntity (typeProviderEnvironment, tcConfig, m, entityToInjectInto, [], path, provider, Some st)

                            for providedNestedNamespace in providedNamespace.PApplyArray((fun provider -> provider.GetNestedNamespaces()), "GetNestedNamespaces", m) do
                                loop providedNestedNamespace

                        RequireCompilationThread ctok // IProvidedType.GetNamespaces is an example of a type provider call
                        let providedNamespaces = provider.PApplyArray((fun r -> r.GetNamespaces()), "GetNamespaces", m)

                        for providedNamespace in providedNamespaces do
                            loop providedNamespace
                    with e ->
                        errorRecovery e m

                if startingErrorCount<CompileThreadStatic.ErrorLogger.ErrorCount then
                    error(Error(FSComp.SR.etOneOrMoreErrorsSeenDuringExtensionTypeSetting(), m))

            providers
        else []
#endif

    /// Query information about types available in target system runtime library
    member tcImports.SystemRuntimeContainsType (typeName: string) : bool =
        let ns, typeName = splitILTypeName typeName
        let tcGlobals = tcImports.GetTcGlobals()
        tcGlobals.TryFindSysTyconRef ns typeName |> Option.isSome

    // Add a referenced assembly
    //
    // Retargetable assembly refs are required for binaries that must run
    // against DLLs supported by multiple publishers. For example
    // Compact Framework binaries must use this. However it is not
    // clear when else it is required, e.g. for Mono.

    member tcImports.PrepareToImportReferencedILAssembly (ctok, m, filename, dllinfo: ImportedBinary) =
        CheckDisposed()
        let tcConfig = tcConfigP.Get ctok
        assert dllinfo.RawMetadata.TryGetILModuleDef().IsSome
        let ilModule = dllinfo.RawMetadata.TryGetILModuleDef().Value
        let ilScopeRef = dllinfo.ILScopeRef
        let aref =
            match ilScopeRef with
            | ILScopeRef.Assembly aref -> aref
            | _ -> error(InternalError("PrepareToImportReferencedILAssembly: cannot reference .NET netmodules directly, reference the containing assembly instead", m))

        let nm = aref.Name
        if verbose then dprintn ("Converting IL assembly to F# data structures "+nm)
        let auxModuleLoader = tcImports.MkLoaderForMultiModuleILAssemblies ctok m
        let invalidateCcu = new Event<_>()
        let ccu = Import.ImportILAssembly(tcImports.GetImportMap, m, auxModuleLoader, tcConfig.xmlDocInfoLoader, ilScopeRef, tcConfig.implicitIncludeDir, Some filename, ilModule, invalidateCcu.Publish)

        let ccuinfo =
            { FSharpViewOfMetadata=ccu
              ILScopeRef = ilScopeRef
              AssemblyAutoOpenAttributes = GetAutoOpenAttributes ilModule
              AssemblyInternalsVisibleToAttributes = GetInternalsVisibleToAttributes ilModule
#if !NO_EXTENSIONTYPING
              IsProviderGenerated = false
              TypeProviders = []
#endif
              FSharpOptimizationData = notlazy None }
        tcImports.RegisterCcu ccuinfo

        let phase2 () =
#if !NO_EXTENSIONTYPING
            ccuinfo.TypeProviders <- tcImports.ImportTypeProviderExtensions (ctok, tcConfig, filename, ilScopeRef, ilModule.ManifestOfAssembly.CustomAttrs.AsList, ccu.Contents, invalidateCcu, m)
#endif
            [ResolvedImportedAssembly ccuinfo]
        phase2

    member tcImports.PrepareToImportReferencedFSharpAssembly (ctok, m, filename, dllinfo: ImportedBinary) =
        CheckDisposed()
#if !NO_EXTENSIONTYPING
        let tcConfig = tcConfigP.Get ctok
#endif
        let ilModule = dllinfo.RawMetadata
        let ilScopeRef = dllinfo.ILScopeRef
        let ilShortAssemName = getNameOfScopeRef ilScopeRef
        if verbose then dprintn ("Converting F# assembly to F# data structures "+(getNameOfScopeRef ilScopeRef))
        if verbose then dprintn ("Relinking interface info from F# assembly "+ilShortAssemName)
        let optDataReaders = ilModule.GetRawFSharpOptimizationData(m, ilShortAssemName, filename)

        let ccuRawDataAndInfos =
            ilModule.GetRawFSharpSignatureData(m, ilShortAssemName, filename)
            |> List.map (fun (ccuName, sigDataReader) ->
                let data = GetSignatureData (filename, ilScopeRef, ilModule.TryGetILModuleDef(), sigDataReader)

                let optDatas = Map.ofList optDataReaders

                let minfo: PickledCcuInfo = data.RawData
                let mspec = minfo.mspec

#if !NO_EXTENSIONTYPING
                let invalidateCcu = new Event<_>()
#endif

                let codeDir = minfo.compileTimeWorkingDir
                let ccuData: CcuData =
                    { ILScopeRef=ilScopeRef
                      Stamp = newStamp()
                      FileName = Some filename
                      QualifiedName= Some(ilScopeRef.QualifiedName)
                      SourceCodeDirectory = codeDir (* note: in some cases we fix up this information later *)
                      IsFSharp=true
                      Contents = mspec
#if !NO_EXTENSIONTYPING
                      InvalidateEvent=invalidateCcu.Publish
                      IsProviderGenerated = false
                      ImportProvidedType = (fun ty -> Import.ImportProvidedType (tcImports.GetImportMap()) m ty)
#endif
                      TryGetILModuleDef = ilModule.TryGetILModuleDef
                      UsesFSharp20PlusQuotations = minfo.usesQuotations
                      MemberSignatureEquality= (fun ty1 ty2 -> typeEquivAux EraseAll (tcImports.GetTcGlobals()) ty1 ty2)
                      TypeForwarders = ImportILAssemblyTypeForwarders(tcImports.GetImportMap, m, ilModule.GetRawTypeForwarders())
                      XmlDocumentationInfo =
                        match tcConfig.xmlDocInfoLoader, ilModule.TryGetILModuleDef() with
                        | Some xmlDocInfoLoader, Some ilModuleDef -> xmlDocInfoLoader.TryLoad(filename, ilModuleDef)
                        | _ -> None
                    }

                let ccu = CcuThunk.Create(ccuName, ccuData)

                let optdata =
                    lazy
                        (match Map.tryFind ccuName optDatas with
                         | None ->
                            if verbose then dprintf "*** no optimization data for CCU %s, was DLL compiled with --no-optimization-data??\n" ccuName
                            None
                         | Some info ->
                            let data = GetOptimizationData (filename, ilScopeRef, ilModule.TryGetILModuleDef(), info)
                            let fixupThunk () = data.OptionalFixup(fun nm -> availableToOptionalCcu(tcImports.FindCcu(ctok, m, nm, lookupOnly=false)))

                            // Make a note of all ccuThunks that may still need to be fixed up when other dlls are loaded
                            tciLock.AcquireLock (fun tcitok ->
                                RequireTcImportsLock(tcitok, ccuThunks)
                                for ccuThunk in data.FixupThunks do
                                    if ccuThunk.IsUnresolvedReference then
                                        ccuThunks.Add(ccuThunk, fun () -> fixupThunk () |> ignore) |> ignore
                            )

                            if verbose then dprintf "found optimization data for CCU %s\n" ccuName
                            Some (fixupThunk ()))

                let ccuinfo =
                    { FSharpViewOfMetadata=ccu
                      AssemblyAutoOpenAttributes = ilModule.GetAutoOpenAttributes()
                      AssemblyInternalsVisibleToAttributes = ilModule.GetInternalsVisibleToAttributes()
                      FSharpOptimizationData=optdata
#if !NO_EXTENSIONTYPING
                      IsProviderGenerated = false
                      TypeProviders = []
#endif
                      ILScopeRef = ilScopeRef }

                let phase2() =
#if !NO_EXTENSIONTYPING
                     match ilModule.TryGetILModuleDef() with
                     | None -> () // no type providers can be used without a real IL Module present
                     | Some ilModule ->
                         let tps = tcImports.ImportTypeProviderExtensions (ctok, tcConfig, filename, ilScopeRef, ilModule.ManifestOfAssembly.CustomAttrs.AsList, ccu.Contents, invalidateCcu, m)
                         ccuinfo.TypeProviders <- tps
#else
                     ()
#endif
                data, ccuinfo, phase2)

        // Register all before relinking to cope with mutually-referential ccus
        ccuRawDataAndInfos |> List.iter (p23 >> tcImports.RegisterCcu)
        let phase2 () =
            (* Relink *)
            (* dprintf "Phase2: %s\n" filename; REMOVE DIAGNOSTICS *)
            ccuRawDataAndInfos
            |> List.iter (fun (data, _, _) ->
                let fixupThunk () = data.OptionalFixup(fun nm -> availableToOptionalCcu(tcImports.FindCcu(ctok, m, nm, lookupOnly=false))) |> ignore
                fixupThunk()
                for ccuThunk in data.FixupThunks do
                    if ccuThunk.IsUnresolvedReference then
                      tciLock.AcquireLock <| fun tcitok ->
                        RequireTcImportsLock(tcitok, ccuThunks)
                        ccuThunks.Add(ccuThunk, fixupThunk) |> ignore
                )
#if !NO_EXTENSIONTYPING
            ccuRawDataAndInfos |> List.iter (fun (_, _, phase2) -> phase2())
#endif
            ccuRawDataAndInfos |> List.map p23 |> List.map ResolvedImportedAssembly
        phase2

    // NOTE: When used in the Language Service this can cause the transitive checking of projects. Hence it must be cancellable.
    member tcImports.TryRegisterAndPrepareToImportReferencedDll (ctok, r: AssemblyResolution) : NodeCode<(_ * (unit -> AvailableImportedAssembly list)) option> =
      node {
        CheckDisposed()
        let m = r.originalReference.Range
        let filename = r.resolvedPath
        let! contentsOpt =
          node {
            match r.ProjectReference with
            | Some ilb -> 
                return! ilb.EvaluateRawContents()
            | None -> 
                return None
          }

        // If we have a project reference but did not get any valid contents,
        //     just return None and do not attempt to read elsewhere.
        if contentsOpt.IsNone && r.ProjectReference.IsSome then
            return None
        else

        let assemblyData =
            match contentsOpt with
            | Some ilb -> ilb
            | None ->
                let ilModule, ilAssemblyRefs = tcImports.OpenILBinaryModule(ctok, filename, m)
                RawFSharpAssemblyDataBackedByFileOnDisk (ilModule, ilAssemblyRefs) :> IRawFSharpAssemblyData

        let ilShortAssemName = assemblyData.ShortAssemblyName
        let ilScopeRef = assemblyData.ILScopeRef

        if tcImports.IsAlreadyRegistered ilShortAssemName then
            let dllinfo = tcImports.FindDllInfo(ctok, m, ilShortAssemName)
            let phase2() = [tcImports.FindCcuInfo(ctok, m, ilShortAssemName, lookupOnly=true)]
            return Some(dllinfo, phase2)
        else
            let dllinfo =
                { RawMetadata=assemblyData
                  FileName=filename
#if !NO_EXTENSIONTYPING
                  ProviderGeneratedAssembly=None
                  IsProviderGenerated=false
                  ProviderGeneratedStaticLinkMap = None
#endif
                  ILScopeRef = ilScopeRef
                  ILAssemblyRefs = assemblyData.ILAssemblyRefs }
            tcImports.RegisterDll dllinfo
            let phase2 =
                if assemblyData.HasAnyFSharpSignatureDataAttribute then
                    if not assemblyData.HasMatchingFSharpSignatureDataAttribute then
                        errorR(Error(FSComp.SR.buildDifferentVersionMustRecompile filename, m))
                        tcImports.PrepareToImportReferencedILAssembly (ctok, m, filename, dllinfo)
                    else
                        try
                        tcImports.PrepareToImportReferencedFSharpAssembly (ctok, m, filename, dllinfo)
                        with e -> error(Error(FSComp.SR.buildErrorOpeningBinaryFile(filename, e.Message), m))
                else
                    tcImports.PrepareToImportReferencedILAssembly (ctok, m, filename, dllinfo)
            return Some(dllinfo, phase2)
         }

    // NOTE: When used in the Language Service this can cause the transitive checking of projects. Hence it must be cancellable.
    member tcImports.RegisterAndImportReferencedAssemblies (ctok, nms: AssemblyResolution list) =
      node {
        CheckDisposed()

        let! results =
            nms
            |> List.map (fun nm -> 
                node {
                    try
                         return! tcImports.TryRegisterAndPrepareToImportReferencedDll (ctok, nm)
                    with e ->
                         errorR(Error(FSComp.SR.buildProblemReadingAssembly(nm.resolvedPath, e.Message), nm.originalReference.Range))
                         return None
                }
            )
            |> NodeCode.Sequential

        let dllinfos, phase2s = results |> Array.choose id |> List.ofArray |> List.unzip
        fixupOrphanCcus()
        let ccuinfos = (List.collect (fun phase2 -> phase2()) phase2s)
        return dllinfos, ccuinfos
      }

    /// Note that implicit loading is not used for compilations from MSBuild, which passes ``--noframework``
    /// Implicit loading is done in non-cancellation mode. Implicit loading is never used in the language service, so
    /// no cancellation is needed.
    member tcImports.ImplicitLoadIfAllowed (ctok, m, assemblyName, lookupOnly) =
        CheckDisposed()
        // If the user is asking for the default framework then also try to resolve other implicit assemblies as they are discovered.
        // Using this flag to mean 'allow implicit discover of assemblies'.
        let tcConfig = tcConfigP.Get ctok
        if not lookupOnly && tcConfig.implicitlyResolveAssemblies then
            let tryFile speculativeFileName =
                let foundFile = tcImports.TryResolveAssemblyReference (ctok, AssemblyReference (m, speculativeFileName, None), ResolveAssemblyReferenceMode.Speculative)
                match foundFile with
                | OkResult (warns, res) ->
                    ReportWarnings warns
                    tcImports.RegisterAndImportReferencedAssemblies(ctok, res) 
                    |> NodeCode.RunImmediateWithoutCancellation 
                    |> ignore
                    true
                | ErrorResult (_warns, _err) ->
                    // Throw away warnings and errors - this is speculative loading
                    false

            if tryFile (assemblyName + ".dll") then ()
            else tryFile (assemblyName + ".exe") |> ignore

#if !NO_EXTENSIONTYPING
    member tcImports.TryFindProviderGeneratedAssemblyByName(ctok, assemblyName: string) : System.Reflection.Assembly option =
        // The assembly may not be in the resolutions, but may be in the load set including EST injected assemblies
        match tcImports.TryFindDllInfo (ctok, range0, assemblyName, lookupOnly=true) with
        | Some res ->
            // Provider-generated assemblies don't necessarily have an on-disk representation we can load.
            res.ProviderGeneratedAssembly
        | _ -> None
#endif

    /// Only used by F# Interactive
    member tcImports.TryFindExistingFullyQualifiedPathBySimpleAssemblyName (simpleAssemName) : string option =
      tciLock.AcquireLock <| fun tcitok ->
        RequireTcImportsLock(tcitok, resolutions)
        resolutions.TryFindBySimpleAssemblyName (simpleAssemName) |> Option.map (fun r -> r.resolvedPath)

    /// Only used by F# Interactive
    member tcImports.TryFindExistingFullyQualifiedPathByExactAssemblyRef(assemblyRef: ILAssemblyRef) : string option =
      tciLock.AcquireLock <| fun tcitok ->
        RequireTcImportsLock(tcitok, resolutions)
        resolutions.TryFindByExactILAssemblyRef (assemblyRef) |> Option.map (fun r -> r.resolvedPath)

    member tcImports.TryResolveAssemblyReference(ctok, assemblyReference: AssemblyReference, mode: ResolveAssemblyReferenceMode) : OperationResult<AssemblyResolution list> =
      tciLock.AcquireLock <| fun tcitok ->
        let tcConfig = tcConfigP.Get ctok

        RequireTcImportsLock(tcitok, resolutions)
        // First try to lookup via the original reference text.
        match resolutions.TryFindByOriginalReference assemblyReference with
        | Some assemblyResolution ->
            ResultD [assemblyResolution]
        | None ->
#if NO_MSBUILD_REFERENCE_RESOLUTION
           try
               ResultD [tcConfig.ResolveLibWithDirectories assemblyReference]
           with e ->
               ErrorD e
#else
            // Next try to lookup up by the exact full resolved path.
            match resolutions.TryFindByResolvedPath assemblyReference.Text with
            | Some assemblyResolution ->
                ResultD [assemblyResolution]
            | None ->
                if tcConfigP.Get(ctok).useSimpleResolution then
                    let action =
                        match mode with
                        | ResolveAssemblyReferenceMode.ReportErrors -> CcuLoadFailureAction.RaiseError
                        | ResolveAssemblyReferenceMode.Speculative -> CcuLoadFailureAction.ReturnNone
                    match tcConfig.ResolveLibWithDirectories (action, assemblyReference) with
                    | Some resolved ->
                        resolutions <- resolutions.AddResolutionResults [resolved]
                        ResultD [resolved]
                    | None ->
                        ErrorD(AssemblyNotResolved(assemblyReference.Text, assemblyReference.Range))
                else
                    // This is a previously unencountered assembly. Resolve it and add it to the list.
                    // But don't cache resolution failures because the assembly may appear on the disk later.
                    let resolved, unresolved = TcConfig.TryResolveLibsUsingMSBuildRules(tcConfig, [ assemblyReference ], assemblyReference.Range, mode)
                    match resolved, unresolved with
                    | (assemblyResolution :: _, _) ->
                        resolutions <- resolutions.AddResolutionResults resolved
                        ResultD [assemblyResolution]
                    | (_, _ :: _) ->
                        resolutions <- resolutions.AddUnresolvedReferences unresolved
                        ErrorD(AssemblyNotResolved(assemblyReference.Text, assemblyReference.Range))
                    | [], [] ->
                        // Note, if mode=ResolveAssemblyReferenceMode.Speculative and the resolution failed then TryResolveLibsUsingMSBuildRules returns
                        // the empty list and we convert the failure into an AssemblyNotResolved here.
                        ErrorD(AssemblyNotResolved(assemblyReference.Text, assemblyReference.Range))
#endif

    member tcImports.ResolveAssemblyReference(ctok, assemblyReference, mode) : AssemblyResolution list =
        CommitOperationResult(tcImports.TryResolveAssemblyReference(ctok, assemblyReference, mode))

    // Note: This returns a TcImports object. However, framework TcImports are not currently disposed. The only reason
    // we dispose TcImports is because we need to dispose type providers, and type providers are never included in the framework DLL set.
    // If a framework set ever includes type providers, you will not have to worry about explicitly calling Dispose as the Finalizer will handle it.
    static member BuildFrameworkTcImports (tcConfigP: TcConfigProvider, frameworkDLLs, nonFrameworkDLLs) =
      node {
        let ctok = CompilationThreadToken()
        let tcConfig = tcConfigP.Get ctok
        let tcResolutions = TcAssemblyResolutions.BuildFromPriorResolutions(tcConfig, frameworkDLLs, [])
        let tcAltResolutions = TcAssemblyResolutions.BuildFromPriorResolutions(tcConfig, nonFrameworkDLLs, [])

        let frameworkTcImports = new TcImports(tcConfigP, tcResolutions, None, None)

        // Fetch the primaryAssembly from the referenced assemblies otherwise
        let primaryAssemblyReference =
            let path = frameworkDLLs |> List.tryFind(fun dll -> String.Compare(Path.GetFileNameWithoutExtension(dll.resolvedPath), tcConfig.primaryAssembly.Name, StringComparison.OrdinalIgnoreCase) = 0)
            match path with
            | Some p -> AssemblyReference(range0, p.resolvedPath, None)
            | None -> tcConfig.PrimaryAssemblyDllReference()

        let primaryAssemblyResolution = frameworkTcImports.ResolveAssemblyReference(ctok, primaryAssemblyReference, ResolveAssemblyReferenceMode.ReportErrors)
        let! primaryAssem = frameworkTcImports.RegisterAndImportReferencedAssemblies(ctok, primaryAssemblyResolution)
        let primaryScopeRef =
            match primaryAssem with
              | (_, [ResolvedImportedAssembly ccu]) -> ccu.FSharpViewOfMetadata.ILScopeRef
              | _ -> failwith "unexpected"

        let primaryAssemblyResolvedPath =
            match primaryAssemblyResolution with
            | [primaryAssemblyResolution] -> primaryAssemblyResolution.resolvedPath
            | _ -> failwith "unexpected"

        let resolvedAssemblies = tcResolutions.GetAssemblyResolutions()

        let readerSettings: ILReaderOptions =
            { pdbDirPath=None
              reduceMemoryUsage = tcConfig.reduceMemoryUsage
              metadataOnly = MetadataOnlyFlag.Yes
              tryGetMetadataSnapshot = tcConfig.tryGetMetadataSnapshot }

        let tryFindAssemblyByExportedType manifest (exportedType: ILExportedTypeOrForwarder) =
            match exportedType.ScopeRef, primaryScopeRef with
            | ILScopeRef.Assembly aref1, ILScopeRef.Assembly aref2 when aref1.EqualsIgnoringVersion aref2 ->
                mkRefToILAssembly manifest
                |> Some
            | _ ->
                None

        let tryFindAssemblyThatForwardsToPrimaryAssembly manifest =
            manifest.ExportedTypes.TryFindByName "System.Object"
            |> Option.bind (tryFindAssemblyByExportedType manifest)

        // Determine what other assemblies could have been the primary assembly
        // by checking to see if "System.Object" is an exported type.
        let assembliesThatForwardToPrimaryAssembly =
            resolvedAssemblies
            |> List.choose (fun resolvedAssembly ->
                if primaryAssemblyResolvedPath <> resolvedAssembly.resolvedPath then
                    let reader = OpenILModuleReader resolvedAssembly.resolvedPath readerSettings
                    reader.ILModuleDef.Manifest
                    |> Option.bind tryFindAssemblyThatForwardsToPrimaryAssembly
                else
                    None)

        let! fslibCcu, fsharpCoreAssemblyScopeRef =
            node {
                if tcConfig.compilingFslib then
                    // When compiling FSharp.Core.dll, the fslibCcu reference to FSharp.Core.dll is a delayed ccu thunk fixed up during type checking
                    return CcuThunk.CreateDelayed getFSharpCoreLibraryName, ILScopeRef.Local
                else
                    let coreLibraryReference = tcConfig.CoreLibraryDllReference()

                    let resolvedAssemblyRef =
                        match tcResolutions.TryFindByOriginalReference coreLibraryReference with
                        | Some resolution -> Some resolution
                        | _ ->
                            // Are we using a "non-canonical" FSharp.Core?
                            match tcAltResolutions.TryFindByOriginalReference coreLibraryReference with
                            | Some resolution -> Some resolution
                            | _ -> tcResolutions.TryFindByOriginalReferenceText (getFSharpCoreLibraryName)  // was the ".dll" elided?

                    match resolvedAssemblyRef with
                    | Some coreLibraryResolution ->
                        match! frameworkTcImports.RegisterAndImportReferencedAssemblies(ctok, [coreLibraryResolution]) with
                        | (_, [ResolvedImportedAssembly fslibCcuInfo ]) -> return fslibCcuInfo.FSharpViewOfMetadata, fslibCcuInfo.ILScopeRef
                        | _ ->
                            return error(InternalError("BuildFrameworkTcImports: no successful import of "+coreLibraryResolution.resolvedPath, coreLibraryResolution.originalReference.Range))
                    | None ->
                        return error(InternalError(sprintf "BuildFrameworkTcImports: no resolution of '%s'" coreLibraryReference.Text, rangeStartup))   
            }

        // Load the rest of the framework DLLs all at once (they may be mutually recursive)
        let! _assemblies = frameworkTcImports.RegisterAndImportReferencedAssemblies (ctok, resolvedAssemblies)

        // These are the DLLs we can search for well-known types
        let sysCcus =
             [| for ccu in frameworkTcImports.GetCcusInDeclOrder() do
                   yield ccu |]

        let tryFindSysTypeCcu path typeName =
            sysCcus |> Array.tryFind (fun ccu -> ccuHasType ccu path typeName)

        let ilGlobals = mkILGlobals (primaryScopeRef, assembliesThatForwardToPrimaryAssembly, fsharpCoreAssemblyScopeRef)

        // OK, now we have both mscorlib.dll and FSharp.Core.dll we can create TcGlobals
        let tcGlobals = TcGlobals(tcConfig.compilingFslib, ilGlobals, fslibCcu,
                                  tcConfig.implicitIncludeDir, tcConfig.mlCompatibility,
                                  tcConfig.isInteractive, tryFindSysTypeCcu, tcConfig.emitDebugInfoInQuotations,
                                  tcConfig.noDebugData, tcConfig.pathMap, tcConfig.langVersion)

#if DEBUG
        // the global_g reference cell is used only for debug printing
        global_g <- Some tcGlobals
#endif
        frameworkTcImports.SetTcGlobals tcGlobals
        return tcGlobals, frameworkTcImports
      }

    member tcImports.ReportUnresolvedAssemblyReferences knownUnresolved =
        // Report that an assembly was not resolved.
        let reportAssemblyNotResolved(file, originalReferences: AssemblyReference list) =
            originalReferences |> List.iter(fun originalReference -> errorR(AssemblyNotResolved(file, originalReference.Range)))
        knownUnresolved
        |> List.map (function UnresolvedAssemblyReference(file, originalReferences) -> file, originalReferences)
        |> List.iter reportAssemblyNotResolved

    static member BuildNonFrameworkTcImports
       (tcConfigP: TcConfigProvider, baseTcImports,
        nonFrameworkReferences, knownUnresolved, dependencyProvider) =

      node {
        let ctok = CompilationThreadToken()
        let tcConfig = tcConfigP.Get ctok
        let tcResolutions = TcAssemblyResolutions.BuildFromPriorResolutions(tcConfig, nonFrameworkReferences, knownUnresolved)
        let references = tcResolutions.GetAssemblyResolutions()
        let tcImports = new TcImports(tcConfigP, tcResolutions, Some baseTcImports, Some dependencyProvider)
        let! _assemblies = tcImports.RegisterAndImportReferencedAssemblies(ctok, references)
        tcImports.ReportUnresolvedAssemblyReferences knownUnresolved
        return tcImports
      }

    static member BuildTcImports(tcConfigP: TcConfigProvider, dependencyProvider) =
      node {
        let ctok = CompilationThreadToken()
        let tcConfig = tcConfigP.Get ctok
        let frameworkDLLs, nonFrameworkReferences, knownUnresolved = TcAssemblyResolutions.SplitNonFoundationalResolutions(tcConfig)
        let! tcGlobals, frameworkTcImports = TcImports.BuildFrameworkTcImports (tcConfigP, frameworkDLLs, nonFrameworkReferences)
        let! tcImports = TcImports.BuildNonFrameworkTcImports(tcConfigP, frameworkTcImports, nonFrameworkReferences, knownUnresolved, dependencyProvider)
        return tcGlobals, tcImports
      }

    interface System.IDisposable with
        member tcImports.Dispose() =
            dispose ()

    override tcImports.ToString() = "TcImports(...)"

/// Process #r in F# Interactive.
/// Adds the reference to the tcImports and add the ccu to the type checking environment.
let RequireDLL (ctok, tcImports: TcImports, tcEnv, thisAssemblyName, referenceRange, file) =
    let resolutions = CommitOperationResult(tcImports.TryResolveAssemblyReference(ctok, AssemblyReference(referenceRange, file, None), ResolveAssemblyReferenceMode.ReportErrors))
    let dllinfos, ccuinfos = 
        tcImports.RegisterAndImportReferencedAssemblies(ctok, resolutions) 
        |> NodeCode.RunImmediateWithoutCancellation

    let asms =
        ccuinfos |> List.map (function
            | ResolvedImportedAssembly asm -> asm
            | UnresolvedImportedAssembly assemblyName -> error(Error(FSComp.SR.buildCouldNotResolveAssemblyRequiredByFile(assemblyName, file), referenceRange)))

    let g = tcImports.GetTcGlobals()
    let amap = tcImports.GetImportMap()
    let buildTcEnv tcEnv asm =
        AddCcuToTcEnv(g, amap, referenceRange, tcEnv, thisAssemblyName, asm.FSharpViewOfMetadata, asm.AssemblyAutoOpenAttributes, asm.AssemblyInternalsVisibleToAttributes)
    let tcEnv = (tcEnv, asms) ||> List.fold buildTcEnv
    tcEnv, (dllinfos, asms)
