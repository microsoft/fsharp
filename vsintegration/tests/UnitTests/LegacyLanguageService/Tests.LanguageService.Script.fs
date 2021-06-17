// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Tests.LanguageService.Script

open System
open System.IO
open System.Reflection
open NUnit.Framework
open Salsa.Salsa
open Salsa.VsOpsUtils
open UnitTests.TestLib.Salsa
open UnitTests.TestLib.Utils
open UnitTests.TestLib.LanguageService
open UnitTests.TestLib.ProjectSystem

[<TestFixture>]
[<Category "LanguageService">]
type UsingMSBuild() as this = 
    inherit LanguageServiceBaseTests() 

    let notAA l = None,l

    let createSingleFileFsx (code : string) = 
        let (_, p, f) = this.CreateSingleFileProject(code, fileKind = SourceFileKind.FSX)
        (p, f)

    let createSingleFileFsxFromLines (code : list<string>) = 
        let (_, p, f) = this.CreateSingleFileProject(code, fileKind = SourceFileKind.FSX)
        (p, f)

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

    let AssertListContainsInOrder(s:string list,cs:string list) =
        let s : string array = Array.ofList s
        let s : string = String.Join("\n",s)
        AssertContainsInOrder(s,cs)

    /// Assert that there is no squiggle.
    let AssertNoSquiggle(squiggleOption) = 
        match squiggleOption with 
        | None -> ()
        | Some(severity,message) ->
            Assert.Fail(sprintf "Expected no squiggle but got '%A' with message: %s" severity message)

    let VerifyErrorListContainedExpetedStr(expectedStr:string,project : OpenProject) = 
        let errorList = GetErrors(project)
        let GetErrorMessages(errorList : Error list) =
            [ for i = 0 to errorList.Length - 1 do
                yield errorList.[i].Message]
            
        Assert.IsTrue(errorList
                          |> GetErrorMessages
                          |> Seq.exists (fun errorMessage ->
                                errorMessage.Contains(expectedStr)))

    let AssertNoErrorsOrWarnings(project:OpenProject) = 
        let count = List.length (GetErrors(project))
        if count<>0 then
            printf "Saw %d errors and expected none.\n" count
            printf "Errors are: \n" 
            for e in GetErrors project do 
                printf "  path = <<<%s>>>\n" e.Path
                printf "  message = <<<%s> \n" e.Message 
            AssertEqual(0,count)

    let AssertExactlyCountErrorSeenContaining(project:OpenProject,text,expectedCount) =
        let nMatching = (GetErrors(project)) |> List.filter (fun e ->e.ToString().Contains(text)) |> List.length
        match nMatching with
        | 0 -> 
            failwith (sprintf "No errors containing \"%s\"" text)
        | x when x = expectedCount -> ()
        | _ -> 
            failwith (sprintf "Multiple errors containing \"%s\"" text)

    let AssertExactlyOneErrorSeenContaining(project:OpenProject,text) =
        AssertExactlyCountErrorSeenContaining(project,text,1)

    /// Assert that a given squiggle is an Error (or warning) containing the given text        
    let AssertSquiggleIsErrorContaining,AssertSquiggleIsWarningContaining, AssertSquiggleIsErrorNotContaining,AssertSquiggleIsWarningNotContaining =         
        let AssertSquiggle expectedSeverity nameOfExpected nameOfNotExpected assertf (squiggleOption,containing) = 
            match squiggleOption with
            | None -> Assert.Fail("Expected a squiggle but none was seen.")
            | Some(severity,message) ->
                Assert.IsTrue((severity=expectedSeverity), sprintf "Expected %s but saw %s: %s" nameOfExpected nameOfNotExpected message)
                assertf(message,containing)        
        AssertSquiggle Microsoft.VisualStudio.FSharp.LanguageService.Severity.Error    "Error"    "Warning" AssertContains,
        AssertSquiggle Microsoft.VisualStudio.FSharp.LanguageService.Severity.Warning  "Warning"  "Error"   AssertContains,
        AssertSquiggle Microsoft.VisualStudio.FSharp.LanguageService.Severity.Error    "Error"    "Warning" AssertNotContains,
        AssertSquiggle Microsoft.VisualStudio.FSharp.LanguageService.Severity.Warning  "Warning"  "Error"   AssertNotContains 


    //Verify the error list in fsx file containd the expected string
    member private this.VerifyFSXErrorListContainedExpectedString(fileContents : string, expectedStr : string) =
        let (_, project, file) = this.CreateSingleFileProject(fileContents, fileKind = SourceFileKind.FSX)
        VerifyErrorListContainedExpetedStr(expectedStr,project)    

    //Verify no error list in fsx file 
    member private this.VerifyFSXNoErrorList(fileContents : string) =
        let (_, project, file) = this.CreateSingleFileProject(fileContents, fileKind = SourceFileKind.FSX)
        AssertNoErrorsOrWarnings(project)  
    //Verify QuickInfo Containd In Fsx file
    member public this.AssertQuickInfoContainsAtEndOfMarkerInFsxFile (code : string) marker expected =

        let (_, _, file) = this.CreateSingleFileProject(code, fileKind = SourceFileKind.FSX)

        MoveCursorToEndOfMarker(file, marker)
        let tooltip = GetQuickInfoAtCursor file
        AssertContains(tooltip, expected)
    //Verify QuickInfo Containd In Fsx file
    member public this.AssertQuickInfoContainsAtStartOfMarkerInFsxFile (code : string) marker expected =
        let (_, _, file) = this.CreateSingleFileProject((code : string), fileKind = SourceFileKind.FSX)

        MoveCursorToStartOfMarker(file, marker)
        let tooltip = GetQuickInfoAtCursor file
        AssertContains(tooltip, expected)
    //Verify QuickInfo Not Containd In Fsx file     
    member public this.AssertQuickInfoNotContainsAtEndOfMarkerInFsxFile code marker notexpected =
        let (_, _, file) = this.CreateSingleFileProject((code : string), fileKind = SourceFileKind.FSX)

        MoveCursorToEndOfMarker(file, marker)
        let tooltip = GetQuickInfoAtCursor file
        AssertNotContains(tooltip, notexpected)

    /// There was a problem with Salsa that caused squiggles not to be shown for .fsx files.
    [<Test>]
    member public this.``Fsx.Squiggles.ShowInFsxFiles``() =  
        let fileContent = """open Thing1.Thing2"""
        this.VerifyFSXErrorListContainedExpectedString(fileContent,"Thing1")
        
    /// Regression test for FSharp1.0:4861 - #r to non-existent file causes the first line to be squiggled
    /// There was a problem with Salsa that caused squiggles not to be shown for .fsx files.
    [<Test>]
    member public this.``Fsx.Hash.RProperSquiggleForNonExistentFile``() =  
        let fileContent = """#r "NonExistent" """
        this.VerifyFSXErrorListContainedExpectedString(fileContent,"was not found or is invalid") 

    /// Nonexistent hash. There was a problem with Salsa that caused squiggles not to be shown for .fsx files.
    [<Test>]
    member public this.``Fsx.Hash.RDoesNotExist.Bug3325``() =  
        let fileContent = """#r "ThisDLLDoesNotExist" """
        this.VerifyFSXErrorListContainedExpectedString(fileContent,"'ThisDLLDoesNotExist' was not found or is invalid") 

    // There was a spurious error message on the first line.
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.ExactlyOneError.Bug4861``() =  
        let code = 
                                      ["#light" // First line is important in this repro
                                       "#r \"Nonexistent\""
                                       ]
        let (project, _) = createSingleFileFsxFromLines code
        AssertExactlyCountErrorSeenContaining(project, "Nonexistent", 2)   // ...and not an error on the first line.
        
    [<Test>]
    member public this.``Fsx.InvalidHashLoad.ShouldBeASquiggle.Bug3012``() =  
        let fileContent = """
            #light
            #load "Bar.fs"
            """
        this.VerifyFSXErrorListContainedExpectedString(fileContent,"Bar.fs") 

    // Transitive to existing property.
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.ScriptClosure.TransitiveLoad1``() = 
        use _guard = this.UsingNewVS() 
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")    
        let file1 = AddFileFromText(project,"File1.fs",
                                    ["namespace Namespace"
                                     "type Foo = "
                                     "     static member public Property = 0"
                                     ])    
        let file1 = OpenFile(project,"File1.fs")   
        let script2 = AddFileFromText(project,"Script2.fsx",
                                      ["#load \"File1.fs\""
                                       ])    
        let script2 = OpenFile(project,"Script2.fsx")   
        let script2 = AddFileFromText(project,"Script1.fsx",
                                      ["#load \"Script1.fsx\""
                                       "Namespace.Foo.Property" 
                                       ])    
        let script2 = OpenFile(project,"Script2.fsx")   
        TakeCoffeeBreak(this.VS)
        AssertNoErrorsOrWarnings(project)

    // Transitive to nonexisting property.
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.ScriptClosure.TransitiveLoad2``() = 
        use _guard = this.UsingNewVS()  
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")    
        let file1 = AddFileFromText(project,"File1.fs",
                                    ["namespace Namespace"
                                     "type Foo = "
                                     "     static member public Property = 0"
                                     ])    
        let script2 = AddFileFromText(project,"Script2.fsx",
                                      ["#load \"File1.fs\""
                                       ])    
        let script1 = AddFileFromText(project,"Script1.fsx",
                                      ["#load \"Script2.fsx\""
                                       "Namespace.Foo.NonExistingProperty" 
                                       ])    
        let script1 = OpenFile(project,"Script1.fsx")   
        TakeCoffeeBreak(this.VS)
        AssertExactlyOneErrorSeenContaining(project, "NonExistingProperty")

    /// FEATURE: Typing a #r into a file will cause it to be recognized by intellisense.
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.HashR.AddedIn``() =  
        let code =
                                    ["#light"
                                     "//#r \"System.Transactions.dll\"" // Pick anything that isn't in the standard set of assemblies.
                                     "open System.Transactions"
                                     ]
        let (project, file) = createSingleFileFsxFromLines code
        VerifyErrorListContainedExpetedStr("Transactions",project)
        
        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)
        ReplaceFileInMemory file
                        ["#light"
                         "#r \"System.Transactions.dll\"" // <-- Uncomment this line
                         "open System.Transactions"
                         ]
        AssertNoErrorsOrWarnings(project)
        gpatcc.AssertExactly(notAA[file],notAA[file], true (* expectCreate, because dependent DLL set changed *))

    // FEATURE: Adding a #load to a file will cause types from that file to be visible in intellisense
    [<Test>]
    member public this.``Fsx.HashLoad.Added``() =  
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let fs = AddFileFromText(project,"File1.fs",
                                    ["#light"
                                     "namespace MyNamespace" 
                                     "    module MyModule ="
                                     "        let x = 1" 
                                     ])            
        
        let fsx = AddFileFromText(project,"File2.fsx",
                                    ["#light"
                                     "//#load \"File1.fs\"" 
                                     "open MyNamespace.MyModule"
                                     "printfn \"%d\" x"
                                     ])    
        let fsx = OpenFile(project,"File2.fsx")    
        VerifyErrorListContainedExpetedStr("MyNamespace",project)
        
        ReplaceFileInMemory fsx
                         ["#light"
                          "#load \"File1.fs\"" 
                          "open MyNamespace.MyModule"
                          "printfn \"%d\" x"
                          ]
        TakeCoffeeBreak(this.VS)
        AssertNoErrorsOrWarnings(project)

    // FEATURE: Removing a #load to a file will cause types from that file to no longer be visible in intellisense
    [<Test>]
    member public this.``Fsx.HashLoad.Removed``() =  
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let fs = AddFileFromText(project,"File1.fs",
                                    ["#light"
                                     "namespace MyNamespace" 
                                     "    module MyModule ="
                                     "        let x = 1" 
                                     ])            
        
        let fsx = AddFileFromText(project,"File2.fsx",
                                    ["#light"
                                     "#load \"File1.fs\"" 
                                     "open MyNamespace.MyModule"
                                     "printfn \"%d\" x"
                                     ])    
        let fsx = OpenFile(project,"File2.fsx")    
        AssertNoErrorsOrWarnings(project)
        
        ReplaceFileInMemory fsx
                         ["#light"
                          "//#load \"File1.fs\"" 
                          "open MyNamespace.MyModule"
                          "printfn \"%d\" x"
                          ]
        TakeCoffeeBreak(this.VS)
        VerifyErrorListContainedExpetedStr("MyNamespace",project)
    
    [<Test>]
    member public this.``Fsx.HashLoad.Conditionals``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let fs = AddFileFromText(project,"File1.fs",
                                    ["module InDifferentFS"
                                     "#if INTERACTIVE"
                                     "let x = 1"
                                     "#else"
                                     "let y = 2"
                                     "#endif"
                                     "#if DEBUG"
                                     "let A = 3"
                                     "#else"
                                     "let B = 4"
                                     "#endif"
                                     ])            
        
        let fsx = AddFileFromText(project,"File2.fsx",
                                    [
                                     "#load \"File1.fs\"" 
                                     "InDifferentFS."
                                     ])    
        let fsx = OpenFile(project,"File2.fsx")

        MoveCursorToEndOfMarker(fsx, "InDifferentFS.")
        let completion = AutoCompleteAtCursor fsx
        let completion = completion |> Array.map (fun (CompletionItem(name, _, _, _, _)) -> name) |> set
        Assert.AreEqual(Set.count completion, 2, "Expected 2 elements in the completion list")
        Assert.IsTrue(completion.Contains "x", "Completion list should contain x because INTERACTIVE is defined")
        Assert.IsTrue(completion.Contains "B", "Completion list should contain B because DEBUG is not defined")
        

    /// FEATURE: Removing a #r into a file will cause it to no longer be seen by intellisense.
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.HashR.Removed``() =  
        let code =
                                    ["#light"
                                     "#r \"System.Transactions.dll\"" // Pick anything that isn't in the standard set of assemblies.
                                     "open System.Transactions"
                                     ]
        let (project, file) = createSingleFileFsxFromLines code
        TakeCoffeeBreak(this.VS)
        AssertNoErrorsOrWarnings(project)  
        
        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)
        ReplaceFileInMemory file
                        ["#light"
                         "//#r \"System.Transactions.dll\"" // <-- Comment this line
                         "open System.Transactions"
                         ]
        SaveFileToDisk(file)
        TakeCoffeeBreak(this.VS)
        VerifyErrorListContainedExpetedStr("Transactions",project)
        gpatcc.AssertExactly(notAA[file], notAA[file], true (* expectCreate, because dependent DLL set changed *))
    


    // Corecursive load to existing property.
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.NoError.ScriptClosure.TransitiveLoad3``() =  
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")    
        let file1 = AddFileFromText(project,"File1.fs",
                                    ["namespace Namespace"
                                     "type Foo = "
                                     "     static member public Property = 0"
                                     ])    
        let script2 = AddFileFromText(project,"Script2.fsx",
                                      ["#load \"Script1.fsx\""
                                       "#load \"File1.fs\""
                                       ])    
        let script1 = AddFileFromText(project,"Script1.fsx",
                                      ["#load \"Script2.fsx\""
                                       "#load \"File1.fs\""
                                       "Namespace.Foo.Property" 
                                       ])    
        let script1 = OpenFile(project,"Script1.fsx")   
        TakeCoffeeBreak(this.VS)
        AssertNoErrorsOrWarnings(project)
        
    // #load of .fsi is respected (for non-hidden property)
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.NoError.ScriptClosure.TransitiveLoad9``() =  
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")    
        let file1fsi = AddFileFromText(project,"File1.fsi",
                                      ["namespace Namespace"
                                       "type Foo ="
                                       "  class"
                                       "    static member Property : int" // Not exposing 'HiddenProperty'
                                       "  end"
                                       ])            
        let file1 = AddFileFromText(project,"File1.fs",
                                    ["namespace Namespace"
                                     "type Foo = "
                                     "     static member public HiddenProperty = 0"
                                     "     static member public Property = 0"
                                     ])    

        let script1 = AddFileFromText(project,"Script1.fsx",
                                      ["#load \"File1.fsi\""
                                       "#load \"File1.fs\""
                                       "Namespace.Foo.Property" 
                                       ])     
        let script1 = OpenFile(project,"Script1.fsx")   
        AssertNoErrorsOrWarnings(project) 

    // #load of .fsi is respected at second #load level (for non-hidden property) 
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.NoError.ScriptClosure.TransitiveLoad10``() =  
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")    
        let file1fsi = AddFileFromText(project,"File1.fsi",
                                      ["namespace Namespace"
                                       "type Foo ="
                                       "  class"
                                       "    static member Property : int" // Not exposing 'HiddenProperty'
                                       "  end"
                                       ])            
        let file1 = AddFileFromText(project,"File1.fs",
                                    ["namespace Namespace"
                                     "type Foo = "
                                     "     static member public HiddenProperty = 0"
                                     "     static member public Property = 0"
                                     ])    

        let script1 = AddFileFromText(project,"Script1.fsx",
                                      ["#load \"File1.fsi\""
                                       "#load \"File1.fs\""
                                       ])     
        let script2 = AddFileFromText(project,"Script2.fsx",
                                      ["#load \"Script1.fsx\""
                                       "Namespace.Foo.Property" 
                                       ])    
        let script2 = OpenFile(project,"Script2.fsx")   
        AssertNoErrorsOrWarnings(project) 

    // #load of .fsi is respected when dispersed between two #load levels (for non-hidden property)
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.NoError.ScriptClosure.TransitiveLoad11``() =  
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")    
        let file1fsi = AddFileFromText(project,"File1.fsi",
                                      ["namespace Namespace"
                                       "type Foo ="
                                       "  class"
                                       "    static member Property : int" // Not exposing 'HiddenProperty'
                                       "  end"
                                       ])            
        let file1 = AddFileFromText(project,"File1.fs",
                                    ["namespace Namespace"
                                     "type Foo = "
                                     "     static member public HiddenProperty = 0"
                                     "     static member public Property = 0"
                                     ])    

        let script1 = AddFileFromText(project,"Script1.fsx",
                                      ["#load \"File1.fsi\""
                                       ])     
        let script2 = AddFileFromText(project,"Script2.fsx",
                                      ["#load \"Script1.fsx\""
                                       "#load \"File1.fs\""
                                       "Namespace.Foo.Property" 
                                       ])    
        let script2 = OpenFile(project,"Script2.fsx")   
        AssertNoErrorsOrWarnings(project)  
        
    // #load of .fsi is respected when dispersed between two #load levels (the other way) (for non-hidden property)
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.NoError.ScriptClosure.TransitiveLoad12``() =  
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")    
        let file1fsi = AddFileFromText(project,"File1.fsi",
                                      ["namespace Namespace"
                                       "type Foo ="
                                       "  class"
                                       "    static member Property : int" // Not exposing 'HiddenProperty'
                                       "  end"
                                       ])            
        let file1 = AddFileFromText(project,"File1.fs",
                                    ["namespace Namespace"
                                     "type Foo = "
                                     "     static member public HiddenProperty = 0"
                                     "     static member public Property = 0"
                                     ])    

        let script1 = AddFileFromText(project,"Script1.fsx",
                                      ["#load \"File1.fs\""
                                       ])     
        let script2 = AddFileFromText(project,"Script2.fsx",
                                      ["#load \"File1.fsi\""
                                       "#load \"Script1.fsx\""
                                       "Namespace.Foo.Property" 
                                       ])    
        let script2 = OpenFile(project,"Script2.fsx")   
        AssertNoErrorsOrWarnings(project)  
        
    // #nowarn seen in closed .fsx is global to the closure
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.NoError.ScriptClosure.TransitiveLoad16``() =  
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")    
        let thisProject = AddFileFromText(project,"ThisProject.fsx",
                                      ["#nowarn \"44\""
                                       ])  
        let script1 = AddFileFromText(project,"Script1.fsx",
                                      ["#load \"ThisProject.fsx\"" // Should bring in #nowarn "44" so we don't see this warning:
                                       "[<System.Obsolete(\"x\")>]"
                                       "let fn x = 0"
                                       "let y = fn 1"
                                       ])                                                   

        let script1 = OpenFile(project,"Script1.fsx")   
        MoveCursorToEndOfMarker(script1,"let y = f") 
        TakeCoffeeBreak(this.VS) 
        AssertNoErrorsOrWarnings(project)   

    /// FEATURE: #r in .fsx to a .dll name works.
    [<Test>]
    member public this.``Fsx.NoError.HashR.DllWithNoPath``() =  
        let fileContent = """
            #light
            #r "System.Transactions.dll"
            open System.Transactions"""
        this.VerifyFSXNoErrorList(fileContent)


    [<Test>]
    // 'System' is in the default set. Make sure we can still resolve it.
    member public this.``Fsx.NoError.HashR.BugDefaultReferenceFileIsAlsoResolved``() =  
        let fileContent = """
            #light
            #r "System"
            """
        this.VerifyFSXNoErrorList(fileContent)

    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.NoError.HashR.DoubleReference``() =  
        let fileContent = """
            #light
            #r "System"
            #r "System"
            """
        this.VerifyFSXNoErrorList(fileContent)

    [<Test>]
    [<Category("fsx closure")>]
    // 'CustomMarshalers' is loaded from the GAC _and_ it is available on XP and above.
    member public this.``Fsx.NoError.HashR.ResolveFromGAC``() =  
        let fileContent = """
            #light
            #r "CustomMarshalers"
            """
        this.VerifyFSXNoErrorList(fileContent)

    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.NoError.HashR.ResolveFromFullyQualifiedPath``() =
        let fullyqualifiepathtoddll = System.IO.Path.Combine( System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "System.configuration.dll" )
        let code = ["#light";"#r @\"" + fullyqualifiepathtoddll + "\""]
        let (project, _) = createSingleFileFsxFromLines code
        AssertNoErrorsOrWarnings(project)

    [<Test>]
    [<Category("fsx closure")>]
    [<Ignore("Re-enable this test --- https://github.com/Microsoft/visualfsharp/issues/5238")>]
    member public this.``Fsx.NoError.HashR.RelativePath1``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let file1 = AddFileFromText(project,"lib.fs",
                                    ["module Lib"
                                     "let X = 42"
                                     ])
        
        let bld = Build(project)

        let script1Dir = Path.Combine(ProjectDirectory(project), "ccc")
        let script1Path = Path.Combine(script1Dir, "Script1.fsx")
        let script2Dir = Path.Combine(ProjectDirectory(project), "aaa\\bbb")
        let script2Path = Path.Combine(script2Dir, "Script2.fsx")
        
        Directory.CreateDirectory(script1Dir) |> ignore
        Directory.CreateDirectory(script2Dir) |> ignore
        File.Move(bld.ExecutableOutput, Path.Combine(ProjectDirectory(project), "aaa\\lib.exe"))

        let script1 = File.WriteAllLines(script1Path,
                                      ["#load \"../aaa/bbb/Script2.fsx\""
                                       "printfn \"%O\" Lib.X"
                                       ])
        let script2 = File.WriteAllLines(script2Path,
                                      ["#r \"../lib.exe\""
                                       ])

        let script1 = OpenFile(project, script1Path)   
        TakeCoffeeBreak(this.VS)
        
        MoveCursorToEndOfMarker(script1,"#load")
        let ans = GetSquiggleAtCursor(script1)
        AssertNoSquiggle(ans)

    [<Test; Category("fsx closure")>]
    [<Ignore("Re-enable this test --- https://github.com/Microsoft/visualfsharp/issues/5238")>]
    member public this.``Fsx.NoError.HashR.RelativePath2``() = 
        use _guard = this.UsingNewVS()  
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let file1 = AddFileFromText(project,"lib.fs",
                                    ["module Lib"
                                     "let X = 42"
                                     ])

        let bld = Build(project)

        let script1Dir = Path.Combine(ProjectDirectory(project), "ccc")
        let script1Path = Path.Combine(script1Dir, "Script1.fsx")
        let script2Dir = Path.Combine(ProjectDirectory(project), "aaa")
        let script2Path = Path.Combine(script2Dir, "Script2.fsx")
        
        Directory.CreateDirectory(script1Dir) |> ignore
        Directory.CreateDirectory(script2Dir) |> ignore
        File.Move(bld.ExecutableOutput, Path.Combine(ProjectDirectory(project), "aaa\\lib.exe"))

        let script1 = File.WriteAllLines(script1Path,
                                      ["#load \"../aaa/Script2.fsx\""
                                       "printfn \"%O\" Lib.X"
                                       ])
        let script2 = File.WriteAllLines(script2Path,
                                      ["#r \"lib.exe\""
                                       ])
                                       
        let script1 = OpenFile(project, script1Path)   
        TakeCoffeeBreak(this.VS)
        
        MoveCursorToEndOfMarker(script1,"#load")
        let ans = GetSquiggleAtCursor(script1)
        AssertNoSquiggle(ans)

     /// FEATURE: #load in an .fsx file will include that file in the 'build' of the .fsx.
    [<Test>]
    member public this.``Fsx.NoError.HashLoad.Simple``() =  
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let fs = AddFileFromText(project,"File1.fs",
                                    ["#light"
                                     "namespace MyNamespace" 
                                     "    module MyModule ="
                                     "        let x = 1" 
                                     ])            
        
        let fsx = AddFileFromText(project,"File2.fsx",
                                    ["#light"
                                     "#load \"File1.fs\"" 
                                     "open MyNamespace.MyModule"
                                     "printfn \"%d\" x"
                                     ])    
        let fsx = OpenFile(project,"File2.fsx")    
        AssertNoErrorsOrWarnings(project)

    // In this bug the #loaded file contains a level-4 warning (copy to avoid mutation). This warning was reported at the #load in file2.fsx but shouldn't have been.s
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.NoWarn.OnLoadedFile.Bug4837``() =  
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let fs = AddFileFromText(project,"File1.fs",
                                    ["module File1Module"
                                     "let x = System.DateTime.Now - System.DateTime.Now"
                                     "x.Add(x) |> ignore" 
                                     ])            
        
        let fsx = AddFileFromText(project,"File2.fsx",
                                    [
                                     "#load \"File1.fs\"" 
                                     ])    
        let fsx = OpenFile(project,"File2.fsx")    
        AssertNoErrorsOrWarnings(project) 

    /// FEATURE: .fsx files have automatic imports of certain system assemblies.
    //There is a test bug here. The actual scenario works. Need to revisit.
    [<Test>]
    [<Category("ReproX")>]  
    member public this.``Fsx.NoError.AutomaticImportsForFsxFiles``() =
        let fileContent = """
            #light
            open System
            open System.Xml
            open System.Drawing
            open System.Runtime.Remoting
            open System.Runtime.Serialization.Formatters.Soap
            open System.Data
            open System.Drawing
            open System.Web
            open System.Web.Services
            open System.Windows.Forms"""
        this.VerifyFSXNoErrorList(fileContent) 

    // Corecursive load to nonexisting property.
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.ExactlyOneError.ScriptClosure.TransitiveLoad4``() =  
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")    
        let file1 = AddFileFromText(project,"File1.fs",
                                    ["namespace Namespace"
                                     "type Foo = "
                                     "     static member public Property = 0"
                                     ])    
        let script2 = AddFileFromText(project,"Script2.fsx",
                                      ["#load \"Script1.fsx\""
                                       "#load \"File1.fs\""
                                       ])    
        let script1 = AddFileFromText(project,"Script1.fsx",
                                      ["#load \"Script2.fsx\""
                                       "#load \"File1.fs\""
                                       "Namespace.Foo.NonExistingProperty" 
                                       ])     
        let script1 = OpenFile(project,"Script1.fsx")   
        AssertExactlyOneErrorSeenContaining(project, "NonExistingProperty")  

    // #load of .fsi is respected
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.ExactlyOneError.ScriptClosure.TransitiveLoad5``() =  
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")    
        let file1fsi = AddFileFromText(project,"File1.fsi",
                                      ["namespace Namespace"
                                       "type Foo ="
                                       "  class"
                                       "    static member Property : int" // Not exposing 'HiddenProperty'
                                       "  end"
                                       ])            
        let file1 = AddFileFromText(project,"File1.fs",
                                    ["namespace Namespace"
                                     "type Foo = "
                                     "     static member public HiddenProperty = 0"
                                     "     static member public Property = 0"
                                     ])    

        let script1 = AddFileFromText(project,"Script1.fsx",
                                      ["#load \"File1.fsi\""
                                       "#load \"File1.fs\""
                                       "Namespace.Foo.HiddenProperty" 
                                       ])     
        let script1 = OpenFile(project,"Script1.fsx")   
        AssertExactlyOneErrorSeenContaining(project, "HiddenProperty")   

    // #load of .fsi is respected at second #load level
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.ExactlyOneError.ScriptClosure.TransitiveLoad6``() =  
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")    
        let file1fsi = AddFileFromText(project,"File1.fsi",
                                      ["namespace Namespace"
                                       "type Foo ="
                                       "  class"
                                       "    static member Property : int" // Not exposing 'HiddenProperty'
                                       "  end"
                                       ])            
        let file1 = AddFileFromText(project,"File1.fs",
                                    ["namespace Namespace"
                                     "type Foo = "
                                     "     static member public HiddenProperty = 0"
                                     "     static member public Property = 0"
                                     ])    

        let script1 = AddFileFromText(project,"Script1.fsx",
                                      ["#load \"File1.fsi\""
                                       "#load \"File1.fs\""
                                       ])     
        let script2 = AddFileFromText(project,"Script2.fsx",
                                      ["#load \"Script1.fsx\""
                                       "Namespace.Foo.HiddenProperty" 
                                       ])    
        let script2 = OpenFile(project,"Script2.fsx")   
        AssertExactlyOneErrorSeenContaining(project, "HiddenProperty") 
        
    // #load of .fsi is respected when dispersed between two #load levels
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.ExactlyOneError.ScriptClosure.TransitiveLoad7``() =  
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")    
        let file1fsi = AddFileFromText(project,"File1.fsi",
                                      ["namespace Namespace"
                                       "type Foo ="
                                       "  class"
                                       "    static member Property : int" // Not exposing 'HiddenProperty'
                                       "  end"
                                       ])            
        let file1 = AddFileFromText(project,"File1.fs",
                                    ["namespace Namespace"
                                     "type Foo = "
                                     "     static member public HiddenProperty = 0"
                                     "     static member public Property = 0"
                                     ])    

        let script1 = AddFileFromText(project,"Script1.fsx",
                                      ["#load \"File1.fsi\""
                                       ])     
        let script2 = AddFileFromText(project,"Script2.fsx",
                                      ["#load \"Script1.fsx\""
                                       "#load \"File1.fs\""
                                       "Namespace.Foo.HiddenProperty" 
                                       ])    
        let script2 = OpenFile(project,"Script2.fsx")   
        AssertExactlyOneErrorSeenContaining(project, "HiddenProperty")    
        
    // #load of .fsi is respected when dispersed between two #load levels (the other way)
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.ExactlyOneError.ScriptClosure.TransitiveLoad8``() =  
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")    
        let file1fsi = AddFileFromText(project,"File1.fsi",
                                      ["namespace Namespace"
                                       "type Foo ="
                                       "  class"
                                       "    static member Property : int" // Not exposing 'HiddenProperty'
                                       "  end"
                                       ])            
        let file1 = AddFileFromText(project,"File1.fs",
                                    ["namespace Namespace"
                                     "type Foo = "
                                     "     static member public HiddenProperty = 0"
                                     "     static member public Property = 0"
                                     ])    

        let script1 = AddFileFromText(project,"Script1.fsx",
                                      ["#load \"File1.fs\""
                                       ])     
        let script2 = AddFileFromText(project,"Script2.fsx",
                                      ["#load \"File1.fsi\""
                                       "#load \"Script1.fsx\""
                                       "Namespace.Foo.HiddenProperty" 
                                       ])    
        let script2 = OpenFile(project,"Script2.fsx")   
        AssertExactlyOneErrorSeenContaining(project, "HiddenProperty")   
        
    // Bug seen during development: A #load in an .fs would be followed.
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.ExactlyOneError.ScriptClosure.TransitiveLoad15``() =  
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")    
        let file2 = AddFileFromText(project,"File2.fs",
                                      ["namespace Namespace"
                                       "type Type() ="
                                       "    static member Property = 0"
                                       ])  
        let file1 = AddFileFromText(project,"File1.fs",
                                      ["#load \"File2.fs\""  // This is not allowed but it was working anyway.
                                       "namespace File2Namespace"
                                       ])                                              
        let script1 = AddFileFromText(project,"Script1.fsx",
                                      ["#load \"File1.fs\""
                                       "Namespace.Type.Property"
                                       ])                                                   

        let script1 = OpenFile(project,"Script1.fsx")   
        TakeCoffeeBreak(this.VS)
        AssertExactlyOneErrorSeenContaining(project, "Namespace")  

    [<Test>]
    member public this.``Fsx.Bug4311HoverOverReferenceInFirstLine``() =
        let fileContent = """#r "PresentationFramework.dll"
                             
                             #r "PresentationCore.dll" """
        let marker = "#r \"PresentationFrame"
        this.AssertQuickInfoContainsAtEndOfMarkerInFsxFile fileContent marker "PresentationFramework.dll"
        this.AssertQuickInfoNotContainsAtEndOfMarkerInFsxFile fileContent marker "multiple results"

    [<Test>]
    member public this.``Fsx.QuickInfo.Bug4979``() =
        let code = 
                ["System.ConsoleModifiers.Shift |> ignore "
                 "(3).ToString().Length |> ignore "]
        let (project, file) = createSingleFileFsxFromLines code
        MoveCursorToEndOfMarker(file, "System.ConsoleModifiers.Sh")
        let tooltip = GetQuickInfoAtCursor file
        AssertContains(tooltip, @"<summary>The left or right SHIFT modifier key.</summary>")    
        
        MoveCursorToEndOfMarker(file, "(3).ToString().Len")
        let tooltip = GetQuickInfoAtCursor file
        AssertContains(tooltip, @"[Signature:P:System.String.Length]") // A message from the mock IDocumentationBuilder
        AssertContains(tooltip, @"[Filename:") 
        AssertContains(tooltip, @"netstandard.dll]") // The assembly we expect the documentation to get taken from  

    // Especially under 4.0 we need #r of .NET framework assemblies to resolve from like,
    //
    //      %program files%\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0
    //
    // because this is where the the .XML files are.
    //
    // When executing scripts, however, we need to _not_ resolve from these directories because
    // they may be metadata-only assemblies.
    //
    // "Reference Assemblies" was only introduced in 3.5sp1, so not all 2.0 F# boxes will have it, so only run on 4.0
    [<Test>]
    member public this.``Fsx.Bug5073``() =
        let fileContent = """#r "System" """
        let marker = "#r \"System"
        this.AssertQuickInfoContainsAtEndOfMarkerInFsxFile fileContent marker @"Reference Assemblies\Microsoft"
        this.AssertQuickInfoContainsAtEndOfMarkerInFsxFile fileContent marker ".NET Framework"

    /// FEATURE: Hovering over a resolved #r file will show a data tip with the fully qualified path to that file.
    [<Test>]
    member public this.``Fsx.HashR_QuickInfo.ShowFilenameOfResolvedAssembly``() =
        this.AssertQuickInfoContainsAtEndOfMarkerInFsxFile
            """#r "System.Transactions" """ // Pick anything that isn't in the standard set of assemblies.
            "#r \"System.Tra" "System.Transactions.dll"

    [<Test>]
    member public this.``Fsx.HashR_QuickInfo.BugDefaultReferenceFileIsAlsoResolved``() =
        this.AssertQuickInfoContainsAtEndOfMarkerInFsxFile
            """#r "System" """  // 'System' is in the default set. Make sure we can still resolve it.
            "#r \"Syst" "System.dll"
        
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.HashR_QuickInfo.DoubleReference``() =
        let fileContent = """#r "System" // Mark1
                             #r "System" // Mark2 """   // The same reference repeated twice. 
        this.AssertQuickInfoContainsAtStartOfMarkerInFsxFile fileContent "tem\" // Mark1" "System.dll"
        this.AssertQuickInfoContainsAtStartOfMarkerInFsxFile fileContent "tem\" // Mark2" "System.dll"
        
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.HashR_QuickInfo.ResolveFromGAC``() = 
        this.AssertQuickInfoContainsAtEndOfMarkerInFsxFile
            """#r "CustomMarshalers" """        // 'mscorcfg' is loaded from the GAC _and_ it is available on XP and above.
            "#r \"Custo" ".NET Framework"

    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.HashR_QuickInfo.ResolveFromFullyQualifiedPath``() = 
        let fullyqualifiepathtoddll = System.IO.Path.Combine( System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "System.configuration.dll" ) // Can be any fully qualified path to an assembly
        let expectedtooltip = System.Reflection.Assembly.ReflectionOnlyLoadFrom(fullyqualifiepathtoddll).FullName
        let fileContent = "#r @\"" + fullyqualifiepathtoddll + "\""
        let marker = "#r @\"" + fullyqualifiepathtoddll.Substring(0,fullyqualifiepathtoddll.Length/2)       // somewhere in the middle of the string
        this.AssertQuickInfoContainsAtEndOfMarkerInFsxFile fileContent marker expectedtooltip
        //this.AssertQuickInfoNotContainsAtEndOfMarkerInFsxFile fileContent marker ".dll"

    [<Test>]
    member public this.``Fsx.InvalidHashReference.ShouldBeASquiggle.Bug3012``() =  
        let code = 
            ["#light"
             "#r \"Bar.dll\""]
        let (project, file) = createSingleFileFsxFromLines code
        MoveCursorToEndOfMarker(file,"#r \"Ba") 
        let squiggle = GetSquiggleAtCursor(file)
        TakeCoffeeBreak(this.VS)
        Assert.IsTrue(snd squiggle.Value |> fun str -> str.Contains("Bar.dll"))

    // Bug seen during development: The unresolved reference error would x-ray through to the root.
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.ScriptClosure.TransitiveLoad14``() =  
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")    
        let script2 = AddFileFromText(project,"Script2.fsx",
                                      ["#load \"Script1.fsx\""
                                       "#r \"NonExisting\""
                                       ])      
        let script1 = AddFileFromText(project,"Script1.fsx",
                                      ["#load \"Script2.fsx\""
                                       "#r \"System\""
                                       ])                                                   

        let script1 = OpenFile(project,"Script1.fsx")   
        TakeCoffeeBreak(this.VS)      
        MoveCursorToEndOfMarker(script1,"#r \"Sys") 
        AssertEqual(None,GetSquiggleAtCursor(script1))
    
    member private this.TestFsxHashDirectivesAreErrors(mark : string, expectedStr : string) = 
        let code = 
                                    ["#light"
                                     "#r \"JoeBob\""
                                     "#I \".\""
                                     "#load \"Dooby\""
                                     ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        MoveCursorToEndOfMarker(file,mark) 
        let ans = GetSquiggleAtCursor(file)
        match ans with
        | Some(sev,msg) -> 
            AssertEqual(Microsoft.VisualStudio.FSharp.LanguageService.Severity.Error,sev)
            AssertContains(msg,expectedStr)
        | _ -> Assert.Fail() 

    /// FEATURE: #r, #I, #load are all errors when running under the language service
    [<Test>]
    member public this.``Fsx.HashDirectivesAreErrors.InNonScriptFiles.Case1``() =  
        this.TestFsxHashDirectivesAreErrors("#r \"Joe","#r")
     
    [<Test>]
    member public this.``Fsx.HashDirectivesAreErrors.InNonScriptFiles.Case2``() =  
        this.TestFsxHashDirectivesAreErrors("#I \"","#I")      
        
    [<Test>]
    member public this.``Fsx.HashDirectivesAreErrors.InNonScriptFiles.Case3``() =  
        this.TestFsxHashDirectivesAreErrors("#load \"Doo","may only be used in F# script files")      

    /// FEATURE: #reference against a non-assembly .EXE gives a reasonable error message
    //[<Test>]
    member public this.``Fsx.HashReferenceAgainstNonAssemblyExe``() = 
        let windows = System.Environment.GetEnvironmentVariable("windir")
        let code =
                                    ["#light"
                                     sprintf "#reference @\"%s\"" (Path.Combine(windows,"notepad.exe"))
                                     "    let x = 1"]
        let (_, file) = createSingleFileFsxFromLines code
        
        MoveCursorToEndOfMarker(file,"#refe")
        let ans = GetSquiggleAtCursor(file)
        AssertSquiggleIsErrorContaining(ans, "was not found or is invalid")

    (* ---------------------------------------------------------------------------------- *)

     // FEATURE: A #loaded file is squiggled with an error if there are errors in that file.
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.HashLoadedFileWithErrors.Bug3149``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        
        let file1 = AddFileFromText(project,"File1.fs", 
                                    ["#light"
                                     "module File1" 
                                     "DogChow" // <-- error
                                    ])

        let file2 = AddFileFromText(project,"File2.fsx",
                                    ["#light"
                                     "#load @\"File1.fs\""
                                     ])
        let file2 = OpenFile(project,"File2.fsx")
        
        MoveCursorToEndOfMarker(file2,"#load @\"Fi")
        TakeCoffeeBreak(this.VS)        
        let ans = GetSquiggleAtCursor(file2)    
        AssertSquiggleIsErrorContaining(ans, "DogChow")      
        
        
     // FEATURE: A #loaded file is squiggled with a warning if there are warning that file.
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.HashLoadedFileWithWarnings.Bug3149``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        
        let file1 = AddFileFromText(project,"File1.fs", 
                                    ["module File1Module"
                                     "type WarningHere<'a> = static member X() = 0"
                                     "let y = WarningHere.X"
                                    ])

        let file2 = AddFileFromText(project,"File2.fsx",
                                    ["#light"
                                     "#load @\"File1.fs\""
                                     ])
        let file2 = OpenFile(project,"File2.fsx")
        
        MoveCursorToEndOfMarker(file2,"#load @\"Fi")
        let ans = GetSquiggleAtCursor(file2)
        AssertSquiggleIsWarningContaining(ans, "WarningHere")  

     // Bug: #load should report the first error message from a file
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.HashLoadedFileWithErrors.Bug3652``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        
        let file1 = AddFileFromText(project,"File1.fs", 
                                    [ "#light"
                                      "module File1" 
                                      "let a = 1 + \"\""
                                      "let c = new obj()"
                                      "let b = c.foo()"
                                    ])
        let file2 = AddFileFromText(project,"File2.fsx",
                                    ["#light"
                                     "#load @\"File1.fs\""
                                     ])
        let file2 = OpenFile(project,"File2.fsx")
       
        MoveCursorToEndOfMarker(file2,"#load @\"Fi")
        let ans = GetSquiggleAtCursor(file2)
        AssertSquiggleIsErrorContaining(ans, "'string'")
        AssertSquiggleIsErrorContaining(ans, "'int'")
        AssertSquiggleIsErrorNotContaining(ans, "foo")

    // In this bug the .fsx project directory was wrong so it couldn't reference a relative file.
    [<Test>]
    member public this.``Fsx.ScriptCanReferenceBinDirectoryOutput.Bug3151``() =
        use _guard = this.UsingNewVS()
        let stopWatch = new System.Diagnostics.Stopwatch()
        let ResetStopWatch() = stopWatch.Reset(); stopWatch.Start()
        let time1 op a message = 
            ResetStopWatch()
            let result = op a
            printf "%s %d ms\n" message stopWatch.ElapsedMilliseconds
            result
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let file1 = AddFileFromText(project,"File1.fs", ["#light"])
        let projectOutput = time1 Build project "Time to build project"
        printfn "Output of building project was %s" projectOutput.ExecutableOutput
        printfn "Project directory is %s" (ProjectDirectory project)
        
        let file2 = AddFileFromText(project,"File2.fsx",
                                    ["#light"
                                     "#reference @\"bin\\Debug\\testproject.exe\""
                                     ])
        let file2 = OpenFile(project,"File2.fsx")
        
        MoveCursorToEndOfMarker(file2,"#reference @\"bin\\De")
        let ans = GetSquiggleAtCursor(file2)
        AssertNoSquiggle(ans)

               

    /// In this bug, multiple references to mscorlib .dll were causing problem in load closure
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.BugAllowExplicitReferenceToMsCorlib``() =  
        let code =
                                    ["#r \"mscorlib\""
                                     "fsi."
                                     ]
        let (_, file) = createSingleFileFsxFromLines code
        MoveCursorToEndOfMarker(file,"fsi.")
        TakeCoffeeBreak(this.VS)
        let completions = AutoCompleteAtCursor file
        AssertCompListContains(completions,"CommandLineArgs")        
        
    /// FEATURE: There is a global fsi module that should be in scope for script files.        
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.Bug2530FsiObject``() =  
        let code = 
                                    ["#light"
                                     "fsi."
                                     ]
        let (_, file) = createSingleFileFsxFromLines code
        MoveCursorToEndOfMarker(file,"fsi.")
        TakeCoffeeBreak(this.VS)
        let completions = AutoCompleteAtCursor file
        AssertCompListContains(completions,"CommandLineArgs")
  
    // Ensure that the script closure algorithm gets the right order of hash directives
    [<Test>]
    [<Category("fsx closure")>]
    member public this.``Fsx.ScriptClosure.SurfaceOrderOfHashes``() =  
        let code = 
                                    ["#r \"System.Runtime.Remoting\""
                                     "#r \"System.Transactions\""
                                     "#load \"Load1.fs\""
                                     "#load \"Load2.fsx\""
                                     ]
        let (project, file) = createSingleFileFsxFromLines code
        let projectFolder = ProjectDirectory(project)
        let fas = GetProjectOptionsOfScript(file)
        AssertArrayContainsPartialMatchOf(fas.OtherOptions, "--noframework")
        AssertArrayContainsPartialMatchOf(fas.OtherOptions, "System.Runtime.Remoting.dll")
        AssertArrayContainsPartialMatchOf(fas.OtherOptions, "System.Transactions.dll")
        AssertArrayContainsPartialMatchOf(fas.OtherOptions, "FSharp.Compiler.Interactive.Settings.dll")
        Assert.AreEqual(Path.Combine(projectFolder,"File1.fsx"), fas.SourceFiles.[0])
        Assert.AreEqual(1, fas.SourceFiles.Length)


    /// FEATURE: #reference against a strong name should work.
    [<Test>]
    member public this.``Fsx.HashReferenceAgainstStrongName``() = 
        let code =
                                            ["#light"
                                             sprintf "#reference \"System.Core, Version=%s, Culture=neutral, PublicKeyToken=b77a5c561934e089\"" (System.Environment.Version.ToString())
                                             "open System."]
        let (_, file) = createSingleFileFsxFromLines code
        MoveCursorToEndOfMarker(file,"open System.") 
        let completions = AutoCompleteAtCursor file
        AssertCompListContains(completions,"Linq")   


    /// Try out some bogus file names in #r, #I and #load.
    [<Test>]
    member public this.``Fsx.InvalidMetaCommandFilenames``() =
        let code = 
                                    [
                                     "#r @\"\""
                                     "#load @\"\""
                                     "#I @\"\""
                                     "#r @\"*\""
                                     "#load @\"*\""
                                     "#I @\"*\""
                                     "#r @\"?\""
                                     "#load @\"?\""
                                     "#I @\"?\""
                                     """#r @"C:\path\does\not\exist.dll"  """
                                    ]
        let (_, file) = createSingleFileFsxFromLines code
        TakeCoffeeBreak(this.VS) // This used to assert

    /// FEATURE: .fsx files have INTERACTIVE #defined
    [<Test>]
    member public this.``Fsx.INTERACTIVEIsDefinedInFsxFiles``() =
        let code =
                                    [
                                     "#if INTERACTIVE"
                                     "let xyz = 1"
                                     "#endif"
                                    ]
        let (_, file) = createSingleFileFsxFromLines code
        MoveCursorToEndOfMarker(file,"let xy")
        AssertEqual(TokenType.Identifier ,GetTokenTypeAtCursor(file))  

    // Ensure that basic compile of an .fsx works        
    [<Test>]
    [<Category("fsx closure")>]
    [<Category("fsx compile")>]
    member public this.``Fsx.CompileFsx_1``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        
        let file1 = AddFileFromTextEx(project,"Script.fsx","Script.fsx",BuildAction.Compile,
                                      ["printfn \"Hello world\""])
        let build = time1 Build project "Time to build project"
        Assert.IsTrue(build.BuildSucceeded, "Expected build to succeed")
        ShowErrors(project)
        

    // Compile a script which #loads a source file. The build can't succeed without the loaded file.      
    [<Test; Category("Expensive")>]
    [<Category("fsx closure")>]
    [<Category("fsx compile")>]
    member public this.``Fsx.CompileFsx_2``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let fs = AddFileFromTextEx(project,"File.fs","File.fs",BuildAction.Compile,
                                      ["namespace Namespace"
                                       "module Module ="
                                       "  let Value = 1"
                                      ])
        let fsx = AddFileFromTextEx(project,"Script.fsx","Script.fsx",BuildAction.Compile,
                                      ["#load \"File.fs\""
                                       "printfn \"%d\" Namespace.Module.Value"])
        let build = time1 Build project "Time to build project"
        if SupportsOutputWindowPane(this.VS) then 
            let lines = GetOutputWindowPaneLines(this.VS)
            for line in lines do printfn "%s" line
            ()
        Assert.IsTrue(build.BuildSucceeded, "Expected build to succeed")
        
    // Compile a script which #loads a source file. The build can't succeed without 
    [<Test; Category("Expensive")>]
    [<Category("fsx closure")>]
    [<Category("fsx compile")>]
    member public this.``Fsx.CompileFsx_3``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let fs = AddFileFromTextEx(project,"File.fs","File.fs",BuildAction.None,
                                      ["namespace Namespace"
                                       "module Module ="
                                       "  let Value = 1"
                                      ])
        let fsx = AddFileFromTextEx(project,"Script.fsx","Script.fsx",BuildAction.Compile,
                                      ["#load \"File.fs\""
                                       "printfn \"%d\" Namespace.Module.Value"])
        let build = time1 Build project "Time to build project" 
        if SupportsOutputWindowPane(this.VS) then 
            let lines = GetOutputWindowPaneLines(this.VS)
            for line in lines do printfn "%s" line
            ()
        Assert.IsTrue(build.BuildSucceeded, "Expected build to succeed")        
        
    // Must be explicitly referenced by compile.
    [<Test>]
    [<Category("fsx closure")>]
    [<Category("fsx compile")>]
    member public this.``Fsx.CompileFsx_Bug5416_1``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let fsx = AddFileFromTextEx(project,"Script.fsx","Script.fsx",BuildAction.Compile,
                                      ["let x = fsi.CommandLineArgs"])
        let build = time1 Build project "Time to build project" 
        if SupportsOutputWindowPane(this.VS) then 
            let lines = GetOutputWindowPaneLines(this.VS)
            for line in lines do printfn "%s" line
            ()
        Assert.IsTrue(not(build.BuildSucceeded), "Expected build to fail")    
        
    // Must be explicitly referenced by compile.
    [<Test>]
    [<Category("fsx closure")>]
    [<Category("fsx compile")>]
    member public this.``Fsx.CompileFsx_Bug5416_2``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let binariesFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
        PlaceIntoProjectFileBeforeImport
            (project, sprintf @"
                <ItemGroup>
                    <!-- Subtle: You need this reference to compile but not to get language service -->
                    <Reference Include=""FSharp.Compiler.Interactive.Settings"">
                        <HintPath>%s\\FSharp.Compiler.Interactive.Settings.dll</HintPath>
                    </Reference>
                    <Reference Include=""FSharp.Compiler.Service"">
                        <HintPath>%s\\FSharp.Compiler.Service.dll</HintPath>
                    </Reference>
                </ItemGroup>" binariesFolder binariesFolder)

        let fsx = AddFileFromTextEx(project,"Script.fsx","Script.fsx",BuildAction.Compile,
                                      ["let x = fsi.CommandLineArgs"])
        let build = time1 Build project "Time to build project" 
        if SupportsOutputWindowPane(this.VS) then 
            let lines = GetOutputWindowPaneLines(this.VS)
            for line in lines do printfn "%s" line
            ()
        if not(SupportsOutputWindowPane(this.VS)) then  
            Assert.IsTrue(build.BuildSucceeded, "Expected build to succeed")                
        
        
    // Ensure that #load order is preserved when #loading multiple files. 
    [<Test>]
    [<Category("fsx closure")>]
    [<Category("fsx compile")>]
    member public this.``Fsx.CompileFsx_5``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let fs1 = AddFileFromTextEx(project,"File1.fs","File1.fs",BuildAction.None,
                                      ["namespace Namespace"
                                       "module Module1 ="
                                       "  let Value = 1"
                                      ])
        let fs2 = AddFileFromTextEx(project,"File2.fs","File2.fs",BuildAction.None,
                                      ["namespace Namespace"
                                       "module Module2 ="
                                       "  let Value = Module1.Value"
                                      ])                                      
        let fsx = AddFileFromTextEx(project,"Script.fsx","Script.fsx",BuildAction.Compile,
                                      [
                                       "#load \"File1.fs\""
                                       "#load \"File2.fs\""
                                       "printfn \"%d\" Namespace.Module2.Value"])
        let build = time1 Build project "Time to build project" 
        if SupportsOutputWindowPane(this.VS) then 
            let lines = GetOutputWindowPaneLines(this.VS)
            for line in lines do printfn "%s" line
            ()
        Assert.IsTrue(build.BuildSucceeded, "Expected build to succeed")          
        
    // If an fs file is explicitly passed in to the compiler and also #loaded then 
    // the command-line order is respected rather than the #load order
    [<Test>]
    [<Category("fsx closure")>]
    [<Category("fsx compile")>]
    member public this.``Fsx.CompileFsx_6``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let fs1 = AddFileFromTextEx(project,"File1.fs","File1.fs",BuildAction.Compile,
                                      ["namespace Namespace"
                                       "module Module1 ="
                                       "  let Value = 1"
                                      ])
        let fs2 = AddFileFromTextEx(project,"File2.fs","File2.fs",BuildAction.Compile,
                                      ["namespace Namespace"
                                       "module Module2 ="
                                       "  let Value = Module1.Value"
                                      ])                                      
        let fsx = AddFileFromTextEx(project,"Script.fsx","Script.fsx",BuildAction.Compile,
                                      [
                                       "#load \"File2.fs\"" // Wrong order
                                       "#load \"File1.fs\""
                                       "printfn \"%d\" Namespace.Module2.Value"])
        let build = time1 Build project "Time to build project" 
        if SupportsOutputWindowPane(this.VS) then 
            let lines = GetOutputWindowPaneLines(this.VS)
            for line in lines do printfn "%s" line
            ()
        Assert.IsTrue(build.BuildSucceeded, "Expected build to succeed") 

        
        
    // If a #loaded file does not exist, there should be an error
    [<Test>]
    [<Category("fsx closure")>]
    [<Category("fsx compile")>]
    member public this.``Fsx.CompileFsx_7``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let fsx = AddFileFromTextEx(project,"Script.fsx","Script.fsx",BuildAction.Compile,
                                      [
                                       "#load \"NonexistentFile.fs\""
                                       ])
        let build = time1 Build project "Time to build project" 
        if SupportsOutputWindowPane(this.VS) then 
            let lines = GetOutputWindowPaneLines(this.VS)
            for line in lines do printfn "%s" line
            AssertListContainsInOrder(lines, ["error FS0079: Could not load file"; "NonexistentFile.fs"; "because it does not exist or is inaccessible"])            
            
        Assert.IsTrue(not(build.BuildSucceeded), "Expected build to fail")       
        
        
    // #r references should be respected.
    [<Test>]
    [<Category("fsx closure")>]
    [<Category("fsx compile")>]
    member public this.``Fsx.CompileFsx_8``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let fsx = AddFileFromTextEx(project,"Script.fsx","Script.fsx",BuildAction.Compile,
                                      [
                                       "#r \"System.Messaging\""
                                       "let a = new System.Messaging.AccessControlEntry()"
                                       ])
        let build = time1 Build project "Time to build project" 
        if SupportsOutputWindowPane(this.VS) then 
            let lines = GetOutputWindowPaneLines(this.VS)
            for line in lines do printfn "%s" line
            
        Assert.IsTrue(build.BuildSucceeded, "Expected build to succeed")          
        
        
    // Missing script file should be a reasonable failure, not a callstack.
    [<Test>]
    [<Category("fsx closure")>]
    [<Category("fsx compile")>]
    member public this.``Fsx.CompileFsx_Bug5414``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let fsx = AddFileFromTextEx(project,"Script.fsx","Script.fsx",BuildAction.Compile,[])
        DeleteFileFromDisk(this.VS, fsx) 
        
        let build = Build project
        if SupportsOutputWindowPane(this.VS) then 
            let lines = GetOutputWindowPaneLines(this.VS)
            AssertListContainsInOrder(lines, 
                                      ["Could not find file "
                                       "Script.fsx"])           
            for line in lines do 
                printfn "%s" line
                AssertNotContains(line,"error MSB") // Microsoft.FSharp.Targets(135,9): error MSB6006: "fsc.exe" exited with code -532462766.

        Assert.IsTrue(not(build.BuildSucceeded), "Expected build to fail")                                  
        
        
    /// There was a problem in which synthetic tokens like #load were causing asserts
    [<Test; Category("Expensive")>]
    member public this.``Fsx.SyntheticTokens``() =     
        Helper.ExhaustivelyScrutinize(
            this.TestRunner,
              ["#light"
               "#r \"\""
               "#reference \"\""
               "#load \"\""
               "#line 52"
               "#nowarn 72"]        
            )
                                 
    /// There was a problem where an unclosed reference picked up the text of the reference on the next line.
    [<Test>]
    member public this.``Fsx.ShouldBeAbleToReference30Assemblies.Bug2050``() =     
        let code = 
                                    ["#light"
                                     "#r \"System.Core.dll\""
                                     "open System."
                                     ]
        let (_, file) = createSingleFileFsxFromLines code
        MoveCursorToEndOfMarker(file,"open System.") 
        let completions = AutoCompleteAtCursor file
        AssertCompListContains(completions,"Linq")

    /// There was a problem where an unclosed reference picked up the text of the reference on the next line.
    [<Test>]
    member public this.``Fsx.UnclosedHashReference.Case1``() =     
        Helper.ExhaustivelyScrutinize(
            this.TestRunner,
           ["#light"
            "#reference \"" // Unclosed
            "#reference \"Hello There\""]    
            )
    [<Test>]
    member public this.``Fsx.UnclosedHashReference.Case2``() =     
        Helper.ExhaustivelyScrutinize(
            this.TestRunner,
          ["#light"
           "#r \"" // Unclosed
           "# \"Hello There\""]                                            
           )
                                     
    /// There was a problem where an unclosed reference picked up the text of the reference on the next line.
    [<Test>]
    member public this.``Fsx.UnclosedHashLoad``() =     
        Helper.ExhaustivelyScrutinize(
            this.TestRunner, 
            [ "#light"
              "#load \"" // Unclosed
              "#load \"Hello There\""]
            ) 

    [<Test>]
    [<Category("TypeProvider")>]
    member public this.``TypeProvider.UnitsOfMeasure.SmokeTest1``() =
        let code =
                                    ["open Microsoft.FSharp.Data.UnitSystems.SI.UnitNames"
                                     "let x : System.Nullable<decimal<kilogram / hertz^2>> = N1.T1.MethodWithTypesInvolvingUnitsOfMeasure(1.0<kilogram>)"
                                     "let x2 : int = N1.T1().MethodWithErasedCodeUsingConditional()"
                                     "let x3 : int = N1.T1().MethodWithErasedCodeUsingTypeAs()"
                                     ]
        let refs = 
            [
                PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll")
            ]
        let (_, project, file) = this.CreateSingleFileProject(code, references = refs)
        TakeCoffeeBreak(this.VS)
        AssertNoErrorsOrWarnings(project)

    member public this.TypeProviderDisposalSmokeTest(clearing) =
        use _guard = this.UsingNewVS()
        let providerAssemblyName = PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll")
        let providerAssembly = System.Reflection.Assembly.LoadFrom providerAssemblyName
        Assert.IsNotNull(providerAssembly, "provider assembly should not be null")
        let providerCounters = providerAssembly.GetType("DummyProviderForLanguageServiceTesting.GlobalCounters")
        Assert.IsNotNull(providerCounters, "provider counters module should not be null")
        let totalCreationsMeth = providerCounters.GetMethod("GetTotalCreations")
        Assert.IsNotNull(totalCreationsMeth, "totalCreationsMeth should not be null")
        let totalDisposalsMeth = providerCounters.GetMethod("GetTotalDisposals")
        Assert.IsNotNull(totalDisposalsMeth, "totalDisposalsMeth should not be null")
        let checkConfigsMeth = providerCounters.GetMethod("CheckAllConfigsDisposed")
        Assert.IsNotNull(checkConfigsMeth, "checkConfigsMeth should not be null")

        let providerCounters2 = providerAssembly.GetType("ProviderImplementation.ProvidedTypes.GlobalCountersForInvalidation")
        Assert.IsNotNull(providerCounters2, "provider counters #2 module should not be null")
        let totalInvaldiationHandlersAddedMeth = providerCounters2.GetMethod("GetInvalidationHandlersAdded")
        Assert.IsNotNull(totalInvaldiationHandlersAddedMeth, "totalInvaldiationHandlersAddedMeth should not be null")
        let totalInvaldiationHandlersRemovedMeth = providerCounters2.GetMethod("GetInvalidationHandlersRemoved")
        Assert.IsNotNull(totalInvaldiationHandlersRemovedMeth, "totalInvaldiationHandlersRemovedMeth should not be null")

        let totalCreations() = totalCreationsMeth.Invoke(null, [| |]) :?> int
        let totalDisposals() = totalDisposalsMeth.Invoke(null, [| |]) :?> int
        let checkConfigsDisposed() = checkConfigsMeth.Invoke(null, [| |]) |> ignore
        let totalInvaldiationHandlersAdded() = totalInvaldiationHandlersAddedMeth.Invoke(null, [| |]) :?> int
        let totalInvaldiationHandlersRemoved() = totalInvaldiationHandlersRemovedMeth.Invoke(null, [| |]) :?> int

         
        let startCreations = totalCreations()
        let startDisposals = totalDisposals()
        let startInvaldiationHandlersAdded = totalInvaldiationHandlersAdded()
        let startInvaldiationHandlersRemoved =  totalInvaldiationHandlersRemoved()
        let countCreations() = totalCreations() - startCreations
        let countDisposals() = totalDisposals() - startDisposals
        let countInvaldiationHandlersAdded() = totalInvaldiationHandlersAdded() - startInvaldiationHandlersAdded
        let countInvaldiationHandlersRemoved() = totalInvaldiationHandlersRemoved() - startInvaldiationHandlersRemoved

        Assert.IsTrue(startCreations >= startDisposals, "Check0")
        Assert.IsTrue(startInvaldiationHandlersAdded >= startInvaldiationHandlersRemoved, "Check0")
        for i in 1 .. 50 do 
            let solution = this.CreateSolution()
            let project = CreateProject(solution,"testproject" + string (i % 20))    
            this.AddAssemblyReference(project, PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll"))
            let fileName = sprintf "File%d.fs" i
            let file1 = AddFileFromText(project,fileName, ["let x" + string i + " = N1.T1()" ])    
            let file = OpenFile(project,fileName)
            TakeCoffeeBreak(this.VS)
            AssertNoErrorsOrWarnings project   // ...and not an error on the first line.
            MoveCursorToEndOfMarker(file, "N1.T1")

            // do some stuff to get declarations etc.
            let tooltip = GetQuickInfoAtCursor file
            AssertContains(tooltip, "T1")
            ignore (GetF1KeywordAtCursor file)
            let parmInfo = GetParameterInfoAtCursor file

            let file1 = OpenFile(project,fileName)   

            // The disposals should be at least one less 
            let c = countCreations()
            let d = countDisposals()

            // Creations should always be greater or equal to disposals
            Assert.IsTrue(c >= d, "Check2, countCreations() >= countDisposals(), iteration " + string i + ", countCreations() = " + string c + ", countDisposals() = " + string d)

            // Creations can run ahead of iterations if the background checker resurrects the builder for a project
            // even after we've moved on from it.
            Assert.IsTrue((c >= i), "Check3, countCreations() >= i, iteration " + string i + ", countCreations() = " + string c)

            if not clearing then 
                // By default we hold 3 build incrementalBuilderCache entries and 5 typeCheckInfo entries, so if we're not clearing
                // there should be some roots to project builds still present
                if i >= 3 then 
                    Assert.IsTrue(c >= d + 3, "Check4a, c >= countDisposals() + 3, iteration " + string i + ", i = " + string i + ", countDisposals() = " + string d)
                    printfn "Check4a2, i = %d, countInvaldiationHandlersRemoved() = %d" i (countInvaldiationHandlersRemoved())

            // If we forcefully clear out caches and force a collection, then we can say much stronger things...
            if clearing then 
                ClearLanguageServiceRootCachesAndCollectAndFinalizeAllTransients(this.VS)
                let c = countCreations()
                let d = countDisposals()

                // Creations should be equal to disposals after a `ClearLanguageServiceRootCachesAndCollectAndFinalizeAllTransients`
                Assert.IsTrue((c = d), "Check4b, countCreations() = countDisposals(), iteration " + string i)
                Assert.IsTrue((countInvaldiationHandlersAdded() = countInvaldiationHandlersRemoved()), "Check4b2, all invlidation handlers removed, iteration " + string i)
        
        let c = countCreations()
        let d = countDisposals()
        Assert.IsTrue(c >= 50, "Check5, at end, countCreations() >= 50")

        ClearLanguageServiceRootCachesAndCollectAndFinalizeAllTransients(this.VS)

        let c = countCreations()
        let d = countDisposals()
        // Creations should be equal to disposals after a `ClearLanguageServiceRootCachesAndCollectAndFinalizeAllTransients`
        Assert.IsTrue((c = d), "Check6b, at end, countCreations() = countDisposals() after explicit clearing")
        Assert.IsTrue((countInvaldiationHandlersAdded() = countInvaldiationHandlersRemoved()), "Check6b2, at end, all invalidation handlers removed after explicit cleraring")
        checkConfigsDisposed()

    [<Test;Category("TypeProvider"); Category("Expensive"); Ignore("Flaky test, unclear if it is valuable")>]
    member public this.``TypeProvider.Disposal.SmokeTest1``() = this.TypeProviderDisposalSmokeTest(true)

    [<Test;Category("TypeProvider"); Ignore("Flaky test, unclear if it is valuable")>]
    member public this.``TypeProvider.Disposal.SmokeTest2``() = this.TypeProviderDisposalSmokeTest(false)


// Context project system
[<TestFixture>] 
type UsingProjectSystem() = 
    inherit UsingMSBuild(VsOpts = LanguageServiceExtension.ProjectSystemTestFlavour)

