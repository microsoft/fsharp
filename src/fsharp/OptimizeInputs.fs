// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

// # FSComp.SR.opts

module internal FSharp.Compiler.OptimizeInputs

open System.IO
open Internal.Utilities.Library
open FSharp.Compiler
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AbstractIL.Diagnostics
open FSharp.Compiler.CheckDeclarations
open FSharp.Compiler.CompilerConfig
open FSharp.Compiler.CompilerImports
open FSharp.Compiler.CompilerOptions
open FSharp.Compiler.IlxGen
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.IO
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeOps

let mutable showTermFileCount = 0

let PrintWholeAssemblyImplementation g (tcConfig:TcConfig) outfile header expr =
    if tcConfig.showTerms then
        if tcConfig.writeTermsToFiles then
            let filename = outfile + ".terms"
            use f = FileSystem.OpenFileForWriteShim(filename + "-" + string showTermFileCount + "-" + header, FileMode.OpenOrCreate).GetWriter()
            showTermFileCount <- showTermFileCount + 1
            LayoutRender.outL f (Display.squashTo 192 (DebugPrint.implFilesL g expr))
        else
            dprintf "\n------------------\nshowTerm: %s:\n" header
            LayoutRender.outL stderr (Display.squashTo 192 (DebugPrint.implFilesL g expr))
            dprintf "\n------------------\n"

let AddExternalCcuToOptimizationEnv tcGlobals optEnv (ccuinfo: ImportedAssembly) =
    match ccuinfo.FSharpOptimizationData.Force() with
    | None -> optEnv
    | Some data -> Optimizer.BindCcu ccuinfo.FSharpViewOfMetadata data optEnv tcGlobals

let GetInitialOptimizationEnv (tcImports:TcImports, tcGlobals:TcGlobals) =
    let ccuinfos = tcImports.GetImportedAssemblies()
    let optEnv = Optimizer.IncrementalOptimizationEnv.Empty
    let optEnv = List.fold (AddExternalCcuToOptimizationEnv tcGlobals) optEnv ccuinfos
    optEnv

let ApplyAllOptimizations (tcConfig:TcConfig, tcGlobals, tcVal, outfile, importMap, isIncrementalFragment, optEnv, ccu:CcuThunk, implFiles) =
    // NOTE: optEnv - threads through
    //
    // Always optimize once - the results of this step give the x-module optimization
    // info.  Subsequent optimization steps choose representations etc. which we don't
    // want to save in the x-module info (i.e. x-module info is currently "high level").
    PrintWholeAssemblyImplementation tcGlobals tcConfig outfile "pass-start" implFiles
#if DEBUG
    if tcConfig.showOptimizationData then
        dprintf "Expression prior to optimization:\n%s\n" (LayoutRender.showL (Display.squashTo 192 (DebugPrint.implFilesL tcGlobals implFiles)))

    if tcConfig.showOptimizationData then
        dprintf "CCU prior to optimization:\n%s\n" (LayoutRender.showL (Display.squashTo 192 (DebugPrint.entityL tcGlobals ccu.Contents)))
#endif

    let optEnv0 = optEnv
    ReportTime tcConfig ("Optimizations")

    // Only do abstract_big_targets on the first pass!  Only do it when TLR is on!
    let optSettings = tcConfig.optSettings
    let optSettings = { optSettings with abstractBigTargets = tcConfig.doTLR }
    let optSettings = { optSettings with reportingPhase = true }

    let results, (optEnvFirstLoop, _, _, _) =
        ((optEnv0, optEnv0, optEnv0, SignatureHidingInfo.Empty), implFiles)

        ||> List.mapFold (fun (optEnvFirstLoop, optEnvExtraLoop, optEnvFinalSimplify, hidden) implFile ->

            //ReportTime tcConfig ("Initial simplify")
            let (optEnvFirstLoop, implFile, implFileOptData, hidden), optimizeDuringCodeGen =
                Optimizer.OptimizeImplFile
                   (optSettings, ccu, tcGlobals, tcVal, importMap,
                    optEnvFirstLoop, isIncrementalFragment,
                    tcConfig.emitTailcalls, hidden, implFile)

            let implFile = AutoBox.TransformImplFile tcGlobals importMap implFile

            // Only do this on the first pass!
            let optSettings = { optSettings with abstractBigTargets = false; reportingPhase = false }
#if DEBUG
            if tcConfig.showOptimizationData then
                dprintf "Optimization implFileOptData:\n%s\n" (LayoutRender.showL (Display.squashTo 192 (Optimizer.moduleInfoL tcGlobals implFileOptData)))
#endif

            let implFile, optEnvExtraLoop =
                if tcConfig.extraOptimizationIterations > 0 then

                    //ReportTime tcConfig ("Extra simplification loop")
                    let (optEnvExtraLoop, implFile, _, _), _ =
                        Optimizer.OptimizeImplFile
                           (optSettings, ccu, tcGlobals, tcVal, importMap,
                            optEnvExtraLoop, isIncrementalFragment,
                            tcConfig.emitTailcalls, hidden, implFile)

                    //PrintWholeAssemblyImplementation tcConfig outfile (sprintf "extra-loop-%d" n) implFile
                    implFile, optEnvExtraLoop
                else
                    implFile, optEnvExtraLoop

            let implFile =
                if tcConfig.doDetuple then
                    //ReportTime tcConfig ("Detupled optimization")
                    let implFile = implFile |> Detuple.DetupleImplFile ccu tcGlobals
                    //PrintWholeAssemblyImplementation tcConfig outfile "post-detuple" implFile
                    implFile
                else implFile

            let implFile =
                if tcConfig.doTLR then
                    implFile |> InnerLambdasToTopLevelFuncs.MakeTLRDecisions ccu tcGlobals
                else implFile

            let implFile =
                LowerCallsAndSeqs.LowerImplFile tcGlobals implFile

            let implFile, optEnvFinalSimplify =
                if tcConfig.doFinalSimplify then

                    //ReportTime tcConfig ("Final simplify pass")
                    let (optEnvFinalSimplify, implFile, _, _), _ =
                        Optimizer.OptimizeImplFile
                           (optSettings, ccu, tcGlobals, tcVal, importMap, optEnvFinalSimplify,
                            isIncrementalFragment, tcConfig.emitTailcalls, hidden, implFile)

                    //PrintWholeAssemblyImplementation tcConfig outfile "post-rec-opt" implFile
                    implFile, optEnvFinalSimplify
                else
                    implFile, optEnvFinalSimplify

            let implFile =
                { ImplFile = implFile
                  OptimizeDuringCodeGen = optimizeDuringCodeGen }

            (implFile, implFileOptData), (optEnvFirstLoop, optEnvExtraLoop, optEnvFinalSimplify, hidden))

    let implFiles, implFileOptDatas = List.unzip results
    let assemblyOptData = Optimizer.UnionOptimizationInfos implFileOptDatas
    let tassembly = TypedAssemblyAfterOptimization implFiles
    PrintWholeAssemblyImplementation tcGlobals tcConfig outfile "pass-end" (implFiles |> List.map (fun implFile -> implFile.ImplFile))
    ReportTime tcConfig ("Ending Optimizations")
    tassembly, assemblyOptData, optEnvFirstLoop

//----------------------------------------------------------------------------
// ILX generation
//----------------------------------------------------------------------------

let CreateIlxAssemblyGenerator (_tcConfig:TcConfig, tcImports:TcImports, tcGlobals, tcVal, generatedCcu) =
    let ilxGenerator = new IlxGen.IlxAssemblyGenerator (tcImports.GetImportMap(), tcGlobals, tcVal, generatedCcu)
    let ccus = tcImports.GetCcusInDeclOrder()
    ilxGenerator.AddExternalCcus ccus
    ilxGenerator

let GenerateIlxCode
       (ilxBackend, isInteractiveItExpr, isInteractiveOnMono,
        tcConfig:TcConfig, topAttrs: TopAttribs, optimizedImpls,
        fragName, ilxGenerator: IlxAssemblyGenerator) =

    let mainMethodInfo =
        if (tcConfig.target = CompilerTarget.Dll) || (tcConfig.target = CompilerTarget.Module) then
           None
        else Some topAttrs.mainMethodAttrs

    let ilxGenOpts: IlxGenOptions =
        { generateFilterBlocks = tcConfig.generateFilterBlocks
          emitConstantArraysUsingStaticDataBlobs = not isInteractiveOnMono
          workAroundReflectionEmitBugs=tcConfig.isInteractive // REVIEW: is this still required?
          generateDebugSymbols= tcConfig.debuginfo
          fragName = fragName
          localOptimizationsAreOn= tcConfig.optSettings.localOpt ()
          testFlagEmitFeeFeeAs100001 = tcConfig.testFlagEmitFeeFeeAs100001
          mainMethodInfo= mainMethodInfo
          ilxBackend = ilxBackend
          isInteractive = tcConfig.isInteractive
          isInteractiveItExpr = isInteractiveItExpr
          alwaysCallVirt = tcConfig.alwaysCallVirt }

    ilxGenerator.GenerateCode (ilxGenOpts, optimizedImpls, topAttrs.assemblyAttrs, topAttrs.netModuleAttrs)

//----------------------------------------------------------------------------
// Assembly ref normalization: make sure all assemblies are referred to
// by the same references. Only used for static linking.
//----------------------------------------------------------------------------

let NormalizeAssemblyRefs (ctok, ilGlobals: ILGlobals, tcImports:TcImports) scoref =
    let normalizeAssemblyRefByName nm =
        match tcImports.TryFindDllInfo (ctok, Range.rangeStartup, nm, lookupOnly=false) with
        | Some dllInfo -> dllInfo.ILScopeRef
        | None -> scoref

    match scoref with
    | ILScopeRef.Local
    | ILScopeRef.Module _ -> scoref
    | ILScopeRef.PrimaryAssembly -> normalizeAssemblyRefByName ilGlobals.primaryAssemblyName
    | ILScopeRef.Assembly aref -> normalizeAssemblyRefByName aref.Name

let GetGeneratedILModuleName (t:CompilerTarget) (s:string) =
    // return the name of the file as a module name
    let ext = match t with CompilerTarget.Dll -> "dll" | CompilerTarget.Module -> "netmodule" | _ -> "exe"
    s + "." + ext
