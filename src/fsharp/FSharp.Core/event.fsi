// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.FSharp.Control

    open System
    open Microsoft.FSharp.Core
    open Microsoft.FSharp.Core.Operators
    open Microsoft.FSharp.Control
    open Microsoft.FSharp.Collections

    /// <summary>Event implementations for an arbitrary type of delegate.</summary>
    ///
    /// <category index="3">Events and Observables</category>
    [<CompiledName("FSharpDelegateEvent`1")>]
    type DelegateEvent<'Delegate when 'Delegate :> System.Delegate> = 
        /// <summary>Creates an event object suitable for implementing an arbitrary type of delegate.</summary>
        /// <returns>The event object.</returns>
        new : unit -> DelegateEvent<'Delegate>
        /// <summary>Triggers the event using the given parameters.</summary>
        /// <param name="args">The parameters for the event.</param>
        member Trigger : args:obj[] -> unit
        /// <summary>Publishes the event as a first class event value.</summary>
        member Publish : IDelegateEvent<'Delegate>

    /// <summary>Event implementations for a delegate types following the standard .NET Framework convention of a first 'sender' argument.</summary>
    ///
    /// <category index="3">Events and Observables</category>
    [<CompiledName("FSharpEvent`2")>]
    type Event<'Delegate,'Args when 'Delegate : delegate<'Args,unit> and 'Delegate :> System.Delegate and 'Delegate : not struct> = 
        /// <summary>Creates an event object suitable for delegate types following the standard .NET Framework convention of a first 'sender' argument.</summary>
        /// <returns>The created event.</returns>
        new : unit -> Event<'Delegate,'Args>
        /// <summary>Triggers the event using the given sender object and parameters. The sender object may be <c>null</c>.</summary>
        ///
        /// <param name="sender">The object triggering the event.</param>
        /// <param name="args">The parameters for the event.</param>
        member Trigger : sender:obj * args:'Args -> unit
        /// <summary>Publishes the event as a first class event value.</summary>
        member Publish : IEvent<'Delegate,'Args> 


    /// <summary>Event implementations for the IEvent&lt;_&gt; type.</summary>
    ///
    /// <category index="3">Events and Observables</category>
    [<CompiledName("FSharpEvent`1")>]
    type Event<'T> = 
        /// <summary>Creates an observable object.</summary>
        /// <returns>The created event.</returns>
        new : unit -> Event<'T>
        /// <summary>Triggers an observation using the given parameters.</summary>
        ///
        /// <param name="arg">The event parameters.</param>
        member Trigger : arg:'T -> unit
        /// <summary>Publishes an observation  as a first class value.</summary>
        member Publish : IEvent<'T> 
