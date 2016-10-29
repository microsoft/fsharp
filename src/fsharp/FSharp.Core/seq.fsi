// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.FSharp.Collections

    open System
    open System.Collections
    open System.Collections.Generic
    open Microsoft.FSharp.Core
    open Microsoft.FSharp.Collections
        

    /// <summary>Basic operations on IEnumerables.</summary>
    [<RequireQualifiedAccess>]
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Seq = 
        module Composer =
            module Internal =
                /// <summary>PipeIdx denotes the index of the element within the pipeline. 0 denotes the
                /// source of the chain.</summary>
                type PipeIdx = int
                type ``PipeIdx?`` = Nullable<PipeIdx>

                /// <summary>ICompletionChaining is used to correctly handle cleaning up of the pipeline. A
                /// base implementation is provided in Consumer, and should not be overwritten. Consumer
                /// provides it's own OnComplete and OnDispose function which should be used to handle
                /// a particular consumers cleanup.</summary>
                type ICompletionChaining =
                    /// <summary>OnComplete is used to determine if the object has been processed correctly, 
                    /// and possibly throw exceptions to denote incorrect application (i.e. such as a Take
                    /// operation which didn't have a source at least as large as was required). It is
                    /// not called in the case of an exception being thrown whilst the stream is still
                    /// being processed.</summary>
                    abstract OnComplete : PipeIdx -> unit
                    /// <summary>OnDispose is used to cleanup the stream. It is always called at the last operation
                    /// after the enumeration has completed.</summary>
                    abstract OnDispose : unit -> unit

                type IOutOfBand =
                    abstract StopFurtherProcessing : PipeIdx -> unit

                /// <summary>Consumer is the base class of all elements within the pipeline</summary>
                [<AbstractClass>]
                type Consumer<'T,'U> =
                    interface ICompletionChaining
                    new : unit -> Consumer<'T,'U>
                    abstract member ProcessNext : input:'T -> bool
                    abstract member OnComplete : PipeIdx -> unit
                    abstract member OnDispose : unit -> unit

                /// <summary>Values is a mutable struct. It can be embedded within the folder type
                /// if two values are required for the calculation.</summary>
                [<Struct; NoComparison; NoEquality>]
                type Values<'a,'b> =
                    new : a:'a * b:'b -> Values<'a,'b>
                    val mutable _1: 'a
                    val mutable _2: 'b

                /// <summary>Values is a mutable struct. It can be embedded within the folder type
                /// if three values are required for the calculation.</summary>
                [<Struct; NoComparison; NoEquality>]
                type Values<'a,'b,'c> =
                    new : a:'a * b:'b * c:'c -> Values<'a,'b,'c>
                    val mutable _1: 'a
                    val mutable _2: 'b
                    val mutable _3: 'c

                /// <summary>Folder is a base class to assist with fold-like operations. It's intended usage
                /// is as a base class for an object expression that will be used from within
                /// the ForEach function.</summary>
                [<AbstractClass>]
                type Folder<'T,'U> =
                    inherit Consumer<'T,'T>
                    new : init:'U -> Folder<'T,'U>
                    val mutable Value: 'U

                type ISeqFactory<'T,'U> =
                    abstract member Create : IOutOfBand -> ``PipeIdx?`` -> Consumer<'U,'V> -> Consumer<'T,'V>
                    abstract member PipeIdx : PipeIdx

                type ISeq<'T> =
                    inherit System.Collections.Generic.IEnumerable<'T>
                    abstract member Compose : ISeqFactory<'T,'U> -> ISeq<'U>
                    abstract member ForEach : f:((unit -> unit) -> 'a) -> 'a when 'a :> Consumer<'T,'T>

        /// <summary>Returns a new sequence that contains the cartesian product of the two input sequences.</summary>
        /// <param name="source1">The first sequence.</param>
        /// <param name="source2">The second sequence.</param>
        /// <returns>The result sequence.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when either of the input sequences is null.</exception>
        [<CompiledName("AllPairs")>]
        val allPairs: source1:seq<'T1> -> source2:seq<'T2> -> seq<'T1 * 'T2>

        /// <summary>Wraps the two given enumerations as a single concatenated
        /// enumeration.</summary>
        ///
        /// <remarks>The returned sequence may be passed between threads safely. However, 
        /// individual IEnumerator values generated from the returned sequence should not be accessed
        /// concurrently.</remarks>
        ///
        /// <param name="source1">The first sequence.</param>
        /// <param name="source2">The second sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when either of the two provided sequences is
        /// null.</exception>
        [<CompiledName("Append")>]
        val append: source1:seq<'T>  -> source2:seq<'T> -> seq<'T> 

        /// <summary>Returns the average of the elements in the sequence.</summary>
        ///
        /// <remarks>The elements are averaged using the <c>+</c> operator, <c>DivideByInt</c> method and <c>Zero</c> property 
        /// associated with the element type.</remarks>
        ///
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The average.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <exception cref="System.ArgumentException">Thrown when the input sequence has zero elements.</exception>
        [<CompiledName("Average")>]
        val inline average   : source:seq<(^T)> -> ^T 
                                      when ^T : (static member ( + ) : ^T * ^T -> ^T) 
                                      and  ^T : (static member DivideByInt : ^T * int -> ^T) 
                                      and  ^T : (static member Zero : ^T)

        /// <summary>Returns the average of the results generated by applying the function to each element 
        /// of the sequence.</summary>
        ///
        /// <remarks>The elements are averaged using the <c>+</c> operator, <c>DivideByInt</c> method and <c>Zero</c> property 
        /// associated with the generated type.</remarks>
        ///
        /// <param name="projection">A function applied to transform each element of the sequence.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The average.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <exception cref="System.ArgumentException">Thrown when the input sequence has zero elements.</exception>
        [<CompiledName("AverageBy")>]
        val inline averageBy   : projection:('T -> ^U) -> source:seq<'T>  -> ^U 
                                      when ^U : (static member ( + ) : ^U * ^U -> ^U) 
                                      and  ^U : (static member DivideByInt : ^U * int -> ^U) 
                                      and  ^U : (static member Zero : ^U)

        /// <summary>Returns a sequence that corresponds to a cached version of the input sequence.
        /// This result sequence will have the same elements as the input sequence. The result 
        /// can be enumerated multiple times. The input sequence will be enumerated at most 
        /// once and only as far as is necessary.  Caching a sequence is typically useful when repeatedly
        /// evaluating items in the original sequence is computationally expensive or if
        /// iterating the sequence causes side-effects that the user does not want to be
        /// repeated multiple times.
        ///
        /// Enumeration of the result sequence is thread safe in the sense that multiple independent IEnumerator
        /// values may be used simultaneously from different threads (accesses to 
        /// the internal lookaside table are thread safe). Each individual IEnumerator
        /// is not typically thread safe and should not be accessed concurrently.</summary>
        ///
        /// <remarks>Once enumeration of the input sequence has started,
        /// it's enumerator will be kept live by this object until the enumeration has completed.
        /// At that point, the enumerator will be disposed. 
        ///
        /// The enumerator may be disposed and underlying cache storage released by 
        /// converting the returned sequence object to type IDisposable, and calling the Dispose method
        /// on this object. The sequence object may then be re-enumerated and a fresh enumerator will
        /// be used.</remarks>
        ///
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("Cache")>]
        val cache: source:seq<'T> -> seq<'T>

        /// <summary>Wraps a loosely-typed System.Collections sequence as a typed sequence.</summary>
        ///
        /// <remarks>The use of this function usually requires a type annotation.
        /// An incorrect type annotation may result in runtime type
        /// errors.
        /// Individual IEnumerator values generated from the returned sequence should not be accessed concurrently.</remarks>
        /// 
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("Cast")>]
        val cast: source:IEnumerable -> seq<'T>

        /// <summary>Applies the given function to each element of the list. Return
        /// the list comprised of the results "x" for each element where
        /// the function returns Some(x).</summary>
        ///
        /// <remarks>The returned sequence may be passed between threads safely. However, 
        /// individual IEnumerator values generated from the returned sequence should not
        /// be accessed concurrently.</remarks>
        ///
        /// <param name="chooser">A function to transform items of type T into options of type U.</param>
        /// <param name="source">The input sequence of type T.</param>
        ///
        /// <returns>The result sequence.</returns>
        /// 
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("Choose")>]
        val choose: chooser:('T -> 'U option) -> source:seq<'T> -> seq<'U>

        /// <summary>Divides the input sequence into chunks of size at most <c>chunkSize</c>.</summary>
        /// <param name="chunkSize">The maximum size of each chunk.</param>
        /// <param name="source">The input sequence.</param>
        /// <returns>The sequence divided into chunks.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <exception cref="System.ArgumentException">Thrown when <c>chunkSize</c> is not positive.</exception>
        [<CompiledName("ChunkBySize")>]
        val chunkBySize: chunkSize:int -> source:seq<'T> -> seq<'T[]>

        /// <summary>Applies the given function to each element of the sequence and concatenates all the
        /// results.</summary>
        ///
        /// <remarks>Remember sequence is lazy, effects are delayed until it is enumerated.</remarks>
        ///
        /// <param name="mapping">A function to transform elements of the input sequence into the sequences
        /// that will then be concatenated.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("Collect")>]
        val collect: mapping:('T -> 'Collection) -> source:seq<'T> -> seq<'U>  when 'Collection :> seq<'U>

        /// <summary>Compares two sequences using the given comparison function, element by element.</summary>
        ///
        /// <param name="comparer">A function that takes an element from each sequence and returns an int.
        /// If it evaluates to a non-zero value iteration is stopped and that value is returned.</param>
        /// <param name="source1">The first input sequence.</param>
        /// <param name="source2">The second input sequence.</param>
        ///
        /// <returns>Returns the first non-zero result from the comparison function.  If the end of a sequence
        /// is reached it returns a -1 if the first sequence is shorter and a 1 if the second sequence
        /// is shorter.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when either of the input sequences
        /// is null.</exception>
        [<CompiledName("CompareWith")>]
        val compareWith: comparer:('T -> 'T -> int) -> source1:seq<'T> -> source2:seq<'T> -> int

        /// <summary>Combines the given enumeration-of-enumerations as a single concatenated
        /// enumeration.</summary>
        ///
        /// <remarks>The returned sequence may be passed between threads safely. However, 
        /// individual IEnumerator values generated from the returned sequence should not be accessed concurrently.</remarks>
        ///
        /// <param name="sources">The input enumeration-of-enumerations.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("Concat")>]
        val concat: sources:seq<'Collection> -> seq<'T> when 'Collection :> seq<'T>

        /// <summary>Tests if the sequence contains the specified element.</summary>
        /// <param name="value">The value to locate in the input sequence.</param>
        /// <param name="source">The input sequence.</param>
        /// <returns>True if the input sequence contains the specified element; false otherwise.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("Contains")>]
        val inline contains: value:'T -> source:seq<'T> -> bool when 'T : equality

        /// <summary>Applies a key-generating function to each element of a sequence and returns a sequence yielding unique
        /// keys and their number of occurrences in the original sequence.</summary>
        /// 
        /// <remarks>Note that this function returns a sequence that digests the whole initial sequence as soon as 
        /// that sequence is iterated. As a result this function should not be used with 
        /// large or infinite sequences. The function makes no assumption on the ordering of the original 
        /// sequence.</remarks>
        ///
        /// <param name="projection">A function transforming each item of the input sequence into a key to be
        /// compared against the others.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("CountBy")>]
        val countBy : projection:('T -> 'Key) -> source:seq<'T> -> seq<'Key * int> when 'Key : equality

        /// <summary>Returns a sequence that is built from the given delayed specification of a
        /// sequence.</summary>
        ///
        /// <remarks>The input function is evaluated each time an IEnumerator for the sequence 
        /// is requested.</remarks>
        ///
        /// <param name="generator">The generating function for the sequence.</param>
        [<CompiledName("Delay")>]
        val delay   : generator:(unit -> seq<'T>) -> seq<'T>

        /// <summary>Returns a sequence that contains no duplicate entries according to generic hash and
        /// equality comparisons on the entries.
        /// If an element occurs multiple times in the sequence then the later occurrences are discarded.</summary>
        ///
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("Distinct")>]
        val distinct: source:seq<'T> -> seq<'T> when 'T : equality

        /// <summary>Returns a sequence that contains no duplicate entries according to the 
        /// generic hash and equality comparisons on the keys returned by the given key-generating function.
        /// If an element occurs multiple times in the sequence then the later occurrences are discarded.</summary>
        ///
        /// <param name="projection">A function transforming the sequence items into comparable keys.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("DistinctBy")>]
        val distinctBy: projection:('T -> 'Key) -> source:seq<'T> -> seq<'T> when 'Key : equality

        /// <summary>Splits the input sequence into at most <c>count</c> chunks.</summary>
        /// <remarks>This function returns a sequence that digests the whole initial sequence as soon as that
        /// sequence is iterated. As a result this function should not be used with large or infinite sequences.</remarks>
        /// <param name="count">The maximum number of chunks.</param>
        /// <param name="source">The input sequence.</param>
        /// <returns>The sequence split into chunks.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <exception cref="System.ArgumentException">Thrown when <c>count</c> is not positive.</exception>
        /// <remarks>This function consumes the whole input sequence before yielding the first element of the result sequence.</remarks>
        [<CompiledName("SplitInto")>]
        val splitInto: count:int -> source:seq<'T> -> seq<'T[]>

        /// <summary>Creates an empty sequence.</summary>
        ///
        /// <returns>An empty sequence.</returns>
        [<GeneralizableValueAttribute>]
        [<CompiledName("Empty")>]
        val empty<'T> : seq<'T>

        /// <summary>Returns a new sequence with the distinct elements of the second sequence which do not appear in the first sequence,
        /// using generic hash and equality comparisons to compare values.</summary>
        ///
        /// <remarks>Note that this function returns a sequence that digests the whole of the first input sequence as soon as
        /// the result sequence is iterated. As a result this function should not be used with
        /// large or infinite sequences in the first parameter. The function makes no assumption on the ordering of the first input
        /// sequence.</remarks>
        ///
        /// <param name="itemsToExclude">A sequence whose elements that also occur in the second sequence will cause those elements to be
        /// removed from the returned sequence.</param>
        /// <param name="source">A sequence whose elements that are not also in first will be returned.</param>
        ///
        /// <returns>A sequence that contains the set difference of the elements of two sequences.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when either of the two input sequences is null.</exception>
        [<CompiledName("Except")>]
        val except: itemsToExclude:seq<'T> -> source:seq<'T> -> seq<'T> when 'T : equality

        /// <summary>Tests if any element of the sequence satisfies the given predicate.</summary>
        ///
        /// <remarks>The predicate is applied to the elements of the input sequence. If any application 
        /// returns true then the overall result is true and no further elements are tested. 
        /// Otherwise, false is returned.</remarks>
        ///
        /// <param name="predicate">A function to test each item of the input sequence.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>True if any result from the predicate is true; false otherwise.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("Exists")>]
        val exists: predicate:('T -> bool) -> source:seq<'T> -> bool

        /// <summary>Tests if any pair of corresponding elements of the input sequences satisfies the given predicate.</summary>
        ///
        /// <remarks>The predicate is applied to matching elements in the two sequences up to the lesser of the 
        /// two lengths of the collections. If any application returns true then the overall result is 
        /// true and no further elements are tested. Otherwise, false is returned. If one sequence is shorter than 
        /// the other then the remaining elements of the longer sequence are ignored.</remarks>
        ///
        /// <param name="predicate">A function to test each pair of items from the input sequences.</param>
        /// <param name="source1">The first input sequence.</param>
        /// <param name="source2">The second input sequence.</param>
        ///
        /// <returns>True if any result from the predicate is true; false otherwise.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when either of the two input sequences is null.</exception>
        [<CompiledName("Exists2")>]
        val exists2: predicate:('T1 -> 'T2 -> bool) -> source1:seq<'T1> -> source2:seq<'T2> -> bool

        /// <summary>Returns a new collection containing only the elements of the collection
        /// for which the given predicate returns "true". This is a synonym for Seq.where.</summary>
        ///
        /// <remarks>The returned sequence may be passed between threads safely. However, 
        /// individual IEnumerator values generated from the returned sequence should not be accessed concurrently.
        ///
        /// Remember sequence is lazy, effects are delayed until it is enumerated.</remarks>
        ///
        /// <param name="predicate">A function to test whether each item in the input sequence should be included in the output.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>    
        [<CompiledName("Filter")>]
        val filter: predicate:('T -> bool) -> source:seq<'T> -> seq<'T>

        /// <summary>Returns a new collection containing only the elements of the collection
        /// for which the given predicate returns "true".</summary>
        ///
        /// <remarks>The returned sequence may be passed between threads safely. However, 
        /// individual IEnumerator values generated from the returned sequence should not be accessed concurrently.
        ///
        /// Remember sequence is lazy, effects are delayed until it is enumerated.
        /// 
        /// A synonym for Seq.filter.</remarks>
        ///
        /// <param name="predicate">A function to test whether each item in the input sequence should be included in the output.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>    
        [<CompiledName("Where")>]
        val where: predicate:('T -> bool) -> source:seq<'T> -> seq<'T>

        /// <summary>Returns the first element for which the given function returns True.</summary>
        ///
        /// <param name="predicate">A function to test whether an item in the sequence should be returned.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The first element for which the predicate returns True.</returns>
        ///
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">Thrown if no element returns true when
        /// evaluated by the predicate</exception>
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null</exception>
        [<CompiledName("Find")>]
        val find: predicate:('T -> bool) -> source:seq<'T> -> 'T

        /// <summary>Returns the last element for which the given function returns True.</summary>
        /// <remarks>This function digests the whole initial sequence as soon as it is called. As a
        /// result this function should not be used with large or infinite sequences.</remarks>
        /// <param name="predicate">A function to test whether an item in the sequence should be returned.</param>
        /// <param name="source">The input sequence.</param>
        /// <returns>The last element for which the predicate returns True.</returns>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">Thrown if no element returns true when
        /// evaluated by the predicate</exception>
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null</exception>
        /// <remarks>This function consumes the whole input sequence before returning the result.</remarks>
        [<CompiledName("FindBack")>]
        val findBack: predicate:('T -> bool) -> source:seq<'T> -> 'T

        /// <summary>Returns the index of the first element for which the given function returns True.</summary>
        ///
        /// <param name="predicate">A function to test whether the index of a particular element should be returned.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The index of the first element for which the predicate returns True.</returns>
        ///
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">Thrown if no element returns true when
        /// evaluated by the predicate</exception>
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null</exception>
        [<CompiledName("FindIndex")>]
        val findIndex: predicate:('T -> bool) -> source:seq<'T> -> int

        /// <summary>Returns the index of the last element for which the given function returns True.</summary>
        /// <remarks>This function digests the whole initial sequence as soon as it is called. As a
        /// result this function should not be used with large or infinite sequences.</remarks>
        /// <param name="predicate">A function to test whether the index of a particular element should be returned.</param>
        /// <param name="source">The input sequence.</param>
        /// <returns>The index of the last element for which the predicate returns True.</returns>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">Thrown if no element returns true when
        /// evaluated by the predicate</exception>
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null</exception>
        /// <remarks>This function consumes the whole input sequence before returning the result.</remarks>
        [<CompiledName("FindIndexBack")>]
        val findIndexBack: predicate:('T -> bool) -> source:seq<'T> -> int

        /// <summary>Applies a function to each element of the collection, threading an accumulator argument
        /// through the computation. If the input function is <c>f</c> and the elements are <c>i0...iN</c> 
        /// then computes <c>f (... (f s i0)...) iN</c></summary>
        ///
        /// <param name="folder">A function that updates the state with each element from the sequence.</param>
        /// <param name="state">The initial state.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The state object after the folding function is applied to each element of the sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("Fold")>]
        val fold<'T,'State> : folder:('State -> 'T -> 'State) -> state:'State -> source:seq<'T> -> 'State

        /// <summary>Applies a function to corresponding elements of two collections, threading an accumulator argument
        /// through the computation. The two sequences need not have equal lengths:
        /// when one sequence is exhausted any remaining elements in the other sequence are ignored.
        /// If the input function is <c>f</c> and the elements are <c>i0...iN</c> and <c>j0...jN</c>
        /// then computes <c>f (... (f s i0 j0)...) iN jN</c>.</summary>
        /// <param name="folder">The function to update the state given the input elements.</param>
        /// <param name="state">The initial state.</param>
        /// <param name="source1">The first input sequence.</param>
        /// <param name="source2">The second input sequence.</param>
        /// <returns>The final state value.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the either of the input sequences is null.</exception>
        [<CompiledName("Fold2")>]
        val fold2<'T1,'T2,'State> : folder:('State -> 'T1 -> 'T2 -> 'State) -> state:'State -> source1:seq<'T1> -> source2:seq<'T2> -> 'State

        /// <summary>Applies a function to each element of the collection, starting from the end, threading an accumulator argument
        /// through the computation. If the input function is <c>f</c> and the elements are <c>i0...iN</c>
        /// then computes <c>f i0 (... (f iN s)...)</c></summary>
        /// <param name="folder">The function to update the state given the input elements.</param>
        /// <param name="source">The input sequence.</param>
        /// <param name="state">The initial state.</param>
        /// <returns>The state object after the folding function is applied to each element of the sequence.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <remarks>This function consumes the whole input sequence before returning the result.</remarks>
        [<CompiledName("FoldBack")>]
        val foldBack<'T,'State> : folder:('T -> 'State -> 'State) -> source:seq<'T> -> state:'State -> 'State

        /// <summary>Applies a function to corresponding elements of two collections, starting from the end of the shorter collection,
        /// threading an accumulator argument through the computation. The two sequences need not have equal lengths.
        /// If the input function is <c>f</c> and the elements are <c>i0...iN</c> and <c>j0...jM</c>, N &lt; M
        /// then computes <c>f i0 j0 (... (f iN jN s)...)</c>.</summary>
        /// <param name="folder">The function to update the state given the input elements.</param>
        /// <param name="source1">The first input sequence.</param>
        /// <param name="source2">The second input sequence.</param>
        /// <param name="state">The initial state.</param>
        /// <returns>The final state value.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the either of the input sequences is null.</exception>
        [<CompiledName("FoldBack2")>]
        val foldBack2<'T1,'T2,'State> : folder:('T1 -> 'T2 -> 'State -> 'State) -> source1:seq<'T1> -> source2:seq<'T2> -> state:'State -> 'State

        /// <summary>Tests if all elements of the sequence satisfy the given predicate.</summary>
        ///
        /// <remarks>The predicate is applied to the elements of the input sequence. If any application 
        /// returns false then the overall result is false and no further elements are tested. 
        /// Otherwise, true is returned.</remarks>
        ///
        /// <param name="predicate">A function to test an element of the input sequence.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>True if every element of the sequence satisfies the predicate; false otherwise.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("ForAll")>]
        val forall: predicate:('T -> bool) -> source:seq<'T> -> bool

        /// <summary>Tests the all pairs of elements drawn from the two sequences satisfy the
        /// given predicate. If one sequence is shorter than 
        /// the other then the remaining elements of the longer sequence are ignored.</summary>
        ///
        /// <param name="predicate">A function to test pairs of elements from the input sequences.</param>
        /// <param name="source1">The first input sequence.</param>
        /// <param name="source2">The second input sequence.</param>
        ///
        /// <returns>True if all pairs satisfy the predicate; false otherwise.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when either of the input sequences is null.</exception>
        [<CompiledName("ForAll2")>]
        val forall2: predicate:('T1 -> 'T2 -> bool) -> source1:seq<'T1> -> source2:seq<'T2> -> bool

        /// <summary>Applies a key-generating function to each element of a sequence and yields a sequence of 
        /// unique keys. Each unique key contains a sequence of all elements that match 
        /// to this key.</summary>
        /// 
        /// <remarks>This function returns a sequence that digests the whole initial sequence as soon as 
        /// that sequence is iterated. As a result this function should not be used with 
        /// large or infinite sequences. The function makes no assumption on the ordering of the original 
        /// sequence.</remarks>
        ///
        /// <param name="projection">A function that transforms an element of the sequence into a comparable key.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        [<CompiledName("GroupBy")>]
        val groupBy : projection:('T -> 'Key) -> source:seq<'T> -> seq<'Key * seq<'T>> when 'Key : equality

        /// <summary>Returns the first element of the sequence.</summary>
        ///
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The first element of the sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <exception cref="System.ArgumentException">Thrown when the input does not have any elements.</exception>
        [<CompiledName("Head")>]
        val head: source:seq<'T> -> 'T

        /// <summary>Returns the first element of the sequence, or None if the sequence is empty.</summary>
        ///
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The first element of the sequence or None.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("TryHead")>]
        val tryHead: source:seq<'T> -> 'T option

        /// <summary>Returns the last element of the sequence.</summary>
        /// <param name="source">The input sequence.</param>
        /// <returns>The last element of the sequence.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <exception cref="System.ArgumentException">Thrown when the input does not have any elements.</exception>
        [<CompiledName("Last")>]
        val last: source:seq<'T> -> 'T

        /// <summary>Returns the last element of the sequence.
        /// Return <c>None</c> if no such element exists.</summary>
        ///
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The last element of the sequence or None.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("TryLast")>]
        val tryLast: source:seq<'T> -> 'T option

        /// <summary>Returns the only element of the sequence.</summary>
        ///
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The only element of the sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <exception cref="System.ArgumentException">Thrown when the input does not have precisely one element.</exception>
        [<CompiledName("ExactlyOne")>]
        val exactlyOne: source:seq<'T> -> 'T

        /// <summary>Returns true if the sequence contains no elements, false otherwise.</summary>
        ///
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>True if the sequence is empty; false otherwise.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("IsEmpty")>]
        val isEmpty: source:seq<'T> -> bool

        /// <summary>Builds a new collection whose elements are the corresponding elements of the input collection
        /// paired with the integer index (from 0) of each element.</summary>
        /// <param name="source">The input sequence.</param>
        /// <returns>The result sequence.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("Indexed")>]
        val indexed: source:seq<'T> -> seq<int * 'T>

        /// <summary>Generates a new sequence which, when iterated, will return successive
        /// elements by calling the given function, up to the given count.  Each element is saved after its
        /// initialization.  The function is passed the index of the item being
        /// generated.</summary>
        ///
        /// <remarks>The returned sequence may be passed between threads safely. However, 
        /// individual IEnumerator values generated from the returned sequence should not be accessed concurrently.</remarks>
        ///
        /// <param name="count">The maximum number of items to generate for the sequence.</param>
        /// <param name="initializer">A function that generates an item in the sequence from a given index.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentException">Thrown when count is negative.</exception>
        [<CompiledName("Initialize")>]
        val init: count:int -> initializer:(int -> 'T) -> seq<'T>
        
        /// <summary>Generates a new sequence which, when iterated, will return successive
        /// elements by calling the given function.  The results of calling the function
        /// will not be saved, that is the function will be reapplied as necessary to
        /// regenerate the elements.  The function is passed the index of the item being
        /// generated.</summary>
        ///
        /// <remarks>The returned sequence may be passed between threads safely. However, 
        /// individual IEnumerator values generated from the returned sequence should not be accessed concurrently.
        /// Iteration can continue up to <c>Int32.MaxValue</c>.</remarks>
        ///
        /// <param name="initializer">A function that generates an item in the sequence from a given index.</param>
        ///
        /// <returns>The result sequence.</returns>
        [<CompiledName("InitializeInfinite")>]
        val initInfinite: initializer:(int -> 'T) -> seq<'T>

        /// <summary>Computes the element at the specified index in the collection.</summary>
        /// <param name="index">The index of the element to retrieve.</param>
        /// <param name="source">The input sequence.</param>
        /// <returns>The element at the specified index of the sequence.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <exception cref="System.ArgumentException">Thrown when the index is negative or the input sequence does not contain enough elements.</exception>
        [<CompiledName("Item")>]
        val item: index:int -> source:seq<'T> -> 'T

        /// <summary>Applies the given function to each element of the collection.</summary>
        ///
        /// <param name="action">A function to apply to each element of the sequence.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("Iterate")>]
        val iter: action:('T -> unit) -> source:seq<'T> -> unit

        /// <summary>Applies the given function to each element of the collection. The integer passed to the
        /// function indicates the index of element.</summary>
        ///
        /// <param name="action">A function to apply to each element of the sequence that can also access the current index.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("IterateIndexed")>]
        val iteri: action:(int -> 'T -> unit) -> source:seq<'T> -> unit

        /// <summary>Applies the given function to two collections simultaneously. If one sequence is shorter than 
        /// the other then the remaining elements of the longer sequence are ignored.</summary>
        ///
        /// <param name="action">A function to apply to each pair of elements from the input sequences.</param>
        /// <param name="source1">The first input sequence.</param>
        /// <param name="source2">The second input sequence.</param>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when either of the input sequences is null.</exception>
        [<CompiledName("Iterate2")>]
        val iter2: action:('T1 -> 'T2 -> unit) -> source1:seq<'T1> -> source2:seq<'T2> -> unit

        /// <summary>Applies the given function to two collections simultaneously. If one sequence is shorter than 
        /// the other then the remaining elements of the longer sequence are ignored. The integer passed to the
        /// function indicates the index of element.</summary>
        ///
        /// <param name="action">A function to apply to each pair of elements from the input sequences along with their index.</param>
        /// <param name="source1">The first input sequence.</param>
        /// <param name="source2">The second input sequence.</param>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when either of the input sequences is null.</exception>
        [<CompiledName("IterateIndexed2")>]
        val iteri2: action:(int -> 'T1 -> 'T2 -> unit) -> source1:seq<'T1> -> source2:seq<'T2> -> unit

        /// <summary>Returns the length of the sequence</summary>
        ///
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The length of the sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("Length")>]
        val length: source:seq<'T> -> int

        /// <summary>Builds a new collection whose elements are the results of applying the given function
        /// to each of the elements of the collection.  The given function will be applied
        /// as elements are demanded using the <c>MoveNext</c> method on enumerators retrieved from the
        /// object.</summary>
        ///
        /// <remarks>The returned sequence may be passed between threads safely. However, 
        /// individual IEnumerator values generated from the returned sequence should not be accessed concurrently.</remarks>
        ///
        /// <param name="mapping">A function to transform items from the input sequence.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("Map")>]
        val map  : mapping:('T -> 'U) -> source:seq<'T> -> seq<'U>

        /// <summary>Builds a new collection whose elements are the results of applying the given function
        /// to the corresponding pairs of elements from the two sequences. If one input sequence is shorter than 
        /// the other then the remaining elements of the longer sequence are ignored.</summary>
        ///
        /// <param name="mapping">A function to transform pairs of items from the input sequences.</param>
        /// <param name="source1">The first input sequence.</param>
        /// <param name="source2">The second input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when either of the input sequences is null.</exception>
        [<CompiledName("Map2")>]
        val map2: mapping:('T1 -> 'T2 -> 'U) -> source1:seq<'T1> -> source2:seq<'T2> -> seq<'U>

        /// <summary>Combines map and fold. Builds a new collection whose elements are the results of applying the given function
        /// to each of the elements of the collection. The function is also used to accumulate a final value.</summary>
        /// <remarks>This function digests the whole initial sequence as soon as it is called. As a result this function should
        /// not be used with large or infinite sequences.</remarks>
        /// <param name="mapping">The function to transform elements from the input collection and accumulate the final value.</param>
        /// <param name="state">The initial state.</param>
        /// <param name="array">The input collection.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the input collection is null.</exception>
        /// <returns>The collection of transformed elements, and the final accumulated value.</returns>
        /// <remarks>This function consumes the whole input sequence before yielding the first element of the result sequence.</remarks>
        [<CompiledName("MapFold")>]
        val mapFold<'T,'State,'Result> : mapping:('State -> 'T -> 'Result * 'State) -> state:'State -> source:seq<'T> -> seq<'Result> * 'State

        /// <summary>Combines map and foldBack. Builds a new collection whose elements are the results of applying the given function
        /// to each of the elements of the collection. The function is also used to accumulate a final value.</summary>
        /// <remarks>This function digests the whole initial sequence as soon as it is called. As a result this function should
        /// not be used with large or infinite sequences.</remarks>
        /// <param name="mapping">The function to transform elements from the input collection and accumulate the final value.</param>
        /// <param name="array">The input collection.</param>
        /// <param name="state">The initial state.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the input collection is null.</exception>
        /// <returns>The collection of transformed elements, and the final accumulated value.</returns>
        /// <remarks>This function consumes the whole input sequence before yielding the first element of the result sequence.</remarks>
        [<CompiledName("MapFoldBack")>]
        val mapFoldBack<'T,'State,'Result> : mapping:('T -> 'State -> 'Result * 'State) -> source:seq<'T> -> state:'State -> seq<'Result> * 'State

        /// <summary>Builds a new collection whose elements are the results of applying the given function
        /// to the corresponding triples of elements from the three sequences. If one input sequence if shorter than
        /// the others then the remaining elements of the longer sequences are ignored.</summary>
        ///
        /// <param name="mapping">The function to transform triples of elements from the input sequences.</param>
        /// <param name="source1">The first input sequence.</param>
        /// <param name="source2">The second input sequence.</param>
        /// <param name="source3">The third input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when any of the input sequences is null.</exception>
        [<CompiledName("Map3")>]
        val map3: mapping:('T1 -> 'T2 -> 'T3 -> 'U) -> source1:seq<'T1> -> source2:seq<'T2> -> source3:seq<'T3> -> seq<'U>

        /// <summary>Builds a new collection whose elements are the results of applying the given function
        /// to each of the elements of the collection. The integer index passed to the
        /// function indicates the index (from 0) of element being transformed.</summary>
        ///
        /// <param name="mapping">A function to transform items from the input sequence that also supplies the current index.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("MapIndexed")>]
        val mapi: mapping:(int -> 'T -> 'U) -> source:seq<'T> -> seq<'U>

        /// <summary>Builds a new collection whose elements are the results of applying the given function
        /// to the corresponding pairs of elements from the two sequences. If one input sequence is shorter than 
        /// the other then the remaining elements of the longer sequence are ignored. The integer index passed to the
        /// function indicates the index (from 0) of element being transformed.</summary>
        ///
        /// <param name="mapping">A function to transform pairs of items from the input sequences that also supplies the current index.</param>
        /// <param name="source1">The first input sequence.</param>
        /// <param name="source2">The second input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when either of the input sequences is null.</exception>
        [<CompiledName("MapIndexed2")>]
        val mapi2: mapping:(int -> 'T1 -> 'T2 -> 'U) -> source1:seq<'T1> -> source2:seq<'T2> -> seq<'U>

        /// <summary>Returns the greatest of all elements of the sequence, compared via Operators.max</summary>
        ///
        /// <param name="source">The input sequence.</param>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <exception cref="System.ArgumentException">Thrown when the input sequence is empty.</exception>
        ///
        /// <returns>The largest element of the sequence.</returns>
        [<CompiledName("Max")>]
        val inline max     : source:seq<'T> -> 'T when 'T : comparison 

        /// <summary>Returns the greatest of all elements of the sequence, compared via Operators.max on the function result.</summary>
        ///
        /// <param name="projection">A function to transform items from the input sequence into comparable keys.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The largest element of the sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <exception cref="System.ArgumentException">Thrown when the input sequence is empty.</exception>
        [<CompiledName("MaxBy")>]
        val inline maxBy  : projection:('T -> 'U) -> source:seq<'T> -> 'T when 'U : comparison 

(*
        /// <summary>Returns the greatest function result from the elements of the sequence, compared via Operators.max.</summary>
        ///
        /// <param name="projection">A function to transform items from the input sequence into comparable keys.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The largest element of the sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <exception cref="System.ArgumentException">Thrown when the input sequence is empty.</exception>
        [<CompiledName("MaxValueBy")>]
        val inline maxValBy  : projection:('T -> 'U) -> source:seq<'T> -> 'U when 'U : comparison 
*)

        /// <summary>Returns the lowest of all elements of the sequence, compared via <c>Operators.min</c>.</summary>
        ///
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The smallest element of the sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <exception cref="System.ArgumentException">Thrown when the input sequence is empty.</exception>
        [<CompiledName("Min")>]
        val inline min     : source:seq<'T> -> 'T when 'T : comparison 

        /// <summary>Returns the lowest of all elements of the sequence, compared via Operators.min on the function result.</summary>
        ///
        /// <param name="projection">A function to transform items from the input sequence into comparable keys.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The smallest element of the sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <exception cref="System.ArgumentException">Thrown when the input sequence is empty.</exception>
        [<CompiledName("MinBy")>]
        val inline minBy  : projection:('T -> 'U) -> source:seq<'T> -> 'T when 'U : comparison 

(*
        /// <summary>Returns the lowest function result from the elements of the sequence, compared via Operators.max.</summary>
        ///
        /// <param name="projection">A function to transform items from the input sequence into comparable keys.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The smallest element of the sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <exception cref="System.ArgumentException">Thrown when the input sequence is empty.</exception>
        [<CompiledName("MinValueBy")>]
        val inline minValBy  : projection:('T -> 'U) -> source:seq<'T> -> 'U when 'U : comparison 
*)

        /// <summary>Computes the nth element in the collection.</summary>
        ///
        /// <param name="index">The index of element to retrieve.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The nth element of the sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <exception cref="System.ArgumentException">Thrown when the index is negative or the input sequence does not contain enough elements.</exception>
        [<CompiledName("Get")>]
        [<Obsolete("please use Seq.item")>]
        val nth: index:int -> source:seq<'T> -> 'T


        [<CompiledName("OfArray")>]
        /// <summary>Views the given array as a sequence.</summary>
        ///
        /// <param name="source">The input array.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        val ofArray: source:'T[] -> seq<'T>

        [<CompiledName("OfList")>]
        /// <summary>Views the given list as a sequence.</summary>
        ///
        /// <param name="source">The input list.</param>
        ///
        /// <returns>The result sequence.</returns>
        val ofList: source:'T list -> seq<'T>

        /// <summary>Returns a sequence of each element in the input sequence and its predecessor, with the
        /// exception of the first element which is only returned as the predecessor of the second element.</summary>
        ///
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("Pairwise")>]
        val pairwise: source:seq<'T> -> seq<'T * 'T>

        /// <summary>Returns a sequence with all elements permuted according to the
        /// specified permutation.</summary>
        ///
        /// <remarks>Note that this function returns a sequence that digests the whole initial sequence as soon as
        /// that sequence is iterated. As a result this function should not be used with
        /// large or infinite sequences.</remarks>
        ///
        /// <param name="indexMap">The function that maps input indices to output indices.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <exception cref="System.ArgumentException">Thrown when indexMap does not produce a valid permutation.</exception>
        /// <remarks>This function consumes the whole input sequence before yielding the first element of the result sequence.</remarks>
        [<CompiledName("Permute")>]
        val permute: indexMap:(int -> int) -> source:seq<'T> -> seq<'T>

        /// <summary>Applies the given function to successive elements, returning the first
        /// <c>x</c> where the function returns "Some(x)".</summary>
        ///
        /// <param name="chooser">A function to transform each item of the input sequence into an option of the output type.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The selected element.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">Thrown when every item of the sequence
        /// evaluates to <c>None</c> when the given function is applied.</exception>
        [<CompiledName("Pick")>]
        val pick: chooser:('T -> 'U option) -> source:seq<'T> -> 'U 

        /// <summary>Builds a new sequence object that delegates to the given sequence object. This ensures 
        /// the original sequence cannot be rediscovered and mutated by a type cast. For example, 
        /// if given an array the returned sequence will return the elements of the array, but
        /// you cannot cast the returned sequence object to an array.</summary>
        ///
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("ReadOnly")>]
        val readonly : source:seq<'T> -> seq<'T>

        /// <summary>Applies a function to each element of the sequence, threading an accumulator argument
        /// through the computation. Begin by applying the function to the first two elements.
        /// Then feed this result into the function along with the third element and so on.  
        /// Return the final result.</summary>
        ///
        /// <param name="reduction">A function that takes in the current accumulated result and the next
        /// element of the sequence to produce the next accumulated result.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The final result of the reduction function.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <exception cref="System.ArgumentException">Thrown when the input sequence is empty.</exception>
        [<CompiledName("Reduce")>]
        val reduce: reduction:('T -> 'T -> 'T) -> source:seq<'T> -> 'T

        /// <summary>Creates a sequence by replicating the given initial value.</summary>
        /// <param name="count">The number of elements to replicate.</param>
        /// <param name="initial">The value to replicate</param>
        /// <returns>The generated sequence.</returns>
        [<CompiledName("Replicate")>]
        val replicate: count:int -> initial:'T -> seq<'T>

        /// <summary>Applies a function to each element of the sequence, starting from the end, threading an accumulator argument
        /// through the computation. If the input function is <c>f</c> and the elements are <c>i0...iN</c> 
        /// then computes <c>f i0 (...(f iN-1 iN))</c>.</summary>
        /// <param name="reduction">A function that takes in the next-to-last element of the sequence and the
        /// current accumulated result to produce the next accumulated result.</param>
        /// <param name="source">The input sequence.</param>
        /// <returns>The final result of the reductions.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <exception cref="System.ArgumentException">Thrown when the input sequence is empty.</exception>
        /// <remarks>This function consumes the whole input sequence before returning the result.</remarks>
        [<CompiledName("ReduceBack")>]
        val reduceBack: reduction:('T -> 'T -> 'T) -> source:seq<'T> -> 'T

        /// <summary>Returns a new sequence with the elements in reverse order.</summary>
        /// <param name="source">The input sequence.</param>
        /// <returns>The reversed sequence.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <remarks>This function consumes the whole input sequence before yielding the first element of the reversed sequence.</remarks>
        [<CompiledName("Reverse")>]
        val rev: source:seq<'T> -> seq<'T>

        /// <summary>Like fold, but computes on-demand and returns the sequence of intermediary and final results.</summary>
        ///
        /// <param name="folder">A function that updates the state with each element from the sequence.</param>
        /// <param name="state">The initial state.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The resulting sequence of computed states.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("Scan")>]
        val scan<'T,'State> : folder:('State -> 'T -> 'State) -> state:'State -> source:seq<'T> -> seq<'State>

        /// <summary>Like <c>foldBack</c>, but returns the sequence of intermediary and final results.</summary>
        /// <remarks>This function returns a sequence that digests the whole initial sequence as soon as that
        /// sequence is iterated. As a result this function should not be used with large or infinite sequences.
        /// </remarks>
        /// <param name="folder">A function that updates the state with each element from the sequence.</param>
        /// <param name="source">The input sequence.</param>
        /// <param name="state">The initial state.</param>
        /// <returns>The resulting sequence of computed states.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <remarks>This function consumes the whole input sequence before yielding the first element of the result sequence.</remarks>
        [<CompiledName("ScanBack")>]
        val scanBack<'T,'State> : folder:('T -> 'State -> 'State) -> source:seq<'T> -> state:'State -> seq<'State>

        /// <summary>Returns a sequence that yields one item only.</summary>
        ///
        /// <param name="value">The input item.</param>
        ///
        /// <returns>The result sequence of one item.</returns>
        [<CompiledName("Singleton")>]
        val singleton: value:'T -> seq<'T>

        /// <summary>Returns a sequence that skips N elements of the underlying sequence and then yields the
        /// remaining elements of the sequence.</summary>
        ///
        /// <param name="count">The number of items to skip.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when count exceeds the number of elements
        /// in the sequence.</exception>
        [<CompiledName("Skip")>]
        val skip: count:int -> source:seq<'T> -> seq<'T>

        /// <summary>Returns a sequence that, when iterated, skips elements of the underlying sequence while the 
        /// given predicate returns True, and then yields the remaining elements of the sequence.</summary>
        ///
        /// <param name="predicate">A function that evaluates an element of the sequence to a boolean value.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("SkipWhile")>]
        val skipWhile: predicate:('T -> bool) -> source:seq<'T> -> seq<'T>

        /// <summary>Yields a sequence ordered by keys.</summary>
        /// 
        /// <remarks>This function returns a sequence that digests the whole initial sequence as soon as 
        /// that sequence is iterated. As a result this function should not be used with 
        /// large or infinite sequences. The function makes no assumption on the ordering of the original 
        /// sequence.
        ///
        /// This is a stable sort, that is the original order of equal elements is preserved.</remarks>
        ///
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <remarks>This function consumes the whole input sequence before yielding the first element of the result sequence.</remarks>
        [<CompiledName("Sort")>]
        val sort : source:seq<'T> -> seq<'T> when 'T : comparison

        /// <summary>Yields a sequence ordered using the given comparison function.</summary>
        /// <remarks>This function returns a sequence that digests the whole initial sequence as soon as
        /// that sequence is iterated. As a result this function should not be used with
        /// large or infinite sequences. The function makes no assumption on the ordering of the original
        /// sequence.
        ///
        /// This is a stable sort, that is the original order of equal elements is preserved.</remarks>
        /// <param name="comparer">The function to compare the collection elements.</param>
        /// <param name="list">The input sequence.</param>
        /// <returns>The result sequence.</returns>
        /// <remarks>This function consumes the whole input sequence before yielding the first element of the result sequence.</remarks>
        [<CompiledName("SortWith")>]
        val sortWith : comparer:('T -> 'T -> int) -> source:seq<'T> -> seq<'T>

        /// <summary>Applies a key-generating function to each element of a sequence and yield a sequence ordered
        /// by keys.  The keys are compared using generic comparison as implemented by <c>Operators.compare</c>.</summary> 
        /// 
        /// <remarks>This function returns a sequence that digests the whole initial sequence as soon as 
        /// that sequence is iterated. As a result this function should not be used with 
        /// large or infinite sequences. The function makes no assumption on the ordering of the original 
        /// sequence.
        ///
        /// This is a stable sort, that is the original order of equal elements is preserved.</remarks>
        ///
        /// <param name="projection">A function to transform items of the input sequence into comparable keys.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("SortBy")>]
        val sortBy : projection:('T -> 'Key) -> source:seq<'T> -> seq<'T> when 'Key : comparison 

        /// <summary>Yields a sequence ordered descending by keys.</summary>
        /// 
        /// <remarks>This function returns a sequence that digests the whole initial sequence as soon as 
        /// that sequence is iterated. As a result this function should not be used with 
        /// large or infinite sequences. The function makes no assumption on the ordering of the original 
        /// sequence.
        ///
        /// This is a stable sort, that is the original order of equal elements is preserved.</remarks>
        ///
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("SortDescending")>]
        val inline sortDescending : source:seq<'T> -> seq<'T> when 'T : comparison

        /// <summary>Applies a key-generating function to each element of a sequence and yield a sequence ordered
        /// descending by keys.  The keys are compared using generic comparison as implemented by <c>Operators.compare</c>.</summary> 
        /// 
        /// <remarks>This function returns a sequence that digests the whole initial sequence as soon as 
        /// that sequence is iterated. As a result this function should not be used with 
        /// large or infinite sequences. The function makes no assumption on the ordering of the original 
        /// sequence.
        ///
        /// This is a stable sort, that is the original order of equal elements is preserved.</remarks>
        ///
        /// <param name="projection">A function to transform items of the input sequence into comparable keys.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("SortByDescending")>]
        val inline sortByDescending : projection:('T -> 'Key) -> source:seq<'T> -> seq<'T> when 'Key : comparison

        /// <summary>Returns the sum of the elements in the sequence.</summary>
        ///
        /// <remarks>The elements are summed using the <c>+</c> operator and <c>Zero</c> property associated with the generated type.</remarks>
        ///
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The computed sum.</returns>
        [<CompiledName("Sum")>]
        val inline sum   : source:seq<(^T)> -> ^T 
                                      when ^T : (static member ( + ) : ^T * ^T -> ^T) 
                                      and  ^T : (static member Zero : ^T)

        /// <summary>Returns the sum of the results generated by applying the function to each element of the sequence.</summary>
        /// <remarks>The generated elements are summed using the <c>+</c> operator and <c>Zero</c> property associated with the generated type.</remarks>
        ///
        /// <param name="projection">A function to transform items from the input sequence into the type that will be summed.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The computed sum.</returns>
        [<CompiledName("SumBy")>]
        val inline sumBy   : projection:('T -> ^U) -> source:seq<'T>  -> ^U 
                                      when ^U : (static member ( + ) : ^U * ^U -> ^U) 
                                      and  ^U : (static member Zero : ^U)

        /// <summary>Returns a sequence that skips 1 element of the underlying sequence and then yields the
        /// remaining elements of the sequence.</summary>
        ///
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the input sequence is empty.</exception>
        [<CompiledName("Tail")>]
        val tail: source:seq<'T> -> seq<'T>

        /// <summary>Returns the first N elements of the sequence.</summary>
        /// <remarks>Throws <c>InvalidOperationException</c>
        /// if the count exceeds the number of elements in the sequence. <c>Seq.truncate</c>
        /// returns as many items as the sequence contains instead of throwing an exception.</remarks>
        ///
        /// <param name="count">The number of items to take.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <exception cref="System.ArgumentException">Thrown when the input sequence is empty.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when count exceeds the number of elements
        /// in the sequence.</exception>
        [<CompiledName("Take")>]
        val take: count:int -> source:seq<'T> -> seq<'T>

        /// <summary>Returns a sequence that, when iterated, yields elements of the underlying sequence while the 
        /// given predicate returns True, and then returns no further elements.</summary>
        ///
        /// <param name="predicate">A function that evaluates to false when no more items should be returned.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("TakeWhile")>]
        val takeWhile: predicate:('T -> bool) -> source:seq<'T> -> seq<'T>

        /// <summary>Builds an array from the given collection.</summary>
        ///
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result array.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("ToArray")>]
        val toArray: source:seq<'T> -> 'T[]
        
        /// <summary>Builds an SeqEnumerable from the given collection.</summary>
        ///
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result SeqEnumerable.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("ToComposer")>]
        val toComposer   : source:seq<'T> -> Composer.Internal.ISeq<'T>

        /// <summary>Builds a list from the given collection.</summary>
        ///
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result list.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("ToList")>]
        val toList: source:seq<'T> -> 'T list

        /// <summary>Returns the first element for which the given function returns True.
        /// Return None if no such element exists.</summary>
        ///
        /// <param name="predicate">A function that evaluates to a Boolean when given an item in the sequence.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The found element or None.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("TryFind")>]
        val tryFind: predicate:('T -> bool) -> source:seq<'T> -> 'T option

        /// <summary>Returns the last element for which the given function returns True.
        /// Return None if no such element exists.</summary>
        /// <remarks>This function digests the whole initial sequence as soon as it is called. As a
        /// result this function should not be used with large or infinite sequences.</remarks>
        /// <param name="predicate">A function that evaluates to a Boolean when given an item in the sequence.</param>
        /// <param name="source">The input sequence.</param>
        /// <returns>The found element or None.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <remarks>This function consumes the whole input sequence before returning the result.</remarks>
        [<CompiledName("TryFindBack")>]
        val tryFindBack: predicate:('T -> bool) -> source:seq<'T> -> 'T option

        /// <summary>Returns the index of the first element in the sequence 
        /// that satisfies the given predicate. Return <c>None</c> if no such element exists.</summary>
        ///
        /// <param name="predicate">A function that evaluates to a Boolean when given an item in the sequence.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The found index or None.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("TryFindIndex")>]
        val tryFindIndex : predicate:('T -> bool) -> source:seq<'T> -> int option

        /// <summary>Tries to find the nth element in the sequence.
        /// Returns <c>None</c> if index is negative or the input sequence does not contain enough elements.</summary>
        /// <param name="index">The index of element to retrieve.</param>
        /// <param name="source">The input sequence.</param>
        /// <returns>The nth element of the sequence or <c>None</c>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("TryItem")>]
        val tryItem: index:int -> source:seq<'T> -> 'T option

        /// <summary>Returns the index of the last element in the sequence
        /// that satisfies the given predicate. Return <c>None</c> if no such element exists.</summary>
        /// <remarks>This function digests the whole initial sequence as soon as it is called. As a
        /// result this function should not be used with large or infinite sequences.</remarks>
        /// <param name="predicate">A function that evaluates to a Boolean when given an item in the sequence.</param>
        /// <param name="source">The input sequence.</param>
        /// <returns>The found index or <c>None</c>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <remarks>This function consumes the whole input sequence before returning the result.</remarks>
        [<CompiledName("TryFindIndexBack")>]
        val tryFindIndexBack : predicate:('T -> bool) -> source:seq<'T> -> int option

        /// <summary>Applies the given function to successive elements, returning the first
        /// result where the function returns "Some(x)".</summary>
        ///
        /// <param name="chooser">A function that transforms items from the input sequence into options.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The chosen element or <c>None</c>.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("TryPick")>]
        val tryPick: chooser:('T -> 'U option) -> source:seq<'T> -> 'U option

        /// <summary>Returns a sequence that when enumerated returns at most N elements.</summary>
        ///
        /// <param name="count">The maximum number of items to enumerate.</param>
        /// <param name="source">The input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        [<CompiledName("Truncate")>]
        val truncate: count:int -> source:seq<'T> -> seq<'T>

        /// <summary>Returns a sequence that contains the elements generated by the given computation.
        /// The given initial <c>state</c> argument is passed to the element generator.
        /// For each IEnumerator elements in the stream are generated on-demand by applying the element
        /// generator, until a None value is returned by the element generator. Each call to the element
        /// generator returns a new residual <c>state</c>.</summary>
        ///
        /// <remarks>The stream will be recomputed each time an IEnumerator is requested and iterated for the Seq.
        ///
        /// The returned sequence may be passed between threads safely. However, 
        /// individual IEnumerator values generated from the returned sequence should not be accessed concurrently.</remarks>
        ///
        /// <param name="generator">A function that takes in the current state and returns an option tuple of the next
        /// element of the sequence and the next state value.</param>
        /// <param name="state">The initial state value.</param>
        ///
        /// <returns>The result sequence.</returns>
        [<CompiledName("Unfold")>]
        val unfold   : generator:('State -> ('T * 'State) option) -> state:'State -> seq<'T>

        /// <summary>Returns a sequence that yields sliding windows containing elements drawn from the input
        /// sequence. Each window is returned as a fresh array.</summary>
        /// <param name="windowSize">The number of elements in each window.</param>
        /// <param name="source">The input sequence.</param>
        /// <returns>The result sequence.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the input sequence is null.</exception>
        /// <exception cref="System.ArgumentException">Thrown when windowSize is not positive.</exception>
        [<CompiledName("Windowed")>]
        val windowed: windowSize:int -> source:seq<'T> -> seq<'T[]>

        /// <summary>Combines the two sequences into a list of pairs. The two sequences need not have equal lengths:
        /// when one sequence is exhausted any remaining elements in the other
        /// sequence are ignored.</summary>
        ///
        /// <param name="source1">The first input sequence.</param>
        /// <param name="source2">The second input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when either of the input sequences is null.</exception>
        [<CompiledName("Zip")>]
        val zip: source1:seq<'T1> -> source2:seq<'T2> -> seq<'T1 * 'T2>

        /// <summary>Combines the three sequences into a list of triples. The sequences need not have equal lengths:
        /// when one sequence is exhausted any remaining elements in the other
        /// sequences are ignored.</summary>
        ///
        /// <param name="source1">The first input sequence.</param>
        /// <param name="source2">The second input sequence.</param>
        /// <param name="source3">The third input sequence.</param>
        ///
        /// <returns>The result sequence.</returns>
        ///
        /// <exception cref="System.ArgumentNullException">Thrown when any of the input sequences is null.</exception>
        [<CompiledName("Zip3")>]
        val zip3: source1:seq<'T1> -> source2:seq<'T2> -> source3:seq<'T3> -> seq<'T1 * 'T2 * 'T3>

namespace Microsoft.FSharp.Core.CompilerServices

    open System
    open System.Collections
    open System.Collections.Generic
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

