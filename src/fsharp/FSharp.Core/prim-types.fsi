// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

#nowarn "35" // This construct is deprecated: the treatment of this operator is now handled directly by the F# compiler and its meaning may not be redefined.
#nowarn "61" // The containing type can use 'null' as a representation value for its nullary union case. This member will be compiled as a static member.

/// <summary>Basic F# type definitions, functions and operators </summary>
namespace Microsoft.FSharp.Core

    open System

    /// <namespacedoc><summary>
    ///   Basic definitions of operators, options, functions, results, choices, attributes and plain text formatting.
    /// </summary></namespacedoc>
    ///
    /// <summary>The type 'unit', which has only one value "()". This value is special and
    /// always uses the representation 'null'.</summary>
    ///
    /// <category>Basic Types</category>
    /// <exclude />
    type Unit =
       interface IComparable
        
    /// <summary>The type 'unit', which has only one value "()". This value is special and
    /// always uses the representation 'null'.</summary>
    ///
    /// <category index="1">Basic Types</category>
    and unit = Unit

    /// <summary>Indicates the relationship between a compiled entity in a CLI binary and an element in F# source code.</summary>
    ///
    /// <category index="8">Attributes</category>
    type SourceConstructFlags = 
       /// <summary>Indicates that the compiled entity has no relationship to an element in F# source code.</summary>
       | None = 0

       /// <summary>Indicates that the compiled entity is part of the representation of an F# union type declaration.</summary>
       | SumType = 1

       /// <summary>Indicates that the compiled entity is part of the representation of an F# record type declaration.</summary>
       | RecordType = 2

       /// <summary>Indicates that the compiled entity is part of the representation of an F# class or other object type declaration.</summary>
       | ObjectType = 3

       /// <summary>Indicates that the compiled entity is part of the representation of an F# record or union case field declaration.</summary>
       | Field = 4

       /// <summary>Indicates that the compiled entity is part of the representation of an F# exception declaration.</summary>
       | Exception = 5

       /// <summary>Indicates that the compiled entity is part of the representation of an F# closure.</summary>
       | Closure = 6

       /// <summary>Indicates that the compiled entity is part of the representation of an F# module declaration.</summary>
       | Module = 7

       /// <summary>Indicates that the compiled entity is part of the representation of an F# union case declaration.</summary>
       | UnionCase = 8

       /// <summary>Indicates that the compiled entity is part of the representation of an F# value declaration.</summary>
       | Value = 9

       /// <summary>The mask of values related to the kind of the compiled entity.</summary>
       | KindMask = 31

       /// <summary>Indicates that the compiled entity had private or internal representation in F# source code.</summary>
       | NonPublicRepresentation = 32

    [<Flags>]
    /// <summary>Indicates one or more adjustments to the compiled representation of an F# type or member.</summary>
    ///
    /// <category>Attributes</category>
    type CompilationRepresentationFlags = 

       /// <summary>No special compilation representation.</summary>
       | None = 0

       /// <summary>Compile an instance member as 'static' .</summary>
       | Static = 1

       /// <summary>Compile a member as 'instance' even if <c>null</c> is used as a representation for this type.</summary>
       | Instance = 2

       /// <summary>append 'Module' to the end of a module whose name clashes with a type name in the same namespace.</summary>
       | ModuleSuffix = 4  

       /// <summary>Permit the use of <c>null</c> as a representation for nullary discriminators in a discriminated union.</summary>
       | UseNullAsTrueValue = 8

       /// <summary>Compile a property as a CLI event.</summary>
       | Event = 16

    /// <summary>Adding this attribute to class definition makes it sealed, which means it may not
    /// be extended or implemented.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Class,AllowMultiple=false)>]  
    type SealedAttribute =
        inherit Attribute
        /// <summary>Creates an instance of the attribute.</summary>
        /// <returns>The created attribute.</returns>
        new : unit -> SealedAttribute

        /// <summary>Creates an instance of the attribute</summary>
        ///
        /// <param name="value">Indicates whether the class is sealed.</param>
        ///
        /// <returns>SealedAttribute</returns>
        new : value:bool -> SealedAttribute
        
        /// <summary>The value of the attribute, indicating whether the type is sealed or not.</summary>
        member Value: bool

    /// <summary>Adding this attribute to class definition makes it abstract, which means it need not
    /// implement all its methods. Instances of abstract classes may not be constructed directly.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Class,AllowMultiple=false)>]  
    [<Sealed>]
    type AbstractClassAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>AbstractClassAttribute</returns>
        new : unit -> AbstractClassAttribute

    /// <summary>Adding this attribute to the let-binding for the definition of a top-level 
    /// value makes the quotation expression that implements the value available
    /// for use at runtime.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Class ||| AttributeTargets.Parameter ||| AttributeTargets.Method ||| AttributeTargets.Property ||| AttributeTargets.Constructor,AllowMultiple=false)>]  
    [<Sealed>]
    type ReflectedDefinitionAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>ReflectedDefinitionAttribute</returns>
        new : unit -> ReflectedDefinitionAttribute

        /// <summary>Creates an instance of the attribute</summary>
        ///
        /// <param name="includeValue">Indicates whether to include the evaluated value of the definition as the outer node of the quotation</param>
        ///
        /// <returns>ReflectedDefinitionAttribute</returns>
        new : includeValue:bool -> ReflectedDefinitionAttribute

        /// <summary>The value of the attribute, indicating whether to include the evaluated value of the definition as the outer node of the quotation</summary>
        member IncludeValue: bool

    /// <summary>This attribute is used to indicate a generic container type satisfies the F# 'equality' 
    /// constraint only if a generic argument also satisfies this constraint.</summary>
    ///
    ///
    ///
    /// <remarks> For example, adding 
    /// this attribute to parameter 'T on a type definition C&lt;'T&gt; means that a type C&lt;X&gt; only supports 
    /// equality if the type X also supports equality and all other conditions for C&lt;X&gt; to support 
    /// equality are also met. The type C&lt;'T&gt; can still be used with other type arguments, but a type such 
    /// as C&lt;(int -> int)&gt; will not support equality because the type (int -> int) is an F# function type 
    /// and does not support equality.
    /// 
    /// This attribute will be ignored if it is used on the generic parameters of functions or methods.
    /// </remarks>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.GenericParameter,AllowMultiple=false)>]  
    [<Sealed>]
    type EqualityConditionalOnAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>EqualityConditionalOnAttribute</returns>
        new : unit -> EqualityConditionalOnAttribute

    /// <summary>This attribute is used to indicate a generic container type satisfies the F# 'comparison' 
    /// constraint only if a generic argument also satisfies this constraint.</summary>
    ///
    /// <remarks>For example, adding 
    /// this attribute to parameter 'T on a type definition C&lt;'T&gt; means that a type C&lt;X&gt; only supports 
    /// comparison if the type X also supports comparison and all other conditions for C&lt;X&gt; to support 
    /// comparison are also met. The type C&lt;'T&gt; can still be used with other type arguments, but a type such 
    /// as C&lt;(int -> int)&gt; will not support comparison because the type (int -> int) is an F# function type 
    /// and does not support comparison.
    ///
    /// This attribute will be ignored if it is used on the generic parameters of functions or methods.
    /// </remarks>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.GenericParameter,AllowMultiple=false)>]  
    [<Sealed>]
    type ComparisonConditionalOnAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>ComparisonConditionalOnAttribute</returns>
        new : unit -> ComparisonConditionalOnAttribute

    /// <summary>Adding this attribute to a type causes it to be represented using a CLI struct.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Struct ||| AttributeTargets.ReturnValue ,AllowMultiple=false)>]  
    [<Sealed>]
    type StructAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>StructAttribute</returns>
        new : unit -> StructAttribute

    /// <summary>Adding this attribute to a type causes it to be interpreted as a unit of measure.
    /// This may only be used under very limited conditions.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.GenericParameter ||| AttributeTargets.Class,AllowMultiple=false)>]  
    [<Sealed>]
    type MeasureAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>MeasureAttribute</returns>
        new : unit -> MeasureAttribute

    /// <summary>Adding this attribute to a type causes it to be interpreted as a refined type, currently limited to measure-parameterized types.
    /// This may only be used under very limited conditions.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Class,AllowMultiple=false)>]  
    [<Sealed>]
    type MeasureAnnotatedAbbreviationAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>MeasureAnnotatedAbbreviationAttribute</returns>
        new : unit -> MeasureAnnotatedAbbreviationAttribute

    /// <summary>Adding this attribute to a type causes it to be represented using a CLI interface.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Interface,AllowMultiple=false)>]  
    [<Sealed>]
    type InterfaceAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>InterfaceAttribute</returns>
        new : unit -> InterfaceAttribute

    /// <summary>Adding this attribute to a type causes it to be represented using a CLI class.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Class,AllowMultiple=false)>]  
    [<Sealed>]
    type ClassAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>ClassAttribute</returns>
        new : unit -> ClassAttribute

    /// <summary>Adding this attribute to a type lets the 'null' literal be used for the type 
    /// within F# code. This attribute may only be added to F#-defined class or 
    /// interface types.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Class,AllowMultiple=false)>]  
    [<Sealed>]
    type AllowNullLiteralAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>AllowNullLiteralAttribute</returns>
        new : unit -> AllowNullLiteralAttribute

        /// <summary>Creates an instance of the attribute with the specified value</summary>
        /// <returns>AllowNullLiteralAttribute</returns>
        new : value: bool -> AllowNullLiteralAttribute

        /// <summary>The value of the attribute, indicating whether the type allows the null literal or not</summary>
        member Value: bool

    /// <summary>Adding this attribute to a value causes it to be compiled as a CLI constant literal.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Field,AllowMultiple=false)>]  
    [<Sealed>]
    type LiteralAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>LiteralAttribute</returns>
        new : unit -> LiteralAttribute

    /// <summary>Adding this attribute to a property with event type causes it to be compiled with as a CLI
    /// metadata event, through a syntactic translation to a pair of 'add_EventName' and 
    /// 'remove_EventName' methods.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Property,AllowMultiple=false)>]  
    [<Sealed>]
    type CLIEventAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>CLIEventAttribute</returns>
        new : unit -> CLIEventAttribute

    /// <summary>Adding this attribute to a record type causes it to be compiled to a CLI representation
    /// with a default constructor with property getters and setters.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Class,AllowMultiple=false)>]  
    [<Sealed>]
    type CLIMutableAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>CLIMutableAttribute</returns>
        new : unit -> CLIMutableAttribute

    /// <summary>Adding this attribute to a discriminated union with value false
    /// turns off the generation of standard helper member tester, constructor 
    /// and accessor members for the generated CLI class for that type.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Class,AllowMultiple=false)>]  
    [<Sealed>]
    type DefaultAugmentationAttribute =
        inherit Attribute

        /// <summary>The value of the attribute, indicating whether the type has a default augmentation or not</summary>
        member Value: bool

        /// <summary>Creates an instance of the attribute</summary>
        ///
        /// <param name="value">Indicates whether to generate helper members on the CLI class representing a discriminated
        /// union.</param>
        ///
        /// <returns>DefaultAugmentationAttribute</returns>
        new : value:bool -> DefaultAugmentationAttribute

    /// <summary>Adding this attribute to an F# mutable binding causes the "volatile"
    /// prefix to be used for all accesses to the field.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Field,AllowMultiple=false)>]  
    [<Sealed>]
    type VolatileFieldAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>VolatileFieldAttribute</returns>
        new : unit -> VolatileFieldAttribute

    /// <summary>Adding this attribute to a function indicates it is the entrypoint for an application.
    /// If this attribute is not specified for an EXE then the initialization implicit in the
    /// module bindings in the last file in the compilation sequence are used as the entrypoint.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Method,AllowMultiple=false)>]  
    [<Sealed>]
    type EntryPointAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>EntryPointAttribute</returns>
        new : unit -> EntryPointAttribute

    /// <summary>Adding this attribute to a record or union type disables the automatic generation
    /// of overrides for 'System.Object.Equals(obj)', 'System.Object.GetHashCode()' 
    /// and 'System.IComparable' for the type. The type will by default use reference equality.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Class,AllowMultiple=false)>]  
    [<Sealed>]
    type ReferenceEqualityAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>ReferenceEqualityAttribute</returns>
        new : unit -> ReferenceEqualityAttribute

    /// <summary>Adding this attribute to a record, union or struct type confirms the automatic 
    /// generation of overrides for 'System.Object.Equals(obj)' and 
    /// 'System.Object.GetHashCode()' for the type. </summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Class,AllowMultiple=false)>]  
    [<Sealed>]
    type StructuralEqualityAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>StructuralEqualityAttribute</returns>
        new : unit -> StructuralEqualityAttribute

    /// <summary>Adding this attribute to a record, union, exception, or struct type confirms the 
    /// automatic generation of implementations for 'System.IComparable' for the type.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Class,AllowMultiple=false)>]  
    [<Sealed>]
    type StructuralComparisonAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>StructuralComparisonAttribute</returns>
        new : unit -> StructuralComparisonAttribute

    [<AttributeUsage(AttributeTargets.Method,AllowMultiple=false)>]
    [<Sealed>]
    /// <summary>Indicates that a member on a computation builder type is a custom query operator,
    /// and indicates the name of that operator.</summary>
    ///
    /// <category>Attributes</category>
    type CustomOperationAttribute = 
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>CustomOperationAttribute</returns>
        new : name:string -> CustomOperationAttribute

        /// <summary>Get the name of the custom operation when used in a query or other computation expression</summary>
        member Name : string

        /// <summary>Indicates if the custom operation supports the use of 'into' immediately after the use of the operation in a query or other computation expression to consume the results of the operation</summary>
        member AllowIntoPattern : bool with get,set

        /// <summary>Indicates if the custom operation is an operation similar to a zip in a sequence computation, supporting two inputs</summary>
        member IsLikeZip : bool with get,set

        /// <summary>Indicates if the custom operation is an operation similar to a join in a sequence computation, supporting two inputs and a correlation constraint</summary>
        member IsLikeJoin : bool with get,set

        /// <summary>Indicates if the custom operation is an operation similar to a group join in a sequence computation, supporting two inputs and a correlation constraint, and generating a group</summary>
        member IsLikeGroupJoin : bool with get,set

        /// <summary>Indicates the name used for the 'on' part of the custom query operator for join-like operators</summary>
        member JoinConditionWord : string with get,set

        /// <summary>Indicates if the custom operation maintains the variable space of the query of computation expression</summary>
        member MaintainsVariableSpace : bool with get,set

        /// <summary>Indicates if the custom operation maintains the variable space of the query of computation expression through the use of a bind operation</summary>
        member MaintainsVariableSpaceUsingBind : bool with get,set

    [<AttributeUsage(AttributeTargets.Parameter,AllowMultiple=false)>]
    [<Sealed>]
    /// <summary>Indicates that, when a custom operator is used in a computation expression,
    /// a parameter is automatically parameterized by the variable space of the computation expression</summary>
    ///
    /// <category>Attributes</category>
    type ProjectionParameterAttribute = 

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>ProjectionParameterAttribute</returns>
        new : unit -> ProjectionParameterAttribute
        inherit Attribute

    /// <summary>Adding this attribute to a type indicates it is a type where equality is an abnormal operation.
    /// This means that the type does not satisfy the F# 'equality' constraint. Within the bounds of the 
    /// F# type system, this helps ensure that the F# generic equality function is not instantiated directly
    /// at this type. The attribute and checking does not constrain the use of comparison with base or child 
    /// types of this type.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Class ||| AttributeTargets.Interface ||| AttributeTargets.Delegate ||| AttributeTargets.Struct ||| AttributeTargets.Enum,AllowMultiple=false)>]  
    [<Sealed>]
    type NoEqualityAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>NoEqualityAttribute</returns>
        new : unit -> NoEqualityAttribute

    /// <summary>Adding this attribute to a type indicates it is a type with a user-defined implementation of equality.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Class ||| AttributeTargets.Struct,AllowMultiple=false)>]  
    [<Sealed>]
    type CustomEqualityAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>CustomEqualityAttribute</returns>
        new : unit -> CustomEqualityAttribute

    /// <summary>Adding this attribute to a type indicates it is a type with a user-defined implementation of comparison.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Class ||| AttributeTargets.Struct,AllowMultiple=false)>]  
    [<Sealed>]
    type CustomComparisonAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>CustomComparisonAttribute</returns>
        new : unit -> CustomComparisonAttribute

    /// <summary>Adding this attribute to a type indicates it is a type where comparison is an abnormal operation.
    /// This means that the type does not satisfy the F# 'comparison' constraint. Within the bounds of the 
    /// F# type system, this helps ensure that the F# generic comparison function is not instantiated directly
    /// at this type. The attribute and checking does not constrain the use of comparison with base or child 
    /// types of this type.
    /// </summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Class ||| AttributeTargets.Interface ||| AttributeTargets.Delegate ||| AttributeTargets.Struct ||| AttributeTargets.Enum,AllowMultiple=false)>]  
    [<Sealed>]
    type NoComparisonAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>NoComparisonAttribute</returns>
        new : unit -> NoComparisonAttribute

    /// <summary>Adding this attribute to a field declaration means that the field is 
    /// not initialized. During type checking a constraint is asserted that the field type supports 'null'. 
    /// If the 'check' value is false then the constraint is not asserted. 
    /// </summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Field|||AttributeTargets.Method,AllowMultiple=false)>]  
    [<Sealed>]
    type DefaultValueAttribute =
        inherit Attribute

        /// <summary>Indicates if a constraint is asserted that the field type supports 'null'</summary>
        member Check: bool

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>DefaultValueAttribute</returns>
        new : unit -> DefaultValueAttribute

        /// <summary>Creates an instance of the attribute</summary>
        ///
        /// <param name="check">Indicates whether to assert that the field type supports <c>null</c>.</param>
        ///
        /// <returns>DefaultValueAttribute</returns>
        new : check: bool -> DefaultValueAttribute

    /// <summary>This attribute is added automatically for all optional arguments.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Parameter,AllowMultiple=false)>]  
    [<Sealed>]
    type OptionalArgumentAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>OptionalArgumentAttribute</returns>
        new : unit -> OptionalArgumentAttribute

    /// <summary>Adding this attribute to a type, value or member requires that 
    /// uses of the construct must explicitly instantiate any generic type parameters.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Method,AllowMultiple=false)>]  
    [<Sealed>]
    type RequiresExplicitTypeArgumentsAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>RequiresExplicitTypeArgumentsAttribute</returns>
        new : unit -> RequiresExplicitTypeArgumentsAttribute

    /// <summary>Adding this attribute to a non-function value with generic parameters indicates that 
    /// uses of the construct can give rise to generic code through type inference. </summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Method,AllowMultiple=false)>]  
    [<Sealed>]
    type GeneralizableValueAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>GeneralizableValueAttribute</returns>
        new : unit -> GeneralizableValueAttribute

    /// <summary>Adding this attribute to a value or function definition in an F# module changes the name used
    /// for the value in compiled CLI code.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Method ||| AttributeTargets.Class ||| AttributeTargets.Field ||| AttributeTargets.Interface ||| AttributeTargets.Struct ||| AttributeTargets.Delegate ||| AttributeTargets.Enum ||| AttributeTargets.Property,AllowMultiple=false)>]  
    [<Sealed>]
    type CompiledNameAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        ///
        /// <param name="compiledName">The name to use in compiled code.</param>
        ///
        /// <returns>CompiledNameAttribute</returns>
        new : compiledName:string -> CompiledNameAttribute

        /// <summary>The name of the value as it appears in compiled code</summary>
        member CompiledName: string

    /// <summary>Adding this attribute to a type with value 'false' disables the behaviour where F# makes the
    /// type Serializable by default.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Class,AllowMultiple=false)>]  
    [<Sealed>]
    type AutoSerializableAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        ///
        /// <param name="value">Indicates whether the type should be serializable by default.</param>
        ///
        /// <returns>AutoSerializableAttribute</returns>
        new : value:bool -> AutoSerializableAttribute

        /// <summary>The value of the attribute, indicating whether the type is automatically marked serializable or not</summary>
        member Value: bool

    /// <summary>This attribute is added to generated assemblies to indicate the 
    /// version of the data schema used to encode additional F#
    /// specific information in the resource attached to compiled F# libraries.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Assembly,AllowMultiple=false)>]  
    [<Sealed>]
    type FSharpInterfaceDataVersionAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        ///
        /// <param name="major">The major version number.</param>
        /// <param name="minor">The minor version number.</param>
        /// <param name="release">The release number.</param>
        ///
        /// <returns>FSharpInterfaceDataVersionAttribute</returns>
        new : major:int * minor:int * release:int -> FSharpInterfaceDataVersionAttribute

        /// <summary>The major version number of the F# version associated with the attribute</summary>
        member Major: int

        /// <summary>The minor version number of the F# version associated with the attribute</summary>
        member Minor: int

        /// <summary>The release number of the F# version associated with the attribute</summary>
        member Release: int

    /// <summary>This attribute is inserted automatically by the F# compiler to tag types 
    /// and methods in the generated CLI code with flags indicating the correspondence 
    /// with original source constructs.</summary>
    ///
    /// <remarks>This attribute is used by the functions in the 
    /// FSharp.Reflection namespace to reverse-map compiled constructs to 
    /// their original forms. It is not intended for use from user code.</remarks>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.All,AllowMultiple=false)>]  
    [<Sealed>]
    type CompilationMappingAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        ///
        /// <param name="sourceConstructFlags">Indicates the type of source construct.</param>
        ///
        /// <returns>CompilationMappingAttribute</returns>
        new : sourceConstructFlags:SourceConstructFlags -> CompilationMappingAttribute

        /// <summary>Creates an instance of the attribute</summary>
        ///
        /// <param name="sourceConstructFlags">Indicates the type of source construct.</param>
        /// <param name="sequenceNumber">Indicates the index in the sequence of constructs.</param>
        ///
        /// <returns>CompilationMappingAttribute</returns>
        new : sourceConstructFlags:SourceConstructFlags * sequenceNumber: int -> CompilationMappingAttribute

        /// <summary>Creates an instance of the attribute</summary>
        ///
        /// <param name="sourceConstructFlags">Indicates the type of source construct.</param>
        /// <param name="variantNumber">Indicates the index in the sequence of variants.</param>
        /// <param name="sequenceNumber">Indicates the index in the sequence of constructs.</param>
        ///
        /// <returns>CompilationMappingAttribute</returns>
        new : sourceConstructFlags:SourceConstructFlags * variantNumber : int * sequenceNumber : int -> CompilationMappingAttribute

        /// <summary>Creates an instance of the attribute</summary>
        ///
        /// <param name="typeDefinitions">Indicates the type definitions needed to resolve the source construct.</param>
        /// <param name="resourceName">The name of the resource needed to resolve the source construct.</param>
        ///
        /// <returns>CompilationMappingAttribute</returns>
        new : resourceName:string * typeDefinitions:System.Type[] -> CompilationMappingAttribute

        /// <summary>Indicates the relationship between the compiled entity and F# source code</summary>
        member SourceConstructFlags : SourceConstructFlags

        /// <summary>Indicates the sequence number of the entity, if any, in a linear sequence of elements with F# source code</summary>
        member SequenceNumber : int

        /// <summary>Indicates the variant number of the entity, if any, in a linear sequence of elements with F# source code</summary>
        member VariantNumber : int
        /// <summary>Indicates the resource the source construct relates to</summary>
        member ResourceName : string

        /// <summary>Indicates the type definitions needed to resolve the source construct</summary>
        member TypeDefinitions : System.Type[]

    /// <summary>This attribute is inserted automatically by the F# compiler to tag 
    /// methods which are given the 'CompiledName' attribute.</summary>
    ///
    /// <remarks>This attribute is used by the functions in the 
    /// FSharp.Reflection namespace to reverse-map compiled constructs to 
    /// their original forms. It is not intended for use from user code.
    /// </remarks>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.All,AllowMultiple=false)>]  
    [<Sealed>]
    type CompilationSourceNameAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        ///
        /// <param name="sourceName">The name of the method in source.</param>
        ///
        /// <returns>CompilationSourceNameAttribute</returns>
        new : sourceName:string -> CompilationSourceNameAttribute

        /// <summary>Indicates the name of the entity in F# source code</summary>
        member SourceName : string

    /// <summary>This attribute is used to adjust the runtime representation for a type. 
    /// For example, it may be used to note that the <c>null</c> representation
    /// may be used for a type. This affects how some constructs are compiled.
    /// </summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.All,AllowMultiple=false)>]  
    [<Sealed>]
    type CompilationRepresentationAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        ///
        /// <param name="flags">Indicates adjustments to the compiled representation of the type or member.</param>
        ///
        /// <returns>CompilationRepresentationAttribute</returns>
        new : flags:CompilationRepresentationFlags -> CompilationRepresentationAttribute

        /// <summary>Indicates one or more adjustments to the compiled representation of an F# type or member</summary>
        member Flags : CompilationRepresentationFlags

    module internal ExperimentalAttributeMessages =
        [<Literal>]
        val RequiresPreview : string = "Experimental library feature, requires '--langversion:preview'"
        [<Literal>]
        val NotSupportedYet : string = "This construct is not supported by your version of the F# compiler"
        
    /// <summary>This attribute is used to tag values that are part of an experimental library
    /// feature.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.All,AllowMultiple=false)>]
    [<Sealed>]
    type ExperimentalAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        ///
        /// <param name="message">The warning message to be emitted when code uses this construct.</param>
        ///
        /// <returns>ExperimentalAttribute</returns>
        new : message:string-> ExperimentalAttribute

        /// <summary>Indicates the warning message to be emitted when F# source code uses this construct</summary>
        member Message: string

    /// <summary>Adding this attribute to a parameter of function type indicates that, if the overall function or method is inlined and the parameter is
    /// determined to be a known lambda, then this function should be statically inlined throughout the body of the function of method.</summary>
    ///
    /// <remarks>If the function parameter is called multiple times in the implementation of the function or method this attribute may cause code explosion and slow compilation times.</remarks>
    ///
    /// <category>Attributes</category>
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
    [<AttributeUsage (AttributeTargets.Parameter,AllowMultiple=false)>]  
    [<Sealed>]
    type InlineIfLambdaAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>InlineIfLambdaAttribute</returns>
        new : unit -> InlineIfLambdaAttribute

    /// <summary>This attribute is generated automatically by the F# compiler to tag functions and members 
    /// that accept a partial application of some of their arguments and return a residual function.
    /// </summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Method,AllowMultiple=false)>]  
    [<Sealed>]
    type CompilationArgumentCountsAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        ///
        /// <param name="counts">Indicates the number of arguments in each argument group.</param>
        ///
        /// <returns>CompilationArgumentCountsAttribute</returns>
        new : counts:int[] -> CompilationArgumentCountsAttribute

        /// <summary>Indicates the number of arguments in each argument group </summary>
        member Counts: System.Collections.Generic.IEnumerable<int>

    /// <summary>This attribute is used to mark how a type is displayed by default when using 
    /// '%A' printf formatting patterns and other two-dimensional text-based display layouts. 
    /// In this version of F# valid values are of the form <c>PreText {PropertyName1} PostText {PropertyName2} ... {PropertyNameX} PostText</c>.
    /// The property names indicate properties to evaluate and to display instead of the object itself. </summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Class ||| AttributeTargets.Interface ||| AttributeTargets.Struct ||| AttributeTargets.Delegate ||| AttributeTargets.Enum,AllowMultiple=false)>]  
    [<Sealed>]
    type StructuredFormatDisplayAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        ///
        /// <param name="value">Indicates the text to display when using the '%A' printf formatting.</param>
        ///
        /// <returns>StructuredFormatDisplayAttribute</returns>
        new : value:string-> StructuredFormatDisplayAttribute

        /// <summary>Indicates the text to display by default when objects of this type are displayed 
        /// using '%A' printf formatting patterns and other two-dimensional text-based display 
        /// layouts. </summary>
        member Value: string

    /// <summary>Indicates that a message should be emitted when F# source code uses this construct.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.All,AllowMultiple=false)>]  
    [<Sealed>]
    type CompilerMessageAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute.</summary>
        new : message:string * messageNumber: int -> CompilerMessageAttribute

        /// <summary>Indicates the warning message to be emitted when F# source code uses this construct</summary>
        member Message: string

        /// <summary>Indicates the number associated with the message.</summary>
        member MessageNumber: int

        /// <summary>Indicates if the message should indicate a compiler error. Error numbers less than
        /// 10000 are considered reserved for use by the F# compiler and libraries.</summary>
        member IsError: bool with get,set

        /// <summary>Indicates if the construct should always be hidden in an editing environment.</summary>
        member IsHidden: bool with get,set

    /// <summary>This attribute is used to tag values whose use will result in the generation
    /// of unverifiable code. These values are inevitably marked 'inline' to ensure that
    /// the unverifiable constructs are not present in the actual code for the F# library,
    /// but are rather copied to the source code of the caller.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Method ||| AttributeTargets.Property,AllowMultiple=false)>]  
    [<Sealed>]
    type UnverifiableAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>UnverifiableAttribute</returns>
        new : unit -> UnverifiableAttribute

    /// <summary>This attribute is used to tag values that may not be dynamically invoked at runtime. This is
    /// typically added to inlined functions whose implementations include unverifiable code. It
    /// causes the method body emitted for the inlined function to raise an exception if 
    /// dynamically invoked, rather than including the unverifiable code in the generated
    /// assembly.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Method ||| AttributeTargets.Property,AllowMultiple=false)>]  
    [<Sealed>]
    type NoDynamicInvocationAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>NoDynamicInvocationAttribute</returns>
        new : unit -> NoDynamicInvocationAttribute

        internal new : isLegacy: bool -> NoDynamicInvocationAttribute

    /// <summary>This attribute is used to indicate that references to the elements of a module, record or union 
    /// type require explicit qualified access.</summary>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Class,AllowMultiple=false)>]  
    [<Sealed>]
    type RequireQualifiedAccessAttribute =
        inherit Attribute

        /// <summary>Creates an instance of the attribute</summary>
        /// <returns>RequireQualifiedAccessAttribute</returns>
        new : unit -> RequireQualifiedAccessAttribute

    /// <summary>Indicates a construct is automatically opened when brought into scope through
    /// an assembly reference or then opening of the containing namespace or module.</summary>
    ///
    /// <remarks>When applied to an assembly, this attribute must be given a string
    /// argument, and this indicates a valid module or namespace in that assembly. Source
    /// code files compiled with a reference to this assembly are processed in an environment
    /// where the given path is automatically opened.
    ///
    /// When applied to a type or module within an assembly, then the attribute must not be given any arguments, and
    /// the type or module is implicitly opened when its enclosing namespace or module is opened.
    /// </remarks>
    ///
    /// <category>Attributes</category>
    [<AttributeUsage (AttributeTargets.Class ||| AttributeTargets.Assembly, AllowMultiple=true)>]  
    [<Sealed>]
    type AutoOpenAttribute =
        inherit Attribute

        /// <summary>Creates an attribute used to mark a module as 'automatically opened' when the enclosing namespace is opened</summary>
        /// <returns>AutoOpenAttribute</returns>
        new : unit -> AutoOpenAttribute

        /// <summary>Creates an attribute used to mark a namespace or module path to be 'automatically opened' when an assembly is referenced</summary>
        ///
        /// <param name="path">The namespace or module to be automatically opened when an assembly is referenced
        /// or an enclosing module opened.</param>
        ///
        /// <returns>AutoOpenAttribute</returns>
        new : path:string-> AutoOpenAttribute

        /// <summary>Indicates the namespace or module to be automatically opened when an assembly is referenced
        /// or an enclosing module opened.</summary>
        member Path: string

    [<MeasureAnnotatedAbbreviation>] 
    /// <summary>The type of double-precision floating point numbers, annotated with a unit of measure. 
    /// The unit of measure is erased in compiled code and when values of this type
    /// are analyzed using reflection. The type is representationally equivalent to 
    /// <see cref="T:System.Double"/>.</summary>
    ///
    /// <category index="6">Basic Types with Units of Measure</category>
    type float<[<Measure>] 'Measure> = float

    [<MeasureAnnotatedAbbreviation>] 
    /// <summary>The type of single-precision floating point numbers, annotated with a unit of measure. 
    /// The unit of measure is erased in compiled code and when values of this type
    /// are analyzed using reflection. The type is representationally equivalent to 
    /// <see cref="T:System.Single"/>.
    /// </summary>
    ///
    /// <category>Basic Types with Units of Measure</category>
    type float32<[<Measure>] 'Measure> = float32
    
    [<MeasureAnnotatedAbbreviation>] 
    /// <summary>The type of decimal numbers, annotated with a unit of measure. The unit
    /// of measure is erased in compiled code and when values of this type
    /// are analyzed using reflection. The type is representationally equivalent to 
    /// <see cref="T:System.Decimal"/>.</summary>
    ///
    /// <category>Basic Types with Units of Measure</category>
    type decimal<[<Measure>] 'Measure> = decimal

    [<MeasureAnnotatedAbbreviation>] 
    /// <summary>The type of 32-bit signed integer numbers, annotated with a unit of measure. The unit
    /// of measure is erased in compiled code and when values of this type
    /// are analyzed using reflection. The type is representationally equivalent to 
    /// <see cref="T:System.Int32"/>.</summary>
    ///
    /// <category>Basic Types with Units of Measure</category>
    type int<[<Measure>] 'Measure> = int
    
    [<MeasureAnnotatedAbbreviation>] 
    /// <summary>The type of 8-bit signed integer numbers, annotated with a unit of measure. The unit
    /// of measure is erased in compiled code and when values of this type
    /// are analyzed using reflection. The type is representationally equivalent to 
    /// <see cref="T:System.SByte"/>.</summary>
    ///
    /// <category>Basic Types with Units of Measure</category>
    type sbyte<[<Measure>] 'Measure> = sbyte

    [<MeasureAnnotatedAbbreviation>] 
    /// <summary>The type of 16-bit signed integer numbers, annotated with a unit of measure. The unit
    /// of measure is erased in compiled code and when values of this type
    /// are analyzed using reflection. The type is representationally equivalent to 
    /// <see cref="T:System.Int16"/>.</summary>
    ///
    /// <category>Basic Types with Units of Measure</category>
    type int16<[<Measure>] 'Measure> = int16

    [<MeasureAnnotatedAbbreviation>] 
    /// <summary>The type of 64-bit signed integer numbers, annotated with a unit of measure. The unit
    /// of measure is erased in compiled code and when values of this type
    /// are analyzed using reflection. The type is representationally equivalent to 
    /// <see cref="T:System.Int64"/>.</summary>
    ///
    /// <category>Basic Types with Units of Measure</category>
    type int64<[<Measure>] 'Measure> = int64

    [<Experimental("Experimental library feature, requires '--langversion:preview'")>] 
    [<MeasureAnnotatedAbbreviation>]
    /// <summary>The type of machine-sized signed integer numbers, annotated with a unit of measure. 
    /// The unit of measure is erased in compiled code and when values of this type
    /// are analyzed using reflection. The type is representationally equivalent to 
    /// <see cref="T:System.IntPtr"/>.</summary>
    ///
    /// <category>Basic Types with Units of Measure</category>
    type nativeint<[<Measure>] 'Measure> = nativeint

    [<Experimental("Experimental library feature, requires '--langversion:preview'")>] 
    [<MeasureAnnotatedAbbreviation>] 
    /// <summary>The type of 32-bit unsigned integer numbers, annotated with a unit of measure. 
    /// The unit of measure is erased in compiled code and when values of this type
    /// are analyzed using reflection. The type is representationally equivalent to 
    /// <see cref="T:System.UInt32"/>.</summary>
    ///
    /// <category>Basic Types with Units of Measure</category>
    type uint<[<Measure>] 'Measure> = uint
    
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>] 
    [<MeasureAnnotatedAbbreviation>] 
    /// <summary>The type of 8-bit unsigned integer numbers, annotated with a unit of measure. 
    /// The unit of measure is erased in compiled code and when values of this type
    /// are analyzed using reflection. The type is representationally equivalent to 
    /// <see cref="T:System.Byte"/>.</summary>
    ///
    /// <category>Basic Types with Units of Measure</category>
    type byte<[<Measure>] 'Measure> = byte
    
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>] 
    [<MeasureAnnotatedAbbreviation>] 
    /// <summary>The type of 16-bit unsigned integer numbers, annotated with a unit of measure. 
    /// The unit of measure is erased in compiled code and when values of this type
    /// are analyzed using reflection. The type is representationally equivalent to 
    /// <see cref="T:System.UInt16"/>.</summary>
    ///
    /// <category>Basic Types with Units of Measure</category>
    type uint16<[<Measure>] 'Measure> = uint16
    
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>] 
    [<MeasureAnnotatedAbbreviation>] 
    /// <summary>The type of 64-bit unsigned integer numbers, annotated with a unit of measure. 
    /// The unit of measure is erased in compiled code and when values of this type
    /// are analyzed using reflection. The type is representationally equivalent to 
    /// <see cref="T:System.UInt64"/>.</summary>
    ///
    /// <category>Basic Types with Units of Measure</category>
    type uint64<[<Measure>] 'Measure> = uint64
    
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>] 
    [<MeasureAnnotatedAbbreviation>]
    /// <summary>The type of machine-sized unsigned integer numbers, annotated with a unit of measure. 
    /// The unit of measure is erased in compiled code and when values of this type
    /// are analyzed using reflection. The type is representationally equivalent to 
    /// <see cref="T:System.UIntPtr"/>.</summary>
    ///
    /// <category>Basic Types with Units of Measure</category>
    type unativeint<[<Measure>] 'Measure> = unativeint
    
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>] 
    /// <summary>The type of double-precision floating point numbers, annotated with a unit of measure. 
    /// The unit of measure is erased in compiled code and when values of this type
    /// are analyzed using reflection. The type is representationally equivalent to 
    /// <see cref="T:System.Double"/>.</summary>
    ///
    /// <category index="6">Basic Types with Units of Measure</category>
    type double<[<Measure>] 'Measure> = float<'Measure>
    
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>] 
    /// <summary>The type of single-precision floating point numbers, annotated with a unit of measure. 
    /// The unit of measure is erased in compiled code and when values of this type
    /// are analyzed using reflection. The type is representationally equivalent to 
    /// <see cref="T:System.Single"/>.</summary>
    ///
    /// <category index="6">Basic Types with Units of Measure</category>
    type single<[<Measure>] 'Measure> = float32<'Measure>
    
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>] 
    /// <summary>The type of 8-bit signed integer numbers, annotated with a unit of measure. 
    /// The unit of measure is erased in compiled code and when values of this type
    /// are analyzed using reflection. The type is representationally equivalent to 
    /// <see cref="T:System.SByte"/>.</summary>
    ///
    /// <category>Basic Types with Units of Measure</category>
    type int8<[<Measure>] 'Measure> = sbyte<'Measure>
    
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>] 
    /// <summary>The type of 32-bit signed integer numbers, annotated with a unit of measure. 
    /// The unit of measure is erased in compiled code and when values of this type
    /// are analyzed using reflection. The type is representationally equivalent to 
    /// <see cref="T:System.Int32"/>.</summary>
    ///
    /// <category>Basic Types with Units of Measure</category>
    type int32<[<Measure>] 'Measure> = int<'Measure>
    
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>] 
    /// <summary>The type of 8-bit unsigned integer numbers, annotated with a unit of measure. 
    /// The unit of measure is erased in compiled code and when values of this type
    /// are analyzed using reflection. The type is representationally equivalent to 
    /// <see cref="T:System.Byte"/>.</summary>
    ///
    /// <category>Basic Types with Units of Measure</category>
    type uint8<[<Measure>] 'Measure> = byte<'Measure>
    
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>] 
    /// <summary>The type of 32-bit unsigned integer numbers, annotated with a unit of measure. 
    /// The unit of measure is erased in compiled code and when values of this type
    /// are analyzed using reflection. The type is representationally equivalent to 
    /// <see cref="T:System.UInt32"/>.</summary>
    ///
    /// <category>Basic Types with Units of Measure</category>
    type uint32<[<Measure>] 'Measure> = uint<'Measure>

    /// <summary>Represents a managed pointer in F# code.</summary>
#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE
    [<CompilerMessage("This construct is for use in the FSharp.Core library and should not be used directly", 1204, IsHidden=true)>]
#else
    [<CompilerMessage("This construct is for use in the FSharp.Core library and should not be used directly", 1204, IsHidden=true, IsError=true)>]
#endif
    /// <category index="7">ByRef and Pointer Types</category>
    type byref<'T, 'Kind> = (# "!0&" #)

    /// <summary>Represents a managed pointer in F# code. For F# 4.5+ this is considered equivalent to <c>byref&lt;'T, ByRefKinds.InOut&gt;</c></summary>
    /// <category>ByRef and Pointer Types</category>
    type byref<'T> = (# "!0&" #)

    /// <summary>Represents the types of byrefs in F# 4.5+</summary>
#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE
    [<CompilerMessage("This construct is for use in the FSharp.Core library and should not be used directly", 1204, IsHidden=true)>]
#else
    [<CompilerMessage("This construct is for use in the FSharp.Core library and should not be used directly", 1204, IsHidden=true, IsError=true)>]
#endif
    /// <category>ByRef and Pointer Types</category>
    module ByRefKinds = 

        /// Represents a byref that can be written
#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE
        [<CompilerMessage("This construct is for use in the FSharp.Core library and should not be used directly", 1204, IsHidden=true)>]
#else
        [<CompilerMessage("This construct is for use in the FSharp.Core library and should not be used directly", 1204, IsHidden=true, IsError=true)>]
#endif
        type Out

        /// Represents a byref that can be read
#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE
        [<CompilerMessage("This construct is for use in the FSharp.Core library and should not be used directly", 1204, IsHidden=true)>]
#else
        [<CompilerMessage("This construct is for use in the FSharp.Core library and should not be used directly", 1204, IsHidden=true, IsError=true)>]
#endif
        type In

        /// Represents a byref that can be both read and written
#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE
        [<CompilerMessage("This construct is for use in the FSharp.Core library and should not be used directly", 1204, IsHidden=true)>]
#else
        [<CompilerMessage("This construct is for use in the FSharp.Core library and should not be used directly", 1204, IsHidden=true, IsError=true)>]
#endif
        type InOut

    /// <summary>Represents a in-argument or readonly managed pointer in F# code. This type should only be used with F# 4.5+.</summary>
    /// <category>ByRef and Pointer Types</category>
    type inref<'T> = byref<'T, ByRefKinds.In>

    /// <summary>Represents a out-argument managed pointer in F# code. This type should only be used with F# 4.5+.</summary>
    /// <category>ByRef and Pointer Types</category>
    type outref<'T> = byref<'T, ByRefKinds.Out>

    /// <summary>Language primitives associated with the F# language</summary>
    ///
    /// <category index="9">Language Primitives</category>
    module LanguagePrimitives =

        /// <summary>Compare two values for equality using partial equivalence relation semantics ([nan] &lt;&gt; [nan])</summary>
        ///
        /// <param name="e1">The first value.</param>
        /// <param name="e2">The second value.</param>
        ///
        /// <returns>The result of the comparison.</returns>
        val inline GenericEquality : e1:'T -> e2:'T -> bool when 'T : equality
        
        /// <summary>Compare two values for equality using equivalence relation semantics ([nan] = [nan])</summary>
        ///
        /// <param name="e1">The first value.</param>
        /// <param name="e2">The second value.</param>
        ///
        /// <returns>The result of the comparison.</returns>
        val inline GenericEqualityER : e1:'T -> e2:'T -> bool when 'T : equality
        
        /// <summary>Compare two values for equality</summary>
        ///
        /// <param name="comp"></param>
        /// <param name="e1">The first value.</param>
        /// <param name="e2">The second value.</param>
        ///
        /// <returns>The result of the comparison.</returns>
        val inline GenericEqualityWithComparer : comp:System.Collections.IEqualityComparer -> e1:'T -> e2:'T -> bool when 'T : equality

        /// <summary>Compare two values </summary>
        ///
        /// <param name="e1">The first value.</param>
        /// <param name="e2">The second value.</param>
        ///
        /// <returns>The result of the comparison.</returns>
        val inline GenericComparison : e1:'T -> e2:'T -> int when 'T : comparison 

        /// <summary>Compare two values. May be called as a recursive case from an implementation of System.IComparable to
        /// ensure consistent NaN comparison semantics.</summary>
        ///
        /// <param name="comp">The function to compare the values.</param>
        /// <param name="e1">The first value.</param>
        /// <param name="e2">The second value.</param>
        ///
        /// <returns>The result of the comparison.</returns>
        val inline GenericComparisonWithComparer : comp:System.Collections.IComparer -> e1:'T -> e2:'T -> int when 'T : comparison 

        /// <summary>Compare two values   </summary>
        ///
        /// <param name="e1">The first value.</param>
        /// <param name="e2">The second value.</param>
        ///
        /// <returns>The result of the comparison.</returns>
        val inline GenericLessThan : e1:'T -> e2:'T -> bool when 'T : comparison 

        /// <summary>Compare two values   </summary>
        ///
        /// <param name="e1">The first value.</param>
        /// <param name="e2">The second value.</param>
        ///
        /// <returns>The result of the comparison.</returns>
        val inline GenericGreaterThan : e1:'T -> e2:'T -> bool when 'T : comparison 

        /// <summary>Compare two values   </summary>
        ///
        /// <param name="e1">The first value.</param>
        /// <param name="e2">The second value.</param>
        ///
        /// <returns>The result of the comparison.</returns>
        val inline GenericLessOrEqual : e1:'T -> e2:'T -> bool when 'T : comparison 

        /// <summary>Compare two values   </summary>
        ///
        /// <param name="e1">The first value.</param>
        /// <param name="e2">The second value.</param>
        ///
        /// <returns>The result of the comparison.</returns>
        val inline GenericGreaterOrEqual : e1:'T -> e2:'T -> bool when 'T : comparison 

        /// <summary>Take the minimum of two values structurally according to the order given by GenericComparison</summary>
        ///
        /// <param name="e1">The first value.</param>
        /// <param name="e2">The second value.</param>
        ///
        /// <returns>The minimum value.</returns>
        val inline GenericMinimum : e1:'T -> e2:'T -> 'T when 'T : comparison 

        /// <summary>Take the maximum of two values structurally according to the order given by GenericComparison</summary>
        ///
        /// <param name="e1">The first value.</param>
        /// <param name="e2">The second value.</param>
        ///
        /// <returns>The maximum value.</returns>
        val inline GenericMaximum : e1:'T -> e2:'T -> 'T when 'T : comparison 

        /// <summary>Reference/physical equality. 
        /// True if the inputs are reference-equal, false otherwise.</summary>
        ///
        /// <param name="e1">The first value.</param>
        /// <param name="e2">The second value.</param>
        ///
        /// <returns>The result of the comparison.</returns>
        val inline PhysicalEquality : e1:'T -> e2:'T -> bool when 'T : not struct

        /// <summary>The physical hash. Hashes on the object identity, except for value types,
        /// where we hash on the contents.</summary>
        ///
        /// <param name="obj">The input object.</param>
        ///
        /// <returns>The hashed value.</returns>
        val inline PhysicalHash : obj:'T -> int when 'T : not struct
        
        /// <summary>Return an F# comparer object suitable for hashing and equality. This hashing behaviour
        /// of the returned comparer is not limited by an overall node count when hashing F#
        /// records, lists and union types.</summary>
        val GenericEqualityComparer : System.Collections.IEqualityComparer
        
        /// <summary>Return an F# comparer object suitable for hashing and equality. This hashing behaviour
        /// of the returned comparer is not limited by an overall node count when hashing F#
        /// records, lists and union types. This equality comparer has equivalence 
        /// relation semantics ([nan] = [nan]).</summary>
        val GenericEqualityERComparer : System.Collections.IEqualityComparer

        /// <summary>A static F# comparer object</summary>
        val GenericComparer : System.Collections.IComparer

        /// <summary>Make an F# comparer object for the given type</summary>
        val inline FastGenericComparer<'T>  : System.Collections.Generic.IComparer<'T> when 'T : comparison 

        /// <summary>Make an F# comparer object for the given type, where it can be null if System.Collections.Generic.Comparer&lt;'T&gt;.Default</summary>
        val internal FastGenericComparerCanBeNull<'T>  : System.Collections.Generic.IComparer<'T> when 'T : comparison 

        /// <summary>Make an F# hash/equality object for the given type</summary>
        val inline FastGenericEqualityComparer<'T> : System.Collections.Generic.IEqualityComparer<'T> when 'T : equality

        /// <summary>Make an F# hash/equality object for the given type using node-limited hashing when hashing F#
        /// records, lists and union types.</summary>
        ///
        /// <param name="limit">The input limit on the number of nodes.</param>
        ///
        /// <returns>System.Collections.Generic.IEqualityComparer&lt;'T&gt;</returns>
        val inline FastLimitedGenericEqualityComparer<'T> : limit: int -> System.Collections.Generic.IEqualityComparer<'T> when 'T : equality

        /// <summary>Make an F# hash/equality object for the given type</summary>
        [<CompilerMessage("This function is a compiler intrinsic should not be used directly", 1204, IsHidden=true)>]
        val FastGenericEqualityComparerFromTable<'T> : System.Collections.Generic.IEqualityComparer<'T> when 'T : equality

        [<CompilerMessage("This function is a compiler intrinsic should not be used directly", 1204, IsHidden=true)>]
        /// <summary>Make an F# comparer object for the given type</summary>
        val FastGenericComparerFromTable<'T>  : System.Collections.Generic.IComparer<'T> when 'T : comparison 

        /// <summary>Hash a value according to its structure. This hash is not limited by an overall node count when hashing F#
        /// records, lists and union types.</summary>
        ///
        /// <param name="obj">The input object.</param>
        ///
        /// <returns>The hashed value.</returns>
        val inline GenericHash : obj:'T -> int
        
        /// <summary>Hash a value according to its structure. Use the given limit to restrict the hash when hashing F#
        /// records, lists and union types.</summary>
        ///
        /// <param name="limit">The limit on the number of nodes.</param>
        /// <param name="obj">The input object.</param>
        ///
        /// <returns>The hashed value.</returns>
        val inline GenericLimitedHash : limit: int -> obj:'T -> int
        
        /// <summary>Recursively hash a part of a value according to its structure. </summary>
        ///
        /// <param name="comparer">The comparison function.</param>
        /// <param name="obj">The input object.</param>
        ///
        /// <returns>The hashed value.</returns>
        val inline GenericHashWithComparer : comparer : System.Collections.IEqualityComparer -> obj:'T -> int

        /// <summary>Build an enum value from an underlying value</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The value as an enumeration.</returns>
        val inline EnumOfValue : value:'T -> 'Enum when 'Enum : enum<'T>

        /// <summary>Get the underlying value for an enum value</summary>
        ///
        /// <param name="enum">The input enum.</param>
        ///
        /// <returns>The enumeration as a value.</returns>
        val inline EnumToValue : enum:'Enum -> 'T when 'Enum : enum<'T>

        /// <summary>Creates a float value with units-of-measure</summary>
        ///
        /// <param name="input">The input float.</param>
        ///
        /// <returns>The float with units-of-measure.</returns>
        val inline FloatWithMeasure : input: float -> float<'Measure>

        /// <summary>Creates a float32 value with units-of-measure</summary>
        ///
        /// <param name="input">The input float.</param>
        ///
        /// <returns>The float with units-of-measure.</returns>
        val inline Float32WithMeasure : input: float32 -> float32<'Measure>

        /// <summary>Creates a decimal value with units-of-measure</summary>
        ///
        /// <param name="input">The input decimal.</param>
        ///
        /// <returns>The decimal with units of measure.</returns>
        val inline DecimalWithMeasure : input: decimal -> decimal<'Measure>

        /// <summary>Creates an int32 value with units-of-measure</summary>
        ///
        /// <param name="input">The input int.</param>
        ///
        /// <returns>The int with units of measure.</returns>
        val inline Int32WithMeasure : input: int -> int<'Measure>

        /// <summary>Creates an int64 value with units-of-measure</summary>
        ///
        /// <param name="input">The input int64.</param>
        ///
        /// <returns>The int64 with units of measure.</returns>
        val inline Int64WithMeasure : input: int64 -> int64<'Measure>

        /// <summary>Creates an int16 value with units-of-measure</summary>
        ///
        /// <param name="input">The input int16.</param>
        ///
        /// <returns>The int16 with units-of-measure.</returns>
        val inline Int16WithMeasure : input: int16 -> int16<'Measure>

        /// <summary>Creates an sbyte value with units-of-measure</summary>
        ///
        /// <param name="input">The input sbyte.</param>
        ///
        /// <returns>The sbyte with units-of-measure.</returns>
        val inline SByteWithMeasure : input: sbyte -> sbyte<'Measure>

        [<Experimental("Experimental library feature, requires '--langversion:preview'")>] 
        /// <summary>Creates a nativeint value with units-of-measure</summary>
        ///
        /// <param name="input">The input nativeint.</param>
        ///
        /// <returns>The nativeint with units-of-measure.</returns>
        val inline IntPtrWithMeasure : input: nativeint -> nativeint<'Measure>

        [<Experimental("Experimental library feature, requires '--langversion:preview'")>] 
        /// <summary>Creates a uint value with units-of-measure</summary>
        ///
        /// <param name="input">The input uint.</param>
        ///
        /// <returns>The uint with units-of-measure.</returns>
        val inline UInt32WithMeasure : input: uint -> uint<'Measure>
        
        [<Experimental("Experimental library feature, requires '--langversion:preview'")>] 
        /// <summary>Creates a uint64 value with units-of-measure</summary>
        ///
        /// <param name="input">The input uint64.</param>
        ///
        /// <returns>The uint64 with units-of-measure.</returns>
        val inline UInt64WithMeasure : input: uint64 -> uint64<'Measure>
        
        [<Experimental("Experimental library feature, requires '--langversion:preview'")>] 
        /// <summary>Creates a uint16 value with units-of-measure</summary>
        ///
        /// <param name="input">The input uint16.</param>
        ///
        /// <returns>The uint16 with units-of-measure.</returns>
        val inline UInt16WithMeasure : input: uint16 -> uint16<'Measure>
        
        [<Experimental("Experimental library feature, requires '--langversion:preview'")>] 
        /// <summary>Creates a byte value with units-of-measure</summary>
        ///
        /// <param name="input">The input byte.</param>
        ///
        /// <returns>The byte with units-of-measure.</returns>
        val inline ByteWithMeasure : input: byte -> byte<'Measure>
        
        [<Experimental("Experimental library feature, requires '--langversion:preview'")>] 
        /// <summary>Creates a unativeint value with units-of-measure</summary>
        ///
        /// <param name="input">The input unativeint.</param>
        ///
        /// <returns>The unativeint with units-of-measure.</returns>
        val inline UIntPtrWithMeasure : input: unativeint -> unativeint<'Measure>

        /// <summary>Parse an int32 according to the rules used by the overloaded 'int32' conversion operator when applied to strings</summary>
        ///
        /// <param name="s">The input string.</param>
        ///
        /// <returns>The parsed value.</returns>
        val ParseInt32 : s: string -> int32

        /// <summary>Parse an uint32 according to the rules used by the overloaded 'uint32' conversion operator when applied to strings</summary>
        ///
        /// <param name="s">The input string.</param>
        ///
        /// <returns>The parsed value.</returns>
        val ParseUInt32 : s:string -> uint32

        /// <summary>Parse an int64 according to the rules used by the overloaded 'int64' conversion operator when applied to strings</summary>
        ///
        /// <param name="s">The input string.</param>
        ///
        /// <returns>The parsed value.</returns>
        val ParseInt64 : s:string -> int64

        /// <summary>Parse an uint64 according to the rules used by the overloaded 'uint64' conversion operator when applied to strings</summary>
        ///
        /// <param name="s">The input string.</param>
        ///
        /// <returns>The parsed value.</returns>
        val ParseUInt64 : s:string -> uint64

        /// <summary>Resolves to the zero value for any primitive numeric type or any type with a static member called 'Zero'.</summary>
        [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
        val GenericZeroDynamic : unit -> 'T 

        /// <summary>Resolves to the value 'one' for any primitive numeric type or any type with a static member called 'One'.</summary>
        [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
        val GenericOneDynamic : unit -> 'T 

        /// <summary>A compiler intrinsic that implements dynamic invocations to the '+' operator.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val AdditionDynamic : x:'T1 -> y:'T2 -> 'U

        /// <summary>A compiler intrinsic that implements dynamic invocations to the checked '+' operator.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val CheckedAdditionDynamic : x:'T1 -> y:'T2 -> 'U

        /// <summary>A compiler intrinsic that implements dynamic invocations to the '*' operator.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val MultiplyDynamic : x:'T1 -> y:'T2 -> 'U

        /// <summary>A compiler intrinsic that implements dynamic invocations to the checked '*' operator.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val CheckedMultiplyDynamic : x:'T1 -> y:'T2 -> 'U

        /// <summary>A compiler intrinsic that implements dynamic invocations to the '-' operator.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val SubtractionDynamic : x:'T1 -> y:'T2 -> 'U

        /// <summary>A compiler intrinsic that implements dynamic invocations to the '/' operator.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val DivisionDynamic : x:'T1 -> y:'T2 -> 'U

        /// <summary>A compiler intrinsic that implements dynamic invocations to the unary '-' operator.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val UnaryNegationDynamic : value:'T -> 'U

        /// <summary>A compiler intrinsic that implements dynamic invocations to the '%' operator.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val ModulusDynamic : x:'T1 -> y:'T2 -> 'U

        /// <summary>A compiler intrinsic that implements dynamic invocations to the checked '-' operator.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val CheckedSubtractionDynamic : x:'T1 -> y:'T2 -> 'U

        /// <summary>A compiler intrinsic that implements dynamic invocations to the checked unary '-' operator.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val CheckedUnaryNegationDynamic : value:'T -> 'U

        /// <summary>A compiler intrinsic that implements dynamic invocations to the '&lt;&lt;&lt;' operator.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val LeftShiftDynamic : value:'T1 -> shift:'T2 -> 'U

        /// <summary>A compiler intrinsic that implements dynamic invocations to the '&gt;&gt;&gt;' operator.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val RightShiftDynamic : value:'T1 -> shift:'T2 -> 'U

        /// <summary>A compiler intrinsic that implements dynamic invocations to the '&amp;&amp;&amp;' operator.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val BitwiseAndDynamic : x:'T1 -> y:'T2 -> 'U

        /// <summary>A compiler intrinsic that implements dynamic invocations to the '|||' operator.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val BitwiseOrDynamic : x:'T1 -> y:'T2 -> 'U

        /// <summary>A compiler intrinsic that implements dynamic invocations related to the '^^^' operator.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val ExclusiveOrDynamic : x:'T1 -> y:'T2 -> 'U

        /// <summary>A compiler intrinsic that implements dynamic invocations related to the '~~~' operator.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val LogicalNotDynamic : value:'T -> 'U

        /// <summary>A compiler intrinsic that implements dynamic invocations related to conversion operators.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val ExplicitDynamic : value:'T -> 'U

        /// <summary>A compiler intrinsic that implements dynamic invocations related to the '&lt;' operator.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val LessThanDynamic : x:'T1 -> y:'T2 -> 'U

        /// <summary>A compiler intrinsic that implements dynamic invocations related to the '&gt;' operator.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val GreaterThanDynamic : x:'T1 -> y:'T2 -> 'U

        /// <summary>A compiler intrinsic that implements dynamic invocations related to the '&lt;=' operator.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val LessThanOrEqualDynamic : x:'T1 -> y:'T2 -> 'U

        /// <summary>A compiler intrinsic that implements dynamic invocations related to the '&gt;=' operator.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val GreaterThanOrEqualDynamic : x:'T1 -> y:'T2 -> 'U

        /// <summary>A compiler intrinsic that implements dynamic invocations related to the '=' operator.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val EqualityDynamic : x:'T1 -> y:'T2 -> 'U

        /// <summary>A compiler intrinsic that implements dynamic invocations related to the '=' operator.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val InequalityDynamic : x:'T1 -> y:'T2 -> 'U

        /// <summary>A compiler intrinsic that implements dynamic invocations for the DivideByInt primitive.</summary>
        [<CompilerMessage("This function is for use by dynamic invocations of F# code and should not be used directly", 1204, IsHidden=true)>]
        val DivideByIntDynamic : x:'T -> y:int -> 'T

        /// <summary>Resolves to the zero value for any primitive numeric type or any type with a static member called 'Zero'</summary>
        val inline GenericZero< ^T > : ^T when ^T : (static member Zero : ^T) 

        /// <summary>Resolves to the value 'one' for any primitive numeric type or any type with a static member called 'One'</summary>
        val inline GenericOne< ^T > : ^T when ^T : (static member One : ^T) 
        
        val internal anyToStringShowingNull : 'T -> string

        /// <summary>Divides a value by an integer.</summary>
        ///
        /// <param name="x">The input value.</param>
        /// <param name="y">The input int.</param>
        ///
        /// <returns>The division result.</returns>
        val inline DivideByInt< ^T >  : x:^T -> y:int -> ^T when ^T : (static member DivideByInt : ^T * int -> ^T) 

        /// <summary>For compiler use only</summary>
        module (* internal *) ErrorStrings = 

            [<CompilerMessage("This value is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val InputSequenceEmptyString : string

            [<CompilerMessage("This value is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val InputArrayEmptyString : string
        
            [<CompilerMessage("This value is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val AddressOpNotFirstClassString : string

            [<CompilerMessage("This value is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val NoNegateMinValueString : string
                
            [<CompilerMessage("This value is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val InputMustBeNonNegativeString : string
                

        //-------------------------------------------------------------------------

        /// <summary>The F# compiler emits calls to some of the functions in this module as part of the compiled form of some language constructs</summary>
        module IntrinsicOperators = 

            /// <summary>Binary 'and'. When used as a binary operator the right hand value is evaluated only on demand.</summary>
            [<CompilerMessage("In F# code, use 'e1 && e2' instead of 'e1 & e2'", 1203, IsHidden=true)>]
            val ( & ) : e1:bool -> e2:bool -> bool

            /// <summary>Binary 'and'. When used as a binary operator the right hand value is evaluated only on demand</summary>
            ///
            /// <param name="e1">The first value.</param>
            /// <param name="e2">The second value.</param>
            ///
            /// <returns>The result of the operation.</returns>
            val ( && ) : e1:bool -> e2:bool -> bool

            /// <summary>Binary 'or'. When used as a binary operator the right hand value is evaluated only on demand.</summary>
            [<CompiledName("Or")>]
            [<CompilerMessage("In F# code, use 'e1 || e2' instead of 'e1 or e2'", 1203, IsHidden=true)>]
            val ( or ) : e1:bool -> e2:bool -> bool

            /// <summary>Binary 'or'. When used as a binary operator the right hand value is evaluated only on demand</summary>
            ///
            /// <param name="e1">The first value.</param>
            /// <param name="e2">The second value.</param>
            ///
            /// <returns>The result of the operation.</returns>
            val ( || ) : e1:bool -> e2:bool -> bool

            /// <summary>Address-of. Uses of this value may result in the generation of unverifiable code.</summary>
            ///
            /// <param name="obj">The input object.</param>
            ///
            /// <returns>The managed pointer.</returns>
            val inline ( ~& ) : obj:'T -> byref<'T>

            /// <summary>Address-of. Uses of this value may result in the generation of unverifiable code.</summary>
            ///
            /// <param name="obj">The input object.</param>
            ///
            /// <returns>The unmanaged pointer.</returns>
            val inline ( ~&& ) : obj:'T -> nativeptr<'T>

        //-------------------------------------------------------------------------

        /// <summary>The F# compiler emits calls to some of the functions in this module as part of the compiled form of some language constructs</summary>
        module IntrinsicFunctions = 

            /// <summary>A compiler intrinsic that implements the ':?>' operator</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val UnboxGeneric<'T> : source:obj -> 'T

            /// <summary>A compiler intrinsic that implements the ':?>' operator</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline UnboxFast<'T> : source:obj -> 'T

            /// <summary>A compiler intrinsic that implements the ':?' operator</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val TypeTestGeneric<'T> : source:obj -> bool

            /// <summary>A compiler intrinsic that implements the ':?' operator</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline TypeTestFast<'T> : source:obj -> bool 

            /// <summary>Primitive used by pattern match compilation</summary>
            //[<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline GetString : source:string -> index:int -> char

            /// <summary>This function implements calls to default constructors
            /// accessed by 'new' constraints.</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline CreateInstance : unit -> 'T when 'T : (new : unit -> 'T)

            /// <summary>This function implements parsing of decimal constants</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline MakeDecimal : low:int -> medium:int -> high:int -> isNegative:bool -> scale:byte -> decimal

            /// <summary>A compiler intrinsic for the efficient compilation of sequence expressions</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val Dispose<'T when 'T :> System.IDisposable > : resource:'T -> unit

            /// <summary>A compiler intrinsic for checking initialization soundness of recursive bindings</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val FailInit : unit -> unit

            /// <summary>A compiler intrinsic for checking initialization soundness of recursive static bindings</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val FailStaticInit : unit -> unit

            /// <summary>A compiler intrinsic for checking initialization soundness of recursive bindings</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val CheckThis : 'T -> 'T when 'T : not struct

            /// <summary>The standard overloaded associative (indexed) lookup operator</summary>
            //[<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline GetArray     : source:'T[] -> index:int -> 'T                           
            
            /// <summary>The standard overloaded associative (2-indexed) lookup operator</summary>
            //[<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline GetArray2D    : source:'T[,] -> index1:int -> index2:int -> 'T    
            
            /// <summary>The standard overloaded associative (3-indexed) lookup operator</summary>
            //[<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline GetArray3D   : source:'T[,,] ->index1:int -> index2:int -> index3:int -> 'T  
            
            /// <summary>The standard overloaded associative (4-indexed) lookup operator</summary>
            //[<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline GetArray4D   : source:'T[,,,] ->index1:int -> index2:int -> index3:int -> index4:int -> 'T
            
            /// <summary>The standard overloaded associative (indexed) mutation operator</summary>
            //[<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline SetArray   : target:'T[] -> index:int -> value:'T -> unit      
            
            /// <summary>The standard overloaded associative (2-indexed) mutation operator</summary>
            //[<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline SetArray2D  : target:'T[,] -> index1:int -> index2:int -> value:'T -> unit    
            
            /// <summary>The standard overloaded associative (3-indexed) mutation operator</summary>
            //[<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline SetArray3D : target:'T[,,] -> index1:int -> index2:int -> index3:int -> value:'T -> unit  

            /// The standard overloaded associative (4-indexed) mutation operator
            //[<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline SetArray4D : target:'T[,,,] -> index1:int -> index2:int -> index3:int -> index4:int -> value:'T -> unit  

        /// <summary>The F# compiler emits calls to some of the functions in this module as part of the compiled form of some language constructs</summary>
        module HashCompare =
            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val PhysicalHashIntrinsic : input:'T -> int when 'T : not struct

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary>
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val PhysicalEqualityIntrinsic : x:'T -> y:'T -> bool when 'T : not struct

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val GenericHashIntrinsic : input:'T -> int

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val LimitedGenericHashIntrinsic : limit: int -> input:'T -> int

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val GenericHashWithComparerIntrinsic : comp:System.Collections.IEqualityComparer -> input:'T -> int           

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val GenericComparisonWithComparerIntrinsic : comp:System.Collections.IComparer -> x:'T -> y:'T -> int

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val GenericComparisonIntrinsic : x:'T -> y:'T -> int

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val GenericEqualityIntrinsic : x:'T -> y:'T -> bool

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val GenericEqualityERIntrinsic : x:'T -> y:'T -> bool
            
            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val GenericEqualityWithComparerIntrinsic : comp:System.Collections.IEqualityComparer -> x:'T -> y:'T -> bool

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary>
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val GenericLessThanIntrinsic : x:'T -> y:'T -> bool

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val GenericGreaterThanIntrinsic : x:'T -> y:'T -> bool

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val GenericGreaterOrEqualIntrinsic : x:'T -> y:'T -> bool

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val GenericLessOrEqualIntrinsic : x:'T -> y:'T -> bool

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline FastHashTuple2 : comparer:System.Collections.IEqualityComparer -> tuple:('T1 * 'T2) -> int

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline FastHashTuple3 : comparer:System.Collections.IEqualityComparer -> tuple:('T1 * 'T2 * 'T3) -> int

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline FastHashTuple4 : comparer:System.Collections.IEqualityComparer -> tuple:('T1 * 'T2 * 'T3 * 'T4) -> int

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline FastHashTuple5 : comparer:System.Collections.IEqualityComparer -> tuple:('T1 * 'T2 * 'T3 * 'T4 * 'T5) -> int

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline FastEqualsTuple2 : comparer:System.Collections.IEqualityComparer -> tuple1:('T1 * 'T2) -> tuple2:('T1 * 'T2) -> bool

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline FastEqualsTuple3 : comparer:System.Collections.IEqualityComparer -> tuple1:('T1 * 'T2 * 'T3) -> tuple2:('T1 * 'T2 * 'T3) -> bool

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline FastEqualsTuple4 : comparer:System.Collections.IEqualityComparer -> tuple1:('T1 * 'T2 * 'T3 * 'T4) -> tuple2:('T1 * 'T2 * 'T3 * 'T4) -> bool

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline FastEqualsTuple5 : comparer:System.Collections.IEqualityComparer -> tuple1:('T1 * 'T2 * 'T3 * 'T4 * 'T5) -> tuple2:('T1 * 'T2 * 'T3 * 'T4 * 'T5) -> bool

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline FastCompareTuple2 : comparer:System.Collections.IComparer -> tuple1:('T1 * 'T2) -> tuple2:('T1 * 'T2) -> int

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline FastCompareTuple3 : comparer:System.Collections.IComparer -> tuple1:('T1 * 'T2 * 'T3) -> tuple2:('T1 * 'T2 * 'T3) -> int

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline FastCompareTuple4 : comparer:System.Collections.IComparer -> tuple1:('T1 * 'T2 * 'T3 * 'T4) -> tuple2:('T1 * 'T2 * 'T3 * 'T4) -> int

            /// <summary>A primitive entry point used by the F# compiler for optimization purposes.</summary> 
            [<CompilerMessage("This function is a primitive library routine used by optimized F# code and should not be used directly", 1204, IsHidden=true)>]
            val inline FastCompareTuple5 : comparer:System.Collections.IComparer -> tuple1:('T1 * 'T2 * 'T3 * 'T4 * 'T5) -> tuple2:('T1 * 'T2 * 'T3 * 'T4 * 'T5) -> int

    //-------------------------------------------------------------------------
    // F# Choice Types


    /// <summary>Helper types for active patterns with 2 choices.</summary>
    /// <category index="5">Choices and Results</category>
    [<StructuralEquality; StructuralComparison>]
    [<CompiledName("FSharpChoice`2")>]
    type Choice<'T1,'T2> = 

      /// <summary>Choice 1 of 2 choices</summary>
      | Choice1Of2 of 'T1 

      /// <summary>Choice 2 of 2 choices</summary>
      | Choice2Of2 of 'T2
    
    /// <summary>Helper types for active patterns with 3 choices.</summary>
    /// <category>Choices and Results</category>
    [<StructuralEquality; StructuralComparison>]
    [<CompiledName("FSharpChoice`3")>]
    type Choice<'T1,'T2,'T3> = 

      /// <summary>Choice 1 of 3 choices</summary>
      | Choice1Of3 of 'T1 

      /// <summary>Choice 2 of 3 choices</summary>
      | Choice2Of3 of 'T2

      /// <summary>Choice 3 of 3 choices</summary>
      | Choice3Of3 of 'T3
    
    /// <summary>Helper types for active patterns with 4 choices.</summary>
    /// <category>Choices and Results</category>
    [<StructuralEquality; StructuralComparison>]
    [<CompiledName("FSharpChoice`4")>]
    type Choice<'T1,'T2,'T3,'T4> = 

      /// <summary>Choice 1 of 4 choices</summary>
      | Choice1Of4 of 'T1 

      /// <summary>Choice 2 of 4 choices</summary>
      | Choice2Of4 of 'T2

      /// <summary>Choice 3 of 4 choices</summary>
      | Choice3Of4 of 'T3

      /// <summary>Choice 4 of 4 choices</summary>
      | Choice4Of4 of 'T4
    
    /// <summary>Helper types for active patterns with 5 choices.</summary>
    /// <category>Choices and Results</category>
    [<StructuralEquality; StructuralComparison>]
    [<CompiledName("FSharpChoice`5")>]
    type Choice<'T1,'T2,'T3,'T4,'T5> = 

      /// <summary>Choice 1 of 5 choices</summary>
      | Choice1Of5 of 'T1 

      /// <summary>Choice 2 of 5 choices</summary>
      | Choice2Of5 of 'T2

      /// <summary>Choice 3 of 5 choices</summary>
      | Choice3Of5 of 'T3

      /// <summary>Choice 4 of 5 choices</summary>
      | Choice4Of5 of 'T4

      /// <summary>Choice 5 of 5 choices</summary>
      | Choice5Of5 of 'T5
    
    /// <summary>Helper types for active patterns with 6 choices.</summary>
    /// <category>Choices and Results</category>
    [<StructuralEquality; StructuralComparison>]
    [<CompiledName("FSharpChoice`6")>]
    type Choice<'T1,'T2,'T3,'T4,'T5,'T6> = 

      /// <summary>Choice 1 of 6 choices</summary>
      | Choice1Of6 of 'T1 

      /// <summary>Choice 2 of 6 choices</summary>
      | Choice2Of6 of 'T2

      /// <summary>Choice 3 of 6 choices</summary>
      | Choice3Of6 of 'T3

      /// <summary>Choice 4 of 6 choices</summary>
      | Choice4Of6 of 'T4

      /// <summary>Choice 5 of 6 choices</summary>
      | Choice5Of6 of 'T5

      /// <summary>Choice 6 of 6 choices</summary>
      | Choice6Of6 of 'T6
    
    /// <summary>Helper types for active patterns with 7 choices.</summary>
    /// <category>Choices and Results</category>
    [<StructuralEquality; StructuralComparison>]
    [<CompiledName("FSharpChoice`7")>]
    type Choice<'T1,'T2,'T3,'T4,'T5,'T6,'T7> = 

      /// <summary>Choice 1 of 7 choices</summary>
      | Choice1Of7 of 'T1 

      /// <summary>Choice 2 of 7 choices</summary>
      | Choice2Of7 of 'T2

      /// <summary>Choice 3 of 7 choices</summary>
      | Choice3Of7 of 'T3

      /// <summary>Choice 4 of 7 choices</summary>
      | Choice4Of7 of 'T4

      /// <summary>Choice 5 of 7 choices</summary>
      | Choice5Of7 of 'T5

      /// <summary>Choice 6 of 7 choices</summary>
      | Choice6Of7 of 'T6

      /// <summary>Choice 7 of 7 choices</summary>
      | Choice7Of7 of 'T7
    
    /// <summary>Non-exhaustive match failures will raise the MatchFailureException exception</summary>
    /// <category>Language Primitives</category>
    [<StructuralEquality; NoComparison>]
    exception MatchFailureException of string * int * int

    /// <summary>The CLI type used to represent F# first-class type function values. This type is for use
    /// by compiled F# code.</summary>
    /// <category>Language Primitives</category>
    [<AbstractClass>]
    type FSharpTypeFunc =

        /// <summary>Specialize the type function at a given type</summary>
        /// <returns>The specialized type.</returns>
        abstract Specialize<'T> : unit -> obj

        /// <summary>Construct an instance of an F# first class type function value </summary>
        /// <returns>FSharpTypeFunc</returns>
        new : unit -> FSharpTypeFunc

    /// <summary>The CLI type used to represent F# function values. This type is not
    /// typically used directly, though may be used from other CLI languages.</summary>
    /// <category>Language Primitives</category>
    [<AbstractClass>]
    type FSharpFunc<'T,'U> = 

        /// <summary>Construct an instance of an F# first class function value </summary> 
        /// <returns>The created F# function.</returns> 
        new : unit ->  FSharpFunc<'T,'U>
 
        /// <summary>Invoke an F# first class function value with one argument</summary>
        ///
        /// <param name="func"></param>
        ///
        /// <returns>'U</returns>
        abstract member Invoke : func:'T -> 'U

        /// <summary>Convert an F# first class function value to a value of type <see cref="T:System.Converter"/></summary>
        ///
        /// <param name="func">The input function.</param>
        ///
        /// <returns>A System.Converter of the function type.</returns>
        static member op_Implicit : func:('T -> 'U) -> System.Converter<'T,'U>

        /// <summary>Convert an value of type <see cref="T:System.Converter"/> to a F# first class function value </summary>
        ///
        /// <param name="converter">The input System.Converter.</param>
        ///
        /// <returns>An F# function of the same type.</returns>
        static member op_Implicit : converter:System.Converter<'T,'U> -> ('T -> 'U)

        /// <summary>Convert an F# first class function value to a value of type <see cref="T:System.Converter"/></summary>
        ///
        /// <param name="func">The input function.</param>
        ///
        /// <returns>System.Converter&lt;'T,'U&gt;</returns>
        static member ToConverter : func:('T -> 'U) -> System.Converter<'T,'U>

        /// <summary>Convert an value of type <see cref="T:System.Converter"/> to a F# first class function value </summary>
        ///
        /// <param name="converter">The input System.Converter.</param>
        ///
        /// <returns>An F# function of the same type.</returns>
        static member FromConverter : converter:System.Converter<'T,'U> -> ('T -> 'U)

        /// <summary>Invoke an F# first class function value with five curried arguments. In some cases this
        /// will result in a more efficient application than applying the arguments successively.</summary>
        ///
        /// <param name="func">The input function.</param>
        /// <param name="arg1">The first arg.</param>
        /// <param name="arg2">The second arg.</param>
        /// <param name="arg3">The third arg.</param>
        /// <param name="arg4">The fourth arg.</param>
        /// <param name="arg5">The fifth arg.</param>
        ///
        /// <returns>The function result.</returns>
        static member InvokeFast : func: FSharpFunc<'T,('U -> 'V -> 'W -> 'X -> 'Y)> * arg1:'T * arg2:'U * arg3:'V * arg4:'W * arg5:'X -> 'Y

        /// <summary>Invoke an F# first class function value with four curried arguments. In some cases this
        /// will result in a more efficient application than applying the arguments successively.</summary>
        ///
        /// <param name="func">The input function.</param>
        /// <param name="arg1">The first arg.</param>
        /// <param name="arg2">The second arg.</param>
        /// <param name="arg3">The third arg.</param>
        /// <param name="arg4">The fourth arg.</param>
        ///
        /// <returns>The function result.</returns>
        static member InvokeFast : func: FSharpFunc<'T,('U -> 'V -> 'W -> 'X)> * arg1:'T * arg2:'U * arg3:'V * arg4:'W -> 'X

        /// <summary>Invoke an F# first class function value with three curried arguments. In some cases this
        /// will result in a more efficient application than applying the arguments successively.</summary>
        ///
        /// <param name="func">The input function.</param>
        /// <param name="arg1">The first arg.</param>
        /// <param name="arg2">The second arg.</param>
        /// <param name="arg3">The third arg.</param>
        ///
        /// <returns>The function result.</returns>
        static member InvokeFast : func: FSharpFunc<'T,('U -> 'V -> 'W)> * arg1:'T * arg2:'U * arg3:'V -> 'W

        /// <summary>Invoke an F# first class function value with two curried arguments. In some cases this
        /// will result in a more efficient application than applying the arguments successively.</summary>
        ///
        /// <param name="func">The input function.</param>
        /// <param name="arg1">The first arg.</param>
        /// <param name="arg2">The second arg.</param>
        ///
        /// <returns>The function result.</returns>
        static member InvokeFast : func: FSharpFunc<'T,('U -> 'V)> * arg1:'T * arg2:'U -> 'V

    [<AbstractClass>]
    [<Sealed>]
    /// <summary>Helper functions for converting F# first class function values to and from CLI representations
    /// of functions using delegates.</summary>
    /// <category>Language Primitives</category>
    type FuncConvert = 

        /// <summary>Convert the given Action delegate object to an F# function value</summary>
        ///
        /// <param name="action">The input Action delegate.</param>
        ///
        /// <returns>The F# function.</returns>
        static member  inline ToFSharpFunc       : action:Action<'T>            -> ('T -> unit)

        /// <summary>Convert the given Converter delegate object to an F# function value</summary>
        ///
        /// <param name="converter">The input Converter delegate.</param>
        ///
        /// <returns>The F# function.</returns>
        static member  inline ToFSharpFunc       : converter:Converter<'T,'U>          -> ('T -> 'U)

        /// <summary>Convert the given Action delegate object to an F# function value</summary>
        ///
        /// <param name="action">The input Action delegate.</param>
        ///
        /// <returns>The F# function.</returns>
        static member  inline FromAction       : action:Action          -> (unit -> unit)

        /// <summary>Convert the given Action delegate object to an F# function value</summary>
        ///
        /// <param name="action">The input Action delegate.</param>
        ///
        /// <returns>The F# function.</returns>
        static member  inline FromAction       : action:Action<'T>          -> ('T -> unit)

        /// <summary>Convert the given Action delegate object to an F# function value</summary>
        ///
        /// <param name="action">The input Action delegate.</param>
        ///
        /// <returns>The F#funcfunction.</returns>
        static member  inline FromAction       : action:Action<'T1,'T2>          -> ('T1 -> 'T2 -> unit)

        /// <summary>Convert the given Action delegate object to an F# function value</summary>
        ///
        /// <param name="action">The input Action delegate.</param>
        ///
        /// <returns>The F# function.</returns>
        static member  inline FromAction       : action:Action<'T1,'T2,'T3>          -> ('T1 -> 'T2 -> 'T3 -> unit)

        /// <summary>Convert the given Action delegate object to an F# function value</summary>
        ///
        /// <param name="action">The input Action delegate.</param>
        ///
        /// <returns>The F# function.</returns>
        static member  inline FromAction       : action:Action<'T1,'T2,'T3,'T4>          -> ('T1 -> 'T2 -> 'T3 -> 'T4 -> unit)

        /// <summary>Convert the given Action delegate object to an F# function value</summary>
        ///
        /// <param name="action">The input Action delegate.</param>
        ///
        /// <returns>The F# function.</returns>
        static member  inline FromAction       : action:Action<'T1,'T2,'T3,'T4,'T5>          -> ('T1 -> 'T2 -> 'T3 -> 'T4 -> 'T5 -> unit)

        /// <summary>Convert the given Func delegate object to an F# function value</summary>
        ///
        /// <param name="func">The input Func delegate.</param>
        ///
        /// <returns>The F# function.</returns>
        static member  inline FromFunc       : func:Func<'T>          -> (unit -> 'T)

        /// <summary>Convert the given Func delegate object to an F# function value</summary>
        ///
        /// <param name="func">The input Func delegate.</param>
        ///
        /// <returns>The F# function.</returns>
        static member  inline FromFunc       : func:Func<'T,'U>          -> ('T -> 'U)

        /// <summary>Convert the given Func delegate object to an F# function value</summary>
        ///
        /// <param name="func">The input Func delegate.</param>
        ///
        /// <returns>The F#funcfunction.</returns>
        static member  inline FromFunc       : func:Func<'T1,'T2,'U>          -> ('T1 -> 'T2 -> 'U)

        /// <summary>Convert the given Func delegate object to an F# function value</summary>
        ///
        /// <param name="func">The input Func delegate.</param>
        ///
        /// <returns>The F# function.</returns>
        static member  inline FromFunc       : func:Func<'T1,'T2,'T3,'U>          -> ('T1 -> 'T2 -> 'T3 -> 'U)

        /// <summary>Convert the given Func delegate object to an F# function value</summary>
        ///
        /// <param name="func">The input Func delegate.</param>
        ///
        /// <returns>The F# function.</returns>
        static member  inline FromFunc       : func:Func<'T1,'T2,'T3,'T4,'U>          -> ('T1 -> 'T2 -> 'T3 -> 'T4 -> 'U)

        /// <summary>Convert the given Func delegate object to an F# function value</summary>
        ///
        /// <param name="func">The input Func delegate.</param>
        ///
        /// <returns>The F# function.</returns>
        static member  inline FromFunc       : func:Func<'T1,'T2,'T3,'T4,'T5,'U>          -> ('T1 -> 'T2 -> 'T3 -> 'T4 -> 'T5 -> 'U)

        /// <summary>A utility function to convert function values from tupled to curried form</summary>
        ///
        /// <param name="func">The input tupled function.</param>
        ///
        /// <returns>The output curried function.</returns>
        static member inline FuncFromTupled : func:('T1 * 'T2 -> 'U) -> ('T1 -> 'T2 -> 'U)
        
        /// <summary>A utility function to convert function values from tupled to curried form</summary>
        ///
        /// <param name="func">The input tupled function.</param>
        ///
        /// <returns>The output curried function.</returns>
        static member inline FuncFromTupled : func:('T1 * 'T2 * 'T3 -> 'U) -> ('T1 -> 'T2 -> 'T3 -> 'U)
        
        /// <summary>A utility function to convert function values from tupled to curried form</summary>
        ///
        /// <param name="func">The input tupled function.</param>
        ///
        /// <returns>The output curried function.</returns>
        static member inline FuncFromTupled : func:('T1 * 'T2 * 'T3 * 'T4 -> 'U) -> ('T1 -> 'T2 -> 'T3 -> 'T4 -> 'U)
        
        /// <summary>A utility function to convert function values from tupled to curried form</summary>
        ///
        /// <param name="func">The input tupled function.</param>
        ///
        /// <returns>The output curried function.</returns>
        static member inline FuncFromTupled : func:('T1 * 'T2 * 'T3 * 'T4 * 'T5 -> 'U) -> ('T1 -> 'T2 -> 'T3 -> 'T4 -> 'T5 -> 'U)

    /// <summary>An implementation module used to hold some private implementations of function
    /// value invocation.</summary>
    /// <category>Language Primitives</category>
    module OptimizedClosures =

        /// <summary>The CLI type used to represent F# function values that accept
        /// two iterated (curried) arguments without intervening execution. This type should not
        /// typically used directly from either F# code or from other CLI languages.</summary>
        [<AbstractClass>]
        type FSharpFunc<'T1,'T2,'U> = 
            inherit  FSharpFunc<'T1,('T2 -> 'U)>

            /// <summary>Invoke the optimized function value with two curried arguments </summary>
            ///
            /// <param name="arg1">The first arg.</param>
            /// <param name="arg2">The second arg.</param>
            ///
            /// <returns>The function result.</returns>
            abstract member Invoke : arg1:'T1 * arg2:'T2 -> 'U

            /// <summary>Adapt an F# first class function value to be an optimized function value that can 
            /// accept two curried arguments without intervening execution. </summary>
            ///
            /// <param name="func">The input function.</param>
            ///
            /// <returns>The adapted function.</returns>
            static member Adapt : func:('T1 -> 'T2 -> 'U) -> FSharpFunc<'T1,'T2,'U>

            /// <summary>Construct an optimized function value that can accept two curried 
            /// arguments without intervening execution.</summary>
            /// <returns>The optimized function.</returns>
            new : unit ->  FSharpFunc<'T1,'T2,'U>

        /// <summary>The CLI type used to represent F# function values that accept
        /// three iterated (curried) arguments without intervening execution. This type should not
        /// typically used directly from either F# code or from other CLI languages.</summary>
        [<AbstractClass>]
        type FSharpFunc<'T1,'T2,'T3,'U> = 

            inherit  FSharpFunc<'T1,('T2 -> 'T3 -> 'U)>

            /// <summary>Invoke an F# first class function value that accepts three curried arguments 
            /// without intervening execution</summary>
            ///
            /// <param name="arg1">The first arg.</param>
            /// <param name="arg2">The second arg.</param>
            /// <param name="arg3">The third arg.</param>
            ///
            /// <returns>The function result.</returns>
            abstract member Invoke : arg1:'T1 * arg2:'T2 * arg3:'T3 -> 'U

            /// <summary>Adapt an F# first class function value to be an optimized function value that can 
            /// accept three curried arguments without intervening execution. </summary>
            ///
            /// <param name="func">The input function.</param>
            ///
            /// <returns>The adapted function.</returns>
            static member Adapt : func:('T1 -> 'T2 -> 'T3 -> 'U) -> FSharpFunc<'T1,'T2,'T3,'U>

            /// <summary>Construct an optimized function value that can accept three curried 
            /// arguments without intervening execution.</summary>
            /// <returns>The optimized function.</returns>
            new : unit ->  FSharpFunc<'T1,'T2,'T3,'U>

        /// <summary>The CLI type used to represent F# function values that accept four curried arguments 
        /// without intervening execution. This type should not typically used directly from 
        /// either F# code or from other CLI languages.</summary>
        [<AbstractClass>]
        type FSharpFunc<'T1,'T2,'T3,'T4,'U> = 
            inherit  FSharpFunc<'T1,('T2 -> 'T3 -> 'T4 -> 'U)>

            /// <summary>Invoke an F# first class function value that accepts four curried arguments 
            /// without intervening execution</summary>
            ///
            /// <param name="arg1">The first arg.</param>
            /// <param name="arg2">The second arg.</param>
            /// <param name="arg3">The third arg.</param>
            /// <param name="arg4">The fourth arg.</param>
            ///
            /// <returns>The function result.</returns>
            abstract member Invoke : arg1:'T1 * arg2:'T2 * arg3:'T3 * arg4:'T4 -> 'U

            /// <summary>Adapt an F# first class function value to be an optimized function value that can 
            /// accept four curried arguments without intervening execution. </summary>
            ///
            /// <param name="func">The input function.</param>
            ///
            /// <returns>The optimized function.</returns>
            static member Adapt : func:('T1 -> 'T2 -> 'T3 -> 'T4 -> 'U) -> FSharpFunc<'T1,'T2,'T3,'T4,'U>

            /// <summary>Construct an optimized function value that can accept four curried 
            /// arguments without intervening execution.</summary>
            /// <returns>The optimized function.</returns>
            new : unit ->  FSharpFunc<'T1,'T2,'T3,'T4,'U>

        /// <summary>The CLI type used to represent F# function values that accept five curried arguments 
        /// without intervening execution. This type should not typically used directly from 
        /// either F# code or from other CLI languages.</summary>
        [<AbstractClass>]
        type FSharpFunc<'T1,'T2,'T3,'T4,'T5,'U> = 
            inherit  FSharpFunc<'T1,('T2 -> 'T3 -> 'T4 -> 'T5 -> 'U)>

            /// <summary>Invoke an F# first class function value that accepts five curried arguments 
            /// without intervening execution</summary>
            ///
            /// <param name="arg1">The first arg.</param>
            /// <param name="arg2">The second arg.</param>
            /// <param name="arg3">The third arg.</param>
            /// <param name="arg4">The fourth arg.</param>
            /// <param name="arg5">The fifth arg.</param>
            ///
            /// <returns>The function result.</returns>
            abstract member Invoke : arg1:'T1 * arg2:'T2 * arg3:'T3 * arg4:'T4 * arg5:'T5 -> 'U

            /// <summary>Adapt an F# first class function value to be an optimized function value that can 
            /// accept five curried arguments without intervening execution. </summary>
            ///
            /// <param name="func">The input function.</param>
            ///
            /// <returns>The optimized function.</returns>
            static member Adapt : func:('T1 -> 'T2 -> 'T3 -> 'T4 -> 'T5 -> 'U) -> FSharpFunc<'T1,'T2,'T3,'T4,'T5,'U>

            /// <summary>Construct an optimized function value that can accept five curried 
            /// arguments without intervening execution.</summary>
            /// <returns>The optimized function.</returns>
            new : unit ->  FSharpFunc<'T1,'T2,'T3,'T4,'T5,'U>

    /// <summary>The type of mutable references. Use the functions [!] and [:=] to get and
    /// set values of this type.</summary>
    ///
    /// <category>Basic Types</category>
    /// <exclude />
    [<StructuralEquality; StructuralComparison>]
    [<CompiledName("FSharpRef`1")>]
    type Ref<'T> = 
        {  /// The current value of the reference cell
           mutable contents: 'T }

        /// <summary>The current value of the reference cell</summary>
        member Value : 'T with get,set
            
    /// <summary>The type of mutable references. Use the functions [!] and [:=] to get and
    /// set values of this type.</summary>
    /// <category>Basic Types</category>
    and 'T ref = Ref<'T>

    /// <summary>The type of optional values. When used from other CLI languages the
    /// empty option is the <c>null</c> value. </summary>
    ///
    /// <remarks>Use the constructors <c>Some</c> and <c>None</c> to create values of this type.
    /// Use the values in the <c>Option</c> module to manipulate values of this type,
    /// or pattern match against the values directly.
    ///
    /// <c>None</c> values will appear as the value <c>null</c> to other CLI languages.
    /// Instance methods on this type will appear as static methods to other CLI languages
    /// due to the use of <c>null</c> as a value representation.</remarks>
    ///
    /// <category>Options</category>
    /// <exclude />
    [<DefaultAugmentation(false)>]
    [<CompilationRepresentation(CompilationRepresentationFlags.UseNullAsTrueValue)>]
    [<StructuralEquality; StructuralComparison>]
    [<CompiledName("FSharpOption`1")>]
    type Option<'T> =

        /// <summary>The representation of "No value"</summary>
        | None :       'T option

        /// <summary>The representation of "Value of type 'T"</summary>
        ///
        /// <param name="Value">The input value.</param>
        ///
        /// <returns>An option representing the value.</returns>
        | Some : Value:'T -> 'T option 

        /// <summary>Create an option value that is a 'None' value.</summary>
        /// <exclude />
        static member None : 'T option

        /// <summary>Create an option value that is a 'Some' value.</summary>
        ///
        /// <param name="value">The input value</param>
        ///
        /// <returns>An option representing the value.</returns>
        /// <exclude />
        static member Some : value:'T -> 'T option

        /// <summary>Implicitly converts a value into an optional that is a 'Some' value.</summary>
        ///
        /// <param name="value">The input value</param>
        ///
        /// <returns>An option representing the value.</returns>
        /// <exclude />
        static member op_Implicit : value:'T -> 'T option

        [<CompilationRepresentation(CompilationRepresentationFlags.Instance)>]
        /// <summary>Get the value of a 'Some' option. A NullReferenceException is raised if the option is 'None'.</summary>
        member Value : 'T

        /// <summary>Return 'true' if the option is a 'Some' value.</summary>
        member IsSome : bool

        /// <summary>Return 'true' if the option is a 'None' value.</summary>
        member IsNone : bool
  
    /// <summary>The type of optional values. When used from other CLI languages the
    /// empty option is the <c>null</c> value. </summary>
    ///
    /// <remarks>Use the constructors <c>Some</c> and <c>None</c> to create values of this type.
    /// Use the values in the <c>Option</c> module to manipulate values of this type,
    /// or pattern match against the values directly.
    ///
    /// 'None' values will appear as the value <c>null</c> to other CLI languages.
    /// Instance methods on this type will appear as static methods to other CLI languages
    /// due to the use of <c>null</c> as a value representation.</remarks>
    ///
    /// <category index="3">Options</category>
    and 'T option = Option<'T>

    /// <summary>The type of optional values, represented as structs.</summary>
    ///
    /// <remarks>Use the constructors <c>ValueSome</c> and <c>ValueNone</c> to create values of this type.
    /// Use the values in the <c>ValueOption</c> module to manipulate values of this type,
    /// or pattern match against the values directly.</remarks>
    ///
    /// <category>Options</category>
    /// <exclude />
    [<StructuralEquality; StructuralComparison>]
    [<CompiledName("FSharpValueOption`1")>]
    [<Struct>]
    type ValueOption<'T> =
        /// <summary>The representation of "No value"</summary>
        | ValueNone: 'T voption

        /// <summary>The representation of "Value of type 'T"</summary>
        ///
        /// <param name="Item">The input value.</param>
        ///
        /// <returns>An option representing the value.</returns>
        | ValueSome: 'T -> 'T voption

        /// <summary>Get the value of a 'ValueSome' option. An InvalidOperationException is raised if the option is 'ValueNone'.</summary>
        member Value : 'T

        /// <summary>Create a value option value that is a 'ValueNone' value.</summary>
        /// <exclude />
        static member None : 'T voption

        /// <summary>Create a value option value that is a 'Some' value.</summary>
        ///
        /// <param name="value">The input value</param>
        ///
        /// <returns>A value option representing the value.</returns>
        /// <exclude />
        static member Some : value:'T -> 'T voption
        
        /// <summary>Return 'true' if the value option is a 'ValueSome' value.</summary>
        member IsSome : bool

        /// <summary>Return 'true' if the value option is a 'ValueNone' value.</summary>
        member IsNone : bool
        
        /// <summary>Implicitly converts a value into an optional that is a 'ValueSome' value.</summary>
        ///
        /// <param name="value">The input value</param>
        ///
        /// <returns>A voption representing the value.</returns>
        /// <exclude />
        static member op_Implicit: value: 'T -> 'T voption

    /// <summary>The type of optional values, represented as structs.</summary>
    ///
    /// <remarks>Use the constructors <c>ValueSome</c> and <c>ValueNone</c> to create values of this type.
    /// Use the values in the <c>ValueOption</c> module to manipulate values of this type,
    /// or pattern match against the values directly.</remarks>
    ///
    /// <category>Options</category>
    and 'T voption = ValueOption<'T>

    /// <summary>Helper type for error handling without exceptions.</summary>
    ///
    /// <category>Choices and Results</category>
    [<StructuralEquality; StructuralComparison>]
    [<CompiledName("FSharpResult`2")>]
    [<Struct>]
    type Result<'T,'TError> = 

      /// Represents an OK or a Successful result. The code succeeded with a value of 'T.
      | Ok of ResultValue:'T 

      /// Represents an Error or a Failure. The code failed with a value of 'TError representing what went wrong.
      | Error of ErrorValue:'TError

namespace Microsoft.FSharp.Collections

    open System
    open System.Collections
    open System.Collections.Generic
    open Microsoft.FSharp.Core

    /// <summary>The type of immutable singly-linked lists.</summary>
    ///
    /// <remarks>Use the constructors <c>[]</c> and <c>::</c> (infix) to create values of this type, or
    /// the notation <c>[1;2;3]</c>. Use the values in the <c>List</c> module to manipulate 
    /// values of this type, or pattern match against the values directly.
    /// </remarks>
    ///
    /// <exclude />
    [<DefaultAugmentation(false)>]
    [<StructuralEquality; StructuralComparison>]
    [<CompiledName("FSharpList`1")>]
    type List<'T> =
        | ([])  : 'T list
        | (::)  : Head: 'T * Tail: 'T list -> 'T list

        /// <summary>Returns an empty list of a particular type</summary>
        static member Empty : 'T list
        
        /// <summary>Gets the number of items contained in the list</summary>
        member Length : int

        /// <summary>Gets a value indicating if the list contains no entries</summary>
        member IsEmpty : bool

        /// <summary>Gets the first element of the list</summary>
        member Head : 'T

        /// <summary>Gets the tail of the list, which is a list containing all the elements of the list, excluding the first element </summary>
        member Tail : 'T list

        /// <summary>Gets the element of the list at the given position.</summary>
        /// <remarks>Lists are represented as linked lists so this is an O(n) operation.</remarks>
        /// <param name="index">The index.</param>
        ///
        /// <returns>The value at the given index.</returns>
        member Item : index:int -> 'T with get 
        
        /// <summary>Gets a slice of the list, the elements of the list from the given start index to the given end index.</summary>
        ///
        /// <param name="startIndex">The start index.</param>
        /// <param name="endIndex">The end index.</param>
        ///
        /// <returns>The sub list specified by the input indices.</returns>
        member GetSlice : startIndex:int option * endIndex:int option -> 'T list  

        /// <summary>Get the index for the element offset elements away from the end of the collection.</summary>
        ///
        /// <param name="rank">The rank of the index.</param>
        /// <param name="offset">The offset from the end.</param>
        ///
        /// <returns>The corresponding index from the start.</returns>
        [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
        member GetReverseIndex: rank: int * offset: int -> int

        /// <summary>Returns a list with <c>head</c> as its first element and <c>tail</c> as its subsequent elements</summary>
        ///
        /// <param name="head">A new head value for the list.</param>
        /// <param name="tail">The existing list.</param>
        ///
        /// <returns>The list with head appended to the front of tail.</returns>
        static member Cons : head:'T * tail:'T list -> 'T list
        
        interface IEnumerable<'T>
        interface IEnumerable
        interface IReadOnlyCollection<'T>
        interface IReadOnlyList<'T>

    /// <summary>The type of immutable singly-linked lists. </summary>
    ///
    /// <remarks>See the <see cref="T:Microsoft.FSharp.Collections.ListModule"/> module for further operations related to lists.
    ///
    /// Use the constructors <c>[]</c> and <c>::</c> (infix) to create values of this type, or
    /// the notation <c>[1; 2; 3]</c>. Use the values in the <c>List</c> module to manipulate 
    /// values of this type, or pattern match against the values directly.
    ///
    ///  See also <a href="https://docs.microsoft.com/dotnet/fsharp/language-reference/lists">F# Language Guide - Lists</a>.
    /// </remarks>
    and 'T list = List<'T>

    /// <summary>An abbreviation for the CLI type <see cref="T:System.Collections.Generic.List`1"/></summary>
    type ResizeArray<'T> = System.Collections.Generic.List<'T>

    /// <summary>An abbreviation for the CLI type <see cref="T:System.Collections.Generic.IEnumerable`1"/></summary>
    ///
    /// <remarks>
    ///  See the <see cref="T:Microsoft.FSharp.Collections.SeqModule"/> module for further operations related to sequences.
    ///
    ///  See also <a href="https://docs.microsoft.com/dotnet/fsharp/language-reference/sequences">F# Language Guide - Sequences</a>.
    ///</remarks>
    type seq<'T> = IEnumerable<'T>

namespace Microsoft.FSharp.Core

    open System
    open System.Collections.Generic
    open Microsoft.FSharp.Core
    open Microsoft.FSharp.Collections

    /// <summary>Basic F# Operators. This module is automatically opened in all F# code.</summary>
    ///
    /// <category index="2">Basic Operators</category>
    [<AutoOpen>]
    module Operators = 

        /// <summary>Overloaded unary negation.</summary>
        ///
        /// <param name="n">The value to negate.</param>
        ///
        /// <returns>The result of the operation.</returns>
        val inline ( ~- ) : n:^T -> ^T when ^T : (static member ( ~- ) : ^T -> ^T) and default ^T : int

        /// <summary>Overloaded addition operator</summary>
        ///
        /// <param name="x">The first parameter.</param>
        /// <param name="y">The second parameter.</param>
        ///
        /// <returns>The result of the operation.</returns>
        val inline ( + ) : x:^T1 -> y:^T2 -> ^T3  when (^T1 or ^T2) : (static member ( + ) : ^T1 * ^T2    -> ^T3) and default ^T2 : ^T3 and default ^T3 : ^T1 and default ^T3 : ^T2 and default ^T1 : ^T3 and default ^T1 : ^T2 and default ^T1 : int
        
        /// <summary>Overloaded subtraction operator</summary>
        ///
        /// <param name="x">The first parameter.</param>
        /// <param name="y">The second parameter.</param>
        ///
        /// <returns>The result of the operation.</returns>
        val inline ( - ) : x:^T1 -> y:^T2 -> ^T3  when (^T1 or ^T2) : (static member ( - ) : ^T1 * ^T2    -> ^T3) and default ^T2 : ^T3 and default ^T3 : ^T1 and default ^T3 : ^T2 and default ^T1 : ^T3 and default ^T1 : ^T2 and default ^T1 : int
        
        /// <summary>Overloaded multiplication operator</summary>
        ///
        /// <param name="x">The first parameter.</param>
        /// <param name="y">The second parameter.</param>
        ///
        /// <returns>The result of the operation.</returns>
        val inline ( * ) : x:^T1 -> y:^T2 -> ^T3  when (^T1 or ^T2) : (static member ( * ) : ^T1 * ^T2    -> ^T3) and default ^T2 : ^T3 and default ^T3 : ^T1 and default ^T3 : ^T2 and default ^T1 : ^T3 and default ^T1 : ^T2 and default ^T1 : int
        
        /// <summary>Overloaded division operator</summary>
        ///
        /// <param name="x">The first parameter.</param>
        /// <param name="y">The second parameter.</param>
        ///
        /// <returns>The result of the operation.</returns>
        val inline ( / ) : x:^T1 -> y:^T2 -> ^T3  when (^T1 or ^T2) : (static member ( / ) : ^T1 * ^T2    -> ^T3) and default ^T2 : ^T3 and default ^T3 : ^T1 and default ^T3 : ^T2 and default ^T1 : ^T3 and default ^T1 : ^T2 and default ^T1 : int
        
        /// <summary>Overloaded modulo operator</summary>
        ///
        /// <param name="x">The first parameter.</param>
        /// <param name="y">The second parameter.</param>
        ///
        /// <returns>The result of the operation.</returns>
        val inline ( % ) : x:^T1 -> y:^T2 -> ^T3    when (^T1 or ^T2) : (static member ( % ) : ^T1 * ^T2    -> ^T3) and default ^T2 : ^T3 and default ^T3 : ^T1 and default ^T3 : ^T2 and default ^T1 : ^T3 and default ^T1 : ^T2 and default ^T1 : int
        
        /// <summary>Overloaded bitwise-AND operator</summary>
        ///
        /// <param name="x">The first parameter.</param>
        /// <param name="y">The second parameter.</param>
        ///
        /// <returns>The result of the operation.</returns>
        val inline (&&&): x:^T -> y:^T -> ^T     when ^T : (static member (&&&) : ^T * ^T    -> ^T) and default ^T : int
        
        /// <summary>Overloaded bitwise-OR operator</summary>
        ///
        /// <param name="x">The first parameter.</param>
        /// <param name="y">The second parameter.</param>
        ///
        /// <returns>The result of the operation.</returns>
        val inline (|||) : x:^T -> y:^T -> ^T    when ^T : (static member (|||) : ^T * ^T    -> ^T) and default ^T : int
        
        /// <summary>Overloaded bitwise-XOR operator</summary>
        ///
        /// <param name="x">The first parameter.</param>
        /// <param name="y">The second parameter.</param>
        ///
        /// <returns>The result of the operation.</returns>
        val inline (^^^) : x:^T -> y:^T -> ^T    when ^T : (static member (^^^) : ^T * ^T    -> ^T) and default ^T : int
        
        /// <summary>Overloaded byte-shift left operator by a specified number of bits</summary>
        ///
        /// <param name="value">The input value.</param>
        /// <param name="shift">The amount to shift.</param>
        ///
        /// <returns>The result of the operation.</returns>
        val inline (<<<) : value:^T -> shift:int32 -> ^T when ^T : (static member (<<<) : ^T * int32 -> ^T) and default ^T : int
        
        /// <summary>Overloaded byte-shift right operator by a specified number of bits</summary>
        ///
        /// <param name="value">The input value.</param>
        /// <param name="shift">The amount to shift.</param>
        ///
        /// <returns>The result of the operation.</returns>
        val inline (>>>) : value:^T -> shift:int32 -> ^T when ^T : (static member (>>>) : ^T * int32 -> ^T) and default ^T : int
        
        /// <summary>Overloaded bitwise-NOT operator</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The result of the operation.</returns>
        val inline (~~~)  : value:^T -> ^T         when ^T : (static member (~~~) : ^T         -> ^T) and default ^T : int
        
        /// <summary>Overloaded prefix-plus operator</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The result of the operation.</returns>
        val inline (~+) : value:^T -> ^T           when ^T : (static member (~+)  : ^T         -> ^T) and default ^T : int
        
        /// <summary>Structural less-than comparison</summary>
        ///
        /// <param name="x">The first parameter.</param>
        /// <param name="y">The second parameter.</param>
        ///
        /// <returns>The result of the comparison.</returns>
        val inline ( < ) : x:'T -> y:'T -> bool when 'T : comparison
        
        /// <summary>Structural greater-than</summary>
        ///
        /// <param name="x">The first parameter.</param>
        /// <param name="y">The second parameter.</param>
        ///
        /// <returns>The result of the comparison.</returns>
        val inline ( > ) : x:'T -> y:'T -> bool when 'T : comparison
        
        /// <summary>Structural greater-than-or-equal</summary>
        ///
        /// <param name="x">The first parameter.</param>
        /// <param name="y">The second parameter.</param>
        ///
        /// <returns>The result of the comparison.</returns>
        val inline ( >= ) : x:'T -> y:'T -> bool when 'T : comparison
        
        /// <summary>Structural less-than-or-equal comparison</summary>
        ///
        /// <param name="x">The first parameter.</param>
        /// <param name="y">The second parameter.</param>
        ///
        /// <returns>The result of the comparison.</returns>
        val inline ( <= ) : x:'T -> y:'T -> bool when 'T : comparison
        
        /// <summary>Structural equality</summary>
        ///
        /// <param name="x">The first parameter.</param>
        /// <param name="y">The second parameter.</param>
        ///
        /// <returns>The result of the comparison.</returns>
        val inline ( = ) : x:'T -> y:'T -> bool when 'T : equality
        
        /// <summary>Structural inequality</summary>
        ///
        /// <param name="x">The first parameter.</param>
        /// <param name="y">The second parameter.</param>
        ///
        /// <returns>The result of the comparison.</returns>
        val inline ( <> ) : x:'T -> y:'T -> bool when 'T : equality

        /// <summary>Compose two functions, the function on the left being applied first</summary>
        ///
        /// <param name="func1">The first function to apply.</param>
        /// <param name="func2">The second function to apply.</param>
        ///
        /// <returns>The composition of the input functions.</returns>
        val inline (>>): func1:('T1 -> 'T2) -> func2:('T2 -> 'T3) -> ('T1 -> 'T3) 
        
        /// <summary>Compose two functions, the function on the right being applied first</summary>
        ///
        /// <param name="func2">The second function to apply.</param>
        /// <param name="func1">The first function to apply.</param>
        ///
        /// <returns>The composition of the input functions.</returns>
        val inline (<<): func2:('T2 -> 'T3) -> func1:('T1 -> 'T2) -> ('T1 -> 'T3) 
        
        /// <summary>Apply a function to a value, the value being on the left, the function on the right</summary>
        ///
        /// <param name="arg">The argument.</param>
        /// <param name="func">The function.</param>
        ///
        /// <returns>The function result.</returns>
        val inline (|>): arg:'T1 -> func:('T1 -> 'U) -> 'U

        /// <summary>Apply a function to two values, the values being a pair on the left, the function on the right</summary>
        ///
        /// <param name="arg1">The first argument.</param>
        /// <param name="arg2">The second argument.</param>
        /// <param name="func">The function.</param>
        ///
        /// <returns>The function result.</returns>
        val inline (||>): arg1:'T1 * arg2:'T2 -> func:('T1 -> 'T2 -> 'U) -> 'U

        /// <summary>Apply a function to three values, the values being a triple on the left, the function on the right</summary>
        ///
        /// <param name="arg1">The first argument.</param>
        /// <param name="arg2">The second argument.</param>
        /// <param name="arg3">The third argument.</param>
        /// <param name="func">The function.</param>
        ///
        /// <returns>The function result.</returns>
        val inline (|||>): arg1:'T1 * arg2:'T2 * arg3:'T3 -> func:('T1 -> 'T2 -> 'T3 -> 'U) -> 'U
        
        /// <summary>Apply a function to a value, the value being on the right, the function on the left</summary>
        ///
        /// <param name="func">The function.</param>
        /// <param name="arg1">The argument.</param>
        ///
        /// <returns>The function result.</returns>
        val inline (<|): func:('T -> 'U) -> arg1:'T -> 'U

        /// <summary>Apply a function to two values, the values being a pair on the right, the function on the left</summary>
        ///
        /// <param name="func">The function.</param>
        /// <param name="arg1">The first argument.</param>
        /// <param name="arg2">The second argument.</param>
        ///
        /// <returns>The function result.</returns>
        val inline (<||): func:('T1 -> 'T2 -> 'U) -> arg1:'T1 * arg2:'T2 -> 'U

        /// <summary>Apply a function to three values, the values being a triple on the right, the function on the left</summary>
        ///
        /// <param name="func">The function.</param>
        /// <param name="arg1">The first argument.</param>
        /// <param name="arg2">The second argument.</param>
        /// <param name="arg3">The third argument.</param>
        ///
        /// <returns>The function result.</returns>
        val inline (<|||): func:('T1 -> 'T2 -> 'T3 -> 'U) -> arg1:'T1 * arg2:'T2 * arg3:'T3 -> 'U

        /// <summary>Used to specify a default value for an optional argument in the implementation of a function</summary>
        ///
        /// <param name="arg">An option representing the argument.</param>
        /// <param name="defaultValue">The default value of the argument.</param>
        ///
        /// <returns>The argument value. If it is None, the defaultValue is returned.</returns>
        [<CompiledName("DefaultArg")>]
        val defaultArg : arg:'T option -> defaultValue:'T -> 'T 

        /// <summary>Used to specify a default value for an optional argument in the implementation of a function</summary>
        ///
        /// <param name="arg">A value option representing the argument.</param>
        /// <param name="defaultValue">The default value of the argument.</param>
        ///
        /// <returns>The argument value. If it is None, the defaultValue is returned.</returns>
        [<CompiledName("DefaultValueArg")>]
        val defaultValueArg : arg:'T voption -> defaultValue:'T -> 'T 

        /// <summary>Concatenate two strings. The operator '+' may also be used.</summary>
        [<CompilerMessage("This construct is for ML compatibility. Consider using the '+' operator instead. This may require a type annotation to indicate it acts on strings. This message can be disabled using '--nowarn:62' or '#nowarn \"62\"'.", 62, IsHidden=true)>]
        val (^): s1:string -> s2:string -> string

        /// <summary>Raises an exception</summary>
        ///
        /// <param name="exn">The exception to raise.</param>
        ///
        /// <returns>The result value.</returns>
        [<CompiledName("Raise")>]
        val inline raise : exn:System.Exception -> 'T
        
        /// <summary>Rethrows an exception. This should only be used when handling an exception</summary>
        /// <returns>The result value.</returns>
        [<CompiledName("Rethrow")>]
        [<System.Obsolete("This function has been renamed to 'reraise'. Please adjust your code to reflect this", true)>]
        val inline rethrow : unit -> 'T

        /// <summary>Rethrows an exception. This should only be used when handling an exception</summary>
        /// <returns>The result value.</returns>
        [<CompiledName("Reraise")>]
        val inline reraise : unit -> 'T

        /// <summary>Builds a <see cref="T:System.Exception"/> object.</summary>
        ///
        /// <param name="message">The message for the Exception.</param>
        ///
        /// <returns>A System.Exception.</returns>
        val Failure : message:string -> exn
        
        /// <summary>Matches <see cref="T:System.Exception"/> objects whose runtime type is precisely <see cref="T:System.Exception"/></summary>
        ///
        /// <param name="error">The input exception.</param>
        ///
        /// <returns>A string option.</returns>
        [<CompiledName("FailurePattern")>]
        val (|Failure|_|) : error:exn -> string option
        
        /// <summary>Return the first element of a tuple, <c>fst (a,b) = a</c>.</summary>
        ///
        /// <param name="tuple">The input tuple.</param>
        ///
        /// <returns>The first value.</returns>
        [<CompiledName("Fst")>]
        val inline fst : tuple:('T1 * 'T2) -> 'T1
        
        /// <summary>Return the second element of a tuple, <c>snd (a,b) = b</c>.</summary>
        ///
        /// <param name="tuple">The input tuple.</param>
        ///
        /// <returns>The second value.</returns>
        [<CompiledName("Snd")>]
        val inline snd : tuple:('T1 * 'T2) -> 'T2

        /// <summary>Generic comparison.</summary>
        ///
        /// <param name="e1">The first value.</param>
        /// <param name="e2">The second value.</param>
        ///
        /// <returns>The result of the comparison.</returns>
        [<CompiledName("Compare")>]
        val inline compare: e1:'T -> e2:'T -> int when 'T : comparison

        /// <summary>Maximum based on generic comparison</summary>
        ///
        /// <param name="e1">The first value.</param>
        /// <param name="e2">The second value.</param>
        ///
        /// <returns>The maximum value.</returns>
        [<CompiledName("Max")>]
        val inline max : e1:'T -> e2:'T -> 'T  when 'T : comparison

        /// <summary>Minimum based on generic comparison</summary>
        ///
        /// <param name="e1">The first value.</param>
        /// <param name="e2">The second value.</param>
        ///
        /// <returns>The minimum value.</returns>
        [<CompiledName("Min")>]
        val inline min : e1:'T -> e2:'T -> 'T  when 'T : comparison

        /// <summary>Ignore the passed value. This is often used to throw away results of a computation.</summary>
        ///
        /// <param name="value">The value to ignore.</param>
        [<CompiledName("Ignore")>]
        val inline ignore : value:'T -> unit

        /// <summary>Unbox a strongly typed value.</summary>
        ///
        /// <param name="value">The boxed value.</param>
        ///
        /// <returns>The unboxed result.</returns>
        [<CompiledName("Unbox")>]
        val inline unbox : value:obj -> 'T

        /// <summary>Boxes a strongly typed value.</summary>
        ///
        /// <param name="value">The value to box.</param>
        ///
        /// <returns>The boxed object.</returns>
        [<CompiledName("Box")>]
        val inline box : value:'T -> obj

        /// <summary>Try to unbox a strongly typed value.</summary>
        ///
        /// <param name="value">The boxed value.</param>
        ///
        /// <returns>The unboxed result as an option.</returns>
        [<CompiledName("TryUnbox")>]
        val inline tryUnbox : value:obj -> 'T option

        /// <summary>Determines whether the given value is null.</summary>
        ///
        /// <param name="value">The value to check.</param>
        ///
        /// <returns>True when value is null, false otherwise.</returns>
        [<CompiledName("IsNull")>]
        val inline isNull : value:'T -> bool when 'T : null
        
        /// <summary>Determines whether the given value is not null.</summary>
        ///
        /// <param name="value">The value to check.</param>
        ///
        /// <returns>True when value is not null, false otherwise.</returns>
        [<CompiledName("IsNotNull")>]
        val inline internal isNotNull : value:'T -> bool when 'T : null

        /// <summary>Throw a <see cref="T:System.Exception"/> exception.</summary>
        ///
        /// <param name="message">The exception message.</param>
        ///
        /// <returns>The result value.</returns>
        [<CompiledName("FailWith")>]
        val inline failwith : message:string -> 'T 

        /// <summary>Throw a <see cref="T:System.ArgumentException"/> exception with
        /// the given argument name and message.</summary>
        ///
        /// <param name="argumentName">The argument name.</param>
        /// <param name="message">The exception message.</param>
        ///
        /// <returns>The result value.</returns>
        [<CompiledName("InvalidArg")>]
        val inline invalidArg : argumentName:string -> message:string -> 'T 

        /// <summary>Throw a <see cref="T:System.ArgumentNullException"/> exception</summary>
        ///
        /// <param name="argumentName">The argument name.</param>
        ///
        /// <returns>The result value.</returns>
        [<CompiledName("NullArg")>]
        val inline nullArg : argumentName:string -> 'T 

        /// <summary>Throw a <see cref="T:System.InvalidOperationException"/> exception</summary>
        ///
        /// <param name="message">The exception message.</param>
        ///
        /// <returns>The result value.</returns>
        [<CompiledName("InvalidOp")>]
        val inline invalidOp : message:string -> 'T 

        /// <summary>The identity function</summary>
        ///
        /// <param name="x">The input value.</param>
        ///
        /// <returns>The same value.</returns>
        [<CompiledName("Identity")>]
        val id : x:'T -> 'T 

        /// <summary>Create a mutable reference cell</summary>
        ///
        /// <param name="value">The value to contain in the cell.</param>
        ///
        /// <returns>The created reference cell.</returns>
        [<CompiledName("Ref")>]
        val ref : value:'T -> 'T ref

        /// <summary>Assign to a mutable reference cell</summary>
        ///
        /// <param name="cell">The cell to mutate.</param>
        /// <param name="value">The value to set inside the cell.</param>
        val ( := ) : cell:'T ref -> value:'T -> unit

        /// <summary>Dereference a mutable reference cell</summary>
        ///
        /// <param name="cell">The cell to dereference.</param>
        ///
        /// <returns>The value contained in the cell.</returns>
        val ( ! ) : cell:'T ref -> 'T

        /// <summary>Decrement a mutable reference cell containing an integer</summary>
        ///
        /// <param name="cell">The reference cell.</param>
        [<CompiledName("Decrement")>]
        val decr: cell:int ref -> unit

        /// <summary>Increment a mutable reference cell containing an integer</summary>
        ///
        /// <param name="cell">The reference cell.</param>
        [<CompiledName("Increment")>]
        val incr: cell:int ref -> unit

        /// <summary>Concatenate two lists.</summary>
        ///
        /// <param name="list1">The first list.</param>
        /// <param name="list2">The second list.</param>
        ///
        /// <returns>The concatenation of the lists.</returns>
        val (@): list1:'T list -> list2:'T list -> 'T list

        /// <summary>Negate a logical value. Not True equals False and not False equals True</summary>
        ///
        /// <param name="value">The value to negate.</param>
        ///
        /// <returns>The result of the negation.</returns>
        [<CompiledName("Not")>]
        val inline not : value:bool -> bool

        /// <summary>Builds a sequence using sequence expression syntax</summary>
        ///
        /// <param name="sequence">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        [<CompiledName("CreateSequence")>]
        val seq : sequence:seq<'T> -> seq<'T>

        /// <summary>Exit the current hardware isolated process, if security settings permit,
        /// otherwise raise an exception. Calls <see cref="M:System.Environment.Exit"/>.</summary>
        ///
        /// <param name="exitcode">The exit code to use.</param>
        ///
        /// <returns>The result value.</returns>
        [<CompiledName("Exit")>]
        val exit: exitcode:int -> 'T   when default 'T : obj

        /// <summary>Equivalent to <see cref="P:System.Double.PositiveInfinity"/></summary>
        [<CompiledName("Infinity")>]
        val infinity: float

        /// <summary>Equivalent to <see cref="P:System.Double.NaN"/></summary>
        [<CompiledName("NaN")>]
        val nan: float

        /// <summary>Equivalent to <see cref="P:System.Single.PositiveInfinity"/></summary>
        [<CompiledName("InfinitySingle")>]
        val infinityf: float32

        /// <summary>Equivalent to <see cref="P:System.Single.NaN"/></summary>
        [<CompiledName("NaNSingle")>]
        val nanf: float32

        /// <summary>Reads the value of the property <see cref="P:System.Console.In"/>. </summary>
        [<CompiledName("ConsoleIn")>]
        val stdin<'T> : System.IO.TextReader

        /// <summary>Reads the value of the property <see cref="P:System.Console.Error"/>. </summary>
        [<CompiledName("ConsoleError")>]
        val stderr<'T> : System.IO.TextWriter

        /// <summary>Reads the value of the property <see cref="P:System.Console.Out"/>.</summary>
        [<CompiledName("ConsoleOut")>]
        val stdout<'T> : System.IO.TextWriter

        /// <summary>The standard overloaded range operator, e.g. <c>[n..m]</c> for lists, <c>seq {n..m}</c> for sequences</summary>
        ///
        /// <param name="start">The start value of the range.</param>
        /// <param name="finish">The end value of the range.</param>
        ///
        /// <returns>The sequence spanning the range.</returns>
        val inline (..)    : start:^T       -> finish:^T -> seq< ^T >    
                                when ^T : (static member (+)   : ^T * ^T -> ^T) 
                                and ^T : (static member One  : ^T)
                                and ^T : equality
                                and ^T : comparison 
                                and default ^T : int
        
        /// <summary>The standard overloaded skip range operator, e.g. <c>[n..skip..m]</c> for lists, <c>seq {n..skip..m}</c> for sequences</summary>
        ///
        /// <param name="start">The start value of the range.</param>
        /// <param name="step">The step value of the range.</param>
        /// <param name="finish">The end value of the range.</param>
        ///
        /// <returns>The sequence spanning the range using the specified step size.</returns>
        val inline (.. ..) : start:^T -> step:^Step -> finish:^T -> seq< ^T >    
                                when (^T or ^Step) : (static member (+)   : ^T * ^Step -> ^T) 
                                and ^Step : (static member Zero : ^Step)
                                and ^T : equality
                                and ^T : comparison                                
                                and default ^Step : ^T
                                and default ^T : int
        
        /// <summary>Execute the function as a mutual-exclusion region using the input value as a lock. </summary>
        ///
        /// <param name="lockObject">The object to be locked.</param>
        /// <param name="action">The action to perform during the lock.</param>
        ///
        /// <returns>The resulting value.</returns>
        [<CompiledName("Lock")>]
        val inline lock: lockObject:'Lock -> action:(unit -> 'T) -> 'T when 'Lock : not struct 

        /// <summary>Clean up resources associated with the input object after the completion of the given function.
        /// Cleanup occurs even when an exception is raised by the protected
        /// code. </summary>
        ///
        /// <param name="resource">The resource to be disposed after action is called.</param>
        /// <param name="action">The action that accepts the resource.</param>
        ///
        /// <returns>The resulting value.</returns>
        [<CompiledName("Using")>]
        val using: resource:('T :> System.IDisposable) -> action:('T -> 'U) -> 'U


        /// <summary>Generate a System.Type runtime representation of a static type.</summary>
        [<RequiresExplicitTypeArguments>] 
        [<CompiledName("TypeOf")>]
        val inline typeof<'T> : System.Type

        /// <summary>Returns the name of the given symbol.</summary>        
        [<CompiledName("NameOf"); CompilerMessage(ExperimentalAttributeMessages.NotSupportedYet, 3501, IsError=true)>]
        val inline nameof : 'T -> string

        /// <summary>An internal, library-only compiler intrinsic for compile-time 
        /// generation of a RuntimeMethodHandle.</summary>
        [<CompiledName("MethodHandleOf")>]
#if DEBUG
        val methodhandleof : ('T -> 'TResult) -> System.RuntimeMethodHandle
#else
        val internal methodhandleof : ('T -> 'TResult) -> System.RuntimeMethodHandle
#endif

        /// <summary>Generate a System.Type representation for a type definition. If the
        /// input type is a generic type instantiation then return the 
        /// generic type definition associated with all such instantiations.</summary>
        [<RequiresExplicitTypeArguments>] 
        [<CompiledName("TypeDefOf")>]
        val inline typedefof<'T> : System.Type

        /// <summary>Returns the internal size of a type in bytes. For example, <c>sizeof&lt;int&gt;</c> returns 4.</summary>
        [<CompiledName("SizeOf")>]
        [<RequiresExplicitTypeArguments>] 
        val inline sizeof<'T> : int
        
        /// <summary>A generic hash function, designed to return equal hash values for items that are 
        /// equal according to the "=" operator. By default it will use structural hashing
        /// for F# union, record and tuple types, hashing the complete contents of the 
        /// type. The exact behaviour of the function can be adjusted on a 
        /// type-by-type basis by implementing GetHashCode for each type.</summary>
        ///
        /// <param name="obj">The input object.</param>
        ///
        /// <returns>The computed hash.</returns>
        [<CompiledName("Hash")>]
        val inline hash: obj:'T -> int when 'T : equality

        /// <summary>A generic hash function. This function has the same behaviour as 'hash', 
        /// however the default structural hashing for F# union, record and tuple 
        /// types stops when the given limit of nodes is reached. The exact behaviour of 
        /// the function can be adjusted on a type-by-type basis by implementing 
        /// GetHashCode for each type.</summary>
        ///
        /// <param name="limit">The limit of nodes.</param>
        /// <param name="obj">The input object.</param>
        ///
        /// <returns>The computed hash.</returns>
        val inline limitedHash: limit: int -> obj:'T -> int when 'T : equality


        /// <summary>Absolute value of the given number.</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The absolute value of the input.</returns>
        [<CompiledName("Abs")>]
        val inline abs      : value:^T -> ^T       when ^T : (static member Abs      : ^T -> ^T)      and default ^T : int
        
        /// <summary>Inverse cosine of the given number</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The inverse cosine of the input.</returns>
        [<CompiledName("Acos")>]
        val inline acos     : value:^T -> ^T       when ^T : (static member Acos     : ^T -> ^T)      and default ^T : float
        
        /// <summary>Inverse sine of the given number</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The inverse sine of the input.</returns>
        [<CompiledName("Asin")>]
        val inline asin     : value:^T -> ^T       when ^T : (static member Asin     : ^T -> ^T)      and default ^T : float
        
        /// <summary>Inverse tangent of the given number</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The inverse tangent of the input.</returns>
        [<CompiledName("Atan")>]
        val inline atan     : value:^T -> ^T       when ^T : (static member Atan     : ^T -> ^T)      and default ^T : float
        
        /// <summary>Inverse tangent of <c>x/y</c> where <c>x</c> and <c>y</c> are specified separately</summary>
        ///
        /// <param name="y">The y input value.</param>
        /// <param name="x">The x input value.</param>
        ///
        /// <returns>The inverse tangent of the input ratio.</returns>
        [<CompiledName("Atan2")>]
        val inline atan2    : y:^T1 -> x:^T1 -> 'T2 when ^T1 : (static member Atan2    : ^T1 * ^T1 -> 'T2) and default ^T1 : float
        
        /// <summary>Ceiling of the given number</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The ceiling of the input.</returns>
        [<CompiledName("Ceiling")>]
        val inline ceil     : value:^T -> ^T       when ^T : (static member Ceiling  : ^T -> ^T)      and default ^T : float
        
        /// <summary>Exponential of the given number</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The exponential of the input.</returns>
        [<CompiledName("Exp")>]
        val inline exp      : value:^T -> ^T       when ^T : (static member Exp      : ^T -> ^T)      and default ^T : float

        /// <summary>Floor of the given number</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The floor of the input.</returns>
        [<CompiledName("Floor")>]
        val inline floor    : value:^T -> ^T       when ^T : (static member Floor    : ^T -> ^T)      and default ^T : float

        /// <summary>Sign of the given number</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>-1, 0, or 1 depending on the sign of the input.</returns>
        [<CompiledName("Sign")>]
        val inline sign     : value:^T -> int      when ^T : (member Sign    : int)      and default ^T : float

        /// <summary>Round the given number</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The nearest integer to the input value.</returns>
        [<CompiledName("Round")>]
        val inline round    : value:^T -> ^T       when ^T : (static member Round    : ^T -> ^T)      and default ^T : float

        /// <summary>Natural logarithm of the given number</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The natural logarithm of the input.</returns>
        [<CompiledName("Log")>]
        val inline log      : value:^T -> ^T       when ^T : (static member Log      : ^T -> ^T)      and default ^T : float

        /// <summary>Logarithm to base 10 of the given number</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The logarithm to base 10 of the input.</returns>
        [<CompiledName("Log10")>]
        val inline log10    : value:^T -> ^T       when ^T : (static member Log10    : ^T -> ^T)      and default ^T : float

        /// <summary>Square root of the given number</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The square root of the input.</returns>
        [<CompiledName("Sqrt")>]
        val inline sqrt     : value:^T -> ^U       when ^T : (static member Sqrt     : ^T -> ^U)      and default ^U : ^T and default ^T : ^U and default ^T : float 

        /// <summary>Cosine of the given number</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The cosine of the input.</returns>
        [<CompiledName("Cos")>]
        val inline cos      : value:^T -> ^T       when ^T : (static member Cos      : ^T -> ^T)      and default ^T : float

        /// <summary>Hyperbolic cosine  of the given number</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The hyperbolic cosine of the input.</returns>
        [<CompiledName("Cosh")>]
        val inline cosh     : value:^T -> ^T       when ^T : (static member Cosh     : ^T -> ^T)      and default ^T : float
        
        /// <summary>Sine of the given number</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The sine of the input.</returns>
        [<CompiledName("Sin")>]
        val inline sin      : value:^T -> ^T       when ^T : (static member Sin      : ^T -> ^T)      and default ^T : float
        
        /// <summary>Hyperbolic sine of the given number</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The hyperbolic sine of the input.</returns>
        [<CompiledName("Sinh")>]
        val inline sinh     : value:^T -> ^T       when ^T : (static member Sinh     : ^T -> ^T)      and default ^T : float
        
        /// <summary>Tangent of the given number</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The tangent of the input.</returns>
        [<CompiledName("Tan")>]
        val inline tan      : value:^T -> ^T       when ^T : (static member Tan      : ^T -> ^T)      and default ^T : float
        
        /// <summary>Hyperbolic tangent of the given number</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The hyperbolic tangent of the input.</returns>
        [<CompiledName("Tanh")>]
        val inline tanh     : value:^T -> ^T       when ^T : (static member Tanh     : ^T -> ^T)      and default ^T : float

        /// <summary>Overloaded truncate operator.</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The truncated value.</returns>
        [<CompiledName("Truncate")>]
        val inline truncate : value:^T -> ^T       when ^T : (static member Truncate : ^T -> ^T)      and default ^T : float

        /// <summary>Overloaded power operator.</summary>
        ///
        /// <param name="x">The input base.</param>
        /// <param name="y">The input exponent.</param>
        ///
        /// <returns>The base raised to the exponent.</returns>
        val inline ( **  )  : x:^T -> y:^U -> ^T when ^T : (static member Pow : ^T * ^U -> ^T) and default ^U : float  and default ^T : float

        /// <summary>Overloaded power operator. If <c>n > 0</c> then equivalent to <c>x*...*x</c> for <c>n</c> occurrences of <c>x</c>. </summary>
        ///
        /// <param name="x">The input base.</param>
        /// <param name="n">The input exponent.</param>
        ///
        /// <returns>The base raised to the exponent.</returns>
        [<CompiledName("PowInteger")>]
        val inline pown  : x:^T -> n:int -> ^T when ^T : (static member One : ^T) 
                                               and  ^T : (static member ( * ) : ^T * ^T -> ^T) 
                                               and  ^T : (static member ( / ) : ^T * ^T -> ^T) 
                                               and default ^T : int

        /// <summary>Converts the argument to byte. This is a direct conversion for all 
        /// primitive numeric types. For strings, the input is converted using <c>Byte.Parse()</c> 
        /// with InvariantCulture settings. Otherwise the operation requires an appropriate
        /// static conversion method on the input type.</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The converted byte</returns>
        [<CompiledName("ToByte")>]
        val inline byte       : value:^T -> byte       when ^T : (static member op_Explicit : ^T -> byte)       and default ^T : int        
        
        /// <summary>Converts the argument to signed byte. This is a direct conversion for all 
        /// primitive numeric types. For strings, the input is converted using <c>SByte.Parse()</c>  
        /// with InvariantCulture settings. Otherwise the operation requires an appropriate
        /// static conversion method on the input type.</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The converted sbyte</returns>
        [<CompiledName("ToSByte")>]
        val inline sbyte      : value:^T -> sbyte      when ^T : (static member op_Explicit : ^T -> sbyte)      and default ^T : int
        
        /// <summary>Converts the argument to signed 16-bit integer. This is a direct conversion for all 
        /// primitive numeric types. For strings, the input is converted using <c>Int16.Parse()</c>  
        /// with InvariantCulture settings. Otherwise the operation requires an appropriate
        /// static conversion method on the input type.</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The converted int16</returns>
        [<CompiledName("ToInt16")>]
        val inline int16      : value:^T -> int16      when ^T : (static member op_Explicit : ^T -> int16)      and default ^T : int
        
        /// <summary>Converts the argument to unsigned 16-bit integer. This is a direct conversion for all 
        /// primitive numeric types. For strings, the input is converted using <c>UInt16.Parse()</c>  
        /// with InvariantCulture settings. Otherwise the operation requires an appropriate
        /// static conversion method on the input type.</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The converted uint16</returns>
        [<CompiledName("ToUInt16")>]
        val inline uint16     : value:^T -> uint16     when ^T : (static member op_Explicit : ^T -> uint16)     and default ^T : int
        
        /// <summary>Converts the argument to signed 32-bit integer. This is a direct conversion for all 
        /// primitive numeric types. For strings, the input is converted using <c>Int32.Parse()</c>  
        /// with InvariantCulture settings. Otherwise the operation requires an appropriate
        /// static conversion method on the input type.</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The converted int</returns>
        [<CompiledName("ToInt")>]
        val inline int        : value:^T -> int        when ^T : (static member op_Explicit : ^T -> int)        and default ^T : int
        
        /// <summary>Converts the argument to an unsigned 32-bit integer. This is a direct conversion for all 
        /// primitive numeric types. For strings, the input is converted using <c>UInt32.Parse()</c>  
        /// with InvariantCulture settings. Otherwise the operation requires an appropriate
        /// static conversion method on the input type.</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The converted int</returns>
        [<CompiledName("ToUInt")>]
        val inline uint: value:^T -> uint when ^T: (static member op_Explicit: ^T -> uint) and default ^T: uint

        /// <summary>Converts the argument to a particular enum type.</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The converted enum type.</returns>
        [<CompiledName("ToEnum")>]
        val inline enum       : value:int32 -> ^U        when ^U : enum<int32> 

        /// <summary>Converts the argument to signed 32-bit integer. This is a direct conversion for all 
        /// primitive numeric types. For strings, the input is converted using <c>Int32.Parse()</c>  
        /// with InvariantCulture settings. Otherwise the operation requires an appropriate
        /// static conversion method on the input type.</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The converted int32</returns>
        [<CompiledName("ToInt32")>]
        val inline int32      : value:^T -> int32      when ^T : (static member op_Explicit : ^T -> int32)      and default ^T : int

        /// <summary>Converts the argument to unsigned 32-bit integer. This is a direct conversion for all 
        /// primitive numeric types. For strings, the input is converted using <c>UInt32.Parse()</c>  
        /// with InvariantCulture settings. Otherwise the operation requires an appropriate
        /// static conversion method on the input type.</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The converted uint32</returns>
        [<CompiledName("ToUInt32")>]
        val inline uint32     : value:^T -> uint32     when ^T : (static member op_Explicit : ^T -> uint32)     and default ^T : int

        /// <summary>Converts the argument to signed 64-bit integer. This is a direct conversion for all 
        /// primitive numeric types. For strings, the input is converted using <c>Int64.Parse()</c> 
        /// with InvariantCulture settings. Otherwise the operation requires an appropriate
        /// static conversion method on the input type.</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The converted int64</returns>
        [<CompiledName("ToInt64")>]
        val inline int64      : value:^T -> int64      when ^T : (static member op_Explicit : ^T -> int64)      and default ^T : int

        /// <summary>Converts the argument to unsigned 64-bit integer. This is a direct conversion for all 
        /// primitive numeric types. For strings, the input is converted using <c>UInt64.Parse()</c>  
        /// with InvariantCulture settings. Otherwise the operation requires an appropriate
        /// static conversion method on the input type.</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The converted uint64</returns>
        [<CompiledName("ToUInt64")>]
        val inline uint64     : value:^T -> uint64     when ^T : (static member op_Explicit : ^T -> uint64)     and default ^T : int

        /// <summary>Converts the argument to 32-bit float. This is a direct conversion for all 
        /// primitive numeric types. For strings, the input is converted using <c>Single.Parse()</c>  
        /// with InvariantCulture settings. Otherwise the operation requires an appropriate
        /// static conversion method on the input type.</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The converted float32</returns>
        [<CompiledName("ToSingle")>]
        val inline float32    : value:^T -> float32    when ^T : (static member op_Explicit : ^T -> float32)    and default ^T : int

        /// <summary>Converts the argument to 64-bit float. This is a direct conversion for all 
        /// primitive numeric types. For strings, the input is converted using <c>Double.Parse()</c>  
        /// with InvariantCulture settings. Otherwise the operation requires an appropriate
        /// static conversion method on the input type.</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The converted float</returns>
        [<CompiledName("ToDouble")>]
        val inline float      : value:^T -> float      when ^T : (static member op_Explicit : ^T -> float)      and default ^T : int

        /// <summary>Converts the argument to signed native integer. This is a direct conversion for all 
        /// primitive numeric types. Otherwise the operation requires an appropriate
        /// static conversion method on the input type.</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The converted nativeint</returns>
        [<CompiledName("ToIntPtr")>]
        val inline nativeint  : value:^T -> nativeint  when ^T : (static member op_Explicit : ^T -> nativeint)  and default ^T : int

        /// <summary>Converts the argument to unsigned native integer using a direct conversion for all 
        /// primitive numeric types. Otherwise the operation requires an appropriate
        /// static conversion method on the input type.</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The converted unativeint</returns>
        [<CompiledName("ToUIntPtr")>]
        val inline unativeint : value:^T -> unativeint when ^T : (static member op_Explicit : ^T -> unativeint) and default ^T : int
        
        /// <summary>Converts the argument to a string using <c>ToString</c>.</summary>
        ///
        /// <remarks>For standard integer and floating point values the and any type that implements <c>IFormattable</c>
        /// <c>ToString</c> conversion uses <c>CultureInfo.InvariantCulture</c>. </remarks>
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The converted string.</returns>
        [<CompiledName("ToString")>]
        val inline string  : value:'T -> string

        /// <summary>Converts the argument to System.Decimal using a direct conversion for all 
        /// primitive numeric types. For strings, the input is converted using <c>UInt64.Parse()</c>  
        /// with InvariantCulture settings. Otherwise the operation requires an appropriate
        /// static conversion method on the input type.</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The converted decimal.</returns>
        [<CompiledName("ToDecimal")>]
        val inline decimal : value:^T -> decimal when ^T : (static member op_Explicit : ^T -> decimal) and default ^T : int

        /// <summary>Converts the argument to character. Numeric inputs are converted according to the UTF-16 
        /// encoding for characters. String inputs must be exactly one character long. For other
        /// input types the operation requires an appropriate static conversion method on the input type.</summary>
        ///
        /// <param name="value">The input value.</param>
        ///
        /// <returns>The converted char.</returns>
        [<CompiledName("ToChar")>]
        val inline char        : value:^T -> char      when ^T : (static member op_Explicit : ^T -> char)        and default ^T : int

        /// <summary>An active pattern to match values of type <see cref="T:System.Collections.Generic.KeyValuePair"/></summary>
        ///
        /// <param name="keyValuePair">The input key/value pair.</param>
        ///
        /// <returns>A tuple containing the key and value.</returns>
        [<CompiledName("KeyValuePattern")>]
        val ( |KeyValue| ): keyValuePair:KeyValuePair<'Key,'Value> -> 'Key * 'Value

        [<AutoOpen>]
        [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
        /// <summary>Contains extension methods to allow the use of F# indexer notation with arrays.
        /// This module is automatically opened in all F# code.</summary>
        module ArrayExtensions = 
            type ``[,,,]``<'T> with
                /// <summary>Get the index for the element offset elements away from the end of the collection.</summary>
                ///
                /// <param name="rank">The rank of the index. This refers to the dimension in the 4d array.</param>
                /// <param name="offset">The offset from the end.</param>
                ///
                /// <returns>The corresponding index from the start.</returns>
                [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
                member GetReverseIndex: rank: int * offset: int -> int

            type ``[,,]``<'T> with
                /// <summary>Get the index for the element offset elements away from the end of the collection.</summary>
                ///
                /// <param name="rank">The rank of the index. This refers to the dimension in the 3d array.</param>
                /// <param name="offset">The offset from the end.</param>
                ///
                /// <returns>The corresponding index from the start.</returns>
                [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
                member GetReverseIndex: rank: int * offset: int -> int

            type ``[,]``<'T> with
                /// <summary>Get the index for the element offset elements away from the end of the collection.</summary>
                ///
                /// <param name="rank">The rank of the index. This refers to the dimension in the 2d array.</param>
                /// <param name="offset">The offset from the end.</param>
                ///
                /// <returns>The corresponding index from the start.</returns>
                [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
                member GetReverseIndex: rank: int * offset: int -> int

            type ``[]``<'T> with
                /// <summary>Get the index for the element offset elements away from the end of the collection.</summary>
                ///
                /// <param name="rank">The rank of the index.</param>
                /// <param name="offset">The offset from the end.</param>
                ///
                /// <returns>The corresponding index from the start.</returns>
                [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
                member GetReverseIndex: rank: int * offset: int -> int

            type System.String with
                /// <summary>Get the index for the element offset elements away from the end of the collection.</summary>
                ///
                /// <param name="rank">The rank of the index.</param>
                /// <param name="offset">The offset from the end.</param>
                ///
                /// <returns>The corresponding index from the start.</returns>
                [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
                member GetReverseIndex: rank: int * offset: int -> int


        /// <summary>A module of compiler intrinsic functions for efficient implementations of F# integer ranges
        /// and dynamic invocations of other F# operators</summary>
        module OperatorIntrinsics =

            /// <summary>Gets a slice of an array</summary>
            ///
            /// <param name="source">The input array.</param>
            /// <param name="start">The start index.</param>
            /// <param name="finish">The end index.</param>
            ///
            /// <returns>The sub array from the input indices.</returns>
            val inline GetArraySlice : source:'T[] -> start:int option -> finish:int option -> 'T[] 

            /// <summary>Sets a slice of an array</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="start">The start index.</param>
            /// <param name="finish">The end index.</param>
            /// <param name="source">The source array.</param>
            val inline SetArraySlice : target:'T[] -> start:int option -> finish:int option -> source:'T[] -> unit

            /// <summary>Gets a region slice of an array</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            ///
            /// <returns>The two dimensional sub array from the input indices.</returns>
            val inline GetArraySlice2D : source:'T[,] -> start1:int option -> finish1:int option -> start2:int option -> finish2:int option -> 'T[,]

            /// <summary>Gets a vector slice of a 2D array. The index of the first dimension is fixed.</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="index1">The index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            ///
            /// <returns>The sub array from the input indices.</returns>
            val inline GetArraySlice2DFixed1 : source:'T[,] -> index1:int -> start2:int option -> finish2:int option -> 'T[]

            /// <summary>Gets a vector slice of a 2D array. The index of the second dimension is fixed.</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="index2">The fixed index of the second dimension.</param>
            ///
            /// <returns>The sub array from the input indices.</returns>
            val inline GetArraySlice2DFixed2 : source:'T[,] -> start1:int option -> finish1:int option -> index2: int -> 'T[]

            /// <summary>Sets a region slice of an array</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="source">The source array.</param>
            val inline SetArraySlice2D : target:'T[,] -> start1:int option -> finish1:int option -> start2:int option -> finish2:int option -> source:'T[,] -> unit

            /// <summary>Sets a vector slice of a 2D array. The index of the first dimension is fixed.</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="index1">The index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="source">The source array.</param>
            val inline SetArraySlice2DFixed1 : target:'T[,] -> index1:int -> start2:int option -> finish2:int option -> source:'T[] -> unit

            /// <summary>Sets a vector slice of a 2D array. The index of the second dimension is fixed.</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="index2">The index of the second dimension.</param>
            /// <param name="source">The source array.</param>
            val inline SetArraySlice2DFixed2 : target:'T[,] -> start1:int option -> finish1:int option -> index2:int -> source:'T[] -> unit

            /// <summary>Gets a slice of an array</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            ///
            /// <returns>The three dimensional sub array from the given indices.</returns>
            val inline GetArraySlice3D : source:'T[,,] -> start1:int option -> finish1:int option -> start2:int option -> finish2:int option -> start3:int option -> finish3:int option -> 'T[,,]

            /// <summary>Gets a 2D slice of a 3D array.</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="index1">The fixed index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            ///
            /// <returns>The two dimensional sub array from the given indices.</returns>
            [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
            val inline GetArraySlice3DFixedSingle1 : source:'T[,,] ->  index1:int -> start2:int option -> finish2:int option -> start3:int option -> finish3:int option -> 'T[,]

            /// <summary>Gets a 2D slice of a 3D array.</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="index2">The fixed index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            ///
            /// <returns>The two dimensional sub array from the given indices.</returns>
            [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
            val inline GetArraySlice3DFixedSingle2 : source:'T[,,] ->  start1:int option -> finish1:int option -> index2: int -> start3:int option -> finish3:int option -> 'T[,]

            /// <summary>Gets a 2D slice of a 3D array.</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="index3">The fixed index of the third dimension.</param>
            ///
            /// <returns>The two dimensional sub array from the given indices.</returns>
            [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
            val inline GetArraySlice3DFixedSingle3 : source:'T[,,] ->  start1:int option -> finish1:int option -> start2:int option -> finish2:int option -> index3: int -> 'T[,]

            /// <summary>Gets a 1D slice of a 3D array.</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="index1">The fixed index of the first dimension.</param>
            /// <param name="index2">The fixed index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            ///
            /// <returns>The one dimensional sub array from the given indices.</returns>
            [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
            val inline GetArraySlice3DFixedDouble1 : source:'T[,,] ->  index1:int -> index2:int -> start3:int option -> finish3:int option -> 'T[]

            /// <summary>Gets a 1D slice of a 3D array.</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="index1">The fixed index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="index3">The fixed index of the third dimension.</param>
            ///
            /// <returns>The one dimensional sub array from the given indices.</returns>
            [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
            val inline GetArraySlice3DFixedDouble2 : source:'T[,,] ->  index1:int -> start2:int option -> finish2:int option -> index3:int -> 'T[]

            /// <summary>Gets a 1D slice of a 3D array.</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="index2">The fixed index of the second dimension.</param>
            /// <param name="index3">The fixed index of the third dimension.</param>
            ///
            /// <returns>The one dimensional sub array from the given indices.</returns>
            [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
            val inline GetArraySlice3DFixedDouble3 : source:'T[,,] ->  start1:int option -> finish1:int option -> index2:int -> index3:int -> 'T[]

            /// <summary>Sets a slice of an array</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            /// <param name="source">The source array.</param>
            val inline SetArraySlice3D : target:'T[,,] -> start1:int option -> finish1:int option -> start2:int option -> finish2:int option -> start3:int option -> finish3:int option -> source:'T[,,] -> unit

            /// <summary>Sets a 2D slice of a 3D array</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="index1">The fixed index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            /// <param name="source">The source array.</param>
            ///
            /// <returns>The two dimensional sub array from the given indices.</returns>
            [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
            val inline SetArraySlice3DFixedSingle1 : target: 'T[,,] -> index1: int -> start2: int option -> finish2: int option -> start3: int option -> finish3: int option -> source: 'T[,] -> unit

            /// <summary>Sets a 2D slice of a 3D array</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="index2">The fixed index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            /// <param name="source">The source array.</param>
            ///
            /// <returns>The two dimensional sub array from the given indices.</returns>
            [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
            val inline SetArraySlice3DFixedSingle2 : target: 'T[,,] -> start1: int option -> finish1: int option -> index2: int -> start3: int option -> finish3: int option -> source: 'T[,] -> unit

            /// <summary>Sets a 2D slice of a 3D array</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="index3">The fixed index of the third dimension.</param>
            /// <param name="source">The source array.</param>
            ///
            /// <returns>The two dimensional sub array from the given indices.</returns>
            [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
            val inline SetArraySlice3DFixedSingle3 : target: 'T[,,] -> start1: int option -> finish1: int option ->  start2: int option -> finish2: int option -> index3: int -> source: 'T[,] -> unit

            /// <summary>Sets a 1D slice of a 3D array.</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="index1">The fixed index of the first dimension.</param>
            /// <param name="index2">The fixed index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            /// <param name="source">The source array.</param>
            ///
            /// <returns>The one dimensional sub array from the given indices.</returns>
            [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
            val inline SetArraySlice3DFixedDouble1 : target: 'T[,,] -> index1: int -> index2: int -> start3: int option -> finish3: int option -> source: 'T[] -> unit

            /// <summary>Sets a 1D slice of a 3D array.</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="index1">The fixed index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="index3">The fixed index of the third dimension.</param>
            /// <param name="source">The source array.</param>
            ///
            /// <returns>The one dimensional sub array from the given indices.</returns>
            [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
            val inline SetArraySlice3DFixedDouble2 : target: 'T[,,] -> index1: int -> start2: int option -> finish2: int option -> index3: int -> source: 'T[] -> unit

            /// <summary>Sets a 1D slice of a 3D array.</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="index2">The fixed index of the second dimension.</param>
            /// <param name="index3">The fixed index of the third dimension.</param>
            /// <param name="source">The source array.</param>
            ///
            /// <returns>The one dimensional sub array from the given indices.</returns>
            [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
            val inline SetArraySlice3DFixedDouble3 : target: 'T[,,] -> start1: int option -> finish1: int option ->  index2: int -> index3: int -> source: 'T[] -> unit

            /// <summary>Gets a slice of an array</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            /// <param name="start4">The start index of the fourth dimension.</param>
            /// <param name="finish4">The end index of the fourth dimension.</param>
            ///
            /// <returns>The four dimensional sub array from the given indices.</returns>
            val inline GetArraySlice4D : source:'T[,,,] -> start1:int option -> finish1:int option -> start2:int option -> finish2:int option -> start3:int option -> finish3:int option -> start4:int option -> finish4:int option -> 'T[,,,]

            /// <summary>Gets a 3D slice of a 4D array</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="index1">The fixed index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            /// <param name="start4">The start index of the fourth dimension.</param>
            /// <param name="finish4">The end index of the fourth dimension.</param>
            ///
            /// <returns>The three dimensional sub array from the given indices.</returns>
            val inline GetArraySlice4DFixedSingle1 : source:'T[,,,] -> index1:int -> start2: int option -> finish2:int option -> start3:int option -> finish3:int option -> start4:int option -> finish4:int option -> 'T[,,]

            /// <summary>Gets a 3D slice of a 4D array</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="index2">The fixed index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            /// <param name="start4">The start index of the fourth dimension.</param>
            /// <param name="finish4">The end index of the fourth dimension.</param>
            ///
            /// <returns>The three dimensional sub array from the given indices.</returns>
            val inline GetArraySlice4DFixedSingle2 : source:'T[,,,] -> start1:int option -> finish1:int option -> index2:int -> start3:int option -> finish3:int option -> start4:int option -> finish4:int option -> 'T[,,]

            /// <summary>Gets a 3D slice of a 4D array</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="index3">The fixed index of the third dimension.</param>
            /// <param name="start4">The start index of the fourth dimension.</param>
            /// <param name="finish4">The end index of the fourth dimension.</param>
            ///
            /// <returns>The three dimensional sub array from the given indices.</returns>
            val inline GetArraySlice4DFixedSingle3 : source:'T[,,,] -> start1:int option -> finish1:int option -> start2:int option -> finish2:int option -> index3:int  -> start4:int option -> finish4:int option -> 'T[,,]

            /// <summary>Gets a 3D slice of a 4D array</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            /// <param name="index4">The fixed index of the fourth dimension.</param>
            ///
            /// <returns>The three dimensional sub array from the given indices.</returns>
            val inline GetArraySlice4DFixedSingle4 : source:'T[,,,] -> start1:int option -> finish1:int option -> start2: int option -> finish2:int option -> start3:int option -> finish3:int option -> index4:int -> 'T[,,]

            /// <summary>Gets a 2D slice of a 4D array</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="index1">The fixed index of the first dimension.</param>
            /// <param name="index2">The fixed index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            /// <param name="start4">The start index of the fourth dimension.</param>
            /// <param name="finish4">The end index of the fourth dimension.</param>
            ///
            /// <returns>The two dimensional sub array from the given indices.</returns>
            val inline GetArraySlice4DFixedDouble1 : source:'T[,,,] -> index1: int -> index2:int -> start3:int option -> finish3:int option -> start4:int option -> finish4:int option -> 'T[,]

            /// <summary>Gets a 2D slice of a 4D array</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="index1">The fixed index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="index3">The fixed index of the third dimension.</param>
            /// <param name="start4">The start index of the fourth dimension.</param>
            /// <param name="finish4">The end index of the fourth dimension.</param>
            ///
            /// <returns>The two dimensional sub array from the given indices.</returns>
            val inline GetArraySlice4DFixedDouble2 : source:'T[,,,] -> index1: int -> start2: int option -> finish2:int option -> index3:int -> start4:int option -> finish4:int option -> 'T[,]

            /// <summary>Gets a 2D slice of a 4D array</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="index1">The fixed index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            /// <param name="index4">The fixed index of the fourth dimension.</param>
            ///
            /// <returns>The two dimensional sub array from the given indices.</returns>
            val inline GetArraySlice4DFixedDouble3 : source:'T[,,,] -> index1:int -> start2: int option -> finish2:int option -> start3:int option -> finish3:int option -> index4:int -> 'T[,]

            /// <summary>Gets a 2D slice of a 4D array</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="index2">The fixed index of the second dimension.</param>
            /// <param name="index3">The fixed index of the third dimension.</param>
            /// <param name="start4">The start index of the fourth dimension.</param>
            /// <param name="finish4">The end index of the fourth dimension.</param>
            ///
            /// <returns>The two dimensional sub array from the given indices.</returns>
            val inline GetArraySlice4DFixedDouble4 : source:'T[,,,] -> start1:int option -> finish1:int option -> index2:int -> index3:int -> start4:int option -> finish4:int option -> 'T[,]

            /// <summary>Gets a 2D slice of a 4D array</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="index2">The fixed index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            /// <param name="index4">The fixed index of the fourth dimension.</param>
            ///
            /// <returns>The two dimensional sub array from the given indices.</returns>
            val inline GetArraySlice4DFixedDouble5 : source:'T[,,,] -> start1:int option -> finish1:int option -> index2:int -> start3:int option -> finish3:int option -> index4:int -> 'T[,]

            /// <summary>Gets a 2D slice of a 4D array</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="index3">The fixed index of the third dimension.</param>
            /// <param name="index4">The fixed index of the fourth dimension.</param>
            ///
            /// <returns>The two dimensional sub array from the given indices.</returns>
            val inline GetArraySlice4DFixedDouble6 : source:'T[,,,] -> start1:int option -> finish1:int option -> start2: int option -> finish2:int option -> index3:int -> index4:int -> 'T[,]

            /// <summary>Gets a 1D slice of a 4D array</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="index1">The fixed index of the first dimension.</param>
            /// <param name="index2">The fixed index of the second dimension.</param>
            /// <param name="index3">The fixed index of the third dimension.</param>
            /// <param name="start4">The start index of the fourth dimension.</param>
            /// <param name="finish4">The end index of the fourth dimension.</param>
            ///
            /// <returns>The one dimensional sub array from the given indices.</returns>
            val inline GetArraySlice4DFixedTriple4 : source:'T[,,,] -> index1:int -> index2:int -> index3:int -> start4:int option -> finish4:int option -> 'T[]

            /// <summary>Gets a 1D slice of a 4D array</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="index1">The fixed index of the first dimension.</param>
            /// <param name="index2">The fixed index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            /// <param name="index4">The fixed index of the fourth dimension.</param>
            ///
            /// <returns>The one dimensional sub array from the given indices.</returns>
            val inline GetArraySlice4DFixedTriple3 : source:'T[,,,] -> index1:int -> index2:int -> start3:int option -> finish3:int option -> index4:int -> 'T[]
            
            /// <summary>Gets a 1D slice of a 4D array</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="index1">The fixed index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="index3">The fixed index of the third dimension.</param>
            /// <param name="index4">The fixed index of the fourth dimension.</param>
            ///
            /// <returns>The one dimensional sub array from the given indices.</returns>
            val inline GetArraySlice4DFixedTriple2 : source:'T[,,,] -> index1:int -> start2: int option -> finish2:int option -> index3:int -> index4:int -> 'T[]

            /// <summary>Gets a 1D slice of a 4D array</summary>
            ///
            /// <param name="source">The source array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="index2">The fixed index of the second dimension.</param>
            /// <param name="index3">The fixed index of the third dimension.</param>
            /// <param name="index4">The fixed index of the fourth dimension.</param>
            ///
            /// <returns>The one dimensional sub array from the given indices.</returns>
            val inline GetArraySlice4DFixedTriple1 : source:'T[,,,] -> start1:int option -> finish1:int option -> index2:int -> index3:int -> index4:int -> 'T[]
            
            /// <summary>Sets a 3D slice of a 4D array</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="index1">The fixed index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            /// <param name="start4">The start index of the fourth dimension.</param>
            /// <param name="finish4">The end index of the fourth dimension.</param>
            /// <param name="source">The source array.</param>
            val inline SetArraySlice4DFixedSingle1 : target:'T[,,,] -> index1:int -> start2: int option -> finish2:int option -> start3:int option -> finish3:int option -> start4:int option -> finish4:int option -> source: 'T[,,] -> unit

            /// <summary>Sets a 3D slice of a 4D array</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="index2">The fixed index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            /// <param name="start4">The start index of the fourth dimension.</param>
            /// <param name="finish4">The end index of the fourth dimension.</param>
            /// <param name="source">The source array.</param>
            val inline SetArraySlice4DFixedSingle2 : target:'T[,,,] -> start1:int option -> finish1:int option -> index2:int -> start3:int option -> finish3:int option -> start4:int option -> finish4:int option -> source: 'T[,,] -> unit

            /// <summary>Sets a 3D slice of a 4D array</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="index3">The fixed index of the third dimension.</param>
            /// <param name="start4">The start index of the fourth dimension.</param>
            /// <param name="finish4">The end index of the fourth dimension.</param>
            /// <param name="source">The source array.</param>
            val inline SetArraySlice4DFixedSingle3 : target:'T[,,,] -> start1:int option -> finish1:int option -> start2:int option -> finish2:int option -> index3:int  -> start4:int option -> finish4:int option -> source: 'T[,,] -> unit

            /// <summary>Sets a 3D slice of a 4D array</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            /// <param name="index4">The fixed index of the fourth dimension.</param>
            /// <param name="source">The source array.</param>
            val inline SetArraySlice4DFixedSingle4 : target:'T[,,,] -> start1:int option -> finish1:int option -> start2: int option -> finish2:int option -> start3:int option -> finish3:int option -> index4:int -> source: 'T[,,] -> unit

            /// <summary>Sets a 2D slice of a 4D array</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="index1">The fixed index of the first dimension.</param>
            /// <param name="index2">The fixed index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            /// <param name="start4">The start index of the fourth dimension.</param>
            /// <param name="finish4">The end index of the fourth dimension.</param>
            /// <param name="source">The source array.</param>
            val inline SetArraySlice4DFixedDouble1 : target:'T[,,,] -> index1: int -> index2:int -> start3:int option -> finish3:int option -> start4:int option -> finish4:int option -> source: 'T[,] -> unit

            /// <summary>Sets a 2D slice of a 4D array</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="index1">The fixed index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="index3">The fixed index of the third dimension.</param>
            /// <param name="start4">The start index of the fourth dimension.</param>
            /// <param name="finish4">The end index of the fourth dimension.</param>
            /// <param name="source">The source array.</param>
            val inline SetArraySlice4DFixedDouble2 : target:'T[,,,] -> index1: int -> start2: int option -> finish2:int option -> index3:int -> start4:int option -> finish4:int option -> source: 'T[,] -> unit

            /// <summary>Sets a 2D slice of a 4D array</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="index1">The fixed index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            /// <param name="index4">The fixed index of the fourth dimension.</param>
            /// <param name="source">The source array.</param>
            val inline SetArraySlice4DFixedDouble3 : target:'T[,,,] -> index1:int -> start2: int option -> finish2:int option -> start3:int option -> finish3:int option -> index4:int -> source: 'T[,] -> unit

            /// <summary>Sets a 2D slice of a 4D array</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="index2">The fixed index of the second dimension.</param>
            /// <param name="index3">The fixed index of the third dimension.</param>
            /// <param name="start4">The start index of the fourth dimension.</param>
            /// <param name="finish4">The end index of the fourth dimension.</param>
            /// <param name="source">The source array.</param>
            val inline SetArraySlice4DFixedDouble4 : target:'T[,,,] -> start1:int option -> finish1:int option -> index2:int -> index3:int -> start4:int option -> finish4:int option -> source: 'T[,] -> unit

            /// <summary>Sets a 2D slice of a 4D array</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="index2">The fixed index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            /// <param name="index4">The fixed index of the fourth dimension.</param>
            /// <param name="source">The source array.</param>
            val inline SetArraySlice4DFixedDouble5 : target:'T[,,,] -> start1:int option -> finish1:int option -> index2:int -> start3:int option -> finish3:int option -> index4:int -> source: 'T[,] -> unit

            /// <summary>Sets a 2D slice of a 4D array</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="index3">The fixed index of the third dimension.</param>
            /// <param name="index4">The fixed index of the fourth dimension.</param>
            /// <param name="source">The source array.</param>
            val inline SetArraySlice4DFixedDouble6 : target:'T[,,,] -> start1:int option -> finish1:int option -> start2: int option -> finish2:int option -> index3:int -> index4:int -> source: 'T[,] -> unit

            /// <summary>Sets a 1D slice of a 4D array</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="index1">The fixed index of the first dimension.</param>
            /// <param name="index2">The fixed index of the second dimension.</param>
            /// <param name="index3">The fixed index of the third dimension.</param>
            /// <param name="start4">The start index of the fourth dimension.</param>
            /// <param name="finish4">The end index of the fourth dimension.</param>
            /// <param name="source">The source array.</param>
            val inline SetArraySlice4DFixedTriple4 : target:'T[,,,] -> index1:int -> index2:int -> index3:int -> start4:int option -> finish4:int option -> source: 'T[] -> unit

            /// <summary>Sets a 1D slice of a 4D array</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="index1">The fixed index of the first dimension.</param>
            /// <param name="index2">The fixed index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            /// <param name="index4">The fixed index of the fourth dimension.</param>
            /// <param name="source">The source array.</param>
            val inline SetArraySlice4DFixedTriple3 : target:'T[,,,] -> index1:int -> index2:int -> start3:int option -> finish3:int option -> index4:int -> source: 'T[] -> unit
            
            /// <summary>Sets a 1D slice of a 4D array</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="index1">The fixed index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="index3">The fixed index of the third dimension.</param>
            /// <param name="index4">The fixed index of the fourth dimension.</param>
            /// <param name="source">The source array.</param>
            val inline SetArraySlice4DFixedTriple2 : target:'T[,,,] -> index1:int -> start2: int option -> finish2:int option -> index3:int -> index4:int -> source: 'T[] -> unit

            /// <summary>Sets a 1D slice of a 4D array</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="index2">The fixed index of the second dimension.</param>
            /// <param name="index3">The fixed index of the third dimension.</param>
            /// <param name="index4">The fixed index of the fourth dimension.</param>
            /// <param name="source">The source array.</param>
            val inline SetArraySlice4DFixedTriple1 : target:'T[,,,] -> start1:int option -> finish1:int option -> index2:int -> index3:int -> index4:int -> source: 'T[] -> unit

            /// <summary>Sets a slice of an array</summary>
            ///
            /// <param name="target">The target array.</param>
            /// <param name="start1">The start index of the first dimension.</param>
            /// <param name="finish1">The end index of the first dimension.</param>
            /// <param name="start2">The start index of the second dimension.</param>
            /// <param name="finish2">The end index of the second dimension.</param>
            /// <param name="start3">The start index of the third dimension.</param>
            /// <param name="finish3">The end index of the third dimension.</param>
            /// <param name="start4">The start index of the fourth dimension.</param>
            /// <param name="finish4">The end index of the fourth dimension.</param>
            /// <param name="source">The source array.</param>
            val inline SetArraySlice4D : target:'T[,,,] -> start1:int option -> finish1:int option -> start2:int option -> finish2:int option -> start3:int option -> finish3:int option -> start4:int option -> finish4:int option -> source:'T[,,,] -> unit

            /// <summary>Gets a slice from a string</summary>
            ///
            /// <param name="source">The source string.</param>
            /// <param name="start">The index of the first character of the slice.</param>
            /// <param name="finish">The index of the last character of the slice.</param>
            ///
            /// <returns>The substring from the given indices.</returns>
            val inline GetStringSlice : source:string -> start:int option -> finish:int option -> string

            /// <summary>Generate a range of integers</summary>  
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val RangeInt32        : start:int        -> step:int        -> stop:int        -> seq<int>  

            /// <summary>Generate a range of float values</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val RangeDouble      : start:float      -> step:float      -> stop:float      -> seq<float>

            /// <summary>Generate a range of float32 values</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val RangeSingle    : start:float32    -> step:float32    -> stop:float32    -> seq<float32> 

            /// <summary>Generate a range of int64 values</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val RangeInt64      : start:int64      -> step:int64      -> stop:int64      -> seq<int64> 

            /// <summary>Generate a range of uint64 values</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val RangeUInt64     : start:uint64     -> step:uint64     -> stop:uint64     -> seq<uint64> 

            /// <summary>Generate a range of uint32 values</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val RangeUInt32     : start:uint32     -> step:uint32     -> stop:uint32     -> seq<uint32> 

            /// <summary>Generate a range of nativeint values</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val RangeIntPtr  : start:nativeint  -> step:nativeint  -> stop:nativeint  -> seq<nativeint> 

            /// <summary>Generate a range of unativeint values</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val RangeUIntPtr : start:unativeint -> step:unativeint -> stop:unativeint -> seq<unativeint> 

            /// <summary>Generate a range of int16 values</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val RangeInt16      : start:int16      -> step:int16      -> stop:int16      -> seq<int16> 

            /// <summary>Generate a range of uint16 values</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val RangeUInt16     : start:uint16     -> step:uint16     -> stop:uint16     -> seq<uint16> 

            /// <summary>Generate a range of sbyte values</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val RangeSByte      : start:sbyte      -> step:sbyte      -> stop:sbyte      -> seq<sbyte> 

            /// <summary>Generate a range of byte values</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val RangeByte       : start:byte       -> step:byte       -> stop:byte       -> seq<byte> 

            /// <summary>Generate a range of char values</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val RangeChar       : start:char                          -> stop:char       -> seq<char> 

            /// <summary>Generate a range of values using the given zero, add, start, step and stop values</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val RangeGeneric   : one:'T -> add:('T -> 'T -> 'T) -> start:'T   -> stop:'T       -> seq<'T> 

            /// <summary>Generate a range of values using the given zero, add, start, step and stop values</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val RangeStepGeneric   : zero:'Step -> add:('T -> 'Step -> 'T) -> start:'T   -> step:'Step -> stop:'T       -> seq<'T> 

            /// <summary>This is a library intrinsic. Calls to this function may be generated by evaluating quotations.</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val AbsDynamic : x:'T -> 'T 

            /// <summary>This is a library intrinsic. Calls to this function may be generated by evaluating quotations.</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val AcosDynamic : x:'T -> 'T 

            /// <summary>This is a library intrinsic. Calls to this function may be generated by evaluating quotations.</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val AsinDynamic : x:'T -> 'T 

            /// <summary>This is a library intrinsic. Calls to this function may be generated by evaluating quotations.</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val AtanDynamic : x:'T -> 'T 

            /// <summary>This is a library intrinsic. Calls to this function may be generated by evaluating quotations.</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val Atan2Dynamic : y:'T1 -> x:'T1 -> 'T2

            /// <summary>This is a library intrinsic. Calls to this function may be generated by evaluating quotations.</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val CeilingDynamic : x:'T -> 'T 

            /// <summary>This is a library intrinsic. Calls to this function may be generated by evaluating quotations.</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val ExpDynamic : x:'T -> 'T 

            /// <summary>This is a library intrinsic. Calls to this function may be generated by evaluating quotations.</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val FloorDynamic : x:'T -> 'T 

            /// <summary>This is a library intrinsic. Calls to this function may be generated by evaluating quotations.</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val TruncateDynamic : x:'T -> 'T 

            /// <summary>This is a library intrinsic. Calls to this function may be generated by evaluating quotations.</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val RoundDynamic : x:'T -> 'T 

            /// <summary>This is a library intrinsic. Calls to this function may be generated by evaluating quotations.</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val SignDynamic : 'T -> int

            /// <summary>This is a library intrinsic. Calls to this function may be generated by evaluating quotations.</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val LogDynamic : x:'T -> 'T 

            /// <summary>This is a library intrinsic. Calls to this function may be generated by evaluating quotations.</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val Log10Dynamic : x:'T -> 'T 

            /// <summary>This is a library intrinsic. Calls to this function may be generated by evaluating quotations.</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val SqrtDynamic : 'T1 -> 'T2

            /// <summary>This is a library intrinsic. Calls to this function may be generated by evaluating quotations.</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val CosDynamic : x:'T -> 'T 

            /// <summary>This is a library intrinsic. Calls to this function may be generated by evaluating quotations.</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val CoshDynamic : x:'T -> 'T 

            /// <summary>This is a library intrinsic. Calls to this function may be generated by evaluating quotations.</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val SinDynamic : x:'T -> 'T 

            /// <summary>This is a library intrinsic. Calls to this function may be generated by evaluating quotations.</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val SinhDynamic : x:'T -> 'T 

            /// <summary>This is a library intrinsic. Calls to this function may be generated by evaluating quotations.</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val TanDynamic : x:'T -> 'T 

            /// <summary>This is a library intrinsic. Calls to this function may be generated by evaluating quotations.</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val TanhDynamic : x:'T -> 'T 

            /// <summary>This is a library intrinsic. Calls to this function may be generated by evaluating quotations.</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val PowDynamic : x:'T -> y:'U -> 'T 
            
            /// <summary>This is a library intrinsic. Calls to this function may be generated by uses of the generic 'pown' operator on values of type 'byte'</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val PowByte : x:byte -> n:int -> byte

            /// <summary>This is a library intrinsic. Calls to this function may be generated by uses of the generic 'pown' operator on values of type 'sbyte'</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val PowSByte : x:sbyte -> n:int -> sbyte

            /// <summary>This is a library intrinsic. Calls to this function may be generated by uses of the generic 'pown' operator on values of type 'int16'</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val PowInt16 : x:int16 -> n:int -> int16

            /// <summary>This is a library intrinsic. Calls to this function may be generated by uses of the generic 'pown' operator on values of type 'uint16'</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val PowUInt16 : x:uint16 -> n:int -> uint16

            /// <summary>This is a library intrinsic. Calls to this function may be generated by uses of the generic 'pown' operator on values of type 'int32'</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val PowInt32 : x:int32 -> n:int -> int32

            /// <summary>This is a library intrinsic. Calls to this function may be generated by uses of the generic 'pown' operator on values of type 'uint32'</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val PowUInt32 : x:uint32 -> n:int -> uint32

            /// <summary>This is a library intrinsic. Calls to this function may be generated by uses of the generic 'pown' operator on values of type 'int64'</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val PowInt64 : x:int64 -> n:int -> int64

            /// <summary>This is a library intrinsic. Calls to this function may be generated by uses of the generic 'pown' operator on values of type 'uint64'</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val PowUInt64 : x:uint64 -> n:int -> uint64

            /// <summary>This is a library intrinsic. Calls to this function may be generated by uses of the generic 'pown' operator on values of type 'nativeint'</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val PowIntPtr : x:nativeint -> n:int -> nativeint

            /// <summary>This is a library intrinsic. Calls to this function may be generated by uses of the generic 'pown' operator on values of type 'unativeint'</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val PowUIntPtr : x:unativeint -> n:int -> unativeint

            /// <summary>This is a library intrinsic. Calls to this function may be generated by uses of the generic 'pown' operator on values of type 'float32'</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val PowSingle : x:float32 -> n:int -> float32

            /// <summary>This is a library intrinsic. Calls to this function may be generated by uses of the generic 'pown' operator on values of type 'float'</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val PowDouble : x:float -> n:int -> float

            /// <summary>This is a library intrinsic. Calls to this function may be generated by uses of the generic 'pown' operator on values of type 'decimal'</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val PowDecimal : x:decimal -> n:int -> decimal

            /// <summary>This is a library intrinsic. Calls to this function may be generated by uses of the generic 'pown' operator</summary>
            [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
            val PowGeneric : one:'T * mul: ('T -> 'T -> 'T) * value:'T * exponent:int -> 'T

        /// <summary>This module contains basic operations which do not apply runtime and/or static checks</summary>
        module Unchecked =

            /// <summary>Unboxes a strongly typed value. This is the inverse of <c>box</c>, unbox&lt;t&gt;(box&lt;t&gt; a) equals a.</summary>
            ///
            /// <param name="value">The boxed value.</param>
            ///
            /// <returns>The unboxed result.</returns>
            [<CompiledName("Unbox")>]
            val inline unbox<'T> : value: obj -> 'T

            /// <summary>Generate a default value for any type. This is null for reference types, 
            /// For structs, this is struct value where all fields have the default value. 
            /// This function is unsafe in the sense that some F# values do not have proper <c>null</c> values.</summary>
            [<RequiresExplicitTypeArguments>] 
            [<CompiledName("DefaultOf")>]
            val inline defaultof<'T> : 'T

            [<CompiledName("Compare")>]
            /// <summary>Perform generic comparison on two values where the type of the values is not 
            /// statically required to have the 'comparison' constraint. </summary>
            /// <returns>The result of the comparison.</returns>
            val inline compare : 'T -> 'T -> int

            [<CompiledName("Equals")>]
            /// <summary>Perform generic equality on two values where the type of the values is not 
            /// statically required to satisfy the 'equality' constraint. </summary>
            /// <returns>The result of the comparison.</returns>
            val inline equals : 'T -> 'T -> bool

            [<CompiledName("Hash")>]
            /// <summary>Perform generic hashing on a value where the type of the value is not 
            /// statically required to satisfy the 'equality' constraint. </summary>
            /// <returns>The computed hash value.</returns>
            val inline hash : 'T -> int

        /// <summary>A module of comparison and equality operators that are statically resolved, but which are not fully generic and do not make structural comparison. Opening this
        /// module may make code that relies on structural or generic comparison no longer compile.</summary>
        module NonStructuralComparison = 

            /// <summary>Compares the two values for less-than</summary>
            ///
            /// <param name="x">The first parameter.</param>
            /// <param name="y">The second parameter.</param>
            ///
            /// <returns>The result of the comparison.</returns>
            val inline ( < ) : x:^T -> y:^U -> bool when (^T or ^U) : (static member ( < ) : ^T * ^U    -> bool) 
        
            /// <summary>Compares the two values for greater-than</summary>
            ///
            /// <param name="x">The first parameter.</param>
            /// <param name="y">The second parameter.</param>
            ///
            /// <returns>The result of the comparison.</returns>
            val inline ( > ) : x:^T -> y:^U -> bool when (^T or ^U) : (static member ( > ) : ^T * ^U    -> bool) 
        
            /// <summary>Compares the two values for greater-than-or-equal</summary>
            ///
            /// <param name="x">The first parameter.</param>
            /// <param name="y">The second parameter.</param>
            ///
            /// <returns>The result of the comparison.</returns>
            val inline ( >= ) : x:^T -> y:^U -> bool when (^T or ^U) : (static member ( >= ) : ^T * ^U    -> bool) 
        
            /// <summary>Compares the two values for less-than-or-equal</summary>
            ///
            /// <param name="x">The first parameter.</param>
            /// <param name="y">The second parameter.</param>
            ///
            /// <returns>The result of the comparison.</returns>
            val inline ( <= ) : x:^T -> y:^U -> bool when (^T or ^U) : (static member ( <= ) : ^T * ^U    -> bool) 
        
            /// <summary>Compares the two values for equality</summary>
            ///
            /// <param name="x">The first parameter.</param>
            /// <param name="y">The second parameter.</param>
            ///
            /// <returns>The result of the comparison.</returns>
            val inline ( = ) : x:^T -> y:^T -> bool when ^T : (static member ( = ) : ^T * ^T    -> bool) 
        
            /// <summary>Compares the two values for inequality</summary>
            ///
            /// <param name="x">The first parameter.</param>
            /// <param name="y">The second parameter.</param>
            ///
            /// <returns>The result of the comparison.</returns>
            val inline ( <> ) : x:^T -> y:^T -> bool when ^T : (static member ( <> ) : ^T * ^T    -> bool) 

            /// <summary>Compares the two values</summary>
            ///
            /// <param name="e1">The first value.</param>
            /// <param name="e2">The second value.</param>
            ///
            /// <returns>The result of the comparison.</returns>
            [<CompiledName("Compare")>]
            val inline compare: e1:'T -> e2:^T -> int when ^T : (static member ( < ) : ^T * ^T    -> bool) and ^T : (static member ( > ) : ^T * ^T    -> bool) 

            /// <summary>Maximum of the two values</summary>
            ///
            /// <param name="e1">The first value.</param>
            /// <param name="e2">The second value.</param>
            ///
            /// <returns>The maximum value.</returns>
            [<CompiledName("Max")>]
            val inline max : e1:^T -> e2:^T -> ^T when ^T : (static member ( < ) : ^T * ^T    -> bool) 

            /// <summary>Minimum of the two values</summary>
            ///
            /// <param name="e1">The first value.</param>
            /// <param name="e2">The second value.</param>
            ///
            /// <returns>The minimum value.</returns>
            [<CompiledName("Min")>]
            val inline min : e1:^T -> e2:^T -> ^T  when ^T : (static member ( < ) : ^T * ^T    -> bool) 

            /// <summary>Calls GetHashCode() on the value</summary>
            ///
            /// <param name="value">The value.</param>
            ///
            /// <returns>The hash code.</returns>
            [<CompiledName("Hash")>]
            val inline hash :value:'T -> int   when 'T : equality

        /// <summary>This module contains the basic arithmetic operations with overflow checks.</summary>
        module Checked =
            /// <summary>Overloaded unary negation (checks for overflow)</summary>
            ///
            /// <param name="value">The input value.</param>
            ///
            /// <returns>The negated value.</returns>
            val inline ( ~- ) : value:^T -> ^T when ^T : (static member ( ~- ) : ^T -> ^T) and default ^T : int

            /// <summary>Overloaded subtraction operator (checks for overflow)</summary>
            ///
            /// <param name="x">The first value.</param>
            /// <param name="y">The second value.</param>
            ///
            /// <returns>The first value minus the second value.</returns>
            val inline ( - ) : x:^T1 -> y:^T2 -> ^T3  when (^T1 or ^T2) : (static member ( - ) : ^T1 * ^T2    -> ^T3) and default ^T2 : ^T3 and default ^T3 : ^T1 and default ^T3 : ^T2 and default ^T1 : ^T3 and default ^T1 : ^T2 and default ^T1 : int

            /// <summary>Overloaded addition operator (checks for overflow)</summary>
            ///
            /// <param name="x">The first value.</param>
            /// <param name="y">The second value.</param>
            ///
            /// <returns>The sum of the two input values.</returns>
            val inline ( + ) : x:^T1 -> y:^T2 -> ^T3  when (^T1 or ^T2) : (static member ( + ) : ^T1 * ^T2    -> ^T3) and default ^T2 : ^T3 and default ^T3 : ^T1 and default ^T3 : ^T2 and default ^T1 : ^T3 and default ^T1 : ^T2 and default ^T1 : int

            /// <summary>Overloaded multiplication operator (checks for overflow)</summary>
            ///
            /// <param name="x">The first value.</param>
            /// <param name="y">The second value.</param>
            ///
            /// <returns>The product of the two input values.</returns>
            val inline ( * ) : x:^T1 -> y:^T2 -> ^T3  when (^T1 or ^T2) : (static member ( * ) : ^T1 * ^T2    -> ^T3) and default ^T2 : ^T3 and default ^T3 : ^T1 and default ^T3 : ^T2 and default ^T1 : ^T3 and default ^T1 : ^T2 and default ^T1 : int

            /// <summary>Converts the argument to <c>byte</c>. This is a direct, checked conversion for all 
            /// primitive numeric types. For strings, the input is converted using <see cref="M:System.Byte.Parse"/> 
            /// with InvariantCulture settings. Otherwise the operation requires an appropriate
            /// static conversion method on the input type.</summary>
            ///
            /// <param name="value">The input value.</param>
            ///
            /// <returns>The converted byte</returns>
            [<CompiledName("ToByte")>]
            val inline byte       : value:^T -> byte       when ^T : (static member op_Explicit : ^T -> byte)       and default ^T : int

            /// <summary>Converts the argument to <c>sbyte</c>. This is a direct, checked conversion for all 
            /// primitive numeric types. For strings, the input is converted using <see cref="M:System.SByte.Parse"/> 
            /// with InvariantCulture settings. Otherwise the operation requires an appropriate
            /// static conversion method on the input type.</summary>
            ///
            /// <param name="value">The input value.</param>
            ///
            /// <returns>The converted sbyte</returns>
            [<CompiledName("ToSByte")>]
            val inline sbyte      : value:^T -> sbyte      when ^T : (static member op_Explicit : ^T -> sbyte)      and default ^T : int

            /// <summary>Converts the argument to <c>int16</c>. This is a direct, checked conversion for all 
            /// primitive numeric types. For strings, the input is converted using <see cref="M:System.Int16.Parse"/> 
            /// with InvariantCulture settings. Otherwise the operation requires an appropriate
            /// static conversion method on the input type.</summary>
            ///
            /// <param name="value">The input value.</param>
            ///
            /// <returns>The converted int16</returns>
            [<CompiledName("ToInt16")>]
            val inline int16      : value:^T -> int16      when ^T : (static member op_Explicit : ^T -> int16)      and default ^T : int

            /// <summary>Converts the argument to <c>uint16</c>. This is a direct, checked conversion for all 
            /// primitive numeric types. For strings, the input is converted using <see cref="M:System.UInt16.Parse"/> 
            /// with InvariantCulture settings. Otherwise the operation requires an appropriate
            /// static conversion method on the input type.</summary>
            ///
            /// <param name="value">The input value.</param>
            ///
            /// <returns>The converted uint16</returns>
            [<CompiledName("ToUInt16")>]
            val inline uint16     : value:^T -> uint16     when ^T : (static member op_Explicit : ^T -> uint16)     and default ^T : int

            /// <summary>Converts the argument to <c>int</c>. This is a direct, checked conversion for all 
            /// primitive numeric types. For strings, the input is converted using <see cref="M:System.Int32.Parse"/> 
            /// with InvariantCulture settings. Otherwise the operation requires an appropriate
            /// static conversion method on the input type.</summary>
            ///
            /// <param name="value">The input value.</param>
            ///
            /// <returns>The converted int</returns>
            [<CompiledName("ToInt")>]
            val inline int        : value:^T -> int        when ^T : (static member op_Explicit : ^T -> int)        and default ^T : int

            /// <summary>Converts the argument to <c>int32</c>. This is a direct, checked conversion for all 
            /// primitive numeric types. For strings, the input is converted using <see cref="M:System.Int32.Parse"/> 
            /// with InvariantCulture settings. Otherwise the operation requires an appropriate
            /// static conversion method on the input type.</summary>
            ///
            /// <param name="value">The input value.</param>
            ///
            /// <returns>The converted int32</returns>
            [<CompiledName("ToInt32")>]
            val inline int32      : value:^T -> int32      when ^T : (static member op_Explicit : ^T -> int32)      and default ^T : int

            /// <summary>Converts the argument to <c>uint32</c>. This is a direct, checked conversion for all 
            /// primitive numeric types. For strings, the input is converted using <see cref="M:System.UInt32.Parse"/> 
            /// with InvariantCulture settings. Otherwise the operation requires an appropriate
            /// static conversion method on the input type.</summary>
            ///
            /// <param name="value">The input value.</param>
            ///
            /// <returns>The converted uint32</returns>
            [<CompiledName("ToUInt32")>]
            val inline uint32     : value:^T -> uint32     when ^T : (static member op_Explicit : ^T -> uint32)     and default ^T : int

            /// <summary>Converts the argument to <c>int64</c>. This is a direct, checked conversion for all 
            /// primitive numeric types. For strings, the input is converted using <see cref="M:System.Int64.Parse"/> 
            /// with InvariantCulture settings. Otherwise the operation requires an appropriate
            /// static conversion method on the input type.</summary>
            ///
            /// <param name="value">The input value.</param>
            ///
            /// <returns>The converted int64</returns>
            [<CompiledName("ToInt64")>]
            val inline int64      : value:^T -> int64      when ^T : (static member op_Explicit : ^T -> int64)      and default ^T : int

            /// <summary>Converts the argument to <c>uint64</c>. This is a direct, checked conversion for all 
            /// primitive numeric types. For strings, the input is converted using <see cref="M:System.UInt64.Parse"/> 
            /// with InvariantCulture settings. Otherwise the operation requires an appropriate
            /// static conversion method on the input type.</summary>
            ///
            /// <param name="value">The input value.</param>
            ///
            /// <returns>The converted uint64</returns>
            [<CompiledName("ToUInt64")>]
            val inline uint64     : value:^T -> uint64     when ^T : (static member op_Explicit : ^T -> uint64)     and default ^T : int

            /// <summary>Converts the argument to <see cref="T:Microsoft.FSharp.Core.nativeint" />. This is a direct, checked conversion for all 
            /// primitive numeric types. Otherwise the operation requires an appropriate
            /// static conversion method on the input type.</summary>
            ///
            /// <param name="value">The input value.</param>
            ///
            /// <returns>The converted nativeint</returns>
            [<CompiledName("ToIntPtr")>]
            val inline nativeint  : value:^T -> nativeint  when ^T : (static member op_Explicit : ^T -> nativeint)  and default ^T : int

            /// <summary>Converts the argument to <c>unativeint</c>. This is a direct, checked conversion for all 
            /// primitive numeric types. Otherwise the operation requires an appropriate
            /// static conversion method on the input type.</summary>
            ///
            /// <param name="value">The input value.</param>
            ///
            /// <returns>The converted unativeint</returns>
            [<CompiledName("ToUIntPtr")>]
            val inline unativeint : value:^T -> unativeint when ^T : (static member op_Explicit : ^T -> unativeint) and default ^T : int

            /// <summary>Converts the argument to <c>char</c>. Numeric inputs are converted using a checked 
            /// conversion according to the UTF-16 encoding for characters. String inputs must 
            /// be exactly one character long. For other input types the operation requires an 
            /// appropriate static conversion method on the input type.</summary>
            ///
            /// <param name="value">The input value.</param>
            ///
            /// <returns>The converted char</returns>
            [<CompiledName("ToChar")>]
            val inline char        : value:^T -> char      when ^T : (static member op_Explicit : ^T -> char)        and default ^T : int


namespace Microsoft.FSharp.Control

    open Microsoft.FSharp.Core

    /// <summary>Extensions related to Lazy values.</summary>
    ///
    /// <category index="3">Lazy Computation</category>
    [<AutoOpen>]
    module LazyExtensions =

        type System.Lazy<'T> with

            /// <summary>Creates a lazy computation that evaluates to the result of the given function when forced.</summary>
            ///
            /// <param name="creator">The function to provide the value when needed.</param>
            ///
            /// <returns>The created Lazy object.</returns>
            [<CompiledName("Create")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
            static member Create : creator:(unit -> 'T) -> System.Lazy<'T>
            
            /// <summary>Creates a lazy computation that evaluates to the given value when forced.</summary>
            ///
            /// <param name="value">The input value.</param>
            ///
            /// <returns>The created Lazy object.</returns>
            [<CompiledName("CreateFromValue")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
            static member CreateFromValue : value:'T -> System.Lazy<'T>
            
            /// <summary>Forces the execution of this value and return its result. Same as Value. Mutual exclusion is used to 
            /// prevent other threads also computing the value.</summary>
            /// <returns>The value of the Lazy object.</returns>
            [<CompiledName("Force")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
            member Force : unit -> 'T
            
    /// <summary>The type of delayed computations.</summary>
    /// 
    /// <remarks>Use the values in the <c>Lazy</c> module to manipulate 
    /// values of this type, and the notation <c>lazy expr</c> to create values
    /// of type <see cref="T:System.Lazy`1" />.</remarks>
    ///
    /// <category index="3">Lazy Computation</category>
    type Lazy<'T> = System.Lazy<'T>
    and 
        [<System.Obsolete("This type is obsolete. Please use System.Lazy instead.", true)>]
        'T ``lazy`` = System.Lazy<'T>        

namespace Microsoft.FSharp.Control

    open Microsoft.FSharp.Core
    open System

    /// <summary>First class event values for arbitrary delegate types.</summary>
    ///
    /// <remarks>F# gives special status to member properties compatible with type IDelegateEvent and 
    /// tagged with the CLIEventAttribute. In this case the F# compiler generates appropriate 
    /// CLI metadata to make the member appear to other CLI languages as a CLI event.</remarks>
    ///
    /// <category index="3">Events and Observables</category>
    type IDelegateEvent<'Delegate when 'Delegate :> System.Delegate > =

        /// <summary>Connect a handler delegate object to the event. A handler can
        /// be later removed using RemoveHandler. The listener will
        /// be invoked when the event is fired.</summary>
        ///
        /// <param name="handler">A delegate to be invoked when the event is fired.</param>
        abstract AddHandler: handler:'Delegate -> unit

        /// <summary>Remove a listener delegate from an event listener store.</summary>
        ///
        /// <param name="handler">The delegate to be removed from the event listener store.</param>
        abstract RemoveHandler: handler:'Delegate -> unit 

    /// <summary>First class event values for CLI events conforming to CLI Framework standards.</summary>
    ///
    /// <category index="3">Events and Observables</category>
    [<Interface>]
    type IEvent<'Delegate,'Args when 'Delegate : delegate<'Args,unit> and 'Delegate :> System.Delegate > =
        inherit IDelegateEvent<'Delegate>
        inherit IObservable<'Args>
    
    /// <summary>A delegate type associated with the F# event type <c>IEvent&lt;_&gt;</c></summary>
    ///
    /// <param name="sender">The object that fired the event.</param>
    /// <param name="args">The event arguments.</param>
    ///
    /// <category index="3">Events and Observables</category>
    [<CompiledName("FSharpHandler`1")>]
    type Handler<'T> =  delegate of sender:obj * args:'T -> unit 

    /// <summary>First-class listening points (i.e. objects that permit you to register a callback
    /// activated when the event is triggered). </summary>
    ///
    /// <category index="3">Events and Observables</category>
    type IEvent<'T> = IEvent<Handler<'T>, 'T>
