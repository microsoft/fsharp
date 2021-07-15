// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.EditorServices

open System
open System.Diagnostics
open Internal.Utilities.Library 
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpSymbolPatterns
open FSharp.Compiler.Syntax
open FSharp.Compiler.SyntaxTreeOps
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Compiler.Tokenization

[<AutoOpen>]
module internal CodeGenerationUtils =
    open System.IO
    open System.CodeDom.Compiler

    type ColumnIndentedTextWriter() =
        let stringWriter = new StringWriter()
        let indentWriter = new IndentedTextWriter(stringWriter, " ")

        member _.Write(s: string) =
            indentWriter.Write("{0}", s)

        member _.Write(s: string, [<ParamArray>] objs: obj []) =
            indentWriter.Write(s, objs)

        member _.WriteLine(s: string) =
            indentWriter.WriteLine("{0}", s)

        member _.WriteLine(s: string, [<ParamArray>] objs: obj []) =
            indentWriter.WriteLine(s, objs)

        member x.WriteBlankLines count =
            for _ in 0 .. count - 1 do
                x.WriteLine ""

        member _.Indent i = 
            indentWriter.Indent <- indentWriter.Indent + i

        member _.Unindent i = 
            indentWriter.Indent <- max 0 (indentWriter.Indent - i)

        member _.Dump() =
            indentWriter.InnerWriter.ToString()

        interface IDisposable with
            member _.Dispose() =
                stringWriter.Dispose()
                indentWriter.Dispose()

    let (|IndexerArg|) = function
        | SynIndexerArg.Two(e1, _, e2, _, _, _) -> [e1; e2]
        | SynIndexerArg.One (e, _, _) -> [e]

    let (|IndexerArgList|) xs =
        List.collect (|IndexerArg|) xs
        
    /// An recursive pattern that collect all sequential expressions to avoid StackOverflowException
    let rec (|Sequentials|_|) = function
        | SynExpr.Sequential (_, _, e, Sequentials es, _) ->
            Some(e :: es)
        | SynExpr.Sequential (_, _, e1, e2, _) ->
            Some [e1; e2]
        | _ -> None

    /// Represent environment where a captured identifier should be renamed
    type NamesWithIndices = Map<string, Set<int>>

    let keywordSet = set FSharpKeywords.KeywordNames

    /// Rename a given argument if the identifier has been used
    let normalizeArgName (namesWithIndices: NamesWithIndices) nm =
        match nm with
        | "()" -> nm, namesWithIndices
        | _ ->
            let nm = String.lowerCaseFirstChar nm
            let nm, index = String.extractTrailingIndex nm
                
            let index, namesWithIndices =
                match namesWithIndices |> Map.tryFind nm, index with
                | Some indexes, index ->
                    let rec getAvailableIndex idx =
                        if indexes |> Set.contains idx then 
                            getAvailableIndex (idx + 1)
                        else idx
                    let index = index |> Option.defaultValue 1 |> getAvailableIndex
                    Some index, namesWithIndices |> Map.add nm (indexes |> Set.add index)
                | None, Some index -> Some index, namesWithIndices |> Map.add nm (Set.ofList [index])
                | None, None -> None, namesWithIndices |> Map.add nm Set.empty

            let nm = 
                match index with
                | Some index -> sprintf "%s%d" nm index
                | None -> nm
                
            let nm = if Set.contains nm keywordSet then sprintf "``%s``" nm else nm
            nm, namesWithIndices

/// Capture information about an interface in ASTs
[<RequireQualifiedAccess; NoEquality; NoComparison>]
type InterfaceData =
    | Interface of interfaceType: SynType * memberDefns: SynMemberDefns option
    | ObjExpr of objType: SynType * bindings: SynBinding list

    member x.Range =
        match x with
        | InterfaceData.Interface(ty, _) -> 
            ty.Range
        | InterfaceData.ObjExpr(ty, _) -> 
            ty.Range

    member x.TypeParameters = 
        match x with
        | InterfaceData.Interface(StripParenTypes ty, _)
        | InterfaceData.ObjExpr(StripParenTypes ty, _) ->
            let rec (|RationalConst|) = function
                | SynRationalConst.Integer i ->
                    string i
                | SynRationalConst.Rational(numerator, denominator, _) ->
                    sprintf "(%i/%i)" numerator denominator
                | SynRationalConst.Negate (RationalConst s) ->
                    sprintf "- %s" s

            let rec (|TypeIdent|_|) = function
                | SynType.Var(SynTypar(s, req, _), _) ->
                    match req with
                    | TyparStaticReq.None -> 
                        Some ("'" + s.idText)
                    | TyparStaticReq.HeadType -> 
                        Some ("^" + s.idText)
                | SynType.LongIdent(LongIdentWithDots(xs, _)) ->
                    xs |> Seq.map (fun x -> x.idText) |> String.concat "." |> Some
                | SynType.App(t, _, ts, _, _, isPostfix, _) ->
                    match t, ts with
                    | TypeIdent typeName, [] -> Some typeName
                    | TypeIdent typeName, [TypeIdent typeArg] -> 
                        if isPostfix then 
                            Some (sprintf "%s %s" typeArg typeName)
                        else
                            Some (sprintf "%s<%s>" typeName typeArg)
                    | TypeIdent typeName, _ -> 
                        let typeArgs = ts |> Seq.choose (|TypeIdent|_|) |> String.concat ", "
                        if isPostfix then 
                            Some (sprintf "(%s) %s" typeArgs typeName)
                        else
                            Some(sprintf "%s<%s>" typeName typeArgs)
                    | _ ->
                        //debug "Unsupported case with %A and %A" t ts
                        None
                | SynType.Anon _ -> 
                    Some "_"
                | SynType.AnonRecd (_, ts, _)  -> 
                    Some (ts |> Seq.choose (snd >> (|TypeIdent|_|)) |> String.concat "; ")
                | SynType.Array(dimension, TypeIdent typeName, _) ->
                    Some (sprintf "%s [%s]" typeName (new String(',', dimension-1)))
                | SynType.MeasurePower(TypeIdent typeName, RationalConst power, _) ->
                    Some (sprintf "%s^%s" typeName power)
                | SynType.MeasureDivide(TypeIdent numerator, TypeIdent denominator, _) ->
                    Some (sprintf "%s/%s" numerator denominator)
                | SynType.Paren(TypeIdent typeName, _) ->
                    Some typeName
                | _ -> 
                    None
            match ty with
            | SynType.App(_, _, ts, _, _, _, _)
            | SynType.LongIdentApp(_, _, _, ts, _, _, _) ->
                ts |> Seq.choose (|TypeIdent|_|) |> Seq.toArray
            | _ ->
                [||]

module InterfaceStubGenerator =
    [<NoComparison>]
    type internal Context =
        {
            Writer: ColumnIndentedTextWriter

            /// Map generic types to specific instances for specialized interface implementation
            TypeInstantations: Map<string, string>

            /// Data for interface instantiation
            ArgInstantiations: (FSharpGenericParameter * FSharpType) seq

            /// Indentation inside method bodies
            Indentation: int

            /// Object identifier of the interface e.g. 'x', 'this', '__', etc.
            ObjectIdent: string

            /// A list of lines represents skeleton of each member
            MethodBody: string []

            /// Context in order to display types in the short form
            DisplayContext: FSharpDisplayContext

        }

    // Adapt from MetadataFormat module in FSharp.Formatting 

    let internal (|AllAndLast|_|) (xs: 'T list) = 
        match xs with
        | [] -> 
            None
        | _ -> 
            let revd = List.rev xs
            Some (List.rev revd.Tail, revd.Head)

    let internal getTypeParameterName (typar: FSharpGenericParameter) =
        (if typar.IsSolveAtCompileTime then "^" else "'") + typar.Name

    let internal bracket (str: string) = 
        if str.Contains(" ") then "(" + str + ")" else str

    let internal formatType ctx (ty: FSharpType) =
        let genericDefinition = ty.Instantiate(Seq.toList ctx.ArgInstantiations).Format(ctx.DisplayContext)
        (genericDefinition, ctx.TypeInstantations)
        ||> Map.fold (fun s k v -> s.Replace(k, v))

    // Format each argument, including its name and type 
    let internal formatArgUsage ctx hasTypeAnnotation (namesWithIndices: Map<string, Set<int>>) (arg: FSharpParameter) = 
        let nm = 
            match arg.Name with 
            | None ->
                if arg.Type.HasTypeDefinition && arg.Type.TypeDefinition.CompiledName = "unit" && arg.Type.TypeDefinition.Namespace = Some "Microsoft.FSharp.Core" then "()" 
                else sprintf "arg%d" (namesWithIndices |> Map.toSeq |> Seq.map snd |> Seq.sumBy Set.count |> max 1)
            | Some x -> x
        
        let nm, namesWithIndices = normalizeArgName namesWithIndices nm
        
        // Detect an optional argument
        let isOptionalArg = arg.HasAttribute<OptionalArgumentAttribute>()
        let argName = if isOptionalArg then "?" + nm else nm
        (if hasTypeAnnotation && argName <> "()" then 
            argName + ": " + formatType ctx arg.Type
        else argName),
        namesWithIndices

    let internal formatArgsUsage ctx hasTypeAnnotation (v: FSharpMemberOrFunctionOrValue) args =
        let isItemIndexer = (v.IsInstanceMember && v.DisplayName = "Item")
        let unit, argSep, tupSep = "()", " ", ", "
        let args, namesWithIndices =
            args
            |> List.fold (fun (argsSoFar: string list list, namesWithIndices) args ->
                let argsSoFar', namesWithIndices =
                    args 
                    |> List.fold (fun (acc: string list, allNames) arg -> 
                        let name, allNames = formatArgUsage ctx hasTypeAnnotation allNames arg
                        name :: acc, allNames) ([], namesWithIndices)
                List.rev argsSoFar' :: argsSoFar, namesWithIndices) 
                ([], Map.ofList [ ctx.ObjectIdent, Set.empty ])
        let argText =
            args
            |> List.rev
            |> List.map (function 
                | [] -> unit 
                | [arg] when arg = unit -> unit
                | [arg] when not v.IsMember || isItemIndexer -> arg 
                | args when isItemIndexer -> String.concat tupSep args
                | args -> bracket (String.concat tupSep args))
            |> String.concat argSep

        argText, namesWithIndices

    [<RequireQualifiedAccess; NoComparison>]
    type internal MemberInfo =
        | PropertyGetSet of FSharpMemberOrFunctionOrValue * FSharpMemberOrFunctionOrValue
        | Member of FSharpMemberOrFunctionOrValue

    let internal getArgTypes (ctx: Context) (v: FSharpMemberOrFunctionOrValue) =
        let argInfos = v.CurriedParameterGroups |> Seq.map Seq.toList |> Seq.toList 
            
        let retType = v.ReturnParameter.Type

        let argInfos, retType = 
            match argInfos, v.IsPropertyGetterMethod, v.IsPropertySetterMethod with
            | [ AllAndLast(args, last) ], _, true -> [ args ], Some last.Type
            | [[]], true, _ -> [], Some retType
            | _, _, _ -> argInfos, Some retType

        let retType = 
            match retType with
            | Some ty ->
                let coreType = formatType ctx ty
                if v.IsEvent then
                    let isEventHandler = 
                        ty.BaseType 
                        |> Option.bind (fun t -> 
                            if t.HasTypeDefinition then
                                t.TypeDefinition.TryGetFullName()
                             else None)
                        |> Option.exists ((=) "System.MulticastDelegate")
                    if isEventHandler then sprintf "IEvent<%s, _>" coreType else coreType
                else coreType
            | None -> 
                "unit"
            
        argInfos, retType

    /// Convert a getter/setter to its canonical form
    let internal normalizePropertyName (v: FSharpMemberOrFunctionOrValue) =
        let displayName = v.DisplayName
        if (v.IsPropertyGetterMethod && displayName.StartsWithOrdinal("get_")) || 
            (v.IsPropertySetterMethod && displayName.StartsWithOrdinal("set_")) then
            displayName.[4..]
        else displayName

    let internal isEventMember (m: FSharpMemberOrFunctionOrValue) =
        m.IsEvent || m.HasAttribute<CLIEventAttribute>()
    
    let internal formatMember (ctx: Context) m verboseMode = 
        let getParamArgs (argInfos: FSharpParameter list list) (ctx: Context) (v: FSharpMemberOrFunctionOrValue) = 
            let args, namesWithIndices =
                match argInfos with
                | [[x]] when v.IsPropertyGetterMethod && x.Name.IsNone 
                                && x.Type.TypeDefinition.CompiledName = "unit"
                                && x.Type.TypeDefinition.Namespace = Some "Microsoft.FSharp.Core" -> 
                    "", Map.ofList [ctx.ObjectIdent, Set.empty]
                | _  -> formatArgsUsage ctx verboseMode v argInfos
             
            let argText = 
                if String.IsNullOrWhiteSpace(args) then "" 
                elif args.StartsWithOrdinal("(") then args
                elif v.CurriedParameterGroups.Count > 1 && (not verboseMode) then " " + args
                else sprintf "(%s)" args
            argText, namesWithIndices

        let preprocess (ctx: Context) (v: FSharpMemberOrFunctionOrValue) = 
            let buildUsage argInfos = 
                let parArgs, _ = getParamArgs argInfos ctx v
                match v.IsMember, v.IsInstanceMember, v.LogicalName, v.DisplayName with
                // Constructors
                | _, _, ".ctor", _ -> "new" + parArgs
                // Properties (skipping arguments)
                | _, true, _, name when v.IsPropertyGetterMethod || v.IsPropertySetterMethod -> 
                    if name.StartsWithOrdinal("get_") || name.StartsWithOrdinal("set_") then name.[4..] else name
                // Ordinary instance members
                | _, true, _, name -> name + parArgs
                // Ordinary functions or values
                | false, _, _, name when
                    not (v.ApparentEnclosingEntity.HasAttribute<RequireQualifiedAccessAttribute>()) -> 
                    name + " " + parArgs
                // Ordinary static members or things (?) that require fully qualified access
                | _, _, _, name -> name + parArgs

            let modifiers =
                [ if v.InlineAnnotation = FSharpInlineAnnotation.AlwaysInline then yield "inline"
                  if v.Accessibility.IsInternal then yield "internal" ]

            let argInfos, retType = getArgTypes ctx v
            let usage = buildUsage argInfos
            usage, modifiers, argInfos, retType

        // A couple of helper methods for emitting close declarations of members and stub method bodies.
        let closeDeclaration (returnType:string) (writer:ColumnIndentedTextWriter) =
            if verboseMode then writer.Write(": {0}", returnType)
            writer.Write(" = ", returnType)
            if verboseMode then writer.WriteLine("")
        let writeImplementation (ctx:Context) (writer:ColumnIndentedTextWriter) =
            match verboseMode, ctx.MethodBody with
            | false, [| singleLine |] -> writer.WriteLine(singleLine)
            | _, lines ->
                writer.Indent ctx.Indentation
                for line in lines do
                    writer.WriteLine(line)
                writer.Unindent ctx.Indentation

        match m with
        | MemberInfo.PropertyGetSet(getter, setter) ->
            let (usage, modifiers, getterArgInfos, retType) = preprocess ctx getter
            let closeDeclaration = closeDeclaration retType
            let writeImplementation = writeImplementation ctx
            let (_, _, setterArgInfos, _) = preprocess ctx setter
            let writer = ctx.Writer
            writer.Write("member ")
            for modifier in modifiers do
                writer.Write("{0} ", modifier)
            writer.Write("{0}.", ctx.ObjectIdent)

            // Try to print getters and setters on the same identifier
            writer.WriteLine(usage)
            writer.Indent ctx.Indentation
            match getParamArgs getterArgInfos ctx getter with
            | "", _ | "()", _ -> writer.Write("with get ()")
            | args, _ -> writer.Write("with get {0}", args)
            writer |> closeDeclaration
            writer |> writeImplementation
            match getParamArgs setterArgInfos ctx setter with
            | "", _ | "()", _ ->
                if verboseMode then writer.WriteLine("and set (v: {0}): unit = ", retType)
                else writer.Write("and set v = ")
            | args, namesWithIndices ->
                let valueArgName, _ = normalizeArgName namesWithIndices "v"
                if verboseMode then writer.WriteLine("and set {0} ({1}: {2}): unit = ", args, valueArgName, retType)
                else writer.Write("and set {0} {1} = ", args, valueArgName)
            writer |> writeImplementation
            writer.Unindent ctx.Indentation

        | MemberInfo.Member v ->
            let (usage, modifiers, argInfos, retType) = preprocess ctx v
            let closeDeclaration = closeDeclaration retType
            let writeImplementation = writeImplementation ctx
            let writer = ctx.Writer
            if isEventMember v then
                writer.WriteLine("[<CLIEvent>]")
            writer.Write("member ")
            for modifier in modifiers do
                writer.Write("{0} ", modifier)
            writer.Write("{0}.", ctx.ObjectIdent)
        
            if v.IsEvent then
                writer.Write(usage)
                writer |> closeDeclaration
                writer |> writeImplementation
            elif v.IsPropertySetterMethod then
                writer.WriteLine(usage)
                writer.Indent ctx.Indentation
                match getParamArgs argInfos ctx v with
                | "", _ | "()", _ ->
                    writer.WriteLine("with set (v: {0}): unit = ", retType)
                | args, namesWithIndices ->
                    let valueArgName, _ = normalizeArgName namesWithIndices "v"
                    writer.Write("with set {0} ({1}", args, valueArgName)
                    if verboseMode then writer.Write(": {0}): unit", retType)
                    else writer.Write(")")
                    writer.Write(" = ")
                    if verboseMode then writer.WriteLine("")

                writer |> writeImplementation
                writer.Unindent ctx.Indentation
            elif v.IsPropertyGetterMethod then
                writer.Write(usage)
                match getParamArgs argInfos ctx v with
                | "", _ | "()", _ ->
                    // Use the short-hand notation for getters without arguments
                    writer |> closeDeclaration
                    writer |> writeImplementation
                | args, _ ->
                    writer.WriteLine("")
                    writer.Indent ctx.Indentation
                    writer.Write("with get {0}", args)
                    writer |> closeDeclaration
                    writer |> writeImplementation
                    writer.Unindent ctx.Indentation
            else
                writer.Write(usage)
                writer |> closeDeclaration
                writer |> writeImplementation

    let rec internal getNonAbbreviatedType (ty: FSharpType) =
        if ty.HasTypeDefinition && ty.TypeDefinition.IsFSharpAbbreviation then
            getNonAbbreviatedType ty.AbbreviatedType
        else ty

    // Sometimes interface members are stored in the form of `IInterface<'T> -> ...`,
    // so we need to get the 2nd generic argument
    let internal (|MemberFunctionType|_|) (ty: FSharpType) =
        if ty.IsFunctionType && ty.GenericArguments.Count = 2 then
            Some ty.GenericArguments.[1]
        else None

    let internal (|TypeOfMember|_|) (m: FSharpMemberOrFunctionOrValue) =
        match m.FullTypeSafe with
        | Some (MemberFunctionType ty) when m.IsProperty && m.DeclaringEntity.IsSome && m.DeclaringEntity.Value.IsFSharp ->
            Some ty
        | Some ty -> Some ty
        | None -> None

    let internal (|EventFunctionType|_|) (ty: FSharpType) =
        match ty with
        | MemberFunctionType ty ->
            if ty.IsFunctionType && ty.GenericArguments.Count = 2 then
                let retType = ty.GenericArguments.[0]
                let argType = ty.GenericArguments.[1]
                if argType.GenericArguments.Count = 2 then
                    Some (argType.GenericArguments.[0], retType)
                else None
            else None
        | _ ->
            None

    let internal removeWhitespace (str: string) = 
        str.Replace(" ", "")

    /// Filter out duplicated interfaces in inheritance chain
    let rec internal getInterfaces (e: FSharpEntity) = 
        seq { for iface in e.AllInterfaces ->
                let ty = getNonAbbreviatedType iface
                // Argument should be kept lazy so that it is only evaluated when instantiating a new type
                ty.TypeDefinition, Seq.zip ty.TypeDefinition.GenericParameters ty.GenericArguments
        }
        |> Seq.distinct

    /// Get members in the decreasing order of inheritance chain
    let GetInterfaceMembers (entity: FSharpEntity) = 
        seq {
            for (iface, instantiations) in getInterfaces entity do
                yield! iface.TryGetMembersFunctionsAndValues()
                       |> Seq.choose (fun m -> 
                           // Use this hack when FCS doesn't return enough information on .NET properties and events
                           if m.IsProperty || m.IsEventAddMethod || m.IsEventRemoveMethod then 
                               None 
                           else Some (m, instantiations))
         }

    /// Check whether an interface is empty
    let HasNoInterfaceMember entity =
        GetInterfaceMembers entity |> Seq.isEmpty

    let internal (|LongIdentPattern|_|) = function
        | SynPat.LongIdent(LongIdentWithDots(xs, _), _, _, _, _, _) ->
//            let (name, range) = xs |> List.map (fun x -> x.idText, x.idRange) |> List.last
            let last = List.last xs
            Some(last.idText, last.idRange)
        | _ -> 
            None

    // Get name and associated range of a member
    // On merged properties (consisting both getters and setters), they have the same range values,
    // so we use 'get_' and 'set_' prefix to ensure corresponding symbols are retrieved correctly.
    let internal (|MemberNameAndRange|_|) = function
        | SynBinding(_access, _bindingKind, _isInline, _isMutable, _attrs, _xmldoc, SynValData(Some mf, _, _), LongIdentPattern(name, range), 
                     _retTy, _expr, _bindingRange, _seqPoint) when mf.MemberKind = SynMemberKind.PropertyGet ->
            if name.StartsWithOrdinal("get_") then Some(name, range) else Some("get_" + name, range)
        | SynBinding(_access, _bindingKind, _isInline, _isMutable, _attrs, _xmldoc, SynValData(Some mf, _, _), LongIdentPattern(name, range), 
                     _retTy, _expr, _bindingRange, _seqPoint) when mf.MemberKind = SynMemberKind.PropertySet ->
            if name.StartsWithOrdinal("set_") then Some(name, range) else Some("set_" + name, range)
        | SynBinding(_access, _bindingKind, _isInline, _isMutable, _attrs, _xmldoc, _valData, LongIdentPattern(name, range), 
                     _retTy, _expr, _bindingRange, _seqPoint) ->
            Some(name, range)
        | _ ->
            None

    /// Get associated member names and ranges
    /// In case of properties, intrinsic ranges might not be correct for the purpose of getting
    /// positions of 'member', which indicate the indentation for generating new members
    let GetMemberNameAndRanges interfaceData = 
        match interfaceData with
        | InterfaceData.Interface(_, None) -> 
            []
        | InterfaceData.Interface(_, Some memberDefns) -> 
            memberDefns
            |> Seq.choose (function (SynMemberDefn.Member(binding, _)) -> Some binding | _ -> None)
            |> Seq.choose (|MemberNameAndRange|_|)
            |> Seq.toList
        | InterfaceData.ObjExpr(_, bindings) -> 
            List.choose (|MemberNameAndRange|_|) bindings

    let internal normalizeEventName (m: FSharpMemberOrFunctionOrValue) =
        let name = m.DisplayName
        if name.StartsWithOrdinal("add_") then name.[4..]
        elif name.StartsWithOrdinal("remove_")  then name.[7..]
        else name

    /// Ideally this info should be returned in error symbols from FCS. 
    /// Because it isn't, we implement a crude way of getting member signatures:
    ///  (1) Crack ASTs to get member names and their associated ranges
    ///  (2) Check symbols of those members based on ranges
    ///  (3) If any symbol found, capture its member signature 
    let GetImplementedMemberSignatures (getMemberByLocation: string * range -> FSharpSymbolUse option) displayContext interfaceData = 
        let formatMemberSignature (symbolUse: FSharpSymbolUse) =            
            match symbolUse.Symbol with
            | :? FSharpMemberOrFunctionOrValue as m ->
                match m.FullTypeSafe with
                | Some _ when isEventMember m ->
                    // Events don't have overloads so we use only display names for comparison
                    let signature = normalizeEventName m
                    Some [signature]
                | Some ty ->
                    let signature = removeWhitespace (sprintf "%s:%s" m.DisplayName (ty.Format(displayContext)))
                    Some [signature]
                | None ->
                    None
            | _ ->
                //fail "Should only accept symbol uses of members."
                None
        async {
            let symbolUses = 
                GetMemberNameAndRanges interfaceData
                |> List.toArray
                |> Array.map getMemberByLocation
            return symbolUses |> Array.choose (Option.bind formatMemberSignature >> Option.map String.Concat)
                              |> Set.ofArray
        }

    /// Check whether an entity is an interface or type abbreviation of an interface
    let rec IsInterface (entity: FSharpEntity) =
        entity.IsInterface || (entity.IsFSharpAbbreviation && IsInterface entity.AbbreviatedType.TypeDefinition)

    /// Generate stub implementation of an interface at a start column
    let FormatInterface startColumn indentation (typeInstances: string []) objectIdent
            (methodBody: string) (displayContext: FSharpDisplayContext) excludedMemberSignatures
            (e: FSharpEntity) verboseMode =
        Debug.Assert(IsInterface e, "The entity should be an interface.")
        let lines = String.getLines methodBody
        use writer = new ColumnIndentedTextWriter()
        let typeParams = Seq.map getTypeParameterName e.GenericParameters
        let instantiations = 
            let insts =
                Seq.zip typeParams typeInstances
                // Filter out useless instances (when it is replaced by the same name or by wildcard)
                |> Seq.filter(fun (t1, t2) -> t1 <> t2 && t2 <> "_") 
                |> Map.ofSeq
            // A simple hack to handle instantiation of type alias 
            if e.IsFSharpAbbreviation then
                let ty = getNonAbbreviatedType e.AbbreviatedType
                (ty.TypeDefinition.GenericParameters |> Seq.map getTypeParameterName, 
                    ty.GenericArguments |> Seq.map (fun ty -> ty.Format(displayContext)))
                ||> Seq.zip
                |> Seq.fold (fun acc (x, y) -> Map.add x y acc) insts
            else insts
        let ctx = { Writer = writer; TypeInstantations = instantiations; ArgInstantiations = Seq.empty;
                    Indentation = indentation; ObjectIdent = objectIdent; MethodBody = lines; DisplayContext = displayContext }
        let missingMembers =
            GetInterfaceMembers e
            |> Seq.groupBy (fun (m, insts) ->               
                match m with
                | _ when isEventMember m  ->
                    Some (normalizeEventName m)
                | TypeOfMember ty ->
                    let signature = removeWhitespace (sprintf "%s:%s" m.DisplayName (formatType { ctx with ArgInstantiations = insts } ty))
                    Some signature 
                | _ -> 
                    //debug "FullType throws exceptions due to bugs in FCS."
                    None)
            |> Seq.collect (fun (signature, members) ->               
                match signature with
                | None ->
                    members
                | Some signature when not (Set.contains signature excludedMemberSignatures) ->
                    // Return the first member from a group of members for a particular signature
                    Seq.truncate 1 members
                | _ -> Seq.empty)

        // All members have already been implemented
        if Seq.isEmpty missingMembers then
            String.Empty
        else
            writer.Indent startColumn
            writer.WriteLine("")
            let duplicatedMembers =
                missingMembers
                |> Seq.countBy(fun (m, insts) -> m.DisplayName, insts |> Seq.length)
                |> Seq.filter (snd >> (<) 1)
                |> Seq.map (fst >> fst)
                |> Set.ofSeq

            let getReturnType v = snd (getArgTypes ctx v)
            let rec formatMembers (members : (FSharpMemberOrFunctionOrValue * _) list) =
                match members with
                // Since there is no unified source of information for properties,
                // we try to merge getters and setters when they seem to match.
                // Assume that getter and setter come right after each other.
                // They belong to the same property if names and return types are the same
                | (getter as first, insts) :: (setter, _) :: otherMembers
                | (setter as first, _) :: (getter, insts) :: otherMembers when
                    getter.IsPropertyGetterMethod && setter.IsPropertySetterMethod &&
                    normalizePropertyName getter = normalizePropertyName setter &&
                    getReturnType getter = getReturnType setter ->
                    let useVerboseMode = verboseMode || duplicatedMembers.Contains first.DisplayName
                    formatMember { ctx with ArgInstantiations = insts } (MemberInfo.PropertyGetSet(getter, setter)) useVerboseMode
                    formatMembers otherMembers
                | (m, insts) :: otherMembers ->
                    let useVerboseMode = verboseMode || duplicatedMembers.Contains m.DisplayName
                    formatMember { ctx with ArgInstantiations = insts } (MemberInfo.Member m) useVerboseMode
                    formatMembers otherMembers
                | [] -> ()

            missingMembers
            |> Seq.sortBy (fun (m, _) -> 
                // Sort by normalized name and return type so that getters and setters of the same properties
                // are guaranteed to be neighboring.
                normalizePropertyName m, getReturnType m)
            |> Seq.toList
            |> formatMembers
            writer.Dump()

    /// Find corresponding interface declaration at a given position
    let TryFindInterfaceDeclaration (pos: pos) (parsedInput: ParsedInput) =
        let rec walkImplFileInput (ParsedImplFileInput (modules = moduleOrNamespaceList)) = 
            List.tryPick walkSynModuleOrNamespace moduleOrNamespaceList

        and walkSynModuleOrNamespace(SynModuleOrNamespace(decls = decls; range = range)) =
            if not <| rangeContainsPos range pos then
                None
            else
                List.tryPick walkSynModuleDecl decls

        and walkSynModuleDecl(decl: SynModuleDecl) =
            if not <| rangeContainsPos decl.Range pos then
                None
            else
                match decl with
                | SynModuleDecl.Exception(SynExceptionDefn(_, synMembers, _), _) -> 
                    List.tryPick walkSynMemberDefn synMembers
                | SynModuleDecl.Let(_isRecursive, bindings, _range) ->
                    List.tryPick walkBinding bindings
                | SynModuleDecl.ModuleAbbrev(_lhs, _rhs, _range) ->
                    None
                | SynModuleDecl.NamespaceFragment(fragment) ->
                    walkSynModuleOrNamespace fragment
                | SynModuleDecl.NestedModule(_, _, modules, _, _) ->
                    List.tryPick walkSynModuleDecl modules
                | SynModuleDecl.Types(typeDefs, _range) ->
                    List.tryPick walkSynTypeDefn typeDefs
                | SynModuleDecl.DoExpr (_, expr, _) ->
                    walkExpr expr
                | SynModuleDecl.Attributes _
                | SynModuleDecl.HashDirective _
                | SynModuleDecl.Open _ -> 
                    None

        and walkSynTypeDefn(SynTypeDefn(_componentInfo, representation, members, _, range)) = 
            if not <| rangeContainsPos range pos then
                None
            else
                walkSynTypeDefnRepr representation
                |> Option.orElse (List.tryPick walkSynMemberDefn members)        

        and walkSynTypeDefnRepr(typeDefnRepr: SynTypeDefnRepr) = 
            if not <| rangeContainsPos typeDefnRepr.Range pos then
                None
            else
                match typeDefnRepr with
                | SynTypeDefnRepr.ObjectModel(_kind, members, _range) ->
                    List.tryPick walkSynMemberDefn members
                | SynTypeDefnRepr.Simple(_repr, _range) -> 
                    None
                | SynTypeDefnRepr.Exception _ -> None

        and walkSynMemberDefn (memberDefn: SynMemberDefn) =
            if not <| rangeContainsPos memberDefn.Range pos then
                None
            else
                match memberDefn with
                | SynMemberDefn.AbstractSlot(_synValSig, _memberFlags, _range) ->
                    None
                | SynMemberDefn.AutoProperty(_attributes, _isStatic, _id, _type, _memberKind, _memberFlags, _xmlDoc, _access, expr, _r1, _r2) ->
                    walkExpr expr
                | SynMemberDefn.Interface(interfaceType, members, _range) ->
                    if rangeContainsPos interfaceType.Range pos then
                        Some(InterfaceData.Interface(interfaceType, members))
                    else
                        Option.bind (List.tryPick walkSynMemberDefn) members
                | SynMemberDefn.Member(binding, _range) ->
                    walkBinding binding
                | SynMemberDefn.NestedType(typeDef, _access, _range) -> 
                    walkSynTypeDefn typeDef
                | SynMemberDefn.ValField(_field, _range) ->
                    None
                | SynMemberDefn.LetBindings(bindings, _isStatic, _isRec, _range) ->
                    List.tryPick walkBinding bindings
                | SynMemberDefn.Open _
                | SynMemberDefn.ImplicitCtor _
                | SynMemberDefn.Inherit _ -> None
                | SynMemberDefn.ImplicitInherit (_, expr, _, _) -> walkExpr expr

        and walkBinding (SynBinding(_access, _bindingKind, _isInline, _isMutable, _attrs, _xmldoc, _valData, _headPat, _retTy, expr, _bindingRange, _seqPoint)) =
            walkExpr expr

        and walkExpr expr =
            if not <| rangeContainsPos expr.Range pos then 
                None
            else
                match expr with
                | SynExpr.Quote (synExpr1, _, synExpr2, _, _range) ->
                    List.tryPick walkExpr [synExpr1; synExpr2]

                | SynExpr.Const (_synConst, _range) -> 
                    None

                | SynExpr.Paren (synExpr, _, _, _parenRange) ->
                    walkExpr synExpr
                | SynExpr.Typed (synExpr, _synType, _range) -> 
                    walkExpr synExpr

                | SynExpr.Tuple (_, synExprList, _, _range)
                | SynExpr.ArrayOrList (_, synExprList, _range) ->
                    List.tryPick walkExpr synExprList

                | SynExpr.Record (_inheritOpt, _copyOpt, fields, _range) -> 
                    List.tryPick (fun (_, e, _) -> Option.bind walkExpr e) fields

                | SynExpr.New (_, _synType, synExpr, _range) -> 
                    walkExpr synExpr

                | SynExpr.ObjExpr (ty, baseCallOpt, binds, ifaces, _range1, _range2) -> 
                    match baseCallOpt with
                    | None -> 
                        if rangeContainsPos ty.Range pos then
                            Some (InterfaceData.ObjExpr(ty, binds))
                        else
                            ifaces |> List.tryPick (fun (SynInterfaceImpl(ty, binds, range)) ->
                                if rangeContainsPos range pos then 
                                    Some (InterfaceData.ObjExpr(ty, binds))
                                else None)
                    | Some _ -> 
                        // Ignore object expressions of normal objects
                        None

                | SynExpr.While (_sequencePointInfoForWhileLoop, synExpr1, synExpr2, _range) ->
                    List.tryPick walkExpr [synExpr1; synExpr2]
                | SynExpr.ForEach (_sequencePointInfoForForLoop, _seqExprOnly, _isFromSource, _synPat, synExpr1, synExpr2, _range) -> 
                    List.tryPick walkExpr [synExpr1; synExpr2]

                | SynExpr.For (_sequencePointInfoForForLoop, _ident, synExpr1, _, synExpr2, synExpr3, _range) -> 
                    List.tryPick walkExpr [synExpr1; synExpr2; synExpr3]

                | SynExpr.ArrayOrListOfSeqExpr (_, synExpr, _range) ->
                    walkExpr synExpr
                | SynExpr.CompExpr (_, _, synExpr, _range) ->
                    walkExpr synExpr
                | SynExpr.Lambda (_, _, _synSimplePats, synExpr, _, _range) ->
                     walkExpr synExpr

                | SynExpr.MatchLambda (_isExnMatch, _argm, synMatchClauseList, _spBind, _wholem) -> 
                    synMatchClauseList |> List.tryPick (fun (SynMatchClause(_, _, e, _, _)) -> walkExpr e)
                | SynExpr.Match (_sequencePointInfoForBinding, synExpr, synMatchClauseList, _range) ->
                    walkExpr synExpr
                    |> Option.orElse (synMatchClauseList |> List.tryPick (fun (SynMatchClause(_, _, e, _, _)) -> walkExpr e))

                | SynExpr.Lazy (synExpr, _range) ->
                    walkExpr synExpr
                | SynExpr.Do (synExpr, _range) ->
                    walkExpr synExpr
                | SynExpr.Assert (synExpr, _range) -> 
                    walkExpr synExpr

                | SynExpr.App (_exprAtomicFlag, _isInfix, synExpr1, synExpr2, _range) ->
                    List.tryPick walkExpr [synExpr1; synExpr2]

                | SynExpr.TypeApp (synExpr, _, _synTypeList, _commas, _, _, _range) -> 
                    walkExpr synExpr

                | SynExpr.LetOrUse (_, _, synBindingList, synExpr, _range) -> 
                    Option.orElse (List.tryPick walkBinding synBindingList) (walkExpr synExpr)

                | SynExpr.TryWith (synExpr, _range, _synMatchClauseList, _range2, _range3, _sequencePointInfoForTry, _sequencePointInfoForWith) -> 
                    walkExpr synExpr

                | SynExpr.TryFinally (synExpr1, synExpr2, _range, _sequencePointInfoForTry, _sequencePointInfoForFinally) -> 
                    List.tryPick walkExpr [synExpr1; synExpr2]

                | Sequentials exprs  -> 
                    List.tryPick walkExpr exprs

                | SynExpr.IfThenElse (_, synExpr1, _, synExpr2, _, synExprOpt, _sequencePointInfoForBinding, _isRecovery, _range, _range2) -> 
                    match synExprOpt with
                    | Some synExpr3 ->
                        List.tryPick walkExpr [synExpr1; synExpr2; synExpr3]
                    | None ->
                        List.tryPick walkExpr [synExpr1; synExpr2]

                | SynExpr.Ident (_ident) ->
                    None

                | SynExpr.LongIdent (_, _longIdent, _altNameRefCell, _range) -> 
                    None

                | SynExpr.LongIdentSet (_longIdent, synExpr, _range) ->
                    walkExpr synExpr

                | SynExpr.DotGet (synExpr, _dotm, _longIdent, _range) -> 
                    walkExpr synExpr

                | SynExpr.DotSet (synExpr1, _longIdent, synExpr2, _range) ->
                    List.tryPick walkExpr [synExpr1; synExpr2]

                | SynExpr.Set (synExpr1, synExpr2, _range) ->
                    List.tryPick walkExpr [synExpr1; synExpr2]

                | SynExpr.DotIndexedGet (synExpr, IndexerArgList synExprList, _range, _range2) -> 
                    Option.orElse (walkExpr synExpr) (List.tryPick walkExpr synExprList) 

                | SynExpr.DotIndexedSet (synExpr1, IndexerArgList synExprList, synExpr2, _, _range, _range2) -> 
                    [ yield synExpr1
                      yield! synExprList
                      yield synExpr2 ]
                    |> List.tryPick walkExpr

                | SynExpr.JoinIn (synExpr1, _range, synExpr2, _range2) ->
                    List.tryPick walkExpr [synExpr1; synExpr2]
                | SynExpr.NamedIndexedPropertySet (_longIdent, synExpr1, synExpr2, _range) ->
                    List.tryPick walkExpr [synExpr1; synExpr2]

                | SynExpr.DotNamedIndexedPropertySet (synExpr1, _longIdent, synExpr2, synExpr3, _range) ->  
                    List.tryPick walkExpr [synExpr1; synExpr2; synExpr3]

                | SynExpr.TypeTest (synExpr, _synType, _range)
                | SynExpr.Upcast (synExpr, _synType, _range)
                | SynExpr.Downcast (synExpr, _synType, _range) ->
                    walkExpr synExpr
                | SynExpr.InferredUpcast (synExpr, _range)
                | SynExpr.InferredDowncast (synExpr, _range) ->
                    walkExpr synExpr
                | SynExpr.AddressOf (_, synExpr, _range, _range2) ->
                    walkExpr synExpr
                | SynExpr.TraitCall (_synTyparList, _synMemberSig, synExpr, _range) ->
                    walkExpr synExpr

                | SynExpr.Null (_range)
                | SynExpr.ImplicitZero (_range) -> 
                    None

                | SynExpr.YieldOrReturn (_, synExpr, _range)
                | SynExpr.YieldOrReturnFrom (_, synExpr, _range) 
                | SynExpr.DoBang (synExpr, _range) -> 
                    walkExpr synExpr

                | SynExpr.LetOrUseBang (_sequencePointInfoForBinding, _, _, _synPat, synExpr1, synExprAndBangs, synExpr2, _range) -> 
                    [
                        yield synExpr1
                        for (_,_,_,_,eAndBang,_) in synExprAndBangs do
                            yield eAndBang
                        yield synExpr2
                    ]
                    |> List.tryPick walkExpr

                | SynExpr.LibraryOnlyILAssembly _
                | SynExpr.LibraryOnlyStaticOptimization _ 
                | SynExpr.LibraryOnlyUnionCaseFieldGet _
                | SynExpr.LibraryOnlyUnionCaseFieldSet _ ->
                    None
                | SynExpr.ArbitraryAfterError (_debugStr, _range) -> 
                    None

                | SynExpr.FromParseError (synExpr, _range)
                | SynExpr.DiscardAfterMissingQualificationAfterDot (synExpr, _range) -> 
                    walkExpr synExpr 

                | _ -> None

        match parsedInput with
        | ParsedInput.SigFile _input ->
            None
        | ParsedInput.ImplFile input -> 
            walkImplFileInput input
