// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler

#if !NO_EXTENSIONTYPING

open Internal.Utilities.Library
open FSharp.Core.CompilerServices
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.Text

[<Sealed>]
type internal TypeProviderToken = 
    interface LockToken

[<Sealed;Class>]
type internal TypeProviderLock =
    inherit Lock<TypeProviderToken>

/// Stores and transports aggregated list of errors reported by the type provider
type internal TypeProviderError =
    inherit System.Exception
    
    /// creates new instance of TypeProviderError that represents one error
    new : (int * string) * string * range -> TypeProviderError

    /// creates new instance of TypeProviderError that represents collection of errors
    new : int * string * range * seq<string> -> TypeProviderError

    member Number : int

    member Range : range

    member ContextualErrorMessage : string

    /// creates new instance of TypeProviderError with specified type\method names
    member WithContext : string * string -> TypeProviderError

    /// creates new instance of TypeProviderError based on current instance information(message)
    member MapText : (string -> int * string) * string * range -> TypeProviderError

    /// provides uniform way to process aggregated errors
    member Iter : (TypeProviderError -> unit) -> unit


/// This struct wraps a value produced by a type provider to properly attribute any failures.
[<NoEquality; NoComparison; Class>]
type internal Tainted<'T> =

    /// Create an initial tainted value
    static member CreateAll : (ITypeProvider * ILScopeRef) list -> Tainted<ITypeProvider> list

    /// A type provider that produced the value
    member TypeProvider : Tainted<ITypeProvider>

    /// Test to report for the name of the type provider that produced the value
    member TypeProviderDesignation : string

    /// The ILScopeRef of the runtime assembly reference for type provider that produced the value
    member TypeProviderAssemblyRef : ILScopeRef

    /// Apply an operation. Any exception will be attributed to the type provider with an error located at the given range
    member PApply : ('T -> 'U) * range:range -> Tainted<'U>

    /// Apply an operation. Any exception will be attributed to the type provider with an error located at the given range
    member PApply2 : ('T -> 'U1 * 'U2) * range:range -> Tainted<'U1> * Tainted<'U2> 

    /// Apply an operation. Any exception will be attributed to the type provider with an error located at the given range
    member PApply3 : ('T -> 'U1 * 'U2 * 'U3) * range:range -> Tainted<'U1> * Tainted<'U2>  * Tainted<'U3>

    /// Apply an operation. Any exception will be attributed to the type provider with an error located at the given range
    member PApply4 : ('T -> 'U1 * 'U2 * 'U3 * 'U4) * range:range -> Tainted<'U1> * Tainted<'U2>  * Tainted<'U3> * Tainted<'U4>

    /// Apply an operation. No exception may be raised by 'f'
    member PApplyNoFailure : f: ('T -> 'U) -> Tainted<'U>

    /// Apply an operation. Any exception will be attributed to the type provider with an error located at the given range
    member PApplyWithProvider : ('T * ITypeProvider -> 'U) * range:range -> Tainted<'U>

    /// Apply an operation that returns an array. Unwrap array. Any exception will be attributed to the type provider with an error located at the given range.  String is method name of thing-returning-array, to diagnostically attribute if it is null
#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE || NO_CHECKNULLS
    member PApplyArray : ('T -> 'U[]) * string * range:range -> Tainted<'U>[]
#else
    member PApplyArray : ('T -> 'U[]?) * string * range:range -> Tainted<'U>[]
#endif

    /// Apply an operation that returns an option. Unwrap option. Any exception will be attributed to the type provider with an error located at the given range
    member PApplyOption : ('T -> 'U option) * range:range -> Tainted<'U> option

    /// Apply an operation and 'untaint' the result. The result must be marshallable. Any exception will be attributed to the type provider with an error located at the given range
    member PUntaint : ('T -> 'U) * range:range -> 'U

    /// Apply an operation and 'untaint' the result. This can be used if the return type 
    /// is guaranteed not to be implemented by a type provider
    member PUntaintNoFailure : ('T -> 'U) -> 'U

    /// Conditionally coerce the value
    member OfType<'U> : unit -> Tainted<'U> option

    /// Assert that the value is of 'U and coerce the value.
    /// If coercion fails, the failure will be blamed on a type provider
    member Coerce<'U> : range:range -> Tainted<'U>

[<RequireQualifiedAccess>]
module internal Tainted =

    /// Test whether the tainted value is null
#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE || NO_CHECKNULLS
    val (|Null|NonNull|) : Tainted<'T> -> Choice<unit, Tainted<'T>> when 'T : null and 'T : not struct
#else
    val (|Null|NonNull|) : Tainted<'T?> -> Choice<unit, Tainted<'T>> when 'T : not null and 'T : not struct
#endif


    /// Test whether the tainted value equals given value. 
    /// Failure in call to equality operation will be blamed on type provider of first operand
    val Eq : Tainted<'T> -> 'T -> bool when 'T : equality

    /// Test whether the tainted value equals given value. Type providers are ignored (equal tainted values produced by different type providers are equal)
    /// Failure in call to equality operation will be blamed on type provider of first operand
    val EqTainted : Tainted<'T> -> Tainted<'T> -> bool when 'T : equality and 'T : not struct

    /// Compute the hash value for the tainted value
    val GetHashCodeTainted : Tainted<'T> -> int when 'T : equality

#endif
