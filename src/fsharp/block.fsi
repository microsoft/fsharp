[<AutoOpen>]
module internal Internal.Utilities.Library.Block

open System.Collections.Immutable

/// Type alias for System.Collections.Immutable.ImmutableArray<'T>
type block<'T> = ImmutableArray<'T>

/// Type alias for System.Collections.Immutable.ImmutableArray<'T>.Builder
type blockbuilder<'T> = ImmutableArray<'T>.Builder

[<RequireQualifiedAccess>]
module BlockBuilder =

    val create : size: int -> blockbuilder<'T>

[<RequireQualifiedAccess>]
module Block =

    [<GeneralizableValue>]
    val empty<'T> : block<'T>

    val init : n: int -> f: (int -> 'T) -> block<'T>

    val iter : f: ('T -> unit) -> block<'T> -> unit

    val iteri : f: (int -> 'T -> unit) -> block<'T> -> unit

    val iter2 : f: ('T1 -> 'T2 -> unit) -> block<'T1> -> block<'T2> -> unit

    val iteri2 : f: (int -> 'T1 -> 'T2 -> unit) -> block<'T1> -> block<'T2> -> unit

    val map : mapper: ('T1 -> 'T2) -> block<'T1> -> block<'T2>

    val mapi : mapper: (int -> 'T1 -> 'T2) -> block<'T1> -> block<'T2>

    val concat : block<block<'T>> -> block<'T>

    val forall : predicate: ('T -> bool) -> block<'T> -> bool

    val forall2 : predicate: ('T1 -> 'T2 -> bool) -> block<'T1> -> block<'T2> -> bool

    val tryFind : predicate: ('T -> bool) -> block<'T> -> 'T option

    val tryFindIndex : predicate: ('T -> bool) -> block<'T> -> int option

    val tryPick : chooser: ('T1 -> 'T2 option) -> block<'T1> -> 'T2 option

    val ofSeq : seq<'T> -> block<'T>

    val append : block<'T> -> block<'T> -> block<'T>

    val createOne : 'T -> block<'T>

    val filter : predicate: ('T -> bool) -> block<'T> -> block<'T>

    val exists : predicate: ('T -> bool) -> block<'T> -> bool

    val choose : chooser: ('T -> 'U option) -> block<'T> -> block<'U>

    val isEmpty : block<'T> -> bool

    val fold : folder: ('State -> 'T -> 'State) -> 'State -> block<'T> -> 'State