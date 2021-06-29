// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Tests.LanguageService.QuickInfo

open System
open NUnit.Framework
open Salsa.Salsa
open Salsa.VsOpsUtils
open UnitTests.TestLib.Salsa
open UnitTests.TestLib.Utils
open UnitTests.TestLib.LanguageService
open UnitTests.TestLib.ProjectSystem

[<AutoOpen>]
module QuickInfoStandardSettings = 
    let standard40AssemblyRefs  = [ "System"; "System.Core"; "System.Numerics" ]
    let queryAssemblyRefs = [ "System.Xml.Linq"; "System.Core" ]

[<TestFixture>]
[<Category "LanguageService">] 
type UsingMSBuild() = 
    inherit LanguageServiceBaseTests()

    // Work around an innocuous 'feature' with how QuickInfo is displayed, lines which 
    // should have a "\r\n" just have a "\r"
    let trimnewlines (str : string) = 
        str.Replace("\r", "").Replace("\n", "")
    
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

    let checkTooltip expected ((tooltip, span : TextSpan), (row, col)) = 
        AssertContains(tooltip, expected)
        // cursor should be inside the span
        Assert.IsTrue(row = (span.iStartLine + 1) && row = (span.iEndLine + 1), "Cursor should be one the same line with the tooltip span")
        Assert.IsTrue(col >= span.iStartIndex && col <= span.iEndIndex, "Cursor should be located inside the span")


//    (* Tests for QuickInfos ---------------------------------------------------------------- *)
    member public this.InfoInDeclarationTestQuickInfoImplWithTrim (code : string) marker expected =
        let (_, _, file) = this.CreateSingleFileProject(code)
        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)
        MoveCursorToStartOfMarker(file, marker)
        let tooltip = time1 GetQuickInfoAtCursor file "Time of first tooltip"
        AssertContains(trimnewlines tooltip, trimnewlines expected) 
        gpatcc.AssertExactly(0,0)

    member public this.CheckTooltip(code : string,marker,atStart, f, ?addtlRefAssy : list<string>) =
        let (_, _, file) = this.CreateSingleFileProject(code, ?references = addtlRefAssy)

        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)
        if atStart then
            MoveCursorToStartOfMarker(file, marker)
        else
            MoveCursorToEndOfMarker(file, marker)
        let pos = GetCursorLocation file
        let tooltip = GetQuickInfoAndSpanAtCursor file
        f (tooltip, pos)
        gpatcc.AssertExactly(0,0)
                         
    member public this.InfoInDeclarationTestQuickInfoImpl(code,marker,expected,atStart, ?addtlRefAssy : list<string>) =
        let check ((tooltip, _), _) = AssertContains(tooltip, expected)
        this.CheckTooltip(code, marker, atStart, check, ?addtlRefAssy=addtlRefAssy )

    member public this.AssertQuickInfoContainsAtEndOfMarker(code,marker,expected, ?addtlRefAssy : list<string>) =
        this.InfoInDeclarationTestQuickInfoImpl(code,marker,expected,false,?addtlRefAssy=addtlRefAssy)

    member public this.AssertQuickInfoContainsAtStartOfMarker(code, marker, expected, ?addtlRefAssy : list<string>) =
        this.InfoInDeclarationTestQuickInfoImpl(code,marker,expected,true,?addtlRefAssy=addtlRefAssy)
        
    member public this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker (code : string) marker notexpected =
        let (_, _, file) = this.CreateSingleFileProject(code)
        
        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)
        MoveCursorToEndOfMarker(file, marker)
        let tooltip = time1 GetQuickInfoAtCursor file "Time of first tooltip"
        AssertNotContains(tooltip, notexpected)         
        gpatcc.AssertExactly(0,0)

    member public this.VerifyQuickInfoDoesNotContainAnyAtStartOfMarker (code : string) marker notexpected =
        let (_, _, file) = this.CreateSingleFileProject(code)
        
        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)
        MoveCursorToStartOfMarker(file, marker)
        let tooltip = time1 GetQuickInfoAtCursor file "Time of first tooltip"
        AssertNotContains(tooltip, notexpected)         
        gpatcc.AssertExactly(0,0)

    member public this.AssertIdentifierInToolTipExactlyOnce (code : string) marker ident =
    /// Asserts that an identifier occurs exactly once in a tooltip
        let AssertIdentifierInToolTipExactlyOnce(ident, (tooltip:string)) =
            let count = tooltip.Split([| '='; '.'; ' '; '\t'; '('; ':'; ')'; '\n' |]) |> Array.filter ((=) ident) |> Array.length
            if (count <> 1) then
                Assert.Fail(sprintf "Identifier '%s' doesn't occure once in the tooltip '%s'" ident tooltip)
        
        let (_, _, file) = this.CreateSingleFileProject(code)

        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)
        MoveCursorToEndOfMarker(file, marker)
        let tooltip = GetQuickInfoAtCursor file
        AssertIdentifierInToolTipExactlyOnce(ident, tooltip)
        gpatcc.AssertExactly(0,0)

    member this.VerifyOrderOfNestedTypesInQuickInfo (source : string, marker : string, expectedExactOrder : string list, ?extraRefs : string list) = 
        let (_, _, file) = this.CreateSingleFileProject(source, ?references = extraRefs)
        
        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)
        MoveCursorToStartOfMarker(file, "(*M*)")
        let tooltip = time1 GetQuickInfoAtCursor file "Time of first tooltip"
        AssertContainsInOrder(tooltip, expectedExactOrder)

    [<Test>]
    member public this.``NestedTypesOrder``() = 
        this.VerifyOrderOfNestedTypesInQuickInfo(
            source = "type t = System.Runtime.CompilerServices.RuntimeHelpers(*M*)",
            marker = "(*M*)",
            expectedExactOrder = ["GetHashCode"; "GetObjectValue"]
            )
    
    [<Test>]
    member public this.``Operators.TopLevel``() =
        let source = """
            /// tooltip for operator
            let (===) a b = a + b
            let _ = "" === ""
            """
        this.CheckTooltip(
            code = source,
            marker = "== \"\"",
            atStart = true,
            f = (fun ((text, _), _) -> printfn "actual %s" text; Assert.IsTrue(text.Contains "tooltip for operator"))
            )
            
    [<Test>]
    member public this.``Operators.Member``() =
        let source = """
            type U = U
                with
                /// tooltip for operator
                static member (+++) (U, U) = U
            let _ = U +++ U
            """
        this.CheckTooltip(
            code = source,
            marker = "++ U",
            atStart = true,
            f = (fun ((text, _), _) -> printfn "actual %s" text; Assert.IsTrue(text.Contains "tooltip for operator"))
            )
    
    [<Test>]
    member public this.``QuickInfo.HiddenMember``() =
        // Tooltips showed hidden members - #50
        let source = """
            open System.ComponentModel

            type TypeU = { Element : string }
                with
                  [<EditorBrowsableAttribute(EditorBrowsableState.Never)>] 
                  [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>] 
                  member x._Print = x.Element.ToString() 

            let u = { Element = "abc" }
            """
        this.CheckTooltip(
            code = source,
            marker = "ypeU =",
            atStart = true,
            f = (fun ((text, _), _) -> printfn "actual %s" text; Assert.IsFalse(text.Contains "member _Print"))
            )

    [<Test>]
    member public this.``QuickInfo.ObsoleteMember``() =
        // Tooltips showed obsolete members - #50
        let source = """
            type TypeU = { Element : string }
                with
                    [<System.ObsoleteAttribute("This is replaced with Print2")>]
                    member x.Print1 = x.Element.ToString() 
                    member x.Print2 = x.Element.ToString() 

            let u = { Element = "abc" }
            """
        this.CheckTooltip(
            code = source,
            marker = "ypeU =",
            atStart = true,
            f = (fun ((text, _), _) -> printfn "actual %s" text; Assert.IsFalse(text.Contains "member Print1"))
            )

    [<Test>]
    member public this.``QuickInfo.HideBaseClassMembersTP``() =
        let fileContents = "type foo = HiddenMembersInBaseClass.HiddenBaseMembersTP(*Marker*)"
        
        this.AssertQuickInfoContainsAtStartOfMarker(
            fileContents,
            marker = "MembersTP(*Marker*)",
            expected = "type HiddenBaseMembersTP =\n  inherit TPBaseTy",
            addtlRefAssy = [PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll")])
    
    [<Test>]
    member public this.``QuickInfo.OverriddenMethods``() =
        let source = """
            type A() =
                abstract member M : unit -> unit
                /// 1234
                default this.M() = ()

            type AA() = 
                inherit A()
                /// 5678
                override this.M() = ()
            let x = new AA()
            x.M()

            let y = new A()
            y.M()
            """
        for (marker, expected) in ["x.M", "5678"; "y.M", "1234"] do
            this.CheckTooltip
                (
                    code = source,
                    marker = marker,
                    atStart = false,
                    f = (fun ((text : string, _), _) -> printfn "expected %s, actual %s" expected text; Assert.IsTrue (text.Contains(expected)))
                )

    [<Test>]
    member public this.``QuickInfoForQuotedIdentifiers``() =
        let source = """
            /// The fff function
            let fff x = x
            /// The gg gg function
            let ``gg gg`` x = x
            let r = fff 1 + ``gg gg`` 2  // no tip hovering over"""
        let identifier = "``gg gg``"
        for i = 1 to (identifier.Length - 1) do
            let marker = "+ " + (identifier.Substring(0, i))
            this.CheckTooltip (source, marker, false, checkTooltip "gg gg")
    [<Test>]
    member public this.``QuickInfoSingleCharQuotedIdentifier``() = 
        let source = """
        let ``x`` = 10
        ``x``|> printfn "%A"
        """
        this.CheckTooltip(source, "x``|>", true, checkTooltip "x")

    [<Test>]
    member public this.QuickInfoForTypesWithHiddenRepresentation() =
        let source = """
            let x = Async.AsBeginEnd
            1
        """
        let expectedTooltip = """
type Async =
  static member AsBeginEnd : computation:('Arg -> Async<'T>) -> ('Arg * AsyncCallback * obj -> IAsyncResult) * (IAsyncResult -> 'T) * (IAsyncResult -> unit)
  static member AwaitEvent : event:IEvent<'Del,'T> * ?cancelAction:(unit -> unit) -> Async<'T> (requires delegate and 'Del :> Delegate)
  static member AwaitIAsyncResult : iar:IAsyncResult * ?millisecondsTimeout:int -> Async<bool>
  static member AwaitTask : task:Task<'T> -> Async<'T> + 1 overload
  static member AwaitWaitHandle : waitHandle:WaitHandle * ?millisecondsTimeout:int -> Async<bool>
  static member CancelDefaultToken : unit -> unit
  static member Catch : computation:Async<'T> -> Async<Choice<'T,exn>>
  static member Choice : computations:seq<Async<'T option>> -> Async<'T option>
  static member FromBeginEnd : beginAction:(AsyncCallback * obj -> IAsyncResult) * endAction:(IAsyncResult -> 'T) * ?cancelAction:(unit -> unit) -> Async<'T> + 3 overloads
  static member FromContinuations : callback:(('T -> unit) * (exn -> unit) * (OperationCanceledException -> unit) -> unit) -> Async<'T>
  ...
Full name: Microsoft.FSharp.Control.Async""".TrimStart().Replace("\r\n", "\n")

        this.CheckTooltip(source, "Asyn", false, checkTooltip expectedTooltip)

    [<Test>]
    [<Category("TypeProvider")>]
    member public this.``TypeProviders.NestedTypesOrder``() = 
        let code = "type t = N1.TypeWithNestedTypes(*M*)"
        let tpReference = PathRelativeToTestAssembly( @"DummyProviderForLanguageServiceTesting.dll")
        this.VerifyOrderOfNestedTypesInQuickInfo(
            source = code,
            marker = "(*M*)",
            expectedExactOrder = ["A"; "X"; "Z"],
            extraRefs = [tpReference]
            ) 

    [<Test>]
    member public this.``GetterSetterInsideInterfaceImpl.ThisOnceAsserted``() =
        let fileContent ="""
            type IFoo =
                abstract member X : int with get,set

            type Bar =
                interface IFoo with
                    member this.X
                        with get() = 42  // hello 
                        and set(v) = id() """
        this.AssertQuickInfoContainsAtStartOfMarker(fileContent, "id", "Operators.id")

    //regression test for bug 3184 -- intellisense should normalize to ¡°int[]¡± so that [] is not mistaken for list.
    [<Test>]
    member public this.IntArrayQuickInfo() = 
      
        let fileContents = """
                            let x(*MIntArray1*) : int array = [| 1; 2; 3 |]
                            let y(*MInt[]*) : int []    = [| 1; 2; 3 |]
                            """
        this.AssertQuickInfoContainsAtStartOfMarker(fileContents, "x(*MIntArray1*)", "int array")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "y(*MInt[]*)", "int []")
        
    //Verify no quickinfo -- link name string have 
    [<Test>]
    member public this.LinkNameStringQuickInfo() = 
      
        let fileContents = """
                            let y = 1
                            let f x = "x"(*Marker1*)
                            let g z = "y"(*Marker2*)
                            """
        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "\"x\"(*Marker1*)", "")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "\"y\"(*Marker2*)", "")

    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.XmlDocAttribute")>]
    //This is to test the correct TypeProvider Type message is shown or not in the TypeProviderXmlDocAttribute
    member public this.``TypeProvider.XmlDocAttribute.Type.Comment``() = 
        
        let fileContents = """
                                let a = typeof<N.T(*Marker*)> """

        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "T(*Marker*)", "This is a synthetic type created by me!",
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithAdequateComment.dll")])
    
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.XmlDocAttribute")>]
    //This is to test for long message in the TypeProviderXmlDocAttribute for TypeProvider Type
    member public this.``TypeProvider.XmlDocAttribute.Type.WithLongComment``() = 
        
        let fileContents = """
                                let a = typeof<N.T(*Marker*)> """

        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "T(*Marker*)",
         "This is a synthetic type created by me!. Which is used to test the tool tip of the typeprovider type to check if it shows the right message or not.",
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithLongComment.dll")])
    
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.XmlDocAttribute")>]
    [<Ignore("This is not outputting 'member', but 'event'.")>]
    //This is to test when the message is null in the TypeProviderXmlDocAttribute for TypeProvider Type
    member public this.``TypeProvider.XmlDocAttribute.Type.WithNullComment``() = 
        
        let fileContents = """
                                let a = typeof<N.T(*Marker*)> """

        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "T(*Marker*)",
         "type T =\n  new : unit -> T\n  static member M : unit -> int []\n  static member StaticProp : decimal\n  member Event1 : EventHandler", 
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithNullComment.dll")])
    
    [<Test>]
    [<Category("TypeProvider")>]    
    [<Category("TypeProvider.XmlDocAttribute")>]
    [<Ignore("This is not outputting 'member', but 'event'.")>]
    //This is to test when there is empty message from the TypeProviderXmlDocAttribute for TypeProvider Type
    member public this.``TypeProvider.XmlDocAttribute.Type.WithEmptyComment``() =

        let fileContents = """
                                let a = typeof<N.T(*Marker*)> """
        
        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "T(*Marker*)",
         "type T =\n  new : unit -> T\n  static member M : unit -> int []\n  static member StaticProp : decimal\n  member Event1 : EventHandler",
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithEmptyComment.dll")])
         

    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.XmlDocAttribute")>]
    //This is to test the multi-language in the TypeProviderXmlDocAttribute for TypeProvider Type
    member public this.``TypeProvider.XmlDocAttribute.Type.LocalizedComment``() = 
        
        let fileContents = """
                                let a = typeof<N.T(*Marker*)> """

        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "T(*Marker*)",
         "This is a synthetic type Localized! ኤፍ ሻርፕ",
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithLocalizedComment.dll")])
   
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.XmlDocAttribute")>]
    //This is to test the correct TypeProvider Constructor message is shown or not in the TypeProviderXmlDocAttribute
    member public this.``TypeProvider.XmlDocAttribute.Constructor.Comment``() = 
        
        let fileContents = """
                                let foo = new N.T(*Marker*)() """

        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "T(*Marker*)", "This is a synthetic .ctor created by me for N.T",
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithAdequateComment.dll")])
    
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.XmlDocAttribute")>]
    //This is to test for long message in the TypeProviderXmlDocAttribute for TypeProvider Constructor
    member public this.``TypeProvider.XmlDocAttribute.Constructor.WithLongComment``() = 
        
        let fileContents = """
                                let foo = new N.T(*Marker*)() """

        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "T(*Marker*)",
         "This is a synthetic .ctor created by me for N.T. Which is used to test the tool tip of the typeprovider Constructor to check if it shows the right message or not.",
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithLongComment.dll")])
    
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.XmlDocAttribute")>]
    //This is to test when the message is null in the TypeProviderXmlDocAttribute for TypeProvider Constructor
    member public this.``TypeProvider.XmlDocAttribute.Constructor.WithNullComment``() = 
        
        let fileContents = """
                                let foo = new N.T(*Marker*)() """

        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "T(*Marker*)",
         "N.T() : N.T", 
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithNullComment.dll")])
    
    [<Test>]
    [<Category("TypeProvider")>]    
    [<Category("TypeProvider.XmlDocAttribute")>]
    //This is to test when there is empty message from the TypeProviderXmlDocAttribute for TypeProvider Constructor
    member public this.``TypeProvider.XmlDocAttribute.Constructor.WithEmptyComment``() =

        let fileContents = """
                                let foo = new N.T(*Marker*)() """
        
        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "T(*Marker*)",
         "N.T() : N.T",
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithEmptyComment.dll")])
    
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.XmlDocAttribute")>]
    //This is to test the multi-language in the TypeProviderXmlDocAttribute for TypeProvider Constructor
    member public this.``TypeProvider.XmlDocAttribute.Constructor.LocalizedComment``() = 
        
        let fileContents = """
                                let foo = new N.T(*Marker*)() """

        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "T(*Marker*)",
         "This is a synthetic .ctor Localized! ኤፍ ሻርፕ for N.T",
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithLocalizedComment.dll")])
        

    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.XmlDocAttribute")>]
    //This is to test the correct TypeProvider event message is shown or not in the TypeProviderXmlDocAttribute
    member public this.``TypeProvider.XmlDocAttribute.Event.Comment``() = 
        
        let fileContents = """ 
                                let t = new N.T()
                                t.Event1(*Marker*)"""

        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "Event1(*Marker*)",
         "This is a synthetic *event* created by me for N.T",
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithAdequateComment.dll")])
    
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.XmlDocAttribute")>]
    //This is to test the multi-language in the TypeProviderXmlDocAttribute for TypeProvider Event
    member public this.``TypeProvider.XmlDocAttribute.Event.LocalizedComment``() = 
        
        let fileContents = """ 
                                let t = new N.T()
                                t.Event1(*Marker*)"""

        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "Event1(*Marker*)", 
         "This is a synthetic *event* Localized! ኤፍ ሻርፕ for N.T",
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithLocalizedComment.dll")])
   
    [<Test>]
    [<Category("QuickInfo.ParamsAttribute")>]
    //This is to test the multi-language in the TypeProviderXmlDocAttribute for TypeProvider Event
    member public this.``TypeProvider.ParamsAttributeTest``() = 
        
        let fileContents = """ 
                                let t = "a".Split('c')"""

        this.AssertQuickInfoContainsAtEndOfMarker (fileContents, "Spl", "[<System.ParamArray>] separator")

    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.XmlDocAttribute")>]
    //This is to test for long message in the TypeProviderXmlDocAttribute for TypeProvider Event
    member public this.``TypeProvider.XmlDocAttribute.Event.WithLongComment``() = 
        
        let fileContents = """ 
                                let t = new N.T()
                                t.Event1(*Marker*)"""

        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "Event1(*Marker*)",
         "This is a synthetic *event* created by me for N.T. Which is used to test the tool tip of the typeprovider Event to check if it shows the right message or not.!",
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithLongComment.dll")])
    
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.XmlDocAttribute")>]
    [<Ignore("This is not outputting 'member', but 'event'.")>]
    //This is to test when the message is null in the TypeProviderXmlDocAttribute for TypeProvider Event
    member public this.``TypeProvider.XmlDocAttribute.Event.WithNullComment``() = 
        
        let fileContents = """ 
                                let t = new N.T()
                                t.Event1(*Marker*)"""

        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "Event1(*Marker*)",
         "member N.T.Event1: IEvent<System.EventHandler,System.EventArgs>", 
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithNullComment.dll")])
    
    [<Test>]
    [<Category("TypeProvider")>]    
    [<Category("TypeProvider.XmlDocAttribute")>]
    [<Ignore("This is not outputting 'member', but 'event'.")>]
    //This is to test when there is empty message from the TypeProviderXmlDocAttribute for TypeProvider Event
    member public this.``TypeProvider.XmlDocAttribute.Event.WithEmptyComment``() =

        let fileContents = """ 
                                let t = new N.T()
                                t.Event1(*Marker*)"""
        
        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "Event1(*Marker*)",
         "member N.T.Event1: IEvent<System.EventHandler,System.EventArgs>",
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithEmptyComment.dll")])
    

    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.XmlDocAttribute")>]
    //This is to test the correct TypeProvider Method message is shown or not in the TypeProviderXmlDocAttribute
    member public this.``TypeProvider.XmlDocAttribute.Method.Comment``() = 
        
        let fileContents = """ 
                                let t = new N.T.M(*Marker*)()"""

        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "M(*Marker*)",
         "This is a synthetic *method* created by me!!",
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithAdequateComment.dll")])
    
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.XmlDocAttribute")>]
    //This is to test the multi-language in the TypeProviderXmlDocAttribute for TypeProvider Method
    member public this.``TypeProvider.XmlDocAttribute.Method.LocalizedComment``() = 
        
        let fileContents = """ 
                                let t = new N.T.M(*Marker*)()"""

        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "M(*Marker*)", 
         "This is a synthetic *method* Localized! ኤፍ ሻርፕ",
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithLocalizedComment.dll")])
   
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.XmlDocAttribute")>]
    //This is to test for long message in the TypeProviderXmlDocAttribute for TypeProvider Method
    member public this.``TypeProvider.XmlDocAttribute.Method.WithLongComment``() = 
        
        let fileContents = """ 
                                let t = new N.T.M(*Marker*)()"""

        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "M(*Marker*)",
         "This is a synthetic *method* created by me!!. Which is used to test the tool tip of the typeprovider Method to check if it shows the right message or not.!",
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithLongComment.dll")])
    
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.XmlDocAttribute")>]
    //This is to test when the message is null in the TypeProviderXmlDocAttribute for TypeProvider Method
    member public this.``TypeProvider.XmlDocAttribute.Method.WithNullComment``() = 
        
        let fileContents = """ 
                                let t = new N.T.M(*Marker*)()"""

        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "M(*Marker*)",
         "N.T.M() : int []", 
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithNullComment.dll")])
    
    [<Test>]
    [<Category("TypeProvider")>]    
    [<Category("TypeProvider.XmlDocAttribute")>]
    //This is to test when there is empty message from the TypeProviderXmlDocAttribute for TypeProvider Method
    member public this.``TypeProvider.XmlDocAttribute.Method.WithEmptyComment``() =

        let fileContents = """ 
                                let t = new N.T.M(*Marker*)()"""
        
        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "M(*Marker*)",
         "N.T.M() : int []",
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithEmptyComment.dll")])
    

    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.XmlDocAttribute")>]
    //This is to test the correct TypeProvider Property message is shown or not in the TypeProviderXmlDocAttribute
    member public this.``TypeProvider.XmlDocAttribute.Property.Comment``() = 
        
        let fileContents = """ 
                                let p = N.T.StaticProp(*Marker*)"""

        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "StaticProp(*Marker*)",
         "This is a synthetic *property* created by me for N.T",
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithAdequateComment.dll")])
    
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.XmlDocAttribute")>]
    //This is to test the multi-language in the TypeProviderXmlDocAttribute for TypeProvider Property
    member public this.``TypeProvider.XmlDocAttribute.Property.LocalizedComment``() = 
        
        let fileContents = """ 
                                let p = N.T.StaticProp(*Marker*)"""

        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "StaticProp(*Marker*)", 
         "This is a synthetic *property* Localized! ኤፍ ሻርፕ for N.T",
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithLocalizedComment.dll")])
   
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.XmlDocAttribute")>]
    //This is to test for long message in the TypeProviderXmlDocAttribute for TypeProvider Property
    member public this.``TypeProvider.XmlDocAttribute.Property.WithLongComment``() = 
        
        let fileContents = """
                                let p = N.T.StaticProp(*Marker*)"""

        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "StaticProp(*Marker*)",
         "This is a synthetic *property* created by me for N.T. Which is used to test the tool tip of the typeprovider Property to check if it shows the right message or not.!",
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithLongComment.dll")])
    
    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.XmlDocAttribute")>]
    //This is to test when the message is null in the TypeProviderXmlDocAttribute for TypeProvider Property
    member public this.``TypeProvider.XmlDocAttribute.Property.WithNullComment``() = 
        
        let fileContents = """
                                let p = N.T.StaticProp(*Marker*)"""

        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "StaticProp(*Marker*)",
         "property N.T.StaticProp: decimal", 
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithNullComment.dll")])
    
    [<Test>]
    [<Category("TypeProvider")>]    
    [<Category("TypeProvider.XmlDocAttribute")>]
    //This is to test when there is empty message from the TypeProviderXmlDocAttribute for TypeProvider Property
    member public this.``TypeProvider.XmlDocAttribute.Property.WithEmptyComment``() =

        let fileContents = """
                                let p = N.T.StaticProp(*Marker*)"""
        
        this.AssertQuickInfoContainsAtStartOfMarker (fileContents, "StaticProp(*Marker*)",
         "property N.T.StaticProp: decimal",
         addtlRefAssy = [PathRelativeToTestAssembly( @"XmlDocAttributeWithEmptyComment.dll")])
    

    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.StaticParameters")>]
    //This test case Verify that when Hover over foo the correct quickinfo is displayed for TypeProvider static parameter
    //Dummy Type Provider exposes a parametric type (N1.T) that takes 2 static params (string * int)
    member public this.``TypeProvider.StaticParameters.Correct``() =
        
        let fileContents = """ 
                       type foo(*Marker*) = N1.T< const "Hello World",2>"""
        
        this.AssertQuickInfoContainsAtStartOfMarker(
            fileContents,
            marker = "foo(*Marker*)",
            expected = "type foo = N1.T",
            addtlRefAssy = [PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll")])

    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.StaticParameters")>]
    //This test case Verify that when Hover over foo the correct quickinfo is displayed
    //Dummy Type Provider exposes a parametric type (N1.T) that takes 2 static params (string * int)
    //As you can see this is "Negative Case" to check that when given invalid static Parameter quickinfo shows "type foo = obj"
    member public this.``TypeProvider.StaticParameters.Negative.Invalid``() =

        let fileContents = """                    
                    type foo(*Marker*) = N1.T< const 100,2>"""

        this.AssertQuickInfoContainsAtStartOfMarker(
            fileContents,
            marker = "foo(*Marker*)",
            expected = "type foo",
            addtlRefAssy = [PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll")])

    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.StaticParameters")>]
    //This test case Verify that when Hover over foo the XmlComment is shown in quickinfo
    //Dummy Type Provider exposes a parametric type (N1.T) that takes 2 static params (string * int)
    member public this.``TypeProvider.StaticParameters.XmlComment``() =
              
        let fileContents = """                    
                    ///XMLComment
                    type foo(*Marker*) = N1.T< const "Hello World",2>"""

        this.AssertQuickInfoContainsAtStartOfMarker( 
            fileContents,
            marker = "foo(*Marker*)",
            expected = "XMLComment",
            addtlRefAssy = [PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll")])

    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.StaticParameters")>]
    member public this.``TypeProvider.StaticParameters.QuickInfo.OnTheErasedType``() =
        let fileContents = """type TTT = Samples.FSharp.RegexTypeProvider.RegexTyped< @"(?<AreaCode>^\d{3})-(?<PhoneNumber>\d{3}-\d{7}$)">"""
        this.AssertQuickInfoContainsAtStartOfMarker( 
            fileContents,
            marker = "TTT",
            expected = "type TTT = Samples.FSharp.RegexTypeProvider.RegexTyped<...>\nFull name: File1.TTT",
            addtlRefAssy = ["System"; PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll")])

    [<Test>]
    [<Category("TypeProvider")>]
    [<Category("TypeProvider.StaticParameters")>]
    member public this.``TypeProvider.StaticParameters.QuickInfo.OnNestedErasedTypeProperty``() =
        let fileContents = """
            type T = Samples.FSharp.RegexTypeProvider.RegexTyped< @"(?<AreaCode>^\d{3})-(?<PhoneNumber>\d{3}-\d{7}$)">
            let reg = T() 
            let r = reg.Match("425-123-2345").AreaCode.Value
            """
        this.AssertQuickInfoContainsAtStartOfMarker( 
            fileContents,
            marker = "reaCode.Val",
            expected = """property Samples.FSharp.RegexTypeProvider.RegexTyped<...>.MatchType.AreaCode: System.Text.RegularExpressions.Group""",
            addtlRefAssy = ["System"; PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll")])
    
    // Regression for 2948
    [<Test>]
    member public this.TypeRecordQuickInfo() = 
      
        let fileContents = """namespace NS
                           type Re(*MarkerRecord*) = { X : int } """
        let expectedQuickinfoTypeRecored = "type Re =  { X: int }"
        
        this.InfoInDeclarationTestQuickInfoImplWithTrim fileContents "Re(*MarkerRecord*)" expectedQuickinfoTypeRecored
    
    [<Test>]
    member public this.``QuickInfo.LetBindingsInTypes``() = 
        let code = 
            """
            type A() = 
                let fff n = n + 1                
            """
        this.AssertQuickInfoContainsAtEndOfMarker(code, "let ff", "val fff : (int -> int)")

    // Regression for 2494
    [<Test>]
    member public this.TypeConstructorQuickInfo() = 
      
        let fileContents = """
                            open System

                            type PriorityQueue(*MarkerType*)<'k,'a> =
                              | Nil(*MarkerDataConstructor*)
                              | Branch of 'k * 'a * PriorityQueue<'k,'a> * PriorityQueue<'k,'a>
  
                            module PriorityQueue(*MarkerModule*) =
                              let empty = Nil
  
                              let minKeyValue = function
                                | Nil             -> failwith "empty queue"
                                | Branch(k,a,_,_) -> (k,a)
    
                              let minKey pq = fst (minKeyValue pq(*MarkerVal*))
  
                              let singleton(*MarkerLastLine*) k a = Branch(k,a,Nil,Nil)
                            """
        //Verify the quick info as expected
        let expectedquickinfoPriorityQueue = "type PriorityQueue<'k,'a> =  | Nil  | Branch of 'k * 'a * PriorityQueue<'k,'a> * PriorityQueue<'k,'a>"
        let expectedquickinfoNil = "union case PriorityQueue.Nil: PriorityQueue<'k,'a>"
        let expectedquickinfoPriorityQueueinModule = "module PriorityQueue\n\nfrom File1"
        let expectedquickinfoVal = "val pq : PriorityQueue<'a,'b>"
        let expectedquickinfoLastLine = "val singleton : k:'a -> a:'b -> PriorityQueue<'a,'b>"

        this.InfoInDeclarationTestQuickInfoImplWithTrim fileContents "PriorityQueue(*MarkerType*)" expectedquickinfoPriorityQueue
        this.InfoInDeclarationTestQuickInfoImplWithTrim fileContents "Nil(*MarkerDataConstructor*)" expectedquickinfoNil
        this.InfoInDeclarationTestQuickInfoImplWithTrim fileContents "PriorityQueue(*MarkerModule*)" expectedquickinfoPriorityQueueinModule
        this.InfoInDeclarationTestQuickInfoImplWithTrim fileContents "pq(*MarkerVal*)" expectedquickinfoVal
        this.InfoInDeclarationTestQuickInfoImplWithTrim fileContents "singleton(*MarkerLastLine*)" expectedquickinfoLastLine
        
    [<Test>]
    member public this.NamedDUFieldQuickInfo() = 
      
        let fileContents = """
                            type NamedFieldDU(*MarkerType*) =
                              | Case1(*MarkerCase1*) of V1 : int * bool * V3 : float
                              | Case2(*MarkerCase2*) of ``Big Name`` : int * Item2 : bool
                              | Case3(*MarkerCase3*) of Item : int
                              
                            exception NamedExn(*MarkerException*) of int * V2 : string * bool * Data9 : float
                            """
        //Verify the quick info as expected
        let expectedquickinfoType = "type NamedFieldDU =  | Case1 of V1: int * bool * V3: float  | Case2 of Big Name: int * bool  | Case3 of int"
        let expectedquickinfoCase1 = "union case NamedFieldDU.Case1: V1: int * bool * V3: float -> NamedFieldDU"
        let expectedquickinfoCase2 = "union case NamedFieldDU.Case2: Big Name: int * bool -> NamedFieldDU"
        let expectedquickinfoCase3 = "union case NamedFieldDU.Case3: int -> NamedFieldDU"
        let expectedquickinfoException = "exception NamedExn of int * V2: string * bool * Data9: float"

        this.InfoInDeclarationTestQuickInfoImplWithTrim fileContents "NamedFieldDU(*MarkerType*)" expectedquickinfoType
        this.InfoInDeclarationTestQuickInfoImplWithTrim fileContents "Case1(*MarkerCase1*)" expectedquickinfoCase1
        this.InfoInDeclarationTestQuickInfoImplWithTrim fileContents "Case2(*MarkerCase2*)" expectedquickinfoCase2
        this.InfoInDeclarationTestQuickInfoImplWithTrim fileContents "Case3(*MarkerCase3*)" expectedquickinfoCase3
        this.InfoInDeclarationTestQuickInfoImplWithTrim fileContents "NamedExn(*MarkerException*)" expectedquickinfoException

    [<Test>]
    member public this.``EnsureNoAssertFromBadParserRangeOnAttribute``() = 
        let fileContents = """ 
                [<System.Obsolete>]
                Types foo = int"""
        this.AssertQuickInfoContainsAtEndOfMarker (fileContents, "ype", "")  // just want to ensure there is no assertion fired by the parse tree walker
   
    [<Test>]
    member public this.``ShiftKeyDown``() =
        ShiftKeyDown(this.VS)
        this.AssertQuickInfoContainsAtEndOfMarker 
          ("""#light""","#ligh","")

    [<Test>]
    member public this.``ActivePatterns.Declaration``() =        
        this.AssertQuickInfoContainsAtEndOfMarker 
          ("""let ( |One|Two| ) x = One(x+1)""","ne|Tw","int -> Choice")

    [<Test>]
    member public this.``ActivePatterns.Result``() =        
        this.AssertQuickInfoContainsAtEndOfMarker 
          ("""let ( |One|Two| ) x = One(x+1)""","= On","active pattern result One: int -> Choice")


    [<Test>]
    member public this.``ActivePatterns.Value``() =        
        this.AssertQuickInfoContainsAtEndOfMarker 
         ("""let ( |One|Two| ) x = One(x+1)
             let patval = (|One|Two|) // use""","= (|On","int -> Choice")
          
    [<Test>]
    member public this.``Regression.InDeclaration.Bug3176a``() =        
        this.AssertQuickInfoContainsAtEndOfMarker 
          ("""type T<'a> = { aaaa : 'a; bbbb : int } ""","aa","aaaa")

    [<Test>]
    member public this.``Regression.InDeclaration.Bug3176c``() =        
        this.AssertQuickInfoContainsAtEndOfMarker 
          ("""type C =
                val aaaa : int""","aa","aaaa")
                      
    [<Test>]
    member public this.``Regression.InDeclaration.Bug3176d``() =        
        this.AssertQuickInfoContainsAtEndOfMarker 
          ("""type DU<'a> =
                | DULabel of 'a""","DULab","DULabel")

    [<Test>]
    member public this.``Regression.Generic.3773a``() =        
        this.AssertQuickInfoContainsAtEndOfMarker 
          ("""let rec M2<'a>(a:'a) = M2(a)""","let rec M","val M2 : a:'a -> obj")

    // Before this fix, if the user hovered over 'cccccc' they would see 'Yield'
    [<Test>]
    member public this.``Regression.ComputationExpressionMemberAppearingInQuickInfo``() =        
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker 
            """
                module Test
                let q2 = 
                    query { 
                        for p in [1;2] do            
                            join cccccc in [3;4] on (p = cccccc)
                            yield cccccc
                    }"""
            "yield ccc" "Yield"
          
    // Before this fix, if the user hovered over get or set in a property then
    // they would see a quickinfo for any available function named get or set.
    // The tests below define a get function with 'let' and then test to make sure that
    // this isn't the get seen in the tool tip.
    [<Test>]
    member public this.``Regression.AccessorMutator.Bug4903a``() =        
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker 
            """namespace CountChocula
               type BooBerry() =
                   let get() = ""
                   member source.Prop
                       with get() : int = 0
                       and set(value:int) : unit = ()""" 
            "with g" "string"
          
    [<Test>]
    member public this.``Regression.AccessorMutator.Bug4903d``() =        
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker 
            """namespace CountChocula
               type BooBerry() =
                   member source.AMethod() = ()
                   member source.AProperty
                       with get() : int = 0
                       and set(value:int) : unit = ()""" 
            "AMetho" "string"          
          
    [<Test>]
    member public this.``Regression.AccessorMutator.Bug4903b``() =        
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker 
            """namespace CountChocula
               type BooBerry() =
                   let get() = ""
                   member source.Prop
                       with get() : int = 0
                       and set(value:int) : unit = ()"""
            "and s" "seq"          
          
    [<Test>]
    member public this.``Regression.AccessorMutator.Bug4903c``() =        
        this.AssertQuickInfoContainsAtEndOfMarker 
           ("""namespace CountChocula
               type BooBerry() =
                   let get() = ""
                   member source.Prop
                       with get() : int = 0
                       and set(value:int) : unit = ()""",
            "let g","string")
          

    [<Test>]
    member public this.``ParamsArrayArgument.OnType``() =        
        this.AssertQuickInfoContainsAtEndOfMarker 
           ("""
                type A() =
                    static member Foo([<System.ParamArrayAttribute>] a : int[]) = ()
                let r = A.Foo(42)""" ,
            "type A","[<ParamArray>] a:"    )

    [<Test>]
    member public this.``ParamsArrayArgument.OnMethod``() =        
        this.AssertQuickInfoContainsAtEndOfMarker 
           ("""
                type A() =
                    static member Foo([<System.ParamArrayAttribute>] a : int[]) = ()
                let r = A.Foo(42)""" ,
            "A.Foo","[<System.ParamArray>] a:"    )
          
    [<Test>]
    member public this.``Regression.AccessorMutator.Bug4903e``() =        
        this.AssertQuickInfoContainsAtEndOfMarker 
           ("""namespace CountChocula
               type BooBerry() =
                   let get() = ""
                   member source.Prop
                       with get() : int = 0
                       and set(value:int) : unit = ()""" ,
            "member source.Pr","Prop"    )
          
    [<Test>]
    member public this.``Regression.AccessorMutator.Bug4903f``() =        
        this.AssertQuickInfoContainsAtEndOfMarker 
           ("""namespace CountChocula
               type BooBerry() =
                   let get() = ""
                   member source.Prop
                       with get() : int = 0
                       and set(value:int) : unit = ()""" ,
            "member source.Pr","int"                   )
          
    [<Test>]
    member public this.``Regression.AccessorMutator.Bug4903g``() =        
        this.AssertQuickInfoContainsAtEndOfMarker 
           ("""namespace CountChocula
               type BooBerry() =
                   let get() = ""
                   member source.Prop
                       with get() : int = 0
                       and set(value:int) : unit = ()""" ,
            "member sou","source"                )

    [<Test>]
    member public this.``Regression.RecursiveDefinition.Generic.3773b``() =        
        this.AssertQuickInfoContainsAtEndOfMarker 
          ("""let rec M1<'a>(a:'a) = M1(0)""","let rec M","val M1 : a:int -> 'a")
          
        //regression test for bug Dev11:138110 - "F# language service hover tip for ITypeProvider does now show Invalidate event"
    [<Test>]
    member public this.``Regression.ImportedEvent.138110``() =
        let fileContents = """
open Microsoft.FSharp.Core.CompilerServices
let f (tp:ITypeProvider(*$$$*)) = tp.Invalidate
                           """
        this.AssertQuickInfoContainsAtStartOfMarker(
            fileContents, 
            "Provider(*$$$*)", 
            "Invalidate", addtlRefAssy=standard40AssemblyRefs ) //"FSharp.Core" add the reference in SxS will cause build failure and intellisense broken, the dll is added by default



    [<Test>]
    member public this.``Declaration.CyclicalDeclarationDoesNotCrash``() =
        this.AssertQuickInfoContainsAtEndOfMarker
          ("""type (*1*)A = int * (*2*)A ""","(*2*)","type A")

    [<Test>]
    member public this.``JustAfterIdentifier``() =
        this.AssertQuickInfoContainsAtEndOfMarker
          ("""let f x = x + 1 ""","let f","int")
        
    [<Test; Ignore(".NET classes are treated differently now. Should this be revisited? Probably not.")>]
    member public this.``FrameworkClass``() =
        let fileContent = """let l = new System.Collections.Generic.List<int>()"""
        let marker = "Generic.List"
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,marker,"member Capacity : int\n")
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,marker,"member Clear : unit -> unit\n")
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker fileContent marker "get_Capacity"
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker fileContent marker "set_Capacity"
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker fileContent marker "get_Count"
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker fileContent marker "set_Count"
          
    [<Test>]
    member public this.``FrameworkClassNoMethodImpl``() =
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker
          """let l = new System.Collections.Generic.LinkedList<int>()"""
           "Generic.LinkedList" "System.Collections.ICollection.ISynchronized" // Bug 5092: A framework class contained a private method impl

    [<Test>]
    [<Ignore("https://github.com/dotnet/fsharp/issues/11724")>]
    member public this.``Regression.ModulesFromExternalLibrariesBug5785``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let projectLib = CreateProject(solution,"testlib")
        let file = AddFileFromText(projectLib,"MyLibrary.fs", 
                      [ "module MyLibrary"
                        "let x = 1"
                        "module Nested ="
                        "    let y = 2"
                        "    module Deeper ="
                        "        let z = 3"
                      ])
        let project = CreateProject(solution,"testapp")
        let file = AddFileFromText(project,"App.fs", 
                      [ "let a = MyLibrary.Nested.Deeper.z" ])
        SetConfigurationAndPlatform(project, "Debug|AnyCPU")  // we must set config/platform when building with ProjectReferences
        SetConfigurationAndPlatform(projectLib, "Debug|AnyCPU")  // we must set config/platform when building with ProjectReferences
        AddProjectReference(project, projectLib)
        let br = BuildTarget(projectLib, "Build") // build the dependent library
        Assert.IsTrue(br.BuildSucceeded, "build should succeed")
        let file = OpenFile(project,"App.fs")
        TakeCoffeeBreak(this.VS) // Wait for the background compiler to catch up.

        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)

        ShiftKeyUp(this.VS)
        MoveCursorToEndOfMarker(file, "MyLi")
        let tooltip = time1 GetQuickInfoAtCursor file "Time of first tooltip"
        AssertMatchesRegex '\n' "module MyLibrary[Filename:.*\\bin\\Debug\\testlib.exe]\n[Signature:T:MyLibrary]" tooltip  

        MoveCursorToEndOfMarker(file, "Nes")
        let tooltip = time1 GetQuickInfoAtCursor file "Time of first tooltip"
        AssertMatchesRegex '\n' "module Nested\n\nfrom MyLibrary[Filename:.*\\bin\Debug\\testlib.exe]\n[Signature:T:MyLibrary.Nested]" tooltip

        MoveCursorToEndOfMarker(file, "Dee")
        let tooltip = time1 GetQuickInfoAtCursor file "Time of first tooltip"
        AssertMatchesRegex '\n' "module Deeper\n\nfrom MyLibrary.Nested[Filename:.*\\bin\\Debug\\testlib.exe]\n[Signature:T:MyLibrary.Nested]" tooltip

        gpatcc.AssertExactly(0,0)

    (* ------------------------------------------------------------------------------------- *)


    /// Even though we don't show squiggles, some types will still be known. For example, System.String.
    [<Test>]
    member public this.``OrphanFs.BaselineIntellisenseStillWorks``() = 
        this.AssertQuickInfoContainsAtEndOfMarker
           ("""let astring = "Hello" ""","let astr","string")

    /// FEATURE: User may hover over a type or identifier and get basic information about it in a tooltip.
    [<Test>]
    member public this.``Basic``() = 
        let fileContent = """type (*bob*)Bob() = 
                                  let x = 1"""
        let marker = "(*bob*)"
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,marker,"Bob =")
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,marker,"Bob =")

    [<Test>]
    member public this.``ModuleDefinition.ModuleNoNewLines``() = 
        let fileContent = """module XXX
                             type t = C3
                             module YYY =
                                type t = C4
                             ///Doc
                             module ZZZ =
                                type t = C5 """
        // The <summary> arises because the xml doc mechanism places these before handing them to VS for processing.
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"XX","module XXX")
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"YY","module YYY\n\nfrom XXX")
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"ZZ","module ZZZ\n\nfrom XXX<summary>\n\nDoc</summary>")

    [<Test>]
    member public this.``IdentifierWithTick``() = 
        let code = 
                                    ["#light"
                                     "let x = 1"
                                     "let x' = \"foo\""
                                     "if (*aaa*)x = 1 then (*bbb*)x' else \"\""
                                     ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        MoveCursorToEndOfMarker(file,"(*aaa*)")
        let tooltip = GetQuickInfoAtCursor file
        AssertContains(tooltip,"val x : int")

        MoveCursorToEndOfMarker(file,"(*bbb*)")
        let tooltip = GetQuickInfoAtCursor file
        AssertContains(tooltip,"val x' : string")

    [<Test>]
    member public this.``NegativeTest.CharLiteralNotConfusedWithIdentifierWithTick``() = 
        let fileContent = """let x = 1"
                             let y = 'x' """
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"'x","")   // no tooltips for char literals

    [<Test>]
    member public this.``QueryExpression.QuickInfoSmokeTest1``() = 
        let fileContent = """let q = query { for x in ["1"] do select x }"""
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"selec","custom operation: select", addtlRefAssy=standard40AssemblyRefs)
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"selec","custom operation: select ('Result)"   , addtlRefAssy=standard40AssemblyRefs)
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"selec","Calls"   , addtlRefAssy=standard40AssemblyRefs)
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"selec","Linq.QueryBuilder.Select"  , addtlRefAssy=standard40AssemblyRefs )

    [<Test>]
    member public this.``QueryExpression.QuickInfoSmokeTest2``() = 
        let fileContent = """let q = query { for x in ["1"] do join y in ["2"] on (x = y); select (x,y) }"""
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"joi","custom operation: join"  , addtlRefAssy=standard40AssemblyRefs )
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"joi","join var in collection on (outerKey = innerKey)"   , addtlRefAssy=standard40AssemblyRefs)
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"joi","Calls"  , addtlRefAssy=standard40AssemblyRefs )
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"joi","Linq.QueryBuilder.Join"  , addtlRefAssy=standard40AssemblyRefs )

    [<Test>]
    member public this.``QueryExpression.QuickInfoSmokeTest3``() = 
        let fileContent = """let q = query { for x in ["1"] do groupJoin y in ["2"] on (x = y) into g; select (x,g) }"""
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"groupJoin","custom operation: groupJoin"  , addtlRefAssy=standard40AssemblyRefs )
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"groupJoin","groupJoin var in collection on (outerKey = innerKey)"   , addtlRefAssy=standard40AssemblyRefs)
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"groupJoin","Calls"  , addtlRefAssy=standard40AssemblyRefs )
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"groupJoin","Linq.QueryBuilder.GroupJoin"   , addtlRefAssy=standard40AssemblyRefs)


    /// Hovering over a literal string should not show data tips for variable names that appear in the string
    [<Test>]
    member public this.``StringLiteralWithIdentifierLookALikes.Bug2360_A``() =
        let fileContent = """let y = 1
                             let f x = "x"
                             let g z = "y" """
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker fileContent "f x = \"" "val"

    /// Hovering over a literal string should not show data tips for variable names that appear in the string
    [<Test>]
    member public this.``Regression.StringLiteralWithIdentifierLookALikes.Bug2360_B``() =
        let fileContent = """let y = 1"""
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"let ","int")

    /// FEATURE: Intellisense information from types in earlier files in the project is available in subsequent files.        
    [<Test>]
    member public this.``AcrossMultipleFiles``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let file1 = AddFileFromText(project,"File1.fs",
                                    ["#light"
                                     "type Bob() = "
                                     "    let x = 1"])
        let file2 = AddFileFromText(project,"File2.fs",
                                    ["#light"
                                     "let bob = new File1.Bob()"])
        let file1 = OpenFile(project,"File1.fs")
        let file2 = OpenFile(project,"File2.fs")
        
        // Get the tooltip at type Bob        
        MoveCursorToEndOfMarker(file2,"let bo")
        let tooltip = time1 GetQuickInfoAtCursor file2 "Time of first tooltip"
        printf "First-%s\n" tooltip
        AssertContains(tooltip,"File1.Bob")
        
        // Get the tooltip again
        MoveCursorToEndOfMarker(file2,"let bo")
        let tooltip = time1 GetQuickInfoAtCursor file2 "Time of second tooltip"
        printf "Second-%s\n" tooltip
        AssertContains(tooltip,"File1.Bob")

    /// FEATURE: Linked files work
    [<Test>]
    member public this.``AcrossLinkedFiles``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let file1 = AddLinkedFileFromTextEx(project, @"..\LINK.FS", @"..\link.fs", @"MyLink.fs",
                                    ["#light"
                                     "type Bob() = "
                                     "    let x = 1"])
        let file2 = AddFileFromText(project,"File2.fs",
                                    ["#light"
                                     "let bob = new Link.Bob()"])
        let file1 = OpenFile(project, @"..\link.fs")
        let file2 = OpenFile(project, @"File2.fs")
        
        // Get the tooltip at type Bob        
        MoveCursorToEndOfMarker(file2,"let bo")
        let tooltip = time1 GetQuickInfoAtCursor file2 "Time of first tooltip"
        printf "First-%s\n" tooltip
        AssertContains(tooltip,"Link.Bob")
        
        // Get the tooltip again
        MoveCursorToEndOfMarker(file2,"let bo")
        let tooltip = time1 GetQuickInfoAtCursor file2 "Time of second tooltip"
        printf "Second-%s\n" tooltip
        AssertContains(tooltip,"Link.Bob")

    [<Test>]
    member public this.``TauStarter``() =
        let code =
                                    ["#light"
                                     "type (*Scenario01*)Bob() ="
                                     "    let x = 1"
                                     "type (*Scenario021*)Bob ="
                                     "    class"
                                     "    public new() = { }"
                                     "end"
                                     "type (*Scenario022*)Alice ="
                                     "    class"
                                     "    public new() = { }" 
                                     "end"]
        let (_, _, file) = this.CreateSingleFileProject(code)
        TakeCoffeeBreak(this.VS) 

        MoveCursorToEndOfMarker(file,"(*Scenario021*)")
        let tooltip = time1 GetQuickInfoAtCursor file "Time of first tooltip"
        printf "First-%s\n" tooltip
        Assert.IsTrue(tooltip.Contains("Bob ="))
        
        MoveCursorToEndOfMarker(file,"(*Scenario022*)")
        let tooltip = time1 GetQuickInfoAtCursor file "Time of first tooltip"
        printf "First-%s\n" tooltip
        Assert.IsTrue(tooltip.Contains("Alice ="))

    member private this.QuickInfoResolutionTest lines queries =
        let code = [ yield "#light"
                     yield! lines ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        TakeCoffeeBreak(this.VS) 


        // Move along the entire length of the identifier checking that the tooltip text contains something familiar
        for (initial,ident:string,expectedText) in queries do
            for i in 0..ident.Length-1 do
                let marker = initial+ident.[0..i-1]
                MoveCursorToEndOfMarker(file,marker)
                let tooltip = time1 GetQuickInfoAtCursor file "Time for tooltip"
                printf "QuickInfo at marker '%s' is '%s', expect '%s'\n" marker tooltip expectedText
                Assert.IsTrue(tooltip.Contains(expectedText))

    member public this.GetLongPathsTestCases() =
            ["let test0 = System.Console.In"
             "let test0b = System.Collections.Generic.List<int>()"
             "let test0c = System.Collections.Generic.KeyNotFoundException()"
             "type Test0d = System.Collections.Generic.List<int>"
             "type Test0e = System.Collections.Generic.KeyNotFoundException"],
        
              // The quick info specification                                    // Some of the expected quick info text
             [("let test0 = ","System"                                           ,"namespace System"); 
              ("let test0 = System.","Console"                                   ,"Console =");  
              ("let test0 = System.Console.","In"                                ,"System.Console.In");  
              ("let test0 = System.Console.","In"                                ,"TextReader");  
              ("let test0b = ","System"                                          ,"namespace System"); 
              ("let test0b = System.","Collections"                              ,"namespace System.Collections");  
              ("let test0b = System.Collections.","Generic"                      ,"namespace System.Collections.Generic");  
              ("let test0b = System.Collections.Generic.","List"                 ,"List()"); // note resolves to constructor  
              ("let test0c = ","System"                                          ,"namespace System"); 
              ("let test0c = System.","Collections"                              ,"namespace System.Collections");  
              ("let test0c = System.Collections.","Generic"                      ,"namespace System.Collections.Generic");  
              ("let test0c = System.Collections.Generic.","KeyNotFoundException" ,"KeyNotFoundException()");  // note resolves to constructor  
              ("type Test0d = ","System"                                         ,"namespace System"); 
              ("type Test0d = System.","Collections"                             ,"namespace System.Collections");  
              ("type Test0d = System.Collections.","Generic"                     ,"namespace System.Collections.Generic");  
              ("type Test0d = System.Collections.Generic.","List"                ,"Generic.List"); // note resolves to type
              ("type Test0e = ","System"                                         ,"namespace System"); 
              ("type Test0e = System.","Collections"                             ,"namespace System.Collections");  
              ("type Test0e = System.Collections.","Generic"                     ,"namespace System.Collections.Generic");  
              ("type Test0e = System.Collections.Generic.","KeyNotFoundException","Generic.KeyNotFoundException");  // note resolves to type
             ]
        
    [<Test>]
    member public this.``LongPaths``() =
        let text,cases = this.GetLongPathsTestCases()
        this.QuickInfoResolutionTest text cases

    [<Test>]
    member public this.``Global.LongPaths``() =
        let text,cases = this.GetLongPathsTestCases()
        let replace (s:string) = s.Replace("System", "global.System")
        let text = text |> List.map (fun s -> replace s) 
        let cases = 
           cases 
               |> List.filter (fun (a,_,_) -> a.Contains "System") 
               |> List.map (fun (a,b,expectedResult) -> replace a, replace b, expectedResult)

        this.QuickInfoResolutionTest text cases
        
    [<Test>]
    member public this.``TypeAndModuleReferences``() =
        this.QuickInfoResolutionTest 
            ["let test1 = List.length"
             "let test2 = List.Empty"
             "let test3 = (\"1\").Length"
             "let test3b = (id \"1\").Length"]

            // The quick info specification                                    // Some of the expected quick info text
           [("let test1 = ","List"                                             ,"module List");  
            ("let test1 = List.","length"                                      ,"length");  
            ("let test2 = ","List"                                             ,"Collections.List");  
            ("let test2 = List.","Empty"                                       ,"List.Empty");
            ("let test3 = (\"1\").","Length"                                   ,"String.Length");
            ("let test3b = (id \"1\").","Length"                               ,"String.Length") ]
        
    [<Test>]
    member public this.``ModuleNameAndMisc``() =
        this.QuickInfoResolutionTest 
            ["module (*test3q*)MM3 ="
             "    let y = 2"
             "let test4 = lock";
             "let (*test5*) ffff xx = xx + 1" ]

            // The quick info specification                                    // Some of the expected quick info text
           [("module (*test3q*)","MM3"                                         ,"module MM3");
            ("let test4 = ","lock"                                             ,"lock");
            ("let (*test5*) ","ffff"                                           ,"ffff") ]

    [<Test>]
    member public this.``MemberIdentifiers``() =
        this.QuickInfoResolutionTest 
            ["type TestType() ="
             "     member (*test6*) xx.PPPP = 1"
             "     member (*test7*) xx.QQQQ(x) = 3.0"
             "let test8 = (TestType()).PPPP"]

            // The quick info specification                                    // Some of the expected quick info text
           [("member (*test6*) ","xx"                                          ,"TestType");
            ("member (*test6*) xx.","PPPP"                                     ,"PPPP");
            ("member (*test7*) ","xx"                                          ,"TestType");
            ("member (*test7*) xx.","QQQQ"                                     ,"float");
            ("member (*test7*) xx.","QQQQ"                                     ,"float");
            ("let test8 = (TestType()).", "PPPP"                               , "PPPP") ]
        
    [<Test>]
    member public this.``IdentifiersForFields``() =
        this.QuickInfoResolutionTest 
            ["type TestType9 = { XXX : int }"
             "let test11 = { XXX = 1 }"]

            // The quick info specification                                    // Some of the expected quick info text
           [("type TestType9 = { ", "XXX"                                      , "XXX: int");
            ("let test11 = { ", "XXX"                                          , "XXX");] 
        
    [<Test>]
    member public this.``IdentifiersForUnionCases``() =
        this.QuickInfoResolutionTest 
            ["type TestType10 = Case1 | Case2 of int"
             "let test12 = (Case1,Case2(3))"]

            // The quick info specification                                    // Some of the expected quick info text
           [("type TestType10 = ", "Case1"                                     , "union case TestType10.Case1");
            ("type TestType10 = Case1 | ", "Case2"                             , "union case TestType10.Case2");
            ("let test12 = (", "Case1"                                         , "union case TestType10.Case1");
            ("let test12 = (Case1,", "Case2"                                   , "union case TestType10.Case2");] 
        
    [<Test>]
    member public this.``IdentifiersInAttributes``() =
        this.QuickInfoResolutionTest 
            ["[<(*test13*)System.CLSCompliant(true)>]"
             "let test13 = 1"
             "open System"
             "[<(*test14*)CLSCompliant(true)>]"
             "let test14 = 1"]

            // The quick info specification                                    // Some of the expected quick info text
           [("[<(*test13*)", "System"                                          , "namespace System");
            ("[<(*test13*)System.", "CLSCompliant"                             , "CLSCompliantAttribute");
            ("[<(*test14*)", "CLSCompliant"                                    , "CLSCompliantAttribute");] 
        
    [<Test>]
    member public this.``ArgumentAndPropertyNames``() =
        this.QuickInfoResolutionTest 
            ["type R = { mutable AAA : int }"
             "         static member M() = { AAA = 1 }" 
             "let test13 = R.M(AAA=3)"
             "type R2() = "
             "    static member M() = System.Reflection.InterfaceMapping()"
             ""
             "let test14 = R2.M(InterfaceMethods= [| |])"
             ""
             "let test15 = new System.Reflection.AssemblyName(Name=\"Foo\")"
             "let test16 = new System.Reflection.AssemblyName(assemblyName=\"Foo\")"]

            // The quick info specification                                    // Some of the expected quick info text
           [("let test13 = R.M(", "AAA"        , "R.AAA: int");
            ("let test14 = R2.M(", "InterfaceMethods"        , "field System.Reflection.InterfaceMapping.InterfaceMethods");
            ("let test15 = new System.Reflection.AssemblyName(", "Name"        , "property System.Reflection.AssemblyName.Name");
            ("let test16 = new System.Reflection.AssemblyName(", "assemblyName", "argument assemblyName")] 
        
    /// Quickinfo was throwing an exception when the mouse was over the end of a line.
    [<Test>]
    member public this.``AtEndOfLine``() =
        let fileContent = """#light"""
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker fileContent "#light" "Bug:"
        
    [<Test>]
    member public this.``Regression.FieldRepeatedInToolTip.Bug3538``() = 
        this.AssertIdentifierInToolTipExactlyOnce
          """#light
             open System.Runtime.InteropServices
             [<StructLayout(LayoutKind.Explicit)>]
             type A() = 
               [<DefaultValue>]
               val mutable x : int"""
             "LayoutKind.Expl" "Explicit"

    [<Test>]
    member public this.``Regression.FieldRepeatedInToolTip.Bug3818``() = 
        this.AssertIdentifierInToolTipExactlyOnce
          """#light
             [<System.AttributeUsage(System.AttributeTargets.All, Inherited = false)>]
             type A() = 
               do ()"""
             "Inherite" "Inherited"  // Get the tooltip at "Inherite" & Verify that it contains the 'Inherited' fild exactly once
        
    [<Test>]
    member public this.``MethodAndPropTooltip``() = 
        let fileContent = """#light
                             open System
                             do
                               Console.Clear()
                               Console.BackgroundColor |> ignore"""
        this.AssertIdentifierInToolTipExactlyOnce fileContent "Console.Cle" "Clear"
        this.AssertIdentifierInToolTipExactlyOnce fileContent "Console.Back" "BackgroundColor"
        
    [<Test>]
    member public this.``Regression.StaticVsInstance.Bug3626``() = 
        let fileContent = """
                             type Foo() =
                                 member this.Bar () = "hllo"
                                 static member Bar() = 13
                             let z = (*int*) Foo.Bar()
                             let Hoo = new Foo()
                             let y = (*string*) Hoo.Bar() """
        // Get the tooltip at "Foo.Bar("
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"(*int*) Foo.Ba","Foo.Bar")
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"(*int*) Foo.Ba","-> int")
        // Get the tooltip at "Hoo.Bar("
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"(*string*) Hoo.Ba","Foo.Bar")
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"(*string*) Hoo.Ba","-> string")

    [<Test>]
    member public this.``Class.OnlyClassInfo``() = 
        let fileContent = """type TT(x : int, ?y : int) = 
                                 class end"""

        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"type T","type TT")
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker fileContent "type T" "---"

    //KnownFail:  [<Test>]
    member public this.``Async.AsyncToolTips``() = 
        let fileContent = """let a = 
                             async {
                                 let ms = new System.IO.MemoryStream(Array.create 1000 1uy)
                                 let toFill = Array.create 2000 0uy
                                 let! x = ms.AsyncRead(2000)
                                 return x
                             }"""
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"asy","AsyncBuilder")
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker fileContent "asy" "---"

    [<Test>]
    member public this.``Regression.Exceptions.Bug3723``() = 
        let fileContent = """exception E3E of int * int
                             exception E4E of (int * int)
                             exception E5E = E4E"""
        // E3E should be un-parenthesized
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker fileContent "exception E3" "(int * int)"
        // E4E should be parenthesized
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"exception E4","(int * int)")
        // E5E is an alias - should contain name of the aliased exception
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"exception E5","E4E")

    [<Test>]
    member public this.``Regression.Classes.Bug4066``() = 
        let fileContent = """type Foo() as this =
                                 do this |> ignore
                                 member this.Bar() = this"""
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"type Foo() as thi","this")
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker fileContent "type Foo() as thi" "ref"

        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"do thi","this")
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker fileContent "do thi" "ref"

        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"member thi","this")
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker fileContent "member thi" "ref"

        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"Bar() = thi","this")
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker fileContent "Bar() = thi" "ref"

    [<Test>]
    member public this.``Regression.Classes.Bug2362``() = 
        let fileContent = """let append mm nn = fun ac -> mm (nn ac)"""
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"let appen","mm:('a -> 'b) -> nn:('c -> 'a) -> ac:'c -> 'b")
        // check consistency of QuickInfo for 'm' and 'n', which is the main point of this test
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"let append m","'a -> 'b")
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"let append mm n","'c -> 'a")

    [<Test>]
    member public this.``Regression.ModuleAlias.Bug3790a``() = 
        let fileContent = """module ``Some`` = Microsoft.FSharp.Collections.List
                             module None = Microsoft.FSharp.Collections.List"""
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker fileContent "module ``So" "Option"
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker fileContent "module No" "Option"

    [<Test>]
    member public this.``Regression.ModuleAlias.Bug3790b``() = 
        let code =
                                    [ "#light"
                                      "module ``Some`` = Microsoft.FSharp.Collections.List"
                                      "let _ = ``Some``.append [] []" ]
        let (_, _, file) = this.CreateSingleFileProject(code)
        
        // Test quickinfo in place where the declaration is used
        MoveCursorToEndOfMarker(file, "= ``So")
        let tooltip = GetQuickInfoAtCursor file
        AssertNotContains(tooltip, "Option")
            
    [<Test>]
    member public this.``Regression.ActivePatterns.Bug4100a``() = 
        let fileContent = """let (|Lazy|) x = x
                             match 0 with | Lazy y -> ()"""
        // Test quickinfo in place where the declaration is used
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker fileContent "with | Laz" "'?"    // e.g. "Lazy: '?3107 -> '?3107", "Lazy: 'a -> 'a" will be fine

    [<Test>]
    member public this.``Regression.ActivePatterns.Bug4100b``() = 
        let fileContent = """let Some (a:int) = a
                             match None with
                             | Some _ -> ()
                             | _ -> ()
                                      
                             let (|NSome|) (a:int) = a
                             let NSome (a:int) = a.ToString()
                             match 0 with 
                             | NSome _ -> ()"""
        // This shouldn't be the local function - it should find the 'Some' union case
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker fileContent "| Som" "int -> int"
        // This shouldn't find the function returning string but a pattern returning int
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker fileContent "| NSom" "int -> string"

    [<Test>]
    member public this.``Regression.ActivePatterns.Bug4103``() = 
        let fileContent = """let (|Lazy|) x = x
                             match 0 with | Lazy y -> ()"""
        // Test quickinfo in place where the declaration is used
        this.VerifyQuickInfoDoesNotContainAnyAtEndOfMarker fileContent "(|Laz" "Control.Lazy"
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"(|Laz","|Lazy|")
            
    // This test checks that we don't show any tooltips for operators
    // (which is currently not supported, but it used to collide with support for active patterns)
    [<Test>]
    member public this.``Regression.NoTooltipForOperators.Bug4567``() = 
        let fileContent = """let ( |+| ) a b = a + b
                             let n = 1 |+| 2
                             let b = true || false
                             ()"""
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"( |+","")
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"1 |+","")
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"true |","")

    // Check to see that two distinct projects can be present
    [<Test>]
    member public this.``AcrossTwoProjects``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project1 = CreateProject(solution,"testproject1")
        let file1 = AddFileFromText(project1,"File1.fs",
                                    ["#light"
                                     "type (*bob*)Bob1() = "
                                     "    let x = 1"])
        let file1 = OpenFile(project1,"File1.fs")
        let project2 = CreateProject(solution,"testproject2")
        let file2 = AddFileFromText(project2,"File2.fs",
                                    ["#light"
                                     "type (*bob*)Bob2() = "
                                     "    let x = 1"])
        let file2 = OpenFile(project2,"File2.fs")
        
        // Check Bob1
        MoveCursorToEndOfMarker(file1,"type (*bob*)Bob")
        let tooltip = time1 GetQuickInfoAtCursor file1 "Time of file1 tooltip"
        printf "Tooltip for file1:\n%s\n" tooltip
        Assert.IsTrue(tooltip.Contains("Bob1 ="))
        
        // Check Bob2
        MoveCursorToEndOfMarker(file2,"type (*bob*)Bob")
        let tooltip = time1 GetQuickInfoAtCursor file2 "Time of file2 tooltip"
        printf "Tooltip for file2:\n%s\n" tooltip
        Assert.IsTrue(tooltip.Contains("Bob2 ="))
        
    // In this bug, relative paths with .. in them weren't working.
    [<Test>]
    member public this.``BugInRelativePaths``() =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let file1 = AddFileFromText(project,"File1.fs",
                                    ["#light"
                                     "type Bob() = "
                                     "    let x = 1"])
        let file2 = AddFileFromText(project,"..\\File2.fs",
                                    ["#light"
                                     "let bob = new File1.Bob()"])
        let file1 = OpenFile(project,"File1.fs")
        let file2 = OpenFile(project,"..\\File2.fs")
        
        // Get the tooltip at type Bob     
        MoveCursorToEndOfMarker(file2,"let bo")
        let tooltip = time1 GetQuickInfoAtCursor file2 "Time of first tooltip"
        printf "First-%s\n" tooltip
        AssertContains(tooltip,"File1.Bob")
        
        // Get the tooltip again
        MoveCursorToEndOfMarker(file2,"let bo")
        let tooltip = time1 GetQuickInfoAtCursor file2 "Time of second tooltip"
        printf "Second-%s\n" tooltip
        AssertContains(tooltip,"File1.Bob")

    // QuickInfo over a type that references types in an unreferenced assembly works.        
    [<Test>]
    member public this.``MissingDependencyReferences.QuickInfo.Bug5409``() =     
        let code = 
                                    ["#light"
                                     "let myForm = new System.Windows.Forms.Form()"
                                    ]
        let (_, _, file) = this.CreateSingleFileProject(code, references = ["System.Windows.Forms"])
        MoveCursorToEndOfMarker(file,"myFo")
        let tooltip = time1 GetQuickInfoAtCursor file "Time of first tooltip"
        printf "First-%s\n" tooltip
//        ShowErrors(project)
        AssertContains(tooltip,"Form")

    /// In this bug, the EOF token was reached before the parser could close the (, with, and let
    /// The fix--at the point in time it was fixed--was to modify the parser to send a limitted number
    /// of additional EOF tokens to allow the recovery code to proceed up the change of productions
    /// in the grammar.
    [<Test>]
    member public this.``Regression.Bug1605``() = 
        let fileContent = """let rec f l =
                                 match l with
                                 | [] -> string.Format(
                                 | x::xs -> "hello" """
        // This string doesn't matter except that it should prove there is some datatip present.
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"| [] -> str","string")
        
    [<Test>]
    member public this.``Regression.Bug4642``() =   
        let fileContent = """ "AA".Chars """
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"\"AA\".Ch","int -> char")

    /// Complete a member completion and confirm that its data tip contains the fragments
    /// in rhsContainsOrder
    member public this.AssertMemberDataTipContainsInOrder(code : list<string>,marker,completionName,rhsContainsOrder) =
        let code = code |> Seq.collect (fun s -> s.Split [|'\r'; '\n'|]) |> List.ofSeq
        let (_, project, file) = this.CreateSingleFileProject(code, fileKind = SourceFileKind.FSX)
        TakeCoffeeBreak(this.VS) (* why needed? *)       
        MoveCursorToEndOfMarker(file,marker)
        let completions = CtrlSpaceCompleteAtCursor file
        match completions |> Array.tryFind (fun (CompletionItem(name, _, _, _, _)) -> name = completionName) with
        | Some(CompletionItem(_, _, _, descrFunc, _)) ->
            let descr = descrFunc()
            AssertContainsInOrder(descr,rhsContainsOrder)
        | None -> 
            Console.WriteLine("Could not find completion name '{0}'", completionName)
            ShowErrors(project)
            Assert.Fail()

    [<Test>]
    //``CompletiongListItem.DocCommentsOnMembers`` and with //Regression 5856
    member public this.``Regression.MemberDefinition.DocComments.Bug5856_1``() =
        this.AssertMemberDataTipContainsInOrder
            ((*code *)
              [
               "type MyType = "
               "    /// Hello"
               "    static member Overload() = 0"
               "    /// Hello2"
               "    static member Overload(x:int) = 0"
               "    /// Hello3"
               "    static member NonOverload() = 0"
               "MyType."
               ] ,
             (* marker *)
             "MyType.",
             (* completed item *)             
             "Overload", 
             (* expect to see in order... *)
             [
              "static member MyType.Overload : unit -> int";
              "static member MyType.Overload : x:int -> int";
              "Hello"
             ]
            )

    [<Test>]
    member public this.``Regression.MemberDefinition.DocComments.Bug5856_2``() =
        this.AssertMemberDataTipContainsInOrder
            ((*code *)
              [
               "module Outer ="
               "    /// Comment"
               "    module Inner ="
               "        let x = 1"
               "let x() = "
               "    Outer."
               ] ,
             (* marker *)
             "Outer.",
             (* completed item *)             
             "Inner", 
             (* expect to see in order... *)
             [
              "module Inner";
              "from"; "Outer";
              "Comment"
             ]
            )

    [<Test>]
    member public this.``Regression.MemberDefinition.DocComments.Bug5856_3``() =
        this.AssertMemberDataTipContainsInOrder
            ((*code *)
              [
               "module Module ="
               "    /// Union comment"
               "    type Union ="
               "        /// Case comment"
               "        | Case of int"
               "let x() = "
               "    Module."
               ] ,
             (* marker *)
             "Module.",
             (* completed item *)             
             "Case", 
             (* expect to see in order... *)
             [
              "union case Module.Union.Case: int -> Module.Union";
              "Case comment";
             ]
            )

    [<Test>]
    member public this.``Regression.MemberDefinition.DocComments.Bug5856_4``() =
        this.AssertMemberDataTipContainsInOrder
            ((*code *)
              [
               "module Module ="
               "    /// Union comment"
               "    type Union ="
               "        /// Case comment"
               "        | Case of int"
               "let x() = "
               "    Module."
               ] ,
             (* marker *)
             "Module.",
             (* completed item *)             
             "Union", 
             (* expect to see in order... *)
             [
              "type Union = | Case of int";
              //"Full name:"; "Module.Union";
              "Union comment";
             ]
            )

    [<Test>]
    member public this.``Regression.MemberDefinition.DocComments.Bug5856_5``() =
        this.AssertMemberDataTipContainsInOrder
            ((*code *)
              [
               "module Module ="
               "    /// Pattern comment"
               "    let (|Pattern|) = 0"
               "let x() = "
               "    Module."
               ] ,
             (* marker *)
             "Module.",
             (* completed item *)             
             "Pattern", 
             (* expect to see in order... *)
             [
              "active recognizer Pattern: int";
              //"Full name:"; "Module";  "|Pattern|";
              "Pattern comment";
             ]
            )

    [<Test>]
    member public this.``Regression.MemberDefinition.DocComments.Bug5856_6``() =
        this.AssertMemberDataTipContainsInOrder
            ((*code *)
              [
               "module Module ="
               "    /// A comment"
               "    exception MyException of int"
               "let x() = "
               "    Module."
               ] ,
             (* marker *)
             "Module.",
             (* completed item *)             
             "MyException", 
             (* expect to see in order... *)
             [
              "exception MyException of int";
              //"Full name:"; "Module";  "MyException";
              "A comment";
             ]
            )

    [<Test>]
    member public this.``Regression.MemberDefinition.DocComments.Bug5856_7``() =
        this.AssertMemberDataTipContainsInOrder
            ((*code *)
              [
               "type Record = {"
               "    /// A comment"
               "    field : int"
               "    }"
               "let record = {field = 1}"
               "let x() ="
               "    record."
               ] ,
             (* marker *)
             "record.",
             (* completed item *)             
             "field", 
             (* expect to see in order... *)
             [
              "Record.field: int";
              "A comment";
             ]
            )

    [<Test>]
    member public this.``Regression.MemberDefinition.DocComments.Bug5856_8``() =
        this.AssertMemberDataTipContainsInOrder
            ((*code *)
              [
               "type Foo ="
               "    /// A comment"
               "    static member Property"
               "        with get() = \"\""
               "let x() = "
               "    Foo."
               ] ,
             (* marker *)
             "Foo.",
             (* completed item *)             
             "Property", 
             (* expect to see in order... *)
             [
              "property Foo.Property: string";
              "A comment";
             ]
            )

    [<Test>]
    member public this.``Regression.MemberDefinition.DocComments.Bug5856_9``() =
        this.AssertMemberDataTipContainsInOrder
            ((*code *)
              [
               "module Module ="
               "    /// A comment"
               "    type Class = class end"
               "let x() = "
               "    Module."
               ] ,
             (* marker *)
             "Module.",
             (* completed item *)             
             "Class", 
             (* expect to see in order... *)
             [
              "type Class";
              //"Full name:"; "Module";  "Class";
              "A comment";
             ]
            )

    [<Test>]
    member public this.``Regression.MemberDefinition.DocComments.Bug5856_10``() =
        this.AssertMemberDataTipContainsInOrder
            ((*code *)
              [
               "System.String."
               ] ,
             (* marker *)
             "String.",
             (* completed item *)             
             "Format", 
             (* expect to see in order... *)
             [
              "System.String.Format(";
              "[Filename:"; "mscorlib.dll]";
              "[Signature:M:System.String.Format(System.String,System.Object[])]";
             ]
            )

    [<Test>]
    member public this.``Regression.MemberDefinition.DocComments.Bug5856_13``() =
        this.AssertMemberDataTipContainsInOrder
            ((*code *)
              [
               "System.Collections.Generic.Dictionary."
               ] ,
             (* marker *)
             "Dictionary.",
             (* completed item *)             
             "KeyCollection", 
             (* expect to see in order... *)
             [
              "type KeyCollection<";
              "member CopyTo"; 
              """<summary>Represents the collection of keys in a <see cref="T:System.Collections.Generic.Dictionary`2" />. This class cannot be inherited.</summary>"""
             ]
            )   

    [<Test>]
    member public this.``Regression.MemberDefinition.DocComments.Bug5856_14``() =
        this.AssertMemberDataTipContainsInOrder
            ((*code *)
              [
               "System."
               ] ,
             (* marker *)
             "System.",
             (* completed item *)             
             "ArgumentException", 
             (* expect to see in order... *)
             [
              "type ArgumentException";
              "member Message"; 
              "<summary>The exception that is thrown when one of the arguments provided to a method is not valid.</summary"
             ]
            )    

    [<Test>]
    member public this.``Regression.MemberDefinition.DocComments.Bug5856_15``() =
        this.AssertMemberDataTipContainsInOrder
            ((*code *)
              [
               "System.AppDomain."
               ] ,
             (* marker *)
             "AppDomain.",
             (* completed item *)             
             "CurrentDomain", 
             (* expect to see in order... *)
             [
              "property System.AppDomain.CurrentDomain: System.AppDomain";
              """<summary>Gets the current application domain for the current <see cref="T:System.Threading.Thread" />.</summary>"""
             ]
            ) 


    [<Test>]
    member public this.``Regression.ExtensionMethods.DocComments.Bug6028``() =
        this.AssertMemberDataTipContainsInOrder
            ((*code *)
              [
               @"open System.Linq
let rec query:System.Linq.IQueryable<_> = null
query."
               ] ,
             (* marker *)
             "query.",
             (* completed item *)             
             "All", 
             (* expect to see in order... *)
             [
              "IQueryable.All";
              "[Filename"; "System.Core.dll]";
              "[Signature:M:System.Linq.Enumerable.All``1"
             ]
            ) 
            
    [<Test>]
    member public this.``Regression.OnMscorlibMethodInScript.Bug6489``() =
        this.AssertMemberDataTipContainsInOrder
            ((*code *)
              [
               "let Func() = 
    let actual = [| |]
    actual."
               ] ,
             (* marker *)
             "actual.",
             (* completed item *)             
             "CopyTo", 
             (* expect to see in order... *)
             [
              "[Filename"; "Reference Assemblies\Microsoft\Framework\.NETFramework"; "mscorlib.dll]";
              "[Signature:M:System.Array.CopyTo("
             ]
            ) 
              
              
    /// BUG: intelisense on "self" parameter in implicit ctor classes is wrong
    [<Test>]
    member public this.``Regression.CompListItemInfo.Bug5694``() =
        this.AssertMemberDataTipContainsInOrder
            ((*code *)
              [
                 "type Form2() as self ="
                 "    inherit System.Windows.Forms.Form()"
                 "    let f() = self."
               ] ,
             (* marker *)
             "self.",
             (* completed item *)             
             "AcceptButton", 
             (* expect to see in order... *)
             [
              "<summary>Gets or sets the button on the form that is clicked when the user presses the ENTER key.</summary>"
             ]
            )


    /// Bug 4592: Check that ctors are displayed from C# classes, i.e. the "new" lines below.
    [<Test>]
    member public this.``Regression.Class.Printing.CSharp.Classes.Only.Bug4592``() =
        this.AssertMemberDataTipContainsInOrder
            ((*code *)
              ["#light";
              "System.Random"] ,
             (* marker *)
             "System.Random",
             (* completed item *)             
             "Random", 
             (* expect to see in order... *)
             ["type Random =";
              "  new : unit -> unit + 1 overload"
              "  member Next : unit -> int + 2 overloads";  
              "  member NextBytes : buffer: byte [] -> unit";
              "  member NextDouble : unit -> float";
              "  member Sample : unit -> float";]
            )

    [<Test>]
    member public this.``GenericDotNetMethodShowsComment``() =
        this.AssertMemberDataTipContainsInOrder
            ((*code *)
             ["System.Linq.ParallelEnumerable."] ,
             (* marker *)
             "ParallelEnumerable.",
             (* completed item *)             
             "ElementAt", 
             (* expect to see in order... *)
             [
              "Signature:M:System.Linq.ParallelEnumerable.ElementAt``1(System.Linq.ParallelQuery{``0},System.Int32"
             ]
            )

    /// Bug 4624: Check the order in which members are printed, C# classes
    [<Test>]
    member public this.``Regression.Class.Printing.CSharp.Classes.Bug4624``() =
        //let f (x:System.Security.Policy.CodeConnectAccess) = x.
        this.AssertMemberDataTipContainsInOrder
            ((*code *)
              ["#light";
               "System.Security.Policy.CodeConnectAccess"],
             (* marker *)
             "System.Security.Policy.CodeConnectAccess",
             (* completed item *)             
             "CodeConnectAccess", 
             (* expect to see in order... *)
             // Pre fix output is mixed up
             [ "type CodeConnectAccess =";
               "  new : allowScheme: string * allowPort: int -> unit + 2 overloads";
               "  member Equals : o: obj -> bool";
               "  member GetHashCode : unit -> int";
               "  static member CreateAnySchemeAccess : allowPort: int -> CodeConnectAccess";
               "  static member CreateOriginSchemeAccess : allowPort: int -> CodeConnectAccess";
               "  static member IsValidScheme : scheme: string -> bool";
               "  static val AnyPort : int";
               "  static val AnyScheme : string";
               "  static val DefaultPort : int";
               "  static val NoPort : int";
               "  ...";
             ])

    /// Bug 4624: Check the order in which members are printed, F# classes
    [<Test>]
    member public this.``Regression.Class.Printing.FSharp.Classes.Bug4624``() =
        this.AssertMemberDataTipContainsInOrder
            ((*code *)
              ["#light";               
               "type F1() = ";
               "    class        ";
               "        inherit System.Windows.Forms.Form()";
               "        abstract AAA : int  with get";
               "        abstract ZZZ : int  with get";
               "        abstract AAA : bool with set";
               "        val x : F1";
               "        static val x : F1";
               "        static member A() = 12";
               "        member this.B() = 12";
               "        static member C() = 12";
               "        member this.D() = 12";
               "        member this.D with get() = 12 and set(12) = ()";
               "        member this.D(x:int,y:int) = 12";
               "        member this.D(x:int) = 12";
               "        member this.D x y z = [1;x;y;z]";
               "        override this.ToString() = \"\"";
               "        interface System.IDisposable with";
               "            override this.Dispose() = ()        ";
               "        end";
               "    end";
               "type A1 = F1"],
             (* marker *)
             "type A1 = F1",
             (* completed item *)             
             "F1", 
             (* expect to see in order... *)
             // Pre fix output is mixed up
             [ "type F1 =";
               "  inherit Form";
               "  interface IDisposable";
               "  new : unit -> F1";
               "  val x: F1"
               "  member B : unit -> int";
               "  override ToString : unit -> string";
               "  static member A : unit -> int";
               "  static member C : unit -> int";
               "  abstract member AAA : int";
               "  member D : int";
               "  ...";
             ])

(*------------------------------------------IDE automation starts here -------------------------------------------------*)
    [<Test>]
    member public this.``Automation.Regression.AccessibilityOnTypeMembers.Bug4168``() =
        let fileContent = """module Test
                             type internal Foo2(*Marker*) () = 
                                  member public this.Prop1 = 12
                                  member internal this.Prop2 = 12
                                  member private this.Prop3 = 12
                                  public new(x:int) = new Foo2()
                                  internal new(x:int,y:int) = new Foo2()
                                  private new(x:int,y:int,z:int) = new Foo2()"""
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker*)", "type internal Foo2")

    [<Test>]
    member public this.``Automation.Regression.AccessorsAndMutators.Bug4276``() =
        let fileContent = """type TestType1(*Marker1*)( x : int , y : int ) =  
                                 let mutable x = x
                                 let mutable y = y
     
                                 // Property with getter and setter
                                 member this.X with get () = x 
                                               and  set x' = x <- x'

                                 // Property with setter only
                                 member this.Y with set y' = y <- y'

                                 // Property with getter only
                                 member this.Length with get () = sqrt(float (x * x + y * y))

                                 member this.Item with get (i : int) = match i with | 0 -> x | 1 -> y | _ -> failwith "Incorrect index"

                             let point = TestType1(10,10)

                             point.X <- 3
                             point.Y <- 4

                             let x = point.[0]
                             let y = point.[1]

                             let bitArray = new System.Collections.BitArray(*Marker2*)(1)

                             point.Length |> ignore"""
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker1*)", "type TestType1")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker1*)", "member Length : float")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker1*)", "member Item")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker1*)", "member X : int")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker1*)", "member Y : int")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker2*)", "type BitArray")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker2*)", "member Not : unit -> BitArray")
        this.VerifyQuickInfoDoesNotContainAnyAtStartOfMarker fileContent "(*Marker2*)" "get_Length"
        this.VerifyQuickInfoDoesNotContainAnyAtStartOfMarker fileContent "(*Marker2*)" "set_Length"

    [<Test>]
    [<Ignore("DocComment issue")>]
    member public this.``Automation.AutoOpenMyNamespace``() =
        let fileContent ="""namespace System.Numerics
                            type t = BigInteger(*Marker1*)"""
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "r(*Marker1*)", "type BigInteger")

    [<Test>]
    member public this.``Automation.Regression.BeforeAndAfterIdentifier.Bug4371``() =
        let fileContent = """module Test
                             let f arg1 (arg2, arg3, arg4) arg5 = 42
                             let goo a = f(*Marker1*) 12 a

                             type printer = System.Console
                             let z = (*Marker3*)printer.BufferWidth(*Marker2*)"""
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker1*)", "Full name: Test.f")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker1*)", "val f")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker2*)", "property System.Console.BufferWidth: int")
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"(*Marker3*)","Full name: Test.printer")

    [<Test>]
    member public this.``Automation.Regression.ConstrutorWithSameNameAsType.Bug2739``() =
        let fileContent = """namespace AA
                             module AA = 
                                 type AA = | AA(*Marker1*) = 1
                                           | BB = 2
                             type BB = { BB(*Marker2*) : string; }"""
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker1*)", "AA.AA: AA")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker2*)", "BB.BB: string")

    [<Test>]
    member public this.``Automation.Regression.EventImplementation.Bug5471``() =
        let fileContent = """namespace regressiontest
                             open System
                             open System.Windows
                             open System.Windows.Input

                             type CommandReference() =
                                 inherit Freezable()
 
                                 static let commandProperty =
                                     DependencyProperty.Register(
                                         "Command", 
                                         typeof<ICommand>, 
                                         typeof<CommandReference>, 
                                         PropertyMetadata(PropertyChangedCallback(fun o e -> CommandReference.OnCommandChanged(o, e))))
 
                                 let evt = Event<EventHandler, EventArgs>()
 
                                 member this.Command
                                     with get () = this.GetValue(commandProperty) :?> ICommand
                                     and  set v  = this.SetValue(commandProperty, (v: ICommand) )
 
                                 interface ICommand with
 
                                     member this.CanExecute(parameter) =
                                         if this.Command <> null then
                                              this.Command.CanExecute(parameter)
                                         else false
 
                                     member this.Execute(parameter) =
                                         this.Command.Execute(parameter)
 
                                     [<CLIEvent>]
                                     member x.CanExecuteChanged(*Marker*) = evt.Publish
 
                                 static member OnCommandChanged(d: DependencyObject, e: DependencyPropertyChangedEventArgs) =
                                         let commandReference = (d :?> CommandReference) :> ICommand
                                         let oldCommand = e.OldValue :?> ICommand
                                         let newCommand = e.NewValue :?> ICommand
                                         if oldCommand <> null then
                                             // Error: This expression has type IEvent<EventHandler,EventArgs> but is here used with type  EventHandler 
                                             oldCommand.CanExecuteChanged.RemoveHandler(commandReference.CanExecuteChanged)
                                         if newCommand <> null then 
                                             // Error: This expression has type IEvent<EventHandler,EventArgs> but is here used with type  EventHandler 
                                             newCommand.CanExecuteChanged.AddHandler(commandReference.CanExecuteChanged)
  
                                 override this.CreateInstanceCore() =
                                     raise (NotImplementedException())"""
        let (_, _, file) = this.CreateSingleFileProject(fileContent, references = ["PresentationCore"; "WindowsBase"])
        MoveCursorToStartOfMarker(file, "(*Marker*)")
        let tooltip = time1 GetQuickInfoAtCursor file "Time of first tooltip"
        AssertContains(tooltip, "override CommandReference.CanExecuteChanged : IEvent<EventHandler,EventArgs>") 
        AssertContains(tooltip, "regressiontest.CommandReference.CanExecuteChanged") 

    [<Test>]
    member public this.``Automation.ExtensionMethod``() =
        let fileContent ="""namespace TestQuickinfo

                            module BCLExtensions =
                                type System.Random with
                                    /// BCL class Extension method
                                    member this.NextDice()  = this.Next() + 1
                                    /// new BCL class Extension method with overload
                                    member this.NextDice(a : bool)  = this.Next() + 1
                                    /// existing BCL class Extension method with overload
                                    member this.Next(a : bool)  = this.Next() + 1
                                    /// BCL class Extension property
                                    member this.DiceValue with get() = 6
        
                                type System.ConsoleKeyInfo with
                                    /// BCL struct extension method
                                    member this.ExtentionMethod()  =  100
                                    /// BCL struct extension property
                                    member this.ExtentionProperty with get() = "Foo"        

                            module OwnCode =
                                /// fs class
                                type FSClass() =
                                    class
                                        /// fs class method original
                                        member this.Method(a:string) = ""
                                        /// fs class property original
                                        member this.Prop with get(a:string) = ""
                                    end
    
                                /// fs struct
                                type FSStruct(x:int) =
                                    struct
                                    end
                                    
                            module OwnCodeExtensions =
                                type OwnCode.FSClass with
                                    /// fs class extension method
                                    member this.ExtentionMethod()  =  100
        
                                    /// fs class extension property
                                    member this.ExtentionProperty with get() = "Foo"
                                    
                                    /// fs class method extension overload
                                    member this.Method(a:int)  =  ""
                                    
                                    /// fs class property extension overload
                                    member this.Prop with get(a:int)  =  ""
        
                                type OwnCode.FSStruct with
                                    /// fs struct extension method
                                    member this.ExtentionMethod()  =  100
        
                                    /// fs struct extension property
                                    member this.ExtentionProperty with get() = "Foo"      

                            module BCLClass = 
                                open BCLExtensions
                                let rnd = new System.Random()
                                rnd.DiceValue(*Marker11*) |>ignore
                                rnd.NextDice(*Marker12*)() |>ignore
                                rnd.NextDice(*Marker13*)(true) |>ignore
                                rnd.Next(*Marker14*)(true) |>ignore
                                
    
                            module BCLStruct = 
                                open BCLExtensions
                                let cki = new System.ConsoleKeyInfo()
                                cki.ExtentionMethod(*Marker21*) |>ignore
                                cki.ExtentionProperty(*Marker22*) |>ignore
    
                            module OwnClass = 
                                open OwnCode
                                open OwnCodeExtensions
                                let rnd = new FSClass()
                                rnd.ExtentionMethod(*Marker31*) |>ignore
                                rnd.ExtentionProperty(*Marker32*) |>ignore
                                rnd.Method(*Marker33*)("") |>ignore
                                rnd.Method(*Marker34*)(6) |>ignore
                                rnd.Prop(*Marker35*)("") |>ignore
                                rnd.Prop(*Marker36*)(6) |>ignore
    
                            module OwnStruct = 
                                open OwnCode
                                open OwnCodeExtensions
                                let cki = new FSStruct(100)
                                cki.ExtentionMethod(*Marker41*) |>ignore
                                cki.ExtentionProperty(*Marker42*) |>ignore"""
                                
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker11*)", "property System.Random.DiceValue: int")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker11*)", "BCL class Extension property")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker12*)", "member System.Random.NextDice : unit -> int")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker12*)", "BCL class Extension method")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker13*)", "member System.Random.NextDice : a:bool -> int")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker13*)", "new BCL class Extension method with overload")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker14*)", "member System.Random.Next : a:bool -> int")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker14*)", "existing BCL class Extension method with overload")        
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker21*)", "member System.ConsoleKeyInfo.ExtentionMethod : unit -> int")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker21*)", "BCL struct extension method")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker22*)", "System.ConsoleKeyInfo.ExtentionProperty: string")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker22*)", "BCL struct extension property")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker31*)", "member FSClass.ExtentionMethod : unit -> int")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker31*)", "fs class extension method")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker32*)", "FSClass.ExtentionProperty: string")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker32*)", "fs class extension property")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker33*)", "member FSClass.Method : a:string -> string")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker33*)", "fs class method original")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker34*)", "member FSClass.Method : a:int -> string")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker34*)", "fs class method extension overload")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker35*)", "property FSClass.Prop: string -> string")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker35*)", "fs class property original")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker36*)", "property FSClass.Prop: int -> string")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker36*)", "fs class property extension overload")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker41*)", "member FSStruct.ExtentionMethod : unit -> int")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker41*)", "fs struct extension method")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker42*)", "FSStruct.ExtentionProperty: string")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker42*)", "fs struct extension property")

    [<Test>]
    member public this.``Automation.Regression.GenericFunction.Bug2868``() =
        let fileContent ="""module Test
                            // Hovering over a generic function (generic argument decorated with [<Measure>] attribute yields a bad tooltip
                            let F (f :_ -> float<_>) = fun x -> f (x+1.0)
                            let rec Gen<[<Measure>] 'u> (f:float<'u> -> float<'u>) = 
                              Gen(*Marker*)(F f)"""
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker*)", "val Gen : f:(float -> float) -> 'a")
        this.VerifyQuickInfoDoesNotContainAnyAtStartOfMarker fileContent "(*Marker*)" "Exception"
        this.VerifyQuickInfoDoesNotContainAnyAtStartOfMarker fileContent "(*Marker*)" "thrown"

    [<Test>]
    [<Ignore("DocComment issue")>]
    member public this.``Automation.IdentifierHaveDiffMeanings``() =
        let fileContent ="""namespace NS
                            module float(*Marker1_1*) =

                                let GenerateTuple =  fun x ->   let tuple = (x,x.ToString(),(float(*Marker1_2*))x, ( fun y -> (y.ToString(),y+1)) )
                                                                tuple

                                let MySeq : (*Marker2_1*)seq<float(*Marker1_3*)> = 
                                    seq(*Marker2_2*)    {
                
                                                           for i in 1..9 do
                                     
                                                                let myTuple = GenerateTuple i
                                                                let fieldInt,fieldString,fieldFloat,_ = myTuple
                                                                yield fieldFloat
                                                        }
            
                                let MySet : (*Marker3_1*)Set<float> = 
                                    MySeq 
                                    |> Array.ofSeq
                                    |> List.ofArray
                                    |> Set(*Marker3_2*).ofList

                                let int(*Marker4_1*) : int(*Marker4_2*) = 1

                                type int(*Marker4_3*)() = 
                                    member this.M = 1

                                type T(*Marker5_1*)() =
                                    [<DefaultValueAttribute>]
                                    val mutable T : T

                                let T = new T()
                                let t = T.T.T.T(*Marker5_2*);
    
                                type ValType() = 
                                    member this.Value with get(*Marker6_1*) () = 10
                                                       and set(*Marker6_2*) x  = x + 1 |> ignore"""
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker1_1*)", "module float")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker1_2*)", "val float : 'T -> float (requires member op_Explicit)")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker1_2*)", "Full name: Microsoft.FSharp.Core.Operators.float")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker1_3*)", "type float = System.Double")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker1_3*)", "Full name: Microsoft.FSharp.Core.float")
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"(*Marker2_1*)","type seq<'T> = System.Collections.Generic.IEnumerable<'T>")
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"(*Marker2_1*)","Full name: Microsoft.FSharp.Collections.seq<_>")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker2_2*)", "val seq : seq<'T> -> seq<'T>")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker2_2*)", "Full name: Microsoft.FSharp.Core.Operators.seq")
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"(*Marker3_1*)","type Set<'T (requires comparison)> =")
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"(*Marker3_1*)","Full name: Microsoft.FSharp.Collections.Set")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker3_2*)", "module Set")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker3_2*)", "Functional programming operators related to the Set<_> type")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker4_1*)", "val int : int")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker4_1*)", "Full name: NS.float.int")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker4_2*)", "type int = int32")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker4_2*)", "Full name: Microsoft.FSharp.Core.int")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker4_3*)", "type int =")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker4_3*)", "member M : int")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker5_1*)", "type T =")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker5_1*)", "new : unit -> T")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker5_1*)", "val mutable T: T")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker5_2*)", "T.T: T")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker6_1*)", "member ValType.Value : int")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker6_2*)", "member ValType.Value : int with set")
        this.VerifyQuickInfoDoesNotContainAnyAtStartOfMarker fileContent "(*Marker6_2*)" "Microsoft.FSharp.Core.ExtraTopLevelOperators.set"

    [<Test>]
    member public this.``Automation.Regression.ModuleIdentifier.Bug2937``() =
        let fileContent ="""module XXX(*Marker*)
                            type t = C3"""
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker*)", "module XXX")
        this.VerifyQuickInfoDoesNotContainAnyAtStartOfMarker fileContent "(*Marker*)" "\n"

    [<Test>]
    member public this.``Automation.Regression.NamesArgument.Bug3818``() =
        let fileContent ="""module m 
                            [<System.AttributeUsage(System.AttributeTargets.All, AllowMultiple(*Marker1*) = true)>]
                            type T = class
                                     end"""
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker1*)", "property System.AttributeUsageAttribute.AllowMultiple: bool")

    [<Test>]
    [<Ignore("DocComment issue")>]
    member public this.``Automation.OnUnitsOfMeasure``() =
        let fileContent ="""namespace TestQuickinfo

                            module TestCase1 =
                                [<Measure>] 
                                /// this type represents kilogram in UOM
                                type kg
                                let mass(*Marker11*) = 2.0<kg(*Marker12*)>

                            module TestCase2 =
                                [<Measure>] 
                                /// use Set as the type name of UoM
                                type Set

                                let v1 = [1.0<Set> .. 2.0<Set> .. 5.0<Set>] |> Seq.item 1

                                (if v1 = 3.0<Set> then 0 else 1) |> ignore
    
                                let twoSets = 2.0<Set>
    
                                [1.0<Set(*Marker21*)>]
                                |> Set.ofList
                                |> Set(*Marker22*).isEmpty 
                                |> ignore"""
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker11*)", "val mass : float<kg>")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker11*)", "Full name: TestQuickinfo.TestCase1.mass")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker11*)", "inherits: System.ValueType")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker12*)", "[<Measure>]")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker12*)", "type kg")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker12*)", "this type represents kilogram in UOM")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker12*)", "Full name: TestQuickinfo.TestCase1.kg")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker21*)", "[<Measure>]")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker21*)", "type Set")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker21*)", "use Set as the type name of UoM")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker21*)", "Full name: TestQuickinfo.TestCase2.Set")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker22*)", "module Set")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker22*)", "from Microsoft.FSharp.Collections")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker22*)", "Functional programming operators related to the Set<_> type.")

    [<Test>]
    member public this.``Automation.OverRiddenMembers``() =
        let fileContent ="""namespace QuickinfoGeneric

                            module FSharpOwnCode =
                                [<AbstractClass>]
                                type TextOutputSink() =
                                    abstract WriteChar : char -> unit
                                    abstract WriteString : string -> unit
                                    default x.WriteString(s) = s |> String.iter x.WriteChar 

                                type ByteOutputSink() =
                                    inherit TextOutputSink()    
                                    default sink.WriteChar(c) = System.Console.Write(c)
                                    override sink.WriteString(s) = System.Console.Write(s)   
        
                                let sink = new ByteOutputSink()
                                sink.WriteChar(*Marker11*)('c') 
                                sink.WriteString(*Marker12*)("Hello World!")"""
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker11*)", "override ByteOutputSink.WriteChar : c:char -> unit")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker12*)", "override ByteOutputSink.WriteString : s:string -> unit")

    [<Test>]
    member public this.``Automation.Regression.QuotedIdentifier.Bug3790``() =
        let fileContent ="""module Test
                            module ``Some``(*Marker1*) = Microsoft.FSharp.Collections.List
                            let _ = ``Some``(*Marker2*).append [] [] """
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "``(*Marker1*)", "module List")
        this.VerifyQuickInfoDoesNotContainAnyAtStartOfMarker fileContent "``(*Marker1*)" "Option.Some"
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "``(*Marker2*)", "module List")
        this.VerifyQuickInfoDoesNotContainAnyAtStartOfMarker fileContent "``(*Marker2*)" "Option.Some"

    [<Test>]
    [<Ignore("Can not get QuickInfo tips")>]
    member public this.``Automation.Setter``() =
        let fileContent ="""type T() =
                                 member this.XX
                                   with set ((a:int), (b:int), (c:int)) = ()

                            (new T()).XX(*Marker1*) <- (1,2,3)
                            //===================================================
                            // More cases:
                            //===================================================
                            type IFoo = interface
                                abstract foo : int -> int
                                end    
                            let i : IFoo = Unchecked.defaultof<IFoo>
                            i.foo(*Marker2*) |> ignore
                            //===================================================
                            type Rec =  { bar:int->int->int }
                            let r = {bar = fun x y -> x + y }

                            r.bar(*Marker3*) 1 2 |>ignore
                            //===================================================
                            type M() = 
                                member this.baz x y = x + y
                            let m = new M()
                            m.baz(*Marker3*) 1 2 |>ignore
                            //===================================================
                            type T2() =
                                 member this.Foo(a,b) = ""
                            let t = new T2()
                            t.Foo(*Marker4*)(1,2) |>ignore
                            //===================================================
                            let foo (x:int) (y:int) : int = 1
                            foo(*Marker5*) 2 3 |> ignore"""
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker1*)", "T.XX: int * int * int")
        this.VerifyQuickInfoDoesNotContainAnyAtStartOfMarker fileContent "(*Marker1*)" "->"
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker2*)", "IFoo.foo : int -> int")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker3*)", "Rec.bar: int -> int -> int")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker4*)", "T2.Foo : a:'a * b:'b -> string")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker5*)", "val foo : int -> int -> int")

    [<Test>]
    member public this.``Automation.Regression.TupleException.Bug3723``() =
        let fileContent ="""namespace TestQuickinfo
                            exception E3(*Marker1*) of int * int
                            exception E4(*Marker2*) of (int * int)
                            exception E5(*Marker3*) = E4"""
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker1*)", "exception E3 of int * int")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker1*)", "Full name: TestQuickinfo.E3")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker2*)", "exception E4 of (int * int)")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker2*)", "Full name: TestQuickinfo.E4")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker3*)", "exception E5 = E4")

    [<Test>]
    member public this.``Automation.TypeAbbreviations``() =
        let fileContent ="""namespace NS
                            module TypeAbbreviation =
    
                                type MyInt(*Marker1_1*) = int
    
                                type PairOfFloat(*Marker2_1*) = float * float
    
    
                                type AbAttrName(*Marker5_1*) = AbstractClassAttribute
      
    
                                type IA(*Marker3_1*) = 
                                    abstract AbstractMember : int -> int
        
                                [<AbAttrName(*Marker5_2*)>]
                                type ClassIA(*Marker3_2*)() =
                                    interface IA with
                                        member this.AbstractMember x = x + 1
    
                                type GenericClass(*Marker4_1*)<'a when 'a :> IA>() = 
                                    static member StaticMember(x:'a) = x.AbstractMember(1)


                                let GenerateTuple =  fun ( x : MyInt) ->    
                                                        let myInt(*Marker1_2*),float1,float2,function1 = (x,(float)x,(float)x, ( fun y -> (y.ToString(),y+1)) )
                                                        myInt,((float1,float2):PairOfFloat),function1

                                let MySeq(*Marker2_2*) = 
                                    seq     {
                
                                               for i in 1..9 do
                                                    let myInt,pairofFloat,function1 = GenerateTuple i
                        
                                                    yield pairofFloat
                                            }
                
                                let genericClass(*Marker4_2*) = new GenericClass<ClassIA>()"""

        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker1_1*)", "type MyInt = int")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker1_2*)", "val myInt : MyInt")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker2_1*)", "type PairOfFloat = float * float")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker2_2*)", "val MySeq : seq<PairOfFloat>")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker3_1*)", "type IA =")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker3_2*)", "type ClassIA =")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker4_1*)", "type GenericClass<'a (requires 'a :> IA)> =")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker4_2*)", "val genericClass : GenericClass<ClassIA>")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker5_1*)", "type AbAttrName = AbstractClassAttribute")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker5_2*)", "type AbAttrName = AbstractClassAttribute")

    [<Test>]
    member public this.``Automation.Regression.TypeInferenceSenarios.Bug2362&3538``() =
        let fileContent ="""module Test.Module1

                            open System
                            open System.Diagnostics
                            open System.Runtime.InteropServices

                            #nowarn "9"

                            let append m(*Marker1*) n(*Marker2*) = fun ac(*Marker3*) -> m (n ac)

                            type Foo() as this(*Marker4*) =
                                do this(*Marker5*) |> ignore
                                member this.Bar() =
                                    this(*Marker6*) |> ignore
                                    ()

                            [<StructLayout(LayoutKind.Explicit)>]
                            type A =
                                [<DefaultValue>]
                                val mutable x : int
                                new () = { }
                                member this.Prop = this.x
    
                            let x = new (*Marker7*)A()"""
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker1*)", "val m : ('a -> 'b)")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker2*)", "val n : ('c -> 'a)")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker3*)", "val ac : 'c")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker4*)", "val this : Foo")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker5*)", "val this : Foo")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker6*)", "val this : Foo")
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"(*Marker7*)","type A =")
        this.AssertQuickInfoContainsAtEndOfMarker(fileContent,"(*Marker7*)","val mutable x: int")

    [<Test>]
    member public this.``Automation.Regression.TypemoduleConstructorLastLine.Bug2494``() =
        let fileContent ="""namespace NS
                            open System
                            //regression test for bug 2494

                            type PriorityQueue(*MarkerType*)<'k,'a> =
                              | Nil(*MarkerDataConstructor*)
                              | Branch of 'k * 'a * PriorityQueue<'k,'a> * PriorityQueue<'k,'a>
  
                            module PriorityQueue(*Marker3*) =
                              let empty = Nil
  
                              let minKeyValue = function
                                | Nil             -> failwith "empty queue"
                                | Branch(k,a,_,_) -> (k,a)
    
                              let minKey pq = fst (minKeyValue pq(*MarkerVal*))
  
                              let singleton(*MarkerLastLine*) k a = Branch(k,a,Nil,Nil)"""
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*MarkerType*)", "type PriorityQueue")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*MarkerDataConstructor*)", "union case PriorityQueue.Nil: PriorityQueue<'k,'a>")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker3*)", "module PriorityQueue")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*MarkerVal*)", "val pq : PriorityQueue<'a,'b>")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*MarkerLastLine*)", "val singleton : k:'a -> a:'b -> PriorityQueue<'a,'b>")

    [<Test>]
    member public this.``Automation.WhereQuickInfoShouldNotShowUp``() =
        let fileContent ="""namespace Test

                            module Helper = 
                                /// Tests if passed System.Numerics.BigInteger(*Marker1*) argument is prime
                                let IsPrime x =  
                                    let mutable i = 2I
                                    let mutable foundFactor = false  
                                    while not foundFactor && i < x do
                                        (*
                                            the most naive way to test for number being prime
                                            Works great for small int(*Marker2*)
                                        *)
                                        if x % i = 0I then  
                                            foundFactor <- true  
                                        i <- i + 1I 
                                    not foundFactor
        
                            module App = 
                                open Helper
    
                                let sumOfAllPrimesUnder1Mi =
                                #if TEST_TWO_MI
                                    seq(*Marker4*) { 1I .. 2000000I }
                                #else
                                    seq { 1I .. 1000000I(*Marker7*) }
                                #endif
                                    |> Seq.filter(IsPrime)
                                    // find result after filtering seq(*Marker3*)
                                    |> Seq.sum
        
                                let myString hello = "hello"(*Marker5*)
    
                                myString "myString"(*Marker8*)
                                |> Seq.filter (fun c -> int c > 75)
                                |> Seq.item 0
                                |> (=) 'e'(*Marker6*)
                                |> ignore"""
        this.VerifyQuickInfoDoesNotContainAnyAtStartOfMarker fileContent "(*Marker1*)" "BigInteger"
        this.VerifyQuickInfoDoesNotContainAnyAtStartOfMarker fileContent "(*Marker2*)" "int"
        this.VerifyQuickInfoDoesNotContainAnyAtStartOfMarker fileContent "(*Marker3*)" "seq"
        this.VerifyQuickInfoDoesNotContainAnyAtStartOfMarker fileContent "(*Marker4*)" "seq"
        this.VerifyQuickInfoDoesNotContainAnyAtStartOfMarker fileContent "(*Marker5*)" "hello"
        this.VerifyQuickInfoDoesNotContainAnyAtStartOfMarker fileContent "(*Marker6*)" "char"
        this.VerifyQuickInfoDoesNotContainAnyAtStartOfMarker fileContent "(*Marker7*)" "bigint"
        this.VerifyQuickInfoDoesNotContainAnyAtStartOfMarker fileContent "(*Marker8*)" "myString"

    [<Test>]
    member public this.``Automation.Regression.XmlDocComments.Bug3157``() =
        let fileContent ="""namespace TestQuickinfo
                            module XmlComment =
                                /// XmlComment J
                                let func(*Marker*) x =
                                    /// XmlComment K
                                    let rec g x = 1
                                    g x"""
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker*)", "val func : x:'a -> int")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker*)", "XmlComment J")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker*)", "Full name: TestQuickinfo.XmlComment.func")
        this.VerifyQuickInfoDoesNotContainAnyAtStartOfMarker fileContent "(*Marker*)" "XmlComment K"

    [<Test>]
    member public this.``Automation.Regression.XmlDocCommentsOnExtensionMembers.Bug138112``() =
        let fileContent ="""module Module1 =
                                type T() = 
                                    /// XmlComment M1
                                    member this.M1() = ()
                                type T with
                                    /// XmlComment M2
                                    member this.M2() = ()
                                module public Extension =
                                    type T with
                                        /// XmlComment M3
                                        member this.M3() = ()
                            open Module1
                            open Extension

                            let x1 = T().M1(*Marker1*)()
                            let x2 = T().M2(*Marker2*)()
                            let x3 = T().M3(*Marker3*)()"""
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker1*)", "XmlComment M1")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker2*)", "XmlComment M2")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker3*)", "XmlComment M3")

    [<Test>]
    member public this.XmlDocCommentsForArguments() =
        let fileContent = """
                             type bar() =
                                 /// <summary> Test for members</summary>
                                 /// <param name="x1">x1 param!</param>
                                 member this.foo
                                     (x1:int)=
                                     System.Console.WriteLine(x1.ToString())
                             
                             type Uni1 = 
                                /// <summary> Test for unions </summary>
                                /// <param name="str">str of case1</param>
                                | Case1 of str: string
                                | None

                             /// <summary> Test for exception types</summary>
                             /// <param name="value">value param</param>
                             exception Ex1 of value: string

                             // Methods
                             let f1 = (new bar()).foo(*Marker0*)(x1(*Marker1*) = 10)
                             let f2 = System.String.Concat(1, arg1(*Marker2*) = "") 
                             
                             //Unions
                             let f3 = Case1(str(*Marker3*) = "10")
                             match f3 with 
                             | Case1(str(*Marker4*) = "10") -> ()
                             | _ -> ()
                             
                             //Exceptions
                             let f4 = Ex1(value(*Marker5*) = "")
                             try
                               ()
                             with
                               Ex1(value(*Marker6*) = v) -> ()

                             //Static parameters of type providers
                             type provType = N1.T<Param1(*Marker7*)="hello", ParamIgnored(*Marker8*)=10>
                             """

        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker0*)", "Test for members")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker1*)", "x1 param!")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker2*)", "<summary>Concatenates the string representations of two specified objects.</summary>")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker3*)", "str of case1")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker4*)", "str of case1")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker5*)", "value param")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker6*)", "value param")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker7*)", "Param1 of string",
                                                     addtlRefAssy = [PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll")])
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Marker8*)", "Ignored",
                                                     addtlRefAssy = [PathRelativeToTestAssembly(@"DummyProviderForLanguageServiceTesting.dll")])

    member private this.VerifyUsingFsTestLib fileContent queries crossProject =
        use _guard = this.UsingNewVS()
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        let testLibCode ="""namespace FSTestLib

                            /// DocComment: This is MyStruct type, represents a struct.
                            type MyPoint =
                                struct
                                    val mutable private m_X : float
                                    val mutable private m_Y : float
        
                                    new (x, y) = { m_X = x; m_Y = y }
        
                                    /// Gets and sets X
                                    member this.X with get () = this.m_X and set x = this.m_X <- x
        
                                    /// Gets and sets Y
                                    member this.Y with get () = this.m_Y and set y = this.m_Y <- y
        
                                    // Length of given Point
                                    member this.Len = sqrt ( this.X * this.X + this.Y * this.Y )
        
                                    static member (+) (p1 : MyPoint, p2 : MyPoint) = MyPoint(p1.X + p2.X, p1.Y + p2.Y)
        
                                end

                            [<NoComparison;NoEquality>]
                            /// DocComment: This is my record type.
                            type MyEmployee = 
                                { mutable Name  : string;
                                  mutable Age   : int;
                                  /// DocComment: Indicates whether the employee is full time or not
                                  mutable IsFTE : bool }
    
                                interface System.IComparable with
                                    member this.CompareTo (emp : obj) = 
                                        let r = emp :?> MyEmployee
                                        match r.IsFTE && this.IsFTE with
                                        | true -> this.Age - r.Age
                                        | _ -> System.Convert.ToInt32(this.IsFTE) - System.Convert.ToInt32(r.IsFTE)
    
                                override this.ToString() = sprintf "%s is %d." this.Name this.Age
    
                                /// DocComment: Method
                                static member MakeDummy () =
                                    { Name = System.String.Empty; Age = -1; IsFTE = false }
    
                                // TODO: Normally there's no DotCompletion after "this" here
                                override this.Equals(ob : obj) = 
                                    let r = ob :?> MyEmployee
                                    this.Name = r.Name && this.Age = r.Age && this.IsFTE = r.IsFTE

                            /// DocComment: This is my interface type
                            type IMyInterface = 
                                interface
                                    /// DocComment: abstract method in Interface
                                    abstract Represent : unit -> string        
                                end

                            // TODO: add formatable ToString()
                            /// DocComment: This is my discriminated union type
                            type MyDistance =
                                | Kilometers of float
                                | Miles of float
                                | NauticalMiles of float
    
    
                                /// DocComment: Static Method
                                static member toMiles x =
                                    Miles(
                                        match x with
                                        | Miles x -> x
                                        | Kilometers x -> x / 1.6
                                        | NauticalMiles x -> x * 1.15
                                    )

                                /// DocComment: Property        
                                member this.toNautical =
                                    NauticalMiles(
                                        match this with
                                        | Kilometers x -> x / 1.852
                                        | Miles x -> x / 1.15
                                        | NauticalMiles x -> x
                                    )

                                /// DocComment: Method        
                                member this.IncreaseBy dist = 
                                    match this with
                                    | Kilometers x -> Kilometers (x + dist)
                                    | Miles x -> Miles (x + dist)
                                    | NauticalMiles x -> NauticalMiles (x + dist)
        
                                /// DocComment: Event
                                static member Event = 
                                    let evnt = new Event<string>()
                                    evnt
        
                            /// DocComment: This is my enum type
                            type MyColors =
                                | /// DocComment: Field
                                  Red = 0
                                | Green = 1
                                | Blue = 2
    
                            /// DocComment: This is my class type
                            type MyCar( number: int, color:MyColors) =
                                /// DocComment: This is static field
                                static member Owner     = "MySelf"
                                /// DocComment: This is instance field
                                member this.Number      = number
                                member this.Color       = color
                                /// DocComment: This is static method
                                static member Run (number:int)      = printf "%s" (number.ToString()+"Running")
                                /// DocComment: This is instance method
                                member this.Repair  (expense:int)  = printf "%s" ("Spent " + expense.ToString() + " for repairing. ")
    
                            /// DocComment: This is my delegate type
                            type ControlEventHandler = delegate of int -> unit"""
        let file2 =
            if (crossProject = true) then
                let file1 = AddFileFromTextBlob(project,"File1.fs",testLibCode)
                let project2 = CreateProject(solution,"codeProject")
                let file2 = AddFileFromTextBlob(project2,"File2.fs",fileContent)
                Build(project).BuildSucceeded |> ignore
                AddProjectReference(project2, project)
                let file1 = OpenFile(project,"File1.fs")
                let file2 = OpenFile(project2,"File2.fs")
                file2
            else
                let file1 = AddFileFromTextBlob(project,"File1.fs",testLibCode)
                let file2 = AddFileFromTextBlob(project,"File2.fs",fileContent)
                let file1 = OpenFile(project,"File1.fs")
                let file2 = OpenFile(project,"File2.fs")
                file2
        //Build(project).BuildSucceeded |> printf "%b"
        for (marker, expectedTip) in queries do
            MoveCursorToStartOfMarker(file2, marker)
            let tooltip = time1 GetQuickInfoAtCursor file2 "Time of first tooltip"
            printf "First-%s\n" tooltip
            AssertContains(tooltip, expectedTip)

                
    [<Test>]
    member public this.``Automation.XDelegateDUStructfromOwnCode``() =
        let fileContent ="""module Test

                            open FSTestLib
                                
                            open System.Runtime.InteropServices
                            let ctrlSignal = ref false
                            [<DllImport("kernel32.dll")>]
                            extern void SetConsoleCtrlHandler(ControlEventHandler callback,bool add)
                            let ctrlEnventHandlerStatic     = new ControlEventHandler(MyCar.Run)
                            let ctrlEnventHandlerInstance   = new ControlEventHandler( (new MyCar(10, MyColors.Blue)).Repair )

                            let IsInstanceMethod (controlEventHandler:ControlEventHandler) =
                                // TC 32	Identifier	Delegate	Own Code	Pattern Match
                                match controlEventHandler(*Marker1*).Method.IsStatic  with 
                                | true -> printf "It's not a instance method. "
                                | false -> printf " It's a instance method. " 
    
                            // TC 33	Event	DiscUnion	Own Code	Quotation
                            let a = <@ MyDistance.Event(*Marker2*) @>

                            let DelegateSeq =
                                seq {   for i in 1..10 do
                                            let newDelegate = new ControlEventHandler(MyCar.Run)
                                            // TC 35	Identifier	Delegate	Own Code	Comp Expression
                                            yield newDelegate(*Marker3*) }
                
                            let StructFieldSeq =
                                seq { for i in 1..10 do
                                            let a = MyPoint((float)i,2.0)
                                            // TC 36	Field	Struct	Own Code	Comp Expression
                                            yield a.X(*Marker4*) }"""
        let queries =   [("(*Marker1*)", "val controlEventHandler : ControlEventHandler");
                         ("(*Marker2*)", "property MyDistance.Event: Event<string>");
                         ("(*Marker3*)", "val newDelegate : ControlEventHandler");
                         ("(*Marker4*)", "property MyPoint.X: float");
                         ("(*Marker4*)", "Gets and sets X")]
        this.VerifyUsingFsTestLib fileContent queries false

    [<Test>]
    member public this.``Automation.StructDelegateDUfromOwnCode``() =
        let fileContent ="""module Test

                            open FSTestLib
                                
                            open System.Runtime.InteropServices
                            let ctrlSignal = ref false
                            [<DllImport("kernel32.dll")>]
                            extern void SetConsoleCtrlHandler(ControlEventHandler callback,bool add)
                            let ctrlEnventHandlerStatic     = new ControlEventHandler(MyCar.Run)
                            let ctrlEnventHandlerInstance   = new ControlEventHandler( (new MyCar(10, MyColors.Blue)).Repair )

                            let IsInstanceMethod (controlEventHandler:ControlEventHandler) =
                                // TC 32	Identifier	Delegate	Own Code	Pattern Match
                                match controlEventHandler(*Marker1*).Method.IsStatic  with 
                                | true -> printf "It's not a instance method. "
                                | false -> printf " It's a instance method. " 
    
                            // TC 33	Event	DiscUnion	Own Code	Quotation
                            let a = <@ MyDistance.Event(*Marker2*) @>


                            let DelegateSeq =
                                seq {   for i in 1..10 do
                                            let newDelegate = new ControlEventHandler(MyCar.Run)
                                            // TC 35	Identifier	Delegate	Own Code	Comp Expression
                                            yield newDelegate(*Marker3*) }

                            let StructFieldSeq =
                                seq { for i in 1..10 do
                                            let a = MyPoint((float)i,2.0)
                                            // TC 36	Field	Struct	Own Code	Comp Expression
                                            yield a.X(*Marker4*) }"""
        let queries =  [("(*Marker1*)", "val controlEventHandler : ControlEventHandler");
                        ("(*Marker2*)", "property MyDistance.Event: Event<string>");
                        ("(*Marker3*)", "val newDelegate : ControlEventHandler");
                        ("(*Marker4*)", "property MyPoint.X: float");
                        ("(*Marker4*)", "Gets and sets X");
                        ]
        this.VerifyUsingFsTestLib fileContent queries false

    [<Test>]
    member public this.``Automation.TupleRecordClassfromOwnCode``() =
        let fileContent ="""module Test

                            open FSTestLib

                            let AbsTuple =  fun x ->        let tuple1 = (x,x.ToString(),(float)x, ( fun y -> (y.ToString(),y+1)) )
                                                            let tuple2 = (-x,(-x).ToString(),(float)(-x), ( fun y -> (y.ToString(),y+1)) )
                                                            if x >= 0 then 
                                                            // TC 29	Self	Tuple	Own Code	Imperative
                                                                tuple1(*Marker1*)
                                                            else
                                                                tuple2
                                
                            let GenerateMyEmployee name age = 
                                let a = MyEmployee.MakeDummy()
                                a.Name  <- name
                                a.Age   <- age
                                a.IsFTE <- System.Convert.ToBoolean(System.Random().Next(2))
                                match a.IsFTE with
                                | true -> a
                                // TC 30  Operator	Record	Own Code	Pattern Match
                                | _ -> MyEmployee(*Marker2*).MakeDummy()
    
                            // TC 31	Self	Class	Own Code	Quotation
                            let myCarQuot = <@ new MyCar(*Marker3*)(19,MyColors.Red) @>

                            open System.Runtime.InteropServices
                            let ctrlSignal = ref false
                            [<DllImport("kernel32.dll")>]
                            extern void SetConsoleCtrlHandler(ControlEventHandler callback,bool add)
                            let ctrlEnventHandlerStatic     = new ControlEventHandler(MyCar.Run)
                            let ctrlEnventHandlerInstance   = new ControlEventHandler( (new MyCar(10, MyColors.Blue)).Repair )
    
                            let MaxTuple x y =
                                let tuplex = (x,x.ToString() )
                                let tupley = (y,(y).ToString())
                                match x>y with
                                // TC 34	Operator	Tuple	Own Code	Pattern Match
                                | true -> tuplex(*Marker4*)
                                | false -> tupley"""
        let queries =  [("(*Marker1*)", "val tuple1 : int * string * float * (int -> string * int)");
                        ("(*Marker2*)", "type MyEmployee");
                        ("(*Marker2*)", "Full name: FSTestLib.MyEmployee");
                        ("(*Marker3*)", "type MyCar");
                        ("(*Marker3*)", "Full name: FSTestLib.MyCar");
                        ("(*Marker4*)", "val tuplex : 'a * string")
                        ]
        this.VerifyUsingFsTestLib fileContent queries false

(*------------------------------------------IDE Query automation start -------------------------------------------------*)

    member private this.AssertQuickInfoInQuery(code: string, mark : string, expectedstring : string) =
        use _guard = this.UsingNewVS()
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
        let solution = this.CreateSolution()
        let project = CreateProject(solution,"testproject")
        this.AddAssemblyReference(project, "System.Xml.Linq")
        let file1 = AddFileFromTextBlob(project,"File1.fs",datacode)
        //build
        let file2 = AddFileFromTextBlob(project,"File2.fs",code)
        let file1 = OpenFile(project,"File1.fs")
        let file2 = OpenFile(project,"File2.fs")
        
        let gpatcc = GlobalParseAndTypeCheckCounter.StartNew(this.VS)
        MoveCursorToStartOfMarker(file2,mark)
        let tooltip = time1 GetQuickInfoAtCursor file2 "Time of first tooltip"
        printfn "%s" tooltip
        AssertContains(tooltip, expectedstring) 
        gpatcc.AssertExactly(0,0)   


    [<Test>]
    [<Category("Query")>]
    [<Ignore("bug196137:Wrong type quickinfo in the query with errors elsewhere")>]
    // QuickInfo still works on valid operators in a query with errors elsewhere in it
    member public this.``Query.WithError1.Bug196137``() =
        let fileContent ="""
            open DataSource
            // get the product list, defined in another file, see AssertQuickInfoInQuery
            let products = Products.getProductList() 
            let sortedProducts =
                query {
                    for p in products do
                    let x = p.ProductID + "a"
                    sortBy p.ProductName(*Mark*)
                    select p
                }"""
        this.AssertQuickInfoInQuery (fileContent, "(*Mark*)", "Product.ProductName: string")

    [<Test>]
    [<Category("Query")>]
    // QuickInfo still works on valid operators in a query with errors elsewhere in it
    member public this.``Query.WithError2``() =
        let fileContent ="""
            open DataSource
            let products = Products.getProductList()
            let test = 
                query {
                    for p in products do
                    let x = p.ProductID + "1"
                    minBy(*Mark*) p.UnitPrice 
                    }"""
        this.AssertQuickInfoInQuery (fileContent, "(*Mark*)", "custom operation: minBy ('Value)")

    [<Test; Ignore("Multiple failures due to CancellationTokenSource being disposed. Bad test")>]
    [<Category("Query")>]
    // QuickInfo works in a large query (using many operators)
    member public this.``Query.WithinLargeQuery``() =
        let fileContent ="""
            open DataSource
            let products = Products.getProductList()
            let numbers = [ 1;2; 8; 9; 15; 23; 3; 42; 4;0; 55;]
            let largequery =
                query {
                    for p in products do
                    sortBy p.ProductName
                    thenBy p.UnitPrice
                    thenByDescending p.Category
                    where (p.UnitsInStock < 100)
                    where (p.Category = "Condiments")
                    groupValBy(*Mark1*) p p.Category into g
                    let maxPrice = query { for x in g do maxBy(*Mark2*) x.UnitPrice }
                    let mostExpensiveProducts = query { for x in g do where (x.UnitPrice = maxPrice) }
                    select (g.Key, mostExpensiveProducts, query {
                                                                for n in numbers do
                                                                where (n%2 = 0)
                                                                where(*Mark3*) (n > 2)
                                                                where (n < 40)
                                                                select n})
                    distinct(*Mark4*)
                }"""
        this.AssertQuickInfoInQuery (fileContent, "(*Mark1*)", "custom operation: groupValBy ('Value) ('Key)")
        this.AssertQuickInfoInQuery (fileContent, "(*Mark2*)", "custom operation: maxBy ('Value)")
        this.AssertQuickInfoInQuery (fileContent, "(*Mark3*)", "custom operation: where (bool)")
        this.AssertQuickInfoInQuery (fileContent, "(*Mark4*)", "custom operation: distinct")

    [<Test>]
    [<Category("Query")>]
    // Arguments to query operators have correct QuickInfo
    // quickinfo should be corroct including when the operator is causing an error
    member public this.``Query.ArgumentToQuery.OperatorError``() =
        let fileContent ="""
            let numbers = [ 1;2; 8; 9; 15; 23; 3; 42; 4;0; 55;]
            let foo = 
                query {
                    for n in numbers do
                    orderBy (n.GetType())
                    select n}"""
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "n.GetType()", "val n : int",queryAssemblyRefs)
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "Type()", "System.Object.GetType() : System.Type",queryAssemblyRefs)

    [<Test>]
    [<Category("Query")>]
    // Arguments to query operators have correct QuickInfo
    // quickinfo should be corroct In a nested query
    member public this.``Query.ArgumentToQuery.InNestedQuery``() =
        let fileContent ="""
            open DataSource
            let products = Products.getProductList()
            let test1 = 
                query {
                    for p in products do
                    sortBy p.ProductName
                    select (p.ProductName, query { for f in products do
                                                   groupValBy(*Mark3*) f f.Category into g      
                                                   let maxPrice = query { for x in g do maxBy x.UnitPrice }
                                                   let mostExpensiveProducts = query { for x in g do where(*Mark1*) (x.UnitPrice = maxPrice(*Mark2*)) }
                                                   select(*Mark4*) (g.Key, g)}) } """
        this.AssertQuickInfoInQuery (fileContent, "(*Mark1*)", "custom operation: where (bool)")
        this.AssertQuickInfoInQuery (fileContent, "(*Mark2*)", "val maxPrice : decimal")
        this.AssertQuickInfoInQuery (fileContent, "(*Mark3*)", "custom operation: groupValBy ('Value) ('Key)")
        this.AssertQuickInfoInQuery (fileContent, "(*Mark4*)", "custom operation: select ('Result)")

    [<Test>]
    [<Category("Query")>]
    // A computation expression with its own custom operators has correct QuickInfo displayed
    member public this.``Query.ComputationExpression.Method``() =
        let fileContent ="""
            open System.Collections.Generic
            let chars = ["A";"B";"C"]
            type WorkflowBuilder() =

                let yieldedItems = new List<string>()
                member this.Items = yieldedItems |> Array.ofSeq

                member this.Yield(item) = yieldedItems.Add(item)
                member this.YieldFrom(items : seq<string>) = 
                    items |> Seq.iter (fun item -> yieldedItems.Add(item.ToUpper()))
                    ()

                member this.Combine(f, g) = g
                member this.Delay (f : unit -> 'a) =
                    f()

                member this.Zero() = ()
                member this.Return _ = this.Items

            let computationExpreQuery = 
                query {
                        for char in chars do
                        let workflow = new WorkflowBuilder()
                        let result =
                            workflow {
                                yield "foo"
                                yield "bar"
                                yield! [| "a"; "b"; "c" |]
        
                                return ()
                            }
                        let t = workflow.Combine(*Mark1*)("a","b")
                        let d = workflow.Zero(*Mark2*)()
                        where (result |> Array.exists(fun i -> i = char)) 
                        yield char
                       } """
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Mark1*)", "member WorkflowBuilder.Combine : f:'b0 * g:'c1 -> 'c1",queryAssemblyRefs)
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Mark2*)", "member WorkflowBuilder.Zero : unit -> unit",queryAssemblyRefs)

    [<Test>]
    [<Category("Query")>]
    // A computation expression with its own custom operators has correct QuickInfo displayed
    member public this.``Query.ComputationExpression.CustomOp``() =
        let fileContent ="""
            open System
            open Microsoft.FSharp.Quotations

            type EventBuilder() = 
                member _.For(ev:IObservable<'T>, loop:('T -> #IObservable<'U>)) : IObservable<'U> = failwith ""
                member _.Yield(v:'T) : IObservable<'T> = failwith ""
                member _.Quote(v:Quotations.Expr<'T>) : Expr<'T> = v
                member _.Run(x:Expr<'T>) = Microsoft.FSharp.Linq.RuntimeHelpers.LeafExpressionConverter.EvaluateQuotation x :?> 'T
         
                [<CustomOperation("myWhere",MaintainsVariableSpace=true)>]
                member _.Where (x, [<ProjectionParameter>] f) = Observable.filter f x
         
                [<CustomOperation("mySelect")>]
                member _.Select (x, [<ProjectionParameter>] f) = Observable.map f x

                [<CustomOperation("scanSumBy")>]
                member inline _.ScanSumBy (source, [<ProjectionParameter>] f : 'T -> 'U) : IObservable<'U> = Observable.scan (fun a b -> a + f b) LanguagePrimitives.GenericZero<'U> source
 
            let myquery = EventBuilder()
            let f = new Event<int * int >()
            let e1 =     
                myquery { for x in f.Publish do 
                            myWhere(*Mark1*) (fst x < 100)
                            scanSumBy(*Mark2*) (snd x)
                            } """
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Mark1*)", "custom operation: myWhere (bool)")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Mark1*)", "Calls EventBuilder.Where")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Mark2*)", "custom operation: scanSumBy ('U)")
        this.AssertQuickInfoContainsAtStartOfMarker (fileContent, "(*Mark2*)", "Calls EventBuilder.ScanSumBy")


// Context project system
[<TestFixture>] 
type UsingProjectSystem() = 
    inherit UsingMSBuild(VsOpts = LanguageServiceExtension.ProjectSystemTestFlavour)
