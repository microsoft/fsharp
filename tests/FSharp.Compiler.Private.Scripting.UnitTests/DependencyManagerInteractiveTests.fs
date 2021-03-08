// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.Scripting.DependencyManager.UnitTests

open System
open System.IO
open System.Reflection
open System.Runtime.InteropServices
open System.Threading

open FSharp.Compiler.Interactive.Shell
open FSharp.Compiler.DependencyManager
open FSharp.Compiler.Diagnostics
open FSharp.Test.ScriptHelpers
open FSharp.DependencyManager.Nuget

open Internal.Utilities

open Xunit

module Native =
    [<DllImport("NoneExistentDll")>]
    extern int NoneSuch()

type scriptHost (?langVersion: LangVersion) = inherit FSharpScript(langVersion=defaultArg langVersion LangVersion.Preview)

type DependencyManagerInteractiveTests() =

    let getValue ((value: Result<FsiValue option, exn>), (errors: FSharpDiagnostic[])) =
        if errors.Length > 0 then
            failwith <| sprintf "Evaluation returned %d errors:\r\n\t%s" errors.Length (String.Join("\r\n\t", errors))
        match value with
        | Ok(value) -> value
        | Error ex -> raise ex

    let getErrors ((_value: Result<FsiValue option, exn>), (errors: FSharpDiagnostic[])) =
        errors

    let ignoreValue = getValue >> ignore

    [<Fact>]
    member _.``SmokeTest - #r nuget``() =
        let text = """
#r @"nuget:Newtonsoft.Json, Version=9.0.1"
0"""
        use script = new scriptHost()
        let opt = script.Eval(text) |> getValue
        let value = opt.Value
        Assert.Equal(typeof<int>, value.ReflectionType)
        Assert.Equal(0, value.ReflectionValue :?> int)

    [<Fact>]
    member _.``SmokeTest - #r nuget package not found``() =
        let text = """
#r @"nuget:System.Collections.Immutable.DoesNotExist, version=1.5.0"
0"""
        use script = new scriptHost()
        let opt, errors = script.Eval(text)
        Assert.Equal(errors.Length, 1)

(*
    [<Theory>]
    [<InlineData("""#r "#i "unknown:Astring" """, """ """)>]
    member _.``syntax produces error messages in FSharp 4.7``(code:string, message: string) =
        use script = new scriptHost()
        let errors = script.Eval(code) |> getErrors
        Assert.Contains(message, errors |> Array.map(fun e -> e.Message))
*)
    [<Fact>]
    member _.``Use Dependency Manager to resolve dependency FSharp.Data``() =

        let nativeProbingRoots () = Seq.empty<string>

        use dp = new DependencyProvider(NativeResolutionProbe(nativeProbingRoots))
        let reportError =
            let report errorType code message =
                match errorType with
                | ErrorReportType.Error -> printfn "PackageManagementError %d : %s" code message
                | ErrorReportType.Warning -> printfn "PackageManagementWarning %d : %s" code message
            ResolvingErrorReport (report)

        let idm = dp.TryFindDependencyManagerByKey(Seq.empty, "", reportError, "nuget")

        if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
            let result = dp.Resolve(idm, ".fsx", [|"r", "FSharp.Data,3.3.3"|], reportError, "net472")
            Assert.Equal(true, result.Success)
            Assert.Equal(1, result.Resolutions |> Seq.length)
            Assert.Equal(1, result.SourceFiles |> Seq.length)
            Assert.Equal(2, result.Roots |> Seq.length)

        let result = dp.Resolve(idm, ".fsx", [|"r", "FSharp.Data,3.3.3"|], reportError, "net5.0")
        Assert.Equal(true, result.Success)
        Assert.Equal(1, result.Resolutions |> Seq.length)
        Assert.Equal(1, result.SourceFiles |> Seq.length)
        Assert.Equal(1, result.Roots |> Seq.length)
        ()

    [<Fact>]
    member _.``Dependency Manager Reports package root for nuget package with no build artifacts``() =

        let nativeProbingRoots () = Seq.empty<string>

        use dp = new DependencyProvider(NativeResolutionProbe(nativeProbingRoots))
        let reportError =
            let report errorType code message =
                match errorType with
                | ErrorReportType.Error -> printfn "PackageManagementError %d : %s" code message
                | ErrorReportType.Warning -> printfn "PackageManagementWarning %d : %s" code message
            ResolvingErrorReport (report)

        let idm = dp.TryFindDependencyManagerByKey(Seq.empty, "", reportError, "nuget")

        let result = dp.Resolve(idm, ".fsx", [|"r", "Microsoft.Data.Sqlite, 3.1.8"|], reportError, "net5.0")
        Assert.Equal(true, result.Success)
        Assert.True((result.Resolutions |> Seq.length) > 1)
        Assert.Equal(1, result.SourceFiles |> Seq.length)
        Assert.True(Option.isSome(result.Roots |> Seq.tryFind(fun root -> root.EndsWith("microsoft.data.sqlite/3.1.8/"))))
        ()


    [<Fact>]
    member _.``Dependency add with nonexistent package should fail``() =

        let nativeProbingRoots () = Seq.empty<string>

        use dp = new DependencyProvider(NativeResolutionProbe(nativeProbingRoots))
        let reportError =
            let report errorType code message =
                match errorType with
                | ErrorReportType.Error -> printfn "PackageManagementError %d : %s" code message
                | ErrorReportType.Warning -> printfn "PackageManagementWarning %d : %s" code message
            ResolvingErrorReport (report)

        let idm = dp.TryFindDependencyManagerByKey(Seq.empty, "", reportError, "nuget")

        if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
            let result = dp.Resolve(idm, ".fsx", [|"r", "System.Collections.Immutable.DoesNotExist"|], reportError, "net472")
            Assert.Equal(false, result.Success)
            Assert.Equal(0, result.Resolutions |> Seq.length)
            Assert.Equal(0, result.SourceFiles |> Seq.length)
            Assert.Equal(0, result.Roots |> Seq.length)

        let result = dp.Resolve(idm, ".fsx", [|"r", "System.Collections.Immutable.DoesNotExist"|], reportError, "net5.0")
        Assert.Equal(false, result.Success)
        Assert.Equal(0, result.Resolutions |> Seq.length)
        Assert.Equal(0, result.SourceFiles |> Seq.length)
        Assert.Equal(0, result.Roots |> Seq.length)
        ()


    [<Fact(Skip="failing on main")>]
    member _.``Multiple Instances of DependencyProvider should be isolated``() =

        let assemblyProbingPaths () = Seq.empty<string>
        let nativeProbingRoots () = Seq.empty<string>

        use dp1 = new DependencyProvider(NativeResolutionProbe(nativeProbingRoots))
        let reportError =
            let report errorType code message =
                match errorType with
                | ErrorReportType.Error -> printfn "PackageManagementError %d : %s" code message
                | ErrorReportType.Warning -> printfn "PackageManagementWarning %d : %s" code message
            ResolvingErrorReport (report)

        let idm1 = dp1.TryFindDependencyManagerByKey(Seq.empty, "", reportError, "nuget")
        if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
            let result1 = dp1.Resolve(idm1, ".fsx", [|"r", "FSharp.Data,3.3.3"|], reportError, "net472")
            Assert.Equal(true, result1.Success)
            Assert.Equal(1, result1.Resolutions |> Seq.length)
            Assert.True((result1.Resolutions |> Seq.head).Contains("/net45/"))
            Assert.Equal(1, result1.SourceFiles |> Seq.length)
            Assert.Equal(2, result1.Roots |> Seq.length)
            Assert.True((result1.Roots |> Seq.head).EndsWith("/fsharp.data/3.3.3/"))
            Assert.True((result1.Roots |> Seq.last).EndsWith("/microsoft.netframework.referenceassemblies/1.0.0/"))

        let result2 = dp1.Resolve(idm1, ".fsx", [|"r", "FSharp.Data,3.3.3"|], reportError, "net5.0")
        Assert.Equal(true, result2.Success)
        Assert.Equal(1, result2.Resolutions |> Seq.length)
        let expected2 = "/netstandard2.0/"
        Assert.True((result2.Resolutions |> Seq.head).Contains(expected2))
        Assert.Equal(1, result2.SourceFiles |> Seq.length)
        Assert.Equal(1, result2.Roots |> Seq.length)
        Assert.True((result2.Roots |> Seq.head).EndsWith("/fsharp.data/3.3.3/"))

        use dp2 = new DependencyProvider(NativeResolutionProbe(nativeProbingRoots))
        let idm2 = dp2.TryFindDependencyManagerByKey(Seq.empty, "", reportError, "nuget")

        if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
            let result3 = dp2.Resolve(idm2, ".fsx", [|"r", "System.Json, Version=4.6.0"|], reportError, "net472")
            Assert.Equal(true, result3.Success)
            Assert.Equal(1, result3.Resolutions |> Seq.length)
            Assert.True((result3.Resolutions |> Seq.head).Contains("/netstandard2.0/"))
            Assert.Equal(1, result3.SourceFiles |> Seq.length)
            Assert.Equal(1, result3.SourceFiles |> Seq.length)
            Assert.True((result3.Roots |> Seq.head).EndsWith("/system.json/4.6.0/"))

        let result4 = dp2.Resolve(idm2, ".fsx", [|"r", "System.Json, Version=4.6.0"|], reportError, "net5.0")
        Assert.Equal(true, result4.Success)
        Assert.Equal(1, result4.Resolutions |> Seq.length)
        let expected4 = "/netstandard2.0/"
        Assert.True((result4.Resolutions |> Seq.head).Contains(expected4))
        Assert.Equal(1, result4.SourceFiles |> Seq.length)
        Assert.Equal(1, result4.Roots |> Seq.length)
        Assert.True((result4.Roots |> Seq.head).EndsWith("/system.json/4.6.0/"))
        ()

    [<Fact>]
    member _.``Nuget Reference package with dependencies we should get package roots and dependent references``() =

        let nativeProbingRoots () = Seq.empty<string>

        use dp1 = new DependencyProvider(NativeResolutionProbe(nativeProbingRoots))
        let reportError =
            let report errorType code message =
                match errorType with
                | ErrorReportType.Error -> printfn "PackageManagementError %d : %s" code message
                | ErrorReportType.Warning -> printfn "PackageManagementWarning %d : %s" code message
            ResolvingErrorReport (report)

        let idm1 = dp1.TryFindDependencyManagerByKey(Seq.empty, "", reportError, "nuget")

        if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
            let result1 = dp1.Resolve(idm1, ".fsx", [|"r", "Microsoft.Extensions.Configuration.Abstractions, 3.1.1"|], reportError, "net472")
            Assert.Equal(true, result1.Success)
            Assert.Equal(6, result1.Resolutions |> Seq.length)
            Assert.True((result1.Resolutions |> Seq.head).Contains("/netstandard2.0/"))
            Assert.Equal(1, result1.SourceFiles |> Seq.length)
            Assert.Equal(7, result1.Roots |> Seq.length)
            Assert.True((result1.Roots |> Seq.head).EndsWith("/microsoft.extensions.configuration.abstractions/3.1.1/"))

        // Netstandard gets fewer dependencies than desktop, because desktop framework doesn't contain assemblies like System.Memory
        // Those assemblies must be delivered by nuget for desktop apps
        let result2 = dp1.Resolve(idm1, ".fsx", [|"r", "Microsoft.Extensions.Configuration.Abstractions, 3.1.1"|], reportError, "net5.0")
        Assert.Equal(true, result2.Success)
        Assert.Equal(2, result2.Resolutions |> Seq.length)
        let expected = "/netcoreapp3.1/"
        Assert.True((result2.Resolutions |> Seq.head).Contains(expected))
        Assert.Equal(1, result2.SourceFiles |> Seq.length)
        Assert.Equal(2, result2.Roots |> Seq.length)
        Assert.True((result2.Roots |> Seq.head).EndsWith("/microsoft.extensions.configuration.abstractions/3.1.1/"))
        ()

/// Native dll resolution is not implemented on desktop
#if NETCOREAPP
    [<Fact(Skip="downloads very large ephemeral packages"); >]
    member _.``Script using TorchSharp``() =
        let text = """
#r "nuget:RestoreSources=https://donsyme.pkgs.visualstudio.com/TorchSharp/_packaging/packages2/nuget/v3/index.json"
#r "nuget:libtorch-cpu,0.3.52118"
#r "nuget:TorchSharp,0.3.52118"

TorchSharp.Tensor.LongTensor.From([| 0L .. 100L |]).Device
"""

        if RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
            use script = new scriptHost()
            let opt = script.Eval(text) |> getValue
            let value = opt.Value
            Assert.Equal(typeof<string>, value.ReflectionType)
            Assert.Equal("cpu", value.ReflectionValue :?> string)
        ()


    [<Fact>]
    member _.``Use Dependency Manager to restore packages with native dependencies, build and run script that depends on the results``() =
        let packagemanagerlines = [|
            "r", "Microsoft.ML,version=1.4.0-preview"
            "r", "Microsoft.ML.AutoML,version=0.16.0-preview"
            "r", "Microsoft.Data.Analysis,version=0.4.0"
        |]

        let reportError =
            let report errorType code message =
                match errorType with
                | ErrorReportType.Error -> printfn "PackageManagementError %d : %s" code message
                | ErrorReportType.Warning -> printfn "PackageManagementWarning %d : %s" code message
            ResolvingErrorReport (report)

        let mutable resolverPackageRoots = Seq.empty<string>
        let mutable resolverPackageRoots = Seq.empty<string>
        let mutable resolverReferences = Seq.empty<string>

        let nativeProbingRoots () = resolverPackageRoots
        let assemblyProbingPaths () = resolverReferences

        // Restore packages, Get Reference dll paths and package roots
        let result =
            use dp = new DependencyProvider(AssemblyResolutionProbe(assemblyProbingPaths), NativeResolutionProbe(nativeProbingRoots))
            let idm = dp.TryFindDependencyManagerByKey(Seq.empty, "", reportError, "nuget")
            dp.Resolve(idm, ".fsx", packagemanagerlines, reportError, "net5.0")

        Assert.True(result.Success, "resolve failed")

        resolverPackageRoots <- result.Roots
        resolverReferences <- result.Resolutions

// Build and execute reference
        let referenceText =
#if DISABLED_DUE_TO_ISSUE_8588
//
// https://github.com/dotnet/fsharp/issues/8588
            // For this test case use Assembly Qualified References
            ("", result.Resolutions)
            ||> Seq.fold(fun acc r ->
                let assemblyName = AssemblyName.GetAssemblyName(r)
                acc + "#r @\"" + assemblyName.FullName + "\"" + Environment.NewLine)
#else
            // use standard #r for now
            ("", result.Resolutions)
            ||> Seq.fold(fun acc r ->
                acc + "#r @\"" + r + "\"" + Environment.NewLine)
#endif

        let code = @"
$(REFERENCES)

open System
open System.IO
open System.Linq
open Microsoft.Data.Analysis

let Shuffle (arr:int[]) =
    let rnd = Random()
    for i in 0 .. arr.Length - 1 do
        let r = i + rnd.Next(arr.Length - i)
        let temp = arr.[r]
        arr.[r] <- arr.[i]
        arr.[i] <- temp
    arr

let housingPath = ""housing.csv""
let housingData = DataFrame.LoadCsv(housingPath)
let randomIndices = (Shuffle(Enumerable.Range(0, (int (housingData.Rows.Count) - 1)).ToArray()))
let testSize = int (float (housingData.Rows.Count) * 0.1)
let trainRows = randomIndices.[testSize..]
let testRows = randomIndices.[..testSize]
let housing_train = housingData.[trainRows]

open Microsoft.ML
open Microsoft.ML.Data
open Microsoft.ML.AutoML

let mlContext = MLContext()
let experiment = mlContext.Auto().CreateRegressionExperiment(maxExperimentTimeInSeconds = 15u)
let result = experiment.Execute(housing_train, labelColumnName = ""median_house_value"")
let details = result.RunDetails
printfn ""%A"" result
123
"
        let scriptText = code.Replace("$(REFERENCES)", referenceText)

        // Use the dependency manager to resolve assemblies and native paths
        use dp = new DependencyProvider(AssemblyResolutionProbe(assemblyProbingPaths), NativeResolutionProbe(nativeProbingRoots))

        use script = new FSharpScript()
        let opt = script.Eval(scriptText)  |> getValue
        let value = opt.Value
        Assert.Equal(123, value.ReflectionValue :?> int32)

    [<Fact>]
    member _.``Use NativeResolver to resolve native dlls.``() =
        let packagemanagerlines = [|
            "r", "Microsoft.ML,version=1.4.0-preview"
            "r", "Microsoft.ML.AutoML,version=0.16.0-preview"
            "r", "Microsoft.Data.Analysis,version=0.4.0"
        |]

        let reportError =
            let report errorType code message =
                match errorType with
                | ErrorReportType.Error -> printfn "PackageManagementError %d : %s" code message
                | ErrorReportType.Warning -> printfn "PackageManagementWarning %d : %s" code message
            ResolvingErrorReport (report)

        let mutable resolverPackageRoots = Seq.empty<string>
        let mutable resolverPackageRoots = Seq.empty<string>

        let mutable resolverReferences = Seq.empty<string>
        let nativeProbingRoots () = resolverPackageRoots
        let assemblyPaths () = resolverReferences

        // Restore packages, Get Reference dll paths and package roots
        let result =
            use dp = new DependencyProvider(NativeResolutionProbe(nativeProbingRoots))
            let idm = dp.TryFindDependencyManagerByKey(Seq.empty, "", reportError, "nuget")
            dp.Resolve(idm, ".fsx", packagemanagerlines, reportError, "net5.0")

        Assert.True(result.Success, "resolve failed")

        resolverPackageRoots <- result.Roots
        resolverReferences <- result.Resolutions

        use _nativeDepencyResolver = new NativeDllResolveHandler(NativeResolutionProbe(nativeProbingRoots))

        // Build and execute script
        let referenceText =
            ("", result.Resolutions) ||> Seq.fold(fun acc r -> acc + @"#r @""" + r + "\"" + Environment.NewLine)

        let code = @"
$(REFERENCES)

open System
open System.IO
open System.Linq
open Microsoft.Data.Analysis

let Shuffle (arr:int[]) =
    let rnd = Random()
    for i in 0 .. arr.Length - 1 do
        let r = i + rnd.Next(arr.Length - i)
        let temp = arr.[r]
        arr.[r] <- arr.[i]
        arr.[i] <- temp
    arr

let housingPath = ""housing.csv""
let housingData = DataFrame.LoadCsv(housingPath)
let randomIndices = (Shuffle(Enumerable.Range(0, (int (housingData.Rows.Count) - 1)).ToArray()))
let testSize = int (float (housingData.Rows.Count) * 0.1)
let trainRows = randomIndices.[testSize..]
let testRows = randomIndices.[..testSize]
let housing_train = housingData.[trainRows]

open Microsoft.ML
open Microsoft.ML.Data
open Microsoft.ML.AutoML

let mlContext = MLContext()
let experiment = mlContext.Auto().CreateRegressionExperiment(maxExperimentTimeInSeconds = 15u)
let result = experiment.Execute(housing_train, labelColumnName = ""median_house_value"")
let details = result.RunDetails
printfn ""%A"" result
123
"
        let scriptText = code.Replace("$(REFERENCES)", referenceText)

        use script = new FSharpScript()
        let opt = script.Eval(scriptText)  |> getValue
        let value = opt.Value
        Assert.Equal(123, value.ReflectionValue :?> int32)

    [<Fact>]
    member _.``Use AssemblyResolver to resolve assemblies``() =
        let packagemanagerlines = [|
            "r", "Microsoft.ML,version=1.4.0-preview"
            "r", "Microsoft.ML.AutoML,version=0.16.0-preview"
            "r", "Microsoft.Data.Analysis,version=0.4.0"
        |]

        let reportError =
            let report errorType code message =
                match errorType with
                | ErrorReportType.Error -> printfn "PackageManagementError %d : %s" code message
                | ErrorReportType.Warning -> printfn "PackageManagementWarning %d : %s" code message
            ResolvingErrorReport (report)

        let mutable resolverPackageRoots = Seq.empty<string>
        let mutable resolverReferences = Seq.empty<string>

        let nativeProbingRoots () = resolverPackageRoots
        let assemblyProbingPaths () = resolverReferences

        // Restore packages, Get Reference dll paths and package roots
        let result =
            use dp = new DependencyProvider(NativeResolutionProbe(nativeProbingRoots))
            let idm = dp.TryFindDependencyManagerByKey(Seq.empty, "", reportError, "nuget")
            dp.Resolve(idm, ".fsx", packagemanagerlines, reportError, "net5.0")

        Assert.True(result.Success, "resolve failed")

        resolverPackageRoots <- result.Roots
        resolverReferences <- result.Resolutions

        use _assemblyResolver = new AssemblyResolveHandler(AssemblyResolutionProbe(assemblyProbingPaths))

        // Build and execute script
        let referenceText =
            ("", result.Resolutions)
            ||> Seq.fold(fun acc r ->
                acc + "    @\"" + r + "\";" + Environment.NewLine)

        let code = """
open System.Reflection

let x = [|
$(REFERENCES)
    |]

x |> Seq.iter(fun r ->
    let name = AssemblyName.GetAssemblyName(r)
    let asm = Assembly.Load(name)
    printfn "%A" (asm.FullName)
    )
123
"""
        let scriptText = code.Replace("$(REFERENCES)", referenceText)

        use script = new FSharpScript()
        let opt = script.Eval(scriptText)  |> getValue
        let value = opt.Value
        Assert.Equal(123, value.ReflectionValue :?> int32)

    [<Fact>]
    member _.``Verify that referencing FSharp.Core fails with FSharp Scripts``() =
        let packagemanagerlines = [| "r", "FSharp.Core,version=4.7.1" |]

        let reportError =
            let report errorType code message =
                match errorType with
                | ErrorReportType.Error -> printfn "PackageManagementError %d : %s" code message
                | ErrorReportType.Warning -> printfn "PackageManagementWarning %d : %s" code message
            ResolvingErrorReport (report)

        let mutable resolverPackageRoots = Seq.empty<string>
        let mutable resolverReferences = Seq.empty<string>

        let nativeProbingRoots () = resolverPackageRoots
        let assemblyProbingPaths () = resolverReferences

        // Restore packages, Get Reference dll paths and package roots
        let result =
            use dp = new DependencyProvider(NativeResolutionProbe(nativeProbingRoots))
            let idm = dp.TryFindDependencyManagerByKey(Seq.empty, "", reportError, "nuget")
            dp.Resolve(idm, ".fsx", packagemanagerlines, reportError, "net5.0")

        // Expected: error FS3217: PackageManager can not reference the System Package 'FSharp.Core'
        Assert.False(result.Success, "resolve succeeded but should have failed")

    [<Fact>]
    member _.``Verify that referencing FSharp.Core succeeds with CSharp Scripts``() =
        let packagemanagerlines = [| "r", "FSharp.Core,version=4.7.1" |]

        let reportError =
            let report errorType code message =
                match errorType with
                | ErrorReportType.Error -> printfn "PackageManagementError %d : %s" code message
                | ErrorReportType.Warning -> printfn "PackageManagementWarning %d : %s" code message
            ResolvingErrorReport (report)

        let mutable resolverPackageRoots = Seq.empty<string>
        let mutable resolverReferences = Seq.empty<string>

        let nativeProbingRoots () = resolverPackageRoots
        let assemblyProbingPaths () = resolverReferences

        // Restore packages, Get Reference dll paths and package roots
        let result =
            use dp = new DependencyProvider(NativeResolutionProbe(nativeProbingRoots))
            let idm = dp.TryFindDependencyManagerByKey(Seq.empty, "", reportError, "nuget")
            dp.Resolve(idm, ".csx", packagemanagerlines, reportError, "net5.0")

        Assert.True(result.Success, "resolve failed but should have succeeded")


    [<Fact>]
    member _.``Verify that Dispose on DependencyProvider unhooks ResolvingUnmanagedDll event handler``() =

        let mutable found = false
        let nativeProbingRoots () =
            found <- true
            Seq.empty<string>

        let reportError =
            let report errorType code message =
                match errorType with
                | ErrorReportType.Error -> printfn "PackageManagementError %d : %s" code message
                | ErrorReportType.Warning -> printfn "PackageManagementWarning %d : %s" code message
            ResolvingErrorReport (report)

        // Set up native resolver to resolve dll's
        do
            use dp = new DependencyProvider(NativeResolutionProbe(nativeProbingRoots))

            // Invoking a non-existent dll via pinvoke cause a probe. which should invoke the call back
            try Native.NoneSuch() |> ignore with _ -> ()
            Assert.True (found, "Failed to invoke the nativeProbingRoots callback")

        // Here the dispose was invoked which should clear the ResolvingUnmanagedDll handler
        found <- false
        try Native.NoneSuch() |> ignore with _ -> ()
        Assert.False (found, "Invoke the nativeProbingRoots callback -- Error the ResolvingUnmanagedDll still fired ")

        use dp = new DependencyProvider(NativeResolutionProbe(nativeProbingRoots))
        let idm = dp.TryFindDependencyManagerByKey(Seq.empty, "", reportError, "nuget")

        if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
            let result = dp.Resolve(idm, ".fsx", [|"r", "FSharp.Data,3.3.3"|], reportError, "net472")
            Assert.Equal(true, result.Success)
            Assert.Equal(1, result.Resolutions |> Seq.length)
            Assert.Equal(1, result.SourceFiles |> Seq.length)
            Assert.Equal(2, result.Roots |> Seq.length)

        let result = dp.Resolve(idm, ".fsx", [|"r", "FSharp.Data,3.3.3"|], reportError, "net5.0")
        Assert.Equal(true, result.Success)
        Assert.Equal(1, result.Resolutions |> Seq.length)
        Assert.Equal(1, result.SourceFiles |> Seq.length)
        Assert.Equal(1, result.Roots |> Seq.length)
        ()


    [<Fact>]
    member _.``Verify that Dispose on DependencyProvider unhooks ResolvingUnmanagedDll and AssemblyResolver event handler``() =

        let mutable assemblyFound = false
        let assemblyProbingPaths () =
            assemblyFound <- true
            Seq.empty<string>

        let mutable nativeFound = false
        let nativeProbingRoots () =
            nativeFound <- true
            Seq.empty<string>

        // Set up native resolver to resolve dll's
        do
            use dp = new DependencyProvider(AssemblyResolutionProbe(assemblyProbingPaths), NativeResolutionProbe(nativeProbingRoots))

            // Invoking a non-existent dll via pinvoke cause a probe. which should invoke the call back
            try Native.NoneSuch() |> ignore with _ -> ()
            Assert.True (nativeFound, "Failed to invoke the nativeProbingRoots callback")

            // Invoking a non-existent assembly causes a probe. which should invoke the call back
            try Assembly.Load("NoneSuchAssembly") |> ignore with _ -> ()
            Assert.True (assemblyFound, "Failed to invoke the AssemblyResolve handler")

        // Here the dispose was invoked which should clear the ResolvingUnmanagedDll handler
        nativeFound <- false
        assemblyFound <- false

        try Native.NoneSuch() |> ignore with _ -> ()
        Assert.False (nativeFound, "Invoke the nativeProbingRoots callback -- Error the ResolvingUnmanagedDll still fired ")

        try Assembly.Load("NoneSuchAssembly") |> ignore with _ -> ()
        Assert.False (assemblyFound, "Invoke the assemblyProbingRoots callback -- Error the AssemblyResolve still fired ")
#endif

    [<Fact>]
    member _.``Verify that Dispose on AssemblyResolveHandler unhooks AssemblyResolve event handler``() =

        let mutable assemblyFound = false
        let assemblyProbingPaths () =
            assemblyFound <- true
            Seq.empty<string>

        // Set up AssemblyResolver to resolve dll's
        do
            use dp = new AssemblyResolveHandler(AssemblyResolutionProbe(assemblyProbingPaths))

            // Invoking a non-existent assembly causes a probe. which should invoke the call back
            try Assembly.Load("NoneSuchAssembly") |> ignore with _ -> ()
            Assert.True (assemblyFound, "Failed to invoke the AssemblyResolve handler")

        // Here the dispose was invoked which should clear the ResolvingUnmanagedDll handler
        assemblyFound <- false

        try Assembly.Load("NoneSuchAssembly") |> ignore with _ -> ()
        Assert.False (assemblyFound, "Invoke the assemblyProbingRoots callback -- Error the AssemblyResolve still fired ")

    [<Fact>]
    member _.``Verify that Dispose cleans up the native paths added``() =
        let nativeProbingRoots () = Seq.empty<string>

        let appendSemiColon (p:string) =
            if not(p.EndsWith(";", StringComparison.OrdinalIgnoreCase)) then
                p + ";"
            else
                p

        let reportError =
            let report errorType code message =
                match errorType with
                | ErrorReportType.Error -> printfn "PackageManagementError %d : %s" code message
                | ErrorReportType.Warning -> printfn "PackageManagementWarning %d : %s" code message
            ResolvingErrorReport (report)

        let mutable initialPath:string = null
        let mutable currentPath:string = null
        let mutable finalPath:string =  null
        do
            initialPath <- appendSemiColon (Environment.GetEnvironmentVariable("PATH"))
            use dp = new DependencyProvider(NativeResolutionProbe(nativeProbingRoots))
            let idm = dp.TryFindDependencyManagerByKey(Seq.empty, "", reportError, "nuget")
            let mutable currentPath:string = null
            if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
                let result = dp.Resolve(idm, ".fsx", [|"r", "Microsoft.Data.Sqlite,3.1.7"|], reportError, "netstandard2.0")
                Assert.Equal(true, result.Success)
                currentPath <-  appendSemiColon (Environment.GetEnvironmentVariable("PATH"))
        finalPath <- appendSemiColon (Environment.GetEnvironmentVariable("PATH"))
        Assert.True(currentPath <> initialPath)     // The path was modified by #r "nuget: ..."
        Assert.Equal(finalPath, initialPath)        // IDispose correctly cleaned up the path

        initialPath <- null
        currentPath <- null
        finalPath <-  null
        do
            initialPath <- appendSemiColon (Environment.GetEnvironmentVariable("PATH"))
            let mutable currentPath:string = null
            use dp = new DependencyProvider(NativeResolutionProbe(nativeProbingRoots))
            let idm = dp.TryFindDependencyManagerByKey(Seq.empty, "", reportError, "nuget")
            let result = dp.Resolve(idm, ".fsx", [|"r", "Microsoft.Data.Sqlite,3.1.7"|], reportError, "net5.0")
            Assert.Equal(true, result.Success)
            currentPath <-  appendSemiColon (Environment.GetEnvironmentVariable("PATH"))
        finalPath <- appendSemiColon (Environment.GetEnvironmentVariable("PATH"))
        Assert.True(currentPath <> initialPath)      // The path was modified by #r "nuget: ..."
        Assert.Equal(finalPath, initialPath)        // IDispose correctly cleaned up the path

        ()

    [<Fact>]
    member _.``Verify that #help produces help text for fsi + dependency manager``() =
        let expected = [|
            """  F# Interactive directives:"""
            """"""
            """    #r "file.dll";;                               // Reference (dynamically load) the given DLL"""
            """    #i "package source uri";;                     // Include package source uri when searching for packages"""
            """    #I "path";;                                   // Add the given search path for referenced DLLs"""
            """    #load "file.fs" ...;;                         // Load the given file(s) as if compiled and referenced"""
            """    #time ["on"|"off"];;                          // Toggle timing on/off"""
            """    #help;;                                       // Display help"""
            """    #quit;;                                       // Exit"""
            """"""
            """  F# Interactive command line options:"""
            """"""

            // this is the end of the line each different platform has a different mechanism for starting fsi
            // Actual output looks similar to: """      See 'testhost --help' for options"""
            """--help' for options"""

            """"""
            """"""
        |]

        let mutable found = 0
        let lines = System.Collections.Generic.List()
        use sawExpectedOutput = new ManualResetEvent(false)
        let verifyOutput (line: string) =
            let compareLine (s: string) =
                if s = "" then line = ""
                else line.EndsWith(s)
            lines.Add(line)
            match expected |> Array.tryFind(compareLine) with
            | None -> ()
            | Some t ->
                found <- found + 1
                if found = expected.Length then sawExpectedOutput.Set() |> ignore

        let text = "#help"
        use output = new RedirectConsoleOutput()
        use script = new FSharpScript(quiet = false, langVersion = LangVersion.V47)
        let mutable found = 0
        output.OutputProduced.Add (fun line -> verifyOutput line)
        let opt = script.Eval(text) |> getValue
        Assert.True(sawExpectedOutput.WaitOne(TimeSpan.FromSeconds(5.0)), sprintf "Expected to see error sentinel value written\nexpected:%A\nactual:%A" expected lines)


    [<Fact>]
    member _.``Verify that #help produces help text for fsi + dependency manager language version preview``() =
        let expected = [|
            """  F# Interactive directives:"""
            """"""
            """    #r "file.dll";;                               // Reference (dynamically load) the given DLL"""
            """    #i "package source uri";;                     // Include package source uri when searching for packages"""
            """    #I "path";;                                   // Add the given search path for referenced DLLs"""
            """    #load "file.fs" ...;;                         // Load the given file(s) as if compiled and referenced"""
            """    #time ["on"|"off"];;                          // Toggle timing on/off"""
            """    #help;;                                       // Display help"""
            """    #r "nuget:FSharp.Data, 3.1.2";;               // Load Nuget Package 'FSharp.Data' version '3.1.2'"""
            """    #r "nuget:FSharp.Data";;                      // Load Nuget Package 'FSharp.Data' with the highest version"""
            """    #quit;;                                       // Exit"""
            """"""
            """  F# Interactive command line options:"""
            """"""

            // this is the end of the line each different platform has a different mechanism for starting fsi
            // Actual output looks similar to: """      See 'testhost --help' for options"""
            """--help' for options"""

            """"""
            """"""
        |]

        let mutable found = 0
        let lines = System.Collections.Generic.List()
        use sawExpectedOutput = new ManualResetEvent(false)
        let verifyOutput (line: string) =
            let compareLine (s: string) =
                if s = "" then line = ""
                else line.EndsWith(s)
            lines.Add(line)
            match expected |> Array.tryFind(compareLine) with
            | None -> ()
            | Some t ->
                found <- found + 1
                if found = expected.Length then sawExpectedOutput.Set() |> ignore

        let text = "#help"
        use output = new RedirectConsoleOutput()
        use script = new FSharpScript(quiet = false, langVersion = LangVersion.Preview)
        let mutable found = 0
        output.OutputProduced.Add (fun line -> verifyOutput line)
        let opt = script.Eval(text) |> getValue
        Assert.True(sawExpectedOutput.WaitOne(TimeSpan.FromSeconds(5.0)), sprintf "Expected to see error sentinel value written\nexpected:%A\nactual:%A" expected lines)


    [<Fact>]
    member _.``Verify that timeout --- times out and fails``() =
        let nativeProbingRoots () = Seq.empty<string>
        let mutable foundCorrectError = false
        let mutable foundWrongError = false

        use dp = new DependencyProvider(NativeResolutionProbe(nativeProbingRoots))
        let reportError =
            let report errorType code message =
                match errorType with
                | ErrorReportType.Error ->
                    if code = 3217 then foundCorrectError <- true
                    else foundWrongError <- true
                | ErrorReportType.Warning -> printfn "PackageManagementWarning %d : %s" code message
            ResolvingErrorReport (report)

        let idm = dp.TryFindDependencyManagerByKey(Seq.empty, "", reportError, "nuget")
        let result = dp.Resolve(idm, ".fsx", [|"r", "FSharp.Data,3.3.3"|], reportError, "net5.0", timeout=0)           // Fail in 0 milliseconds
        Assert.Equal(false, result.Success)
        Assert.Equal(foundCorrectError, true)
        Assert.Equal(foundWrongError, false)
        ()

    [<Fact>]
    member _.``Verify that script based timeout overrides api based - timeout on script``() =
        let nativeProbingRoots () = Seq.empty<string>
        let mutable foundCorrectError = false
        let mutable foundWrongError = false

        use dp = new DependencyProvider(NativeResolutionProbe(nativeProbingRoots))
        let reportError =
            let report errorType code message =
                match errorType with
                | ErrorReportType.Error ->
                    if code = 3217 then foundCorrectError <- true
                    else foundWrongError <- true
                | ErrorReportType.Warning -> printfn "PackageManagementWarning %d : %s" code message
            ResolvingErrorReport (report)

        let idm = dp.TryFindDependencyManagerByKey(Seq.empty, "", reportError, "nuget")
        let result = dp.Resolve(idm, ".fsx", [|"r", "FSharp.Data,3.3.3"; "r", "timeout=0"|], reportError, "net5.0", null, "", "", "", -1)           // Wait forever
        Assert.Equal(false, result.Success)
        Assert.Equal(foundCorrectError, true)
        Assert.Equal(foundWrongError, false)
        ()

    [<Fact>]
    member _.``Verify that script based timeout overrides api based - timeout on api , forever on script``() =
        let nativeProbingRoots () = Seq.empty<string>
        let mutable foundCorrectError = false
        let mutable foundWrongError = false

        use dp = new DependencyProvider(NativeResolutionProbe(nativeProbingRoots))
        let reportError =
            let report errorType code message =
                match errorType with
                | ErrorReportType.Error ->
                    if code = 3401 then foundCorrectError <- true
                    else foundWrongError <- true
                | ErrorReportType.Warning -> printfn "PackageManagementWarning %d : %s" code message
            ResolvingErrorReport (report)

        let idm = dp.TryFindDependencyManagerByKey(Seq.empty, "", reportError, "nuget")
        let result = dp.Resolve(idm, ".fsx", [|"r", "FSharp.Data,3.3.3"; "r", "timeout=none"|], reportError, "net5.0", null, "", "", "", -1)           // Wait forever
        Assert.Equal(true, result.Success)
        Assert.Equal(foundCorrectError, false)
        Assert.Equal(foundWrongError, false)
        ()


