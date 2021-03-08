// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Test.Utilities

open System
open System.Diagnostics
open System.IO
open System.Text
open System.Diagnostics
open System.Collections.Generic
open System.Reflection
open FSharp.Compiler.Interactive.Shell
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Text
#if FX_NO_APP_DOMAINS
open System.Runtime.Loader
#endif
open NUnit.Framework
open System.Reflection.Emit
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.CSharp
open FSharp.Test.Utilities.Utilities
open TestFramework

[<Sealed>]
type ILVerifier (dllFilePath: string) =

    member this.VerifyIL (qualifiedItemName: string, expectedIL: string) =
        ILChecker.checkILItem qualifiedItemName dllFilePath [ expectedIL ]

    member this.VerifyIL (expectedIL: string list) =
        ILChecker.checkIL dllFilePath expectedIL

    member this.VerifyILWithLineNumbers (qualifiedItemName: string, expectedIL: string) =
        ILChecker.checkILItemWithLineNumbers qualifiedItemName dllFilePath [ expectedIL ]

type Worker () =
    inherit MarshalByRefObject()

    member x.ExecuteTestCase assemblyPath (deps: string[]) =
        AppDomain.CurrentDomain.add_AssemblyResolve(ResolveEventHandler(fun _ args ->
            deps
            |> Array.tryFind (fun (x: string) -> Path.GetFileNameWithoutExtension x = args.Name)
            |> Option.bind (fun x -> if File.Exists x then Some x else None)
            |> Option.map Assembly.LoadFile
            |> Option.defaultValue null))
        let asm = Assembly.LoadFrom(assemblyPath)
        let entryPoint = asm.EntryPoint
        (entryPoint.Invoke(Unchecked.defaultof<obj>, [||])) |> ignore

type SourceKind =
    | Fs
    | Fsx

type CompileOutput =
    | Library
    | Exe

type CompilationReference =
    private
    | CompilationReference of Compilation * staticLink: bool
    | TestCompilationReference of TestCompilation

    static member CreateFSharp(cmpl: Compilation, ?staticLink) =
        let staticLink = defaultArg staticLink false
        CompilationReference(cmpl, staticLink)

    static member Create(cmpl: TestCompilation) =
        TestCompilationReference cmpl

and Compilation = private Compilation of source: string * SourceKind * CompileOutput * options: string[] * CompilationReference list * name: string option with

    static member Create(source, sourceKind, output, ?options, ?cmplRefs, ?name) =
        let options = defaultArg options [||]
        let cmplRefs = defaultArg cmplRefs []
        Compilation(source, sourceKind, output, options, cmplRefs, name)

[<Sealed;AbstractClass>]
type CompilerAssert private () =

    static let checker = FSharpChecker.Create(suggestNamesForErrors=true)

    static let config = TestFramework.initializeSuite ()

    static let _ = config |> ignore

// Do a one time dotnet sdk build to compute the proper set of reference assemblies to pass to the compiler
    static let projectFile = """
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>$TARGETFRAMEWORK</TargetFramework>
    <UseFSharpPreview>true</UseFSharpPreview>
    <DisableImplicitFSharpCoreReference>true</DisableImplicitFSharpCoreReference>
  </PropertyGroup>

  <ItemGroup><Compile Include="Program.fs" /></ItemGroup>
  <ItemGroup><Reference Include="$FSHARPCORELOCATION" /></ItemGroup>
  <ItemGroup Condition="'$(TARGETFRAMEWORK)'=='net472'">
    <Reference Include="System" />
    <Reference Include="System.Runtime" />
    <Reference Include="System.Core.dll" />
    <Reference Include="System.Xml.Linq.dll" />
    <Reference Include="System.Data.DataSetExtensions.dll" />
    <Reference Include="Microsoft.CSharp.dll" />
    <Reference Include="System.Data.dll" />
    <Reference Include="System.Deployment.dll" />
    <Reference Include="System.Drawing.dll" />
    <Reference Include="System.Net.Http.dll" />
    <Reference Include="System.Windows.Forms.dll" />
    <Reference Include="System.Xml.dll" />
  </ItemGroup>

  <Target Name="WriteFrameworkReferences" AfterTargets="AfterBuild">
    <WriteLinesToFile File="FrameworkReferences.txt" Lines="@(ReferencePath)" Overwrite="true" WriteOnlyWhenDifferent="true" />
  </Target>

</Project>"""

    static let directoryBuildProps = """
<Project>
</Project>
"""

    static let directoryBuildTargets = """
<Project>
</Project>
"""

    static let programFs = """
open System

[<EntryPoint>]
let main argv = 0"""

    static let getNetCoreAppReferences =
        let mutable output = ""
        let mutable errors = ""
        let mutable cleanUp = true
        let pathToArtifacts = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "../../../.."))
        if Path.GetFileName(pathToArtifacts) <> "artifacts" then failwith "CompilerAssert did not find artifacts directory --- has the location changed????"
        let pathToTemp = Path.Combine(pathToArtifacts, "Temp")
        let projectDirectory = Path.Combine(pathToTemp, "CompilerAssert", Path.GetRandomFileName())
        let pathToFSharpCore = typeof<RequireQualifiedAccessAttribute>.Assembly.Location
        try
            try
                Directory.CreateDirectory(projectDirectory) |> ignore
                let projectFileName = Path.Combine(projectDirectory, "ProjectFile.fsproj")
                let programFsFileName = Path.Combine(projectDirectory, "Program.fs")
                let directoryBuildPropsFileName = Path.Combine(projectDirectory, "Directory.Build.props")
                let directoryBuildTargetsFileName = Path.Combine(projectDirectory, "Directory.Build.targets")
                let frameworkReferencesFileName = Path.Combine(projectDirectory, "FrameworkReferences.txt")
#if NETCOREAPP
                File.WriteAllText(projectFileName, projectFile.Replace("$TARGETFRAMEWORK", "net5.0").Replace("$FSHARPCORELOCATION", pathToFSharpCore))
#else
                File.WriteAllText(projectFileName, projectFile.Replace("$TARGETFRAMEWORK", "net472").Replace("$FSHARPCORELOCATION", pathToFSharpCore))
#endif
                File.WriteAllText(programFsFileName, programFs)
                File.WriteAllText(directoryBuildPropsFileName, directoryBuildProps)
                File.WriteAllText(directoryBuildTargetsFileName, directoryBuildTargets)

                let timeout = 30000
                let exitCode, output, errors = Commands.executeProcess (Some config.DotNetExe) "build" projectDirectory timeout

                if exitCode <> 0 || errors.Length > 0 then
                    printfn "Output:\n=======\n"
                    output |> Seq.iter(fun line -> printfn "STDOUT:%s\n" line)
                    printfn "Errors:\n=======\n"
                    errors  |> Seq.iter(fun line -> printfn "STDERR:%s\n" line)
                    Assert.True(false, "Errors produced generating References")

                File.ReadLines(frameworkReferencesFileName) |> Seq.toArray
            with | e ->
                cleanUp <- false
                printfn "Project directory: %s" projectDirectory
                printfn "STDOUT: %s" output
                File.WriteAllText(Path.Combine(projectDirectory, "project.stdout"), output)
                printfn "STDERR: %s" errors
                File.WriteAllText(Path.Combine(projectDirectory, "project.stderror"), errors)
                raise (new Exception (sprintf "An error occurred getting netcoreapp references: %A" e))
        finally
            if cleanUp then
                try Directory.Delete(projectDirectory, recursive=true) with | _ -> ()

#if FX_NO_APP_DOMAINS
    static let executeBuiltApp assembly deps =
        let ctxt = AssemblyLoadContext("ContextName", true)
        try
            let asm = ctxt.LoadFromAssemblyPath(assembly)
            let entryPoint = asm.EntryPoint
            ctxt.add_Resolving(fun ctxt name ->
                deps
                |> List.tryFind (fun (x: string) -> Path.GetFileNameWithoutExtension x = name.Name)
                |> Option.map ctxt.LoadFromAssemblyPath
                |> Option.defaultValue null)
            (entryPoint.Invoke(Unchecked.defaultof<obj>, [||])) |> ignore
        finally
            ctxt.Unload()
#else

    static let pathToThisDll = Assembly.GetExecutingAssembly().CodeBase

    static let adSetup =
        let setup = new System.AppDomainSetup ()
        setup.PrivateBinPath <- pathToThisDll
        setup

    static let executeBuiltApp assembly deps =
        let ad = AppDomain.CreateDomain((Guid()).ToString(), null, adSetup)
        let worker = (ad.CreateInstanceFromAndUnwrap(pathToThisDll, typeof<Worker>.FullName)) :?> Worker
        worker.ExecuteTestCase assembly (deps |> Array.ofList) |>ignore
#endif

    static let defaultProjectOptions =
        {
            ProjectFileName = "Z:\\test.fsproj"
            ProjectId = None
            SourceFiles = [|"test.fs"|]
            OtherOptions =
                let assemblies = getNetCoreAppReferences |> Array.map (fun x -> sprintf "-r:%s" x)
#if NETCOREAPP
                Array.append [|"--preferreduilang:en-US"; "--targetprofile:netcore"; "--noframework"; "--simpleresolution"; "--warn:5"|] assemblies
#else
                Array.append [|"--preferreduilang:en-US"; "--targetprofile:mscorlib"; "--noframework"; "--warn:5"|] assemblies
#endif
            ReferencedProjects = [||]
            IsIncompleteTypeCheckEnvironment = false
            UseScriptResolutionRules = false
            LoadTime = DateTime()
            UnresolvedReferences = None
            OriginalLoadReferences = []
            Stamp = None
        }

    static let rawCompile inputFilePath outputFilePath isExe options source =
        File.WriteAllText (inputFilePath, source)
        let args =
            options
            |> Array.append defaultProjectOptions.OtherOptions
            |> Array.append [| "fsc.dll"; inputFilePath; "-o:" + outputFilePath; (if isExe then "--target:exe" else "--target:library"); "--nowin32manifest" |]
        let errors, _ = checker.Compile args |> Async.RunSynchronously
        errors, outputFilePath

    static let compileAux isExe options source f : unit =
        let inputFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
        let outputFilePath = Path.ChangeExtension (Path.GetTempFileName(), if isExe then ".exe" else ".dll")
        try
            f (rawCompile inputFilePath outputFilePath isExe options source)
        finally
            try File.Delete inputFilePath with | _ -> ()
            try File.Delete outputFilePath with | _ -> ()

    static let compileDisposable outputPath isScript isExe options nameOpt source =
        let ext =
            if isScript then ".fsx"
            else ".fs"
        let inputFilePath = Path.ChangeExtension(Path.Combine(outputPath, Path.GetRandomFileName()), ext)
        let name =
            match nameOpt with
            | Some name -> name
            | _ -> Path.GetRandomFileName()
        let outputFilePath = Path.ChangeExtension (Path.Combine(outputPath, name), if isExe then ".exe" else ".dll")
        let o =
            { new IDisposable with
                member _.Dispose() =
                    try File.Delete inputFilePath with | _ -> ()
                    try File.Delete outputFilePath with | _ -> () }
        try
            o, rawCompile inputFilePath outputFilePath isExe options source
        with
        | _ ->
            o.Dispose()
            reraise()

    static let assertErrors libAdjust ignoreWarnings (errors: FSharpDiagnostic []) expectedErrors =
        let errors =
            errors
            |> Array.filter (fun error -> if ignoreWarnings then error.Severity <> FSharpDiagnosticSeverity.Warning else true)
            |> Array.distinctBy (fun e -> e.Severity, e.ErrorNumber, e.StartLine, e.StartColumn, e.EndLine, e.EndColumn, e.Message)
            |> Array.map (fun info ->
                (info.Severity, info.ErrorNumber, (info.StartLine - libAdjust, info.StartColumn + 1, info.EndLine - libAdjust, info.EndColumn + 1), info.Message))

        let checkEqual k a b =
            if a <> b then
                Assert.AreEqual(a, b, sprintf "Mismatch in %s, expected '%A', got '%A'.\nAll errors:\n%A" k a b errors)

        checkEqual "Errors"  (Array.length expectedErrors) errors.Length

        Array.zip errors expectedErrors
        |> Array.iter (fun (actualError, expectedError) ->
            let (expectedSeverity, expectedErrorNumber, expectedErrorRange, expectedErrorMsg: string) = expectedError
            let (actualSeverity, actualErrorNumber, actualErrorRange, actualErrorMsg: string) = actualError
            let expectedErrorMsg = expectedErrorMsg.Replace("\r\n", "\n")
            let actualErrorMsg = actualErrorMsg.Replace("\r\n", "\n")
            checkEqual "Severity" expectedSeverity actualSeverity
            checkEqual "ErrorNumber" expectedErrorNumber actualErrorNumber
            checkEqual "ErrorRange" expectedErrorRange actualErrorRange
            checkEqual "Message" expectedErrorMsg actualErrorMsg)

    static let gate = obj ()

    static let compile isExe options source f =
        lock gate (fun _ -> compileAux isExe options source f)

    static let rec compileCompilationAux outputPath (disposals: ResizeArray<IDisposable>) ignoreWarnings (cmpl: Compilation) : (FSharpDiagnostic[] * string) * string list =
        let compilationRefs, deps =
            match cmpl with
            | Compilation(_, _, _, _, cmpls, _) ->
                let compiledRefs =
                    cmpls
                    |> List.map (fun cmpl ->
                            match cmpl with
                            | CompilationReference (cmpl, staticLink) ->
                                compileCompilationAux outputPath disposals ignoreWarnings cmpl, staticLink
                            | TestCompilationReference (cmpl) ->
                                let filename =
                                 match cmpl with
                                 | TestCompilation.CSharp c when not (String.IsNullOrWhiteSpace c.AssemblyName) -> c.AssemblyName
                                 | _ -> Path.GetRandomFileName()
                                let tmp = Path.Combine(outputPath, Path.ChangeExtension(filename, ".dll"))
                                disposals.Add({ new IDisposable with
                                                    member _.Dispose() =
                                                        try File.Delete tmp with | _ -> () })
                                cmpl.EmitAsFile tmp
                                (([||], tmp), []), false)

                let compilationRefs =
                    compiledRefs
                    |> List.map (fun (((errors, outputFilePath), _), staticLink) ->
                        assertErrors 0 ignoreWarnings errors [||]
                        let rOption = "-r:" + outputFilePath
                        if staticLink then
                            [rOption;"--staticlink:" + Path.GetFileNameWithoutExtension outputFilePath]
                        else
                            [rOption])
                    |> List.concat
                    |> Array.ofList

                let deps =
                    compiledRefs
                    |> List.map (fun ((_, deps), _) -> deps)
                    |> List.concat
                    |> List.distinct

                compilationRefs, deps

        let isScript =
            match cmpl with
            | Compilation(_, kind, _, _, _, _) ->
                match kind with
                | Fs -> false
                | Fsx -> true

        let isExe =
            match cmpl with
            | Compilation(_, _, output, _, _, _) ->
                match output with
                | Library -> false
                | Exe -> true

        let source =
            match cmpl with
            | Compilation(source, _, _, _, _, _) -> source

        let options =
            match cmpl with
            | Compilation(_, _, _, options, _, _) -> options

        let nameOpt =
            match cmpl with
            | Compilation(_, _, _, _, _, nameOpt) -> nameOpt

        let disposal, res = compileDisposable outputPath isScript isExe (Array.append options compilationRefs) nameOpt source
        disposals.Add disposal

        let deps2 =
            compilationRefs
            |> Array.filter (fun x -> not (x.Contains("--staticlink")))
            |> Array.map (fun x -> x.Replace("-r:", String.Empty))
            |> List.ofArray

        res, (deps @ deps2)

    static let rec compileCompilation ignoreWarnings (cmpl: Compilation) f =
        let compileDirectory = Path.Combine(Path.GetTempPath(), "CompilerAssert", Path.GetRandomFileName())
        let disposals = ResizeArray()
        try
            Directory.CreateDirectory(compileDirectory) |> ignore
            f (compileCompilationAux compileDirectory disposals ignoreWarnings cmpl)
        finally
            try Directory.Delete compileDirectory with | _ -> ()
            disposals
            |> Seq.iter (fun x -> x.Dispose())

    // NOTE: This function will not clean up all the compiled projects after itself.
    // The reason behind is so we can compose verification of test runs easier.
    // TODO: We must not rely on the filesystem when compiling
    static let rec returnCompilation (cmpl: Compilation) ignoreWarnings =
        let compileDirectory = Path.Combine(Path.GetTempPath(), "CompilerAssert", Path.GetRandomFileName())
        Directory.CreateDirectory(compileDirectory) |> ignore
        compileCompilationAux compileDirectory (ResizeArray()) ignoreWarnings cmpl

    static let executeBuiltAppAndReturnResult (outputFilePath: string) (deps: string list) : (int * string * string) =
        let out = Console.Out
        let err = Console.Error

        let stdout = StringBuilder ()
        let stderr = StringBuilder ()

        let outWriter = new StringWriter (stdout)
        let errWriter = new StringWriter (stderr)

        let mutable exitCode = 0

        try
            try
                Console.SetOut(outWriter)
                Console.SetError(errWriter)
                (executeBuiltApp outputFilePath deps) |> ignore
            with e ->
                let errorMessage = if e.InnerException <> null then (e.InnerException.ToString()) else (e.ToString())
                stderr.Append (errorMessage) |> ignore
                exitCode <- -1
        finally
            Console.SetOut(out)
            Console.SetError(err)
            outWriter.Close()
            errWriter.Close()

        (exitCode, stdout.ToString(), stderr.ToString())

    static let executeBuiltAppNewProcessAndReturnResult (outputFilePath: string) : (int * string * string) =
#if !NETCOREAPP
        let filename = outputFilePath
        let arguments = ""
#else
        let filename = "dotnet"
        let arguments = outputFilePath

        let runtimeconfig = """
{
    "runtimeOptions": {
        "tfm": "net5.0",
        "framework": {
            "name": "Microsoft.NETCore.App",
            "version": "5.0.0"
        }
    }
}"""
        let runtimeconfigPath = Path.ChangeExtension(outputFilePath, ".runtimeconfig.json")
        File.WriteAllText(runtimeconfigPath, runtimeconfig)
        use _disposal =
            { new IDisposable with
              member _.Dispose() = try File.Delete runtimeconfigPath with | _ -> () }
#endif
        let timeout = 30000
        let exitCode, output, errors = Commands.executeProcess (Some filename) arguments (Path.GetDirectoryName(outputFilePath)) timeout
        (exitCode, output |> String.concat "\n", errors |> String.concat "\n")

    static member CompileWithErrors(cmpl: Compilation, expectedErrors, ?ignoreWarnings) =
        let ignoreWarnings = defaultArg ignoreWarnings false
        lock gate (fun () ->
            compileCompilation ignoreWarnings cmpl (fun ((errors, _), _) ->
                assertErrors 0 ignoreWarnings errors expectedErrors))

    static member Compile(cmpl: Compilation, ?ignoreWarnings) =
        CompilerAssert.CompileWithErrors(cmpl, [||], defaultArg ignoreWarnings false)

    static member CompileRaw(cmpl: Compilation, ?ignoreWarnings) =
        lock gate (fun () -> returnCompilation cmpl (defaultArg ignoreWarnings false))

    static member ExecuteAndReturnResult (outputFilePath: string, deps: string list, newProcess: bool) =
        // If we execute in-process (true by default), then the only way of getting STDOUT is to redirect it to SB, and STDERR is from catching an exception.
       if not newProcess then
           executeBuiltAppAndReturnResult outputFilePath deps
       else
           executeBuiltAppNewProcessAndReturnResult outputFilePath

    static member Execute(cmpl: Compilation, ?ignoreWarnings, ?beforeExecute, ?newProcess, ?onOutput) =
        let ignoreWarnings = defaultArg ignoreWarnings false
        let beforeExecute = defaultArg beforeExecute (fun _ _ -> ())
        let newProcess = defaultArg newProcess false
        let onOutput = defaultArg onOutput (fun _ -> ())
        lock gate (fun () ->
            compileCompilation ignoreWarnings cmpl (fun ((errors, outputFilePath), deps) ->
                assertErrors 0 ignoreWarnings errors [||]
                beforeExecute outputFilePath deps
                if newProcess then
                    let (exitCode, output, errors) = executeBuiltAppNewProcessAndReturnResult outputFilePath
                    if exitCode <> 0 then
                        Assert.Fail errors
                    onOutput output
                else
                    executeBuiltApp outputFilePath deps))

    static member ExecutionHasOutput(cmpl: Compilation, expectedOutput: string) =
        CompilerAssert.Execute(cmpl, newProcess = true, onOutput = (fun output -> Assert.AreEqual(expectedOutput, output, sprintf "'%s' = '%s'" expectedOutput output)))

    /// Assert that the given source code compiles with the `defaultProjectOptions`, with no errors or warnings
    static member CompileOfAst isExe source =
        let outputFilePath = Path.ChangeExtension (Path.GetTempFileName(), if isExe then "exe" else ".dll")
        let parseOptions = { FSharpParsingOptions.Default with SourceFiles = [|"test.fs"|] }

        let parseResults = 
            checker.ParseFile("test.fs", SourceText.ofString source, parseOptions) 
            |> Async.RunSynchronously

        Assert.IsEmpty(parseResults.Diagnostics, sprintf "Parse errors: %A" parseResults.Diagnostics)

        let dependencies =
        #if NETCOREAPP
            Array.toList getNetCoreAppReferences
        #else
            []
        #endif

        let compileErrors, statusCode = 
            checker.Compile([parseResults.ParseTree], "test", outputFilePath, dependencies, executable = isExe, noframework = true) 
            |> Async.RunSynchronously

        Assert.IsEmpty(compileErrors, sprintf "Compile errors: %A" compileErrors)
        Assert.AreEqual(0, statusCode, sprintf "Nonzero status code: %d" statusCode)
        outputFilePath

    static member CompileOfAstToDynamicAssembly source =
        let assemblyName = sprintf "test-%O" (Guid.NewGuid())
        let parseOptions = { FSharpParsingOptions.Default with SourceFiles = [|"test.fs"|] }
        let parseResults = 
            checker.ParseFile("test.fs", SourceText.ofString source, parseOptions) 
            |> Async.RunSynchronously
    
        Assert.IsEmpty(parseResults.Diagnostics, sprintf "Parse errors: %A" parseResults.Diagnostics)

        let dependencies =
            #if NETCOREAPP
                Array.toList getNetCoreAppReferences
            #else
                []
            #endif

        let compileErrors, statusCode, assembly = 
            checker.CompileToDynamicAssembly([parseResults.ParseTree], assemblyName, dependencies, None, noframework = true) 
            |> Async.RunSynchronously

        Assert.IsEmpty(compileErrors, sprintf "Compile errors: %A" compileErrors)
        Assert.AreEqual(0, statusCode, sprintf "Nonzero status code: %d" statusCode)
        Assert.IsTrue(assembly.IsSome, "no assembly returned")
        Option.get assembly

    static member Pass (source: string) =
        lock gate <| fun () ->
            let parseResults, fileAnswer = checker.ParseAndCheckFileInProject("test.fs", 0, SourceText.ofString source, defaultProjectOptions) |> Async.RunSynchronously

            Assert.IsEmpty(parseResults.Diagnostics, sprintf "Parse errors: %A" parseResults.Diagnostics)

            match fileAnswer with
            | FSharpCheckFileAnswer.Aborted _ -> Assert.Fail("Type Checker Aborted")
            | FSharpCheckFileAnswer.Succeeded(typeCheckResults) ->

            Assert.IsEmpty(typeCheckResults.Diagnostics, sprintf "Type Check errors: %A" typeCheckResults.Diagnostics)

    static member PassWithOptions options (source: string) =
        lock gate <| fun () ->
            let options = { defaultProjectOptions with OtherOptions = Array.append options defaultProjectOptions.OtherOptions}

            let parseResults, fileAnswer = checker.ParseAndCheckFileInProject("test.fs", 0, SourceText.ofString source, options) |> Async.RunSynchronously

            Assert.IsEmpty(parseResults.Diagnostics, sprintf "Parse errors: %A" parseResults.Diagnostics)

            match fileAnswer with
            | FSharpCheckFileAnswer.Aborted _ -> Assert.Fail("Type Checker Aborted")
            | FSharpCheckFileAnswer.Succeeded(typeCheckResults) ->

            Assert.IsEmpty(typeCheckResults.Diagnostics, sprintf "Type Check errors: %A" typeCheckResults.Diagnostics)

    static member TypeCheckWithErrorsAndOptionsAgainstBaseLine options (sourceDirectory:string) (sourceFile: string) =
        lock gate <| fun () ->
            let absoluteSourceFile = System.IO.Path.Combine(sourceDirectory, sourceFile)
            let parseResults, fileAnswer =
                checker.ParseAndCheckFileInProject(
                    sourceFile,
                    0,
                    SourceText.ofString (File.ReadAllText absoluteSourceFile),
                    { defaultProjectOptions with OtherOptions = Array.append options defaultProjectOptions.OtherOptions; SourceFiles = [|sourceFile|] })
                |> Async.RunSynchronously

            Assert.IsEmpty(parseResults.Diagnostics, sprintf "Parse errors: %A" parseResults.Diagnostics)

            match fileAnswer with
            | FSharpCheckFileAnswer.Aborted _ -> Assert.Fail("Type Checker Aborted")
            | FSharpCheckFileAnswer.Succeeded(typeCheckResults) ->

            let errorsExpectedBaseLine =
                let bslFile = Path.ChangeExtension(absoluteSourceFile, "bsl")
                if not (File.Exists bslFile) then
                    // new test likely initialized, create empty baseline file
                    File.WriteAllText(bslFile, "")
                File.ReadAllText(Path.ChangeExtension(absoluteSourceFile, "bsl"))
            let errorsActual =
                typeCheckResults.Diagnostics
                |> Array.map (sprintf "%A")
                |> String.concat "\n"
            File.WriteAllText(Path.ChangeExtension(absoluteSourceFile,"err"), errorsActual)

            Assert.AreEqual(errorsExpectedBaseLine.Replace("\r\n","\n"), errorsActual.Replace("\r\n","\n"))

    static member TypeCheckWithOptionsAndName options name (source: string) =
        lock gate <| fun () ->
            let errors =
                let parseResults, fileAnswer =
                    checker.ParseAndCheckFileInProject(
                        name,
                        0,
                        SourceText.ofString source,
                        { defaultProjectOptions with OtherOptions = Array.append options defaultProjectOptions.OtherOptions; SourceFiles = [|name|] })
                    |> Async.RunSynchronously

                if parseResults.Diagnostics.Length > 0 then
                    parseResults.Diagnostics
                else

                    match fileAnswer with
                    | FSharpCheckFileAnswer.Aborted _ -> Assert.Fail("Type Checker Aborted"); [| |]
                    | FSharpCheckFileAnswer.Succeeded(typeCheckResults) -> typeCheckResults.Diagnostics

            errors

    static member TypeCheckWithOptions options (source: string) =
        lock gate <| fun () ->
            let errors =
                let parseResults, fileAnswer =
                    checker.ParseAndCheckFileInProject(
                        "test.fs",
                        0,
                        SourceText.ofString source,
                        { defaultProjectOptions with OtherOptions = Array.append options defaultProjectOptions.OtherOptions})
                    |> Async.RunSynchronously

                if parseResults.Diagnostics.Length > 0 then
                    parseResults.Diagnostics
                else

                    match fileAnswer with
                    | FSharpCheckFileAnswer.Aborted _ -> Assert.Fail("Type Checker Aborted"); [| |]
                    | FSharpCheckFileAnswer.Succeeded(typeCheckResults) -> typeCheckResults.Diagnostics

            errors

    static member TypeCheckWithErrorsAndOptionsAndAdjust options libAdjust (source: string) expectedTypeErrors =
        lock gate <| fun () ->
            let errors =
                let parseResults, fileAnswer =
                    checker.ParseAndCheckFileInProject(
                        "test.fs",
                        0,
                        SourceText.ofString source,
                        { defaultProjectOptions with OtherOptions = Array.append options defaultProjectOptions.OtherOptions})
                    |> Async.RunSynchronously

                if parseResults.Diagnostics.Length > 0 then
                    parseResults.Diagnostics
                else

                    match fileAnswer with
                    | FSharpCheckFileAnswer.Aborted _ -> Assert.Fail("Type Checker Aborted"); [| |]
                    | FSharpCheckFileAnswer.Succeeded(typeCheckResults) -> typeCheckResults.Diagnostics

            assertErrors libAdjust false errors expectedTypeErrors


    static member TypeCheckWithErrorsAndOptions options (source: string) expectedTypeErrors =
        CompilerAssert.TypeCheckWithErrorsAndOptionsAndAdjust options 0 (source: string) expectedTypeErrors

    static member TypeCheckWithErrors (source: string) expectedTypeErrors =
        CompilerAssert.TypeCheckWithErrorsAndOptions [||] source expectedTypeErrors

    static member TypeCheckSingleErrorWithOptions options (source: string) (expectedSeverity: FSharpDiagnosticSeverity) (expectedErrorNumber: int) (expectedErrorRange: int * int * int * int) (expectedErrorMsg: string) =
        CompilerAssert.TypeCheckWithErrorsAndOptions options source [| expectedSeverity, expectedErrorNumber, expectedErrorRange, expectedErrorMsg |]

    static member TypeCheckSingleError (source: string) (expectedSeverity: FSharpDiagnosticSeverity) (expectedErrorNumber: int) (expectedErrorRange: int * int * int * int) (expectedErrorMsg: string) =
        CompilerAssert.TypeCheckWithErrors source [| expectedSeverity, expectedErrorNumber, expectedErrorRange, expectedErrorMsg |]

    static member CompileExeWithOptions options (source: string) =
        compile true options source (fun (errors, _) ->
            if errors.Length > 0 then
                Assert.Fail (sprintf "Compile had warnings and/or errors: %A" errors))

    static member CompileExe (source: string) =
        CompilerAssert.CompileExeWithOptions [||] source

    static member CompileExeAndRunWithOptions options (source: string) =
        compile true options source (fun (errors, outputExe) ->

            if errors.Length > 0 then
                Assert.Fail (sprintf "Compile had warnings and/or errors: %A" errors)

            executeBuiltApp outputExe []
        )

    static member CompileExeAndRun (source: string) =
        CompilerAssert.CompileExeAndRunWithOptions [||] source

    static member CompileLibraryAndVerifyILWithOptions options (source: string) (f: ILVerifier -> unit) =
        compile false options source (fun (errors, outputFilePath) ->
            let errors =
                errors |> Array.filter (fun x -> x.Severity = FSharpDiagnosticSeverity.Error)
            if errors.Length > 0 then
                Assert.Fail (sprintf "Compile had errors: %A" errors)

            f (ILVerifier outputFilePath)
        )

    static member CompileLibraryAndVerifyIL (source: string) (f: ILVerifier -> unit) =
        CompilerAssert.CompileLibraryAndVerifyILWithOptions [||] source f

    static member RunScriptWithOptionsAndReturnResult options (source: string) =
        // Intialize output and input streams
        use inStream = new StringReader("")
        use outStream = new StringWriter()
        use errStream = new StringWriter()

        // Build command line arguments & start FSI session
        let argv = [| "C:\\fsi.exe" |]
#if NETCOREAPP
        let args = Array.append argv [|"--noninteractive"; "--targetprofile:netcore"|]
#else
        let args = Array.append argv [|"--noninteractive"; "--targetprofile:mscorlib"|]
#endif
        let allArgs = Array.append args options

        let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
        use fsiSession = FsiEvaluationSession.Create(fsiConfig, allArgs, inStream, outStream, errStream, collectible = true)

        let ch, errors = fsiSession.EvalInteractionNonThrowing source

        let errorMessages = ResizeArray()
        errors
        |> Seq.iter (fun error -> errorMessages.Add(error.Message))

        match ch with
        | Choice2Of2 ex -> errorMessages.Add(ex.Message)
        | _ -> ()

        errorMessages

    static member RunScriptWithOptions options (source: string) (expectedErrorMessages: string list) =
        lock gate <| fun () ->
            let errorMessages = CompilerAssert.RunScriptWithOptionsAndReturnResult options source
            if expectedErrorMessages.Length <> errorMessages.Count then
                Assert.Fail(sprintf "Expected error messages: %A \n\n Actual error messages: %A" expectedErrorMessages errorMessages)
            else
                (expectedErrorMessages, errorMessages)
                ||> Seq.iter2 (fun expectedErrorMessage errorMessage ->
                    Assert.AreEqual(expectedErrorMessage, errorMessage)
            )

    static member RunScript source expectedErrorMessages =
        CompilerAssert.RunScriptWithOptions [||] source expectedErrorMessages

    static member Parse (source: string) =
        let sourceFileName = "test.fs"
        let parsingOptions = { FSharpParsingOptions.Default with SourceFiles = [| sourceFileName |] }
        checker.ParseFile(sourceFileName, SourceText.ofString source, parsingOptions) |> Async.RunSynchronously

    static member ParseWithErrors (source: string) expectedParseErrors =
        let parseResults = CompilerAssert.Parse source

        Assert.True(parseResults.ParseHadErrors)

        let errors =
            parseResults.Diagnostics
            |> Array.distinctBy (fun e -> e.Severity, e.ErrorNumber, e.StartLine, e.StartColumn, e.EndLine, e.EndColumn, e.Message)

        Assert.AreEqual(Array.length expectedParseErrors, errors.Length, sprintf "Parse errors: %A" parseResults.Diagnostics)

        Array.zip errors expectedParseErrors
        |> Array.iter (fun (info, expectedError) ->
            let (expectedSeverity: FSharpDiagnosticSeverity, expectedErrorNumber: int, expectedErrorRange: int * int * int * int, expectedErrorMsg: string) = expectedError
            Assert.AreEqual(expectedSeverity, info.Severity)
            Assert.AreEqual(expectedErrorNumber, info.ErrorNumber, "expectedErrorNumber")
            Assert.AreEqual(expectedErrorRange, (info.StartLine, info.StartColumn + 1, info.EndLine, info.EndColumn + 1), "expectedErrorRange")
            Assert.AreEqual(expectedErrorMsg, info.Message, "expectedErrorMsg")
        )
