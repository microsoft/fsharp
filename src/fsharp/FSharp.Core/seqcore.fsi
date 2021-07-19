// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.FSharp.Collections
    open System
    open System.Collections
    open System.Collections.Generic
    open Microsoft.FSharp.Core
    open Microsoft.FSharp.Collections
    module internal IEnumerator =
        val noReset : unit -> 'a
        val notStarted : unit -> 'a
        val alreadyFinished : unit -> 'a
        val check : started:bool -> unit
        val dispose : r:System.IDisposable -> unit
        val cast :
            e:System.Collections.IEnumerator ->
            System.Collections.Generic.IEnumerator<'T>
        [<SealedAttribute ()>]
        type EmptyEnumerator<'T> =
            class
            interface System.IDisposable
            interface System.Collections.IEnumerator
            interface System.Collections.Generic.IEnumerator<'T>
            new : unit -> EmptyEnumerator<'T>
            end
        val Empty : unit -> System.Collections.Generic.IEnumerator<'T>
        [<NoEqualityAttribute (); NoComparisonAttribute ()>]
        type EmptyEnumerable<'T> =
            | EmptyEnumerable
            with
            interface System.Collections.IEnumerable
            interface System.Collections.Generic.IEnumerable<'T>
            end

        val readAndClear : r:'a option ref -> 'a option
        val generateWhileSome :
            openf:(unit -> 'a) ->
            compute:('a -> 'U option) ->
                closef:('a -> unit) -> System.Collections.Generic.IEnumerator<'U>
        [<SealedAttribute ()>]
        type Singleton<'T> =
            class
            interface System.IDisposable
            interface System.Collections.IEnumerator
            interface System.Collections.Generic.IEnumerator<'T>
            new : v:'T -> Singleton<'T>
            end
        val Singleton : x:'T -> System.Collections.Generic.IEnumerator<'T>
        val EnumerateThenFinally :
            f:(unit -> unit) ->
            e:System.Collections.Generic.IEnumerator<'T> ->
                System.Collections.Generic.IEnumerator<'T>
        val inline checkNonNull : argName:string -> arg:'a -> unit when 'a : null
        val mkSeq :
            f:(unit -> System.Collections.Generic.IEnumerator<'U>) ->
            System.Collections.Generic.IEnumerable<'U>

namespace Microsoft.FSharp.Core.CompilerServices

    open System
    open System.Collections
    open System.Collections.Generic
    open System.Runtime.CompilerServices
    open Microsoft.FSharp.Core
    open Microsoft.FSharp.Collections

    [<RequireQualifiedAccess>]
    /// <summary>A group of functions used as part of the compiled representation of F# sequence expressions.</summary>
    module RuntimeHelpers = 

        [<Struct; NoComparison; NoEquality>]
        type internal StructBox<'T when 'T : equality> = 
            new : value:'T -> StructBox<'T>
            member Value : 'T
            static member Comparer : IEqualityComparer<StructBox<'T>>

        val internal mkConcatSeq : sources:(seq<#seq<'T>>) -> seq<'T>

        /// <summary>The F# compiler emits calls to this function to 
        /// implement the <c>while</c> operator for F# sequence expressions.</summary>
        ///
        /// <param name="guard">A function that indicates whether iteration should continue.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        val EnumerateWhile   : guard:(unit -> bool) -> source:seq<'T> -> seq<'T>

        /// <summary>The F# compiler emits calls to this function to 
        /// implement the <c>try/finally</c> operator for F# sequence expressions.</summary>
        ///
        /// <param name="source">The input sequence.</param>
        /// <param name="compensation">A computation to be included in an enumerator's Dispose method.</param>
        ///
        /// <returns>The result sequence.</returns>
        val EnumerateThenFinally :  source:seq<'T> -> compensation:(unit -> unit) -> seq<'T>
        
        /// <summary>The F# compiler emits calls to this function to implement the compiler-intrinsic
        /// conversions from untyped System.Collections.IEnumerable sequences to typed sequences.</summary>
        ///
        /// <param name="create">An initializer function.</param>
        /// <param name="moveNext">A function to iterate and test if end of sequence is reached.</param>
        /// <param name="current">A function to retrieve the current element.</param>
        ///
        /// <returns>The resulting typed sequence.</returns>
        val EnumerateFromFunctions: create:(unit -> 'T) -> moveNext:('T -> bool) -> current:('T -> 'U) -> seq<'U>

        /// <summary>The F# compiler emits calls to this function to implement the <c>use</c> operator for F# sequence
        /// expressions.</summary>
        ///
        /// <param name="resource">The resource to be used and disposed.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        val EnumerateUsing : resource:'T -> source:('T -> 'Collection) -> seq<'U> when 'T :> IDisposable and 'Collection :> seq<'U>

        /// <summary>Creates an anonymous event with the given handlers.</summary>
        ///
        /// <param name="addHandler">A function to handle adding a delegate for the event to trigger.</param>
        /// <param name="removeHandler">A function to handle removing a delegate that the event triggers.</param>
        /// <param name="createHandler">A function to produce the delegate type the event can trigger.</param>
        ///
        /// <returns>The initialized event.</returns>
        val CreateEvent : addHandler : ('Delegate -> unit) -> removeHandler : ('Delegate -> unit) -> createHandler : ((obj -> 'Args -> unit) -> 'Delegate) -> Microsoft.FSharp.Control.IEvent<'Delegate,'Args>

    [<AbstractClass>]
    /// <summary>The F# compiler emits implementations of this type for compiled sequence expressions.</summary>
    type GeneratedSequenceBase<'T> =
        /// <summary>The F# compiler emits implementations of this type for compiled sequence expressions.</summary>
        ///
        /// <returns>A new sequence generator for the expression.</returns>
        new : unit -> GeneratedSequenceBase<'T>

        /// <summary>The F# compiler emits implementations of this type for compiled sequence expressions.</summary>
        ///
        /// <returns>A new enumerator for the sequence.</returns>
        abstract GetFreshEnumerator : unit -> IEnumerator<'T>

        /// <summary>The F# compiler emits implementations of this type for compiled sequence expressions.</summary>
        ///
        /// <param name="result">A reference to the sequence.</param>
        ///
        /// <returns>A 0, 1, and 2 respectively indicate Stop, Yield, and Goto conditions for the sequence generator.</returns>
        abstract GenerateNext : result:byref<IEnumerable<'T>> -> int

        /// <summary>The F# compiler emits implementations of this type for compiled sequence expressions.</summary>
        abstract Close: unit -> unit

        /// <summary>The F# compiler emits implementations of this type for compiled sequence expressions.</summary>
        abstract CheckClose: bool

        /// <summary>The F# compiler emits implementations of this type for compiled sequence expressions.</summary>
        abstract LastGenerated : 'T
        interface IEnumerable<'T> 
        interface IEnumerable
        interface IEnumerator<'T> 
        interface IEnumerator 
        interface IDisposable 

    /// Collects elements and builds a list
    [<Struct; NoEquality; NoComparison>]
    type ListCollector<'T> =
        [<DefaultValue(false)>]
        val mutable internal Result: 'T list

        [<DefaultValue(false)>]
        val mutable internal LastCons: 'T list

        /// Add an element to the collector
        member Add: value: 'T -> unit

        /// Add multiple elements to the collector
        member AddMany: values: seq<'T> -> unit

        /// Add multiple elements to the collector and return the resulting list
        member AddManyAndClose: values: seq<'T> -> 'T list

        /// Return the resulting list
        member Close: unit -> 'T list

    /// Collects elements and builds an array
    [<Struct; NoEquality; NoComparison>]
    type ArrayCollector<'T> =
        [<DefaultValue(false)>]
        val mutable internal ResizeArray: ResizeArray<'T>
        [<DefaultValue(false)>]
        val mutable internal First: 'T
        [<DefaultValue(false)>]
        val mutable internal Second: 'T
        [<DefaultValue(false)>]
        val mutable internal Count: int

        /// Add an element to the collector
        member Add: value: 'T -> unit

        /// Add multiple elements to the collector
        member AddMany: values: seq<'T> -> unit

        /// Add multiple elements to the collector and return the resulting array
        member AddManyAndClose: values: seq<'T> -> 'T[]

        /// Return the resulting list
        member Close: unit -> 'T[]

