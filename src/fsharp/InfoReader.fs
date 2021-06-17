// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.


/// Select members from a type by name, searching the type hierarchy if needed
module internal FSharp.Compiler.InfoReader

open System.Collections.Concurrent
open Internal.Utilities.Library
open FSharp.Compiler 
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AccessibilityLogic
open FSharp.Compiler.AttributeChecking
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Features
open FSharp.Compiler.Infos
open FSharp.Compiler.Syntax
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TypeRelations

/// Use the given function to select some of the member values from the members of an F# type
let SelectImmediateMemberVals g optFilter f (tcref: TyconRef) = 
    let chooser (vref: ValRef) = 
        match vref.MemberInfo with 
        // The 'when' condition is a workaround for the fact that values providing 
        // override and interface implementations are published in inferred module types 
        // These cannot be selected directly via the "." notation. 
        // However, it certainly is useful to be able to publish these values, as we can in theory 
        // optimize code to make direct calls to these methods. 
        | Some membInfo when not (ValRefIsExplicitImpl g vref) -> 
            f membInfo vref
        | _ ->  
            None

    match optFilter with 
    | None -> tcref.MembersOfFSharpTyconByName |> NameMultiMap.chooseRange chooser
    | Some nm -> tcref.MembersOfFSharpTyconByName |> NameMultiMap.find nm |> List.choose chooser

/// Check whether a name matches an optional filter
let private checkFilter optFilter (nm: string) = match optFilter with None -> true | Some n2 -> nm = n2

/// Try to select an F# value when querying members, and if so return a MethInfo that wraps the F# value.
let TrySelectMemberVal g optFilter ty pri _membInfo (vref: ValRef) =
    if checkFilter optFilter vref.LogicalName then 
        Some(FSMeth(g, ty, vref, pri))
    else 
        None

let rec GetImmediateIntrinsicMethInfosOfTypeAux (optFilter, ad) g amap m origTy metadataTy =

    let minfos =
        match metadataOfTy g metadataTy with 
#if !NO_EXTENSIONTYPING
        | ProvidedTypeMetadata info -> 
            let st = info.ProvidedType
            let meths = 
                match optFilter with
                | Some name ->  st.PApplyArray ((fun st -> st.GetMethods() |> Array.filter (fun mi -> mi.Name = name) ), "GetMethods", m)
                | None -> st.PApplyArray ((fun st -> st.GetMethods()), "GetMethods", m)
            [   for mi in meths -> ProvidedMeth(amap, mi.Coerce(m), None, m) ]
#endif
        | ILTypeMetadata _ -> 
            let tinfo = ILTypeInfo.FromType g origTy
            let mdefs = tinfo.RawMetadata.Methods
            let mdefs = match optFilter with None -> mdefs.AsList | Some nm -> mdefs.FindByName nm
            mdefs |> List.map (fun mdef -> MethInfo.CreateILMeth(amap, m, origTy, mdef)) 

        | FSharpOrArrayOrByrefOrTupleOrExnTypeMetadata -> 
            // Tuple types also support the methods get_Item1-8, get_Rest from the compiled tuple type.
            // In this case convert to the .NET Tuple type that carries metadata and try again
            if isAnyTupleTy g metadataTy then 
                let betterMetadataTy = convertToTypeWithMetadataIfPossible g metadataTy
                GetImmediateIntrinsicMethInfosOfTypeAux (optFilter, ad) g amap m origTy betterMetadataTy
            // Function types support methods FSharpFunc<_, _>.FromConverter and friends from .NET metadata,
            // but not instance methods (you can't write "f.Invoke(x)", you have to write "f x")
            elif isFunTy g metadataTy then 
                let betterMetadataTy = convertToTypeWithMetadataIfPossible g metadataTy
                GetImmediateIntrinsicMethInfosOfTypeAux (optFilter, ad) g amap m origTy betterMetadataTy
                  |> List.filter (fun minfo -> not minfo.IsInstance)
            else
                match tryTcrefOfAppTy g metadataTy with
                | ValueNone -> []
                | ValueSome tcref ->
                    SelectImmediateMemberVals g optFilter (TrySelectMemberVal g optFilter origTy None) tcref
    let minfos = minfos |> List.filter (IsMethInfoAccessible amap m ad)
    minfos

/// Query the immediate methods of an F# type, not taking into account inherited methods. The optFilter
/// parameter is an optional name to restrict the set of properties returned.
let GetImmediateIntrinsicMethInfosOfType (optFilter, ad) g amap m ty = 
    GetImmediateIntrinsicMethInfosOfTypeAux (optFilter, ad) g amap m ty ty

/// A helper type to help collect properties.
///
/// Join up getters and setters which are not associated in the F# data structure 
type PropertyCollector(g, amap, m, ty, optFilter, ad) = 

    let hashIdentity = 
        HashIdentity.FromFunctions 
            (fun (pinfo: PropInfo) -> hash pinfo.PropertyName) 
            (fun pinfo1 pinfo2 -> 
                pinfo1.IsStatic = pinfo2.IsStatic &&
                PropInfosEquivByNameAndPartialSig EraseNone g amap m pinfo1 pinfo2 &&
                pinfo1.IsDefiniteFSharpOverride = pinfo2.IsDefiniteFSharpOverride )

    let props = new ConcurrentDictionary<PropInfo, PropInfo>(hashIdentity)

    let add pinfo =
        match props.TryGetValue pinfo, pinfo with
        | (true, FSProp (_, ty, Some vref1, _)), FSProp (_, _, _, Some vref2)
        | (true, FSProp (_, ty, _, Some vref2)), FSProp (_, _, Some vref1, _) ->
            let pinfo = FSProp (g, ty, Some vref1, Some vref2)
            props.[pinfo] <- pinfo 
        | (true, _), _ -> 
            // This assert fires while editing bad code. We will give a warning later in check.fs
            //assert ("unexpected case"= "")
            ()
        | _ ->
            props.[pinfo] <- pinfo

    member x.Collect(membInfo: ValMemberInfo, vref: ValRef) = 
        match membInfo.MemberFlags.MemberKind with 
        | SynMemberKind.PropertyGet ->
            let pinfo = FSProp(g, ty, Some vref, None) 
            if checkFilter optFilter vref.PropertyName && IsPropInfoAccessible g amap m ad pinfo then
                add pinfo
        | SynMemberKind.PropertySet ->
            let pinfo = FSProp(g, ty, None, Some vref)
            if checkFilter optFilter vref.PropertyName  && IsPropInfoAccessible g amap m ad pinfo then 
                add pinfo
        | _ -> 
            ()

    member x.Close() = [ for KeyValue(_, pinfo) in props -> pinfo ]

let rec GetImmediateIntrinsicPropInfosOfTypeAux (optFilter, ad) g amap m origTy metadataTy =

    let pinfos =
        match metadataOfTy g metadataTy with 
#if !NO_EXTENSIONTYPING
        | ProvidedTypeMetadata info -> 
            let st = info.ProvidedType
            let matchingProps =
                match optFilter with
                |   Some name ->
                        match st.PApply((fun st -> st.GetProperty name), m) with
                        |   Tainted.Null -> [||]
                        |   pi -> [|pi|]
                |   None ->
                        st.PApplyArray((fun st -> st.GetProperties()), "GetProperties", m)
            matchingProps
            |> Seq.map(fun pi -> ProvidedProp(amap, pi, m)) 
            |> List.ofSeq
#endif

        | ILTypeMetadata _ -> 
            let tinfo = ILTypeInfo.FromType g origTy
            let pdefs = tinfo.RawMetadata.Properties
            let pdefs = match optFilter with None -> pdefs.AsList | Some nm -> pdefs.LookupByName nm
            pdefs |> List.map (fun pdef -> ILProp(ILPropInfo(tinfo, pdef))) 

        | FSharpOrArrayOrByrefOrTupleOrExnTypeMetadata -> 
            // Tuple types also support the properties Item1-8, Rest from the compiled tuple type
            // In this case convert to the .NET Tuple type that carries metadata and try again
            if isAnyTupleTy g metadataTy || isFunTy g metadataTy then 
                let betterMetadataTy = convertToTypeWithMetadataIfPossible g metadataTy
                GetImmediateIntrinsicPropInfosOfTypeAux (optFilter, ad) g amap m origTy betterMetadataTy
            else
                match tryTcrefOfAppTy g metadataTy with
                | ValueNone -> []
                | ValueSome tcref ->
                    let propCollector = new PropertyCollector(g, amap, m, origTy, optFilter, ad)
                    SelectImmediateMemberVals g None (fun membInfo vref -> propCollector.Collect(membInfo, vref); None) tcref |> ignore
                    propCollector.Close()

    let pinfos = pinfos |> List.filter (IsPropInfoAccessible g amap m ad)
    pinfos

/// Query the immediate properties of an F# type, not taking into account inherited properties. The optFilter
/// parameter is an optional name to restrict the set of properties returned.
let rec GetImmediateIntrinsicPropInfosOfType (optFilter, ad) g amap m ty =
    GetImmediateIntrinsicPropInfosOfTypeAux (optFilter, ad) g amap m ty ty

// Checks whether the given type has an indexer property.
let IsIndexerType g amap ty = 
    isArray1DTy g ty ||
    isListTy g ty ||
    match tryTcrefOfAppTy g ty with
    | ValueSome tcref ->
        let _, entityTy = generalizeTyconRef tcref
        let props = GetImmediateIntrinsicPropInfosOfType (None, AccessibleFromSomeFSharpCode) g amap range0 entityTy
        props |> List.exists (fun x -> x.PropertyName = "Item")
    | ValueNone -> false

/// Get the items that are considered the most specific in the hierarchy out of the given items by type.
/// REVIEW: Note complexity O(N^2)
let GetMostSpecificItemsByType g amap f xs =
    [ for x in xs do
        match f x with
        | None -> ()
        | Some (xTy, m) ->
            let isEqual =
                xs
                |> List.forall (fun y ->
                    match f y with
                    | None -> true
                    | Some (yTy, _) ->
                        if typeEquiv g xTy yTy then true
                        else not (TypeFeasiblySubsumesType 0 g amap m xTy CanCoerce yTy))
            if isEqual then
                yield x ]

/// Finds the most specific methods from a method collection by a given method's signature.
let GetMostSpecificMethodInfosByMethInfoSig g amap m (ty, minfo) minfos =
    minfos
    |> GetMostSpecificItemsByType g amap (fun (ty2, minfo2) -> 
        let isEqual =
            typeEquiv g ty ty2 &&
            MethInfosEquivByPartialSig EraseNone true g amap m minfo minfo2
        if isEqual then
            Some(minfo2.ApparentEnclosingType, m)
        else
            None)

/// From the given method sets, filter each set down to the most specific ones. 
let FilterMostSpecificMethInfoSets g amap m (minfoSets: NameMultiMap<_>) : NameMultiMap<_> =
    minfoSets
    |> Map.map (fun _ minfos ->
        ([], minfos)
        ||> List.fold (fun minfoSpecifics (ty, minfo) ->
            let alreadySeen = 
                minfoSpecifics 
                |> List.exists (fun (tySpecific, minfoSpecific) -> 
                    typeEquiv g ty tySpecific &&
                    MethInfosEquivByPartialSig EraseNone true g amap m minfo minfoSpecific)
            if alreadySeen then
                minfoSpecifics
            else
                GetMostSpecificMethodInfosByMethInfoSig g amap m (ty, minfo) minfos @ minfoSpecifics))

/// Sets of methods up the hierarchy, ignoring duplicates by name and sig.
/// Used to collect sets of virtual methods, protected methods, protected
/// properties etc. 
type HierarchyItem = 
    | MethodItem of MethInfo list list
    | PropertyItem of PropInfo list list
    | RecdFieldItem of RecdFieldInfo
    | EventItem of EventInfo list
    | ILFieldItem of ILFieldInfo list

/// An InfoReader is an object to help us read and cache infos. 
/// We create one of these for each file we typecheck. 
type InfoReader(g: TcGlobals, amap: Import.ImportMap) as this =

    /// Get the declared IL fields of a type, not including inherited fields
    let GetImmediateIntrinsicILFieldsOfType (optFilter, ad) m ty =
        let infos =
            match metadataOfTy g ty with 
#if !NO_EXTENSIONTYPING
            | ProvidedTypeMetadata info -> 
                let st = info.ProvidedType
                match optFilter with
                |   None ->
                        [ for fi in st.PApplyArray((fun st -> st.GetFields()), "GetFields", m) -> ProvidedField(amap, fi, m) ]
                |   Some name ->
                        match st.PApply ((fun st -> st.GetField name), m) with
                        |   Tainted.Null -> []
                        |   fi -> [  ProvidedField(amap, fi, m) ]
#endif
            | ILTypeMetadata _ -> 
                let tinfo = ILTypeInfo.FromType g ty
                let fdefs = tinfo.RawMetadata.Fields
                let fdefs = match optFilter with None -> fdefs.AsList | Some nm -> fdefs.LookupByName nm
                fdefs |> List.map (fun pd -> ILFieldInfo(tinfo, pd)) 
            | FSharpOrArrayOrByrefOrTupleOrExnTypeMetadata -> 
                []
        let infos = infos |> List.filter (IsILFieldInfoAccessible g amap m  ad)
        infos           

    /// Get the declared events of a type, not including inherited events, and not including F#-declared CLIEvents
    let ComputeImmediateIntrinsicEventsOfType (optFilter, ad) m ty =
        let infos =
            match metadataOfTy g ty with 
#if !NO_EXTENSIONTYPING
            | ProvidedTypeMetadata info -> 
                let st = info.ProvidedType
                match optFilter with
                |   None ->
                        [   for ei in st.PApplyArray((fun st -> st.GetEvents()), "GetEvents", m) -> ProvidedEvent(amap, ei, m) ]
                |   Some name ->
                        match st.PApply ((fun st -> st.GetEvent name), m) with
                        |   Tainted.Null -> []
                        |   ei -> [  ProvidedEvent(amap, ei, m) ]
#endif
            | ILTypeMetadata _ -> 
                let tinfo = ILTypeInfo.FromType g ty
                let edefs = tinfo.RawMetadata.Events
                let edefs = match optFilter with None -> edefs.AsList | Some nm -> edefs.LookupByName nm
                [ for edef in edefs   do
                    let ileinfo = ILEventInfo(tinfo, edef)
                    if IsILEventInfoAccessible g amap m ad ileinfo then 
                        yield ILEvent ileinfo ]
            | FSharpOrArrayOrByrefOrTupleOrExnTypeMetadata -> 
                []
        infos 

    /// Make a reference to a record or class field
    let MakeRecdFieldInfo g ty (tcref: TyconRef) fspec = 
        RecdFieldInfo(argsOfAppTy g ty, tcref.MakeNestedRecdFieldRef fspec)

    /// Get the F#-declared record fields or class 'val' fields of a type
    let GetImmediateIntrinsicRecdOrClassFieldsOfType (optFilter, _ad) _m ty =
        match tryTcrefOfAppTy g ty with 
        | ValueNone -> []
        | ValueSome tcref -> 
            // Note;secret fields are not allowed in lookups here, as we're only looking
            // up user-visible fields in name resolution.
            match optFilter with
            | Some nm ->
               match tcref.GetFieldByName nm with
               | Some rfield when not rfield.IsCompilerGenerated -> [MakeRecdFieldInfo g ty tcref rfield]
               | _ -> []
            | None -> 
                [ for fdef in tcref.AllFieldsArray do
                    if not fdef.IsCompilerGenerated then
                        yield MakeRecdFieldInfo g ty tcref fdef ]


    /// The primitive reader for the method info sets up a hierarchy
    let GetIntrinsicMethodSetsUncached ((optFilter, ad, allowMultiIntfInst), m, ty) =
        FoldPrimaryHierarchyOfType (fun ty acc -> GetImmediateIntrinsicMethInfosOfType (optFilter, ad) g amap m ty :: acc) g amap m allowMultiIntfInst ty []

    /// The primitive reader for the property info sets up a hierarchy
    let GetIntrinsicPropertySetsUncached ((optFilter, ad, allowMultiIntfInst), m, ty) =
        FoldPrimaryHierarchyOfType (fun ty acc -> GetImmediateIntrinsicPropInfosOfType (optFilter, ad) g amap m ty :: acc) g amap m allowMultiIntfInst ty []

    let GetIntrinsicILFieldInfosUncached ((optFilter, ad), m, ty) =
        FoldPrimaryHierarchyOfType (fun ty acc -> GetImmediateIntrinsicILFieldsOfType (optFilter, ad) m ty @ acc) g amap m AllowMultiIntfInstantiations.Yes ty []

    let GetIntrinsicEventInfosUncached ((optFilter, ad), m, ty) =
        FoldPrimaryHierarchyOfType (fun ty acc -> ComputeImmediateIntrinsicEventsOfType (optFilter, ad) m ty @ acc) g amap m AllowMultiIntfInstantiations.Yes ty []

    let GetIntrinsicRecdOrClassFieldInfosUncached ((optFilter, ad), m, ty) =
        FoldPrimaryHierarchyOfType (fun ty acc -> GetImmediateIntrinsicRecdOrClassFieldsOfType (optFilter, ad) m ty @ acc) g amap m AllowMultiIntfInstantiations.Yes ty []
    
    let GetEntireTypeHierarchyUncached (allowMultiIntfInst, m, ty) =
        FoldEntireHierarchyOfType (fun ty acc -> ty :: acc) g amap m allowMultiIntfInst ty  [] 

    let GetPrimaryTypeHierarchyUncached (allowMultiIntfInst, m, ty) =
        FoldPrimaryHierarchyOfType (fun ty acc -> ty :: acc) g amap m allowMultiIntfInst ty [] 

    /// The primitive reader for the named items up a hierarchy
    let GetIntrinsicNamedItemsUncached ((nm, ad), m, ty) =
        if nm = ".ctor" then None else // '.ctor' lookups only ever happen via constructor syntax
        let optFilter = Some nm
        FoldPrimaryHierarchyOfType (fun ty acc -> 
             let minfos = GetImmediateIntrinsicMethInfosOfType (optFilter, ad) g amap m ty
             let pinfos = GetImmediateIntrinsicPropInfosOfType (optFilter, ad) g amap m ty
             let finfos = GetImmediateIntrinsicILFieldsOfType (optFilter, ad) m ty 
             let einfos = ComputeImmediateIntrinsicEventsOfType (optFilter, ad) m ty 
             let rfinfos = GetImmediateIntrinsicRecdOrClassFieldsOfType (optFilter, ad) m ty 
             match acc with 
             | Some(MethodItem(inheritedMethSets)) when not (isNil minfos) -> Some(MethodItem (minfos :: inheritedMethSets))
             | _ when not (isNil minfos) -> Some(MethodItem ([minfos]))
             | Some(PropertyItem(inheritedPropSets)) when not (isNil pinfos) -> Some(PropertyItem(pinfos :: inheritedPropSets))
             | _ when not (isNil pinfos) -> Some(PropertyItem([pinfos]))
             | _ when not (isNil finfos) -> Some(ILFieldItem(finfos))
             | _ when not (isNil einfos) -> Some(EventItem(einfos))
             | _ when not (isNil rfinfos) -> 
                match rfinfos with
                | [single] -> Some(RecdFieldItem(single))
                | _ -> failwith "Unexpected multiple fields with the same name" // Because an explicit name (i.e., nm) was supplied, there will be only one element at most.
             | _ -> acc)
          g amap m 
          AllowMultiIntfInstantiations.Yes
          ty
          None

    let GetImmediateIntrinsicOverrideMethodSetsOfType optFilter m (interfaceTys: TType list) ty acc =
        match tryAppTy g ty with
        | ValueSome (tcref, _) when tcref.IsILTycon && tcref.ILTyconRawMetadata.IsInterface ->
            let mimpls = tcref.ILTyconRawMetadata.MethodImpls.AsList
            let mdefs = tcref.ILTyconRawMetadata.Methods

            // MethodImpls contains a list of methods that override.
            // OverrideBy is the method that does the overriding.
            // Overrides is the method being overriden.
            (acc, mimpls)
            ||> List.fold (fun acc ilMethImpl ->
                let overridesName = ilMethImpl.Overrides.MethodRef.Name
                let overrideBy = ilMethImpl.OverrideBy
                let canAccumulate =     
                    match optFilter with
                    | None -> true
                    | Some name when name = overridesName -> true
                    | _ -> false
                if canAccumulate then
                    match mdefs.TryFindInstanceByNameAndCallingSignature (overrideBy.Name, overrideBy.MethodRef.CallingSignature) with
                    | Some mdef ->
                        let overridesILTy = ilMethImpl.Overrides.DeclaringType
                        let overridesTyFullName = overridesILTy.TypeRef.FullName
                        let overridesTyOpt = 
                            interfaceTys
                            |> List.tryPick (fun ty -> 
                                match tryTcrefOfAppTy g ty with
                                | ValueSome tcref when tcref.IsILTycon && tcref.ILTyconRawMetadata.Name = overridesTyFullName ->
                                    generalizedTyconRef tcref
                                    |> Some
                                | _ -> 
                                    None)
                        match overridesTyOpt with
                        | Some overridesTy ->
                            NameMultiMap.add overridesName (overridesTy, MethInfo.CreateILMeth(amap, m, ty, mdef)) acc
                        | _ ->
                            acc
                    | _ ->
                        acc
                else
                    acc)
        | _ -> acc

    /// Visiting each type in the hierarchy and accumulate most specific methods that are the OverrideBy target from types.
    let GetIntrinsicMostSpecificOverrideMethodSetsUncached ((optFilter, _ad, allowMultiIntfInst), m, ty) : NameMultiMap<_> =
        let interfaceTys = 
            FoldPrimaryHierarchyOfType (fun ty acc ->
                if isInterfaceTy g ty then ty :: acc
                else acc) g amap m allowMultiIntfInst ty []

        (NameMultiMap.Empty, interfaceTys)
        ||> List.fold (fun acc ty -> GetImmediateIntrinsicOverrideMethodSetsOfType optFilter m interfaceTys ty acc)
        |> FilterMostSpecificMethInfoSets g amap m

    /// Make a cache for function 'f' keyed by type (plus some additional 'flags') that only 
    /// caches computations for monomorphic types.

    let MakeInfoCache f (flagsEq : System.Collections.Generic.IEqualityComparer<_>) = 
        new MemoizationTable<_, _>
             (compute=f,
              // Only cache closed, monomorphic types (closed = all members for the type
              // have been processed). Generic type instantiations could be processed if we had 
              // a decent hash function for these.
              canMemoize=(fun (_flags, (_: range), ty) -> 
                                    match stripTyEqns g ty with 
                                    | TType_app(tcref, []) -> tcref.TypeContents.tcaug_closed 
                                    | _ -> false),
              
              keyComparer=
                 { new System.Collections.Generic.IEqualityComparer<_> with 
                       member x.Equals((flags1, _, typ1), (flags2, _, typ2)) =
                                    // Ignoring the ranges - that's OK.
                                    flagsEq.Equals(flags1, flags2) && 
                                    match stripTyEqns g typ1, stripTyEqns g typ2 with 
                                    | TType_app(tcref1, []), TType_app(tcref2, []) -> tyconRefEq g tcref1 tcref2
                                    | _ -> false
                       member x.GetHashCode((flags, _, ty)) =
                                    // Ignoring the ranges - that's OK.
                                    flagsEq.GetHashCode flags + 
                                    (match stripTyEqns g ty with 
                                     | TType_app(tcref, []) -> hash tcref.LogicalName
                                     | _ -> 0) })
    
    let hashFlags0 = 
        { new System.Collections.Generic.IEqualityComparer<_> with 
               member x.GetHashCode((filter: string option, ad: AccessorDomain, _allowMultiIntfInst1)) = hash filter + AccessorDomain.CustomGetHashCode ad
               member x.Equals((filter1, ad1, allowMultiIntfInst1), (filter2, ad2, allowMultiIntfInst2)) = 
                   (filter1 = filter2) && AccessorDomain.CustomEquals(g, ad1, ad2) && allowMultiIntfInst1 = allowMultiIntfInst2 }

    let hashFlags1 = 
        { new System.Collections.Generic.IEqualityComparer<_> with 
               member x.GetHashCode((filter: string option, ad: AccessorDomain)) = hash filter + AccessorDomain.CustomGetHashCode ad
               member x.Equals((filter1, ad1), (filter2, ad2)) = (filter1 = filter2) && AccessorDomain.CustomEquals(g, ad1, ad2) }

    let hashFlags2 = 
        { new System.Collections.Generic.IEqualityComparer<_> with 
               member x.GetHashCode((nm: string, ad: AccessorDomain)) = hash nm + AccessorDomain.CustomGetHashCode ad
               member x.Equals((nm1, ad1), (nm2, ad2)) = (nm1 = nm2) && AccessorDomain.CustomEquals(g, ad1, ad2) }
                         
    let methodInfoCache = MakeInfoCache GetIntrinsicMethodSetsUncached hashFlags0
    let propertyInfoCache = MakeInfoCache GetIntrinsicPropertySetsUncached hashFlags0
    let recdOrClassFieldInfoCache =  MakeInfoCache GetIntrinsicRecdOrClassFieldInfosUncached hashFlags1
    let ilFieldInfoCache = MakeInfoCache GetIntrinsicILFieldInfosUncached hashFlags1
    let eventInfoCache = MakeInfoCache GetIntrinsicEventInfosUncached hashFlags1
    let namedItemsCache = MakeInfoCache GetIntrinsicNamedItemsUncached hashFlags2
    let mostSpecificOverrideMethodInfoCache = MakeInfoCache GetIntrinsicMostSpecificOverrideMethodSetsUncached hashFlags0

    let entireTypeHierarchyCache = MakeInfoCache GetEntireTypeHierarchyUncached HashIdentity.Structural
    let primaryTypeHierarchyCache = MakeInfoCache GetPrimaryTypeHierarchyUncached HashIdentity.Structural

    // Runtime feature support

    let isRuntimeFeatureSupported (infoReader: InfoReader) runtimeFeature =
        match g.System_Runtime_CompilerServices_RuntimeFeature_ty with
        | Some runtimeFeatureTy ->
            infoReader.GetILFieldInfosOfType (None, AccessorDomain.AccessibleFromEverywhere, range0, runtimeFeatureTy)
            |> List.exists (fun (ilFieldInfo: ILFieldInfo) -> ilFieldInfo.FieldName = runtimeFeature)
        | _ ->
            false

    let isRuntimeFeatureDefaultImplementationsOfInterfacesSupported =
        lazy isRuntimeFeatureSupported this "DefaultImplementationsOfInterfaces"
                                            
    member x.g = g
    member x.amap = amap
    
    /// Read the raw method sets of a type, including inherited ones. Cache the result for monomorphic types
    member x.GetRawIntrinsicMethodSetsOfType (optFilter, ad, allowMultiIntfInst, m, ty) =
        methodInfoCache.Apply(((optFilter, ad, allowMultiIntfInst), m, ty))

    /// Read the raw property sets of a type, including inherited ones. Cache the result for monomorphic types
    member x.GetRawIntrinsicPropertySetsOfType (optFilter, ad, allowMultiIntfInst, m, ty) =
        propertyInfoCache.Apply(((optFilter, ad, allowMultiIntfInst), m, ty))

    /// Read the record or class fields of a type, including inherited ones. Cache the result for monomorphic types.
    member x.GetRecordOrClassFieldsOfType (optFilter, ad, m, ty) =
        recdOrClassFieldInfoCache.Apply(((optFilter, ad), m, ty))

    /// Read the IL fields of a type, including inherited ones. Cache the result for monomorphic types.
    member x.GetILFieldInfosOfType (optFilter, ad, m, ty) =
        ilFieldInfoCache.Apply(((optFilter, ad), m, ty))

    member x.GetImmediateIntrinsicEventsOfType (optFilter, ad, m, ty) = ComputeImmediateIntrinsicEventsOfType (optFilter, ad) m ty

    /// Read the events of a type, including inherited ones. Cache the result for monomorphic types.
    member x.GetEventInfosOfType (optFilter, ad, m, ty) =
        eventInfoCache.Apply(((optFilter, ad), m, ty))

    /// Try and find a record or class field for a type.
    member x.TryFindRecdOrClassFieldInfoOfType (nm, m, ty) =
        match recdOrClassFieldInfoCache.Apply((Some nm, AccessibleFromSomewhere), m, ty) with
        | [] -> ValueNone
        | [single] -> ValueSome single
        | flds ->
            // multiple fields with the same name can come from different classes,
            // so filter them by the given type name
            match tryTcrefOfAppTy g ty with 
            | ValueNone -> ValueNone
            | ValueSome tcref ->
                match flds |> List.filter (fun rfinfo -> tyconRefEq g tcref rfinfo.TyconRef) with
                | [] -> ValueNone
                | [single] -> ValueSome single
                | _ -> failwith "unexpected multiple fields with same name" // Because it should have been already reported as duplicate fields

    /// Try and find an item with the given name in a type.
    member x.TryFindNamedItemOfType (nm, ad, m, ty) =
        namedItemsCache.Apply(((nm, ad), m, ty))

    /// Read the raw method sets of a type that are the most specific overrides. Cache the result for monomorphic types
    member x.GetIntrinsicMostSpecificOverrideMethodSetsOfType (optFilter, ad, allowMultiIntfInst, m, ty) =
        mostSpecificOverrideMethodInfoCache.Apply(((optFilter, ad, allowMultiIntfInst), m, ty))

    /// Get the super-types of a type, including interface types.
    member x.GetEntireTypeHierarchy (allowMultiIntfInst, m, ty) =
        entireTypeHierarchyCache.Apply((allowMultiIntfInst, m, ty))

    /// Get the super-types of a type, excluding interface types.
    member x.GetPrimaryTypeHierarchy (allowMultiIntfInst, m, ty) =
        primaryTypeHierarchyCache.Apply((allowMultiIntfInst, m, ty))

    /// Check if the given language feature is supported by the runtime.
    member x.IsLanguageFeatureRuntimeSupported langFeature =
        match langFeature with
        // Both default and static interface method consumption features are tied to the runtime support of DIMs.
        | LanguageFeature.DefaultInterfaceMemberConsumption -> isRuntimeFeatureDefaultImplementationsOfInterfacesSupported.Value
        | _ -> true
            
let private tryLanguageFeatureRuntimeErrorAux (infoReader: InfoReader) langFeature m error =
    if not (infoReader.IsLanguageFeatureRuntimeSupported langFeature) then
        let featureStr = infoReader.g.langVersion.GetFeatureString langFeature
        error (Error(FSComp.SR.chkFeatureNotRuntimeSupported featureStr, m))
        false
    else
        true

let checkLanguageFeatureRuntimeError infoReader langFeature m =
    tryLanguageFeatureRuntimeErrorAux infoReader langFeature m error |> ignore

let checkLanguageFeatureRuntimeErrorRecover infoReader langFeature m =
    tryLanguageFeatureRuntimeErrorAux infoReader langFeature m errorR |> ignore

let tryLanguageFeatureRuntimeErrorRecover infoReader langFeature m =
    tryLanguageFeatureRuntimeErrorAux infoReader langFeature m errorR

/// Get the declared constructors of any F# type
let rec GetIntrinsicConstructorInfosOfTypeAux (infoReader: InfoReader) m origTy metadataTy = 
  protectAssemblyExploration [] (fun () -> 
    let g = infoReader.g
    let amap = infoReader.amap 
    match metadataOfTy g metadataTy with 
#if !NO_EXTENSIONTYPING
    | ProvidedTypeMetadata info -> 
        let st = info.ProvidedType
        [ for ci in st.PApplyArray((fun st -> st.GetConstructors()), "GetConstructors", m) do
                yield ProvidedMeth(amap, ci.Coerce(m), None, m) ]
#endif
    | ILTypeMetadata _ -> 
        let tinfo = ILTypeInfo.FromType g origTy
        tinfo.RawMetadata.Methods.FindByName ".ctor" 
        |> List.filter (fun md -> md.IsConstructor) 
        |> List.map (fun mdef -> MethInfo.CreateILMeth (amap, m, origTy, mdef)) 

    | FSharpOrArrayOrByrefOrTupleOrExnTypeMetadata -> 
        // Tuple types also support constructors. In this case convert to the .NET Tuple type that carries metadata and try again
        // Function types also support constructors. In this case convert to the FSharpFunc type that carries metadata and try again
        if isAnyTupleTy g metadataTy || isFunTy g metadataTy then 
            let betterMetadataTy = convertToTypeWithMetadataIfPossible g metadataTy
            GetIntrinsicConstructorInfosOfTypeAux infoReader m origTy betterMetadataTy
        else
            match tryTcrefOfAppTy g metadataTy with
            | ValueNone -> []
            | ValueSome tcref -> 
                tcref.MembersOfFSharpTyconByName 
                |> NameMultiMap.find ".ctor"
                |> List.choose(fun vref -> 
                    match vref.MemberInfo with 
                    | Some membInfo when (membInfo.MemberFlags.MemberKind = SynMemberKind.Constructor) -> Some vref 
                    | _ -> None) 
                |> List.map (fun x -> FSMeth(g, origTy, x, None)) 
  )    

let GetIntrinsicConstructorInfosOfType infoReader m ty = 
    GetIntrinsicConstructorInfosOfTypeAux infoReader m ty ty

//-------------------------------------------------------------------------
// Collecting methods and properties taking into account hiding rules in the hierarchy

  
/// Indicates if we prefer overrides or abstract slots. 
type FindMemberFlag = 
    /// Prefer items toward the top of the hierarchy, which we do if the items are virtual 
    /// but not when resolving base calls. 
    | IgnoreOverrides 
    /// Get overrides instead of abstract slots when measuring whether a class/interface implements all its required slots. 
    | PreferOverrides

/// The input list is sorted from most-derived to least-derived type, so any System.Object methods 
/// are at the end of the list. Return a filtered list where prior/subsequent members matching by name and 
/// that are in the same equivalence class have been removed. We keep a name-indexed table to 
/// be more efficient when we check to see if we've already seen a particular named method. 
type private IndexedList<'T>(itemLists: 'T list list, itemsByName: NameMultiMap<'T>) = 
    
    /// Get the item sets
    member x.Items = itemLists

    /// Get the items with a particular name
    member x.ItemsWithName(nm)  = NameMultiMap.find nm itemsByName

    /// Add new items, extracting the names using the given function.
    member x.AddItems(items, nmf) = IndexedList<'T>(items :: itemLists, List.foldBack (fun x acc -> NameMultiMap.add (nmf x) x acc) items itemsByName )

    /// Get an empty set of items
    static member Empty = IndexedList<'T>([], NameMultiMap.empty)

    /// Filter a set of new items to add according to the content of the list.  Only keep an item
    /// if it passes 'keepTest' for all matching items already in the list.
    member x.FilterNewItems keepTest nmf itemsToAdd =
        // Have we already seen an item with the same name and that is in the same equivalence class?
        // If so, ignore this one. Note we can check against the original incoming 'ilist' because we are assuming that
        // none the elements of 'itemsToAdd' are equivalent. 
        itemsToAdd |> List.filter (fun item -> List.forall (keepTest item) (x.ItemsWithName(nmf item)))

/// Add all the items to the IndexedList, preferring the ones in the super-types. This is used to hide methods
/// in super classes and/or hide overrides of methods in subclasses.
///
/// Assume no items in 'items' are equivalent according to 'equivTest'. This is valid because each step in a
/// .NET class hierarchy introduces a consistent set of methods, none of which hide each other within the 
/// given set. This is an important optimization because it means we don't have filter for equivalence between the 
/// large overload sets introduced by methods like System.WriteLine.
///
/// Assume items can be given names by 'nmf', where two items with different names are
/// not equivalent.

let private FilterItemsInSubTypesBasedOnItemsInSuperTypes nmf keepTest itemLists = 
    let rec loop itemLists = 
        match itemLists with
        | [] -> IndexedList.Empty
        | items :: itemsInSuperTypes -> 
            let ilist = loop itemsInSuperTypes
            let itemsToAdd = ilist.FilterNewItems keepTest nmf items 
            ilist.AddItems(itemsToAdd, nmf)
    (loop itemLists).Items

/// Add all the items to the IndexedList, preferring the ones in the sub-types.
let private FilterItemsInSuperTypesBasedOnItemsInSubTypes nmf keepTest itemLists  = 
    let rec loop itemLists (indexedItemsInSubTypes: IndexedList<_>) = 
        match itemLists with
        | [] -> List.rev indexedItemsInSubTypes.Items
        | items :: itemsInSuperTypes -> 
            let itemsToAdd = items |> List.filter (fun item -> keepTest item (indexedItemsInSubTypes.ItemsWithName(nmf item)))            
            let ilist = indexedItemsInSubTypes.AddItems(itemsToAdd, nmf)
            loop itemsInSuperTypes ilist

    loop itemLists IndexedList.Empty

let private ExcludeItemsInSuperTypesBasedOnEquivTestWithItemsInSubTypes nmf equivTest itemLists = 
    FilterItemsInSuperTypesBasedOnItemsInSubTypes nmf (fun item1 items -> not (items |> List.exists (fun item2 -> equivTest item1 item2))) itemLists 

/// Filter the overrides of methods or properties, either keeping the overrides or keeping the dispatch slots.
let private FilterOverrides findFlag (isVirt:'a->bool, isNewSlot, isDefiniteOverride, isFinal, equivSigs, nmf:'a->string) items = 
    let equivVirts x y = isVirt x && isVirt y && equivSigs x y

    match findFlag with 
    | PreferOverrides -> 
        items
        // For each F#-declared override, get rid of any equivalent abstract member in the same type
        // This is because F# abstract members with default overrides give rise to two members with the
        // same logical signature in the same type, e.g.
        // type ClassType1() =
        //      abstract VirtualMethod1: string -> int
        //      default x.VirtualMethod1(s) = 3
        
        |> List.map (fun items -> 
            let definiteOverrides = items |> List.filter isDefiniteOverride 
            items |> List.filter (fun item -> (isDefiniteOverride item || not (List.exists (equivVirts item) definiteOverrides))))
       
        // only keep virtuals that are not signature-equivalent to virtuals in subtypes
        |> ExcludeItemsInSuperTypesBasedOnEquivTestWithItemsInSubTypes nmf equivVirts 
    | IgnoreOverrides ->  
        let equivNewSlots x y = isNewSlot x && isNewSlot y && equivSigs x y
        items
          // Remove any F#-declared overrides. These may occur in the same type as the abstract member (unlike with .NET metadata)
          // Include any 'newslot' declared methods.
          |> List.map (List.filter (fun x -> not (isDefiniteOverride x))) 

          // Remove any virtuals that are signature-equivalent to virtuals in subtypes, except for newslots
          // That is, keep if it's 
          ///      (a) not virtual
          //       (b) is a new slot or 
          //       (c) not equivalent
          // We keep virtual finals around for error detection later on
          |> FilterItemsInSubTypesBasedOnItemsInSuperTypes nmf (fun newItem priorItem  ->
                 (isVirt newItem && isFinal newItem) || not (isVirt newItem) || isNewSlot newItem || not (equivVirts newItem priorItem) )

          // Remove any abstract slots in supertypes that are (a) hidden by another newslot and (b) implemented
          // We leave unimplemented ones around to give errors, e.g. for
          // [<AbstractClass>]
          //   type PA() =
          //   abstract M : int -> unit
          // 
          //   [<AbstractClass>]
          //   type PB<'a>() =
          //       inherit PA()
          //       abstract M : 'a -> unit
          // 
          //   [<AbstractClass>]
          //   type PC() =
          //       inherit PB<int>()
          //       // Here, PA.M and PB<int>.M have the same signature, so PA.M is unimplementable.
          //       // REVIEW: in future we may give a friendly error at this point
          // 
          //   type PD() = 
          //       inherit PC()
          //       override this.M(x: int) = ()

          |> FilterItemsInSuperTypesBasedOnItemsInSubTypes nmf (fun item1 superTypeItems -> 
                  not (isNewSlot item1 && 
                       superTypeItems |> List.exists (equivNewSlots item1) &&
                       superTypeItems |> List.exists (fun item2 -> isDefiniteOverride item1 && equivVirts item1 item2))) 

    
/// Filter the overrides of methods, either keeping the overrides or keeping the dispatch slots.
let private FilterOverridesOfMethInfos findFlag g amap m minfos = 
    minfos 
    |> FilterOverrides findFlag 
        ((fun (minfo: MethInfo) -> minfo.IsVirtual),
         (fun minfo -> minfo.IsNewSlot),
         (fun minfo -> minfo.IsDefiniteFSharpOverride),
         (fun minfo -> minfo.IsFinal),
         MethInfosEquivByNameAndSig EraseNone true g amap m,
         (fun minfo -> minfo.LogicalName)) 

/// Filter the overrides of properties, either keeping the overrides or keeping the dispatch slots.
let private FilterOverridesOfPropInfos findFlag g amap m props = 
    props 
    |> FilterOverrides findFlag 
          ((fun (pinfo: PropInfo) -> pinfo.IsVirtualProperty),
           (fun pinfo -> pinfo.IsNewSlot),
           (fun pinfo -> pinfo.IsDefiniteFSharpOverride),
           (fun _ -> false),
           PropInfosEquivByNameAndSig EraseNone g amap m,
           (fun pinfo -> pinfo.PropertyName)) 

/// Exclude methods from super types which have the same signature as a method in a more specific type.
let ExcludeHiddenOfMethInfos g amap m (minfos: MethInfo list list) = 
    minfos
    |> ExcludeItemsInSuperTypesBasedOnEquivTestWithItemsInSubTypes 
        (fun minfo -> minfo.LogicalName)
        (fun m1 m2 -> 
             // only hide those truly from super classes 
             not (tyconRefEq g m1.DeclaringTyconRef m2.DeclaringTyconRef) &&
             MethInfosEquivByNameAndPartialSig EraseNone true g amap m m1 m2)
        
    |> List.concat

/// Exclude properties from super types which have the same name as a property in a more specific type.
let ExcludeHiddenOfPropInfos g amap m pinfos = 
    pinfos 
    |> ExcludeItemsInSuperTypesBasedOnEquivTestWithItemsInSubTypes (fun (pinfo: PropInfo) -> pinfo.PropertyName) (PropInfosEquivByNameAndPartialSig EraseNone g amap m) 
    |> List.concat

/// Get the sets of intrinsic methods in the hierarchy (not including extension methods)
let GetIntrinsicMethInfoSetsOfType (infoReader:InfoReader) optFilter ad allowMultiIntfInst findFlag m ty = 
    infoReader.GetRawIntrinsicMethodSetsOfType(optFilter, ad, allowMultiIntfInst, m, ty)
    |> FilterOverridesOfMethInfos findFlag infoReader.g infoReader.amap m
  
/// Get the sets intrinsic properties in the hierarchy (not including extension properties)
let GetIntrinsicPropInfoSetsOfType (infoReader:InfoReader) optFilter ad allowMultiIntfInst findFlag m ty = 
    infoReader.GetRawIntrinsicPropertySetsOfType(optFilter, ad, allowMultiIntfInst, m, ty) 
    |> FilterOverridesOfPropInfos findFlag infoReader.g infoReader.amap m

/// Get the flattened list of intrinsic methods in the hierarchy
let GetIntrinsicMethInfosOfType infoReader optFilter ad allowMultiIntfInst findFlag m ty = 
    GetIntrinsicMethInfoSetsOfType infoReader optFilter ad allowMultiIntfInst findFlag m ty |> List.concat
  
/// Get the flattened list of intrinsic properties in the hierarchy
let GetIntrinsicPropInfosOfType infoReader optFilter ad allowMultiIntfInst findFlag m ty = 
    GetIntrinsicPropInfoSetsOfType infoReader optFilter ad allowMultiIntfInst findFlag m ty  |> List.concat

/// Perform type-directed name resolution of a particular named member in an F# type
let TryFindIntrinsicNamedItemOfType (infoReader: InfoReader) (nm, ad) findFlag m ty = 
    match infoReader.TryFindNamedItemOfType(nm, ad, m, ty) with
    | Some item -> 
        match item with 
        | PropertyItem psets -> Some(PropertyItem (psets |> FilterOverridesOfPropInfos findFlag infoReader.g infoReader.amap m))
        | MethodItem msets -> Some(MethodItem (msets |> FilterOverridesOfMethInfos findFlag infoReader.g infoReader.amap m))
        | _ -> Some(item)
    | None -> None

/// Try to detect the existence of a method on a type.
/// Used for 
///     -- getting the GetEnumerator, get_Current, MoveNext methods for enumerable types 
///     -- getting the Dispose method when resolving the 'use' construct 
///     -- getting the various methods used to desugar the computation expression syntax 
let TryFindIntrinsicMethInfo infoReader m ad nm ty = 
    GetIntrinsicMethInfosOfType infoReader (Some nm) ad AllowMultiIntfInstantiations.Yes IgnoreOverrides m ty 

/// Try to find a particular named property on a type. Only used to ensure that local 'let' definitions and property names
/// are distinct, a somewhat adhoc check in tc.fs.
let TryFindPropInfo infoReader m ad nm ty = 
    GetIntrinsicPropInfosOfType infoReader (Some nm) ad AllowMultiIntfInstantiations.Yes IgnoreOverrides m ty 

/// Get a set of most specific override methods.
let GetIntrinisicMostSpecificOverrideMethInfoSetsOfType (infoReader: InfoReader) m ty =
    infoReader.GetIntrinsicMostSpecificOverrideMethodSetsOfType (None, AccessibleFromSomewhere, AllowMultiIntfInstantiations.Yes, m, ty)

//-------------------------------------------------------------------------
// Helpers related to delegates and events - these use method searching hence are in this file
//------------------------------------------------------------------------- 

/// The Invoke MethInfo, the function argument types, the function return type 
/// and the overall F# function type for the function type associated with a .NET delegate type
[<NoEquality;NoComparison>]
type SigOfFunctionForDelegate = SigOfFunctionForDelegate of MethInfo * TType list * TType * TType

/// Given a delegate type work out the minfo, argument types, return type 
/// and F# function type by looking at the Invoke signature of the delegate. 
let GetSigOfFunctionForDelegate (infoReader: InfoReader) delty m ad =
    let g = infoReader.g
    let amap = infoReader.amap
    let invokeMethInfo = 
        match GetIntrinsicMethInfosOfType infoReader (Some "Invoke") ad AllowMultiIntfInstantiations.Yes IgnoreOverrides m delty with 
        | [h] -> h
        | [] -> error(Error(FSComp.SR.noInvokeMethodsFound (), m))
        | h :: _ -> warning(InternalError(FSComp.SR.moreThanOneInvokeMethodFound (), m)); h
    
    let minst = []   // a delegate's Invoke method is never generic 
    let compiledViewOfDelArgTys = 
        match invokeMethInfo.GetParamTypes(amap, m, minst) with 
        | [args] -> args
        | _ -> error(Error(FSComp.SR.delegatesNotAllowedToHaveCurriedSignatures (), m))
    let fsharpViewOfDelArgTys = 
        match compiledViewOfDelArgTys with 
        | [] -> [g.unit_ty] 
        | _ -> compiledViewOfDelArgTys
    let delRetTy = invokeMethInfo.GetFSharpReturnTy(amap, m, minst)
    CheckMethInfoAttributes g m None invokeMethInfo |> CommitOperationResult
    let fty = mkIteratedFunTy fsharpViewOfDelArgTys delRetTy
    SigOfFunctionForDelegate(invokeMethInfo, compiledViewOfDelArgTys, delRetTy, fty)

/// Try and interpret a delegate type as a "standard" .NET delegate type associated with an event, with a "sender" parameter.
let TryDestStandardDelegateType (infoReader: InfoReader) m ad delTy =
    let g = infoReader.g
    let (SigOfFunctionForDelegate(_, compiledViewOfDelArgTys, delRetTy, _)) = GetSigOfFunctionForDelegate infoReader delTy m ad
    match compiledViewOfDelArgTys with 
    | senderTy :: argTys when (isObjTy g senderTy) && not (List.exists (isByrefTy g) argTys)  -> Some(mkRefTupledTy g argTys, delRetTy)
    | _ -> None


/// Indicates if an event info is associated with a delegate type that is a "standard" .NET delegate type
/// with a sender parameter.
//
/// In the F# design, we take advantage of the following idiom to simplify away the bogus "object" parameter of the 
/// of the "Add" methods associated with events.  If you want to access it you
/// can use AddHandler instead.
   
/// The .NET Framework guidelines indicate that the delegate type used for
/// an event should take two parameters, an "object source" parameter
/// indicating the source of the event, and an "e" parameter that
/// encapsulates any additional information about the event. The type of
/// the "e" parameter should derive from the EventArgs class. For events
/// that do not use any additional information, the .NET Framework has
/// already defined an appropriate delegate type: EventHandler.
/// (from http://msdn.microsoft.com/library/default.asp?url=/library/en-us/csref/html/vcwlkEventsTutorial.asp) 
let IsStandardEventInfo (infoReader: InfoReader) m ad (einfo: EventInfo) =
    let dty = einfo.GetDelegateType(infoReader.amap, m)
    match TryDestStandardDelegateType infoReader m ad dty with
    | Some _ -> true
    | None -> false

/// Get the (perhaps tupled) argument type accepted by an event 
let ArgsTypOfEventInfo (infoReader: InfoReader) m ad (einfo: EventInfo)  =
    let amap = infoReader.amap
    let dty = einfo.GetDelegateType(amap, m)
    match TryDestStandardDelegateType infoReader m ad dty with
    | Some(argtys, _) -> argtys
    | None -> error(nonStandardEventError einfo.EventName m)

/// Get the type of the event when looked at as if it is a property 
/// Used when displaying the property in Intellisense 
let PropTypOfEventInfo (infoReader: InfoReader) m ad (einfo: EventInfo) =  
    let g = infoReader.g
    let amap = infoReader.amap
    let delTy = einfo.GetDelegateType(amap, m)
    let argsTy = ArgsTypOfEventInfo infoReader m ad einfo 
    mkIEventType g delTy argsTy

/// Try to find the name of the metadata file for this external definition 
let TryFindMetadataInfoOfExternalEntityRef (infoReader: InfoReader) m eref = 
    let g = infoReader.g
    match eref with 
    | ERefLocal _ -> None
    | ERefNonLocal nlref -> 
        // Generalize to get a formal signature 
        let formalTypars = eref.Typars m
        let formalTypeInst = generalizeTypars formalTypars
        let ty = TType_app(eref, formalTypeInst)
        if isILAppTy g ty then
            let formalTypeInfo = ILTypeInfo.FromType g ty
            Some(nlref.Ccu.FileName, formalTypars, formalTypeInfo)
        else None

/// Try to find the xml doc associated with the assembly name and xml doc signature
let TryFindXmlDocByAssemblyNameAndSig (infoReader: InfoReader) assemblyName xmlDocSig =
    infoReader.amap.assemblyLoader.TryFindXmlDocumentationInfo(assemblyName)
    |> Option.bind (fun xmlDocInfo ->
        xmlDocInfo.TryGetXmlDocBySig(xmlDocSig)
    )

let private libFileOfEntityRef x =
    match x with
    | ERefLocal _ -> None
    | ERefNonLocal nlref -> nlref.Ccu.FileName 

let GetXmlDocSigOfEntityRef infoReader m (eref: EntityRef) = 
    if eref.IsILTycon then 
        match TryFindMetadataInfoOfExternalEntityRef infoReader m eref  with
        | None -> None
        | Some (ccuFileName, _, formalTypeInfo) -> Some(ccuFileName, "T:"+formalTypeInfo.ILTypeRef.FullName)
    else
        let ccuFileName = libFileOfEntityRef eref
        let m = eref.Deref
        if m.XmlDocSig = "" then
            m.XmlDocSig <- XmlDocSigOfEntity eref
        Some (ccuFileName, m.XmlDocSig)

let GetXmlDocSigOfScopedValRef g (tcref: TyconRef) (vref: ValRef) = 
    let ccuFileName = libFileOfEntityRef tcref
    let v = vref.Deref
    if v.XmlDocSig = "" && v.HasDeclaringEntity then
        let ap = buildAccessPath vref.TopValDeclaringEntity.CompilationPathOpt
        let path =
            if vref.TopValDeclaringEntity.IsModule then
                let sep = if ap.Length > 0 then "." else ""
                ap + sep + vref.TopValDeclaringEntity.CompiledName
            else
                ap
        v.XmlDocSig <- XmlDocSigOfVal g false path v
    Some (ccuFileName, v.XmlDocSig)                

let GetXmlDocSigOfRecdFieldRef (rfref: RecdFieldRef) = 
    let tcref = rfref.TyconRef
    let ccuFileName = libFileOfEntityRef tcref 
    if rfref.RecdField.XmlDocSig = "" then
        rfref.RecdField.XmlDocSig <- XmlDocSigOfProperty [tcref.CompiledRepresentationForNamedType.FullName; rfref.RecdField.Name]
    Some (ccuFileName, rfref.RecdField.XmlDocSig)

let GetXmlDocSigOfUnionCaseRef (ucref: UnionCaseRef) = 
    let tcref =  ucref.TyconRef
    let ccuFileName = libFileOfEntityRef tcref
    if  ucref.UnionCase.XmlDocSig = "" then
        ucref.UnionCase.XmlDocSig <- XmlDocSigOfUnionCase [tcref.CompiledRepresentationForNamedType.FullName; ucref.CaseName]
    Some (ccuFileName, ucref.UnionCase.XmlDocSig)

let GetXmlDocSigOfMethInfo (infoReader: InfoReader)  m (minfo: MethInfo) = 
    let amap = infoReader.amap
    match minfo with
    | FSMeth (g, _, vref, _) ->
        GetXmlDocSigOfScopedValRef g minfo.DeclaringTyconRef vref
    | ILMeth (g, ilminfo, _) ->            
        let actualTypeName = ilminfo.DeclaringTyconRef.CompiledRepresentationForNamedType.FullName
        let fmtps = ilminfo.FormalMethodTypars            
        let genArity = if fmtps.Length=0 then "" else sprintf "``%d" fmtps.Length

        match TryFindMetadataInfoOfExternalEntityRef infoReader m ilminfo.DeclaringTyconRef  with 
        | None -> None
        | Some (ccuFileName, formalTypars, formalTypeInfo) ->
            let filminfo = ILMethInfo(g, formalTypeInfo.ToType, None, ilminfo.RawMetadata, fmtps) 
            let args = 
                match ilminfo.IsILExtensionMethod with
                | true -> filminfo.GetRawArgTypes(amap, m, minfo.FormalMethodInst)
                | false -> filminfo.GetParamTypes(amap, m, minfo.FormalMethodInst)

            // http://msdn.microsoft.com/en-us/library/fsbx0t7x.aspx
            // If the name of the item itself has periods, they are replaced by the hash-sign ('#'). 
            // It is assumed that no item has a hash-sign directly in its name. For example, the fully 
            // qualified name of the String constructor would be "System.String.#ctor".
            let normalizedName = ilminfo.ILName.Replace(".", "#")

            Some (ccuFileName, "M:"+actualTypeName+"."+normalizedName+genArity+XmlDocArgsEnc g (formalTypars, fmtps) args)
    | DefaultStructCtor _ -> None
#if !NO_EXTENSIONTYPING
    | ProvidedMeth _ -> None
#endif

let GetXmlDocSigOfValRef g (vref: ValRef) =
    if not vref.IsLocalRef then
        let ccuFileName = vref.nlr.Ccu.FileName
        let v = vref.Deref
        if v.XmlDocSig = "" && v.HasDeclaringEntity then
            v.XmlDocSig <- XmlDocSigOfVal g false vref.TopValDeclaringEntity.CompiledRepresentationForNamedType.Name v
        Some (ccuFileName, v.XmlDocSig)
    else 
        match vref.ApparentEnclosingEntity with
        | Parent tcref ->
            GetXmlDocSigOfScopedValRef g tcref vref
        | _ ->
            None

let GetXmlDocSigOfProp infoReader m (pinfo: PropInfo) =
    let g = pinfo.TcGlobals
    match pinfo with 
#if !NO_EXTENSIONTYPING
    | ProvidedProp _ -> None // No signature is possible. If an xml comment existed it would have been returned by PropInfo.XmlDoc in infos.fs
#endif
    | FSProp _ as fspinfo -> 
        match fspinfo.ArbitraryValRef with 
        | None -> None
        | Some vref -> GetXmlDocSigOfScopedValRef g pinfo.DeclaringTyconRef vref
    | ILProp(ILPropInfo(_, pdef)) -> 
        match TryFindMetadataInfoOfExternalEntityRef infoReader m pinfo.DeclaringTyconRef with
        | Some (ccuFileName, formalTypars, formalTypeInfo) ->
            let filpinfo = ILPropInfo(formalTypeInfo, pdef)
            Some (ccuFileName, "P:"+formalTypeInfo.ILTypeRef.FullName+"."+pdef.Name+XmlDocArgsEnc g (formalTypars, []) (filpinfo.GetParamTypes(infoReader.amap, m)))
        | _ -> None

let GetXmlDocSigOfEvent infoReader m (einfo: EventInfo) =
    match einfo with
    | ILEvent _ ->
        match TryFindMetadataInfoOfExternalEntityRef infoReader m einfo.DeclaringTyconRef with 
        | Some (ccuFileName, _, formalTypeInfo) -> 
            Some(ccuFileName, "E:"+formalTypeInfo.ILTypeRef.FullName+"."+einfo.EventName)
        | _ -> None
    | _ -> None

let GetXmlDocSigOfILFieldInfo infoReader m (finfo: ILFieldInfo) =
    match TryFindMetadataInfoOfExternalEntityRef infoReader m finfo.DeclaringTyconRef with
    | Some (ccuFileName, _, formalTypeInfo) ->
        Some(ccuFileName, "F:"+formalTypeInfo.ILTypeRef.FullName+"."+finfo.FieldName)
    | _ -> None
