// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// The "unlinked" view of .NET metadata and code.  Central to the Abstract IL library

module rec FSharp.Compiler.AbstractIL.IL

open FSharp.Compiler.IO
open Internal.Utilities
open System.Collections.Generic
open System.Reflection

[<RequireQualifiedAccess>]
type internal PrimaryAssembly =
    | Mscorlib
    | System_Runtime
    | NetStandard

    member Name: string

/// Represents guids
type ILGuid = byte[]

[<StructuralEquality; StructuralComparison>]
type ILPlatform =
    internal
    | X86
    | AMD64
    | IA64

/// Debug info.  Values of type "source" can be attached at sequence
/// points and some other locations.
[<Sealed>]
type ILSourceDocument =
    static member Create: language: ILGuid option * vendor: ILGuid option * documentType: ILGuid option * file: string -> ILSourceDocument
    member Language: ILGuid option
    member Vendor: ILGuid option
    member DocumentType: ILGuid option
    member File: string

[<Sealed>]
type internal ILSourceMarker =
    static member Create: document: ILSourceDocument * line: int * column: int * endLine:int * endColumn: int-> ILSourceMarker
    member Document: ILSourceDocument
    member Line: int
    member Column: int
    member EndLine: int
    member EndColumn: int

[<StructuralEquality; StructuralComparison>]
type PublicKey =
    | PublicKey of byte[]
    | PublicKeyToken of byte[]
    member IsKey: bool
    member IsKeyToken: bool
    member Key: byte[]
    member KeyToken: byte[]
    static member KeyAsToken: byte[] -> PublicKey

[<Struct>]
type ILVersionInfo =

    val Major: uint16
    val Minor: uint16
    val Build: uint16
    val Revision: uint16

    new : major: uint16 * minor: uint16 * build: uint16 * revision: uint16 -> ILVersionInfo

[<Sealed>]
type ILAssemblyRef =
    static member Create: name: string * hash: byte[] option * publicKey: PublicKey option * retargetable: bool * version: ILVersionInfo option * locale: string option -> ILAssemblyRef

    static member FromAssemblyName: System.Reflection.AssemblyName -> ILAssemblyRef

    member Name: string

    /// The fully qualified name of the assembly reference, e.g. mscorlib, Version=1.0.3705 etc.
    member QualifiedName: string

    member Hash: byte[] option

    member PublicKey: PublicKey option

    /// CLI says this indicates if the assembly can be retargeted (at runtime) to be from a different publisher.
    member Retargetable: bool

    member Version: ILVersionInfo option

    member Locale: string option

    member EqualsIgnoringVersion: ILAssemblyRef -> bool

    interface System.IComparable

[<Sealed>]
type ILModuleRef =
    static member Create: name: string * hasMetadata: bool * hash: byte[] option -> ILModuleRef

    member Name: string

    member HasMetadata: bool

    member Hash: byte[] option

    interface System.IComparable

// Scope references
[<StructuralEquality; StructuralComparison; RequireQualifiedAccess>]
type ILScopeRef =
    /// A reference to the type in the current module
    | Local

    /// A reference to a type in a module in the same assembly
    | Module of ILModuleRef

    /// A reference to a type in another assembly
    | Assembly of ILAssemblyRef

    /// A reference to a type in the primary assembly
    | PrimaryAssembly

    member IsLocalRef: bool

    member QualifiedName: string

// Calling conventions.
//
// For nearly all purposes you simply want to use ILArgConvention.Default combined
// with ILThisConvention.Instance or ILThisConvention.Static, i.e.
//   ILCallingConv.Instance == Callconv(ILThisConvention.Instance, ILArgConvention.Default): for an instance method
//   ILCallingConv.Static   == Callconv(ILThisConvention.Static, ILArgConvention.Default): for a static method
//
// ILThisConvention.InstanceExplicit is only used by Managed C++, and indicates
// that the 'this' pointer is actually explicit in the signature.
[<StructuralEquality; StructuralComparison; RequireQualifiedAccess>]
type ILArgConvention =
    | Default
    | CDecl
    | StdCall
    | ThisCall
    | FastCall
    | VarArg

[<StructuralEquality; StructuralComparison; RequireQualifiedAccess>]
type ILThisConvention =
    /// accepts an implicit 'this' pointer
    | Instance

    /// accepts an explicit 'this' pointer
    | InstanceExplicit

    /// no 'this' pointer is passed
    | Static

[<StructuralEquality; StructuralComparison>]
type ILCallingConv =
    | Callconv of ILThisConvention * ILArgConvention

    member internal IsInstance: bool
    member internal IsInstanceExplicit: bool
    member internal IsStatic: bool
    member internal ThisConv: ILThisConvention
    member internal BasicConv: ILArgConvention

    static member Instance: ILCallingConv
    static member Static  : ILCallingConv

/// Array shapes. For most purposes the rank is the only thing that matters.
type internal ILArrayBound = int32 option

/// Lower-bound/size pairs
type internal ILArrayBounds = ILArrayBound * ILArrayBound

type ILArrayShape =
    internal
    | ILArrayShape of ILArrayBounds list

    member Rank: int

    /// Bounds for a single dimensional, zero based array
    static member SingleDimensional: ILArrayShape
    static member FromRank: int -> ILArrayShape

type internal ILBoxity =
    | AsObject
    | AsValue

type ILGenericVariance =
    | NonVariant
    | CoVariant
    | ContraVariant

/// Type refs, i.e. references to types in some .NET assembly
[<Sealed>]
type ILTypeRef =

    /// Create a ILTypeRef.
    static member Create: scope: ILScopeRef * enclosing: string list * name: string -> ILTypeRef

    /// Where is the type, i.e. is it in this module, in another module in this assembly or in another assembly?
    member Scope: ILScopeRef

    /// The list of enclosing type names for a nested type. If non-nil then the first of these also contains the namespace.
    member Enclosing: string list

    /// The name of the type. This also contains the namespace if Enclosing is empty.
    member Name: string

    /// The name of the type in the assembly using the '.' notation for nested types.
    member FullName: string

    /// The name of the type in the assembly using the '+' notation for nested types.
    member BasicQualifiedName: string

    member QualifiedName: string

    member internal EqualsWithPrimaryScopeRef: ILScopeRef * obj -> bool

    interface System.IComparable

/// Type specs and types.
[<Sealed>]
type ILTypeSpec =
    /// Create an ILTypeSpec.
    static member Create: typeRef:ILTypeRef * instantiation:ILGenericArgs -> ILTypeSpec

    /// Which type is being referred to?
    member TypeRef: ILTypeRef

    /// The type instantiation if the type is generic, otherwise empty
    member GenericArgs: ILGenericArgs

    /// Where is the type, i.e. is it in this module, in another module in this assembly or in another assembly?
    member Scope: ILScopeRef

    /// The list of enclosing type names for a nested type. If non-nil then the first of these also contains the namespace.
    member Enclosing: string list

    /// The name of the type. This also contains the namespace if Enclosing is empty.
    member Name: string

    /// The name of the type in the assembly using the '.' notation for nested types.
    member FullName: string

    member internal EqualsWithPrimaryScopeRef: ILScopeRef * obj -> bool

    interface System.IComparable

[<RequireQualifiedAccess; StructuralEquality; StructuralComparison>]
type ILType =

    /// Used only in return and pointer types.
    | Void

    /// Array types
    | Array of ILArrayShape * ILType

    /// Unboxed types, including builtin types.
    | Value of ILTypeSpec

    /// Reference types.  Also may be used for parents of members even if for members in value types.
    | Boxed of ILTypeSpec

    /// Unmanaged pointers.  Nb. the type is used by tools and for binding only, not by the verifier.
    | Ptr of ILType

    /// Managed pointers.
    | Byref of ILType

    /// ILCode pointers.
    | FunctionPointer of ILCallingSignature

    /// Reference a generic arg.
    | TypeVar of uint16

    /// Custom modifiers.
    | Modified of
          /// True if modifier is "required".
          bool *
          /// The class of the custom modifier.
          ILTypeRef *
          /// The type being modified.
          ILType

    member TypeSpec: ILTypeSpec

    member internal Boxity: ILBoxity

    member TypeRef: ILTypeRef

    member IsNominal: bool

    member GenericArgs: ILGenericArgs

    member IsTyvar: bool

    member BasicQualifiedName: string

    member QualifiedName: string

[<StructuralEquality; StructuralComparison>]
type ILCallingSignature =
    { CallingConv: ILCallingConv
      ArgTypes: ILTypes
      ReturnType: ILType }

/// Actual generic parameters are  always types.
type ILGenericArgs = ILType list

type ILTypes = ILType list

/// Formal identities of methods.
[<Sealed>]
type ILMethodRef =

     /// Functional creation
     static member Create:
        enclosingTypeRef: ILTypeRef *
        callingConv: ILCallingConv *
        name: string *
        genericArity: int *
        argTypes: ILTypes *
        returnType: ILType -> ILMethodRef

     member DeclaringTypeRef: ILTypeRef

     member CallingConv: ILCallingConv

     member Name: string

     member GenericArity: int

     member ArgCount: int

     member ArgTypes: ILTypes

     member ReturnType: ILType

     member CallingSignature: ILCallingSignature

     interface System.IComparable

/// Formal identities of fields.
[<StructuralEquality; StructuralComparison>]
type ILFieldRef =
    { DeclaringTypeRef: ILTypeRef
      Name: string
      Type: ILType }

/// The information at the callsite of a method
[<Sealed>]
type ILMethodSpec =

     /// Functional creation
     static member Create: ILType * ILMethodRef * ILGenericArgs -> ILMethodSpec

     member MethodRef: ILMethodRef

     member DeclaringType: ILType

     member GenericArgs: ILGenericArgs

     member CallingConv: ILCallingConv

     member GenericArity: int

     member Name: string

     member FormalArgTypes: ILTypes

     member FormalReturnType: ILType

     interface System.IComparable

/// Field specs.  The data given for a ldfld, stfld etc. instruction.
[<StructuralEquality; StructuralComparison>]
type ILFieldSpec =
    { FieldRef: ILFieldRef
      DeclaringType: ILType }

    member DeclaringTypeRef: ILTypeRef

    member Name: string

    member FormalType: ILType

    member ActualType: ILType

/// ILCode labels.  In structured code each code label refers to a basic block somewhere in the code of the method.
type internal ILCodeLabel = int

[<StructuralEquality; StructuralComparison>]
type internal ILBasicType =
    | DT_R
    | DT_I1
    | DT_U1
    | DT_I2
    | DT_U2
    | DT_I4
    | DT_U4
    | DT_I8
    | DT_U8
    | DT_R4
    | DT_R8
    | DT_I
    | DT_U
    | DT_REF

[<StructuralEquality; StructuralComparison; RequireQualifiedAccess>]
type internal ILToken =
    | ILType of ILType
    | ILMethod of ILMethodSpec
    | ILField of ILFieldSpec

[<StructuralEquality; StructuralComparison; RequireQualifiedAccess>]
type internal ILConst =
    | I4 of int32
    | I8 of int64
    | R4 of single
    | R8 of double

type internal ILTailcall =
    | Tailcall
    | Normalcall

type internal ILAlignment =
    | Aligned
    | Unaligned1
    | Unaligned2
    | Unaligned4

type internal ILVolatility =
    | Volatile
    | Nonvolatile

type internal ILReadonly =
    | ReadonlyAddress
    | NormalAddress

type internal ILVarArgs = ILTypes option

[<StructuralEquality; StructuralComparison>]
type internal ILComparisonInstr =
    | BI_beq
    | BI_bge
    | BI_bge_un
    | BI_bgt
    | BI_bgt_un
    | BI_ble
    | BI_ble_un
    | BI_blt
    | BI_blt_un
    | BI_bne_un
    | BI_brfalse
    | BI_brtrue

/// The instruction set.
[<StructuralEquality; NoComparison>]
type internal ILInstr =
    | AI_add
    | AI_add_ovf
    | AI_add_ovf_un
    | AI_and
    | AI_div
    | AI_div_un
    | AI_ceq
    | AI_cgt
    | AI_cgt_un
    | AI_clt
    | AI_clt_un
    | AI_conv      of ILBasicType
    | AI_conv_ovf  of ILBasicType
    | AI_conv_ovf_un  of ILBasicType
    | AI_mul
    | AI_mul_ovf
    | AI_mul_ovf_un
    | AI_rem
    | AI_rem_un
    | AI_shl
    | AI_shr
    | AI_shr_un
    | AI_sub
    | AI_sub_ovf
    | AI_sub_ovf_un
    | AI_xor
    | AI_or
    | AI_neg
    | AI_not
    | AI_ldnull
    | AI_dup
    | AI_pop
    | AI_ckfinite
    | AI_nop
    | AI_ldc       of ILBasicType * ILConst
    | I_ldarg     of uint16
    | I_ldarga    of uint16
    | I_ldind     of ILAlignment * ILVolatility * ILBasicType
    | I_ldloc     of uint16
    | I_ldloca    of uint16
    | I_starg     of uint16
    | I_stind     of  ILAlignment * ILVolatility * ILBasicType
    | I_stloc     of uint16

    // Control transfer
    | I_br    of  ILCodeLabel
    | I_jmp   of ILMethodSpec
    | I_brcmp of ILComparisonInstr * ILCodeLabel
    | I_switch    of ILCodeLabel list
    | I_ret

     // Method call
    | I_call     of ILTailcall * ILMethodSpec * ILVarArgs
    | I_callvirt of ILTailcall * ILMethodSpec * ILVarArgs
    | I_callconstraint of ILTailcall * ILType * ILMethodSpec * ILVarArgs
    | I_calli    of ILTailcall * ILCallingSignature * ILVarArgs
    | I_ldftn    of ILMethodSpec
    | I_newobj   of ILMethodSpec  * ILVarArgs

    // Exceptions
    | I_throw
    | I_endfinally
    | I_endfilter
    | I_leave     of  ILCodeLabel
    | I_rethrow

    // Object instructions
    | I_ldsfld      of ILVolatility * ILFieldSpec
    | I_ldfld       of ILAlignment * ILVolatility * ILFieldSpec
    | I_ldsflda     of ILFieldSpec
    | I_ldflda      of ILFieldSpec
    | I_stsfld      of ILVolatility  *  ILFieldSpec
    | I_stfld       of ILAlignment * ILVolatility * ILFieldSpec
    | I_ldstr       of string
    | I_isinst      of ILType
    | I_castclass   of ILType
    | I_ldtoken     of ILToken
    | I_ldvirtftn   of ILMethodSpec

    // Value type instructions
    | I_cpobj       of ILType
    | I_initobj     of ILType
    | I_ldobj       of ILAlignment * ILVolatility * ILType
    | I_stobj       of ILAlignment * ILVolatility * ILType
    | I_box         of ILType
    | I_unbox       of ILType
    | I_unbox_any   of ILType
    | I_sizeof      of ILType

    // Generalized array instructions. In AbsIL these instructions include
    // both the single-dimensional variants (with ILArrayShape == ILArrayShape.SingleDimensional)
    // and calls to the "special" multi-dimensional "methods" such as:
    //   newobj void string[,]::.ctor(int32, int32)
    //   call string string[,]::Get(int32, int32)
    //   call string& string[,]::Address(int32, int32)
    //   call void string[,]::Set(int32, int32,string)
    //
    // The IL reader transforms calls of this form to the corresponding
    // generalized instruction with the corresponding ILArrayShape
    // argument. This is done to simplify the IL and make it more uniform.
    // The IL writer then reverses this when emitting the binary.
    | I_ldelem      of ILBasicType
    | I_stelem      of ILBasicType
    | I_ldelema     of ILReadonly * bool * ILArrayShape * ILType (* ILArrayShape = ILArrayShape.SingleDimensional for single dimensional arrays *)
    | I_ldelem_any  of ILArrayShape * ILType (* ILArrayShape = ILArrayShape.SingleDimensional for single dimensional arrays *)
    | I_stelem_any  of ILArrayShape * ILType (* ILArrayShape = ILArrayShape.SingleDimensional for single dimensional arrays *)
    | I_newarr      of ILArrayShape * ILType (* ILArrayShape = ILArrayShape.SingleDimensional for single dimensional arrays *)
    | I_ldlen

    // "System.TypedReference" related instructions: almost
    // no languages produce these, though they do occur in mscorlib.dll
    // System.TypedReference represents a pair of a type and a byref-pointer
    // to a value of that type.
    | I_mkrefany    of ILType
    | I_refanytype
    | I_refanyval   of ILType

    // Debug-specific
    // I_seqpoint is a fake instruction to represent a sequence point:
    // the next instruction starts the execution of the
    // statement covered by the given range - this is a
    // dummy instruction and is not emitted
    | I_break
    | I_seqpoint of ILSourceMarker

    // Varargs - C++ only
    | I_arglist

    // Local aggregates, i.e. stack allocated data (alloca): C++ only
    | I_localloc
    | I_cpblk of ILAlignment * ILVolatility
    | I_initblk of ILAlignment  * ILVolatility

    // EXTENSIONS
    | EI_ilzero of ILType
    | EI_ldlen_multi      of int32 * int32

[<RequireQualifiedAccess>]
type internal ILExceptionClause =
    | Finally of (ILCodeLabel * ILCodeLabel)
    | Fault  of (ILCodeLabel * ILCodeLabel)
    | FilterCatch of filterRange: (ILCodeLabel * ILCodeLabel) * handlerRange: (ILCodeLabel * ILCodeLabel)
    | TypeCatch of ILType * (ILCodeLabel * ILCodeLabel)

[<RequireQualifiedAccess; NoEquality; NoComparison>]
type internal ILExceptionSpec =
    { Range: (ILCodeLabel * ILCodeLabel)
      Clause: ILExceptionClause }

/// Indicates that a particular local variable has a particular source
/// language name within a given set of ranges. This does not effect local
/// variable numbering, which is global over the whole method.
[<RequireQualifiedAccess; NoEquality; NoComparison>]
type internal ILLocalDebugMapping =
    { LocalIndex: int
      LocalName: string }

[<RequireQualifiedAccess; NoEquality; NoComparison>]
type internal ILLocalDebugInfo =
    { Range: (ILCodeLabel * ILCodeLabel);
      DebugMappings: ILLocalDebugMapping list }

[<RequireQualifiedAccess; NoEquality; NoComparison>]
type internal ILCode =
    { Labels: Dictionary<ILCodeLabel,int>
      Instrs:ILInstr[]
      Exceptions: ILExceptionSpec list
      Locals: ILLocalDebugInfo list }

/// Field Init
[<RequireQualifiedAccess; StructuralEquality; StructuralComparison>]
type ILFieldInit =
    | String of string
    | Bool of bool
    | Char of uint16
    | Int8 of sbyte
    | Int16 of int16
    | Int32 of int32
    | Int64 of int64
    | UInt8 of byte
    | UInt16 of uint16
    | UInt32 of uint32
    | UInt64 of uint64
    | Single of single
    | Double of double
    | Null

    member AsObject: unit -> obj

[<RequireQualifiedAccess; StructuralEquality; StructuralComparison>]
type internal ILNativeVariant =
    | Empty
    | Null
    | Variant
    | Currency
    | Decimal
    | Date
    | BSTR
    | LPSTR
    | LPWSTR
    | IUnknown
    | IDispatch
    | SafeArray
    | Error
    | HRESULT
    | CArray
    | UserDefined
    | Record
    | FileTime
    | Blob
    | Stream
    | Storage
    | StreamedObject
    | StoredObject
    | BlobObject
    | CF
    | CLSID
    | Void
    | Bool
    | Int8
    | Int16
    | Int32
    | Int64
    | Single
    | Double
    | UInt8
    | UInt16
    | UInt32
    | UInt64
    | PTR
    | Array of ILNativeVariant
    | Vector of ILNativeVariant
    | Byref of ILNativeVariant
    | Int
    | UInt

/// Native Types, for marshalling to the native C interface.
/// These are taken directly from the ILASM syntax.
/// Most of these are listed in the CLI ECMA-335 Spec (Partition II, 7.4).
[<RequireQualifiedAccess; StructuralEquality; StructuralComparison>]
type ILNativeType =
    | Empty
    | Custom of ILGuid * nativeTypeName: string * custMarshallerName: string * cookieString: byte[]
    | FixedSysString of int32
    | FixedArray of int32
    | Currency
    | LPSTR
    | LPWSTR
    | LPTSTR
    | LPUTF8STR
    | ByValStr
    | TBSTR
    | LPSTRUCT
    | Struct
    | Void
    | Bool
    | Int8
    | Int16
    | Int32
    | Int64
    | Single
    | Double
    | Byte
    | UInt16
    | UInt32
    | UInt64
    ///  optional idx of parameter giving size plus optional additive i.e. num elems
    | Array of ILNativeType option * (int32 * int32 option) option
    | Int
    | UInt
    | Method
    | AsAny
    | BSTR
    | IUnknown
    | IDispatch
    | Interface
    | Error
    | SafeArray of ILNativeVariant * string option
    | ANSIBSTR
    | VariantBool

/// Local variables
[<RequireQualifiedAccess; NoComparison; NoEquality>]
type internal ILLocal =
    { Type: ILType
      IsPinned: bool
      DebugInfo: (string * int * int) option }

type internal ILLocals = list<ILLocal>

/// IL method bodies
[<RequireQualifiedAccess; NoComparison; NoEquality>]
type internal ILMethodBody =
    { IsZeroInit: bool
      MaxStack: int32
      NoInlining: bool
      AggressiveInlining: bool
      Locals: ILLocals
      Code: ILCode
      SourceMarker: ILSourceMarker option }

/// Member Access
[<RequireQualifiedAccess>]
type ILMemberAccess =
    | Assembly
    | CompilerControlled
    | FamilyAndAssembly
    | FamilyOrAssembly
    | Family
    | Private
    | Public

[<RequireQualifiedAccess>]
type ILAttribElem =
    /// Represents a custom attribute parameter of type 'string'. These may be null, in which case they are encoded in a special
    /// way as indicated by Ecma-335 Partition II.
    | String of string  option
    | Bool of bool
    | Char of char
    | SByte of sbyte
    | Int16 of int16
    | Int32 of int32
    | Int64 of int64
    | Byte of byte
    | UInt16 of uint16
    | UInt32 of uint32
    | UInt64 of uint64
    | Single of single
    | Double of double
    | Null
    | Type of ILType option
    | TypeRef of ILTypeRef option
    | Array of ILType * ILAttribElem list

/// Named args: values and flags indicating if they are fields or properties.
type ILAttributeNamedArg = string * ILType * bool * ILAttribElem

/// Custom attribute.
type ILAttribute =
    /// Attribute with args encoded to a binary blob according to ECMA-335 II.21 and II.23.3.
    /// 'decodeILAttribData' is used to parse the byte[] blob to ILAttribElem's as best as possible.
    | Encoded of method: ILMethodSpec * data: byte[] * elements: ILAttribElem list

    /// Attribute with args in decoded form.
    | Decoded of method: ILMethodSpec * fixedArgs: ILAttribElem list * namedArgs: ILAttributeNamedArg list

    /// Attribute instance constructor.
    member internal Method: ILMethodSpec

    /// Decoded arguments. May be empty in encoded attribute form.
    member internal Elements: ILAttribElem list

    member internal WithMethod: method: ILMethodSpec -> ILAttribute

[<NoEquality; NoComparison; Struct>]
type ILAttributes =
    member AsArray: ILAttribute []

    member AsList: ILAttribute list

/// Represents the efficiency-oriented storage of ILAttributes in another item.
[<NoEquality; NoComparison>]
type ILAttributesStored

/// Method parameters and return values.
[<RequireQualifiedAccess; NoEquality; NoComparison>]
type ILParameter =
    { Name: string option
      Type: ILType
      Default: ILFieldInit option
      /// Marshalling map for parameters. COM Interop only.
      Marshal: ILNativeType option
      IsIn: bool
      IsOut: bool
      IsOptional: bool
      CustomAttrsStored: ILAttributesStored
      MetadataIndex: int32 }
    member CustomAttrs: ILAttributes

type ILParameters = ILParameter list

val internal typesOfILParams: ILParameters -> ILType list

/// Method return values.
[<RequireQualifiedAccess; NoEquality; NoComparison>]
type ILReturn =
    { Marshal: ILNativeType option
      Type: ILType
      CustomAttrsStored: ILAttributesStored
      MetadataIndex: int32  }

    member CustomAttrs: ILAttributes

    member WithCustomAttrs: customAttrs: ILAttributes -> ILReturn

[<RequireQualifiedAccess>]
type internal ILSecurityAction =
    | Request
    | Demand
    | Assert
    | Deny
    | PermitOnly
    | LinkCheck
    | InheritCheck
    | ReqMin
    | ReqOpt
    | ReqRefuse
    | PreJitGrant
    | PreJitDeny
    | NonCasDemand
    | NonCasLinkDemand
    | NonCasInheritance
    | LinkDemandChoice
    | InheritanceDemandChoice
    | DemandChoice

type internal ILSecurityDecl =
    | ILSecurityDecl of ILSecurityAction * byte[]

/// Abstract type equivalent to ILSecurityDecl list - use helpers
/// below to construct/destruct these.
[<NoComparison; NoEquality; Struct>]
type internal ILSecurityDecls =
    member AsList: ILSecurityDecl list

/// Represents the efficiency-oriented storage of ILSecurityDecls in another item.
[<NoEquality; NoComparison>]
type ILSecurityDeclsStored

/// PInvoke attributes.
[<RequireQualifiedAccess>]
type internal PInvokeCallingConvention =
    | None
    | Cdecl
    | Stdcall
    | Thiscall
    | Fastcall
    | WinApi

[<RequireQualifiedAccess>]
type internal PInvokeCharEncoding =
    | None
    | Ansi
    | Unicode
    | Auto

[<RequireQualifiedAccess>]
type internal PInvokeCharBestFit =
    | UseAssembly
    | Enabled
    | Disabled

[<RequireQualifiedAccess>]
type internal PInvokeThrowOnUnmappableChar =
    | UseAssembly
    | Enabled
    | Disabled

[<RequireQualifiedAccess; NoComparison; NoEquality>]
type internal PInvokeMethod =
    { Where: ILModuleRef
      Name: string
      CallingConv: PInvokeCallingConvention
      CharEncoding: PInvokeCharEncoding
      NoMangle: bool
      LastError: bool
      ThrowOnUnmappableChar: PInvokeThrowOnUnmappableChar
      CharBestFit: PInvokeCharBestFit }

/// Represents a reference to a method declaration in a superclass or interface.
type internal ILOverridesSpec =
    | OverridesSpec of ILMethodRef * ILType
    member MethodRef: ILMethodRef
    member DeclaringType: ILType

[<RequireQualifiedAccess>]
type MethodBody =
    | IL of Lazy<ILMethodBody>
    | PInvoke of Lazy<PInvokeMethod>
    | Abstract
    | Native
    | NotAvailable

/// Generic parameters.  Formal generic parameter declarations may include the bounds, if any, on the generic parameter.
type ILGenericParameterDef =
    { Name: string

      /// At most one is the parent type, the others are interface types.
      Constraints: ILTypes

      /// Variance of type parameters, only applicable to generic parameters for generic interfaces and delegates.
      Variance: ILGenericVariance

      /// Indicates the type argument must be a reference type.
      HasReferenceTypeConstraint: bool

      /// Indicates the type argument must be a value type, but not Nullable.
      HasNotNullableValueTypeConstraint: bool

      /// Indicates the type argument must have a public nullary constructor.
      HasDefaultConstructorConstraint: bool

      /// Do not use this
      CustomAttrsStored: ILAttributesStored

      /// Do not use this
      MetadataIndex: int32 }

    member CustomAttrs: ILAttributes

type ILGenericParameterDefs = ILGenericParameterDef list

/// IL Method definitions.
[<NoComparison; NoEquality>]
type ILMethodDef =

    /// Functional creation of a value, with delayed reading of some elements via a metadata index
    internal new:
         name: string * attributes: MethodAttributes * implAttributes: MethodImplAttributes * callingConv: ILCallingConv *
         parameters: ILParameters * ret: ILReturn * body: Lazy<MethodBody> * isEntryPoint:bool * genericParams: ILGenericParameterDefs *
         securityDeclsStored: ILSecurityDeclsStored * customAttrsStored: ILAttributesStored * metadataIndex: int32 -> ILMethodDef

    /// Functional creation of a value, immediate
    new: name: string * attributes: MethodAttributes * implAttributes: MethodImplAttributes * callingConv: ILCallingConv *
         parameters: ILParameters * ret: ILReturn * body: Lazy<MethodBody> * isEntryPoint:bool * genericParams: ILGenericParameterDefs *
         securityDecls: ILSecurityDecls * customAttrs: ILAttributes -> ILMethodDef

    member Name: string
    member Attributes: MethodAttributes
    member ImplAttributes: MethodImplAttributes
    member CallingConv: ILCallingConv
    member Parameters: ILParameters
    member Return: ILReturn
    member Body: MethodBody
    member SecurityDecls: ILSecurityDecls
    member IsEntryPoint:bool
    member GenericParams: ILGenericParameterDefs
    member CustomAttrs: ILAttributes
    member ParameterTypes: ILTypes
    member IsIL: bool
    member Code: ILCode option
    member Locals: ILLocals
    member MaxStack: int32
    member IsZeroInit: bool

    /// Indicates a .cctor method.
    member IsClassInitializer: bool

    /// Indicates a .ctor method.
    member IsConstructor: bool

    /// Indicates a static method.
    member IsStatic: bool

    /// Indicates this is an instance methods that is not virtual.
    member IsNonVirtualInstance: bool

    /// Indicates an instance methods that is virtual or abstract or implements an interface slot.
    member IsVirtual: bool

    member IsFinal: bool
    member IsNewSlot: bool
    member IsCheckAccessOnOverride: bool
    member IsAbstract: bool
    member MethodBody: ILMethodBody
    member CallingSignature: ILCallingSignature
    member Access: ILMemberAccess
    member IsHideBySig: bool
    member IsSpecialName: bool

    /// The method is exported to unmanaged code using COM interop.
    member IsUnmanagedExport: bool
    member IsReqSecObj: bool

    /// Some methods are marked "HasSecurity" even if there are no permissions attached, e.g. if they use SuppressUnmanagedCodeSecurityAttribute
    member HasSecurity: bool
    member IsManaged: bool
    member IsForwardRef: bool
    member IsInternalCall: bool
    member IsPreserveSig: bool
    member IsSynchronized: bool
    member IsNoInline: bool
    member IsAggressiveInline: bool

    /// SafeHandle finalizer must be run.
    member IsMustRun: bool

    /// Functional update of the value
    member internal
        With: ?name: string * ?attributes: MethodAttributes * ?implAttributes: MethodImplAttributes * ?callingConv: ILCallingConv *
                 ?parameters: ILParameters * ?ret: ILReturn * ?body: Lazy<MethodBody> * ?securityDecls: ILSecurityDecls * ?isEntryPoint:bool *
                 ?genericParams: ILGenericParameterDefs * ?customAttrs: ILAttributes -> ILMethodDef
    member internal WithSpecialName: ILMethodDef
    member internal WithHideBySig: unit -> ILMethodDef
    member internal WithHideBySig: bool -> ILMethodDef
    member internal WithFinal: bool -> ILMethodDef
    member internal WithAbstract: bool -> ILMethodDef
    member internal WithAccess: ILMemberAccess -> ILMethodDef
    member internal WithNewSlot: ILMethodDef
    member internal WithSecurity: bool -> ILMethodDef
    member internal WithPInvoke: bool -> ILMethodDef
    member internal WithPreserveSig: bool -> ILMethodDef
    member internal WithSynchronized: bool -> ILMethodDef
    member internal WithNoInlining: bool -> ILMethodDef
    member internal WithAggressiveInlining: bool -> ILMethodDef
    member internal WithRuntime: bool -> ILMethodDef

/// Tables of methods.  Logically equivalent to a list of methods but
/// the table is kept in a form optimized for looking up methods by
/// name and arity.
[<NoEquality; NoComparison; Sealed>]
type ILMethodDefs =
    interface IEnumerable<ILMethodDef>
    member AsArray: ILMethodDef[]
    member AsList: ILMethodDef list
    member FindByName: string -> ILMethodDef list
    member TryFindInstanceByNameAndCallingSignature: string * ILCallingSignature -> ILMethodDef option

/// Field definitions.
[<NoComparison; NoEquality>]
type ILFieldDef =

    /// Functional creation of a value using delayed reading via a metadata index
    internal new:
         name: string * fieldType: ILType * attributes: FieldAttributes * data: byte[] option *
         literalValue: ILFieldInit option * offset:  int32 option * marshal: ILNativeType option *
         customAttrsStored: ILAttributesStored * metadataIndex: int32 -> ILFieldDef

    /// Functional creation of a value, immediate
    new: name: string * fieldType: ILType * attributes: FieldAttributes * data: byte[] option *
         literalValue: ILFieldInit option * offset:  int32 option * marshal: ILNativeType option *
         customAttrs: ILAttributes -> ILFieldDef

    member Name: string
    member FieldType: ILType
    member Attributes: FieldAttributes
    member Data:  byte[] option
    member LiteralValue: ILFieldInit option

    /// The explicit offset in bytes when explicit layout is used.
    member Offset:  int32 option
    member Marshal: ILNativeType option
    member CustomAttrs: ILAttributes
    member IsStatic: bool
    member IsSpecialName: bool
    member IsLiteral: bool
    member NotSerialized: bool
    member IsInitOnly: bool
    member Access: ILMemberAccess

    /// Functional update of the value
    member internal With:
        ?name: string * ?fieldType: ILType * ?attributes: FieldAttributes * ?data: byte[] option * ?literalValue: ILFieldInit option *
        ?offset:  int32 option * ?marshal: ILNativeType option * ?customAttrs: ILAttributes -> ILFieldDef
    member internal WithAccess: ILMemberAccess -> ILFieldDef
    member internal WithInitOnly: bool -> ILFieldDef
    member internal WithStatic: bool -> ILFieldDef
    member internal WithSpecialName: bool -> ILFieldDef
    member internal WithNotSerialized: bool -> ILFieldDef
    member internal WithLiteralDefaultValue: ILFieldInit option -> ILFieldDef
    member internal WithFieldMarshal: ILNativeType option -> ILFieldDef

/// Tables of fields.  Logically equivalent to a list of fields but the table is kept in
/// a form to allow efficient looking up fields by name.
[<NoEquality; NoComparison; Sealed>]
type ILFieldDefs =
    member internal AsList: ILFieldDef list
    member internal LookupByName: string -> ILFieldDef list

/// Event definitions.
[<NoComparison; NoEquality>]
type ILEventDef =

    /// Functional creation of a value, using delayed reading via a metadata index, for ilread.fs
    internal new:
         eventType: ILType option * name: string * attributes: EventAttributes * addMethod: ILMethodRef *
         removeMethod: ILMethodRef * fireMethod: ILMethodRef option * otherMethods: ILMethodRef list *
         customAttrsStored: ILAttributesStored * metadataIndex: int32 -> ILEventDef

    /// Functional creation of a value, immediate
    new: eventType: ILType option * name: string * attributes: EventAttributes * addMethod: ILMethodRef *
         removeMethod: ILMethodRef * fireMethod: ILMethodRef option * otherMethods: ILMethodRef list *
         customAttrs: ILAttributes -> ILEventDef

    member EventType: ILType option
    member Name: string
    member Attributes: EventAttributes
    member AddMethod: ILMethodRef
    member RemoveMethod: ILMethodRef
    member FireMethod: ILMethodRef option
    member OtherMethods: ILMethodRef list
    member CustomAttrs: ILAttributes
    member IsSpecialName: bool
    member IsRTSpecialName: bool

    /// Functional update of the value
    member internal With:
        ?eventType: ILType option * ?name: string * ?attributes: EventAttributes * ?addMethod: ILMethodRef *
        ?removeMethod: ILMethodRef * ?fireMethod: ILMethodRef option * ?otherMethods: ILMethodRef list *
        ?customAttrs: ILAttributes -> ILEventDef

/// Table of those events in a type definition.
[<NoEquality; NoComparison; Sealed>]
type ILEventDefs =
    member internal AsList: ILEventDef list
    member internal LookupByName: string -> ILEventDef list

/// Property definitions
[<NoComparison; NoEquality>]
type ILPropertyDef =

    /// Functional creation of a value, using delayed reading via a metadata index, for ilread.fs
    internal new:
         name: string * attributes: PropertyAttributes * setMethod: ILMethodRef option * getMethod: ILMethodRef option *
         callingConv: ILThisConvention * propertyType: ILType * init: ILFieldInit option * args: ILTypes *
         customAttrsStored: ILAttributesStored * metadataIndex: int32 -> ILPropertyDef

    /// Functional creation of a value, immediate
    new: name: string * attributes: PropertyAttributes * setMethod: ILMethodRef option * getMethod: ILMethodRef option *
         callingConv: ILThisConvention * propertyType: ILType * init: ILFieldInit option * args: ILTypes *
         customAttrs: ILAttributes -> ILPropertyDef

    member Name: string
    member Attributes: PropertyAttributes
    member SetMethod: ILMethodRef option
    member GetMethod: ILMethodRef option
    member CallingConv: ILThisConvention
    member PropertyType: ILType
    member Init: ILFieldInit option
    member Args: ILTypes
    member CustomAttrs: ILAttributes
    member IsSpecialName: bool
    member IsRTSpecialName: bool

    /// Functional update of the value
    member internal With:
        ?name: string * ?attributes: PropertyAttributes * ?setMethod: ILMethodRef option * ?getMethod: ILMethodRef option *
        ?callingConv: ILThisConvention * ?propertyType: ILType * ?init: ILFieldInit option * ?args: ILTypes *
        ?customAttrs: ILAttributes -> ILPropertyDef

/// Table of properties in an IL type definition.
[<NoEquality; NoComparison>]
[<Sealed>]
type ILPropertyDefs =
    member internal AsList: ILPropertyDef list
    member internal LookupByName: string -> ILPropertyDef list

/// Method Impls
type ILMethodImplDef =
    { Overrides: ILOverridesSpec
      OverrideBy: ILMethodSpec }

[<NoEquality; NoComparison; Sealed>]
type ILMethodImplDefs =
    member internal AsList: ILMethodImplDef list

/// Type Layout information.
[<RequireQualifiedAccess>]
type ILTypeDefLayout =
    | Auto
    | Sequential of ILTypeDefLayoutInfo
    | Explicit of ILTypeDefLayoutInfo

type internal ILTypeDefLayoutInfo =
    { Size: int32 option
      Pack: uint16 option }

/// Indicate the initialization semantics of a type.
[<RequireQualifiedAccess>]
type ILTypeInit =
    | BeforeField
    | OnAny

/// Default Unicode encoding for P/Invoke  within a type.
[<RequireQualifiedAccess>]
type ILDefaultPInvokeEncoding =
    | Ansi
    | Auto
    | Unicode

/// Type Access.
[<RequireQualifiedAccess>]
type ILTypeDefAccess =
    | Public
    | Private
    | Nested of ILMemberAccess

/// A categorization of type definitions into "kinds"
[<RequireQualifiedAccess>]
type ILTypeDefKind =
    | Class
    | ValueType
    | Interface
    | Enum
    | Delegate

/// Tables of named type definitions.
[<NoEquality; NoComparison; Sealed>]
type ILTypeDefs =
    interface IEnumerable<ILTypeDef>

    member internal AsArray: ILTypeDef[]

    member internal AsList: ILTypeDef list

    /// Get some information about the type defs, but do not force the read of the type defs themselves.
    member internal AsArrayOfPreTypeDefs: ILPreTypeDef[]

    /// Calls to <c>FindByName</c> will result in any laziness in the overall
    /// set of ILTypeDefs being read in in addition
    /// to the details for the type found, but the remaining individual
    /// type definitions will not be read.
    member internal FindByName: string -> ILTypeDef

/// Represents IL Type Definitions.
[<NoComparison; NoEquality>]
type ILTypeDef =

    /// Functional creation of a value, using delayed reading via a metadata index, for ilread.fs
    internal new:
        name: string * attributes: TypeAttributes * layout: ILTypeDefLayout * implements: ILTypes * genericParams: ILGenericParameterDefs *
        extends: ILType option * methods: ILMethodDefs * nestedTypes: ILTypeDefs * fields: ILFieldDefs * methodImpls: ILMethodImplDefs *
        events: ILEventDefs * properties: ILPropertyDefs * securityDeclsStored: ILSecurityDeclsStored * customAttrsStored: ILAttributesStored * metadataIndex: int32 -> ILTypeDef

    /// Functional creation of a value, immediate
    new: name: string * attributes: TypeAttributes * layout: ILTypeDefLayout * implements: ILTypes * genericParams: ILGenericParameterDefs *
          extends: ILType option * methods: ILMethodDefs * nestedTypes: ILTypeDefs * fields: ILFieldDefs * methodImpls: ILMethodImplDefs *
          events: ILEventDefs * properties: ILPropertyDefs * securityDecls: ILSecurityDecls * customAttrs: ILAttributes -> ILTypeDef

    member Name: string
    member Attributes: TypeAttributes
    member GenericParams: ILGenericParameterDefs
    member Layout: ILTypeDefLayout
    member NestedTypes: ILTypeDefs
    member Implements: ILTypes
    member Extends: ILType option
    member Methods: ILMethodDefs
    member SecurityDecls: ILSecurityDecls
    member Fields: ILFieldDefs
    member MethodImpls: ILMethodImplDefs
    member Events: ILEventDefs
    member Properties: ILPropertyDefs
    member CustomAttrs: ILAttributes
    member IsClass: bool
    member IsStruct: bool
    member IsInterface: bool
    member IsEnum: bool
    member IsDelegate: bool
    member IsStructOrEnum: bool
    member Access: ILTypeDefAccess
    member IsAbstract: bool
    member IsSealed: bool
    member IsSerializable: bool
    /// Class or interface generated for COM interop.
    member IsComInterop: bool
    member IsSpecialName: bool
    /// Some classes are marked "HasSecurity" even if there are no permissions attached,
    /// e.g. if they use SuppressUnmanagedCodeSecurityAttribute
    member HasSecurity: bool
    member Encoding: ILDefaultPInvokeEncoding

    member internal WithAccess: ILTypeDefAccess -> ILTypeDef
    member internal WithNestedAccess: ILMemberAccess -> ILTypeDef
    member internal WithSealed: bool -> ILTypeDef
    member internal WithSerializable: bool -> ILTypeDef
    member internal WithAbstract: bool -> ILTypeDef
    member internal WithImport: bool -> ILTypeDef
    member internal WithHasSecurity: bool -> ILTypeDef
    member internal WithLayout: ILTypeDefLayout -> ILTypeDef
    member internal WithKind: ILTypeDefKind -> ILTypeDef
    member internal WithEncoding: ILDefaultPInvokeEncoding -> ILTypeDef
    member internal WithSpecialName: bool -> ILTypeDef
    member internal WithInitSemantics: ILTypeInit -> ILTypeDef

    /// Functional update
    member With: ?name: string * ?attributes: TypeAttributes * ?layout: ILTypeDefLayout *  ?implements: ILTypes *
                 ?genericParams:ILGenericParameterDefs * ?extends:ILType option * ?methods:ILMethodDefs *
                 ?nestedTypes:ILTypeDefs * ?fields: ILFieldDefs * ?methodImpls:ILMethodImplDefs * ?events:ILEventDefs *
                 ?properties:ILPropertyDefs * ?customAttrs:ILAttributes * ?securityDecls: ILSecurityDecls -> ILTypeDef

/// Represents a prefix of information for ILTypeDef.
///
/// The information is enough to perform name resolution for the F# compiler, probe attributes
/// for ExtensionAttribute  etc.  This is key to the on-demand exploration of .NET metadata.
/// This information has to be "Goldilocks" - not too much, not too little, just right.
[<NoEquality; NoComparison>]
type ILPreTypeDef =
    abstract Namespace: string list
    abstract Name: string
    /// Realise the actual full typedef
    abstract GetTypeDef : unit -> ILTypeDef


[<NoEquality; NoComparison; Sealed>]
type internal ILPreTypeDefImpl =
    interface ILPreTypeDef

[<Sealed>]
type internal ILTypeDefStored

val internal mkILPreTypeDef : ILTypeDef -> ILPreTypeDef
val internal mkILPreTypeDefComputed : string list * string * (unit -> ILTypeDef) -> ILPreTypeDef
val internal mkILPreTypeDefRead : string list * string * int32 * ILTypeDefStored -> ILPreTypeDef
val internal mkILTypeDefReader: (int32 -> ILTypeDef) -> ILTypeDefStored

[<NoEquality; NoComparison; Sealed>]
type ILNestedExportedTypes =
    member internal AsList: ILNestedExportedType  list

/// "Classes Elsewhere" - classes in auxiliary modules.
///
/// Manifests include declarations for all the classes in an
/// assembly, regardless of which module they are in.
///
/// The ".class extern" construct describes so-called exported types --
/// these are public classes defined in the auxiliary modules of this assembly,
/// i.e. modules other than the manifest-carrying module.
///
/// For example, if you have a two-module
/// assembly (A.DLL and B.DLL), and the manifest resides in the A.DLL,
/// then in the manifest all the public classes declared in B.DLL should
/// be defined as exported types, i.e., as ".class extern". The public classes
/// defined in A.DLL should not be defined as ".class extern" -- they are
/// already available in the manifest-carrying module. The union of all
/// public classes defined in the manifest-carrying module and all
/// exported types defined there is the set of all classes exposed by
/// this assembly. Thus, by analysing the metadata of the manifest-carrying
/// module of an assembly, you can identify all the classes exposed by
/// this assembly, and where to find them.
///
/// Nested classes found in external modules should also be located in
/// this table, suitably nested inside another "ILExportedTypeOrForwarder"
/// definition.

/// these are only found in the "Nested" field of ILExportedTypeOrForwarder objects
// REVIEW: fold this into ILExportedTypeOrForwarder. There's not much value in keeping these distinct
type ILNestedExportedType =
    { Name: string
      Access: ILMemberAccess
      Nested: ILNestedExportedTypes
      CustomAttrsStored: ILAttributesStored
      MetadataIndex: int32 }

    member CustomAttrs: ILAttributes

/// these are only found in the ILExportedTypesAndForwarders table in the manifest
[<NoComparison; NoEquality>]
type ILExportedTypeOrForwarder =
    { ScopeRef: ILScopeRef
      /// [Namespace.]Name
      Name: string
      Attributes: TypeAttributes
      Nested: ILNestedExportedTypes
      CustomAttrsStored: ILAttributesStored
      MetadataIndex: int32 }

    member Access: ILTypeDefAccess

    member IsForwarder: bool

    member CustomAttrs: ILAttributes

[<NoEquality; NoComparison>]
[<Sealed>]
type ILExportedTypesAndForwarders =
    member internal AsList: ILExportedTypeOrForwarder list
    member internal TryFindByName: string -> ILExportedTypeOrForwarder option

[<RequireQualifiedAccess>]
type internal ILResourceAccess =
    | Public
    | Private

[<RequireQualifiedAccess;NoEquality;NoComparison>]
type internal ILResourceLocation =

    /// Represents a manifest resource that can be read or written to a PE file
    | Local of ByteStorage

    /// Represents a manifest resource in an associated file
    | File of ILModuleRef * int32

    /// Represents a manifest resource in a different assembly
    | Assembly of ILAssemblyRef

/// "Manifest ILResources" are chunks of resource data, being one of:
///   - the data section of the current module (byte[] of resource given directly).
///   - in an external file in this assembly (offset given in the ILResourceLocation field).
///   - as a resources in another assembly of the same name.
type internal ILResource =
    { Name: string
      Location: ILResourceLocation
      Access: ILResourceAccess
      CustomAttrsStored: ILAttributesStored
      MetadataIndex: int32 }

    /// Read the bytes from a resource local to an assembly. Will fail for non-local resources.
    member internal GetBytes : unit -> ReadOnlyByteMemory

    member CustomAttrs: ILAttributes

/// Table of resources in a module.
[<NoEquality; NoComparison>]
[<Sealed>]
type ILResources =
    member internal AsList: ILResource  list

[<RequireQualifiedAccess>]
type ILAssemblyLongevity =
    internal
    | Unspecified
    | Library
    | PlatformAppDomain
    | PlatformProcess
    | PlatformSystem

    static member Default : ILAssemblyLongevity

/// The main module of an assembly is a module plus some manifest information.
type ILAssemblyManifest =
    { Name: string
      /// This is the ID of the algorithm used for the hashes of auxiliary
      /// files in the assembly.   These hashes are stored in the
      /// <c>ILModuleRef.Hash</c> fields of this assembly. These are not
      /// cryptographic hashes: they are simple file hashes. The algorithm
      /// is normally <c>0x00008004</c> indicating the SHA1 hash algorithm.
      AuxModuleHashAlgorithm: int32

      SecurityDeclsStored: ILSecurityDeclsStored

      /// This is the public key used to sign this
      /// assembly (the signature itself is stored elsewhere: see the
      /// binary format, and may not have been written if delay signing
      /// is used).  (member Name, member PublicKey) forms the full
      /// public name of the assembly.
      PublicKey: byte[] option

      Version: ILVersionInfo option

      Locale: string option

      CustomAttrsStored: ILAttributesStored

      AssemblyLongevity: ILAssemblyLongevity

      DisableJitOptimizations: bool

      JitTracking: bool

      IgnoreSymbolStoreSequencePoints: bool

      Retargetable: bool

      /// Records the types implemented by this assembly in auxiliary
      /// modules.
      ExportedTypes: ILExportedTypesAndForwarders

      /// Records whether the entrypoint resides in another module.
      EntrypointElsewhere: ILModuleRef option

      MetadataIndex: int32
    }
    member CustomAttrs: ILAttributes
    member SecurityDecls: ILSecurityDecls


[<RequireQualifiedAccess>]
type ILNativeResource =
    internal
    /// Represents a native resource to be read from the PE file
    | In of fileName: string * linkedResourceBase: int * linkedResourceStart: int * linkedResourceLength: int

    /// Represents a native resource to be written in an output file
    | Out of unlinkedResource: byte[]

/// One module in the "current" assembly, either a main-module or
/// an auxiliary module.  The main module will have a manifest.
///
/// An assembly is built by joining together a "main" module plus
/// several auxiliary modules.
type ILModuleDef =
    { Manifest: ILAssemblyManifest option
      Name: string
      TypeDefs: ILTypeDefs
      SubsystemVersion: int * int
      UseHighEntropyVA: bool
      SubSystemFlags: int32
      IsDLL: bool
      IsILOnly: bool
      Platform: ILPlatform option
      StackReserveSize: int32 option
      Is32Bit: bool
      Is32BitPreferred: bool
      Is64Bit: bool
      VirtualAlignment: int32
      PhysicalAlignment: int32
      ImageBase: int32
      MetadataVersion: string
      Resources: ILResources
      /// e.g. win86 resources, as the exact contents of a .res or .obj file. Must be unlinked manually.
      NativeResources: ILNativeResource list
      CustomAttrsStored: ILAttributesStored
      MetadataIndex: int32 }

    member ManifestOfAssembly: ILAssemblyManifest

    member HasManifest: bool

    member CustomAttrs: ILAttributes

/// Find the method definition corresponding to the given property or
/// event operation. These are always in the same class as the property
/// or event. This is useful especially if your code is not using the Ilbind
/// API to bind references.
val internal resolveILMethodRef: ILTypeDef -> ILMethodRef -> ILMethodDef

val internal resolveILMethodRefWithRescope: (ILType -> ILType) -> ILTypeDef -> ILMethodRef -> ILMethodDef

// ------------------------------------------------------------------
// Type Names
//
// The name of a type stored in the Name field is as follows:
//   - For outer types it is, for example, System.String, i.e.
//     the namespace followed by the type name.
//   - For nested types, it is simply the type name.  The namespace
//     must be gleaned from the context in which the nested type
//     lies.
// ------------------------------------------------------------------

val internal splitNamespace: string -> string list

val internal splitNamespaceToArray: string -> string[]

/// The <c>splitILTypeName</c> utility helps you split a string representing
/// a type name into the leading namespace elements (if any), the
/// names of any nested types and the type name itself.  This function
/// memoizes and interns the splitting of the namespace portion of
/// the type name.
val internal splitILTypeName: string -> string list * string

val internal splitILTypeNameWithPossibleStaticArguments: string -> string[] * string

/// <c>splitTypeNameRight</c> is like <c>splitILTypeName</c> except the
/// namespace is kept as a whole string, rather than split at dots.
val internal splitTypeNameRight: string -> string option * string

val internal typeNameForGlobalFunctions: string

val internal isTypeNameForGlobalFunctions: string -> bool

// ====================================================================
// PART 2
//
// Making metadata.  Where no explicit constructor
// is given, you should create the concrete datatype directly,
// e.g. by filling in all appropriate record fields.
// ==================================================================== *)

/// A table of common references to items in primary assembly (System.Runtime or mscorlib).
/// If a particular version of System.Runtime.dll has been loaded then you should
/// reference items from it via an ILGlobals for that specific version built using mkILGlobals.
[<NoEquality; NoComparison; Class>]
type internal ILGlobals =
    member primaryAssemblyScopeRef: ILScopeRef
    member primaryAssemblyRef: ILAssemblyRef
    member primaryAssemblyName: string
    member typ_Object: ILType
    member typ_String: ILType
    member typ_Type: ILType
    member typ_Array: ILType
    member typ_IntPtr: ILType
    member typ_UIntPtr: ILType
    member typ_Byte: ILType
    member typ_Int16: ILType
    member typ_Int32: ILType
    member typ_Int64: ILType
    member typ_SByte: ILType
    member typ_UInt16: ILType
    member typ_UInt32: ILType
    member typ_UInt64: ILType
    member typ_Single: ILType
    member typ_Double: ILType
    member typ_Bool: ILType
    member typ_Char: ILType
    member typ_TypedReference: ILType

    member fsharpCoreAssemblyScopeRef: ILScopeRef

    /// Is the given assembly possibly a primary assembly?
    /// In practice, a primary assembly is an assembly that contains the System.Object type definition
    /// and has no referenced assemblies.
    /// However, we must consider assemblies that forward the System.Object type definition
    /// to be possible primary assemblies.
    /// Therefore, this will return true if the given assembly is the real primary assembly or an assembly that forwards
    /// the System.Object type definition.
    /// Assembly equivalency ignores the version here.
    member IsPossiblePrimaryAssemblyRef: ILAssemblyRef -> bool

/// Build the table of commonly used references given functions to find types in system assemblies
val internal mkILGlobals: primaryScopeRef: ILScopeRef * assembliesThatForwardToPrimaryAssembly: ILAssemblyRef list * fsharpCoreAssemblyScopeRef: ILScopeRef -> ILGlobals

val internal PrimaryAssemblyILGlobals: ILGlobals

/// When writing a binary the fake "toplevel" type definition (called <Module>)
/// must come first. This function puts it first, and creates it in the returned
/// list as an empty typedef if it doesn't already exist.
val internal destTypeDefsWithGlobalFunctionsFirst: ILGlobals -> ILTypeDefs -> ILTypeDef list

/// Not all custom attribute data can be decoded without binding types.  In particular
/// enums must be bound in order to discover the size of the underlying integer.
/// The following assumes enums have size int32.
val internal decodeILAttribData:
    ILAttribute ->
      ILAttribElem list *  (* fixed args *)
      ILAttributeNamedArg list (* named args: values and flags indicating if they are fields or properties *)

/// Generate simple references to assemblies and modules.
val internal mkSimpleAssemblyRef: string -> ILAssemblyRef

val internal mkSimpleModRef: string -> ILModuleRef

val internal mkILTyvarTy: uint16 -> ILType

/// Make type refs.
val internal mkILNestedTyRef: ILScopeRef * string list * string -> ILTypeRef
val internal mkILTyRef: ILScopeRef * string -> ILTypeRef
val internal mkILTyRefInTyRef: ILTypeRef * string -> ILTypeRef

type internal ILGenericArgsList = ILType list

/// Make type specs.
val internal mkILNonGenericTySpec: ILTypeRef -> ILTypeSpec
val internal mkILTySpec: ILTypeRef * ILGenericArgsList -> ILTypeSpec

/// Make types.
val internal mkILTy: ILBoxity -> ILTypeSpec -> ILType
val internal mkILNamedTy: ILBoxity -> ILTypeRef -> ILGenericArgsList -> ILType
val internal mkILBoxedTy: ILTypeRef -> ILGenericArgsList -> ILType
val internal mkILValueTy: ILTypeRef -> ILGenericArgsList -> ILType
val internal mkILNonGenericBoxedTy: ILTypeRef -> ILType
val internal mkILNonGenericValueTy: ILTypeRef -> ILType
val internal mkILArrTy: ILType * ILArrayShape -> ILType
val internal mkILArr1DTy: ILType -> ILType
val internal isILArrTy: ILType -> bool
val internal destILArrTy: ILType -> ILArrayShape * ILType
val internal mkILBoxedType: ILTypeSpec -> ILType

/// Make method references and specs.
val internal mkILMethRef: ILTypeRef * ILCallingConv * string * int * ILType list * ILType -> ILMethodRef
val internal mkILMethSpec: ILMethodRef * ILBoxity * ILGenericArgsList * ILGenericArgsList -> ILMethodSpec
val internal mkILMethSpecForMethRefInTy: ILMethodRef * ILType * ILGenericArgsList -> ILMethodSpec
val internal mkILMethSpecInTy: ILType * ILCallingConv * string * ILType list * ILType * ILGenericArgsList -> ILMethodSpec

/// Construct references to methods on a given type .
val internal mkILNonGenericMethSpecInTy: ILType * ILCallingConv * string * ILType list * ILType -> ILMethodSpec

/// Construct references to instance methods.
val internal mkILInstanceMethSpecInTy: ILType * string * ILType list * ILType * ILGenericArgsList -> ILMethodSpec

/// Construct references to instance methods.
val internal mkILNonGenericInstanceMethSpecInTy: ILType * string * ILType list * ILType -> ILMethodSpec

/// Construct references to static methods.
val internal mkILStaticMethSpecInTy: ILType * string * ILType list * ILType * ILGenericArgsList -> ILMethodSpec

/// Construct references to static, non-generic methods.
val internal mkILNonGenericStaticMethSpecInTy: ILType * string * ILType list * ILType -> ILMethodSpec

/// Construct references to constructors.
val internal mkILCtorMethSpecForTy: ILType * ILType list -> ILMethodSpec
val internal mkILNonGenericCtorMethSpec: ILTypeRef * ILType list -> ILMethodSpec

/// Construct references to fields.
val internal mkILFieldRef: ILTypeRef * string * ILType -> ILFieldRef
val internal mkILFieldSpec: ILFieldRef * ILType -> ILFieldSpec
val internal mkILFieldSpecInTy: ILType * string * ILType -> ILFieldSpec

val internal mkILCallSig: ILCallingConv * ILType list * ILType -> ILCallingSignature

/// Make generalized versions of possibly-generic types, e.g. Given the ILTypeDef for List, return the type "List<T>".
val internal mkILFormalBoxedTy: ILTypeRef -> ILGenericParameterDef list -> ILType
val internal mkILFormalNamedTy: ILBoxity -> ILTypeRef -> ILGenericParameterDef list -> ILType

val internal mkILFormalTypars: ILType list -> ILGenericParameterDefs
val internal mkILFormalGenericArgs: int -> ILGenericParameterDefs -> ILGenericArgsList
val internal mkILSimpleTypar: string -> ILGenericParameterDef

/// Make custom attributes.
val internal mkILCustomAttribMethRef:
    ILMethodSpec
       * ILAttribElem list (* fixed args: values and implicit types *)
       * ILAttributeNamedArg list (* named args: values and flags indicating if they are fields or properties *)
      -> ILAttribute

val internal mkILCustomAttribute:
    ILTypeRef * ILType list *
       ILAttribElem list (* fixed args: values and implicit types *) *
       ILAttributeNamedArg list (* named args: values and flags indicating if they are fields or properties *)
         -> ILAttribute

val internal getCustomAttrData: ILAttribute -> byte[]

val internal mkPermissionSet: ILSecurityAction * (ILTypeRef * (string * ILType * ILAttribElem) list) list -> ILSecurityDecl

/// Making code.
val internal generateCodeLabel: unit -> ILCodeLabel
val internal formatCodeLabel: ILCodeLabel -> string

/// Make some code that is a straight line sequence of instructions.
/// The function will add a "return" if the last instruction is not an exiting instruction.
val internal nonBranchingInstrsToCode: ILInstr list -> ILCode

/// Helpers for codegen: scopes for allocating new temporary variables.
type internal ILLocalsAllocator =
    new: preAlloc: int -> ILLocalsAllocator
    member AllocLocal: ILLocal -> uint16
    member Close: unit -> ILLocal list

/// Derived functions for making some common patterns of instructions.
val internal mkNormalCall: ILMethodSpec -> ILInstr
val internal mkNormalCallvirt: ILMethodSpec -> ILInstr
val internal mkNormalCallconstraint: ILType * ILMethodSpec -> ILInstr
val internal mkNormalNewobj: ILMethodSpec -> ILInstr
val internal mkCallBaseConstructor: ILType * ILType list -> ILInstr list
val internal mkNormalStfld: ILFieldSpec -> ILInstr
val internal mkNormalStsfld: ILFieldSpec -> ILInstr
val internal mkNormalLdsfld: ILFieldSpec -> ILInstr
val internal mkNormalLdfld: ILFieldSpec -> ILInstr
val internal mkNormalLdflda: ILFieldSpec -> ILInstr
val internal mkNormalLdobj: ILType -> ILInstr
val internal mkNormalStobj: ILType -> ILInstr
val internal mkLdcInt32: int32 -> ILInstr
val internal mkLdarg0: ILInstr
val internal mkLdloc: uint16 -> ILInstr
val internal mkStloc: uint16 -> ILInstr
val internal mkLdarg: uint16 -> ILInstr

val internal andTailness: ILTailcall -> bool -> ILTailcall

/// Derived functions for making return, parameter and local variable
/// objects for use in method definitions.
val internal mkILParam: string option * ILType -> ILParameter
val internal mkILParamAnon: ILType -> ILParameter
val internal mkILParamNamed: string * ILType -> ILParameter
val mkILReturn: ILType -> ILReturn
val internal mkILLocal: ILType -> (string * int * int) option -> ILLocal

/// Make a formal generic parameters.
val internal mkILEmptyGenericParams: ILGenericParameterDefs

/// Make method definitions.
val internal mkILMethodBody: initlocals:bool * ILLocals * int * ILCode * ILSourceMarker option -> ILMethodBody
val internal mkMethodBody: bool * ILLocals * int * ILCode * ILSourceMarker option -> MethodBody
val internal methBodyNotAvailable: Lazy<MethodBody>
val internal methBodyAbstract: Lazy<MethodBody>
val internal methBodyNative: Lazy<MethodBody>

val internal mkILCtor: ILMemberAccess * ILParameter list * MethodBody -> ILMethodDef
val internal mkILClassCtor: MethodBody -> ILMethodDef
val internal mkILNonGenericEmptyCtor: ILSourceMarker option -> ILType -> ILMethodDef
val internal mkILStaticMethod: ILGenericParameterDefs * string * ILMemberAccess * ILParameter list * ILReturn * MethodBody -> ILMethodDef
val internal mkILNonGenericStaticMethod: string * ILMemberAccess * ILParameter list * ILReturn * MethodBody -> ILMethodDef
val internal mkILGenericVirtualMethod: string * ILMemberAccess  * ILGenericParameterDefs * ILParameter list * ILReturn * MethodBody -> ILMethodDef
val internal mkILGenericNonVirtualMethod: string * ILMemberAccess  * ILGenericParameterDefs * ILParameter list * ILReturn * MethodBody -> ILMethodDef
val internal mkILNonGenericVirtualMethod: string * ILMemberAccess * ILParameter list * ILReturn * MethodBody -> ILMethodDef
val internal mkILNonGenericInstanceMethod: string * ILMemberAccess  * ILParameter list * ILReturn * MethodBody -> ILMethodDef

/// Make field definitions.
val internal mkILInstanceField: string * ILType * ILFieldInit option * ILMemberAccess -> ILFieldDef
val internal mkILStaticField: string * ILType * ILFieldInit option * byte[] option * ILMemberAccess -> ILFieldDef
val internal mkILLiteralField: string * ILType * ILFieldInit * byte[] option * ILMemberAccess -> ILFieldDef

/// Make a type definition.
val internal mkILGenericClass: string * ILTypeDefAccess * ILGenericParameterDefs * ILType * ILType list * ILMethodDefs * ILFieldDefs * ILTypeDefs * ILPropertyDefs * ILEventDefs * ILAttributes * ILTypeInit -> ILTypeDef
val internal mkILSimpleClass: ILGlobals -> string * ILTypeDefAccess * ILMethodDefs * ILFieldDefs * ILTypeDefs * ILPropertyDefs * ILEventDefs * ILAttributes * ILTypeInit  -> ILTypeDef
val internal mkILTypeDefForGlobalFunctions: ILGlobals -> ILMethodDefs * ILFieldDefs -> ILTypeDef

/// Make a type definition for a value type used to point to raw data.
/// These are useful when generating array initialization code
/// according to the
///   ldtoken    field valuetype '<PrivateImplementationDetails>'/'$$struct0x6000127-1' '<PrivateImplementationDetails>'::'$$method0x6000127-1'
///   call       void System.Runtime.CompilerServices.RuntimeHelpers::InitializeArray(class System.Array,valuetype System.RuntimeFieldHandle)
/// idiom.
val internal mkRawDataValueTypeDef:  ILType -> string * size:int32 * pack:uint16 -> ILTypeDef

/// Injecting code into existing code blocks.  A branch will
/// be added from the given instructions to the (unique) entry of
/// the code, and the first instruction will be the new entry
/// of the method.  The instructions should be non-branching.

val internal prependInstrsToCode: ILInstr list -> ILCode -> ILCode
val internal prependInstrsToMethod: ILInstr list -> ILMethodDef -> ILMethodDef

/// Injecting initialization code into a class.
/// Add some code to the end of the .cctor for a type.  Create a .cctor
/// if one doesn't exist already.
val internal prependInstrsToClassCtor: ILInstr list -> ILSourceMarker option -> ILTypeDef -> ILTypeDef

/// Derived functions for making some simple constructors
val internal mkILStorageCtor: ILSourceMarker option * ILInstr list * ILType * (string * ILType) list * ILMemberAccess -> ILMethodDef
val internal mkILSimpleStorageCtor: ILSourceMarker option * ILTypeSpec option * ILType * ILParameter list * (string * ILType) list * ILMemberAccess -> ILMethodDef
val internal mkILSimpleStorageCtorWithParamNames: ILSourceMarker option * ILTypeSpec option * ILType * ILParameter list * (string * string * ILType) list * ILMemberAccess -> ILMethodDef

val internal mkILDelegateMethods: ILMemberAccess -> ILGlobals -> ILType * ILType -> ILParameter list * ILReturn -> ILMethodDef list

/// Given a delegate type definition which lies in a particular scope,
/// make a reference to its constructor.
val internal mkCtorMethSpecForDelegate: ILGlobals -> ILType * bool -> ILMethodSpec

/// The toplevel "class" for a module or assembly.
val internal mkILTypeForGlobalFunctions: ILScopeRef -> ILType

/// Making tables of custom attributes, etc.
val mkILCustomAttrs: ILAttribute list -> ILAttributes
val mkILCustomAttrsFromArray: ILAttribute[] -> ILAttributes
val storeILCustomAttrs: ILAttributes -> ILAttributesStored
val internal mkILCustomAttrsReader: (int32 -> ILAttribute[]) -> ILAttributesStored
val emptyILCustomAttrs: ILAttributes

val mkILSecurityDecls: ILSecurityDecl list -> ILSecurityDecls
val emptyILSecurityDecls: ILSecurityDecls
val storeILSecurityDecls: ILSecurityDecls -> ILSecurityDeclsStored
val internal mkILSecurityDeclsReader: (int32 -> ILSecurityDecl[]) -> ILSecurityDeclsStored

val mkILEvents: ILEventDef list -> ILEventDefs
val mkILEventsLazy: Lazy<ILEventDef list> -> ILEventDefs
val emptyILEvents: ILEventDefs

val mkILProperties: ILPropertyDef list -> ILPropertyDefs
val mkILPropertiesLazy: Lazy<ILPropertyDef list> -> ILPropertyDefs
val emptyILProperties: ILPropertyDefs

val mkILMethods: ILMethodDef list -> ILMethodDefs
val mkILMethodsFromArray: ILMethodDef[] -> ILMethodDefs
val mkILMethodsComputed: (unit -> ILMethodDef[]) -> ILMethodDefs
val emptyILMethods: ILMethodDefs

val mkILFields: ILFieldDef list -> ILFieldDefs
val mkILFieldsLazy: Lazy<ILFieldDef list> -> ILFieldDefs
val emptyILFields: ILFieldDefs

val mkILMethodImpls: ILMethodImplDef list -> ILMethodImplDefs
val mkILMethodImplsLazy: Lazy<ILMethodImplDef list> -> ILMethodImplDefs
val emptyILMethodImpls: ILMethodImplDefs

val mkILTypeDefs: ILTypeDef list -> ILTypeDefs
val mkILTypeDefsFromArray: ILTypeDef[] -> ILTypeDefs
val emptyILTypeDefs: ILTypeDefs

/// Create table of types which is loaded/computed on-demand, and whose individual
/// elements are also loaded/computed on-demand. Any call to tdefs.AsList will
/// result in the laziness being forced.  Operations can examine the
/// custom attributes and name of each type in order to decide whether
/// to proceed with examining the other details of the type.
///
/// Note that individual type definitions may contain further delays
/// in their method, field and other tables.
val mkILTypeDefsComputed: (unit -> ILPreTypeDef[]) -> ILTypeDefs
val internal addILTypeDef: ILTypeDef -> ILTypeDefs -> ILTypeDefs

val internal mkTypeForwarder: ILScopeRef -> string -> ILNestedExportedTypes -> ILAttributes -> ILTypeDefAccess -> ILExportedTypeOrForwarder
val mkILNestedExportedTypes: ILNestedExportedType list -> ILNestedExportedTypes
val internal mkILNestedExportedTypesLazy: Lazy<ILNestedExportedType list> -> ILNestedExportedTypes

val mkILExportedTypes: ILExportedTypeOrForwarder list -> ILExportedTypesAndForwarders
val internal mkILExportedTypesLazy: Lazy<ILExportedTypeOrForwarder list> ->   ILExportedTypesAndForwarders

val emptyILResources: ILResources
val internal mkILResources: ILResource list -> ILResources

/// Making modules.
val mkILSimpleModule: assemblyName:string -> moduleName:string -> dll:bool -> subsystemVersion: (int * int) -> useHighEntropyVA: bool -> ILTypeDefs -> int32 option -> string option -> int -> ILExportedTypesAndForwarders -> string -> ILModuleDef

/// Generate references to existing type definitions, method definitions
/// etc.  Useful for generating references, e.g. to a  class we're processing
/// Also used to reference type definitions that we've generated.  [ILScopeRef]
/// is normally ILScopeRef.Local, unless we've generated the ILTypeDef in
/// an auxiliary module or are generating multiple assemblies at
/// once.

val internal mkRefForNestedILTypeDef: ILScopeRef -> ILTypeDef list * ILTypeDef -> ILTypeRef
val internal mkRefForILMethod       : ILScopeRef -> ILTypeDef list * ILTypeDef -> ILMethodDef -> ILMethodRef
val internal mkRefForILField       : ILScopeRef -> ILTypeDef list * ILTypeDef -> ILFieldDef  -> ILFieldRef

val internal mkRefToILMethod: ILTypeRef * ILMethodDef -> ILMethodRef
val internal mkRefToILField: ILTypeRef * ILFieldDef -> ILFieldRef

val internal mkRefToILAssembly: ILAssemblyManifest -> ILAssemblyRef
val internal mkRefToILModule: ILModuleDef -> ILModuleRef

val NoMetadataIdx: int32

// --------------------------------------------------------------------
// Rescoping.
//
// Given an object O1 referenced from where1 (e.g. O1 binds to some
// result R when referenced from where1), and given that SR2 resolves to where1 from where2,
// produce a new O2 for use from where2 (e.g. O2 binds to R from where2)
//
// So, ILScopeRef tells you how to reference the original scope from
// the new scope. e.g. if ILScopeRef is:
//    [ILScopeRef.Local] then the object is returned unchanged
//    [ILScopeRef.Module m] then an object is returned
//                        where all ILScopeRef.Local references
//                        become ILScopeRef.Module m
//    [ILScopeRef.Assembly m] then an object is returned
//                         where all ILScopeRef.Local and ILScopeRef.Module references
//                        become ILScopeRef.Assembly m
// --------------------------------------------------------------------

/// Rescoping. The first argument tells the function how to reference the original scope from
/// the new scope.
val internal rescopeILScopeRef: ILScopeRef -> ILScopeRef -> ILScopeRef

/// Rescoping. The first argument tells the function how to reference the original scope from
/// the new scope.
val internal rescopeILTypeSpec: ILScopeRef -> ILTypeSpec -> ILTypeSpec

/// Rescoping. The first argument tells the function how to reference the original scope from
/// the new scope.
val internal rescopeILType: ILScopeRef -> ILType -> ILType

/// Rescoping. The first argument tells the function how to reference the original scope from
/// the new scope.
val internal rescopeILMethodRef: ILScopeRef -> ILMethodRef -> ILMethodRef

/// Rescoping. The first argument tells the function how to reference the original scope from
/// the new scope.
val internal rescopeILFieldRef: ILScopeRef -> ILFieldRef -> ILFieldRef

/// Unscoping. Clears every scope information, use for looking up IL method references only.
val internal unscopeILType: ILType -> ILType

val internal buildILCode: string -> lab2pc: Dictionary<ILCodeLabel,int> -> instrs:ILInstr[] -> ILExceptionSpec list -> ILLocalDebugInfo list -> ILCode

/// Instantiate type variables that occur within types and other items.
val internal instILTypeAux: int -> ILGenericArgs -> ILType -> ILType

/// Instantiate type variables that occur within types and other items.
val internal instILType: ILGenericArgs -> ILType -> ILType

/// This is a 'vendor neutral' way of referencing mscorlib.
val internal ecmaPublicKey: PublicKey

/// Strips ILType.Modified from the ILType.
val internal stripILModifiedFromTy: ILType -> ILType

/// Discriminating different important built-in types.
val internal isILObjectTy: ILGlobals -> ILType -> bool
val internal isILStringTy: ILGlobals -> ILType -> bool
val internal isILSByteTy: ILGlobals -> ILType -> bool
val internal isILByteTy: ILGlobals -> ILType -> bool
val internal isILInt16Ty: ILGlobals -> ILType -> bool
val internal isILUInt16Ty: ILGlobals -> ILType -> bool
val internal isILInt32Ty: ILGlobals -> ILType -> bool
val internal isILUInt32Ty: ILGlobals -> ILType -> bool
val internal isILInt64Ty: ILGlobals -> ILType -> bool
val internal isILUInt64Ty: ILGlobals -> ILType -> bool
val internal isILIntPtrTy: ILGlobals -> ILType -> bool
val internal isILUIntPtrTy: ILGlobals -> ILType -> bool
val internal isILBoolTy: ILGlobals -> ILType -> bool
val internal isILCharTy: ILGlobals -> ILType -> bool
val internal isILTypedReferenceTy: ILGlobals -> ILType -> bool
val internal isILDoubleTy: ILGlobals -> ILType -> bool
val internal isILSingleTy: ILGlobals -> ILType -> bool

val internal sha1HashInt64 : byte[] -> int64
/// Get a public key token from a public key.
val internal sha1HashBytes: byte[] -> byte[] (* SHA1 hash *)

/// Get a version number from a CLR version string, e.g. 1.0.3705.0
val internal parseILVersion: string -> ILVersionInfo
val internal formatILVersion: ILVersionInfo -> string
val internal compareILVersions: ILVersionInfo -> ILVersionInfo -> int

/// Decompose a type definition according to its kind.
type internal ILEnumInfo =
    { enumValues: (string * ILFieldInit) list
      enumType: ILType }

val internal getTyOfILEnumInfo: ILEnumInfo -> ILType

val internal computeILEnumInfo: string * ILFieldDefs -> ILEnumInfo

/// A utility type provided for completeness
[<Sealed>]
type internal ILEventRef =
    static member Create: ILTypeRef * string -> ILEventRef
    member DeclaringTypeRef: ILTypeRef
    member Name: string

/// A utility type provided for completeness
[<Sealed>]
type internal ILPropertyRef =
     static member Create: ILTypeRef * string -> ILPropertyRef
     member DeclaringTypeRef: ILTypeRef
     member Name: string
     interface System.IComparable

type internal ILReferences =
    { AssemblyReferences: ILAssemblyRef list
      ModuleReferences: ILModuleRef list }

/// Find the full set of assemblies referenced by a module.
val internal computeILRefs: ILGlobals -> ILModuleDef -> ILReferences
val internal emptyILRefs: ILReferences

