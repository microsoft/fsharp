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
            | decl -> Assert.Fail (sprintf "unexpected decl: %O" decl))

        [ "a", (true, false, false, false)
          "b", (true, false, false, false)
          "c", (false, false, false, true) ]
        |> List.iter (fun (name, expected) ->
            match findSymbolByName name checkResults with
            | :? FSharpMemberOrFunctionOrValue as mfv ->
                let access = mfv.Accessibility
                (access.IsPublic, access.IsProtected, access.IsInternal, access.IsPrivate)
                |> should equal expected
            | _ -> Assert.Fail (sprintf "Couldn't get mfv: %s" name))

    [<Test>]
    let ``Range of attribute should be included in SynDecl.Let and SynBinding`` () =
        let parseResults =
            getParseResults
                """
[<DllImport("oleacc.dll")>]
extern int AccessibleChildren()"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.Let(false, [ SynBinding(range = mb) ] , ml)
        ]) ])) ->
            assertRange (2, 0) (3, 31) ml
            assertRange (2, 0) (3, 31) mb
        | _ -> Assert.Fail "Could not get valid AST"

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

            | _ -> Assert.Fail (sprintf "Couldn't get entity: %s" symbolName))

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
            | _ -> Assert.Fail (sprintf "Couldn't get member: %s" entity)

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
            | _ -> Assert.Fail (sprintf "Couldn't get member: %s" entity)

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
                | _ -> Assert.Fail (sprintf "Couldn't get member: %s" entityName)
            )

    [<Test>]
    let ``Single SynEnumCase contains range of constant`` () =
        let parseResults = 
            getParseResults
                """
type Foo = One = 0x00000001
"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.Types(typeDefns = [
                SynTypeDefn.SynTypeDefn(typeRepr =
                    SynTypeDefnRepr.Simple(simpleRepr = SynTypeDefnSimpleRepr.Enum(cases = [ SynEnumCase.SynEnumCase(valueRange = r) ])))])
        ]) ])) ->
            assertRange (2, 17) (2, 27) r
        | _ -> Assert.Fail "Could not get valid AST"
        
    [<Test>]
    let ``Multiple SynEnumCase contains range of constant`` () =
        let parseResults = 
            getParseResults
                """
type Foo =
    | One =  0x00000001
    | Two = 2
"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.Types(typeDefns = [
                SynTypeDefn.SynTypeDefn(typeRepr =
                    SynTypeDefnRepr.Simple(simpleRepr = SynTypeDefnSimpleRepr.Enum(cases = [ SynEnumCase.SynEnumCase(valueRange = r1)
                                                                                             SynEnumCase.SynEnumCase(valueRange = r2) ])))])
        ]) ])) ->
            assertRange (3, 13) (3, 23) r1
            assertRange (4, 12) (4, 13) r2
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Range of attribute should be included in SynTypeDefn`` () =
        let parseResults = 
            getParseResults
                """
[<Foo>]
type Bar =
    class
    end"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.Types(typeDefns = [t]) as types
        ]) ])) ->
            assertRange (2, 0) (5, 7) types.Range
            assertRange (2, 0) (5, 7) t.Range
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Range of attributes should be included in recursive types`` () =
        let parseResults = 
            getParseResults
                """
[<NoEquality ; NoComparison>]
type Foo<'context, 'a> =
    | Apply of ApplyCrate<'context, 'a>

and [<CustomEquality ; NoComparison>] Bar<'context, 'a> =
    internal {
        Hash : int
        Foo : Foo<'a, 'b>
    }"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.Types(typeDefns = [t1;t2]) as types
        ]) ])) ->
            assertRange (2, 0) (10, 5) types.Range
            assertRange (2, 0) (4, 39) t1.Range
            assertRange (6, 4) (10, 5) t2.Range
        | _ -> Assert.Fail "Could not get valid AST"

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
            Assert.Fail "Could not find SynExpr.Do"

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
                            | SynBinding.SynBinding (_,_,_,_,_,_,_,(SynPat.Named _|SynPat.As(_,SynPat.Named _,_)),_,e,_,_) -> Some e
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
        | _ -> Assert.Fail "Couldn't find const"

    [<Test>]
    let ``SynConst.String with SynStringKind.Verbatim`` () =
        let parseResults =
            getParseResults
                """
 let s = @"yo"
 """

        match getBindingConstValue parseResults with
        | Some (SynConst.String (_,  kind, _)) -> kind |> should equal SynStringKind.Verbatim
        | _ -> Assert.Fail "Couldn't find const"

    [<Test>]
    let ``SynConst.String with SynStringKind.TripleQuote`` () =
        let parseResults =
            getParseResults
                "
 let s = \"\"\"yo\"\"\"
 "

        match getBindingConstValue parseResults with
        | Some (SynConst.String (_,  kind, _)) -> kind |> should equal SynStringKind.TripleQuote
        | _ -> Assert.Fail "Couldn't find const"

    [<Test>]
    let ``SynConst.Bytes with SynByteStringKind.Regular`` () =
        let parseResults =
            getParseResults
                """
let bytes = "yo"B
 """

        match getBindingConstValue parseResults with
        | Some (SynConst.Bytes (_,  kind, _)) -> kind |> should equal SynByteStringKind.Regular
        | _ -> Assert.Fail "Couldn't find const"

    [<Test>]
    let ``SynConst.Bytes with SynByteStringKind.Verbatim`` () =
        let parseResults =
            getParseResults
                """
let bytes = @"yo"B
 """

        match getBindingConstValue parseResults with
        | Some (SynConst.Bytes (_,  kind, _)) -> kind |> should equal SynByteStringKind.Verbatim
        | _ -> Assert.Fail "Couldn't find const"

    [<Test>]
    let ``SynExpr.InterpolatedString with SynStringKind.TripleQuote`` () =
        let parseResults =
            getParseResults
                "
 let s = $\"\"\"yo {42}\"\"\"
 "

        match getBindingExpressionValue parseResults with
        | Some (SynExpr.InterpolatedString(_,  kind, _)) -> kind |> should equal SynStringKind.TripleQuote
        | _ -> Assert.Fail "Couldn't find const"

    [<Test>]
    let ``SynExpr.InterpolatedString with SynStringKind.Regular`` () =
        let parseResults =
            getParseResults
                """
 let s = $"yo {42}"
 """

        match getBindingExpressionValue parseResults with
        | Some (SynExpr.InterpolatedString(_,  kind, _)) -> kind |> should equal SynStringKind.Regular
        | _ -> Assert.Fail "Couldn't find const"

    [<Test>]
    let ``SynExpr.InterpolatedString with SynStringKind.Verbatim`` () =
        let parseResults =
            getParseResults
                """
 let s = $@"Migrate notes of file ""{oldId}"" to new file ""{newId}""."
 """

        match getBindingExpressionValue parseResults with
        | Some (SynExpr.InterpolatedString(_,  kind, _)) -> kind |> should equal SynStringKind.Verbatim
        | _ -> Assert.Fail "Couldn't find const"

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
        | _ -> Assert.Fail "Could not get valid AST"
        
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
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``GlobalNamespace should start at namespace keyword`` () =
        let parseResults = 
            getParseResults
                """// foo
// bar
namespace  global

type X = int
"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [
            SynModuleOrNamespace.SynModuleOrNamespace(kind = SynModuleOrNamespaceKind.GlobalNamespace; range = r) ])) ->
            assertRange (3, 0) (5, 12) r
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Module range should start at first attribute`` () =
        let parseResults = 
            getParseResults
                """
[<  Foo  >]
module Bar

let s : string = "s"
"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [
            SynModuleOrNamespace.SynModuleOrNamespace(kind = SynModuleOrNamespaceKind.NamedModule; range = r) ])) ->
            assertRange (2, 0) (5, 20) r
        | _ -> Assert.Fail "Could not get valid AST"

module SynConsts =
    [<Test>]
    let ``Measure contains the range of the constant`` () =
        let parseResults = 
            getParseResults
                """
let n = 1.0m<cm>
let m = 7.000<cm>
"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.Let(bindings = [ SynBinding.SynBinding(expr = SynExpr.Const(SynConst.Measure(constantRange = r1), _)) ])
            SynModuleDecl.Let(bindings = [ SynBinding.SynBinding(expr = SynExpr.Const(SynConst.Measure(constantRange = r2), _)) ])
        ]) ])) ->
            assertRange (2, 8) (2, 12) r1
            assertRange (3, 8) (3, 13) r2
        | _ -> Assert.Fail "Could not get valid AST"

module SynModuleOrNamespaceSig =
    [<Test>]
    let ``Range member returns range of SynModuleOrNamespaceSig`` () =
        let parseResults =
            getParseResultsOfSignatureFile
                """
namespace Foobar

type Bar = | Bar of string * int
"""

        match parseResults with
        | ParsedInput.SigFile(ParsedSigFileInput(modules = [
            SynModuleOrNamespaceSig(kind = SynModuleOrNamespaceKind.DeclaredNamespace) as singleModule
        ])) ->
            assertRange (2,0) (4,32) singleModule.Range
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``GlobalNamespace should start at namespace keyword`` () =
        let parseResults = 
            getParseResultsOfSignatureFile
                """// foo
// bar
namespace  global

type Bar = | Bar of string * int
"""

        match parseResults with
        | ParsedInput.SigFile (ParsedSigFileInput (modules = [
            SynModuleOrNamespaceSig(kind = SynModuleOrNamespaceKind.GlobalNamespace; range = r) ])) ->
            assertRange (3, 0) (5, 32) r
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Module range should start at first attribute`` () =
        let parseResults = 
            getParseResultsOfSignatureFile
                """
 [<  Foo  >]
module Bar

val s : string
"""

        match parseResults with
        | ParsedInput.SigFile (ParsedSigFileInput (modules = [
            SynModuleOrNamespaceSig.SynModuleOrNamespaceSig(kind = SynModuleOrNamespaceKind.NamedModule; range = r) ])) ->
            assertRange (2, 1) (5, 14) r
        | _ -> Assert.Fail "Could not get valid AST"

module SignatureTypes =
    [<Test>]
    let ``Range of Type should end at end keyword`` () =
        let parseResults = 
            getParseResultsOfSignatureFile
                """namespace GreatProjectThing

type Meh =
        class
        end


// foo"""

        match parseResults with
        | ParsedInput.SigFile (ParsedSigFileInput (modules = [
            SynModuleOrNamespaceSig(decls = [SynModuleSigDecl.Types(range = r)]) ])) ->
            assertRange (3, 0) (5,11) r
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Range of SynTypeDefnSig record should end at last member`` () =
        let parseResults = 
            getParseResultsOfSignatureFile
                """namespace X
type MyRecord =
    { Level: int }
    member Score : unit -> int"""

        match parseResults with
        | ParsedInput.SigFile (ParsedSigFileInput (modules = [
            SynModuleOrNamespaceSig(decls = [SynModuleSigDecl.Types(types = [SynTypeDefnSig.SynTypeDefnSig(range = r)])]) ])) ->
            assertRange (2, 0) (4, 30) r
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Range of SynTypeDefnSig object model should end at last member`` () =
        let parseResults = 
            getParseResultsOfSignatureFile
                """namespace X
type MyRecord =
    class
    end
    member Score : unit -> int"""

        match parseResults with
        | ParsedInput.SigFile (ParsedSigFileInput (modules = [
            SynModuleOrNamespaceSig(decls = [SynModuleSigDecl.Types(types = [SynTypeDefnSig.SynTypeDefnSig(range = r)])]) ])) ->
            assertRange (2, 0) (5, 30) r
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Range of SynTypeDefnSig delegate of should start from name`` () =
        let parseResults = 
            getParseResultsOfSignatureFile
                """namespace Y
type MyFunction =
    delegate of int -> string"""

        match parseResults with
        | ParsedInput.SigFile (ParsedSigFileInput (modules = [
            SynModuleOrNamespaceSig(decls = [SynModuleSigDecl.Types(types = [SynTypeDefnSig.SynTypeDefnSig(range = r)])]) ])) ->
            assertRange (2, 0) (3, 29) r
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Range of SynTypeDefnSig simple should end at last val`` () =
        let parseResults = 
            getParseResultsOfSignatureFile
                """namespace Z
type SomeCollection with
    val LastIndex : int
    val SomeThingElse : int -> string"""

        match parseResults with
        | ParsedInput.SigFile (ParsedSigFileInput (modules = [
            SynModuleOrNamespaceSig(decls = [SynModuleSigDecl.Types(types = [SynTypeDefnSig.SynTypeDefnSig(range = r)])]) ])) ->
            assertRange (2, 0) (4, 37) r
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Range of attribute should be included in SynTypeDefnSig`` () =
        let parseResults = 
            getParseResultsOfSignatureFile
                """
namespace SomeNamespace

[<Foo1>]
type MyType =
    class
    end
"""

        match parseResults with
        | ParsedInput.SigFile (ParsedSigFileInput (modules = [
            SynModuleOrNamespaceSig(decls = [SynModuleSigDecl.Types(types = [SynTypeDefnSig.SynTypeDefnSig(range = r)]) as t]) ])) ->
            assertRange (4, 0) (7, 7) r
            assertRange (4, 0) (7, 7) t.Range
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Range of attributes should be included in recursive types`` () =
        let parseResults = 
            getParseResultsOfSignatureFile
                """
namespace SomeNamespace

type Foo =
    | Bar

and [<CustomEquality>] Bang =
    internal
        {
            LongNameBarBarBarBarBarBarBar: int
        }
        override GetHashCode : unit -> int
"""

        match parseResults with
        | ParsedInput.SigFile (ParsedSigFileInput (modules = [
            SynModuleOrNamespaceSig(decls = [SynModuleSigDecl.Types(types = [
                SynTypeDefnSig.SynTypeDefnSig(range = r1)
                SynTypeDefnSig.SynTypeDefnSig(range = r2)
            ]) as t]) ])) ->
            assertRange (4, 0) (5, 9) r1
            assertRange (7, 4) (12, 42) r2
            assertRange (4, 0) (12, 42) t.Range
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Range of attribute should be included in SynValSpfn and Member`` () =
        let parseResults = 
            getParseResultsOfSignatureFile
                """
namespace SomeNamespace

type FooType =
    [<Foo2>] // ValSpfn
    abstract x : int
"""

        match parseResults with
        | ParsedInput.SigFile (ParsedSigFileInput (modules = [
            SynModuleOrNamespaceSig(decls =
                [ SynModuleSigDecl.Types(types = [
                    SynTypeDefnSig.SynTypeDefnSig(typeRepr =
                        SynTypeDefnSigRepr.ObjectModel(memberSigs = [
                            SynMemberSig.Member(range = mr; memberSig = SynValSig(range = mv)) ]))
                ]) ]) ])) ->
            assertRange (5, 4) (6, 20) mr
            assertRange (5, 4) (6, 20) mv
        | _ -> Assert.Fail "Could not get valid AST"

module SynMatchClause =
    [<Test>]
    let ``Range of single SynMatchClause`` () =
        let parseResults = 
            getParseResults
                """
try
    let content = tryDownloadFile url
    Some content
with ex ->
    Infrastructure.ReportWarning ex
    None"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.DoExpr(expr = SynExpr.TryWith(withCases = [ SynMatchClause(range = range) as clause ]))
        ]) ])) ->
            assertRange (5, 5) (7, 8) range
            assertRange (5, 5) (7, 8) clause.Range
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Range of multiple SynMatchClause`` () =
        let parseResults = 
            getParseResults
                """
try
    let content = tryDownloadFile url
    Some content
with
| ex ->
    Infrastructure.ReportWarning ex
    None
| exx ->
    None"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.DoExpr(expr = SynExpr.TryWith(withCases = [ SynMatchClause(range = r1) as clause1
                                                                      SynMatchClause(range = r2) as clause2 ]))
        ]) ])) ->
            assertRange (6, 2) (8, 8) r1
            assertRange (6, 2) (8, 8) clause1.Range
            
            assertRange (9, 2) (10, 8) r2
            assertRange (9, 2) (10, 8) clause2.Range
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Range of single SynMatchClause followed by bar`` () =
        let parseResults = 
            getParseResults
                """
try
    let content = tryDownloadFile url
    Some content
with
| ex ->
    ()
| """

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.DoExpr(expr = SynExpr.TryWith(withCases = [ SynMatchClause(range = range) as clause ]))
        ]) ])) ->
            assertRange (6, 2) (7, 6) range
            assertRange (6, 2) (7, 6) clause.Range
        | _ -> Assert.Fail "Could not get valid AST"
    
    [<Test>]
    let ``Range of single SynMatchClause with missing body`` () =
        let parseResults = 
            getParseResults
                """
try
    let content = tryDownloadFile url
    Some content
with
| ex ->"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.DoExpr(expr = SynExpr.TryWith(withCases = [ SynMatchClause(range = range) as clause ]))
        ]) ])) ->
            assertRange (6, 2) (6, 4) range
            assertRange (6, 2) (6, 4) clause.Range
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Range of single SynMatchClause with missing body and when expr`` () =
        let parseResults = 
            getParseResults
                """
try
    let content = tryDownloadFile url
    Some content
with
| ex when (isNull ex) ->"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.DoExpr(expr = SynExpr.TryWith(withCases = [ SynMatchClause(range = range) as clause ]))
        ]) ])) ->
            assertRange (6, 2) (6, 21) range
            assertRange (6, 2) (6, 21) clause.Range
        | _ -> Assert.Fail "Could not get valid AST"

module SourceIdentifiers =
    [<Test>]
    let ``__LINE__`` () =
        let parseResults = 
            getParseResults
                """
__LINE__"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.DoExpr(expr = SynExpr.Const(SynConst.SourceIdentifier("__LINE__", "2", range), _))
        ]) ])) ->
            assertRange (2, 0) (2, 8) range
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``__SOURCE_DIRECTORY__`` () =
        let parseResults = 
            getParseResults
                """
__SOURCE_DIRECTORY__"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.DoExpr(expr = SynExpr.Const(SynConst.SourceIdentifier("__SOURCE_DIRECTORY__", _, range), _))
        ]) ])) ->
            assertRange (2, 0) (2, 20) range
        | _ -> Assert.Fail "Could not get valid AST"
        
    [<Test>]
    let ``__SOURCE_FILE__`` () =
        let parseResults = 
            getParseResults
                """
__SOURCE_FILE__"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.DoExpr(expr = SynExpr.Const(SynConst.SourceIdentifier("__SOURCE_FILE__", _, range), _))
        ]) ])) ->
            assertRange (2, 0) (2, 15) range
        | _ -> Assert.Fail "Could not get valid AST"

module NestedModules =

    [<Test>]
    let ``Range of attribute should be included in SynModuleSigDecl.NestedModule`` () =
        let parseResults = 
            getParseResultsOfSignatureFile
                """
namespace SomeNamespace

[<Foo>]
module Nested =
    val x : int
"""

        match parseResults with
        | ParsedInput.SigFile (ParsedSigFileInput (modules = [ SynModuleOrNamespaceSig(decls = [
            SynModuleSigDecl.NestedModule _ as nm
        ]) as sigModule ])) ->
            assertRange (4, 0) (6, 15) nm.Range
            assertRange (2, 0) (6, 15) sigModule.Range
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Range of attribute should be included in SynModuleDecl.NestedModule`` () =
        let parseResults = 
            getParseResults
                """
module TopLevel

[<Foo>]
module Nested =
    ()"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.NestedModule _ as nm
        ]) ])) ->
            assertRange (4, 0) (6, 6) nm.Range
        | _ -> Assert.Fail "Could not get valid AST"

module SynBindings =
    [<Test>]
    let ``Range of attribute should be included in SynModuleDecl.Let`` () =
        let parseResults = 
            getParseResults
                """
[<Foo>]
let a = 0"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.Let(bindings = [SynBinding(range = mb)]) as lt
        ]) ])) ->
            assertRange (2, 0) (3, 5) mb
            assertRange (2, 0) (3, 9) lt.Range
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Range of attribute between let keyword and pattern should be included in SynModuleDecl.Let`` () =
        let parseResults = 
            getParseResults
                """
let [<Literal>] (A x) = 1"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.Let(bindings = [SynBinding(range = mb)]) as lt
        ]) ])) ->
            assertRange (2, 4) (2, 21) mb
            assertRange (2, 0) (2, 25) lt.Range
        | _ -> Assert.Fail "Could not get valid AST"
    
    [<Test>]
    let ``Range of attribute should be included in SynMemberDefn.LetBindings`` () =
        let parseResults = 
            getParseResults
                """
type Bar =
    [<Foo>]
    let x = 8"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.Types(typeDefns = [SynTypeDefn(typeRepr = SynTypeDefnRepr.ObjectModel(members = [SynMemberDefn.LetBindings(bindings = [SynBinding(range = mb)]) as m]))])
        ]) ])) ->
            assertRange (3, 4) (4, 9) mb
            assertRange (3, 4) (4, 13) m.Range
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Range of attribute should be included in SynMemberDefn.Member`` () =
        let parseResults = 
            getParseResults
                """
type Bar =
    [<Foo>]
    member this.Something () = ()"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.Types(typeDefns = [SynTypeDefn(typeRepr = SynTypeDefnRepr.ObjectModel(members = [SynMemberDefn.Member(memberDefn = SynBinding(range = mb)) as m]))])
        ]) ])) ->
            assertRange (3, 4) (4, 28) mb
            assertRange (3, 4) (4, 33) m.Range
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Range of attribute should be included in binding of SynExpr.ObjExpr`` () =
        let parseResults = 
            getParseResults
                """
{ new System.Object() with
    [<Foo>]
    member x.ToString() = "F#" }"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.DoExpr(expr = SynExpr.ObjExpr(bindings = [SynBinding(range = mb)]))
        ]) ])) ->
            assertRange (3, 4) (4, 23) mb
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Range of attribute should be included in constructor SynMemberDefn.Member`` () =
        let parseResults = 
            getParseResults
                """
type Tiger =
    [<Foo>]
    new () = ()"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.Types(typeDefns = [SynTypeDefn(typeRepr = SynTypeDefnRepr.ObjectModel(members = [SynMemberDefn.Member(memberDefn = SynBinding(range = mb)) as m]))])
        ]) ])) ->
            assertRange (3, 4) (4, 10) mb
            assertRange (3, 4) (4, 15) m.Range
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Range of attribute should be included in constructor SynMemberDefn.Member, optAsSpec`` () =
        let parseResults = 
            getParseResults
                """
type Tiger =
    [<Foo>]
    new () as tony = ()"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.Types(typeDefns = [SynTypeDefn(typeRepr = SynTypeDefnRepr.ObjectModel(members = [SynMemberDefn.Member(memberDefn = SynBinding(range = mb)) as m]))])
        ]) ])) ->
            assertRange (3, 4) (4, 18) mb
            assertRange (3, 4) (4, 23) m.Range
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Range of attribute should be included in secondary constructor`` () =
        let parseResults = 
            getParseResults
                """
type T() =
    new () =
        T ()

    internal new () =
        T ()

    [<Foo>]
    new () =
        T ()"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.Types(typeDefns = [SynTypeDefn(typeRepr = SynTypeDefnRepr.ObjectModel(members = [
                SynMemberDefn.ImplicitCtor _
                SynMemberDefn.Member(memberDefn = SynBinding(range = mb1)) as m1
                SynMemberDefn.Member(memberDefn = SynBinding(range = mb2)) as m2
                SynMemberDefn.Member(memberDefn = SynBinding(range = mb3)) as m3
            ]))])
        ]) ])) ->
            assertRange (3, 4) (3, 10) mb1
            assertRange (3, 4) (4, 12) m1.Range
            assertRange (6, 4) (6, 19) mb2
            assertRange (6, 4) (7, 12) m2.Range
            assertRange (9, 4) (10, 10) mb3
            assertRange (9, 4) (11, 12) m3.Range
        | _ -> Assert.Fail "Could not get valid AST"

    
    [<Test>]
    let ``Range of attribute should be included in write only SynMemberDefn.Member property`` () =
        let parseResults = 
            getParseResults
                """
type Crane =
    [<Foo>]
    member this.MyWriteOnlyProperty with set (value) = myInternalValue <- value"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.Types(typeDefns = [SynTypeDefn(typeRepr = SynTypeDefnRepr.ObjectModel(members = [SynMemberDefn.Member(memberDefn = SynBinding(range = mb)) as m]))])
        ]) ])) ->
            assertRange (3, 4) (4, 52) mb
            assertRange (3, 4) (4, 79) m.Range
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Range of attribute should be included in full SynMemberDefn.Member property`` () =
        let parseResults = 
            getParseResults
                """
type Bird =
    [<Foo>]
    member this.TheWord
        with get () = myInternalValue
        and set (value) = myInternalValue <- value"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.Types(typeDefns = [SynTypeDefn(typeRepr = SynTypeDefnRepr.ObjectModel(members = [
                SynMemberDefn.Member(memberDefn = SynBinding(range = mb1)) as getter
                SynMemberDefn.Member(memberDefn = SynBinding(range = mb2)) as setter
            ]))])
        ]) ])) ->
            assertRange (3, 4) (5, 19) mb1
            assertRange (3, 4) (6, 50) getter.Range
            assertRange (3, 4) (6, 23) mb2
            assertRange (3, 4) (6, 50) setter.Range
        | _ -> Assert.Fail "Could not get valid AST"

module ParsedHashDirective =
    [<Test>]
    let ``SourceIdentifier as ParsedHashDirectiveArgument`` () =
        let parseResults = 
            getParseResults
                "#I __SOURCE_DIRECTORY__"

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.HashDirective(ParsedHashDirective("I", [ ParsedHashDirectiveArgument.SourceIdentifier(c,_,m) ] , _), _)
        ]) ])) ->
            Assert.AreEqual("__SOURCE_DIRECTORY__", c)
            assertRange (1, 3) (1, 23) m
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Regular String as ParsedHashDirectiveArgument`` () =
        let parseResults = 
            getParseResults
                "#I \"/tmp\""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.HashDirective(ParsedHashDirective("I", [ ParsedHashDirectiveArgument.String(v, SynStringKind.Regular, m) ] , _), _)
        ]) ])) ->
            Assert.AreEqual("/tmp", v)
            assertRange (1, 3) (1, 9) m
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Verbatim String as ParsedHashDirectiveArgument`` () =
        let parseResults = 
            getParseResults
                "#I @\"C:\\Temp\""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.HashDirective(ParsedHashDirective("I", [ ParsedHashDirectiveArgument.String(v, SynStringKind.Verbatim, m) ] , _), _)
        ]) ])) ->
            Assert.AreEqual("C:\\Temp", v)
            assertRange (1, 3) (1, 13) m
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Triple quote String as ParsedHashDirectiveArgument`` () =
        let parseResults = 
            getParseResults
                "#nowarn \"\"\"40\"\"\""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.HashDirective(ParsedHashDirective("nowarn", [ ParsedHashDirectiveArgument.String(v, SynStringKind.TripleQuote, m) ] , _), _)
        ]) ])) ->
            Assert.AreEqual("40", v)
            assertRange (1, 8) (1, 16) m
        | _ -> Assert.Fail "Could not get valid AST"

module Lambdas =
    [<Test>]
    let ``Lambda with two parameters gives correct body`` () =
        let parseResults = 
            getParseResults
                "fun a b -> x"

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.DoExpr(
                expr = SynExpr.Lambda(parsedData = Some([SynPat.Named _; SynPat.Named _], SynExpr.Ident(ident)))
            )
        ]) ])) ->
            Assert.AreEqual("x", ident.idText)
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Lambda with wild card parameter gives correct body`` () =
        let parseResults = 
            getParseResults
                "fun a _ b -> x"

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.DoExpr(
                expr = SynExpr.Lambda(parsedData = Some([SynPat.Named _; SynPat.Wild _; SynPat.Named _], SynExpr.Ident(ident)))
            )
        ]) ])) ->
            Assert.AreEqual("x", ident.idText)
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Lambda with tuple parameter with wild card gives correct body`` () =
        let parseResults = 
            getParseResults
                "fun a (b, _) c -> x"

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.DoExpr(
                expr = SynExpr.Lambda(parsedData = Some([SynPat.Named _; SynPat.Paren(SynPat.Tuple _,_); SynPat.Named _], SynExpr.Ident(ident)))
            )
        ]) ])) ->
            Assert.AreEqual("x", ident.idText)
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Lambda with wild card that returns a lambda gives correct body`` () =
        let parseResults = 
            getParseResults
                "fun _ -> fun _ -> x"

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.DoExpr(
                expr = SynExpr.Lambda(parsedData = Some([SynPat.Wild _], SynExpr.Lambda(parsedData = Some([SynPat.Wild _], SynExpr.Ident(ident)))))
            )
        ]) ])) ->
            Assert.AreEqual("x", ident.idText)
        | _ -> Assert.Fail "Could not get valid AST"

module IfThenElse =
    [<Test>]
    let ``If keyword in IfThenElse`` () =
        let parseResults = 
            getParseResults
                "if a then b"

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.DoExpr(
                expr = SynExpr.IfThenElse(ifKeyword = SynIfThenElseStart.If mIfKw ; thenKeyword = mThenKw ; elseKeyword = None)
            )
        ]) ])) ->
            assertRange (1, 0) (1, 2) mIfKw
            assertRange (1, 5) (1, 9) mThenKw
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Else keyword in simple IfThenElse`` () =
        let parseResults = 
            getParseResults
                "if a then b else c"

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.DoExpr(
                expr = SynExpr.IfThenElse(ifKeyword = SynIfThenElseStart.If mIfKw ; thenKeyword = mThenKw ; elseKeyword = Some mElse)
            )
        ]) ])) ->
            assertRange (1, 0) (1, 2) mIfKw
            assertRange (1, 5) (1, 9) mThenKw
            assertRange (1, 12) (1, 16) mElse
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``If, Then and Else keyword on separate lines`` () =
        let parseResults = 
            getParseResults
                """
if a
then b
else c"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.DoExpr(
                expr = SynExpr.IfThenElse(ifKeyword = SynIfThenElseStart.If mIfKw ; thenKeyword = mThenKw ; elseKeyword = Some mElse)
            )
        ]) ])) ->
            assertRange (2, 0) (2, 2) mIfKw
            assertRange (3, 0) (3, 4) mThenKw
            assertRange (4, 0) (4, 4) mElse
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Nested elif in IfThenElse`` () =
        let parseResults = 
            getParseResults
                """
if a then
    b
elif c then d"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.DoExpr(
                expr = SynExpr.IfThenElse(ifKeyword = SynIfThenElseStart.If mIfKw
                                          thenKeyword = mThenKw
                                          elseKeyword = None
                                          elseExpr = Some (SynExpr.IfThenElse(ifKeyword = SynIfThenElseStart.Elif mElif)))
            )
        ]) ])) ->
            assertRange (2, 0) (2, 2) mIfKw
            assertRange (2, 5) (2, 9) mThenKw
            assertRange (4, 0) (4, 4) mElif
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Nested else if in IfThenElse`` () =
        let parseResults = 
            getParseResults
                """
if a then
    b
else
    if c then d"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.DoExpr(
                expr = SynExpr.IfThenElse(ifKeyword = SynIfThenElseStart.If mIfKw
                                          thenKeyword = mThenKw
                                          elseKeyword = Some mElse
                                          elseExpr = Some (SynExpr.IfThenElse(ifKeyword = SynIfThenElseStart.If mElseIf)))
            )
        ]) ])) ->
            assertRange (2, 0) (2, 2) mIfKw
            assertRange (2, 5) (2, 9) mThenKw
            assertRange (4, 0) (4, 4) mElse
            assertRange (5, 4) (5, 6) mElseIf
        | _ -> Assert.Fail "Could not get valid AST"

    [<Test>]
    let ``Nested else if on the same line in IfThenElse`` () =
        let parseResults = 
            getParseResults
                """
if a then
    b
else if c then
    d"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.DoExpr(
                expr = SynExpr.IfThenElse(ifKeyword = SynIfThenElseStart.If mIfKw
                                          thenKeyword = mThenKw
                                          elseKeyword = Some mElse
                                          elseExpr = Some (SynExpr.IfThenElse(ifKeyword = SynIfThenElseStart.If mElseIf)))
            )
        ]) ])) ->
            assertRange (2, 0) (2, 2) mIfKw
            assertRange (2, 5) (2, 9) mThenKw
            assertRange (4, 0) (4, 4) mElse
            assertRange (4, 5) (4, 7) mElseIf
        | _ -> Assert.Fail "Could not get valid AST"
    
    [<Test>]
    let ``Deeply nested IfThenElse`` () =
        let parseResults = 
            getParseResults
                """
if a then
    b
elif c then
    d
else
        if e then
            f
        else
            g"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.DoExpr(
                expr = SynExpr.IfThenElse(ifKeyword = SynIfThenElseStart.If mIf1
                                          elseKeyword = None
                                          elseExpr = Some (SynExpr.IfThenElse(ifKeyword = SynIfThenElseStart.Elif mElif
                                                                              elseKeyword = Some mElse1
                                                                              elseExpr = Some (SynExpr.IfThenElse(ifKeyword = SynIfThenElseStart.If mIf2
                                                                                                                  elseKeyword = Some mElse2))))))
        ]) ])) ->
            assertRange (2, 0) (2, 2) mIf1
            assertRange (4, 0) (4, 4) mElif
            assertRange (6, 0) (6, 4) mElse1
            assertRange (7, 8) (7, 10) mIf2
            assertRange (9, 8) (9, 12) mElse2

        | _ -> Assert.Fail "Could not get valid AST"
        
    [<Test>]
    let ``Comment between else and if`` () =
        let parseResults = 
            getParseResults
                """
if a then
    b
else (* some long comment here *) if c then
    d"""

        match parseResults with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = [ SynModuleOrNamespace.SynModuleOrNamespace(decls = [
            SynModuleDecl.DoExpr(
                expr = SynExpr.IfThenElse(ifKeyword = SynIfThenElseStart.If mIf1
                                          elseKeyword = Some mElse
                                          elseExpr = Some (SynExpr.IfThenElse(ifKeyword = SynIfThenElseStart.If mIf2))))
        ]) ])) ->
            assertRange (2, 0) (2, 2) mIf1
            assertRange (4, 0) (4, 4) mElse
            assertRange (4, 34) (4, 36) mIf2

        | _ -> Assert.Fail "Could not get valid AST"
