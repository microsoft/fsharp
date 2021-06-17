// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace rec FSharp.Compiler.Symbols

open System
open System.Collections.Generic
open Internal.Utilities.Collections
open Internal.Utilities.Library
open FSharp.Compiler
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AttributeChecking
open FSharp.Compiler.AccessibilityLogic
open FSharp.Compiler.CheckDeclarations
open FSharp.Compiler.CompilerImports
open FSharp.Compiler.Infos
open FSharp.Compiler.InfoReader
open FSharp.Compiler.NameResolution
open FSharp.Compiler.Syntax
open FSharp.Compiler.SyntaxTreeOps
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Compiler.Xml
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.Syntax.PrettyNaming
open FSharp.Compiler.AbstractIL

type FSharpAccessibility(a:Accessibility, ?isProtected) = 
    let isProtected = defaultArg isProtected  false

    let isInternalCompPath x = 
        match x with 
        | CompPath(ILScopeRef.Local, []) -> true 
        | _ -> false

    let (|Public|Internal|Private|) (TAccess p) = 
        match p with 
        | [] -> Public 
        | _ when List.forall isInternalCompPath p  -> Internal 
        | _ -> Private

    member _.IsPublic = not isProtected && match a with TAccess [] -> true | _ -> false

    member _.IsPrivate = not isProtected && match a with Private -> true | _ -> false

    member _.IsInternal = not isProtected && match a with Internal -> true | _ -> false

    member _.IsProtected = isProtected

    member internal _.Contents = a

    override _.ToString() = 
        let (TAccess paths) = a
        let mangledTextOfCompPath (CompPath(scoref, path)) = getNameOfScopeRef scoref + "/" + textOfPath (List.map fst path)  
        String.concat ";" (List.map mangledTextOfCompPath paths)

type SymbolEnv(g: TcGlobals, thisCcu: CcuThunk, thisCcuTyp: ModuleOrNamespaceType option, tcImports: TcImports, amap: Import.ImportMap, infoReader: InfoReader) = 

    let tcVal = CheckExpressions.LightweightTcValForUsingInBuildMethodCall g

    new(g: TcGlobals, thisCcu: CcuThunk, thisCcuTyp: ModuleOrNamespaceType option, tcImports: TcImports) =
        let amap = tcImports.GetImportMap()
        let infoReader = InfoReader(g, amap)
        SymbolEnv(g, thisCcu, thisCcuTyp, tcImports, amap, infoReader)

    member _.g = g
    member _.amap = amap
    member _.thisCcu = thisCcu
    member _.thisCcuTy = thisCcuTyp
    member _.infoReader = infoReader
    member _.tcImports = tcImports
    member _.tcValF = tcVal

[<AutoOpen>]
module Impl = 
    let protect f = 
       ErrorLogger.protectAssemblyExplorationF  
         (fun (asmName, path) -> invalidOp (sprintf "The entity or value '%s' does not exist or is in an unresolved assembly. You may need to add a reference to assembly '%s'" path asmName))
         f

    let makeReadOnlyCollection (arr: seq<'T>) = 
        System.Collections.ObjectModel.ReadOnlyCollection<_>(Seq.toArray arr) :> IList<_>
        
    let makeXmlDoc (doc: XmlDoc) =
        FSharpXmlDoc.FromXmlText doc
    
    let makeElaboratedXmlDoc (doc: XmlDoc) =
        makeReadOnlyCollection (doc.GetElaboratedXmlLines())
    
    let rescopeEntity optViewedCcu (entity: Entity) = 
        match optViewedCcu with 
        | None -> mkLocalEntityRef entity
        | Some viewedCcu -> 
        match tryRescopeEntity viewedCcu entity with
        | ValueNone -> mkLocalEntityRef entity
        | ValueSome eref -> eref

    let entityIsUnresolved(entity:EntityRef) = 
        match entity with
        | ERefNonLocal(NonLocalEntityRef(ccu, _)) -> 
            ccu.IsUnresolvedReference && entity.TryDeref.IsNone
        | _ -> false

    let checkEntityIsResolved(entity:EntityRef) = 
        if entityIsUnresolved entity then 
            let poorQualifiedName =
                if entity.nlr.AssemblyName = "mscorlib" then 
                    entity.nlr.DisplayName + ", mscorlib"
                else 
                    entity.nlr.DisplayName + ", " + entity.nlr.Ccu.AssemblyName
            invalidOp (sprintf "The entity '%s' does not exist or is in an unresolved assembly." poorQualifiedName)

    /// Checking accessibility that arise from different compilations needs more care - this is a duplicate of the F# compiler code for this case
    let checkForCrossProjectAccessibility (ilg: ILGlobals) (thisCcu2:CcuThunk, ad2) (thisCcu1, taccess1) = 
        match ad2 with 
        | AccessibleFrom(cpaths2, _) ->
            let nameOfScoRef (thisCcu:CcuThunk) scoref = 
                match scoref with 
                | ILScopeRef.Local -> thisCcu.AssemblyName 
                | ILScopeRef.Assembly aref -> aref.Name 
                | ILScopeRef.Module mref -> mref.Name
                | ILScopeRef.PrimaryAssembly -> ilg.primaryAssemblyName
            let canAccessCompPathFromCrossProject (CompPath(scoref1, cpath1)) (CompPath(scoref2, cpath2)) =
                let rec loop p1 p2  = 
                    match p1, p2 with 
                    | (a1, k1) :: rest1, (a2, k2) :: rest2 -> (a1=a2) && (k1=k2) && loop rest1 rest2
                    | [], _ -> true 
                    | _ -> false // cpath1 is longer
                loop cpath1 cpath2 &&
                nameOfScoRef thisCcu1 scoref1 = nameOfScoRef thisCcu2 scoref2
            let canAccessFromCrossProject (TAccess x1) cpath2 = x1 |> List.forall (fun cpath1 -> canAccessCompPathFromCrossProject cpath1 cpath2)
            cpaths2 |> List.exists (canAccessFromCrossProject taccess1) 
        | _ -> true // otherwise use the normal check


    /// Convert an IL member accessibility into an F# accessibility
    let getApproxFSharpAccessibilityOfMember (declaringEntity: EntityRef) (ilAccess: ILMemberAccess) = 
        match ilAccess with 
        | ILMemberAccess.CompilerControlled
        | ILMemberAccess.FamilyAndAssembly 
        | ILMemberAccess.Assembly -> 
            taccessPrivate  (CompPath(declaringEntity.CompilationPath.ILScopeRef, []))

        | ILMemberAccess.Private ->
            taccessPrivate  declaringEntity.CompilationPath

        // This is an approximation - the thing may actually be nested in a private class, in which case it is not actually "public"
        | ILMemberAccess.Public
        // This is an approximation - the thing is actually "protected", but F# accessibilities can't express "protected", so we report it as "public"
        | ILMemberAccess.FamilyOrAssembly
        | ILMemberAccess.Family ->
            taccessPublic 

    /// Convert an IL type definition accessibility into an F# accessibility
    let getApproxFSharpAccessibilityOfEntity (entity: EntityRef) = 
        match metadataOfTycon entity.Deref with 
#if !NO_EXTENSIONTYPING
        | ProvidedTypeMetadata _info -> 
            // This is an approximation - for generative type providers some type definitions can be private.
            taccessPublic
#endif

        | ILTypeMetadata (TILObjectReprData(_, _, td)) -> 
            match td.Access with 
            | ILTypeDefAccess.Public 
            | ILTypeDefAccess.Nested ILMemberAccess.Public -> taccessPublic 
            | ILTypeDefAccess.Private  -> taccessPrivate  (CompPath(entity.CompilationPath.ILScopeRef, []))
            | ILTypeDefAccess.Nested nested -> getApproxFSharpAccessibilityOfMember entity nested

        | FSharpOrArrayOrByrefOrTupleOrExnTypeMetadata -> 
            entity.Accessibility

    let getLiteralValue = function
        | Some lv  ->
            match lv with
            | Const.Bool    v -> Some(box v)
            | Const.SByte   v -> Some(box v)
            | Const.Byte    v -> Some(box v)
            | Const.Int16   v -> Some(box v)
            | Const.UInt16  v -> Some(box v)
            | Const.Int32   v -> Some(box v)
            | Const.UInt32  v -> Some(box v)
            | Const.Int64   v -> Some(box v)
            | Const.UInt64  v -> Some(box v)
            | Const.IntPtr  v -> Some(box v)
            | Const.UIntPtr v -> Some(box v)
            | Const.Single  v -> Some(box v)
            | Const.Double  v -> Some(box v)
            | Const.Char    v -> Some(box v)
            | Const.String  v -> Some(box v)
            | Const.Decimal v -> Some(box v)
            | Const.Unit
            | Const.Zero      -> None
        | None -> None
            

    let getXmlDocSigForEntity (cenv: SymbolEnv) (ent:EntityRef)=
        match GetXmlDocSigOfEntityRef cenv.infoReader ent.Range ent with
        | Some (_, docsig) -> docsig
        | _ -> ""

type FSharpDisplayContext(denv: TcGlobals -> DisplayEnv) = 
    member x.Contents g = denv g

    static member Empty = FSharpDisplayContext(fun g -> DisplayEnv.Empty g)

    member x.WithShortTypeNames shortNames =
         FSharpDisplayContext(fun g -> { denv g with shortTypeNames = shortNames })

    member x.WithPrefixGenericParameters () =
        FSharpDisplayContext(fun g -> { denv g with genericParameterStyle = GenericParameterStyle.Prefix }  )

    member x.WithSuffixGenericParameters () =
        FSharpDisplayContext(fun g -> { denv g with genericParameterStyle = GenericParameterStyle.Suffix }  )

// delay the realization of 'item' in case it is unresolved
type FSharpSymbol(cenv: SymbolEnv, item: (unit -> Item), access: (FSharpSymbol -> CcuThunk -> AccessorDomain -> bool)) =

    member x.Assembly = 
        let ccu = defaultArg (SymbolHelpers.ccuOfItem cenv.g x.Item) cenv.thisCcu 
        FSharpAssembly(cenv, ccu)

    member x.IsAccessible(rights: FSharpAccessibilityRights) = access x rights.ThisCcu rights.Contents

    member x.IsExplicitlySuppressed = SymbolHelpers.IsExplicitlySuppressed cenv.g x.Item

    member x.FullName = SymbolHelpers.FullNameOfItem cenv.g x.Item 

    member x.DeclarationLocation = SymbolHelpers.rangeOfItem cenv.g None x.Item

    member x.ImplementationLocation = SymbolHelpers.rangeOfItem cenv.g (Some false) x.Item

    member x.SignatureLocation = SymbolHelpers.rangeOfItem cenv.g (Some true) x.Item

    member x.IsEffectivelySameAs(other:FSharpSymbol) = 
        x.Equals other || ItemsAreEffectivelyEqual cenv.g x.Item other.Item

    member x.GetEffectivelySameAsHash() = ItemsAreEffectivelyEqualHash cenv.g x.Item

    member internal _.Item = item()

    member _.DisplayName = item().DisplayName

    // This is actually overridden in all cases below. However some symbols are still just of type FSharpSymbol, 
    // see 'FSharpSymbol.Create' further below.
    override x.Equals(other: obj) =
        box x === other ||
        match other with
        |   :? FSharpSymbol as otherSymbol -> ItemsAreEffectivelyEqual cenv.g x.Item otherSymbol.Item
        |   _ -> false

    override x.GetHashCode() = hash x.ImplementationLocation  

    override x.ToString() = "symbol " + (try item().DisplayName with _ -> "?")

    // TODO: there are several cases where we may need to report more interesting
    // symbol information below. By default we return a vanilla symbol.
    static member Create(g, thisCcu, thisCcuTyp, tcImports, item): FSharpSymbol = 
        FSharpSymbol.Create(SymbolEnv(g, thisCcu, Some thisCcuTyp, tcImports), item)

    static member Create(cenv, item): FSharpSymbol = 
        let dflt() = FSharpSymbol(cenv, (fun () -> item), (fun _ _ _ -> true)) 
        match item with 
        | Item.Value v -> FSharpMemberOrFunctionOrValue(cenv, V v, item) :> _
        | Item.UnionCase (uinfo, _) -> FSharpUnionCase(cenv, uinfo.UnionCaseRef) :> _
        | Item.ExnCase tcref -> FSharpEntity(cenv, tcref) :>_
        | Item.RecdField rfinfo -> FSharpField(cenv, RecdOrClass rfinfo.RecdFieldRef) :> _
        | Item.UnionCaseField (UnionCaseInfo (_, ucref), index) -> FSharpField (cenv, Union (ucref, index)) :> _

        | Item.ILField finfo -> FSharpField(cenv, ILField finfo) :> _

        | Item.AnonRecdField (anonInfo, tinst, n, m) -> FSharpField(cenv,  AnonField (anonInfo, tinst, n, m)) :> _
        
        | Item.Event einfo -> 
            FSharpMemberOrFunctionOrValue(cenv, E einfo, item) :> _
            
        | Item.Property(_, pinfo :: _) -> 
            FSharpMemberOrFunctionOrValue(cenv, P pinfo, item) :> _
            
        | Item.MethodGroup(_, minfo :: _, _) -> 
            FSharpMemberOrFunctionOrValue(cenv, M minfo, item) :> _

        | Item.CtorGroup(_, cinfo :: _) -> 
            FSharpMemberOrFunctionOrValue(cenv, C cinfo, item) :> _

        | Item.DelegateCtor (AbbrevOrAppTy tcref) -> 
            FSharpEntity(cenv, tcref) :>_ 

        | Item.UnqualifiedType(tcref :: _)  
        | Item.Types(_, AbbrevOrAppTy tcref :: _) -> 
            FSharpEntity(cenv, tcref) :>_  

        | Item.ModuleOrNamespaces(modref :: _) ->  
            FSharpEntity(cenv, modref) :> _

        | Item.SetterArg (_id, item) -> FSharpSymbol.Create(cenv, item)

        | Item.CustomOperation (_customOpName, _, Some minfo) -> 
            FSharpMemberOrFunctionOrValue(cenv, M minfo, item) :> _

        | Item.CustomBuilder (_, vref) -> 
            FSharpMemberOrFunctionOrValue(cenv, V vref, item) :> _

        | Item.TypeVar (_, tp) ->
             FSharpGenericParameter(cenv, tp) :> _

        | Item.ActivePatternCase apref -> 
             FSharpActivePatternCase(cenv, apref.ActivePatternInfo, apref.ActivePatternVal.Type, apref.CaseIndex, Some apref.ActivePatternVal, item) :> _

        | Item.ActivePatternResult (apinfo, ty, n, _) ->
             FSharpActivePatternCase(cenv, apinfo, ty, n, None, item) :> _

        | Item.ArgName(id, ty, argOwner) ->
            FSharpParameter(cenv, id, ty, argOwner) :> _

        | Item.ImplicitOp(_, { contents = Some(TraitConstraintSln.FSMethSln(_, vref, _)) }) ->
            FSharpMemberOrFunctionOrValue(cenv, V vref, item) :> _

        // TODO: the following don't currently return any interesting subtype
        | Item.ImplicitOp _
        | Item.ILField _ 
        | Item.FakeInterfaceCtor _
        | Item.NewDef _ -> dflt()
        // These cases cover unreachable cases
        | Item.CustomOperation (_, _, None) 
        | Item.UnqualifiedType []
        | Item.ModuleOrNamespaces []
        | Item.Property (_, [])
        | Item.MethodGroup (_, [], _)
        | Item.CtorGroup (_, [])
        // These cases cover misc. corned cases (non-symbol types)
        | Item.Types _
        | Item.DelegateCtor _  -> dflt()

    abstract Accessibility: FSharpAccessibility
    default _.Accessibility = FSharpAccessibility(taccessPublic)
        
    abstract Attributes: IList<FSharpAttribute>
    default _.Attributes = makeReadOnlyCollection[]

    member sym.HasAttribute<'T> () =
        sym.Attributes |> Seq.exists (fun attr -> attr.IsAttribute<'T>())

    member sym.TryGetAttribute<'T>() =
        sym.Attributes |> Seq.tryFind (fun attr -> attr.IsAttribute<'T>())

type FSharpEntity(cenv: SymbolEnv, entity:EntityRef) = 
    inherit FSharpSymbol(cenv, 
                         (fun () -> 
                              checkEntityIsResolved entity
                              if entity.IsModuleOrNamespace then Item.ModuleOrNamespaces [entity] 
                              else Item.UnqualifiedType [entity]), 
                         (fun _this thisCcu2 ad -> 
                             checkForCrossProjectAccessibility cenv.g.ilg (thisCcu2, ad) (cenv.thisCcu, getApproxFSharpAccessibilityOfEntity entity)) 
                             // && AccessibilityLogic.IsEntityAccessible cenv.amap range0 ad entity)
                             )

    // If an entity is in an assembly not available to us in the resolution set, 
    // we generally return "false" from predicates like IsClass, since we know
    // nothing about that type.
    let isResolvedAndFSharp() = 
        match entity with
        | ERefNonLocal(NonLocalEntityRef(ccu, _)) -> not ccu.IsUnresolvedReference && ccu.IsFSharp
        | _ -> cenv.thisCcu.IsFSharp

    let isUnresolved() = entityIsUnresolved entity
    let isResolved() = not (isUnresolved())
    let checkIsResolved() = checkEntityIsResolved entity

    let isDefinedInFSharpCore() =
        match ccuOfTyconRef entity with
        | None -> false
        | Some ccu -> ccuEq ccu cenv.g.fslibCcu

    member _.Entity = entity
        
    member _.LogicalName = 
        checkIsResolved()
        entity.LogicalName 

    member _.CompiledName = 
        checkIsResolved()
        entity.CompiledName 

    member _.DisplayName = 
        checkIsResolved()
        if entity.IsModuleOrNamespace then entity.DemangledModuleOrNamespaceName
        else entity.DisplayName 

    member _.AccessPath  = 
        checkIsResolved()
        match entity.CompilationPathOpt with 
        | None -> "global" 
        | Some (CompPath(_, [])) -> "global" 
        | Some cp -> buildAccessPath (Some cp)
    
    member x.DeclaringEntity = 
        match entity.CompilationPathOpt with 
        | None -> None
        | Some (CompPath(_, [])) -> None
        | Some cp -> 
            match x.Assembly.Contents.FindEntityByPath cp.MangledPath with
            | Some res -> Some res
            | None -> 
            // The declaring entity may be in this assembly, including a type possibly hidden by a signature.
            match cenv.thisCcuTy with 
            | Some t -> 
                let s = FSharpAssemblySignature(cenv, None, None, t)
                s.FindEntityByPath cp.MangledPath 
            | None -> None

    member _.Namespace  = 
        checkIsResolved()
        match entity.CompilationPathOpt with 
        | None -> None
        | Some (CompPath(_, [])) -> None
        | Some cp when cp.AccessPath |> List.forall (function (_, ModuleOrNamespaceKind.Namespace) -> true | _  -> false) -> 
            Some (buildAccessPath (Some cp))
        | Some _ -> None

    member x.QualifiedName = 
        checkIsResolved()
        let fail() = invalidOp (sprintf "the type '%s' does not have a qualified name" x.LogicalName)
#if !NO_EXTENSIONTYPING
        if entity.IsTypeAbbrev || entity.IsProvidedErasedTycon || entity.IsNamespace then fail()
        #else
        if entity.IsTypeAbbrev || entity.IsNamespace then fail()
#endif
        match entity.CompiledRepresentation with 
        | CompiledTypeRepr.ILAsmNamed(tref, _, _) -> tref.QualifiedName
        | CompiledTypeRepr.ILAsmOpen _ -> fail()
        
    member x.FullName = 
        checkIsResolved()
        match x.TryFullName with 
        | None -> invalidOp (sprintf "the type '%s' does not have a qualified name" x.LogicalName)
        | Some nm -> nm
    
    member x.TryFullName = 
        if isUnresolved() then None
#if !NO_EXTENSIONTYPING
        elif entity.IsTypeAbbrev || entity.IsProvidedErasedTycon then None
        #else
        elif entity.IsTypeAbbrev then None
#endif
        elif entity.IsNamespace  then Some entity.DemangledModuleOrNamespaceName
        else
            match entity.CompiledRepresentation with 
            | CompiledTypeRepr.ILAsmNamed(tref, _, _) -> Some tref.FullName
            | CompiledTypeRepr.ILAsmOpen _ -> None   

    member _.DeclarationLocation = 
        checkIsResolved()
        entity.Range

    member x.GenericParameters = 
        checkIsResolved()
        entity.TyparsNoRange |> List.map (fun tp -> FSharpGenericParameter(cenv, tp)) |> makeReadOnlyCollection

    member _.IsMeasure = 
        isResolvedAndFSharp() && (entity.TypeOrMeasureKind = TyparKind.Measure)

    member x.IsAbstractClass = 
        isResolved() && isAbstractTycon entity.Deref

    member _.IsFSharpModule = 
        isResolvedAndFSharp() && entity.IsModule

    member _.HasFSharpModuleSuffix = 
        isResolvedAndFSharp() && 
        entity.IsModule && 
        (entity.ModuleOrNamespaceType.ModuleOrNamespaceKind = ModuleOrNamespaceKind.FSharpModuleWithSuffix)

    member _.IsValueType  = 
        isResolved() &&
        entity.IsStructOrEnumTycon 

    member x.IsArrayType  = 
        isResolved() &&
        isArrayTyconRef cenv.g entity

    member _.ArrayRank  = 
        checkIsResolved()
        rankOfArrayTyconRef cenv.g entity

#if !NO_EXTENSIONTYPING
    member _.IsProvided  = 
        isResolved() &&
        entity.IsProvided

    member _.IsProvidedAndErased  = 
        isResolved() &&
        entity.IsProvidedErasedTycon

    member _.IsStaticInstantiation  = 
        isResolved() &&
        entity.IsStaticInstantiationTycon

    member _.IsProvidedAndGenerated  = 
        isResolved() &&
        entity.IsProvidedGeneratedTycon
#endif
    member _.IsClass = 
        isResolved() &&
        match metadataOfTycon entity.Deref with
#if !NO_EXTENSIONTYPING 
        | ProvidedTypeMetadata info -> info.IsClass
#endif
        | ILTypeMetadata (TILObjectReprData(_, _, td)) -> td.IsClass
        | FSharpOrArrayOrByrefOrTupleOrExnTypeMetadata -> entity.Deref.IsFSharpClassTycon

    member _.IsByRef = 
        isResolved() &&
        isByrefTyconRef cenv.g entity

    member _.IsOpaque = 
        isResolved() &&
        entity.IsHiddenReprTycon

    member _.IsInterface = 
        isResolved() &&
        isInterfaceTyconRef entity

    member _.IsDelegate = 
        isResolved() &&
        match metadataOfTycon entity.Deref with 
#if !NO_EXTENSIONTYPING
        | ProvidedTypeMetadata info -> info.IsDelegate ()
#endif
        | ILTypeMetadata (TILObjectReprData(_, _, td)) -> td.IsDelegate
        | FSharpOrArrayOrByrefOrTupleOrExnTypeMetadata -> entity.IsFSharpDelegateTycon

    member _.IsEnum = 
        isResolved() &&
        entity.IsEnumTycon
    
    member _.IsFSharpExceptionDeclaration = 
        isResolvedAndFSharp() && entity.IsExceptionDecl

    member _.IsUnresolved = 
        isUnresolved()

    member _.IsFSharp = 
        isResolvedAndFSharp()

    member _.IsFSharpAbbreviation = 
        isResolvedAndFSharp() && entity.IsTypeAbbrev 

    member _.IsFSharpRecord = 
        isResolvedAndFSharp() && entity.IsRecordTycon

    member _.IsFSharpUnion = 
        isResolvedAndFSharp() && entity.IsUnionTycon

    member _.HasAssemblyCodeRepresentation = 
        isResolvedAndFSharp() && (entity.IsAsmReprTycon || entity.IsMeasureableReprTycon)

    member _.FSharpDelegateSignature =
        checkIsResolved()
        match entity.TypeReprInfo with 
        | TFSharpObjectRepr r when entity.IsFSharpDelegateTycon -> 
            match r.fsobjmodel_kind with 
            | TTyconDelegate ss -> FSharpDelegateSignature(cenv, ss)
            | _ -> invalidOp "not a delegate type"
        | _ -> invalidOp "not a delegate type"
      

    override _.Accessibility = 
        if isUnresolved() then FSharpAccessibility taccessPublic else
        FSharpAccessibility(getApproxFSharpAccessibilityOfEntity entity) 

    member _.RepresentationAccessibility = 
        if isUnresolved() then FSharpAccessibility taccessPublic else
        FSharpAccessibility(entity.TypeReprAccessibility)

    member x.DeclaredInterfaces = 
        if isUnresolved() then makeReadOnlyCollection [] else
        ErrorLogger.protectAssemblyExploration [] (fun () -> 
            [ for ty in GetImmediateInterfacesOfType SkipUnrefInterfaces.Yes cenv.g cenv.amap range0 (generalizedTyconRef entity) do 
                 yield FSharpType(cenv, ty) ])
        |> makeReadOnlyCollection

    member x.AllInterfaces = 
        if isUnresolved() then makeReadOnlyCollection [] else
        ErrorLogger.protectAssemblyExploration [] (fun () -> 
            [ for ty in AllInterfacesOfType  cenv.g cenv.amap range0 AllowMultiIntfInstantiations.Yes (generalizedTyconRef entity) do 
                 yield FSharpType(cenv, ty) ])
        |> makeReadOnlyCollection
    
    member x.IsAttributeType =
        if isUnresolved() then false else
        let ty = generalizedTyconRef entity
        ErrorLogger.protectAssemblyExploration false <| fun () -> 
        Infos.ExistsHeadTypeInEntireHierarchy cenv.g cenv.amap range0 ty cenv.g.tcref_System_Attribute
        
    member x.IsDisposableType =
        if isUnresolved() then false else
        let ty = generalizedTyconRef entity
        ErrorLogger.protectAssemblyExploration false <| fun () -> 
        Infos.ExistsHeadTypeInEntireHierarchy cenv.g cenv.amap range0 ty cenv.g.tcref_System_IDisposable

    member x.BaseType = 
        checkIsResolved()        
        GetSuperTypeOfType cenv.g cenv.amap range0 (generalizedTyconRef entity) 
        |> Option.map (fun ty -> FSharpType(cenv, ty)) 
        
    member _.UsesPrefixDisplay = 
        if isUnresolved() then true else
        not (isResolvedAndFSharp()) || entity.Deref.IsPrefixDisplay

    member x.IsNamespace =  entity.IsNamespace

    member x.MembersFunctionsAndValues = 
      if isUnresolved() then makeReadOnlyCollection[] else
      protect <| fun () -> 
        ([ let _, entityTy = generalizeTyconRef entity
           let createMember (minfo: MethInfo) =
               if minfo.IsConstructor then FSharpMemberOrFunctionOrValue(cenv, C minfo, Item.CtorGroup (minfo.DisplayName, [minfo]))
               else FSharpMemberOrFunctionOrValue(cenv, M minfo, Item.MethodGroup (minfo.DisplayName, [minfo], None))
           if x.IsFSharpAbbreviation then 
               ()
           elif x.IsFSharp then 
               // For F# code we emit methods members in declaration order
               for v in entity.MembersOfFSharpTyconSorted do 
                 // Ignore members representing the generated .cctor
                 if not v.Deref.IsClassConstructor then 
                     yield createMember (FSMeth(cenv.g, entityTy, v, None))
           else
               for minfo in GetImmediateIntrinsicMethInfosOfType (None, AccessibleFromSomeFSharpCode) cenv.g cenv.amap range0 entityTy do
                    yield createMember minfo
           let props = GetImmediateIntrinsicPropInfosOfType (None, AccessibleFromSomeFSharpCode) cenv.g cenv.amap range0 entityTy
           let events = cenv.infoReader.GetImmediateIntrinsicEventsOfType (None, AccessibleFromSomeFSharpCode, range0, entityTy)
           for pinfo in props do
                yield FSharpMemberOrFunctionOrValue(cenv, P pinfo, Item.Property (pinfo.PropertyName, [pinfo]))
           for einfo in events do
                yield FSharpMemberOrFunctionOrValue(cenv, E einfo, Item.Event einfo)

           // Emit the values, functions and F#-declared extension members in a module
           for v in entity.ModuleOrNamespaceType.AllValsAndMembers do
               if v.IsExtensionMember then

                   // For F#-declared extension members, yield a value-backed member and a property info if possible
                   let vref = mkNestedValRef entity v
                   yield FSharpMemberOrFunctionOrValue(cenv, V vref, Item.Value vref) 
                   match v.MemberInfo.Value.MemberFlags.MemberKind, v.ApparentEnclosingEntity with
                   | SynMemberKind.PropertyGet, Parent p -> 
                        let pinfo = FSProp(cenv.g, generalizedTyconRef p, Some vref, None)
                        yield FSharpMemberOrFunctionOrValue(cenv, P pinfo, Item.Property (pinfo.PropertyName, [pinfo]))
                   | SynMemberKind.PropertySet, Parent p -> 
                        let pinfo = FSProp(cenv.g, generalizedTyconRef p, None, Some vref)
                        yield FSharpMemberOrFunctionOrValue(cenv, P pinfo, Item.Property (pinfo.PropertyName, [pinfo]))
                   | _ -> ()

               elif not v.IsMember then
                   let vref = mkNestedValRef entity v
                   yield FSharpMemberOrFunctionOrValue(cenv, V vref, Item.Value vref) ]  
         |> makeReadOnlyCollection)
 
    member _.XmlDocSig = 
        checkIsResolved()
        getXmlDocSigForEntity cenv entity
 
    member _.XmlDoc = 
        if isUnresolved() then XmlDoc.Empty  |> makeXmlDoc else
        entity.XmlDoc |> makeXmlDoc

    member _.ElaboratedXmlDoc = 
        if isUnresolved() then XmlDoc.Empty  |> makeElaboratedXmlDoc else
        entity.XmlDoc |> makeElaboratedXmlDoc

    member x.StaticParameters = 
        match entity.TypeReprInfo with 
#if !NO_EXTENSIONTYPING
        | TProvidedTypeExtensionPoint info -> 
            let m = x.DeclarationLocation
            let typeBeforeArguments = info.ProvidedType 
            let staticParameters = typeBeforeArguments.PApplyWithProvider((fun (typeBeforeArguments, provider) -> typeBeforeArguments.GetStaticParameters provider), range=m) 
            let staticParameters = staticParameters.PApplyArray(id, "GetStaticParameters", m)
            [| for p in staticParameters -> FSharpStaticParameter(cenv, p, m) |]
#endif
        | _ -> [| |]
      |> makeReadOnlyCollection

    member _.NestedEntities = 
        if isUnresolved() then makeReadOnlyCollection[] else
        entity.ModuleOrNamespaceType.AllEntities 
        |> QueueList.toList
        |> List.map (fun x -> FSharpEntity(cenv, entity.NestedTyconRef x))
        |> makeReadOnlyCollection

    member x.UnionCases = 
        if isUnresolved() then makeReadOnlyCollection[] else
        entity.UnionCasesAsRefList
        |> List.map (fun x -> FSharpUnionCase(cenv, x)) 
        |> makeReadOnlyCollection

    member x.FSharpFields =
        if isUnresolved() then makeReadOnlyCollection[] else
    
        if entity.IsILEnumTycon then
            let (TILObjectReprData(_scoref, _enc, tdef)) = entity.ILTyconInfo
            let formalTypars = entity.Typars(range.Zero)
            let formalTypeInst = generalizeTypars formalTypars
            let ty = TType_app(entity, formalTypeInst)
            let formalTypeInfo = ILTypeInfo.FromType cenv.g ty
            tdef.Fields.AsList
            |> List.map (fun tdef -> let ilFieldInfo = ILFieldInfo(formalTypeInfo, tdef)
                                     FSharpField(cenv, FSharpFieldData.ILField ilFieldInfo ))
            |> makeReadOnlyCollection

        else
            entity.AllFieldsAsList
            |> List.map (fun x -> FSharpField(cenv, mkRecdFieldRef entity x.Name))
            |> makeReadOnlyCollection

    member x.AbbreviatedType   = 
        checkIsResolved()

        match entity.TypeAbbrev with
        | None -> invalidOp "not a type abbreviation"
        | Some ty -> FSharpType(cenv, ty)

    override _.Attributes = 
        if isUnresolved() then makeReadOnlyCollection[] else
        GetAttribInfosOfEntity cenv.g cenv.amap range0 entity
        |> List.map (fun a -> FSharpAttribute(cenv, a))
        |> makeReadOnlyCollection

    member _.AllCompilationPaths =
        checkIsResolved()
        let (CompPath(_, parts)) = entity.CompilationPath
        let partsList =
            [ yield parts
              match parts with
              | ("Microsoft", ModuleOrNamespaceKind.Namespace) :: rest when isDefinedInFSharpCore() -> yield rest
              | _ -> ()]

        let mapEachCurrentPath (paths: string list list) path =
            match paths with
            | [] -> [[path]]
            | _ -> paths |> List.map (fun x -> path :: x)

        let walkParts (parts: (string * ModuleOrNamespaceKind) list) =
            let rec loop (currentPaths: string list list) parts =
                match parts with
                | [] -> currentPaths
                | (name: string, kind) :: rest ->
                    match kind with
                    | ModuleOrNamespaceKind.FSharpModuleWithSuffix ->
                        [ yield! loop (mapEachCurrentPath currentPaths name) rest
                          yield! loop (mapEachCurrentPath currentPaths (name.[..name.Length - 7])) rest ]
                    | _ -> 
                       loop (mapEachCurrentPath currentPaths name) rest
            loop [] parts |> List.map (List.rev >> String.concat ".")
            
        let res =
            [ for parts in partsList do
                yield! walkParts parts ]
        res

    member x.ActivePatternCases =
        protect <| fun () -> 
            ActivePatternElemsOfModuleOrNamespace cenv.g x.Entity
            |> Map.toList
            |> List.map (fun (_, apref) ->
                let item = Item.ActivePatternCase apref
                FSharpActivePatternCase(cenv, apref.ActivePatternInfo, apref.ActivePatternVal.Type, apref.CaseIndex, Some apref.ActivePatternVal, item))

    member x.TryGetFullName() =
        try x.TryFullName 
        with _ -> 
            try Some(String.Join(".", x.AccessPath, x.DisplayName))
            with _ -> None

    member x.TryGetFullDisplayName() =
        let fullName = x.TryGetFullName() |> Option.map (fun fullName -> fullName.Split '.')
        let res = 
            match fullName with
            | Some fullName ->
                match Option.attempt (fun _ -> x.DisplayName) with
                | Some shortDisplayName when not (shortDisplayName.Contains ".") ->
                    Some (fullName |> Array.replace (fullName.Length - 1) shortDisplayName)
                | _ -> Some fullName
            | None -> None 
            |> Option.map (fun fullDisplayName -> String.Join (".", fullDisplayName))
        //debug "GetFullDisplayName: FullName = %A, Result = %A" fullName res
        res

    member x.TryGetFullCompiledName() =
        let fullName = x.TryGetFullName() |> Option.map (fun fullName -> fullName.Split '.')
        let res = 
            match fullName with
            | Some fullName ->
                match Option.attempt (fun _ -> x.CompiledName) with
                | Some shortCompiledName when not (shortCompiledName.Contains ".") ->
                    Some (fullName |> Array.replace (fullName.Length - 1) shortCompiledName)
                | _ -> Some fullName
            | None -> None 
            |> Option.map (fun fullDisplayName -> String.Join (".", fullDisplayName))
        //debug "GetFullCompiledName: FullName = %A, Result = %A" fullName res
        res

    member x.GetPublicNestedEntities() =
        x.NestedEntities |> Seq.filter (fun entity -> entity.Accessibility.IsPublic)

    member x.TryGetMembersFunctionsAndValues() = 
        try x.MembersFunctionsAndValues with _ -> [||] :> _

    member this.TryGetMetadataText() =
        match entity.TryDeref with
        | ValueSome _ ->
            if entity.IsNamespace then None
            else

            let denv = DisplayEnv.InitialForSigFileGeneration cenv.g

            let extraOpenPath =
                match entity.CompilationPathOpt with
                | Some cpath ->
                    let rec getOpenPath accessPath acc =
                        match accessPath with
                        | [] -> acc
                        | (name, ModuleOrNamespaceKind.ModuleOrType) :: accessPath ->
                            getOpenPath accessPath (name :: acc)
                        | (name, ModuleOrNamespaceKind.Namespace) :: accessPath ->
                            getOpenPath accessPath (name :: acc)
                        | (name, ModuleOrNamespaceKind.FSharpModuleWithSuffix) :: accessPath ->
                            getOpenPath accessPath (name :: acc)

                    getOpenPath cpath.AccessPath []
                | _ -> 
                    []
                |> List.rev

            let needOpenType =
                match entity.CompilationPathOpt with
                | Some cpath ->
                    match cpath.AccessPath with
                    | (_, ModuleOrNamespaceKind.ModuleOrType) :: _ ->
                        match this.DeclaringEntity with
                        | Some (declaringEntity: FSharpEntity) -> not declaringEntity.IsFSharpModule
                        | _ -> false
                    | _ -> false
                | _ ->
                    false

            let denv = denv.AddOpenPath extraOpenPath

            let infoReader = cenv.infoReader

            let openPathL =
                extraOpenPath
                |> List.map (fun x -> Layout.wordL (TaggedText.tagUnknownEntity x))

            let pathL =
                if List.isEmpty extraOpenPath then
                    Layout.emptyL
                else
                    Layout.sepListL (Layout.sepL TaggedText.dot) openPathL
                    
            let headerL =
                if List.isEmpty extraOpenPath then
                    Layout.emptyL
                else
                    Layout.(^^)
                        (Layout.wordL (TaggedText.tagKeyword "namespace"))
                        pathL

            let openL = 
                if List.isEmpty openPathL then Layout.emptyL
                else
                    let openKeywordL =
                        if needOpenType then
                            Layout.(^^)
                                (Layout.wordL (TaggedText.tagKeyword "open"))
                                (Layout.wordL TaggedText.keywordType)
                        else
                            Layout.wordL (TaggedText.tagKeyword "open")                            
                    Layout.(^^)
                        openKeywordL
                        pathL

            Layout.aboveListL
                [
                    (Layout.(^^) headerL (Layout.sepL TaggedText.lineBreak))
                    (Layout.(^^) openL (Layout.sepL TaggedText.lineBreak))
                    (NicePrint.layoutEntityRef denv infoReader AccessibleFromSomewhere range0 entity)
                ]
            |> LayoutRender.showL
            |> SourceText.ofString
            |> Some
        | _ ->
            None

    override x.Equals(other: obj) =
        box x === other ||
        match other with
        |   :? FSharpEntity as otherEntity -> tyconRefEq cenv.g entity otherEntity.Entity
        |   _ -> false

    override x.GetHashCode() =
        checkIsResolved()
        ((hash entity.Stamp) <<< 1) + 1

    override x.ToString() = x.CompiledName

type FSharpUnionCase(cenv, v: UnionCaseRef) =
    inherit FSharpSymbol (cenv,  
                          (fun () -> 
                               checkEntityIsResolved v.TyconRef
                               Item.UnionCase(UnionCaseInfo(generalizeTypars v.TyconRef.TyparsNoRange, v), false)), 
                          (fun _this thisCcu2 ad -> 
                               checkForCrossProjectAccessibility cenv.g.ilg (thisCcu2, ad) (cenv.thisCcu, v.UnionCase.Accessibility)) 
                               //&& AccessibilityLogic.IsUnionCaseAccessible cenv.amap range0 ad v)
                               )


    let isUnresolved() =
        entityIsUnresolved v.TyconRef || v.TryUnionCase.IsNone 
        
    let checkIsResolved() = 
        checkEntityIsResolved v.TyconRef
        if v.TryUnionCase.IsNone then 
            invalidOp (sprintf "The union case '%s' could not be found in the target type" v.CaseName)

    member _.IsUnresolved = 
        isUnresolved()

    member _.Name = 
        checkIsResolved()
        v.UnionCase.DisplayName

    member _.DeclarationLocation = 
        checkIsResolved()
        v.Range

    member _.HasFields =
        if isUnresolved() then false else
        v.UnionCase.RecdFieldsArray.Length <> 0

    member _.Fields = 
        if isUnresolved() then makeReadOnlyCollection [] else
        v.UnionCase.RecdFieldsArray |> Array.mapi (fun i _ ->  FSharpField(cenv, FSharpFieldData.Union (v, i))) |> makeReadOnlyCollection

    member _.ReturnType = 
        checkIsResolved()
        FSharpType(cenv, v.ReturnType)

    member _.CompiledName = 
        checkIsResolved()
        v.UnionCase.CompiledName

    member _.XmlDocSig = 
        checkIsResolved()
        let unionCase = UnionCaseInfo(generalizeTypars v.TyconRef.TyparsNoRange, v)
        match GetXmlDocSigOfUnionCaseRef unionCase.UnionCaseRef with
        | Some (_, docsig) -> docsig
        | _ -> ""

    member _.XmlDoc = 
        if isUnresolved() then XmlDoc.Empty  |> makeXmlDoc else
        v.UnionCase.XmlDoc |> makeXmlDoc

    member _.ElaboratedXmlDoc = 
        if isUnresolved() then XmlDoc.Empty  |> makeElaboratedXmlDoc else
        v.UnionCase.XmlDoc |> makeElaboratedXmlDoc

    override _.Attributes = 
        if isUnresolved() then makeReadOnlyCollection [] else
        v.Attribs |> List.map (fun a -> FSharpAttribute(cenv, AttribInfo.FSAttribInfo(cenv.g, a))) |> makeReadOnlyCollection

    override _.Accessibility =  
        if isUnresolved() then FSharpAccessibility taccessPublic else
        FSharpAccessibility(v.UnionCase.Accessibility)

    member private x.V = v
    override x.Equals(other: obj) =
        box x === other ||
        match other with
        |   :? FSharpUnionCase as uc -> v === uc.V
        |   _ -> false

    override x.GetHashCode() = hash v.CaseName

    override x.ToString() = x.CompiledName

type FSharpFieldData = 
    | AnonField of AnonRecdTypeInfo * TTypes * int * range
    | ILField of ILFieldInfo
    | RecdOrClass of RecdFieldRef
    | Union of UnionCaseRef * int

    member x.TryRecdField =
        match x with 
        | AnonField (anonInfo, tinst, n, m) -> (anonInfo, tinst, n, m) |> Choice3Of3
        | RecdOrClass v -> v.RecdField |> Choice1Of3
        | Union (v, n) -> v.FieldByIndex n |> Choice1Of3
        | ILField f -> f |> Choice2Of3

    member x.TryDeclaringTyconRef =
        match x with 
        | RecdOrClass v -> Some v.TyconRef
        | ILField f -> Some f.DeclaringTyconRef
        | _ -> None

type FSharpAnonRecordTypeDetails(cenv: SymbolEnv, anonInfo: AnonRecdTypeInfo)  =
    member _.Assembly = FSharpAssembly (cenv, anonInfo.Assembly)

    /// Names of any enclosing types of the compiled form of the anonymous type (if the anonymous type was defined as a nested type)
    member _.EnclosingCompiledTypeNames = anonInfo.ILTypeRef.Enclosing

    /// The name of the compiled form of the anonymous type
    member _.CompiledName = anonInfo.ILTypeRef.Name

    /// The sorted labels of the anonymous type
    member _.SortedFieldNames = anonInfo.SortedNames

type FSharpField(cenv: SymbolEnv, d: FSharpFieldData)  =
    inherit FSharpSymbol (cenv, 
                          (fun () -> 
                                match d with 
                                | AnonField (anonInfo, tinst, n, m) -> 
                                    Item.AnonRecdField(anonInfo, tinst, n, m)
                                | RecdOrClass v -> 
                                    checkEntityIsResolved v.TyconRef
                                    Item.RecdField(RecdFieldInfo(generalizeTypars v.TyconRef.TyparsNoRange, v))
                                | Union (v, fieldIndex) ->
                                    checkEntityIsResolved v.TyconRef
                                    Item.UnionCaseField (UnionCaseInfo (generalizeTypars v.TyconRef.TyparsNoRange, v), fieldIndex)
                                | ILField f -> 
                                    Item.ILField f), 
                          (fun this thisCcu2 ad -> 
                                checkForCrossProjectAccessibility cenv.g.ilg (thisCcu2, ad) (cenv.thisCcu, (this :?> FSharpField).Accessibility.Contents)) 
                                //&&
                                //match d with 
                                //| Recd v -> AccessibilityLogic.IsRecdFieldAccessible cenv.amap range0 ad v
                                //| Union (v, _) -> AccessibilityLogic.IsUnionCaseAccessible cenv.amap range0 ad v)
                                )

    let isUnresolved() = 
        d.TryDeclaringTyconRef |> Option.exists entityIsUnresolved ||
        match d with
        | AnonField _ -> false
        | RecdOrClass v -> v.TryRecdField.IsNone 
        | Union (v, _) -> v.TryUnionCase.IsNone 
        | ILField _ -> false

    let checkIsResolved() = 
        d.TryDeclaringTyconRef |> Option.iter checkEntityIsResolved 
        match d with 
        | AnonField _ -> ()
        | RecdOrClass v -> 
            if v.TryRecdField.IsNone then 
                invalidOp (sprintf "The record field '%s' could not be found in the target type" v.FieldName)
        | Union (v, _) -> 
            if v.TryUnionCase.IsNone then 
                invalidOp (sprintf "The union case '%s' could not be found in the target type" v.CaseName)
        | ILField _ -> ()

    new (cenv, ucref: UnionCaseRef, n) = FSharpField(cenv, FSharpFieldData.Union(ucref, n))

    new (cenv, rfref: RecdFieldRef) = FSharpField(cenv, FSharpFieldData.RecdOrClass rfref)

    member _.DeclaringEntity = 
        d.TryDeclaringTyconRef |> Option.map (fun tcref -> FSharpEntity(cenv, tcref))

    member _.IsUnresolved = 
        isUnresolved()

    member _.IsMutable = 
        if isUnresolved() then false else 
        match d.TryRecdField with 
        | Choice1Of3 r -> r.IsMutable
        | Choice2Of3 f -> not f.IsInitOnly && f.LiteralValue.IsNone
        | Choice3Of3 _ -> false

    member _.IsLiteral = 
        if isUnresolved() then false else 
        match d.TryRecdField with 
        | Choice1Of3 r -> r.LiteralValue.IsSome
        | Choice2Of3 f -> f.LiteralValue.IsSome
        | Choice3Of3 _ -> false

    member _.LiteralValue = 
        if isUnresolved() then None else 
        match d.TryRecdField with 
        | Choice1Of3 r -> getLiteralValue r.LiteralValue
        | Choice2Of3 f -> f.LiteralValue |> Option.map (fun v -> v.AsObject())
        | Choice3Of3 _ -> None

    member _.IsVolatile = 
        if isUnresolved() then false else 
        match d.TryRecdField with 
        | Choice1Of3 r -> r.IsVolatile
        | Choice2Of3 _ -> false // F# doesn't actually respect "volatile" from other assemblies in any case
        | Choice3Of3 _ -> false

    member _.IsDefaultValue = 
        if isUnresolved() then false else 
        match d.TryRecdField with 
        | Choice1Of3 r -> r.IsZeroInit
        | Choice2Of3 _ -> false 
        | Choice3Of3 _ -> false

    member _.IsAnonRecordField = 
        match d with 
        | AnonField _ -> true
        | _ -> false

    member _.AnonRecordFieldDetails = 
        match d with 
        | AnonField (anonInfo, types, n, _) -> FSharpAnonRecordTypeDetails(cenv, anonInfo), [| for ty in types -> FSharpType(cenv, ty) |], n
        | _ -> invalidOp "not an anonymous record field"

    member _.IsUnionCaseField = 
        match d with 
        | Union _ -> true
        | _ -> false

    member _.DeclaringUnionCase =
        match d with
        | Union (v, _) -> Some (FSharpUnionCase (cenv, v))
        | _ -> None

    member _.XmlDocSig = 
        checkIsResolved()
        let xmlsig =
            match d with 
            | RecdOrClass v -> 
                let recd = RecdFieldInfo(generalizeTypars v.TyconRef.TyparsNoRange, v)
                GetXmlDocSigOfRecdFieldRef recd.RecdFieldRef
            | Union (v, _) -> 
                let unionCase = UnionCaseInfo(generalizeTypars v.TyconRef.TyparsNoRange, v)
                GetXmlDocSigOfUnionCaseRef unionCase.UnionCaseRef
            | ILField f -> 
                GetXmlDocSigOfILFieldInfo cenv.infoReader range0 f
            | AnonField _ -> None
        match xmlsig with
        | Some (_, docsig) -> docsig
        | _ -> ""

    member _.XmlDoc = 
        if isUnresolved() then XmlDoc.Empty  |> makeXmlDoc else
        match d.TryRecdField with 
        | Choice1Of3 r -> r.XmlDoc 
        | Choice2Of3 _ -> XmlDoc.Empty
        | Choice3Of3 _ -> XmlDoc.Empty
        |> makeXmlDoc

    member _.ElaboratedXmlDoc = 
        if isUnresolved() then XmlDoc.Empty  |> makeElaboratedXmlDoc else
        match d.TryRecdField with 
        | Choice1Of3 r -> r.XmlDoc 
        | Choice2Of3 _ -> XmlDoc.Empty
        | Choice3Of3 _ -> XmlDoc.Empty
        |> makeElaboratedXmlDoc

    member _.FieldType = 
        checkIsResolved()
        let fty = 
            match d.TryRecdField with 
            | Choice1Of3 r -> r.FormalType
            | Choice2Of3 f -> f.FieldType(cenv.amap, range0)
            | Choice3Of3 (_,tinst,n,_) -> tinst.[n]
        FSharpType(cenv, fty)

    member _.IsStatic = 
        if isUnresolved() then false else 
        match d.TryRecdField with 
        | Choice1Of3 r -> r.IsStatic
        | Choice2Of3 f -> f.IsStatic
        | Choice3Of3 _ -> false

    member _.Name = 
        checkIsResolved()
        match d.TryRecdField with 
        | Choice1Of3 r -> r.Name
        | Choice2Of3 f -> f.FieldName
        | Choice3Of3 (anonInfo, _tinst, n, _) -> anonInfo.SortedNames.[n]

    member _.IsCompilerGenerated = 
        if isUnresolved() then false else 
        match d.TryRecdField with 
        | Choice1Of3 r -> r.IsCompilerGenerated
        | Choice2Of3 _ -> false
        | Choice3Of3 _ -> false

    member _.IsNameGenerated =
        if isUnresolved() then false else
        match d.TryRecdField with
        | Choice1Of3 r -> r.rfield_name_generated
        | _ -> false

    member _.DeclarationLocation = 
        checkIsResolved()
        match d.TryRecdField with 
        | Choice1Of3 r -> r.Range
        | Choice2Of3 _ -> range0
        | Choice3Of3 (_anonInfo, _tinst, _n, m) -> m

    member _.FieldAttributes = 
        if isUnresolved() then makeReadOnlyCollection [] else 
        match d.TryRecdField with 
        | Choice1Of3 r -> r.FieldAttribs |> List.map (fun a -> FSharpAttribute(cenv, AttribInfo.FSAttribInfo(cenv.g, a))) 
        | Choice2Of3 _ -> [] 
        | Choice3Of3 _ -> []
        |> makeReadOnlyCollection

    member _.PropertyAttributes = 
        if isUnresolved() then makeReadOnlyCollection [] else 
        match d.TryRecdField with 
        | Choice1Of3 r -> r.PropertyAttribs |> List.map (fun a -> FSharpAttribute(cenv, AttribInfo.FSAttribInfo(cenv.g, a))) 
        | Choice2Of3 _ -> [] 
        | Choice3Of3 _ -> []
        |> makeReadOnlyCollection

    override _.Accessibility: FSharpAccessibility =  
        if isUnresolved() then FSharpAccessibility taccessPublic else 
        let access = 
            match d.TryRecdField with 
            | Choice1Of3 r -> r.Accessibility
            | Choice2Of3 _ -> taccessPublic
            | Choice3Of3 _ -> taccessPublic
        FSharpAccessibility access 

    member private x.V = d

    override x.Equals(other: obj) =
        box x === other ||
        match other with
        |   :? FSharpField as uc -> 
            match d, uc.V with 
            | RecdOrClass r1, RecdOrClass r2 -> recdFieldRefOrder.Compare(r1, r2) = 0
            | Union (u1, n1), Union (u2, n2) -> cenv.g.unionCaseRefEq u1 u2 && n1 = n2
            | AnonField (anonInfo1, _, _, _), AnonField (anonInfo2, _, _, _) -> x.Name = uc.Name && anonInfoEquiv anonInfo1 anonInfo2
            | _ -> false
        |   _ -> false

    override x.GetHashCode() = hash x.Name

    override x.ToString() = "field " + x.Name

type [<Class>] FSharpAccessibilityRights(thisCcu: CcuThunk, ad:AccessorDomain) =
    member internal _.ThisCcu = thisCcu

    member internal _.Contents = ad

type FSharpActivePatternCase(cenv, apinfo: PrettyNaming.ActivePatternInfo, ty, n, valOpt: ValRef option, item) = 

    inherit FSharpSymbol (cenv, 
                          (fun () -> item), 
                          (fun _ _ _ -> true))

    member _.Name = apinfo.ActiveTags.[n]

    member _.Index = n

    member _.DeclarationLocation = snd apinfo.ActiveTagsWithRanges.[n]

    member _.Group = FSharpActivePatternGroup(cenv, apinfo, ty, valOpt)

    member _.XmlDoc = 
        defaultArg (valOpt |> Option.map (fun vref -> vref.XmlDoc)) XmlDoc.Empty
        |> makeXmlDoc

    member _.ElaboratedXmlDoc = 
        defaultArg (valOpt |> Option.map (fun vref -> vref.XmlDoc)) XmlDoc.Empty
        |> makeElaboratedXmlDoc

    member _.XmlDocSig = 
        let xmlsig = 
            match valOpt with
            | Some valref -> GetXmlDocSigOfValRef cenv.g valref
            | None -> None
        match xmlsig with
        | Some (_, docsig) -> docsig
        | _ -> ""

type FSharpActivePatternGroup(cenv, apinfo:PrettyNaming.ActivePatternInfo, ty, valOpt) =

    member _.Name = valOpt |> Option.map (fun vref -> vref.LogicalName)

    member _.Names = makeReadOnlyCollection apinfo.Names

    member _.IsTotal = apinfo.IsTotal

    member _.OverallType = FSharpType(cenv, ty)

    member _.DeclaringEntity = 
        valOpt 
        |> Option.bind (fun vref -> 
            match vref.DeclaringEntity with 
            | ParentNone -> None
            | Parent p -> Some (FSharpEntity(cenv, p)))

type FSharpGenericParameter(cenv, v:Typar) = 

    inherit FSharpSymbol (cenv, 
                          (fun () -> Item.TypeVar(v.Name, v)), 
                          (fun _ _ _ad -> true))

    member _.Range = v.Range

    member _.Name = v.DisplayName

    member _.DeclarationLocation = v.Range

    member _.IsCompilerGenerated = v.IsCompilerGenerated
       
    member _.IsMeasure = (v.Kind = TyparKind.Measure)

    member _.XmlDoc = v.XmlDoc |> makeXmlDoc

    member _.ElaboratedXmlDoc = v.XmlDoc |> makeElaboratedXmlDoc

    member _.IsSolveAtCompileTime = (v.StaticReq = TyparStaticReq.HeadType)

    override _.Attributes = 
         // INCOMPLETENESS: If the type parameter comes from .NET then the .NET metadata for the type parameter
         // has been lost (it is not accessible via Typar).  So we can't easily report the attributes in this 
         // case.
         v.Attribs |> List.map (fun a -> FSharpAttribute(cenv, AttribInfo.FSAttribInfo(cenv.g, a))) |> makeReadOnlyCollection
    member _.Constraints = v.Constraints |> List.map (fun a -> FSharpGenericParameterConstraint(cenv, a)) |> makeReadOnlyCollection
    
    member internal x.V = v

    override x.Equals(other: obj) =
        box x === other ||
        match other with
        |   :? FSharpGenericParameter as p -> typarRefEq v p.V
        |   _ -> false

    override x.GetHashCode() = (hash v.Stamp)

    override x.ToString() = "generic parameter " + x.Name

type FSharpDelegateSignature(cenv, info: SlotSig) = 

    member _.DelegateArguments = 
        info.FormalParams.Head
        |> List.map (fun (TSlotParam(nm, ty, _, _, _, _)) -> nm, FSharpType(cenv, ty))
        |> makeReadOnlyCollection

    member _.DelegateReturnType = 
        match info.FormalReturnType with
        | None -> FSharpType(cenv, cenv.g.unit_ty)
        | Some ty -> FSharpType(cenv, ty)
    override x.ToString() = "<delegate signature>"

type FSharpAbstractParameter(cenv, info: SlotParam) =

    member _.Name =    
        let (TSlotParam(name, _, _, _, _, _)) = info
        name

    member _.Type = FSharpType(cenv, info.Type)

    member _.IsInArg =
        let (TSlotParam(_, _, isIn, _, _, _)) = info
        isIn

    member _.IsOutArg =
        let (TSlotParam(_, _, _, isOut, _, _)) = info
        isOut

    member _.IsOptionalArg =
        let (TSlotParam(_, _, _, _, isOptional, _)) = info
        isOptional

    member _.Attributes =
        let (TSlotParam(_, _, _, _, _, attribs)) = info
        attribs |> List.map (fun a -> FSharpAttribute(cenv, AttribInfo.FSAttribInfo(cenv.g, a)))
        |> makeReadOnlyCollection

type FSharpAbstractSignature(cenv, info: SlotSig) =

    member _.AbstractArguments = 
        info.FormalParams
        |> List.map (List.map (fun p -> FSharpAbstractParameter(cenv, p)) >> makeReadOnlyCollection)
        |> makeReadOnlyCollection

    member _.AbstractReturnType = 
        match info.FormalReturnType with
        | None -> FSharpType(cenv, cenv.g.unit_ty)
        | Some ty -> FSharpType(cenv, ty)

    member _.DeclaringTypeGenericParameters =
        info.ClassTypars 
        |> List.map (fun t -> FSharpGenericParameter(cenv, t))
        |> makeReadOnlyCollection
        
    member _.MethodGenericParameters =
        info.MethodTypars 
        |> List.map (fun t -> FSharpGenericParameter(cenv, t))
        |> makeReadOnlyCollection

    member _.Name = info.Name 
    
    member _.DeclaringType = FSharpType(cenv, info.ImplementedType)

type FSharpGenericParameterMemberConstraint(cenv, info: TraitConstraintInfo) = 
    let (TTrait(tys, nm, flags, atys, rty, _)) = info 
    member _.MemberSources = 
        tys   |> List.map (fun ty -> FSharpType(cenv, ty)) |> makeReadOnlyCollection

    member _.MemberName = nm

    member _.MemberIsStatic = not flags.IsInstance

    member _.MemberArgumentTypes = atys   |> List.map (fun ty -> FSharpType(cenv, ty)) |> makeReadOnlyCollection

    member x.MemberReturnType =
        match rty with 
        | None -> FSharpType(cenv, cenv.g.unit_ty) 
        | Some ty -> FSharpType(cenv, ty) 
    override x.ToString() = "<member constraint info>"


type FSharpGenericParameterDelegateConstraint(cenv, tupledArgTy: TType, rty: TType) = 
    member _.DelegateTupledArgumentType = FSharpType(cenv, tupledArgTy)
    member _.DelegateReturnType =  FSharpType(cenv, rty)
    override x.ToString() = "<delegate constraint info>"

type FSharpGenericParameterDefaultsToConstraint(cenv, pri:int, ty:TType) = 
    member _.DefaultsToPriority = pri 
    member _.DefaultsToTarget = FSharpType(cenv, ty) 
    override x.ToString() = "<defaults-to constraint info>"

type FSharpGenericParameterConstraint(cenv, cx: TyparConstraint) = 

    member _.IsCoercesToConstraint = 
        match cx with 
        | TyparConstraint.CoercesTo _ -> true 
        | _ -> false

    member _.CoercesToTarget = 
        match cx with 
        | TyparConstraint.CoercesTo(ty, _) -> FSharpType(cenv, ty) 
        | _ -> invalidOp "not a coerces-to constraint"

    member _.IsDefaultsToConstraint = 
        match cx with 
        | TyparConstraint.DefaultsTo _ -> true 
        | _ -> false

    member _.DefaultsToConstraintData = 
        match cx with 
        | TyparConstraint.DefaultsTo(pri, ty, _) ->  FSharpGenericParameterDefaultsToConstraint(cenv, pri, ty) 
        | _ -> invalidOp "not a 'defaults-to' constraint"

    member _.IsSupportsNullConstraint  = match cx with TyparConstraint.SupportsNull _ -> true | _ -> false

    member _.IsMemberConstraint = 
        match cx with 
        | TyparConstraint.MayResolveMember _ -> true 
        | _ -> false

    member _.MemberConstraintData =  
        match cx with 
        | TyparConstraint.MayResolveMember(info, _) ->  FSharpGenericParameterMemberConstraint(cenv, info) 
        | _ -> invalidOp "not a member constraint"

    member _.IsNonNullableValueTypeConstraint = 
        match cx with 
        | TyparConstraint.IsNonNullableStruct _ -> true 
        | _ -> false
    
    member _.IsReferenceTypeConstraint  = 
        match cx with 
        | TyparConstraint.IsReferenceType _ -> true 
        | _ -> false

    member _.IsSimpleChoiceConstraint = 
        match cx with 
        | TyparConstraint.SimpleChoice _ -> true 
        | _ -> false

    member _.SimpleChoices = 
        match cx with 
        | TyparConstraint.SimpleChoice (tys, _) -> 
            tys   |> List.map (fun ty -> FSharpType(cenv, ty)) |> makeReadOnlyCollection
        | _ -> invalidOp "incorrect constraint kind"

    member _.IsRequiresDefaultConstructorConstraint  = 
        match cx with 
        | TyparConstraint.RequiresDefaultConstructor _ -> true 
        | _ -> false

    member _.IsEnumConstraint = 
        match cx with 
        | TyparConstraint.IsEnum _ -> true 
        | _ -> false

    member _.EnumConstraintTarget = 
        match cx with 
        | TyparConstraint.IsEnum(ty, _) -> FSharpType(cenv, ty)
        | _ -> invalidOp "incorrect constraint kind"
    
    member _.IsComparisonConstraint = 
        match cx with 
        | TyparConstraint.SupportsComparison _ -> true 
        | _ -> false

    member _.IsEqualityConstraint = 
        match cx with 
        | TyparConstraint.SupportsEquality _ -> true 
        | _ -> false

    member _.IsUnmanagedConstraint = 
        match cx with 
        | TyparConstraint.IsUnmanaged _ -> true 
        | _ -> false

    member _.IsDelegateConstraint = 
        match cx with 
        | TyparConstraint.IsDelegate _ -> true 
        | _ -> false

    member _.DelegateConstraintData =  
        match cx with 
        | TyparConstraint.IsDelegate(ty1, ty2, _) ->  FSharpGenericParameterDelegateConstraint(cenv, ty1, ty2) 
        | _ -> invalidOp "not a delegate constraint"

    override x.ToString() = "<type constraint>"

type FSharpInlineAnnotation = 
   | PseudoValue
   | AlwaysInline 
   | OptionalInline 
   | NeverInline 
   | AggressiveInline 

type FSharpMemberOrValData = 
    | E of EventInfo
    | P of PropInfo
    | M of MethInfo
    | C of MethInfo
    | V of ValRef

type FSharpMemberOrVal = FSharpMemberOrFunctionOrValue

type FSharpMemberFunctionOrValue =  FSharpMemberOrFunctionOrValue

type FSharpMemberOrFunctionOrValue(cenv, d:FSharpMemberOrValData, item) = 

    inherit FSharpSymbol(cenv, 
                         (fun () -> item), 
                         (fun this thisCcu2 ad -> 
                              let this = this :?> FSharpMemberOrFunctionOrValue 
                              checkForCrossProjectAccessibility cenv.g.ilg (thisCcu2, ad) (cenv.thisCcu, this.Accessibility.Contents)) 
                              //&& 
                              //match d with 
                              //| E e -> 
                              //    match e with 
                              //    | EventInfo.ILEvent (_, e) -> AccessibilityLogic.IsILEventInfoAccessible g cenv.amap range0 ad e
                              //    | EventInfo.FSEvent (_, _, vref, _) ->  AccessibilityLogic.IsValAccessible ad vref
                              //    | _ -> true
                              //| M m -> AccessibilityLogic.IsMethInfoAccessible cenv.amap range0 ad m
                              //| P p -> AccessibilityLogic.IsPropInfoAccessible g cenv.amap range0 ad p
                              //| V v -> AccessibilityLogic.IsValAccessible ad v
                          )

    let fsharpInfo() = 
        match d with 
        | M m | C m -> m.ArbitraryValRef 
        | P p -> p.ArbitraryValRef 
        | E e -> e.ArbitraryValRef 
        | V v -> Some v
    
    let isUnresolved() = 
        match fsharpInfo() with 
        | None -> false
        | Some v -> v.TryDeref.IsNone

    let checkIsResolved() = 
        if isUnresolved() then 
            let v = (fsharpInfo()).Value
            let nm = (match v with VRefNonLocal n -> n.ItemKey.PartialKey.LogicalName | _ -> "<local>")
            invalidOp (sprintf "The value or member '%s' does not exist or is in an unresolved assembly." nm)

    let mkMethSym minfo = FSharpMemberOrFunctionOrValue(cenv, M minfo, Item.MethodGroup (minfo.DisplayName, [minfo], None))
    let mkEventSym einfo = FSharpMemberOrFunctionOrValue(cenv, E einfo, Item.Event einfo)

    new (cenv, vref) = FSharpMemberFunctionOrValue(cenv, V vref, Item.Value vref)

    new (cenv, minfo: MethInfo) =
        if minfo.IsConstructor || minfo.IsClassConstructor then
            FSharpMemberFunctionOrValue(cenv, C minfo, Item.CtorGroup(minfo.LogicalName, [minfo]))
        else
            FSharpMemberFunctionOrValue(cenv, M minfo, Item.MethodGroup(minfo.LogicalName, [minfo], None))

    member _.IsUnresolved = 
        isUnresolved()

    member _.DeclarationLocationOpt = 
        checkIsResolved()
        match fsharpInfo() with 
        | Some v -> Some v.Range
        | None -> base.DeclarationLocation 

    member x.GetOverloads matchParameterNumber =
        checkIsResolved()
        match d with
        | M m | C m ->
            match item with
            | Item.MethodGroup (_, methodInfos, _)
            | Item.CtorGroup (_, methodInfos) ->
                let isConstructor = x.IsConstructor
                let methods =
                    if matchParameterNumber then
                        methodInfos
                        |> List.filter (fun methodInfo -> not (methodInfo.NumArgs = m.NumArgs) )
                    else methodInfos
                methods
                |> List.map (fun mi ->
                    if isConstructor then
                        FSharpMemberOrFunctionOrValue(cenv, C mi, item)
                    else
                        FSharpMemberOrFunctionOrValue(cenv, M mi, item))
                |> makeReadOnlyCollection
                |> Some
            | _ -> None
        | _ -> None

    member x.DeclarationLocation = 
        checkIsResolved()
        match x.DeclarationLocationOpt with 
        | Some v -> v
        | None -> failwith "DeclarationLocation property not available"

    member _.DeclaringEntity = 
        checkIsResolved()
        match d with 
        | E e -> FSharpEntity(cenv, e.DeclaringTyconRef) |> Some
        | P p -> FSharpEntity(cenv, p.DeclaringTyconRef) |> Some
        | M m | C m -> FSharpEntity(cenv, m.DeclaringTyconRef) |> Some
        | V v -> 
        match v.DeclaringEntity with 
        | ParentNone -> None
        | Parent p -> FSharpEntity(cenv, p) |> Some

    member _.ApparentEnclosingEntity = 
        checkIsResolved()
        match d with 
        | E e -> FSharpEntity(cenv, e.ApparentEnclosingTyconRef)
        | P p -> FSharpEntity(cenv, p.ApparentEnclosingTyconRef)
        | M m | C m -> FSharpEntity(cenv, m.ApparentEnclosingTyconRef)
        | V v -> 
        match v.ApparentEnclosingEntity with 
        | ParentNone -> invalidOp "the value or member doesn't have a logical parent" 
        | Parent p -> FSharpEntity(cenv, p)

    member x.GenericParameters = 
        checkIsResolved()
        let tps = 
            match d with 
            | E _ -> []
            | P _ -> []
            | M m | C m -> m.FormalMethodTypars
            | V v -> v.Typars 
        tps |> List.map (fun tp -> FSharpGenericParameter(cenv, tp)) |> makeReadOnlyCollection

    member x.FullType = 
        checkIsResolved()
        let ty = 
            match d with 
            | E e -> e.GetDelegateType(cenv.amap, range0)
            | P p -> p.GetPropertyType(cenv.amap, range0)
            | M m | C m -> 
                let rty = m.GetFSharpReturnTy(cenv.amap, range0, m.FormalMethodInst)
                let argtysl = m.GetParamTypes(cenv.amap, range0, m.FormalMethodInst) 
                mkIteratedFunTy (List.map (mkRefTupledTy cenv.g) argtysl) rty
            | V v -> v.TauType
        FSharpType(cenv, ty)

    member _.HasGetterMethod =
        if isUnresolved() then false
        else
            match d with 
            | P p -> p.HasGetter
            | E _
            | M _
            | C _
            | V _ -> false

    member _.GetterMethod =
        checkIsResolved()
        match d with 
        | P p -> mkMethSym p.GetterMethod
        | E _ | M _ | C _ | V _ -> invalidOp "the value or member doesn't have an associated getter method" 

    member _.HasSetterMethod =
        if isUnresolved() then false
        else
            match d with 
            | P p -> p.HasSetter
            | E _
            | M _
            | C _
            | V _ -> false

    member _.SetterMethod =
        checkIsResolved()
        match d with 
        | P p -> mkMethSym p.SetterMethod
        | E _ | M _ | C _ | V _ -> invalidOp "the value or member doesn't have an associated setter method" 

    member _.EventAddMethod =
        checkIsResolved()
        match d with 
        | E e -> mkMethSym e.AddMethod
        | P _ | M _ | C _ | V _ -> invalidOp "the value or member doesn't have an associated add method" 

    member _.EventRemoveMethod =
        checkIsResolved()
        match d with 
        | E e -> mkMethSym e.RemoveMethod
        | P _ | M _ | C _ | V _ -> invalidOp "the value or member doesn't have an associated remove method" 

    member _.EventDelegateType =
        checkIsResolved()
        match d with 
        | E e -> FSharpType(cenv, e.GetDelegateType(cenv.amap, range0))
        | P _ | M _ | C _ | V _ -> invalidOp "the value or member doesn't have an associated event delegate type" 

    member _.EventIsStandard =
        checkIsResolved()
        match d with 
        | E e -> 
            let dty = e.GetDelegateType(cenv.amap, range0)
            TryDestStandardDelegateType cenv.infoReader range0 AccessibleFromSomewhere dty |> Option.isSome
        | P _ | M _ | C _ | V _ -> invalidOp "the value or member is not an event" 

    member _.IsCompilerGenerated = 
        if isUnresolved() then false else 
        match fsharpInfo() with 
        | None -> false
        | Some v -> 
        v.IsCompilerGenerated

    member _.InlineAnnotation = 
        if isUnresolved() then FSharpInlineAnnotation.OptionalInline else 
        match fsharpInfo() with 
        | None -> FSharpInlineAnnotation.OptionalInline
        | Some v -> 
        match v.InlineInfo with 
        | ValInline.PseudoVal -> FSharpInlineAnnotation.PseudoValue
        | ValInline.Always -> FSharpInlineAnnotation.AlwaysInline
        | ValInline.Optional -> FSharpInlineAnnotation.OptionalInline
        | ValInline.Never -> FSharpInlineAnnotation.NeverInline

    member _.IsMutable = 
        if isUnresolved() then false else 
        match d with 
        | M _ | C _ | P _ |  E _ -> false
        | V v -> v.IsMutable

    member _.IsModuleValueOrMember = 
        if isUnresolved() then false else 
        match d with 
        | M _ | C _ | P _ | E _ -> true
        | V v -> v.IsMember || v.IsModuleBinding

    member _.IsMember = 
        if isUnresolved() then false else 
        match d with 
        | M _ | C _ | P _ | E _ -> true
        | V v -> v.IsMember 
    
    member _.IsDispatchSlot = 
        if isUnresolved() then false else 
        match d with 
        | E e -> e.AddMethod.IsDispatchSlot
        | P p -> p.IsDispatchSlot
        | M m | C m -> m.IsDispatchSlot
        | V v -> v.IsDispatchSlot

    member x.IsProperty = 
        match d with 
        | P _ -> true
        | _ -> false

    member x.IsEvent = 
        match d with 
        | E _ -> true
        | _ -> false

    member x.EventForFSharpProperty = 
        match d with 
        | P p when p.IsFSharpEventProperty  ->
            let minfos1 = GetImmediateIntrinsicMethInfosOfType (Some("add_"+p.PropertyName), AccessibleFromSomeFSharpCode) cenv.g cenv.amap range0 p.ApparentEnclosingType 
            let minfos2 = GetImmediateIntrinsicMethInfosOfType (Some("remove_"+p.PropertyName), AccessibleFromSomeFSharpCode) cenv.g cenv.amap range0 p.ApparentEnclosingType
            match  minfos1, minfos2 with 
            | [addMeth], [removeMeth] -> 
                match addMeth.ArbitraryValRef, removeMeth.ArbitraryValRef with 
                | Some addVal, Some removeVal -> Some (mkEventSym (FSEvent(cenv.g, p, addVal, removeVal)))
                | _ -> None
            | _ -> None
        | _ -> None

    member _.IsEventAddMethod = 
        if isUnresolved() then false else 
        match d with 
        | M m ->
            let logicalName = m.LogicalName
            logicalName.Length > 4 && logicalName.StartsWithOrdinal("add_") &&

            let eventName = logicalName.[4..]
            let entityTy = generalizedTyconRef m.DeclaringTyconRef
            not (isNil (cenv.infoReader.GetImmediateIntrinsicEventsOfType (Some eventName, AccessibleFromSomeFSharpCode, range0, entityTy))) ||
            let declaringTy = generalizedTyconRef m.DeclaringTyconRef
            match GetImmediateIntrinsicPropInfosOfType (Some eventName, AccessibleFromSomeFSharpCode) cenv.g cenv.amap range0 declaringTy with 
            | pinfo :: _  -> pinfo.IsFSharpEventProperty
            | _ -> false

        | _ -> false

    member _.IsEventRemoveMethod = 
        if isUnresolved() then false else 
        match d with 
        | M m ->
            let logicalName = m.LogicalName
            logicalName.Length > 4 && logicalName.StartsWithOrdinal("remove_") &&

            let eventName = logicalName.[7..]
            let entityTy = generalizedTyconRef m.DeclaringTyconRef
            not (isNil (cenv.infoReader.GetImmediateIntrinsicEventsOfType (Some eventName, AccessibleFromSomeFSharpCode, range0, entityTy))) ||
            let declaringTy = generalizedTyconRef m.DeclaringTyconRef
            match GetImmediateIntrinsicPropInfosOfType (Some eventName, AccessibleFromSomeFSharpCode) cenv.g cenv.amap range0 declaringTy with 
            | pinfo :: _ -> pinfo.IsFSharpEventProperty
            | _ -> false
        | _ -> false

    member _.IsPropertyGetterMethod = 
        if isUnresolved() then false else 
        match d with 
        | M m ->
            let logicalName = m.LogicalName
            logicalName.Length > 4 && logicalName.StartsWithOrdinal("get_") &&

            let propName = PrettyNaming.ChopPropertyName(logicalName)
            let declaringTy = generalizedTyconRef m.DeclaringTyconRef
            not (isNil (GetImmediateIntrinsicPropInfosOfType (Some propName, AccessibleFromSomeFSharpCode) cenv.g cenv.amap range0 declaringTy))
        | V v -> v.IsPropertyGetterMethod
        | _ -> false

    member _.IsPropertySetterMethod = 
        if isUnresolved() then false else 
        match d with 
        | M m ->
            let logicalName = m.LogicalName
            logicalName.Length > 4 && logicalName.StartsWithOrdinal("set_") &&

            let propName = PrettyNaming.ChopPropertyName(logicalName) 
            let declaringTy = generalizedTyconRef m.DeclaringTyconRef
            not (isNil (GetImmediateIntrinsicPropInfosOfType (Some propName, AccessibleFromSomeFSharpCode) cenv.g cenv.amap range0 declaringTy))
        | V v -> v.IsPropertySetterMethod
        | _ -> false

    member _.IsInstanceMember = 
        if isUnresolved() then false else 
        match d with 
        | E e -> not e.IsStatic
        | P p -> not p.IsStatic
        | M m | C m -> m.IsInstance
        | V v -> v.IsInstanceMember

    member x.IsInstanceMemberInCompiledCode = 
        if isUnresolved() then false else 
        x.IsInstanceMember &&
        match d with 
        | E e -> match e.ArbitraryValRef with Some vref -> ValRefIsCompiledAsInstanceMember cenv.g vref | None -> true
        | P p -> match p.ArbitraryValRef with Some vref -> ValRefIsCompiledAsInstanceMember cenv.g vref | None -> true
        | M m | C m -> match m.ArbitraryValRef with Some vref -> ValRefIsCompiledAsInstanceMember cenv.g vref | None -> true
        | V vref -> ValRefIsCompiledAsInstanceMember cenv.g vref 

    member _.IsExtensionMember = 
        if isUnresolved() then false else 
        match d with 
        | E e -> e.AddMethod.IsExtensionMember
        | P p -> p.IsExtensionMember
        | M m -> m.IsExtensionMember
        | V v -> v.IsExtensionMember
        | C _ -> false

    member _.IsOverrideOrExplicitInterfaceImplementation =
        if isUnresolved() then false else 
        match d with 
        | E e -> e.AddMethod.IsDefiniteFSharpOverride
        | P p -> p.IsDefiniteFSharpOverride
        | M m -> m.IsDefiniteFSharpOverride
        | V v -> 
            v.MemberInfo.IsSome && v.IsDefiniteFSharpOverrideMember
        | C _ -> false

    member _.IsExplicitInterfaceImplementation =
        if isUnresolved() then false else 
        match d with 
        | E e -> e.AddMethod.IsFSharpExplicitInterfaceImplementation
        | P p -> p.IsFSharpExplicitInterfaceImplementation
        | M m -> m.IsFSharpExplicitInterfaceImplementation
        | V v -> v.IsFSharpExplicitInterfaceImplementation cenv.g
        | C _ -> false

    member _.ImplementedAbstractSignatures =
        checkIsResolved()
        let sigs =
            match d with
            | E e -> e.AddMethod.ImplementedSlotSignatures
            | P p -> p.ImplementedSlotSignatures
            | M m | C m -> m.ImplementedSlotSignatures
            | V v -> v.ImplementedSlotSignatures
        sigs |> List.map (fun s -> FSharpAbstractSignature (cenv, s))
        |> makeReadOnlyCollection

    member _.IsImplicitConstructor = 
        if isUnresolved() then false else 
        match fsharpInfo() with 
        | None -> false
        | Some v -> v.IsIncrClassConstructor
    
    member _.IsTypeFunction = 
        if isUnresolved() then false else 
        match fsharpInfo() with 
        | None -> false
        | Some v -> v.IsTypeFunction

    member _.IsActivePattern =  
        if isUnresolved() then false else 
        match fsharpInfo() with 
        | Some v -> PrettyNaming.ActivePatternInfoOfValName v.CoreDisplayName v.Range |> Option.isSome
        | None -> false

    member x.CompiledName = 
        checkIsResolved()
        match fsharpInfo() with 
        | Some v -> v.CompiledName cenv.g.CompilerGlobalState
        | None -> x.LogicalName

    member _.LogicalName = 
        checkIsResolved()
        match d with 
        | E e -> e.EventName
        | P p -> p.PropertyName
        | M m | C m -> m.LogicalName
        | V v -> v.LogicalName

    member _.DisplayName = 
        checkIsResolved()
        match d with 
        | E e -> e.EventName
        | P p -> p.PropertyName
        | M m | C m -> m.DisplayName
        | V v -> v.DisplayName

    member sym.XmlDocSig = 
        checkIsResolved()
 
        match d with 
        | E e ->
            let range = defaultArg sym.DeclarationLocationOpt range0
            match GetXmlDocSigOfEvent cenv.infoReader range e with
            | Some (_, docsig) -> docsig
            | _ -> ""
        | P p ->
            let range = defaultArg sym.DeclarationLocationOpt range0
            match GetXmlDocSigOfProp cenv.infoReader range p with
            | Some (_, docsig) -> docsig
            | _ -> ""
        | M m | C m -> 
            let range = defaultArg sym.DeclarationLocationOpt range0
            match GetXmlDocSigOfMethInfo cenv.infoReader range m with
            | Some (_, docsig) -> docsig
            | _ -> ""
        | V v ->
            match v.DeclaringEntity with 
            | Parent entityRef -> 
                match GetXmlDocSigOfScopedValRef cenv.g entityRef v with
                | Some (_, docsig) -> docsig
                | _ -> ""
            | ParentNone -> "" 

    member _.XmlDoc = 
        if isUnresolved() then XmlDoc.Empty  |> makeXmlDoc else
        match d with 
        | E e -> e.XmlDoc |> makeXmlDoc
        | P p -> p.XmlDoc |> makeXmlDoc
        | M m | C m -> m.XmlDoc |> makeXmlDoc
        | V v -> v.XmlDoc |> makeXmlDoc

    member _.ElaboratedXmlDoc = 
        if isUnresolved() then XmlDoc.Empty  |> makeElaboratedXmlDoc else
        match d with 
        | E e -> e.XmlDoc |> makeElaboratedXmlDoc
        | P p -> p.XmlDoc |> makeElaboratedXmlDoc
        | M m | C m -> m.XmlDoc |> makeElaboratedXmlDoc
        | V v -> v.XmlDoc |> makeElaboratedXmlDoc

    member x.CurriedParameterGroups = 
        checkIsResolved()
        match d with 
        | P p -> 
            [ [ for (ParamData(isParamArrayArg, isInArg, isOutArg, optArgInfo, _callerInfo, nmOpt, _reflArgInfo, pty)) in p.GetParamDatas(cenv.amap, range0) do 
                    // INCOMPLETENESS: Attribs is empty here, so we can't look at attributes for
                    // either .NET or F# parameters
                    let argInfo: ArgReprInfo = { Name=nmOpt; Attribs= [] }
                    yield FSharpParameter(cenv, pty, argInfo, None, x.DeclarationLocationOpt, isParamArrayArg, isInArg, isOutArg, optArgInfo.IsOptional, false) ] 
              |> makeReadOnlyCollection  ]
           |> makeReadOnlyCollection

        | E _ ->  []  |> makeReadOnlyCollection
        | M m | C m -> 
            [ for argtys in m.GetParamDatas(cenv.amap, range0, m.FormalMethodInst) do 
                 yield
                   [ for (ParamData(isParamArrayArg, isInArg, isOutArg, optArgInfo, _callerInfo, nmOpt, _reflArgInfo, pty)) in argtys do 
                // INCOMPLETENESS: Attribs is empty here, so we can't look at attributes for
                // either .NET or F# parameters
                        let argInfo: ArgReprInfo = { Name=nmOpt; Attribs= [] }
                        yield FSharpParameter(cenv, pty, argInfo, None, x.DeclarationLocationOpt, isParamArrayArg, isInArg, isOutArg, optArgInfo.IsOptional, false) ] 
                   |> makeReadOnlyCollection ]
             |> makeReadOnlyCollection

        | V v -> 
        match v.ValReprInfo with 
        | None ->
            let _, tau = v.TypeScheme
            if isFunTy cenv.g tau then
                let argtysl, _typ = stripFunTy cenv.g tau
                [ for ty in argtysl do
                    let allArguments =
                        if isRefTupleTy cenv.g ty
                        then tryDestRefTupleTy cenv.g ty
                        else [ty]
                    yield
                      allArguments
                      |> List.map (fun arg -> FSharpParameter(cenv, arg, ValReprInfo.unnamedTopArg1, x.DeclarationLocationOpt))
                      |> makeReadOnlyCollection ]
                |> makeReadOnlyCollection
            else makeReadOnlyCollection []
        | Some (ValReprInfo(_typars, curriedArgInfos, _retInfo)) -> 
            let tau = v.TauType
            let argtysl, _ = GetTopTauTypeInFSharpForm cenv.g curriedArgInfos tau range0
            let argtysl = if v.IsInstanceMember then argtysl.Tail else argtysl
            [ for argtys in argtysl do 
                 yield 
                   [ for argty, argInfo in argtys do 
                        let isParamArrayArg = HasFSharpAttribute cenv.g cenv.g.attrib_ParamArrayAttribute argInfo.Attribs
                        let isInArg = HasFSharpAttribute cenv.g cenv.g.attrib_InAttribute argInfo.Attribs && isByrefTy cenv.g argty
                        let isOutArg = HasFSharpAttribute cenv.g cenv.g.attrib_OutAttribute argInfo.Attribs && isByrefTy cenv.g argty
                        let isOptionalArg = HasFSharpAttribute cenv.g cenv.g.attrib_OptionalArgumentAttribute argInfo.Attribs
                        yield FSharpParameter(cenv, argty, argInfo, None, x.DeclarationLocationOpt, isParamArrayArg, isInArg, isOutArg, isOptionalArg, false) ] 
                   |> makeReadOnlyCollection ]
             |> makeReadOnlyCollection

    member x.ReturnParameter = 
        checkIsResolved()
        match d with 
        | E e -> 
            // INCOMPLETENESS: Attribs is empty here, so we can't look at return attributes for .NET or F# methods
            let rty = 
                try PropTypOfEventInfo cenv.infoReader range0 AccessibleFromSomewhere e
                with _ -> 
                    // For non-standard events, just use the delegate type as the ReturnParameter type
                    e.GetDelegateType(cenv.amap, range0)
            FSharpParameter(cenv, rty, ValReprInfo.unnamedRetVal, x.DeclarationLocationOpt) 

        | P p -> 
            // INCOMPLETENESS: Attribs is empty here, so we can't look at return attributes for .NET or F# methods
            let rty = p.GetPropertyType(cenv.amap, range0)
            FSharpParameter(cenv, rty, ValReprInfo.unnamedRetVal, x.DeclarationLocationOpt) 
        | M m | C m -> 
            // INCOMPLETENESS: Attribs is empty here, so we can't look at return attributes for .NET or F# methods
            let rty = m.GetFSharpReturnTy(cenv.amap, range0, m.FormalMethodInst)
            FSharpParameter(cenv, rty, ValReprInfo.unnamedRetVal, x.DeclarationLocationOpt) 
        | V v -> 
        match v.ValReprInfo with 
        | None ->
            let _, tau = v.TypeScheme
            let _argtysl, rty = stripFunTy cenv.g tau
            FSharpParameter(cenv, rty, ValReprInfo.unnamedRetVal, x.DeclarationLocationOpt)
        | Some (ValReprInfo(_typars, argInfos, retInfo)) -> 
            let tau = v.TauType
            let _c, rty = GetTopTauTypeInFSharpForm cenv.g argInfos tau range0
            FSharpParameter(cenv, rty, retInfo, x.DeclarationLocationOpt) 


    override _.Attributes = 
        if isUnresolved() then makeReadOnlyCollection [] else 
        let m = range0
        match d with 
        | E einfo -> 
            GetAttribInfosOfEvent cenv.amap m einfo |> List.map (fun a -> FSharpAttribute(cenv, a))
        | P pinfo -> 
            GetAttribInfosOfProp cenv.amap m pinfo |> List.map (fun a -> FSharpAttribute(cenv, a))
        | M minfo | C minfo -> 
            GetAttribInfosOfMethod cenv.amap m minfo |> List.map (fun a -> FSharpAttribute(cenv, a))
        | V v -> 
            v.Attribs |> List.map (fun a -> FSharpAttribute(cenv, AttribInfo.FSAttribInfo(cenv.g, a))) 
     |> makeReadOnlyCollection
     
    /// Is this "base" in "base.M(...)"
    member _.IsBaseValue =
        if isUnresolved() then false else
        match d with
        | M _ | C _ | P _ | E _ -> false
        | V v -> v.BaseOrThisInfo = BaseVal

    /// Is this the "x" in "type C() as x = ..."
    member _.IsConstructorThisValue =
        if isUnresolved() then false else
        match d with
        | M _ | C _| P _ | E _ -> false
        | V v -> v.BaseOrThisInfo = CtorThisVal

    /// Is this the "x" in "member x.M = ..."
    member _.IsMemberThisValue =
        if isUnresolved() then false else
        match d with
        | M _ | C _ | P _ | E _ -> false
        | V v -> v.BaseOrThisInfo = MemberThisVal

    /// Is this a [<Literal>] value, and if so what value? (may be null)
    member _.LiteralValue =
        if isUnresolved() then None else
        match d with
        | M _ | C _ | P _ | E _ -> None
        | V v -> getLiteralValue v.LiteralValue

      /// How visible is this? 
    override this.Accessibility: FSharpAccessibility  = 
        if isUnresolved() then FSharpAccessibility taccessPublic else 
        match fsharpInfo() with 
        | Some v -> FSharpAccessibility(v.Accessibility)
        | None ->  
        
        // Note, returning "public" is wrong for IL members that are private
        match d with 
        | E e ->  
            // For IL events, we get an approximate accessibility that at least reports "internal" as "internal" and "private" as "private"
            let access = 
                match e with 
                | ILEvent ileinfo -> 
                    let ilAccess = AccessibilityLogic.GetILAccessOfILEventInfo ileinfo
                    getApproxFSharpAccessibilityOfMember this.DeclaringEntity.Value.Entity ilAccess
                | _ -> taccessPublic

            FSharpAccessibility access

        | P p ->  
            // For IL  properties, we get an approximate accessibility that at least reports "internal" as "internal" and "private" as "private"
            let access = 
                match p with 
                | ILProp ilpinfo -> 
                    let ilAccess = AccessibilityLogic.GetILAccessOfILPropInfo ilpinfo
                    getApproxFSharpAccessibilityOfMember this.DeclaringEntity.Value.Entity  ilAccess
                | _ -> taccessPublic

            FSharpAccessibility access

        | M m | C m ->  

            // For IL  methods, we get an approximate accessibility that at least reports "internal" as "internal" and "private" as "private"
            let access = 
                match m with 
                | ILMeth (_, x, _) -> getApproxFSharpAccessibilityOfMember x.DeclaringTyconRef x.RawMetadata.Access 
                | _ -> taccessPublic

            FSharpAccessibility(access, isProtected=m.IsProtectedAccessibility)

        | V v -> FSharpAccessibility(v.Accessibility)

    member x.IsConstructor =
        match d with
        | C _ -> true
        | V v -> v.IsConstructor
        | _ -> false

    member x.Data = d

    member x.IsValCompiledAsMethod =
        match d with
        | V valRef -> IlxGen.IsFSharpValCompiledAsMethod cenv.g valRef.Deref
        | _ -> false

    member x.IsValue =
        match d with
        | V valRef -> not (isForallFunctionTy cenv.g valRef.Type)
        | _ -> false

    member x.IsFunction =
        match d with
        | V valRef -> isForallFunctionTy cenv.g valRef.Type
        | _ -> false

    override x.Equals(other: obj) =
        box x === other ||
        match other with
        |   :? FSharpMemberOrFunctionOrValue as other ->
            match d, other.Data with 
            | E evt1, E evt2 -> EventInfo.EventInfosUseIdenticalDefinitions evt1 evt2 
            | P p1, P p2 ->  PropInfo.PropInfosUseIdenticalDefinitions p1 p2
            | M m1, M m2
            | C m1, C m2 ->  MethInfo.MethInfosUseIdenticalDefinitions m1 m2
            | V v1, V v2 -> valRefEq cenv.g v1 v2
            | _ -> false
        |   _ -> false

    override x.GetHashCode() = hash (box x.LogicalName)
    override x.ToString() = 
        try  
            let prefix = (if x.IsEvent then "event " elif x.IsProperty then "property " elif x.IsMember then "member " else "val ") 
            prefix + x.LogicalName
        with _  -> "??"

    member x.FormatLayout (context:FSharpDisplayContext) =
        match x.IsMember, d with
        | true, V v ->
            NicePrint.prettyLayoutOfMemberNoInstShort { (context.Contents cenv.g) with showMemberContainers=true } v.Deref
            |> LayoutRender.toArray
        | _,_ ->
            checkIsResolved()
            let ty = 
                match d with 
                | E e -> e.GetDelegateType(cenv.amap, range0)
                | P p -> p.GetPropertyType(cenv.amap, range0)
                | M m | C m -> 
                    let rty = m.GetFSharpReturnTy(cenv.amap, range0, m.FormalMethodInst)
                    let argtysl = m.GetParamTypes(cenv.amap, range0, m.FormalMethodInst) 
                    mkIteratedFunTy (List.map (mkRefTupledTy cenv.g) argtysl) rty
                | V v -> v.TauType
            NicePrint.prettyLayoutOfTypeNoCx (context.Contents cenv.g) ty
            |> LayoutRender.toArray

    member x.GetWitnessPassingInfo() = 
        let witnessInfos = 
            match d with 
            | M (FSMeth(_, _, vref, _))  -> 
                let _tps, witnessInfos, _curriedArgInfos, _retTy, _ = GetTypeOfMemberInMemberForm cenv.g vref
                witnessInfos
            | V vref  ->
                let arities = arityOfVal vref.Deref
                let numEnclosingTypars = CountEnclosingTyparsOfActualParentOfVal vref.Deref
                let _tps, witnessInfos, _curriedArgInfos, _retTy, _ = GetTopValTypeInCompiledForm cenv.g arities numEnclosingTypars vref.Type vref.DefinitionRange
                witnessInfos
            | E _ | P _ | M _ | C _ -> []
        match witnessInfos with 
        | [] -> None
        | _ when not (cenv.g.langVersion.SupportsFeature(Features.LanguageFeature.WitnessPassing)) -> None
        | _ ->
        let witnessParams = 
            ((Set.empty, 0), witnessInfos) ||> List.mapFold (fun (used,i) witnessInfo -> 
                let paramTy = GenWitnessTy cenv.g witnessInfo
                let nm = String.uncapitalize witnessInfo.MemberName
                let nm = if used.Contains nm then nm + string i else nm
                let argReprInfo : ArgReprInfo = { Attribs=[]; Name=Some (mkSynId x.DeclarationLocation nm) }
                let p = FSharpParameter(cenv, paramTy, argReprInfo, None, None, false, false, false, false, true)
                p, (used.Add nm, i + 1))
            |> fst
        let witnessMethName = PrettyNaming.ExtraWitnessMethodName x.CompiledName
        Some (witnessMethName, makeReadOnlyCollection witnessParams)

    // FullType may raise exceptions (see https://github.com/fsharp/fsharp/issues/307). 
    member x.FullTypeSafe = Option.attempt (fun _ -> x.FullType)

    member x.TryGetFullDisplayName() =
        let fullName = Option.attempt (fun _ -> x.FullName.Split '.')
        match fullName with
        | Some fullName ->
            match Option.attempt (fun _ -> x.DisplayName) with
            | Some shortDisplayName when not (shortDisplayName.Contains ".") ->
                Some (fullName |> Array.replace (fullName.Length - 1) shortDisplayName)
            | _ -> Some fullName
        | None -> None
        |> Option.map (fun fullDisplayName -> String.Join (".", fullDisplayName))

    member x.TryGetFullCompiledOperatorNameIdents() : string[] option =
        // For operator ++ displayName is ( ++ ) compiledName is op_PlusPlus
        if PrettyNaming.IsOperatorName x.DisplayName && x.DisplayName <> x.CompiledName then
            x.DeclaringEntity
            |> Option.bind (fun e -> e.TryGetFullName())
            |> Option.map (fun enclosingEntityFullName -> 
                    Array.append (enclosingEntityFullName.Split '.') [| x.CompiledName |])
        else None

type FSharpType(cenv, ty:TType) =

    let isUnresolved() = 
       ErrorLogger.protectAssemblyExploration true <| fun () -> 
        match stripTyparEqns ty with 
        | TType_app (tcref, _) -> FSharpEntity(cenv, tcref).IsUnresolved
        | TType_measure (Measure.Con tcref) ->  FSharpEntity(cenv, tcref).IsUnresolved
        | TType_measure (Measure.Prod _) ->  FSharpEntity(cenv, cenv.g.measureproduct_tcr).IsUnresolved 
        | TType_measure Measure.One ->  FSharpEntity(cenv, cenv.g.measureone_tcr).IsUnresolved 
        | TType_measure (Measure.Inv _) ->  FSharpEntity(cenv, cenv.g.measureinverse_tcr).IsUnresolved 
        | _ -> false
    
    let isResolved() = not (isUnresolved())

    new (g, thisCcu, thisCcuTyp, tcImports, ty) = FSharpType(SymbolEnv(g, thisCcu, Some thisCcuTyp, tcImports), ty)

    member _.IsUnresolved = isUnresolved()

    member _.HasTypeDefinition = 
       isResolved() &&
       protect <| fun () -> 
         match stripTyparEqns ty with 
         | TType_app _ | TType_measure (Measure.Con _ | Measure.Prod _ | Measure.Inv _ | Measure.One _) -> true 
         | _ -> false

    member _.IsTupleType = 
       isResolved() &&
       protect <| fun () -> 
        match stripTyparEqns ty with 
        | TType_tuple _ -> true 
        | _ -> false

    member _.IsStructTupleType = 
       isResolved() &&
       protect <| fun () -> 
        match stripTyparEqns ty with 
        | TType_tuple (tupInfo, _) -> evalTupInfoIsStruct tupInfo
        | _ -> false

    member _.TypeDefinition = 
       protect <| fun () -> 
        match stripTyparEqns ty with 
        | TType_app (tcref, _) -> FSharpEntity(cenv, tcref) 
        | TType_measure (Measure.Con tcref) ->  FSharpEntity(cenv, tcref) 
        | TType_measure (Measure.Prod _) ->  FSharpEntity(cenv, cenv.g.measureproduct_tcr) 
        | TType_measure Measure.One ->  FSharpEntity(cenv, cenv.g.measureone_tcr) 
        | TType_measure (Measure.Inv _) ->  FSharpEntity(cenv, cenv.g.measureinverse_tcr) 
        | _ -> invalidOp "not a named type"

    member _.GenericArguments = 
       protect <| fun () -> 
        match stripTyparEqns ty with 
        | TType_anon (_, tyargs) 
        | TType_app (_, tyargs) 
        | TType_tuple (_, tyargs) -> (tyargs |> List.map (fun ty -> FSharpType(cenv, ty)) |> makeReadOnlyCollection) 
        | TType_fun(d, r) -> [| FSharpType(cenv, d); FSharpType(cenv, r) |] |> makeReadOnlyCollection
        | TType_measure (Measure.Con _) ->  [| |] |> makeReadOnlyCollection
        | TType_measure (Measure.Prod (t1, t2)) ->  [| FSharpType(cenv, TType_measure t1); FSharpType(cenv, TType_measure t2) |] |> makeReadOnlyCollection
        | TType_measure Measure.One ->  [| |] |> makeReadOnlyCollection
        | TType_measure (Measure.Inv t1) ->  [| FSharpType(cenv, TType_measure t1) |] |> makeReadOnlyCollection
        | _ -> invalidOp "not a named type"

(*
    member _.ProvidedArguments = 
        let typeName, argNamesAndValues = 
            try 
                PrettyNaming.demangleProvidedTypeName typeLogicalName 
            with PrettyNaming.InvalidMangledStaticArg piece -> 
                error(Error(FSComp.SR.etProvidedTypeReferenceInvalidText(piece), range0)) 
*)

    member ty.IsAbbreviation = 
       isResolved() && ty.HasTypeDefinition && ty.TypeDefinition.IsFSharpAbbreviation

    member _.AbbreviatedType = 
       protect <| fun () -> FSharpType(cenv, stripTyEqns cenv.g ty)

    member _.IsFunctionType = 
       isResolved() &&
       protect <| fun () -> 
        match stripTyparEqns ty with 
        | TType_fun _ -> true 
        | _ -> false

    member _.IsAnonRecordType = 
       isResolved() &&
       protect <| fun () -> 
        match stripTyparEqns ty with 
        | TType_anon _ -> true 
        | _ -> false

    member _.AnonRecordTypeDetails = 
       protect <| fun () -> 
        match stripTyparEqns ty with 
        | TType_anon (anonInfo, _) -> FSharpAnonRecordTypeDetails(cenv, anonInfo)
        | _ -> invalidOp "not an anonymous record type"

    member _.IsGenericParameter = 
       protect <| fun () -> 
        match stripTyparEqns ty with 
        | TType_var _ -> true 
        | TType_measure (Measure.Var _) -> true 
        | _ -> false

    member _.GenericParameter = 
       protect <| fun () -> 
        match stripTyparEqns ty with 
        | TType_var tp 
        | TType_measure (Measure.Var tp) -> 
            FSharpGenericParameter (cenv, tp)
        | _ -> invalidOp "not a generic parameter type"

    member _.AllInterfaces = 
        if isUnresolved() then makeReadOnlyCollection [] else
        [ for ty in AllInterfacesOfType  cenv.g cenv.amap range0 AllowMultiIntfInstantiations.Yes ty do 
             yield FSharpType(cenv, ty) ]
        |> makeReadOnlyCollection

    member _.BaseType = 
        GetSuperTypeOfType cenv.g cenv.amap range0 ty
        |> Option.map (fun ty -> FSharpType(cenv, ty)) 

    member _.Instantiate(instantiation:(FSharpGenericParameter * FSharpType) list) = 
        let typI = instType (instantiation |> List.map (fun (tyv, ty) -> tyv.V, ty.V)) ty
        FSharpType(cenv, typI)

    member private x.V = ty
    member private x.cenv = cenv

    member private ty.AdjustType t = 
        FSharpType(ty.cenv, t)

    // Note: This equivalence relation is modulo type abbreviations
    override x.Equals(other: obj) =
        box x === other ||
        match other with
        |   :? FSharpType as t -> typeEquiv cenv.g ty t.V
        |   _ -> false

    // Note: This equivalence relation is modulo type abbreviations. The hash is less than perfect.
    override _.GetHashCode() = 
        let rec hashType ty = 
            let ty = stripTyEqnsWrtErasure EraseNone cenv.g ty
            match ty with
            | TType_forall _ ->  10000
            | TType_var tp  -> 10100 + int32 tp.Stamp
            | TType_app (tc1, b1)  -> 10200 + int32 tc1.Stamp + List.sumBy hashType b1
            | TType_ucase _   -> 10300  // shouldn't occur in symbols
            | TType_tuple (_, l1) -> 10400 + List.sumBy hashType l1
            | TType_fun (dty, rty) -> 10500 + hashType dty + hashType rty
            | TType_measure _ -> 10600 
            | TType_anon (_,l1) -> 10800 + List.sumBy hashType l1
        hashType ty

    member _.Format(context: FSharpDisplayContext) = 
       protect <| fun () -> 
            NicePrint.prettyStringOfTyNoCx (context.Contents cenv.g) ty 

    member _.FormatWithConstraints(context: FSharpDisplayContext) = 
        protect <| fun () -> 
            NicePrint.prettyStringOfTy (context.Contents cenv.g) ty 

    member _.FormatLayout(context: FSharpDisplayContext) =
       protect <| fun () -> 
            NicePrint.prettyLayoutOfTypeNoCx (context.Contents cenv.g) ty
            |> LayoutRender.toArray

    member _.FormatLayoutWithConstraints(context: FSharpDisplayContext) =
        protect <| fun () -> 
            NicePrint.prettyLayoutOfType (context.Contents cenv.g) ty
            |> LayoutRender.toArray

    override _.ToString() = 
       protect <| fun () -> 
        "type " + NicePrint.prettyStringOfTyNoCx (DisplayEnv.Empty(cenv.g)) ty 

    static member Prettify(ty: FSharpType) = 
        let prettyTy = PrettyTypes.PrettifyType ty.cenv.g ty.V  |> fst
        ty.AdjustType prettyTy

    static member Prettify(types: IList<FSharpType>) = 
        let xs = types |> List.ofSeq
        match xs with 
        | [] -> []
        | h :: _ -> 
            let cenv = h.cenv
            let prettyTys = PrettyTypes.PrettifyTypes cenv.g [ for t in xs -> t.V ] |> fst
            (xs, prettyTys) ||> List.map2 (fun p pty -> p.AdjustType pty)
        |> makeReadOnlyCollection

    static member Prettify(parameter: FSharpParameter) = 
        let prettyTy = parameter.V |> PrettyTypes.PrettifyType parameter.cenv.g |> fst
        parameter.AdjustType prettyTy

    static member Prettify(parameters: IList<FSharpParameter>) = 
        let parameters = parameters |> List.ofSeq
        match parameters with 
        | [] -> []
        | h :: _ -> 
            let cenv = h.cenv
            let prettyTys = parameters |> List.map (fun p -> p.V) |> PrettyTypes.PrettifyTypes cenv.g |> fst
            (parameters, prettyTys) ||> List.map2 (fun p pty -> p.AdjustType pty)
        |> makeReadOnlyCollection

    static member Prettify(parameters: IList<IList<FSharpParameter>>) = 
        let xs = parameters |> List.ofSeq |> List.map List.ofSeq
        let hOpt = xs |> List.tryPick (function h :: _ -> Some h | _ -> None) 
        match hOpt with 
        | None -> xs
        | Some h -> 
            let cenv = h.cenv
            let prettyTys = xs |> List.mapSquared (fun p -> p.V) |> PrettyTypes.PrettifyCurriedTypes cenv.g |> fst
            (xs, prettyTys) ||> List.map2 (List.map2 (fun p pty -> p.AdjustType pty))
        |> List.map makeReadOnlyCollection |> makeReadOnlyCollection

    static member Prettify(parameters: IList<IList<FSharpParameter>>, returnParameter: FSharpParameter) = 
        let xs = parameters |> List.ofSeq |> List.map List.ofSeq
        let cenv = returnParameter.cenv
        let prettyTys, prettyRetTy = xs |> List.mapSquared (fun p -> p.V) |> (fun tys -> PrettyTypes.PrettifyCurriedSigTypes cenv.g (tys, returnParameter.V) )|> fst
        let ps = (xs, prettyTys) ||> List.map2 (List.map2 (fun p pty -> p.AdjustType pty)) |> List.map makeReadOnlyCollection |> makeReadOnlyCollection
        ps, returnParameter.AdjustType prettyRetTy

    member x.StripAbbreviations() =
        if x.IsAbbreviation then
            x.AbbreviatedType.StripAbbreviations()
        else x

type FSharpAttribute(cenv: SymbolEnv, attrib: AttribInfo) = 

    let rec resolveArgObj (arg: obj) =
        match arg with
        | :? TType as t -> box (FSharpType(cenv, t)) 
        | :? (obj[]) as a -> a |> Array.map resolveArgObj |> box
        | _ -> arg

    member _.AttributeType =  
        FSharpEntity(cenv, attrib.TyconRef)

    member _.IsUnresolved = entityIsUnresolved(attrib.TyconRef)

    member _.ConstructorArguments = 
        attrib.ConstructorArguments 
        |> List.map (fun (ty, obj) -> FSharpType(cenv, ty), resolveArgObj obj)
        |> makeReadOnlyCollection

    member _.NamedArguments = 
        attrib.NamedArguments 
        |> List.map (fun (ty, nm, isField, obj) -> FSharpType(cenv, ty), nm, isField, resolveArgObj obj)
        |> makeReadOnlyCollection

    member _.Format(context: FSharpDisplayContext) = 
        protect <| fun () -> 
            match attrib with
            | AttribInfo.FSAttribInfo(g, attrib) ->
                NicePrint.stringOfFSAttrib (context.Contents g) attrib
            | AttribInfo.ILAttribInfo (g, _, _scoref, cattr, _) -> 
                let parms, _args = decodeILAttribData cattr 
                NicePrint.stringOfILAttrib (context.Contents g) (cattr.Method.DeclaringType, parms)

    member _.Range = attrib.Range

    override _.ToString() = 
        if entityIsUnresolved attrib.TyconRef then "attribute ???" else "attribute " + attrib.TyconRef.CompiledName + "(...)" 

    member attr.IsAttribute<'T> () =
        // CompiledName throws exception on DataContractAttribute generated by SQLProvider
        try attr.AttributeType.CompiledName = typeof<'T>.Name with _ -> false

#if !NO_EXTENSIONTYPING    
type FSharpStaticParameter(cenv, sp: Tainted< ExtensionTyping.ProvidedParameterInfo >, m) = 
    inherit FSharpSymbol(cenv, 
                         (fun () -> 
                              protect <| fun () -> 
                                let spKind = Import.ImportProvidedType cenv.amap m (sp.PApply((fun x -> x.ParameterType), m))
                                let nm = sp.PUntaint((fun p -> p.Name), m)
                                Item.ArgName((mkSynId m nm, spKind, None))), 
                         (fun _ _ _ -> true))

    member _.Name = 
        protect <| fun () -> 
            sp.PUntaint((fun p -> p.Name), m)

    member _.DeclarationLocation = m

    member _.Kind = 
        protect <| fun () -> 
            let ty = Import.ImportProvidedType cenv.amap m (sp.PApply((fun x -> x.ParameterType), m))
            FSharpType(cenv, ty)

    member _.IsOptional = 
        protect <| fun () -> sp.PUntaint((fun x -> x.IsOptional), m)

    member _.HasDefaultValue = 
        protect <| fun () -> sp.PUntaint((fun x -> x.HasDefaultValue), m)

    member _.DefaultValue = 
        protect <| fun () -> sp.PUntaint((fun x -> x.RawDefaultValue), m)

    member _.Range = m

    override x.Equals(other: obj) =
        box x === other || 
        match other with
        |   :? FSharpStaticParameter as p -> x.Name = p.Name && Range.equals x.DeclarationLocation p.DeclarationLocation
        |   _ -> false

    override x.GetHashCode() = hash x.Name
    override x.ToString() = 
        "static parameter " + x.Name 
#endif

type FSharpParameter(cenv, paramTy: TType, topArgInfo: ArgReprInfo, ownerOpt, ownerRangeOpt, isParamArrayArg, isInArg, isOutArg, isOptionalArg, isWitnessArg) = 
    inherit FSharpSymbol(cenv, 
                         (fun () -> 
                            let m = defaultArg ownerRangeOpt range0
                            let id = match topArgInfo.Name with | Some id -> id | None -> mkSynId m ""
                            Item.ArgName(id, paramTy, ownerOpt)), 
                         (fun _ _ _ -> true))

    new (cenv, id, ty, container) =
        let argInfo: ArgReprInfo = { Name = Some id; Attribs = [] }
        FSharpParameter(cenv, ty, argInfo, container, None, false, false, false, false, false)

    new (cenv, ty, argInfo: ArgReprInfo, ownerRangeOpt) =
        FSharpParameter(cenv, ty, argInfo, None, ownerRangeOpt, false, false, false, false, false)

    member _.Name = match topArgInfo.Name with None -> None | Some v -> Some v.idText

    member _.cenv: SymbolEnv = cenv

    member _.AdjustType ty = FSharpParameter(cenv, ty, topArgInfo, ownerOpt, ownerRangeOpt, isParamArrayArg, isInArg, isOutArg, isOptionalArg, isWitnessArg)

    member _.Type: FSharpType = FSharpType(cenv, paramTy)

    member _.V = paramTy

    member _.DeclarationLocation =
        match topArgInfo.Name with
        | Some v -> v.idRange
        | None ->

        match ownerRangeOpt with
        | Some m  -> m
        | None -> range0

    member _.Owner =
        match ownerOpt with
        | Some (ArgumentContainer.Method minfo) -> Some (FSharpMemberOrFunctionOrValue (cenv, minfo) :> FSharpSymbol)
        | _ -> None

    override _.Attributes = 
        topArgInfo.Attribs |> List.map (fun a -> FSharpAttribute(cenv, AttribInfo.FSAttribInfo(cenv.g, a))) |> makeReadOnlyCollection

    member _.IsParamArrayArg = isParamArrayArg

    member _.IsInArg = isInArg

    member _.IsOutArg = isOutArg

    member _.IsOptionalArg = isOptionalArg
    
    member _.IsWitnessArg = isWitnessArg
    
    member private x.ValReprInfo = topArgInfo

    override x.Equals(other: obj) =
        box x === other || 
        match other with
        |   :? FSharpParameter as p -> x.Name = p.Name && Range.equals x.DeclarationLocation p.DeclarationLocation
        |   _ -> false

    override x.GetHashCode() = hash (box topArgInfo)

    override x.ToString() = 
        "parameter " + (match x.Name with None -> "<unnamed" | Some s -> s)

type FSharpAssemblySignature (cenv, topAttribs: TopAttribs option, optViewedCcu: CcuThunk option, mtyp: ModuleOrNamespaceType) = 

    // Assembly signature for a referenced/linked assembly
    new (cenv: SymbolEnv, ccu: CcuThunk) = 
        let cenv = if ccu.IsUnresolvedReference then cenv else SymbolEnv(cenv.g, ccu, None, cenv.tcImports)
        FSharpAssemblySignature(cenv, None, Some ccu, ccu.Contents.ModuleOrNamespaceType)
    
    // Assembly signature for an assembly produced via type-checking.
    new (tcGlobals, thisCcu, thisCcuTyp, tcImports, topAttribs, contents) = 
        FSharpAssemblySignature(SymbolEnv(tcGlobals, thisCcu, Some thisCcuTyp, tcImports), topAttribs, None, contents)

    member _.Entities = 

        let rec loop (rmtyp: ModuleOrNamespaceType) = 
            [| for entity in rmtyp.AllEntities do
                   if entity.IsNamespace then 
                       yield! loop entity.ModuleOrNamespaceType
                   else 
                       let entityRef = rescopeEntity optViewedCcu entity 
                       yield FSharpEntity(cenv, entityRef) |]
        
        loop mtyp |> makeReadOnlyCollection

    member _.Attributes =
        [ match optViewedCcu with 
          | Some ccu -> 
                match ccu.TryGetILModuleDef() with 
                | Some ilModule -> 
                    match ilModule.Manifest with 
                    | None -> ()
                    | Some manifest -> 
                        for a in AttribInfosOfIL cenv.g cenv.amap cenv.thisCcu.ILScopeRef range0 manifest.CustomAttrs do
                            yield FSharpAttribute(cenv, a)
                | None -> 
                    // If no module is available, then look in the CCU contents. 
                    if ccu.IsFSharp then
                        for a in ccu.Contents.Attribs do 
                            yield FSharpAttribute(cenv, FSAttribInfo (cenv.g, a))
          | None -> 
              match topAttribs with
              | None -> ()
              | Some tA -> for a in tA.assemblyAttrs do yield FSharpAttribute(cenv, AttribInfo.FSAttribInfo(cenv.g, a)) ]
        |> makeReadOnlyCollection

    member _.FindEntityByPath path =
        let findNested name entity = 
            match entity with
            | Some (e: Entity) ->e.ModuleOrNamespaceType.AllEntitiesByCompiledAndLogicalMangledNames.TryFind name
            | _ -> None

        match path with
        | hd :: tl ->
             (mtyp.AllEntitiesByCompiledAndLogicalMangledNames.TryFind hd, tl) 
             ||> List.fold (fun a x -> findNested x a)  
             |> Option.map (fun e -> FSharpEntity(cenv, rescopeEntity optViewedCcu e))
        | _ -> None

    member x.TryGetEntities() = try x.Entities :> _ seq with _ -> Seq.empty

    override x.ToString() = "<assembly signature>"

type FSharpAssembly internal (cenv, ccu: CcuThunk) = 

    new (tcGlobals, tcImports, ccu: CcuThunk) = 
        FSharpAssembly(SymbolEnv(tcGlobals, ccu, None, tcImports), ccu)

    member _.RawCcuThunk = ccu

    member _.QualifiedName = match ccu.QualifiedName with None -> "" | Some s -> s

    member _.FileName = ccu.FileName

    member _.SimpleName = ccu.AssemblyName 

#if !NO_EXTENSIONTYPING
    member _.IsProviderGenerated = ccu.IsProviderGenerated
#endif

    member _.Contents : FSharpAssemblySignature = FSharpAssemblySignature(cenv, ccu)
                 
    override x.ToString() = 
        match ccu.ILScopeRef with
        | ILScopeRef.PrimaryAssembly ->
            cenv.g.ilg.primaryAssemblyRef.QualifiedName
        | scoref ->
            scoref.QualifiedName

/// Represents open declaration in F# code.
[<Sealed>]
type FSharpOpenDeclaration(target: SynOpenDeclTarget, range: range option, modules: FSharpEntity list, types: FSharpType list, appliedScope: range, isOwnNamespace: bool) =

    member _.Target = target

    member _.LongId = 
        match target with 
        | SynOpenDeclTarget.ModuleOrNamespace(longId, _) -> longId
        | SynOpenDeclTarget.Type(synType, _) ->
            let rec get ty = 
                match ty with 
                | SynType.LongIdent (LongIdentWithDots(lid, _)) -> lid
                | SynType.App (ty2, _, _, _, _, _, _) -> get ty2
                | SynType.LongIdentApp (ty2, _, _, _, _, _, _) -> get ty2
                | SynType.Paren (ty2, _) -> get ty2
                | _ -> []
            get synType

    member _.Range = range

    member _.Types = types

    member _.Modules = modules

    member _.AppliedScope = appliedScope

    member _.IsOwnNamespace = isOwnNamespace

