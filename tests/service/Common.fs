[<AutoOpen>]
module internal FSharp.Compiler.Service.Tests.Common

open System
open System.Diagnostics
open System.IO
open System.Collections.Generic
open System.Collections.Immutable
open System.Threading
open System.Threading.Tasks
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.IO
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text
open FsUnit
open NUnit.Framework

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

#if NETCOREAPP
let readRefs (folder : string) (projectFile: string) =
    let runProcess (workingDir: string) (exePath: string) (args: string) =
        let psi = System.Diagnostics.ProcessStartInfo()
        psi.FileName <- exePath
        psi.WorkingDirectory <- workingDir
        psi.RedirectStandardOutput <- false
        psi.RedirectStandardError <- false
        psi.Arguments <- args
        psi.CreateNoWindow <- true
        psi.UseShellExecute <- false

        use p = new System.Diagnostics.Process()
        p.StartInfo <- psi
        p.Start() |> ignore
        p.WaitForExit()

        let exitCode = p.ExitCode
        exitCode, ()

    let projFilePath = Path.Combine(folder, projectFile)
    let runCmd exePath args = runProcess folder exePath ((args |> String.concat " ") + " -restore")
    let msbuildExec = Dotnet.ProjInfo.Inspect.dotnetMsbuild runCmd
    let result = Dotnet.ProjInfo.Inspect.getProjectInfo ignore msbuildExec Dotnet.ProjInfo.Inspect.getFscArgs [] projFilePath
    match result with
    | Ok(Dotnet.ProjInfo.Inspect.GetResult.FscArgs x) ->
        x
        |> List.filter (fun s -> s.StartsWith("-r:", StringComparison.Ordinal))
        |> List.map (fun s -> s.Replace("-r:", ""))
    | _ -> []
#endif


// Create one global interactive checker instance
let checker = FSharpChecker.Create()

type TempFile(ext, contents: string) =
    let tmpFile =  Path.ChangeExtension(System.IO.Path.GetTempFileName() , ext)
    do FileSystem.OpenFileForWriteShim(tmpFile).Write(contents)

    interface System.IDisposable with
        member x.Dispose() = try FileSystem.FileDeleteShim tmpFile with _ -> ()
    member x.Name = tmpFile

#nowarn "57"

let getBackgroundParseResultsForScriptText (input: string) =
    use file =  new TempFile("fsx", input)
    let checkOptions, _diagnostics = checker.GetProjectOptionsFromScript(file.Name, SourceText.ofString input) |> Async.RunImmediate
    checker.GetBackgroundParseResultsForFileInProject(file.Name, checkOptions)  |> Async.RunImmediate


let getBackgroundCheckResultsForScriptText (input: string) =
    use file =  new TempFile("fsx", input)
    let checkOptions, _diagnostics = checker.GetProjectOptionsFromScript(file.Name, SourceText.ofString input) |> Async.RunImmediate
    checker.GetBackgroundCheckResultsForFileInProject(file.Name, checkOptions) |> Async.RunImmediate


let sysLib nm =
#if !NETCOREAPP
    if System.Environment.OSVersion.Platform = System.PlatformID.Win32NT then // file references only valid on Windows
        let programFilesx86Folder = System.Environment.GetEnvironmentVariable("PROGRAMFILES(X86)")
        programFilesx86Folder + @"\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\" + nm + ".dll"
    else
#endif
        let sysDir = System.AppContext.BaseDirectory
        let (++) a b = System.IO.Path.Combine(a,b)
        sysDir ++ nm + ".dll"

[<AutoOpen>]
module Helpers =
    type DummyType = A | B
    let PathRelativeToTestAssembly p = Path.Combine(Path.GetDirectoryName(Uri(typeof<FSharpChecker>.Assembly.Location).LocalPath), p)

let fsCoreDefaultReference() =
    PathRelativeToTestAssembly "FSharp.Core.dll"

let mkStandardProjectReferences () =
#if NETCOREAPP
            let file = "Sample_NETCoreSDK_FSharp_Library_netstandard2_0.fsproj"
            let projDir = Path.Combine(__SOURCE_DIRECTORY__, "../projects/Sample_NETCoreSDK_FSharp_Library_netstandard2_0")
            readRefs projDir file
#else
            [ yield sysLib "mscorlib"
              yield sysLib "System"
              yield sysLib "System.Core"
              yield sysLib "System.Numerics"
              yield fsCoreDefaultReference() ]
#endif

let mkProjectCommandLineArgsSilent (dllName, fileNames) =
  let args =
    [|  yield "--simpleresolution"
        yield "--noframework"
        yield "--debug:full"
        yield "--define:DEBUG"
#if NETCOREAPP
        yield "--targetprofile:netcore"
        yield "--langversion:preview"
#endif
        yield "--optimize-"
        yield "--out:" + dllName
        yield "--doc:test.xml"
        yield "--warn:3"
        yield "--fullpaths"
        yield "--flaterrors"
        yield "--target:library"
        for x in fileNames do
            yield x
        let references = mkStandardProjectReferences ()
        for r in references do
            yield "-r:" + r
     |]
  args

let mkProjectCommandLineArgs (dllName, fileNames) =
  let args = mkProjectCommandLineArgsSilent (dllName, fileNames)
  printfn "dllName = %A, args = %A" dllName args
  args

#if NETCOREAPP
let mkProjectCommandLineArgsForScript (dllName, fileNames) =
    [|  yield "--simpleresolution"
        yield "--noframework"
        yield "--debug:full"
        yield "--define:DEBUG"
        yield "--targetprofile:netcore"
        yield "--optimize-"
        yield "--out:" + dllName
        yield "--doc:test.xml"
        yield "--warn:3"
        yield "--fullpaths"
        yield "--flaterrors"
        yield "--target:library"
        for x in fileNames do
            yield x
        let references = mkStandardProjectReferences ()
        for r in references do
            yield "-r:" + r
     |]
#endif

let mkTestFileAndOptions source additionalArgs =
    let fileName = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    let project = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(project, ".dll")
    let projFileName = Path.ChangeExtension(project, ".fsproj")
    let fileSource1 = "module M"
    FileSystem.OpenFileForWriteShim(fileName).Write(fileSource1)

    let args = Array.append (mkProjectCommandLineArgs (dllName, [fileName])) additionalArgs
    let options = checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)
    fileName, options

let parseAndCheckFile fileName source options =
    match checker.ParseAndCheckFileInProject(fileName, 0, SourceText.ofString source, options) |> Async.RunImmediate with
    | parseResults, FSharpCheckFileAnswer.Succeeded(checkResults) -> parseResults, checkResults
    | _ -> failwithf "Parsing aborted unexpectedly..."

let parseAndCheckScriptWithOptions (file:string, input, opts) =

#if NETCOREAPP
    let projectOptions =
        let path = Path.Combine(Path.GetTempPath(), "tests", Process.GetCurrentProcess().Id.ToString() + "--"+ Guid.NewGuid().ToString())
        try
            if not (Directory.Exists(path)) then
                Directory.CreateDirectory(path) |> ignore

            let fname = Path.Combine(path, Path.GetFileName(file))
            let dllName = Path.ChangeExtension(fname, ".dll")
            let projName = Path.ChangeExtension(fname, ".fsproj")
            let args = mkProjectCommandLineArgsForScript (dllName, [file])
            printfn "file = %A, args = %A" file args
            checker.GetProjectOptionsFromCommandLineArgs (projName, args)

        finally
            if Directory.Exists(path) then
                Directory.Delete(path, true)

#else
    let projectOptions, _diagnostics = checker.GetProjectOptionsFromScript(file, SourceText.ofString input) |> Async.RunImmediate
    //printfn "projectOptions = %A" projectOptions
#endif

    let projectOptions = { projectOptions with OtherOptions = Array.append opts projectOptions.OtherOptions }
    let parseResult, typedRes = checker.ParseAndCheckFileInProject(file, 0, SourceText.ofString input, projectOptions) |> Async.RunImmediate

    // if parseResult.Errors.Length > 0 then
    //     printfn "---> Parse Input = %A" input
    //     printfn "---> Parse Error = %A" parseResult.Errors

    match typedRes with
    | FSharpCheckFileAnswer.Succeeded(res) -> parseResult, res
    | res -> failwithf "Parsing did not finish... (%A)" res

let parseAndCheckScript (file, input) = parseAndCheckScriptWithOptions (file, input, [| |])
let parseAndCheckScriptPreview (file, input) = parseAndCheckScriptWithOptions (file, input, [| "--langversion:preview" |])

let parseSourceCode (name: string, code: string) =
    let location = Path.Combine(Path.GetTempPath(),"test"+string(hash (name, code)))
    try Directory.CreateDirectory(location) |> ignore with _ -> ()
    let filePath = Path.Combine(location, name)
    let dllPath = Path.Combine(location, name + ".dll")
    let args = mkProjectCommandLineArgs(dllPath, [filePath])
    let options, errors = checker.GetParsingOptionsFromCommandLineArgs(List.ofArray args)
    let parseResults = checker.ParseFile(filePath, SourceText.ofString code, options) |> Async.RunImmediate
    parseResults.ParseTree

let matchBraces (name: string, code: string) =
    let location = Path.Combine(Path.GetTempPath(),"test"+string(hash (name, code)))
    try Directory.CreateDirectory(location) |> ignore with _ -> ()
    let filePath = Path.Combine(location, name + ".fs")
    let dllPath = Path.Combine(location, name + ".dll")
    let args = mkProjectCommandLineArgs(dllPath, [filePath])
    let options, errors = checker.GetParsingOptionsFromCommandLineArgs(List.ofArray args)
    let braces = checker.MatchBraces(filePath, SourceText.ofString code, options) |> Async.RunImmediate
    braces


let getSingleModuleLikeDecl (input: ParsedInput) =
    match input with
    | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ decl ])) -> decl
    | _ -> failwith "Could not get module decls"

let parseSourceCodeAndGetModule (source: string) =
    parseSourceCode ("test.fsx", source) |> getSingleModuleLikeDecl

/// Extract range info
let tups (m: range) = (m.StartLine, m.StartColumn), (m.EndLine, m.EndColumn)

/// Extract range info  and convert to zero-based line  - please don't use this one any more
let tupsZ (m: range) = (m.StartLine-1, m.StartColumn), (m.EndLine-1, m.EndColumn)

let attribsOfSymbolUse (s:FSharpSymbolUse) =
    [ if s.IsFromDefinition then yield "defn"
      if s.IsFromType then yield "type"
      if s.IsFromAttribute then yield "attribute"
      if s.IsFromDispatchSlotImplementation then yield "override"
      if s.IsFromPattern then yield "pattern"
      if s.IsFromComputationExpression then yield "compexpr" ]

let attribsOfSymbol (s:FSharpSymbol) =
    [ match s with
        | :? FSharpField as v ->
            yield "field"
            if v.IsCompilerGenerated then yield "compgen"
            if v.IsDefaultValue then yield "default"
            if v.IsMutable then yield "mutable"
            if v.IsVolatile then yield "volatile"
            if v.IsStatic then yield "static"
            if v.IsLiteral then yield sprintf "%A" v.LiteralValue.Value
            if v.IsAnonRecordField then
                let info, tys, i = v.AnonRecordFieldDetails
                yield "anon(" + string i + ", [" + info.Assembly.QualifiedName + "/" + String.concat "+" info.EnclosingCompiledTypeNames + "/" + info.CompiledName + "]" + String.concat "," info.SortedFieldNames + ")"


        | :? FSharpEntity as v ->
            v.TryFullName |> ignore // check there is no failure here
            if v.IsNamespace then yield "namespace"
            if v.IsFSharpModule then yield "module"
            if v.IsByRef then yield "byref"
            if v.IsClass then yield "class"
            if v.IsDelegate then yield "delegate"
            if v.IsEnum then yield "enum"
            if v.IsFSharpAbbreviation then yield "abbrev"
            if v.IsFSharpExceptionDeclaration then yield "exn"
            if v.IsFSharpRecord then yield "record"
            if v.IsFSharpUnion then yield "union"
            if v.IsInterface then yield "interface"
            if v.IsMeasure then yield "measure"
#if !NO_EXTENSIONTYPING
            if v.IsProvided then yield "provided"
            if v.IsStaticInstantiation then yield "staticinst"
            if v.IsProvidedAndErased then yield "erased"
            if v.IsProvidedAndGenerated then yield "generated"
#endif
            if v.IsUnresolved then yield "unresolved"
            if v.IsValueType then yield "valuetype"

        | :? FSharpMemberOrFunctionOrValue as v ->
            if v.IsActivePattern then yield "apat"
            if v.IsDispatchSlot then yield "slot"
            if v.IsModuleValueOrMember && not v.IsMember then yield "val"
            if v.IsMember then yield "member"
            if v.IsProperty then yield "prop"
            if v.IsExtensionMember then yield "extmem"
            if v.IsPropertyGetterMethod then yield "getter"
            if v.IsPropertySetterMethod then yield "setter"
            if v.IsEvent then yield "event"
            if v.EventForFSharpProperty.IsSome then yield "clievent"
            if v.IsEventAddMethod then yield "add"
            if v.IsEventRemoveMethod then yield "remove"
            if v.IsTypeFunction then yield "typefun"
            if v.IsCompilerGenerated then yield "compgen"
            if v.IsImplicitConstructor then yield "ctor"
            if v.IsMutable then yield "mutable"
            if v.IsOverrideOrExplicitInterfaceImplementation then yield "overridemem"
            if v.IsInstanceMember && not v.IsInstanceMemberInCompiledCode && not v.IsExtensionMember then yield "funky"
            if v.IsExplicitInterfaceImplementation then yield "intfmem"
//            if v.IsConstructorThisValue then yield "ctorthis"
//            if v.IsMemberThisValue then yield "this"
//            if v.LiteralValue.IsSome then yield "literal"
        | _ -> () ]

let rec allSymbolsInEntities compGen (entities: IList<FSharpEntity>) =
    [ for e in entities do
          yield (e :> FSharpSymbol)
          for gp in e.GenericParameters do
            if compGen || not gp.IsCompilerGenerated then
             yield (gp :> FSharpSymbol)
          for x in e.MembersFunctionsAndValues do
             if compGen || not x.IsCompilerGenerated then
               yield (x :> FSharpSymbol)
             for gp in x.GenericParameters do
              if compGen || not gp.IsCompilerGenerated then
               yield (gp :> FSharpSymbol)
          for x in e.UnionCases do
             yield (x :> FSharpSymbol)
             for f in x.Fields do
                 if compGen || not f.IsCompilerGenerated then
                     yield (f :> FSharpSymbol)
          for x in e.FSharpFields do
             if compGen || not x.IsCompilerGenerated then
                 yield (x :> FSharpSymbol)
          yield! allSymbolsInEntities compGen e.NestedEntities ]


let getParseResults (source: string) =
    parseSourceCode("/home/user/Test.fsx", source)

let getParseResultsOfSignatureFile (source: string) =
    parseSourceCode("/home/user/Test.fsi", source)

let getParseAndCheckResults (source: string) =
    parseAndCheckScript("/home/user/Test.fsx", source)

let getParseAndCheckResultsPreview (source: string) =
    parseAndCheckScriptPreview("/home/user/Test.fsx", source)

let inline dumpErrors results =
    (^TResults: (member Diagnostics: FSharpDiagnostic[]) results)
    |> Array.map (fun e ->
        let message =
            e.Message.Split('\n')
            |> Array.map (fun s -> s.Trim())
            |> String.concat " "
        sprintf "%s: %s" (e.Range.ToShortString()) message)
    |> List.ofArray

let getSymbolUses (results: FSharpCheckFileResults) =
    results.GetAllUsesOfAllSymbolsInFile()

let getSymbolUsesFromSource (source: string) =
    let _, typeCheckResults = getParseAndCheckResults source
    typeCheckResults.GetAllUsesOfAllSymbolsInFile()

let getSymbols (symbolUses: seq<FSharpSymbolUse>) =
    symbolUses |> Seq.map (fun symbolUse -> symbolUse.Symbol)


let getSymbolName (symbol: FSharpSymbol) =
    match symbol with
    | :? FSharpMemberOrFunctionOrValue as mfv -> Some mfv.LogicalName
    | :? FSharpEntity as entity -> Some entity.LogicalName
    | :? FSharpGenericParameter as parameter -> Some parameter.Name
    | :? FSharpParameter as parameter -> parameter.Name
    | :? FSharpStaticParameter as parameter -> Some parameter.Name
    | :? FSharpActivePatternCase as case -> Some case.Name
    | :? FSharpUnionCase as case -> Some case.Name
    | _ -> None


let assertContainsSymbolWithName name source =
    getSymbols source
    |> Seq.choose getSymbolName
    |> Seq.contains name
    |> shouldEqual true

let assertContainsSymbolsWithNames (names: string list) source =
    let symbolNames =
        getSymbols source
        |> Seq.choose getSymbolName

    for name in names do
        symbolNames
        |> Seq.contains name
        |> shouldEqual true

let assertHasSymbolUsages (names: string list) (results: FSharpCheckFileResults) =
    let symbolNames =
        getSymbolUses results
        |> getSymbols
        |> Seq.choose getSymbolName
        |> set

    for name in names do
        Assert.That(Set.contains name symbolNames, name)


let findSymbolUseByName (name: string) (results: FSharpCheckFileResults) =
    getSymbolUses results
    |> Seq.find (fun symbolUse ->
        match getSymbolName symbolUse.Symbol with
        | Some symbolName -> symbolName = name
        | _ -> false)

let findSymbolByName (name: string) (results: FSharpCheckFileResults) =
    let symbolUse = findSymbolUseByName name results
    symbolUse.Symbol

let taggedTextToString (tts: TaggedText[]) =
    tts |> Array.map (fun tt -> tt.Text) |> String.concat ""

let getRangeCoords (r: range) =
    (r.StartLine, r.StartColumn), (r.EndLine, r.EndColumn)

let coreLibAssemblyName =
#if NETCOREAPP
    "System.Runtime"
#else
    "mscorlib"
#endif

let assertRange
    (expectedStartLine: int, expectedStartColumn: int)
    (expectedEndLine: int, expectedEndColumn: int)
    (actualRange: range)
    : unit =
    Assert.AreEqual(Position.mkPos expectedStartLine expectedStartColumn, actualRange.Start)
    Assert.AreEqual(Position.mkPos expectedEndLine expectedEndColumn, actualRange.End)

