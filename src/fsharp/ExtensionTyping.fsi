// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

// Extension typing, validation of extension types, etc.

namespace FSharp.Compiler

#if !NO_EXTENSIONTYPING

open System
open System.Collections.Generic
open FSharp.Core.CompilerServices
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.Text

module ExtensionTyping =

    type TypeProviderDesignation = TypeProviderDesignation of string

    /// Raised when a type provider has thrown an exception.    
    exception ProvidedTypeResolution of range * exn

    /// Raised when an type provider has thrown an exception.    
    exception ProvidedTypeResolutionNoRange of exn

    /// Get the list of relative paths searched for type provider design-time components
    val toolingCompatiblePaths: unit -> string list

    /// Carries information about the type provider resolution environment.
    type ResolutionEnvironment =
      {
        /// The folder from which an extension provider is resolving from. This is typically the project folder.
        resolutionFolder            : string
        /// Output file name
        outputFile                  : string option
        /// Whether or not the --showextensionresolution flag was supplied to the compiler.
        showResolutionMessages      : bool
        
        /// All referenced assemblies, including the type provider itself, and possibly other type providers.
        referencedAssemblies        : string[]

        /// The folder for temporary files
        temporaryFolder             : string
      }

    /// Find and instantiate the set of ITypeProvider components for the given assembly reference
    val GetTypeProvidersOfAssembly : 
          runtimeAssemblyFilename: string  *
          ilScopeRefOfRuntimeAssembly:ILScopeRef *
          designTimeName: string *
          resolutionEnvironment: ResolutionEnvironment *
          isInvalidationSupported: bool *
          isInteractive: bool *
          systemRuntimeContainsType : (string -> bool) *
          systemRuntimeAssemblyVersion : System.Version *
          compilerToolPaths : string list * 
          range -> Tainted<ITypeProvider> list

    /// Given an extension type resolver, supply a human-readable name suitable for error messages.
    val DisplayNameOfTypeProvider : Tainted<Microsoft.FSharp.Core.CompilerServices.ITypeProvider> * range -> string

     /// The context used to interpret information in the closure of System.Type, System.MethodInfo and other 
     /// info objects coming from the type provider.
     ///
     /// At the moment this is the "Type --> ILTypeRef" and "Type --> Tycon" remapping 
     /// context for generated types (it is empty for erased types). This is computed from
     /// while processing the [<Generate>] declaration related to the type.
     ///
     /// Immutable (after type generation for a [<Generate>] declaration populates the dictionaries).
     ///
     /// The 'obj' values are all TyconRef, but obj is used due to a forward reference being required. Not particularly
     /// pleasant, but better than intertwining the whole "ProvidedType" with the TAST structure.
    [<Sealed>]
    type ProvidedTypeContext =

        member TryGetILTypeRef : ProvidedType -> ILTypeRef option

        member TryGetTyconRef : ProvidedType -> obj option

        static member Empty : ProvidedTypeContext 

        static member Create : Dictionary<ProvidedType, ILTypeRef> * Dictionary<ProvidedType, obj (* TyconRef *) > -> ProvidedTypeContext 

        member GetDictionaries : unit -> Dictionary<ProvidedType, ILTypeRef> * Dictionary<ProvidedType, obj (* TyconRef *) > 

        /// Map the TyconRef objects, if any
        member RemapTyconRefs : (obj -> obj) -> ProvidedTypeContext 

    and [<AllowNullLiteral; Sealed; Class>] 
        ProvidedType =
        inherit ProvidedMemberInfo
        member IsSuppressRelocate : bool
        member IsErased : bool
        member IsGenericType : bool
        member Namespace : string
        member FullName : string
        member IsArray : bool
        member GetInterfaces : unit -> ProvidedType[]
        member Assembly : ProvidedAssembly
        member BaseType : ProvidedType
        member GetNestedType : string -> ProvidedType
        member GetNestedTypes : unit -> ProvidedType[]
        member GetAllNestedTypes : unit -> ProvidedType[]
        member GetMethods : unit -> ProvidedMethodInfo[]
        member GetFields : unit -> ProvidedFieldInfo[]
        member GetField : string -> ProvidedFieldInfo
        member GetProperties : unit -> ProvidedPropertyInfo[]
        member GetProperty : string -> ProvidedPropertyInfo
        member GetEvents : unit -> ProvidedEventInfo[]
        member GetEvent : string -> ProvidedEventInfo
        member GetConstructors : unit -> ProvidedConstructorInfo[]
        member GetStaticParameters : ITypeProvider -> ProvidedParameterInfo[]
        member GetGenericTypeDefinition : unit -> ProvidedType
        member IsVoid : bool
        member IsGenericParameter : bool
        member IsValueType : bool
        member IsByRef : bool
        member IsPointer : bool
        member IsEnum : bool
        member IsInterface : bool
        member IsClass : bool
        member IsMeasure: bool
        member IsSealed : bool
        member IsAbstract : bool
        member IsPublic : bool
        member IsNestedPublic : bool
        member GenericParameterPosition : int
        member GetElementType : unit -> ProvidedType
        member GetGenericArguments : unit -> ProvidedType[]
        member GetArrayRank : unit -> int
        member RawSystemType : System.Type
        member GetEnumUnderlyingType : unit -> ProvidedType
        member MakePointerType: unit -> ProvidedType
        member MakeByRefType: unit -> ProvidedType
        member MakeArrayType: unit -> ProvidedType
        member MakeArrayType: rank: int -> ProvidedType
        member MakeGenericType: args: ProvidedType[] -> ProvidedType
        member AsProvidedVar : name: string -> ProvidedVar
        static member Void : ProvidedType
        static member CreateNoContext : Type -> ProvidedType
        member TryGetILTypeRef : unit -> ILTypeRef option
        member TryGetTyconRef : unit -> obj option
        static member ApplyContext : ProvidedType * ProvidedTypeContext -> ProvidedType
        member Context : ProvidedTypeContext 
        interface IProvidedCustomAttributeProvider
        static member TaintedEquals : Tainted<ProvidedType> * Tainted<ProvidedType> -> bool 

    and [<AllowNullLiteral>] 
        IProvidedCustomAttributeProvider =
        abstract GetHasTypeProviderEditorHideMethodsAttribute : provider:ITypeProvider -> bool
        abstract GetDefinitionLocationAttribute : provider:ITypeProvider -> (string * int * int) option 
        abstract GetXmlDocAttributes : provider:ITypeProvider -> string[]
        abstract GetAttributeConstructorArgs: provider:ITypeProvider * attribName:string -> (obj option list * (string * obj option) list) option
        
    and [<AllowNullLiteral; Sealed; Class>] 
        ProvidedAssembly = 
        member GetName : unit -> System.Reflection.AssemblyName
        member FullName : string
        member GetManifestModuleContents : ITypeProvider -> byte[]
        member Handle : System.Reflection.Assembly

    and [<AllowNullLiteral;AbstractClass>] 
        ProvidedMemberInfo = 
        member Name :string
        member DeclaringType : ProvidedType
        interface IProvidedCustomAttributeProvider 

    and [<AllowNullLiteral;AbstractClass>] 
        ProvidedMethodBase = 
        inherit ProvidedMemberInfo
        member IsGenericMethod : bool
        member IsStatic : bool
        member IsFamily : bool
        member IsFamilyAndAssembly : bool
        member IsFamilyOrAssembly : bool
        member IsVirtual : bool
        member IsFinal : bool
        member IsPublic : bool
        member IsAbstract : bool
        member IsHideBySig : bool
        member IsConstructor : bool
        member GetParameters : unit -> ProvidedParameterInfo[]
        member GetGenericArguments : unit -> ProvidedType[]
        member GetStaticParametersForMethod : ITypeProvider -> ProvidedParameterInfo[]
        static member TaintedGetHashCode : Tainted<ProvidedMethodBase> -> int
        static member TaintedEquals : Tainted<ProvidedMethodBase> * Tainted<ProvidedMethodBase> -> bool 

    and [<AllowNullLiteral; Sealed; Class>] 
        ProvidedMethodInfo = 
        inherit ProvidedMethodBase
        member ReturnType : ProvidedType
        member MetadataToken : int

    and [<AllowNullLiteral; Sealed; Class>] 
        ProvidedParameterInfo = 
        member Name :string
        member ParameterType : ProvidedType
        member IsIn : bool
        member IsOut : bool
        member IsOptional : bool
        member RawDefaultValue : obj
        member HasDefaultValue : bool
        interface IProvidedCustomAttributeProvider 

    and [<AllowNullLiteral; Class; Sealed>] 
        ProvidedFieldInfo = 
        inherit ProvidedMemberInfo
        member IsInitOnly : bool
        member IsStatic : bool
        member IsSpecialName : bool
        member IsLiteral : bool
        member GetRawConstantValue : unit -> obj
        member FieldType : ProvidedType
        member IsPublic : bool
        member IsFamily : bool
        member IsFamilyAndAssembly : bool
        member IsFamilyOrAssembly : bool
        member IsPrivate : bool
        static member TaintedEquals : Tainted<ProvidedFieldInfo> * Tainted<ProvidedFieldInfo> -> bool 

    and [<AllowNullLiteral; Class; Sealed>] 
        ProvidedPropertyInfo = 
        inherit ProvidedMemberInfo
        member GetGetMethod : unit -> ProvidedMethodInfo
        member GetSetMethod : unit -> ProvidedMethodInfo
        member GetIndexParameters : unit -> ProvidedParameterInfo[]
        member CanRead : bool
        member CanWrite : bool
        member PropertyType : ProvidedType
        static member TaintedGetHashCode : Tainted<ProvidedPropertyInfo> -> int
        static member TaintedEquals : Tainted<ProvidedPropertyInfo> * Tainted<ProvidedPropertyInfo> -> bool 

    and [<AllowNullLiteral; Class; Sealed>] 
        ProvidedEventInfo = 
        inherit ProvidedMemberInfo
        member GetAddMethod : unit -> ProvidedMethodInfo
        member GetRemoveMethod : unit -> ProvidedMethodInfo
        member EventHandlerType : ProvidedType
        static member TaintedGetHashCode : Tainted<ProvidedEventInfo> -> int
        static member TaintedEquals : Tainted<ProvidedEventInfo> * Tainted<ProvidedEventInfo> -> bool 

    and [<AllowNullLiteral; Class; Sealed>] 
        ProvidedConstructorInfo = 
        inherit ProvidedMethodBase
      
    and ProvidedExprType =
        | ProvidedNewArrayExpr of ProvidedType * ProvidedExpr[]
#if PROVIDED_ADDRESS_OF
        | ProvidedAddressOfExpr of ProvidedExpr
#endif
        | ProvidedNewObjectExpr of ProvidedConstructorInfo * ProvidedExpr[]
        | ProvidedWhileLoopExpr of ProvidedExpr * ProvidedExpr
        | ProvidedNewDelegateExpr of ProvidedType * ProvidedVar[] * ProvidedExpr
        | ProvidedForIntegerRangeLoopExpr of ProvidedVar * ProvidedExpr * ProvidedExpr * ProvidedExpr
        | ProvidedSequentialExpr of ProvidedExpr * ProvidedExpr
        | ProvidedTryWithExpr of ProvidedExpr * ProvidedVar * ProvidedExpr * ProvidedVar * ProvidedExpr
        | ProvidedTryFinallyExpr of ProvidedExpr * ProvidedExpr
        | ProvidedLambdaExpr of ProvidedVar * ProvidedExpr
        | ProvidedCallExpr of ProvidedExpr option * ProvidedMethodInfo * ProvidedExpr[]
        | ProvidedConstantExpr of obj * ProvidedType
        | ProvidedDefaultExpr of ProvidedType
        | ProvidedNewTupleExpr of ProvidedExpr[]
        | ProvidedTupleGetExpr of ProvidedExpr * int
        | ProvidedTypeAsExpr of ProvidedExpr * ProvidedType
        | ProvidedTypeTestExpr of ProvidedExpr * ProvidedType
        | ProvidedLetExpr of ProvidedVar * ProvidedExpr * ProvidedExpr
        | ProvidedVarSetExpr of ProvidedVar * ProvidedExpr
        | ProvidedIfThenElseExpr of ProvidedExpr * ProvidedExpr * ProvidedExpr
        | ProvidedVarExpr of ProvidedVar
        
    and [<RequireQualifiedAccess; Class; Sealed; AllowNullLiteral>]
        ProvidedExpr =
        member Type : ProvidedType
        /// Convert the expression to a string for diagnostics
        member UnderlyingExpressionString : string
        member GetExprType : unit -> ProvidedExprType option

    and [<RequireQualifiedAccess; Class; Sealed; AllowNullLiteral>]
        ProvidedVar =
        member Type : ProvidedType
        member Name : string
        member IsMutable : bool
        override Equals : obj -> bool
        override GetHashCode : unit -> int

    /// Get the provided expression for a particular use of a method.
    val GetInvokerExpression : ITypeProvider * ProvidedMethodBase * ProvidedVar[] ->  ProvidedExpr

    /// Get all provided types from provided namespace
    val GetProvidedTypes: pn: IProvidedNamespace -> ProvidedType[]

    /// Validate that the given provided type meets some of the rules for F# provided types
    val ValidateProvidedTypeAfterStaticInstantiation : range * Tainted<ProvidedType> * expectedPath : string[] * expectedName : string-> unit

    /// Try to apply a provided type to the given static arguments. If successful also return a function 
    /// to check the type name is as expected (this function is called by the caller of TryApplyProvidedType
    /// after other checks are made).
    val TryApplyProvidedType : typeBeforeArguments:Tainted<ProvidedType> * optGeneratedTypePath: string list option * staticArgs:obj[]  * range -> (Tainted<ProvidedType> * (unit -> unit)) option

    /// Try to apply a provided method to the given static arguments. 
    val TryApplyProvidedMethod : methBeforeArgs:Tainted<ProvidedMethodBase> * staticArgs:obj[]  * range -> Tainted<ProvidedMethodBase> option

    /// Try to resolve a type in the given extension type resolver
    val TryResolveProvidedType : Tainted<ITypeProvider> * range * string[] * typeName: string -> Tainted<ProvidedType> option

    /// Try to resolve a type in the given extension type resolver
    val TryLinkProvidedType : Tainted<ITypeProvider> * string[] * typeLogicalName: string * range: range -> Tainted<ProvidedType> option

    /// Get the parts of a .NET namespace. Special rules: null means global, empty is not allowed.
    val GetProvidedNamespaceAsPath : range * Tainted<ITypeProvider> * string -> string list

    /// Decompose the enclosing name of a type (including any class nestings) into a list of parts.
    /// e.g. System.Object -> ["System"; "Object"]
    val GetFSharpPathToProvidedType : Tainted<ProvidedType> * range:range-> string list
    
    /// Get the ILTypeRef for the provided type (including for nested types). Take into account
    /// any type relocations or static linking for generated types.
    val GetILTypeRefOfProvidedType : Tainted<ProvidedType> * range:range -> FSharp.Compiler.AbstractIL.IL.ILTypeRef

    /// Get the ILTypeRef for the provided type (including for nested types). Do not take into account
    /// any type relocations or static linking for generated types.
    val GetOriginalILTypeRefOfProvidedType : Tainted<ProvidedType> * range:range -> FSharp.Compiler.AbstractIL.IL.ILTypeRef


    /// Represents the remapping information for a generated provided type and its nested types.
    ///
    /// There is one overall tree for each root 'type X = ... type generation expr...' specification.
    type ProviderGeneratedType = ProviderGeneratedType of (*ilOrigTyRef*)ILTypeRef * (*ilRenamedTyRef*)ILTypeRef * ProviderGeneratedType list

    /// The table of information recording remappings from type names in the provided assembly to type
    /// names in the statically linked, embedded assembly, plus what types are nested in side what types.
    type ProvidedAssemblyStaticLinkingMap = 
        {  /// The table of remappings from type names in the provided assembly to type
           /// names in the statically linked, embedded assembly.
           ILTypeMap: System.Collections.Generic.Dictionary<ILTypeRef, ILTypeRef> }
        
        /// Create a new static linking map, ready to populate with data.
        static member CreateNew : unit -> ProvidedAssemblyStaticLinkingMap

    /// Check if this is a direct reference to a non-embedded generated type. This is not permitted at any name resolution.
    /// We check by seeing if the type is absent from the remapping context.
    val IsGeneratedTypeDirectReference         : Tainted<ProvidedType> * range -> bool

    [<AutoOpen>]
    module Shim =

        type IExtensionTypingProvider =

             /// Find and instantiate the set of ITypeProvider components for the given assembly reference
            abstract InstantiateTypeProvidersOfAssembly :
              runtimeAssemblyFilename: string
              * designerAssemblyName: string
              * ResolutionEnvironment
              * bool
              * isInteractive: bool
              * systemRuntimeContainsType : (string -> bool)
              * systemRuntimeAssemblyVersion : System.Version
              * compilerToolsPath : string list
              * m: range -> ITypeProvider list

            abstract GetProvidedTypes: pn: IProvidedNamespace -> ProvidedType[]
            abstract ResolveTypeName: pn: IProvidedNamespace * typeName: string -> ProvidedType
            abstract GetInvokerExpression: provider: ITypeProvider * methodBase: ProvidedMethodBase * paramExprs: ProvidedVar[] -> ProvidedExpr
            abstract DisplayNameOfTypeProvider: typeProvider: ITypeProvider * fullName: bool -> string

        [<Sealed>]
        type DefaultExtensionTypingProvider =
            interface IExtensionTypingProvider

        val mutable ExtensionTypingProvider: IExtensionTypingProvider

#endif
