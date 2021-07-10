// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Tests.LanguageService.ErrorList

open System
open System.IO
open NUnit.Framework
open Microsoft.VisualStudio.FSharp
open Salsa.Salsa
open Salsa.VsOpsUtils
open UnitTests.TestLib.Salsa
open UnitTests.TestLib.Utils
open UnitTests.TestLib.LanguageService
open UnitTests.TestLib.ProjectSystem

[<SetUpFixture>]
type public AssemblyResolverTestFixture () =

    [<OneTimeSetUp>]
    member public _.Init () = AssemblyResolver.addResolver ()

[<TestFixture>]
[<Category "LanguageService">] 
type UsingMSBuild() as this = 
    inherit LanguageServiceBaseTests()

    let VerifyErrorListContainedExpectedStr(expectedStr:string,project : OpenProject) = 
        let convertNewlines (s:string) = s.Replace("\r\n", " ").Replace("\n", " ")
        let errorList = GetErrors(project)
        let GetErrorMessages(errorList : Error list) =
            [ for i = 0 to errorList.Length - 1 do
                yield errorList.[i].Message]
        let msgs = GetErrorMessages errorList
        if msgs |> Seq.exists (fun errorMessage -> convertNewlines(errorMessage).Contains(convertNewlines expectedStr)) then
            ()
        else
            Assert.Fail(sprintf "Expected errors to contain '%s' but it was not there; errors were %A" expectedStr msgs)

    let GetWarnings(project : OpenProject) =
        let errorList = GetErrors(project)
        [for error in errorList do
            if (error.Severity = Microsoft.VisualStudio.FSharp.LanguageService.Severity.Warning) then
            yield error]
    
    let CheckErrorList (content : string) f : unit = 
        let (_, project, file) = this.CreateSingleFileProject(content)
        Build(project) |> ignore

        TakeCoffeeBreak(this.VS)
        let errors = GetErrors project
        printfn "==="
        for err in errors do
            printfn "%s" err.Message
        f errors

    let assertContains (errors : list<Error>) text = 
        let ok = errors |> List.exists (fun err -> err.Message = text)
        Assert.IsTrue(ok, sprintf "Error list should contain '%s' message" text)

    let assertExpectedErrorMessages expected (actual: list<Error>) =
        let normalizeCR input = System.Text.RegularExpressions.Regex.Replace(input, @"\r\n|\n\r|\n|\r", "\r\n")
        let actual = 
            actual 
            |> Seq.map (fun e -> e.Message)
            |> String.concat Environment.NewLine
            |> normalizeCR
        let expected = expected |> String.concat Environment.NewLine |> normalizeCR
        
        let message = 
            sprintf """
=[ expected ]============
%s
=[ actual ]==============
%s
=========================""" expected actual
        Assert.AreEqual(expected, actual, message)

    //verify the error list Count
    member private this.VerifyErrorListCountAtOpenProject(fileContents : string, num : int) =
        let (solution, project, file) = this.CreateSingleFileProject(fileContents)
        let errorList = GetErrors(project)
        let errorTexts = new System.Text.StringBuilder()
        for error in errorList do
            printfn "%A" error.Severity
            let s = error.ToString()
            errorTexts.AppendLine s |> ignore
            printf "%s\n" s 

        if num <> errorList.Length then 
            failwithf "The error list number is not the expected %d but %d%s%s" 
                num 
                errorList.Length
                System.Environment.NewLine
                (errorTexts.ToString())

    //Verify the warning list Count
    member private this.VerifyWarningListCountAtOpenProject(fileContents : string, expectedNum : int, ?addtlRefAssy : list<string>) = 
        let (solution, project, file) = this.CreateSingleFileProject(fileContents, ?references = addtlRefAssy)
        
        TakeCoffeeBreak(this.VS) // Wait for the background compiler to catch up.
        let warnList = GetWarnings(project)
        Assert.AreEqual(expectedNum,warnList.Length)

    //verify no the error list 
    member private this.VerifyNoErrorListAtOpenProject(fileContents : string, ?addtlRefAssy : list<string>) = 
        let (solution, project, file) = this.CreateSingleFileProject(fileContents, ?references = addtlRefAssy)
        
        TakeCoffeeBreak(this.VS) // Wait for the background compiler to catch up.
        let errorList = GetErrors(project)      
        for error in errorList do
            printfn "%A" error.Severity
            printf "%s\n" (error.ToString()) 
        Assert.IsTrue(errorList.IsEmpty)
    
    //Verify the error list containd the expected string
    member private this.VerifyErrorListContainedExpectedString(fileContents : string, expectedStr : string, ?addtlRefAssy : list<string>) =
        let (solution, project, file) = this.CreateSingleFileProject(fileContents, ?references = addtlRefAssy)
        
        TakeCoffeeBreak(this.VS) // Wait for the background compiler to catch up.
       
        VerifyErrorListContainedExpectedStr(expectedStr,project)

    //Verify error Count at specified file
    member private this.VerifyCountAtSpecifiedFile(project : OpenProject ,num : int) = 
        TakeCoffeeBreak(this.VS)
        let errorList = GetErrors(project)
        for error in errorList do
            printfn "%A" error.Severity
            printf "%s\n" (error.ToString()) 
        if (num = errorList.Length) then 
                ()
            else
                failwithf "The error list number is not the expected %d" num
    
    [<Test>]
    member public this.``OverloadsAndExtensionMethodsForGenericTypes``() = 
        let fileContent = 
            """
open System.Linq

type T = 
    abstract Count : int -> bool
    default this.Count(_ : int) = true

    interface System.Collections.Generic.IEnumerable<int> with
        member this.GetEnumerator() : System.Collections.Generic.IEnumerator<int> = failwith "not implemented"
    interface System.Collections.IEnumerable with
        member this.GetEnumerator() : System.Collections.IEnumerator = failwith "not implemented"

let g (t : T) = t.Count()
            """
        this.VerifyNoErrorListAtOpenProject(fileContent)


    [<Test>]
    member public this.``ErrorsInScriptFile``() = 
        let (solution, project, file) = this.CreateSingleFileProject("", fileKind = SourceFileKind.FSX)
        
        let checkErrors expected = 
            let l = List.length (GetErrors project)
            Assert.AreEqual(expected, l, "Unexpected number of errors in error list")
        
        TakeCoffeeBreak(this.VS)
        checkErrors 0

        ReplaceFileInMemory file <|
            [
                "#r \"System\""
                "#r \"System2\""
            ]
        TakeCoffeeBreak(this.VS)
        checkErrors 2

        ReplaceFileInMemory file <|
            [
                "#r \"System\""
            ]
        TakeCoffeeBreak(this.VS)
        checkErrors 0

    [<Test>]
    [<Ignore("GetErrors function doese not work for this case")>]
    member public this.``LineDirective``() = 
        use _guard = this.UsingNewVS()
        let fileContents = """
            # 100 "foo.fs"
            let x = y """
        let solution = this.CreateSolution()
        let project = CreateProject(solution, "testproject")
        let _ = AddFileFromTextBlob(project, "File1.fs", "namespace LineDirectives")
        let _ = AddFileFromTextBlob(project,"File2.fs", fileContents)

        let file = OpenFile(project, "File1.fs")
        let _ = OpenFile(project,"File2.fs")
        Assert.IsFalse(Build(project).BuildSucceeded)

        this.VerifyCountAtSpecifiedFile(project,1)
        VerifyErrorListContainedExpectedStr("The value or constructor 'y' is not defined",project)

    [<Test>]
    member public this.``InvalidConstructorOverload``() = 
        let content = """
        type X private() = 
            new(_ : int) = X()
            new(_ : bool) = X()
            new(_ : float, _ : int) = X()
        X(1.0)
        """

        let expectedMessages = [ """No overloads match for method 'X'.

Known type of argument: float

Available overloads:
 - new : bool -> X // Argument at index 1 doesn't match
 - new : int -> X // Argument at index 1 doesn't match""" ]

        CheckErrorList content (assertExpectedErrorMessages expectedMessages)
            

    [<Test>]
    member public this.``Query.InvalidJoinRelation.GroupJoin``() = 
        let content = """
let x = query { 
    for x in [1] do
    groupJoin y in [2] on ( x < y) into g
    select x }
        """
        CheckErrorList content <|
            fun errors ->
                match errors with
                | [err] ->
                    Assert.AreEqual("Invalid join relation in 'groupJoin'. Expected 'expr <op> expr', where <op> is =, =?, ?= or ?=?.", err.Message)
                | errs -> 
                    Assert.Fail("Unexpected content of error list")

    [<Test>]
    member public this.``Query.NonOpenedNullableModule.Join``() = 
        let content = """
let t = 
    query {
        for x in [1] do
        join y in [""] on (x ?=? y)
        select 1  }
        """
        CheckErrorList content <|
            fun errors ->
                match errors with
                | [err] ->
                    Assert.AreEqual("The operator '?=?' cannot be resolved. Consider opening the module 'Microsoft.FSharp.Linq.NullableOperators'.", err.Message)
                | errs -> 
                    Assert.Fail("Unexpected content of error list")

    [<Test>]
    member public this.``Query.NonOpenedNullableModule.GroupJoin``() = 
        let content = """
let t = 
    query {
        for x in [1] do
        groupJoin y in [""] on (x ?=? y) into g
        select 1  }
        """
        CheckErrorList content <|
            fun errors ->
                match errors with
                | [err] ->
                    Assert.AreEqual("The operator '?=?' cannot be resolved. Consider opening the module 'Microsoft.FSharp.Linq.NullableOperators'.", err.Message)
                | errs -> 
                    Assert.Fail("Unexpected content of error list")


    [<Test>]
    member public this.``Query.InvalidJoinRelation.Join``() = 
        let content = """
let x = 
    query {
        for x in [1] do
        join y in [""] on (x > y)
        select 1
    }
    """
        CheckErrorList content <|
            fun errors ->
                match errors with
                | [err] ->
                    Assert.AreEqual("Invalid join relation in 'join'. Expected 'expr <op> expr', where <op> is =, =?, ?= or ?=?.", err.Message)
                | errs -> 
                    Assert.Fail("Unexpected content of error list")

    [<Test>]
    member public this.``InvalidMethodOverload``() = 
        let content = """
        System.Console.WriteLine(null)
        """
        let expectedMessages = [ """A unique overload for method 'WriteLine' could not be determined based on type information prior to this program point. A type annotation may be needed.

Known type of argument: 'a0 when 'a0 : null

Candidates:
 - System.Console.WriteLine(buffer: char []) : unit
 - System.Console.WriteLine(format: string, [<System.ParamArray>] arg: obj []) : unit
 - System.Console.WriteLine(value: obj) : unit
 - System.Console.WriteLine(value: string) : unit""" ]
        CheckErrorList content (assertExpectedErrorMessages expectedMessages)

    [<Test>]
    member public this.``InvalidMethodOverload2``() = 
        let content = """
type A<'T>() = 
    member this.Do(a : int, b : 'T) = ()
    member this.Do(a : int, b : int) = ()
type B() = 
    inherit A<int>() 

let b = B()
b.Do(1, 1)
        """
        let expectedMessages = [ """A unique overload for method 'Do' could not be determined based on type information prior to this program point. A type annotation may be needed.

Known types of arguments: int * int

Candidates:
 - member A.Do : a:int * b:'T -> unit
 - member A.Do : a:int * b:int -> unit""" ]
        CheckErrorList content (assertExpectedErrorMessages expectedMessages)

    [<Test; Category("Expensive")>]
    member public this.``NoErrorInErrList``() = 
        use _guard = this.UsingNewVS()
        let fileContents1 = """
            module NoErrors

            open System.Collections.Generic
            // but do not use it
            """
        let fileContents2 = """
            // Regression test for FSHARP1.0:3824 - Problems with generic type parameters in type extensions (was: Confusing error/warning on type extension: code is less generic)
            module NoErrors2

            module DictionaryExtension = 

                type System.Collections.Generic.IDictionary<'k,'v> with
                    member this.TryLookup(key : 'k) =
                        let mutable value = Unchecked.defaultof<'v>
                        if this.TryGetValue(key, &value) then
                            Some value
                        else
                            None

            open DictionaryExtension"""
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let _ = AddFileFromTextBlob(project,"File1.fs", fileContents1)
        let _ = OpenFile(project,"File1.fs")
        let _ = AddFileFromTextBlob(project,"File2.fs", fileContents2)
        let _ = OpenFile(project,"File2.fs")
        Build(project) |> ignore
        TakeCoffeeBreak(this.VS)
        this.VerifyCountAtSpecifiedFile(project,0)

    [<Test; Category("Expensive")>]
    member public this.``NoLevel4Warning``() = 
        use _guard = this.UsingNewVS()
        let fileContents = """
            namespace testerrorlist
            module nolevel4warnings =
                let x = System.DateTime.Now - System.DateTime.Now
                x.Add(x) |> ignore """
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let _ = AddFileFromTextBlob(project,"Module1.fs",fileContents)
        
        let _ = AddFileFromTextBlob(project,"Script.fsx","")
        let _ = OpenFile(project,"Script.fsx")
        Build(project) |> ignore

        this.VerifyCountAtSpecifiedFile(project,0)
        
    [<Test>]
    //This is an verify action test & example
    member public this.``TestErrorMessage``() =
        let fileContent = """Console.WriteLine("test")"""
        let expectedStr = "The value, namespace, type or module 'Console' is not defined"
        this.VerifyErrorListContainedExpectedString(fileContent,expectedStr)
    
    [<Test>]
    member public this.``TestWrongKeywordInInterfaceImplementation``() = 
        let fileContent = 
            """
type staticInInterface =
    class
        interface System.IDisposable with
            static member Foo() = ()
            member x.Dispose() = ()
        end
    end"""
            
        CheckErrorList fileContent <| function
                | [err] -> Assert.IsTrue(err.Message.Contains("Unexpected keyword 'static' in member definition. Expected 'member', 'override' or other token"))
                | x -> Assert.Fail(sprintf "Unexpected errors: %A" x)
    
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.MultipleErrors")>]
    member public this.``TypeProvider.MultipleErrors`` () =
        let tpRef = PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll")
        let checkList n = 
            printfn "===TypeProvider.MultipleErrors: %d===" n
            let content = sprintf "type Err = TPErrors.TP<%d>" n
            let (solution, project, file) = this.CreateSingleFileProject(content, references = [tpRef])
            TakeCoffeeBreak(this.VS)
            let errorList = GetErrors(project)

            for err in errorList do
                printfn "Severity: %A, Message: %s" err.Severity err.Message

            Assert.IsTrue(List.length errorList = n, "Unexpected size of error list")
            let uniqueErrors = 
                errorList 
                |> Seq.map (fun m -> m.Message, m.Severity) 
                |> set
            Assert.IsTrue(uniqueErrors.Count = n, "List should not contain duplicate errors")
            for x = 0 to (n - 1) do
                let expectedName = sprintf "The type provider 'DummyProviderForLanguageServiceTesting.TypeProviderThatThrowsErrors' reported an error: Error %d" x
                Assert.IsTrue(Set.contains (expectedName, Microsoft.VisualStudio.FSharp.LanguageService.Severity.Error) uniqueErrors)

        for i = 1 to 10 do
            checkList i

    [<Test>]
    [<Category("Records")>]
    member public this.``Records.ErrorList.IncorrectBindings1``() = 
        for code in [ "{_}"; "{_ = }"] do
            printfn "checking %s" code
            CheckErrorList code <|
                fun errs ->
                    printfn "%A" errs
                    Assert.IsTrue((List.length errs) = 2)
                    assertContains errs "Field bindings must have the form 'id = expr;'"
                    assertContains errs "'_' cannot be used as field name"

    [<Test>]
    [<Category("Records")>]
    member public this.``Records.ErrorList.IncorrectBindings2``() =
        CheckErrorList "{_ = 1}" <|
            function
            | [err] -> Assert.AreEqual("'_' cannot be used as field name", err.Message)
            | x -> printfn "%A" x; Assert.Fail("unexpected content of error list")

    [<Test>]
    [<Category("Records")>]
    member public this.``Records.ErrorList.IncorrectBindings3``() =
        CheckErrorList "{a = 1; _; _ = 1}" <|
            fun errs -> 
                Assert.IsTrue((List.length errs) = 3)
                let groupedErrs = errs |> Seq.groupBy (fun e -> e.Message) |> Seq.toList
                Assert.IsTrue((List.length groupedErrs) = 2)
                for (msg, e) in groupedErrs do
                    if msg = "'_' cannot be used as field name" then Assert.AreEqual(2, Seq.length e)
                    elif msg = "Field bindings must have the form 'id = expr;'" then Assert.AreEqual(1, Seq.length e)
                    else Assert.Fail (sprintf "Unexpected message %s" msg)


    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.StaticParameters")>]
    //This test case Verify the Error List shows the correct error message when the static parameter type is invalid
    //Intent: We want to make sure that both errors coming from the TP and the compilation of things specific to type provider are properly flagged in the error list.
    member public this.``TypeProvider.StaticParameters.IncorrectType `` () =
        // dummy Type Provider exposes a parametric type (N1.T) that takes 2 static params (string * int) 
        // but here as you can see it's give (int * int)
        let fileContent = """ type foo = N1.T< const 42,2>"""
        let expectedStr = """This expression was expected to have type
    'string'    
but here has type
    'int'    """
        this.VerifyErrorListContainedExpectedString(fileContent,expectedStr,
            addtlRefAssy = [PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll")])
    
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.StaticParameters")>]
    [<Ignore("This is ignored because currently the Mock Type Provider is not evaluating the static parameter.")>]
    //This test case Verify the Error List shows the correct error message when applying invalid static parameter to the provided type
    member public this.``TypeProvider.StaticParameters.Incorrect `` () =
        
        // dummy Type Provider exposes a parametric type (N1.T) that takes 2 static params (string * int) 
        let fileContent = """ type foo = N1.T< const " ",2>"""
        let expectedStr = "An error occurred applying the static arguments to a provided type"
       
        this.VerifyErrorListContainedExpectedString(fileContent,expectedStr,
            addtlRefAssy = [PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll")])
   
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.StaticParameters")>]
    //This test case Verify that Error List shows the correct error message when Type Provider that takes two static parameter is given only one static parameter.
    member public this.``TypeProvider.StaticParameters.IncorrectNumberOfParameter  `` () =
        
        // dummy Type Provider exposes a parametric type (N1.T) that takes 2 static params (string * int) 
        // but here as you can see it's give (string)
        let fileContent = """type foo = N1.T< const "Hello World">"""
        let expectedStr = "The static parameter 'ParamIgnored' of the provided type or method 'T' requires a value. Static parameters to type providers may be optionally specified using named arguments, e.g. 'T<ParamIgnored=...>'."
       
        this.VerifyErrorListContainedExpectedString(fileContent,expectedStr,
            addtlRefAssy = [PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll")])
    [<Test>]
    [<Category("TypeProvider")>]
    member public this.``TypeProvider.ProhibitedMethods`` () =
        let cases = 
            [
                "let x = BadMethods.Arr.GetFirstElement([||])", "GetFirstElement"
                "let y = BadMethods.Arr.SetFirstElement([||], 5)", "SetFirstElement"
                "let z = BadMethods.Arr.AddressOfFirstElement([||])", "AddressOfFirstElement"
            ]
        for (code, str) in cases do
            this.VerifyErrorListContainedExpectedString
                (
                    code,
                    sprintf "The type provider 'DummyProviderForLanguageServiceTesting.TypeProviderThatEmitsBadMethods' reported an error in the context of provided type 'BadMethods.Arr', member '%s'. The error: The operation 'GetMethodImpl' on item 'Int32[]' should not be called on provided type, member or parameter of type 'ProviderImplementation.ProvidedTypes.TypeSymbol'." str,
                    addtlRefAssy = [PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll")]
                )    
    
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.StaticParameters")>]
    //This test case verify that the Error list count is one in the Error list item when given invalid static parameter that raises an error.
    member public this.``TypeProvider.StaticParameters.ErrorListItem `` () =
        
         this.VerifyErrorListCountAtOpenProject(
            fileContents = """
                            type foo = N1.T< const "Hello World",2>""",
            num = 1) 
    
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.StaticParameters")>]
    //This test case Verify that the Warning list count is one in the Warning list item when there is incorrect indentation in the code.
    member public this.``TypeProvider.StaticParameters.WarningListItem `` () =
        
         this.VerifyWarningListCountAtOpenProject(
            fileContents = """
                            type foo = N1.T< 
                           const "Hello World",2>""",
            expectedNum = 1,
            addtlRefAssy = [PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll")]) 
    
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.StaticParameters")>]
    //This test case Verify that there is No Error list count in the Error list item when the file content is correct.
    member public this.``TypeProvider.StaticParameters.NoErrorListCount `` () =
                 
         this.VerifyNoErrorListAtOpenProject(
            fileContents = """
                            type foo = N1.T< const "Hello World",2>""",
            addtlRefAssy = [PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll")]) 
    

    [<Test>]
    member public this.``NoError.FlagsAndSettings.TargetOptionsRespected``() =
        let fileContent = """
            [<System.Obsolete("x")>]
            let fn x = 0
            let y = fn 1"""
        // Turn off the "Obsolete" warning.
        let (solution, project, file) = this.CreateSingleFileProject(fileContent, disabledWarnings = ["44"])

        TakeCoffeeBreak(this.VS) // Wait for the background compiler to catch up.
        let errorList = GetErrors(project)
        Assert.IsTrue(errorList.IsEmpty)

    [<Test>]
    [<Ignore("https://github.com/Microsoft/visualfsharp/issues/6166")>]
    member public this.``UnicodeCharacters``() = 
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"新規baApplication5")
        let _ = AddFileFromTextBlob(project,"新規baProgram.fsi","")       
        let _ = AddFileFromTextBlob(project,"新規bcrogram.fs","")

        let file = OpenFile(project,"新規baProgram.fsi")  
        let file = OpenFile(project,"新規bcrogram.fs") 

        Assert.IsFalse(Build(project).BuildSucceeded)
        Assert.IsTrue(GetErrors(project) 
                        |> List.exists(fun error -> (error.ToString().Contains("新規baProgram")))) 

    // In this bug, particular warns were still present after nowarn        
    [<Test>]
    member public this.``NoWarn.Bug5424``() =  
        let fileContent = """
            #nowarn "67" // this type test or downcast will always hold
            #nowarn "66" // this upcast is unnecessary - the types are identical
            namespace Namespace1
                module Test =
                    open System
                    let a = ((5 :> obj) :?> Object)
                    let b = a :> obj"""
        this.VerifyNoErrorListAtOpenProject(fileContent)

    /// FEATURE: Errors in flags are sent in Error list.
    [<Test>]
    member public this.``FlagsAndSettings.ErrorsInFlagsDisplayed``() =  
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        SetVersionFile(project,"nonexistent")
        let file = AddFileFromText(project,"File1.fs",["#light"])
        let file = OpenFile(project,"File1.fs")            
        TakeCoffeeBreak(this.VS) // Wait for the background compiler to catch up.
        VerifyErrorListContainedExpectedStr("nonexistent",project)

    [<Test>]
    member public this.``BackgroundComplier``() = 
        this.VerifyErrorListCountAtOpenProject(
            fileContents = """
                #light

                module Test

                module M =
                    let func (args : string[]) = 
                        if(args.Length=1 && args.[0]="Hello") then 0 else 1

                    [<EntryPoint>]
                    let main2 args =
                        let res = func(args)
                        exit(res)
       
                    let f x = 
                        let p = x
                        p + 1
                
                    let g x = 
                        let p = x
                        p + 1
                    """,
            num = 2)

    [<Test>]
    member public this.``CompilerErrorsInErrList1``() = 
        this.VerifyErrorListCountAtOpenProject(
            fileContents = """
                namespace Errorlist
                module CompilerError =
        
                    let a = NoVal""",
            num = 1 )

    [<Test>]
    member public this.``CompilerErrorsInErrList4``() = 
        this.VerifyNoErrorListAtOpenProject(
            fileContents = """
                #nowarn "47"

                type Fruit (shelfLife : int) as x =
    
                        let mutable m_age = (fun () -> x)


                #nowarn "25" // FS0025: Incomplete pattern matches on this expression. For example, the value 'C' 

                type DU = A | B | C
                let f x = function A -> true | B -> false


                #nowarn "58" // FS0058: possible incorrect indentation: this token is offside of context started at
  
                let _fsyacc_gotos = [| 
                0us; 
                1us;
                2us
                |] """ )

    [<Test>]
    member public this.``CompilerErrorsInErrList5``() = 
        this.VerifyErrorListCountAtOpenProject(
            fileContents = """                
                #r "D:\x\Absent.dll"

                let x = 0 """,
            num = 1)

    [<Test>]
    member public this.``CompilerErrorsInErrList6``() = 
        this.VerifyErrorListCountAtOpenProject(
            fileContents = """               
                type EnumOfBigInt =
                    | A = 0I
                    | B = 0I

                type EnumOfNatNum =
                    | A = 0N
                    | B = 0N """,
            num = 2)

    [<Test>]
    member public this.``CompilerErrorsInErrList7``() = 
        this.VerifyErrorListCountAtOpenProject(
            fileContents = """               
                // FSB 1124, Implement constant literals
                type EnumType =
                    | A = 1
                    | B = 2

                type CustomAttrib(a:int, b:string, c:float, d:EnumType) =
                    inherit System.Attribute()
    
                //[<Literal>]    
                let a = 42
                //[<Literal>]
                let b = "str"
                //[<Literal>]
                let c = 3.141
                //[<Literal>]
                let d = EnumType.A

                [<CustomAttrib(a, b, c, d)>]
                type SomeClass() =
                    override this.ToString() = "SomeClass"

                [<EntryPoint>]
                let main0 args = ()

                let foo = 1 """,
            num = 5)

    [<Test>]
    member public this.``CompilerErrorsInErrList8``() = 
        this.VerifyErrorListCountAtOpenProject(
            fileContents = """              
                type EnumInt8s      = | A1 = - 10y """ ,
            num = 1 )

    [<Test>]
    member public this.``CompilerErrorsInErrList9``() = 
        use _guard = this.UsingNewVS()
        let fileContents1 = """
            namespace NS
                [<AbstractClass>]
                type Lib() =
                    class
                        abstract M : int -> int
                    end """
        let fileContents2 = """
            namespace NS
                module M = 
                    type Lib with
                        override x.M i = i
            """
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let _ = AddFileFromTextBlob(project,"File1.fs",fileContents1)
        let file1 = OpenFile(project,"File1.fs")
        let _ = AddFileFromTextBlob(project,"File2.fs",fileContents2)
        let file2 = OpenFile(project,"File2.fs")
        //this.VerifyErrorListNumberAtOpenProject
        this.VerifyCountAtSpecifiedFile(project,1)
        TakeCoffeeBreak(this.VS)
        Build(project) |> ignore
        this.VerifyCountAtSpecifiedFile(project,1)

    [<Test>]
    member public this.``CompilerErrorsInErrList10``() = 
        let fileContents = """
            namespace Errorlist
            module CompilerError =
            
                printfn "%A" System.Windows.Forms.Application.UserAppDataPath """
        let (_, project, _) = this.CreateSingleFileProject(fileContents, references = ["PresentationCore.dll"; "PresentationFramework.dll"])
        Build(project) |> ignore

        this.VerifyCountAtSpecifiedFile(project,1)

    [<Test>]
    member public this.``DoubleClickErrorListItem``() = 
        this.VerifyErrorListCountAtOpenProject(
            fileContents = """
                let x = x """,
            num = 1)

    [<Test>]
    member public this.``FixingCodeAfterBuildRemovesErrors01``() = 
        this.VerifyErrorListCountAtOpenProject(
            fileContents = """
                let x = 4 + "x" """,
            num = 2)

    [<Test>]
    member public this.``FixingCodeAfterBuildRemovesErrors02``() = 
        this.VerifyNoErrorListAtOpenProject(
            fileContents = "let x = 4" )   
                                 
    [<Test>]
    member public this.``IncompleteExpression``() = 
        this.VerifyErrorListCountAtOpenProject(
            fileContents = """
                // Regresson test for FSHARP1.0:1397 - Warning required on expr of function type who result is immediately thrown away
                module Test

                printfn "%A"

                List.map (fun x -> x + 1) """ ,
            num = 2)

    [<Test>]
    member public this.``IntellisenseRequest``() = 
        this.VerifyErrorListCountAtOpenProject(
            fileContents = """
                type Foo() =
                    member a.B(*Marker*) : int = "1" """,
            num = 1)

    [<Test>]
    member public this.``TypeChecking1``() = 
        this.VerifyErrorListCountAtOpenProject(
            fileContents = """
                open System

                module Foo  =
                    type Thread(thread) =
                        let mutable next : Thread option = thread
                        member t.Next with get() = next and set(thread) = next - thread

                module Bar = 
                    let x = new Foo.Thread(None)

                    x.Next <- Some x  """,
            num = 1)
            
    [<Test>]
    member public this.``TypeChecking2``() = 
        this.VerifyErrorListContainedExpectedString(
            fileContents = """
                open System

                module Foo  =
                    type Thread(thread) =
                        let mutable next : Thread option = thread
                        member t.Next with get() = next and set(thread) = next - thread

                module Bar = 
                    let x = new Foo.Thread(None)

                    x.Next <- Some x  """,
            expectedStr = "Foo.Thread option")
            
    [<Test>]
    member public this.``TypeChecking3``() = 
        this.VerifyErrorListCountAtOpenProject(
            fileContents = """
                open System

                module Foo  =
                    type Thread(thread) =
                        let mutable next : Thread option = thread
                        member t.Next with get() = next and set(thread) = next - thread

                module Bar = 
                    let x = new Foo.Thread(None)
                    x.Next <- Some 1 """,
            num = 1)
            
    [<Test>]
    member public this.``TypeChecking4``() = 
        this.VerifyErrorListContainedExpectedString(
            fileContents = """
                open System

                module Foo  =
                    type Thread(thread) =
                        let mutable next : Thread option = thread
                        member t.Next with get() = next and set(thread) = next - thread

                module Bar = 
                    let x = new Foo.Thread(None)
                    x.Next <- Some 1 """,
            expectedStr = "operator '-'" )
                
(* TODO why does this portion not work?  specifically, last assert fails 
        printfn "changing file..."
        ReplaceFileInMemory file1 ["#light"
                                   "let xx = \"foo\""   // now x is string
                                   "printfn \"hi\""]

        // assert p1 xx is string
        MoveCursorToEndOfMarker(file1,"let x")
        TakeCoffeeBreak(this.VS) 
        let tooltip = GetQuickInfoAtCursor file1
        AssertContains(tooltip,"string")

        // assert p2 yy is int
        MoveCursorToEndOfMarker(file2,"let y")
        let tooltip = GetQuickInfoAtCursor file2
        AssertContains(tooltip,"int")

        AssertNoErrorsOrWarnings(project1)
        AssertNoErrorsOrWarnings(project2)

        printfn "rebuilding dependent project..."
        // (re)build p1 (with xx now string)
        Build(project1) |> ignore
        TakeCoffeeBreak(this.VS) 

        AssertNoErrorsOrWarnings(project1)
        AssertNoErrorsOrWarnings(project2)

        // assert p2 yy is now string
        MoveCursorToEndOfMarker(file2,"let y")
        let tooltip = GetQuickInfoAtCursor file2
        AssertContains(tooltip,"string")
*)

    [<Test>]
    member public this.``Warning.ConsistentWithLanguageService``() =  
        let fileContent = """
            open System
            mixin mixin mixin mixin mixin mixin mixin mixin mixin mixin
            mixin mixin mixin mixin mixin mixin mixin mixin mixin mixin"""
        let (_, project, file) = this.CreateSingleFileProject(fileContent, fileKind = SourceFileKind.FSX)
        TakeCoffeeBreak(this.VS) // Wait for the background compiler to catch up.
        let warnList = GetWarnings(project)
        Assert.AreEqual(20,warnList.Length)

    [<Test>]
    member public this.``Warning.ConsistentWithLanguageService.Comment``() =  
        let fileContent = """
            open System
            //mixin mixin mixin mixin mixin mixin mixin mixin mixin mixin
            //mixin mixin mixin mixin mixin mixin mixin mixin mixin mixin"""
        let (_, project, file) = this.CreateSingleFileProject(fileContent, fileKind = SourceFileKind.FSX)
        TakeCoffeeBreak(this.VS) // Wait for the background compiler to catch up.
        let warnList = GetWarnings(project)
        Assert.AreEqual(0,warnList.Length)

    [<Test>]
    [<Ignore("GetErrors function doese not work for this case")>]
    member public this.``Errorlist.WorkwithoutNowarning``() =  
        let fileContent = """
            type Fruit (shelfLife : int) as x =
                let mutable m_age = (fun () -> x)
            #nowarn "47"
            """
        let (_, project, file) = this.CreateSingleFileProject(fileContent)

        Assert.IsTrue(Build(project).BuildSucceeded)
        TakeCoffeeBreak(this.VS)
        let warnList = GetErrors(project)
        Assert.AreEqual(1,warnList.Length) 
        
// Context project system
[<TestFixture>] 
type UsingProjectSystem() = 
    inherit UsingMSBuild(VsOpts = LanguageServiceExtension.ProjectSystemTestFlavour)
