// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module TestFramework

open System
open System.IO
open System.Reflection
open System.Diagnostics
open Scripting
open NUnit.Framework
open FSharp.Compiler.IO

let inline getTestsDirectory src dir = src ++ dir

// Temporary directory is TempPath + "/FSharp.Test.Utilities/" date ("yyy-MM-dd")
// Throws exception if it Fails
let tryCreateTemporaryDirectory () =
    let path = Path.GetTempPath()
    let now = DateTime.Now.ToString("yyyy-MM-dd")
    let directory = Path.Combine(path, "FSharp.Test.Utilities", now)
    Directory.CreateDirectory(directory).FullName

// Create a temporaryFileName -- newGuid is random --- there is no point validating the file alread exists because: threading and Path.ChangeExtension() is commonly used after this API
let tryCreateTemporaryFileName () =
    let directory = tryCreateTemporaryDirectory ()
    let fileName = ("Temp-" + Guid.NewGuid().ToString() + ".tmp").Replace('-', '_')
    let filePath = Path.Combine(directory, fileName)
    filePath

[<RequireQualifiedAccess>]
module Commands =

    let gate = obj()

    // Execute the process pathToExe passing the arguments: arguments with the working directory: workingDir timeout after timeout milliseconds -1 = wait forever
    // returns exit code, stdio and stderr as string arrays
    let executeProcess pathToExe arguments workingDir timeout =
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
                    raise (new TimeoutException(sprintf "Timeout executing command '%s' '%s'" (psi.FileName) (psi.Arguments)))
                else
                    p.WaitForExit()
    #if DEBUG
            let workingDir' =
                if workingDir = ""
                then
                    // Assign working dir to prevent default to C:\Windows\System32
                    let executionLocation = Assembly.GetExecutingAssembly().Location
                    Path.GetDirectoryName executionLocation
                else
                    workingDir

            lock gate (fun () ->
                File.WriteAllLines(Path.Combine(workingDir', "StandardOutput.txt"), outputList)
                File.WriteAllLines(Path.Combine(workingDir', "StandardError.txt"), errorsList)
            )
    #endif
            p.ExitCode, outputList.ToArray(), errorsList.ToArray()
        | None -> -1, Array.empty, Array.empty

    let getfullpath workDir (path:string) =
        let rooted =
            if Path.IsPathRooted(path) then path
            else Path.Combine(workDir, path)
        rooted |> Path.GetFullPath

    let fileExists workDir path =
        if path |> getfullpath workDir |> FileSystem.FileExistsShim then Some path else None

    let directoryExists workDir path =
        if path |> getfullpath workDir |> Directory.Exists then Some path else None

    let copy_y workDir source dest =
        log "copy /y %s %s" source dest
        File.Copy( source |> getfullpath workDir, dest |> getfullpath workDir, true)
        CmdResult.Success

    let mkdir_p workDir dir =
        log "mkdir %s" dir
        Directory.CreateDirectory ( Path.Combine(workDir, dir) ) |> ignore

    let rm dir path =
        let p = path |> getfullpath dir
        if FileSystem.FileExistsShim(p) then
            (log "rm %s" p) |> ignore
            File.Delete(p)
        else
            (log "not found: %s p") |> ignore

    let rmdir dir path =
        let p = path |> getfullpath dir
        if Directory.Exists(p) then
            (log "rmdir /sy %s" p) |> ignore
            Directory.Delete(p, true)
        else
            (log "not found: %s p") |> ignore

    let pathAddBackslash (p: FilePath) =
        if String.IsNullOrWhiteSpace (p) then p
        else
            p.TrimEnd ([| Path.DirectorySeparatorChar; Path.AltDirectorySeparatorChar |])
            + Path.DirectorySeparatorChar.ToString()

    let echoAppendToFile workDir text p =
        log "echo %s> %s" text p
        let dest = p |> getfullpath workDir in File.AppendAllText(dest, text + Environment.NewLine)

    let appendToFile workDir source p =
        log "type %s >> %s" source p
        let from = source |> getfullpath workDir
        let dest = p |> getfullpath workDir
        let contents = File.ReadAllText(from)
        File.AppendAllText(dest, contents)

    let fsc workDir exec (dotNetExe: FilePath) (fscExe: FilePath) flags srcFiles =
        let args = (sprintf "%s %s" flags (srcFiles |> Seq.ofList |> String.concat " "))

#if FSC_IN_PROCESS
        // This is not yet complete
        printfn "Hosted Compiler:"
        printfn "workdir: %A\nargs: %A"workdir args
        let fscCompiler = FSharp.Compiler.Hosted.FscCompiler()
        let exitCode, _stdin, _stdout = FSharp.Compiler.Hosted.CompilerHelpers.fscCompile workDir (FSharp.Compiler.Hosted.CompilerHelpers.parseCommandLine args)

        match exitCode with
        | 0 -> CmdResult.Success
        | err ->
            let msg = sprintf "Error running command '%s' with args '%s' in directory '%s'" fscExe args workDir
            CmdResult.ErrorLevel (msg, err)
#else
        ignore workDir
#if NETCOREAPP
        exec dotNetExe (fscExe + " " + args)
#else
        ignore dotNetExe
        printfn "fscExe: %A" fscExe
        printfn "args: %A" args
        exec fscExe args
#endif
#endif

    let csc exec cscExe flags srcFiles =
        exec cscExe (sprintf "%s %s  /reference:netstandard.dll" flags (srcFiles |> Seq.ofList |> String.concat " "))

    let vbc exec vbcExe flags srcFiles =
        exec vbcExe (sprintf "%s %s  /reference:netstandard.dll" flags (srcFiles |> Seq.ofList |> String.concat " "))

    let fsi exec fsiExe flags sources =
        exec fsiExe (sprintf "%s %s"  flags (sources |> Seq.ofList |> String.concat " "))

    let internal quotepath (p: FilePath) =
        let quote = '"'.ToString()
        if p.Contains(" ") then (sprintf "%s%s%s" quote p quote) else p

    let ildasm exec ildasmExe flags assembly =
        exec ildasmExe (sprintf "%s %s" flags (quotepath assembly))

    let ilasm exec ilasmExe flags assembly =
        exec ilasmExe (sprintf "%s %s" flags (quotepath assembly))

    let peverify exec peverifyExe flags path =
        exec peverifyExe (sprintf "%s %s" (quotepath path) flags)

    let createTempDir () =
        let path = tryCreateTemporaryFileName  ()
        File.Delete path
        Directory.CreateDirectory path |> ignore
        path

type TestConfig =
    { EnvironmentVariables : Map<string, string>
      CSC : string
      csc_flags : string
      VBC : string
      vbc_flags : string
      BUILD_CONFIG : string
      FSC : string
      fsc_flags : string
      FSCOREDLLPATH : string
      FSI : string
#if !NETCOREAPP
      FSIANYCPU : string
#endif
      FSI_FOR_SCRIPTS : string
      FSharpBuild : string
      FSharpCompilerInteractiveSettings : string
      fsi_flags : string
      ILDASM : string
      ILASM : string
      PEVERIFY : string
      Directory: string
      DotNetExe: string
      DotNetMultiLevelLookup: string
      DotNetRoot: string
      DefaultPlatform: string}

#if NETCOREAPP
open System.Runtime.InteropServices
#endif

let getOperatingSystem () =
#if NETCOREAPP
    let isPlatform p = RuntimeInformation.IsOSPlatform(p)
    if   isPlatform OSPlatform.Windows then "win"
    elif isPlatform OSPlatform.Linux   then "linux"
    elif isPlatform OSPlatform.OSX     then "osx"
    else                                    "unknown"
#else
    "win"
#endif

module DotnetPlatform =
    let Is64BitOperatingSystem envVars =
        match getOperatingSystem () with
        | "win" ->
            // On Windows PROCESSOR_ARCHITECTURE has the value AMD64 on 64 bit Intel Machines
            let value =
                let find s = envVars |> Map.tryFind s
                [| "PROCESSOR_ARCHITECTURE" |] |> Seq.tryPick (fun s -> find s) |> function None -> "" | Some x -> x
            value = "AMD64"
        | _ -> System.Environment.Is64BitOperatingSystem // As an alternative for netstandard1.4+: System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture

type FSLibPaths =
    { FSCOREDLLPATH : string }

let getPackagesDir () =
    match Environment.GetEnvironmentVariable("NUGET_PACKAGES") with
    | null ->
        let path = match  Environment.GetEnvironmentVariable("USERPROFILE") with
                   | null -> Environment.GetEnvironmentVariable("HOME")
                   | p -> p
        path ++ ".nuget" ++ "packages"
    | path -> path

let requireFile dir path =
    // Linux filesystems are (in most cases) case-sensitive.
    // However when nuget packages are installed to $HOME/.nuget/packages, it seems they are lowercased
    let fullPath = (dir ++ path)
    match Commands.fileExists __SOURCE_DIRECTORY__ fullPath with
    | Some _ -> fullPath
    | None ->
        let fullPathLower = (dir ++ path.ToLower())
        match Commands.fileExists __SOURCE_DIRECTORY__ fullPathLower with
        | Some _ -> fullPathLower
        | None -> failwith (sprintf "Couldn't find \"%s\" on the following paths: \"%s\", \"%s\". Running 'build test' once might solve this issue" path fullPath fullPathLower)

let config configurationName envVars =
    let SCRIPT_ROOT = __SOURCE_DIRECTORY__
    let fsharpCoreArchitecture = "netstandard2.0"
    let fsharpBuildArchitecture = "netstandard2.0"
    let fsharpCompilerInteractiveSettingsArchitecture = "netstandard2.0"
#if NET472
    let fscArchitecture = "net472"
    let fsiArchitecture = "net472"
    let peverifyArchitecture = "net472"
#else
    let fscArchitecture = "net5.0"
    let fsiArchitecture = "net5.0"
    let peverifyArchitecture = "net5.0"
#endif
    let repoRoot = SCRIPT_ROOT ++ ".." ++ ".."
    let artifactsPath = repoRoot ++ "artifacts"
    let artifactsBinPath = artifactsPath ++ "bin"
    let coreClrRuntimePackageVersion = "5.0.0-preview.7.20364.11"
    let csc_flags = "/nologo"
    let vbc_flags = "/nologo"
    let fsc_flags = "-r:System.Core.dll --nowarn:20 --define:COMPILED"
    let fsi_flags = "-r:System.Core.dll --nowarn:20 --define:INTERACTIVE --maxerrors:1 --abortonerror"
    let operatingSystem = getOperatingSystem ()
    let Is64BitOperatingSystem = DotnetPlatform.Is64BitOperatingSystem envVars
    let architectureMoniker = if Is64BitOperatingSystem then "x64" else "x86"
    let packagesDir = getPackagesDir ()
    let requirePackage = requireFile packagesDir
    let requireArtifact = requireFile artifactsBinPath
    let CSC = requirePackage ("Microsoft.Net.Compilers" ++ "2.7.0" ++ "tools" ++ "csc.exe")
    let VBC = requirePackage ("Microsoft.Net.Compilers" ++ "2.7.0" ++ "tools" ++ "vbc.exe")
    let ILDASM_EXE = if operatingSystem = "win" then "ildasm.exe" else "ildasm"
    let ILDASM = requirePackage (("runtime." + operatingSystem + "-" + architectureMoniker + ".Microsoft.NETCore.ILDAsm") ++ coreClrRuntimePackageVersion ++ "runtimes" ++ (operatingSystem + "-" + architectureMoniker) ++ "native" ++ ILDASM_EXE)
    let ILASM_EXE = if operatingSystem = "win" then "ilasm.exe" else "ilasm"
    let ILASM = requirePackage (("runtime." + operatingSystem + "-" + architectureMoniker + ".Microsoft.NETCore.ILAsm") ++ coreClrRuntimePackageVersion ++ "runtimes" ++ (operatingSystem + "-" + architectureMoniker) ++ "native" ++ ILASM_EXE)
    let PEVERIFY_EXE = if operatingSystem = "win" then "PEVerify.exe" elif operatingSystem = "osx" then "PEVerify.dll" else "PEVerify"
    let PEVERIFY = requireArtifact ("PEVerify" ++ configurationName ++ peverifyArchitecture ++ PEVERIFY_EXE)
//    let FSI_FOR_SCRIPTS = artifactsBinPath ++ "fsi" ++ configurationName ++ fsiArchitecture ++ "fsi.exe"
    let FSharpBuild = requireArtifact ("FSharp.Build" ++ configurationName ++ fsharpBuildArchitecture ++ "FSharp.Build.dll")
    let FSharpCompilerInteractiveSettings = requireArtifact ("FSharp.Compiler.Interactive.Settings" ++ configurationName ++ fsharpCompilerInteractiveSettingsArchitecture ++ "FSharp.Compiler.Interactive.Settings.dll")

    let dotNetExe =
        // first look for {repoRoot}\.dotnet\dotnet.exe, otherwise fallback to %PATH%
        let DOTNET_EXE = if operatingSystem = "win" then "dotnet.exe" else "dotnet"
        let repoLocalDotnetPath = repoRoot ++ ".dotnet" ++ DOTNET_EXE
        if FileSystem.FileExistsShim(repoLocalDotnetPath) then repoLocalDotnetPath
        else DOTNET_EXE

#if !NETCOREAPP
    let FSI_PATH = ("fsi" ++ configurationName ++ fsiArchitecture ++ "fsi.exe")
#else
    let FSI_PATH = ("fsi" ++ configurationName ++ fsiArchitecture ++ "fsi.dll")
#endif
    let FSI_FOR_SCRIPTS = requireArtifact FSI_PATH
    let FSI = requireArtifact FSI_PATH
#if !NETCOREAPP
    let FSIANYCPU = requireArtifact ("fsiAnyCpu" ++ configurationName ++ "net472" ++ "fsiAnyCpu.exe")
    let FSC = requireArtifact ("fsc" ++ configurationName ++ fscArchitecture ++ "fsc.exe")
#else
    let FSC = requireArtifact ("fsc" ++ configurationName ++ fscArchitecture ++ "fsc.dll")
#endif
    let FSCOREDLLPATH = requireArtifact ("FSharp.Core" ++ configurationName ++ fsharpCoreArchitecture ++ "FSharp.Core.dll")

    let defaultPlatform =
        match Is64BitOperatingSystem with
//        | PlatformID.MacOSX, true -> "osx.10.10-x64"
//        | PlatformID.Unix,true -> "ubuntu.14.04-x64"
        | true -> "win7-x64"
        | false -> "win7-x86"

    { EnvironmentVariables = envVars
      FSCOREDLLPATH = FSCOREDLLPATH
      ILDASM = ILDASM
      ILASM = ILASM
      PEVERIFY = PEVERIFY
      VBC = VBC
      CSC = CSC
      BUILD_CONFIG = configurationName
      FSC = FSC
      FSI = FSI
#if !NETCOREAPP
      FSIANYCPU = FSIANYCPU
#endif
      FSI_FOR_SCRIPTS = FSI_FOR_SCRIPTS
      FSharpBuild = FSharpBuild
      FSharpCompilerInteractiveSettings = FSharpCompilerInteractiveSettings
      csc_flags = csc_flags
      fsc_flags = fsc_flags
      fsi_flags = fsi_flags
      vbc_flags = vbc_flags
      Directory=""
      DotNetExe = dotNetExe
      DotNetMultiLevelLookup = System.Environment.GetEnvironmentVariable "DOTNET_MULTILEVEL_LOOKUP"
      DotNetRoot = System.Environment.GetEnvironmentVariable "DOTNET_ROOT"
      DefaultPlatform = defaultPlatform }

let logConfig (cfg: TestConfig) =
    log "---------------------------------------------------------------"
    log "Executables"
    log ""
    log "CSC                      = %s" cfg.CSC
    log "BUILD_CONFIG             = %s" cfg.BUILD_CONFIG
    log "csc_flags                = %s" cfg.csc_flags
    log "FSC                      = %s" cfg.FSC
    log "fsc_flags                = %s" cfg.fsc_flags
    log "FSCOREDLLPATH            = %s" cfg.FSCOREDLLPATH
    log "FSI                      = %s" cfg.FSI
#if NETCOREAPP
    log "DotNetExe                =%s" cfg.DotNetExe
    log "DOTNET_MULTILEVEL_LOOKUP = %s" cfg.DotNetMultiLevelLookup
    log "DOTNET_ROOT              = %s" cfg.DotNetRoot
#else
    log "FSIANYCPU                = %s" cfg.FSIANYCPU
#endif
    log "FSI_FOR_SCRIPTS          = %s" cfg.FSI_FOR_SCRIPTS
    log "fsi_flags                = %s" cfg.fsi_flags
    log "ILDASM                   = %s" cfg.ILDASM
    log "PEVERIFY                 = %s" cfg.PEVERIFY
    log "---------------------------------------------------------------"

let checkResult result =
    match result with
    | CmdResult.ErrorLevel (msg1, err) -> Assert.Fail (sprintf "%s. ERRORLEVEL %d" msg1 err)
    | CmdResult.Success -> ()

let checkErrorLevel1 result =
    match result with
    | CmdResult.ErrorLevel (_,1) -> ()
    | CmdResult.Success | CmdResult.ErrorLevel _ -> Assert.Fail (sprintf "Command passed unexpectedly")

let envVars () =
    System.Environment.GetEnvironmentVariables ()
    |> Seq.cast<System.Collections.DictionaryEntry>
    |> Seq.map (fun d -> d.Key :?> string, d.Value :?> string)
    |> Map.ofSeq

let initializeSuite () =

#if DEBUG
    let configurationName = "Debug"
#else
    let configurationName = "Release"
#endif
    let env = envVars ()

    let cfg =
        let c = config configurationName env
        let usedEnvVars = c.EnvironmentVariables  |> Map.add "FSC" c.FSC
        { c with EnvironmentVariables = usedEnvVars }

    logConfig cfg

    cfg


let suiteHelpers = lazy (initializeSuite ())

[<AttributeUsage(AttributeTargets.Assembly)>]
type public InitializeSuiteAttribute () =
    inherit TestActionAttribute()

    override x.BeforeTest details =
        try
            if details.IsSuite
            then suiteHelpers.Force() |> ignore
        with
        | e -> raise (Exception("failed test suite initialization, debug code in InitializeSuiteAttribute", e))
    override x.AfterTest _details =
        ()

    override x.Targets = ActionTargets.Test ||| ActionTargets.Suite

let testConfig (testDir: string) =
    let cfg = suiteHelpers.Value
    if not (Path.IsPathRooted testDir) then
      failwith $"path is not rooted: {testDir}"
    let testDir = Path.GetFullPath testDir // mostly used to normalize / and \
    log "------------------ %s ---------------" testDir
    log "cd %s" testDir
    { cfg with Directory = testDir }

[<AllowNullLiteral>]
type FileGuard(path: string) =
    let remove path = if FileSystem.FileExistsShim(path) then Commands.rm (Path.GetTempPath()) path
    do if not (Path.IsPathRooted(path)) then failwithf "path '%s' must be absolute" path
    do remove path
    member x.Path = path
    member x.Exists = x.Path |> FileSystem.FileExistsShim
    member x.CheckExists() =
        if not x.Exists then
             failwith (sprintf "exit code 0 but %s file doesn't exists" (x.Path |> Path.GetFileName))

    interface IDisposable with
        member x.Dispose () = remove path


type RedirectToType =
    | Overwrite of FilePath
    | Append of FilePath

type RedirectTo =
    | Inherit
    | Output of RedirectToType
    | OutputAndError of RedirectToType * RedirectToType
    | OutputAndErrorToSameFile of RedirectToType
    | Error of RedirectToType

type RedirectFrom =
    | RedirectInput of FilePath

type RedirectInfo =
    { Output : RedirectTo
      Input : RedirectFrom option }


module Command =

    let logExec _dir path args redirect =
        let inF =
            function
            | None -> ""
            | Some(RedirectInput l) -> sprintf " <%s" l
        let redirectType = function Overwrite x -> sprintf ">%s" x | Append x -> sprintf ">>%s" x
        let outF =
            function
            | Inherit -> ""
            | Output r-> sprintf " 1%s" (redirectType r)
            | OutputAndError (r1, r2) -> sprintf " 1%s 2%s" (redirectType r1)  (redirectType r2)
            | OutputAndErrorToSameFile r -> sprintf " 1%s 2>1" (redirectType r)
            | Error r -> sprintf " 2%s" (redirectType r)
        sprintf "%s%s%s%s" path (match args with "" -> "" | x -> " " + x) (inF redirect.Input) (outF redirect.Output)

    let exec dir envVars (redirect:RedirectInfo) path args =

        let inputWriter sources (writer: StreamWriter) =
            let pipeFile name = async {
                let path = Commands.getfullpath dir name
                use reader = File.OpenRead (path)
                use ms = new MemoryStream()
                do! reader.CopyToAsync (ms) |> (Async.AwaitIAsyncResult >> Async.Ignore)
                ms.Position <- 0L
                try
                    do! ms.CopyToAsync(writer.BaseStream) |> (Async.AwaitIAsyncResult >> Async.Ignore)
                    do! writer.FlushAsync() |> (Async.AwaitIAsyncResult >> Async.Ignore)
                with
                | :? System.IO.IOException -> //input closed is ok if process is closed
                    ()
                }
            sources |> pipeFile |> Async.RunSynchronously

        let inF fCont cmdArgs =
            match redirect.Input with
            | None -> fCont cmdArgs
            | Some(RedirectInput l) -> fCont { cmdArgs with RedirectInput = Some (inputWriter l) }

        let openWrite rt =
            let fullpath = Commands.getfullpath dir
            match rt with
            | Append p -> File.AppendText( p |> fullpath)
            | Overwrite p -> new StreamWriter(new FileStream(p |> fullpath, FileMode.Create))

        let outF fCont cmdArgs =
            match redirect.Output with
            | RedirectTo.Inherit ->
                use toLog = redirectToLog ()
                fCont { cmdArgs with RedirectOutput = Some (toLog.Post); RedirectError = Some (toLog.Post) }
            | Output r ->
                use writer = openWrite r
                use outFile = redirectTo writer
                use toLog = redirectToLog ()
                fCont { cmdArgs with RedirectOutput = Some (outFile.Post); RedirectError = Some (toLog.Post) }
            | OutputAndError (r1,r2) ->
                use writer1 = openWrite r1
                use writer2 = openWrite r2
                use outFile1 = redirectTo writer1
                use outFile2 = redirectTo writer2
                fCont { cmdArgs with RedirectOutput = Some (outFile1.Post); RedirectError = Some (outFile2.Post) }
            | OutputAndErrorToSameFile r ->
                use writer = openWrite r
                use outFile = redirectTo writer
                fCont { cmdArgs with RedirectOutput = Some (outFile.Post); RedirectError = Some (outFile.Post) }
            | Error r ->
                use writer = openWrite r
                use outFile = redirectTo writer
                use toLog = redirectToLog ()
                fCont { cmdArgs with RedirectOutput = Some (toLog.Post); RedirectError = Some (outFile.Post) }

        let exec cmdArgs =
            log "%s" (logExec dir path args redirect)
            Process.exec cmdArgs dir envVars path args

        { RedirectOutput = None; RedirectError = None; RedirectInput = None }
        |> (outF (inF exec))

let alwaysSuccess _ = ()

let execArgs = { Output = Inherit; Input = None; }
let execAppend cfg stdoutPath stderrPath p = Command.exec cfg.Directory cfg.EnvironmentVariables { execArgs with Output = OutputAndError(Append(stdoutPath), Append(stderrPath)) } p >> checkResult
let execAppendIgnoreExitCode cfg stdoutPath stderrPath p = Command.exec cfg.Directory cfg.EnvironmentVariables { execArgs with Output = OutputAndError(Append(stdoutPath), Append(stderrPath)) } p >> alwaysSuccess
let exec cfg p = Command.exec cfg.Directory cfg.EnvironmentVariables execArgs p >> checkResult
let execExpectFail cfg p = Command.exec cfg.Directory cfg.EnvironmentVariables execArgs p >> checkErrorLevel1
let execIn cfg workDir p = Command.exec workDir cfg.EnvironmentVariables execArgs p >> checkResult
let execBothToOutNoCheck cfg workDir outFile p = Command.exec workDir  cfg.EnvironmentVariables { execArgs with Output = OutputAndErrorToSameFile(Overwrite(outFile)) } p
let execBothToOut cfg workDir outFile p = execBothToOutNoCheck cfg workDir outFile p >> checkResult
let execBothToOutExpectFail cfg workDir outFile p = execBothToOutNoCheck cfg workDir outFile p >> checkErrorLevel1
let execAppendOutIgnoreExitCode cfg workDir outFile p = Command.exec workDir  cfg.EnvironmentVariables { execArgs with Output = Output(Append(outFile)) } p >> alwaysSuccess
let execAppendErrExpectFail cfg errPath p = Command.exec cfg.Directory cfg.EnvironmentVariables { execArgs with Output = Error(Overwrite(errPath)) } p >> checkErrorLevel1
let execStdin cfg l p = Command.exec cfg.Directory cfg.EnvironmentVariables { Output = Inherit; Input = Some(RedirectInput(l)) } p >> checkResult
let execStdinAppendBothIgnoreExitCode cfg stdoutPath stderrPath stdinPath p = Command.exec cfg.Directory cfg.EnvironmentVariables { Output = OutputAndError(Append(stdoutPath), Append(stderrPath)); Input = Some(RedirectInput(stdinPath)) } p >> alwaysSuccess
let fsc cfg arg = Printf.ksprintf (Commands.fsc cfg.Directory (exec cfg) cfg.DotNetExe cfg.FSC) arg
let fscIn cfg workDir arg = Printf.ksprintf (Commands.fsc workDir (execIn cfg workDir) cfg.DotNetExe  cfg.FSC) arg
let fscAppend cfg stdoutPath stderrPath arg = Printf.ksprintf (Commands.fsc cfg.Directory (execAppend cfg stdoutPath stderrPath) cfg.DotNetExe  cfg.FSC) arg
let fscAppendIgnoreExitCode cfg stdoutPath stderrPath arg = Printf.ksprintf (Commands.fsc cfg.Directory (execAppendIgnoreExitCode cfg stdoutPath stderrPath) cfg.DotNetExe  cfg.FSC) arg
let fscBothToOut cfg out arg = Printf.ksprintf (Commands.fsc cfg.Directory (execBothToOut cfg cfg.Directory out) cfg.DotNetExe  cfg.FSC) arg
let fscBothToOutExpectFail cfg out arg = Printf.ksprintf (Commands.fsc cfg.Directory (execBothToOutExpectFail cfg cfg.Directory out) cfg.DotNetExe  cfg.FSC) arg
let fscAppendErrExpectFail cfg errPath arg = Printf.ksprintf (Commands.fsc cfg.Directory (execAppendErrExpectFail cfg errPath) cfg.DotNetExe  cfg.FSC) arg
let csc cfg arg = Printf.ksprintf (Commands.csc (exec cfg) cfg.CSC) arg
let vbc cfg arg = Printf.ksprintf (Commands.vbc (exec cfg) cfg.VBC) arg
let ildasm cfg arg = Printf.ksprintf (Commands.ildasm (exec cfg) cfg.ILDASM) arg
let ilasm cfg arg = Printf.ksprintf (Commands.ilasm (exec cfg) cfg.ILASM) arg
let peverify cfg = Commands.peverify (exec cfg) cfg.PEVERIFY "/nologo"
let peverifyWithArgs cfg args = Commands.peverify (exec cfg) cfg.PEVERIFY args
let fsi cfg = Printf.ksprintf (Commands.fsi (exec cfg) cfg.FSI)
#if !NETCOREAPP
let fsiAnyCpu cfg = Printf.ksprintf (Commands.fsi (exec cfg) cfg.FSIANYCPU)
#endif
let fsi_script cfg = Printf.ksprintf (Commands.fsi (exec cfg) cfg.FSI_FOR_SCRIPTS)
let fsiExpectFail cfg = Printf.ksprintf (Commands.fsi (execExpectFail cfg) cfg.FSI)
let fsiAppendIgnoreExitCode cfg stdoutPath stderrPath = Printf.ksprintf (Commands.fsi (execAppendIgnoreExitCode cfg stdoutPath stderrPath) cfg.FSI)
let fileguard cfg = (Commands.getfullpath cfg.Directory) >> (fun x -> new FileGuard(x))
let getfullpath cfg = Commands.getfullpath cfg.Directory
let fileExists cfg = Commands.fileExists cfg.Directory >> Option.isSome
let fsiStdin cfg stdinPath = Printf.ksprintf (Commands.fsi (execStdin cfg stdinPath) cfg.FSI)
let fsiStdinAppendBothIgnoreExitCode cfg stdoutPath stderrPath stdinPath = Printf.ksprintf (Commands.fsi (execStdinAppendBothIgnoreExitCode cfg stdoutPath stderrPath stdinPath) cfg.FSI)
let rm cfg x = Commands.rm cfg.Directory x
let rmdir cfg x = Commands.rmdir cfg.Directory x
let mkdir cfg = Commands.mkdir_p cfg.Directory
let copy_y cfg f = Commands.copy_y cfg.Directory f >> checkResult
let copySystemValueTuple cfg = copy_y cfg (getDirectoryName(cfg.FSC) ++ "System.ValueTuple.dll") ("." ++ "System.ValueTuple.dll")

let diff normalize path1 path2 =
    let result = System.Text.StringBuilder()
    let append s = result.AppendLine s |> ignore
    let cwd = Directory.GetCurrentDirectory()

    if not <| FileSystem.FileExistsShim(path1) then
        // creating empty baseline file as this is likely someone initializing a new test
        File.WriteAllText(path1, String.Empty)
    if not <| FileSystem.FileExistsShim(path2) then failwithf "Invalid path %s" path2

    let lines1 = File.ReadAllLines(path1)
    let lines2 = File.ReadAllLines(path2)

    let minLines = min lines1.Length lines2.Length

    for i = 0 to (minLines - 1) do
        let normalizePath (line:string) =
            if normalize then
                let x = line.IndexOf(cwd, StringComparison.OrdinalIgnoreCase)
                if x >= 0 then line.Substring(x+cwd.Length) else line
            else line

        let line1 = normalizePath lines1.[i]
        let line2 = normalizePath lines2.[i]

        if line1 <> line2 then
            append <| sprintf "diff between [%s] and [%s]" path1 path2
            append <| sprintf "line %d" (i+1)
            append <| sprintf " - %s" line1
            append <| sprintf " + %s" line2

    if lines1.Length <> lines2.Length then
        append <| sprintf "diff between [%s] and [%s]" path1 path2
        append <| sprintf "diff at line %d" minLines
        lines1.[minLines .. (lines1.Length - 1)] |> Array.iter (append << sprintf "- %s")
        lines2.[minLines .. (lines2.Length - 1)] |> Array.iter (append << sprintf "+ %s")

    result.ToString()

let fsdiff cfg a b =
    let actualFile = System.IO.Path.Combine(cfg.Directory, a)
    let expectedFile = System.IO.Path.Combine(cfg.Directory, b)
    let errorText = System.IO.File.ReadAllText (System.IO.Path.Combine(cfg.Directory, a))

    let result = diff false expectedFile actualFile
    if result <> "" then
        log "%s" result
        log "New error file:"
        log "%s" errorText

    result

let requireENCulture () =
    match System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName with
    | "en" -> true
    | _ -> false
