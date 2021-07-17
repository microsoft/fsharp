// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

//----------------------------------------------------------------------------
// Open up the compiler as an incremental service for parsing,
// type checking and intellisense-like environment-reporting.
//--------------------------------------------------------------------------

namespace FSharp.Compiler.EditorServices

open System.Collections.Immutable
open Internal.Utilities.Library  
open Internal.Utilities.Library.Extras
open FSharp.Compiler 
open FSharp.Compiler.AbstractIL.Diagnostics 
open FSharp.Compiler.AccessibilityLogic
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.EditorServices
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Infos
open FSharp.Compiler.InfoReader
open FSharp.Compiler.NameResolution
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.SymbolHelpers
open FSharp.Compiler.Syntax.PrettyNaming
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Layout
open FSharp.Compiler.Text.LayoutRender
open FSharp.Compiler.Text.TaggedText
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TypedTreeOps

/// A single data tip display element
[<RequireQualifiedAccess>]
type ToolTipElementData = 
    { MainDescription:  TaggedText[]
      XmlDoc: FSharpXmlDoc
      TypeMapping: TaggedText[] list
      Remarks: TaggedText[] option
      ParamName : string option }

    static member Create(layout, xml, ?typeMapping, ?paramName, ?remarks) = 
        { MainDescription=layout; XmlDoc=xml; TypeMapping=defaultArg typeMapping []; ParamName=paramName; Remarks=remarks }

/// A single data tip display element
[<RequireQualifiedAccess>]
type ToolTipElement = 
    | None

    /// A single type, method, etc with comment. May represent a method overload group.
    | Group of elements: ToolTipElementData list

    /// An error occurred formatting this element
    | CompositionError of errorText: string

    static member Single(layout, xml, ?typeMapping, ?paramName, ?remarks) = 
        Group [ ToolTipElementData.Create(layout, xml, ?typeMapping=typeMapping, ?paramName=paramName, ?remarks=remarks) ]

/// Information for building a data tip box.
type ToolTipText = 
    /// A list of data tip elements to display.
    | ToolTipText of ToolTipElement list  

[<RequireQualifiedAccess>]
type CompletionItemKind =
    | Field
    | Property
    | Method of isExtension : bool
    | Event
    | Argument
    | CustomOperation
    | Other

type UnresolvedSymbol =
    { FullName: string
      DisplayName: string
      Namespace: string[] }

type CompletionItem =
    { ItemWithInst: ItemWithInst
      Kind: CompletionItemKind
      IsOwnMember: bool
      MinorPriority: int
      Type: TyconRef option
      Unresolved: UnresolvedSymbol option }
    member x.Item = x.ItemWithInst.Item

[<AutoOpen>]
module DeclarationListHelpers =
    let mutable ToolTipFault  = None

    /// Generate the structured tooltip for a method info
    let FormatOverloadsToList (infoReader: InfoReader) m denv (item: ItemWithInst) minfos : ToolTipElement = 
        ToolTipFault |> Option.iter (fun msg -> 
           let exn = Error((0, msg), range.Zero)
           let ph = PhasedDiagnostic.Create(exn, BuildPhase.TypeCheck)
           simulateError ph)
        
        let layouts = 
            [ for minfo in minfos -> 
                let prettyTyparInst, layout = NicePrint.prettyLayoutOfMethInfoFreeStyle infoReader m denv item.TyparInst minfo
                let xml = GetXmlCommentForMethInfoItem infoReader m item.Item minfo
                let tpsL = FormatTyparMapping denv prettyTyparInst
                let layout = LayoutRender.toArray layout
                let tpsL = List.map LayoutRender.toArray tpsL
                ToolTipElementData.Create(layout, xml, tpsL) ]
 
        ToolTipElement.Group layouts
        
    let CompletionItemDisplayPartialEquality g = 
        let itemComparer = ItemDisplayPartialEquality g
  
        { new IPartialEqualityComparer<CompletionItem> with
            member x.InEqualityRelation item = itemComparer.InEqualityRelation item.Item
            member x.Equals(item1, item2) = itemComparer.Equals(item1.Item, item2.Item)
            member x.GetHashCode item = itemComparer.GetHashCode(item.Item) }

    /// Remove all duplicate items
    let RemoveDuplicateCompletionItems g items =     
        if isNil items then items else
        items |> IPartialEqualityComparer.partialDistinctBy (CompletionItemDisplayPartialEquality g) 

    /// Filter types that are explicitly suppressed from the IntelliSense (such as uppercase "FSharpList", "Option", etc.)
    let RemoveExplicitlySuppressedCompletionItems (g: TcGlobals) (items: CompletionItem list) = 
        items |> List.filter (fun item -> not (IsExplicitlySuppressed g item.Item))

    // Remove items containing the same module references
    let RemoveDuplicateModuleRefs modrefs  = 
        modrefs |> IPartialEqualityComparer.partialDistinctBy 
                      { new IPartialEqualityComparer<ModuleOrNamespaceRef> with
                          member x.InEqualityRelation _ = true
                          member x.Equals(item1, item2) = (fullDisplayTextOfModRef item1 = fullDisplayTextOfModRef item2)
                          member x.GetHashCode item = hash item.Stamp  }

    let OutputFullName isListItem ppF fnF r = 
      // Only display full names in quick info, not declaration lists or method lists
      if not isListItem then 
        match ppF r with 
        | None -> emptyL
        | Some _ -> wordL (tagText (FSComp.SR.typeInfoFullName())) ^^ RightL.colon ^^ (fnF r)
      else emptyL

    let pubpathOfValRef (v: ValRef) = v.PublicPath        

    let pubpathOfTyconRef (x: TyconRef) = x.PublicPath

    /// Output the quick info information of a language item
    let rec FormatItemDescriptionToToolTipElement isListItem (infoReader: InfoReader) m denv (item: ItemWithInst) = 
        let g = infoReader.g
        let amap = infoReader.amap
        let denv = SimplerDisplayEnv denv 
        let xml = GetXmlCommentForItem infoReader m item.Item
        match item.Item with
        | Item.ImplicitOp(_, { contents = Some(TraitConstraintSln.FSMethSln(_, vref, _, _)) }) -> 
            // operator with solution
            FormatItemDescriptionToToolTipElement isListItem infoReader m denv { item with Item = Item.Value vref }

        | Item.Value vref | Item.CustomBuilder (_, vref) ->            
            let prettyTyparInst, resL = NicePrint.layoutQualifiedValOrMember denv infoReader item.TyparInst vref
            let remarks = OutputFullName isListItem pubpathOfValRef fullDisplayTextOfValRefAsLayout vref
            let tpsL = FormatTyparMapping denv prettyTyparInst
            let tpsL = List.map LayoutRender.toArray tpsL
            let resL = LayoutRender.toArray resL
            let remarks = LayoutRender.toArray remarks
            ToolTipElement.Single(resL, xml, tpsL, remarks=remarks)

        // Union tags (constructors)
        | Item.UnionCase(ucinfo, _) -> 
            let uc = ucinfo.UnionCase 
            let rty = generalizedTyconRef ucinfo.TyconRef
            let recd = uc.RecdFields 
            let layout = 
                wordL (tagText (FSComp.SR.typeInfoUnionCase())) ^^
                NicePrint.layoutTyconRef denv ucinfo.TyconRef ^^
                sepL (tagPunctuation ".") ^^
                wordL (tagUnionCase (DecompileOpName uc.Id.idText) |> mkNav uc.DefinitionRange) ^^
                RightL.colon ^^
                (if List.isEmpty recd then emptyL else NicePrint.layoutUnionCases denv infoReader ucinfo.TyconRef recd ^^ WordL.arrow) ^^
                NicePrint.layoutType denv rty
            let layout = LayoutRender.toArray layout
            ToolTipElement.Single (layout, xml)

        // Active pattern tag inside the declaration (result)             
        | Item.ActivePatternResult(apinfo, ty, idx, _) ->
            let items = apinfo.ActiveTags
            let layout = 
                wordL (tagText ((FSComp.SR.typeInfoActivePatternResult()))) ^^
                wordL (tagActivePatternResult (List.item idx items) |> mkNav apinfo.Range) ^^
                RightL.colon ^^
                NicePrint.layoutType denv ty
            let layout = LayoutRender.toArray layout
            ToolTipElement.Single (layout, xml)

        // Active pattern tags 
        | Item.ActivePatternCase apref -> 
            let v = apref.ActivePatternVal
            // Format the type parameters to get e.g. ('a -> 'a) rather than ('?1234 -> '?1234)
            let tau = v.TauType
            // REVIEW: use _cxs here
            let (prettyTyparInst, ptau), _cxs = PrettyTypes.PrettifyInstAndType denv.g (item.TyparInst, tau)
            let remarks = OutputFullName isListItem pubpathOfValRef fullDisplayTextOfValRefAsLayout v
            let layout =
                wordL (tagText (FSComp.SR.typeInfoActiveRecognizer())) ^^
                wordL (tagActivePatternCase apref.Name |> mkNav v.DefinitionRange) ^^
                RightL.colon ^^
                NicePrint.layoutType denv ptau

            let tpsL = FormatTyparMapping denv prettyTyparInst

            let layout = LayoutRender.toArray layout
            let tpsL = List.map LayoutRender.toArray tpsL
            let remarks = LayoutRender.toArray remarks
            ToolTipElement.Single (layout, xml, tpsL, remarks=remarks)

        // F# exception names
        | Item.ExnCase ecref -> 
            let layout = NicePrint.layoutExnDef denv infoReader ecref
            let remarks = OutputFullName isListItem pubpathOfTyconRef fullDisplayTextOfExnRefAsLayout ecref
            let layout = LayoutRender.toArray layout
            let remarks = LayoutRender.toArray remarks
            ToolTipElement.Single (layout, xml, remarks=remarks)

        | Item.RecdField rfinfo when rfinfo.TyconRef.IsExceptionDecl ->
            let ty, _ = PrettyTypes.PrettifyType g rfinfo.FieldType
            let id = rfinfo.RecdField.Id
            let layout =
                wordL (tagText (FSComp.SR.typeInfoArgument())) ^^
                wordL (tagParameter id.idText) ^^
                RightL.colon ^^
                NicePrint.layoutType denv ty
            let layout = LayoutRender.toArray layout
            ToolTipElement.Single (layout, xml, paramName = id.idText)

        // F# record field names
        | Item.RecdField rfinfo ->
            let rfield = rfinfo.RecdField
            let ty, _cxs = PrettyTypes.PrettifyType g rfinfo.FieldType
            let layout = 
                NicePrint.layoutTyconRef denv rfinfo.TyconRef ^^
                SepL.dot ^^
                wordL (tagRecordField (DecompileOpName rfield.Name) |> mkNav rfield.DefinitionRange) ^^
                RightL.colon ^^
                NicePrint.layoutType denv ty ^^
                (
                    match rfinfo.LiteralValue with
                    | None -> emptyL
                    | Some lit -> try WordL.equals ^^  NicePrint.layoutConst denv.g ty lit with _ -> emptyL
                )
            let layout = LayoutRender.toArray layout
            ToolTipElement.Single (layout, xml)

        | Item.UnionCaseField (ucinfo, fieldIndex) ->
            let rfield = ucinfo.UnionCase.GetFieldByIndex(fieldIndex)
            let fieldTy, _ = PrettyTypes.PrettifyType g rfield.rfield_type
            let id = rfield.Id
            let layout =
                wordL (tagText (FSComp.SR.typeInfoArgument())) ^^
                wordL (tagParameter id.idText) ^^
                RightL.colon ^^
                NicePrint.layoutType denv fieldTy
            let layout = LayoutRender.toArray layout
            ToolTipElement.Single (layout, xml, paramName = id.idText)

        // Not used
        | Item.NewDef id -> 
            let layout = 
                wordL (tagText (FSComp.SR.typeInfoPatternVariable())) ^^
                wordL (tagUnknownEntity id.idText)
            let layout = LayoutRender.toArray layout
            ToolTipElement.Single (layout, xml)

        // .NET fields
        | Item.ILField finfo ->
            let layout = 
                wordL (tagText (FSComp.SR.typeInfoField())) ^^
                NicePrint.layoutType denv finfo.ApparentEnclosingAppType ^^
                SepL.dot ^^
                wordL (tagField finfo.FieldName) ^^
                RightL.colon ^^
                NicePrint.layoutType denv (finfo.FieldType(amap, m)) ^^
                (
                    match finfo.LiteralValue with
                    | None -> emptyL
                    | Some v ->
                        WordL.equals ^^
                        try NicePrint.layoutConst denv.g (finfo.FieldType(infoReader.amap, m)) (CheckExpressions.TcFieldInit m v) with _ -> emptyL
                )
            let layout = LayoutRender.toArray layout
            ToolTipElement.Single (layout, xml)

        // .NET events
        | Item.Event einfo ->
            let rty = PropTypOfEventInfo infoReader m AccessibleFromSomewhere einfo
            let rty, _cxs = PrettyTypes.PrettifyType g rty
            let layout =
                wordL (tagText (FSComp.SR.typeInfoEvent())) ^^
                NicePrint.layoutTyconRef denv einfo.ApparentEnclosingTyconRef ^^
                SepL.dot ^^
                wordL (tagEvent einfo.EventName) ^^
                RightL.colon ^^
                NicePrint.layoutType denv rty
            let layout = LayoutRender.toArray layout
            ToolTipElement.Single (layout, xml)

        // F# and .NET properties
        | Item.Property(_, pinfo :: _) -> 
            let layout = NicePrint.prettyLayoutOfPropInfoFreeStyle  g amap m denv pinfo
            let layout = LayoutRender.toArray layout
            ToolTipElement.Single (layout, xml)

        // Custom operations in queries
        | Item.CustomOperation (customOpName, usageText, Some minfo) -> 

            // Build 'custom operation: where (bool)
            //        
            //        Calls QueryBuilder.Where'
            let layout = 
                wordL (tagText (FSComp.SR.typeInfoCustomOperation())) ^^
                RightL.colon ^^
                (
                    match usageText() with
                    | Some t -> wordL (tagText t)
                    | None ->
                        let argTys = ParamNameAndTypesOfUnaryCustomOperation g minfo |> List.map (fun (ParamNameAndType(_, ty)) -> ty)
                        let argTys, _ = PrettyTypes.PrettifyTypes g argTys 
                        wordL (tagMethod customOpName) ^^ sepListL SepL.space (List.map (fun ty -> LeftL.leftParen ^^ NicePrint.layoutType denv ty ^^ SepL.rightParen) argTys)
                ) ^^
                SepL.lineBreak ^^ SepL.lineBreak  ^^
                wordL (tagText (FSComp.SR.typeInfoCallsWord())) ^^
                NicePrint.layoutTyconRef denv minfo.ApparentEnclosingTyconRef ^^
                SepL.dot ^^
                wordL (tagMethod minfo.DisplayName)

            let layout = LayoutRender.toArray layout
            ToolTipElement.Single (layout, xml)

        // F# constructors and methods
        | Item.CtorGroup(_, minfos) 
        | Item.MethodGroup(_, minfos, _) ->
            FormatOverloadsToList infoReader m denv item minfos
        
        // The 'fake' zero-argument constructors of .NET interfaces.
        // This ideally should never appear in intellisense, but we do get here in repros like:
        //     type IFoo = abstract F : int
        //     type II = IFoo  // remove 'type II = ' and quickly hover over IFoo before it gets squiggled for 'invalid use of interface type'
        // and in that case we'll just show the interface type name.
        | Item.FakeInterfaceCtor ty ->
           let ty, _ = PrettyTypes.PrettifyType g ty
           let layout = NicePrint.layoutTyconRef denv (tcrefOfAppTy g ty)
           let layout = LayoutRender.toArray layout
           ToolTipElement.Single(layout, xml)
        
        // The 'fake' representation of constructors of .NET delegate types
        | Item.DelegateCtor delty -> 
           let delty, _cxs = PrettyTypes.PrettifyType g delty
           let (SigOfFunctionForDelegate(_, _, _, fty)) = GetSigOfFunctionForDelegate infoReader delty m AccessibleFromSomewhere
           let layout =
               NicePrint.layoutTyconRef denv (tcrefOfAppTy g delty) ^^
               LeftL.leftParen ^^
               NicePrint.layoutType denv fty ^^
               RightL.rightParen
           let layout = LayoutRender.toArray layout
           ToolTipElement.Single(layout, xml)

        // Types.
        | Item.Types(_, ((TType_app(tcref, _)) :: _))
        | Item.UnqualifiedType (tcref :: _) -> 
            let denv = { denv with
                            // tooltips are space-constrained, so use shorter names
                            shortTypeNames = true
                            // tooltips are space-constrained, so don't include xml doc comments
                            // on types/members. The doc comments for the actual member will still
                            // be shown in the tip.
                            showDocumentation = false  }
            let layout = NicePrint.layoutTycon denv infoReader AccessibleFromSomewhere m (* width *) tcref.Deref
            let remarks = OutputFullName isListItem pubpathOfTyconRef fullDisplayTextOfTyconRefAsLayout tcref
            let layout = LayoutRender.toArray layout
            let remarks = LayoutRender.toArray remarks
            ToolTipElement.Single (layout, xml, remarks=remarks)

        // F# Modules and namespaces
        | Item.ModuleOrNamespaces((modref :: _) as modrefs) -> 
            //let os = StringBuilder()
            let modrefs = modrefs |> RemoveDuplicateModuleRefs
            let definiteNamespace = modrefs |> List.forall (fun modref -> modref.IsNamespace)
            let kind = 
                if definiteNamespace then FSComp.SR.typeInfoNamespace()
                elif modrefs |> List.forall (fun modref -> modref.IsModule) then FSComp.SR.typeInfoModule()
                else FSComp.SR.typeInfoNamespaceOrModule()
            
            let layout = 
                wordL (tagKeyword kind) ^^
                (if definiteNamespace then tagNamespace (fullDisplayTextOfModRef modref) else (tagModule modref.DemangledModuleOrNamespaceName)
                 |> mkNav modref.DefinitionRange
                 |> wordL)
            if not definiteNamespace then
                let namesToAdd = 
                    ([], modrefs) 
                    ||> Seq.fold (fun st modref -> 
                        match fullDisplayTextOfParentOfModRef modref with 
                        | ValueSome txt -> txt :: st 
                        | _ -> st) 
                    |> Seq.mapi (fun i x -> i, x) 
                    |> Seq.toList
                let layout =
                    layout ^^
                    (
                        if not (List.isEmpty namesToAdd) then
                            SepL.lineBreak ^^
                            List.fold ( fun s (i, txt) ->
                                s ^^
                                SepL.lineBreak ^^
                                wordL (tagText ((if i = 0 then FSComp.SR.typeInfoFromFirst else FSComp.SR.typeInfoFromNext) txt))
                            ) emptyL namesToAdd 
                        else 
                            emptyL
                    )
                let layout = LayoutRender.toArray layout
                ToolTipElement.Single (layout, xml)
            else
                let layout = LayoutRender.toArray layout
                ToolTipElement.Single (layout, xml)

        | Item.AnonRecdField(anon, argTys, i, _) -> 
            let argTy = argTys.[i]
            let nm = anon.SortedNames.[i]
            let argTy, _ = PrettyTypes.PrettifyType g argTy
            let layout =
                wordL (tagText (FSComp.SR.typeInfoAnonRecdField())) ^^
                wordL (tagRecordField nm) ^^
                RightL.colon ^^
                NicePrint.layoutType denv argTy
            let layout = LayoutRender.toArray layout
            ToolTipElement.Single (layout, FSharpXmlDoc.None)
            
        // Named parameters
        | Item.ArgName (id, argTy, _) -> 
            let argTy, _ = PrettyTypes.PrettifyType g argTy
            let layout =
                wordL (tagText (FSComp.SR.typeInfoArgument())) ^^
                wordL (tagParameter id.idText) ^^
                RightL.colon ^^
                NicePrint.layoutType denv argTy
            let layout = LayoutRender.toArray layout
            ToolTipElement.Single (layout, xml, paramName = id.idText)
            
        | Item.SetterArg (_, item) -> 
            FormatItemDescriptionToToolTipElement isListItem infoReader m denv (ItemWithNoInst item)

        |  _ -> 
            ToolTipElement.None

    /// Format the structured version of a tooltip for an item
    let FormatStructuredDescriptionOfItem isDecl infoReader m denv item = 
        ErrorScope.Protect m 
            (fun () -> FormatItemDescriptionToToolTipElement isDecl infoReader m denv item)
            (fun err -> ToolTipElement.CompositionError err)

[<Sealed>]
/// Represents one parameter for one method (or other item) in a group. 
type MethodGroupItemParameter(name: string, canonicalTypeTextForSorting: string, display: TaggedText[], isOptional: bool) = 

    /// The name of the parameter.
    member _.ParameterName = name

    /// A key that can be used for sorting the parameters, used to help sort overloads.
    member _.CanonicalTypeTextForSorting = canonicalTypeTextForSorting

    /// The text to display for the parameter including its name, its type and visual indicators of other
    /// information such as whether it is optional.
    member _.Display = display

    /// Is the parameter optional
    member _.IsOptional = isOptional

[<AutoOpen>]
module internal DescriptionListsImpl = 

    let isFunction g ty =
        let _, tau = tryDestForallTy g ty
        isFunTy g tau 
   
    let printCanonicalizedTypeName g (denv:DisplayEnv) tau =
        // get rid of F# abbreviations and such
        let strippedType = stripTyEqnsWrtErasure EraseAll g tau
        // pretend no namespaces are open
        let denv = denv.SetOpenPaths([])
        // now printing will see a .NET-like canonical representation, that is good for sorting overloads into a reasonable order (see bug 94520)
        NicePrint.stringOfTy denv strippedType

    let PrettyParamOfRecdField g denv (f: RecdField) =
        let display = NicePrint.prettyLayoutOfType denv f.FormalType
        let display = LayoutRender.toArray display
        MethodGroupItemParameter(
          name = f.Name,
          canonicalTypeTextForSorting = printCanonicalizedTypeName g denv f.FormalType,
          // Note: the instantiation of any type parameters is currently incorporated directly into the type
          // rather than being returned separately.
          display = display,
          isOptional=false)
    
    let PrettyParamOfUnionCaseField g denv isGenerated (i: int) (f: RecdField) = 
        let initial = PrettyParamOfRecdField g denv f
        let display = 
            if isGenerated i f then 
                initial.Display 
            else 
                // TODO: in this case ucinst is ignored - it gives the instantiation of the type parameters of
                // the union type containing this case.
                let display = NicePrint.layoutOfParamData denv (ParamData(false, false, false, NotOptional, NoCallerInfo, Some f.Id, ReflectedArgInfo.None, f.FormalType)) 
                LayoutRender.toArray display

        MethodGroupItemParameter(
          name=initial.ParameterName,
          canonicalTypeTextForSorting=initial.CanonicalTypeTextForSorting,
          display=display,
          isOptional=false)

    let ParamOfParamData g denv (ParamData(_isParamArrayArg, _isInArg, _isOutArg, optArgInfo, _callerInfo, nmOpt, _reflArgInfo, pty) as paramData) =
        let display = NicePrint.layoutOfParamData denv paramData
        let display = LayoutRender.toArray display
        MethodGroupItemParameter(
          name = (match nmOpt with None -> "" | Some pn -> pn.idText),
          canonicalTypeTextForSorting = printCanonicalizedTypeName g denv pty,
          display = display,
          isOptional=optArgInfo.IsOptional)

    // TODO this code is similar to NicePrint.fs:formatParamDataToBuffer, refactor or figure out why different?
    let PrettyParamsOfParamDatas g denv typarInst (paramDatas:ParamData list) rty = 
        let paramInfo, paramTypes = 
            paramDatas 
            |> List.map (fun (ParamData(isParamArrayArg, _isInArg, _isOutArg, optArgInfo, _callerInfo, nmOpt, _reflArgInfo, pty)) -> 
                let isOptArg = optArgInfo.IsOptional
                match nmOpt, isOptArg, tryDestOptionTy denv.g pty with 
                // Layout an optional argument 
                | Some id, true, ptyOpt -> 
                    let nm = id.idText
                    // detect parameter type, if ptyOpt is None - this is .NET style optional argument
                    let pty = match ptyOpt with ValueSome x -> x | _ -> pty
                    (nm, isOptArg, SepL.questionMark ^^ (wordL (TaggedText.tagParameter nm))),  pty
                // Layout an unnamed argument 
                | None, _, _ -> 
                    ("", isOptArg, emptyL), pty
                // Layout a named argument 
                | Some id, _, _ -> 
                    let nm = id.idText
                    let prefix = 
                        if isParamArrayArg then
                            NicePrint.PrintUtilities.layoutBuiltinAttribute denv denv.g.attrib_ParamArrayAttribute ^^
                            wordL (TaggedText.tagParameter nm) ^^
                            RightL.colon
                            //sprintf "%s %s: " (NicePrint.PrintUtilities.layoutBuiltinAttribute denv denv.g.attrib_ParamArrayAttribute |> showL) nm 
                        else 
                            wordL (TaggedText.tagParameter nm) ^^
                            RightL.colon
                            //sprintf "%s: " nm
                    (nm, isOptArg, prefix), pty)
            |> List.unzip

        // Prettify everything
        let prettyTyparInst, (prettyParamTys, _prettyRetTy), (prettyParamTysL, prettyRetTyL), prettyConstraintsL = 
            NicePrint.prettyLayoutOfInstAndSig denv (typarInst, paramTypes, rty)

        // Remake the params using the prettified versions
        let prettyParams = 
          (paramInfo, prettyParamTys, prettyParamTysL) |||> List.map3 (fun (nm, isOptArg, paramPrefix) tau tyL -> 
            let display = paramPrefix ^^ tyL
            let display = LayoutRender.toArray display
            MethodGroupItemParameter(
              name = nm,
              canonicalTypeTextForSorting = printCanonicalizedTypeName g denv tau,
              display = display,
              isOptional=isOptArg
            ))

        prettyTyparInst, prettyParams, prettyRetTyL, prettyConstraintsL

    let PrettyParamsOfTypes g denv typarInst paramTys retTy = 

        // Prettify everything
        let prettyTyparInst, (prettyParamTys, _prettyRetTy), (prettyParamTysL, prettyRetTyL), prettyConstraintsL = 
            NicePrint.prettyLayoutOfInstAndSig denv  (typarInst, paramTys, retTy) 

        // Remake the params using the prettified versions
        let parameters = 
            (prettyParamTys, prettyParamTysL)
            ||> List.map2 (fun tau tyL ->
                let display = LayoutRender.toArray tyL
                MethodGroupItemParameter(
                    name = "",
                    canonicalTypeTextForSorting = printCanonicalizedTypeName g denv tau,
                    display =  display,
                    isOptional=false
                ))

        // Return the results
        prettyTyparInst, parameters, prettyRetTyL, prettyConstraintsL
                          

#if !NO_EXTENSIONTYPING

    /// Get the set of static parameters associated with an item
    let StaticParamsOfItem (infoReader:InfoReader) m denv item = 
        let amap = infoReader.amap
        let g = infoReader.g
        match item with
        | SymbolHelpers.ItemIsWithStaticArguments m g staticParameters ->
            staticParameters 
                |> Array.map (fun sp -> 
                    let ty = Import.ImportProvidedType amap m (sp.PApply((fun x -> x.ParameterType), m))
                    let spKind = NicePrint.prettyLayoutOfType denv ty
                    let spName = sp.PUntaint((fun sp -> sp.Name), m)
                    let spOpt = sp.PUntaint((fun sp -> sp.IsOptional), m)
                    let display = (if spOpt then SepL.questionMark else emptyL) ^^ wordL (TaggedText.tagParameter spName) ^^ RightL.colon ^^ spKind
                    let display = LayoutRender.toArray display
                    MethodGroupItemParameter(
                      name = spName,
                      canonicalTypeTextForSorting = showL spKind,
                      display = display,
                      //display = sprintf "%s%s: %s" (if spOpt then "?" else "") spName spKind,
                      isOptional=spOpt))
        | _ -> [| |]
#endif

    /// Get all the information about parameters and "prettify" the types by choosing nice type variable
    /// names.  This is similar to the other variations on "show me an item" code. This version is
    /// is used when presenting groups of methods (see MethodGroup).  It is possible these different
    /// versions could be better unified.
    let rec PrettyParamsAndReturnTypeOfItem (infoReader:InfoReader) m denv (item: ItemWithInst) = 
        let amap = infoReader.amap
        let g = infoReader.g
        let denv = { SymbolHelpers.SimplerDisplayEnv denv with useColonForReturnType=true}
        match item.Item with
        | Item.Value vref -> 
            let getPrettyParamsOfTypes() = 
                let tau = vref.TauType
                match tryDestFunTy denv.g tau with
                | ValueSome(arg, rtau) ->
                    let args = tryDestRefTupleTy denv.g arg 
                    let _prettyTyparInst, prettyParams, prettyRetTyL, _prettyConstraintsL = PrettyParamsOfTypes g denv item.TyparInst args rtau
                    // FUTURE: prettyTyparInst is the pretty version of the known instantiations of type parameters in the output. It could be returned
                    // for display as part of the method group
                    prettyParams, prettyRetTyL
                | _ -> 
                    let _prettyTyparInst, prettyTyL = NicePrint.prettyLayoutOfUncurriedSig denv item.TyparInst [] tau
                    [], prettyTyL

            match vref.ValReprInfo with
            | None -> 
                // ValReprInfo = None i.e. in let bindings defined in types or in local functions
                // in this case use old approach and return only information about types
                getPrettyParamsOfTypes ()

            | Some valRefInfo ->
                // ValReprInfo will exist for top-level syntactic functions
                // per spec: binding is considered to define a syntactic function if it is either a function or its immediate right-hand-side is a anonymous function
                let (_, argInfos,  lastRetTy, _) = GetTopValTypeInFSharpForm  g valRefInfo vref.Type m
                match argInfos with
                | [] -> 
                    // handles cases like 'let foo = List.map'
                    getPrettyParamsOfTypes() 
                | firstCurriedArgInfo :: _ ->
                    // result 'paramDatas' collection corresponds to the first argument of curried function
                    // i.e. let func (a : int) (b : int) = a + b
                    // paramDatas will contain information about a and retTy will be: int -> int
                    // This is good enough as we don't provide ways to display info for the second curried argument
                    let firstCurriedParamDatas = 
                        firstCurriedArgInfo
                        |> List.map ParamNameAndType.FromArgInfo
                        |> List.map (fun (ParamNameAndType(nmOpt, pty)) -> ParamData(false, false, false, NotOptional, NoCallerInfo, nmOpt, ReflectedArgInfo.None, pty))

                    // Adjust the return type so it only strips the first argument
                    let curriedRetTy = 
                        match tryDestFunTy denv.g vref.TauType with
                        | ValueSome(_, rtau) -> rtau
                        | _ -> lastRetTy

                    let _prettyTyparInst, prettyFirstCurriedParams, prettyCurriedRetTyL, prettyConstraintsL = PrettyParamsOfParamDatas g denv item.TyparInst firstCurriedParamDatas curriedRetTy
                    
                    let prettyCurriedRetTyL = prettyCurriedRetTyL ^^ SepL.space ^^ prettyConstraintsL

                    prettyFirstCurriedParams, prettyCurriedRetTyL

        | Item.UnionCase(ucinfo, _)   -> 
            let prettyParams = 
                match ucinfo.UnionCase.RecdFields with
                | [f] -> [PrettyParamOfUnionCaseField g denv NicePrint.isGeneratedUnionCaseField -1 f]
                | fs -> fs |> List.mapi (PrettyParamOfUnionCaseField g denv NicePrint.isGeneratedUnionCaseField)
            let rty = generalizedTyconRef ucinfo.TyconRef
            let rtyL = NicePrint.layoutType denv rty
            prettyParams, rtyL

        | Item.ActivePatternCase(apref)   -> 
            let v = apref.ActivePatternVal 
            let tau = v.TauType
            let args, resTy = stripFunTy denv.g tau 

            let apinfo = Option.get (TryGetActivePatternInfo v)
            let aparity = apinfo.Names.Length
            
            let rty = if aparity <= 1 then resTy else (argsOfAppTy g resTy).[apref.CaseIndex]

            let _prettyTyparInst, prettyParams, prettyRetTyL, _prettyConstraintsL = PrettyParamsOfTypes g denv item.TyparInst args rty
            // FUTURE: prettyTyparInst is the pretty version of the known instantiations of type parameters in the output. It could be returned
            // for display as part of the method group
            prettyParams, prettyRetTyL

        | Item.ExnCase ecref -> 
            let prettyParams = ecref |> recdFieldsOfExnDefRef |> List.mapi (PrettyParamOfUnionCaseField g denv NicePrint.isGeneratedExceptionField) 
            let _prettyTyparInst, prettyRetTyL = NicePrint.prettyLayoutOfUncurriedSig denv item.TyparInst [] g.exn_ty
            prettyParams, prettyRetTyL

        | Item.RecdField rfinfo ->
            let _prettyTyparInst, prettyRetTyL = NicePrint.prettyLayoutOfUncurriedSig denv item.TyparInst [] rfinfo.FieldType
            [], prettyRetTyL

        | Item.AnonRecdField(_anonInfo, tys, i, _) ->
            let _prettyTyparInst, prettyRetTyL = NicePrint.prettyLayoutOfUncurriedSig denv item.TyparInst [] tys.[i]
            [], prettyRetTyL

        | Item.ILField finfo ->
            let _prettyTyparInst, prettyRetTyL = NicePrint.prettyLayoutOfUncurriedSig denv item.TyparInst [] (finfo.FieldType(amap, m))
            [], prettyRetTyL

        | Item.Event einfo ->
            let _prettyTyparInst, prettyRetTyL = NicePrint.prettyLayoutOfUncurriedSig denv item.TyparInst [] (PropTypOfEventInfo infoReader m AccessibleFromSomewhere einfo)
            [], prettyRetTyL

        | Item.Property(_, pinfo :: _) -> 
            let paramDatas = pinfo.GetParamDatas(amap, m)
            let rty = pinfo.GetPropertyType(amap, m) 

            let _prettyTyparInst, prettyParams, prettyRetTyL, _prettyConstraintsL = PrettyParamsOfParamDatas g denv item.TyparInst paramDatas rty
            // FUTURE: prettyTyparInst is the pretty version of the known instantiations of type parameters in the output. It could be returned
            // for display as part of the method group
            prettyParams, prettyRetTyL

        | Item.CtorGroup(_, (minfo :: _)) 
        | Item.MethodGroup(_, (minfo :: _), _) -> 
            let paramDatas = minfo.GetParamDatas(amap, m, minfo.FormalMethodInst) |> List.head
            let rty = minfo.GetFSharpReturnTy(amap, m, minfo.FormalMethodInst)
            let _prettyTyparInst, prettyParams, prettyRetTyL, _prettyConstraintsL = PrettyParamsOfParamDatas g denv item.TyparInst paramDatas rty
            // FUTURE: prettyTyparInst is the pretty version of the known instantiations of type parameters in the output. It could be returned
            // for display as part of the method group
            prettyParams, prettyRetTyL

        | Item.CustomBuilder (_, vref) -> 
            PrettyParamsAndReturnTypeOfItem infoReader m denv { item with Item = Item.Value vref }

        | Item.TypeVar _ -> 
            [], emptyL

        | Item.CustomOperation (_, usageText, Some minfo) -> 
            match usageText() with 
            | None -> 
                let argNamesAndTys = SymbolHelpers.ParamNameAndTypesOfUnaryCustomOperation g minfo 
                let argTys, _ = PrettyTypes.PrettifyTypes g (argNamesAndTys |> List.map (fun (ParamNameAndType(_, ty)) -> ty))
                let paramDatas = (argNamesAndTys, argTys) ||> List.map2 (fun (ParamNameAndType(nmOpt, _)) argTy -> ParamData(false, false, false, NotOptional, NoCallerInfo, nmOpt, ReflectedArgInfo.None, argTy))
                let rty = minfo.GetFSharpReturnTy(amap, m, minfo.FormalMethodInst)
                let _prettyTyparInst, prettyParams, prettyRetTyL, _prettyConstraintsL = PrettyParamsOfParamDatas g denv item.TyparInst paramDatas rty

                // FUTURE: prettyTyparInst is the pretty version of the known instantiations of type parameters in the output. It could be returned
                // for display as part of the method group
                prettyParams, prettyRetTyL

            | Some _ -> 
                let rty = minfo.GetFSharpReturnTy(amap, m, minfo.FormalMethodInst)
                let _prettyTyparInst, prettyRetTyL = NicePrint.prettyLayoutOfUncurriedSig denv item.TyparInst [] rty
                [], prettyRetTyL  // no parameter data available for binary operators like 'zip', 'join' and 'groupJoin' since they use bespoke syntax 

        | Item.FakeInterfaceCtor ty -> 
            let _prettyTyparInst, prettyRetTyL = NicePrint.prettyLayoutOfUncurriedSig denv item.TyparInst [] ty
            [], prettyRetTyL

        | Item.DelegateCtor delty -> 
            let (SigOfFunctionForDelegate(_, _, _, fty)) = GetSigOfFunctionForDelegate infoReader delty m AccessibleFromSomewhere

            // No need to pass more generic type information in here since the instanitations have already been applied
            let _prettyTyparInst, prettyParams, prettyRetTyL, _prettyConstraintsL = PrettyParamsOfParamDatas g denv item.TyparInst [ParamData(false, false, false, NotOptional, NoCallerInfo, None, ReflectedArgInfo.None, fty)] delty

            // FUTURE: prettyTyparInst is the pretty version of the known instantiations of type parameters in the output. It could be returned
            // for display as part of the method group
            prettyParams, prettyRetTyL

        |  _ -> 
            [], emptyL


    /// Compute the index of the VS glyph shown with an item in the Intellisense menu
    let GlyphOfItem(denv, item) : FSharpGlyph = 
         /// Find the glyph for the given representation.    
         let reprToGlyph repr = 
            match repr with
            | TFSharpObjectRepr om -> 
                match om.fsobjmodel_kind with 
                | TTyconClass -> FSharpGlyph.Class
                | TTyconInterface -> FSharpGlyph.Interface
                | TTyconStruct -> FSharpGlyph.Struct
                | TTyconDelegate _ -> FSharpGlyph.Delegate
                | TTyconEnum _ -> FSharpGlyph.Enum
            | TRecdRepr _ -> FSharpGlyph.Type
            | TUnionRepr _ -> FSharpGlyph.Union
            | TILObjectRepr (TILObjectReprData (_, _, td)) -> 
                if td.IsClass        then FSharpGlyph.Class
                elif td.IsStruct     then FSharpGlyph.Struct
                elif td.IsInterface  then FSharpGlyph.Interface
                elif td.IsEnum       then FSharpGlyph.Enum
                else                      FSharpGlyph.Delegate
            | TAsmRepr _ -> FSharpGlyph.Typedef
            | TMeasureableRepr _-> FSharpGlyph.Typedef 
#if !NO_EXTENSIONTYPING
            | TProvidedTypeExtensionPoint _-> FSharpGlyph.Typedef 
            | TProvidedNamespaceExtensionPoint  _-> FSharpGlyph.Typedef  
#endif
            | TNoRepr -> FSharpGlyph.Class  
         
         /// Find the glyph for the given type representation.
         let typeToGlyph ty = 
            match tryTcrefOfAppTy denv.g ty with
            | ValueSome tcref -> tcref.TypeReprInfo |> reprToGlyph
            | _ ->
                if isStructTupleTy denv.g ty then FSharpGlyph.Struct
                elif isRefTupleTy denv.g ty then FSharpGlyph.Class
                elif isFunction denv.g ty then FSharpGlyph.Delegate
                elif isTyparTy denv.g ty then FSharpGlyph.Struct
                else FSharpGlyph.Typedef
            
         // This may explore assemblies that are not in the reference set,
         // e.g. for type abbreviations to types not in the reference set. 
         // In this case just use GlyphMajor.Class.
         protectAssemblyExploration FSharpGlyph.Class (fun () ->
            match item with 
            | Item.Value(vref) | Item.CustomBuilder (_, vref) -> 
                  if isFunction denv.g vref.Type then FSharpGlyph.Method
                  elif vref.LiteralValue.IsSome then FSharpGlyph.Constant
                  else FSharpGlyph.Variable
            | Item.Types(_, ty :: _) -> typeToGlyph (stripTyEqns denv.g ty)    
            | Item.UnionCase _
            | Item.ActivePatternCase _ -> FSharpGlyph.EnumMember   
            | Item.ExnCase _ -> FSharpGlyph.Exception   
            | Item.AnonRecdField _ -> FSharpGlyph.Field
            | Item.RecdField _ -> FSharpGlyph.Field
            | Item.UnionCaseField _ -> FSharpGlyph.Field
            | Item.ILField _ -> FSharpGlyph.Field
            | Item.Event _ -> FSharpGlyph.Event   
            | Item.Property _ -> FSharpGlyph.Property   
            | Item.CtorGroup _ 
            | Item.DelegateCtor _ 
            | Item.FakeInterfaceCtor _
            | Item.CustomOperation _ -> FSharpGlyph.Method
            | Item.MethodGroup (_, minfos, _) when minfos |> List.forall (fun minfo -> minfo.IsExtensionMember) -> FSharpGlyph.ExtensionMethod
            | Item.MethodGroup _ -> FSharpGlyph.Method
            | Item.TypeVar _ 
            | Item.Types _  -> FSharpGlyph.Class
            | Item.UnqualifiedType (tcref :: _) -> 
                if tcref.IsEnumTycon || tcref.IsILEnumTycon then FSharpGlyph.Enum
                elif tcref.IsExceptionDecl then FSharpGlyph.Exception
                elif tcref.IsFSharpDelegateTycon then FSharpGlyph.Delegate
                elif tcref.IsFSharpInterfaceTycon then FSharpGlyph.Interface
                elif tcref.IsFSharpStructOrEnumTycon then FSharpGlyph.Struct
                elif tcref.IsModule then FSharpGlyph.Module
                elif tcref.IsNamespace then FSharpGlyph.NameSpace
                elif tcref.IsUnionTycon then FSharpGlyph.Union
                elif tcref.IsILTycon then 
                    let (TILObjectReprData (_, _, tydef)) = tcref.ILTyconInfo
                    if tydef.IsInterface then FSharpGlyph.Interface
                    elif tydef.IsDelegate then FSharpGlyph.Delegate
                    elif tydef.IsEnum then FSharpGlyph.Enum
                    elif tydef.IsStruct then FSharpGlyph.Struct
                    else FSharpGlyph.Class
                else FSharpGlyph.Class
            | Item.ModuleOrNamespaces(modref :: _) -> 
                  if modref.IsNamespace then FSharpGlyph.NameSpace else FSharpGlyph.Module
            | Item.ArgName _ -> FSharpGlyph.Variable
            | Item.SetterArg _ -> FSharpGlyph.Variable
            | _ -> FSharpGlyph.Error)


    /// Get rid of groups of overloads an replace them with single items.
    /// (This looks like it is doing the a similar thing as FlattenItems, this code 
    /// duplication could potentially be removed)
    let AnotherFlattenItems g m item =
        match item with 
        | Item.CtorGroup(nm, cinfos) -> List.map (fun minfo -> Item.CtorGroup(nm, [minfo])) cinfos 
        | Item.FakeInterfaceCtor _
        | Item.DelegateCtor _ -> [item]
        | Item.NewDef _ 
        | Item.ILField _ -> []
        | Item.Event _ -> []
        | Item.RecdField(rfinfo) -> 
            if isFunction g rfinfo.FieldType then [item] else []
        | Item.Value v -> 
            if isFunction g v.Type then [item] else []
        | Item.UnionCase(ucr, _) -> 
            if not ucr.UnionCase.IsNullary then [item] else []
        | Item.ExnCase(ecr) -> 
            if isNil (recdFieldsOfExnDefRef ecr) then [] else [item]
        | Item.Property(_, pinfos) -> 
            let pinfo = List.head pinfos 
            if pinfo.IsIndexer then [item] else []
#if !NO_EXTENSIONTYPING
        | SymbolHelpers.ItemIsWithStaticArguments m g _ -> 
            // we pretend that provided-types-with-static-args are method-like in order to get ParamInfo for them
            [item] 
#endif
        | Item.MethodGroup(nm, minfos, orig) -> minfos |> List.map (fun minfo -> Item.MethodGroup(nm, [minfo], orig)) 
        | Item.CustomOperation(_name, _helpText, _minfo) -> [item]
        | Item.TypeVar _ -> []
        | Item.CustomBuilder _ -> []
        | _ -> []


/// An intellisense declaration
[<Sealed>]
type DeclarationListItem(name: string, nameInCode: string, fullName: string, glyph: FSharpGlyph, info, accessibility: FSharpAccessibility,
                               kind: CompletionItemKind, isOwnMember: bool, priority: int, isResolved: bool, namespaceToOpen: string option) =
    member _.Name = name

    member _.NameInCode = nameInCode

    member _.Description = 
        match info with
        | Choice1Of2 (items: CompletionItem list, infoReader, m, denv) -> 
            ToolTipText(items |> List.map (fun x -> FormatStructuredDescriptionOfItem true infoReader m denv x.ItemWithInst))
        | Choice2Of2 result -> 
            result

    member _.Glyph = glyph 

    member _.Accessibility = accessibility

    member _.Kind = kind

    member _.IsOwnMember = isOwnMember

    member _.MinorPriority = priority

    member _.FullName = fullName

    member _.IsResolved = isResolved

    member _.NamespaceToOpen = namespaceToOpen

/// A table of declarations for Intellisense completion 
[<Sealed>]
type DeclarationListInfo(declarations: DeclarationListItem[], isForType: bool, isError: bool) = 
    static let fsharpNamespace = [|"Microsoft"; "FSharp"|]

    member _.Items = declarations

    member _.IsForType = isForType

    member _.IsError = isError

    // Make a 'Declarations' object for a set of selected items
    static member Create(infoReader:InfoReader, m: range, denv, getAccessibility: (Item -> FSharpAccessibility), items: CompletionItem list, currentNamespace: string[] option, isAttributeApplicationContext: bool) = 
        let g = infoReader.g
        let isForType = items |> List.exists (fun x -> x.Type.IsSome)
        let items = items |> RemoveExplicitlySuppressedCompletionItems g
        
        let tyconRefOptEq tref1 tref2 =
            match tref1, tref2 with
            | Some tref1, tref2 -> tyconRefEq g tref1 tref2
            | _ -> false

        // Adjust items priority. Sort by name. For things with the same name,
        //     - show types with fewer generic parameters first
        //     - show types before over other related items - they usually have very useful XmlDocs 
        let _, _, items = 
            items 
            |> List.map (fun x ->
                match x.Item with
                | Item.Types (_, (TType_app(tcref, _) :: _)) -> { x with MinorPriority = 1 + tcref.TyparsNoRange.Length }
                // Put delegate ctors after types, sorted by #typars. RemoveDuplicateItems will remove FakeInterfaceCtor and DelegateCtor if an earlier type is also reported with this name
                | Item.FakeInterfaceCtor (TType_app(tcref, _)) 
                | Item.DelegateCtor (TType_app(tcref, _)) -> { x with MinorPriority = 1000 + tcref.TyparsNoRange.Length }
                // Put type ctors after types, sorted by #typars. RemoveDuplicateItems will remove DefaultStructCtors if a type is also reported with this name
                | Item.CtorGroup (_, (cinfo :: _)) -> { x with MinorPriority = 1000 + 10 * cinfo.DeclaringTyconRef.TyparsNoRange.Length }
                | Item.MethodGroup(_, minfo :: _, _) -> { x with IsOwnMember = tyconRefOptEq x.Type minfo.DeclaringTyconRef }
                | Item.Property(_, pinfo :: _) -> { x with IsOwnMember = tyconRefOptEq x.Type pinfo.DeclaringTyconRef }
                | Item.ILField finfo -> { x with IsOwnMember = tyconRefOptEq x.Type finfo.DeclaringTyconRef }
                | _ -> x)
            |> List.sortBy (fun x -> x.MinorPriority)
            |> List.fold (fun (prevRealPrior, prevNormalizedPrior, acc) x ->
                if x.MinorPriority = prevRealPrior then
                    prevRealPrior, prevNormalizedPrior, x :: acc
                else
                    let normalizedPrior = prevNormalizedPrior + 1
                    x.MinorPriority, normalizedPrior, { x with MinorPriority = normalizedPrior } :: acc
                ) (0, 0, [])

        if verbose then dprintf "service.ml: mkDecls: %d found groups after filtering\n" (List.length items); 

        // Group by full name for unresolved items and by display name for resolved ones.
        let items = 
            items
            |> List.rev
            // Prefer items from file check results to ones from referenced assemblies via GetAssemblyContent ("all entities")
            |> List.sortBy (fun x -> x.Unresolved.IsSome) 
            // Remove all duplicates. We've put the types first, so this removes the DelegateCtor and DefaultStructCtor's.
            |> RemoveDuplicateCompletionItems g
            |> List.groupBy (fun x ->
                match x.Unresolved with
                | Some u -> 
                    match u.Namespace with
                    | [||] -> u.DisplayName
                    | ns -> (ns |> String.concat ".") + "." + u.DisplayName
                | None -> x.Item.DisplayName)
            |> List.map (fun (_, items) -> 
                let item = items.Head
                let name = 
                    match item.Unresolved with
                    | Some u -> u.DisplayName
                    | None -> item.Item.DisplayName
                name, items)

        // Filter out operators, active patterns (as values) and the empty list
        let items = 
            // Check whether this item looks like an operator.
            let isOperatorItem name (items: CompletionItem list) =
                match items with
                | [item] ->
                    match item.Item with
                    | Item.Value _ | Item.MethodGroup _ | Item.UnionCase _ -> IsOperatorName name
                    | _ -> false
                | _ -> false              

            let isActivePatternItem (items: CompletionItem list) =
                match items with
                | [item] ->
                    match item.Item with
                    | Item.Value vref -> IsActivePatternName vref.CoreDisplayName
                    | _ -> false
                | _ -> false

            items |> List.filter (fun (displayName, items) -> 
                not (isOperatorItem displayName items) && 
                not (displayName = "[]") && // list shows up as a Type and a UnionCase, only such entity with a symbolic name, but want to filter out of intellisense
                not (isActivePatternItem items))

        let decls = 
            items 
            |> List.map (fun (displayName, itemsWithSameFullName) -> 
                match itemsWithSameFullName with
                | [] -> failwith "Unexpected empty bag"
                | _ ->
                    let items =
                        match itemsWithSameFullName |> List.partition (fun x -> x.Unresolved.IsNone) with
                        | [], unresolved -> unresolved
                        // if there are resolvable items, throw out unresolved to prevent duplicates like `Set` and `FSharp.Collections.Set`.
                        | resolved, _ -> resolved 
                    
                    let item = items.Head
                    let glyph = GlyphOfItem(denv, item.Item)

                    let name, nameInCode =
                        if displayName.StartsWithOrdinal("( ") && displayName.EndsWithOrdinal(" )") then
                            let cleanName = displayName.[2..displayName.Length - 3]
                            cleanName,
                            if IsOperatorName displayName then cleanName else "``" + cleanName + "``"
                        else 
                            displayName,
                            match item.Unresolved with
                            | Some _ -> displayName
                            | None -> Lexhelp.Keywords.QuoteIdentifierIfNeeded displayName

                    let isAttributeItem = lazy (SymbolHelpers.IsAttribute infoReader item.Item)

                    let cutAttributeSuffix (name: string) =
                        if isAttributeApplicationContext && name <> "Attribute" && name.EndsWithOrdinal("Attribute") && isAttributeItem.Value then
                            name.[0..name.Length - "Attribute".Length - 1]
                        else name

                    let name = cutAttributeSuffix name
                    let nameInCode = cutAttributeSuffix nameInCode
                    
                    let fullName = 
                        match item.Unresolved with
                        | Some x -> x.FullName
                        | None -> SymbolHelpers.FullNameOfItem g item.Item
                    
                    let namespaceToOpen = 
                        item.Unresolved 
                        |> Option.map (fun x -> x.Namespace)
                        |> Option.bind (fun ns ->
                            if ns |> Array.startsWith fsharpNamespace then None
                            else Some ns)
                        |> Option.map (fun ns ->
                            match currentNamespace with
                            | Some currentNs ->
                               if ns |> Array.startsWith currentNs then
                                 ns.[currentNs.Length..]
                               else ns
                            | None -> ns)
                        |> Option.bind (function
                            | [||] -> None
                            | ns -> Some (System.String.Join(".", ns)))

                    DeclarationListItem(
                        name, nameInCode, fullName, glyph, Choice1Of2 (items, infoReader, m, denv), getAccessibility item.Item,
                        item.Kind, item.IsOwnMember, item.MinorPriority, item.Unresolved.IsNone, namespaceToOpen))

        new DeclarationListInfo(Array.ofList decls, isForType, false)
    
    static member Error message = 
        new DeclarationListInfo(
                [| DeclarationListItem("<Note>", "<Note>", "<Note>", FSharpGlyph.Error, Choice2Of2 (ToolTipText [ToolTipElement.CompositionError message]),
                                             FSharpAccessibility(taccessPublic), CompletionItemKind.Other, false, 0, false, None) |], false, true)
    
    static member Empty = DeclarationListInfo([| |], false, false)



/// Represents one method (or other item) in a method group. The item may represent either a method or 
/// a single, non-overloaded item such as union case or a named function value.
// Note: instances of this type do not hold any references to any compiler resources.
[<Sealed; NoEquality; NoComparison>]
type MethodGroupItem(description: ToolTipText, xmlDoc: FSharpXmlDoc,
                           returnType: TaggedText[], parameters: MethodGroupItemParameter[],
                           hasParameters: bool, hasParamArrayArg: bool, staticParameters: MethodGroupItemParameter[]) = 

    /// The description representation for the method (or other item)
    member _.Description = description

    /// The documentation for the item
    member _.XmlDoc = xmlDoc

    /// The return type text for the method (or other item)
    member _.ReturnTypeText = returnType

    /// The parameters of the method in the overload set
    member _.Parameters = parameters

    /// Does the method support an arguments list?  This is always true except for static type instantiations like TP<42, "foo">.
    member _.HasParameters = hasParameters

    /// Does the method support a params list arg?
    member _.HasParamArrayArg = hasParamArrayArg

    /// Does the type name or method support a static arguments list, like TP<42, "foo"> or conn.CreateCommand<42, "foo">(arg1, arg2)?
    member _.StaticParameters = staticParameters


/// A table of methods for Intellisense completion
//
// Note: this type does not hold any strong references to any compiler resources, nor does evaluating any of the properties execute any
// code on the compiler thread.  
[<Sealed>]
type MethodGroup( name: string, unsortedMethods: MethodGroupItem[] ) = 
    // BUG 413009 : [ParameterInfo] takes about 3 seconds to move from one overload parameter to another
    // cache allows to avoid recomputing parameterinfo for the same item
#if !FX_NO_WEAKTABLE
    static let methodOverloadsCache = System.Runtime.CompilerServices.ConditionalWeakTable<ItemWithInst, MethodGroupItem[]>()
#endif

    let methods = 
        unsortedMethods 
        // Methods with zero arguments show up here as taking a single argument of type 'unit'.  Patch them now to appear as having zero arguments.
        |> Array.map (fun meth -> 
            let parms = meth.Parameters
            if parms.Length = 1 && parms.[0].CanonicalTypeTextForSorting="Microsoft.FSharp.Core.Unit" then 
                MethodGroupItem(meth.Description, meth.XmlDoc, meth.ReturnTypeText, [||], true, meth.HasParamArrayArg, meth.StaticParameters) 
            else 
                meth)
        // Fix the order of methods, to be stable for unit testing.
        |> Array.sortBy (fun meth -> 
            let parms = meth.Parameters
            parms.Length, (parms |> Array.map (fun p -> p.CanonicalTypeTextForSorting)))

    member _.MethodName = name

    member _.Methods = methods

    static member Create (infoReader: InfoReader, m, denv, items:ItemWithInst list) = 
        let g = infoReader.g
        if isNil items then new MethodGroup("", [| |]) else
        let name = items.Head.Item.DisplayName 

        let methods = 
          [| for item in items do 
#if !FX_NO_WEAKTABLE
               match methodOverloadsCache.TryGetValue item with
               | true, res -> yield! res
               | false, _ ->
#endif
                let flatItems = AnotherFlattenItems g  m item.Item

                let methods = 
                    flatItems |> Array.ofList |> Array.map (fun flatItem -> 
                        let prettyParams, prettyRetTyL = 
                            ErrorScope.Protect m 
                                (fun () -> PrettyParamsAndReturnTypeOfItem infoReader m denv  { item with Item = flatItem })
                                (fun err -> [], wordL (tagText err))
                            
                        let description = ToolTipText [FormatStructuredDescriptionOfItem true infoReader m denv { item with Item = flatItem }]

                        let hasParamArrayArg = 
                            match flatItem with 
                            | Item.CtorGroup(_, [meth]) 
                            | Item.MethodGroup(_, [meth], _) -> meth.HasParamArrayArg(infoReader.amap, m, meth.FormalMethodInst) 
                            | _ -> false

                        let hasStaticParameters = 
                            match flatItem with 
#if !NO_EXTENSIONTYPING
                            | SymbolHelpers.ItemIsProvidedTypeWithStaticArguments m g _ -> false 
#endif
                            | _ -> true

                        let prettyRetTyL = LayoutRender.toArray prettyRetTyL
                        MethodGroupItem(
                          description = description,
                          returnType = prettyRetTyL,
                          xmlDoc = SymbolHelpers.GetXmlCommentForItem infoReader m flatItem,
                          parameters = (prettyParams |> Array.ofList),
                          hasParameters = hasStaticParameters,
                          hasParamArrayArg = hasParamArrayArg,
#if !NO_EXTENSIONTYPING
                          staticParameters = StaticParamsOfItem infoReader m denv flatItem
#else
                          staticParameters = [| |]
#endif
                        ))
#if !FX_NO_WEAKTABLE
                methodOverloadsCache.Add(item, methods)
#endif
                yield! methods 
           |]

        new MethodGroup(name, methods)



