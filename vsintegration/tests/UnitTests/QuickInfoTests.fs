namespace Microsoft.VisualStudio.FSharp.Editor.Tests.Roslyn

open System.IO
open FSharp.Compiler.CodeAnalysis
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Text
open Microsoft.VisualStudio.FSharp.Editor
open NUnit.Framework
open UnitTests.TestLib.LanguageService
open VisualFSharp.UnitTests.Roslyn

[<Category "Roslyn Services">]
module QuickInfo =

let internal GetQuickInfo (project:FSharpProject) (fileName:string) (caretPosition:int) =
    async {
        let code = File.ReadAllText(fileName)
        let document, _ = RoslynTestHelpers.CreateDocument(fileName, code)
        return! FSharpAsyncQuickInfoSource.ProvideQuickInfo(document, caretPosition)
    } |> Async.RunSynchronously

let GetQuickInfoText (project:FSharpProject) (fileName:string) (caretPosition:int) =
    let sigHelp = GetQuickInfo project fileName caretPosition
    match sigHelp with
    | Some (quickInfo) ->
        let documentationBuilder =
            { new IDocumentationBuilder with
                override _.AppendDocumentationFromProcessedXML(_, _, _, _, _, _) = ()
                override _.AppendDocumentation(_, _, _, _, _, _, _) = ()
            }
        let mainDescription, docs = FSharpAsyncQuickInfoSource.BuildSingleQuickInfoItem documentationBuilder quickInfo
        let mainTextItems =
            mainDescription
            |> Seq.map (fun x -> x.Text)
        let docTextItems =
            docs
            |> Seq.map (fun x -> x.Text)
        System.String.Join(System.String.Empty, (Seq.concat [mainTextItems; docTextItems]))
    | _ -> ""

let GetQuickInfoTextFromCode (code:string) =
    use project = SingleFileProject code
    let fileName, caretPosition = project.GetCaretPosition()
    GetQuickInfoText project fileName caretPosition

let expectedLines (lines:string list) = System.String.Join("\n", lines)

// migrated from legacy test
[<Test>]
let ``Automation.EnumDUInterfacefromFSBrowse.InsideComputationExpression`` () =
    let code = """
namespace FsTest

type MyColors =
     | Red = 0
     | Green = 1
     | Blue = 2

module Test =
    let test() =
        let x =
            seq {
                for i in 1..10 do
                    let f = MyColors.Re$$d
                    yield f
                }
        ()
"""
    let quickInfo = GetQuickInfoTextFromCode code
    let expected = "MyColors.Red: MyColors = 0"
    Assert.AreEqual(expected, quickInfo)

// migrated from legacy test
[<Test>]
let ``Automation.EnumDUInterfacefromFSBrowse.InsideMatch`` () =
    let code = """
namespace FsTest

type MyDistance =
    | Kilometers of float
    | Miles of float
    | NauticalMiles of float

module Test =
    let test() =
        let myDuList = (fun x ->
            match x with
            | 0 -> MyDistanc$$e.Kilometers
            | 1 -> MyDistance.Miles
            | _ -> MyDistance.NauticalMiles
            )
        ()
"""
    let quickInfo = GetQuickInfoTextFromCode code
    let expected =
        expectedLines [ "type MyDistance ="
                        "  | Kilometers of float"
                        "  | Miles of float"
                        "  | NauticalMiles of float"
                        "Full name: FsTest.MyDistance" ]
    Assert.AreEqual(expected, quickInfo)

// migrated from legacy test
[<Test>]
let ``Automation.EnumDUInterfacefromFSBrowse.InsideLambda`` () =
    let code = """
namespace FsTest

type IMyInterface =
    interface
        abstract Represent : unit -> string
    end

type MyTestType() =
    [<DefaultValue>]
    val mutable field : int

    interface IMyInterface with
        member this.Represent () = "Implement Interface"

module Test =
    let test() =
        let s = new MyTestType()
                |> fun (x:MyTestType) -> x :> IMyInterface
                |> fun (x:IMyInterface) -> x.Represen$$t()
        ()
"""
    let quickInfo = GetQuickInfoTextFromCode code
    let expected = "abstract member IMyInterface.Represent : unit -> string"
    Assert.AreEqual(expected, quickInfo)

// migrated from legacy test
[<Test>]
let ``Automation.RecordAndInterfaceFromFSProj.InsideComputationExpression``() =
    let code = """
namespace FsTest

type MyEmployee = 
    { mutable Name  : string;
      mutable Age   : int;
      mutable IsFTE : bool }

module Test =
    let test() =
        let construct =
            seq {
                for i in 1..10 do
                let a = MyEmploye$$e.MakeDummy()
                ()
            }
    ()
"""
    let quickInfo = GetQuickInfoTextFromCode code
    let expected =
        expectedLines [ "type MyEmployee ="
                        "  { mutable Name: string"
                        "    mutable Age: int"
                        "    mutable IsFTE: bool }"
                        "Full name: FsTest.MyEmployee" ]
    Assert.AreEqual(expected, quickInfo)
    ()

// migrated from legacy test
[<Test>]
let ``Automation.RecordAndInterfaceFromFSProj.InsideQuotation``() =
    let code = """
namespace FsTest

type MyEmployee = 
    { mutable Name  : string;
      mutable Age   : int;
      mutable IsFTE : bool }

module Test =
    let test() =
        let aa = { Name: "name";
                   Age: 1;
                   IsFTE: false; }
        let b = <@ a$$a.Name @>
    ()
"""
    let quickInfo = GetQuickInfoTextFromCode code
    let expected = "val aa : MyEmployee"
    Assert.AreEqual(expected, quickInfo)
    ()

// migrated from legacy test
[<Test>]
let ``Automation.RecordAndInterfaceFromFSProj.InsideLambda``() =
    let code = """
namespace FsTest

type MyEmployee = 
    { mutable Name  : string;
      mutable Age   : int;
      mutable IsFTE : bool }

module Test =
    let test() =
        let aa = { Name: "name";
                   Age: 1;
                   IsFTE: false; }
        let b =
            [ aa ]
            |> List.filter (fun e -> e.IsFT$$E)
    ()
"""
    let quickInfo = GetQuickInfoTextFromCode code
    let expected = "MyEmployee.IsFTE: bool"
    Assert.AreEqual(expected, quickInfo)
    ()

// migrated from legacy test
[<Test>]
let ``Automation.TupleRecordFromFSBrowse.InsideComputationExpression``() =
    let code = """
namespace FsTest

module Test =
    let GenerateTuple =
        fun x ->
            let tuple = (x, x.ToString(), (float)x, (fun y -> (y.ToString(), y + 1)))
            tuple
    let test() =
        let mySeq =
            seq {
                for i in 1..9 do
                    let my$$Tuple = GenerateTuple i
                    yield myTuple
            }
    ()
"""
    let quickInfo = GetQuickInfoTextFromCode code
    let expected = "val myTuple : int * string * float * (int -> string * int)"
    Assert.AreEqual(expected, quickInfo)
    ()

// migrated from legacy test
[<Test>]
let ``Automation.TupleRecordFromFSBrowse.SequenceOfMethods``() =
    let code = """
namespace FsTest

module Test =
    let GenerateTuple =
        fun x ->
            let tuple = (x, x.ToString(), (float)x, (fun y -> (y.ToString(), y + 1)))
            tuple
    let GetTupleMethod tuple =
        let (_intInTuple, _stringInTuple, _floatInTuple, methodInTuple) = tuple
        methodInTuple
    let test() =
        let mySeq =
            seq {
                for i in 1..9 do
                    let myTuple = GenerateTuple i
                    yield myTuple
            }
        let method$$Seq = Seq.map GetTupleMethod mySeq
    ()
"""
    let quickInfo = GetQuickInfoTextFromCode code
    let expected = "val methodSeq : seq<(int -> string * int)>"
    Assert.AreEqual(expected, quickInfo)
    ()

// migrated from legacy test
[<Test>]
let ``Automation.UnionAndStructFromFSProj.MatchExpression``() =
    let code = """
namespace FsTest

[<Struct>]
type MyPoint(x:int, y:int) =
    member this.X = x
    member this.Y = y

module Test =
    let test() =
        let p1 = MyPoint(1, 2)
        match p$$1 with
        | p3 when p3.X = 1 -> 0
        | _ -> 1
"""
    let quickInfo = GetQuickInfoTextFromCode code
    let expected = "val p1 : MyPoint"
    Assert.AreEqual(expected, quickInfo)
    ()

// migrated from legacy test
[<Test>]
let ``Automation.UnionAndStructFromFSProj.MatchPattern``() =
    let code = """
namespace FsTest

[<Struct>]
type MyPoint(x:int, y:int) =
    member this.X = x
    member this.Y = y

module Test =
    let test() =
        let p1 = MyPoint(1, 2)
        match p1 with
        | p$$3 when p3.X = 1 -> 0
        | _ -> 1
"""
    let quickInfo = GetQuickInfoTextFromCode code
    let expected = "val p3 : MyPoint"
    Assert.AreEqual(expected, quickInfo)
    ()

// migrated from legacy test
[<Test>]
let ``Automation.UnionAndStructFromFSProj.UnionIfPredicate``() =
    let code = """
namespace FsTest

type MyDistance =
    | Kilometers of float
    | Miles of float
    | NauticalMiles of float

module Test =
    let test() =
        let dd = MyDistance.Kilometers 1.0
        if MyDistance.toMiles d$$d > 0 then ()
        else ()
"""
    let quickInfo = GetQuickInfoTextFromCode code
    let expected = "val dd : MyDistance"
    Assert.AreEqual(expected, quickInfo)
    ()

// migrated from legacy test
[<Test>]
let ``Automation.UnionAndStructFromFSProj.UnionForPattern``() =
    let code = """
namespace FsTest

type MyDistance =
    | Kilometers of float
    | Miles of float
    | NauticalMiles of float

module Test =
    let test() =
        let distances = [ MyDistance.Kilometers 1.0; MyDistance.Miles 1.0 ]
        for dist$$ance in distances do
            ()
"""
    let quickInfo = GetQuickInfoTextFromCode code
    let expected = "val distance : MyDistance"
    Assert.AreEqual(expected, quickInfo)
    ()

// migrated from legacy test
[<Test>]
let ``Automation.UnionAndStructFromFSProj.UnionMethodPatternMatch``() =
    let code = """
namespace FsTest

type MyDistance =
    | Kilometers of float
    | Miles of float
    | NauticalMiles of float
    static member toMiles x =
        Miles(
            match x with
            | Miles x -> x
            | Kilometers x -> x / 1.6
            | NauticalMiles x -> x * 1.15
        )

module Test =
    let test() =
        let dd = MyDistance.Kilometers 1.0
        match MyDistance.to$$Miles dd with
        | Miles x -> 0
        | _ -> 1
"""
    let quickInfo = GetQuickInfoTextFromCode code
    let expected = "static member MyDistance.toMiles : x:MyDistance -> MyDistance"
    Assert.AreEqual(expected, quickInfo)
    ()

// migrated from legacy test
[<Test>]
let ``Automation.UnionAndStructFromFSProj.UnionMethodPatternMatchBody``() =
    let code = """
namespace FsTest

type MyDistance =
    | Kilometers of float
    | Miles of float
    | NauticalMiles of float
    member this.IncreaseBy dist =
        match this with
        | Kilometers x -> Kilometers (x + dist)
        | Miles x -> Miles (x + dist)
        | NauticalMiles x -> NauticalMiles (x + dist)

module Test =
    let test() =
        let dd = MyDistance.Kilometers 1.0
        match dd.toMiles() with
        | Miles x -> dd.Increase$$By 1.0
        | _ -> dd
"""
    let quickInfo = GetQuickInfoTextFromCode code
    let expected = "member MyDistance.IncreaseBy : dist:float -> MyDistance"
    Assert.AreEqual(expected, quickInfo)
    ()

// migrated from legacy test
[<Test>]
let ``Automation.UnionAndStructFromFSProj.UnionPropertyInComputationExpression``() =
    let code = """
namespace FsTest

type MyDistance =
    | Kilometers of float
    | Miles of float
    | NauticalMiles of float
    member this.asNautical =
        NauticalMiles(
            match this with
            | Kilometers x -> x / 1.852
            | Miles x -> x / 1.15
            | NauticalMiles x -> x
        )

module Test =
    let test() =
        let dd = MyDistance.Kilometers 1.0
        async {
            let r = dd.as$$Nautical
            return r
        }
"""
    let quickInfo = GetQuickInfoTextFromCode code
    let expected = "property MyDistance.asNautical: MyDistance with get"
    Assert.AreEqual(expected, quickInfo)
    ()
