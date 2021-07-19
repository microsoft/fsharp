// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.FSharp.Core.CompilerServices

#if !BUILDING_WITH_LKG && !BUILD_FROM_SOURCE
open Microsoft.FSharp.Collections
open Microsoft.FSharp.Core
open System
open System.Runtime.CompilerServices

/// Acts as a template for struct state machines introduced by __stateMachine, and also as a reflective implementation
[<Struct; NoComparison; NoEquality>]
[<Experimental("Experimental library feature, requires '--langversion:preview'")>]
type ResumableStateMachine<'Data> =

    /// When statically compiled, holds the data for the state machine
    [<DefaultValue(false)>]
    val mutable Data: 'Data

    /// When statically compiled, holds the continuation goto-label further execution of the state machine
    [<DefaultValue(false)>]
    val mutable ResumptionPoint: int

    /// <summary>Represents the delegated runtime continuation for a resumable state machine created dynamically</summary>
    /// <remarks>This field is removed from state machines generated using '__stateMachine'. Resumable code
    /// used in state machines which accesses this field will raise a runtime exception.</remarks>
    [<DefaultValue(false)>]
    val mutable ResumptionDynamicInfo: ResumptionDynamicInfo<'Data>

    interface IResumableStateMachine<'Data>

    interface IAsyncStateMachine

and 
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
    IResumableStateMachine<'Data> =
    /// Get the resumption point of the state machine
    abstract ResumptionPoint: int

    /// Copy-out or copy-in the data of the state machine
    abstract Data: 'Data with get, set

/// Represents the delegated runtime continuation of a resumable state machine created dynamically
and
    [<AbstractClass; Experimental("Experimental library feature, requires '--langversion:preview'")>]
    ResumptionDynamicInfo<'Data> =

    /// Create dynamic information for a state machine
    new: initial: ResumptionFunc<'Data> -> ResumptionDynamicInfo<'Data>
    
    /// The continuation of the state machine
    member ResumptionFunc: ResumptionFunc<'Data> with get, set 
    
    /// Additional data associated with the state machine
    member ResumptionData: obj with get, set 

    /// Executes the MoveNext implementation of the state machine
    abstract MoveNext: machine: byref<ResumableStateMachine<'Data>> -> unit

    /// Executes the SetStateMachine implementation of the state machine
    abstract SetStateMachine: machine: byref<ResumableStateMachine<'Data>> * machineState: IAsyncStateMachine -> unit

/// Represents the runtime continuation of a resumable state machine created dynamically
and [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
    ResumptionFunc<'Data> = delegate of byref<ResumableStateMachine<'Data>> -> bool

/// A special compiler-recognised delegate type for specifying blocks of resumable code
/// with access to the state machine.
[<Experimental("Experimental library feature, requires '--langversion:preview'")>]
type ResumableCode<'Data, 'T> = delegate of byref<ResumableStateMachine<'Data>> -> bool

/// Contains functions for composing resumable code blocks
[<Microsoft.FSharp.Core.Experimental("Experimental library feature, requires '--langversion:preview'")>]
[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module ResumableCode =

    /// Sequences one section of resumable code after another
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
    val inline Combine: code1: ResumableCode<'Data, unit> * code2: ResumableCode<'Data, 'T> -> ResumableCode<'Data, 'T>

    /// Creates resumable code whose definition is a delayed function
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
    val inline Delay: f: (unit -> ResumableCode<'Data, 'T>) -> ResumableCode<'Data, 'T>

    /// Specifies resumable code which iterates an input sequence
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
    val inline For: sequence: seq<'T> * body: ('T -> ResumableCode<'Data, unit>) -> ResumableCode<'Data, unit>

    /// Specifies resumable code which iterates yields
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
    val inline Yield: unit -> ResumableCode<'Data, unit>

    /// Specifies resumable code which executes with try/finally semantics
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
    val inline TryFinally: body: ResumableCode<'Data, 'T> * compensation: ResumableCode<'Data,unit> -> ResumableCode<'Data, 'T>

    /// Specifies resumable code which executes with try/finally semantics
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
    val inline TryFinallyAsync: body: ResumableCode<'Data, 'T> * compensation: ResumableCode<'Data,unit> -> ResumableCode<'Data, 'T>

    /// Specifies resumable code which executes with try/with semantics
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
    val inline TryWith: body: ResumableCode<'Data, 'T> * catch: (exn -> ResumableCode<'Data, 'T>) -> ResumableCode<'Data, 'T>

    /// Specifies resumable code which executes with 'use' semantics
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
    val inline Using: resource: 'Resource * body: ('Resource -> ResumableCode<'Data, 'T>) -> ResumableCode<'Data, 'T> when 'Resource :> IDisposable

    /// Specifies resumable code which executes a loop
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
    val inline While: [<InlineIfLambda>] condition: (unit -> bool) * body: ResumableCode<'Data, unit> -> ResumableCode<'Data, unit>

    /// Specifies resumable code which does nothing
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
    val inline Zero: unit -> ResumableCode<'Data, unit>

    /// The dynamic implementation of the corresponding operation. This operation should not be used directly.
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
    val CombineDynamic: sm: byref<ResumableStateMachine<'Data>> * code1: ResumableCode<'Data, unit> * code2: ResumableCode<'Data, 'T> -> bool

    /// The dynamic implementation of the corresponding operation. This operation should not be used directly.
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
    val WhileDynamic: sm: byref<ResumableStateMachine<'Data>> * condition: (unit -> bool) * body: ResumableCode<'Data, unit> -> bool

    /// The dynamic implementation of the corresponding operation. This operation should not be used directly.
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
    val TryFinallyAsyncDynamic: sm: byref<ResumableStateMachine<'Data>> * body: ResumableCode<'Data, 'T> * compensation: ResumableCode<'Data,unit> -> bool

    /// The dynamic implementation of the corresponding operation. This operation should not be used directly.
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
    val TryWithDynamic: sm: byref<ResumableStateMachine<'Data>> * body: ResumableCode<'Data, 'T> * handler: (exn -> ResumableCode<'Data, 'T>) -> bool

    /// The dynamic implementation of the corresponding operation. This operation should not be used directly.
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
    val YieldDynamic: sm: byref<ResumableStateMachine<'Data>> -> bool

/// Defines the implementation of the MoveNext method for a struct state machine.
[<Experimental("Experimental library feature, requires '--langversion:preview'")>]
type MoveNextMethodImpl<'Data> = delegate of byref<ResumableStateMachine<'Data>> -> unit

/// Defines the implementation of the SetStateMachine method for a struct state machine.
[<Experimental("Experimental library feature, requires '--langversion:preview'")>]
type SetStateMachineMethodImpl<'Data> = delegate of byref<ResumableStateMachine<'Data>> * IAsyncStateMachine -> unit

/// Defines the implementation of the code run after the creation of a struct state machine.
[<Experimental("Experimental library feature, requires '--langversion:preview'")>]
type AfterCode<'Data, 'Result> = delegate of byref<ResumableStateMachine<'Data>> -> 'Result

/// Contains compiler intrinsics related to the definition of state machines.
[<Microsoft.FSharp.Core.Experimental("Experimental library feature, requires '--langversion:preview'")>]
module StateMachineHelpers = 

    /// <summary>
    /// When used in a conditional, statically determines whether the 'then' branch
    /// represents valid resumable code and provides an alternative implementation
    /// if not.
    /// </summary>
    [<MethodImpl(MethodImplOptions.NoInlining)>]
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
    val __useResumableCode<'T> : bool 

    /// <summary>
    /// Indicates a resumption point within resumable code
    /// </summary>
    [<MethodImpl(MethodImplOptions.NoInlining)>]
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
    val __resumableEntry: unit -> int option

    /// <summary>
    /// Indicates to jump to a resumption point within resumable code.
    /// This may be the first statement in a MoveNextMethodImpl.
    /// The integer must be a valid resumption point within this resumable code.
    /// </summary>
    /// <param name="programLabel"></param>
    [<MethodImpl(MethodImplOptions.NoInlining)>]
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
    val __resumeAt : programLabel: int -> 'T

    /// <summary>
    /// Statically generates a closure struct type based on ResumableStateMachine,
    /// At runtime an instance of the new struct type is populated and 'afterMethod' is called
    /// to consume it.
    /// </summary>
    ///
    /// <remarks>
    /// At compile-time, the ResumableStateMachine type guides the generation of a new struct type by the F# compiler
    /// with closure-capture fields in a way similar to an object expression. 
    /// Any mention of the ResumableStateMachine type in any the 'methods' is rewritten to this
    /// fresh struct type.  The 'methods' are used to implement the interfaces on ResumableStateMachine and are also rewritten.
    /// The 'after' method is then executed and must eliminate the ResumableStateMachine. For example,
    /// its return type must not include ResumableStateMachine.
    /// </remarks>
    /// <param name="moveNextMethod">Gives the implementation of the MoveNext method on IAsyncStateMachine.</param>
    /// <param name="setStateMachineMethod">Gives the implementation of the SetStateMachine method on IAsyncStateMachine.</param>
    /// <param name="afterCode">Gives code to execute after the generation of the state machine and to produce the final result.</param>
    [<MethodImpl(MethodImplOptions.NoInlining)>]
    [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
    val __stateMachine<'Data, 'Result> :
        moveNextMethod: MoveNextMethodImpl<'Data> -> 
        setStateMachineMethod: SetStateMachineMethodImpl<'Data> -> 
        afterCode: AfterCode<'Data, 'Result> 
            -> 'Result

#endif
