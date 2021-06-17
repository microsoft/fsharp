#if INTERACTIVE
#r "../../artifacts/bin/fcs/net461/FSharp.Compiler.Service.dll" // note, build FSharp.Compiler.Service.Tests.fsproj to generate this, this DLL has a public API so can be used from F# Interactive
#r "../../artifacts/bin/fcs/net461/nunit.framework.dll"
#load "FsUnit.fs"
#load "Common.fs"
#else
module Tests.Service.ScriptOptions
#endif

open NUnit.Framework
open System.IO
open FSharp.Compiler.Service.Tests.Common
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.IO
open FSharp.Compiler.Text.Range
open FSharp.Compiler.Text

// Add additional imports/constructs into this script text to verify that common scenarios
// for FCS script typechecking can be supported
let scriptSource = """
open System
let pi = Math.PI
"""

[<TestCase(true, false, [| "--targetprofile:mscorlib" |])>]
[<TestCase(false, true, [| "--targetprofile:netcore" |])>]
[<Test>]
let ``can generate options for different frameworks regardless of execution environment``(assumeNetFx, useSdk, flags) =
    let path = Path.GetTempPath()
    let file = Path.GetTempFileName()
    let tempFile = Path.Combine(path, file)
    let (_, errors) =
        checker.GetProjectOptionsFromScript(tempFile, SourceText.ofString scriptSource, assumeDotNetFramework = assumeNetFx, useSdkRefs = useSdk, otherFlags = flags)
        |> Async.RunSynchronously
    match errors with
    | [] -> ()
    | errors -> failwithf "Error while parsing script with assumeDotNetFramework:%b, useSdkRefs:%b, and otherFlags:%A:\n%A" assumeNetFx useSdk flags errors

[<TestCase(true, false, [| "--targetprofile:mscorlib" |])>]
[<TestCase(false, true, [| "--targetprofile:netcore" |])>]
[<Test>]
let ``all default assembly references are system assemblies``(assumeNetFx, useSdkRefs, flags) =
    let tempFile = Path.GetTempFileName() + ".fsx"
    let (options, errors) =
        checker.GetProjectOptionsFromScript(tempFile, SourceText.ofString scriptSource, assumeDotNetFramework = assumeNetFx, useSdkRefs = useSdkRefs, otherFlags = flags)
        |> Async.RunSynchronously
    match errors with
    | [] -> ()
    | errors -> failwithf "Error while parsing script with assumeNetFx:%b, useSdkRefs:%b, and otherFlags:%A:\n%A" assumeNetFx useSdkRefs flags errors
    for r in options.OtherOptions do
        if r.StartsWith("-r:") then
            let ref = Path.GetFullPath(r.[3..])
            let baseName = Path.GetFileNameWithoutExtension(ref)
            let projectDir = System.Environment.CurrentDirectory
            if not (FSharp.Compiler.FxResolver(assumeNetFx, projectDir, rangeForErrors=range0, useSdkRefs=useSdkRefs, isInteractive=false, sdkDirOverride=None).GetSystemAssemblies().Contains(baseName)) then
                printfn "Failing, printing options from GetProjectOptionsFromScript..."
                for opt in options.OtherOptions do
                    printfn "option: %s" opt
                failwithf "expected FSharp.Compiler.DotNetFrameworkDependencies.systemAssemblies to contain '%s' because '%s' is a default reference for a script, (assumeNetFx, useSdk, flags) = %A" baseName ref (assumeNetFx, useSdkRefs, flags)

// This test atempts to use a bad SDK number 666.666.666
//
// It's disabled because on CI server the SDK is still being resolved to 5.0.101 by `dotnet --version`.
//
// This must be because of some setting in the CI build scripts - e.g. an environment variable
// that allows SDK resolution to be overriden. I've tried to track this down by looking through
// https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet resolution rules
// and the F# and CI settings but can't find the setting that is causing this.
//
// Because of this the test has been manually verified by running locally.
//[<Test>]
let ``sdk dir with dodgy global json gives warning``() =
    let tempFile = Path.GetTempFileName() + ".fsx"
    let tempPath = Path.GetDirectoryName(tempFile)
    let globalJsonPath = Path.Combine(tempPath, "global.json")
    FileSystem.OpenFileForWriteShim(globalJsonPath).Write("""{ "sdk": { "version": "666.666.666" } }""")
    let (options, errors) =
        checker.GetProjectOptionsFromScript(tempFile, SourceText.ofString scriptSource, assumeDotNetFramework = false, useSdkRefs = true, otherFlags = [| |])
        |> Async.RunSynchronously
    FileSystem.FileDeleteShim(globalJsonPath)
    match errors with
    | [] ->
        printfn "Failure!"
        printfn "tempPath = %A" tempPath
        printfn "options = %A" options
        let fxResolver = FSharp.Compiler.FxResolver(false, tempPath, rangeForErrors=range0, useSdkRefs=true, isInteractive=false, sdkDirOverride=None)
        let result = fxResolver.TryGetDesiredDotNetSdkVersionForDirectory()
        printfn "sdkVersion = %A" result

        printfn "options = %A" options
        failwith "Expected error while parsing script"
    | errors ->
       for error in errors do
           // {C:\Users\Administrator\AppData\Local\Temp\tmp6F0F.tmp.fsx (0,1)-(0,1) The .NET SDK for this script could not be determined. If the script is in a directory using a 'global.json' ensure the relevant .NET SDK is installed. The output from 'C:\Program Files\dotnet\dotnet.exe --version' in the script directory was: '        2.1.300 [C:\Program Files\dotnet\sdk]
           Assert.AreEqual(3384, error.ErrorNumber)
           Assert.AreEqual(tempFile, error.FileName)
