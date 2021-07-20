// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Tests.LanguageService.AutoCompletion

open System
open Microsoft.VisualStudio.FSharp.LanguageService
open Salsa.Salsa
open Salsa.VsMocks
open Salsa.VsOpsUtils
open NUnit.Framework
open UnitTests.TestLib.Salsa
open UnitTests.TestLib.Utils
open UnitTests.TestLib.LanguageService
open UnitTests.TestLib.ProjectSystem

[<AutoOpen>]
module StandardSettings = 
    let standard40AssemblyRefs  = [ "System"; "System.Core"; "System.Numerics" ]
    let queryAssemblyRefs = [ "System.Xml.Linq"; "System.Core" ]
    type Expectation = 
        | QuickInfoExpected of string * string
        | AutoCompleteExpected of string * string
        | DotCompleteExpected of string * string
    let QI x y = QuickInfoExpected(x,y)
    let AC x y = AutoCompleteExpected(x,y)
    let DC x y = DotCompleteExpected(x,y)

[<TestFixture>][<Category "LanguageService">]  
type UsingMSBuild() as this  = 
    inherit LanguageServiceBaseTests()

    let createFile (code : list<string>) fileKind refs otherFlags = 
        let (_, _, file) = 
            match code with
            | [code] when code.IndexOfAny([|'\r'; '\n'|]) <> -1 ->
                this.CreateSingleFileProject(code, fileKind = fileKind, references = refs, ?otherFlags=otherFlags)
            | code ->
                this.CreateSingleFileProject(code, fileKind = fileKind, references = refs, ?otherFlags=otherFlags)
        file

    let DoWithAutoCompleteUsingExtraRefs refs otherFlags coffeeBreak fileKind reason (code : list<string>) marker f  =        
        // Up to 2 untyped parse operations are OK: we do an initial parse to provide breakpoint valdiation etc. 
        // This might be before the before the background builder is ready to process the foreground typecheck.
        // In this case the background builder calls us back when its ready, and we then request a foreground typecheck 
        let file = createFile code fileKind refs otherFlags
            
        if coffeeBreak then
            TakeCoffeeBreak(this.VS)
        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)
        MoveCursorToEndOfMarker(file, marker)
        let completions = CompleteAtCursorForReason(file,reason)
        f completions
        gpatcc.AssertExactly(0,0)


    let DoWithAutoComplete coffeeBreak fileKind reason otherFlags (code : list<string>) marker f  =
        DoWithAutoCompleteUsingExtraRefs [] otherFlags coffeeBreak fileKind reason code marker f

    let AssertAutoCompleteContainsAux coffeeBreak filename reason otherFlags code marker  should shouldnot  =        
        DoWithAutoComplete coffeeBreak filename reason otherFlags code marker (fun completions ->
            AssertCompListContainsAll(completions, should)
            AssertCompListDoesNotContainAny(completions, shouldnot))

    let AssertAutoCompleteContains =
        AssertAutoCompleteContainsAux true SourceFileKind.FS BackgroundRequestReason.MemberSelect None

    let AssertAutoCompleteContainsNoCoffeeBreak = 
        AssertAutoCompleteContainsAux false SourceFileKind.FS BackgroundRequestReason.MemberSelect None

    let AutoCompleteInInterfaceFileContains = 
        AssertAutoCompleteContainsAux true SourceFileKind.FSI BackgroundRequestReason.MemberSelect None

    let AssertCtrlSpaceCompleteContains = 
        AssertAutoCompleteContainsAux true SourceFileKind.FS BackgroundRequestReason.CompleteWord None

    let AssertCtrlSpaceCompleteContainsWithOtherFlags otherFlags = 
        AssertAutoCompleteContainsAux true SourceFileKind.FS BackgroundRequestReason.CompleteWord (Some otherFlags)

    let AssertCtrlSpaceCompleteContainsNoCoffeeBreak = 
        AssertAutoCompleteContainsAux false SourceFileKind.FS BackgroundRequestReason.CompleteWord None
    
    let AssertCtrlSpaceCompletionListIsEmpty code marker = 
        DoWithAutoComplete true SourceFileKind.FS BackgroundRequestReason.CompleteWord None code marker AssertCompListIsEmpty

    let AssertCtrlSpaceCompletionListIsEmptyNoCoffeeBreak code marker = 
        DoWithAutoComplete false SourceFileKind.FS BackgroundRequestReason.CompleteWord None code marker AssertCompListIsEmpty

    let AssertAutoCompleteCompletionListIsEmpty code marker = 
        DoWithAutoComplete true SourceFileKind.FS BackgroundRequestReason.MemberSelect None code marker AssertCompListIsEmpty

    let AssertAutoCompleteCompletionListIsEmptyNoCoffeeBreak code marker = 
        DoWithAutoComplete false SourceFileKind.FS BackgroundRequestReason.MemberSelect None code marker AssertCompListIsEmpty

    let testAutoCompleteAdjacentToDot op =
        let text = sprintf "System.Console%s" op

        // First, test that pressing dot works.
        AssertAutoCompleteContainsNoCoffeeBreak 
          [ text ]
          "System.Console."
          [ "BackgroundColor" ] // should contain
          [ ] // should not contain
 
    let testAutoCompleteAdjacentToDotNegative op =
        let text = sprintf "System.Console%s" op

        // Next test that there is no completion after then end.
        AssertCtrlSpaceCompleteContainsNoCoffeeBreak 
          [ text ]
          text
          [ "abs" ] // should contain (top-level autocomplete on empty identifier)
          [ "BackgroundColor" ] // should not contain (from prior System.Console)

    let customOperations = 
      [ "contains"; "count";"last"; "lastOrDefault"; "exactlyOne"; "exactlyOneOrDefault"; "headOrDefault"; "select"; "where"
        "minBy"; "maxBy"; "groupBy"; "sortBy"; "sortByDescending"; "thenBy"; "thenByDescending"; "groupValBy"; "join"
        "groupJoin"; "sumByNullable"; "minByNullable"; "maxByNullable"; "averageByNullable"; "averageBy"
        "distinct"; "exists"; "find"; "all"; "head"; "nth"; "skip"; "skipWhile"; "sumBy"; "take"
        "takeWhile"; "sortByNullable"; "sortByNullableDescending"; "thenByNullable"; "thenByNullableDescending"]

    let AA l = Some(System.IO.Path.Combine(System.IO.Path.GetTempPath(), ".NETFramework,Version=v4.0.AssemblyAttributes.fs")), l
    let notAA l = None,l
    let stopWatch = new System.Diagnostics.Stopwatch()
    let ResetStopWatch() = stopWatch.Reset(); stopWatch.Start()
    let time1 op a message = 
        ResetStopWatch()
        let result = op a
        //printf "%s %d ms\n" message stopWatch.ElapsedMilliseconds
        result

    let ShowErrors(project:OpenProject) =     
        for error in (GetErrors(project)) do
            printf "%s\n" (error.ToString())    


    // There are some dot completion tests in this type as well, in the systematic tests for queries
    member private this.VerifyDotCompListContainAllAtStartOfMarker(fileContents : string, marker : string, list :string list, ?addtlRefAssy:list<string>, ?coffeeBreak:bool) =
        let (solution, project, file) = this.CreateSingleFileProject(fileContents, ?references = addtlRefAssy)

        //to add references
        if defaultArg coffeeBreak false then TakeCoffeeBreak(this.VS)
        let completions = DotCompletionAtStartOfMarker file marker
        AssertCompListContainsAll(completions, list)

    // There are some quickinfo tests in this file as well, in the systematic tests for queries
    member public this.InfoInDeclarationTestQuickInfoImpl(code : string,marker,expected,atStart, ?addtlRefAssy : list<string>) =
        let (solution, project, file) = this.CreateSingleFileProject(code, ?references = addtlRefAssy)

        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)
        if atStart then
            MoveCursorToStartOfMarker(file, marker)
        else
            MoveCursorToEndOfMarker(file, marker)

        let tooltip = time1 GetQuickInfoAtCursor file "Time of first tooltip"
        AssertContains(tooltip, expected) 
        gpatcc.AssertExactly(0,0)

    member public this.AssertQuickInfoContainsAtEndOfMarker(code,marker,expected, ?addtlRefAssy : list<string>) =
        this.InfoInDeclarationTestQuickInfoImpl(code,marker,expected,false,?addtlRefAssy=addtlRefAssy)
    static member charExpectedCompletions = [
        "CompareTo"; // Members defined on System.Char
        "GetHashCode"] // Members defined on System.Object

    static member intExpectedCompletions = [
        "CompareTo"; // Members defined on System.Int32
        "GetHashCode"] // Members defined on System.Object
    
    static member stringExpectedCompletions = [
        "Substring"; // Methods of System.String
        "GetHashCode"] // Methods of System.Object

    static member arrayExpectedCompletions = [
        "Length"; ] // Methods of System.Object

    member private this.AutoCompleteBug70080HelperHelper(programText:string, shouldContain, shouldNotContain) =
        let i = programText.IndexOf("[<Attr ")
        if i = -1 then failwith "Could not find expected '[<Attr ' in program"
        AssertCtrlSpaceCompleteContains
          [ programText ]
          "[<Attr"       // marker
          shouldContain
          shouldNotContain
        let s = programText.Insert(i+2, "type:")
        AssertCtrlSpaceCompleteContains 
          [ s ]
          "[<type:Attr"       // marker
          shouldContain // should contain
          shouldNotContain
        let s = programText.Insert(i+2, "module:")
        AssertCtrlSpaceCompleteContains 
          [ s ]
          "[<module:Attr"       // marker
          shouldContain // should contain
          shouldNotContain

    member public this.AutoCompleteBug70080Helper(programText: string) =
        this.AutoCompleteBug70080HelperHelper(programText, ["AttributeUsage"], [])

    member private this.testAutoCompleteAdjacentToDot op =
        let text = sprintf "System.Console%s" op
        // First, test that pressing dot works.
        AssertAutoCompleteContainsNoCoffeeBreak 
          [ text ]
          "System.Console."
          [ "BackgroundColor" ] // should contain
          [ ] // should not contain   

    //**Help Function for checking Ctrl-Space Completion Contains the expected value *************
    member private this.AssertCtrlSpaceCompletionContains(fileContents : list<string>, marker, expected, ?addtlRefAssy: list<string>)  = 
        this.AssertCtrlSpaceCompletion(
            fileContents,
            marker,
            (fun completions -> 
                Assert.AreNotEqual(0,completions.Length)
                let found = completions |> Array.exists(fun (CompletionItem(s,_,_,_,_)) -> s = expected)
                if not(found) then 
                    printfn "Expected: %A to contain %s" completions expected  
                    Assert.Fail()                
            ),
            ?addtlRefAssy = addtlRefAssy
        )

   //**Help Function for checking Ctrl-Space Completion Contains the expected value *************
    member private this.AssertCtrlSpaceCompletion(fileContents : list<string>, marker, checkCompletion: (CompletionItem array -> unit), ?addtlRefAssy: list<string>)  = 
        let (_, _, file) = this.CreateSingleFileProject(fileContents, ?references = addtlRefAssy)
        MoveCursorToEndOfMarker(file,marker)
        let completions = CtrlSpaceCompleteAtCursor file
        checkCompletion completions

    member private this.AutoCompletionListNotEmpty (fileContents : list<string>) marker  = 
        let (_, _, file) = this.CreateSingleFileProject(fileContents)
        MoveCursorToEndOfMarker(file,marker)
        let completions = AutoCompleteAtCursor file
        Assert.AreNotEqual(0,completions.Length)

    member public this.TestCompletionNotShowingWhenFastUpdate (firstSrc : list<string>) secondSrc marker =     
        let (_, _, file) = this.CreateSingleFileProject(firstSrc)
        MoveCursorToEndOfMarker(file,marker)

        // Now delete the property and leave only dot at the end 
        //  - user is typing fast so replac the content without background compilation
        ReplaceFileInMemoryWithoutCoffeeBreak file secondSrc      
        let completions = time1 AutoCompleteAtCursor file "Time of first autocomplete."
        AssertCompListIsEmpty(completions)

        // Recheck after some time - after the background compilation runs
        TakeCoffeeBreak(this.VS)                                      
        let completions = time1 AutoCompleteAtCursor file "Time of first autocomplete."
        AssertCompListIsEmpty(completions)      

   /////Helper Functios 
        //DotCompList ContainAll At End Of Marker Helper Function
    member private this.VerifyDotCompListContainAllAtEndOfMarker(fileContents : string, marker : string, list : string list) =
        let (solution, project, file) = this.CreateSingleFileProject(fileContents)
        let completions = DotCompletionAtEndOfMarker file marker
        AssertCompListContainsAll(completions, list)

        //DoesNotContainAny At Start Of Marker Helper Function 
    member private this.VerifyDotCompListDoesNotContainAnyAtStartOfMarker(fileContents : string, marker : string, list : string list, ?addtlRefAssy : list<string>) =
        let (solution, project, file) = this.CreateSingleFileProject(fileContents, ?references = addtlRefAssy)

        let completions = DotCompletionAtStartOfMarker file marker
        AssertCompListDoesNotContainAny(completions, list)

        //DotCompList Is Empty At Start Of Marker Helper Function
    member private this.VerifyDotCompListIsEmptyAtStartOfMarker(fileContents : string, marker : string, ?addtlRefAssy : list<string>) =
        let (solution, project, file) = this.CreateSingleFileProject(fileContents, ?references = addtlRefAssy)

        let completions = DotCompletionAtStartOfMarker file marker
        AssertCompListIsEmpty(completions)

    [<Test>]
    member this.``AutoCompletion.ObjectMethods``() = 
        let code =
            [
                "type DU1 = DU_1"
                
                "[<NoEquality>]"
                "type DU2 = DU_2"

                "[<NoEquality>]"
                "type DU3 ="
                "   | DU_3"
                "   with member this.Equals(b : string) = 1"

                "[<NoEquality>]"
                "type DU4 ="
                "   | DU_4"
                "   with member this.GetHashCode(b : string) = 1"


                "module Extensions ="
                "    type System.Object with"
                "       member this.ExtensionPropObj = 42"
                "       member this.ExtensionMethodObj () = 42"
 
                "open Extensions"
            ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        let test tail marker expected notExpected =
            let code = code @ [tail]
            ReplaceFileInMemory file code
            MoveCursorToEndOfMarker(file,marker)

            let completions = AutoCompleteAtCursor file
            AssertCompListContainsAll(completions, expected)
            AssertCompListDoesNotContainAny(completions, notExpected)

        test "obj()." ")." ["Equals"; "ExtensionPropObj"; "ExtensionMethodObj"] []
        test  "System.Object." "Object." ["Equals"; "ReferenceEquals"] []
        test "System.String." "String." ["Equals"] []
        test "DU_1." "DU_1." ["Equals"; "GetHashCode"; "ExtensionMethodObj"; "ExtensionPropObj"] []
        test "DU_2." "DU_2." ["ExtensionPropObj"; "ExtensionMethodObj"] ["Equals"; "GetHashCode"] // no equals\gethashcode
        test "DU_3." "DU_3." ["ExtensionPropObj"; "ExtensionMethodObj"; "Equals"] ["GetHashCode"] // no gethashcode, has equals defined in DU3 type
        test "DU_4." "DU_4." ["ExtensionPropObj"; "ExtensionMethodObj"; "GetHashCode"] ["Equals"] // no equals, has gethashcode defined in DU4 type

    [<Test>]
    member this.``AutoCompletion.escaped with backticks`` () =
        let code = """
type MyRec = {
  ``field.field``  : string
}

let a = {``field.field`` = ""}
a.
        """
        this.AssertCtrlSpaceCompletion(
          [code]
          , "a."
          , (fun completions ->
              completions 
              |> Seq.tryFind (fun (CompletionItem(_,name,nameInCode,_,_)) ->
                name = "field.field" 
                && nameInCode = "``field.field``"
              )
              |> function
                  | Some _ -> ()
                  | None -> Assert.Fail "expected ``field.field`` to be present"

          ))
        
    [<Test>]
    member this.``AutoCompletion.BeforeThis``() = 
        let code = 
            [
                [
                "type A() ="
                "   member _.X = ()"
                "   member this."
                ]
                [
                "type A() ="
                "   member _.X = ()"
                "   member private this."
                ]
                [
                "type A() ="
                "   member _.X = ()"
                "   member public this."
                ]
                [
                "type A() ="
                "   member _.X = ()"
                "   member internal this."
                ]

            ]

        for c in code do
            AssertCtrlSpaceCompletionListIsEmpty c "this."
            AssertAutoCompleteCompletionListIsEmpty c "this."
            AssertCtrlSpaceCompletionListIsEmptyNoCoffeeBreak c "this."
            AssertAutoCompleteCompletionListIsEmptyNoCoffeeBreak c "this."
                    
    [<Test>]
    [<Category("TypeProvider")>]
    member this.``TypeProvider.VisibilityChecksForGeneratedTypes``() = 
        let extraRefs = [PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll")]
        let check = DoWithAutoCompleteUsingExtraRefs extraRefs None true SourceFileKind.FS BackgroundRequestReason.MemberSelect

        let code = 
            [
                "type T = GeneratedType.SampleType"

                "let t = T(5)"
                "t."

                "T."


                "type T1() = "
                "    inherit T(5)"
                "    member this.Foo() = this."
            ]
        check code "T." <| 
            fun ci -> 
                AssertCompListContains(ci, "PublicField")

        check code "t." <|
            fun ci ->
                AssertCompListContainsAll(ci, ["PublicM"; "PublicProp"])
                AssertCompListDoesNotContainAny(ci, ["f"; "ProtectedProp"; "PrivateProp"; "ProtectedM"; "PrivateM"])

        check code "= this." <|
            fun ci ->
                AssertCompListContainsAll(ci, ["PublicM"; "PublicProp"])
                // The F# compiler never even asks to see protected/private provided members
                AssertCompListDoesNotContainAny(ci, ["f"; "ProtectedProp"; "ProtectedM"; "PrivateProp"; "PrivateM"])
                
               
    [<Test>] member public this.``AdjacentToDot_01``() = testAutoCompleteAdjacentToDot ".."
    [<Test>] member public this.``AdjacentToDot_02``() = testAutoCompleteAdjacentToDot ".<"
    [<Test>] member public this.``AdjacentToDot_03``() = testAutoCompleteAdjacentToDot ".>"
    [<Test>] member public this.``AdjacentToDot_04``() = testAutoCompleteAdjacentToDot ".="
    [<Test>] member public this.``AdjacentToDot_05``() = testAutoCompleteAdjacentToDot ".!="
    [<Test>] member public this.``AdjacentToDot_06``() = testAutoCompleteAdjacentToDot ".$"
    [<Test>] member public this.``AdjacentToDot_07``() = testAutoCompleteAdjacentToDot ".[]"
    [<Test>] member public this.``AdjacentToDot_08``() = testAutoCompleteAdjacentToDot ".[]<-"
    [<Test>] member public this.``AdjacentToDot_09``() = testAutoCompleteAdjacentToDot ".[,]<-"
    [<Test>] member public this.``AdjacentToDot_10``() = testAutoCompleteAdjacentToDot ".[,,]<-"
    [<Test>] member public this.``AdjacentToDot_11``() = testAutoCompleteAdjacentToDot ".[,,,]<-"
    [<Test>] member public this.``AdjacentToDot_12``() = testAutoCompleteAdjacentToDot ".[,,,]"
    [<Test>] member public this.``AdjacentToDot_13``() = testAutoCompleteAdjacentToDot ".[,,]"
    [<Test>] member public this.``AdjacentToDot_14``() = testAutoCompleteAdjacentToDot ".[,]"
    [<Test>] member public this.``AdjacentToDot_15``() = testAutoCompleteAdjacentToDot ".[..]"
    [<Test>] member public this.``AdjacentToDot_16``() = testAutoCompleteAdjacentToDot ".[..,..]"
    [<Test>] member public this.``AdjacentToDot_17``() = testAutoCompleteAdjacentToDot ".[..,..,..]"
    [<Test>] member public this.``AdjacentToDot_18``() = testAutoCompleteAdjacentToDot ".[..,..,..,..]"
    [<Test>] member public this.``AdjacentToDot_19``() = testAutoCompleteAdjacentToDot ".()"
    [<Test>] member public this.``AdjacentToDot_20``() = testAutoCompleteAdjacentToDot ".()<-"
    [<Test>] member public this.``AdjacentToDot_02_Negative``() = testAutoCompleteAdjacentToDotNegative ".<"
    [<Test>] member public this.``AdjacentToDot_03_Negative``() = testAutoCompleteAdjacentToDotNegative ".>"
    [<Test>] member public this.``AdjacentToDot_04_Negative``() = testAutoCompleteAdjacentToDotNegative ".="
    [<Test>] member public this.``AdjacentToDot_05_Negative``() = testAutoCompleteAdjacentToDotNegative ".!="
    [<Test>] member public this.``AdjacentToDot_06_Negative``() = testAutoCompleteAdjacentToDotNegative ".$"
    [<Test>] member public this.``AdjacentToDot_07_Negative``() = testAutoCompleteAdjacentToDotNegative ".[]"
    [<Test>] member public this.``AdjacentToDot_08_Negative``() = testAutoCompleteAdjacentToDotNegative ".[]<-"
    [<Test>] member public this.``AdjacentToDot_09_Negative``() = testAutoCompleteAdjacentToDotNegative ".[,]<-"
    [<Test>] member public this.``AdjacentToDot_10_Negative``() = testAutoCompleteAdjacentToDotNegative ".[,,]<-"
    [<Test>] member public this.``AdjacentToDot_11_Negative``() = testAutoCompleteAdjacentToDotNegative ".[,,,]<-"
    [<Test>] member public this.``AdjacentToDot_12_Negative``() = testAutoCompleteAdjacentToDotNegative ".[,,,]"
    [<Test>] member public this.``AdjacentToDot_13_Negative``() = testAutoCompleteAdjacentToDotNegative ".[,,]"
    [<Test>] member public this.``AdjacentToDot_14_Negative``() = testAutoCompleteAdjacentToDotNegative ".[,]"
    [<Test>] member public this.``AdjacentToDot_15_Negative``() = testAutoCompleteAdjacentToDotNegative ".[..]"
    [<Test>] member public this.``AdjacentToDot_16_Negative``() = testAutoCompleteAdjacentToDotNegative ".[..,..]"
    [<Test>] member public this.``AdjacentToDot_17_Negative``() = testAutoCompleteAdjacentToDotNegative ".[..,..,..]"
    [<Test>] member public this.``AdjacentToDot_18_Negative``() = testAutoCompleteAdjacentToDotNegative ".[..,..,..,..]"
    [<Test>] member public this.``AdjacentToDot_19_Negative``() = testAutoCompleteAdjacentToDotNegative ".()"
    [<Test>] member public this.``AdjacentToDot_20_Negative``() = testAutoCompleteAdjacentToDotNegative ".()<-"
    [<Test>] member public this.``AdjacentToDot_21_Negative``() = testAutoCompleteAdjacentToDotNegative ".+."

    [<Test>]
    member public this.``LambdaOverloads.Completion``() = 
        let prologue = "open System.Linq"
        let cases = 
            [
                "[\"\"].Sum(fun x -> (*$*)x.Len )"
                "[\"\"].Select(fun x -> (*$*)x.Len )"
                "[\"\"].Select(fun x i -> (*$*)x.Len )"
                "[\"\"].GroupBy(fun x -> (*$*)x.Len )"
                "[\"\"].Join([\"\"], (fun x -> (*$*)x.Len), (fun x -> x.Len), (fun x y -> x.Len+ y.Len))"
                "[\"\"].Join([\"\"], (fun x -> x.Len), (fun x -> (*$*)x.Len), (fun x y -> x.Len+ y.Len))"
                "[\"\"].Join([\"\"], (fun x -> x.Len), (fun x -> x.Len), (fun x y -> (*$*)x.Len + y.Len))"
                "[\"\"].Join([\"\"], (fun x -> x.Len), (fun x -> x.Len), (fun y x -> y.Len + (*$*)x.Len))"
                "[\"\"].Where(fun x -> (*$*)x.Len )"
                "[\"\"].Where(fun x -> (*$*)x.Len % 3 )"
                "[\"\"].Where(fun x -> (*$*)x.Len % 3 = 0)"
                "[\"\"].AsQueryable().Select(fun x -> (*$*)x.Len )"
                "[\"\"].AsQueryable().Select(fun x i -> (*$*)x.Len )"
                "[\"\"].AsQueryable().Where(fun x -> (*$*)x.Len )"
            ]
            
        for case in cases do
            let code = [prologue; case]
            AssertCtrlSpaceCompleteContains code "(*$*)x.Len" ["Length"] []

    [<Test>]
    member public this.``Query.CompletionInJoinOn``() = 
        let code = 
            [
                "query {"
                "   for a in [1] do"
                "   join b in [2] on (a.)"
                "   select (a + b)"
                "}"
            ]
        AssertCtrlSpaceCompleteContains code "(a." ["GetHashCode"; "CompareTo"] []



    [<Test>]
    member public this.``TupledArgsInLambda.Completion.Bug312557_1``() = 
        let code = 
            [
                "[(1,2);(1,2);(1,2)]"
                "|> Seq.iter (fun (xxx,yyy) -> printfn \"%d\" (*MARKER*)"
                "                              printfn \"%d\" 1)"
            ]
        AssertCtrlSpaceCompleteContains code "(*MARKER*)" ["xxx"; "yyy"] []

    [<Test>]
    member public this.``TupledArgsInLambda.Completion.Bug312557_2``() = 
        let code = 
            [
                "(1,2) |> (fun (aaa,bbb) ->"
                "    printfn \"hi\""
                "    printfn \"%d%d\" b a"
                "    printfn \"%d%d\" a b   ) "
            ]
        AssertCtrlSpaceCompleteContains code "\" b" ["aaa"; "bbb"] []
        AssertCtrlSpaceCompleteContains code "\" a" ["aaa"; "bbb"] []
        AssertCtrlSpaceCompleteContains code "b a" ["aaa"; "bbb"] []
        AssertCtrlSpaceCompleteContains code "a b" ["aaa"; "bbb"] []

    [<Test>]
    member this.``AutoCompletion.OnTypeConstraintError``() =
        let code =
            [
                "type Foo = Foo"
                "    with"
                "        member _.Bar = 1"
                "        member _.PublicMethodForIntellisense() = 2"
                "        member internal _.InternalMethod() = 3"
                "        member private _.PrivateProperty = 4"
                ""
                "let u: Unit ="
                "    [ Foo ]"
                "    |> List.map (fun abcd -> abcd.)"
            ]
        AssertCtrlSpaceCompleteContains code "abcd." ["Bar"; "Equals"; "GetHashCode"; "GetType"; "InternalMethod"; "PublicMethodForIntellisense"; "ToString"] []

    [<Test>]
    [<Category("RangeOperator")>]
    member public this.``RangeOperator.IncorrectUsage``() = 
        AssertCtrlSpaceCompletionListIsEmpty [".."] ".."
        AssertCtrlSpaceCompletionListIsEmpty ["..."] "..."
    
    [<Test>]
    [<Category("Completion in Inherit")>]
    member public this.``Inherit.CompletionInConstructorArguments1``() = 
        let code = 
            [
                "type A(a : int) = class end"
                "type B() = inherit A(a)"
            ]
        AssertCtrlSpaceCompleteContains code "inherit A(a" ["abs"] []

    [<Test>]
    [<Category("Completion in Inherit")>]
    member public this.``Inherit.CompletionInConstructorArguments2``() = 
        let code = 
            [
                "type A(a : int) = class end"
                "type B() = inherit A(System.String.)"
            ]
        AssertCtrlSpaceCompleteContains code "System.String." ["Empty"] ["Array"; "Collections"]
    
    [<Test>]
    [<Category("Completion in object initializer")>]
    member public this.``ObjectInitializer.CompletionForProperties``() =
        let typeDef1 = 
            [
                "type A() = "
                "   member val SettableProperty = 1 with get,set"
                "   member val AnotherSettableProperty = 1 with get,set"
                "   member val NonSettableProperty = 1"
            ]
        AssertCtrlSpaceCompleteContains (typeDef1 @ ["A((**))"]) "A((**)" ["SettableProperty"; "AnotherSettableProperty"] ["NonSettableProperty"] 
        AssertCtrlSpaceCompleteContains (typeDef1 @ ["A(S = 1)"]) "A(S" ["SettableProperty"] ["NonSettableProperty"] 
        AssertCtrlSpaceCompleteContains (typeDef1 @ ["A(S = 1)"]) "A(S = 1" [] ["SettableProperty"; "NonSettableProperty"] // neg test
        AssertCtrlSpaceCompleteContains (typeDef1 @ ["A(S = 1,)"]) "A(S = 1," ["AnotherSettableProperty"] ["NonSettableProperty"] 
        AssertCtrlSpaceCompleteContains (typeDef1 @ ["new A((**))"]) "A((**)" ["SettableProperty"; "AnotherSettableProperty"] ["NonSettableProperty"] 
        AssertCtrlSpaceCompleteContains (typeDef1 @ ["new A(S = 1)"]) "A(S" ["SettableProperty"] ["NonSettableProperty"] 
        AssertCtrlSpaceCompleteContains (typeDef1 @ ["new A(S = 1)"]) "A(S = 1" [] ["SettableProperty"; "NonSettableProperty"] // neg test
        AssertCtrlSpaceCompleteContains (typeDef1 @ ["new A(S = 1,)"]) "A(S = 1," ["AnotherSettableProperty"] ["NonSettableProperty"] 

        let typeDef2 = 
            [
                "type A<'a>() = "
                "   member val SettableProperty = 1 with get,set"
                "   member val AnotherSettableProperty = 1 with get,set"
                "   member val NonSettableProperty = 1"
            ]
        AssertCtrlSpaceCompleteContains (typeDef2 @ ["A((**))"]) "A((**)" ["SettableProperty"; "AnotherSettableProperty"] ["NonSettableProperty"] 
        AssertCtrlSpaceCompleteContains (typeDef2 @ ["A(S = 1)"]) "A(S" ["SettableProperty"] ["NonSettableProperty"] 
        AssertCtrlSpaceCompleteContains (typeDef2 @ ["A(S = 1)"]) "A(S = 1" [] ["SettableProperty"; "NonSettableProperty"] // neg test
        AssertCtrlSpaceCompleteContains (typeDef2 @ ["A(S = 1,)"]) "A(S = 1," ["AnotherSettableProperty"] ["NonSettableProperty"] 
        AssertCtrlSpaceCompleteContains (typeDef2 @ ["new A<_>((**))"]) "A<_>((**)" ["SettableProperty"; "AnotherSettableProperty"] ["NonSettableProperty"] 
        AssertCtrlSpaceCompleteContains (typeDef2 @ ["new A<_>(S = 1)"]) "A<_>(S" ["SettableProperty"] ["NonSettableProperty"] 
        AssertCtrlSpaceCompleteContains (typeDef2 @ ["new A<_>(S = 1)"]) "A<_>(S = 1" [] ["SettableProperty"; "NonSettableProperty"] // neg test
        AssertCtrlSpaceCompleteContains (typeDef2 @ ["new A<_>(S = 1,)"]) "A<_>(S = 1," ["AnotherSettableProperty"] ["NonSettableProperty"] 

        let typeDef3 = 
            [
                "module M ="
                "   type A() = "
                "       member val SettableProperty = 1 with get,set"
                "       member val AnotherSettableProperty = 1 with get,set"
                "       member val NonSettableProperty = 1"
            ]
        AssertCtrlSpaceCompleteContains (typeDef3 @ ["M.A((**))"]) "A((**)" ["SettableProperty"; "AnotherSettableProperty"] ["NonSettableProperty"] 
        AssertCtrlSpaceCompleteContains (typeDef3 @ ["M.A(S = 1)"]) "A(S" ["SettableProperty"] ["NonSettableProperty"] 
        AssertCtrlSpaceCompleteContains (typeDef3 @ ["M.A(S = 1)"]) "A(S = 1" [] ["NonSettableProperty"; "SettableProperty"] // neg test 
        AssertCtrlSpaceCompleteContains (typeDef3 @ ["M.A(S = 1,)"]) "A(S = 1," ["AnotherSettableProperty"] ["NonSettableProperty"] 
        AssertCtrlSpaceCompleteContains (typeDef3 @ ["new M.A((**))"]) "A((**)" ["SettableProperty"; "AnotherSettableProperty"] ["NonSettableProperty"] 
        AssertCtrlSpaceCompleteContains (typeDef3 @ ["new M.A(S = 1)"]) "A(S" ["SettableProperty"] ["NonSettableProperty"] 
        AssertCtrlSpaceCompleteContains (typeDef3 @ ["new M.A(S = 1)"]) "A(S = 1" [] ["NonSettableProperty"; "SettableProperty"] // neg test 

        let typeDef4 = 
            [
                "module M ="
                "   type A<'a, 'b>() = "
                "       member val SettableProperty = 1 with get,set"
                "       member val AnotherSettableProperty = 1 with get,set"
                "       member val NonSettableProperty = 1"
            ]
        AssertCtrlSpaceCompleteContains (typeDef4 @ ["M.A((**))"]) "A((**)" ["SettableProperty"; "AnotherSettableProperty"] ["NonSettableProperty"] 
        AssertCtrlSpaceCompleteContains (typeDef4 @ ["M.A(S = 1)"]) "A(S" ["SettableProperty"] ["NonSettableProperty"] 
        AssertCtrlSpaceCompleteContains (typeDef4 @ ["M.A(S = 1)"]) "A(S = 1" [] ["SettableProperty"; "NonSettableProperty"] // neg test
        AssertCtrlSpaceCompleteContains (typeDef4 @ ["M.A(S = 1,)"]) "A(S = 1," ["AnotherSettableProperty"] ["NonSettableProperty"] 
        AssertCtrlSpaceCompleteContains (typeDef4 @ ["new M.A<_, _>((**))"]) "A<_, _>((**)" ["SettableProperty"; "AnotherSettableProperty"] ["NonSettableProperty"] 
        AssertCtrlSpaceCompleteContains (typeDef4 @ ["new M.A<_, _>(S = 1)"]) "A<_, _>(S" ["SettableProperty"] ["NonSettableProperty"] 
        AssertCtrlSpaceCompleteContains (typeDef4 @ ["new M.A<_, _>(S = 1)"]) "A<_, _>(S = 1" [] ["NonSettableProperty"; "SettableProperty"] 
        AssertCtrlSpaceCompleteContains (typeDef4 @ ["new M.A<_, _>(S = 1,)"]) "A<_, _>(S = 1," ["AnotherSettableProperty"] ["NonSettableProperty"] 

    [<Test>]
    [<Category("Completion in object initializer")>]
    member public this.``ObjectInitializer.CompletionForSettableExtensionProperties``() =
        let typeDef = 
            [
                "type A() = member this.SetXYZ(v: int) = ()"
                "module Ext = type A with member this.XYZ with set(v) = this.SetXYZ(v)"

            ]

        AssertCtrlSpaceCompleteContains (typeDef @ ["open Ext"; "A((**))"]) "A((**)" ["XYZ"] [] // positive
        AssertCtrlSpaceCompleteContains (typeDef @ ["A((**))"]) "A((**)" [] ["XYZ"] // negative

    [<Test>]
    [<Category("Completion in object initializer")>]
    member public this.``ObjectInitializer.CompletionForNamedParameters``() =
        let typeDef1 = 
            [
                "type A = "
                "   static member Run(xyz: int, zyx: string) = 1"
            ]
        AssertCtrlSpaceCompleteContains (typeDef1 @ ["A.Run()"]) ".Run(" ["xyz"; "zyx"] [] 
        AssertCtrlSpaceCompleteContains (typeDef1 @ ["A.Run(x = 1)"]) ".Run(x" ["xyz"] [] 
        AssertCtrlSpaceCompleteContains (typeDef1 @ ["A.Run(x = 1,)"]) ".Run(x = 1," ["xyz"; "zyx"] [] 

        let typeDef2 = 
            [
                "type A = "
                "   static member Run<'T>(xyz: 'T, zyx: string) = 1"
            ]
        AssertCtrlSpaceCompleteContains (typeDef2 @ ["A.Run()"]) ".Run(" ["xyz"; "zyx"] [] 
        AssertCtrlSpaceCompleteContains (typeDef2 @ ["A.Run(x = 1)"]) ".Run(x" ["xyz"] [] 
        AssertCtrlSpaceCompleteContains (typeDef2 @ ["A.Run(x = 1,)"]) ".Run(x = 1," ["xyz"; "zyx"] [] 
        AssertCtrlSpaceCompleteContains (typeDef2 @ ["A.Run<_>()"]) ".Run<_>(" ["xyz"; "zyx"] [] 
        AssertCtrlSpaceCompleteContains (typeDef2 @ ["A.Run<_>(x = 1)"]) ".Run<_>(x" ["xyz"] [] 
        AssertCtrlSpaceCompleteContains (typeDef2 @ ["A.Run<_>(x = 1,)"]) ".Run<_>(x = 1," ["xyz"; "zyx"] [] 
    
    [<Test>]
    [<Category("Completion in object initializer")>]
    member public this.``ObjectInitializer.CompletionForSettablePropertiesInReturnValue``() =
        let typeDef1 = 
            [
                "type A0() = member val Settable0 = 1 with get,set"
                "type A() = "
                "   member val Settable = 1 with get,set"
                "   member val NonSettable = 1"
                "   static member Run(): A0 =  Unchecked.defaultof<_>"
                "   static member Run(a: string): A =  Unchecked.defaultof<_>"
            ]
        AssertCtrlSpaceCompleteContains (typeDef1 @ ["A.Run()"]) ".Run(" ["Settable"; "Settable0"] ["NonSettable"] 
        AssertCtrlSpaceCompleteContains (typeDef1 @ ["A.Run(S = 1)"]) ".Run(S" ["Settable"; "Settable0"] ["NonSettable"] 
        AssertCtrlSpaceCompleteContains (typeDef1 @ ["A.Run(S = 1,)"]) ".Run(S = 1," ["Settable"; "Settable0"] ["NonSettable"]  
        AssertCtrlSpaceCompleteContains (typeDef1 @ ["A.Run(Settable = 1,)"]) ".Run(Settable = 1," ["Settable0"] ["NonSettable"]  

        let typeDef2 = 
            [
                "type A0() = member val Settable0 = 1 with get,set"
                "type A() = "
                "   member val Settable = 1 with get,set"
                "   member val NonSettable = 1"
                "   static member Run<'T>(): A0 = Unchecked.defaultof<_>"
                "   static member Run(a: int): A = Unchecked.defaultof<_>"
            ]
        AssertCtrlSpaceCompleteContains (typeDef2 @ ["A.Run()"]) ".Run(" ["Settable"; "Settable0"] ["NonSettable"] 
        AssertCtrlSpaceCompleteContains (typeDef2 @ ["A.Run(S = 1)"]) ".Run(S" ["Settable"; "Settable0"] ["NonSettable"] 
        AssertCtrlSpaceCompleteContains (typeDef2 @ ["A.Run(S = 1,)"]) ".Run(S = 1," ["Settable"; "Settable0"] ["NonSettable"] 
        AssertCtrlSpaceCompleteContains (typeDef2 @ ["A.Run(Settable = 1,)"]) ".Run(Settable = 1," ["Settable0"] ["NonSettable"] 
        AssertCtrlSpaceCompleteContains (typeDef2 @ ["A.Run<_>()"]) ".Run<_>(" ["Settable"; "Settable0"] ["NonSettable"] 
        AssertCtrlSpaceCompleteContains (typeDef2 @ ["A.Run<_>(S = 1)"]) ".Run<_>(S" ["Settable"; "Settable0"] ["NonSettable"] 
        AssertCtrlSpaceCompleteContains (typeDef2 @ ["A.Run<_>(S = 1,)"]) ".Run<_>(S = 1," ["Settable"; "Settable0"] ["NonSettable"] 
        AssertCtrlSpaceCompleteContains (typeDef2 @ ["A.Run<_>(Settable = 1,)"]) ".Run<_>(Settable = 1," ["Settable0"] ["NonSettable"] 


    [<Test>]
    [<Category("RangeOperator")>]
    member public this.``RangeOperator.CorrectUsage``() = 
        let useCases = 
            [
                [
                    "let _ = [1..]"
                ], "1.."
                [
                    "["
                    "   1"
                    "    .."
                    "]"
                ], ".."
            ]
        for (code, marker) in useCases do
            printfn "%A"  code
            AssertCtrlSpaceCompleteContains code marker ["abs"] []
            printfn "ok"
        


    [<Test>]
    member public this.``Array.Length.InForRange``() =
        AssertAutoCompleteContainsNoCoffeeBreak 
          [ "let a = [|1;2;3|]
for i in 0..a."]
          "0..a."
          [ "Length" ] // should contain
          [ ] // should not contain   
    
    [<Test>]
    member public this.``ProtectedMembers.BaseClass`` () = 
        let sourceCode = 
            [
                "type T() = "
                "   inherit exn()"
                "   member this.Run(x : exn) = x."
            ]
        AssertCtrlSpaceCompleteContains sourceCode "x." ["Message"; "HResult"] []

    [<Test>]
    member public this.``ProtectedMembers.SelfOrDerivedClass`` () = 
        let sources = 
            [
                [
                    "type T() = "
                    "   inherit exn()"
                    "   member this.Run(x : T) = x."
                ]
                [
                    "type T() = "
                    "   inherit exn()"
                    "   member this.Run(x : Z) = x."
                    "and Z() ="
                    "   inherit T()"
                ]
            ]
        for src in sources do
            AssertCtrlSpaceCompleteContains src "x." ["Message"; "HResult"] []

    
    [<Test>]
    [<Ignore("Should be enabled after fixing 279738")>]
    [<Category("Records")>]
    member public this.``Records.DotCompletion.ConstructingRecords1``() = 
        let prologue = "type OuterRec = {XX : int; YY : string}"
        
        let useCases = 
            [
                "let _ =  (* MARKER*) {X", "(* MARKER*) {X", ["XX"]
                "let _ = {XX = 1; (* MARKER*)O", "(* MARKER*)O", ["OuterRec"]
                "let _ = {XX = 1; (* MARKER*)OuterRec.", "(* MARKER*)OuterRec.", ["XX"; "YY"]
            ]
        ()
        for (code, marker, should) in useCases do
            let code = [prologue; code]
            AssertCtrlSpaceCompleteContains code marker should ["abs"]
    
    [<Test>]
    [<Category("Records")>]
    member public this.``Records.DotCompletion.ConstructingRecords2``() = 
        let prologue = 
            [
                "module Mod = "
                "   type Rec = {XX : int; YY : string}"
            ]
        let useCases = 
            [
                "let _ = (* MARKER*){X", "(* MARKER*){X", [], ["XX"]
                "let _ = {(* MARKER*)Mod. = 1; O", "(* MARKER*)Mod.", ["XX"; "YY"], ["System"]
                "let _ = {(* MARKER*)Mod.Rec. ", "(* MARKER*)Mod.Rec.", ["XX"; "YY"], ["System"]
            ]

        for (code, marker, should, shouldnot) in useCases do
            let code = prologue @ [code]
            let shouldnot = shouldnot @ ["abs"]
            AssertCtrlSpaceCompleteContains code marker should ["abs"]
    
    [<Test>]
    [<Category("Records")>]
    member public this.``Records.CopyOnUpdate``() =
        let prologue = 
            [
                "module SomeOtherPath ="
                "   type r = { a: int; b : int }"
            ]

        let useCases = 
            [
               "let f1 x = { x with SomeOtherPath. = 3 }", "SomeOtherPath."
               "let f2 x = { x with SomeOtherPath.r. = 3 }", "SomeOtherPath.r."
               "let f3 (x : SomeOtherPath.r) = { x with }", "x with "
            ]
        for (code, marker) in useCases do
            let code = prologue @ [code]
            AssertCtrlSpaceCompleteContains code marker ["a"; "b"] ["abs"]
    
    [<Test>]
    [<Category("Records")>]
    member public this.``Records.CopyOnUpdate.NoFieldsCompletionBeforeWith``() =
        let code = 
            [
                "type T = {AAA : int}"
                "let r = {AAA = 5}"
                "let b = {r  with }"
            ]
        AssertCtrlSpaceCompleteContains code "{r " [] ["AAA"]

    [<Test>]
    [<Category("Records")>]
    member public this.``Records.Constructors1``() =
        let prologue = 
            [
                "type X ="
                "   val field1 : int"
                "   val field2 : string"
            ]

        let useCases = 
            [
               "    new() = { f}", "{ f"
               "    new() = { field1; }", "field1; "
               "    new() = { field1 = 5; }", "= 5; "
               "    new() = { field1 = 5; f }", "5; f"
            ]
        for (code, marker) in useCases do
            let code = prologue @ [code]
            AssertCtrlSpaceCompleteContains code marker ["field1"; "field2"] ["abs"]
    
    [<Test>]
    [<Category("Records")>]
    member public this.``Records.Constructors2.UnderscoresInNames``() =
        let prologue = 
            [
                "type X ="
                "   val _field1 : int"
                "   val _field2 : string"
            ]

        let useCases = 
            [
               "    new() = { _}", "{ _"
               "    new() = { _field1; }", "_field1; "
            ]
        for (code, marker) in useCases do
            let code = prologue @ [code]
            AssertCtrlSpaceCompleteContains code marker ["_field1"; "_field2"] ["abs"]  

    
    [<Test>]
    [<Category("Records")>]
    member public this.``Records.NestedRecordPatterns``() =
        let code = ["[1..({contents = 5}).]"]
        AssertCtrlSpaceCompleteContains code "5})." ["Value"; "contents"] ["CompareTo"]  

    [<Test>]
    [<Category("Records")>]
    member public this.``Records.Separators1``() =
        let useCases = 
            [
                [
                    "type X = { AAA : int; BBB : string}"
                    "let r = {AAA = 5 ; }"
                ], "AAA = 5 "
                [
                    "type X = { AAA : int; BBB : string}"
                    "let r = {AAA = 5 ; }"
                    "let b = {r with AAA = 5 ; }"
                ], "with AAA = 5 "
            ]        
        
        for (code, marker) in useCases do
            printfn "checking separators"
            printfn "%A" code
            AssertCtrlSpaceCompleteContains code marker ["abs"] ["AAA"; "BBB"]

    [<Test>]
    [<Category("Records")>]
    member public this.``Records.Separators2``() =
        let useCases = 
            [
                "Offside rule", [
                    "type X = { AAA : int; BBB : string}"
                    "let r ="
                    "       {"
                    "          AAA = 5"
                    "(*MARKER*)     "
                    "       }"
                ], "(*MARKER*)", ["AAA"; "BBB"]

                "Semicolumn", [
                    "type X = { AAA : int; BBB : string}"
                    "let r ="
                    "       {"
                    "          AAA = 5;"
                    "(*MARKER*)       "
                    "       }"
                ], "(*MARKER*)   ", ["AAA"; "BBB"]
                "Semicolumn2", [
                    "type X = { AAA : int; BBB : string; CCC : int}"
                    "let r ="
                    "       {"
                    "          AAA = 5; (*M*)"
                    "          CCC = 5" 
                    "       }"
                ], "(*M*)", ["AAA"; "BBB"; "CCC"]
            ]        
        
        for (caption, code, marker, should) in useCases do
            printfn "%s" caption
            printfn "%A" code
            AssertCtrlSpaceCompleteContains code marker should ["abs"]
    
    [<Test>]
    [<Category("Records")>]
    member public this.``Records.Inherits``() = 
        let prologue = 
            [
                "type A = class end"
                "type B = "
                "   inherit A"
                "   val f1 : int"
                "   val f2 : int"
            ]
        
        let useCases = 
            [
                ["   new() = { inherit A(); }"], "inherit A(); ", ["f1"; "f2"]
                [
                "   new() = { inherit A()"
                "        (*M*)"
                "           }"], "(*M*)", ["f1"; "f2"]
            ]
        for (code, marker, should) in useCases do
            let code = prologue @ code
            printfn "running:"
            printfn "%s" (String.concat "\r\n" code)
            AssertCtrlSpaceCompleteContains code marker should ["abs"]

    [<Test>]
    [<Category("Records")>]
    member public this.``Records.MissingBindings``() = 
        let prologue = 
            [
                "type R = {AAA : int; BBB : bool}"
            ]
        let useCases =
            [
                ["let _ = {A = 1; _;  }"], "; _;", ["R"]  // ["AAA"; "BBB"] <- this check should be used after fixing 279738
                ["let _ = {A = 1; _=; }"], " _=;", ["R"] // ["AAA"; "BBB"] <- this check should be used after fixing 279738
                ["let _ = {A = 1; R. }"], "1; R.", ["AAA"; "BBB"]
                ["let _ = {A = 1; _; R. }"], "_; R.", ["AAA"; "BBB"]
            ]

        for (code, marker, should) in useCases do
            let code = prologue @ code
            printfn "running:"
            printfn "%s" (String.concat "\r\n" code)
            AssertCtrlSpaceCompleteContains code marker should ["abs"]

    [<Test>]
    [<Category("Records")>]
    member public this.``Records.WRONG.MissingBindings``() = 
        // this test should be removed after fixing 279738
        let prologue = 
            [
                "type R = {AAA : int; BBB : bool}"
            ]
        let useCases =
            [
                ["let _ = {A = 1; _;  }"], "; _;", ["AAA"; "BBB"]
                ["let _ = {A = 1; _=; }"], " _=;", ["AAA"; "BBB"]
            ]

        for (code, marker, shouldNot) in useCases do
            let code = prologue @ code
            printfn "running:"
            printfn "%s" (String.concat "\r\n" code)
            AssertCtrlSpaceCompleteContains code marker [] shouldNot



    [<Test>]
    [<Category("Records")>]
    member public this.``Records.WRONG.IncorrectNameResEnv``() = 
        // this test should be removed after fixing 279738
        let prologue = 
            [
                "type R = {AAA : int; BBB : bool; CCC : int}"
            ]
        let useCases =
            [
                ["let _ = {A}"], "_ = {A", ["AAA"; "BBB"; "CCC"]
                ["let _ = {AAA = 1; }"], "_ = {AAA = 1;", ["AAA"; "BBB"; "CCC"]
            ]

        for (code, marker, shouldNot) in useCases do
            let code = prologue @ code
            printfn "running:"
            printfn "%s" (String.concat "\r\n" code)
            AssertCtrlSpaceCompleteContains code marker [] shouldNot

    [<Test>]
    [<Category("Records")>]
    member public this.``Records.WRONG.ErrorsInFirstBinding``() =
        // errors in the first binding are critical now
        let prologue = 
            [
                "type X ="
                "   val field1 : int"
                "   val field2 : string"
            ]

        let useCases = 
            [
               "    new() = { field1 =; }", "=; "  
               "    new() = { field1 =; f}", "=; f"
            ]
        for (code, marker) in useCases do
            let code = prologue @ [code]
            AssertCtrlSpaceCompleteContains code marker [] ["field1"; "field2"]
            
    [<Test>]
    [<Category("Records")>]
    member public this.``Records.InferByFieldsInPriorMethodArguments``() =
        
        let prologue = 
            [
                "type T() ="
                "    new (left: float32, top: float32) = T()"
                "    new (left: float32, top: float32, width: float32, height: float32) = T()"
                ""
                "type Rect ="
                "    { Left: float32"
                "      Top: float32"
                "      Width: float32"
                "      Height: float32 }"
            ]

        let useCases = 
            [ 
              "let toT(original) = T(original.Left, (* MARKER*)original.)", "(* MARKER*)original.", ["Left"; "Top"; "Width"; "Height"]
              "let toT(original) = T(original.Left, original.Height, (* MARKER*)original.)", "(* MARKER*)original.", ["Left"; "Top"; "Width"; "Height"]
              "let toT(original) = T(original.Left, original.Height, original.Width, (* MARKER*)original.)", "(* MARKER*)original.", ["Left"; "Top"; "Width"; "Height"]
              "let toT(original) = T(original.Left, original.Height, (* MARKER*)original., original.Width)", "(* MARKER*)original.", ["Left"; "Top"; "Width"; "Height"]
            ]
            
        for (code, marker, should) in useCases do
            let code = prologue @ [code]
            AssertCtrlSpaceCompleteContains code marker should []

    [<Test>]
    member this.``Completion.DetectInterfaces``() = 
        let shouldBeInterface =
            [
                [
                    "type X = interface"
                    "    inherit (*M*)"
                ]
                [
                    "[<Interface>]"
                    "type X ="
                    "    inherit (*M*)"
                ]
                [
                    "[<Interface>]"
                    "type X = interface"
                    "    inherit (*M*)"
                ]
            ]
        for ifs in shouldBeInterface do
            AssertCtrlSpaceCompleteContains ifs "(*M*)" ["seq"] ["obj"]


    [<Test>]
    member this.``Completion.DetectClasses``() = 
    
        let shouldBeClass = 
            [
                [
                    "type X = class"
                    "    inherit (*M*)"
                ]
                [
                    "[<Class>]"
                    "type X ="
                    "    inherit (*M*)"
                ]
                [
                    "[<Class>]"
                    "type X = class"
                    "    inherit (*M*)"
                ]
                [
                    "[<AbstractClass>]"
                    "type X() = "
                    "    inherit (*M*)"
                ]
            ]
        for cls in shouldBeClass do
            AssertCtrlSpaceCompleteContains cls "(*M*)" ["obj"] ["seq"]

    [<Test>]
    member this.``Completion.DetectUnknownCompletionContext``() = 
        let content = 
            [
                "type X = "
                "    inherit (*M*)"
            ]

        AssertCtrlSpaceCompleteContains content "(*M*)" ["obj"; "seq"] ["abs"]

    [<Test>]
    member this.``Completion.DetectInvalidCompletionContext``() = 
        let shouldBeInvalid = 
            [
                [
                    "type X = struct"
                    "    inherit (*M*)"
                ]
                [
                    "[<Interface>]"
                    "type X = class"
                    "    inherit (*M*)"
                ]
                [
                    "[<Class>]"
                    "type X = interface"
                    "    inherit (*M*)"
                ]
                [
                    "[<Struct>]"
                    "type X = interface"
                    "    inherit (*M*)"
                ]
                [
                    "type X ="
                    "    inherit System (*M*)."
                ]

                [
                    "type X ="
                    "    inherit System (*M*).Collections"
                ]

            ]

        for invalid in shouldBeInvalid do
            AssertCtrlSpaceCompletionListIsEmpty invalid "(*M*)"

    
    [<Test>]
    member this.``Completion.LongIdentifiers``() = 
        // System.Diagnostics.Debugger.Launch() |> ignore
        AssertCtrlSpaceCompleteContains
            [
                "type X = "
                "   inherit System.   "
            ]
            "System.   "
            ["IDisposable"; "Array"]
            []        

        AssertCtrlSpaceCompleteContains
            [
                "type X = "
                "   inherit System."
                "             (*M*)"
            ]
            "(*M*)"
            ["IDisposable"; "Array"]
            []        

        AssertCtrlSpaceCompleteContains
            [
                "type X = "
                "   inherit System"
                "           .(*M*)"
            ]
            "(*M*)"
            ["IDisposable"; "Array"]
            []        

        // caret is immediately after marker
        AssertCtrlSpaceCompleteContains
            [
                "module Mod ="
                "    let x = 1"
                "module Mod2 = "
                "    let x = 1"
                "type X = "
                "   inherit Mod"
            ]
            "  inherit Mod" 
            ["Mod"; "Mod2"]
            []        
        
        AssertCtrlSpaceCompleteContains
            [
                "type X = "
                "   inherit Sys"
            ]
            "Sys"
            ["System"; "obj"]
            []        

        AssertCtrlSpaceCompleteContains
            [
                "type X = "
                "   inherit System.Collection"
            ]
            "System.Col"
            ["Collections"; "IDisposable"]
            []        


        AssertCtrlSpaceCompleteContains
            [
                "type X = "
                "   inherit System.  Collections"
            ]
            "System. "
            ["Collections"; "IDisposable"]
            []        

        AssertCtrlSpaceCompleteContains
            [
                "type X = "
                "   inherit System.  Collections.ArrayList()"
            ]
            "System. "
            ["Collections"; "IDisposable"]
            []

    [<Test>]
    member public this.``Query.GroupJoin.CompletionInIncorrectJoinRelations``() = 
        let code = 
            [
                "let t ="
                "    query {"
                "        for x in [1] do"
                "        groupJoin y in [\"\"] on (x. ?=? y.) into g"
                "        select 1  }"
            ]
        AssertCtrlSpaceCompleteContains code "(x." ["CompareTo"] ["abs"]
        AssertCtrlSpaceCompleteContains code "? y." ["Chars"; "Length"] ["abs"]

    [<Test>]
    member public this.``Query.Join.CompletionInIncorrectJoinRelations``() = 
        let code = 
            [
                "let t ="
                "    query {"
                "        for x in [1] do"
                "        join y in [\"\"] on (x. ?=? y.)"
                "        select 1  }"
            ]
        AssertCtrlSpaceCompleteContains code "(x." ["CompareTo"] ["abs"]
        AssertCtrlSpaceCompleteContains code "? y." ["Chars"; "Length"] ["abs"]

    [<Test>]
    [<Ignore("Re-enable this test --- https://github.com/Microsoft/visualfsharp/issues/5238")>]
    member public this.``Query.ForKeywordCanCompleteIntoIdentifier``() = 
        let code = 
            [
                "let form = 42"
                "let t ="
                "    query {"
                "        for"
                "    }"
            ]
        AssertCtrlSpaceCompleteContains code "for" ["form"] []  // 'for' is a keyword, but should not prevent completion
    
    [<Test>]
    member public this.``ObjInstance.InheritedClass.MethodsWithDiffAccessbility``() =
        AssertAutoCompleteContainsNoCoffeeBreak 
          [ "type Base =
   val mutable baseField : int
   val mutable private baseFieldPrivate : int
   new () = { baseField = 0; baseFieldPrivate=1 }

type Derived =
    val mutable derivedField : int
    val mutable private derivedFieldPrivate : int
    inherit Base
    new () = { derivedField = 0;derivedFieldPrivate = 0 }

let derived = Derived()
derived.derivedField"]
          "derived."
          [ "baseField"; "derivedField" ] // should contain
          [ "baseFieldPrivate"; "derivedFieldPrivate" ] // should not contain

    [<Test>]
    member public this.``ObjInstance.InheritedClass.MethodsWithDiffAccessbilityWithSameNameMethod``() =
        AssertAutoCompleteContainsNoCoffeeBreak 
          [ "type Base =
   val mutable baseField : int
   val mutable private baseFieldPrivate : int
   new () = { baseField = 0; baseFieldPrivate=1 }

type Derived =
    val mutable baseField : int
    val mutable derivedField : int
    val mutable private derivedFieldPrivate : int
    inherit Base
    new () = { baseField = 0; derivedField = 0; derivedFieldPrivate = 0 }

let derived = Derived()
derived.derivedField"]
          "derived."
          [ "baseField"; "derivedField" ] // should contain
          [ "baseFieldPrivate"; "derivedFieldPrivate" ] // should not contain

    [<Test>]
    member public this.``Visibility.InheritedClass.MethodsWithDiffAccessibility``() =
        AssertAutoCompleteContainsNoCoffeeBreak 
          [ "type Base =
   val mutable baseField : int
   val mutable private baseFieldPrivate : int
   new () = { baseField = 0; baseFieldPrivate=1 }

type Derived =
    val mutable derivedField : int
    val mutable private derivedFieldPrivate : int
    inherit Base
    new () = { derivedField = 0;derivedFieldPrivate = 0 }
    member this.Method() =
        (*marker*)this.baseField"]
          "(*marker*)this."
          [ "baseField"; "derivedField"; "derivedFieldPrivate" ] // should contain
          [ "baseFieldPrivate" ] // should not contain

    [<Test>]
    member public this.``Visibility.InheritedClass.MethodsWithDiffAccessibilityWithSameNameMethod``() =
        AssertAutoCompleteContainsNoCoffeeBreak 
          [ "type Base =
   val mutable baseField : int
   val mutable private baseFieldPrivate : int
   new () = { baseField = 0; baseFieldPrivate=1 }

type Derived =
    val mutable baseField : int
    val mutable derivedField : int
    val mutable private derivedFieldPrivate : int
    inherit Base
    new () = { baseField = 0; derivedField = 0; derivedFieldPrivate = 0 }
    member this.Method() =
        (*marker*)this.baseField"]
          "(*marker*)this."
          [ "baseField"; "derivedField"; "derivedFieldPrivate" ] // should contain
          [ "baseFieldPrivate" ] // should not contain

    [<Test>]
    member public this.``Visibility.InheritedClass.MethodsWithSameNameMethod``() =
        AssertAutoCompleteContainsNoCoffeeBreak 
          [ "type MyClass =
    val foo : int
    new (foo) = { foo = foo }
 
type MyClass2 =
    inherit MyClass
    val foo : int
    new (foo) = {
        inherit MyClass(foo)
        foo = foo
        }

let x = new MyClass2(0)
(*marker*)x.foo"]
          "(*marker*)x."
          [ "foo" ] // should contain
          [ ] // should not contain

    [<Test>]
    member public this.``Identifier.Array.AfterassertKeyword``() =
        AssertAutoCompleteContainsNoCoffeeBreak 
          [ "let x = [1;2;3] "
            "assert x." ]
          "x."
          [ "Head" ] // should contain (from List<int>)
          [ "Listeners" ] // should not contain (from System.Diagnostics.Debug)

    [<Test>]
    member public this.``CtrlSpaceCompletion.Bug130670.Case1``() =
        AssertCtrlSpaceCompleteContainsNoCoffeeBreak 
          [ "let i = async.Return(4)" ]
          ")"
          [ "AbstractClassAttribute" ] // should contain (top-level)
          [ "GetType" ] // should not contain (object instance method)

    [<Test>]
    member public this.``CtrlSpaceCompletion.Bug130670.Case2``() =
        AssertCtrlSpaceCompleteContainsNoCoffeeBreak 
          [ """
                let x = 42 
                let r = x + 1 """ ]
          "1 "
          [ "AbstractClassAttribute" ] // should contain (top-level)
          [ "CompareTo" ] // should not contain (instance method on int)

    [<Test>]
    member public this.``CtrlSpaceCompletion.Bug294974.Case1``() =
        
        AssertCtrlSpaceCompleteContains
          [ """
              let xxx = [1]
         (*M*)xxx.IsEmpty // Ctrl-J just before the '.'""" ]
          "(*M*)xxx"
          [ "xxx" ] // should contain (completions before dot)
          [ "IsEmpty" ] // should not contain (completions after dot)

    [<Test>]
    member public this.``CtrlSpaceCompletion.Bug294974.Case2``() =
        AssertCtrlSpaceCompleteContains
          [ """
              let xxx = [1]
              xxx .IsEmpty // Ctrl-J just before the '.' """ ]
          "xxx "
          [ "AbstractClassAttribute" ] // should contain (top-level)
          [ "IsEmpty" ] // should not contain (completions after dot)

    [<Test>]
    member public this.``ObsoleteProperties.6377_1``() =
        AssertAutoCompleteContainsNoCoffeeBreak 
          [ "System.Security.SecurityManager." ]
          "SecurityManager."
          [ "GetStandardSandbox" ] // should contain
          [ "get_SecurityEnabled"; "set_SecurityEnabled" ] // should not contain

    [<Test>]
    member public this.``ObsoleteProperties.6377_2``() =
        AssertAutoCompleteContainsNoCoffeeBreak 
          [ "System.Threading.Thread.CurrentThread." ]
          "CurrentThread."
          [ "CurrentCulture" ] // should contain: just make sure something shows
          [ "get_ApartmentState"; "set_ApartmentState" ] // should not contain

    [<Test>]
    member public this.``PopupsVersusCtrlSpaceOnDotDot.FirstDot.Popup``() =
        AssertAutoCompleteContainsNoCoffeeBreak 
          [ "System.Console..BackgroundColor" ]
          "System.Console."
          [ "BackgroundColor" ] // should contain (from prior System.Console)
          [ "abs" ] // should not contain (top-level autocomplete on empty identifier)

    [<Test>]
    member public this.``PopupsVersusCtrlSpaceOnDotDot.FirstDot.CtrlSpace``() =
        AssertCtrlSpaceCompleteContainsNoCoffeeBreak 
          [ "System.Console..BackgroundColor" ]
          "System.Console."
          [ "BackgroundColor" ] // should contain (from prior System.Console)
          [ "abs" ] // should not contain (top-level autocomplete on empty identifier)

    [<Test>]
    member public this.``PopupsVersusCtrlSpaceOnDotDot.SecondDot.Popup``() =
        // Salsa is no yet capable of determining whether there would be a popup, it can only test what would appear if there were.
        // So can't do test below.
//        AssertAutoCompleteContainsNoCoffeeBreak 
//          [ "System.Console..BackgroundColor" ]
//          "System.Console.."
//          [ ] // should be empty - in fact, there is no popup here
//          [ "abs"; "BackgroundColor" ] // should not contain anything
        ()

    [<Test>]
    member public this.``PopupsVersusCtrlSpaceOnDotDot.SecondDot.CtrlSpace``() =
        AssertCtrlSpaceCompleteContainsNoCoffeeBreak 
          [ "System.Console..BackgroundColor" ]
          "System.Console.."
          [ ] // should contain nothing - .. is not properly used range operator
          [ "abs" ] // should not contain (from prior System.Console)
    
    [<Test>]
    member public this.``DotCompletionInPatternsPartOfLambda``() = 
        let content = ["let _ = fun x . -> x + 1"]
        AssertCtrlSpaceCompletionListIsEmpty content "x ."

    [<Test>]
    member public this.``DotCompletionInBrokenLambda``() = 
        let content = ["1 |> id (fun x .> x)"]
        AssertCtrlSpaceCompletionListIsEmpty content "x ."

    [<Test>]
    member public this.``DotCompletionInPatterns``() = 
        let useCases = 
            [
                ["let (x, y .) = 1, 2"], "y ."
                ["let run (o : obj) = match o with | :? int as i . -> 1 | _ -> 0"], "as i ."
                ["let (``x.y``, ``y.z`` .) = 1, true"], "z`` ."
                ["let ``x`` . = 1"], "x`` ."
            ]
        for (source, marker) in useCases do
            AssertCtrlSpaceCompletionListIsEmpty source marker


    [<Test>]
    member public this.``DotCompletionWithBrokenLambda``() = 
        let errors = 
            [
                "1 |> id (fun)"
                "1 |> id (fun x > x)"
                "1 |> id (fun x > )"
                "1 |> id (fun x -> )"
            ]
        let testcases = 
            [
                for error in errors do
                    let source = 
                        [
                            "let x = 1"
                            "x."
                        ]
                    yield (error::source), "x.", ["CompareTo"], ["Array"]
                    yield (source @ [error]), "x.", ["CompareTo"], ["Array"]
            ]
        for (source, marker, should, shouldnot) in testcases do
            printfn "%A" source
            AssertCtrlSpaceCompleteContains source marker should shouldnot


    [<Test>]
    [<Ignore("https://github.com/Microsoft/visualfsharp/issues/6166")>]
    member public this.``AfterConstructor.5039_1``() =
        AssertAutoCompleteContainsNoCoffeeBreak 
          [ "let someCall(x) = null"
            "let xe = someCall(System.IO.StringReader()." ]
          "StringReader()."
          [ "ReadBlock" ] // should contain (StringReader)
          [ "LastIndexOfAny" ] // should not contain (String)

    [<Test>]
    [<Ignore("https://github.com/Microsoft/visualfsharp/issues/6166")>]
    member public this.``AfterConstructor.5039_1.CoffeeBreak``() =
        AssertAutoCompleteContains
          [ "let someCall(x) = null"
            "let xe = someCall(System.IO.StringReader()." ]
          "StringReader()."
          [ "ReadBlock" ] // should contain (StringReader)
          [ "LastIndexOfAny" ] // should not contain (String)

    [<Test>]
    member public this.``AfterConstructor.5039_2``() =
        AssertAutoCompleteContainsNoCoffeeBreak 
          [ "System.Random()." ]
          "Random()."
          [ "NextDouble" ] // should contain
          [ ] // should not contain

    [<Test>]
    member public this.``AfterConstructor.5039_3``() =
        AssertAutoCompleteContainsNoCoffeeBreak 
          [ "System.Collections.Generic.List<int>()." ]
          "List<int>()."
          [ "BinarySearch" ] // should contain
          [ ] // should not contain

    [<Test>]
    member public this.``AfterConstructor.5039_4``() =
        AssertAutoCompleteContainsNoCoffeeBreak 
          [ "System.Collections.Generic.List()." ]
          "List()."
          [ "BinarySearch" ] // should contain
          [ ] // should not contain

    [<Test>]
    member public this.``Literal.809979``() =
        AssertAutoCompleteContainsNoCoffeeBreak 
          [ "let value=uint64." ]
          "uint64."
          [ ] // should contain
          [ "Parse" ] // should not contain

    [<Test>]
    member public this.``NameSpace.AsConstructor``() =        
        AssertCtrlSpaceCompleteContainsNoCoffeeBreak 
          [ "new System.DateTime()" ]
          "System.DateTime("  // move to marker
          ["System";"Array2D"]
          ["DaysInMonth"; "AddDays" ] // should contain top level info, no static or instance DateTime members!
         
    [<Test>]
    member public this.``DotAfterApplication1``() = 
        AssertAutoCompleteContainsNoCoffeeBreak
          ["let g a = new System.Random()"
           "(g [])."]
          "(g [])."
          ["Next"]
          [ ]

    [<Test>]
    member public this.``DotAfterApplication2``() = 
        AssertAutoCompleteContainsNoCoffeeBreak
          ["let g a = new System.Random()"
           "g []."]
          "g []."
          ["Head"]
          [ ]

    [<Test>]
    member public this.``Quickinfo.809979``() =
        AssertAutoCompleteContainsNoCoffeeBreak 
          [ "let value=uint64." ]
          "uint64."
          [ ] // should contain
          [ "Parse" ] // should not contain

    /// No intellisense in comments/strings!
    [<Test>]
    member public this.``InString``() = 
        this.VerifyCtrlSpaceCompListIsEmptyAtEndOfMarker(
            fileContents = """ // System.C """ ,  
            marker = "// System.C" )
    [<Test>]
    member public this.``InComment``() = 
        this.VerifyCtrlSpaceCompListIsEmptyAtEndOfMarker(
            fileContents = """ let s = "System.C" """,
            marker = "\"System.C")
                 
    /// Intellisense at the top level (on white space)
    [<Test;Category("Repro")>]
    member public this.``Identifier.OnWhiteSpace.AtTopLevel``() =  
        AssertCtrlSpaceCompleteContainsNoCoffeeBreak
          ["(*marker*)  "] 
          "(*marker*) " 
          ["System"; "Array2D"]
          ["Int32"]
     
    /// Intellisense at the top level (after a partial token). All matches should be shown even if there is a unique match
    [<Test;Category("Repro")>]
    member public this.``TopLevelIdentifier.AfterPartialToken1``() =  
        AssertCtrlSpaceCompleteContainsNoCoffeeBreak
          ["let foobaz = 1"
           "(*marker*)fo"] 
          "(*marker*)fo"
          ["System";"Array2D";"foobaz"]
          ["Int32"]

    [<Test;Category("Repro")>]
    member public this.``TopLevelIdentifier.AfterPartialToken2``() =  
        AssertCtrlSpaceCompleteContainsNoCoffeeBreak
          ["let foobaz = 1"
           "(*marker*)fo"] 
          "(*marker*)"
          ["System";"Array2D";"foobaz"]
          []

(* these issues have not been fixed yet, but when they are, here are some tests
    [<Test>]
    member public this.``AutoComplete.Bug65730``() =        
        AssertAutoCompleteContains 
          [ "let f x y = x.Equals(y)" ]
          "x."       // marker
          [ "Equals" ] // should contain
          [  ] // should not contain

    [<Test>]
    member public this.``AutoComplete.Bug65731_A``() =        
        AssertAutoCompleteContains 
          [ 
@"module SomeOtherPath ="
@"    type r = { a: int; b : int }"
@"let f1 x = { x with SomeOtherPath.a = 3 } // a"
           ]
          "SomeOtherPath."       // marker
          [ "a" ] // should contain
          [  ] // should not contain

    [<Test>]
    member public this.``AutoComplete.Bug65731_B``() =        
        AssertAutoCompleteContains 
          [ 
@"module SomeOtherPath ="
@"    type r = { a: int; b : int }"
@"let f2 x = { x with SomeOtherPath.r.a = 3 } // a"
           ]
          "SomeOtherPath.r."       // marker
          [ "a" ] // should contain
          [  ] // should not contain

    [<Test>]
    member public this.``AutoComplete.Bug69654_0``() =        
        let code = [ @"
                        let q =
                            let a = 42
                            let b = (fun i -> i) 43
                            // i shows up in Ctrl-space list here, b does not
                            ((* *)) // but in the parens, things are correct again
                        "]

        let solution = CreateSolution(this.VS)
        let project = CreateProject(solution,"testproject")
        let file = AddFileFromText(project,"File1.fs", code)
        let file = OpenFile(project,"File1.fs")
        TakeCoffeeBreak(this.VS)
        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)

        MoveCursorToStartOfMarker(file, "//")
        let completions = AutoCompleteAtCursor file
        AssertCompListContains(completions, "b")
        AssertCompListDoesNotContain(completions, "i")

        MoveCursorToStartOfMarker(file, "(* *)")
        let completions = AutoCompleteAtCursor file
        AssertCompListContains(completions, "b")
        AssertCompListDoesNotContain(completions, "i")

        gpatcc.AssertExactly(0,0)

    [<Test>]
    member public this.``AutoComplete.Bug69654_1``() =        
        let code = [ 
                    "let s = async {"
                    "            let! xxx = async { return 0 }"
                    "            xxx.CompareTo |> ignore // the dot works"
                    "            xxx |> ignore // no xxx"
                    "            do xxx |> ignore // no xxx"
                    "            return xxx // no xxx"
                    "        }" ]
        let solution = CreateSolution(this.VS)
        let project = CreateProject(solution,"testproject")
        let file = AddFileFromText(project,"File1.fs", code)
        let file = OpenFile(project,"File1.fs")
        TakeCoffeeBreak(this.VS)
        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)

        MoveCursorToEndOfMarker(file, "xx.Comp")
        let completions = AutoCompleteAtCursor file
        AssertCompListContains(completions, "CompareTo")

        MoveCursorToStartOfMarker(file, "xx.Comp")
        let completions = AutoCompleteAtCursor file
        AssertCompListContains(completions, "xxx")

        MoveCursorToStartOfMarker(file, "xx |>")
        let completions = AutoCompleteAtCursor file
        AssertCompListContains(completions, "xxx")

        MoveCursorToEndOfMarker(file, "do xx")
        let completions = AutoCompleteAtCursor file
        AssertCompListContains(completions, "xxx")

        MoveCursorToEndOfMarker(file, "return xx")
        let completions = AutoCompleteAtCursor file
        AssertCompListContains(completions, "xxx")

        gpatcc.AssertExactly(0,0)

    [<Test>]
    member public this.``AutoComplete.Bug69654_2``() =        
        let code = [ 
                    "let s = async {"
                    "            use xxx = null"
                    "            xxx.Dispose() // the dot works"
                    "            xxx |> ignore // no xxx"
                    "            do xxx |> ignore // no xxx"
                    "            return xxx // no xxx"
                    "        }" ]
        let solution = CreateSolution(this.VS)
        let project = CreateProject(solution,"testproject")
        let file = AddFileFromText(project,"File1.fs", code)
        let file = OpenFile(project,"File1.fs")
        TakeCoffeeBreak(this.VS)
        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)

        MoveCursorToEndOfMarker(file, "xx.Disp")
        let completions = AutoCompleteAtCursor file
        AssertCompListContains(completions, "Dispose")

        MoveCursorToStartOfMarker(file, "xx.Disp")
        let completions = AutoCompleteAtCursor file
        AssertCompListContains(completions, "xxx")

        MoveCursorToStartOfMarker(file, "xx |>")
        let completions = AutoCompleteAtCursor file
        AssertCompListContains(completions, "xxx")

        MoveCursorToEndOfMarker(file, "do xx")
        let completions = AutoCompleteAtCursor file
        AssertCompListContains(completions, "xxx")

        MoveCursorToEndOfMarker(file, "return xx")
        let completions = AutoCompleteAtCursor file
        AssertCompListContains(completions, "xxx")

        gpatcc.AssertExactly(0,0)
*)

    [<Test>]
    member public this.``List.AfterAddLinqNamespace.Bug3754``() =        
        let code = 
                ["open System.Xml.Linq"
                 "List." ]
        let (_, _, file) = this.CreateSingleFileProject(code, references = ["System.Xml"; "System.Xml.Linq"])
        MoveCursorToEndOfMarker(file, "List.")
        let completions = AutoCompleteAtCursor file
        AssertCompListContainsAll(completions, [ "map"; "filter" ] )

    [<Test>]
    member public this.``Global``() =    
        AssertAutoCompleteContainsNoCoffeeBreak
          ["global."]
          "global."
          ["System"; "Microsoft" ]
          []

    [<Test>]
    member public this.``Duplicates.Bug4103a``() =        
        let code = 
            [ 
                "open Microsoft.FSharp.Quotations"
                "Expr." ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        MoveCursorToEndOfMarker(file, "Expr.")
        let completions = AutoCompleteAtCursor file
        
        // Get description for Expr.Var
        let (CompletionItem(_, _, _, descrFunc, _)) = completions |> Array.find (fun (CompletionItem(name, _, _, _, _)) -> name = "WhileLoop")
        let descr = descrFunc()
        // Check whether the description contains the name only once        
        let occurrences = ("  " + descr + "  ").Split([| "WhileLoop" |], System.StringSplitOptions.None).Length - 1
        // You'll get two occurrances - one for the signature, and one for the doc
        AssertEqualWithMessage(2, occurrences, "The entry for 'Expr.Var' is duplicated.")
      
    /// Testing autocomplete after a dot directly following method call
    [<Test>]
    member public this.``AfterMethod.Bug2296``() =        
        AssertAutoCompleteContainsNoCoffeeBreak
          [ "type System.Int32 with"
            "  member x.Int32Member() = 0"
            "\"\".CompareTo(\"a\")." ]
          "(\"a\")."
          ["Int32Member" ]
          []

    /// Testing autocomplete after a dot directly following overloaded method call
    [<Test>]
    member public this.``AfterMethod.Overloaded.Bug2296``() =      
        AssertAutoCompleteContainsNoCoffeeBreak
          ["type System.Boolean with"
           "  member x.BooleanMember() = 0"
           "\"\".Contains(\"a\")."]
          "(\"a\")."
          ["BooleanMember"]
          []

    [<Test;Category("Repro")>]
    member public this.``BasicGlobalMemberList``() =  
        AssertAutoCompleteContainsNoCoffeeBreak
          ["let x = 1"
           "x."]
          "x."
          ["CompareTo"; "GetHashCode"]
          []

    [<Test;Category("Repro")>]
    member public this.``CharLiteral``() =  
        AssertAutoCompleteContainsNoCoffeeBreak
          ["let x = \"foo\""
           "let x' = \"bar\""
           "x'."]
          "x'."
          ["CompareTo";"GetHashCode"]
          []

    [<Test;Category("Repro")>]
    member public this.``GlobalMember.ListOnIdentifierEndingWithTick``() =      
        AssertAutoCompleteContainsNoCoffeeBreak
          ["let x' = 1"
           "x'."]
          "x'."
          ["CompareTo";"GetHashCode"]
          []

    [<Test;Category("Repro")>]
    member public this.``GlobalMember.ListOnIdentifierContainingTick``() =   
        AssertAutoCompleteContainsNoCoffeeBreak
          ["let x'y = 1"
           "x'y."]
          "x'y."
          ["CompareTo";"GetHashCode"]
          []
        
    [<Test;Category("Repro")>]
    member public this.``GlobalMember.ListWithPartialMember1``() =    
        AssertCtrlSpaceCompleteContainsNoCoffeeBreak
          ["let x = 1"
           "x.CompareT"]
          "x.CompareT"
          ["CompareTo";"GetHashCode"]
          []

    [<Test;Category("Repro")>]
    member public this.``GlobalMember.ListWithPartialMember2``() =    
        AssertAutoCompleteContainsNoCoffeeBreak
          ["let x = 1"
           "x.CompareT"]
          "x."
          ["CompareTo";"GetHashCode"]
          []

    /// Wrong intellisense for array              
    [<Test;Category("Repro")>]
    member public this.``DotOff.Parenthesized.Expr``() =       
        AssertAutoCompleteContainsNoCoffeeBreak
          ["let string_of_int (x:int) = x.ToString()"
           "let strs = Array.init 10 string_of_int"
           "let x = (strs.[1])."]
          "(strs.[1])."
          ["Substring";"GetHashCode"]
          []

    /// Wrong intellisense for array
    [<Test;Category("Repro")>]
    member public this.``DotOff.ArrayIndexerNotation``() =  
        AssertAutoCompleteContainsNoCoffeeBreak
          ["let string_of_int (x:int) = x.ToString()"
           "let strs = Array.init 10 string_of_int"
           "let test1 = strs.[1]."]
          "strs.[1]."
          ["Substring";"GetHashCode"]
          []

    /// Wrong intellisense for array
    [<Test;Category("Repro")>]
    member public this.``DotOff.ArraySliceNotation1``() =    
        AssertAutoCompleteContainsNoCoffeeBreak
          ["let string_of_int (x:int) = x.ToString()"
           "let strs = Array.init 10 string_of_int"
           "let test2 = strs.[1..]."
           "let test3 = strs.[..1]."
           "let test4 = strs.[1..1]."]
          "trs.[1..]."
          ["Length"]
          []

    [<Test;Category("Repro")>]
    member public this.``DotOff.ArraySliceNotation2``() =    
        AssertAutoCompleteContainsNoCoffeeBreak
          ["let string_of_int (x:int) = x.ToString()"
           "let strs = Array.init 10 string_of_int"
           "let test2 = strs.[1..]."
           "let test3 = strs.[..1]."
           "let test4 = strs.[1..1]."]
          "strs.[..1]."
          ["Length"]
          []

    [<Test;Category("Repro")>]
    member public this.``DotOff.ArraySliceNotation3``() =    
        AssertAutoCompleteContainsNoCoffeeBreak
          ["let string_of_int (x:int) = x.ToString()"
           "let strs = Array.init 10 string_of_int"
           "let test2 = strs.[1..]."
           "let test3 = strs.[..1]."
           "let test4 = strs.[1..1]."]
          "strs.[1..1]."
          ["Length"]
          []
           
    [<Test;Category("Repro")>]
    member public this.``DotOff.DictionaryIndexer``() =        
        AssertAutoCompleteContainsNoCoffeeBreak
          ["let dict = new System.Collections.Generic.Dictionary<int,string>()"
           "let test5 = dict.[1]."]
          "dict.[1]."
          ["Length"]
          []

    /// intellisense on DOT
    [<Test;Category("Repro")>]
    member public this.``EmptyFile.Dot.Bug1115``() =   
        this.VerifyAutoCompListIsEmptyAtEndOfMarker(
            fileContents = "." ,
            marker = ".")    

    [<Test;Category("Repro")>]
    member public this.``Identifier.NonDottedNamespace.Bug1347``() =     
        this.AssertCtrlSpaceCompletionContains(
            ["open System"
             "open Microsoft.FSharp.Math"
             "let x = Mic"
             "let p7 ="
             "    let seive limit = "
             "        let isPrime = Array.create (limit+1) true"
             "        for n in"],
            "let x = Mic",
            "Microsoft")

    [<Test;Category("Repro")>]
    member public this.``MatchStatement.WhenClause.Bug2519``() =   
        AssertAutoCompleteContainsNoCoffeeBreak
          ["type DU = X of int"
           "let timefilter pkt ="
           "    match pkt with"
           "    | X(hdr) when (*aaa*)hdr."
           "    | _ -> ()"]
          "(*aaa*)hdr."
          ["CompareTo";"GetHashCode"]
          []
        
    [<Test;Category("Repro")>]
    member public this.``String.BeforeIncompleteModuleDefinition.Bug2385``() =     
        AssertAutoCompleteContainsNoCoffeeBreak
          ["let s = \"hello\"."
           "module Timer ="]
          "\"hello\"."
          ["Substring";"GetHashCode"]
          []

    [<Test>]
    member public this.``Project.FsFileWithBuildAction``() =
        AssertAutoCompleteContainsNoCoffeeBreak
          ["let i = 4"
           "let r = i.ToString()"
           "let x = File1.bob"]
          "i."
          ["CompareTo"]
          []

     /// Dotting off a string literal should work.
    [<Test;Category("Repro")>]
    member public this.``DotOff.String``() =
        AssertAutoCompleteContainsNoCoffeeBreak
          ["\"x\". (*marker*)"
           ""]
          "\"x\"."
          ["Substring";"GetHashCode"]
          []
                   
    /// FEATURE: Pressing dot (.) after an local variable will produce an Intellisense list of members the user may select.
    [<Test;Category("Repro")>]
    member public this.``BasicLocalMemberList``() = 
        AssertAutoCompleteContainsNoCoffeeBreak 
          ["let MyFunction (s:string) = "
           "    let y=\"dog\""
           "    y."
           "    ()"]
          "    y."
          ["Substring";"GetHashCode"]
          []

    [<Test;Category("Repro")>]
    member public this.``LocalMemberList.WithPartialMemberEntry1``() = 
        AssertCtrlSpaceCompleteContainsNoCoffeeBreak
          ["let MyFunction (s:string) = "
           "    let y=\"dog\""
           "    y.Substri"
           "    ()"]
          "    y.Substri"
          ["Substring";"GetHashCode"]
          []

    [<Test;Category("Repro")>]
    member public this.``LocalMemberList.WithPartialMemberEntry2``() = 
        AssertAutoCompleteContainsNoCoffeeBreak
          ["let MyFunction (s:string) = "
           "    let y=\"dog\""
           "    y.Substri"
           "    ()"]
          "    y."
          ["Substring";"GetHashCode"]
          []

    [<Test;Category("Repro")>]
    [<Ignore("Re-enable this test --- https://github.com/Microsoft/visualfsharp/issues/5238")>]
    member public this.``CurriedArguments.Regression1``() = 
        AssertCtrlSpaceCompleteContainsNoCoffeeBreak  
           ["let fffff x y = 1"
            "let ggggg  = 1"
            "let test1 = fffff \"a\" ggggg"
            "let test2 = fffff 1 ggggg"
            "let test3 = fffff ggggg ggggg"] 
           "let f"
           ["fffff"]
           []   
           
    [<Test;Category("Repro")>]
    member public this.``CurriedArguments.Regression2``() = 
        AssertCtrlSpaceCompleteContainsNoCoffeeBreak  
           ["let fffff x y = 1"
            "let ggggg  = 1"
            "let test1 = fffff \"a\" ggggg"
            "let test2 = fffff 1 ggggg"
            "let test3 = fffff ggggg ggggg"] 
           "let test1 = f"
           ["fffff"]
           []       
           
    [<Test;Category("Repro")>]
    member public this.``CurriedArguments.Regression3``() = 
        AssertCtrlSpaceCompleteContainsNoCoffeeBreak  
           ["let fffff x y = 1"
            "let ggggg  = 1"
            "let test1 = fffff \"a\" ggggg"
            "let test2 = fffff 1 ggggg"
            "let test3 = fffff ggggg ggggg"] 
           "let test1 = fffff \"a\" gg"
           ["ggggg"]
           []                                 
      
    [<Test;Category("Repro")>]
    member public this.``CurriedArguments.Regression4``() = 
        AssertCtrlSpaceCompleteContainsNoCoffeeBreak  
           ["let fffff x y = 1"
            "let ggggg  = 1"
            "let test1 = fffff \"a\" ggggg"
            "let test2 = fffff 1 ggggg"
            "let test3 = fffff ggggg ggggg"] 
           "let test2 = fffff 1 gg"
           ["ggggg"]
           []

    [<Test;Category("Repro")>]
    member public this.``CurriedArguments.Regression5``() = 
        AssertCtrlSpaceCompleteContainsNoCoffeeBreak  
           ["let fffff x y = 1"
            "let ggggg  = 1"
            "let test1 = fffff \"a\" ggggg"
            "let test2 = fffff 1 ggggg"
            "let test3 = fffff ggggg ggggg"] 
           "let test3 = fffff gg"
           ["ggggg"]
           []

    [<Test;Category("Repro")>]
    member public this.``CurriedArguments.Regression6``() = 
        AssertCtrlSpaceCompleteContainsNoCoffeeBreak  
           ["let fffff x y = 1"
            "let ggggg  = 1"
            "let test1 = fffff \"a\" ggggg"
            "let test2 = fffff 1 ggggg"
            "let test3 = fffff ggggg ggggg"] 
           "let test3 = fffff ggggg gg"
           ["ggggg"]
           []

    // Test whether standard types appear in the completion list under both F# and .NET name
    [<Test>]
    member public this.``StandardTypes.Bug4403``() = 
        AssertCtrlSpaceCompleteContainsNoCoffeeBreak
          ["open System"; "let x=" ]
          "let x="
          ["int8"; "int16"; "int32"; "string"; "SByte"; "Int16"; "Int32"; "String" ]
          [ ]

    // Test whether standard types appear in the completion list under both F# and .NET name            
    [<Test>]
    member public this.``ValueDeclarationHidden.Bug4405``() = 
        AssertAutoCompleteContainsNoCoffeeBreak
          [ "do  "
            "  let a = \"string\""
            "  let a = if true then 0 else a."]
          "else a."
          ["IndexOf"; "Substring"]
          [ ]

    [<Test>]
    member public this.``StringFunctions``() = 
        let code = 
            [
                "let y = String."
                "let f x = 0"
            ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        MoveCursorToEndOfMarker(file,"String.")
        let completions = time1 AutoCompleteAtCursor file "Time of first autocomplete."
        // printf "Completions=%A\n" completions
        Assert.IsTrue(completions.Length > 0)
        for completion in completions do
            match completion with 
              | CompletionItem(_,_,_,_,DeclarationType.Method) -> ()
              | CompletionItem(name,_,_,_,x) -> failwith (sprintf "Unexpected item %s seen with declaration type %A" name x)
                         
    // FEATURE: Pressing ctrl+space or ctrl+j will give a list of valid completions.
    
    [<Test>]
    //Verified atleast "Some" is contained in the Ctrl-Space Completion list
    member public this.``NonDotCompletion``() =  
        this.AssertCtrlSpaceCompletionContains(
            ["let x = S"],
            "x = S",
            "Some")
     
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.EditorHideMethodsAttribute")>]
    // This test case checks Pressing ctrl+space on the provided Type instance method shows list of valid completions
    member this.``TypeProvider.EditorHideMethodsAttribute.InstanceMethod.CtrlSpaceCompletionContains``() =
        this.AssertCtrlSpaceCompletionContains(
            fileContents = [""" 
                                let t = new N1.T1()
                                t.I"""],
            marker = "t.I",
            expected = "IM1",    
            addtlRefAssy = [PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll")])
    
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.EditorHideMethodsAttribute")>]
    // This test case checks Pressing ctrl+space on the provided Type Event shows list of valid completions
    member this.``TypeProvider.EditorHideMethodsAttribute.Event.CtrlSpaceCompletionContains``() =
        this.AssertCtrlSpaceCompletionContains(
            fileContents = [""" 
                                let t = new N.T()
                                t.Eve"""],
            marker = "t.Eve", 
            expected = "Event1",          
            addtlRefAssy = [PathRelativeToTestAssembly(@"EditorHideMethodsAttribute.dll")])
     
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.EditorHideMethodsAttribute")>]
    // This test case checks Pressing ctrl+space on the provided Type static parameter and verify "int" is in the list just to make sure bad things don't happen and autocomplete window pops up
    member this.``TypeProvider.EditorHideMethodsAttribute.Type.CtrlSpaceCompletionContains``() =
        this.AssertCtrlSpaceCompletionContains(
            fileContents = [""" 
                                type boo = N1.T<in"""],
            marker = "T<in",  
            expected = "int",  
            addtlRefAssy = [PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll")])
    
        
    // In this bug, pressing dot after this was producing an invalid member list.       
    [<Test>]
    member public this.``Class.Self.Bug1544``() =     
        this.VerifyAutoCompListIsEmptyAtEndOfMarker(
            fileContents = "
                type Foo() =
                    member this.",
            marker = "this.")

    // No completion list at the end of file. 
    [<Test>]
    member public this.``Idenfitier.AfterDefined.Bug1545``() = 
        this.AutoCompletionListNotEmpty
            ["let x = [|\"hello\"|]"
             "x."]
            "x."                              

    [<Test>]
    member public this.``Bug243082.DotAfterNewBreaksCompletion`` () = 
        this.AutoCompletionListNotEmpty
            [
            "module A ="
            "    type B() = class end"
            "let s = 1"
            "s."
            "let z = new A."]
            "s."        

    [<Test>]
    member public this.``Bug243082.DotAfterNewBreaksCompletion2`` () = 
        this.AutoCompletionListNotEmpty
            [
            "let s = 1"
            "s."
            "new System."]
            "s."        

    [<Test>]
    [<Category("QueryExpressions")>]
    member this.``QueryExpression.CtrlSpaceSmokeTest0``() = 
           this.VerifyCtrlSpaceListContainAllAtStartOfMarker(
              fileContents = """
                module BasicTest
                let x = si(*Marker*)""" ,
              marker = "(*Marker*)",
              list = ["sin"],
              addtlRefAssy=standard40AssemblyRefs )

    [<Test>]
    [<Category("QueryExpressions")>]
    member this.``QueryExpression.CtrlSpaceSmokeTest0b``() = 
           this.VerifyCtrlSpaceListContainAllAtStartOfMarker(
              fileContents = """
                module BasicTest
                let x = qu(*Marker*)""" ,
              marker = "(*Marker*)",
              list = ["query"],
              addtlRefAssy=standard40AssemblyRefs   )

    [<Test>]
    [<Category("QueryExpressions")>]
    member this.``QueryExpression.CtrlSpaceSmokeTest1``() = 
           this.VerifyCtrlSpaceListContainAllAtStartOfMarker(
              fileContents = """
                module BasicTest
                let x = query { for x in [1;2;3] do sel(*Marker*)""" ,
              marker = "(*Marker*)",
              list = ["select"],
              addtlRefAssy=standard40AssemblyRefs  )

    [<Test>]
    [<Category("QueryExpressions")>]
    member this.``QueryExpression.CtrlSpaceSmokeTest1b``() = 
           this.VerifyCtrlSpaceListContainAllAtStartOfMarker(
              fileContents = """
                module BasicTest
                let x = query { for x in [1;2;3] do (*Marker*)""" ,
              marker = "(*Marker*)",
              list = ["select"],
              addtlRefAssy=standard40AssemblyRefs  )



    [<Test>]
    [<Category("QueryExpressions")>]
    member this.``QueryExpression.CtrlSpaceSmokeTest2``() = 
           this.VerifyCtrlSpaceListContainAllAtStartOfMarker(
              fileContents = """
                module BasicTest
                let x = query { for x in [1;2;3] do sel(*Marker*) }""" ,
              marker = "(*Marker*)",
              list = ["select"],
              addtlRefAssy=standard40AssemblyRefs  )


    [<Test>]
    [<Category("QueryExpressions")>]
    member this.``QueryExpression.CtrlSpaceSmokeTest3``() = 
           this.VerifyCtrlSpaceListContainAllAtStartOfMarker(
              fileContents = """
                module BasicTest
                let x = query { for xxxxxx in [1;2;3] do xxx(*Marker*)""" ,
              marker = "(*Marker*)",
              list = ["xxxxxx"],
              addtlRefAssy=standard40AssemblyRefs  )


    [<Test>]
    [<Category("QueryExpressions")>]
    member this.``QueryExpression.CtrlSpaceSmokeTest3b``() = 
           this.VerifyCtrlSpaceListContainAllAtStartOfMarker(
              fileContents = """
                module BasicTest
                let x = seq { for xxxxxx in [1;2;3] do xxx(*Marker*)""" ,
              marker = "(*Marker*)",
              list = ["xxxxxx"],
              addtlRefAssy=standard40AssemblyRefs  )



    [<Test>]
    [<Category("QueryExpressions")>]
    member this.``QueryExpression.CtrlSpaceSmokeTest3c``() = 
           this.VerifyCtrlSpaceListContainAllAtStartOfMarker(
              fileContents = """
                module BasicTest
                let x = async { for xxxxxx in [1;2;3] do xxx(*Marker*)""" ,
              marker = "(*Marker*)",
              list = ["xxxxxx"],
              addtlRefAssy=standard40AssemblyRefs  )


    [<Test>]
    [<Category("QueryExpressions")>]
    member this.``AsyncExpression.CtrlSpaceSmokeTest3d``() = 
           this.VerifyCtrlSpaceListContainAllAtStartOfMarker(
              fileContents = """
                module BasicTest
                let x = async { for xxxxxx in [1;2;3] do xxx(*Marker*) }""" ,
              marker = "(*Marker*)",
              list = ["xxxxxx"],
              addtlRefAssy=standard40AssemblyRefs  )


    member this.QueryExpressionFileExamples() = 
           [ """
                module BasicTest
                let x = query { for x in [1;2;3] do (*TYPING*)"""
             """
                module BasicTest
                let x = query { for x in [1;2;3] do (*TYPING*) }""" 
             """
                module BasicTest
                let x = query { for x in [1;2;3] do 
                                (*TYPING*)""" 
             """
                module BasicTest
                let x = query { for x in [1;2;3] do 
                                if x > 3 then 
                                (*TYPING*)""" 
             """
                module BasicTest
                let x = query { for x in [1;2;3] do 
                                where (x > 3)
                                (*TYPING*)""" 
             """
                module BasicTest
                let x = query { for x in [1;2;3] do 
                                sortBy x 
                                (*TYPING*)""" 
             """
                module BasicTest
                let x = query { for x in [1;2;3] do 
                                (*TYPING*)
                                sortBy x """ 
             """
                module BasicTest
                let x = query { for x in [1;2;3] do 
                                let y = x + 1
                                (*TYPING*)""" ]

    [<Test>]
    /// This is the case where at (*TYPING*) we first type 1...N-1 characters of the target custom operation and then invoke the completion list, and we check that the completion list contains the custom operation
    [<Category("QueryExpressions")>]
    [<Category("Expensive")>]
    member this.``QueryExpression.CtrlSpaceSystematic1``() = 
       let rec strictPrefixes (s:string) = seq { if s.Length > 1 then let s = s.[0..s.Length-2] in yield s; yield! strictPrefixes s}
       for customOperation in ["select";"skip";"contains";"groupJoin"] do
        printfn " Running systematic tests looking for completion of '%s' at multiple locations"  customOperation
        for idText in strictPrefixes customOperation do
         for i,fileContents in this.QueryExpressionFileExamples() |> List.mapi (fun i x -> (i,x)) do
           let fileContents = fileContents.Replace("(*TYPING*)",idText+"(*Marker*)")
           try 
               this.VerifyCtrlSpaceListContainAllAtStartOfMarker(
                  fileContents = fileContents,
                  marker = "(*Marker*)",
                  list = [customOperation],
                  addtlRefAssy=standard40AssemblyRefs )
           with _ -> 
               printfn "FAILURE: customOperation = %s, idText = %s, fileContents <<<%s>>>" customOperation idText fileContents
               reraise()


    member this.WordByWordSystematicTestWithSpecificExpectations(prefix, suffixes, lines, variations, knownFailures:list<_>) = 

        let knownFailuresDict = set knownFailures
        printfn "Building systematic tests, excluding %d known failures" knownFailures.Length  
        let tests = 
            [ for (suffixName,suffixText) in suffixes  do
                for builderName in variations do
                  for (lineName, line, checks) in lines builderName do 
                    for check in checks do
                      let expectedToFail = knownFailuresDict.Contains (lineName, suffixName, builderName, check)
                      if not expectedToFail then yield (lineName, suffixName, suffixText, builderName, line, check, expectedToFail) ]

        let unexpectedSuccesses = ResizeArray<_>()
        let successes = ResizeArray<_>()
        let failures = ResizeArray<_>()
        printfn "Running %d systematic tests.... Failure will be printed if it occurs..."  tests.Length
        for (lineName, suffixName, suffixText, builderName, fileContents, check, expectedToFail) in tests do
            if successes.Count % 50 = 0 then
                printfn "Making progress, run %d so far..." successes.Count
            let fileContents = prefix + fileContents + suffixText
            try
                match check with
                | QuickInfoExpected(where,expected) ->
                      let where = where.[0..where.Length-2] // chop a character off to get in the middle of the text
                      this.AssertQuickInfoContainsAtEndOfMarker(fileContents,where,expected,addtlRefAssy=standard40AssemblyRefs )
                | AutoCompleteExpected(where,expected) ->
                      this.VerifyCtrlSpaceListContainAllAtStartOfMarker(fileContents,where,[expected],addtlRefAssy=standard40AssemblyRefs )
                | DotCompleteExpected(where,expected) ->
                      this.VerifyDotCompListContainAllAtStartOfMarker(fileContents,where,[expected], addtlRefAssy=standard40AssemblyRefs)
                if not expectedToFail then
                    successes.Add(lineName,suffixName,builderName,check)
                else
                    unexpectedSuccesses.Add(lineName,suffixName,builderName,check)
            with e ->
                printfn "Exception thrown: (\"%s\", \"%s\", \"%s\", %A) " lineName suffixName builderName check
                if not expectedToFail then
                    printfn " FAILURE on systematic test: (\"%s\", \"%s\", \"%s\", %A) " lineName suffixName builderName check
                    printfn "\n\nfileContents = <<<%s>>>" fileContents
                    failures.Add(lineName,suffixName,builderName,check)

        let nFail = failures.Count
        let nSuccess = successes.Count
        printfn "%d TESTS, %d SUCCESS, %d FAILURE, %%%2.2f success rate" (nSuccess+nFail) successes.Count nFail  (float nSuccess / float (nSuccess+nFail) * 100.0)

        if failures.Count <> 0 then 
            printfn "EXTRA OBSERVED FAILURES:  "
            printfn "["
            for (lineName,suffixName,builderName,check) in failures do
                printfn "   (\"%s\", \"%s\", \"%s\", %A) " lineName suffixName builderName check
            printfn "]"
             
        if unexpectedSuccesses.Count <> 0 then 
            printfn "EXTRA OBSERVED SUCCESSES:  "
            printfn "["
            for (lineName,suffixName,builderName,check) in unexpectedSuccesses do
                printfn "   (\"%s\", \"%s\", \"%s\", %A) " lineName suffixName builderName check
            printfn "]"

        if failures.Count <> 0 || unexpectedSuccesses.Count <> 0 then 
            raise <| new Exception("there were unexpected results, see console output for details")

    [<Test>]
    [<Category("QueryExpressions")>]
    [<Category("Expensive")>]
    [<Ignore("https://github.com/Microsoft/visualfsharp/issues/6166")>]
    member this.``QueryExpressions.QueryAndSequenceExpressionWithForYieldLoopSystematic``() = 

        let prefix =  """
module Test
let aaaaaa = [| "1" |]
"""
        let suffixes = 
          [ "Empty",                                ""; 
            "ClosingBrace",                         " }"; 
            "ClosingBrace,NextDefinition",        " } \nlet nextDefinition () = 1\n"; 
            "NoClosingBrace,NextDefinition",      " \nlet nextDefinition () = 1\n"; 
            "NoClosingBrace,NextTypeDefinition",  " \ntype NextDefinition() = member x.P = 1\n" 
          ] 
        let lines b = 
          [ "AL1",  "let v = " + b +  " { "                                                              , []
            "AL2",  "let v = " + b +  " { for "                                                          , []
            "AL3",  "let v = " + b +  " { for bbbb "                                                     , [QI "for bbbb" "val bbbb"]
            "AL4",  "let v = " + b +  " { for bbbb in (*C*)"                                             , [QI "for bbbb" "val bbbb"; AC "(*C*)" "aaaaaa" ]
            "AL5",  "let v = " + b +  " { for bbbb in [ (*C*) "                                          , [QI "for bbbb" "val bbbb"; AC "(*C*)" "aaaaaa" ]
            "AL6",  "let v = " + b +  " { for bbbb in [ aaa(*C*) "                                       , [QI "for bbbb" "val bbbb"; AC "(*C*)" "aaaaaa" ]
            "AL7",  "let v = " + b +  " { for bbbb in [ aaaaaa(*D1*)"                                    , [QI "for bbbb" "val bbbb"; QI "aaaaaa" "val aaaaaa"; DC "(*D1*)" "Length" ]
            "AL8",  "let v = " + b +  " { for bbbb in [ aaaaaa(*D1*) ] "                                 , [QI "for bbbb" "val bbbb"; QI "aaaaaa" "val aaaaaa"; DC "(*D1*)" "Length" ]
            "AL9",  "let v = " + b +  " { for bbbb in [ aaaaaa(*D1*) ] do (*C*)"                         , [QI "for bbbb" "val bbbb"; QI "aaaaaa" "val aaaaaa"; AC "(*C*)" (if b = "query" then "select" else "sin"); DC "(*D1*)" "Length" ]
            "AL10", "let v = " + b +  " { for bbbb in [ aaaaaa(*D1*) ] do yield (*C*) "                  , [QI "for bbbb" "val bbbb"; QI "aaaaaa" "val aaaaaa"; AC "(*C*)" "aaaaaa"; AC "(*C*)" "bbbb" ; DC "(*D1*)" "Length" ] 
            "AL11", "let v = " + b +  " { for bbbb in [ aaaaaa(*D1*) ] do yield bb(*C*) "                , [QI "for bbbb" "val bbbb"; QI "aaaaaa" "val aaaaaa"; AC "(*C*)" "bbbb" ; DC "(*D1*)" "Length" ]
            "AL12", "let v = " + b +  " { for bbbb in [ aaaaaa(*D1*) ] do yield bbbb(*D2*) "             , [QI "for bbbb" "val bbbb"; QI "aaaaaa" "val aaaaaa"; QI "yield bbbb" "val bbbb"; DC "(*D1*)" "Length" ; DC "(*D2*)" "Length" ]
            "AL13", "let v = " + b +  " { for bbbb in [ aaaaaa(*D1*) ] do yield bbbb(*D2*) + (*C*)"      , [QI "for bbbb" "val bbbb"; QI "aaaaaa" "val aaaaaa"; QI "yield bbbb" "val bbbb"; AC "(*C*)" "aaaaaa"; AC "(*C*)" "bbbb" ; DC "(*D1*)" "Length" ; DC "(*D2*)" "Length" ] 
            "AL14", "let v = " + b +  " { for bbbb in [ aaaaaa(*D1*) ] do yield bbbb(*D2*) + bb(*C*)"    , [QI "for bbbb" "val bbbb"; QI "aaaaaa" "val aaaaaa"; QI "yield bbbb" "val bbbb"; AC "(*C*)" "bbbb" ; DC "(*D1*)" "Length" ; DC "(*D2*)" "Length" ]
            "AL15", "let v = " + b +  " { for bbbb in [ aaaaaa(*D1*) ] do yield bbbb(*D2*) + bbbb(*D3*)" , [QI "for bbbb" "val bbbb"; QI "aaaaaa" "val aaaaaa"; QI "yield bbbb" "val bbbb"; QI "+ bbbb" "val bbbb"; DC "(*D3*)" "Length" ] ]


        let knownFailures = 
            [
                ("AL3", "NoClosingBrace,NextDefinition", "query", QuickInfoExpected ("for bbbb","val bbbb"))
                ("AL3", "NoClosingBrace,NextDefinition", "seq", QuickInfoExpected ("for bbbb","val bbbb"))
                ("AL6", "NoClosingBrace,NextDefinition", "query", AutoCompleteExpected ("(*C*)","aaaaaa"))
                ("AL6", "NoClosingBrace,NextDefinition", "query", QuickInfoExpected ("for bbbb","val bbbb"))
                ("AL6", "NoClosingBrace,NextDefinition", "seq", AutoCompleteExpected ("(*C*)","aaaaaa"))
                ("AL6", "NoClosingBrace,NextDefinition", "seq", QuickInfoExpected ("for bbbb","val bbbb"))
                ("AL7", "NoClosingBrace,NextDefinition", "query", DotCompleteExpected ("(*D1*)","Length"))
                ("AL7", "NoClosingBrace,NextDefinition", "query", QuickInfoExpected ("aaaaaa","val aaaaaa"))
                ("AL7", "NoClosingBrace,NextDefinition", "query", QuickInfoExpected ("for bbbb","val bbbb"))
                ("AL7", "NoClosingBrace,NextDefinition", "seq", DotCompleteExpected ("(*D1*)","Length"))
                ("AL7", "NoClosingBrace,NextDefinition", "seq", QuickInfoExpected ("aaaaaa","val aaaaaa"))
                ("AL7", "NoClosingBrace,NextDefinition", "seq", QuickInfoExpected ("for bbbb","val bbbb"))
                ("AL10", "ClosingBrace", "query", AutoCompleteExpected ("(*C*)","bbbb"))
                ("AL10", "ClosingBrace,NextDefinition", "query", AutoCompleteExpected ("(*C*)","bbbb"))
                ("AL10", "Empty", "query", AutoCompleteExpected ("(*C*)","bbbb"))
                ("AL10", "Empty", "seq", AutoCompleteExpected ("(*C*)","bbbb"))
                ("AL10", "NoClosingBrace,NextDefinition", "query", AutoCompleteExpected ("(*C*)","bbbb"))
                ("AL10", "NoClosingBrace,NextDefinition", "seq", AutoCompleteExpected ("(*C*)","bbbb"))
                ("AL10", "NoClosingBrace,NextTypeDefinition", "query", AutoCompleteExpected ("(*C*)","bbbb"))
                ("AL10", "NoClosingBrace,NextTypeDefinition", "seq", AutoCompleteExpected ("(*C*)","bbbb"))
            ]

        this.WordByWordSystematicTestWithSpecificExpectations(prefix, suffixes, lines, ["seq";"query"], knownFailures) 

    [<Test>]
    [<Category("QueryExpressions")>]
    [<Category("Expensive")>]
    /// Incrementally enter a seq{ .. while ...} loop and check for availability of intellisense etc.
    member this.``SequenceExpressions.SequenceExprWithWhileLoopSystematic``() = 

        let prefix =  """
module Test
let abbbbc = [| 1 |]
let aaaaaa = 0
"""
        let suffixes = 
          [ "Empty",                                ""; 
            "ClosingBrace",                         " }"; 
            "ClosingBrace,NextDefinition",        " } \nlet nextDefinition () = 1\n"; 
            "NoClosingBrace,NextDefinition",      " \nlet nextDefinition () = 1\n"; 
            "NoClosingBrace,NextTypeDefinition",  " \ntype NextDefinition() = member x.P = 1\n" 
          ] 
        let lines b = 
          [ "BL1",  "let f()  = seq { while abb(*C*)"                                     , [AC "(*C*)" "abbbbc"]
            "BL2",  "let f()  = seq { while abbbbc(*D1*)"                                 , [QI "while abbbbc" "val abbbbc"; DC "(*D1*)" "Length"]
            "BL3",  "let f()  = seq { while abbbbc(*D1*) do (*C*)"                        , [QI "while abbbbc" "val abbbbc"; DC "(*D1*)" "Length"; AC "(*C*)" "abbbbc"]
            "BL4",  "let f()  = seq { while abbbbc(*D1*) do abb(*C*)"                     , [QI "while abbbbc" "val abbbbc"; DC "(*D1*)" "Length"; AC "(*C*)" "abbbbc"]
            "BL5",  "let f()  = seq { while abbbbc(*D1*) do abbbbc(*D2*)"                 , [QI "while abbbbc" "val abbbbc"; DC "(*D1*)" "Length"; QI "do abbbbc" "val abbbbc"; DC "(*D2*)" "Length"; ]
            "BL6",  "let f()  = seq { while abbbbc(*D1*) do abbbbc.[(*C*)"                , [QI "while abbbbc" "val abbbbc"; DC "(*D1*)" "Length"; QI "do abbbbc" "val abbbbc"; AC "(*C*)" "abbbbc"; AC "(*C*)" "aaaaaa"; ]
            "BL7",  "let f()  = seq { while abbbbc(*D1*) do abbbbc.[aaa(*C*)"             , [QI "while abbbbc" "val abbbbc"; DC "(*D1*)" "Length"; QI "do abbbbc" "val abbbbc"; AC "(*C*)" "aaaaaa"; ]
            "BL7a", "let f()  = seq { while abbbbc(*D1*) do abbbbc.[aaa(*C*)]"            , [QI "while abbbbc" "val abbbbc"; DC "(*D1*)" "Length"; QI "do abbbbc" "val abbbbc"; AC "(*C*)" "aaaaaa"; ]
            "BL7b", "let f()  = seq { while abbbbc(*D1*) do abbbbc.[aaa(*C*)] <- "        , [QI "while abbbbc" "val abbbbc"; DC "(*D1*)" "Length"; QI "do abbbbc" "val abbbbc"; AC "(*C*)" "aaaaaa"; ]
            "BL7c", "let f()  = seq { while abbbbc(*D1*) do abbbbc.[aaa(*C*)] <- 1"       , [QI "while abbbbc" "val abbbbc"; DC "(*D1*)" "Length"; QI "do abbbbc" "val abbbbc"; AC "(*C*)" "aaaaaa"; ]
            "BL7d", "let f()  = seq { while abbbbc(*D1*) do abbbbc.[ (*C*) ] <- 1"        , [QI "while abbbbc" "val abbbbc"; DC "(*D1*)" "Length"; QI "do abbbbc" "val abbbbc"; AC "(*C*)" "aaaaaa"; ]
            "BL8",  "let f()  = seq { while abbbbc(*D1*) do abbbbc.[aaaaaa]"              , [QI "while abbbbc" "val abbbbc"; DC "(*D1*)" "Length"; QI "do abbbbc" "val abbbbc"; ]
            "BL9",  "let f()  = seq { while abbbbc(*D1*) do abbbbc.[aaaaaa] <- (*C*)"     , [QI "while abbbbc" "val abbbbc"; DC "(*D1*)" "Length"; QI "do abbbbc" "val abbbbc"; AC "(*C*)" "abbbbc"; AC "(*C*)" "aaaaaa"; ]
            "BL10", "let f()  = seq { while abbbbc(*D1*) do abbbbc.[aaaaaa] <- aaa(*C*)"  , [QI "while abbbbc" "val abbbbc"; DC "(*D1*)" "Length"; QI "do abbbbc" "val abbbbc"; AC "(*C*)" "aaaaaa"; ] ]
            
        let knownFailures = 
            [
            ]

        this.WordByWordSystematicTestWithSpecificExpectations(prefix, suffixes, lines, [""], knownFailures) 
             

    [<Test>]
    [<Category("QueryExpressions")>]
    [<Category("Expensive")>]
    [<Ignore("https://github.com/Microsoft/visualfsharp/issues/6166")>]
    /// Incrementally enter query with a 'join' and check for availability of quick info, auto completion and dot completion 
    member this.``QueryAndOtherExpressions.WordByWordSystematicJoinQueryOnSingleLine``() = 

        let prefix =  """
module Test
let abbbbc = [| 1 |]
let aaaaaa = 0
"""
        let suffixes = 
          [ "Empty",                                ""; 
            "ClosingBrace",                         " }"; 
            "ClosingBrace,NextDefinition",        " } \nlet nextDefinition () = 1\n"; 
            "NoClosingBrace,NextDefinition",      " \nlet nextDefinition () = 1\n"; 
            "NoClosingBrace,NextTypeDefinition",  " \ntype NextDefinition() = member x.P = 1\n" 
          ] 
        let lines b = 
          [ "CL1",  "let x = query { for bbbb in abbbbc(*D0*) do join "  ,                                     [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"]
            "CL2",  "let x = query { for bbbb in abbbbc(*D0*) do join cccc "  ,                           [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"]
            "CL2a", "let x = query { for bbbb in abbbbc(*D0*) do join cccc )"  ,                          [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"]
            "CL3",  "let x = query { for bbbb in abbbbc(*D0*) do join cccc in "  ,                        [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"]
            "CL3a", "let x = query { for bbbb in abbbbc(*D0*) do join cccc in )"  ,                       [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"]
            "CL4",  "let x = query { for bbbb in abbbbc(*D0*) do join cccc in abbb(*C*)"  ,               [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length";QI "join" "join"; AC "(*C*)" "abbbbc"]
            "CL4a", "let x = query { for bbbb in abbbbc(*D0*) do join cccc in abbb(*C*) )"  ,             [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length";QI "join" "join"; AC "(*C*)" "abbbbc"]
            "CL5",  "let x = query { for bbbb in abbbbc(*D0*) do join cccc in abbbbc(*D1*)"  ,            [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"]
            "CL5a", "let x = query { for bbbb in abbbbc(*D0*) do join cccc in abbbbc(*D1*) )"  ,          [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"]
            "CL6",  "let x = query { for bbbb in abbbbc(*D0*) do join cccc in abbbbc(*D1*) on "  ,        [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"]
            "CL6a", "let x = query { for bbbb in abbbbc(*D0*) do join cccc in abbbbc(*D1*) on )"  ,       [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"]
            "CL6b", "let x = query { for bbbb in abbbbc(*D0*) do join cccc in abbbbc(*D1*) on (bb(*C*)"  , [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; AC "(*C*)" "bbbb"]
            "CL7",  "let x = query { for bbbb in abbbbc(*D0*) do join cccc in abbbbc(*D1*) on (bbbb"  ,    [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"]
            "CL7a", "let x = query { for bbbb in abbbbc(*D0*) do join cccc in abbbbc(*D1*) on (bbbb )"  ,  [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"]
            "CL8",  "let x = query { for bbbb in abbbbc(*D0*) do join cccc in abbbbc(*D1*) on (bbbb = "  , [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"]
            "CL8a", "let x = query { for bbbb in abbbbc(*D0*) do join cccc in abbbbc(*D1*) on (bbbb = )"  , [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"]
            "CL8b", "let x = query { for bbbb in abbbbc(*D0*) do join cccc in abbbbc(*D1*) on (bbbb = cc(*C*)"  ,                                              [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; AC "(*C*)" "cccc"]
            "CL9",  "let x = query { for bbbb in abbbbc(*D0*) do join cccc in abbbbc(*D1*) on (bbbb(*D11*) = cccc(*D12*)"  ,                                   [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; DC "(*D11*)" "CompareTo"; DC "(*D12*)" "CompareTo"]
            "CL10", "let x = query { for bbbb in abbbbc(*D0*) do join cccc in abbbbc(*D1*) on (bbbb(*D11*) = cccc(*D12*))"  ,                                  [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; DC "(*D11*)" "CompareTo"; DC "(*D12*)" "CompareTo"]
            "CL11", "let x = query { for bbbb in abbbbc(*D0*) do join cccc in abbbbc(*D1*) on (bbbb(*D11*) = cccc(*D12*)); "  ,                                [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; DC "(*D11*)" "CompareTo"; DC "(*D12*)" "CompareTo"]
            "CL12", "let x = query { for bbbb in abbbbc(*D0*) do join cccc in abbbbc(*D1*) on (bbbb(*D11*) = cccc(*D12*)); select"  ,                          [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; DC "(*D11*)" "CompareTo"; DC "(*D12*)" "CompareTo"]
            "CL13", "let x = query { for bbbb in abbbbc(*D0*) do join cccc in abbbbc(*D1*) on (bbbb(*D11*) = cccc(*D12*)); select (bb(*C*)"  ,                 [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; DC "(*D11*)" "CompareTo"; DC "(*D12*)" "CompareTo"]
            "CL14", "let x = query { for bbbb in abbbbc(*D0*) do join cccc in abbbbc(*D1*) on (bbbb(*D11*) = cccc(*D12*)); select (bbbb(*D2*)"  ,              [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; DC "(*D2*)" "CompareTo"; DC "(*D11*)" "CompareTo"; DC "(*D12*)" "CompareTo"]
            "CL15", "let x = query { for bbbb in abbbbc(*D0*) do join cccc in abbbbc(*D1*) on (bbbb(*D11*) = cccc(*D12*)); select (bbbb(*D2*), "  ,            [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; DC "(*D2*)" "CompareTo"; DC "(*D11*)" "CompareTo"; DC "(*D12*)" "CompareTo"]
            "CL16", "let x = query { for bbbb in abbbbc(*D0*) do join cccc in abbbbc(*D1*) on (bbbb(*D11*) = cccc(*D12*)); select (bbbb(*D2*), cc(*C*)"  ,     [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; DC "(*D2*)" "CompareTo"; AC "(*C*)" "cccc"; DC "(*D11*)" "CompareTo"; DC "(*D12*)" "CompareTo"]
            "CL17", "let x = query { for bbbb in abbbbc(*D0*) do join cccc in abbbbc(*D1*) on (bbbb(*D11*) = cccc(*D12*)); select (bbbb(*D2*), cccc(*D3*)"  ,  [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; DC "(*D2*)" "CompareTo"; DC "(*D3*)"  "CompareTo"; QI "(bbbb" "val bbbb"; QI ", cccc" "val cccc"; DC "(*D11*)" "CompareTo"; DC "(*D12*)" "CompareTo" ]
            "CL18", "let x = query { for bbbb in abbbbc(*D0*) do join cccc in abbbbc(*D1*) on (bbbb(*D11*) = cccc(*D12*)); select (bbbb(*D2*), cccc(*D3*))"  , [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; DC "(*D2*)" "CompareTo"; DC "(*D3*)" "CompareTo"; QI "(bbbb" "val bbbb"; QI ", cccc" "val cccc"; DC "(*D11*)" "CompareTo"; DC "(*D12*)" "CompareTo" ] ]
            
        let knownFailures = 
             [
             ]

        this.WordByWordSystematicTestWithSpecificExpectations(prefix, suffixes, lines, [""], knownFailures) 

    [<Test>]
    /// This is a sanity check that the multiple-line case is much the same as the single-line cae
    [<Category("QueryExpressions")>]
    [<Category("Expensive")>]
    [<Ignore("https://github.com/Microsoft/visualfsharp/issues/6166")>]
    member this.``QueryAndOtherExpressions.WordByWordSystematicJoinQueryOnMultipleLine``() = 

        let prefix =  """
module Test
let abbbbc = [| 1 |]
let aaaaaa = 0
"""
        let suffixes = 
          [ "Empty",                                ""; 
            "ClosingBrace",                         " }"; 
            "ClosingBrace,NextDefinition",        " } \nlet nextDefinition () = 1\n"; 
            "NoClosingBrace,NextDefinition",      " \nlet nextDefinition () = 1\n"; 
            "NoClosingBrace,NextTypeDefinition",  " \ntype NextDefinition() = member x.P = 1\n" 
          ] 
        let lines b = 
          [ "DL1",  """
let x = query { for bbbb in abbbbc(*D0*) do 
join 
"""                                                 ,  [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"]
            "DL2",  """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc 
"""                                                 , [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"]

            "DL2a", """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc )
"""                                                 , [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"]

            "DL3",  """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in 
"""                                                 , [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"]

            "DL3a", """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in )
"""                                                 , [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"]

            "DL4",  """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in abbb(*C*)
"""                                                 , [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length";QI "join" "join"; AC "(*C*)" "abbbbc"]

            "DL4a", """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in abbb(*C*) )
"""                                                 , [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length";QI "join" "join"; AC "(*C*)" "abbbbc"]

            "DL5",  """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in abbbbc(*D1*)
"""                                                 , [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"]

            "DL5a", """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in abbbbc(*D1*) )
"""                                                ,  [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"]

            "DL6",  """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in abbbbc(*D1*) on 
"""                                                ,  [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"]

            "L6a", """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in abbbbc(*D1*) on )
"""                                                ,  [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"]
            "L6b", """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in abbbbc(*D1*) on (bb(*C*)
"""                                                , [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; AC "(*C*)" "bbbb"]
            "DL7",  """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in abbbbc(*D1*) on (bbbb
"""                                                ,    [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"]
            "DL7a", """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in abbbbc(*D1*) on (bbbb )
"""                                                ,  [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"]
            "DL8",  """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in abbbbc(*D1*) on (bbbb = 
"""                                                , [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"]
            "DL8a", """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in abbbbc(*D1*) on (bbbb = )
"""                                                , [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"]
            "DL8b", """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in abbbbc(*D1*) on (bbbb = cc(*C*)
"""                                                , [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; AC "(*C*)" "cccc"]
            "DL9",  """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in abbbbc(*D1*) on (bbbb(*D11*) = cccc(*D12*)
"""                                                , [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; DC "(*D11*)" "CompareTo"; DC "(*D12*)" "CompareTo"]
            "DL10", """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in abbbbc(*D1*) on (bbbb(*D11*) = cccc(*D12*))
"""                                               ,  [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; DC "(*D11*)" "CompareTo"; DC "(*D12*)" "CompareTo"]
            "DL11", """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in abbbbc(*D1*) on (bbbb(*D11*) = cccc(*D12*)); 
"""                                               , [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; DC "(*D11*)" "CompareTo"; DC "(*D12*)" "CompareTo"]
            "DL12", """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in abbbbc(*D1*) on (bbbb(*D11*) = cccc(*D12*)); select
"""                                               ,  [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; DC "(*D11*)" "CompareTo"; DC "(*D12*)" "CompareTo"]
            "DL13", """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in abbbbc(*D1*) on (bbbb(*D11*) = cccc(*D12*)); select (bb(*C*)
"""                                               ,  [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; DC "(*D11*)" "CompareTo"; DC "(*D12*)" "CompareTo"]
            "DL14", """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in abbbbc(*D1*) on (bbbb(*D11*) = cccc(*D12*)); select (bbbb(*D2*)
"""                                              , [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; DC "(*D2*)" "CompareTo"; DC "(*D11*)" "CompareTo"; DC "(*D12*)" "CompareTo"]
            "DL15", """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in abbbbc(*D1*) on (bbbb(*D11*) = cccc(*D12*)); select (bbbb(*D2*), 
"""                                               , [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; DC "(*D2*)" "CompareTo"; DC "(*D11*)" "CompareTo"; DC "(*D12*)" "CompareTo"]
            "DL16", """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in abbbbc(*D1*) on (bbbb(*D11*) = cccc(*D12*)); select (bbbb(*D2*), cc(*C*)
"""                                               ,  [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; DC "(*D2*)" "CompareTo"; AC "(*C*)" "cccc"; DC "(*D11*)" "CompareTo"; DC "(*D12*)" "CompareTo"]
            "DL17", """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in abbbbc(*D1*) on (bbbb(*D11*) = cccc(*D12*)); select (bbbb(*D2*), cccc(*D3*)
"""                                               ,  [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; DC "(*D2*)" "CompareTo"; DC "(*D3*)"  "CompareTo"; QI "(bbbb" "val bbbb"; QI ", cccc" "val cccc"; DC "(*D11*)" "CompareTo"; DC "(*D12*)" "CompareTo" ]
            "DL18", """
let x = query { for bbbb in abbbbc(*D0*) do 
                join cccc in abbbbc(*D1*) on (bbbb(*D11*) = cccc(*D12*)); select (bbbb(*D2*), cccc(*D3*))
"""                                              , [QI "for bbbb" "val bbbb"; QI "in abbbbc" "val abbbbc"; DC "(*D0*)" "Length"; QI "join" "join"; DC "(*D1*)" "Length"; DC "(*D2*)" "CompareTo"; DC "(*D3*)" "CompareTo"; QI "(bbbb" "val bbbb"; QI ", cccc" "val cccc"; DC "(*D11*)" "CompareTo"; DC "(*D12*)" "CompareTo" ] ]

        let knownFailures = 

              [
                 //("DL2", "NoClosingBrace,NextDefinition", "", QuickInfoExpected ("for bbbb","val bbbb")) 
                 //("DL2", "NoClosingBrace,NextDefinition", "", QuickInfoExpected ("in abbbbc","val abbbbc")) 
                 //("DL2", "NoClosingBrace,NextDefinition", "", DotCompleteExpected ("(*D0*)","Length")) 
                 //("DL2", "NoClosingBrace,NextDefinition", "", QuickInfoExpected ("join","join")) 
              ]


        this.WordByWordSystematicTestWithSpecificExpectations(prefix, suffixes, lines, [""], knownFailures) 

    [<Test>]
    /// This is the case where (*TYPING*) nothing has been typed yet and we invoke the completion list
    /// This is a known failure right now for some of the example files above.
    member this.``QueryExpression.CtrlSpaceSystematic2``() = 
         for fileContents in this.QueryExpressionFileExamples() do
           
           try 
               this.VerifyCtrlSpaceListContainAllAtStartOfMarker(
                  fileContents = fileContents,
                  marker = "(*TYPING*)",
                  list = customOperations,
                  addtlRefAssy=standard40AssemblyRefs )
           with _ -> 
               printfn "FAILURE on systematic test: fileContents = <<<%s>>>" fileContents
               reraise()



    (* Various parser recovery test cases -------------------------------------------------- *)

//*****************Helper Function*****************
    member public this.AutoCompleteRecoveryTest(source : list<string>, marker, expected) =
        let (_, _, file) = this.CreateSingleFileProject(source)
        MoveCursorToEndOfMarker(file, marker)
        let completions = time1 CtrlSpaceCompleteAtCursor file "Time of first autocomplete."
        AssertCompListContainsAll(completions, expected)
            
    [<Test>]
    member public this.``Parameter.CommonCase.Bug2884``() =     
        this.AutoCompleteRecoveryTest
            ([ 
               "type T1(aaa1) ="
               "  do (" ], "do (", [ "aaa1" ])

    [<Test>]
    member public this.``Parameter.SubsequentLet.Bug2884``() =     
        this.AutoCompleteRecoveryTest
            ([ 
               "type T1(aaa1) ="
               "  do ("
               "let a = 0" ], "do (", [ "aaa1" ]) 
                      
    [<Test>]
    member public this.``Parameter.SubsequentMember.Bug2884``() =     
        this.AutoCompleteRecoveryTest
            ([ 
               "type T1(aaa1) ="
               "  member x.Foo(aaa2) = "
               "    do ("
               "  member x.Bar = 0" ], "do (", [ "aaa1"; "aaa2" ])        

    [<Test>]
    member public this.``Parameter.System.DateTime.Bug2884``() =     
        this.AutoCompleteRecoveryTest
            ([ 
               "type T1(aaa1) ="
               "  member x.Foo(aaa2) = "
               "    let dt = new System.DateTime(" ], "Time(", [ "aaa1"; "aaa2" ])        

    [<Test>]
    member public this.``Parameter.DirectAfterDefined.Bug2884``() =     
        this.AutoCompleteRecoveryTest
            ([  
               "if true then"
               "  let aaa1 = 0"
               "  (" ], "(", [ "aaa1" ])

    [<Test>]
    member public this.``NotShowInfo.LetBinding.Bug3602``() =  
        this.VerifyAutoCompListIsEmptyAtEndOfMarker(
            fileContents = "let s. = \"Hello world\"
                            ()",
            marker = "let s.")   
    
    [<Test>]
    member public this.``NotShowInfo.FunctionParameter.Bug3602``() = 
        this.VerifyAutoCompListIsEmptyAtEndOfMarker(
            fileContents = "let foo s. = s + \"Hello world\"
                            ()",
            marker = "let foo s.") 
                                               
    [<Test>]
    member public this.``NotShowInfo.ClassMemberDeclA.Bug3602``() =     
        this.TestCompletionNotShowingWhenFastUpdate
            [ 
              "type Foo() ="
              "    member this.Func (x, y) = ()"
              "    member (*marker*) this.Prop = 10"
              "()" ]
            [  
              "type Foo() ="
              "    member this.Func (x, y) = ()"
              "    member (*marker*) this."
              "()" ]        
            "(*marker*) this."
                                            
    // Another test case for the same thing - this goes through a different code path
    [<Test>]
    member public this.``NotShowInfo.ClassMemberDeclB.Bug3602``() =     
        this.TestCompletionNotShowingWhenFastUpdate
            [  
              "type Foo() ="
              "    member this.Func (x, y) = ()"
              "    //   marker$" // <- trick to move the cursor to the right location before source replacement
              "()" ] 
            [  
              "type Foo() ="
              "    member this.Func (x, y) = ()"
              "    member this."
              "()" ]        
            "marker$"

    [<Test>]
    member public this.``ComputationExpression.LetBang``() =     
        AssertAutoCompleteContainsNoCoffeeBreak
            ["let http(url:string) = "
             "  async { "
             "    let rnd = new System.Random()"
             "    let! rsp = rnd.N" ]
            "rsp = rnd."
            ["Next"]
            []   

   (* Tests for autocomplete -------------------------------------------------------------- *)
                     
    member public this.TestGenericAutoComplete(line, expected) =
        let code = 
         [  "type GT<'a> ="
            "  static member P = 12"
            "  static member Q = 13"
            "type GT2 ="
            "  static member R = 12"
            "  static member S = 13"
            "type D = | DD"
            "let td = typeof<D>"
            "let f i = typeof<D>"
            ""; line ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)
        MoveCursorToEndOfMarker(file, line)
        let completions = AutoCompleteAtCursor file
        AssertCompListContainsAll(completions, expected)
        gpatcc.AssertExactly(0,0)

    [<Test>]
    member public this.``Generics.Typeof``() =        
        this.TestGenericAutoComplete ("let _ = typeof<int>.", [ "Assembly"; "AssemblyQualifiedName"; (* ... *) ])

    [<Test>]
    member public this.``Generics.NonGenericTypeMembers``() =        
        this.TestGenericAutoComplete ("let _ = GT2.", [ "R"; "S" ])

    [<Test>]
    member public this.``Generics.GenericTypeMembers``() =        
        this.TestGenericAutoComplete ("let _ = GT<int>.", [ "P"; "Q" ])

   //[<Test>]    // keep disabled unless trying to prove that UnhandledExceptionHandler is working
    member public this.EnsureThatUnhandledExceptionsCauseAnAssert() =
        // Do something that causes LanguageService to load
        AssertAutoCompleteContains 
          [ 
            "type FooBuilder() ="
            "   member x.Return(a) = new System.Random()"
            "let foo = FooBuilder()"
            "(foo { return 0 })." ]
          "})."       // marker
          [ "Next" ] // should contain
          [ "GetEnumerator" ] // should not contain
        // kaboom
        let t = new System.Threading.Thread(new System.Threading.ThreadStart(fun () -> failwith "foo"))
        t.Start()
        System.Threading.Thread.Sleep(1000)

    [<Test>]
    member public this.``DotBetweenParens.Bug175360.Case1``() =
        AssertAutoCompleteContains 
            [
                "let a = 10."
                "(a :> System.IConvertible).(null) |> ignore" ]
            "ible)."
            [ "ToDecimal" ] // should contain
            [] // should not contain

    [<Test>]
    member public this.``DotBetweenParens.Bug175360.Case2``() =
        AssertAutoCompleteContains 
            [ "[| 1 |].(0)" ]
            "|]."
            [ "Clone" ] // should contain
            [] // should not contain

    [<Test>]
    [<Ignore("Not worth fixing right now")>]
    member public this.``GenericType.Self.Bug69673_1.01``() =    
        AssertCtrlSpaceCompleteContains
            ["type Base(o:obj) = class end"
             "type Foo() as this ="
             "    inherit Base(this) // this"
             "    let o = this // this ok"
             "    do this.Bar() // this ok, dotting ok"
             "    member this.Bar() = ()" ]
            "Base(th"
            ["this"]
            []

    [<Test>]
    member public this.``GenericType.Self.Bug69673_1.02``() =    
        AssertCtrlSpaceCompleteContains
            ["type Base(o:obj) = class end"
             "type Foo() as this ="
             "    inherit Base(this) // this"
             "    let o = this // this ok"
             "    do this.Bar() // this ok, dotting ok"
             "    member this.Bar() = ()" ]
            "o = th"
            ["this"]
            []

    [<Test>]
    member public this.``GenericType.Self.Bug69673_1.03``() =    
        AssertCtrlSpaceCompleteContains
            ["type Base(o:obj) = class end"
             "type Foo() as this ="
             "    inherit Base(this) // this"
             "    let o = this // this ok"
             "    do this.Bar() // this ok, dotting ok"
             "    member this.Bar() = ()" ]
            "do th"
            ["this"]
            []

    [<Test>]
    member public this.``GenericType.Self.Bug69673_1.04``() =    
        AssertAutoCompleteContains
            ["type Base(o:obj) = class end"
             "type Foo() as this ="
             "    inherit Base(this) // this"
             "    let o = this // this ok"
             "    do this.Bar() // this ok, dotting ok"
             "    member this.Bar() = ()" ]
            "do this."
            ["Bar"]
            []

    [<Test>]
    [<Ignore("this is not worth fixing")>]
    member public this.``GenericType.Self.Bug69673_2.1``() =  
        AssertAutoCompleteContains
            ["type Base(o:obj) = class end"
             "type Food() as this ="
             "    class"
             "    inherit Base(this) // this"
             "    do"
             "        this |> ignore // this (only repros with explicit class/end)"
             "    end" ]  
            "Base(th"  
            ["this"] 
            []       
            
    [<Test>]
    [<Ignore("this is not worth fixing")>]
    member public this.``GenericType.Self.Bug69673_2.2``() =  
        AssertAutoCompleteContains
            ["type Base(o:obj) = class end"
             "type Food() as this ="
             "    class"
             "    inherit Base(this) // this"
             "    do"
             "        this |> ignore // this (only repros with explicit class/end)"
             "    end" ]  
            "     th"  
            ["this"] 
            []                       

    [<Test>]
    member public this.``UnitMeasure.Bug78932_1``() =        
        AssertAutoCompleteContains 
          [ @"
            module M1 =
               [<Measure>] type Kg
 
            module M2 = 
                let f = 1<M1. >  // <- type . between M1 and ' >'   => works" ]
          "M1."       // marker
          [ "Kg" ] // should contain
          [  ] // should not contain

    [<Test>]
    member public this.``UnitMeasure.Bug78932_2``() =        
        // Note: in this case, pressing '.' does not automatically pop up a completion list in VS, but ctrl-space does get the right list
        // This is just like how
        //     let y = true.>"trueSuffix"   // no popup on dot, but ctrl-space brings up list with ToString that is legal completion
        // works, the issue is ".>" is seen as an operator and not a dot-for-completion.
        AssertAutoCompleteContains 
          [ @"
            module M1 =
               [<Measure>] type Kg
 
            module M2 = 
                let f = 1<M1.>  // <- type . between M1 and '>'     => no popup intellisense" ]
          "M1."       // marker
          [ "Kg" ] // should contain
          [ ] // should not contain

    [<Test>]
    member public this.``Array.AfterOperator...Bug65732_A``() =        
        AssertAutoCompleteContains 
          [ "let r = [1 .. System.Int32.MaxValue]" ]
          "System."       // marker
          [ "Int32" ] // should contain
          [ "abs" ] // should not contain (from top level)

    [<Test>]
    member public this.``Array.AfterOperator...Bug65732_B``() =        
        AssertCtrlSpaceCompleteContains 
          [ "let r = [System.Int32.MaxValue..42]" ]
          ".."       // marker
          [ "abs" ] // should contain (top level)
          [ "CompareTo" ] // should not contain (from Int32)

    // Verify the auto completion after the close-parentheses,
    // there should be auto completion
    [<Test>]
    member public this.``Array.AfterParentheses.Bug175360``() =        
        AssertAutoCompleteContainsNoCoffeeBreak 
          [ "let a = 10."
            "let r = (a :> System.IConvertible).(null)" ]
          "IConvertible)."       // marker
          [ "ToDecimal" ] // should contain (top level)
          [ ] // should not contain 

    [<Test>]
    member public this.``Identifier.FuzzyDefiend.Bug67133``() =  
        AssertAutoCompleteContainsNoCoffeeBreak
          [ "let gDateTime (arr: System.DateTime[]) ="
            "    arr.[0]." ]
          "arr.[0]."
          ["AddDays"]
          []

    [<Test>]
    member public this.``Identifier.FuzzyDefiend.Bug67133.Negative``() =        
        let code = [ "let gDateTime (arr: DateTime[]) ="  // Note: no 'open System', so DateTime is unknown
                     "    arr.[0]." ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        TakeCoffeeBreak(this.VS)
        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)
        MoveCursorToEndOfMarker(file, "arr.[0].")
        let completions = AutoCompleteAtCursor file
        AssertCompListContainsExactly(completions, []) // we don't want any completions on <expr>. when <expr> has unknown type due to errors
        // (In particular, we don't want the "didn't find any completions, so just show top-level entities like 'abs' here" logic to kick in.)

    [<Test>]
    member public this.``Class.Property.Bug69150_A``() =        
        AssertCtrlSpaceCompleteContains 
          [ "type ClassType(x : int) ="
            "    member this.Value = x"
            "let z = (new ClassType(23)).Value" ]
          "))."       // marker
          [ "Value" ] // should contain
          [ "CompareTo" ] // should not contain (from Int32)

    [<Test>]
    member public this.``Class.Property.Bug69150_B``() =        
        AssertCtrlSpaceCompleteContains 
          [ "type ClassType(x : int) ="
            "    member this.Value = x"
            "let z = ClassType(23).Value" ]
          "3)."       // marker
          [ "Value" ] // should contain
          [ "CompareTo" ] // should not contain (from Int32)

    [<Test>]
    member public this.``Class.Property.Bug69150_C``() =        
        AssertCtrlSpaceCompleteContains 
          [ "type ClassType(x : int) ="
            "    member this.Value = x"
            "let f x = new ClassType(x)"
            "let z = f(23).Value" ]
          "3)."       // marker
          [ "Value" ] // should contain
          [ "CompareTo" ] // should not contain (from Int32)

    [<Test>]
    member public this.``Class.Property.Bug69150_D``() =        
        AssertCtrlSpaceCompleteContains 
          [ "type ClassType(x : int) ="
            "    member this.Value = x"
            "let z = ClassType(23).Value" ]
          "3).V"       // marker
          [ "Value" ] // should contain
          [ "VolatileFieldAttribute" ] // should not contain (from top-level)

    [<Test>]
    member public this.``Class.Property.Bug69150_E``() =        
        AssertCtrlSpaceCompleteContains 
          [ "type ClassType(x : int) ="
            "    member this.Value = x"
            "let z = ClassType(23)   .   Value" ]
          "3)   . "       // marker
          [ "Value" ] // should contain
          [ "VolatileFieldAttribute" ] // should not contain (from top-level)

    [<Test>]
    member public this.``AssignmentToProperty.Bug231283``() =        
        AssertCtrlSpaceCompleteContains 
            ["""
                type Foo() =
                    member val Bar = 0 with get,set

                let f = new Foo()
                f.Bar <-
                    let xyz = 42 (*Mark*)
                    xyz """]
            "42 "
            [ "AbstractClassAttribute" ] // top-level completions
            [ "Bar" ] // not stuff from the lhs of assignment

    [<Test>]
    member public this.``Dot.AfterOperator.Bug69159``() =        
        AssertAutoCompleteContains 
          [ "let x1 = [|0..1..10|]." ]
          "]."       // marker
          [ "Length" ] // should contain (array)
          [ "abs" ] // should not contain (top-level)

    [<Test>]
    member public this.``Residues1``() =        
        AssertCtrlSpaceCompleteContains 
          [ "System   .   Int32   .   M" ]
          "M"       // marker
          [ "MaxValue"; "MinValue" ] // should contain
          [ "MailboxProcessor"; "Map" ] // should not contain (top-level)

    [<Test>]
    member public this.``Residues2``() =        
        AssertCtrlSpaceCompleteContains 
          [ "let x = 42"
            "x   .  C" ]
          "C"       // marker
          [ "CompareTo" ] // should contain (Int32)
          [ "CLIEventAttribute"; "Checked"; "Choice" ] // should not contain (top-level)

    [<Test>]
    member public this.``Residues3``() =        
        AssertCtrlSpaceCompleteContains 
          [ "let x = 42"
            "x   .  " ]
          ".  "       // marker
          [ "CompareTo" ] // should contain (Int32)
          [ "CLIEventAttribute"; "Checked"; "Choice" ] // should not contain (top-level)

    [<Test>]
    member public this.``Residues4``() =        
        AssertCtrlSpaceCompleteContains 
          [ "let x = 42"
            "id(x)   .  C" ]
          "C"       // marker
          [ "CompareTo" ] // should contain (Int32)
          [ "CLIEventAttribute"; "Checked"; "Choice" ] // should not contain (top-level)

    [<Test>]
    member public this.``CtrlSpaceInWhiteSpace.Bug133112``() =        
        AssertCtrlSpaceCompleteContains 
          [ """
            type Foo = 
                static member A = 1
                static member B = 2
 
            printfn "%d %d" Foo.A  """ ]
          "Foo.A "       // marker
          [ "AbstractClassAttribute" ] // should contain (top-level)
          [ "A"; "B" ] // should not contain (Foo)

    [<Test>]
    member public this.``Residues5``() =        
        AssertCtrlSpaceCompleteContains 
          [ "let x = 42"
            "id(x)   .  " ]
          ".  "       // marker
          [ "CompareTo" ] // should contain (Int32)
          [ "CLIEventAttribute"; "Checked"; "Choice" ] // should not contain (top-level)
    [<Test>]
    member public this.``CompletionInDifferentEnvs1``() = 
        AssertCtrlSpaceCompleteContains
            ["let f1 num ="
             "    let rec completeword d ="
             "        d + d"
             "(**)comple"]
            "(**)comple" // marker
            ["completeword"] // should contain
            [""]

    [<Test>]
    member public this.``CompletionInDifferentEnvs2``() = 
        AssertCtrlSpaceCompleteContains
            ["let aaa = 1"
             "let aab = 2"
             "(aa"
             "let aac = 3"]
             "(aa"
            ["aaa"; "aab"]
            ["aac"] 

    [<Test>]
    member public this.``CompletionInDifferentEnvs3``() = 
        AssertCtrlSpaceCompleteContains
            ["let mb1 = new MailboxProcessor<AsyncReplyChannel<int>>(fun inbox -> async { let! msg = inbox.Receive()"
             "                                                                            do "]
             "do "
            ["msg"]
            [] 

    [<Test>]
    member public this.``CompletionInDifferentEnvs4``() = 
        AssertCtrlSpaceCompleteContains
            ["async {"
             "    let! x = i"
             "    ("
             "}"]
             "("
            ["x"]
            [] 

        AssertCtrlSpaceCompleteContains
            ["let q = "
             "    let a = 20"
             "    let b = (fun i -> i) 40"
             "    (("]
             "(("
            ["b"]
            ["i"]

    [<Test>]
    member public this.``CompletionForAndBang_BaseLine0``() = 
        AssertCtrlSpaceCompleteContains
            ["type Builder() ="
             "    member x.Bind(a: 'T1, f: 'T1 -> 'T2) = f a"
             "    member x.Return(a: 'T) = a"
             "let builder = Builder()"
             "builder {"
             "    let! xxx3 = 2"
             "    return x"
             "}"]
             "    return x"
            ["xxx3"]
            [] 

    [<Test>]
    member public this.``CompletionForAndBang_BaseLine1``() = 
        AssertCtrlSpaceCompleteContains
            ["type Builder() ="
             "    member x.Bind(a: 'T1, f: 'T1 -> 'T2) = f a"
             "    member x.Return(a: 'T) = a"
             "let builder = Builder()"
             "let xxx1 = 1"
             "builder {"
             "   let xxx2 = 1"
             "   let! xxx3 = 1"
             "   return (1 + x)"
             "}"]
             "   return (1 + x"
            ["xxx1"; "xxx2"; "xxx3"]
            [] 

    [<Test>]
    member public this.``CompletionForAndBang_BaseLine2``() = 
        /// Without closing '}'
        AssertCtrlSpaceCompleteContains
            ["type Builder() ="
             "    member x.Bind(a: 'T1, f: 'T1 -> 'T2) = f a"
             "    member x.Return(a: 'T) = a"
             "let builder = Builder()"
             "let yyy1 = 1"
             "builder {"
             "   let yyy2 = 1"
             "   let! yyy3 = 1"
             "   return (1 + y)"]
             "   return (1 + y"
            ["yyy1"; "yyy2"; "yyy3"]
            [] 

    [<Test>]
    member public this.``CompletionForAndBang_BaseLine3``() = 
        /// Without closing ')'
        AssertCtrlSpaceCompleteContains
            ["type Builder() ="
             "    member x.Bind(a: 'T1, f: 'T1 -> 'T2) = f a"
             "    member x.Return(a: 'T) = a"
             "let builder = Builder()"
             "let zzz1 = 1"
             "builder {"
             "   let zzz2 = 1"
             "   let! zzz3 = 1"
             "   return (1 + z" ]
             "   return (1 + z"
            ["zzz1"; "zzz2"; "zzz3"]
            [] 

    [<Test>]
    member public this.``CompletionForAndBang_BaseLine4``() = 
        AssertCtrlSpaceCompleteContains
            ["type Builder() ="
             "    member x.Bind(a: 'T1, f: 'T1 -> 'T2) = f a"
             "    member x.Return(a: 'T) = a"
             "let builder = Builder()"
             "let zzz1 = 1"
             "builder {"
             "   let! zzz3 = 1"
             "   return (1 + z" ]
             "   return (1 + z"
            ["zzz1"; "zzz3"]
            [] 

    [<Test>]
    member public this.``CompletionForAndBang_Test_MergeSources_Bind_Return0``() = 
        AssertCtrlSpaceCompleteContainsWithOtherFlags
            "/langversion:preview"
            ["type Builder() ="
             "    member x.MergeSources(a: 'T1, b: 'T2) = (a, b)"
             "    member x.Bind(a: 'T1, f: 'T1 -> 'T2) = f a"
             "    member x.Return(a: 'T) = a"
             "let builder = Builder()"
             "builder {"
             "    let! xxx3 = 2"
             "    and! xxx4 = 2"
             "    return x"
             "}"]
             "    return x"
            ["xxx3"; "xxx4"]
            [] 

    [<Test>]
    member public this.``CompletionForAndBang_Test_MergeSources_Bind_Return1``() = 
        AssertCtrlSpaceCompleteContainsWithOtherFlags
            "/langversion:preview"
            ["type Builder() ="
             "    member x.MergeSources(a: 'T1, b: 'T2) = (a, b)"
             "    member x.Bind(a: 'T1, f: 'T1 -> 'T2) = f a"
             "    member x.Return(a: 'T) = a"
             "let builder = Builder()"
             "let xxx1 = 1"
             "builder {"
             "   let xxx2 = 1"
             "   let! xxx3 = 1"
             "   and! xxx4 = 1"
             "   return (1 + x)"
             "}"]
             "   return (1 + x"
            ["xxx1"; "xxx2"; "xxx3"; "xxx4"]
            [] 

    [<Test>]
    member public this.``CompletionForAndBang_Test_MergeSources_Bind_Return2``() = 
        AssertCtrlSpaceCompleteContainsWithOtherFlags
            "/langversion:preview"
            ["type Builder() ="
             "    member x.MergeSources(a: 'T1, b: 'T2) = (a, b)"
             "    member x.Bind(a: 'T1, f: 'T1 -> 'T2) = f a"
             "    member x.Return(a: 'T) = a"
             "let builder = Builder()"
             "let yyy1 = 1"
             "builder {"
             "   let yyy2 = 1"
             "   let! yyy3 = 1"
             "   and! yyy4 = 1"
             "   return (1 + y)"]
             "   return (1 + y"
            ["yyy1"; "yyy2"; "yyy3"; "yyy4"]
            [] 

    [<Test>]
    member public this.``CompletionForAndBang_Test_MergeSources_Bind_Return3``() = 
        AssertCtrlSpaceCompleteContainsWithOtherFlags
            "/langversion:preview"
            ["type Builder() ="
             "    member x.MergeSources(a: 'T1, b: 'T2) = (a, b)"
             "    member x.Bind(a: 'T1, f: 'T1 -> 'T2) = f a"
             "    member x.Return(a: 'T) = a"
             "let builder = Builder()"
             "let zzz1 = 1"
             "builder {"
             "   let zzz2 = 1"
             "   let! zzz3 = 1"
             "   and! zzz4 = 1"
             "   return (1 + z" ]
             "   return (1 + z"
            ["zzz1"; "zzz2"; "zzz3"; "zzz4"]
            [] 

    [<Test>]
    member public this.``CompletionForAndBang_Test_MergeSources_Bind_Return4``() = 
        AssertCtrlSpaceCompleteContainsWithOtherFlags
            "/langversion:preview"
            ["type Builder() ="
             "    member x.MergeSources(a: 'T1, b: 'T2) = (a, b)"
             "    member x.Bind(a: 'T1, f: 'T1 -> 'T2) = f a"
             "    member x.Return(a: 'T) = a"
             "let builder = Builder()"
             "let zzz1 = 1"
             "builder {"
             "   let! zzz3 = 1"
             "   and! zzz4 = 1"
             "   return (1 + z" ]
             "   return (1 + z"
            ["zzz1"; "zzz3"; "zzz4"]
            [] 

    [<Test>]
    member public this.``CompletionForAndBang_Test_Bind2Return0``() = 
        AssertCtrlSpaceCompleteContainsWithOtherFlags
            "/langversion:preview"
            ["type Builder() ="
             "    member x.Bind2Return(a: 'T1, b: 'T2, f: ('T1 * 'T2) -> 'T3) = f (a, b)"
             "let builder = Builder()"
             "builder {"
             "    let! xxx3 = 2"
             "    and! xxx4 = 2"
             "    return x"
             "}"]
             "    return x"
            ["xxx3"; "xxx4"]
            [] 

    [<Test>]
    member public this.``CompletionForAndBang_Test_Bind2Return1``() = 
        AssertCtrlSpaceCompleteContainsWithOtherFlags
            "/langversion:preview"
            ["type Builder() ="
             "    member x.Bind2Return(a: 'T1, b: 'T2, f: ('T1 * 'T2) -> 'T3) = f (a, b)"
             "let builder = Builder()"
             "let xxx1 = 1"
             "builder {"
             "   let xxx2 = 1"
             "   let! xxx3 = 1"
             "   and! xxx4 = 1"
             "   return (1 + x)"
             "}"]
             "   return (1 + x"
            ["xxx1"; "xxx2"; "xxx3"; "xxx4"]
            [] 

    [<Test>]
    member public this.``CompletionForAndBang_Test_Bind2Return2``() = 
        AssertCtrlSpaceCompleteContainsWithOtherFlags
            "/langversion:preview"
            ["type Builder() ="
             "    member x.Bind2Return(a: 'T1, b: 'T2, f: ('T1 * 'T2) -> 'T3) = f (a, b)"
             "let builder = Builder()"
             "let yyy1 = 1"
             "builder {"
             "   let yyy2 = 1"
             "   let! yyy3 = 1"
             "   and! yyy4 = 1"
             "   return (1 + y)"]
             "   return (1 + y"
            ["yyy1"; "yyy2"; "yyy3"; "yyy4"]
            [] 

    [<Test>]
    member public this.``CompletionForAndBang_Test_Bind2Return3``() = 
        AssertCtrlSpaceCompleteContainsWithOtherFlags
            "/langversion:preview"
            ["type Builder() ="
             "    member x.Bind2Return(a: 'T1, b: 'T2, f: ('T1 * 'T2) -> 'T3) = f (a, b)"
             "let builder = Builder()"
             "let zzz1 = 1"
             "builder {"
             "   let zzz2 = 1"
             "   let! zzz3 = 1"
             "   and! zzz4 = 1"
             "   return (1 + z" ]
             "   return (1 + z"
            ["zzz1"; "zzz2"; "zzz3"; "zzz4"]
            [] 

    [<Test>]
    member public this.``CompletionForAndBang_Test_Bind2Return4``() = 
        AssertCtrlSpaceCompleteContainsWithOtherFlags
            "/langversion:preview"
            ["type Builder() ="
             "    member x.Bind2Return(a: 'T1, b: 'T2, f: ('T1 * 'T2) -> 'T3) = f (a, b)"
             "let builder = Builder()"
             "let zzz1 = 1"
             "builder {"
             "   let! zzz3 = 1"
             "   and! zzz4 = 1"
             "   return (1 + z" ]
             "   return (1 + z"
            ["zzz1"; "zzz3"; "zzz4"]
            [] 

            (**)
    [<Test>]
    member public this.``Bug229433.AfterMismatchedParensCauseWeirdParseTreeAndExceptionDuringTypecheck``() =        
        AssertAutoCompleteContains [ """
            type T() =
                member this.Bar() = ()
                member val X = "foo" with get,set
                static member Id(x) = x
 
            [1]
            |> Seq.iter (fun x -> 
                let user = x
                ["foo"]
                |> List.iter (fun m -> 
                    let xyz = new T()
                    xyz.X <- null
                    T.Id((*here*)xyz.  // no intellisense here after .
                    )
                printfn ""
                )  """ ]
            "(*here*)xyz."
            [ "Bar"; "X" ]
            []

    [<Test>]
    member public this.``Bug130733.LongIdSet.CtrlSpace``() =        
        AssertCtrlSpaceCompleteContains [ """
            type C() =
                member this.XX with get() = 4 and set(x) = ()

            let c = C()
            c.X <- 42""" ]
            "c.X"
            [ "XX" ]
            []

    [<Test>]
    member public this.``Bug130733.LongIdSet.Dot``() =        
        AssertAutoCompleteContains [ """
            type C() =
                member this.XX with get() = 4 and set(x) = ()

            let c = C()
            c.X <- 42""" ]
            "c."
            [ "XX" ]
            []

    [<Test>]
    member public this.``Bug130733.ExprDotSet.CtrlSpace``() =        
        AssertCtrlSpaceCompleteContains [ """
            type C() =
                member this.XX with get() = 4 and set(x) = ()

            let f(x) = C()
            f(0).X <- 42""" ]
            ").X"
            [ "XX" ]
            []

    [<Test>]
    member public this.``Bug130733.ExprDotSet.Dot``() =        
        AssertAutoCompleteContains [ """
            type C() =
                member this.XX with get() = 4 and set(x) = ()

            let f(x) = C()
            f(0).X <- 42""" ]
            "(0)."
            [ "XX" ]
            []


    [<Test>]
    member public this.``Bug130733.Nested.LongIdSet.CtrlSpace``() =        
        AssertCtrlSpaceCompleteContains [ """
            type C() =
                member this.XX with get() = 4 and set(x) = ()
                member this.CC with get() = C()

            let c = C()
            c.CC.X <- 42""" ]
            "CC.X"
            [ "XX" ]
            []

    [<Test>]
    member public this.``Bug130733.Nested.LongIdSet.Dot``() =        
        AssertAutoCompleteContains [ """
            type C() =
                member this.XX with get() = 4 and set(x) = ()
                member this.CC with get() = C()

            let c = C()
            c.CC.X <- 42""" ]
            "c.CC."
            [ "XX" ]
            []

    [<Test>]
    member public this.``Bug130733.Nested.ExprDotSet.CtrlSpace``() =        
        AssertCtrlSpaceCompleteContains [ """
            type C() =
                member this.XX with get() = 4 and set(x) = ()
                member this.CC with get() = C()

            let f(x) = C()
            f(0).CC.X <- 42""" ]
            "CC.X"
            [ "XX" ]
            []

    [<Test>]
    member public this.``Bug130733.Nested.ExprDotSet.Dot``() =        
        AssertAutoCompleteContains [ """
            type C() =
                member this.XX with get() = 4 and set(x) = ()
                member this.CC with get() = C()

            let f(x) = C()
            f(0).CC.X <- 42""" ]
            "(0).CC."
            [ "XX" ]
            []

    [<Test>]
    member public this.``Bug130733.NamedIndexedPropertyGet.Dot``() =        
        AssertAutoCompleteContains [ """
            let str = "foo"
            str.Chars(3).""" ]
            ")."
            [ "CompareTo" ] // char
            []

    [<Test>]
    member public this.``Bug130733.NamedIndexedPropertyGet.CtrlSpace``() =        
        AssertCtrlSpaceCompleteContains [ """
            let str = "foo"
            str.Chars(3).Co""" ]
            ").Co"
            [ "CompareTo" ] // char
            []

    [<Test>]
    member public this.``Bug230533.NamedIndexedPropertySet.CtrlSpace.Case1``() =        
        AssertCtrlSpaceCompleteContains [ """
            type Foo() =
                member x.MutableInstanceIndexer
                    with get (i) = 0
                    and  set (i) (v:string) = ()

            let h() = new Foo()
            (h()).MutableInstanceIndexer(0) <- "foo" """ ]
            ")).Muta"
            [ "MutableInstanceIndexer" ]
            []

    [<Test>]
    member public this.``Bug230533.NamedIndexedPropertySet.CtrlSpace.Case2``() =        
        AssertCtrlSpaceCompleteContains [ """
            type Foo() =
                member x.MutableInstanceIndexer
                    with get (i) = 0
                    and  set (i) (v:string) = ()
            type Bar() =
                member this.ZZZ = new Foo()

            let g() = new Bar()
            (g()).ZZZ.MutableInstanceIndexer(0) <- "blah"  """ ]
            ")).ZZZ.Muta"
            [ "MutableInstanceIndexer" ]
            []

    [<Test>]
    member public this.``Bug230533.ExprDotSet.CtrlSpace.Case1``() =        
        AssertCtrlSpaceCompleteContains [ """
            type C() =
                member this.XX with get() = 4 and set(x) = ()
            type D() =
                member this.CC = new C()
            let f(x) = D()
            f(0).CC.     <- 42 """ ]
            "0).CC."
            [ "XX" ]
            []

    [<Test>]
    member public this.``Bug230533.ExprDotSet.CtrlSpace.Case2``() =        
        AssertCtrlSpaceCompleteContains [ """
            type C() =
                member this.XX with get() = 4 and set(x) = ()
            type D() =
                member this.CC with get() = new C() and set(x) = ()
            let f(x) = D()
            f(0).CC.     <- 42 """ ]
            "0).CC."
            [ "XX" ]
            []

    [<Test>]
    member public this.``Attribute.WhenAttachedToLet.Bug70080``() =        
        this.AutoCompleteBug70080Helper @"
                    open System
                    [<Attr     // expect AttributeUsage from System namespace
                    let f() = 4"

    [<Test>]
    member public this.``Attribute.WhenAttachedToType.Bug70080``() =        
        this.AutoCompleteBug70080Helper(@"
                    open System
                    [<Attr     // expect AttributeUsage from System namespace
                    type MyAttr() = inherit Attribute()")

    [<Test>]
    member public this.``Attribute.WhenAttachedToLetInNamespace.Bug70080``() =        
        this.AutoCompleteBug70080Helper @"
                    namespace Foo
                    open System
                    [<Attr     // expect AttributeUsage from System namespace
                    let f() = 4"

    [<Test>]
    member public this.``Attribute.WhenAttachedToTypeInNamespace.Bug70080``() =        
        this.AutoCompleteBug70080Helper(@"
                    namespace Foo
                    open System
                    [<Attr     // expect AttributeUsage from System namespace
                    type MyAttr() = inherit Attribute()")

    [<Test>]
    member public this.``Attribute.WhenAttachedToNothingInNamespace.Bug70080``() =        
        this.AutoCompleteBug70080Helper(@"
                    namespace Foo
                    open System
                    [<Attr     // expect AttributeUsage from System namespace
                    // nothing here")

    [<Test>]
    member public this.``Attribute.WhenAttachedToModuleInNamespace.Bug70080``() =        
        this.AutoCompleteBug70080Helper(@"
                    namespace Foo
                    open System
                    [<Attr     // expect AttributeUsage from System namespace
                    module Foo = 
                        let x = 42")

    [<Test>]
    member public this.``Attribute.WhenAttachedToModule.Bug70080``() =        
        this.AutoCompleteBug70080Helper(@"
                    open System
                    [<Attr     // expect AttributeUsage from System namespace
                    module Foo = 
                        let x = 42")

    [<Test>]
    member public this.``Identifer.InMatchStatemente.Bug72595``() =        
        // in this bug, "match blah with let" caused the lexfilter to go awry, which made things hopeless for the parser, yielding no parse tree and thus no intellisense
        AssertAutoCompleteContains 
            [ @"
                    type C() = 
                        let someValue = ""abc""
                        member _.M() = 
                          let x = 1
                          match someValue. with
                            let x = 1
                            match 1 with
                            | _ -> 2
    
                    type D() = 
                        member x.P = 1
 
                    [<assembly:Microsoft.FSharp.Core.CompilerServices.TypeProviderAssembly(""Samples.DataStore.Freebase.DesignTime"")>]
                    do()
                " ]
            "someValue." // marker
            [ "Chars" ] // should contain
            [ ] // should not contain

    [<Test>]
    member public this.``HandleInlineComments1``() =        
        AssertAutoCompleteContains 
          [ "let rrr = System  (* boo! *)  .  Int32  .  MaxValue" ]
          ")  ."       // marker
          [ "Int32"]
          [ "abs" ] // should not contain (top-level)

    [<Test>]
    member public this.``HandleInlineComments2``() =    
        AssertAutoCompleteContains 
          [ "let rrr = System  (* boo! *)  .  Int32  .  MaxValue" ]
          "2  ."       // marker
          [ "MaxValue" ] // should contain 
          [ "abs" ] // should not contain (top-level)

    [<Test>]
    member public this.``OpenNamespaceOrModule.CompletionOnlyContainsNamespaceOrModule.Case1``() =        
        AssertAutoCompleteContains 
            [ "open System." ]
            "." // marker
            [ "Collections" ] // should contain (namespace)
            [ ] // should not contain

    [<Test>]
    member public this.``OpenNamespaceOrModule.CompletionOnlyContainsNamespaceOrModule.Case2``() =        
        AssertAutoCompleteContains 
            [ "open Microsoft.FSharp.Collections.Array." ]
            "Array." // marker
            [ "Parallel" ] // should contain (module)
            [ "map" ] // should not contain (let-bound value)

    [<Test>]
    member public this.``BY_DESIGN.CommonScenarioThatBegsTheQuestion.Bug73940``() =        
        AssertAutoCompleteContains 
            [ @"
                    let r = 
                        [""1""] 
                           |> List.map (fun s -> s.     // user previous had e.g. '(fun s -> s)' here, but he erased after 's' to end-of-line and hit '.' e.g. to eventually type '.Substring(5))'
                           |> List.filter (fun s -> s.Length > 5)  // parser recover assumes close paren is here, and type inference goes wacky-useless with such a parse
                "]
            "s." // marker
            [ ] // should contain (ideally would be string)
            [ "Chars" ] // should not contain (documenting the undesirable behavior, that this does not show up)

    [<Test>]
    member public this.``BY_DESIGN.ExplicitlyCloseTheParens.Bug73940``() =        
        AssertAutoCompleteContains 
            [ @"
                    let g lam = 
                        lam true |> printfn ""%b""
                        sprintf ""%s""
                    let r = 
                        [""1""] 
                           |> List.map (fun s -> s.  )   // user types close paren here to avoid paren mismatch
                           |> g     // regardless of whatever is down here now, it won't affect the type of 's' above
                "]
            "s." // marker
            [ "Chars" ] // should contain (string)
            [ ] // should not contain

    [<Test>]
    member public this.``BY_DESIGN.MismatchedParenthesesAreHardToRecoverFromAndHereIsWhy.Bug73940``() =        
        AssertAutoCompleteContains 
            [ @"
                    let g lam = 
                        lam true |> printfn ""%b""
                        sprintf ""%s""
                    let r = 
                        [""1""] 
                           |> List.map (fun s -> s.   // it looks like s is a string here, but it's not!
                           |> g     // parser recovers as though there is a right-paren here
                "]
            "s." // marker
            [ "CompareTo" ] // should contain (bool)
            [ "Chars" ] // should not contain (string)

(*
    [<Test>]
    member public this.``AutoComplete.Bug72596_A``() =        
        AssertAutoCompleteContains 
          [ "type ClassType() ="
            "    let foo = fo" ]  // is not 'let rec', foo should not be in scope yet, but showed up in completions
          "= fo"       // marker
          [ ] // should contain
          [ "foo" ] // should not contain


    [<Test>]
    member public this.``AutoComplete.Bug72596_B``() =        
        AssertAutoCompleteContains 
          [ "let f() ="
            "    let foo = fo" ]  // is not 'let rec', foo should not be in scope yet, but showed up in completions
          "= fo"       // marker
          [ ] // should contain
          [ "foo" ] // should not contain
*)

    [<Test>]
    member public this.``Expression.MultiLine.Bug66705``() =        
        AssertAutoCompleteContains 
          [ "let x = 4"
            "let y = x.GetType()" 
            "         .ToString()" ]  // "fluent" interface spanning multiple lines
          "  ."       // marker
          [ "ToString" ] // should contain
          [ ] // should not contain

    [<Test>]
    member public this.``Expressions.Computation``() =        
        AssertAutoCompleteContains 
          [
            "type FooBuilder() ="
            "   member x.Return(a) = new System.Random()"
            "let foo = FooBuilder()"
            "(foo { return 0 })." ]
          "})."       // marker
          [ "Next" ] // should contain
          [ "GetEnumerator" ] // should not contain

    [<Test>]
    member public this.``Identifier.DefineByVal.InFsiFile.Bug882304_1``() =        
        AutoCompleteInInterfaceFileContains
          [
            "module BasicTest"
            "val z:int = 1"
            "z."
            ]
          "z."       // marker
          [ ] // should contain
          [ "Equals" ] // should not contain

    [<Test>]
    member public this.``NameSpace.InFsiFile.Bug882304_2``() =        
        AutoCompleteInInterfaceFileContains
          [
            "module BasicTest"
            "open System."
            ]
          "System."       // marker
          [ "Action"; // Delegate
            "Activator"; // Class
            "Collections"; // Subnamespace
            "IConvertible"  // Interface 
            ] // should contain
          [  ] // should not contain

    [<Test>]
    member public this.``CLIEvents.DefinedInAssemblies.Bug787438``() =        
        AssertAutoCompleteContains 
          [ "let mb = new MailboxProcessor<int>(fun _ -> ())"
            "mb." ]
          "mb."       // marker
          [ "Error" ] // should contain
          [ "add_Error"; "remove_Error" ] // should not contain

    [<Test>]
    member public this.CLIEventsWithByRefArgs() =
        AssertAutoCompleteContains 
          [ "type MyDelegate = delegate of obj * string byref  -> unit"
            "type mytype() = [<CLIEvent>] member this.myEvent = (new DelegateEvent<MyDelegate>()).Publish"
            "let t = mytype()"
            "t." ]
          "t."       // marker
          [ "add_myEvent"; "remove_myEvent" ] // should contain
          [ "myEvent" ] // should not contain

    [<Test>]
    member public this.``Identifier.AfterParenthesis.Bug835276``() =        
        AssertAutoCompleteContains 
            [ "let f ( s : string ) ="
              "   let x = 10 + s.Length"
              "   for i in 1..10 do"
              "     let ok = 10 + s.Length // dot here did work"
              "     let y = 10 +(s." ]
            "+(s." // marker
          [ "Length" ] // should contain
          [ ] // should not contain

    [<Test>]
    member public this.``Identifier.AfterParenthesis.Bug6484_1``() =        
        AssertAutoCompleteContains 
            [ "for x in 1..10 do"
              "    printfn \"%s\" (x. " ]
            "x." // marker
          [ "CompareTo" ] // should contain (a method on the 'int' type)
          [ ] // should not contain

    [<Test>]
    member public this.``Identifier.AfterParenthesis.Bug6484_2``() =        
        AssertAutoCompleteContains 
            [ "for x = 1 to 10 do"
              "    printfn \"%s\" (x. " ]
            "x." // marker
          [ "CompareTo" ] // should contain (a method on the 'int' type)
          [ ] // should not contain

    [<Test>]
    member public this.``Type.Indexers.Bug4898_1``() =        
        AssertAutoCompleteContains 
          [ 
            "type Foo(len) ="
            "    member this.Value = [1 .. len]"
            "type Bar ="
            "    static member ParamProp with get len = new Foo(len)"
            "let n = Bar.ParamProp."]
          "ar.ParamProp."       // marker
          [ "ToString" ] // should contain
          [ "Value" ] // should not contain      
          
    [<Test>]
    member public this.``Type.Indexers.Bug4898_2``() =        
        AssertAutoCompleteContains 
          [ 
            "type mytype() ="
            "    let instanceArray2 = [|[| \"A\"; \"B\" |]; [| \"A\"; \"B\" |] |]"
            "    let instanceArray = [| \"A\"; \"B\" |]"
            "    member x.InstanceIndexer"
            "        with get(idx) = instanceArray.[idx]"
            "    member x.InstanceIndexer2"
            "        with get(idx1,idx2) = instanceArray2.[idx1].[idx2]"
            "let a = mytype()"
            "a.InstanceIndexer2."]          

          "a.InstanceIndexer2."       // marker
          [ "ToString" ] // should contain
          [ "Chars" ] // should not contain    
                                             
    [<Test>]
    member public this.``Expressions.Sequence``() =        
        AssertAutoCompleteContains 
          [  
            "(seq { yield 1 })." ]
          "})."       // marker
          [ "GetEnumerator" ] // should contain
          [ ] // should not contain
                      
    [<Test>]
    member public this.``LambdaExpression.WithoutClosing.Bug1346``() =        
        AssertAutoCompleteContains 
          [ 
            "let p4 = "
            "   let isPalindrome x = "
            "       let chars = (string_of_int x).ToCharArray()"
            "       let len = chars."
            "       chars "
            "       |> Array.mapi (fun i c ->" ]
          "chars."       // marker
          [ "Length" ] // should contain
          [ ] // should not contain
                      
    [<Test>]
    member public this.``LambdaExpression.WithoutClosing.Bug1346c``() =        
        AssertAutoCompleteContains 
          [ 
            "let p4 = "
            "   let isPalindrome x = "
            "       let chars = (string_of_int x).ToCharArray()"
            "       let len = chars."
            "       chars "
            "       |> Array.mapi (fun i c -> )" ]
          "chars."       // marker
          [ "Length" ] // should contain
          [ ] // should not contain
                  
    [<Test>]
    member public this.``LambdaExpression.WithoutClosing.Bug1346b``() =        
        AssertAutoCompleteContains 
          [ 
            "let p4 = "
            "   let isPalindrome x = "
            "       let chars = (string_of_int x).ToCharArray()"
            "       let len = chars."
            "       chars "
            "       |> Array.mapi (fun i c ->" 
            "let p5 = 1" ]
          "chars."       // marker
          [ "Length" ] // should contain
          [ ] // should not contain
                                           
    [<Test>]
    member public this.``IncompleteStatement.If_A``() =        
        AssertAutoCompleteContains 
          [
            "let x = \"1\""
            "let test2 = if (x)." ]
          "(x)."       // marker
          [ "Contains" ] // should contain
          [ ] // should not contain
                                          
    [<Test>]
    member public this.``IncompleteStatemen.If_C``() =        
        AssertAutoCompleteContains 
          [  
            "let x = \"1\""
            "let test2 = if (x)." 
            "let y = 2" ]
          "(x)."       // marker
          [ "Contains" ] // should contain
          [ ] // should not contain
                                         
    [<Test>]
    member public this.``IncompleteStatement.Try_A``() =        
        AssertAutoCompleteContains 
          [ 
            "let x = \"1\""
            "try (x)." ]
          "(x)."       // marker
          [ "Contains" ] // should contain
          [ ] // should not contain
                      
    [<Test>]
    member public this.``IncompleteStatement.Try_B``() =        
        AssertAutoCompleteContains 
          [
            "let x = \"1\""
            "try (x). finally ()" ]
          "(x)."       // marker
          [ "Contains" ] // should contain
          [ ] // should not contain
                      
    [<Test>]
    member public this.``IncompleteStatement.Try_C``() =        
        AssertAutoCompleteContains 
          [ 
            "let x = \"1\""
            "try (x). with e -> () " ]
          "(x)."       // marker
          [ "Contains" ] // should contain
          [ ] // should not contain

    [<Test>]
    member public this.``IncompleteStatement.Try_D``() =        
        AssertAutoCompleteContains 
          [
            "let x = \"1\""
            "try (x)."
            "let y = 2" ]
          "(x)."       // marker
          [ "Contains" ] // should contain
          [ ] // should not contain
                                        
    [<Test>]
    member public this.``IncompleteStatement.Match_A``() =        
        AssertAutoCompleteContains 
          [  
            "let x = \"1\""
            "let test2 = match (x)." ]
          "(x)."       // marker
          [ "Contains" ] // should contain
          [ ] // should not contain

    [<Test>]
    member public this.``IncompleteStatement.Match_C``() =        
        AssertAutoCompleteContains 
          [ 
            "let x = \"1\""
            "let test2 = match (x)." 
            "let y = 2"]
          "(x)."       // marker
          [ "Contains" ] // should contain
          [ ] // should not contain

    [<Test>]
    member public this.``InDeclaration.Bug3176a``() =        
        AssertCtrlSpaceCompleteContains 
          [ "type T<'a> = { aaaa : 'a; bbbb : int } " ]
          "aa"       // marker
          [ "aaaa" ] // should contain
          [ "bbbb" ] // should not contain

    [<Test>]
    member public this.``InDeclaration.Bug3176b``() =        
        AssertCtrlSpaceCompleteContains 
          [ "type T<'a> = { aaaa : 'a; bbbb : int } " ]
          "bb"       // marker
          [ "bbbb" ] // should contain
          [ "aaaa" ] // should not contain

    [<Test>]
    member public this.``InDeclaration.Bug3176c``() =        
        AssertCtrlSpaceCompleteContains 
          [ "type C =";
                      "  val aaaa : int" ]
          "aa"        // move to marker
          ["aaaa"] [] // should contain 'aaaa'

    [<Test>]
    member public this.``InDeclaration.Bug3176d``() =        
        AssertCtrlSpaceCompleteContains 
          [ "type DU<'a> =";
                      "  | DULabel of 'a" ]
          "DULab"        // move to marker
          ["DULabel"] [] // should contain 'DULabel'
          
    [<Test>]
    member public this.``IncompleteIfClause.Bug4594``() = 
        AssertCtrlSpaceCompleteContains 
          [ "let Bar(xyz) =";
                      "  let hello = ";
                      "    if x" ]
          "if x"     // move to marker
          ["xyz"] [] // should contain 'xyz'
          
    (* Tests for various uses of ObsoleteAttribute ----------------------------------------- *)
    (* Members marked with obsolete shouldn't be visible, but we should support              *)
    (* dot completions on them                                                               *)
 
    // Obsolete and CompilerMessage(IsError=true) should not appear.
    [<Test>]
    member public this.``ObsoleteAndOCamlCompatDontAppear``() = 
        let code=
            [    
                "open System" 
                "type X = "
                "    static member private Private() = ()"
                "    [<Obsolete>]"
                "    static member Obsolete() = ()"
                "    [<CompilerMessage(\"This construct is for ML compatibility.\", 62, IsHidden=true)>]"
                "    static member CompilerMessageTest() = ()"
                "X."
            ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        MoveCursorToEndOfMarker(file,"X.")
        let completions = time1 AutoCompleteAtCursor file "Time of first autocomplete."
        for completion in completions do
            match completion with 
              | CompletionItem("Obsolete" as s,_,_,_,_) 
              //| ("Private" as s,_,_,_)  this isn't supported yet
              | CompletionItem("CompilerMessageTest" as s,_,_,_,_)-> failwith (sprintf "Unexpected item %s at top level."  s)
              | _ -> ()              
    
    // Test various configurations of nested obsolete modules & types
    // (also test whether we show the right intellisense)    
    member public this.AutoCompleteObsoleteTest testLine appendDot should shouldnot =        
        let code = 
          [ "[<System.ObsoleteAttribute(\"!\", false)>]"
            "module ObsoleteTop ="
            "  let T = \"T\""
            "module Module = "
            "  [<System.ObsoleteAttribute(\"!\", false)>]"
            "  module ObsoleteM ="
            "    let A = \"A\""
            "    [<System.ObsoleteAttribute(\"!\", false)>]"
            "    module ObsoleteNested ="
            "      let C = \"C\""
            "  [<System.ObsoleteAttribute(\"!\", false)>]"
            "  type ObsoleteT = "
            "    static member B = \"B\""
            "  let Other = 0"
            "let mutable level = \"\""
            "" ]
        let (_, _, file) = this.CreateSingleFileProject(code @ [ testLine ])
        
        TakeCoffeeBreak(this.VS)
        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)
        if not appendDot then        
            // In this case, we want to check Ctrl+J completions
            // For "level <- O" this shows completions starting with O (e.g. Other)
            MoveCursorToEndOfMarker(file, testLine)
            let completions = CtrlSpaceCompleteAtCursor file
            AssertCompListContainsAll(completions, should)
            AssertCompListDoesNotContainAny(completions, shouldnot) 
        else
            // In this case, we quickly type "." and then get dot-completions
            // For "level <- Module" this shows completions from the "Module" (e.g. "Module.Other")
            // This simulates the case when the user quickly types "dot" after the file has been TCed before.
            ReplaceFileInMemoryWithoutCoffeeBreak file (code @ [ testLine + "." ])      
            MoveCursorToEndOfMarker(file, testLine + ".")
            let completions = AutoCompleteAtCursor file
            AssertCompListContainsAll(completions, should)
            AssertCompListDoesNotContainAny(completions, shouldnot) 
        gpatcc.AssertExactly(0,0)

    // When the module isn't empty, we should show completion for the module
    // (and not type-inferrence based completion on strings - therefore test for 'Chars')
    
    [<Test>]
    member public this.``Obsolete.TopLevelModule``() =
      this.AutoCompleteObsoleteTest "level <- O" false [ "None" ] [ "ObsoleteTop"; "Chars" ]

    [<Test>]
    member public this.``Obsolete.NestedTypeOrModule``() =
      this.AutoCompleteObsoleteTest "level <- Module" true [ "Other" ] [ "ObsoleteM"; "ObsoleteT"; "Chars" ]

    [<Test>]
    member public this.``Obsolete.CompletionOnObsoleteModule.Bug3992``() =
      this.AutoCompleteObsoleteTest "level <- Module.ObsoleteM" true [ "A" ] [ "ObsoleteNested"; "Chars" ]

    [<Test>]
    member public this.``Obsolete.DoubleNested``() =
      this.AutoCompleteObsoleteTest "level <- Module.ObsoleteM.ObsoleteNested" true [ "C" ] [ "Chars" ]

    [<Test>]
    member public this.``Obsolete.CompletionOnObsoleteType``() =
      this.AutoCompleteObsoleteTest "level <- Module.ObsoleteT" true [ "B" ] [ "Chars" ]

    /// BUG: Referencing a non-existent DLL caused an assert.
    [<Test;Category("Repro")>]
    member public this.``WithNonExistentDll``() = 
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        // in the project system, 'AddAssemblyReference' would throw, so just poke this into the .fsproj file
        PlaceIntoProjectFileBeforeImport
            (project, @"
                <ItemGroup>
                    <Reference Include=""..\bar\nonexistent.dll"" />
                </ItemGroup>")
        let file = AddFileFromText(project,"File1.fs",
                                    [    
                                        "(*marker*)  "
                                     ])
        let file = OpenFile(project,"File1.fs")
        
        MoveCursorToEndOfMarker(file,"(*marker*) ")
        let completions = CtrlSpaceCompleteAtCursor file
        AssertCompListContainsAll(completions,[
                                                "System"; // .NET namespaces
                                                "Array2D"]) // Types in the F# library
        AssertCompListDoesNotContain(completions,"Int32") // Types in the System namespace        

    member internal this.AutoCompleteDuplicatesTest (marker, shortName, fullName:string) =
        let code =
            [  
                "namespace A "
                "module Test = "
                "  let foo n = n + 1"
                "  let (|Pat|) x = x + 1"
                "  exception Failed"
                "  type Del = delegate of int -> int"
                "  type A = | Foo"
                "  type B = | Bar = 0"
                "type TestType ="
                "  static member Prop = 0"
                "  static member Event = (new Event<_>()).Publish"
                "namespace B"
                "open A"
                "open A"
                marker ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        MoveCursorToEndOfMarker(file, marker)
        let completions = AutoCompleteAtCursor file
        let (CompletionItem(_, _, _, descrFunc, _)) = completions |> Array.find (fun (CompletionItem(name, _, _, _, _)) -> name = shortName)
        let descr = descrFunc()
        // Check whether the description contains the name only once        
        let occurrences = ("  " + descr + "  ").Split([| fullName |], System.StringSplitOptions.None).Length - 1
        AssertEqualWithMessage(1, occurrences, "The entry for '" + fullName + "' is duplicated.")

    // Return the number of occurrences of the specified method in a tooltip string
    member this.CountMethodOccurrences(descr, methodName:string) =
        let occurrences = ("  " + descr + "  ").Split([| methodName |], System.StringSplitOptions.None).Length - 1
        // This is some tag in the tooltip that also contains the overload name text
        if descr.Contains("[Signature:") then occurrences - 1 else occurrences
                                      
    [<Test>]
    member public this.``Duplicates.Bug4103b``() = 
        for args in 
              [ "Test.", "foo", "foo"; 
                "Test.", "Pat", "Pat";
                "Test.", "Failed", "exception Failed";
                "Test.", "Del", "type Del"; 
                "Test.", "Foo", "Test.A.Foo"
                "Test.B.", "Bar", "Test.B.Bar"
                "TestType.", "Prop", "TestType.Prop"
                "TestType.", "Event", "TestType.Event" ] do   
            this.AutoCompleteDuplicatesTest args      

    [<Test>]
    member public this.``Duplicates.Bug4103c``() =       
        let code =
            [  
                "open System.IO"
                "open System.IO"
                "File." ]         
        let (_, _, file) = this.CreateSingleFileProject(code)
        MoveCursorToEndOfMarker(file, "File.")
        let completions = AutoCompleteAtCursor file
        
        // Get description for Expr.Var
        let (CompletionItem(_, _, _, descrFunc, _)) = completions |> Array.find (fun (CompletionItem(name, _, _, _, _)) -> name = "Open")
        let occurrences = this.CountMethodOccurrences(descrFunc(), "File.Open")
        AssertEqualWithMessage(3, occurrences, "Found wrong number of overloads for 'File.Open'.")

    [<Test>]
    member public this.``Duplicates.Bug2094``() =
        let code = 
            [  
                "open Microsoft.FSharp.Control"
                "let b = MailboxProcessor." ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        MoveCursorToEndOfMarker(file, "MailboxProcessor.")
        let completions = AutoCompleteAtCursor file
          
        // Get description for Expr.Var
        let (CompletionItem(_, _, _, descrFunc, _)) = completions |> Array.find (fun (CompletionItem(name, _, _, _, _)) -> name = "Start")
        let occurrences = this.CountMethodOccurrences(descrFunc(), "Start")
        AssertEqualWithMessage(1, occurrences, sprintf "Found wrong number of overloads for 'MailboxProcessor.Start'.  Found %A." completions)
       
    [<Test;Category("Repro")>]
    member public this.``WithinMatchClause.Bug1603``() =        
        let code = 
                                    [  
                                      "let rec f l ="
                                      "    match l with"
                                      "    | [] ->"
                                      "        let xx = System.DateTime.Now"
                                      "        let y = xx."
                                      "    | x :: xs -> f xs"
                                     ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        MoveCursorToEndOfMarker(file,"let y = xx.")
        let completions = AutoCompleteAtCursor file
        // Should contain something
        Assert.AreNotEqual(0,completions.Length)      
        Assert.IsTrue(completions |> Array.exists (fun (CompletionItem(name,_,_,_,_)) -> name.Contains("AddMilliseconds")))  
        
    // FEATURE: Saving file N does not cause files 1 to N-1 to re-typecheck (but does cause files N to <end> to 
    [<Test>]
    member public this.``Performance.Bug5774``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        
        let file1 = AddFileFromText(project,"File1.fs", [""])
        let file1 = OpenFile(project,"File1.fs")
        //file1.

        let file2 = AddFileFromText(project,"File2.fs", ["let x = 4"; "x."])
        let file2 = OpenFile(project,"File2.fs")

        let file3 = AddFileFromText(project,"File3.fs", [""])
        let file3 = OpenFile(project,"File3.fs")

        // ensure that the incremental builder is running        
        MoveCursorToEndOfMarker(file2,"x.")
        AutoCompleteAtCursor file2 |> ignore

        TakeCoffeeBreak(this.VS)

        // Start the key instrumentation
        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)
        
        // Save file2
        ReplaceFileInMemory file2 [""]
        SaveFileToDisk file2    
        let file3 = OpenFile(project,"File3.fs")
        TakeCoffeeBreak(this.VS)

        gpatcc.AssertExactly(notAA[file2; file3], notAA[file2;file3])

    /// FEATURE: References added to the project bring corresponding new .NET and F# items into scope.
    [<Test;Category("ReproX")>]
    member public this.``AfterAssemblyReferenceAdded``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let file = AddFileFromText(project,"File1.fs",
                                    [ 
                                     "let y = System.Deployment.Application."
                                     "()"])
        let file = OpenFile(project, "File1.fs")
        MoveCursorToEndOfMarker(file,"System.Deployment.Application.")
        let completions = AutoCompleteAtCursor(file)
        // printf "Completions=%A\n" completions
        Assert.AreEqual(0, completions.Length) // Expect none here because reference hasn't been added.
        
        // Now, add a reference to the given assembly.
        this.AddAssemblyReference(project,"System.Deployment")

        TakeCoffeeBreak(this.VS)
        let completions = AutoCompleteAtCursor(file)
        Assert.AreNotEqual(0, completions.Length, "Expected some items in the list after adding a reference.") 

    /// FEATURE: Updating the active project configuration influences the language service
    [<Test>]
    member public this.``AfterUpdateProjectConfiguration``() = 
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        PlaceIntoProjectFileBeforeImport
            (project, @"
                <ItemGroup>
                    <Reference Include=""System.Deployment"" Condition=""'$(Configuration)'=='Foo'"" />
                </ItemGroup>")
        let file = AddFileFromText(project,"File1.fs",
                                    [ 
                                     "let y = System.Deployment.Application."
                                     "()"])
        let file = OpenFile(project, "File1.fs")
        MoveCursorToEndOfMarker(file,"System.Deployment.Application.")
        let completions = AutoCompleteAtCursor(file)
        // printf "Completions=%A\n" completions
        Assert.AreEqual(0, completions.Length) // Expect none here because reference hasn't been added.
        
        // Now, update active configuration
        SetConfigurationAndPlatform(project, "Foo|x86")
        TakeCoffeeBreak(this.VS)
        let completions = AutoCompleteAtCursor(file)
        Assert.AreNotEqual(0, completions.Length, "Expected some items in the list after updating configuration.") 

    /// FEATURE: Updating the active project platform influences the language service
    [<Test>]
    member public this.``AfterUpdateProjectPlatform``() = 
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        PlaceIntoProjectFileBeforeImport
            (project, @"
            <ItemGroup>
                <Reference Include=""System.Deployment"" Condition=""'$(Platform)'=='x86'"" />
            </ItemGroup>")
        SetConfigurationAndPlatform(project, "Debug|AnyCPU")
        let file = AddFileFromText(project,"File1.fs",
                                    [ 
                                     "let y = System.Deployment.Application."
                                     "()"])
        let file = OpenFile(project, "File1.fs")
        MoveCursorToEndOfMarker(file,"System.Deployment.Application.")
        let completions = AutoCompleteAtCursor(file)
        // printf "Completions=%A\n" completions
        Assert.AreEqual(0, completions.Length) // Expect none here because reference hasn't been added.
        
        // Now, update active platform
        SetConfigurationAndPlatform(project, "Debug|x86")
        let completions = AutoCompleteAtCursor(file)
        Assert.AreNotEqual(0, completions.Length, "Expected some items in the list after updating platform.") 

(*
/// FEATURE: The filename on disk and the filename in the project can differ in case.
    [<Test>]
    member this.``Filenames.MayBeDifferentlyCased``() =
        use _guard = this.UsingNewVS() 
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let file = AddFileFromTextEx(project,"file1.fs","FILE1.FS",BuildAction.Compile,
                                    [ 
                                     "let y = System.Deployment.Application."
                                     "()"])
        let file = OpenFile(project, "file1.fs")
        MoveCursorToEndOfMarker(file,"System.Deployment.Application.")
        let completions = AutoCompleteAtCursor(file)
        // printf "Completions=%A\n" completions
        Assert.AreEqual(0, completions.Length) // Expect none here because reference hasn't been added.
        
        // Now, add a reference to the given assembly.
        this.AddAssemblyReference(project,"System.Deployment")
        let completions = AutoCompleteAtCursor(file)
        Assert.AreNotEqual(0, completions.Length, "Expected some items in the list after adding a reference.") 
*)

    /// In this bug, a bogus flag caused the rest of flag parsing to be ignored.
    [<Test>]
    member public this.``FlagsAndSettings.Bug1969``() = 
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        
        
        let file = AddFileFromText(project,"File1.fs",
                                    [ 
                                     "let y = System.Deployment.Application."
                                     "()"])
        let file = OpenFile(project, "File1.fs")
        MoveCursorToEndOfMarker(file,"System.Deployment.Application.")
        let completions = AutoCompleteAtCursor(file)
        // printf "Completions=%A\n" completions
        Assert.AreEqual(0, completions.Length) // Expect none here because reference hasn't been added.
        
        // Add an unknown flag followed by the reference to our assembly.
        let deploymentAssembly = sprintf @"%s\Microsoft.NET\Framework\v4.0.30319\System.Deployment.dll" (System.Environment.GetEnvironmentVariable("windir"))
        SetOtherFlags(project,"--doo-da -r:" + deploymentAssembly) 
        let completions = AutoCompleteAtCursor(file)
        // Now, make sure the reference added after the erroneous reference is still honored.       
        Assert.AreNotEqual(0, completions.Length, "Expected some items in the list after adding a reference.")         
        ShowErrors(project)      
        
    /// In this bug there was an exception if the user pressed dot after a long identifier
    /// that was unknown.
    [<Test>]
    member public this.``OfSystemWindows``() = 
        let code = ["let y=new System.Windows."]
        let (_, _, file) = this.CreateSingleFileProject(code, references = ["System.Windows.Forms"])
        MoveCursorToEndOfMarker(file,"System.Windows.")
        let completions = AutoCompleteAtCursor(file)
        printfn "Completions=%A" completions
        Assert.AreEqual(3, completions.Length)
        
    /// Tests whether we're correctly showing both type and module when they have the same name
    [<Test>]
    member public this.``ShowSetAsModuleAndType``() = 
        let code = ["let s = Set"]
        let (_, _, file) = this.CreateSingleFileProject(code)
        MoveCursorToEndOfMarker(file,"= Set")
        let completions = CtrlSpaceCompleteAtCursor(file)
        let found = completions |> Array.tryFind (fun (CompletionItem(n, _, _, _, _)) -> n = "Set")
        match found with 
        | Some(CompletionItem(_, _, _, f, _)) ->
            let tip = f()
            AssertContains(tip, "module Set")        
            AssertContains(tip, "type Set")        
        | _ -> 
            Assert.Fail("'Set' not found in the completion list")           
        
    /// FEATURE: The user may type namespace followed by dot and see a completion list containing members of that namespace.
    [<Test>]
    member public this.``AtNamespaceDot``() = 
        let code = ["let y=new System.String()"]
        let (_, _, file) = this.CreateSingleFileProject(code)
        MoveCursorToEndOfMarker(file,"System.")
        let completions = AutoCompleteAtCursor(file)
        Assert.IsTrue(completions.Length>0)
        
    /// FEATURE: The user will see appropriate glyphs in the autocompletion list.
    [<Test>]
    member public this.``OfSeveralModuleMembers``() = 
        let code = 
                                    [ 
                                     "module Module ="
                                     "    let Constant = 5"
                                     "    type Class = class"
                                     "       end"
                                     "    type Record = {AString:string}"
                                     "    exception OutOfRange of string"
                                     "    type Enum = Red = 0 | White = 1 | Blue = 2"
                                     "    type DiscriminatedUnion = A | B | C"
                                     "    type AsmType = (# \"!0[]\" #)"
                                     "    type TupleType = int * int"
                                     "    type FunctionType = unit->unit"
                                     "    let (~+) x = -x"
                                     "    type Interface ="
                                     "        abstract MyMethod : unit->unit"
                                     "    type Struct = struct"
                                     "        end"
                                     "    let Function x = 0"
                                     "    let FunctionValue = fun x -> 0"
                                     "    let Tuple = (0,2)"
                                     "    module Submodule ="
                                     "        let a = 0"
                                     "    type ValueType = int"
                                     "module AbbreviationModule ="
                                     "    type StructAbbreviation = Module.Struct"
                                     "    type InterfaceAbbreviation = Module.Interface"
                                     "    type DiscriminatedUnionAbbreviation = Module.DiscriminatedUnion"
                                     "    type RecordAbbreviation = Module.Record"
                                     "    type EnumAbbreviation = Module.Enum"
                                     "    type TupleTypeAbbreviation = Module.TupleType"
                                     "    type AsmTypeAbbreviation = Module.AsmType"
                                     "let y = AbbreviationModule."
                                     "let y = Module."
                                     "let f x = 0"
                                    ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        MoveCursorToEndOfMarker(file," Module.")
        let completions = time1 AutoCompleteAtCursor file "Time of first autocomplete."

        Assert.IsTrue(completions.Length>0)
        for completion in completions do
            match completion with 
              | CompletionItem("A",_,_,_,DeclarationType.EnumMember)                          -> ()
              | CompletionItem("B",_,_,_,DeclarationType.EnumMember)                          -> ()
              | CompletionItem("C",_,_,_,DeclarationType.EnumMember)                          -> ()
              | CompletionItem("Function",_,_,_,_)                                            -> ()
              | CompletionItem("Enum",_,_,_,DeclarationType.Enum)                             -> ()
              | CompletionItem("Constant",_,_,_,_)                                            -> ()
              | CompletionItem("FunctionValue",_,_,_,DeclarationType.Method)                  -> ()
              | CompletionItem("OutOfRange",_,_,_,DeclarationType.Exception)                  -> ()
              | CompletionItem("OutOfRangeException",_,_,_,DeclarationType.Class)             -> ()
              | CompletionItem("Interface",_,_,_,DeclarationType.Interface)                   -> ()
              | CompletionItem("Struct",_,_,_,DeclarationType.ValueType)                      -> ()
              | CompletionItem("Tuple",_,_,_,_)                                               -> ()
              | CompletionItem("Submodule",_,_,_,DeclarationType.Module)                      -> ()
              | CompletionItem("Record",_,_,_,DeclarationType.Class)                          -> ()
              | CompletionItem("DiscriminatedUnion",_,_,_,DeclarationType.DiscriminatedUnion) -> ()
              | CompletionItem("AsmType",_,_,_,DeclarationType.Class)                         -> ()
              | CompletionItem("FunctionType",_,_,_,DeclarationType.Class)                    -> ()
              | CompletionItem("TupleType",_,_,_,DeclarationType.Class)                       -> ()
              | CompletionItem("ValueType",_,_,_,DeclarationType.ValueType)                   -> ()
              | CompletionItem("Class",_,_,_,DeclarationType.Class)                           -> ()
              | CompletionItem("Int32",_,_,_,DeclarationType.Method)                          -> ()
              | CompletionItem("TupleTypeAbbreviation",_,_,_,_)                               -> ()
              | CompletionItem(name,_,_,_,x) -> failwith (sprintf "Unexpected module member %s seen with declaration type %A" name x)

        MoveCursorToEndOfMarker(file,"AbbreviationModule.")
        let completions = time1 AutoCompleteAtCursor file "Time of second autocomplete."
        // printf "Completions=%A\n" completions
        Assert.IsTrue(completions.Length>0)
        for completion in completions do
            match completion with 
              | CompletionItem("Int32",_,_,_,_) 
              | CompletionItem("Function",_,_,_,_)
              | CompletionItem("Enum",_,_,_,_)
              | CompletionItem("Constant",_,_,_,_)
              | CompletionItem("Function",_,_,_,_)
              | CompletionItem("Interface",_,_,_,_)
              | CompletionItem("Struct",_,_,_,_)
              | CompletionItem("Tuple",_,_,_,_)
              | CompletionItem("Record",_,_,_,_) -> ()
              | CompletionItem("EnumAbbreviation",_,_,_,DeclarationType.Enum) -> ()
              | CompletionItem("InterfaceAbbreviation",_,_,_,DeclarationType.Interface) -> ()
              | CompletionItem("StructAbbreviation",_,_,_,DeclarationType.ValueType) -> ()
              | CompletionItem("DiscriminatedUnion",_,_,_,_) -> ()
              | CompletionItem("RecordAbbreviation",_,_,_,DeclarationType.Class) -> ()
              | CompletionItem("DiscriminatedUnionAbbreviation",_,_,_,DeclarationType.DiscriminatedUnion) -> ()
              | CompletionItem("AsmTypeAbbreviation",_,_,_,DeclarationType.Class) -> ()
              | CompletionItem("TupleTypeAbbreviation",_,_,_,_) -> ()
              | CompletionItem(name,_,_,_,x) -> failwith (sprintf "Unexpected union member %s seen with declaration type %A" name x)
        
    [<Test>]
    member public this.ListFunctions() = 
        let code = 
                                    [ 
                                     "let y = List."
                                     "let f x = 0"
                                    ]
        let (_,_, file) = this.CreateSingleFileProject(code)
        MoveCursorToEndOfMarker(file,"List.")
        let completions = time1 AutoCompleteAtCursor file "Time of first autocomplete."
        // printf "Completions=%A\n" completions
        Assert.IsTrue(completions.Length>0)
        for completion in completions do
            match completion with 
              | CompletionItem("Cons",_,_,_,DeclarationType.Method) -> ()
              | CompletionItem("Equals",_,_,_,DeclarationType.Method) -> ()
              | CompletionItem("Empty",_,_,_,DeclarationType.Property) -> () 
              | CompletionItem("empty",_,_,_,_) -> () 
              | CompletionItem(_,_,_,_,DeclarationType.Method) -> ()
              | CompletionItem(name,_,_,_,x) -> failwith (sprintf "Unexpected item %s seen with declaration type %A" name x)

    [<Test>]
    member public this.``SystemNamespace``() =
        let code =
                                    [ 
                                     "let y = System."
                                    ]         
        let (_,_, file) = this.CreateSingleFileProject(code)
        MoveCursorToEndOfMarker(file,"System.")
        let completions = time1 AutoCompleteAtCursor file "Time of first autocomplete."
        // printf "Completions=%A\n" completions
        Assert.IsTrue(completions.Length>0)
        
        let AssertIsDecl(name,decl,expected) =
            if decl<>expected then failwith (sprintf "Expected %A for %s but was %A" expected name decl)
        
        for completion in completions do
            match completion with 
              | CompletionItem("Action" as name,_,_,_,decl) -> AssertIsDecl(name,decl,DeclarationType.Class)
              | CompletionItem("CodeDom" as name,_,_,_,decl) -> AssertIsDecl(name,decl,DeclarationType.Namespace)
              | _ -> ()
      
    // If there is a compile error that prevents a data tip from resolving then show that data tip.
    [<Test>]
    member public this.``MemberInfoCompileErrorsShowInDataTip``() =     
        let code = 
                                    [ 
                                     "type Foo = "
                                     "    member x.Bar() = 0" 
                                     "let foovalue:Foo = unbox null"
                                     "foovalue.B" // make sure this is different from the line 3!
                                    ]
        let (_,_, file) = this.CreateSingleFileProject(code)
        MoveCursorToEndOfMarker(file,"foovalue.B") 
    
        use scope = AutoCompleteMemberDataTipsThrowsScope(this.VS, "Simulated compiler error")
        let completions = time1 CtrlSpaceCompleteAtCursor file "Time of first autocomplete."
        Assert.IsTrue(completions.Length>0)      
        for completion in completions do 
            let (CompletionItem(_,_,_,descfunc,_)) = completion
            let desc = descfunc()
            printfn "MemberInfoCompileErrorsShowInDataTip: desc = <<<%s>>>" desc
            AssertContains(desc,"Simulated compiler error")

    // Bunch of crud in empty list. This test asserts that unwanted things don't exist at the top level.
    [<Test>]
    member public this.``Editor.WhitoutContext.Bug986``() =     
        let code = ["(*mark*)"]
        let (_,_, file) = this.CreateSingleFileProject(code)

        MoveCursorToEndOfMarker(file,"(*mark*)")
        let completions = time1 CtrlSpaceCompleteAtCursor file "Time of first autocomplete."
        for completion in completions do
            match completion with 
              | CompletionItem("IChapteredRowset" as s,_,_,_,_) 
              | CompletionItem("ICorRuntimeHost" as s,_,_,_,_) -> failwith (sprintf "Unexpected item %s at top level."  s)
              | _ -> ()
              
    [<Test>]
    member public this.``LetBind.TopLevel.Bug1650``() =   
        let code =["let x = "]          
        let (_,_, file) = this.CreateSingleFileProject(code)
        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)
        MoveCursorToEndOfMarker(file,"let x = ")
        let completions = time1 CtrlSpaceCompleteAtCursor file "Time of first autocomplete."
        Assert.IsTrue(completions.Length>0)
        gpatcc.AssertExactly(0,0)
              
    [<Test>]
    member public this.``Identifier.Invalid.Bug876b``() =  
        let code =
                                    [ 
                                     "let f (x:System.Windows.Forms.Form) = x."
                                     "  for x = 0 to 0 do () done"
                                    ]
        let (_,project, file) = this.CreateSingleFileProject(code, references = ["System"; "System.Drawing"; "System.Windows.Forms"])

        MoveCursorToEndOfMarker(file,"x.")
        let completions = time1 AutoCompleteAtCursor file "Time of first autocomplete."
        ShowErrors(project)
        Assert.IsTrue(completions.Length>0)      
        
    [<Test>]
    member public this.``Identifier.Invalid.Bug876c``() =     
        let code =
                                    [ 
                                     "let f (x:System.Windows.Forms.Form) = x."
                                     "  12"
                                    ]
        let (_,_, file) = this.CreateSingleFileProject(code, references = ["System"; "System.Drawing"; "System.Windows.Forms"])
        MoveCursorToEndOfMarker(file,"x.")
        let completions = time1 AutoCompleteAtCursor file "Time of first autocomplete."
        Assert.IsTrue(completions.Length>0)     
        
    [<Test>]
    member public this.``EnumValue.Bug2449``() =     
        let code =
                                    [ 
                                     "type E = | A = 1 | B = 2"
                                     "let e = E.A"
                                     "e."
                                    ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        MoveCursorToEndOfMarker(file,"e.")
        let completions = time1 AutoCompleteAtCursor file "Time of first autocomplete."
        AssertCompListDoesNotContain(completions, "value__")
               
    [<Test>]
    member public this.``EnumValue.Bug4044``() =   
        let code =
                                    [ 
                                     "open System.IO"
                                     "let GetFileSize filePath = File.GetAttributes(filePath)."
                                    ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)
        MoveCursorToEndOfMarker(file,"filePath).")
        let completions = time1 AutoCompleteAtCursor file "Time of first autocomplete."
        AssertCompListDoesNotContain(completions, "value__")
        gpatcc.AssertExactly(0,0)
                            
    /// There was a bug (2584) that IntelliSense should treat 'int' as a type instead of treating it as a function
    /// However, this is now deprecated behavior. We want the user to use 'System.Int32' and 
    /// we generally prefer information from name resolution (aslo see 4405)
    [<Test>]
    member public this.``PrimTypeAndFunc``() =     
        let code =
                                    [ 
                                     "System.Int32. "
                                     "int. "
                                    ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        MoveCursorToEndOfMarker(file,"System.Int32.")
        let completions = time1 AutoCompleteAtCursor file "Time of first autocomplete."
        AssertCompListContains(completions,"MinValue")

        MoveCursorToEndOfMarker(file,"int.")
        let completions = time1 AutoCompleteAtCursor file "Time of first autocomplete."
        AssertCompListDoesNotContain(completions,"MinValue")
           
 /// This is related to Bug1605--since the file couldn't parse there was no information to provide the autocompletion list.    
    [<Test>]
    member public this.``MatchStatement.Clause.AfterLetBinds.Bug1603``() = 
        let code =
                                    [ 
                                     "let rec f l ="
                                     "    match l with"
                                     "    | [] ->"
                                     "        let xx = System.DateTime.Now"
                                     "        let y = xx"
                                     "    | x :: xs -> f xs."
                                    ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        MoveCursorToEndOfMarker(file,"xs -> f xs.")
        let completions = time1 AutoCompleteAtCursor file "Time of first autocomplete."
        // printf "Completions=%A\n" completions
        Assert.IsTrue(completions.Length>0)
        
        let mutable count = 0
        
        let AssertIsDecl(name,decl,expected) =
            if decl<>expected then failwith (sprintf "Expected %A for %s but was %A" expected name decl)
                    
        for completion in completions do
            match completion with 
              | CompletionItem("Head" as name,_,_,_,decl) -> 
                count<-count + 1
                AssertIsDecl(name,decl,DeclarationType.Property) 
              | CompletionItem("Tail" as name,_,_,_,decl) -> 
                count<-count + 1
                AssertIsDecl(name,decl,DeclarationType.Property) 
              | CompletionItem(name,_,_,_,x) -> ()        
        
        Assert.AreEqual(2,count)
        
    // This was a bug in which the third level of dotting was ignored.
    [<Test>]
    member public this.``ThirdLevelOfDotting``() =     
        let code =
                                    [ 
                                     "let x = System.Console.Wr"
                                    ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        MoveCursorToEndOfMarker(file,"Console.Wr")
        let completions = time1 CtrlSpaceCompleteAtCursor file "Time of first autocomplete."
        // printf "Completions=%A\n" completions
        Assert.IsTrue(completions.Length>0)
        
        let AssertIsDecl(name,decl,expected) =
            if decl<>expected then failwith (sprintf "Expected %A for %s but was %A" expected name decl)
                    
        for completion in completions do
            match completion with 
              | CompletionItem("BackgroundColor" as name,_,_,_,decl) -> AssertIsDecl(name,decl,DeclarationType.Property) 
              | CompletionItem("CancelKeyEvent" as name,_,_,_,decl) -> AssertIsDecl(name,decl,DeclarationType.Event) 
              | CompletionItem(name,_,_,_,x) -> ()

    // Test completions in an incomplete computation expression (case 1: for "let")
    [<Test>]
    member public this.``ComputationExpressionLet``() =     
        let code =
                    [  
                      "let http(url:string) = "
                      "  async { "
                      "    let rnd = new System.Random()"
                      "    let rsp = rnd.N" ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        MoveCursorToEndOfMarker(file,"rsp = rnd.")
        let completions = time1 AutoCompleteAtCursor file "Time of first autocomplete."
        AssertCompListContainsAll(completions, ["Next"])
 
    [<Test>]
    member public this.``BestMatch.Bug4320a``() = 
        let code = [ " let x = System." ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        MoveCursorToEndOfMarker(file,"System.")
        let Match text filterText = CompletionBestMatchAtCursorFor(file, text, filterText)
        // (ItemName, isUnique, isPrefix)
        // isUnique=true means it will be selected on ctrl-space invocation
        // isPrefix=true means it will be selected, instead of just outlined
        AssertEqual(Some ("GC", false, true),                Match "G" None)
        AssertEqual(Some ("GC", false, true),                Match "GC" None)
        AssertEqual(Some ("GCCollectionMode", true, true),   Match "GCC" None)
        AssertEqual(Some ("GCCollectionMode", false, false), Match "GCCZ" None)
        AssertEqual(Some ("GC", false, true),                Match "G" (Some "G"))
        AssertEqual(Some ("GC", false, true),                Match "GC" (Some "G"))
        AssertEqual(Some ("GCCollectionMode", true, true),   Match "GCC" (Some "G"))
        AssertEqual(Some ("GCCollectionMode", false, false), Match "GCCZ" (Some "G"))
        AssertEqual(Some ("GC", false, true),                Match "G" (Some "GC"))
        AssertEqual(Some ("GC", false, true),                Match "GC" (Some "GC"))
        AssertEqual(Some ("GCCollectionMode", true, true),   Match "GCC" (Some "GC"))
        AssertEqual(Some ("GCCollectionMode", false, false), Match "GCCZ" (Some "GC"))
    
    [<Test>]
    member public this.``BestMatch.Bug4320b``() = 
        let code = [ " let x = List." ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        MoveCursorToEndOfMarker(file,"List.")
        let Match text = CompletionBestMatchAtCursorFor(file, text, None)
        // (ItemName, isUnique, isPrefix)
        // isUnique=true means it will be selected on ctrl-space invocation
        // isPrefix=true means it will be selected, instead of just outlined
        AssertEqual(Some ("empty", false, true),  Match "e")
        AssertEqual(Some ("empty", true, true), Match "em")
      
    [<Test>]
    member public this.``BestMatch.Bug5131``() = 
        let code = [ "System.Environment." ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        MoveCursorToEndOfMarker(file,"Environment.")
        let Match text = CompletionBestMatchAtCursorFor(file, text, None)
        // (ItemName, isUnique, isPrefix)
        // isUnique=true means it will be selected on ctrl-space invocation
        // isPrefix=true means it will be selected, instead of just outlined
        AssertEqual(Some ("OSVersion", true, true),  Match "o")
    
    [<Test>]
    member public this.``COMPILED.DefineNotPropagatedToIncrementalBuilder``() =
        use _guard = this.UsingNewVS()
 
        let solution = this.CreateSolution()
        let projName = "testproject"
        let project = CreateProject(solution,projName)
        let dir = ProjectDirectory(project)
        let file1 = AddFileFromText(project,"File1.fs", 
                                    [ 
                                     "module File1"
                                     "#if COMPILED"
                                     "let x = 0"
                                     "#else"
                                     "let y = 1"
                                     "#endif"
                                    ]) 
        let file2 = AddFileFromText(project,"File2.fs", 
                                    [ 
                                     "module File2"
                                     "File1."
                                    ]) 

        let file = OpenFile(project, "File2.fs")
        MoveCursorToEndOfMarker(file, "File1.")
        let completionItems = 
            AutoCompleteAtCursor(file)
            |> Array.map (fun (CompletionItem(name, _, _, _, _)) -> name)
        Assert.AreEqual(1, completionItems.Length, "Expected 1 item in the list")
        Assert.AreEqual("x", completionItems.[0], "Expected 'x' in the list")
 
    [<Test>]
    member public this.``VisualStudio.CloseAndReopenSolution``() = 
        use _guard = this.UsingNewVS()
        // This test exposes what was once a bug, where closing a solution and then re-opening
        // it caused the old stale IProjectSiteOption (that the LanguageService had cached)
        // to eventually throw a NullReferenceException and assert.
        let solution = this.CreateSolution()
        let projName = "testproject"
        let project = CreateProject(solution,projName)
        let dir = ProjectDirectory(project)
        let file = AddFileFromText(project,"File1.fs", 
                                    [ 
                                     "let x = 0"
                                     "let y = x."
                                    ]) 
        let file = OpenFile(project, "File1.fs")
        MoveCursorToEndOfMarker(file,"x.")
        let completions = time1 AutoCompleteAtCursor file "Time of first autocomplete."
        // printf "Completions=%A\n" completions
        Assert.IsTrue(completions.Length>0)
        this.CloseSolution(solution)
        let project,solution = OpenExistingProject(this.VS, dir, projName)
        let file = List.item 0 (GetOpenFiles(project))
        MoveCursorToEndOfMarker(file,"x.")
        let completions = time1 AutoCompleteAtCursor file "Time of first autocomplete."
        // printf "Completions=%A\n" completions
        Assert.IsTrue(completions.Length>0)

    [<Test>]
    [<Ignore("https://github.com/Microsoft/visualfsharp/issues/6166")>]
    member this.``BadCompletionAfterQuicklyTyping.Bug72561``() =        
        let code = [ "        " ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        
        TakeCoffeeBreak(this.VS)
        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)
        // In this case, we quickly type "." and then get dot-completions
        // This simulates the case when the user quickly types "dot" after the file has been TCed before.
        ReplaceFileInMemoryWithoutCoffeeBreak file ([ "[1]." ])      
        MoveCursorToEndOfMarker(file, ".")
        // Note: no TakeCoffeeBreak(this.VS)
        let completions = AutoCompleteAtCursor file
        AssertCompListContainsExactly(completions, [])  // there are no stale results for an expression at this location, so nothing is returned immediately
        // second-chance intellisense will kick in:
        TakeCoffeeBreak(this.VS)
        let completions = AutoCompleteAtCursor file
        AssertCompListContainsAll(completions, ["Length"])
        AssertCompListDoesNotContainAny(completions, ["AbstractClassAttribute"]) 
        gpatcc.AssertExactly(0,0)

    [<Test>]
    [<Ignore("https://github.com/Microsoft/visualfsharp/issues/6166")>]
    member this.``BadCompletionAfterQuicklyTyping.Bug72561.Noteworthy.NowWorks``() =        
        let code = [ "123      " ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        
        TakeCoffeeBreak(this.VS)
        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)
        // In this case, we quickly type "." and then get dot-completions
        // This simulates the case when the user quickly types "dot" after the file has been TCed before.
        ReplaceFileInMemoryWithoutCoffeeBreak file ([ "[1]." ])      
        MoveCursorToEndOfMarker(file, ".")
        // Note: no TakeCoffeeBreak(this.VS)
        let completions = AutoCompleteAtCursor file
        AssertCompListIsEmpty(completions)  // empty completion list means second-chance intellisense will kick in
        // if we wait...
        TakeCoffeeBreak(this.VS)
        let completions = AutoCompleteAtCursor file
        // ... we get the expected answer
        AssertCompListContainsAll(completions, ["Length"])
        AssertCompListDoesNotContainAny(completions, ["AbstractClassAttribute"]) 
        gpatcc.AssertExactly(0,0)

    [<Test>]
    [<Ignore("https://github.com/Microsoft/visualfsharp/issues/6166")>]
    member this.``BadCompletionAfterQuicklyTyping.Bug130733.NowWorks``() =        
        let code = [ "let someCall(x) = null"
                     "let xe = someCall(System.IO.StringReader()  "]
        let (_, _, file) = this.CreateSingleFileProject(code)
        
        TakeCoffeeBreak(this.VS)
        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)
        // In this case, we quickly type "." and then get dot-completions
        // This simulates the case when the user quickly types "dot" after the file has been TCed before.
        ReplaceFileInMemoryWithoutCoffeeBreak file [ "let someCall(x) = null"
                                                     "let xe = someCall(System.IO.StringReader(). "]
        MoveCursorToEndOfMarker(file, "().")
        // Note: no TakeCoffeeBreak(this.VS)
        let completions = AutoCompleteAtCursor file
        AssertCompListContainsAll(completions, ["ReadBlock"]) // text to the left of the dot did not change, so we use stale (correct) result immediately
        // if we wait...
        TakeCoffeeBreak(this.VS)
        let completions = AutoCompleteAtCursor file
        // ... we get the expected answer
        AssertCompListContainsAll(completions, ["ReadBlock"])
        gpatcc.AssertExactly(0,0)


//*********************************************Previous Completion test and helper*****
    member private this.VerifyCompListDoesNotContainAnyAtStartOfMarker(fileContents : string, marker : string, list : string list) =
        let (solution, project, file) = this.CreateSingleFileProject(fileContents)
        MoveCursorToStartOfMarker(file, marker)    
        let completions = AutoCompleteAtCursor(file)
        AssertCompListDoesNotContainAny(completions,list)

    member private this.VerifyCtrlSpaceListDoesNotContainAnyAtStartOfMarker(fileContents : string, marker : string, list : string list) =
        let (solution, project, file) = this.CreateSingleFileProject(fileContents)
        MoveCursorToStartOfMarker(file, marker)    
        let completions = CtrlSpaceCompleteAtCursor file
        AssertCompListDoesNotContainAny(completions,list)

    member private this.VerifyCompListContainAllAtStartOfMarker(fileContents : string, marker : string, list : string list) =
        let (solution, project, file) = this.CreateSingleFileProject(fileContents)
        MoveCursorToStartOfMarker(file, marker)
        let completions = AutoCompleteAtCursor(file)
        AssertCompListContainsAll(completions, list)

    member private this.VerifyCtrlSpaceListContainAllAtStartOfMarker(fileContents : string, marker : string, list : string list, ?coffeeBreak:bool, ?addtlRefAssy:list<string>) =
        let coffeeBreak = defaultArg coffeeBreak false
        let (solution, project, file) = this.CreateSingleFileProject(fileContents, ?references = addtlRefAssy)
        MoveCursorToStartOfMarker(file, marker)
        if coffeeBreak then TakeCoffeeBreak(this.VS)
        let completions = CtrlSpaceCompleteAtCursor file
        AssertCompListContainsAll(completions, list)

        
    member private this.VerifyAutoCompListIsEmptyAtEndOfMarker(fileContents : string, marker : string) =
        let (solution, project, file) = this.CreateSingleFileProject(fileContents)
        MoveCursorToEndOfMarker(file, marker)
        let completions = AutoCompleteAtCursor(file)   
        AssertEqual(0,completions.Length)              

    member private this.VerifyCtrlSpaceCompListIsEmptyAtEndOfMarker(fileContents : string, marker : string) =
        let (solution, project, file) = this.CreateSingleFileProject(fileContents)
        MoveCursorToEndOfMarker(file, marker)
        let completions = CtrlSpaceCompleteAtCursor(file)   
        AssertEqual(0,completions.Length)              
                
    [<Test>]
    member this.``Expression.WithoutPreDefinedMethods``() = 
        this.VerifyCtrlSpaceListDoesNotContainAnyAtStartOfMarker(
            fileContents = """
                let x = F(*HERE*)""",
            marker = "(*HERE*)",
            list = ["FSharpDelegateEvent"; "PrivateMethod"; "PrivateType"])
                    
    [<Test>]
    member this.``Expression.WithPreDefinedMethods``() = 
        this.VerifyCtrlSpaceListContainAllAtStartOfMarker(
            fileContents = """
                module Module1 =
                    let private PrivateField = 1    
                    let private PrivateMethod x = 
                        x+1        
                    type private PrivateType() =
                        member this.mem = 1    
                    let a = (*Marker1*)
    
                    let b = 23
                """,
            marker = "(*Marker1*)",
            list = ["PrivateField"; "PrivateMethod"; "PrivateType"])                 
         
    // Regression for bug 2116 -- Consider making selected item in completion list case-insensitiv         
    [<Test>]
    member this.``CaseInsensitive``() =
        this.VerifyCtrlSpaceListContainAllAtStartOfMarker(
            fileContents = """
                type Test() =
                    member this.Xyzzy = ()
                    member this.xYzzy = ()
                    member this.xyZzy = ()
                    member this.xyzZy = ()
                    member this.xyzzY = ()

                let t = new Test()
                t.XYZ(*Marker1*)
                """,
            marker = "(*Marker1*)",
            list = ["Xyzzy"; "xYzzy"; "xyZzy"; "xyzZy"; "xyzzY"])  
      
    [<Test>]
    member this.``Attributes.CanSeeOpenNamespaces.Bug268290.Case1``() =
        AssertCtrlSpaceCompleteContains 
            ["""
                    module Foo
                    open System
                    [<
             """]
            "[<"
            ["AttributeUsage"]
            []

    [<Test>]
    member this.``Selection``() =
        AssertCtrlSpaceCompleteContains 
            ["""
                let preSelectedItem = 1
                let r = (*MarkerPreSelectedItem*)pre
                """]
            "(*MarkerPreSelectedItem*)pre"
            ["preSelectedItem"]
            []
            
    // Regression test for 1653 -- Both the F# exception and the .NET exception representing it are shown in completion lists
    [<Test>]
    member this.``NoDupException.Postive``() = 
        this.VerifyCtrlSpaceListContainAllAtStartOfMarker(
            fileContents = """
                let x = Match(*MarkerException*)""",
            marker = "(*MarkerException*)",
            list = ["MatchFailureException"])

    [<Test>]
    member this.``DotNetException.Negative``() =
        this.VerifyCtrlSpaceListDoesNotContainAnyAtStartOfMarker(
            fileContents = """
                let x = Match(*MarkerException*)""",
            marker = "(*MarkerException*)",
            list = ["MatchFailure"])        

    // Regression for bug 921 -- intellisense case-insensitive? 
    [<Test>]
    member this.``CaseInsensitive.MapMethod``() =
        this.VerifyCtrlSpaceListContainAllAtStartOfMarker(
            fileContents = """
                List.MaP(*MarkerCase*)
                """,
            marker = "(*MarkerCase*)",
            list = ["map"])
                        
    //Regression for bug   69644    69654  Fsharp: no completion for an identifier when 'use'd inside an 'async' block
    [<Test>]
    [<Ignore("69644 - no completion for an identifier when 'use'd inside an 'async' block")>]
    member this.``InAsyncAndUseBlock``() =
        this.VerifyCompListContainAllAtStartOfMarker(
            fileContents = """
                open System.Text.RegularExpressions
                open System.IO

                let collectLinksAsync (url:string) : Async<string> =
                    async { do printfn "requesting %s" url
                            let! html = 
                                async { use reader = new System.IO.StreamReader(new System.IO.FileStream("", FileMode.CreateNew)) 
                                        do printfn "reading %s" url
                                        return (*Marker1*)reader.ReadToEnd()  }  //<---- reader
                            let links = "a"
                            return links }
                """,
            marker = "(*Marker1*)",
            list = ["reader"])  

    [<Test>]
    member this.``WithoutOpenNamespace``() =
        AssertCtrlSpaceCompleteContains 
            ["""
                module CodeAccessibility

                let x = S(*Marker*)
                """]
            "(*Marker*)"
            [] // should
            ["Single"] // should not

    [<Test>]
    member this.``PrivateVisible``() =
        AssertCtrlSpaceCompleteContains 
            ["""
                module CodeAccessibility

                module Module1 =

                    let private fieldPrivate = 1
    
                    let private MethodPrivate x = 
                        x+1
        
                    type private TypePrivate() =
                        member this.mem = 1
    
                    let a = (*Marker1*) 
                    """]
            "(*Marker1*) "
            ["fieldPrivate";"MethodPrivate";"TypePrivate"]
            []

    [<Test>]
    member this.``InternalVisible``() =
        AssertCtrlSpaceCompleteContains 
            ["""
                module CodeAccessibility

                module Module1 =

                    let internal fieldInternal = 1
    
                    let internal MethodInternal x = 
                        x+1
        
                    type internal TypeInternal() =
                        member this.mem = 1
    
                    let a = (*Marker1*) """]
            "(*Marker1*) "
            ["fieldInternal";"MethodInternal";"TypeInternal"]  // should
            [] // should not

    [<Test>]
    [<Category("Unit of Measure")>]
    // Verify that we display the correct list of Unit of Measure (Names) in the autocomplete window. 
    // This also ensures that no UoM are accidenatally added or removed.
    member public this.``UnitMeasure.UnitNames``() =
        AssertAutoCompleteContains
          [ "Microsoft.FSharp.Data.UnitSystems.SI.UnitNames."]
          "UnitNames."
          [ "ampere"; "becquerel"; "candela"; "coulomb"; "farad"; "gray"; "henry"; "hertz";
            "joule"; "katal"; "kelvin"; "kilogram"; "lumen"; "lux"; "metre"; "mole"; "newton";
            "ohm"; "pascal"; "second"; "siemens"; "sievert"; "tesla"; "volt"; "watt"; "weber";] // should contain; exact match
          [ ] // should not contain 

    [<Test>]
    [<Category("Unit of Measure")>]
    // Verify that we display the correct list of Unit of Measure (Symbols) in the autocomplete window. 
    // This also ensures that no UoM are accidenatally added or removed.
    member public this.``UnitMeasure.UnitSymbols``() =
        AssertAutoCompleteContains
          [ "Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols."]
          "UnitSymbols."
          [ "A"; "Bq"; "C"; "F"; "Gy"; "H"; "Hz"; "J"; "K"; "N"; "Pa"; "S"; "Sv"; "T"; "V";
            "W"; "Wb"; "cd"; "kat"; "kg"; "lm"; "lx"; "m"; "mol"; "ohm"; "s";] // should contain; exact match
          [ ] // should not contain 

(*------------------------------------------IDE Query automation start -------------------------------------------------*)
    member private this.AssertAutoCompletionInQuery(fileContent : string list, marker:string,contained:string list) =
        let file = createFile fileContent SourceFileKind.FS ["System.Xml.Linq"] None
            
        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)
        MoveCursorToEndOfMarker(file, marker)
        let completions = CompleteAtCursorForReason(file,BackgroundRequestReason.CompleteWord)
        AssertCompListContainsAll(completions, contained)
        gpatcc.AssertExactly(0,0)

    [<Test>]
    [<Category("Query")>]
    // Custom operators appear in Intellisense list after entering a valid query operator
    // on the previous line and invoking Intellisense manually
    // Including in a nested query
    member public this.``Query.Auto.InNestedQuery``() =
        this.AssertAutoCompletionInQuery(
          fileContent =["""
            let tuples = [ (1, 8, 9); (56, 45, 3)] 
            let numbers = [ 1;2; 8; 9; 15; 23; 3; 42; 4;0; 55;]

            let foo = 
                query {
                    for n in numbers do
                    let maxNumber = query {for x in tuples do ma}
                    select n }"""],
          marker = "do ma",
          contained = [ "maxBy"; "maxByNullable"; ])

    [<Test>]
    [<Category("Query")>]
    // Custom operators appear in Intellisense list after entering a valid query operator
    // on the previous line and invoking Intellisense manually
    // Including in a nested query
    member public this.``Query.Auto.OffSetFromPreviousLine``() =
        this.AssertAutoCompletionInQuery(
          fileContent =["""
            let numbers = [ 1;2; 8; 9; 15; 23; 3; 42; 4;0; 55;]
            let foo = 
                query {
                    for n in numbers do
                        gro
                  }"""],
          marker = "gro",
          contained = [ "groupBy"; "groupJoin"; "groupValBy";])

    [<Test>]
    member this.``Namespace.System``() =
        this.VerifyDotCompListContainAllAtEndOfMarker(
            fileContents = """
                // Test '.' after System
                open System
                let str = "a string"
                // Test '.' after str
                let _ = str(*usage*)
                """,
            marker = "open System",
            list = [ "IO"; "Collections" ]) 
                    
    [<Test>]
    member this.``Identifier.String.Positive``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                open System
                let str = "a string"
                // Test '.' after str
                let _ = str(*usage*)
                """,
            marker = "(*usage*)",
            list = ["Chars"; "ToString"; "Length"; "GetHashCode"])   
            
    [<Test>]
    member this.``Idenfifier.String.Negative``() =
        this.VerifyDotCompListDoesNotContainAnyAtStartOfMarker(
            fileContents = """
                open System
                let str = "a string"
                // Test '.' after str
                let _ = str(*usage*)
                """,
            marker = "(*usage*)",
            list = ["Parse"; "op_Addition"; "op_Subtraction"])   

    // Verify add_* methods show up for non-standard events. These are events
    // where the associated delegate type does not return "void" 
    [<Test>]
    member this.``Event.NonStandard.PrefixMethods``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """System.AppDomain.CurrentDomain(*usage*)""",
            marker = "(*usage*)",
            list = ["add_AssemblyResolve"; "remove_AssemblyResolve"; "add_ReflectionOnlyAssemblyResolve"; "remove_ReflectionOnlyAssemblyResolve"; "add_ResourceResolve"; "remove_ResourceResolve"; "add_TypeResolve"; "remove_TypeResolve"])           
        
    // Verify the events do show up. An error is generated when they are used asking the user to use add_* and remove_* instead.
    // That is, they are legitimate name resolutions but do not pass type checking.
    [<Test>]
    member this.``Event.NonStandard.VerifyLegitimateNameShowUp``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = "System.AppDomain.CurrentDomain(*usage*)",
            marker = "(*usage*)",
            list = ["AssemblyResolve"; "ReflectionOnlyAssemblyResolve"; "ResourceResolve"; "TypeResolve" ])

    [<Test>]
    member this.``Array``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = "let arr = [| for i in 1..10 -> i |](*Mexparray*)",
            marker = "(*Mexparray*)",
            list = ["Clone"; "IsFixedSize"]) 
        
    [<Test>]
    member this.``List``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = "let lst = [ for i in 1..10 -> i](*Mexplist*)",
            marker = "(*Mexplist*)",
            list = ["Head"; "Tail"]) 

    [<Test;Category("Repro")>]
    member public this.``ExpressionDotting.Regression.Bug187799``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                type T() = 
                    member _.P with get() = new T()
                    member _.M() = [|1..2|]
                let t = new T()
                t.P.M()(*marker*)  """,
            marker = "(*marker*)",
            list = ["Clone"])  // should contain method on array (result of M call)

    [<Test;Category("Repro")>]
    member public this.``ExpressionDotting.Regression.Bug187799.Test2``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                type T() = 
                    member _.M() = [|1..2|]

                type R = { P : T } 
                    
                // dotting through an F# record field
                let r = { P = T() }
                r.P.M()(*marker*)  """,
            marker = "(*marker*)",
            list = ["Clone"])  // should contain method on array (result of M call)

    [<Test;Category("Repro")>]
    member public this.``ExpressionDotting.Regression.Bug187799.Test3``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                type R = { P : System.Reflection.InterfaceMapping  } 
                    
                // Dotting through an F# record field and an IL record field
                // Note that InterfaceMapping is a rare example of a public .NET instance field in mscorlib
                let r = { P = Unchecked.defaultof<System.Reflection.InterfaceMapping > }
                r.P(*marker*)""",
            marker = "(*marker*)",
            list = ["InterfaceMethods"])  



    [<Test;Category("Repro")>]
    member public this.``ExpressionDotting.Regression.Bug187799.Test4``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                type R = { P : System.Reflection.InterfaceMapping  } 
                    
                // Dotting through an F# record field and an IL record field
                // Note that InterfaceMapping is a rare example of a public .NET instance field in mscorlib
                let f() = { P = Unchecked.defaultof<System.Reflection.InterfaceMapping > }
                f().P(*marker*)""",
            marker = "(*marker*)",
            list = ["InterfaceMethods"])  

    [<Test;Category("Repro")>]
    member public this.``ExpressionDotting.Regression.Bug187799.Test5``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                type R = { P : System.Reflection.InterfaceMapping  } 
                    
                // Note that InterfaceMapping is a rare example of a public .NET instance field in mscorlib
                let f() = { P = Unchecked.defaultof<System.Reflection.InterfaceMapping > }
                f().P.InterfaceMethods(*marker*)""",
            marker = "(*marker*)",
            list = ["GetEnumerator"])  

    [<Test;Category("Repro")>]
    member public this.``ExpressionDotting.Regression.Bug187799.Test6``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                type R = { P : System.AppDomain  } 
                    
                // Test dotting through an F# record field and a .NET event
                let f() = { P = null }
                f().P.UnhandledException(*marker*)""",
            marker = "(*marker*)",
            list = ["AddHandler"])  

    [<Test;Category("Repro")>]
    member public this.``ExpressionDotting.Regression.Bug187799.Test7``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                type R = { P : System.AppDomain  } 
                    
                // Test dotting through an F# record field and a .NET event
                let f() = { P = null }
                f().P.UnhandledException.GetType()(*marker*)""",
            marker = "(*marker*)",
            list = ["Assembly"])  

    [<Test;Category("Repro")>]
    member public this.``ExpressionDotting.Regression.Bug187799.Test8``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                type C() =
                    static member XXX with get() = 4 and set(x) = ()
                    static member CCC with get() = C()

                C.XXX(*marker*) <- 42""",
            marker = "(*marker*)",
            list = ["CompareTo"])  


    [<Test;Category("Repro")>]
    member public this.``ExpressionDotting.Regression.Bug187799.Test9``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                type C() =
                    static member XXX with get() = 4 and set(x) = ()
                    static member CCC with get() = C()

                C.XXX(*marker*) <- 42""",
            marker = "(*marker*)",
            list = ["CompareTo"])  

    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.EditorHideMethodsAttribute")>]
    // This test case checks that autocomplete on the provided Type DOES NOT show System.Object members
    member this.``TypeProvider.EditorHideMethodsAttribute.Type.DoesnotContain``() =
        this.VerifyDotCompListDoesNotContainAnyAtStartOfMarker(
            fileContents = """ 
                                let t = new N.T()
                                t(*Marker*)""",
            marker = "(*Marker*)",
            list = ["Equals";"GetHashCode"],            
            addtlRefAssy = [PathRelativeToTestAssembly(@"EditorHideMethodsAttribute.dll")])

    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.EditorHideMethodsAttribute")>]
    // This test case checks if autocomplete on the provided Type shows only the Event1 elements
    member this.``TypeProvider.EditorHideMethodsAttribute.Type.Contains``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """ 
                                let t = new N.T()
                                t(*Marker*)""",
            marker = "(*Marker*)",
            list = ["Event1"],            
            addtlRefAssy = [PathRelativeToTestAssembly(@"EditorHideMethodsAttribute.dll")])
    
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.EditorHideMethodsAttribute")>]
    // This test case checks if autocomplete on the provided Type shows the instance method IM1
    member this.``TypeProvider.EditorHideMethodsAttribute.InstanceMethod.Contains``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """ 
                                let t = new N1.T1()
                                t(*Marker*)""",
            marker = "(*Marker*)",
            list = ["IM1"],            
            addtlRefAssy = [PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll")])
    
    [<Test>]
    [<Category("TypeProvider")>]
    // This test case checks that nested types show up only statically and not on instances
    member this.``TypeProvider.TypeContainsNestedType``() =
        // should have it here
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """ 
                                type XXX = N1.T1(*Marker*)""",
            marker = "(*Marker*)",
            list = ["SomeNestedType"],            
            addtlRefAssy = [PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll")])
        // should _not_ have it here
        this.VerifyDotCompListDoesNotContainAnyAtStartOfMarker(
            fileContents = """ 
                                let t = new N1.T1()
                                t(*Marker*)""",
            marker = "(*Marker*)",
            list = ["SomeNestedType"],            
            addtlRefAssy = [PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll")])
    
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.EditorHideMethodsAttribute")>]
    // This test case checks if autocomplete on the provided Event shows only the AddHandler/RemoveHandler elements
    member this.``TypeProvider.EditorHideMethodsAttribute.Event.Contain``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """ 
                                let t = new N.T()
                                t.Event1(*Marker*)""",
            marker = "(*Marker*)",
            list = ["AddHandler";"RemoveHandler"],            
            addtlRefAssy = [PathRelativeToTestAssembly(@"EditorHideMethodsAttribute.dll")])
    
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.EditorHideMethodsAttribute")>]
    // This test case checks if autocomplete on the provided Method shows no elements 
    // You can see this as a "negative case" (to check that the usage of the attribute on a method is harmless)
    member this.``TypeProvider.EditorHideMethodsAttribute.Method.Contain``() =
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """ 
                                let t = N.T.M(*Marker*)()""",
            marker = "(*Marker*)",
            addtlRefAssy = [PathRelativeToTestAssembly(@"EditorHideMethodsAttribute.dll")])

    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.EditorHideMethodsAttribute")>]
    // This test case checks if autocomplete on the provided Property (the type of which is not synthetic) shows the usual elements... like GetType()
    // 1. I think it does not make sense to use this attribute on a synthetic property unless it's type is also synthetic (already covered)
    // 2. You can see this as a "negative case" (to check that the usage of the attribute is harmless)
    member this.``TypeProvider.EditorHideMethodsAttribute.Property.Contain``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """ 
                                let t = N.T.StaticProp(*Marker*)""",
            marker = "(*Marker*)",
            list = ["GetType"; "Equals"],   // just a couple of System.Object methods: we expect them to be there!
            addtlRefAssy = [PathRelativeToTestAssembly(@"EditorHideMethodsAttribute.dll")])
                                          
    [<Test>]
    member this.CompListInDiffFileTypes() =
        let fileContents = """
            val x:int = 1
            x(*MarkerInsideaSignatureFile*)
            """
        let (solution, project, openfile) = this.CreateSingleFileProject(fileContents, fileKind = SourceFileKind.FSI)

        let completions = DotCompletionAtStartOfMarker openfile "(*MarkerInsideaSignatureFile*)"
        AssertCompListContainsAll(completions, []) // .fsi will not contain completions for this (it doesn't make sense)
        
        let fileContents = """
            let i = 1
            i(*MarkerInsideSourceFile*)
            """
        let (solution, project, file) = this.CreateSingleFileProject(fileContents)
        
        let completions = DotCompletionAtStartOfMarker file "(*MarkerInsideSourceFile*)"
        AssertCompListContainsAll(completions, ["CompareTo"; "Equals"])
  
    [<Test>]
    member this.ConstrainedTypes() =
        let fileContents = """
            type Pet() = 
                member x.Name() = "pet"
                member x.Speak() = "this is a pet"    
            type Dog() = 
                inherit Pet()
                member x.dog() = "this is a dog"    
            let dog = new Dog()
            let pet = dog :> Pet
            pet(*Mupcast*)
            let dctest = pet :?> Dog
            dctest(*Mdowncast*)
            let f (x : bigint) = x(*Mconstrainedtoint*)
            """
        let references = 
            [
                "System.Numerics"  // code uses bigint
            ]
        let (solution, project, file) = this.CreateSingleFileProject(fileContents, references = references)        
        let completions = DotCompletionAtStartOfMarker file "(*Mupcast*)"
        AssertCompListContainsAll(completions, ["Name"; "Speak"])
        
        let completions = DotCompletionAtStartOfMarker file "(*Mdowncast*)"
        AssertCompListContainsAll(completions, ["dog"; "Name"])
        
        let completions = DotCompletionAtStartOfMarker file "(*Mconstrainedtoint*)"
        AssertCompListContainsAll(completions, ["ToString"])    

    [<Test>]
    member this.``Literal.Float``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = "let myfloat = (42.0)(*Mconstantfloat*)",
            marker = "(*Mconstantfloat*)",
            list = ["GetType"; "ToString"])
            
    [<Test>] 
    member this.``Literal.String``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """let name = "foo"(*Mconstantstring*)""",
            marker = "(*Mconstantstring*)",
            list = ["Chars"; "Clone"]) 
            
    [<Test>]
    member this.``Literal.Int``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = "let typeint = (10)(*Mint*)",
            marker = "(*Mint*)",
            list = ["GetType";"ToString"])

    [<Test>]
    member this.``Identifier.InLambdaExpression``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = "let funcLambdaExp = fun (x:int)-> x(*MarkerinLambdaExp*)",
            marker = "(*MarkerinLambdaExp*)",
            list = ["ToString"; "Equals"])

    [<Test>]
    member this.``Identifier.InClass``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                type ClassLetBindIn(x:int) = 
                    let m_field = x(*MarkerLetBindinClass*) """,
            marker = "(*MarkerLetBindinClass*)",
            list = ["ToString"; "Equals"])

    [<Test>]
    member this.``Identifier.InNestedLetBind``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = "
let funcNestedLetBinding (x:int) = 
    let funcNested (x:int) = x(*MarkerNestedLetBind*)
    () 
",
            marker = "(*MarkerNestedLetBind*)",
            list = ["ToString"; "Equals"]) 

    [<Test>]
    member this.``Identifier.InModule``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = "
module ModuleLetBindIn =
    let f (x:int) = x(*MarkerLetBindinModule*)
",
            marker = "(*MarkerLetBindinModule*)",
            list = ["ToString"; "Equals"])

    [<Test>]
    member this.``Identifier.InMatchStatement``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = "
let x = 1
match x(*MarkerMatchStatement*) with
    |1 -> 1*1
    |2 -> 2*2

",
            marker = "(*MarkerMatchStatement*)",
            list = ["ToString"; "Equals"]) 

    [<Test>]
    member this.``Identifier.InMatchClause``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = "
let rec f l = 
    match l with
    | [] ->
        let xx = System.DateTime.Now
        let y = xx(*MarkerMatchClause*)
        ()
    | x :: xs -> f xs
",
            marker = "(*MarkerMatchClause*)",
            list = ["Add";"Date"]) 
                           
    [<Test>]
    member this.``Expression.ListItem``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                let a = [1;2;3]
                a.[1](*MarkerListItem*)
                """,
            marker = "(*MarkerListItem*)",
            list = ["CompareTo"; "ToString"])

    [<Test>]
    member this.``Expression.FunctionParameter``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                let f (x : string) = ()
                f ("1" + "1")(*MarkerParameter*)
                """,
            marker = "(*MarkerParameter*)",
            list = ["CompareTo"; "ToString"])

    [<Test>]
    member this.``Expression.Function``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                let func(mm) = 100
                func(x + y)(*MarkerFunction*)
                """,
            marker = "(*MarkerFunction*)",
            list = ["CompareTo"; "ToString"])

    [<Test>]
    member this.``Expression.RecordPattern``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                type Rec = 
                    { X : int} 
                    member this.Value = 42
                { X = 1 }(*MarkerRecordPattern*)
                """,
            marker = "(*MarkerRecordPattern*)",
            list = ["Value"; "ToString"])

    [<Test>]
    member this.``Expression.2DArray``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                let (a2: int[,]) = Array2.zero_create 10 10
                a2.[1,2](*Marker2DArray*) 
                """,
            marker = "(*Marker2DArray*)",
            list = ["ToString"]) 

    [<Test>]
    member this.``Expression.LetBind``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                let f (x : string) = ()
                //And in many different contexts where the ??tomic expression??occurs at the end of the expression, e.g.
                let x = y in f ("1" + "1")(*MarkerContext1*)
                """,
            marker = "(*MarkerContext1*)",
            list = ["CompareTo";"ToString"]) 

    [<Test>]
    member this.``Expression.WhileLoop``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                let f (x : string) = ()
                while true do
                    f ("1" + "1")(*MarkerContext3*) 
                """,
            marker = "(*MarkerContext3*)",
            list = ["CompareTo";"ToString"])  

    [<Test>] 
    member this.``Expression.List``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """[1;2](*MarkerList*)   """,
            marker = "(*MarkerList*)",
            list = ["Head"; "Item"])

    [<Test>]
    member this.``Expression.Nested.InLetBind``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                let f (x : string) = ()
                // Nested expressions
                let x = 42 |> ignore; f ("1" + "1")(*MarkerNested1*)
                """,
            marker = "(*MarkerNested1*)",
            list = ["Chars";"Length"])

    [<Test>]
    member this.``Expression.Nested.InWhileLoop``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                let f (x : string) = ()
                while true do
                    ignore (f ("1" + "1")(*MarkerNested2*)) 
                """,
            marker = "(*MarkerNested2*)",
            list = ["Chars";"Length"])  

    [<Test>]
    member this.``Expression.ArrayItem.Positive``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                //regression test for bug 1001
                let str1 = Array.init 10 string
                str1.[1](*MarkerArrayIndexer*)""",
            marker = "(*MarkerArrayIndexer*)",
            list = ["Chars";"Split"])

    [<Test>]
    member this.``Expression.ArrayItem.Negative``() =
        this.VerifyDotCompListDoesNotContainAnyAtStartOfMarker(
            fileContents = """
                //regression test for bug 1001
                let str1 = Array.init 10 string
                str1.[1](*MarkerArrayIndexer*)""",
            marker = "(*MarkerArrayIndexer*)",
            list = ["IsReadOnly";"Rank"])
                                                                                                                                                                
    [<Test>]
    member this.``ObjInstance.InheritedClass.MethodsDefInBase``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                type Pet() = 
                    member x.Name() = "pet"
                    member x.Speak() = "this is a pet"    
                type Dog() = 
                    inherit Pet() 
                    member x.dog() = "this is a dog"    
                let dog = new Dog()
                dog(*Mderived*)""",
            marker = "(*Mderived*)",
            list = ["Name"; "dog"])

    [<Test>]
    member this.``ObjInstance.AnonymousClass.MethodsDefInInterface``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                type IFoo =
                    abstract DoStuff  : unit -> string
                    abstract DoStuff2 : int * int -> string -> string
                // Implement an interface in a class (This is kind of lame if you don't want to actually declare a class)
                type Foo() =
                    interface IFoo with
                        member this.DoStuff () = "Return a string"
                        member this.DoStuff2 (x, y) z = sprintf "Arguments were (%d, %d) %s" x y z
                // instanceOfIFoo is an instance of an anonomyous class which implements IFoo
                let instanceOfIFoo = {
                                        new IFoo with
                                            member this.DoStuff () = "Implement IFoo"
                                            member this.DoStuff2 (x, y) z = sprintf "Arguments were (%d, %d) %s" x y z
                                     }(*Mexpnewtype*)""",
            marker = "(*Mexpnewtype*)",
            list = ["DoStuff"; "DoStuff2"])
             
    [<Test>]
    member this.``SimpleTypes.SystemTime``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                let typestruct = System.DateTime.Now
                typestruct(*Mstruct*)""",
            marker = "(*Mstruct*)",
            list = ["AddDays"; "Date"])

    [<Test>]
    member this.``SimpleTypes.Record``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                type Person = { Name: string; DateOfBirth: System.DateTime }
                let typrecord = { Name = "Bill"; DateOfBirth = new System.DateTime(1962,09,02) }
                typrecord(*Mrecord*)""",
            marker = "(*Mrecord*)",
            list = ["DateOfBirth"; "Name"]) 

    [<Test>]
    member this.``SimpleTypes.Enum``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                type weekday = 
                    | Monday = 1
                    | Tuesday = 2
                    | Wednesday = 3
                    | Thursday = 4
                    | Friday = 5
                let typeenum = weekday.Friday
                typeenum(*Menum*)""",
            marker = "(*Menum*)",
            list = ["GetType"; "ToString"])

    [<Test>]
    member this.``SimpleTypes.DisUnion``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                type Route = int
                type Make = string
                type Model = string
                type Transport =
                    | Car of Make * Model
                    | Bicycle
                    | Bus of Route
                let typediscriminatedunion = Car("BMW","360")
                typediscriminatedunion(*Mdiscriminatedunion*)""",
            marker = "(*Mdiscriminatedunion*)",
            list = ["GetType"; "ToString"])  

    [<Test>]
    member this.``InheritedClass.BaseClassPrivateMethod.Negative``() =
        this.VerifyDotCompListDoesNotContainAnyAtStartOfMarker(
            fileContents = """
                open System
                //difine the base class
                type Widget() = 
                    let mutable state = 0 
                    member internal x.MethodInternal() = state 
                    member public x.MethodPublic(n) = state <- state + n
                    member private x.MethodPrivate() = (state <> 0)
                    [<DefaultValue>]
                    val mutable internal fieldInternal:int 
                    [<DefaultValue>]
                    val mutable public fieldPublic:int
                    [<DefaultValue>]
                    val mutable private fieldPrivate:int 
                //define the divided class which inherent "Widget"
                type Divided() =
                    inherit Widget() 
                    member x.myPrint() = 
                        base(*MUnShowPrivate*)         
                Console.ReadKey(true)""" ,
            marker = "(*MUnShowPrivate*)",
            list = ["MethodPrivate";"fieldPrivate"]) 

    [<Test>]
    member this.``InheritedClass.BaseClassPublicMethodAndProperty``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                open System
                //difine the base class
                type Widget() = 
                    let mutable state = 0 
                    member internal x.MethodInternal() = state 
                    member public x.MethodPublic(n) = state <- state + n
                    member private x.MethodPrivate() = (state <> 0)
                    [<DefaultValue>]
                    val mutable internal fieldInternal:int 
                    [<DefaultValue>]
                    val mutable public fieldPublic:int
                    [<DefaultValue>]
                    val mutable private fieldPrivate:int   
                //define the divided class which inherent "Widget"
                type Divided() =
                    inherit Widget() 
                    member x.myPrint() = 
                        base(*MShowPublic*) 
                Console.ReadKey(true)""",
            marker = "(*MShowPublic*)",
            list = ["MethodPublic";"fieldPublic"])

    [<Test>]
    member this.``Visibility.InternalNestedClass.Negative``() =
        this.VerifyDotCompListDoesNotContainAnyAtStartOfMarker(
            fileContents = """System.Console(*Marker1*)""",
            marker = "(*Marker1*)",
            list = ["ControlCDelegateData"])

    [<Test>]
    member this.``Visibility.PrivateIdentifierInDiffModule.Negative``() = 
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """
                module Module1 =
                    let private fieldPrivate = 1    
                    let private MethodPrivate x = 
                        x+1    
                    type private TypePrivate()=
                        member this.mem = 1 
                module Module2 =
                    Module1(*Marker1*)  """,
            marker = "(*Marker1*)")

    [<Test>]
    member this.``Visibility.PrivateIdentifierInDiffClass.Negative``() = 
        this.VerifyDotCompListDoesNotContainAnyAtStartOfMarker(
            fileContents = """
                open System
                module Module1 =
                    type Type1()=           
                        [<DefaultValue>]
                        val mutable private fieldPrivate:int            
                        member private x.MethodPrivate() = 1         
                    type Type2()=
                        let M1=        
                        let type1 = new Type1()                
                        type1(*MarkerOutType*) """,
            marker = "(*MarkerOutType*)",
            list = ["fieldPrivate";"MethodPrivate"]) 

    [<Test>]
    member this.``Visibility.PrivateFieldInSameClass``() =  
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                open System

                module Module1 =
                    type Type1()=           
                        [<DefaultValue>]
                        val mutable private PrivateField:int            
                        static member private PrivateMethod() = 1         
                        member this.Field1 with get () = this(*MarkerFieldInType*)         
                        member x.MethodTest() = Type1(*MarkerMethodInType*)         
                    let type1 = new Type1() """,
            marker = "(*MarkerFieldInType*)",
            list = ["PrivateField"]) 

    [<Test>]
    member this.``Visibility.PrivateMethodInSameClass``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                open System

                module Module1 =
                    type Type1()=           
                        [<DefaultValue>]
                        val mutable private PrivateField:int            
                        static member private PrivateMethod() = 1         
                        member this.Field1 with get () = this(*MarkerFieldInType*)         
                        member x.MethodTest() = Type1(*MarkerMethodInType*)         
                    let type1 = new Type1() """,
            marker = "(*MarkerMethodInType*)",
            list = ["PrivateMethod"])       

    [<Test>]
    member this.``VariableIdentifier.AsParameter``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                module MyModule =
                    type DuType =
                         | Tag of int         
                    let f (DuType(*Maftervariable1*).Tag(x)) = 10 """,
            marker = "(*Maftervariable1*)",
            list = ["Tag"])

    [<Test>]
    member this.``VariableIdentifier.InMeasure.DefineInDiffNamespace``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                namespace MyNamespace1
                module MyModule =
                    type DuType =
                         | Tag of int         
                    let f (DuType(*Maftervariable1*).Tag(x)) = 10 
                    type Pet() = 
                        member x.Name = "pet"
                        member x.Speak() = "this is a pet"
                    type Dog() = 
                        inherit Pet()
                        do base(*Maftervariable3*).GetType()
                    let dog = new Dog()
                namespace MyNamespace2
                module MyModule2 = 
                    let typeFunc<[<MyNamespace1.MyModule(*Maftervariable2*)>] 'a> = [1; 2; 3]
                    let f (x:MyNamespace1.MyModule(*Maftervariable4*)) = 10
                    let y = int System.IO(*Maftervariable5*)""",
            marker = "(*Maftervariable2*)",
            list = [])

    [<Test>]
    member this.``VariableIdentifier.MethodsInheritFomeBase``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                namespace MyNamespace1
                module MyModule =
                    type DuType =
                         | Tag of int         
                    let f (DuType(*Maftervariable1*).Tag(x)) = 10 
                    type Pet() = 
                        member x.Name = "pet"
                        member x.Speak() = "this is a pet"
                    type Dog() = 
                        inherit Pet()
                        do base(*Maftervariable3*).GetType()
                    let dog = new Dog()""",
            marker = "(*Maftervariable3*)",
            list = ["Name";"Speak"])

    [<Test>]
    member this.``VariableIdentifier.AsParameter.DefineInDiffNamespace``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                namespace MyNamespace1
                module MyModule =
                    type DuType =
                         | Tag of int         
                    let f (DuType(*Maftervariable1*).Tag(x)) = 10 
                    type Pet() = 
                        member x.Name = "pet"
                        member x.Speak() = "this is a pet"
                    type Dog() = 
                        inherit Pet()
                        do base(*Maftervariable3*).GetType()
                    let dog = new Dog()
                namespace MyNamespace2
                module MyModule2 = 
                    let typeFunc<MyNamespace1.MyModule(*Maftervariable2*)> = [1; 2; 3]
                    let f (x:MyNamespace1.MyModule(*Maftervariable4*)) = 10
                    let y = int System.IO(*Maftervariable5*)""",
            marker = "(*Maftervariable4*)",
            list = ["DuType"])  

    [<Test>]
    member this.``VariableIdentifier.SystemNamespace``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                namespace MyNamespace1
                module MyModule =
                    type DuType =
                         | Tag of int         
                    let f (DuType(*Maftervariable1*).Tag(x)) = 10 
                    type Pet() = 
                        member x.Name = "pet"
                        member x.Speak() = "this is a pet"
                    type Dog() = 
                        inherit Pet()
                        do base(*Maftervariable3*).GetType()
                    let dog = new Dog()
                namespace MyNamespace2
                module MyModule2 = 
                    let typeFunc<MyNamespace1.MyModule(*Maftervariable2*)> = [1; 2; 3]
                    let f (x:MyNamespace1.MyModule(*Maftervariable4*)) = 10
                    let y = int System.IO(*Maftervariable5*)""",
            marker = "(*Maftervariable5*)",
            list = ["BinaryReader";"Stream";"Directory"]) 

    [<Test>]
    member this.``LongIdent.AsAttribute``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                [<System(*Mattribute*)>]
                type TestAttribute() = 
                    member x.print() = "print" """,
            marker = "(*Mattribute*)",
            list = ["Obsolete"])

    [<Test>]
    member this.``ImportStatment.System.ImportDirectly``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                open System(*Mimportstatement1*)
                open IO = System(*Mimportstatement2*)""",
            marker = "(*Mimportstatement1*)",
            list = ["Collections"])

    [<Test>]
    member this.``ImportStatment.System.ImportAsIdentifier``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                open System(*Mimportstatement1*)
                open IO = System(*Mimportstatement2*)""",
            marker = "(*Mimportstatement2*)",
            list = ["IO"])  

    [<Test>]
    member this.``LongIdent.PatternMatch.AsVariable.DefFromDiffNamespace``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                namespace NS
                module longident =
                    type Direction =
                        | Left         = 1
                        | Right        = 2
                    type MoveCursor() = 
                        member this.Direction = Direction.Left
                namespace NS2       
                module test =
                    let cursor = new NS.longident.MoveCursor()
                    match cursor(*Mpatternmatch1*) with
                    | NS.longident.Direction.Left -> "move left"
                    | NS(*Mpatternmatch2*) -> "move right" """,
            marker = "(*Mpatternmatch1*)",
            list = ["Direction";"ToString"])

    [<Test>]
    member this.``LongIdent.PatternMatch.AsConstantValue.DefFromDiffNamespace``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                namespace NS
                module longident =
                    type Direction =
                        | Left         = 1
                        | Right        = 2
                    type MoveCursor() = 
                        member this.Direction = Direction.Left
                namespace NS2       
                module test =
                    let cursor = new NS.longident.MoveCursor()
                    match cursor(*Mpatternmatch1*) with
                    | NS.longident.Direction.Left -> "move left"
                    | NS(*Mpatternmatch2*) -> "move right" """,
            marker = "(*Mpatternmatch2*)",
            list = ["longident"])

    [<Test>]
    member this.``LongIdent.PInvoke.AsReturnType``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                open System.IO
                open System.Runtime.InteropServices
                // Get two temp files, write data into one of them
                let tempFile1, tempFile2 = tryCreateTemporaryFileName , tryCreateTemporaryFileName ()
                let writer = new StreamWriter (tempFile1)
                writer.WriteLine("Some Data")
                writer.Close()
                // Origional signature
                //[<DllImport("kernel32.dll")>]
                //extern bool CopyFile(string lpExistingFileName, string lpNewFileName, bool bFailIfExists);
                [<DllImport("kernel32.dll", EntryPoint="CopyFile")>]
                extern System(*Mpinvokereturntype*) CopyFile_Arrays(char[] lpExistingFileName, char[] lpNewFileName, bool bFailIfExists);
                let result = CopyFile_Arrays(tempFile1.ToCharArray(), tempFile2.ToCharArray(), false)
                printfn "Array %A" result""",
            marker = "(*Mpinvokereturntype*)",
            list = ["Boolean";"Int32"])

    [<Test>]
    member this.``LongIdent.PInvoke.AsAttribute``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                open System.IO
                open System.Runtime.InteropServices

                module mymodule = 
                    type SomeAttrib() = 
                        inherit System.Attribute() 
                    type myclass() = 
                        member x.name() = "test case"
                module mymodule2 =    
                    [<DllImport("kernel32.dll", EntryPoint="CopyFile")>]
                    extern bool CopyFile_Attrib([<mymodule(*Mpinvokeattribute*)>] char [] lpExistingFileName, char []lpNewFileName, [<mymodule.SomeAttrib>] bool & bFailIfExists);

                    let result5 = CopyFile_Arrays(tempFile1.ToCharArray(), tempFile2.ToCharArray(), false)
                    printfn "WithAttribute %A" result5""",
            marker = "(*Mpinvokeattribute*)",
            list = ["SomeAttrib"]) 

    [<Test>]
    member this.``LongIdent.PInvoke.AsParameterType``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                open System.IO
                open System.Runtime.InteropServices
                [<DllImport("kernel32.dll", EntryPoint="CopyFile")>]
                extern bool CopyFile_ArraySpaces(char [] lpExistingFileName, char []lpNewFileName, System(*Mpinvokeparametertype*) bFailIfExists);
                let result2 = CopyFile_Arrays(tempFile1.ToCharArray(), tempFile2.ToCharArray(), false)
                printfn "Array Space %A" result2""",
            marker = "(*Mpinvokeparametertype*)",
            list = ["Boolean";"Int32"])

    [<Test>]
    member this.``LongIdent.Record.AsField``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                module MyModule =
                    type person =
                        { name: string;
                        dateOfBirth: System.DateTime; }
                module MyModule2 =     
                    let x = {MyModule(*Mrecord*) = 32}""",
            marker = "(*Mrecord*)",
            list = ["person"])   

    [<Test>]
    member this.``LongIdent.DiscUnion.AsTypeParameter.DefInDiffNamespace``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                namespace MyNamespace1
                module MyModule =
                    type DuType =
                         | Tag of int          
                    type Pet() = 
                        member x.Name = "pet"
                        member x.Speak() = "this is a pet"  
                    type Dog() = 
                        inherit Pet() 
                namespace MyNamespace2
                module MyModule2 =     
                    let foo = MyNamespace1.MyModule(*Mtypeparameter1*)
                    let f (x:int) = MyNamespace1.MyModule.DuType(*Mtypeparameter2*)    
                    let typeFunc<[<MyNamespace1.MyModule(*Mtypeparameter3*)>] 'a> = 10""",
            marker = "(*Mtypeparameter1*)",
            list = ["Dog";"DuType"])

    [<Test>]
    member this.``LongIdent.AnonymousFunction.AsTypeParameter.DefFromDiffNamespace``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                namespace MyNamespace1
                module MyModule =
                    type DuType =
                         | Tag of int          
                    type Pet() = 
                        member x.Name = "pet"
                        member x.Speak() = "this is a pet"  
                    type Dog() = 
                        inherit Pet() 
                namespace MyNamespace2
                module MyModule2 =     
                    let foo = MyNamespace1.MyModule(*Mtypeparameter1*)
                    let f (x:int) = MyNamespace1.MyModule.DuType(*Mtypeparameter2*)    
                    let typeFunc<[<MyNamespace1.MyModule(*Mtypeparameter3*)>] 'a> = 10""",
            marker = "(*Mtypeparameter2*)",
            list = ["Tag"])

    [<Test>]
    member this.``LongIdent.UnitMeasure.AsTypeParameter.DefFromDiffNamespace``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                namespace MyNamespace1
                module MyModule =
                    type DuType =
                         | Tag of int          
                    type Pet() = 
                        member x.Name = "pet"
                        member x.Speak() = "this is a pet"  
                    type Dog() = 
                        inherit Pet() 
                namespace MyNamespace2
                module MyModule2 =     
                    let foo = MyNamespace1.MyModule(*Mtypeparameter1*)
                    let f (x:int) = MyNamespace1.MyModule.DuType(*Mtypeparameter2*)    
                    let typeFunc<[<MyNamespace1.MyModule(*Mtypeparameter3*)>] 'a> = 10""",
            marker = "(*Mtypeparameter3*)",
            list = [])

    [<Test>]
    member this.``RedefinedIdentifier.DiffScope.InScope.Positive``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                let identifierBothScope = ""
                let functionScope () =
                    let identifierBothScope = System.DateTime.Now
                    identifierBothScope(*MarkerShowLastOneWhenInScope*)
                identifierBothScope(*MarkerShowLastOneWhenOutscoped*)""",
            marker = "(*MarkerShowLastOneWhenInScope*)",
            list = ["DayOfWeek"])  

    [<Test>]
    member this.``RedefinedIdentifier.DiffScope.InScope.Negative``() =
        this.VerifyDotCompListDoesNotContainAnyAtStartOfMarker(
            fileContents = """
                let identifierBothScope = ""
                let functionScope () =
                    let identifierBothScope = System.DateTime.Now
                    identifierBothScope(*MarkerShowLastOneWhenInScope*)
                identifierBothScope(*MarkerShowLastOneWhenOutscoped*)""",
            marker = "(*MarkerShowLastOneWhenInScope*)",
            list = ["Chars"]) 

    [<Test>]
    member this.``RedefinedIdentifier.DiffScope.OutScope.Positive``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                let identifierBothScope = ""
                let functionScope () =
                    let identifierBothScope = System.DateTime.Now
                    identifierBothScope(*MarkerShowLastOneWhenInScope*)
                identifierBothScope(*MarkerShowLastOneWhenOutscoped*)""",
            marker = "(*MarkerShowLastOneWhenOutscoped*)",
            list = ["Chars"])

    [<Test>]
    member this.``ObjInstance.ExtensionMethods.WithoutDef.Negative``() =
        this.VerifyDotCompListDoesNotContainAnyAtStartOfMarker(
            fileContents = """
                open System
                let rnd = new System.Random()
                rnd(*MWithoutReference*)""",
            marker = "(*MWithoutReference*)",
            list = ["NextDice";"DiceValue"])

    [<Test>]
    member this.``Class.DefInDiffNameSpace``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                namespace NS1
                module  MyModule =
                    [<System.ObsoleteAttribute>]
                    type ObsoleteType() = 
                        member this.TestMethod() = 10        
                    [<CompilerMessage("This construct is for ML compatibility.", 62, IsHidden=true)>]
                    type CompilerMesageType() = 
                        member this.TestMethod() = 10
                    type TestType() = 
                        member this.TestMethod() = 100
                        [<System.ObsoleteAttribute>]
                        member this.ObsoleteMethod() = 100
                        [<CompilerMessage("This construct is for ML compatibility.", 62, IsHidden=true)>]
                        member this.CompilerMesageMethod() = 100
                        [<CompilerMessage("This construct is hidden", 1023, IsHidden=true)>]
                        member this.HiddenMethod() = 10
                        [<CompilerMessage("This construct is not hidden", 1023, IsHidden=false)>]
                        member this.VisibleMethod() = 10
                        [<CompilerMessage("This construct is not hidden", 1023)>]
                        member this.VisibleMethod2() = 10
                namespace NS2
                module m2 =
                    type x = NS1.MyModule(*MarkerType*)
                    let b = (new NS1.MyModule.TestType())(*MarkerMethod*)
                """,
            marker = "(*MarkerType*)" ,
            list = ["TestType"])

    [<Test>]
    member this.``Class.DefInDiffNameSpace.WithAttributes.Negative``() = 
        this.VerifyDotCompListDoesNotContainAnyAtStartOfMarker(
            fileContents = """
                namespace NS1
                module  MyModule =
                    [<System.ObsoleteAttribute>]
                    type ObsoleteType() = 
                        member this.TestMethod() = 10        
                    [<CompilerMessage("This construct is for ML compatibility.", 62, IsHidden=true)>]
                    type CompilerMesageType() = 
                        member this.TestMethod() = 10
                    type TestType() = 
                        member this.TestMethod() = 100
                        [<System.ObsoleteAttribute>]
                        member this.ObsoleteMethod() = 100
                        [<CompilerMessage("This construct is for ML compatibility.", 62, IsHidden=true)>]
                        member this.CompilerMesageMethod() = 100
                        [<CompilerMessage("This construct is hidden", 1023, IsHidden=true)>]
                        member this.HiddenMethod() = 10
                        [<CompilerMessage("This construct is not hidden", 1023, IsHidden=false)>]
                        member this.VisibleMethod() = 10
                        [<CompilerMessage("This construct is not hidden", 1023)>]
                        member this.VisibleMethod2() = 10
                namespace NS2
                module m2 =
                    type x = NS1.MyModule(*MarkerType*)
                    let b = (new NS1.MyModule.TestType())(*MarkerMethod*)
                """,
            marker = "(*MarkerType*)",
            list = ["ObsoleteType";"CompilerMesageType"])

    [<Test>]
    member this.``Method.DefInDiffNameSpace``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                namespace NS1
                module  MyModule =
                    [<System.ObsoleteAttribute>]
                    type ObsoleteType() = 
                        member this.TestMethod() = 10        
                    [<CompilerMessage("This construct is for ML compatibility.", 62, IsHidden=true)>]
                    type CompilerMesageType() = 
                        member this.TestMethod() = 10
                    type TestType() = 
                        member this.TestMethod() = 100
                        [<System.ObsoleteAttribute>]
                        member this.ObsoleteMethod() = 100
                        [<CompilerMessage("This construct is for ML compatibility.", 62, IsHidden=true)>]
                        member this.CompilerMesageMethod() = 100
                        [<CompilerMessage("This construct is hidden", 1023, IsHidden=true)>]
                        member this.HiddenMethod() = 10
                        [<CompilerMessage("This construct is not hidden", 1023, IsHidden=false)>]
                        member this.VisibleMethod() = 10
                        [<CompilerMessage("This construct is not hidden", 1023)>]
                        member this.VisibleMethod2() = 10
                namespace NS2
                module m2 =
                type x = NS1.MyModule(*MarkerType*)
                let b = (new NS1.MyModule.TestType())(*MarkerMethod*)
                """,
            marker = "(*MarkerMethod*)",
            list = ["TestMethod";"VisibleMethod";"VisibleMethod2"])

    [<Test>]
    member this.``Method.DefInDiffNameSpace.WithAttributes.Negative``() =
        this.VerifyDotCompListDoesNotContainAnyAtStartOfMarker(
            fileContents = """
                namespace NS1
                module  MyModule =
                    [<System.ObsoleteAttribute>]
                    type ObsoleteType() = 
                        member this.TestMethod() = 10        
                    [<CompilerMessage("This construct is for ML compatibility.", 62, IsHidden=true)>]
                    type CompilerMesageType() = 
                        member this.TestMethod() = 10
                    type TestType() = 
                        member this.TestMethod() = 100
                        [<System.ObsoleteAttribute>]
                        member this.ObsoleteMethod() = 100
                        [<CompilerMessage("This construct is for ML compatibility.", 62, IsHidden=true)>]
                        member this.CompilerMesageMethod() = 100
                        [<CompilerMessage("This construct is hidden", 1023, IsHidden=true)>]
                        member this.HiddenMethod() = 10
                        [<CompilerMessage("This construct is not hidden", 1023, IsHidden=false)>]
                        member this.VisibleMethod() = 10
                        [<CompilerMessage("This construct is not hidden", 1023)>]
                        member this.VisibleMethod2() = 10
                namespace NS2
                module m2 =
                type x = NS1.MyModule(*MarkerType*)
                let b = (new NS1.MyModule.TestType())(*MarkerMethod*)""",
            marker = "(*MarkerMethod*)",
            list = ["ObsoleteMethod";"CompilerMesageMethod";"HiddenMethod"])

    [<Test>]
    member this.``ObjInstance.ExtensionMethods.WithDef.Positive``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                open System

                type System.Random with
                    member this.NextDice() = true
                    member this.DiceValue = 6

                let rnd = new System.Random()
                rnd(*MWithReference*)""",
            marker = "(*MWithReference*)",
            list = ["NextDice";"DiceValue"]) 

    [<Test>]
    member this.``Keywords.If``() =
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """
                if(*MarkerKeywordIf*) true then
                    () """,
            marker ="(*MarkerKeywordIf*)")

    [<Test>]
    member this.``Keywords.Let``() =
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """let(*MarkerKeywordLet*) a = 1""",
            marker = "(*MarkerKeywordLet*)")

    [<Test>]
    member this.``Keywords.Match``() =
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """
                match(*MarkerKeywordMatch*) a with
                    | pattern -> exp""",
            marker = "(*MarkerKeywordMatch*)")

    [<Test>]
    member this.``MacroDirectives.nowarn``() =
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """#nowarn(*MarkerPreProcessNowarn*)""",
            marker = "(*MarkerPreProcessNowarn*)")

    [<Test>]
    member this.``MacroDirectives.light``() = 
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """#light(*MarkerPreProcessLight*)""",
            marker = "(*MarkerPreProcessLight*)") 

    [<Test>]
    member this.``MacroDirectives.define``() =
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """#define(*MarkerPreProcessDefine*)""",
            marker = "(*MarkerPreProcessDefine*)")

    [<Test>]
    member this.``MacroDirectives.PreProcessDefine``() =
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """#define Foo(*MarkerPreProcessDefineConst*)""",
            marker = "(*MarkerPreProcessDefineConst*)")

    [<Test>]
    member this.``Identifier.InClass.WithoutDef``() =
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """
                type Type2 =
                    val mutable x(*MarkerValue*) : string""",
            marker = "(*MarkerValue*)")

    [<Test>]
    member this.``Identifier.InDiscUnion.WithoutDef``() =
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """
                type DUTag =
                    |Tag(*MarkerDU*) of int""",
            marker = "(*MarkerDU*)") 

    [<Test>]
    member this.``Identifier.InRecord.WithoutDef``() =
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """type Rec =  { X(*MarkerRec*) : int }""",
            marker = "(*MarkerRec*)")
            
    [<Test>]
    member this.``Identifier.AsNamespace``() =
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """namespace Namespace1(*MarkerNamespace*)""",
            marker = "(*MarkerNamespace*)")

    [<Test>]
    member this.``Identifier.AsModule``() =
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """module Module1(*MarkerModule*)""",
            marker = "(*MarkerModule*)")

    [<Test>]
    member this.``Identifier.WithouDef``() = 
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """ abcd(*MarkerUndefinedIdentifier*)  """,
            marker = "(*MarkerUndefinedIdentifier*)") 

    [<Test>]
    member this.``Identifier.InMatch.UnderScore``() = 
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """
                let x = 1
                match x with 
                    |1 -> 1*2
                    |2 -> 2*2
                    |_(*MarkerIdentifierIsUnderScore*) -> 0 """,
            marker = "(*MarkerIdentifierIsUnderScore*)") 

    [<Test>]
    member this.MemberSelf() =
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """
                type Foo() = 
                    member this(*Mmemberself*)""",
            marker = "(*Mmemberself*)")   

    [<Test>]
    member this.``Expression.InComment``() =
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """
                //open System
                //open IO = System(*Mcomment*)""",
            marker = "(*Mcomment*)")

    [<Test>]
    member this.``Expression.InString``() =
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """let x = "System.Console(*Minstring*)" """,
            marker = "(*Minstring*)")

    // Regression test for 1067 -- Completion lists don't work after generic arguments  - for generic functions and for static members of generic types
    [<Test>]
    member this.``Regression1067.InstanceOfGenericType``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                type GT<'a> =
                    static member P = 12
                    static member Q = 13

                let _ = GT<int>(*Marker1*)
                type gt_int = GT<int>
                gt_int(*Marker2*)

                type D =
                 class
                 end

                let x = typeof<D>(*Marker3*)
                let y = typeof<D>
                y(*Marker4*)
                """,
            marker = "(*Marker2*)",
            list = ["P"; "Q"]) 

    [<Test>]
    member this.``Regression1067.ClassUsingGeniricTypeAsAttribute``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                type GT<'a> =
                    static member P = 12
                    static member Q = 13

                let _ = GT<int>(*Marker1*)
                type gt_int = GT<int>
                gt_int(*Marker2*)

                type D =
                 class
                 end

                let x = typeof<D>(*Marker3*)
                let y = typeof<D>
                y(*Marker4*)
                """,
            marker = "(*Marker4*)",
            list = ["Assembly"; "FullName"; "GUID"])

    [<Test>] 
    member this.NoInfiniteLoopInProperties() =
        let fileContents = """
                open System.Windows.Forms

                let tn = new TreeNode("")

                tn.Nodes(*Marker1*)"""
        let (solution, project, file) = this.CreateSingleFileProject(fileContents, references = ["System.Windows.Forms"])

        let completions = DotCompletionAtStartOfMarker file "(*Marker1*)"
        AssertCompListDoesNotContainAny(completions, ["Nodes"])
    
    // Regression for bug 3225 -- Invalid intellisense when inside of a quotation
    [<Test>]
    member this.``Regression3225.Identifier.InQuotation``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                let _ = <@ let x = "foo"
                           x(*Marker*) @>""",
           marker = "(*Marker*)",
           list = ["Chars"; "Length"])

    // Regression for bug 1911 -- No completion list of expr in match statement
    [<Test>]
    member this.``Regression1911.Expression.InMatchStatement``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                type Thingey = { A : bool; B : int }

                let test = match (List.head [{A = true; B = 0}; {A = false; B = 1}])(*Marker*)""",
            marker = "(*Marker*)",
            list = ["A"; "B"])

          
    // Bug 3627 - Completion lists should be filtered in many contexts
    // This blocks six testcases and is slated for Dev11, so these will be disabled for some time.
    [<Test>]
    [<Ignore("Bug 3627 - Completion lists should be filtered in many contexts")>] 
    member this.AfterTypeParameter() =
        let fileContents = """
            type Type1 = Tag of string(*MarkerDUTypeParam*)

            let f x:int -> string(*MarkerParamFunction*)

            let Type2<'a(*MarkerGenericParam*)> = 1
   
            let type1 = typeof<int(*MarkerParamTypeof*)>
            """
        let (solution, project, file) = this.CreateSingleFileProject(fileContents)

        //Completion list Not comes up after DUType parameter
        let completions = DotCompletionAtStartOfMarker file "(*MarkerDUTypeParam*)"
        AssertCompListIsEmpty(completions)

        //Completion list Not comes up after function parameter
        let completions = DotCompletionAtStartOfMarker file "(*MarkerParamFunction*)"
        AssertCompListIsEmpty(completions)

        //Completion list Not comes up after generic parameter
        let completions = DotCompletionAtStartOfMarker file "(*MarkerGenericParam*)"
        AssertCompListIsEmpty(completions)

        //Completion list Not comes up after parameter in typeof
        let completions = DotCompletionAtStartOfMarker file "(*MarkerParamTypeof*)"
        AssertCompListIsEmpty(completions)

    [<Test>]
    member this.``Identifier.AsClassName.Uninitial``() =
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """
                type f1(*MarkerType*) = 
                    val field : int""",
            marker = "(*MarkerType*)")

    [<Test>]
    member this.``Identifier.AsFunctionName.UnInitial``() =
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """let f2(*MarkerFunctionIndentifier*) x = x+1 """,
            marker = "(*MarkerFunctionIndentifier*)")

    [<Test>]
    member this.``Identifier.AsParameter.UnInitial``() =
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """ let f3 x(*MarkerParam*) = x+1""",
            marker = "(*MarkerParam*)")

    [<Test>]
    member this.``Identifier.AsFunctionName.UsingfunKeyword``() =
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """fun f4(*MarkerFunctionDeclaration*)  x -> x+1""",
            marker = "(*MarkerFunctionDeclaration*)")

    [<Test>]
    member public this.``Identifier.EqualityConstraint.Bug65730``() =  
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """let g3<'a when 'a : equality> (x:'a) = x(*Marker*)""",
            marker = "(*Marker*)",
            list = ["Equals"; "GetHashCode"]) // equality constraint should make these show up
 
    [<Test>]
    [<Ignore("this no longer works, but I'm unclear why - now you get all the top-level completions")>]
    member this.``Identifier.InFunctionMatch``() =
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """
                let f5 = function
                    | 1(*MarkerFunctionMatch*) -> printfn "1"
                    | 2 -> printfn "2" """,
            marker = "(*MarkerFunctionMatch*)")

    [<Test>]
    member this.``Identifier.This``() =
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """
                type Type1 = 
                    member this(*MarkerMemberThis*).Foo () = 3""",
            marker = "(*MarkerMemberThis*)")

    [<Test>]
    member this.``Identifier.AsProperty``() =
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """
                type Type2 =
                    member this.Foo(*MarkerMemberThisProperty*) = 1""",
            marker = "(*MarkerMemberThisProperty*)")

    [<Test>]
    member this.``ExpressionPropertyAssignment.Bug217051``() =  
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                type Foo() =
                    member val Prop = 0 with get, set
 
                Foo()(*Marker*) <- 4 """,
            marker = "(*Marker*)",
            list = ["Prop"])

    [<Test>]
    member this.``ExpressionProperty.Bug234687``() =  
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                open System.Reflection
                let x = obj()
                let a = x.GetType().Assembly(*Marker*)
            """,
            marker = "(*Marker*)",
            list = ["CodeBase"]) // expect instance properties of Assembly, not static Assembly methods

    [<Test>]
    [<Ignore("Bug 3627 - Completion lists should be filtered in many contexts")>] 
    member this.NotShowAttribute() =
        let fileContents = """
                open System

                [<System.ObsoleteAttribute(*Mattribute1*)>]
                type testclass() = 
                    member x.Name() = "test"
    
                [<ObsoleteAttribute("stuff")(*Mattribute2*)>]
                type testattribute() = 
                    member x.Empty = 0
                """
        let (solution, project, file) = this.CreateSingleFileProject(fileContents)

        //Completion List----where completion list does not come up
        let completions = DotCompletionAtStartOfMarker file "(*Mattribute1*)"
        AssertCompListIsEmpty(completions)
        
        //Completion List----type where completion list does not come up
        let completions = DotCompletionAtStartOfMarker file "(*Mattribute2*)"
        AssertCompListIsEmpty(completions)
        
    [<Test>]
    [<Ignore("Bug 3627 - Completion lists should be filtered in many contexts")>] 
    member this.NotShowPInvokeSignature() =
        let fileContents = """
                //open System
                //open IO = System(*Mcomment*)

                #if RELEASE
                    System.Console(*Mdisablecode*)
                #endif

                let x = "System.Console(*Minstring*)"
                """
        let (solution, project, file) = this.CreateSingleFileProject(fileContents)


        // description="Completion List----where completion list does not come up
        let completions = DotCompletionAtStartOfMarker file "(*Mreturntype*)"
        AssertCompListIsEmpty(completions)


        // description="Completion List----type where completion list does not come up
        let completions = DotCompletionAtStartOfMarker file "(*Mfunctionname*)"
        AssertCompListIsEmpty(completions)


        // description="Completion List----type where completion list does not come up
        let completions = DotCompletionAtStartOfMarker file "(*Mparametertype*)"
        AssertCompListIsEmpty(completions)


        // description="Completion List----type where completion list does not come up
        let completions = DotCompletionAtStartOfMarker file "(*Mparameter*)"
        AssertCompListIsEmpty(completions)


        // description="Completion List----type where completion list does not come up
        let completions = DotCompletionAtStartOfMarker file "(*Mparameterlist*)"
        AssertCompListIsEmpty(completions)

    [<Test>]
    member this.``Basic.Completion.UnfinishedLet``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                let g(x) = x+1

                let f() =
                    let r = g(4)(*Marker*) """,
            marker = "(*Marker*)",
            list = ["CompareTo"])

    [<Test>]
    [<Ignore("I don't understand why this test doesn't work, but the product works")>]
    member this.``ShortFormSeqExpr.Bug229610``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                module test
                    open System.Text.RegularExpressions
 
                    let limit = 50
                    let linkPat = "href=\s*\"[^\"h]*(http://[^&\"]*)\""
                    let getLinks (txt:string) =  [ for m in Regex.Matches(txt,linkPat)  -> m.Groups.Item(1)(*Marker*) ]   """,
            marker = "(*Marker*)",
            list = ["Value"])

    //Regression test for bug 69159 Fsharp: dot completion is mission for an array
    [<Test>]
    member this.``Array.InitialUsing..``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """let x1 = [| 0.0 .. 0.1 .. 10.0 |](*Marker*)""",
            marker = "(*Marker*)",
            list = ["Length";"Clone";"ToString"])

    //Regression test for bug 65740 Fsharp: dot completion is mission after a '#' statement
    [<Test>]
    [<Ignore("Re-enable this test --- https://github.com/Microsoft/visualfsharp/issues/5238")>]
    member this.``Identifier.In#Statement``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                # 29 "original-test-file.fs"
                let argv = System.Environment.GetCommandLineArgs() 
 
                let SetCulture() = 
                  if argv(*Marker*)Length > 2 && argv.[1] = "--culture" then  
                    let cultureString = argv.[2] 
                """,
            marker = "(*Marker*)",
            list = ["Length";"Clone";"ToString"])

    //This test is about CompletionList which should be moved to completionList, it's too special to refactor.
    //Regression test for bug 72595 typing quickly yields wrong intellisense
    [<Test>]
    member this.``BadCompletionAfterQuicklyTyping``() =        
        let code = [ "        " ]
        let (_, _, file) = this.CreateSingleFileProject(code)

        TakeCoffeeBreak(this.VS)

        // In this case, we quickly type "." and then get dot-completions
        // For "level <- Module" this shows completions from the "Module" (e.g. "Module.Other")
        // This simulates the case when the user quickly types "dot" after the file has been TCed before.

        ReplaceFileInMemoryWithoutCoffeeBreak file ([ "[1]." ])      
        MoveCursorToEndOfMarker(file, ".")
        TakeCoffeeBreak(this.VS)

        let completions = AutoCompleteAtCursor file
        AssertCompListContainsAll(completions, ["Length"])
        AssertCompListDoesNotContainAny(completions, ["AbstractClassAttribute"]) 

    [<Test>]
    member this.``SelfParameter.InDoKeywordScope``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                type foo() as this =
                    do
                        this(*Marker*)""",
            marker = "(*Marker*)",
            list = ["ToString"])

    [<Test>]
    member this.``SelfParameter.InDoKeywordScope.Negative``() =
        this.VerifyDotCompListDoesNotContainAnyAtStartOfMarker(
            fileContents = """
                type foo() as this =
                    do
                        this(*Marker*)""",
            marker = "(*Marker*)",
            list = ["Value";"Contents"])

    [<Test>]
    member this.``ReOpenNameSpace.StaticProperties``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                // Static properties & events
                namespace A 
                type TestType =
                  static member Prop = 0
                  static member Event = (new Event<int>()).Publish
                namespace B
                open A
                open A
                TestType(*Marker1*)""",
            marker = "(*Marker1*)",
            list = ["Prop";"Event"])

    [<Test>]
    member this.``ReOpenNameSpace.EnumTypes``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                // F# declared enum types:
                namespace A 
                module Test = 
                  type A = | Foo = 0

                namespace B
                open A
                open A
                Test.A(*Marker2*)
                """,
            marker = "(*Marker2*)",
            list = ["Foo"])

    [<Test>]
    member this.``ReOpenNameSpace.SystemLibrary``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                module BasicTest
                open System.IO
                open System.IO

                File(*Marker3*)
                """,
            marker = "(*Marker3*)",
            list = ["Open"])

    [<Test>]
    member this.``ReOpenNameSpace.FsharpQuotation``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                module BasicTest
                open Microsoft.FSharp.Quotations
                open Microsoft.FSharp.Quotations
                Expr(*Marker4*)
                """,
            marker = "(*Marker4*)",
            list = ["Value"])

    [<Test>]
    member this.``ReOpenNameSpace.MailboxProcessor``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                module BasicTest
                open Microsoft.FSharp.Control
                open Microsoft.FSharp.Control
                let counter = 
                    MailboxProcessor(*Marker6*)""",
            marker = "(*Marker6*)",
            list = ["Start"])

    [<Test>]
    member this.``ReopenNamespace.Module``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                namespace A 
                module Test = 
                  let foo n = n + 1
                namespace B
                open A
                open A
                Test(*Marker7*)""",
            marker = "(*Marker7*)",
            list = ["foo"])

    [<Test>]
    member this.``Expression.InLetScope``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                module BasicTest

                let p4 = 
                    let isPalindrome x = 
                        let chars = (string_of_int x).ToCharArray()
                        let len = chars(*Marker1*)
                        chars 
                        |> Array.mapi (fun i c -> (i(*Marker2*), c(*Marker3*))""",
            marker = "(*Marker1*)",
            list = ["IsFixedSize";"Initialize"])

    [<Test>]
    member this.``Expression.InFunScope.FirstParameter``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                module BasicTest
                let p4 = 
                    let isPalindrome x = 
                        let chars = (string_of_int x).ToCharArray()
                        let len = chars(*Marker1*)
                        chars 
                        |> Array.mapi (fun i c -> (i(*Marker2*), c(*Marker3*))""",
            marker = "(*Marker2*)",
            list = ["CompareTo"])

    [<Test>]
    member this.``Expression.InFunScope.SecParameter``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                module BasicTest

                let p4 = 
                    let isPalindrome x = 
                        let chars = (string_of_int x).ToCharArray()
                        let len = chars(*Marker1*)
                        chars 
                        |> Array.mapi (fun i c -> (i(*Marker2*), c(*Marker3*))""",
            marker = "(*Marker3*)",
            list = ["GetType";"ToString"])

    [<Test>]
    member this.``Expression.InMatchWhenClause``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                module BasicTest
                type DU = X of int

                let timefilter pkt =
                    match pkt with
                    | X(hdr) when hdr(*MarkerMatch*) -> ()
                    | _ -> ()
                """,
            marker = "(*MarkerMatch*)",
            list = ["CompareTo";"ToString"])

    //Regression test for bug 3223 in PS: No intellisense at point
    [<Test>]
    member this.``Identifier.InActivePattern.Positive``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                module BasicTest
                // regression test for bug 3223  No intellisense at point

                open Microsoft.FSharp.Quotations.Patterns
                open Microsoft.FSharp.Quotations.DerivedPatterns

                let test1 = <@ 1 + 1 @>
                let _ =
                    match test1 with
                    | Call(None, methInfo, args) ->
                        if methInfo(*Marker*)
                """,
            marker = "(*Marker*)",
            list = ["Attributes";"CallingConvention";"ContainsGenericParameters"])

    [<Test>]
    member this.``Identifier.InActivePattern.Negative``() =
        this.VerifyDotCompListDoesNotContainAnyAtStartOfMarker(
            fileContents = """
                module BasicTest
                // regression test for bug 3223  No intellisense at point

                open Microsoft.FSharp.Quotations.Patterns
                open Microsoft.FSharp.Quotations.DerivedPatterns

                let test1 = <@ 1 + 1 @>
                let _ =
                    match test1 with
                    | Call(None, methInfo, args) ->
                        if methInfo(*Marker*)
                """,
            marker = "(*Marker*)",
            list = ["Head";"ToInt"])

   //Regression test of bug 2296:No completion lists on the direct results of a method call
    [<Test>] 
    member this.``Regression2296.DirectResultsOfMethodCall``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                module BasicTest
                // regression test of bug 2296: No completion lists on the direct results of a method call

                // This is a function that has a custom attribute on the return type.
                let foo(a) : [<Core.OCamlCompatibilityAttribute("Attribute on return type!")>] int
                   = a + 5

                // The rest of the code is a mere verification that the compiler thru reflection
                let executingAssembly = System.Reflection.Assembly.GetExecutingAssembly()
                let programType = executingAssembly.GetType("Program")
                let message = programType.GetMethod("foo")(*Marker1*)
                """,
            marker = "(*Marker1*)",
            list = ["Attributes";"CallingConvention";"IsFamily"])

    [<Test>]
    member this.``Regression2296.DirectResultsOfMethodCall.Negative``() = 
        this.VerifyDotCompListDoesNotContainAnyAtStartOfMarker(
            fileContents = """
                module BasicTest
                // regression test of bug 2296: No completion lists on the direct results of a method call

                // This is a function that has a custom attribute on the return type.
                let foo(a) : [<Core.OCamlCompatibilityAttribute("Attribute on return type!")>] int
                   = a + 5

                // The rest of the code is a mere verification that the compiler thru reflection
                let executingAssembly = System.Reflection.Assembly.GetExecutingAssembly()
                let programType = executingAssembly.GetType("Program")
                let message = programType.GetMethod("foo")(*Marker1*)
                """,
            marker = "(*Marker1*)",
            list = ["value__"])                     

    [<Test>]
    member this.``Regression2296.Identifier.String.Reflection01``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                module BasicTest
                // regression test of bug 2296: No completion lists on the direct results of a method call

                // This is a function that has a custom attribute on the return type.
                let foo(a) : [<Core.OCamlCompatibilityAttribute("Attribute on return type!")>] int
                    = a + 5

                // The rest of the code is a mere verification that the compiler thru reflection
                let executingAssembly = System.Reflection.Assembly.GetExecutingAssembly()

                let programType = executingAssembly.GetType("Program")

                let message = programType.GetMethod("foo")(*Marker1*)

                let x = ""
                let _ = x.Contains("a")(*Marker2*)""",
            marker = "(*Marker2*)",
            list = ["CompareTo";"GetType";"ToString"])  

    [<Test>]
    member this.``Regression2296.Identifier.String.Reflection01.Negative``() = 
        this.VerifyDotCompListDoesNotContainAnyAtStartOfMarker(
            fileContents = """
                module BasicTest
                // regression test of bug 2296: No completion lists on the direct results of a method call

                // This is a function that has a custom attribute on the return type.
                let foo(a) : [<Core.OCamlCompatibilityAttribute("Attribute on return type!")>] int
                   = a + 5

                // The rest of the code is a mere verification that the compiler thru reflection
                let executingAssembly = System.Reflection.Assembly.GetExecutingAssembly()

                let programType = executingAssembly.GetType("Program")

                let message = programType.GetMethod("foo")(*Marker1*)

                let x = ""
                let _ = x.Contains("a")(*Marker2*)""",
            marker = "(*Marker2*)",
            list = ["value__"])  

    [<Test>]
    member this.``Regression2296.Identifier.String.Reflection02``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                module BasicTest
                // regression test of bug 2296: No completion lists on the direct results of a method call

                // This is a function that has a custom attribute on the return type.
                let foo(a) : [<Core.OCamlCompatibilityAttribute("Attribute on return type!")>] int
                   = a + 5

                // The rest of the code is a mere verification that the compiler thru reflection
                let executingAssembly = System.Reflection.Assembly.GetExecutingAssembly()

                let programType = executingAssembly.GetType("Program")

                let message = programType.GetMethod("foo")(*Marker1*)

                let x = ""
                let _ = x.Contains("a")(*Marker2*)
                let _ = x.CompareTo("a")(*Marker3*)""",
            marker = "(*Marker3*)",
            list = ["CompareTo";"GetType";"ToString"]) 

    [<Test>]
    member this.``Regression2296.Identifier.String.Reflection02.Negative``() = 
        this.VerifyDotCompListDoesNotContainAnyAtStartOfMarker(
            fileContents = """
                module BasicTest
                // regression test of bug 2296: No completion lists on the direct results of a method call

                // This is a function that has a custom attribute on the return type.
                let foo(a) : [<Core.OCamlCompatibilityAttribute("Attribute on return type!")>] int
                   = a + 5

                // The rest of the code is a mere verification that the compiler thru reflection
                let executingAssembly = System.Reflection.Assembly.GetExecutingAssembly()
                let programType = executingAssembly.GetType("Program")
                let message = programType.GetMethod("foo")(*Marker1*)
                let x = ""
                let _ = x.Contains("a")(*Marker2*)
                let _ = x.CompareTo("a")(*Marker3*)""",
            marker = "(*Marker3*)",
            list = ["value__"])  

    [<Test>]
    member this.``Regression2296.System.StaticMethod.Reflection``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                module BasicTest
                // regression test of bug 2296: No completion lists on the direct results of a method call

                // This is a function that has a custom attribute on the return type.
                let foo(a) : [<Core.OCamlCompatibilityAttribute("Attribute on return type!")>] int
                   = a + 5

                // The rest of the code is a mere verification that the compiler thru reflection
                let executingAssembly = System.Reflection.Assembly.GetExecutingAssembly()
                let programType = executingAssembly.GetType("Program")
                let message = programType.GetMethod("foo")(*Marker1*)
                let x = ""
                let _ = x.Contains("a")(*Marker2*)
                let _ = x.CompareTo("a")(*Marker3*)

                open System.IO

                let GetFileSize filePath = File.GetAttributes(filePath)(*Marker4*)""",
            marker = "(*Marker4*)",
            list = ["CompareTo";"GetType";"ToString"]) 

    [<Test>]
    member this.``Regression2296.System.StaticMethod.Reflection.Negative``() =
        this.VerifyDotCompListDoesNotContainAnyAtStartOfMarker(
            fileContents = """
                module BasicTest
                // regression test of bug 2296: No completion lists on the direct results of a method call

                // This is a function that has a custom attribute on the return type.
                let foo(a) : [<Core.OCamlCompatibilityAttribute("Attribute on return type!")>] int
                   = a + 5

                // The rest of the code is a mere verification that the compiler thru reflection
                let executingAssembly = System.Reflection.Assembly.GetExecutingAssembly()
                let programType = executingAssembly.GetType("Program")
                let message = programType.GetMethod("foo")(*Marker1*)
                let x = ""
                let _ = x.Contains("a")(*Marker2*)
                let _ = x.CompareTo("a")(*Marker3*)

                open System.IO

                let GetFileSize filePath = File.GetAttributes(filePath)(*Marker4*)""",
            marker = "(*Marker4*)",
            list = ["value__"])    

    [<Test>]
    member this.``Seq.NearTheEndOfFile``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                module BasicTest
                open Microsoft.FSharp.Math

                let trianglenumbers = Seq.init_infinite (fun i -> let i = BigInt(i) in i * (i+1I) / 2I)

                (trianglenumbers |> Seq(*MarkerNearTheEnd*))""",
            marker = "(*MarkerNearTheEnd*)",
            list = ["cache";"find"])

    //Regression test of bug 3879: intellisense glitch for computation expression
    [<Test>]
    [<Ignore("This is still fail")>]
    member this.``ComputationExpression.WithClosingBrace``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                module BasicTest
                // regression test for bug 3879: intellisense glitch for computation expression
                // intellisense does not work in computation expression without the closing brace
                type System.Net.WebRequest with

                    member x.AsyncGetResponse() = Async.BuildPrimitive(x.BeginGetResponse, x.EndGetResponse)
                    member x.GetResponseAsync() = x.AsyncGetResponse()

                let http(url:string) = 
                     async {let req = System.Net.WebRequest.Create("http://www.yahoo.com")
                            let! rsp = req(*Marker*)} """,
            marker = "(*Marker*)",
            list = ["AsyncGetResponse";"GetResponseAsync";"ToString"])

    [<Test>]
    [<Ignore("This is still fail")>]
    member this.``ComputationExpression.WithoutClosingBrace``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                module BasicTest
                // regression test for bug 3879: intellisense glitch for computation expression
                // intellisense does not work in computation expression without the closing brace
                type System.Net.WebRequest with

                    member x.AsyncGetResponse() = Async.BuildPrimitive(x.BeginGetResponse, x.EndGetResponse)
                    member x.GetResponseAsync() = x.AsyncGetResponse()

                let http(url:string) = 
                    async { let req = System.Net.WebRequest.Create("http://www.yahoo.com")
                            let! rsp = req(*Marker*) """,           
            marker = "(*Marker*)",
            list = ["AsyncGetResponse";"GetResponseAsync";"ToString"])  

    //Regression Test of 4405:intelisense has wrong type for identifier, using most recently bound of same name rather than the one in scope?
    [<Test>]
    member this.``Regression4405.Identifier.ReBinded``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                let f x = 
                    let varA = "string"
                    let varA = if x then varA(*MarkerRebound*) else 2
                    varA""",
            marker = "(*MarkerRebound*)",
            list = ["Chars";"StartsWith"])

    //Regression test for FSharp1.0:4702
    [<Test>]
    member this.``Regression4702.SystemWord``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = "System(*Marker*)",
            marker = "(*Marker*)",
            list = ["Console";"Byte";"ArgumentException"])

    [<Test>]
    member this.``TypeAbbreviation.Positive``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                module BasicTest

                Microsoft.FSharp.Core(*Marker1*)""",
            marker = "(*Marker1*)",
            list = ["int16";"int32";"int64"])

    [<Test>]
    member this.``TypeAbbreviation.Negative``() =
        this.VerifyDotCompListDoesNotContainAnyAtStartOfMarker(
            fileContents = """
                module BasicTest

                Microsoft.FSharp.Core(*Marker1*)""",
            marker = "(*Marker1*)",
            list = ["Int16";"Int32";"Int64"])    

    //Regression test of bug 3754:tupe forwarder bug? intellisense bug?
    [<Test>]
    member this.``Regression3754.TypeOfListForward.Positive``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                module BasicTest
                // regression test for bug 3754
                // tupe forwarder bug? intellisense bug?

                open System.IO
                open System.Xml
                open System.Xml.Linq 
                let xmlStr = @"<?xml version='1.0' encoding='UTF-8'?><doc>    <blah>Blah</blah>    <a href='urn:foo' />    <yadda>        <blah>Blah</blah>        <a href='urn:bar' />    </yadda></doc>"
                let xns = XNamespace.op_Implicit ""
                let a = xns + "a"
                let reader = new StringReader(xmlStr)
                let xdoc = XDocument.Load(reader)
                let aElements = [for x in xdoc.Root.Elements() do
                                    if x.Name = a then
                                        yield x]
                let href = xns + "href"
                aElements |> List(*Marker*)""",
            marker = "(*Marker*)",
            list = ["append";"choose";"isEmpty"])

    [<Test>]
    member this.``Regression3754.TypeOfListForward.Negative``() =
        this.VerifyDotCompListDoesNotContainAnyAtStartOfMarker(
            fileContents = """
                module BasicTest
                // regression test for bug 3754
                // tupe forwarder bug? intellisense bug?

                open System.IO
                open System.Xml
                open System.Xml.Linq 
                let xmlStr = @"<?xml version='1.0' encoding='UTF-8'?><doc>    <blah>Blah</blah>    <a href='urn:foo' />    <yadda>        <blah>Blah</blah>        <a href='urn:bar' />    </yadda></doc>"
                let xns = XNamespace.op_Implicit ""
                let a = xns + "a"
                let reader = new StringReader(xmlStr)
                let xdoc = XDocument.Load(reader)
                let aElements = [for x in xdoc.Root.Elements() do
                                    if x.Name = a then
                                        yield x]
                let href = xns + "href"
                aElements |> List(*Marker*)""",
            marker = "Marker",
            list = ["<Note>"])

    [<Test>]
    member this.``NonApplicableExtensionMembersDoNotAppear.Bug40379``() =
        let code =
                                    [ "open System.Xml.Linq"
                                      "type MyType() ="
                                      "   static member Foo(actual:XElement) =  actual.Name "
                                      "   member public this.Bar1() ="
                                      "      let actual1 : int[] = failwith \"\""
                                      "      actual1.(*Marker*)" 
                                      "   member public this.Bar2() ="
                                      "      let actual2 : XNode[] = failwith \"\""
                                      "      actual2.(*Marker*)" 
                                      "   member public this.Bar3() ="
                                      "      let actual3 : XElement[] = failwith \"\""
                                      "      actual3.(*Marker*)" 
                                      ]
        let (_, _, file) = this.CreateSingleFileProject(code, references = ["System.Xml"; "System.Xml.Linq"])
        MoveCursorToEndOfMarker(file, "actual1.")
        let completions = AutoCompleteAtCursor file
        AssertCompListDoesNotContainAny(completions, [ "Ancestors"; "AncestorsAndSelf"])
        MoveCursorToEndOfMarker(file, "actual2.")
        let completions = AutoCompleteAtCursor file
        AssertCompListContains(completions, "Ancestors")
        AssertCompListDoesNotContain(completions, "AncestorsAndSelf")
        MoveCursorToEndOfMarker(file, "actual3.")
        let completions = AutoCompleteAtCursor file
        AssertCompListContainsAll(completions, ["Ancestors"; "AncestorsAndSelf"])

    [<Test>]
    member this.``Visibility.InternalMethods.DefInSameAssambly``() =
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                module CodeAccessibility
                open System
                module Module1 =

                type Type1()=
                    [<DefaultValue>]
                    val mutable internal fieldInternal:int
       
                    member internal x.MethodInternal (x:int) = x+2
        
                let type1 = new Type1()

                type1(*MarkerSameAssemb*)""",
            marker = "(*MarkerSameAssemb*)",
            list = ["fieldInternal";"MethodInternal"])

    [<Test>]
    member this.``QueryExpression.DotCompletionSmokeTest1``() = 
        this.VerifyDotCompListContainAllAtStartOfMarker(
            fileContents = """
                module Basic
                let x2 = query { for x in ["1";"2";"3"] do 
                                 select x(*Marker*)""",
            marker = "(*Marker*)",
            list = ["Chars";"Length"],
            addtlRefAssy=standard40AssemblyRefs)

    member this.``QueryExpression.DotCompletionSmokeTest2``() = 
           this.VerifyDotCompListContainAllAtStartOfMarker(
              fileContents = """
                module BasicTest
                let x = query { for x in ["1";"2";"3"] do select x(*Marker*)""" ,
              marker = "(*Marker*)",
              list = ["Chars"; "Length"],
              addtlRefAssy=standard40AssemblyRefs )

    [<Test>]
    member this.``QueryExpression.DotCompletionSmokeTest0``() = 
           this.VerifyDotCompListContainAllAtStartOfMarker(
              fileContents = """
                module BasicTest
                let x = seq { for x in ["1";"2";"3"] do yield x(*Marker*) }""" ,
              marker = "(*Marker*)",
              list = ["Chars";"Length"],
              addtlRefAssy=standard40AssemblyRefs )


    [<Test>]
    member this.``QueryExpression.DotCompletionSmokeTest3``() = 
           this.VerifyDotCompListContainAllAtStartOfMarker(
              fileContents = """
                module BasicTest
                let x = query { for x in ["1";"2";"3"] do select x(*Marker*) }""" ,
              marker = "(*Marker*)",
              list = ["Chars";"Length"],
              addtlRefAssy=standard40AssemblyRefs )


    [<Test>]
    member this.``QueryExpression.DotCompletionSystematic1``() = 
      for customOperation in ["select";"sortBy";"where"] do
        let fileContentsList = 
            ["""
                module Simple
                let x2 = query { for x in ["1";"2";"3"] do 
                                 """+customOperation+""" x(*Marker*)"""
             """
                module Simple
                let x2 = query { for x in ["1";"2";"3"] do 
                                 """+customOperation+""" (x(*Marker*)""" 
             """
                module Simple
                let x2 = query { for x in ["1";"2";"3"] do 
                                 """+customOperation+""" (x(*Marker*) }""" 
             """
                module Simple
                let x2 = query { for x in ["1";"2";"3"] do 
                                 """+customOperation+""" (x(*Marker*)
                                 select x""" 
             """
                module Simple
                let x2 = query { for x in ["1";"2";"3"] do 
                                 """+customOperation+""" x(*Marker*)
                                 select x }""" 
             """
                module Simple
                let x2 = query { for x in ["1";"2";"3"] do 
                                 """+customOperation+""" (x(*Marker*))""" 
             """
                module Simple
                let x2 = query { for x in ["1";"2";"3"] do 
                                 """+customOperation+""" (x.Length + x(*Marker*)"""
             """
                module Simple
                let x2 = query { for x in [1;2;3] do 
                                 for y in ["1";"2";"3"] do 
                                 """+customOperation+""" (x + y(*Marker*)""" 
             """
                module Simple
                let x2 = query { for x in [1;2;3] do 
                                 for y in ["1";"2";"3"] do 
                                 """+customOperation+""" (x + y(*Marker*))""" 
             """
                module Simple
                let x2 = query { for x in [1;2;3] do 
                                 for y in ["1";"2";"3"] do 
                                 where (x > y.Length)
                                 """+customOperation+""" (x + y(*Marker*)""" ] 
        for fileContents in fileContentsList do 
            printfn "customOperation = %s, fileContents = <<<%s>>>" customOperation fileContents
            this.VerifyDotCompListContainAllAtStartOfMarker(
                fileContents = fileContents,
                marker = "(*Marker*)",
                list = ["Chars";"Length"],
                addtlRefAssy=standard40AssemblyRefs)

    [<Test>] 
    member public this.``QueryExpression.InsideJoin.Bug204147``() =        
           this.VerifyDotCompListContainAllAtStartOfMarker(
              fileContents = """
                    module Simple
                    type T() =
                         member x.GetCollection() = [1;2;3;4]
                    let q =
                        query {
                           for e in [1..10] do
                           join b in T()(*Marker*)
                           select b
                        }""",
              marker = "(*Marker*)",
              list = ["GetCollection"],
              addtlRefAssy=queryAssemblyRefs )

(*------------------------------------------IDE Query automation start -------------------------------------------------*)

    member private this.AssertDotCompletionListInQuery(fileContents: string, marker : string, list : string list) =
        let datacode = """
        namespace DataSource
        open System
        open System.Xml.Linq
        type Product() =
            let mutable id = 0
            let mutable name = ""
            let mutable category = ""
            let mutable price = 0M
            let mutable unitsInStock = 0
            member x.ProductID with get() = id and set(v) = id <- v
            member x.ProductName with get() = name and set(v) = name <- v
            member x.Category with get() = category and set(v) = category <- v
            member x.UnitPrice with get() = price and set(v) = price <- v
            member x.UnitsInStock with get() = unitsInStock and set(v) = unitsInStock <- v

        module Products =
            let getProductList() =
                [
                Product(ProductID = 1, ProductName = "Chai", Category = "Beverages", UnitPrice = 18.0000M, UnitsInStock = 39 );
                Product(ProductID = 2, ProductName = "Chang", Category = "Beverages", UnitPrice = 19.0000M, UnitsInStock = 17 ); 
                Product(ProductID = 3, ProductName = "Aniseed Syrup", Category = "Condiments", UnitPrice = 10.0000M, UnitsInStock = 13 );
                ] 
        """
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        this.AddAssemblyReference(project, "System.Xml.Linq")
        let file1 = AddFileFromTextBlob(project,"File1.fs",datacode)
        //build
        let file2 = AddFileFromTextBlob(project,"File2.fs",fileContents)
        let file1 = OpenFile(project,"File1.fs")
        let file2 = OpenFile(project,"File2.fs")

        TakeCoffeeBreak(this.VS)
        let completions = DotCompletionAtStartOfMarker file2 marker
        AssertCompListContainsAll(completions, list) 

    [<Test>]
    [<Category("Query")>]
    [<Ignore("196230")>]
    // Intellisense still appears on arguments when the operator is used in error 
    member public this.``Query.HasErrors.Bug196230``() =
        this.AssertDotCompletionListInQuery(
              fileContents = """
                open DataSource
                // defined in another file; see AssertDotCompletionListInQuery
                let products = Products.getProductList()
                let sortedProducts =
                    query {
                        for p in products do
                        let x = p.ProductID + "a"
                        sortBy p(*Marker*)
                        select p
                    }""" ,
              marker = "(*Marker*)",
              list = ["ProductID";"ProductName"] )

    [<Test>]
    [<Category("Query")>]
    // Intellisense still appears on arguments when the operator is used in error 
    member public this.``Query.HasErrors2``() =
        this.AssertDotCompletionListInQuery(
              fileContents = """
                open DataSource
                let products = Products.getProductList()
                let sortedProducts =
                    query {
                        for p in products do
                        orderBy (p(*Marker*))
                    }""" ,
              marker = "(*Marker*)",
              list = ["ProductID";"ProductName"] )

    [<Test>]
    [<Category("Query")>]
    // Shadowed variables have correct Intellisense
    member public this.``Query.ShadowedVariables``() =
        this.AssertDotCompletionListInQuery(
              fileContents = """
                open DataSource
                let products = Products.getProductList()
                let p = 12
                let sortedProducts =
                    query {
                        for p in products do
                        select p(*Marker*)
                    }""" ,
              marker = "(*Marker*)",
              list = ["Category";"ProductName"] )

    [<Test>]
    [<Category("Query")>]
    // Intellisense works correctly in a nested query
    member public this.``Query.InNestedQuery``() =
        let fileContents = """
        let tuples = [ (1, 8, 9); (56, 45, 3)] 
        let numbers = [ 1;2; 8; 9; 15; 23; 3; 42; 4;0; 55;]

        let foo = 
            query {
                for n in numbers do
                let maxNumber = query {for x in tuples do maxBy x(*Marker1*)}
                select (n, query {for y in numbers do minBy y(*Marker2*)}) }
        """
        this.VerifyDotCompListContainAllAtStartOfMarker(fileContents, "(*Marker1*)", 
            ["Equals";"GetType"], queryAssemblyRefs )
        this.VerifyDotCompListContainAllAtStartOfMarker(fileContents, "(*Marker2*)", 
            ["Equals";"CompareTo"], queryAssemblyRefs )

    [<Test>]
    [<Category("Query")>]
    // Intellisense works correctly in a nested expression within a lamda
    member public this.``Query.NestedExpressionWithinLamda``() =
        let fileContents = """
        let numbers = [ 1;2; 8; 9; 15; 23; 3; 42; 4;0; 55;]
        let f (x : string) = ()
        let foo = 
            query {
                for n in numbers do
                let x = 42 |> ignore; numbers |> List.iter( fun n -> f ("1" + "1")(*Marker*))
                skipWhile (n < 30)
                 }
        """
        this.VerifyDotCompListContainAllAtStartOfMarker(fileContents, "(*Marker*)", 
            ["Chars";"Length"], queryAssemblyRefs )

    [<Test>]
    member this.``Verify no completion on dot after module definition``() = 
        this.VerifyDotCompListIsEmptyAtStartOfMarker(
            fileContents = """
                module BasicTest(*Marker*)

                let foo x = x
                let bar = 1""",
            marker = "(*Marker*)")

    [<Test>]
    member this.``Verify no completion after module definition``() = 
        this.VerifyCtrlSpaceCompListIsEmptyAtEndOfMarker(
            fileContents = """
                module BasicTest 

                let foo x = x
                let bar = 1""",
            marker = "module BasicTest ")

    [<Test>]
    member this.``Verify no completion in hash derictives``() =
        this.VerifyCtrlSpaceCompListIsEmptyAtEndOfMarker(
            fileContents = """
                #r (*Marker*)

                let foo x = x
                let bar = 1""",
            marker = "(*Marker*)")

    [<Test;Category("Repro")>]
    member public this.``ExpressionDotting.Regression.Bug3709``() = 
        this.VerifyCtrlSpaceListContainAllAtStartOfMarker(
            fileContents = """
                let foo = ""
                let foo = foo.E(*marker*)n "a" """,
            marker = "(*marker*)",
            list = ["EndsWith"])  

// Context project system
[<TestFixture>] 
type UsingProjectSystem() = 
    inherit UsingMSBuild(VsOpts = LanguageServiceExtension.ProjectSystemTestFlavour)


               
