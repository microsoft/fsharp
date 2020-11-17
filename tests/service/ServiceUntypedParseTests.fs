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
open FSharp.Compiler.Range
open FSharp.Compiler.SourceCodeServices
open FSharp.Compiler.Service.Tests.Common
open FSharp.Compiler.SyntaxTree
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
        match parseSourceCode("C:\\test.fs", source) with
        | None -> failwith "No parse tree"
        | Some parseTree ->
            let actual = UntypedParseImpl.TryGetCompletionContext(markerPos, parseTree, lines.[Line.toZ markerPos.Line])
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
        | SynModuleDecl.Let (_, [Binding (attributes = attributeLists)], _) ->
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
        | [ SynModuleDecl.Types ([ TypeDefn (_, SynTypeDefnRepr.ObjectModel (_, memberDecls, _), _, _) ], _) ] ->
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

module TypeAnnotations =
    [<Test>]
    let ``IsTypeAnnotationGivenAtPosition - function - no annotation``() =
        let source = """
let f x = ()
"""
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsFalse(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 2 6), "Expected no annotation for argument 'x'")

    [<Test>]
    let ``IsTypeAnnotationGivenAtPosition - function - single arg annotation``() =
        let source = """
let f (x: int) = ()
"""
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsTrue(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 2 7), "Expected annotation for argument 'x'")

    [<Test>]
    let ``IsTypeAnnotationGivenAtPosition - function - first arg annotated``() =
        let source = """
let f (x: int) y = ()
"""
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsTrue(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 2 7), "Expected annotation for argument 'x'")
        Assert.IsFalse(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 2 15), "Expected no annotation for argument 'x'")

    [<Test>]
    let ``IsTypeAnnotationGivenAtPosition - function - second arg annotated``() =
        let source = """
let f x (y: string) = ()
"""
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.False(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 2 7), "Expected no annotation for argument 'x'")
        Assert.IsTrue(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 2 9), "Expected annotation for argument 'y'")

    [<Test>]
    let ``IsTypeAnnotationGivenAtPosition - function - all args annotated``() =
        let source = """
let f (x: int) (y: string) = ()
"""
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsTrue(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 2 7), "Expected annotation for argument 'x'")
        Assert.IsTrue(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 2 16), "Expected annotation for argument 'y'")

    [<Test>]
    let ``IsTypeAnnotationGivenAtPosition - lambda function - all args annotated``() =
        let source = """
let f = fun (x: int) (y: string) -> ()
"""
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsTrue(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 2 13), "Expected a annotation for argument 'x'")
        Assert.IsTrue(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 2 22), "Expected a annotation for argument 'y'")

    [<Test>]
    let ``IsTypeAnnotationGivenAtPosition - constuctor - arg no annotations``() =
        let source = """
type C(x) = class end
"""
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsFalse(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 2 7), "Expected no annotation for argument 'x'")

    [<Test>]
    let ``IsTypeAnnotationGivenAtPosition - constuctor - first arg unannotated``() =
        let source = """
type C(x, y: string) = class end
"""
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsFalse(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 2 7), "Expected no annotation for argument 'x'")
        Assert.IsTrue(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 2 10), "Expected annotation for argument 'y'")

    [<Test>]
    let ``IsTypeAnnotationGivenAtPosition - constuctor - second arg unannotated``() =
        let source = """
type C(x: int, y) = class end
    """
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsTrue(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 2 7), "Expected annotation for argument 'x'")
        Assert.IsFalse(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 2 15), "Expected no annotation for argument 'y'")

    [<Test>]
    let ``IsTypeAnnotationGivenAtPosition - constuctor - both args annotated``() =
        let source = """
type C(x: int, y: int) = class end
    """
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsTrue(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 2 7), "Expected annotation for argument 'x'")
        Assert.IsTrue(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 2 15), "Expected annotation for argument 'y'")

    [<Test>]
    let ``IsTypeAnnotationGivenAtPosition - method - args no unannotions``() =
        let source = """
type C() =
    member _.M(x, y) = ()
"""
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsFalse(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 3 15), "Expected no annotation for argument 'x'")
        Assert.IsFalse(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 3 18), "Expected no annotation for argument 'y'")

    [<Test>]
    let ``IsTypeAnnotationGivenAtPosition - method - first arg annotated``() =
        let source = """
type C() =
    member _.M(x: int, y) = ()
    """
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsTrue(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 3 15), "Expected annotation for argument 'x'")
        Assert.IsFalse(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 3 23), "Expected no annotation for argument 'y'")

    [<Test>]
    let ``IsTypeAnnotationGivenAtPosition - method - second arg annotated``() =
        let source = """
type C() =
    member _.M(x, y: int) = ()
    """
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsFalse(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 3 15), "Expected no annotation for argument 'x'")
        Assert.IsTrue(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 3 18), "Expected annotation for argument 'y'")

    [<Test>]
    let ``IsTypeAnnotationGivenAtPosition - method - both args annotated``() =
        let source = """
type C() =
    member _.M(x: int, y: string) = ()
"""
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsTrue(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 3 15), "Expected annotation for argument 'x'")
        Assert.IsTrue(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 3 23), "Expected annotation for argument 'y'")

    [<Test>]
    let ``IsTypeAnnotationGivenAtPosition - method currying - args no unannotions``() =
        let source = """
type C() =
    member _.M x y = ()
"""
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsFalse(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 3 15), "Expected no annotation for argument 'x'")
        Assert.IsFalse(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 3 17), "Expected no annotation for argument 'y'")

    [<Test>]
    let ``IsTypeAnnotationGivenAtPosition - method currying - first arg annotated``() =
        let source = """
type C() =
    member _.M (x: int) y = ()
    """
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsTrue(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 3 16), "Expected annotation for argument 'x'")
        Assert.IsFalse(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 3 24), "Expected no annotation for argument 'y'")

    [<Test>]
    let ``IsTypeAnnotationGivenAtPosition - method currying - second arg annotated``() =
        let source = """
type C() =
    member _.M x (y: int) = ()
    """
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsFalse(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 3 16), "Expected no annotation for argument 'x'")
        Assert.IsTrue(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 3 18), "Expected annotation for argument 'y'")

    [<Test>]
    let ``IsTypeAnnotationGivenAtPosition - method currying - both args annotated``() =
        let source = """
type C() =
    member _.M (x: int) (y: string) = ()
"""
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsTrue(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 3 16), "Expected annotation for argument 'x'")
        Assert.IsTrue(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 3 25), "Expected annotation for argument 'y'")

    [<Test>]
    let ``IsTypeAnnotationGivenAtPosition - method - only return type annotated``() =
        let source = """
type C() =
    member _.M(x): string = "hello" + x
"""
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsFalse(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 3 15), "Expected no annotation for argument 'x'")

    [<Test>]
    let ``IsTypeAnnotationGivenAtPosition - tuple - no annotations``() =
        let source = """
let (x, y) = (12, "hello")
"""
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsFalse(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 2 5), "Expected no annotation for value 'x'")
        Assert.IsFalse(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 2 8), "Expected no annotation for value 'y'")

    [<Test>]
    let ``IsTypeAnnotationGivenAtPosition - tuple - first value annotated``() =
        let source = """
let (x: int, y) = (12, "hello")
"""
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsTrue(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 2 5), "Expected annotation for argument 'x'")
        Assert.IsFalse(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 2 13), "Expected no annotation for argument 'y'")

    [<Test>]
    let ``IsTypeAnnotationGivenAtPosition - tuple - second value annotated``() =
        let source = """
let (x, y: string) = (12, "hello")
"""
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsFalse(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 2 5), "Expected no annotation for argument 'x'")
        Assert.IsTrue(parseFileResults.IsTypeAnnotationGivenAtPosition (mkPos 2 8), "Expected annotation for argument 'y'")

module LambdaRecognition =
    [<Test>]
    let ``IsBindingALambdaAtPosition - recognize a lambda``() =
        let source = """
let f = fun x y -> x + y
    """
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsTrue(parseFileResults.IsBindingALambdaAtPosition (mkPos 2 4), "Expected 'f' to be a lambda expression")

    [<Test>]
    let ``IsBindingALambdaAtPosition - recognize a nested lambda``() =
        let source = """
let f =
    fun x ->
        fun y ->
            x + y
    """
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsTrue(parseFileResults.IsBindingALambdaAtPosition (mkPos 2 4), "Expected 'f' to be a lambda expression")

    [<Test>]
    let ``IsBindingALambdaAtPosition - recognize a "partial" lambda``() =
        let source = """
let f x =
    fun y ->
        x + y
    """
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsTrue(parseFileResults.IsBindingALambdaAtPosition (mkPos 2 4), "Expected 'f' to be a lambda expression")

    [<Test>]
    let ``IsBindingALambdaAtPosition - not a lambda``() =
        let source = """
let f x y = x + y
    """
        let parseFileResults, _ = getParseAndCheckResults source
        Assert.IsFalse(parseFileResults.IsBindingALambdaAtPosition (mkPos 2 4), "'f' is not a lambda expression'")