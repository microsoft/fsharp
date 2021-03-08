#if INTERACTIVE
#r "../../artifacts/bin/fcs/net461/FSharp.Compiler.Service.dll" // note, build FSharp.Compiler.Service.Tests.fsproj to generate this, this DLL has a public API so can be used from F# Interactive
#r "../../artifacts/bin/fcs/net461/nunit.framework.dll"
#load "FsUnit.fs"
#load "Common.fs"
#else
module Tests.Service.ServiceUntypedParseTests
#endif

open System.IO
open FsUnit
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.EditorServices
open FSharp.Compiler.IO
open FSharp.Compiler.Service.Tests.Common
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Position
open NUnit.Framework

let [<Literal>] private Marker = "(* marker *)"

let private (=>) (source: string) (expected: CompletionContext option) =

    let lines =
        use reader = new StringReader(source)
        [| let line = ref (reader.ReadLine())
           while not (isNull !line) do
              yield !line
              line := reader.ReadLine()
           if source.EndsWith "\n" then
              yield "" |]
        
    let markerPos = 
        lines 
        |> Array.mapi (fun i x -> i, x)
        |> Array.tryPick (fun (lineIdx, line) -> 
            match line.IndexOf Marker with 
            | -1 -> None 
            | idx -> Some (mkPos (Line.fromZ lineIdx) idx))
        
    match markerPos with
    | None -> failwithf "Marker '%s' was not found in the source code" Marker
    | Some markerPos ->
        let parseTree = parseSourceCode("C:\\test.fs", source)
        let actual = ParsedInput.TryGetCompletionContext(markerPos, parseTree, lines.[Line.toZ markerPos.Line])
        try Assert.AreEqual(expected, actual)
        with e ->
            printfn "ParseTree: %A" parseTree
            reraise()

module AttributeCompletion =
    [<Test>]
    let ``at [<|, applied to nothing``() =
        """
[<(* marker *)
"""  
     => Some CompletionContext.AttributeApplication

    [<TestCase ("[<(* marker *)", true)>]
    [<TestCase ("[<AnAttr(* marker *)", true)>]
    [<TestCase ("[<type:(* marker *)", true)>]
    [<TestCase ("[<type:AnAttr(* marker *)", true)>]
    [<TestCase ("[< (* marker *)", true)>]
    [<TestCase ("[<AnAttribute;(* marker *)", true)>]
    [<TestCase ("[<AnAttribute; (* marker *)", true)>]
    [<TestCase ("[<AnAttribute>][<(* marker *)", true)>]
    [<TestCase ("[<AnAttribute>][< (* marker *)", true)>]
    [<TestCase ("[<AnAttribute((* marker *)", false)>]
    [<TestCase ("[<AnAttribute( (* marker *)", false)>]
    [<TestCase ("[<AnAttribute (* marker *)", false)>]
    [<TestCase ("[<AnAttribute>][<AnAttribute((* marker *)", false)>]
    [<TestCase ("[<AnAttribute; AnAttribute((* marker *)", false)>]
    let ``incomplete``(lineStr: string, expectAttributeApplicationContext: bool) =
        (sprintf """
%s
type T =
    { F: int }
""" lineStr)  => (if expectAttributeApplicationContext then Some CompletionContext.AttributeApplication else None)

    [<TestCase ("[<(* marker *)>]", true)>]
    [<TestCase ("[<AnAttr(* marker *)>]", true)>]
    [<TestCase ("[<type:(* marker *)>]", true)>]
    [<TestCase ("[<type:AnAttr(* marker *)>]", true)>]
    [<TestCase ("[< (* marker *)>]", true)>]
    [<TestCase ("[<AnAttribute>][<(* marker *)>]", true)>]
    [<TestCase ("[<AnAttribute>][< (* marker *)>]", true)>]
    [<TestCase ("[<AnAttribute;(* marker *)>]", true)>]
    [<TestCase ("[<AnAttribute; (* marker *) >]", true)>]
    [<TestCase ("[<AnAttribute>][<AnAttribute;(* marker *)>]", true)>]
    [<TestCase ("[<AnAttribute((* marker *)>]", false)>]
    [<TestCase ("[<AnAttribute (* marker *) >]", false)>]
    [<TestCase ("[<AnAttribute>][<AnAttribute((* marker *)>]", false)>]
    [<TestCase ("[<AnAttribute; AnAttribute((* marker *)>]", false)>]
    [<TestCase ("[<AnAttribute; AnAttribute( (* marker *)>]", false)>]
    [<TestCase ("[<AnAttribute>][<AnAttribute; AnAttribute((* marker *)>]", false)>]
    let ``complete``(lineStr: string, expectAttributeApplicationContext: bool) =
        (sprintf """
%s
type T =
    { F: int }
""" lineStr)  => (if expectAttributeApplicationContext then Some CompletionContext.AttributeApplication else None)



[<Test>]
let ``Attribute lists`` () =
    let source = """
[<A>]
let foo1 = ()

[<A>]
[<B;C>]
let foo2 = ()

[<A>] [<B;C>]
let foo3 = ()

[<A
let foo4 = ()

[<A;
let foo5 = ()

[<
let foo6 = ()

[<>]
let foo7 = ()

[<A;>]
let foo8 = ()
"""
    let (SynModuleOrNamespace (decls = decls)) = parseSourceCodeAndGetModule source
    decls |> List.map (fun decl ->
        match decl with
        | SynModuleDecl.Let (_, [SynBinding (attributes = attributeLists)], _) ->
            attributeLists |> List.map (fun list -> list.Attributes.Length, getRangeCoords list.Range)
        | _ -> failwith "Could not get binding")
    |> shouldEqual
        [ [ (1, ((2,  0),  (2, 5))) ]
          [ (1, ((5,  0),  (5, 5))); (2, ((6, 0), (6, 7))) ]
          [ (1, ((9,  0),  (9, 5))); (2, ((9, 6), (9, 13))) ]
          [ (1, ((12, 0), (13, 0))) ]
          [ (1, ((15, 0), (15, 4))) ]
          [ (0, ((18, 0), (18, 2))) ]
          [ (0, ((21, 0), (21, 4))) ]
          [ (1, ((24, 0), (24, 6))) ] ]


let rec getParenTypes (synType: SynType): SynType list =
    [ match synType with
      | SynType.Paren (innerType, _) ->
          yield synType
          yield! getParenTypes innerType

      | SynType.Fun (argType, returnType, _) ->
          yield! getParenTypes argType
          yield! getParenTypes returnType

      | SynType.Tuple (_, types, _) ->
          for _, synType in types do
              yield! getParenTypes synType

      | SynType.AnonRecd (_, fields, _) ->
          for _, synType in fields do
              yield! getParenTypes synType

      | SynType.App (typeName = typeName; typeArgs = typeArgs)
      | SynType.LongIdentApp (typeName = typeName; typeArgs = typeArgs) ->
          yield! getParenTypes typeName
          for synType in typeArgs do
              yield! getParenTypes synType

      | _ -> () ]

[<Test>]
let ``SynType.Paren ranges`` () =
    let source = """
((): int * (int * int))
((): (int -> int) -> int)
((): ((int)))
"""
    let (SynModuleOrNamespace (decls = decls)) = parseSourceCodeAndGetModule source
    decls |> List.map (fun decl ->
        match decl with
        | SynModuleDecl.DoExpr (expr = SynExpr.Paren (expr = SynExpr.Typed (_, synType ,_))) ->
            getParenTypes synType
            |> List.map (fun synType -> getRangeCoords synType.Range)
        | _ -> failwith "Could not get binding")
    |> shouldEqual
        [ [ (2, 11), (2, 22) ]
          [ (3, 5), (3, 17) ]
          [ (4, 5), (4, 12); (4, 6), (4, 11) ] ]


module TypeMemberRanges =

    let getTypeMemberRange source =
        let (SynModuleOrNamespace (decls = decls)) = parseSourceCodeAndGetModule source
        match decls with
        | [ SynModuleDecl.Types ([ SynTypeDefn (_, SynTypeDefnRepr.ObjectModel (_, memberDecls, _), _, _, _) ], _) ] ->
            memberDecls |> List.map (fun memberDecl -> getRangeCoords memberDecl.Range)
        | _ -> failwith "Could not get member"

    
    [<Test>]
    let ``Member range 01 - Simple``() =
        let source = """
type T =
    member x.Foo() = ()
"""
        getTypeMemberRange source |> shouldEqual [ (3, 4), (3, 23) ]

    
    [<Test>]
    let ``Member range 02 - Static``() =
        let source = """
type T =
    static member Foo() = ()
"""
        getTypeMemberRange source |> shouldEqual [ (3, 4), (3, 28) ]


    [<Test>]
    let ``Member range 03 - Attribute``() =
        let source = """
type T =
    [<Foo>]
    static member Foo() = ()
"""
        getTypeMemberRange source |> shouldEqual [ (3, 4), (4, 28) ]


    [<Test>]
    let ``Member range 04 - Property``() =
        let source = """
type T =
    member x.P = ()
"""
        getTypeMemberRange source |> shouldEqual [ (3, 4), (3, 19) ]


    [<Test>]
    let ``Member range 05 - Setter only property``() =
        let source = """
type T =
    member x.P with set (value) = v <- value
"""
        getTypeMemberRange source |> shouldEqual [ (3, 4), (3, 44) ]

    
    [<Test>]
    let ``Member range 06 - Read-write property``() =
        let source = """
type T =
    member this.MyReadWriteProperty
        with get () = x
        and set (value) = x <- value
"""
        getTypeMemberRange source |> shouldEqual [ (3, 4), (5, 36)
                                                   (3, 4), (5, 36) ]


    [<Test>]
    let ``Member range 07 - Auto property``() =
        let source = """
type T =
    member val Property1 = ""
"""
        getTypeMemberRange source |> shouldEqual [ (3, 4), (3, 29) ]


    [<Test>]
    let ``Member range 08 - Auto property with setter``() =
        let source = """
type T =
    member val Property1 = "" with get, set
"""
        getTypeMemberRange source |> shouldEqual [ (3, 4), (3, 29) ]

    
    [<Test>]
    let ``Member range 09 - Abstract slot``() =
        let source = """
type T =
    abstract P: int
    abstract M: unit -> unit
"""
        getTypeMemberRange source |> shouldEqual [ (3, 4), (3, 19)
                                                   (4, 4), (4, 28) ]

    [<Test>]
    let ``Member range 10 - Val field``() =
        let source = """
type T =
    val x: int
"""
        getTypeMemberRange source |> shouldEqual [ (3, 4), (3, 14) ]


    [<Test>]
    let ``Member range 11 - Ctor``() =
        let source = """
type T =
    new (x:int) = ()
"""
        getTypeMemberRange source |> shouldEqual [ (3, 4), (3, 20) ]


[<Test>]
let ``TryRangeOfRefCellDereferenceContainingPos - simple``() =
    let source = """
let x = false
let y = !x
"""
    let parseFileResults, _ = getParseAndCheckResults source
    let res = parseFileResults.TryRangeOfRefCellDereferenceContainingPos (mkPos 3 9)
    match res with
    | Some res ->
        res
        |> tups
        |> fst
        |> shouldEqual (3, 8)
    | None ->
        Assert.Fail("No deref operator found in source.")

[<Test>]
let ``TryRangeOfRefCellDereferenceContainingPos - parens``() =
    let source = """
let x = false
let y = !(x)
"""
    let parseFileResults, _ = getParseAndCheckResults source
    let res = parseFileResults.TryRangeOfRefCellDereferenceContainingPos (mkPos 3 10)
    match res with
    | Some res ->
        res
        |> tups
        |> fst
        |> shouldEqual (3, 8)
    | None ->
        Assert.Fail("No deref operator found in source.")


[<Test>]
let ``TryRangeOfRefCellDereferenceContainingPos - binary expr``() =
    let source = """
let x = false
let y = !(x = false)
"""
    let parseFileResults, _ = getParseAndCheckResults source
    let res = parseFileResults.TryRangeOfRefCellDereferenceContainingPos (mkPos 3 10)
    match res with
    | Some res ->
        res
        |> tups
        |> fst
        |> shouldEqual (3, 8)
    | None ->
        Assert.Fail("No deref operator found in source.")

[<Test>]
let ``TryRangeOfRecordExpressionContainingPos - contained``() =
    let source = """
let x = { Name = "Hello" }
"""
    let parseFileResults, _ = getParseAndCheckResults source
    let res = parseFileResults.TryRangeOfRecordExpressionContainingPos (mkPos 2 10)
    match res with
    | Some res ->
        res
        |> tups
        |> shouldEqual ((2, 8), (2, 26))
    | None ->
        Assert.Fail("No range of record found in source.")

[<Test>]
let ``TryRangeOfRecordExpressionContainingPos - not contained``() =
    let source = """
let x = { Name = "Hello" }
"""
    let parseFileResults, _ = getParseAndCheckResults source
    let res = parseFileResults.TryRangeOfRecordExpressionContainingPos (mkPos 2 7)
    Assert.True(res.IsNone, "Expected not to find a range.")

module FunctionApplicationArguments =

    [<Test>]
    let ``GetAllArgumentsForFunctionApplicationAtPostion - Single arg``() =
        let source = """
let f x = ()
f 12
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.GetAllArgumentsForFunctionApplicationAtPostion (mkPos 3 0)
        match res with
        | Some res ->
            res
            |> List.map (tups >> fst)
            |> shouldEqual [(3, 2)]
        | None ->
            Assert.Fail("No arguments found in source code")

    [<Test>]
    let ``GetAllArgumentsForFunctionApplicationAtPostion - Multi arg``() =
        let source = """
let f x y z = ()
f 1 2 3
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.GetAllArgumentsForFunctionApplicationAtPostion (mkPos 3 0)
        match res with
        | Some res ->
            res
            |> List.map (tups >> fst)
            |> shouldEqual [(3, 2); (3, 4); (3, 6)]
        | None ->
            Assert.Fail("No arguments found in source code")

    [<Test>]
    let ``GetAllArgumentsForFunctionApplicationAtPostion - Multi arg parentheses``() =
        let source = """
let f x y z = ()
f (1) (2) (3)
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.GetAllArgumentsForFunctionApplicationAtPostion (mkPos 3 0)
        match res with
        | Some res ->
            res
            |> List.map (tups >> fst)
            |> shouldEqual [(3, 2); (3, 6); (3, 10)]
        | None ->
            Assert.Fail("No arguments found in source code")

    [<Test>]
    let ``GetAllArgumentsForFunctionApplicationAtPostion - Multi arg nested parentheses``() =
        let source = """
let f x y z = ()
f ((1)) (((2))) ((((3))))
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.GetAllArgumentsForFunctionApplicationAtPostion (mkPos 3 0)
        match res with
        | Some res ->
            res
            |> List.map (tups >> fst)
            |> shouldEqual [(3, 3); (3, 10); (3, 19)]
        | None ->
            Assert.Fail("No arguments found in source code")

    [<Test>]
    let ``GetAllArgumentsForFunctionApplicationAtPostion - unit``() =
        let source = """
let f () = ()
f ()
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.GetAllArgumentsForFunctionApplicationAtPostion (mkPos 3 0)
        Assert.IsTrue(res.IsNone, "Found argument for unit-accepting function, which shouldn't be the case.")

    [<Test>]
    let ``GetAllArgumentsForFunctionApplicationAtPostion - curried function``() =
        let source = """
let f x y = x + y
f 12
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.GetAllArgumentsForFunctionApplicationAtPostion (mkPos 3 0)
        match res with
        | Some res ->
            res
            |> List.map (tups >> fst)
            |> shouldEqual [(3, 2)]
        | None ->
            Assert.Fail("No arguments found in source code")

    [<Test>]
    let ``GetAllArgumentsForFunctionApplicationAtPostion - tuple value``() =
        let source = """
let f (t: int * int) = ()
let t = (1, 2)
f t
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.GetAllArgumentsForFunctionApplicationAtPostion (mkPos 4 0)
        match res with
        | Some res ->
            res
            |> List.map (tups >> fst)
            |> shouldEqual [(4, 2)]
        | None ->
            Assert.Fail("No arguments found in source code")

    [<Test>]
    let ``GetAllArgumentsForFunctionApplicationAtPostion - tuple literal``() =
        let source = """
let f (t: int * int) = ()
f (1, 2)
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.GetAllArgumentsForFunctionApplicationAtPostion (mkPos 3 0)
        match res with
        | Some res ->
            res
            |> List.map (tups >> fst)
            |> shouldEqual [(3, 3); (3, 6)]
        | None ->
            Assert.Fail("No arguments found in source code")

    [<Test>]
    let ``GetAllArgumentsForFunctionApplicationAtPostion - tuple value with definition that has explicit names``() =
        let source = """
let f ((x, y): int * int) = ()
let t = (1, 2)
f t
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.GetAllArgumentsForFunctionApplicationAtPostion (mkPos 4 0)
        match res with
        | Some res ->
            res
            |> List.map (tups >> fst)
            |> shouldEqual [(4, 2)]
        | None ->
            Assert.Fail("No arguments found in source code")

    [<Test>]
    let ``GetAllArgumentsForFunctionApplicationAtPostion - tuple literal inside parens``() =
        let source = """
let f (x, y) = ()
f ((1, 2))
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.GetAllArgumentsForFunctionApplicationAtPostion (mkPos 3 0)
        match res with
        | Some res ->
            res
            |> List.map (tups >> fst)
            |> shouldEqual [(3, 4); (3, 7)]
        | None ->
            Assert.Fail("No arguments found in source code")

    [<Test>]
    let ``GetAllArgumentsForFunctionApplicationAtPostion - tuples with elements as arguments``() =
        let source = """
let f (a, b) = ()
f (1, 2)
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.GetAllArgumentsForFunctionApplicationAtPostion (mkPos 3 0)
        match res with
        | Some res ->
            res
            |> List.map (tups >> fst)
            |> shouldEqual [(3, 3); (3, 6)]
        | None ->
            Assert.Fail("No arguments found in source code")

    [<Test>]
    let ``GetAllArgumentsForFunctionApplicationAtPostion - top-level arguments with nested function call``() =
        let source = """
let f x y = x + y
f (f 1 2) 3
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.GetAllArgumentsForFunctionApplicationAtPostion (mkPos 3 0)
        match res with
        | Some res ->
            res
            |> List.map (tups >> fst)
            |> shouldEqual [(3, 2); (3, 10)]
        | None ->
            Assert.Fail("No arguments found in source code")

    [<Test>]
    let ``GetAllArgumentsForFunctionApplicationAtPostion - nested function argument positions``() =
        let source = """
let f x y = x + y
f (f 1 2) 3
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.GetAllArgumentsForFunctionApplicationAtPostion (mkPos 3 3)
        match res with
        | Some res ->
            res
            |> List.map (tups >> fst)
            |> shouldEqual [(3, 5); (3, 7)]
        | None ->
            Assert.Fail("No arguments found in source code")

    [<Test>]
    let ``GetAllArgumentsForFunctionApplicationAtPostion - nested function application in infix expression``() =
        let source = """
let addStr x y = string x + y
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.GetAllArgumentsForFunctionApplicationAtPostion (mkPos 2 17)
        match res with
        | Some res ->
            res
            |> List.map (tups >> fst)
            |> shouldEqual [(2, 24)]
        | None ->
            Assert.Fail("No arguments found in source code")

    [<Test>]
    let ``GetAllArgumentsForFunctionApplicationAtPostion - nested function application outside of infix expression``() =
        let source = """
let addStr x y = x + string y
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.GetAllArgumentsForFunctionApplicationAtPostion (mkPos 2 21)
        match res with
        | Some res ->
            res
            |> List.map (tups >> fst)
            |> shouldEqual [(2, 28)]
        | None ->
            Assert.Fail("No arguments found in source code")

    [<Test>]
    let ``GetAllArgumentsForFunctionApplicationAtPostion - nested function applications both inside and outside of infix expression``() =
        let source = """
let addStr x y = string x + string y
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.GetAllArgumentsForFunctionApplicationAtPostion (mkPos 2 17)
        match res with
        | Some res ->
            res
            |> List.map (tups >> fst)
            |> shouldEqual [(2, 24)]
        | None ->
            Assert.Fail("No arguments found in source code")

        
        let res = parseFileResults.GetAllArgumentsForFunctionApplicationAtPostion (mkPos 2 28)
        match res with
        | Some res ->
            res
            |> List.map (tups >> fst)
            |> shouldEqual [(2, 35)]
        | None ->
            Assert.Fail("No arguments found in source code")

    [<Test>]
    let ``IsPosContainedInApplication - no``() =
        let source = """
sqrt x
12
"""
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.False(parseFileResults.IsPosContainedInApplication (mkPos 3 2), "Pos should not be in application")

    [<Test>]
    let ``IsPosContainedInApplication - yes, single arg``() =
        let source = """
sqrt x
"""
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.True(parseFileResults.IsPosContainedInApplication (mkPos 2 5), "Pos should be in application")

    [<Test>]
    let ``IsPosContainedInApplication - yes, multi arg``() =
        let source = """
let add2 x y = x + y
add2 x y
"""
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.True(parseFileResults.IsPosContainedInApplication (mkPos 3 6), "Pos should be in application")

    [<Test>]
    let ``IsPosContainedInApplication - inside computation expression - no``() =
        let source = """
async {
    sqrt
}
"""
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.False(parseFileResults.IsPosContainedInApplication (mkPos 2 5), "Pos should not be in application")

    [<Test>]
    let ``TryRangeOfFunctionOrMethodBeingApplied - inside CE return - no``() =
        let source = """
async {
    return sqrt
}
"""
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.False(parseFileResults.IsPosContainedInApplication (mkPos 2 5), "Pos should not be in application")

    [<Test>]
    let ``TryRangeOfFunctionOrMethodBeingApplied - inside CE - yes``() =
        let source = """
let myAdd x y = x + y
async {
    return myAdd 1
}
    """
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.False(parseFileResults.IsPosContainedInApplication (mkPos 3 18), "Pos should not be in application")

    [<Test>]
    let ``TryRangeOfFunctionOrMethodBeingApplied - no application``() =
        let source = """
let add2 x y = x + y
add2 x y
12
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.TryRangeOfFunctionOrMethodBeingApplied (mkPos 4 2)
        Assert.IsTrue(res.IsNone, "Not in a function application but got one")

    [<Test>]
    let ``TryRangeOfFunctionOrMethodBeingApplied - single arg application``() =
        let source = """
sqrt 12.0
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.TryRangeOfFunctionOrMethodBeingApplied (mkPos 2 9)
        match res with
        | None -> Assert.Fail("Expected 'sqrt' but got nothing")
        | Some range ->
            range
            |> tups
            |> shouldEqual ((2, 0), (2, 4))

    [<Test>]
    let ``TryRangeOfFunctionOrMethodBeingApplied - multi arg application``() =
        let source = """
let f x y z = ()
f 1 2 3
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.TryRangeOfFunctionOrMethodBeingApplied (mkPos 3 5)
        match res with
        | None -> Assert.Fail("Expected 'f' but got nothing")
        | Some range ->
            range
            |> tups
            |> shouldEqual ((3, 0), (3, 1))

    [<Test>]
    let ``TryRangeOfFunctionOrMethodBeingApplied - multi arg application but at function itself``() =
        let source = """
let f x y z = ()
f 1 2 3
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.TryRangeOfFunctionOrMethodBeingApplied (mkPos 3 1)
        match res with
        | None -> Assert.Fail("Expected 'f' but got nothing")
        | Some range ->
            range
            |> tups
            |> shouldEqual ((3, 0), (3, 1))

    [<Test>]
    let ``TryRangeOfFunctionOrMethodBeingApplied - function in pipeline``() =
        let source = """
[1..10] |> List.map id
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.TryRangeOfFunctionOrMethodBeingApplied (mkPos 2 20)
        match res with
        | None -> Assert.Fail("Expected 'List.map' but got nothing")
        | Some range ->
            range
            |> tups
            |> shouldEqual ((2, 11), (2, 19))

    [<Test>]
    let ``TryRangeOfFunctionOrMethodBeingApplied - function in middle of pipeline``() =
        let source = """
[1..10]
|> List.filter (fun x -> x > 3)
|> List.map id
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.TryRangeOfFunctionOrMethodBeingApplied (mkPos 3 14)
        match res with
        | None -> Assert.Fail("Expected 'List.filter' but got nothing")
        | Some range ->
            range
            |> tups
            |> shouldEqual ((3, 3), (3, 14))

    [<Test>]
    let ``TryRangeOfFunctionOrMethodBeingApplied - function in middle of pipeline, no qualification``() =
        let source = """
[1..10]
|> id
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.TryRangeOfFunctionOrMethodBeingApplied (mkPos 3 5)
        match res with
        | None -> Assert.Fail("Expected 'id' but got nothing")
        | Some range ->
            range
            |> tups
            |> shouldEqual ((3, 3), (3, 5))

    [<Test>]
    let ``TryRangeOfFunctionOrMethodBeingApplied - incomplete infix app``() =
        let source = """
let add2 x y = x + y
let square x = x *
add2 1 2
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.TryRangeOfFunctionOrMethodBeingApplied (mkPos 3 18)
        match res with
        | None -> Assert.Fail("Expected '*' but got nothing")
        | Some range ->
            range
            |> tups
            |> shouldEqual ((3, 17), (3, 18))

    [<Test>]
    let ``TryRangeOfFunctionOrMethodBeingApplied - inside CE``() =
        let source = """
let myAdd x y = x + y
async {
    return myAdd 1 
}
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.TryRangeOfFunctionOrMethodBeingApplied (mkPos 4 18)
        match res with
        | None -> Assert.Fail("Expected 'myAdd' but got nothing")
        | Some range ->
            range
            |> tups
            |> shouldEqual ((4, 11), (4, 16))

    [<Test>]
    let ``TryRangeOfFunctionOrMethodBeingApplied - inside lambda - binding``() =
        let source = """
let add n1 n2 = n1 + n2
let lst = [1; 2; 3]
let mapped = 
    lst |> List.map (fun n ->
        let sum = add
        n.ToString()
    )
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.TryRangeOfFunctionOrMethodBeingApplied (mkPos 6 21)
        match res with
        | None -> Assert.Fail("Expected 'add' but got nothing")
        | Some range ->
            range
            |> tups
            |> shouldEqual ((6, 18), (6, 21))

    [<Test>]
    let ``TryRangeOfFunctionOrMethodBeingApplied - inside lambda - if expression``() =
        let source = """
let add n1 n2 = n1 + n2
let lst = [1; 2; 3]
let mapped = 
    lst |> List.map (fun n ->
        if true then
            add
        else
            match add 1 2 with
            | 0 when 0 = 0 -> add 1 2
            | _ -> add 1 2
    )
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.TryRangeOfFunctionOrMethodBeingApplied (mkPos 7 15)
        match res with
        | None -> Assert.Fail("Expected 'add' but got nothing")
        | Some range ->
            range
            |> tups
            |> shouldEqual ((7, 12), (7, 15))

    [<Test>]
    let ``TryRangeOfFunctionOrMethodBeingApplied - inside lambda - match expression``() =
        let source = """
let add n1 n2 = n1 + n2
let lst = [1; 2; 3]
let mapped = 
    lst |> List.map (fun n ->
        if true then
            add
        else
            match add 1 2 with
            | 0 when 0 = 0 -> add 1 2
            | _ -> add 1 2
    )
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.TryRangeOfFunctionOrMethodBeingApplied (mkPos 7 15)
        match res with
        | None -> Assert.Fail("Expected 'add' but got nothing")
        | Some range ->
            range
            |> tups
            |> shouldEqual ((7, 12), (7, 15))

    [<Test>]
    let ``TryRangeOfFunctionOrMethodBeingApplied - inside lambda - match expr``() =
        let source = """
let add n1 n2 = n1 + n2
let lst = [1; 2; 3]
let mapped = 
    lst |> List.map (fun n ->
        if true then
            add
        else
            match add with
            | 0 when 0 = 0 -> add 1 2
            | _ -> add 1 2
    )
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.TryRangeOfFunctionOrMethodBeingApplied (mkPos 9 21)
        match res with
        | None -> Assert.Fail("Expected 'add' but got nothing")
        | Some range ->
            range
            |> tups
            |> shouldEqual ((9, 18), (9, 21))

    [<Test>]
    let ``TryRangeOfFunctionOrMethodBeingApplied - inside lambda - match case``() =
        let source = """
let add n1 n2 = n1 + n2
let lst = [1; 2; 3]
let mapped = 
    lst |> List.map (fun n ->
        if true then
            add
        else
            match add 1 2 with
            | 0 when 0 = 0 -> add 1 2
            | _ -> add
    )
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.TryRangeOfFunctionOrMethodBeingApplied (mkPos 11 22)
        match res with
        | None -> Assert.Fail("Expected 'add' but got nothing")
        | Some range ->
            range
            |> tups
            |> shouldEqual ((11, 19), (11, 22))

    [<Test>]
    let ``TryRangeOfFunctionOrMethodBeingApplied - inside method call``() =
        let source = """
type C() = static member Yeet(x, y, z) = ()
C.Yeet(1, 2, sqrt)
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.TryRangeOfFunctionOrMethodBeingApplied (mkPos 3 17)
        match res with
        | None -> Assert.Fail("Expected 'sqrt' but got nothing")
        | Some range ->
            range
            |> tups
            |> shouldEqual ((3, 13), (3, 17))

    [<Test>]
    let ``TryRangeOfFunctionOrMethodBeingApplied - inside method call - parenthesized lambda``() =
        let source = """
type C() = static member Yeet(x, y, z) = ()
C.Yeet(1, 2, (fun x -> sqrt))
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.TryRangeOfFunctionOrMethodBeingApplied (mkPos 3 27)
        match res with
        | None -> Assert.Fail("Expected 'sqrt' but got nothing")
        | Some range ->
            range
            |> tups
            |> shouldEqual ((3, 23), (3, 27))

module PipelinesAndArgs =
    [<Test>]
    let ``TryIdentOfPipelineContainingPosAndNumArgsApplied - No pipeline, no infix app``() =
        let source = """
let f x = ()
f 12
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.TryIdentOfPipelineContainingPosAndNumArgsApplied (mkPos 3 0)
        Assert.True(res.IsNone, sprintf "Got a result, did not expect one: %A" res)

    [<Test>]
    let ``TryIdentOfPipelineContainingPosAndNumArgsApplied - No pipeline, but infix app``() =
        let source = """
let square x = x * 
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.TryIdentOfPipelineContainingPosAndNumArgsApplied (mkPos 2 18)
        Assert.True(res.IsNone, sprintf "Got a result, did not expect one: %A" res)

    [<Test>]
    let ``TryIdentOfPipelineContainingPosAndNumArgsApplied - Single pipeline``() =
        let source = """
[1..10] |> List.map 
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.TryIdentOfPipelineContainingPosAndNumArgsApplied (mkPos 2 19)
        match res with
        | Some (ident, numArgs) ->
            (ident.idText, numArgs)
            |> shouldEqual ("op_PipeRight", 1)
        | None ->
            Assert.Fail("No pipeline found")
                
    [<Test>]
    let ``TryIdentOfPipelineContainingPosAndNumArgsApplied - Double pipeline``() =
        let source = """
([1..10], 1) ||> List.fold
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.TryIdentOfPipelineContainingPosAndNumArgsApplied (mkPos 2 26)
        match res with
        | Some (ident, numArgs) ->
            (ident.idText, numArgs)
            |> shouldEqual ("op_PipeRight2", 2)
        | None ->
            Assert.Fail("No pipeline found")

    [<Test>]
    let ``TryIdentOfPipelineContainingPosAndNumArgsApplied - Triple pipeline``() =
        let source = """
([1..10], [1..10], 3) |||> List.fold2
"""
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.TryIdentOfPipelineContainingPosAndNumArgsApplied (mkPos 2 37)
        match res with
        | Some (ident, numArgs) ->
            (ident.idText, numArgs)
            |> shouldEqual ("op_PipeRight3", 3)
        | None ->
            Assert.Fail("No pipeline found")

    [<Test>]
    let ``TryIdentOfPipelineContainingPosAndNumArgsApplied - none when inside lambda``() =
        let source = """
let add n1 n2 = n1 + n2
let lst = [1; 2; 3]
let mapped = 
    lst |> List.map (fun n ->
        let sum = add 1
        n.ToString()
    )
    """
        let parseFileResults, _ = getParseAndCheckResults source
        let res = parseFileResults.TryIdentOfPipelineContainingPosAndNumArgsApplied (mkPos 6 22)
        Assert.IsTrue(res.IsNone, "Inside a lambda but counted the pipeline outside of that lambda.")

[<Test>]
let ``TryRangeOfExprInYieldOrReturn - not contained``() =
    let source = """
let f x =
    x
"""
    let parseFileResults, _ = getParseAndCheckResults source
    let res = parseFileResults.TryRangeOfExprInYieldOrReturn (mkPos 3 4)
    Assert.True(res.IsNone, "Expected not to find a range.")

[<Test>]
let ``TryRangeOfExprInYieldOrReturn - contained``() =
    let source = """
let f x =
    return x
"""
    let parseFileResults, _ = getParseAndCheckResults source
    let res = parseFileResults.TryRangeOfExprInYieldOrReturn (mkPos 3 4)
    match res with
    | Some range ->
        range
        |> tups
        |> shouldEqual ((3, 11), (3, 12))
    | None ->
        Assert.Fail("Expected to get a range back, but got none.")

[<Test>]
let ``TryRangeOfParenEnclosingOpEqualsGreaterUsage - not correct operator``() =
    let source = """
let x = y |> y + 1
"""
    let parseFileResults, _ = getParseAndCheckResults source
    let res = parseFileResults.TryRangeOfParenEnclosingOpEqualsGreaterUsage (mkPos 2 8)
    Assert.True(res.IsNone, "Expected not to find any ranges.")

[<Test>]
let ``TryRangeOfParenEnclosingOpEqualsGreaterUsage - error arg pos``() =
    let source = """
let x = y => y + 1
"""
    let parseFileResults, _ = getParseAndCheckResults source
    let res = parseFileResults.TryRangeOfParenEnclosingOpEqualsGreaterUsage (mkPos 2 8)
    match res with
    | Some (overallRange, argRange, exprRange) ->
        [overallRange; argRange; exprRange]
        |> List.map tups
        |> shouldEqual [((2, 8), (2, 18)); ((2, 8), (2, 9)); ((2, 13), (2, 18))]
    | None ->
        Assert.Fail("Expected to get a range back, but got none.")

[<Test>]
let ``TryRangeOfParenEnclosingOpEqualsGreaterUsage - error expr pos``() =
    let source = """
let x = y => y + 1
"""
    let parseFileResults, _ = getParseAndCheckResults source
    let res = parseFileResults.TryRangeOfParenEnclosingOpEqualsGreaterUsage (mkPos 2 13)
    match res with
    | Some (overallRange, argRange, exprRange) ->
        [overallRange; argRange; exprRange]
        |> List.map tups
        |> shouldEqual [((2, 8), (2, 18)); ((2, 8), (2, 9)); ((2, 13), (2, 18))]
    | None ->
        Assert.Fail("Expected to get a range back, but got none.")

[<Test>]
let ``TryRangeOfParenEnclosingOpEqualsGreaterUsage - parenthesized lambda``() =
    let source = """
[1..10] |> List.map (x => x + 1)
"""
    let parseFileResults, _ = getParseAndCheckResults source
    let res = parseFileResults.TryRangeOfParenEnclosingOpEqualsGreaterUsage (mkPos 2 21)
    match res with
    | Some (overallRange, argRange, exprRange) ->
        [overallRange; argRange; exprRange]
        |> List.map tups
        |> shouldEqual [((2, 21), (2, 31)); ((2, 21), (2, 22)); ((2, 26), (2, 31))]
    | None ->
        Assert.Fail("Expected to get a range back, but got none.")

[<Test>]
let ``TryRangeOfNameOfNearestOuterBindingContainingPos - simple``() =
    let source = """
let x = nameof x
"""
    let parseFileResults, _ = getParseAndCheckResults source
    let res = parseFileResults.TryRangeOfNameOfNearestOuterBindingContainingPos (mkPos 2 15)
    match res with
    | Some range ->
        range
        |> tups
        |> shouldEqual ((2, 4), (2, 5))
    | None ->
        Assert.Fail("Expected to get a range back, but got none.")

[<Test>]
let ``TryRangeOfNameOfNearestOuterBindingContainingPos - inside match``() =
    let source = """
let mySum xs acc =
    match xs with
    | [] -> acc
    | _ :: tail ->
        mySum tail (acc + 1)
"""
    let parseFileResults, _ = getParseAndCheckResults source
    let res = parseFileResults.TryRangeOfNameOfNearestOuterBindingContainingPos (mkPos 6 8)
    match res with
    | Some range ->
        range
        |> tups
        |> shouldEqual ((2, 4), (2, 9))
    | None ->
        Assert.Fail("Expected to get a range back, but got none.")

[<Test>]
let ``TryRangeOfNameOfNearestOuterBindingContainingPos - nested binding``() =
    let source = """
let f x =
    let z = 12
    let h x = 
        h x
    g x
"""
    let parseFileResults, _ = getParseAndCheckResults source
    let res = parseFileResults.TryRangeOfNameOfNearestOuterBindingContainingPos (mkPos 5 8)
    match res with
    | Some range ->
        range
        |> tups
        |> shouldEqual ((4, 8), (4, 9))
    | None ->
        Assert.Fail("Expected to get a range back, but got none.")

[<Test>]
let ``TryRangeOfNameOfNearestOuterBindingContainingPos - nested and after other statements``() =
    let source = """
let f x =
    printfn "doot doot"
    printfn "toot toot"
    let z = 12
    let h x = 
        h x
    g x
"""
    let parseFileResults, _ = getParseAndCheckResults source
    let res = parseFileResults.TryRangeOfNameOfNearestOuterBindingContainingPos (mkPos 7 8)
    match res with
    | Some range ->
        range
        |> tups
        |> shouldEqual ((6, 8), (6, 9))
    | None ->
        Assert.Fail("Expected to get a range back, but got none.")
