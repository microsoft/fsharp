// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.Infos

open FSharp.Compiler
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.Syntax
open FSharp.Compiler.Import
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.Xml
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeOps

#if !NO_EXTENSIONTYPING
open FSharp.Compiler.ExtensionTyping
#endif

/// Import an IL type as an F# type. importInst gives the context for interpreting type variables.
val ImportILType: scoref:ILScopeRef -> amap:ImportMap -> m:range -> importInst:TType list -> ilty:ILType -> TType

val CanImportILType: scoref:ILScopeRef -> amap:ImportMap -> m:range -> ilty:ILType -> bool

/// Indicates if an F# type is the type associated with an F# exception declaration
val isExnDeclTy: g:TcGlobals -> ty:TType -> bool

/// Get the base type of a type, taking into account type instantiations. Return None if the
/// type has no base type.
val GetSuperTypeOfType: g:TcGlobals -> amap:ImportMap -> m:range -> ty:TType -> TType option

/// Indicates whether we can skip interface types that lie outside the reference set
[<RequireQualifiedAccess>]
type SkipUnrefInterfaces =
    | Yes
    | No

/// Collect the set of immediate declared interface types for an F# type, but do not
/// traverse the type hierarchy to collect further interfaces.
val GetImmediateInterfacesOfType: skipUnref:SkipUnrefInterfaces -> g:TcGlobals -> amap:ImportMap -> m:range -> ty:TType -> TType list

/// Indicates whether we should visit multiple instantiations of the same generic interface or not
[<RequireQualifiedAccess>]
type AllowMultiIntfInstantiations =
    | Yes
    | No

/// Fold, do not follow interfaces (unless the type is itself an interface)
val FoldPrimaryHierarchyOfType: f:(TType -> 'a -> 'a) -> g:TcGlobals -> amap:ImportMap -> m:range -> allowMultiIntfInst:AllowMultiIntfInstantiations -> ty:TType -> acc:'a -> 'a

/// Fold, following interfaces. Skipping interfaces that lie outside the referenced assembly set is allowed.
val FoldEntireHierarchyOfType: f:(TType -> 'a -> 'a) -> g:TcGlobals -> amap:ImportMap -> m:range -> allowMultiIntfInst:AllowMultiIntfInstantiations -> ty:TType -> acc:'a -> 'a

/// Iterate, following interfaces. Skipping interfaces that lie outside the referenced assembly set is allowed.
val IterateEntireHierarchyOfType: f:(TType -> unit) -> g:TcGlobals -> amap:ImportMap -> m:range -> allowMultiIntfInst:AllowMultiIntfInstantiations -> ty:TType -> unit

/// Search for one element satisfying a predicate, following interfaces
val ExistsInEntireHierarchyOfType: f:(TType -> bool) -> g:TcGlobals -> amap:ImportMap -> m:range -> allowMultiIntfInst:AllowMultiIntfInstantiations -> ty:TType -> bool

/// Search for one element where a function returns a 'Some' result, following interfaces
val SearchEntireHierarchyOfType: f:(TType -> bool) -> g:TcGlobals -> amap:ImportMap -> m:range -> ty:TType -> TType option

/// Get all super types of the type, including the type itself
val AllSuperTypesOfType: g:TcGlobals -> amap:ImportMap -> m:range -> allowMultiIntfInst:AllowMultiIntfInstantiations -> ty:TType -> TType list

/// Get all interfaces of a type, including the type itself if it is an interface
val AllInterfacesOfType: g:TcGlobals -> amap:ImportMap -> m:range -> allowMultiIntfInst:AllowMultiIntfInstantiations -> ty:TType -> TType list

/// Check if two types have the same nominal head type
val HaveSameHeadType: g:TcGlobals -> ty1:TType -> ty2:TType -> bool

/// Check if a type has a particular head type
val HasHeadType: g:TcGlobals -> tcref:TyconRef -> ty2:TType -> bool

/// Check if a type exists somewhere in the hierarchy which has the same head type as the given type (note, the given type need not have a head type at all)
val ExistsSameHeadTypeInHierarchy: g:TcGlobals -> amap:ImportMap -> m:range -> typeToSearchFrom:TType -> typeToLookFor:TType -> bool

/// Check if a type exists somewhere in the hierarchy which has the given head type.
val ExistsHeadTypeInEntireHierarchy: g:TcGlobals -> amap:ImportMap -> m:range -> typeToSearchFrom:TType -> tcrefToLookFor:TyconRef -> bool

/// Read an Abstract IL type from metadata and convert to an F# type.
val ImportILTypeFromMetadata: amap:ImportMap -> m:range -> scoref:ILScopeRef -> tinst:TType list -> minst:TType list -> ilty:ILType -> TType

/// Read an Abstract IL type from metadata, including any attributes that may affect the type itself, and convert to an F# type.
val ImportILTypeFromMetadataWithAttributes: amap:ImportMap -> m:range -> scoref:ILScopeRef -> tinst:TType list -> minst:TType list -> ilty:ILType -> cattrs:ILAttributes -> TType

/// Get the parameter type of an IL method.
val ImportParameterTypeFromMetadata: amap:ImportMap -> m:range -> ilty:ILType -> cattrs:ILAttributes -> scoref:ILScopeRef -> tinst:TType list -> mist:TType list -> TType

/// Get the return type of an IL method, taking into account instantiations for type, return attributes and method generic parameters, and
/// translating 'void' to 'None'.
val ImportReturnTypeFromMetadata: amap:ImportMap -> m:range -> ilty:ILType -> cattrs:ILAttributes -> scoref:ILScopeRef -> tinst:TType list -> minst:TType list -> TType option

/// Copy constraints.  If the constraint comes from a type parameter associated
/// with a type constructor then we are simply renaming type variables.  If it comes
/// from a generic method in a generic class (e.g. ty.M<_>) then we may be both substituting the
/// instantiation associated with 'ty' as well as copying the type parameters associated with
/// M and instantiating their constraints
///
/// Note: this now looks identical to constraint instantiation.

val CopyTyparConstraints: traitCtxt: ITraitContext option -> m:range -> tprefInst:TypedTreeOps.TyparInst -> tporig:Typar -> TyparConstraint list

/// The constraints for each typar copied from another typar can only be fixed up once
/// we have generated all the new constraints, e.g. f<A :> List<B>, B :> List<A>> ...
val FixupNewTypars: traitCtxt: ITraitContext option -> m:range -> formalEnclosingTypars:Typars -> tinst:TType list -> tpsorig:Typars -> tps:Typars -> TypedTreeOps.TyparInst * TTypes

type ValRef with
    /// Indicates if an F#-declared function or member value is a CLIEvent property compiled as a .NET event
    member IsFSharpEventProperty: g:TcGlobals -> bool

    /// Check if an F#-declared member value is a virtual method
    member IsVirtualMember: bool

    /// Check if an F#-declared member value is a dispatch slot
    member IsDispatchSlotMember: bool

    /// Check if an F#-declared member value is an 'override' or explicit member implementation
    member IsDefiniteFSharpOverrideMember: bool

    /// Check if an F#-declared member value is an  explicit interface member implementation
    member IsFSharpExplicitInterfaceImplementation: g:TcGlobals -> bool

    member ImplementedSlotSignatures: SlotSig list

#if !NO_EXTENSIONTYPING
/// Get the return type of a provided method, where 'void' is returned as 'None'
val GetCompiledReturnTyOfProvidedMethodInfo: amap:ImportMap ->m:range -> mi:Tainted<ProvidedMethodBase> -> TType option
#endif

/// The slotsig returned by methInfo.GetSlotSig is in terms of the type parameters on the parent type of the overriding method.
/// Reverse-map the slotsig so it is in terms of the type parameters for the overriding method
val ReparentSlotSigToUseMethodTypars: g:TcGlobals -> m:range -> ovByMethValRef:ValRef -> slotsig:SlotSig -> SlotSig

/// Construct the data representing a parameter in the signature of an abstract method slot
val MakeSlotParam: ty:TType * argInfo:ArgReprInfo -> SlotParam

/// Construct the data representing the signature of an abstract method slot
val MakeSlotSig: nm:string * ty:TType * ctps:Typars * mtps:Typars * paraml:SlotParam list list * retTy:TType option -> SlotSig

/// Describes the sequence order of the introduction of an extension method. Extension methods that are introduced
/// later through 'open' get priority in overload resolution.
type ExtensionMethodPriority = uint64

/// The caller-side value for the optional arg, if any
type OptionalArgCallerSideValue =
    | Constant of ILFieldInit
    | DefaultValue
    | MissingValue
    | WrapperForIDispatch
    | WrapperForIUnknown
    | PassByRef of TType * OptionalArgCallerSideValue

/// Represents information about a parameter indicating if it is optional.
type OptionalArgInfo =
    /// The argument is not optional
    | NotOptional

    /// The argument is optional, and is an F# callee-side optional arg
    | CalleeSide

    /// The argument is optional, and is a caller-side .NET optional or default arg.
    /// Note this is correctly termed caller side, even though the default value is optically specified on the callee:
    /// in fact the default value is read from the metadata and passed explicitly to the callee on the caller side.
    | CallerSide of OptionalArgCallerSideValue

    static member FieldInitForDefaultParameterValueAttrib: attrib:Attrib -> ILFieldInit option

    /// Compute the OptionalArgInfo for an IL parameter
    ///
    /// This includes the Visual Basic rules for IDispatchConstant and IUnknownConstant and optional arguments.
    static member FromILParameter: g:TcGlobals -> amap:ImportMap -> m:range -> ilScope:ILScopeRef -> ilTypeInst:TType list -> ilParam:ILParameter -> OptionalArgInfo

    static member ValueOfDefaultParameterValueAttrib: Attrib -> Expr option

    member IsOptional: bool
  
type CallerInfo =
    | NoCallerInfo
    | CallerLineNumber
    | CallerMemberName
    | CallerFilePath
  
[<RequireQualifiedAccess>]
type ReflectedArgInfo =
    | None
    | Quote of bool

    member AutoQuote: bool
  
/// Partial information about a parameter returned for use by the Language Service
[<NoComparison; NoEquality>]
type ParamNameAndType =
    | ParamNameAndType of Ident option * TType

    static member FromArgInfo: ty:TType * argInfo:ArgReprInfo -> ParamNameAndType

    static member FromMember: isCSharpExtMem:bool -> g:TcGlobals -> vref:ValRef -> ParamNameAndType list list

    static member Instantiate: inst:TypedTreeOps.TyparInst -> p:ParamNameAndType -> ParamNameAndType

    static member InstantiateCurried: inst:TypedTreeOps.TyparInst -> paramTypes:ParamNameAndType list list -> ParamNameAndType list list
  
/// Full information about a parameter returned for use by the type checker and language service.
[<NoComparison; NoEquality>]
type ParamData =
    | ParamData of
      isParamArray: bool * 
      isInArg: bool * 
      isOut: bool *
      optArgInfo: OptionalArgInfo * 
      callerInfo: CallerInfo *
      nameOpt: Ident option * 
      reflArgInfo: ReflectedArgInfo *
      ttype: TType

/// Describes an F# use of an IL type, including the type instantiation associated with the type at a particular usage point.
[<NoComparison; NoEquality>]
type ILTypeInfo =
    | ILTypeInfo of TcGlobals * TType * ILTypeRef * ILTypeDef
    static member FromType: g:TcGlobals -> ty:TType -> ILTypeInfo

    member Instantiate: inst:TypedTreeOps.TyparInst -> ILTypeInfo

    member ILScopeRef: ILScopeRef

    member ILTypeRef: ILTypeRef

    member IsValueType: bool

    member Name: string

    member RawMetadata: ILTypeDef

    member TcGlobals: TcGlobals

    /// Get the compiled nominal type. In the case of tuple types, this is a .NET tuple type
    member ToAppType: TType

    member ToType: TType

    member TyconRefOfRawMetadata: TyconRef

    member TypeInstOfRawMetadata: TypeInst
  
/// Describes an F# use of an IL method.
[<NoComparison; NoEquality>]
type ILMethInfo =
    | ILMethInfo of g: TcGlobals * ilApparentType: TType * ilDeclaringTyconRefOpt: TyconRef option  * ilMethodDef: ILMethodDef * ilGenericMethodTyArgs: Typars

    /// Like ApparentEnclosingType but use the compiled nominal type if this is a method on a tuple type
    member ApparentEnclosingAppType: TType

    /// Get the apparent declaring type of the method as an F# type.
    /// If this is a C#-style extension method then this is the type which the method
    /// appears to extend. This may be a variable type.
    member ApparentEnclosingType: TType

    /// Get the declaring type of the method. If this is an C#-style extension method then this is the IL type
    /// holding the static member that is the extension method.
    member DeclaringTyconRef: TyconRef

    /// Get the instantiation of the declaring type of the method.
    /// If this is an C#-style extension method then this is empty because extension members
    /// are never in generic classes.
    member DeclaringTypeInst: TType list

    /// Get the formal method type parameters associated with a method.
    member FormalMethodTypars: Typars

    /// Get the declaring type associated with an extension member, if any.
    member ILExtensionMethodDeclaringTyconRef: TyconRef option

    /// Get a reference to the method (dropping all generic instantiations), as an Abstract IL ILMethodRef.
    member ILMethodRef: ILMethodRef

    /// Get the IL name of the method
    member ILName: string

    /// Indicates if the IL method is marked abstract.
    member IsAbstract: bool

    /// Indicates if the method is a class initializer.
    member IsClassConstructor: bool

    /// Indicates if the method is a constructor
    member IsConstructor: bool

    /// Indicates if the IL method is marked final.
    member IsFinal: bool

    /// Indicates if the method is an extension method
    member IsILExtensionMethod: bool

    /// Does it appear to the user as an instance method?
    member IsInstance: bool

    /// Does it have the .NET IL 'newslot' flag set, and is also a virtual?
    member IsNewSlot: bool

    /// Indicates if the method has protected accessibility,
    member IsProtectedAccessibility: bool

    /// Does it appear to the user as a static method?
    member IsStatic: bool

    /// Indicates if the IL method is marked virtual.
    member IsVirtual: bool

    /// Get the Abstract IL scope information associated with interpreting the Abstract IL metadata that backs this method.
    member MetadataScope: ILScopeRef

    /// Get the number of parameters of the method
    member NumParams: int

    /// Get the Abstract IL metadata corresponding to the parameters of the method.
    /// If this is an C#-style extension method then drop the object argument.
    member ParamMetadata: ILParameter list

    /// Get the Abstract IL metadata associated with the method.
    member RawMetadata: ILMethodDef

    member TcGlobals: TcGlobals
  
    /// Get the compiled return type of the method, where 'void' is None.
    member GetCompiledReturnTy: amap:ImportMap * m:range * minst:TType list -> TType option

    /// Get the F# view of the return type of the method, where 'void' is 'unit'.
    member GetFSharpReturnTy: amap:ImportMap * m:range * minst:TType list -> TType

    /// Get the (zero or one) 'self'/'this'/'object' arguments associated with an IL method.
    /// An instance extension method returns one object argument.
    member GetObjArgTypes: amap:ImportMap * m:range * minst:TType list -> TType list

    /// Get info about the arguments of the IL method. If this is an C#-style extension method then
    /// drop the object argument.
    ///
    /// Any type parameters of the enclosing type are instantiated in the type returned.
    member GetParamNamesAndTypes: amap:ImportMap * m:range * minst:TType list -> ParamNameAndType list

    /// Get the argument types of the the IL method. If this is an C#-style extension method
    /// then drop the object argument.
    member GetParamTypes: amap:ImportMap * m:range * minst:TType list -> TType list

    /// Get all the argument types of the IL method. Include the object argument even if this is
    /// an C#-style extension method.
    member GetRawArgTypes: amap:ImportMap * m:range * minst:TType list -> TType list

    /// Indicates if the method is marked as a DllImport (a PInvoke). This is done by looking at the IL custom attributes on
    /// the method.
    member IsDllImport: g:TcGlobals -> bool

    /// Indicates if the method is marked with the [<IsReadOnly>] attribute. This is done by looking at the IL custom attributes on
    /// the method.
    member IsReadOnly: g:TcGlobals -> bool

/// Describes an F# use of a method
[<NoComparison; NoEquality>]
type MethInfo =

    /// Describes a use of a method declared in F# code and backed by F# metadata.
    | FSMeth of tcGlobals: TcGlobals * enclosingType: TType * valRef: ValRef  * extensionMethodPriority: ExtensionMethodPriority option

    /// Describes a use of a method backed by Abstract IL # metadata
    | ILMeth of tcGlobals: TcGlobals * ilMethInfo: ILMethInfo * extensionMethodPriority: ExtensionMethodPriority option

    /// Describes a use of a pseudo-method corresponding to the default constructor for a .NET struct type
    | DefaultStructCtor of tcGlobals: TcGlobals * structTy: TType

#if !NO_EXTENSIONTYPING
    /// Describes a use of a method backed by provided metadata
    | ProvidedMeth of amap: Import.ImportMap * methodBase: Tainted<ProvidedMethodBase> * extensionMethodPriority: ExtensionMethodPriority option * m: range
#endif

    interface ITraitExtensionMember

    /// Get the enclosing type of the method info, using a nominal type for tuple types
    member ApparentEnclosingAppType: TType

    member ApparentEnclosingTyconRef: TyconRef

    /// Get the enclosing type of the method info.
    ///
    /// If this is an extension member, then this is the apparent parent, i.e. the type the method appears to extend.
    /// This may be a variable type.
    member ApparentEnclosingType: TType

    /// Try to get an arbitrary F# ValRef associated with the member. This is to determine if the member is virtual, amongst other things.
    member ArbitraryValRef: ValRef option

     /// Get the method name in DebuggerDisplayForm
    member DebuggerDisplayName: string

    /// Get the declaring type or module holding the method. If this is an C#-style extension method then this is the type
    /// holding the static member that is the extension method. If this is an F#-style extension method it is the logical module
    /// holding the value for the extension method.
    member DeclaringTyconRef: TyconRef

    /// Get the actual type instantiation of the declaring type associated with this use of the method.
    ///
    /// For extension members this is empty (the instantiation of the declaring type).
    member DeclaringTypeInst: TType list

    /// Get the method name in DisplayName form
    member DisplayName: string

    /// Get the extension method priority of the method. If it is not an extension method
    /// then use the highest possible value since non-extension methods always take priority
    /// over extension members.
    member ExtensionMemberPriority: ExtensionMethodPriority

    /// Get the extension method priority of the method, if it has one.
    member ExtensionMemberPriorityOption: ExtensionMethodPriority option

    /// Get the formal generic method parameters for the method as a list of variable types.
    member FormalMethodInst: TypeInst

    member FormalMethodTyparInst: TypedTreeOps.TyparInst

    /// Get the formal generic method parameters for the method as a list of type variables.
    ///
    /// For an extension method this includes all type parameters, even if it is extending a generic type.
    member FormalMethodTypars: Typars

    /// Get the number of generic method parameters for a method.
    /// For an extension method this includes all type parameters, even if it is extending a generic type.
    member GenericArity: int

    /// Indicates if this is a method defined in this assembly with an internal XML comment
    member HasDirectXmlComment: bool

    member ImplementedSlotSignatures: SlotSig list

    // Is this particular MethInfo one that doesn't provide an implementation?
    ///
    // For F# methods, this is 'true' for the MethInfos corresponding to 'abstract' declarations,
    // and false for the (potentially) matching 'default' implementation MethInfos that eventually
    // provide an implementation for the dispatch slot.
    //
    // For IL methods, this is 'true' for abstract methods, and 'false' for virtual methods
    member IsAbstract: bool

    /// Indicates if this is an C#-style extension member.
    member IsCSharpStyleExtensionMember: bool

    member IsClassConstructor: bool

    member IsConstructor: bool

    member IsCurried: bool

    /// Check if this method is marked 'override' and thus definitely overrides another method.
    member IsDefiniteFSharpOverride: bool

    member IsDispatchSlot: bool

    /// Indicates if this is an extension member.
    member IsExtensionMember: bool

    /// Indicates if this method is a generated method associated with an F# CLIEvent property compiled as a .NET event
    member IsFSharpEventPropertyMethod: bool

    /// Check if this method is an explicit implementation of an interface member
    member IsFSharpExplicitInterfaceImplementation: bool

    /// Indicates if this is an F#-style extension member.
    member IsFSharpStyleExtensionMember: bool

    member IsFinal: bool

    /// Indicates if this is an IL method.
    member IsILMethod: bool

    /// Does the method appear to the user as an instance method?
    member IsInstance: bool

    member IsNewSlot: bool

    /// Indicates if this method takes no arguments
    member IsNullary: bool

    member IsProtectedAccessibility: bool

    /// Indicates if this method is read-only; usually by the [<IsReadOnly>] attribute.
    /// Must be an instance method.
    /// Receiver must be a struct type.
    member IsReadOnly: bool

    /// Indicates if the enclosing type for the method is a value type.
    ///
    /// For an extension method, this indicates if the method extends a struct type.
    member IsStruct: bool

    member IsVirtual: bool

    /// Get the method name in LogicalName form, i.e. the name as it would be stored in .NET metadata
    member LogicalName: string

    /// Get a list of argument-number counts, one count for each set of curried arguments.
    ///
    /// For an extension member, drop the 'this' argument.
    member NumArgs: int list

/// Get the information about provided static parameters, if any
#if NO_EXTENSIONTYPING
    member ProvidedStaticParameterInfo: obj option
#else
    member ProvidedStaticParameterInfo: (Tainted<ProvidedMethodBase> * Tainted<ProvidedParameterInfo> []) option
#endif

    /// Get the TcGlobals value that governs the method declaration
    member TcGlobals: TcGlobals

    /// Get the XML documentation associated with the method
    member XmlDoc: XmlDoc
  
    /// Build IL method infos.
    static member CreateILMeth: amap:ImportMap * m:range * ty:TType * md:ILMethodDef -> MethInfo

    /// Build IL method infos for a C#-style extension method
    static member CreateILExtensionMeth: amap:ImportMap * m:range * apparentTy:TType * declaringTyconRef:TyconRef * extMethPri:ExtensionMethodPriority option * md:ILMethodDef -> MethInfo

    /// Tests whether two method infos have the same underlying definition.
    /// Used to merge operator overloads collected from left and right of an operator constraint.
    ///
    /// Compatible with ItemsAreEffectivelyEqual relation.
    static member MethInfosUseIdenticalDefinitions: x1:MethInfo -> x2:MethInfo -> bool

    /// Add the actual type instantiation of the apparent type of an F# extension method.
    member AdjustUserTypeInstForFSharpStyleIndexedExtensionMembers: tyargs:TType list -> TType list

    /// Calculates a hash code of method info. Compatible with ItemsAreEffectivelyEqual relation.
    member ComputeHashCode: unit -> int

    /// Get the return type of a method info, where 'void' is returned as 'None'
    member GetCompiledReturnTy: amap:ImportMap * m:range * minst:TType list -> TType option

    /// Get the return type of a method info, where 'void' is returned as 'unit'
    member GetFSharpReturnTy: amap:ImportMap * m:range * minst:TType list -> TType

    /// Select all the type parameters of the declaring type of a method.
    ///
    /// For extension methods, no type parameters are returned, because all the
    /// type parameters are part of the apparent type, rather the
    /// declaring type, even for extension methods extending generic types.
    member GetFormalTyparsOfDeclaringType: m:range -> Typar list

    /// Get the (zero or one) 'self'/'this'/'object' arguments associated with a method.
    /// An instance method returns one object argument.
    member GetObjArgTypes: amap:ImportMap * m:range * minst:TypeInst -> TType list

    /// Get the parameter attributes of a method info, which get combined with the parameter names and types
    member GetParamAttribs: amap:ImportMap * m:range -> (bool * bool * bool * OptionalArgInfo * CallerInfo * ReflectedArgInfo) list list

    /// Get the ParamData objects for the parameters of a MethInfo
    member GetParamDatas: amap:ImportMap * m:range * minst:TType list -> ParamData list list

    /// Get the parameter types of a method info
    member GetParamTypes: amap:ImportMap * m:range * minst:TType list -> TType list list

    /// Get the signature of an abstract method slot.
    member GetSlotSig: amap:ImportMap * m:range * traitCtxt: ITraitContext option -> SlotSig

    /// Get the ParamData objects for the parameters of a MethInfo
    member HasParamArrayArg: amap:ImportMap * m:range * minst:TType list -> bool

    /// Apply a type instantiation to a method info, i.e. apply the instantiation to the enclosing type.
    member Instantiate: amap:ImportMap * m:range * inst:TypedTreeOps.TyparInst -> MethInfo

    /// Indicates if this method is an extension member that is read-only.
    /// An extension member is considered read-only if the first argument is a read-only byref (inref) type.
    member IsReadOnlyExtensionMember: amap:ImportMap * m:range -> bool

    /// Indicates if this is an extension member (e.g. on a struct) that takes a byref arg
    member ObjArgNeedsAddress: amap:ImportMap * m:range -> bool

    /// Tries to get the object arg type if it's a byref type.
    member TryObjArgByrefType: amap:ImportMap * m:range * minst:TypeInst -> TType option

/// Represents a single use of a IL or provided field from one point in an F# program
[<NoComparison; NoEquality>]
type ILFieldInfo =
     /// Represents a single use of a field backed by Abstract IL metadata
    | ILFieldInfo of ilTypeInfo: ILTypeInfo * ilFieldDef: ILFieldDef

#if !NO_EXTENSIONTYPING
     /// Represents a single use of a field backed by provided metadata
    | ProvidedField of amap: ImportMap * providedField: Tainted<ProvidedFieldInfo> * range: range
#endif

    /// Like ApparentEnclosingType but use the compiled nominal type if this is a method on a tuple type
    member ApparentEnclosingAppType: TType

    member ApparentEnclosingTyconRef: TyconRef

    /// Get the enclosing ("parent"/"declaring") type of the field.
    member ApparentEnclosingType: TType

    member DeclaringTyconRef: TyconRef

     /// Get the name of the field
    member FieldName: string

     /// Get an (uninstantiated) reference to the field as an Abstract IL ILFieldRef
    member ILFieldRef: ILFieldRef

     /// Get the type of the field as an IL type
    member ILFieldType: ILType

     /// Get a reference to the declaring type of the field as an ILTypeRef
    member ILTypeRef: ILTypeRef

     /// Indicates if the field is readonly (in the .NET/C# sense of readonly)
    member IsInitOnly: bool

     /// Indicates if the field has the 'specialname' property in the .NET IL
    member IsSpecialName: bool

     /// Indicates if the field is static
    member IsStatic: bool

    /// Indicates if the field is a member of a struct or enum type
    member IsValueType: bool

     /// Indicates if the field is a literal field with an associated literal value
    member LiteralValue: ILFieldInit option

     /// Get the scope used to interpret IL metadata
    member ScopeRef: ILScopeRef

    member TcGlobals: TcGlobals

    /// Get the type instantiation of the declaring type of the field
    member TypeInst: TypeInst

    /// Tests whether two infos have the same underlying definition.
    /// Compatible with ItemsAreEffectivelyEqual relation.
    static member ILFieldInfosUseIdenticalDefinitions: x1:ILFieldInfo -> x2:ILFieldInfo -> bool

    /// Calculates a hash code of field info. Must be compatible with ItemsAreEffectivelyEqual relation.
    member ComputeHashCode: unit -> int

     /// Get the type of the field as an F# type
    member FieldType: amap:ImportMap * m:range -> TType

/// Describes an F# use of a field in an F#-declared record, class or struct type
[<NoComparison; NoEquality>]
type RecdFieldInfo =
    | RecdFieldInfo of typeInst: TypeInst * recdFieldRef: RecdFieldRef

    /// Get the enclosing (declaring) type of the field in an F#-declared record, class or struct type
    member DeclaringType: TType

    /// Get the (instantiated) type of the field in an F#-declared record, class or struct type
    member FieldType: TType

    /// Indicate if the field is a static field in an F#-declared record, class or struct type
    member IsStatic: bool

    /// Indicate if the field is a literal field in an F#-declared record, class or struct type
    member LiteralValue: Const option

    /// Get the name of the field in an F#-declared record, class or struct type
    member Name: string

    /// Get the F# metadata for the uninstantiated field
    member RecdField: RecdField

    /// Get a reference to the F# metadata for the uninstantiated field
    member RecdFieldRef: RecdFieldRef

    /// Get the F# metadata for the F#-declared record, class or struct type
    member Tycon: Entity

    /// Get a reference to the F# metadata for the F#-declared record, class or struct type
    member TyconRef: TyconRef

    /// Get the generic instantiation of the declaring type of the field
    member TypeInst: TypeInst

/// Describes an F# use of a union case
[<NoComparison; NoEquality>]
type UnionCaseInfo =
    | UnionCaseInfo of TypeInst * UnionCaseRef

    /// Get the name of the union case
    member Name: string

    /// Get the F# metadata for the declaring union type
    member Tycon: Entity

    /// Get a reference to the F# metadata for the declaring union type
    member TyconRef: TyconRef

    /// Get the list of types for the instantiation of the type parameters of the declaring type of the union case
    member TypeInst: TypeInst

    /// Get the F# metadata for the uninstantiated union case
    member UnionCase: UnionCase

    /// Get a reference to the F# metadata for the uninstantiated union case
    member UnionCaseRef: UnionCaseRef

    /// Get the instantiation of the type parameters of the declaring type of the union case
    member GetTyparInst: m:range -> TypedTreeOps.TyparInst

/// Describes an F# use of a property backed by Abstract IL metadata
[<NoComparison; NoEquality>]
type ILPropInfo =
    | ILPropInfo of ilTypeInfo: ILTypeInfo * ilPropertyDef: ILPropertyDef

    /// Like ApparentEnclosingType but use the compiled nominal type if this is a method on a tuple type
    member ApparentEnclosingAppType: TType

    /// Get the apparent declaring type of the method as an F# type.
    /// If this is a C#-style extension method then this is the type which the method
    /// appears to extend. This may be a variable type.
    member ApparentEnclosingType: TType

    /// Gets the ILMethInfo of the 'get' method for the IL property
    member GetterMethod: ILMethInfo

    /// Indicates if the IL property has a 'get' method
    member HasGetter: bool

    /// Indicates if the IL property has a 'set' method
    member HasSetter: bool

    /// Get the declaring IL type of the IL property, including any generic instantiation
    member ILTypeInfo: ILTypeInfo

    /// Indicates if the IL property is logically a 'newslot', i.e. hides any previous slots of the same name.
    member IsNewSlot: bool

    /// Indicates if the IL property is static
    member IsStatic: bool

    /// Indicates if the IL property is virtual
    member IsVirtual: bool

    /// Get the name of the IL property
    member PropertyName: string

    /// Get the raw Abstract IL metadata for the IL property
    member RawMetadata: ILPropertyDef

    /// Gets the ILMethInfo of the 'set' method for the IL property
    member SetterMethod: ILMethInfo

    /// Get the TcGlobals governing this value
    member TcGlobals: TcGlobals
  
    /// Get the names and types of the indexer arguments associated with the IL property.
    ///
    /// Any type parameters of the enclosing type are instantiated in the type returned.
    member GetParamNamesAndTypes: amap:ImportMap * m:range -> ParamNameAndType list

    /// Get the types of the indexer arguments associated with the IL property.
    ///
    /// Any type parameters of the enclosing type are instantiated in the type returned.
    member GetParamTypes: amap:ImportMap * m:range -> TType list

    /// Get the return type of the IL property.
    ///
    /// Any type parameters of the enclosing type are instantiated in the type returned.
    member GetPropertyType: amap:ImportMap * m:range -> TType

/// Describes an F# use of a property
[<NoComparison; NoEquality>]
type PropInfo =
    /// An F# use of a property backed by F#-declared metadata
    | FSProp of tcGlobals: TcGlobals * apparentEnclTy: TType * getter: ValRef option * setter: ValRef option

    /// An F# use of a property backed by Abstract IL metadata
    | ILProp of ilPropInfo: ILPropInfo

#if !NO_EXTENSIONTYPING
    /// An F# use of a property backed by provided metadata
    | ProvidedProp of amap: ImportMap * providedProp: Tainted<ProvidedPropertyInfo> * range: range
#endif

    /// Get the enclosing type of the method info, using a nominal type for tuple types
    member ApparentEnclosingAppType: TType

    member ApparentEnclosingTyconRef: TyconRef

    /// Get the enclosing type of the property.
    ///
    /// If this is an extension member, then this is the apparent parent, i.e. the type the property appears to extend.
    member ApparentEnclosingType: TType

    /// Try to get an arbitrary F# ValRef associated with the member. This is to determine if the member is virtual, amongst other things.
    member ArbitraryValRef: ValRef option

    /// Get the declaring type or module holding the method.
    /// Note that C#-style extension properties don't exist in the C# design as yet.
    /// If this is an F#-style extension method it is the logical module
    /// holding the value for the extension method.
    member DeclaringTyconRef: EntityRef

    /// Return a new property info where there is no associated getter, only an associated setter.
    ///
    /// Property infos can combine getters and setters, assuming they are consistent w.r.t. 'virtual', indexer argument types etc.
    /// When checking consistency we split these apart
    member DropGetter: unit -> PropInfo

    /// Return a new property info where there is no associated setter, only an associated getter.
    ///
    /// Property infos can combine getters and setters, assuming they are consistent w.r.t. 'virtual', indexer argument types etc.
    /// When checking consistency we split these apart
    member DropSetter: unit -> PropInfo

    member GetterMethod: MethInfo

    /// Indicates if this property has an associated XML comment authored in this assembly.
    member HasDirectXmlComment: bool

    /// Indicates if this property has an associated getter method.
    member HasGetter: bool

    /// Indicates if this property has an associated setter method.
    member HasSetter: bool

    member ImplementedSlotSignatures: SlotSig list

    /// Indicates if this property is marked 'override' and thus definitely overrides another property.
    member IsDefiniteFSharpOverride: bool

    /// Indicates if the getter (or, if absent, the setter) for the property is a dispatch slot.
    member IsDispatchSlot: bool

    /// Indicates if this is an extension member
    member IsExtensionMember: bool

    /// Indicates if this is an F# property compiled as a CLI event, e.g. a [<CLIEvent>] property.
    member IsFSharpEventProperty: bool

    member IsFSharpExplicitInterfaceImplementation: bool

    /// Indicates if this property is an indexer property, i.e. a property with arguments.
    member IsIndexer: bool

    /// Indicates if the property is logically a 'newslot', i.e. hides any previous slots of the same name.
    member IsNewSlot: bool

    /// Indicates if this property is static.
    member IsStatic: bool

    /// Indicates if the enclosing type for the property is a value type.
    ///
    /// For an extension property, this indicates if the property extends a struct type.
    member IsValueType: bool

    /// True if the getter (or, if absent, the setter) is a virtual method
    member IsVirtualProperty: bool

    /// Get the logical name of the property.
    member PropertyName: string

    /// Get a MethInfo for the 'setter' method associated with the property
    member SetterMethod: MethInfo

    /// Get the TcGlobals associated with the object
    member TcGlobals: TcGlobals

    /// Get the intra-assembly XML documentation for the property.
    member XmlDoc: XmlDoc

    /// Test whether two property infos have the same underlying definition.
    /// Uses the same techniques as 'MethInfosUseIdenticalDefinitions'.
    /// Compatible with ItemsAreEffectivelyEqual relation.
    static member PropInfosUseIdenticalDefinitions: x1:PropInfo -> x2:PropInfo -> bool

    /// Calculates a hash code of property info. Must be compatible with ItemsAreEffectivelyEqual relation.
    member ComputeHashCode: unit -> int

    /// Get the details of the indexer parameters associated with the property
    member GetParamDatas: amap:ImportMap * m:range -> ParamData list

    /// Get the names and types of the indexer parameters associated with the property
    ///
    /// If the property is in a generic type, then the type parameters are instantiated in the types returned.
    member GetParamNamesAndTypes: amap:ImportMap * m:range -> ParamNameAndType list

    /// Get the types of the indexer parameters associated with the property
    member GetParamTypes: amap:ImportMap * m:range -> TType list

    /// Get the result type of the property
    member GetPropertyType: amap:ImportMap * m:range -> TType

/// Describes an F# use of an event backed by Abstract IL metadata
[<NoComparison; NoEquality>]
type ILEventInfo =
    | ILEventInfo of ilTypeInfo: ILTypeInfo * ilEventDef: ILEventDef

    /// Get the ILMethInfo describing the 'add' method associated with the event
    member AddMethod: ILMethInfo

    // Note: events are always associated with nominal types
    member ApparentEnclosingAppType: TType

    /// Get the enclosing ("parent"/"declaring") type of the field.
    member ApparentEnclosingType: TType

    // Note: IL Events are never extension members as C# has no notion of extension events as yet
    member DeclaringTyconRef: TyconRef

    /// Get the declaring IL type of the event as an ILTypeInfo
    member ILTypeInfo: ILTypeInfo

    /// Indicates if the property is static
    member IsStatic: bool

    /// Get the name of the event
    member Name: string

    /// Get the raw Abstract IL metadata for the event
    member RawMetadata: ILEventDef

    /// Get the ILMethInfo describing the 'remove' method associated with the event
    member RemoveMethod: ILMethInfo

    member TcGlobals: TcGlobals

    /// Get the declaring type of the event as an ILTypeRef
    member TypeRef: ILTypeRef
  
/// Describes an F# use of an event
[<NoComparison; NoEquality>]
type EventInfo =
    /// An F# use of an event backed by F#-declared metadata
    | FSEvent of tcGlobals: TcGlobals * propInfo: PropInfo * addMethod: ValRef * removeMethod: ValRef

    /// An F# use of an event backed by .NET metadata
    | ILEvent of ilEventInfo: ILEventInfo

#if !NO_EXTENSIONTYPING
    /// An F# use of an event backed by provided metadata
    | ProvidedEvent of amap: Import.ImportMap * providedEvent: Tainted<ProvidedEventInfo> * range: range
#endif

    /// Get the enclosing type of the method info, using a nominal type for tuple types
    member ApparentEnclosingAppType: TType

    member ApparentEnclosingTyconRef: TyconRef

    /// Get the enclosing type of the event.
    ///
    /// If this is an extension member, then this is the apparent parent, i.e. the type the event appears to extend.
    member ApparentEnclosingType: TType

    /// Try to get an arbitrary F# ValRef associated with the member. This is to determine if the member is virtual, amongst other things.
    member ArbitraryValRef: ValRef option

    /// Get the declaring type or module holding the method.
    /// Note that C#-style extension properties don't exist in the C# design as yet.
    /// If this is an F#-style extension method it is the logical module
    /// holding the value for the extension method.
    member DeclaringTyconRef: EntityRef

    /// Get the logical name of the event.
    member EventName: string

    /// Indicates if this event has an associated XML comment authored in this assembly.
    member HasDirectXmlComment: bool

    /// Indicates if this is an extension member
    member IsExtensionMember: bool

    /// Indicates if this property is static.
    member IsStatic: bool

    /// Indicates if the enclosing type for the event is a value type.
    ///
    /// For an extension event, this indicates if the event extends a struct type.
    member IsValueType: bool

    /// Get the 'add' method associated with an event
    member AddMethod: MethInfo

    /// Get the 'remove' method associated with an event
    member RemoveMethod: MethInfo

    /// Get the TcGlobals associated with the object
    member TcGlobals: TcGlobals

    /// Get the intra-assembly XML documentation for the property.
    member XmlDoc: XmlDoc
  
    /// Test whether two event infos have the same underlying definition.
    /// Compatible with ItemsAreEffectivelyEqual relation.
    static member EventInfosUseIdenticalDefinitions: x1:EventInfo -> x2:EventInfo -> bool

    /// Calculates a hash code of event info (similar as previous)
    /// Compatible with ItemsAreEffectivelyEqual relation.
    member ComputeHashCode: unit -> int

    /// Get the delegate type associated with the event.
    member GetDelegateType: amap:ImportMap * m:range -> TType

/// An exception type used to raise an error using the old error system.
///
/// Error text: "A definition to be compiled as a .NET event does not have the expected form. Only property members can be compiled as .NET events."
exception BadEventTransformation of range

/// Create an error object to raise should an event not have the shape expected by the .NET idiom described further below
val nonStandardEventError: nm:System.String -> m:range -> exn

/// Find the delegate type that an F# event property implements by looking through the type hierarchy of the type of the property
/// for the first instantiation of IDelegateEvent.
val FindDelegateTypeOfPropertyEvent: g:TcGlobals -> amap:ImportMap -> nm:System.String -> m:range -> ty:TType -> TType

/// Strips inref and outref to be a byref.
val stripByrefTy: g:TcGlobals -> ty:TType -> TType

/// Represents the information about the compiled form of a method signature. Used when analyzing implementation
/// relations between members and abstract slots.
type CompiledSig = CompiledSig of argTys: TType list list * returnTy: TType option * formalMethTypars: Typars * formalMethTyparInst: TyparInst

/// Get the information about the compiled form of a method signature. Used when analyzing implementation
/// relations between members and abstract slots.
val CompiledSigOfMeth: g:TcGlobals -> amap:ImportMap -> m:range -> minfo:MethInfo -> CompiledSig

/// Inref and outref parameter types will be treated as a byref type for equivalency.
val MethInfosEquivByPartialSig: erasureFlag:TypedTreeOps.Erasure -> ignoreFinal:bool -> g:TcGlobals -> amap:ImportMap -> m:range -> minfo:MethInfo -> minfo2:MethInfo -> bool

/// Used to hide/filter members from super classes based on signature
/// Inref and outref parameter types will be treated as a byref type for equivalency.
val MethInfosEquivByNameAndPartialSig: erasureFlag:TypedTreeOps.Erasure -> ignoreFinal:bool -> g:TcGlobals -> amap:ImportMap -> m:range -> minfo:MethInfo -> minfo2:MethInfo -> bool

/// Used to hide/filter members from super classes based on signature
val PropInfosEquivByNameAndPartialSig: erasureFlag:TypedTreeOps.Erasure -> g:TcGlobals -> amap:ImportMap -> m:range -> pinfo:PropInfo -> pinfo2:PropInfo -> bool

/// Used to hide/filter members from base classes based on signature
val MethInfosEquivByNameAndSig: erasureFlag:TypedTreeOps.Erasure -> ignoreFinal:bool -> g:TcGlobals -> amap:ImportMap -> m:range -> minfo:MethInfo -> minfo2:MethInfo -> bool

/// Used to hide/filter members from super classes based on signature
val PropInfosEquivByNameAndSig: erasureFlag:TypedTreeOps.Erasure -> g:TcGlobals -> amap:ImportMap -> m:range -> pinfo:PropInfo -> pinfo2:PropInfo -> bool

val SettersOfPropInfos: pinfos:PropInfo list -> (MethInfo * PropInfo option) list

val GettersOfPropInfos: pinfos:PropInfo list -> (MethInfo * PropInfo option) list

