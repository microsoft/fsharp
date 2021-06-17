// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Optional static linking of all DLLs that depend on the F# Library, plus other specified DLLs
module internal FSharp.Compiler.StaticLinking

open System
open Internal.Utilities
open Internal.Utilities.Collections
open Internal.Utilities.Library
open Internal.Utilities.Library.Extras
open FSharp.Compiler.AbstractIL
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AbstractIL.ILBinaryReader
open FSharp.Compiler.CompilerConfig
open FSharp.Compiler.CompilerImports
open FSharp.Compiler.CompilerOptions
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.IO
open FSharp.Compiler.OptimizeInputs
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics

#if !NO_EXTENSIONTYPING
open FSharp.Compiler.ExtensionTyping
#endif

// Handles TypeForwarding for the generated IL model
type TypeForwarding (tcImports: TcImports) =

    // Make a dictionary of ccus passed to the compiler will be looked up by qualified assembly name
    let ccuThunksQualifiedName =
        tcImports.GetCcusInDeclOrder()
        |> List.choose (fun ccuThunk -> ccuThunk.QualifiedName |> Option.map (fun v -> v, ccuThunk))
        |> dict

    // If we can't type forward using exact assembly match, we need to rely on the loader (Policy, Configuration or the coreclr load heuristics), so use try simple name
    let ccuThunksSimpleName =
        tcImports.GetCcusInDeclOrder()
        |> List.choose (fun ccuThunk ->
            if String.IsNullOrEmpty(ccuThunk.AssemblyName) then
                None
            else
                Some (ccuThunk.AssemblyName, ccuThunk))
        |> dict

    let followTypeForwardForILTypeRef (tref:ILTypeRef) =
        let typename =
            let parts =  tref.FullName.Split([|'.'|])
            match parts.Length with
            | 0 -> None
            | 1 -> Some (Array.empty<string>, parts.[0])
            | n -> Some (parts.[0..n-2], parts.[n-1])

        let  scoref = tref.Scope
        match scoref with
        | ILScopeRef.Assembly scope ->
            match ccuThunksQualifiedName.TryGetValue(scope.QualifiedName) with
            | true, ccu ->
                match typename with
                | Some (parts, name) ->
                    let forwarded = ccu.TryForward(parts, name)
                    let result =
                        match forwarded with
                        | Some fwd -> fwd.CompilationPath.ILScopeRef
                        | None -> scoref
                    result
                | None -> scoref
            | false, _ ->
                // Couldn't find an assembly with the version so try using a simple name
                match ccuThunksSimpleName.TryGetValue(scope.Name) with
                | true, ccu ->
                    match typename with
                    | Some (parts, name) ->
                        let forwarded = ccu.TryForward(parts, name)
                        let result =
                            match forwarded with
                            | Some fwd -> fwd.CompilationPath.ILScopeRef
                            | None -> scoref
                        result
                    | None -> scoref
                | false, _ -> scoref
        | _ -> scoref

    let typeForwardILTypeRef (tref: ILTypeRef) =
        let scoref1 = tref.Scope
        let scoref2 = followTypeForwardForILTypeRef tref
        if scoref1 === scoref2 then tref
        else ILTypeRef.Create (scoref2, tref.Enclosing, tref.Name)

    member _.TypeForwardILTypeRef tref = typeForwardILTypeRef tref

let debugStaticLinking = condition "FSHARP_DEBUG_STATIC_LINKING"

let StaticLinkILModules (tcConfig:TcConfig, ilGlobals, tcImports, ilxMainModule, dependentILModules: (CcuThunk option * ILModuleDef) list) =
    if isNil dependentILModules then
        ilxMainModule, (fun x -> x)
    else
        let typeForwarding = new TypeForwarding(tcImports)

        // Check no dependent assemblies use quotations
        let dependentCcuUsingQuotations = dependentILModules |> List.tryPick (function (Some ccu, _) when ccu.UsesFSharp20PlusQuotations -> Some ccu | _ -> None)
        match dependentCcuUsingQuotations with
        | Some ccu -> error(Error(FSComp.SR.fscQuotationLiteralsStaticLinking(ccu.AssemblyName), rangeStartup))
        | None -> ()

        // Check we're not static linking a .EXE
        if dependentILModules |> List.exists (fun (_, x) -> not x.IsDLL)  then
            error(Error(FSComp.SR.fscStaticLinkingNoEXE(), rangeStartup))

        // Check we're not static linking something that is not pure IL
        if dependentILModules |> List.exists (fun (_, x) -> not x.IsILOnly)  then
            error(Error(FSComp.SR.fscStaticLinkingNoMixedDLL(), rangeStartup))

        // The set of short names for the all dependent assemblies
        let assems =
            set [ for (_, m) in dependentILModules  do
                    match m.Manifest with
                    | Some m -> yield m.Name
                    | _ -> () ]

        // A rewriter which rewrites scope references to things in dependent assemblies to be local references
        let rewriteExternalRefsToLocalRefs x =
            if assems.Contains (getNameOfScopeRef x) then ILScopeRef.Local else x

        let savedManifestAttrs =
            [ for (_, depILModule) in dependentILModules do
                match depILModule.Manifest with
                | Some m ->
                    for ca in m.CustomAttrs.AsArray do
                        if ca.Method.MethodRef.DeclaringTypeRef.FullName = typeof<CompilationMappingAttribute>.FullName then
                            yield ca
                | _ -> () ]

        let savedResources =
            let allResources = [ for (ccu, m) in dependentILModules do for r in m.Resources.AsList do yield (ccu, r) ]
            // Don't save interface, optimization or resource definitions for provider-generated assemblies.
            // These are "fake".
            let isProvided (ccu: CcuThunk option) =
#if !NO_EXTENSIONTYPING
                match ccu with
                | Some c -> c.IsProviderGenerated
                | None -> false
#else
                ignore ccu
                false
#endif

            // Save only the interface/optimization attributes of generated data
            let intfDataResources, others = allResources |> List.partition (snd >> IsSignatureDataResource)
            let intfDataResources =
                [ for (ccu, r) in intfDataResources do
                     if tcConfig.GenerateSignatureData && not (isProvided ccu) then
                         yield r ]

            let optDataResources, others = others |> List.partition (snd >> IsOptimizationDataResource)
            let optDataResources =
                [ for (ccu, r) in optDataResources do
                    if tcConfig.GenerateOptimizationData && not (isProvided ccu) then
                        yield r ]

            let otherResources = others |> List.map snd

            let result = intfDataResources@optDataResources@otherResources
            result

        let moduls = ilxMainModule :: (List.map snd dependentILModules)

        let savedNativeResources =
            [ //yield! ilxMainModule.NativeResources
                for m in moduls do
                    yield! m.NativeResources ]

        let topTypeDefs, normalTypeDefs =
            moduls
            |> List.map (fun m -> m.TypeDefs.AsList |> List.partition (fun td -> isTypeNameForGlobalFunctions td.Name))
            |> List.unzip

        let topTypeDef =
            let topTypeDefs = List.concat topTypeDefs
            mkILTypeDefForGlobalFunctions ilGlobals
                (mkILMethods (topTypeDefs |> List.collect (fun td -> td.Methods.AsList)),
                mkILFields (topTypeDefs |> List.collect (fun td -> td.Fields.AsList)))

        let ilxMainModule =
            let main =
                { ilxMainModule with
                    Manifest = (let m = ilxMainModule.ManifestOfAssembly in Some {m with CustomAttrsStored = storeILCustomAttrs (mkILCustomAttrs (m.CustomAttrs.AsList @ savedManifestAttrs)) })
                    CustomAttrsStored = storeILCustomAttrs (mkILCustomAttrs [ for m in moduls do yield! m.CustomAttrs.AsArray ])
                    TypeDefs = mkILTypeDefs (topTypeDef :: List.concat normalTypeDefs)
                    Resources = mkILResources (savedResources @ ilxMainModule.Resources.AsList)
                    NativeResources = savedNativeResources }
            Morphs.morphILTypeRefsInILModuleMemoized typeForwarding.TypeForwardILTypeRef main

        ilxMainModule, rewriteExternalRefsToLocalRefs

[<NoEquality; NoComparison>]
type Node =
    { name: string
      data: ILModuleDef
      ccu: option<CcuThunk>
      refs: ILReferences
      mutable edges: list<Node>
      mutable visited: bool }

// Find all IL modules that are to be statically linked given the static linking roots.
let FindDependentILModulesForStaticLinking (ctok, tcConfig: TcConfig, tcImports: TcImports, ilGlobals: ILGlobals, ilxMainModule) =
    if not tcConfig.standalone && tcConfig.extraStaticLinkRoots.IsEmpty then
        []
    else
        // Recursively find all referenced modules and add them to a module graph
        let depModuleTable = HashMultiMap(0, HashIdentity.Structural)
        let dummyEntry nm =
            { refs = IL.emptyILRefs
              name=nm
              ccu=None
              data=ilxMainModule // any old module
              edges = []
              visited = true }
        let assumedIndependentSet = set [ "mscorlib";  "System"; "System.Core"; "System.Xml"; "Microsoft.Build.Framework"; "Microsoft.Build.Utilities"; "netstandard" ]

        begin
            let mutable remaining = (computeILRefs ilGlobals ilxMainModule).AssemblyReferences
            while not (isNil remaining) do
                let ilAssemRef = List.head remaining
                remaining <- List.tail remaining
                if assumedIndependentSet.Contains ilAssemRef.Name || (ilAssemRef.PublicKey = Some ecmaPublicKey) then
                    depModuleTable.[ilAssemRef.Name] <- dummyEntry ilAssemRef.Name
                else
                    if not (depModuleTable.ContainsKey ilAssemRef.Name) then
                        match tcImports.TryFindDllInfo(ctok, Range.rangeStartup, ilAssemRef.Name, lookupOnly=false) with
                        | Some dllInfo ->
                            let ccu =
                                match tcImports.FindCcuFromAssemblyRef (ctok, Range.rangeStartup, ilAssemRef) with
                                | ResolvedCcu ccu -> Some ccu
                                | UnresolvedCcu(_ccuName) -> None

                            let fileName = dllInfo.FileName
                            let modul =
                                let pdbDirPathOption =
                                    // We open the pdb file if one exists parallel to the binary we
                                    // are reading, so that --standalone will preserve debug information.
                                    if tcConfig.openDebugInformationForLaterStaticLinking then
                                        let pdbDir = (try FileSystem.GetDirectoryNameShim fileName with _ -> ".")
                                        let pdbFile = (try FileSystemUtils.chopExtension fileName with _ -> fileName)+".pdb"
                                        if FileSystem.FileExistsShim pdbFile then
                                            Some pdbDir
                                        else
                                            None
                                    else
                                        None

                                let opts : ILReaderOptions =
                                    { metadataOnly = MetadataOnlyFlag.No // turn this off here as we need the actual IL code
                                      reduceMemoryUsage = tcConfig.reduceMemoryUsage
                                      pdbDirPath = pdbDirPathOption
                                      tryGetMetadataSnapshot = (fun _ -> None) }

                                let reader = ILBinaryReader.OpenILModuleReader dllInfo.FileName opts
                                reader.ILModuleDef

                            let refs =
                                if ilAssemRef.Name = GetFSharpCoreLibraryName() then
                                    IL.emptyILRefs
                                elif not modul.IsILOnly then
                                    warning(Error(FSComp.SR.fscIgnoringMixedWhenLinking ilAssemRef.Name, rangeStartup))
                                    IL.emptyILRefs
                                else
                                    { AssemblyReferences = dllInfo.ILAssemblyRefs
                                      ModuleReferences = [] }

                            depModuleTable.[ilAssemRef.Name] <-
                                { refs=refs
                                  name=ilAssemRef.Name
                                  ccu=ccu
                                  data=modul
                                  edges = []
                                  visited = false }

                            // Push the new work items
                            remaining <- refs.AssemblyReferences @ remaining

                        | None ->
                            warning(Error(FSComp.SR.fscAssumeStaticLinkContainsNoDependencies(ilAssemRef.Name), rangeStartup))
                            depModuleTable.[ilAssemRef.Name] <- dummyEntry ilAssemRef.Name
            done
        end

        ReportTime tcConfig "Find dependencies"

        // Add edges from modules to the modules that depend on them
        for (KeyValue(_, n)) in depModuleTable do
            for aref in n.refs.AssemblyReferences do
                let n2 = depModuleTable.[aref.Name]
                n2.edges <- n :: n2.edges

        // Find everything that depends on FSharp.Core
        let roots =
            [ if tcConfig.standalone && depModuleTable.ContainsKey (GetFSharpCoreLibraryName()) then
                  yield depModuleTable.[GetFSharpCoreLibraryName()]
              for n in tcConfig.extraStaticLinkRoots  do
                  match depModuleTable.TryFind n with
                  | Some x -> yield x
                  | None -> error(Error(FSComp.SR.fscAssemblyNotFoundInDependencySet n, rangeStartup))
            ]

        let mutable remaining = roots
        [ while not (isNil remaining) do
            let n = List.head remaining
            remaining <- List.tail remaining
            if not n.visited then
                n.visited <- true
                remaining <- n.edges @ remaining
                yield (n.ccu, n.data)  ]

// Add all provider-generated assemblies into the static linking set
let FindProviderGeneratedILModules (ctok, tcImports: TcImports, providerGeneratedAssemblies: (ImportedBinary * _) list) =
    [ for (importedBinary, provAssemStaticLinkInfo) in providerGeneratedAssemblies do
        let ilAssemRef =
            match importedBinary.ILScopeRef with
            | ILScopeRef.Assembly aref -> aref
            | _ -> failwith "Invalid ILScopeRef, expected ILScopeRef.Assembly"
        if debugStaticLinking then printfn "adding provider-generated assembly '%s' into static linking set" ilAssemRef.Name
        match tcImports.TryFindDllInfo(ctok, Range.rangeStartup, ilAssemRef.Name, lookupOnly=false) with
        | Some dllInfo ->
            let ccu =
                match tcImports.FindCcuFromAssemblyRef (ctok, Range.rangeStartup, ilAssemRef) with
                | ResolvedCcu ccu -> Some ccu
                | UnresolvedCcu(_ccuName) -> None

            let modul = dllInfo.RawMetadata.TryGetILModuleDef().Value
            yield (ccu, dllInfo.ILScopeRef, modul), (ilAssemRef.Name, provAssemStaticLinkInfo)
        | None -> () ]

// Compute a static linker. This only captures tcImports (a large data structure) if
// static linking is enabled. Normally this is not the case, which lets us collect tcImports
// prior to this point.
let StaticLink (ctok, tcConfig: TcConfig, tcImports: TcImports, ilGlobals: ILGlobals) =

#if !NO_EXTENSIONTYPING
    let providerGeneratedAssemblies =

        [ // Add all EST-generated assemblies into the static linking set
            for KeyValue(_, importedBinary: ImportedBinary) in tcImports.DllTable do
                if importedBinary.IsProviderGenerated then
                    match importedBinary.ProviderGeneratedStaticLinkMap with
                    | None -> ()
                    | Some provAssemStaticLinkInfo -> yield (importedBinary, provAssemStaticLinkInfo) ]
#endif
    if not tcConfig.standalone && tcConfig.extraStaticLinkRoots.IsEmpty
#if !NO_EXTENSIONTYPING
            && providerGeneratedAssemblies.IsEmpty
#endif
            then
        (fun ilxMainModule -> ilxMainModule)
    else
        (fun ilxMainModule  ->
            ReportTime tcConfig "Find assembly references"

            let dependentILModules = FindDependentILModulesForStaticLinking (ctok, tcConfig, tcImports, ilGlobals, ilxMainModule)

            ReportTime tcConfig "Static link"

#if !NO_EXTENSIONTYPING
            Morphs.enableMorphCustomAttributeData()
            let providerGeneratedILModules =  FindProviderGeneratedILModules (ctok, tcImports, providerGeneratedAssemblies)

            // Transform the ILTypeRefs references in the IL of all provider-generated assemblies so that the references
            // are now local.
            let providerGeneratedILModules =

                providerGeneratedILModules |> List.map (fun ((ccu, ilOrigScopeRef, ilModule), (_, localProvAssemStaticLinkInfo)) ->
                    let ilAssemStaticLinkMap =
                        dict [ for (_, (_, provAssemStaticLinkInfo)) in providerGeneratedILModules do
                                    for KeyValue(k, v) in provAssemStaticLinkInfo.ILTypeMap do
                                        yield (k, v)
                               for KeyValue(k, v) in localProvAssemStaticLinkInfo.ILTypeMap do
                                   yield (ILTypeRef.Create(ILScopeRef.Local, k.Enclosing, k.Name), v) ]

                    let ilModule =
                        ilModule |> Morphs.morphILTypeRefsInILModuleMemoized (fun tref ->
                                if debugStaticLinking then printfn "deciding whether to rewrite type ref %A" tref.QualifiedName
                                let ok, v = ilAssemStaticLinkMap.TryGetValue tref
                                if ok then
                                    if debugStaticLinking then printfn "rewriting type ref %A to %A" tref.QualifiedName v.QualifiedName
                                    v
                                else
                                    tref)
                    (ccu, ilOrigScopeRef, ilModule))

            // Relocate provider generated type definitions into the expected shape for the [<Generate>] declarations in an assembly
            let providerGeneratedILModules, ilxMainModule =
                  // Build a dictionary of all remapped IL type defs
                  let ilOrigTyRefsForProviderGeneratedTypesToRelocate =
                      let rec walk acc (ProviderGeneratedType(ilOrigTyRef, _, xs) as node) = List.fold walk ((ilOrigTyRef, node) :: acc) xs
                      dict (Seq.fold walk [] tcImports.ProviderGeneratedTypeRoots)

                  // Build a dictionary of all IL type defs, mapping ilOrigTyRef --> ilTypeDef
                  let allTypeDefsInProviderGeneratedAssemblies =
                      let rec loop ilOrigTyRef (ilTypeDef: ILTypeDef) =
                          seq { yield (ilOrigTyRef, ilTypeDef)
                                for ntdef in ilTypeDef.NestedTypes do
                                    yield! loop (mkILTyRefInTyRef (ilOrigTyRef, ntdef.Name)) ntdef }
                      dict [
                          for (_ccu, ilOrigScopeRef, ilModule) in providerGeneratedILModules do
                              for td in ilModule.TypeDefs do
                                  yield! loop (mkILTyRef (ilOrigScopeRef, td.Name)) td ]


                  // Debugging output
                  if debugStaticLinking then
                      for (ProviderGeneratedType(ilOrigTyRef, _, _)) in tcImports.ProviderGeneratedTypeRoots do
                          printfn "Have [<Generate>] root '%s'" ilOrigTyRef.QualifiedName

                  // Build the ILTypeDefs for generated types, starting with the roots
                  let generatedILTypeDefs =
                      let rec buildRelocatedGeneratedType (ProviderGeneratedType(ilOrigTyRef, ilTgtTyRef, ch)) =
                          let isNested = not (isNil ilTgtTyRef.Enclosing)
                          match allTypeDefsInProviderGeneratedAssemblies.TryGetValue ilOrigTyRef with
                          | true, ilOrigTypeDef ->
                              if debugStaticLinking then printfn "Relocating %s to %s " ilOrigTyRef.QualifiedName ilTgtTyRef.QualifiedName
                              let ilOrigTypeDef =
                                if isNested then
                                    ilOrigTypeDef
                                        .WithAccess(match ilOrigTypeDef.Access with
                                                    | ILTypeDefAccess.Public -> ILTypeDefAccess.Nested ILMemberAccess.Public
                                                    | ILTypeDefAccess.Private -> ILTypeDefAccess.Nested ILMemberAccess.Private
                                                    | _ -> ilOrigTypeDef.Access)
                                else ilOrigTypeDef
                              ilOrigTypeDef.With(name = ilTgtTyRef.Name,
                                                 nestedTypes = mkILTypeDefs (List.map buildRelocatedGeneratedType ch))
                          | _ ->
                              // If there is no matching IL type definition, then make a simple container class
                              if debugStaticLinking then
                                  printfn "Generating simple class '%s' because we didn't find an original type '%s' in a provider generated assembly"
                                      ilTgtTyRef.QualifiedName ilOrigTyRef.QualifiedName

                              let access = (if isNested  then ILTypeDefAccess.Nested ILMemberAccess.Public else ILTypeDefAccess.Public)
                              let tdefs = mkILTypeDefs (List.map buildRelocatedGeneratedType ch)
                              mkILSimpleClass ilGlobals (ilTgtTyRef.Name, access, emptyILMethods, emptyILFields, tdefs, emptyILProperties, emptyILEvents, emptyILCustomAttrs, ILTypeInit.OnAny)

                      [ for (ProviderGeneratedType(_, ilTgtTyRef, _) as node) in tcImports.ProviderGeneratedTypeRoots  do
                           yield (ilTgtTyRef, buildRelocatedGeneratedType node) ]

                  // Implant all the generated type definitions into the ilxMainModule (generating a new ilxMainModule)
                  let ilxMainModule =

                      /// Split the list into left, middle and right parts at the first element satisfying 'p'. If no element matches return
                      /// 'None' for the middle part.
                      let trySplitFind p xs =
                          let rec loop xs acc =
                              match xs with
                              | [] -> List.rev acc, None, []
                              | h :: t -> if p h then List.rev acc, Some h, t else loop t (h :: acc)
                          loop xs []

                      /// Implant the (nested) type definition 'td' at path 'enc' in 'tdefs'.
                      let rec implantTypeDef isNested (tdefs: ILTypeDefs) (enc: string list) (td: ILTypeDef) =
                          match enc with
                          | [] -> addILTypeDef td tdefs
                          | h :: t ->
                               let tdefs = tdefs.AsList
                               let (ltdefs, htd, rtdefs) =
                                   match tdefs |> trySplitFind (fun td -> td.Name = h) with
                                   | (ltdefs, None, rtdefs) ->
                                       let access = if isNested  then ILTypeDefAccess.Nested ILMemberAccess.Public else ILTypeDefAccess.Public
                                       let fresh = mkILSimpleClass ilGlobals (h, access, emptyILMethods, emptyILFields, emptyILTypeDefs, emptyILProperties, emptyILEvents, emptyILCustomAttrs, ILTypeInit.OnAny)
                                       (ltdefs, fresh, rtdefs)
                                   | (ltdefs, Some htd, rtdefs) ->
                                       (ltdefs, htd, rtdefs)
                               let htd = htd.With(nestedTypes = implantTypeDef true htd.NestedTypes t td)
                               mkILTypeDefs (ltdefs @ [htd] @ rtdefs)

                      let newTypeDefs =
                          (ilxMainModule.TypeDefs, generatedILTypeDefs) ||> List.fold (fun acc (ilTgtTyRef, td) ->
                              if debugStaticLinking then printfn "implanting '%s' at '%s'" td.Name ilTgtTyRef.QualifiedName
                              implantTypeDef false acc ilTgtTyRef.Enclosing td)
                      { ilxMainModule with TypeDefs = newTypeDefs }

                  // Remove any ILTypeDefs from the provider generated modules if they have been relocated because of a [<Generate>] declaration.
                  let providerGeneratedILModules =
                      providerGeneratedILModules |> List.map (fun (ccu, ilOrigScopeRef, ilModule) ->
                          let ilTypeDefsAfterRemovingRelocatedTypes =
                              let rec rw enc (tdefs: ILTypeDefs) =
                                  mkILTypeDefs
                                   [ for tdef in tdefs do
                                        let ilOrigTyRef = mkILNestedTyRef (ilOrigScopeRef, enc, tdef.Name)
                                        if  not (ilOrigTyRefsForProviderGeneratedTypesToRelocate.ContainsKey ilOrigTyRef) then
                                          if debugStaticLinking then printfn "Keep provided type %s in place because it wasn't relocated" ilOrigTyRef.QualifiedName
                                          yield tdef.With(nestedTypes = rw (enc@[tdef.Name]) tdef.NestedTypes) ]
                              rw [] ilModule.TypeDefs
                          (ccu, { ilModule with TypeDefs = ilTypeDefsAfterRemovingRelocatedTypes }))

                  providerGeneratedILModules, ilxMainModule

            Morphs.disableMorphCustomAttributeData()
#else
            let providerGeneratedILModules = []
#endif

            // Glue all this stuff into ilxMainModule
            let ilxMainModule, rewriteExternalRefsToLocalRefs =
                  StaticLinkILModules (tcConfig, ilGlobals, tcImports, ilxMainModule, dependentILModules @ providerGeneratedILModules)

            // Rewrite type and assembly references
            let ilxMainModule =
                  let isMscorlib = ilGlobals.primaryAssemblyName = PrimaryAssembly.Mscorlib.Name
                  let validateTargetPlatform (scopeRef : ILScopeRef) =
                      let name = getNameOfScopeRef scopeRef
                      if (not isMscorlib && name = PrimaryAssembly.Mscorlib.Name) then
                          error (Error(FSComp.SR.fscStaticLinkingNoProfileMismatches(), rangeCmdArgs))
                      scopeRef
                  let rewriteAssemblyRefsToMatchLibraries = NormalizeAssemblyRefs (ctok, ilGlobals, tcImports)
                  Morphs.morphILTypeRefsInILModuleMemoized (Morphs.morphILScopeRefsInILTypeRef (validateTargetPlatform >> rewriteExternalRefsToLocalRefs >> rewriteAssemblyRefsToMatchLibraries)) ilxMainModule

            ilxMainModule)
