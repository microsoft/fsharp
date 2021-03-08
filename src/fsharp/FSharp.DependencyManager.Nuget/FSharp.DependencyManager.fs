// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.DependencyManager.Nuget

open System
open System.Collections.Concurrent
open System.Diagnostics
open System.IO
open FSharp.DependencyManager.Nuget
open FSharp.DependencyManager.Nuget.Utilities
open FSharp.DependencyManager.Nuget.ProjectFile
open FSDependencyManager

module FSharpDependencyManager =
    
    [<assembly: DependencyManagerAttribute()>]
    do ()

    let private concat (s:string) (v:string) : string =
        match String.IsNullOrEmpty(s), String.IsNullOrEmpty(v) with
        | false, false -> s + ";" + v
        | false, true -> s
        | true, false -> v
        | _  -> ""

    let validateAndFormatRestoreSources (sources:string) = [|
            let items = sources.Split(';')
            for item in items do
                let uri = new Uri(item)
                if uri.IsFile then
                    let directoryName = uri.LocalPath
                    if Directory.Exists(directoryName) then
                        yield sprintf """  <PropertyGroup Condition="Exists('%s')"><RestoreAdditionalProjectSources>$(RestoreAdditionalProjectSources);%s</RestoreAdditionalProjectSources></PropertyGroup>""" directoryName directoryName
                    else
                        raise (Exception(SR.sourceDirectoryDoesntExist(directoryName)))
                else
                    yield sprintf """  <PropertyGroup><RestoreAdditionalProjectSources>$(RestoreAdditionalProjectSources);%s</RestoreAdditionalProjectSources></PropertyGroup>""" uri.OriginalString 
        |]

    let formatPackageReference p =
        let { Include=inc; Version=ver; RestoreSources=src; Script=script } = p
        seq {
            match not (String.IsNullOrEmpty(inc)), not (String.IsNullOrEmpty(ver)), not (String.IsNullOrEmpty(script)) with
            | true, true, false  -> yield sprintf @"  <ItemGroup><PackageReference Include='%s' Version='%s' /></ItemGroup>" inc ver
            | true, true, true   -> yield sprintf @"  <ItemGroup><PackageReference Include='%s' Version='%s' Script='%s' /></ItemGroup>" inc ver script
            | true, false, false -> yield sprintf @"  <ItemGroup><PackageReference Include='%s' /></ItemGroup>" inc
            | true, false, true  -> yield sprintf @"  <ItemGroup><PackageReference Include='%s' Script='%s' /></ItemGroup>" inc script
            | _ -> ()
            match not (String.IsNullOrEmpty(src)) with
            | true -> yield! validateAndFormatRestoreSources src
            | _ -> ()
        }

    let parsePackageReferenceOption scriptExt (setBinLogPath: string option option -> unit) (setTimeout: int option -> unit) (line: string) =
        let validatePackageName package packageName =
            if String.Compare(packageName, package, StringComparison.OrdinalIgnoreCase) = 0 then
                raise (ArgumentException(SR.cantReferenceSystemPackage(packageName)))
        let rec parsePackageReferenceOption' (options: (string option * string option) list) (implicitArgumentCount: int) (packageReference: PackageReference option) =
            let current =
                match packageReference with
                | Some p -> p
                | None -> { Include = ""; Version = "*"; RestoreSources = ""; Script = "" }
            match options with
            | [] -> packageReference
            | opt :: rest ->
                let addInclude v =
                    validatePackageName v "mscorlib"
                    if scriptExt = fsxExt then
                        validatePackageName v "FSharp.Core"
                    validatePackageName v "System.ValueTuple"
                    validatePackageName v "NETStandard.Library"
                    validatePackageName v "Microsoft.NETFramework.ReferenceAssemblies"
                    Some { current with Include = v }
                let setVersion v = Some { current with Version = v }
                match opt with
                | Some "include", Some v -> addInclude v |> parsePackageReferenceOption' rest implicitArgumentCount
                | Some "include", None -> raise (ArgumentException(SR.requiresAValue("Include")))
                | Some "version", Some v -> setVersion v |> parsePackageReferenceOption' rest implicitArgumentCount
                | Some "version", None -> setVersion "*" |> parsePackageReferenceOption' rest implicitArgumentCount
                | Some "restoresources", Some v -> Some { current with RestoreSources = concat current.RestoreSources v } |> parsePackageReferenceOption' rest implicitArgumentCount
                | Some "restoresources", None -> raise (ArgumentException(SR.requiresAValue("RestoreSources")))
                | Some "script", Some v -> Some { current with Script = v } |> parsePackageReferenceOption' rest implicitArgumentCount
                | Some "timeout", None -> raise (ArgumentException(SR.missingTimeoutValue()))
                | Some "timeout", value ->
                    match value with
                    | Some v when v.GetType() = typeof<string> ->
                        let parsed, value = Int32.TryParse(v)
                        if parsed && value >= 0 then setTimeout (Some (Int32.Parse v))
                        elif v = "none" then setTimeout (Some -1)
                        else raise (ArgumentException(SR.invalidTimeoutValue(v)))
                    | _ -> setTimeout None // auto-generated logging location
                    parsePackageReferenceOption' rest implicitArgumentCount packageReference
                | Some "bl", value ->
                    match value with
                    | Some v when v.ToLowerInvariant() = "true" -> setBinLogPath (Some None)      // auto-generated logging location
                    | Some v when v.ToLowerInvariant() = "false" -> setBinLogPath None          // no logging
                    | Some path -> setBinLogPath (Some (Some (path))) // explicit logging location
                    | None ->
                        // parser shouldn't get here because unkeyed values follow a different path, but for the sake of completeness and keeping the compiler happy,
                        // this is fine
                        setBinLogPath (Some None) // auto-generated logging location
                    parsePackageReferenceOption' rest implicitArgumentCount packageReference
                | None, Some v ->
                    match v.ToLowerInvariant() with
                    | "bl" ->
                        // a bare 'bl' enables binary logging and is NOT interpreted as one of the positional arguments.  On the off chance that the user actually wants
                        // to reference a package named 'bl' they still have the 'Include=bl' syntax as a fallback.
                        setBinLogPath (Some None) // auto-generated logging location
                        parsePackageReferenceOption' rest implicitArgumentCount packageReference
                    | "timeout" ->
                        // bare timeout is invalid
                        raise (ArgumentException(SR.missingTimeoutValue()))
                    | _ ->
                        match implicitArgumentCount with
                        | 0 -> addInclude v
                        | 1 -> setVersion v
                        | _ -> raise (ArgumentException(SR.unableToApplyImplicitArgument(implicitArgumentCount + 1)))
                        |> parsePackageReferenceOption' rest (implicitArgumentCount + 1)
                | _ -> parsePackageReferenceOption' rest implicitArgumentCount packageReference
        let options = getOptions line
        parsePackageReferenceOption' options 0 None

    let parsePackageReference scriptExt (lines: string list) =
        let mutable binLogPath = None
        let mutable timeout = None
        lines
        |> List.choose (fun line -> parsePackageReferenceOption scriptExt (fun p -> binLogPath <- p) (fun t -> timeout <- t) line)
        |> List.distinct
        |> (fun l -> l, binLogPath, timeout)

    let parsePackageDirective scriptExt (lines: (string * string) list) =
        let mutable binLogPath = None
        let mutable timeout = None
        lines
        |> List.map(fun (directive, line) ->
            match directive with
            | "i" -> sprintf "RestoreSources=%s" line
            | _ -> line)
        |> List.choose (fun line -> parsePackageReferenceOption scriptExt (fun p -> binLogPath <- p) (fun t -> timeout <- t) line)
        |> List.distinct
        |> (fun l -> l, binLogPath, timeout)

/// The results of ResolveDependencies
type ResolveDependenciesResult (success: bool, stdOut: string array, stdError: string array, resolutions: string seq, sourceFiles: string seq, roots: string seq) =

    /// Succeded?
    member _.Success = success

    /// The resolution output log
    member _.StdOut = stdOut

    /// The resolution error log (* process stderror *)
    member _.StdError = stdError

    /// The resolution paths - the full paths to selected resolved dll's.
    /// In scripts this is equivalent to #r @"c:\somepath\to\packages\ResolvedPackage\1.1.1\lib\netstandard2.0\ResolvedAssembly.dll"
    member _.Resolutions = resolutions

    /// The source code file paths
    member _.SourceFiles = sourceFiles

    /// The roots to package directories
    ///     This points to the root of each located package.
    ///     The layout of the package manager will be package manager specific.
    ///     however, the dependency manager dll understands the nuget package layout
    ///     and so if the package contains folders similar to the nuget layout then
    ///     the dependency manager will be able to probe and resolve any native dependencies
    ///     required by the nuget package.
    ///
    /// This path is also equivalent to
    ///     #I @"c:\somepath\to\packages\ResolvedPackage\1.1.1\"
    member _.Roots = roots

[<DependencyManagerAttribute>] 
type FSharpDependencyManager (outputDirectory:string option) =

    let key = "nuget"
    let name = "MsBuild Nuget DependencyManager"
    let workingDirectory =
        let path = Path.Combine(Path.GetTempPath(), key, Process.GetCurrentProcess().Id.ToString() + "--"+ Guid.NewGuid().ToString())
        match outputDirectory with
        | None -> path
        | Some v -> Path.Combine(path, v)

    let generatedScripts = new ConcurrentDictionary<string,string>()

    let deleteScripts () =
        try
#if !Debug
            if Directory.Exists(workingDirectory) then
                Directory.Delete(workingDirectory, true)
#else
            ()
#endif
        with | _ -> ()

    let deleteAtExit =
        try
            if not (Directory.Exists(workingDirectory)) then
                Directory.CreateDirectory(workingDirectory) |> ignore
            true
        with | _ -> false

    let emitFile filename (body:string) =
        try
            // Create a file to write to
            use sw = File.CreateText(filename)
            sw.WriteLine(body)
        with | _ -> ()

    let prepareDependencyResolutionFiles (scriptExt: string, directiveLines: (string * string) seq, targetFrameworkMoniker: string, runtimeIdentifier: string, timeout: int): PackageBuildResolutionResult =
        let scriptExt =
            match scriptExt with
            | ".csx" -> csxExt
            | _ -> fsxExt

        let packageReferences, binLogPath, package_timeout =
            directiveLines
            |> List.ofSeq
            |> FSharpDependencyManager.parsePackageDirective scriptExt

        let packageReferenceLines =
            packageReferences
            |> List.map FSharpDependencyManager.formatPackageReference
            |> Seq.concat

        let packageReferenceText = String.Join(Environment.NewLine, packageReferenceLines)

        let projectPath = Path.Combine(workingDirectory, "Project.fsproj")

        let generateAndBuildProjectArtifacts =
            let writeFile path body =
                if not (generatedScripts.ContainsKey(body.GetHashCode().ToString())) then
                    emitFile path  body

            let generateProjBody =
                generateProjectBody.Replace("$(TARGETFRAMEWORK)", targetFrameworkMoniker)
                                   .Replace("$(RUNTIMEIDENTIFIER)", runtimeIdentifier)
                                   .Replace("$(PACKAGEREFERENCES)", packageReferenceText)
                                   .Replace("$(SCRIPTEXTENSION)", scriptExt)

            let timeout =
                match package_timeout with
                | Some _ -> package_timeout
                | None -> Some timeout
            writeFile projectPath generateProjBody
            buildProject projectPath binLogPath timeout

        generateAndBuildProjectArtifacts


    do if deleteAtExit then AppDomain.CurrentDomain.ProcessExit |> Event.add(fun _ -> deleteScripts () )

    member _.Name = name

    member _.Key = key

    member _.HelpMessages = [|
        sprintf """    #r "nuget:FSharp.Data, 3.1.2";;               // %s 'FSharp.Data' %s '3.1.2'""" (SR.loadNugetPackage()) (SR.version())
        sprintf """    #r "nuget:FSharp.Data";;                      // %s 'FSharp.Data' %s""" (SR.loadNugetPackage()) (SR.highestVersion())
        |]

    member this.ResolveDependencies(scriptDirectory: string, scriptName: string, scriptExt: string, packageManagerTextLines: (string * string) seq, targetFrameworkMoniker: string, runtimeIdentifier: string, timeout: int) : obj =
        ignore scriptName
        let poundRprefix  =
            match scriptExt with
            | ".csx" -> "#r \""
            | _ -> "#r @\""

        let generateAndBuildProjectArtifacts =
            let configIncludes = generateSourcesFromNugetConfigs scriptDirectory workingDirectory timeout
            let directiveLines = Seq.append packageManagerTextLines configIncludes
            let resolutionResult = prepareDependencyResolutionFiles (scriptExt, directiveLines, targetFrameworkMoniker, runtimeIdentifier, timeout)
            match resolutionResult.resolutionsFile with
            | Some file ->
                let resolutions = getResolutionsFromFile file
                let references = (findReferencesFromResolutions resolutions) |> Array.toSeq
                let scripts =
                    let generatedScriptPath = resolutionResult.projectPath + scriptExt
                    let generatedScriptBody =  makeScriptFromReferences references poundRprefix
                    emitFile generatedScriptPath generatedScriptBody
                    let loads = (findLoadsFromResolutions resolutions) |> Array.toList
                    List.concat [ [generatedScriptPath]; loads] |> List.toSeq
                let includes = (findIncludesFromResolutions resolutions) |> Array.toSeq

                ResolveDependenciesResult(resolutionResult.success, resolutionResult.stdOut, resolutionResult.stdErr, references, scripts, includes)

            | None ->
                let empty = Seq.empty<string>
                ResolveDependenciesResult(resolutionResult.success, resolutionResult.stdOut, resolutionResult.stdErr, empty, empty, empty)

        generateAndBuildProjectArtifacts :> obj
