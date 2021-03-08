// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Tests.ProjectSystem

// System namespaces
open System
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Text
open System.Text.RegularExpressions

// VS namespaces 
open Microsoft.VisualStudio
open Microsoft.VisualStudio.Shell
open Microsoft.VisualStudio.Shell.Interop
open Microsoft.VisualStudio.FSharp.ProjectSystem
open Microsoft.VisualStudio.FSharp.Editor

// Internal unittest namespaces
open NUnit.Framework
open Salsa
open UnitTests.TestLib.Utils.Asserts
open UnitTests.TestLib.Utils.FilesystemHelpers
open UnitTests.TestLib.ProjectSystem

[<TestFixture>][<Category "ProjectSystem">]
type Miscellaneous() = 
    inherit TheTests()

    //TODO: look for a way to remove the helper functions
    /////////////////////////////////
    // project helpers
    static let SaveProject(project : UnitTestingFSharpProjectNode) =
        project.Save(null, 1, 0u) |> ignore

    //[<Test>]   // keep disabled unless trying to prove that UnhandledExceptionHandler is working 
    member public this.EnsureThatUnhandledExceptionsCauseAnAssert() =
        this.MakeProjectAndDo([], ["System"], "", (fun proj ->
            let t = new System.Threading.Thread(new System.Threading.ThreadStart(fun () -> failwith "foo"))
            t.Start()
            System.Threading.Thread.Sleep(1000)
        ))

    [<Test>]
    member public this.``Miscellaneous.CreatePropertiesObject`` () =
        DoWithTempFile "Test.fsproj" (fun projFile ->
            File.AppendAllText(projFile, TheTests.SimpleFsprojText([], [], ""))
            use project = TheTests.CreateProject(projFile) 
            let prop = project.CreatePropertiesObject()
            Assert.AreEqual(typeof<FSharpProjectNodeProperties>, prop.GetType())
            )
            
    [<Test>]
    member public this.``Miscellaneous.TestProperties`` () =
        DoWithTempFile "Test.fsproj" (fun projFile ->
            File.AppendAllText(projFile, TheTests.SimpleFsprojText([], [], ""))
            use project = TheTests.CreateProject(projFile) 
            let prop = new FSharpProjectNodeProperties(project)
            
            prop.AssemblyName <- "a"
            Assert.AreEqual("a", prop.AssemblyName)            
            
            // Output type and output file name
            prop.OutputType <- OutputType.Exe
            Assert.AreEqual(OutputType.Exe, prop.OutputType)
            Assert.AreEqual("a.exe", prop.OutputFileName)
            
            prop.OutputType <- OutputType.Library
            Assert.AreEqual(OutputType.Library, prop.OutputType)
            Assert.AreEqual("a.dll", prop.OutputFileName)
            
            prop.OutputType <- OutputType.WinExe
            Assert.AreEqual(OutputType.WinExe, prop.OutputType)
            Assert.AreEqual("a.exe", prop.OutputFileName)
            )            
            
    [<Test>]
    member public this.``Miscellaneous.CreateServices`` () =
        DoWithTempFile "Test.fsproj" (fun projFile ->
            File.AppendAllText(projFile, TheTests.SimpleFsprojText([], [], ""))
            use project = TheTests.CreateProject(projFile) 
            let proj = project.CreateServices(typeof<VSLangProj.VSProject>)
            Assert.AreEqual(typeof<Microsoft.VisualStudio.FSharp.ProjectSystem.Automation.OAVSProject>, proj.GetType())
            let eproj = project.CreateServices(typeof<EnvDTE.Project>)
            Assert.AreEqual(typeof<Microsoft.VisualStudio.FSharp.ProjectSystem.Automation.OAProject>, eproj.GetType())
            let badservice = project.CreateServices(typeof<string>)
            Assert.IsNull(badservice)
            )
            
    [<Test>]
    member public this.``Miscellaneous.FSharpFileNode.RelativeFilePath`` () =
        this.MakeProjectAndDo(["orig1.fs"], [], "", (fun project ->
            let absFilePath = Path.Combine(project.ProjectFolder, "orig1.fs")
            let files = new List<FSharpFileNode>()
            project.FindNodesOfType(files)
            Assert.AreEqual(1, files.Count)
            let file = files.[0]
            let path = file.RelativeFilePath
            Assert.AreEqual("orig1.fs", path)
            ))
           
    [<Test>]
    member public this.``Miscellaneous.FSharpFileNode.CreateServices`` () =
        this.MakeProjectAndDo(["orig1.fs"], [], "", (fun project ->
            let absFilePath = Path.Combine(project.ProjectFolder, "orig1.fs")
            let files = new List<FSharpFileNode>()
            project.FindNodesOfType(files)
            Assert.AreEqual(1, files.Count)
            let file = files.[0]
            let badservice = file.CreateServices(typeof<string>)
            Assert.IsNull(badservice)
            let eproj = file.CreateServices(typeof<EnvDTE.ProjectItem>)
            Assert.AreEqual(typeof<Microsoft.VisualStudio.FSharp.ProjectSystem.Automation.OAFileItem>, eproj.GetType())
            ))

    
    //[<Test>]    
    member public this.AttemptDragAndDrop() =
        printfn "starting..."
        let fsproj = "D:\Depot\staging\Test.fsproj"
        printfn "here1"
        let buildEngine = Utilities.InitializeMsBuildEngine(null)
        printfn "here2"
        let buildProject = Utilities.InitializeMsBuildProject(buildEngine, fsproj, null)
        printfn "here3"
        let package = new FSharpProjectPackage()
        let project = new UnitTestingFSharpProjectNode(package)
        //let dte = Package.GetGlobalService(typeof<EnvDTE.DTE>)
        //let dte = System.Runtime.InteropServices.Marshal.GetActiveObject("VisualStudio.DTE.8.0") :?> EnvDTE80.DTE2
        //let dteServiceProvider = dte :?> Microsoft.VisualStudio.OLE.Interop.IServiceProvider
        //project.SetSite(dteServiceProvider) |> ignore
        let ivstrk = (new ServiceProvider(VsMocks.mockServiceProvider)).GetService(typeof<SVsTrackProjectDocuments>) :?> IVsTrackProjectDocuments2
        printfn "%A" ivstrk
        project.SetSite(VsMocks.mockServiceProvider) |> ignore
        printfn "here4"
        project.BuildProject <- buildProject
        let mutable cancelled = 0
        let mutable guid = Guid.NewGuid()
        printfn "about to load .fsproj"
        project.Load(fsproj, null, null, 2u, &guid, &cancelled)
        printfn "loaded"
        let mutable dwEffect = 0u
        let mutable iOleDataObject = null
        let mutable iDropSource = null
        project.GetDropInfo(&dwEffect, &iOleDataObject, &iDropSource) |> ValidateOK
        // REVIEW validate dwEffect
        let mutable keyboardState = 1u
        let mutable node = project.FirstChild
        let mutable finished = false
        printfn "find file..."
        while not finished do
            match node with
            | :? FSharpFileNode as fileNode ->
                printfn "file %s" fileNode.FileName 
                if fileNode.FileName = "aaa.fs" then
                    finished <- true
            | _ ->
                node <- node.NextSibling 
                if node = null then
                    finished <- true
        Assert.AreNotEqual(node, null)
        let itemId = node.ID
        
        project.DragEnter(iOleDataObject, keyboardState, itemId, &dwEffect) |> ignore
        ()

    
    [<Test>]
    member public this.``Automation.OutputGroup.OUTPUTLOC``() =
        this.MakeProjectAndDoWithProjectFile(["foo.fs"], [],
            this.MSBuildProjectMultiPlatform(["x86",""],"x86"),
            (fun project projFileName ->
                let prjCfg = project.ConfigProvider.GetProjectConfiguration(new ConfigCanonicalName("Debug", "x86")) :> IVsProjectCfg2
                let mutable outputGroup : IVsOutputGroup = null
                prjCfg.OpenOutputGroup("Built", &outputGroup) |> ValidateOK
                let mutable keyOutput : IVsOutput2 = null
                (outputGroup :?> IVsOutputGroup2).get_KeyOutputObject(&keyOutput) |> ValidateOK
                let mutable value : obj = null
                keyOutput.get_Property("OUTPUTLOC", &value) |> ValidateOK
                let expectedOutput = (projFileName |> Path.GetDirectoryName) + @"\bin\Debug\Blah.dll"
                AssertEqual expectedOutput (value :?> string)
            )
         )

    [<Test>]
    member public this.``Automation.OutputGroups``() =
        DoWithTempFile "Test.fsproj" (fun file ->
            let text = TheTests.FsprojTextWithProjectReferences([],[],[],@"
                <PropertyGroup>
                    <DocumentationFile>Out.xml</DocumentationFile>
                </PropertyGroup>
                <ItemGroup>
                    <Compile Include=""foo.fs"" />
                    <EmbeddedResource Include=""Blah.resx"" />
                    <Content Include=""Yadda.txt"" />
                </ItemGroup>")
            File.AppendAllText(file, text)
            let dirName = Path.GetDirectoryName(file)
            let sp, cnn = VsMocks.MakeMockServiceProviderAndConfigChangeNotifier()
            let project = TheTests.CreateProject(file, "false", cnn, sp)            
            use project = project
            let prjCfg = project.ConfigProvider.GetProjectConfiguration(new ConfigCanonicalName("Debug","AnyCPU")) :> IVsProjectCfg2
            let count = [| 0u |]
            prjCfg.get_OutputGroups(0u, null, count) |> ValidateOK
            let ogs : array<IVsOutputGroup> = Array.create (int count.[0]) null
            prjCfg.get_OutputGroups(count.[0], ogs, count)  |> ValidateOK
            let ogs : array<IVsOutputGroup2> = ogs |> Array.map (fun x -> downcast x)
            let ogInfos = 
                [for og in ogs do
                    let mutable canonicalName = ""
                    og.get_CanonicalName(&canonicalName) |> ValidateOK
                    let mutable description = ""
                    og.get_Description(&description) |> ValidateOK 
                    let mutable displayName = ""
                    og.get_DisplayName(&displayName) |> ValidateOK 
                    let mutable keyOutput = ""
                    let keyOutputResult = og.get_KeyOutput(&keyOutput)
                    let count = [| 0u |]
                    og.get_Outputs(0u, null, count) |> ValidateOK 
                    let os : array<IVsOutput2> = Array.create (int count.[0]) null
                    og.get_Outputs(count.[0], os, count) |> ValidateOK 
                    yield canonicalName, description, displayName, keyOutput, keyOutputResult, [
                        for o in os do
                            let mutable canonicalName = ""
                            o.get_CanonicalName(&canonicalName) |> ValidateOK 
                            let mutable url = ""
                            o.get_DeploySourceURL(&url) |> ValidateOK 
                            let mutable displayName = ""
                            o.get_DisplayName(&displayName) |> ValidateOK 
                            let mutable relativeUrl = ""
                            o.get_RootRelativeURL(&relativeUrl) |> ValidateOK
                            yield canonicalName, url, displayName, relativeUrl]
                ]
            let expected =
                ["Built", "Contains the DLL or EXE built by the project.", "Primary Output", dirName+ @"\obj\Debug\Test.exe", 0, [dirName+ @"\obj\Debug\Test.exe", "file:///"+dirName+ @"\obj\Debug\Test.exe", dirName+ @"\obj\Debug\Test.exe", "Test.exe"]
                 "ContentFiles", "Contains all content files in the project.", "Content Files", "", 1, [dirName+ @"\Yadda.txt", "file:///"+dirName+ @"\Yadda.txt", dirName+ @"\Yadda.txt", "Yadda.txt"]
                 "LocalizedResourceDlls", "Contains the satellite assemblies for each culture's resources.", "Localized Resources", "", 1, []
                 "Documentation", "Contains the XML Documentation files for the project.", "Documentation Files", dirName+ @"\Out.xml", 0, [dirName+ @"\Out.xml", "file:///"+dirName+ @"\Out.xml", dirName+ @"\Out.xml", "Out.xml"]
                 "Symbols", "Contains the debugging files for the project.", "Debug Symbols", "", 1, [dirName+ @"\obj\Debug\Test.pdb", "file:///"+dirName+ @"\obj\Debug\Test.pdb", dirName+ @"\obj\Debug\Test.pdb", "Test.pdb"]
                 "SourceFiles", "Contains all source files in the project.", "Source Files", "", 1, [dirName+ @"\foo.fs", "file:///"+dirName+ @"\foo.fs", dirName+ @"\foo.fs", "foo.fs"
                                                                                                     dirName+ @"\Blah.resx", "file:///"+dirName+ @"\Blah.resx", dirName+ @"\Blah.resx", "Blah.resx"
                                                                                                     dirName+ @"\Test.fsproj", "file:///"+dirName+ @"\Test.fsproj", dirName+ @"\Test.fsproj", "Test.fsproj"]
                 "XmlSerializer", "Contains the XML serialization assemblies for the project.", "XML Serialization Assemblies", "", 1, []]
            AssertEqual expected ogInfos
        )

    [<Test>]
    member public this.``LoadProject.x86`` () =
        this.MakeProjectAndDoWithProjectFile(["foo.fs"], ["System"],
            this.MSBuildProjectMultiPlatform(["x86",""],"x86"),
            (fun project projFileName ->
                this.CheckPlatformNames(project, [|"x86"|])
                let refContainer =
                    let l = new List<ReferenceContainerNode>()
                    project.FindNodesOfType(l)
                    l.[0]
                let l = new List<AssemblyReferenceNode>()
                refContainer.FindNodesOfType<AssemblyReferenceNode>(l)
                AssertEqual 1 l.Count
        ))

    [<Test>]
    member public this.``BuildAndClean``() =
        this.MakeProjectAndDoWithProjectFileAndConfigChangeNotifier(["foo.fs"], [], 
             this.MSBuildProjectBoilerplate "Library", 
             (fun project ccn projFileName ->
                let fooPath = Path.Combine(project.ProjectFolder, "foo.fs")
                File.AppendAllLines(fooPath, ["#light"; "module Foo"])
                
                //ccn((project :> IVsHierarchy), "Debug|Any CPU")
                let configName = "Debug"                
                let (hr, configurationInterface) = project.ConfigProvider.GetCfgOfName(configName, ProjectConfig.Any_CPU)
                AssertEqual VSConstants.S_OK hr
                let config = configurationInterface :?> ProjectConfig
                let (hr, vsBuildableCfg) = config.get_BuildableProjectCfg()
                let buildableCfg = vsBuildableCfg :?> BuildableProjectConfig
                AssertEqual VSConstants.S_OK hr
                
                let mutable isCleaning = false
                let success = ref false
                use event = new System.Threading.ManualResetEvent(false)
                let (hr, cookie) = 
                    buildableCfg.AdviseBuildStatusCallback(
                        { new IVsBuildStatusCallback with
                            member this.BuildBegin pfContinue = pfContinue <- 1; VSConstants.S_OK
                            member this.BuildEnd fSuccess =
                                success := fSuccess <> 0
                                printfn "Build %s, code %i, phase: %s." (if !success then "succeeded" else "failed") fSuccess (if isCleaning then "Cleaning" else "Build")

                                event.Set() |> Assert.IsTrue
                                VSConstants.S_OK
                            member this.Tick pfContinue = pfContinue <- 1; VSConstants.S_OK
                        }
                    )
                try
                    let buildMgrAccessor = project.Site.GetService(typeof<SVsBuildManagerAccessor>) :?> IVsBuildManagerAccessor
                    let output = VsMocks.vsOutputWindowPane(ref [])
                    let doBuild target =
                        success := false
                        event.Reset() |> Assert.IsTrue
                        buildMgrAccessor.BeginDesignTimeBuild() |> ValidateOK // this is not a design-time build, but our mock does all the right initialization of the build manager for us, similar to what the system would do in VS for real
                        buildableCfg.Build(0u, output, target)
                        event.WaitOne() |> Assert.IsTrue
                        buildMgrAccessor.EndDesignTimeBuild() |> ValidateOK // this is not a design-time build, but our mock does all the right initialization of the build manager for us, similar to what the system would do in VS for real
                        AssertEqual true !success

                    printfn "Building..."
                    doBuild "Build"
                    AssertEqual true (File.Exists (Path.Combine(project.ProjectFolder, "bin\\Debug\\Blah.dll")))
                    printfn "Output files present."
                    
                    isCleaning <- true
                    printfn "Cleaning..."
                    doBuild "Clean"
                    printfn "Finished build-then-clean."
                    AssertEqual false (File.Exists (Path.Combine(project.ProjectFolder, "bin\\Debug\\Blah.dll")))
                    printfn "Files were cleaned."
                finally
                    buildableCfg.UnadviseBuildStatusCallback(cookie) |> AssertEqual VSConstants.S_OK
        ))
        
        
    //KnownFail: [<Test>]
    member public this.``ErrorReporting.EmptyModuleReportedAtTheLastLine``() =
        let (outputWindowPaneErrors : string list ref) = ref [] // output window pane errors
        let vso = VsMocks.vsOutputWindowPane(outputWindowPaneErrors)
        let compileItem = ["foo.fs"]
        let expectedError = "foo.fs(4,1): warning FS0988: Main module of program is empty: nothing will happen when it is run" // expected error

        // Compile & verify error range
        DoWithTempFile "Test.fsproj" (fun projFile ->
            File.AppendAllText(projFile, TheTests.SimpleFsprojText(compileItem, [], ""))
            use project = TheTests.CreateProject(projFile)
            let srcFile = (Path.GetDirectoryName projFile) + "\\" + "foo.fs"
            File.AppendAllText(srcFile, "#light\nlet foo () =\n  printfn \"A\"\n") 
            project.BuildToOutput("Build", vso, null) |> ignore // Build the project using vso as the output logger
            let errors = List.filter (fun (s:string) -> s.Contains(expectedError)) !outputWindowPaneErrors    
            AssertEqual 1 (List.length errors)
        )        

    member public this.``DebuggingDLLFailsFunc``() =
        this.MakeProjectAndDoWithProjectFileAndConfigChangeNotifier(["foo.fs"], [], 
               this.MSBuildProjectBoilerplate "Library",  
               (fun project ccn projFileName ->
                   ccn((project :> IVsHierarchy), "Debug|Any CPU")
                   let fooPath = Path.Combine(project.ProjectFolder, "foo.fs")
                   File.AppendAllText(fooPath, "#light")                
                   let mutable configurationInterface : IVsCfg = null
                   let hr = project.ConfigProvider.GetCfgOfName("Debug", "Any CPU", &configurationInterface)
                   AssertEqual VSConstants.S_OK hr                
                   let config = configurationInterface :?> ProjectConfig
                   config.DebugLaunch(0ul) |> ignore
                   ()
               ))

#if NUNIT_V2
    [<Test>][<ExpectedException (typeof<ClassLibraryCannotBeStartedDirectlyException>)>]
    member public this.``DebuggingDLLFails``() = this.``DebuggingDLLFailsFunc``()
#else
    [<Test>]
    member public this.``DebuggingDLLFails``() =
        Assert.That((fun () -> this.``DebuggingDLLFailsFunc``()), NUnit.Framework.Throws.TypeOf(typeof<ClassLibraryCannotBeStartedDirectlyException>))
#endif

    [<Test>]
    member public this.``DebuggingEXESucceeds``() =
        this.MakeProjectAndDoWithProjectFileAndConfigChangeNotifier(["foo.fs"], [], 
            this.MSBuildProjectBoilerplate "Exe",  
            (fun project ccn projFileName ->
                ccn((project :> IVsHierarchy), "Debug|Any CPU")
                let fooPath = Path.Combine(project.ProjectFolder, "foo.fs")
                File.AppendAllText(fooPath, "#light")                
                let buildResult = project.Build("Build")
                Assert.IsTrue buildResult.IsSuccessful
                AssertEqual true (File.Exists (Path.Combine(project.ProjectFolder, "bin\\Debug\\Blah.exe")))

                let mutable configurationInterface : IVsCfg = null
                let hr = project.ConfigProvider.GetCfgOfName("Debug", "Any CPU", &configurationInterface)
                AssertEqual VSConstants.S_OK hr
                let config = configurationInterface :?> ProjectConfig
                try
                    config.DebugLaunch(0ul) |> ignore
                with
                | :? ClassLibraryCannotBeStartedDirectlyException -> Assert.Fail("Exe should be debuggable")
                | _ -> Assert.Fail() // DmiLom: Currently DebugLaunch() swallows most exceptions, in future if we improve DebugLaunch() we will expect it to throw a particular exception here
                ()
        ))
        
    [<Test>]
    member public this.``IsDocumentInProject`` () =
        DoWithTempFile "Test.fsproj" (fun file ->
            let fakeCsLibProjectFile = @"..\CsLib\CsLib.csproj"            
            File.AppendAllText(file, TheTests.FsprojTextWithProjectReferences(["foo.fs"; @"bar\baz.fs"],["System.dll";"Foo.Bar.Baz.dll"],[fakeCsLibProjectFile],""))
            let sp, cnn = VsMocks.MakeMockServiceProviderAndConfigChangeNotifier()
            let project = TheTests.CreateProject(file, "false", cnn, sp)            
            use project = project
            let checkInProject shouldBeInProject relPath  =
                let mkDoc = Path.Combine(project.ProjectFolder, relPath)
                let priority = [|VSDOCUMENTPRIORITY.DP_Unsupported|]
                let mutable found = 0
                let mutable itemId = 0ul
                let hr,_ = project.IsDocumentInProject(mkDoc, &found, priority)
                AssertEqual VSConstants.S_OK hr
                AssertEqual shouldBeInProject (found <> 0)
            checkInProject true "foo.fs"
            checkInProject true @"bar\baz.fs"
            checkInProject false "abracadabra.fs"
            checkInProject false fakeCsLibProjectFile
            checkInProject false "System.dll"
        )

    //Known Fail:  [<Test>]
    member public this.``PreBuildEvent`` () =
        this.MakeProjectAndDoWithProjectFile(["foo.fs"], ["System"], "",
            (fun project projFileName ->
                project.SetOrCreateBuildEventProperty("PreBuildEvent", "echo ProjectExt[$(ProjectExt)]")  // just test one example property
                SaveProject(project)
                // ensure that <PreBuildEvent> is after <Import> declaration
                let fsprojFileText = File.ReadAllText(projFileName)
                printfn "%s" fsprojFileText
                let regexStr = "<Import Project=.*?Microsoft.FSharp.Targets(.|\\n)*?<PreBuildEvent>"
                TheTests.HelpfulAssertMatches '<' regexStr fsprojFileText
                // ensure it runs
                let outputWindowPaneErrors : string list ref = ref [] 
                let vso = VsMocks.vsOutputWindowPane(outputWindowPaneErrors)
                let srcFile = (Path.GetDirectoryName projFileName) + "\\" + "foo.fs"
                File.AppendAllText(srcFile, "let x = 5\n") 
                project.BuildToOutput("Build", vso, null) |> ignore // Build the project using vso as the output logger
                printfn "Build output:"
                !outputWindowPaneErrors |> Seq.iter (printfn "%s")
                let expectedRegex = new Regex("\\s*ProjectExt\\[.fsproj\\]")                
                Assert.IsTrue(!outputWindowPaneErrors |> List.exists (fun s -> expectedRegex.IsMatch(s)), "did not see expected value in build output")
            ))
        
    [<Test>]
    member public this.``BuildMacroValues`` () = 
        let logger (message:string) = System.IO.File.AppendAllText(@"c:\temp\logfile.txt", (message + Environment.NewLine))

        DoWithTempFile "MyAssembly.fsproj" (fun file ->

            File.AppendAllText(file, TheTests.FsprojTextWithProjectReferences([],[],[],""))
            let sp, cnn = VsMocks.MakeMockServiceProviderAndConfigChangeNotifier()
            use project = TheTests.CreateProject(file, "false", cnn, sp) 
            let targetPath = project.GetBuildMacroValue("TargetPath")
            let expectedTargetPath = Path.Combine(Path.GetDirectoryName(file), @"bin\Debug\MyAssembly.exe")
            AssertEqual expectedTargetPath targetPath
            let targetDir = project.GetBuildMacroValue("TargetDir")
            let expectedTargetDir = Path.Combine(Path.GetDirectoryName(file), @"bin\Debug\")
            AssertEqual expectedTargetDir targetDir

            // Verify Solution values
            let solutionDir = project.GetBuildMacroValue("SolutionDir")
            Assert.IsNotNull (solutionDir, "SolutionDir is NULL")
            Assert.IsFalse ( (solutionDir = "*Undefined*"), "SolutionDir not defined")

            let solutionFileName = project.GetBuildMacroValue("SolutionFileName")
            Assert.IsNotNull (solutionFileName, "SolutionFileName is null")
            Assert.IsFalse ( (solutionFileName = "*Undefined*"), "SolutionFileName not defined")

            let solutionName = project.GetBuildMacroValue("SolutionName")
            Assert.IsNotNull (solutionName, "SolutionName is null")
            Assert.IsFalse ( (solutionName = "*Undefined*"), "SolutionName not defined")

            let solutionExt = project.GetBuildMacroValue("SolutionExt")
            Assert.IsNotNull (solutionExt, "SolutionExt is null")
            Assert.IsFalse ( (solutionExt = "*Undefined*"), "SolutionExt not defined")
        )
     
    [<Test>]
    member public this.CreateFSharpManifestResourceName () =
        DoWithTempFile "Test.fsproj" (fun file ->
            let text = TheTests.FsprojTextWithProjectReferences(["foo.fs";"Bar.resx"; "Bar.de.resx"; "Xyz\Baz.ru.resx"; "Abc.resources"],[],[],"")
            File.AppendAllText(file, text)
            let result = Salsa.Salsa.CreateFSharpManifestResourceName file "" "" |> List.sort
            let expected =
                ["Abc.resources", "Abc.resources";
                 "Bar.de.resx", "Bar.de"; 
                 "Bar.resx", "Bar"; 
                 "Xyz\Baz.ru.resx", "Xyz.Baz.ru"] |> List.sort
            if expected <> result then
                Assert.Fail ((sprintf "%A" expected) + "<>" + (sprintf "%A" result))            
            ()
        )

    member this.GetCurrentConfigCanonicalName(project : ProjectNode) =
        let buildMgr = project.Site.GetService(typeof<SVsSolutionBuildManager>) :?> IVsSolutionBuildManager
        let cfgs = Array.create 1 (null : IVsProjectCfg)
        let hr = buildMgr.FindActiveProjectCfg(System.IntPtr.Zero, System.IntPtr.Zero, project, cfgs)
        Assert.AreEqual(VSConstants.S_OK, hr)
        Assert.IsNotNull(cfgs.[0])
        let mutable cfgName = ""
        let hr = cfgs.[0].get_CanonicalName(&cfgName)
        Assert.AreEqual(VSConstants.S_OK, hr)
        cfgName

    [<Test>]
    member this.``MSBuildExtensibility.BrokenCompileDependsOn.WithRecovery`` () =
        this.MakeProjectAndDoWithProjectFileAndConfigChangeNotifier(["foo.fs";"bar.fs"], [], 
// define a legal 'Foo' configuration
@"<PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Foo|AnyCPU' "">
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>bin\Debug\</OutputPath>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>3</WarningLevel>
  </PropertyGroup>", fun project configChangeNotifier projFile -> 
            let projFileText = File.ReadAllText(projFile)
            // We need to add text _after_ the import of Microsoft.FSharp.Targets.  
            let i = projFileText.IndexOf("<Import Project=")
            let i = projFileText.IndexOf(">", i)
            let newProjFileText = projFileText.Insert(i+1, @"
                  <PropertyGroup>
                    <CompileDependsOn>MyTarget;$(CompileDependsOn)</CompileDependsOn>
                  </PropertyGroup>
                  <Target Name=""MyTarget"">
                    <Error Condition="" '$(Configuration)'!='Foo' "" Text=""This is my error message."" ContinueOnError=""false"" />
                  </Target>")
            File.WriteAllText(projFile, newProjFileText)
            project.Reload()
            // Ensure we are not in 'Foo' config, and thus expect failure
            let curCfgCanonicalName = this.GetCurrentConfigCanonicalName(project)
            Assert.IsFalse(curCfgCanonicalName.StartsWith("Foo"), sprintf "default config should not be 'Foo'! in fact it had canonical name '%s'" curCfgCanonicalName)
            // Now the project system is in a state where ComputeSourcesAndFlags will fail.
            // Our goal is to at least be able to open individual source files and treat them like 'files outside a project' with regards to intellisense, etc.
            // Also, if the user does 'Build', he will get an error which will help diagnose the problem.
            let ipps = project :> IProvideProjectSite
            let ips = ipps.GetProjectSite()
            let expected = [| |] // Ideal behavior is [|"foo.fs";"bar.fs"|], and we could choose to improve this in the future.  For now we are just happy to now throw/crash.
            let actual = ips.CompilationSourceFiles
            Assert.AreEqual(expected, actual, "project site did not report expected set of source files")
        )

    [<Test>]
    member public this.TestBuildActions () =
        DoWithTempFile "Test.fsproj" (fun file ->
            let text = TheTests.FsprojTextWithProjectReferences(["foo.fs";"Bar.resx"; "Bar.de.resx"; "Xyz\Baz.ru.resx"; "Abc.resources"],[],[],"<Import Project=\"My.targets\" />")
            
            File.AppendAllText(file, text)
            let dirName = Path.GetDirectoryName(file)
            let targetsFile = Path.Combine(dirName, "My.targets")
            let targetsText = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                    <ItemGroup>
                        <AvailableItemName Include=""MyBuildAction"" />
                        <AvailableItemName Include=""MyBuildAction3"" />
                    </ItemGroup>
                </Project>"
            File.AppendAllText(targetsFile, targetsText)
            let sp, cnn = VsMocks.MakeMockServiceProviderAndConfigChangeNotifier()
            let project = TheTests.CreateProject(file, "false", cnn, sp)            
            use project = project
            let values = project.BuildActionConverter.GetStandardValues()
            let list = values |> Seq.cast |> Seq.map (fun (ba : BuildAction)-> ba.Name) |> Seq.toList
            // expected list of build actions is union of standard actions, custom actions, and "extended" standard actions 
            // this is not exhaustive (exhaustive list is not static), but covers the main equivalence classes
            let expected = ["Compile"; "Content"; "EmbeddedResource"; "None"; "MyBuildAction"; "MyBuildAction3"; "Resource"]
            if expected |> List.forall (fun i -> List.exists ((=)i) list) |> not then                
                let s0 = sprintf "%A" expected
                let s1 = sprintf "%A" list
                Assert.Fail(s0 + "<>" + s1)
            ()
        )

    [<Test>]
    member public this.TestBuildActionConversions () =

        let replace (pattern:string) (replacement:string) (input:string) = Regex.Replace(input, pattern, replacement)

        let getBuildableNodeProps project caption = 
            let node = TheTests.FindNodeWithCaption (project, caption)
            let props = node.CreatePropertiesObject()
            props :?> BuildableNodeProperties

        let checkNotStandardBuildAction buildAction = 
            Assert.IsFalse(VSLangProj.prjBuildAction.prjBuildActionNone = buildAction, "Unexpected None match")
            Assert.IsFalse(VSLangProj.prjBuildAction.prjBuildActionCompile = buildAction, "Unexpected Compile match")
            Assert.IsFalse(VSLangProj.prjBuildAction.prjBuildActionContent = buildAction, "Unexpected Content match")
            Assert.IsFalse(VSLangProj.prjBuildAction.prjBuildActionEmbeddedResource = buildAction, "Unexpected EmbeddedResource match")

        DoWithTempFile "Test.fsproj" (fun file ->
            let text =
                TheTests.FsprojTextWithProjectReferences(["Compile.fs"; "None.fs"; "Resource.fs"; "SplashSceen.fs"; "Dude.fs"],[],[],"")
                |> replace "Compile\s+Include='([a-zA-Z]+)\.fs'" "$1 Include='$1.fs'"            
            File.AppendAllText(file, text)
            let sp, cnn = VsMocks.MakeMockServiceProviderAndConfigChangeNotifier()
            let project = TheTests.CreateProject(file, "false", cnn, sp)
            use project = project

            // test proper behavior from project file
            let node = getBuildableNodeProps project "Compile.fs"
            Assert.IsTrue(node.BuildAction = VSLangProj.prjBuildAction.prjBuildActionCompile, "Compile build action failed")
            Assert.IsTrue(node.ItemType = "Compile", "Compile item type failed")

            let node = getBuildableNodeProps project "None.fs"
            Assert.IsTrue(node.BuildAction = VSLangProj.prjBuildAction.prjBuildActionNone, "None build action failed")
            Assert.IsTrue(node.ItemType = "None", "None item type failed")

            let node = getBuildableNodeProps project "Resource.fs"
            checkNotStandardBuildAction node.BuildAction
            Assert.IsTrue(node.ItemType = "Resource", "Resource item type failed")

            let node = getBuildableNodeProps project "Dude.fs"
            checkNotStandardBuildAction node.BuildAction
            Assert.IsTrue(node.ItemType = "Dude", "Dude item type failed")

            // test handling of bogus values
            node.BuildAction <- enum 100
            Assert.IsTrue(node.BuildAction = VSLangProj.prjBuildAction.prjBuildActionNone, "Bogus build action not mapped to None")

            node.ItemType <- "Wibble"
            Assert.IsTrue(node.ItemType = "None", "Bogus item type not mapped to None")

            ()
        )

    [<Test>]
    member this.``WildcardsInProjectFile.ThrowingCase`` () =
        DoWithTempFile "Test.fsproj"(fun file ->
            let text = TheTests.FsprojTextWithProjectReferences(["*.fs"],[],[],"")
            File.AppendAllText(file, text)
            let dirName= Path.GetDirectoryName(file)
            File.AppendAllText(Path.Combine(dirName, "Foo.fs"), "do ()")
            File.AppendAllText(Path.Combine(dirName, "Bar.fs"), "do ()")
            let mutable exceptionThrown = false
            try
                let sp, cnn = VsMocks.MakeMockServiceProviderAndConfigChangeNotifier()
                let project = TheTests.CreateProject(file, "false", cnn, sp) in project.Close() |> ignore
            with :? CannotOpenProjectsWithWildcardsException as e ->
                    exceptionThrown <- true
                    AssertEqual "*.fs" e.ItemSpecification
                    AssertEqual "Compile" e.ItemType
            Assert.IsTrue(exceptionThrown)
        )
        
    [<Test>]
    member this.``WildcardsInProjectFile.OkCase`` () =
        DoWithTempFile "Test.fsproj"(fun file ->
            let text = TheTests.FsprojTextWithProjectReferences(["*.fs"],[],[],"")
            File.AppendAllText(file, text)
            let dirName= Path.GetDirectoryName(file)
            let fileName = Path.Combine(dirName, "Foo.fs")
            File.AppendAllText(fileName, "do ()")
            let sp, cnn = VsMocks.MakeMockServiceProviderAndConfigChangeNotifier()
            let project = TheTests.CreateProject(file, "false", cnn, sp) 
            try
                project.ComputeSourcesAndFlags()
                let items = project.CompilationSourceFiles |> Array.toList
                match items with
                | [ _; fn ] -> // first file is AssemblyAttributes.fs
                    AssertEqual fileName fn
                | _ ->
                    sprintf "wring set of compile items %A" items |> Assert.Fail
                ()
            finally
                project.Close() |> ignore
        )

[<TestFixture>]
type Utilities() = 
    (*
        Simulation of the code found in Xaml editor that we were crashing. The relevent code is pasted below.
        Note that they're assuming PKT is eight bytes. This need not be true and we don't enforce it from our
        side. We're just going to make sure to send an even number of characters (two per-byte).
        
        private static AssemblyName EnsureAssemblyName(Reference r) {
            if (r.Type != prjReferenceType.prjReferenceTypeAssembly)
                return null;

            // If possible, we use the data stored in the reference
            // to get to the assembly name.  It is much faster than
            // cracking the manifest.

            AssemblyName an = new AssemblyName();
            // Reference.Name does not have to be the actual assembly name here (seen in Project ref cases)
            // Identity (for Reference.Type == prjReferenceType.prjReferenceTypeAssembly) is the assembly name 
            // without path or extension and is reserved so it can't be set in the project file by users
            an.Name = r.Identity; 
            an.CultureInfo = new CultureInfo(r.Culture);
            an.Version = new Version(
                r.MajorVersion,
                r.MinorVersion,
                r.BuildNumber,
                r.RevisionNumber);

            string publicTokenString = r.PublicKeyToken;
            if (publicTokenString != null && publicTokenString.Length > 0) {
                byte[] publicToken = new byte[8];
                for (int i = 0; i < publicToken.Length; i++) {
                    string byteString = publicTokenString.Substring(i * 2, 2);
                    publicToken[i] = byte.Parse(byteString, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                }
                an.SetPublicKeyToken(publicToken);
            }
            else {
                an.SetPublicKeyToken(new byte[0]);
            }

            try {
                Uri uri = new Uri(r.Path);
                an.CodeBase = uri.AbsoluteUri;
            }
            catch (UriFormatException) {
                // Reference path is ill-formed or empty => no code base
            }

            return an;
        }
        
        
        
    *)
    
    let SimulateXamlEditorReceivingThroughDTE(publicToken:string) = 
        printfn "Simulating xaml pkt parsing for %s" publicToken
        // ----------------------------------------------------------------------------------------------------
        // Don't change code between these lines. Its simulating external code.
        // ----------------------------------------------------------------------------------------------------
        let ParseOneByte(i:int) = 
            let byteString = publicToken.Substring(i * 2, 2)
            System.Byte.Parse(byteString, NumberStyles.HexNumber, CultureInfo.InvariantCulture) |> ignore
        [0..7] |> List.iter ParseOneByte
        // ----------------------------------------------------------------------------------------------------

    let CheckPublicKeyToString(bytes,expect) =
        let actual = KeyToken.ToHexString(bytes)
        Assert.AreEqual(expect, actual)
        SimulateXamlEditorReceivingThroughDTE(actual)

    [<Test>]
    member public this.``PublicKeyToken.0000000000000000``() = CheckPublicKeyToString([|0uy;0uy;0uy;0uy;0uy;0uy;0uy;0uy|], "0000000000000000")
        
    [<Test>]
    member public this.``PublicKeyToken.0000000000000001``() = CheckPublicKeyToString([|0uy;0uy;0uy;0uy;0uy;0uy;0uy;1uy|], "0000000000000001")

    [<Test>]
    member public this.``PublicKeyToken.0a00000000000001``() = CheckPublicKeyToString([|0xauy;0uy;0uy;0uy;0uy;0uy;0uy;1uy|], "0a00000000000001")

    [<Test>]
    member public this.``Parse MSBuild property of type Int64`` () = 
        Assert.AreEqual(123L, ProjectNode.ParsePropertyValueToInt64("123"))
        Assert.AreEqual(255L, ProjectNode.ParsePropertyValueToInt64("0xFF"))
        Assert.AreEqual(null, ProjectNode.ParsePropertyValueToInt64(""))
        Assert.AreEqual(null, ProjectNode.ParsePropertyValueToInt64(null))
        Throws<Exception>(fun () -> ignore (ProjectNode.ParsePropertyValueToInt64("abc")))
        Throws<Exception>(fun () -> ignore (ProjectNode.ParsePropertyValueToInt64("12333333333333333333333333")))

#if DEBUGGERVISUALIZER
module internal DebugViz =
    open System.Windows.Forms
    open System.Drawing
    open Microsoft.VisualStudio.DebuggerVisualizers
    open System.Runtime.Serialization.Formatters.Binary
    open System.Runtime.Serialization
    open System.Collections.ObjectModel

    // debugger visualizer for HierarchyNodes
    [<Serializable>]
    type MyTreeNode(_data : string, _children : Collection<MyTreeNode>) = 
        let mutable data = _data
        let mutable children = _children
        new() = MyTreeNode("", new Collection<MyTreeNode>())
        new(data) = MyTreeNode(data, new Collection<MyTreeNode>())
        new(data, child : MyTreeNode, ()) = let nodes = new Collection<MyTreeNode>()
                                            nodes.Add(child)
                                            MyTreeNode(data, nodes)
        
        member this.Data with get() = data
                         and set(s) = data <- s
        member this.Nodes with get() = children
                          and set(c) = children <- c
        member this.ToTreeNode() =
            let (result : TreeNode) = this.ToTreeNode(false) 
            result.Toggle()
            result
        member this.ToTreeNode(toggle) =
            let result = new TreeNode(data)
            for mtn in children do
                result.Nodes.Add(mtn.ToTreeNode(true)) |> ignore
            if toggle then
                result.Toggle()
            result

    type MyVisualizerObjectSource() =
        inherit VisualizerObjectSource()
        override this.CreateReplacementObject(target, incomingData) =
            null : Object
        override this.GetData(target, outgoingData) =
            let node = MyVisualizerObjectSource.MakeFromObject(target)
            let formatter = new BinaryFormatter()
            formatter.Serialize(outgoingData, node)
            ()
        override this.TransferData(target, incomingData, outgoingData) =
            this.GetData(target, outgoingData)
        static member MakeFromObject(o : Object) =
            let hn = o :?> HierarchyNode
            if hn <> null then
                let rec Make(n : HierarchyNode) =
                    let result = new MyTreeNode(n.Caption)
                    let mutable c = n.FirstChild
                    while c <> null do
                        result.Nodes.Add(Make(c))
                        c <- c.NextSibling
                    result
                Make(hn)
            else failwith "expected a HierarchyNode"
        
    type MyForm(myNode : MyTreeNode) as this =
        inherit Form()

        static let normalFont = new Font("Courier New", 12.0f, FontStyle.Regular);
        static let boldFont = new Font("Courier New", 12.0f, FontStyle.Bold);

        let node = myNode.ToTreeNode()
        // REVIEW work on auto-sizing the window
        let maxX = 820;
        let maxY = 620;
        let tv = new TreeView()
        do
            tv.Font <- boldFont
            tv.Nodes.Add(node) |> ignore
            tv.Location <- new System.Drawing.Point(0, 0)
            tv.Size <- new System.Drawing.Size(maxX, maxY)
            tv.DrawMode <- TreeViewDrawMode.OwnerDrawText
            tv.DrawNode.AddHandler(new DrawTreeNodeEventHandler(this.DrawNode))
            this.Controls.Add(tv)
            this.Size <- new System.Drawing.Size(maxX + 15, maxY + 30)
            this.Location <- new System.Drawing.Point(10, 10)

        member this.DrawNode sender (e : DrawTreeNodeEventArgs) =
            let text = e.Node.Text;
            let g = e.Graphics
            let size1 = g.MeasureString(text, boldFont)
            let rec1 = RectangleF.FromLTRB(single e.Bounds.Left, single e.Bounds.Top, single e.Bounds.Left + size1.Width+1.0f, single e.Bounds.Top + size1.Height+1.0f)
            e.Graphics.DrawString(text, boldFont, Brushes.Black, rec1)
            ()

    type MyExpandedDialogDebuggerVisualizer() =
        inherit DialogDebuggerVisualizer()
        override this.Show(windowService, objectProvider) =
            let formatter = new BinaryFormatter()
            let input = objectProvider.GetData()
            let node = formatter.Deserialize(input, null) :?> MyTreeNode
            let form = new MyForm(node)
            windowService.ShowDialog(form :> Form) |> ignore
        static member TestShowVisualizer(objectToVisualize : HierarchyNode) =
            let serializable = MyVisualizerObjectSource.MakeFromObject(box objectToVisualize)
            let visualizerHost = new VisualizerDevelopmentHost(serializable, typeof<MyExpandedDialogDebuggerVisualizer>)
            visualizerHost.ShowVisualizer()

    [<assembly: DebuggerVisualizer(typeof<MyExpandedDialogDebuggerVisualizer>, 
                                   typeof<MyVisualizerObjectSource>,
                                   Target = typeof<HierarchyNode>,
                                   Description = "HierarchyNode visualizer")>]
    [<assembly: DebuggerVisualizer(typeof<MyExpandedDialogDebuggerVisualizer>, 
                                   typeof<MyVisualizerObjectSource>,
                                   Target = typeof<FSharpProjectNode>,
                                   Description = "HierarchyNode visualizer")>]
    [<assembly: DebuggerVisualizer(typeof<MyExpandedDialogDebuggerVisualizer>, 
                                   typeof<MyVisualizerObjectSource>,
                                   Target = typeof<FSharpFileNode>,
                                   Description = "HierarchyNode visualizer")>]
    [<assembly: DebuggerVisualizer(typeof<MyExpandedDialogDebuggerVisualizer>, 
                                   typeof<MyVisualizerObjectSource>,
                                   Target = typeof<UnitTestingFSharpProjectNode>,
                                   Description = "HierarchyNode visualizer")>]
    do
        () // module-level do for assembly-level attribute                     
#endif