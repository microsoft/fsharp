// Copyright (c) Microsoft Corporation. All Rights Reserved. See License.txt in the project root for license information.

module FSharp.Compiler.AbstractIL.ILBinaryReader

open System
open System.IO
open System.Linq
open System.Collections.Generic
open System.Collections.Immutable
open System.Collections.ObjectModel
open System.Collections.Concurrent
open System.Reflection
open System.Reflection.PortableExecutable
open System.Reflection.Metadata
open System.Reflection.Metadata.Ecma335
open FSharp.NativeInterop
open FSharp.Compiler.Lib
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AbstractIL.Internal
open FSharp.Compiler.AbstractIL.Internal.Library
open FSharp.Compiler.AbstractIL.Internal.BinaryConstants
open Internal.Utilities.Collections

#nowarn "9"

type ILReaderMetadataSnapshot = (obj * nativeint * int) 
type ILReaderTryGetMetadataSnapshot = (* path: *) string * (* snapshotTimeStamp: *) System.DateTime -> ILReaderMetadataSnapshot option

[<RequireQualifiedAccess>]
type MetadataOnlyFlag = Yes | No

[<RequireQualifiedAccess>]
type ReduceMemoryFlag = Yes | No

[<AutoOpen>]
module rec ILBinaryReaderImpl =

    let primaryAssemblyILGlobals = mkILGlobals (ILScopeRef.PrimaryAssembly, [])

    type OperandType = System.Reflection.Emit.OperandType

    type PdbReaderProvider = MetadataReaderProvider * string

    let parseTyparCount (nm: string) =
        let index = nm.IndexOf('`')
        if index < 0 || (index + 1) < nm.Length then 0
        else Int32.Parse(nm.Substring(index + 1)) // REVIEW: Maybe use Span here?

    type MethodTypeVarOffset = int

    [<Sealed>]
    type cenv(
                peReaderOpt: PEReader option, // only set when reading full PE including code etc. for static linking
                mdReader: MetadataReader,
                pdbReaderProviderOpt: PdbReaderProvider option,
                entryPointToken: int,
                canReduceMemory: bool,
                sigTyProvider: ISignatureTypeProvider<ILType, MethodTypeVarOffset>,
                localSigTyProvider: ISignatureTypeProvider<ILLocal, MethodTypeVarOffset>) =

        let typeDefCache = ConcurrentDictionary()
        let typeRefCache = ConcurrentDictionary()
        let asmRefCache = ConcurrentDictionary()
        let methDefToILMethSpecCache = ConcurrentDictionary()
        let methDefCache = ConcurrentDictionary()
        let stringCache = ConcurrentDictionary()

        let isCachingEnabled = not canReduceMemory

        member _.CanReduceMemory = canReduceMemory

        member _.TryPEReader = peReaderOpt

        member _.MetadataReader = mdReader

        member _.PdbReaderProvider = pdbReaderProviderOpt

        member _.EntryPointToken = entryPointToken

        member _.SignatureTypeProvider = sigTyProvider

        member _.LocalSignatureTypeProvider = localSigTyProvider

        member _.CacheILType(key: struct(TypeDefinitionHandle * SignatureTypeKind), ilType: ILType) =
            if isCachingEnabled then
                typeDefCache.[key] <- ilType

        member _.CacheILType(key: struct(TypeReferenceHandle * SignatureTypeKind), ilType: ILType) =
            if isCachingEnabled then
                typeRefCache.[key] <- ilType

        member _.CacheILAssemblyRef(key: AssemblyReferenceHandle, ilAsmRef: ILAssemblyRef) =
            asmRefCache.[key] <- ilAsmRef

        member _.CacheILMethodSpec(key: MethodDefinitionHandle, ilMethSpec: ILMethodSpec) =
            if isCachingEnabled then
                methDefToILMethSpecCache.[key] <- ilMethSpec

        member _.CacheILMethodDef(key: MethodDefinitionHandle, ilMethDef: ILMethodDef) =
            if isCachingEnabled then
                methDefCache.[key] <- ilMethDef

        member _.CacheString(key: StringHandle, str: string) =
            stringCache.[key] <- str
        
        member _.TryGetCachedILType(key) =
            match typeDefCache.TryGetValue(key) with
            | true, ilType -> ValueSome(ilType)
            | _ -> ValueNone

        member _.TryGetCachedILType(key) =
            match typeRefCache.TryGetValue(key) with
            | true, ilType -> ValueSome(ilType)
            | _ -> ValueNone

        member _.TryGetCachedILAssemblyRef(key: AssemblyReferenceHandle) =
            match asmRefCache.TryGetValue(key) with
            | true, ilAsmRef -> ValueSome(ilAsmRef)
            | _ -> ValueNone

        member _.TryGetCachedILMethodSpec(key) =
            match methDefToILMethSpecCache.TryGetValue(key) with
            | true, ilMethSpec -> ValueSome(ilMethSpec)
            | _ -> ValueNone

        member _.TryGetCachedILMethodDef key =
            match methDefCache.TryGetValue key with
            | true, ilMethDef -> ValueSome ilMethDef
            | _ -> ValueNone

        member _.TryGetCachedString key =
            match stringCache.TryGetValue key with
            | true, str -> ValueSome str
            | _ -> ValueNone

    let mkILVersionInfo (v: Version) =
        ILVersionInfo(uint16 v.Major, uint16 v.Minor, uint16 v.Build, uint16 v.Revision)
    
    let mkILMemberAccess (attributes: TypeAttributes) =
        let attributes = attributes &&& TypeAttributes.VisibilityMask
        match attributes with
        | TypeAttributes.Public -> ILMemberAccess.Public
        | TypeAttributes.NestedPublic -> ILMemberAccess.Public
        | TypeAttributes.NestedPrivate -> ILMemberAccess.Private
        | TypeAttributes.NestedFamily -> ILMemberAccess.Family
        | TypeAttributes.NestedAssembly -> ILMemberAccess.Assembly
        | TypeAttributes.NestedFamANDAssem -> ILMemberAccess.FamilyAndAssembly
        | TypeAttributes.NestedFamORAssem -> ILMemberAccess.FamilyOrAssembly
        | _ -> ILMemberAccess.Private

    let mkILTypeDefAccess (attributes: TypeAttributes) =
        let attributes = attributes &&& TypeAttributes.VisibilityMask
        match attributes with
        | TypeAttributes.Public -> ILTypeDefAccess.Public
        | TypeAttributes.NestedPublic
        | TypeAttributes.NestedPrivate
        | TypeAttributes.NestedFamily
        | TypeAttributes.NestedAssembly
        | TypeAttributes.NestedFamANDAssem
        | TypeAttributes.NestedFamORAssem -> ILTypeDefAccess.Nested (mkILMemberAccess attributes)
        | _ -> ILTypeDefAccess.Private

    let mkILAssemblyLongevity (flags: AssemblyFlags) =
        let  masked = int flags &&& 0x000e
        if   masked = 0x0000 then ILAssemblyLongevity.Unspecified
        elif masked = 0x0002 then ILAssemblyLongevity.Library
        elif masked = 0x0004 then ILAssemblyLongevity.PlatformAppDomain
        elif masked = 0x0006 then ILAssemblyLongevity.PlatformProcess
        elif masked = 0x0008 then ILAssemblyLongevity.PlatformSystem
        else                      ILAssemblyLongevity.Unspecified

    let mkILTypeDefLayoutInfo (layout: TypeLayout) =
        if layout.IsDefault then
            { Size = None; Pack = None }
        else
            { Size = Some(layout.Size); Pack = Some(uint16 layout.PackingSize) }

    let mkILTypeDefLayout (attributes: TypeAttributes) (layout: TypeLayout) =
        match attributes &&& TypeAttributes.LayoutMask with
        | TypeAttributes.SequentialLayout ->
            ILTypeDefLayout.Sequential(mkILTypeDefLayoutInfo layout)
        | TypeAttributes.ExplicitLayout ->
            ILTypeDefLayout.Explicit(mkILTypeDefLayoutInfo layout)
        | _ ->
            ILTypeDefLayout.Auto

    let mkILSecurityAction (declSecurityAction: DeclarativeSecurityAction) =
        match declSecurityAction with
        | DeclarativeSecurityAction.Demand -> ILSecurityAction.Demand
        | DeclarativeSecurityAction.Assert -> ILSecurityAction.Assert
        | DeclarativeSecurityAction.Deny -> ILSecurityAction.Deny
        | DeclarativeSecurityAction.PermitOnly -> ILSecurityAction.PermitOnly
        | DeclarativeSecurityAction.LinkDemand -> ILSecurityAction.LinkCheck
        | DeclarativeSecurityAction.InheritanceDemand -> ILSecurityAction.InheritCheck
        | DeclarativeSecurityAction.RequestMinimum -> ILSecurityAction.ReqMin
        | DeclarativeSecurityAction.RequestOptional -> ILSecurityAction.ReqOpt
        | DeclarativeSecurityAction.RequestRefuse -> ILSecurityAction.ReqRefuse
        | _ ->
            // Comment below is from System.Reflection.Metadata
            // Wait for an actual need before exposing these. They all have ilasm keywords, but some are missing from the CLI spec and 
            // and none are defined in System.Security.Permissions.SecurityAction.
            //Request = 0x0001,
            //PrejitGrant = 0x000B,
            //PrejitDeny = 0x000C,
            //NonCasDemand = 0x000D,
            //NonCasLinkDemand = 0x000E,
            //NonCasInheritanceDemand = 0x000F,
            match int declSecurityAction with
            | 0x0001 -> ILSecurityAction.Request
            | 0x000b -> ILSecurityAction.PreJitGrant
            | 0x000c -> ILSecurityAction.PreJitDeny
            | 0x000d -> ILSecurityAction.NonCasDemand
            | 0x000e -> ILSecurityAction.NonCasLinkDemand
            | 0x000f -> ILSecurityAction.NonCasInheritance
            | 0x0010 -> ILSecurityAction.LinkDemandChoice
            | 0x0011 -> ILSecurityAction.InheritanceDemandChoice
            | 0x0012 -> ILSecurityAction.DemandChoice
            | x -> failwithf "Invalid DeclarativeSecurityAction: %i" x

    let mkILThisConvention (sigHeader: SignatureHeader) =
        if sigHeader.Attributes &&& SignatureAttributes.Instance = SignatureAttributes.Instance then
            ILThisConvention.Instance
        elif sigHeader.Attributes &&& SignatureAttributes.ExplicitThis = SignatureAttributes.ExplicitThis then
            ILThisConvention.InstanceExplicit
        else
            ILThisConvention.Static

    let mkILCallingConv (sigHeader: SignatureHeader) =
        let ilThisConvention = mkILThisConvention sigHeader

        let ilArgConvention =
            match sigHeader.CallingConvention with
            | SignatureCallingConvention.Default -> ILArgConvention.Default
            | SignatureCallingConvention.CDecl -> ILArgConvention.CDecl
            | SignatureCallingConvention.StdCall -> ILArgConvention.StdCall
            | SignatureCallingConvention.ThisCall -> ILArgConvention.ThisCall
            | SignatureCallingConvention.FastCall -> ILArgConvention.FastCall
            | SignatureCallingConvention.VarArgs -> ILArgConvention.VarArg
            | _ -> failwithf "Invalid Signature Calling Convention: %A" sigHeader.CallingConvention

        // Optimize allocations.
        if ilThisConvention = ILThisConvention.Instance && ilArgConvention = ILArgConvention.Default then
            ILCallingConv.Instance
        elif ilThisConvention = ILThisConvention.Static && ilArgConvention = ILArgConvention.Default then
            ILCallingConv.Static
        else
            ILCallingConv.Callconv(ilThisConvention, ilArgConvention)

    let mkPInvokeCallingConvention (methImportAttributes: MethodImportAttributes) =
        match methImportAttributes &&& MethodImportAttributes.CallingConventionMask with
        | MethodImportAttributes.CallingConventionCDecl ->
            PInvokeCallingConvention.Cdecl
        | MethodImportAttributes.CallingConventionStdCall ->
            PInvokeCallingConvention.Stdcall
        | MethodImportAttributes.CallingConventionThisCall ->
            PInvokeCallingConvention.Thiscall
        | MethodImportAttributes.CallingConventionFastCall ->
            PInvokeCallingConvention.Fastcall
        | MethodImportAttributes.CallingConventionWinApi ->
            PInvokeCallingConvention.WinApi
        | _ ->
            PInvokeCallingConvention.None

    let mkPInvokeCharEncoding (methImportAttributes: MethodImportAttributes) =
        match methImportAttributes &&& MethodImportAttributes.CharSetMask with
        | MethodImportAttributes.CharSetAnsi ->
            PInvokeCharEncoding.Ansi
        | MethodImportAttributes.CharSetUnicode ->
            PInvokeCharEncoding.Unicode
        | MethodImportAttributes.CharSetAuto ->
            PInvokeCharEncoding.Auto
        | _ ->
            PInvokeCharEncoding.None

    let mkPInvokeThrowOnUnmappableChar (methImportAttrs: MethodImportAttributes) =
        match methImportAttrs &&& MethodImportAttributes.ThrowOnUnmappableCharMask with
        | MethodImportAttributes.ThrowOnUnmappableCharEnable ->
            PInvokeThrowOnUnmappableChar.Enabled
        | MethodImportAttributes.ThrowOnUnmappableCharDisable ->
            PInvokeThrowOnUnmappableChar.Disabled
        | _ -> 
            PInvokeThrowOnUnmappableChar.UseAssembly

    let mkPInvokeCharBestFit (methImportAttrs: MethodImportAttributes) =
        match methImportAttrs &&& MethodImportAttributes.BestFitMappingMask with
        | MethodImportAttributes.BestFitMappingEnable ->
            PInvokeCharBestFit.Enabled
        | MethodImportAttributes.BestFitMappingDisable ->
            PInvokeCharBestFit.Disabled
        | _ ->
            PInvokeCharBestFit.UseAssembly

    let mkILTypeFunctionPointer (sigHeader: SignatureHeader) argTypes returnType =
        let callingSig =
            {
                CallingConv = mkILCallingConv sigHeader
                ArgTypes = argTypes
                ReturnType = returnType
            }
        ILType.FunctionPointer(callingSig)

    let mkILTypeModified isRequired typeRef unmodifiedType =
        ILType.Modified(isRequired, typeRef, unmodifiedType)
        
    let mkILTypePrimitive (primitiveTypeCode: PrimitiveTypeCode) =
        match primitiveTypeCode with
        | PrimitiveTypeCode.Boolean -> primaryAssemblyILGlobals.typ_Bool
        | PrimitiveTypeCode.Byte -> primaryAssemblyILGlobals.typ_Byte
        | PrimitiveTypeCode.Char -> primaryAssemblyILGlobals.typ_Char
        | PrimitiveTypeCode.Double -> primaryAssemblyILGlobals.typ_Double
        | PrimitiveTypeCode.Int16 -> primaryAssemblyILGlobals.typ_Int16
        | PrimitiveTypeCode.Int32 -> primaryAssemblyILGlobals.typ_Int32
        | PrimitiveTypeCode.Int64 -> primaryAssemblyILGlobals.typ_Int64
        | PrimitiveTypeCode.IntPtr -> primaryAssemblyILGlobals.typ_IntPtr
        | PrimitiveTypeCode.Object -> primaryAssemblyILGlobals.typ_Object
        | PrimitiveTypeCode.SByte -> primaryAssemblyILGlobals.typ_SByte
        | PrimitiveTypeCode.Single -> primaryAssemblyILGlobals.typ_Single
        | PrimitiveTypeCode.String -> primaryAssemblyILGlobals.typ_String
        | PrimitiveTypeCode.TypedReference -> primaryAssemblyILGlobals.typ_TypedReference
        | PrimitiveTypeCode.UInt16 -> primaryAssemblyILGlobals.typ_UInt16
        | PrimitiveTypeCode.UInt32 -> primaryAssemblyILGlobals.typ_UInt32
        | PrimitiveTypeCode.UInt64 -> primaryAssemblyILGlobals.typ_UInt64
        | PrimitiveTypeCode.UIntPtr -> primaryAssemblyILGlobals.typ_UIntPtr
        | PrimitiveTypeCode.Void -> ILType.Void
        | _ -> failwithf "Invalid Primitive Type Code: %A" primitiveTypeCode

    let mkILGenericArgsByCount typarOffset count : ILGenericArgs =
        List.init count (fun i -> mkILTyvarTy (uint16 (typarOffset + i)))

    let mkILTypeGeneric typeRef boxity typeArgs =
        let ilTypeSpec = ILTypeSpec.Create(typeRef, typeArgs)
        mkILTy boxity ilTypeSpec

    let mkILTypeArray elementType (shape: ArrayShape) =
        let lowerBounds = shape.LowerBounds
        let sizes = shape.Sizes
        let rank = shape.Rank
        let shape = 
            let dim i =
              (if i < lowerBounds.Length then Some (Seq.item i lowerBounds) else None), 
              (if i < sizes.Length then Some (Seq.item i sizes) else None)
            ILArrayShape (List.init rank dim)
        mkILArrTy (elementType, shape)

    type SignatureTypeProvider() =

        member val cenv : cenv = Unchecked.defaultof<_> with get, set

        interface ISignatureTypeProvider<ILType, MethodTypeVarOffset> with

            member _.GetFunctionPointerType si =
                mkILTypeFunctionPointer si.Header (si.ParameterTypes |> Seq.toList) si.ReturnType

            member _.GetGenericMethodParameter(typarOffset, index) =
                mkILTyvarTy (uint16 (typarOffset + index))

            member _.GetGenericTypeParameter(_, index) =
                mkILTyvarTy (uint16 (index))

            member _.GetModifiedType(modifier, unmodifiedType, isRequired) =
                mkILTypeModified isRequired modifier.TypeRef unmodifiedType

            member _.GetPinnedType elementType = elementType

            member this.GetTypeFromSpecification(_, typarOffset, typeSpecHandle, _) =
                readILTypeFromTypeSpecification this.cenv typarOffset typeSpecHandle
            
        interface ISimpleTypeProvider<ILType> with

            member _.GetPrimitiveType typeCode =
                mkILTypePrimitive typeCode
            
            member this.GetTypeFromDefinition(_, typeDefHandle, rawTypeKind) =
                readILTypeFromTypeDefinition this.cenv (LanguagePrimitives.EnumOfValue rawTypeKind) typeDefHandle

            member this.GetTypeFromReference(_, typeRefHandle, rawTypeKind) =
                readILTypeFromTypeReference this.cenv (LanguagePrimitives.EnumOfValue rawTypeKind) typeRefHandle

        interface IConstructedTypeProvider<ILType> with

            member _.GetGenericInstantiation(genericType, typeArgs) =
                mkILTypeGeneric genericType.TypeRef genericType.Boxity (typeArgs |> List.ofSeq)

            member _.GetArrayType(elementType, shape) =
                mkILTypeArray elementType shape

            member _.GetByReferenceType elementType =
                ILType.Byref(elementType)

            member _.GetPointerType elementType =
                ILType.Ptr(elementType)

        interface ISZArrayTypeProvider<ILType> with

            member _.GetSZArrayType elementType =
                mkILArr1DTy elementType

        interface ICustomAttributeTypeProvider<ILType> with

            member _.GetSystemType() = primaryAssemblyILGlobals.typ_Type
            
            member _.GetTypeFromSerializedName nm = ILType.Parse nm

            member _.GetUnderlyingEnumType ilType = 
                if isILSByteTy primaryAssemblyILGlobals ilType then PrimitiveTypeCode.SByte
                elif isILByteTy primaryAssemblyILGlobals ilType then PrimitiveTypeCode.Byte
                elif isILInt16Ty primaryAssemblyILGlobals ilType then PrimitiveTypeCode.Int16
                elif isILUInt16Ty primaryAssemblyILGlobals ilType then PrimitiveTypeCode.UInt16
                elif isILInt32Ty primaryAssemblyILGlobals ilType then PrimitiveTypeCode.Int32
                elif isILUInt32Ty primaryAssemblyILGlobals ilType then PrimitiveTypeCode.UInt32
                elif isILInt64Ty primaryAssemblyILGlobals ilType then PrimitiveTypeCode.Int64
                elif isILUInt64Ty primaryAssemblyILGlobals ilType then PrimitiveTypeCode.UInt64
                elif isILCharTy primaryAssemblyILGlobals ilType then PrimitiveTypeCode.Char
                elif isILDoubleTy primaryAssemblyILGlobals ilType then PrimitiveTypeCode.Double
                elif isILSingleTy primaryAssemblyILGlobals ilType then PrimitiveTypeCode.Single
                elif isILBoolTy primaryAssemblyILGlobals ilType then PrimitiveTypeCode.Boolean
                else
                    failwith "GetUnderlyingEnumType: Invalid type"

            member _.IsSystemType ilType = isILTypeTy primaryAssemblyILGlobals ilType

    type LocalSignatureTypeProvider() =

        member val cenv : cenv = Unchecked.defaultof<_> with get, set

        interface ISignatureTypeProvider<ILLocal, MethodTypeVarOffset> with

            member _.GetFunctionPointerType si =
                {
                    IsPinned = false
                    Type = mkILTypeFunctionPointer si.Header (si.ParameterTypes |> Seq.map (fun x -> x.Type) |> Seq.toList) si.ReturnType.Type
                    DebugInfo = None
                }

            member _.GetGenericMethodParameter(typarOffset, index) =
                {
                    IsPinned = false
                    Type = mkILTyvarTy (uint16 (typarOffset + index))
                    DebugInfo = None
                }

            member _.GetGenericTypeParameter(_, index) =
                {
                    IsPinned = false
                    Type = mkILTyvarTy (uint16 (index))
                    DebugInfo = None
                }

            member _.GetModifiedType(modifier, unmodifiedType, isRequired) =
                {
                    IsPinned = false
                    Type = mkILTypeModified isRequired modifier.Type.TypeRef unmodifiedType.Type
                    DebugInfo = None
                }

            member _.GetPinnedType elementType =
                {
                    IsPinned = true
                    Type = elementType.Type
                    DebugInfo = None
                }

            member this.GetTypeFromSpecification(_, typarOffset, typeSpecHandle, _) =
                {
                    IsPinned = false
                    Type = readILTypeFromTypeSpecification this.cenv typarOffset typeSpecHandle
                    DebugInfo = None
                }
            
        interface ISimpleTypeProvider<ILLocal> with

            member _.GetPrimitiveType typeCode =
                {
                    IsPinned = false
                    Type = mkILTypePrimitive typeCode
                    DebugInfo = None
                }
            
            member this.GetTypeFromDefinition(_, typeDefHandle, rawTypeKind) =
                {
                    IsPinned = false
                    Type = readILTypeFromTypeDefinition this.cenv (LanguagePrimitives.EnumOfValue rawTypeKind) typeDefHandle
                    DebugInfo = None
                }    

            member this.GetTypeFromReference(_, typeRefHandle, rawTypeKind) =
                {
                    IsPinned = false
                    Type = readILTypeFromTypeReference this.cenv (LanguagePrimitives.EnumOfValue rawTypeKind) typeRefHandle
                    DebugInfo = None
                } 
            
        interface IConstructedTypeProvider<ILLocal> with

            member _.GetGenericInstantiation(genericType, typeArgs) =
                {
                    IsPinned = false
                    Type = mkILTypeGeneric genericType.Type.TypeRef genericType.Type.Boxity (typeArgs |> Seq.map (fun x -> x.Type) |> List.ofSeq)
                    DebugInfo = None
                }

            member _.GetArrayType(elementType, shape) =
                {
                    IsPinned = false
                    Type = mkILTypeArray elementType.Type shape
                    DebugInfo = None
                }

            member _.GetByReferenceType elementType =
                {
                    IsPinned = false
                    Type = ILType.Byref(elementType.Type)
                    DebugInfo = None
                }

            member _.GetPointerType elementType =
                {
                    IsPinned = false
                    Type = ILType.Ptr(elementType.Type)
                    DebugInfo = None
                }

        interface ISZArrayTypeProvider<ILLocal> with

            member _.GetSZArrayType elementType =
                {
                    IsPinned = false
                    Type =  mkILArr1DTy elementType.Type
                    DebugInfo = None
                }

    let rec seekCountUtf8String offset length (addr: nativeptr<byte>) n = 
        if offset >= length then
            failwith "Unable to read string from metadata."

        let c = NativePtr.read addr |> int
        if c = 0 then n 
        else seekCountUtf8String (offset + 1) length (NativePtr.add addr 1) (n + 1)
    
    let seekReadUTF8String offset length addr = 
        let count = seekCountUtf8String offset length addr 0
        if count = 0 then
            String.Empty
        else
            System.Text.Encoding.UTF8.GetString(addr, count)
                
    let readString (cenv: cenv) (stringHandle: StringHandle) =
        if stringHandle.IsNil then
            String.Empty
        else
            match cenv.TryGetCachedString stringHandle with
            | ValueSome str -> str
            | _ ->
                let str = cenv.MetadataReader.GetString(stringHandle)
                cenv.CacheString(stringHandle, str)
                str

    let readTypeName (cenv: cenv) (namespaceHandle: StringHandle) (nameHandle: StringHandle) =
        let name = readString cenv nameHandle
        if namespaceHandle.IsNil then name
        else readString cenv namespaceHandle + "." + name

    let rec readILScopeRef (cenv: cenv) (handle: EntityHandle) =
        let mdReader = cenv.MetadataReader

        match handle.Kind with
        | HandleKind.AssemblyFile ->
            let asmFile = mdReader.GetAssemblyFile(AssemblyFileHandle.op_Explicit handle)
            ILScopeRef.Module(readILModuleRefFromAssemblyFile cenv asmFile)

        | HandleKind.AssemblyReference ->
            ILScopeRef.Assembly(readILAssemblyRefFromAssemblyReference cenv (AssemblyReferenceHandle.op_Explicit(handle)))

        | HandleKind.ModuleReference ->
            let modRef = mdReader.GetModuleReference(ModuleReferenceHandle.op_Explicit handle)
            ILScopeRef.Module(readILModuleRefFromModuleReference cenv modRef)

        | HandleKind.TypeReference ->
            let typeRef = mdReader.GetTypeReference(TypeReferenceHandle.op_Explicit handle)
            readILScopeRef cenv typeRef.ResolutionScope

        | HandleKind.ModuleDefinition ->
            ILScopeRef.Local

        | _ ->
            failwithf "Invalid Handle Kind: %A" handle.Kind

    let readILAssemblyRefFromAssemblyReferenceUncached (cenv: cenv) (asmRefHandle: AssemblyReferenceHandle) =
        let mdReader = cenv.MetadataReader

        let asmRef = mdReader.GetAssemblyReference(asmRefHandle)
        let name = readString cenv asmRef.Name
        let flags = asmRef.Flags

        let hash = 
            let hashValue = asmRef.HashValue
            if hashValue.IsNil then None
            else Some(mdReader.GetBlobBytes(hashValue))

        let publicKey =
            if asmRef.PublicKeyOrToken.IsNil then None
            else 
                let bytes = mdReader.GetBlobBytes(asmRef.PublicKeyOrToken)
                let publicKey = 
                    if int (flags &&& AssemblyFlags.PublicKey) <> 0 then
                        PublicKey(bytes)
                    else
                        PublicKeyToken(bytes)
                Some(publicKey)

        let retargetable = int (flags &&& AssemblyFlags.Retargetable) <> 0

        let version = mkILVersionInfo asmRef.Version

        let locale =
            let locale = readString cenv asmRef.Culture
            if String.IsNullOrWhiteSpace(locale) then None
            else Some(locale)

        ILAssemblyRef.Create(name, hash, publicKey, retargetable, Some version, locale)

    let readILAssemblyRefFromAssemblyReference (cenv: cenv) (asmRefHandle: AssemblyReferenceHandle) =
        match cenv.TryGetCachedILAssemblyRef(asmRefHandle) with
        | ValueSome(ilAsmRef) -> ilAsmRef
        | _ ->
            let ilAsmRef = readILAssemblyRefFromAssemblyReferenceUncached cenv asmRefHandle
            cenv.CacheILAssemblyRef(asmRefHandle, ilAsmRef)
            ilAsmRef

    let readILModuleRefFromAssemblyFile (cenv: cenv) (asmFile: AssemblyFile) =
        let mdReader = cenv.MetadataReader

        let name = readString cenv asmFile.Name

        let hash = 
            let hashValue = asmFile.HashValue
            if hashValue.IsNil then None
            else Some(mdReader.GetBlobBytes(hashValue))

        ILModuleRef.Create(name, asmFile.ContainsMetadata, hash)

    let readDeclaringTypeGenericCountFromMethodDefinition (cenv: cenv) (methDef: MethodDefinition) =
        let mdReader = cenv.MetadataReader

        let typeDef = mdReader.GetTypeDefinition(methDef.GetDeclaringType())
        typeDef.GetGenericParameters().Count
        
    let readILGenericArgs (cenv: cenv) (entityHandle: EntityHandle) : ILGenericArgs =
        let mdReader = cenv.MetadataReader

        match entityHandle.Kind with
        | HandleKind.TypeDefinition ->
            let typeDef = mdReader.GetTypeDefinition(TypeDefinitionHandle.op_Explicit entityHandle)
            let typarCount = typeDef.GetGenericParameters().Count
            mkILGenericArgsByCount 0 typarCount

        | HandleKind.TypeReference ->
            let typeRef = mdReader.GetTypeReference(TypeReferenceHandle.op_Explicit entityHandle)
            let typarCount = parseTyparCount (readString cenv typeRef.Name)
            mkILGenericArgsByCount 0 typarCount

        | HandleKind.MethodDefinition ->
            let methDef = mdReader.GetMethodDefinition(MethodDefinitionHandle.op_Explicit entityHandle)
            let typarCount = methDef.GetGenericParameters().Count
            mkILGenericArgsByCount 0 typarCount

        | _ ->
            invalidOp "readILGenericArgs: Invalid handle kind."

    let readILType (cenv: cenv) typarOffset sigTypeKind (handle: EntityHandle) : ILType =
        match handle.Kind with
        | HandleKind.TypeReference ->
            readILTypeFromTypeReference cenv sigTypeKind (TypeReferenceHandle.op_Explicit(handle))
        | HandleKind.TypeDefinition ->
            readILTypeFromTypeDefinition cenv sigTypeKind (TypeDefinitionHandle.op_Explicit(handle))
        | HandleKind.TypeSpecification ->
            readILTypeFromTypeSpecification cenv typarOffset (TypeSpecificationHandle.op_Explicit(handle))

        | _ ->
            failwithf "Invalid Handle Kind: %A" handle.Kind

    let readILModuleRefFromModuleReference (cenv: cenv) (modRef: ModuleReference) =
        let name = readString cenv modRef.Name
        ILModuleRef.Create(name, hasMetadata = true, hash = None)

    let readILTypeRefFromTypeReference (cenv: cenv) (typeRef: TypeReference) : ILTypeRef =
        let mdReader = cenv.MetadataReader

        let ilScopeRef = readILScopeRef cenv typeRef.ResolutionScope
        let enc =
            match typeRef.ResolutionScope.Kind with
            | HandleKind.TypeReference ->
                let encTypeRef = mdReader.GetTypeReference(TypeReferenceHandle.op_Explicit typeRef.ResolutionScope)
                let encILTypeRef = readILTypeRefFromTypeReference cenv encTypeRef
                encILTypeRef.Enclosing @ [encILTypeRef.Name]
            | _ ->
                List.empty
        let name = readTypeName cenv typeRef.Namespace typeRef.Name

        ILTypeRef.Create(ilScopeRef, enc, name)

    let readILTypeFromTypeReference (cenv: cenv) (sigTypeKind: SignatureTypeKind) (typeRefHandle: TypeReferenceHandle) =
        let cacheKey = struct(typeRefHandle, sigTypeKind)
        match cenv.TryGetCachedILType cacheKey with
        | ValueSome ilType -> ilType
        | _ ->
            let mdReader = cenv.MetadataReader

            let typeRef = mdReader.GetTypeReference(typeRefHandle)

            let ilTypeRef = readILTypeRefFromTypeReference cenv typeRef
            let ilGenericArgs = readILGenericArgs cenv (TypeReferenceHandle.op_Implicit typeRefHandle)
            let ilTypeSpec = ILTypeSpec.Create(ilTypeRef, ilGenericArgs)

            let ilBoxity =
                match mdReader.ResolveSignatureTypeKind(TypeReferenceHandle.op_Implicit typeRefHandle, byte sigTypeKind) with
                | SignatureTypeKind.ValueType -> AsValue
                | _ -> AsObject

            let ilType = mkILTy ilBoxity ilTypeSpec
            cenv.CacheILType(cacheKey, ilType)
            ilType

    let rec readILTypeRefFromTypeDefinition (cenv: cenv) (typeDef: TypeDefinition) : ILTypeRef =
        let mdReader = cenv.MetadataReader

        let enclosing =
            if typeDef.IsNested then
                let parentTypeDefHandle = typeDef.GetDeclaringType()
                let parentTypeDef = mdReader.GetTypeDefinition(parentTypeDefHandle)
                let ilTypeRef = readILTypeRefFromTypeDefinition cenv parentTypeDef
                ilTypeRef.Enclosing @ [ ilTypeRef.Name ]
            else
                []
  
        let name =
            if enclosing.Length > 0 then 
                readString cenv typeDef.Name
            else
                readTypeName cenv typeDef.Namespace typeDef.Name

        ILTypeRef.Create(ILScopeRef.Local, enclosing, name)

    let readILTypeFromTypeDefinitionUncached (cenv: cenv) (sigTypeKind: SignatureTypeKind) (typeDefHandle: TypeDefinitionHandle) =
        let mdReader = cenv.MetadataReader

        let typeDef = mdReader.GetTypeDefinition(typeDefHandle)
        let ilTypeRef = readILTypeRefFromTypeDefinition cenv typeDef
        let ilGenericArgs = readILGenericArgs cenv (TypeDefinitionHandle.op_Implicit typeDefHandle)
        let ilTypeSpec = ILTypeSpec.Create(ilTypeRef, ilGenericArgs)

        let ilBoxity =
            match mdReader.ResolveSignatureTypeKind(TypeDefinitionHandle.op_Implicit typeDefHandle, byte sigTypeKind) with
            | SignatureTypeKind.ValueType -> AsValue
            | _ -> AsObject

        mkILTy ilBoxity ilTypeSpec

    let readILTypeFromTypeDefinition (cenv: cenv) (sigTypeKind: SignatureTypeKind) (typeDefHandle: TypeDefinitionHandle) =
        let cacheKey = struct(typeDefHandle, sigTypeKind)
        match cenv.TryGetCachedILType cacheKey with
        | ValueSome(ilType) -> ilType
        | _ ->
            let ilType = readILTypeFromTypeDefinitionUncached cenv sigTypeKind typeDefHandle
            cenv.CacheILType(cacheKey, ilType)
            ilType

    let readILTypeFromTypeSpecification (cenv: cenv) typarOffset (typeSpecHandle: TypeSpecificationHandle) =
        let mdReader = cenv.MetadataReader
        let typeSpec = mdReader.GetTypeSpecification(typeSpecHandle)
        typeSpec.DecodeSignature(cenv.SignatureTypeProvider, typarOffset)

    let readILGenericParameterDef (cenv: cenv) typarOffset (genParamHandle: GenericParameterHandle) : ILGenericParameterDef =
        let mdReader = cenv.MetadataReader

        let genParam = mdReader.GetGenericParameter(genParamHandle)
        let attributes = genParam.Attributes

        let constraints =
            genParam.GetConstraints()
            |> Seq.map (fun genParamCnstrHandle ->
                let genParamCnstr = mdReader.GetGenericParameterConstraint(genParamCnstrHandle)
                readILType cenv typarOffset SignatureTypeKind.Class (* original reader assumed object, it's ok *) genParamCnstr.Type
            )
            |> List.ofSeq     

        let variance = 
            let attributes = attributes &&& GenericParameterAttributes.VarianceMask
            match attributes with
            | GenericParameterAttributes.Covariant -> CoVariant
            | GenericParameterAttributes.Contravariant -> ContraVariant
            | _ -> NonVariant

        {
            Name = readString cenv genParam.Name
            Constraints = constraints
            Variance = variance
            HasReferenceTypeConstraint = int (attributes &&& GenericParameterAttributes.ReferenceTypeConstraint) <> 0
            HasNotNullableValueTypeConstraint = int (attributes &&& GenericParameterAttributes.NotNullableValueTypeConstraint) <> 0
            HasDefaultConstructorConstraint = int (attributes &&& GenericParameterAttributes.DefaultConstructorConstraint) <> 0
            CustomAttrsStored = readILAttributesStored cenv typarOffset (genParam.GetCustomAttributes())
            MetadataIndex = MetadataTokens.GetRowNumber(GenericParameterHandle.op_Implicit(genParamHandle))
        }

    let readILGenericParameterDefs (cenv: cenv) typarOffset (genParamHandles: GenericParameterHandleCollection) =
        genParamHandles
        |> Seq.map (readILGenericParameterDef cenv typarOffset)
        |> List.ofSeq

    let rec readDeclaringTypeInfoFromMemberOrMethod (cenv: cenv) typarOffset (handle: EntityHandle) : string * ILType =
        let mdReader = cenv.MetadataReader
        match handle.Kind with
        | HandleKind.MemberReference ->
            let memberRef = mdReader.GetMemberReference(MemberReferenceHandle.op_Explicit(handle))
            (readString cenv memberRef.Name, readILType cenv typarOffset SignatureTypeKind.Class (* original reader assumed object, it's ok *) memberRef.Parent)

        | HandleKind.MethodDefinition ->
            let methodDef = mdReader.GetMethodDefinition(MethodDefinitionHandle.op_Explicit(handle))
            (readString cenv methodDef.Name, readILTypeFromTypeDefinition cenv SignatureTypeKind.Class (* original reader assumed object, it's ok *) (methodDef.GetDeclaringType()))

        | HandleKind.MethodSpecification ->
            let methodSpec = mdReader.GetMethodSpecification(MethodSpecificationHandle.op_Explicit(handle))
            readDeclaringTypeInfoFromMemberOrMethod cenv typarOffset methodSpec.Method

        | _ ->
            failwithf "Invalid Entity Handle Kind: %A" handle.Kind

    let readILMethodSpecFromMemberReference (cenv: cenv) typarOffset (memberRefHandle: MemberReferenceHandle) =
        let mdReader = cenv.MetadataReader

        let memberRef = mdReader.GetMemberReference(memberRefHandle)
        let enclILTy = readILType cenv typarOffset SignatureTypeKind.Class (* original reader assumed object, it's ok *) memberRef.Parent
        let typarOffset = enclILTy.GenericArgs.Length
        let si = memberRef.DecodeMethodSignature(cenv.SignatureTypeProvider, typarOffset)

        let name = readString cenv memberRef.Name
        let ilCallingConv = mkILCallingConv si.Header
        let genericArity = si.GenericParameterCount
        let ilMethodRef = ILMethodRef.Create(enclILTy.TypeRef, ilCallingConv, name, genericArity, si.ParameterTypes |> List.ofSeq, si.ReturnType)
        let ilGenericArgs = mkILGenericArgsByCount typarOffset genericArity 

        ILMethodSpec.Create(enclILTy, ilMethodRef, ilGenericArgs)

    let readILMethodSpecFromMethodDefinitionUncached (cenv: cenv) (methDefHandle: MethodDefinitionHandle) =
        let mdReader = cenv.MetadataReader

        let methDef = mdReader.GetMethodDefinition(methDefHandle)
        let enclILTy = readILTypeFromTypeDefinition cenv SignatureTypeKind.Class (* original reader assumed object, it's ok *) (methDef.GetDeclaringType())
        let typarOffset = enclILTy.GenericArgs.Length
        let ilMethDef = readILMethodDef cenv methDefHandle

        let genericArity = ilMethDef.GenericParams.Length
        let ilMethodRef = ILMethodRef.Create(enclILTy.TypeRef, ilMethDef.CallingConv, ilMethDef.Name, genericArity, ilMethDef.ParameterTypes, ilMethDef.Return.Type)
        let ilGenericArgs = mkILGenericArgsByCount typarOffset genericArity 
        ILMethodSpec.Create(enclILTy, ilMethodRef, ilGenericArgs)

    let readILMethodSpecFromMethodDefinition (cenv: cenv) (methDefHandle: MethodDefinitionHandle) =
        match cenv.TryGetCachedILMethodSpec(methDefHandle) with
        | ValueSome(ilMethSpec) -> ilMethSpec
        | _ ->
            let ilMethSpec = readILMethodSpecFromMethodDefinitionUncached cenv methDefHandle
            cenv.CacheILMethodSpec(methDefHandle, ilMethSpec)
            ilMethSpec

    let readILMethodSpecFromMethodSpecification (cenv: cenv) typarOffset (methSpecHandle: MethodSpecificationHandle) =
        let mdReader = cenv.MetadataReader

        let methSpec = mdReader.GetMethodSpecification methSpecHandle
        let ilGenericArgs =
            methSpec.DecodeSignature(cenv.SignatureTypeProvider, typarOffset)
            |> List.ofSeq

        let origILMethSpec = readILMethodSpec cenv typarOffset methSpec.Method

        ILMethodSpec.Create(origILMethSpec.DeclaringType, origILMethSpec.MethodRef, ilGenericArgs)

    let rec readILMethodSpec (cenv: cenv) typarOffset (handle: EntityHandle) : ILMethodSpec =
        match handle.Kind with
        | HandleKind.MemberReference ->
            readILMethodSpecFromMemberReference cenv typarOffset (MemberReferenceHandle.op_Explicit handle)
        | HandleKind.MethodDefinition ->
            readILMethodSpecFromMethodDefinition cenv (MethodDefinitionHandle.op_Explicit handle)
        | HandleKind.MethodSpecification ->
            readILMethodSpecFromMethodSpecification cenv typarOffset (MethodSpecificationHandle.op_Explicit handle)

        | _ ->
            failwithf "Invalid Entity Handle Kind: %A" handle.Kind

    let readILSecurityDecl (cenv: cenv) (declSecurityAttributeHandle: DeclarativeSecurityAttributeHandle) =
        let mdReader = cenv.MetadataReader

        let declSecurityAttribute = mdReader.GetDeclarativeSecurityAttribute(declSecurityAttributeHandle)

        let bytes = mdReader.GetBlobBytes(declSecurityAttribute.PermissionSet)
        ILSecurityDecl(mkILSecurityAction declSecurityAttribute.Action, bytes)

    let readILSecurityDeclsStored (cenv: cenv) (declSecurityAttributeHandles: DeclarativeSecurityAttributeHandleCollection) =
        mkILSecurityDeclsReader (fun _ ->
            let securityDeclsArray = Array.zeroCreate declSecurityAttributeHandles.Count
            let mutable i = 0
            for declSecurityAttributeHandle in declSecurityAttributeHandles do
                securityDeclsArray.[i] <- readILSecurityDecl cenv declSecurityAttributeHandle
                i <- i + 1
            securityDeclsArray
        )

    let readILAttribute (cenv: cenv) typarOffset (customAttrHandle: CustomAttributeHandle) =
        let mdReader = cenv.MetadataReader
        let customAttr = mdReader.GetCustomAttribute(customAttrHandle)

        let bytes = 
            if customAttr.Value.IsNil then [||]
            else mdReader.GetBlobBytes(customAttr.Value)

        let elements = [] // Why are we not putting elements in here?
        ILAttribute.Encoded(readILMethodSpec cenv typarOffset customAttr.Constructor, bytes, elements)

    let readILAttributesStored (cenv: cenv) typarOffset (customAttrs: CustomAttributeHandleCollection) =
        if customAttrs.Count = 0 then
            emptyILCustomAttrsStored
        else
            mkILCustomAttrsReader (fun _ ->
                let customAttrsArray = Array.zeroCreate customAttrs.Count
                let mutable i = 0
                for customAttrHandle in customAttrs do
                    customAttrsArray.[i] <- readILAttribute cenv typarOffset customAttrHandle
                    i <- i + 1
                customAttrsArray)

    let readILNestedExportedType (cenv: cenv) typarOffset nested (exportedTyHandle: ExportedTypeHandle) =
        let mdReader = cenv.MetadataReader

        let exportedTy = mdReader.GetExportedType(exportedTyHandle)

        let name = readTypeName cenv exportedTy.Namespace exportedTy.Name

        {
            Name = name
            Access = mkILMemberAccess exportedTy.Attributes
            Nested = realILNestedExportedTypes cenv typarOffset nested exportedTyHandle
            CustomAttrsStored = readILAttributesStored cenv typarOffset (exportedTy.GetCustomAttributes())
            MetadataIndex = MetadataTokens.GetRowNumber(ExportedTypeHandle.op_Implicit(exportedTyHandle))
        }

    let realILNestedExportedTypes (cenv: cenv) typarOffset (nested: ReadOnlyDictionary<ExportedTypeHandle, ResizeArray<ExportedTypeHandle>>) (parentExportedTyHandle: ExportedTypeHandle) =
        match nested.TryGetValue parentExportedTyHandle with
        | true, nestedTys ->
            nestedTys
            |> Seq.map (fun x -> readILNestedExportedType cenv typarOffset nested x)
            |> List.ofSeq
            |> mkILNestedExportedTypes
        | _ ->
            mkILNestedExportedTypes List.empty

    let readILExportedType (cenv: cenv) typarOffset (nested: ReadOnlyDictionary<ExportedTypeHandle, ResizeArray<ExportedTypeHandle>>) (exportedTyHandle: ExportedTypeHandle) =
        let mdReader = cenv.MetadataReader

        let exportedTy = mdReader.GetExportedType(exportedTyHandle)

        let name = readTypeName cenv exportedTy.Namespace exportedTy.Name

        {
            ScopeRef = readILScopeRef cenv exportedTy.Implementation
            Name = name
            Attributes = exportedTy.Attributes
            Nested = realILNestedExportedTypes cenv typarOffset nested exportedTyHandle
            CustomAttrsStored = readILAttributesStored cenv typarOffset (exportedTy.GetCustomAttributes())
            MetadataIndex = MetadataTokens.GetRowNumber(ExportedTypeHandle.op_Implicit(exportedTyHandle))
        }

    let readILExportedTypes (cenv: cenv) typarOffset (exportedTys: ExportedTypeHandleCollection) =
        let mdReader = cenv.MetadataReader
        let nested =
            lazy
                let lookup = Dictionary<ExportedTypeHandle, ResizeArray<ExportedTypeHandle>>()

                for exportedTyHandle in exportedTys do
                    let exportedTy = mdReader.GetExportedType(exportedTyHandle)
                    let access = mkILTypeDefAccess exportedTy.Attributes
                    if not (access = ILTypeDefAccess.Public || access = ILTypeDefAccess.Private) && exportedTy.Implementation.Kind = HandleKind.ExportedType then
                        let parentExportedTyHandle = ExportedTypeHandle.op_Explicit exportedTy.Implementation
                        let nested =
                            match lookup.TryGetValue parentExportedTyHandle with
                            | true, nested -> nested
                            | _ ->
                                let nested = ResizeArray()
                                lookup.[parentExportedTyHandle] <- nested
                                nested
                        nested.Add exportedTyHandle

                ReadOnlyDictionary lookup
        let f =
            lazy
                let nested = nested.Value
                [
                    for exportedTyHandle in exportedTys do
                        let exportedTy = mdReader.GetExportedType(exportedTyHandle)
                        let access = mkILTypeDefAccess exportedTy.Attributes
                        // Not a nested type
                        if (access = ILTypeDefAccess.Public || access = ILTypeDefAccess.Private) && exportedTy.Implementation.Kind <> HandleKind.ExportedType then
                            yield readILExportedType cenv typarOffset nested exportedTyHandle
                ]
        mkILExportedTypesLazy f

    let readILAssemblyManifest (cenv: cenv) (entryPointToken: int) =
        let mdReader = cenv.MetadataReader

        let asmDef = mdReader.GetAssemblyDefinition()

        let publicKey =
            let bytes = 
                asmDef.PublicKey
                |> mdReader.GetBlobBytes
            if bytes.Length = 0 then None
            else Some(bytes)

        let locale =
            let str = readString cenv asmDef.Culture
            if str.Length = 0 then None
            else Some(str)

        let flags = asmDef.Flags

        let entrypointElsewhere =
            let handle = MetadataTokens.EntityHandle(entryPointToken)
            if handle.IsNil then None
            else
                match handle.Kind with
                | HandleKind.AssemblyFile -> 
                    let asmFile = mdReader.GetAssemblyFile(AssemblyFileHandle.op_Explicit(handle))
                    Some(readILModuleRefFromAssemblyFile cenv asmFile)
                | _ -> None

        {
            Name = readString cenv asmDef.Name
            AuxModuleHashAlgorithm = int asmDef.HashAlgorithm
            SecurityDeclsStored = readILSecurityDeclsStored cenv (asmDef.GetDeclarativeSecurityAttributes())
            PublicKey = publicKey
            Version = Some(mkILVersionInfo asmDef.Version)
            Locale = locale
            CustomAttrsStored = readILAttributesStored cenv 0 (asmDef.GetCustomAttributes())
            AssemblyLongevity = mkILAssemblyLongevity flags
            DisableJitOptimizations = int (flags &&& AssemblyFlags.DisableJitCompileOptimizer) <> 0
            JitTracking = int (flags &&& AssemblyFlags.EnableJitCompileTracking) <> 0
            IgnoreSymbolStoreSequencePoints = (int flags &&& 0x2000) <> 0 // Not listed in AssemblyFlags
            Retargetable = int (flags &&& AssemblyFlags.Retargetable) <> 0
            ExportedTypes = readILExportedTypes cenv 0 mdReader.ExportedTypes
            EntrypointElsewhere = entrypointElsewhere
            MetadataIndex = 1 // always one
        }

    let readILNativeResources (peReader: PEReader) =
        peReader.PEHeaders.SectionHeaders
        |> Seq.choose (fun s ->
            if s.Name.Equals(".rsrc", StringComparison.OrdinalIgnoreCase) then
                let memBlock = peReader.GetSectionData(s.VirtualAddress)
                // REVIEW: We should not read the entire raw bytes.
                let bytes = memBlock.GetContent().ToArray()
                // TODO: This is probably wrong and we shouldn't try to catch this.
                try
                    ILNativeResource.Out(Support.unlinkResource s.VirtualAddress bytes)
                    |> Some
                with
                | _ ->
                    None
            else
                None
        )
        |> Seq.toList

    let tryReadILFieldInit (cenv: cenv) (constantHandle: ConstantHandle) =
        if constantHandle.IsNil then None
        else
            let mdReader = cenv.MetadataReader

            let constant = mdReader.GetConstant(constantHandle)
            let blobReader = mdReader.GetBlobReader(constant.Value)
            match constant.TypeCode with
            | ConstantTypeCode.Boolean -> ILFieldInit.Bool(blobReader.ReadBoolean()) |> Some
            | ConstantTypeCode.Byte -> ILFieldInit.UInt8(blobReader.ReadByte()) |> Some
            | ConstantTypeCode.Char -> ILFieldInit.Char(blobReader.ReadChar() |> uint16) |> Some // Why does ILFieldInit.Char not just take a char?
            | ConstantTypeCode.Double -> ILFieldInit.Double(blobReader.ReadDouble()) |> Some
            | ConstantTypeCode.Int16 -> ILFieldInit.Int16(blobReader.ReadInt16()) |> Some
            | ConstantTypeCode.Int32 -> ILFieldInit.Int32(blobReader.ReadInt32()) |> Some
            | ConstantTypeCode.Int64 -> ILFieldInit.Int64(blobReader.ReadInt64()) |> Some
            | ConstantTypeCode.SByte -> ILFieldInit.Int8(blobReader.ReadSByte()) |> Some
            | ConstantTypeCode.Single -> ILFieldInit.Single(blobReader.ReadSingle()) |> Some
            | ConstantTypeCode.String -> ILFieldInit.String(blobReader.ReadUTF16(blobReader.Length)) |> Some
            | ConstantTypeCode.UInt16 -> ILFieldInit.UInt16(blobReader.ReadUInt16()) |> Some
            | ConstantTypeCode.UInt32 -> ILFieldInit.UInt32(blobReader.ReadUInt32()) |> Some
            | ConstantTypeCode.UInt64 -> ILFieldInit.UInt64(blobReader.ReadUInt64()) |> Some
            | ConstantTypeCode.NullReference -> ILFieldInit.Null |> Some
            | _ -> (* possible warning? *) None

    let ilNativeTypeLookup = (ILNativeTypeMap.Value |> Seq.map (fun x -> x)).ToDictionary((fun (key, _) -> key), fun (_, value) -> value) // This looks terrible. Cleanup later.
    let ilVariantTypeMap = (ILVariantTypeMap.Value |> Seq.map (fun x -> x)).ToDictionary((fun (_, key) -> key), fun (value, _) -> value) // This looks terrible. Cleanup later.

    let rec mkILVariantType (kind: int) =
        match ilVariantTypeMap.TryGetValue(kind) with
        | true, ilVariantType -> ilVariantType
        | _ ->
            match kind with
            | _ when (kind &&& vt_ARRAY) <> 0 -> ILNativeVariant.Array(mkILVariantType (kind &&& (~~~vt_ARRAY)))
            | _ when (kind &&& vt_VECTOR) <> 0 -> ILNativeVariant.Vector(mkILVariantType (kind &&& (~~~vt_VECTOR)))
            | _ when (kind &&& vt_BYREF) <> 0 -> ILNativeVariant.Byref(mkILVariantType (kind &&& (~~~vt_BYREF)))
            | _ -> (* possible warning? *) ILNativeVariant.Empty

    let rec readILNativeType (cenv: cenv) (reader: byref<BlobReader>) =
        let kind = reader.ReadByte()
        match ilNativeTypeLookup.TryGetValue(kind) with
        | true, ilNativeType -> ilNativeType
        | _ ->
            match kind with
            | 0x0uy -> ILNativeType.Empty
            | _ when kind = nt_FIXEDSYSSTRING -> ILNativeType.FixedSysString(reader.ReadCompressedInteger())
            | _ when kind = nt_FIXEDARRAY -> ILNativeType.FixedArray(reader.ReadCompressedInteger())

            | _ when kind = nt_SAFEARRAY ->
                if reader.RemainingBytes = 0 then
                    ILNativeType.SafeArray(ILNativeVariant.Empty, None)
                else
                    let variantKind = reader.ReadCompressedInteger()
                    let ilVariantType = mkILVariantType variantKind
                    if reader.RemainingBytes = 0 then
                        ILNativeType.SafeArray(ilVariantType, None)
                    else
                        let s = reader.ReadUTF16(reader.ReadCompressedInteger())
                        ILNativeType.SafeArray(ilVariantType, Some(s))

            | _ when kind = nt_ARRAY ->
                if reader.RemainingBytes = 0 then
                    ILNativeType.Array(None, None)
                else
                    let nt = 
                        let oldReader = reader
                        let u = reader.ReadCompressedInteger() // What is 'u'?
                        if u = int nt_MAX then // What is this doing?
                            ILNativeType.Empty
                        else
                            // NOTE: go back to start and read native type
                            reader <- oldReader
                            readILNativeType cenv &reader
              
                    if reader.RemainingBytes = 0 then
                        ILNativeType.Array(Some(nt), None)
                    else
                        let pnum = reader.ReadCompressedInteger()
                        if reader.RemainingBytes = 0 then
                            ILNativeType.Array(Some(nt), Some(pnum, None))
                        else
                            let additive = reader.ReadCompressedInteger()
                            ILNativeType.Array(Some(nt), Some(pnum, Some(additive)))

            | _ when kind = nt_CUSTOMMARSHALER ->
                let guid = reader.ReadBytes(reader.ReadCompressedInteger())
                let nativeTypeName = reader.ReadUTF16(reader.ReadCompressedInteger())
                let custMarshallerName = reader.ReadUTF16(reader.ReadCompressedInteger())
                let cookieString = reader.ReadBytes(reader.ReadCompressedInteger())
                ILNativeType.Custom(guid, nativeTypeName, custMarshallerName, cookieString)

            | _ -> ILNativeType.Empty

    let tryReadILNativeType (cenv: cenv) (marshalDesc: BlobHandle) =
        if marshalDesc.IsNil then None
        else
            let mdReader = cenv.MetadataReader

            let mutable (* it doesn't have to be mutable, but it's best practice for .NET structs *) reader = mdReader.GetBlobReader(marshalDesc)
            try
                Some(readILNativeType cenv &reader)
            with
            | ex ->
                failwithf "tryReadILNativeType: %A" ex

    let readILParameter (cenv: cenv) typarOffset (returnType: ILReturn) (parameters: ILParameter []) (paramHandle: ParameterHandle) : struct(ILParameter * int) =
        let mdReader = cenv.MetadataReader

        let param = mdReader.GetParameter paramHandle

        let nameOpt = 
            if param.Name.IsNil then ValueNone
            else 
                let str = readString cenv param.Name
                if String.IsNullOrEmpty str then ValueNone
                else ValueSome str

        let typ = 
            if param.SequenceNumber = 0 then returnType.Type
            else parameters.[param.SequenceNumber - 1].Type

        let defaul =
            if int (param.Attributes &&& ParameterAttributes.HasDefault) <> 0 then
                tryReadILFieldInit cenv (param.GetDefaultValue())
            else
                None

        let marshal =
            if int (param.Attributes &&& ParameterAttributes.HasFieldMarshal) <> 0 then 
                tryReadILNativeType cenv (param.GetMarshallingDescriptor())
            else
                None

        let ilParameter =
            {
                Name = match nameOpt with | ValueNone -> None | ValueSome name -> Some name
                Type = typ
                Default = defaul
                Marshal = marshal
                IsIn = int (param.Attributes &&& ParameterAttributes.In) <> 0
                IsOut = int (param.Attributes &&& ParameterAttributes.Out) <> 0
                IsOptional = int (param.Attributes &&& ParameterAttributes.Optional) <> 0
                CustomAttrsStored = readILAttributesStored cenv typarOffset (param.GetCustomAttributes())
                MetadataIndex = MetadataTokens.GetRowNumber(ParameterHandle.op_Implicit paramHandle)
            } : ILParameter
        struct(ilParameter, param.SequenceNumber)

    let readILParameters (cenv: cenv) typarOffset (si: MethodSignature<ILType>) (methDef: MethodDefinition) =
        let ret = ref (mkILReturn si.ReturnType)
        let parameters = 
            let parameters = Array.zeroCreate si.ParameterTypes.Length
            for i = 0 to si.ParameterTypes.Length - 1 do
                parameters.[i] <- mkILParamAnon si.ParameterTypes.[i]
            parameters
        let paramHandles = methDef.GetParameters()

        if paramHandles.Count > 0 then
            paramHandles
            |> Seq.iter (fun paramHandle ->
                let struct(ilParameter, sequenceNumber) = readILParameter cenv typarOffset !ret parameters paramHandle
                if sequenceNumber = 0 then
                    ret := 
                        { Marshal = ilParameter.Marshal
                          Type = ilParameter.Type
                          CustomAttrsStored = ilParameter.CustomAttrsStored
                          MetadataIndex = ilParameter.MetadataIndex }
                else
                    parameters.[sequenceNumber - 1] <- ilParameter)

        !ret, parameters |> List.ofArray

    // -------------------------------------------------------------------- 
    // IL Instruction reading
    // --------------------------------------------------------------------

    [<NoEquality; NoComparison>]
    type ILOperandPrefixEnv =
        {
            mutable al: ILAlignment
            mutable tl: ILTailcall
            mutable vol: ILVolatility
            mutable ro: ILReadonly
            mutable constrained: ILType option
        }

    let noPrefixes mk prefixes = 
        if prefixes.al <> Aligned then failwith "an unaligned prefix is not allowed here"
        if prefixes.vol <> Nonvolatile then failwith "a volatile prefix is not allowed here"
        if prefixes.tl <> Normalcall then failwith "a tailcall prefix is not allowed here"
        if prefixes.ro <> NormalAddress then failwith "a readonly prefix is not allowed here"
        if prefixes.constrained <> None then failwith "a constrained prefix is not allowed here"
        mk

    let volatileOrUnalignedPrefix mk prefixes = 
        if prefixes.tl <> Normalcall then failwith "a tailcall prefix is not allowed here"
        if prefixes.constrained <> None then failwith "a constrained prefix is not allowed here"
        if prefixes.ro <> NormalAddress then failwith "a readonly prefix is not allowed here"
        mk (prefixes.al, prefixes.vol) 

    let volatilePrefix mk prefixes = 
        if prefixes.al <> Aligned then failwith "an unaligned prefix is not allowed here"
        if prefixes.tl <> Normalcall then failwith "a tailcall prefix is not allowed here"
        if prefixes.constrained <> None then failwith "a constrained prefix is not allowed here"
        if prefixes.ro <> NormalAddress then failwith "a readonly prefix is not allowed here"
        mk prefixes.vol

    let tailPrefix mk prefixes = 
        if prefixes.al <> Aligned then failwith "an unaligned prefix is not allowed here"
        if prefixes.vol <> Nonvolatile then failwith "a volatile prefix is not allowed here"
        if prefixes.constrained <> None then failwith "a constrained prefix is not allowed here"
        if prefixes.ro <> NormalAddress then failwith "a readonly prefix is not allowed here"
        mk prefixes.tl 

    let constraintOrTailPrefix mk prefixes = 
        if prefixes.al <> Aligned then failwith "an unaligned prefix is not allowed here"
        if prefixes.vol <> Nonvolatile then failwith "a volatile prefix is not allowed here"
        if prefixes.ro <> NormalAddress then failwith "a readonly prefix is not allowed here"
        mk (prefixes.constrained, prefixes.tl )

    let readonlyPrefix mk prefixes = 
        if prefixes.al <> Aligned then failwith "an unaligned prefix is not allowed here"
        if prefixes.vol <> Nonvolatile then failwith "a volatile prefix is not allowed here"
        if prefixes.tl <> Normalcall then failwith "a tailcall prefix is not allowed here"
        if prefixes.constrained <> None then failwith "a constrained prefix is not allowed here"
        mk prefixes.ro

    type ILOperandDecoder =
        | NoDecoder
        | InlineNone of (ILOperandPrefixEnv -> ILInstr)
        | ShortInlineVar of (ILOperandPrefixEnv -> uint8 -> ILInstr)
        | ShortInlineI of (ILOperandPrefixEnv -> int8 -> ILInstr)
        | InlineI of (ILOperandPrefixEnv -> int32 -> ILInstr)
        | InlineI8 of (ILOperandPrefixEnv -> int64 -> ILInstr)
        | ShortInlineR of (ILOperandPrefixEnv -> single -> ILInstr)
        | InlineR of (ILOperandPrefixEnv -> double -> ILInstr)
        | InlineMethod of (ILOperandPrefixEnv -> ILMethodSpec * ILVarArgs -> ILInstr)
        | InlineSig of (ILOperandPrefixEnv -> ILCallingSignature * ILVarArgs -> ILInstr)
        | ShortInlineBrTarget of (ILOperandPrefixEnv -> ILCodeLabel -> ILInstr)
        | InlineBrTarget of (ILOperandPrefixEnv -> ILCodeLabel -> ILInstr)
        | InlineSwitch of (ILOperandPrefixEnv -> ILCodeLabel list -> ILInstr)
        | InlineType of (ILOperandPrefixEnv -> ILType -> ILInstr)
        | InlineString of (ILOperandPrefixEnv -> string -> ILInstr)
        | InlineField of (ILOperandPrefixEnv -> ILFieldSpec -> ILInstr)
        | InlineTok of (ILOperandPrefixEnv -> ILToken -> ILInstr)
        | InlineVar of (ILOperandPrefixEnv -> uint16 -> ILInstr)

        | PrefixShortInlineI of (ILOperandPrefixEnv -> uint16 -> unit)
        | PrefixInlineNone of (ILOperandPrefixEnv -> unit)
        | PrefixInlineType of (ILOperandPrefixEnv -> ILType -> unit)

    let OneByteDecoders = 
        [|
            InlineNone(noPrefixes AI_nop)//byte OperandType.InlineNone           // nop
            InlineNone(noPrefixes I_break)//byte OperandType.InlineNone           // break
            InlineNone(noPrefixes (I_ldarg(0us)))//byte OperandType.InlineNone           // ldarg.0
            InlineNone(noPrefixes (I_ldarg(1us)))//byte OperandType.InlineNone           // ldarg.1
            InlineNone(noPrefixes (I_ldarg(2us)))//byte OperandType.InlineNone           // ldarg.2
            InlineNone(noPrefixes (I_ldarg(3us)))//byte OperandType.InlineNone           // ldarg.3
            InlineNone(noPrefixes (I_ldloc(0us)))//byte OperandType.InlineNone           // ldloc.0
            InlineNone(noPrefixes (I_ldloc(1us)))//byte OperandType.InlineNone           // ldloc.1
            InlineNone(noPrefixes (I_ldloc(2us)))//byte OperandType.InlineNone           // ldloc.2
            InlineNone(noPrefixes (I_ldloc(3us)))//byte OperandType.InlineNone           // ldloc.3
            InlineNone(noPrefixes (I_stloc(0us)))//byte OperandType.InlineNone           // stloc.0
            InlineNone(noPrefixes (I_stloc(1us)))//byte OperandType.InlineNone           // stloc.1
            InlineNone(noPrefixes (I_stloc(2us)))//byte OperandType.InlineNone           // stloc.2
            InlineNone(noPrefixes (I_stloc(3us)))//byte OperandType.InlineNone           // stloc.3
            ShortInlineVar(noPrefixes (fun index -> I_ldarg(uint16 index))) //byte OperandType.ShortInlineVar       // ldarg.s
            ShortInlineVar(noPrefixes (fun index -> I_ldarga(uint16 index)))//byte OperandType.ShortInlineVar       // ldarga.s
            ShortInlineVar(noPrefixes (fun index -> I_starg(uint16 index)))//byte OperandType.ShortInlineVar       // starg.s
            ShortInlineVar(noPrefixes (fun index -> I_ldloc(uint16 index)))//byte OperandType.ShortInlineVar       // ldloc.s
            ShortInlineVar(noPrefixes (fun index -> I_ldloca(uint16 index)))//byte OperandType.ShortInlineVar       // ldloca.s
            ShortInlineVar(noPrefixes (fun index -> I_stloc(uint16 index)))//byte OperandType.ShortInlineVar       // stloc.s
            InlineNone(noPrefixes AI_ldnull)//byte OperandType.InlineNone           // ldnull
            InlineNone(noPrefixes (AI_ldc(DT_I4, ILConst.I4(-1))))//byte OperandType.InlineNone           // ldc.i4.m1
            InlineNone(noPrefixes (AI_ldc(DT_I4, ILConst.I4(0))))//byte OperandType.InlineNone           // ldc.i4.0
            InlineNone(noPrefixes (AI_ldc(DT_I4, ILConst.I4(1))))//byte OperandType.InlineNone           // ldc.i4.1
            InlineNone(noPrefixes (AI_ldc(DT_I4, ILConst.I4(2))))//byte OperandType.InlineNone           // ldc.i4.2
            InlineNone(noPrefixes (AI_ldc(DT_I4, ILConst.I4(3))))//byte OperandType.InlineNone           // ldc.i4.3
            InlineNone(noPrefixes (AI_ldc(DT_I4, ILConst.I4(4))))//byte OperandType.InlineNone           // ldc.i4.4
            InlineNone(noPrefixes (AI_ldc(DT_I4, ILConst.I4(5))))//byte OperandType.InlineNone           // ldc.i4.5
            InlineNone(noPrefixes (AI_ldc(DT_I4, ILConst.I4(6))))//byte OperandType.InlineNone           // ldc.i4.6
            InlineNone(noPrefixes (AI_ldc(DT_I4, ILConst.I4(7))))//byte OperandType.InlineNone           // ldc.i4.7
            InlineNone(noPrefixes (AI_ldc(DT_I4, ILConst.I4(8))))//byte OperandType.InlineNone           // ldc.i4.8
            ShortInlineI(noPrefixes (fun value -> AI_ldc(DT_I4, ILConst.I4(int32 value))))//byte OperandType.ShortInlineI         // ldc.i4.s
            InlineI(noPrefixes (fun value -> AI_ldc(DT_I4, ILConst.I4(value))))//byte OperandType.InlineI              // ldc.i4
            InlineI8(noPrefixes (fun value -> AI_ldc(DT_I8, ILConst.I8(value))))//byte OperandType.InlineI8             // ldc.i8
            ShortInlineR(noPrefixes (fun value -> AI_ldc(DT_R4, ILConst.R4(value))))//byte OperandType.ShortInlineR         // ldc.r4
            InlineR(noPrefixes (fun value -> AI_ldc(DT_R8, ILConst.R8(value))))//byte OperandType.InlineR              // ldc.r8
            NoDecoder
            InlineNone(noPrefixes AI_dup)//byte OperandType.InlineNone           // dup
            InlineNone(noPrefixes AI_pop)//byte OperandType.InlineNone           // pop
            InlineMethod(noPrefixes (fun (ilMethSpec, _) -> I_jmp(ilMethSpec)))//byte OperandType.InlineMethod         // jmp
            InlineMethod(tailPrefix (fun ilTailcall (ilMethSpec, ilVarArgs) -> I_call(ilTailcall, ilMethSpec, ilVarArgs)))//byte OperandType.InlineMethod         // call
            InlineSig(tailPrefix (fun ilTailcall (ilCallSig, ilVarArgs) -> I_calli(ilTailcall, ilCallSig, ilVarArgs)))//byte OperandType.InlineSig            // calli
            InlineNone(noPrefixes I_ret)//byte OperandType.InlineNone           // ret
            ShortInlineBrTarget(noPrefixes (fun value -> I_br(int value)))//byte OperandType.ShortInlineBrTarget  // br.s
            ShortInlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_brfalse, int value)))//byte OperandType.ShortInlineBrTarget  // brfalse.s
            ShortInlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_brtrue, int value)))//byte OperandType.ShortInlineBrTarget  // brtrue.s
            ShortInlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_beq, int value)))//byte OperandType.ShortInlineBrTarget  // beq.s
            ShortInlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_bge, int value)))//byte OperandType.ShortInlineBrTarget  // bge.s
            ShortInlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_bgt, int value)))//byte OperandType.ShortInlineBrTarget  // bgt.s
            ShortInlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_ble, int value)))//byte OperandType.ShortInlineBrTarget  // ble.s
            ShortInlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_blt, int value)))//byte OperandType.ShortInlineBrTarget  // blt.s
            ShortInlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_bne_un, int value)))//byte OperandType.ShortInlineBrTarget  // bne.un.s
            ShortInlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_bge_un, int value)))//byte OperandType.ShortInlineBrTarget  // bge.un.s
            ShortInlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_bgt_un, int value)))//byte OperandType.ShortInlineBrTarget  // bgt.un.s
            ShortInlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_ble_un, int value)))//byte OperandType.ShortInlineBrTarget  // ble.un.s
            ShortInlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_blt_un, int value)))//byte OperandType.ShortInlineBrTarget  // blt.un.s
            InlineBrTarget(noPrefixes (fun value -> I_br(value))) // br
            InlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_brfalse, value))) // brfalse
            InlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_brtrue, value))) // brtrue
            InlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_beq, value))) // beq
            InlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_bge, value))) // bge
            InlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_bgt, value))) // bgt
            InlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_ble, value))) // ble
            InlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_blt, value))) // blt
            InlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_bne_un, value))) // bne.un
            InlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_bge_un, value))) // bge.un
            InlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_bgt_un, value))) // bgt.un
            InlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_ble_un, value))) // ble.un
            InlineBrTarget(noPrefixes (fun value -> I_brcmp(BI_blt_un, value))) // blt.un
            InlineSwitch(noPrefixes (fun values -> ILInstr.I_switch(values)))//byte OperandType.InlineSwitch         // switch
            InlineNone(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) -> I_ldind(ilAlignment, ilVolatility, DT_I1)))//byte OperandType.InlineNone           // ldind.i1
            InlineNone(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) -> I_ldind(ilAlignment, ilVolatility, DT_U1)))//byte OperandType.InlineNone           // ldind.u1
            InlineNone(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) -> I_ldind(ilAlignment, ilVolatility, DT_I2)))//byte OperandType.InlineNone           // ldind.i2
            InlineNone(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) -> I_ldind(ilAlignment, ilVolatility, DT_U2)))//byte OperandType.InlineNone           // ldind.u2
            InlineNone(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) -> I_ldind(ilAlignment, ilVolatility, DT_I4)))//byte OperandType.InlineNone           // ldind.i4
            InlineNone(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) -> I_ldind(ilAlignment, ilVolatility, DT_U4)))//byte OperandType.InlineNone           // ldind.u4
            InlineNone(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) -> I_ldind(ilAlignment, ilVolatility, DT_I8)))//byte OperandType.InlineNone           // ldind.i8
            InlineNone(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) -> I_ldind(ilAlignment, ilVolatility, DT_I)))//byte OperandType.InlineNone           // ldind.i
            InlineNone(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) -> I_ldind(ilAlignment, ilVolatility, DT_R4)))//byte OperandType.InlineNone           // ldind.r4
            InlineNone(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) -> I_ldind(ilAlignment, ilVolatility, DT_R8)))//byte OperandType.InlineNone           // ldind.r8
            InlineNone(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) -> I_ldind(ilAlignment, ilVolatility, DT_REF)))//byte OperandType.InlineNone           // ldind.ref
            InlineNone(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) -> I_stind(ilAlignment, ilVolatility, DT_REF)))//byte OperandType.InlineNone           // stind.ref
            InlineNone(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) -> I_stind(ilAlignment, ilVolatility, DT_I1)))//byte OperandType.InlineNone           // stind.i1
            InlineNone(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) -> I_stind(ilAlignment, ilVolatility, DT_I2)))//byte OperandType.InlineNone           // stind.i2
            InlineNone(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) -> I_stind(ilAlignment, ilVolatility, DT_I4)))//byte OperandType.InlineNone           // stind.i4
            InlineNone(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) -> I_stind(ilAlignment, ilVolatility, DT_I8)))//byte OperandType.InlineNone           // stind.i8
            InlineNone(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) -> I_stind(ilAlignment, ilVolatility, DT_R4)))//byte OperandType.InlineNone           // stind.r4
            InlineNone(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) -> I_stind(ilAlignment, ilVolatility, DT_R8)))//byte OperandType.InlineNone           // stind.r8
            InlineNone(noPrefixes AI_add)//byte OperandType.InlineNone           // add
            InlineNone(noPrefixes AI_sub)//byte OperandType.InlineNone           // sub
            InlineNone(noPrefixes AI_mul)//byte OperandType.InlineNone           // mul
            InlineNone(noPrefixes AI_div)//byte OperandType.InlineNone           // div
            InlineNone(noPrefixes AI_div_un)//byte OperandType.InlineNone           // div.un
            InlineNone(noPrefixes AI_rem)//byte OperandType.InlineNone           // rem
            InlineNone(noPrefixes AI_rem_un)//byte OperandType.InlineNone           // rem.un
            InlineNone(noPrefixes AI_and)//byte OperandType.InlineNone           // and
            InlineNone(noPrefixes AI_or)//byte OperandType.InlineNone           // or
            InlineNone(noPrefixes AI_xor)//byte OperandType.InlineNone           // xor
            InlineNone(noPrefixes AI_shl)//byte OperandType.InlineNone           // shl
            InlineNone(noPrefixes AI_shr)//byte OperandType.InlineNone           // shr
            InlineNone(noPrefixes AI_shr_un)//byte OperandType.InlineNone           // shr.un
            InlineNone(noPrefixes AI_neg)//byte OperandType.InlineNone           // neg
            InlineNone(noPrefixes AI_not)//byte OperandType.InlineNone           // not
            InlineNone(noPrefixes (AI_conv(DT_I1)))//byte OperandType.InlineNone           // conv.i1
            InlineNone(noPrefixes (AI_conv(DT_I2)))//byte OperandType.InlineNone           // conv.i2
            InlineNone(noPrefixes (AI_conv(DT_I4)))//byte OperandType.InlineNone           // conv.i4
            InlineNone(noPrefixes (AI_conv(DT_I8)))//byte OperandType.InlineNone           // conv.i8
            InlineNone(noPrefixes (AI_conv(DT_R4)))//byte OperandType.InlineNone           // conv.r4
            InlineNone(noPrefixes (AI_conv(DT_R8)))//byte OperandType.InlineNone           // conv.r8
            InlineNone(noPrefixes (AI_conv(DT_U4)))//byte OperandType.InlineNone           // conv.u4
            InlineNone(noPrefixes (AI_conv(DT_U8)))//byte OperandType.InlineNone           // conv.u8
            InlineMethod(constraintOrTailPrefix (fun (ilConstraint, ilTailcall) (ilMethSpec, ilVarArgs) -> match ilConstraint with | Some(ilType) -> I_callconstraint(ilTailcall, ilType, ilMethSpec, ilVarArgs) | _ -> I_callvirt(ilTailcall, ilMethSpec, ilVarArgs)))//byte OperandType.InlineMethod         // callvirt
            InlineType(noPrefixes (fun ilType -> I_cpobj(ilType)))//byte OperandType.InlineType           // cpobj
            InlineType(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) ilType -> I_ldobj(ilAlignment, ilVolatility, ilType)))//byte OperandType.InlineType           // ldobj
            InlineString(noPrefixes (fun value -> I_ldstr(value)))//byte OperandType.InlineString         // ldstr
            InlineMethod(noPrefixes (fun (ilMethSpec, ilVarArgs) -> I_newobj(ilMethSpec, ilVarArgs)))//byte OperandType.InlineMethod         // newobj
            InlineType(noPrefixes (fun ilType -> I_castclass(ilType)))//byte OperandType.InlineType           // castclass
            InlineType(noPrefixes (fun ilType -> I_isinst(ilType)))//byte OperandType.InlineType           // isinst
            InlineNone(noPrefixes (AI_conv(DT_R)))//byte OperandType.InlineNone           // conv.r.un // TODO: Looks like we don't have this?
            NoDecoder
            NoDecoder
            InlineType(noPrefixes (fun ilType -> I_unbox(ilType)))//byte OperandType.InlineType           // unbox
            InlineNone(noPrefixes I_throw)//byte OperandType.InlineNone           // throw
            InlineField(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) ilFieldSpec -> I_ldfld(ilAlignment, ilVolatility, ilFieldSpec)))//byte OperandType.InlineField          // ldfld
            InlineField(noPrefixes (fun ilFieldSpec -> I_ldflda(ilFieldSpec)))//byte OperandType.InlineField          // ldflda
            InlineField(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) ilFieldSpec -> I_stfld(ilAlignment, ilVolatility, ilFieldSpec)))//byte OperandType.InlineField          // stfld
            InlineField(volatilePrefix (fun ilVolatility ilFieldSpec -> I_ldsfld(ilVolatility, ilFieldSpec)))//byte OperandType.InlineField          // ldsfld
            InlineField(noPrefixes (fun ilFieldSpec -> I_ldsflda(ilFieldSpec)))//byte OperandType.InlineField          // ldsflda
            InlineField(volatilePrefix (fun ilVolatility ilFieldSpec -> I_stsfld(ilVolatility, ilFieldSpec)))//byte OperandType.InlineField          // stsfld
            InlineType(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) ilType -> I_stobj(ilAlignment, ilVolatility, ilType)))//byte OperandType.InlineType           // stobj
            InlineNone(noPrefixes (AI_conv_ovf_un(DT_I1)))//byte OperandType.InlineNone           // conv.ovf.i1.un
            InlineNone(noPrefixes (AI_conv_ovf_un(DT_I2)))//byte OperandType.InlineNone           // conv.ovf.i2.un
            InlineNone(noPrefixes (AI_conv_ovf_un(DT_I4)))//byte OperandType.InlineNone           // conv.ovf.i4.un
            InlineNone(noPrefixes (AI_conv_ovf_un(DT_I8)))//byte OperandType.InlineNone           // conv.ovf.i8.un
            InlineNone(noPrefixes (AI_conv_ovf_un(DT_U1)))//byte OperandType.InlineNone           // conv.ovf.u1.un
            InlineNone(noPrefixes (AI_conv_ovf_un(DT_U2)))//byte OperandType.InlineNone           // conv.ovf.u2.un
            InlineNone(noPrefixes (AI_conv_ovf_un(DT_U4)))//byte OperandType.InlineNone           // conv.ovf.u4.un
            InlineNone(noPrefixes (AI_conv_ovf_un(DT_U8)))//byte OperandType.InlineNone           // conv.ovf.u8.un
            InlineNone(noPrefixes (AI_conv_ovf_un(DT_I)))//byte OperandType.InlineNone           // conv.ovf.i.un
            InlineNone(noPrefixes (AI_conv_ovf_un(DT_U)))//byte OperandType.InlineNone           // conv.ovf.u.un
            InlineType(noPrefixes (fun ilType -> I_box(ilType)))//byte OperandType.InlineType           // box
            InlineType(noPrefixes (fun ilType -> I_newarr(ILArrayShape.SingleDimensional, ilType)))//byte OperandType.InlineType           // newarr
            InlineNone(noPrefixes I_ldlen)//byte OperandType.InlineNone           // ldlen
            InlineType(readonlyPrefix (fun ilReadonly ilType -> I_ldelema(ilReadonly, false, ILArrayShape.SingleDimensional, ilType)))//byte OperandType.InlineType           // ldelema
            InlineNone(noPrefixes (I_ldelem(DT_I1)))//byte OperandType.InlineNone           // ldelem.i1
            InlineNone(noPrefixes (I_ldelem(DT_U1)))//byte OperandType.InlineNone           // ldelem.u1
            InlineNone(noPrefixes (I_ldelem(DT_I2)))//byte OperandType.InlineNone           // ldelem.i2
            InlineNone(noPrefixes (I_ldelem(DT_U2)))//byte OperandType.InlineNone           // ldelem.u2
            InlineNone(noPrefixes (I_ldelem(DT_I4)))//byte OperandType.InlineNone           // ldelem.i4
            InlineNone(noPrefixes (I_ldelem(DT_U4)))//byte OperandType.InlineNone           // ldelem.u4
            InlineNone(noPrefixes (I_ldelem(DT_I8)))//byte OperandType.InlineNone           // ldelem.i8
            InlineNone(noPrefixes (I_ldelem(DT_I)))//byte OperandType.InlineNone           // ldelem.i
            InlineNone(noPrefixes (I_ldelem(DT_R4)))//byte OperandType.InlineNone           // ldelem.r4
            InlineNone(noPrefixes (I_ldelem(DT_R8)))//byte OperandType.InlineNone           // ldelem.r8
            InlineNone(noPrefixes (I_ldelem(DT_REF)))//byte OperandType.InlineNone           // ldelem.ref
            InlineNone(noPrefixes (I_stelem(DT_I)))//byte OperandType.InlineNone           // stelem.i
            InlineNone(noPrefixes (I_stelem(DT_I1)))//byte OperandType.InlineNone           // stelem.i1
            InlineNone(noPrefixes (I_stelem(DT_I2)))//byte OperandType.InlineNone           // stelem.i2
            InlineNone(noPrefixes (I_stelem(DT_I4)))//byte OperandType.InlineNone           // stelem.i4
            InlineNone(noPrefixes (I_stelem(DT_I8)))//byte OperandType.InlineNone           // stelem.i8
            InlineNone(noPrefixes (I_stelem(DT_R4)))//byte OperandType.InlineNone           // stelem.r4
            InlineNone(noPrefixes (I_stelem(DT_R8)))//byte OperandType.InlineNone           // stelem.r8
            InlineNone(noPrefixes (I_stelem(DT_REF)))//byte OperandType.InlineNone           // stelem.ref
            InlineType(noPrefixes (fun ilType -> I_ldelem_any(ILArrayShape.SingleDimensional, ilType)))//byte OperandType.InlineType           // ldelem
            InlineType(noPrefixes (fun ilType -> I_stelem_any(ILArrayShape.SingleDimensional, ilType)))//byte OperandType.InlineType           // stelem
            InlineType(noPrefixes (fun ilType -> I_unbox_any(ilType)))//byte OperandType.InlineType           // unbox.any
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            InlineNone(noPrefixes (AI_conv_ovf(DT_I1)))//byte OperandType.InlineNone           // conv.ovf.i1
            InlineNone(noPrefixes (AI_conv_ovf(DT_U1)))//byte OperandType.InlineNone           // conv.ovf.u1
            InlineNone(noPrefixes (AI_conv_ovf(DT_I2)))//byte OperandType.InlineNone           // conv.ovf.i2
            InlineNone(noPrefixes (AI_conv_ovf(DT_U2)))//byte OperandType.InlineNone           // conv.ovf.u2
            InlineNone(noPrefixes (AI_conv_ovf(DT_I4)))//byte OperandType.InlineNone           // conv.ovf.i4
            InlineNone(noPrefixes (AI_conv_ovf(DT_U4)))//byte OperandType.InlineNone           // conv.ovf.u4
            InlineNone(noPrefixes (AI_conv_ovf(DT_I8)))//byte OperandType.InlineNone           // conv.ovf.i8
            InlineNone(noPrefixes (AI_conv_ovf(DT_U8)))//byte OperandType.InlineNone           // conv.ovf.u8
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            InlineType(noPrefixes (fun ilType -> I_refanyval(ilType)))//byte OperandType.InlineType           // refanyval
            InlineNone(noPrefixes AI_ckfinite)//byte OperandType.InlineNone           // ckfinite
            NoDecoder
            NoDecoder
            InlineType(noPrefixes (fun ilType -> I_mkrefany(ilType)))//byte OperandType.InlineType           // mkrefany
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            InlineTok(noPrefixes (fun ilToken -> I_ldtoken(ilToken)))//byte OperandType.InlineTok            // ldtoken
            InlineNone(noPrefixes (AI_conv(DT_U2)))//byte OperandType.InlineNone           // conv.u2
            InlineNone(noPrefixes (AI_conv(DT_U1)))//byte OperandType.InlineNone           // conv.u1
            InlineNone(noPrefixes (AI_conv(DT_I)))//byte OperandType.InlineNone           // conv.i
            InlineNone(noPrefixes (AI_conv_ovf(DT_I)))//byte OperandType.InlineNone           // conv.ovf.i
            InlineNone(noPrefixes (AI_conv_ovf(DT_U)))//byte OperandType.InlineNone           // conv.ovf.u
            InlineNone(noPrefixes AI_add_ovf)//byte OperandType.InlineNone           // add.ovf
            InlineNone(noPrefixes AI_add_ovf_un)//byte OperandType.InlineNone           // add.ovf.un
            InlineNone(noPrefixes AI_mul_ovf)//byte OperandType.InlineNone           // mul.ovf
            InlineNone(noPrefixes AI_mul_ovf_un)//byte OperandType.InlineNone           // mul.ovf.un
            InlineNone(noPrefixes AI_sub_ovf)//byte OperandType.InlineNone           // sub.ovf
            InlineNone(noPrefixes AI_sub_ovf_un)//byte OperandType.InlineNone           // sub.ovf.un
            InlineNone(noPrefixes I_endfinally)//byte OperandType.InlineNone           // endfinally
            InlineBrTarget(noPrefixes (fun value -> I_leave(value))) // leave
            ShortInlineBrTarget(noPrefixes (fun value -> I_leave(int value)))//byte OperandType.ShortInlineBrTarget  // leave.s
            InlineNone(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) -> I_stind(ilAlignment, ilVolatility, DT_I)))//byte OperandType.InlineNone           // stind.i
            InlineNone(noPrefixes (AI_conv(DT_U)))//byte OperandType.InlineNone           // conv.u            (0xe0)
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
            NoDecoder
        |]

    let TwoByteDecoders =
        [|
            InlineNone(noPrefixes I_arglist)//(byte)OperandType.InlineNone,           // arglist           (0xfe 0x00)
            InlineNone(noPrefixes AI_ceq)//(byte)OperandType.InlineNone,           // ceq
            InlineNone(noPrefixes AI_cgt)//(byte)OperandType.InlineNone,           // cgt
            InlineNone(noPrefixes AI_cgt_un)//(byte)OperandType.InlineNone,           // cgt.un
            InlineNone(noPrefixes AI_clt)//(byte)OperandType.InlineNone,           // clt
            InlineNone(noPrefixes AI_clt_un)//(byte)OperandType.InlineNone,           // clt.un
            InlineMethod(noPrefixes (fun (ilMethSpec, _) -> I_ldftn(ilMethSpec)))//(byte)OperandType.InlineMethod,         // ldftn
            InlineMethod(noPrefixes (fun (ilMethSpec, _) -> I_ldvirtftn(ilMethSpec)))//(byte)OperandType.InlineMethod,         // ldvirtftn
            NoDecoder
            InlineVar(noPrefixes (fun index -> I_ldarg(index)))//(byte)OperandType.InlineVar,            // ldarg
            InlineVar(noPrefixes (fun index -> I_ldarga(index)))//(byte)OperandType.InlineVar,            // ldarga
            InlineVar(noPrefixes (fun index -> I_starg(index)))//(byte)OperandType.InlineVar,            // starg
            InlineVar(noPrefixes (fun index -> I_ldloc(index)))//(byte)OperandType.InlineVar,            // ldloc
            InlineVar(noPrefixes (fun index -> I_ldloca(index)))//(byte)OperandType.InlineVar,            // ldloca
            InlineVar(noPrefixes (fun index -> I_stloc(index)))//(byte)OperandType.InlineVar,            // stloc
            InlineNone(noPrefixes I_localloc)//(byte)OperandType.InlineNone,           // localloc
            NoDecoder
            InlineNone(noPrefixes I_endfilter)//(byte)OperandType.InlineNone,           // endfilter
            PrefixShortInlineI(fun prefixes value -> prefixes.al <- match value with | 1us -> Unaligned1 | 2us -> Unaligned2 | 4us -> Unaligned4 | _ -> (* possible warning? *) Aligned)//(byte)OperandType.ShortInlineI,         // unaligned.
            PrefixInlineNone(fun prefixes -> prefixes.vol <- Volatile)//(byte)OperandType.InlineNone,           // volatile.
            PrefixInlineNone(fun prefixes -> prefixes.tl <- Tailcall)//(byte)OperandType.InlineNone,           // tail.
            InlineType(noPrefixes (fun ilType -> I_initobj(ilType)))//(byte)OperandType.InlineType,           // initobj
            PrefixInlineType(fun prefixes ilType -> prefixes.constrained <- Some(ilType))//(byte)OperandType.InlineType,           // constrained.
            InlineNone(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) -> I_cpblk(ilAlignment, ilVolatility)))//(byte)OperandType.InlineNone,           // cpblk
            InlineNone(volatileOrUnalignedPrefix (fun (ilAlignment, ilVolatility) -> I_initblk(ilAlignment, ilVolatility)))//(byte)OperandType.InlineNone,           // initblk
            NoDecoder
            InlineNone(noPrefixes I_rethrow)//(byte)OperandType.InlineNone,           // rethrow
            NoDecoder
            InlineType(noPrefixes (fun ilType -> I_sizeof(ilType)))//(byte)OperandType.InlineType,           // sizeof
            InlineNone(noPrefixes I_refanytype)//(byte)OperandType.InlineNone,           // refanytype
            PrefixInlineNone(fun prefixes -> prefixes.ro <- ReadonlyAddress)//(byte)OperandType.InlineNone,           // readonly.         (0xfe 0x1e)
        |]

    let readILOperandDecoder (ilReader: byref<BlobReader>) : ILOperandDecoder =
        let operation = int (ilReader.ReadByte())
        if operation = 0xfe then TwoByteDecoders.[int (ilReader.ReadByte())]
        else OneByteDecoders.[operation]

    let readILInstrs (cenv: cenv) typarOffset (ilReader: byref<BlobReader>) =
        let mdReader = cenv.MetadataReader

        let instrs = ResizeArray()

        let prefixes =
            {
                al = Aligned
                tl = Normalcall
                vol = Nonvolatile
                ro = NormalAddress
                constrained = None
            }

        let labelsOfRawOffsets = Dictionary() 
        let ilOffsetsOfLabels = Dictionary()
        let tryRawToLabel rawOffset =
            match labelsOfRawOffsets.TryGetValue rawOffset with
            | true, v -> Some v
            | _ -> None

        let rawToLabel rawOffset = 
            match tryRawToLabel rawOffset with 
            | Some l -> l
            | None -> 
                let lab = generateCodeLabel()
                labelsOfRawOffsets.[rawOffset] <- lab
                lab

        let markAsInstructionStart rawOffset ilOffset = 
            let lab = rawToLabel rawOffset
            ilOffsetsOfLabels.[lab] <- ilOffset

        while ilReader.RemainingBytes > 0 do

            markAsInstructionStart ilReader.Offset instrs.Count

            match readILOperandDecoder &ilReader with
            | PrefixInlineNone(f) -> f prefixes
            | PrefixShortInlineI(f) -> f prefixes (ilReader.ReadUInt16())
            | PrefixInlineType(f) ->
                let handle = MetadataTokens.EntityHandle(ilReader.ReadInt32())
                let ilType = readILType cenv typarOffset SignatureTypeKind.Class (* original reader assumed object, it's ok *) handle
                f prefixes ilType

            | decoder ->
                let instr =
                    match decoder with
                    | NoDecoder -> failwith "Bad IL reading format"
                    | InlineNone(f) -> f prefixes
                    | ShortInlineVar(f) -> f prefixes (ilReader.ReadByte())
                    | ShortInlineI(f) -> f prefixes (ilReader.ReadSByte())
                    | InlineI(f) -> f prefixes (ilReader.ReadInt32())
                    | InlineI8(f) -> f prefixes (ilReader.ReadInt64())
                    | ShortInlineR(f) -> f prefixes (ilReader.ReadSingle())
                    | InlineR(f) -> f prefixes (ilReader.ReadDouble())

                    | InlineMethod(f) ->
                        let handle = MetadataTokens.EntityHandle(ilReader.ReadInt32())

                        match readDeclaringTypeInfoFromMemberOrMethod cenv typarOffset handle with
                        | name, ILType.Array(shape, ilType) ->
                            match name with
                            | "Get" -> I_ldelem_any(shape, ilType)
                            | "Set" -> I_stelem_any(shape, ilType)
                            | "Address" -> I_ldelema(prefixes.ro, false, shape, ilType)
                            | ".ctor" -> I_newarr(shape, ilType)
                            | _ -> failwith "Bad method on array type"
                        | _ ->
                            let ilMethSpec = readILMethodSpec cenv typarOffset handle
                            f prefixes (ilMethSpec, None)

                    | InlineSig(f) ->
                        let handle = MetadataTokens.EntityHandle(ilReader.ReadInt32())
                        let ilMethSpec = readILMethodSpec cenv typarOffset handle
                        let ilVarArgs =
                            match ilMethSpec.GenericArgs with
                            | [] -> None
                            | xs -> Some(xs)
                        f prefixes (ilMethSpec.MethodRef.CallingSignature, ilVarArgs)

                    | ShortInlineBrTarget(f) -> 
                        let offset = ilReader.ReadSByte()
                        f prefixes (rawToLabel (ilReader.Offset + int offset))
                    | InlineBrTarget(f) -> 
                        let offset = ilReader.ReadInt32()
                        f prefixes (rawToLabel (ilReader.Offset + offset))

                    | InlineSwitch(f) ->
                        let deltas = Array.zeroCreate (ilReader.ReadInt32())
                        for i = 0 to deltas.Length - 1 do 
                            deltas.[i] <- ilReader.ReadInt32()
                        for i = 0 to deltas.Length - 1 do
                            deltas.[i] <- rawToLabel (ilReader.Offset + deltas.[i])
                        f prefixes (deltas |> List.ofArray)

                    | InlineType(f) ->
                        let handle = MetadataTokens.EntityHandle(ilReader.ReadInt32())
                        let ilType = readILType cenv typarOffset SignatureTypeKind.Class (* original reader assumed object, it's ok *) handle
                        f prefixes ilType
                    
                    | InlineString(f) ->
                        let handle = MetadataTokens.Handle(ilReader.ReadInt32())
                        let value =
                            match handle.Kind with
                            | HandleKind.String -> readString cenv (StringHandle.op_Explicit(handle))
                            | HandleKind.UserString -> mdReader.GetUserString(UserStringHandle.op_Explicit(handle))
                            | _ -> failwithf "Invalid Handle Kind: %A" handle.Kind
                        f prefixes value

                    | InlineField(f) ->
                        let handle = MetadataTokens.EntityHandle(ilReader.ReadInt32())
                        let ilFieldSpec = readILFieldSpec cenv typarOffset handle
                        f prefixes ilFieldSpec

                    | InlineTok(f) ->
                        let handle = MetadataTokens.EntityHandle(ilReader.ReadInt32())

                        let ilToken =
                            match handle.Kind with
                            | HandleKind.MethodDefinition
                            | HandleKind.MemberReference -> ILToken.ILMethod(readILMethodSpec cenv typarOffset handle)
                            | HandleKind.FieldDefinition -> ILToken.ILField(readILFieldSpec cenv typarOffset handle)
                            | HandleKind.TypeDefinition
                            | HandleKind.TypeReference
                            | HandleKind.TypeSpecification -> ILToken.ILType(readILType cenv typarOffset SignatureTypeKind.Class (* original reader assumed object, it's ok *) handle)
                            | _ -> failwithf "Invalid Handle Kind: %A" handle.Kind

                        f prefixes ilToken

                    | InlineVar(f) -> f prefixes (ilReader.ReadUInt16())
                    | _ -> failwith "Incorrect IL reading decoder at this point"

                instrs.Add(instr)

                // Reset prefixes
                prefixes.al <- Aligned
                prefixes.tl <- Normalcall
                prefixes.vol <- Nonvolatile
                prefixes.ro <- NormalAddress
                prefixes.constrained <- None

        // Finished reading instructions - mark the end of the instruction stream in case the PDB information refers to it. 
        markAsInstructionStart ilReader.Offset instrs.Count
        // Build the function that maps from raw labels (offsets into the bytecode stream) to indexes in the AbsIL instruction stream 
        let lab2pc = ilOffsetsOfLabels

        // Some offsets used in debug info refer to the end of an instruction, rather than the 
        // start of the subsequent instruction. But all labels refer to instruction starts, 
        // apart from a final label which refers to the end of the method. This function finds 
        // the start of the next instruction referred to by the raw offset. 
        let raw2nextLab rawOffset = 
            let isInstrStart x = 
                match tryRawToLabel x with 
                | None -> false
                | Some lab -> ilOffsetsOfLabels.ContainsKey lab
            if isInstrStart rawOffset then rawToLabel rawOffset 
            elif isInstrStart (rawOffset+1) then rawToLabel (rawOffset+1)
            else failwith ("the bytecode raw offset "+string rawOffset+" did not refer either to the start or end of an instruction")

        instrs.ToArray(), rawToLabel, lab2pc, raw2nextLab

    // --------------------------------------------------------------------

    let decodeLocalSignature (cenv: cenv) (mdReader: MetadataReader) typarOffset localSignature =
        let si = mdReader.GetStandaloneSignature localSignature
        si.DecodeLocalSignature(cenv.LocalSignatureTypeProvider, typarOffset)
        |> List.ofSeq

    let readILLocalDebugInfo (pdbReader: MetadataReader) (debugInfoHandle: MethodDebugInformationHandle) (raw2nextLab: int -> ILCodeLabel) =
        let localScopes = pdbReader.GetLocalScopes debugInfoHandle |> Seq.map pdbReader.GetLocalScope
        localScopes 
        |> Seq.map (fun localScope ->
            {
                Range = (raw2nextLab localScope.StartOffset, raw2nextLab localScope.EndOffset)
                DebugMappings = 
                    localScope.GetLocalVariables()
                    |> Seq.choose (fun handle -> 
                        let x = pdbReader.GetLocalVariable handle
                        if x.Attributes &&& LocalVariableAttributes.DebuggerHidden <> LocalVariableAttributes.DebuggerHidden then
                            Some({ LocalIndex = x.Index; LocalName = pdbReader.GetString x.Name } : ILLocalDebugMapping)
                        else
                            None)
                    |> List.ofSeq
            } : ILLocalDebugInfo)
        |> List.ofSeq

    let readMethodDebugInfo (cenv: cenv) (methDef: MethodDefinition) (raw2nextLab: int -> ILCodeLabel) =
        match cenv.PdbReaderProvider with
        | Some(readerProvider, _) ->
            let pdbReader = readerProvider.GetMetadataReader()
            let debugInfoOpt = 
                pdbReader.MethodDebugInformation 
                |> Seq.tryPick (fun handle -> 
                    if handle.IsNil then None 
                    else
                        let debugInfo = pdbReader.GetMethodDebugInformation handle
                        let doc = pdbReader.GetDocument debugInfo.Document
                        if pdbReader.GetString doc.Name = readString cenv methDef.Name then
                            Some handle
                        else
                            None)

            match debugInfoOpt with
            | Some debugInfoHandle when not debugInfoHandle.IsNil ->
                readILLocalDebugInfo pdbReader debugInfoHandle raw2nextLab
            | _ ->
                List.empty
        | _ ->
            List.empty

    let readILCode (cenv: cenv) typarOffset (_methDef: MethodDefinition) (methBodyBlock: MethodBodyBlock) : ILCode =
        let mutable ilReader = methBodyBlock.GetILReader()
        let instrs, rawToLabel, lab2pc, _raw2nextLab = readILInstrs cenv typarOffset &ilReader

        let exceptions =
            methBodyBlock.ExceptionRegions
            |> Seq.map (fun region ->
                let start = rawToLabel region.HandlerOffset
                let finish = rawToLabel (region.HandlerOffset + region.HandlerLength)
                let clause =
                    match region.Kind with
                    | ExceptionRegionKind.Finally ->
                        ILExceptionClause.Finally(start, finish)
                    | ExceptionRegionKind.Fault ->
                        ILExceptionClause.Fault(start, finish)
                    | ExceptionRegionKind.Filter ->
                        let filterStart = rawToLabel region.FilterOffset
                        let filterFinish = rawToLabel region.HandlerOffset
                        ILExceptionClause.FilterCatch((filterStart, filterFinish), (start, finish))
                    | ExceptionRegionKind.Catch ->
                        ILExceptionClause.TypeCatch(readILType cenv typarOffset SignatureTypeKind.Class (* original reader assumed object, it's ok *) region.CatchType, (start, finish))
                    | _ ->
                        failwithf "Invalid Exception Region Kind: %A" region.Kind

                {
                    ILExceptionSpec.Range = (rawToLabel region.TryOffset, rawToLabel (region.TryOffset + region.TryLength))
                    ILExceptionSpec.Clause = clause
                }
            )
            |> List.ofSeq

        {
            Labels = lab2pc
            Instrs = instrs
            Exceptions = exceptions
            Locals = [] // readMethodDebugInfo cenv methDef raw2nextLab - does not work yet and didn't in the original reader
        }

    let readILMethodBody (cenv: cenv) (peReader: PEReader) typarOffset (methDef: MethodDefinition) : ILMethodBody =
        let mdReader = cenv.MetadataReader

        let methBodyBlock = peReader.GetMethodBody(methDef.RelativeVirtualAddress)

        let ilLocals =
            if methBodyBlock.LocalSignature.IsNil then []
            else decodeLocalSignature cenv mdReader typarOffset methBodyBlock.LocalSignature

        let ilCode = readILCode cenv typarOffset methDef methBodyBlock
    
        {
            IsZeroInit = methBodyBlock.LocalVariablesInitialized
            MaxStack = methBodyBlock.MaxStack
            NoInlining = int (methDef.ImplAttributes &&& MethodImplAttributes.NoInlining) <> 0
            AggressiveInlining = int (methDef.ImplAttributes &&& MethodImplAttributes.AggressiveInlining) <> 0
            Locals = ilLocals
            Code = ilCode
            SourceMarker = None // Note: The original reader never set this.
        }

    let lazyReadMethodBody (cenv: cenv) typarOffset (methDef: MethodDefinition) =
        let mdReader = cenv.MetadataReader
        let attrs = methDef.Attributes
        let implAttrs = methDef.ImplAttributes

        let codeType = implAttrs &&& MethodImplAttributes.CodeTypeMask
        let isPinvoke = int (attrs &&& MethodAttributes.PinvokeImpl) <> 0
        let isAbstract = int (attrs &&& MethodAttributes.Abstract) <> 0
        let isInternalCall = int (implAttrs &&& MethodImplAttributes.InternalCall) <> 0
        let isUnmanaged = int (implAttrs &&& MethodImplAttributes.Unmanaged) <> 0

        if codeType = MethodImplAttributes.Native && isPinvoke then
            methBodyNative
        elif isPinvoke then
            mkMethBodyLazyAux (
                lazy
                    let import = methDef.GetImport()
                    let importAttrs = import.Attributes
                    let pInvokeMethod : PInvokeMethod =
                        {
                            Where = readILModuleRefFromModuleReference cenv (mdReader.GetModuleReference(import.Module))
                            Name = readString cenv import.Name
                            CallingConv = mkPInvokeCallingConvention importAttrs
                            CharEncoding = mkPInvokeCharEncoding importAttrs
                            NoMangle = int (importAttrs &&& MethodImportAttributes.ExactSpelling) <> 0
                            LastError = int (importAttrs &&& MethodImportAttributes.SetLastError) <> 0
                            ThrowOnUnmappableChar = mkPInvokeThrowOnUnmappableChar importAttrs
                            CharBestFit = mkPInvokeCharBestFit importAttrs
                        }
                    MethodBody.PInvoke(pInvokeMethod))
        elif isInternalCall || isAbstract || isUnmanaged || codeType <> MethodImplAttributes.IL then
            methBodyAbstract
        else
            match cenv.TryPEReader with
            | Some peReader ->
                mkMethBodyLazyAux(lazy MethodBody.IL(readILMethodBody cenv peReader typarOffset methDef))
            | _ ->
                methBodyNotAvailable

    let readILMethodDef (cenv: cenv) (methDefHandle: MethodDefinitionHandle) : ILMethodDef =
        match cenv.TryGetCachedILMethodDef methDefHandle with
        | ValueSome ilMethDef -> ilMethDef
        | _ ->
            let mdReader = cenv.MetadataReader

            let methDef = mdReader.GetMethodDefinition(methDefHandle)
            let typarOffset = readDeclaringTypeGenericCountFromMethodDefinition cenv methDef
            let si = methDef.DecodeSignature(cenv.SignatureTypeProvider, typarOffset)

            let name = readString cenv methDef.Name

            let isEntryPoint =
                let handle = MetadataTokens.MethodDefinitionHandle cenv.EntryPointToken
                handle = methDefHandle

            let ret, parameters = readILParameters cenv typarOffset si methDef

            let ilMethDef =
                ILMethodDef(
                    name = name,
                    attributes = methDef.Attributes,
                    implAttributes = methDef.ImplAttributes,
                    callingConv = mkILCallingConv si.Header,
                    parameters = parameters,
                    ret = ret,
                    body = lazyReadMethodBody cenv typarOffset methDef,
                    isEntryPoint = isEntryPoint,
                    genericParams = readILGenericParameterDefs cenv typarOffset (methDef.GetGenericParameters()),
                    securityDeclsStored = readILSecurityDeclsStored cenv (methDef.GetDeclarativeSecurityAttributes()),
                    customAttrsStored = readILAttributesStored cenv typarOffset (methDef.GetCustomAttributes()),
                    metadataIndex = MetadataTokens.GetRowNumber(MethodDefinitionHandle.op_Implicit methDefHandle))
            cenv.CacheILMethodDef(methDefHandle, ilMethDef)
            ilMethDef

    let readILFieldDef (cenv: cenv) typarOffset (fieldDefHandle: FieldDefinitionHandle) : ILFieldDef =
        let mdReader = cenv.MetadataReader

        let fieldDef = mdReader.GetFieldDefinition(fieldDefHandle)

        let data = 
            match cenv.TryPEReader with
            | Some peReader when int (fieldDef.Attributes &&& FieldAttributes.HasFieldRVA) <> 0 ->
                let mutable blobReader = peReader.GetSectionData(fieldDef.GetRelativeVirtualAddress()).GetReader()
                Some(blobReader.ReadBytes(blobReader.Length))
            | _ ->
                None

        let literalValue =
            if int (fieldDef.Attributes &&& FieldAttributes.HasDefault) <> 0 then
                tryReadILFieldInit cenv (fieldDef.GetDefaultValue())
            else
                None

        let offset =
            let isStatic = int (fieldDef.Attributes &&& FieldAttributes.Static) <> 0
            if not isStatic then
                let offset = fieldDef.GetOffset()
                if offset = -1 then None else Some(offset)
            else
                None
            
        let marshal =
            if int (fieldDef.Attributes &&& FieldAttributes.HasFieldMarshal) <> 0 then 
                tryReadILNativeType cenv (fieldDef.GetMarshallingDescriptor())
            else
                None

        ILFieldDef(
            name = readString cenv fieldDef.Name,
            fieldType = fieldDef.DecodeSignature(cenv.SignatureTypeProvider, typarOffset),
            attributes = fieldDef.Attributes,
            data = data,
            literalValue = literalValue,
            offset = offset,
            marshal = marshal,
            customAttrsStored = readILAttributesStored cenv typarOffset (fieldDef.GetCustomAttributes()),
            metadataIndex = MetadataTokens.GetRowNumber(FieldDefinitionHandle.op_Implicit fieldDefHandle))

    let readILFieldSpec (cenv: cenv) typarOffset (handle: EntityHandle) : ILFieldSpec =
        let mdReader = cenv.MetadataReader

        match handle.Kind with
        | HandleKind.FieldDefinition ->
            let fieldDef = mdReader.GetFieldDefinition(FieldDefinitionHandle.op_Explicit(handle))

            let declaringILType = readILTypeFromTypeDefinition cenv SignatureTypeKind.Class (* original reader assumed object, it's ok *) (fieldDef.GetDeclaringType())

            let ilFieldRef =
                {
                    DeclaringTypeRef = declaringILType.TypeRef
                    Name = readString cenv fieldDef.Name
                    Type = fieldDef.DecodeSignature(cenv.SignatureTypeProvider, typarOffset)
                }

            {
                FieldRef = ilFieldRef
                DeclaringType = declaringILType
            }

        | HandleKind.MemberReference ->
            let memberRef = mdReader.GetMemberReference(MemberReferenceHandle.op_Explicit(handle))

            let declaringType = readILType cenv typarOffset SignatureTypeKind.Class (* original reader assumed object, it's ok *) memberRef.Parent

            let ilFieldRef =
                {
                    DeclaringTypeRef = declaringType.TypeRef
                    Name = readString cenv memberRef.Name
                    Type = memberRef.DecodeFieldSignature(cenv.SignatureTypeProvider, typarOffset)
                }

            {
                FieldRef = ilFieldRef
                DeclaringType = declaringType
            }

        | _ -> failwithf "Invalid Handle Kind: %A" handle.Kind

    let readILFieldDefs (cenv: cenv) typarOffset (fieldDefHandles: FieldDefinitionHandleCollection) =
        let f =
            lazy
                fieldDefHandles
                |> Seq.map (readILFieldDef cenv typarOffset)
                |> List.ofSeq
        mkILFieldsLazy f

    let readILPropertyDef (cenv: cenv) typarOffset (propDefHandle: PropertyDefinitionHandle) =
        let mdReader = cenv.MetadataReader

        let propDef = mdReader.GetPropertyDefinition propDefHandle
        let accessors = propDef.GetAccessors()

        let getMethod =
            if accessors.Getter.IsNil then None
            else
                let spec = readILMethodSpec cenv typarOffset (MethodDefinitionHandle.op_Implicit accessors.Getter)
                Some spec.MethodRef

        let setMethod =
            if accessors.Setter.IsNil then None
            else
                let spec = readILMethodSpec cenv typarOffset (MethodDefinitionHandle.op_Implicit accessors.Setter)
                Some spec.MethodRef

        let init =
            if (propDef.Attributes &&& PropertyAttributes.HasDefault) = PropertyAttributes.HasDefault then
                tryReadILFieldInit cenv (propDef.GetDefaultValue())
            else
                None

        let typarOffset =
            let methDefHandle =
                if accessors.Getter.IsNil then
                    if accessors.Setter.IsNil then
                        invalidOp "readILPropertyDef: Property definition read with no getter or setter."
                    else
                        accessors.Setter
                else
                    accessors.Getter
            let methDef = mdReader.GetMethodDefinition methDefHandle
            readDeclaringTypeGenericCountFromMethodDefinition cenv methDef
        let si = propDef.DecodeSignature(cenv.SignatureTypeProvider, typarOffset)
        let args = si.ParameterTypes |> List.ofSeq

        (* NOTE: the "ThisConv" value on the property is not reliable: better to look on the getter/setter *)
        let ilThisConv =
            match getMethod with
            | Some mref -> mref.CallingConv.ThisConv
            | _ ->
                match setMethod with
                | Some mref -> mref.CallingConv.ThisConv
                | _ -> mkILThisConvention si.Header

        ILPropertyDef(
            name = readString cenv propDef.Name,
            attributes = propDef.Attributes,
            setMethod = setMethod,
            getMethod = getMethod,
            callingConv = ilThisConv,
            propertyType = si.ReturnType,
            init = init,
            args = args,
            customAttrsStored = readILAttributesStored cenv typarOffset (propDef.GetCustomAttributes()),
            metadataIndex = MetadataTokens.GetRowNumber(PropertyDefinitionHandle.op_Implicit propDefHandle))

    let readILPropertyDefs (cenv: cenv) typarOffset (propDefHandles: PropertyDefinitionHandleCollection) =
        let f =
            lazy
                propDefHandles
                |> Seq.map (readILPropertyDef cenv typarOffset)
                |> List.ofSeq
        mkILPropertiesLazy f

    let readILOverridesSpec (cenv: cenv) typarOffset (handle: EntityHandle) =
        let ilMethSpec = readILMethodSpec cenv typarOffset handle
        OverridesSpec(ilMethSpec.MethodRef, ilMethSpec.DeclaringType)

    let readILMethodImpl (cenv: cenv) typarOffset (methImplHandle: MethodImplementationHandle) =
        let mdReader = cenv.MetadataReader

        let methImpl = mdReader.GetMethodImplementation(methImplHandle)

        {
            OverrideBy = readILMethodSpec cenv typarOffset methImpl.MethodBody
            Overrides = readILOverridesSpec cenv typarOffset methImpl.MethodDeclaration
        }

    let readILMethodImpls (cenv: cenv) typarOffset (methImplHandles: MethodImplementationHandleCollection) =
        let f =
            lazy
                methImplHandles
                |> Seq.map (readILMethodImpl cenv typarOffset)
                |> List.ofSeq
        mkILMethodImplsLazy f

    let readILMethodRef (cenv: cenv) typarOffset (handle: EntityHandle) =
        (readILMethodSpec cenv typarOffset handle).MethodRef

    let tryReadILMethodRef (cenv: cenv) typarOffset (handle: EntityHandle) =
        if handle.IsNil then None
        else
            readILMethodRef cenv typarOffset handle
            |> Some

    let tryReadILType (cenv: cenv) typarOffset (handle: EntityHandle) =
        if handle.IsNil then None
        else
            readILType cenv typarOffset SignatureTypeKind.Class (* original reader assumed object, it's ok *) handle
            |> Some

    let readILEventDef (cenv: cenv) typarOffset (eventDefHandle: EventDefinitionHandle) =
        let mdReader = cenv.MetadataReader

        let eventDef = mdReader.GetEventDefinition eventDefHandle
        let accessors = eventDef.GetAccessors()

        let otherMethods = accessors.Others |> Seq.map (fun h -> readILMethodRef cenv typarOffset (MethodDefinitionHandle.op_Implicit h)) |> List.ofSeq

        ILEventDef(
            eventType = tryReadILType cenv typarOffset eventDef.Type,
            name = readString cenv eventDef.Name,
            attributes = eventDef.Attributes,
            addMethod = readILMethodRef cenv typarOffset (MethodDefinitionHandle.op_Implicit accessors.Adder),
            removeMethod = readILMethodRef cenv typarOffset (MethodDefinitionHandle.op_Implicit accessors.Remover),
            fireMethod = tryReadILMethodRef cenv typarOffset (MethodDefinitionHandle.op_Implicit accessors.Raiser),
            otherMethods = otherMethods,
            customAttrsStored = readILAttributesStored cenv typarOffset (eventDef.GetCustomAttributes()),
            metadataIndex = MetadataTokens.GetRowNumber(EventDefinitionHandle.op_Implicit eventDefHandle))
 
    let readILEventDefs (cenv: cenv) typarOffset (eventDefHandles: EventDefinitionHandleCollection) =
        let f =
            lazy
                eventDefHandles
                |> Seq.map (readILEventDef cenv typarOffset)
                |> List.ofSeq
        mkILEventsLazy f

    let rec readILTypeDef (cenv: cenv) (typeDefHandle: TypeDefinitionHandle) : ILTypeDef =
        let mdReader = cenv.MetadataReader

        let typeDef = mdReader.GetTypeDefinition typeDefHandle

        let name = readTypeName cenv typeDef.Namespace typeDef.Name

        let implements =
            typeDef.GetInterfaceImplementations()
            |> Seq.map (fun h ->
                let interfaceImpl = mdReader.GetInterfaceImplementation h
                readILType cenv 0 SignatureTypeKind.Class (* original reader assumed object, it's ok *) interfaceImpl.Interface)
            |> List.ofSeq

        let genericParams = readILGenericParameterDefs cenv 0 (typeDef.GetGenericParameters())

        let extends =
            if typeDef.BaseType.IsNil then None
            else Some(readILType cenv 0 SignatureTypeKind.Class (* original reader assumed object, it's ok *) typeDef.BaseType)

        let methods =
            mkILMethodsComputed (fun () ->
                let methDefHandles = typeDef.GetMethods()
                let ilMethodDefs = Array.zeroCreate methDefHandles.Count

                let mutable i = 0
                for methDefHandle in methDefHandles do
                    ilMethodDefs.[i] <- readILMethodDef cenv methDefHandle
                    i <- i + 1

                ilMethodDefs)

        let nestedTypes =
            mkILTypeDefsComputed (fun () ->
                typeDef.GetNestedTypes()
                |> Seq.map (readILPreTypeDef cenv)
                |> Array.ofSeq)

        let ilTypeDefLayout = mkILTypeDefLayout typeDef.Attributes (typeDef.GetLayout())

        ILTypeDef(
            name = name,
            attributes = typeDef.Attributes,
            layout = ilTypeDefLayout,
            implements = implements,
            genericParams = genericParams,
            extends = extends,
            methods = methods,
            nestedTypes = nestedTypes,
            fields = readILFieldDefs cenv 0 (typeDef.GetFields()),
            methodImpls = readILMethodImpls cenv 0 (typeDef.GetMethodImplementations()),
            events = readILEventDefs cenv 0 (typeDef.GetEvents()),
            properties = readILPropertyDefs cenv 0 (typeDef.GetProperties()),
            securityDeclsStored = readILSecurityDeclsStored cenv (typeDef.GetDeclarativeSecurityAttributes()),
            customAttrsStored = readILAttributesStored cenv 0 (typeDef.GetCustomAttributes()),
            metadataIndex = MetadataTokens.GetRowNumber(TypeDefinitionHandle.op_Implicit(typeDefHandle)))

    let readILPreTypeDef (cenv: cenv) (typeDefHandle: TypeDefinitionHandle) =
        let mdReader = cenv.MetadataReader

        let typeDef = mdReader.GetTypeDefinition typeDefHandle

        let namespaceOpt = 
            if typeDef.Namespace.IsNil then ValueNone
            else
                let str = readString cenv typeDef.Namespace
                if String.IsNullOrEmpty str then ValueNone
                else ValueSome str

        let name = readString cenv typeDef.Name

        let namespaceSplit =
            match namespaceOpt with
            | ValueNone -> []
            | ValueSome namespac -> splitNamespace namespac

        mkILPreTypeDefComputed (namespaceSplit, name, (fun () -> readILTypeDef cenv typeDefHandle))

    let readILPreTypeDefs (cenv: cenv) = 
        let mdReader = cenv.MetadataReader

        [| for typeDefHandle in mdReader.TypeDefinitions do
            let typeDef = mdReader.GetTypeDefinition(typeDefHandle)
            // Only get top types.
            if not typeDef.IsNested then
                yield readILPreTypeDef cenv typeDefHandle |]

    let readILResources (cenv: cenv) (peReader: PEReader) =
        let mdReader = cenv.MetadataReader

        mdReader.ManifestResources
        |> Seq.map mdReader.GetManifestResource
        |> Seq.map (fun resource ->
            let location =
                if resource.Implementation.IsNil then
                    let rva = peReader.PEHeaders.CorHeader.ResourcesDirectory.RelativeVirtualAddress
                    let block = peReader.GetSectionData(rva)
                    let mutable reader = block.GetReader()
                    reader.Offset <- int resource.Offset
                    let length = reader.ReadInt32()
                    let mutable reader = block.GetReader(int resource.Offset + 4, length)
                    let byteStorage =
                        let bytes = ByteMemory.FromUnsafePointer(reader.CurrentPointer |> NativePtr.toNativeInt, reader.RemainingBytes, null).AsReadOnly()
                        ByteStorage.FromByteMemoryAndCopy(bytes, useBackingMemoryMappedFile = cenv.CanReduceMemory)
                    ILResourceLocation.Local(byteStorage)
                else
                    match readILScopeRef cenv resource.Implementation with
                    | ILScopeRef.Module mref -> ILResourceLocation.File (mref, int resource.Offset)
                    | ILScopeRef.Assembly aref -> ILResourceLocation.Assembly aref
                    | ILScopeRef.Local -> failwith "Unexpected ILScopeRef.Local"
                    | ILScopeRef.PrimaryAssembly -> failwith "Unexpected ILScopeRef.PrimaryAssembly"

            {
                Name = readString cenv resource.Name
                Location = location
                Access = (if resource.Attributes &&& ManifestResourceAttributes.Public = ManifestResourceAttributes.Public then ILResourceAccess.Public else ILResourceAccess.Private)
                CustomAttrsStored = resource.GetCustomAttributes() |> readILAttributesStored cenv 0
                MetadataIndex = MetadataTokens.GetRowNumber(resource.Implementation)
            })
        |> List.ofSeq
        |> mkILResources

    let readModuleDef (peReader: PEReader) (peReaderCaptured: PEReader option) (reduceMemory: ReduceMemoryFlag) (pdbReaderProviderOpt: PdbReaderProvider option) (mdReader: MetadataReader) =
        let nativeResources = readILNativeResources peReader

        let subsys =
            int16 peReader.PEHeaders.PEHeader.Subsystem

        let subsysversion =
            (int32 peReader.PEHeaders.PEHeader.MajorSubsystemVersion, int32 peReader.PEHeaders.PEHeader.MinorSubsystemVersion)

        let useHighEntropyVA =
            int (peReader.PEHeaders.PEHeader.DllCharacteristics &&& DllCharacteristics.HighEntropyVirtualAddressSpace) <> 0

        let ilOnly =
            int (peReader.PEHeaders.CorHeader.Flags &&& CorFlags.ILOnly) <> 0

        let only32 =
            int (peReader.PEHeaders.CorHeader.Flags &&& CorFlags.Requires32Bit) <> 0

        let is32bitpreferred =
            int (peReader.PEHeaders.CorHeader.Flags &&& CorFlags.Prefers32Bit) <> 0

        let only64 =
            peReader.PEHeaders.CoffHeader.SizeOfOptionalHeader = 240s (* May want to read in the optional header Magic number and check that as well... *)

        let platform = 
            match peReader.PEHeaders.CoffHeader.Machine with
            | Machine.Amd64 -> Some AMD64
            | Machine.IA64 -> Some IA64
            | _ -> Some X86

        let isDll = peReader.PEHeaders.IsDll

        let alignVirt =
            peReader.PEHeaders.PEHeader.SectionAlignment

        let alignPhys =
            peReader.PEHeaders.PEHeader.FileAlignment

        let imageBaseReal = int peReader.PEHeaders.PEHeader.ImageBase

        let entryPointToken = peReader.PEHeaders.CorHeader.EntryPointTokenOrRelativeVirtualAddress

        let moduleDef = mdReader.GetModuleDefinition()
        let ilModuleName = mdReader.GetString moduleDef.Name
        let ilMetadataVersion = mdReader.MetadataVersion
    
        let cenv = 
            let sigTyProvider = SignatureTypeProvider()
            let localSigTyProvider = LocalSignatureTypeProvider()
            let cenv = cenv(peReaderCaptured, mdReader, pdbReaderProviderOpt, entryPointToken, (reduceMemory = ReduceMemoryFlag.Yes), sigTyProvider, localSigTyProvider)
            sigTyProvider.cenv <- cenv
            localSigTyProvider.cenv <- cenv
            cenv

        let ilAsmRefs =
            let asmRefs = mdReader.AssemblyReferences

            let arr = Array.zeroCreate asmRefs.Count
            let mutable i = 0
            for asmRefHandle in asmRefs do
                arr.[i] <- readILAssemblyRefFromAssemblyReference cenv asmRefHandle
                i <- i + 1
            arr |> List.ofArray

        { Manifest = Some(readILAssemblyManifest cenv entryPointToken)
          CustomAttrsStored = readILAttributesStored cenv 0 (moduleDef.GetCustomAttributes())
          MetadataIndex = 1 // Note: The original reader set this to 1.
          Name = ilModuleName
          NativeResources = nativeResources
          TypeDefs = mkILTypeDefsComputed (fun () -> readILPreTypeDefs cenv)
          SubSystemFlags = int32 subsys
          IsILOnly = ilOnly
          SubsystemVersion = subsysversion
          UseHighEntropyVA = useHighEntropyVA
          Platform = platform
          StackReserveSize = None  // TODO - Note: The original reader did not set this and was marked as a TODO.
          Is32Bit = only32
          Is32BitPreferred = is32bitpreferred
          Is64Bit = only64
          IsDLL=isDll
          VirtualAlignment = alignVirt
          PhysicalAlignment = alignPhys
          ImageBase = imageBaseReal
          MetadataVersion = ilMetadataVersion
          Resources = readILResources cenv peReader
        }, ilAsmRefs

type ILReaderOptions =
    { pdbDirPath: string option
      reduceMemoryUsage: ReduceMemoryFlag
      metadataOnly: MetadataOnlyFlag
      tryGetMetadataSnapshot: ILReaderTryGetMetadataSnapshot }

type ILModuleReader =
    abstract ILModuleDef : ILModuleDef
    abstract ILAssemblyRefs : ILAssemblyRef list
    
    /// ILModuleReader objects only need to be explicitly disposed if memory mapping is used, i.e. reduceMemoryUsage = false
    inherit  System.IDisposable

type Statistics = 
    { mutable rawMemoryFileCount : int
      mutable memoryMapFileOpenedCount : int
      mutable memoryMapFileClosedCount : int
      mutable weakByteFileCount : int
      mutable byteFileCount : int }

let defaultStatistics = 
    { rawMemoryFileCount = 0
      memoryMapFileOpenedCount = 0
      memoryMapFileClosedCount = 0
      weakByteFileCount = 0
      byteFileCount = 0 }

let GetStatistics() = defaultStatistics

type ILModuleReaderImpl(peReaderCaptured: PEReader option, holder: obj, ilModuleDef, ilAsmRefs, dispose) =

    member _.Holder = holder

    override _.Finalize() =
        match peReaderCaptured with
        | Some peReader -> peReader.Dispose()
        | _ -> ()

    interface ILModuleReader with

        member _.ILModuleDef = ilModuleDef

        member _.ILAssemblyRefs = ilAsmRefs

    interface IDisposable with

        member _.Dispose() = dispose ()

let aliveReaders = System.Runtime.CompilerServices.ConditionalWeakTable<MetadataReader, ILModuleReader>()

let OpenILModuleReaderAux (peReader: PEReader) (opts: ILReaderOptions) metadataSnapshotOpt =
    let peReaderCaptured, mdReader, snapshotHolder =
        if opts.reduceMemoryUsage = ReduceMemoryFlag.Yes && opts.metadataOnly = MetadataOnlyFlag.Yes then
            match metadataSnapshotOpt with
            | Some(obj, start, len) ->
                None, MetadataReader(NativePtr.ofNativeInt start, len), Some obj
            | _ ->
                Some peReader, peReader.GetMetadataReader(), None
        else
            Some peReader, peReader.GetMetadataReader(), None

    let pdbReaderProviderOpt = 
        opts.pdbDirPath
        |> Option.bind (fun pdbDirPath ->
            let streamProvider = System.Func<_,_>(fun pdbPath -> ByteMemory.FromFile(pdbPath, FileAccess.Read, canShadowCopy=true).AsReadOnlyStream())
            match peReader.TryOpenAssociatedPortablePdb(pdbDirPath, streamProvider) with
            | true, pdbReaderProvider, pdbPath -> Some(pdbReaderProvider, pdbPath)
            | _ -> None)
    let ilModuleDef, ilAsmRefs = readModuleDef peReader peReaderCaptured opts.reduceMemoryUsage pdbReaderProviderOpt mdReader

    let disposePdbReader = fun () -> match pdbReaderProviderOpt with Some (provider, _) -> provider.Dispose() | _ -> ()
    let dispose =
        // If we are not capturing the PEReader, then we will dispose of it.
        if peReaderCaptured.IsNone then
            disposePdbReader ()
            peReader.Dispose()
            id
        else
            disposePdbReader

    let holder = (snapshotHolder, mdReader)
    let reader = new ILModuleReaderImpl(peReaderCaptured, holder, ilModuleDef, ilAsmRefs, dispose) :> ILModuleReader
    aliveReaders.Add(mdReader, reader)
    reader

let OpenILModuleReaderFromBytes (_fileNameForDebugOutput: string) (assemblyContents: byte []) (opts: ILReaderOptions) =
    let peReader = new PEReader(new MemoryStream(assemblyContents), PEStreamOptions.PrefetchEntireImage)
    OpenILModuleReaderAux peReader opts None

let OpenILModuleReaderFromFile fileName (opts: ILReaderOptions) metadataSnapshotOpt =
    let peReader =
        if opts.reduceMemoryUsage = ReduceMemoryFlag.Yes && opts.metadataOnly = MetadataOnlyFlag.Yes then
            let memory = ByteMemory.FromFile(fileName, FileAccess.Read, canShadowCopy=true)
            new PEReader(memory.AsStream(), PEStreamOptions.Default)
        else
            new PEReader(new MemoryStream(File.ReadAllBytes(fileName)), PEStreamOptions.PrefetchEntireImage)
    OpenILModuleReaderAux peReader opts metadataSnapshotOpt

type ILModuleReaderCacheKey = ILModuleReaderCacheKey of string * writeStamp: DateTime * bool * ReduceMemoryFlag * MetadataOnlyFlag with

    member x.WriteStamp =
        match x with
        | ILModuleReaderCacheKey(writeStamp=writeStamp) -> writeStamp

let stronglyHeldReaderCacheSizeDefault = 30
let stronglyHeldReaderCacheSize = try (match System.Environment.GetEnvironmentVariable("FSharp_StronglyHeldBinaryReaderCacheSize") with null -> stronglyHeldReaderCacheSizeDefault | s -> int32 s) with _ -> stronglyHeldReaderCacheSizeDefault

// ++GLOBAL MUTABLE STATE (concurrency safe via locking)
// Cache to extend the lifetime of a limited number of readers that are otherwise eligible for GC
type ILModuleReaderCache1LockToken() = interface LockToken
let ilModuleReaderCache1 =
    new AgedLookup<ILModuleReaderCache1LockToken, ILModuleReaderCacheKey, ILModuleReader>
            (stronglyHeldReaderCacheSize, 
            keepMax=stronglyHeldReaderCacheSize, // only strong entries
            areSimilar=(fun (x, y) -> x = y))
let ilModuleReaderCache1Lock = Lock()

// // Cache to reuse readers that have already been created and are not yet GC'd
let ilModuleReaderCache2 = new ConcurrentDictionary<ILModuleReaderCacheKey, System.WeakReference<ILModuleReader>>(HashIdentity.Structural)

let ClearAllILModuleReaderCache () =
    ilModuleReaderCache1.Clear(ILModuleReaderCache1LockToken())
    ilModuleReaderCache2.Clear()

let OpenILModuleReader fileName opts =
    let (ILModuleReaderCacheKey (fullPath,writeStamp,_,_,_) as key) = 
        let fullPath = FileSystem.GetFullPathShim fileName
        let writeTime = FileSystem.GetLastWriteTimeShim fileName
        ILModuleReaderCacheKey (fullPath, writeTime, opts.pdbDirPath.IsSome, opts.reduceMemoryUsage, opts.metadataOnly)

    let cacheResult1 = 
        // can't used a cached entry when reading PDBs, since it makes the returned object IDisposable
        if opts.pdbDirPath.IsNone then 
            ilModuleReaderCache1Lock.AcquireLock (fun ltok -> ilModuleReaderCache1.TryGet(ltok, key))
        else 
            None
        
    match cacheResult1 with 
    | Some ilModuleReader -> ilModuleReader
    | None -> 

    let cacheResult2 = 
        // can't used a cached entry when reading PDBs, since it makes the returned object IDisposable
        if opts.pdbDirPath.IsNone then 
            ilModuleReaderCache2.TryGetValue key
        else 
            false, Unchecked.defaultof<_>

    let mutable res = Unchecked.defaultof<_> 
    match cacheResult2 with 
    | true, weak when weak.TryGetTarget(&res) -> res
    | _ ->

    let metadataSnapshotOpt =
        if opts.reduceMemoryUsage = ReduceMemoryFlag.Yes && opts.metadataOnly = MetadataOnlyFlag.Yes then
            opts.tryGetMetadataSnapshot(fullPath, writeStamp)
        else
            None

    let ilModuleReader = OpenILModuleReaderFromFile fullPath opts metadataSnapshotOpt
    ilModuleReaderCache1Lock.AcquireLock (fun ltok -> ilModuleReaderCache1.Put(ltok, key, ilModuleReader))
    ilModuleReaderCache2.[key] <- System.WeakReference<_>(ilModuleReader)
    ilModuleReader
        
[<AutoOpen>]
module Shim =

    type IAssemblyReader =
        abstract GetILModuleReader: filename: string * readerOptions: ILReaderOptions -> ILModuleReader

    [<Sealed>]
    type DefaultAssemblyReader() =
        interface IAssemblyReader with
            member __.GetILModuleReader(filename, readerOptions) =
                OpenILModuleReader filename readerOptions

    let mutable AssemblyReader = DefaultAssemblyReader() :> IAssemblyReader