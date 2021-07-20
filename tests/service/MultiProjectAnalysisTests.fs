
#if INTERACTIVE
#r "../../artifacts/bin/fcs/net461/FSharp.Compiler.Service.dll" // note, build FSharp.Compiler.Service.Tests.fsproj to generate this, this DLL has a public API so can be used from F# Interactive
#r "../../artifacts/bin/fcs/net461/nunit.framework.dll"
#load "FsUnit.fs"
#load "Common.fs"
#else
module Tests.Service.MultiProjectAnalysisTests
#endif

open NUnit.Framework
open FsUnit
open System.IO
open System.Collections.Generic
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.IO
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open FSharp.Compiler.Service.Tests.Common
open TestFramework

let toIList (x: _ array) = x :> IList<_>
let numProjectsForStressTest = 100
let internal checker = FSharpChecker.Create(projectCacheSize=numProjectsForStressTest + 10)

/// Extract range info
let internal tups (m:range) = (m.StartLine, m.StartColumn), (m.EndLine, m.EndColumn)


module internal Project1A =

    let fileName1 = Path.ChangeExtension(tryCreateTemporaryFileName (), ".fs")
    let baseName = tryCreateTemporaryFileName ()
    let dllName = Path.ChangeExtension(baseName, ".dll")
    let projFileName = Path.ChangeExtension(baseName, ".fsproj")
    let fileSource1 = """
module Project1A

/// This is type C
type C() =
    static member M(arg1: int, arg2: int, ?arg3 : int) = arg1 + arg2 + defaultArg arg3 4

/// This is x1
let x1 = C.M(arg1 = 3, arg2 = 4, arg3 = 5)

/// This is x2
let x2 = C.M(arg1 = 3, arg2 = 4, ?arg3 = Some 5)

/// This is
/// x3
let x3 (
          /// This is not x3
          p: int
      ) = ()

/// This is type U
type U =

   /// This is Case1
   | Case1 of int

   /// This is Case2
   | Case2 of string
    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)



//-----------------------------------------------------------------------------------------
module internal Project1B =

    let fileName1 = Path.ChangeExtension(tryCreateTemporaryFileName (), ".fs")
    let baseName = tryCreateTemporaryFileName ()
    let dllName = Path.ChangeExtension(baseName, ".dll")
    let projFileName = Path.ChangeExtension(baseName, ".fsproj")
    let fileSource1 = """
module Project1B

type A = B of xxx: int * yyy : int
let b = B(xxx=1, yyy=2)

let x =
    match b with
    // does not find usage here
    | B (xxx = a; yyy = b) -> ()
    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)


// A project referencing two sub-projects
module internal MultiProject1 =

    let fileName1 = Path.ChangeExtension(tryCreateTemporaryFileName (), ".fs")
    let baseName = tryCreateTemporaryFileName ()
    let dllName = Path.ChangeExtension(baseName, ".dll")
    let projFileName = Path.ChangeExtension(baseName, ".fsproj")
    let fileSource1 = """

module MultiProject1

open Project1A
open Project1B

let p = (Project1A.x1, Project1B.b)
let c = C()
let u = Case1 3
    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =
        let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)
        { options with
            OtherOptions = Array.append options.OtherOptions [| ("-r:" + Project1A.dllName); ("-r:" + Project1B.dllName) |]
            ReferencedProjects = [| FSharpReferencedProject.CreateFSharp(Project1A.dllName, Project1A.options);
                                    FSharpReferencedProject.CreateFSharp(Project1B.dllName, Project1B.options); |] }
    let cleanFileName a = if a = fileName1 then "file1" else "??"

[<Test>]
let ``Test multi project 1 basic`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(MultiProject1.options) |> Async.RunImmediate

    [ for x in wholeProjectResults.AssemblySignature.Entities -> x.DisplayName ] |> shouldEqual ["MultiProject1"]

    [ for x in wholeProjectResults.AssemblySignature.Entities.[0].NestedEntities -> x.DisplayName ] |> shouldEqual []


    [ for x in wholeProjectResults.AssemblySignature.Entities.[0].MembersFunctionsAndValues -> x.DisplayName ]
        |> shouldEqual ["p"; "c"; "u"]

[<Test>]
let ``Test multi project 1 all symbols`` () =

    let p1A = checker.ParseAndCheckProject(Project1A.options) |> Async.RunImmediate
    let p1B = checker.ParseAndCheckProject(Project1B.options) |> Async.RunImmediate
    let mp = checker.ParseAndCheckProject(MultiProject1.options) |> Async.RunImmediate

    let x1FromProject1A =
        [ for s in p1A.GetAllUsesOfAllSymbols() do
             if  s.Symbol.DisplayName = "x1" then
                 yield s.Symbol ]   |> List.head

    let x1FromProjectMultiProject =
        [ for s in mp.GetAllUsesOfAllSymbols() do
             if  s.Symbol.DisplayName = "x1" then
                 yield s.Symbol ]   |> List.head

    let bFromProjectMultiProject =
        [ for s in mp.GetAllUsesOfAllSymbols() do
             if  s.Symbol.DisplayName = "b" then
                 yield s.Symbol ]   |> List.head

    x1FromProject1A.Assembly.FileName.IsNone |> shouldEqual true // For now, the assembly being analyzed doesn't return a filename
    x1FromProject1A.Assembly.QualifiedName |> shouldEqual "" // For now, the assembly being analyzed doesn't return a qualified name
    x1FromProject1A.Assembly.SimpleName |> shouldEqual (Path.GetFileNameWithoutExtension Project1A.dllName)
    x1FromProjectMultiProject.Assembly.FileName |> shouldEqual (Some Project1A.dllName)
    bFromProjectMultiProject.Assembly.FileName |> shouldEqual  (Some Project1B.dllName)

    let usesOfx1FromProject1AInMultiProject1 =
       mp.GetUsesOfSymbol(x1FromProject1A)
            |> Array.map (fun s -> s.Symbol.DisplayName, MultiProject1.cleanFileName  s.FileName, tups s.Symbol.DeclarationLocation.Value)

    let usesOfx1FromMultiProject1InMultiProject1 =
       mp.GetUsesOfSymbol(x1FromProjectMultiProject)
            |> Array.map (fun s -> s.Symbol.DisplayName, MultiProject1.cleanFileName  s.FileName, tups s.Symbol.DeclarationLocation.Value)

    usesOfx1FromProject1AInMultiProject1 |> shouldEqual usesOfx1FromMultiProject1InMultiProject1

[<Test>]
let ``Test multi project 1 xmldoc`` () =

    let p1A = checker.ParseAndCheckProject(Project1A.options) |> Async.RunImmediate
    let p1B = checker.ParseAndCheckProject(Project1B.options) |> Async.RunImmediate
    let mp = checker.ParseAndCheckProject(MultiProject1.options) |> Async.RunImmediate

    let symbolFromProject1A sym =
        [ for s in p1A.GetAllUsesOfAllSymbols() do
             if  s.Symbol.DisplayName = sym then
                 yield s.Symbol ]   |> List.head

    let x1FromProject1A = symbolFromProject1A "x1"
    let x3FromProject1A = symbolFromProject1A "x3"

    let x1FromProjectMultiProject =
        [ for s in mp.GetAllUsesOfAllSymbols() do
             if  s.Symbol.DisplayName = "x1" then
                 yield s.Symbol ]   |> List.head

    let ctorFromProjectMultiProject =
        [ for s in mp.GetAllUsesOfAllSymbols() do
             if  s.Symbol.DisplayName = "C" then
                 yield s.Symbol ]   |> List.head

    let case1FromProjectMultiProject =
        [ for s in mp.GetAllUsesOfAllSymbols() do
             if  s.Symbol.DisplayName = "Case1" then
                 yield s.Symbol ]   |> List.head


    match x1FromProject1A with
    | :? FSharpMemberOrFunctionOrValue as v ->
        match v.XmlDoc with
        | FSharpXmlDoc.FromXmlText t -> t.UnprocessedLines.Length |> shouldEqual 1
        | _ -> failwith "wrong kind"
    | _ -> failwith "odd symbol!"

    match x3FromProject1A with
    | :? FSharpMemberOrFunctionOrValue as v ->
        match v.XmlDoc with
        | FSharpXmlDoc.FromXmlText t -> t.UnprocessedLines |> shouldEqual [|" This is"; " x3"|]
        | _ -> failwith "wrong kind"
    | _ -> failwith "odd symbol!"

    match x3FromProject1A with
    | :? FSharpMemberOrFunctionOrValue as v ->
        match v.XmlDoc with
        | FSharpXmlDoc.FromXmlText t -> t.GetElaboratedXmlLines() |> shouldEqual [|"<summary>"; " This is"; " x3"; "</summary>" |]
        | _ -> failwith "wrong kind"
    | _ -> failwith "odd symbol!"

    match x1FromProjectMultiProject with
    | :? FSharpMemberOrFunctionOrValue as v ->
        match v.XmlDoc with
        | FSharpXmlDoc.FromXmlText t -> t.UnprocessedLines.Length |> shouldEqual 1
        | _ -> failwith "wrong kind"
    | _ -> failwith "odd symbol!"

    match ctorFromProjectMultiProject with
    | :? FSharpMemberOrFunctionOrValue as c ->
        match c.XmlDoc with
        | FSharpXmlDoc.FromXmlText t -> t.UnprocessedLines.Length |> shouldEqual 0
        | _ -> failwith "wrong kind"
    | _ -> failwith "odd symbol!"

    match case1FromProjectMultiProject with
    | :? FSharpUnionCase as c ->
        match c.XmlDoc with
        | FSharpXmlDoc.FromXmlText t -> t.UnprocessedLines.Length |> shouldEqual 1
        | _ -> failwith "wrong kind"
    | _ -> failwith "odd symbol!"

//------------------------------------------------------------------------------------


// A project referencing many sub-projects
module internal ManyProjectsStressTest =

    let numProjectsForStressTest = 100

    type Project = { ModuleName: string; FileName: string; Options: FSharpProjectOptions; DllName: string }
    let projects =
        [ for i in 1 .. numProjectsForStressTest do
                let fileName1 = Path.ChangeExtension(tryCreateTemporaryFileName (), ".fs")
                let moduleName = "Project" + string i
                let fileSource1 = "module " + moduleName + """

// Some random code
open System

type C() =
    static member Print() = System.Console.WriteLine("Hello World")

let v = C()

let p = C.Print()

    """
                FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)
                let baseName = tryCreateTemporaryFileName ()
                let dllName = Path.ChangeExtension(baseName, ".dll")
                let projFileName = Path.ChangeExtension(baseName, ".fsproj")
                let fileNames = [fileName1 ]
                let args = mkProjectCommandLineArgs (dllName, fileNames)
                let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)
                yield { ModuleName = moduleName; FileName=fileName1; Options = options; DllName=dllName } ]

    let jointProject =
        let fileName = Path.ChangeExtension(tryCreateTemporaryFileName (), ".fs")
        let dllBase = tryCreateTemporaryFileName ()
        let dllName = Path.ChangeExtension(dllBase, ".dll")
        let projFileName = Path.ChangeExtension(dllBase, ".fsproj")
        let fileSource =
            """

module JointProject

"""          + String.concat "\r\n" [ for p in projects -> "open " + p.ModuleName ] +  """

let p = ("""
             + String.concat ",\r\n         " [ for p in projects -> p.ModuleName  + ".v" ] +  ")"
        FileSystem.OpenFileForWriteShim(fileName).Write(fileSource)

        let fileNames = [fileName]
        let args = mkProjectCommandLineArgs (dllName, fileNames)
        let options =
            let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)
            { options with
                OtherOptions = Array.append options.OtherOptions [| for p in  projects -> ("-r:" + p.DllName) |]
                ReferencedProjects = [| for p in projects -> FSharpReferencedProject.CreateFSharp(p.DllName, p.Options); |] }
        { ModuleName = "JointProject"; FileName=fileName; Options = options; DllName=dllName }

    let cleanFileName a =
        projects |> List.tryPick (fun m -> if a = m.FileName then Some m.ModuleName else None)
        |> function Some x -> x | None -> if a = jointProject.FileName then "fileN" else "??"


    let makeCheckerForStressTest ensureBigEnough =
        let size = (if ensureBigEnough then numProjectsForStressTest + 10 else numProjectsForStressTest / 2 )
        FSharpChecker.Create(projectCacheSize=size)

[<Test>]
let ``Test ManyProjectsStressTest basic`` () =

    let checker = ManyProjectsStressTest.makeCheckerForStressTest true

    let wholeProjectResults = checker.ParseAndCheckProject(ManyProjectsStressTest.jointProject.Options) |> Async.RunImmediate

    [ for x in wholeProjectResults.AssemblySignature.Entities -> x.DisplayName ] |> shouldEqual ["JointProject"]

    [ for x in wholeProjectResults.AssemblySignature.Entities.[0].NestedEntities -> x.DisplayName ] |> shouldEqual []

    [ for x in wholeProjectResults.AssemblySignature.Entities.[0].MembersFunctionsAndValues -> x.DisplayName ]
        |> shouldEqual ["p"]

[<Test>]
let ``Test ManyProjectsStressTest cache too small`` () =

    let checker = ManyProjectsStressTest.makeCheckerForStressTest false

    let wholeProjectResults = checker.ParseAndCheckProject(ManyProjectsStressTest.jointProject.Options) |> Async.RunImmediate

    [ for x in wholeProjectResults.AssemblySignature.Entities -> x.DisplayName ] |> shouldEqual ["JointProject"]

    [ for x in wholeProjectResults.AssemblySignature.Entities.[0].NestedEntities -> x.DisplayName ] |> shouldEqual []

    [ for x in wholeProjectResults.AssemblySignature.Entities.[0].MembersFunctionsAndValues -> x.DisplayName ]
        |> shouldEqual ["p"]

[<Test>]
let ``Test ManyProjectsStressTest all symbols`` () =

  let checker = ManyProjectsStressTest.makeCheckerForStressTest true
  for i in 1 .. 10 do
    printfn "stress test iteration %d (first may be slow, rest fast)" i
    let projectsResults = [ for p in ManyProjectsStressTest.projects -> p, checker.ParseAndCheckProject(p.Options) |> Async.RunImmediate ]
    let jointProjectResults = checker.ParseAndCheckProject(ManyProjectsStressTest.jointProject.Options) |> Async.RunImmediate

    let vsFromJointProject =
        [ for s in jointProjectResults.GetAllUsesOfAllSymbols() do
             if  s.Symbol.DisplayName = "v" then
                 yield s.Symbol ]

    for (p,pResults) in projectsResults do
        let vFromProject =
            [ for s in pResults.GetAllUsesOfAllSymbols() do
                if  s.Symbol.DisplayName = "v" then
                   yield s.Symbol ]   |> List.head
        vFromProject.Assembly.FileName.IsNone |> shouldEqual true // For now, the assembly being analyzed doesn't return a filename
        vFromProject.Assembly.QualifiedName |> shouldEqual "" // For now, the assembly being analyzed doesn't return a qualified name
        vFromProject.Assembly.SimpleName |> shouldEqual (Path.GetFileNameWithoutExtension p.DllName)

        let usesFromJointProject =
            jointProjectResults.GetUsesOfSymbol(vFromProject)
                |> Array.map (fun s -> s.Symbol.DisplayName, ManyProjectsStressTest.cleanFileName  s.FileName, tups s.Symbol.DeclarationLocation.Value)

        usesFromJointProject.Length |> shouldEqual 1

//-----------------------------------------------------------------------------------------

module internal MultiProjectDirty1 =

    let fileName1 = Path.ChangeExtension(tryCreateTemporaryFileName (), ".fs")
    let baseName = tryCreateTemporaryFileName()
    let dllName = Path.ChangeExtension(baseName, ".dll")
    let projFileName = Path.ChangeExtension(baseName, ".fsproj")
    let content = """module Project1

let x = "F#"
"""

    FileSystem.OpenFileForWriteShim(fileName1).Write(content)

    let cleanFileName a = if a = fileName1 then "Project1" else "??"

    let fileNames = [fileName1]

    let getOptions() =
        let args = mkProjectCommandLineArgs (dllName, fileNames)
        checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)

module internal MultiProjectDirty2 =


    let fileName1 = Path.ChangeExtension(tryCreateTemporaryFileName (), ".fs")
    let baseName = tryCreateTemporaryFileName ()
    let dllName = Path.ChangeExtension(baseName, ".dll")
    let projFileName = Path.ChangeExtension(baseName, ".fsproj")

    let content = """module Project2

open Project1

let y = x
let z = Project1.x
"""
    FileSystem.OpenFileForWriteShim(fileName1).Write(content)

    let cleanFileName a = if a = fileName1 then "Project2" else "??"

    let fileNames = [fileName1]

    let getOptions() =
        let args = mkProjectCommandLineArgs (dllName, fileNames)
        let options = checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)
        { options with
            OtherOptions = Array.append options.OtherOptions [| ("-r:" + MultiProjectDirty1.dllName) |]
            ReferencedProjects = [| FSharpReferencedProject.CreateFSharp(MultiProjectDirty1.dllName, MultiProjectDirty1.getOptions()) |] }

[<Test>]
let ``Test multi project symbols should pick up changes in dependent projects`` () =

    //  register to count the file checks
    let count = ref 0
    checker.FileChecked.Add (fun _ -> incr count)

    //---------------- Write the first version of the file in project 1 and check the project --------------------

    let proj1options = MultiProjectDirty1.getOptions()

    let wholeProjectResults1 = checker.ParseAndCheckProject(proj1options) |> Async.RunImmediate

    count.Value |> shouldEqual 1

    let backgroundParseResults1, backgroundTypedParse1 =
        checker.GetBackgroundCheckResultsForFileInProject(MultiProjectDirty1.fileName1, proj1options)
        |> Async.RunImmediate

    count.Value |> shouldEqual 1

    //---------------- Get a symbol from project 1 and look up its uses in both projects --------------------

    let xSymbolUse = backgroundTypedParse1.GetSymbolUseAtLocation(3, 4, "", ["x"])
    xSymbolUse.IsSome |> shouldEqual true
    let xSymbol = xSymbolUse.Value.Symbol

    printfn "Symbol found. Checking symbol uses in another project..."

    let proj2options = MultiProjectDirty2.getOptions()

    let wholeProjectResults2 = checker.ParseAndCheckProject(proj2options) |> Async.RunImmediate

    count.Value |> shouldEqual 2

    let _ = checker.ParseAndCheckProject(proj2options) |> Async.RunImmediate

    count.Value |> shouldEqual 2 // cached

    let usesOfXSymbolInProject1 =
        wholeProjectResults1.GetUsesOfSymbol(xSymbol)
        |> Array.map (fun su -> su.Symbol.ToString(), MultiProjectDirty1.cleanFileName su.FileName, tups su.Range)

    usesOfXSymbolInProject1
    |> shouldEqual
        [|("val x", "Project1", ((3, 4), (3, 5))) |]

    let usesOfXSymbolInProject2 =
        wholeProjectResults2.GetUsesOfSymbol(xSymbol)
        |> Array.map (fun su -> su.Symbol.ToString(), MultiProjectDirty2.cleanFileName su.FileName, tups su.Range)

    usesOfXSymbolInProject2
    |> shouldEqual
        [|("val x", "Project2", ((5, 8), (5, 9)));
          ("val x", "Project2", ((6, 8), (6, 18)))|]

    //---------------- Change the file by adding a line, then re-check everything --------------------

    let wt0 = System.DateTime.UtcNow
    let wt1 = FileSystem.GetLastWriteTimeShim MultiProjectDirty1.fileName1
    printfn "Writing new content to file '%s'" MultiProjectDirty1.fileName1

    System.Threading.Thread.Sleep(1000)
    FileSystem.OpenFileForWriteShim(MultiProjectDirty1.fileName1).Write(System.Environment.NewLine + MultiProjectDirty1.content)
    printfn "Wrote new content to file '%s'"  MultiProjectDirty1.fileName1
    let wt2 = FileSystem.GetLastWriteTimeShim MultiProjectDirty1.fileName1
    printfn "Current time: '%A', ticks = %d"  wt0 wt0.Ticks
    printfn "Old write time: '%A', ticks = %d"  wt1 wt1.Ticks
    printfn "New write time: '%A', ticks = %d"  wt2 wt2.Ticks

    let wholeProjectResults1AfterChange1 = checker.ParseAndCheckProject(proj1options) |> Async.RunImmediate
    count.Value |> shouldEqual 3

    let backgroundParseResults1AfterChange1, backgroundTypedParse1AfterChange1 =
        checker.GetBackgroundCheckResultsForFileInProject(MultiProjectDirty1.fileName1, proj1options)
        |> Async.RunImmediate

    let xSymbolUseAfterChange1 = backgroundTypedParse1AfterChange1.GetSymbolUseAtLocation(4, 4, "", ["x"])
    xSymbolUseAfterChange1.IsSome |> shouldEqual true
    let xSymbolAfterChange1 = xSymbolUseAfterChange1.Value.Symbol


    printfn "Checking project 2 after first change, options = '%A'" proj2options

    let wholeProjectResults2AfterChange1 = checker.ParseAndCheckProject(proj2options) |> Async.RunImmediate

    count.Value |> shouldEqual 4

    let usesOfXSymbolInProject1AfterChange1 =
        wholeProjectResults1AfterChange1.GetUsesOfSymbol(xSymbolAfterChange1)
        |> Array.map (fun su -> su.Symbol.ToString(), MultiProjectDirty1.cleanFileName su.FileName, tups su.Range)

    usesOfXSymbolInProject1AfterChange1
    |> shouldEqual
        [|("val x", "Project1", ((4, 4), (4, 5))) |]

    let usesOfXSymbolInProject2AfterChange1 =
        wholeProjectResults2AfterChange1.GetUsesOfSymbol(xSymbolAfterChange1)
        |> Array.map (fun su -> su.Symbol.ToString(), MultiProjectDirty2.cleanFileName su.FileName, tups su.Range)

    usesOfXSymbolInProject2AfterChange1
    |> shouldEqual
        [|("val x", "Project2", ((5, 8), (5, 9)));
          ("val x", "Project2", ((6, 8), (6, 18)))|]

    //---------------- Revert the change to the file --------------------

    let wt0b = System.DateTime.UtcNow
    let wt1b = FileSystem.GetLastWriteTimeShim MultiProjectDirty1.fileName1
    printfn "Writing old content to file '%s'" MultiProjectDirty1.fileName1
    System.Threading.Thread.Sleep(1000)
    FileSystem.OpenFileForWriteShim(MultiProjectDirty1.fileName1).Write(MultiProjectDirty1.content)
    printfn "Wrote old content to file '%s'"  MultiProjectDirty1.fileName1
    let wt2b = FileSystem.GetLastWriteTimeShim MultiProjectDirty1.fileName1
    printfn "Current time: '%A', ticks = %d"  wt0b wt0b.Ticks
    printfn "Old write time: '%A', ticks = %d"  wt1b wt1b.Ticks
    printfn "New write time: '%A', ticks = %d"  wt2b wt2b.Ticks

    count.Value |> shouldEqual 4
    let wholeProjectResults2AfterChange2 = checker.ParseAndCheckProject(proj2options) |> Async.RunImmediate

    System.Threading.Thread.Sleep(1000)
    count.Value |> shouldEqual 6 // note, causes two files to be type checked, one from each project


    let wholeProjectResults1AfterChange2 = checker.ParseAndCheckProject(proj1options) |> Async.RunImmediate

    count.Value |> shouldEqual 6 // the project is already checked

    let backgroundParseResults1AfterChange2, backgroundTypedParse1AfterChange2 =
        checker.GetBackgroundCheckResultsForFileInProject(MultiProjectDirty1.fileName1, proj1options)
        |> Async.RunImmediate

    let xSymbolUseAfterChange2 = backgroundTypedParse1AfterChange2.GetSymbolUseAtLocation(4, 4, "", ["x"])
    xSymbolUseAfterChange2.IsSome |> shouldEqual true
    let xSymbolAfterChange2 = xSymbolUseAfterChange2.Value.Symbol


    let usesOfXSymbolInProject1AfterChange2 =
        wholeProjectResults1AfterChange2.GetUsesOfSymbol(xSymbolAfterChange2)
        |> Array.map (fun su -> su.Symbol.ToString(), MultiProjectDirty1.cleanFileName su.FileName, tups su.Range)

    usesOfXSymbolInProject1AfterChange2
    |> shouldEqual
        [|("val x", "Project1", ((3, 4), (3, 5))) |]


    let usesOfXSymbolInProject2AfterChange2 =
        wholeProjectResults2AfterChange2.GetUsesOfSymbol(xSymbolAfterChange2)
        |> Array.map (fun su -> su.Symbol.ToString(), MultiProjectDirty2.cleanFileName su.FileName, tups su.Range)

    usesOfXSymbolInProject2AfterChange2
    |> shouldEqual
        [|("val x", "Project2", ((5, 8), (5, 9)));
          ("val x", "Project2", ((6, 8), (6, 18)))|]


//------------------------------------------------------------------


module internal Project2A =

    let fileName1 = Path.ChangeExtension(tryCreateTemporaryFileName(), ".fs")
    let baseName1 = tryCreateTemporaryFileName ()
    let baseName2 = tryCreateTemporaryFileName ()
    let baseName3 = tryCreateTemporaryFileName () // this one doesn't get InternalsVisibleTo rights
    let dllShortName = Path.GetFileNameWithoutExtension(baseName2)
    let dllName = Path.ChangeExtension(baseName1, ".dll")
    let projFileName = Path.ChangeExtension(baseName1, ".fsproj")
    let fileSource1 = """
module Project2A

[<assembly:System.Runtime.CompilerServices.InternalsVisibleTo(""" + "\"" + dllShortName + "\"" + """)>]
do()

type C() =
    member internal x.InternalMember = 1

    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)

//Project2A.fileSource1
// A project referencing Project2A
module internal Project2B =

    let fileName1 = Path.ChangeExtension(tryCreateTemporaryFileName (), ".fs")
    let dllName = Path.ChangeExtension(Project2A.baseName2, ".dll")
    let projFileName = Path.ChangeExtension(Project2A.baseName2, ".fsproj")
    let fileSource1 = """

module Project2B

let v = Project2A.C().InternalMember // access an internal symbol
    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =
        let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)
        { options with
            OtherOptions = Array.append options.OtherOptions [| ("-r:" + Project2A.dllName);  |]
            ReferencedProjects = [| FSharpReferencedProject.CreateFSharp(Project2A.dllName, Project2A.options); |] }
    let cleanFileName a = if a = fileName1 then "file1" else "??"

//Project2A.fileSource1
// A project referencing Project2A but without access to the internals of A
module internal Project2C =

    let fileName1 = Path.ChangeExtension(tryCreateTemporaryFileName (), ".fs")
    let dllName = Path.ChangeExtension(Project2A.baseName3, ".dll")
    let projFileName = Path.ChangeExtension(Project2A.baseName3, ".fsproj")
    let fileSource1 = """

module Project2C

let v = Project2A.C().InternalMember // access an internal symbol
    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =
        let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)
        { options with
            OtherOptions = Array.append options.OtherOptions [| ("-r:" + Project2A.dllName);  |]
            ReferencedProjects = [| FSharpReferencedProject.CreateFSharp(Project2A.dllName, Project2A.options); |] }
    let cleanFileName a = if a = fileName1 then "file1" else "??"

[<Test>]
let ``Test multi project2 errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project2B.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "multi project2 error: <<<%s>>>" e.Message

    wholeProjectResults .Diagnostics.Length |> shouldEqual 0


    let wholeProjectResultsC = checker.ParseAndCheckProject(Project2C.options) |> Async.RunImmediate
    wholeProjectResultsC.Diagnostics.Length |> shouldEqual 1



[<Test>]
let ``Test multi project 2 all symbols`` () =

    let mpA = checker.ParseAndCheckProject(Project2A.options) |> Async.RunImmediate
    let mpB = checker.ParseAndCheckProject(Project2B.options) |> Async.RunImmediate
    let mpC = checker.ParseAndCheckProject(Project2C.options) |> Async.RunImmediate

    // These all get the symbol in A, but from three different project compilations/checks
    let symFromA =
        [ for s in mpA.GetAllUsesOfAllSymbols() do
             if  s.Symbol.DisplayName = "InternalMember" then
                 yield s.Symbol ]   |> List.head

    let symFromB =
        [ for s in mpB.GetAllUsesOfAllSymbols() do
             if  s.Symbol.DisplayName = "InternalMember" then
                 yield s.Symbol ]   |> List.head

    symFromA.IsAccessible(mpA.ProjectContext.AccessibilityRights) |> shouldEqual true
    symFromA.IsAccessible(mpB.ProjectContext.AccessibilityRights) |> shouldEqual true
    symFromA.IsAccessible(mpC.ProjectContext.AccessibilityRights) |> shouldEqual false
    symFromB.IsAccessible(mpA.ProjectContext.AccessibilityRights) |> shouldEqual true
    symFromB.IsAccessible(mpB.ProjectContext.AccessibilityRights) |> shouldEqual true
    symFromB.IsAccessible(mpC.ProjectContext.AccessibilityRights) |> shouldEqual false

//------------------------------------------------------------------------------------

module internal Project3A =

    let fileName1 = Path.ChangeExtension(tryCreateTemporaryFileName (), ".fs")
    let baseName = tryCreateTemporaryFileName ()
    let dllName = Path.ChangeExtension(baseName, ".dll")
    let projFileName = Path.ChangeExtension(baseName, ".fsproj")
    let fileSource1 = """
module Project3A

///A parameterized active pattern of divisibility
let (|DivisibleBy|_|) by n =
    if n % by = 0 then Some DivisibleBy else None
    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)


// A project referencing a sub-project
module internal MultiProject3 =

    let fileName1 = Path.ChangeExtension(tryCreateTemporaryFileName (), ".fs")
    let baseName = tryCreateTemporaryFileName ()
    let dllName = Path.ChangeExtension(baseName, ".dll")
    let projFileName = Path.ChangeExtension(baseName, ".fsproj")
    let fileSource1 = """
module MultiProject3

open Project3A

let fizzBuzz = function
    | DivisibleBy 3 & DivisibleBy 5 -> "FizzBuzz"
    | DivisibleBy 3 -> "Fizz"
    | DivisibleBy 5 -> "Buzz"
    | _ -> ""
    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =
        let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)
        { options with
            OtherOptions = Array.append options.OtherOptions [| ("-r:" + Project3A.dllName) |]
            ReferencedProjects = [| FSharpReferencedProject.CreateFSharp(Project3A.dllName, Project3A.options) |] }
    let cleanFileName a = if a = fileName1 then "file1" else "??"

[<Test>]
let ``Test multi project 3 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(MultiProject3.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "multi project 3 error: <<<%s>>>" e.Message

    wholeProjectResults.Diagnostics.Length |> shouldEqual 0

[<Test>]
let ``Test active patterns' XmlDocSig declared in referenced projects`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(MultiProject3.options) |> Async.RunImmediate
    let backgroundParseResults1, backgroundTypedParse1 =
        checker.GetBackgroundCheckResultsForFileInProject(MultiProject3.fileName1, MultiProject3.options)
        |> Async.RunImmediate

    let divisibleBySymbolUse = backgroundTypedParse1.GetSymbolUseAtLocation(7,7,"",["DivisibleBy"])
    divisibleBySymbolUse.IsSome |> shouldEqual true
    let divisibleBySymbol = divisibleBySymbolUse.Value.Symbol
    divisibleBySymbol.ToString() |> shouldEqual "symbol DivisibleBy"

    let divisibleByActivePatternCase = divisibleBySymbol :?> FSharpActivePatternCase
    match divisibleByActivePatternCase.XmlDoc with
    | FSharpXmlDoc.FromXmlText t ->
        t.UnprocessedLines |> shouldEqual [| "A parameterized active pattern of divisibility" |]
        t.GetElaboratedXmlLines() |> shouldEqual [| "<summary>"; "A parameterized active pattern of divisibility"; "</summary>" |]
    | _ -> failwith "wrong kind"
    divisibleByActivePatternCase.XmlDocSig |> shouldEqual "M:Project3A.|DivisibleBy|_|(System.Int32,System.Int32)"
    let divisibleByGroup = divisibleByActivePatternCase.Group
    divisibleByGroup.IsTotal |> shouldEqual false
    divisibleByGroup.Names |> Seq.toList |> shouldEqual ["DivisibleBy"]
    divisibleByGroup.OverallType.Format(divisibleBySymbolUse.Value.DisplayContext) |> shouldEqual "int -> int -> unit option"
    let divisibleByEntity = divisibleByGroup.DeclaringEntity.Value
    divisibleByEntity.ToString() |> shouldEqual "Project3A"

//------------------------------------------------------------------------------------



[<Test>]
let ``Test max memory gets triggered`` () =
    let checker = FSharpChecker.Create()
    let reached = ref false
    checker.MaxMemoryReached.Add (fun () -> reached := true)
    let wholeProjectResults = checker.ParseAndCheckProject(MultiProject3.options) |> Async.RunImmediate
    reached.Value |> shouldEqual false
    checker.MaxMemory <- 0
    let wholeProjectResults2 = checker.ParseAndCheckProject(MultiProject3.options) |> Async.RunImmediate
    reached.Value |> shouldEqual true
    let wholeProjectResults3 = checker.ParseAndCheckProject(MultiProject3.options) |> Async.RunImmediate
    reached.Value |> shouldEqual true


//------------------------------------------------------------------------------------


[<Test>]
let ``In-memory cross-project references to projects using generative type provides should fallback to on-disk references`` () =
    // The type provider and its dependency are compiled as part of the solution build
#if DEBUG
    let csDLL = __SOURCE_DIRECTORY__ + @"/../../artifacts/bin/TestTP/Debug/netstandard2.0/CSharp_Analysis.dll"
    let tpDLL = __SOURCE_DIRECTORY__ + @"/../../artifacts/bin/TestTP/Debug/netstandard2.0/TestTP.dll"
#else
    let csDLL = __SOURCE_DIRECTORY__ + @"/../../artifacts/bin/TestTP/Release/netstandard2.0/CSharp_Analysis.dll"
    let tpDLL = __SOURCE_DIRECTORY__ + @"/../../artifacts/bin/TestTP/Release/netstandard2.0/TestTP.dll"
#endif
// These two projects should have been built before the test executes
    if not (File.Exists csDLL) then 
        failwith $"expect {csDLL} to exist"
    if not (File.Exists tpDLL) then 
        failwith $"expect {tpDLL} to exist"
    let optionsTestProject = 
        {       ProjectFileName = __SOURCE_DIRECTORY__ + @"/data/TestProject/TestProject.fsproj"
                ProjectId = None
                SourceFiles = 
                    [|  __SOURCE_DIRECTORY__ + @"/data/TestProject/TestProject.fs" |]
                Stamp = None
                OtherOptions =
                 [|yield "--simpleresolution"
                   yield "--noframework"
                   yield "--out:" + __SOURCE_DIRECTORY__ + @"/../../artifacts/bin/TestProject/Debug/netstandard2.0/TestProject.dll"
                   yield "--doc:" + __SOURCE_DIRECTORY__ + @"/data/TestProject/bin/Debug/TestProject.xml"
                   yield "--subsystemversion:6.00"
                   yield "--highentropyva+"
                   yield "--fullpaths"
                   yield "--flaterrors"
                   yield "--target:library"
                   yield "--define:DEBUG"
                   yield "--define:TRACE"
                   yield "--debug+"
                   yield "--optimize-"
                   yield "--tailcalls-"
                   yield "--debug:full"
                   yield "--platform:anycpu"
                   for r in mkStandardProjectReferences () do
                       yield "-r:" + r
                   // Make use of the type provider and reference its dependency
                   yield "-r:" + csDLL
                   yield "-r:" + tpDLL
                  |]
                ReferencedProjects = [||]
                IsIncompleteTypeCheckEnvironment = false
                UseScriptResolutionRules = false
                LoadTime = System.DateTime.Now
                UnresolvedReferences = None
                OriginalLoadReferences = [] }

    let optionsTestProject2 testProjectOutput =
          {ProjectFileName = __SOURCE_DIRECTORY__ + @"/data/TestProject2/TestProject2.fsproj"
           ProjectId = None
           SourceFiles = [|__SOURCE_DIRECTORY__ + @"/data/TestProject2/TestProject2.fs"|]
           Stamp = None
           OtherOptions =
            [|yield "--simpleresolution"
              yield "--noframework"
              yield "--out:" + __SOURCE_DIRECTORY__ + @"/data/TestProject2/bin/Debug/TestProject2.dll"
              yield "--doc:" + __SOURCE_DIRECTORY__ + @"/data/TestProject2/bin/Debug/TestProject2.xml"
              yield "--subsystemversion:6.00"
              yield "--highentropyva+"
              yield "--fullpaths"
              yield "--flaterrors"
              yield "--target:library"
              yield "--define:DEBUG"
              yield "--define:TRACE"
              yield "--debug+"
              yield "--optimize-"
              yield "--tailcalls-"
              yield "--debug:full"
              yield "--platform:anycpu"
              for r in mkStandardProjectReferences () do
                  yield "-r:" + r
              // Make use of the type provider and reference its dependency
              yield "-r:" + csDLL
              yield "-r:" + tpDLL
             // Make an in-memory reference to TestProject, which itself makes use of a type provider
             // NOTE TestProject may not actually have been compiled
              yield "-r:" + testProjectOutput|]
           ReferencedProjects =
            [|FSharpReferencedProject.CreateFSharp(testProjectOutput, optionsTestProject )|]
           IsIncompleteTypeCheckEnvironment = false
           UseScriptResolutionRules = false
           LoadTime = System.DateTime.Now
           UnresolvedReferences = None
           OriginalLoadReferences = []}

    //printfn "options: %A" options
    begin
        let fileName = __SOURCE_DIRECTORY__ + @"/data/TestProject/TestProject.fs"
        let fileSource = FileSystem.OpenFileForReadShim(fileName).ReadAllText()
        let fileParseResults, fileCheckAnswer = checker.ParseAndCheckFileInProject(fileName, 0, SourceText.ofString fileSource, optionsTestProject) |> Async.RunImmediate
        let fileCheckResults =
            match fileCheckAnswer with
            | FSharpCheckFileAnswer.Succeeded(res) -> res
            | res -> failwithf "Parsing did not finish... (%A)" res

        printfn "Parse Diagnostics (TestProject): %A" fileParseResults.Diagnostics
        printfn "Check Diagnostics (TestProject): %A" fileCheckResults.Diagnostics
        fileCheckResults.Diagnostics |> Array.exists (fun error -> error.Severity = FSharpDiagnosticSeverity.Error) |> shouldEqual false
    end

    // Set up a TestProject2 using an in-memory reference to TestProject but where TestProject has not
    // compiled to be on-disk.  In this case, we expect an error, because TestProject uses a generative
    // type provider, and in-memory cross-references to projects using generative type providers are
    // not yet supported.
    begin
        let testProjectNotCompiledSimulatedOutput = __SOURCE_DIRECTORY__ + @"/DUMMY/TestProject.dll"
        let options = optionsTestProject2 testProjectNotCompiledSimulatedOutput
        let fileName = __SOURCE_DIRECTORY__ + @"/data/TestProject2/TestProject2.fs"
        let fileSource = FileSystem.OpenFileForReadShim(fileName).ReadAllText()
        let fileParseResults, fileCheckAnswer = checker.ParseAndCheckFileInProject(fileName, 0, SourceText.ofString fileSource, options) |> Async.RunImmediate
        let fileCheckResults =
            match fileCheckAnswer with
            | FSharpCheckFileAnswer.Succeeded(res) -> res
            | res -> failwithf "Parsing did not finish... (%A)" res

        printfn "Parse Diagnostics (TestProject2 without compiled TestProject): %A" fileParseResults.Diagnostics
        printfn "Check Diagnostics (TestProject2 without compiled TestProject): %A" fileCheckResults.Diagnostics
        fileCheckResults.Diagnostics 
            |> Array.exists (fun diag -> 
                 diag.Severity = FSharpDiagnosticSeverity.Error &&
                 diag.Message.Contains("TestProject.dll"))
            |> shouldEqual true
    end

    // Do the same check with an in-memory reference to TestProject where TestProject exists 
    // compiled to disk.  In this case, we expect no error, because even though TestProject uses a generative
    // type provider, the in-memory cross-reference is ignored and an on-disk reference is used instead.
    begin
        let testProjectCompiledOutput = __SOURCE_DIRECTORY__ + @"/data/TestProject/netstandard2.0/TestProject.dll"
        if not (File.Exists testProjectCompiledOutput) then 
            failwith $"expect {testProjectCompiledOutput} to exist"
        let options = optionsTestProject2 testProjectCompiledOutput
        let fileName = __SOURCE_DIRECTORY__ + @"/data/TestProject2/TestProject2.fs"
        let fileSource = FileSystem.OpenFileForReadShim(fileName).ReadAllText()
        let fileParseResults, fileCheckAnswer = checker.ParseAndCheckFileInProject(fileName, 0, SourceText.ofString fileSource, options) |> Async.RunImmediate
        let fileCheckResults =
            match fileCheckAnswer with
            | FSharpCheckFileAnswer.Succeeded(res) -> res
            | res -> failwithf "Parsing did not finish... (%A)" res

        printfn "Parse Diagnostics (TestProject2 with compiled TestProject): %A" fileParseResults.Diagnostics
        printfn "Check Diagnostics (TestProject2 with compiled TestProject): %A" fileCheckResults.Diagnostics
        fileCheckResults.Diagnostics.Length |> shouldEqual 0
    end

