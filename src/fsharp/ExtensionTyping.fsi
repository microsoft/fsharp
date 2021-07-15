// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

// Extension typing, validation of extension types, etc.

namespace FSharp.Compiler

#if !NO_EXTENSIONTYPING

open System
open System.Collections.Generic
open System.Reflection
open FSharp.Core.CompilerServices
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.Text

module internal ExtensionTyping =

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

    and [<AllowNullLiteral; Class>]
        ProvidedType =
        new : x: System.Type * ctxt: ProvidedTypeContext -> ProvidedType
        inherit ProvidedMemberInfo
        abstract member IsSuppressRelocate : bool
        abstract member IsErased : bool
        abstract member IsGenericType : bool
        abstract member Namespace : string
        abstract member FullName : string
        abstract member IsArray : bool
        abstract member GetInterfaces : unit -> ProvidedType[]
        abstract member Assembly : ProvidedAssembly
        abstract member BaseType : ProvidedType
        abstract member GetNestedType : string -> ProvidedType
        abstract member GetNestedTypes : unit -> ProvidedType[]
        abstract member GetAllNestedTypes : unit -> ProvidedType[]
        abstract member GetMethods : unit -> ProvidedMethodInfo[]
        abstract member GetFields : unit -> ProvidedFieldInfo[]
        abstract member GetField : string -> ProvidedFieldInfo
        abstract member GetProperties : unit -> ProvidedPropertyInfo[]
        abstract member GetProperty : string -> ProvidedPropertyInfo
        abstract member GetEvents : unit -> ProvidedEventInfo[]
        abstract member GetEvent : string -> ProvidedEventInfo
        abstract member GetConstructors : unit -> ProvidedConstructorInfo[]
        member RawSystemType : System.Type
        abstract member GetStaticParameters : ITypeProvider -> ProvidedParameterInfo[]
        abstract member ApplyStaticArguments: ITypeProvider * string[] * obj[] -> ProvidedType
        abstract member GetGenericTypeDefinition : unit -> ProvidedType
        abstract member IsVoid : bool
        abstract member IsGenericParameter : bool
        abstract member IsValueType : bool
        abstract member IsByRef : bool
        abstract member IsPointer : bool
        abstract member IsEnum : bool
        abstract member IsInterface : bool
        abstract member IsClass : bool
        abstract member IsMeasure: bool
        abstract member IsSealed : bool
        abstract member IsAbstract : bool
        abstract member IsPublic : bool
        abstract member IsNestedPublic : bool
        abstract member GenericParameterPosition : int
        abstract member GetElementType : unit -> ProvidedType
        abstract member GetGenericArguments : unit -> ProvidedType[]
        abstract member GetArrayRank : unit -> int
        abstract member GetEnumUnderlyingType : unit -> ProvidedType
        abstract member MakePointerType: unit -> ProvidedType
        abstract member MakeByRefType: unit -> ProvidedType
        abstract member MakeArrayType: unit -> ProvidedType
        abstract member MakeArrayType: rank: int -> ProvidedType
        abstract member MakeGenericType: args: ProvidedType[] -> ProvidedType
        abstract member AsProvidedVar: nm: string -> ProvidedVar
        static member Void : ProvidedType
        static member CreateNoContext : Type -> ProvidedType
        member TryGetILTypeRef : unit -> ILTypeRef option
        member TryGetTyconRef : unit -> obj option
        abstract member ApplyContext : ProvidedTypeContext -> ProvidedType
        abstract member Context : ProvidedTypeContext
        static member TaintedEquals : Tainted<ProvidedType> * Tainted<ProvidedType> -> bool

    and [<AllowNullLiteral>]
        IProvidedCustomAttributeProvider =
        abstract GetCustomAttributes : provider: ITypeProvider -> seq<CustomAttributeData>
        abstract GetHasTypeProviderEditorHideMethodsAttribute : provider:ITypeProvider -> bool
        abstract GetDefinitionLocationAttribute : provider:ITypeProvider -> (string * int * int) option
        abstract GetXmlDocAttributes : provider:ITypeProvider -> string[]
        abstract GetAttributeConstructorArgs: provider:ITypeProvider * attribName:string -> (obj option list * (string * obj option) list) option

    and [<AllowNullLiteral; Class>]
        ProvidedAssembly =
        new: x: System.Reflection.Assembly -> ProvidedAssembly
        abstract member GetName : unit -> System.Reflection.AssemblyName
        abstract member FullName : string
        abstract member GetManifestModuleContents : ITypeProvider -> byte[]
        member Handle : System.Reflection.Assembly

    and [<AllowNullLiteral;AbstractClass>]
        ProvidedMemberInfo =
        abstract member Name :string
        abstract member DeclaringType : ProvidedType
        abstract GetCustomAttributes : provider: ITypeProvider -> seq<CustomAttributeData>
        abstract GetHasTypeProviderEditorHideMethodsAttribute : provider:ITypeProvider -> bool
        abstract GetDefinitionLocationAttribute : provider:ITypeProvider -> (string * int * int) option
        abstract GetXmlDocAttributes : provider:ITypeProvider -> string[]
        abstract GetAttributeConstructorArgs: provider:ITypeProvider * attribName:string -> (obj option list * (string * obj option) list) option
        interface IProvidedCustomAttributeProvider

    and [<AllowNullLiteral;AbstractClass>]
        ProvidedMethodBase =
        inherit ProvidedMemberInfo
        member Context : ProvidedTypeContext
        abstract member IsGenericMethod : bool
        abstract member IsStatic : bool
        abstract member IsFamily : bool
        abstract member IsFamilyAndAssembly : bool
        abstract member IsFamilyOrAssembly : bool
        abstract member IsVirtual : bool
        abstract member IsFinal : bool
        abstract member IsPublic : bool
        abstract member IsAbstract : bool
        abstract member IsHideBySig : bool
        abstract member IsConstructor : bool
        abstract member GetParameters : unit -> ProvidedParameterInfo[]
        abstract member GetGenericArguments : unit -> ProvidedType[]
        abstract member GetStaticParametersForMethod : ITypeProvider -> ProvidedParameterInfo[]
        abstract member ApplyStaticArgumentsForMethod : provider: ITypeProvider * fullNameAfterArguments: string * staticArgs: obj[] -> ProvidedMethodBase
        static member TaintedGetHashCode : Tainted<ProvidedMethodBase> -> int
        static member TaintedEquals : Tainted<ProvidedMethodBase> * Tainted<ProvidedMethodBase> -> bool

    and [<AllowNullLiteral; Class>]
        ProvidedMethodInfo =
        new: x: System.Reflection.MethodInfo * ctxt: ProvidedTypeContext -> ProvidedMethodInfo
        inherit ProvidedMethodBase
        member Handle: System.Reflection.MethodInfo
        abstract member ReturnType : ProvidedType
        abstract member MetadataToken : int

    and [<AllowNullLiteral; Class>]
        ProvidedParameterInfo =
        new: x: System.Reflection.ParameterInfo * ctxt: ProvidedTypeContext -> ProvidedParameterInfo
        abstract member Name :string
        abstract member ParameterType : ProvidedType
        abstract member IsIn : bool
        abstract member IsOut : bool
        abstract member IsOptional : bool
        abstract member RawDefaultValue : obj
        abstract member HasDefaultValue : bool
        abstract GetCustomAttributes : provider: ITypeProvider -> seq<CustomAttributeData>
        abstract GetHasTypeProviderEditorHideMethodsAttribute : provider:ITypeProvider -> bool
        abstract GetDefinitionLocationAttribute : provider:ITypeProvider -> (string * int * int) option
        abstract GetXmlDocAttributes : provider:ITypeProvider -> string[]
        abstract GetAttributeConstructorArgs: provider:ITypeProvider * attribName:string -> (obj option list * (string * obj option) list) option
        interface IProvidedCustomAttributeProvider

    and [<AllowNullLiteral; Class>]
        ProvidedFieldInfo =
        inherit ProvidedMemberInfo
        new: x: System.Reflection.FieldInfo * ctxt: ProvidedTypeContext -> ProvidedFieldInfo
        abstract member IsInitOnly : bool
        abstract member IsStatic : bool
        abstract member IsSpecialName : bool
        abstract member IsLiteral : bool
        abstract member GetRawConstantValue : unit -> obj
        abstract member FieldType : ProvidedType
        abstract member IsPublic : bool
        abstract member IsFamily : bool
        abstract member IsFamilyAndAssembly : bool
        abstract member IsFamilyOrAssembly : bool
        abstract member IsPrivate : bool
        static member TaintedEquals : Tainted<ProvidedFieldInfo> * Tainted<ProvidedFieldInfo> -> bool

    and [<AllowNullLiteral; Class>]
        ProvidedPropertyInfo =
        new: x: System.Reflection.PropertyInfo * ctxt: ProvidedTypeContext -> ProvidedPropertyInfo
        inherit ProvidedMemberInfo
        abstract member GetGetMethod : unit -> ProvidedMethodInfo
        abstract member GetSetMethod : unit -> ProvidedMethodInfo
        abstract member GetIndexParameters : unit -> ProvidedParameterInfo[]
        abstract member CanRead : bool
        abstract member CanWrite : bool
        abstract member PropertyType : ProvidedType
        static member TaintedGetHashCode : Tainted<ProvidedPropertyInfo> -> int
        static member TaintedEquals : Tainted<ProvidedPropertyInfo> * Tainted<ProvidedPropertyInfo> -> bool

    and [<AllowNullLiteral; Class>]
        ProvidedEventInfo =
        inherit ProvidedMemberInfo
        new: x: System.Reflection.EventInfo * ctxt: ProvidedTypeContext -> ProvidedEventInfo
        abstract member GetAddMethod : unit -> ProvidedMethodInfo
        abstract member GetRemoveMethod : unit -> ProvidedMethodInfo
        abstract member EventHandlerType : ProvidedType
        static member TaintedGetHashCode : Tainted<ProvidedEventInfo> -> int
        static member TaintedEquals : Tainted<ProvidedEventInfo> * Tainted<ProvidedEventInfo> -> bool

    and [<AllowNullLiteral; Class>]
        ProvidedConstructorInfo =
        new: x: System.Reflection.ConstructorInfo * ctxt: ProvidedTypeContext -> ProvidedConstructorInfo
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


    and [<RequireQualifiedAccess; Class; AllowNullLiteral>]
        ProvidedExpr =
        new: x: Quotations.Expr * ctxt: ProvidedTypeContext -> ProvidedExpr
        abstract member Type : ProvidedType
        /// Convert the expression to a string for diagnostics
        abstract member UnderlyingExpressionString : string
        abstract member GetExprType : unit -> ProvidedExprType option
        member Handle: Quotations.Expr

    and [<RequireQualifiedAccess; Class; AllowNullLiteral>]
        ProvidedVar =
        new: x: Quotations.Var * ctxt: ProvidedTypeContext -> ProvidedVar
        abstract member Type : ProvidedType
        abstract member Name : string
        abstract member IsMutable : bool
        override Equals : obj -> bool
        override GetHashCode : unit -> int

    /// Get the provided expression for a particular use of a method.
    val GetInvokerExpression : ITypeProvider * ProvidedMethodBase * ProvidedVar[] ->  ProvidedExpr

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

#endif
