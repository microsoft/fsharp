// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Primary logic related to method overrides.
module internal FSharp.Compiler.MethodOverrides

open Internal.Utilities.Library 
open Internal.Utilities.Library.Extras
open FSharp.Compiler 
open FSharp.Compiler.AccessibilityLogic
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.InfoReader
open FSharp.Compiler.Infos
open FSharp.Compiler.Features
open FSharp.Compiler.NameResolution
open FSharp.Compiler.Syntax
open FSharp.Compiler.SyntaxTreeOps
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Compiler.Text
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.TypeRelations

//-------------------------------------------------------------------------
// Completeness of classes
//------------------------------------------------------------------------- 

type OverrideCanImplement = 
    | CanImplementAnyInterfaceSlot
    | CanImplementAnyClassHierarchySlot
    | CanImplementAnySlot
    | CanImplementNoSlots
    
/// The overall information about a method implementation in a class or object expression 
type OverrideInfo = 
    | Override of OverrideCanImplement * TyconRef * Ident * (Typars * TyparInst) * TType list list * TType option * bool * bool
    member x.CanImplement = let (Override(a, _, _, _, _, _, _, _)) = x in a
    member x.BoundingTyconRef = let (Override(_, ty, _, _, _, _, _, _)) = x in ty
    member x.LogicalName = let (Override(_, _, id, _, _, _, _, _)) = x in id.idText
    member x.Range = let (Override(_, _, id, _, _, _, _, _)) = x in id.idRange
    member x.IsFakeEventProperty = let (Override(_, _, _, _, _, _, b, _)) = x in b
    member x.ArgTypes = let (Override(_, _, _, _, b, _, _, _)) = x in b
    member x.ReturnType = let (Override(_, _, _, _, _, b, _, _)) = x in b
    member x.IsCompilerGenerated = let (Override(_, _, _, _, _, _, _, b)) = x in b

type RequiredSlot = 
    | RequiredSlot of MethInfo * isOptional: bool
    | DefaultInterfaceImplementationSlot of MethInfo * isOptional: bool * possiblyNoMostSpecific: bool

    /// A slot which does not have to be implemented, because an inherited implementation is available.
    member this.IsOptional =
        match this with
        | RequiredSlot(_, isOptional)
        | DefaultInterfaceImplementationSlot(_, isOptional, _) -> isOptional

    /// A slot which has a default interface implementation.
    /// A combination of this flag and the lack of IsOptional means the slot may have been reabstracted.
    member this.HasDefaultInterfaceImplementation =
        match this with
        | DefaultInterfaceImplementationSlot _ -> true
        | _ -> false
            
    /// A slot that *might* have ambiguity due to multiple inheritance; happens with default interface implementations.
    member this.PossiblyNoMostSpecificImplementation =
        match this with
        | DefaultInterfaceImplementationSlot(_, _, possiblyNoMostSpecific) -> possiblyNoMostSpecific
        | _ -> false

    /// Gets the method info.
    member this.MethodInfo =
        match this with
        | RequiredSlot(methInfo, _)
        | DefaultInterfaceImplementationSlot(methInfo, _, _) -> methInfo

type SlotImplSet = SlotImplSet of RequiredSlot list * NameMultiMap<RequiredSlot> * OverrideInfo list * PropInfo list

exception TypeIsImplicitlyAbstract of range
exception OverrideDoesntOverride of DisplayEnv * OverrideInfo * MethInfo option * TcGlobals * Import.ImportMap * range

module DispatchSlotChecking =

    /// Print the signature of an override to a buffer as part of an error message
    let PrintOverrideToBuffer denv os (Override(_, _, id, (mtps, memberToParentInst), argTys, retTy, _, _)) = 
       let denv = { denv with showTyparBinding = true }
       let retTy = (retTy  |> GetFSharpViewOfReturnType denv.g)
       let argInfos = 
           match argTys with 
           | [] -> [[(denv.g.unit_ty, ValReprInfo.unnamedTopArg1)]]
           | _ -> argTys |> List.mapSquared (fun ty -> (ty, ValReprInfo.unnamedTopArg1)) 
       LayoutRender.bufferL os (NicePrint.prettyLayoutOfMemberSig denv (memberToParentInst, id.idText, mtps, argInfos, retTy))

    /// Print the signature of a MethInfo to a buffer as part of an error message
    let PrintMethInfoSigToBuffer g amap m denv os minfo =
        let denv = { denv with showTyparBinding = true }
        let (CompiledSig(argTys, retTy, fmtps, ttpinst)) = CompiledSigOfMeth g amap m minfo
        let retTy = (retTy  |> GetFSharpViewOfReturnType g)
        let argInfos = argTys |> List.mapSquared (fun ty -> (ty, ValReprInfo.unnamedTopArg1))
        let nm = minfo.LogicalName
        LayoutRender.bufferL os (NicePrint.prettyLayoutOfMemberSig denv (ttpinst, nm, fmtps, argInfos, retTy))

    /// Format the signature of an override as a string as part of an error message
    let FormatOverride denv d = bufs (fun buf -> PrintOverrideToBuffer denv buf d)

    /// Format the signature of a MethInfo as a string as part of an error message
    let FormatMethInfoSig g amap m denv d = bufs (fun buf -> PrintMethInfoSigToBuffer g amap m denv buf d)

    /// Get the override info for an existing (inherited) method being used to implement a dispatch slot.
    let GetInheritedMemberOverrideInfo g amap m parentType (minfo: MethInfo) = 
        let nm = minfo.LogicalName
        let (CompiledSig (argTys, retTy, fmtps, ttpinst)) = CompiledSigOfMeth g amap m minfo

        let isFakeEventProperty = minfo.IsFSharpEventPropertyMethod
        Override(parentType, minfo.ApparentEnclosingTyconRef, mkSynId m nm, (fmtps, ttpinst), argTys, retTy, isFakeEventProperty, false)

    /// Get the override info for a value being used to implement a dispatch slot.
    let GetTypeMemberOverrideInfo g reqdTy (overrideBy: ValRef) = 
        let _, _, argInfos, retTy, _ = GetTypeOfMemberInMemberForm g overrideBy
        let nm = overrideBy.LogicalName

        let argTys = argInfos |> List.mapSquared fst
        
        let memberMethodTypars, memberToParentInst, argTys, retTy = 
            match PartitionValRefTypars g overrideBy with
            | Some(_, _, memberMethodTypars, memberToParentInst, _tinst) -> 
                let argTys = argTys |> List.mapSquared (instType memberToParentInst) 
                let retTy = retTy |> Option.map (instType memberToParentInst) 
                memberMethodTypars, memberToParentInst, argTys, retTy
            | None -> 
                error(Error(FSComp.SR.typrelMethodIsOverconstrained(), overrideBy.Range))
        let implKind = 
            if ValRefIsExplicitImpl g overrideBy then 
                
                let belongsToReqdTy = 
                    match overrideBy.MemberInfo.Value.ImplementedSlotSigs with
                    | [] -> false
                    | ss :: _ -> isInterfaceTy g ss.ImplementedType && typeEquiv g reqdTy ss.ImplementedType
                if belongsToReqdTy then 
                    CanImplementAnyInterfaceSlot
                else
                    CanImplementNoSlots
            else if overrideBy.IsDispatchSlotMember then 
                CanImplementNoSlots
                // abstract slots can only implement interface slots
                //CanImplementAnyInterfaceSlot  <<----- Change to this to enable implicit interface implementation
            
            else 
                CanImplementAnyClassHierarchySlot
                //CanImplementAnySlot  <<----- Change to this to enable implicit interface implementation

        let isFakeEventProperty = overrideBy.IsFSharpEventProperty(g)
        Override(implKind, overrideBy.MemberApparentEntity, mkSynId overrideBy.Range nm, (memberMethodTypars, memberToParentInst), argTys, retTy, isFakeEventProperty, overrideBy.IsCompilerGenerated)

    /// Get the override information for an object expression method being used to implement dispatch slots
    let GetObjectExprOverrideInfo g amap (implty, id: Ident, memberFlags, ty, arityInfo, bindingAttribs, rhsExpr) = 
        // Dissect the type. The '0' indicates there are no enclosing generic class type parameters relevant here.
        let tps, _, argInfos, retTy, _ = GetMemberTypeInMemberForm g memberFlags arityInfo 0 ty id.idRange
        let argTys = argInfos |> List.mapSquared fst
        // Dissect the implementation
        let _, ctorThisValOpt, baseValOpt, vsl, rhsExpr, _ = destTopLambda g amap arityInfo (rhsExpr, ty)
        assert ctorThisValOpt.IsNone

        // Drop 'this'
        match vsl with 
        | [thisv] :: vs -> 
            // Check for empty variable list from a () arg
            let vs = if vs.Length = 1 && argInfos.IsEmpty then [] else vs
            let implKind = 
                if isInterfaceTy g implty then 
                    CanImplementAnyInterfaceSlot 
                else 
                    CanImplementAnyClassHierarchySlot
                    //CanImplementAnySlot  <<----- Change to this to enable implicit interface implementation
            let isFakeEventProperty = CompileAsEvent g bindingAttribs
            let overrideByInfo = Override(implKind, tcrefOfAppTy g implty, id, (tps, []), argTys, retTy, isFakeEventProperty, false)
            overrideByInfo, (baseValOpt, thisv, vs, bindingAttribs, rhsExpr)
        | _ -> 
            error(InternalError("Unexpected shape for object expression override", id.idRange))
          
    /// Check if an override matches a dispatch slot by name
    let IsNameMatch (dispatchSlot: MethInfo) (overrideBy: OverrideInfo) = 
        (overrideBy.LogicalName = dispatchSlot.LogicalName)
          
    /// Check if an override matches a dispatch slot by name
    let IsImplMatch g (dispatchSlot: MethInfo) (overrideBy: OverrideInfo) = 
        // If the override is listed as only relevant to one type, and we're matching it against an abstract slot of an interface type,
        // then check that interface type is the right type.
        match overrideBy.CanImplement with 
        | CanImplementNoSlots -> false
        | CanImplementAnySlot -> true 
        | CanImplementAnyClassHierarchySlot -> not (isInterfaceTy g dispatchSlot.ApparentEnclosingType)
        | CanImplementAnyInterfaceSlot -> isInterfaceTy g dispatchSlot.ApparentEnclosingType

    /// Check if the kinds of type parameters match between a dispatch slot and an override.
    let IsTyparKindMatch (CompiledSig(_, _, fvmtps, _)) (Override(_, _, _, (mtps, _), _, _, _, _)) = 
        List.lengthsEqAndForall2 (fun (tp1: Typar) (tp2: Typar) -> tp1.Kind = tp2.Kind) mtps fvmtps
        
    /// Check if an override is a partial match for the requirements for a dispatch slot except for the name.
    let IsSigPartialMatch g (dispatchSlot: MethInfo) compiledSig (Override(_, _, _, (mtps, _), argTys, _retTy, _, _) as overrideBy) =
        let (CompiledSig (vargtys, _, fvmtps, _)) = compiledSig
        mtps.Length = fvmtps.Length &&
        IsTyparKindMatch compiledSig overrideBy && 
        argTys.Length = vargtys.Length &&
        IsImplMatch g dispatchSlot overrideBy
        
    /// Check if an override is a partial match for the requirements for a dispatch slot.
    let IsPartialMatch g dispatchSlot compiledSig overrideBy = 
        IsNameMatch dispatchSlot overrideBy &&
        IsSigPartialMatch g dispatchSlot compiledSig overrideBy
          
    /// Compute the reverse of a type parameter renaming.
    let ReverseTyparRenaming g tinst = 
        tinst |> List.map (fun (tp, ty) -> (destTyparTy g ty, mkTyparTy tp))

    /// Compose two instantiations of type parameters.
    let ComposeTyparInsts inst1 inst2 = 
        inst1 |> List.map (map2Of2 (instType inst2)) 
     
    /// Check if an override exactly matches the requirements for a dispatch slot except for the name.
    let IsSigExactMatch g amap m dispatchSlot (Override(_, _, _, (mtps, mtpinst), argTys, retTy, _, _) as overrideBy) =
        let compiledSig = CompiledSigOfMeth g amap m dispatchSlot
        IsSigPartialMatch g dispatchSlot compiledSig overrideBy &&
        let (CompiledSig (vargtys, vrty, fvmtps, ttpinst)) = compiledSig

        // Compare the types. CompiledSigOfMeth, GetObjectExprOverrideInfo and GetTypeMemberOverrideInfo have already 
        // applied all relevant substitutions except the renamings from fvtmps <-> mtps 

        let aenv = TypeEquivEnv.FromEquivTypars fvmtps mtps 

        List.forall2 (List.lengthsEqAndForall2 (typeAEquiv g aenv)) vargtys argTys &&
        returnTypesAEquiv g aenv vrty retTy &&
        
        // Comparing the method typars and their constraints is much trickier since the substitutions have not been applied 
        // to the constraints of these babies. This is partly because constraints are directly attached to typars so it's 
        // difficult to apply substitutions to them unless we separate them off at some point, which we don't as yet.        
        //
        // Given   C<ctps>
        //         D<dtps>
        //         dispatchSlot :   C<ctys[dtps]>.M<fvmtps[ctps]>(...)
        //         overrideBy:  parent: D<dtys[dtps]>  value: !<ttps> <mtps[ttps]>(...) 
        //         
        //     where X[dtps] indicates that X may involve free type variables dtps
        //     
        //     we have 
        //         ttpinst maps  ctps --> ctys[dtps] 
        //         mtpinst maps  ttps --> dtps
        //       
        //     compare fvtmps[ctps] and mtps[ttps] by 
        //        fvtmps[ctps]  @ ttpinst     -- gives fvtmps[dtps]
        //        fvtmps[dtps] @ rev(mtpinst) -- gives fvtmps[ttps]
        //        
        //     Now fvtmps[ttps] and mtpinst[ttps] are comparable, i.e. have constraints w.r.t. the same set of type variables 
        //         
        // i.e.  Compose the substitutions ttpinst and rev(mtpinst) 
        
        let ttpinst = 
            // check we can reverse - in some error recovery situations we can't 
            if mtpinst |> List.exists (snd >> isTyparTy g >> not) then ttpinst 
            else ComposeTyparInsts ttpinst (ReverseTyparRenaming g mtpinst)

        // Compare under the composed substitutions 
        let aenv = TypeEquivEnv.FromTyparInst ttpinst 
        
        typarsAEquiv g aenv fvmtps mtps

    /// Check if an override exactly matches the requirements for a dispatch slot.
    let IsExactMatch g amap m dispatchSlot overrideBy =
        IsNameMatch dispatchSlot overrideBy &&
        IsSigExactMatch g amap m dispatchSlot overrideBy

    /// Check if an override implements a dispatch slot 
    let OverrideImplementsDispatchSlot g amap m dispatchSlot availPriorOverride =
        IsExactMatch g amap m dispatchSlot availPriorOverride &&
        // The override has to actually be in some subtype of the dispatch slot
        ExistsHeadTypeInEntireHierarchy g amap m (generalizedTyconRef availPriorOverride.BoundingTyconRef) dispatchSlot.DeclaringTyconRef

    /// Check if a dispatch slot is already implemented
    let DispatchSlotIsAlreadyImplemented g amap m availPriorOverridesKeyed (dispatchSlot: MethInfo) =
        availPriorOverridesKeyed 
            |> NameMultiMap.find  dispatchSlot.LogicalName  
            |> List.exists (OverrideImplementsDispatchSlot g amap m dispatchSlot)

    /// Check all dispatch slots are implemented by some override.
    let CheckDispatchSlotsAreImplemented (denv, infoReader: InfoReader, m,
                                          nenv, sink: TcResultsSink,
                                          isOverallTyAbstract,
                                          reqdTy,
                                          dispatchSlots: RequiredSlot list,
                                          availPriorOverrides: OverrideInfo list,
                                          overrides: OverrideInfo list) = 
        let g = infoReader.g
        let amap = infoReader.amap

        let isReqdTyInterface = isInterfaceTy g reqdTy 
        let showMissingMethodsAndRaiseErrors = (isReqdTyInterface || not isOverallTyAbstract)
        
        let mutable res = true
        let fail exn =
            res <- false
            if showMissingMethodsAndRaiseErrors then
                errorR exn

        // Index the availPriorOverrides and overrides by name
        let availPriorOverridesKeyed = availPriorOverrides |> NameMultiMap.initBy (fun ov -> ov.LogicalName)
        let overridesKeyed = overrides |> NameMultiMap.initBy (fun ov -> ov.LogicalName)
        
        // we accumulate those to compose a more complete error message, see noimpl() bellow.
        let missingOverloadImplementation = ResizeArray()

        for reqdSlot in dispatchSlots do
            let dispatchSlot = reqdSlot.MethodInfo

            // Always try to raise a target runtime error if we have a DIM.
            if reqdSlot.HasDefaultInterfaceImplementation then
                checkLanguageFeatureRuntimeErrorRecover infoReader LanguageFeature.DefaultInterfaceMemberConsumption m

            let maybeResolvedSlot =
                NameMultiMap.find dispatchSlot.LogicalName overridesKeyed 
                |> List.filter (OverrideImplementsDispatchSlot g amap m dispatchSlot)

            match maybeResolvedSlot with
            | [ovd] -> 
                if not ovd.IsCompilerGenerated then 
                    let item = Item.MethodGroup(ovd.LogicalName, [dispatchSlot],None)
                    CallNameResolutionSink sink (ovd.Range, nenv, item, dispatchSlot.FormalMethodTyparInst, ItemOccurence.Implemented, AccessorDomain.AccessibleFromSomewhere)
            | [] -> 
                if not reqdSlot.IsOptional &&
                   // Check that no available prior override implements this dispatch slot
                   not (DispatchSlotIsAlreadyImplemented g amap m availPriorOverridesKeyed dispatchSlot) 
                then 
                    // Always try to raise a language version error if we have a DIM that is not explicitly implemented.
                    if reqdSlot.HasDefaultInterfaceImplementation then
                        checkLanguageFeatureErrorRecover g.langVersion LanguageFeature.DefaultInterfaceMemberConsumption m

                    if reqdSlot.PossiblyNoMostSpecificImplementation then
                        errorR(Error(FSComp.SR.typrelInterfaceMemberNoMostSpecificImplementation(NicePrint.stringOfMethInfo infoReader m denv dispatchSlot), m))

                    // error reporting path
                    let compiledSig = CompiledSigOfMeth g amap m dispatchSlot
                    
                    let noimpl() = 
                        missingOverloadImplementation.Add((isReqdTyInterface, lazy NicePrint.stringOfMethInfo infoReader m denv dispatchSlot))
                    
                    match overrides |> List.filter (IsPartialMatch g dispatchSlot compiledSig) with 
                    | [] -> 
                        let possibleOverrides =
                            overrides
                            |> List.filter (fun overrideBy -> IsNameMatch dispatchSlot overrideBy && IsImplMatch g dispatchSlot overrideBy)

                        match possibleOverrides with 
                        | [] -> 
                            noimpl()
                        | [ Override(_, _, _, (mtps, _), argTys, _, _, _) as overrideBy ] ->
                            let moreThanOnePossibleDispatchSlot =
                                dispatchSlots
                                |> List.filter (fun reqdSlot-> IsNameMatch reqdSlot.MethodInfo overrideBy && IsImplMatch g reqdSlot.MethodInfo overrideBy)
                                |> isNilOrSingleton
                                |> not
                            
                            let (CompiledSig (vargtys, _, fvmtps, _)) = compiledSig

                            if moreThanOnePossibleDispatchSlot then
                                noimpl()

                            elif argTys.Length <> vargtys.Length then
                                fail(Error(FSComp.SR.typrelMemberDoesNotHaveCorrectNumberOfArguments(FormatOverride denv overrideBy, FormatMethInfoSig g amap m denv dispatchSlot), overrideBy.Range))
                            elif mtps.Length <> fvmtps.Length then
                                fail(Error(FSComp.SR.typrelMemberDoesNotHaveCorrectNumberOfTypeParameters(FormatOverride denv overrideBy, FormatMethInfoSig g amap m denv dispatchSlot), overrideBy.Range))
                            elif not (IsTyparKindMatch compiledSig overrideBy) then
                                fail(Error(FSComp.SR.typrelMemberDoesNotHaveCorrectKindsOfGenericParameters(FormatOverride denv overrideBy, FormatMethInfoSig g amap m denv dispatchSlot), overrideBy.Range))
                            else 
                                fail(Error(FSComp.SR.typrelMemberCannotImplement(FormatOverride denv overrideBy, NicePrint.stringOfMethInfo infoReader m denv dispatchSlot, FormatMethInfoSig g amap m denv dispatchSlot), overrideBy.Range))
                        | overrideBy :: _ -> 
                            errorR(Error(FSComp.SR.typrelOverloadNotFound(FormatMethInfoSig g amap m denv dispatchSlot, FormatMethInfoSig g amap m denv dispatchSlot), overrideBy.Range))

                    | [ overrideBy ] -> 
                        if dispatchSlots |> List.exists (fun reqdSlot -> OverrideImplementsDispatchSlot g amap m reqdSlot.MethodInfo overrideBy) then
                            noimpl()
                        else
                            // Error will be reported below in CheckOverridesAreAllUsedOnce 
                            ()
                    | _ -> 
                        fail(Error(FSComp.SR.typrelOverrideWasAmbiguous(FormatMethInfoSig g amap m denv dispatchSlot), m))
            | _ -> fail(Error(FSComp.SR.typrelMoreThenOneOverride(FormatMethInfoSig g amap m denv dispatchSlot), m))
        
        if missingOverloadImplementation.Count > 0 then
            // compose message listing missing override implementation
            let maxDisplayedOverrides = 10
            let shouldTruncate = missingOverloadImplementation.Count > maxDisplayedOverrides
            let messageWithInterfaceSuggestion = 
                // check any of the missing overrides has isReqdTyInterface flag set
                // in which case we use the message "with suggestion"
                missingOverloadImplementation 
                |> Seq.map fst 
                |> Seq.filter id 
                |> Seq.isEmpty
                |> not

            if missingOverloadImplementation.Count = 1 then
                // only one missing override, we have specific message for that
                let signature = (snd missingOverloadImplementation.[0]).Value
                if messageWithInterfaceSuggestion then 
                    fail(Error(FSComp.SR.typrelNoImplementationGivenWithSuggestion(signature), m))
                else
                    fail(Error(FSComp.SR.typrelNoImplementationGiven(signature), m))
            else
                let signatures = 
                    (missingOverloadImplementation 
                    |> Seq.truncate maxDisplayedOverrides 
                    |> Seq.map snd 
                    |> Seq.map (fun signature -> System.Environment.NewLine + "\t'" + signature.Value + "'")
                    |> String.concat "") + System.Environment.NewLine 
                
                // we have specific message if the list is truncated
                let messageFunction =
                    match shouldTruncate, messageWithInterfaceSuggestion with
                    | false, true  -> FSComp.SR.typrelNoImplementationGivenSeveralWithSuggestion
                    | false, false -> FSComp.SR.typrelNoImplementationGivenSeveral
                    | true , true  -> FSComp.SR.typrelNoImplementationGivenSeveralTruncatedWithSuggestion
                    | true , false -> FSComp.SR.typrelNoImplementationGivenSeveralTruncated
                fail(Error(messageFunction(signatures), m))

        res

    /// This is to find override methods that are at the most specific in the hierarchy of interface types.
    let GetMostSpecificOverrideInterfaceMethodSets (infoReader: InfoReader) allReqdTys =
        let g = infoReader.g
        let amap = infoReader.amap

        let multipleSets =
            allReqdTys
            // Widdle down to the most specific interfaces.
            |> GetMostSpecificItemsByType g amap (fun (ty, m) ->
                if isInterfaceTy g ty then
                    Some(ty, m)
                else
                    None)

            // Get the most specific method overrides for each interface type.
            |> List.choose (fun (ty, m) -> 
                let mostSpecificOverrides = GetIntrinisicMostSpecificOverrideMethInfoSetsOfType infoReader m ty
                if mostSpecificOverrides.IsEmpty then None
                else Some mostSpecificOverrides)

        match multipleSets with
        | [] -> NameMultiMap.Empty
        | [set] -> set
        | _ ->
            multipleSets
            // Merge method sets together.
            |> List.reduce (fun final minfoSets ->
                Map.fold (fun acc key minfos -> 
                    match acc.TryGetValue key with
                    | true, minfos2 -> Map.add key (minfos @ minfos2) acc
                    | _ -> Map.add key minfos acc) final minfoSets)

            // Filter for most specifics when the sets have merged together.
            |> FilterMostSpecificMethInfoSets g amap range0

    /// Finds the override interface methods from the most specific overrides by the given method.
    let GetMostSpecificOverrideInterfaceMethodsByMethod g amap m (mostSpecificOverrides: NameMultiMap<TType * MethInfo>) (minfo: MethInfo) =
        let overrideBy = GetInheritedMemberOverrideInfo g amap m OverrideCanImplement.CanImplementAnyInterfaceSlot minfo
        let minfoTy = generalizedTyconRef minfo.ApparentEnclosingTyconRef
        NameMultiMap.find minfo.LogicalName mostSpecificOverrides
        |> List.filter (fun (overridenTy, minfo2) -> 
            typeEquiv g overridenTy minfoTy && 
            IsSigExactMatch g amap m minfo2 overrideBy)

    /// Get a collection of slots for the given interface type.
    let GetInterfaceDispatchSlots (infoReader: InfoReader) ad m availImpliedInterfaces mostSpecificOverrides interfaceTy =
        let g = infoReader.g
        let amap = infoReader.amap

        if isInterfaceTy g interfaceTy then
            // Check if the interface has an inherited implementation
            // If so, you do not have to implement all the methods - each
            // specific method is "optionally" implemented.
            let isInterfaceOptional = ListSet.contains (typeEquiv g) interfaceTy availImpliedInterfaces
            [ for minfo in GetImmediateIntrinsicMethInfosOfType (None, ad) g amap m interfaceTy do
                if minfo.IsNewSlot then
                      // If the interface itself is considered optional, then we are finished and do not need anymore context.
                      //     Even if the method is actually not abstract.
                      if isInterfaceOptional then
                          yield RequiredSlot (minfo, true)

                      // F# defined interface methods have no notion of optional/abstract or DIMs.
                      elif not minfo.IsILMethod then
                          yield RequiredSlot (minfo, false)

                      // IL methods might have default implementations.
                      else
                          let isMethodOptional (minfo: MethInfo) =
                            // A DIM is considered *not* 'optional' if it is not language supported.
                            g.langVersion.SupportsFeature LanguageFeature.DefaultInterfaceMemberConsumption &&
                            not minfo.IsAbstract

                          match GetMostSpecificOverrideInterfaceMethodsByMethod g amap m mostSpecificOverrides minfo with
                          // No override.
                          | [] ->
                            if minfo.IsAbstract then
                                // Regular interface methods are never optional.
                                yield RequiredSlot (minfo, false)
                            else
                                yield DefaultInterfaceImplementationSlot (minfo, isMethodOptional minfo, false)

                          // One override, one default implementation.
                          | [ (_, minfo2) ] ->
                            yield DefaultInterfaceImplementationSlot (minfo, isMethodOptional minfo2, false)

                          // We found multiple override methods, means we might have ambiguity.
                          | _ ->
                            // If DIMs are not language supported, then do not consider a slot to have a specific implementation.
                            let possiblyNoMostSpecific =
                                g.langVersion.SupportsFeature LanguageFeature.DefaultInterfaceMemberConsumption

                            yield DefaultInterfaceImplementationSlot (minfo, false, possiblyNoMostSpecific) ]
            else
                []

    /// Get a collection of slots for the given class type.
    let GetClassDispatchSlots (infoReader: InfoReader) ad m reqdTy =
        [ if not (isInterfaceTy infoReader.g reqdTy) then
            // In the normal case, the requirements for a class are precisely all the abstract slots up the whole hierarchy.
            // So here we get and yield all of those.
            for minfo in reqdTy |> GetIntrinsicMethInfosOfType infoReader None ad AllowMultiIntfInstantiations.Yes IgnoreOverrides m do
                if minfo.IsDispatchSlot then
                    yield RequiredSlot(minfo, not minfo.IsAbstract) ]

    /// Get a collection of slots for the given type and implied types.
    let GetDispatchSlotSet (infoReader: InfoReader) ad m availImpliedInterfaces mostSpecificOverrides reqdTy impliedTys =
        let g = infoReader.g

        if isInterfaceTy g reqdTy then 
            [ for impliedTy in impliedTys do
                yield (impliedTy, GetInterfaceDispatchSlots infoReader ad m availImpliedInterfaces mostSpecificOverrides impliedTy) ]
        else                  
            [ (reqdTy, GetClassDispatchSlots infoReader ad m reqdTy) ]

    /// Check all implementations implement some dispatch slot.
    let CheckOverridesAreAllUsedOnce(denv, g, infoReader: InfoReader, isObjExpr, reqdTy,
                                     dispatchSlotsKeyed: NameMultiMap<RequiredSlot>,
                                     availPriorOverrides: OverrideInfo list,
                                     overrides: OverrideInfo list) = 
        let amap = infoReader.amap

        let availPriorOverridesKeyed = availPriorOverrides |> NameMultiMap.initBy (fun ov -> ov.LogicalName)
        for overrideBy in overrides do 
          if not overrideBy.IsFakeEventProperty then
            let m = overrideBy.Range
            let relevantVirts = NameMultiMap.find overrideBy.LogicalName dispatchSlotsKeyed
            let relevantVirts = relevantVirts |> List.map (fun reqdSlot -> reqdSlot.MethodInfo)

            match relevantVirts |> List.filter (fun dispatchSlot -> OverrideImplementsDispatchSlot g amap m dispatchSlot overrideBy) with
            | [] -> 
                // This is all error reporting
                match relevantVirts |> List.filter (fun dispatchSlot -> IsPartialMatch g dispatchSlot (CompiledSigOfMeth g amap m dispatchSlot) overrideBy) with 
                | [dispatchSlot] -> 
                    errorR(OverrideDoesntOverride(denv, overrideBy, Some dispatchSlot, g, amap, m))
                | _ -> 
                    match relevantVirts |> List.filter (fun dispatchSlot -> IsNameMatch dispatchSlot overrideBy) with 
                    | [] -> errorR(OverrideDoesntOverride(denv, overrideBy, None, g, amap, m))
                    | [dispatchSlot] -> 
                        errorR(OverrideDoesntOverride(denv, overrideBy, Some dispatchSlot, g, amap, m))
                    | possibleDispatchSlots -> 
                       let details =
                            possibleDispatchSlots
                            |> List.map (fun dispatchSlot -> FormatMethInfoSig g amap m denv dispatchSlot)
                            |> Seq.map (sprintf "%s   %s" System.Environment.NewLine)
                            |> String.concat ""

                       errorR(Error(FSComp.SR.typrelMemberHasMultiplePossibleDispatchSlots(FormatOverride denv overrideBy, details), overrideBy.Range))


            | [dispatchSlot] -> 
                if dispatchSlot.IsFinal && (isObjExpr || not (typeEquiv g reqdTy dispatchSlot.ApparentEnclosingType)) then 
                    errorR(Error(FSComp.SR.typrelMethodIsSealed(NicePrint.stringOfMethInfo infoReader m denv dispatchSlot), m))
            | dispatchSlots -> 
                match dispatchSlots |> List.filter (fun dispatchSlot -> 
                              isInterfaceTy g dispatchSlot.ApparentEnclosingType || 
                              not (DispatchSlotIsAlreadyImplemented g amap m availPriorOverridesKeyed dispatchSlot)) with
                | h1 :: h2 :: _ -> 
                    errorR(Error(FSComp.SR.typrelOverrideImplementsMoreThenOneSlot((FormatOverride denv overrideBy), (NicePrint.stringOfMethInfo infoReader m denv h1), (NicePrint.stringOfMethInfo infoReader m denv h2)), m))
                | _ -> 
                    // dispatch slots are ordered from the derived classes to base
                    // so we can check the topmost dispatch slot if it is final
                    match dispatchSlots with
                    | meth :: _ when meth.IsFinal -> errorR(Error(FSComp.SR.tcCannotOverrideSealedMethod((sprintf "%s::%s" (meth.ApparentEnclosingType.ToString()) (meth.LogicalName))), m))
                    | _ -> ()



    /// Get the slots of a type that can or must be implemented. This depends
    /// partly on the full set of interface types that are being implemented
    /// simultaneously, e.g.
    ///    { new C with  interface I2 = ... interface I3 = ... }
    /// allReqdTys = {C;I2;I3}
    ///
    /// allReqdTys can include one class/record/union type. 
    let GetSlotImplSets (infoReader: InfoReader) denv ad isObjExpr allReqdTys = 

        let g = infoReader.g
        let amap = infoReader.amap
        
        let availImpliedInterfaces : TType list = 
            [ for (reqdTy, m) in allReqdTys do
                if not (isInterfaceTy g reqdTy) then 
                    let baseTyOpt = if isObjExpr then Some reqdTy else GetSuperTypeOfType g amap m reqdTy 
                    match baseTyOpt with 
                    | None -> ()
                    | Some baseTy -> yield! AllInterfacesOfType g amap m AllowMultiIntfInstantiations.Yes baseTy  ]
                    
        // For each implemented type, get a list containing the transitive closure of
        // interface types implied by the type. This includes the implemented type itself if the implemented type
        // is an interface type.
        let intfSets = 
            allReqdTys |> List.mapi (fun i (reqdTy, m) -> 
                let interfaces = AllInterfacesOfType g amap m AllowMultiIntfInstantiations.Yes reqdTy 
                let impliedTys = (if isInterfaceTy g reqdTy then interfaces else reqdTy :: interfaces)
                (i, reqdTy, impliedTys, m))

        // For each implemented type, reduce its list of implied interfaces by subtracting out those implied 
        // by another implemented interface type.
        //
        // REVIEW: Note complexity O(N^2)
        let reqdTyInfos = 
            intfSets |> List.map (fun (i, reqdTy, impliedTys, m) -> 
                let reduced = 
                    (impliedTys, intfSets) ||> List.fold (fun acc (j, jty, impliedTys2, m) -> 
                         if i <> j && TypeFeasiblySubsumesType 0 g amap m jty CanCoerce reqdTy 
                         then ListSet.subtract (TypesFeasiblyEquiv 0 g amap m) acc impliedTys2
                         else acc ) 
                (i, reqdTy, m, reduced))

        // Find the full set of most derived interfaces, used roots to search for default interface implementations of interface methods.
        let mostSpecificOverrides = GetMostSpecificOverrideInterfaceMethodSets infoReader allReqdTys

        // Get the SlotImplSet for each implemented type
        // This contains the list of required members and the list of available members
        [ for (i, reqdTy, reqdTyRange, impliedTys) in reqdTyInfos do

            // Check that, for each implemented type, at least one implemented type is implied. This is enough to capture
            // duplicates.
            if isInterfaceTy g reqdTy && isNil impliedTys then 
                errorR(Error(FSComp.SR.typrelDuplicateInterface(), reqdTyRange))

            // Build a set of the implied interface types, for quicker lookup, by nominal type
            let isImpliedInterfaceTable = 
                impliedTys 
                |> List.filter (isInterfaceTy g) 
                |> List.map (fun ty -> tcrefOfAppTy g ty, ()) 
                |> TyconRefMap.OfList 
            
            // Is a member an abstract slot of one of the implied interface types?
            let isImpliedInterfaceType ty =
                isAppTy g ty &&
                isImpliedInterfaceTable.ContainsKey (tcrefOfAppTy g ty) &&
                impliedTys |> List.exists (TypesFeasiblyEquiv 0 g amap reqdTyRange ty)

            let dispatchSlotSet = GetDispatchSlotSet infoReader ad reqdTyRange availImpliedInterfaces mostSpecificOverrides reqdTy impliedTys
                
            // Compute the methods that are available to implement abstract slots from the base class
            //
            // This is used in CheckDispatchSlotsAreImplemented when we think a dispatch slot may not
            // have been implemented. 
            let availPriorOverrides : OverrideInfo list = 
                if isInterfaceTy g reqdTy then 
                    []
                else 
                    let reqdTy = 
                        let baseTyOpt = if isObjExpr then Some reqdTy else GetSuperTypeOfType g amap reqdTyRange reqdTy 
                        match baseTyOpt with 
                        | None -> reqdTy
                        | Some baseTy -> baseTy 
                    [ // Get any class hierarchy methods or default interface methods on this type on this type 
                      //
                      // NOTE: What we have below is an over-approximation that will get too many methods 
                      // and not always correctly relate them to the slots they implement. For example,
                      // we may get an override from a base class and believe it implements a fresh, new abstract
                      // slot in a subclass. 
                      for minfos in infoReader.GetRawIntrinsicMethodSetsOfType(None, ad, AllowMultiIntfInstantiations.Yes, reqdTyRange, reqdTy) do
                        for minfo in minfos do
                          if not minfo.IsAbstract then 
                              yield GetInheritedMemberOverrideInfo g amap reqdTyRange CanImplementAnyClassHierarchySlot minfo   ]

            /// Check that no interface type is implied twice
            for (j, _, _, impliedTys2) in reqdTyInfos do
                if i > j then
                    for (ty, dispatchSlots) in dispatchSlotSet do
                        if impliedTys2 |> List.exists (TypesFeasiblyEquiv 0 g amap reqdTyRange ty) then
                            if  dispatchSlots 
                                |> List.exists (fun reqdSlot ->
                                    let minfo = reqdSlot.MethodInfo
                                    // If the slot is optional, then we do not need an explicit implementation.
                                    minfo.IsNewSlot && not reqdSlot.IsOptional) then
                                errorR(Error(FSComp.SR.typrelNeedExplicitImplementation(NicePrint.minimalStringOfType denv ty), reqdTyRange))
                     
            // We also collect up the properties. This is used for abstract slot inference when overriding properties
            let isRelevantRequiredProperty (x: PropInfo) = 
                (x.IsVirtualProperty && not (isInterfaceTy g reqdTy)) ||
                isImpliedInterfaceType x.ApparentEnclosingType
                
            let reqdProperties = 
                GetIntrinsicPropInfosOfType infoReader None ad AllowMultiIntfInstantiations.Yes IgnoreOverrides reqdTyRange reqdTy 
                |> List.filter isRelevantRequiredProperty
                
            let dispatchSlots = dispatchSlotSet |> List.map snd |> List.concat
            let dispatchSlotsKeyed = dispatchSlots |> NameMultiMap.initBy (fun reqdSlot -> reqdSlot.MethodInfo.LogicalName) 
            yield SlotImplSet(dispatchSlots, dispatchSlotsKeyed, availPriorOverrides, reqdProperties) ]


    /// Check that a type definition implements all its required interfaces after processing all declarations 
    /// within a file.
    let CheckImplementationRelationAtEndOfInferenceScope (infoReader : InfoReader, denv, nenv, sink, tycon: Tycon, isImplementation) =

        let g = infoReader.g
        let amap = infoReader.amap

        let tcaug = tycon.TypeContents        
        let interfaces = tycon.ImmediateInterfacesOfFSharpTycon |> List.map (fun (ity, _compgen, m) -> (ity, m))

        let overallTy = generalizedTyconRef (mkLocalTyconRef tycon)

        let allReqdTys = (overallTy, tycon.Range) :: interfaces 

        // Get all the members that are immediately part of this type
        // Include the auto-generated members
        let allImmediateMembers = tycon.MembersOfFSharpTyconSorted @ tycon.AllGeneratedValues

        // Get all the members we have to implement, organized by each type we explicitly implement
        let slotImplSets = GetSlotImplSets infoReader denv AccessibleFromSomewhere false allReqdTys

        let allImpls = List.zip allReqdTys slotImplSets

        // Find the methods relevant to implementing the abstract slots listed under the reqdType being checked.
        let allImmediateMembersThatMightImplementDispatchSlots = 
            allImmediateMembers |> List.filter (fun overrideBy -> 
                overrideBy.IsInstanceMember   &&  // exclude static
                overrideBy.IsVirtualMember &&  // exclude non virtual (e.g. keep override/default). [4469]
                not overrideBy.IsDispatchSlotMember)

        let mustOverrideSomething reqdTy (overrideBy: ValRef) =
           let memberInfo = overrideBy.MemberInfo.Value
           not (overrideBy.IsFSharpEventProperty(g)) &&
           memberInfo.MemberFlags.IsOverrideOrExplicitImpl && 
    
           match memberInfo.ImplementedSlotSigs with 
           | [] -> 
                // Are we looking at the implementation of the class hierarchy? If so include all the override members
                not (isInterfaceTy g reqdTy)
           | ss -> 
                 ss |> List.forall (fun ss -> 
                     let ty = ss.ImplementedType
                     if isInterfaceTy g ty then 
                         // Is this a method impl listed under the reqdTy?
                         typeEquiv g ty reqdTy
                     else
                         not (isInterfaceTy g reqdTy) )
        

        // We check all the abstracts related to the class hierarchy and then check each interface implementation
        for ((reqdTy, m), slotImplSet) in allImpls do
            let (SlotImplSet(dispatchSlots, dispatchSlotsKeyed, availPriorOverrides, _)) = slotImplSet
            try 

                // Now extract the information about each overriding method relevant to this SlotImplSet
                let allImmediateMembersThatMightImplementDispatchSlots = 
                    allImmediateMembersThatMightImplementDispatchSlots
                    |> List.map (fun overrideBy -> overrideBy, GetTypeMemberOverrideInfo g reqdTy overrideBy)
                
                // Now check the implementation
                // We don't give missing method errors for abstract classes 
                
                if isImplementation && not (isInterfaceTy g overallTy) then 
                    let overrides = allImmediateMembersThatMightImplementDispatchSlots |> List.map snd 
                    let allCorrect = CheckDispatchSlotsAreImplemented (denv, infoReader, m, nenv, sink, tcaug.tcaug_abstract, reqdTy, dispatchSlots, availPriorOverrides, overrides)
                    
                    // Tell the user to mark the thing abstract if it was missing implementations
                    if not allCorrect && not tcaug.tcaug_abstract && not (isInterfaceTy g reqdTy) then 
                        errorR(TypeIsImplicitlyAbstract(m))
                    
                    let overridesToCheck = 
                        allImmediateMembersThatMightImplementDispatchSlots 
                           |> List.filter (fst >> mustOverrideSomething reqdTy)
                           |> List.map snd

                    CheckOverridesAreAllUsedOnce (denv, g, infoReader, false, reqdTy, dispatchSlotsKeyed, availPriorOverrides, overridesToCheck)

            with e -> errorRecovery e m

        // Now record the full slotsigs of the abstract members implemented by each override.
        // This is used to generate IL MethodImpls in the code generator.
        allImmediateMembersThatMightImplementDispatchSlots |> List.iter (fun overrideBy -> 

            let isFakeEventProperty = overrideBy.IsFSharpEventProperty(g)
            let overriden = 
                if isFakeEventProperty then 
                    let slotsigs = overrideBy.MemberInfo.Value.ImplementedSlotSigs 
                    slotsigs |> List.map (ReparentSlotSigToUseMethodTypars g overrideBy.Range overrideBy)
                else
                    [ for ((reqdTy, m), (SlotImplSet(_dispatchSlots, dispatchSlotsKeyed, _, _))) in allImpls do
                          let overrideByInfo = GetTypeMemberOverrideInfo g reqdTy overrideBy
                          let overridenForThisSlotImplSet = 
                              [ for reqdSlot in NameMultiMap.find overrideByInfo.LogicalName dispatchSlotsKeyed do
                                    let dispatchSlot = reqdSlot.MethodInfo
                                    if OverrideImplementsDispatchSlot g amap m dispatchSlot overrideByInfo then 
                                        if tyconRefEq g overrideByInfo.BoundingTyconRef dispatchSlot.DeclaringTyconRef then 
                                             match dispatchSlot.ArbitraryValRef with 
                                             | Some virtMember -> 
                                                  if virtMember.MemberInfo.Value.IsImplemented then errorR(Error(FSComp.SR.tcDefaultImplementationAlreadyExists(), overrideByInfo.Range))
                                                  virtMember.MemberInfo.Value.IsImplemented <- true
                                             | None -> () // not an F# slot

                                        // Get the slotsig of the overridden method 
                                        let slotsig = dispatchSlot.GetSlotSig(amap, m, NameResolution.traitCtxtNone)

                                        // The slotsig from the overridden method is in terms of the type parameters on the parent type of the overriding method,
                                        // Modify map the slotsig so it is in terms of the type parameters for the overriding method 
                                        let slotsig = ReparentSlotSigToUseMethodTypars g m overrideBy slotsig
                     
                                        // Record the slotsig via mutation
                                        yield slotsig ]
                          //if mustOverrideSomething reqdTy overrideBy then 
                          //    assert nonNil overridenForThisSlotImplSet
                          yield! overridenForThisSlotImplSet ]
                
            overrideBy.MemberInfo.Value.ImplementedSlotSigs <- overriden)

/// "Type Completion" inference and a few other checks at the end of the inference scope
let FinalTypeDefinitionChecksAtEndOfInferenceScope (infoReader: InfoReader, nenv, sink, isImplementation, denv) (tycon: Tycon) =

    let g = infoReader.g
    let amap = infoReader.amap

    let tcaug = tycon.TypeContents
    tcaug.tcaug_closed <- true
  
    // Note you only have to explicitly implement 'System.IComparable' to customize structural comparison AND equality on F# types 
    if isImplementation &&
#if !NO_EXTENSIONTYPING
       not tycon.IsProvidedGeneratedTycon &&
#endif
       Option.isNone tycon.GeneratedCompareToValues &&
       tycon.HasInterface g g.mk_IComparable_ty && 
       not (tycon.HasOverride g "Equals" [g.obj_ty]) && 
       not tycon.IsFSharpInterfaceTycon
     then
        (* Warn when we're doing this for class types *)
        if AugmentWithHashCompare.TyconIsCandidateForAugmentationWithEquals g tycon then
            warning(Error(FSComp.SR.typrelTypeImplementsIComparableShouldOverrideObjectEquals(tycon.DisplayName), tycon.Range))
        else
            warning(Error(FSComp.SR.typrelTypeImplementsIComparableDefaultObjectEqualsProvided(tycon.DisplayName), tycon.Range))

    AugmentWithHashCompare.CheckAugmentationAttribs isImplementation g amap tycon
    // Check some conditions about generic comparison and hashing. We can only check this condition after we've done the augmentation 
    if isImplementation 
#if !NO_EXTENSIONTYPING
       && not tycon.IsProvidedGeneratedTycon  
#endif
       then
        let tcaug = tycon.TypeContents
        let m = tycon.Range
        let hasExplicitObjectGetHashCode = tycon.HasOverride g "GetHashCode" []
        let hasExplicitObjectEqualsOverride = tycon.HasOverride g "Equals" [g.obj_ty]

        if (Option.isSome tycon.GeneratedHashAndEqualsWithComparerValues) && 
           (hasExplicitObjectGetHashCode || hasExplicitObjectEqualsOverride) then 
            errorR(Error(FSComp.SR.typrelExplicitImplementationOfGetHashCodeOrEquals(tycon.DisplayName), m)) 

        if not hasExplicitObjectEqualsOverride && hasExplicitObjectGetHashCode then 
            warning(Error(FSComp.SR.typrelExplicitImplementationOfGetHashCode(tycon.DisplayName), m)) 

        if hasExplicitObjectEqualsOverride && not hasExplicitObjectGetHashCode then 
            warning(Error(FSComp.SR.typrelExplicitImplementationOfEquals(tycon.DisplayName), m)) 

        // remember these values to ensure we don't generate these methods during codegen 
        tcaug.SetHasObjectGetHashCode hasExplicitObjectGetHashCode

        if not tycon.IsHiddenReprTycon
           && not tycon.IsTypeAbbrev
           && not tycon.IsMeasureableReprTycon
           && not tycon.IsAsmReprTycon
           && not tycon.IsFSharpInterfaceTycon
           && not tycon.IsFSharpDelegateTycon then 

            DispatchSlotChecking.CheckImplementationRelationAtEndOfInferenceScope (infoReader, denv, nenv, sink, tycon, isImplementation) 
    
/// Get the methods relevant to determining if a uniquely-identified-override exists based on the syntactic information 
/// at the member signature prior to type inference. This is used to pre-assign type information if it does 
let GetAbstractMethInfosForSynMethodDecl(infoReader: InfoReader, ad, memberName: Ident, bindm, typToSearchForAbstractMembers, valSynData) =
    let minfos = 
        match typToSearchForAbstractMembers with 
        | _, Some(SlotImplSet(_, dispatchSlotsKeyed, _, _)) -> 
            NameMultiMap.find  memberName.idText dispatchSlotsKeyed |> List.map (fun reqdSlot -> reqdSlot.MethodInfo)
        | ty, None -> 
            GetIntrinsicMethInfosOfType infoReader (Some memberName.idText) ad AllowMultiIntfInstantiations.Yes IgnoreOverrides bindm ty
    let dispatchSlots = minfos |> List.filter (fun minfo -> minfo.IsDispatchSlot)
    let topValSynArities = SynInfo.AritiesOfArgs valSynData
    let topValSynArities = if List.isEmpty topValSynArities then topValSynArities else topValSynArities.Tail
    let dispatchSlotsArityMatch = dispatchSlots |> List.filter (fun minfo -> minfo.NumArgs = topValSynArities) 
    dispatchSlots, dispatchSlotsArityMatch 

/// Get the properties relevant to determining if a uniquely-identified-override exists based on the syntactic information 
/// at the member signature prior to type inference. This is used to pre-assign type information if it does 
let GetAbstractPropInfosForSynPropertyDecl(infoReader: InfoReader, ad, memberName: Ident, bindm, typToSearchForAbstractMembers, _k, _valSynData) = 
    let pinfos = 
        match typToSearchForAbstractMembers with 
        | _, Some(SlotImplSet(_, _, _, reqdProps)) -> 
            reqdProps |> List.filter (fun pinfo -> pinfo.PropertyName = memberName.idText) 
        | ty, None -> 
            GetIntrinsicPropInfosOfType infoReader (Some memberName.idText) ad AllowMultiIntfInstantiations.Yes IgnoreOverrides bindm ty
        
    let dispatchSlots = pinfos |> List.filter (fun pinfo -> pinfo.IsVirtualProperty)
    dispatchSlots

