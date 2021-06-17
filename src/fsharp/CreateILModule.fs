// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.CreateILModule

open System
open System.IO
open System.Reflection

open Internal.Utilities
open Internal.Utilities.Library
open Internal.Utilities.Library.Extras
open FSharp.Compiler
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AbstractIL.NativeRes
open FSharp.Compiler.AbstractIL.StrongNameSign
open FSharp.Compiler.BinaryResourceFormats
open FSharp.Compiler.CheckDeclarations
open FSharp.Compiler.CompilerConfig
open FSharp.Compiler.CompilerImports
open FSharp.Compiler.CompilerGlobalState
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.IlxGen
open FSharp.Compiler.IO
open FSharp.Compiler.OptimizeInputs
open FSharp.Compiler.Text.Range
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.TcGlobals

/// Helpers for finding attributes
module AttributeHelpers =

    /// Try to find an attribute that takes a string argument
    let TryFindStringAttribute (g: TcGlobals) attrib attribs =
      match g.TryFindSysAttrib attrib with
      | None -> None
      | Some attribRef ->
        match TryFindFSharpAttribute g attribRef attribs with
        | Some (Attrib(_, _, [ AttribStringArg s ], _, _, _, _))  -> Some (s)
        | _ -> None

    let TryFindIntAttribute (g: TcGlobals) attrib attribs =
      match g.TryFindSysAttrib attrib with
      | None -> None
      | Some attribRef ->
        match TryFindFSharpAttribute g attribRef attribs with
        | Some (Attrib(_, _, [ AttribInt32Arg i ], _, _, _, _)) -> Some (i)
        | _ -> None

    let TryFindBoolAttribute (g: TcGlobals) attrib attribs =
      match g.TryFindSysAttrib attrib with
      | None -> None
      | Some attribRef ->
        match TryFindFSharpAttribute g attribRef attribs with
        | Some (Attrib(_, _, [ AttribBoolArg p ], _, _, _, _)) -> Some (p)
        | _ -> None

    let (|ILVersion|_|) (versionString: string) =
        try Some (parseILVersion versionString)
        with e ->
            None

//----------------------------------------------------------------------------
// ValidateKeySigningAttributes, GetStrongNameSigner
//----------------------------------------------------------------------------

/// Represents the configuration settings used to perform strong-name signing
type StrongNameSigningInfo = StrongNameSigningInfo of delaysign: bool * publicsign: bool * signer: string option * container: string option

/// Validate the attributes and configuration settings used to perform strong-name signing
let ValidateKeySigningAttributes (tcConfig : TcConfig, tcGlobals, topAttrs) =
    let delaySignAttrib = AttributeHelpers.TryFindBoolAttribute tcGlobals "System.Reflection.AssemblyDelaySignAttribute" topAttrs.assemblyAttrs
    let signerAttrib = AttributeHelpers.TryFindStringAttribute tcGlobals "System.Reflection.AssemblyKeyFileAttribute" topAttrs.assemblyAttrs
    let containerAttrib = AttributeHelpers.TryFindStringAttribute tcGlobals "System.Reflection.AssemblyKeyNameAttribute" topAttrs.assemblyAttrs

    // if delaySign is set via an attribute, validate that it wasn't set via an option
    let delaysign =
        match delaySignAttrib with
        | Some delaysign ->
          if tcConfig.delaysign then
            warning(Error(FSComp.SR.fscDelaySignWarning(), rangeCmdArgs))
            tcConfig.delaysign
          else
            delaysign
        | _ -> tcConfig.delaysign

    // if signer is set via an attribute, validate that it wasn't set via an option
    let signer =
        match signerAttrib with
        | Some signer ->
            if tcConfig.signer.IsSome && tcConfig.signer <> Some signer then
                warning(Error(FSComp.SR.fscKeyFileWarning(), rangeCmdArgs))
                tcConfig.signer
            else
                Some signer
        | None -> tcConfig.signer

    // if container is set via an attribute, validate that it wasn't set via an option, and that they keyfile wasn't set
    // if keyfile was set, use that instead (silently)
    // REVIEW: This is C# behavior, but it seems kind of sketchy that we fail silently
    let container =
        match containerAttrib with
        | Some container ->
            if not (FSharpEnvironment.isRunningOnCoreClr) then
                warning(Error(FSComp.SR.containerDeprecated(), rangeCmdArgs))
            if tcConfig.container.IsSome && tcConfig.container <> Some container then
              warning(Error(FSComp.SR.fscKeyNameWarning(), rangeCmdArgs))
              tcConfig.container
            else
              Some container
        | None -> tcConfig.container

    StrongNameSigningInfo (delaysign, tcConfig.publicsign, signer, container)

/// Get the object used to perform strong-name signing
let GetStrongNameSigner signingInfo =
    let (StrongNameSigningInfo(delaysign, publicsign, signer, container)) = signingInfo
    // REVIEW: favor the container over the key file - C# appears to do this
    match container with
    | Some container ->
        Some (ILStrongNameSigner.OpenKeyContainer container)
    | None ->
        match signer with
        | None -> None
        | Some s ->
            try
                if publicsign || delaysign then
                    Some (ILStrongNameSigner.OpenPublicKeyOptions s publicsign)
                else
                    Some (ILStrongNameSigner.OpenKeyPairFile s)
            with _ ->
                // Note :: don't use errorR here since we really want to fail and not produce a binary
                error(Error(FSComp.SR.fscKeyFileCouldNotBeOpened s, rangeCmdArgs))

//----------------------------------------------------------------------------
// Building the contents of the finalized IL module
//----------------------------------------------------------------------------

module MainModuleBuilder =

    let injectedCompatTypes =
      set [ "System.Tuple`1"
            "System.Tuple`2"
            "System.Tuple`3"
            "System.Tuple`4"
            "System.Tuple`5"
            "System.Tuple`6"
            "System.Tuple`7"
            "System.Tuple`8"
            "System.ITuple"
            "System.Tuple"
            "System.Collections.IStructuralComparable"
            "System.Collections.IStructuralEquatable" ]

    let typesForwardedToMscorlib =
      set [ "System.AggregateException"
            "System.Threading.CancellationTokenRegistration"
            "System.Threading.CancellationToken"
            "System.Threading.CancellationTokenSource"
            "System.Lazy`1"
            "System.IObservable`1"
            "System.IObserver`1" ]

    let typesForwardedToSystemNumerics =
      set [ "System.Numerics.BigInteger" ]

    let createMscorlibExportList (tcGlobals: TcGlobals) =
      // We want to write forwarders out for all injected types except for System.ITuple, which is internal
      // Forwarding System.ITuple will cause FxCop failures on 4.0
      Set.union (Set.filter (fun t -> t <> "System.ITuple") injectedCompatTypes) typesForwardedToMscorlib |>
          Seq.map (fun t -> mkTypeForwarder (tcGlobals.ilg.primaryAssemblyScopeRef) t (mkILNestedExportedTypes List.empty<ILNestedExportedType>) (mkILCustomAttrs List.empty<ILAttribute>) ILTypeDefAccess.Public )
          |> Seq.toList

    let createSystemNumericsExportList (tcConfig: TcConfig) (tcImports: TcImports) =
        let refNumericsDllName =
            if (tcConfig.primaryAssembly.Name = "mscorlib") then "System.Numerics"
            else "System.Runtime.Numerics"
        let numericsAssemblyRef =
            match tcImports.GetImportedAssemblies() |> List.tryFind<ImportedAssembly>(fun a -> a.FSharpViewOfMetadata.AssemblyName = refNumericsDllName) with
            | Some asm ->
                match asm.ILScopeRef with
                | ILScopeRef.Assembly aref -> Some aref
                | _ -> None
            | None -> None
        match numericsAssemblyRef with
        | Some aref ->
            let systemNumericsAssemblyRef = ILAssemblyRef.Create(refNumericsDllName, aref.Hash, aref.PublicKey, aref.Retargetable, aref.Version, aref.Locale)
            typesForwardedToSystemNumerics |>
                Seq.map (fun t ->
                            { ScopeRef = ILScopeRef.Assembly systemNumericsAssemblyRef
                              Name = t
                              Attributes = enum<TypeAttributes>(0x00200000) ||| TypeAttributes.Public
                              Nested = mkILNestedExportedTypes []
                              CustomAttrsStored = storeILCustomAttrs emptyILCustomAttrs
                              MetadataIndex = NoMetadataIdx }) |>
                Seq.toList
        | None -> []

    let fileVersion findStringAttr (assemblyVersion: ILVersionInfo) =
        let attrName = "System.Reflection.AssemblyFileVersionAttribute"
        match findStringAttr attrName with
        | None -> assemblyVersion
        | Some (AttributeHelpers.ILVersion v) -> v
        | Some _ ->
            // Warning will be reported by CheckExpressions.fs
            assemblyVersion

    let productVersion findStringAttr (fileVersion: ILVersionInfo) =
        let attrName = "System.Reflection.AssemblyInformationalVersionAttribute"
        let toDotted (version: ILVersionInfo) = sprintf "%d.%d.%d.%d" version.Major version.Minor version.Build version.Revision
        match findStringAttr attrName with
        | None | Some "" -> fileVersion |> toDotted
        | Some (AttributeHelpers.ILVersion v) -> v |> toDotted
        | Some v ->
            // Warning will be reported by CheckExpressions.fs
            v

    let productVersionToILVersionInfo (version: string) : ILVersionInfo =
        let parseOrZero i (v:string) =
            let v =
                // When i = 3 then this is the 4th part of the version.  The last part of the version can be trailed by any characters so we trim them off
                if i <> 3 then
                    v
                else
                    ((false, ""), v)
                    ||> Seq.fold(fun (finished, v) c ->
                        match finished with
                        | false when Char.IsDigit(c) -> false, v + c.ToString()
                        | _ -> true, v)
                    |> snd
            match System.UInt16.TryParse v with
            | (true, i) -> i
            | (false, _) -> 0us
        let validParts =
            version.Split('.')
            |> Array.mapi(fun i v -> parseOrZero i v)
            |> Seq.toList
        match validParts @ [0us; 0us; 0us; 0us] with
        | major :: minor :: build :: rev :: _ -> ILVersionInfo(major, minor, build, rev)
        | x -> failwithf "error converting product version '%s' to binary, tried '%A' " version x


    let CreateMainModule
            (ctok, tcConfig: TcConfig, tcGlobals, tcImports: TcImports,
             pdbfile, assemblyName, outfile, topAttrs,
             sigDataAttributes: ILAttribute list, sigDataResources: ILResource list, optDataResources: ILResource list,
             codegenResults, assemVerFromAttrib, metadataVersion, secDecls) =

        RequireCompilationThread ctok
        let ilTypeDefs =
            //let topTypeDef = mkILTypeDefForGlobalFunctions tcGlobals.ilg (mkILMethods [], emptyILFields)
            mkILTypeDefs codegenResults.ilTypeDefs

        let mainModule =
            let hashAlg = AttributeHelpers.TryFindIntAttribute tcGlobals "System.Reflection.AssemblyAlgorithmIdAttribute" topAttrs.assemblyAttrs
            let locale = AttributeHelpers.TryFindStringAttribute tcGlobals "System.Reflection.AssemblyCultureAttribute" topAttrs.assemblyAttrs
            let flags =  match AttributeHelpers.TryFindIntAttribute tcGlobals "System.Reflection.AssemblyFlagsAttribute" topAttrs.assemblyAttrs with | Some f -> f | _ -> 0x0

            // You're only allowed to set a locale if the assembly is a library
            if (locale <> None && locale.Value <> "") && tcConfig.target <> CompilerTarget.Dll then
              error(Error(FSComp.SR.fscAssemblyCultureAttributeError(), rangeCmdArgs))

            // Add the type forwarders to any .NET DLL post-.NET-2.0, to give binary compatibility
            let exportedTypesList =
                if tcConfig.compilingFslib then
                   List.append (createMscorlibExportList tcGlobals) (createSystemNumericsExportList tcConfig tcImports)
                else
                    []

            let ilModuleName = GetGeneratedILModuleName tcConfig.target assemblyName
            let isDLL = (tcConfig.target = CompilerTarget.Dll || tcConfig.target = CompilerTarget.Module)
            mkILSimpleModule assemblyName ilModuleName isDLL tcConfig.subsystemVersion tcConfig.useHighEntropyVA ilTypeDefs hashAlg locale flags (mkILExportedTypes exportedTypesList) metadataVersion

        let disableJitOptimizations = not (tcConfig.optSettings.jitOpt())

        let tcVersion = tcConfig.version.GetVersionInfo(tcConfig.implicitIncludeDir)

        let reflectedDefinitionAttrs, reflectedDefinitionResources =
            codegenResults.quotationResourceInfo
            |> List.map (fun (referencedTypeDefs, reflectedDefinitionBytes) ->
                let reflectedDefinitionResourceName = QuotationPickler.SerializedReflectedDefinitionsResourceNameBase+"-"+assemblyName+"-"+string(newUnique())+"-"+string(hash reflectedDefinitionBytes)
                let reflectedDefinitionAttrs =
                    let qf = QuotationTranslator.QuotationGenerationScope.ComputeQuotationFormat tcGlobals
                    if qf.SupportsDeserializeEx then
                        [ mkCompilationMappingAttrForQuotationResource tcGlobals (reflectedDefinitionResourceName, referencedTypeDefs) ]
                    else
                        [  ]
                let reflectedDefinitionResource =
                  { Name=reflectedDefinitionResourceName
                    Location = ILResourceLocation.Local(ByteStorage.FromByteArray(reflectedDefinitionBytes))
                    Access= ILResourceAccess.Public
                    CustomAttrsStored = storeILCustomAttrs emptyILCustomAttrs
                    MetadataIndex = NoMetadataIdx }
                reflectedDefinitionAttrs, reflectedDefinitionResource)
            |> List.unzip
            |> (fun (attrs, resource) -> List.concat attrs, resource)

        let manifestAttrs =
            mkILCustomAttrs
                 [ if not tcConfig.internConstantStrings then
                       yield mkILCustomAttribute
                                 (tcGlobals.FindSysILTypeRef "System.Runtime.CompilerServices.CompilationRelaxationsAttribute",
                                  [tcGlobals.ilg.typ_Int32], [ILAttribElem.Int32( 8)], [])
                   yield! sigDataAttributes
                   yield! codegenResults.ilAssemAttrs
                   if Option.isSome pdbfile then
                       yield (tcGlobals.mkDebuggableAttributeV2 (tcConfig.jitTracking, tcConfig.ignoreSymbolStoreSequencePoints, disableJitOptimizations, false (* enableEnC *) ))
                   yield! reflectedDefinitionAttrs ]

        // Make the manifest of the assembly
        let manifest =
             if tcConfig.target = CompilerTarget.Module then None else
             let man = mainModule.ManifestOfAssembly
             let ver =
                 match assemVerFromAttrib with
                 | None -> tcVersion
                 | Some v -> v
             Some { man with Version= Some ver
                             CustomAttrsStored = storeILCustomAttrs manifestAttrs
                             DisableJitOptimizations=disableJitOptimizations
                             JitTracking= tcConfig.jitTracking
                             IgnoreSymbolStoreSequencePoints = tcConfig.ignoreSymbolStoreSequencePoints
                             SecurityDeclsStored=storeILSecurityDecls secDecls }

        let resources =
          mkILResources
            [ for file in tcConfig.embedResources do
                 let name, bytes, pub =
                         let file, name, pub = TcConfigBuilder.SplitCommandLineResourceInfo file
                         let file = tcConfig.ResolveSourceFile(rangeStartup, file, tcConfig.implicitIncludeDir)
                         let bytes = FileSystem.OpenFileForReadShim(file).ReadAllBytes()
                         name, bytes, pub
                 yield { Name=name
                         // TODO: We probably can directly convert ByteMemory to ByteStorage, without reading all bytes.
                         Location=ILResourceLocation.Local(ByteStorage.FromByteArray(bytes))
                         Access=pub
                         CustomAttrsStored=storeILCustomAttrs emptyILCustomAttrs
                         MetadataIndex = NoMetadataIdx }

              yield! reflectedDefinitionResources
              yield! sigDataResources
              yield! optDataResources
              for ri in tcConfig.linkResources do
                 let file, name, pub = TcConfigBuilder.SplitCommandLineResourceInfo ri
                 yield { Name=name
                         Location=ILResourceLocation.File(ILModuleRef.Create(name=file, hasMetadata=false, hash=Some (sha1HashBytes (FileSystem.OpenFileForReadShim(file).ReadAllBytes()))), 0)
                         Access=pub
                         CustomAttrsStored=storeILCustomAttrs emptyILCustomAttrs
                         MetadataIndex = NoMetadataIdx } ]

        let assemblyVersion =
            match tcConfig.version with
            | VersionNone -> assemVerFromAttrib
            | _ -> Some tcVersion

        let findAttribute name =
            AttributeHelpers.TryFindStringAttribute tcGlobals name topAttrs.assemblyAttrs

        //NOTE: the culture string can be turned into a number using this:
        //    sprintf "%04x" (CultureInfo.GetCultureInfo("en").KeyboardLayoutId )
        let assemblyVersionResources assemblyVersion =
            match assemblyVersion with
            | None -> []
            | Some assemblyVersion ->
                let FindAttribute key attrib =
                    match findAttribute attrib with
                    | Some text  -> [(key, text)]
                    | _ -> []

                let fileVersionInfo = fileVersion findAttribute assemblyVersion

                let productVersionString = productVersion findAttribute fileVersionInfo

                let stringFileInfo =
                     // 000004b0:
                     // Specifies an 8-digit hexadecimal number stored as a Unicode string. The
                     // four most significant digits represent the language identifier. The four least
                     // significant digits represent the code page for which the data is formatted.
                     // Each Microsoft Standard Language identifier contains two parts: the low-order 10 bits
                     // specify the major language, and the high-order 6 bits specify the sublanguage.
                     // For a table of valid identifiers see Language Identifiers.                                           //
                     // see e.g. http://msdn.microsoft.com/en-us/library/aa912040.aspx 0000 is neutral and 04b0(hex)=1252(dec) is the code page.
                      [ ("000004b0", [ yield ("Assembly Version", (sprintf "%d.%d.%d.%d" assemblyVersion.Major assemblyVersion.Minor assemblyVersion.Build assemblyVersion.Revision))
                                       yield ("FileVersion", (sprintf "%d.%d.%d.%d" fileVersionInfo.Major fileVersionInfo.Minor fileVersionInfo.Build fileVersionInfo.Revision))
                                       yield ("ProductVersion", productVersionString)
                                       match tcConfig.outputFile with
                                       | Some f -> yield ("OriginalFilename", Path.GetFileName f)
                                       | None -> ()
                                       yield! FindAttribute "Comments" "System.Reflection.AssemblyDescriptionAttribute"
                                       yield! FindAttribute "FileDescription" "System.Reflection.AssemblyTitleAttribute"
                                       yield! FindAttribute "ProductName" "System.Reflection.AssemblyProductAttribute"
                                       yield! FindAttribute "CompanyName" "System.Reflection.AssemblyCompanyAttribute"
                                       yield! FindAttribute "LegalCopyright" "System.Reflection.AssemblyCopyrightAttribute"
                                       yield! FindAttribute "LegalTrademarks" "System.Reflection.AssemblyTrademarkAttribute" ]) ]

                // These entries listed in the MSDN documentation as "standard" string entries are not yet settable

                // InternalName:
                //     The Value member identifies the file's internal name, if one exists. For example, this
                //     string could contain the module name for Windows dynamic-link libraries (DLLs), a virtual
                //     device name for Windows virtual devices, or a device name for MS-DOS device drivers.
                // OriginalFilename:
                //     The Value member identifies the original name of the file, not including a path. This
                //     enables an application to determine whether a file has been renamed by a user. This name
                //     may not be MS-DOS 8.3-format if the file is specific to a non-FAT file system.
                // PrivateBuild:
                //     The Value member describes by whom, where, and why this private version of the
                //     file was built. This string should only be present if the VS_FF_PRIVATEBUILD flag
                //     is set in the dwFileFlags member of the VS_FIXEDFILEINFO structure. For example,
                //     Value could be 'Built by OSCAR on \OSCAR2'.
                // SpecialBuild:
                //     The Value member describes how this version of the file differs from the normal version.
                //     This entry should only be present if the VS_FF_SPECIALBUILD flag is set in the dwFileFlags
                //     member of the VS_FIXEDFILEINFO structure. For example, Value could be 'Private build
                //     for Olivetti solving mouse problems on M250 and M250E computers'.

                // "If you use the Var structure to list the languages your application
                // or DLL supports instead of using multiple version resources,
                // use the Value member to contain an array of DWORD values indicating the
                // language and code page combinations supported by this file. The
                // low-order word of each DWORD must contain a Microsoft language identifier,
                // and the high-order word must contain the IBM code page number.
                // Either high-order or low-order word can be zero, indicating that
                // the file is language or code page independent. If the Var structure is
                // omitted, the file will be interpreted as both language and code page independent. "
                let varFileInfo = [ (0x0, 0x04b0)  ]

                let fixedFileInfo =
                    let dwFileFlagsMask = 0x3f // REVIEW: HARDWIRED
                    let dwFileFlags = 0x00 // REVIEW: HARDWIRED
                    let dwFileOS = 0x04 // REVIEW: HARDWIRED
                    let dwFileType = 0x01 // REVIEW: HARDWIRED
                    let dwFileSubtype = 0x00 // REVIEW: HARDWIRED
                    let lwFileDate = 0x00L // REVIEW: HARDWIRED
                    (fileVersionInfo, productVersionString |> productVersionToILVersionInfo, dwFileFlagsMask, dwFileFlags, dwFileOS, dwFileType, dwFileSubtype, lwFileDate)

                let vsVersionInfoResource =
                    VersionResourceFormat.VS_VERSION_INFO_RESOURCE(fixedFileInfo, stringFileInfo, varFileInfo)

                let resource =
                    [| yield! ResFileFormat.ResFileHeader()
                       yield! vsVersionInfoResource |]

                [ resource ]

        // a user cannot specify both win32res and win32manifest
        if not(tcConfig.win32manifest = "") && not(tcConfig.win32res = "") then
            error(Error(FSComp.SR.fscTwoResourceManifests(), rangeCmdArgs))

        let win32Manifest =
            // use custom manifest if provided
            if not(tcConfig.win32manifest = "") then tcConfig.win32manifest

            // don't embed a manifest if target is not an exe, if manifest is specifically excluded, if another native resource is being included, or if running on mono
            elif not(tcConfig.target.IsExe) || not(tcConfig.includewin32manifest) || not(tcConfig.win32res = "") || runningOnMono then ""
            // otherwise, include the default manifest
            else
                let path = Path.Combine(System.AppContext.BaseDirectory, @"default.win32manifest")
                if FileSystem.FileExistsShim(path) then path
                else Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), @"default.win32manifest")

        let nativeResources =
            [ for av in assemblyVersionResources assemblyVersion do
                  yield ILNativeResource.Out av
              if not(tcConfig.win32res = "") then
                  yield ILNativeResource.Out (FileSystem.OpenFileForReadShim(tcConfig.win32res).ReadAllBytes())
              if tcConfig.includewin32manifest && not(win32Manifest = "") && not runningOnMono then
                  yield  ILNativeResource.Out [| yield! ResFileFormat.ResFileHeader()
                                                 yield! (ManifestResourceFormat.VS_MANIFEST_RESOURCE((FileSystem.OpenFileForReadShim(win32Manifest).ReadAllBytes()), tcConfig.target = CompilerTarget.Dll)) |]
              if tcConfig.win32res = "" && tcConfig.win32icon <> "" && tcConfig.target <> CompilerTarget.Dll then
                  use ms = new MemoryStream()
                  Win32ResourceConversions.AppendIconToResourceStream(ms, FileSystem.OpenFileForWriteShim(tcConfig.win32icon))
                  yield ILNativeResource.Out [| yield! ResFileFormat.ResFileHeader()
                                                yield! ms.ToArray() |] ]

        // Add attributes, version number, resources etc.
        {mainModule with
              StackReserveSize = tcConfig.stackReserveSize
              Name = (if tcConfig.target = CompilerTarget.Module then FileSystemUtils.fileNameOfPath outfile else mainModule.Name)
              SubSystemFlags = (if tcConfig.target = CompilerTarget.WinExe then 2 else 3)
              Resources= resources
              ImageBase = (match tcConfig.baseAddress with None -> 0x00400000l | Some b -> b)
              IsDLL=(tcConfig.target = CompilerTarget.Dll || tcConfig.target=CompilerTarget.Module)
              Platform = tcConfig.platform
              Is32Bit=(match tcConfig.platform with Some X86 -> true | _ -> false)
              Is64Bit=(match tcConfig.platform with Some AMD64 | Some IA64 -> true | _ -> false)
              Is32BitPreferred = if tcConfig.prefer32Bit && not tcConfig.target.IsExe then (error(Error(FSComp.SR.invalidPlatformTarget(), rangeCmdArgs))) else tcConfig.prefer32Bit
              CustomAttrsStored=
                  storeILCustomAttrs
                    (mkILCustomAttrs
                      [ if tcConfig.target = CompilerTarget.Module then
                           yield! sigDataAttributes
                        yield! codegenResults.ilNetModuleAttrs ])
              NativeResources=nativeResources
              Manifest = manifest }
