
#if INTERACTIVE
#r "../../artifacts/bin/fcs/net461/FSharp.Compiler.Service.dll" // note, build FSharp.Compiler.Service.Tests.fsproj to generate this, this DLL has a public API so can be used from F# Interactive
#r "../../artifacts/bin/fcs/net461/nunit.framework.dll"
#load "FsUnit.fs"
#load "Common.fs"
#else
module FSharp.Compiler.Service.Tests.InteractiveChecker
#endif

open NUnit.Framework
open FsUnit
open System
open FSharp.Compiler.Service.Tests.Common
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range

let internal longIdentToString (longIdent: LongIdent) =
    String.Join(".", longIdent |> List.map (fun ident -> ident.ToString()))
let internal longIdentWithDotsToString (LongIdentWithDots (longIdent, _)) = longIdentToString longIdent

let internal posToTuple (pos: pos) = (pos.Line, pos.Column)
let internal rangeToTuple (range: range) = (posToTuple range.Start, posToTuple range.End)

let internal identsAndRanges (input: ParsedInput) =
    let identAndRange ident (range: range) =
        (ident, rangeToTuple range)
    let extractFromComponentInfo (componentInfo: SynComponentInfo) =
        let ((SynComponentInfo.SynComponentInfo(_attrs, _typarDecls, _typarConstraints, longIdent, _, _, _, range))) = componentInfo
        // TODO : attrs, typarDecls and typarConstraints
        [identAndRange (longIdentToString longIdent) range]
    let extractFromTypeDefn (typeDefn: SynTypeDefn) =
        let (SynTypeDefn(componentInfo, _repr, _members, _, _)) = typeDefn
        // TODO : repr and members
        extractFromComponentInfo componentInfo
    let rec extractFromModuleDecl (moduleDecl: SynModuleDecl) =
        match moduleDecl with
        | SynModuleDecl.Types(typeDefns, _) -> (typeDefns |> List.collect extractFromTypeDefn)
        | SynModuleDecl.ModuleAbbrev(ident, _, range) -> [ identAndRange (ident.ToString()) range ]
        | SynModuleDecl.NestedModule(componentInfo, _, decls, _, _) -> (extractFromComponentInfo componentInfo) @ (decls |> List.collect extractFromModuleDecl)
        | SynModuleDecl.Let(_, _, _) -> failwith "Not implemented yet"
        | SynModuleDecl.DoExpr(_, _, _range) -> failwith "Not implemented yet"
        | SynModuleDecl.Exception(_, _range) -> failwith "Not implemented yet"
        | SynModuleDecl.Open(SynOpenDeclTarget.ModuleOrNamespace (lid, range), _) -> [ identAndRange (longIdentToString lid) range ]
        | SynModuleDecl.Open(SynOpenDeclTarget.Type _, _) -> failwith "Not implemented yet"
        | SynModuleDecl.Attributes(_attrs, _range) -> failwith "Not implemented yet"
        | SynModuleDecl.HashDirective(_, _range) -> failwith "Not implemented yet"
        | SynModuleDecl.NamespaceFragment(moduleOrNamespace) -> extractFromModuleOrNamespace moduleOrNamespace
    and extractFromModuleOrNamespace (SynModuleOrNamespace(longIdent, _, _, moduleDecls, _, _, _, _)) =
        let xs = moduleDecls |> List.collect extractFromModuleDecl
        if longIdent.IsEmpty then xs
        else
            (identAndRange (longIdentToString longIdent) (longIdent |> List.map (fun id -> id.idRange) |> List.reduce unionRanges)) :: xs

    match input with
    | ParsedInput.ImplFile(ParsedImplFileInput(_, _, _, _, _, modulesOrNamespaces, _)) ->
         modulesOrNamespaces |> List.collect extractFromModuleOrNamespace
    | ParsedInput.SigFile _ -> []

let internal parseAndExtractRanges code =
    let file = "Test.fs"
    let result = parseSourceCode (file, code)
    result |> identsAndRanges

let input =
    """
    namespace N

    type Sample () = class end
    """

[<Test>]
let ``Test ranges - namespace`` () =
    let res = parseAndExtractRanges input 
    printfn "Test ranges - namespace, res = %A" res
    res |> shouldEqual [("N", ((2, 14), (2, 15))); ("Sample", ((4, 9), (4, 15)))]

let input2 =
    """
    module M

    type Sample () = class end
    """
    
[<Test>]
let ``Test ranges - module`` () =
    let res = parseAndExtractRanges input2
    printfn "Test ranges - module, res = %A" res
    res |> shouldEqual [("M", ((2, 11), (2, 12))); ("Sample", ((4, 9), (4, 15)))]

let input3 =
    """
    namespace global

    type Sample () = class end
    """

[<Test>]
let ``Test ranges - global namespace`` () =
    let res = parseAndExtractRanges input3 
    printfn "Test ranges - global namespace, res = %A" res
    res |> shouldEqual [("Sample", ((4, 9), (4, 15)))]

open System.Threading

