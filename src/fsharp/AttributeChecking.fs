// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Logic associated with checking "ObsoleteAttribute" and other attributes 
/// on items from name resolution
module internal FSharp.Compiler.AttributeChecking

open System
open System.Collections.Generic
open Internal.Utilities.Library
open FSharp.Compiler.AbstractIL.IL 
open FSharp.Compiler 
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Infos
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeOps

#if !NO_EXTENSIONTYPING
open FSharp.Compiler.ExtensionTyping
open FSharp.Core.CompilerServices
#endif

exception ObsoleteWarning of string * range
exception ObsoleteError of string * range

let fail() = failwith "This custom attribute has an argument that can not yet be converted using this API"

let rec private evalILAttribElem e = 
    match e with 
    | ILAttribElem.String (Some x)  -> box x
    | ILAttribElem.String None      -> null
    | ILAttribElem.Bool x           -> box x
    | ILAttribElem.Char x           -> box x
    | ILAttribElem.SByte x          -> box x
    | ILAttribElem.Int16 x          -> box x
    | ILAttribElem.Int32 x          -> box x
    | ILAttribElem.Int64 x          -> box x
    | ILAttribElem.Byte x           -> box x
    | ILAttribElem.UInt16 x         -> box x
    | ILAttribElem.UInt32 x         -> box x
    | ILAttribElem.UInt64 x         -> box x
    | ILAttribElem.Single x         -> box x
    | ILAttribElem.Double x         -> box x
    | ILAttribElem.Null             -> null
    | ILAttribElem.Array (_, a)     -> box [| for i in a -> evalILAttribElem i |]
    // TODO: typeof<..> in attribute values
    | ILAttribElem.Type (Some _t)    -> fail() 
    | ILAttribElem.Type None        -> null
    | ILAttribElem.TypeRef (Some _t) -> fail()
    | ILAttribElem.TypeRef None     -> null

let rec private evalFSharpAttribArg g e = 
    match e with
    | Expr.Const (c, _, _) -> 
        match c with 
        | Const.Bool b -> box b
        | Const.SByte i  -> box i
        | Const.Int16 i  -> box  i
        | Const.Int32 i   -> box i
        | Const.Int64 i   -> box i  
        | Const.Byte i    -> box i
        | Const.UInt16 i  -> box i
        | Const.UInt32 i  -> box i
        | Const.UInt64 i  -> box i
        | Const.Single i   -> box i
        | Const.Double i -> box i
        | Const.Char i    -> box i
        | Const.Zero -> null
        | Const.String s ->  box s
        | _ -> fail()
    | Expr.Op (TOp.Array, _, a, _) -> box [| for i in a -> evalFSharpAttribArg g i |]
    | TypeOfExpr g ty -> box ty
    // TODO: | TypeDefOfExpr g ty
    | _ -> fail()

type AttribInfo = 
    | FSAttribInfo of TcGlobals * Attrib
    | ILAttribInfo of TcGlobals * Import.ImportMap * ILScopeRef * ILAttribute * range

    member x.Range = 
         match x with 
         | FSAttribInfo(_, attrib) -> attrib.Range
         | ILAttribInfo (_, _, _, _, m) -> m

    member x.TyconRef = 
         match x with 
         | FSAttribInfo(_g, Attrib(tcref, _, _, _, _, _, _)) -> tcref
         | ILAttribInfo (g, amap, scoref, a, m) -> 
             let ty = ImportILType scoref amap m [] a.Method.DeclaringType
             tcrefOfAppTy g ty

    member x.ConstructorArguments = 
         match x with 
         | FSAttribInfo(g, Attrib(_, _, unnamedArgs, _, _, _, _)) -> 
             unnamedArgs
             |> List.map (fun (AttribExpr(origExpr, evaluatedExpr)) -> 
                    let ty = tyOfExpr g origExpr
                    let obj = evalFSharpAttribArg g evaluatedExpr
                    ty, obj) 
         | ILAttribInfo (g, amap, scoref, cattr, m) -> 
              let parms, _args = decodeILAttribData g.ilg cattr 
              [ for (argty, argval) in Seq.zip cattr.Method.FormalArgTypes parms ->
                    let ty = ImportILType scoref amap m [] argty
                    let obj = evalILAttribElem argval
                    ty, obj ]

    member x.NamedArguments = 
         match x with 
         | FSAttribInfo(g, Attrib(_, _, _, namedArgs, _, _, _)) -> 
             namedArgs
             |> List.map (fun (AttribNamedArg(nm, _, isField, AttribExpr(origExpr, evaluatedExpr))) -> 
                    let ty = tyOfExpr g origExpr
                    let obj = evalFSharpAttribArg g evaluatedExpr
                    ty, nm, isField, obj) 
         | ILAttribInfo (g, amap, scoref, cattr, m) -> 
              let _parms, namedArgs = decodeILAttribData g.ilg cattr 
              [ for (nm, argty, isProp, argval) in namedArgs ->
                    let ty = ImportILType scoref amap m [] argty
                    let obj = evalILAttribElem argval
                    let isField = not isProp 
                    ty, nm, isField, obj ]


/// Check custom attributes. This is particularly messy because custom attributes come in in three different
/// formats.
let AttribInfosOfIL g amap scoref m (attribs: ILAttributes) = 
    attribs.AsList  |> List.map (fun a -> ILAttribInfo (g, amap, scoref, a, m))

let AttribInfosOfFS g attribs = 
    attribs |> List.map (fun a -> FSAttribInfo (g, a))

let GetAttribInfosOfEntity g amap m (tcref:TyconRef) = 
    match metadataOfTycon tcref.Deref with 
#if !NO_EXTENSIONTYPING
    // TODO: provided attributes
    | ProvidedTypeMetadata _info -> []
        //let provAttribs = info.ProvidedType.PApply((fun a -> (a :> IProvidedCustomAttributeProvider)), m)
        //match provAttribs.PUntaint((fun a -> a. .GetAttributeConstructorArgs(provAttribs.TypeProvider.PUntaintNoFailure(id), atref.FullName)), m) with
        //| Some args -> f3 args
        //| None -> None
#endif
    | ILTypeMetadata (TILObjectReprData(scoref, _, tdef)) -> 
        tdef.CustomAttrs |> AttribInfosOfIL g amap scoref m
    | FSharpOrArrayOrByrefOrTupleOrExnTypeMetadata -> 
        tcref.Attribs |> List.map (fun a -> FSAttribInfo (g, a))


let GetAttribInfosOfMethod amap m minfo = 
    match minfo with 
    | ILMeth (g, ilminfo, _) -> ilminfo.RawMetadata.CustomAttrs  |> AttribInfosOfIL g amap ilminfo.MetadataScope m
    | FSMeth (g, _, vref, _) -> vref.Attribs |> AttribInfosOfFS g 
    | DefaultStructCtor _ -> []
#if !NO_EXTENSIONTYPING
    // TODO: provided attributes
    | ProvidedMeth (_, _mi, _, _m) -> 
            []

#endif

let GetAttribInfosOfProp amap m pinfo = 
    match pinfo with 
    | ILProp ilpinfo -> ilpinfo.RawMetadata.CustomAttrs |> AttribInfosOfIL ilpinfo.TcGlobals amap ilpinfo.ILTypeInfo.ILScopeRef m
    | FSProp(g, _, Some vref, _) 
    | FSProp(g, _, _, Some vref) -> vref.Attribs |> AttribInfosOfFS g 
    | FSProp _ -> failwith "GetAttribInfosOfProp: unreachable"
#if !NO_EXTENSIONTYPING
    // TODO: provided attributes
    | ProvidedProp _ ->  []
#endif

let GetAttribInfosOfEvent amap m einfo = 
    match einfo with 
    | ILEvent ileinfo -> ileinfo.RawMetadata.CustomAttrs |> AttribInfosOfIL einfo.TcGlobals amap ileinfo.ILTypeInfo.ILScopeRef m
    | FSEvent(_, pi, _vref1, _vref2) -> GetAttribInfosOfProp amap m pi
#if !NO_EXTENSIONTYPING
    // TODO: provided attributes
    | ProvidedEvent _ -> []
#endif

/// Analyze three cases for attributes declared on methods: IL-declared attributes, F#-declared attributes and
/// provided attributes.
let BindMethInfoAttributes m minfo f1 f2 f3 = 
    ignore m; ignore f3
    match minfo with 
    | ILMeth (_, x, _) -> f1 x.RawMetadata.CustomAttrs 
    | FSMeth (_, _, vref, _) -> f2 vref.Attribs
    | DefaultStructCtor _ -> f2 []
#if !NO_EXTENSIONTYPING
    | ProvidedMeth (_, mi, _, _) -> f3 (mi.PApply((fun st -> (st :> IProvidedCustomAttributeProvider)), m))
#endif

/// Analyze three cases for attributes declared on methods: IL-declared attributes, F#-declared attributes and
/// provided attributes.
let TryBindMethInfoAttribute g (m: range) (AttribInfo(atref, _) as attribSpec) minfo f1 f2 f3 = 
#if NO_EXTENSIONTYPING
    // to prevent unused parameter warning
    ignore f3
#endif
    BindMethInfoAttributes m minfo 
        (fun ilAttribs -> TryDecodeILAttribute g atref ilAttribs |> Option.bind f1)
        (fun fsAttribs -> TryFindFSharpAttribute g attribSpec fsAttribs |> Option.bind f2)
#if !NO_EXTENSIONTYPING
        (fun provAttribs -> 
            match provAttribs.PUntaint((fun a -> a.GetAttributeConstructorArgs(provAttribs.TypeProvider.PUntaintNoFailure(id), atref.FullName)), m) with
            | Some args -> f3 args
            | None -> None)  
#else
        (fun _provAttribs -> None)
#endif

/// Try to find a specific attribute on a method, where the attribute accepts a string argument.
///
/// This is just used for the 'ConditionalAttribute' attribute
let TryFindMethInfoStringAttribute g (m: range) attribSpec minfo  =
    TryBindMethInfoAttribute g m attribSpec minfo 
                    (function ([ILAttribElem.String (Some msg) ], _) -> Some msg | _ -> None) 
                    (function (Attrib(_, _, [ AttribStringArg msg ], _, _, _, _)) -> Some msg | _ -> None)
                    (function ([ Some ((:? string as msg) : obj) ], _) -> Some msg | _ -> None)

/// Check if a method has a specific attribute.
let MethInfoHasAttribute g m attribSpec minfo  =
    TryBindMethInfoAttribute g m attribSpec minfo 
                    (fun _ -> Some ()) 
                    (fun _ -> Some ())
                    (fun _ -> Some ())
        |> Option.isSome


/// Check IL attributes for 'ObsoleteAttribute', returning errors and warnings as data
let private CheckILAttributes (g: TcGlobals) isByrefLikeTyconRef cattrs m = 
    let (AttribInfo(tref,_)) = g.attrib_SystemObsolete
    match TryDecodeILAttribute g tref cattrs with 
    | Some ([ILAttribElem.String (Some msg) ], _) when not isByrefLikeTyconRef -> 
            WarnD(ObsoleteWarning(msg, m))
    | Some ([ILAttribElem.String (Some msg); ILAttribElem.Bool isError ], _) when not isByrefLikeTyconRef  -> 
        if isError then 
            ErrorD (ObsoleteError(msg, m))
        else 
            WarnD (ObsoleteWarning(msg, m))
    | Some ([ILAttribElem.String None ], _) when not isByrefLikeTyconRef -> 
        WarnD(ObsoleteWarning("", m))
    | Some _ when not isByrefLikeTyconRef -> 
        WarnD(ObsoleteWarning("", m))
    | _ -> 
        CompleteD

let langVersionPrefix = "--langversion:preview"

/// Check F# attributes for 'ObsoleteAttribute', 'CompilerMessageAttribute' and 'ExperimentalAttribute',
/// returning errors and warnings as data
let CheckFSharpAttributes (g:TcGlobals) attribs m =
    let isExperimentalAttributeDisabled (s:string) =
        if g.compilingFslib then
            true
        else
            g.langVersion.IsPreviewEnabled && (s.IndexOf(langVersionPrefix, StringComparison.OrdinalIgnoreCase) >= 0)

    if isNil attribs then CompleteD
    else
        (match TryFindFSharpAttribute g g.attrib_SystemObsolete attribs with
        | Some(Attrib(_, _, [ AttribStringArg s ], _, _, _, _)) ->
            WarnD(ObsoleteWarning(s, m))
        | Some(Attrib(_, _, [ AttribStringArg s; AttribBoolArg(isError) ], _, _, _, _)) -> 
            if isError then 
                ErrorD (ObsoleteError(s, m))
            else 
                WarnD (ObsoleteWarning(s, m))
        | Some _ -> 
            WarnD(ObsoleteWarning("", m))
        | None -> 
            CompleteD
        ) ++ (fun () -> 

        match TryFindFSharpAttribute g g.attrib_CompilerMessageAttribute attribs with
        | Some(Attrib(_, _, [ AttribStringArg s ; AttribInt32Arg n ], namedArgs, _, _, _)) ->
            let msg = UserCompilerMessage(s, n, m)
            let isError = 
                match namedArgs with 
                | ExtractAttribNamedArg "IsError" (AttribBoolArg v) -> v 
                | _ -> false 
            // If we are using a compiler that supports nameof then error 3501 is always suppressed.
            // See attribute on FSharp.Core 'nameof'
            if n = 3501 then CompleteD
            elif isError && (not g.compilingFslib || n <> 1204) then ErrorD msg 
            else WarnD msg
        | _ -> 
            CompleteD
        ) ++ (fun () -> 

        match TryFindFSharpAttribute g g.attrib_ExperimentalAttribute attribs with
        | Some(Attrib(_, _, [ AttribStringArg(s) ], _, _, _, _)) ->
            if isExperimentalAttributeDisabled s then
                CompleteD
            else
                WarnD(Experimental(s, m))
        | Some _ ->
            WarnD(Experimental(FSComp.SR.experimentalConstruct (), m))
        | _ ->
            CompleteD
        ) ++ (fun () ->

        match TryFindFSharpAttribute g g.attrib_UnverifiableAttribute attribs with
        | Some _ -> 
            WarnD(PossibleUnverifiableCode(m))
        | _ ->  
            CompleteD
        )

#if !NO_EXTENSIONTYPING
/// Check a list of provided attributes for 'ObsoleteAttribute', returning errors and warnings as data
let private CheckProvidedAttributes (g: TcGlobals) m (provAttribs: Tainted<IProvidedCustomAttributeProvider>)  = 
    let (AttribInfo(tref, _)) = g.attrib_SystemObsolete
    match provAttribs.PUntaint((fun a -> a.GetAttributeConstructorArgs(provAttribs.TypeProvider.PUntaintNoFailure(id), tref.FullName)), m) with
    | Some ([ Some (:? string as msg) ], _) -> WarnD(ObsoleteWarning(msg, m))
    | Some ([ Some (:? string as msg); Some (:?bool as isError) ], _)  ->
        if isError then 
            ErrorD (ObsoleteError(msg, m))
        else 
            WarnD (ObsoleteWarning(msg, m))
    | Some ([ None ], _) -> 
        WarnD(ObsoleteWarning("", m))
    | Some _ -> 
        WarnD(ObsoleteWarning("", m))
    | None -> 
        CompleteD
#endif

/// Indicate if a list of IL attributes contains 'ObsoleteAttribute'. Used to suppress the item in intellisense.
let CheckILAttributesForUnseen (g: TcGlobals) cattrs _m = 
    let (AttribInfo(tref, _)) = g.attrib_SystemObsolete
    Option.isSome (TryDecodeILAttribute g tref cattrs)

/// Checks the attributes for CompilerMessageAttribute, which has an IsHidden argument that allows
/// items to be suppressed from intellisense.
let CheckFSharpAttributesForHidden g attribs = 
    not (isNil attribs) &&         
    (match TryFindFSharpAttribute g g.attrib_CompilerMessageAttribute attribs with
        | Some(Attrib(_, _, [AttribStringArg _; AttribInt32Arg messageNumber],
                    ExtractAttribNamedArg "IsHidden" (AttribBoolArg v), _, _, _)) -> 
            // Message number 62 is for "ML Compatibility". Items labelled with this are visible in intellisense
            // when mlCompatibility is set.
            v && not (messageNumber = 62 && g.mlCompatibility)
        | _ -> false)
    || 
    (match TryFindFSharpAttribute g g.attrib_ComponentModelEditorBrowsableAttribute attribs with
     | Some(Attrib(_, _, [AttribInt32Arg state], _, _, _, _)) -> state = int System.ComponentModel.EditorBrowsableState.Never
     | _ -> false)

/// Indicate if a list of F# attributes contains 'ObsoleteAttribute'. Used to suppress the item in intellisense.
let CheckFSharpAttributesForObsolete g attribs = 
    not (isNil attribs) && (HasFSharpAttribute g g.attrib_SystemObsolete attribs)


/// Indicate if a list of F# attributes contains 'RequireArgumentNameAttribute'. Used to error on call site if arguments aren't explicitly named.
let CheckFSharpAttributesForRequireNamedParametersAttribute g attribs = 
    not (isNil attribs) && (HasFSharpAttribute g g.attrib_RequireArgumentNameAttribute attribs)

/// Indicate if a list of F# attributes contains 'ObsoleteAttribute'. Used to suppress the item in intellisense.
/// Also check the attributes for CompilerMessageAttribute, which has an IsHidden argument that allows
/// items to be suppressed from intellisense.
let CheckFSharpAttributesForUnseen g attribs _m = 
    not (isNil attribs) &&         
    (CheckFSharpAttributesForObsolete g attribs ||
        CheckFSharpAttributesForHidden g attribs)
      
#if !NO_EXTENSIONTYPING
/// Indicate if a list of provided attributes contains 'ObsoleteAttribute'. Used to suppress the item in intellisense.
let CheckProvidedAttributesForUnseen (provAttribs: Tainted<IProvidedCustomAttributeProvider>) m = 
    provAttribs.PUntaint((fun a -> a.GetAttributeConstructorArgs(provAttribs.TypeProvider.PUntaintNoFailure(id), typeof<System.ObsoleteAttribute>.FullName).IsSome), m)
#endif

/// Check the attributes associated with a property, returning warnings and errors as data.
let CheckPropInfoAttributes pinfo m = 
    match pinfo with
    | ILProp(ILPropInfo(_, pdef)) -> CheckILAttributes pinfo.TcGlobals false pdef.CustomAttrs m
    | FSProp(g, _, Some vref, _) 
    | FSProp(g, _, _, Some vref) -> CheckFSharpAttributes g vref.Attribs m
    | FSProp _ -> failwith "CheckPropInfoAttributes: unreachable"
#if !NO_EXTENSIONTYPING
    | ProvidedProp (amap, pi, m) ->  
        CheckProvidedAttributes amap.g m (pi.PApply((fun st -> (st :> IProvidedCustomAttributeProvider)), m))
         
#endif

      
/// Check the attributes associated with a IL field, returning warnings and errors as data.
let CheckILFieldAttributes g (finfo:ILFieldInfo) m = 
    match finfo with 
    | ILFieldInfo(_, pd) -> 
        CheckILAttributes g false pd.CustomAttrs m |> CommitOperationResult
#if !NO_EXTENSIONTYPING
    | ProvidedField (amap, fi, m) -> 
        CheckProvidedAttributes amap.g m (fi.PApply((fun st -> (st :> IProvidedCustomAttributeProvider)), m)) |> CommitOperationResult
#endif

/// Check the attributes associated with a method, returning warnings and errors as data.
let CheckMethInfoAttributes g m tyargsOpt minfo hasUnammedCallerArgs =
    let search = 
        BindMethInfoAttributes m minfo 
            (fun ilAttribs -> Some(CheckILAttributes g false ilAttribs m)) 
            (fun fsAttribs -> 
                let res = 
                    CheckFSharpAttributes g fsAttribs m ++ (fun () -> 
                        if Option.isNone tyargsOpt && HasFSharpAttribute g g.attrib_RequiresExplicitTypeArgumentsAttribute fsAttribs then
                            ErrorD(Error(FSComp.SR.tcFunctionRequiresExplicitTypeArguments(minfo.LogicalName), m))
                        elif Option.isSome hasUnammedCallerArgs && hasUnammedCallerArgs.Value && HasFSharpAttribute g g.attrib_RequireArgumentNameAttribute fsAttribs then
                            ErrorD(Error(FSComp.SR.tcMethodRequireNamedArgumentAttribute(minfo.LogicalName), m))
                        else
                            CompleteD)
                Some res) 
#if !NO_EXTENSIONTYPING
            (fun provAttribs -> Some (CheckProvidedAttributes g m provAttribs)) 
#else
            (fun _provAttribs -> None)
#endif 
    match search with
    | Some res -> res
    | None -> CompleteD // no attribute = no errors 

/// Indicate if a method has 'Obsolete', 'CompilerMessageAttribute' or 'TypeProviderEditorHideMethodsAttribute'. 
/// Used to suppress the item in intellisense.
let MethInfoIsUnseen g (m: range) (ty: TType) minfo = 
    let isUnseenByObsoleteAttrib () = 
        match BindMethInfoAttributes m minfo 
                (fun ilAttribs -> Some(CheckILAttributesForUnseen g ilAttribs m)) 
                (fun fsAttribs -> Some(CheckFSharpAttributesForUnseen g fsAttribs m))
#if !NO_EXTENSIONTYPING
                (fun provAttribs -> Some(CheckProvidedAttributesForUnseen provAttribs m))
#else
                (fun _provAttribs -> None)
#endif
                    with
        | Some res -> res
        | None -> false

    let isUnseenByHidingAttribute () = 
#if !NO_EXTENSIONTYPING
        not (isObjTy g ty) &&
        isAppTy g ty &&
        isObjTy g minfo.ApparentEnclosingType &&
        let tcref = tcrefOfAppTy g ty 
        match tcref.TypeReprInfo with 
        | TProvidedTypeExtensionPoint info -> 
            info.ProvidedType.PUntaint((fun st -> (st :> IProvidedCustomAttributeProvider).GetHasTypeProviderEditorHideMethodsAttribute(info.ProvidedType.TypeProvider.PUntaintNoFailure(id))), m)
        | _ -> 
        // This attribute check is done by name to ensure compilation doesn't take a dependency 
        // on Microsoft.FSharp.Core.CompilerServices.TypeProviderEditorHideMethodsAttribute.
        //
        // We are only interested in filtering out the method on System.Object, so it is sufficient
        // just to look at the attributes on IL methods.
        if tcref.IsILTycon then 
                tcref.ILTyconRawMetadata.CustomAttrs.AsArray 
                |> Array.exists (fun attr -> attr.Method.DeclaringType.TypeSpec.Name = typeof<TypeProviderEditorHideMethodsAttribute>.FullName)
        else 
            false
#else
        ty |> ignore
        false
#endif

    //let isUnseenByBeingTupleMethod () = isAnyTupleTy g ty

    isUnseenByObsoleteAttrib () || isUnseenByHidingAttribute () //|| isUnseenByBeingTupleMethod ()

/// Indicate if a property has 'Obsolete' or 'CompilerMessageAttribute'.
/// Used to suppress the item in intellisense.
let PropInfoIsUnseen m pinfo = 
    match pinfo with
    | ILProp (ILPropInfo(_, pdef) as ilpinfo) -> 
        // Properties on .NET tuple types are resolvable but unseen
        isAnyTupleTy pinfo.TcGlobals ilpinfo.ILTypeInfo.ToType || 
        CheckILAttributesForUnseen pinfo.TcGlobals pdef.CustomAttrs m
    | FSProp (g, _, Some vref, _) 
    | FSProp (g, _, _, Some vref) -> CheckFSharpAttributesForUnseen g vref.Attribs m
    | FSProp _ -> failwith "CheckPropInfoAttributes: unreachable"
#if !NO_EXTENSIONTYPING
    | ProvidedProp (_amap, pi, m) -> 
        CheckProvidedAttributesForUnseen (pi.PApply((fun st -> (st :> IProvidedCustomAttributeProvider)), m)) m
#endif
     
/// Check the attributes on an entity, returning errors and warnings as data.
let CheckEntityAttributes g (x:TyconRef) m = 
    if x.IsILTycon then 
        CheckILAttributes g (isByrefLikeTyconRef g m x) x.ILTyconRawMetadata.CustomAttrs m
    else 
        CheckFSharpAttributes g x.Attribs m

/// Check the attributes on a union case, returning errors and warnings as data.
let CheckUnionCaseAttributes g (x:UnionCaseRef) m =
    CheckEntityAttributes g x.TyconRef m ++ (fun () ->
    CheckFSharpAttributes g x.Attribs m)

/// Check the attributes on a record field, returning errors and warnings as data.
let CheckRecdFieldAttributes g (x:RecdFieldRef) m =
    CheckEntityAttributes g x.TyconRef m ++ (fun () ->
    CheckFSharpAttributes g x.PropertyAttribs m)

/// Check the attributes on an F# value, returning errors and warnings as data.
let CheckValAttributes g (x:ValRef) m =
    CheckFSharpAttributes g x.Attribs m

/// Check the attributes on a record field, returning errors and warnings as data.
let CheckRecdFieldInfoAttributes g (x:RecdFieldInfo) m =
    CheckRecdFieldAttributes g x.RecdFieldRef m

    
// Identify any security attributes
let IsSecurityAttribute (g: TcGlobals) amap (casmap : Dictionary<Stamp, bool>) (Attrib(tcref, _, _, _, _, _, _)) m =
    // There's no CAS on Silverlight, so we have to be careful here
    match g.attrib_SecurityAttribute with
    | None -> false
    | Some attr ->
        match attr.TyconRef.TryDeref with
        | ValueSome _ -> 
            let tcs = tcref.Stamp
            match casmap.TryGetValue tcs with
            | true, c -> c
            | _ ->
                let exists = ExistsInEntireHierarchyOfType (fun t -> typeEquiv g t (mkAppTy attr.TyconRef [])) g amap m AllowMultiIntfInstantiations.Yes (mkAppTy tcref [])
                casmap.[tcs] <- exists
                exists
        | ValueNone -> false  

let IsSecurityCriticalAttribute g (Attrib(tcref, _, _, _, _, _, _)) =
    (tyconRefEq g tcref g.attrib_SecurityCriticalAttribute.TyconRef || tyconRefEq g tcref g.attrib_SecuritySafeCriticalAttribute.TyconRef)

