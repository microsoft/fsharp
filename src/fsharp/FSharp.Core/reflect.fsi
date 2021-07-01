// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// <summary>This namespace contains constructs for reflecting on the representation of
/// F# values and types. It augments the design of System.Reflection.</summary>
namespace Microsoft.FSharp.Reflection 

open System
open System.Reflection
open Microsoft.FSharp.Core
open Microsoft.FSharp.Primitives.Basics
open Microsoft.FSharp.Collections

/// <summary>Represents a case of a discriminated union type</summary>
///
/// <namespacedoc><summary>
///   Library functionality for accessing additional information about F# types and F# values at 
///   runtime, augmenting that available through <a href="https://docs.microsoft.com/dotnet/api/system.reflection">System.Reflection</a>.
/// </summary></namespacedoc>
[<Sealed>]
type UnionCaseInfo =
    /// <summary>The name of the case.</summary>
    member Name : string

    /// <summary>The type in which the case occurs.</summary>
    member DeclaringType: Type
    
    /// <summary>Returns the custom attributes associated with the case.</summary>
    /// <returns>An array of custom attributes.</returns>
    member GetCustomAttributes: unit -> obj[]

    /// <summary>Returns the custom attributes associated with the case matching the given attribute type.</summary>
    /// <param name="attributeType">The type of attributes to return.</param>
    ///
    /// <returns>An array of custom attributes.</returns>
    member GetCustomAttributes: attributeType:System.Type -> obj[]

    /// <summary>Returns the custom attributes data associated with the case.</summary>
    /// <returns>An list of custom attribute data items.</returns>
    member GetCustomAttributesData: unit -> System.Collections.Generic.IList<CustomAttributeData>

    /// <summary>The fields associated with the case, represented by a PropertyInfo.</summary>
    /// <returns>The fields associated with the case.</returns>
    member GetFields: unit -> PropertyInfo []

    /// <summary>The integer tag for the case.</summary>
    member Tag: int


[<AbstractClass; Sealed>]
/// <summary>Contains operations associated with constructing and analyzing values associated with F# types
/// such as records, unions and tuples.</summary>
type FSharpValue = 

    /// <summary>Reads a field from a record value.</summary>
    ///
    /// <remarks>Assumes the given input is a record value. If not, <see cref="T:System.ArgumentException" /> is raised.</remarks>
    ///
    /// <param name="record">The record object.</param>
    /// <param name="info">The PropertyInfo describing the field to read.</param>
    ///
    /// <exception cref="T:System.ArgumentException">Thrown when the input is not a record value.</exception>
    /// <returns>The field from the record.</returns>
    static member GetRecordField:  record:obj * info:PropertyInfo -> obj
    
    /// <summary>Precompute a function for reading a particular field from a record.
    /// Assumes the given type is a RecordType with a field of the given name. 
    /// If not, <see cref="T:System.ArgumentException" /> is raised during pre-computation.</summary>
    ///
    /// <remarks>Using the computed function will typically be faster than executing a corresponding call to Value.GetInfo
    /// because the path executed by the computed function is optimized given the knowledge that it will be
    /// used to read values of the given type.</remarks>
    ///
    /// <param name="info">The PropertyInfo of the field to read.</param>
    ///
    /// <exception cref="T:System.ArgumentException">Thrown when the input type is not a record type.</exception>
    ///
    /// <returns>A function to read the specified field from the record.</returns>
    static member PreComputeRecordFieldReader : info:PropertyInfo -> (obj -> obj)

    /// <summary>Creates an instance of a record type.</summary>
    ///
    /// <remarks>Assumes the given input is a record type.</remarks>
    ///
    /// <param name="recordType">The type of record to make.</param>
    /// <param name="values">The array of values to initialize the record.</param>
    /// <param name="bindingFlags">Optional binding flags for the record.</param>
    ///
    /// <exception cref="T:System.ArgumentException">Thrown when the input type is not a record type.</exception>
    ///
    /// <returns>The created record.</returns>
    static member MakeRecord: recordType:Type * values:obj [] * ?bindingFlags:BindingFlags  -> obj

    /// <summary>Reads all the fields from a record value.</summary>
    ///
    /// <remarks>Assumes the given input is a record value. If not, <see cref="T:System.ArgumentException" /> is raised.</remarks>
    /// <param name="record">The record object.</param>
    /// <param name="bindingFlags">Optional binding flags for the record.</param>
    ///
    /// <exception cref="T:System.ArgumentException">Thrown when the input type is not a record type.</exception>
    ///
    /// <returns>The array of fields from the record.</returns>
    static member GetRecordFields:  record:obj * ?bindingFlags:BindingFlags  -> obj[]

    /// <summary>Precompute a function for reading all the fields from a record. The fields are returned in the
    /// same order as the fields reported by a call to Microsoft.FSharp.Reflection.Type.GetInfo for
    /// this type.</summary>
    ///
    /// <remarks>Assumes the given type is a RecordType. 
    /// If not, <see cref="T:System.ArgumentException" /> is raised during pre-computation.
    ///
    /// Using the computed function will typically be faster than executing a corresponding call to Value.GetInfo
    /// because the path executed by the computed function is optimized given the knowledge that it will be
    /// used to read values of the given type.</remarks>
    ///
    /// <param name="recordType">The type of record to read.</param>
    /// <param name="bindingFlags">Optional binding flags.</param>
    ///
    /// <exception cref="T:System.ArgumentException">Thrown when the input type is not a record type.</exception>
    ///
    /// <returns>An optimized reader for the given record type.</returns>
    static member PreComputeRecordReader : recordType:Type  * ?bindingFlags:BindingFlags -> (obj -> obj[])

    /// <summary>Precompute a function for constructing a record value. </summary>
    ///
    /// <remarks>Assumes the given type is a RecordType.
    /// If not, <see cref="T:System.ArgumentException" /> is raised during pre-computation.</remarks>
    ///
    /// <param name="recordType">The type of record to construct.</param>
    /// <param name="bindingFlags">Optional binding flags.</param>
    ///
    /// <exception cref="T:System.ArgumentException">Thrown when the input type is not a record type.</exception>
    ///
    /// <returns>A function to construct records of the given type.</returns>
    static member PreComputeRecordConstructor : recordType:Type  * ?bindingFlags:BindingFlags -> (obj[] -> obj)

    /// <summary>Get a ConstructorInfo for a record type</summary>
    ///
    /// <param name="recordType">The record type.</param>
    /// <param name="bindingFlags">Optional binding flags.</param>
    ///
    /// <returns>A ConstructorInfo for the given record type.</returns>
    static member PreComputeRecordConstructorInfo: recordType:Type * ?bindingFlags:BindingFlags -> ConstructorInfo
    
    /// <summary>Create a union case value.</summary>
    ///
    /// <param name="unionCase">The description of the union case to create.</param>
    /// <param name="args">The array of arguments to construct the given case.</param>
    /// <param name="bindingFlags">Optional binding flags.</param>
    ///
    /// <returns>The constructed union case.</returns>
    static member MakeUnion: unionCase:UnionCaseInfo * args:obj [] * ?bindingFlags:BindingFlags -> obj

    /// <summary>Identify the union case and its fields for an object</summary>
    ///
    /// <remarks>Assumes the given input is a union case value. If not, <see cref="T:System.ArgumentException" /> is raised.
    ///
    /// If the type is not given, then the runtime type of the input object is used to identify the
    /// relevant union type. The type should always be given if the input object may be null. For example, 
    /// option values may be represented using the 'null'.</remarks>
    /// <param name="value">The input union case.</param>
    /// <param name="unionType">The union type containing the value.</param>
    /// <param name="bindingFlags">Optional binding flags.</param>
    ///
    /// <exception cref="T:System.ArgumentException">Thrown when the input type is not a union case value.</exception>
    ///
    /// <returns>The description of the union case and its fields.</returns>
    static member GetUnionFields:  value:obj * unionType:Type * ?bindingFlags:BindingFlags -> UnionCaseInfo * obj []
    
    /// <summary>Assumes the given type is a union type. 
    /// If not, <see cref="T:System.ArgumentException" /> is raised during pre-computation.</summary>
    ///
    /// <remarks>Using the computed function is more efficient than calling GetUnionCase
    /// because the path executed by the computed function is optimized given the knowledge that it will be
    /// used to read values of the given type.</remarks>
    ///
    /// <param name="unionType">The type of union to optimize reading.</param>
    /// <param name="bindingFlags">Optional binding flags.</param>
    ///
    /// <returns>An optimized function to read the tags of the given union type.</returns>
    static member PreComputeUnionTagReader          : unionType:Type  * ?bindingFlags:BindingFlags -> (obj -> int)

    /// <summary>Precompute a property or static method for reading an integer representing the case tag of a union type.</summary>
    ///
    /// <param name="unionType">The type of union to read.</param>
    /// <param name="bindingFlags">Optional binding flags.</param>
    ///
    /// <returns>The description of the union case reader.</returns>
    static member PreComputeUnionTagMemberInfo : unionType:Type  * ?bindingFlags:BindingFlags -> MemberInfo

    /// <summary>Precompute a function for reading all the fields for a particular discriminator case of a union type</summary>
    ///
    /// <remarks>Using the computed function will typically be faster than executing a corresponding call to GetFields</remarks>
    ///
    /// <param name="unionCase">The description of the union case to read.</param>
    /// <param name="bindingFlags">Optional binding flags.</param>
    ///
    /// <returns>A function to for reading the fields of the given union case.</returns>
    static member PreComputeUnionReader       : unionCase:UnionCaseInfo  * ?bindingFlags:BindingFlags -> (obj -> obj[])

    /// <summary>Precompute a function for constructing a discriminated union value for a particular union case. </summary>
    ///
    /// <param name="unionCase">The description of the union case.</param>
    /// <param name="bindingFlags">Optional binding flags.</param>
    ///
    /// <returns>A function for constructing values of the given union case.</returns>
    static member PreComputeUnionConstructor : unionCase:UnionCaseInfo  * ?bindingFlags:BindingFlags -> (obj[] -> obj)

    /// <summary>A method that constructs objects of the given case</summary>
    ///
    /// <param name="unionCase">The description of the union case.</param>
    /// <param name="bindingFlags">Optional binding flags.</param>
    ///
    /// <returns>The description of the constructor of the given union case.</returns>
    static member PreComputeUnionConstructorInfo: unionCase:UnionCaseInfo * ?bindingFlags:BindingFlags -> MethodInfo

    /// <summary>Reads all the fields from a value built using an instance of an F# exception declaration</summary>
    ///
    /// <remarks>Assumes the given input is an F# exception value. If not, <see cref="T:System.ArgumentException" /> is raised.</remarks>
    ///
    /// <param name="exn">The exception instance.</param>
    /// <param name="bindingFlags">Optional binding flags.</param>
    ///
    /// <exception cref="T:System.ArgumentException">Thrown when the input type is not an F# exception.</exception>
    ///
    /// <returns>The fields from the given exception.</returns>
    static member GetExceptionFields:  exn:obj * ?bindingFlags:BindingFlags  -> obj[]

    /// <summary>Creates an instance of a tuple type</summary>
    ///
    /// <remarks>Assumes at least one element is given. If not, <see cref="T:System.ArgumentException" /> is raised.</remarks>
    ///
    /// <param name="tupleElements">The array of tuple fields.</param>
    /// <param name="tupleType">The tuple type to create.</param>
    ///
    /// <exception cref="T:System.ArgumentException">Thrown if no elements are given.</exception>
    ///
    /// <returns>An instance of the tuple type with the given elements.</returns>
    static member MakeTuple: tupleElements:obj[] * tupleType:Type -> obj

    /// <summary>Reads a field from a tuple value.</summary>
    ///
    /// <remarks>Assumes the given input is a tuple value. If not, <see cref="T:System.ArgumentException" /> is raised.</remarks>
    ///
    /// <param name="tuple">The input tuple.</param>
    /// <param name="index">The index of the field to read.</param>
    ///
    /// <returns>The value of the field.</returns>
    static member GetTupleField: tuple:obj * index:int -> obj

    /// <summary>Reads all fields from a tuple.</summary>
    ///
    /// <remarks>Assumes the given input is a tuple value. If not, <see cref="T:System.ArgumentException" /> is raised.</remarks>
    ///
    /// <param name="tuple">The input tuple.</param>
    ///
    /// <exception cref="T:System.ArgumentException">Thrown when the input is not a tuple value.</exception>
    ///
    /// <returns>An array of the fields from the given tuple.</returns>
    static member GetTupleFields: tuple:obj -> obj []
    
    /// <summary>Precompute a function for reading the values of a particular tuple type</summary>
    ///
    /// <remarks>Assumes the given type is a TupleType.
    /// If not, <see cref="T:System.ArgumentException" /> is raised during pre-computation.</remarks>
    ///
    /// <param name="tupleType">The tuple type to read.</param>
    ///
    /// <exception cref="T:System.ArgumentException">Thrown when the given type is not a tuple type.</exception>
    ///
    /// <returns>A function to read values of the given tuple type.</returns>
    static member PreComputeTupleReader           : tupleType:Type -> (obj -> obj[])
    
    /// <summary>Gets information that indicates how to read a field of a tuple</summary>
    ///
    /// <param name="tupleType">The input tuple type.</param>
    /// <param name="index">The index of the tuple element to describe.</param>
    ///
    /// <returns>The description of the tuple element and an optional type and index if the tuple is big.</returns>
    static member PreComputeTuplePropertyInfo: tupleType:Type * index:int -> PropertyInfo * (Type * int) option
    
    /// <summary>Precompute a function for reading the values of a particular tuple type</summary>
    ///
    /// <remarks>Assumes the given type is a TupleType.
    /// If not, <see cref="T:System.ArgumentException" /> is raised during pre-computation.</remarks>
    ///
    /// <param name="tupleType">The type of tuple to read.</param>
    ///
    /// <exception cref="T:System.ArgumentException">Thrown when the given type is not a tuple type.</exception>
    ///
    /// <returns>A function to read a particular tuple type.</returns>
    static member PreComputeTupleConstructor      : tupleType:Type -> (obj[] -> obj)

    /// <summary>Gets a method that constructs objects of the given tuple type. 
    /// For small tuples, no additional type will be returned.</summary>
    /// 
    /// <remarks>For large tuples, an additional type is returned indicating that
    /// a nested encoding has been used for the tuple type. In this case
    /// the suffix portion of the tuple type has the given type and an
    /// object of this type must be created and passed as the last argument 
    /// to the ConstructorInfo. A recursive call to PreComputeTupleConstructorInfo 
    /// can be used to determine the constructor for that the suffix type.</remarks>
    ///
    /// <param name="tupleType">The input tuple type.</param>
    ///
    /// <returns>The description of the tuple type constructor and an optional extra type
    /// for large tuples.</returns>
    static member PreComputeTupleConstructorInfo: tupleType:Type -> ConstructorInfo * Type option

    /// <summary>Builds a typed function from object from a dynamic function implementation</summary>
    ///
    /// <param name="functionType">The function type of the implementation.</param>
    /// <param name="implementation">The untyped lambda of the function implementation.</param>
    ///
    /// <returns>A typed function from the given dynamic implementation.</returns>
    static member MakeFunction           : functionType:Type * implementation:(obj -> obj) -> obj

[<AbstractClass; Sealed>]
/// <summary>Contains operations associated with constructing and analyzing F# types such as records, unions and tuples</summary>
type FSharpType =

    /// <summary>Reads all the fields from a record value, in declaration order</summary>
    ///
    /// <remarks>Assumes the given input is a record value. If not, <see cref="T:System.ArgumentException" /> is raised.</remarks>
    ///
    /// <param name="recordType">The input record type.</param>
    /// <param name="bindingFlags">Optional binding flags.</param>
    ///
    /// <returns>An array of descriptions of the properties of the record type.</returns>
    static member GetRecordFields: recordType:Type * ?bindingFlags:BindingFlags -> PropertyInfo[]

    /// <summary>Gets the cases of a union type.</summary>
    ///
    /// <remarks>Assumes the given type is a union type. If not, <see cref="T:System.ArgumentException" /> is raised during pre-computation.</remarks>
    ///
    /// <param name="unionType">The input union type.</param>
    /// <param name="bindingFlags">Optional binding flags.</param>
    ///
    /// <exception cref="T:System.ArgumentException">Thrown when the input type is not a union type.</exception>
    ///
    /// <returns>An array of descriptions of the cases of the given union type.</returns>
    static member GetUnionCases: unionType:Type * ?bindingFlags:BindingFlags -> UnionCaseInfo[]

    
    /// <summary>Return true if the <c>typ</c> is a representation of an F# record type </summary>
    ///
    /// <param name="typ">The type to check.</param>
    /// <param name="bindingFlags">Optional binding flags.</param>
    ///
    /// <returns>True if the type check succeeds.</returns>
    static member IsRecord: typ:Type * ?bindingFlags:BindingFlags -> bool

    /// <summary>Returns true if the <c>typ</c> is a representation of an F# union type or the runtime type of a value of that type</summary>
    ///
    /// <param name="typ">The type to check.</param>
    /// <param name="bindingFlags">Optional binding flags.</param>
    ///
    /// <returns>True if the type check succeeds.</returns>
    static member IsUnion: typ:Type * ?bindingFlags:BindingFlags -> bool

    /// <summary>Reads all the fields from an F# exception declaration, in declaration order</summary>
    ///
    /// <remarks>Assumes <c>exceptionType</c> is an exception representation type. If not, <see cref="T:System.ArgumentException" /> is raised.</remarks>
    ///
    /// <param name="exceptionType">The exception type to read.</param>
    /// <param name="bindingFlags">Optional binding flags.</param>
    ///
    /// <exception cref="T:System.ArgumentException">Thrown if the given type is not an exception.</exception>
    ///
    /// <returns>An array containing the PropertyInfo of each field in the exception.</returns>
    static member GetExceptionFields: exceptionType:Type * ?bindingFlags:BindingFlags -> PropertyInfo[]

    /// <summary>Returns true if the <c>typ</c> is a representation of an F# exception declaration</summary>
    ///
    /// <param name="exceptionType">The type to check.</param>
    /// <param name="bindingFlags">Optional binding flags.</param>
    ///
    /// <returns>True if the type check is an F# exception.</returns>
    static member IsExceptionRepresentation: exceptionType:Type * ?bindingFlags:BindingFlags -> bool

    /// <summary>Returns a <see cref="T:System.Type"/> representing the F# function type with the given domain and range</summary>
    ///
    /// <param name="domain">The input type of the function.</param>
    /// <param name="range">The output type of the function.</param>
    ///
    /// <returns>The function type with the given domain and range.</returns>
    static member MakeFunctionType: domain:Type * range:Type -> Type

    /// <summary>Returns a <see cref="T:System.Type"/> representing an F# tuple type with the given element types</summary>
    ///
    /// <param name="types">An array of types for the tuple elements.</param>
    ///
    /// <returns>The type representing the tuple containing the input elements.</returns>
    static member MakeTupleType: types:Type[] -> Type

    /// <summary>Returns a <see cref="T:System.Type"/> representing an F# tuple type with the given element types</summary>
    ///
    /// <param name="asm">Runtime assembly containing System.Tuple definitions.</param>
    /// <param name="types">An array of types for the tuple elements.</param>
    ///
    /// <returns>The type representing the tuple containing the input elements.</returns>
    static member MakeTupleType: asm:Assembly * types:Type[] -> Type

    /// <summary>Returns a <see cref="T:System.Type"/> representing an F# struct tuple type with the given element types</summary>
    ///
    /// <param name="asm">Runtime assembly containing System.ValueTuple definitions.</param>
    /// <param name="types">An array of types for the tuple elements.</param>
    ///
    /// <returns>The type representing the struct tuple containing the input elements.</returns>
    static member MakeStructTupleType: asm:Assembly * types:Type[] -> Type

    /// <summary>Return true if the <c>typ</c> is a representation of an F# tuple type </summary>
    ///
    /// <param name="typ">The type to check.</param>
    ///
    /// <returns>True if the type check succeeds.</returns>
    static member IsTuple : typ:Type -> bool

    /// <summary>Return true if the <c>typ</c> is a representation of an F# function type or the runtime type of a closure implementing an F# function type</summary>
    ///
    /// <param name="typ">The type to check.</param>
    ///
    /// <returns>True if the type check succeeds.</returns>
    static member IsFunction : typ:Type -> bool

    /// <summary>Return true if the <c>typ</c> is a <see cref="T:System.Type"/> value corresponding to the compiled form of an F# module </summary>
    ///
    /// <param name="typ">The type to check.</param>
    ///
    /// <returns>True if the type check succeeds.</returns>
    static member IsModule: typ:Type -> bool


    /// <summary>Gets the tuple elements from the representation of an F# tuple type.</summary>
    ///
    /// <param name="tupleType">The input tuple type.</param>
    ///
    /// <returns>An array of the types contained in the given tuple type.</returns>
    static member GetTupleElements : tupleType:Type -> Type[]

    /// <summary>Gets the domain and range types from an F# function type  or from the runtime type of a closure implementing an F# type</summary>
    ///
    /// <param name="functionType">The input function type.</param>
    ///
    /// <returns>A tuple of the domain and range types of the input function.</returns>
    static member GetFunctionElements : functionType:Type -> Type * Type

[<AutoOpen>]
/// <summary>Defines further accessing additional information about F# types and F# values at runtime.</summary>
module FSharpReflectionExtensions =
    type FSharpValue with
        /// <summary>Creates an instance of a record type.</summary>
        ///
        /// <remarks>Assumes the given input is a record type.</remarks>
        ///
        /// <param name="recordType">The type of record to make.</param>
        /// <param name="values">The array of values to initialize the record.</param>
        /// <param name="allowAccessToPrivateRepresentation">Optional flags that denotes accessibility of the private representation.</param>
        ///
        /// <exception cref="T:System.ArgumentException">Thrown when the input type is not a record type.</exception>
        ///
        /// <returns>The created record.</returns>
        static member MakeRecord: recordType:Type * values:obj [] * ?allowAccessToPrivateRepresentation : bool -> obj

        /// <summary>Reads all the fields from a record value.</summary>
        ///
        /// <remarks>Assumes the given input is a record value. If not, <see cref="T:System.ArgumentException" /> is raised.</remarks>
        ///
        /// <param name="record">The record object.</param>
        /// <param name="allowAccessToPrivateRepresentation">Optional flag that denotes accessibility of the private representation.</param>
        ///
        /// <exception cref="T:System.ArgumentException">Thrown when the input type is not a record type.</exception>
        ///
        /// <returns>The array of fields from the record.</returns>
        static member GetRecordFields:  record:obj * ?allowAccessToPrivateRepresentation : bool  -> obj[]

        /// <summary>Precompute a function for reading all the fields from a record. The fields are returned in the
        /// same order as the fields reported by a call to Microsoft.FSharp.Reflection.Type.GetInfo for
        /// this type.</summary>
        ///
        /// <remarks>Assumes the given type is a RecordType. 
        /// If not, <see cref="T:System.ArgumentException" /> is raised during pre-computation.
        ///
        /// Using the computed function will typically be faster than executing a corresponding call to Value.GetInfo
        /// because the path executed by the computed function is optimized given the knowledge that it will be
        /// used to read values of the given type.</remarks>
        ///
        /// <param name="recordType">The type of record to read.</param>
        /// <param name="allowAccessToPrivateRepresentation">Optional flag that denotes accessibility of the private representation.</param>    
        ///
        /// <exception cref="T:System.ArgumentException">Thrown when the input type is not a record type.</exception>
        ///
        /// <returns>An optimized reader for the given record type.</returns>
        static member PreComputeRecordReader : recordType:Type * ?allowAccessToPrivateRepresentation : bool -> (obj -> obj[])

        /// <summary>Precompute a function for constructing a record value. </summary>
        ///
        /// <remarks>Assumes the given type is a RecordType.
        /// If not, <see cref="T:System.ArgumentException" /> is raised during pre-computation.</remarks>
        ///
        /// <param name="recordType">The type of record to construct.</param>
        /// <param name="allowAccessToPrivateRepresentation">Optional flag that denotes accessibility of the private representation.</param>    
        ///
        /// <exception cref="T:System.ArgumentException">Thrown when the input type is not a record type.</exception>
        ///
        /// <returns>A function to construct records of the given type.</returns>
        static member PreComputeRecordConstructor : recordType:Type * ?allowAccessToPrivateRepresentation : bool -> (obj[] -> obj)

        /// <summary>Get a ConstructorInfo for a record type</summary>
        ///
        /// <param name="recordType">The record type.</param>
        /// <param name="allowAccessToPrivateRepresentation">Optional flag that denotes accessibility of the private representation.</param>    
        ///
        /// <returns>A ConstructorInfo for the given record type.</returns>
        static member PreComputeRecordConstructorInfo: recordType:Type * ?allowAccessToPrivateRepresentation : bool-> ConstructorInfo
    
        /// <summary>Create a union case value.</summary>
        ///
        /// <param name="unionCase">The description of the union case to create.</param>
        /// <param name="args">The array of arguments to construct the given case.</param>
        /// <param name="allowAccessToPrivateRepresentation">Optional flag that denotes accessibility of the private representation.</param>    
        ///
        /// <returns>The constructed union case.</returns>
        static member MakeUnion: unionCase:UnionCaseInfo * args:obj [] * ?allowAccessToPrivateRepresentation : bool-> obj

        /// <summary>Identify the union case and its fields for an object</summary>
        ///
        /// <remarks>Assumes the given input is a union case value. If not, <see cref="T:System.ArgumentException" /> is raised.
        ///
        /// If the type is not given, then the runtime type of the input object is used to identify the
        /// relevant union type. The type should always be given if the input object may be null. For example, 
        /// option values may be represented using the 'null'.</remarks>
        ///
        /// <param name="value">The input union case.</param>
        /// <param name="unionType">The union type containing the value.</param>
        /// <param name="allowAccessToPrivateRepresentation">Optional flag that denotes accessibility of the private representation.</param>    
        ///
        /// <exception cref="T:System.ArgumentException">Thrown when the input type is not a union case value.</exception>
        ///
        /// <returns>The description of the union case and its fields.</returns>
        static member GetUnionFields:  value:obj * unionType:Type * ?allowAccessToPrivateRepresentation : bool -> UnionCaseInfo * obj []
    
        /// <summary>Assumes the given type is a union type. 
        /// If not, <see cref="T:System.ArgumentException" /> is raised during pre-computation.</summary>
        ///
        /// <remarks>Using the computed function is more efficient than calling GetUnionCase
        /// because the path executed by the computed function is optimized given the knowledge that it will be
        /// used to read values of the given type.</remarks>
        ///
        /// <param name="unionType">The type of union to optimize reading.</param>
        /// <param name="allowAccessToPrivateRepresentation">Optional flag that denotes accessibility of the private representation.</param>
        ///
        /// <returns>An optimized function to read the tags of the given union type.</returns>
        static member PreComputeUnionTagReader          : unionType:Type * ?allowAccessToPrivateRepresentation : bool -> (obj -> int)

        /// <summary>Precompute a property or static method for reading an integer representing the case tag of a union type.</summary>
        ///
        /// <param name="unionType">The type of union to read.</param>
        /// <param name="allowAccessToPrivateRepresentation">Optional flag that denotes accessibility of the private representation.</param>    
        ///
        /// <returns>The description of the union case reader.</returns>
        static member PreComputeUnionTagMemberInfo : unionType:Type * ?allowAccessToPrivateRepresentation : bool -> MemberInfo

        /// <summary>Precompute a function for reading all the fields for a particular discriminator case of a union type</summary>
        ///
        /// <remarks>Using the computed function will typically be faster than executing a corresponding call to GetFields</remarks>
        ///
        /// <param name="unionCase">The description of the union case to read.</param>
        /// <param name="allowAccessToPrivateRepresentation">Optional flag that denotes accessibility of the private representation.</param>    
        ///
        /// <returns>A function to for reading the fields of the given union case.</returns>
        static member PreComputeUnionReader       : unionCase:UnionCaseInfo * ?allowAccessToPrivateRepresentation : bool -> (obj -> obj[])

        /// <summary>Precompute a function for constructing a discriminated union value for a particular union case. </summary>
        ///
        /// <param name="unionCase">The description of the union case.</param>
        /// <param name="allowAccessToPrivateRepresentation">Optional flag that denotes accessibility of the private representation.</param>    
        ///
        /// <returns>A function for constructing values of the given union case.</returns>
        static member PreComputeUnionConstructor : unionCase:UnionCaseInfo * ?allowAccessToPrivateRepresentation : bool -> (obj[] -> obj)

        /// <summary>A method that constructs objects of the given case</summary>
        ///
        /// <param name="unionCase">The description of the union case.</param>
        /// <param name="allowAccessToPrivateRepresentation">Optional flag that denotes accessibility of the private representation.</param>    
        ///
        /// <returns>The description of the constructor of the given union case.</returns>
        static member PreComputeUnionConstructorInfo: unionCase:UnionCaseInfo * ?allowAccessToPrivateRepresentation : bool -> MethodInfo

        /// <summary>Reads all the fields from a value built using an instance of an F# exception declaration</summary>
        ///
        /// <remarks>Assumes the given input is an F# exception value. If not, <see cref="T:System.ArgumentException" /> is raised.</remarks>
        ///
        /// <param name="exn">The exception instance.</param>
        /// <param name="allowAccessToPrivateRepresentation">Optional flag that denotes accessibility of the private representation.</param>    
        ///
        /// <exception cref="T:System.ArgumentException">Thrown when the input type is not an F# exception.</exception>
        ///
        /// <returns>The fields from the given exception.</returns>
        static member GetExceptionFields:  exn:obj * ?allowAccessToPrivateRepresentation : bool -> obj[]

    type FSharpType with
        /// <summary>Reads all the fields from a record value, in declaration order</summary>
        ///
        /// <remarks>Assumes the given input is a record value. If not, <see cref="T:System.ArgumentException" /> is raised.</remarks>
        ///
        /// <param name="recordType">The input record type.</param>
        /// <param name="allowAccessToPrivateRepresentation">Optional flag that denotes accessibility of the private representation.</param>    
        ///
        /// <returns>An array of descriptions of the properties of the record type.</returns>
        static member GetRecordFields: recordType:Type * ?allowAccessToPrivateRepresentation : bool -> PropertyInfo[]

        /// <summary>Gets the cases of a union type.</summary>
        ///
        /// <remarks>Assumes the given type is a union type. If not, <see cref="T:System.ArgumentException" /> is raised during pre-computation.</remarks>
        ///
        /// <param name="unionType">The input union type.</param>
        /// <param name="allowAccessToPrivateRepresentation">Optional flag that denotes accessibility of the private representation.</param>    
        ///
        /// <exception cref="T:System.ArgumentException">Thrown when the input type is not a union type.</exception>
        ///
        /// <returns>An array of descriptions of the cases of the given union type.</returns>
        static member GetUnionCases: unionType:Type * ?allowAccessToPrivateRepresentation : bool -> UnionCaseInfo[]


        /// <summary>Return true if the <c>typ</c> is a representation of an F# record type </summary>
        ///
        /// <param name="typ">The type to check.</param>
        /// <param name="allowAccessToPrivateRepresentation">Optional flag that denotes accessibility of the private representation.</param>    
        ///
        /// <returns>True if the type check succeeds.</returns>
        static member IsRecord: typ:Type * ?allowAccessToPrivateRepresentation : bool -> bool

        /// <summary>Returns true if the <c>typ</c> is a representation of an F# union type or the runtime type of a value of that type</summary>
        ///
        /// <param name="typ">The type to check.</param>
        /// <param name="allowAccessToPrivateRepresentation">Optional flag that denotes accessibility of the private representation.</param>    
        ///
        /// <returns>True if the type check succeeds.</returns>
        static member IsUnion: typ:Type * ?allowAccessToPrivateRepresentation : bool -> bool

        /// <summary>Reads all the fields from an F# exception declaration, in declaration order</summary>
        ///
        /// <remarks>Assumes <c>exceptionType</c> is an exception representation type. If not, <see cref="T:System.ArgumentException" /> is raised.</remarks>
        ///
        /// <param name="exceptionType">The exception type to read.</param>
        /// <param name="allowAccessToPrivateRepresentation">Optional flag that denotes accessibility of the private representation.</param>    
        ///
        /// <exception cref="T:System.ArgumentException">Thrown if the given type is not an exception.</exception>
        ///
        /// <returns>An array containing the PropertyInfo of each field in the exception.</returns>
        static member GetExceptionFields: exceptionType:Type * ?allowAccessToPrivateRepresentation : bool -> PropertyInfo[]

        /// <summary>Returns true if the <c>exceptionType</c> is a representation of an F# exception declaration</summary>
        ///
        /// <param name="exceptionType">The type to check.</param>
        /// <param name="allowAccessToPrivateRepresentation">Optional flag that denotes accessibility of the private representation.</param>    
        ///
        /// <returns>True if the type check is an F# exception.</returns>
        static member IsExceptionRepresentation: exceptionType:Type * ?allowAccessToPrivateRepresentation : bool -> bool


namespace Microsoft.FSharp.Reflection

open Microsoft.FSharp.Core

module internal ReflectionUtils = 
    type BindingFlags = System.Reflection.BindingFlags
    val toBindingFlags  : allowAccessToNonPublicMembers : bool -> BindingFlags