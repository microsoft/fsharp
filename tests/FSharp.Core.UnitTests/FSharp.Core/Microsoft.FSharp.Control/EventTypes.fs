// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

// Various tests for the:
// Microsoft.FSharp.Control event types

namespace FSharp.Core.UnitTests.Control

open System
open System.Reflection
open Xunit

module private EventTypes =
    type MultiArgDelegate = delegate of obj * obj[] -> unit

    let getListeners event =
        let eventType = event.GetType()
        let multicastField = 
            eventType
                .GetField("multicast", BindingFlags.NonPublic ||| BindingFlags.Instance)
                .GetValue event 
                :?> System.Delegate

        if not (isNull multicastField) then
            let multicastType = typeof<System.MulticastDelegate>
            let listeners = 
                multicastType
                    .GetMethod("GetInvocationList")
                    .Invoke(multicastField, [||]) 
                    :?> System.Delegate []
            Some listeners              
        else
            None

type EventTypes() =

    [<Literal>]
    let RunsCount = 100

    let runAddRemoveHandlers (event: IDelegateEvent<_>) handlerInitializer =
        seq {
            for _ in 1 .. RunsCount do
                async {
                    let h = handlerInitializer()
                    event.AddHandler(h)
                    event.RemoveHandler(h)
                }
        } |> Async.Parallel |> Async.RunSynchronously |> ignore

    [<Fact>]
    member this.``Adding/removing handlers to published Event<'T> is thread-safe``() = 
        let event = new Event<int>()  
        let listenersBefore = EventTypes.getListeners event
        runAddRemoveHandlers (event.Publish) (fun _ -> new Handler<_>(fun sender args -> ()))
        let listenersAfter = EventTypes.getListeners event

        Assert.True(listenersBefore.IsNone)
        Assert.True(listenersAfter.IsNone)

    [<Fact>]
    member this.``Adding/removing handlers to published DelegateEvent is thread-safe``() = 
        let event = new DelegateEvent<_>()  
        let listenersBefore = EventTypes.getListeners event
        runAddRemoveHandlers (event.Publish) (fun _ -> EventTypes.MultiArgDelegate(fun sender args -> ()))
        let listenersAfter = EventTypes.getListeners event

        Assert.True(listenersBefore.IsNone)
        Assert.True(listenersAfter.IsNone)

    [<Fact>]
    member this.``Adding/removing handlers to published Event<'D,'A> is thread-safe``() = 
        let event = new Event<_, _>()  
        let listenersBefore = EventTypes.getListeners event
        runAddRemoveHandlers (event.Publish) (fun _ -> EventTypes.MultiArgDelegate(fun sender args -> ()))
        let listenersAfter = EventTypes.getListeners event

        Assert.True(listenersBefore.IsNone)
        Assert.True(listenersAfter.IsNone)
