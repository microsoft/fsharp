// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.FSharp.Control

    open Microsoft.FSharp.Core
    open Microsoft.FSharp.Core.LanguagePrimitives
    open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicOperators
    open Microsoft.FSharp.Core.Operators
    open Microsoft.FSharp.Collections
    open Microsoft.FSharp.Control
    open System.Reflection
    open System.Diagnostics
    
    module private Atomic =
        open System.Threading

        let inline setWith (thunk: 'a -> 'a) (value: byref<'a>) =
            let mutable exchanged = false
            let mutable oldValue = value
            while not exchanged do
                let comparand = oldValue
                let newValue = thunk comparand
                oldValue <- Interlocked.CompareExchange(&value, newValue, comparand)
                if obj.ReferenceEquals(comparand, oldValue) then
                    exchanged <- true

    [<CompiledName("FSharpDelegateEvent`1")>]
    type DelegateEvent<'Delegate when 'Delegate :> System.Delegate>() = 
        let mutable multicast : System.Delegate = null
        member x.Trigger(args:obj[]) = 
            match multicast with 
            | null -> ()
            | d -> d.DynamicInvoke(args) |> ignore
        member x.Publish = 
            { new IDelegateEvent<'Delegate> with 
                member x.AddHandler(d) =
                    Atomic.setWith (fun value -> System.Delegate.Combine(value, d)) &multicast
                member x.RemoveHandler(d) =
                    Atomic.setWith (fun value -> System.Delegate.Remove(value, d)) &multicast }
    
    type EventDelegee<'Args>(observer: System.IObserver<'Args>) =
        static let makeTuple = 
            if Microsoft.FSharp.Reflection.FSharpType.IsTuple(typeof<'Args>) then
                Microsoft.FSharp.Reflection.FSharpValue.PreComputeTupleConstructor(typeof<'Args>)
            else
                fun _ -> assert false; null // should not be called, one-argument case don't use makeTuple function

        member x.Invoke(_sender:obj, args: 'Args) = 
            observer.OnNext args
        member x.Invoke(_sender:obj, a, b) = 
            let args = makeTuple([|a; b|]) :?> 'Args
            observer.OnNext args
        member x.Invoke(_sender:obj, a, b, c) = 
            let args = makeTuple([|a; b; c|]) :?> 'Args
            observer.OnNext args
        member x.Invoke(_sender:obj, a, b, c, d) = 
            let args = makeTuple([|a; b; c; d|]) :?> 'Args
            observer.OnNext args
        member x.Invoke(_sender:obj, a, b, c, d, e) = 
            let args = makeTuple([|a; b; c; d; e|]) :?> 'Args
            observer.OnNext args
        member x.Invoke(_sender:obj, a, b, c, d, e, f) = 
            let args = makeTuple([|a; b; c; d; e; f|]) :?> 'Args
            observer.OnNext args


    type EventWrapper<'Delegate,'Args> = delegate of 'Delegate * obj * 'Args -> unit

    [<CompiledName("FSharpEvent`2")>]
    type Event<'Delegate, 'Args when 'Delegate : delegate<'Args, unit> and 'Delegate :> System.Delegate and 'Delegate: not struct>() =  

        let mutable multicast : 'Delegate = Unchecked.defaultof<_>     

        static let mi, argTypes = 
            let instanceBindingFlags = BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.DeclaredOnly
            let mi = typeof<'Delegate>.GetMethod("Invoke",instanceBindingFlags)
            let actualTypes = mi.GetParameters() |> Array.map (fun p -> p.ParameterType)
            mi, actualTypes.[1..]

        // For the one-argument case, use an optimization that allows a fast call. 
        // CreateDelegate creates a delegate that is fast to invoke.
        static let invoker = 
            if argTypes.Length = 1 then 
                (System.Delegate.CreateDelegate(typeof<EventWrapper<'Delegate,'Args>>, mi) :?> EventWrapper<'Delegate,'Args>)
            else
                null

        // For the multi-arg case, use a slower DynamicInvoke.
        static let invokeInfo =
            let instanceBindingFlags = BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.DeclaredOnly
            let mi = 
                typeof<EventDelegee<'Args>>.GetMethods(instanceBindingFlags)
                |> Seq.filter(fun mi -> mi.Name = "Invoke" && mi.GetParameters().Length = argTypes.Length + 1)
                |> Seq.exactlyOne
            if mi.IsGenericMethodDefinition then
                mi.MakeGenericMethod argTypes
            else 
                mi                

        member x.Trigger(sender:obj,args: 'Args) = 
            // Copy multicast value into local variable to avoid changing during member call. 
            let multicast = multicast
            match box multicast with 
            | null -> () 
            | _ -> 
                match invoker with 
                | null ->  
                    let args = Array.append [| sender |] (Microsoft.FSharp.Reflection.FSharpValue.GetTupleFields(box args))
                    multicast.DynamicInvoke(args) |> ignore
                | _ -> 
                    // For the one-argument case, use an optimization that allows a fast call. 
                    // CreateDelegate creates a delegate that is fast to invoke.
                    invoker.Invoke(multicast, sender, args) |> ignore

        member x.Publish =
            { new obj() with
                  member x.ToString() = "<published event>"
              interface IEvent<'Delegate,'Args> with 
                member e.AddHandler(d) =
                    Atomic.setWith (fun value -> System.Delegate.Combine(value, d) :?> 'Delegate) &multicast
                member e.RemoveHandler(d) = 
                    Atomic.setWith (fun value -> System.Delegate.Remove(value, d) :?> 'Delegate) &multicast
              interface System.IObservable<'Args> with 
                member e.Subscribe(observer) = 
                   let obj = new EventDelegee<'Args>(observer)
                   let h = System.Delegate.CreateDelegate(typeof<'Delegate>, obj, invokeInfo) :?> 'Delegate
                   (e :?> IDelegateEvent<'Delegate>).AddHandler(h)
                   { new System.IDisposable with 
                        member x.Dispose() = (e :?> IDelegateEvent<'Delegate>).RemoveHandler(h) } } 


    [<CompiledName("FSharpEvent`1")>]
    type Event<'T> = 
        val mutable multicast : Handler<'T>
        new() = { multicast = null }

        member x.Trigger(arg:'T) = 
            match x.multicast with 
            | null -> ()
            | d -> d.Invoke(null,arg) |> ignore
        member x.Publish =
            { new obj() with
                  member x.ToString() = "<published event>"
              interface IEvent<'T> with 
                member e.AddHandler(d) =
                    Atomic.setWith (fun value -> System.Delegate.Combine(value, d) :?> Handler<'T>) &x.multicast
                member e.RemoveHandler(d) = 
                    Atomic.setWith (fun value -> System.Delegate.Remove(value, d) :?> Handler<'T>) &x.multicast
              interface System.IObservable<'T> with 
                member e.Subscribe(observer) = 
                   let h = new Handler<_>(fun sender args -> observer.OnNext(args))
                   (e :?> IEvent<_,_>).AddHandler(h)
                   { new System.IDisposable with 
                        member x.Dispose() = (e :?> IEvent<_,_>).RemoveHandler(h) } }
