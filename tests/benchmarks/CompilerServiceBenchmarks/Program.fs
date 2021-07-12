open System
open System.IO
open System.Text
open System.Threading.Tasks
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Text
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AbstractIL.ILBinaryReader
open Microsoft.CodeAnalysis.Text
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running

module private SourceText =

    open System.Runtime.CompilerServices

    let weakTable = ConditionalWeakTable<SourceText, ISourceText>()

    let create (sourceText: SourceText) =

        let sourceText =
            { new ISourceText with
            
                member __.Item with get index = sourceText.[index]

                member __.GetLineString(lineIndex) =
                    sourceText.Lines.[lineIndex].ToString()

                member __.GetLineCount() =
                    sourceText.Lines.Count

                member __.GetLastCharacterPosition() =
                    if sourceText.Lines.Count > 0 then
                        (sourceText.Lines.Count, sourceText.Lines.[sourceText.Lines.Count - 1].Span.Length)
                    else
                        (0, 0)

                member __.GetSubTextString(start, length) =
                    sourceText.GetSubText(TextSpan(start, length)).ToString()

                member __.SubTextEquals(target, startIndex) =
                    if startIndex < 0 || startIndex >= sourceText.Length then
                        raise (ArgumentOutOfRangeException("startIndex"))

                    if String.IsNullOrEmpty(target) then
                        raise (ArgumentException("Target is null or empty.", "target"))

                    let lastIndex = startIndex + target.Length
                    if lastIndex <= startIndex || lastIndex >= sourceText.Length then
                        raise (ArgumentException("Target is too big.", "target"))

                    let mutable finished = false
                    let mutable didEqual = true
                    let mutable i = 0
                    while not finished && i < target.Length do
                        if target.[i] <> sourceText.[startIndex + i] then
                            didEqual <- false
                            finished <- true // bail out early                        
                        else
                            i <- i + 1

                    didEqual

                member __.ContentEquals(sourceText) =
                    match sourceText with
                    | :? SourceText as sourceText -> sourceText.ContentEquals(sourceText)
                    | _ -> false

                member __.Length = sourceText.Length

                member __.CopyTo(sourceIndex, destination, destinationIndex, count) =
                    sourceText.CopyTo(sourceIndex, destination, destinationIndex, count)
            }

        sourceText

type SourceText with

    member this.ToFSharpSourceText() =
        SourceText.weakTable.GetValue(this, Runtime.CompilerServices.ConditionalWeakTable<_,_>.CreateValueCallback(SourceText.create))

[<AutoOpen>]
module Helpers =

    type Async with
        static member RunImmediate (computation: Async<'T>, ?cancellationToken ) =
            let cancellationToken = defaultArg cancellationToken Async.DefaultCancellationToken
            let ts = TaskCompletionSource<'T>()
            let task = ts.Task
            Async.StartWithContinuations(
                computation,
                (fun k -> ts.SetResult k),
                (fun exn -> ts.SetException exn),
                (fun _ -> ts.SetCanceled()),
                cancellationToken)
            task.Result
    
    let createProject name referencedProjects =
        let tmpPath = Path.GetTempPath()
        let file = Path.Combine(tmpPath, Path.ChangeExtension(name, ".fs"))
        {
            ProjectFileName = Path.Combine(tmpPath, Path.ChangeExtension(name, ".dll"))
            ProjectId = None
            SourceFiles = [|file|]
            OtherOptions = 
                Array.append [|"--optimize+"; "--target:library" |] (referencedProjects |> Array.ofList |> Array.map (fun x -> "-r:" + x.ProjectFileName))
            ReferencedProjects =
                referencedProjects
                |> List.map (fun x -> FSharpReferencedProject.CreateFSharp (x.ProjectFileName, x))
                |> Array.ofList
            IsIncompleteTypeCheckEnvironment = false
            UseScriptResolutionRules = false
            LoadTime = DateTime()
            UnresolvedReferences = None
            OriginalLoadReferences = []
            Stamp = Some 0L (* set the stamp to 0L on each run so we don't evaluate the whole project again *)
        }

    let generateSourceCode moduleName =
        sprintf """
module Benchmark.%s

type %s =

    val X : int

    val Y : int

    val Z : int

let function%s (x: %s) =
    let x = 1
    let y = 2
    let z = x + y
    z""" moduleName moduleName moduleName moduleName

    let decentlySizedStandAloneFile = File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "decentlySizedStandAloneFile.fsx"))

[<MemoryDiagnoser>]
type CompilerService() =
    let mutable checkerOpt = None
    let mutable sourceOpt = None
    let mutable assembliesOpt = None
    let mutable decentlySizedStandAloneFileCheckResultOpt = None

    let parsingOptions =
        {
            SourceFiles = [|"CheckExpressions.fs"|]
            ConditionalCompilationDefines = []
            ErrorSeverityOptions = FSharpDiagnosticOptions.Default
            IsInteractive = false
            LightSyntax = None
            CompilingFsLib = false
            IsExe = false
        }

    let readerOptions =
        {
            pdbDirPath = None
            reduceMemoryUsage = ReduceMemoryFlag.No
            metadataOnly = MetadataOnlyFlag.Yes
            tryGetMetadataSnapshot = fun _ -> None
        }

    [<GlobalSetup>]
    member __.Setup() =
        match checkerOpt with
        | None -> checkerOpt <- Some(FSharpChecker.Create(projectCacheSize = 200))
        | _ -> ()

        match sourceOpt with
        | None ->
            sourceOpt <- Some <| SourceText.From(File.OpenRead("""..\..\..\..\..\src\fsharp\CheckExpressions.fs"""), Encoding.Default, SourceHashAlgorithm.Sha1, true)
        | _ -> ()

        match assembliesOpt with
        | None -> 
            assembliesOpt <- 
                System.AppDomain.CurrentDomain.GetAssemblies()
                |> Array.map (fun x -> (x.Location))
                |> Some
        
        | _ -> ()

        match decentlySizedStandAloneFileCheckResultOpt with
        | None ->
            let options, _ =
                checkerOpt.Value.GetProjectOptionsFromScript("decentlySizedStandAloneFile.fsx", SourceText.ofString decentlySizedStandAloneFile)
                |> Async.RunImmediate
            let _, checkResult =                                                                
                checkerOpt.Value.ParseAndCheckFileInProject("decentlySizedStandAloneFile.fsx", 0, SourceText.ofString decentlySizedStandAloneFile, options)
                |> Async.RunImmediate
            decentlySizedStandAloneFileCheckResultOpt <- Some checkResult
        | _ -> ()

    [<Benchmark>]
    member __.ParsingTypeCheckerFs() =
        match checkerOpt, sourceOpt with
        | None, _ -> failwith "no checker"
        | _, None -> failwith "no source"
        | Some(checker), Some(source) ->
            let results = checker.ParseFile("CheckExpressions.fs", source.ToFSharpSourceText(), parsingOptions) |> Async.RunImmediate
            if results.ParseHadErrors then failwithf "parse had errors: %A" results.Diagnostics

    [<IterationCleanup(Target = "ParsingTypeCheckerFs")>]
    member __.ParsingTypeCheckerFsSetup() =
        match checkerOpt with
        | None -> failwith "no checker"
        | Some(checker) ->
            checker.InvalidateAll()
            checker.ClearLanguageServiceRootCachesAndCollectAndFinalizeAllTransients()
            checker.ParseFile("dummy.fs", SourceText.ofString "dummy", parsingOptions) |> Async.RunImmediate |> ignore
            ClearAllILModuleReaderCache()

    [<Benchmark>]
    member __.ILReading() =
        match assembliesOpt with
        | None -> failwith "no assemblies"
        | Some(assemblies) ->
            // We try to read most of everything in the assembly that matter, mainly types with their properties, methods, and fields.
            // CustomAttrs and SecurityDecls are lazy until you call them, so we call them here for benchmarking.
            assemblies
            |> Array.iter (fun (fileName) ->
                let reader = OpenILModuleReader fileName readerOptions

                let ilModuleDef = reader.ILModuleDef

                let ilAssemblyManifest = ilModuleDef.Manifest.Value

                ilAssemblyManifest.CustomAttrs |> ignore
                ilAssemblyManifest.SecurityDecls |> ignore
                ilAssemblyManifest.ExportedTypes.AsList
                |> List.iter (fun x ->
                   x.CustomAttrs |> ignore
                )

                ilModuleDef.CustomAttrs |> ignore
                ilModuleDef.TypeDefs.AsArray
                |> Array.iter (fun ilTypeDef ->
                    ilTypeDef.CustomAttrs |> ignore
                    ilTypeDef.SecurityDecls |> ignore

                    ilTypeDef.Methods.AsArray
                    |> Array.iter (fun ilMethodDef ->
                        ilMethodDef.CustomAttrs |> ignore
                        ilMethodDef.SecurityDecls |> ignore
                    )

                    ilTypeDef.Fields.AsList
                    |> List.iter (fun ilFieldDef ->
                        ilFieldDef.CustomAttrs |> ignore
                    )

                    ilTypeDef.Properties.AsList
                    |> List.iter (fun ilPropertyDef ->
                        ilPropertyDef.CustomAttrs |> ignore
                    )
                )
            )

    [<IterationCleanup(Target = "ILReading")>]
    member __.ILReadingSetup() =
        // With caching, performance increases an order of magnitude when re-reading an ILModuleReader.
        // Clear it for benchmarking.
        ClearAllILModuleReaderCache()

    member val TypeCheckFileWith100ReferencedProjectsOptions =
        createProject "MainProject"
            [ for i = 1 to 100 do
                yield 
                    createProject ("ReferencedProject" + string i) []
            ]

    member this.TypeCheckFileWith100ReferencedProjectsRun() =
        let options = this.TypeCheckFileWith100ReferencedProjectsOptions
        let file = options.SourceFiles.[0]

        match checkerOpt with
        | None -> failwith "no checker"
        | Some checker ->
            let parseResult, checkResult =                                                                
                checker.ParseAndCheckFileInProject(file, 0, SourceText.ofString (File.ReadAllText(file)), options)
                |> Async.RunImmediate

            if parseResult.Diagnostics.Length > 0 then
                failwithf "%A" parseResult.Diagnostics

            match checkResult with
            | FSharpCheckFileAnswer.Aborted -> failwith "aborted"
            | FSharpCheckFileAnswer.Succeeded checkFileResult ->

                if checkFileResult.Diagnostics.Length > 0 then
                    failwithf "%A" checkFileResult.Diagnostics

    [<IterationSetup(Target = "TypeCheckFileWith100ReferencedProjects")>]
    member this.TypeCheckFileWith100ReferencedProjectsSetup() =
        this.TypeCheckFileWith100ReferencedProjectsOptions.SourceFiles
        |> Seq.iter (fun file ->
            File.WriteAllText(file, generateSourceCode (Path.GetFileNameWithoutExtension(file)))
        )

        this.TypeCheckFileWith100ReferencedProjectsOptions.ReferencedProjects
        |> Seq.iter (function
            | FSharpReferencedProject.FSharpReference(_, referencedProjectOptions) ->
                referencedProjectOptions.SourceFiles
                |> Seq.iter (fun file ->
                    File.WriteAllText(file, generateSourceCode (Path.GetFileNameWithoutExtension(file)))
                )
            | _ -> ()
        )

        this.TypeCheckFileWith100ReferencedProjectsRun()

    [<Benchmark>]
    member this.TypeCheckFileWith100ReferencedProjects() =
        // Because the checker's projectcachesize is set to 200, this should be fast.
        // If set to 3, it will be almost as slow as re-evaluating all project and it's projects references on setup; this could be a bug or not what we want.
        this.TypeCheckFileWith100ReferencedProjectsRun()

    member val TypeCheckFileWithNoReferencesOptions = createProject "MainProject" []

    [<IterationCleanup(Target = "TypeCheckFileWith100ReferencedProjects")>]
    member this.TypeCheckFileWith100ReferencedProjectsCleanup() =
        this.TypeCheckFileWith100ReferencedProjectsOptions.SourceFiles
        |> Seq.iter (fun file ->
            try File.Delete(file) with | _ -> ()
        )

        this.TypeCheckFileWith100ReferencedProjectsOptions.ReferencedProjects
        |> Seq.iter (function
            | FSharpReferencedProject.FSharpReference(_, referencedProjectOptions) ->
                referencedProjectOptions.SourceFiles
                |> Seq.iter (fun file ->
                    try File.Delete(file) with | _ -> ()
                )
            | _ -> ()
        )

        match checkerOpt with
        | None -> failwith "no checker"
        | Some(checker) ->
            checker.InvalidateAll()
            checker.ClearLanguageServiceRootCachesAndCollectAndFinalizeAllTransients()
            ClearAllILModuleReaderCache()

    [<Benchmark>]
    member this.SimplifyNames() =
        match decentlySizedStandAloneFileCheckResultOpt with
        | Some checkResult ->
            match checkResult with
            | FSharpCheckFileAnswer.Aborted -> failwith "checker aborted"
            | FSharpCheckFileAnswer.Succeeded results ->
                let sourceLines = decentlySizedStandAloneFile.Split ([|"\r\n"; "\n"; "\r"|], StringSplitOptions.None)
                let ranges = SimplifyNames.getSimplifiableNames(results, fun lineNum -> sourceLines.[Line.toZ lineNum]) |> Async.RunImmediate
                ignore ranges                
            ()
        | _ -> failwith "oopsie"

    [<Benchmark>]
    member this.UnusedOpens() =
        match decentlySizedStandAloneFileCheckResultOpt with
        | Some checkResult ->
            match checkResult with
            | FSharpCheckFileAnswer.Aborted -> failwith "checker aborted"
            | FSharpCheckFileAnswer.Succeeded results ->
                let sourceLines = decentlySizedStandAloneFile.Split ([|"\r\n"; "\n"; "\r"|], StringSplitOptions.None)
                let decls = UnusedOpens.getUnusedOpens(results, fun lineNum -> sourceLines.[Line.toZ lineNum]) |> Async.RunImmediate
                ignore decls              
            ()
        | _ -> failwith "oopsie"

    [<Benchmark>]
    member this.UnusedDeclarations() =
        match decentlySizedStandAloneFileCheckResultOpt with
        | Some checkResult ->
            match checkResult with
            | FSharpCheckFileAnswer.Aborted -> failwith "checker aborted"
            | FSharpCheckFileAnswer.Succeeded results ->
                let decls = UnusedDeclarations.getUnusedDeclarations(results, true) |> Async.RunImmediate
                ignore decls // should be 16                
            ()
        | _ -> failwith "oopsie"

[<EntryPoint>]
let main _ =
    BenchmarkRunner.Run<CompilerService>() |> ignore
    0
