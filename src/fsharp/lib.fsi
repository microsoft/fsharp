// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal Internal.Utilities.Library.Extras

open System.IO
open System.Text
open System.Globalization
open System.Collections.Generic
open Internal.Utilities.Collections

val debug: bool

val verbose: bool

val mutable progress: bool

val mutable tracking: bool

val condition: s:string -> bool

val GetEnvInteger: e:string -> dflt:int -> int

val dispose: x:System.IDisposable -> unit

module Bits =
    /// Get the least significant byte of a 32-bit integer
    val b0: n:int -> int

    /// Get the 2nd least significant byte of a 32-bit integer
    val b1: n:int -> int

    /// Get the 3rd least significant byte of a 32-bit integer
    val b2: n:int -> int

    /// Get the most significant byte of a 32-bit integer
    val b3: n:int -> int

    val pown32: n:int -> int

    val pown64: n:int -> int64

    val mask32: m:int32 -> n:int -> int

    val mask64: m:int32 -> n:int -> int64

module Bool =
    val order: IComparer<bool>

module Int32 =
    val order: IComparer<int>

module Int64 =
    val order: IComparer<int64>

module Pair =
    val order :
      compare1:IComparer<'T1> *
      compare2:IComparer<'T2> ->
        IComparer<'T1 * 'T2>

type NameSet = Zset<string>

module NameSet =
    val ofList: l:string list -> NameSet

module NameMap =
    val domain: m:Map<string,'a> -> Zset<string>

    val domainL: m:Map<string,'a> -> string list

module Check =
    /// Throw <cref>System.InvalidOperationException</cref> if argument is <c>None</c>.
    /// If there is a value (e.g. <c>Some(value)</c>) then value is returned.
    val NotNone: argName:string -> arg:'T option -> 'T

    /// Throw <cref>System.ArgumentNullException</cref> if argument is <c>null</c>.
    val ArgumentNotNull: arg:'a -> argName:string -> unit

    /// Throw <cref>System.ArgumentNullException</cref> if array argument is <c>null</c>.
    /// Throw <cref>System.ArgumentOutOfRangeException</cref> is array argument is empty.
    val ArrayArgumentNotNullOrEmpty: arr:'T [] -> argName:string -> unit

    /// Throw <cref>System.ArgumentNullException</cref> if string argument is <c>null</c>.
    /// Throw <cref>System.ArgumentOutOfRangeException</cref> is string argument is empty.
    val StringArgumentNotNullOrEmpty: s:string -> argName:string -> unit

type IntMap<'T> = Zmap<int,'T>

module IntMap =
    val empty: unit -> Zmap<int,'a>

    val add : k:int -> v:'T -> t:IntMap<'T> -> Zmap<int,'T>

    val find: k:int -> t:IntMap<'T> -> 'T

    val tryFind: k:int -> t:IntMap<'T> -> 'T option

    val remove: k:int -> t:IntMap<'T> -> Zmap<int,'T>

    val mem: k:int -> t:IntMap<'T> -> bool

    val iter: f:(int -> 'T -> unit) -> t:IntMap<'T> -> unit

    val map: f:('T -> 'a) -> t:IntMap<'T> -> Zmap<int,'a>

    val fold: f:(int -> 'T -> 'a -> 'a) -> t:IntMap<'T> -> z:'a -> 'a

module ListAssoc =

    /// Treat a list of key-value pairs as a lookup collection.
    /// This function looks up a value based on a match from the supplied predicate function.
    val find: f:('a -> 'b -> bool) -> x:'a -> l:('b * 'c) list -> 'c

    /// Treat a list of key-value pairs as a lookup collection.
    /// This function looks up a value based on a match from the supplied
    /// predicate function and returns None if value does not exist.
    val tryFind : f:('key -> 'key -> bool) -> x:'key -> l:('key * 'value) list -> 'value option

module ListSet =
    val inline contains: f:('a -> 'b -> bool) -> x:'a -> l:'b list -> bool

    /// NOTE: O(n)!
    val insert: f:('a -> 'a -> bool) -> x:'a -> l:'a list -> 'a list

    val unionFavourRight : f:('a -> 'a -> bool) -> l1:'a list -> l2:'a list -> 'a list

    /// NOTE: O(n)!
    val findIndex: eq:('a -> 'b -> bool) -> x:'b -> l:'a list -> int

    val remove: f:('a -> 'b -> bool) -> x:'a -> l:'b list -> 'b list

    /// NOTE: quadratic!
    val subtract: f:('a -> 'b -> bool) -> l1:'a list -> l2:'b list -> 'a list

    val isSubsetOf: f:('a -> 'b -> bool) -> l1:'a list -> l2:'b list -> bool

    val isSupersetOf : f:('a -> 'b -> bool) -> l1:'a list -> l2:'b list -> bool

    val equals: f:('a -> 'b -> bool) -> l1:'a list -> l2:'b list -> bool

    val unionFavourLeft : f:('a -> 'a -> bool) -> l1:'a list -> l2:'a list -> 'a list

    /// NOTE: not tail recursive!
    val intersect : f:('a -> 'b -> bool) -> l1:'b list -> l2:'a list -> 'a list

    /// Note: if duplicates appear, keep the ones toward the _front_ of the list
    val setify: f:('a -> 'a -> bool) -> l:'a list -> 'a list

    val hasDuplicates: f:('a -> 'a -> bool) -> l:'a list -> bool

val mapFoldFst : f:('a -> 'b -> 'c * 'd) -> s:'a -> x:'b * y:'e -> ('c * 'e) * 'd

val mapFoldSnd : f:('a -> 'b -> 'c * 'd) -> s:'a -> x:'e * y:'b -> ('e * 'c) * 'd

val pair: a:'a -> b:'b -> 'a * 'b

val p13: x:'a * _y:'b * _z:'c -> 'a

val p23: _x:'a * y:'b * _z:'c -> 'b

val p33: _x:'a * _y:'b * z:'c -> 'c

val map1Of2: f:('a -> 'b) -> a1:'a * a2:'c -> 'b * 'c

val map2Of2: f:('a -> 'b) -> a1:'c * a2:'a -> 'c * 'b

val map1Of3: f:('a -> 'b) -> a1:'a * a2:'c * a3:'d -> 'b * 'c * 'd

val map2Of3: f:('a -> 'b) -> a1:'c * a2:'a * a3:'d -> 'c * 'b * 'd

val map3Of3: f:('a -> 'b) -> a1:'c * a2:'d * a3:'a -> 'c * 'd * 'b

val map3Of4 : f:('a -> 'b) -> a1:'c * a2:'d * a3:'a * a4:'e -> 'c * 'd * 'b * 'e

val map4Of4 : f:('a -> 'b) -> a1:'c * a2:'d * a3:'e * a4:'a -> 'c * 'd * 'e * 'b

val map5Of5 : f:('a -> 'b) -> a1:'c * a2:'d * a3:'e * a4:'f * a5:'a -> 'c * 'd * 'e * 'f * 'b

val map6Of6 : f:('a -> 'b) -> a1:'c * a2:'d * a3:'e * a4:'f * a5:'g * a6:'a -> 'c * 'd * 'e * 'f * 'g * 'b

val foldPair : f1:('a -> 'b -> 'c) * f2:('c -> 'd -> 'e) -> acc:'a -> a1:'b * a2:'d -> 'e

val fold1Of2: f1:('a -> 'b -> 'c) -> acc:'a -> a1:'b * _a2:'d -> 'c

val foldTriple : f1:('a -> 'b -> 'c) * f2:('c -> 'd -> 'e) * f3:('e -> 'f -> 'g) -> acc:'a -> a1:'b * a2:'d * a3:'f -> 'g

val foldQuadruple : f1:('a -> 'b -> 'c) * f2:('c -> 'd -> 'e) * f3:('e -> 'f -> 'g) * f4:('g -> 'h -> 'i) -> acc:'a -> a1:'b * a2:'d * a3:'f * a4:'h -> 'i val mapPair: f1:('a -> 'b) * f2:('c -> 'd) -> a1:'a * a2:'c -> 'b * 'd

val mapTriple : f1:('a -> 'b) * f2:('c -> 'd) * f3:('e -> 'f) -> a1:'a * a2:'c * a3:'e -> 'b * 'd * 'f

val mapQuadruple : f1:('a -> 'b) * f2:('c -> 'd) * f3:('e -> 'f) * f4:('g -> 'h) -> a1:'a * a2:'c * a3:'e * a4:'g -> 'b * 'd * 'f * 'h

val fmap2Of2 : f:('a -> 'b -> 'c * 'd) -> z:'a -> a1:'e * a2:'b -> 'c * ('e * 'd)

module Zmap =
    val force: k:'a -> mp:Zmap<'a,'b> -> 'b
    val mapKey :key:'a -> f:('b option -> 'b option) -> mp:Zmap<'a,'b> -> Zmap<'a,'b>

module Zset =
    val ofList : order:IComparer<'a> -> xs:'a list -> Zset<'a>
    val fixpoint : f:(Zset<'a> -> Zset<'a>) -> Zset<'a> -> Zset<'a>

val equalOn: f:('a -> 'b) -> x:'a -> y:'a -> bool when 'b: equality

/// Buffer printing utility
val bufs: f:(StringBuilder -> unit) -> string

/// Writing to output stream via a string buffer.
val writeViaBuffer : os:TextWriter -> f:(StringBuilder -> 'a -> unit) -> x:'a -> unit

type Graph<'Data,'Id when 'Id: comparison> =

      new: nodeIdentity:('Data -> 'Id) * nodes:'Data list *
            edges:('Data * 'Data) list -> Graph<'Data,'Id>
      member GetNodeData: nodeId:'Id -> 'Data
      member IterateCycles: f:('Data list -> unit) -> unit

/// In some cases we play games where we use 'null' as a more efficient representation
/// in F#. The functions below are used to give initial values to mutable fields.
/// This is an unsafe trick, as it relies on the fact that the type of values
/// being placed into the slot never utilizes "null" as a representation. To be used with
/// with care.
type NonNullSlot<'T> = 'T

val nullableSlotEmpty: unit -> 'T

val nullableSlotFull: x:'a -> 'a

/// Caches, mainly for free variables
type cache<'T> = { mutable cacheVal: NonNullSlot<'T> }

val newCache: unit -> cache<'a>

val inline cached: cache:cache<'a> -> resF:(unit -> 'a) -> 'a

val inline cacheOptByref: cache:byref<'T option> -> f:(unit -> 'T) -> 'T

val inline cacheOptRef: cache:'a option ref -> f:(unit -> 'a) -> 'a

val inline tryGetCacheValue: cache:cache<'a> -> NonNullSlot<'a> voption

module AsyncUtil =

    /// Represents the reified result of an asynchronous computation.
    [<NoEqualityAttribute; NoComparisonAttribute>]
    type AsyncResult<'T> =
        | AsyncOk of 'T
        | AsyncException of exn
        | AsyncCanceled of System.OperationCanceledException
        static member Commit: res:AsyncResult<'T> -> Async<'T>

    /// When using .NET 4.0 you can replace this type by <see cref="Task{T}"/>
    [<SealedAttribute>]
    type AsyncResultCell<'T> =

        new: unit -> AsyncResultCell<'T>
        member RegisterResult: res:AsyncResult<'T> -> unit
        member AsyncResult: Async<'T>

module UnmanagedProcessExecutionOptions =
    val EnableHeapTerminationOnCorruption: unit -> unit

module StackGuard =
    val EnsureSufficientExecutionStack: recursionDepth:int -> unit

[<RequireQualifiedAccessAttribute>]
type MaybeLazy<'T> =
    | Strict of 'T
    | Lazy of System.Lazy<'T>
    member Force: unit -> 'T
    member Value: 'T

val inline vsnd: struct ('T * 'T) -> 'T

/// Track a set of resources to cleanup
type DisposablesTracker =

    new: unit -> DisposablesTracker

    /// Register some items to dispose
    member Register: i:System.IDisposable -> unit

    interface System.IDisposable

/// Specialized parallel functions for an array.
/// Different from Array.Parallel as it will try to minimize the max degree of parallelism.
/// Will flatten aggregate exceptions that contain one exception.
[<RequireQualifiedAccess>]
module ArrayParallel =

    val inline map : ('T -> 'U) -> 'T [] -> 'U []

    val inline mapi : (int -> 'T -> 'U) -> 'T [] -> 'U []
