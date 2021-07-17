// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Print Signatures/Types, for signatures, intellisense, quick info, FSI responses
module internal FSharp.Compiler.NicePrint

open Internal.Utilities.Collections
open Internal.Utilities.Library
open Internal.Utilities.Library.Extras
open Internal.Utilities.Rational
open FSharp.Compiler 
open FSharp.Compiler.AbstractIL 
open FSharp.Compiler.AbstractIL.IL 
open FSharp.Compiler.AttributeChecking
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Infos
open FSharp.Compiler.InfoReader
open FSharp.Compiler.Syntax
open FSharp.Compiler.Syntax.PrettyNaming
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Layout
open FSharp.Compiler.Text.LayoutRender
open FSharp.Compiler.Text.TaggedText
open FSharp.Compiler.Xml
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TypedTreeOps

open FSharp.Core.Printf

[<AutoOpen>]
module internal PrintUtilities = 
    let bracketIfL x lyt = if x then bracketL lyt else lyt

    let squareAngleL x = LeftL.leftBracketAngle ^^ x ^^ RightL.rightBracketAngle

    let angleL x = sepL TaggedText.leftAngle ^^ x ^^ rightL TaggedText.rightAngle

    let braceL x = wordL TaggedText.leftBrace ^^ x ^^ wordL TaggedText.rightBrace

    let braceBarL x = wordL TaggedText.leftBraceBar ^^ x ^^ wordL TaggedText.rightBraceBar

    let comment str = wordL (tagText (sprintf "(* %s *)" str))

    let layoutsL (ls: Layout list) : Layout =
        match ls with
        | [] -> emptyL
        | [x] -> x
        | x :: xs -> List.fold (^^) x xs 

    let suppressInheritanceAndInterfacesForTyInSimplifiedDisplays g amap m ty = 
        isEnumTy g ty || isDelegateTy g ty || ExistsHeadTypeInEntireHierarchy g amap m ty g.exn_tcr

    let applyMaxMembers maxMembers (allDecls: _ list) = 
        match maxMembers with 
        | Some n when allDecls.Length > n -> (allDecls |> List.truncate n) @ [wordL (tagPunctuation "...")] 
        | _ -> allDecls

    /// fix up a name coming from IL metadata by quoting "funny" names (keywords, otherwise invalid identifiers)
    let adjustILName n =
        n |> Lexhelp.Keywords.QuoteIdentifierIfNeeded

    // Put the "+ N overloads" into the layout
    let shrinkOverloads layoutFunction resultFunction group = 
        match group with 
        | [x] -> [resultFunction x (layoutFunction x)] 
        | (x :: rest) -> [ resultFunction x (layoutFunction x -- leftL (tagText (match rest.Length with 1 -> FSComp.SR.nicePrintOtherOverloads1() | n -> FSComp.SR.nicePrintOtherOverloadsN(n)))) ] 
        | _ -> []
    
    let layoutTyconRefImpl isAttribute (denv: DisplayEnv) (tcref: TyconRef) =
        let tagEntityRefName (xref: EntityRef) name =
            if xref.IsNamespace then tagNamespace name
            elif xref.IsModule then tagModule name
            elif xref.IsTypeAbbrev then
                let ty = xref.TypeAbbrev.Value
                match stripTyEqns denv.g ty with
                | TType_app(tcref, _) when tcref.IsStructOrEnumTycon ->
                    tagStruct name
                | _ ->
                    tagAlias name
            elif xref.IsFSharpDelegateTycon then tagDelegate name
            elif xref.IsILEnumTycon || xref.IsFSharpEnumTycon then tagEnum name
            elif xref.IsStructOrEnumTycon then tagStruct name
            elif isInterfaceTyconRef xref then tagInterface name
            elif xref.IsUnionTycon then tagUnion name
            elif xref.IsRecordTycon then tagRecord name
            else tagClass name

        let demangled = 
            let name =
                if denv.includeStaticParametersInTypeNames then 
                    tcref.DisplayNameWithStaticParameters 
                elif tcref.DisplayName = tcref.DisplayNameWithStaticParameters then
                    tcref.DisplayName // has no static params
                else
                    tcref.DisplayName+"<...>" // shorten
            if isAttribute && name.EndsWithOrdinal("Attribute") then
                String.dropSuffix name "Attribute"
            else 
                name
        let tyconTextL =
            tagEntityRefName tcref demangled
            |> mkNav tcref.DefinitionRange
            |> wordL
        if denv.shortTypeNames then 
            tyconTextL
        else
            let path = tcref.CompilationPath.DemangledPath
            let path =
                if denv.includeStaticParametersInTypeNames then
                    path
                else
                    path |> List.map (fun s -> 
                        let i = s.IndexOf(',')
                        if i <> -1 then s.Substring(0, i)+"<...>" // apparently has static params, shorten
                        else s)
            let pathText = trimPathByDisplayEnv denv path
            if pathText = "" then tyconTextL else leftL (tagUnknownEntity pathText) ^^ tyconTextL

    let layoutBuiltinAttribute (denv: DisplayEnv) (attrib: BuiltinAttribInfo) =
        let tcref = attrib.TyconRef
        squareAngleL (layoutTyconRefImpl true denv tcref)

    /// layout the xml docs immediately before another block
    let layoutXmlDoc (denv: DisplayEnv) (xml: XmlDoc) restL =
        if denv.showDocumentation
        then
            let xmlDocL =
                if xml.IsEmpty
                then
                    emptyL
                else
                    xml.UnprocessedLines
                    |> Array.map (fun x ->
                        x.Split('\n') // These lines may have new-lines in them and we need to split them so we can format it
                    )
                    |> Array.concat
                    /// note here that we don't add a space after the triple-slash, because
                    /// the implicit spacing hasn't been trimmed here.
                    |> Array.map (fun line -> ("///" + line) |> tagText |> wordL)
                    |> List.ofArray
                    |> aboveListL
            xmlDocL @@ restL
        else restL

    let private layoutXmlDocFromSig (denv: DisplayEnv) (infoReader: InfoReader) (possibleXmlDoc: XmlDoc) restL (info: (string option * string) option) =
        let xmlDoc =
            if possibleXmlDoc.IsEmpty then
                match info with
                | Some(Some ccuFileName, xmlDocSig) ->
                    infoReader.amap.assemblyLoader.TryFindXmlDocumentationInfo(System.IO.Path.GetFileNameWithoutExtension ccuFileName)
                    |> Option.bind (fun xmlDocInfo ->
                        xmlDocInfo.TryGetXmlDocBySig(xmlDocSig)
                    )
                    |> Option.defaultValue possibleXmlDoc
                | _ ->
                    possibleXmlDoc
            else
                possibleXmlDoc
        layoutXmlDoc denv xmlDoc restL

    let layoutXmlDocOfValRef (denv: DisplayEnv) (infoReader: InfoReader) (vref: ValRef) restL =
        if denv.showDocumentation then
            GetXmlDocSigOfValRef denv.g vref
            |> layoutXmlDocFromSig denv infoReader vref.XmlDoc restL             
        else
            restL

    let layoutXmlDocOfMethInfo (denv: DisplayEnv) (infoReader: InfoReader) (minfo: MethInfo) restL =
        if denv.showDocumentation then
            GetXmlDocSigOfMethInfo infoReader Range.range0 minfo
            |> layoutXmlDocFromSig denv infoReader minfo.XmlDoc restL             
        else
            restL

    let layoutXmlDocOfPropInfo (denv: DisplayEnv) (infoReader: InfoReader) (pinfo: PropInfo) restL =
        if denv.showDocumentation then
            GetXmlDocSigOfProp infoReader Range.range0 pinfo
            |> layoutXmlDocFromSig denv infoReader pinfo.XmlDoc restL             
        else
            restL

    let layoutXmlDocOfEventInfo (denv: DisplayEnv) (infoReader: InfoReader) (einfo: EventInfo) restL =
        if denv.showDocumentation then
            GetXmlDocSigOfEvent infoReader Range.range0 einfo
            |> layoutXmlDocFromSig denv infoReader einfo.XmlDoc restL             
        else
            restL

    let layoutXmlDocOfRecdFieldRef (denv: DisplayEnv) (infoReader: InfoReader) (rfref: RecdFieldRef) restL =
        if denv.showDocumentation then
            GetXmlDocSigOfRecdFieldRef rfref
            |> layoutXmlDocFromSig denv infoReader rfref.RecdField.XmlDoc restL             
        else
            restL

    let layoutXmlDocOfUnionCaseRef (denv: DisplayEnv) (infoReader: InfoReader) (ucref: UnionCaseRef) restL =
        if denv.showDocumentation then
            GetXmlDocSigOfUnionCaseRef ucref
            |> layoutXmlDocFromSig denv infoReader ucref.UnionCase.XmlDoc restL             
        else
            restL

    let layoutXmlDocOfEntityRef (denv: DisplayEnv) (infoReader: InfoReader) (eref: EntityRef) restL =
        if denv.showDocumentation then
            GetXmlDocSigOfEntityRef infoReader Range.range0 eref
            |> layoutXmlDocFromSig denv infoReader eref.XmlDoc restL             
        else
            restL

module private PrintIL = 

    let fullySplitILTypeRef (tref: ILTypeRef) = 
        (List.collect IL.splitNamespace (tref.Enclosing @ [DemangleGenericTypeName tref.Name])) 

    let layoutILTypeRefName denv path =
        let path = 
            match path with 
            | [ "System"; "Void" ] -> ["unit"]
            | [ "System"; "Object" ] -> ["obj"]
            | [ "System"; "String" ] -> ["string"]
            | [ "System"; "Single" ] -> ["float32"]
            | [ "System"; "Double" ] -> ["float"]
            | [ "System"; "Decimal"] -> ["decimal"]
            | [ "System"; "Char" ] -> ["char"]
            | [ "System"; "Byte" ] -> ["byte"]
            | [ "System"; "SByte" ] -> ["sbyte"]
            | [ "System"; "Int16" ] -> ["int16"]
            | [ "System"; "Int32" ] -> ["int" ]
            | [ "System"; "Int64" ] -> ["int64" ]
            | [ "System"; "UInt16" ] -> ["uint16" ]
            | [ "System"; "UInt32" ] -> ["uint" ]
            | [ "System"; "UInt64" ] -> ["uint64" ]
            | [ "System"; "IntPtr" ] -> ["nativeint" ]
            | [ "System"; "UIntPtr" ] -> ["unativeint" ]
            | [ "System"; "Boolean"] -> ["bool"]
            | _ -> path
        let p2, n = List.frontAndBack path
        let tagged = if n = "obj" || n = "string" then tagClass n else tagStruct n
        if denv.shortTypeNames then 
            wordL tagged
          else
            leftL (tagNamespace (trimPathByDisplayEnv denv p2)) ^^ wordL tagged

    let layoutILTypeRef denv tref =
        let path = fullySplitILTypeRef tref
        layoutILTypeRefName denv path

    let layoutILArrayShape (ILArrayShape sh) = 
        SepL.leftBracket ^^ wordL (tagPunctuation (sh |> List.tail |> List.map (fun _ -> ",") |> String.concat "")) ^^ RightL.rightBracket // drop off one "," so that a n-dimensional array has n - 1 ","'s

    let paramsL (ps: Layout list) : Layout = 
        match ps with
        | [] -> emptyL
        | _ -> 
            let body = Layout.commaListL ps
            SepL.leftAngle ^^ body ^^ RightL.rightAngle

    let pruneParams (className: string) (ilTyparSubst: Layout list) =
        let numParams = 
            // can't find a way to see the number of generic parameters for *this* class (the GenericParams also include type variables for enclosing classes); this will have to do
            let rightMost = className |> SplitNamesForILPath |> List.last
            match System.Int32.TryParse(rightMost, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture) with 
            | true, n -> n
            | false, _ -> 0 // looks like it's non-generic
        ilTyparSubst |> List.rev |> List.truncate numParams |> List.rev
 
    let rec layoutILType (denv: DisplayEnv) (ilTyparSubst: Layout list) (ty: ILType) : Layout =
        match ty with
        | ILType.Void -> WordL.structUnit // These are type-theoretically totally different type-theoretically `void` is Fin 0 and `unit` is Fin (S 0) ... but, this looks like as close as we can get.
        | ILType.Array (sh, t) -> layoutILType denv ilTyparSubst t ^^ layoutILArrayShape sh
        | ILType.Value t
        | ILType.Boxed t -> layoutILTypeRef denv t.TypeRef ^^ (t.GenericArgs |> List.map (layoutILType denv ilTyparSubst) |> paramsL)
        | ILType.Ptr t
        | ILType.Byref t -> layoutILType denv ilTyparSubst t
        | ILType.FunctionPointer t -> layoutILCallingSignature denv ilTyparSubst None t
        | ILType.TypeVar n -> List.item (int n) ilTyparSubst
        | ILType.Modified (_, _, t) -> layoutILType denv ilTyparSubst t // Just recurse through them to the contained ILType

    /// Layout a function pointer signature using type-only-F#-style. No argument names are printed.
    and private layoutILCallingSignature denv ilTyparSubst cons (signature: ILCallingSignature) =
        // We need a special case for
        // constructors (Their return types are reported as `void`, but this is
        // incorrect; so if we're dealing with a constructor we require that the
        // return type be passed along as the `cons` parameter.)
        let args = signature.ArgTypes |> List.map (layoutILType denv ilTyparSubst) 
        let res = 
            match cons with
            | Some className -> 
                let names = SplitNamesForILPath (DemangleGenericTypeName className)
                // special case for constructor return-type (viz., the class itself)
                layoutILTypeRefName denv names ^^ (pruneParams className ilTyparSubst |> paramsL) 
            | None -> 
                signature.ReturnType |> layoutILType denv ilTyparSubst
        
        match args with
        | [] -> WordL.structUnit ^^ WordL.arrow ^^ res
        | [x] -> x ^^ WordL.arrow ^^ res
        | _ -> sepListL WordL.star args ^^ WordL.arrow ^^ res

    let layoutILFieldInit x =
        let textOpt = 
            match x with
            | Some init -> 
                match init with
                | ILFieldInit.Bool x -> 
                    if x
                    then Some TaggedText.keywordTrue
                    else Some TaggedText.keywordFalse
                | ILFieldInit.Char c -> ("'" + (char c).ToString () + "'") |> (tagStringLiteral >> Some)
                | ILFieldInit.Int8 x -> ((x |> int32 |> string) + "y") |> (tagNumericLiteral >> Some)
                | ILFieldInit.Int16 x -> ((x |> int32 |> string) + "s") |> (tagNumericLiteral >> Some)
                | ILFieldInit.Int32 x -> x |> (string >> tagNumericLiteral >> Some)
                | ILFieldInit.Int64 x -> ((x |> string) + "L") |> (tagNumericLiteral >> Some)
                | ILFieldInit.UInt8 x -> ((x |> int32 |> string) + "uy") |> (tagNumericLiteral >> Some)
                | ILFieldInit.UInt16 x -> ((x |> int32 |> string) + "us") |> (tagNumericLiteral >> Some)
                | ILFieldInit.UInt32 x -> (x |> int64 |> string) + "u" |> (tagNumericLiteral >> Some)
                | ILFieldInit.UInt64 x -> ((x |> int64 |> string) + "UL") |> (tagNumericLiteral >> Some)
                | ILFieldInit.Single d -> 
                    let s = d.ToString ("g12", System.Globalization.CultureInfo.InvariantCulture)
                    let s = 
                        if String.forall (fun c -> System.Char.IsDigit c || c = '-') s 
                        then s + ".0" 
                        else s
                    (s + "f") |> (tagNumericLiteral >> Some)
                | ILFieldInit.Double d -> 
                      let s = d.ToString ("g12", System.Globalization.CultureInfo.InvariantCulture)
                      let s = 
                          if String.forall (fun c -> System.Char.IsDigit c || c = '-') s 
                          then (s + ".0")
                          else s
                      s |> (tagNumericLiteral >> Some)
                | _ -> None
            | None -> None
        match textOpt with
        | None -> WordL.equals ^^ (comment "value unavailable")
        | Some s -> WordL.equals ^^ wordL s

    let layoutILEnumDefParts nm litVal =
        WordL.bar ^^ wordL (tagEnum (adjustILName nm)) ^^ layoutILFieldInit litVal

module private PrintTypes = 
    // Note: We need nice printing of constants in order to print literals and attributes 
    let layoutConst g ty c =
        let str = 
            match c with
            | Const.Bool x -> if x then TaggedText.keywordTrue else TaggedText.keywordFalse
            | Const.SByte x -> (x |> string)+"y" |> tagNumericLiteral
            | Const.Byte x -> (x |> string)+"uy" |> tagNumericLiteral
            | Const.Int16 x -> (x |> string)+"s" |> tagNumericLiteral
            | Const.UInt16 x -> (x |> string)+"us" |> tagNumericLiteral
            | Const.Int32 x -> (x |> string) |> tagNumericLiteral
            | Const.UInt32 x -> (x |> string)+"u" |> tagNumericLiteral
            | Const.Int64 x -> (x |> string)+"L" |> tagNumericLiteral
            | Const.UInt64 x -> (x |> string)+"UL" |> tagNumericLiteral
            | Const.IntPtr x -> (x |> string)+"n" |> tagNumericLiteral
            | Const.UIntPtr x -> (x |> string)+"un" |> tagNumericLiteral
            | Const.Single d -> 
                 ((let s = d.ToString("g12", System.Globalization.CultureInfo.InvariantCulture)
                  if String.forall (fun c -> System.Char.IsDigit(c) || c = '-') s 
                  then s + ".0" 
                  else s) + "f") |> tagNumericLiteral
            | Const.Double d -> 
                let s = d.ToString("g12", System.Globalization.CultureInfo.InvariantCulture)
                (if String.forall (fun c -> System.Char.IsDigit(c) || c = '-') s 
                then s + ".0" 
                else s) |> tagNumericLiteral
            | Const.Char c -> "'" + c.ToString() + "'" |> tagStringLiteral
            | Const.String bs -> "\"" + bs + "\"" |> tagNumericLiteral
            | Const.Unit -> "()" |> tagPunctuation
            | Const.Decimal bs -> string bs + "M" |> tagNumericLiteral
            // either "null" or "the default value for a struct"
            | Const.Zero -> tagKeyword(if isRefTy g ty then "null" else "default")
        wordL str

    let layoutAccessibility (denv: DisplayEnv) accessibility itemL =
        let isInternalCompPath x = 
            match x with 
            | CompPath(ILScopeRef.Local, []) -> true 
            | _ -> false
        let (|Public|Internal|Private|) (TAccess p) = 
            match p with 
            | [] -> Public 
            | _ when List.forall isInternalCompPath p -> Internal 
            | _ -> Private
        match denv.contextAccessibility, accessibility with
        | Public, Internal -> WordL.keywordInternal ++ itemL   // print modifier, since more specific than context
        | Public, Private -> WordL.keywordPrivate ++ itemL     // print modifier, since more specific than context
        | Internal, Private -> WordL.keywordPrivate ++ itemL   // print modifier, since more specific than context
        | _ -> itemL

    /// Layout a reference to a type 
    let layoutTyconRefImpl denv tycon = layoutTyconRefImpl false denv tycon

    /// Layout the flags of a member 
    let layoutMemberFlags (memFlags: SynMemberFlags) = 
        let stat = 
            if memFlags.IsInstance || (memFlags.MemberKind = SynMemberKind.Constructor) then emptyL 
            else WordL.keywordStatic
        let stat = 
            if memFlags.IsDispatchSlot then stat ++ WordL.keywordAbstract
            elif memFlags.IsOverrideOrExplicitImpl then stat ++ WordL.keywordOverride
            else stat
        let stat = 
            if memFlags.IsOverrideOrExplicitImpl then stat else
            match memFlags.MemberKind with 
            | SynMemberKind.ClassConstructor 
            | SynMemberKind.Constructor 
            | SynMemberKind.PropertyGetSet -> stat
            | SynMemberKind.Member 
            | SynMemberKind.PropertyGet 
            | SynMemberKind.PropertySet -> stat ++ WordL.keywordMember

        // let stat = if memFlags.IsFinal then stat ++ wordL "final" else stat in
        stat

    /// Layout a single attribute arg, following the cases of 'gen_attr_arg' in ilxgen.fs
    /// This is the subset of expressions we display in the NicePrint pretty printer 
    /// See also dataExprL - there is overlap between these that should be removed 
    let rec private layoutAttribArg denv arg = 
        match arg with 
        | Expr.Const (c, _, ty) -> 
            if isEnumTy denv.g ty then 
                WordL.keywordEnum ^^ angleL (layoutType denv ty) ^^ bracketL (layoutConst denv.g ty c)
            else
                layoutConst denv.g ty c

        | Expr.Op (TOp.Array, [_elemTy], args, _) ->
            LeftL.leftBracketBar ^^ semiListL (List.map (layoutAttribArg denv) args) ^^ RightL.rightBracketBar

        // Detect 'typeof<ty>' calls 
        | TypeOfExpr denv.g ty ->
            LeftL.keywordTypeof ^^ wordL (tagPunctuation "<") ^^ layoutType denv ty ^^ rightL (tagPunctuation ">")

        // Detect 'typedefof<ty>' calls 
        | TypeDefOfExpr denv.g ty ->
            LeftL.keywordTypedefof ^^ wordL (tagPunctuation "<") ^^ layoutType denv ty ^^ rightL (tagPunctuation ">")

        | Expr.Op (TOp.Coerce, [tgTy;_], [arg2], _) ->
            leftL (tagPunctuation "(") ^^ layoutAttribArg denv arg2 ^^ wordL (tagPunctuation ":>") ^^ layoutType denv tgTy ^^ rightL (tagPunctuation ")")

        | AttribBitwiseOrExpr denv.g (arg1, arg2) ->
            layoutAttribArg denv arg1 ^^ wordL (tagPunctuation "|||") ^^ layoutAttribArg denv arg2

        // Detect explicit enum values 
        | EnumExpr denv.g arg1 ->
            WordL.keywordEnum ++ bracketL (layoutAttribArg denv arg1)


        | _ -> comment "(* unsupported attribute argument *)"

    /// Layout arguments of an attribute 'arg1, ..., argN' 
    and private layoutAttribArgs denv args = 
        sepListL (rightL (tagPunctuation ",")) (List.map (fun (AttribExpr(e1, _)) -> layoutAttribArg denv e1) args)

    /// Layout an attribute 'Type(arg1, ..., argN)' 
    //
    // REVIEW: we are ignoring "props" here
    and layoutAttrib denv (Attrib(_, k, args, _props, _, _, _)) = 
        let argsL = bracketL (layoutAttribArgs denv args)
        match k with 
        | ILAttrib ilMethRef -> 
            let trimmedName = 
                let name = ilMethRef.DeclaringTypeRef.Name
                if name.EndsWithOrdinal("Attribute") then
                    String.dropSuffix name "Attribute"
                else
                    name
            let tref = ilMethRef.DeclaringTypeRef
            let tref = ILTypeRef.Create(scope= tref.Scope, enclosing=tref.Enclosing, name=trimmedName)
            PrintIL.layoutILTypeRef denv tref ++ argsL
        | FSAttrib vref -> 
            // REVIEW: this is not trimming "Attribute" 
            let _, _, _, rty, _ = GetTypeOfMemberInMemberForm denv.g vref
            let rty = GetFSharpViewOfReturnType denv.g rty
            let tcref = tcrefOfAppTy denv.g rty
            layoutTyconRefImpl denv tcref ++ argsL

    and layoutILAttribElement denv arg = 
        match arg with 
        | ILAttribElem.String (Some x) -> wordL (tagStringLiteral ("\"" + x + "\""))
        | ILAttribElem.String None -> wordL (tagStringLiteral "")
        | ILAttribElem.Bool x -> if x then WordL.keywordTrue else WordL.keywordFalse
        | ILAttribElem.Char x -> wordL (tagStringLiteral ("'" + x.ToString() + "'" ))
        | ILAttribElem.SByte x -> wordL (tagNumericLiteral ((x |> string)+"y"))
        | ILAttribElem.Int16 x -> wordL (tagNumericLiteral ((x |> string)+"s"))
        | ILAttribElem.Int32 x -> wordL (tagNumericLiteral ((x |> string)))
        | ILAttribElem.Int64 x -> wordL (tagNumericLiteral ((x |> string)+"L"))
        | ILAttribElem.Byte x -> wordL (tagNumericLiteral ((x |> string)+"uy"))
        | ILAttribElem.UInt16 x -> wordL (tagNumericLiteral ((x |> string)+"us"))
        | ILAttribElem.UInt32 x -> wordL (tagNumericLiteral ((x |> string)+"u"))
        | ILAttribElem.UInt64 x -> wordL (tagNumericLiteral ((x |> string)+"UL"))
        | ILAttribElem.Single x -> 
            let str =
                let s = x.ToString("g12", System.Globalization.CultureInfo.InvariantCulture)
                (if String.forall (fun c -> System.Char.IsDigit(c) || c = '-') s 
                 then s + ".0" 
                 else s) + "f"
            wordL (tagNumericLiteral str)
        | ILAttribElem.Double x -> 
            let str =
                let s = x.ToString("g12", System.Globalization.CultureInfo.InvariantCulture)
                if String.forall (fun c -> System.Char.IsDigit(c) || c = '-') s 
                then s + ".0" 
                else s
            wordL (tagNumericLiteral str)
        | ILAttribElem.Null -> wordL (tagKeyword "null")
        | ILAttribElem.Array (_, xs) -> 
            leftL (tagPunctuation "[|") ^^ semiListL (List.map (layoutILAttribElement denv) xs) ^^ RightL.rightBracketBar
        | ILAttribElem.Type (Some ty) -> 
            LeftL.keywordTypeof ^^ SepL.leftAngle ^^ PrintIL.layoutILType denv [] ty ^^ RightL.rightAngle
        | ILAttribElem.Type None -> wordL (tagText "")
        | ILAttribElem.TypeRef (Some ty) -> 
            LeftL.keywordTypedefof ^^ SepL.leftAngle ^^ PrintIL.layoutILTypeRef denv ty ^^ RightL.rightAngle
        | ILAttribElem.TypeRef None -> emptyL

    and layoutILAttrib denv (ty, args) = 
        let argsL = bracketL (sepListL (rightL (tagPunctuation ",")) (List.map (layoutILAttribElement denv) args))
        PrintIL.layoutILType denv [] ty ++ argsL

    /// Layout '[<attribs>]' above another block 
    and layoutAttribs denv isValue ty kind attrs restL = 
        
        if denv.showAttributes then
            // Don't display DllImport attributes in generated signatures
            let attrs = attrs |> List.filter (IsMatchingFSharpAttributeOpt denv.g denv.g.attrib_DllImportAttribute >> not)
            let attrs = attrs |> List.filter (IsMatchingFSharpAttributeOpt denv.g denv.g.attrib_ContextStaticAttribute >> not)
            let attrs = attrs |> List.filter (IsMatchingFSharpAttributeOpt denv.g denv.g.attrib_ThreadStaticAttribute >> not)
            let attrs = attrs |> List.filter (IsMatchingFSharpAttribute denv.g denv.g.attrib_EntryPointAttribute >> not)
            let attrs = attrs |> List.filter (IsMatchingFSharpAttributeOpt denv.g denv.g.attrib_MarshalAsAttribute >> not)
            let attrs = attrs |> List.filter (IsMatchingFSharpAttribute denv.g denv.g.attrib_ReflectedDefinitionAttribute >> not)
            let attrs = attrs |> List.filter (IsMatchingFSharpAttribute denv.g denv.g.attrib_StructLayoutAttribute >> not)
            let attrs = attrs |> List.filter (IsMatchingFSharpAttribute denv.g denv.g.attrib_AutoSerializableAttribute >> not)
            
            match attrs with
            | [] -> restL 
            | _ -> 
                squareAngleL (sepListL (rightL (tagPunctuation ";")) (List.map (layoutAttrib denv) attrs)) @@ 
                restL
        elif not isValue && (isStructTy denv.g ty && not (isEnumTy denv.g ty)) then
            squareAngleL (wordL (tagClass "Struct")) @@ restL
        else
            match kind with 
            | TyparKind.Type -> restL
            | TyparKind.Measure -> squareAngleL (wordL (tagClass "Measure")) @@ restL

    and layoutTyparAttribs denv kind attrs restL =
        match attrs, kind with
        | [], TyparKind.Type -> restL 
        | _, _ -> squareAngleL (sepListL (rightL (tagPunctuation ";")) ((match kind with TyparKind.Type -> [] | TyparKind.Measure -> [wordL (tagText "Measure")]) @ List.map (layoutAttrib denv) attrs)) ^^ restL

    and private layoutTyparRef denv (typar: Typar) =
        wordL
            (tagTypeParameter 
                (sprintf "%s%s%s"
                    (if denv.showConstraintTyparAnnotations then prefixOfStaticReq typar.StaticReq else "'")
                    (if denv.showImperativeTyparAnnotations then prefixOfRigidTypar typar else "")
                    typar.DisplayName))

    /// Layout a single type parameter declaration, taking TypeSimplificationInfo into account
    /// There are several printing-cases for a typar:
    ///
    ///  'a              - is multiple occurrence.
    ///  _               - singleton occurrence, an underscore preferred over 'b. (OCaml accepts but does not print)
    ///  #Type           - inplace coercion constraint and singleton.
    ///  ('a :> Type)    - inplace coercion constraint not singleton.
    ///  ('a.opM: S->T) - inplace operator constraint.
    ///
    and private layoutTyparRefWithInfo denv (env: SimplifyTypes.TypeSimplificationInfo) (typar: Typar) =
        let varL = layoutTyparRef denv typar
        let varL = if denv.showAttributes then layoutTyparAttribs denv typar.Kind typar.Attribs varL else varL

        match Zmap.tryFind typar env.inplaceConstraints with
        | Some (typarConstraintTy) ->
            if Zset.contains typar env.singletons then
                leftL (tagPunctuation "#") ^^ layoutTypeWithInfo denv env typarConstraintTy
            else
                (varL ^^ sepL (tagPunctuation ":>") ^^ layoutTypeWithInfo denv env typarConstraintTy) |> bracketL

        | _ -> varL

      
    /// Layout type parameter constraints, taking TypeSimplificationInfo into account 
    and layoutConstraintsWithInfo denv env cxs = 
        
        // Internally member constraints get attached to each type variable in their support. 
        // This means we get too many constraints being printed. 
        // So we normalize the constraints to eliminate duplicate member constraints 
        let cxs = 
            cxs
            |> ListSet.setify (fun (_, cx1) (_, cx2) ->
                match cx1, cx2 with 
                | TyparConstraint.MayResolveMember(traitInfo1, _),
                  TyparConstraint.MayResolveMember(traitInfo2, _) -> traitsAEquiv denv.g TypeEquivEnv.Empty traitInfo1 traitInfo2
                | _ -> false)

        let cxsL = List.collect (layoutConstraintWithInfo denv env) cxs
        match cxsL with 
        | [] -> emptyL 
        | _ -> 
            if denv.abbreviateAdditionalConstraints then 
                wordL (tagKeyword "when") ^^ wordL(tagText "<constraints>")
            elif denv.shortConstraints then 
                leftL (tagPunctuation "(") ^^ wordL (tagKeyword "requires") ^^ sepListL (wordL (tagKeyword "and")) cxsL ^^ rightL (tagPunctuation ")")
            else
                wordL (tagKeyword "when") ^^ sepListL (wordL (tagKeyword "and")) cxsL

    /// Layout constraints, taking TypeSimplificationInfo into account 
    and private layoutConstraintWithInfo denv env (tp, tpc) =
        let longConstraintPrefix l = layoutTyparRefWithInfo denv env tp ^^ WordL.colon ^^ l
        match tpc with 
        | TyparConstraint.CoercesTo(tpct, _) -> 
            [layoutTyparRefWithInfo denv env tp ^^ wordL (tagOperator ":>") --- layoutTypeWithInfo denv env tpct]

        | TyparConstraint.MayResolveMember(traitInfo, _) ->
            [layoutTraitWithInfo denv env traitInfo]

        | TyparConstraint.DefaultsTo(_, ty, _) ->
              if denv.showTyparDefaultConstraints then [wordL (tagKeyword "default") ^^ layoutTyparRefWithInfo denv env tp ^^ WordL.colon ^^ layoutTypeWithInfo denv env ty]
              else []

        | TyparConstraint.IsEnum(ty, _) ->
            if denv.shortConstraints then 
                [wordL (tagKeyword "enum")]
            else
                [longConstraintPrefix (layoutTypeAppWithInfoAndPrec denv env (wordL (tagKeyword "enum")) 2 true [ty])]

        | TyparConstraint.SupportsComparison _ ->
            if denv.shortConstraints then 
                [wordL (tagKeyword "comparison")]
            else
                [wordL (tagKeyword "comparison") |> longConstraintPrefix]

        | TyparConstraint.SupportsEquality _ ->
            if denv.shortConstraints then 
                [wordL (tagKeyword "equality")]
            else
                [wordL (tagKeyword "equality") |> longConstraintPrefix]

        | TyparConstraint.IsDelegate(aty, bty, _) ->
            if denv.shortConstraints then 
                [WordL.keywordDelegate]
            else
                [layoutTypeAppWithInfoAndPrec denv env (WordL.keywordDelegate) 2 true [aty;bty] |> longConstraintPrefix]

        | TyparConstraint.SupportsNull _ ->
            [wordL (tagKeyword "null") |> longConstraintPrefix]

        | TyparConstraint.IsNonNullableStruct _ ->
            if denv.shortConstraints then 
                [wordL (tagText "value type")]
            else
                [WordL.keywordStruct |> longConstraintPrefix]

        | TyparConstraint.IsUnmanaged _ ->
            if denv.shortConstraints then
                [wordL (tagKeyword "unmanaged")]
            else
                [wordL (tagKeyword "unmanaged") |> longConstraintPrefix]

        | TyparConstraint.IsReferenceType _ ->
            if denv.shortConstraints then 
                [wordL (tagText "reference type")]
            else
                [(wordL (tagKeyword "not") ^^ wordL(tagKeyword "struct")) |> longConstraintPrefix]

        | TyparConstraint.SimpleChoice(tys, _) ->
            [bracketL (sepListL (sepL (tagPunctuation "|")) (List.map (layoutTypeWithInfo denv env) tys)) |> longConstraintPrefix]

        | TyparConstraint.RequiresDefaultConstructor _ -> 
            if denv.shortConstraints then 
                [wordL (tagKeyword "default") ^^ wordL (tagKeyword "constructor")]
            else
                [bracketL (
                    wordL (tagKeyword "new") ^^
                    wordL (tagPunctuation ":") ^^
                    WordL.structUnit ^^ 
                    WordL.arrow ^^
                    (layoutTyparRefWithInfo denv env tp)) |> longConstraintPrefix]

    and private layoutTraitWithInfo denv env (TTrait(supportTys, nm, memFlags, argtys, rty, _, _)) =
        let nm = DemangleOperatorName nm
        if denv.shortConstraints then 
            WordL.keywordMember ^^ wordL (tagMember nm)
        else
            let rty = GetFSharpViewOfReturnType denv.g rty
            let stat = layoutMemberFlags memFlags
            let supportTys = ListSet.setify (typeEquiv denv.g) supportTys
            let supportTysL = 
                match supportTys with 
                | [ty] -> layoutTypeWithInfo denv env ty 
                | _ -> bracketL (layoutTypesWithInfoAndPrec denv env 2 (wordL (tagKeyword "or")) supportTys)

            let argtys = 
                if memFlags.IsInstance then 
                    match argtys with 
                    | [] | [_] -> [denv.g.unit_ty]
                    | _ :: rest -> rest
                else argtys

            let rtyL = layoutReturnType denv env rty

            let tyL =
                match argtys with 
                | [] -> rtyL
                | _ ->
                    let argtysL = layoutTypesWithInfoAndPrec denv env 2 (wordL (tagPunctuation "*")) argtys
                    argtysL --- wordL (tagPunctuation "->") --- rtyL

            supportTysL ^^ wordL (tagPunctuation ":")  --- bracketL (stat ++ wordL (tagMember nm) ^^ wordL (tagPunctuation ":") --- tyL)


    /// Layout a unit expression 
    and private layoutMeasure denv unt =
        let sortVars vs = vs |> List.sortBy (fun (v: Typar, _) -> v.DisplayName) 
        let sortCons cs = cs |> List.sortBy (fun (c: TyconRef, _) -> c.DisplayName) 
        let negvs, posvs = ListMeasureVarOccsWithNonZeroExponents unt |> sortVars |> List.partition (fun (_, e) -> SignRational e < 0)
        let negcs, poscs = ListMeasureConOccsWithNonZeroExponents denv.g false unt |> sortCons |> List.partition (fun (_, e) -> SignRational e < 0)
        let unparL uv = layoutTyparRef denv uv
        let unconL tc = layoutTyconRefImpl denv tc
        let rationalL e = wordL (tagNumericLiteral (RationalToString e))
        let measureToPowerL x e = if e = OneRational then x else x -- wordL (tagPunctuation "^") -- rationalL e
        let prefix = spaceListL (List.map (fun (v, e) -> measureToPowerL (unparL v) e) posvs @
                                 List.map (fun (c, e) -> measureToPowerL (unconL c) e) poscs)
        let postfix = spaceListL (List.map (fun (v, e) -> measureToPowerL (unparL v) (NegRational e)) negvs @
                                  List.map (fun (c, e) -> measureToPowerL (unconL c) (NegRational e)) negcs)
        match (negvs, negcs) with 
        | [], [] -> (match posvs, poscs with [], [] -> wordL (tagNumericLiteral "1") | _ -> prefix)
        | _ -> prefix ^^ sepL (tagPunctuation "/") ^^ (if List.length negvs + List.length negcs > 1 then sepL (tagPunctuation "(") ^^ postfix ^^ sepL (tagPunctuation ")") else postfix)

    /// Layout type arguments, either NAME<ty, ..., ty> or (ty, ..., ty) NAME *)
    and private layoutTypeAppWithInfoAndPrec denv env tcL prec prefix args =
        if prefix then 
            match args with
            | [] -> tcL
            | [arg] -> tcL ^^ sepL (tagPunctuation "<") ^^ (layoutTypeWithInfoAndPrec denv env 4 arg) ^^ rightL (tagPunctuation">")
            | args -> bracketIfL (prec <= 1) (tcL ^^ angleL (layoutTypesWithInfoAndPrec denv env 2 (sepL (tagPunctuation ",")) args))
        else
            match args with
            | [] -> tcL
            | [arg] -> layoutTypeWithInfoAndPrec denv env 2 arg ^^ tcL
            | args -> bracketIfL (prec <= 1) (bracketL (layoutTypesWithInfoAndPrec denv env 2 (sepL (tagPunctuation ",")) args) --- tcL)

    /// Layout a type, taking precedence into account to insert brackets where needed
    and layoutTypeWithInfoAndPrec denv env prec ty =

        match stripTyparEqns ty with 

        // Always prefer to format 'byref<ty, ByRefKind.In>' as 'inref<ty>'
        | ty when isInByrefTy denv.g ty && (match ty with TType_app (tc, _) when denv.g.inref_tcr.CanDeref && tyconRefEq denv.g tc denv.g.byref2_tcr -> true | _ -> false) ->
            layoutTypeWithInfoAndPrec denv env prec (mkInByrefTy denv.g (destByrefTy denv.g ty))

        // Always prefer to format 'byref<ty, ByRefKind.Out>' as 'outref<ty>'
        | ty when isOutByrefTy denv.g ty && (match ty with TType_app (tc, _) when denv.g.outref_tcr.CanDeref && tyconRefEq denv.g tc denv.g.byref2_tcr -> true | _ -> false) ->
            layoutTypeWithInfoAndPrec denv env prec (mkOutByrefTy denv.g (destByrefTy denv.g ty))

        // Always prefer to format 'byref<ty, ByRefKind.InOut>' as 'byref<ty>'
        | ty when isByrefTy denv.g ty && (match ty with TType_app (tc, _) when denv.g.byref_tcr.CanDeref && tyconRefEq denv.g tc denv.g.byref2_tcr -> true | _ -> false) ->
            layoutTypeWithInfoAndPrec denv env prec (mkByrefTy denv.g (destByrefTy denv.g ty))

        // Always prefer 'float' to 'float<1>'
        | TType_app (tc, args) when tc.IsMeasureableReprTycon && List.forall (isDimensionless denv.g) args ->
          layoutTypeWithInfoAndPrec denv env prec (reduceTyconRefMeasureableOrProvided denv.g tc args)

        // Layout a type application
        | TType_app (tc, args) ->
          let usePrefix =
              match denv.genericParameterStyle with
              | GenericParameterStyle.Implicit -> tc.IsPrefixDisplay
              | GenericParameterStyle.Prefix -> true
              | GenericParameterStyle.Suffix -> false
          layoutTypeAppWithInfoAndPrec denv env (layoutTyconRefImpl denv tc) prec usePrefix args

        | TType_ucase (UnionCaseRef(tc, _), args) ->
          let usePrefix =
              match denv.genericParameterStyle with
              | GenericParameterStyle.Implicit -> tc.IsPrefixDisplay
              | GenericParameterStyle.Prefix -> true
              | GenericParameterStyle.Suffix -> false
          layoutTypeAppWithInfoAndPrec denv env (layoutTyconRefImpl denv tc) prec usePrefix args

        // Layout a tuple type 
        | TType_anon (anonInfo, tys) ->
            let core = sepListL (rightL (tagPunctuation ";")) (List.map2 (fun nm ty -> wordL (tagField nm) ^^ rightL (tagPunctuation ":") ^^ layoutTypeWithInfoAndPrec denv env prec ty) (Array.toList anonInfo.SortedNames) tys)
            if evalAnonInfoIsStruct anonInfo then 
                WordL.keywordStruct --- braceBarL core
            else 
                braceBarL core

        // Layout a tuple type 
        | TType_tuple (tupInfo, t) ->
            if evalTupInfoIsStruct tupInfo then 
                WordL.keywordStruct --- bracketL (layoutTypesWithInfoAndPrec denv env 2 (wordL (tagPunctuation "*")) t)
            else 
                bracketIfL (prec <= 2) (layoutTypesWithInfoAndPrec denv env 2 (wordL (tagPunctuation "*")) t)

        // Layout a first-class generic type. 
        | TType_forall (tps, tau) ->
            let tauL = layoutTypeWithInfoAndPrec denv env prec tau
            match tps with 
            | [] -> tauL
            | [h] -> layoutTyparRefWithInfo denv env h ^^ rightL (tagPunctuation ".") --- tauL
            | (h :: t) -> spaceListL (List.map (layoutTyparRefWithInfo denv env) (h :: t)) ^^ rightL (tagPunctuation ".") --- tauL

        // Layout a function type. 
        | TType_fun _ ->
            let rec loop soFarL ty = 
              match stripTyparEqns ty with 
              | TType_fun (dty, rty) -> loop (soFarL --- (layoutTypeWithInfoAndPrec denv env 4 dty ^^ wordL (tagPunctuation "->"))) rty
              | rty -> soFarL --- layoutTypeWithInfoAndPrec denv env 5 rty
            bracketIfL (prec <= 4) (loop emptyL ty)

        // Layout a type variable . 
        | TType_var r ->
            layoutTyparRefWithInfo denv env r

        | TType_measure unt -> layoutMeasure denv unt

    /// Layout a list of types, separated with the given separator, either '*' or ','
    and private layoutTypesWithInfoAndPrec denv env prec sep typl = 
        sepListL sep (List.map (layoutTypeWithInfoAndPrec denv env prec) typl)

    and private layoutReturnType denv env rty = layoutTypeWithInfoAndPrec denv env 4 rty

    /// Layout a single type, taking TypeSimplificationInfo into account 
    and private layoutTypeWithInfo denv env ty = 
        layoutTypeWithInfoAndPrec denv env 5 ty

    and layoutType denv ty = 
        layoutTypeWithInfo denv SimplifyTypes.typeSimplificationInfo0 ty

    let layoutArgInfos denv env argInfos =

        // Format each argument, including its name and type 
        let argL (ty, argInfo: ArgReprInfo) = 
       
            // Detect an optional argument 
            let isOptionalArg = HasFSharpAttribute denv.g denv.g.attrib_OptionalArgumentAttribute argInfo.Attribs
            let isParamArray = HasFSharpAttribute denv.g denv.g.attrib_ParamArrayAttribute argInfo.Attribs
            match argInfo.Name, isOptionalArg, isParamArray, tryDestOptionTy denv.g ty with 
            // Layout an optional argument 
            | Some(id), true, _, ValueSome ty -> 
                leftL (tagPunctuation "?") ^^ sepL (tagParameter id.idText) ^^ SepL.colon ^^ layoutTypeWithInfoAndPrec denv env 2 ty 
            // Layout an unnamed argument 
            | None, _, _, _ -> 
                layoutTypeWithInfoAndPrec denv env 2 ty
            // Layout a named argument 
            | Some id, _, isParamArray, _ -> 
                let prefix =
                    if isParamArray then
                        layoutBuiltinAttribute denv denv.g.attrib_ParamArrayAttribute ^^ leftL (tagParameter id.idText)
                    else
                        leftL (tagParameter id.idText)
                prefix ^^ SepL.colon ^^ layoutTypeWithInfoAndPrec denv env 2 ty

        let allArgsL = 
            argInfos 
            |> List.mapSquared argL 
            |> List.map (sepListL (wordL (tagPunctuation "*")))
        allArgsL

    let layoutGenericParameterTypes denv env = 
      function
      | [] -> emptyL
      | genParamTys ->
        (wordL (tagPunctuation "<"))
        ^^
        (
          genParamTys
          |> List.map (layoutTypeWithInfoAndPrec denv env 4)
          |> sepListL (wordL (tagPunctuation ","))
        ) 
        ^^
        (wordL (tagPunctuation ">"))

    /// Layout a single type used as the type of a member or value 
    let layoutTopType denv env argInfos rty cxs =
        // Parenthesize the return type to match the topValInfo 
        let rtyL = layoutReturnType denv env rty
        let cxsL = layoutConstraintsWithInfo denv env cxs
        match argInfos with
        | [] -> rtyL --- cxsL
        | _ -> 
            let delimitReturnValue = tagPunctuation (if denv.useColonForReturnType then ":" else "->")
            let allArgsL = 
                layoutArgInfos denv env argInfos
                |> List.map (fun x -> (x ^^ wordL delimitReturnValue)) 
            (List.foldBack (---) allArgsL rtyL) --- cxsL

    /// Layout type parameters
    let layoutTyparDecls denv nmL prefix (typars: Typars) =
        let env = SimplifyTypes.typeSimplificationInfo0 
        let tpcs = typars |> List.collect (fun tp -> List.map (fun tpc -> tp, tpc) tp.Constraints) 
        match typars, tpcs with 
        | [], []  -> 
            nmL

        | [h], [] when not prefix -> 
            layoutTyparRefWithInfo denv env h --- nmL

        | _ -> 
            let tpcsL = layoutConstraintsWithInfo denv env tpcs
            let coreL = sepListL (sepL (tagPunctuation ",")) (List.map (layoutTyparRefWithInfo denv env) typars)
            (if prefix || not (isNil tpcs) then nmL ^^ angleL (coreL --- tpcsL) else bracketL coreL --- nmL)


    let layoutTyparConstraint denv (tp, tpc) = 
        match layoutConstraintWithInfo denv SimplifyTypes.typeSimplificationInfo0 (tp, tpc) with 
        | h :: _ -> h 
        | [] -> emptyL

    let prettyLayoutOfInstAndSig denv (typarInst, tys, retTy) =
        let (prettyTyparInst, prettyTys, prettyRetTy), cxs = PrettyTypes.PrettifyInstAndSig denv.g (typarInst, tys, retTy)
        let env = SimplifyTypes.CollectInfo true (prettyRetTy :: prettyTys) cxs
        let prettyTysL = List.map (layoutTypeWithInfo denv env) prettyTys
        let prettyRetTyL = layoutTopType denv env [[]] prettyRetTy []
        prettyTyparInst, (prettyTys, prettyRetTy), (prettyTysL, prettyRetTyL), layoutConstraintsWithInfo denv env env.postfixConstraints

    let prettyLayoutOfTopTypeInfoAux denv prettyArgInfos prettyRetTy cxs = 
        let env = SimplifyTypes.CollectInfo true (prettyRetTy :: List.collect (List.map fst) prettyArgInfos) cxs
        layoutTopType denv env prettyArgInfos prettyRetTy env.postfixConstraints

    // Oddly this is called in multiple places with argInfos=[] and denv.useColonForReturnType=true, as a complex
    // way of giving give ": ty"
    let prettyLayoutOfUncurriedSig denv typarInst argInfos retTy = 
        let (prettyTyparInst, prettyArgInfos, prettyRetTy), cxs = PrettyTypes.PrettifyInstAndUncurriedSig denv.g (typarInst, argInfos, retTy)
        prettyTyparInst, prettyLayoutOfTopTypeInfoAux denv [prettyArgInfos] prettyRetTy cxs

    let prettyLayoutOfCurriedMemberSig denv typarInst argInfos retTy parentTyparTys = 
        let (prettyTyparInst, parentTyparTys, argInfos, retTy), cxs = PrettyTypes.PrettifyInstAndCurriedSig denv.g (typarInst, parentTyparTys, argInfos, retTy)
        // Filter out the parent typars, which don't get shown in the member signature 
        let cxs = cxs |> List.filter (fun (tp, _) -> not (parentTyparTys |> List.exists (fun ty -> match tryDestTyparTy denv.g ty with ValueSome destTypar -> typarEq tp destTypar | _ -> false))) 
        prettyTyparInst, prettyLayoutOfTopTypeInfoAux denv argInfos retTy cxs


    let private prettyArgInfos denv allTyparInst =
        function 
        | [] -> [(denv.g.unit_ty, ValReprInfo.unnamedTopArg1)] 
        | infos -> infos |> List.map (map1Of2 (instType allTyparInst)) 

    // Layout: type spec - class, datatype, record, abbrev 
    let private prettyLayoutOfMemberSigCore denv memberToParentInst (typarInst, methTypars: Typars, argInfos, retTy) = 
        let niceMethodTypars, allTyparInst = 
            let methTyparNames = methTypars |> List.mapi (fun i tp -> if (PrettyTypes.NeedsPrettyTyparName tp) then sprintf "a%d" (List.length memberToParentInst + i) else tp.Name)
            PrettyTypes.NewPrettyTypars memberToParentInst methTypars methTyparNames

        let retTy = instType allTyparInst retTy
        let argInfos = argInfos |> List.map (prettyArgInfos denv allTyparInst) 

        // Also format dummy types corresponding to any type variables on the container to make sure they 
        // aren't chosen as names for displayed variables. 
        let memberParentTypars = List.map fst memberToParentInst
        let parentTyparTys = List.map (mkTyparTy >> instType allTyparInst) memberParentTypars
        let prettyTyparInst, layout = prettyLayoutOfCurriedMemberSig denv typarInst argInfos retTy parentTyparTys

        prettyTyparInst, niceMethodTypars, layout

    let prettyLayoutOfMemberType denv v typarInst argInfos retTy = 
        match PartitionValRefTypars denv.g v with
        | Some(_, _, memberMethodTypars, memberToParentInst, _) ->
            prettyLayoutOfMemberSigCore denv memberToParentInst (typarInst, memberMethodTypars, argInfos, retTy)
        | None -> 
            let prettyTyparInst, layout = prettyLayoutOfUncurriedSig denv typarInst (List.concat argInfos) retTy 
            prettyTyparInst, [], layout

    let prettyLayoutOfMemberSig denv (memberToParentInst, nm, methTypars, argInfos, retTy) = 
        let _, niceMethodTypars, tauL = prettyLayoutOfMemberSigCore denv memberToParentInst (emptyTyparInst, methTypars, argInfos, retTy)
        let nameL = 
            let nameL = DemangleOperatorNameAsLayout tagMember nm
            let nameL = if denv.showTyparBinding then layoutTyparDecls denv nameL true niceMethodTypars else nameL
            nameL
        nameL ^^ wordL (tagPunctuation ":") ^^ tauL

    /// layouts the elements of an unresolved overloaded method call:
    /// argInfos: unammed and named arguments
    /// retTy: return type
    /// genParamTy: generic parameter types
    let prettyLayoutsOfUnresolvedOverloading denv argInfos retTy genParamTys =
        let _niceMethodTypars, typarInst =
            let memberToParentInst = List.empty
            let typars = argInfos |> List.choose (function (TType.TType_var typar,_) -> Some typar | _ -> None)
            let methTyparNames = typars |> List.mapi (fun i tp -> if (PrettyTypes.NeedsPrettyTyparName tp) then sprintf "a%d" (List.length memberToParentInst + i) else tp.Name)
            PrettyTypes.NewPrettyTypars memberToParentInst typars methTyparNames
        let retTy = instType typarInst retTy
        let argInfos = prettyArgInfos denv typarInst argInfos
        let argInfos,retTy,genParamTys, cxs =
            // using 0, 1, 2 as discriminant for return, arguments and generic parameters
            // respectively, in order to easily retrieve each of the types with their
            // expected quality below.
            let typesWithDiscrimants =
                [
                    yield 0, retTy 
                    for ty,_ in argInfos do
                        yield 1, ty
                    for ty in genParamTys do
                        yield 2, ty
                ]
            let typesWithDiscrimants,typarsAndCxs = PrettyTypes.PrettifyDiscriminantAndTypePairs denv.g typesWithDiscrimants
            let retTy = typesWithDiscrimants |> List.find (function (0, _) -> true | _ -> false) |> snd
            let argInfos = 
                typesWithDiscrimants
                |> List.choose (function (1,ty) -> Some ty | _ -> None)
                |> List.map2 (fun (_, argInfo) tTy -> tTy, argInfo) argInfos
            let genParamTys = 
                typesWithDiscrimants
                |> List.choose (function (2,ty) -> Some ty | _ -> None)
              
            argInfos, retTy, genParamTys, typarsAndCxs

        let env = SimplifyTypes.CollectInfo true (List.collect (List.map fst) [argInfos]) cxs
        let cxsL = layoutConstraintsWithInfo denv env env.postfixConstraints

        (List.foldBack (---) (layoutArgInfos denv env [argInfos]) cxsL,
            layoutReturnType denv env retTy,
            layoutGenericParameterTypes denv env genParamTys)

    let prettyLayoutOfType denv ty = 
        let ty, cxs = PrettyTypes.PrettifyType denv.g ty
        let env = SimplifyTypes.CollectInfo true [ty] cxs
        let cxsL = layoutConstraintsWithInfo denv env env.postfixConstraints
        layoutTypeWithInfoAndPrec denv env 2 ty --- cxsL

    let prettyLayoutOfTypeNoConstraints denv ty = 
        let ty, _cxs = PrettyTypes.PrettifyType denv.g ty
        layoutTypeWithInfoAndPrec denv SimplifyTypes.typeSimplificationInfo0 5 ty 

    let layoutAssemblyName _denv (ty: TType) =
        ty.GetAssemblyName()

/// Printing TAST objects
module private PrintTastMemberOrVals =
    open PrintTypes 

    let mkInlineL denv (v: Val) nameL = 
        if v.MustInline && not denv.suppressInlineKeyword then 
            wordL (tagKeyword "inline") ++ nameL 
        else 
            nameL

    let private prettyLayoutOfMemberShortOption denv typarInst (v:Val) short =
        let v = mkLocalValRef v
        let membInfo = Option.get v.MemberInfo
        let stat = PrintTypes.layoutMemberFlags membInfo.MemberFlags
        let _tps, argInfos, rty, _ = GetTypeOfMemberInFSharpForm denv.g v
        
        if short then
            for argInfo in argInfos do
                for _,info in argInfo do
                    info.Attribs <- []
                    info.Name <- None

        let mkNameL niceMethodTypars tagFunction name =
            let nameL =
                DemangleOperatorNameAsLayout (tagFunction >> mkNav v.DefinitionRange) name
            let nameL =
                if denv.showMemberContainers then 
                    layoutTyconRefImpl denv v.MemberApparentEntity ^^ SepL.dot ^^ nameL
                else
                    nameL
            let nameL = if denv.showTyparBinding then layoutTyparDecls denv nameL true niceMethodTypars else nameL
            let nameL = layoutAccessibility denv v.Accessibility nameL
            nameL

        let prettyTyparInst, memberL =
            match membInfo.MemberFlags.MemberKind with
            | SynMemberKind.Member ->
                let prettyTyparInst, niceMethodTypars,tauL = prettyLayoutOfMemberType denv v typarInst argInfos rty
                let resL =
                    if short then tauL
                    else
                        let nameL = mkNameL niceMethodTypars tagMember v.LogicalName
                        let nameL = if short then nameL else mkInlineL denv v.Deref nameL
                        stat --- (nameL ^^ WordL.colon ^^ tauL)
                prettyTyparInst, resL

            | SynMemberKind.ClassConstructor
            | SynMemberKind.Constructor ->
                let prettyTyparInst, _, tauL = prettyLayoutOfMemberType denv v typarInst argInfos rty
                let resL = 
                    if short then tauL
                    else
                        let newL = layoutAccessibility denv v.Accessibility WordL.keywordNew
                        stat ++ newL ^^ wordL (tagPunctuation ":") ^^ tauL
                prettyTyparInst, resL

            | SynMemberKind.PropertyGetSet ->
                emptyTyparInst, stat

            | SynMemberKind.PropertyGet ->
                if isNil argInfos then
                    // use error recovery because intellisense on an incomplete file will show this
                    errorR(Error(FSComp.SR.tastInvalidFormForPropertyGetter(), v.Id.idRange))
                    let nameL = mkNameL [] tagProperty v.CoreDisplayName
                    let resL =
                        if short then nameL --- (WordL.keywordWith ^^ WordL.keywordGet)
                        else stat --- nameL --- (WordL.keywordWith ^^ WordL.keywordGet)
                    emptyTyparInst, resL
                else
                    let argInfos =
                        match argInfos with
                        | [[(ty, _)]] when isUnitTy denv.g ty -> []
                        | _ -> argInfos
                    let prettyTyparInst, niceMethodTypars,tauL = prettyLayoutOfMemberType denv v typarInst argInfos rty
                    let resL =
                        if short then
                            if isNil argInfos then tauL
                            else tauL --- (WordL.keywordWith ^^ WordL.keywordGet)
                        else
                            let nameL = mkNameL niceMethodTypars tagProperty v.CoreDisplayName
                            stat --- (nameL ^^ WordL.colon ^^ (if isNil argInfos then tauL else tauL --- (WordL.keywordWith ^^ WordL.keywordGet)))
                    prettyTyparInst, resL

            | SynMemberKind.PropertySet ->
                if argInfos.Length <> 1 || isNil argInfos.Head then
                    // use error recovery because intellisense on an incomplete file will show this
                    errorR(Error(FSComp.SR.tastInvalidFormForPropertySetter(), v.Id.idRange))
                    let nameL = mkNameL [] tagProperty v.CoreDisplayName
                    let resL = stat --- nameL --- (WordL.keywordWith ^^ WordL.keywordSet)
                    emptyTyparInst, resL
                else
                    let argInfos, valueInfo = List.frontAndBack argInfos.Head
                    let prettyTyparInst, niceMethodTypars, tauL = prettyLayoutOfMemberType denv v typarInst (if isNil argInfos then [] else [argInfos]) (fst valueInfo)
                    let resL =
                        if short then
                            (tauL --- (WordL.keywordWith ^^ WordL.keywordSet))
                        else
                            let nameL = mkNameL niceMethodTypars tagProperty v.CoreDisplayName
                            stat --- (nameL ^^ wordL (tagPunctuation ":") ^^ (tauL --- (WordL.keywordWith ^^ WordL.keywordSet)))
                    prettyTyparInst, resL

        prettyTyparInst, memberL

    let prettyLayoutOfMember denv typarInst (v:Val) = prettyLayoutOfMemberShortOption denv typarInst v false

    let prettyLayoutOfMemberNoInstShort denv v = 
        prettyLayoutOfMemberShortOption denv emptyTyparInst v true |> snd

    let layoutOfLiteralValue literalValue =
        let literalValue =
            match literalValue with
            | Const.Bool value -> if value then WordL.keywordTrue else WordL.keywordFalse
            | Const.SByte _
            | Const.Byte _
            | Const.Int16 _
            | Const.UInt16 _
            | Const.Int32 _
            | Const.UInt32 _
            | Const.Int64 _
            | Const.UInt64 _
            | Const.IntPtr _
            | Const.UIntPtr _
            | Const.Single _
            | Const.Double _
            | Const.Decimal _ -> literalValue.ToString() |> tagNumericLiteral |> wordL
            | Const.Char _
            | Const.String _ -> literalValue.ToString() |> tagStringLiteral |> wordL
            | Const.Unit
            | Const.Zero -> literalValue.ToString() |> tagText |> wordL
        WordL.equals ++ literalValue

    let private layoutNonMemberVal denv (tps, v: Val, tau, cxs) =
        let env = SimplifyTypes.CollectInfo true [tau] cxs
        let cxs = env.postfixConstraints
        let argInfos, rty = GetTopTauTypeInFSharpForm denv.g (arityOfVal v).ArgInfos tau v.Range
        let nameL =
            let isDiscard (str: string) = str.StartsWith("_")

            let tagF =
                if isForallFunctionTy denv.g v.Type && not (isDiscard v.DisplayName) then
                    if IsOperatorName v.DisplayName then
                        tagOperator
                    else
                        tagFunction
                elif not v.IsCompiledAsTopLevel && not(isDiscard v.DisplayName) then
                    tagLocal
                elif v.IsModuleBinding then
                    tagModuleBinding
                else
                    tagUnknownEntity

            v.DisplayName
            |> tagF
            |> mkNav v.DefinitionRange
            |> wordL 
        let nameL = layoutAccessibility denv v.Accessibility nameL
        let nameL = 
            if v.IsMutable && not denv.suppressMutableKeyword then 
                wordL (tagKeyword "mutable") ++ nameL 
                else 
                    nameL
        let nameL = mkInlineL denv v nameL

        let isOverGeneric = List.length (Zset.elements (freeInType CollectTyparsNoCaching tau).FreeTypars) < List.length tps // Bug: 1143 
        let isTyFunction = v.IsTypeFunction // Bug: 1143, and innerpoly tests 
        let typarBindingsL = 
            if isTyFunction || isOverGeneric || denv.showTyparBinding then 
                layoutTyparDecls denv nameL true tps 
            else nameL
        let valAndTypeL = (WordL.keywordVal ^^ typarBindingsL --- wordL (tagPunctuation ":")) --- layoutTopType denv env argInfos rty cxs
        let valAndTypeL =
            match denv.generatedValueLayout v with
            | None -> valAndTypeL
            | Some rhsL -> (valAndTypeL ++ wordL (tagPunctuation"=")) --- rhsL
        match v.LiteralValue with
        | Some literalValue -> valAndTypeL ++ layoutOfLiteralValue literalValue
        | None -> valAndTypeL

    let prettyLayoutOfValOrMember denv infoReader typarInst (vref: ValRef) =
        let prettyTyparInst, vL =
            match vref.MemberInfo with 
            | None ->
                let tps, tau = vref.TypeScheme

                // adjust the type in case this is the 'this' pointer stored in a reference cell
                let tau = StripSelfRefCell(denv.g, vref.BaseOrThisInfo, tau)

                let (prettyTyparInst, prettyTypars, prettyTauTy), cxs = PrettyTypes.PrettifyInstAndTyparsAndType denv.g (typarInst, tps, tau)
                let resL = layoutNonMemberVal denv (prettyTypars, vref.Deref, prettyTauTy, cxs)
                prettyTyparInst, resL
            | Some _ -> 
                prettyLayoutOfMember denv typarInst vref.Deref

        prettyTyparInst, 
            layoutAttribs denv true vref.Type TyparKind.Type vref.Attribs vL
            |> layoutXmlDocOfValRef denv infoReader vref

    let prettyLayoutOfValOrMemberNoInst denv infoReader v =
        prettyLayoutOfValOrMember denv infoReader emptyTyparInst v |> snd

let layoutTyparConstraint denv x = x |> PrintTypes.layoutTyparConstraint denv 

let outputType denv os x = x |> PrintTypes.layoutType denv |> bufferL os

let layoutType denv x = x |> PrintTypes.layoutType denv

let outputTypars denv nm os x = x |> PrintTypes.layoutTyparDecls denv (wordL nm) true |> bufferL os

let outputTyconRef denv os x = x |> PrintTypes.layoutTyconRefImpl denv |> bufferL os

let layoutTyconRef denv x = x |> PrintTypes.layoutTyconRefImpl denv

let layoutConst g ty c = PrintTypes.layoutConst g ty c

let prettyLayoutOfMemberSig denv x = x |> PrintTypes.prettyLayoutOfMemberSig denv 

let prettyLayoutOfUncurriedSig denv argInfos tau = PrintTypes.prettyLayoutOfUncurriedSig denv argInfos tau

let prettyLayoutsOfUnresolvedOverloading denv argInfos retTy genericParameters = PrintTypes.prettyLayoutsOfUnresolvedOverloading denv argInfos retTy genericParameters

//-------------------------------------------------------------------------

/// Printing info objects
module InfoMemberPrinting = 

    /// Format the arguments of a method to a buffer. 
    ///
    /// This uses somewhat "old fashioned" printf-style buffer printing.
    let layoutParamData denv (ParamData(isParamArray, _isInArg, _isOutArg, optArgInfo, _callerInfo, nmOpt, _reflArgInfo, pty)) =
        let isOptArg = optArgInfo.IsOptional
        match isParamArray, nmOpt, isOptArg, tryDestOptionTy denv.g pty with 
        // Layout an optional argument 
        | _, Some nm, true, ptyOpt -> 
            // detect parameter type, if ptyOpt is None - this is .NET style optional argument
            let pty = match ptyOpt with ValueSome x -> x | _ -> pty
            let idText =
                if denv.escapeKeywordNames && Lexhelp.Keywords.keywordNames |> List.contains nm.idText then
                    "``" + nm.idText + "``"
                else
                    nm.idText
            SepL.questionMark ^^
            wordL (tagParameter idText) ^^
            RightL.colon ^^
            PrintTypes.layoutType denv pty
        // Layout an unnamed argument 
        | _, None, _, _ -> 
            PrintTypes.layoutType denv pty
        // Layout a named argument 
        | true, Some nm, _, _ -> 
            let idText =
                if denv.escapeKeywordNames && Lexhelp.Keywords.keywordNames |> List.contains nm.idText then
                    "``" + nm.idText + "``"
                else
                    nm.idText
            layoutBuiltinAttribute denv denv.g.attrib_ParamArrayAttribute ^^
            wordL (tagParameter idText) ^^
            RightL.colon ^^
            PrintTypes.layoutType denv pty
        | false, Some nm, _, _ -> 
            let idText =
                if denv.escapeKeywordNames && Lexhelp.Keywords.keywordNames |> List.contains nm.idText then
                    "``" + nm.idText + "``"
                else
                    nm.idText
            wordL (tagParameter idText) ^^
            RightL.colon ^^
            PrintTypes.layoutType denv pty

    let formatParamDataToBuffer denv os pd = layoutParamData denv pd |> bufferL os
        
    /// Format a method info using "F# style".
    //
    // That is, this style:
    //          new: argName1: argType1 * ... * argNameN: argTypeN -> retType
    //          Method: argName1: argType1 * ... * argNameN: argTypeN -> retType
    let private layoutMethInfoFSharpStyleCore (infoReader: InfoReader) m denv (minfo: MethInfo) minst =
        let amap = infoReader.amap

        match minfo.ArbitraryValRef with
        | Some vref ->
            PrintTastMemberOrVals.prettyLayoutOfValOrMemberNoInst denv infoReader vref
        | None ->
            let layout = 
                if not minfo.IsConstructor && not minfo.IsInstance then WordL.keywordStatic
                else emptyL

            let layout =
                layout ^^ 
                (
                    if minfo.IsConstructor then
                        wordL (tagKeyword "new")
                    else
                        WordL.keywordMember ^^
                        PrintTypes.layoutTyparDecls denv (minfo.LogicalName |> tagMethod |> wordL) true minfo.FormalMethodTypars
                ) ^^
                WordL.colon
            let layout = layoutXmlDocOfMethInfo denv infoReader minfo layout
            let paramDatas = minfo.GetParamDatas(amap, m, minst)
            let layout =
                layout ^^
                    if List.forall isNil paramDatas then
                        WordL.structUnit
                    else
                        sepListL WordL.arrow (List.map ((List.map (layoutParamData denv)) >> sepListL WordL.star) paramDatas)
            let retTy = minfo.GetFSharpReturnTy(amap, m, minst)
            layout ^^
            WordL.arrow ^^
            PrintTypes.layoutType denv retTy

    /// Format a method info using "half C# style".
    //
    // That is, this style:
    //          Container(argName1: argType1, ..., argNameN: argTypeN) : retType
    //          Container.Method(argName1: argType1, ..., argNameN: argTypeN) : retType
    let private layoutMethInfoCSharpStyle amap m denv (minfo: MethInfo) minst =
        let retTy = if minfo.IsConstructor then minfo.ApparentEnclosingType else minfo.GetFSharpReturnTy(amap, m, minst) 
        let layout = 
            if minfo.IsExtensionMember then
                LeftL.leftParen ^^ wordL (tagKeyword (FSComp.SR.typeInfoExtension())) ^^ RightL.rightParen
            else emptyL
        let layout = 
            layout ^^
                if isAppTy minfo.TcGlobals minfo.ApparentEnclosingAppType then
                    let tcref = minfo.ApparentEnclosingTyconRef 
                    PrintTypes.layoutTyconRefImpl denv tcref
                else
                    emptyL
        let layout = 
            layout ^^
                if minfo.IsConstructor then
                    SepL.leftParen
                else
                    SepL.dot ^^
                    PrintTypes.layoutTyparDecls denv (wordL (tagMethod minfo.LogicalName)) true minfo.FormalMethodTypars ^^
                    SepL.leftParen

        let paramDatas = minfo.GetParamDatas (amap, m, minst)
        let layout = layout ^^ sepListL RightL.comma ((List.concat >> List.map (layoutParamData denv)) paramDatas)
        layout ^^ RightL.rightParen ^^ WordL.colon ^^ PrintTypes.layoutType denv retTy


    // Prettify an ILMethInfo
    let prettifyILMethInfo (amap: Import.ImportMap) m (minfo: MethInfo) typarInst ilMethInfo = 
        let (ILMethInfo(_, apparentTy, dty, mdef, _)) = ilMethInfo
        let (prettyTyparInst, prettyTys), _ = PrettyTypes.PrettifyInstAndTypes amap.g (typarInst, (apparentTy :: minfo.FormalMethodInst))
        match prettyTys with
        | prettyApparentTy :: prettyFormalMethInst ->
            let prettyMethInfo = 
                match dty with 
                | None -> MethInfo.CreateILMeth (amap, m, prettyApparentTy, mdef)
                | Some declaringTyconRef -> MethInfo.CreateILExtensionMeth(amap, m, prettyApparentTy, declaringTyconRef, minfo.ExtensionMemberPriorityOption, mdef)
            prettyTyparInst, prettyMethInfo, prettyFormalMethInst
        | _ -> failwith "prettifyILMethInfo - prettyTys empty"

    /// Format a method to a buffer using "standalone" display style. 
    /// For example, these are the formats used when printing signatures of methods that have not been overridden,
    /// and the format used when showing the individual member in QuickInfo and DeclarationInfo.
    /// The formats differ between .NET/provided methods and F# methods. Surprisingly people don't really seem 
    /// to notice this, or they find it helpful. It feels that moving from this position should not be done lightly.
    //
    // For F# members:
    //          new: unit -> retType
    //          new: argName1: argType1 * ... * argNameN: argTypeN -> retType
    //          Container.Method: unit -> retType
    //          Container.Method: argName1: argType1 * ... * argNameN: argTypeN -> retType
    //
    // For F# extension members:
    //          ApparentContainer.Method: argName1: argType1 * ... * argNameN: argTypeN -> retType
    //
    // For C# and provided members:
    //          Container(argName1: argType1, ..., argNameN: argTypeN) : retType
    //          Container.Method(argName1: argType1, ..., argNameN: argTypeN) : retType
    //
    // For C# extension members:
    //          ApparentContainer.Method(argName1: argType1, ..., argNameN: argTypeN) : retType
    let prettyLayoutOfMethInfoFreeStyle (infoReader: InfoReader) m denv typarInst methInfo =
        let amap = infoReader.amap

        match methInfo with 
        | DefaultStructCtor _ -> 
            let prettyTyparInst, _ = PrettyTypes.PrettifyInst amap.g typarInst 
            prettyTyparInst, PrintTypes.layoutTyconRefImpl denv methInfo.ApparentEnclosingTyconRef ^^ wordL (tagPunctuation "()")
        | FSMeth(_, _, vref, _) -> 
            let prettyTyparInst, resL = PrintTastMemberOrVals.prettyLayoutOfValOrMember { denv with showMemberContainers=true } infoReader typarInst vref
            prettyTyparInst, resL
        | ILMeth(_, ilminfo, _) -> 
            let prettyTyparInst, prettyMethInfo, minst = prettifyILMethInfo amap m methInfo typarInst ilminfo
            let resL = layoutMethInfoCSharpStyle amap m denv prettyMethInfo minst
            prettyTyparInst, resL
#if !NO_EXTENSIONTYPING
        | ProvidedMeth _ -> 
            let prettyTyparInst, _ = PrettyTypes.PrettifyInst amap.g typarInst 
            prettyTyparInst, layoutMethInfoCSharpStyle amap m denv methInfo methInfo.FormalMethodInst
    #endif

    let prettyLayoutOfPropInfoFreeStyle g amap m denv (pinfo: PropInfo) =
        let rty = pinfo.GetPropertyType(amap, m) 
        let rty = if pinfo.IsIndexer then mkRefTupledTy g (pinfo.GetParamTypes(amap, m)) --> rty else rty 
        let rty, _ = PrettyTypes.PrettifyType g rty
        let tagProp =
            match pinfo.ArbitraryValRef with
            | None -> tagProperty
            | Some vref -> tagProperty >> mkNav vref.DefinitionRange
        let nameL = DemangleOperatorNameAsLayout tagProp pinfo.PropertyName
        let getterSetter =
            match pinfo.HasGetter, pinfo.HasSetter with
            | (true, false) ->
                wordL (tagKeyword "with") ^^ wordL (tagText "get")
            | (false, true) ->
                wordL (tagKeyword "with") ^^ wordL (tagText "set")
            | (true, true) ->
                wordL (tagKeyword "with") ^^ wordL (tagText "get, set")
            | (false, false) ->
                emptyL

        wordL (tagText (FSComp.SR.typeInfoProperty())) ^^
        layoutTyconRef denv pinfo.ApparentEnclosingTyconRef ^^
        SepL.dot ^^
        nameL ^^
        RightL.colon ^^
        layoutType denv rty ^^
        getterSetter

    let formatMethInfoToBufferFreeStyle amap m denv os (minfo: MethInfo) = 
        let _, resL = prettyLayoutOfMethInfoFreeStyle amap m denv emptyTyparInst minfo 
        resL |> bufferL os

    /// Format a method to a layout (actually just containing a string) using "free style" (aka "standalone"). 
    let layoutMethInfoFSharpStyle amap m denv (minfo: MethInfo) =
        layoutMethInfoFSharpStyleCore amap m denv minfo minfo.FormalMethodInst

//-------------------------------------------------------------------------

/// Printing TAST objects
module private TastDefinitionPrinting = 
    open PrintTypes

    let layoutExtensionMember denv infoReader (vref: ValRef) =
        let tycon = vref.MemberApparentEntity.Deref
        let nameL = tagMethod tycon.DisplayName |> mkNav vref.DefinitionRange |> wordL
        let nameL = layoutAccessibility denv tycon.Accessibility nameL // "type-accessibility"
        let tps =
            match PartitionValTyparsForApparentEnclosingType denv.g vref.Deref with
              | Some(_, memberParentTypars, _, _, _) -> memberParentTypars
              | None -> []
        let lhsL = WordL.keywordType ^^ layoutTyparDecls denv nameL tycon.IsPrefixDisplay tps
        let memberL = PrintTastMemberOrVals.prettyLayoutOfValOrMemberNoInst denv infoReader vref
        (lhsL ^^ WordL.keywordWith) @@-- memberL

    let layoutExtensionMembers denv infoReader vs =
        aboveListL (List.map (layoutExtensionMember denv infoReader) vs) 

    let layoutRecdField addAccess denv infoReader (enclosingTcref: TyconRef) (fld: RecdField) =
        let lhs =
            tagRecordField fld.Name
            |> mkNav fld.DefinitionRange
            |> wordL
        let lhs = (if addAccess then layoutAccessibility denv fld.Accessibility lhs else lhs)
        let lhs = if fld.IsMutable then wordL (tagKeyword "mutable") --- lhs else lhs
        let fieldL = (lhs ^^ RightL.colon) --- layoutType denv fld.FormalType

        // The enclosing TyconRef might be a union and we can only get fields from union cases, so we need ignore unions here.
        if not enclosingTcref.IsUnionTycon then
            layoutXmlDocOfRecdFieldRef denv infoReader (RecdFieldRef(enclosingTcref, fld.Id.idText)) fieldL
        else
            fieldL

    let layoutUnionOrExceptionField denv infoReader isGenerated enclosingTcref i (fld: RecdField) =
        if isGenerated i fld then layoutTypeWithInfoAndPrec denv SimplifyTypes.typeSimplificationInfo0 2 fld.FormalType
        else layoutRecdField false denv infoReader enclosingTcref fld
    
    let isGeneratedUnionCaseField pos (f: RecdField) = 
        if pos < 0 then f.Name = "Item"
        else f.Name = "Item" + string (pos + 1)

    let isGeneratedExceptionField pos (f: RecdField) = 
        f.Name = "Data" + (string pos)

    let layoutUnionCaseFields denv infoReader isUnionCase enclosingTcref fields = 
        match fields with
        | [f] when isUnionCase -> layoutUnionOrExceptionField denv infoReader isGeneratedUnionCaseField enclosingTcref -1 f
        | _ -> 
            let isGenerated = if isUnionCase then isGeneratedUnionCaseField else isGeneratedExceptionField
            sepListL (wordL (tagPunctuation "*")) (List.mapi (layoutUnionOrExceptionField denv infoReader isGenerated enclosingTcref) fields)

    let layoutUnionCase denv infoReader prefixL enclosingTcref (ucase: UnionCase) =
        let nmL = DemangleOperatorNameAsLayout (tagUnionCase >> mkNav ucase.DefinitionRange) ucase.Id.idText
        //let nmL = layoutAccessibility denv ucase.Accessibility nmL
        let caseL =
            match ucase.RecdFields with
            | []     -> (prefixL ^^ nmL)
            | fields -> (prefixL ^^ nmL ^^ WordL.keywordOf) --- layoutUnionCaseFields denv infoReader true enclosingTcref fields
        layoutXmlDocOfUnionCaseRef denv infoReader (UnionCaseRef(enclosingTcref, ucase.Id.idText)) caseL

    let layoutUnionCases denv infoReader enclosingTcref ucases =
        let prefixL = WordL.bar // See bug://2964 - always prefix in case preceded by accessibility modifier
        List.map (layoutUnionCase denv infoReader prefixL enclosingTcref) ucases

    /// When to force a break? "type tyname = <HERE> repn"
    /// When repn is class or datatype constructors (not single one).
    let breakTypeDefnEqn repr =
        match repr with 
        | TFSharpObjectRepr _ -> true
        | TUnionRepr r -> not (isNilOrSingleton r.CasesTable.UnionCasesAsList)
        | TRecdRepr _ -> true
        | TAsmRepr _ 
        | TILObjectRepr _
        | TMeasureableRepr _ 
#if !NO_EXTENSIONTYPING
        | TProvidedTypeExtensionPoint _
        | TProvidedNamespaceExtensionPoint _
#endif
        | TNoRepr -> false
      
    let private layoutILFieldInfo denv amap m (e: ILFieldInfo) =
        let staticL = if e.IsStatic then WordL.keywordStatic else emptyL
        let nameL = wordL (tagField (adjustILName e.FieldName))
        let typL = layoutType denv (e.FieldType(amap, m))
        staticL ^^ WordL.keywordVal ^^ nameL ^^ WordL.colon ^^ typL

    let private layoutEventInfo denv (infoReader: InfoReader) m (e: EventInfo) =
        let amap = infoReader.amap

        let staticL = if e.IsStatic then WordL.keywordStatic else emptyL

        let eventTag =
            let tag =
                e.EventName
                |> adjustILName
                |> tagEvent

            match e.ArbitraryValRef with
            | Some vref ->
                tag |> mkNav vref.DefinitionRange
            | None ->
                tag

        let nameL = eventTag |> wordL
        let typL = layoutType denv (e.GetDelegateType(amap, m))
        let overallL = staticL ^^ WordL.keywordMember ^^ nameL ^^ WordL.colon ^^ typL
        layoutXmlDocOfEventInfo denv infoReader e overallL

    let private layoutPropInfo denv (infoReader: InfoReader) m (p: PropInfo) =
        let amap = infoReader.amap

        match p.ArbitraryValRef with
        | Some vref ->
            PrintTastMemberOrVals.prettyLayoutOfValOrMemberNoInst denv infoReader vref
        | None ->

            let modifierAndMember =
                if p.IsStatic then
                    WordL.keywordStatic ^^ WordL.keywordMember
                else
                    WordL.keywordMember
        
            let propTag =
                p.PropertyName
                |> adjustILName
                |> tagProperty

            let nameL = propTag |> wordL
            
            let typL = layoutType denv (p.GetPropertyType(amap, m)) // shouldn't happen
            let overallL = modifierAndMember ^^ nameL ^^ WordL.colon ^^ typL
            layoutXmlDocOfPropInfo denv infoReader p overallL

    let layoutTyconRef (denv: DisplayEnv) (infoReader: InfoReader) ad m simplified typewordL (tcref: TyconRef) =
        let g = denv.g
        let tycon = tcref.Deref
        let _, ty = generalizeTyconRef tcref 
        let start, name =
            let n = tycon.DisplayName
            if isStructTy g ty then
                if denv.printVerboseSignatures then
                    Some "struct", tagStruct n
                else
                    None, tagStruct n
            elif isInterfaceTy g ty then
                if denv.printVerboseSignatures then
                    Some "interface", tagInterface n
                else
                    None, tagInterface n
            elif isClassTy g ty then
                if denv.printVerboseSignatures then
                    (if simplified then None else Some "class"), tagClass n
                else
                    None, tagClass n
            else
                None, tagUnknownType n
        let name = mkNav tycon.DefinitionRange name
        let nameL = layoutAccessibility denv tycon.Accessibility (wordL name)
        let denv = denv.AddAccessibility tycon.Accessibility 
        let lhsL =
            let tps = tycon.TyparsNoRange
            let tpsL = layoutTyparDecls denv nameL tycon.IsPrefixDisplay tps
            typewordL ^^ tpsL

        let start = Option.map tagKeyword start
        let amap = infoReader.amap
        let sortKey (v: MethInfo) = 
            (not v.IsConstructor,
                not v.IsInstance, // instance first
                v.DisplayName, // sort by name 
                List.sum v.NumArgs, // sort by #curried
                v.NumArgs.Length)     // sort by arity 

        let shouldShow (valRef: ValRef option) =
            match valRef with
            | None -> true
            | Some(vr) ->
                (denv.showObsoleteMembers || not (CheckFSharpAttributesForObsolete denv.g vr.Attribs)) &&
                (denv.showHiddenMembers || not (CheckFSharpAttributesForHidden denv.g vr.Attribs))
                
        let isDiscard (name: string) = name.StartsWith("_")

        let ctors =
            GetIntrinsicConstructorInfosOfType infoReader m ty
            |> List.filter (fun v -> not v.IsClassConstructor && shouldShow v.ArbitraryValRef)

        let iimplsLs =
            if suppressInheritanceAndInterfacesForTyInSimplifiedDisplays g amap m ty then 
                []
            elif isRecdTy g ty || isUnionTy g ty || tycon.IsStructOrEnumTycon then
                tycon.ImmediateInterfacesOfFSharpTycon
                |> List.filter (fun (_, compgen, _) -> not compgen)
                |> List.map (fun (ty, _, _) -> wordL (tagKeyword "interface") --- layoutType denv ty)
            else 
                GetImmediateInterfacesOfType SkipUnrefInterfaces.Yes g amap m ty
                |> List.map (fun ity -> wordL (tagKeyword (if isInterfaceTy g ty then "inherit" else "interface")) --- layoutType denv ity)

        let props =
            GetImmediateIntrinsicPropInfosOfType (None, ad) g amap m ty
            |> List.filter (fun v -> shouldShow v.ArbitraryValRef)

        let events = 
            infoReader.GetEventInfosOfType(None, ad, m, ty)
            |> List.filter (fun v -> shouldShow v.ArbitraryValRef && typeEquiv g ty v.ApparentEnclosingType)

        let impliedNames = 
            try 
                Set.ofList [ for p in props do 
                                if p.HasGetter then yield p.GetterMethod.DisplayName
                                if p.HasSetter then yield p.SetterMethod.DisplayName
                                for e in events do 
                                yield e.AddMethod.DisplayName 
                                yield e.RemoveMethod.DisplayName ]
            with _ -> Set.empty

        let meths =
            GetImmediateIntrinsicMethInfosOfType (None, ad) g amap m ty
            |> List.filter (fun m ->
                not m.IsClassConstructor &&
                not m.IsConstructor &&
                shouldShow m.ArbitraryValRef &&
                not (impliedNames.Contains m.DisplayName) &&
                not (m.DisplayName.Split('.') |> Array.exists (fun part -> isDiscard part)))

        let ctorLs =
            if denv.shrinkOverloads then
                ctors 
                |> shrinkOverloads (InfoMemberPrinting.layoutMethInfoFSharpStyle infoReader m denv) (fun _ xL -> xL) 
            else
                ctors
                |> List.map (fun ctor -> InfoMemberPrinting.layoutMethInfoFSharpStyle infoReader m denv ctor)

        let methLs = 
            meths
            |> List.groupBy (fun md -> md.DisplayName)
            |> List.collect (fun (_, group) ->
                if denv.shrinkOverloads then
                    shrinkOverloads (InfoMemberPrinting.layoutMethInfoFSharpStyle infoReader m denv) (fun x xL -> (sortKey x, xL)) group
                else
                    group
                    |> List.sortBy sortKey
                    |> List.map (fun methinfo -> ((not methinfo.IsConstructor, methinfo.IsInstance, methinfo.DisplayName, List.sum methinfo.NumArgs, methinfo.NumArgs.Length), InfoMemberPrinting.layoutMethInfoFSharpStyle infoReader m denv methinfo)))
            |> List.sortBy fst
            |> List.map snd

        let fieldLs =
            infoReader.GetILFieldInfosOfType (None, ad, m, ty)
            |> List.filter (fun fld -> not (isDiscard fld.FieldName))
            |> List.map (fun x -> (true, x.IsStatic, x.FieldName, 0, 0), layoutILFieldInfo denv amap m x)
            |> List.sortBy fst
            |> List.map snd

        let staticValsLs =
            if isRecdTy g ty then
                []
            else
                tycon.TrueFieldsAsList
                |> List.filter (fun f -> f.IsStatic && not (isDiscard f.Name))
                |> List.map (fun f -> WordL.keywordStatic ^^ WordL.keywordVal ^^ layoutRecdField true denv infoReader tcref f)

        let instanceValsLs =
            if isRecdTy g ty then
                []
            else
                tycon.TrueInstanceFieldsAsList
                |> List.filter (fun f -> not (isDiscard f.Name))
                |> List.map (fun f -> WordL.keywordVal ^^ layoutRecdField true denv infoReader tcref f)
    
        let propLs =
            props
            |> List.map (fun x -> (true, x.IsStatic, x.PropertyName, 0, 0), layoutPropInfo denv infoReader m x)
            |> List.sortBy fst
            |> List.map snd

        let eventLs = 
            events
            |> List.map (fun x -> (true, x.IsStatic, x.EventName, 0, 0), layoutEventInfo denv infoReader m x)
            |> List.sortBy fst
            |> List.map snd

        let nestedTypeLs =
#if !NO_EXTENSIONTYPING
            match tryTcrefOfAppTy g ty with
            | ValueSome tcref ->
                match tcref.TypeReprInfo with 
                | TProvidedTypeExtensionPoint info ->
                    [ 
                        for nestedType in info.ProvidedType.PApplyArray((fun sty -> sty.GetNestedTypes()), "GetNestedTypes", m) do 
                            yield nestedType.PUntaint((fun t -> t.IsClass, t.Name), m)
                    ] 
                    |> List.sortBy snd
                    |> List.map (fun (isClass, t) -> WordL.keywordNested ^^ WordL.keywordType ^^ wordL ((if isClass then tagClass else tagStruct) t))
                | _ ->
                    []
            | ValueNone ->
#endif
                []

        let inherits = 
            if suppressInheritanceAndInterfacesForTyInSimplifiedDisplays g amap m ty then 
                []
            else
                match GetSuperTypeOfType g amap m ty with 
                | Some super when not (isObjTy g super) && not (isValueTypeTy g super) ->
                    [wordL (tagKeyword "inherit") ^^ (layoutType denv super)]
                | _ -> []

        let erasedL = 
#if SHOW_ERASURE
            match tryTcrefOfAppTy g ty with
            | ValueSome tcref ->
                if tcref.IsProvidedErasedTycon then 
                    [ wordL ""; wordL (FSComp.SR.erasedTo()) ^^ PrintIL.layoutILTypeRef { denv with shortTypeNames = false } tcref.CompiledRepresentationForNamedType; wordL "" ] 
                else
                    []
            | None ->
#endif
                []

        let decls = inherits @ iimplsLs @ ctorLs @ methLs @ fieldLs @ propLs @ eventLs @ instanceValsLs @ staticValsLs @ nestedTypeLs @ erasedL
        let declsL =
            decls
            |> applyMaxMembers denv.maxMembers
            |> aboveListL
            |> fun declsL ->
                match start with
                | Some s -> (wordL s @@-- declsL) @@ wordL (tagKeyword "end")
                | None -> declsL

        let addMembersAsWithEnd reprL =
            if isNil decls then
                reprL
            else
                let memberLs = applyMaxMembers denv.maxMembers decls
                if simplified then
                    reprL @@-- aboveListL memberLs
                else
                    reprL @@ (WordL.keywordWith @@-- aboveListL memberLs) @@ WordL.keywordEnd

        let reprL = 
            let repr = tycon.TypeReprInfo
            match repr with 
            | TRecdRepr _ 
            | TUnionRepr _
            | TFSharpObjectRepr _ 
            | TAsmRepr _ 
            | TMeasureableRepr _
            | TILObjectRepr _ -> 
                let brk = not (isNil decls) || breakTypeDefnEqn repr
                let rhsL = 
                    let addReprAccessL l = layoutAccessibility denv tycon.TypeReprAccessibility l 
                    let denv = denv.AddAccessibility tycon.TypeReprAccessibility 
                    match repr with 
                    | TRecdRepr _ ->
                        let recdFieldRefL fld = layoutRecdField false denv infoReader tcref fld

                        let recdL =
                            tycon.TrueFieldsAsList
                            |> List.map recdFieldRefL
                            |> applyMaxMembers denv.maxMembers
                            |> aboveListL
                            |> braceL

                        Some (addMembersAsWithEnd (addReprAccessL recdL))

                    | TUnionRepr _ -> 
                        let layoutUnionCases =
                            tycon.UnionCasesAsList
                            |> layoutUnionCases denv infoReader tcref
                            |> applyMaxMembers denv.maxMembers
                            |> aboveListL
                        Some (addMembersAsWithEnd (addReprAccessL layoutUnionCases))
                  
                    | TFSharpObjectRepr r ->
                        match r.fsobjmodel_kind with
                        | TTyconDelegate (TSlotSig(_, _, _, _, paraml, rty)) ->
                            let rty = GetFSharpViewOfReturnType denv.g rty
                            Some (WordL.keywordDelegate ^^ WordL.keywordOf --- layoutTopType denv SimplifyTypes.typeSimplificationInfo0 (paraml |> List.mapSquared (fun sp -> (sp.Type, ValReprInfo.unnamedTopArg1))) rty [])
                        | _ ->
                            match r.fsobjmodel_kind with
                            | TTyconEnum -> 
                                tycon.TrueFieldsAsList
                                |> List.map (fun f -> 
                                    match f.LiteralValue with 
                                    | None -> emptyL
                                    | Some c -> WordL.bar ^^
                                                wordL (tagField f.Name) ^^
                                                WordL.equals ^^ 
                                                layoutConst denv.g ty c)
                                |> aboveListL
                                |> Some
                            | _ ->
                                let allDecls = inherits @ iimplsLs @ ctorLs @ instanceValsLs @ methLs @ propLs @ eventLs @ staticValsLs
                                if isNil allDecls then
                                    None
                                else
                                    let allDecls = applyMaxMembers denv.maxMembers allDecls
                                    let emptyMeasure = match tycon.TypeOrMeasureKind with TyparKind.Measure -> isNil allDecls | _ -> false
                                    if emptyMeasure then None else 
                                    let declsL = aboveListL allDecls
                                    let declsL = match start with Some s -> (wordL s @@-- declsL) @@ wordL (tagKeyword "end") | None -> declsL
                                    Some declsL

                    | TAsmRepr _ -> 
                        Some (wordL (tagText "(# \"<Common IL Type Omitted>\" #)"))

                    | TMeasureableRepr ty ->
                        Some (layoutType denv ty)

                    | TILObjectRepr _ ->
                        if tycon.ILTyconRawMetadata.IsEnum then
                            infoReader.GetILFieldInfosOfType (None, ad, m, ty) 
                            |> List.filter (fun x -> x.FieldName <> "value__")
                            |> List.map (fun x -> PrintIL.layoutILEnumDefParts x.FieldName x.LiteralValue)
                            |> applyMaxMembers denv.maxMembers
                            |> aboveListL
                            |> Some
                        else
                            Some declsL

                    | _ -> None

                let brk = match tycon.TypeReprInfo with | TILObjectRepr _ -> true | _ -> brk
                match rhsL with 
                | None -> lhsL
                | Some rhsL -> 
                    if brk then 
                        (lhsL ^^ WordL.equals) @@-- rhsL 
                    else 
                        (lhsL ^^ WordL.equals) --- rhsL

            | _ -> 
                match tycon.TypeAbbrev with
                | None   -> 
                    addMembersAsWithEnd (lhsL ^^ WordL.equals)
                | Some a -> 
                    (lhsL ^^ WordL.equals) --- (layoutType { denv with shortTypeNames = false } a)

        let attribsL = layoutAttribs denv false ty tycon.TypeOrMeasureKind tycon.Attribs reprL
        layoutXmlDocOfEntityRef denv infoReader tcref attribsL

    // Layout: exception definition
    let layoutExnDefn denv infoReader (exncref: EntityRef) =
        let exnc = exncref.Deref
        let nm = exnc.LogicalName
        let nmL = wordL (tagClass nm)
        let nmL = layoutAccessibility denv exnc.TypeReprAccessibility nmL
        let exnL = wordL (tagKeyword "exception") ^^ nmL // need to tack on the Exception at the right of the name for goto definition
        let reprL = 
            match exnc.ExceptionInfo with 
            | TExnAbbrevRepr ecref -> WordL.equals --- layoutTyconRefImpl denv ecref
            | TExnAsmRepr _ -> WordL.equals --- wordL (tagText "(# ... #)")
            | TExnNone -> emptyL
            | TExnFresh r -> 
                match r.TrueFieldsAsList with
                | [] -> emptyL
                | r -> WordL.keywordOf --- layoutUnionCaseFields denv infoReader false exncref r

        let overallL = exnL ^^ reprL
        layoutXmlDocOfEntityRef denv infoReader exncref overallL

    // Layout: module spec 

    let layoutTyconDefns denv infoReader ad m (tycons: Tycon list) =
        match tycons with 
        | [] -> emptyL
        | [h] when h.IsExceptionDecl -> layoutExnDefn denv infoReader (mkLocalEntityRef h)
        | h :: t -> 
            let x = layoutTyconRef denv infoReader ad m false WordL.keywordType (mkLocalEntityRef h)
            let xs = List.map (mkLocalEntityRef >> layoutTyconRef denv infoReader ad m false (wordL (tagKeyword "and"))) t
            aboveListL (x :: xs)

    let rec layoutModuleOrNamespace (denv: DisplayEnv) (infoReader: InfoReader) ad m isFirstTopLevel (mspec: ModuleOrNamespace) =
        let rec fullPath (mspec: ModuleOrNamespace) acc =
            if mspec.IsNamespace then
                match mspec.ModuleOrNamespaceType.ModuleAndNamespaceDefinitions |> List.tryHead with
                | Some next when next.IsNamespace ->
                    fullPath next (acc @ [next.DemangledModuleOrNamespaceName])
                | _ ->
                    acc, mspec
            else
                acc, mspec

        let outerPath = mspec.CompilationPath.AccessPath

        let path, mspec = fullPath mspec [mspec.DemangledModuleOrNamespaceName]

        let denv =
            let outerPath = outerPath |> List.map fst
            denv.AddOpenPath (outerPath @ path)

        let headerL =
            if mspec.IsNamespace then
                // This is a container namespace. We print the header when we get to the first concrete module.
                wordL (tagKeyword "namespace") ^^ sepListL SepL.dot (List.map (tagNamespace >> wordL) path)
            else
                // This is a module 
                let nmL = 
                    match path with
                    | [nm] -> wordL (tagModule nm)
                    | _ ->
                        let nm = path |> List.last
                        let innerPath = path.[..path.Length - 2]
                        sepListL SepL.dot (List.map (tagNamespace >> wordL) innerPath) ^^ SepL.dot ^^ wordL (tagModule nm)
                // Check if its an outer module or a nested module
                if (outerPath |> List.forall (fun (_, istype) -> istype = Namespace)) then 
                    // Check if this is an outer module with no namespace
                    if isNil outerPath then 
                        // If so print a "module" declaration
                        (wordL (tagKeyword "module") ^^ nmL)
                    else 
                        if mspec.ModuleOrNamespaceType.AllEntities |> Seq.isEmpty && mspec.ModuleOrNamespaceType.AllValsAndMembers |> Seq.isEmpty then
                            (wordL (tagKeyword "module") ^^ nmL ^^ WordL.equals ^^ wordL (tagKeyword "begin") ^^ wordL (tagKeyword "end"))
                        else
                            // Otherwise this is an outer module contained immediately in a namespace
                            // We already printed the namespace declaration earlier. So just print the 
                            // module now.
                            (wordL (tagKeyword "module") ^^ nmL ^^ WordL.equals)
                else
                    if mspec.ModuleOrNamespaceType.AllEntities |> Seq.isEmpty && mspec.ModuleOrNamespaceType.AllValsAndMembers |> Seq.isEmpty then
                        (wordL (tagKeyword "module") ^^ nmL ^^ WordL.equals ^^ wordL (tagKeyword "begin") ^^ wordL (tagKeyword "end"))
                    else
                        // OK, this is a nested module
                        (wordL (tagKeyword "module") ^^ nmL ^^ WordL.equals)

        let headerL =
            PrintTypes.layoutAttribs denv false (generalizedTyconRef(mkLocalEntityRef mspec)) mspec.TypeOrMeasureKind mspec.Attribs headerL

        let shouldShow (v: Val) =
            (denv.showObsoleteMembers || not (CheckFSharpAttributesForObsolete denv.g v.Attribs)) &&
            (denv.showHiddenMembers || not (CheckFSharpAttributesForHidden denv.g v.Attribs))

        let entityLs =
            if mspec.IsNamespace then []
            else
                mspec.ModuleOrNamespaceType.AllEntities
                |> QueueList.toList
                |> List.map (fun entity -> layoutEntityRef denv infoReader ad m (mkLocalEntityRef entity))
            
        let valLs =
            if mspec.IsNamespace then []
            else
                mspec.ModuleOrNamespaceType.AllValsAndMembers
                |> QueueList.toList
                |> List.filter shouldShow
                |> List.sortBy (fun v -> v.DisplayName)
                |> List.map (mkLocalValRef >> PrintTastMemberOrVals.prettyLayoutOfValOrMemberNoInst denv infoReader)

        if List.isEmpty entityLs && List.isEmpty valLs then
            headerL
        else
            let entitiesL =
                entityLs
                |> aboveListL

            let valsL =
                valLs
                |> aboveListL

            if isFirstTopLevel then
                aboveListL
                    [
                        headerL
                        entitiesL
                        valsL
                    ]
            else
                headerL @@---- entitiesL @@ valsL

    and layoutEntityRef (denv: DisplayEnv) (infoReader: InfoReader) ad m (eref: EntityRef) =
        if eref.IsModuleOrNamespace then
            layoutModuleOrNamespace denv infoReader ad m false eref.Deref
            |> layoutXmlDocOfEntityRef denv infoReader eref
        elif eref.IsExceptionDecl then
            layoutExnDefn denv infoReader eref
        else
            layoutTyconRef denv infoReader ad m true WordL.keywordType eref

//--------------------------------------------------------------------------

module private InferredSigPrinting = 
    open PrintTypes

    /// Layout the inferred signature of a compilation unit
    let layoutInferredSigOfModuleExpr showHeader denv infoReader ad m expr =

        let rec isConcreteNamespace x = 
            match x with 
            | TMDefRec(_, tycons, mbinds, _) -> 
                not (isNil tycons) || (mbinds |> List.exists (function ModuleOrNamespaceBinding.Binding _ -> true | ModuleOrNamespaceBinding.Module(x, _) -> not x.IsNamespace))
            | TMDefLet _ -> true
            | TMDefDo _ -> true
            | TMDefs defs -> defs |> List.exists isConcreteNamespace 
            | TMAbstract(ModuleOrNamespaceExprWithSig(_, def, _)) -> isConcreteNamespace def

        let rec imexprLP denv (ModuleOrNamespaceExprWithSig(_, def, _)) = imdefL denv def

        and imexprL denv (ModuleOrNamespaceExprWithSig(mty, def, m)) = imexprLP denv (ModuleOrNamespaceExprWithSig(mty, def, m))

        and imdefsL denv x = aboveListL (x |> List.map (imdefL denv))

        and imdefL denv x = 
            let filterVal (v: Val) = not v.IsCompilerGenerated && Option.isNone v.MemberInfo
            let filterExtMem (v: Val) = v.IsExtensionMember

            match x with 
            | TMDefRec(_, tycons, mbinds, _) -> 
                TastDefinitionPrinting.layoutTyconDefns denv infoReader ad m tycons @@ 
                (mbinds 
                    |> List.choose (function ModuleOrNamespaceBinding.Binding bind -> Some bind | _ -> None) 
                    |> valsOfBinds 
                    |> List.filter filterExtMem
                    |> List.map mkLocalValRef
                    |> TastDefinitionPrinting.layoutExtensionMembers denv infoReader) @@

                (mbinds 
                    |> List.choose (function ModuleOrNamespaceBinding.Binding bind -> Some bind | _ -> None) 
                    |> valsOfBinds 
                    |> List.filter filterVal
                    |> List.map mkLocalValRef
                    |> List.map (PrintTastMemberOrVals.prettyLayoutOfValOrMemberNoInst denv infoReader)
                    |> aboveListL) @@

                (mbinds 
                    |> List.choose (function ModuleOrNamespaceBinding.Module (mspec, def) -> Some (mspec, def) | _ -> None) 
                    |> List.map (imbindL denv) 
                    |> aboveListL)

            | TMDefLet(bind, _) -> 
                ([bind.Var] 
                    |> List.filter filterVal 
                    |> List.map mkLocalValRef
                    |> List.map (PrintTastMemberOrVals.prettyLayoutOfValOrMemberNoInst denv infoReader) 
                    |> aboveListL)

            | TMDefs defs -> imdefsL denv defs

            | TMDefDo _ -> emptyL

            | TMAbstract mexpr -> imexprLP denv mexpr

        and imbindL denv (mspec, def) = 
            let nm = mspec.DemangledModuleOrNamespaceName
            let innerPath = (fullCompPathOfModuleOrNamespace mspec).AccessPath
            let outerPath = mspec.CompilationPath.AccessPath

            let denv = denv.AddOpenPath (List.map fst innerPath) 
            if mspec.IsNamespace then
                let basic = imdefL denv def
                let basicL =
                    // Check if this namespace contains anything interesting
                    if isConcreteNamespace def then
                        // This is a container namespace. We print the header when we get to the first concrete module.
                        let headerL =
                            wordL (tagKeyword "namespace") ^^ sepListL SepL.dot (List.map (fst >> tagNamespace >> wordL) innerPath)
                        headerL @@-- basic
                    else
                        // This is a namespace that only contains namespaces. Skip the header
                        basic
                // NOTE: explicitly not calling `layoutXmlDoc` here, because even though
                // `ModuleOrNamespace` has a field for XmlDoc, it is never present at the parser
                // level.  This should be changed if the parser/spec changes.
                basicL
            else
                // This is a module 
                let nmL = layoutAccessibility denv mspec.Accessibility (wordL (tagModule nm))
                let denv = denv.AddAccessibility mspec.Accessibility
                let basic = imdefL denv def
                let basicL =
                    // Check if its an outer module or a nested module
                    if (outerPath |> List.forall (fun (_, istype) -> istype = Namespace) ) then
                        // OK, this is an outer module
                        if showHeader then
                            // OK, we're not in F# Interactive
                            // Check if this is an outer module with no namespace
                            if isNil outerPath then
                                // If so print a "module" declaration
                                (wordL (tagKeyword "module") ^^ nmL) @@ basic
                            else
                                // Otherwise this is an outer module contained immediately in a namespace
                                // We already printed the namespace declaration earlier. So just print the
                                // module now.
                                ((wordL (tagKeyword"module") ^^ nmL ^^ WordL.equals ^^ wordL (tagKeyword "begin")) @@-- basic) @@ WordL.keywordEnd
                        else
                            // OK, we're in F# Interactive, presumably the implicit module for each interaction.
                            basic
                    else
                        // OK, this is a nested module
                        ((wordL (tagKeyword "module") ^^ nmL ^^ WordL.equals ^^ wordL (tagKeyword"begin")) @@-- basic) @@ WordL.keywordEnd
                layoutXmlDoc denv mspec.XmlDoc basicL
        imexprL denv expr

//--------------------------------------------------------------------------

module private PrintData = 
    open PrintTypes

    /// Nice printing of a subset of expressions, e.g. for refutations in pattern matching
    let rec dataExprL denv expr = dataExprWrapL denv false expr

    and private dataExprWrapL denv isAtomic expr =
        match expr with
        | Expr.Const (c, _, ty) -> 
            if isEnumTy denv.g ty then 
                wordL (tagKeyword "enum") ^^ angleL (layoutType denv ty) ^^ bracketL (layoutConst denv.g ty c)
            else
                layoutConst denv.g ty c

        | Expr.Val (v, _, _) -> wordL (tagLocal v.DisplayName)

        | Expr.Link rX -> dataExprWrapL denv isAtomic (!rX)

        | Expr.Op (TOp.UnionCase (c), _, args, _) -> 
            if denv.g.unionCaseRefEq c denv.g.nil_ucref then wordL (tagPunctuation "[]")
            elif denv.g.unionCaseRefEq c denv.g.cons_ucref then 
                let rec strip = function (Expr.Op (TOp.UnionCase _, _, [h;t], _)) -> h :: strip t | _ -> []
                listL (dataExprL denv) (strip expr)
            elif isNil args then 
                wordL (tagUnionCase c.CaseName)
            else 
                (wordL (tagUnionCase c.CaseName) ++ bracketL (commaListL (dataExprsL denv args)))
            
        | Expr.Op (TOp.ExnConstr (c), _, args, _) -> (wordL (tagMethod c.LogicalName) ++ bracketL (commaListL (dataExprsL denv args)))

        | Expr.Op (TOp.Tuple _, _, xs, _) -> tupleL (dataExprsL denv xs)

        | Expr.Op (TOp.Recd (_, tc), _, xs, _) -> 
            let fields = tc.TrueInstanceFieldsAsList
            let lay fs x = (wordL (tagRecordField fs.rfield_id.idText) ^^ sepL (tagPunctuation "=")) --- (dataExprL denv x)
            leftL (tagPunctuation "{") ^^ semiListL (List.map2 lay fields xs) ^^ rightL (tagPunctuation "}")

        | Expr.Op (TOp.ValFieldGet (RecdFieldRef.RecdFieldRef (tcref, name)), _, _, _) ->
            (layoutTyconRefImpl denv tcref) ^^ sepL (tagPunctuation ".") ^^ wordL (tagField name)

        | Expr.Op (TOp.Array, [_], xs, _) -> leftL (tagPunctuation "[|") ^^ semiListL (dataExprsL denv xs) ^^ RightL.rightBracketBar

        | _ -> wordL (tagPunctuation "?")

    and private dataExprsL denv xs = List.map (dataExprL denv) xs

let dataExprL denv expr = PrintData.dataExprL denv expr

//--------------------------------------------------------------------------
// Print Signatures/Types - output functions 
//-------------------------------------------------------------------------- 

let outputValOrMember denv infoReader os x = x |> PrintTastMemberOrVals.prettyLayoutOfValOrMemberNoInst denv infoReader |> bufferL os

let stringValOrMember denv infoReader x = x |> PrintTastMemberOrVals.prettyLayoutOfValOrMemberNoInst denv infoReader |> showL

/// Print members with a qualification showing the type they are contained in 
let layoutQualifiedValOrMember denv infoReader typarInst v = PrintTastMemberOrVals.prettyLayoutOfValOrMember { denv with showMemberContainers=true; } infoReader typarInst v

let outputQualifiedValOrMember denv infoReader os v = outputValOrMember { denv with showMemberContainers=true; } infoReader os v

let outputQualifiedValSpec denv infoReader os v = outputQualifiedValOrMember denv infoReader os v

let stringOfQualifiedValOrMember denv infoReader v = PrintTastMemberOrVals.prettyLayoutOfValOrMemberNoInst { denv with showMemberContainers=true; } infoReader v |> showL

/// Convert a MethInfo to a string
let formatMethInfoToBufferFreeStyle infoReader m denv buf d = InfoMemberPrinting.formatMethInfoToBufferFreeStyle infoReader m denv buf d

let prettyLayoutOfMethInfoFreeStyle infoReader m denv typarInst minfo = InfoMemberPrinting.prettyLayoutOfMethInfoFreeStyle infoReader m denv typarInst minfo

/// Convert a PropInfo to a string
let prettyLayoutOfPropInfoFreeStyle g amap m denv d = InfoMemberPrinting.prettyLayoutOfPropInfoFreeStyle g amap m denv d

/// Convert a MethInfo to a string
let stringOfMethInfo infoReader m denv d = bufs (fun buf -> InfoMemberPrinting.formatMethInfoToBufferFreeStyle infoReader m denv buf d)

/// Convert a ParamData to a string
let stringOfParamData denv paramData = bufs (fun buf -> InfoMemberPrinting.formatParamDataToBuffer denv buf paramData)

let layoutOfParamData denv paramData = InfoMemberPrinting.layoutParamData denv paramData

let layoutExnDef denv infoReader x = x |> TastDefinitionPrinting.layoutExnDefn denv infoReader

let stringOfTyparConstraints denv x = x |> PrintTypes.layoutConstraintsWithInfo denv SimplifyTypes.typeSimplificationInfo0 |> showL

let layoutTycon denv infoReader ad m (* width *) x = TastDefinitionPrinting.layoutTyconRef denv infoReader ad m true WordL.keywordType (mkLocalEntityRef x) (* |> Display.squashTo width *)

let layoutEntityRef denv infoReader ad m x = TastDefinitionPrinting.layoutEntityRef denv infoReader ad m x

let layoutUnionCases denv infoReader enclosingTcref x = x |> TastDefinitionPrinting.layoutUnionCaseFields denv infoReader true enclosingTcref

/// Pass negative number as pos in case of single cased discriminated unions
let isGeneratedUnionCaseField pos f = TastDefinitionPrinting.isGeneratedUnionCaseField pos f

let isGeneratedExceptionField pos f = TastDefinitionPrinting.isGeneratedExceptionField pos f

let stringOfTyparConstraint denv tpc = stringOfTyparConstraints denv [tpc]

let stringOfTy denv x = x |> PrintTypes.layoutType denv |> showL

let prettyLayoutOfType denv x = x |> PrintTypes.prettyLayoutOfType denv

let prettyLayoutOfTypeNoCx denv x = x |> PrintTypes.prettyLayoutOfTypeNoConstraints denv

let prettyStringOfTy denv x = x |> PrintTypes.prettyLayoutOfType denv |> showL

let prettyStringOfTyNoCx denv x = x |> PrintTypes.prettyLayoutOfTypeNoConstraints denv |> showL

let stringOfRecdField denv infoReader enclosingTcref x = x |> TastDefinitionPrinting.layoutRecdField false denv infoReader enclosingTcref |> showL

let stringOfUnionCase denv infoReader enclosingTcref x = x |> TastDefinitionPrinting.layoutUnionCase denv infoReader WordL.bar enclosingTcref |> showL

let stringOfExnDef denv infoReader x = x |> TastDefinitionPrinting.layoutExnDefn denv infoReader |> showL

let stringOfFSAttrib denv x = x |> PrintTypes.layoutAttrib denv |> squareAngleL |> showL

let stringOfILAttrib denv x = x |> PrintTypes.layoutILAttrib denv |> squareAngleL |> showL

let layoutInferredSigOfModuleExpr showHeader denv infoReader ad m expr = InferredSigPrinting.layoutInferredSigOfModuleExpr showHeader denv infoReader ad m expr 

let prettyLayoutOfValOrMember denv infoReader typarInst v = PrintTastMemberOrVals.prettyLayoutOfValOrMember denv infoReader typarInst v  

let prettyLayoutOfValOrMemberNoInst denv infoReader v = PrintTastMemberOrVals.prettyLayoutOfValOrMemberNoInst denv infoReader v

let prettyLayoutOfMemberNoInstShort denv v = PrintTastMemberOrVals.prettyLayoutOfMemberNoInstShort denv v 

let prettyLayoutOfInstAndSig denv x = PrintTypes.prettyLayoutOfInstAndSig denv x

/// Generate text for comparing two types.
///
/// If the output text is different without showing constraints and/or imperative type variable 
/// annotations and/or fully qualifying paths then don't show them! 
let minimalStringsOfTwoTypes denv t1 t2= 
    let (t1, t2), tpcs = PrettyTypes.PrettifyTypePair denv.g (t1, t2)
    // try denv + no type annotations 
    let attempt1 = 
        let denv = { denv with showImperativeTyparAnnotations=false; showConstraintTyparAnnotations=false }
        let min1 = stringOfTy denv t1
        let min2 = stringOfTy denv t2
        if min1 <> min2 then Some (min1, min2, "") else None
    match attempt1 with 
    | Some res -> res
    | None -> 
    // try denv + no type annotations + show full paths
    let attempt2 = 
        let denv = { denv with showImperativeTyparAnnotations=false; showConstraintTyparAnnotations=false }.SetOpenPaths []
        let min1 = stringOfTy denv t1
        let min2 = stringOfTy denv t2
        if min1 <> min2 then Some (min1, min2, "") else None
        // try denv 
    match attempt2 with 
    | Some res -> res
    | None -> 
    let attempt3 = 
        let min1 = stringOfTy denv t1
        let min2 = stringOfTy denv t2
        if min1 <> min2 then Some (min1, min2, stringOfTyparConstraints denv tpcs) else None
    match attempt3 with 
    | Some res -> res 
    | None -> 
    let attempt4 = 
        // try denv + show full paths + static parameters
        let denv = denv.SetOpenPaths []
        let denv = { denv with includeStaticParametersInTypeNames=true }
        let min1 = stringOfTy denv t1
        let min2 = stringOfTy denv t2
        if min1 <> min2 then Some (min1, min2, stringOfTyparConstraints denv tpcs) else None
    match attempt4 with
    | Some res -> res
    | None ->
        // https://github.com/Microsoft/visualfsharp/issues/2561
        // still identical, we better (try to) show assembly qualified name to disambiguate
        let denv = denv.SetOpenPaths []
        let denv = { denv with includeStaticParametersInTypeNames=true }
        let makeName t =
            let assemblyName = PrintTypes.layoutAssemblyName denv t |> function | null | "" -> "" | name -> sprintf " (%s)" name
            sprintf "%s%s" (stringOfTy denv t1) assemblyName

        (makeName t1, makeName t2, stringOfTyparConstraints denv tpcs)
    
// Note: Always show imperative annotations when comparing value signatures 
let minimalStringsOfTwoValues denv infoReader v1 v2= 
    let denvMin = { denv with showImperativeTyparAnnotations=true; showConstraintTyparAnnotations=false }
    let min1 = bufs (fun buf -> outputQualifiedValOrMember denvMin infoReader buf v1)
    let min2 = bufs (fun buf -> outputQualifiedValOrMember denvMin infoReader buf v2) 
    if min1 <> min2 then 
        (min1, min2) 
    else
        let denvMax = { denv with showImperativeTyparAnnotations=true; showConstraintTyparAnnotations=true }
        let max1 = bufs (fun buf -> outputQualifiedValOrMember denvMax infoReader buf v1)
        let max2 = bufs (fun buf -> outputQualifiedValOrMember denvMax infoReader buf v2) 
        max1, max2
    
let minimalStringOfType denv ty = 
    let ty, _cxs = PrettyTypes.PrettifyType denv.g ty
    let denvMin = { denv with showImperativeTyparAnnotations=false; showConstraintTyparAnnotations=false }
    showL (PrintTypes.layoutTypeWithInfoAndPrec denvMin SimplifyTypes.typeSimplificationInfo0 2 ty)

