
#if INTERACTIVE
#r "../../artifacts/bin/FSharp.Compiler.Service/Debug/netstandard2.0/FSharp.Compiler.Service.dll"
#r "../../artifacts/bin/FSharp.Compiler.UnitTests/Debug/net472/nunit.framework.dll"
#load "FsUnit.fs"
#load "Common.fs"
#else
module FSharp.Compiler.Service.Tests.ExprTests
#endif

open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Text
open System.Collections.Generic
open System.Diagnostics
open System.Threading
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.EditorServices
open FSharp.Compiler.IO
open FSharp.Compiler.Service.Tests.Common
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns

type FSharpCore =
    | FC45
    | FC46
    | FC47
    | FC50

    static member fsharpVersion fc =
        match fc with
        | FC45 -> "FSharp.Core 4.5"
        | FC46 -> "FSharp.Core 4.6"
        | FC47 -> "FSharp.Core 4.7"
        | FC50 -> "FSharp.Core 5.0"


[<AutoOpen>]
module internal Utils =
    let getTempPath() =
        Path.Combine(Path.GetTempPath(), "ExprTests")

    /// If it doesn't exists, create a folder 'ExprTests' in local user's %TEMP% folder
    let createTempDir() =
        let tempPath = getTempPath()
        do
            if Directory.Exists tempPath then ()
            else Directory.CreateDirectory tempPath |> ignore

    /// Returns the filename part of a temp file name created with Path.GetTempFileName()
    /// and an added process id and thread id to ensure uniqueness between threads.
    let getTempFileName() =
        let tempFileName = Path.GetTempFileName()
        try
            let tempFile, tempExt = Path.GetFileNameWithoutExtension tempFileName, Path.GetExtension tempFileName
            let procId, threadId = Process.GetCurrentProcess().Id, Thread.CurrentThread.ManagedThreadId
            String.concat "" [tempFile; "_"; string procId; "_"; string threadId; tempExt]  // ext includes dot
        finally
            try
                // Since Path.GetTempFileName() creates a *.tmp file in the %TEMP% folder, we want to clean it up.
                // This also prevents a system to run out of available randomized temp files (the pool is only 64k large).
                FileSystem.FileDeleteShim tempFileName
            with _ -> ()

    /// Clean up after a test is run. If you need to inspect the create *.fs files, change this function to do nothing, or just break here.
    let cleanupTempFiles files =
        { new System.IDisposable with
            member _.Dispose() =
                for fileName in files do
                    try
                        // cleanup: only the source file is written to the temp dir.
                        FileSystem.FileDeleteShim fileName
                    with _ -> ()

                try
                    // remove the dir when empty
                    let tempPath = getTempPath()
                    if Directory.GetFiles tempPath |> Array.isEmpty then
                        Directory.Delete tempPath
                with _ -> () }

    /// Given just a filename, returns it with changed extension located in %TEMP%\ExprTests
    let getTempFilePathChangeExt tmp ext =
        Path.Combine(getTempPath(), Path.ChangeExtension(tmp, ext))

    // This behaves slightly differently on Mono versions, 'null' is printed somethimes, 'None' other times
    // Presumably this is very small differences in Mono reflection causing F# printing to change behaviour
    // For now just disabling this test. See https://github.com/fsharp/FSharp.Compiler.Service/pull/766
    let filterHack l =
        l |> List.map (fun (s:string) ->
            // potential difference on Mono
            s.Replace("ILArrayShape [(Some 0, None)]", "ILArrayShape [(Some 0, null)]")
             // spacing difference when run locally in VS
             .Replace("I_ldelema (NormalAddress,false,ILArrayShape [(Some 0, null)],!0)]", "I_ldelema (NormalAddress, false, ILArrayShape [(Some 0, null)], !0)]")
             // local VS IDE vs CI env difference
             .Replace("Operators.Hash<Microsoft.FSharp.Core.string> (x)", "x.GetHashCode()"))

    let rec printExpr low (e:FSharpExpr) =
        match e with
        | AddressOf(e1) -> "&"+printExpr 0 e1
        | AddressSet(e1,e2) -> printExpr 0 e1 + " <- " + printExpr 0 e2
        | Application(f,tyargs,args) -> quote low (printExpr 10 f + printTyargs tyargs + " " + printCurriedArgs args)
        | BaseValue(_) -> "base"
        | CallWithWitnesses(Some obj,v,tyargs1,tyargs2,witnessL,argsL) -> printObjOpt (Some obj) + v.CompiledName  + printTyargs tyargs2 + printTupledArgs (witnessL @ argsL)
        | CallWithWitnesses(None,v,tyargs1,tyargs2,witnessL,argsL) -> v.DeclaringEntity.Value.CompiledName + printTyargs tyargs1 + "." + v.CompiledName  + printTyargs tyargs2 + " " + printTupledArgs (witnessL @ argsL)
        | Call(Some obj,v,tyargs1,tyargs2,argsL) -> printObjOpt (Some obj) + v.CompiledName  + printTyargs tyargs2 + printTupledArgs argsL
        | Call(None,v,tyargs1,tyargs2,argsL) -> v.DeclaringEntity.Value.CompiledName + printTyargs tyargs1 + "." + v.CompiledName  + printTyargs tyargs2 + " " + printTupledArgs argsL
        | Coerce(ty1,e1) -> quote low (printExpr 10 e1 + " :> " + printTy ty1)
        | DefaultValue(ty1) -> "dflt"
        | FastIntegerForLoop _ -> "for-loop"
        | ILAsm(s,tyargs,args) -> s + printTupledArgs args
        | ILFieldGet _ -> "ILFieldGet"
        | ILFieldSet _ -> "ILFieldSet"
        | IfThenElse (a,b,c) -> "(if " + printExpr 0 a + " then " + printExpr 0 b + " else " + printExpr 0 c + ")"
        | Lambda(v,e1) -> "fun " + v.CompiledName + " -> " + printExpr 0 e1
        | Let((v,e1),b) -> "let " + (if v.IsMutable then "mutable " else "") + v.CompiledName + ": " + printTy v.FullType + " = " + printExpr 0 e1 + " in " + printExpr 0 b
        | LetRec(vse,b) -> "let rec ... in " + printExpr 0 b
        | NewArray(ty,es) -> "[|" + (es |> Seq.map (printExpr 0) |> String.concat "; ") +  "|]"
        | NewDelegate(ty,es) -> "new-delegate"
        | NewObject(v,tys,args) -> "new " + v.DeclaringEntity.Value.CompiledName + printTupledArgs args
        | NewRecord(v,args) ->
            let fields = v.TypeDefinition.FSharpFields
            "{" + ((fields, args) ||> Seq.map2 (fun f a -> f.Name + " = " + printExpr 0 a) |> String.concat "; ") + "}"
        | NewAnonRecord(v,args) ->
            let fields = v.AnonRecordTypeDetails.SortedFieldNames
            "{" + ((fields, args) ||> Seq.map2 (fun f a -> f+ " = " + printExpr 0 a) |> String.concat "; ") + "}"
        | NewTuple(v,args) -> printTupledArgs args
        | NewUnionCase(ty,uc,args) -> uc.CompiledName + printTupledArgs args
        | Quote(e1) -> "quote" + printTupledArgs [e1]
        | FSharpFieldGet(obj, ty,f) -> printObjOpt obj + f.Name
        | AnonRecordGet(obj, ty, n) -> printExpr 0 obj + "." + ty.AnonRecordTypeDetails.SortedFieldNames.[n]
        | FSharpFieldSet(obj, ty,f,arg) -> printObjOpt obj + f.Name + " <- " + printExpr 0 arg
        | Sequential(e1,e2) -> "(" + printExpr 0 e1 + "; " + printExpr 0 e2 + ")"
        | ThisValue _ -> "this"
        | TryFinally(e1,e2) -> "try " + printExpr 0 e1 + " finally " + printExpr 0 e2
        | TryWith(e1,_,_,vC,eC) -> "try " + printExpr 0 e1 + " with " + vC.CompiledName + " -> " + printExpr 0 eC
        | TupleGet(ty,n,e1) -> printExpr 10 e1 + ".Item" + string n
        | DecisionTree(dtree,targets) -> "match " + printExpr 10 dtree + " targets ..."
        | DecisionTreeSuccess (tg,es) -> "$" + string tg
        | TypeLambda(gp1,e1) -> "FUN ... -> " + printExpr 0 e1
        | TypeTest(ty,e1) -> printExpr 10 e1 + " :? " + printTy ty
        | UnionCaseSet(obj,ty,uc,f1,e1) -> printExpr 10 obj + "." + f1.Name + " <- " + printExpr 0 e1
        | UnionCaseGet(obj,ty,uc,f1) -> printExpr 10 obj + "." + f1.Name
        | UnionCaseTest(obj,ty,f1) -> printExpr 10 obj + ".Is" + f1.Name
        | UnionCaseTag(obj,ty) -> printExpr 10 obj + ".Tag"
        | ObjectExpr(ty,basecall,overrides,iimpls) -> "{ " + printExpr 10 basecall + " with " + printOverrides overrides + " " + printIimpls iimpls + " }"
        | TraitCall(tys,nm,_,argtys,tinst,args) -> "trait call " + nm + printTupledArgs args
        | Const(obj,ty) ->
            match obj with
            | :? string  as s -> "\"" + s + "\""
            | null -> "()"
            | _ -> string obj
        | Value(v) -> v.CompiledName
        | ValueSet(v,e1) -> quote low (v.CompiledName + " <- " + printExpr 0 e1)
        | WhileLoop(e1,e2) -> "while " + printExpr 0 e1 + " do " + printExpr 0 e2 + " done"
        | _ -> failwith (sprintf "unrecognized %+A" e)

    and quote low s = if low > 0 then "(" + s + ")" else s
    and printObjOpt e = match e with None -> "" | Some e -> printExpr 10 e + "."
    and printTupledArgs args = "(" + String.concat "," (List.map (printExpr 0) args) + ")"
    and printCurriedArgs args = String.concat " " (List.map (printExpr 10) args)
    and printParams (vs: FSharpMemberOrFunctionOrValue list) = "(" + String.concat "," (vs |> List.map (fun v -> v.CompiledName)) + ")"
    and printCurriedParams (vs: FSharpMemberOrFunctionOrValue list list) = String.concat " " (List.map printParams vs)
    and printTy ty = ty.Format(FSharpDisplayContext.Empty)
    and printTyargs tyargs = match tyargs with [] -> "" | args -> "<" + String.concat "," (List.map printTy tyargs) + ">"
    and printOverrides ors = String.concat ";" (List.map printOverride ors)
    and printOverride o =
        match o.CurriedParameterGroups with
        | [t] :: a ->
            "member " + t.CompiledName + "." + o.Signature.Name + printCurriedParams a + " = " + printExpr 10 o.Body
        | _ -> failwith "wrong this argument in object expression override"
    and printIimpls iis = String.concat ";" (List.map printImlementation iis)
    and printImlementation (i, ors) = "interface " + printTy i + " with " + printOverrides ors

    let rec printFSharpDecls prefix decls =
        seq {
            let mutable i = 0
            for decl in decls do
                i <- i + 1
                match decl with
                | FSharpImplementationFileDeclaration.Entity (e, sub) ->
                    yield sprintf "%s%i) ENTITY: %s %A" prefix i e.CompiledName (attribsOfSymbol e)
                    if not (Seq.isEmpty e.Attributes) then
                        yield sprintf "%sattributes: %A" prefix (Seq.toList e.Attributes)
                    if not (Seq.isEmpty e.DeclaredInterfaces) then
                        yield sprintf "%sinterfaces: %A" prefix (Seq.toList e.DeclaredInterfaces)
                    yield ""
                    yield! printFSharpDecls (prefix + "\t") sub
                | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue (meth, args, body) ->
                    yield sprintf "%s%i) METHOD: %s %A" prefix i meth.CompiledName (attribsOfSymbol meth)
                    yield sprintf "%stype: %A" prefix meth.FullType
                    yield sprintf "%sargs: %A" prefix args
                    // if not meth.IsCompilerGenerated then
                    yield sprintf "%sbody: %A" prefix body
                    yield ""
                | FSharpImplementationFileDeclaration.InitAction (expr) ->
                    yield sprintf "%s%i) ACTION" prefix i
                    yield sprintf "%s%A" prefix expr
                    yield ""
        }

    let rec printDeclaration (excludes:HashSet<_> option) (d: FSharpImplementationFileDeclaration) =
        seq {
           match d with
            | FSharpImplementationFileDeclaration.Entity(e,ds) ->
                yield sprintf "type %s" e.LogicalName
                yield! printDeclarations excludes ds
            | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v,vs,e) ->

               if not v.IsCompilerGenerated &&
                  not (match excludes with None -> false | Some t -> t.Contains v.CompiledName) then
                let text =
                    //printfn "%s" v.CompiledName
//                 try
                    if v.IsMember then
                        sprintf "member %s%s = %s @ %s" v.CompiledName (printCurriedParams vs)  (printExpr 0 e) (e.Range.ToShortString())
                    else
                        sprintf "let %s%s = %s @ %s" v.CompiledName (printCurriedParams vs) (printExpr 0 e) (e.Range.ToShortString())
//                 with e ->
//                     printfn "FAILURE STACK: %A" e
//                     sprintf "!!!!!!!!!! FAILED on %s @ %s, message: %s" v.CompiledName (v.DeclarationLocation.ToString()) e.Message
                yield text
            | FSharpImplementationFileDeclaration.InitAction(e) ->
                yield sprintf "do %s" (printExpr 0 e) }

    and printDeclarations excludes ds =
        seq { for d in ds do
                yield! printDeclaration excludes d }

    let rec exprsOfDecl (d: FSharpImplementationFileDeclaration) =
        seq {
           match d with
            | FSharpImplementationFileDeclaration.Entity(e,ds) ->
                yield! exprsOfDecls ds
            | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v,vs,e) ->
               if not v.IsCompilerGenerated then
                  yield e, e.Range
            | FSharpImplementationFileDeclaration.InitAction(e) ->
                yield e, e.Range }
    and exprsOfDecls ds =
        seq { for d in ds do
                yield! exprsOfDecl d }

    let printGenericConstraint name (p: FSharpGenericParameterConstraint) =
        if p.IsCoercesToConstraint then
            Some <| name + " :> " + printTy p.CoercesToTarget
        elif p.IsComparisonConstraint then
            Some <| name + " : comparison"
        elif p.IsEqualityConstraint then
            Some <| name + " : equality"
        elif p.IsReferenceTypeConstraint then
            Some <| name + " : class"
        elif p.IsNonNullableValueTypeConstraint then
            Some <| name + " : struct"
        elif p.IsEnumConstraint then
            Some <| name + " : enum"
        elif p.IsSupportsNullConstraint then
            Some <| name + " : null"
        else None

    let printGenericParameter (p: FSharpGenericParameter) =
        let name =
            if p.Name.StartsWith("?", StringComparison.Ordinal) then "_"
            elif p.IsSolveAtCompileTime then "^" + p.Name
            else "'" + p.Name
        let constraints =
            p.Constraints |> Seq.choose (printGenericConstraint name) |> List.ofSeq
        name, constraints

    let printMemberSignature (v: FSharpMemberOrFunctionOrValue) =
        let genParams =
            let ps = v.GenericParameters |> Seq.map printGenericParameter |> List.ofSeq
            if List.isEmpty ps then "" else
                let constraints = ps |> List.collect snd
                "<" + (ps |> Seq.map fst |> String.concat ", ") +
                    (if List.isEmpty constraints then "" else " when " + String.concat " and " constraints) + ">"

        v.CompiledName + genParams + ": " + printTy v.FullType

    let rec collectMembers (e:FSharpExpr) =
        match e with
        | AddressOf(e) -> collectMembers e
        | AddressSet(e1,e2) -> Seq.append (collectMembers e1) (collectMembers e2)
        | Application(f,_,args) -> Seq.append (collectMembers f) (Seq.collect collectMembers args)
        | BaseValue(_) -> Seq.empty
        | Call(Some obj,v,_,_,argsL) -> Seq.concat [ collectMembers obj; Seq.singleton v; Seq.collect collectMembers argsL ]
        | Call(None,v,_,_,argsL) -> Seq.concat [ Seq.singleton v; Seq.collect collectMembers argsL ]
        | Coerce(_,e) -> collectMembers e
        | DefaultValue(_) -> Seq.empty
        | FastIntegerForLoop (fromArg, toArg, body, _) -> Seq.collect collectMembers [ fromArg; toArg; body ]
        | ILAsm(_,_,args) -> Seq.collect collectMembers args
        | ILFieldGet (Some e,_,_) -> collectMembers e
        | ILFieldGet _ -> Seq.empty
        | ILFieldSet (Some e,_,_,v) -> Seq.append (collectMembers e) (collectMembers v)
        | ILFieldSet _ -> Seq.empty
        | IfThenElse (a,b,c) -> Seq.collect collectMembers [ a; b; c ]
        | Lambda(v,e1) -> collectMembers e1
        | Let((v,e1),b) -> Seq.append (collectMembers e1) (collectMembers b)
        | LetRec(vse,b) -> Seq.append (vse |> Seq.collect (snd >> collectMembers)) (collectMembers b)
        | NewArray(_,es) -> Seq.collect collectMembers es
        | NewDelegate(ty,es) -> collectMembers es
        | NewObject(v,tys,args) -> Seq.append (Seq.singleton v) (Seq.collect collectMembers args)
        | NewRecord(v,args) -> Seq.collect collectMembers args
        | NewTuple(v,args) -> Seq.collect collectMembers args
        | NewUnionCase(ty,uc,args) -> Seq.collect collectMembers args
        | Quote(e1) -> collectMembers e1
        | FSharpFieldGet(Some obj, _,_) -> collectMembers obj
        | FSharpFieldGet _ -> Seq.empty
        | FSharpFieldSet(Some obj,_,_,arg) -> Seq.append (collectMembers obj) (collectMembers arg)
        | FSharpFieldSet(None,_,_,arg) -> collectMembers arg
        | Sequential(e1,e2) -> Seq.append (collectMembers e1) (collectMembers e2)
        | ThisValue _ -> Seq.empty
        | TryFinally(e1,e2) -> Seq.append (collectMembers e1) (collectMembers e2)
        | TryWith(e1,_,f,_,eC) -> Seq.collect collectMembers [ e1; f; eC ]
        | TupleGet(ty,n,e1) -> collectMembers e1
        | DecisionTree(dtree,targets) -> Seq.append (collectMembers dtree) (targets |> Seq.collect (snd >> collectMembers))
        | DecisionTreeSuccess (tg,es) -> Seq.collect collectMembers es
        | TypeLambda(gp1,e1) -> collectMembers e1
        | TypeTest(ty,e1) -> collectMembers e1
        | UnionCaseSet(obj,ty,uc,f1,e1) -> Seq.append (collectMembers obj) (collectMembers e1)
        | UnionCaseGet(obj,ty,uc,f1) -> collectMembers obj
        | UnionCaseTest(obj,ty,f1) -> collectMembers obj
        | UnionCaseTag(obj,ty) -> collectMembers obj
        | ObjectExpr(ty,basecall,overrides,iimpls) ->
            seq {
                yield! collectMembers basecall
                for o in overrides do
                    yield! collectMembers o.Body
                for _, i in iimpls do
                    for o in i do
                        yield! collectMembers o.Body
            }
        | TraitCall(tys,nm,_,argtys,tinst,args) -> Seq.collect collectMembers args
        | Const(obj,ty) -> Seq.empty
        | Value(v) -> Seq.singleton v
        | ValueSet(v,e1) -> Seq.append (Seq.singleton v) (collectMembers e1)
        | WhileLoop(e1,e2) -> Seq.append (collectMembers e1) (collectMembers e2)
        | _ -> failwith (sprintf "unrecognized %+A" e)

    let rec printMembersOfDeclatations ds =
        seq {
            for d in ds do
            match d with
            | FSharpImplementationFileDeclaration.Entity(_,ds) ->
                yield! printMembersOfDeclatations ds
            | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v,vs,e) ->
                yield printMemberSignature v
                yield! collectMembers e |> Seq.map printMemberSignature
            | FSharpImplementationFileDeclaration.InitAction(e) ->
                yield! collectMembers e |> Seq.map printMemberSignature
        }


let createOptionsAux fileSources extraArgs =
    let fileNames = fileSources |> List.map (fun _ -> Utils.getTempFileName())
    let temp2 = Utils.getTempFileName()
    let fileNames = fileNames |> List.map (fun temp1 -> Utils.getTempFilePathChangeExt temp1 ".fs")
    let dllName = Utils.getTempFilePathChangeExt temp2 ".dll"
    let projFileName = Utils.getTempFilePathChangeExt temp2 ".fsproj"

    Utils.createTempDir()
    for (fileSource: string, fileName) in List.zip fileSources fileNames do
         FileSystem.OpenFileForWriteShim(fileName).Write(fileSource)
    let args = [| yield! extraArgs; yield! mkProjectCommandLineArgs (dllName, fileNames) |]
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)

    Utils.cleanupTempFiles (fileNames @ [dllName; projFileName]), options

//---------------------------------------------------------------------------------------------------------
// This project is a smoke test for a whole range of standard and obscure expressions

module internal Project1 =

    let fileSource1 = """
module M

type IntAbbrev = int

let boolEx1 = true
let intEx1 = 1
let int64Ex1 = 1L
let tupleEx1 = (1, 1L)
let tupleEx2 = (1, 1L, 1u)
let tupleEx3 = (1, 1L, 1u, 1s)

let localExample =
   let y = 1
   let z = 1
   y, z

let localGenericFunctionExample() =
   let y = 1
   let compiledAsLocalGenericFunction x = x
   compiledAsLocalGenericFunction y, compiledAsLocalGenericFunction 1.0

let funcEx1 (x:int) =  x
let genericFuncEx1 (x:'T) =  x
let (topPair1a, topPair1b) = (1,2)

let testILCall1 = new obj()
let testILCall2 = System.Console.WriteLine("176")

// Test recursive values in a module
let rec recValNeverUsedAtRuntime = recFuncIgnoresFirstArg (fun _ -> recValNeverUsedAtRuntime) 1
and recFuncIgnoresFirstArg g v = v

let testFun4() =
    // Test recursive values in expression position
    let rec recValNeverUsedAtRuntime = recFuncIgnoresFirstArg (fun _ -> recValNeverUsedAtRuntime) 1
    and recFuncIgnoresFirstArg g v = v

    recValNeverUsedAtRuntime

type ClassWithImplicitConstructor(compiledAsArg: int) =
    inherit obj()
    let compiledAsField = 1
    let compiledAsLocal = 1
    let compiledAsLocal2 = compiledAsLocal + compiledAsLocal
    let compiledAsInstanceMethod () = compiledAsField + compiledAsField
    let compiledAsGenericInstanceMethod x = x

    static let compiledAsStaticField = 1
    static let compiledAsStaticLocal = 1
    static let compiledAsStaticLocal2 = compiledAsStaticLocal + compiledAsStaticLocal
    static let compiledAsStaticMethod () = compiledAsStaticField + compiledAsStaticField
    static let compiledAsGenericStaticMethod x = x

    member __.M1() = compiledAsField + compiledAsGenericInstanceMethod compiledAsField + compiledAsArg
    member __.M2() = compiledAsInstanceMethod()
    static member SM1() = compiledAsStaticField + compiledAsGenericStaticMethod compiledAsStaticField
    static member SM2() = compiledAsStaticMethod()
    //override _.ToString() = base.ToString() + string 999
    member this.TestCallinToString() = this.ToString()

exception Error of int * int

let err = Error(3,4)

let matchOnException err = match err with Error(a,b) -> 3  | e -> raise e

let upwardForLoop () =
    let mutable a = 1
    for i in 0 .. 10 do a <- a + 1
    a

let upwardForLoop2 () =
    let mutable a = 1
    for i = 0 to 10 do a <- a + 1
    a

let downwardForLoop () =
    let mutable a = 1
    for i = 10 downto 1 do a <- a + 1
    a

let quotationTest1() =  <@ 1 + 1 @>
let quotationTest2 v =  <@ %v + 1 @>

type RecdType = { Field1: int; Field2: int }
type UnionType = Case1 of int | Case2 | Case3 of int * string

type ClassWithEventsAndProperties() =
    let ev = new Event<_>()
    static let sev = new Event<_>()
    member x.InstanceProperty = ev.Trigger(1); 1
    static member StaticProperty = sev.Trigger(1); 1
    member x.InstanceEvent = ev.Publish
    member x.StaticEvent = sev.Publish

let c = ClassWithEventsAndProperties()
let v = c.InstanceProperty

System.Console.WriteLine("777") // do a top-levl action

let functionWithSubmsumption(x:obj)  =  x :?> string
//let functionWithCoercion(x:string)  =  (x :> obj) :?> string |> functionWithSubmsumption |> functionWithSubmsumption

type MultiArgMethods(c:int,d:int) =
   member x.Method(a:int, b : int) = 1
   member x.CurriedMethod(a1:int, b1: int)  (a2:int, b2:int) = 1

let testFunctionThatCallsMultiArgMethods() =
    let m = MultiArgMethods(3,4)
    (m.Method(7,8) + m.CurriedMethod (9,10) (11,12))

//let functionThatUsesObjectExpression() =
//   { new obj() with  member x.ToString() = string 888 }
//
//let functionThatUsesObjectExpressionWithInterfaceImpl() =
//   { new obj() with
//       member x.ToString() = string 888
//     interface System.IComparable with
//       member x.CompareTo(y:obj) = 0 }

let testFunctionThatUsesUnitsOfMeasure (x : float<_>) (y: float<_>) = x + y

let testFunctionThatUsesAddressesAndByrefs (x: byref<int>) =
    let mutable w = 4
    let y1 = &x  // address-of
    let y2 = &w  // address-of
    let arr = [| 3;4 |]  // address-of
    let r = ref 3  // address-of
    let y3 = &arr.[0] // address-of array
    let y4 = &r.contents // address-of field
    let z = x + y1 + y2 + y3 // dereference
    w <- 3 // assign to pointer
    x <- 4 // assign to byref
    y2 <- 4 // assign to byref
    y3 <- 5 // assign to byref
    z + x + y1 + y2 + y3 + y4 + arr.[0] + r.contents

let testFunctionThatUsesStructs1 (dt:System.DateTime) =  dt.AddDays(3.0)

let testFunctionThatUsesStructs2 () =
   let dt1 = System.DateTime.Now
   let mutable dt2 = System.DateTime.Now
   let dt3 = dt1 - dt2
   let dt4 = dt1.AddDays(3.0)
   let dt5 = dt1.Millisecond
   let dt6 = &dt2
   let dt7 = dt6 - dt4
   dt7

let testFunctionThatUsesWhileLoop() =
   let mutable x = 1
   while x  < 100 do
      x <- x + 1
   x

let testFunctionThatUsesTryWith() =
   try
     testFunctionThatUsesWhileLoop()
   with :? System.ArgumentException as e -> e.Message.Length


let testFunctionThatUsesTryFinally() =
   try
     testFunctionThatUsesWhileLoop()
   finally
     System.Console.WriteLine("8888")

type System.Console with
    static member WriteTwoLines() = System.Console.WriteLine(); System.Console.WriteLine()

type System.DateTime with
    member x.TwoMinute = x.Minute + x.Minute

let testFunctionThatUsesExtensionMembers() =
   System.Console.WriteTwoLines()
   let v = System.DateTime.Now.TwoMinute
   System.Console.WriteTwoLines()

let testFunctionThatUsesOptionMembers() =
   let x = Some(3)
   (x.IsSome, x.IsNone)

let testFunctionThatUsesOverAppliedFunction() =
   id id 3

let testFunctionThatUsesPatternMatchingOnLists(x) =
    match x with
    | [] -> 1
    | [h] -> 2
    | [h;h2] -> 3
    | _ -> 4

let testFunctionThatUsesPatternMatchingOnOptions(x) =
    match x with
    | None -> 1
    | Some h -> 2 + h

let testFunctionThatUsesPatternMatchingOnOptions2(x) =
    match x with
    | None -> 1
    | Some _ -> 2

let testFunctionThatUsesConditionalOnOptions2(x: int option) =
    if x.IsSome then 1 else 2

let f x y = x+y
let g = f 1
let h = (g 2) + 3

type TestFuncProp() =
    member this.Id = fun x -> x

let wrong = TestFuncProp().Id 0 = 0

let start (name:string) =
    name, name

let last (name:string, values:string ) =
    id (name, values)

let last2 (name:string) =
    id name

let test7(s:string) =
    start s |> last

let test8() =
    last

let test9(s:string) =
    (s,s) |> last

let test10() =
    last2

let test11(s:string) =
    s |> last2

let rec badLoop : (int -> int) =
    () // so that it is a function value
    fun x -> badLoop (x + 1)

module LetLambda =
    let f =
        () // so that it is a function value
        fun a b -> a + b

let letLambdaRes = [ 1, 2 ] |> List.map (fun (a, b) -> LetLambda.f a b)

let anonRecd = {| X = 1; Y = 2 |}
let anonRecdGet = (anonRecd.X, anonRecd.Y)

    """


    let fileSource2 = """
module N

type IntAbbrev = int

let bool2 = false

let testHashChar (x:char) = hash x
let testHashSByte (x:sbyte) = hash x
let testHashInt16 (x:int16) = hash x
let testHashInt64 (x:int64) = hash x
let testHashUInt64 (x:uint64) = hash x
let testHashIntPtr (x:nativeint) = hash x
let testHashUIntPtr (x:unativeint) = hash x

let testHashString (x:string) = hash x
let testTypeOf (x:'T) = typeof<'T>

let inline mutableVar x =
    if x > 0 then
        let mutable acc = x
        acc <- x

let inline mutableConst () =
    let mutable acc = ()
    acc <- ()

let testMutableVar = mutableVar 1
let testMutableConst = mutableConst ()
    """

    let createOptions() = createOptionsAux [fileSource1; fileSource2] []

    let operatorTests = """
module OperatorTests{0}

let test{0}EqualsOperator               (e1:{1}) (e2:{1}) = (=) e1 e2
let test{0}NotEqualsOperator            (e1:{1}) (e2:{1}) = (<>) e1 e2
let test{0}LessThanOperator             (e1:{1}) (e2:{1}) = (<) e1 e2
let test{0}LessThanOrEqualsOperator     (e1:{1}) (e2:{1}) = (<=) e1 e2
let test{0}GreaterThanOperator          (e1:{1}) (e2:{1}) = (>) e1 e2
let test{0}GreaterThanOrEqualsOperator  (e1:{1}) (e2:{1}) = (>=) e1 e2

let test{0}AdditionOperator     (e1:{1}) (e2:{1}) = (+) e1 e2
let test{0}SubtractionOperator  (e1:{1}) (e2:{1}) = (-) e1 e2
let test{0}MultiplyOperator     (e1:{1}) (e2:{1}) = (*) e1 e2
let test{0}DivisionOperator     (e1:{1}) (e2:{1}) = (/) e1 e2
let test{0}ModulusOperator      (e1:{1}) (e2:{1}) = (%) e1 e2
let test{0}BitwiseAndOperator   (e1:{1}) (e2:{1}) = (&&&) e1 e2
let test{0}BitwiseOrOperator    (e1:{1}) (e2:{1}) = (|||) e1 e2
let test{0}BitwiseXorOperator   (e1:{1}) (e2:{1}) = (^^^) e1 e2
let test{0}ShiftLeftOperator    (e1:{1}) (e2:int) = (<<<) e1 e2
let test{0}ShiftRightOperator   (e1:{1}) (e2:int) = (>>>) e1 e2

let test{0}UnaryNegOperator   (e1:{1}) = (~-) e1

let test{0}AdditionChecked    (e1:{1}) (e2:{1}) = Checked.(+) e1 e2
let test{0}SubtractionChecked (e1:{1}) (e2:{1}) = Checked.(-) e1 e2
let test{0}MultiplyChecked    (e1:{1}) (e2:{1}) = Checked.(*) e1 e2
let test{0}UnaryNegChecked    (e1:{1}) = Checked.(~-) e1

let test{0}ToByteChecked      (e1:{1}) = Checked.byte e1
let test{0}ToSByteChecked     (e1:{1}) = Checked.sbyte e1
let test{0}ToInt16Checked     (e1:{1}) = Checked.int16 e1
let test{0}ToUInt16Checked    (e1:{1}) = Checked.uint16 e1
let test{0}ToIntChecked       (e1:{1}) = Checked.int e1
let test{0}ToInt32Checked     (e1:{1}) = Checked.int32 e1
let test{0}ToUInt32Checked    (e1:{1}) = Checked.uint32 e1
let test{0}ToInt64Checked     (e1:{1}) = Checked.int64 e1
let test{0}ToUInt64Checked    (e1:{1}) = Checked.uint64 e1
let test{0}ToIntPtrChecked    (e1:{1}) = Checked.nativeint e1
let test{0}ToUIntPtrChecked   (e1:{1}) = Checked.unativeint e1

let test{0}ToByteOperator     (e1:{1}) = byte e1
let test{0}ToSByteOperator    (e1:{1}) = sbyte e1
let test{0}ToInt16Operator    (e1:{1}) = int16 e1
let test{0}ToUInt16Operator   (e1:{1}) = uint16 e1
let test{0}ToIntOperator      (e1:{1}) = int e1
let test{0}ToInt32Operator    (e1:{1}) = int32 e1
let test{0}ToUInt32Operator   (e1:{1}) = uint32 e1
let test{0}ToInt64Operator    (e1:{1}) = int64 e1
let test{0}ToUInt64Operator   (e1:{1}) = uint64 e1
let test{0}ToIntPtrOperator   (e1:{1}) = nativeint e1
let test{0}ToUIntPtrOperator  (e1:{1}) = unativeint e1
let test{0}ToSingleOperator   (e1:{1}) = float32 e1
let test{0}ToDoubleOperator   (e1:{1}) = float e1
let test{0}ToDecimalOperator  (e1:{1}) = decimal e1
let test{0}ToCharOperator     (e1:{1}) = char e1
let test{0}ToStringOperator   (e1:{1}) = string e1

"""

/// This test is run in unison with its optimized counterpart below
[<Test>]
let ``Test Unoptimized Declarations Project1`` () =
    let cleanup, options = Project1.createOptions()
    use _holder = cleanup
    let exprChecker = FSharpChecker.Create(keepAssemblyContents=true)
    let wholeProjectResults = exprChecker.ParseAndCheckProject(options) |> Async.RunSynchronously

    for e in wholeProjectResults.Diagnostics do
        printfn "Project1 error: <<<%s>>>" e.Message

    wholeProjectResults.Diagnostics.Length |> shouldEqual 3 // recursive value warning
    wholeProjectResults.Diagnostics.[0].Severity |> shouldEqual FSharpDiagnosticSeverity.Warning
    wholeProjectResults.Diagnostics.[1].Severity |> shouldEqual FSharpDiagnosticSeverity.Warning
    wholeProjectResults.Diagnostics.[2].Severity |> shouldEqual FSharpDiagnosticSeverity.Warning

    wholeProjectResults.AssemblyContents.ImplementationFiles.Length |> shouldEqual 2
    let file1 = wholeProjectResults.AssemblyContents.ImplementationFiles.[0]
    let file2 = wholeProjectResults.AssemblyContents.ImplementationFiles.[1]

    let expected =
        ["type M"; "type IntAbbrev"; "let boolEx1 = True @ (6,14--6,18)";
         "let intEx1 = 1 @ (7,13--7,14)"; "let int64Ex1 = 1 @ (8,15--8,17)";
         "let tupleEx1 = (1,1) @ (9,16--9,21)";
         "let tupleEx2 = (1,1,1) @ (10,16--10,25)";
         "let tupleEx3 = (1,1,1,1) @ (11,16--11,29)";
         "let localExample = let y: Microsoft.FSharp.Core.int = 1 in let z: Microsoft.FSharp.Core.int = 1 in (y,z) @ (14,7--14,8)";
         "let localGenericFunctionExample(unitVar0) = let y: Microsoft.FSharp.Core.int = 1 in let compiledAsLocalGenericFunction: 'a -> 'a = FUN ... -> fun x -> x in (compiledAsLocalGenericFunction<Microsoft.FSharp.Core.int> y,compiledAsLocalGenericFunction<Microsoft.FSharp.Core.float> 1) @ (19,7--19,8)";
         "let funcEx1(x) = x @ (23,23--23,24)";
         "let genericFuncEx1(x) = x @ (24,29--24,30)";
         "let topPair1b = M.patternInput@25 ().Item1 @ (25,4--25,26)";
         "let topPair1a = M.patternInput@25 ().Item0 @ (25,4--25,26)";
         "let testILCall1 = new Object() @ (27,18--27,27)";
         "let testILCall2 = Console.WriteLine (\"176\") @ (28,18--28,49)";
         "let recValNeverUsedAtRuntime = recValNeverUsedAtRuntime@31.Force<Microsoft.FSharp.Core.int>(()) @ (31,8--31,32)";
         "let recFuncIgnoresFirstArg(g) (v) = v @ (32,33--32,34)";
         "let testFun4(unitVar0) = let rec ... in recValNeverUsedAtRuntime @ (36,4--39,28)";
         "type ClassWithImplicitConstructor";
         "member .ctor(compiledAsArg) = (new Object(); (this.compiledAsArg <- compiledAsArg; (this.compiledAsField <- 1; let compiledAsLocal: Microsoft.FSharp.Core.int = 1 in let compiledAsLocal2: Microsoft.FSharp.Core.int = Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),compiledAsLocal,compiledAsLocal) in ()))) @ (41,5--41,33)";
         "member .cctor(unitVar) = (compiledAsStaticField <- 1; let compiledAsStaticLocal: Microsoft.FSharp.Core.int = 1 in let compiledAsStaticLocal2: Microsoft.FSharp.Core.int = Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),compiledAsStaticLocal,compiledAsStaticLocal) in ()) @ (49,11--49,40)";
         "member M1(__) (unitVar1) = Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),__.compiledAsField,let x: Microsoft.FSharp.Core.int = __.compiledAsField in __.compiledAsGenericInstanceMethod<Microsoft.FSharp.Core.int>(x)),__.compiledAsArg) @ (55,21--55,102)";
         "member M2(__) (unitVar1) = __.compiledAsInstanceMethod(()) @ (56,21--56,47)";
         "member SM1(unitVar0) = Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),compiledAsStaticField,let x: Microsoft.FSharp.Core.int = compiledAsStaticField in ClassWithImplicitConstructor.compiledAsGenericStaticMethod<Microsoft.FSharp.Core.int> (x)) @ (57,26--57,101)";
         "member SM2(unitVar0) = ClassWithImplicitConstructor.compiledAsStaticMethod (()) @ (58,26--58,50)";
         "member TestCallinToString(this) (unitVar1) = this.ToString() @ (60,39--60,54)";
         "type Error"; "let err = {Data0 = 3; Data1 = 4} @ (64,10--64,20)";
         "let matchOnException(err) = match (if err :? M.Error then $0 else $1) targets ... @ (66,33--66,36)";
         "let upwardForLoop(unitVar0) = let mutable a: Microsoft.FSharp.Core.int = 1 in (for-loop; a) @ (69,16--69,17)";
         "let upwardForLoop2(unitVar0) = let mutable a: Microsoft.FSharp.Core.int = 1 in (for-loop; a) @ (74,16--74,17)";
         "let downwardForLoop(unitVar0) = let mutable a: Microsoft.FSharp.Core.int = 1 in (for-loop; a) @ (79,16--79,17)";
         "let quotationTest1(unitVar0) = quote(Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),1,1)) @ (83,24--83,35)";
         "let quotationTest2(v) = quote(Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),ExtraTopLevelOperators.SpliceExpression<Microsoft.FSharp.Core.int> (v),1)) @ (84,24--84,36)";
         "type RecdType"; "type UnionType"; "type ClassWithEventsAndProperties";
         "member .ctor(unitVar0) = (new Object(); (this.ev <- new FSharpEvent`1(()); ())) @ (89,5--89,33)";
         "member .cctor(unitVar) = (sev <- new FSharpEvent`1(()); ()) @ (91,11--91,35)";
         "member get_InstanceProperty(x) (unitVar1) = (x.ev.Trigger(1); 1) @ (92,32--92,48)";
         "member get_StaticProperty(unitVar0) = (sev.Trigger(1); 1) @ (93,35--93,52)";
         "member get_InstanceEvent(x) (unitVar1) = x.ev.get_Publish(()) @ (94,29--94,39)";
         "member get_StaticEvent(x) (unitVar1) = sev.get_Publish(()) @ (95,27--95,38)";
         "let c = new ClassWithEventsAndProperties(()) @ (97,8--97,38)";
         "let v = M.c ().get_InstanceProperty(()) @ (98,8--98,26)";
         "do Console.WriteLine (\"777\")";
         "let functionWithSubmsumption(x) = IntrinsicFunctions.UnboxGeneric<Microsoft.FSharp.Core.string> (x) @ (102,40--102,52)";
         "type MultiArgMethods";
         "member .ctor(c,d) = (new Object(); ()) @ (105,5--105,20)";
         "member Method(x) (a,b) = 1 @ (106,37--106,38)";
         "member CurriedMethod(x) (a1,b1) (a2,b2) = 1 @ (107,63--107,64)";
         "let testFunctionThatCallsMultiArgMethods(unitVar0) = let m: M.MultiArgMethods = new MultiArgMethods(3,4) in Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),m.Method(7,8),fun tupledArg -> let arg00: Microsoft.FSharp.Core.int = tupledArg.Item0 in let arg01: Microsoft.FSharp.Core.int = tupledArg.Item1 in fun tupledArg -> let arg10: Microsoft.FSharp.Core.int = tupledArg.Item0 in let arg11: Microsoft.FSharp.Core.int = tupledArg.Item1 in m.CurriedMethod(arg00,arg01,arg10,arg11) (9,10) (11,12)) @ (110,8--110,9)";
         "let testFunctionThatUsesUnitsOfMeasure(x) (y) = Operators.op_Addition<Microsoft.FSharp.Core.float<'u>,Microsoft.FSharp.Core.float<'u>,Microsoft.FSharp.Core.float<'u>> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.float<'u>,Microsoft.FSharp.Core.float<'u>,Microsoft.FSharp.Core.float<'u>> (arg0_0,arg1_0),x,y) @ (122,70--122,75)";
         "let testFunctionThatUsesAddressesAndByrefs(x) = let mutable w: Microsoft.FSharp.Core.int = 4 in let y1: Microsoft.FSharp.Core.byref<Microsoft.FSharp.Core.int> = x in let y2: Microsoft.FSharp.Core.byref<Microsoft.FSharp.Core.int> = &w in let arr: Microsoft.FSharp.Core.int Microsoft.FSharp.Core.[] = [|3; 4|] in let r: Microsoft.FSharp.Core.int Microsoft.FSharp.Core.ref = Operators.Ref<Microsoft.FSharp.Core.int> (3) in let y3: Microsoft.FSharp.Core.byref<Microsoft.FSharp.Core.int> = [I_ldelema (NormalAddress, false, ILArrayShape [(Some 0, None)], !0)](arr,0) in let y4: Microsoft.FSharp.Core.byref<Microsoft.FSharp.Core.int> = &r.contents in let z: Microsoft.FSharp.Core.int = Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),x,y1),y2),y3) in (w <- 3; (x <- 4; (y2 <- 4; (y3 <- 5; Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),z,x),y1),y2),y3),y4),IntrinsicFunctions.GetArray<Microsoft.FSharp.Core.int> (arr,0)),r.contents))))) @ (125,16--125,17)";
         "let testFunctionThatUsesStructs1(dt) = dt.AddDays(3) @ (139,57--139,72)";
         "let testFunctionThatUsesStructs2(unitVar0) = let dt1: System.DateTime = DateTime.get_Now () in let mutable dt2: System.DateTime = DateTime.get_Now () in let dt3: System.TimeSpan = Operators.op_Subtraction<System.DateTime,System.DateTime,System.TimeSpan> (fun arg0_0 -> fun arg1_0 -> DateTime.op_Subtraction (arg0_0,arg1_0),dt1,dt2) in let dt4: System.DateTime = dt1.AddDays(3) in let dt5: Microsoft.FSharp.Core.int = dt1.get_Millisecond() in let dt6: Microsoft.FSharp.Core.byref<System.DateTime> = &dt2 in let dt7: System.TimeSpan = Operators.op_Subtraction<System.DateTime,System.DateTime,System.TimeSpan> (fun arg0_0 -> fun arg1_0 -> DateTime.op_Subtraction (arg0_0,arg1_0),dt6,dt4) in dt7 @ (142,7--142,10)";
         "let testFunctionThatUsesWhileLoop(unitVar0) = let mutable x: Microsoft.FSharp.Core.int = 1 in (while Operators.op_LessThan<Microsoft.FSharp.Core.int> (x,100) do x <- Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),x,1) done; x) @ (152,15--152,16)";
         "let testFunctionThatUsesTryWith(unitVar0) = try M.testFunctionThatUsesWhileLoop (()) with matchValue -> match (if matchValue :? System.ArgumentException then $0 else $1) targets ... @ (158,3--160,60)";
         "let testFunctionThatUsesTryFinally(unitVar0) = try M.testFunctionThatUsesWhileLoop (()) finally Console.WriteLine (\"8888\") @ (164,3--167,37)";
         "member Console.WriteTwoLines.Static(unitVar0) = (Console.WriteLine (); Console.WriteLine ()) @ (170,36--170,90)";
         "member DateTime.get_TwoMinute(x) (unitVar1) = Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),x.get_Minute(),x.get_Minute()) @ (173,25--173,44)";
         "let testFunctionThatUsesExtensionMembers(unitVar0) = (M.Console.WriteTwoLines.Static (()); let v: Microsoft.FSharp.Core.int = DateTime.get_Now ().DateTime.get_TwoMinute(()) in M.Console.WriteTwoLines.Static (())) @ (176,3--178,33)";
         "let testFunctionThatUsesOptionMembers(unitVar0) = let x: Microsoft.FSharp.Core.int Microsoft.FSharp.Core.option = Some(3) in (x.get_IsSome() (),x.get_IsNone() ()) @ (181,7--181,8)";
         "let testFunctionThatUsesOverAppliedFunction(unitVar0) = Operators.Identity<Microsoft.FSharp.Core.int -> Microsoft.FSharp.Core.int> (fun x -> Operators.Identity<Microsoft.FSharp.Core.int> (x)) 3 @ (185,3--185,10)";
         "let testFunctionThatUsesPatternMatchingOnLists(x) = match (if x.Isop_ColonColon then (if x.Tail.Isop_ColonColon then (if x.Tail.Tail.Isop_Nil then $2 else $3) else $1) else $0) targets ... @ (188,10--188,11)";
         "let testFunctionThatUsesPatternMatchingOnOptions(x) = match (if x.IsSome then $1 else $0) targets ... @ (195,10--195,11)";
         "let testFunctionThatUsesPatternMatchingOnOptions2(x) = match (if x.IsSome then $1 else $0) targets ... @ (200,10--200,11)";
         "let testFunctionThatUsesConditionalOnOptions2(x) = (if x.get_IsSome() () then 1 else 2) @ (205,4--205,29)";
         "let f(x) (y) = Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),x,y) @ (207,12--207,15)";
         "let g = let x: Microsoft.FSharp.Core.int = 1 in fun y -> M.f (x,y) @ (208,8--208,11)";
         "let h = Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),M.g () 2,3) @ (209,8--209,17)";
         "type TestFuncProp";
         "member .ctor(unitVar0) = (new Object(); ()) @ (211,5--211,17)";
         "member get_Id(this) (unitVar1) = fun x -> x @ (212,21--212,31)";
         "let wrong = Operators.op_Equality<Microsoft.FSharp.Core.int> (new TestFuncProp(()).get_Id(()) 0,0) @ (214,12--214,35)";
         "let start(name) = (name,name) @ (217,4--217,14)";
         "let last(name,values) = Operators.Identity<Microsoft.FSharp.Core.string * Microsoft.FSharp.Core.string> ((name,values)) @ (220,4--220,21)";
         "let last2(name) = Operators.Identity<Microsoft.FSharp.Core.string> (name) @ (223,4--223,11)";
         "let test7(s) = Operators.op_PipeRight<Microsoft.FSharp.Core.string * Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.string * Microsoft.FSharp.Core.string> (M.start (s),fun tupledArg -> let name: Microsoft.FSharp.Core.string = tupledArg.Item0 in let values: Microsoft.FSharp.Core.string = tupledArg.Item1 in M.last (name,values)) @ (226,4--226,19)";
         "let test8(unitVar0) = fun tupledArg -> let name: Microsoft.FSharp.Core.string = tupledArg.Item0 in let values: Microsoft.FSharp.Core.string = tupledArg.Item1 in M.last (name,values) @ (229,4--229,8)";
         "let test9(s) = Operators.op_PipeRight<Microsoft.FSharp.Core.string * Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.string * Microsoft.FSharp.Core.string> ((s,s),fun tupledArg -> let name: Microsoft.FSharp.Core.string = tupledArg.Item0 in let values: Microsoft.FSharp.Core.string = tupledArg.Item1 in M.last (name,values)) @ (232,4--232,17)";
         "let test10(unitVar0) = fun name -> M.last2 (name) @ (235,4--235,9)";
         "let test11(s) = Operators.op_PipeRight<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.string> (s,fun name -> M.last2 (name)) @ (238,4--238,14)";
         "let badLoop = badLoop@240.Force<Microsoft.FSharp.Core.int -> Microsoft.FSharp.Core.int>(()) @ (240,8--240,15)";
         "type LetLambda";
         "let f = ((); fun a -> fun b -> Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),a,b)) @ (246,8--247,24)";
         "let letLambdaRes = Operators.op_PipeRight<(Microsoft.FSharp.Core.int * Microsoft.FSharp.Core.int) Microsoft.FSharp.Collections.list,Microsoft.FSharp.Core.int Microsoft.FSharp.Collections.list> (Cons((1,2),Empty()),let mapping: Microsoft.FSharp.Core.int * Microsoft.FSharp.Core.int -> Microsoft.FSharp.Core.int = fun tupledArg -> let a: Microsoft.FSharp.Core.int = tupledArg.Item0 in let b: Microsoft.FSharp.Core.int = tupledArg.Item1 in (LetLambda.f () a) b in fun list -> ListModule.Map<Microsoft.FSharp.Core.int * Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (mapping,list)) @ (249,19--249,71)";
         "let anonRecd = {X = 1; Y = 2} @ (251,15--251,33)";
         "let anonRecdGet = (M.anonRecd ().X,M.anonRecd ().Y) @ (252,19--252,41)"]
    let expected2 =
        ["type N"; "type IntAbbrev"; "let bool2 = False @ (6,12--6,17)";
         "let testHashChar(x) = Operators.Hash<Microsoft.FSharp.Core.char> (x) @ (8,28--8,34)";
         "let testHashSByte(x) = Operators.Hash<Microsoft.FSharp.Core.sbyte> (x) @ (9,30--9,36)";
         "let testHashInt16(x) = Operators.Hash<Microsoft.FSharp.Core.int16> (x) @ (10,30--10,36)";
         "let testHashInt64(x) = Operators.Hash<Microsoft.FSharp.Core.int64> (x) @ (11,30--11,36)";
         "let testHashUInt64(x) = Operators.Hash<Microsoft.FSharp.Core.uint64> (x) @ (12,32--12,38)";
         "let testHashIntPtr(x) = Operators.Hash<Microsoft.FSharp.Core.nativeint> (x) @ (13,35--13,41)";
         "let testHashUIntPtr(x) = Operators.Hash<Microsoft.FSharp.Core.unativeint> (x) @ (14,37--14,43)";
         "let testHashString(x) = Operators.Hash<Microsoft.FSharp.Core.string> (x) @ (16,32--16,38)";
         "let testTypeOf(x) = Operators.TypeOf<'T> () @ (17,24--17,30)";
         "let mutableVar(x) = (if Operators.op_GreaterThan<Microsoft.FSharp.Core.int> (x,0) then let mutable acc: Microsoft.FSharp.Core.int = x in acc <- x else ()) @ (20,4--22,16)";
         "let mutableConst(unitVar0) = let mutable acc: Microsoft.FSharp.Core.unit = () in acc <- () @ (25,16--25,19)";
         "let testMutableVar = N.mutableVar (1) @ (28,21--28,33)";
         "let testMutableConst = N.mutableConst (()) @ (29,23--29,38)"]

    printfn "// unoptimized"
    printfn "let expected =\n%A" (printDeclarations None (List.ofSeq file1.Declarations) |> Seq.toList)
    printfn "let expected2 =\n%A" (printDeclarations None (List.ofSeq file2.Declarations) |> Seq.toList)
    printDeclarations None (List.ofSeq file1.Declarations)
      |> Seq.toList
      |> Utils.filterHack
      |> shouldPairwiseEqual (Utils.filterHack expected)

    printDeclarations None (List.ofSeq file2.Declarations)
      |> Seq.toList
      |> Utils.filterHack
      |> shouldPairwiseEqual (Utils.filterHack expected2)

    ()

[<Test>]
let ``Test Optimized Declarations Project1`` () =
    let cleanup, options = Project1.createOptions()
    use _holder = cleanup
    let exprChecker = FSharpChecker.Create(keepAssemblyContents=true)
    let wholeProjectResults = exprChecker.ParseAndCheckProject(options) |> Async.RunSynchronously

    for e in wholeProjectResults.Diagnostics do
        printfn "Project1 error: <<<%s>>>" e.Message

    wholeProjectResults.Diagnostics.Length |> shouldEqual 3 // recursive value warning
    wholeProjectResults.Diagnostics.[0].Severity |> shouldEqual FSharpDiagnosticSeverity.Warning
    wholeProjectResults.Diagnostics.[1].Severity |> shouldEqual FSharpDiagnosticSeverity.Warning
    wholeProjectResults.Diagnostics.[2].Severity |> shouldEqual FSharpDiagnosticSeverity.Warning

    wholeProjectResults.GetOptimizedAssemblyContents().ImplementationFiles.Length |> shouldEqual 2
    let file1 = wholeProjectResults.GetOptimizedAssemblyContents().ImplementationFiles.[0]
    let file2 = wholeProjectResults.GetOptimizedAssemblyContents().ImplementationFiles.[1]

    let expected =
        ["type M"; "type IntAbbrev"; "let boolEx1 = True @ (6,14--6,18)";
         "let intEx1 = 1 @ (7,13--7,14)"; "let int64Ex1 = 1 @ (8,15--8,17)";
         "let tupleEx1 = (1,1) @ (9,16--9,21)";
         "let tupleEx2 = (1,1,1) @ (10,16--10,25)";
         "let tupleEx3 = (1,1,1,1) @ (11,16--11,29)";
         "let localExample = let y: Microsoft.FSharp.Core.int = 1 in let z: Microsoft.FSharp.Core.int = 1 in (y,z) @ (14,7--14,8)";
         "let localGenericFunctionExample(unitVar0) = let y: Microsoft.FSharp.Core.int = 1 in let compiledAsLocalGenericFunction: 'a -> 'a = FUN ... -> fun x -> x in (compiledAsLocalGenericFunction<Microsoft.FSharp.Core.int> y,compiledAsLocalGenericFunction<Microsoft.FSharp.Core.float> 1) @ (19,7--19,8)";
         "let funcEx1(x) = x @ (23,23--23,24)";
         "let genericFuncEx1(x) = x @ (24,29--24,30)";
         "let topPair1b = M.patternInput@25 ().Item1 @ (25,4--25,26)";
         "let topPair1a = M.patternInput@25 ().Item0 @ (25,4--25,26)";
         "let testILCall1 = new Object() @ (27,18--27,27)";
         "let testILCall2 = Console.WriteLine (\"176\") @ (28,18--28,49)";
         "let recValNeverUsedAtRuntime = recValNeverUsedAtRuntime@31.Force<Microsoft.FSharp.Core.int>(()) @ (31,8--31,32)";
         "let recFuncIgnoresFirstArg(g) (v) = v @ (32,33--32,34)";
         "let testFun4(unitVar0) = let rec ... in recValNeverUsedAtRuntime @ (36,4--39,28)";
         "type ClassWithImplicitConstructor";
         "member .ctor(compiledAsArg) = (new Object(); (this.compiledAsArg <- compiledAsArg; (this.compiledAsField <- 1; let compiledAsLocal: Microsoft.FSharp.Core.int = 1 in let compiledAsLocal2: Microsoft.FSharp.Core.int = Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),compiledAsLocal,compiledAsLocal) in ()))) @ (41,5--41,33)";
         "member .cctor(unitVar) = (compiledAsStaticField <- 1; let compiledAsStaticLocal: Microsoft.FSharp.Core.int = 1 in let compiledAsStaticLocal2: Microsoft.FSharp.Core.int = Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),compiledAsStaticLocal,compiledAsStaticLocal) in ()) @ (49,11--49,40)";
         "member M1(__) (unitVar1) = Operators.op_Addition<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),__.compiledAsField,__.compiledAsGenericInstanceMethod<Microsoft.FSharp.Core.int>(__.compiledAsField)),__.compiledAsArg) @ (55,21--55,102)";
         "member M2(__) (unitVar1) = __.compiledAsInstanceMethod(()) @ (56,21--56,47)";
         "member SM1(unitVar0) = Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),compiledAsStaticField,ClassWithImplicitConstructor.compiledAsGenericStaticMethod<Microsoft.FSharp.Core.int> (compiledAsStaticField)) @ (57,26--57,101)";
         "member SM2(unitVar0) = ClassWithImplicitConstructor.compiledAsStaticMethod (()) @ (58,26--58,50)";
         "member TestCallinToString(this) (unitVar1) = this.ToString() @ (60,39--60,54)";
         "type Error"; "let err = {Data0 = 3; Data1 = 4} @ (64,10--64,20)";
         "let matchOnException(err) = match (if err :? M.Error then $0 else $1) targets ... @ (66,33--66,36)";
         "let upwardForLoop(unitVar0) = let mutable a: Microsoft.FSharp.Core.int = 1 in (for-loop; a) @ (69,16--69,17)";
         "let upwardForLoop2(unitVar0) = let mutable a: Microsoft.FSharp.Core.int = 1 in (for-loop; a) @ (74,16--74,17)";
         "let downwardForLoop(unitVar0) = let mutable a: Microsoft.FSharp.Core.int = 1 in (for-loop; a) @ (79,16--79,17)";
         "let quotationTest1(unitVar0) = quote(Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),1,1)) @ (83,24--83,35)";
         "let quotationTest2(v) = quote(Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),ExtraTopLevelOperators.SpliceExpression<Microsoft.FSharp.Core.int> (v),1)) @ (84,24--84,36)";
         "type RecdType"; "type UnionType"; "type ClassWithEventsAndProperties";
         "member .ctor(unitVar0) = (new Object(); (this.ev <- new FSharpEvent`1(()); ())) @ (89,5--89,33)";
         "member .cctor(unitVar) = (sev <- new FSharpEvent`1(()); ()) @ (91,11--91,35)";
         "member get_InstanceProperty(x) (unitVar1) = (x.ev.Trigger(1); 1) @ (92,32--92,48)";
         "member get_StaticProperty(unitVar0) = (sev.Trigger(1); 1) @ (93,35--93,52)";
         "member get_InstanceEvent(x) (unitVar1) = x.ev.get_Publish(()) @ (94,29--94,39)";
         "member get_StaticEvent(x) (unitVar1) = sev.get_Publish(()) @ (95,27--95,38)";
         "let c = new ClassWithEventsAndProperties(()) @ (97,8--97,38)";
         "let v = M.c ().get_InstanceProperty(()) @ (98,8--98,26)";
         "do Console.WriteLine (\"777\")";
         "let functionWithSubmsumption(x) = IntrinsicFunctions.UnboxGeneric<Microsoft.FSharp.Core.string> (x) @ (102,40--102,52)";
         "type MultiArgMethods";
         "member .ctor(c,d) = (new Object(); ()) @ (105,5--105,20)";
         "member Method(x) (a,b) = 1 @ (106,37--106,38)";
         "member CurriedMethod(x) (a1,b1) (a2,b2) = 1 @ (107,63--107,64)";
         "let testFunctionThatCallsMultiArgMethods(unitVar0) = let m: M.MultiArgMethods = new MultiArgMethods(3,4) in Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),m.Method(7,8),let arg00: Microsoft.FSharp.Core.int = 9 in let arg01: Microsoft.FSharp.Core.int = 10 in let arg10: Microsoft.FSharp.Core.int = 11 in let arg11: Microsoft.FSharp.Core.int = 12 in m.CurriedMethod(arg00,arg01,arg10,arg11)) @ (110,8--110,9)";
         "let testFunctionThatUsesUnitsOfMeasure(x) (y) = Operators.op_Addition<Microsoft.FSharp.Core.float<'u>,Microsoft.FSharp.Core.float<'u>,Microsoft.FSharp.Core.float<'u>> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.float<'u>,Microsoft.FSharp.Core.float<'u>,Microsoft.FSharp.Core.float<'u>> (arg0_0,arg1_0),x,y) @ (122,70--122,75)";
         "let testFunctionThatUsesAddressesAndByrefs(x) = let mutable w: Microsoft.FSharp.Core.int = 4 in let y1: Microsoft.FSharp.Core.byref<Microsoft.FSharp.Core.int> = x in let y2: Microsoft.FSharp.Core.byref<Microsoft.FSharp.Core.int> = &w in let arr: Microsoft.FSharp.Core.int Microsoft.FSharp.Core.[] = [|3; 4|] in let r: Microsoft.FSharp.Core.int Microsoft.FSharp.Core.ref = Operators.Ref<Microsoft.FSharp.Core.int> (3) in let y3: Microsoft.FSharp.Core.byref<Microsoft.FSharp.Core.int> = [I_ldelema (NormalAddress, false, ILArrayShape [(Some 0, None)], !0)](arr,0) in let y4: Microsoft.FSharp.Core.byref<Microsoft.FSharp.Core.int> = &r.contents in let z: Microsoft.FSharp.Core.int = Operators.op_Addition<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),Operators.op_Addition<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),x,y1),y2),y3) in (w <- 3; (x <- 4; (y2 <- 4; (y3 <- 5; Operators.op_Addition<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),Operators.op_Addition<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),Operators.op_Addition<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),Operators.op_Addition<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),Operators.op_Addition<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),Operators.op_Addition<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),z,x),y1),y2),y3),y4),IntrinsicFunctions.GetArray<Microsoft.FSharp.Core.int> (arr,0)),r.contents))))) @ (125,16--125,17)";
         "let testFunctionThatUsesStructs1(dt) = dt.AddDays(3) @ (139,57--139,72)";
         "let testFunctionThatUsesStructs2(unitVar0) = let dt1: System.DateTime = DateTime.get_Now () in let mutable dt2: System.DateTime = DateTime.get_Now () in let dt3: System.TimeSpan = DateTime.op_Subtraction (dt1,dt2) in let dt4: System.DateTime = dt1.AddDays(3) in let dt5: Microsoft.FSharp.Core.int = dt1.get_Millisecond() in let dt6: Microsoft.FSharp.Core.byref<System.DateTime> = &dt2 in let dt7: System.TimeSpan = DateTime.op_Subtraction (dt6,dt4) in dt7 @ (142,7--142,10)";
         "let testFunctionThatUsesWhileLoop(unitVar0) = let mutable x: Microsoft.FSharp.Core.int = 1 in (while Operators.op_LessThan<Microsoft.FSharp.Core.int> (x,100) do x <- Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),x,1) done; x) @ (152,15--152,16)";
         "let testFunctionThatUsesTryWith(unitVar0) = try M.testFunctionThatUsesWhileLoop (()) with matchValue -> match (if matchValue :? System.ArgumentException then $0 else $1) targets ... @ (158,3--160,60)";
         "let testFunctionThatUsesTryFinally(unitVar0) = try M.testFunctionThatUsesWhileLoop (()) finally Console.WriteLine (\"8888\") @ (164,3--167,37)";
         "member Console.WriteTwoLines.Static(unitVar0) = (Console.WriteLine (); Console.WriteLine ()) @ (170,36--170,90)";
         "member DateTime.get_TwoMinute(x) (unitVar1) = Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),x.get_Minute(),x.get_Minute()) @ (173,25--173,44)";
         "let testFunctionThatUsesExtensionMembers(unitVar0) = (M.Console.WriteTwoLines.Static (()); let v: Microsoft.FSharp.Core.int = DateTime.get_Now ().DateTime.get_TwoMinute(()) in M.Console.WriteTwoLines.Static (())) @ (176,3--178,33)";
         "let testFunctionThatUsesOptionMembers(unitVar0) = let x: Microsoft.FSharp.Core.int Microsoft.FSharp.Core.option = Some(3) in (x.get_IsSome() (),x.get_IsNone() ()) @ (181,7--181,8)";
         "let testFunctionThatUsesOverAppliedFunction(unitVar0) = Operators.Identity<Microsoft.FSharp.Core.int -> Microsoft.FSharp.Core.int> (fun x -> Operators.Identity<Microsoft.FSharp.Core.int> (x)) 3 @ (185,3--185,10)";
         "let testFunctionThatUsesPatternMatchingOnLists(x) = match (if x.Isop_ColonColon then (if x.Tail.Isop_ColonColon then (if x.Tail.Tail.Isop_Nil then $2 else $3) else $1) else $0) targets ... @ (188,10--188,11)";
         "let testFunctionThatUsesPatternMatchingOnOptions(x) = match (if x.IsSome then $1 else $0) targets ... @ (195,10--195,11)";
         "let testFunctionThatUsesPatternMatchingOnOptions2(x) = match (if x.IsSome then $1 else $0) targets ... @ (200,10--200,11)";
         "let testFunctionThatUsesConditionalOnOptions2(x) = (if x.get_IsSome() () then 1 else 2) @ (205,4--205,29)";
         "let f(x) (y) = Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),x,y) @ (207,12--207,15)";
         "let g = let x: Microsoft.FSharp.Core.int = 1 in fun y -> M.f (x,y) @ (208,8--208,11)";
         "let h = Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),M.g () 2,3) @ (209,8--209,17)";
         "type TestFuncProp";
         "member .ctor(unitVar0) = (new Object(); ()) @ (211,5--211,17)";
         "member get_Id(this) (unitVar1) = fun x -> x @ (212,21--212,31)";
         "let wrong = Operators.op_Equality<Microsoft.FSharp.Core.int> (new TestFuncProp(()).get_Id(()) 0,0) @ (214,12--214,35)";
         "let start(name) = (name,name) @ (217,4--217,14)";
         "let last(name,values) = Operators.Identity<Microsoft.FSharp.Core.string * Microsoft.FSharp.Core.string> ((name,values)) @ (220,4--220,21)";
         "let last2(name) = Operators.Identity<Microsoft.FSharp.Core.string> (name) @ (223,4--223,11)";
         "let test7(s) = let tupledArg: Microsoft.FSharp.Core.string * Microsoft.FSharp.Core.string = M.start (s) in let name: Microsoft.FSharp.Core.string = tupledArg.Item0 in let values: Microsoft.FSharp.Core.string = tupledArg.Item1 in M.last (name,values) @ (226,4--226,19)";
         "let test8(unitVar0) = fun tupledArg -> let name: Microsoft.FSharp.Core.string = tupledArg.Item0 in let values: Microsoft.FSharp.Core.string = tupledArg.Item1 in M.last (name,values) @ (229,4--229,8)";
         "let test9(s) = M.last (s,s) @ (232,4--232,17)";
         "let test10(unitVar0) = fun name -> M.last2 (name) @ (235,4--235,9)";
         "let test11(s) = M.last2 (s) @ (238,4--238,14)";
         "let badLoop = badLoop@240.Force<Microsoft.FSharp.Core.int -> Microsoft.FSharp.Core.int>(()) @ (240,8--240,15)";
         "type LetLambda";
         "let f = fun a -> fun b -> Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),a,b) @ (247,8--247,24)";
         "let letLambdaRes = ListModule.Map<Microsoft.FSharp.Core.int * Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun tupledArg -> let a: Microsoft.FSharp.Core.int = tupledArg.Item0 in let b: Microsoft.FSharp.Core.int = tupledArg.Item1 in (LetLambda.f () a) b,Cons((1,2),Empty())) @ (249,19--249,71)";
         "let anonRecd = {X = 1; Y = 2} @ (251,15--251,33)";
         "let anonRecdGet = (M.anonRecd ().X,M.anonRecd ().Y) @ (252,19--252,41)"]
    let expected2 =
        ["type N"; "type IntAbbrev"; "let bool2 = False @ (6,12--6,17)";
         "let testHashChar(x) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),Operators.op_LeftShift<Microsoft.FSharp.Core.char> (x,16),x) @ (8,28--8,34)";
         "let testHashSByte(x) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),Operators.op_LeftShift<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),x,8),x) @ (9,30--9,36)";
         "let testHashInt16(x) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),Operators.ToUInt16<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.uint16> (arg0_0),x),Operators.op_LeftShift<Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),x,16)) @ (10,30--10,36)";
         "let testHashInt64(x) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),Operators.ToInt32<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int32> (arg0_0),x),Operators.ToInt32<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int32> (arg0_0),Operators.op_RightShift<Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),x,32))) @ (11,30--11,36)";
         "let testHashUInt64(x) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),Operators.ToInt32<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int32> (arg0_0),x),Operators.ToInt32<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int32> (arg0_0),Operators.op_RightShift<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),x,32))) @ (12,32--12,38)";
         "let testHashIntPtr(x) = Operators.ToInt32<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int32> (arg0_0),Operators.ToUInt64<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.uint64> (arg0_0),x)) @ (13,35--13,41)";
         "let testHashUIntPtr(x) = Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),Operators.ToInt32<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int32> (arg0_0),Operators.ToUInt64<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.uint64> (arg0_0),x)),2147483647) @ (14,37--14,43)";
         "let testHashString(x) = (if Operators.op_Equality<Microsoft.FSharp.Core.string> (x,dflt) then 0 else x.GetHashCode()) @ (16,32--16,38)";
         "let testTypeOf(x) = Operators.TypeOf<'T> () @ (17,24--17,30)";
         "let mutableVar(x) = (if Operators.op_GreaterThan<Microsoft.FSharp.Core.int> (x,0) then let mutable acc: Microsoft.FSharp.Core.int = x in acc <- x else ()) @ (20,4--22,16)";
         "let mutableConst(unitVar0) = let mutable acc: Microsoft.FSharp.Core.unit = () in acc <- () @ (25,16--25,19)";
         "let testMutableVar = let x: Microsoft.FSharp.Core.int = 1 in (if Operators.op_GreaterThan<Microsoft.FSharp.Core.int> (x,0) then let mutable acc: Microsoft.FSharp.Core.int = x in acc <- x else ()) @ (28,21--28,33)";
         "let testMutableConst = let mutable acc: Microsoft.FSharp.Core.unit = () in acc <- () @ (29,23--29,38)"]

    // printFSharpDecls "" file2.Declarations |> Seq.iter (printfn "%s")
    printfn "// optimized"
    printfn "let expected =\n%A" (printDeclarations None (List.ofSeq file1.Declarations) |> Seq.toList)
    printfn "let expected2 =\n%A" (printDeclarations None (List.ofSeq file2.Declarations) |> Seq.toList)
    printDeclarations None (List.ofSeq file1.Declarations)
      |> Seq.toList
      |> Utils.filterHack
      |> shouldPairwiseEqual (Utils.filterHack expected)

    printDeclarations None (List.ofSeq file2.Declarations)
      |> Seq.toList
      |> Utils.filterHack
      |> shouldPairwiseEqual (Utils.filterHack expected2)

    ()

let testOperators dnName fsName excludedTests expectedUnoptimized expectedOptimized =

    let tempFileName = Utils.getTempFileName()
    let filePath = Utils.getTempFilePathChangeExt tempFileName ".fs"
    let dllPath =Utils.getTempFilePathChangeExt tempFileName ".dll"
    let projFilePath = Utils.getTempFilePathChangeExt tempFileName ".fsproj"
    let exprChecker = FSharpChecker.Create(keepAssemblyContents=true)

    begin
        use _cleanup = Utils.cleanupTempFiles [filePath; dllPath; projFilePath]
        createTempDir()
        let source = System.String.Format(Project1.operatorTests, dnName, fsName)
        let replace (s:string) r = s.Replace("let " + r, "// let " + r)
        let fileSource = excludedTests |> List.fold replace source
        FileSystem.OpenFileForWriteShim(filePath).Write(fileSource)

        let args = mkProjectCommandLineArgsSilent (dllPath, [filePath])

        let options =  checker.GetProjectOptionsFromCommandLineArgs (projFilePath, args)

        let wholeProjectResults = exprChecker.ParseAndCheckProject(options) |> Async.RunSynchronously
        let referencedAssemblies = wholeProjectResults.ProjectContext.GetReferencedAssemblies()
        let currentAssemblyToken =
            let fsCore = referencedAssemblies |> List.tryFind (fun asm -> asm.SimpleName = "FSharp.Core")
            match fsCore with
            | Some core ->
                if core.QualifiedName.StartsWith("FSharp.Core, Version=5.0") then FC50
                elif core.QualifiedName.StartsWith("FSharp.Core, Version=4.7") then  FC47
                elif core.QualifiedName.StartsWith("FSharp.Core, Version=4.6") then FC46
                else FC45
            | None -> FC45

        for r in referencedAssemblies do
            printfn "Referenced assembly %s: %O" r.QualifiedName r.FileName

        let errors = StringBuilder()
        for e in wholeProjectResults.Diagnostics do
            printfn "%s Operator Tests error: <<<%s>>>" dnName e.Message
            errors.AppendLine e.Message |> ignore

        errors.ToString() |> shouldEqual ""
        wholeProjectResults.Diagnostics.Length |> shouldEqual 0

        let resultUnoptimized =
            wholeProjectResults.AssemblyContents.ImplementationFiles.[0].Declarations
            |> printDeclarations None
            |> Seq.toList

        let resultOptimized =
            wholeProjectResults.GetOptimizedAssemblyContents().ImplementationFiles.[0].Declarations
            |> printDeclarations None
            |> Seq.toList

        printfn "    let expectedUnoptimized = ["
        for s in resultUnoptimized do
                printfn "        [], %A" s
        printfn "      ]"

        printfn "    let expectedOptimized = ["
        for s in resultOptimized do
                printfn "        [%s], %A" (if s.Contains("ToStringOperator") then "FC47; FC50" else "") s
        printfn "      ]"

        let mutable countFC45 = 0
        let mutable countFC46 = 0
        let mutable countFC47 = 0
        let mutable countFC50 = 0

        /// Filter for allowed FSharp.Core definition. Optimizations can differ between Core versions
        let filterTests result expected =
            List.zip result expected
            |> List.choose (fun (result, (when', s)) ->
                if List.isEmpty when' then
                    countFC45 <- countFC45 + 1
                    countFC46 <- countFC46 + 1
                    countFC47 <- countFC47 + 1
                    countFC50 <- countFC50 + 1
                    Some(result, s)
                else
                    if when' |> List.contains FC45 then countFC45 <- countFC45 + 1
                    if when' |> List.contains FC46 then countFC46 <- countFC46 + 1
                    if when' |> List.contains FC47 then countFC47 <- countFC47 + 1
                    if when' |> List.contains FC50 then countFC50 <- countFC50 + 1
                    if when' |> List.contains currentAssemblyToken then
                        Some(result, s)
                    else
                        None)
            |> List.unzip

        printfn ""
        printfn "Running in %O mode (%s)" currentAssemblyToken (FSharpCore.fsharpVersion currentAssemblyToken)

        let resultUnoptFiltered, expectedUnoptFiltered = filterTests resultUnoptimized expectedUnoptimized
        printfn "Unoptimized FC45 tests: %i, FC46 tests: %i, FC47 tests: %i, FC50 tests: %i" countFC45 countFC46 countFC47 countFC50
        printfn "Unfiltered unoptimized: %i, filtered: %i" (List.length expectedUnoptimized) (List.length expectedUnoptFiltered)

        countFC45 <- 0
        countFC46 <- 0
        countFC47 <- 0
        countFC50 <- 0
        let resultOptFiltered, expectedOptFiltered = filterTests resultOptimized expectedOptimized
        printfn "Optimized FC45 tests: %i, FC46 tests: %i, FC47 tests: %i, FC50 tests: %i" countFC45 countFC46 countFC47 countFC50
        printfn "Unfiltered optimized: %i, filtered: %i" (List.length expectedOptimized) (List.length expectedOptFiltered)

        // fail test on first line that fails, show difference in output window
        resultUnoptFiltered
        |> shouldPairwiseEqual expectedUnoptFiltered

        // fail test on first line that fails, show difference in output window
        resultOptFiltered
        |> shouldPairwiseEqual expectedOptFiltered
    end

[<Test>]
let ``Test Operator Declarations for Byte`` () =
    let excludedTests = [
        "testByteUnaryNegOperator";
        "testByteUnaryNegChecked";
      ]

    let expectedUnoptimized = [
        [], "type OperatorTestsByte"
        [], "let testByteEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.byte> (e1,e2) @ (4,63--4,72)"
        [], "let testByteNotEqualsOperator(e1) (e2) = Operators.op_Inequality<Microsoft.FSharp.Core.byte> (e1,e2) @ (5,63--5,73)"
        [], "let testByteLessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.byte> (e1,e2) @ (6,63--6,72)"
        [], "let testByteLessThanOrEqualsOperator(e1) (e2) = Operators.op_LessThanOrEqual<Microsoft.FSharp.Core.byte> (e1,e2) @ (7,63--7,73)"
        [], "let testByteGreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.byte> (e1,e2) @ (8,63--8,72)"
        [], "let testByteGreaterThanOrEqualsOperator(e1) (e2) = Operators.op_GreaterThanOrEqual<Microsoft.FSharp.Core.byte> (e1,e2) @ (9,63--9,73)"
        [], "let testByteAdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2) @ (11,55--11,64)"
        [], "let testByteSubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2) @ (12,55--12,64)"
        [], "let testByteMultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2) @ (13,55--13,64)"
        [], "let testByteDivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2) @ (14,55--14,64)"
        [], "let testByteModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2) @ (15,55--15,64)"
        [], "let testByteBitwiseAndOperator(e1) (e2) = Operators.op_BitwiseAnd<Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2) @ (16,55--16,66)"
        [], "let testByteBitwiseOrOperator(e1) (e2) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2) @ (17,55--17,66)"
        [], "let testByteBitwiseXorOperator(e1) (e2) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2) @ (18,55--18,66)"
        [], "let testByteShiftLeftOperator(e1) (e2) = Operators.op_LeftShift<Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2) @ (19,54--19,65)"
        [], "let testByteShiftRightOperator(e1) (e2) = Operators.op_RightShift<Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2) @ (20,54--20,65)"
        [], "let testByteAdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2) @ (24,53--24,70)"
        [], "let testByteSubtractionChecked(e1) (e2) = Checked.op_Subtraction<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2) @ (25,53--25,70)"
        [], "let testByteMultiplyChecked(e1) (e2) = Checked.op_Multiply<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2) @ (26,53--26,70)"
        [], "let testByteToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,43--29,58)"
        [], "let testByteToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,43--30,59)"
        [], "let testByteToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,43--31,59)"
        [], "let testByteToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,43--32,60)"
        [], "let testByteToIntChecked(e1) = Checked.ToInt<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (33,43--33,57)"
        [], "let testByteToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,43--34,59)"
        [], "let testByteToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,43--35,60)"
        [], "let testByteToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,43--36,59)"
        [], "let testByteToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,43--37,60)"
        [], "let testByteToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,43--38,63)"
        [], "let testByteToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,43--39,64)"
        [], "let testByteToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,43--41,50)"
        [], "let testByteToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,43--42,51)"
        [], "let testByteToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,43--43,51)"
        [], "let testByteToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,43--44,52)"
        [], "let testByteToIntOperator(e1) = Operators.ToInt<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (45,43--45,49)"
        [], "let testByteToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,43--46,51)"
        [], "let testByteToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,43--47,52)"
        [], "let testByteToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,43--48,51)"
        [], "let testByteToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,43--49,52)"
        [], "let testByteToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,43--50,55)"
        [], "let testByteToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,43--51,56)"
        [], "let testByteToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,43--52,53)"
        [], "let testByteToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,43--53,51)"
        [], "let testByteToDecimalOperator(e1) = Operators.ToDecimal<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.decimal> (arg0_0),e1) @ (54,43--54,53)"
        [], "let testByteToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,43--55,50)"
        [], "let testByteToStringOperator(e1) = Operators.ToString<Microsoft.FSharp.Core.byte> (e1) @ (56,43--56,52)"
      ]
    let expectedOptimized = [
        [], "type OperatorTestsByte"
        [], "let testByteEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.byte> (e1,e2) @ (4,63--4,72)"
        [], "let testByteNotEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_Equality<Microsoft.FSharp.Core.byte> (e1,e2),False) @ (5,63--5,73)"
        [], "let testByteLessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.byte> (e1,e2) @ (6,63--6,72)"
        [], "let testByteLessThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_GreaterThan<Microsoft.FSharp.Core.byte> (e1,e2),False) @ (7,63--7,73)"
        [], "let testByteGreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.byte> (e1,e2) @ (8,63--8,72)"
        [], "let testByteGreaterThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_LessThan<Microsoft.FSharp.Core.byte> (e1,e2),False) @ (9,63--9,73)"
        [], "let testByteAdditionOperator(e1) (e2) = Operators.ToByte<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.byte> (arg0_0),Operators.op_Addition<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2)) @ (11,55--11,64)"
        [], "let testByteSubtractionOperator(e1) (e2) = Operators.ToByte<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.byte> (arg0_0),Operators.op_Subtraction<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2)) @ (12,55--12,64)"
        [], "let testByteMultiplyOperator(e1) (e2) = Operators.ToByte<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.byte> (arg0_0),Operators.op_Multiply<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2)) @ (13,55--13,64)"
        [], "let testByteDivisionOperator(e1) (e2) = Operators.ToByte<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.byte> (arg0_0),Operators.op_Division<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2)) @ (14,55--14,64)"
        [], "let testByteModulusOperator(e1) (e2) = Operators.ToByte<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.byte> (arg0_0),Operators.op_Modulus<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2)) @ (15,55--15,64)"
        [], "let testByteBitwiseAndOperator(e1) (e2) = Operators.op_BitwiseAnd<Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2) @ (16,55--16,66)"
        [], "let testByteBitwiseOrOperator(e1) (e2) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2) @ (17,55--17,66)"
        [], "let testByteBitwiseXorOperator(e1) (e2) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2) @ (18,55--18,66)"
        [], "let testByteShiftLeftOperator(e1) (e2) = Operators.ToByte<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.byte> (arg0_0),Operators.op_LeftShift<Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e2,7))) @ (19,54--19,65)"
        [], "let testByteShiftRightOperator(e1) (e2) = Operators.ToByte<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.byte> (arg0_0),Operators.op_RightShift<Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e2,7))) @ (20,54--20,65)"
        [], "let testByteAdditionChecked(e1) (e2) = Checked.ToByte<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.byte> (arg0_0),Checked.op_Addition<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2)) @ (24,53--24,70)"
        [], "let testByteSubtractionChecked(e1) (e2) = Checked.ToByte<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.byte> (arg0_0),Checked.op_Subtraction<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2)) @ (25,53--25,70)"
        [], "let testByteMultiplyChecked(e1) (e2) = Checked.ToByte<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.byte> (arg0_0),Checked.op_Multiply<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0,arg1_0),e1,e2)) @ (26,53--26,70)"
        [], "let testByteToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,43--29,58)"
        [], "let testByteToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,43--30,59)"
        [], "let testByteToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,43--31,59)"
        [], "let testByteToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,43--32,60)"
        [], "let testByteToIntChecked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (33,43--33,57)"
        [], "let testByteToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,43--34,59)"
        [], "let testByteToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,43--35,60)"
        [], "let testByteToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,43--36,59)"
        [], "let testByteToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,43--37,60)"
        [], "let testByteToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,43--38,63)"
        [], "let testByteToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,43--39,64)"
        [], "let testByteToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,43--41,50)"
        [], "let testByteToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,43--42,51)"
        [], "let testByteToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,43--43,51)"
        [], "let testByteToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,43--44,52)"
        [], "let testByteToIntOperator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (45,43--45,49)"
        [], "let testByteToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,43--46,51)"
        [], "let testByteToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,43--47,52)"
        [], "let testByteToInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (48,43--48,51)"
        [], "let testByteToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,43--49,52)"
        [], "let testByteToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,43--50,55)"
        [], "let testByteToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,43--51,56)"
        [], "let testByteToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float32> (arg0_0),Operators.ToDouble<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.float> (arg0_0),e1)) @ (52,43--52,53)"
        [], "let testByteToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0),Operators.ToDouble<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.float> (arg0_0),e1)) @ (53,43--53,51)"
        [], "let testByteToDecimalOperator(e1) = Convert.ToDecimal (e1) @ (54,43--54,53)"
        [], "let testByteToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.byte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.byte,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,43--55,50)"
        [FC47; FC50], "let testByteToStringOperator(e1) = let mutable copyOfStruct: Microsoft.FSharp.Core.byte = e1 in copyOfStruct.ToString() @ (56,43--56,52)"
      ]


    testOperators "Byte" "byte" excludedTests expectedUnoptimized expectedOptimized


[<Test>]
let ``Test Operator Declarations for SByte`` () =
    let excludedTests = [ ]

    let expectedUnoptimized = [
        [], "type OperatorTestsSByte"
        [], "let testSByteEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.sbyte> (e1,e2) @ (4,66--4,75)"
        [], "let testSByteNotEqualsOperator(e1) (e2) = Operators.op_Inequality<Microsoft.FSharp.Core.sbyte> (e1,e2) @ (5,66--5,76)"
        [], "let testSByteLessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.sbyte> (e1,e2) @ (6,66--6,75)"
        [], "let testSByteLessThanOrEqualsOperator(e1) (e2) = Operators.op_LessThanOrEqual<Microsoft.FSharp.Core.sbyte> (e1,e2) @ (7,66--7,76)"
        [], "let testSByteGreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.sbyte> (e1,e2) @ (8,66--8,75)"
        [], "let testSByteGreaterThanOrEqualsOperator(e1) (e2) = Operators.op_GreaterThanOrEqual<Microsoft.FSharp.Core.sbyte> (e1,e2) @ (9,66--9,76)"
        [], "let testSByteAdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2) @ (11,58--11,67)"
        [], "let testSByteSubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2) @ (12,58--12,67)"
        [], "let testSByteMultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2) @ (13,58--13,67)"
        [], "let testSByteDivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2) @ (14,58--14,67)"
        [], "let testSByteModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2) @ (15,58--15,67)"
        [], "let testSByteBitwiseAndOperator(e1) (e2) = Operators.op_BitwiseAnd<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2) @ (16,58--16,69)"
        [], "let testSByteBitwiseOrOperator(e1) (e2) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2) @ (17,58--17,69)"
        [], "let testSByteBitwiseXorOperator(e1) (e2) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2) @ (18,58--18,69)"
        [], "let testSByteShiftLeftOperator(e1) (e2) = Operators.op_LeftShift<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2) @ (19,56--19,67)"
        [], "let testSByteShiftRightOperator(e1) (e2) = Operators.op_RightShift<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2) @ (20,56--20,67)"
        [], "let testSByteUnaryNegOperator(e1) = Operators.op_UnaryNegation<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (22,45--22,52)"
        [], "let testSByteAdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2) @ (24,56--24,73)"
        [], "let testSByteSubtractionChecked(e1) (e2) = Checked.op_Subtraction<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2) @ (25,56--25,73)"
        [], "let testSByteMultiplyChecked(e1) (e2) = Checked.op_Multiply<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2) @ (26,56--26,73)"
        [], "let testSByteUnaryNegChecked(e1) = Checked.op_UnaryNegation<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (27,45--27,60)"
        [], "let testSByteToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,45--29,60)"
        [], "let testSByteToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,45--30,61)"
        [], "let testSByteToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,45--31,61)"
        [], "let testSByteToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,45--32,62)"
        [], "let testSByteToIntChecked(e1) = Checked.ToInt<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (33,45--33,59)"
        [], "let testSByteToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,45--34,61)"
        [], "let testSByteToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,45--35,62)"
        [], "let testSByteToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,45--36,61)"
        [], "let testSByteToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,45--37,62)"
        [], "let testSByteToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,45--38,65)"
        [], "let testSByteToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,45--39,66)"
        [], "let testSByteToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,45--41,52)"
        [], "let testSByteToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,45--42,53)"
        [], "let testSByteToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,45--43,53)"
        [], "let testSByteToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,45--44,54)"
        [], "let testSByteToIntOperator(e1) = Operators.ToInt<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (45,45--45,51)"
        [], "let testSByteToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,45--46,53)"
        [], "let testSByteToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,45--47,54)"
        [], "let testSByteToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,45--48,53)"
        [], "let testSByteToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,45--49,54)"
        [], "let testSByteToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,45--50,57)"
        [], "let testSByteToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,45--51,58)"
        [], "let testSByteToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,45--52,55)"
        [], "let testSByteToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,45--53,53)"
        [], "let testSByteToDecimalOperator(e1) = Operators.ToDecimal<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.decimal> (arg0_0),e1) @ (54,45--54,55)"
        [], "let testSByteToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,45--55,52)"
        [], "let testSByteToStringOperator(e1) = Operators.ToString<Microsoft.FSharp.Core.sbyte> (e1) @ (56,45--56,54)"
      ]
    let expectedOptimized = [
        [], "type OperatorTestsSByte"
        [], "let testSByteEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.sbyte> (e1,e2) @ (4,66--4,75)"
        [], "let testSByteNotEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_Equality<Microsoft.FSharp.Core.sbyte> (e1,e2),False) @ (5,66--5,76)"
        [], "let testSByteLessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.sbyte> (e1,e2) @ (6,66--6,75)"
        [], "let testSByteLessThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_GreaterThan<Microsoft.FSharp.Core.sbyte> (e1,e2),False) @ (7,66--7,76)"
        [], "let testSByteGreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.sbyte> (e1,e2) @ (8,66--8,75)"
        [], "let testSByteGreaterThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_LessThan<Microsoft.FSharp.Core.sbyte> (e1,e2),False) @ (9,66--9,76)"
        [], "let testSByteAdditionOperator(e1) (e2) = Operators.ToSByte<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.sbyte> (arg0_0),Operators.op_Addition<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2)) @ (11,58--11,67)"
        [], "let testSByteSubtractionOperator(e1) (e2) = Operators.ToSByte<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.sbyte> (arg0_0),Operators.op_Subtraction<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2)) @ (12,58--12,67)"
        [], "let testSByteMultiplyOperator(e1) (e2) = Operators.ToSByte<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.sbyte> (arg0_0),Operators.op_Multiply<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2)) @ (13,58--13,67)"
        [], "let testSByteDivisionOperator(e1) (e2) = Operators.ToSByte<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.sbyte> (arg0_0),Operators.op_Division<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2)) @ (14,58--14,67)"
        [], "let testSByteModulusOperator(e1) (e2) = Operators.ToSByte<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.sbyte> (arg0_0),Operators.op_Modulus<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2)) @ (15,58--15,67)"
        [], "let testSByteBitwiseAndOperator(e1) (e2) = Operators.op_BitwiseAnd<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2) @ (16,58--16,69)"
        [], "let testSByteBitwiseOrOperator(e1) (e2) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2) @ (17,58--17,69)"
        [], "let testSByteBitwiseXorOperator(e1) (e2) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2) @ (18,58--18,69)"
        [], "let testSByteShiftLeftOperator(e1) (e2) = Operators.ToSByte<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.sbyte> (arg0_0),Operators.op_LeftShift<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e2,7))) @ (19,56--19,67)"
        [], "let testSByteShiftRightOperator(e1) (e2) = Operators.ToSByte<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.sbyte> (arg0_0),Operators.op_RightShift<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e2,7))) @ (20,56--20,67)"
        [], "let testSByteUnaryNegOperator(e1) = Operators.op_UnaryNegation<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (22,45--22,52)"
        [], "let testSByteAdditionChecked(e1) (e2) = Checked.ToSByte<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.sbyte> (arg0_0),Checked.op_Addition<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2)) @ (24,56--24,73)"
        [], "let testSByteSubtractionChecked(e1) (e2) = Checked.ToSByte<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.sbyte> (arg0_0),Checked.op_Subtraction<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2)) @ (25,56--25,73)"
        [], "let testSByteMultiplyChecked(e1) (e2) = Checked.ToSByte<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.sbyte> (arg0_0),Checked.op_Multiply<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),e1,e2)) @ (26,56--26,73)"
        [], "let testSByteUnaryNegChecked(e1) = Checked.op_Subtraction<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0,arg1_0),0,e1) @ (27,45--27,60)"
        [], "let testSByteToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,45--29,60)"
        [], "let testSByteToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,45--30,61)"
        [], "let testSByteToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,45--31,61)"
        [], "let testSByteToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,45--32,62)"
        [], "let testSByteToIntChecked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (33,45--33,59)"
        [], "let testSByteToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,45--34,61)"
        [], "let testSByteToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,45--35,62)"
        [], "let testSByteToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,45--36,61)"
        [], "let testSByteToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,45--37,62)"
        [], "let testSByteToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,45--38,65)"
        [], "let testSByteToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,45--39,66)"
        [], "let testSByteToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,45--41,52)"
        [], "let testSByteToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,45--42,53)"
        [], "let testSByteToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,45--43,53)"
        [], "let testSByteToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,45--44,54)"
        [], "let testSByteToIntOperator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (45,45--45,51)"
        [], "let testSByteToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,45--46,53)"
        [], "let testSByteToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,45--47,54)"
        [], "let testSByteToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,45--48,53)"
        [], "let testSByteToUInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (49,45--49,54)"
        [], "let testSByteToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,45--50,57)"
        [], "let testSByteToUIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (51,45--51,58)"
        [], "let testSByteToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,45--52,55)"
        [], "let testSByteToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,45--53,53)"
        [], "let testSByteToDecimalOperator(e1) = Convert.ToDecimal (e1) @ (54,45--54,55)"
        [], "let testSByteToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.sbyte> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.sbyte,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,45--55,52)"
        [FC47; FC50], "let testSByteToStringOperator(e1) = IntrinsicFunctions.UnboxGeneric<System.IFormattable> (Operators.Box<Microsoft.FSharp.Core.sbyte> (e1)).ToString(dflt,CultureInfo.get_InvariantCulture () :> System.IFormatProvider) @ (56,45--56,54)"
      ]

    testOperators "SByte" "sbyte" excludedTests expectedUnoptimized expectedOptimized

[<Test>]
let ``Test Operator Declarations for Int16`` () =
    let excludedTests = [ ]

    let expectedUnoptimized = [
        [], "type OperatorTestsInt16"
        [], "let testInt16EqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.int16> (e1,e2) @ (4,66--4,75)"
        [], "let testInt16NotEqualsOperator(e1) (e2) = Operators.op_Inequality<Microsoft.FSharp.Core.int16> (e1,e2) @ (5,66--5,76)"
        [], "let testInt16LessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.int16> (e1,e2) @ (6,66--6,75)"
        [], "let testInt16LessThanOrEqualsOperator(e1) (e2) = Operators.op_LessThanOrEqual<Microsoft.FSharp.Core.int16> (e1,e2) @ (7,66--7,76)"
        [], "let testInt16GreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.int16> (e1,e2) @ (8,66--8,75)"
        [], "let testInt16GreaterThanOrEqualsOperator(e1) (e2) = Operators.op_GreaterThanOrEqual<Microsoft.FSharp.Core.int16> (e1,e2) @ (9,66--9,76)"
        [], "let testInt16AdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2) @ (11,58--11,67)"
        [], "let testInt16SubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2) @ (12,58--12,67)"
        [], "let testInt16MultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2) @ (13,58--13,67)"
        [], "let testInt16DivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2) @ (14,58--14,67)"
        [], "let testInt16ModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2) @ (15,58--15,67)"
        [], "let testInt16BitwiseAndOperator(e1) (e2) = Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2) @ (16,58--16,69)"
        [], "let testInt16BitwiseOrOperator(e1) (e2) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2) @ (17,58--17,69)"
        [], "let testInt16BitwiseXorOperator(e1) (e2) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2) @ (18,58--18,69)"
        [], "let testInt16ShiftLeftOperator(e1) (e2) = Operators.op_LeftShift<Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2) @ (19,56--19,67)"
        [], "let testInt16ShiftRightOperator(e1) (e2) = Operators.op_RightShift<Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2) @ (20,56--20,67)"
        [], "let testInt16UnaryNegOperator(e1) = Operators.op_UnaryNegation<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (22,45--22,52)"
        [], "let testInt16AdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2) @ (24,56--24,73)"
        [], "let testInt16SubtractionChecked(e1) (e2) = Checked.op_Subtraction<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2) @ (25,56--25,73)"
        [], "let testInt16MultiplyChecked(e1) (e2) = Checked.op_Multiply<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2) @ (26,56--26,73)"
        [], "let testInt16UnaryNegChecked(e1) = Checked.op_UnaryNegation<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (27,45--27,60)"
        [], "let testInt16ToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,45--29,60)"
        [], "let testInt16ToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,45--30,61)"
        [], "let testInt16ToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,45--31,61)"
        [], "let testInt16ToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,45--32,62)"
        [], "let testInt16ToIntChecked(e1) = Checked.ToInt<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (33,45--33,59)"
        [], "let testInt16ToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,45--34,61)"
        [], "let testInt16ToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,45--35,62)"
        [], "let testInt16ToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,45--36,61)"
        [], "let testInt16ToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,45--37,62)"
        [], "let testInt16ToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,45--38,65)"
        [], "let testInt16ToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,45--39,66)"
        [], "let testInt16ToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,45--41,52)"
        [], "let testInt16ToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,45--42,53)"
        [], "let testInt16ToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,45--43,53)"
        [], "let testInt16ToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,45--44,54)"
        [], "let testInt16ToIntOperator(e1) = Operators.ToInt<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (45,45--45,51)"
        [], "let testInt16ToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,45--46,53)"
        [], "let testInt16ToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,45--47,54)"
        [], "let testInt16ToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,45--48,53)"
        [], "let testInt16ToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,45--49,54)"
        [], "let testInt16ToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,45--50,57)"
        [], "let testInt16ToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,45--51,58)"
        [], "let testInt16ToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,45--52,55)"
        [], "let testInt16ToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,45--53,53)"
        [], "let testInt16ToDecimalOperator(e1) = Operators.ToDecimal<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.decimal> (arg0_0),e1) @ (54,45--54,55)"
        [], "let testInt16ToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,45--55,52)"
        [], "let testInt16ToStringOperator(e1) = Operators.ToString<Microsoft.FSharp.Core.int16> (e1) @ (56,45--56,54)"
      ]
    let expectedOptimized = [
        [], "type OperatorTestsInt16"
        [], "let testInt16EqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.int16> (e1,e2) @ (4,66--4,75)"
        [], "let testInt16NotEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_Equality<Microsoft.FSharp.Core.int16> (e1,e2),False) @ (5,66--5,76)"
        [], "let testInt16LessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.int16> (e1,e2) @ (6,66--6,75)"
        [], "let testInt16LessThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_GreaterThan<Microsoft.FSharp.Core.int16> (e1,e2),False) @ (7,66--7,76)"
        [], "let testInt16GreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.int16> (e1,e2) @ (8,66--8,75)"
        [], "let testInt16GreaterThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_LessThan<Microsoft.FSharp.Core.int16> (e1,e2),False) @ (9,66--9,76)"
        [], "let testInt16AdditionOperator(e1) (e2) = Operators.ToInt16<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int16> (arg0_0),Operators.op_Addition<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2)) @ (11,58--11,67)"
        [], "let testInt16SubtractionOperator(e1) (e2) = Operators.ToInt16<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int16> (arg0_0),Operators.op_Subtraction<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2)) @ (12,58--12,67)"
        [], "let testInt16MultiplyOperator(e1) (e2) = Operators.ToInt16<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int16> (arg0_0),Operators.op_Multiply<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2)) @ (13,58--13,67)"
        [], "let testInt16DivisionOperator(e1) (e2) = Operators.ToInt16<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int16> (arg0_0),Operators.op_Division<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2)) @ (14,58--14,67)"
        [], "let testInt16ModulusOperator(e1) (e2) = Operators.ToInt16<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int16> (arg0_0),Operators.op_Modulus<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2)) @ (15,58--15,67)"
        [], "let testInt16BitwiseAndOperator(e1) (e2) = Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2) @ (16,58--16,69)"
        [], "let testInt16BitwiseOrOperator(e1) (e2) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2) @ (17,58--17,69)"
        [], "let testInt16BitwiseXorOperator(e1) (e2) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2) @ (18,58--18,69)"
        [], "let testInt16ShiftLeftOperator(e1) (e2) = Operators.ToInt16<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int16> (arg0_0),Operators.op_LeftShift<Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e2,15))) @ (19,56--19,67)"
        [], "let testInt16ShiftRightOperator(e1) (e2) = Operators.ToInt16<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int16> (arg0_0),Operators.op_RightShift<Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e2,15))) @ (20,56--20,67)"
        [], "let testInt16UnaryNegOperator(e1) = Operators.op_UnaryNegation<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (22,45--22,52)"
        [], "let testInt16AdditionChecked(e1) (e2) = Checked.ToInt16<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int16> (arg0_0),Checked.op_Addition<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2)) @ (24,56--24,73)"
        [], "let testInt16SubtractionChecked(e1) (e2) = Checked.ToInt16<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int16> (arg0_0),Checked.op_Subtraction<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2)) @ (25,56--25,73)"
        [], "let testInt16MultiplyChecked(e1) (e2) = Checked.ToInt16<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int16> (arg0_0),Checked.op_Multiply<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),e1,e2)) @ (26,56--26,73)"
        [], "let testInt16UnaryNegChecked(e1) = Checked.op_Subtraction<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0,arg1_0),0,e1) @ (27,45--27,60)"
        [], "let testInt16ToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,45--29,60)"
        [], "let testInt16ToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,45--30,61)"
        [], "let testInt16ToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,45--31,61)"
        [], "let testInt16ToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,45--32,62)"
        [], "let testInt16ToIntChecked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (33,45--33,59)"
        [], "let testInt16ToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,45--34,61)"
        [], "let testInt16ToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,45--35,62)"
        [], "let testInt16ToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,45--36,61)"
        [], "let testInt16ToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,45--37,62)"
        [], "let testInt16ToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,45--38,65)"
        [], "let testInt16ToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,45--39,66)"
        [], "let testInt16ToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,45--41,52)"
        [], "let testInt16ToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,45--42,53)"
        [], "let testInt16ToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,45--43,53)"
        [], "let testInt16ToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,45--44,54)"
        [], "let testInt16ToIntOperator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (45,45--45,51)"
        [], "let testInt16ToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,45--46,53)"
        [], "let testInt16ToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,45--47,54)"
        [], "let testInt16ToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,45--48,53)"
        [], "let testInt16ToUInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (49,45--49,54)"
        [], "let testInt16ToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,45--50,57)"
        [], "let testInt16ToUIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (51,45--51,58)"
        [], "let testInt16ToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,45--52,55)"
        [], "let testInt16ToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,45--53,53)"
        [], "let testInt16ToDecimalOperator(e1) = Convert.ToDecimal (e1) @ (54,45--54,55)"
        [], "let testInt16ToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.int16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int16,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,45--55,52)"
        [FC47; FC50], "let testInt16ToStringOperator(e1) = IntrinsicFunctions.UnboxGeneric<System.IFormattable> (Operators.Box<Microsoft.FSharp.Core.int16> (e1)).ToString(dflt,CultureInfo.get_InvariantCulture () :> System.IFormatProvider) @ (56,45--56,54)"
      ]

    testOperators "Int16" "int16" excludedTests expectedUnoptimized expectedOptimized

[<Test>]
let ``Test Operator Declarations for UInt16`` () =
    let excludedTests = [
        "testUInt16UnaryNegOperator";
        "testUInt16UnaryNegChecked";
      ]

    let expectedUnoptimized = [
        [], "type OperatorTestsUInt16"
        [], "let testUInt16EqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.uint16> (e1,e2) @ (4,69--4,78)"
        [], "let testUInt16NotEqualsOperator(e1) (e2) = Operators.op_Inequality<Microsoft.FSharp.Core.uint16> (e1,e2) @ (5,69--5,79)"
        [], "let testUInt16LessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.uint16> (e1,e2) @ (6,69--6,78)"
        [], "let testUInt16LessThanOrEqualsOperator(e1) (e2) = Operators.op_LessThanOrEqual<Microsoft.FSharp.Core.uint16> (e1,e2) @ (7,69--7,79)"
        [], "let testUInt16GreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.uint16> (e1,e2) @ (8,69--8,78)"
        [], "let testUInt16GreaterThanOrEqualsOperator(e1) (e2) = Operators.op_GreaterThanOrEqual<Microsoft.FSharp.Core.uint16> (e1,e2) @ (9,69--9,79)"
        [], "let testUInt16AdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2) @ (11,61--11,70)"
        [], "let testUInt16SubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2) @ (12,61--12,70)"
        [], "let testUInt16MultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2) @ (13,61--13,70)"
        [], "let testUInt16DivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2) @ (14,61--14,70)"
        [], "let testUInt16ModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2) @ (15,61--15,70)"
        [], "let testUInt16BitwiseAndOperator(e1) (e2) = Operators.op_BitwiseAnd<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2) @ (16,61--16,72)"
        [], "let testUInt16BitwiseOrOperator(e1) (e2) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2) @ (17,61--17,72)"
        [], "let testUInt16BitwiseXorOperator(e1) (e2) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2) @ (18,61--18,72)"
        [], "let testUInt16ShiftLeftOperator(e1) (e2) = Operators.op_LeftShift<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2) @ (19,58--19,69)"
        [], "let testUInt16ShiftRightOperator(e1) (e2) = Operators.op_RightShift<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2) @ (20,58--20,69)"
        [], "let testUInt16AdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2) @ (24,59--24,76)"
        [], "let testUInt16SubtractionChecked(e1) (e2) = Checked.op_Subtraction<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2) @ (25,59--25,76)"
        [], "let testUInt16MultiplyChecked(e1) (e2) = Checked.op_Multiply<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2) @ (26,59--26,76)"
        [], "let testUInt16ToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,47--29,62)"
        [], "let testUInt16ToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,47--30,63)"
        [], "let testUInt16ToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,47--31,63)"
        [], "let testUInt16ToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,47--32,64)"
        [], "let testUInt16ToIntChecked(e1) = Checked.ToInt<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (33,47--33,61)"
        [], "let testUInt16ToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,47--34,63)"
        [], "let testUInt16ToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,47--35,64)"
        [], "let testUInt16ToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,47--36,63)"
        [], "let testUInt16ToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,47--37,64)"
        [], "let testUInt16ToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,47--38,67)"
        [], "let testUInt16ToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,47--39,68)"
        [], "let testUInt16ToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,47--41,54)"
        [], "let testUInt16ToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,47--42,55)"
        [], "let testUInt16ToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,47--43,55)"
        [], "let testUInt16ToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,47--44,56)"
        [], "let testUInt16ToIntOperator(e1) = Operators.ToInt<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (45,47--45,53)"
        [], "let testUInt16ToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,47--46,55)"
        [], "let testUInt16ToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,47--47,56)"
        [], "let testUInt16ToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,47--48,55)"
        [], "let testUInt16ToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,47--49,56)"
        [], "let testUInt16ToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,47--50,59)"
        [], "let testUInt16ToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,47--51,60)"
        [], "let testUInt16ToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,47--52,57)"
        [], "let testUInt16ToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,47--53,55)"
        [], "let testUInt16ToDecimalOperator(e1) = Operators.ToDecimal<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.decimal> (arg0_0),e1) @ (54,47--54,57)"
        [], "let testUInt16ToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,47--55,54)"
        [], "let testUInt16ToStringOperator(e1) = Operators.ToString<Microsoft.FSharp.Core.uint16> (e1) @ (56,47--56,56)"
      ]
    let expectedOptimized = [
        [], "type OperatorTestsUInt16"
        [], "let testUInt16EqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.uint16> (e1,e2) @ (4,69--4,78)"
        [], "let testUInt16NotEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_Equality<Microsoft.FSharp.Core.uint16> (e1,e2),False) @ (5,69--5,79)"
        [], "let testUInt16LessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.uint16> (e1,e2) @ (6,69--6,78)"
        [], "let testUInt16LessThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_GreaterThan<Microsoft.FSharp.Core.uint16> (e1,e2),False) @ (7,69--7,79)"
        [], "let testUInt16GreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.uint16> (e1,e2) @ (8,69--8,78)"
        [], "let testUInt16GreaterThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_LessThan<Microsoft.FSharp.Core.uint16> (e1,e2),False) @ (9,69--9,79)"
        [], "let testUInt16AdditionOperator(e1) (e2) = Operators.ToUInt16<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint16> (arg0_0),Operators.op_Addition<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2)) @ (11,61--11,70)"
        [], "let testUInt16SubtractionOperator(e1) (e2) = Operators.ToUInt16<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint16> (arg0_0),Operators.op_Subtraction<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2)) @ (12,61--12,70)"
        [], "let testUInt16MultiplyOperator(e1) (e2) = Operators.ToUInt16<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint16> (arg0_0),Operators.op_Multiply<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2)) @ (13,61--13,70)"
        [], "let testUInt16DivisionOperator(e1) (e2) = Operators.ToUInt16<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint16> (arg0_0),Operators.op_Division<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2)) @ (14,61--14,70)"
        [], "let testUInt16ModulusOperator(e1) (e2) = Operators.ToUInt16<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint16> (arg0_0),Operators.op_Modulus<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2)) @ (15,61--15,70)"
        [], "let testUInt16BitwiseAndOperator(e1) (e2) = Operators.op_BitwiseAnd<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2) @ (16,61--16,72)"
        [], "let testUInt16BitwiseOrOperator(e1) (e2) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2) @ (17,61--17,72)"
        [], "let testUInt16BitwiseXorOperator(e1) (e2) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2) @ (18,61--18,72)"
        [], "let testUInt16ShiftLeftOperator(e1) (e2) = Operators.ToUInt16<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint16> (arg0_0),Operators.op_LeftShift<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e2,15))) @ (19,58--19,69)"
        [], "let testUInt16ShiftRightOperator(e1) (e2) = Operators.ToUInt16<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint16> (arg0_0),Operators.op_RightShift<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e2,15))) @ (20,58--20,69)"
        [], "let testUInt16AdditionChecked(e1) (e2) = Checked.ToUInt16<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint16> (arg0_0),Checked.op_Addition<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2)) @ (24,59--24,76)"
        [], "let testUInt16SubtractionChecked(e1) (e2) = Checked.ToUInt16<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint16> (arg0_0),Checked.op_Subtraction<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2)) @ (25,59--25,76)"
        [], "let testUInt16MultiplyChecked(e1) (e2) = Checked.ToUInt16<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint16> (arg0_0),Checked.op_Multiply<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0,arg1_0),e1,e2)) @ (26,59--26,76)"
        [], "let testUInt16ToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,47--29,62)"
        [], "let testUInt16ToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,47--30,63)"
        [], "let testUInt16ToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,47--31,63)"
        [], "let testUInt16ToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,47--32,64)"
        [], "let testUInt16ToIntChecked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (33,47--33,61)"
        [], "let testUInt16ToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,47--34,63)"
        [], "let testUInt16ToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,47--35,64)"
        [], "let testUInt16ToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,47--36,63)"
        [], "let testUInt16ToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,47--37,64)"
        [], "let testUInt16ToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,47--38,67)"
        [], "let testUInt16ToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,47--39,68)"
        [], "let testUInt16ToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,47--41,54)"
        [], "let testUInt16ToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,47--42,55)"
        [], "let testUInt16ToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,47--43,55)"
        [], "let testUInt16ToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,47--44,56)"
        [], "let testUInt16ToIntOperator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (45,47--45,53)"
        [], "let testUInt16ToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,47--46,55)"
        [], "let testUInt16ToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,47--47,56)"
        [], "let testUInt16ToInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (48,47--48,55)"
        [], "let testUInt16ToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,47--49,56)"
        [], "let testUInt16ToIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (50,47--50,59)"
        [], "let testUInt16ToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,47--51,60)"
        [], "let testUInt16ToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float32> (arg0_0),Operators.ToDouble<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.float> (arg0_0),e1)) @ (52,47--52,57)"
        [], "let testUInt16ToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0),Operators.ToDouble<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.float> (arg0_0),e1)) @ (53,47--53,55)"
        [], "let testUInt16ToDecimalOperator(e1) = Convert.ToDecimal (e1) @ (54,47--54,57)"
        [], "let testUInt16ToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.uint16> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint16,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,47--55,54)"
        [FC47; FC50], "let testUInt16ToStringOperator(e1) = let mutable copyOfStruct: Microsoft.FSharp.Core.uint16 = e1 in copyOfStruct.ToString() @ (56,47--56,56)"
      ]

    testOperators "UInt16" "uint16" excludedTests expectedUnoptimized expectedOptimized

[<Test>]
let ``Test Operator Declarations for Int`` () =
    let excludedTests = [ ]

    let expectedUnoptimized = [
        [], "type OperatorTestsInt"
        [], "let testIntEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.int> (e1,e2) @ (4,60--4,69)"
        [], "let testIntNotEqualsOperator(e1) (e2) = Operators.op_Inequality<Microsoft.FSharp.Core.int> (e1,e2) @ (5,60--5,70)"
        [], "let testIntLessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.int> (e1,e2) @ (6,60--6,69)"
        [], "let testIntLessThanOrEqualsOperator(e1) (e2) = Operators.op_LessThanOrEqual<Microsoft.FSharp.Core.int> (e1,e2) @ (7,60--7,70)"
        [], "let testIntGreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.int> (e1,e2) @ (8,60--8,69)"
        [], "let testIntGreaterThanOrEqualsOperator(e1) (e2) = Operators.op_GreaterThanOrEqual<Microsoft.FSharp.Core.int> (e1,e2) @ (9,60--9,70)"
        [], "let testIntAdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (11,52--11,61)"
        [], "let testIntSubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (12,52--12,61)"
        [], "let testIntMultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (13,52--13,61)"
        [], "let testIntDivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (14,52--14,61)"
        [], "let testIntModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (15,52--15,61)"
        [], "let testIntBitwiseAndOperator(e1) (e2) = Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (16,52--16,63)"
        [], "let testIntBitwiseOrOperator(e1) (e2) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (17,52--17,63)"
        [], "let testIntBitwiseXorOperator(e1) (e2) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (18,52--18,63)"
        [], "let testIntShiftLeftOperator(e1) (e2) = Operators.op_LeftShift<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (19,52--19,63)"
        [], "let testIntShiftRightOperator(e1) (e2) = Operators.op_RightShift<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (20,52--20,63)"
        [], "let testIntUnaryNegOperator(e1) = Operators.op_UnaryNegation<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (22,41--22,48)"
        [], "let testIntAdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (24,50--24,67)"
        [], "let testIntSubtractionChecked(e1) (e2) = Checked.op_Subtraction<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (25,50--25,67)"
        [], "let testIntMultiplyChecked(e1) (e2) = Checked.op_Multiply<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (26,50--26,67)"
        [], "let testIntUnaryNegChecked(e1) = Checked.op_UnaryNegation<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (27,41--27,56)"
        [], "let testIntToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,41--29,56)"
        [], "let testIntToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,41--30,57)"
        [], "let testIntToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,41--31,57)"
        [], "let testIntToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,41--32,58)"
        [], "let testIntToIntChecked(e1) = Checked.ToInt<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (33,41--33,55)"
        [], "let testIntToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,41--34,57)"
        [], "let testIntToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,41--35,58)"
        [], "let testIntToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,41--36,57)"
        [], "let testIntToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,41--37,58)"
        [], "let testIntToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,41--38,61)"
        [], "let testIntToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,41--39,62)"
        [], "let testIntToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,41--41,48)"
        [], "let testIntToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,41--42,49)"
        [], "let testIntToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,41--43,49)"
        [], "let testIntToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,41--44,50)"
        [], "let testIntToIntOperator(e1) = Operators.ToInt<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (45,41--45,47)"
        [], "let testIntToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,41--46,49)"
        [], "let testIntToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,41--47,50)"
        [], "let testIntToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,41--48,49)"
        [], "let testIntToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,41--49,50)"
        [], "let testIntToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,41--50,53)"
        [], "let testIntToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,41--51,54)"
        [], "let testIntToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,41--52,51)"
        [], "let testIntToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,41--53,49)"
        [], "let testIntToDecimalOperator(e1) = Operators.ToDecimal<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.decimal> (arg0_0),e1) @ (54,41--54,51)"
        [], "let testIntToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,41--55,48)"
        [], "let testIntToStringOperator(e1) = Operators.ToString<Microsoft.FSharp.Core.int> (e1) @ (56,41--56,50)"
      ]
    let expectedOptimized = [
        [], "type OperatorTestsInt"
        [], "let testIntEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.int> (e1,e2) @ (4,60--4,69)"
        [], "let testIntNotEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_Equality<Microsoft.FSharp.Core.int> (e1,e2),False) @ (5,60--5,70)"
        [], "let testIntLessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.int> (e1,e2) @ (6,60--6,69)"
        [], "let testIntLessThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_GreaterThan<Microsoft.FSharp.Core.int> (e1,e2),False) @ (7,60--7,70)"
        [], "let testIntGreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.int> (e1,e2) @ (8,60--8,69)"
        [], "let testIntGreaterThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_LessThan<Microsoft.FSharp.Core.int> (e1,e2),False) @ (9,60--9,70)"
        [], "let testIntAdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (11,52--11,61)"
        [], "let testIntSubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (12,52--12,61)"
        [], "let testIntMultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (13,52--13,61)"
        [], "let testIntDivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (14,52--14,61)"
        [], "let testIntModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (15,52--15,61)"
        [], "let testIntBitwiseAndOperator(e1) (e2) = Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (16,52--16,63)"
        [], "let testIntBitwiseOrOperator(e1) (e2) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (17,52--17,63)"
        [], "let testIntBitwiseXorOperator(e1) (e2) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (18,52--18,63)"
        [], "let testIntShiftLeftOperator(e1) (e2) = Operators.op_LeftShift<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e2,31)) @ (19,52--19,63)"
        [], "let testIntShiftRightOperator(e1) (e2) = Operators.op_RightShift<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e2,31)) @ (20,52--20,63)"
        [], "let testIntUnaryNegOperator(e1) = Operators.op_UnaryNegation<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (22,41--22,48)"
        [], "let testIntAdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (24,50--24,67)"
        [], "let testIntSubtractionChecked(e1) (e2) = Checked.op_Subtraction<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (25,50--25,67)"
        [], "let testIntMultiplyChecked(e1) (e2) = Checked.op_Multiply<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e1,e2) @ (26,50--26,67)"
        [], "let testIntUnaryNegChecked(e1) = Checked.op_Subtraction<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),0,e1) @ (27,41--27,56)"
        [], "let testIntToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,41--29,56)"
        [], "let testIntToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,41--30,57)"
        [], "let testIntToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,41--31,57)"
        [], "let testIntToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,41--32,58)"
        [], "let testIntToIntChecked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (33,41--33,55)"
        [], "let testIntToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,41--34,57)"
        [], "let testIntToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,41--35,58)"
        [], "let testIntToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,41--36,57)"
        [], "let testIntToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,41--37,58)"
        [], "let testIntToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,41--38,61)"
        [], "let testIntToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,41--39,62)"
        [], "let testIntToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,41--41,48)"
        [], "let testIntToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,41--42,49)"
        [], "let testIntToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,41--43,49)"
        [], "let testIntToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,41--44,50)"
        [], "let testIntToIntOperator(e1) = e1 @ (45,45--45,47)"
        [], "let testIntToInt32Operator(e1) = e1 @ (46,47--46,49)"
        [], "let testIntToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,41--47,50)"
        [], "let testIntToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,41--48,49)"
        [], "let testIntToUInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (49,41--49,50)"
        [], "let testIntToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,41--50,53)"
        [], "let testIntToUIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (51,41--51,54)"
        [], "let testIntToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,41--52,51)"
        [], "let testIntToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,41--53,49)"
        [], "let testIntToDecimalOperator(e1) = Convert.ToDecimal (e1) @ (54,41--54,51)"
        [], "let testIntToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,41--55,48)"
        [FC47; FC50], "let testIntToStringOperator(e1) = IntrinsicFunctions.UnboxGeneric<System.IFormattable> (Operators.Box<Microsoft.FSharp.Core.int> (e1)).ToString(dflt,CultureInfo.get_InvariantCulture () :> System.IFormatProvider) @ (56,41--56,50)"
      ]

    testOperators "Int" "int" excludedTests expectedUnoptimized expectedOptimized

[<Test>]
let ``Test Operator Declarations for Int32`` () =
    let excludedTests = [ ]

    let expectedUnoptimized = [
        [], "type OperatorTestsInt32"
        [], "let testInt32EqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.int32> (e1,e2) @ (4,66--4,75)"
        [], "let testInt32NotEqualsOperator(e1) (e2) = Operators.op_Inequality<Microsoft.FSharp.Core.int32> (e1,e2) @ (5,66--5,76)"
        [], "let testInt32LessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.int32> (e1,e2) @ (6,66--6,75)"
        [], "let testInt32LessThanOrEqualsOperator(e1) (e2) = Operators.op_LessThanOrEqual<Microsoft.FSharp.Core.int32> (e1,e2) @ (7,66--7,76)"
        [], "let testInt32GreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.int32> (e1,e2) @ (8,66--8,75)"
        [], "let testInt32GreaterThanOrEqualsOperator(e1) (e2) = Operators.op_GreaterThanOrEqual<Microsoft.FSharp.Core.int32> (e1,e2) @ (9,66--9,76)"
        [], "let testInt32AdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (11,58--11,67)"
        [], "let testInt32SubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (12,58--12,67)"
        [], "let testInt32MultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (13,58--13,67)"
        [], "let testInt32DivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (14,58--14,67)"
        [], "let testInt32ModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (15,58--15,67)"
        [], "let testInt32BitwiseAndOperator(e1) (e2) = Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (16,58--16,69)"
        [], "let testInt32BitwiseOrOperator(e1) (e2) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (17,58--17,69)"
        [], "let testInt32BitwiseXorOperator(e1) (e2) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (18,58--18,69)"
        [], "let testInt32ShiftLeftOperator(e1) (e2) = Operators.op_LeftShift<Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (19,56--19,67)"
        [], "let testInt32ShiftRightOperator(e1) (e2) = Operators.op_RightShift<Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (20,56--20,67)"
        [], "let testInt32UnaryNegOperator(e1) = Operators.op_UnaryNegation<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (22,45--22,52)"
        [], "let testInt32AdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (24,56--24,73)"
        [], "let testInt32SubtractionChecked(e1) (e2) = Checked.op_Subtraction<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (25,56--25,73)"
        [], "let testInt32MultiplyChecked(e1) (e2) = Checked.op_Multiply<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (26,56--26,73)"
        [], "let testInt32UnaryNegChecked(e1) = Checked.op_UnaryNegation<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (27,45--27,60)"
        [], "let testInt32ToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,45--29,60)"
        [], "let testInt32ToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,45--30,61)"
        [], "let testInt32ToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,45--31,61)"
        [], "let testInt32ToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,45--32,62)"
        [], "let testInt32ToIntChecked(e1) = Checked.ToInt<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (33,45--33,59)"
        [], "let testInt32ToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,45--34,61)"
        [], "let testInt32ToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,45--35,62)"
        [], "let testInt32ToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,45--36,61)"
        [], "let testInt32ToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,45--37,62)"
        [], "let testInt32ToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,45--38,65)"
        [], "let testInt32ToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,45--39,66)"
        [], "let testInt32ToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,45--41,52)"
        [], "let testInt32ToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,45--42,53)"
        [], "let testInt32ToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,45--43,53)"
        [], "let testInt32ToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,45--44,54)"
        [], "let testInt32ToIntOperator(e1) = Operators.ToInt<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (45,45--45,51)"
        [], "let testInt32ToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,45--46,53)"
        [], "let testInt32ToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,45--47,54)"
        [], "let testInt32ToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,45--48,53)"
        [], "let testInt32ToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,45--49,54)"
        [], "let testInt32ToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,45--50,57)"
        [], "let testInt32ToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,45--51,58)"
        [], "let testInt32ToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,45--52,55)"
        [], "let testInt32ToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,45--53,53)"
        [], "let testInt32ToDecimalOperator(e1) = Operators.ToDecimal<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.decimal> (arg0_0),e1) @ (54,45--54,55)"
        [], "let testInt32ToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,45--55,52)"
        [], "let testInt32ToStringOperator(e1) = Operators.ToString<Microsoft.FSharp.Core.int32> (e1) @ (56,45--56,54)"
      ]
    let expectedOptimized = [
        [], "type OperatorTestsInt32"
        [], "let testInt32EqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.int32> (e1,e2) @ (4,66--4,75)"
        [], "let testInt32NotEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_Equality<Microsoft.FSharp.Core.int32> (e1,e2),False) @ (5,66--5,76)"
        [], "let testInt32LessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.int32> (e1,e2) @ (6,66--6,75)"
        [], "let testInt32LessThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_GreaterThan<Microsoft.FSharp.Core.int32> (e1,e2),False) @ (7,66--7,76)"
        [], "let testInt32GreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.int32> (e1,e2) @ (8,66--8,75)"
        [], "let testInt32GreaterThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_LessThan<Microsoft.FSharp.Core.int32> (e1,e2),False) @ (9,66--9,76)"
        [], "let testInt32AdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (11,58--11,67)"
        [], "let testInt32SubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (12,58--12,67)"
        [], "let testInt32MultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (13,58--13,67)"
        [], "let testInt32DivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (14,58--14,67)"
        [], "let testInt32ModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (15,58--15,67)"
        [], "let testInt32BitwiseAndOperator(e1) (e2) = Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (16,58--16,69)"
        [], "let testInt32BitwiseOrOperator(e1) (e2) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (17,58--17,69)"
        [], "let testInt32BitwiseXorOperator(e1) (e2) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (18,58--18,69)"
        [], "let testInt32ShiftLeftOperator(e1) (e2) = Operators.op_LeftShift<Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e2,31)) @ (19,56--19,67)"
        [], "let testInt32ShiftRightOperator(e1) (e2) = Operators.op_RightShift<Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e2,31)) @ (20,56--20,67)"
        [], "let testInt32UnaryNegOperator(e1) = Operators.op_UnaryNegation<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (22,45--22,52)"
        [], "let testInt32AdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (24,56--24,73)"
        [], "let testInt32SubtractionChecked(e1) (e2) = Checked.op_Subtraction<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (25,56--25,73)"
        [], "let testInt32MultiplyChecked(e1) (e2) = Checked.op_Multiply<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0,arg1_0),e1,e2) @ (26,56--26,73)"
        [], "let testInt32UnaryNegChecked(e1) = Checked.op_Subtraction<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),0,e1) @ (27,45--27,60)"
        [], "let testInt32ToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,45--29,60)"
        [], "let testInt32ToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,45--30,61)"
        [], "let testInt32ToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,45--31,61)"
        [], "let testInt32ToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,45--32,62)"
        [], "let testInt32ToIntChecked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (33,45--33,59)"
        [], "let testInt32ToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,45--34,61)"
        [], "let testInt32ToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,45--35,62)"
        [], "let testInt32ToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,45--36,61)"
        [], "let testInt32ToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,45--37,62)"
        [], "let testInt32ToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,45--38,65)"
        [], "let testInt32ToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,45--39,66)"
        [], "let testInt32ToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,45--41,52)"
        [], "let testInt32ToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,45--42,53)"
        [], "let testInt32ToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,45--43,53)"
        [], "let testInt32ToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,45--44,54)"
        [], "let testInt32ToIntOperator(e1) = e1 @ (45,49--45,51)"
        [], "let testInt32ToInt32Operator(e1) = e1 @ (46,51--46,53)"
        [], "let testInt32ToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,45--47,54)"
        [], "let testInt32ToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,45--48,53)"
        [], "let testInt32ToUInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (49,45--49,54)"
        [], "let testInt32ToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,45--50,57)"
        [], "let testInt32ToUIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (51,45--51,58)"
        [], "let testInt32ToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,45--52,55)"
        [], "let testInt32ToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,45--53,53)"
        [], "let testInt32ToDecimalOperator(e1) = Convert.ToDecimal (e1) @ (54,45--54,55)"
        [], "let testInt32ToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.int32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,45--55,52)"
        [FC47; FC50], "let testInt32ToStringOperator(e1) = IntrinsicFunctions.UnboxGeneric<System.IFormattable> (Operators.Box<Microsoft.FSharp.Core.int32> (e1)).ToString(dflt,CultureInfo.get_InvariantCulture () :> System.IFormatProvider) @ (56,45--56,54)"
      ]


    testOperators "Int32" "int32" excludedTests expectedUnoptimized expectedOptimized

[<Test>]
let ``Test Operator Declarations for UInt32`` () =
    let excludedTests = [
        "testUInt32UnaryNegOperator";
        "testUInt32UnaryNegChecked";
      ]

    let expectedUnoptimized = [
        [], "type OperatorTestsUInt32"
        [], "let testUInt32EqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.uint32> (e1,e2) @ (4,69--4,78)"
        [], "let testUInt32NotEqualsOperator(e1) (e2) = Operators.op_Inequality<Microsoft.FSharp.Core.uint32> (e1,e2) @ (5,69--5,79)"
        [], "let testUInt32LessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.uint32> (e1,e2) @ (6,69--6,78)"
        [], "let testUInt32LessThanOrEqualsOperator(e1) (e2) = Operators.op_LessThanOrEqual<Microsoft.FSharp.Core.uint32> (e1,e2) @ (7,69--7,79)"
        [], "let testUInt32GreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.uint32> (e1,e2) @ (8,69--8,78)"
        [], "let testUInt32GreaterThanOrEqualsOperator(e1) (e2) = Operators.op_GreaterThanOrEqual<Microsoft.FSharp.Core.uint32> (e1,e2) @ (9,69--9,79)"
        [], "let testUInt32AdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (11,61--11,70)"
        [], "let testUInt32SubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (12,61--12,70)"
        [], "let testUInt32MultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (13,61--13,70)"
        [], "let testUInt32DivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (14,61--14,70)"
        [], "let testUInt32ModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (15,61--15,70)"
        [], "let testUInt32BitwiseAndOperator(e1) (e2) = Operators.op_BitwiseAnd<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (16,61--16,72)"
        [], "let testUInt32BitwiseOrOperator(e1) (e2) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (17,61--17,72)"
        [], "let testUInt32BitwiseXorOperator(e1) (e2) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (18,61--18,72)"
        [], "let testUInt32ShiftLeftOperator(e1) (e2) = Operators.op_LeftShift<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (19,58--19,69)"
        [], "let testUInt32ShiftRightOperator(e1) (e2) = Operators.op_RightShift<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (20,58--20,69)"
        [], "let testUInt32AdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (24,59--24,76)"
        [], "let testUInt32SubtractionChecked(e1) (e2) = Checked.op_Subtraction<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (25,59--25,76)"
        [], "let testUInt32MultiplyChecked(e1) (e2) = Checked.op_Multiply<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (26,59--26,76)"
        [], "let testUInt32ToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,47--29,62)"
        [], "let testUInt32ToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,47--30,63)"
        [], "let testUInt32ToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,47--31,63)"
        [], "let testUInt32ToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,47--32,64)"
        [], "let testUInt32ToIntChecked(e1) = Checked.ToInt<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (33,47--33,61)"
        [], "let testUInt32ToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,47--34,63)"
        [], "let testUInt32ToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,47--35,64)"
        [], "let testUInt32ToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,47--36,63)"
        [], "let testUInt32ToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,47--37,64)"
        [], "let testUInt32ToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,47--38,67)"
        [], "let testUInt32ToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,47--39,68)"
        [], "let testUInt32ToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,47--41,54)"
        [], "let testUInt32ToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,47--42,55)"
        [], "let testUInt32ToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,47--43,55)"
        [], "let testUInt32ToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,47--44,56)"
        [], "let testUInt32ToIntOperator(e1) = Operators.ToInt<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (45,47--45,53)"
        [], "let testUInt32ToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,47--46,55)"
        [], "let testUInt32ToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,47--47,56)"
        [], "let testUInt32ToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,47--48,55)"
        [], "let testUInt32ToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,47--49,56)"
        [], "let testUInt32ToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,47--50,59)"
        [], "let testUInt32ToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,47--51,60)"
        [], "let testUInt32ToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,47--52,57)"
        [], "let testUInt32ToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,47--53,55)"
        [], "let testUInt32ToDecimalOperator(e1) = Operators.ToDecimal<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.decimal> (arg0_0),e1) @ (54,47--54,57)"
        [], "let testUInt32ToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,47--55,54)"
        [], "let testUInt32ToStringOperator(e1) = Operators.ToString<Microsoft.FSharp.Core.uint32> (e1) @ (56,47--56,56)"
      ]
    let expectedOptimized = [
        [], "type OperatorTestsUInt32"
        [], "let testUInt32EqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.uint32> (e1,e2) @ (4,69--4,78)"
        [], "let testUInt32NotEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_Equality<Microsoft.FSharp.Core.uint32> (e1,e2),False) @ (5,69--5,79)"
        [], "let testUInt32LessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.uint32> (e1,e2) @ (6,69--6,78)"
        [], "let testUInt32LessThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_GreaterThan<Microsoft.FSharp.Core.uint32> (e1,e2),False) @ (7,69--7,79)"
        [], "let testUInt32GreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.uint32> (e1,e2) @ (8,69--8,78)"
        [], "let testUInt32GreaterThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_LessThan<Microsoft.FSharp.Core.uint32> (e1,e2),False) @ (9,69--9,79)"
        [], "let testUInt32AdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (11,61--11,70)"
        [], "let testUInt32SubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (12,61--12,70)"
        [], "let testUInt32MultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (13,61--13,70)"
        [], "let testUInt32DivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (14,61--14,70)"
        [], "let testUInt32ModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (15,61--15,70)"
        [], "let testUInt32BitwiseAndOperator(e1) (e2) = Operators.op_BitwiseAnd<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (16,61--16,72)"
        [], "let testUInt32BitwiseOrOperator(e1) (e2) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (17,61--17,72)"
        [], "let testUInt32BitwiseXorOperator(e1) (e2) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (18,61--18,72)"
        [], "let testUInt32ShiftLeftOperator(e1) (e2) = Operators.op_LeftShift<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e2,31)) @ (19,58--19,69)"
        [], "let testUInt32ShiftRightOperator(e1) (e2) = Operators.op_RightShift<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e2,31)) @ (20,58--20,69)"
        [], "let testUInt32AdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (24,59--24,76)"
        [], "let testUInt32SubtractionChecked(e1) (e2) = Checked.op_Subtraction<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (25,59--25,76)"
        [], "let testUInt32MultiplyChecked(e1) (e2) = Checked.op_Multiply<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0,arg1_0),e1,e2) @ (26,59--26,76)"
        [], "let testUInt32ToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,47--29,62)"
        [], "let testUInt32ToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,47--30,63)"
        [], "let testUInt32ToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,47--31,63)"
        [], "let testUInt32ToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,47--32,64)"
        [], "let testUInt32ToIntChecked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (33,47--33,61)"
        [], "let testUInt32ToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,47--34,63)"
        [], "let testUInt32ToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,47--35,64)"
        [], "let testUInt32ToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,47--36,63)"
        [], "let testUInt32ToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,47--37,64)"
        [], "let testUInt32ToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,47--38,67)"
        [], "let testUInt32ToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,47--39,68)"
        [], "let testUInt32ToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,47--41,54)"
        [], "let testUInt32ToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,47--42,55)"
        [], "let testUInt32ToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,47--43,55)"
        [], "let testUInt32ToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,47--44,56)"
        [], "let testUInt32ToIntOperator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (45,47--45,53)"
        [], "let testUInt32ToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,47--46,55)"
        [], "let testUInt32ToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,47--47,56)"
        [], "let testUInt32ToInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (48,47--48,55)"
        [], "let testUInt32ToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,47--49,56)"
        [], "let testUInt32ToIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (50,47--50,59)"
        [], "let testUInt32ToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,47--51,60)"
        [], "let testUInt32ToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float32> (arg0_0),Operators.ToDouble<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.float> (arg0_0),e1)) @ (52,47--52,57)"
        [], "let testUInt32ToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0),Operators.ToDouble<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.float> (arg0_0),e1)) @ (53,47--53,55)"
        [], "let testUInt32ToDecimalOperator(e1) = Convert.ToDecimal (e1) @ (54,47--54,57)"
        [], "let testUInt32ToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,47--55,54)"
        [FC47; FC50], "let testUInt32ToStringOperator(e1) = let mutable copyOfStruct: Microsoft.FSharp.Core.uint32 = e1 in copyOfStruct.ToString() @ (56,47--56,56)"
      ]

    testOperators "UInt32" "uint32" excludedTests expectedUnoptimized expectedOptimized

[<Test>]
let ``Test Operator Declarations for Int64`` () =
    let excludedTests = [ ]

    let expectedUnoptimized = [
        [], "type OperatorTestsInt64"
        [], "let testInt64EqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.int64> (e1,e2) @ (4,66--4,75)"
        [], "let testInt64NotEqualsOperator(e1) (e2) = Operators.op_Inequality<Microsoft.FSharp.Core.int64> (e1,e2) @ (5,66--5,76)"
        [], "let testInt64LessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.int64> (e1,e2) @ (6,66--6,75)"
        [], "let testInt64LessThanOrEqualsOperator(e1) (e2) = Operators.op_LessThanOrEqual<Microsoft.FSharp.Core.int64> (e1,e2) @ (7,66--7,76)"
        [], "let testInt64GreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.int64> (e1,e2) @ (8,66--8,75)"
        [], "let testInt64GreaterThanOrEqualsOperator(e1) (e2) = Operators.op_GreaterThanOrEqual<Microsoft.FSharp.Core.int64> (e1,e2) @ (9,66--9,76)"
        [], "let testInt64AdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (11,58--11,67)"
        [], "let testInt64SubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (12,58--12,67)"
        [], "let testInt64MultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (13,58--13,67)"
        [], "let testInt64DivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (14,58--14,67)"
        [], "let testInt64ModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (15,58--15,67)"
        [], "let testInt64BitwiseAndOperator(e1) (e2) = Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (16,58--16,69)"
        [], "let testInt64BitwiseOrOperator(e1) (e2) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (17,58--17,69)"
        [], "let testInt64BitwiseXorOperator(e1) (e2) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (18,58--18,69)"
        [], "let testInt64ShiftLeftOperator(e1) (e2) = Operators.op_LeftShift<Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (19,56--19,67)"
        [], "let testInt64ShiftRightOperator(e1) (e2) = Operators.op_RightShift<Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (20,56--20,67)"
        [], "let testInt64UnaryNegOperator(e1) = Operators.op_UnaryNegation<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (22,45--22,52)"
        [], "let testInt64AdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (24,56--24,73)"
        [], "let testInt64SubtractionChecked(e1) (e2) = Checked.op_Subtraction<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (25,56--25,73)"
        [], "let testInt64MultiplyChecked(e1) (e2) = Checked.op_Multiply<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (26,56--26,73)"
        [], "let testInt64UnaryNegChecked(e1) = Checked.op_UnaryNegation<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (27,45--27,60)"
        [], "let testInt64ToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,45--29,60)"
        [], "let testInt64ToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,45--30,61)"
        [], "let testInt64ToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,45--31,61)"
        [], "let testInt64ToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,45--32,62)"
        [], "let testInt64ToIntChecked(e1) = Checked.ToInt<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (33,45--33,59)"
        [], "let testInt64ToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,45--34,61)"
        [], "let testInt64ToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,45--35,62)"
        [], "let testInt64ToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,45--36,61)"
        [], "let testInt64ToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,45--37,62)"
        [], "let testInt64ToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,45--38,65)"
        [], "let testInt64ToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,45--39,66)"
        [], "let testInt64ToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,45--41,52)"
        [], "let testInt64ToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,45--42,53)"
        [], "let testInt64ToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,45--43,53)"
        [], "let testInt64ToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,45--44,54)"
        [], "let testInt64ToIntOperator(e1) = Operators.ToInt<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (45,45--45,51)"
        [], "let testInt64ToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,45--46,53)"
        [], "let testInt64ToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,45--47,54)"
        [], "let testInt64ToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,45--48,53)"
        [], "let testInt64ToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,45--49,54)"
        [], "let testInt64ToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,45--50,57)"
        [], "let testInt64ToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,45--51,58)"
        [], "let testInt64ToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,45--52,55)"
        [], "let testInt64ToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,45--53,53)"
        [], "let testInt64ToDecimalOperator(e1) = Operators.ToDecimal<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.decimal> (arg0_0),e1) @ (54,45--54,55)"
        [], "let testInt64ToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,45--55,52)"
        [], "let testInt64ToStringOperator(e1) = Operators.ToString<Microsoft.FSharp.Core.int64> (e1) @ (56,45--56,54)"
      ]
    let expectedOptimized = [
        [], "type OperatorTestsInt64"
        [], "let testInt64EqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.int64> (e1,e2) @ (4,66--4,75)"
        [], "let testInt64NotEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_Equality<Microsoft.FSharp.Core.int64> (e1,e2),False) @ (5,66--5,76)"
        [], "let testInt64LessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.int64> (e1,e2) @ (6,66--6,75)"
        [], "let testInt64LessThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_GreaterThan<Microsoft.FSharp.Core.int64> (e1,e2),False) @ (7,66--7,76)"
        [], "let testInt64GreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.int64> (e1,e2) @ (8,66--8,75)"
        [], "let testInt64GreaterThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_LessThan<Microsoft.FSharp.Core.int64> (e1,e2),False) @ (9,66--9,76)"
        [], "let testInt64AdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (11,58--11,67)"
        [], "let testInt64SubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (12,58--12,67)"
        [], "let testInt64MultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (13,58--13,67)"
        [], "let testInt64DivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (14,58--14,67)"
        [], "let testInt64ModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (15,58--15,67)"
        [], "let testInt64BitwiseAndOperator(e1) (e2) = Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (16,58--16,69)"
        [], "let testInt64BitwiseOrOperator(e1) (e2) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (17,58--17,69)"
        [], "let testInt64BitwiseXorOperator(e1) (e2) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (18,58--18,69)"
        [], "let testInt64ShiftLeftOperator(e1) (e2) = Operators.op_LeftShift<Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e2,63)) @ (19,56--19,67)"
        [], "let testInt64ShiftRightOperator(e1) (e2) = Operators.op_RightShift<Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e2,63)) @ (20,56--20,67)"
        [], "let testInt64UnaryNegOperator(e1) = Operators.op_UnaryNegation<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (22,45--22,52)"
        [], "let testInt64AdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (24,56--24,73)"
        [], "let testInt64SubtractionChecked(e1) (e2) = Checked.op_Subtraction<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (25,56--25,73)"
        [], "let testInt64MultiplyChecked(e1) (e2) = Checked.op_Multiply<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),e1,e2) @ (26,56--26,73)"
        [], "let testInt64UnaryNegChecked(e1) = Checked.op_Subtraction<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0,arg1_0),0,e1) @ (27,45--27,60)"
        [], "let testInt64ToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,45--29,60)"
        [], "let testInt64ToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,45--30,61)"
        [], "let testInt64ToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,45--31,61)"
        [], "let testInt64ToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,45--32,62)"
        [], "let testInt64ToIntChecked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (33,45--33,59)"
        [], "let testInt64ToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,45--34,61)"
        [], "let testInt64ToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,45--35,62)"
        [], "let testInt64ToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,45--36,61)"
        [], "let testInt64ToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,45--37,62)"
        [], "let testInt64ToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,45--38,65)"
        [], "let testInt64ToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,45--39,66)"
        [], "let testInt64ToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,45--41,52)"
        [], "let testInt64ToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,45--42,53)"
        [], "let testInt64ToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,45--43,53)"
        [], "let testInt64ToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,45--44,54)"
        [], "let testInt64ToIntOperator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (45,45--45,51)"
        [], "let testInt64ToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,45--46,53)"
        [], "let testInt64ToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,45--47,54)"
        [], "let testInt64ToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,45--48,53)"
        [], "let testInt64ToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,45--49,54)"
        [], "let testInt64ToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,45--50,57)"
        [], "let testInt64ToUIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (51,45--51,58)"
        [], "let testInt64ToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,45--52,55)"
        [], "let testInt64ToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,45--53,53)"
        [], "let testInt64ToDecimalOperator(e1) = Convert.ToDecimal (e1) @ (54,45--54,55)"
        [], "let testInt64ToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.int64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int64,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,45--55,52)"
        [FC47; FC50], "let testInt64ToStringOperator(e1) = IntrinsicFunctions.UnboxGeneric<System.IFormattable> (Operators.Box<Microsoft.FSharp.Core.int64> (e1)).ToString(dflt,CultureInfo.get_InvariantCulture () :> System.IFormatProvider) @ (56,45--56,54)"
      ]


    testOperators "Int64" "int64" excludedTests expectedUnoptimized expectedOptimized

[<Test>]
let ``Test Operator Declarations for UInt64`` () =
    let excludedTests = [
        "testUInt64UnaryNegOperator";
        "testUInt64UnaryNegChecked";
      ]

    let expectedUnoptimized = [
        [], "type OperatorTestsUInt64"
        [], "let testUInt64EqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.uint64> (e1,e2) @ (4,69--4,78)"
        [], "let testUInt64NotEqualsOperator(e1) (e2) = Operators.op_Inequality<Microsoft.FSharp.Core.uint64> (e1,e2) @ (5,69--5,79)"
        [], "let testUInt64LessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.uint64> (e1,e2) @ (6,69--6,78)"
        [], "let testUInt64LessThanOrEqualsOperator(e1) (e2) = Operators.op_LessThanOrEqual<Microsoft.FSharp.Core.uint64> (e1,e2) @ (7,69--7,79)"
        [], "let testUInt64GreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.uint64> (e1,e2) @ (8,69--8,78)"
        [], "let testUInt64GreaterThanOrEqualsOperator(e1) (e2) = Operators.op_GreaterThanOrEqual<Microsoft.FSharp.Core.uint64> (e1,e2) @ (9,69--9,79)"
        [], "let testUInt64AdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (11,61--11,70)"
        [], "let testUInt64SubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (12,61--12,70)"
        [], "let testUInt64MultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (13,61--13,70)"
        [], "let testUInt64DivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (14,61--14,70)"
        [], "let testUInt64ModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (15,61--15,70)"
        [], "let testUInt64BitwiseAndOperator(e1) (e2) = Operators.op_BitwiseAnd<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (16,61--16,72)"
        [], "let testUInt64BitwiseOrOperator(e1) (e2) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (17,61--17,72)"
        [], "let testUInt64BitwiseXorOperator(e1) (e2) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (18,61--18,72)"
        [], "let testUInt64ShiftLeftOperator(e1) (e2) = Operators.op_LeftShift<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (19,58--19,69)"
        [], "let testUInt64ShiftRightOperator(e1) (e2) = Operators.op_RightShift<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (20,58--20,69)"
        [], "let testUInt64AdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (24,59--24,76)"
        [], "let testUInt64SubtractionChecked(e1) (e2) = Checked.op_Subtraction<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (25,59--25,76)"
        [], "let testUInt64MultiplyChecked(e1) (e2) = Checked.op_Multiply<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (26,59--26,76)"
        [], "let testUInt64ToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,47--29,62)"
        [], "let testUInt64ToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,47--30,63)"
        [], "let testUInt64ToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,47--31,63)"
        [], "let testUInt64ToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,47--32,64)"
        [], "let testUInt64ToIntChecked(e1) = Checked.ToInt<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (33,47--33,61)"
        [], "let testUInt64ToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,47--34,63)"
        [], "let testUInt64ToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,47--35,64)"
        [], "let testUInt64ToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,47--36,63)"
        [], "let testUInt64ToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,47--37,64)"
        [], "let testUInt64ToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,47--38,67)"
        [], "let testUInt64ToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,47--39,68)"
        [], "let testUInt64ToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,47--41,54)"
        [], "let testUInt64ToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,47--42,55)"
        [], "let testUInt64ToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,47--43,55)"
        [], "let testUInt64ToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,47--44,56)"
        [], "let testUInt64ToIntOperator(e1) = Operators.ToInt<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (45,47--45,53)"
        [], "let testUInt64ToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,47--46,55)"
        [], "let testUInt64ToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,47--47,56)"
        [], "let testUInt64ToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,47--48,55)"
        [], "let testUInt64ToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,47--49,56)"
        [], "let testUInt64ToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,47--50,59)"
        [], "let testUInt64ToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,47--51,60)"
        [], "let testUInt64ToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,47--52,57)"
        [], "let testUInt64ToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,47--53,55)"
        [], "let testUInt64ToDecimalOperator(e1) = Operators.ToDecimal<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.decimal> (arg0_0),e1) @ (54,47--54,57)"
        [], "let testUInt64ToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,47--55,54)"
        [], "let testUInt64ToStringOperator(e1) = Operators.ToString<Microsoft.FSharp.Core.uint64> (e1) @ (56,47--56,56)"
      ]
    let expectedOptimized = [
        [], "type OperatorTestsUInt64"
        [], "let testUInt64EqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.uint64> (e1,e2) @ (4,69--4,78)"
        [], "let testUInt64NotEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_Equality<Microsoft.FSharp.Core.uint64> (e1,e2),False) @ (5,69--5,79)"
        [], "let testUInt64LessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.uint64> (e1,e2) @ (6,69--6,78)"
        [], "let testUInt64LessThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_GreaterThan<Microsoft.FSharp.Core.uint64> (e1,e2),False) @ (7,69--7,79)"
        [], "let testUInt64GreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.uint64> (e1,e2) @ (8,69--8,78)"
        [], "let testUInt64GreaterThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_LessThan<Microsoft.FSharp.Core.uint64> (e1,e2),False) @ (9,69--9,79)"
        [], "let testUInt64AdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (11,61--11,70)"
        [], "let testUInt64SubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (12,61--12,70)"
        [], "let testUInt64MultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (13,61--13,70)"
        [], "let testUInt64DivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (14,61--14,70)"
        [], "let testUInt64ModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (15,61--15,70)"
        [], "let testUInt64BitwiseAndOperator(e1) (e2) = Operators.op_BitwiseAnd<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (16,61--16,72)"
        [], "let testUInt64BitwiseOrOperator(e1) (e2) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (17,61--17,72)"
        [], "let testUInt64BitwiseXorOperator(e1) (e2) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (18,61--18,72)"
        [], "let testUInt64ShiftLeftOperator(e1) (e2) = Operators.op_LeftShift<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e2,63)) @ (19,58--19,69)"
        [], "let testUInt64ShiftRightOperator(e1) (e2) = Operators.op_RightShift<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,Operators.op_BitwiseAnd<Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),e2,63)) @ (20,58--20,69)"
        [], "let testUInt64AdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (24,59--24,76)"
        [], "let testUInt64SubtractionChecked(e1) (e2) = Checked.op_Subtraction<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (25,59--25,76)"
        [], "let testUInt64MultiplyChecked(e1) (e2) = Checked.op_Multiply<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0,arg1_0),e1,e2) @ (26,59--26,76)"
        [], "let testUInt64ToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,47--29,62)"
        [], "let testUInt64ToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,47--30,63)"
        [], "let testUInt64ToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,47--31,63)"
        [], "let testUInt64ToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,47--32,64)"
        [], "let testUInt64ToIntChecked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (33,47--33,61)"
        [], "let testUInt64ToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,47--34,63)"
        [], "let testUInt64ToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,47--35,64)"
        [], "let testUInt64ToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,47--36,63)"
        [], "let testUInt64ToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,47--37,64)"
        [], "let testUInt64ToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,47--38,67)"
        [], "let testUInt64ToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,47--39,68)"
        [], "let testUInt64ToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,47--41,54)"
        [], "let testUInt64ToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,47--42,55)"
        [], "let testUInt64ToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,47--43,55)"
        [], "let testUInt64ToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,47--44,56)"
        [], "let testUInt64ToIntOperator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (45,47--45,53)"
        [], "let testUInt64ToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,47--46,55)"
        [], "let testUInt64ToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,47--47,56)"
        [], "let testUInt64ToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,47--48,55)"
        [], "let testUInt64ToUInt64Operator(e1) = e1 @ (49,54--49,56)"
        [], "let testUInt64ToIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (50,47--50,59)"
        [], "let testUInt64ToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,47--51,60)"
        [], "let testUInt64ToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float32> (arg0_0),Operators.ToDouble<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.float> (arg0_0),e1)) @ (52,47--52,57)"
        [], "let testUInt64ToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0),Operators.ToDouble<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.float> (arg0_0),e1)) @ (53,47--53,55)"
        [], "let testUInt64ToDecimalOperator(e1) = Convert.ToDecimal (e1) @ (54,47--54,57)"
        [], "let testUInt64ToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.uint64> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint64,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,47--55,54)"
        [FC47; FC50], "let testUInt64ToStringOperator(e1) = let mutable copyOfStruct: Microsoft.FSharp.Core.uint64 = e1 in copyOfStruct.ToString() @ (56,47--56,56)"
      ]

    testOperators "UInt64" "uint64" excludedTests expectedUnoptimized expectedOptimized

[<Test>]
let ``Test Operator Declarations for IntPtr`` () =
    let excludedTests = [ ]

    let expectedUnoptimized = [
        [], "type OperatorTestsIntPtr"
        [], "let testIntPtrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.nativeint> (e1,e2) @ (4,75--4,84)"
        [], "let testIntPtrNotEqualsOperator(e1) (e2) = Operators.op_Inequality<Microsoft.FSharp.Core.nativeint> (e1,e2) @ (5,75--5,85)"
        [], "let testIntPtrLessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.nativeint> (e1,e2) @ (6,75--6,84)"
        [], "let testIntPtrLessThanOrEqualsOperator(e1) (e2) = Operators.op_LessThanOrEqual<Microsoft.FSharp.Core.nativeint> (e1,e2) @ (7,75--7,85)"
        [], "let testIntPtrGreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.nativeint> (e1,e2) @ (8,75--8,84)"
        [], "let testIntPtrGreaterThanOrEqualsOperator(e1) (e2) = Operators.op_GreaterThanOrEqual<Microsoft.FSharp.Core.nativeint> (e1,e2) @ (9,75--9,85)"
        [], "let testIntPtrAdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (11,67--11,76)"
        [], "let testIntPtrSubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (12,67--12,76)"
        [], "let testIntPtrMultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (13,67--13,76)"
        [], "let testIntPtrDivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (14,67--14,76)"
        [], "let testIntPtrModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (15,67--15,76)"
        [], "let testIntPtrBitwiseAndOperator(e1) (e2) = Operators.op_BitwiseAnd<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (16,67--16,78)"
        [], "let testIntPtrBitwiseOrOperator(e1) (e2) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (17,67--17,78)"
        [], "let testIntPtrBitwiseXorOperator(e1) (e2) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (18,67--18,78)"
        [], "let testIntPtrShiftLeftOperator(e1) (e2) = Operators.op_LeftShift<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (19,61--19,72)"
        [], "let testIntPtrShiftRightOperator(e1) (e2) = Operators.op_RightShift<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (20,61--20,72)"
        [], "let testIntPtrUnaryNegOperator(e1) = Operators.op_UnaryNegation<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (22,50--22,57)"
        [], "let testIntPtrAdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (24,65--24,82)"
        [], "let testIntPtrSubtractionChecked(e1) (e2) = Checked.op_Subtraction<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (25,65--25,82)"
        [], "let testIntPtrMultiplyChecked(e1) (e2) = Checked.op_Multiply<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (26,65--26,82)"
        [], "let testIntPtrUnaryNegChecked(e1) = Checked.op_UnaryNegation<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (27,50--27,65)"
        [], "let testIntPtrToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,50--29,65)"
        [], "let testIntPtrToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,50--30,66)"
        [], "let testIntPtrToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,50--31,66)"
        [], "let testIntPtrToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,50--32,67)"
        [], "let testIntPtrToIntChecked(e1) = Checked.ToInt<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (33,50--33,64)"
        [], "let testIntPtrToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,50--34,66)"
        [], "let testIntPtrToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,50--35,67)"
        [], "let testIntPtrToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,50--36,66)"
        [], "let testIntPtrToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,50--37,67)"
        [], "let testIntPtrToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,50--38,70)"
        [], "let testIntPtrToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,50--39,71)"
        [], "let testIntPtrToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,50--41,57)"
        [], "let testIntPtrToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,50--42,58)"
        [], "let testIntPtrToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,50--43,58)"
        [], "let testIntPtrToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,50--44,59)"
        [], "let testIntPtrToIntOperator(e1) = Operators.ToInt<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (45,50--45,56)"
        [], "let testIntPtrToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,50--46,58)"
        [], "let testIntPtrToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,50--47,59)"
        [], "let testIntPtrToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,50--48,58)"
        [], "let testIntPtrToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,50--49,59)"
        [], "let testIntPtrToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,50--50,62)"
        [], "let testIntPtrToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,50--51,63)"
        [], "let testIntPtrToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,50--52,60)"
        [], "let testIntPtrToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,50--53,58)"
        [], "let testIntPtrToDecimalOperator(e1) = Operators.ToDecimal<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.decimal> (arg0_0),e1) @ (54,50--54,60)"
        [], "let testIntPtrToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,50--55,57)"
        [], "let testIntPtrToStringOperator(e1) = Operators.ToString<Microsoft.FSharp.Core.nativeint> (e1) @ (56,50--56,59)"
      ]
    let expectedOptimized = [
        [], "type OperatorTestsIntPtr"
        [], "let testIntPtrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.nativeint> (e1,e2) @ (4,75--4,84)"
        [], "let testIntPtrNotEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_Equality<Microsoft.FSharp.Core.nativeint> (e1,e2),False) @ (5,75--5,85)"
        [], "let testIntPtrLessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.nativeint> (e1,e2) @ (6,75--6,84)"
        [], "let testIntPtrLessThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_GreaterThan<Microsoft.FSharp.Core.nativeint> (e1,e2),False) @ (7,75--7,85)"
        [], "let testIntPtrGreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.nativeint> (e1,e2) @ (8,75--8,84)"
        [], "let testIntPtrGreaterThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_LessThan<Microsoft.FSharp.Core.nativeint> (e1,e2),False) @ (9,75--9,85)"
        [], "let testIntPtrAdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (11,67--11,76)"
        [], "let testIntPtrSubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (12,67--12,76)"
        [], "let testIntPtrMultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (13,67--13,76)"
        [], "let testIntPtrDivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (14,67--14,76)"
        [], "let testIntPtrModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (15,67--15,76)"
        [], "let testIntPtrBitwiseAndOperator(e1) (e2) = Operators.op_BitwiseAnd<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (16,67--16,78)"
        [], "let testIntPtrBitwiseOrOperator(e1) (e2) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (17,67--17,78)"
        [], "let testIntPtrBitwiseXorOperator(e1) (e2) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (18,67--18,78)"
        [], "let testIntPtrShiftLeftOperator(e1) (e2) = Operators.op_LeftShift<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (19,61--19,72)"
        [], "let testIntPtrShiftRightOperator(e1) (e2) = Operators.op_RightShift<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (20,61--20,72)"
        [], "let testIntPtrUnaryNegOperator(e1) = Operators.op_UnaryNegation<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (22,50--22,57)"
        [], "let testIntPtrAdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (24,65--24,82)"
        [], "let testIntPtrSubtractionChecked(e1) (e2) = Checked.op_Subtraction<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (25,65--25,82)"
        [], "let testIntPtrMultiplyChecked(e1) (e2) = Checked.op_Multiply<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),e1,e2) @ (26,65--26,82)"
        [], "let testIntPtrUnaryNegChecked(e1) = Checked.op_Subtraction<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0,arg1_0),0,e1) @ (27,50--27,65)"
        [], "let testIntPtrToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,50--29,65)"
        [], "let testIntPtrToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,50--30,66)"
        [], "let testIntPtrToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,50--31,66)"
        [], "let testIntPtrToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,50--32,67)"
        [], "let testIntPtrToIntChecked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (33,50--33,64)"
        [], "let testIntPtrToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,50--34,66)"
        [], "let testIntPtrToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,50--35,67)"
        [], "let testIntPtrToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,50--36,66)"
        [], "let testIntPtrToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,50--37,67)"
        [], "let testIntPtrToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,50--38,70)"
        [], "let testIntPtrToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,50--39,71)"
        [], "let testIntPtrToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,50--41,57)"
        [], "let testIntPtrToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,50--42,58)"
        [], "let testIntPtrToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,50--43,58)"
        [], "let testIntPtrToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,50--44,59)"
        [], "let testIntPtrToIntOperator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (45,50--45,56)"
        [], "let testIntPtrToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,50--46,58)"
        [], "let testIntPtrToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,50--47,59)"
        [], "let testIntPtrToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,50--48,58)"
        [], "let testIntPtrToUInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (49,50--49,59)"
        [], "let testIntPtrToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,50--50,62)"
        [], "let testIntPtrToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,50--51,63)"
        [], "let testIntPtrToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,50--52,60)"
        [], "let testIntPtrToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,50--53,58)"
        [], "let testIntPtrToDecimalOperator(e1) = Convert.ToDecimal (Operators.ToInt64<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.int64> (arg0_0),e1)) @ (54,50--54,60)"
        [], "let testIntPtrToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.nativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.nativeint,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,50--55,57)"
        [FC47; FC50], "let testIntPtrToStringOperator(e1) = e1.ToString() @ (56,50--56,59)"
      ]

    testOperators "IntPtr" "nativeint" excludedTests expectedUnoptimized expectedOptimized

[<Test>]
let ``Test Operator Declarations for UIntPtr`` () =
    let excludedTests = [
        "testUIntPtrUnaryNegOperator";
        "testUIntPtrUnaryNegChecked";
      ]

    let expectedUnoptimized = [
        [], "type OperatorTestsUIntPtr"
        [], "let testUIntPtrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.unativeint> (e1,e2) @ (4,78--4,87)"
        [], "let testUIntPtrNotEqualsOperator(e1) (e2) = Operators.op_Inequality<Microsoft.FSharp.Core.unativeint> (e1,e2) @ (5,78--5,88)"
        [], "let testUIntPtrLessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.unativeint> (e1,e2) @ (6,78--6,87)"
        [], "let testUIntPtrLessThanOrEqualsOperator(e1) (e2) = Operators.op_LessThanOrEqual<Microsoft.FSharp.Core.unativeint> (e1,e2) @ (7,78--7,88)"
        [], "let testUIntPtrGreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.unativeint> (e1,e2) @ (8,78--8,87)"
        [], "let testUIntPtrGreaterThanOrEqualsOperator(e1) (e2) = Operators.op_GreaterThanOrEqual<Microsoft.FSharp.Core.unativeint> (e1,e2) @ (9,78--9,88)"
        [], "let testUIntPtrAdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (11,70--11,79)"
        [], "let testUIntPtrSubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (12,70--12,79)"
        [], "let testUIntPtrMultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (13,70--13,79)"
        [], "let testUIntPtrDivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (14,70--14,79)"
        [], "let testUIntPtrModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (15,70--15,79)"
        [], "let testUIntPtrBitwiseAndOperator(e1) (e2) = Operators.op_BitwiseAnd<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (16,70--16,81)"
        [], "let testUIntPtrBitwiseOrOperator(e1) (e2) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (17,70--17,81)"
        [], "let testUIntPtrBitwiseXorOperator(e1) (e2) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (18,70--18,81)"
        [], "let testUIntPtrShiftLeftOperator(e1) (e2) = Operators.op_LeftShift<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (19,63--19,74)"
        [], "let testUIntPtrShiftRightOperator(e1) (e2) = Operators.op_RightShift<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (20,63--20,74)"
        [], "let testUIntPtrAdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (24,68--24,85)"
        [], "let testUIntPtrSubtractionChecked(e1) (e2) = Checked.op_Subtraction<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (25,68--25,85)"
        [], "let testUIntPtrMultiplyChecked(e1) (e2) = Checked.op_Multiply<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (26,68--26,85)"
        [], "let testUIntPtrToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,52--29,67)"
        [], "let testUIntPtrToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,52--30,68)"
        [], "let testUIntPtrToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,52--31,68)"
        [], "let testUIntPtrToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,52--32,69)"
        [], "let testUIntPtrToIntChecked(e1) = Checked.ToInt<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (33,52--33,66)"
        [], "let testUIntPtrToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,52--34,68)"
        [], "let testUIntPtrToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,52--35,69)"
        [], "let testUIntPtrToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,52--36,68)"
        [], "let testUIntPtrToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,52--37,69)"
        [], "let testUIntPtrToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,52--38,72)"
        [], "let testUIntPtrToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,52--39,73)"
        [], "let testUIntPtrToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,52--41,59)"
        [], "let testUIntPtrToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,52--42,60)"
        [], "let testUIntPtrToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,52--43,60)"
        [], "let testUIntPtrToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,52--44,61)"
        [], "let testUIntPtrToIntOperator(e1) = Operators.ToInt<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (45,52--45,58)"
        [], "let testUIntPtrToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,52--46,60)"
        [], "let testUIntPtrToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,52--47,61)"
        [], "let testUIntPtrToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,52--48,60)"
        [], "let testUIntPtrToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,52--49,61)"
        [], "let testUIntPtrToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,52--50,64)"
        [], "let testUIntPtrToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,52--51,65)"
        [], "let testUIntPtrToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,52--52,62)"
        [], "let testUIntPtrToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,52--53,60)"
        [], "let testUIntPtrToDecimalOperator(e1) = Operators.ToDecimal<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.decimal> (arg0_0),e1) @ (54,52--54,62)"
        [], "let testUIntPtrToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,52--55,59)"
        [], "let testUIntPtrToStringOperator(e1) = Operators.ToString<Microsoft.FSharp.Core.unativeint> (e1) @ (56,52--56,61)"
      ]
    let expectedOptimized = [
        [], "type OperatorTestsUIntPtr"
        [], "let testUIntPtrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.unativeint> (e1,e2) @ (4,78--4,87)"
        [], "let testUIntPtrNotEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_Equality<Microsoft.FSharp.Core.unativeint> (e1,e2),False) @ (5,78--5,88)"
        [], "let testUIntPtrLessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.unativeint> (e1,e2) @ (6,78--6,87)"
        [], "let testUIntPtrLessThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_GreaterThan<Microsoft.FSharp.Core.unativeint> (e1,e2),False) @ (7,78--7,88)"
        [], "let testUIntPtrGreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.unativeint> (e1,e2) @ (8,78--8,87)"
        [], "let testUIntPtrGreaterThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_LessThan<Microsoft.FSharp.Core.unativeint> (e1,e2),False) @ (9,78--9,88)"
        [], "let testUIntPtrAdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (11,70--11,79)"
        [], "let testUIntPtrSubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (12,70--12,79)"
        [], "let testUIntPtrMultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (13,70--13,79)"
        [], "let testUIntPtrDivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (14,70--14,79)"
        [], "let testUIntPtrModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (15,70--15,79)"
        [], "let testUIntPtrBitwiseAndOperator(e1) (e2) = Operators.op_BitwiseAnd<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseAndDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (16,70--16,81)"
        [], "let testUIntPtrBitwiseOrOperator(e1) (e2) = Operators.op_BitwiseOr<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.BitwiseOrDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (17,70--17,81)"
        [], "let testUIntPtrBitwiseXorOperator(e1) (e2) = Operators.op_ExclusiveOr<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ExclusiveOrDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (18,70--18,81)"
        [], "let testUIntPtrShiftLeftOperator(e1) (e2) = Operators.op_LeftShift<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.LeftShiftDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (19,63--19,74)"
        [], "let testUIntPtrShiftRightOperator(e1) (e2) = Operators.op_RightShift<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.RightShiftDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.int32,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (20,63--20,74)"
        [], "let testUIntPtrAdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (24,68--24,85)"
        [], "let testUIntPtrSubtractionChecked(e1) (e2) = Checked.op_Subtraction<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (25,68--25,85)"
        [], "let testUIntPtrMultiplyChecked(e1) (e2) = Checked.op_Multiply<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0,arg1_0),e1,e2) @ (26,68--26,85)"
        [], "let testUIntPtrToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,52--29,67)"
        [], "let testUIntPtrToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,52--30,68)"
        [], "let testUIntPtrToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,52--31,68)"
        [], "let testUIntPtrToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,52--32,69)"
        [], "let testUIntPtrToIntChecked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (33,52--33,66)"
        [], "let testUIntPtrToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,52--34,68)"
        [], "let testUIntPtrToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,52--35,69)"
        [], "let testUIntPtrToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,52--36,68)"
        [], "let testUIntPtrToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,52--37,69)"
        [], "let testUIntPtrToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,52--38,72)"
        [], "let testUIntPtrToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,52--39,73)"
        [], "let testUIntPtrToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,52--41,59)"
        [], "let testUIntPtrToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,52--42,60)"
        [], "let testUIntPtrToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,52--43,60)"
        [], "let testUIntPtrToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,52--44,61)"
        [], "let testUIntPtrToIntOperator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (45,52--45,58)"
        [], "let testUIntPtrToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,52--46,60)"
        [], "let testUIntPtrToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,52--47,61)"
        [], "let testUIntPtrToInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (48,52--48,60)"
        [], "let testUIntPtrToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,52--49,61)"
        [], "let testUIntPtrToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,52--50,64)"
        [], "let testUIntPtrToUIntPtrOperator(e1) = e1 @ (51,63--51,65)"
        [], "let testUIntPtrToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float32> (arg0_0),Operators.ToDouble<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.float> (arg0_0),e1)) @ (52,52--52,62)"
        [], "let testUIntPtrToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0),Operators.ToDouble<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.float> (arg0_0),e1)) @ (53,52--53,60)"
        [], "let testUIntPtrToDecimalOperator(e1) = Convert.ToDecimal (Operators.ToUInt64<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.uint64> (arg0_0),e1)) @ (54,52--54,62)"
        [], "let testUIntPtrToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.unativeint> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.unativeint,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,52--55,59)"
        [FC47; FC50], "let testUIntPtrToStringOperator(e1) = e1.ToString() @ (56,52--56,61)"
      ]

    testOperators "UIntPtr" "unativeint" excludedTests expectedUnoptimized expectedOptimized

[<Test>]
let ``Test Operator Declarations for Single`` () =
    let excludedTests = [
        "testSingleBitwiseAndOperator";
        "testSingleBitwiseOrOperator";
        "testSingleBitwiseXorOperator";
        "testSingleShiftLeftOperator";
        "testSingleShiftRightOperator";
      ]

    let expectedUnoptimized = [
        [], "type OperatorTestsSingle"
        [], "let testSingleEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.float32> (e1,e2) @ (4,71--4,80)"
        [], "let testSingleNotEqualsOperator(e1) (e2) = Operators.op_Inequality<Microsoft.FSharp.Core.float32> (e1,e2) @ (5,71--5,81)"
        [], "let testSingleLessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.float32> (e1,e2) @ (6,71--6,80)"
        [], "let testSingleLessThanOrEqualsOperator(e1) (e2) = Operators.op_LessThanOrEqual<Microsoft.FSharp.Core.float32> (e1,e2) @ (7,71--7,81)"
        [], "let testSingleGreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.float32> (e1,e2) @ (8,71--8,80)"
        [], "let testSingleGreaterThanOrEqualsOperator(e1) (e2) = Operators.op_GreaterThanOrEqual<Microsoft.FSharp.Core.float32> (e1,e2) @ (9,71--9,81)"
        [], "let testSingleAdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (arg0_0,arg1_0),e1,e2) @ (11,63--11,72)"
        [], "let testSingleSubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (arg0_0,arg1_0),e1,e2) @ (12,63--12,72)"
        [], "let testSingleMultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (arg0_0,arg1_0),e1,e2) @ (13,63--13,72)"
        [], "let testSingleDivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (arg0_0,arg1_0),e1,e2) @ (14,63--14,72)"
        [], "let testSingleModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (arg0_0,arg1_0),e1,e2) @ (15,63--15,72)"
        [], "let testSingleUnaryNegOperator(e1) = Operators.op_UnaryNegation<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (22,48--22,55)"
        [], "let testSingleAdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (arg0_0,arg1_0),e1,e2) @ (24,61--24,78)"
        [], "let testSingleSubtractionChecked(e1) (e2) = Checked.op_Subtraction<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (arg0_0,arg1_0),e1,e2) @ (25,61--25,78)"
        [], "let testSingleMultiplyChecked(e1) (e2) = Checked.op_Multiply<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (arg0_0,arg1_0),e1,e2) @ (26,61--26,78)"
        [], "let testSingleUnaryNegChecked(e1) = Checked.op_UnaryNegation<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (27,48--27,63)"
        [], "let testSingleToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,48--29,63)"
        [], "let testSingleToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,48--30,64)"
        [], "let testSingleToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,48--31,64)"
        [], "let testSingleToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,48--32,65)"
        [], "let testSingleToIntChecked(e1) = Checked.ToInt<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (33,48--33,62)"
        [], "let testSingleToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,48--34,64)"
        [], "let testSingleToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,48--35,65)"
        [], "let testSingleToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,48--36,64)"
        [], "let testSingleToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,48--37,65)"
        [], "let testSingleToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,48--38,68)"
        [], "let testSingleToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,48--39,69)"
        [], "let testSingleToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,48--41,55)"
        [], "let testSingleToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,48--42,56)"
        [], "let testSingleToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,48--43,56)"
        [], "let testSingleToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,48--44,57)"
        [], "let testSingleToIntOperator(e1) = Operators.ToInt<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (45,48--45,54)"
        [], "let testSingleToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,48--46,56)"
        [], "let testSingleToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,48--47,57)"
        [], "let testSingleToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,48--48,56)"
        [], "let testSingleToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,48--49,57)"
        [], "let testSingleToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,48--50,60)"
        [], "let testSingleToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,48--51,61)"
        [], "let testSingleToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,48--52,58)"
        [], "let testSingleToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,48--53,56)"
        [], "let testSingleToDecimalOperator(e1) = Operators.ToDecimal<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.decimal> (arg0_0),e1) @ (54,48--54,58)"
        [], "let testSingleToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,48--55,55)"
        [], "let testSingleToStringOperator(e1) = Operators.ToString<Microsoft.FSharp.Core.float32> (e1) @ (56,48--56,57)"
      ]
    let expectedOptimized = [
        [], "type OperatorTestsSingle"
        [], "let testSingleEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.float32> (e1,e2) @ (4,71--4,80)"
        [], "let testSingleNotEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_Equality<Microsoft.FSharp.Core.float32> (e1,e2),False) @ (5,71--5,81)"
        [], "let testSingleLessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.float32> (e1,e2) @ (6,71--6,80)"
        [], "let testSingleLessThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_GreaterThan<Microsoft.FSharp.Core.float32> (e1,e2),False) @ (7,71--7,81)"
        [], "let testSingleGreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.float32> (e1,e2) @ (8,71--8,80)"
        [], "let testSingleGreaterThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_LessThan<Microsoft.FSharp.Core.float32> (e1,e2),False) @ (9,71--9,81)"
        [], "let testSingleAdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (arg0_0,arg1_0),e1,e2) @ (11,63--11,72)"
        [], "let testSingleSubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (arg0_0,arg1_0),e1,e2) @ (12,63--12,72)"
        [], "let testSingleMultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (arg0_0,arg1_0),e1,e2) @ (13,63--13,72)"
        [], "let testSingleDivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (arg0_0,arg1_0),e1,e2) @ (14,63--14,72)"
        [], "let testSingleModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (arg0_0,arg1_0),e1,e2) @ (15,63--15,72)"
        [], "let testSingleUnaryNegOperator(e1) = Operators.op_UnaryNegation<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (22,48--22,55)"
        [], "let testSingleAdditionChecked(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (arg0_0,arg1_0),e1,e2) @ (24,61--24,78)"
        [], "let testSingleSubtractionChecked(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (arg0_0,arg1_0),e1,e2) @ (25,61--25,78)"
        [], "let testSingleMultiplyChecked(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (arg0_0,arg1_0),e1,e2) @ (26,61--26,78)"
        [], "let testSingleUnaryNegChecked(e1) = Operators.op_UnaryNegation<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (27,48--27,63)"
        [], "let testSingleToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,48--29,63)"
        [], "let testSingleToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,48--30,64)"
        [], "let testSingleToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,48--31,64)"
        [], "let testSingleToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,48--32,65)"
        [], "let testSingleToIntChecked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (33,48--33,62)"
        [], "let testSingleToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,48--34,64)"
        [], "let testSingleToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,48--35,65)"
        [], "let testSingleToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,48--36,64)"
        [], "let testSingleToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,48--37,65)"
        [], "let testSingleToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,48--38,68)"
        [], "let testSingleToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,48--39,69)"
        [], "let testSingleToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,48--41,55)"
        [], "let testSingleToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,48--42,56)"
        [], "let testSingleToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,48--43,56)"
        [], "let testSingleToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,48--44,57)"
        [], "let testSingleToIntOperator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (45,48--45,54)"
        [], "let testSingleToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,48--46,56)"
        [], "let testSingleToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,48--47,57)"
        [], "let testSingleToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,48--48,56)"
        [], "let testSingleToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,48--49,57)"
        [], "let testSingleToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,48--50,60)"
        [], "let testSingleToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,48--51,61)"
        [], "let testSingleToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,48--52,58)"
        [], "let testSingleToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,48--53,56)"
        [], "let testSingleToDecimalOperator(e1) = Convert.ToDecimal (e1) @ (54,48--54,58)"
        [], "let testSingleToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.float32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,48--55,55)"
        [FC47; FC50], "let testSingleToStringOperator(e1) = e1.ToString(dflt,CultureInfo.get_InvariantCulture () :> System.IFormatProvider) @ (56,48--56,57)"
      ]

    testOperators "Single" "float32" excludedTests expectedUnoptimized expectedOptimized

[<Test>]
let ``Test Operator Declarations for Single with unit of measure`` () =
    let excludedTests = [
        "testSingleUnitizedBitwiseAndOperator";
        "testSingleUnitizedBitwiseOrOperator";
        "testSingleUnitizedBitwiseXorOperator";
        "testSingleUnitizedShiftLeftOperator";
        "testSingleUnitizedShiftRightOperator";
      ]

    let expectedUnoptimized = [
        [], "type OperatorTestsSingleUnitized"
        [], "let testSingleUnitizedEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (e1,e2) @ (4,163--4,172)"
        [], "let testSingleUnitizedNotEqualsOperator(e1) (e2) = Operators.op_Inequality<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (e1,e2) @ (5,163--5,173)"
        [], "let testSingleUnitizedLessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (e1,e2) @ (6,163--6,172)"
        [], "let testSingleUnitizedLessThanOrEqualsOperator(e1) (e2) = Operators.op_LessThanOrEqual<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (e1,e2) @ (7,163--7,173)"
        [], "let testSingleUnitizedGreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (e1,e2) @ (8,163--8,172)"
        [], "let testSingleUnitizedGreaterThanOrEqualsOperator(e1) (e2) = Operators.op_GreaterThanOrEqual<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (e1,e2) @ (9,163--9,173)"
        [], "let testSingleUnitizedAdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (arg0_0,arg1_0),e1,e2) @ (11,155--11,164)"
        [], "let testSingleUnitizedSubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (arg0_0,arg1_0),e1,e2) @ (12,155--12,164)"
        [], "let testSingleUnitizedMultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m ^ 2>> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m ^ 2>> (arg0_0,arg1_0),e1,e2) @ (13,155--13,164)"
        [], "let testSingleUnitizedDivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32> (arg0_0,arg1_0),e1,e2) @ (14,155--14,164)"
        [], "let testSingleUnitizedModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (arg0_0,arg1_0),e1,e2) @ (15,155--15,164)"
        [], "let testSingleUnitizedUnaryNegOperator(e1) = Operators.op_UnaryNegation<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (arg0_0),e1) @ (22,98--22,105)"
        [], "let testSingleUnitizedAdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (arg0_0,arg1_0),e1,e2) @ (24,153--24,170)"
        [], "let testSingleUnitizedSubtractionChecked(e1) (e2) = Checked.op_Subtraction<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (arg0_0,arg1_0),e1,e2) @ (25,153--25,170)"
        [], "let testSingleUnitizedMultiplyChecked(e1) (e2) = Checked.op_Multiply<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m ^ 2>> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m ^ 2>> (arg0_0,arg1_0),e1,e2) @ (26,153--26,170)"
        [], "let testSingleUnitizedUnaryNegChecked(e1) = Checked.op_UnaryNegation<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (arg0_0),e1) @ (27,98--27,113)"
        [], "let testSingleUnitizedToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,98--29,113)"
        [], "let testSingleUnitizedToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,98--30,114)"
        [], "let testSingleUnitizedToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,98--31,114)"
        [], "let testSingleUnitizedToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,98--32,115)"
        [], "let testSingleUnitizedToIntChecked(e1) = Checked.ToInt<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (33,98--33,112)"
        [], "let testSingleUnitizedToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,98--34,114)"
        [], "let testSingleUnitizedToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,98--35,115)"
        [], "let testSingleUnitizedToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,98--36,114)"
        [], "let testSingleUnitizedToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,98--37,115)"
        [], "let testSingleUnitizedToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,98--38,118)"
        [], "let testSingleUnitizedToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,98--39,119)"
        [], "let testSingleUnitizedToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,98--41,105)"
        [], "let testSingleUnitizedToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,98--42,106)"
        [], "let testSingleUnitizedToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,98--43,106)"
        [], "let testSingleUnitizedToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,98--44,107)"
        [], "let testSingleUnitizedToIntOperator(e1) = Operators.ToInt<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (45,98--45,104)"
        [], "let testSingleUnitizedToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,98--46,106)"
        [], "let testSingleUnitizedToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,98--47,107)"
        [], "let testSingleUnitizedToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,98--48,106)"
        [], "let testSingleUnitizedToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,98--49,107)"
        [], "let testSingleUnitizedToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,98--50,110)"
        [], "let testSingleUnitizedToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,98--51,111)"
        [], "let testSingleUnitizedToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,98--52,108)"
        [], "let testSingleUnitizedToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,98--53,106)"
        [], "let testSingleUnitizedToDecimalOperator(e1) = Operators.ToDecimal<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal> (arg0_0),e1) @ (54,98--54,108)"
        [], "let testSingleUnitizedToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,98--55,105)"
        [], "let testSingleUnitizedToStringOperator(e1) = Operators.ToString<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (e1) @ (56,98--56,107)"
      ]
    let expectedOptimized = [
        [], "type OperatorTestsSingleUnitized"
        [], "let testSingleUnitizedEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (e1,e2) @ (4,163--4,172)"
        [], "let testSingleUnitizedNotEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_Equality<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (e1,e2),False) @ (5,163--5,173)"
        [], "let testSingleUnitizedLessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (e1,e2) @ (6,163--6,172)"
        [], "let testSingleUnitizedLessThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_GreaterThan<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (e1,e2),False) @ (7,163--7,173)"
        [], "let testSingleUnitizedGreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (e1,e2) @ (8,163--8,172)"
        [], "let testSingleUnitizedGreaterThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_LessThan<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (e1,e2),False) @ (9,163--9,173)"
        [], "let testSingleUnitizedAdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (arg0_0,arg1_0),e1,e2) @ (11,155--11,164)"
        [], "let testSingleUnitizedSubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (arg0_0,arg1_0),e1,e2) @ (12,155--12,164)"
        [], "let testSingleUnitizedMultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m ^ 2>> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m ^ 2>> (arg0_0,arg1_0),e1,e2) @ (13,155--13,164)"
        [], "let testSingleUnitizedDivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32> (arg0_0,arg1_0),e1,e2) @ (14,155--14,164)"
        [], "let testSingleUnitizedModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (arg0_0,arg1_0),e1,e2) @ (15,155--15,164)"
        [], "let testSingleUnitizedUnaryNegOperator(e1) = Operators.op_UnaryNegation<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (arg0_0),e1) @ (22,98--22,105)"
        [], "let testSingleUnitizedAdditionChecked(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (arg0_0,arg1_0),e1,e2) @ (24,153--24,170)"
        [], "let testSingleUnitizedSubtractionChecked(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (arg0_0,arg1_0),e1,e2) @ (25,153--25,170)"
        [], "let testSingleUnitizedMultiplyChecked(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m ^ 2>> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m ^ 2>> (arg0_0,arg1_0),e1,e2) @ (26,153--26,170)"
        [], "let testSingleUnitizedUnaryNegChecked(e1) = Operators.op_UnaryNegation<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (arg0_0),e1) @ (27,98--27,113)"
        [], "let testSingleUnitizedToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,98--29,113)"
        [], "let testSingleUnitizedToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,98--30,114)"
        [], "let testSingleUnitizedToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,98--31,114)"
        [], "let testSingleUnitizedToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,98--32,115)"
        [], "let testSingleUnitizedToIntChecked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (33,98--33,112)"
        [], "let testSingleUnitizedToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,98--34,114)"
        [], "let testSingleUnitizedToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,98--35,115)"
        [], "let testSingleUnitizedToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,98--36,114)"
        [], "let testSingleUnitizedToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,98--37,115)"
        [], "let testSingleUnitizedToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,98--38,118)"
        [], "let testSingleUnitizedToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,98--39,119)"
        [], "let testSingleUnitizedToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,98--41,105)"
        [], "let testSingleUnitizedToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,98--42,106)"
        [], "let testSingleUnitizedToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,98--43,106)"
        [], "let testSingleUnitizedToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,98--44,107)"
        [], "let testSingleUnitizedToIntOperator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (45,98--45,104)"
        [], "let testSingleUnitizedToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,98--46,106)"
        [], "let testSingleUnitizedToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,98--47,107)"
        [], "let testSingleUnitizedToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,98--48,106)"
        [], "let testSingleUnitizedToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,98--49,107)"
        [], "let testSingleUnitizedToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,98--50,110)"
        [], "let testSingleUnitizedToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,98--51,111)"
        [], "let testSingleUnitizedToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,98--52,108)"
        [], "let testSingleUnitizedToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,98--53,106)"
        [], "let testSingleUnitizedToDecimalOperator(e1) = Convert.ToDecimal (Operators.ToSingle<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32> (arg0_0),e1)) @ (54,98--54,108)"
        [], "let testSingleUnitizedToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,98--55,105)"
        [FC47; FC50], "let testSingleUnitizedToStringOperator(e1) = let x: Microsoft.FSharp.Core.float32 = Operators.ToSingle<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float32<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.float32> (arg0_0),e1) in x.ToString(dflt,CultureInfo.get_InvariantCulture () :> System.IFormatProvider) @ (56,98--56,107)"
      ]

    testOperators "SingleUnitized" "float32<FSharp.Data.UnitSystems.SI.UnitSymbols.m>" excludedTests expectedUnoptimized expectedOptimized

[<Test>]
let ``Test Operator Declarations for Double`` () =
    let excludedTests = [
        "testDoubleBitwiseAndOperator";
        "testDoubleBitwiseOrOperator";
        "testDoubleBitwiseXorOperator";
        "testDoubleShiftLeftOperator";
        "testDoubleShiftRightOperator";
      ]

    let expectedUnoptimized = [
        [], "type OperatorTestsDouble"
        [], "let testDoubleEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.float> (e1,e2) @ (4,67--4,76)"
        [], "let testDoubleNotEqualsOperator(e1) (e2) = Operators.op_Inequality<Microsoft.FSharp.Core.float> (e1,e2) @ (5,67--5,77)"
        [], "let testDoubleLessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.float> (e1,e2) @ (6,67--6,76)"
        [], "let testDoubleLessThanOrEqualsOperator(e1) (e2) = Operators.op_LessThanOrEqual<Microsoft.FSharp.Core.float> (e1,e2) @ (7,67--7,77)"
        [], "let testDoubleGreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.float> (e1,e2) @ (8,67--8,76)"
        [], "let testDoubleGreaterThanOrEqualsOperator(e1) (e2) = Operators.op_GreaterThanOrEqual<Microsoft.FSharp.Core.float> (e1,e2) @ (9,67--9,77)"
        [], "let testDoubleAdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0,arg1_0),e1,e2) @ (11,59--11,68)"
        [], "let testDoubleSubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0,arg1_0),e1,e2) @ (12,59--12,68)"
        [], "let testDoubleMultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0,arg1_0),e1,e2) @ (13,59--13,68)"
        [], "let testDoubleDivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0,arg1_0),e1,e2) @ (14,59--14,68)"
        [], "let testDoubleModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0,arg1_0),e1,e2) @ (15,59--15,68)"
        [], "let testDoubleUnaryNegOperator(e1) = Operators.op_UnaryNegation<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (22,46--22,53)"
        [], "let testDoubleAdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0,arg1_0),e1,e2) @ (24,57--24,74)"
        [], "let testDoubleSubtractionChecked(e1) (e2) = Checked.op_Subtraction<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0,arg1_0),e1,e2) @ (25,57--25,74)"
        [], "let testDoubleMultiplyChecked(e1) (e2) = Checked.op_Multiply<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0,arg1_0),e1,e2) @ (26,57--26,74)"
        [], "let testDoubleUnaryNegChecked(e1) = Checked.op_UnaryNegation<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (27,46--27,61)"
        [], "let testDoubleToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,46--29,61)"
        [], "let testDoubleToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,46--30,62)"
        [], "let testDoubleToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,46--31,62)"
        [], "let testDoubleToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,46--32,63)"
        [], "let testDoubleToIntChecked(e1) = Checked.ToInt<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (33,46--33,60)"
        [], "let testDoubleToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,46--34,62)"
        [], "let testDoubleToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,46--35,63)"
        [], "let testDoubleToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,46--36,62)"
        [], "let testDoubleToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,46--37,63)"
        [], "let testDoubleToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,46--38,66)"
        [], "let testDoubleToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,46--39,67)"
        [], "let testDoubleToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,46--41,53)"
        [], "let testDoubleToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,46--42,54)"
        [], "let testDoubleToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,46--43,54)"
        [], "let testDoubleToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,46--44,55)"
        [], "let testDoubleToIntOperator(e1) = Operators.ToInt<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (45,46--45,52)"
        [], "let testDoubleToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,46--46,54)"
        [], "let testDoubleToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,46--47,55)"
        [], "let testDoubleToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,46--48,54)"
        [], "let testDoubleToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,46--49,55)"
        [], "let testDoubleToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,46--50,58)"
        [], "let testDoubleToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,46--51,59)"
        [], "let testDoubleToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,46--52,56)"
        [], "let testDoubleToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,46--53,54)"
        [], "let testDoubleToDecimalOperator(e1) = Operators.ToDecimal<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.decimal> (arg0_0),e1) @ (54,46--54,56)"
        [], "let testDoubleToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,46--55,53)"
        [], "let testDoubleToStringOperator(e1) = Operators.ToString<Microsoft.FSharp.Core.float> (e1) @ (56,46--56,55)"
      ]
    let expectedOptimized = [
        [], "type OperatorTestsDouble"
        [], "let testDoubleEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.float> (e1,e2) @ (4,67--4,76)"
        [], "let testDoubleNotEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_Equality<Microsoft.FSharp.Core.float> (e1,e2),False) @ (5,67--5,77)"
        [], "let testDoubleLessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.float> (e1,e2) @ (6,67--6,76)"
        [], "let testDoubleLessThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_GreaterThan<Microsoft.FSharp.Core.float> (e1,e2),False) @ (7,67--7,77)"
        [], "let testDoubleGreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.float> (e1,e2) @ (8,67--8,76)"
        [], "let testDoubleGreaterThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_LessThan<Microsoft.FSharp.Core.float> (e1,e2),False) @ (9,67--9,77)"
        [], "let testDoubleAdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0,arg1_0),e1,e2) @ (11,59--11,68)"
        [], "let testDoubleSubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0,arg1_0),e1,e2) @ (12,59--12,68)"
        [], "let testDoubleMultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0,arg1_0),e1,e2) @ (13,59--13,68)"
        [], "let testDoubleDivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0,arg1_0),e1,e2) @ (14,59--14,68)"
        [], "let testDoubleModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0,arg1_0),e1,e2) @ (15,59--15,68)"
        [], "let testDoubleUnaryNegOperator(e1) = Operators.op_UnaryNegation<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (22,46--22,53)"
        [], "let testDoubleAdditionChecked(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0,arg1_0),e1,e2) @ (24,57--24,74)"
        [], "let testDoubleSubtractionChecked(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0,arg1_0),e1,e2) @ (25,57--25,74)"
        [], "let testDoubleMultiplyChecked(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0,arg1_0),e1,e2) @ (26,57--26,74)"
        [], "let testDoubleUnaryNegChecked(e1) = Operators.op_UnaryNegation<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (27,46--27,61)"
        [], "let testDoubleToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,46--29,61)"
        [], "let testDoubleToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,46--30,62)"
        [], "let testDoubleToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,46--31,62)"
        [], "let testDoubleToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,46--32,63)"
        [], "let testDoubleToIntChecked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (33,46--33,60)"
        [], "let testDoubleToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,46--34,62)"
        [], "let testDoubleToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,46--35,63)"
        [], "let testDoubleToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,46--36,62)"
        [], "let testDoubleToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,46--37,63)"
        [], "let testDoubleToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,46--38,66)"
        [], "let testDoubleToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,46--39,67)"
        [], "let testDoubleToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,46--41,53)"
        [], "let testDoubleToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,46--42,54)"
        [], "let testDoubleToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,46--43,54)"
        [], "let testDoubleToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,46--44,55)"
        [], "let testDoubleToIntOperator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (45,46--45,52)"
        [], "let testDoubleToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,46--46,54)"
        [], "let testDoubleToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,46--47,55)"
        [], "let testDoubleToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,46--48,54)"
        [], "let testDoubleToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,46--49,55)"
        [], "let testDoubleToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,46--50,58)"
        [], "let testDoubleToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,46--51,59)"
        [], "let testDoubleToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,46--52,56)"
        [], "let testDoubleToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,46--53,54)"
        [], "let testDoubleToDecimalOperator(e1) = Convert.ToDecimal (e1) @ (54,46--54,56)"
        [], "let testDoubleToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,46--55,53)"
        [FC47; FC50], "let testDoubleToStringOperator(e1) = e1.ToString(dflt,CultureInfo.get_InvariantCulture () :> System.IFormatProvider) @ (56,46--56,55)"
      ]


    testOperators "Double" "float" excludedTests expectedUnoptimized expectedOptimized

[<Test>]
let ``Test Operator Declarations for Decimal`` () =
    let excludedTests = [
        // None of these are supported for decimals
        "testDecimalBitwiseAndOperator";
        "testDecimalBitwiseOrOperator";
        "testDecimalBitwiseXorOperator";
        "testDecimalShiftLeftOperator";
        "testDecimalShiftRightOperator";
        "testDecimalUnaryNegOperator";
        "testDecimalSubtractionChecked";
        "testDecimalMultiplyChecked";
        "testDecimalUnaryNegChecked";
        "testDecimalToIntPtrChecked";
        "testDecimalToUIntPtrChecked";
        "testDecimalToIntPtrOperator";
        "testDecimalToUIntPtrOperator";
      ]

    let expectedUnoptimized = [
        [], "type OperatorTestsDecimal"
        [], "let testDecimalEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.decimal> (e1,e2) @ (4,72--4,81)"
        [], "let testDecimalNotEqualsOperator(e1) (e2) = Operators.op_Inequality<Microsoft.FSharp.Core.decimal> (e1,e2) @ (5,72--5,82)"
        [], "let testDecimalLessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.decimal> (e1,e2) @ (6,72--6,81)"
        [], "let testDecimalLessThanOrEqualsOperator(e1) (e2) = Operators.op_LessThanOrEqual<Microsoft.FSharp.Core.decimal> (e1,e2) @ (7,72--7,82)"
        [], "let testDecimalGreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.decimal> (e1,e2) @ (8,72--8,81)"
        [], "let testDecimalGreaterThanOrEqualsOperator(e1) (e2) = Operators.op_GreaterThanOrEqual<Microsoft.FSharp.Core.decimal> (e1,e2) @ (9,72--9,82)"
        [], "let testDecimalAdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.decimal,Microsoft.FSharp.Core.decimal,Microsoft.FSharp.Core.decimal> (fun arg0_0 -> fun arg1_0 -> Decimal.op_Addition (arg0_0,arg1_0),e1,e2) @ (11,64--11,73)"
        [], "let testDecimalSubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.decimal,Microsoft.FSharp.Core.decimal,Microsoft.FSharp.Core.decimal> (fun arg0_0 -> fun arg1_0 -> Decimal.op_Subtraction (arg0_0,arg1_0),e1,e2) @ (12,64--12,73)"
        [], "let testDecimalMultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.decimal,Microsoft.FSharp.Core.decimal,Microsoft.FSharp.Core.decimal> (fun arg0_0 -> fun arg1_0 -> Decimal.op_Multiply (arg0_0,arg1_0),e1,e2) @ (13,64--13,73)"
        [], "let testDecimalDivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.decimal,Microsoft.FSharp.Core.decimal,Microsoft.FSharp.Core.decimal> (fun arg0_0 -> fun arg1_0 -> Decimal.op_Division (arg0_0,arg1_0),e1,e2) @ (14,64--14,73)"
        [], "let testDecimalModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.decimal,Microsoft.FSharp.Core.decimal,Microsoft.FSharp.Core.decimal> (fun arg0_0 -> fun arg1_0 -> Decimal.op_Modulus (arg0_0,arg1_0),e1,e2) @ (15,64--15,73)"
        [], "let testDecimalAdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.decimal,Microsoft.FSharp.Core.decimal,Microsoft.FSharp.Core.decimal> (fun arg0_0 -> fun arg1_0 -> Decimal.op_Addition (arg0_0,arg1_0),e1,e2) @ (24,62--24,79)"
        [], "let testDecimalToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.decimal> (fun arg0_0 -> Decimal.op_Explicit (arg0_0),e1) @ (29,49--29,64)"
        [], "let testDecimalToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.decimal> (fun arg0_0 -> Decimal.op_Explicit (arg0_0),e1) @ (30,49--30,65)"
        [], "let testDecimalToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.decimal> (fun arg0_0 -> Decimal.op_Explicit (arg0_0),e1) @ (31,49--31,65)"
        [], "let testDecimalToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.decimal> (fun arg0_0 -> Decimal.op_Explicit (arg0_0),e1) @ (32,49--32,66)"
        [], "let testDecimalToIntChecked(e1) = Checked.ToInt<Microsoft.FSharp.Core.decimal> (fun arg0_0 -> Decimal.op_Explicit (arg0_0),e1) @ (33,49--33,63)"
        [], "let testDecimalToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.decimal> (fun arg0_0 -> Decimal.op_Explicit (arg0_0),e1) @ (34,49--34,65)"
        [], "let testDecimalToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.decimal> (fun arg0_0 -> Decimal.op_Explicit (arg0_0),e1) @ (35,49--35,66)"
        [], "let testDecimalToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.decimal> (fun arg0_0 -> Decimal.op_Explicit (arg0_0),e1) @ (36,49--36,65)"
        [], "let testDecimalToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.decimal> (fun arg0_0 -> Decimal.op_Explicit (arg0_0),e1) @ (37,49--37,66)"
        [], "let testDecimalToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.decimal> (fun arg0_0 -> Decimal.op_Explicit (arg0_0),e1) @ (41,49--41,56)"
        [], "let testDecimalToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.decimal> (fun arg0_0 -> Decimal.op_Explicit (arg0_0),e1) @ (42,49--42,57)"
        [], "let testDecimalToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.decimal> (fun arg0_0 -> Decimal.op_Explicit (arg0_0),e1) @ (43,49--43,57)"
        [], "let testDecimalToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.decimal> (fun arg0_0 -> Decimal.op_Explicit (arg0_0),e1) @ (44,49--44,58)"
        [], "let testDecimalToIntOperator(e1) = Operators.ToInt<Microsoft.FSharp.Core.decimal> (fun arg0_0 -> Decimal.op_Explicit (arg0_0),e1) @ (45,49--45,55)"
        [], "let testDecimalToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.decimal> (fun arg0_0 -> Decimal.op_Explicit (arg0_0),e1) @ (46,49--46,57)"
        [], "let testDecimalToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.decimal> (fun arg0_0 -> Decimal.op_Explicit (arg0_0),e1) @ (47,49--47,58)"
        [], "let testDecimalToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.decimal> (fun arg0_0 -> Decimal.op_Explicit (arg0_0),e1) @ (48,49--48,57)"
        [], "let testDecimalToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.decimal> (fun arg0_0 -> Decimal.op_Explicit (arg0_0),e1) @ (49,49--49,58)"
        [], "let testDecimalToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.decimal> (fun arg0_0 -> Decimal.op_Explicit (arg0_0),e1) @ (52,49--52,59)"
        [], "let testDecimalToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.decimal> (fun arg0_0 -> Decimal.op_Explicit (arg0_0),e1) @ (53,49--53,57)"
        [], "let testDecimalToDecimalOperator(e1) = Operators.ToDecimal<Microsoft.FSharp.Core.decimal> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.decimal,Microsoft.FSharp.Core.decimal> (arg0_0),e1) @ (54,49--54,59)"
        [], "let testDecimalToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.decimal> (fun arg0_0 -> Decimal.op_Explicit (arg0_0),e1) @ (55,49--55,56)"
        [], "let testDecimalToStringOperator(e1) = Operators.ToString<Microsoft.FSharp.Core.decimal> (e1) @ (56,49--56,58)"
      ]
    let expectedOptimized = [
        [], "type OperatorTestsDecimal"
        [], "let testDecimalEqualsOperator(e1) (e2) = Decimal.op_Equality (e1,e2) @ (4,72--4,81)"
        [], "let testDecimalNotEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Decimal.op_Equality (e1,e2),False) @ (5,72--5,82)"
        [], "let testDecimalLessThanOperator(e1) (e2) = Decimal.op_LessThan (e1,e2) @ (6,72--6,81)"
        [], "let testDecimalLessThanOrEqualsOperator(e1) (e2) = Decimal.op_LessThanOrEqual (e1,e2) @ (7,72--7,82)"
        [], "let testDecimalGreaterThanOperator(e1) (e2) = Decimal.op_GreaterThan (e1,e2) @ (8,72--8,81)"
        [], "let testDecimalGreaterThanOrEqualsOperator(e1) (e2) = Decimal.op_GreaterThanOrEqual (e1,e2) @ (9,72--9,82)"
        [], "let testDecimalAdditionOperator(e1) (e2) = Decimal.op_Addition (e1,e2) @ (11,64--11,73)"
        [], "let testDecimalSubtractionOperator(e1) (e2) = Decimal.op_Subtraction (e1,e2) @ (12,64--12,73)"
        [], "let testDecimalMultiplyOperator(e1) (e2) = Decimal.op_Multiply (e1,e2) @ (13,64--13,73)"
        [], "let testDecimalDivisionOperator(e1) (e2) = Decimal.op_Division (e1,e2) @ (14,64--14,73)"
        [], "let testDecimalModulusOperator(e1) (e2) = Decimal.op_Modulus (e1,e2) @ (15,64--15,73)"
        [], "let testDecimalAdditionChecked(e1) (e2) = Decimal.op_Addition (e1,e2) @ (24,62--24,79)"
        [], "let testDecimalToByteChecked(e1) = Decimal.op_Explicit (e1) @ (29,49--29,64)"
        [], "let testDecimalToSByteChecked(e1) = Decimal.op_Explicit (e1) @ (30,49--30,65)"
        [], "let testDecimalToInt16Checked(e1) = Decimal.op_Explicit (e1) @ (31,49--31,65)"
        [], "let testDecimalToUInt16Checked(e1) = Decimal.op_Explicit (e1) @ (32,49--32,66)"
        [], "let testDecimalToIntChecked(e1) = Decimal.op_Explicit (e1) @ (33,49--33,63)"
        [], "let testDecimalToInt32Checked(e1) = Decimal.op_Explicit (e1) @ (34,49--34,65)"
        [], "let testDecimalToUInt32Checked(e1) = Decimal.op_Explicit (e1) @ (35,49--35,66)"
        [], "let testDecimalToInt64Checked(e1) = Decimal.op_Explicit (e1) @ (36,49--36,65)"
        [], "let testDecimalToUInt64Checked(e1) = Decimal.op_Explicit (e1) @ (37,49--37,66)"
        [], "let testDecimalToByteOperator(e1) = Decimal.op_Explicit (e1) @ (41,49--41,56)"
        [], "let testDecimalToSByteOperator(e1) = Decimal.op_Explicit (e1) @ (42,49--42,57)"
        [], "let testDecimalToInt16Operator(e1) = Decimal.op_Explicit (e1) @ (43,49--43,57)"
        [], "let testDecimalToUInt16Operator(e1) = Decimal.op_Explicit (e1) @ (44,49--44,58)"
        [], "let testDecimalToIntOperator(e1) = Decimal.op_Explicit (e1) @ (45,49--45,55)"
        [], "let testDecimalToInt32Operator(e1) = Decimal.op_Explicit (e1) @ (46,49--46,57)"
        [], "let testDecimalToUInt32Operator(e1) = Decimal.op_Explicit (e1) @ (47,49--47,58)"
        [], "let testDecimalToInt64Operator(e1) = Decimal.op_Explicit (e1) @ (48,49--48,57)"
        [], "let testDecimalToUInt64Operator(e1) = Decimal.op_Explicit (e1) @ (49,49--49,58)"
        [], "let testDecimalToSingleOperator(e1) = Decimal.op_Explicit (e1) @ (52,49--52,59)"
        [], "let testDecimalToDoubleOperator(e1) = Convert.ToDouble (e1) @ (53,49--53,57)"
        [], "let testDecimalToDecimalOperator(e1) = e1 @ (54,57--54,59)"
        [], "let testDecimalToCharOperator(e1) = Decimal.op_Explicit (e1) @ (55,49--55,56)"
        [FC47; FC50], "let testDecimalToStringOperator(e1) = e1.ToString(dflt,CultureInfo.get_InvariantCulture () :> System.IFormatProvider) @ (56,49--56,58)"
      ]


    testOperators "Decimal" "decimal" excludedTests expectedUnoptimized expectedOptimized

[<Test>]
let ``Test Operator Declarations for Decimal unitized`` () =
    let excludedTests = [
        // None of these are supported for unitized decimals
        "testDecimalUnitizedBitwiseAndOperator";
        "testDecimalUnitizedBitwiseOrOperator";
        "testDecimalUnitizedBitwiseXorOperator";
        "testDecimalUnitizedShiftLeftOperator";
        "testDecimalUnitizedShiftRightOperator";
        "testDecimalUnitizedToSByte";
        "testDecimalUnitizedToIntPtrChecked";
        "testDecimalUnitizedToUIntPtrChecked";
        "testDecimalUnitizedToIntPtrOperator";
        "testDecimalUnitizedToUIntPtrOperator";
        "testDecimalUnitizedToByteChecked";
        "testDecimalUnitizedToSByteChecked";
        "testDecimalUnitizedToInt16Checked";
        "testDecimalUnitizedToUInt16Checked";
        "testDecimalUnitizedToIntChecked";
        "testDecimalUnitizedToInt32Checked";
        "testDecimalUnitizedToUInt32Checked";
        "testDecimalUnitizedToInt64Checked";
        "testDecimalUnitizedToUInt64Checked";
        "testDecimalUnitizedToByteOperator";
        "testDecimalUnitizedToSByteOperator";
        "testDecimalUnitizedToInt16Operator";
        "testDecimalUnitizedToUInt16Operator";
        "testDecimalUnitizedToIntOperator";
        "testDecimalUnitizedToInt32Operator";
        "testDecimalUnitizedToUInt32Operator";
        "testDecimalUnitizedToInt64Operator";
        "testDecimalUnitizedToUInt64Operator";
        "testDecimalUnitizedToSingleOperator";
        "testDecimalUnitizedToDoubleOperator";
        "testDecimalUnitizedToCharOperator";
      ]

    let expectedUnoptimized = [
        [], "type OperatorTestsDecimalUnitized"
        [], "let testDecimalUnitizedEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (e1,e2) @ (4,164--4,173)"
        [], "let testDecimalUnitizedNotEqualsOperator(e1) (e2) = Operators.op_Inequality<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (e1,e2) @ (5,164--5,174)"
        [], "let testDecimalUnitizedLessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (e1,e2) @ (6,164--6,173)"
        [], "let testDecimalUnitizedLessThanOrEqualsOperator(e1) (e2) = Operators.op_LessThanOrEqual<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (e1,e2) @ (7,164--7,174)"
        [], "let testDecimalUnitizedGreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (e1,e2) @ (8,164--8,173)"
        [], "let testDecimalUnitizedGreaterThanOrEqualsOperator(e1) (e2) = Operators.op_GreaterThanOrEqual<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (e1,e2) @ (9,164--9,174)"
        [], "let testDecimalUnitizedAdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (arg0_0,arg1_0),e1,e2) @ (11,156--11,165)"
        [], "let testDecimalUnitizedSubtractionOperator(e1) (e2) = Operators.op_Subtraction<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (arg0_0,arg1_0),e1,e2) @ (12,156--12,165)"
        [], "let testDecimalUnitizedMultiplyOperator(e1) (e2) = Operators.op_Multiply<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m ^ 2>> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m ^ 2>> (arg0_0,arg1_0),e1,e2) @ (13,156--13,165)"
        [], "let testDecimalUnitizedDivisionOperator(e1) (e2) = Operators.op_Division<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal> (arg0_0,arg1_0),e1,e2) @ (14,156--14,165)"
        [], "let testDecimalUnitizedModulusOperator(e1) (e2) = Operators.op_Modulus<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.ModulusDynamic<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (arg0_0,arg1_0),e1,e2) @ (15,156--15,165)"
        [], "let testDecimalUnitizedUnaryNegOperator(e1) = Operators.op_UnaryNegation<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (arg0_0),e1) @ (22,99--22,106)"
        [], "let testDecimalUnitizedAdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (arg0_0,arg1_0),e1,e2) @ (24,154--24,171)"
        [], "let testDecimalUnitizedSubtractionChecked(e1) (e2) = Checked.op_Subtraction<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.SubtractionDynamic<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (arg0_0,arg1_0),e1,e2) @ (25,154--25,171)"
        [], "let testDecimalUnitizedMultiplyChecked(e1) (e2) = Checked.op_Multiply<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m ^ 2>> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.MultiplyDynamic<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m ^ 2>> (arg0_0,arg1_0),e1,e2) @ (26,154--26,171)"
        [], "let testDecimalUnitizedUnaryNegChecked(e1) = Checked.op_UnaryNegation<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (arg0_0),e1) @ (27,99--27,114)"
        [], "let testDecimalUnitizedToDecimalOperator(e1) = Operators.ToDecimal<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>,Microsoft.FSharp.Core.decimal> (arg0_0),e1) @ (54,99--54,109)"
        [], "let testDecimalUnitizedToStringOperator(e1) = Operators.ToString<Microsoft.FSharp.Core.decimal<Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m>> (e1) @ (56,99--56,108)"
      ]
    let expectedOptimized = [
        [], "type OperatorTestsDecimalUnitized"
        [], "let testDecimalUnitizedEqualsOperator(e1) (e2) = Decimal.op_Equality (e1,e2) @ (4,164--4,173)"
        [], "let testDecimalUnitizedNotEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Decimal.op_Equality (e1,e2),False) @ (5,164--5,174)"
        [], "let testDecimalUnitizedLessThanOperator(e1) (e2) = Decimal.op_LessThan (e1,e2) @ (6,164--6,173)"
        [], "let testDecimalUnitizedLessThanOrEqualsOperator(e1) (e2) = Decimal.op_LessThanOrEqual (e1,e2) @ (7,164--7,174)"
        [], "let testDecimalUnitizedGreaterThanOperator(e1) (e2) = Decimal.op_GreaterThan (e1,e2) @ (8,164--8,173)"
        [], "let testDecimalUnitizedGreaterThanOrEqualsOperator(e1) (e2) = Decimal.op_GreaterThanOrEqual (e1,e2) @ (9,164--9,174)"
        [], "let testDecimalUnitizedAdditionOperator(e1) (e2) = Decimal.op_Addition (e1,e2) @ (11,156--11,165)"
        [], "let testDecimalUnitizedSubtractionOperator(e1) (e2) = Decimal.op_Subtraction (e1,e2) @ (12,156--12,165)"
        [], "let testDecimalUnitizedMultiplyOperator(e1) (e2) = Decimal.op_Multiply (e1,e2) @ (13,156--13,165)"
        [], "let testDecimalUnitizedDivisionOperator(e1) (e2) = Decimal.op_Division (e1,e2) @ (14,156--14,165)"
        [], "let testDecimalUnitizedModulusOperator(e1) (e2) = Decimal.op_Modulus (e1,e2) @ (15,156--15,165)"
        [], "let testDecimalUnitizedUnaryNegOperator(e1) = Decimal.op_UnaryNegation (e1) @ (22,99--22,106)"
        [], "let testDecimalUnitizedAdditionChecked(e1) (e2) = Decimal.op_Addition (e1,e2) @ (24,154--24,171)"
        [], "let testDecimalUnitizedSubtractionChecked(e1) (e2) = Decimal.op_Subtraction (e1,e2) @ (25,154--25,171)"
        [], "let testDecimalUnitizedMultiplyChecked(e1) (e2) = Decimal.op_Multiply (e1,e2) @ (26,154--26,171)"
        [], "let testDecimalUnitizedUnaryNegChecked(e1) = Decimal.op_UnaryNegation (e1) @ (27,99--27,114)"
        [], "let testDecimalUnitizedToDecimalOperator(e1) = e1 @ (54,99--54,109)"
        [FC47; FC50], "let testDecimalUnitizedToStringOperator(e1) = let x: Microsoft.FSharp.Core.decimal = e1 in x.ToString(dflt,CultureInfo.get_InvariantCulture () :> System.IFormatProvider) @ (56,99--56,108)"
      ]
    testOperators "DecimalUnitized" "decimal<FSharp.Data.UnitSystems.SI.UnitSymbols.m>" excludedTests expectedUnoptimized expectedOptimized

[<Test>]
let ``Test Operator Declarations for Char`` () =
    let excludedTests = [
        "testCharSubtractionOperator";
        "testCharMultiplyOperator";
        "testCharDivisionOperator";
        "testCharModulusOperator";
        "testCharBitwiseAndOperator";
        "testCharBitwiseOrOperator";
        "testCharBitwiseXorOperator";
        "testCharShiftLeftOperator";
        "testCharShiftRightOperator";
        "testCharUnaryNegOperator";
        "testCharSubtractionChecked";
        "testCharMultiplyChecked";
        "testCharUnaryNegChecked";
        "testCharToDecimalOperator";
      ]

    let expectedUnoptimized = [
        [], "type OperatorTestsChar"
        [], "let testCharEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.char> (e1,e2) @ (4,63--4,72)"
        [], "let testCharNotEqualsOperator(e1) (e2) = Operators.op_Inequality<Microsoft.FSharp.Core.char> (e1,e2) @ (5,63--5,73)"
        [], "let testCharLessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.char> (e1,e2) @ (6,63--6,72)"
        [], "let testCharLessThanOrEqualsOperator(e1) (e2) = Operators.op_LessThanOrEqual<Microsoft.FSharp.Core.char> (e1,e2) @ (7,63--7,73)"
        [], "let testCharGreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.char> (e1,e2) @ (8,63--8,72)"
        [], "let testCharGreaterThanOrEqualsOperator(e1) (e2) = Operators.op_GreaterThanOrEqual<Microsoft.FSharp.Core.char> (e1,e2) @ (9,63--9,73)"
        [], "let testCharAdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.char> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.char> (arg0_0,arg1_0),e1,e2) @ (11,55--11,64)"
        [], "let testCharAdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.char> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.char> (arg0_0,arg1_0),e1,e2) @ (24,53--24,70)"
        [], "let testCharToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,43--29,58)"
        [], "let testCharToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,43--30,59)"
        [], "let testCharToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,43--31,59)"
        [], "let testCharToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,43--32,60)"
        [], "let testCharToIntChecked(e1) = Checked.ToInt<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (33,43--33,57)"
        [], "let testCharToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,43--34,59)"
        [], "let testCharToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,43--35,60)"
        [], "let testCharToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,43--36,59)"
        [], "let testCharToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,43--37,60)"
        [], "let testCharToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,43--38,63)"
        [], "let testCharToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,43--39,64)"
        [], "let testCharToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,43--41,50)"
        [], "let testCharToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,43--42,51)"
        [], "let testCharToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,43--43,51)"
        [], "let testCharToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,43--44,52)"
        [], "let testCharToIntOperator(e1) = Operators.ToInt<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (45,43--45,49)"
        [], "let testCharToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,43--46,51)"
        [], "let testCharToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,43--47,52)"
        [], "let testCharToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,43--48,51)"
        [], "let testCharToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,43--49,52)"
        [], "let testCharToIntPtrOperator(e1) = Operators.ToIntPtr<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (50,43--50,55)"
        [], "let testCharToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,43--51,56)"
        [], "let testCharToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,43--52,53)"
        [], "let testCharToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,43--53,51)"
        [], "let testCharToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,43--55,50)"
        [], "let testCharToStringOperator(e1) = Operators.ToString<Microsoft.FSharp.Core.char> (e1) @ (56,43--56,52)"
      ]
    let expectedOptimized = [
        [], "type OperatorTestsChar"
        [], "let testCharEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.char> (e1,e2) @ (4,63--4,72)"
        [], "let testCharNotEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_Equality<Microsoft.FSharp.Core.char> (e1,e2),False) @ (5,63--5,73)"
        [], "let testCharLessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.char> (e1,e2) @ (6,63--6,72)"
        [], "let testCharLessThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_GreaterThan<Microsoft.FSharp.Core.char> (e1,e2),False) @ (7,63--7,73)"
        [], "let testCharGreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.char> (e1,e2) @ (8,63--8,72)"
        [], "let testCharGreaterThanOrEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (Operators.op_LessThan<Microsoft.FSharp.Core.char> (e1,e2),False) @ (9,63--9,73)"
        [], "let testCharAdditionOperator(e1) (e2) = Operators.ToChar<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.char> (arg0_0),Operators.op_Addition<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.char> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.char> (arg0_0,arg1_0),e1,e2)) @ (11,55--11,64)"
        [], "let testCharAdditionChecked(e1) (e2) = Operators.ToChar<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.char> (arg0_0),Checked.op_Addition<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.char> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.char> (arg0_0,arg1_0),e1,e2)) @ (24,53--24,70)"
        [], "let testCharToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,43--29,58)"
        [], "let testCharToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,43--30,59)"
        [], "let testCharToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,43--31,59)"
        [], "let testCharToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,43--32,60)"
        [], "let testCharToIntChecked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (33,43--33,57)"
        [], "let testCharToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,43--34,59)"
        [], "let testCharToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,43--35,60)"
        [], "let testCharToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,43--36,59)"
        [], "let testCharToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,43--37,60)"
        [], "let testCharToIntPtrChecked(e1) = Checked.ToIntPtr<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.nativeint> (arg0_0),e1) @ (38,43--38,63)"
        [], "let testCharToUIntPtrChecked(e1) = Checked.ToUIntPtr<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (39,43--39,64)"
        [], "let testCharToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,43--41,50)"
        [], "let testCharToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,43--42,51)"
        [], "let testCharToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,43--43,51)"
        [], "let testCharToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,43--44,52)"
        [], "let testCharToIntOperator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (45,43--45,49)"
        [], "let testCharToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,43--46,51)"
        [], "let testCharToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,43--47,52)"
        [], "let testCharToInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (48,43--48,51)"
        [], "let testCharToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,43--49,52)"
        [], "let testCharToIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (50,43--50,55)"
        [], "let testCharToUIntPtrOperator(e1) = Operators.ToUIntPtr<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.unativeint> (arg0_0),e1) @ (51,43--51,56)"
        [], "let testCharToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float32> (arg0_0),Operators.ToDouble<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.float> (arg0_0),e1)) @ (52,43--52,53)"
        [], "let testCharToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.float> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.float,Microsoft.FSharp.Core.float> (arg0_0),Operators.ToDouble<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.float> (arg0_0),e1)) @ (53,43--53,51)"
        [], "let testCharToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.char> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.char,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,43--55,50)"
        [FC47; FC50], "let testCharToStringOperator(e1) = let mutable copyOfStruct: Microsoft.FSharp.Core.char = e1 in copyOfStruct.ToString() @ (56,43--56,52)"
      ]


    testOperators "Char" "char" excludedTests expectedUnoptimized expectedOptimized

[<Test>]
let ``Test Operator Declarations for String`` () =
    let excludedTests = [
        "testStringSubtractionOperator";
        "testStringMultiplyOperator";
        "testStringDivisionOperator";
        "testStringModulusOperator";
        "testStringBitwiseAndOperator";
        "testStringBitwiseOrOperator";
        "testStringBitwiseXorOperator";
        "testStringShiftLeftOperator";
        "testStringShiftRightOperator";
        "testStringUnaryNegOperator";
        "testStringSubtractionChecked";
        "testStringMultiplyChecked";
        "testStringUnaryNegChecked";
        "testStringToIntPtrChecked";
        "testStringToUIntPtrChecked";
        "testStringToIntPtrOperator";
        "testStringToUIntPtrOperator";
      ]

    let expectedUnoptimized = [
        [], "type OperatorTestsString"
        [], "let testStringEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.string> (e1,e2) @ (4,69--4,78)"
        [], "let testStringNotEqualsOperator(e1) (e2) = Operators.op_Inequality<Microsoft.FSharp.Core.string> (e1,e2) @ (5,69--5,79)"
        [], "let testStringLessThanOperator(e1) (e2) = Operators.op_LessThan<Microsoft.FSharp.Core.string> (e1,e2) @ (6,69--6,78)"
        [], "let testStringLessThanOrEqualsOperator(e1) (e2) = Operators.op_LessThanOrEqual<Microsoft.FSharp.Core.string> (e1,e2) @ (7,69--7,79)"
        [], "let testStringGreaterThanOperator(e1) (e2) = Operators.op_GreaterThan<Microsoft.FSharp.Core.string> (e1,e2) @ (8,69--8,78)"
        [], "let testStringGreaterThanOrEqualsOperator(e1) (e2) = Operators.op_GreaterThanOrEqual<Microsoft.FSharp.Core.string> (e1,e2) @ (9,69--9,79)"
        [], "let testStringAdditionOperator(e1) (e2) = Operators.op_Addition<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.string> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.string> (arg0_0,arg1_0),e1,e2) @ (11,61--11,70)"
        [], "let testStringAdditionChecked(e1) (e2) = Checked.op_Addition<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.string> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.string> (arg0_0,arg1_0),e1,e2) @ (24,59--24,76)"
        [], "let testStringToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.string> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (29,47--29,62)"
        [], "let testStringToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.string> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (30,47--30,63)"
        [], "let testStringToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.string> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (31,47--31,63)"
        [], "let testStringToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.string> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (32,47--32,64)"
        [], "let testStringToIntChecked(e1) = Checked.ToInt<Microsoft.FSharp.Core.string> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (33,47--33,61)"
        [], "let testStringToInt32Checked(e1) = Checked.ToInt32<Microsoft.FSharp.Core.string> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (34,47--34,63)"
        [], "let testStringToUInt32Checked(e1) = Checked.ToUInt32<Microsoft.FSharp.Core.string> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (35,47--35,64)"
        [], "let testStringToInt64Checked(e1) = Checked.ToInt64<Microsoft.FSharp.Core.string> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (36,47--36,63)"
        [], "let testStringToUInt64Checked(e1) = Checked.ToUInt64<Microsoft.FSharp.Core.string> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (37,47--37,64)"
        [], "let testStringToByteOperator(e1) = Operators.ToByte<Microsoft.FSharp.Core.string> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.byte> (arg0_0),e1) @ (41,47--41,54)"
        [], "let testStringToSByteOperator(e1) = Operators.ToSByte<Microsoft.FSharp.Core.string> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.sbyte> (arg0_0),e1) @ (42,47--42,55)"
        [], "let testStringToInt16Operator(e1) = Operators.ToInt16<Microsoft.FSharp.Core.string> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.int16> (arg0_0),e1) @ (43,47--43,55)"
        [], "let testStringToUInt16Operator(e1) = Operators.ToUInt16<Microsoft.FSharp.Core.string> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.uint16> (arg0_0),e1) @ (44,47--44,56)"
        [], "let testStringToIntOperator(e1) = Operators.ToInt<Microsoft.FSharp.Core.string> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.int> (arg0_0),e1) @ (45,47--45,53)"
        [], "let testStringToInt32Operator(e1) = Operators.ToInt32<Microsoft.FSharp.Core.string> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.int32> (arg0_0),e1) @ (46,47--46,55)"
        [], "let testStringToUInt32Operator(e1) = Operators.ToUInt32<Microsoft.FSharp.Core.string> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.uint32> (arg0_0),e1) @ (47,47--47,56)"
        [], "let testStringToInt64Operator(e1) = Operators.ToInt64<Microsoft.FSharp.Core.string> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.int64> (arg0_0),e1) @ (48,47--48,55)"
        [], "let testStringToUInt64Operator(e1) = Operators.ToUInt64<Microsoft.FSharp.Core.string> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.uint64> (arg0_0),e1) @ (49,47--49,56)"
        [], "let testStringToSingleOperator(e1) = Operators.ToSingle<Microsoft.FSharp.Core.string> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.float32> (arg0_0),e1) @ (52,47--52,57)"
        [], "let testStringToDoubleOperator(e1) = Operators.ToDouble<Microsoft.FSharp.Core.string> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.float> (arg0_0),e1) @ (53,47--53,55)"
        [], "let testStringToDecimalOperator(e1) = Operators.ToDecimal<Microsoft.FSharp.Core.string> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.decimal> (arg0_0),e1) @ (54,47--54,57)"
        [], "let testStringToCharOperator(e1) = Operators.ToChar<Microsoft.FSharp.Core.string> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.string,Microsoft.FSharp.Core.char> (arg0_0),e1) @ (55,47--55,54)"
        [], "let testStringToStringOperator(e1) = Operators.ToString<Microsoft.FSharp.Core.string> (e1) @ (56,47--56,56)"
      ]
    let expectedOptimized = [
        [], "type OperatorTestsString"
        [], "let testStringEqualsOperator(e1) (e2) = String.Equals (e1,e2) @ (4,69--4,78)"
        [], "let testStringNotEqualsOperator(e1) (e2) = Operators.op_Equality<Microsoft.FSharp.Core.bool> (String.Equals (e1,e2),False) @ (5,69--5,79)"
        [], "let testStringLessThanOperator(e1) (e2) = HashCompare.GenericLessThanIntrinsic<Microsoft.FSharp.Core.string> (e1,e2) @ (6,69--6,78)"
        [], "let testStringLessThanOrEqualsOperator(e1) (e2) = HashCompare.GenericLessOrEqualIntrinsic<Microsoft.FSharp.Core.string> (e1,e2) @ (7,69--7,79)"
        [], "let testStringGreaterThanOperator(e1) (e2) = HashCompare.GenericGreaterThanIntrinsic<Microsoft.FSharp.Core.string> (e1,e2) @ (8,69--8,78)"
        [], "let testStringGreaterThanOrEqualsOperator(e1) (e2) = HashCompare.GenericGreaterOrEqualIntrinsic<Microsoft.FSharp.Core.string> (e1,e2) @ (9,69--9,79)"
        [], "let testStringAdditionOperator(e1) (e2) = String.Concat (e1,e2) @ (11,61--11,70)"
        [], "let testStringAdditionChecked(e1) (e2) = String.Concat (e1,e2) @ (24,59--24,76)"
        [], "let testStringToByteChecked(e1) = Checked.ToByte<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.byte> (arg0_0),LanguagePrimitives.ParseUInt32 (e1)) @ (29,47--29,62)"
        [], "let testStringToSByteChecked(e1) = Checked.ToSByte<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.sbyte> (arg0_0),LanguagePrimitives.ParseInt32 (e1)) @ (30,47--30,63)"
        [], "let testStringToInt16Checked(e1) = Checked.ToInt16<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int16> (arg0_0),LanguagePrimitives.ParseInt32 (e1)) @ (31,47--31,63)"
        [], "let testStringToUInt16Checked(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint16> (arg0_0),LanguagePrimitives.ParseUInt32 (e1)) @ (32,47--32,64)"
        [], "let testStringToIntChecked(e1) = LanguagePrimitives.ParseInt32 (e1) @ (33,47--33,61)"
        [], "let testStringToInt32Checked(e1) = LanguagePrimitives.ParseInt32 (e1) @ (34,47--34,63)"
        [], "let testStringToUInt32Checked(e1) = LanguagePrimitives.ParseUInt32 (e1) @ (35,47--35,64)"
        [], "let testStringToInt64Checked(e1) = LanguagePrimitives.ParseInt64 (e1) @ (36,47--36,63)"
        [], "let testStringToUInt64Checked(e1) = LanguagePrimitives.ParseUInt64 (e1) @ (37,47--37,64)"
        [], "let testStringToByteOperator(e1) = Checked.ToByte<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.byte> (arg0_0),LanguagePrimitives.ParseUInt32 (e1)) @ (41,47--41,54)"
        [], "let testStringToSByteOperator(e1) = Checked.ToSByte<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.sbyte> (arg0_0),LanguagePrimitives.ParseInt32 (e1)) @ (42,47--42,55)"
        [], "let testStringToInt16Operator(e1) = Checked.ToInt16<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int16> (arg0_0),LanguagePrimitives.ParseInt32 (e1)) @ (43,47--43,55)"
        [], "let testStringToUInt16Operator(e1) = Checked.ToUInt16<Microsoft.FSharp.Core.uint32> (fun arg0_0 -> LanguagePrimitives.ExplicitDynamic<Microsoft.FSharp.Core.uint32,Microsoft.FSharp.Core.uint16> (arg0_0),LanguagePrimitives.ParseUInt32 (e1)) @ (44,47--44,56)"
        [], "let testStringToIntOperator(e1) = LanguagePrimitives.ParseInt32 (e1) @ (45,47--45,53)"
        [], "let testStringToInt32Operator(e1) = LanguagePrimitives.ParseInt32 (e1) @ (46,47--46,55)"
        [], "let testStringToUInt32Operator(e1) = LanguagePrimitives.ParseUInt32 (e1) @ (47,47--47,56)"
        [], "let testStringToInt64Operator(e1) = LanguagePrimitives.ParseInt64 (e1) @ (48,47--48,55)"
        [], "let testStringToUInt64Operator(e1) = LanguagePrimitives.ParseUInt64 (e1) @ (49,47--49,56)"
        [], "let testStringToSingleOperator(e1) = Single.Parse ((if Operators.op_Equality<Microsoft.FSharp.Core.string> (e1,dflt) then dflt else e1.Replace(\"_\",\"\")),167,CultureInfo.get_InvariantCulture () :> System.IFormatProvider) @ (52,47--52,57)"
        [], "let testStringToDoubleOperator(e1) = Double.Parse ((if Operators.op_Equality<Microsoft.FSharp.Core.string> (e1,dflt) then dflt else e1.Replace(\"_\",\"\")),167,CultureInfo.get_InvariantCulture () :> System.IFormatProvider) @ (53,47--53,55)"
        [], "let testStringToDecimalOperator(e1) = Decimal.Parse (e1,167,CultureInfo.get_InvariantCulture () :> System.IFormatProvider) @ (54,47--54,57)"
        [], "let testStringToCharOperator(e1) = Char.Parse (e1) @ (55,47--55,54)"
        [FC47; FC50], """let testStringToStringOperator(e1) = (if String.Equals (e1,dflt) then "" else e1) @ (56,47--56,56)"""
      ]

    testOperators "String" "string" excludedTests expectedUnoptimized expectedOptimized


//---------------------------------------------------------------------------------------------------------
// This big list expression was causing us trouble

module internal ProjectStressBigExpressions =

    let fileSource1 = """
module StressBigExpressions


let BigListExpression =

   [("C", "M.C", "file1", ((3, 5), (3, 6)), ["class"]);
    ("( .ctor )", "M.C.( .ctor )", "file1", ((3, 5), (3, 6)),["member"; "ctor"]);
    ("P", "M.C.P", "file1", ((4, 13), (4, 14)), ["member"; "getter"]);
    ("x", "x", "file1", ((4, 11), (4, 12)), []);
    ("( + )", "Microsoft.FSharp.Core.Operators.( + )", "file1",((6, 12), (6, 13)), ["val"]);
    ("xxx", "M.xxx", "file1", ((6, 4), (6, 7)), ["val"]);
    ("( + )", "Microsoft.FSharp.Core.Operators.( + )", "file1",((7, 17), (7, 18)), ["val"]);
    ("xxx", "M.xxx", "file1", ((7, 13), (7, 16)), ["val"]);
    ("xxx", "M.xxx", "file1", ((7, 19), (7, 22)), ["val"]);
    ("fff", "M.fff", "file1", ((7, 4), (7, 7)), ["val"]);
    ("C", "M.C", "file1", ((9, 15), (9, 16)), ["class"]);
    ("C", "M.C", "file1", ((9, 15), (9, 16)), ["class"]);
    ("C", "M.C", "file1", ((9, 15), (9, 16)), ["class"]);
    ("C", "M.C", "file1", ((9, 15), (9, 16)), ["class"]);
    ("CAbbrev", "M.CAbbrev", "file1", ((9, 5), (9, 12)), ["abbrev"]);
    ("M", "M", "file1", ((1, 7), (1, 8)), ["module"]);
    ("D1", "N.D1", "file2", ((5, 5), (5, 7)), ["class"]);
    ("( .ctor )", "N.D1.( .ctor )", "file2", ((5, 5), (5, 7)),["member"; "ctor"]);
    ("SomeProperty", "N.D1.SomeProperty", "file2", ((6, 13), (6, 25)),["member"; "getter"]);
    ("x", "x", "file2", ((6, 11), (6, 12)), []);
    ("M", "M", "file2", ((6, 28), (6, 29)), ["module"]);
    ("xxx", "M.xxx", "file2", ((6, 28), (6, 33)), ["val"]);
    ("D2", "N.D2", "file2", ((8, 5), (8, 7)), ["class"]);
    ("( .ctor )", "N.D2.( .ctor )", "file2", ((8, 5), (8, 7)),["member"; "ctor"]);
    ("SomeProperty", "N.D2.SomeProperty", "file2", ((9, 13), (9, 25)),["member"; "getter"]); ("x", "x", "file2", ((9, 11), (9, 12)), []);
    ("( + )", "Microsoft.FSharp.Core.Operators.( + )", "file2",((9, 36), (9, 37)), ["val"]);
    ("M", "M", "file2", ((9, 28), (9, 29)), ["module"]);
    ("fff", "M.fff", "file2", ((9, 28), (9, 33)), ["val"]);
    ("D1", "N.D1", "file2", ((9, 38), (9, 40)), ["member"; "ctor"]);
    ("M", "M", "file2", ((12, 27), (12, 28)), ["module"]);
    ("xxx", "M.xxx", "file2", ((12, 27), (12, 32)), ["val"]);
    ("y2", "N.y2", "file2", ((12, 4), (12, 6)), ["val"]);
    ("DefaultValueAttribute", "Microsoft.FSharp.Core.DefaultValueAttribute","file2", ((18, 6), (18, 18)), ["class"]);
    ("DefaultValueAttribute", "Microsoft.FSharp.Core.DefaultValueAttribute","file2", ((18, 6), (18, 18)), ["class"]);
    ("DefaultValueAttribute", "Microsoft.FSharp.Core.DefaultValueAttribute","file2", ((18, 6), (18, 18)), ["member"]);
    ("int", "Microsoft.FSharp.Core.int", "file2", ((19, 20), (19, 23)),["abbrev"]);
    ("DefaultValueAttribute", "Microsoft.FSharp.Core.DefaultValueAttribute","file2", ((18, 6), (18, 18)), ["class"]);
    ("DefaultValueAttribute", "Microsoft.FSharp.Core.DefaultValueAttribute","file2", ((18, 6), (18, 18)), ["class"]);
    ("DefaultValueAttribute", "Microsoft.FSharp.Core.DefaultValueAttribute","file2", ((18, 6), (18, 18)), ["member"]);
    ("x", "N.D3.x", "file2", ((19, 16), (19, 17)),["field"; "default"; "mutable"]);
    ("D3", "N.D3", "file2", ((15, 5), (15, 7)), ["class"]);
    ("int", "Microsoft.FSharp.Core.int", "file2", ((15, 10), (15, 13)),["abbrev"]); ("a", "a", "file2", ((15, 8), (15, 9)), []);
    ("( .ctor )", "N.D3.( .ctor )", "file2", ((15, 5), (15, 7)),["member"; "ctor"]);
    ("SomeProperty", "N.D3.SomeProperty", "file2", ((21, 13), (21, 25)),["member"; "getter"]);
    ("( + )", "Microsoft.FSharp.Core.Operators.( + )", "file2",((16, 14), (16, 15)), ["val"]);
    ("a", "a", "file2", ((16, 12), (16, 13)), []);
    ("b", "b", "file2", ((16, 8), (16, 9)), []);
    ("x", "x", "file2", ((21, 11), (21, 12)), []);
    ("( + )", "Microsoft.FSharp.Core.Operators.( + )", "file2",((21, 30), (21, 31)), ["val"]);
    ("a", "a", "file2", ((21, 28), (21, 29)), []);
    ("b", "b", "file2", ((21, 32), (21, 33)), []);
    ("( + )", "Microsoft.FSharp.Core.Operators.( + )", "file2",((23, 25), (23, 26)), ["val"]);
    ("( + )", "Microsoft.FSharp.Core.Operators.( + )", "file2",((23, 21), (23, 22)), ["val"]);
    ("int32", "Microsoft.FSharp.Core.Operators.int32", "file2",((23, 27), (23, 32)), ["val"]);
    ("DateTime", "System.DateTime", "file2", ((23, 40), (23, 48)),["valuetype"]);
    ("System", "System", "file2", ((23, 33), (23, 39)), ["namespace"]);
    ("Now", "System.DateTime.Now", "file2", ((23, 33), (23, 52)),["member"; "prop"]);
    ("Ticks", "System.DateTime.Ticks", "file2", ((23, 33), (23, 58)),["member"; "prop"]);
    ("( + )", "Microsoft.FSharp.Core.Operators.( + )", "file2",((23, 62), (23, 63)), ["val"]);
    ("pair2", "N.pair2", "file2", ((23, 10), (23, 15)), ["val"]);
    ("pair1", "N.pair1", "file2", ((23, 4), (23, 9)), ["val"]);
    ("None", "N.SaveOptions.None", "file2", ((27, 4), (27, 8)),["field"; "static"; "0"]);
    ("DisableFormatting", "N.SaveOptions.DisableFormatting", "file2",((28, 4), (28, 21)), ["field"; "static"; "1"]);
    ("SaveOptions", "N.SaveOptions", "file2", ((26, 5), (26, 16)),["enum"; "valuetype"]);
    ("SaveOptions", "N.SaveOptions", "file2", ((30, 16), (30, 27)),["enum"; "valuetype"]);
    ("DisableFormatting", "N.SaveOptions.DisableFormatting", "file2",((30, 16), (30, 45)), ["field"; "static"; "1"]);
    ("enumValue", "N.enumValue", "file2", ((30, 4), (30, 13)), ["val"]);
    ("x", "x", "file2", ((32, 9), (32, 10)), []);
    ("y", "y", "file2", ((32, 11), (32, 12)), []);
    ("( + )", "Microsoft.FSharp.Core.Operators.( + )", "file2",((32, 17), (32, 18)), ["val"]);
    ("x", "x", "file2", ((32, 15), (32, 16)), []);
    ("y", "y", "file2", ((32, 19), (32, 20)), []);
    ("( ++ )", "N.( ++ )", "file2", ((32, 5), (32, 7)), ["val"]);
    ("( ++ )", "N.( ++ )", "file2", ((34, 11), (34, 13)), ["val"]);
    ("c1", "N.c1", "file2", ((34, 4), (34, 6)), ["val"]);
    ("( ++ )", "N.( ++ )", "file2", ((36, 11), (36, 13)), ["val"]);
    ("c2", "N.c2", "file2", ((36, 4), (36, 6)), ["val"]);
    ("M", "M", "file2", ((38, 12), (38, 13)), ["module"]);
    ("C", "M.C", "file2", ((38, 12), (38, 15)), ["class"]);
    ("M", "M", "file2", ((38, 22), (38, 23)), ["module"]);
    ("C", "M.C", "file2", ((38, 22), (38, 25)), ["class"]);
    ("C", "M.C", "file2", ((38, 22), (38, 25)), ["member"; "ctor"]);
    ("mmmm1", "N.mmmm1", "file2", ((38, 4), (38, 9)), ["val"]);
    ("M", "M", "file2", ((39, 12), (39, 13)), ["module"]);
    ("CAbbrev", "M.CAbbrev", "file2", ((39, 12), (39, 21)), ["abbrev"]);
    ("M", "M", "file2", ((39, 28), (39, 29)), ["module"]);
    ("CAbbrev", "M.CAbbrev", "file2", ((39, 28), (39, 37)), ["abbrev"]);
    ("C", "M.C", "file2", ((39, 28), (39, 37)), ["member"; "ctor"]);
    ("mmmm2", "N.mmmm2", "file2", ((39, 4), (39, 9)), ["val"]);
    ("N", "N", "file2", ((1, 7), (1, 8)), ["module"])]

let BigSequenceExpression(outFileOpt,docFileOpt,baseAddressOpt) =
        [   yield "--simpleresolution"
            yield "--noframework"
            match outFileOpt with
            | None -> ()
            | Some outFile -> yield "--out:" + outFile
            match docFileOpt with
            | None -> ()
            | Some docFile -> yield "--doc:" + docFile
            match baseAddressOpt with
            | None -> ()
            | Some baseAddress -> yield "--baseaddress:" + baseAddress
            match baseAddressOpt with
            | None -> ()
            | Some keyFile -> yield "--keyfile:" + keyFile
            match baseAddressOpt with
            | None -> ()
            | Some sigFile -> yield "--sig:" + sigFile
            match baseAddressOpt with
            | None -> ()
            | Some pdbFile -> yield "--pdb:" + pdbFile
            match baseAddressOpt with
            | None -> ()
            | Some versionFile -> yield "--versionfile:" + versionFile
            match baseAddressOpt with
            | None -> ()
            | Some warnLevel -> yield "--warn:" + warnLevel
            match baseAddressOpt with
            | None -> ()
            | Some s -> yield "--subsystemversion:" + s
            if true then yield "--highentropyva+"
            match baseAddressOpt with
            | None -> ()
            | Some win32Res -> yield "--win32res:" + win32Res
            match baseAddressOpt with
            | None -> ()
            | Some win32Manifest -> yield "--win32manifest:" + win32Manifest
            match baseAddressOpt with
            | None -> ()
            | Some targetProfile -> yield "--targetprofile:" + targetProfile
            yield "--fullpaths"
            yield "--flaterrors"
            if true then yield "--warnaserror"
            yield
                if true then "--target:library"
                else "--target:exe"
            for symbol in [] do
                if not (System.String.IsNullOrWhiteSpace symbol) then yield "--define:" + symbol
            for nw in [] do
                if not (System.String.IsNullOrWhiteSpace nw) then yield "--nowarn:" + nw
            for nw in [] do
                if not (System.String.IsNullOrWhiteSpace nw) then yield "--warnaserror:" + nw
            yield if true then "--debug+"
                    else "--debug-"
            yield if true then "--optimize+"
                    else "--optimize-"
            yield if true then "--tailcalls+"
                    else "--tailcalls-"
            match baseAddressOpt with
            | None -> ()
            | Some debugType ->
                match "" with
                | "NONE" -> ()
                | "PDBONLY" -> yield "--debug:pdbonly"
                | "FULL" -> yield "--debug:full"
                | _ -> ()
            match baseAddressOpt |> Option.map (fun o -> ""), true, baseAddressOpt |> Option.map (fun o -> "") with
            | Some "ANYCPU", true, Some "EXE" | Some "ANYCPU", true, Some "WINEXE" -> yield "--platform:anycpu32bitpreferred"
            | Some "ANYCPU", _, _ -> yield "--platform:anycpu"
            | Some "X86", _, _ -> yield "--platform:x86"
            | Some "X64", _, _ -> yield "--platform:x64"
            | Some "ITANIUM", _, _ -> yield "--platform:Itanium"
            | _ -> ()
            match baseAddressOpt |> Option.map (fun o -> "") with
            | Some "LIBRARY" -> yield "--target:library"
            | Some "EXE" -> yield "--target:exe"
            | Some "WINEXE" -> yield "--target:winexe"
            | Some "MODULE" -> yield "--target:module"
            | _ -> ()
            yield! []
            for f in [] do
                yield "--resource:" + f
            for i in [] do
                yield "--lib:"
            for r in []  do
                yield "-r:" + r
            yield! [] ]


    """


    let createOptions() = createOptionsAux [fileSource1] []


[<Test>]
let ``Test expressions of declarations stress big expressions`` () =
    let cleanup, options = ProjectStressBigExpressions.createOptions()
    use _holder = cleanup
    let exprChecker = FSharpChecker.Create(keepAssemblyContents=true)
    let wholeProjectResults = exprChecker.ParseAndCheckProject(options) |> Async.RunSynchronously

    wholeProjectResults.Diagnostics.Length |> shouldEqual 0

    wholeProjectResults.AssemblyContents.ImplementationFiles.Length |> shouldEqual 1
    let file1 = wholeProjectResults.AssemblyContents.ImplementationFiles.[0]

    // This should not stack overflow
    printDeclarations None (List.ofSeq file1.Declarations) |> Seq.toList |> ignore


[<Test>]
let ``Test expressions of optimized declarations stress big expressions`` () =
    let cleanup, options = ProjectStressBigExpressions.createOptions()
    use _holder = cleanup
    let exprChecker = FSharpChecker.Create(keepAssemblyContents=true)
    let wholeProjectResults = exprChecker.ParseAndCheckProject(options) |> Async.RunSynchronously

    wholeProjectResults.Diagnostics.Length |> shouldEqual 0

    wholeProjectResults.GetOptimizedAssemblyContents().ImplementationFiles.Length |> shouldEqual 1
    let file1 = wholeProjectResults.GetOptimizedAssemblyContents().ImplementationFiles.[0]

    // This should not stack overflow
    printDeclarations None (List.ofSeq file1.Declarations) |> Seq.toList |> ignore

//---------------------------------------------------------------------------------------------------------
// This project is for witness arguments (CallWithWitnesses)

module internal ProjectForWitnesses1 =

    let fileSource1 = """
module M

/// One witness
let inline callX (x: ^T) (y: ^U) = ((^T or ^U): (static member X : ^T * ^U -> ^V) (x,y))

/// Two witnesses
let inline callXY (x: ^T) (y: ^U) =
    ((^T or ^U): (static member Y1 : ^T * ^U -> unit) (x,y))
    ((^T or ^U): (static member Y2 : ^T * ^U -> unit) (x,y))

type C() =
    static member X(a: C, b: C) = C()
    static member X(a: C, b: D) = D()
    static member Y1(a: C, b: C) = ()
    static member Y1(a: C, b: D) = ()
    static member Y2(a: C, b: C) = ()
    static member Y2(a: C, b: D) = ()

and D() =
    static member X(a: D, b: D) = D()
    static member X(a: D, b: C) = C()
    static member Y1(a: D, b: D) = ()
    static member Y1(a: D, b: C) = ()
    static member Y2(a: D, b: D) = ()
    static member Y2(a: D, b: C) = ()

let f1() = callX (C()) (C())
let f2() = callX (D()) (D())
let f3() = callX (C()) (D())
let f4() = callX (D()) (C())

let f5() = callXY (C()) (C())
let f6() = callXY (D()) (D())
let f7() = callXY (C()) (D())
let f8() = callXY (D()) (C())
    """

    let createOptions() = createOptionsAux [fileSource1] ["--langversion:preview"]

[<Test>]
let ``Test ProjectForWitnesses1`` () =
    let cleanup, options = ProjectForWitnesses1.createOptions()
    use _holder = cleanup
    let exprChecker = FSharpChecker.Create(keepAssemblyContents=true)
    let wholeProjectResults = exprChecker.ParseAndCheckProject(options) |> Async.RunSynchronously

    for e in wholeProjectResults.Diagnostics do
        printfn "Project1 error: <<<%s>>>" e.Message

    wholeProjectResults.AssemblyContents.ImplementationFiles.Length |> shouldEqual 1
    let file1 = wholeProjectResults.AssemblyContents.ImplementationFiles.[0]

    let expected =
        ["type M"; "let callX(x) (y) = trait call X(x,y) @ (5,35--5,88)";
         "let callXY(x) (y) = (trait call Y1(x,y); trait call Y2(x,y)) @ (9,4--10,60)";
         "type C"; "type D";
         "member .ctor(unitVar0) = (new Object(); ()) @ (12,5--12,6)";
         "member X(a,b) = new C(()) @ (13,34--13,37)";
         "member X(a,b) = new D(()) @ (14,34--14,37)";
         "member Y1(a,b) = () @ (15,35--15,37)"; "member Y1(a,b) = () @ (16,35--16,37)";
         "member Y2(a,b) = () @ (17,35--17,37)"; "member Y2(a,b) = () @ (18,35--18,37)";
         "member .ctor(unitVar0) = (new Object(); ()) @ (20,4--20,5)";
         "member X(a,b) = new D(()) @ (21,34--21,37)";
         "member X(a,b) = new C(()) @ (22,34--22,37)";
         "member Y1(a,b) = () @ (23,35--23,37)"; "member Y1(a,b) = () @ (24,35--24,37)";
         "member Y2(a,b) = () @ (25,35--25,37)"; "member Y2(a,b) = () @ (26,35--26,37)";
         "let f1(unitVar0) = M.callX<M.C,M.C,M.C> (fun arg0_0 -> fun arg1_0 -> C.X (arg0_0,arg1_0),new C(()),new C(())) @ (28,11--28,28)";
         "let f2(unitVar0) = M.callX<M.D,M.D,M.D> (fun arg0_0 -> fun arg1_0 -> D.X (arg0_0,arg1_0),new D(()),new D(())) @ (29,11--29,28)";
         "let f3(unitVar0) = M.callX<M.C,M.D,M.D> (fun arg0_0 -> fun arg1_0 -> C.X (arg0_0,arg1_0),new C(()),new D(())) @ (30,11--30,28)";
         "let f4(unitVar0) = M.callX<M.D,M.C,M.C> (fun arg0_0 -> fun arg1_0 -> D.X (arg0_0,arg1_0),new D(()),new C(())) @ (31,11--31,28)";
         "let f5(unitVar0) = M.callXY<M.C,M.C> (fun arg0_0 -> fun arg1_0 -> C.Y1 (arg0_0,arg1_0),fun arg0_0 -> fun arg1_0 -> C.Y2 (arg0_0,arg1_0),new C(()),new C(())) @ (33,11--33,29)";
         "let f6(unitVar0) = M.callXY<M.D,M.D> (fun arg0_0 -> fun arg1_0 -> D.Y1 (arg0_0,arg1_0),fun arg0_0 -> fun arg1_0 -> D.Y2 (arg0_0,arg1_0),new D(()),new D(())) @ (34,11--34,29)";
         "let f7(unitVar0) = M.callXY<M.C,M.D> (fun arg0_0 -> fun arg1_0 -> C.Y1 (arg0_0,arg1_0),fun arg0_0 -> fun arg1_0 -> C.Y2 (arg0_0,arg1_0),new C(()),new D(())) @ (35,11--35,29)";
         "let f8(unitVar0) = M.callXY<M.D,M.C> (fun arg0_0 -> fun arg1_0 -> D.Y1 (arg0_0,arg1_0),fun arg0_0 -> fun arg1_0 -> D.Y2 (arg0_0,arg1_0),new D(()),new C(())) @ (36,11--36,29)"]

    let actual =
      printDeclarations None (List.ofSeq file1.Declarations)
      |> Seq.toList
    printfn "actual:\n\n%A" actual
    actual
      |> shouldPairwiseEqual expected


[<Test>]
let ``Test ProjectForWitnesses1 GetWitnessPassingInfo`` () =
    let cleanup, options = ProjectForWitnesses1.createOptions()
    use _holder = cleanup
    let exprChecker = FSharpChecker.Create(keepAssemblyContents=true)
    let wholeProjectResults = exprChecker.ParseAndCheckProject(options) |> Async.RunSynchronously

    for e in wholeProjectResults.Diagnostics do
        printfn "ProjectForWitnesses1 error: <<<%s>>>" e.Message

    begin
        let symbol =
            wholeProjectResults.GetAllUsesOfAllSymbols()
            |> Array.tryFind (fun su -> su.Symbol.DisplayName = "callX")
            |> Option.orElseWith (fun _ -> failwith "Could not get symbol")
            |> Option.map (fun su -> su.Symbol :?> FSharpMemberOrFunctionOrValue)
            |> Option.get
        printfn "symbol = %s" symbol.FullName
        let wpi = (symbol.GetWitnessPassingInfo())
        match wpi with
        | None -> failwith "witness passing info expected"
        | Some (nm, argTypes) ->
            nm |> shouldEqual "callX$W"
            argTypes.Count |> shouldEqual 1
            let argText = argTypes.[0].Type.ToString()
            argText |> shouldEqual "type  ^T ->  ^U ->  ^V"
    end


    begin
        let symbol =
            wholeProjectResults.GetAllUsesOfAllSymbols()
            |> Array.tryFind (fun su -> su.Symbol.DisplayName = "callXY")
            |> Option.orElseWith (fun _ -> failwith "Could not get symbol")
            |> Option.map (fun su -> su.Symbol :?> FSharpMemberOrFunctionOrValue)
            |> Option.get
        printfn "symbol = %s" symbol.FullName
        let wpi = (symbol.GetWitnessPassingInfo())
        match wpi with
        | None -> failwith "witness passing info expected"
        | Some (nm, argTypes) ->
            nm |> shouldEqual "callXY$W"
            argTypes.Count |> shouldEqual 2
            let argName1 = argTypes.[0].Name
            let argText1 = argTypes.[0].Type.ToString()
            let argName2 = argTypes.[1].Name
            let argText2 = argTypes.[1].Type.ToString()
            argText1 |> shouldEqual "type  ^T ->  ^U -> Microsoft.FSharp.Core.unit"
            argText2 |> shouldEqual "type  ^T ->  ^U -> Microsoft.FSharp.Core.unit"
    end


//---------------------------------------------------------------------------------------------------------
// This project is for witness arguments (CallWithWitnesses)

module internal ProjectForWitnesses2 =

    let fileSource1 = """
module M

type Point =
    { x: int; y: int }
    static member Zero = { x=0; y=0 }
    static member Neg(p: Point) = { x = -p.x; y = -p.y }
    static member (+) (p1, p2) = { x= p1.x + p2.x; y = p1.y + p2.y }

type MyNumber =
    | MyNumber of int
    static member Zero = MyNumber 0
    static member (+) (MyNumber x, MyNumber y) =
        MyNumber(x + y)
    static member DivideByInt (MyNumber x, i: int) =
        MyNumber(x / i)

type MyNumberWrapper =
    { MyNumber: MyNumber }
    """

    let createOptions() = createOptionsAux [fileSource1] ["--langversion:preview"]

[<Test>]
let ``Test ProjectForWitnesses2`` () =
    let cleanup, options = ProjectForWitnesses2.createOptions()
    use _holder = cleanup
    let exprChecker = FSharpChecker.Create(keepAssemblyContents=true)
    let wholeProjectResults = exprChecker.ParseAndCheckProject(options) |> Async.RunSynchronously

    for e in wholeProjectResults.Diagnostics do
        printfn "ProjectForWitnesses2 error: <<<%s>>>" e.Message

    wholeProjectResults.Diagnostics.Length |> shouldEqual 0
    wholeProjectResults.AssemblyContents.ImplementationFiles.Length |> shouldEqual 1
    let file1 = wholeProjectResults.AssemblyContents.ImplementationFiles.[0]

    let expected =
        ["type M"; "type Point";
         "member get_Zero(unitVar0) = {x = 0; y = 0} @ (6,25--6,37)";
         "member Neg(p) = {x = Operators.op_UnaryNegation<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0),p.x); y = Operators.op_UnaryNegation<Microsoft.FSharp.Core.int> (fun arg0_0 -> LanguagePrimitives.UnaryNegationDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0),p.y)} @ (7,34--7,56)";
         "member op_Addition(p1,p2) = {x = Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),p1.x,p2.x); y = Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),p1.y,p2.y)} @ (8,33--8,68)";
         "type MyNumber"; "member get_Zero(unitVar0) = MyNumber(0) @ (12,25--12,35)";
         "member op_Addition(_arg1,_arg2) = let x: Microsoft.FSharp.Core.int = _arg1.Item in let y: Microsoft.FSharp.Core.int = _arg2.Item in MyNumber(Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),x,y)) @ (13,23--13,33)";
         "member DivideByInt(_arg3,i) = let x: Microsoft.FSharp.Core.int = _arg3.Item in MyNumber(Operators.op_Division<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.DivisionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),x,i)) @ (15,31--15,41)";
         "type MyNumberWrapper"]

    let actual =
      printDeclarations None (List.ofSeq file1.Declarations)
      |> Seq.toList
    printfn "actual:\n\n%A" actual
    actual
      |> shouldPairwiseEqual expected

//---------------------------------------------------------------------------------------------------------
// This project is for witness arguments, testing for https://github.com/dotnet/fsharp/issues/10364

module internal ProjectForWitnesses3 =

    let fileSource1 = """
module M

type Point =
    { x: int; y: int }
    static member Zero = { x=0; y=0 }
    member p.Sign = sign p.x

    static member (+) (p1, p2) = { x= p1.x + p2.x; y = p1.y + p2.y }

let p1 = {x=1; y=10}
let p2 = {x=2; y=20}
let s = List.sum [p1; p2]
let s2 = sign p1

    """

    let createOptions() = createOptionsAux [fileSource1] ["--langversion:preview"]

[<Test>]
let ``Test ProjectForWitnesses3`` () =
    let cleanup, options = createOptionsAux [ ProjectForWitnesses3.fileSource1 ] ["--langversion:preview"]
    use _holder = cleanup
    let exprChecker = FSharpChecker.Create(keepAssemblyContents=true)
    let wholeProjectResults = exprChecker.ParseAndCheckProject(options) |> Async.RunSynchronously

    for e in wholeProjectResults.Diagnostics do
        printfn "ProjectForWitnesses3 error: <<<%s>>>" e.Message

    wholeProjectResults.Diagnostics.Length |> shouldEqual 0
    wholeProjectResults.AssemblyContents.ImplementationFiles.Length |> shouldEqual 1
    let file1 = wholeProjectResults.AssemblyContents.ImplementationFiles.[0]

    let expected =
        ["type M"; "type Point";
         "member get_Zero(unitVar0) = {x = 0; y = 0} @ (6,25--6,37)";
         "member get_Sign(p) (unitVar1) = Operators.Sign<Microsoft.FSharp.Core.int> (fun arg0_0 -> Operators.Sign<Microsoft.FSharp.Core.int> (arg0_0),p.x) @ (7,20--7,28)";
         "member op_Addition(p1,p2) = {x = Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),p1.x,p2.x); y = Operators.op_Addition<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (fun arg0_0 -> fun arg1_0 -> LanguagePrimitives.AdditionDynamic<Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int,Microsoft.FSharp.Core.int> (arg0_0,arg1_0),p1.y,p2.y)} @ (9,33--9,68)";
         "let p1 = {x = 1; y = 10} @ (11,9--11,20)";
         "let p2 = {x = 2; y = 20} @ (12,9--12,20)";
         "let s = ListModule.Sum<M.Point> (fun arg0_0 -> Point.get_Zero (()),fun arg0_0 -> fun arg1_0 -> Point.op_Addition (arg0_0,arg1_0),Cons(M.p1 (),Cons(M.p2 (),Empty()))) @ (13,8--13,25)";
         "let s2 = Operators.Sign<M.Point> (fun arg0_0 -> arg0_0.get_Sign(()),M.p1 ()) @ (14,9--14,16)"]

    let actual =
      printDeclarations None (List.ofSeq file1.Declarations)
      |> Seq.toList
    printfn "actual:\n\n%A" actual
    actual
      |> shouldPairwiseEqual expected

[<Test>]
let ``Test ProjectForWitnesses3 GetWitnessPassingInfo`` () =
    let cleanup, options = ProjectForWitnesses3.createOptions()
    use _holder = cleanup
    let exprChecker = FSharpChecker.Create(keepAssemblyContents=true)
    let wholeProjectResults = exprChecker.ParseAndCheckProject(options) |> Async.RunSynchronously

    for e in wholeProjectResults.Diagnostics do
        printfn "ProjectForWitnesses3 error: <<<%s>>>" e.Message

    begin
        let symbol =
            wholeProjectResults.GetAllUsesOfAllSymbols()
            |> Array.tryFind (fun su -> su.Symbol.DisplayName = "sum")
            |> Option.orElseWith (fun _ -> failwith "Could not get symbol")
            |> Option.map (fun su -> su.Symbol :?> FSharpMemberOrFunctionOrValue)
            |> Option.get
        printfn "symbol = %s" symbol.FullName
        let wpi = (symbol.GetWitnessPassingInfo())
        match wpi with
        | None -> failwith "witness passing info expected"
        | Some (nm, argTypes) ->
            nm |> shouldEqual "Sum$W"
            argTypes.Count |> shouldEqual 2
            let argName1 = argTypes.[0].Name
            let argText1 = argTypes.[0].Type.ToString()
            let argName2 = argTypes.[1].Name
            let argText2 = argTypes.[1].Type.ToString()
            argName1 |> shouldEqual (Some "get_Zero")
            argText1 |> shouldEqual "type Microsoft.FSharp.Core.unit ->  ^T"
            argName2 |> shouldEqual (Some "op_Addition")
            argText2 |> shouldEqual "type  ^T ->  ^T ->  ^T"
    end

//---------------------------------------------------------------------------------------------------------
// This project is for witness arguments, testing for https://github.com/dotnet/fsharp/issues/10364

module internal ProjectForWitnesses4 =

    let fileSource1 = """
module M

let isEmptyArray x =
    match x with
    | [| |] -> x
    | _ -> x

let isNull (ts : 't[]) =
    match ts with
    | null -> true
    | _ -> false

let isNullQuoted (ts : 't[]) =
    <@
        match ts with
        | null -> true
        | _ -> false
    @>

"""

    let createOptions() = createOptionsAux [fileSource1] ["--langversion:preview"]

[<Test>]
let ``Test ProjectForWitnesses4 GetWitnessPassingInfo`` () =
    let cleanup, options = ProjectForWitnesses4.createOptions()
    use _holder = cleanup
    let exprChecker = FSharpChecker.Create(keepAssemblyContents=true)
    let wholeProjectResults = exprChecker.ParseAndCheckProject(options) |> Async.RunSynchronously

    for e in wholeProjectResults.Diagnostics do
        printfn "ProjectForWitnesses4 error: <<<%s>>>" e.Message

    Assert.AreEqual(wholeProjectResults.Diagnostics.Length, 0)

    wholeProjectResults.AssemblyContents.ImplementationFiles.Length |> shouldEqual 1
    let file1 = wholeProjectResults.AssemblyContents.ImplementationFiles.[0]

    let expected =
        ["type M";
         "let isEmptyArray(x) = (if (if Operators.op_Inequality<'a Microsoft.FSharp.Core.[]> (x,dflt) then Operators.op_Equality<Microsoft.FSharp.Core.int> (ArrayModule.Length<'a> (x),0) else False) then x else x) @ (5,10--5,11)";
         "let isNull(ts) = (if Operators.op_Equality<'t Microsoft.FSharp.Core.[]> (ts,dflt) then True else False) @ (10,10--10,12)";
         "let isNullQuoted(ts) = quote((if Operators.op_Equality<'t Microsoft.FSharp.Core.[]> (ts,dflt) then True else False)) @ (15,4--19,6)"]

    let actual =
      printDeclarations None (List.ofSeq file1.Declarations)
      |> Seq.toList
    printfn "actual:\n\n%A" actual
    actual
      |> shouldPairwiseEqual expected
