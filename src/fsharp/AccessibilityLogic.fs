// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// The basic logic of private/internal/protected/InternalsVisibleTo/public accessibility
module internal FSharp.Compiler.AccessibilityLogic

open FSharp.Compiler.AbstractIL.IL 
open FSharp.Compiler 
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Infos
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TypedTreeOps

#if !NO_EXTENSIONTYPING
open FSharp.Compiler.ExtensionTyping
#endif

/// Represents the 'keys' a particular piece of code can use to access other constructs?.
[<NoEquality; NoComparison>]
type AccessorDomain = 
    /// cpaths: indicates we have the keys to access any members private to the given paths 
    /// tyconRefOpt:  indicates we have the keys to access any protected members of the super types of 'TyconRef' 
    | AccessibleFrom of cpaths: CompilationPath list * tyconRefOpt: TyconRef option        

    /// An AccessorDomain which returns public items
    | AccessibleFromEverywhere

    /// An AccessorDomain which returns everything but .NET private/internal items.
    /// This is used 
    ///    - when solving member trait constraints, which are solved independently of accessibility 
    ///    - for failure paths in error reporting, e.g. to produce an error that an F# item is not accessible
    ///    - an adhoc use in service.fs to look up a delegate signature
    | AccessibleFromSomeFSharpCode 

    /// An AccessorDomain which returns all items
    | AccessibleFromSomewhere 

    interface ITraitAccessorDomain

    // Hashing and comparison is used for the memoization tables keyed by an accessor domain.
    // It is dependent on a TcGlobals because of the TyconRef in the data structure
    static member CustomGetHashCode(ad:AccessorDomain) = 
        match ad with 
        | AccessibleFrom _ -> 1
        | AccessibleFromEverywhere -> 2
        | AccessibleFromSomeFSharpCode  -> 3
        | AccessibleFromSomewhere  -> 4

    static member CustomEquals(g:TcGlobals, ad1:AccessorDomain, ad2:AccessorDomain) = 
        match ad1, ad2 with 
        | AccessibleFrom(cs1, tc1), AccessibleFrom(cs2, tc2) -> (cs1 = cs2) && (match tc1, tc2 with None, None -> true | Some tc1, Some tc2 -> tyconRefEq g tc1 tc2 | _ -> false)
        | AccessibleFromEverywhere, AccessibleFromEverywhere -> true
        | AccessibleFromSomeFSharpCode, AccessibleFromSomeFSharpCode  -> true
        | AccessibleFromSomewhere, AccessibleFromSomewhere  -> true
        | _ -> false

/// Indicates if an F# item is accessible 
let IsAccessible ad taccess = 
    match ad with 
    | AccessibleFromEverywhere -> canAccessFromEverywhere taccess
    | AccessibleFromSomeFSharpCode -> canAccessFromSomewhere taccess
    | AccessibleFromSomewhere -> true
    | AccessibleFrom (cpaths, _tcrefViewedFromOption) -> 
        List.exists (canAccessFrom taccess) cpaths

/// Indicates if an IL member is accessible (ignoring its enclosing type)
let private IsILMemberAccessible g amap m (tcrefOfViewedItem : TyconRef) ad access = 
    match ad with 
    | AccessibleFromEverywhere -> 
            access = ILMemberAccess.Public

    | AccessibleFromSomeFSharpCode -> 
           (access = ILMemberAccess.Public || 
            access = ILMemberAccess.Family  || 
            access = ILMemberAccess.FamilyOrAssembly) 

    | AccessibleFrom (cpaths, tcrefViewedFromOption) ->

            let accessibleByFamily =
              ((access = ILMemberAccess.Family  || 
                access = ILMemberAccess.FamilyOrAssembly) &&
                match tcrefViewedFromOption with 
                | None -> false
                | Some tcrefViewedFrom ->
                    ExistsHeadTypeInEntireHierarchy  g amap m (generalizedTyconRef tcrefViewedFrom) tcrefOfViewedItem)     

            let accessibleByInternalsVisibleTo = 
                (access = ILMemberAccess.Assembly || access = ILMemberAccess.FamilyOrAssembly) && 
                canAccessFromOneOf cpaths tcrefOfViewedItem.CompilationPath

            let accessibleByFamilyAndAssembly =
                access = ILMemberAccess.FamilyAndAssembly &&
                canAccessFromOneOf cpaths tcrefOfViewedItem.CompilationPath &&
                match tcrefViewedFromOption with 
                | None -> false
                | Some tcrefViewedFrom ->
                    ExistsHeadTypeInEntireHierarchy  g amap m (generalizedTyconRef tcrefViewedFrom) tcrefOfViewedItem    

            (access = ILMemberAccess.Public) || accessibleByFamily || accessibleByInternalsVisibleTo || accessibleByFamilyAndAssembly

    | AccessibleFromSomewhere -> 
            true
    
/// Indicates if tdef is accessible. If tdef.Access = ILTypeDefAccess.Nested then encTyconRefOpt s TyconRef of enclosing type
/// and visibility of tdef is obtained using member access rules
let private IsILTypeDefAccessible (amap : Import.ImportMap) m ad encTyconRefOpt (tdef: ILTypeDef) =
    match tdef.Access with
    | ILTypeDefAccess.Nested nestedAccess ->
        match encTyconRefOpt with
        | None -> assert false; true
        | Some encTyconRef -> IsILMemberAccessible amap.g amap m encTyconRef ad nestedAccess
    | _ ->
        match ad with 
        | AccessibleFromSomewhere -> true
        | AccessibleFromEverywhere 
        | AccessibleFromSomeFSharpCode 
        | AccessibleFrom _ -> tdef.Access = ILTypeDefAccess.Public

/// Indicates if a TyconRef is visible through the AccessibleFrom(cpaths, _).
/// Note that InternalsVisibleTo extends those cpaths.
let private IsTyconAccessibleViaVisibleTo ad (tcrefOfViewedItem:TyconRef) =
    match ad with 
    | AccessibleFromEverywhere 
    | AccessibleFromSomewhere 
    | AccessibleFromSomeFSharpCode -> false
    | AccessibleFrom (cpaths, _tcrefViewedFromOption) ->
        canAccessFromOneOf cpaths tcrefOfViewedItem.CompilationPath
    
/// Indicates if given IL based TyconRef is accessible. If TyconRef is nested then we'll 
/// walk though the list of enclosing types and test if all of them are accessible 
let private IsILTypeInfoAccessible amap m ad (tcrefOfViewedItem : TyconRef) = 
    let (TILObjectReprData(scoref, enc, tdef)) = tcrefOfViewedItem.ILTyconInfo
    let rec check parentTycon path =
        let ilTypeDefAccessible =
            match parentTycon with
            | None -> 
                match path with
                | [] -> assert false; true // in this case path should have at least one element
                | [x] -> IsILTypeDefAccessible amap m ad None x // shortcut for non-nested types
                | x :: xs -> 
                    // check if enclosing type x is accessible.
                    // if yes - create parent tycon for type 'x' and continue with the rest of the path
                    IsILTypeDefAccessible amap m ad None x && 
                    (
                        let parentILTyRef = mkRefForNestedILTypeDef scoref ([], x)
                        let parentTycon = Import.ImportILTypeRef amap m parentILTyRef
                        check (Some (parentTycon, [x])) xs
                    )
            | (Some (parentTycon, parentPath)) -> 
                match path with
                | [] -> true // end of path is reached - success
                | x :: xs -> 
                    // check if x is accessible from the parent tycon
                    // if yes - create parent tycon for type 'x' and continue with the rest of the path
                    IsILTypeDefAccessible amap m ad (Some parentTycon) x &&
                    (
                        let parentILTyRef = mkRefForNestedILTypeDef scoref (parentPath, x)
                        let parentTycon = Import.ImportILTypeRef amap m parentILTyRef
                        check (Some (parentTycon, parentPath @ [x])) xs
                    )
        ilTypeDefAccessible || IsTyconAccessibleViaVisibleTo ad tcrefOfViewedItem
        
    check None (enc @ [tdef])
                       
/// Indicates if an IL member associated with the given ILType is accessible
let private IsILTypeAndMemberAccessible g amap m adType ad (ty: ILTypeInfo) access = 
    IsILTypeInfoAccessible amap m adType ty.TyconRefOfRawMetadata && IsILMemberAccessible g amap m ty.TyconRefOfRawMetadata ad access

/// Indicates if an entity is accessible
let IsEntityAccessible amap m ad (tcref:TyconRef) = 
    if tcref.IsILTycon then 
        IsILTypeInfoAccessible amap m ad tcref
    else  
        tcref.Accessibility |> IsAccessible ad

/// Check that an entity is accessible
let CheckTyconAccessible amap m ad tcref =
    let res = IsEntityAccessible amap m ad tcref
    if not res then  
        errorR(Error(FSComp.SR.typeIsNotAccessible tcref.DisplayName, m))
    res

/// Indicates if a type definition and its representation contents are accessible
let IsTyconReprAccessible amap m ad tcref =
    IsEntityAccessible amap m ad tcref &&
    IsAccessible ad tcref.TypeReprAccessibility
            
/// Check that a type definition and its representation contents are accessible
let CheckTyconReprAccessible amap m ad tcref =
    CheckTyconAccessible amap m ad tcref &&
    (let res = IsAccessible ad tcref.TypeReprAccessibility
     if not res then 
         errorR (Error (FSComp.SR.unionCasesAreNotAccessible tcref.DisplayName, m))
     res)
            
/// Indicates if a type is accessible (both definition and instantiation)
let rec IsTypeAccessible g amap m ad ty = 
    match tryAppTy g ty with
    | ValueNone -> true
    | ValueSome(tcref, tinst) ->
        IsEntityAccessible amap m ad tcref && IsTypeInstAccessible g amap m ad tinst

and IsTypeInstAccessible g amap m ad tinst = 
    match tinst with 
    | [] -> true 
    | _ -> List.forall (IsTypeAccessible g amap m ad) tinst

/// Indicate if a provided member is accessible
let IsProvidedMemberAccessible (amap:Import.ImportMap) m ad ty access = 
    let g = amap.g
    if IsTypeAccessible g amap m ad ty then 
        match tryTcrefOfAppTy g ty with
        | ValueNone -> true
        | ValueSome tcrefOfViewedItem ->
            IsILMemberAccessible g amap m tcrefOfViewedItem ad access
    else
        false

/// Compute the accessibility of a provided member
let ComputeILAccess isPublic isFamily isFamilyOrAssembly isFamilyAndAssembly =
    if isPublic then ILMemberAccess.Public
    elif isFamily then ILMemberAccess.Family
    elif isFamilyOrAssembly then ILMemberAccess.FamilyOrAssembly
    elif isFamilyAndAssembly then ILMemberAccess.FamilyAndAssembly
    else ILMemberAccess.Private

let IsILFieldInfoAccessible g amap m ad x = 
    match x with 
    | ILFieldInfo (tinfo, fd) -> IsILTypeAndMemberAccessible g amap m ad ad tinfo fd.Access
#if !NO_EXTENSIONTYPING
    | ProvidedField (amap, tpfi, m) -> 
        let access = tpfi.PUntaint((fun fi -> ComputeILAccess fi.IsPublic fi.IsFamily fi.IsFamilyOrAssembly fi.IsFamilyAndAssembly), m)
        IsProvidedMemberAccessible amap m ad x.ApparentEnclosingType access
#endif

let GetILAccessOfILEventInfo (ILEventInfo (tinfo, edef)) =
    (resolveILMethodRef tinfo.RawMetadata edef.AddMethod).Access 

let IsILEventInfoAccessible g amap m ad einfo =
    let access = GetILAccessOfILEventInfo einfo
    IsILTypeAndMemberAccessible g amap m ad ad einfo.ILTypeInfo access

let private IsILMethInfoAccessible g amap m adType ad ilminfo = 
    match ilminfo with 
    | ILMethInfo (_, ty, None, mdef, _) -> IsILTypeAndMemberAccessible g amap m adType ad (ILTypeInfo.FromType g ty) mdef.Access 
    | ILMethInfo (_, _, Some declaringTyconRef, mdef, _) -> IsILMemberAccessible g amap m declaringTyconRef ad mdef.Access

let GetILAccessOfILPropInfo (ILPropInfo(tinfo, pdef)) =
    let tdef = tinfo.RawMetadata
    let ilAccess =
        match pdef.GetMethod, pdef.SetMethod with 
        | Some mref, None 
        | None, Some mref -> (resolveILMethodRef tdef mref).Access

        | Some mrefGet, Some mrefSet ->
            //
            // Dotnet properties have a getter and a setter method, each of which can have a separate visibility public, protected, private etc ...
            // This code computes the visibility for the property by choosing the most visible method. This approximation is usefull for cases
            // where the compiler needs to know the visibility of the property.
            // The specific ordering for choosing the most visible is:
            //  ILMemberAccess.Public,
            //  ILMemberAccess.FamilyOrAssembly
            //  ILMemberAccess.Assembly
            //  ILMemberAccess.Family
            //  ILMemberAccess.FamilyAndAssembly
            //  ILMemberAccess.Private
            //  ILMemberAccess.CompilerControlled
            //          
            let getA = (resolveILMethodRef tdef mrefGet).Access
            let setA = (resolveILMethodRef tdef mrefSet).Access

            // Use the accessors to determine the visibility of the property.
            // N.B. It is critical to keep the ordering in decreasing visibility order in the following match expression
            match getA, setA with
            | ILMemberAccess.Public, _
            | _, ILMemberAccess.Public -> ILMemberAccess.Public

            | ILMemberAccess.FamilyOrAssembly, _
            | _, ILMemberAccess.FamilyOrAssembly -> ILMemberAccess.FamilyOrAssembly

            | ILMemberAccess.Assembly, _
            | _, ILMemberAccess.Assembly -> ILMemberAccess.Assembly

            | ILMemberAccess.Family, _
            | _, ILMemberAccess.Family -> ILMemberAccess.Family

            | ILMemberAccess.FamilyAndAssembly, _
            | _, ILMemberAccess.FamilyAndAssembly -> ILMemberAccess.FamilyAndAssembly

            | ILMemberAccess.Private, _
            | _, ILMemberAccess.Private -> ILMemberAccess.Private

            | ILMemberAccess.CompilerControlled, _
            | _, ILMemberAccess.CompilerControlled -> ILMemberAccess.CompilerControlled

        | None, None -> ILMemberAccess.Public

    ilAccess

let IsILPropInfoAccessible g amap m ad pinfo =
    let ilAccess = GetILAccessOfILPropInfo pinfo
    IsILTypeAndMemberAccessible g amap m ad ad pinfo.ILTypeInfo ilAccess

let IsValAccessible ad (vref:ValRef) = 
    vref.Accessibility |> IsAccessible ad

let CheckValAccessible  m ad (vref:ValRef) = 
    if not (IsValAccessible ad vref) then 
        errorR (Error (FSComp.SR.valueIsNotAccessible vref.DisplayName, m))
        
let IsUnionCaseAccessible amap m ad (ucref:UnionCaseRef) =
    IsTyconReprAccessible amap m ad ucref.TyconRef &&
    IsAccessible ad ucref.UnionCase.Accessibility

let CheckUnionCaseAccessible amap m ad (ucref:UnionCaseRef) =
    CheckTyconReprAccessible amap m ad ucref.TyconRef &&
    (let res = IsAccessible ad ucref.UnionCase.Accessibility
     if not res then 
        errorR (Error (FSComp.SR.unionCaseIsNotAccessible ucref.CaseName, m))
     res)

let IsRecdFieldAccessible amap m ad (rfref:RecdFieldRef) =
    IsTyconReprAccessible amap m ad rfref.TyconRef &&
    IsAccessible ad rfref.RecdField.Accessibility

let CheckRecdFieldAccessible amap m ad (rfref:RecdFieldRef) =
    CheckTyconReprAccessible amap m ad rfref.TyconRef &&
    (let res = IsAccessible ad rfref.RecdField.Accessibility
     if not res then 
        errorR (Error (FSComp.SR.fieldIsNotAccessible rfref.FieldName, m))
     res)

let CheckRecdFieldInfoAccessible amap m ad (rfinfo:RecdFieldInfo) = 
    CheckRecdFieldAccessible amap m ad rfinfo.RecdFieldRef |> ignore

let CheckILFieldInfoAccessible g amap m ad finfo =
    if not (IsILFieldInfoAccessible g amap m ad finfo) then 
        errorR (Error (FSComp.SR.structOrClassFieldIsNotAccessible finfo.FieldName, m))
    
/// Uses a separate accessibility domains for containing type and method itself
/// This makes sense cases like
/// type A() =
///     type protected B() =
///         member this.Public() = ()
///         member protected this.Protected() = ()
/// type C() =
///     inherit A()
///     let x = A.B()
///     do x.Public()
/// when calling x.SomeMethod() we need to use 'adTyp' do verify that type of x is accessible from C 
/// and 'ad' to determine accessibility of SomeMethod.
/// I.e when calling x.Public() and x.Protected() -in both cases first check should succeed and second - should fail in the latter one. 
let IsTypeAndMethInfoAccessible amap m accessDomainTy ad = function
    | ILMeth (g, x, _) -> IsILMethInfoAccessible g amap m accessDomainTy ad x 
    | FSMeth (_, _, vref, _) -> IsValAccessible ad vref
    | DefaultStructCtor(g, ty) -> IsTypeAccessible g amap m ad ty
#if !NO_EXTENSIONTYPING
    | ProvidedMeth(amap, tpmb, _, m) as etmi -> 
        let access = tpmb.PUntaint((fun mi -> ComputeILAccess mi.IsPublic mi.IsFamily mi.IsFamilyOrAssembly mi.IsFamilyAndAssembly), m)        
        IsProvidedMemberAccessible amap m ad etmi.ApparentEnclosingType access
#endif
let IsMethInfoAccessible amap m ad minfo = IsTypeAndMethInfoAccessible amap m ad ad minfo

let IsPropInfoAccessible g amap m ad = function 
    | ILProp ilpinfo -> IsILPropInfoAccessible g amap m ad ilpinfo
    | FSProp (_, _, Some vref, None) 
    | FSProp (_, _, None, Some vref) -> IsValAccessible ad vref
    | FSProp (_, _, Some vrefGet, Some vrefSet) -> 
        // pick most accessible
        IsValAccessible ad vrefGet || IsValAccessible ad vrefSet
#if !NO_EXTENSIONTYPING
    | ProvidedProp (amap, tppi, m) as pp-> 
        let access = 
            let a = tppi.PUntaint((fun ppi -> 
                let tryGetILAccessForProvidedMethodBase (mi : ProvidedMethodBase) = 
                    match mi with
                    | null -> None
                    | mi -> Some(ComputeILAccess mi.IsPublic mi.IsFamily mi.IsFamilyOrAssembly mi.IsFamilyAndAssembly)
                match tryGetILAccessForProvidedMethodBase(ppi.GetGetMethod()) with
                | None -> tryGetILAccessForProvidedMethodBase(ppi.GetSetMethod())
                | x -> x), m)
            defaultArg a ILMemberAccess.Public
        IsProvidedMemberAccessible amap m ad pp.ApparentEnclosingType access
#endif
    | _ -> false

let IsFieldInfoAccessible ad (rfref:RecdFieldInfo) =
    IsAccessible ad rfref.RecdField.Accessibility
