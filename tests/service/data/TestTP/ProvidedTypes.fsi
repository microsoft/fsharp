// Copyright (c) Microsoft Corporation 2005-2014 and other contributors.
// This sample code is provided "as is" without warranty of any kind.
// We disclaim all warranties, either express or implied, including the
// warranties of merchantability and fitness for a particular purpose.
//
// This file contains a set of helper types and methods for providing types in an implementation
// of ITypeProvider.
//
// This code has been modified and is appropriate for use in conjunction with the F# 3.0-4.0 releases

namespace ProviderImplementation.ProvidedTypes

open System
open System.Reflection
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Core.CompilerServices

/// Represents an erased provided parameter
[<Class>]
type ProvidedParameter =
    inherit ParameterInfo

    /// Create a new provided parameter.
    new : parameterName: string * parameterType: Type * ?isOut: bool * ?optionalValue: obj -> ProvidedParameter

    /// Indicates if the parameter is marked as ParamArray
    member IsParamArray: bool with set

    /// Indicates if the parameter is marked as ReflectedDefinition
    member IsReflectedDefinition: bool with set

    /// Indicates if the parameter has a default value
    member HasDefaultParameterValue: bool

    /// Add a custom attribute to the provided parameter.
    member AddCustomAttribute: CustomAttributeData -> unit

/// Represents a provided static parameter.
[<Class>]
type ProvidedStaticParameter =
    inherit ParameterInfo

    /// Create a new provided static parameter, for use with DefineStaticParamaeters on a provided type definition.
    new: parameterName: string * parameterType: Type * ?parameterDefaultValue: obj -> ProvidedStaticParameter

    /// Add XML documentation information to this provided constructor
    member AddXmlDoc: xmlDoc: string -> unit

    /// Add XML documentation information to this provided constructor, where the computation of the documentation is delayed until necessary
    member AddXmlDocDelayed: xmlDocFunction: (unit -> string) -> unit

/// Represents an erased provided constructor.
[<Class>]
type ProvidedConstructor =
    inherit ConstructorInfo

    /// When making a cross-targeting type provider, use this method instead of the ProvidedConstructor constructor from ProvidedTypes
    new: parameters: ProvidedParameter list * invokeCode: (Expr list -> Expr) -> ProvidedConstructor

    /// Add a 'Obsolete' attribute to this provided constructor
    member AddObsoleteAttribute: message: string * ?isError: bool -> unit

    /// Add XML documentation information to this provided constructor
    member AddXmlDoc: xmlDoc: string -> unit

    /// Add XML documentation information to this provided constructor, where the computation of the documentation is delayed until necessary
    member AddXmlDocDelayed: xmlDocFunction: (unit -> string) -> unit

    /// Add XML documentation information to this provided constructor, where the documentation is re-computed  every time it is required.
    member AddXmlDocComputed: xmlDocFunction: (unit -> string) -> unit

    /// Set the target and arguments of the base constructor call. Only used for generated types.
    member BaseConstructorCall: (Expr list -> ConstructorInfo * Expr list) with set

    /// Set a flag indicating that the constructor acts like an F# implicit constructor, so the
    /// parameters of the constructor become fields and can be accessed using Expr.GlobalVar with the
    /// same name.
    member IsImplicitConstructor: bool with get,set

    /// Add definition location information to the provided constructor.
    member AddDefinitionLocation: line:int * column:int * filePath:string -> unit

    member IsTypeInitializer: bool with get,set

    /// This method is for internal use only in the type provider SDK
    member internal GetInvokeCode: Expr list -> Expr

[<Class>]
type ProvidedMethod =
    inherit MethodInfo

    /// When making a cross-targeting type provider, use this method instead of the ProvidedMethod constructor from ProvidedTypes
    new: methodName: string * parameters: ProvidedParameter list * returnType: Type * ?invokeCode: (Expr list -> Expr) * ?isStatic: bool -> ProvidedMethod

    /// Add XML documentation information to this provided method
    member AddObsoleteAttribute: message: string * ?isError: bool -> unit

    /// Add XML documentation information to this provided constructor
    member AddXmlDoc: xmlDoc: string -> unit

    /// Add XML documentation information to this provided constructor, where the computation of the documentation is delayed until necessary
    member AddXmlDocDelayed: xmlDocFunction: (unit -> string) -> unit

    /// Add XML documentation information to this provided constructor, where the computation of the documentation is delayed until necessary
    /// The documentation is re-computed  every time it is required.
    member AddXmlDocComputed: xmlDocFunction: (unit -> string) -> unit

    member AddMethodAttrs: attributes:MethodAttributes -> unit

    /// Set the method attributes of the method. By default these are simple 'MethodAttributes.Public'
    member SetMethodAttrs: attributes:MethodAttributes -> unit

    /// Add definition location information to the provided type definition.
    member AddDefinitionLocation: line:int * column:int * filePath:string -> unit

    /// Add a custom attribute to the provided method definition.
    member AddCustomAttribute: CustomAttributeData -> unit

    /// Define the static parameters available on a statically parameterized method
    member DefineStaticParameters: parameters: ProvidedStaticParameter list * instantiationFunction: (string -> obj[] -> ProvidedMethod) -> unit

    /// This method is for internal use only in the type provider SDK
    member internal GetInvokeCode: (Expr list -> Expr) option

/// Represents an erased provided property.
[<Class>]
type ProvidedProperty =
    inherit PropertyInfo

    /// Create a new provided property. It is not initially associated with any specific provided type definition.
    new: propertyName: string * propertyType: Type * ?getterCode: (Expr list -> Expr) * ?setterCode: (Expr list -> Expr) * ?isStatic: bool * ?indexParameters: ProvidedParameter list -> ProvidedProperty

    /// Add a 'Obsolete' attribute to this provided property
    member AddObsoleteAttribute: message: string * ?isError: bool -> unit

    /// Add XML documentation information to this provided constructor
    member AddXmlDoc: xmlDoc: string -> unit

    /// Add XML documentation information to this provided constructor, where the computation of the documentation is delayed until necessary
    member AddXmlDocDelayed: xmlDocFunction: (unit -> string) -> unit

    /// Add XML documentation information to this provided constructor, where the computation of the documentation is delayed until necessary
    /// The documentation is re-computed  every time it is required.
    member AddXmlDocComputed: xmlDocFunction: (unit -> string) -> unit

    /// Get or set a flag indicating if the property is static.
    member IsStatic: bool

    /// Add definition location information to the provided type definition.
    member AddDefinitionLocation: line:int * column:int * filePath:string -> unit

    /// Add a custom attribute to the provided property definition.
    member AddCustomAttribute: CustomAttributeData -> unit

/// Represents an erased provided property.
[<Class>]
type ProvidedEvent =
    inherit EventInfo

    /// Create a new provided event. It is not initially associated with any specific provided type definition.
    new: eventName: string * eventHandlerType: Type * adderCode: (Expr list -> Expr) * removerCode: (Expr list -> Expr) * ?isStatic: bool -> ProvidedEvent

    /// Add XML documentation information to this provided constructor
    member AddXmlDoc: xmlDoc: string -> unit

    /// Add XML documentation information to this provided constructor, where the computation of the documentation is delayed until necessary
    member AddXmlDocDelayed: xmlDocFunction: (unit -> string) -> unit

    /// Add XML documentation information to this provided constructor, where the computation of the documentation is delayed until necessary
    /// The documentation is re-computed  every time it is required.
    member AddXmlDocComputed: xmlDocFunction: (unit -> string) -> unit

    /// Get a flag indicating if the property is static.
    member IsStatic: bool with get

    /// Add definition location information to the provided type definition.
    member AddDefinitionLocation: line:int * column:int * filePath:string -> unit

/// Represents an erased provided field.
[<Class>]
type ProvidedField =
    inherit FieldInfo

    /// Create a new provided field. It is not initially associated with any specific provided type definition.
    new: fieldName: string * fieldType: Type -> ProvidedField

    /// Add a 'Obsolete' attribute to this provided field
    member AddObsoleteAttribute: message: string * ?isError: bool -> unit

    /// Add XML documentation information to this provided field
    member AddXmlDoc: xmlDoc: string -> unit

    /// Add XML documentation information to this provided field, where the computation of the documentation is delayed until necessary
    member AddXmlDocDelayed: xmlDocFunction: (unit -> string) -> unit

    /// Add XML documentation information to this provided field, where the computation of the documentation is delayed until necessary
    /// The documentation is re-computed  every time it is required.
    member AddXmlDocComputed: xmlDocFunction: (unit -> string) -> unit

    /// Add definition location information to the provided field definition.
    member AddDefinitionLocation: line:int * column:int * filePath:string -> unit

    member SetFieldAttributes: attributes: FieldAttributes -> unit

    /// Add a custom attribute to the provided property definition.
    member AddCustomAttribute: CustomAttributeData -> unit

    /// Create a new provided literal field. It is not initially associated with any specific provided type definition.
    static member Literal : fieldName: string * fieldType: Type * literalValue:obj -> ProvidedField

/// Represents an array or other symbolic type involving a provided type as the argument.
/// See the type provider spec for the methods that must be implemented.
/// Note that the type provider specification does not require us to implement pointer-equality for provided types.
[<Class>]
type ProvidedTypeSymbol =
    inherit TypeDelegator

    /// For example, kg
    member IsFSharpTypeAbbreviation: bool

    /// For example, int<kg> or int<kilogram>
    member IsFSharpUnitAnnotated: bool

/// Helpers to build symbolic provided types
[<Class>]
type ProvidedTypeBuilder =

    /// Like typ.MakeGenericType, but will also work with unit-annotated types
    static member MakeGenericType: genericTypeDefinition: Type * genericArguments: Type list -> Type

    /// Like methodInfo.MakeGenericMethod, but will also work with unit-annotated types and provided types
    static member MakeGenericMethod: genericMethodDefinition: MethodInfo * genericArguments: Type list -> MethodInfo

    /// Like FsharpType.MakeTupleType, but will also work with unit-annotated types and provided types
    static member MakeTupleType: types: Type list*isStruct: bool -> Type

    /// Like FsharpType.MakeTupleType, but will also work with unit-annotated types and provided types
    static member MakeTupleType: types: Type list -> Type

/// Helps create erased provided unit-of-measure annotations.
[<Class>]
type ProvidedMeasureBuilder =

    /// Gets the measure indicating the "1" unit of measure, that is the unitless measure.
    static member One: Type

    /// Returns the measure indicating the product of two units of measure, e.g. kg * m
    static member Product: measure1: Type * measure2: Type  -> Type

    /// Returns the measure indicating the inverse of two units of measure, e.g. 1 / s
    static member Inverse: denominator: Type -> Type

    /// Returns the measure indicating the ratio of two units of measure, e.g. kg / m
    static member Ratio: numerator: Type * denominator: Type -> Type

    /// Returns the measure indicating the square of a unit of measure, e.g. m * m
    static member Square: ``measure``: Type -> Type

    /// Returns the measure for an SI unit from the F# core library, where the string is in capitals and US spelling, e.g. Meter
    static member SI: unitName:string -> Type

    /// Returns a type where the type has been annotated with the given types and/or units-of-measure.
    /// e.g. float<kg>, Vector<int, kg>
    static member AnnotateType: basic: Type * argument: Type list -> Type

/// Represents a provided type definition.
[<Class>]
type ProvidedTypeDefinition =
    inherit TypeDelegator

    /// When making a cross-targeting type provider, use this method instead of the corresponding ProvidedTypeDefinition constructor from ProvidedTypes
    new: className: string * baseType: Type option * ?hideObjectMethods: bool * ?nonNullable: bool * ?isErased: bool * ?isSealed: bool * ?isInterface: bool * ?isAbstract: bool -> ProvidedTypeDefinition

    /// When making a cross-targeting type provider, use this method instead of the corresponding ProvidedTypeDefinition constructor from ProvidedTypes
    new: assembly: Assembly * namespaceName: string * className: string * baseType: Type option * ?hideObjectMethods: bool * ?nonNullable: bool * ?isErased: bool * ?isSealed: bool * ?isInterface: bool * ?isAbstract: bool -> ProvidedTypeDefinition

    /// Add the given type as an implemented interface.
    member AddInterfaceImplementation: interfaceType: Type -> unit

    /// Add the given function as a set of on-demand computed interfaces.
    member AddInterfaceImplementationsDelayed: interfacesFunction:(unit -> Type list)-> unit

    /// Specifies that the given method body implements the given method declaration.
    member DefineMethodOverride: methodInfoBody: ProvidedMethod * methodInfoDeclaration: MethodInfo -> unit

    /// Specifies that the given method bodies implement the given method declarations
    member DefineMethodOverridesDelayed: (unit -> (ProvidedMethod * MethodInfo) list) -> unit

    /// Add a 'Obsolete' attribute to this provided type definition
    member AddObsoleteAttribute: message: string * ?isError: bool -> unit

    /// Add XML documentation information to this provided constructor
    member AddXmlDoc: xmlDoc: string -> unit

    /// Set the base type
    member SetBaseType: Type -> unit

    /// Set the base type to a lazily evaluated value. Use this to delay realization of the base type as late as possible.
    member SetBaseTypeDelayed: baseTypeFunction:(unit -> Type) -> unit

    /// Set underlying type for generated enums
    member SetEnumUnderlyingType: Type -> unit

    /// Add XML documentation information to this provided constructor, where the computation of the documentation is delayed until necessary.
    /// The documentation is only computed once.
    member AddXmlDocDelayed: xmlDocFunction: (unit -> string) -> unit

    /// Add XML documentation information to this provided constructor, where the computation of the documentation is delayed until necessary
    /// The documentation is re-computed  every time it is required.
    member AddXmlDocComputed: xmlDocFunction: (unit -> string) -> unit

    /// Set the attributes on the provided type. This fully replaces the default TypeAttributes.
    member SetAttributes: TypeAttributes -> unit

    /// Add a method, property, nested type or other member to a ProvidedTypeDefinition
    member AddMember: memberInfo:MemberInfo      -> unit

    /// Add a set of members to a ProvidedTypeDefinition
    member AddMembers: memberInfos:list<#MemberInfo> -> unit

    /// Add a member to a ProvidedTypeDefinition, delaying computation of the members until required by the compilation context.
    member AddMemberDelayed: memberFunction:(unit -> #MemberInfo)      -> unit

    /// Add a set of members to a ProvidedTypeDefinition, delaying computation of the members until required by the compilation context.
    member AddMembersDelayed: membersFunction:(unit -> list<#MemberInfo>) -> unit

    /// Add the types of the generated assembly as generative types, where types in namespaces get hierarchically positioned as nested types.
    member AddAssemblyTypesAsNestedTypesDelayed: assemblyFunction:(unit -> Assembly) -> unit

    /// Define the static parameters available on a statically parameterized type
    member DefineStaticParameters: parameters: ProvidedStaticParameter list * instantiationFunction: (string -> obj[] -> ProvidedTypeDefinition) -> unit

    /// Add definition location information to the provided type definition.
    member AddDefinitionLocation: line:int * column:int * filePath:string -> unit

    /// Suppress Object entries in intellisense menus in instances of this provided type
    member HideObjectMethods: bool 

    /// Disallows the use of the null literal.
    member NonNullable: bool

    /// Get a flag indicating if the ProvidedTypeDefinition is erased
    member IsErased: bool 

    /// Get or set a flag indicating if the ProvidedTypeDefinition has type-relocation suppressed
    [<Experimental("SuppressRelocation is a workaround and likely to be removed")>]
    member SuppressRelocation: bool  with get,set

    // This method is used by Debug.fs
    member ApplyStaticArguments: name:string * args:obj[] -> ProvidedTypeDefinition

    /// Add a custom attribute to the provided type definition.
    member AddCustomAttribute: CustomAttributeData -> unit

    /// Emulate the F# type provider type erasure mechanism to get the
    /// actual (erased) type. We erase ProvidedTypes to their base type
    /// and we erase array of provided type to array of base type. In the
    /// case of generics all the generic type arguments are also recursively
    /// replaced with the erased-to types
    static member EraseType: typ:Type -> Type

    /// Get or set a utility function to log the creation of root Provided Type. Used to debug caching/invalidation.
    static member Logger: (string -> unit) option ref


#if !NO_GENERATIVE
/// A provided generated assembly
type ProvidedAssembly =

    inherit Assembly

    /// Create a provided generated assembly
    new: assemblyName: AssemblyName * assemblyFileName:string -> ProvidedAssembly

    /// Create a provided generated assembly using a temporary file as the interim assembly storage
    new: unit -> ProvidedAssembly

    /// Emit the given provided type definitions as part of the assembly
    /// and adjust the 'Assembly' property of all provided type definitions to return that
    /// assembly.
    ///
    /// The assembly is only emitted when the Assembly property on the root type is accessed for the first time.
    /// The host F# compiler does this when processing a generative type declaration for the type.
    member AddTypes: types: ProvidedTypeDefinition list -> unit

    /// <summary>
    /// Emit the given nested provided type definitions as part of the assembly.
    /// and adjust the 'Assembly' property of all provided type definitions to return that
    /// assembly.
    /// </summary>
    /// <param name="types">Provided type definitions. </param>
    /// <param name="enclosingGeneratedTypeNames">A path of type names to wrap the generated types. The generated types are then generated as nested types.</param>
    member AddNestedTypes: types: ProvidedTypeDefinition list * enclosingGeneratedTypeNames: string list -> unit

#endif

[<Class>]
/// Represents the context for which code is to be generated. Normally you should not need to use this directly.
type ProvidedTypesContext = 
        
    /// Try to find the given target assembly in the context
    member TryBindAssemblyNameToTarget: aref: AssemblyName -> Choice<Assembly, exn> 

    /// Try to find the given target assembly in the context
    member TryBindSimpleAssemblyNameToTarget: assemblyName: string  -> Choice<Assembly, exn> 

    /// Get the list of referenced assemblies determined by the type provider configuration
    member ReferencedAssemblyPaths: string list

    /// Get the resolved referenced assemblies determined by the type provider configuration
    member GetTargetAssemblies : unit -> Assembly[]

    /// Get the set of design-time assemblies available to use as a basis for authoring provided types.
    member GetSourceAssemblies : unit -> Assembly[]

    /// Add an assembly to the set of design-time assemblies available to use as a basis for authoring provided types
    member AddSourceAssembly : Assembly -> unit

    /// Try to get the version of FSharp.Core referenced. May raise an exception if FSharp.Core has not been correctly resolved
    member FSharpCoreAssemblyVersion: Version

        /// Returns a type from the referenced assemblies that corresponds to the given design-time type.  Normally
        /// this method should not be used directly when authoring a type provider.
    member ConvertSourceTypeToTarget: Type -> Type

        /// Returns the design-time type that corresponds to the given type from the target referenced assemblies.  Normally
        /// this method should not be used directly when authoring a type provider.
    member ConvertTargetTypeToSource: Type -> Type

        /// Returns a quotation rebuilt with resepct to the types from the target referenced assemblies.  Normally
        /// this method should not be used directly when authoring a type provider.
    member ConvertSourceExprToTarget: Expr -> Expr

    /// Read the assembly related to this context
    member ReadRelatedAssembly: fileName: string -> Assembly

    /// Read the assembly related to this context
    member ReadRelatedAssembly: bytes: byte[] -> Assembly

/// A base type providing default implementations of type provider functionality.
type TypeProviderForNamespaces =

    /// <summary>Initializes a type provider to provide the types in the given namespace.</summary>
    /// <param name="config"> Type provider config. </param>
    /// <param name="namespaceName"> Name of namespace. </param>
    /// <param name="types"> Provided type definitions. </param>
    ///               
    /// <param name="sourceAssemblies">
    ///    Optionally specify the design-time assemblies available to use as a basis for authoring provided types.
    ///    The transitive dependencies of these assemblies are also included. By default
    ///    Assembly.GetCallingAssembly() and its transitive dependencies are used.
    /// </param>
    ///               
    /// <param name="assemblyReplacementMap">
    ///    Optionally specify a map of assembly names from source model to referenced assemblies.
    /// </param>
    ///               
    /// <param name="addDefaultProbingLocation">
    ///    Optionally specify that the location of the type provider design-time component should be used to resolve failing assembly resolutions.
    ///    This flag or an equivalent call to RegisterProbingFolder is generally needed for any type provider design-time components loaded into .NET Core tooling.
    /// </param>
    new: config: TypeProviderConfig * namespaceName:string * types: ProvidedTypeDefinition list * ?sourceAssemblies: Assembly list * ?assemblyReplacementMap: (string * string) list * ?addDefaultProbingLocation: bool -> TypeProviderForNamespaces

    /// <summary>Initializes a type provider.</summary>
    /// <param name="config"> Type provider config. </param>
    /// <param name="sourceAssemblies">
    ///    Optionally specify the design-time assemblies available to use as a basis for authoring provided types.
    ///    The transitive dependencies of these assemblies are also included. By default
    ///    Assembly.GetCallingAssembly() and its transitive dependencies are used.
    /// </param>
    ///               
    /// <param name="assemblyReplacementMap">
    ///    Optionally specify a map of assembly names from source model to referenced assemblies.
    /// </param>
    ///               
    /// <param name="addDefaultProbingLocation">
    ///    Optionally specify that the location of the type provider design-time component should be used to resolve failing assembly resolutions.
    ///    This flag or an equivalent call to RegisterProbingFolder is generally needed for any type provider design-time components loaded into .NET Core tooling.
    /// </param>
    new: config: TypeProviderConfig * ?sourceAssemblies: Assembly list * ?assemblyReplacementMap: (string * string) list * ?addDefaultProbingLocation: bool -> TypeProviderForNamespaces

    /// Invoked by the type provider to add a namespace of provided types in the specification of the type provider.
    member AddNamespace: namespaceName:string * types: ProvidedTypeDefinition list -> unit

    /// Invoked by the type provider to get all provided namespaces with their provided types.
    member Namespaces: IProvidedNamespace[]

    /// Invoked by the type provider to invalidate the information provided by the provider
    member Invalidate: unit -> unit

    /// Invoked by the host of the type provider to get the static parameters for a method.
    member GetStaticParametersForMethod: MethodBase -> ParameterInfo[]

    /// Invoked by the host of the type provider to apply the static argumetns for a method.
    member ApplyStaticArgumentsForMethod: MethodBase * string * obj[] -> MethodBase

#if !FX_NO_LOCAL_FILESYSTEM
    /// AssemblyResolve handler. Default implementation searches <assemblyname>.dll file in registered folders
    abstract ResolveAssembly: ResolveEventArgs -> Assembly
    default ResolveAssembly: ResolveEventArgs -> Assembly

    /// Registers custom probing path that can be used for probing assemblies
    member RegisterProbingFolder: folder: string -> unit

    /// Registers location of RuntimeAssembly (from TypeProviderConfig) as probing folder
    member RegisterRuntimeAssemblyLocationAsProbingFolder: config: TypeProviderConfig -> unit

#endif

#if !NO_GENERATIVE
    /// Register that a given file is a provided generated target assembly, e.g. an assembly produced by an external
    /// code generation tool.  This assembly should be a target assembly, i.e. use the same asssembly references
    /// as given by TargetContext.ReferencedAssemblyPaths
    member RegisterGeneratedTargetAssembly: fileName: string -> Assembly
#endif

    [<CLIEvent>]
    member Disposing: IEvent<EventHandler,EventArgs>

    /// The context for which code is eventually to be generated. You should not normally
    /// need to use this property directly, as translation from the compiler-hosted context to 
    /// the design-time context will normally be performed automatically.
    member TargetContext: ProvidedTypesContext

    interface ITypeProvider

module internal UncheckedQuotations =

    type Expr with
        static member NewDelegateUnchecked: ty:Type * vs:Var list * body:Expr -> Expr
        static member NewObjectUnchecked: cinfo:ConstructorInfo * args:Expr list -> Expr
        static member NewArrayUnchecked: elementType:Type * elements:Expr list -> Expr
        static member CallUnchecked: minfo:MethodInfo * args:Expr list -> Expr
        static member CallUnchecked: obj:Expr * minfo:MethodInfo * args:Expr list -> Expr
        static member ApplicationUnchecked: f:Expr * x:Expr -> Expr
        static member PropertyGetUnchecked: pinfo:PropertyInfo * args:Expr list -> Expr
        static member PropertyGetUnchecked: obj:Expr * pinfo:PropertyInfo * ?args:Expr list -> Expr
        static member PropertySetUnchecked: pinfo:PropertyInfo * value:Expr * ?args:Expr list -> Expr
        static member PropertySetUnchecked: obj:Expr * pinfo:PropertyInfo * value:Expr * ?args:Expr list -> Expr
        static member FieldGetUnchecked: pinfo:FieldInfo -> Expr
        static member FieldGetUnchecked: obj:Expr * pinfo:FieldInfo -> Expr
        static member FieldSetUnchecked: pinfo:FieldInfo * value:Expr -> Expr
        static member FieldSetUnchecked: obj:Expr * pinfo:FieldInfo * value:Expr -> Expr
        static member TupleGetUnchecked: e:Expr * n:int -> Expr
        static member LetUnchecked: v:Var * e:Expr * body:Expr -> Expr
        static member NewRecordUnchecked : ty:Type * args:Expr list -> Expr

    type Shape
        val ( |ShapeCombinationUnchecked|ShapeVarUnchecked|ShapeLambdaUnchecked| ): e:Expr -> Choice<(Shape * Expr list),Var, (Var * Expr)>
        val RebuildShapeCombinationUnchecked: Shape * args:Expr list -> Expr

module internal AssemblyReader =

    module Reader =

        type ILModuleReader = class end
    
        val GetWeakReaderCache : unit -> System.Collections.Concurrent.ConcurrentDictionary<(string * string), DateTime * WeakReference<ILModuleReader>>
        val GetStrongReaderCache : unit -> System.Collections.Concurrent.ConcurrentDictionary<(string * string), DateTime * int * ILModuleReader>
    