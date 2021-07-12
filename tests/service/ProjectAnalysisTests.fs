#if INTERACTIVE
#r "../../artifacts/bin/fcs/net461/FSharp.Compiler.Service.dll" // note, build FSharp.Compiler.Service.Tests.fsproj to generate this, this DLL has a public API so can be used from F# Interactive
#r "../../artifacts/bin/fcs/net461/nunit.framework.dll"
#load "FsUnit.fs"
#load "Common.fs"
#else
module Tests.Service.ProjectAnalysisTests
#endif

let runningOnMono = try System.Type.GetType("Mono.Runtime") <> null with e ->  false

open NUnit.Framework
open FsUnit
open System
open System.IO
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.EditorServices
open FSharp.Compiler.IO
open FSharp.Compiler.Text
open FSharp.Compiler.Service.Tests.Common
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns

module internal Project1 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let fileName2 = Path.ChangeExtension(base2, ".fs")
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1Text = """
module M

type C() =
    member x.P = 1

let xxx = 3 + 4
let fff () = xxx + xxx

type CAbbrev = C
    """
    let fileSource1 = SourceText.ofString fileSource1Text
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1Text)

    let fileSource2Text = """
module N

open M

type D1() =
    member x.SomeProperty = M.xxx

type D2() =
    member x.SomeProperty = M.fff() + D1().P

// Generate a warning
let y2 = match 1 with 1 -> M.xxx

// A class with some 'let' bindings
type D3(a:int) =
    let b = a + 4

    [<DefaultValue(false)>]
    val mutable x : int

    member x.SomeProperty = a + b

let pair1,pair2 = (3 + 4 + int32 System.DateTime.Now.Ticks, 5 + 6)

// Check enum values
type SaveOptions =
  | None = 0
  | DisableFormatting = 1

let enumValue = SaveOptions.DisableFormatting

let (++) x y = x + y

let c1 = 1 ++ 2

let c2 = 1 ++ 2

let mmmm1 : M.C = new M.C()             // note, these don't count as uses of CAbbrev
let mmmm2 : M.CAbbrev = new M.CAbbrev() // note, these don't count as uses of C

    """
    let fileSource2 = SourceText.ofString fileSource2Text
    FileSystem.OpenFileForWriteShim(fileName2).Write(fileSource2Text)

    let fileNames = [fileName1; fileName2]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options = checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)
    let parsingOptions, _ = checker.GetParsingOptionsFromCommandLineArgs(List.ofArray args)
    let cleanFileName a = if a = fileName1 then "file1" else if a = fileName2 then "file2" else "??"

[<Test>]
let ``Test project1 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project1.options) |> Async.RunImmediate
    wholeProjectResults .Diagnostics.Length |> shouldEqual 2
    wholeProjectResults.Diagnostics.[1].Message.Contains("Incomplete pattern matches on this expression") |> shouldEqual true // yes it does
    wholeProjectResults.Diagnostics.[1].ErrorNumber |> shouldEqual 25

    wholeProjectResults.Diagnostics.[0].Range.StartLine |> shouldEqual 10
    wholeProjectResults.Diagnostics.[0].Range.EndLine |> shouldEqual 10
    wholeProjectResults.Diagnostics.[0].Range.StartColumn |> shouldEqual 43
    wholeProjectResults.Diagnostics.[0].Range.EndColumn |> shouldEqual 44

[<Test;NonParallelizable>]
let ``Test project1 and make sure TcImports gets cleaned up`` () =

    let test () =
        let (_, checkFileAnswer) = checker.ParseAndCheckFileInProject(Project1.fileName1, 0, Project1.fileSource1, Project1.options) |> Async.RunImmediate
        match checkFileAnswer with
        | FSharpCheckFileAnswer.Aborted -> failwith "should not be aborted"
        | FSharpCheckFileAnswer.Succeeded checkFileResults ->
            let tcImportsOpt = checkFileResults.TryGetCurrentTcImports ()
            Assert.True tcImportsOpt.IsSome
            let weakTcImports = WeakReference tcImportsOpt.Value
            Assert.True weakTcImports.IsAlive
            weakTcImports

    // Here we are only keeping a handle to weakTcImports and nothing else
    let weakTcImports = test ()
    checker.InvalidateConfiguration (Project1.options)
    checker.ClearLanguageServiceRootCachesAndCollectAndFinalizeAllTransients()
    GC.Collect(2, GCCollectionMode.Forced, blocking = true)
    Assert.False weakTcImports.IsAlive

[<Test>]
let ``Test Project1 should have protected FullName and TryFullName return same results`` () =
    let wholeProjectResults = checker.ParseAndCheckProject(Project1.options) |> Async.RunImmediate
    let rec getFullNameComparisons (entity: FSharpEntity) =
        #if !NO_EXTENSIONTYPING
        seq { if not entity.IsProvided && entity.Accessibility.IsPublic then
        #else
        seq { if entity.Accessibility.IsPublic then
        #endif
                yield (entity.TryFullName = try Some entity.FullName with _ -> None)
                for e in entity.NestedEntities do
                    yield! getFullNameComparisons e }

    wholeProjectResults.ProjectContext.GetReferencedAssemblies()
    |> List.map (fun asm -> asm.Contents.Entities)
    |> Seq.collect (Seq.collect getFullNameComparisons)
    |> Seq.iter (shouldEqual true)

[<Test>]
[<Ignore("SKIPPED: BaseType shouldn't throw exceptions")>]
let ``Test project1 should not throw exceptions on entities from referenced assemblies`` () =
    let wholeProjectResults = checker.ParseAndCheckProject(Project1.options) |> Async.RunImmediate
    let rec getAllBaseTypes (entity: FSharpEntity) =
        seq { if not entity.IsProvided && entity.Accessibility.IsPublic then
                if not entity.IsUnresolved then yield entity.BaseType
                for e in entity.NestedEntities do
                    yield! getAllBaseTypes e }
    let allBaseTypes =
        wholeProjectResults.ProjectContext.GetReferencedAssemblies()
        |> List.map (fun asm -> asm.Contents.Entities)
        |> Seq.collect (Seq.map getAllBaseTypes)
        |> Seq.concat
    Assert.DoesNotThrow(fun () -> Seq.iter (fun _ -> ()) allBaseTypes)

[<Test>]
let ``Test project1 basic`` () =


    let wholeProjectResults = checker.ParseAndCheckProject(Project1.options) |> Async.RunImmediate

    set [ for x in wholeProjectResults.AssemblySignature.Entities -> x.DisplayName ] |> shouldEqual (set ["N"; "M"])

    [ for x in wholeProjectResults.AssemblySignature.Entities.[0].NestedEntities -> x.DisplayName ] |> shouldEqual ["D1"; "D2"; "D3"; "SaveOptions" ]

    [ for x in wholeProjectResults.AssemblySignature.Entities.[1].NestedEntities -> x.DisplayName ] |> shouldEqual ["C"; "CAbbrev"]

    set [ for x in wholeProjectResults.AssemblySignature.Entities.[0].MembersFunctionsAndValues -> x.DisplayName ]
        |> shouldEqual (set ["y2"; "pair2"; "pair1"; "( ++ )"; "c1"; "c2"; "mmmm1"; "mmmm2"; "enumValue" ])

[<Test>]
let ``Test project1 all symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project1.options) |> Async.RunImmediate
    let allSymbols = allSymbolsInEntities true wholeProjectResults.AssemblySignature.Entities
    for s in allSymbols do
        s.DeclarationLocation.IsSome |> shouldEqual true

    for s in allSymbols do
        match s with
        | :? FSharpMemberOrFunctionOrValue as v when v.IsModuleValueOrMember ->
           s.IsAccessible(wholeProjectResults.ProjectContext.AccessibilityRights) |> shouldEqual true
        | :? FSharpEntity ->
           s.IsAccessible(wholeProjectResults.ProjectContext.AccessibilityRights) |> shouldEqual true
        | _ -> ()

    let allDeclarationLocations =
        [ for s in allSymbols do
             let m = s.DeclarationLocation.Value
             yield s.ToString(), Project1.cleanFileName  m.FileName, (m.StartLine, m.StartColumn), (m.EndLine, m.EndColumn ), attribsOfSymbol s
            ]

    allDeclarationLocations |> shouldEqual
          [("N", "file2", (2, 7), (2, 8), ["module"]);
           ("val y2", "file2", (13, 4), (13, 6), ["val"]);
           ("val pair2", "file2", (24, 10), (24, 15), ["val"]);
           ("val pair1", "file2", (24, 4), (24, 9), ["val"]);
           ("val enumValue", "file2", (31, 4), (31, 13), ["val"]);
           ("val op_PlusPlus", "file2", (33, 5), (33, 7), ["val"]);
           ("val c1", "file2", (35, 4), (35, 6), ["val"]);
           ("val c2", "file2", (37, 4), (37, 6), ["val"]);
           ("val mmmm1", "file2", (39, 4), (39, 9), ["val"]);
           ("val mmmm2", "file2", (40, 4), (40, 9), ["val"]);
           ("D1", "file2", (6, 5), (6, 7), ["class"]);
           ("member .ctor", "file2", (6, 5), (6, 7), ["member"; "ctor"]);
           ("member get_SomeProperty", "file2", (7, 13), (7, 25), ["member"; "getter"]);
           ("property SomeProperty", "file2", (7, 13), (7, 25), ["member"; "prop"]);
           ("D2", "file2", (9, 5), (9, 7), ["class"]);
           ("member .ctor", "file2", (9, 5), (9, 7), ["member"; "ctor"]);
           ("member get_SomeProperty", "file2", (10, 13), (10, 25),
            ["member"; "getter"]);
           ("property SomeProperty", "file2", (10, 13), (10, 25), ["member"; "prop"]);
           ("D3", "file2", (16, 5), (16, 7), ["class"]);
           ("member .ctor", "file2", (16, 5), (16, 7), ["member"; "ctor"]);
           ("member get_SomeProperty", "file2", (22, 13), (22, 25),
            ["member"; "getter"]);
           ("property SomeProperty", "file2", (22, 13), (22, 25), ["member"; "prop"]);
           ("field a", "file2", (16, 8), (16, 9), ["field"; "compgen"]);
           ("field b", "file2", (17, 8), (17, 9), ["field"; "compgen"]);
           ("field x", "file2", (20, 16), (20, 17), ["field"; "default"; "mutable"]);
           ("SaveOptions", "file2", (27, 5), (27, 16), ["enum"; "valuetype"]);
           ("field value__", "file2", (28, 2), (29, 25), ["field"; "compgen"]);
           ("field None", "file2", (28, 4), (28, 8), ["field"; "static"; "0"]);
           ("field DisableFormatting", "file2", (29, 4), (29, 21), ["field"; "static"; "1"]);
           ("M", "file1", (2, 7), (2, 8), ["module"]);
           ("val xxx", "file1", (7, 4), (7, 7), ["val"]);
           ("val fff", "file1", (8, 4), (8, 7), ["val"]);
           ("C", "file1", (4, 5), (4, 6), ["class"]);
           ("member .ctor", "file1", (4, 5), (4, 6), ["member"; "ctor"]);
           ("member get_P", "file1", (5, 13), (5, 14), ["member"; "getter"]);
           ("property P", "file1", (5, 13), (5, 14), ["member"; "prop"]);
           ("CAbbrev", "file1", (10, 5), (10, 12), ["abbrev"]);
           ("property P", "file1", (5, 13), (5, 14), ["member"; "prop"])]

    for s in allSymbols do
        s.ImplementationLocation.IsSome |> shouldEqual true

    let allImplementationLocations =
        [ for s in allSymbols do
             let m = s.ImplementationLocation.Value
             yield s.ToString(), Project1.cleanFileName  m.FileName, (m.StartLine, m.StartColumn), (m.EndLine, m.EndColumn ), attribsOfSymbol s
            ]

    allImplementationLocations |> shouldEqual
           [("N", "file2", (2, 7), (2, 8), ["module"]);
           ("val y2", "file2", (13, 4), (13, 6), ["val"]);
           ("val pair2", "file2", (24, 10), (24, 15), ["val"]);
           ("val pair1", "file2", (24, 4), (24, 9), ["val"]);
           ("val enumValue", "file2", (31, 4), (31, 13), ["val"]);
           ("val op_PlusPlus", "file2", (33, 5), (33, 7), ["val"]);
           ("val c1", "file2", (35, 4), (35, 6), ["val"]);
           ("val c2", "file2", (37, 4), (37, 6), ["val"]);
           ("val mmmm1", "file2", (39, 4), (39, 9), ["val"]);
           ("val mmmm2", "file2", (40, 4), (40, 9), ["val"]);
           ("D1", "file2", (6, 5), (6, 7), ["class"]);
           ("member .ctor", "file2", (6, 5), (6, 7), ["member"; "ctor"]);
           ("member get_SomeProperty", "file2", (7, 13), (7, 25), ["member"; "getter"]);
           ("property SomeProperty", "file2", (7, 13), (7, 25), ["member"; "prop"]);
           ("D2", "file2", (9, 5), (9, 7), ["class"]);
           ("member .ctor", "file2", (9, 5), (9, 7), ["member"; "ctor"]);
           ("member get_SomeProperty", "file2", (10, 13), (10, 25),
            ["member"; "getter"]);
           ("property SomeProperty", "file2", (10, 13), (10, 25), ["member"; "prop"]);
           ("D3", "file2", (16, 5), (16, 7), ["class"]);
           ("member .ctor", "file2", (16, 5), (16, 7), ["member"; "ctor"]);
           ("member get_SomeProperty", "file2", (22, 13), (22, 25),
            ["member"; "getter"]);
           ("property SomeProperty", "file2", (22, 13), (22, 25), ["member"; "prop"]);
           ("field a", "file2", (16, 8), (16, 9), ["field"; "compgen"]);
           ("field b", "file2", (17, 8), (17, 9), ["field"; "compgen"]);
           ("field x", "file2", (20, 16), (20, 17), ["field"; "default"; "mutable"]);
           ("SaveOptions", "file2", (27, 5), (27, 16), ["enum"; "valuetype"]);
           ("field value__", "file2", (28, 2), (29, 25), ["field"; "compgen"]);
           ("field None", "file2", (28, 4), (28, 8), ["field"; "static"; "0"]);
           ("field DisableFormatting", "file2", (29, 4), (29, 21), ["field"; "static"; "1"]);
           ("M", "file1", (2, 7), (2, 8), ["module"]);
           ("val xxx", "file1", (7, 4), (7, 7), ["val"]);
           ("val fff", "file1", (8, 4), (8, 7), ["val"]);
           ("C", "file1", (4, 5), (4, 6), ["class"]);
           ("member .ctor", "file1", (4, 5), (4, 6), ["member"; "ctor"]);
           ("member get_P", "file1", (5, 13), (5, 14), ["member"; "getter"]);
           ("property P", "file1", (5, 13), (5, 14), ["member"; "prop"]);
           ("CAbbrev", "file1", (10, 5), (10, 12), ["abbrev"]);
           ("property P", "file1", (5, 13), (5, 14), ["member"; "prop"])]

    [ for x in allSymbols -> x.ToString() ]
      |> shouldEqual
              ["N"; "val y2"; "val pair2"; "val pair1"; "val enumValue"; "val op_PlusPlus";
               "val c1"; "val c2"; "val mmmm1"; "val mmmm2"; "D1"; "member .ctor";
               "member get_SomeProperty"; "property SomeProperty"; "D2"; "member .ctor";
               "member get_SomeProperty"; "property SomeProperty"; "D3"; "member .ctor";
               "member get_SomeProperty"; "property SomeProperty"; "field a"; "field b";
               "field x"; "SaveOptions"; "field value__"; "field None";
               "field DisableFormatting"; "M"; "val xxx"; "val fff"; "C"; "member .ctor";
               "member get_P"; "property P"; "CAbbrev"; "property P"]

[<Test>]
let ``Test project1 all symbols excluding compiler generated`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project1.options) |> Async.RunImmediate
    let allSymbolsNoCompGen = allSymbolsInEntities false wholeProjectResults.AssemblySignature.Entities
    [ for x in allSymbolsNoCompGen -> x.ToString() ]
      |> shouldEqual
              ["N"; "val y2"; "val pair2"; "val pair1"; "val enumValue"; "val op_PlusPlus";
               "val c1"; "val c2"; "val mmmm1"; "val mmmm2"; "D1"; "member .ctor";
               "member get_SomeProperty"; "property SomeProperty"; "D2"; "member .ctor";
               "member get_SomeProperty"; "property SomeProperty"; "D3"; "member .ctor";
               "member get_SomeProperty"; "property SomeProperty"; "field x";
               "SaveOptions"; "field None"; "field DisableFormatting"; "M"; "val xxx";
               "val fff"; "C"; "member .ctor"; "member get_P"; "property P"; "CAbbrev";
               "property P"]

[<Test>]
let ``Test project1 xxx symbols`` () =


    let wholeProjectResults = checker.ParseAndCheckProject(Project1.options) |> Async.RunImmediate
    let backgroundParseResults1, backgroundTypedParse1 =
        checker.GetBackgroundCheckResultsForFileInProject(Project1.fileName1, Project1.options)
        |> Async.RunImmediate

    let xSymbolUseOpt = backgroundTypedParse1.GetSymbolUseAtLocation(9,9,"",["xxx"])
    let xSymbolUse = xSymbolUseOpt.Value
    let xSymbol = xSymbolUse.Symbol
    xSymbol.ToString() |> shouldEqual "val xxx"

    let usesOfXSymbol =
        [ for su in wholeProjectResults.GetUsesOfSymbol(xSymbol) do
              yield Project1.cleanFileName su.FileName , tups su.Range, attribsOfSymbol su.Symbol ]

    usesOfXSymbol |> shouldEqual
       [("file1", ((7, 4), (7, 7)), ["val"]);
        ("file1", ((8, 13), (8, 16)), ["val"]);
        ("file1", ((8, 19), (8, 22)), ["val"]);
        ("file2", ((7, 28), (7, 33)), ["val"]);
        ("file2", ((13, 27), (13, 32)), ["val"])]

[<Test>]
let ``Test project1 all uses of all signature symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project1.options) |> Async.RunImmediate
    let allSymbols = allSymbolsInEntities true wholeProjectResults.AssemblySignature.Entities
    let allUsesOfAllSymbols =
        [ for s in allSymbols do
             yield s.ToString(),
                  [ for s in wholeProjectResults.GetUsesOfSymbol(s) ->
                         (Project1.cleanFileName s.FileName, tupsZ s.Range) ] ]
    let expected =
        [("N", [("file2", ((1, 7), (1, 8)))]);
         ("val y2", [("file2", ((12, 4), (12, 6)))]);
         ("val pair2", [("file2", ((23, 10), (23, 15)))]);
         ("val pair1", [("file2", ((23, 4), (23, 9)))]);
         ("val enumValue", [("file2", ((30, 4), (30, 13)))]);
         ("val op_PlusPlus",
          [("file2", ((32, 5), (32, 7))); ("file2", ((34, 11), (34, 13)));
           ("file2", ((36, 11), (36, 13)))]);
         ("val c1", [("file2", ((34, 4), (34, 6)))]);
         ("val c2", [("file2", ((36, 4), (36, 6)))]);
         ("val mmmm1", [("file2", ((38, 4), (38, 9)))]);
         ("val mmmm2", [("file2", ((39, 4), (39, 9)))]);
         ("D1", [("file2", ((5, 5), (5, 7))); ("file2", ((9, 38), (9, 40)))]);
         ("member .ctor", [("file2", ((5, 5), (5, 7))); ("file2", ((9, 38), (9, 40)))]);
         ("member get_SomeProperty", [("file2", ((6, 13), (6, 25)))]);
         ("property SomeProperty", [("file2", ((6, 13), (6, 25)))]);
         ("D2", [("file2", ((8, 5), (8, 7)))]);
         ("member .ctor", [("file2", ((8, 5), (8, 7)))]);
         ("member get_SomeProperty", [("file2", ((9, 13), (9, 25)))]);
         ("property SomeProperty", [("file2", ((9, 13), (9, 25)))]);
         ("D3", [("file2", ((15, 5), (15, 7)))]);
         ("member .ctor", [("file2", ((15, 5), (15, 7)))]);
         ("member get_SomeProperty", [("file2", ((21, 13), (21, 25)))]);
         ("property SomeProperty", [("file2", ((21, 13), (21, 25)))]); ("field a", []);
         ("field b", []); ("field x", [("file2", ((19, 16), (19, 17)))]);
         ("SaveOptions",
          [("file2", ((26, 5), (26, 16))); ("file2", ((30, 16), (30, 27)))]);
         ("field value__", []); ("field None", [("file2", ((27, 4), (27, 8)))]);
         ("field DisableFormatting",
          [("file2", ((28, 4), (28, 21))); ("file2", ((30, 16), (30, 45)))]);
         ("M",
          [("file1", ((1, 7), (1, 8))); ("file2", ((3, 5), (3, 6)));
           ("file2", ((6, 28), (6, 29))); ("file2", ((9, 28), (9, 29)));
           ("file2", ((12, 27), (12, 28))); ("file2", ((38, 12), (38, 13)));
           ("file2", ((38, 22), (38, 23))); ("file2", ((39, 12), (39, 13)));
           ("file2", ((39, 28), (39, 29)))]);
         ("val xxx",
          [("file1", ((6, 4), (6, 7))); ("file1", ((7, 13), (7, 16)));
           ("file1", ((7, 19), (7, 22))); ("file2", ((6, 28), (6, 33)));
           ("file2", ((12, 27), (12, 32)))]);
         ("val fff", [("file1", ((7, 4), (7, 7))); ("file2", ((9, 28), (9, 33)))]);
         ("C",
          [("file1", ((3, 5), (3, 6))); ("file1", ((9, 15), (9, 16)));
           ("file2", ((38, 12), (38, 15))); ("file2", ((38, 22), (38, 25)))]);
         ("member .ctor",
          [("file1", ((3, 5), (3, 6))); ("file1", ((9, 15), (9, 16)));
           ("file2", ((38, 12), (38, 15))); ("file2", ((38, 22), (38, 25)))]);
         ("member get_P", [("file1", ((4, 13), (4, 14)))]);
         ("property P", [("file1", ((4, 13), (4, 14)))]);
         ("CAbbrev",
          [("file1", ((9, 5), (9, 12))); ("file2", ((39, 12), (39, 21)));
           ("file2", ((39, 28), (39, 37)))]);
         ("property P", [("file1", ((4, 13), (4, 14)))])]
    set allUsesOfAllSymbols - set expected |> shouldEqual Set.empty
    set expected - set allUsesOfAllSymbols |> shouldEqual Set.empty
    (set expected = set allUsesOfAllSymbols) |> shouldEqual true

[<Test>]
let ``Test project1 all uses of all symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project1.options) |> Async.RunImmediate
    let allUsesOfAllSymbols =
        [ for s in wholeProjectResults.GetAllUsesOfAllSymbols() ->
              s.Symbol.DisplayName, s.Symbol.FullName, Project1.cleanFileName s.FileName, tupsZ s.Range, attribsOfSymbol s.Symbol ]
    let expected =
              [("C", "M.C", "file1", ((3, 5), (3, 6)), ["class"]);
               ("( .ctor )", "M.C.( .ctor )", "file1", ((3, 5), (3, 6)),
                ["member"; "ctor"]);
               ("P", "M.C.P", "file1", ((4, 13), (4, 14)), ["member"; "getter"]);
               ("x", "x", "file1", ((4, 11), (4, 12)), []);
               ("( + )", "Microsoft.FSharp.Core.Operators.( + )", "file1",
                ((6, 12), (6, 13)), ["val"]);
               ("xxx", "M.xxx", "file1", ((6, 4), (6, 7)), ["val"]);
               ("( + )", "Microsoft.FSharp.Core.Operators.( + )", "file1",
                ((7, 17), (7, 18)), ["val"]);
               ("xxx", "M.xxx", "file1", ((7, 13), (7, 16)), ["val"]);
               ("xxx", "M.xxx", "file1", ((7, 19), (7, 22)), ["val"]);
               ("fff", "M.fff", "file1", ((7, 4), (7, 7)), ["val"]);
               ("C", "M.C", "file1", ((9, 15), (9, 16)), ["class"]);
               ("C", "M.C", "file1", ((9, 15), (9, 16)), ["class"]);
               ("C", "M.C", "file1", ((9, 15), (9, 16)), ["class"]);
               ("C", "M.C", "file1", ((9, 15), (9, 16)), ["class"]);
               ("CAbbrev", "M.CAbbrev", "file1", ((9, 5), (9, 12)), ["abbrev"]);
               ("M", "M", "file1", ((1, 7), (1, 8)), ["module"]);
               ("M", "M", "file2", ((3, 5), (3, 6)), ["module"]);
               ("D1", "N.D1", "file2", ((5, 5), (5, 7)), ["class"]);
               ("( .ctor )", "N.D1.( .ctor )", "file2", ((5, 5), (5, 7)),
                ["member"; "ctor"]);
               ("SomeProperty", "N.D1.SomeProperty", "file2", ((6, 13), (6, 25)),
                ["member"; "getter"]); ("x", "x", "file2", ((6, 11), (6, 12)), []);
               ("M", "M", "file2", ((6, 28), (6, 29)), ["module"]);
               ("xxx", "M.xxx", "file2", ((6, 28), (6, 33)), ["val"]);
               ("D2", "N.D2", "file2", ((8, 5), (8, 7)), ["class"]);
               ("( .ctor )", "N.D2.( .ctor )", "file2", ((8, 5), (8, 7)),
                ["member"; "ctor"]);
               ("SomeProperty", "N.D2.SomeProperty", "file2", ((9, 13), (9, 25)),
                ["member"; "getter"]); ("x", "x", "file2", ((9, 11), (9, 12)), []);
               ("( + )", "Microsoft.FSharp.Core.Operators.( + )", "file2",
                ((9, 36), (9, 37)), ["val"]);
               ("M", "M", "file2", ((9, 28), (9, 29)), ["module"]);
               ("fff", "M.fff", "file2", ((9, 28), (9, 33)), ["val"]);
               ("D1", "N.D1", "file2", ((9, 38), (9, 40)), ["member"; "ctor"]);
               ("M", "M", "file2", ((12, 27), (12, 28)), ["module"]);
               ("xxx", "M.xxx", "file2", ((12, 27), (12, 32)), ["val"]);
               ("y2", "N.y2", "file2", ((12, 4), (12, 6)), ["val"]);
               ("DefaultValueAttribute", "Microsoft.FSharp.Core.DefaultValueAttribute",
                "file2", ((18, 6), (18, 18)), ["class"]);
               ("DefaultValueAttribute", "Microsoft.FSharp.Core.DefaultValueAttribute",
                "file2", ((18, 6), (18, 18)), ["class"]);
               ("DefaultValueAttribute", "Microsoft.FSharp.Core.DefaultValueAttribute",
                "file2", ((18, 6), (18, 18)), ["member"]);
               ("int", "Microsoft.FSharp.Core.int", "file2", ((19, 20), (19, 23)),
                ["abbrev"]);
               ("DefaultValueAttribute", "Microsoft.FSharp.Core.DefaultValueAttribute",
                "file2", ((18, 6), (18, 18)), ["class"]);
               ("DefaultValueAttribute", "Microsoft.FSharp.Core.DefaultValueAttribute",
                "file2", ((18, 6), (18, 18)), ["class"]);
               ("DefaultValueAttribute", "Microsoft.FSharp.Core.DefaultValueAttribute",
                "file2", ((18, 6), (18, 18)), ["member"]);
               ("x", "N.D3.x", "file2", ((19, 16), (19, 17)),
                ["field"; "default"; "mutable"]);
               ("D3", "N.D3", "file2", ((15, 5), (15, 7)), ["class"]);
               ("int", "Microsoft.FSharp.Core.int", "file2", ((15, 10), (15, 13)),
                ["abbrev"]); ("a", "a", "file2", ((15, 8), (15, 9)), []);
               ("( .ctor )", "N.D3.( .ctor )", "file2", ((15, 5), (15, 7)),
                ["member"; "ctor"]);
               ("SomeProperty", "N.D3.SomeProperty", "file2", ((21, 13), (21, 25)),
                ["member"; "getter"]);
               ("( + )", "Microsoft.FSharp.Core.Operators.( + )", "file2",
                ((16, 14), (16, 15)), ["val"]);
               ("a", "a", "file2", ((16, 12), (16, 13)), []);
               ("b", "b", "file2", ((16, 8), (16, 9)), []);
               ("x", "x", "file2", ((21, 11), (21, 12)), []);
               ("( + )", "Microsoft.FSharp.Core.Operators.( + )", "file2",
                ((21, 30), (21, 31)), ["val"]);
               ("a", "a", "file2", ((21, 28), (21, 29)), []);
               ("b", "b", "file2", ((21, 32), (21, 33)), []);
               ("( + )", "Microsoft.FSharp.Core.Operators.( + )", "file2",
                ((23, 25), (23, 26)), ["val"]);
               ("( + )", "Microsoft.FSharp.Core.Operators.( + )", "file2",
                ((23, 21), (23, 22)), ["val"]);
               ("int32", "Microsoft.FSharp.Core.Operators.int32", "file2",
                ((23, 27), (23, 32)), ["val"]);
               ("DateTime", "System.DateTime", "file2", ((23, 40), (23, 48)),
                ["valuetype"]);
               ("System", "System", "file2", ((23, 33), (23, 39)), ["namespace"]);
               ("Now", "System.DateTime.Now", "file2", ((23, 33), (23, 52)),
                ["member"; "prop"]);
               ("Ticks", "System.DateTime.Ticks", "file2", ((23, 33), (23, 58)),
                ["member"; "prop"]);
               ("( + )", "Microsoft.FSharp.Core.Operators.( + )", "file2",
                ((23, 62), (23, 63)), ["val"]);
               ("pair2", "N.pair2", "file2", ((23, 10), (23, 15)), ["val"]);
               ("pair1", "N.pair1", "file2", ((23, 4), (23, 9)), ["val"]);
               ("None", "N.SaveOptions.None", "file2", ((27, 4), (27, 8)),
                ["field"; "static"; "0"]);
               ("DisableFormatting", "N.SaveOptions.DisableFormatting", "file2",
                ((28, 4), (28, 21)), ["field"; "static"; "1"]);
               ("SaveOptions", "N.SaveOptions", "file2", ((26, 5), (26, 16)),
                ["enum"; "valuetype"]);
               ("SaveOptions", "N.SaveOptions", "file2", ((30, 16), (30, 27)),
                ["enum"; "valuetype"]);
               ("DisableFormatting", "N.SaveOptions.DisableFormatting", "file2",
                ((30, 16), (30, 45)), ["field"; "static"; "1"]);
               ("enumValue", "N.enumValue", "file2", ((30, 4), (30, 13)), ["val"]);
               ("x", "x", "file2", ((32, 9), (32, 10)), []);
               ("y", "y", "file2", ((32, 11), (32, 12)), []);
               ("( + )", "Microsoft.FSharp.Core.Operators.( + )", "file2",
                ((32, 17), (32, 18)), ["val"]);
               ("x", "x", "file2", ((32, 15), (32, 16)), []);
               ("y", "y", "file2", ((32, 19), (32, 20)), []);
               ("( ++ )", "N.( ++ )", "file2", ((32, 5), (32, 7)), ["val"]);
               ("( ++ )", "N.( ++ )", "file2", ((34, 11), (34, 13)), ["val"]);
               ("c1", "N.c1", "file2", ((34, 4), (34, 6)), ["val"]);
               ("( ++ )", "N.( ++ )", "file2", ((36, 11), (36, 13)), ["val"]);
               ("c2", "N.c2", "file2", ((36, 4), (36, 6)), ["val"]);
               ("M", "M", "file2", ((38, 12), (38, 13)), ["module"]);
               ("C", "M.C", "file2", ((38, 12), (38, 15)), ["class"]);
               ("M", "M", "file2", ((38, 22), (38, 23)), ["module"]);
               ("C", "M.C", "file2", ((38, 22), (38, 25)), ["class"]);
               ("C", "M.C", "file2", ((38, 22), (38, 25)), ["member"; "ctor"]);
               ("mmmm1", "N.mmmm1", "file2", ((38, 4), (38, 9)), ["val"]);
               ("M", "M", "file2", ((39, 12), (39, 13)), ["module"]);
               ("CAbbrev", "M.CAbbrev", "file2", ((39, 12), (39, 21)), ["abbrev"]);
               ("M", "M", "file2", ((39, 28), (39, 29)), ["module"]);
               ("CAbbrev", "M.CAbbrev", "file2", ((39, 28), (39, 37)), ["abbrev"]);
               ("C", "M.C", "file2", ((39, 28), (39, 37)), ["member"; "ctor"]);
               ("mmmm2", "N.mmmm2", "file2", ((39, 4), (39, 9)), ["val"]);
               ("N", "N", "file2", ((1, 7), (1, 8)), ["module"])]

    set allUsesOfAllSymbols - set expected |> shouldEqual Set.empty
    set expected - set allUsesOfAllSymbols |> shouldEqual Set.empty
    (set expected = set allUsesOfAllSymbols) |> shouldEqual true

#if !NO_EXTENSIONTYPING
[<Test>]
let ``Test file explicit parse symbols`` () =


    let wholeProjectResults = checker.ParseAndCheckProject(Project1.options) |> Async.RunImmediate
    let parseResults1 = checker.ParseFile(Project1.fileName1, Project1.fileSource1, Project1.parsingOptions)  |> Async.RunImmediate
    let parseResults2 = checker.ParseFile(Project1.fileName2, Project1.fileSource2, Project1.parsingOptions)  |> Async.RunImmediate

    let checkResults1 =
        checker.CheckFileInProject(parseResults1, Project1.fileName1, 0, Project1.fileSource1, Project1.options)
        |> Async.RunImmediate
        |> function FSharpCheckFileAnswer.Succeeded x ->  x | _ -> failwith "unexpected aborted"

    let checkResults2 =
        checker.CheckFileInProject(parseResults2, Project1.fileName2, 0, Project1.fileSource2, Project1.options)
        |> Async.RunImmediate
        |> function FSharpCheckFileAnswer.Succeeded x ->  x | _ -> failwith "unexpected aborted"

    let xSymbolUse2Opt = checkResults1.GetSymbolUseAtLocation(9,9,"",["xxx"])
    let xSymbol2 = xSymbolUse2Opt.Value.Symbol
    let usesOfXSymbol2 =
        [| for s in wholeProjectResults.GetUsesOfSymbol(xSymbol2) -> (Project1.cleanFileName s.FileName, tupsZ s.Range) |]

    let usesOfXSymbol21 =
        [| for s in checkResults1.GetUsesOfSymbolInFile(xSymbol2) -> (Project1.cleanFileName s.FileName, tupsZ s.Range) |]

    let usesOfXSymbol22 =
        [| for s in checkResults2.GetUsesOfSymbolInFile(xSymbol2) -> (Project1.cleanFileName s.FileName, tupsZ s.Range) |]

    usesOfXSymbol2
         |> shouldEqual [|("file1", ((6, 4), (6, 7)));
                          ("file1", ((7, 13), (7, 16)));
                          ("file1", ((7, 19), (7, 22)));
                          ("file2", ((6, 28), (6, 33)));
                          ("file2", ((12, 27), (12, 32)))|]

    usesOfXSymbol21
         |> shouldEqual [|("file1", ((6, 4), (6, 7)));
                          ("file1", ((7, 13), (7, 16)));
                          ("file1", ((7, 19), (7, 22)))|]

    usesOfXSymbol22
         |> shouldEqual [|("file2", ((6, 28), (6, 33)));
                          ("file2", ((12, 27), (12, 32)))|]


[<Test>]
let ``Test file explicit parse all symbols`` () =


    let wholeProjectResults = checker.ParseAndCheckProject(Project1.options) |> Async.RunImmediate
    let parseResults1 = checker.ParseFile(Project1.fileName1, Project1.fileSource1, Project1.parsingOptions) |> Async.RunImmediate
    let parseResults2 = checker.ParseFile(Project1.fileName2, Project1.fileSource2, Project1.parsingOptions) |> Async.RunImmediate

    let checkResults1 =
        checker.CheckFileInProject(parseResults1, Project1.fileName1, 0, Project1.fileSource1, Project1.options)
        |> Async.RunImmediate
        |> function FSharpCheckFileAnswer.Succeeded x ->  x | _ -> failwith "unexpected aborted"

    let checkResults2 =
        checker.CheckFileInProject(parseResults2, Project1.fileName2, 0, Project1.fileSource2, Project1.options)
        |> Async.RunImmediate
        |> function FSharpCheckFileAnswer.Succeeded x ->  x | _ -> failwith "unexpected aborted"

    let usesOfSymbols = checkResults1.GetAllUsesOfAllSymbolsInFile()
    let cleanedUsesOfSymbols =
         [ for s in usesOfSymbols -> s.Symbol.DisplayName, Project1.cleanFileName s.FileName, tupsZ s.Range, attribsOfSymbol s.Symbol ]

    cleanedUsesOfSymbols
       |> shouldEqual
              [("C", "file1", ((3, 5), (3, 6)), ["class"]);
               ("( .ctor )", "file1", ((3, 5), (3, 6)), ["member"; "ctor"]);
               ("P", "file1", ((4, 13), (4, 14)), ["member"; "getter"]);
               ("x", "file1", ((4, 11), (4, 12)), []);
               ("( + )", "file1", ((6, 12), (6, 13)), ["val"]);
               ("xxx", "file1", ((6, 4), (6, 7)), ["val"]);
               ("( + )", "file1", ((7, 17), (7, 18)), ["val"]);
               ("xxx", "file1", ((7, 13), (7, 16)), ["val"]);
               ("xxx", "file1", ((7, 19), (7, 22)), ["val"]);
               ("fff", "file1", ((7, 4), (7, 7)), ["val"]);
               ("C", "file1", ((9, 15), (9, 16)), ["class"]);
               ("C", "file1", ((9, 15), (9, 16)), ["class"]);
               ("C", "file1", ((9, 15), (9, 16)), ["class"]);
               ("C", "file1", ((9, 15), (9, 16)), ["class"]);
               ("CAbbrev", "file1", ((9, 5), (9, 12)), ["abbrev"]);
               ("M", "file1", ((1, 7), (1, 8)), ["module"])]
#endif

//-----------------------------------------------------------------------------------------

module internal Project2 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module M

type DUWithNormalFields =
    | DU1 of int * int
    | DU2 of int * int
    | D of int * int

let _ = DU1(1, 2)
let _ = DU2(1, 2)
let _ = D(1, 2)

type DUWithNamedFields = DU of x : int * y : int

let _ = DU(x=1, y=2)

type GenericClass<'T>() =
    member x.GenericMethod<'U>(t: 'T, u: 'U) = 1

let c = GenericClass<int>()
let _ = c.GenericMethod<int>(3, 4)

let GenericFunction (x:'T, y: 'T) = (x,y) : ('T * 'T)

let _ = GenericFunction(3, 4)
    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)




[<Test>]
let ``Test project2 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project2.options) |> Async.RunImmediate
    wholeProjectResults .Diagnostics.Length |> shouldEqual 0


[<Test>]
let ``Test project2 basic`` () =


    let wholeProjectResults = checker.ParseAndCheckProject(Project2.options) |> Async.RunImmediate

    set [ for x in wholeProjectResults.AssemblySignature.Entities -> x.DisplayName ] |> shouldEqual (set ["M"])

    [ for x in wholeProjectResults.AssemblySignature.Entities.[0].NestedEntities -> x.DisplayName ] |> shouldEqual ["DUWithNormalFields"; "DUWithNamedFields"; "GenericClass" ]

    set [ for x in wholeProjectResults.AssemblySignature.Entities.[0].MembersFunctionsAndValues -> x.DisplayName ]
        |> shouldEqual (set ["c"; "GenericFunction"])

[<Test>]
let ``Test project2 all symbols in signature`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project2.options) |> Async.RunImmediate
    let allSymbols = allSymbolsInEntities true wholeProjectResults.AssemblySignature.Entities
    [ for x in allSymbols -> x.ToString() ]
       |> shouldEqual
              ["M"; "val c"; "val GenericFunction"; "generic parameter T";
               "DUWithNormalFields"; "DU1"; "field Item1"; "field Item2"; "DU2";
               "field Item1"; "field Item2"; "D"; "field Item1"; "field Item2";
               "DUWithNamedFields"; "DU"; "field x"; "field y"; "GenericClass`1";
               "generic parameter T"; "member .ctor"; "member GenericMethod";
               "generic parameter U"]

[<Test>]
let ``Test project2 all uses of all signature symbols`` () =
    let wholeProjectResults = checker.ParseAndCheckProject(Project2.options) |> Async.RunImmediate
    let allSymbols = allSymbolsInEntities true wholeProjectResults.AssemblySignature.Entities
    let allUsesOfAllSymbols =
        [ for s in allSymbols do
             let uses = [ for s in wholeProjectResults.GetUsesOfSymbol(s) -> (if s.FileName = Project2.fileName1 then "file1" else "??"), tupsZ s.Range ]
             yield s.ToString(), uses ]
    let expected =
              [("M", [("file1", ((1, 7), (1, 8)))]);
               ("val c", [("file1", ((19, 4), (19, 5))); ("file1", ((20, 8), (20, 9)))]);
               ("val GenericFunction",
                [("file1", ((22, 4), (22, 19))); ("file1", ((24, 8), (24, 23)))]);
               ("generic parameter T",
                [("file1", ((22, 23), (22, 25))); ("file1", ((22, 30), (22, 32)));
                 ("file1", ((22, 45), (22, 47))); ("file1", ((22, 50), (22, 52)))]);
               ("DUWithNormalFields", [("file1", ((3, 5), (3, 23)))]);
               ("DU1", [("file1", ((4, 6), (4, 9))); ("file1", ((8, 8), (8, 11)))]);
               ("field Item1", []); ("field Item2", []);
               ("DU2", [("file1", ((5, 6), (5, 9))); ("file1", ((9, 8), (9, 11)))]);
               ("D", [("file1", ((6, 6), (6, 7))); ("file1", ((10, 8), (10, 9)))]);
               ("DUWithNamedFields", [("file1", ((12, 5), (12, 22)))]);
               ("DU", [("file1", ((12, 25), (12, 27))); ("file1", ((14, 8), (14, 10)))]);
               ("field x", [("file1", ((12, 31), (12, 32))); ("file1", ((14, 11), (14, 12)))]);
               ("field y", [("file1", ((12, 41), (12, 42))); ("file1", ((14, 16), (14, 17)))]);
               ("GenericClass`1",
                [("file1", ((16, 5), (16, 17))); ("file1", ((19, 8), (19, 20)))]);
               ("generic parameter T",
                [("file1", ((16, 18), (16, 20))); ("file1", ((17, 34), (17, 36)))]);
               ("member .ctor",
                [("file1", ((16, 5), (16, 17))); ("file1", ((19, 8), (19, 20)))]);
               ("member GenericMethod",
                [("file1", ((17, 13), (17, 26))); ("file1", ((20, 8), (20, 23)))]);
               ("generic parameter U",
                [("file1", ((17, 27), (17, 29))); ("file1", ((17, 41), (17, 43)))])]
    set allUsesOfAllSymbols - set expected |> shouldEqual Set.empty
    set expected - set allUsesOfAllSymbols |> shouldEqual Set.empty
    (set expected = set allUsesOfAllSymbols) |> shouldEqual true

[<Test>]
let ``Test project2 all uses of all symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project2.options) |> Async.RunImmediate
    let allUsesOfAllSymbols =
        [ for s in wholeProjectResults.GetAllUsesOfAllSymbols() ->
            s.Symbol.DisplayName, (if s.FileName = Project2.fileName1 then "file1" else "???"), tupsZ s.Range, attribsOfSymbol s.Symbol ]
    let expected =
          [("int", "file1", ((4, 13), (4, 16)), ["abbrev"]);
           ("int", "file1", ((4, 19), (4, 22)), ["abbrev"]);
           ("int", "file1", ((5, 13), (5, 16)), ["abbrev"]);
           ("int", "file1", ((5, 19), (5, 22)), ["abbrev"]);
           ("int", "file1", ((6, 11), (6, 14)), ["abbrev"]);
           ("int", "file1", ((6, 17), (6, 20)), ["abbrev"]);
           ("int", "file1", ((4, 13), (4, 16)), ["abbrev"]);
           ("int", "file1", ((4, 19), (4, 22)), ["abbrev"]);
           ("int", "file1", ((5, 13), (5, 16)), ["abbrev"]);
           ("int", "file1", ((5, 19), (5, 22)), ["abbrev"]);
           ("int", "file1", ((6, 11), (6, 14)), ["abbrev"]);
           ("int", "file1", ((6, 17), (6, 20)), ["abbrev"]);
           ("DU1", "file1", ((4, 6), (4, 9)), []);
           ("DU2", "file1", ((5, 6), (5, 9)), []);
           ("D", "file1", ((6, 6), (6, 7)), []);
           ("DUWithNormalFields", "file1", ((3, 5), (3, 23)), ["union"]);
           ("DU1", "file1", ((8, 8), (8, 11)), []);
           ("DU2", "file1", ((9, 8), (9, 11)), []);
           ("D", "file1", ((10, 8), (10, 9)), []);
           ("int", "file1", ((12, 35), (12, 38)), ["abbrev"]);
           ("int", "file1", ((12, 45), (12, 48)), ["abbrev"]);
           ("int", "file1", ((12, 35), (12, 38)), ["abbrev"]);
           ("x", "file1", ((12, 31), (12, 32)), ["field"]);
           ("int", "file1", ((12, 45), (12, 48)), ["abbrev"]);
           ("y", "file1", ((12, 41), (12, 42)), ["field"]);
           ("DU", "file1", ((12, 25), (12, 27)), []);
           ("DUWithNamedFields", "file1", ((12, 5), (12, 22)), ["union"]);
           ("DU", "file1", ((14, 8), (14, 10)), []);
           ("x", "file1", ((14, 11), (14, 12)), ["field"]);
           ("y", "file1", ((14, 16), (14, 17)), ["field"]);
           ("T", "file1", ((16, 18), (16, 20)), []);
           ("GenericClass", "file1", ((16, 5), (16, 17)), ["class"]);
           ("( .ctor )", "file1", ((16, 5), (16, 17)), ["member"; "ctor"]);
           ("U", "file1", ((17, 27), (17, 29)), []);
           ("T", "file1", ((17, 34), (17, 36)), []);
           ("U", "file1", ((17, 41), (17, 43)), []);
           ("GenericMethod", "file1", ((17, 13), (17, 26)), ["member"]);
           ("x", "file1", ((17, 11), (17, 12)), []);
           ("T", "file1", ((17, 34), (17, 36)), []);
           ("U", "file1", ((17, 41), (17, 43)), []);
           ("u", "file1", ((17, 38), (17, 39)), []);
           ("t", "file1", ((17, 31), (17, 32)), []);
           ("GenericClass", "file1", ((19, 8), (19, 20)), ["member"; "ctor"]);
           ("int", "file1", ((19, 21), (19, 24)), ["abbrev"]);
           ("c", "file1", ((19, 4), (19, 5)), ["val"]);
           ("c", "file1", ((20, 8), (20, 9)), ["val"]);
           ("GenericMethod", "file1", ((20, 8), (20, 23)), ["member"]);
           ("int", "file1", ((20, 24), (20, 27)), ["abbrev"]);
           ("T", "file1", ((22, 23), (22, 25)), []);
           ("T", "file1", ((22, 30), (22, 32)), []);
           ("y", "file1", ((22, 27), (22, 28)), []);
           ("x", "file1", ((22, 21), (22, 22)), []);
           ("T", "file1", ((22, 45), (22, 47)), []);
           ("T", "file1", ((22, 50), (22, 52)), []);
           ("x", "file1", ((22, 37), (22, 38)), []);
           ("y", "file1", ((22, 39), (22, 40)), []);
           ("GenericFunction", "file1", ((22, 4), (22, 19)), ["val"]);
           ("GenericFunction", "file1", ((24, 8), (24, 23)), ["val"]);
           ("M", "file1", ((1, 7), (1, 8)), ["module"])]
    set allUsesOfAllSymbols - set expected |> shouldEqual Set.empty
    set expected - set allUsesOfAllSymbols |> shouldEqual Set.empty
    (set expected = set allUsesOfAllSymbols) |> shouldEqual true

//-----------------------------------------------------------------------------------------

module internal Project3 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module M

type IFoo =
    abstract InterfaceProperty: string
    abstract InterfacePropertySet: string with set
    abstract InterfaceMethod: methodArg:string -> string
    [<CLIEvent>]
    abstract InterfaceEvent: IEvent<int>

[<AbstractClass>]
type CFoo() =
    abstract AbstractClassProperty: string
    abstract AbstractClassPropertySet: string with set
    abstract AbstractClassMethod: methodArg:string -> string
    [<CLIEvent>]
    abstract AbstractClassEvent: IEvent<int>

type CBaseFoo() =
    let ev = Event<_>()
    abstract BaseClassProperty: string
    abstract BaseClassPropertySet: string with set
    abstract BaseClassMethod: methodArg:string -> string
    [<CLIEvent>]
    abstract BaseClassEvent: IEvent<int>
    default __.BaseClassProperty = "dflt"
    default __.BaseClassPropertySet with set (v:string) = ()
    default __.BaseClassMethod(m) = m
    [<CLIEvent>]
    default __.BaseClassEvent = ev.Publish

type IFooImpl() =
    let ev = Event<_>()
    interface IFoo with
        member this.InterfaceProperty = "v"
        member this.InterfacePropertySet with set (v:string) = ()
        member this.InterfaceMethod(x) = x
        [<CLIEvent>]
        member this.InterfaceEvent = ev.Publish

type CFooImpl() =
    inherit CFoo()
    let ev = Event<_>()
    override this.AbstractClassProperty = "v"
    override this.AbstractClassPropertySet with set (v:string) = ()
    override this.AbstractClassMethod(x) = x
    [<CLIEvent>]
    override this.AbstractClassEvent = ev.Publish

type CBaseFooImpl() =
    inherit CBaseFoo()
    let ev = Event<_>()
    override this.BaseClassProperty = "v"
    override this.BaseClassPropertySet with set (v:string)  = ()
    override this.BaseClassMethod(x) = x
    [<CLIEvent>]
    override this.BaseClassEvent = ev.Publish

let IFooImplObjectExpression() =
    let ev = Event<_>()
    { new IFoo with
        member this.InterfaceProperty = "v"
        member this.InterfacePropertySet with set (v:string) = ()
        member this.InterfaceMethod(x) = x
        [<CLIEvent>]
        member this.InterfaceEvent = ev.Publish }

let CFooImplObjectExpression() =
    let ev = Event<_>()
    { new CFoo() with
        override this.AbstractClassProperty = "v"
        override this.AbstractClassPropertySet with set (v:string) = ()
        override this.AbstractClassMethod(x) = x
        [<CLIEvent>]
        override this.AbstractClassEvent = ev.Publish }

let getP (foo: IFoo) = foo.InterfaceProperty
let setP (foo: IFoo) v = foo.InterfacePropertySet <- v
let getE (foo: IFoo) = foo.InterfaceEvent
let getM (foo: IFoo) = foo.InterfaceMethod("d")
    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)




[<Test>]
let ``Test project3 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project3.options) |> Async.RunImmediate
    wholeProjectResults .Diagnostics.Length |> shouldEqual 0


[<Test>]
let ``Test project3 basic`` () =


    let wholeProjectResults = checker.ParseAndCheckProject(Project3.options) |> Async.RunImmediate

    set [ for x in wholeProjectResults.AssemblySignature.Entities -> x.DisplayName ] |> shouldEqual (set ["M"])

    [ for x in wholeProjectResults.AssemblySignature.Entities.[0].NestedEntities -> x.DisplayName ]
        |> shouldEqual ["IFoo"; "CFoo"; "CBaseFoo"; "IFooImpl"; "CFooImpl"; "CBaseFooImpl"]

    [ for x in wholeProjectResults.AssemblySignature.Entities.[0].MembersFunctionsAndValues -> x.DisplayName ]
        |> shouldEqual ["IFooImplObjectExpression"; "CFooImplObjectExpression"; "getP"; "setP"; "getE";"getM"]

[<Test>]
let ``Test project3 all symbols in signature`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project3.options) |> Async.RunImmediate
    let allSymbols = allSymbolsInEntities false wholeProjectResults.AssemblySignature.Entities
    let results = [ for x in allSymbols -> x.ToString(), attribsOfSymbol x ]
    [("M", ["module"]);
        ("val IFooImplObjectExpression", ["val"]);
        ("val CFooImplObjectExpression", ["val"]);
        ("val getP", ["val"]);
        ("val setP", ["val"]); ("val getE", ["val"]);
        ("val getM", ["val"]);
        ("IFoo", ["interface"]);
        ("member InterfaceMethod", ["slot"; "member"]);
        ("member add_InterfaceEvent", ["slot"; "member"; "add"]);
        ("member get_InterfaceEvent", ["slot"; "member"; "getter"]);
        ("member get_InterfaceProperty", ["slot"; "member"; "getter"]);
        ("member remove_InterfaceEvent", ["slot"; "member"; "remove"]);
        ("member set_InterfacePropertySet", ["slot"; "member"; "setter"]);
        ("property InterfacePropertySet", ["slot"; "member"; "prop"]);
        ("property InterfaceProperty", ["slot"; "member"; "prop"]);
        ("property InterfaceEvent", ["slot"; "member"; "prop"; "clievent"]);
        ("CFoo", ["class"]);
        ("member .ctor", ["member"; "ctor"]);
        ("member AbstractClassMethod", ["slot"; "member"]);
        ("member add_AbstractClassEvent", ["slot"; "member"; "add"]);
        ("member get_AbstractClassEvent", ["slot"; "member"; "getter"]);
        ("member get_AbstractClassProperty", ["slot"; "member"; "getter"]);
        ("member remove_AbstractClassEvent", ["slot"; "member"; "remove"]);
        ("member set_AbstractClassPropertySet", ["slot"; "member"; "setter"]);
        ("property AbstractClassPropertySet", ["slot"; "member"; "prop"]);
        ("property AbstractClassProperty", ["slot"; "member"; "prop"]);
        ("property AbstractClassEvent", ["slot"; "member"; "prop"; "clievent"]);
        ("CBaseFoo", ["class"]); ("member .ctor", ["member"; "ctor"]);
        ("member BaseClassMethod", ["slot"; "member"]);
        ("member BaseClassMethod", ["member"; "overridemem"]);
        ("member add_BaseClassEvent", ["slot"; "member"; "add"]);
        ("member add_BaseClassEvent", ["member"; "add"; "overridemem"]);
        ("member get_BaseClassEvent", ["slot"; "member"; "getter"]);
        ("member get_BaseClassEvent", ["member"; "getter"; "overridemem"]);
        ("member get_BaseClassProperty", ["slot"; "member"; "getter"]);
        ("member get_BaseClassProperty", ["member"; "getter"; "overridemem"]);
        ("member remove_BaseClassEvent", ["slot"; "member"; "remove"]);
        ("member remove_BaseClassEvent", ["member"; "remove"; "overridemem"]);
        ("member set_BaseClassPropertySet", ["slot"; "member"; "setter"]);
        ("member set_BaseClassPropertySet", ["member"; "setter"; "overridemem"]);
        ("property BaseClassPropertySet", ["member"; "prop"; "overridemem"]);
        ("property BaseClassPropertySet", ["slot"; "member"; "prop"]);
        ("property BaseClassProperty", ["member"; "prop"; "overridemem"]);
        ("property BaseClassProperty", ["slot"; "member"; "prop"]);
        ("property BaseClassEvent", ["member"; "prop"; "overridemem"]);
        ("property BaseClassEvent", ["slot"; "member"; "prop"]);
        ("IFooImpl", ["class"]); ("member .ctor", ["member"; "ctor"]);
        ("member InterfaceMethod", ["member"; "overridemem"; "intfmem"]);
        ("member add_InterfaceEvent", ["member"; "overridemem"; "intfmem"]);
        ("member get_InterfaceEvent", ["member"; "overridemem"; "intfmem"]);
        ("member get_InterfaceProperty", ["member"; "overridemem"; "intfmem"]);
        ("member remove_InterfaceEvent", ["member"; "overridemem"; "intfmem"]);
        ("member set_InterfacePropertySet", ["member"; "overridemem"; "intfmem"]);
        ("CFooImpl", ["class"]); ("member .ctor", ["member"; "ctor"]);
        ("member AbstractClassMethod", ["member"; "overridemem"]);
        ("member add_AbstractClassEvent", ["member"; "add"; "overridemem"]);
        ("member get_AbstractClassEvent", ["member"; "getter"; "overridemem"]);
        ("member get_AbstractClassProperty", ["member"; "getter"; "overridemem"]);
        ("member remove_AbstractClassEvent", ["member"; "remove"; "overridemem"]);
        ("member set_AbstractClassPropertySet", ["member"; "setter"; "overridemem"]);
        ("property AbstractClassPropertySet", ["member"; "prop"; "overridemem"]);
        ("property AbstractClassProperty", ["member"; "prop"; "overridemem"]);
        ("property AbstractClassEvent", ["member"; "prop"; "clievent"; "overridemem"]);
        ("CBaseFooImpl", ["class"]); ("member .ctor", ["member"; "ctor"]);
        ("member BaseClassMethod", ["member"; "overridemem"]);
        ("member add_BaseClassEvent", ["member"; "add"; "overridemem"]);
        ("member get_BaseClassEvent", ["member"; "getter"; "overridemem"]);
        ("member get_BaseClassProperty", ["member"; "getter"; "overridemem"]);
        ("member remove_BaseClassEvent", ["member"; "remove"; "overridemem"]);
        ("member set_BaseClassPropertySet", ["member"; "setter"; "overridemem"]);
        ("property BaseClassPropertySet", ["member"; "prop"; "overridemem"]);
        ("property BaseClassProperty", ["member"; "prop"; "overridemem"]);
        ("property BaseClassEvent", ["member"; "prop"; "clievent"; "overridemem"])]
        |> List.iter (fun x ->
            if results |> List.exists (fun y -> x = y) |> not then
                failwithf "%A does not exist in the collection." x
        )

[<Test>]
let ``Test project3 all uses of all signature symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project3.options) |> Async.RunImmediate
    let allSymbols = allSymbolsInEntities false wholeProjectResults.AssemblySignature.Entities

    let allUsesOfAllSymbols =
        [ for s in allSymbols do
             let uses = [ for s in wholeProjectResults.GetUsesOfSymbol(s) ->
                            ((if s.FileName = Project3.fileName1 then "file1" else "??"),
                             tupsZ s.Range, attribsOfSymbolUse s, attribsOfSymbol s.Symbol) ]
             yield s.ToString(), uses ]
    let expected =
        [("M", [("file1", ((1, 7), (1, 8)), ["defn"], ["module"])]);
         ("val IFooImplObjectExpression",
          [("file1", ((58, 4), (58, 28)), ["defn"], ["val"])]);
         ("val CFooImplObjectExpression",
          [("file1", ((67, 4), (67, 28)), ["defn"], ["val"])]);
         ("val getP", [("file1", ((76, 4), (76, 8)), ["defn"], ["val"])]);
         ("val setP", [("file1", ((77, 4), (77, 8)), ["defn"], ["val"])]);
         ("val getE", [("file1", ((78, 4), (78, 8)), ["defn"], ["val"])]);
         ("val getM", [("file1", ((79, 4), (79, 8)), ["defn"], ["val"])]);
         ("IFoo",
          [("file1", ((3, 5), (3, 9)), ["defn"], ["interface"]);
           ("file1", ((33, 14), (33, 18)), ["type"], ["interface"]);
           ("file1", ((60, 10), (60, 14)), ["type"], ["interface"]);
           ("file1", ((76, 15), (76, 19)), ["type"], ["interface"]);
           ("file1", ((77, 15), (77, 19)), ["type"], ["interface"]);
           ("file1", ((78, 15), (78, 19)), ["type"], ["interface"]);
           ("file1", ((79, 15), (79, 19)), ["type"], ["interface"])]);
         ("member InterfaceMethod",
          [("file1", ((6, 13), (6, 28)), ["defn"], ["slot"; "member"]);
           ("file1", ((63, 20), (63, 35)), ["override"], ["slot"; "member"]);
           ("file1", ((79, 23), (79, 42)), [], ["slot"; "member"]);
           ("file1", ((36, 20), (36, 35)), ["override"], ["slot"; "member"])]);
         ("member add_InterfaceEvent",
          [("file1", ((8, 13), (8, 27)), ["defn"], ["slot"; "member"; "add"]);
           ("file1", ((65, 20), (65, 34)), ["override"], ["slot"; "member"; "add"]);
           ("file1", ((78, 23), (78, 41)), [], ["slot"; "member"; "add"]);
           ("file1", ((38, 20), (38, 34)), ["override"], ["slot"; "member"; "add"])]);
         ("member get_InterfaceEvent",
          [("file1", ((8, 13), (8, 27)), ["defn"], ["slot"; "member"; "getter"]);
           ("file1", ((65, 20), (65, 34)), ["override"], ["slot"; "member"; "getter"]);
           ("file1", ((38, 20), (38, 34)), ["override"], ["slot"; "member"; "getter"])]);
         ("member get_InterfaceProperty",
          [("file1", ((4, 13), (4, 30)), ["defn"], ["slot"; "member"; "getter"]);
           ("file1", ((61, 20), (61, 37)), ["override"], ["slot"; "member"; "getter"]);
           ("file1", ((76, 23), (76, 44)), [], ["slot"; "member"; "getter"]);
           ("file1", ((34, 20), (34, 37)), ["override"], ["slot"; "member"; "getter"])]);
         ("member remove_InterfaceEvent",
          [("file1", ((8, 13), (8, 27)), ["defn"], ["slot"; "member"; "remove"]);
           ("file1", ((65, 20), (65, 34)), ["override"], ["slot"; "member"; "remove"]);
           ("file1", ((38, 20), (38, 34)), ["override"], ["slot"; "member"; "remove"])]);
         ("member set_InterfacePropertySet",
          [("file1", ((5, 13), (5, 33)), ["defn"], ["slot"; "member"; "setter"]);
           ("file1", ((62, 20), (62, 40)), ["override"], ["slot"; "member"; "setter"]);
           ("file1", ((77, 25), (77, 49)), [], ["slot"; "member"; "setter"]);
           ("file1", ((35, 20), (35, 40)), ["override"], ["slot"; "member"; "setter"])]);
         ("property InterfacePropertySet",
          [("file1", ((5, 13), (5, 33)), ["defn"], ["slot"; "member"; "prop"]);
           ("file1", ((62, 20), (62, 40)), ["override"], ["slot"; "member"; "prop"]);
           ("file1", ((77, 25), (77, 49)), [], ["slot"; "member"; "prop"]);
           ("file1", ((35, 20), (35, 40)), ["override"], ["slot"; "member"; "prop"])]);
         ("property InterfaceProperty",
          [("file1", ((4, 13), (4, 30)), ["defn"], ["slot"; "member"; "prop"]);
           ("file1", ((61, 20), (61, 37)), ["override"], ["slot"; "member"; "prop"]);
           ("file1", ((76, 23), (76, 44)), [], ["slot"; "member"; "prop"]);
           ("file1", ((34, 20), (34, 37)), ["override"], ["slot"; "member"; "prop"])]);
         ("property InterfaceEvent",
          [("file1", ((8, 13), (8, 27)), ["defn"], ["slot"; "member"; "prop"; "clievent"]);
           ("file1", ((65, 20), (65, 34)), ["override"], ["slot"; "member"; "prop"; "clievent"]);
           ("file1", ((38, 20), (38, 34)), ["override"], ["slot"; "member"; "prop"; "clievent"])]);
         ("CFoo",
          [("file1", ((11, 5), (11, 9)), ["defn"], ["class"]);
           ("file1", ((41, 12), (41, 16)), ["type"], ["class"]);
           ("file1", ((41, 12), (41, 16)), [], ["class"]);
           ("file1", ((69, 10), (69, 14)), ["type"], ["class"]);
           ("file1", ((69, 10), (69, 14)), [], ["class"])]);
         ("member .ctor",
          [("file1", ((11, 5), (11, 9)), ["defn"], ["member"; "ctor"]);
           ("file1", ((41, 12), (41, 16)), ["type"], ["member"; "ctor"]);
           ("file1", ((41, 12), (41, 16)), [], ["member"; "ctor"]);
           ("file1", ((69, 10), (69, 14)), ["type"], ["member"; "ctor"]);
           ("file1", ((69, 10), (69, 14)), [], ["member"; "ctor"])]);
         ("member AbstractClassMethod",
          [("file1", ((14, 13), (14, 32)), ["defn"], ["slot"; "member"]);
           ("file1", ((72, 22), (72, 41)), ["override"], ["slot"; "member"]);
           ("file1", ((45, 18), (45, 37)), ["override"], ["slot"; "member"])]);
         ("member add_AbstractClassEvent",
          [("file1", ((16, 13), (16, 31)), ["defn"], ["slot"; "member"; "add"]);
           ("file1", ((74, 22), (74, 40)), ["override"], ["slot"; "member"; "add"]);
           ("file1", ((47, 18), (47, 36)), ["override"], ["slot"; "member"; "add"])]);
         ("member get_AbstractClassEvent",
          [("file1", ((16, 13), (16, 31)), ["defn"], ["slot"; "member"; "getter"]);
           ("file1", ((74, 22), (74, 40)), ["override"], ["slot"; "member"; "getter"]);
           ("file1", ((47, 18), (47, 36)), ["override"], ["slot"; "member"; "getter"])]);
         ("member get_AbstractClassProperty",
          [("file1", ((12, 13), (12, 34)), ["defn"], ["slot"; "member"; "getter"]);
           ("file1", ((70, 22), (70, 43)), ["override"], ["slot"; "member"; "getter"]);
           ("file1", ((43, 18), (43, 39)), ["override"], ["slot"; "member"; "getter"])]);
         ("member remove_AbstractClassEvent",
          [("file1", ((16, 13), (16, 31)), ["defn"], ["slot"; "member"; "remove"]);
           ("file1", ((74, 22), (74, 40)), ["override"], ["slot"; "member"; "remove"]);
           ("file1", ((47, 18), (47, 36)), ["override"], ["slot"; "member"; "remove"])]);
         ("member set_AbstractClassPropertySet",
          [("file1", ((13, 13), (13, 37)), ["defn"], ["slot"; "member"; "setter"]);
           ("file1", ((71, 22), (71, 46)), ["override"], ["slot"; "member"; "setter"]);
           ("file1", ((44, 18), (44, 42)), ["override"], ["slot"; "member"; "setter"])]);
         ("property AbstractClassPropertySet",
          [("file1", ((13, 13), (13, 37)), ["defn"], ["slot"; "member"; "prop"]);
           ("file1", ((71, 22), (71, 46)), ["override"], ["slot"; "member"; "prop"]);
           ("file1", ((44, 18), (44, 42)), ["override"], ["slot"; "member"; "prop"])]);
         ("property AbstractClassProperty",
          [("file1", ((12, 13), (12, 34)), ["defn"], ["slot"; "member"; "prop"]);
           ("file1", ((70, 22), (70, 43)), ["override"], ["slot"; "member"; "prop"]);
           ("file1", ((43, 18), (43, 39)), ["override"], ["slot"; "member"; "prop"])]);
         ("property AbstractClassEvent",
          [("file1", ((16, 13), (16, 31)), ["defn"], ["slot"; "member"; "prop"; "clievent"]);
           ("file1", ((74, 22), (74, 40)), ["override"], ["slot"; "member"; "prop"; "clievent"]);
           ("file1", ((47, 18), (47, 36)), ["override"], ["slot"; "member"; "prop"; "clievent"])]);
         ("CBaseFoo",
          [("file1", ((18, 5), (18, 13)), ["defn"], ["class"]);
           ("file1", ((50, 12), (50, 20)), ["type"], ["class"]);
           ("file1", ((50, 12), (50, 20)), [], ["class"])]);
         ("member .ctor",
          [("file1", ((18, 5), (18, 13)), ["defn"], ["member"; "ctor"]);
           ("file1", ((50, 12), (50, 20)), ["type"], ["member"; "ctor"]);
           ("file1", ((50, 12), (50, 20)), [], ["member"; "ctor"])]);
         ("member BaseClassMethod",
          [("file1", ((22, 13), (22, 28)), ["defn"], ["slot"; "member"]);
           ("file1", ((27, 15), (27, 30)), ["override"], ["slot"; "member"]);
           ("file1", ((54, 18), (54, 33)), ["override"], ["slot"; "member"])]);
         ("member BaseClassMethod",
          [("file1", ((27, 15), (27, 30)), ["defn"], ["member"; "overridemem"])]);
         ("member add_BaseClassEvent",
          [("file1", ((24, 13), (24, 27)), ["defn"], ["slot"; "member"; "add"]);
           ("file1", ((29, 15), (29, 29)), ["override"], ["slot"; "member"; "add"]);
           ("file1", ((56, 18), (56, 32)), ["override"], ["slot"; "member"; "add"])]);
         ("member add_BaseClassEvent",
          [("file1", ((29, 15), (29, 29)), ["defn"], ["member"; "add"; "overridemem"])]);
         ("member get_BaseClassEvent",
          [("file1", ((24, 13), (24, 27)), ["defn"], ["slot"; "member"; "getter"]);
           ("file1", ((29, 15), (29, 29)), ["override"], ["slot"; "member"; "getter"]);
           ("file1", ((56, 18), (56, 32)), ["override"], ["slot"; "member"; "getter"])]);
         ("member get_BaseClassEvent",
          [("file1", ((29, 15), (29, 29)), ["defn"], ["member"; "getter"; "overridemem"])]);
         ("member get_BaseClassProperty",
          [("file1", ((20, 13), (20, 30)), ["defn"], ["slot"; "member"; "getter"]);
           ("file1", ((25, 15), (25, 32)), ["override"], ["slot"; "member"; "getter"]);
           ("file1", ((52, 18), (52, 35)), ["override"], ["slot"; "member"; "getter"])]);
         ("member get_BaseClassProperty",
          [("file1", ((25, 15), (25, 32)), ["defn"], ["member"; "getter"; "overridemem"])]);
         ("member remove_BaseClassEvent",
          [("file1", ((24, 13), (24, 27)), ["defn"], ["slot"; "member"; "remove"]);
           ("file1", ((29, 15), (29, 29)), ["override"], ["slot"; "member"; "remove"]);
           ("file1", ((56, 18), (56, 32)), ["override"], ["slot"; "member"; "remove"])]);
         ("member remove_BaseClassEvent",
          [("file1", ((29, 15), (29, 29)), ["defn"], ["member"; "remove"; "overridemem"])]);
         ("member set_BaseClassPropertySet",
          [("file1", ((21, 13), (21, 33)), ["defn"], ["slot"; "member"; "setter"]);
           ("file1", ((26, 15), (26, 35)), ["override"], ["slot"; "member"; "setter"]);
           ("file1", ((53, 18), (53, 38)), ["override"], ["slot"; "member"; "setter"])]);
         ("member set_BaseClassPropertySet",
          [("file1", ((26, 15), (26, 35)), ["defn"], ["member"; "setter"; "overridemem"])]);
         ("property BaseClassPropertySet",
          [("file1", ((26, 15), (26, 35)), ["defn"], ["member"; "prop"; "overridemem"])]);
         ("property BaseClassPropertySet",
          [("file1", ((21, 13), (21, 33)), ["defn"], ["slot"; "member"; "prop"]);
           ("file1", ((26, 15), (26, 35)), ["override"], ["slot"; "member"; "prop"]);
           ("file1", ((53, 18), (53, 38)), ["override"], ["slot"; "member"; "prop"])]);
         ("property BaseClassProperty",
          [("file1", ((25, 15), (25, 32)), ["defn"], ["member"; "prop"; "overridemem"])]);
         ("property BaseClassProperty",
          [("file1", ((20, 13), (20, 30)), ["defn"], ["slot"; "member"; "prop"]);
           ("file1", ((25, 15), (25, 32)), ["override"], ["slot"; "member"; "prop"]);
           ("file1", ((52, 18), (52, 35)), ["override"], ["slot"; "member"; "prop"])]);
         ("property BaseClassEvent",
          [("file1", ((29, 15), (29, 29)), ["defn"], ["member"; "prop"; "overridemem"])]);
         ("property BaseClassEvent",
          [("file1", ((24, 13), (24, 27)), ["defn"], ["slot"; "member"; "prop"]);
           ("file1", ((29, 15), (29, 29)), ["override"], ["slot"; "member"; "prop"]);
           ("file1", ((56, 18), (56, 32)), ["override"], ["slot"; "member"; "prop"])]);
         ("IFooImpl", [("file1", ((31, 5), (31, 13)), ["defn"], ["class"])]);
         ("member .ctor", [("file1", ((31, 5), (31, 13)), ["defn"], ["member"; "ctor"])]);
         ("member InterfaceMethod",
          [("file1", ((36, 20), (36, 35)), ["defn"], ["member"; "overridemem"; "intfmem"])]);
         ("member add_InterfaceEvent",
          [("file1", ((38, 20), (38, 34)), ["defn"], ["member"; "overridemem"; "intfmem"])]);
         ("member get_InterfaceEvent",
          [("file1", ((38, 20), (38, 34)), ["defn"], ["member"; "overridemem"; "intfmem"])]);
         ("member get_InterfaceProperty",
          [("file1", ((34, 20), (34, 37)), ["defn"], ["member"; "overridemem"; "intfmem"])]);
         ("member remove_InterfaceEvent",
          [("file1", ((38, 20), (38, 34)), ["defn"], ["member"; "overridemem"; "intfmem"])]);
         ("member set_InterfacePropertySet",
          [("file1", ((35, 20), (35, 40)), ["defn"], ["member"; "overridemem"; "intfmem"])]);
         ("CFooImpl", [("file1", ((40, 5), (40, 13)), ["defn"], ["class"])]);
         ("member .ctor", [("file1", ((40, 5), (40, 13)), ["defn"], ["member"; "ctor"])]);
         ("member AbstractClassMethod",
          [("file1", ((45, 18), (45, 37)), ["defn"], ["member"; "overridemem"])]);
         ("member add_AbstractClassEvent",
          [("file1", ((47, 18), (47, 36)), ["defn"], ["member"; "add"; "overridemem"])]);
         ("member get_AbstractClassEvent",
          [("file1", ((47, 18), (47, 36)), ["defn"], ["member"; "getter"; "overridemem"])]);
         ("member get_AbstractClassProperty",
          [("file1", ((43, 18), (43, 39)), ["defn"], ["member"; "getter"; "overridemem"])]);
         ("member remove_AbstractClassEvent",
          [("file1", ((47, 18), (47, 36)), ["defn"], ["member"; "remove"; "overridemem"])]);
         ("member set_AbstractClassPropertySet",
          [("file1", ((44, 18), (44, 42)), ["defn"], ["member"; "setter"; "overridemem"])]);
         ("property AbstractClassPropertySet",
          [("file1", ((44, 18), (44, 42)), ["defn"], ["member"; "prop"; "overridemem"])]);
         ("property AbstractClassProperty",
          [("file1", ((43, 18), (43, 39)), ["defn"], ["member"; "prop"; "overridemem"])]);
         ("property AbstractClassEvent",
          [("file1", ((47, 18), (47, 36)), ["defn"], ["member"; "prop"; "clievent"; "overridemem"])]);
         ("CBaseFooImpl", [("file1", ((49, 5), (49, 17)), ["defn"], ["class"])]);
         ("member .ctor", [("file1", ((49, 5), (49, 17)), ["defn"], ["member"; "ctor"])]);
         ("member BaseClassMethod",
          [("file1", ((54, 18), (54, 33)), ["defn"], ["member"; "overridemem"])]);
         ("member add_BaseClassEvent",
          [("file1", ((56, 18), (56, 32)), ["defn"], ["member"; "add"; "overridemem"])]);
         ("member get_BaseClassEvent",
          [("file1", ((56, 18), (56, 32)), ["defn"], ["member"; "getter"; "overridemem"])]);
         ("member get_BaseClassProperty",
          [("file1", ((52, 18), (52, 35)), ["defn"], ["member"; "getter"; "overridemem"])]);
         ("member remove_BaseClassEvent",
          [("file1", ((56, 18), (56, 32)), ["defn"], ["member"; "remove"; "overridemem"])]);
         ("member set_BaseClassPropertySet",
          [("file1", ((53, 18), (53, 38)), ["defn"], ["member"; "setter"; "overridemem"])]);
         ("property BaseClassPropertySet",
          [("file1", ((53, 18), (53, 38)), ["defn"], ["member"; "prop"; "overridemem"])]);
         ("property BaseClassProperty",
          [("file1", ((52, 18), (52, 35)), ["defn"], ["member"; "prop"; "overridemem"])]);
         ("property BaseClassEvent",
          [("file1", ((56, 18), (56, 32)), ["defn"], ["member"; "prop"; "clievent"; "overridemem"])])]
    set allUsesOfAllSymbols - set expected |> shouldEqual Set.empty
    set expected - set allUsesOfAllSymbols |> shouldEqual Set.empty
    (set expected = set allUsesOfAllSymbols) |> shouldEqual true

//-----------------------------------------------------------------------------------------

module internal Project4 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module M

type Foo<'T>(x : 'T, y : Foo<'T>) = class end

let inline twice(x : ^U, y : ^U) = x + y
    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)




[<Test>]
let ``Test project4 whole project errors`` () =
    let wholeProjectResults = checker.ParseAndCheckProject(Project4.options) |> Async.RunImmediate
    wholeProjectResults .Diagnostics.Length |> shouldEqual 0


[<Test>]
let ``Test project4 basic`` () =
    let wholeProjectResults = checker.ParseAndCheckProject(Project4.options) |> Async.RunImmediate

    set [ for x in wholeProjectResults.AssemblySignature.Entities -> x.DisplayName ] |> shouldEqual (set ["M"])

    [ for x in wholeProjectResults.AssemblySignature.Entities.[0].NestedEntities -> x.DisplayName ]
        |> shouldEqual ["Foo"]

    [ for x in wholeProjectResults.AssemblySignature.Entities.[0].MembersFunctionsAndValues -> x.DisplayName ]
        |> shouldEqual ["twice"]

[<Test>]
let ``Test project4 all symbols in signature`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project4.options) |> Async.RunImmediate
    let allSymbols = allSymbolsInEntities false wholeProjectResults.AssemblySignature.Entities
    [ for x in allSymbols -> x.ToString() ]
      |> shouldEqual
              ["M"; "val twice"; "generic parameter U"; "Foo`1"; "generic parameter T";
               "member .ctor"]


[<Test>]
let ``Test project4 all uses of all signature symbols`` () =
    let wholeProjectResults = checker.ParseAndCheckProject(Project4.options) |> Async.RunImmediate
    let allSymbols = allSymbolsInEntities false wholeProjectResults.AssemblySignature.Entities
    let allUsesOfAllSymbols =
        [ for s in allSymbols do
             let uses = [ for s in wholeProjectResults.GetUsesOfSymbol(s) -> (if s.FileName = Project4.fileName1 then "file1" else "??"), tupsZ s.Range ]
             yield s.ToString(), uses ]
    let expected =
      [("M", [("file1", ((1, 7), (1, 8)))]);
       ("val twice", [("file1", ((5, 11), (5, 16)))]);
       ("generic parameter U",
        [("file1", ((5, 21), (5, 23))); ("file1", ((5, 29), (5, 31)))]);
       ("Foo`1", [("file1", ((3, 5), (3, 8))); ("file1", ((3, 25), (3, 28)))]);
       ("generic parameter T",
        [("file1", ((3, 9), (3, 11))); ("file1", ((3, 17), (3, 19)));
         ("file1", ((3, 29), (3, 31)))]);
       ("member .ctor",
        [("file1", ((3, 5), (3, 8))); ("file1", ((3, 25), (3, 28)))])]

    set allUsesOfAllSymbols - set expected |> shouldEqual Set.empty
    set expected - set allUsesOfAllSymbols |> shouldEqual Set.empty
    (set expected = set allUsesOfAllSymbols) |> shouldEqual true

[<Test>]
let ``Test project4 T symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project4.options) |> Async.RunImmediate
    let backgroundParseResults1, backgroundTypedParse1 =
        checker.GetBackgroundCheckResultsForFileInProject(Project4.fileName1, Project4.options)
        |> Async.RunImmediate

    let tSymbolUse2 = backgroundTypedParse1.GetSymbolUseAtLocation(4,19,"",["T"])
    tSymbolUse2.IsSome |> shouldEqual true
    let tSymbol2 = tSymbolUse2.Value.Symbol
    tSymbol2.ToString() |> shouldEqual "generic parameter T"

    tSymbol2.ImplementationLocation.IsSome |> shouldEqual true

    let uses = backgroundTypedParse1.GetAllUsesOfAllSymbolsInFile()
    let allUsesOfAllSymbols =
        [ for s in uses -> s.Symbol.ToString(), (if s.FileName = Project4.fileName1 then "file1" else "??"), tupsZ s.Range ]
    allUsesOfAllSymbols |> shouldEqual
          [("generic parameter T", "file1", ((3, 9), (3, 11)));
           ("Foo`1", "file1", ((3, 5), (3, 8)));
           ("generic parameter T", "file1", ((3, 17), (3, 19)));
           ("Foo`1", "file1", ((3, 25), (3, 28)));
           ("generic parameter T", "file1", ((3, 29), (3, 31)));
           ("val y", "file1", ((3, 21), (3, 22)));
           ("val x", "file1", ((3, 13), (3, 14)));
           ("member .ctor", "file1", ((3, 5), (3, 8)));
           ("generic parameter U", "file1", ((5, 21), (5, 23)));
           ("generic parameter U", "file1", ((5, 29), (5, 31)));
           ("val y", "file1", ((5, 25), (5, 26)));
           ("val x", "file1", ((5, 17), (5, 18)));
           ("val op_Addition", "file1", ((5, 37), (5, 38)));
           ("val x", "file1", ((5, 35), (5, 36)));
           ("val y", "file1", ((5, 39), (5, 40)));
           ("val twice", "file1", ((5, 11), (5, 16)));
           ("M", "file1", ((1, 7), (1, 8)))]

    let tSymbolUse3 = backgroundTypedParse1.GetSymbolUseAtLocation(4,11,"",["T"])
    tSymbolUse3.IsSome |> shouldEqual true
    let tSymbol3 = tSymbolUse3.Value.Symbol
    tSymbol3.ToString() |> shouldEqual "generic parameter T"

    tSymbol3.ImplementationLocation.IsSome |> shouldEqual true

    let usesOfTSymbol2 =
        wholeProjectResults.GetUsesOfSymbol(tSymbol2)
        |> Array.map (fun su -> su.FileName , tupsZ su.Range)
        |> Array.map (fun (a,b) -> (if a = Project4.fileName1 then "file1" else "??"), b)

    usesOfTSymbol2 |> shouldEqual
          [|("file1", ((3, 9), (3, 11))); ("file1", ((3, 17), (3, 19)));
            ("file1", ((3, 29), (3, 31)))|]

    let usesOfTSymbol3 =
        wholeProjectResults.GetUsesOfSymbol(tSymbol3)

        |> Array.map (fun su -> su.FileName , tupsZ su.Range)
        |> Array.map (fun (a,b) -> (if a = Project4.fileName1 then "file1" else "??"), b)

    usesOfTSymbol3 |> shouldEqual usesOfTSymbol2

    let uSymbolUse2 = backgroundTypedParse1.GetSymbolUseAtLocation(6,23,"",["U"])
    uSymbolUse2.IsSome |> shouldEqual true
    let uSymbol2 = uSymbolUse2.Value.Symbol
    uSymbol2.ToString() |> shouldEqual "generic parameter U"

    uSymbol2.ImplementationLocation.IsSome |> shouldEqual true

    let usesOfUSymbol2 =
        wholeProjectResults.GetUsesOfSymbol(uSymbol2)

        |> Array.map (fun su -> su.FileName , tupsZ su.Range)
        |> Array.map (fun (a,b) -> (if a = Project4.fileName1 then "file1" else "??"), b)

    usesOfUSymbol2 |> shouldEqual  [|("file1", ((5, 21), (5, 23))); ("file1", ((5, 29), (5, 31)))|]

//-----------------------------------------------------------------------------------------


module internal Project5 =


    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module ActivePatterns

///Total active pattern for even/odd integers
let (|Even|Odd|) input = if input % 2 = 0 then Even else Odd


let TestNumber input =
   match input with
   | Even -> printfn "%d is even" input
   | Odd -> printfn "%d is odd" input

///Partial active pattern for floats
let (|Float|_|) (str: string) =
   let mutable floatvalue = 0.0
   if System.Double.TryParse(str, &floatvalue) then Some(floatvalue)
   else None


let parseNumeric str =
   match str with
   | Float f -> printfn "%f : Floating point" f
   | _ -> printfn "%s : Not matched." str
    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)


[<Test>]
let ``Test project5 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project5.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project5 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0


[<Test>]
let ``Test project 5 all symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project5.options) |> Async.RunImmediate

    let allUsesOfAllSymbols =
        wholeProjectResults.GetAllUsesOfAllSymbols()

        |> Array.map (fun su -> su.Symbol.ToString(), su.Symbol.FullName, Project5.cleanFileName su.FileName, tupsZ su.Range, attribsOfSymbolUse su)

    allUsesOfAllSymbols |> shouldEqual
          [|("symbol ", "Even", "file1", ((4, 6), (4, 10)), ["defn"]);
            ("symbol ", "Odd", "file1", ((4, 11), (4, 14)), ["defn"]);
            ("val input", "input", "file1", ((4, 17), (4, 22)), ["defn"]);
            ("val op_Equality", "Microsoft.FSharp.Core.Operators.( = )", "file1",
             ((4, 38), (4, 39)), []);
            ("val op_Modulus", "Microsoft.FSharp.Core.Operators.( % )", "file1",
             ((4, 34), (4, 35)), []);
            ("val input", "input", "file1", ((4, 28), (4, 33)), []);
            ("symbol ", "Even", "file1", ((4, 47), (4, 51)), ["defn"]);
            ("symbol ", "Odd", "file1", ((4, 57), (4, 60)), ["defn"]);
            ("val |Even|Odd|", "ActivePatterns.( |Even|Odd| )", "file1", ((4, 5), (4, 15)),
             ["defn"]); ("val input", "input", "file1", ((7, 15), (7, 20)), ["defn"]);
            ("val input", "input", "file1", ((8, 9), (8, 14)), []);
            ("symbol Even", "ActivePatterns.( |Even|Odd| ).Even", "file1",
             ((9, 5), (9, 9)), ["pattern"]);
            ("val printfn", "Microsoft.FSharp.Core.ExtraTopLevelOperators.printfn",
             "file1", ((9, 13), (9, 20)), []);
            ("val input", "input", "file1", ((9, 34), (9, 39)), []);
            ("symbol Odd", "ActivePatterns.( |Even|Odd| ).Odd", "file1",
             ((10, 5), (10, 8)), ["pattern"]);
            ("val printfn", "Microsoft.FSharp.Core.ExtraTopLevelOperators.printfn",
             "file1", ((10, 12), (10, 19)), []);
            ("val input", "input", "file1", ((10, 32), (10, 37)), []);
            ("val TestNumber", "ActivePatterns.TestNumber", "file1", ((7, 4), (7, 14)),
             ["defn"]); ("symbol ", "Float", "file1", ((13, 6), (13, 11)), ["defn"]);
            ("string", "Microsoft.FSharp.Core.string", "file1", ((13, 22), (13, 28)),
             ["type"]); ("val str", "str", "file1", ((13, 17), (13, 20)), ["defn"]);
            ("val floatvalue", "floatvalue", "file1", ((14, 15), (14, 25)), ["defn"]);
            ("System", "System", "file1", ((15, 6), (15, 12)), []);
            ("Double", "System.Double", "file1", ((15, 13), (15, 19)), []);
            ("val str", "str", "file1", ((15, 29), (15, 32)), []);
            ("val op_AddressOf",
             "Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicOperators.( ~& )", "file1",
             ((15, 34), (15, 35)), []);
            ("val floatvalue", "floatvalue", "file1", ((15, 35), (15, 45)), []);
            ("member TryParse", "System.Double.TryParse", "file1", ((15, 6), (15, 28)), []);
            ("Some", "Microsoft.FSharp.Core.Option<_>.Some", "file1", ((15, 52), (15, 56)),
             []); ("val floatvalue", "floatvalue", "file1", ((15, 57), (15, 67)), []);
            ("None", "Microsoft.FSharp.Core.Option<_>.None", "file1", ((16, 8), (16, 12)),
             []);
            ("val |Float|_|", "ActivePatterns.( |Float|_| )", "file1", ((13, 5), (13, 14)),
             ["defn"]); ("val str", "str", "file1", ((19, 17), (19, 20)), ["defn"]);
            ("val str", "str", "file1", ((20, 9), (20, 12)), []);
            ("symbol Float", "ActivePatterns.( |Float|_| ).Float", "file1",
             ((21, 5), (21, 10)), ["pattern"]);
            ("val f", "f", "file1", ((21, 11), (21, 12)), ["defn"]);
            ("val printfn", "Microsoft.FSharp.Core.ExtraTopLevelOperators.printfn",
             "file1", ((21, 16), (21, 23)), []);
            ("val f", "f", "file1", ((21, 46), (21, 47)), []);
            ("val printfn", "Microsoft.FSharp.Core.ExtraTopLevelOperators.printfn",
             "file1", ((22, 10), (22, 17)), []);
            ("val str", "str", "file1", ((22, 38), (22, 41)), []);
            ("val parseNumeric", "ActivePatterns.parseNumeric", "file1",
             ((19, 4), (19, 16)), ["defn"]);
            ("ActivePatterns", "ActivePatterns", "file1", ((1, 7), (1, 21)), ["defn"])|]

[<Test>]
let ``Test complete active patterns' exact ranges from uses of symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project5.options) |> Async.RunImmediate
    let backgroundParseResults1, backgroundTypedParse1 =
        checker.GetBackgroundCheckResultsForFileInProject(Project5.fileName1, Project5.options)
        |> Async.RunImmediate

    let oddSymbolUse = backgroundTypedParse1.GetSymbolUseAtLocation(11,8,"",["Odd"])
    oddSymbolUse.IsSome |> shouldEqual true
    let oddSymbol = oddSymbolUse.Value.Symbol
    oddSymbol.ToString() |> shouldEqual "symbol Odd"

    let oddActivePatternCase = oddSymbol :?> FSharpActivePatternCase
    match oddActivePatternCase.XmlDoc with
    | FSharpXmlDoc.FromXmlText t -> t.UnprocessedLines |> shouldEqual [| "Total active pattern for even/odd integers" |]
    | _ -> failwith "wrong kind"
    match oddActivePatternCase.XmlDoc with
    | FSharpXmlDoc.FromXmlText t -> t.GetElaboratedXmlLines() |> shouldEqual [|"<summary>"; "Total active pattern for even/odd integers"; "</summary>" |]
    | _ -> failwith "wrong kind"
    oddActivePatternCase.XmlDocSig |> shouldEqual "M:ActivePatterns.|Even|Odd|(System.Int32)"
    let oddGroup = oddActivePatternCase.Group
    oddGroup.IsTotal |> shouldEqual true
    oddGroup.Names |> Seq.toList |> shouldEqual ["Even"; "Odd"]
    oddGroup.OverallType.Format(oddSymbolUse.Value.DisplayContext) |> shouldEqual "int -> Choice<unit,unit>"
    let oddEntity = oddGroup.DeclaringEntity.Value
    oddEntity.ToString() |> shouldEqual "ActivePatterns"

    let evenSymbolUse = backgroundTypedParse1.GetSymbolUseAtLocation(10,9,"",["Even"])
    evenSymbolUse.IsSome |> shouldEqual true
    let evenSymbol = evenSymbolUse.Value.Symbol
    evenSymbol.ToString() |> shouldEqual "symbol Even"
    let evenActivePatternCase = evenSymbol :?> FSharpActivePatternCase
    match evenActivePatternCase.XmlDoc with
    | FSharpXmlDoc.FromXmlText t ->
        t.UnprocessedLines |> shouldEqual [| "Total active pattern for even/odd integers" |]
        t.GetElaboratedXmlLines() |> shouldEqual [| "<summary>"; "Total active pattern for even/odd integers"; "</summary>" |]
    | _ -> failwith "wrong kind"
    evenActivePatternCase.XmlDocSig |> shouldEqual "M:ActivePatterns.|Even|Odd|(System.Int32)"
    let evenGroup = evenActivePatternCase.Group
    evenGroup.IsTotal |> shouldEqual true
    evenGroup.Names |> Seq.toList |> shouldEqual ["Even"; "Odd"]
    evenGroup.OverallType.Format(evenSymbolUse.Value.DisplayContext) |> shouldEqual "int -> Choice<unit,unit>"
    let evenEntity = evenGroup.DeclaringEntity.Value
    evenEntity.ToString() |> shouldEqual "ActivePatterns"

    let usesOfEvenSymbol =
        wholeProjectResults.GetUsesOfSymbol(evenSymbol)

        |> Array.map (fun su -> su.Symbol.ToString(), Project5.cleanFileName su.FileName, tupsZ su.Range)

    let usesOfOddSymbol =
        wholeProjectResults.GetUsesOfSymbol(oddSymbol)

        |> Array.map (fun su -> su.Symbol.ToString(), Project5.cleanFileName su.FileName, tupsZ su.Range)

    usesOfEvenSymbol |> shouldEqual
          [|("symbol Even", "file1", ((4, 6), (4, 10)));
            ("symbol Even", "file1", ((4, 47), (4, 51)));
            ("symbol Even", "file1", ((9, 5), (9, 9)))|]

    usesOfOddSymbol |> shouldEqual
          [|("symbol Odd", "file1", ((4, 11), (4, 14)));
            ("symbol Odd", "file1", ((4, 57), (4, 60)));
            ("symbol Odd", "file1", ((10, 5), (10, 8)))|]


[<Test>]
let ``Test partial active patterns' exact ranges from uses of symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project5.options) |> Async.RunImmediate
    let backgroundParseResults1, backgroundTypedParse1 =
        checker.GetBackgroundCheckResultsForFileInProject(Project5.fileName1, Project5.options)
        |> Async.RunImmediate

    let floatSymbolUse = backgroundTypedParse1.GetSymbolUseAtLocation(22,10,"",["Float"])
    floatSymbolUse.IsSome |> shouldEqual true
    let floatSymbol = floatSymbolUse.Value.Symbol
    floatSymbol.ToString() |> shouldEqual "symbol Float"

    let floatActivePatternCase = floatSymbol :?> FSharpActivePatternCase
    match floatActivePatternCase.XmlDoc with
    | FSharpXmlDoc.FromXmlText t ->
        t.UnprocessedLines |> shouldEqual [| "Partial active pattern for floats" |]
        t.GetElaboratedXmlLines() |> shouldEqual [| "<summary>"; "Partial active pattern for floats"; "</summary>" |]
    | _ -> failwith "wrong kind"
    floatActivePatternCase.XmlDocSig |> shouldEqual "M:ActivePatterns.|Float|_|(System.String)"
    let floatGroup = floatActivePatternCase.Group
    floatGroup.IsTotal |> shouldEqual false
    floatGroup.Names |> Seq.toList |> shouldEqual ["Float"]
    floatGroup.OverallType.Format(floatSymbolUse.Value.DisplayContext) |> shouldEqual "string -> float option"
    let evenEntity = floatGroup.DeclaringEntity.Value
    evenEntity.ToString() |> shouldEqual "ActivePatterns"

    let usesOfFloatSymbol =
        wholeProjectResults.GetUsesOfSymbol(floatSymbol)

        |> Array.map (fun su -> su.Symbol.ToString(), Project5.cleanFileName su.FileName, tups su.Range)

    usesOfFloatSymbol |> shouldEqual
          [|("symbol Float", "file1", ((14, 6), (14, 11)));
            ("symbol Float", "file1", ((22, 5), (22, 10)))|]

    // Should also return its definition
    let floatSymUseOpt =
        backgroundTypedParse1.GetSymbolUseAtLocation(14,11,"",["Float"])


    floatSymUseOpt.IsSome |> shouldEqual true

//-----------------------------------------------------------------------------------------

module internal Project6 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module Exceptions

exception Fail of string

let f () =
   raise (Fail "unknown")
    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)


[<Test>]
let ``Test project6 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project6.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project6 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0


[<Test>]
let ``Test project 6 all symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project6.options) |> Async.RunImmediate

    let allUsesOfAllSymbols =
        wholeProjectResults.GetAllUsesOfAllSymbols()

        |> Array.map (fun su -> su.Symbol.ToString(), Project6.cleanFileName su.FileName, tupsZ su.Range, attribsOfSymbol su.Symbol)

    allUsesOfAllSymbols |> shouldEqual
          [|("string", "file1", ((3, 18), (3, 24)), ["abbrev"]);
            ("Fail", "file1", ((3, 10), (3, 14)), ["exn"]);
            ("val raise", "file1", ((6, 3), (6, 8)), ["val"]);
            ("Fail", "file1", ((6, 10), (6, 14)), ["exn"]);
            ("val f", "file1", ((5, 4), (5, 5)), ["val"]);
            ("Exceptions", "file1", ((1, 7), (1, 17)), ["module"])|]


//-----------------------------------------------------------------------------------------

module internal Project7 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module NamedArgs

type C() =
    static member M(arg1: int, arg2: int, ?arg3 : int) = arg1 + arg2 + defaultArg arg3 4

let x1 = C.M(arg1 = 3, arg2 = 4, arg3 = 5)

let x2 = C.M(arg1 = 3, arg2 = 4, ?arg3 = Some 5)

    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)


[<Test>]
let ``Test project7 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project7.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project7 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0


[<Test>]
let ``Test project 7 all symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project7.options) |> Async.RunImmediate

    let allUsesOfAllSymbols =
        wholeProjectResults.GetAllUsesOfAllSymbols()

        |> Array.map (fun su -> su.Symbol.ToString(), su.Symbol.DisplayName, Project7.cleanFileName su.FileName, tups su.Range)

    let arg1symbol =
        wholeProjectResults.GetAllUsesOfAllSymbols()

        |> Array.pick (fun x -> if x.Symbol.DisplayName = "arg1" then Some x.Symbol else None)
    let arg1uses =
        wholeProjectResults.GetUsesOfSymbol(arg1symbol)

        |> Array.map (fun su -> su.Symbol.ToString(), Option.map tups su.Symbol.DeclarationLocation, Project7.cleanFileName su.FileName, tups su.Range, attribsOfSymbol su.Symbol)
    arg1uses |> shouldEqual
          [|("val arg1", Some ((5, 20), (5, 24)), "file1", ((5, 20), (5, 24)), []);
            ("val arg1", Some ((5, 20), (5, 24)), "file1", ((5, 57), (5, 61)), []);
            ("val arg1", Some ((5, 20), (5, 24)), "file1", ((7, 13), (7, 17)), []);
            ("val arg1", Some ((5, 20), (5, 24)), "file1", ((9, 13), (9, 17)), [])|]


//-----------------------------------------------------------------------------------------
module internal Project8 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module NamedUnionFields

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


[<Test>]
let ``Test project8 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project8.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project8 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0


[<Test>]
let ``Test project 8 all symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project8.options) |> Async.RunImmediate

    let allUsesOfAllSymbols =
        wholeProjectResults.GetAllUsesOfAllSymbols()

        |> Array.map (fun su -> su.Symbol.ToString(), su.Symbol.DisplayName, Project8.cleanFileName su.FileName, tups su.Range, attribsOfSymbolUse su, attribsOfSymbol su.Symbol)

    allUsesOfAllSymbols
      |> shouldEqual
              [|("int", "int", "file1", ((4, 19), (4, 22)), ["type"], ["abbrev"]);
                ("int", "int", "file1", ((4, 31), (4, 34)), ["type"], ["abbrev"]);
                ("field xxx", "xxx", "file1", ((4, 14), (4, 17)), ["defn"], ["field"]);
                ("int", "int", "file1", ((4, 19), (4, 22)), ["type"], ["abbrev"]);
                ("field yyy", "yyy", "file1", ((4, 25), (4, 28)), ["defn"], ["field"]);
                ("int", "int", "file1", ((4, 31), (4, 34)), ["type"], ["abbrev"]);
                ("B", "B", "file1", ((4, 9), (4, 10)), ["defn"], []);
                ("A", "A", "file1", ((4, 5), (4, 6)), ["defn"], ["union"]);
                ("B", "B", "file1", ((5, 8), (5, 9)), [], []);
                ("field xxx", "xxx", "file1", ((5, 10), (5, 13)), [], ["field"]);
                ("field yyy", "yyy", "file1", ((5, 17), (5, 20)), [], ["field"]);
                ("val b", "b", "file1", ((5, 4), (5, 5)), ["defn"], ["val"]);
                ("val b", "b", "file1", ((8, 10), (8, 11)), [], ["val"]);
                ("B", "B", "file1", ((10, 6), (10, 7)), ["pattern"], []);
                ("field xxx", "xxx", "file1", ((10, 9), (10, 12)), ["pattern"], ["field"]);
                ("field yyy", "yyy", "file1", ((10, 18), (10, 21)), ["pattern"], ["field"]);
                ("val b", "b", "file1", ((10, 24), (10, 25)), ["defn"], []);
                ("val a", "a", "file1", ((10, 15), (10, 16)), ["defn"], []);
                ("val x", "x", "file1", ((7, 4), (7, 5)), ["defn"], ["val"]);
                ("NamedUnionFields", "NamedUnionFields", "file1", ((2, 7), (2, 23)),
                 ["defn"], ["module"])|]

    let arg1symbol =
        wholeProjectResults.GetAllUsesOfAllSymbols()

        |> Array.pick (fun x -> if x.Symbol.DisplayName = "xxx" then Some x.Symbol else None)
    let arg1uses =
        wholeProjectResults.GetUsesOfSymbol(arg1symbol)

        |> Array.map (fun su -> Option.map tups su.Symbol.DeclarationLocation, Project8.cleanFileName su.FileName, tups su.Range)

    arg1uses |> shouldEqual
     [|(Some ((4, 14), (4, 17)), "file1", ((4, 14), (4, 17)));
       (Some ((4, 14), (4, 17)), "file1", ((5, 10), (5, 13)));
       (Some ((4, 14), (4, 17)), "file1", ((10, 9), (10, 12)))|]

//-----------------------------------------------------------------------------------------
module internal Project9 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module Constraints

let inline check< ^T when ^T : (static member IsInfinity : ^T -> bool)> (num: ^T) : ^T option =
    if (^T : (static member IsInfinity: ^T -> bool) (num)) then None
    else Some num
    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)


[<Test>]
let ``Test project9 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project9.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project9 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0


[<Test>]
let ``Test project 9 all symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project9.options) |> Async.RunImmediate

    let allUsesOfAllSymbols =
        wholeProjectResults.GetAllUsesOfAllSymbols()

        |> Array.map (fun su -> su.Symbol.ToString(), su.Symbol.DisplayName, Project9.cleanFileName su.FileName, tups su.Range, attribsOfSymbol su.Symbol)

    allUsesOfAllSymbols |> shouldEqual
          [|("generic parameter T", "T", "file1", ((4, 18), (4, 20)), []);
            ("generic parameter T", "T", "file1", ((4, 26), (4, 28)), []);
            ("generic parameter T", "T", "file1", ((4, 59), (4, 61)), []);
            ("bool", "bool", "file1", ((4, 65), (4, 69)), ["abbrev"]);
            ("parameter IsInfinity", "IsInfinity", "file1", ((4, 46), (4, 56)), []);
            ("generic parameter T", "T", "file1", ((4, 78), (4, 80)), []);
            ("val num", "num", "file1", ((4, 73), (4, 76)), []);
            ("option`1", "option", "file1", ((4, 87), (4, 93)), ["abbrev"]);
            ("generic parameter T", "T", "file1", ((4, 84), (4, 86)), []);
            ("generic parameter T", "T", "file1", ((5, 8), (5, 10)), []);
            ("generic parameter T", "T", "file1", ((5, 40), (5, 42)), []);
            ("bool", "bool", "file1", ((5, 46), (5, 50)), ["abbrev"]);
            ("parameter IsInfinity", "IsInfinity", "file1", ((5, 28), (5, 38)), []);
            ("val num", "num", "file1", ((5, 53), (5, 56)), []);
            ("None", "None", "file1", ((5, 64), (5, 68)), []);
            ("Some", "Some", "file1", ((6, 9), (6, 13)), []);
            ("val num", "num", "file1", ((6, 14), (6, 17)), []);
            ("val check", "check", "file1", ((4, 11), (4, 16)), ["val"]);
            ("Constraints", "Constraints", "file1", ((2, 7), (2, 18)), ["module"])|]

    let arg1symbol =
        wholeProjectResults.GetAllUsesOfAllSymbols()

        |> Array.pick (fun x -> if x.Symbol.DisplayName = "IsInfinity" then Some x.Symbol else None)
    let arg1uses =
        wholeProjectResults.GetUsesOfSymbol(arg1symbol)

        |> Array.map (fun su -> Option.map tups su.Symbol.DeclarationLocation, Project9.cleanFileName su.FileName, tups su.Range)

    arg1uses |> shouldEqual
     [|(Some ((4, 46), (4, 56)), "file1", ((4, 46), (4, 56)))|]

//-----------------------------------------------------------------------------------------
// see https://github.com/fsharp/FSharp.Compiler.Service/issues/95

module internal Project10 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module NamedArgs

type C() =
    static member M(url: string, query: int)  = ()

C.M("http://goo", query = 1)

    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)


[<Test>]
let ``Test Project10 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project10.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project10 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0


[<Test>]
let ``Test Project10 all symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project10.options) |> Async.RunImmediate

    let allUsesOfAllSymbols =
        wholeProjectResults.GetAllUsesOfAllSymbols()

        |> Array.map (fun su -> su.Symbol.ToString(), su.Symbol.DisplayName, Project10.cleanFileName su.FileName, tups su.Range, attribsOfSymbol su.Symbol)

    allUsesOfAllSymbols |> shouldEqual
          [|("C", "C", "file1", ((4, 5), (4, 6)), ["class"]);
            ("member .ctor", "( .ctor )", "file1", ((4, 5), (4, 6)),
             ["member"; "ctor"]);
            ("string", "string", "file1", ((5, 25), (5, 31)), ["abbrev"]);
            ("int", "int", "file1", ((5, 40), (5, 43)), ["abbrev"]);
            ("member M", "M", "file1", ((5, 18), (5, 19)), ["member"]);
            ("string", "string", "file1", ((5, 25), (5, 31)), ["abbrev"]);
            ("int", "int", "file1", ((5, 40), (5, 43)), ["abbrev"]);
            ("val url", "url", "file1", ((5, 20), (5, 23)), []);
            ("val query", "query", "file1", ((5, 33), (5, 38)), []);
            ("C", "C", "file1", ((7, 0), (7, 1)), ["class"]);
            ("member M", "M", "file1", ((7, 0), (7, 3)), ["member"]);
            ("parameter query", "query", "file1", ((7, 18), (7, 23)), []);
            ("NamedArgs", "NamedArgs", "file1", ((2, 7), (2, 16)), ["module"])|]

    let backgroundParseResults1, backgroundTypedParse1 =
        checker.GetBackgroundCheckResultsForFileInProject(Project10.fileName1, Project10.options)
        |> Async.RunImmediate

    let querySymbolUseOpt =
        backgroundTypedParse1.GetSymbolUseAtLocation(7,23,"",["query"])


    let querySymbolUse = querySymbolUseOpt.Value
    let querySymbol = querySymbolUse.Symbol
    querySymbol.ToString() |> shouldEqual "parameter query"

    let querySymbolUse2Opt =
        backgroundTypedParse1.GetSymbolUseAtLocation(7,22,"",["query"])


    let querySymbolUse2 = querySymbolUse2Opt.Value
    let querySymbol2 = querySymbolUse2.Symbol
    querySymbol2.ToString() |> shouldEqual "val query" // This is perhaps the wrong result, but not that the input location was wrong - was not the "column at end of names"

//-----------------------------------------------------------------------------------------
// see https://github.com/fsharp/FSharp.Compiler.Service/issues/92

module internal Project11 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module NestedTypes

let enum = new System.Collections.Generic.Dictionary<int,int>.Enumerator()
let fff (x:System.Collections.Generic.Dictionary<int,int>.Enumerator) = ()

    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)


[<Test>]
let ``Test Project11 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project11.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project11 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0


[<Test>]
let ``Test Project11 all symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project11.options) |> Async.RunImmediate

    let allUsesOfAllSymbols =
        wholeProjectResults.GetAllUsesOfAllSymbols()

        |> Array.map (fun su -> su.Symbol.ToString(), su.Symbol.DisplayName, Project11.cleanFileName su.FileName, tups su.Range, attribsOfSymbolUse su, attribsOfSymbol su.Symbol)

    allUsesOfAllSymbols |> shouldEqual
          [|("System", "System", "file1", ((4, 15), (4, 21)), [], ["namespace"]);
            ("Collections", "Collections", "file1", ((4, 22), (4, 33)), [], ["namespace"]);
            ("Generic", "Generic", "file1", ((4, 34), (4, 41)), [], ["namespace"]);
            ("Dictionary`2", "Dictionary", "file1", ((4, 15), (4, 52)), ["type"],
             ["class"]); ("int", "int", "file1", ((4, 53), (4, 56)), [], ["abbrev"]);
            ("int", "int", "file1", ((4, 57), (4, 60)), [], ["abbrev"]);
            ("Enumerator", "Enumerator", "file1", ((4, 62), (4, 72)), ["type"],
             ["valuetype"]);
            ("member .ctor", "Enumerator", "file1", ((4, 15), (4, 72)), [], ["member"]);
            ("val enum", "enum", "file1", ((4, 4), (4, 8)), ["defn"], ["val"]);
            ("System", "System", "file1", ((5, 11), (5, 17)), [], ["namespace"]);
            ("Collections", "Collections", "file1", ((5, 18), (5, 29)), [], ["namespace"]);
            ("Generic", "Generic", "file1", ((5, 30), (5, 37)), [], ["namespace"]);
            ("Dictionary`2", "Dictionary", "file1", ((5, 11), (5, 48)), ["type"],
             ["class"]); ("int", "int", "file1", ((5, 49), (5, 52)), ["type"], ["abbrev"]);
            ("int", "int", "file1", ((5, 53), (5, 56)), ["type"], ["abbrev"]);
            ("Enumerator", "Enumerator", "file1", ((5, 58), (5, 68)), ["type"],
             ["valuetype"]); ("val x", "x", "file1", ((5, 9), (5, 10)), ["defn"], []);
            ("val fff", "fff", "file1", ((5, 4), (5, 7)), ["defn"], ["val"]);
            ("NestedTypes", "NestedTypes", "file1", ((2, 7), (2, 18)), ["defn"],
             ["module"])|]

//-----------------------------------------------------------------------------------------
// see https://github.com/fsharp/FSharp.Compiler.Service/issues/92

module internal Project12 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module ComputationExpressions

let x1 = seq { for i in 0 .. 100 -> i }
let x2 = query { for i in 0 .. 100 do
                 where (i = 0)
                 select (i,i) }

    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)


[<Test>]
let ``Test Project12 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project12.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project12 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0


[<Test>]
let ``Test Project12 all symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project12.options) |> Async.RunImmediate

    let allUsesOfAllSymbols =
        wholeProjectResults.GetAllUsesOfAllSymbols()

        |> Array.map (fun su -> su.Symbol.ToString(), su.Symbol.DisplayName, Project12.cleanFileName su.FileName, tups su.Range, attribsOfSymbolUse su, attribsOfSymbol su.Symbol)

    allUsesOfAllSymbols |> shouldEqual
          [|("val seq", "seq", "file1", ((4, 9), (4, 12)), ["compexpr"], ["val"]);
            ("val op_Range", "( .. )", "file1", ((4, 26), (4, 28)), [], ["val"]);
            ("val i", "i", "file1", ((4, 19), (4, 20)), ["defn"], []);
            ("val i", "i", "file1", ((4, 36), (4, 37)), [], []);
            ("val x1", "x1", "file1", ((4, 4), (4, 6)), ["defn"], ["val"]);
            ("val query", "query", "file1", ((5, 9), (5, 14)), [], ["val"]);
            ("val query", "query", "file1", ((5, 9), (5, 14)), ["compexpr"], ["val"]);
            ("member Where", "where", "file1", ((6, 17), (6, 22)), ["compexpr"],
             ["member"]);
            ("member Select", "select", "file1", ((7, 17), (7, 23)), ["compexpr"],
             ["member"]);
            ("val op_Range", "( .. )", "file1", ((5, 28), (5, 30)), [], ["val"]);
            ("val i", "i", "file1", ((5, 21), (5, 22)), ["defn"], []);
            ("val op_Equality", "( = )", "file1", ((6, 26), (6, 27)), [], ["val"]);
            ("val i", "i", "file1", ((6, 24), (6, 25)), [], []);
            ("val i", "i", "file1", ((7, 25), (7, 26)), [], []);
            ("val i", "i", "file1", ((7, 27), (7, 28)), [], []);
            ("val x2", "x2", "file1", ((5, 4), (5, 6)), ["defn"], ["val"]);
            ("ComputationExpressions", "ComputationExpressions", "file1",
             ((2, 7), (2, 29)), ["defn"], ["module"])|]

//-----------------------------------------------------------------------------------------
// Test fetching information about some external types (e.g. System.Object, System.DateTime)

module internal Project13 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module ExternalTypes

let x1  = new System.Object()
let x2  = new System.DateTime(1,1,1)
let x3 = new System.DateTime()

    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)


[<Test>]
let ``Test Project13 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project13.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project13 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0


[<Test>]
let ``Test Project13 all symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project13.options) |> Async.RunImmediate

    let allUsesOfAllSymbols =
        wholeProjectResults.GetAllUsesOfAllSymbols()

        |> Array.map (fun su -> su.Symbol.ToString(), su.Symbol.DisplayName, Project13.cleanFileName su.FileName, tups su.Range, attribsOfSymbolUse su, attribsOfSymbol su.Symbol)

    allUsesOfAllSymbols |> shouldEqual
          [|("System", "System", "file1", ((4, 14), (4, 20)), [], ["namespace"]);
            ("Object", "Object", "file1", ((4, 14), (4, 27)), [], ["class"]);
            ("member .ctor", "Object", "file1", ((4, 14), (4, 27)), [], ["member"]);
            ("val x1", "x1", "file1", ((4, 4), (4, 6)), ["defn"], ["val"]);
            ("System", "System", "file1", ((5, 14), (5, 20)), [], ["namespace"]);
            ("DateTime", "DateTime", "file1", ((5, 14), (5, 29)), [], ["valuetype"]);
            ("member .ctor", "DateTime", "file1", ((5, 14), (5, 29)), [], ["member"]);
            ("val x2", "x2", "file1", ((5, 4), (5, 6)), ["defn"], ["val"]);
            ("System", "System", "file1", ((6, 13), (6, 19)), [], ["namespace"]);
            ("DateTime", "DateTime", "file1", ((6, 13), (6, 28)), [], ["valuetype"]);
            ("member .ctor", "DateTime", "file1", ((6, 13), (6, 28)), [], ["member"]);
            ("val x3", "x3", "file1", ((6, 4), (6, 6)), ["defn"], ["val"]);
            ("ExternalTypes", "ExternalTypes", "file1", ((2, 7), (2, 20)), ["defn"],
             ["module"])|]

    let objSymbol = wholeProjectResults.GetAllUsesOfAllSymbols() |> Array.find (fun su -> su.Symbol.DisplayName = "Object")
    let objEntity = objSymbol.Symbol :?> FSharpEntity
    let objMemberNames = [ for x in objEntity.MembersFunctionsAndValues -> x.DisplayName ]
    set objMemberNames |> shouldEqual (set [".ctor"; "ToString"; "Equals"; "Equals"; "ReferenceEquals"; "GetHashCode"; "GetType"; "Finalize"; "MemberwiseClone"])

    let dtSymbol = wholeProjectResults.GetAllUsesOfAllSymbols() |> Array.find (fun su -> su.Symbol.DisplayName = "DateTime")
    let dtEntity = dtSymbol.Symbol :?> FSharpEntity
    let dtPropNames = [ for x in dtEntity.MembersFunctionsAndValues do if x.IsProperty then yield x.DisplayName ]

    let dtType = dtSymbol.Symbol:?> FSharpEntity

    set [ for i in dtType.DeclaredInterfaces -> i.ToString() ] |> shouldEqual
        (set
          ["type System.IComparable";
           "type System.IFormattable";
           "type System.IConvertible";
           "type System.Runtime.Serialization.ISerializable";
           "type System.IComparable<System.DateTime>";
           "type System.IEquatable<System.DateTime>"])

    dtType.BaseType.ToString() |> shouldEqual "Some(type System.ValueType)"

    set ["Date"; "Day"; "DayOfWeek"; "DayOfYear"; "Hour"; "Kind"; "Millisecond"; "Minute"; "Month"; "Now"; "Second"; "Ticks"; "TimeOfDay"; "Today"; "Year"]
    - set dtPropNames
      |> shouldEqual (set [])

    let objDispatchSlotNames = [ for x in objEntity.MembersFunctionsAndValues do if x.IsDispatchSlot then yield x.DisplayName ]

    set objDispatchSlotNames |> shouldEqual (set ["ToString"; "Equals"; "GetHashCode"; "Finalize"])

    // check we can get the CurriedParameterGroups
    let objMethodsCurriedParameterGroups =
        [ for x in objEntity.MembersFunctionsAndValues do
             for pg in x.CurriedParameterGroups do
                 for p in pg do
                     yield x.CompiledName, p.Name,  p.Type.ToString(), p.Type.Format(dtSymbol.DisplayContext) ]

    objMethodsCurriedParameterGroups |> shouldEqual
          [("Equals", Some "obj", "type Microsoft.FSharp.Core.obj", "obj");
           ("Equals", Some "objA", "type Microsoft.FSharp.Core.obj", "obj");
           ("Equals", Some "objB", "type Microsoft.FSharp.Core.obj", "obj");
           ("ReferenceEquals", Some "objA", "type Microsoft.FSharp.Core.obj", "obj");
           ("ReferenceEquals", Some "objB", "type Microsoft.FSharp.Core.obj", "obj")]

    // check we can get the ReturnParameter
    let objMethodsReturnParameter =
        [ for x in objEntity.MembersFunctionsAndValues do
             let p = x.ReturnParameter
             yield x.DisplayName, p.Name,  p.Type.ToString(), p.Type.Format(dtSymbol.DisplayContext) ]
    set objMethodsReturnParameter |> shouldEqual
       (set
           [(".ctor", None, "type Microsoft.FSharp.Core.unit", "unit");
            ("ToString", None, "type Microsoft.FSharp.Core.string", "string");
            ("Equals", None, "type Microsoft.FSharp.Core.bool", "bool");
            ("Equals", None, "type Microsoft.FSharp.Core.bool", "bool");
            ("ReferenceEquals", None, "type Microsoft.FSharp.Core.bool", "bool");
            ("GetHashCode", None, "type Microsoft.FSharp.Core.int", "int");
            ("GetType", None, "type System.Type", "System.Type");
            ("Finalize", None, "type Microsoft.FSharp.Core.unit", "unit");
            ("MemberwiseClone", None, "type Microsoft.FSharp.Core.obj", "obj")])

    // check we can get the CurriedParameterGroups
    let dtMethodsCurriedParameterGroups =
        [ for x in dtEntity.MembersFunctionsAndValues do
           if x.CompiledName = "FromFileTime" || x.CompiledName = "AddMilliseconds"  then
             for pg in x.CurriedParameterGroups do
                 for p in pg do
                     yield x.CompiledName, p.Name,  p.Type.ToString(), p.Type.Format(dtSymbol.DisplayContext) ]

    dtMethodsCurriedParameterGroups |> shouldEqual
          [("AddMilliseconds", Some "value", "type Microsoft.FSharp.Core.float","float");
           ("FromFileTime", Some "fileTime", "type Microsoft.FSharp.Core.int64","int64")]


    let _test1 = [ for x in objEntity.MembersFunctionsAndValues -> x.FullType ]
    for x in objEntity.MembersFunctionsAndValues do
       x.IsCompilerGenerated |> shouldEqual false
       x.IsExtensionMember |> shouldEqual false
       x.IsEvent |> shouldEqual false
       x.IsProperty |> shouldEqual false
       x.IsPropertySetterMethod |> shouldEqual false
       x.IsPropertyGetterMethod |> shouldEqual false
       x.IsImplicitConstructor |> shouldEqual false
       x.IsTypeFunction |> shouldEqual false
       x.IsUnresolved |> shouldEqual false
    ()

//-----------------------------------------------------------------------------------------
// Misc - structs

module internal Project14 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module Structs

[<Struct>]
type S(p:int) =
   member x.P = p

let x1  = S()
let x2  = S(3)

    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)


[<Test>]
let ``Test Project14 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project14.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project14 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0


[<Test>]
let ``Test Project14 all symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project14.options) |> Async.RunImmediate

    let allUsesOfAllSymbols =
        wholeProjectResults.GetAllUsesOfAllSymbols()

        |> Array.map (fun su -> su.Symbol.ToString(), su.Symbol.DisplayName, Project14.cleanFileName su.FileName, tups su.Range, attribsOfSymbolUse su)

    allUsesOfAllSymbols |> shouldEqual
          [|("StructAttribute", "StructAttribute", "file1", ((4, 2), (4, 8)),
             ["attribute"]);
            ("StructAttribute", "StructAttribute", "file1", ((4, 2), (4, 8)), ["type"]);
            ("member .ctor", "StructAttribute", "file1", ((4, 2), (4, 8)), []);
            ("int", "int", "file1", ((5, 9), (5, 12)), ["type"]);
            ("int", "int", "file1", ((5, 9), (5, 12)), ["type"]);
            ("S", "S", "file1", ((5, 5), (5, 6)), ["defn"]);
            ("int", "int", "file1", ((5, 9), (5, 12)), ["type"]);
            ("val p", "p", "file1", ((5, 7), (5, 8)), ["defn"]);
            ("member .ctor", "( .ctor )", "file1", ((5, 5), (5, 6)), ["defn"]);
            ("member get_P", "P", "file1", ((6, 12), (6, 13)), ["defn"]);
            ("val x", "x", "file1", ((6, 10), (6, 11)), ["defn"]);
            ("val p", "p", "file1", ((6, 16), (6, 17)), []);
            ("member .ctor", ".ctor", "file1", ((8, 10), (8, 11)), []);
            ("val x1", "x1", "file1", ((8, 4), (8, 6)), ["defn"]);
            ("member .ctor", ".ctor", "file1", ((9, 10), (9, 11)), []);
            ("val x2", "x2", "file1", ((9, 4), (9, 6)), ["defn"]);
            ("Structs", "Structs", "file1", ((2, 7), (2, 14)), ["defn"])|]

//-----------------------------------------------------------------------------------------
// Misc - union patterns

module internal Project15 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module UnionPatterns

let f x =
    match x with
    | [h]
    | [_; h]
    | [_; _; h] -> h
    | _ -> 0

    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)


[<Test>]
let ``Test Project15 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project15.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project15 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0


[<Test>]
let ``Test Project15 all symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project15.options) |> Async.RunImmediate

    let allUsesOfAllSymbols =
        wholeProjectResults.GetAllUsesOfAllSymbols()

        |> Array.map (fun su -> su.Symbol.ToString(), su.Symbol.DisplayName, Project15.cleanFileName su.FileName, tups su.Range, attribsOfSymbolUse su)

    allUsesOfAllSymbols |> shouldEqual
          [|("val x", "x", "file1", ((4, 6), (4, 7)), ["defn"]);
            ("val x", "x", "file1", ((5, 10), (5, 11)), []);
            ("val h", "h", "file1", ((6, 7), (6, 8)), ["defn"]);
            ("val h", "h", "file1", ((7, 10), (7, 11)), ["defn"]);
            ("val h", "h", "file1", ((8, 13), (8, 14)), ["defn"]);
            ("val h", "h", "file1", ((8, 19), (8, 20)), []);
            ("val f", "f", "file1", ((4, 4), (4, 5)), ["defn"]);
            ("UnionPatterns", "UnionPatterns", "file1", ((2, 7), (2, 20)), ["defn"])|]


//-----------------------------------------------------------------------------------------
// Misc - signature files

module internal Project16 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let sigFileName1 = Path.ChangeExtension(fileName1, ".fsi")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1Text = """
module Impl

type C() =
    member x.PC = 1

and D() =
    member x.PD = 1

and E() =
    member x.PE = 1

and F = { Field1 : int; Field2 : int }
and G = Case1 | Case2 of int

    """
    let fileSource1 = SourceText.ofString fileSource1Text
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1Text)

    let sigFileSource1Text = """
module Impl

type C =
    new : unit -> C
    member PC : int

and [<Class>] D =
    new : unit -> D
    member PD : int

and [<Class>] E =
    new : unit -> E
    member PE : int

and F = { Field1 : int; Field2 : int }
and G = Case1 | Case2 of int

    """
    let sigFileSource1 = SourceText.ofString sigFileSource1Text
    FileSystem.OpenFileForWriteShim(sigFileName1).Write(sigFileSource1Text)
    let cleanFileName a = if a = fileName1 then "file1" elif a = sigFileName1 then "sig1"  else "??"

    let fileNames = [sigFileName1; fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)


[<Test>]
let ``Test Project16 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project16.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project16 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0


[<Test>]
let ``Test Project16 all symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project16.options) |> Async.RunImmediate

    let allUsesOfAllSymbols =
        wholeProjectResults.GetAllUsesOfAllSymbols()

        |> Array.map (fun su -> su.Symbol.ToString(), su.Symbol.DisplayName, Project16.cleanFileName su.FileName, tups su.Range, attribsOfSymbolUse su, attribsOfSymbol su.Symbol)

    allUsesOfAllSymbols |> shouldEqual
          [|("ClassAttribute", "ClassAttribute", "sig1", ((8, 6), (8, 11)),
             ["attribute"], ["class"]);
            ("ClassAttribute", "ClassAttribute", "sig1", ((8, 6), (8, 11)), ["type"],
             ["class"]);
            ("member .ctor", "ClassAttribute", "sig1", ((8, 6), (8, 11)), [],
             ["member"]);
            ("ClassAttribute", "ClassAttribute", "sig1", ((12, 6), (12, 11)),
             ["attribute"], ["class"]);
            ("ClassAttribute", "ClassAttribute", "sig1", ((12, 6), (12, 11)), ["type"],
             ["class"]);
            ("member .ctor", "ClassAttribute", "sig1", ((12, 6), (12, 11)), [],
             ["member"]);
            ("int", "int", "sig1", ((16, 19), (16, 22)), ["type"], ["abbrev"]);
            ("int", "int", "sig1", ((16, 33), (16, 36)), ["type"], ["abbrev"]);
            ("int", "int", "sig1", ((17, 25), (17, 28)), ["type"], ["abbrev"]);
            ("int", "int", "sig1", ((16, 19), (16, 22)), ["type"], ["abbrev"]);
            ("int", "int", "sig1", ((16, 33), (16, 36)), ["type"], ["abbrev"]);
            ("field Field1", "Field1", "sig1", ((16, 10), (16, 16)), ["defn"],
             ["field"]);
            ("field Field2", "Field2", "sig1", ((16, 24), (16, 30)), ["defn"],
             ["field"]);
            ("int", "int", "sig1", ((17, 25), (17, 28)), ["type"], ["abbrev"]);
            ("Case1", "Case1", "sig1", ((17, 8), (17, 13)), ["defn"], []);
            ("Case2", "Case2", "sig1", ((17, 16), (17, 21)), ["defn"], []);
            ("C", "C", "sig1", ((4, 5), (4, 6)), ["defn"], ["class"]);
            ("unit", "unit", "sig1", ((5, 10), (5, 14)), ["type"], ["abbrev"]);
            ("C", "C", "sig1", ((5, 18), (5, 19)), ["type"], ["class"]);
            ("member .ctor", "( .ctor )", "sig1", ((5, 4), (5, 7)), ["defn"],
             ["member"]);
            ("int", "int", "sig1", ((6, 16), (6, 19)), ["type"], ["abbrev"]);
            ("member get_PC", "PC", "sig1", ((6, 11), (6, 13)), ["defn"],
             ["member"; "getter"]);
            ("D", "D", "sig1", ((8, 14), (8, 15)), ["defn"], ["class"]);
            ("unit", "unit", "sig1", ((9, 10), (9, 14)), ["type"], ["abbrev"]);
            ("D", "D", "sig1", ((9, 18), (9, 19)), ["type"], ["class"]);
            ("member .ctor", "( .ctor )", "sig1", ((9, 4), (9, 7)), ["defn"],
             ["member"]);
            ("int", "int", "sig1", ((10, 16), (10, 19)), ["type"], ["abbrev"]);
            ("member get_PD", "PD", "sig1", ((10, 11), (10, 13)), ["defn"],
             ["member"; "getter"]);
            ("E", "E", "sig1", ((12, 14), (12, 15)), ["defn"], ["class"]);
            ("unit", "unit", "sig1", ((13, 10), (13, 14)), ["type"], ["abbrev"]);
            ("E", "E", "sig1", ((13, 18), (13, 19)), ["type"], ["class"]);
            ("member .ctor", "( .ctor )", "sig1", ((13, 4), (13, 7)), ["defn"],
             ["member"]);
            ("int", "int", "sig1", ((14, 16), (14, 19)), ["type"], ["abbrev"]);
            ("member get_PE", "PE", "sig1", ((14, 11), (14, 13)), ["defn"],
             ["member"; "getter"]);
            ("F", "F", "sig1", ((16, 4), (16, 5)), ["defn"], ["record"]);
            ("G", "G", "sig1", ((17, 4), (17, 5)), ["defn"], ["union"]);
            ("Impl", "Impl", "sig1", ((2, 7), (2, 11)), ["defn"], ["module"]);
            ("int", "int", "file1", ((13, 19), (13, 22)), ["type"], ["abbrev"]);
            ("int", "int", "file1", ((13, 33), (13, 36)), ["type"], ["abbrev"]);
            ("int", "int", "file1", ((14, 25), (14, 28)), ["type"], ["abbrev"]);
            ("int", "int", "file1", ((13, 19), (13, 22)), ["type"], ["abbrev"]);
            ("int", "int", "file1", ((13, 33), (13, 36)), ["type"], ["abbrev"]);
            ("field Field1", "Field1", "file1", ((13, 10), (13, 16)), ["defn"],
             ["field"]);
            ("field Field2", "Field2", "file1", ((13, 24), (13, 30)), ["defn"],
             ["field"]);
            ("int", "int", "file1", ((14, 25), (14, 28)), ["type"], ["abbrev"]);
            ("Case1", "Case1", "file1", ((14, 8), (14, 13)), ["defn"], []);
            ("Case2", "Case2", "file1", ((14, 16), (14, 21)), ["defn"], []);
            ("C", "C", "file1", ((4, 5), (4, 6)), ["defn"], ["class"]);
            ("D", "D", "file1", ((7, 4), (7, 5)), ["defn"], ["class"]);
            ("E", "E", "file1", ((10, 4), (10, 5)), ["defn"], ["class"]);
            ("F", "F", "file1", ((13, 4), (13, 5)), ["defn"], ["record"]);
            ("G", "G", "file1", ((14, 4), (14, 5)), ["defn"], ["union"]);
            ("member .ctor", "( .ctor )", "file1", ((4, 5), (4, 6)), ["defn"],
             ["member"; "ctor"]);
            ("member get_PC", "PC", "file1", ((5, 13), (5, 15)), ["defn"],
             ["member"; "getter"]);
            ("member .ctor", "( .ctor )", "file1", ((7, 4), (7, 5)), ["defn"],
             ["member"; "ctor"]);
            ("member get_PD", "PD", "file1", ((8, 13), (8, 15)), ["defn"],
             ["member"; "getter"]);
            ("member .ctor", "( .ctor )", "file1", ((10, 4), (10, 5)), ["defn"],
             ["member"; "ctor"]);
            ("member get_PE", "PE", "file1", ((11, 13), (11, 15)), ["defn"],
             ["member"; "getter"]);
            ("val x", "x", "file1", ((5, 11), (5, 12)), ["defn"], []);
            ("val x", "x", "file1", ((8, 11), (8, 12)), ["defn"], []);
            ("val x", "x", "file1", ((11, 11), (11, 12)), ["defn"], []);
            ("Impl", "Impl", "file1", ((2, 7), (2, 11)), ["defn"], ["module"])|]

[<Test>]
let ``Test Project16 sig symbols are equal to impl symbols`` () =

    let checkResultsSig =
        checker.ParseAndCheckFileInProject(Project16.sigFileName1, 0, Project16.sigFileSource1, Project16.options)  |> Async.RunImmediate
        |> function
            | _, FSharpCheckFileAnswer.Succeeded(res) -> res
            | _ -> failwithf "Parsing aborted unexpectedly..."

    let checkResultsImpl =
        checker.ParseAndCheckFileInProject(Project16.fileName1, 0, Project16.fileSource1, Project16.options)  |> Async.RunImmediate
        |> function
            | _, FSharpCheckFileAnswer.Succeeded(res) -> res
            | _ -> failwithf "Parsing aborted unexpectedly..."


    let symbolsSig = checkResultsSig.GetAllUsesOfAllSymbolsInFile()
    let symbolsImpl = checkResultsImpl.GetAllUsesOfAllSymbolsInFile()

    // Test that all 'definition' symbols in the signature (or implementation) have a matching symbol in the
    // implementation (or signature).
    let testFind (tag1,symbols1) (tag2,symbols2) =
        for (symUse1: FSharpSymbolUse) in symbols1 do

          if symUse1.IsFromDefinition &&
             (match symUse1.Symbol with
              | :? FSharpMemberOrFunctionOrValue as m -> m.IsModuleValueOrMember
              | :? FSharpEntity -> true
              | _ -> false) then

            let ok =
                symbols2
                |> Seq.filter (fun (symUse2:FSharpSymbolUse) ->
                    //if symUse2.IsFromDefinition && symUse1.Symbol.DisplayName = symUse2.Symbol.DisplayName then
                    //   printfn "Comparing \n\t'%A' \n\t\t@ %A \n\t\t@@ %A and \n\t'%A' \n\t\t@ %A \n\t\t@@ %A" symUse1.Symbol symUse1.Symbol.ImplementationLocation symUse1.Symbol.SignatureLocation symUse2.Symbol symUse2.Symbol.ImplementationLocation symUse2.Symbol.SignatureLocation
                    symUse2.Symbol.IsEffectivelySameAs(symUse1.Symbol) )
                |> Seq.toList

            match ok with
            | [] -> failwith (sprintf "Didn't find symbol equivalent to %s symbol '%A' in %s" tag1 symUse1.Symbol tag2)
            | [sym] -> ()
            | [sym1;sym2] when sym1.Symbol.DisplayName = sym2.Symbol.DisplayName -> ()   // constructor and type
            | syms -> failwith (sprintf "Found multiple symbols for %s '%A' in  %s: '%A'" tag1 symUse1.Symbol tag2 [for sym in syms -> sym.Symbol ] )

    testFind ("signature", symbolsSig) ("implementation", symbolsImpl)
    testFind ("implementation", symbolsImpl) ("signature", symbolsSig)  // test the other way around too, since this signature doesn't hide any definitions

    testFind ("implementation", symbolsImpl) ("implementation", symbolsImpl)  // of course this should pass...
    testFind ("signature", symbolsSig) ("signature", symbolsSig)  // of course this should pass...

[<Test>]
let ``Test Project16 sym locations`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project16.options) |> Async.RunImmediate

    let fmtLoc (mOpt: range option) =
        match mOpt with
        | None -> None
        | Some m ->
            let file = Project16.cleanFileName m.FileName
            if file = "??" then None
            else Some (file, (m.StartLine, m.StartColumn), (m.EndLine, m.EndColumn ))

    let allUsesOfAllSymbols =
        wholeProjectResults.GetAllUsesOfAllSymbols()

        |> Array.choose (fun su ->
             match fmtLoc su.Symbol.SignatureLocation, fmtLoc su.Symbol.DeclarationLocation, fmtLoc su.Symbol.ImplementationLocation with
             | Some a, Some b, Some c -> Some (su.Symbol.ToString(), a, b, c)
             | _ -> None)

    allUsesOfAllSymbols |> shouldEqual
          [|("field Field1", ("sig1", (16, 10), (16, 16)),("sig1", (16, 10), (16, 16)), ("file1", (13, 10), (13, 16)));
            ("field Field2", ("sig1", (16, 24), (16, 30)),("sig1", (16, 24), (16, 30)), ("file1", (13, 24), (13, 30)));
            ("Case1", ("sig1", (17, 8), (17, 13)), ("sig1", (17, 8), (17, 13)),("file1", (14, 8), (14, 13)));
            ("Case2", ("sig1", (17, 16), (17, 21)), ("sig1", (17, 16), (17, 21)),("file1", (14, 16), (14, 21)));
            ("C", ("sig1", (4, 5), (4, 6)), ("sig1", (4, 5), (4, 6)),("file1", (4, 5), (4, 6)));
            ("C", ("sig1", (4, 5), (4, 6)), ("sig1", (4, 5), (4, 6)),("file1", (4, 5), (4, 6)));
            ("member .ctor", ("sig1", (5, 4), (5, 7)), ("sig1", (5, 4), (5, 7)),("file1", (4, 5), (4, 6)));
            ("member get_PC", ("sig1", (6, 11), (6, 13)), ("sig1", (6, 11), (6, 13)),("file1", (5, 13), (5, 15)));
            ("D", ("sig1", (8, 14), (8, 15)), ("sig1", (8, 14), (8, 15)),("file1", (7, 4), (7, 5)));
            ("D", ("sig1", (8, 14), (8, 15)), ("sig1", (8, 14), (8, 15)),("file1", (7, 4), (7, 5)));
            ("member .ctor", ("sig1", (9, 4), (9, 7)), ("sig1", (9, 4), (9, 7)),("file1", (7, 4), (7, 5)));
            ("member get_PD", ("sig1", (10, 11), (10, 13)),("sig1", (10, 11), (10, 13)), ("file1", (8, 13), (8, 15)));
            ("E", ("sig1", (12, 14), (12, 15)), ("sig1", (12, 14), (12, 15)),("file1", (10, 4), (10, 5)));
            ("E", ("sig1", (12, 14), (12, 15)), ("sig1", (12, 14), (12, 15)),("file1", (10, 4), (10, 5)));
            ("member .ctor", ("sig1", (13, 4), (13, 7)), ("sig1", (13, 4), (13, 7)),("file1", (10, 4), (10, 5)));
            ("member get_PE", ("sig1", (14, 11), (14, 13)),("sig1", (14, 11), (14, 13)), ("file1", (11, 13), (11, 15)));
            ("F", ("sig1", (16, 4), (16, 5)), ("sig1", (16, 4), (16, 5)),("file1", (13, 4), (13, 5)));
            ("G", ("sig1", (17, 4), (17, 5)), ("sig1", (17, 4), (17, 5)),("file1", (14, 4), (14, 5)));
            ("Impl", ("sig1", (2, 7), (2, 11)), ("sig1", (2, 7), (2, 11)),("file1", (2, 7), (2, 11)));
            ("field Field1", ("sig1", (16, 10), (16, 16)),("file1", (13, 10), (13, 16)), ("file1", (13, 10), (13, 16)));
            ("field Field2", ("sig1", (16, 24), (16, 30)),("file1", (13, 24), (13, 30)), ("file1", (13, 24), (13, 30)));
            ("Case1", ("sig1", (17, 8), (17, 13)), ("file1", (14, 8), (14, 13)),("file1", (14, 8), (14, 13)));
            ("Case2", ("sig1", (17, 16), (17, 21)), ("file1", (14, 16), (14, 21)),("file1", (14, 16), (14, 21)));
            ("C", ("sig1", (4, 5), (4, 6)), ("file1", (4, 5), (4, 6)),("file1", (4, 5), (4, 6)));
            ("D", ("sig1", (8, 14), (8, 15)), ("file1", (7, 4), (7, 5)),("file1", (7, 4), (7, 5)));
            ("E", ("sig1", (12, 14), (12, 15)), ("file1", (10, 4), (10, 5)),("file1", (10, 4), (10, 5)));
            ("F", ("sig1", (16, 4), (16, 5)), ("file1", (13, 4), (13, 5)),("file1", (13, 4), (13, 5)));
            ("G", ("sig1", (17, 4), (17, 5)), ("file1", (14, 4), (14, 5)),("file1", (14, 4), (14, 5)));
            ("member .ctor", ("sig1", (5, 4), (5, 7)), ("file1", (4, 5), (4, 6)),("file1", (4, 5), (4, 6)));
            ("member get_PC", ("sig1", (6, 11), (6, 13)), ("file1", (5, 13), (5, 15)),("file1", (5, 13), (5, 15)));
            ("member .ctor", ("sig1", (9, 4), (9, 7)), ("file1", (7, 4), (7, 5)),("file1", (7, 4), (7, 5)));
            ("member get_PD", ("sig1", (10, 11), (10, 13)),("file1", (8, 13), (8, 15)), ("file1", (8, 13), (8, 15)));
            ("member .ctor", ("sig1", (13, 4), (13, 7)), ("file1", (10, 4), (10, 5)),("file1", (10, 4), (10, 5)));
            ("member get_PE", ("sig1", (14, 11), (14, 13)),("file1", (11, 13), (11, 15)), ("file1", (11, 13), (11, 15)));
            ("val x", ("file1", (5, 11), (5, 12)), ("file1", (5, 11), (5, 12)),("file1", (5, 11), (5, 12)));
            ("val x", ("file1", (8, 11), (8, 12)), ("file1", (8, 11), (8, 12)),("file1", (8, 11), (8, 12)));
            ("val x", ("file1", (11, 11), (11, 12)), ("file1", (11, 11), (11, 12)),("file1", (11, 11), (11, 12)));
            ("Impl", ("sig1", (2, 7), (2, 11)), ("file1", (2, 7), (2, 11)),("file1", (2, 7), (2, 11)))|]

[<Test>]
let ``Test project16 DeclaringEntity`` () =
    let wholeProjectResults =
        checker.ParseAndCheckProject(Project16.options)
        |> Async.RunImmediate
    let allSymbolsUses = wholeProjectResults.GetAllUsesOfAllSymbols()
    for sym in allSymbolsUses do
       match sym.Symbol with
       | :? FSharpEntity as e when not e.IsNamespace || e.AccessPath.Contains(".") ->
           printfn "checking declaring type of entity '%s' --> '%s', assembly = '%s'" e.AccessPath e.CompiledName (e.Assembly.ToString())
           shouldEqual e.DeclaringEntity.IsSome (e.AccessPath <> "global")
           match e.AccessPath with
           | "C" | "D" | "E" | "F" | "G" ->
               shouldEqual e.AccessPath "Impl"
               shouldEqual e.DeclaringEntity.Value.IsFSharpModule true
               shouldEqual e.DeclaringEntity.Value.IsNamespace false
           | "int" ->
               shouldEqual e.AccessPath "Microsoft.FSharp.Core"
               shouldEqual e.DeclaringEntity.Value.AccessPath "Microsoft.FSharp"
           | _ -> ()
       | :? FSharpMemberOrFunctionOrValue as e when e.IsModuleValueOrMember ->
           printfn "checking declaring type of value '%s', assembly = '%s'" e.CompiledName (e.Assembly.ToString())
           shouldEqual e.DeclaringEntity.IsSome true
       | _ ->  ()


//-----------------------------------------------------------------------------------------
// Misc - namespace symbols

module internal Project17 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module Impl

let _ = Microsoft.FSharp.Collections.List<int>.Empty // check use of getter property using long namespace

let f1 (x: System.Collections.Generic.IList<'T>) = x.Item(3), x.[3], x.Count  // check use of getter properties and indexer

let f2 (x: System.Collections.Generic.IList<int>) = x.[3] <- 4  // check use of .NET setter indexer

let f3 (x: System.Exception) = x.HelpLink <- "" // check use of .NET setter property
    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)
    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)


[<Test>]
let ``Test Project17 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project17.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project17 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0


[<Test>]
let ``Test Project17 all symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project17.options) |> Async.RunImmediate

    let allUsesOfAllSymbols =
        wholeProjectResults.GetAllUsesOfAllSymbols()

        |> Array.map (fun su -> su.Symbol.ToString(), su.Symbol.DisplayName, Project17.cleanFileName su.FileName, tups su.Range, attribsOfSymbolUse su, attribsOfSymbol su.Symbol)

    allUsesOfAllSymbols
      |> shouldEqual
          [|("Microsoft", "Microsoft", "file1", ((4, 8), (4, 17)), [], ["namespace"]);
            ("Collections", "Collections", "file1", ((4, 25), (4, 36)), [], ["namespace"]);
            ("FSharp", "FSharp", "file1", ((4, 18), (4, 24)), [], ["namespace"]);
            ("FSharpList`1", "List", "file1", ((4, 8), (4, 41)), [], ["union"]);
            ("int", "int", "file1", ((4, 42), (4, 45)), ["type"], ["abbrev"]);
            ("FSharpList`1", "List", "file1", ((4, 8), (4, 46)), [], ["union"]);
            ("property Empty", "Empty", "file1", ((4, 8), (4, 52)), [], ["member"; "prop"]);
            ("System", "System", "file1", ((6, 11), (6, 17)), [], ["namespace"]);
            ("Collections", "Collections", "file1", ((6, 18), (6, 29)), [], ["namespace"]);
            ("Generic", "Generic", "file1", ((6, 30), (6, 37)), [], ["namespace"]);
            ("IList`1", "IList", "file1", ((6, 11), (6, 43)), ["type"], ["interface"]);
            ("generic parameter T", "T", "file1", ((6, 44), (6, 46)), ["type"], []);
            ("val x", "x", "file1", ((6, 8), (6, 9)), ["defn"], []);
            ("val x", "x", "file1", ((6, 51), (6, 52)), [], []);
            ("property Item", "Item", "file1", ((6, 51), (6, 57)), [],
             ["slot"; "member"; "prop"]);
            ("val x", "x", "file1", ((6, 62), (6, 63)), [], []);
            ("property Item", "Item", "file1", ((6, 62), (6, 67)), [],
             ["slot"; "member"; "prop"]);
            ("val x", "x", "file1", ((6, 69), (6, 70)), [], []);
            ("property Count", "Count", "file1", ((6, 69), (6, 76)), [],
             ["slot"; "member"; "prop"]);
            ("val f1", "f1", "file1", ((6, 4), (6, 6)), ["defn"], ["val"]);
            ("System", "System", "file1", ((8, 11), (8, 17)), [], ["namespace"]);
            ("Collections", "Collections", "file1", ((8, 18), (8, 29)), [], ["namespace"]);
            ("Generic", "Generic", "file1", ((8, 30), (8, 37)), [], ["namespace"]);
            ("IList`1", "IList", "file1", ((8, 11), (8, 43)), ["type"], ["interface"]);
            ("int", "int", "file1", ((8, 44), (8, 47)), ["type"], ["abbrev"]);
            ("val x", "x", "file1", ((8, 8), (8, 9)), ["defn"], []);
            ("val x", "x", "file1", ((8, 52), (8, 53)), [], []);
            ("property Item", "Item", "file1", ((8, 52), (8, 57)), [],
             ["slot"; "member"; "prop"]);
            ("val f2", "f2", "file1", ((8, 4), (8, 6)), ["defn"], ["val"]);
            ("System", "System", "file1", ((10, 11), (10, 17)), [], ["namespace"]);
            ("Exception", "Exception", "file1", ((10, 11), (10, 27)), ["type"], ["class"]);
            ("val x", "x", "file1", ((10, 8), (10, 9)), ["defn"], []);
            ("val x", "x", "file1", ((10, 31), (10, 32)), [], []);
            ("property HelpLink", "HelpLink", "file1", ((10, 31), (10, 41)), [],
             ["slot"; "member"; "prop"]);
            ("val f3", "f3", "file1", ((10, 4), (10, 6)), ["defn"], ["val"]);
            ("Impl", "Impl", "file1", ((2, 7), (2, 11)), ["defn"], ["module"])|]


//-----------------------------------------------------------------------------------------
// Misc - generic type definnitions

module internal Project18 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module Impl

let _ = list<_>.Empty
    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)
    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)


[<Test>]
let ``Test Project18 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project18.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project18 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0


[<Test>]
let ``Test Project18 all symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project18.options) |> Async.RunImmediate

    let allUsesOfAllSymbols =
        wholeProjectResults.GetAllUsesOfAllSymbols()

        |> Array.map (fun su -> su.Symbol.ToString(), su.Symbol.DisplayName, Project18.cleanFileName su.FileName, tups su.Range, attribsOfSymbolUse su,
                                (match su.Symbol with :? FSharpEntity as e -> e.IsNamespace | _ -> false))

    allUsesOfAllSymbols |> shouldEqual
      [|("list`1", "list", "file1", ((4, 8), (4, 12)), [], false);
        ("list`1", "list", "file1", ((4, 8), (4, 15)), [], false);
        ("property Empty", "Empty", "file1", ((4, 8), (4, 21)), [], false);
        ("Impl", "Impl", "file1", ((2, 7), (2, 11)), ["defn"], false)|]



//-----------------------------------------------------------------------------------------
// Misc - enums

module internal Project19 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module Impl

type Enum = | EnumCase1 = 1 | EnumCase2 = 2

let _ = Enum.EnumCase1
let _ = Enum.EnumCase2
let f x = match x with Enum.EnumCase1 -> 1 | Enum.EnumCase2 -> 2 | _ -> 3

let s = System.DayOfWeek.Monday
    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)
    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)


[<Test>]
let ``Test Project19 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project19.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project19 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0


[<Test>]
let ``Test Project19 all symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project19.options) |> Async.RunImmediate

    let allUsesOfAllSymbols =
        wholeProjectResults.GetAllUsesOfAllSymbols()

        |> Array.map (fun su -> su.Symbol.ToString(), su.Symbol.DisplayName, Project19.cleanFileName su.FileName, tups su.Range, attribsOfSymbolUse su, attribsOfSymbol su.Symbol)

    allUsesOfAllSymbols |> shouldEqual
          [|("field EnumCase1", "EnumCase1", "file1", ((4, 14), (4, 23)), ["defn"],
             ["field"; "static"; "1"]);
            ("field EnumCase2", "EnumCase2", "file1", ((4, 30), (4, 39)), ["defn"],
             ["field"; "static"; "2"]);
            ("Enum", "Enum", "file1", ((4, 5), (4, 9)), ["defn"], ["enum"; "valuetype"]);
            ("Enum", "Enum", "file1", ((6, 8), (6, 12)), [], ["enum"; "valuetype"]);
            ("field EnumCase1", "EnumCase1", "file1", ((6, 8), (6, 22)), [],
             ["field"; "static"; "1"]);
            ("Enum", "Enum", "file1", ((7, 8), (7, 12)), [], ["enum"; "valuetype"]);
            ("field EnumCase2", "EnumCase2", "file1", ((7, 8), (7, 22)), [],
             ["field"; "static"; "2"]);
            ("val x", "x", "file1", ((8, 6), (8, 7)), ["defn"], []);
            ("val x", "x", "file1", ((8, 16), (8, 17)), [], []);
            ("Enum", "Enum", "file1", ((8, 23), (8, 27)), [], ["enum"; "valuetype"]);
            ("field EnumCase1", "EnumCase1", "file1", ((8, 23), (8, 37)), ["pattern"],
             ["field"; "static"; "1"]);
            ("Enum", "Enum", "file1", ((8, 45), (8, 49)), [], ["enum"; "valuetype"]);
            ("field EnumCase2", "EnumCase2", "file1", ((8, 45), (8, 59)), ["pattern"],
             ["field"; "static"; "2"]);
            ("val f", "f", "file1", ((8, 4), (8, 5)), ["defn"], ["val"]);
            ("System", "System", "file1", ((10, 8), (10, 14)), [], ["namespace"]);
            ("DayOfWeek", "DayOfWeek", "file1", ((10, 15), (10, 24)), [],
             ["enum"; "valuetype"]);
            ("field Monday", "Monday", "file1", ((10, 8), (10, 31)), [],
             ["field"; "static"; "1"]);
            ("val s", "s", "file1", ((10, 4), (10, 5)), ["defn"], ["val"]);
            ("Impl", "Impl", "file1", ((2, 7), (2, 11)), ["defn"], ["module"])|]



//-----------------------------------------------------------------------------------------
// Misc - https://github.com/fsharp/FSharp.Compiler.Service/issues/109

module internal Project20 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module Impl

type A<'T>() =
    member x.M() : 'T = failwith ""

    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)
    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)


[<Test>]
let ``Test Project20 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project20.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project20 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0


[<Test>]
let ``Test Project20 all symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project20.options) |> Async.RunImmediate

    let tSymbolUse = wholeProjectResults.GetAllUsesOfAllSymbols() |> Array.find (fun su -> su.Range.StartLine = 5 && su.Symbol.ToString() = "generic parameter T")
    let tSymbol = tSymbolUse.Symbol



    let allUsesOfTSymbol =
        wholeProjectResults.GetUsesOfSymbol(tSymbol)

        |> Array.map (fun su -> su.Symbol.ToString(), su.Symbol.DisplayName, Project20.cleanFileName su.FileName, tups su.Range, attribsOfSymbolUse su, attribsOfSymbol su.Symbol)

    allUsesOfTSymbol |> shouldEqual
          [|("generic parameter T", "T", "file1", ((4, 7), (4, 9)), ["type"], []);
            ("generic parameter T", "T", "file1", ((5, 19), (5, 21)), ["type"], [])|]

//-----------------------------------------------------------------------------------------
// Misc - https://github.com/fsharp/FSharp.Compiler.Service/issues/137

module internal Project21 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module Impl

type IMyInterface<'a> =
    abstract Method1: 'a -> unit
    abstract Method2: 'a -> unit

let _ = { new IMyInterface<int> with
              member x.Method1(arg1: string): unit =
                  raise (System.NotImplementedException())

              member x.Method2(arg1: int): unit =
                  raise (System.NotImplementedException())
               }

    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)
    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)


[<Test>]
let ``Test Project21 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project21.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project21 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 2


[<Test>]
let ``Test Project21 all symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project21.options) |> Async.RunImmediate

    let allUsesOfAllSymbols =
        wholeProjectResults.GetAllUsesOfAllSymbols()

        |> Array.map (fun su -> su.Symbol.ToString(), su.Symbol.DisplayName, Project21.cleanFileName su.FileName, tups su.Range, attribsOfSymbolUse su, attribsOfSymbol su.Symbol)

    allUsesOfAllSymbols |> shouldEqual
          [|("generic parameter a", "a", "file1", ((4, 18), (4, 20)), ["type"], []);
            ("generic parameter a", "a", "file1", ((5, 22), (5, 24)), ["type"], []);
            ("unit", "unit", "file1", ((5, 28), (5, 32)), ["type"], ["abbrev"]);
            ("member Method1", "Method1", "file1", ((5, 13), (5, 20)), ["defn"],
             ["slot"; "member"]);
            ("generic parameter a", "a", "file1", ((6, 22), (6, 24)), ["type"], []);
            ("unit", "unit", "file1", ((6, 28), (6, 32)), ["type"], ["abbrev"]);
            ("member Method2", "Method2", "file1", ((6, 13), (6, 20)), ["defn"],
             ["slot"; "member"]);
            ("IMyInterface`1", "IMyInterface", "file1", ((4, 5), (4, 17)), ["defn"],
             ["interface"]);
            ("IMyInterface`1", "IMyInterface", "file1", ((8, 14), (8, 26)), ["type"],
             ["interface"]);
            ("int", "int", "file1", ((8, 27), (8, 30)), ["type"], ["abbrev"]);
            ("val x", "x", "file1", ((9, 21), (9, 22)), ["defn"], []);
            ("string", "string", "file1", ((9, 37), (9, 43)), ["type"], ["abbrev"]);
            ("val x", "x", "file1", ((12, 21), (12, 22)), ["defn"], []);
            ("int", "int", "file1", ((12, 37), (12, 40)), ["type"], ["abbrev"]);
            ("val arg1", "arg1", "file1", ((12, 31), (12, 35)), ["defn"], []);
            ("unit", "unit", "file1", ((12, 43), (12, 47)), ["type"], ["abbrev"]);
            ("val raise", "raise", "file1", ((13, 18), (13, 23)), [], ["val"]);
            ("System", "System", "file1", ((13, 25), (13, 31)), [], ["namespace"]);
            ("member .ctor", ".ctor", "file1", ((13, 25), (13, 55)), [], ["member"]);
            ("Impl", "Impl", "file1", ((2, 7), (2, 11)), ["defn"], ["module"])|]

//-----------------------------------------------------------------------------------------
// Misc - namespace symbols

module internal Project22 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module Impl

type AnotherMutableList() =
    member x.Item with get() = 3 and set (v:int) = ()

let f1 (x: System.Collections.Generic.IList<'T>) = () // grab the IList symbol and look into it
let f2 (x: AnotherMutableList) = () // grab the AnotherMutableList symbol and look into it
let f3 (x: System.Collections.ObjectModel.ObservableCollection<'T>) = () // grab the ObservableCollection symbol and look into it
let f4 (x: int[]) = () // test a one-dimensional array
let f5 (x: int[,,]) = () // test a multi-dimensional array
    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)
    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)



[<Test>]
let ``Test Project22 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project22.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project22 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0


[<Test>]
let ``Test Project22 IList contents`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project22.options) |> Async.RunImmediate

    let allUsesOfAllSymbols =
        wholeProjectResults.GetAllUsesOfAllSymbols()


    let ilistTypeUse =
        allUsesOfAllSymbols
        |> Array.find (fun su -> su.Symbol.DisplayName = "IList")

    let ocTypeUse =
        allUsesOfAllSymbols
        |> Array.find (fun su -> su.Symbol.DisplayName = "ObservableCollection")

    let alistTypeUse =
        allUsesOfAllSymbols
        |> Array.find (fun su -> su.Symbol.DisplayName = "AnotherMutableList")

    let allTypes =
        allUsesOfAllSymbols
        |> Array.choose (fun su -> match su.Symbol with :? FSharpMemberOrFunctionOrValue as s -> Some s.FullType | _ -> None )

    let arrayTypes =
        allTypes
        |> Array.choose (fun t ->
            if t.HasTypeDefinition then
               let td = t.TypeDefinition
               if td.IsArrayType then Some (td.DisplayName, td.ArrayRank) else None
            else None )

    let ilistTypeDefn = ilistTypeUse.Symbol :?> FSharpEntity
    let ocTypeDefn = ocTypeUse.Symbol :?> FSharpEntity
    let alistTypeDefn = alistTypeUse.Symbol :?> FSharpEntity

    set [ for x in ilistTypeDefn.MembersFunctionsAndValues -> x.LogicalName, attribsOfSymbol x ]
      |> shouldEqual
           (set [("get_Item", ["slot"; "member"; "getter"]);
                ("set_Item", ["slot"; "member"; "setter"]);
                ("IndexOf", ["slot"; "member"]);
                ("Insert", ["slot"; "member"]);
                ("RemoveAt", ["slot"; "member"]);
                ("Item", ["slot"; "member"; "prop"])])

    set [ for x in ocTypeDefn.MembersFunctionsAndValues -> x.LogicalName, attribsOfSymbol x ]
      |> shouldEqual
         (set [(".ctor", ["member"]);
               (".ctor", ["member"]);
               (".ctor", ["member"]);
               ("Move", ["member"]);
               ("add_CollectionChanged", ["slot"; "member"; "add"]);
               ("remove_CollectionChanged", ["slot"; "member"; "remove"]);
               ("ClearItems", ["slot"; "member"]);
               ("RemoveItem", ["slot"; "member"]);
               ("InsertItem", ["slot"; "member"]);
               ("SetItem", ["slot"; "member"]);
               ("MoveItem", ["slot"; "member"]);
               ("OnPropertyChanged", ["slot"; "member"]);
               ("add_PropertyChanged", ["slot"; "member"; "add"]);
               ("remove_PropertyChanged", ["slot"; "member"; "remove"]);
               ("OnCollectionChanged", ["slot"; "member"]);
               ("BlockReentrancy", ["member"]);
               ("CheckReentrancy", ["member"]);
               ("CollectionChanged", ["slot"; "member"; "event"]);
               ("PropertyChanged", ["slot"; "member"; "event"])])

    set [ for x in alistTypeDefn.MembersFunctionsAndValues -> x.LogicalName, attribsOfSymbol x ]
      |> shouldEqual
            (set [(".ctor", ["member"; "ctor"]);
                  ("get_Item", ["member"; "getter"]);
                  ("set_Item", ["member"; "setter"]);
                  ("Item", ["member"; "prop"])])

    set [ for x in ilistTypeDefn.AllInterfaces -> x.TypeDefinition.DisplayName, attribsOfSymbol x.TypeDefinition ]
       |> shouldEqual
              (set [("IList", ["interface"]); ("ICollection", ["interface"]);
                    ("IEnumerable", ["interface"]); ("IEnumerable", ["interface"])])

    arrayTypes |> shouldEqual [|("[]", 1); ("[,,]", 3)|]

[<Test>]
let ``Test Project22 IList properties`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project22.options) |> Async.RunImmediate

    let ilistTypeUse =
        wholeProjectResults.GetAllUsesOfAllSymbols()

        |> Array.find (fun su -> su.Symbol.DisplayName = "IList")

    let ilistTypeDefn = ilistTypeUse.Symbol :?> FSharpEntity

    attribsOfSymbol ilistTypeDefn |> shouldEqual ["interface"]

#if !NETCOREAPP // TODO: check if this can be enabled in .NET Core testing of FSharp.Compiler.Service
    ilistTypeDefn.Assembly.SimpleName |> shouldEqual coreLibAssemblyName
#endif

//-----------------------------------------------------------------------------------------
// Misc - properties

module internal Project23 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module Impl

type Class() =
    static member StaticProperty = 1
    member x.Property = 1

module Getter =
    type System.Int32 with
        static member Zero = 0
        member x.Value = 0

    let _ = 0 .Value

module Setter =
    type System.Int32 with
        member x.Value with set (_: int) = ()

    0 .Value <- 0
"""
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)
    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)

[<Test>]
let ``Test Project23 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project23.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project23 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0

[<Test>]
let ``Test Project23 property`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project23.options) |> Async.RunImmediate
    let allSymbolsUses = wholeProjectResults.GetAllUsesOfAllSymbols()

    let classTypeUse = allSymbolsUses |> Array.find (fun su -> su.Symbol.DisplayName = "Class")
    let classTypeDefn = classTypeUse.Symbol :?> FSharpEntity

    let results = [ for x in classTypeDefn.MembersFunctionsAndValues -> x.LogicalName, attribsOfSymbol x ]
    [(".ctor", ["member"; "ctor"]);
    ("get_Property", ["member"; "getter"]);
    ("get_StaticProperty", ["member"; "getter"]);
    ("StaticProperty", ["member"; "prop"]);
    ("Property", ["member"; "prop"])]
    |> List.iter (fun x ->
        if results |> List.exists (fun y -> x = y) |> not then
            failwithf "%A does not exist in the collection." x
    )

    let getterModuleUse = allSymbolsUses |> Array.find (fun su -> su.Symbol.DisplayName = "Getter")
    let getterModuleDefn = getterModuleUse.Symbol :?> FSharpEntity

    [ for x in getterModuleDefn.MembersFunctionsAndValues -> x.LogicalName, attribsOfSymbol x ]
      |> shouldEqual
              [("get_Zero", ["member"; "extmem"; "getter"]);
               ("Zero", ["member"; "prop"; "extmem"]);
               ("get_Value", ["member"; "extmem"; "getter"]);
               ("Value", ["member"; "prop"; "extmem"])]

    let extensionProps = getterModuleDefn.MembersFunctionsAndValues |> Seq.toArray |> Array.filter (fun su -> su.LogicalName = "Value" || su.LogicalName = "Zero" )
    let extensionPropsRelated =
        extensionProps
        |> Array.collect (fun f ->
            [|  if f.HasGetterMethod then
                    yield (f.DeclaringEntity.Value.FullName, f.ApparentEnclosingEntity.FullName, f.GetterMethod.CompiledName, f.GetterMethod.DeclaringEntity.Value.FullName, attribsOfSymbol f)
                if f.HasSetterMethod then
                    yield (f.DeclaringEntity.Value.FullName, f.ApparentEnclosingEntity.FullName, f.SetterMethod.CompiledName, f.SetterMethod.DeclaringEntity.Value.FullName, attribsOfSymbol f)
            |])
        |> Array.toList

    extensionPropsRelated  |> shouldEqual
          [("Impl.Getter", "System.Int32", "Int32.get_Zero.Static", "Impl.Getter",
            ["member"; "prop"; "extmem"]);
           ("Impl.Getter", "System.Int32", "Int32.get_Value", "Impl.Getter",
            ["member"; "prop"; "extmem"])]

    allSymbolsUses
    |> Array.map (fun x -> x.Symbol)
    |> Array.choose (function
        | :? FSharpMemberOrFunctionOrValue as f -> Some (f.LogicalName, attribsOfSymbol f)
        | _ -> None)
    |> Array.toList
    |> shouldEqual
        [(".ctor", ["member"; "ctor"]);
         ("get_StaticProperty", ["member"; "getter"]);
         ("get_Property", ["member"; "getter"]);
         ("x", []);
         ("get_Zero", ["member"; "extmem"; "getter"]);
         ("get_Value", ["member"; "extmem"; "getter"]);
         ("x", []);
         ("Value", ["member"; "prop"; "extmem"]);
         ("set_Value", ["member"; "extmem"; "setter"]);
         ("x", []);
         ("Value", ["member"; "prop"; "extmem"])]

[<Test>]
let ``Test Project23 extension properties' getters/setters should refer to the correct declaring entities`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project23.options) |> Async.RunImmediate
    let allSymbolsUses = wholeProjectResults.GetAllUsesOfAllSymbols()

    let extensionMembers = allSymbolsUses |> Array.rev |> Array.filter (fun su -> su.Symbol.DisplayName = "Value")
    extensionMembers
    |> Array.collect (fun memb -> wholeProjectResults.GetUsesOfSymbol(memb.Symbol) )
    |> Array.collect (fun x ->
        [|
        match x.Symbol with
        | :? FSharpMemberOrFunctionOrValue as f ->
            if f.HasGetterMethod then
                yield (f.DeclaringEntity.Value.FullName, f.GetterMethod.DeclaringEntity.Value.FullName, f.ApparentEnclosingEntity.FullName, f.GetterMethod.ApparentEnclosingEntity.FullName, attribsOfSymbol f)
            if f.HasSetterMethod then
                yield (f.DeclaringEntity.Value.FullName, f.SetterMethod.DeclaringEntity.Value.FullName, f.ApparentEnclosingEntity.FullName, f.SetterMethod.ApparentEnclosingEntity.FullName, attribsOfSymbol f)
        | _ -> ()
        |])
    |> Array.toList
    |> shouldEqual
        [ ("Impl.Setter", "Impl.Setter", "System.Int32", "System.Int32", ["member"; "prop"; "extmem"]);
          ("Impl.Setter", "Impl.Setter", "System.Int32", "System.Int32", ["member"; "prop"; "extmem"]);
          ("Impl.Getter", "Impl.Getter", "System.Int32", "System.Int32", ["member"; "prop"; "extmem"])
          ("Impl.Getter", "Impl.Getter", "System.Int32", "System.Int32", ["member"; "prop"; "extmem"]) ]

// Misc - property symbols
module internal Project24 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module PropertyTest

type TypeWithProperties() =
    member x.NameGetSet
        with get() = 0
        and set (v: int) = ()

    member x.NameGet
        with get() = 0
        and set (v: int) = ()

    member x.NameSet
        with set (v: int) = ()

    static member StaticNameGetSet
        with get() = 0
        and set (v: int) = ()

    static member StaticNameGet
        with get() = 0
        and set (v: int) = ()

    static member StaticNameSet
        with set (v: int) = ()

    member val AutoPropGet = 0 with get
    member val AutoPropGetSet = 0 with get, set

    static member val StaticAutoPropGet = 0 with get
    static member val StaticAutoPropGetSet = 0 with get, set

let v1 = TypeWithProperties().NameGetSet
TypeWithProperties().NameGetSet  <- 3

let v2 = TypeWithProperties().NameGet

TypeWithProperties().NameSet  <- 3

let v3 = TypeWithProperties.StaticNameGetSet
TypeWithProperties.StaticNameGetSet  <- 3

let v4 = TypeWithProperties.StaticNameGet

TypeWithProperties.StaticNameSet  <- 3

let v5 = TypeWithProperties().AutoPropGet

let v6 = TypeWithProperties().AutoPropGetSet
TypeWithProperties().AutoPropGetSet  <- 3

let v7 = TypeWithProperties.StaticAutoPropGet

let v8 = TypeWithProperties.StaticAutoPropGetSet
TypeWithProperties.StaticAutoPropGetSet  <- 3

"""
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)
    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options = checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)

[<Test>]
let ``Test Project24 whole project errors`` () =
    let wholeProjectResults = checker.ParseAndCheckProject(Project24.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project24 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0

[<Test>]
let ``Test Project24 all symbols`` () =
    let wholeProjectResults = checker.ParseAndCheckProject(Project24.options) |> Async.RunImmediate
    let backgroundParseResults1, backgroundTypedParse1 =
        checker.GetBackgroundCheckResultsForFileInProject(Project24.fileName1, Project24.options)
        |> Async.RunImmediate

    let allUses  =
        backgroundTypedParse1.GetAllUsesOfAllSymbolsInFile()
        |> Array.ofSeq
        |> Array.map (fun s -> (s.Symbol.DisplayName, Project24.cleanFileName s.FileName, tups s.Range, attribsOfSymbolUse s, attribsOfSymbol s.Symbol))

    allUses |> shouldEqual
          [|("TypeWithProperties", "file1", ((4, 5), (4, 23)), ["defn"], ["class"]);
            ("( .ctor )", "file1", ((4, 5), (4, 23)), ["defn"], ["member"; "ctor"]);
            ("NameGetSet", "file1", ((5, 13), (5, 23)), ["defn"], ["member"; "getter"]);
            ("int", "file1", ((7, 20), (7, 23)), ["type"], ["abbrev"]);
            ("NameGet", "file1", ((9, 13), (9, 20)), ["defn"], ["member"; "getter"]);
            ("int", "file1", ((11, 20), (11, 23)), ["type"], ["abbrev"]);
            ("int", "file1", ((14, 21), (14, 24)), ["type"], ["abbrev"]);
            ("NameSet", "file1", ((13, 13), (13, 20)), ["defn"], ["member"; "setter"]);
            ("StaticNameGetSet", "file1", ((16, 18), (16, 34)), ["defn"],
             ["member"; "getter"]);
            ("int", "file1", ((18, 20), (18, 23)), ["type"], ["abbrev"]);
            ("StaticNameGet", "file1", ((20, 18), (20, 31)), ["defn"],
             ["member"; "getter"]);
            ("int", "file1", ((22, 20), (22, 23)), ["type"], ["abbrev"]);
            ("int", "file1", ((25, 21), (25, 24)), ["type"], ["abbrev"]);
            ("StaticNameSet", "file1", ((24, 18), (24, 31)), ["defn"],
             ["member"; "setter"]);
            ("AutoPropGet", "file1", ((27, 15), (27, 26)), ["defn"],
             ["member"; "getter"]);
            ("AutoPropGetSet", "file1", ((28, 15), (28, 29)), ["defn"],
             ["member"; "getter"]);
            ("StaticAutoPropGet", "file1", ((30, 22), (30, 39)), ["defn"],
             ["member"; "getter"]);
            ("StaticAutoPropGetSet", "file1", ((31, 22), (31, 42)), ["defn"],
             ["member"; "getter"]);
            ("x", "file1", ((5, 11), (5, 12)), ["defn"], []);
            ("int", "file1", ((7, 20), (7, 23)), ["type"], ["abbrev"]);
            ("v", "file1", ((7, 17), (7, 18)), ["defn"], []);
            ("x", "file1", ((9, 11), (9, 12)), ["defn"], []);
            ("int", "file1", ((11, 20), (11, 23)), ["type"], ["abbrev"]);
            ("v", "file1", ((11, 17), (11, 18)), ["defn"], []);
            ("x", "file1", ((13, 11), (13, 12)), ["defn"], []);
            ("int", "file1", ((14, 21), (14, 24)), ["type"], ["abbrev"]);
            ("v", "file1", ((14, 18), (14, 19)), ["defn"], []);
            ("int", "file1", ((18, 20), (18, 23)), ["type"], ["abbrev"]);
            ("v", "file1", ((18, 17), (18, 18)), ["defn"], []);
            ("int", "file1", ((22, 20), (22, 23)), ["type"], ["abbrev"]);
            ("v", "file1", ((22, 17), (22, 18)), ["defn"], []);
            ("int", "file1", ((25, 21), (25, 24)), ["type"], ["abbrev"]);
            ("v", "file1", ((25, 18), (25, 19)), ["defn"], []);
            ("( AutoPropGet@ )", "file1", ((27, 15), (27, 26)), [], ["compgen"]);
            ("( AutoPropGetSet@ )", "file1", ((28, 15), (28, 29)), [], ["compgen";"mutable"]);
            ("v", "file1", ((28, 15), (28, 29)), ["defn"], []);
            ("( StaticAutoPropGet@ )", "file1", ((30, 22), (30, 39)), [], ["compgen"]);
            ("( StaticAutoPropGetSet@ )", "file1", ((31, 22), (31, 42)), [],
             ["compgen";"mutable"]); ("v", "file1", ((31, 22), (31, 42)), ["defn"], []);
            ("( .cctor )", "file1", ((4, 5), (4, 23)), ["defn"], ["member"]);
            ("TypeWithProperties", "file1", ((33, 9), (33, 27)), [],
             ["member"; "ctor"]);
            ("NameGetSet", "file1", ((33, 9), (33, 40)), [], ["member"; "prop"]);
            ("v1", "file1", ((33, 4), (33, 6)), ["defn"], ["val"]);
            ("TypeWithProperties", "file1", ((34, 0), (34, 18)), [],
             ["member"; "ctor"]);
            ("NameGetSet", "file1", ((34, 0), (34, 31)), [], ["member"; "prop"]);
            ("TypeWithProperties", "file1", ((36, 9), (36, 27)), [],
             ["member"; "ctor"]);
            ("NameGet", "file1", ((36, 9), (36, 37)), [], ["member"; "prop"]);
            ("v2", "file1", ((36, 4), (36, 6)), ["defn"], ["val"]);
            ("TypeWithProperties", "file1", ((38, 0), (38, 18)), [],
             ["member"; "ctor"]);
            ("NameSet", "file1", ((38, 0), (38, 28)), [], ["member"; "prop"]);
            ("TypeWithProperties", "file1", ((40, 9), (40, 27)), [], ["class"]);
            ("StaticNameGetSet", "file1", ((40, 9), (40, 44)), [], ["member"; "prop"]);
            ("v3", "file1", ((40, 4), (40, 6)), ["defn"], ["val"]);
            ("TypeWithProperties", "file1", ((41, 0), (41, 18)), [], ["class"]);
            ("StaticNameGetSet", "file1", ((41, 0), (41, 35)), [], ["member"; "prop"]);
            ("TypeWithProperties", "file1", ((43, 9), (43, 27)), [], ["class"]);
            ("StaticNameGet", "file1", ((43, 9), (43, 41)), [], ["member"; "prop"]);
            ("v4", "file1", ((43, 4), (43, 6)), ["defn"], ["val"]);
            ("TypeWithProperties", "file1", ((45, 0), (45, 18)), [], ["class"]);
            ("StaticNameSet", "file1", ((45, 0), (45, 32)), [], ["member"; "prop"]);
            ("TypeWithProperties", "file1", ((47, 9), (47, 27)), [],
             ["member"; "ctor"]);
            ("AutoPropGet", "file1", ((47, 9), (47, 41)), [], ["member"; "prop"]);
            ("v5", "file1", ((47, 4), (47, 6)), ["defn"], ["val"]);
            ("TypeWithProperties", "file1", ((49, 9), (49, 27)), [],
             ["member"; "ctor"]);
            ("AutoPropGetSet", "file1", ((49, 9), (49, 44)), [], ["member"; "prop"]);
            ("v6", "file1", ((49, 4), (49, 6)), ["defn"], ["val"]);
            ("TypeWithProperties", "file1", ((50, 0), (50, 18)), [],
             ["member"; "ctor"]);
            ("AutoPropGetSet", "file1", ((50, 0), (50, 35)), [], ["member"; "prop"]);
            ("TypeWithProperties", "file1", ((52, 9), (52, 27)), [], ["class"]);
            ("StaticAutoPropGet", "file1", ((52, 9), (52, 45)), [], ["member"; "prop"]);
            ("v7", "file1", ((52, 4), (52, 6)), ["defn"], ["val"]);
            ("TypeWithProperties", "file1", ((54, 9), (54, 27)), [], ["class"]);
            ("StaticAutoPropGetSet", "file1", ((54, 9), (54, 48)), [],
             ["member"; "prop"]);
            ("v8", "file1", ((54, 4), (54, 6)), ["defn"], ["val"]);
            ("TypeWithProperties", "file1", ((55, 0), (55, 18)), [], ["class"]);
            ("StaticAutoPropGetSet", "file1", ((55, 0), (55, 39)), [],
             ["member"; "prop"]);
            ("PropertyTest", "file1", ((2, 7), (2, 19)), ["defn"], ["module"])|]

[<Test>]
let ``Test symbol uses of properties with both getters and setters`` () =
    let wholeProjectResults = checker.ParseAndCheckProject(Project24.options) |> Async.RunImmediate
    let backgroundParseResults1, backgroundTypedParse1 =
        checker.GetBackgroundCheckResultsForFileInProject(Project24.fileName1, Project24.options)
        |> Async.RunImmediate

    let getAllSymbolUses =
        backgroundTypedParse1.GetAllUsesOfAllSymbolsInFile()
        |> Array.ofSeq
        |> Array.map (fun s -> (s.Symbol.DisplayName, Project24.cleanFileName s.FileName, tups s.Range, attribsOfSymbol s.Symbol))

    getAllSymbolUses |> shouldEqual
          [|("TypeWithProperties", "file1", ((4, 5), (4, 23)), ["class"]);
            ("( .ctor )", "file1", ((4, 5), (4, 23)), ["member"; "ctor"]);
            ("NameGetSet", "file1", ((5, 13), (5, 23)), ["member"; "getter"]);
            ("int", "file1", ((7, 20), (7, 23)), ["abbrev"]);
            ("NameGet", "file1", ((9, 13), (9, 20)), ["member"; "getter"]);
            ("int", "file1", ((11, 20), (11, 23)), ["abbrev"]);
            ("int", "file1", ((14, 21), (14, 24)), ["abbrev"]);
            ("NameSet", "file1", ((13, 13), (13, 20)), ["member"; "setter"]);
            ("StaticNameGetSet", "file1", ((16, 18), (16, 34)), ["member"; "getter"]);
            ("int", "file1", ((18, 20), (18, 23)), ["abbrev"]);
            ("StaticNameGet", "file1", ((20, 18), (20, 31)), ["member"; "getter"]);
            ("int", "file1", ((22, 20), (22, 23)), ["abbrev"]);
            ("int", "file1", ((25, 21), (25, 24)), ["abbrev"]);
            ("StaticNameSet", "file1", ((24, 18), (24, 31)), ["member"; "setter"]);
            ("AutoPropGet", "file1", ((27, 15), (27, 26)), ["member"; "getter"]);
            ("AutoPropGetSet", "file1", ((28, 15), (28, 29)), ["member"; "getter"]);
            ("StaticAutoPropGet", "file1", ((30, 22), (30, 39)), ["member"; "getter"]);
            ("StaticAutoPropGetSet", "file1", ((31, 22), (31, 42)),
             ["member"; "getter"]);
            ("x", "file1", ((5, 11), (5, 12)), []);
            ("int", "file1", ((7, 20), (7, 23)), ["abbrev"]);
            ("v", "file1", ((7, 17), (7, 18)), []);
            ("x", "file1", ((9, 11), (9, 12)), []);
            ("int", "file1", ((11, 20), (11, 23)), ["abbrev"]);
            ("v", "file1", ((11, 17), (11, 18)), []);
            ("x", "file1", ((13, 11), (13, 12)), []);
            ("int", "file1", ((14, 21), (14, 24)), ["abbrev"]);
            ("v", "file1", ((14, 18), (14, 19)), []);
            ("int", "file1", ((18, 20), (18, 23)), ["abbrev"]);
            ("v", "file1", ((18, 17), (18, 18)), []);
            ("int", "file1", ((22, 20), (22, 23)), ["abbrev"]);
            ("v", "file1", ((22, 17), (22, 18)), []);
            ("int", "file1", ((25, 21), (25, 24)), ["abbrev"]);
            ("v", "file1", ((25, 18), (25, 19)), []);
            ("( AutoPropGet@ )", "file1", ((27, 15), (27, 26)), ["compgen"]);
            ("( AutoPropGetSet@ )", "file1", ((28, 15), (28, 29)), ["compgen";"mutable"]);
            ("v", "file1", ((28, 15), (28, 29)), []);
            ("( StaticAutoPropGet@ )", "file1", ((30, 22), (30, 39)), ["compgen"]);
            ("( StaticAutoPropGetSet@ )", "file1", ((31, 22), (31, 42)), ["compgen";"mutable"]);
            ("v", "file1", ((31, 22), (31, 42)), []);
            ("( .cctor )", "file1", ((4, 5), (4, 23)), ["member"]);
            ("TypeWithProperties", "file1", ((33, 9), (33, 27)), ["member"; "ctor"]);
            ("NameGetSet", "file1", ((33, 9), (33, 40)), ["member"; "prop"]);
            ("v1", "file1", ((33, 4), (33, 6)), ["val"]);
            ("TypeWithProperties", "file1", ((34, 0), (34, 18)), ["member"; "ctor"]);
            ("NameGetSet", "file1", ((34, 0), (34, 31)), ["member"; "prop"]);
            ("TypeWithProperties", "file1", ((36, 9), (36, 27)), ["member"; "ctor"]);
            ("NameGet", "file1", ((36, 9), (36, 37)), ["member"; "prop"]);
            ("v2", "file1", ((36, 4), (36, 6)), ["val"]);
            ("TypeWithProperties", "file1", ((38, 0), (38, 18)), ["member"; "ctor"]);
            ("NameSet", "file1", ((38, 0), (38, 28)), ["member"; "prop"]);
            ("TypeWithProperties", "file1", ((40, 9), (40, 27)), ["class"]);
            ("StaticNameGetSet", "file1", ((40, 9), (40, 44)), ["member"; "prop"]);
            ("v3", "file1", ((40, 4), (40, 6)), ["val"]);
            ("TypeWithProperties", "file1", ((41, 0), (41, 18)), ["class"]);
            ("StaticNameGetSet", "file1", ((41, 0), (41, 35)), ["member"; "prop"]);
            ("TypeWithProperties", "file1", ((43, 9), (43, 27)), ["class"]);
            ("StaticNameGet", "file1", ((43, 9), (43, 41)), ["member"; "prop"]);
            ("v4", "file1", ((43, 4), (43, 6)), ["val"]);
            ("TypeWithProperties", "file1", ((45, 0), (45, 18)), ["class"]);
            ("StaticNameSet", "file1", ((45, 0), (45, 32)), ["member"; "prop"]);
            ("TypeWithProperties", "file1", ((47, 9), (47, 27)), ["member"; "ctor"]);
            ("AutoPropGet", "file1", ((47, 9), (47, 41)), ["member"; "prop"]);
            ("v5", "file1", ((47, 4), (47, 6)), ["val"]);
            ("TypeWithProperties", "file1", ((49, 9), (49, 27)), ["member"; "ctor"]);
            ("AutoPropGetSet", "file1", ((49, 9), (49, 44)), ["member"; "prop"]);
            ("v6", "file1", ((49, 4), (49, 6)), ["val"]);
            ("TypeWithProperties", "file1", ((50, 0), (50, 18)), ["member"; "ctor"]);
            ("AutoPropGetSet", "file1", ((50, 0), (50, 35)), ["member"; "prop"]);
            ("TypeWithProperties", "file1", ((52, 9), (52, 27)), ["class"]);
            ("StaticAutoPropGet", "file1", ((52, 9), (52, 45)), ["member"; "prop"]);
            ("v7", "file1", ((52, 4), (52, 6)), ["val"]);
            ("TypeWithProperties", "file1", ((54, 9), (54, 27)), ["class"]);
            ("StaticAutoPropGetSet", "file1", ((54, 9), (54, 48)), ["member"; "prop"]);
            ("v8", "file1", ((54, 4), (54, 6)), ["val"]);
            ("TypeWithProperties", "file1", ((55, 0), (55, 18)), ["class"]);
            ("StaticAutoPropGetSet", "file1", ((55, 0), (55, 39)), ["member"; "prop"]);
            ("PropertyTest", "file1", ((2, 7), (2, 19)), ["module"])|]

    let getSampleSymbolUseOpt =
        backgroundTypedParse1.GetSymbolUseAtLocation(9,20,"",["NameGet"])


    let getSampleSymbol = getSampleSymbolUseOpt.Value.Symbol

    let usesOfGetSampleSymbol =
        backgroundTypedParse1.GetUsesOfSymbolInFile(getSampleSymbol)

        |> Array.map (fun s -> (Project24.cleanFileName s.FileName, tups s.Range))

    usesOfGetSampleSymbol |> shouldEqual [|("file1", ((9, 13), (9, 20))); ("file1", ((36, 9), (36, 37)))|]

#if NO_CHECK_USE_OF_FSHARP_DATA_DLL
#endif
// Misc - type provider symbols
module internal Project25 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module TypeProviderTests
open FSharp.Data
type Project = XmlProvider<"<root><value>1</value><value>3</value></root>">
let _ = Project.GetSample()

type Record = { Field: int }
let r = { Record.Field = 1 }

let _ = XmlProvider<"<root><value>1</value><value>3</value></root>">.GetSample()
"""
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)
    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args =
        [| yield! mkProjectCommandLineArgs (dllName, fileNames)
           yield @"-r:" + Path.Combine(__SOURCE_DIRECTORY__, Path.Combine("data", "FSharp.Data.dll"))
           yield @"-r:" + sysLib "System.Xml.Linq" |]
    let options = checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)

[<Test>]
#if NETCOREAPP
[<Ignore "SKIPPED: Disabled until FSharp.Data.dll is build for dotnet core.">]
#endif
let ``Test Project25 whole project errors`` () =
    let wholeProjectResults = checker.ParseAndCheckProject(Project25.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project25 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0

[<Test>]
#if NETCOREAPP
[<Ignore "SKIPPED: Disabled until FSharp.Data.dll is build for dotnet core.">]
#endif
let ``Test Project25 symbol uses of type-provided members`` () =
    let wholeProjectResults = checker.ParseAndCheckProject(Project25.options) |> Async.RunImmediate
    let backgroundParseResults1, backgroundTypedParse1 =
        checker.GetBackgroundCheckResultsForFileInProject(Project25.fileName1, Project25.options)
        |> Async.RunImmediate

    let allUses  =
        backgroundTypedParse1.GetAllUsesOfAllSymbolsInFile()
        |> Array.ofSeq
        |> Array.map (fun s -> (s.Symbol.FullName, Project25.cleanFileName s.FileName, tups s.Range, attribsOfSymbol s.Symbol))

    allUses |> shouldEqual

         [|("FSharp", "file1", ((3, 5), (3, 11)), ["namespace"]);
           ("FSharp.Data", "file1", ((3, 12), (3, 16)), ["namespace"; "provided"]);
           ("Microsoft.FSharp", "file1", ((3, 5), (3, 11)), ["namespace"]);
           ("Microsoft.FSharp.Data", "file1", ((3, 12), (3, 16)), ["namespace"]);
           ("FSharp.Data.XmlProvider", "file1", ((4, 15), (4, 26)),
            ["class"; "provided"; "erased"]);
           ("FSharp.Data.XmlProvider", "file1", ((4, 15), (4, 26)),
            ["class"; "provided"; "erased"]);
           ("FSharp.Data.XmlProvider", "file1", ((4, 15), (4, 26)),
            ["class"; "provided"; "erased"]);
           ("FSharp.Data.XmlProvider", "file1", ((4, 15), (4, 26)),
            ["class"; "provided"; "erased"]);
           ("TypeProviderTests.Project", "file1", ((4, 5), (4, 12)), ["abbrev"]);
           ("TypeProviderTests.Project", "file1", ((5, 8), (5, 15)), ["abbrev"]);
           ("FSharp.Data.XmlProvider<...>.GetSample", "file1", ((5, 8), (5, 25)),
            ["member"]);
           ("Microsoft.FSharp.Core.int", "file1", ((7, 23), (7, 26)), ["abbrev"]);
           ("Microsoft.FSharp.Core.int", "file1", ((7, 23), (7, 26)), ["abbrev"]);
           ("TypeProviderTests.Record.Field", "file1", ((7, 16), (7, 21)), ["field"]);
           ("TypeProviderTests.Record", "file1", ((7, 5), (7, 11)), ["record"]);
           ("TypeProviderTests.Record", "file1", ((8, 10), (8, 16)), ["record"]);
           ("TypeProviderTests.Record.Field", "file1", ((8, 17), (8, 22)), ["field"]);
           ("TypeProviderTests.r", "file1", ((8, 4), (8, 5)), ["val"]);
           ("FSharp.Data.XmlProvider", "file1", ((10, 8), (10, 19)),
            ["class"; "provided"; "erased"]);
           ("FSharp.Data.XmlProvider<...>", "file1", ((10, 8), (10, 68)),
            ["class"; "provided"; "staticinst"; "erased"]);
           ("FSharp.Data.XmlProvider<...>.GetSample", "file1", ((10, 8), (10, 78)),
            ["member"]); ("TypeProviderTests", "file1", ((2, 7), (2, 24)), ["module"])|]
    let getSampleSymbolUseOpt =
        backgroundTypedParse1.GetSymbolUseAtLocation(5,25,"",["GetSample"])


    let getSampleSymbol = getSampleSymbolUseOpt.Value.Symbol

    let usesOfGetSampleSymbol =
        backgroundTypedParse1.GetUsesOfSymbolInFile(getSampleSymbol)

        |> Array.map (fun s -> (Project25.cleanFileName s.FileName, tups s.Range))

    usesOfGetSampleSymbol |> shouldEqual [|("file1", ((5, 8), (5, 25))); ("file1", ((10, 8), (10, 78)))|]

[<Test>]
#if NETCOREAPP
[<Ignore "SKIPPED: Disabled until FSharp.Data.dll is build for dotnet core.">]
#endif
let ``Test symbol uses of type-provided types`` () =
    let wholeProjectResults = checker.ParseAndCheckProject(Project25.options) |> Async.RunImmediate
    let backgroundParseResults1, backgroundTypedParse1 =
        checker.GetBackgroundCheckResultsForFileInProject(Project25.fileName1, Project25.options)
        |> Async.RunImmediate

    let getSampleSymbolUseOpt =
        backgroundTypedParse1.GetSymbolUseAtLocation(4,26,"",["XmlProvider"])


    let getSampleSymbol = getSampleSymbolUseOpt.Value.Symbol

    let usesOfGetSampleSymbol =
        backgroundTypedParse1.GetUsesOfSymbolInFile(getSampleSymbol)

        |> Array.map (fun s -> (Project25.cleanFileName s.FileName, tups s.Range))

    usesOfGetSampleSymbol |> shouldEqual [|("file1", ((4, 15), (4, 26))); ("file1", ((10, 8), (10, 19)))|]

[<Test>]
let ``Test symbol uses of fully-qualified records`` () =
    let wholeProjectResults = checker.ParseAndCheckProject(Project25.options) |> Async.RunImmediate
    let backgroundParseResults1, backgroundTypedParse1 =
        checker.GetBackgroundCheckResultsForFileInProject(Project25.fileName1, Project25.options)
        |> Async.RunImmediate

    let getSampleSymbolUseOpt =
        backgroundTypedParse1.GetSymbolUseAtLocation(7,11,"",["Record"])


    let getSampleSymbol = getSampleSymbolUseOpt.Value.Symbol

    let usesOfGetSampleSymbol =
        backgroundTypedParse1.GetUsesOfSymbolInFile(getSampleSymbol)

        |> Array.map (fun s -> (Project25.cleanFileName s.FileName, tups s.Range))

    usesOfGetSampleSymbol |> shouldEqual [|("file1", ((7, 5), (7, 11))); ("file1", ((8, 10), (8, 16)))|]


module internal Project26 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module FSharpParameter
open System
open System.Runtime.InteropServices

type Class() =
    member x.M1(arg1, ?arg2) = ()
    member x.M2([<ParamArray>] arg1, [<OptionalArgument>] arg2) = ()
    member x.M3([<Out>] arg: byref<int>) = ()
    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)


[<Test>]
let ``Test Project26 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project26.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project26 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0


[<Test>]
let ``Test Project26 parameter symbols`` () =
    let wholeProjectResults = checker.ParseAndCheckProject(Project26.options) |> Async.RunImmediate

    let allUsesOfAllSymbols =
        wholeProjectResults.GetAllUsesOfAllSymbols()

        |> Array.map (fun su -> su.Symbol.ToString(), su.Symbol.DisplayName, Project13.cleanFileName su.FileName, tups su.Range, attribsOfSymbolUse su, attribsOfSymbol su.Symbol)


    let objSymbol = wholeProjectResults.GetAllUsesOfAllSymbols()  |> Array.find (fun su -> su.Symbol.DisplayName = "Class")
    let objEntity = objSymbol.Symbol :?> FSharpEntity

    let rec isByRef (ty: FSharpType) =
        if ty.IsAbbreviation then isByRef ty.AbbreviatedType
        else ty.HasTypeDefinition && ty.TypeDefinition.IsByRef

    // check we can get the CurriedParameterGroups
    let objMethodsCurriedParameterGroups =
        [ for x in objEntity.MembersFunctionsAndValues do
             for pg in x.CurriedParameterGroups do
                 for p in pg do
                     let attributeNames =
                        seq {
                            if p.IsParamArrayArg then yield "params"
                            if p.IsOutArg then yield "out"
                            if p.IsOptionalArg then yield "optional"
                        }
                        |> String.concat ","
                     yield x.CompiledName, p.Name,  p.Type.ToString(), isByRef p.Type, attributeNames ]

    objMethodsCurriedParameterGroups |> shouldEqual
          [("M1", Some "arg1", "type 'c", false, "");
           ("M1", Some "arg2", "type 'd Microsoft.FSharp.Core.option", false, "optional");
           ("M2", Some "arg1", "type 'a", false, "params");
           ("M2", Some "arg2", "type 'b", false, "optional");
           ("M3", Some "arg", "type Microsoft.FSharp.Core.byref<Microsoft.FSharp.Core.int>", true, "out")]

    // check we can get the ReturnParameter
    let objMethodsReturnParameter =
        [ for x in objEntity.MembersFunctionsAndValues do
             let p = x.ReturnParameter
             let attributeNames =
                 seq {
                    if p.IsParamArrayArg then yield "params"
                    if p.IsOutArg then yield "out"
                    if p.IsOptionalArg then yield "optional"
                 }
                 |> String.concat ","
             yield x.DisplayName, p.Name,  p.Type.ToString(), attributeNames ]
    set objMethodsReturnParameter |> shouldEqual
       (set
           [("( .ctor )", None, "type FSharpParameter.Class", "");
            ("M1", None, "type Microsoft.FSharp.Core.unit", "");
            ("M2", None, "type Microsoft.FSharp.Core.unit", "");
            ("M3", None, "type Microsoft.FSharp.Core.unit", "")])

module internal Project27 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module M

type CFoo() =
    abstract AbstractMethod: int -> string
    default _.AbstractMethod _ = "dflt"

type CFooImpl() =
    inherit CFoo()
    override _.AbstractMethod _ = "v"
"""
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)

[<Test>]
let ``Test project27 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project27.options) |> Async.RunImmediate
    wholeProjectResults .Diagnostics.Length |> shouldEqual 0

[<Test>]
let ``Test project27 all symbols in signature`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project27.options) |> Async.RunImmediate
    let allSymbols = allSymbolsInEntities true wholeProjectResults.AssemblySignature.Entities
    [ for x in allSymbols -> x.ToString(), attribsOfSymbol x ]
      |> shouldEqual
            [("M", ["module"]);
             ("CFoo", ["class"]);
             ("member .ctor", ["member"; "ctor"]);
             ("member AbstractMethod", ["slot"; "member"]);
             ("member AbstractMethod", ["member"; "overridemem"]);
             ("CFooImpl", ["class"]);
             ("member .ctor", ["member"; "ctor"]);
             ("member AbstractMethod", ["member"; "overridemem"])]

module internal Project28 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module M
open System
open System.Collections.Generic
let (|Even|Odd|) input = if input % 2 = 0 then Even else Odd
let TestNumber input =
   match input with
   | Even -> printfn "%d is even" input
   | Odd -> printfn "%d is odd" input
type DU = A of string | B of int
type XmlDocSigTest() =
    let event1 = new Event<_>()
    let event2 = new Event<_>()
    let aString = "fourtytwo"
    let anInt = 42
    member x.AProperty = Dictionary<int, string>()
    member x.AnotherProperty = aString
    member x.AMethod () = x.AProperty
    member x.AnotherMethod () = anInt
    [<CLIEvent>]
    member this.AnEvent = event1.Publish
    member this.AnotherEvent = event2.Publish
    member this.TestEvent1(arg) = event1.Trigger(this, arg)
    member this.TestEvent2(arg) = event2.Trigger(this, arg)

type Use() =
    let a = XmlDocSigTest ()
    do a.AnEvent.Add (fun _ -> () )
    member x.Test number =
        TestNumber 42
"""
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)
#if !NO_EXTENSIONTYPING
[<Test>]
let ``Test project28 all symbols in signature`` () =
    let wholeProjectResults = checker.ParseAndCheckProject(Project28.options) |> Async.RunImmediate
    let allSymbols = allSymbolsInEntities true wholeProjectResults.AssemblySignature.Entities
    let xmlDocSigs =
        allSymbols
        |> Seq.map (fun s ->
                        let typeName = s.GetType().Name
                        match s with
                        #if !NO_EXTENSIONTYPING
                        | :? FSharpEntity as fse -> typeName, fse.DisplayName, fse.XmlDocSig
                        #endif
                        | :? FSharpField as fsf -> typeName, fsf.DisplayName, fsf.XmlDocSig
                        | :? FSharpMemberOrFunctionOrValue as fsm -> typeName, fsm.DisplayName, fsm.XmlDocSig
                        | :? FSharpUnionCase as fsu -> typeName, fsu.DisplayName, fsu.XmlDocSig
                        | :? FSharpActivePatternCase as ap -> typeName, ap.DisplayName, ap.XmlDocSig
                        | :? FSharpGenericParameter as fsg -> typeName, fsg.DisplayName, ""
                        | :? FSharpParameter as fsp -> typeName, fsp.DisplayName, ""
                        #if !NO_EXTENSIONTYPING
                        | :? FSharpStaticParameter as fss -> typeName, fss.DisplayName, ""
                        #endif
                        | _ -> typeName, s.DisplayName, "unknown")
        |> Seq.toArray

    [|("FSharpEntity", "M", "T:M");
        ("FSharpMemberOrFunctionOrValue", "( |Even|Odd| )", "M:M.|Even|Odd|(System.Int32)");
        ("FSharpMemberOrFunctionOrValue", "TestNumber", "M:M.TestNumber(System.Int32)");
        ("FSharpEntity", "DU", "T:M.DU");
        ("FSharpUnionCase", "A", "T:M.DU.A");
        ("FSharpField", "Item", "T:M.DU.A");
        ("FSharpUnionCase", "B", "T:M.DU.B");
        ("FSharpField", "Item", "T:M.DU.B");
        ("FSharpEntity", "XmlDocSigTest", "T:M.XmlDocSigTest");
        ("FSharpMemberOrFunctionOrValue", "( .ctor )", "M:M.XmlDocSigTest.#ctor");
        ("FSharpMemberOrFunctionOrValue", "AMethod", "M:M.XmlDocSigTest.AMethod");
        ("FSharpMemberOrFunctionOrValue", "AnotherMethod", "M:M.XmlDocSigTest.AnotherMethod");
        ("FSharpMemberOrFunctionOrValue", "TestEvent1", "M:M.XmlDocSigTest.TestEvent1(System.Object)");
        ("FSharpMemberOrFunctionOrValue", "TestEvent2", "M:M.XmlDocSigTest.TestEvent2(System.Object)");
        ("FSharpMemberOrFunctionOrValue", "add_AnEvent", "M:M.XmlDocSigTest.add_AnEvent(Microsoft.FSharp.Control.FSharpHandler{System.Tuple{M.XmlDocSigTest,System.Object}})");
        ("FSharpMemberOrFunctionOrValue", "AProperty", "P:M.XmlDocSigTest.AProperty");
        ("FSharpMemberOrFunctionOrValue", "AnEvent", "P:M.XmlDocSigTest.AnEvent");
        ("FSharpMemberOrFunctionOrValue", "AnotherEvent", "P:M.XmlDocSigTest.AnotherEvent");
        ("FSharpMemberOrFunctionOrValue", "AnotherProperty", "P:M.XmlDocSigTest.AnotherProperty");
        ("FSharpMemberOrFunctionOrValue", "remove_AnEvent", "M:M.XmlDocSigTest.remove_AnEvent(Microsoft.FSharp.Control.FSharpHandler{System.Tuple{M.XmlDocSigTest,System.Object}})");
        ("FSharpMemberOrFunctionOrValue", "AnotherProperty", "P:M.XmlDocSigTest.AnotherProperty");
        ("FSharpMemberOrFunctionOrValue", "AnotherEvent", "P:M.XmlDocSigTest.AnotherEvent");
        ("FSharpMemberOrFunctionOrValue", "AnEvent", "P:M.XmlDocSigTest.AnEvent");
        ("FSharpMemberOrFunctionOrValue", "AProperty", "P:M.XmlDocSigTest.AProperty");
        ("FSharpField", "event1", "P:M.XmlDocSigTest.event1");
        ("FSharpField", "event2", "P:M.XmlDocSigTest.event2");
        ("FSharpField", "aString", "P:M.XmlDocSigTest.aString");
        ("FSharpField", "anInt", "P:M.XmlDocSigTest.anInt");
        ("FSharpEntity", "Use", "T:M.Use");
        ("FSharpMemberOrFunctionOrValue", "( .ctor )", "M:M.Use.#ctor");
        ("FSharpMemberOrFunctionOrValue", "Test", "M:M.Use.Test``1(``0)");
        ("FSharpGenericParameter", "?", "")|]
    |> Array.iter (fun x ->
        if xmlDocSigs |> Array.exists (fun y -> x = y) |> not then
            failwithf "%A does not exist in the collection." x
    )
#endif
module internal Project29 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module M
open System.ComponentModel
let f (x: INotifyPropertyChanged) = failwith ""
"""
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)


[<Test>]
let ``Test project29 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project29.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project29 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0

[<Test>]
let ``Test project29 event symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project29.options) |> Async.RunImmediate

    let objSymbol = wholeProjectResults.GetAllUsesOfAllSymbols()  |> Array.find (fun su -> su.Symbol.DisplayName = "INotifyPropertyChanged")
    let objEntity = objSymbol.Symbol :?> FSharpEntity

    let objMethodsCurriedParameterGroups =
        [ for x in objEntity.MembersFunctionsAndValues do
             for pg in x.CurriedParameterGroups do
                 for p in pg do
                     yield x.CompiledName, p.Name,  p.Type.Format(objSymbol.DisplayContext) ]

    objMethodsCurriedParameterGroups |> shouldEqual
          [("add_PropertyChanged", Some "value", "PropertyChangedEventHandler");
           ("remove_PropertyChanged", Some "value", "PropertyChangedEventHandler")]

    // check we can get the ReturnParameter
    let objMethodsReturnParameter =
        [ for x in objEntity.MembersFunctionsAndValues do
             let p = x.ReturnParameter
             yield x.DisplayName, p.Name, p.Type.Format(objSymbol.DisplayContext) ]
    set objMethodsReturnParameter |> shouldEqual
       (set
           [("PropertyChanged", None, "IEvent<PropertyChangedEventHandler,PropertyChangedEventArgs>");
           ("add_PropertyChanged", None, "unit");
           ("remove_PropertyChanged", None, "unit")])


module internal Project30 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Module
open System
type T() =
    [<Obsolete("hello")>]
    member _.Member = 0
"""
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options = checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)

let ``Test project30 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project30.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project30 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0

[<Test>]
let ``Test project30 Format attributes`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project30.options) |> Async.RunImmediate

    let moduleSymbol = wholeProjectResults.GetAllUsesOfAllSymbols()  |> Array.find (fun su -> su.Symbol.DisplayName = "Module")
    let moduleEntity = moduleSymbol.Symbol :?> FSharpEntity

    let moduleAttributes =
        [ for x in moduleEntity.Attributes do
             yield x.Format(moduleSymbol.DisplayContext), x.Format(FSharpDisplayContext.Empty) ]

    moduleAttributes
    |> set
    |> shouldEqual
         (set
            [("[<CompilationRepresentationAttribute (enum<CompilationRepresentationFlags> (4))>]",
              "[<Microsoft.FSharp.Core.CompilationRepresentationAttribute (enum<Microsoft.FSharp.Core.CompilationRepresentationFlags> (4))>]")])

    let memberSymbol = wholeProjectResults.GetAllUsesOfAllSymbols()  |> Array.find (fun su -> su.Symbol.DisplayName = "Member")
    let memberEntity = memberSymbol.Symbol :?> FSharpMemberOrFunctionOrValue

    let memberAttributes =
        [ for x in memberEntity.Attributes do
             yield x.Format(memberSymbol.DisplayContext), x.Format(FSharpDisplayContext.Empty) ]

    memberAttributes
    |> set
    |> shouldEqual
         (set
              [("""[<Obsolete ("hello")>]""",
                """[<System.Obsolete ("hello")>]""")])

module internal Project31 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module M
open System
open System.Collections.Generic
open System.Diagnostics
let f (x: List<'T>) = failwith ""
let g = Console.ReadKey()
"""
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)

    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)

let ``Test project31 whole project errors`` () =
    let wholeProjectResults = checker.ParseAndCheckProject(Project31.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project31 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0

[<Test>]
#if NETCOREAPP
[<Ignore("SKIPPED: Fails on .NET Core - DebuggerTypeProxyAttribute and DebuggerDisplayAttribute note being emitted?")>]
#endif
let ``Test project31 C# type attributes`` () =
    if not runningOnMono then
        let wholeProjectResults = checker.ParseAndCheckProject(Project31.options) |> Async.RunImmediate

        let objSymbol = wholeProjectResults.GetAllUsesOfAllSymbols()  |> Array.find (fun su -> su.Symbol.DisplayName = "List")
        let objEntity = objSymbol.Symbol :?> FSharpEntity
        let attributes = objEntity.Attributes |> Seq.filter (fun attrib -> attrib.AttributeType.DisplayName <> "__DynamicallyInvokableAttribute")

        [ for attrib in attributes do
             let args = try Seq.toList attrib.ConstructorArguments with _ -> []
             let namedArgs = try Seq.toList attrib.NamedArguments with _ -> []
             let output = sprintf "%A" (attrib.AttributeType, args, namedArgs)
             yield output.Replace("\r\n", "\n").Replace("\n", "") ]
        |> set
        |> shouldEqual
             (set [
                  "(DebuggerTypeProxyAttribute, [], [])";
                  """(DebuggerDisplayAttribute, [(type Microsoft.FSharp.Core.string, "Count = {Count}")], [])""";
                  """(DefaultMemberAttribute, [(type Microsoft.FSharp.Core.string, "Item")], [])""";
                  ])

[<Test>]
let ``Test project31 C# method attributes`` () =
    if not runningOnMono then
        let wholeProjectResults = checker.ParseAndCheckProject(Project31.options) |> Async.RunImmediate

        let objSymbol = wholeProjectResults.GetAllUsesOfAllSymbols()  |> Array.find (fun su -> su.Symbol.DisplayName = "Console")
        let objEntity = objSymbol.Symbol :?> FSharpEntity

        let objMethodsAttributes =
            [ for x in objEntity.MembersFunctionsAndValues do
                 for attrib in x.Attributes do
                    let args = try Seq.toList attrib.ConstructorArguments with _ -> []
                    let namedArgs = try Seq.toList attrib.NamedArguments with _ -> []
                    yield sprintf "%A" (attrib.AttributeType, args, namedArgs) ]

        objMethodsAttributes
        |> set
        |> shouldEqual
              (set [
#if !NETCOREAPP
                   "(SecuritySafeCriticalAttribute, [], [])";
#endif
                   "(CLSCompliantAttribute, [(type Microsoft.FSharp.Core.bool, false)], [])"])

[<Test>]
#if NETCOREAPP
[<Ignore("SKIPPED: Fails on .NET Core - DebuggerTypeProxyAttribute and DebuggerDisplayAttribute note being emitted?")>]
#endif
let ``Test project31 Format C# type attributes`` () =
    if not runningOnMono then
        let wholeProjectResults = checker.ParseAndCheckProject(Project31.options) |> Async.RunImmediate

        let objSymbol = wholeProjectResults.GetAllUsesOfAllSymbols()  |> Array.find (fun su -> su.Symbol.DisplayName = "List")
        let objEntity = objSymbol.Symbol :?> FSharpEntity
        let attributes = objEntity.Attributes |> Seq.filter (fun attrib -> attrib.AttributeType.DisplayName <> "__DynamicallyInvokableAttribute")

        [ for attrib in attributes -> attrib.Format(objSymbol.DisplayContext) ]
        |> set
        |> shouldEqual
             (set ["[<DebuggerTypeProxyAttribute (typeof<Mscorlib_CollectionDebugView<>>)>]";
                   """[<DebuggerDisplayAttribute ("Count = {Count}")>]""";
                   """[<Reflection.DefaultMemberAttribute ("Item")>]""";
                   ])

[<Test>]
let ``Test project31 Format C# method attributes`` () =
    if not runningOnMono then
        let wholeProjectResults = checker.ParseAndCheckProject(Project31.options) |> Async.RunImmediate

        let objSymbol = wholeProjectResults.GetAllUsesOfAllSymbols()  |> Array.find (fun su -> su.Symbol.DisplayName = "Console")
        let objEntity = objSymbol.Symbol :?> FSharpEntity

        let objMethodsAttributes =
            [ for x in objEntity.MembersFunctionsAndValues do
                 for attrib in x.Attributes -> attrib.Format(objSymbol.DisplayContext) ]

        objMethodsAttributes
        |> set
        |> shouldEqual
              (set ["[<CLSCompliantAttribute (false)>]";
#if !NETCOREAPP
                    "[<Security.SecuritySafeCriticalAttribute ()>]";
#endif
                    ])

module internal Project32 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let sigFileName1 = Path.ChangeExtension(fileName1, ".fsi")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module Sample
let func x = x + 1
    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)

    let sigFileSource1 = """
module Sample

val func : int -> int
    """
    FileSystem.OpenFileForWriteShim(sigFileName1).Write(sigFileSource1)
    let cleanFileName a = if a = fileName1 then "file1" elif a = sigFileName1 then "sig1"  else "??"

    let fileNames = [sigFileName1; fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)


[<Test>]
let ``Test Project32 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project32.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project32 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0

[<Test>]
let ``Test Project32 should be able to find sig symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project32.options) |> Async.RunImmediate
    let _sigBackgroundParseResults1, sigBackgroundTypedParse1 =
        checker.GetBackgroundCheckResultsForFileInProject(Project32.sigFileName1, Project32.options)
        |> Async.RunImmediate

    let sigSymbolUseOpt = sigBackgroundTypedParse1.GetSymbolUseAtLocation(4,5,"",["func"])
    let sigSymbol = sigSymbolUseOpt.Value.Symbol

    let usesOfSigSymbol =
        [ for su in wholeProjectResults.GetUsesOfSymbol(sigSymbol)  do
              yield Project32.cleanFileName su.FileName , tups su.Range, attribsOfSymbol su.Symbol ]

    usesOfSigSymbol |> shouldEqual
       [("sig1", ((4, 4), (4, 8)), ["val"]);
        ("file1", ((3, 4), (3, 8)), ["val"])]

[<Test>]
let ``Test Project32 should be able to find impl symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project32.options) |> Async.RunImmediate
    let _implBackgroundParseResults1, implBackgroundTypedParse1 =
        checker.GetBackgroundCheckResultsForFileInProject(Project32.fileName1, Project32.options)
        |> Async.RunImmediate

    let implSymbolUseOpt = implBackgroundTypedParse1.GetSymbolUseAtLocation(3,5,"",["func"])
    let implSymbol = implSymbolUseOpt.Value.Symbol

    let usesOfImplSymbol =
        [ for su in wholeProjectResults.GetUsesOfSymbol(implSymbol)  do
              yield Project32.cleanFileName su.FileName , tups su.Range, attribsOfSymbol su.Symbol ]

    usesOfImplSymbol |> shouldEqual
       [("sig1", ((4, 4), (4, 8)), ["val"]);
        ("file1", ((3, 4), (3, 8)), ["val"])]

module internal Project33 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module Impl
open System.Runtime.CompilerServices

type System.Int32 with
    member x.SetValue (_: int) = ()
    member x.GetValue () = x
"""
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)
    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)

[<Test>]
let ``Test Project33 whole project errors`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project33.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project33 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0

[<Test>]
let ``Test Project33 extension methods`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project33.options) |> Async.RunImmediate
    let allSymbolsUses = wholeProjectResults.GetAllUsesOfAllSymbols()

    let implModuleUse = allSymbolsUses |> Array.find (fun su -> su.Symbol.DisplayName = "Impl")
    let implModuleDefn = implModuleUse.Symbol :?> FSharpEntity

    [
      for x in implModuleDefn.MembersFunctionsAndValues -> x.LogicalName, attribsOfSymbol x
    ]
    |> shouldEqual
            [("SetValue", ["member"; "extmem"]);
             ("GetValue", ["member"; "extmem"])]

module internal Project34 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module Dummy
"""
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)
    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args =
        [|
            yield! mkProjectCommandLineArgs (dllName, fileNames)
            // We use .NET-buit version of System.Data.dll since the tests depend on implementation details
            // i.e. the private type System.Data.Listeners may not be available on Mono.
            yield @"-r:" + Path.Combine(__SOURCE_DIRECTORY__, Path.Combine("data", "System.Data.dll"))
        |]
    let options = checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)

[<Test>]
let ``Test Project34 whole project errors`` () =
    let wholeProjectResults = checker.ParseAndCheckProject(Project34.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "Project34 error: <<<%s>>>" e.Message
    wholeProjectResults.Diagnostics.Length |> shouldEqual 0

[<Test>]
#if NETCOREAPP
[<Ignore("SKIPPED: need to check if these tests can be enabled for .NET Core testing of FSharp.Compiler.Service")>]
#endif
let ``Test project34 should report correct accessibility for System.Data.Listeners`` () =
    let wholeProjectResults = checker.ParseAndCheckProject(Project34.options) |> Async.RunImmediate
    let rec getNestedEntities (entity: FSharpEntity) =
        seq { yield entity
              for e in entity.NestedEntities do
                  yield! getNestedEntities e }
    let listenerEntity =
        wholeProjectResults.ProjectContext.GetReferencedAssemblies()
        |> List.tryPick (fun asm -> if asm.SimpleName.Contains("System.Data") then Some asm.Contents.Entities else None)
        |> Option.get
        |> Seq.collect getNestedEntities
        |> Seq.tryFind (fun entity ->
            entity.TryFullName
            |> Option.map (fun s -> s.Contains("System.Data.Listeners"))
            |> fun arg -> defaultArg arg false)
        |> Option.get
    listenerEntity.Accessibility.IsPrivate |> shouldEqual true

    let listenerFuncEntity =
        listenerEntity.NestedEntities
        |> Seq.tryFind (fun entity ->
            entity.TryFullName
            |> Option.map (fun s -> s.Contains("Func"))
            |> fun arg -> defaultArg arg false)
        |> Option.get

    listenerFuncEntity.Accessibility.IsPrivate |> shouldEqual true


//------------------------------------------------------

module internal Project35 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
type Test =
    let curriedFunction (one:int) (two:float) (three:string) =
        float32 (one + int two + int three)
    let tupleFunction (one:int, two:float, three:string) =
        float32 (one + int two + int three)
"""
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)
    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)


[<Test>]
let ``Test project35 CurriedParameterGroups should be available for nested functions`` () =
    let wholeProjectResults = checker.ParseAndCheckProject(Project35.options) |> Async.RunImmediate
    let allSymbolUses = wholeProjectResults.GetAllUsesOfAllSymbols()
    let findByDisplayName name =
        Array.find (fun (su:FSharpSymbolUse) -> su.Symbol.DisplayName = name)

    let curriedFunction = allSymbolUses |> findByDisplayName "curriedFunction"

    match curriedFunction.Symbol with
    | :? FSharpMemberOrFunctionOrValue as mfv ->

        let curriedParamGroups =
            mfv.CurriedParameterGroups
            |> Seq.map Seq.toList
            |> Seq.toList

        //check the parameters
        match curriedParamGroups with
        | [[param1];[param2];[param3]] ->
            param1.Type.TypeDefinition.DisplayName |> shouldEqual "int"
            param2.Type.TypeDefinition.DisplayName |> shouldEqual "float"
            param3.Type.TypeDefinition.DisplayName |> shouldEqual "string"
        | _ -> failwith "Unexpected parameters"

        //now check the return type
        let retTyp = mfv.ReturnParameter
        retTyp.Type.TypeDefinition.DisplayName |> shouldEqual "float32"

    | _ -> failwith "Unexpected symbol type"

    let tupledFunction = allSymbolUses |> findByDisplayName "tupleFunction"
    match tupledFunction.Symbol with
    | :? FSharpMemberOrFunctionOrValue as mfv ->

        let curriedParamGroups =
            mfv.CurriedParameterGroups
            |> Seq.map Seq.toList
            |> Seq.toList

        //check the parameters
        match curriedParamGroups with
        | [[param1;param2;param3]] ->
            param1.Type.TypeDefinition.DisplayName |> shouldEqual "int"
            param2.Type.TypeDefinition.DisplayName |> shouldEqual "float"
            param3.Type.TypeDefinition.DisplayName |> shouldEqual "string"
        | _ -> failwith "Unexpected parameters"

        //now check the return type
        let retTyp = mfv.ReturnParameter
        retTyp.Type.TypeDefinition.DisplayName |> shouldEqual "float32"

    | _ -> failwith "Unexpected symbol type"

//------------------------------------------------------

module internal Project35b =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fsx")
    let fileSource1Text = """
#r "System.dll"
#r "notexist.dll"
"""
    let fileSource1 = SourceText.ofString fileSource1Text
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1Text)
    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
#if NETCOREAPP
    let projPath = Path.ChangeExtension(fileName1, ".fsproj")
    let dllPath = Path.ChangeExtension(fileName1, ".dll")
    let args = mkProjectCommandLineArgs(dllPath, fileNames)
    let args2 = Array.append args [| "-r:notexist.dll" |]
    let options = checker.GetProjectOptionsFromCommandLineArgs (projPath, args2)
#else
    let options =  checker.GetProjectOptionsFromScript(fileName1, fileSource1) |> Async.RunImmediate |> fst
#endif

[<Test>]
let ``Test project35b Dependency files for ParseAndCheckFileInProject`` () =
    let checkFileResults =
        checker.ParseAndCheckFileInProject(Project35b.fileName1, 0, Project35b.fileSource1, Project35b.options) |> Async.RunImmediate
        |> function
            | _, FSharpCheckFileAnswer.Succeeded(res) -> res
            | _ -> failwithf "Parsing aborted unexpectedly..."
    for d in checkFileResults.DependencyFiles do
        printfn "ParseAndCheckFileInProject dependency: %s" d
    checkFileResults.DependencyFiles |> Array.exists (fun s -> s.Contains "notexist.dll") |> shouldEqual true
    // The file itself is not a dependency since it is never read from the file system when using ParseAndCheckFileInProject
    checkFileResults.DependencyFiles |> Array.exists (fun s -> s.Contains Project35b.fileName1) |> shouldEqual false

[<Test>]
let ``Test project35b Dependency files for GetBackgroundCheckResultsForFileInProject`` () =
    let _,checkFileResults = checker.GetBackgroundCheckResultsForFileInProject(Project35b.fileName1, Project35b.options) |> Async.RunImmediate
    for d in checkFileResults.DependencyFiles do
        printfn "GetBackgroundCheckResultsForFileInProject dependency: %s" d
    checkFileResults.DependencyFiles |> Array.exists (fun s -> s.Contains "notexist.dll") |> shouldEqual true
    // The file is a dependency since it is read from the file system when using GetBackgroundCheckResultsForFileInProject
    checkFileResults.DependencyFiles |> Array.exists (fun s -> s.Contains Project35b.fileName1) |> shouldEqual true

[<Test>]
let ``Test project35b Dependency files for check of project`` () =
    let checkResults = checker.ParseAndCheckProject(Project35b.options) |> Async.RunImmediate
    for d in checkResults.DependencyFiles do
        printfn "ParseAndCheckProject dependency: %s" d
    checkResults.DependencyFiles |> Array.exists (fun s -> s.Contains "notexist.dll") |> shouldEqual true
    checkResults.DependencyFiles |> Array.exists (fun s -> s.Contains Project35b.fileName1) |> shouldEqual true

//------------------------------------------------------

module internal Project36 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """module Project36
type A(i:int) =
    member x.Value = i

type B(i:int) as b =
    inherit A(i*2)
    let a = b.Overload(i)
    member x.Overload() = a
    member x.Overload(y: int) = y + y
    member x.BaseValue = base.Value

let [<Literal>] lit = 1.0
let notLit = 1.0
let callToOverload = B(5).Overload(4)
"""
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)
    let cleanFileName a = if a = fileName1 then "file1" else "??"

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)

[<Test>]
let ``Test project36 FSharpMemberOrFunctionOrValue.IsBaseValue`` () =
    let keepAssemblyContentsChecker = FSharpChecker.Create(keepAssemblyContents=true)
    let options =  keepAssemblyContentsChecker.GetProjectOptionsFromCommandLineArgs (Project36.projFileName, Project36.args)
    let wholeProjectResults =
        keepAssemblyContentsChecker.ParseAndCheckProject(options)
        |> Async.RunImmediate

    wholeProjectResults.GetAllUsesOfAllSymbols()
    |> Array.pick (fun (su:FSharpSymbolUse) ->
        if su.Symbol.DisplayName = "base"
        then Some (su.Symbol :?> FSharpMemberOrFunctionOrValue)
        else None)
    |> fun baseSymbol -> shouldEqual true baseSymbol.IsBaseValue

[<Test>]
let ``Test project36 FSharpMemberOrFunctionOrValue.IsConstructorThisValue & IsMemberThisValue`` () =
    let keepAssemblyContentsChecker = FSharpChecker.Create(keepAssemblyContents=true)
    let options =  keepAssemblyContentsChecker.GetProjectOptionsFromCommandLineArgs (Project36.projFileName, Project36.args)
    let wholeProjectResults = keepAssemblyContentsChecker.ParseAndCheckProject(options) |> Async.RunImmediate
    let declarations =
        let checkedFile = wholeProjectResults.AssemblyContents.ImplementationFiles.[0]
        match checkedFile.Declarations.[0] with
        | FSharpImplementationFileDeclaration.Entity (_, subDecls) -> subDecls
        | _ -> failwith "unexpected declaration"
    let getExpr exprIndex =
        match declarations.[exprIndex] with
        | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(_,_,e) -> e
        | FSharpImplementationFileDeclaration.InitAction e -> e
        | _ -> failwith "unexpected declaration"
    // Instead of checking the symbol uses directly, walk the typed tree to check
    // the correct values are also visible from there. Also note you cannot use
    // ThisValue in these cases, this is only used when the symbol
    // is implicit in the constructor
    match getExpr 4 with
    | Let((b,_),_) ->
        b.IsConstructorThisValue && not b.IsMemberThisValue
    | _ -> failwith "unexpected expression"
    |> shouldEqual true

    match getExpr 5 with
    | FSharpFieldGet(Some(Value x),_,_) ->
        x.IsMemberThisValue && not x.IsConstructorThisValue
    | _ -> failwith "unexpected expression"
    |> shouldEqual true

    match getExpr 6 with
    | Call(_,_,_,_,[Value s;_]) ->
        not s.IsMemberThisValue && not s.IsConstructorThisValue
    | _ -> failwith "unexpected expression"
    |> shouldEqual true

[<Test>]
let ``Test project36 FSharpMemberOrFunctionOrValue.LiteralValue`` () =
    let keepAssemblyContentsChecker = FSharpChecker.Create(keepAssemblyContents=true)
    let options =  keepAssemblyContentsChecker.GetProjectOptionsFromCommandLineArgs (Project36.projFileName, Project36.args)
    let wholeProjectResults = keepAssemblyContentsChecker.ParseAndCheckProject(options) |> Async.RunImmediate
    let project36Module = wholeProjectResults.AssemblySignature.Entities.[0]
    let lit = project36Module.MembersFunctionsAndValues.[0]
    shouldEqual true (lit.LiteralValue.Value |> unbox |> (=) 1.)

    let notLit = project36Module.MembersFunctionsAndValues.[1]
    shouldEqual true notLit.LiteralValue.IsNone

module internal Project37 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let fileName2 = Path.ChangeExtension(base2, ".fs")
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
namespace AttrTests
type X = int list
[<System.AttributeUsage(System.AttributeTargets.Method ||| System.AttributeTargets.Assembly) >]
type AttrTestAttribute() =
    inherit System.Attribute()

    new (t: System.Type) = AttrTestAttribute()
    new (t: System.Type[]) = AttrTestAttribute()
    new (t: int[]) = AttrTestAttribute()

[<System.AttributeUsage(System.AttributeTargets.Assembly) >]
type AttrTest2Attribute() =
    inherit System.Attribute()

type TestUnion = | A of string
type TestRecord = { B : int }

module Test =
    [<AttrTest(typeof<int>)>]
    let withType = 0
    [<AttrTest(typeof<list<int>>)>]
    let withGenericType = 0
    [<AttrTest(typeof<int * int>)>]
    let withTupleType = 0
    [<AttrTest(typeof<int -> int>)>]
    let withFuncType = 0
    [<AttrTest([| typeof<TestUnion>; typeof<TestRecord> |])>]
    let withTypeArray = 0
    [<AttrTest([| 0; 1; 2 |])>]
    let withIntArray = 0
    module NestedModule =
        type NestedRecordType = { B : int }

[<assembly: AttrTest()>]
do ()
"""
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)
    let fileSource2 = """
namespace AttrTests

[<assembly: AttrTest2()>]
do ()
"""
    FileSystem.OpenFileForWriteShim(fileName2).Write(fileSource2)
    let fileNames = [fileName1; fileName2]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)

[<Test>]
let ``Test project37 typeof and arrays in attribute constructor arguments`` () =
    let wholeProjectResults =
        checker.ParseAndCheckProject(Project37.options)
        |> Async.RunImmediate
    let allSymbolsUses = wholeProjectResults.GetAllUsesOfAllSymbols()
    for su in allSymbolsUses do
        match su.Symbol with
        | :? FSharpMemberOrFunctionOrValue as funcSymbol ->
            let getAttrArg() =
                let arg = funcSymbol.Attributes.[0].ConstructorArguments.[0] |> snd
                arg :?> FSharpType
            match funcSymbol.DisplayName with
            | "withType" ->
                let t = getAttrArg()
                t.TypeDefinition.DisplayName |> shouldEqual "int"
            | "withGenericType" ->
                let t = getAttrArg()
                t.TypeDefinition.DisplayName |> shouldEqual "list"
                t.GenericArguments.[0].TypeDefinition.DisplayName |> shouldEqual "int"
            | "withTupleType" ->
                let t = getAttrArg()
                t.IsTupleType |> shouldEqual true
                t.GenericArguments.[0].TypeDefinition.DisplayName |> shouldEqual "int"
                t.GenericArguments.[1].TypeDefinition.DisplayName |> shouldEqual "int"
            | "withFuncType" ->
                let t = getAttrArg()
                t.IsFunctionType |> shouldEqual true
                t.GenericArguments.[0].TypeDefinition.DisplayName |> shouldEqual "int"
                t.GenericArguments.[1].TypeDefinition.DisplayName |> shouldEqual "int"
            | "withTypeArray" ->
                let attr = funcSymbol.Attributes.[0].ConstructorArguments.[0] |> snd
                let ta = attr :?> obj[] |> Array.map (fun t -> t :?> FSharpType)
                ta.[0].TypeDefinition.DisplayName |> shouldEqual "TestUnion"
                ta.[1].TypeDefinition.DisplayName |> shouldEqual "TestRecord"
            | "withIntArray" ->
                let attr = funcSymbol.Attributes.[0].ConstructorArguments.[0] |> snd
                let a = attr :?> obj[] |> Array.map (fun t -> t :?> int)
                a |> shouldEqual [| 0; 1; 2 |]
            | _ -> ()
        | _ -> ()

    let mscorlibAsm =
        wholeProjectResults.ProjectContext.GetReferencedAssemblies()
        |> Seq.find (fun a -> a.SimpleName = "mscorlib")
    printfn "Attributes found in mscorlib: %A" mscorlibAsm.Contents.Attributes
    shouldEqual (mscorlibAsm.Contents.Attributes.Count > 0) true

    let fsharpCoreAsm =
        wholeProjectResults.ProjectContext.GetReferencedAssemblies()
        |> Seq.find (fun a -> a.SimpleName = "FSharp.Core")
    printfn "Attributes found in FSharp.Core: %A" fsharpCoreAsm.Contents.Attributes
    shouldEqual (fsharpCoreAsm.Contents.Attributes.Count > 0) true

[<Test>]
let ``Test project37 DeclaringEntity`` () =
    let wholeProjectResults =
        checker.ParseAndCheckProject(Project37.options)
        |> Async.RunImmediate
    let allSymbolsUses = wholeProjectResults.GetAllUsesOfAllSymbols()
    for sym in allSymbolsUses do
       match sym.Symbol with
       | :? FSharpEntity as e when not e.IsNamespace || e.AccessPath.Contains(".") ->
           printfn "checking declaring type of entity '%s' --> '%s', assembly = '%s'" e.AccessPath e.CompiledName (e.Assembly.ToString())
           shouldEqual e.DeclaringEntity.IsSome true
           match e.CompiledName with
           | "AttrTestAttribute" ->
               shouldEqual e.AccessPath "AttrTests"
           | "int" ->
               shouldEqual e.AccessPath "Microsoft.FSharp.Core"
               shouldEqual e.DeclaringEntity.Value.AccessPath "Microsoft.FSharp"
           | "list`1" ->
               shouldEqual e.AccessPath "Microsoft.FSharp.Collections"
               shouldEqual e.DeclaringEntity.Value.AccessPath "Microsoft.FSharp"
               shouldEqual e.DeclaringEntity.Value.DeclaringEntity.IsSome true
               shouldEqual e.DeclaringEntity.Value.DeclaringEntity.Value.IsNamespace true
               shouldEqual e.DeclaringEntity.Value.DeclaringEntity.Value.AccessPath "Microsoft"
               shouldEqual e.DeclaringEntity.Value.DeclaringEntity.Value.DeclaringEntity.Value.DeclaringEntity.IsSome false
           | "Attribute" ->
               shouldEqual e.AccessPath "System"
               shouldEqual e.DeclaringEntity.Value.AccessPath "global"
           | "NestedRecordType" ->
                shouldEqual e.AccessPath "AttrTests.Test.NestedModule"
                shouldEqual e.DeclaringEntity.Value.AccessPath "AttrTests.Test"
                shouldEqual e.DeclaringEntity.Value.DeclaringEntity.Value.AccessPath "AttrTests"
                shouldEqual e.DeclaringEntity.Value.DeclaringEntity.Value.DeclaringEntity.Value.AccessPath "global"
           | _ -> ()
       | :? FSharpMemberOrFunctionOrValue as e when e.IsModuleValueOrMember ->
           printfn "checking declaring type of value '%s', assembly = '%s'" e.CompiledName (e.Assembly.ToString())
           shouldEqual e.DeclaringEntity.IsSome true
       | _ ->  ()

//-----------------------------------------------------------


module internal Project38 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
namespace OverrideTests

type I<'X> =
    abstract Method : unit -> unit
    abstract Generic : named:'X -> unit
    abstract Generic<'Y> : 'X * 'Y -> unit
    abstract Property : int

[<AbstractClass>]
type B<'Y>() =
    abstract Method : unit -> unit
    abstract Generic : 'Y -> unit
    abstract Property : int
    [<CLIEvent>]
    abstract Event : IEvent<unit>

type A<'XX, 'YY>() =
    inherit B<'YY>()

    let ev = Event<unit>()

    override this.Method() = ()
    override this.Generic (a: 'YY) = ()
    override this.Property = 0
    [<CLIEvent>]
    override this.Event = ev.Publish

    member this.NotOverride() = ()

    interface I<'XX> with
        member this.Method() = ()
        member this.Generic (a: 'XX) = ()
        member this.Generic<'Y> (a: 'XX, b: 'Y) = ()
        member this.Property = 1
"""
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)
    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)

[<Test>]
let ``Test project38 abstract slot information`` () =
    let wholeProjectResults =
        checker.ParseAndCheckProject(Project38.options)
        |> Async.RunImmediate
    let printAbstractSignature (s: FSharpAbstractSignature) =
        let printType (t: FSharpType) =
            hash t  |> ignore // smoke test to check hash code doesn't loop
            (string t).[5 ..]
        let args =
            (s.AbstractArguments |> Seq.concat |> Seq.map (fun a ->
                (match a.Name with Some n -> n + ":" | _ -> "") + printType a.Type) |> String.concat " * ")
            |> function "" -> "()" | a -> a
        let tgen =
            match s.DeclaringTypeGenericParameters |> Seq.map (fun m -> "'" + m.Name) |> String.concat "," with
            | "" -> ""
            | g -> " original generics: <" + g + ">"
        let mgen =
            match s.MethodGenericParameters |> Seq.map (fun m -> "'" + m.Name) |> String.concat "," with
            | "" -> ""
            | g -> "<" + g + ">"
        "type " + printType s.DeclaringType + tgen + " with member " + s.Name + mgen + " : " + args + " -> " +
        printType s.AbstractReturnType

    let results =
        let a2ent = wholeProjectResults.AssemblySignature.Entities |> Seq.find (fun e -> e.FullName = "OverrideTests.A`2")
        a2ent.MembersFunctionsAndValues |> Seq.map (fun m ->
            m.CompiledName, (m.ImplementedAbstractSignatures |> Seq.map printAbstractSignature |> List.ofSeq)
        )
        |> Array.ofSeq

    [|
        ".ctor", []
        "Generic", ["type OverrideTests.B<'YY> original generics: <'Y> with member Generic : 'Y -> Microsoft.FSharp.Core.unit"]
        "OverrideTests.I<'XX>.Generic", ["type OverrideTests.I<'XX> original generics: <'X> with member Generic : named:'X -> Microsoft.FSharp.Core.unit"]
        "OverrideTests.I<'XX>.Generic", ["type OverrideTests.I<'XX> original generics: <'X> with member Generic<'Y> : 'X * 'Y -> Microsoft.FSharp.Core.unit"]
        "Method", ["type OverrideTests.B<'YY> original generics: <'Y> with member Method : () -> Microsoft.FSharp.Core.unit"]
        "OverrideTests.I<'XX>.Method", ["type OverrideTests.I<'XX> original generics: <'X> with member Method : () -> Microsoft.FSharp.Core.unit"]
        "NotOverride", []
        "add_Event", ["type OverrideTests.B<'YY> original generics: <'Y> with member add_Event : Microsoft.FSharp.Control.Handler<Microsoft.FSharp.Core.unit> -> Microsoft.FSharp.Core.unit"]
        "get_Event", ["type OverrideTests.B<'YY> with member get_Event : () -> Microsoft.FSharp.Core.unit"]
        "get_Property", ["type OverrideTests.B<'YY> original generics: <'Y> with member get_Property : () -> Microsoft.FSharp.Core.int"]
        "OverrideTests.I<'XX>.get_Property", ["type OverrideTests.I<'XX> original generics: <'X> with member get_Property : () -> Microsoft.FSharp.Core.int"]
        "remove_Event", ["type OverrideTests.B<'YY> original generics: <'Y> with member remove_Event : Microsoft.FSharp.Control.Handler<Microsoft.FSharp.Core.unit> -> Microsoft.FSharp.Core.unit"]
        "get_Property", ["type OverrideTests.B<'YY> original generics: <'Y> with member get_Property : () -> Microsoft.FSharp.Core.int"]
        "get_Event", ["type OverrideTests.B<'YY> with member get_Event : () -> Microsoft.FSharp.Core.unit"]
    |]
    |> Array.iter (fun x ->
        if results |> Array.exists (fun y -> x = y) |> not then
            failwithf "%A does not exist in the collection." x
    )


//--------------------------------------------

module internal Project39 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module M

let functionWithIncompleteSignature x = System.ThisDoesntExist.SomeMethod(x)
let curriedFunctionWithIncompleteSignature (x1:'a) x2 (x3:'a,x4) =
    (x2 = x4) |> ignore
    System.ThisDoesntExist.SomeMethod(x1,x2,x3,x4)

type C() =
    member x.MemberWithIncompleteSignature x = System.ThisDoesntExist.SomeMethod(x)
    member x.CurriedMemberWithIncompleteSignature (x1:'a) x2 (x3:'a,x4) =
        (x2 = x4) |> ignore
        System.ThisDoesntExist.SomeMethod(x1,x2,x3,x4)

let uses () =
   functionWithIncompleteSignature (failwith "something")
   curriedFunctionWithIncompleteSignature (failwith "x1") (failwith "x2") (failwith "x3", failwith "x4")
   C().MemberWithIncompleteSignature (failwith "something")
   C().CurriedMemberWithIncompleteSignature (failwith "x1") (failwith "x2") (failwith "x3", failwith "x4")
    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)
    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)
    let cleanFileName a = if a = fileName1 then "file1" else "??"

[<Test>]
let ``Test project39 all symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project39.options) |> Async.RunImmediate
    let allSymbolUses = wholeProjectResults.GetAllUsesOfAllSymbols()
    let typeTextOfAllSymbolUses =
        [ for s in allSymbolUses do
            match s.Symbol with
            | :? FSharpMemberOrFunctionOrValue as mem ->
              if s.Symbol.DisplayName.Contains "Incomplete" then
                yield s.Symbol.DisplayName, tups s.Range,
                      ("full", mem.FullType |> FSharpType.Prettify |> fun p -> p.Format(s.DisplayContext)),
                      ("params", mem.CurriedParameterGroups |> FSharpType.Prettify |> Seq.toList |> List.map (Seq.toList >> List.map (fun p -> p.Type.Format(s.DisplayContext)))),
                      ("return", mem.ReturnParameter |> FSharpType.Prettify |> fun p -> p.Type.Format(s.DisplayContext))
            | _ -> () ]
    typeTextOfAllSymbolUses |> shouldEqual
              [("functionWithIncompleteSignature", ((4, 4), (4, 35)),
                ("full", "'a -> 'b"), ("params", [["'a"]]), ("return", "'b"));
               ("curriedFunctionWithIncompleteSignature", ((5, 4), (5, 42)),
                ("full", "'a -> 'a0 -> 'a * 'a0 -> 'b"),
                ("params",
                 [["'a"]; ["'a0"]; ["'a"; "'a0"]]),
                ("return", "'b"));
               ("MemberWithIncompleteSignature", ((10, 13), (10, 42)),
                ("full", "C -> 'c -> 'd"), ("params", [["'c"]]), ("return", "'d"));
               ("CurriedMemberWithIncompleteSignature", ((11, 13), (11, 49)),
                ("full", "C -> 'a -> 'a0 -> 'a * 'a0 -> 'b"),
                ("params",
                 [["'a"]; ["'a0"]; ["'a"; "'a0"]]),
                ("return", "'b"));
               ("functionWithIncompleteSignature", ((16, 3), (16, 34)),
                ("full", "'a -> 'b"), ("params", [["'a"]]), ("return", "'b"));
               ("curriedFunctionWithIncompleteSignature", ((17, 3), (17, 41)),
                ("full", "'a -> 'a0 -> 'a * 'a0 -> 'b"),
                ("params",
                 [["'a"]; ["'a0"]; ["'a"; "'a0"]]),
                ("return", "'b"));
               ("MemberWithIncompleteSignature", ((18, 3), (18, 36)),
                ("full", "'c -> 'd"), ("params", [["'c"]]), ("return", "'d"));
               ("CurriedMemberWithIncompleteSignature", ((19, 3), (19, 43)),
                ("full", "'a -> 'a0 -> 'a * 'a0 -> 'b"),
                ("params",
                 [["'a"]; ["'a0"]; ["'a"; "'a0"]]),
                ("return", "'b"))]


//--------------------------------------------

module internal Project40 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module M

let f (x: option<_>) = x.IsSome, x.IsNone

[<CompilationRepresentation(CompilationRepresentationFlags.UseNullAsTrueValue)>]
type C =
    | A
    | B of string
    member x.IsItAnA = match x with A -> true | B _ -> false
    member x.IsItAnAMethod() = match x with A -> true | B _ -> false

let g (x: C) = x.IsItAnA,x.IsItAnAMethod()
    """

    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)
    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)
    let cleanFileName a = if a = fileName1 then "file1" else "??"

[<Test>]
let ``Test Project40 all symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project40.options) |> Async.RunImmediate
    let allSymbolUses = wholeProjectResults.GetAllUsesOfAllSymbols()
    let allSymbolUsesInfo =  [ for s in allSymbolUses -> s.Symbol.DisplayName, tups s.Range, attribsOfSymbol s.Symbol ]
    allSymbolUsesInfo |> shouldEqual
          [("option", ((4, 10), (4, 16)), ["abbrev"]); ("x", ((4, 7), (4, 8)), []);
           ("x", ((4, 23), (4, 24)), []);
           ("IsSome", ((4, 23), (4, 31)), ["member"; "prop"; "funky"]);
           ("x", ((4, 33), (4, 34)), []);
           ("IsNone", ((4, 33), (4, 41)), ["member"; "prop"; "funky"]);
           ("f", ((4, 4), (4, 5)), ["val"]);
           ("CompilationRepresentationAttribute", ((6, 2), (6, 27)), ["class"]);
           ("CompilationRepresentationAttribute", ((6, 2), (6, 27)), ["class"]);
           ("CompilationRepresentationAttribute", ((6, 2), (6, 27)), ["member"]);
           ("CompilationRepresentationFlags", ((6, 28), (6, 58)),
            ["enum"; "valuetype"]);
           ("UseNullAsTrueValue", ((6, 28), (6, 77)), ["field"; "static"; "8"]);
           ("string", ((9, 11), (9, 17)), ["abbrev"]);
           ("string", ((9, 11), (9, 17)), ["abbrev"]); ("A", ((8, 6), (8, 7)), []);
           ("B", ((9, 6), (9, 7)), []); ("C", ((7, 5), (7, 6)), ["union"]);
           ("IsItAnA", ((10, 13), (10, 20)), ["member"; "getter"; "funky"]);
           ("IsItAnAMethod", ((11, 13), (11, 26)), ["member"; "funky"]);
           ("x", ((10, 11), (10, 12)), []); ("x", ((10, 29), (10, 30)), []);
           ("A", ((10, 36), (10, 37)), []); ("B", ((10, 48), (10, 49)), []);
           ("x", ((11, 11), (11, 12)), []); ("x", ((11, 37), (11, 38)), []);
           ("A", ((11, 44), (11, 45)), []); ("B", ((11, 56), (11, 57)), []);
           ("C", ((13, 10), (13, 11)), ["union"]); ("x", ((13, 7), (13, 8)), []);
           ("x", ((13, 15), (13, 16)), []);
           ("IsItAnA", ((13, 15), (13, 24)), ["member"; "prop"; "funky"]);
           ("x", ((13, 25), (13, 26)), []);
           ("IsItAnAMethod", ((13, 25), (13, 40)), ["member"; "funky"]);
           ("g", ((13, 4), (13, 5)), ["val"]); ("M", ((2, 7), (2, 8)), ["module"])]

//--------------------------------------------

module internal Project41 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    // We need to us a stable name to keep the hashes stable
    let base2 = Path.Combine(Path.GetDirectoryName(Path.GetTempFileName()), "stabletmp.tmp")
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module M

    let data1 = {| X = 1 |}

    // Types can be written with the same syntax
    let data2 : {| X : int |} = data1

    type D = {| X : int |}

    // Access is as expected
    let f1 (v : {| X : int |}) = v.X

    // Access is as expected
    let f2 (v : D) = v.X

    // Access can be nested
    let f3 (v : {| X: {| X : int; Y : string |} |}) = v.X.X

    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)
    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)
    let cleanFileName a = if a = fileName1 then "file1" else "??"

[<Test>]
let ``Test project41 all symbols`` () =

    let wholeProjectResults = checker.ParseAndCheckProject(Project41.options) |> Async.RunImmediate
    let allSymbolUses = wholeProjectResults.GetAllUsesOfAllSymbols()
    let allSymbolUsesInfo =
        [ for s in allSymbolUses do
              let pos =
                  match s.Symbol.DeclarationLocation with
                  | Some r when r.FileName = Project41.fileName1 -> r.StartLine, r.StartColumn
                  | _ -> (0,0)
              yield (s.Symbol.DisplayName, tups s.Range, attribsOfSymbol s.Symbol, pos) ]
    allSymbolUsesInfo |> shouldEqual
          [("X", ((4, 19), (4, 20)),
            ["field"; "anon(0, [//<>f__AnonymousType1416859829`1]X)"], (4, 19));
           ("data1", ((4, 8), (4, 13)), ["val"], (4, 8));
           ("int", ((7, 23), (7, 26)), ["abbrev"], (0, 0));
           ("X", ((7, 19), (7, 20)),
            ["field"; "anon(0, [//<>f__AnonymousType1416859829`1]X)"], (7, 19));
           ("data1", ((7, 32), (7, 37)), ["val"], (4, 8));
           ("data2", ((7, 8), (7, 13)), ["val"], (7, 8));
           ("int", ((9, 20), (9, 23)), ["abbrev"], (0, 0));
           ("X", ((9, 16), (9, 17)),
            ["field"; "anon(0, [//<>f__AnonymousType1416859829`1]X)"], (9, 16));
           ("int", ((9, 20), (9, 23)), ["abbrev"], (0, 0));
           ("X", ((9, 16), (9, 17)),
            ["field"; "anon(0, [//<>f__AnonymousType1416859829`1]X)"], (9, 16));
           ("D", ((9, 9), (9, 10)), ["abbrev"], (9, 9));
           ("int", ((12, 23), (12, 26)), ["abbrev"], (0, 0));
           ("X", ((12, 19), (12, 20)),
            ["field"; "anon(0, [//<>f__AnonymousType1416859829`1]X)"], (12, 19));
           ("v", ((12, 12), (12, 13)), [], (12, 12));
           ("v", ((12, 33), (12, 34)), [], (12, 12));
           ("X", ((12, 33), (12, 36)),
            ["field"; "anon(0, [//<>f__AnonymousType1416859829`1]X)"], (12, 19));
           ("f1", ((12, 8), (12, 10)), ["val"], (12, 8));
           ("D", ((15, 16), (15, 17)), ["abbrev"], (9, 9));
           ("v", ((15, 12), (15, 13)), [], (15, 12));
           ("v", ((15, 21), (15, 22)), [], (15, 12));
           ("X", ((15, 21), (15, 24)),
            ["field"; "anon(0, [//<>f__AnonymousType1416859829`1]X)"], (9, 16));
           ("f2", ((15, 8), (15, 10)), ["val"], (15, 8));
           ("int", ((18, 29), (18, 32)), ["abbrev"], (0, 0));
           ("string", ((18, 38), (18, 44)), ["abbrev"], (0, 0));
           ("X", ((18, 25), (18, 26)),
            ["field"; "anon(0, [//<>f__AnonymousType4026451324`2]X,Y)"], (18, 25));
           ("Y", ((18, 34), (18, 35)),
            ["field"; "anon(1, [//<>f__AnonymousType4026451324`2]X,Y)"], (18, 34));
           ("X", ((18, 19), (18, 20)),
            ["field"; "anon(0, [//<>f__AnonymousType1416859829`1]X)"], (18, 19));
           ("v", ((18, 12), (18, 13)), [], (18, 12));
           ("v", ((18, 54), (18, 55)), [], (18, 12));
           ("X", ((18, 56), (18, 57)),
            ["field"; "anon(0, [//<>f__AnonymousType1416859829`1]X)"], (18, 19));
           ("X", ((18, 54), (18, 59)),
            ["field"; "anon(0, [//<>f__AnonymousType4026451324`2]X,Y)"], (18, 25));
           ("f3", ((18, 8), (18, 10)), ["val"], (18, 8));
           ("M", ((2, 7), (2, 8)), ["module"], (2, 7))]


module internal Project42 =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let fileName2 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    // We need to us a stable name to keep the hashes stable
    let base2 = Path.Combine(Path.GetDirectoryName(Path.GetTempFileName()), "stabletmp.tmp")
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module File1

let test() = ()
    """
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)
    let fileSource2 = """
module File2

open File1

let test2() = test()
    """
    FileSystem.OpenFileForWriteShim(fileName2).Write(fileSource2)
    let fileNames = [fileName1;fileName2]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)

[<Test>]
let ``Test project42 to ensure cached checked results are invalidated`` () =
    let text2 = SourceText.ofString(FileSystem.OpenFileForReadShim(Project42.fileName2).ReadAllText())
    let checkedFile2 = checker.ParseAndCheckFileInProject(Project42.fileName2, text2.GetHashCode(), text2, Project42.options) |> Async.RunImmediate
    match checkedFile2 with
    | _, FSharpCheckFileAnswer.Succeeded(checkedFile2Results) ->
        Assert.IsEmpty(checkedFile2Results.Diagnostics)
        FileSystem.OpenFileForWriteShim(Project42.fileName1).Write("""module File1""")
        try
            let checkedFile2Again = checker.ParseAndCheckFileInProject(Project42.fileName2, text2.GetHashCode(), text2, Project42.options) |> Async.RunImmediate
            match checkedFile2Again with
            | _, FSharpCheckFileAnswer.Succeeded(checkedFile2AgainResults) ->
                Assert.IsNotEmpty(checkedFile2AgainResults.Diagnostics) // this should contain errors as File1 does not contain the function `test()`
            | _ ->
                failwith "Project42 failed to check."
        finally
            FileSystem.OpenFileForWriteShim(Project42.fileName1).Write(Project42.fileSource1) // Revert to the original state of the file
    | _ ->
        failwith "Project42 failed to check."

module internal ProjectBig =

    let fileNamesI = [ for i in 1 .. 10 -> (i, Path.ChangeExtension(Path.GetTempFileName(), ".fs")) ]
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSources = [ for (i,f) in fileNamesI -> (f, "module M" + string i) ]
    for (f,text) in fileSources do FileSystem.OpenFileForWriteShim(f).Write(text)
    let fileSources2 = [ for (i,f) in fileSources -> SourceText.ofString f ]

    let fileNames = [ for (_,f) in fileNamesI -> f ]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options = checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)
    let parsingOptions, _ = checker.GetParsingOptionsFromCommandLineArgs(List.ofArray args)



[<Test>]
// Simplified repro for https://github.com/Microsoft/visualfsharp/issues/2679
let ``add files with same name from different folders`` () =
    let fileNames =
        [ __SOURCE_DIRECTORY__ + "/data/samename/folder1/a.fs"
          __SOURCE_DIRECTORY__ + "/data/samename/folder2/a.fs" ]
    let projFileName = __SOURCE_DIRECTORY__ + "/data/samename/tempet.fsproj"
    let args = mkProjectCommandLineArgs ("test.dll", fileNames)
    let options = checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)
    let wholeProjectResults = checker.ParseAndCheckProject(options) |> Async.RunImmediate
    let errors =
        wholeProjectResults.Diagnostics
        |> Array.filter (fun x -> x.Severity = FSharpDiagnosticSeverity.Error)
    if errors.Length > 0 then
        printfn "add files with same name from different folders"
        for err in errors do
            printfn "ERROR: %s" err.Message
    shouldEqual 0 errors.Length

module internal ProjectStructUnions =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1 = """
module M

// Custom struct result type as test projects still use FSharp.Core 4.0
type [<Struct>] Result<'a,'b> = Ok of ResultValue:'a | Error of ErrorValue:'b

type Foo =
    | Foo of Result<int, string>

let foo (a: Foo): bool =
    match a with
    | Foo(Ok(_)) -> true
    | _ -> false
    """

    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1)
    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)

[<Test>]
let ``Test typed AST for struct unions`` () = // See https://github.com/fsharp/FSharp.Compiler.Service/issues/756
    let keepAssemblyContentsChecker = FSharpChecker.Create(keepAssemblyContents=true)
    let wholeProjectResults = keepAssemblyContentsChecker.ParseAndCheckProject(ProjectStructUnions.options) |> Async.RunImmediate
    let declarations =
        let checkedFile = wholeProjectResults.AssemblyContents.ImplementationFiles.[0]
        match checkedFile.Declarations.[0] with
        | FSharpImplementationFileDeclaration.Entity (_, subDecls) -> subDecls
        | _ -> failwith "unexpected declaration"
    let getExpr exprIndex =
        match declarations.[exprIndex] with
        | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(_,_,e) -> e
        | FSharpImplementationFileDeclaration.InitAction e -> e
        | _ -> failwith "unexpected declaration"
    match getExpr (declarations.Length - 1) with
    | IfThenElse(UnionCaseTest(AddressOf(UnionCaseGet _),_,uci),
                                Const(trueValue, _), Const(falseValue, _))
        when uci.Name = "Ok" && obj.Equals(trueValue, true) && obj.Equals(falseValue, false) -> true
    | _ -> failwith "unexpected expression"
    |> shouldEqual true

module internal ProjectLineDirectives =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1Text = """
module M

# 10 "Test.fsy"
let x = (1 = 3.0)
    """
    let fileSource1 = SourceText.ofString fileSource1Text
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1Text)
    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)

[<Test>]
let ``Test line directives in foreground analysis`` () = // see https://github.com/Microsoft/visualfsharp/issues/3317

    // In background analysis and normal compiler checking, the errors are reported w.r.t. the line directives
    let wholeProjectResults = checker.ParseAndCheckProject(ProjectLineDirectives.options) |> Async.RunImmediate
    for e in wholeProjectResults.Diagnostics do
        printfn "ProjectLineDirectives wholeProjectResults error file: <<<%s>>>" e.Range.FileName

    [ for e in wholeProjectResults.Diagnostics -> e.Range.StartLine, e.Range.EndLine, e.Range.FileName ] |> shouldEqual [(10, 10, "Test.fsy")]

    // In foreground analysis routines, used by visual editing tools, the errors are reported w.r.t. the source
    // file, which is assumed to be in the editor, not the other files referred to by line directives.
    let checkResults1 =
        checker.ParseAndCheckFileInProject(ProjectLineDirectives.fileName1, 0, ProjectLineDirectives.fileSource1, ProjectLineDirectives.options)
        |> Async.RunImmediate
        |> function (_,FSharpCheckFileAnswer.Succeeded x) ->  x | _ -> failwith "unexpected aborted"

    for e in checkResults1.Diagnostics do
        printfn "ProjectLineDirectives checkResults1 error file: <<<%s>>>" e.Range.FileName

    [ for e in checkResults1.Diagnostics -> e.Range.StartLine, e.Range.EndLine, e.Range.FileName ] |> shouldEqual [(5, 5, ProjectLineDirectives.fileName1)]

//------------------------------------------------------

[<Test>]
let ``ParseAndCheckFileResults contains ImplFile list if FSharpChecker is created with keepAssemblyContent flag set to true``() =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1Text = """
type A(i:int) =
    member x.Value = i
"""
    let fileSource1 = SourceText.ofString fileSource1Text
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1Text)

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let keepAssemblyContentsChecker = FSharpChecker.Create(keepAssemblyContents=true)
    let options =  keepAssemblyContentsChecker.GetProjectOptionsFromCommandLineArgs (projFileName, args)

    let fileCheckResults =
        keepAssemblyContentsChecker.ParseAndCheckFileInProject(fileName1, 0, fileSource1, options)  |> Async.RunImmediate
        |> function
            | _, FSharpCheckFileAnswer.Succeeded(res) -> res
            | _ -> failwithf "Parsing aborted unexpectedly..."

    let declarations =
        match fileCheckResults.ImplementationFile with
        | Some implFile ->
            match implFile.Declarations |> List.tryHead with
            | Some (FSharpImplementationFileDeclaration.Entity (_, subDecls)) -> subDecls
            | _ -> failwith "unexpected declaration"
        | None -> failwith "File check results does not contain any `ImplementationFile`s"

    match declarations |> List.tryHead with
    | Some (FSharpImplementationFileDeclaration.Entity(entity, [])) ->
        entity.DisplayName |> shouldEqual "A"
        let memberNames = entity.MembersFunctionsAndValues |> Seq.map (fun x -> x.DisplayName) |> Set.ofSeq
        Assert.That(memberNames, Contains.Item "Value")

    | Some decl -> failwithf "unexpected declaration %A" decl
    | None -> failwith "declaration list is empty"


[<TestCase(([||]: string[]), ([||]: bool[]))>]
[<TestCase([| "--times" |], [| false |])>]
[<TestCase([| "--times"; "--nowarn:75" |], ([||]: bool[]))>]
[<TestCase([| "--times"; "--warnaserror:75" |], [| true |])>]
[<TestCase([| "--times"; "--warnaserror-:75"; "--warnaserror" |], [| false |])>]
let ``#4030, Incremental builder creation warnings`` (args, errorSeverities) =
    let source = "module M"
    let fileName, options = mkTestFileAndOptions source args

    let _, checkResults = parseAndCheckFile fileName source options
    checkResults.Diagnostics |> Array.map (fun e -> e.Severity = FSharpDiagnosticSeverity.Error) |> shouldEqual errorSeverities


//------------------------------------------------------

[<Test>]
let ``Unused opens in rec module smoke test 1``() =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1Text = """
module rec Module

open System.Collections // unused
open System.Collections.Generic // used, should not appear
open FSharp.Control // unused
open FSharp.Data // unused
open System.Globalization // unused

module SomeUnusedModule =
    let f x  = x

module SomeUsedModuleContainingFunction =
    let g x  = x

module SomeUsedModuleContainingActivePattern =
    let (|ActivePattern|) x  = x

module SomeUsedModuleContainingExtensionMember =
    type System.Int32 with member x.Q = 1

module SomeUsedModuleContainingUnion =
    type Q = A | B

open SomeUnusedModule
open SomeUsedModuleContainingFunction
open SomeUsedModuleContainingExtensionMember
open SomeUsedModuleContainingActivePattern
open SomeUsedModuleContainingUnion

type UseTheThings(i:int) =
    member x.Value = Dictionary<int,int>() // use something from System.Collections.Generic, as a constructor
    member x.UseSomeUsedModuleContainingFunction() = g 3
    member x.UseSomeUsedModuleContainingActivePattern(ActivePattern g) = g
    member x.UseSomeUsedModuleContainingExtensionMember() = (3).Q
    member x.UseSomeUsedModuleContainingUnion() = A
"""
    let fileSource1 = SourceText.ofString fileSource1Text
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1Text)

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let keepAssemblyContentsChecker = FSharpChecker.Create(keepAssemblyContents=true)
    let options =  keepAssemblyContentsChecker.GetProjectOptionsFromCommandLineArgs (projFileName, args)

    let fileCheckResults =
        keepAssemblyContentsChecker.ParseAndCheckFileInProject(fileName1, 0, fileSource1, options)  |> Async.RunImmediate
        |> function
            | _, FSharpCheckFileAnswer.Succeeded(res) -> res
            | _ -> failwithf "Parsing aborted unexpectedly..."
    //let symbolUses = fileCheckResults.GetAllUsesOfAllSymbolsInFile() |> Async.RunImmediate |> Array.indexed
    // Fragments used to check hash codes:
    //(snd symbolUses.[42]).Symbol.IsEffectivelySameAs((snd symbolUses.[37]).Symbol)
    //(snd symbolUses.[42]).Symbol.GetEffectivelySameAsHash()
    //(snd symbolUses.[37]).Symbol.GetEffectivelySameAsHash()
    let lines = FileSystem.OpenFileForReadShim(fileName1).ReadAllLines()
    let unusedOpens = UnusedOpens.getUnusedOpens (fileCheckResults, (fun i -> lines.[i-1])) |> Async.RunImmediate
    let unusedOpensData = [ for uo in unusedOpens -> tups uo, lines.[uo.StartLine-1] ]
    let expected =
          [(((4, 5), (4, 23)), "open System.Collections // unused");
           (((6, 5), (6, 19)), "open FSharp.Control // unused");
           (((7, 5), (7, 16)), "open FSharp.Data // unused");
           (((8, 5), (8, 25)), "open System.Globalization // unused");
           (((25, 5), (25, 21)), "open SomeUnusedModule")]
    unusedOpensData |> shouldEqual expected

[<Test>]
let ``Unused opens in non rec module smoke test 1``() =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1Text = """
module Module

open System.Collections // unused
open System.Collections.Generic // used, should not appear
open FSharp.Control // unused
open FSharp.Data // unused
open System.Globalization // unused

module SomeUnusedModule =
    let f x  = x

module SomeUsedModuleContainingFunction =
    let g x  = x

module SomeUsedModuleContainingActivePattern =
    let (|ActivePattern|) x  = x

module SomeUsedModuleContainingExtensionMember =
    type System.Int32 with member x.Q = 1

module SomeUsedModuleContainingUnion =
    type Q = A | B

open SomeUnusedModule
open SomeUsedModuleContainingFunction
open SomeUsedModuleContainingExtensionMember
open SomeUsedModuleContainingActivePattern
open SomeUsedModuleContainingUnion

type UseTheThings(i:int) =
    member x.Value = Dictionary<int,int>() // use something from System.Collections.Generic, as a constructor
    member x.UseSomeUsedModuleContainingFunction() = g 3
    member x.UseSomeUsedModuleContainingActivePattern(ActivePattern g) = g
    member x.UseSomeUsedModuleContainingExtensionMember() = (3).Q
    member x.UseSomeUsedModuleContainingUnion() = A
"""
    let fileSource1 = SourceText.ofString fileSource1Text
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1Text)

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let keepAssemblyContentsChecker = FSharpChecker.Create(keepAssemblyContents=true)
    let options =  keepAssemblyContentsChecker.GetProjectOptionsFromCommandLineArgs (projFileName, args)

    let fileCheckResults =
        keepAssemblyContentsChecker.ParseAndCheckFileInProject(fileName1, 0, fileSource1, options)  |> Async.RunImmediate
        |> function
            | _, FSharpCheckFileAnswer.Succeeded(res) -> res
            | _ -> failwithf "Parsing aborted unexpectedly..."
    //let symbolUses = fileCheckResults.GetAllUsesOfAllSymbolsInFile() |> Async.RunImmediate |> Array.indexed
    // Fragments used to check hash codes:
    //(snd symbolUses.[42]).Symbol.IsEffectivelySameAs((snd symbolUses.[37]).Symbol)
    //(snd symbolUses.[42]).Symbol.GetEffectivelySameAsHash()
    //(snd symbolUses.[37]).Symbol.GetEffectivelySameAsHash()
    let lines = FileSystem.OpenFileForReadShim(fileName1).ReadAllLines()
    let unusedOpens = UnusedOpens.getUnusedOpens (fileCheckResults, (fun i -> lines.[i-1])) |> Async.RunImmediate
    let unusedOpensData = [ for uo in unusedOpens -> tups uo, lines.[uo.StartLine-1] ]
    let expected =
          [(((4, 5), (4, 23)), "open System.Collections // unused");
           (((6, 5), (6, 19)), "open FSharp.Control // unused");
           (((7, 5), (7, 16)), "open FSharp.Data // unused");
           (((8, 5), (8, 25)), "open System.Globalization // unused");
           (((25, 5), (25, 21)), "open SomeUnusedModule")]
    unusedOpensData |> shouldEqual expected

[<Test>]
let ``Unused opens smoke test auto open``() =

    let fileName1 = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSource1Text = """
open System.Collections // unused
open System.Collections.Generic // used, should not appear
open FSharp.Control // unused
open FSharp.Data // unused
open System.Globalization // unused

[<AutoOpen>]
module Helpers =
    module SomeUnusedModule =
        let f x  = x

    module SomeUsedModuleContainingFunction =
        let g x  = x

    module SomeUsedModuleContainingActivePattern =
        let (|ActivePattern|) x  = x

    module SomeUsedModuleContainingExtensionMember =
        type System.Int32 with member x.Q = 1

    module SomeUsedModuleContainingUnion =
        type Q = A | B

open SomeUnusedModule
open SomeUsedModuleContainingFunction
open SomeUsedModuleContainingExtensionMember
open SomeUsedModuleContainingActivePattern
open SomeUsedModuleContainingUnion

type UseTheThings(i:int) =
    member x.Value = Dictionary<int,int>() // use something from System.Collections.Generic, as a constructor
    member x.UseSomeUsedModuleContainingFunction() = g 3
    member x.UseSomeUsedModuleContainingActivePattern(ActivePattern g) = g
    member x.UseSomeUsedModuleContainingExtensionMember() = (3).Q
    member x.UseSomeUsedModuleContainingUnion() = A

module M1 =
    type R = { Field: int }

module M2 =
    open M1

    let foo x = x.Field
"""
    let fileSource1 = SourceText.ofString fileSource1Text
    FileSystem.OpenFileForWriteShim(fileName1).Write(fileSource1Text)

    let fileNames = [fileName1]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let keepAssemblyContentsChecker = FSharpChecker.Create(keepAssemblyContents=true)
    let options =  keepAssemblyContentsChecker.GetProjectOptionsFromCommandLineArgs (projFileName, args)

    let fileCheckResults =
        keepAssemblyContentsChecker.ParseAndCheckFileInProject(fileName1, 0, fileSource1, options)  |> Async.RunImmediate
        |> function
            | _, FSharpCheckFileAnswer.Succeeded(res) -> res
            | _ -> failwithf "Parsing aborted unexpectedly..."
    let lines = FileSystem.OpenFileForReadShim(fileName1).ReadAllLines()
    let unusedOpens = UnusedOpens.getUnusedOpens (fileCheckResults, (fun i -> lines.[i-1])) |> Async.RunImmediate
    let unusedOpensData = [ for uo in unusedOpens -> tups uo, lines.[uo.StartLine-1] ]
    let expected =
          [(((2, 5), (2, 23)), "open System.Collections // unused");
           (((4, 5), (4, 19)), "open FSharp.Control // unused");
           (((5, 5), (5, 16)), "open FSharp.Data // unused");
           (((6, 5), (6, 25)), "open System.Globalization // unused");
           (((25, 5), (25, 21)), "open SomeUnusedModule")]
    unusedOpensData |> shouldEqual expected


[<Test>]
let ``Opens in nested recursive modules`` () =
    let _, checkResults = getParseAndCheckResults """
module rec Module

open System

module Nested =
    open System
"""
    checkResults.OpenDeclarations
    |> Seq.filter (fun openDeclaration ->
        match openDeclaration.Range with
        | Some range -> range <> Range.rangeStartup
        | _ -> false)
    |> List.ofSeq
    |> List.map(fun openDeclaration -> tups openDeclaration.AppliedScope)
    |> shouldEqual
           [ (2, 0), (7, 15)
             (6, 0), (7, 15) ]

[<Test>]
let ``Opens in nested recursive modules - namespaces`` () =
    let _, checkResults = getParseAndCheckResults """
namespace rec Ns1

open System

module Nested =
    open System

namespace rec Ns2

open System

module Nested =
    open System

"""
    checkResults.OpenDeclarations
    |> Seq.filter (fun openDeclaration ->
        match openDeclaration.Range with
        | Some range -> range <> Range.rangeStartup
        | _ -> false)
    |> List.ofSeq
    |> List.map(fun openDeclaration -> tups openDeclaration.AppliedScope)
    |> shouldEqual
           [ (4, 0), (7, 15)
             (6, 0), (7, 15)
             (11, 0), (14, 15)
             (13, 0), (14, 15) ]

let checkContentAsScript content =
    // can't use the helper function in these tests because `getParseAndCheckResults` doesn't seem to operate in a mode
    // that uses the dependency manager (possibly because `useSdkScripts` isn't set/`assumeDotNetFramework` is implicitly
    // set).
    // because of this we have to do it all manually
    let scriptName = "test.fsx"
    let tempDir = Path.GetTempPath()
    let scriptFullPath = Path.Combine(tempDir, scriptName)
    let sourceText = SourceText.ofString content
    let projectOptions, _ = checker.GetProjectOptionsFromScript(scriptFullPath, sourceText, useSdkRefs = true, assumeDotNetFramework = false) |> Async.RunImmediate
    let parseOptions, _ = checker.GetParsingOptionsFromProjectOptions projectOptions
    let parseResults = checker.ParseFile(scriptFullPath, sourceText, parseOptions) |> Async.RunImmediate
    let checkResults = checker.CheckFileInProject(parseResults, scriptFullPath, 0, sourceText, projectOptions) |> Async.RunImmediate
    match checkResults with
    | FSharpCheckFileAnswer.Aborted -> failwith "no check results"
    | FSharpCheckFileAnswer.Succeeded r -> r

[<Test>]
let ``References from #r nuget are included in script project options`` () =
    let checkResults = checkContentAsScript """
#r "nuget: Dapper"
"""
    let assemblyNames =
        checkResults.ProjectContext.GetReferencedAssemblies()
        |> Seq.choose (fun f -> f.FileName |> Option.map Path.GetFileName)
        |> Seq.distinct
    printfn "%s" (assemblyNames |> String.concat "\n")
    assemblyNames |> should contain "Dapper.dll"
