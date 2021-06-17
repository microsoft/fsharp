// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal Internal.Utilities.Library.Extras

open System
open System.IO
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open System.Globalization
open System.Runtime.InteropServices
open Internal.Utilities
open Internal.Utilities.Collections
open Internal.Utilities.Library
open FSharp.Compiler.IO

let debug = false

let verbose = false

let mutable progress = false

// Intended to be a general hook to control diagnostic output when tracking down bugs
let mutable tracking = false

let condition s =
    try (System.Environment.GetEnvironmentVariable(s) <> null) with _ -> false

let GetEnvInteger e dflt = match System.Environment.GetEnvironmentVariable(e) with null -> dflt | t -> try int t with _ -> dflt

let dispose (x:System.IDisposable) = match x with null -> () | x -> x.Dispose()

//-------------------------------------------------------------------------
// Library: bits
//------------------------------------------------------------------------

module Bits =
    let b0 n =  (n          &&& 0xFF)
    let b1 n =  ((n >>> 8)  &&& 0xFF)
    let b2 n =  ((n >>> 16) &&& 0xFF)
    let b3 n =  ((n >>> 24) &&& 0xFF)

    let rec pown32 n = if n = 0 then 0  else (pown32 (n-1) ||| (1  <<<  (n-1)))
    let rec pown64 n = if n = 0 then 0L else (pown64 (n-1) ||| (1L <<< (n-1)))
    let mask32 m n = (pown32 n) <<< m
    let mask64 m n = (pown64 n) <<< m

//-------------------------------------------------------------------------
// Library: Orders
//------------------------------------------------------------------------

module Bool =
    let order = LanguagePrimitives.FastGenericComparer<bool>

module Int32 =
    let order = LanguagePrimitives.FastGenericComparer<int>

module Int64 =
    let order = LanguagePrimitives.FastGenericComparer<int64>

module Pair =
    let order (compare1: IComparer<'T1>, compare2: IComparer<'T2>) =
        { new IComparer<'T1 * 'T2> with
             member _.Compare((a1, a2), (aa1, aa2)) =
                  let res1 = compare1.Compare (a1, aa1)
                  if res1 <> 0 then res1 else compare2.Compare (a2, aa2) }


type NameSet =  Zset<string>

module NameSet =
    let ofList l : NameSet = List.foldBack Zset.add l (Zset.empty String.order)

module NameMap =
    let domain m = Map.foldBack (fun x _ acc -> Zset.add x acc) m (Zset.empty String.order)
    let domainL m = Zset.elements (domain m)

// Library: Pre\Post checks
//-------------------------------------------------------------------------
module Check =

    /// Throw <cref>System.InvalidOperationException</cref> if argument is <c>None</c>.
    /// If there is a value (e.g. <c>Some(value)</c>) then value is returned.
    let NotNone argName (arg:'T option) : 'T =
        match arg with
        | None -> raise (new System.InvalidOperationException(argName))
        | Some x -> x

    /// Throw <cref>System.ArgumentNullException</cref> if argument is <c>null</c>.
    let ArgumentNotNull arg argName =
        match box(arg) with
        | null -> raise (new System.ArgumentNullException(argName))
        | _ -> ()

    /// Throw <cref>System.ArgumentNullException</cref> if array argument is <c>null</c>.
    /// Throw <cref>System.ArgumentOutOfRangeException</cref> is array argument is empty.
    let ArrayArgumentNotNullOrEmpty (arr:'T[]) argName =
        ArgumentNotNull arr argName
        if (0 = arr.Length) then
            raise (new System.ArgumentOutOfRangeException(argName))

    /// Throw <cref>System.ArgumentNullException</cref> if string argument is <c>null</c>.
    /// Throw <cref>System.ArgumentOutOfRangeException</cref> is string argument is empty.
    let StringArgumentNotNullOrEmpty (s:string) argName =
        ArgumentNotNull s argName
        if s.Length = 0 then
            raise (new System.ArgumentNullException(argName))

//-------------------------------------------------------------------------
// Library
//------------------------------------------------------------------------

type IntMap<'T> = Zmap<int, 'T>
module IntMap =
    let empty () = Zmap.empty Int32.order

    let add k v (t:IntMap<'T>) = Zmap.add k v t
    let find k (t:IntMap<'T>) = Zmap.find k t
    let tryFind k (t:IntMap<'T>) = Zmap.tryFind k t
    let remove k (t:IntMap<'T>) = Zmap.remove k t
    let mem k (t:IntMap<'T>) = Zmap.mem k t
    let iter f (t:IntMap<'T>) = Zmap.iter f t
    let map f (t:IntMap<'T>) = Zmap.map f t
    let fold f (t:IntMap<'T>) z = Zmap.fold f t z


//-------------------------------------------------------------------------
// Library: generalized association lists
//------------------------------------------------------------------------

module ListAssoc =

    /// Treat a list of key-value pairs as a lookup collection.
    /// This function looks up a value based on a match from the supplied
    /// predicate function.
    let rec find f x l =
      match l with
      | [] -> notFound()
      | (x2, y) :: t -> if f x x2 then y else find f x t

    /// Treat a list of key-value pairs as a lookup collection.
    /// This function looks up a value based on a match from the supplied
    /// predicate function and returns None if value does not exist.
    let rec tryFind (f:'key->'key->bool) (x:'key) (l:('key*'value) list) : 'value option =
        match l with
        | [] -> None
        | (x2, y) :: t -> if f x x2 then Some y else tryFind f x t

//-------------------------------------------------------------------------
// Library: lists as generalized sets
//------------------------------------------------------------------------

module ListSet =
    let inline contains f x l = List.exists (f x) l

    /// NOTE: O(n)!
    let insert f x l = if contains f x l then l else x :: l

    let unionFavourRight f l1 l2 =
        match l1, l2 with
        | _, [] -> l1
        | [], _ -> l2
        | _ -> List.foldBack (insert f) l1 l2 (* nb. foldBack to preserve natural orders *)

    /// NOTE: O(n)!
    let rec private findIndexAux eq x l n =
        match l with
        | [] -> notFound()
        | (h :: t) -> if eq h x then n else findIndexAux eq x t (n+1)

    /// NOTE: O(n)!
    let findIndex eq x l = findIndexAux eq x l 0

    let rec remove f x l =
        match l with
        | (h :: t) -> if f x h then t else h :: remove f x t
        | [] -> []

    /// NOTE: quadratic!
    let rec subtract f l1 l2 =
      match l2 with
      | (h :: t) -> subtract f (remove (fun y2 y1 -> f y1 y2) h l1) t
      | [] -> l1

    let isSubsetOf f l1 l2 = List.forall (fun x1 -> contains f x1 l2) l1

    /// nb. preserve orders here: f must be applied to elements of l1 then elements of l2
    let isSupersetOf f l1 l2 = List.forall (fun x2 -> contains (fun y2 y1 -> f y1 y2) x2 l1) l2

    let equals f l1 l2 = isSubsetOf f l1 l2 && isSupersetOf f l1 l2

    let unionFavourLeft f l1 l2 =
        match l1, l2 with
        | _, [] -> l1
        | [], _ -> l2
        | _ -> l1 @ (subtract f l2 l1)

    /// NOTE: not tail recursive!
    let rec intersect f l1 l2 =
        match l2 with
        | (h :: t) -> if contains f h l1 then h :: intersect f l1 t else intersect f l1 t
        | [] -> []

    /// Note: if duplicates appear, keep the ones toward the _front_ of the list
    let setify f l = List.fold (fun acc x -> insert f x acc) [] l |> List.rev

    let hasDuplicates f l =
        match l with
        | [] -> false
        | [_] -> false
        | [x; y] -> f x y
        | x :: rest ->
            let rec loop acc l =
                match l with
                | [] -> false
                | x :: rest ->
                    if contains f x acc then
                        true
                    else
                        loop (x :: acc) rest

            loop [x] rest

//-------------------------------------------------------------------------
// Library: pairs
//------------------------------------------------------------------------

let mapFoldFst f s (x, y) = let x2, s = f s x in (x2, y), s

let mapFoldSnd f s (x, y) = let y2, s = f s y in (x, y2), s

let pair a b = a, b

let p13 (x, _y, _z) = x

let p23 (_x, y, _z) = y

let p33 (_x, _y, z) = z

let map1Of2 f (a1, a2) = (f a1, a2)

let map2Of2 f (a1, a2) = (a1, f a2)

let map1Of3 f (a1, a2, a3) = (f a1, a2, a3)

let map2Of3 f (a1, a2, a3) = (a1, f a2, a3)

let map3Of3 f (a1, a2, a3) = (a1, a2, f a3)

let map3Of4 f (a1, a2, a3, a4) = (a1, a2, f a3, a4)

let map4Of4 f (a1, a2, a3, a4) = (a1, a2, a3, f a4)

let map5Of5 f (a1, a2, a3, a4, a5) = (a1, a2, a3, a4, f a5)

let map6Of6 f (a1, a2, a3, a4, a5, a6) = (a1, a2, a3, a4, a5, f a6)

let foldPair (f1, f2) acc (a1, a2) = f2 (f1 acc a1) a2

let fold1Of2 f1 acc (a1, _a2) = f1 acc a1

let foldTriple (f1, f2, f3) acc (a1, a2, a3) = f3 (f2 (f1 acc a1) a2) a3

let foldQuadruple (f1, f2, f3, f4) acc (a1, a2, a3, a4) = f4 (f3 (f2 (f1 acc a1) a2) a3) a4

let mapPair (f1, f2) (a1, a2) = (f1 a1, f2 a2)

let mapTriple (f1, f2, f3) (a1, a2, a3) = (f1 a1, f2 a2, f3 a3)

let mapQuadruple (f1, f2, f3, f4) (a1, a2, a3, a4) = (f1 a1, f2 a2, f3 a3, f4 a4)

let fmap2Of2 f z (a1, a2) = let z, a2 = f z a2 in z, (a1, a2)

//---------------------------------------------------------------------------
// Zmap rebinds
//-------------------------------------------------------------------------

module Zmap =
    let force k mp = match Zmap.tryFind k mp with Some x -> x | None -> failwith "Zmap.force: lookup failed"

    let mapKey key f mp =
      match f (Zmap.tryFind key mp) with
      | Some fx -> Zmap.add key fx mp
      | None -> Zmap.remove key mp

//---------------------------------------------------------------------------
// Zset
//-------------------------------------------------------------------------

module Zset =
    let ofList order xs = Zset.addList xs (Zset.empty order)

    // CLEANUP NOTE: move to Zset?
    let rec fixpoint f (s as s0) =
        let s = f s
        if Zset.equal s s0 then s0 (* fixed *)
        else fixpoint f s (* iterate *)

let equalOn f x y = (f x) = (f y)

/// Buffer printing utility
let bufs f =
    let buf = System.Text.StringBuilder 100
    f buf
    buf.ToString()

/// Writing to output stream via a string buffer.
let writeViaBuffer (os: TextWriter) f x =
    let buf = System.Text.StringBuilder 100
    f buf x
    os.Write(buf.ToString())

//---------------------------------------------------------------------------
// Imperative Graphs
//---------------------------------------------------------------------------

type GraphNode<'Data, 'Id> = { nodeId: 'Id; nodeData: 'Data; mutable nodeNeighbours: GraphNode<'Data, 'Id> list }

type Graph<'Data, 'Id when 'Id : comparison and 'Id : equality>
         (nodeIdentity: ('Data -> 'Id),
          nodes: 'Data list,
          edges: ('Data * 'Data) list) =

    let edges = edges |> List.map (fun (v1, v2) -> nodeIdentity v1, nodeIdentity v2)
    let nodes = nodes |> List.map (fun d -> nodeIdentity d, { nodeId = nodeIdentity d; nodeData=d; nodeNeighbours=[] })
    let tab = Map.ofList nodes
    let nodes = List.map snd nodes
    do for node in nodes do
        node.nodeNeighbours <- edges |> List.filter (fun (x, _y) -> x = node.nodeId) |> List.map (fun (_, nodeId) -> tab.[nodeId])

    member g.GetNodeData nodeId = tab.[nodeId].nodeData

    member g.IterateCycles f =
        let rec trace path node =
            if List.exists (nodeIdentity >> (=) node.nodeId) path then f (List.rev path)
            else List.iter (trace (node.nodeData :: path)) node.nodeNeighbours
        List.iter (fun node -> trace [] node) nodes

//---------------------------------------------------------------------------
// In some cases we play games where we use 'null' as a more efficient representation
// in F#. The functions below are used to give initial values to mutable fields.
// This is an unsafe trick, as it relies on the fact that the type of values
// being placed into the slot never utilizes "null" as a representation. To be used with
// with care.
//----------------------------------------------------------------------------

// The following DEBUG code does not currently compile.
//#if DEBUG
//type 'T NonNullSlot = 'T option
//let nullableSlotEmpty() = None
//let nullableSlotFull(x) = Some x
//#else
type NonNullSlot<'T> = 'T
let nullableSlotEmpty() = Unchecked.defaultof<'T>
let nullableSlotFull x = x
//#endif

//---------------------------------------------------------------------------
// Caches, mainly for free variables
//---------------------------------------------------------------------------

type cache<'T> = { mutable cacheVal: 'T NonNullSlot }
let newCache() = { cacheVal = nullableSlotEmpty() }

let inline cached cache resF =
    match box cache.cacheVal with
    | null ->
        let res = resF()
        cache.cacheVal <- nullableSlotFull res
        res
    | _ ->
        cache.cacheVal

let inline cacheOptByref (cache: byref<'T option>) f =
    match cache with
    | Some v -> v
    | None ->
       let res = f()
       cache <- Some res
       res

// REVIEW: this is only used because we want to mutate a record field,
// and because you cannot take a byref<_> of such a thing directly,
// we cannot use 'cacheOptByref'. If that is changed, this can be removed.
let inline cacheOptRef cache f =
    match !cache with
    | Some v -> v
    | None ->
       let res = f()
       cache := Some res
       res

let inline tryGetCacheValue cache =
    match box cache.cacheVal with
    | null -> ValueNone
    | _ -> ValueSome cache.cacheVal

#if DUMPER
type Dumper(x:obj) =
     [<DebuggerBrowsable(DebuggerBrowsableState.Collapsed)>]
     member self.Dump = sprintf "%A" x
#endif

//---------------------------------------------------------------------------
// AsyncUtil
//---------------------------------------------------------------------------

module internal AsyncUtil =
    open System
    open System.Threading
    open Microsoft.FSharp.Control

    /// Represents the reified result of an asynchronous computation.
    [<NoEquality; NoComparison>]
    type AsyncResult<'T> =
        | AsyncOk of 'T
        | AsyncException of exn
        | AsyncCanceled of OperationCanceledException

        static member Commit(res:AsyncResult<'T>) =
            Async.FromContinuations (fun (cont, eCont, cCont) ->
                    match res with
                    | AsyncOk v -> cont v
                    | AsyncException exn -> eCont exn
                    | AsyncCanceled exn -> cCont exn)

    /// When using .NET 4.0 you can replace this type by <see cref="Task{T}"/>
    [<Sealed>]
    type AsyncResultCell<'T>() =
        let mutable result = None
        // The continuation for the result, if any
        let mutable savedConts = []

        let syncRoot = new obj()


        // Record the result in the AsyncResultCell.
        // Ignore subsequent sets of the result. This can happen, e.g. for a race between
        // a cancellation and a success.
        member x.RegisterResult (res:AsyncResult<'T>) =
            let grabbedConts =
                lock syncRoot (fun () ->
                    if result.IsSome then
                        []
                    else
                        result <- Some res
                        // Invoke continuations in FIFO order
                        // Continuations that Async.FromContinuations provide do QUWI/SyncContext.Post,
                        // so the order is not overly relevant but still.
                        List.rev savedConts)
            let postOrQueue (sc:SynchronizationContext, cont) =
                match sc with
                |   null -> ThreadPool.QueueUserWorkItem(fun _ -> cont res) |> ignore
                |   sc -> sc.Post((fun _ -> cont res), state=null)

            // Run continuations outside the lock
            match grabbedConts with
            |   [] -> ()
            |   [(sc, cont) as c] ->
                    if SynchronizationContext.Current = sc then
                        cont res
                    else
                        postOrQueue c
            |   _ ->
                    grabbedConts |> List.iter postOrQueue

        /// Get the reified result.
        member private x.AsyncPrimitiveResult =
            Async.FromContinuations(fun (cont, _, _) ->
                let grabbedResult =
                    lock syncRoot (fun () ->
                        match result with
                        | Some _ ->
                            result
                        | None ->
                            // Otherwise save the continuation and call it in RegisterResult
                            let sc = SynchronizationContext.Current
                            savedConts <- (sc, cont) :: savedConts
                            None)
                // Run the action outside the lock
                match grabbedResult with
                | None -> ()
                | Some res -> cont res)


        /// Get the result and Commit(...).
        member x.AsyncResult =
            async { let! res = x.AsyncPrimitiveResult
                    return! AsyncResult.Commit(res) }

//---------------------------------------------------------------------------
// EnableHeapTerminationOnCorruption()
//---------------------------------------------------------------------------

// USAGE: call UnmanagedProcessExecutionOptions.EnableHeapTerminationOnCorruption() from "main()".
// Note: This is not SDL required but recommended.
module UnmanagedProcessExecutionOptions =
    open System
    open System.Runtime.InteropServices

    [<DllImport("kernel32.dll")>]
    extern UIntPtr private GetProcessHeap()

    [<DllImport("kernel32.dll")>]
    extern bool private HeapSetInformation(
        UIntPtr _HeapHandle,
        UInt32 _HeapInformationClass,
        UIntPtr _HeapInformation,
        UIntPtr _HeapInformationLength)

    [<DllImport("kernel32.dll")>]
    extern UInt32 private GetLastError()

    // Translation of C# from http://swikb/v1/DisplayOnlineDoc.aspx?entryID=826 and copy in bug://5018
    [<System.Security.Permissions.SecurityPermission(System.Security.Permissions.SecurityAction.Assert, UnmanagedCode = true)>]
    let EnableHeapTerminationOnCorruption() =
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&  System.Environment.OSVersion.Version.Major >= 6 && // If OS is Vista or higher
            System.Environment.Version.Major < 3) then // and CLR not 3.0 or higher
            // "The flag HeapSetInformation sets is available in Windows XP SP3 and later.
            //  The data structure used for heap information is available on earlier versions of Windows.
            //  The call will either return TRUE (found and set the flag) or false (flag not found).
            //  Not a problem in native code, so the program will merrily continue running.
            //  In managed code, the call to HeapSetInformation is a p/invoke.
            //  If HeapSetInformation returns FALSE then an exception will be thrown.
            //  If we are not running an OS which supports this (XP SP3, Vista, Server 2008, and Win7)
            //  then the call should not be made." -- see bug://5018.
            // See also:
            //  http://blogs.msdn.com/michael_howard/archive/2008/02/18/faq-about-heapsetinformation-in-windows-vista-and-heap-based-buffer-overruns.aspx
            let HeapEnableTerminationOnCorruption = 1u : uint32
            if not (HeapSetInformation(GetProcessHeap(), HeapEnableTerminationOnCorruption, UIntPtr.Zero, UIntPtr.Zero)) then
                  raise (System.Security.SecurityException(
                            "Unable to enable unmanaged process execution option TerminationOnCorruption. " +
                            "HeapSetInformation() returned FALSE; LastError = 0x" +
                            GetLastError().ToString("X").PadLeft(8, '0') + "."))

[<RequireQualifiedAccess>]
module StackGuard =

    open System.Runtime.CompilerServices

    [<Literal>]
    let private MaxUncheckedRecursionDepth = 20

    let EnsureSufficientExecutionStack recursionDepth =
        if recursionDepth > MaxUncheckedRecursionDepth then
            RuntimeHelpers.EnsureSufficientExecutionStack ()

[<RequireQualifiedAccess>]
type MaybeLazy<'T> =
    | Strict of 'T
    | Lazy of Lazy<'T>

    member this.Value: 'T =
        match this with
        | Strict x -> x
        | Lazy x -> x.Value

    member this.Force() : 'T =
        match this with
        | Strict x -> x
        | Lazy x -> x.Force()

let inline vsnd ((_, y): struct('T * 'T)) = y

/// Track a set of resources to cleanup
type DisposablesTracker() =

    let items = Stack<IDisposable>()

    /// Register some items to dispose
    member _.Register i = items.Push i

    interface IDisposable with

        member _.Dispose() =
            let l = List.ofSeq items
            items.Clear()
            for i in l do
                try i.Dispose() with _ -> ()

/// Specialized parallel functions for an array.
/// Different from Array.Parallel as it will try to minimize the max degree of parallelism.
/// Will flatten aggregate exceptions that contain one exception.
[<RequireQualifiedAccess>]
module ArrayParallel =

    let inline iteri f (arr: 'T []) =
        let parallelOptions = ParallelOptions(MaxDegreeOfParallelism = max (min Environment.ProcessorCount arr.Length) 1)
        try
            Parallel.For(0, arr.Length, parallelOptions, fun i ->
                f i arr.[i]
            ) |> ignore
        with
        | :? AggregateException as ex when ex.InnerExceptions.Count = 1 ->
            raise(ex.InnerExceptions.[0])

    let inline iter f (arr: 'T []) =
        arr |> iteri (fun _ item -> f item)

    let inline mapi f (arr: 'T []) =
        let mapped = Array.zeroCreate arr.Length
        arr |> iteri (fun i item -> mapped.[i] <- f i item)
        mapped

    let inline map f (arr: 'T []) =
        arr |> mapi (fun _ item -> f item)
