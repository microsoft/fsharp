// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// <summary>Pervasives: Additional bindings available at the top level</summary>
namespace Microsoft.FSharp.Core

[<AutoOpen>]
/// <summary>A set of extra operators and functions. This module is automatically opened in all F# code.</summary>
///
/// <category>Basic Operators</category>
module ExtraTopLevelOperators = 

    open System
    open Microsoft.FSharp.Core
    open Microsoft.FSharp.Control
    open Microsoft.FSharp.Collections
    open Microsoft.FSharp.Text

    /// <summary>Print to <c>stdout</c> using the given format.</summary>
    ///
    /// <param name="format">The formatter.</param>
    ///
    /// <returns>The formatted result.</returns>
    [<CompiledName("PrintFormat")>]
    val printf: format:Printf.TextWriterFormat<'T> -> 'T

    /// <summary>Print to <c>stdout</c> using the given format, and add a newline.</summary>
    ///
    /// <param name="format">The formatter.</param>
    ///
    /// <returns>The formatted result.</returns>
    [<CompiledName("PrintFormatLine")>]
    val printfn: format:Printf.TextWriterFormat<'T> -> 'T

    /// <summary>Print to <c>stderr</c> using the given format.</summary>
    ///
    /// <param name="format">The formatter.</param>
    ///
    /// <returns>The formatted result.</returns>
    [<CompiledName("PrintFormatToError")>]
    val eprintf: format:Printf.TextWriterFormat<'T> -> 'T

    /// <summary>Print to <c>stderr</c> using the given format, and add a newline.</summary>
    ///
    /// <param name="format">The formatter.</param>
    ///
    /// <returns>The formatted result.</returns>
    [<CompiledName("PrintFormatLineToError")>]
    val eprintfn: format:Printf.TextWriterFormat<'T> -> 'T

    /// <summary>Print to a string using the given format.</summary>
    ///
    /// <param name="format">The formatter.</param>
    ///
    /// <returns>The formatted result.</returns>
    [<CompiledName("PrintFormatToString")>]
    val sprintf: format:Printf.StringFormat<'T> -> 'T

    /// <summary>Print to a string buffer and raise an exception with the given
    /// result. Helper printers must return strings.</summary>
    ///
    /// <param name="format">The formatter.</param>
    ///
    /// <returns>The formatted result.</returns>
    [<CompiledName("PrintFormatToStringThenFail")>]
    val failwithf: format:Printf.StringFormat<'T,'Result> -> 'T

    /// <summary>Print to a file using the given format.</summary>
    ///
    /// <param name="textWriter">The file TextWriter.</param>
    /// <param name="format">The formatter.</param>
    ///
    /// <returns>The formatted result.</returns>
    [<CompiledName("PrintFormatToTextWriter")>]
    val fprintf : textWriter:System.IO.TextWriter -> format:Printf.TextWriterFormat<'T> -> 'T

    /// <summary>Print to a file using the given format, and add a newline.</summary>
    ///
    /// <param name="textWriter">The file TextWriter.</param>
    /// <param name="format">The formatter.</param>
    ///
    /// <returns>The formatted result.</returns>
    [<CompiledName("PrintFormatLineToTextWriter")>]
    val fprintfn : textWriter:System.IO.TextWriter -> format:Printf.TextWriterFormat<'T> -> 'T

    /// <summary>Builds a set from a sequence of objects. The objects are indexed using generic comparison.</summary>
    ///
    /// <param name="elements">The input sequence of elements.</param>
    ///
    /// <returns>The created set.</returns>
    [<CompiledName("CreateSet")>]
    val set : elements:seq<'T> -> Set<'T>

    /// <summary>Builds an asynchronous workflow using computation expression syntax.</summary>
    [<CompiledName("DefaultAsyncBuilder")>]
    val async : Microsoft.FSharp.Control.AsyncBuilder  

    /// <summary>Converts the argument to 32-bit float.</summary>
    /// <remarks>This is a direct conversion for all 
    /// primitive numeric types. For strings, the input is converted using <c>Single.Parse()</c>  with InvariantCulture settings. Otherwise the operation requires and invokes a <c>ToSingle</c> method on the input type.</remarks>
    [<CompiledName("ToSingle")>]
    val inline single     : value:^T -> single     when ^T : (static member op_Explicit : ^T -> single)     and default ^T : int

    /// <summary>Converts the argument to 64-bit float.</summary>
    /// <remarks>This is a direct conversion for all 
    /// primitive numeric types. For strings, the input is converted using <c>Double.Parse()</c>  with InvariantCulture settings. Otherwise the operation requires and invokes a <c>ToDouble</c> method on the input type.</remarks>
    [<CompiledName("ToDouble")>]
    val inline double     : value:^T -> double      when ^T : (static member op_Explicit : ^T -> double)     and default ^T : int

    /// <summary>Converts the argument to byte.</summary>
    /// <remarks>This is a direct conversion for all 
    /// primitive numeric types. For strings, the input is converted using <c>Byte.Parse()</c> on strings and otherwise requires a <c>ToByte</c> method on the input type.</remarks>
    [<CompiledName("ToByte")>]
    val inline uint8       : value:^T -> uint8       when ^T : (static member op_Explicit : ^T -> uint8)       and default ^T : int        
    
    /// <summary>Converts the argument to signed byte.</summary>
    /// <remarks>This is a direct conversion for all 
    /// primitive numeric types. For strings, the input is converted using <c>SByte.Parse()</c>  with InvariantCulture settings.
    /// Otherwise the operation requires and invokes a <c>ToSByte</c> method on the input type.</remarks>
    [<CompiledName("ToSByte")>]
    val inline int8      : value:^T -> int8      when ^T : (static member op_Explicit : ^T -> int8)      and default ^T : int
    

    module Checked = 

        /// <summary>Converts the argument to byte.</summary>
        /// <remarks>This is a direct, checked conversion for all 
        /// primitive numeric types. For strings, the input is converted using <c>Byte.Parse()</c> on strings and otherwise requires a <c>ToByte</c> method on the input type.</remarks>
        [<CompiledName("ToByte")>]
        val inline uint8       : value:^T -> byte       when ^T : (static member op_Explicit : ^T -> uint8)       and default ^T : int        
    
        /// <summary>Converts the argument to signed byte.</summary>
        /// <remarks>This is a direct, checked conversion for all 
        /// primitive numeric types. For strings, the input is converted using <c>SByte.Parse()</c>  with InvariantCulture settings.
        /// Otherwise the operation requires and invokes a <c>ToSByte</c> method on the input type.</remarks>
        [<CompiledName("ToSByte")>]
        val inline int8      : value:^T -> sbyte      when ^T : (static member op_Explicit : ^T -> int8)      and default ^T : int
    

    /// <summary>Builds a read-only lookup table from a sequence of key/value pairs. The key objects are indexed using generic hashing and equality.</summary>
    [<CompiledName("CreateDictionary")>]
    val dict : keyValuePairs:seq<'Key * 'Value> -> System.Collections.Generic.IDictionary<'Key,'Value> when 'Key : equality

    /// <summary>Builds a read-only lookup table from a sequence of key/value pairs. The key objects are indexed using generic hashing and equality.</summary>
    [<CompiledName("CreateReadOnlyDictionary")>]
    val readOnlyDict : keyValuePairs:seq<'Key * 'Value> -> System.Collections.Generic.IReadOnlyDictionary<'Key,'Value> when 'Key : equality

    /// <summary>Builds a 2D array from a sequence of sequences of elements.</summary>
    [<CompiledName("CreateArray2D")>]
    val array2D : rows:seq<#seq<'T>> -> 'T[,]

    /// <summary>Special prefix operator for splicing typed expressions into quotation holes.</summary>
    [<CompiledName("SpliceExpression")>]
    val (~%) : expression:Microsoft.FSharp.Quotations.Expr<'T> -> 'T

    /// <summary>Special prefix operator for splicing untyped expressions into quotation holes.</summary>
    [<CompiledName("SpliceUntypedExpression")>]
    val (~%%) : expression:Microsoft.FSharp.Quotations.Expr -> 'T

    /// <summary>An active pattern to force the execution of values of type <c>Lazy&lt;_&gt;</c>.</summary>
    [<CompiledName("LazyPattern")>]
    val (|Lazy|) : input:Lazy<'T> -> 'T

    /// <summary>Builds a query using query syntax and operators.</summary>
    val query : Microsoft.FSharp.Linq.QueryBuilder


namespace Microsoft.FSharp.Core.CompilerServices

    open System
    open System.Reflection
    open System.Linq.Expressions
    open System.Collections.Generic
    open Microsoft.FSharp.Core
    open Microsoft.FSharp.Control
    open Microsoft.FSharp.Quotations


    /// <summary>Represents the product of two measure expressions when returned as a generic argument of a provided type.</summary>
    ///
    /// <namespacedoc><summary>
    ///   Library functionality for supporting type providers and code generated by the F# compiler. See
    ///   also <a href="https://docs.microsoft.com/en-us/dotnet/fsharp/tutorials/type-providers/">F# Type Providers</a> in the F# Language Guide.
    /// </summary></namespacedoc>
    type MeasureProduct<'Measure1, 'Measure2> 

    /// <summary>Represents the inverse of a measure expressions when returned as a generic argument of a provided type.</summary>
    type MeasureInverse<'Measure> 

    /// <summary>Represents the '1' measure expression when returned as a generic argument of a provided type.</summary>
    type MeasureOne

    /// <summary>Place on a class that implements ITypeProvider to extend the compiler</summary>
    [<AttributeUsageAttribute(AttributeTargets.Class, AllowMultiple = false)>]
    type TypeProviderAttribute =
        inherit System.Attribute
        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>TypeProviderAttribute</returns>
        new : unit -> TypeProviderAttribute

    /// <summary>Additional type attribute flags related to provided types</summary>
    type TypeProviderTypeAttributes =
        | SuppressRelocate = 0x80000000
        | IsErased = 0x40000000

    /// <summary>Place attribute on runtime assembly to indicate that there is a corresponding design-time 
    /// assembly that contains a type provider. Runtime and designer assembly may be the same. </summary>
    [<AttributeUsageAttribute(AttributeTargets.Assembly, AllowMultiple = false)>]
    type TypeProviderAssemblyAttribute = 
        inherit System.Attribute
        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>TypeProviderAssemblyAttribute</returns>
        new : unit -> TypeProviderAssemblyAttribute
        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>TypeProviderAssemblyAttribute</returns>
        /// <param name="assemblyName">The name of the design-time assembly for this type provider.</param>
        new : assemblyName : string -> TypeProviderAssemblyAttribute
        member AssemblyName : string

    /// The TypeProviderXmlDocAttribute attribute can be added to types and members. 
    /// The language service will display the CommentText property from the attribute 
    /// in the appropriate place when the user hovers over a type or member.
    [<AttributeUsageAttribute(AttributeTargets.All, AllowMultiple = false)>]
    type TypeProviderXmlDocAttribute = 
        inherit System.Attribute
        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>TypeProviderXmlDocAttribute</returns>
        new : commentText : string -> TypeProviderXmlDocAttribute
        member CommentText : string

    [<AttributeUsageAttribute(AttributeTargets.All, AllowMultiple = false)>]
    type TypeProviderDefinitionLocationAttribute = 
        inherit System.Attribute
        new : unit -> TypeProviderDefinitionLocationAttribute
        member FilePath : string with get, set
        member Line : int with get, set
        member Column : int with get, set

    [<AttributeUsageAttribute(AttributeTargets.Class ||| AttributeTargets.Interface ||| AttributeTargets.Struct ||| AttributeTargets.Delegate, AllowMultiple = false)>]
    /// <summary>Indicates that a code editor should hide all System.Object methods from the intellisense menus for instances of a provided type</summary>
    type TypeProviderEditorHideMethodsAttribute = 
        inherit System.Attribute
        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>TypeProviderEditorHideMethodsAttribute</returns>
        new : unit -> TypeProviderEditorHideMethodsAttribute

    /// If the class that implements ITypeProvider has a constructor that accepts TypeProviderConfig
    /// then it will be constructed with an instance of TypeProviderConfig.
    type TypeProviderConfig =
        new :  systemRuntimeContainsType : (string -> bool) -> TypeProviderConfig

        /// Get the full path to use to resolve relative paths in any file name arguments given to the type provider instance.
        member ResolutionFolder : string with get,set

        /// Get the full path to referenced assembly that caused this type provider instance to be created.
        member RuntimeAssembly : string with get,set

        /// Get the referenced assemblies for the type provider instance.
        member ReferencedAssemblies : string[] with get,set

        /// Get the full path to use for temporary files for the type provider instance.
        member TemporaryFolder : string with get,set

        /// Indicates if the type provider host responds to invalidation events for type provider instances. 
        member IsInvalidationSupported : bool with get,set

        /// Indicates if the type provider instance is used in an environment which executes provided code such as F# Interactive.
        member IsHostedExecution : bool with get,set

        /// version of referenced system runtime assembly
        member SystemRuntimeAssemblyVersion : System.Version with get,set

        /// Checks if given type exists in target system runtime library
        member SystemRuntimeContainsType : string -> bool


    /// <summary>
    /// Represents a namespace provided by a type provider component.
    /// </summary>
    type IProvidedNamespace =
        /// Namespace name the provider injects types into.
        abstract NamespaceName : string

        /// The sub-namespaces in this namespace. An optional member to prevent generation of namespaces until an outer namespace is explored.
        abstract GetNestedNamespaces : unit -> IProvidedNamespace[] 

        /// <summary>
        /// The top-level types
        /// </summary>
        /// <returns></returns>
        abstract GetTypes : unit -> Type[] 

        /// <summary>
        /// Compilers call this method to query a type provider for a type <c>name</c>.
        /// </summary>
        /// <remarks>Resolver should return a type called <c>name</c> in namespace <c>NamespaceName</c> or <c>null</c> if the type is unknown.
        /// </remarks>
        /// <returns></returns>
        abstract ResolveTypeName : typeName: string -> Type

    /// <summary>
    /// Represents an instantiation of a type provider component.
    /// </summary>
    type ITypeProvider =
        inherit System.IDisposable

        /// <summary>
        /// Namespace name the this TypeProvider injects types into.
        /// </summary>
        abstract GetNamespaces : unit -> IProvidedNamespace[] 

        /// <summary>
        /// Get the static parameters for a provided type. 
        /// </summary>
        ///
        /// <param name="typeWithoutArguments">A type returned by GetTypes or ResolveTypeName</param>
        ///
        /// <returns></returns>
        abstract GetStaticParameters : typeWithoutArguments:Type -> ParameterInfo[] 

        /// <summary>
        /// Apply static arguments to a provided type that accepts static arguments. 
        /// </summary>
        /// <remarks>The provider must return a type with the given mangled name.</remarks>
        /// <param name="typeWithoutArguments">the provided type definition which has static parameters</param>
        /// <param name="typePathWithArguments">the full path of the type, including encoded representations of static parameters</param>
        /// <param name="staticArguments">the static parameters, indexed by name</param>
        ///
        /// <returns></returns>
        abstract ApplyStaticArguments : typeWithoutArguments:Type * typePathWithArguments:string[] * staticArguments:obj[] -> Type 

        /// <summary>
        /// Called by the compiler to ask for an Expression tree to replace the given MethodBase with.
        /// </summary>
        ///
        /// <param name="syntheticMethodBase">MethodBase that was given to the compiler by a type returned by a GetType(s) call.</param>
        /// <param name="parameters">Expressions that represent the parameters to this call.</param>
        ///
        /// <returns>An expression that the compiler will use in place of the given method base.</returns>
        abstract GetInvokerExpression : syntheticMethodBase:MethodBase * parameters:Expr[] -> Expr

        /// <summary>
        /// Triggered when an assumption changes that invalidates the resolutions so far reported by the provider
        /// </summary>
        [<CLIEvent>]
        abstract Invalidate : IEvent<System.EventHandler, System.EventArgs>

        /// <summary>
        /// Get the physical contents of the given logical provided assembly.
        /// </summary>
        abstract GetGeneratedAssemblyContents : assembly:Assembly -> byte[]

    /// Represents additional, optional information for a type provider component
    type ITypeProvider2 =

        /// <summary>
        /// Get the static parameters for a provided method. 
        /// </summary>
        ///
        /// <param name="methodWithoutArguments">A method returned by GetMethod on a provided type</param>
        ///
        /// <returns>The static parameters of the provided method, if any</returns>

        abstract GetStaticParametersForMethod : methodWithoutArguments:MethodBase -> ParameterInfo[] 

        /// <summary>
        /// Apply static arguments to a provided method that accepts static arguments. 
        /// </summary>
        /// <remarks>The provider must return a provided method with the given mangled name.</remarks>
        /// <param name="methodWithoutArguments">the provided method definition which has static parameters</param>
        /// <param name="methodNameWithArguments">the full name of the method that must be returned, including encoded representations of static parameters</param>
        /// <param name="staticArguments">the values of the static parameters, indexed by name</param>
        ///
        /// <returns>The provided method definition corresponding to the given static parameter values</returns>
        abstract ApplyStaticArgumentsForMethod : methodWithoutArguments:MethodBase * methodNameWithArguments:string * staticArguments:obj[] -> MethodBase

    /// <summary>Place attribute on an assembly to indicate this is an F# analyzer assembly. </summary>
    [<AttributeUsageAttribute(AttributeTargets.Assembly, AllowMultiple = false)>]
    type AnalyzerAssemblyAttribute = 
        inherit System.Attribute
        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>AnalyzerAssemblyAttribute</returns>
        new : unit -> AnalyzerAssemblyAttribute

    /// <summary>Place attribute on a type to indicate this is an F# analyzer. </summary>
    [<AttributeUsageAttribute(AttributeTargets.Class, AllowMultiple = false)>]
    type AnalyzerAttribute = 
        inherit System.Attribute
        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>AnalyzerAttribute</returns>
        new : unit -> AnalyzerAttribute

