// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace UnitTests.TestLib.LanguageService

open System
open System.Reflection
open NUnit.Framework
open System.Diagnostics
open System.IO
open Salsa.Salsa
open Salsa.VsOpsUtils
open Salsa.VsMocks
open UnitTests.TestLib.Salsa
open UnitTests.TestLib.Utils
open System.Text.RegularExpressions 
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open Microsoft.VisualStudio.FSharp

#nowarn "52" // The value has been copied to ensure the original is not mutated

[<AutoOpen>]
module internal Globals =
    let checker = FSharpChecker.Create(legacyReferenceResolver=LegacyMSBuildReferenceResolver.getResolver())


//open Internal.Utilities
type internal TextSpan       = Microsoft.VisualStudio.TextManager.Interop.TextSpan

type internal SourceFileKind = FS | FSI | FSX

type internal ISingleFileTestRunner = 
    abstract CreateSingleFileProject :
        content : string * 
        ?references : list<string> * 
        ?defines : list<string> * 
        ?fileKind : SourceFileKind *
        ?disabledWarnings : list<string> * 
        ?fileName : string -> (OpenSolution * OpenProject * OpenFile)
    abstract CreateSingleFileProject :
        content : list<string> * 
        ?references : list<string> * 
        ?defines : list<string> * 
        ?fileKind : SourceFileKind *
        ?disabledWarnings : list<string>* 
        ? fileName : string -> (OpenSolution * OpenProject * OpenFile)

type internal Helper =
    static member TrimOutExtraMscorlibs (libList:list<string>) =
        // There may be multiple copies of mscorlib referenced; but we're only allowed to use one.  Pick the highest one.
        let allExceptMscorlib = libList |> List.filter (fun s -> not(s.Contains("mscorlib")))
        let mscorlibs = libList |> List.filter (fun s -> s.Contains("mscorlib"))
        // contain e.g. "mscorlib, Version=4.0.0.0 ..."
        let bestMscorlib = mscorlibs |> List.sort |> List.rev |> List.head
        bestMscorlib :: allExceptMscorlib

    static member ExhaustivelyScrutinize (sftr : ISingleFileTestRunner, lines:string list) = 
        let Impl kind = 
            let (_solution, _project, file) = sftr.CreateSingleFileProject(lines, fileKind = kind)
            let Check line col = 
                //printfn "line=%d col=%d" line col
                MoveCursorTo(file,line,col)
                let tooltip = GetQuickInfoAtCursor file
                let parameterInfo = GetParameterInfoAtCursor file
                let squiggle = GetSquiggleAtCursor file
                let tokenType = GetTokenTypeAtCursor file
                let ctrlspacecompletion = CtrlSpaceCompleteAtCursor file
                if col > 1 && lines.[line-1].[col-2] = '.' then  // -2 because, -1 to get to 0-based coords, and -1 more because want to look one char left of cursor for the dot
                    let autocompletion = AutoCompleteAtCursor file
                    ()
                ()
            let lines = lines |> List.toArray
            let lineCount = lines.Length
            for line in 1..lineCount do 
                let len = lines.[line-1].Length
                for col in 1..len do 
                    Check line col
        Impl SourceFileKind.FS
        Impl SourceFileKind.FSX

    static member AssertMemberDataTipContainsInOrder(sftr : ISingleFileTestRunner, code : list<string>,marker,completionName,rhsContainsOrder) =
        let (_solution, project, file) = sftr.CreateSingleFileProject(code, fileKind = SourceFileKind.FSX)
        TakeCoffeeBreak(file.VS) (* why needed? *)       
        MoveCursorToEndOfMarker(file,marker)
        let completions = AutoCompleteAtCursor file
        match completions |> Array.tryFind (fun (CompletionItem(name, _, _, _, _)) -> name = completionName) with
        | Some(CompletionItem(_, _, _, descrFunc, _)) ->
            let descr = descrFunc()
            AssertContainsInOrder(descr,rhsContainsOrder)
        | None -> 
            Console.WriteLine("Could not find completion name '{0}'", completionName)
            for error in (GetErrors(project)) do
                printf "%s\n" (error.ToString())
            Assert.Fail()   

    (* Asserts ----------------------------------------------------------------------------- *)
    static member AssertEqualWithMessage(expected,actual,message) =
        if expected<>actual then 
            printf "%s" message
            Assert.Fail(message)
    static member AssertEqual(expected,actual) =
        if expected<>actual then 
            let message = sprintf "Expected %A but got %A." expected actual
            printf "%s" message
            Assert.Fail(message)
    
    static member AssertContains(s:string,c) =
        if not (s.Contains(c)) then
            printf "Expected '%s' to contain '%s'." s c
            Assert.Fail()
    static member AssertArrayContainsPartialMatchOf(a:string array,c) =
        let found = ref false
        a |> Array.iter(fun s -> found := s.Contains(c) || !found)
        if not(!found) then 
            printfn "Expected: %A" a            
            printfn "to contain '%s'." c
            Assert.Fail()            
    static member AssertNotContains(s:string,c) = 
        if (s.Contains(c)) then
            printf "Expected '%s' to not contain '%s'." s c
            Assert.Fail()   

    static member AssertMatches (r : Regex) (s:string) =
        if not (r.IsMatch(s)) then
            printfn "Expected regex '%s' to match '%s'." (r.ToString()) s
            Assert.Fail()

    static member AssertContainsInOrder(s:string,cs:string list) =
        let rec containsInOrderFrom fromIndex expects =
          match expects with
            | [] -> ()
            | expect :: expects ->
                let index = s.IndexOf((expect:string),(fromIndex:int))           
                if index = -1 then
                    let s= sprintf "Expected '%s' to contain '%s' after index %d." s expect fromIndex
                    Console.WriteLine(s)
                    Assert.Fail(s)
                else
                   printfn "At index %d seen '%s'." index expect
                containsInOrderFrom index expects
        containsInOrderFrom 0 cs

    static member AssertListContainsInOrder(s:string list,cs:string list) =
        let s : string array = Array.ofList s
        let s : string = String.Join("\n",s)
        AssertContainsInOrder(s,cs)

        // Like AssertMatches, but runs for every prefix of regex up to each occurence of 'c'
    // Is helpful so that, if long regex match fails, you see first prefix that fails
    static member AssertMatchesRegex (c : char) (regexStr : string) (s:string) =
        let mutable i = regexStr.IndexOf(c, 0)
        while i <> -1 do
            let r = regexStr.Substring(0,i)
            let regex = new Regex(r)
            AssertMatches regex s
            i <- regexStr.IndexOf(c, i+1)       

type internal GlobalParseAndTypeCheckCounter private(initialParseCount:int, initialTypeCheckCount:int, initialEventNum:int, vs) =
    static member StartNew(vs) =
        TakeCoffeeBreak(vs)
        let n = IncrementalBuilderEventTesting.GetCurrentIncrementalBuildEventNum()
        new GlobalParseAndTypeCheckCounter(FSharpChecker.ActualParseFileCount, FSharpChecker.ActualCheckFileCount, n, vs)
    member private this.GetEvents() = 
        TakeCoffeeBreak(vs)
        let n = IncrementalBuilderEventTesting.GetCurrentIncrementalBuildEventNum()
        IncrementalBuilderEventTesting.GetMostRecentIncrementalBuildEvents(n-initialEventNum)
    member private this.SawIBCreated() = 
        this.GetEvents() |> List.exists (function | IncrementalBuilderEventTesting.IBECreated -> true | _ -> false)
    member private this.GetParsedFilesSet() = 
        this.GetEvents() |> List.choose (function | IncrementalBuilderEventTesting.IBEParsed(file) -> Some(file) | _ -> None) |> set
    member private this.GetTypeCheckedFilesSet() = 
        this.GetEvents() |> List.choose (function | IncrementalBuilderEventTesting.IBETypechecked(file) -> Some(file) | _ -> None) |> set
    member this.AssertExactly(expectedParses, expectedTypeChecks) =
        let actualParses = this.GetParsedFilesSet().Count 
        let actualTypeChecks = this.GetTypeCheckedFilesSet().Count
        if (actualParses,actualTypeChecks) <> (expectedParses, expectedTypeChecks) then
            Assert.Fail(sprintf "Expected %d parses and %d typechecks, but got %d parses and %d typechecks." expectedParses expectedTypeChecks actualParses actualTypeChecks)
    member this.AssertExactly(expectedParses, expectedTypeChecks, expectedParsedFiles : list<OpenFile>, expectedTypeCheckedFiles : list<OpenFile>) =
        this.AssertExactly(expectedParses, 
                           expectedTypeChecks, 
                           expectedParsedFiles |> List.map GetNameOfOpenFile, 
                           expectedTypeCheckedFiles |> List.map GetNameOfOpenFile,
                           false)
    member this.AssertExactly((aap: string option,expectedParsedFiles:list<OpenFile>), (aat: string option,expectedTypeCheckedFiles:list<OpenFile>)) =
        this.AssertExactly((aap,expectedParsedFiles), (aat,expectedTypeCheckedFiles), false)
    member this.AssertExactly((aap,expectedParsedFiles) : string option * list<OpenFile>, (aat,expectedTypeCheckedFiles) : string option * list<OpenFile>, expectCreate : bool) =
        let p = 
            match aap with 
            | Some(aap) -> aap :: (expectedParsedFiles |> List.map GetNameOfOpenFile)
            | _ -> (expectedParsedFiles |> List.map GetNameOfOpenFile)
        let t = 
            match aat with
            | Some(aat) -> aat :: (expectedTypeCheckedFiles |> List.map GetNameOfOpenFile)
            | _ -> (expectedTypeCheckedFiles |> List.map GetNameOfOpenFile)
        this.AssertExactly(p.Length, t.Length, p, t, expectCreate)
    member private this.AssertExactly(expectedParses, expectedTypeChecks, expectedParsedFiles : list<string>, expectedTypeCheckedFiles : list<string>, expectCreate : bool) =
        let note,ok = 
            if expectCreate then
                if this.SawIBCreated() then ("The incremental builder was created, as expected",true) else ("The incremental builder was NOT deleted and recreated, even though we expected it to be",false)
            else
                if this.SawIBCreated() then ("The incremental builder was UNEXPECTEDLY deleted",false) else ("",true)
        let actualParsedFiles = this.GetParsedFilesSet()
        let actualTypeCheckedFiles = this.GetTypeCheckedFilesSet()
        let actualParses = actualParsedFiles.Count 
        let actualTypeChecks = actualTypeCheckedFiles.Count
        let expectedParsedFiles = expectedParsedFiles |> set
        let expectedTypeCheckedFiles = expectedTypeCheckedFiles |> set
        if (actualParses, actualTypeChecks, actualParsedFiles, actualTypeCheckedFiles) <> (expectedParses, expectedTypeChecks, expectedParsedFiles, expectedTypeCheckedFiles) then
            let detail = sprintf "ExpectedParse: %A\nActualParse:   %A\n\nExpectedTypeCheck: %A\nActualTypeCheck:   %A\n\n%s"
                                    expectedParsedFiles actualParsedFiles expectedTypeCheckedFiles actualTypeCheckedFiles note
            let msg = 
                if (actualParses, actualTypeChecks) <> (expectedParses, expectedTypeChecks) then
                    sprintf "Expected %d parses and %d typechecks, but got %d parses and %d typechecks.\n\n%s" expectedParses expectedTypeChecks actualParses actualTypeChecks detail
                else
                    sprintf "Expected %d parses and %d typechecks, and got those counts, but unexpected file sets:\n\n%s" expectedParses expectedTypeChecks detail
            Assert.Fail(msg)
        elif not ok then
            Assert.Fail(sprintf "Got expected events, but also: %s" note)

/// REVIEW: Should be able to get data tip when hovering over a class member method name ie  "member private art.attemptUpgradeToMSBuild hierarchy"

(* Not Unittested Yet ----------------------------------------------------------------------------------------------------------------- *)        
/// FEATURE: Pressing ctrl-j or ctrl-space will bring up the Intellisense completion list at the current cursor.
/// FEATURE: String literals such as "My dog has fleas" will be colored in in String color.
/// FEATURE: Source code files with extensions .fs and .ml are recognized by the language service.
/// FEATURE: Interface files with extensions .fsi and .mli are recognized by the language service.
/// FEATURE: Double-clicking a word in a comment highlight just the word not the whole comment.
/// FEATURE: During debugging user may hover over a tooltip and get debugging information about. For example, instance values.
/// FEATURE: Character literals such as 'x' will be colored in in String color.
(* ------------------------------------------------------------------------------------------------------------------------------------ *)        

/// FEATURE(nyi): As the user types part of an identifier, for example Console.Wr, he'll get an Intellisense list of members that match.
/// FEATURE(nyi): The user can press ctrl-i to start an incremental search within the current document.
/// FEATURE(nyi): Pressing ctrl-} will move cursor to the matching brace. This will work for all brace types.
/// FEATURE(nyi): If a source file has a #r or #R reference then changes to the referenced assembly will be recognized by the language service.
/// FEATURE(nyi): If a source file used to have a #r or #R reference and then the user removes it, that assembly will no longer be in scope.
    
/// FEATURE(nyi): There is a navigation bar at the top of the text editor window that shows the user all the top-level and second-level constructs in the file.
/// FEATURE(nyi): Types in the code will be colored with a special BoundType color
/// FEATURE(nyi): Preprocessor-like keywords aside from #light\#if\#else\#endif will be colored with PreprocessorKeyword color.
/// FEATURE(nyi): Intellisense for argument names.
    
/// PS-FEATURE(nyi): The user may choose to enable mixed-mode debugging by selecting Project Settings\Debug\Enable unamanaged code debugging

/// These are the driver tests. They're parameterized on
/// various functions that abstract actions over vs.
type LanguageServiceBaseTests() =  

    let _resolver = AssemblyResolver.addResolver ()

    let mutable defaultSolution : OpenSolution = Unchecked.defaultof<_>
    let cache = System.Collections.Generic.Dictionary()

    let mutable defaultVS : VisualStudio = Unchecked.defaultof<_>
    let mutable currentVS : VisualStudio = Unchecked.defaultof<_>

    (* VsOps is internal, but this type needs to be public *)
    let mutable ops = BuiltMSBuildTestFlavour()
    let testStopwatch = new Stopwatch()

    (* Timings ----------------------------------------------------------------------------- *)
    let stopWatch = new Stopwatch()
    let ResetStopWatch() = stopWatch.Reset(); stopWatch.Start()
    let time1 op a message = 
        ResetStopWatch()
        let result = op a
        printf "%s %d ms\n" message stopWatch.ElapsedMilliseconds
        result                         
               
    member internal this.VsOpts
        with set op = ops <- op
    
    member internal this.TestRunner : ISingleFileTestRunner = SingleFileTestRunner(this) :> _

    member internal this.VS = currentVS

    member internal this.CreateSingleFileProject
        (
            content : string, 
            ?references : list<string>, 
            ?defines : list<string>, 
            ?fileKind : SourceFileKind, 
            ?disabledWarnings : list<string>,
            ?fileName : string,
            ?otherFlags: string
        ) = 
        let content = content.Split( [|"\r\n"|], StringSplitOptions.None) |> List.ofArray
        this.CreateSingleFileProject(content, ?references = references, ?defines = defines, ?fileKind = fileKind, ?disabledWarnings = disabledWarnings, ?fileName = fileName, ?otherFlags = otherFlags)

    member internal this.CreateSingleFileProject
        (
            content : list<string>, 
            ?references : list<string>, 
            ?defines : list<string>, 
            ?fileKind : SourceFileKind, 
            ?disabledWarnings : list<string>,
            ?fileName : string,
            ?otherFlags: string
        ) = 
        assert (box currentVS = box defaultVS)
        let mkKeyComponent l = 
            defaultArg l []
            |> Seq.sort
            |> String.concat "|"
        let ext = 
            match fileKind with
            | Some SourceFileKind.FS -> ".fs"
            | Some SourceFileKind.FSI -> ".fsi"
            | Some SourceFileKind.FSX -> ".fsx"
            | None -> ".fs"
        let fileName = (defaultArg fileName "File1") + ext

        let key = 
            let refs = mkKeyComponent references
            let defines = mkKeyComponent defines
            let warnings = mkKeyComponent disabledWarnings
            (refs, defines, warnings, otherFlags, fileName.ToLower())

        match cache.TryGetValue key with
        | true, (proj, file) ->
            ReplaceFileInMemory file []
            SaveFileToDisk file
            TakeCoffeeBreak(currentVS)
            
            ReplaceFileInMemory file content
            SaveFileToDisk file
            TakeCoffeeBreak(currentVS)
            defaultSolution, proj, file
        | false, _ ->
            let name = string(Guid.NewGuid())
            let proj = CreateProject(defaultSolution, name)

            for dw in (defaultArg disabledWarnings []) do
                GlobalFunctions.AddDisabledWarning(proj, dw)

            if defines.IsSome then 
                GlobalFunctions.SetProjectDefines(proj, defines.Value)

            for r in (defaultArg references []) do 
                GlobalFunctions.AddAssemblyReference(proj, r)

            match otherFlags with 
            | None -> ()
            | Some flags -> GlobalFunctions.SetOtherFlags(proj, flags) 

            let content = String.concat Environment.NewLine content
            let _ = AddFileFromTextBlob(proj, fileName, content)
            let file = OpenFile(proj, fileName)

            cache.[key] <- (proj, file)

            TakeCoffeeBreak(currentVS)

            defaultSolution, proj, file
    
    member internal this.CreateSolution() = 
        if (box currentVS = box defaultVS) then
            failwith "You are trying to modify default instance of VS. The only operation that is permitted on default instance is CreateSingleFileProject, perhaps you forgot to add line 'use _guard = this.WithNewVS()' at the beginning of the test?"
        GlobalFunctions.CreateSolution(currentVS)

    member internal this.CloseSolution(sln : OpenSolution) = 
        if (box currentVS = box defaultVS) then
            failwith "You are trying to modify default instance of VS. The only operation that is permitted on default instance is CreateSingleFileProject, perhaps you forgot to add line 'use _guard = this.WithNewVS()' at the beginning of the test?"
        if (box sln.VS <> box currentVS) then
            failwith "You are trying to close solution that is not belongs to the active instance VS. This may denote the presence of errors in tests."

        GlobalFunctions.CloseSolution(sln)

    member internal this.AddAssemblyReference(proj, ref) = 
        if (box currentVS = box defaultVS) then
            failwith "You are trying to modify default instance of VS. The only operation that is permitted on default instance is CreateSingleFileProject, perhaps you forgot to add line 'use _guard = this.WithNewVS()' at the beginning of the test?"

        GlobalFunctions.AddAssemblyReference(proj, ref)

    /// Called per test run
#if NUNIT_V2
    [<TestFixtureSetUp>]
    member this.TestFixtureSetUp() =
#else
    [<OneTimeSetUp>]
    member this.Init() =
#endif
        let AssertNotAssemblyNameContains(a:System.Reflection.Assembly, text1:string, text2:string) = 
            let fullname = sprintf "%A" a
            if fullname.Contains(text1) && fullname.Contains(text2) then
                // Can't throw an exception here because its in an event handler.
                System.Diagnostics.Debug.Assert(false, sprintf "Unexpected: loaded assembly '%s' to not contain '%s' and '%s'" fullname text1 text2)

        // Under .NET 4.0 we don't allow 3.5.0.0 assemblies
        let AssertNotBackVersionAssembly(args:AssemblyLoadEventArgs) =

            // We're worried about loading these when running against .NET 4.0:
            // Microsoft.Build.Tasks.v3.5, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
            // Microsoft.Build.Utilities.v3.5, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
            AssertNotAssemblyNameContains(args.LoadedAssembly,"Microsoft.Build", "Version=3.5.0.0") 
            ()
        AppDomain.CurrentDomain.AssemblyLoad.Add AssertNotBackVersionAssembly

        UIStuff.SetupSynchronizationContext()

        defaultVS <- ops.CreateVisualStudio()
        currentVS <- defaultVS
        
        defaultSolution <- GlobalFunctions.CreateSolution(defaultVS)
        cache.Clear()

#if NUNIT_V2
    [<TestFixtureTearDown>]
    member this.Shutdown() =
#else
    [<OneTimeTearDown>]
    member this.Cleanup() =
#endif
        if box currentVS <> box defaultVS then
            failwith "LanguageServiceBaseTests.Shutdown was called when 'active' instance of VS is not 'default' one - this may denote that tests contains errors"
        
        GlobalFunctions.Cleanup(defaultVS)
        cache.Clear()

    member this.UsingNewVS() = 
        if box currentVS <> box defaultVS then
            failwith "LanguageServiceBaseTests.UsingNewVS was called when 'active' instance of VS is not 'default' one - this may denote that tests contains errors"
        currentVS <- ops.CreateVisualStudio()
        { new System.IDisposable with 
            member _.Dispose() = 
                if box currentVS = box defaultVS then
                    failwith "At this moment 'current' instance of VS cannot be the same as the 'default' one. This may denote that tests contains errors."
                GlobalFunctions.Cleanup(currentVS)
                currentVS <- defaultVS }


    /// Called per test
    [<SetUp>]
    member this.Setup() =
        if box currentVS <> box defaultVS then
            failwith "LanguageServiceBaseTests.Setup was called when 'active' instance of VS is not 'default' one - this may denote that tests contains errors"
        
        // reset state of default VS instance that can be shared among the tests
        ShiftKeyUp(currentVS)
        ops.CleanInvisibleProject(currentVS)

        ResetStopWatch()
        testStopwatch.Reset()
        testStopwatch.Start()
        ()
        
    /// Called per test
    [<TearDown>]
    member this.TearDown() =


#if DEBUG_FIND_SLOWEST_TESTS
        if testStopwatch.ElapsedMilliseconds > 15000L then
            let msg = sprintf "a test took %dms" testStopwatch.ElapsedMilliseconds
            stderr.WriteLine(msg)
            System.Console.Beep()
            System.Windows.MessageBox.Show(msg) |> ignore
#endif
        // help find leaks per-test
//        System.GC.Collect()  
//        System.GC.WaitForPendingFinalizers()
        ()

and internal SingleFileTestRunner(owner : LanguageServiceBaseTests) =
    interface ISingleFileTestRunner with
        member sftr.CreateSingleFileProject
            (
                content : string, 
                ?references : list<string>, 
                ?defines : list<string>, 
                ?fileKind : SourceFileKind, 
                ?disabledWarnings : list<string>,
                ?fileName : string
            ) = 
            owner.CreateSingleFileProject(content, ?references = references, ?defines = defines, ?fileKind = fileKind, ?disabledWarnings = disabledWarnings, ?fileName = fileName)

        member sftr.CreateSingleFileProject
            (
                content : list<string>, 
                ?references : list<string>, 
                ?defines : list<string>, 
                ?fileKind : SourceFileKind, 
                ?disabledWarnings : list<string>,
                ?fileName : string
            ) = 
            owner.CreateSingleFileProject(content, ?references = references, ?defines = defines, ?fileKind = fileKind, ?disabledWarnings = disabledWarnings, ?fileName = fileName)
