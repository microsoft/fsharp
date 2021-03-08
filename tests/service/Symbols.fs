#if INTERACTIVE
#r "../../artifacts/bin/fcs/net461/FSharp.Compiler.Service.dll" // note, build FSharp.Compiler.Service.Tests.fsproj to generate this, this DLL has a public API so can be used from F# Interactive
#r "../../artifacts/bin/fcs/net461/nunit.framework.dll"
#load "FsUnit.fs"
#load "Common.fs"
#else
module Tests.Service.Symbols
#endif

open FSharp.Compiler.Service.Tests.Common
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Symbols
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text
open FsUnit
open NUnit.Framework

module ActivePatterns =

    let completePatternInput = """
let (|True|False|) = function
    | true -> True
    | false -> False

match true with
| True | False -> ()
"""

    let partialPatternInput = """
let (|String|_|) = function
    | :? String -> Some ()
    | _ -> None

match "foo" with
| String
| _ -> ()
"""

    let getCaseUsages source line =
         let fileName, options = mkTestFileAndOptions source [| |]
         let _, checkResults = parseAndCheckFile fileName source options
          
         checkResults.GetAllUsesOfAllSymbolsInFile()
         |> Array.ofSeq
         |> Array.filter (fun su -> su.Range.StartLine = line && su.Symbol :? FSharpActivePatternCase)
         |> Array.map (fun su -> su.Symbol :?> FSharpActivePatternCase)

    [<Test>]
    let ``Active pattern case indices`` () =
        let getIndices = Array.map (fun (case: FSharpActivePatternCase) -> case.Index)

        getCaseUsages completePatternInput 7 |> getIndices |> shouldEqual [| 0; 1 |]
        getCaseUsages partialPatternInput 7 |> getIndices |> shouldEqual [| 0 |]

    [<Test>]
    let ``Active pattern group names`` () =
        let getGroupName (case: FSharpActivePatternCase) = case.Group.Name.Value

        getCaseUsages completePatternInput 7 |> Array.head |> getGroupName |> shouldEqual "|True|False|"
        getCaseUsages partialPatternInput 7 |> Array.head |> getGroupName |> shouldEqual "|String|_|"


module ExternDeclarations =
    [<Test>]
    let ``Access modifier`` () =
        let parseResults, checkResults = getParseAndCheckResults """
extern int a()
extern int public b()
extern int private c()
"""
        let (SynModuleOrNamespace (decls = decls)) = getSingleModuleLikeDecl parseResults.ParseTree

        [ None
          Some SynAccess.Public
          Some SynAccess.Private ]
        |> List.zip decls
        |> List.iter (fun (actual, expected) ->
            match actual with
            | SynModuleDecl.Let (_, [SynBinding (accessibility = access)], _) -> access |> should equal expected
            | decl -> failwithf "unexpected decl: %O" decl)

        [ "a", (true, false, false, false)
          "b", (true, false, false, false)
          "c", (false, false, false, true) ]
        |> List.iter (fun (name, expected) ->
            match findSymbolByName name checkResults with
            | :? FSharpMemberOrFunctionOrValue as mfv ->
                let access = mfv.Accessibility
                (access.IsPublic, access.IsProtected, access.IsInternal, access.IsPrivate)
                |> should equal expected
            | _ -> failwithf "Couldn't get mfv: %s" name)


module XmlDocSig =

    [<Test>]
    let ``XmlDocSig of modules in namespace`` () =
        let source = """
namespace Ns1
module Mod1 =
    let val1 = 1
    module Mod2 =
       let func2 () = ()
"""
        let fileName, options = mkTestFileAndOptions source [| |]
        let _, checkResults = parseAndCheckFile fileName source options  

        let mod1 = checkResults.PartialAssemblySignature.FindEntityByPath ["Ns1"; "Mod1"] |> Option.get
        let mod2 = checkResults.PartialAssemblySignature.FindEntityByPath ["Ns1"; "Mod1"; "Mod2"] |> Option.get
        let mod1val1 = mod1.MembersFunctionsAndValues |> Seq.find (fun m -> m.DisplayName = "val1")
        let mod2func2 = mod2.MembersFunctionsAndValues |> Seq.find (fun m -> m.DisplayName = "func2")
        mod1.XmlDocSig |> shouldEqual "T:Ns1.Mod1"
        mod2.XmlDocSig |> shouldEqual "T:Ns1.Mod1.Mod2"
        mod1val1.XmlDocSig |> shouldEqual "P:Ns1.Mod1.val1"
        mod2func2.XmlDocSig |> shouldEqual "M:Ns1.Mod1.Mod2.func2"

    [<Test>]
    let ``XmlDocSig of modules`` () =
         let source = """
module Mod1 
let val1 = 1
module Mod2 =
    let func2 () = ()
"""
         let fileName, options = mkTestFileAndOptions source [| |]
         let _, checkResults = parseAndCheckFile fileName source options  

         let mod1 = checkResults.PartialAssemblySignature.FindEntityByPath ["Mod1"] |> Option.get
         let mod2 = checkResults.PartialAssemblySignature.FindEntityByPath ["Mod1"; "Mod2"] |> Option.get
         let mod1val1 = mod1.MembersFunctionsAndValues |> Seq.find (fun m -> m.DisplayName = "val1")
         let mod2func2 = mod2.MembersFunctionsAndValues |> Seq.find (fun m -> m.DisplayName = "func2")
         mod1.XmlDocSig |> shouldEqual "T:Mod1"
         mod2.XmlDocSig |> shouldEqual "T:Mod1.Mod2"
         mod1val1.XmlDocSig |> shouldEqual "P:Mod1.val1"
         mod2func2.XmlDocSig |> shouldEqual "M:Mod1.Mod2.func2"


module Attributes =
    [<Test>]
    let ``Emit conditional attributes`` () =
        let source = """
open System
open System.Diagnostics

[<Conditional("Bar")>]
type FooAttribute() =
    inherit Attribute()

[<Foo>]
let x = 123
"""
        let fileName, options = mkTestFileAndOptions source [| "--noconditionalerasure" |]
        let _, checkResults = parseAndCheckFile fileName source options

        checkResults.GetAllUsesOfAllSymbolsInFile()
        |> Array.ofSeq
        |> Array.tryFind (fun su -> su.Symbol.DisplayName = "x")
        |> Option.orElseWith (fun _ -> failwith "Could not get symbol")
        |> Option.map (fun su -> su.Symbol :?> FSharpMemberOrFunctionOrValue)
        |> Option.iter (fun symbol -> symbol.Attributes.Count |> shouldEqual 1)

module Types =
    [<Test>]
    let ``FSharpType.Print parent namespace qualifiers`` () =
        let _, checkResults = getParseAndCheckResults """
namespace Ns1.Ns2
type T() = class end
type A = T

namespace Ns1.Ns3
type B = Ns1.Ns2.T

namespace Ns1.Ns4
open Ns1.Ns2
type C = Ns1.Ns2.T

namespace Ns1.Ns5
open Ns1
type D = Ns1.Ns2.T

namespace Ns1.Ns2.Ns6
type E = Ns1.Ns2.T
"""
        [| "A", "T"
           "B", "Ns1.Ns2.T"
           "C", "T"
           "D", "Ns2.T"
           "E", "Ns1.Ns2.T" |]
        |> Array.iter (fun (symbolName, expectedPrintedType) ->
            let symbolUse = findSymbolUseByName symbolName checkResults
            match symbolUse.Symbol with
            | :? FSharpEntity as entity ->
                entity.AbbreviatedType.Format(symbolUse.DisplayContext)
                |> should equal expectedPrintedType

            | _ -> failwithf "Couldn't get entity: %s" symbolName)

    [<Test>]
    let ``FSharpType.Format can use prefix representations`` () =
            let _, checkResults = getParseAndCheckResults """
type 't folks =
| Nil
| Cons of 't * 't folks

let tester: int folks = Cons(1, Nil)
"""
            let prefixForm = "folks<int>"
            let entity = "tester"
            let symbolUse = findSymbolUseByName entity checkResults
            match symbolUse.Symbol with
            | :? FSharpMemberOrFunctionOrValue as v ->
                    v.FullType.Format (symbolUse.DisplayContext.WithPrefixGenericParameters())
                    |> should equal prefixForm
            | _ -> failwithf "Couldn't get member: %s" entity

    [<Test>]
    let ``FSharpType.Format can use suffix representations`` () =
            let _, checkResults = getParseAndCheckResults """
type Folks<'t> =
| Nil
| Cons of 't * Folks<'t>

let tester: Folks<int> = Cons(1, Nil)
"""
            let suffixForm = "int Folks"
            let entity = "tester"
            let symbolUse = findSymbolUseByName entity checkResults
            match symbolUse.Symbol with
            | :? FSharpMemberOrFunctionOrValue as v ->
                    v.FullType.Format (symbolUse.DisplayContext.WithSuffixGenericParameters())
                    |> should equal suffixForm
            | _ -> failwithf "Couldn't get member: %s" entity

    [<Test>]
    let ``FSharpType.Format defaults to derived suffix representations`` () =
            let _, checkResults = getParseAndCheckResults """
type Folks<'t> =
| Nil
| Cons of 't * Folks<'t>

type 't Group = 't list

let tester: Folks<int> = Cons(1, Nil)

let tester2: int Group = []
"""
            let cases =
                ["tester", "Folks<int>"
                 "tester2", "int Group"]
            cases
            |> List.iter (fun (entityName, expectedTypeFormat) ->
                let symbolUse = findSymbolUseByName entityName checkResults
                match symbolUse.Symbol with
                | :? FSharpMemberOrFunctionOrValue as v ->
                        v.FullType.Format (symbolUse.DisplayContext)
                        |> should equal expectedTypeFormat
                | _ -> failwithf "Couldn't get member: %s" entityName
            )

module SyntaxExpressions =
    [<Test>]
    let ``SynExpr.Do contains the range of the do keyword`` () =
        let ast = """let a =
    do
        foobar
    do!
        foobarBang
"""
                        |> getParseResults

        match ast with
        | ParsedInput.ImplFile(ParsedImplFileInput(modules = [
                    SynModuleOrNamespace.SynModuleOrNamespace(decls = [
                        SynModuleDecl.Let(bindings = [
                            SynBinding(expr = SynExpr.Sequential(expr1 = SynExpr.Do(_, doRange) ; expr2 = SynExpr.DoBang(_, doBangRange)))
                        ])
                    ])
                ])) ->
            assertRange (2, 4) (3, 14) doRange
            assertRange (4, 4) (5, 18) doBangRange
        | _ ->
            failwith "Could not find SynExpr.Do"

module Strings =
    let getBindingExpressionValue (parseResults: ParsedInput) =
        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = modules)) ->
            modules |> List.tryPick (fun (SynModuleOrNamespace (decls = decls)) ->
                decls |> List.tryPick (fun decl ->
                    match decl with
                    | SynModuleDecl.Let (bindings = bindings) ->
                        bindings |> List.tryPick (fun binding ->
                            match binding with
                            | SynBinding.SynBinding (_,_,_,_,_,_,_,SynPat.Named _,_,e,_,_) -> Some e
                            | _ -> None)
                    | _ -> None))
        | _ -> None

    let getBindingConstValue parseResults =
        match getBindingExpressionValue parseResults with
        | Some (SynExpr.Const(c,_)) -> Some c
        | _ -> None
    
    [<Test>]
    let ``SynConst.String with SynStringKind.Regular`` () =
        let parseResults =
            getParseResults
                """
 let s = "yo"
 """

        match getBindingConstValue parseResults with
        | Some (SynConst.String (_,  kind, _)) -> kind |> should equal SynStringKind.Regular
        | _ -> failwithf "Couldn't find const"

    [<Test>]
    let ``SynConst.String with SynStringKind.Verbatim`` () =
        let parseResults =
            getParseResults
                """
 let s = @"yo"
 """

        match getBindingConstValue parseResults with
        | Some (SynConst.String (_,  kind, _)) -> kind |> should equal SynStringKind.Verbatim
        | _ -> failwithf "Couldn't find const"

    [<Test>]
    let ``SynConst.String with SynStringKind.TripleQuote`` () =
        let parseResults =
            getParseResults
                "
 let s = \"\"\"yo\"\"\"
 "

        match getBindingConstValue parseResults with
        | Some (SynConst.String (_,  kind, _)) -> kind |> should equal SynStringKind.TripleQuote
        | _ -> failwithf "Couldn't find const"

    [<Test>]
    let ``SynConst.Bytes with SynByteStringKind.Regular`` () =
        let parseResults =
            getParseResults
                """
let bytes = "yo"B
 """

        match getBindingConstValue parseResults with
        | Some (SynConst.Bytes (_,  kind, _)) -> kind |> should equal SynByteStringKind.Regular
        | _ -> failwithf "Couldn't find const"

    [<Test>]
    let ``SynConst.Bytes with SynByteStringKind.Verbatim`` () =
        let parseResults =
            getParseResults
                """
let bytes = @"yo"B
 """

        match getBindingConstValue parseResults with
        | Some (SynConst.Bytes (_,  kind, _)) -> kind |> should equal SynByteStringKind.Verbatim
        | _ -> failwithf "Couldn't find const"

    [<Test>]
    let ``SynExpr.InterpolatedString with SynStringKind.TripleQuote`` () =
        let parseResults =
            getParseResults
                "
 let s = $\"\"\"yo {42}\"\"\"
 "

        match getBindingExpressionValue parseResults with
        | Some (SynExpr.InterpolatedString(_,  kind, _)) -> kind |> should equal SynStringKind.TripleQuote
        | _ -> failwithf "Couldn't find const"

    [<Test>]
    let ``SynExpr.InterpolatedString with SynStringKind.Regular`` () =
        let parseResults =
            getParseResults
                """
 let s = $"yo {42}"
 """

        match getBindingExpressionValue parseResults with
        | Some (SynExpr.InterpolatedString(_,  kind, _)) -> kind |> should equal SynStringKind.Regular
        | _ -> failwithf "Couldn't find const"

module SynModuleOrNamespace =
    [<Test>]
    let ``DeclaredNamespace range should start at namespace keyword`` () =
        let parseResults = 
            getParseResults
                """namespace TypeEquality

/// A type for witnessing type equality between 'a and 'b
type Teq<'a, 'b>
"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(kind = SynModuleOrNamespaceKind.DeclaredNamespace; range = r) ])) ->
            assertRange (1, 0) (4, 8) r
        | _ -> failwith "Could not get valid AST"
        
    [<Test>]
    let ``Multiple DeclaredNamespaces should have a range that starts at the namespace keyword`` () =
        let parseResults = 
            getParseResults
                """namespace TypeEquality

/// A type for witnessing type equality between 'a and 'b
type Teq = class end

namespace Foobar

let x = 42
"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [
            SynModuleOrNamespace.SynModuleOrNamespace(kind = SynModuleOrNamespaceKind.DeclaredNamespace; range = r1)
            SynModuleOrNamespace.SynModuleOrNamespace(kind = SynModuleOrNamespaceKind.DeclaredNamespace; range = r2) ])) ->
            assertRange (1, 0) (4, 20) r1
            assertRange (6, 0) (8, 10) r2
        | _ -> failwith "Could not get valid AST"        