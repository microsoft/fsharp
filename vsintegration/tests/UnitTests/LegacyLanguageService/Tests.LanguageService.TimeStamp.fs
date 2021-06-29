// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Tests.LanguageService.TimeStamp

open System
open System.IO
open NUnit.Framework
open Salsa.Salsa
open Salsa.VsOpsUtils
open UnitTests.TestLib.Salsa
open UnitTests.TestLib.Utils
open UnitTests.TestLib.LanguageService

[<TestFixture>]
[<Category "LanguageService">] 
type UsingMSBuild()  = 
    inherit LanguageServiceBaseTests()

    (* Timings ----------------------------------------------------------------------------- *)
    let stopWatch = new System.Diagnostics.Stopwatch()
    let ResetStopWatch() = stopWatch.Reset(); stopWatch.Start()
    let time1 op a message = 
        ResetStopWatch()
        let result = op a
        printf "%s %d ms\n" message stopWatch.ElapsedMilliseconds
        result

    let ShowErrors(project:OpenProject) =     
        for error in (GetErrors(project)) do
            printf "%s\n" (error.ToString()) 

    let AssertNoErrorsOrWarnings(project:OpenProject) = 
        let count = List.length (GetErrors(project))
        if count<>0 then
            printf "Saw %d errors and expected none.\n" count
            AssertEqual(0,count)

    let AssertNoErrorSeenContaining(project:OpenProject,text) =
        let matching = (GetErrors(project)) |> List.filter (fun e->e.ToString().Contains(text))
        match matching with 
        | [] -> ()
        | _ -> 
            ShowErrors(project)
            failwith (sprintf "Error seen containing \"%s\"" text)  

    // In this bug, if you clean the dependent project, the dependee started getting errors again about the unresolved assembly.
    // The desired behavior is like C#, which is if the assembly disappears from disk, we use cached results of last time it was there.
    [<Test>]
    [<Ignore("https://github.com/dotnet/fsharp/issues/11724")>]
    member public this.``Regression.NoError.Timestamps.Bug3368b``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        // Handy for this test: 
        //Trace.Log <- "ChangeEvents;IncrementalBuildCommandLineMessages;SyncOp;CompilerServices"
              
        // Create the projects/
        let project1 = CreateProject(solution,"testproject1")
        let project2 = CreateProject(solution,"testproject2")                
        SetConfigurationAndPlatform(project1, "Debug|AnyCPU")  // maybe due to msbuild bug on dev10, we must set config/platform when building with ProjectReferences
        SetConfigurationAndPlatform(project2, "Debug|AnyCPU")  // maybe due to msbuild bug on dev10, we must set config/platform when building with ProjectReferences
        let file1 = AddFileFromText(project1,"File1.fs", ["#light"
                                                          "let xx = 42"
                                                          "printfn \"hi\""])
        let file2 = AddFileFromText(project2,"File2.fs", ["#light"
                                                          "let yy = File1.xx"
                                                          "printfn \"hi\""])      
        // Add a project-to-project reference.
        // WARNING: See bug 4434 - when unit testing this actually goes and builds project1!!!!
        AddProjectReference(project2,project1)
        TakeCoffeeBreak(this.VS) // Dependencies between projects get registered for file-watching during OnIdle processing
        AssertNoErrorsOrWarnings(project1)
        AssertNoErrorsOrWarnings(project2)

        // Now build project1
        printfn "building dependent project..."
        Build project1 |> ignore
        TakeCoffeeBreak(this.VS) 
        
        // Open files in editor.
        let file1 = OpenFile(project1,"File1.fs")
        let file2 = OpenFile(project2,"File2.fs")

        // Now clean project1
        printfn "cleaning dependent project..."
        BuildTarget(project1, "Clean") |> ignore
        TakeCoffeeBreak(this.VS) 
        AssertNoErrorsOrWarnings(project1)
        AssertNoErrorsOrWarnings(project2)  // this is key, project2 remembers what used to be on disk, does not fail due to missing assembly

    // In this bug, the referenced project output didn't exist yet. Building dependee should cause update in dependant
    [<Test>]
    [<Ignore("Re-enable this test --- https://github.com/Microsoft/visualfsharp/issues/5238")>]
    member public this.``Regression.NoContainedString.Timestamps.Bug3368a``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        // Handy for this test: Trace.Log <- "ChangeEvents;IncrementalBuildCommandLineMessages"
        // Create the projects/
        let project1 = CreateProject(solution,"testproject1")
        let project2 = CreateProject(solution,"testproject2")    
        SetConfigurationAndPlatform(project1, "Debug|AnyCPU")  // maybe due to msbuild bug on dev10, we must set config/platform when building with ProjectReferences
        SetConfigurationAndPlatform(project2, "Debug|AnyCPU")  // maybe due to msbuild bug on dev10, we must set config/platform when building with ProjectReferences
        let file1 = AddFileFromText(project1,"File1.fs", ["#light"])
        let file2 = AddFileFromText(project2,"File2.fs", ["#light"])
        
        // Add a project-to-project reference. 
        // WARNING: See bug 4434 - when unit testing this actually goes and builds project1!!!!
        AddProjectReference(project2,project1)
        
        // Open files in editor.
        let file1 = OpenFile(project1,"File1.fs")
        let file2 = OpenFile(project2,"File2.fs")
        
        // Wait for things to settle down and make sure there is an error
        TakeCoffeeBreak(this.VS) 
        // This assert no longer holds because project1 has inadvertently been built - see bug 4434 and comment above
        //AssertExactlyOneErrorSeenContaining(project2, "project1")
               
        // Now build project1
        Build project1 |> ignore
        TakeCoffeeBreak(this.VS) 
        AssertNoErrorSeenContaining(project2, "project1")

   // FEATURE: OnIdle() will reprocess open dirty files, even if those file do not currently have editor focus
    // [<Test>] TODO This test does not work because the unit tests do not cover product code that implements this feature
    member public this.``Timestamps.Bug3368c``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        // Handy for this test: 
        //Trace.Log <- "ChangeEvents;IncrementalBuildCommandLineMessages;SyncOp;CompilerServices"
        
        // Create the projects/
        let project1 = CreateProject(solution,"testproject1")
        let project2 = CreateProject(solution,"testproject2")                
        let file1 = AddFileFromText(project1,"File1.fs", ["#light"
                                                          "let xx = 42"
                                                          "printfn \"hi\""])
        let file2 = AddFileFromText(project2,"File2.fs", ["#light"
                                                          "let yy = File1.xx"
                                                          "printfn \"hi\""])
        
        // Add an assembly reference between the projects (a P2P would fall victim to 4434)
        let p1exe = BuildTarget(project1, "Clean")  // just a handy way to get the filename of the exe that would be built
        this.AddAssemblyReference(project2, p1exe.ExecutableOutput)

        // open a file to see the errors
        let file2 = OpenFile(project2,"File2.fs")
        TakeCoffeeBreak(this.VS)
        
        let errs = GetErrors(project2)
        Assert.IsTrue(List.length errs > 0, "There should be errors (unresolved reference)")

        // switch focus to a different file (to turn off 'focus' idle processing for file2)
        let file1 = OpenFile(project1,"File1.fs")

        // Now build project1
        printfn "building dependent project..."
        Build project1 |> ignore
        let errs = GetErrors(project2)
        Assert.IsTrue(List.length errs > 0, "There should be errors (unresolved reference)")
        
        TakeCoffeeBreak(this.VS) // the code that should clear out the errors is in LanguageService.cs:LanguageService.OnIdle(), 
                          // but unit tests do not call this FSharp.LanguageService.Base code; TakeCoffeeBreak(this.VS) just simulates
                          // idle processing for the currently-focused file
        printfn "after idling, file2 errors should be cleared even though file2 is not focused"
        AssertNoErrorsOrWarnings(project2)

   // FEATURE: When a referenced assembly's timestamp changes the reference is reread.
    [<Test; Category("Expensive")>]
    [<Ignore("Re-enable this test --- https://github.com/Microsoft/visualfsharp/issues/5238")>]
    member public this.``Timestamps.ReferenceAssemblyChangeAbsolute``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project1 = CreateProject(solution,"testproject1")
        
        let file1 = AddFileFromText(project1,"File1.fs",
                                    ["#light"]
                                     )
        let file1 = OpenFile(project1,"File1.fs")
        let project2 = CreateProject(solution,"testproject2")
        let file2 = AddFileFromText(project2,"File2.fs",
                                    ["#light"
                                     "File1.File1."
                                     "()"])
        let file2 = OpenFile(project2,"File2.fs")
        
        // Build project1 which will later have the type being referenced by project2
        let project1Dll = time1 Build project1 "Time to build project1"
        printfn "Output of building project1 was %s" project1Dll.ExecutableOutput
        printfn "Project2 directory is %s" (ProjectDirectory project2)

        // Add a new reference project2->project1. There should be no completions because Mary doesn't exist yet.
        this.AddAssemblyReference(project2,project1Dll.ExecutableOutput)
        TakeCoffeeBreak(this.VS) // Dependencies between projects get registered for file-watching during OnIdle processing
        SwitchToFile this.VS file2
        MoveCursorToEndOfMarker(file2,"File1.File1.")
        let completions = AutoCompleteAtCursor(file2)
        Assert.AreEqual(0, completions.Length)
        
        // Now modify project1's file and rebuild.
        ReplaceFileInMemory file1 
                                ["#light"
                                 "module File1 = "
                                 "    let Mary x = \"\""]
        SaveFileToDisk file1
        time1 Build project1 "Time to build project1 second time" |> ignore
        TakeCoffeeBreak(this.VS) // Give enough time to catch up
        SwitchToFile this.VS file2
        MoveCursorToEndOfMarker(file2,"File1.File1.")
        TakeCoffeeBreak(this.VS) // Give enough time to catch up
        let completions = AutoCompleteAtCursor(file2)
        Assert.AreNotEqual(0, completions.Length)
        printfn "Completions=%A\n" completions

    // In this bug, relative paths to referenced assemblies weren't seen.
    [<Test; Category("Expensive")>]
    [<Ignore("Re-enable this test --- https://github.com/Microsoft/visualfsharp/issues/5238")>]
    member public this.``Timestamps.ReferenceAssemblyChangeRelative``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project1 = CreateProject(solution,"testproject1")

        let MakeRelativePath(path1:string, path2) =
            // Pretend to return a path to path1 relative to path2.
            let temp = (System.IO.Path.GetTempPath())
            let tempLen = temp.Length
            ".."+(path1.Substring(tempLen-1))

        let file1 = AddFileFromText(project1,"File1.fs",
                                    ["#light"]
                                     )
        let file1 = OpenFile(project1,"File1.fs")
        let project2 = CreateProject(solution,"testproject2")
        let file2 = AddFileFromText(project2,"File2.fs",
                                    ["#light"
                                     "File1.File1."
                                     "()"])
        let file2 = OpenFile(project2,"File2.fs")

        // Build project1 which will later have the type being referenced by project2
        let project1Dll = time1 Build project1 "Time to build project1"
        printfn "Output of building project1 was %s" project1Dll.ExecutableOutput
        printfn "Project2 directory is %s" (ProjectDirectory project2)
        let project1DllRelative = MakeRelativePath(project1Dll.ExecutableOutput, (ProjectDirectory project2))
        printfn "Relative output of building project1 was %s" project1DllRelative

        // Add a new reference project2->project1. There should be no completions because Mary doesn't exist yet.
        this.AddAssemblyReference(project2,project1DllRelative)
        TakeCoffeeBreak(this.VS) // Dependencies between projects get registered for file-watching during OnIdle processing
        SwitchToFile this.VS file2
        MoveCursorToEndOfMarker(file2,"File1.File1.")
        let completions = AutoCompleteAtCursor(file2)
        Assert.AreEqual(0, completions.Length)

        // Now modify project1's file and rebuild.
        ReplaceFileInMemory file1 
                                ["#light"
                                 "module File1 = "
                                 "    let Mary x = \"\""]
        SaveFileToDisk file1      
        time1 Build project1 "Time to build project1 second time" |> ignore
        TakeCoffeeBreak(this.VS) // Give enough time to catch up
        SwitchToFile this.VS file2
        MoveCursorToEndOfMarker(file2,"File1.File1.")
        TakeCoffeeBreak(this.VS) // Give enough time to catch up
        let completions = AutoCompleteAtCursor(file2)
        Assert.AreNotEqual(0, completions.Length)
        printfn "Completions=%A\n" completions

    // FEATURE: When a referenced project's assembly timestamp changes the reference is reread.
    [<Test; Category("Expensive")>]
    [<Ignore("Re-enable this test --- https://github.com/Microsoft/visualfsharp/issues/5238")>]
    member public this.``Timestamps.ProjectReferenceAssemblyChange``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project1 = CreateProject(solution,"testproject1")
        
        let file1 = AddFileFromText(project1,"File1.fs",
                                    ["#light"]
                                     )
        let file1 = OpenFile(project1,"File1.fs")
        let project2 = CreateProject(solution,"testproject2")
        let file2 = AddFileFromText(project2,"File2.fs",
                                    ["#light"
                                     "File1.File1."
                                     "()"])
        let file2 = OpenFile(project2,"File2.fs")
        SetConfigurationAndPlatform(project1, "Debug|AnyCPU")  // maybe due to msbuild bug on dev10, we must set config/platform when building with ProjectReferences
        SetConfigurationAndPlatform(project2, "Debug|AnyCPU")  // maybe due to msbuild bug on dev10, we must set config/platform when building with ProjectReferences
        
        // Build project1 which will later have the type being referenced by project2
        let project1Dll = time1 Build project1 "Time to build project1"
        printf "Output of building project1 was %s\n" project1Dll.ExecutableOutput
        
        // Add a new reference project2->project1. There should be no completions because Mary doesn't exist yet.
        //
        // WARNING: See bug 4434 - when unit testing this actually goes and builds project1!!!!
        AddProjectReference(project2,project1)

        TakeCoffeeBreak(this.VS) // Dependencies between projects get registered for file-watching during OnIdle processing
        SetConfigurationAndPlatform(project1, "Debug|AnyCPU")  // maybe due to msbuild bug on dev10, we must set config/platform when building with ProjectReferences
        SetConfigurationAndPlatform(project2, "Debug|AnyCPU")  // maybe due to msbuild bug on dev10, we must set config/platform when building with ProjectReferences
        SwitchToFile this.VS file2
        MoveCursorToEndOfMarker(file2,"File1.File1.")
        let completions = AutoCompleteAtCursor(file2)
        Assert.AreEqual(0, completions.Length)
        
        // Now modify project1's file and rebuild.
        ReplaceFileInMemory file1 
                                ["#light"
                                 "module File1 = "
                                 "    let Mary x = \"\""]
        SaveFileToDisk file1   
        time1 Build project1 "Time to build project1 second time" |> ignore                       
        TakeCoffeeBreak(this.VS) // Give enough time to catch up             
        SwitchToFile this.VS file2
        MoveCursorToEndOfMarker(file2,"File1.File1.")
        TakeCoffeeBreak(this.VS) // Give enough time to catch up             
        let completions = AutoCompleteAtCursor(file2)
        Assert.AreNotEqual(0, completions.Length)
        printfn "Completions=%A\n" completions            


//Allow the TimeStampTests run under different context
namespace Tests.LanguageService.TimeStamp
open Tests.LanguageService
open UnitTests.TestLib.LanguageService
open UnitTests.TestLib.ProjectSystem
open NUnit.Framework
open Salsa.Salsa

// Context project system
[<TestFixture>] 
type UsingProjectSystem() = 
    inherit UsingMSBuild(VsOpts = LanguageServiceExtension.ProjectSystemTestFlavour)