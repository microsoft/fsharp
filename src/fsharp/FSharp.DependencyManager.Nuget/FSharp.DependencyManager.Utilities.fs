// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.
namespace FSharp.DependencyManager.Nuget

open System
open System.Diagnostics
open System.IO
open System.Reflection
open FSDependencyManager
open System.Runtime.InteropServices

[<AttributeUsage(AttributeTargets.Assembly ||| AttributeTargets.Class , AllowMultiple = false)>]
type DependencyManagerAttribute() = inherit System.Attribute()

/// The result of building the package resolution files.
type PackageBuildResolutionResult =
    { success: bool
      projectPath: string
      stdOut: string array
      stdErr: string array
      resolutionsFile: string option }

module internal Utilities =

    /// Return a string array delimited by commas
    /// Note that a quoted string is not going to be mangled into pieces.
    let trimChars = [| ' '; '\t'; '\''; '\"' |]

    let inline private isNotQuotedQuotation (text: string) n = n > 0 && text.[n-1] <> '\\'

    let getOptions text =
        let split (option:string) =
            let pos = option.IndexOf('=')
            let stringAsOpt text =
                if String.IsNullOrEmpty(text) then None
                else Some text
            let nameOpt =
                if pos <= 0 then None
                else stringAsOpt (option.Substring(0, pos).Trim(trimChars).ToLowerInvariant())
            let valueOpt =
                let valueText =
                    if pos < 0 then option
                    else if pos < option.Length then
                        option.Substring(pos + 1)
                    else ""
                stringAsOpt (valueText.Trim(trimChars))
            nameOpt,valueOpt

        let last = String.length text - 1
        let result = ResizeArray()
        let mutable insideSQ = false
        let mutable start = 0
        let isSeperator c = c = ','
        for i = 0 to last do
            match text.[i], insideSQ with
            | c, false when isSeperator c ->                        // split when seeing a separator
                result.Add(text.Substring(start, i - start))
                insideSQ <- false
                start <- i + 1
            | _, _ when i = last ->
                result.Add(text.Substring(start, i - start + 1))
            | c, true when isSeperator c ->                         // keep reading if a separator is inside quotation
                insideSQ <- true
            | '\'', _ when isNotQuotedQuotation text i ->           // open or close quotation
                insideSQ <- not insideSQ                            // keep reading
            | _ -> ()

        result
        |> List.ofSeq
        |> List.map (fun option -> split option)

    // Path to the directory containing the fsharp compilers
    let fsharpCompilerPath = Path.Combine(Path.GetDirectoryName(typeof<DependencyManagerAttribute>.GetTypeInfo().Assembly.Location), "Tools")

    // We are running on dotnet core if the executing mscorlib is System.Private.CoreLib
    let isRunningOnCoreClr = (typeof<obj>.Assembly).FullName.StartsWith("System.Private.CoreLib", StringComparison.InvariantCultureIgnoreCase)

    let isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)

    let dotnet =
        if isWindows then "dotnet.exe" else "dotnet"

    let sdks = "Sdks"

    let dotnetHostPath =
        // How to find dotnet.exe --- woe is me; probing rules make me sad.
        // Algorithm:
        // 1. Look for DOTNET_HOST_PATH environment variable
        //    this is the main user programable override .. provided by user to find a specific dotnet.exe
        // 2. Probe for are we part of an .NetSDK install
        //    In an sdk install we are always installed in:   sdk\3.0.100-rc2-014234\FSharp
        //    dotnet or dotnet.exe will be found in the directory that contains the sdk directory
        // 3. We are loaded in-process to some other application ... Eg. try .net
        //    See if the host is dotnet.exe ... from net5.0 on this is fairly unlikely
        // 4. If it's none of the above we are going to have to rely on the path containing the way to find dotnet.exe
        let fileExists pathToFile =
            try
                if File.Exists(pathToFile) then 
                    true
                else
                    false
            with | _ -> false
        // Use the path to search for dotnet.exe
        let probePathForDotnetHost () =
            let paths =
                let p = Environment.GetEnvironmentVariable("PATH")
                if not(isNull p) then p.Split(Path.PathSeparator) 
                else [||]
            paths |> Array.tryFind (fun f -> fileExists (Path.Combine(f, dotnet)))

        if isRunningOnCoreClr then
            match (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")) with
            // Value set externally
            | value when not (String.IsNullOrEmpty(value)) && fileExists value -> Some value
            | _ ->
                // Probe for netsdk install, dotnet. and dotnet.exe is a constant offset from the location of System.Int32
                let candidate =
                    let assemblyLocation = Path.GetDirectoryName(typeof<Int32>.GetTypeInfo().Assembly.Location)
                    Path.GetFullPath(Path.Combine(assemblyLocation, "..", "..", "..", dotnet))
                if fileExists candidate then
                    Some candidate
                else
                    let main = Process.GetCurrentProcess().MainModule
                    if main.ModuleName ="dotnet" then
                        Some main.FileName
                    else
                        Some dotnet
        elif isWindows then
            // Use the path to search for dotnet.exe
            match probePathForDotnetHost () with
            | Some f -> Some (Path.Combine(f, dotnet))
            | None ->
                // Not in paths so look for global install of dotnet sdk
                let pf = Environment.GetEnvironmentVariable("ProgramW6432")
                let pf = if String.IsNullOrEmpty(pf) then Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) else pf
                let candidate = Path.Combine(pf, "dotnet", dotnet)
                if fileExists candidate then
                    Some candidate
                else
                    // Can't find it --- give up
                    None
        else
            None

    let executeTool pathToExe arguments workingDir timeout =
        match pathToExe with
        | Some path ->
            let errorsList = ResizeArray()
            let outputList = ResizeArray()
            let mutable errorslock = obj
            let mutable outputlock = obj
            let outputDataReceived (message: string) =
                if not (isNull message) then
                    lock outputlock (fun () -> outputList.Add(message))

            let errorDataReceived (message: string) =
                if not (isNull message) then
                    lock errorslock (fun () -> errorsList.Add(message))

            let psi = ProcessStartInfo()
            psi.FileName <- path
            psi.WorkingDirectory <- workingDir
            psi.RedirectStandardOutput <- true
            psi.RedirectStandardError <- true
            psi.Arguments <- arguments
            psi.CreateNoWindow <- true
            psi.EnvironmentVariables.Remove("MSBuildSDKsPath")          // Host can sometimes add this, and it can break things
            psi.UseShellExecute <- false

            use p = new Process()
            p.StartInfo <- psi

            p.OutputDataReceived.Add(fun a -> outputDataReceived a.Data)
            p.ErrorDataReceived.Add(fun a ->  errorDataReceived a.Data)

            if p.Start() then
                p.BeginOutputReadLine()
                p.BeginErrorReadLine()
                if not(p.WaitForExit(timeout)) then
                    // Timed out resolving throw a diagnostic.
                    raise (new TimeoutException(SR.timedoutResolvingPackages(psi.FileName, psi.Arguments)))
                else
                    p.WaitForExit()
            p.ExitCode = 0, outputList.ToArray(), errorsList.ToArray()

        | None -> false, Array.empty, Array.empty

    let buildProject projectPath binLogPath timeout =
        let binLoggingArguments =
            match binLogPath with
            | Some(path) ->
                let path = match path with
                           | Some path -> path // specific file
                           | None -> Path.Combine(Path.GetDirectoryName(projectPath), "msbuild.binlog") // auto-generated file
                sprintf "/bl:\"%s\"" path
            | None -> ""

        let timeout =
            match timeout with
            | Some(timeout) -> timeout
            | None -> -1

        let arguments prefix =
            sprintf "%s -restore %s %c%s%c /nologo /t:InteractivePackageManagement" prefix binLoggingArguments '\"' projectPath '\"'

        let workingDir = Path.GetDirectoryName projectPath

        let success, stdOut, stdErr =
            executeTool dotnetHostPath (arguments "msbuild -v:quiet") workingDir timeout

#if DEBUG
        File.WriteAllLines(Path.Combine(workingDir, "build_StandardOutput.txt"), stdOut)
        File.WriteAllLines(Path.Combine(workingDir, "build_StandardError.txt"), stdErr)
#endif

        let outputFile = projectPath + ".resolvedReferences.paths"
        let resolutionsFile = if success && File.Exists(outputFile) then Some outputFile else None
        { success = success
          projectPath = projectPath
          stdOut = stdOut
          stdErr = stdErr
          resolutionsFile = resolutionsFile }

    let generateSourcesFromNugetConfigs scriptDirectory workingDir timeout =
        let success, stdOut, stdErr =
            executeTool dotnetHostPath "nuget list source --format short" scriptDirectory timeout
#if DEBUG
        File.WriteAllLines(Path.Combine(workingDir, "nuget_StandardOutput.txt"), stdOut)
        File.WriteAllLines(Path.Combine(workingDir, "nuget_StandardError.txt"), stdErr)
#else
        ignore workingDir
        ignore stdErr
#endif
        seq {
            if success then
                for source in stdOut do
                    // String returned by dotnet nuget list source --format short
                    // is formatted similar to:
                    // E https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json
                    // So strip off the flags
                    let pos = source.IndexOf(" ")
                    if pos >= 0 then yield ("i", source.Substring(pos).Trim())
        }
