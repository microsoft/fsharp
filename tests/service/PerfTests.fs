#if INTERACTIVE
#r "../../artifacts/bin/fcs/net461/FSharp.Compiler.Service.dll" // note, build FSharp.Compiler.Service.Tests.fsproj to generate this, this DLL has a public API so can be used from F# Interactive
#r "../../artifacts/bin/fcs/net461/nunit.framework.dll"
#load "FsUnit.fs"
#load "Common.fs"
#else
module FSharp.Compiler.Service.Tests.PerfTests
#endif


open NUnit.Framework
open FsUnit
open System.IO
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.IO
open FSharp.Compiler.Text
open FSharp.Compiler.Service.Tests.Common

// Create an interactive checker instance
let internal checker = FSharpChecker.Create()

module internal Project1 =

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
[<Ignore("https://github.com/dotnet/fsharp/issues/11184")>]
let ``Test request for parse and check doesn't check whole project`` () =

    printfn "starting test..."
    let backgroundParseCount = ref 0
    let backgroundCheckCount = ref 0
    checker.FileChecked.Add (fun x -> incr backgroundCheckCount)
    checker.FileParsed.Add (fun x -> incr backgroundParseCount)

    checker.ClearLanguageServiceRootCachesAndCollectAndFinalizeAllTransients()
    let pB, tB = FSharpChecker.ActualParseFileCount, FSharpChecker.ActualCheckFileCount

    printfn "ParseFile()..."
    let parseResults1 = checker.ParseFile(Project1.fileNames.[5], Project1.fileSources2.[5], Project1.parsingOptions)  |> Async.RunSynchronously
    let pC, tC = FSharpChecker.ActualParseFileCount, FSharpChecker.ActualCheckFileCount
    (pC - pB) |> shouldEqual 1
    (tC - tB) |> shouldEqual 0
    printfn "checking backgroundParseCount.Value = %d" backgroundParseCount.Value
    backgroundParseCount.Value |> shouldEqual 0
    printfn "checking backgroundCheckCount.Value = %d" backgroundCheckCount.Value
    backgroundCheckCount.Value |> shouldEqual 0

    printfn "CheckFileInProject()..."
    let checkResults1 = checker.CheckFileInProject(parseResults1, Project1.fileNames.[5], 0, Project1.fileSources2.[5], Project1.options)  |> Async.RunSynchronously
    let pD, tD = FSharpChecker.ActualParseFileCount, FSharpChecker.ActualCheckFileCount

    printfn "checking background parsing happened...., backgroundParseCount.Value = %d" backgroundParseCount.Value
    (backgroundParseCount.Value  >= 5) |> shouldEqual true // but note, the project does not get reparsed
    printfn "checking background typechecks happened...., backgroundCheckCount.Value = %d" backgroundCheckCount.Value
    (backgroundCheckCount.Value  >= 5) |> shouldEqual true // only two extra typechecks of files

    printfn "checking no extra background parsing...., backgroundParseCount.Value = %d" backgroundParseCount.Value
    (backgroundParseCount.Value  <= 10) |> shouldEqual true // but note, the project does not get reparsed
    printfn "checking no extra background typechecks...., backgroundCheckCount.Value = %d" backgroundCheckCount.Value
    (backgroundCheckCount.Value  <= 10) |> shouldEqual true // only two extra typechecks of files

    printfn "checking (pD - pC) = %d" (pD - pC)
    (pD - pC) |> shouldEqual 0
    printfn "checking (tD - tC) = %d" (tD - tC)
    (tD - tC) |> shouldEqual 1

    printfn "CheckFileInProject()..."
    let checkResults2 = checker.CheckFileInProject(parseResults1, Project1.fileNames.[7], 0, Project1.fileSources2.[7], Project1.options)  |> Async.RunSynchronously
    let pE, tE = FSharpChecker.ActualParseFileCount, FSharpChecker.ActualCheckFileCount
    printfn "checking no extra  foreground parsing...., (pE - pD) = %d" (pE - pD)
    (pE - pD) |> shouldEqual 0
    printfn "checking one foreground typecheck...., tE - tD = %d" (tE - tD)
    (tE - tD) |> shouldEqual 1
    printfn "checking no extra background parsing...., backgroundParseCount.Value = %d" backgroundParseCount.Value
    (backgroundParseCount.Value  <= 10) |> shouldEqual true // but note, the project does not get reparsed
    printfn "checking no extra background typechecks...., backgroundCheckCount.Value = %d" backgroundCheckCount.Value
    (backgroundCheckCount.Value  <= 10) |> shouldEqual true // only two extra typechecks of files

    printfn "ParseAndCheckFileInProject()..."
    // A subsequent ParseAndCheck of identical source code doesn't do any more anything
    let checkResults2 = checker.ParseAndCheckFileInProject(Project1.fileNames.[7], 0, Project1.fileSources2.[7], Project1.options)  |> Async.RunSynchronously
    let pF, tF = FSharpChecker.ActualParseFileCount, FSharpChecker.ActualCheckFileCount
    printfn "checking no extra foreground parsing...."
    (pF - pE) |> shouldEqual 0  // note, no new parse of the file
    printfn "checking no extra foreground typechecks...."
    (tF - tE) |> shouldEqual 0  // note, no new typecheck of the file
    printfn "checking no extra background parsing...., backgroundParseCount.Value = %d" backgroundParseCount.Value
    (backgroundParseCount.Value <= 10) |> shouldEqual true // but note, the project does not get reparsed
    printfn "checking no extra background typechecks...., backgroundCheckCount.Value = %d" backgroundCheckCount.Value
    (backgroundCheckCount.Value <= 10) |> shouldEqual true // only two extra typechecks of files
    ()
