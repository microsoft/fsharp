// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.FSharp.Collections
    #nowarn "52" // The value has been copied to ensure the original is not mutated by this operation

    open System
    open System.Diagnostics
    open System.Collections
    open System.Collections.Generic
    open Microsoft.FSharp.Core
    open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicOperators
    open Microsoft.FSharp.Core.Operators
    open Microsoft.FSharp.Control
    open Microsoft.FSharp.Collections

    module internal IEnumerator =

      let noReset() = raise (new System.NotSupportedException(SR.GetString(SR.resetNotSupported)))
      let notStarted() = raise (new System.InvalidOperationException(SR.GetString(SR.enumerationNotStarted)))
      let alreadyFinished() = raise (new System.InvalidOperationException(SR.GetString(SR.enumerationAlreadyFinished)))
      let check started = if not started then notStarted()
      let dispose (r : System.IDisposable) = r.Dispose()

      let cast (e : IEnumerator) : IEnumerator<'T> =
          { new IEnumerator<'T> with
                member _.Current = unbox<'T> e.Current
            interface IEnumerator with
                member _.Current = unbox<'T> e.Current :> obj
                member _.MoveNext() = e.MoveNext()
                member _.Reset() = noReset()
            interface System.IDisposable with
                member _.Dispose() =
                    match e with
                    | :? System.IDisposable as e -> e.Dispose()
                    | _ -> ()   }

      /// A concrete implementation of an enumerator that returns no values
      [<Sealed>]
      type EmptyEnumerator<'T>() =
          let mutable started = false
          interface IEnumerator<'T> with
                member _.Current =
                  check started
                  (alreadyFinished() : 'T)

          interface System.Collections.IEnumerator with
              member _.Current =
                  check started
                  (alreadyFinished() : obj)
              member _.MoveNext() =
                  if not started then started <- true
                  false
              member _.Reset() = noReset()
          interface System.IDisposable with
                member _.Dispose() = ()
                
      let Empty<'T> () = (new EmptyEnumerator<'T>() :> IEnumerator<'T>)

      [<NoEquality; NoComparison>]
      type EmptyEnumerable<'T> =
            | EmptyEnumerable
            interface IEnumerable<'T> with
                member _.GetEnumerator() = Empty<'T>()
            interface IEnumerable with
                member _.GetEnumerator() = (Empty<'T>() :> IEnumerator)

      let readAndClear r =
          lock r (fun () -> match !r with None -> None | Some _ as res -> r := None; res)

      let generateWhileSome openf compute closef : IEnumerator<'U> =
          let mutable started = false
          let mutable curr = None
          let state = ref (Some(openf()))
          let getCurr() =
              check started
              match curr with None -> alreadyFinished() | Some x -> x
          let start() = if not started then (started <- true)

          let dispose() = readAndClear state |> Option.iter closef
          let finish() = try dispose() finally curr <- None
          {  new IEnumerator<'U> with
                 member _.Current = getCurr()
             interface IEnumerator with
                 member _.Current = box (getCurr())
                 member _.MoveNext() =
                     start()
                     match !state with
                     | None -> false (* we started, then reached the end, then got another MoveNext *)
                     | Some s ->
                         match (try compute s with e -> finish(); reraise()) with
                         | None -> finish(); false
                         | Some _ as x -> curr <- x; true

                 member _.Reset() = noReset()
             interface System.IDisposable with
                 member _.Dispose() = dispose() }

      [<Sealed>]
      type Singleton<'T>(v:'T) =
          let mutable started = false
          interface IEnumerator<'T> with
                member _.Current = v
          interface IEnumerator with
              member _.Current = box v
              member _.MoveNext() = if started then false else (started <- true; true)
              member _.Reset() = noReset()
          interface System.IDisposable with
              member _.Dispose() = ()

      let Singleton x = (new Singleton<'T>(x) :> IEnumerator<'T>)

      let EnumerateThenFinally f (e : IEnumerator<'T>) =
          { new IEnumerator<'T> with
                member _.Current = e.Current
            interface IEnumerator with
                member _.Current = (e :> IEnumerator).Current
                member _.MoveNext() = e.MoveNext()
                member _.Reset() = noReset()
            interface System.IDisposable with
                member _.Dispose() =
                    try
                        e.Dispose()
                    finally
                        f()
          }

      let inline checkNonNull argName arg =
          if isNull arg then
              nullArg argName

      let mkSeq f =
            { new IEnumerable<'U> with
                member _.GetEnumerator() = f()
              interface IEnumerable with
                member _.GetEnumerator() = (f() :> IEnumerator) }

namespace Microsoft.FSharp.Core.CompilerServices

    open System
    open System.Diagnostics
    open Microsoft.FSharp.Core
    open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicOperators
    open Microsoft.FSharp.Core.Operators
    open Microsoft.FSharp.Control
    open Microsoft.FSharp.Collections
    open Microsoft.FSharp.Collections.IEnumerator
    open Microsoft.FSharp.Primitives.Basics
    open System.Collections
    open System.Collections.Generic
    open System.Runtime.CompilerServices

    module RuntimeHelpers =

        [<Struct; NoComparison; NoEquality>]
        type internal StructBox<'T when 'T:equality>(value:'T) =
            member x.Value = value
            static member Comparer =
                let gcomparer = HashIdentity.Structural<'T>
                { new IEqualityComparer<StructBox<'T>> with
                       member _.GetHashCode(v) = gcomparer.GetHashCode(v.Value)
                       member _.Equals(v1,v2) = gcomparer.Equals(v1.Value,v2.Value) }

        let Generate openf compute closef =
            mkSeq (fun () -> IEnumerator.generateWhileSome openf compute closef)

        let GenerateUsing (openf : unit -> ('U :> System.IDisposable)) compute =
            Generate openf compute (fun (s:'U) -> s.Dispose())

        let EnumerateFromFunctions create moveNext current =
            Generate
                create
                (fun x -> if moveNext x then Some(current x) else None)
                (fun x -> match box(x) with :? System.IDisposable as id -> id.Dispose() | _ -> ())

        // A family of enumerators that can have additional 'finally' actions added to the enumerator through
        // the use of mutation. This is used to 'push' the disposal action for a 'use' into the next enumerator.
        // For example,
        //    seq { use x = ...
        //          while ... }
        // results in the 'while' loop giving an adjustable enumerator. This is then adjusted by adding the disposal action
        // from the 'use' into the enumerator. This means that we avoid constructing a two-deep enumerator chain in this
        // common case.
        type IFinallyEnumerator =
            abstract AppendFinallyAction : (unit -> unit) -> unit

        /// A concrete implementation of IEnumerable that adds the given compensation to the "Dispose" chain of any
        /// enumerators returned by the enumerable.
        [<Sealed>]
        type FinallyEnumerable<'T>(compensation: unit -> unit, restf: unit -> seq<'T>) =
            interface IEnumerable<'T> with
                member _.GetEnumerator() =
                    try
                        let ie = restf().GetEnumerator()
                        match ie with
                        | :? IFinallyEnumerator as a ->
                            a.AppendFinallyAction(compensation)
                            ie
                        | _ ->
                            IEnumerator.EnumerateThenFinally compensation ie
                    with e ->
                        compensation()
                        reraise()
            interface IEnumerable with
                member x.GetEnumerator() = ((x :> IEnumerable<'T>).GetEnumerator() :> IEnumerator)

        /// An optimized object for concatenating a sequence of enumerables
        [<Sealed>]
        type ConcatEnumerator<'T,'U when 'U :> seq<'T>>(sources: seq<'U>) =
            let mutable outerEnum = sources.GetEnumerator()
            let mutable currInnerEnum = IEnumerator.Empty()

            let mutable started = false
            let mutable finished = false
            let mutable compensations = []

            [<DefaultValue(false)>] // false = unchecked
            val mutable private currElement : 'T

            member _.Finish() =
                finished <- true
                try
                    match currInnerEnum with
                    | null -> ()
                    | _ ->
                        try
                            currInnerEnum.Dispose()
                        finally
                            currInnerEnum <- null
                finally
                    try
                        match outerEnum with
                        | null -> ()
                        | _ ->
                            try
                                outerEnum.Dispose()
                            finally
                                outerEnum <- null
                    finally
                        let rec iter comps =
                            match comps with
                            |   [] -> ()
                            |   h :: t ->
                                    try h() finally iter t
                        try
                            compensations |> List.rev |> iter
                        finally
                            compensations <- []

            member x.GetCurrent() =
                IEnumerator.check started
                if finished then IEnumerator.alreadyFinished() else x.currElement

            interface IFinallyEnumerator with
                member _.AppendFinallyAction(f) =
                    compensations <- f :: compensations

            interface IEnumerator<'T> with
                member x.Current = x.GetCurrent()

            interface IEnumerator with
                member x.Current = box (x.GetCurrent())

                member x.MoveNext() =
                   if not started then started <- true
                   if finished then false
                   else
                      let rec takeInner () =
                        // check the inner list
                        if currInnerEnum.MoveNext() then
                            x.currElement <- currInnerEnum.Current
                            true
                        else
                            // check the outer list
                            let rec takeOuter() =
                                if outerEnum.MoveNext() then
                                    let ie = outerEnum.Current
                                    // Optimization to detect the statically-allocated empty IEnumerables
                                    match box ie with
                                    | :? EmptyEnumerable<'T> ->
                                         // This one is empty, just skip, don't call GetEnumerator, try again
                                         takeOuter()
                                    | _ ->
                                         // OK, this one may not be empty.
                                         // Don't forget to dispose of the enumerator for the inner list now we're done with it
                                         currInnerEnum.Dispose()
                                         currInnerEnum <- ie.GetEnumerator()
                                         takeInner ()
                                else
                                    // We're done
                                    x.Finish()
                                    false
                            takeOuter()
                      takeInner ()

                member _.Reset() = IEnumerator.noReset()

            interface System.IDisposable with
                member x.Dispose() =
                    if not finished then
                        x.Finish()

        let EnumerateUsing (resource : 'T :> System.IDisposable) (source: 'T -> #seq<'U>) =
            (FinallyEnumerable((fun () -> match box resource with null -> () | _ -> resource.Dispose()),
                               (fun () -> source resource :> seq<_>)) :> seq<_>)

        let mkConcatSeq (sources: seq<'U :> seq<'T>>) =
            mkSeq (fun () -> new ConcatEnumerator<_,_>(sources) :> IEnumerator<'T>)

        let EnumerateWhile (guard: unit -> bool) (source: seq<'T>) : seq<'T> =
            let mutable started = false
            let mutable curr = None
            let getCurr() =
                IEnumerator.check started
                match curr with None -> IEnumerator.alreadyFinished() | Some x -> x
            let start() = if not started then (started <- true)

            let finish() = (curr <- None)
            mkConcatSeq
               (mkSeq (fun () ->
                    { new IEnumerator<_> with
                          member x.Current = getCurr()
                       interface IEnumerator with
                          member x.Current = box (getCurr())
                          member x.MoveNext() =
                               start()
                               let keepGoing = (try guard() with e -> finish (); reraise ()) in
                               if keepGoing then
                                   curr <- Some(source); true
                               else
                                   finish(); false
                          member x.Reset() = IEnumerator.noReset()
                       interface System.IDisposable with
                          member x.Dispose() = () }))

        let EnumerateThenFinally (source: seq<'T>) (compensation: unit -> unit)  =
            (FinallyEnumerable(compensation, (fun () -> source)) :> seq<_>)

        let CreateEvent (addHandler : 'Delegate -> unit) (removeHandler : 'Delegate -> unit) (createHandler : (obj -> 'Args -> unit) -> 'Delegate ) :IEvent<'Delegate,'Args> =
            { new obj() with
                  member x.ToString() = "<published event>"
              interface IEvent<'Delegate,'Args> with
                 member x.AddHandler(h) = addHandler h
                 member x.RemoveHandler(h) = removeHandler h
              interface System.IObservable<'Args> with
                 member x.Subscribe(r:IObserver<'Args>) =
                     let h = createHandler (fun _ args -> r.OnNext(args))
                     addHandler h
                     { new System.IDisposable with
                          member x.Dispose() = removeHandler h } }

        let inline SetFreshConsTail cons tail = cons.( :: ).1 <- tail

        [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
        let inline FreshConsNoTail head = head :: (# "ldnull" : 'T list #)

    [<AbstractClass>]
    type GeneratedSequenceBase<'T>() =
        let mutable redirectTo : GeneratedSequenceBase<'T> = Unchecked.defaultof<_>
        let mutable redirect : bool = false

        abstract GetFreshEnumerator : unit -> IEnumerator<'T>
        abstract GenerateNext : result:byref<IEnumerable<'T>> -> int // 0 = Stop, 1 = Yield, 2 = Goto
        abstract Close: unit -> unit
        abstract CheckClose: bool
        abstract LastGenerated : 'T

        //[<System.Diagnostics.DebuggerNonUserCode; System.Diagnostics.DebuggerStepThroughAttribute>]
        member x.MoveNextImpl() =
             let active =
                 if redirect then redirectTo
                 else x
             let mutable target = null
             match active.GenerateNext(&target) with
             | 1 ->
                 true
             | 2 ->
                 match target.GetEnumerator() with
                 | :? GeneratedSequenceBase<'T> as g when not active.CheckClose ->
                     redirectTo <- g
                 | e ->
                     redirectTo <-
                           { new GeneratedSequenceBase<'T>() with
                                 member x.GetFreshEnumerator() = e
                                 member x.GenerateNext(_) = if e.MoveNext() then 1 else 0
                                 member x.Close() = try e.Dispose() finally active.Close()
                                 member x.CheckClose = true
                                 member x.LastGenerated = e.Current }
                 redirect <- true
                 x.MoveNextImpl()
             | _ (* 0 *)  ->
                 false

        interface IEnumerable<'T> with
            member x.GetEnumerator() = x.GetFreshEnumerator()
        interface IEnumerable with
            member x.GetEnumerator() = (x.GetFreshEnumerator() :> IEnumerator)
        interface IEnumerator<'T> with
            member x.Current = if redirect then redirectTo.LastGenerated else x.LastGenerated
        interface System.IDisposable with
            member x.Dispose() = if redirect then redirectTo.Close() else x.Close()
        interface IEnumerator with
            member x.Current = box (if redirect then redirectTo.LastGenerated else x.LastGenerated)

            //[<System.Diagnostics.DebuggerNonUserCode; System.Diagnostics.DebuggerStepThroughAttribute>]
            member x.MoveNext() = x.MoveNextImpl()

            member _.Reset() = raise <| new System.NotSupportedException()

    [<Struct; NoEquality; NoComparison>]
    type ListCollector<'T> =
        [<DefaultValue(false)>]
        val mutable Result : 'T list

        [<DefaultValue(false)>]
        val mutable LastCons : 'T list

        member this.Add (value: 'T) =
            match box this.Result with 
            | null -> 
                let ra = RuntimeHelpers.FreshConsNoTail value
                this.Result <- ra
                this.LastCons <- ra
            | _ -> 
                let ra = RuntimeHelpers.FreshConsNoTail value
                RuntimeHelpers.SetFreshConsTail this.LastCons ra
                this.LastCons <- ra

        member this.AddMany (values: seq<'T>) =
            // cook a faster iterator for lists and arrays
            match values with 
            | :? ('T[]) as valuesAsArray -> 
                for v in valuesAsArray do
                   this.Add v
            | :? ('T list) as valuesAsList -> 
                for v in valuesAsList do
                   this.Add v
            | _ ->
                for v in values do
                   this.Add v

        // In the particular case of closing with a final add of an F# list
        // we can simply stitch the list into the end of the resulting list
        member this.AddManyAndClose (values: seq<'T>) =
            match values with 
            | :? ('T list) as valuesAsList -> 
                let res =
                    match box this.Result with 
                    | null -> 
                        valuesAsList
                    | _ -> 
                        RuntimeHelpers.SetFreshConsTail this.LastCons valuesAsList
                        this.Result
                this.Result <- Unchecked.defaultof<_>
                this.LastCons <- Unchecked.defaultof<_>
                res
            | _ ->
                this.AddMany values
                this.Close()

        member this.Close() =
            match box this.Result with 
            | null -> []
            | _ ->
                RuntimeHelpers.SetFreshConsTail this.LastCons []
                let res = this.Result
                this.Result <- Unchecked.defaultof<_>
                this.LastCons <- Unchecked.defaultof<_>
                res

    // Optimized for 0, 1 and 2 sized arrays
    [<Struct; NoEquality; NoComparison>]
    type ArrayCollector<'T> =
        [<DefaultValue(false)>]
        val mutable ResizeArray: ResizeArray<'T>

        [<DefaultValue(false)>]
        val mutable First: 'T

        [<DefaultValue(false)>]
        val mutable Second: 'T

        [<DefaultValue(false)>]
        val mutable Count: int

        member this.Add (value: 'T) = 
            match this.Count with 
            | 0 -> 
                this.Count <- 1
                this.First <- value
            | 1 -> 
                this.Count <- 2
                this.Second <- value
            | 2 ->
                let ra = ResizeArray<'T>()
                ra.Add(this.First)
                ra.Add(this.Second)
                ra.Add(value)
                this.Count <- 3
                this.ResizeArray <- ra
            | _ -> 
                this.ResizeArray.Add(value)

        member this.AddMany (values: seq<'T>) =
            if this.Count > 2 then
                this.ResizeArray.AddRange(values)
            else
                // cook a faster iterator for lists and arrays
                match values with 
                | :? ('T[]) as valuesAsArray -> 
                    for v in valuesAsArray do
                       this.Add v
                | :? ('T list) as valuesAsList -> 
                    for v in valuesAsList do
                       this.Add v
                | _ ->
                    for v in values do
                       this.Add v

        member this.AddManyAndClose (values: seq<'T>) =
            this.AddMany(values)
            this.Close()

        member this.Close() =
            match this.Count with 
            | 0 -> Array.Empty<'T>()
            | 1 -> 
                let res = [| this.First |]
                this.Count <- 0
                this.First <- Unchecked.defaultof<_>
                res
            | 2 -> 
                let res = [| this.First; this.Second |]
                this.Count <- 0
                this.First <- Unchecked.defaultof<_>
                this.Second <- Unchecked.defaultof<_>
                res           
            | _ ->
                let res = this.ResizeArray.ToArray()
                this <- ArrayCollector<'T>()
                res
            
