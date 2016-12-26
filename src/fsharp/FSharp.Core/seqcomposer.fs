// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.FSharp.Collections

    open System
    open System.Diagnostics
    open System.Collections
    open System.Collections.Generic
    open System.Reflection
    open Microsoft.FSharp.Core
    open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicOperators
    open Microsoft.FSharp.Core.Operators
    open Microsoft.FSharp.Core.CompilerServices
    open Microsoft.FSharp.Control
    open Microsoft.FSharp.Collections
    open Microsoft.FSharp.Primitives.Basics

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Composer =
        open IEnumerator

        module Core =
            [<Struct; NoComparison; NoEquality>]
            type NoValue = struct end

            [<Struct; NoComparison; NoEquality>]
            type Values<'a,'b> =
                val mutable _1 : 'a
                val mutable _2 : 'b
                new (a:'a, b: 'b) = { _1 = a;  _2 = b }

            [<Struct; NoComparison; NoEquality>]
            type Values<'a,'b,'c> =
                val mutable _1 : 'a
                val mutable _2 : 'b
                val mutable _3 : 'c
                new (a:'a, b:'b, c:'c) = { _1 = a; _2 = b; _3 = c }

            type PipeIdx = int

            type IOutOfBand =
                abstract StopFurtherProcessing : PipeIdx -> unit

            [<AbstractClass>]
            type Activity() =
                abstract ChainComplete : stopTailCall:byref<unit> * PipeIdx -> unit
                abstract ChainDispose  : stopTailCall:byref<unit> -> unit

            [<AbstractClass>]
            type Activity<'T,'U> () =
                inherit Activity()
                abstract ProcessNext : input:'T -> bool

            [<AbstractClass>]
            type Transform<'T,'U,'State> =
                inherit Activity<'T,'U>
                
                new (next:Activity, initState:'State) = {
                    inherit Activity<'T,'U> ()
                    State = initState
                    Next = next
                }

                val mutable State : 'State
                val Next : Activity
                
                override this.ChainComplete (stopTailCall, terminatingIdx) =
                    this.Next.ChainComplete (&stopTailCall, terminatingIdx)
                override this.ChainDispose stopTailCall =
                    this.Next.ChainDispose (&stopTailCall)

            [<AbstractClass>]
            type TransformWithPostProcessing<'T,'U,'State>(next:Activity, initState:'State) =
                inherit Transform<'T,'U,'State>(next, initState)

                abstract OnComplete : PipeIdx -> unit
                abstract OnDispose  : unit -> unit

                override this.ChainComplete (stopTailCall, terminatingIdx) =
                    this.OnComplete terminatingIdx
                    this.Next.ChainComplete (&stopTailCall, terminatingIdx)
                override this.ChainDispose stopTailCall  =
                    try     this.OnDispose ()
                    finally this.Next.ChainDispose (&stopTailCall)

            [<AbstractClass>]
            type Folder<'T,'Result,'State> =
                inherit Activity<'T,'T>

                val mutable Result : 'Result
                val mutable State : 'State

                val mutable HaltedIdx : int
                member this.StopFurtherProcessing pipeIdx = this.HaltedIdx <- pipeIdx
                interface IOutOfBand with
                    member this.StopFurtherProcessing pipeIdx = this.StopFurtherProcessing pipeIdx

                new (initalResult,initState) = {
                    inherit Activity<'T,'T>()
                    State = initState
                    HaltedIdx = 0
                    Result = initalResult
                }

                override this.ChainComplete (_,_) = ()
                override this.ChainDispose _ = ()

            [<AbstractClass>]
            type FolderWithPostProcessing<'T,'Result,'State>(initResult,initState) =
                inherit Folder<'T,'Result,'State>(initResult,initState)

                abstract OnComplete : PipeIdx -> unit
                abstract OnDispose : unit -> unit

                override this.ChainComplete (stopTailCall, terminatingIdx) =
                    this.OnComplete terminatingIdx
                override this.ChainDispose _ =
                    this.OnDispose ()

            [<AbstractClass>]
            type TransformFactory<'T,'U> () =
                abstract Compose<'V> : IOutOfBand -> PipeIdx -> Activity<'U,'V> -> Activity<'T,'V>

            type ISeq<'T> =
                inherit IEnumerable<'T>
                abstract member PushTransform<'U> : TransformFactory<'T,'U> -> ISeq<'U>
                abstract member Fold<'Result,'State> : f:(PipeIdx->Folder<'T,'Result,'State>) -> 'Result

        open Core

        module internal TailCall =
            // used for performance reasons; these are not recursive calls, so should be safe
            // ** it should be noted that potential changes to the f# compiler may render this function
            // ineffictive **
            let inline avoid boolean = match boolean with true -> true | false -> false

        module internal Upcast =
            // The f# compiler outputs unnecessary unbox.any calls in upcasts. If this functionality
            // is fixed with the compiler then these functions can be removed.
            let inline seq (t:#ISeq<'T>) : ISeq<'T> = (# "" t : ISeq<'T> #)
            let inline enumerable (t:#IEnumerable<'T>) : IEnumerable<'T> = (# "" t : IEnumerable<'T> #)
            let inline enumerator (t:#IEnumerator<'T>) : IEnumerator<'T> = (# "" t : IEnumerator<'T> #)
            let inline enumeratorNonGeneric (t:#IEnumerator) : IEnumerator = (# "" t : IEnumerator #)
            let inline outOfBand (t:#IOutOfBand) : IOutOfBand = (# "" t : IOutOfBand #)

        let createFold (factory:TransformFactory<_,_>) (folder:Folder<_,_,_>) pipeIdx  =
            factory.Compose (Upcast.outOfBand folder) pipeIdx folder

        type ComposedFactory<'T,'U,'V> private (first:TransformFactory<'T,'U>, second:TransformFactory<'U,'V>) =
            inherit TransformFactory<'T,'V>()

            override this.Compose<'W> (outOfBand:IOutOfBand) (pipeIdx:PipeIdx) (next:Activity<'V,'W>) : Activity<'T,'W> =
                first.Compose outOfBand (pipeIdx-1) (second.Compose outOfBand pipeIdx next)

            static member Combine (first:TransformFactory<'T,'U>) (second:TransformFactory<'U,'V>) : TransformFactory<'T,'V> =
                upcast ComposedFactory(first, second)

        and IdentityFactory<'T> () =
            inherit TransformFactory<'T,'T> ()
            static let singleton : TransformFactory<'T,'T> = upcast (IdentityFactory<'T>())
            override __.Compose<'V> (_outOfBand:IOutOfBand) (_pipeIdx:PipeIdx) (next:Activity<'T,'V>) : Activity<'T,'V> = next
            static member Instance = singleton

        and ISkipping =
            // Seq.init(Infinite)? lazily uses Current. The only Composer component that can do that is Skip
            // and it can only do it at the start of a sequence
            abstract Skipping : unit -> bool

        type SeqProcessNextStates =
        | InProcess  = 0
        | NotStarted = 1
        | Finished   = 2

        type Result<'T>() =
            inherit Folder<'T,'T,NoValue>(Unchecked.defaultof<'T>,Unchecked.defaultof<NoValue>)

            member val SeqState = SeqProcessNextStates.NotStarted with get, set

            override this.ProcessNext (input:'T) : bool =
                this.Result <- input
                true

        module Fold =
            type IIterate<'T> =
                abstract Iterate<'U,'Result,'State> : outOfBand:Folder<'U,'Result,'State> -> consumer:Activity<'T,'U> -> unit

            [<Struct;NoComparison;NoEquality>]
            type enumerable<'T> (enumerable:IEnumerable<'T>) =
                interface IIterate<'T> with
                    member __.Iterate (outOfBand:Folder<'U,'Result,'State>) (consumer:Activity<'T,'U>) =
                        use enumerator = enumerable.GetEnumerator ()
                        let rec iterate () =
                            if enumerator.MoveNext () then  
                                consumer.ProcessNext enumerator.Current |> ignore
                                if outOfBand.HaltedIdx = 0 then
                                    iterate ()
                        iterate ()

            [<Struct;NoComparison;NoEquality>]
            type Array<'T> (array:array<'T>) =
                interface IIterate<'T> with
                    member __.Iterate (outOfBand:Folder<'U,'Result,'State>) (consumer:Activity<'T,'U>) =
                        let array = array
                        let rec iterate idx =
                            if idx < array.Length then  
                                consumer.ProcessNext array.[idx] |> ignore
                                if outOfBand.HaltedIdx = 0 then
                                    iterate (idx+1)
                        iterate 0

            [<Struct;NoComparison;NoEquality>]
            type List<'T> (alist:list<'T>) =
                interface IIterate<'T> with
                    member __.Iterate (outOfBand:Folder<'U,'Result,'State>) (consumer:Activity<'T,'U>) =
                        let rec iterate lst =
                            match lst with
                            | hd :: tl ->
                                consumer.ProcessNext hd |> ignore
                                if outOfBand.HaltedIdx = 0 then
                                    iterate tl
                            | _ -> ()
                        iterate alist

            [<Struct;NoComparison;NoEquality>]
            type unfold<'S,'T> (generator:'S->option<'T*'S>, state:'S) =
                interface IIterate<'T> with
                    member __.Iterate (outOfBand:Folder<'U,'Result,'State>) (consumer:Activity<'T,'U>) =
                        let generator = generator
                        let rec iterate current =
                            match generator current with
                            | Some (item, next) ->
                                consumer.ProcessNext item |> ignore
                                if outOfBand.HaltedIdx = 0 then
                                    iterate next
                            | _ -> ()
                        iterate state

            let makeIsSkipping (consumer:Activity<'T,'U>) =
                match box consumer with
                | :? ISkipping as skip -> skip.Skipping
                | _ -> fun () -> false

            [<Struct;NoComparison;NoEquality>]
            type init<'T> (f:int->'T, terminatingIdx:int) =
                interface IIterate<'T> with
                    member __.Iterate (outOfBand:Folder<'U,'Result,'State>) (consumer:Activity<'T,'U>) =
                        let terminatingIdx =terminatingIdx
                        let f = f

                        let isSkipping = makeIsSkipping consumer
                        let rec skip idx =
                            if idx >= terminatingIdx || outOfBand.HaltedIdx <> 0 then
                                terminatingIdx
                            elif isSkipping () then
                                skip (idx+1)
                            else
                                idx

                        let rec iterate idx =
                            if idx < terminatingIdx then
                                consumer.ProcessNext (f idx) |> ignore
                                if outOfBand.HaltedIdx = 0 then
                                    iterate (idx+1)

                        skip 0
                        |> iterate

            let execute (f:PipeIdx->Folder<'U,'Result,'State>) (current:TransformFactory<'T,'U>) pipeIdx (executeOn:#IIterate<'T>) =
                let mutable stopTailCall = ()
                let result = f (pipeIdx+1)
                let consumer = createFold current result pipeIdx
                try
                    executeOn.Iterate result consumer
                    consumer.ChainComplete (&stopTailCall, result.HaltedIdx)
                    result.Result
                finally
                    consumer.ChainDispose (&stopTailCall)

            let executeThin (f:PipeIdx->Folder<'T,'Result,'State>) (executeOn:#IIterate<'T>) =
                let mutable stopTailCall = ()
                let result = f 1
                try
                    executeOn.Iterate result result
                    result.ChainComplete (&stopTailCall, result.HaltedIdx)
                    result.Result
                finally
                    result.ChainDispose (&stopTailCall)

        module Enumerable =
            type Empty<'T>() =
                let current () = failwith "library implementation error: Current should never be called"
                interface IEnumerator<'T> with
                    member __.Current = current ()
                interface IEnumerator with
                    member __.Current = current ()
                    member __.MoveNext () = false
                    member __.Reset (): unit = noReset ()
                interface IDisposable with
                    member __.Dispose () = ()

            type EmptyEnumerators<'T>() =
                static let element : IEnumerator<'T> = upcast (new Empty<'T> ())
                static member Element = element

            [<AbstractClass>]
            type EnumeratorBase<'T>(result:Result<'T>, seqComponent:Activity) =
                interface IDisposable with
                    member __.Dispose() : unit =
                        let mutable stopTailCall = ()
                        seqComponent.ChainDispose (&stopTailCall)

                interface IEnumerator with
                    member this.Current : obj = box ((Upcast.enumerator this)).Current
                    member __.MoveNext () = failwith "library implementation error: derived class should implement (should be abstract)"
                    member __.Reset () : unit = noReset ()

                interface IEnumerator<'T> with
                    member __.Current =
                        if result.SeqState = SeqProcessNextStates.InProcess then result.Result
                        else
                            match result.SeqState with
                            | SeqProcessNextStates.NotStarted -> notStarted()
                            | SeqProcessNextStates.Finished -> alreadyFinished()
                            | _ -> failwith "library implementation error: all states should have been handled"

            and [<AbstractClass>] EnumerableBase<'T> () =
                let derivedClassShouldImplement () =
                    failwith "library implementation error: derived class should implement (should be abstract)"

                abstract member Append   : (ISeq<'T>) -> ISeq<'T>

                default this.Append source = Upcast.seq (AppendEnumerable [this; source])

                interface IEnumerable with
                    member this.GetEnumerator () : IEnumerator =
                        let genericEnumerable = Upcast.enumerable this
                        let genericEnumerator = genericEnumerable.GetEnumerator ()
                        Upcast.enumeratorNonGeneric genericEnumerator

                interface IEnumerable<'T> with
                    member this.GetEnumerator () : IEnumerator<'T> = derivedClassShouldImplement ()

                interface ISeq<'T> with
                    member __.PushTransform _ = derivedClassShouldImplement ()
                    member __.Fold _ = derivedClassShouldImplement ()

            and Enumerator<'T,'U>(source:IEnumerator<'T>, seqComponent:Activity<'T,'U>, result:Result<'U>) =
                inherit EnumeratorBase<'U>(result, seqComponent)

                let rec moveNext () =
                    if (result.HaltedIdx = 0) && source.MoveNext () then
                        if seqComponent.ProcessNext source.Current then
                            true
                        else
                            moveNext ()
                    else
                        result.SeqState <- SeqProcessNextStates.Finished
                        let mutable stopTailCall = ()
                        (seqComponent).ChainComplete (&stopTailCall, result.HaltedIdx)
                        false

                interface IEnumerator with
                    member __.MoveNext () =
                        result.SeqState <- SeqProcessNextStates.InProcess
                        moveNext ()

                interface IDisposable with
                    member __.Dispose() =
                        try
                            source.Dispose ()
                        finally
                            let mutable stopTailCall = ()
                            (seqComponent).ChainDispose (&stopTailCall)

            and Enumerable<'T,'U>(enumerable:IEnumerable<'T>, current:TransformFactory<'T,'U>, pipeIdx:PipeIdx) =
                inherit EnumerableBase<'U>()

                interface IEnumerable<'U> with
                    member this.GetEnumerator () : IEnumerator<'U> =
                        let result = Result<'U> ()
                        Upcast.enumerator (new Enumerator<'T,'U>(enumerable.GetEnumerator(), createFold current result pipeIdx, result))

                interface ISeq<'U> with
                    member __.PushTransform (next:TransformFactory<'U,'V>) : ISeq<'V> =
                        Upcast.seq (new Enumerable<'T,'V>(enumerable, ComposedFactory.Combine current next, pipeIdx+1))

                    member this.Fold<'Result,'State> (f:PipeIdx->Folder<'U,'Result,'State>) =
                        Fold.execute f current pipeIdx (Fold.enumerable enumerable)

            and EnumerableThin<'T>(enumerable:IEnumerable<'T>) =
                inherit EnumerableBase<'T>()

                interface IEnumerable<'T> with
                    member this.GetEnumerator () = enumerable.GetEnumerator ()

                interface ISeq<'T> with
                    member __.PushTransform (next:TransformFactory<'T,'U>) : ISeq<'U> =
                        Upcast.seq (new Enumerable<'T,'U>(enumerable, next, 1))

                    member this.Fold<'Result,'State> (f:PipeIdx->Folder<'T,'Result,'State>) =
                        Fold.executeThin f (Fold.enumerable enumerable)

            and ConcatEnumerator<'T, 'Collection when 'Collection :> seq<'T>> (sources:seq<'Collection>) =
                let mutable state = SeqProcessNextStates.NotStarted
                let main = sources.GetEnumerator ()

                let mutable active = EmptyEnumerators.Element

                let rec moveNext () =
                    if active.MoveNext () then
                        true
                    elif main.MoveNext () then
                        active.Dispose ()
                        active <- main.Current.GetEnumerator ()
                        moveNext ()
                    else
                        state <- SeqProcessNextStates.Finished
                        false

                interface IEnumerator<'T> with
                    member __.Current =
                        if state = SeqProcessNextStates.InProcess then active.Current
                        else
                            match state with
                            | SeqProcessNextStates.NotStarted -> notStarted()
                            | SeqProcessNextStates.Finished -> alreadyFinished()
                            | _ -> failwith "library implementation error: all states should have been handled"

                interface IEnumerator with
                    member this.Current = box ((Upcast.enumerator this)).Current
                    member __.MoveNext () =
                        state <- SeqProcessNextStates.InProcess
                        moveNext ()
                    member __.Reset () = noReset ()

                interface IDisposable with
                    member __.Dispose() =
                        main.Dispose ()
                        active.Dispose ()

            and AppendEnumerable<'T> (sources:list<ISeq<'T>>) =
                inherit EnumerableBase<'T>()

                interface IEnumerable<'T> with
                    member this.GetEnumerator () : IEnumerator<'T> =
                        Upcast.enumerator (new ConcatEnumerator<_,_> (sources |> List.rev))

                override this.Append source =
                    Upcast.seq (AppendEnumerable (source::sources))

                interface ISeq<'T> with
                    member this.PushTransform (next:TransformFactory<'T,'U>) : ISeq<'U> =
                        Upcast.seq (Enumerable<'T,'V>(this, next, 1))

                    member this.Fold<'Result,'State> (f:PipeIdx->Folder<'T,'Result,'State>) =
                        Fold.executeThin f (Fold.enumerable this)

            and ConcatEnumerable<'T, 'Collection when 'Collection :> seq<'T>> (sources:seq<'Collection>) =
                inherit EnumerableBase<'T>()

                interface IEnumerable<'T> with
                    member this.GetEnumerator () : IEnumerator<'T> =
                        Upcast.enumerator (new ConcatEnumerator<_,_> (sources))

                interface ISeq<'T> with
                    member this.PushTransform (next:TransformFactory<'T,'U>) : ISeq<'U> =
                        Upcast.seq (Enumerable<'T,'V>(this, next, 1))

                    member this.Fold<'Result,'State> (f:PipeIdx->Folder<'T,'Result,'State>) =
                        Fold.executeThin f (Fold.enumerable this)

            let create enumerable current =
                Upcast.seq (Enumerable(enumerable, current, 1))

        module EmptyEnumerable =
            type Enumerable<'T> () =
                inherit Enumerable.EnumerableBase<'T>()

                static let singleton = Enumerable<'T>() :> ISeq<'T>
                static member Instance = singleton

                interface IEnumerable<'T> with
                    member this.GetEnumerator () : IEnumerator<'T> = IEnumerator.Empty<'T>()

                override this.Append source =
                    Upcast.seq (Enumerable.EnumerableThin<'T> source)

                interface ISeq<'T> with
                    member this.PushTransform (next:TransformFactory<'T,'U>) : ISeq<'U> =
                        Upcast.seq (Enumerable.Enumerable<'T,'V>(this, next, 1))

                    member this.Fold<'Result,'State> (f:PipeIdx->Folder<'T,'Result,'State>) =
                        Fold.executeThin f (Fold.enumerable this)

        module Array =
            type Enumerator<'T,'U>(delayedArray:unit->array<'T>, seqComponent:Activity<'T,'U>, result:Result<'U>) =
                inherit Enumerable.EnumeratorBase<'U>(result, seqComponent)

                let mutable idx = 0
                let mutable array = Unchecked.defaultof<_>

                let mutable initMoveNext = Unchecked.defaultof<_>
                do
                    initMoveNext <-
                        fun () ->
                            result.SeqState <- SeqProcessNextStates.InProcess
                            array <- delayedArray ()
                            initMoveNext <- ignore

                let rec moveNext () =
                    if (result.HaltedIdx = 0) && idx < array.Length then
                        idx <- idx+1
                        if seqComponent.ProcessNext array.[idx-1] then
                            true
                        else
                            moveNext ()
                    else
                        result.SeqState <- SeqProcessNextStates.Finished
                        let mutable stopTailCall = ()
                        (seqComponent).ChainComplete (&stopTailCall, result.HaltedIdx)
                        false

                interface IEnumerator with
                    member __.MoveNext () =
                        initMoveNext ()
                        moveNext ()

            type Enumerable<'T,'U>(delayedArray:unit->array<'T>, current:TransformFactory<'T,'U>, pipeIdx:PipeIdx) =
                inherit Enumerable.EnumerableBase<'U>()

                interface IEnumerable<'U> with
                    member this.GetEnumerator () : IEnumerator<'U> =
                        let result = Result<'U> ()
                        Upcast.enumerator (new Enumerator<'T,'U>(delayedArray, createFold current result pipeIdx, result))

                interface ISeq<'U> with
                    member __.PushTransform (next:TransformFactory<'U,'V>) : ISeq<'V> =
                        Upcast.seq (new Enumerable<'T,'V>(delayedArray, ComposedFactory.Combine current next, 1))

                    member this.Fold<'Result,'State> (f:PipeIdx->Folder<'U,'Result,'State>) =
                        Fold.execute f current pipeIdx (Fold.Array (delayedArray ()))

            let createDelayed (delayedArray:unit->array<'T>) (current:TransformFactory<'T,'U>) =
                Upcast.seq (Enumerable(delayedArray, current, 1))

            let create (array:array<'T>) (current:TransformFactory<'T,'U>) =
                createDelayed (fun () -> array) current

            let createDelayedId (delayedArray:unit -> array<'T>) =
                createDelayed delayedArray IdentityFactory.Instance

            let createId (array:array<'T>) =
                create array IdentityFactory.Instance

        module List =
            type Enumerator<'T,'U>(alist:list<'T>, seqComponent:Activity<'T,'U>, result:Result<'U>) =
                inherit Enumerable.EnumeratorBase<'U>(result, seqComponent)

                let mutable list = alist

                let rec moveNext current =
                    match result.HaltedIdx, current with
                    | 0, head::tail ->
                        if seqComponent.ProcessNext head then
                            list <- tail
                            true
                        else
                            moveNext tail
                    | _ ->
                        result.SeqState <- SeqProcessNextStates.Finished
                        let mutable stopTailCall = ()
                        (seqComponent).ChainComplete (&stopTailCall, result.HaltedIdx)
                        false

                interface IEnumerator with
                    member __.MoveNext () =
                        result.SeqState <- SeqProcessNextStates.InProcess
                        moveNext list

            type Enumerable<'T,'U>(alist:list<'T>, current:TransformFactory<'T,'U>, pipeIdx:PipeIdx) =
                inherit Enumerable.EnumerableBase<'U>()

                interface IEnumerable<'U> with
                    member this.GetEnumerator () : IEnumerator<'U> =
                        let result = Result<'U> ()
                        Upcast.enumerator (new Enumerator<'T,'U>(alist, createFold current result pipeIdx, result))

                interface ISeq<'U> with
                    member __.PushTransform (next:TransformFactory<'U,'V>) : ISeq<'V> =
                        Upcast.seq (new Enumerable<'T,'V>(alist, ComposedFactory.Combine current next, pipeIdx+1))

                    member this.Fold<'Result,'State> (f:PipeIdx->Folder<'U,'Result,'State>) =
                        Fold.execute f current pipeIdx (Fold.List alist)

            let create alist current =
                Upcast.seq (Enumerable(alist, current, 1))

        module Unfold =
            type Enumerator<'T,'U,'State>(generator:'State->option<'T*'State>, state:'State, seqComponent:Activity<'T,'U>, result:Result<'U>) =
                inherit Enumerable.EnumeratorBase<'U>(result, seqComponent)

                let mutable current = state

                let rec moveNext () =
                    match result.HaltedIdx, generator current with
                    | 0, Some (item, nextState) ->
                        current <- nextState
                        if seqComponent.ProcessNext item then
                            true
                        else
                            moveNext ()
                    | _ -> false

                interface IEnumerator with
                    member __.MoveNext () =
                        result.SeqState <- SeqProcessNextStates.InProcess
                        moveNext ()

            type Enumerable<'T,'U,'GeneratorState>(generator:'GeneratorState->option<'T*'GeneratorState>, state:'GeneratorState, current:TransformFactory<'T,'U>, pipeIdx:PipeIdx) =
                inherit Enumerable.EnumerableBase<'U>()

                interface IEnumerable<'U> with
                    member this.GetEnumerator () : IEnumerator<'U> =
                        let result = Result<'U> ()
                        Upcast.enumerator (new Enumerator<'T,'U,'GeneratorState>(generator, state, createFold current result pipeIdx, result))

                interface ISeq<'U> with
                    member this.PushTransform (next:TransformFactory<'U,'V>) : ISeq<'V> =
                        Upcast.seq (new Enumerable<'T,'V,'GeneratorState>(generator, state, ComposedFactory.Combine current next, pipeIdx+1))

                    member this.Fold<'Result,'State> (f:PipeIdx->Folder<'U,'Result,'State>) =
                        Fold.execute f current pipeIdx (Fold.unfold (generator, state))

        module Init =
            // The original implementation of "init" delayed the calculation of Current, and so it was possible
            // to do MoveNext without it's value being calculated.
            // I can imagine only two scenerios where that is possibly sane, although a simple solution is readily
            // at hand in both cases. The first is that of an expensive generator function, where you skip the
            // first n elements. The simple solution would have just been to have a map ((+) n) as the first operation
            // instead. The second case would be counting elements, but that is only of use if you're not filtering
            // or mapping or doing anything else (as that would cause Current to be evaluated!) and
            // so you already know what the count is!! Anyway, someone thought it was a good idea, so
            // I have had to add an extra function that is used in Skip to determine if we are touching
            // Current or not.

            let getTerminatingIdx (count:Nullable<int>) =
                // we are offset by 1 to allow for values going up to System.Int32.MaxValue
                // System.Int32.MaxValue is an illegal value for the "infinite" sequence
                if count.HasValue then
                    count.Value - 1
                else
                    System.Int32.MaxValue

            type Enumerator<'T,'U>(count:Nullable<int>, f:int->'T, seqComponent:Activity<'T,'U>, result:Result<'U>) =
                inherit Enumerable.EnumeratorBase<'U>(result, seqComponent)

                let isSkipping =
                    Fold.makeIsSkipping seqComponent

                let terminatingIdx =
                    getTerminatingIdx count

                let mutable maybeSkipping = true
                let mutable idx = -1

                let rec moveNext () =
                    if (result.HaltedIdx = 0) && idx < terminatingIdx then
                        idx <- idx + 1

                        if maybeSkipping then
                            // Skip can only is only checked at the start of the sequence, so once
                            // triggered, we stay triggered.
                            maybeSkipping <- isSkipping ()

                        if maybeSkipping then
                            moveNext ()
                        elif seqComponent.ProcessNext (f idx) then
                            true
                        else
                            moveNext ()
                    elif (result.HaltedIdx = 0) && idx = System.Int32.MaxValue then
                        raise <| System.InvalidOperationException (SR.GetString(SR.enumerationPastIntMaxValue))
                    else
                        result.SeqState <- SeqProcessNextStates.Finished
                        let mutable stopTailCall = ()
                        (seqComponent).ChainComplete (&stopTailCall, result.HaltedIdx)
                        false

                interface IEnumerator with
                    member __.MoveNext () =
                        result.SeqState <- SeqProcessNextStates.InProcess
                        moveNext ()

            type Enumerable<'T,'U>(count:Nullable<int>, f:int->'T, current:TransformFactory<'T,'U>, pipeIdx:PipeIdx) =
                inherit Enumerable.EnumerableBase<'U>()

                interface IEnumerable<'U> with
                    member this.GetEnumerator () : IEnumerator<'U> =
                        let result = Result<'U> ()
                        Upcast.enumerator (new Enumerator<'T,'U>(count, f, createFold current result pipeIdx, result))

                interface ISeq<'U> with
                    member this.PushTransform (next:TransformFactory<'U,'V>) : ISeq<'V> =
                        Upcast.seq (new Enumerable<'T,'V>(count, f, ComposedFactory.Combine current next, pipeIdx+1))

                    member this.Fold<'Result,'State> (createResult:PipeIdx->Folder<'U,'Result,'State>) =
                        let terminatingIdx = getTerminatingIdx count
                        Fold.execute createResult current pipeIdx (Fold.init (f, terminatingIdx))

            let upto lastOption f =
                match lastOption with
                | Some b when b<0 -> failwith "library implementation error: upto can never be called with a negative value"
                | _ ->
                    let unstarted   = -1  // index value means unstarted (and no valid index)
                    let completed   = -2  // index value means completed (and no valid index)
                    let unreachable = -3  // index is unreachable from 0,1,2,3,...
                    let finalIndex  = match lastOption with
                                        | Some b -> b             // here b>=0, a valid end value.
                                        | None   -> unreachable   // run "forever", well as far as Int32.MaxValue since indexing with a bounded type.
                    // The Current value for a valid index is "f i".
                    // Lazy<_> values are used as caches, to store either the result or an exception if thrown.
                    // These "Lazy<_>" caches are created only on the first call to current and forced immediately.
                    // The lazy creation of the cache nodes means enumerations that skip many Current values are not delayed by GC.
                    // For example, the full enumeration of Seq.initInfinite in the tests.
                    // state
                    let index   = ref unstarted
                    // a Lazy node to cache the result/exception
                    let current = ref (Unchecked.defaultof<_>)
                    let setIndex i = index := i; current := (Unchecked.defaultof<_>) // cache node unprimed, initialised on demand.
                    let getCurrent() =
                        if !index = unstarted then notStarted()
                        if !index = completed then alreadyFinished()
                        match box !current with
                        | null -> current := Lazy<_>.Create(fun () -> f !index)
                        | _ ->  ()
                        // forced or re-forced immediately.
                        (!current).Force()
                    { new IEnumerator<'U> with
                            member x.Current = getCurrent()
                        interface IEnumerator with
                            member x.Current = box (getCurrent())
                            member x.MoveNext() =
                                if !index = completed then
                                    false
                                elif !index = unstarted then
                                    setIndex 0
                                    true
                                else (
                                    if !index = System.Int32.MaxValue then raise <| System.InvalidOperationException (SR.GetString(SR.enumerationPastIntMaxValue))
                                    if !index = finalIndex then
                                        false
                                    else
                                        setIndex (!index + 1)
                                        true
                                )
                            member self.Reset() = noReset()
                        interface System.IDisposable with
                            member x.Dispose() = () }

            type EnumerableDecider<'T>(count:Nullable<int>, f:int->'T, pipeIdx:PipeIdx) =
                inherit Enumerable.EnumerableBase<'T>()

                interface IEnumerable<'T> with
                    member this.GetEnumerator () : IEnumerator<'T> =
                        // we defer back to the original implementation as, as it's quite idiomatic in it's decision
                        // to calculate Current in a lazy fashion. I doubt anyone is really using this functionality
                        // in the way presented, but it's possible.
                        upto (if count.HasValue then Some (count.Value-1) else None) f

                interface ISeq<'T> with
                    member this.PushTransform (next:TransformFactory<'T,'U>) : ISeq<'U> =
                        Upcast.seq (Enumerable<'T,'V>(count, f, next, pipeIdx+1))

                    member this.Fold<'Result,'State> (f:PipeIdx->Folder<'T,'Result,'State>) =
                        Fold.executeThin f (Fold.enumerable (Upcast.enumerable this))

        [<CompiledName "OfSeq">]
        let ofSeq (source:seq<'T>) : ISeq<'T> =
            match source with
            | :? ISeq<'T> as s -> s
            | :? array<'T> as a -> Upcast.seq (Array.Enumerable((fun () -> a), IdentityFactory.Instance, 1))
            | :? list<'T> as a -> Upcast.seq (List.Enumerable(a, IdentityFactory.Instance, 1))
            | null -> nullArg "source"
            | _ -> Upcast.seq (Enumerable.EnumerableThin<'T> source)

        [<CompiledName "Average">]
        let inline average (source: ISeq< ^T>) : ^T
            when ^T:(static member Zero : ^T)
            and  ^T:(static member (+) : ^T * ^T -> ^T)
            and  ^T:(static member DivideByInt : ^T * int -> ^T) =
            source.Fold (fun _ ->
                upcast { new FolderWithPostProcessing< ^T, ^T, int> (LanguagePrimitives.GenericZero, 0) with
                    override this.ProcessNext value =
                        this.Result <- Checked.(+) this.Result value
                        this.State <- this.State + 1
                        Unchecked.defaultof<_> (* return value unused in Fold context *)

                    override this.OnComplete _ =
                        if this.State = 0 then
                            invalidArg "source" LanguagePrimitives.ErrorStrings.InputSequenceEmptyString
                        this.Result <- LanguagePrimitives.DivideByInt< ^T> this.Result this.State 
                    override this.OnDispose () = () })

        [<CompiledName "AverageBy">]
        let inline averageBy (f : 'T -> ^U) (source: ISeq< 'T >) : ^U
            when ^U:(static member Zero : ^U)
            and  ^U:(static member (+) : ^U * ^U -> ^U)
            and  ^U:(static member DivideByInt : ^U * int -> ^U) =
            source.Fold (fun _ ->
                upcast { new FolderWithPostProcessing<'T,'U, int>(LanguagePrimitives.GenericZero, 0) with
                    override this.ProcessNext value =
                        this.Result <- Checked.(+) this.Result (f value)
                        this.State <- this.State + 1
                        Unchecked.defaultof<_> (* return value unused in Fold context *)

                    override this.OnComplete _ =
                        if this.State = 0 then
                            invalidArg "source" LanguagePrimitives.ErrorStrings.InputSequenceEmptyString
                        this.Result <- LanguagePrimitives.DivideByInt< ^U> this.Result this.State
                    override this.OnDispose () = () })

        [<CompiledName "Empty">]
        let empty<'T> = EmptyEnumerable.Enumerable<'T>.Instance

        [<CompiledName "ExactlyOne">]
        let exactlyOne (source:ISeq<'T>) : 'T =
            source.Fold (fun pipeIdx ->
                upcast { new FolderWithPostProcessing<'T,'T,Values<bool, bool>>(Unchecked.defaultof<'T>, Values<bool,bool>(true, false)) with
                    override this.ProcessNext value =
                        if this.State._1 then
                            this.State._1 <- false
                            this.Result <- value
                        else
                            this.State._2 <- true
                            this.StopFurtherProcessing pipeIdx
                        Unchecked.defaultof<_> (* return value unused in Fold context *)

                    override this.OnComplete _ =
                        if this.State._1 then
                            invalidArg "source" LanguagePrimitives.ErrorStrings.InputSequenceEmptyString
                        elif this.State._2 then
                            invalidArg "source" (SR.GetString SR.inputSequenceTooLong)
                    override this.OnDispose () = () })

        [<CompiledName "Fold">]
        let inline fold<'T,'State> (f:'State->'T->'State) (seed:'State) (source:ISeq<'T>) : 'State =
            source.Fold (fun _ ->
                upcast { new Folder<'T,'State,NoValue>(seed,Unchecked.defaultof<NoValue>) with
                    override this.ProcessNext value =
                        this.Result <- f this.Result value
                        Unchecked.defaultof<_> (* return value unused in Fold context *) })

        [<CompiledName "Fold2">]
        let inline fold2<'T1,'T2,'State> (folder:'State->'T1->'T2->'State) (state:'State) (source1: ISeq<'T1>) (source2: ISeq<'T2>) =
            source1.Fold (fun pipeIdx ->
                upcast { new FolderWithPostProcessing<_,'State,IEnumerator<'T2>>(state,source2.GetEnumerator()) with
                    override self.ProcessNext value =
                        if self.State.MoveNext() then
                            self.Result <- folder self.Result value self.State.Current
                        else
                            self.StopFurtherProcessing pipeIdx
                        Unchecked.defaultof<_> (* return value unused in Fold context *)

                    override self.OnComplete _ = ()
                    override self.OnDispose () = self.State.Dispose() })

        [<CompiledName "Unfold">]
        let unfold (generator:'State->option<'T * 'State>) (state:'State) : ISeq<'T> =
            Upcast.seq (new Unfold.Enumerable<'T,'T,'State>(generator, state, IdentityFactory.Instance, 1))

        [<CompiledName "InitializeInfinite">]
        let initInfinite<'T> (f:int->'T) : ISeq<'T> =
            Upcast.seq (new Init.EnumerableDecider<'T>(Nullable (), f, 1))

        [<CompiledName "Initialize">]
        let init<'T> (count:int) (f:int->'T) : ISeq<'T> =
            if count < 0 then invalidArgInputMustBeNonNegative "count" count
            elif count = 0 then empty else
            Upcast.seq (new Init.EnumerableDecider<'T>(Nullable count, f, 1))

        [<CompiledName "Iterate">]
        let inline iter f (source:ISeq<'T>) =
            source.Fold (fun _ ->
                upcast { new Folder<'T,NoValue,NoValue> (Unchecked.defaultof<NoValue>,Unchecked.defaultof<NoValue>) with
                    override this.ProcessNext value =
                        f value
                        Unchecked.defaultof<_> (* return value unused in Fold context *) })
            |> ignore

        [<CompiledName "Iterate2">]
        let inline iter2 (f:'T->'U->unit) (source1:ISeq<'T>) (source2:ISeq<'U>) : unit =
            source1.Fold (fun pipeIdx ->
                upcast { new FolderWithPostProcessing<'T,NoValue,IEnumerator<'U>> (Unchecked.defaultof<_>,source2.GetEnumerator()) with
                    override self.ProcessNext value =
                        if self.State.MoveNext() then
                            f value self.State.Current
                        else
                            self.StopFurtherProcessing pipeIdx
                        Unchecked.defaultof<_> (* return value unused in Fold context *)

                    override self.OnComplete _ = ()
                    override self.OnDispose () = self.State.Dispose() })
            |> ignore

        [<CompiledName "IterateIndexed2">]
        let inline iteri2 (f:int->'T->'U->unit) (source1:ISeq<'T>) (source2:ISeq<'U>) : unit =
            source1.Fold (fun pipeIdx ->
                upcast { new FolderWithPostProcessing<'T,NoValue,Values<int,IEnumerator<'U>>>(Unchecked.defaultof<_>,Values<_,_>(-1,source2.GetEnumerator())) with
                    override self.ProcessNext value =
                        if self.State._2.MoveNext() then
                            f self.State._1 value self.State._2.Current
                            self.State._1 <- self.State._1 + 1
                            Unchecked.defaultof<_>
                        else
                            self.StopFurtherProcessing pipeIdx
                            Unchecked.defaultof<_>
                    override self.OnComplete _ = () 
                    override self.OnDispose () = self.State._2.Dispose() })
            |> ignore

        [<CompiledName "TryHead">]
        let tryHead (source:ISeq<'T>) =
            source.Fold (fun pipeIdx ->
                upcast { new Folder<'T, Option<'T>,NoValue> (None,Unchecked.defaultof<NoValue>) with
                    override this.ProcessNext value =
                        this.Result <- Some value
                        this.StopFurtherProcessing pipeIdx
                        Unchecked.defaultof<_> (* return value unused in Fold context *) })

        [<CompiledName "IterateIndexed">]
        let inline iteri f (source:ISeq<'T>) =
            source.Fold (fun _ ->
                { new Folder<'T,NoValue,int> (Unchecked.defaultof<_>,0) with
                    override this.ProcessNext value =
                        f this.State value
                        this.State <- this.State + 1
                        Unchecked.defaultof<_> (* return value unused in Fold context *) })
            |> ignore

        [<CompiledName "Except">]
        let inline except (itemsToExclude: seq<'T>) (source:ISeq<'T>) : ISeq<'T> when 'T:equality =
            source.PushTransform { new TransformFactory<'T,'T>() with
                override __.Compose _ _ next =
                    upcast { new Transform<'T,'V,Lazy<HashSet<'T>>>
                                    (next,lazy(HashSet<'T>(itemsToExclude,HashIdentity.Structural<'T>))) with
                        override this.ProcessNext (input:'T) : bool =
                            if this.State.Value.Add input then TailCall.avoid (next.ProcessNext input)
                            else false }}

        [<CompiledName "Exists">]
        let inline exists f (source:ISeq<'T>) =
            source.Fold (fun pipeIdx ->
                upcast { new Folder<'T, bool,NoValue> (false,Unchecked.defaultof<NoValue>) with
                    override this.ProcessNext value =
                        if f value then
                            this.Result <- true
                            this.StopFurtherProcessing pipeIdx
                        Unchecked.defaultof<_> (* return value unused in Fold context *) })

        [<CompiledName "Exists2">]
        let inline exists2 (predicate:'T->'U->bool) (source1: ISeq<'T>) (source2: ISeq<'U>) : bool =
            source1.Fold (fun pipeIdx ->
                upcast { new FolderWithPostProcessing<'T,bool,IEnumerator<'U>>(false,source2.GetEnumerator()) with
                    override self.ProcessNext value =
                        if self.State.MoveNext() then
                            if predicate value self.State.Current then
                                self.Result <- true
                                self.StopFurtherProcessing pipeIdx
                        else
                            self.StopFurtherProcessing pipeIdx
                        Unchecked.defaultof<_> (* return value unused in Fold context *)

                    override self.OnComplete _ = ()
                    override self.OnDispose () = self.State.Dispose() })

        [<CompiledName "Contains">]
        let inline contains element (source:ISeq<'T>) =
            source.Fold (fun pipeIdx ->
                upcast { new Folder<'T, bool,NoValue> (false,Unchecked.defaultof<NoValue>) with
                    override this.ProcessNext value =
                        if element = value then
                            this.Result <- true
                            this.StopFurtherProcessing pipeIdx
                        Unchecked.defaultof<_> (* return value unused in Fold context *) })

        [<CompiledName "ForAll">]
        let inline forall predicate (source:ISeq<'T>) =
            source.Fold (fun pipeIdx ->
                upcast { new Folder<'T, bool,NoValue> (true,Unchecked.defaultof<NoValue>) with
                    override this.ProcessNext value =
                        if not (predicate value) then
                            this.Result <- false
                            this.StopFurtherProcessing pipeIdx
                        Unchecked.defaultof<_> (* return value unused in Fold context *) })

        [<CompiledName "ForAll2">]
        let inline forall2 predicate (source1:ISeq<'T>) (source2:ISeq<'U>) : bool =
            source1.Fold (fun pipeIdx ->
                upcast { new FolderWithPostProcessing<'T,bool,IEnumerator<'U>>(true,source2.GetEnumerator()) with
                    override self.ProcessNext value =
                        if self.State.MoveNext() then
                            if not (predicate value self.State.Current) then
                                self.Result <- false
                                self.StopFurtherProcessing pipeIdx
                        else
                            self.StopFurtherProcessing pipeIdx
                        Unchecked.defaultof<_> (* return value unused in Fold context *)

                    override self.OnComplete _ = ()
                    override self.OnDispose () = self.State.Dispose() })

        [<CompiledName "Filter">]
        let inline filter<'T> (f:'T->bool) (source:ISeq<'T>) : ISeq<'T> =
            source.PushTransform { new TransformFactory<'T,'T>() with
                override __.Compose _ _ next =
                    upcast { new Transform<'T,'V,NoValue>(next,Unchecked.defaultof<NoValue>) with
                        override __.ProcessNext input =
                            if f input then TailCall.avoid (next.ProcessNext input)
                            else false } }

        [<CompiledName "Map">]
        let inline map<'T,'U> (f:'T->'U) (source:ISeq<'T>) : ISeq<'U> =
            source.PushTransform { new TransformFactory<'T,'U>() with
                override __.Compose _ _ next =
                    upcast { new Transform<'T,'V,NoValue>(next,Unchecked.defaultof<NoValue>) with
                        override __.ProcessNext input =
                            TailCall.avoid (next.ProcessNext (f input)) } }

        [<CompiledName "MapIndexed">]
        let inline mapi f (source:ISeq<_>) =
            source.PushTransform { new TransformFactory<'T,'U>() with
                override __.Compose _ _ next =
                    upcast { new Transform<'T,'V,int>(next, -1) with
                        override this.ProcessNext (input:'T) : bool =
                            this.State <- this.State  + 1
                            TailCall.avoid (next.ProcessNext (f this.State input)) } }

        [<CompiledName "Map2">]
        let inline map2<'First,'Second,'U> (map:'First->'Second->'U) (source1:ISeq<'First>) (source2:ISeq<'Second>) : ISeq<'U> =
            source1.PushTransform { new TransformFactory<'First,'U>() with
                override __.Compose outOfBand pipeIdx (next:Activity<'U,'V>) =
                    upcast { new TransformWithPostProcessing<'First,'V, IEnumerator<'Second>>(next, (source2.GetEnumerator ())) with
                        override self.ProcessNext input =
                            if self.State.MoveNext () then
                                TailCall.avoid (next.ProcessNext (map input self.State.Current))
                            else
                                outOfBand.StopFurtherProcessing pipeIdx
                                false
                        override self.OnComplete _ = () 
                        override self.OnDispose () = self.State.Dispose () }}

        [<CompiledName "MapIndexed2">]
        let inline mapi2<'First,'Second,'U> (map:int -> 'First->'Second->'U) (source1:ISeq<'First>) (source2:ISeq<'Second>) : ISeq<'U> =
            source1.PushTransform { new TransformFactory<'First,'U>() with
                override __.Compose<'V> outOfBand pipeIdx next =
                    upcast { new TransformWithPostProcessing<'First,'V, Values<int,IEnumerator<'Second>>>
                                                (next, Values<_,_>(-1, source2.GetEnumerator ())) with
                        override self.ProcessNext input =
                            if self.State._2.MoveNext () then
                                self.State._1 <- self.State._1 + 1
                                TailCall.avoid (next.ProcessNext (map self.State._1 input self.State._2.Current))
                            else
                                outOfBand.StopFurtherProcessing pipeIdx
                                false
                        override self.OnDispose () = self.State._2.Dispose ()
                        override self.OnComplete _ = () }}

        [<CompiledName "Map3">]
        let inline map3<'First,'Second,'Third,'U>
                        (map:'First->'Second->'Third->'U) (source1:ISeq<'First>) (source2:ISeq<'Second>) (source3:ISeq<'Third>) : ISeq<'U> =
            source1.PushTransform { new TransformFactory<'First,'U>() with
                override __.Compose<'V> outOfBand pipeIdx next =
                    upcast { new TransformWithPostProcessing<'First,'V, Values<IEnumerator<'Second>,IEnumerator<'Third>>>
                                                (next, Values<_,_>(source2.GetEnumerator(),source3.GetEnumerator())) with
                        override self.ProcessNext input =
                            if self.State._1.MoveNext() && self.State._2.MoveNext ()  then
                                TailCall.avoid (next.ProcessNext (map input self.State._1 .Current self.State._2.Current))
                            else
                                outOfBand.StopFurtherProcessing pipeIdx
                                false
                        override self.OnComplete _ = () 
                        override self.OnDispose () = 
                            self.State._1.Dispose ()
                            self.State._2.Dispose () }}

        [<CompiledName "CompareWith">]
        let inline compareWith (f:'T -> 'T -> int) (source1 :ISeq<'T>) (source2:ISeq<'T>) : int =
            source1.Fold (fun pipeIdx ->
                upcast { new FolderWithPostProcessing<'T,int,IEnumerator<'T>>(0,source2.GetEnumerator()) with
                    override self.ProcessNext value =
                        if not (self.State.MoveNext()) then
                            self.Result <- 1
                            self.StopFurtherProcessing pipeIdx
                        else
                            let c = f value self.State.Current
                            if c <> 0 then
                                self.Result <- c
                                self.StopFurtherProcessing pipeIdx
                        Unchecked.defaultof<_> (* return value unused in Fold context *)
                    override self.OnComplete _ =
                        if self.Result = 0 && self.State.MoveNext() then
                            self.Result <- -1
                    override self.OnDispose () = self.State.Dispose() })

        [<CompiledName "Choose">]
        let inline choose (f:'T->option<'U>) (source:ISeq<'T>) : ISeq<'U> =
            source.PushTransform { new TransformFactory<'T,'U>() with
                override __.Compose _ _ next =
                    upcast { new Transform<'T,'V,NoValue>(next,Unchecked.defaultof<NoValue>) with
                        override __.ProcessNext input =
                            match f input with
                            | Some value -> TailCall.avoid (next.ProcessNext value)
                            | None       -> false } }

        [<CompiledName "Distinct">]
        let inline distinct (source:ISeq<'T>) : ISeq<'T> when 'T:equality =
            source.PushTransform { new TransformFactory<'T,'T>() with
                override __.Compose _ _ next =
                    upcast { new Transform<'T,'V,HashSet<'T>>
                                    (next,(HashSet<'T>(HashIdentity.Structural<'T>))) with
                        override this.ProcessNext (input:'T) : bool =
                            if this.State.Add input then TailCall.avoid (next.ProcessNext input)
                            else false } }

        [<CompiledName "DistinctBy">]
        let inline distinctBy (keyf:'T->'Key) (source:ISeq<'T>) :ISeq<'T>  when 'Key:equality =
            source.PushTransform { new TransformFactory<'T,'T>() with
                override __.Compose _ _ next =
                    upcast { new Transform<'T,'V,HashSet<'Key>>
                                    (next,(HashSet<'Key>(HashIdentity.Structural<'Key>))) with
                        override this.ProcessNext (input:'T) : bool =
                            if this.State.Add (keyf input) then TailCall.avoid (next.ProcessNext input)
                            else false } }

        [<CompiledName "Max">]
        let inline max (source: ISeq<'T>) : 'T when 'T:comparison =
            source.Fold (fun _ ->
                upcast { new FolderWithPostProcessing<'T,'T,bool>(Unchecked.defaultof<'T>,true) with
                    override this.ProcessNext value =
                        if this.State then
                            this.State <- false
                            this.Result <- value
                        elif value > this.Result then
                            this.Result <- value
                        Unchecked.defaultof<_> (* return value unused in Fold context *)

                    override this.OnComplete _ =
                        if this.State then
                            invalidArg "source" LanguagePrimitives.ErrorStrings.InputSequenceEmptyString
                    override self.OnDispose () = () })

        [<CompiledName "MaxBy">]
        let inline maxBy (f :'T -> 'U) (source: ISeq<'T>) : 'T when 'U:comparison =
            source.Fold (fun _ ->
                upcast { new FolderWithPostProcessing<'T,'T,Values<bool,'U>>(Unchecked.defaultof<'T>,Values<_,_>(true,Unchecked.defaultof<'U>)) with
                    override this.ProcessNext value =
                        match this.State._1, f value with
                        | true, valueU ->
                            this.State._1 <- false
                            this.State._2 <- valueU
                            this.Result <- value
                        | false, valueU when valueU > this.State._2 ->
                            this.State._2 <- valueU
                            this.Result <- value
                        | _ -> ()
                        Unchecked.defaultof<_> (* return value unused in Fold context *)

                    override this.OnComplete _ =
                        if this.State._1 then
                            invalidArg "source" LanguagePrimitives.ErrorStrings.InputSequenceEmptyString
                    override self.OnDispose () = () })

        [<CompiledName "Min">]
        let inline min (source: ISeq< 'T>) : 'T when 'T:comparison =
            source.Fold (fun _ ->
                upcast { new FolderWithPostProcessing<'T,'T,bool>(Unchecked.defaultof<'T>,true) with
                    override this.ProcessNext value =
                        if this.State then
                            this.State <- false
                            this.Result <- value
                        elif value < this.Result then
                            this.Result <- value
                        Unchecked.defaultof<_> (* return value unused in Fold context *)

                    override this.OnComplete _ =
                        if this.State then
                            invalidArg "source" LanguagePrimitives.ErrorStrings.InputSequenceEmptyString
                    override self.OnDispose () = () })

        [<CompiledName "MinBy">]
        let inline minBy (f : 'T -> 'U) (source: ISeq<'T>) : 'T =
            source.Fold (fun _ ->
                upcast { new FolderWithPostProcessing<'T,'T,Values<bool,'U>>(Unchecked.defaultof<'T>,Values<_,_>(true,Unchecked.defaultof< 'U>)) with
                    override this.ProcessNext value =
                        match this.State._1, f value with
                        | true, valueU ->
                            this.State._1 <- false
                            this.State._2 <- valueU
                            this.Result <- value
                        | false, valueU when valueU < this.State._2 ->
                            this.State._2 <- valueU
                            this.Result <- value
                        | _ -> ()
                        Unchecked.defaultof<_> (* return value unused in Fold context *)

                    override this.OnComplete _ =
                        if this.State._1 then
                            invalidArg "source" LanguagePrimitives.ErrorStrings.InputSequenceEmptyString
                    override self.OnDispose () = () })

        [<CompiledName "Pairwise">]
        let pairwise (source:ISeq<'T>) : ISeq<'T*'T> =
            source.PushTransform { new TransformFactory<'T,'T * 'T>() with
                override __.Compose _ _ next =
                    upcast { new Transform<'T,'U,Values<bool,'T>>
                                (   next
                                ,   Values<bool,'T>
                                    ((* isFirst   = _1*) true
                                    ,(* lastValue = _2*) Unchecked.defaultof<'T>
                                    )
                                ) with
                            override self.ProcessNext (input:'T) : bool =
                                if (*isFirst*) self.State._1  then
                                    self.State._2 (*lastValue*)<- input
                                    self.State._1 (*isFirst*)<- false
                                    false
                                else
                                    let currentPair = self.State._2, input
                                    self.State._2 (*lastValue*)<- input
                                    TailCall.avoid (next.ProcessNext currentPair) }}

        [<CompiledName "Reduce">]
        let inline reduce (f:'T->'T->'T) (source : ISeq<'T>) : 'T =
            source.Fold (fun _ ->
                upcast { new FolderWithPostProcessing<'T,'T,bool>(Unchecked.defaultof<'T>,true) with
                    override this.ProcessNext value =
                        if this.State then
                            this.State <- false
                            this.Result <- value
                        else
                            this.Result <- f this.Result value
                        Unchecked.defaultof<_> (* return value unused in Fold context *)

                    override this.OnComplete _ =
                        if this.State then
                            invalidArg "source" LanguagePrimitives.ErrorStrings.InputSequenceEmptyString
                    override self.OnDispose () = () })

        [<CompiledName "Scan">]
        let inline scan (folder:'State->'T->'State) (initialState:'State) (source:ISeq<'T>) :ISeq<'State> =
            source.PushTransform { new TransformFactory<'T,'State>() with
                override __.Compose _ _ next =
                    upcast { new Transform<'T,'V,'State>(next, initialState) with
                        override this.ProcessNext (input:'T) : bool =
                            this.State <- folder this.State input
                            TailCall.avoid (next.ProcessNext this.State) } }

        [<CompiledName "Skip">]
        let skip (skipCount:int) (source:ISeq<'T>) : ISeq<'T> =
            source.PushTransform { new TransformFactory<'T,'T>() with
                override __.Compose _ _ next =
                    upcast {
                        new TransformWithPostProcessing<'T,'U,int>(next,(*count*)0) with
                            override self.ProcessNext (input:'T) : bool =
                                if (*count*) self.State < skipCount then
                                    self.State <- self.State + 1
                                    false
                                else
                                    TailCall.avoid (next.ProcessNext input)

                            override self.OnComplete _ =
                                if (*count*) self.State < skipCount then
                                    let x = skipCount - self.State
                                    invalidOpFmt "{0}\ntried to skip {1} {2} past the end of the seq"
                                        [|SR.GetString SR.notEnoughElements; x; (if x=1 then "element" else "elements")|]
                            override self.OnDispose () = ()

                        interface ISkipping with
                            member self.Skipping () =
                                let self = self :?> TransformWithPostProcessing<'T,'U,int>
                                if (*count*) self.State < skipCount then
                                    self.State <- self.State + 1
                                    true
                                else
                                    false }}

        [<CompiledName "SkipWhile">]
        let inline skipWhile (predicate:'T->bool) (source:ISeq<'T>) : ISeq<'T> =
            source.PushTransform { new TransformFactory<'T,'T>() with
                override __.Compose _ _ next =
                    upcast { new Transform<'T,'V,bool>(next,true) with
                        override self.ProcessNext (input:'T) : bool =
                            if self.State (*skip*) then
                                self.State <- predicate input
                                if self.State (*skip*) then
                                    false
                                else
                                    TailCall.avoid (next.ProcessNext input)
                            else
                                TailCall.avoid (next.ProcessNext input) }}

        [<CompiledName "Sum">]
        let inline sum (source:ISeq< ^T>) : ^T
            when ^T:(static member Zero : ^T)
            and  ^T:(static member (+) :  ^T *  ^T ->  ^T) =
            source.Fold (fun _ ->
                upcast { new Folder< ^T,^T,NoValue> (LanguagePrimitives.GenericZero,Unchecked.defaultof<NoValue>) with
                    override this.ProcessNext value =
                        this.Result <- Checked.(+) this.Result value
                        Unchecked.defaultof<_> (* return value unused in Fold context *) })

        [<CompiledName "SumBy">]
        let inline sumBy (f : 'T -> ^U) (source: ISeq<'T>) : ^U
            when ^U:(static member Zero : ^U)
            and  ^U:(static member (+) :  ^U *  ^U ->  ^U) =
            source.Fold (fun _ ->
                upcast { new Folder<'T,'U,NoValue> (LanguagePrimitives.GenericZero< ^U>,Unchecked.defaultof<NoValue>) with
                    override this.ProcessNext value =
                        this.Result <- Checked.(+) this.Result (f value)
                        Unchecked.defaultof<_> (* return value unused in Fold context *) })

        [<CompiledName "Take">]
        let take (takeCount:int) (source:ISeq<'T>) : ISeq<'T> =
            source.PushTransform { new TransformFactory<'T,'T>() with
                member __.Compose outOfBand pipelineIdx next =
                    upcast {
                        new TransformWithPostProcessing<'T,'U,int>(next,(*count*)0) with
                            override self.ProcessNext (input:'T) : bool =
                                if (*count*) self.State < takeCount then
                                    self.State <- self.State + 1
                                    if self.State = takeCount then
                                        outOfBand.StopFurtherProcessing pipelineIdx
                                    TailCall.avoid (next.ProcessNext input)
                                else
                                    outOfBand.StopFurtherProcessing pipelineIdx
                                    false

                            override this.OnComplete terminatingIdx =
                                if terminatingIdx < pipelineIdx && this.State < takeCount then
                                    let x = takeCount - this.State
                                    invalidOpFmt "tried to take {0} {1} past the end of the seq"
                                        [|SR.GetString SR.notEnoughElements; x; (if x=1 then "element" else "elements")|]
                            override this.OnDispose () = () }}

        [<CompiledName "TakeWhile">]
        let inline takeWhile (predicate:'T->bool) (source:ISeq<'T>) : ISeq<'T> =
            source.PushTransform { new TransformFactory<'T,'T>() with
                member __.Compose outOfBand pipeIdx next =
                    upcast { new Transform<'T,'V,NoValue>(next,Unchecked.defaultof<NoValue>) with
                        override __.ProcessNext (input:'T) : bool =
                            if predicate input then
                                TailCall.avoid (next.ProcessNext input)
                            else
                                outOfBand.StopFurtherProcessing pipeIdx
                                false }}

        [<CompiledName "Tail">]
        let tail (source:ISeq<'T>) :ISeq<'T> =
            source.PushTransform { new TransformFactory<'T,'T>() with
                member __.Compose _ _ next =
                    upcast { new TransformWithPostProcessing<'T,'V,bool>(next,(*first*) true) with
                        override self.ProcessNext (input:'T) : bool =
                            if (*first*) self.State then
                                self.State <- false
                                false
                            else
                                TailCall.avoid (next.ProcessNext input)

                        override self.OnComplete _ =
                            if (*first*) self.State then
                                invalidArg "source" (SR.GetString SR.notEnoughElements) 
                        override self.OnDispose () = () }}

        [<CompiledName "Truncate">]
        let truncate (truncateCount:int) (source:ISeq<'T>) : ISeq<'T> =
            source.PushTransform { new TransformFactory<'T,'T>() with
                member __.Compose outOfBand pipeIdx next =
                    upcast {
                        new Transform<'T,'U,int>(next,(*count*)0) with
                            override self.ProcessNext (input:'T) : bool =
                                if (*count*) self.State < truncateCount then
                                    self.State <- self.State + 1
                                    if self.State = truncateCount then
                                        outOfBand.StopFurtherProcessing pipeIdx
                                    TailCall.avoid (next.ProcessNext input)
                                else
                                    outOfBand.StopFurtherProcessing pipeIdx
                                    false }}

        [<CompiledName "Indexed">]
        let indexed source =
            mapi (fun i x -> i,x) source

        [<CompiledName "TryItem">]
        let tryItem index (source:ISeq<'T>) =
            if index < 0 then None else
            source |> skip index |> tryHead

        [<CompiledName "TryPick">]
        let inline tryPick f (source:ISeq<'T>)  =
            source.Fold (fun pipeIdx ->
                upcast { new Folder<'T, Option<'U>,NoValue> (None,Unchecked.defaultof<NoValue>) with
                    override this.ProcessNext value =
                        match f value with
                        | (Some _) as some ->
                            this.Result <- some
                            this.StopFurtherProcessing pipeIdx
                        | None -> ()
                        Unchecked.defaultof<_> (* return value unused in Fold context *) })

        [<CompiledName "TryFind">]
        let inline tryFind f (source:ISeq<'T>)  =
            source.Fold (fun pipeIdx ->
                upcast { new Folder<'T, Option<'T>,NoValue> (None,Unchecked.defaultof<NoValue>) with
                    override this.ProcessNext value =
                        if f value then
                            this.Result <- Some value
                            this.StopFurtherProcessing pipeIdx
                        Unchecked.defaultof<_> (* return value unused in Fold context *) })

        [<CompiledName "TryFindIndex">]
        let inline tryFindIndex (predicate:'T->bool) (source:ISeq<'T>) : int option =
            source.Fold (fun pipeIdx ->
                { new Folder<'T, Option<int>, int>(None, 0) with
                    override this.ProcessNext value =
                        if predicate value then
                            this.Result <- Some this.State
                            this.StopFurtherProcessing pipeIdx
                        else
                            this.State <- this.State + 1
                        Unchecked.defaultof<_> (* return value unused in Fold context *) })

        [<CompiledName "TryLast">]
        let inline tryLast (source :ISeq<'T>) : 'T option =
            source.Fold (fun _ ->
                upcast { new FolderWithPostProcessing<'T,option<'T>,Values<bool,'T>>(None,Values<bool,'T>(true, Unchecked.defaultof<'T>)) with
                    override this.ProcessNext value =
                        if this.State._1 then
                            this.State._1 <- false
                        this.State._2 <- value
                        Unchecked.defaultof<_> (* return value unused in Fold context *)
                    override this.OnComplete _ =
                        if not this.State._1 then
                            this.Result <- Some this.State._2
                    override self.OnDispose () = () })

        [<CompiledName "Windowed">]
        let windowed (windowSize:int) (source:ISeq<'T>) : ISeq<'T[]> =
            source.PushTransform { new TransformFactory<'T,'T[]>() with
                member __.Compose outOfBand pipeIdx next =
                    upcast {
                        new Transform<'T,'U,Values<'T[],int,int>>
                                    (   next
                                    ,   Values<'T[],int,int>
                                        ((*circularBuffer = _1 *) Array.zeroCreateUnchecked windowSize
                                        ,(* idx = _2 *)          0
                                        ,(* priming = _3 *)      windowSize-1
                                        )
                                    ) with
                            override self.ProcessNext (input:'T) : bool =
                                self.State._1.[(* idx *)self.State._2] <- input

                                self.State._2 <- (* idx *)self.State._2 + 1
                                if (* idx *) self.State._2 = windowSize then
                                    self.State._2 <- 0

                                if (* priming  *) self.State._3 > 0 then
                                    self.State._3 <- self.State._3 - 1
                                    false
                                else
                                    if windowSize < 32 then
                                        let window :'T [] = Array.init windowSize (fun i -> self.State._1.[((* idx *)self.State._2+i) % windowSize]: 'T)
                                        TailCall.avoid (next.ProcessNext window)
                                    else
                                        let window = Array.zeroCreateUnchecked windowSize
                                        Array.Copy((*circularBuffer*)self.State._1, (* idx *)self.State._2, window, 0, windowSize - (* idx *)self.State._2)
                                        Array.Copy((*circularBuffer*)self.State._1, 0, window, windowSize - (* idx *)self.State._2, (* idx *)self.State._2)
                                        TailCall.avoid (next.ProcessNext window) }}

        [<CompiledName("Concat")>]
        let concat (sources:ISeq<#ISeq<'T>>) : ISeq<'T> =
            Upcast.seq (Enumerable.ConcatEnumerable sources)

        [<CompiledName("Append")>]
        let append (source1: ISeq<'T>) (source2: ISeq<'T>) : ISeq<'T> =
            match source1 with
            | :? Enumerable.EnumerableBase<'T> as s -> s.Append source2
            | _ -> Upcast.seq (new Enumerable.AppendEnumerable<_>([source2; source1]))
