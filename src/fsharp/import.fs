// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Functions to import .NET binary metadata as TAST objects
module internal FSharp.Compiler.Import

open System.Collections.Concurrent
open System.Collections.Generic
open Internal.Utilities.Library
open Internal.Utilities.Library.Extras
open FSharp.Compiler 
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.CompilerGlobalState
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Syntax
open FSharp.Compiler.SyntaxTreeOps
open FSharp.Compiler.Text
open FSharp.Compiler.Xml
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.TcGlobals

#if !NO_EXTENSIONTYPING
open FSharp.Compiler.ExtensionTyping
#endif

/// Represents an interface to some of the functionality of TcImports, for loading assemblies 
/// and accessing information about generated provided assemblies.
type AssemblyLoader = 

    /// Resolve an Abstract IL assembly reference to a Ccu
    abstract FindCcuFromAssemblyRef : CompilationThreadToken * range * ILAssemblyRef -> CcuResolutionResult

    abstract TryFindXmlDocumentationInfo : assemblyName: string -> XmlDocumentationInfo option

#if !NO_EXTENSIONTYPING

    /// Get a flag indicating if an assembly is a provided assembly, plus the
    /// table of information recording remappings from type names in the provided assembly to type
    /// names in the statically linked, embedded assembly.
    abstract GetProvidedAssemblyInfo : CompilationThreadToken * range * Tainted<ProvidedAssembly> -> bool * ProvidedAssemblyStaticLinkingMap option

    /// Record a root for a [<Generate>] type to help guide static linking & type relocation
    abstract RecordGeneratedTypeRoot : ProviderGeneratedType -> unit
#endif
        
//-------------------------------------------------------------------------
// Import an IL types as F# types.
//------------------------------------------------------------------------- 

/// Represents a context used by the import routines that convert AbstractIL types and provided 
/// types to F# internal compiler data structures. 
///
/// Also caches the conversion of AbstractIL ILTypeRef nodes, based on hashes of these.
///
/// There is normally only one ImportMap for any assembly compilation, though additional instances can be created
/// using tcImports.GetImportMap() if needed, and it is not harmful if multiple instances are used. The object 
/// serves as an interface through to the tables stored in the primary TcImports structures defined in CompileOps.fs. 
[<Sealed>]
type ImportMap(g: TcGlobals, assemblyLoader: AssemblyLoader) =
    let typeRefToTyconRefCache = ConcurrentDictionary<ILTypeRef, TyconRef>()
    member this.g = g
    member this.assemblyLoader = assemblyLoader
    member this.ILTypeRefToTyconRefCache = typeRefToTyconRefCache

let CanImportILScopeRef (env: ImportMap) m scoref = 

    let isResolved assemblyRef =
        // Explanation: This represents an unchecked invariant in the hosted compiler: that any operations
        // which import types (and resolve assemblies from the tcImports tables) happen on the compilation thread.
        let ctok = AssumeCompilationThreadWithoutEvidence() 
    
        match env.assemblyLoader.FindCcuFromAssemblyRef (ctok, m, assemblyRef) with
        | UnresolvedCcu _ -> false
        | ResolvedCcu _ -> true

    match scoref with 
    | ILScopeRef.Local
    | ILScopeRef.Module _ -> true
    | ILScopeRef.Assembly assemblyRef -> isResolved assemblyRef
    | ILScopeRef.PrimaryAssembly -> isResolved env.g.ilg.primaryAssemblyRef

/// Import a reference to a type definition, given the AbstractIL data for the type reference
let ImportTypeRefData (env: ImportMap) m (scoref, path, typeName) = 

    let findCcu assemblyRef =
        // Explanation: This represents an unchecked invariant in the hosted compiler: that any operations
        // which import types (and resolve assemblies from the tcImports tables) happen on the compilation thread.
        let ctok = AssumeCompilationThreadWithoutEvidence()

        env.assemblyLoader.FindCcuFromAssemblyRef (ctok, m, assemblyRef)

    let ccu = 
        match scoref with 
        | ILScopeRef.Local    -> error(InternalError("ImportILTypeRef: unexpected local scope", m))
        | ILScopeRef.Module _ -> error(InternalError("ImportILTypeRef: reference found to a type in an auxiliary module", m))
        | ILScopeRef.Assembly assemblyRef -> findCcu assemblyRef
        | ILScopeRef.PrimaryAssembly -> findCcu env.g.ilg.primaryAssemblyRef

    // Do a dereference of a fake tcref for the type just to check it exists in the target assembly and to find
    // the corresponding Tycon.
    let ccu = 
        match ccu with
        | ResolvedCcu ccu->ccu
        | UnresolvedCcu ccuName -> 
            error (Error(FSComp.SR.impTypeRequiredUnavailable(typeName, ccuName), m))
    let fakeTyconRef = mkNonLocalTyconRef (mkNonLocalEntityRef ccu path) typeName
    let tycon = 
        try   
            fakeTyconRef.Deref
        with _ ->
            error (Error(FSComp.SR.impReferencedTypeCouldNotBeFoundInAssembly(String.concat "." (Array.append path  [| typeName |]), ccu.AssemblyName), m))
#if !NO_EXTENSIONTYPING
    // Validate (once because of caching)
    match tycon.TypeReprInfo with
    | TProvidedTypeExtensionPoint info ->
            //printfn "ImportTypeRefData: validating type: typeLogicalName = %A" typeName
            ExtensionTyping.ValidateProvidedTypeAfterStaticInstantiation(m, info.ProvidedType, path, typeName)
    | _ -> 
            ()
#endif
    match tryRescopeEntity ccu tycon with 
    | ValueNone -> error (Error(FSComp.SR.impImportedAssemblyUsesNotPublicType(String.concat "." (Array.toList path@[typeName])), m))
    | ValueSome tcref -> tcref
    

/// Import a reference to a type definition, given an AbstractIL ILTypeRef, without caching
//
// Note, the type names that flow to the point include the "mangled" type names used for static parameters for provided types.
// For example,
//       Foo.Bar,"1.0"
// This is because ImportProvidedType goes via Abstract IL type references. 
let ImportILTypeRefUncached (env: ImportMap) m (tref: ILTypeRef) = 
    let path, typeName = 
        match tref.Enclosing with 
        | [] -> 
            splitILTypeNameWithPossibleStaticArguments tref.Name
        | h :: t -> 
            let nsp, tname = splitILTypeNameWithPossibleStaticArguments h
            // Note, subsequent type names do not need to be split, only the first
            [| yield! nsp; yield tname; yield! t |], tref.Name

    ImportTypeRefData (env: ImportMap) m (tref.Scope, path, typeName)

    
/// Import a reference to a type definition, given an AbstractIL ILTypeRef, with caching
let ImportILTypeRef (env: ImportMap) m (tref: ILTypeRef) =
    match env.ILTypeRefToTyconRefCache.TryGetValue tref with
    | true, tcref -> tcref
    | _ ->
        let tcref = ImportILTypeRefUncached  env m tref
        env.ILTypeRefToTyconRefCache.[tref] <- tcref
        tcref

/// Import a reference to a type definition, given an AbstractIL ILTypeRef, with caching
let CanImportILTypeRef (env: ImportMap) m (tref: ILTypeRef) =
    env.ILTypeRefToTyconRefCache.ContainsKey(tref) || CanImportILScopeRef env m tref.Scope

/// Import a type, given an AbstractIL ILTypeRef and an F# type instantiation.
/// 
/// Prefer the F# abbreviation for some built-in types, e.g. 'string' rather than 
/// 'System.String', since we prefer the F# abbreviation to the .NET equivalents. 
let ImportTyconRefApp (env: ImportMap) tcref tyargs = 
    env.g.improveType tcref tyargs 

/// Import an IL type as an F# type.
let rec ImportILType (env: ImportMap) m tinst ty =  
    match ty with
    | ILType.Void -> 
        env.g.unit_ty

    | ILType.Array(bounds, ty) -> 
        let n = bounds.Rank
        let elementType = ImportILType env m tinst ty
        mkArrayTy env.g n elementType m

    | ILType.Boxed  tspec | ILType.Value tspec ->
        let tcref = ImportILTypeRef env m tspec.TypeRef 
        let inst = tspec.GenericArgs |> List.map (ImportILType env m tinst) 
        ImportTyconRefApp env tcref inst

    | ILType.Byref ty -> mkByrefTy env.g (ImportILType env m tinst ty)
    | ILType.Ptr ILType.Void  when env.g.voidptr_tcr.CanDeref -> mkVoidPtrTy env.g
    | ILType.Ptr ty  -> mkNativePtrTy env.g (ImportILType env m tinst ty)
    | ILType.FunctionPointer _ -> env.g.nativeint_ty (* failwith "cannot import this kind of type (ptr, fptr)" *)
    | ILType.Modified(_, _, ty) -> 
         // All custom modifiers are ignored
         ImportILType env m tinst ty
    | ILType.TypeVar u16 -> 
         try List.item (int u16) tinst
         with _ -> 
              error(Error(FSComp.SR.impNotEnoughTypeParamsInScopeWhileImporting(), m))

/// Determines if an IL type can be imported as an F# type
let rec CanImportILType (env: ImportMap) m ty =  
    match ty with
    | ILType.Void -> true
    | ILType.Array(_bounds, ety) -> CanImportILType env m ety
    | ILType.Boxed  tspec | ILType.Value tspec ->
        CanImportILTypeRef env m tspec.TypeRef 
        && tspec.GenericArgs |> List.forall (CanImportILType env m) 
    | ILType.Byref ety -> CanImportILType env m ety
    | ILType.Ptr ety  -> CanImportILType env m ety
    | ILType.FunctionPointer _ -> true
    | ILType.Modified(_, _, ety) -> CanImportILType env m ety
    | ILType.TypeVar _u16 -> true

#if !NO_EXTENSIONTYPING

/// Import a provided type reference as an F# type TyconRef
let ImportProvidedNamedType (env: ImportMap) (m: range) (st: Tainted<ProvidedType>) = 
    // See if a reverse-mapping exists for a generated/relocated System.Type
    match st.PUntaint((fun st -> st.TryGetTyconRef()), m) with 
    | Some x -> (x :?> TyconRef)
    | None ->         
        let tref = ExtensionTyping.GetILTypeRefOfProvidedType (st, m)
        ImportILTypeRef env m tref

/// Import a provided type as an AbstractIL type
let rec ImportProvidedTypeAsILType (env: ImportMap) (m: range) (st: Tainted<ProvidedType>) = 
    if st.PUntaint ((fun x -> x.IsVoid), m) then ILType.Void
    elif st.PUntaint((fun st -> st.IsGenericParameter), m) then
        mkILTyvarTy (uint16 (st.PUntaint((fun st -> st.GenericParameterPosition), m)))
    elif st.PUntaint((fun st -> st.IsArray), m) then 
        let et = ImportProvidedTypeAsILType env m (st.PApply((fun st -> st.GetElementType()), m))
        ILType.Array(ILArrayShape.FromRank (st.PUntaint((fun st -> st.GetArrayRank()), m)), et)
    elif st.PUntaint((fun st -> st.IsByRef), m) then 
        let et = ImportProvidedTypeAsILType env m (st.PApply((fun st -> st.GetElementType()), m))
        ILType.Byref et
    elif st.PUntaint((fun st -> st.IsPointer), m) then 
        let et = ImportProvidedTypeAsILType env m (st.PApply((fun st -> st.GetElementType()), m))
        ILType.Ptr et
    else
        let gst, genericArgs = 
            if st.PUntaint((fun st -> st.IsGenericType), m) then 
                let args = st.PApplyArray((fun st -> st.GetGenericArguments()), "GetGenericArguments", m) |> Array.map (ImportProvidedTypeAsILType env m) |> List.ofArray 
                let gst = st.PApply((fun st -> st.GetGenericTypeDefinition()), m)
                gst, args
            else   
                st, []
        let tref = ExtensionTyping.GetILTypeRefOfProvidedType (gst, m)
        let tcref = ImportProvidedNamedType env m gst
        let tps = tcref.Typars m
        if tps.Length <> genericArgs.Length then 
           error(Error(FSComp.SR.impInvalidNumberOfGenericArguments(tcref.CompiledName, tps.Length, genericArgs.Length), m))
        // We're converting to an IL type, where generic arguments are erased
        let genericArgs = List.zip tps genericArgs |> List.filter (fun (tp, _) -> not tp.IsErased) |> List.map snd

        let tspec = mkILTySpec(tref, genericArgs)
        if st.PUntaint((fun st -> st.IsValueType), m) then 
            ILType.Value tspec 
        else 
            mkILBoxedType tspec

/// Import a provided type as an F# type.
let rec ImportProvidedType (env: ImportMap) (m: range) (* (tinst: TypeInst) *) (st: Tainted<ProvidedType>) = 

    // Explanation: The two calls below represent am unchecked invariant of the hosted compiler: 
    // that type providers are only activated on the CompilationThread. This invariant is not currently checked 
    // via CompilationThreadToken passing. We leave the two calls below as a reminder of this.
    //
    // This function is one major source of type provider activations, but not the only one: almost 
    // any call in the 'ExtensionTyping' module is a potential type provider activation.
    let ctok = AssumeCompilationThreadWithoutEvidence ()
    RequireCompilationThread ctok

    let g = env.g
    if st.PUntaint((fun st -> st.IsArray), m) then 
        let elemTy = (ImportProvidedType env m (* tinst *) (st.PApply((fun st -> st.GetElementType()), m)))
        mkArrayTy g (st.PUntaint((fun st -> st.GetArrayRank()), m))  elemTy m
    elif st.PUntaint((fun st -> st.IsByRef), m) then 
        let elemTy = (ImportProvidedType env m (* tinst *) (st.PApply((fun st -> st.GetElementType()), m)))
        mkByrefTy g elemTy
    elif st.PUntaint((fun st -> st.IsPointer), m) then 
        let elemTy = (ImportProvidedType env m (* tinst *) (st.PApply((fun st -> st.GetElementType()), m)))
        if isUnitTy g elemTy || isVoidTy g elemTy && g.voidptr_tcr.CanDeref then 
            mkVoidPtrTy g 
        else
            mkNativePtrTy g elemTy
    else

        // REVIEW: Extension type could try to be its own generic arg (or there could be a type loop)
        let tcref, genericArgs = 
            if st.PUntaint((fun st -> st.IsGenericType), m) then 
                let tcref = ImportProvidedNamedType env m (st.PApply((fun st -> st.GetGenericTypeDefinition()), m))
                let args = st.PApplyArray((fun st -> st.GetGenericArguments()), "GetGenericArguments", m) |> Array.map (ImportProvidedType env m (* tinst *) ) |> List.ofArray 
                tcref, args
            else 
                let tcref = ImportProvidedNamedType env m st
                tcref, [] 
        
        let genericArgsLength = genericArgs.Length
        /// Adjust for the known primitive numeric types that accept units of measure. 
        let tcref =
            if genericArgsLength = 1 then
                // real
                if tyconRefEq g tcref g.system_Double_tcref then g.pfloat_tcr
                elif tyconRefEq g tcref g.system_Single_tcref then g.pfloat32_tcr
                elif tyconRefEq g tcref g.system_Decimal_tcref then g.pdecimal_tcr
                // signed
                elif tyconRefEq g tcref g.system_Int16_tcref then g.pint16_tcr
                elif tyconRefEq g tcref g.system_Int32_tcref then g.pint_tcr
                elif tyconRefEq g tcref g.system_Int64_tcref then g.pint64_tcr
                elif tyconRefEq g tcref g.system_SByte_tcref then g.pint8_tcr
                // unsigned
                elif tyconRefEq g tcref g.system_UInt16_tcref then g.puint16_tcr
                elif tyconRefEq g tcref g.system_UInt32_tcref then g.puint_tcr
                elif tyconRefEq g tcref g.system_UInt64_tcref then g.puint64_tcr
                elif tyconRefEq g tcref g.system_Byte_tcref then g.puint8_tcr
                //native
                elif tyconRefEq g tcref g.system_IntPtr_tcref then g.pnativeint_tcr
                elif tyconRefEq g tcref g.system_UIntPtr_tcref then g.punativeint_tcr
                // other
                else tcref
            else
                tcref

        let tps = tcref.Typars m
        if tps.Length <> genericArgsLength then 
           error(Error(FSComp.SR.impInvalidNumberOfGenericArguments(tcref.CompiledName, tps.Length, genericArgsLength), m))

        let genericArgs = 
            (tps, genericArgs) ||> List.map2 (fun tp genericArg ->  
                if tp.Kind = TyparKind.Measure then  
                    let rec conv ty = 
                        match ty with 
                        | TType_app (tcref, [t1;t2]) when tyconRefEq g tcref g.measureproduct_tcr -> Measure.Prod (conv t1, conv t2)
                        | TType_app (tcref, [t1]) when tyconRefEq g tcref g.measureinverse_tcr -> Measure.Inv (conv t1)
                        | TType_app (tcref, []) when tyconRefEq g tcref g.measureone_tcr -> Measure.One 
                        | TType_app (tcref, []) when tcref.TypeOrMeasureKind = TyparKind.Measure -> Measure.Con tcref
                        | TType_app (tcref, _) -> 
                            errorR(Error(FSComp.SR.impInvalidMeasureArgument1(tcref.CompiledName, tp.Name), m))
                            Measure.One
                        | _ -> 
                            errorR(Error(FSComp.SR.impInvalidMeasureArgument2(tp.Name), m))
                            Measure.One

                    TType_measure (conv genericArg)
                else
                    genericArg)

        ImportTyconRefApp env tcref genericArgs


/// Import a provided method reference as an Abstract IL method reference
let ImportProvidedMethodBaseAsILMethodRef (env: ImportMap) (m: range) (mbase: Tainted<ProvidedMethodBase>) = 
     let tref = ExtensionTyping.GetILTypeRefOfProvidedType (mbase.PApply((fun mbase -> mbase.DeclaringType), m), m)

     let mbase = 
         // Find the formal member corresponding to the called member
         match mbase.OfType<ProvidedMethodInfo>() with 
         | Some minfo when 
                    minfo.PUntaint((fun minfo -> minfo.IsGenericMethod|| minfo.DeclaringType.IsGenericType), m) -> 
                let declaringType = minfo.PApply((fun minfo -> minfo.DeclaringType), m)
                let declaringGenericTypeDefn =  
                    if declaringType.PUntaint((fun t -> t.IsGenericType), m) then 
                        declaringType.PApply((fun declaringType -> declaringType.GetGenericTypeDefinition()), m)
                    else 
                        declaringType
                let methods = declaringGenericTypeDefn.PApplyArray((fun x -> x.GetMethods()), "GetMethods", m) 
                let metadataToken = minfo.PUntaint((fun minfo -> minfo.MetadataToken), m)
                let found = methods |> Array.tryFind (fun x -> x.PUntaint((fun x -> x.MetadataToken), m) = metadataToken) 
                match found with
                |   Some found -> found.Coerce(m)
                |   None -> 
                        let methodName = minfo.PUntaint((fun minfo -> minfo.Name), m)
                        let typeName = declaringGenericTypeDefn.PUntaint((fun declaringGenericTypeDefn -> declaringGenericTypeDefn.FullName), m)
                        error(Error(FSComp.SR.etIncorrectProvidedMethod(ExtensionTyping.DisplayNameOfTypeProvider(minfo.TypeProvider, m), methodName, metadataToken, typeName), m))
         | _ -> 
         match mbase.OfType<ProvidedConstructorInfo>() with 
         | Some cinfo when cinfo.PUntaint((fun x -> x.DeclaringType.IsGenericType), m) -> 
                let declaringType = cinfo.PApply((fun x -> x.DeclaringType), m)
                let declaringGenericTypeDefn =  declaringType.PApply((fun x -> x.GetGenericTypeDefinition()), m)
                // We have to find the uninstantiated formal signature corresponding to this instantiated constructor.
                // Annoyingly System.Reflection doesn't give us a MetadataToken to compare on, so we have to look by doing
                // the instantiation and comparing..
                let found = 
                    let ctors = declaringGenericTypeDefn.PApplyArray((fun x -> x.GetConstructors()), "GetConstructors", m) 
                    let actualParameterTypes = 
                        [ for p in cinfo.PApplyArray((fun x -> x.GetParameters()), "GetParameters", m) do
                            yield ImportProvidedType env m (p.PApply((fun p -> p.ParameterType), m)) ]
                    let actualGenericArgs = argsOfAppTy env.g (ImportProvidedType env m declaringType)
                    ctors |> Array.tryFind (fun ctor -> 
                       let formalParameterTypesAfterInstantiation = 
                           [ for p in ctor.PApplyArray((fun x -> x.GetParameters()), "GetParameters", m) do
                                let ilFormalTy = ImportProvidedTypeAsILType env m (p.PApply((fun p -> p.ParameterType), m))
                                yield ImportILType env m actualGenericArgs ilFormalTy ]
                       (formalParameterTypesAfterInstantiation, actualParameterTypes) ||>  List.lengthsEqAndForall2 (typeEquiv env.g))
                     
                match found with
                |   Some found -> found.Coerce(m)
                |   None -> 
                    let typeName = declaringGenericTypeDefn.PUntaint((fun x -> x.FullName), m)
                    error(Error(FSComp.SR.etIncorrectProvidedConstructor(ExtensionTyping.DisplayNameOfTypeProvider(cinfo.TypeProvider, m), typeName), m))
         | _ -> mbase

     let rty = 
         match mbase.OfType<ProvidedMethodInfo>() with 
         |  Some minfo -> minfo.PApply((fun minfo -> minfo.ReturnType), m)
         |  None ->
            match mbase.OfType<ProvidedConstructorInfo>() with
            | Some _  -> mbase.PApply((fun _ -> ProvidedType.Void), m)
            | _ -> failwith "unexpected"
     let genericArity = 
        if mbase.PUntaint((fun x -> x.IsGenericMethod), m) then 
            mbase.PUntaint((fun x -> x.GetGenericArguments().Length), m)
        else 0
     let callingConv = (if mbase.PUntaint((fun x -> x.IsStatic), m) then ILCallingConv.Static else ILCallingConv.Instance)
     let parameters = 
         [ for p in mbase.PApplyArray((fun x -> x.GetParameters()), "GetParameters", m) do
              yield ImportProvidedTypeAsILType env m (p.PApply((fun p -> p.ParameterType), m)) ]
     mkILMethRef (tref, callingConv, mbase.PUntaint((fun x -> x.Name), m), genericArity, parameters, ImportProvidedTypeAsILType env m rty )
#endif

//-------------------------------------------------------------------------
// Load an IL assembly into the compiler's internal data structures
// Careful use is made of laziness here to ensure we don't read the entire IL
// assembly on startup.
//-------------------------------------------------------------------------- 

/// Import a set of Abstract IL generic parameter specifications as a list of new
/// F# generic parameters.  
/// 
/// Fixup the constraints so that any references to the generic parameters
/// in the constraints now refer to the new generic parameters.
let ImportILGenericParameters amap m scoref tinst (gps: ILGenericParameterDefs) = 
    match gps with 
    | [] -> []
    | _ -> 
        let amap = amap()
        let tps = gps |> List.map (fun gp -> Construct.NewRigidTypar gp.Name m) 

        let tptys = tps |> List.map mkTyparTy
        let importInst = tinst@tptys
        (tps, gps) ||> List.iter2 (fun tp gp -> 
            let constraints = gp.Constraints |> List.map (fun ilty -> TyparConstraint.CoercesTo(ImportILType amap m importInst (rescopeILType scoref ilty), m) )
            let constraints = if gp.HasReferenceTypeConstraint then (TyparConstraint.IsReferenceType(m) :: constraints) else constraints
            let constraints = if gp.HasNotNullableValueTypeConstraint then (TyparConstraint.IsNonNullableStruct(m) :: constraints) else constraints
            let constraints = if gp.HasDefaultConstructorConstraint then (TyparConstraint.RequiresDefaultConstructor(m) :: constraints) else constraints
            tp.SetConstraints constraints)
        tps

/// Given a list of items each keyed by an ordered list of keys, apply 'nodef' to the each group
/// with the same leading key. Apply 'tipf' to the elements where the keylist is empty, and return 
/// the overall results.  Used to bucket types, so System.Char and System.Collections.Generic.List 
/// both get initially bucketed under 'System'.
let multisetDiscriminateAndMap nodef tipf (items: ('Key list * 'Value) list) = 
    // Find all the items with an empty key list and call 'tipf' 
    let tips = 
        [ for (keylist, v) in items do 
             match keylist with 
             | [] -> yield tipf v
             | _ -> () ]

    // Find all the items with a non-empty key list. Bucket them together by
    // the first key. For each bucket, call 'nodef' on that head key and the bucket.
    let nodes = 
        let buckets = new Dictionary<_, _>(10)
        for (keylist, v) in items do
            match keylist with 
            | [] -> ()
            | key :: rest ->
                buckets.[key] <-
                    match buckets.TryGetValue key with
                    | true, b -> (rest, v) :: b
                    | _ -> (rest, v) :: []

        [ for (KeyValue(key, items)) in buckets -> nodef key items ]

    tips @ nodes

/// Import an IL type definition as a new F# TAST Entity node.
let rec ImportILTypeDef amap m scoref (cpath: CompilationPath) enc nm (tdef: ILTypeDef)  =
    let lazyModuleOrNamespaceTypeForNestedTypes = 
        lazy 
            let cpath = cpath.NestedCompPath nm ModuleOrType
            ImportILTypeDefs amap m scoref cpath (enc@[tdef]) tdef.NestedTypes
    // Add the type itself. 
    Construct.NewILTycon 
        (Some cpath) 
        (nm, m) 
        // The read of the type parameters may fail to resolve types. We pick up a new range from the point where that read is forced
        // Make sure we reraise the original exception one occurs - see findOriginalException.
        (LazyWithContext.Create((fun m -> ImportILGenericParameters amap m scoref [] tdef.GenericParams), ErrorLogger.findOriginalException))
        (scoref, enc, tdef) 
        (MaybeLazy.Lazy lazyModuleOrNamespaceTypeForNestedTypes)
       

/// Import a list of (possibly nested) IL types as a new ModuleOrNamespaceType node
/// containing new entities, bucketing by namespace along the way.
and ImportILTypeDefList amap m (cpath: CompilationPath) enc items =
    // Split into the ones with namespaces and without. Add the ones with namespaces in buckets.
    // That is, discriminate based in the first element of the namespace list (e.g. "System") 
    // and, for each bag, fold-in a lazy computation to add the types under that bag .
    //
    // nodef - called for each bucket, where 'n' is the head element of the namespace used
    // as a key in the discrimination, tgs is the remaining descriptors.  We create an entity for 'n'.
    //
    // tipf - called if there are no namespace items left to discriminate on. 
    let entities = 
        items 
        |> multisetDiscriminateAndMap 
            (fun n tgs ->
                let modty = lazy (ImportILTypeDefList amap m (cpath.NestedCompPath n Namespace) enc tgs)
                Construct.NewModuleOrNamespace (Some cpath) taccessPublic (mkSynId m n) XmlDoc.Empty [] (MaybeLazy.Lazy modty))
            (fun (n, info: Lazy<_>) -> 
                let (scoref2, lazyTypeDef: ILPreTypeDef) = info.Force()
                ImportILTypeDef amap m scoref2 cpath enc n (lazyTypeDef.GetTypeDef()))

    let kind = match enc with [] -> Namespace | _ -> ModuleOrType
    Construct.NewModuleOrNamespaceType kind entities []
      
/// Import a table of IL types as a ModuleOrNamespaceType.
///
and ImportILTypeDefs amap m scoref cpath enc (tdefs: ILTypeDefs) =
    // We be very careful not to force a read of the type defs here
    tdefs.AsArrayOfPreTypeDefs
    |> Array.map (fun pre -> (pre.Namespace, (pre.Name, notlazy(scoref, pre))))
    |> Array.toList
    |> ImportILTypeDefList amap m cpath enc

/// Import the main type definitions in an IL assembly.
///
/// Example: for a collection of types "System.Char", "System.Int32" and "Library.C"
/// the return ModuleOrNamespaceType will contain namespace entities for "System" and "Library", which in turn contain
/// type definition entities for ["Char"; "Int32"]  and ["C"] respectively.  
let ImportILAssemblyMainTypeDefs amap m scoref modul = 
    modul.TypeDefs |> ImportILTypeDefs amap m scoref (CompPath(scoref, [])) [] 

/// Import the "exported types" table for multi-module assemblies. 
let ImportILAssemblyExportedType amap m auxModLoader (scoref: ILScopeRef) (exportedType: ILExportedTypeOrForwarder) = 
    // Forwarders are dealt with separately in the ref->def dereferencing logic in tast.fs as they effectively give rise to type equivalences
    if exportedType.IsForwarder then 
        []
    else
        let ns, n = splitILTypeName exportedType.Name
        let info = 
            lazy (match 
                    (try 
                        let modul = auxModLoader exportedType.ScopeRef
                        let ptd = mkILPreTypeDefComputed (ns, n, (fun () -> modul.TypeDefs.FindByName exportedType.Name))
                        Some ptd
                     with :? KeyNotFoundException -> None)
                    with 
                  | None -> 
                     error(Error(FSComp.SR.impReferenceToDllRequiredByAssembly(exportedType.ScopeRef.QualifiedName, scoref.QualifiedName, exportedType.Name), m))
                  | Some preTypeDef -> 
                     scoref, preTypeDef)
              
        [ ImportILTypeDefList amap m (CompPath(scoref, [])) [] [(ns, (n, info))]  ]

/// Import the "exported types" table for multi-module assemblies. 
let ImportILAssemblyExportedTypes amap m auxModLoader scoref (exportedTypes: ILExportedTypesAndForwarders) = 
    [ for exportedType in exportedTypes.AsList do 
         yield! ImportILAssemblyExportedType amap m auxModLoader scoref exportedType ]

/// Import both the main type definitions and the "exported types" table, i.e. all the 
/// types defined in an IL assembly.
let ImportILAssemblyTypeDefs (amap, m, auxModLoader, aref, mainmod: ILModuleDef) = 
    let scoref = ILScopeRef.Assembly aref
    let mtypsForExportedTypes = ImportILAssemblyExportedTypes amap m auxModLoader scoref mainmod.ManifestOfAssembly.ExportedTypes
    let mainmod = ImportILAssemblyMainTypeDefs amap m scoref mainmod
    CombineCcuContentFragments m (mainmod :: mtypsForExportedTypes)

/// Import the type forwarder table for an IL assembly
let ImportILAssemblyTypeForwarders (amap, m, exportedTypes: ILExportedTypesAndForwarders) = 
    // Note 'td' may be in another module or another assembly!
    // Note: it is very important that we call auxModLoader lazily
    [ //printfn "reading forwarders..." 
        for exportedType in exportedTypes.AsList do 
            let ns, n = splitILTypeName exportedType.Name
            //printfn "found forwarder for %s..." n
            let tcref = lazy ImportILTypeRefUncached (amap()) m (ILTypeRef.Create(exportedType.ScopeRef, [], exportedType.Name))
            yield (Array.ofList ns, n), tcref
            let rec nested (nets: ILNestedExportedTypes) enc = 
                [ for net in nets.AsList do 
                    
                    //printfn "found nested forwarder for %s..." net.Name
                    let tcref = lazy ImportILTypeRefUncached (amap()) m (ILTypeRef.Create (exportedType.ScopeRef, enc, net.Name))
                    yield (Array.ofList enc, exportedType.Name), tcref 
                    yield! nested net.Nested (enc @ [ net.Name ]) ]
            yield! nested exportedType.Nested (ns@[n]) 
    ] |> Map.ofList

/// Import an IL assembly as a new TAST CCU
let ImportILAssembly(amap: (unit -> ImportMap), m, auxModuleLoader, xmlDocInfoLoader: IXmlDocumentationInfoLoader option, ilScopeRef, sourceDir, filename, ilModule: ILModuleDef, invalidateCcu: IEvent<string>) = 
    invalidateCcu |> ignore
    let aref =   
        match ilScopeRef with 
        | ILScopeRef.Assembly aref -> aref 
        | _ -> error(InternalError("ImportILAssembly: cannot reference .NET netmodules directly, reference the containing assembly instead", m))
    let nm = aref.Name
    let mty = ImportILAssemblyTypeDefs(amap, m, auxModuleLoader, aref, ilModule)
    let forwarders = 
        match ilModule.Manifest with 
        | None -> Map.empty
        | Some manifest -> ImportILAssemblyTypeForwarders(amap, m, manifest.ExportedTypes)

    let ccuData: CcuData = 
        { IsFSharp=false
          UsesFSharp20PlusQuotations=false
#if !NO_EXTENSIONTYPING
          InvalidateEvent=invalidateCcu
          IsProviderGenerated = false
          ImportProvidedType = (fun ty -> ImportProvidedType (amap()) m ty)
#endif
          QualifiedName= Some ilScopeRef.QualifiedName
          Contents = Construct.NewCcuContents ilScopeRef m nm mty 
          ILScopeRef = ilScopeRef
          Stamp = newStamp()
          SourceCodeDirectory = sourceDir  // note: not an accurate value, but IL assemblies don't give us this information in any attributes. 
          FileName = filename
          MemberSignatureEquality= (fun ty1 ty2 -> typeEquivAux EraseAll (amap()).g ty1 ty2)
          TryGetILModuleDef = (fun () -> Some ilModule)
          TypeForwarders = forwarders
          XmlDocumentationInfo = 
            match xmlDocInfoLoader, filename with
            | Some xmlDocInfoLoader, Some filename -> xmlDocInfoLoader.TryLoad(filename, ilModule)
            | _ -> None
        }
                
    CcuThunk.Create(nm, ccuData)
