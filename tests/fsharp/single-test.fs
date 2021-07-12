module SingleTest

open System
open System.IO
open System.Reflection
open TestFramework
open HandleExpects
open FSharp.Compiler.IO

type Permutation =
    | FSC_CORECLR
    | FSC_CORECLR_OPT_MINUS
    | FSC_CORECLR_BUILDONLY
    | FSI_CORECLR
#if !NETCOREAPP
    | FSI_FILE
    | FSI_STDIN
    | GENERATED_SIGNATURE
    | FSC_BUILDONLY
    | FSC_OPT_MINUS_DEBUG
    | FSC_OPT_PLUS_DEBUG
    | AS_DLL
#endif

// Because we build programs ad dlls the compiler will copy an fsharp.core.dll into the build directory
// peverify will fail if fsharp.core.dll is not found or is the wrong one.
// This ensures that we delete any fsharp.core.dlls when we start the build and also when the singleTestBuild and run is finished.
let cleanUpFSharpCore cfg =
    let removeFSharpCore () =
        if fileExists cfg "FSharp.Core.dll" then rm cfg "FSharp.Core.dll"
    removeFSharpCore ()
    { new System.IDisposable with member x.Dispose() = removeFSharpCore () }

// Generate a project files
let emitFile filename (body:string) =
    try
        // Create a file to write to
        use sw = File.CreateText(filename)
        sw.WriteLine(body)
    with | _ -> ()

let copyFilesToDest sourceDir destDir =
    let filenames = Directory.GetFiles(sourceDir, "*", SearchOption.TopDirectoryOnly)
    for file in filenames do
        let dest = Path.Combine(destDir, Path.GetFileName(file))
        File.Copy(file, dest)

type CompileItem = Reference | Compile | UseSource | LoadSource

type OutputType = Library | Exe | Script

type ProjectConfiguration = {
    OutputType:OutputType
    Framework:string
    SourceDirectory:string
    SourceItems:string list
    ExtraSourceItems:string list
    UtilitySourceItems:string list
    ReferenceItems:string list
    LoadSources:string list
    UseSources:string list
    Optimize:bool
}

let replaceTokens tag (replacement:string) (template:string) = template.Replace(tag, replacement)

let generateProps testCompilerVersion configuration =
    let template = @"<Project>
  <PropertyGroup>
    <Configuration Condition=""'$(Configuration)' == ''"">$(TESTCONFIGURATION)</Configuration>
    <FSharpTestCompilerVersion>$(TESTCOMPILERVERSION)</FSharpTestCompilerVersion>
  </PropertyGroup>
  <Import Project=""$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(PROJECTDIRECTORY)'))"" />
</Project>"
    template
    |> replaceTokens "$(TESTCONFIGURATION)" configuration
    |> replaceTokens "$(PROJECTDIRECTORY)" (Path.GetFullPath(__SOURCE_DIRECTORY__))
    |> replaceTokens "$(TESTCOMPILERVERSION)" testCompilerVersion

let generateTargets =
    let template = @"<Project>
  <Import Project=""$([MSBuild]::GetPathOfFileAbove('Directory.Build.targets', '$(PROJECTDIRECTORY)'))"" />
  <Import Project=""$(MSBuildThisFileDirectory)Directory.Overrides.targets"" Condition=""'$(OutputType)'=='Script'"" />
</Project>"
    template
    |> replaceTokens "$(PROJECTDIRECTORY)" (Path.GetFullPath(__SOURCE_DIRECTORY__))

let generateOverrides =
    let template = @"<Project>
  <Target Name=""Build"" DependsOnTargets=""RunFSharpScript"" />
  <Target Name=""Rebuild"" DependsOnTargets=""RunFSharpScript"" />
</Project>"
    template

// Arguments:
//    pc = ProjectConfiguration
//    outputType = OutputType.Exe, OutputType.Library or OutputType.Script
//    targetFramework optimize = "net472" or net5.0 etc ...
//    optimize = true or false
//    configuration = "Release" or "Debug"
//
let generateProjectArtifacts (pc:ProjectConfiguration) outputType (targetFramework:string) configuration languageVersion=
    let fsharpCoreLocation =
        let compiler =
            if outputType = OutputType.Script then
                "fsi"
            else
                "FSharp.Core"
        (Path.GetFullPath(__SOURCE_DIRECTORY__) + "/../../artifacts/bin/"  + compiler + "/" + configuration + "/netstandard2.0/FSharp.Core.dll")

    let computeSourceItems addDirectory addCondition (compileItem:CompileItem) sources =
        let computeInclude src =
            let fileName = if addDirectory then Path.Combine(pc.SourceDirectory, src) else src
            let condition = if addCondition then " Condition=\"Exists('" + fileName + "')\"" else ""
            match compileItem with
            | CompileItem.Compile ->
                "\n    <Compile Include='" + fileName + "'" + condition + " />"
            | CompileItem.Reference ->
                "\n    <Reference Include='" + fileName + "'" + condition + " />"
            | CompileItem.UseSource ->
                "\n    <UseSource Include='" + fileName + "'" + condition + " />"
            | CompileItem.LoadSource ->
                "\n    <LoadSource Include='" + fileName + "'" + condition + " />"

        sources
        |> List.map(fun src -> computeInclude src)
        |> List.fold (fun acc s -> acc + s) ""

    let replace tag items addDirectory addCondition compileItem (template:string) = template.Replace(tag, computeSourceItems addDirectory addCondition compileItem items)

    let outputType =
        match pc.OutputType with
        | OutputType.Script -> "Script"
        | _ -> "Exe"
    let optimize = if pc.Optimize then "True" else "False"
    let debug = if pc.Optimize then "True" else "False"
    let generateProjBody =
        let template = @"<Project Sdk='Microsoft.NET.Sdk'>

  <PropertyGroup>
    <OutputType>$(OUTPUTTYPE)</OutputType>
    <TargetFramework>$(TARGETFRAMEWORK)</TargetFramework>
    <DisableImplicitFSharpCoreReference>true</DisableImplicitFSharpCoreReference>
    <IsPackable>false</IsPackable>
    <DebugSymbols>$(DEBUG)</DebugSymbols>
    <DebugType>portable</DebugType>
    <LangVersion>$(LANGUAGEVERSION)</LangVersion>
    <Optimize>$(OPTIMIZE)</Optimize>
    <SignAssembly>false</SignAssembly>
    <DefineConstants Condition=""'$(OutputType)' == 'Script' and '$(FSharpTestCompilerVersion)' == 'coreclr'"">NETCOREAPP</DefineConstants>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <RestoreAdditionalProjectSources Condition = "" '$(RestoreAdditionalProjectSources)' == ''"">$(RestoreFromArtifactsPath)</RestoreAdditionalProjectSources>
    <RestoreAdditionalProjectSources Condition = "" '$(RestoreAdditionalProjectSources)' != ''"">$(RestoreAdditionalProjectSources);$(RestoreFromArtifactsPath)</RestoreAdditionalProjectSources>
  </PropertyGroup>

  <!-- FSharp.Core reference -->
  <ItemGroup>
    <Reference Include='FSharp.Core'>
        <HintPath>$(FSHARPCORELOCATION)</HintPath>
    </Reference>
  </ItemGroup>

  <ItemGroup>$(UTILITYSOURCEITEMS)
  </ItemGroup>

  <!-- Sources -->
  <ItemGroup>$(SOURCEITEMS)
  </ItemGroup>

  <!-- Extra sources -->
  <ItemGroup>$(EXTRASOURCEITEMS)
  </ItemGroup>

  <!-- References -->
  <ItemGroup>$(REFERENCEITEMS)
    <Reference Condition=""$(TargetFramework.StartsWith('net4'))"" Include=""System.Windows.Forms"" />
    <Reference Condition=""$(TargetFramework.StartsWith('net4'))"" Include=""System.Web"" />
  </ItemGroup>

  <Target Name='CopyCustomContentOnPublish' AfterTargets='Build'>
    <ItemGroup>
        <Libraries Include='*.dll' />
    </ItemGroup>
    <Copy SourceFiles='@(Libraries)' DestinationFolder='$(OutputPath)' SkipUnchangedFiles='false' />
  </Target>

</Project>"
        template
        |> replace "$(UTILITYSOURCEITEMS)" pc.UtilitySourceItems false false CompileItem.Compile
        |> replace "$(SOURCEITEMS)" pc.SourceItems true false CompileItem.Compile
        |> replace "$(EXTRASOURCEITEMS)" pc.ExtraSourceItems true true CompileItem.Compile
        |> replace "$(REFERENCEITEMS)" pc.ReferenceItems true true CompileItem.Reference
        |> replace "$(LOADSOURCEITEMS)" pc.LoadSources true true CompileItem.LoadSource
        |> replace "$(USESOURCEITEMS)" pc.UseSources true true CompileItem.UseSource
        |> replaceTokens "$(FSHARPCORELOCATION)" fsharpCoreLocation
        |> replaceTokens "$(DIRECTORYBUILDLOCATION)" (Path.GetFullPath(__SOURCE_DIRECTORY__))
        |> replaceTokens "$(OUTPUTTYPE)" outputType
        |> replaceTokens "$(OPTIMIZE)" optimize
        |> replaceTokens "$(DEBUG)" debug
        |> replaceTokens "$(TARGETFRAMEWORK)" targetFramework
        |> replaceTokens "$(LANGUAGEVERSION)" languageVersion
        |> replaceTokens "$(RestoreFromArtifactsPath)" (Path.GetFullPath(__SOURCE_DIRECTORY__) + "/../../artifacts/packages/" + configuration)
    generateProjBody

let lockObj = obj()
let singleTestBuildAndRunCore cfg copyFiles p languageVersion =
    let sources = []
    let loadSources = []
    let useSources = []
    let extraSources = ["testlib.fsi";"testlib.fs";"test.mli";"test.ml";"test.fsi";"test.fs";"test2.fsi";"test2.fs";"test.fsx";"test2.fsx"]
    let utilitySources = [__SOURCE_DIRECTORY__  ++ "coreclr_utilities.fs"]
    let referenceItems =  if String.IsNullOrEmpty(copyFiles) then [] else [copyFiles]
    let framework = "net5.0"

    // Arguments:
    //    outputType = OutputType.Exe, OutputType.Library or OutputType.Script
    //    compilerType = "coreclr" or "net40"
    //    targetFramework optimize = "net472" OR net5.0 etc ...
    //    optimize = true or false
    let executeSingleTestBuildAndRun outputType compilerType targetFramework optimize buildOnly =
        let mutable result = false
        let directory =
            let mutable result = ""
            lock lockObj <| (fun () ->
                let rec loop () =
                    let pathToArtifacts = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "../../../.."))
                    if Path.GetFileName(pathToArtifacts) <> "artifacts" then failwith "FSharp.Cambridge did not find artifacts directory --- has the location changed????"
                    let pathToTemp = Path.Combine(pathToArtifacts, "Temp")
                    let projectDirectory = Path.Combine(pathToTemp, "FSharp.Cambridge", Path.GetRandomFileName())
                    if Directory.Exists(projectDirectory) then
                        loop ()
                    else
                        Directory.CreateDirectory(projectDirectory) |>ignore
                        projectDirectory
                result <- loop())
            result

        let pc = {
            OutputType = outputType
            Framework = framework
            SourceDirectory = cfg.Directory
            SourceItems = sources
            ExtraSourceItems = extraSources
            UtilitySourceItems = utilitySources
            ReferenceItems = referenceItems
            LoadSources = loadSources
            UseSources = useSources
            Optimize = optimize
        }

        let findFirstSourceFile (pc:ProjectConfiguration)  =
            let sources = List.append pc.SourceItems pc.ExtraSourceItems
            let found = sources |> List.tryFind(fun source -> FileSystem.FileExistsShim(Path.Combine(directory, source)))
            match found with
            | Some p -> Path.Combine(directory, p)
            | None -> failwith "Missing SourceFile in test case"

        let targetsBody = generateTargets
        let overridesBody = generateOverrides
        let targetsFileName = Path.Combine(directory, "Directory.Build.targets")
        let propsFileName = Path.Combine(directory, "Directory.Build.props")
        let overridesFileName = Path.Combine(directory, "Directory.Overrides.targets")
        let projectFileName = Path.Combine(directory, Path.GetRandomFileName() + ".fsproj")
        try
            // Clean up directory
            Directory.CreateDirectory(directory) |> ignore
            copyFilesToDest cfg.Directory directory
            try File.Delete(Path.Combine(directory, "FSharp.Core.dll")) with _ -> ()
            emitFile targetsFileName targetsBody
            emitFile overridesFileName overridesBody
            let buildOutputFile = Path.Combine(directory, "buildoutput.txt")
            if outputType = OutputType.Exe then
                let executeFsc testCompilerVersion targetFramework =
                    let propsBody = generateProps testCompilerVersion cfg.BUILD_CONFIG
                    emitFile propsFileName propsBody
                    let projectBody = generateProjectArtifacts pc outputType targetFramework cfg.BUILD_CONFIG languageVersion
                    emitFile projectFileName projectBody
                    use testOkFile = new FileGuard(Path.Combine(directory, "test.ok"))
                    let cfg = { cfg with Directory = directory }
                    let result = execBothToOutNoCheck cfg directory buildOutputFile cfg.DotNetExe  (sprintf "run -f %s" targetFramework)
                    if not (buildOnly) then
                        result |> checkResult
                        testOkFile.CheckExists()
                executeFsc compilerType targetFramework
                if buildOnly then verifyResults (findFirstSourceFile pc) buildOutputFile
            else
                let executeFsi testCompilerVersion targetFramework =
                    let propsBody = generateProps testCompilerVersion cfg.BUILD_CONFIG
                    emitFile propsFileName propsBody
                    let projectBody = generateProjectArtifacts pc outputType  targetFramework cfg.BUILD_CONFIG languageVersion
                    emitFile projectFileName projectBody
                    use testOkFile = new FileGuard(Path.Combine(directory, "test.ok"))
                    let cfg = { cfg with Directory = directory }
                    execBothToOut cfg directory buildOutputFile cfg.DotNetExe "build /t:RunFSharpScript"
                    testOkFile.CheckExists()
                executeFsi compilerType targetFramework
            result <- true
        finally
            if result <> false then
                try Directory.Delete(directory, true) with _ -> ()
            else
                printfn "Configuration: %s" cfg.Directory
                printfn "Directory: %s" directory
                printfn "Filename: %s" projectFileName

    match p with
    | FSC_CORECLR -> executeSingleTestBuildAndRun OutputType.Exe "coreclr" "net5.0" true false
    | FSC_CORECLR_OPT_MINUS -> executeSingleTestBuildAndRun OutputType.Exe "coreclr" "net5.0" false false
    | FSC_CORECLR_BUILDONLY -> executeSingleTestBuildAndRun OutputType.Exe "coreclr" "net5.0" true true
    | FSI_CORECLR -> executeSingleTestBuildAndRun OutputType.Script "coreclr" "net5.0" true false

#if !NETCOREAPP
    | FSC_BUILDONLY -> executeSingleTestBuildAndRun OutputType.Exe "net40" "net472" false true
    | FSC_OPT_PLUS_DEBUG -> executeSingleTestBuildAndRun OutputType.Exe "net40" "net472" true false
    | FSC_OPT_MINUS_DEBUG -> executeSingleTestBuildAndRun OutputType.Exe "net40" "net472" false false
    | FSI_FILE -> executeSingleTestBuildAndRun OutputType.Script "net40" "net472" true false

    | FSI_STDIN ->
        use cleanup = (cleanUpFSharpCore cfg)
        use testOkFile = new FileGuard (getfullpath cfg "test.ok")
        let sources = extraSources |> List.filter (fileExists cfg)

        fsiStdin cfg (sources |> List.rev |> List.head) "" [] //use last file, because `cmd < a.txt b.txt` redirect b.txt only

        testOkFile.CheckExists()

    | GENERATED_SIGNATURE ->
        use cleanup = (cleanUpFSharpCore cfg)

        let source1 =
            ["test.ml"; "test.fs"; "test.fsx"]
            |> List.rev
            |> List.tryFind (fileExists cfg)

        source1 |> Option.iter (fun from -> copy_y cfg from "tmptest.fs")

        log "Generated signature file..."
        fsc cfg "%s --sig:tmptest.fsi --define:GENERATED_SIGNATURE" cfg.fsc_flags ["tmptest.fs"]
        (if FileSystem.FileExistsShim("FSharp.Core.dll") then log "found fsharp.core.dll after build" else log "found fsharp.core.dll after build") |> ignore

        log "Compiling against generated signature file..."
        fsc cfg "%s -o:tmptest1.exe" cfg.fsc_flags ["tmptest.fsi";"tmptest.fs"]
        (if FileSystem.FileExistsShim("FSharp.Core.dll") then log "found fsharp.core.dll after build" else log "found fsharp.core.dll after build") |> ignore

        log "Verifying built .exe..."
        peverify cfg "tmptest1.exe"

    | AS_DLL ->
        // Compile as a DLL to exercise pickling of interface data, then recompile the original source file referencing this DLL
        // THe second compilation will not utilize the information from the first in any meaningful way, but the
        // compiler will unpickle the interface and optimization data, so we test unpickling as well.
        use cleanup = (cleanUpFSharpCore cfg)
        use testOkFile = new FileGuard (getfullpath cfg "test.ok")

        let sources = extraSources |> List.filter (fileExists cfg)

        fsc cfg "%s --optimize -a -o:test--optimize-lib.dll -g --langversion:preview " cfg.fsc_flags sources
        fsc cfg "%s --optimize -r:test--optimize-lib.dll -o:test--optimize-client-of-lib.exe -g --langversion:preview " cfg.fsc_flags sources

        peverify cfg "test--optimize-lib.dll"
        peverify cfg "test--optimize-client-of-lib.exe"

        exec cfg ("." ++ "test--optimize-client-of-lib.exe") ""

        testOkFile.CheckExists()
#endif

let singleTestBuildAndRunAux cfg p =
    singleTestBuildAndRunCore cfg "" p "latest"

let singleTestBuildAndRunWithCopyDlls  cfg copyFiles p =
    singleTestBuildAndRunCore cfg copyFiles p "latest"

let singleTestBuildAndRun dir p =
    let cfg = testConfig dir
    singleTestBuildAndRunAux cfg p

let singleTestBuildAndRunVersion dir p version =
    let cfg = testConfig dir
    singleTestBuildAndRunCore cfg "" p version

let singleVersionedNegTest (cfg: TestConfig) version testname =

    let cfg = {
        cfg with
            fsc_flags = sprintf "%s %s --define:NEGATIVE" cfg.fsc_flags (if not (String.IsNullOrEmpty(version)) then "--langversion:" + version else "")
            fsi_flags = sprintf "%s %s" cfg.fsi_flags (if not (String.IsNullOrEmpty(version)) then "--langversion:" + version else "")
            }

    // REM == Set baseline (fsc vs vs, in case the vs baseline exists)
    let VSBSLFILE =
        if (sprintf "%s.vsbsl" testname) |> (fileExists cfg)
        then sprintf "%s.vsbsl" testname
        else sprintf "%s.bsl" testname

    let sources = [
        let src = [ testname + ".mli"; testname + ".fsi"; testname + ".ml"; testname + ".fs"; testname +  ".fsx";
                    testname + "a.mli"; testname + "a.fsi"; testname + "a.ml"; testname + "a.fs";
                    testname + "b.mli"; testname + "b.fsi"; testname + "b.ml"; testname + "b.fs"; ]

        yield! src |> List.filter (fileExists cfg)

        if fileExists cfg "helloWorldProvider.dll" then
            yield "-r:helloWorldProvider.dll"

        if fileExists cfg (testname + "-pre.fs") then
            yield (sprintf "-r:%s-pre.dll" testname)

        ]

    if fileExists cfg (testname + "-pre.fs")
        then
            fsc cfg "%s -a -o:%s-pre.dll" cfg.fsc_flags testname [testname + "-pre.fs"]
        else ()

    if fileExists cfg (testname + "-pre.fsx") then
        fsi_script cfg "--exec %s %s %s"
               cfg.fsi_flags
               (cfg.Directory ++ (testname + "-pre.fsx"))
               ""
               []

    log "Negative typechecker testing: %s" testname

    let warnaserror =
        if cfg.fsc_flags.Contains("--warnaserror-") then String.Empty
        else "--warnaserror"

    fscAppendErrExpectFail cfg  (sprintf "%s.err" testname) """%s --vserrors %s --nologo --maxerrors:10000 -a -o:%s.dll""" cfg.fsc_flags warnaserror testname sources

    let diff = fsdiff cfg (sprintf "%s.err" testname) (sprintf "%s.bsl" testname)

    fscAppendErrExpectFail cfg (sprintf "%s.vserr" testname) "%s --test:ContinueAfterParseFailure --vserrors %s --nologo --maxerrors:10000 -a -o:%s.dll" cfg.fsc_flags warnaserror testname sources

    let vbslDiff = fsdiff cfg (sprintf "%s.vserr" testname) VSBSLFILE

    match diff,vbslDiff with
    | "","" ->
        log "Good, output %s.err matched %s.bsl" testname testname
        log "Good, output %s.vserr matched %s" testname VSBSLFILE
    | l,"" ->
        log "***** %s.err %s.bsl differed: a bug or baseline may need updating" testname testname
        failwithf "%s.err %s.bsl differ; %A" testname testname l
    | "",l ->
        log "Good, output %s.err matched %s.bsl" testname testname
        log "***** %s.vserr %s differed: a bug or baseline may need updating" testname VSBSLFILE
        failwithf "%s.vserr %s differ; %A" testname VSBSLFILE l
    | l1,l2 ->
        log "***** %s.err %s.bsl differed: a bug or baseline may need updating" testname testname
        log "***** %s.vserr %s differed: a bug or baseline may need updating" testname VSBSLFILE
        failwithf "%s.err %s.bsl differ; %A; %s.vserr %s differ; %A" testname testname l1 testname VSBSLFILE l2

let singleNegTest (cfg: TestConfig) testname = singleVersionedNegTest (cfg: TestConfig) "" testname
