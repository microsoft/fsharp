// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module FSharp.Compiler.CodeAnalysis.LegacyMSBuildReferenceResolver

    open System
    open System.IO
    open System.Reflection
    open Internal.Utilities.Library 
    open Microsoft.Build.Tasks
    open Microsoft.Build.Utilities
    open Microsoft.Build.Framework
    open FSharp.Compiler.IO

    // Reflection wrapper for properties
    type System.Object with
        member this.GetPropertyValue(propName) =
            this.GetType().GetProperty(propName, BindingFlags.Public).GetValue(this, null)

    /// Get the Reference Assemblies directory for the .NET Framework on Window.
    let DotNetFrameworkReferenceAssembliesRootDirectory = 
        // ProgramFilesX86 is correct for both x86 and x64 architectures 
        // (the reference assemblies are always in the 32-bit location, which is PF(x86) on an x64 machine)
        let PF = 
            match Environment.GetEnvironmentVariable("ProgramFiles(x86)") with
            | Null -> Environment.GetEnvironmentVariable("ProgramFiles")  // if PFx86 is null, then we are 32-bit and just get PF
            | NonNull s -> s 
        PF + @"\Reference Assemblies\Microsoft\Framework\.NETFramework"

    /// When targeting .NET 2.0-3.5 on Windows, we expand the {WindowsFramework} and {ReferenceAssemblies} paths manually
    let internal ReplaceVariablesForLegacyFxOnWindows(dirs: string list) =
        let windowsFramework = Environment.GetEnvironmentVariable("windir")+ @"\Microsoft.NET\Framework"
        let referenceAssemblies = DotNetFrameworkReferenceAssembliesRootDirectory
        dirs |> List.map(fun d -> d.Replace("{WindowsFramework}",windowsFramework).Replace("{ReferenceAssemblies}",referenceAssemblies))
    
    // ATTENTION!: the following code needs to be updated every time we are switching to the new MSBuild version because new .NET framework version was released
    // 1. List of frameworks
    // 2. DeriveTargetFrameworkDirectoriesFor45Plus
    // 3. HighestInstalledRefAssembliesOrDotNETFramework
    // 4. GetPathToDotNetFrameworkImlpementationAssemblies
    [<Literal>]    
    let private Net45 = "v4.5"

    [<Literal>]    
    let private Net451 = "v4.5.1"

    [<Literal>]    
    let private Net452 = "v4.5.2" // not available in Dev15 MSBuild version

    [<Literal>]    
    let private Net46 = "v4.6"

    [<Literal>]    
    let private Net461 = "v4.6.1"

    [<Literal>]    
    let private Net462 = "v4.6.2"

    [<Literal>]    
    let private Net47 = "v4.7"

    [<Literal>]    
    let private Net471 = "v4.7.1"

    [<Literal>]    
    let private Net472 = "v4.7.2"

    [<Literal>]    
    let private Net48 = "v4.8"

    let SupportedDesktopFrameworkVersions = [ Net48; Net472; Net471; Net47; Net462; Net461; Net46; Net452; Net451; Net45 ]

    /// Get the path to the .NET Framework implementation assemblies by using ToolLocationHelper.GetPathToDotNetFramework
    /// This is only used to specify the "last resort" path for assembly resolution.
    let GetPathToDotNetFrameworkImlpementationAssemblies(v) : string list =
        let v =
            match v with
            | Net45 ->  Some TargetDotNetFrameworkVersion.Version45
            | Net451 -> Some TargetDotNetFrameworkVersion.Version451
            | Net452 -> Some TargetDotNetFrameworkVersion.Version452
            | Net46 -> Some TargetDotNetFrameworkVersion.Version46
            | Net461 -> Some TargetDotNetFrameworkVersion.Version461
            | Net462 -> Some TargetDotNetFrameworkVersion.Version462
            | Net47 -> Some TargetDotNetFrameworkVersion.Version47
            | Net471 -> Some TargetDotNetFrameworkVersion.Version471
            | Net472 -> Some TargetDotNetFrameworkVersion.Version472
            | Net48 -> Some TargetDotNetFrameworkVersion.Version48
            | _ -> assert false; None
        match v with
        | Some v -> 
            match ToolLocationHelper.GetPathToDotNetFramework v with
            | Null -> []
            | NonNull x -> [x]
        | _ -> []

    let GetPathToDotNetFrameworkReferenceAssemblies(version) = 
#if NETSTANDARD
        ignore version
        let r : string list = []
        r
#else
        match Microsoft.Build.Utilities.ToolLocationHelper.GetPathToStandardLibraries(".NETFramework",version,"") with
        | Null | "" -> []
        | NonNull x -> [x]
#endif

    /// Use MSBuild to determine the version of the highest installed set of reference assemblies, failing that grab the highest installed framework version
    let HighestInstalledRefAssembliesOrDotNETFramework () =
        let getHighestInstalledDotNETFramework () =
            try
                if box (ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version48)) <> null then Net48
                elif box (ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version472)) <> null then Net472
                elif box (ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version471)) <> null then Net471
                elif box (ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version47)) <> null then Net47
                elif box (ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version462)) <> null then Net462
                elif box (ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version461)) <> null then Net461
                elif box (ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version461)) <> null then Net461
                elif box (ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version46)) <> null then Net46
                elif box (ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version452)) <> null then Net452
                elif box (ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version451)) <> null then Net451
                elif box (ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version45)) <> null then Net45
                else Net45 // version is 4.5 assumed since this code is running.
            with _ -> Net45

        // 1.   First look to see if we can find the highest installed set of dotnet reference assemblies, if yes then select that framework
        // 2.   Otherwise ask msbuild for the highestinstalled framework
        let checkFrameworkForReferenceAssemblies (dotNetVersion:string) =
            if not (String.IsNullOrEmpty(dotNetVersion)) then
                try
                    let v = if dotNetVersion.StartsWith("v") then dotNetVersion.Substring(1) else dotNetVersion
                    let frameworkName = new System.Runtime.Versioning.FrameworkName(".NETFramework", new Version(v))
                    match ToolLocationHelper.GetPathToReferenceAssemblies(frameworkName) |> Seq.tryHead with
                    | Some p -> if FileSystem.DirectoryExistsShim(p) then true else false
                    | None -> false
                with _ -> false
            else false
        match SupportedDesktopFrameworkVersions |> Seq.tryFind(fun v -> checkFrameworkForReferenceAssemblies v) with
        | Some v -> v
        | None -> getHighestInstalledDotNETFramework()

    /// Derive the target framework directories.
    let DeriveTargetFrameworkDirectories (targetFrameworkVersion:string, logMessage) =

        let targetFrameworkVersion =
            if not(targetFrameworkVersion.StartsWith("v",StringComparison.Ordinal)) then "v"+targetFrameworkVersion
            else targetFrameworkVersion

        let result = GetPathToDotNetFrameworkReferenceAssemblies(targetFrameworkVersion) |> Array.ofList
        logMessage (sprintf "Derived target framework directories for version %s are: %s" targetFrameworkVersion (String.Join(",", result)))
        result

    /// Describes the location where the reference was found, used only for debug and tooltip output
    type ResolvedFrom =
        | AssemblyFolders
        | AssemblyFoldersEx
        | TargetFrameworkDirectory
        | RawFileName
        | GlobalAssemblyCache
        | Path of string
        | Unknown
            
    /// Decode the ResolvedFrom code from MSBuild.
    let DecodeResolvedFrom(resolvedFrom:string) : ResolvedFrom = 
        match resolvedFrom with
        | "{RawFileName}" -> RawFileName
        | "{GAC}" -> GlobalAssemblyCache
        | "{TargetFrameworkDirectory}" -> TargetFrameworkDirectory
        | "{AssemblyFolders}" -> AssemblyFolders
        | r when r.Length >= 10 &&  "{Registry:" = r.Substring(0,10) -> AssemblyFoldersEx
        | r -> ResolvedFrom.Path r
        
    let TooltipForResolvedFrom(resolvedFrom, fusionName, redist) = 
      fun (originalReference,resolvedPath) -> 
        let originalReferenceName = originalReference

        let resolvedPath = // Don't show the resolved path if it is identical to what was referenced.
            if originalReferenceName = resolvedPath then String.Empty
            else resolvedPath

        let lineIfExists text =
            if String.IsNullOrEmpty text then ""
            else text.Trim(' ')+"\n"

        match resolvedFrom with 
        | AssemblyFolders ->
            lineIfExists resolvedPath
            + lineIfExists fusionName
#if CROSS_PLATFORM_COMPILER
            + "Found by AssemblyFolders registry key"
#else
            + FSComp.SR.assemblyResolutionFoundByAssemblyFoldersKey()
#endif
        | AssemblyFoldersEx -> 
            lineIfExists resolvedPath
            + lineIfExists fusionName
#if CROSS_PLATFORM_COMPILER
            + "Found by AssemblyFoldersEx registry key"
#else
            + FSComp.SR.assemblyResolutionFoundByAssemblyFoldersExKey()
#endif
        | TargetFrameworkDirectory -> 
            lineIfExists resolvedPath
            + lineIfExists fusionName
#if CROSS_PLATFORM_COMPILER
            + ".NET Framework"
#else
            + FSComp.SR.assemblyResolutionNetFramework()
#endif
        | Unknown ->
            // Unknown when resolved by plain directory search without help from MSBuild resolver.
            lineIfExists resolvedPath
            + lineIfExists fusionName
        | RawFileName -> 
            lineIfExists fusionName
        | GlobalAssemblyCache -> 
            lineIfExists fusionName
#if CROSS_PLATFORM_COMPILER
            + "Global Assembly Cache"
#else
            + lineIfExists (FSComp.SR.assemblyResolutionGAC())
#endif
            + lineIfExists redist
        | Path _ ->
            lineIfExists resolvedPath
            + lineIfExists fusionName  

    /// Perform assembly resolution by instantiating the ResolveAssembly task directly from the MSBuild SDK.
    let ResolveCore(resolutionEnvironment: LegacyResolutionEnvironment,
                    references:(string*(*baggage*)string)[], 
                    targetFrameworkVersion: string, 
                    targetFrameworkDirectories: string list,
                    targetProcessorArchitecture: string,                
                    fsharpCoreDir: string,
                    explicitIncludeDirs: string list,
                    implicitIncludeDir: string,
                    allowRawFileName: bool,
                    logMessage: (string -> unit), 
                    logDiagnostic: (bool -> string -> string -> unit)) =
                      
        let frameworkRegistryBase, assemblyFoldersSuffix, assemblyFoldersConditions = 
          "Software\Microsoft\.NetFramework", "AssemblyFoldersEx" , ""              
        if Array.isEmpty references then [| |] else

        let mutable backgroundException = false

        let protect f = 
            if not backgroundException then 
                try f() 
                with _ -> backgroundException <- true

        let engine = 
            { new IBuildEngine with 
              member _.BuildProjectFile(projectFileName, targetNames, globalProperties, targetOutputs) = true
              member _.LogCustomEvent(e) =  protect (fun () -> logMessage e.Message)
              member _.LogErrorEvent(e) =   protect (fun () -> logDiagnostic true e.Code e.Message)
              member _.LogMessageEvent(e) = protect (fun () -> logMessage e.Message)
              member _.LogWarningEvent(e) = protect (fun () -> logDiagnostic false e.Code e.Message)
              member _.ColumnNumberOfTaskNode with get() = 1 
              member _.LineNumberOfTaskNode with get() = 1 
              member _.ContinueOnError with get() = true 
              member _.ProjectFileOfTaskNode with get() = "" } 

        // Derive the target framework directory if none was supplied.
        let targetFrameworkDirectories =
            if targetFrameworkDirectories=[] then DeriveTargetFrameworkDirectories(targetFrameworkVersion, logMessage) 
            else targetFrameworkDirectories |> Array.ofList


        // Filter for null and zero length
        let references = references |> Array.filter(fst >> String.IsNullOrEmpty >> not) 

        // Determine the set of search paths for the resolution
        let searchPaths = 

            let explicitIncludeDirs = explicitIncludeDirs |> List.filter(String.IsNullOrEmpty >> not)

            let registry = sprintf "{Registry:%s,%s,%s%s}" frameworkRegistryBase targetFrameworkVersion assemblyFoldersSuffix assemblyFoldersConditions

            [|  // When compiling scripts using fsc.exe, for some reason we have always historically put TargetFrameworkDirectory first
                // It is unclear why.  This is the only place we look at the 'isdifference between ResolutionEnvironment.EditingOrCompilation and ResolutionEnvironment.EditingTime.
                match resolutionEnvironment with
                | LegacyResolutionEnvironment.EditingOrCompilation false -> yield "{TargetFrameworkDirectory}"
                | LegacyResolutionEnvironment.EditingOrCompilation true
                | LegacyResolutionEnvironment.CompilationAndEvaluation -> ()

                // Quick-resolve straight to filename first 
                if allowRawFileName then 
                    yield "{RawFileName}"
                yield! explicitIncludeDirs     // From -I, #I
                yield fsharpCoreDir    // Location of explicit reference to FSharp.Core, otherwise location of fsc.exe
                yield implicitIncludeDir   // Usually the project directory

                match resolutionEnvironment with
                | LegacyResolutionEnvironment.EditingOrCompilation true
                | LegacyResolutionEnvironment.CompilationAndEvaluation -> yield "{TargetFrameworkDirectory}"
                | LegacyResolutionEnvironment.EditingOrCompilation false -> ()

                yield registry
                yield "{AssemblyFolders}"
                yield "{GAC}"
                // use path to implementation assemblies as the last resort
                yield! GetPathToDotNetFrameworkImlpementationAssemblies targetFrameworkVersion 
             |]    
            
        let assemblies = 
            [| for (referenceName,baggage) in references -> 
               let item = new Microsoft.Build.Utilities.TaskItem(referenceName) :> ITaskItem
               item.SetMetadata("Baggage", baggage)
               item |]
        let rar = 
            ResolveAssemblyReference(BuildEngine=engine, TargetFrameworkDirectories=targetFrameworkDirectories,
                                     FindRelatedFiles=false, FindDependencies=false, FindSatellites=false, 
                                     FindSerializationAssemblies=false, Assemblies=assemblies, 
                                     SearchPaths=searchPaths, 
                                     AllowedAssemblyExtensions= [| ".dll" ; ".exe" |])
        rar.TargetProcessorArchitecture <- targetProcessorArchitecture
        let targetedRuntimeVersionValue = typeof<obj>.Assembly.ImageRuntimeVersion
#if ENABLE_MONO_SUPPORT
        // The properties TargetedRuntimeVersion and CopyLocalDependenciesWhenParentReferenceInGac 
        // are not available on Mono. So we only set them if available (to avoid a compile-time dependency). 
        if not runningOnMono then
            typeof<ResolveAssemblyReference>.InvokeMember("TargetedRuntimeVersion",(BindingFlags.Instance ||| BindingFlags.SetProperty ||| BindingFlags.Public),null,rar,[| box targetedRuntimeVersionValue |])  |> ignore 
            typeof<ResolveAssemblyReference>.InvokeMember("CopyLocalDependenciesWhenParentReferenceInGac",(BindingFlags.Instance ||| BindingFlags.SetProperty ||| BindingFlags.Public),null,rar,[| box true |])  |> ignore 
#else
        rar.TargetedRuntimeVersion <- targetedRuntimeVersionValue
        rar.CopyLocalDependenciesWhenParentReferenceInGac <- true
#endif
        
        let succeeded = rar.Execute()
        
        if not succeeded then 
            raise LegacyResolutionFailure

        let resolvedFiles = 
            [| for p in rar.ResolvedFiles -> 
                let resolvedFrom = DecodeResolvedFrom(p.GetMetadata("ResolvedFrom"))
                let fusionName = p.GetMetadata("FusionName")
                let redist = p.GetMetadata("Redist") 
                { itemSpec = p.ItemSpec
                  prepareToolTip = TooltipForResolvedFrom(resolvedFrom, fusionName, redist)
                  baggage = p.GetMetadata("Baggage") } |]

        resolvedFiles

    let getResolver () =
       { new ILegacyReferenceResolver with 
           member _.HighestInstalledNetFrameworkVersion() = HighestInstalledRefAssembliesOrDotNETFramework()
           member _.DotNetFrameworkReferenceAssembliesRootDirectory =  DotNetFrameworkReferenceAssembliesRootDirectory

           /// Perform the resolution on rooted and unrooted paths, and then combine the results.
           member _.Resolve(resolutionEnvironment, references, targetFrameworkVersion, targetFrameworkDirectories, targetProcessorArchitecture,                
                             fsharpCoreDir, explicitIncludeDirs, implicitIncludeDir, logMessage, logDiagnostic) =

                // The {RawFileName} target is 'dangerous', in the sense that is uses <c>Directory.GetCurrentDirectory()</c> to resolve unrooted file paths.
                // It is unreliable to use this mutable global state inside Visual Studio.  As a result, we partition all references into a "rooted" set
                // (which contains e.g. C:\MyDir\MyAssem.dll) and "unrooted" (everything else).  We only allow "rooted" to use {RawFileName}.  Note that
                // unrooted may still find 'local' assemblies by virtue of the fact that "implicitIncludeDir" is one of the places searched during 
                // assembly resolution.
                let references = 
                    [| for ((file,baggage) as data) in references -> 
                            // However, MSBuild will not resolve 'relative' paths, even when e.g. implicitIncludeDir is part of the search.  As a result,
                            // if we have an unrooted path+filename, we'll assume this is relative to the project directory and root it.
                            if FileSystem.IsPathRootedShim(file) then
                                data  // fine, e.g. "C:\Dir\foo.dll"
                            elif not(file.Contains("\\") || file.Contains("/")) then
                                data  // fine, e.g. "System.Transactions.dll"
                            else
                                // We have a 'relative path', e.g. "bin/Debug/foo.exe" or "..\Yadda\bar.dll"
                                // turn it into an absolute path based at implicitIncludeDir
                                (Path.Combine(implicitIncludeDir, file), baggage) |]

                let rooted, unrooted = references |> Array.partition (fst >> FileSystem.IsPathRootedShim)

                let rootedResults = 
                    ResolveCore
                       (resolutionEnvironment, rooted,  targetFrameworkVersion, 
                        targetFrameworkDirectories, targetProcessorArchitecture, 
                        fsharpCoreDir, explicitIncludeDirs, implicitIncludeDir, 
                        true, logMessage, logDiagnostic)

                let unrootedResults = 
                    ResolveCore
                       (resolutionEnvironment, unrooted,  targetFrameworkVersion, 
                        targetFrameworkDirectories, targetProcessorArchitecture, 
                        fsharpCoreDir, explicitIncludeDirs, implicitIncludeDir, 
                        false, logMessage, logDiagnostic)

                // now unify the two sets of results
                Array.concat [| rootedResults; unrootedResults |]
       } 
       |> LegacyReferenceResolver
