// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace rec FSharp.Control.Async2

open System
open System.Threading
open System.Threading.Tasks
open FSharp.Core.CompilerServices
open System.Runtime.CompilerServices

[<NoComparison; NoEquality>]
type Async2StateMachineData<'T> =
    new: unit -> Async2StateMachineData<'T>
    [<DefaultValue(false)>]
    val mutable cancellationToken: CancellationToken
    [<DefaultValue(false)>]
    val mutable result: 'T
    [<DefaultValue(false)>]
    val mutable builder: AsyncTaskMethodBuilder<'T>
    [<DefaultValue(false)>]
    val mutable taken: bool
    //// For tailcalls using 'return!'
    //[<DefaultValue(false)>]
    //val mutable tailcallTarget: IAsync2Invocation<'T>
    
type Async2Code<'TOverall, 'T> = ResumableCode<Async2StateMachineData<'TOverall>, 'T>
and Async2StateMachine<'T> = ResumableStateMachine<Async2StateMachineData<'T>>
and Async2ResumptionFunc<'T> = ResumptionFunc<Async2StateMachineData<'T>>
and Async2ResumptionDynamicInfo<'T> = ResumptionDynamicInfo<Async2StateMachineData<'T>>
     
type IAsync2Invokable<'T> =
    abstract StartImmediate: CancellationToken -> IAsync2Invocation<'T>

type IAsync2Invocation<'T> =
    inherit IAsyncStateMachine
    //abstract TailcallTarget: IAsync2Invocation<'T>
    abstract CancellationToken: CancellationToken
    abstract Task: Task<'T>

[<AbstractClass; NoEquality; NoComparison>]
type Async2<'T> = 
    interface IAsync2Invokable<'T> 
    interface IAsync2Invocation<'T>
    interface IAsyncStateMachine
    member inline StartImmediate: ct: CancellationToken -> IAsync2Invocation<'T>

[<NoComparison; NoEquality>]
type Async2<'Machine, 'T  when 'Machine :> IAsyncStateMachine and 'Machine :> IResumableStateMachine<Async2StateMachineData<'T>>> =
    new : unit -> Async2<'Machine, 'T>
    inherit Async2<'T>
    [<DefaultValue(false)>]
    val mutable Machine: 'Machine
    interface IAsyncStateMachine 
    interface IAsync2Invokable<'T> 
    interface IAsync2Invocation<'T> 

[<Sealed>]
type Async2 =

    static member RunSynchronously: computation:Async2<'T> * ?timeout: int * ?cancellationToken:CancellationToken-> 'T
        
    static member Start: computation:Async2<unit> * ?cancellationToken:CancellationToken -> unit

    static member StartAsTask: computation:Async2<'T> * ?taskCreationOptions:TaskCreationOptions * ?cancellationToken:CancellationToken -> Task<'T>
(*
    static member StartChildAsTask: computation:Async2<'T> * ?taskCreationOptions:TaskCreationOptions -> Async2<Task<'T>>
*)

    static member Catch: computation:Async2<'T> -> Async2<Choice<'T,exn>>

    (*
    static member TryCancelled: computation:Async2<'T> * compensation:(OperationCanceledException -> unit) -> Async2<'T>

    static member OnCancel: interruption: (unit -> unit) -> Async2<System.IDisposable>
    *)
        
    static member CancellationToken: Async2<CancellationToken>

    static member CancelDefaultToken:  unit -> unit 

    static member DefaultCancellationToken: CancellationToken

    //---------- Parallelism
(*
    static member StartChild: computation:Async2<'T> * ?millisecondsTimeout: int -> Async2<Async2<'T>>
 
    static member Parallel: computations:seq<Async2<'T>> -> Async2<'T[]>

    static member Parallel: computations:seq<Async2<'T>> * ?maxDegreeOfParallelism: int -> Async2<'T[]>

    static member Sequential: computations:seq<Async2<'T>> -> Async2<'T[]>

    static member Choice: computations:seq<Async2<'T option>> -> Async2<'T option>
*)               
    static member SwitchToNewThread: unit -> Async2<unit> 
        
    static member SwitchToThreadPool:  unit -> Async2<unit> 

(*
    static member SwitchToContext:  syncContext:System.Threading.SynchronizationContext -> Async2<unit> 

    static member FromContinuations: callback:(('T -> unit) * (exn -> unit) * (OperationCanceledException -> unit) -> unit) -> Async2<'T>

    static member AwaitEvent: event:IEvent<'Del,'T> * ?cancelAction: (unit -> unit) -> Async2<'T> when 'Del: delegate<'T,unit> and 'Del :> System.Delegate 

    static member AwaitWaitHandle: waitHandle: WaitHandle * ?millisecondsTimeout:int -> Async2<bool>

    static member AwaitIAsyncResult: iar: System.IAsyncResult * ?millisecondsTimeout:int -> Async2<bool>
*)
    static member AwaitTask: task: Task<'T> -> Async2<'T>

    static member AwaitTask: task: Task -> Async2<unit>

    static member Sleep: millisecondsDueTime:int -> Async2<unit>

    static member Ignore: computation: Async2<'T> -> Async2<unit>
(*
    static member StartWithContinuations: 
        computation:Async2<'T> * 
        continuation:('T -> unit) * exceptionContinuation:(exn -> unit) * cancellationContinuation:(OperationCanceledException -> unit) *  
        ?cancellationToken:CancellationToken-> unit
*)
    static member StartImmediate: 
        computation:Async2<unit> * ?cancellationToken:CancellationToken-> unit

    static member StartImmediateAsTask: 
        computation:Async2<'T> * ?cancellationToken:CancellationToken-> Task<'T>

(*
type Async2Return

[<Struct; NoEquality; NoComparison>]
type Async2Activation<'T> =

    member IsCancellationRequested: bool

    member OnSuccess: 'T -> Async2Return

    member OnExceptionRaised: unit -> unit

    member OnCancellation: unit -> Async2Return

[<Sealed>]
module Async2Primitives =

    val MakeAsync: body:(Async2Activation<'T> -> Async2Return) -> Async2<'T>

    val Invoke: computation: Async2<'T> -> ctxt:Async2Activation<'T> -> Async2Return

    val CallThenInvoke: ctxt:Async2Activation<'T> -> result1:'U -> part2:('U -> Async2<'T>) -> Async2Return

    val Bind: ctxt:Async2Activation<'T> -> part1:Async2<'U> -> part2:('U -> Async2<'T>) -> Async2Return

    val TryFinally: ctxt:Async2Activation<'T> -> computation: Async2<'T> -> finallyFunction: (unit -> unit) -> Async2Return

    val TryWith: ctxt:Async2Activation<'T> -> computation: Async2<'T> -> catchFunction: (Exception -> Async2<'T> option) -> Async2Return

*)

[<Sealed>]
type Async2Builder =

    member inline Run: code : Async2Code<'T, 'T> -> Async2<'T>

    [<DefaultValue>]
    member inline Zero: unit -> Async2Code<'TOverall, unit> 

    member inline Combine: task1: Async2Code<'TOverall, unit> * task2: Async2Code<'TOverall, 'T> -> Async2Code<'TOverall, 'T>

    member inline While: [<InlineIfLambda>] condition: (unit -> bool) * body: Async2Code<'TOverall, unit> -> Async2Code<'TOverall, unit>

    member inline Return: v: 'T -> Async2Code<'T, 'T>

    member inline ReturnFrom: task: Task<'T> -> Async2Code<'T, 'T>

    member inline ReturnFrom: computation: Async<'T> -> Async2Code<'T, 'T>

    member inline ReturnFrom: other: Async2<'T> -> Async2Code<'T, 'T>

    member inline Delay: f: (unit -> Async2Code<'TOverall, 'T>) -> Async2Code<'TOverall, 'T>

    member inline Using: resource: ('TResource :> IAsyncDisposable) *  body: ('TResource -> Async2Code<'TOverall, 'T>) -> Async2Code<'TOverall, 'T>

    member inline TryFinally: body: Async2Code<'TOverall, 'T> * compensation: (unit -> unit) -> Async2Code<'TOverall, 'T>

    member inline TryWith: body: Async2Code<'TOverall, 'T> * catch: (exn -> Async2Code<'TOverall, 'T>) -> Async2Code<'TOverall, 'T>

    member inline Bind: task: Task<'TResult1> * continuation: ('TResult1 -> Async2Code<'TOverall, 'T>) -> Async2Code<'TOverall, 'T>

    member inline Bind: computation: Async<'TResult1> * continuation: ('TResult1 -> Async2Code<'TOverall, 'T>) -> Async2Code<'TOverall, 'T>

    member inline Bind: computation: Async2<'TResult1> * continuation: ('TResult1 -> Async2Code<'TOverall, 'T>) -> Async2Code<'TOverall, 'T>


[<AutoOpen>]
module Async2 =
    type Async2Builder with
        member inline ReturnFrom< ^TaskLike, ^Awaiter, ^T
                                            when  ^TaskLike: (member GetAwaiter:  unit ->  ^Awaiter)
                                            and ^Awaiter :> ICriticalNotifyCompletion
                                            and ^Awaiter: (member get_IsCompleted: unit -> bool)
                                            and ^Awaiter: (member GetResult: unit ->  ^T)>
                : task: ^TaskLike -> Async2Code< ^T,  ^T> 

        member inline Bind< ^TaskLike, ^TResult1, 'TResult2, ^Awaiter , 'TOverall
                                            when  ^TaskLike: (member GetAwaiter:  unit ->  ^Awaiter)
                                            and ^Awaiter :> ICriticalNotifyCompletion
                                            and ^Awaiter: (member get_IsCompleted:  unit -> bool)
                                            and ^Awaiter: (member GetResult:  unit ->  ^TResult1)>
                    : task: ^TaskLike * continuation: (^TResult1 -> Async2Code<'TOverall, 'TResult2>) -> Async2Code<'TOverall, 'TResult2>

        member inline Using: resource: ('TResource :> IDisposable) *  body: ('TResource -> Async2Code<'TOverall, 'T>) -> Async2Code<'TOverall, 'T>

        member inline For: sequence: seq<'TElement> * body: ('TElement -> Async2Code<'TOverall, unit>) -> Async2Code<'TOverall, unit>

    val async2: Async2Builder
//[<AutoOpen>]
///// <summary>A module of extension members providing asynchronous operations for some basic CLI types related to concurrency and I/O.</summary>
/////
///// <category index="1">Async Programming</category>
//module CommonExtensions =
        
//    type System.IO.Stream with 
            
//        /// <summary>Returns an asynchronous computation that will read from the stream into the given buffer.</summary>
//        /// <param name="buffer">The buffer to read into.</param>
//        /// <param name="offset">An optional offset as a number of bytes in the stream.</param>
//        /// <param name="count">An optional number of bytes to read from the stream.</param>
//        ///
//        /// <returns>An asynchronous computation that will read from the stream into the given buffer.</returns>
//        /// <exception cref="T:System.ArgumentException">Thrown when the sum of offset and count is longer than
//        /// the buffer length.</exception>
//        /// <exception cref="T:System.ArgumentOutOfRangeException">Thrown when offset or count is negative.</exception>
//        member AsyncRead: buffer:byte[] * ?offset:int * ?count:int -> Async2<int>
            
//        /// <summary>Returns an asynchronous computation that will read the given number of bytes from the stream.</summary>
//        ///
//        /// <param name="count">The number of bytes to read.</param>
//        ///
//        /// <returns>An asynchronous computation that returns the read byte[] when run.</returns> 
//        member AsyncRead: count:int -> Async2<byte[]>
            
//        /// <summary>Returns an asynchronous computation that will write the given bytes to the stream.</summary>
//        ///
//        /// <param name="buffer">The buffer to write from.</param>
//        /// <param name="offset">An optional offset as a number of bytes in the stream.</param>
//        /// <param name="count">An optional number of bytes to write to the stream.</param>
//        ///
//        /// <returns>An asynchronous computation that will write the given bytes to the stream.</returns>
//        /// <exception cref="T:System.ArgumentException">Thrown when the sum of offset and count is longer than
//        /// the buffer length.</exception>
//        /// <exception cref="T:System.ArgumentOutOfRangeException">Thrown when offset or count is negative.</exception>
//        member AsyncWrite: buffer:byte[] * ?offset:int * ?count:int -> Async2<unit>


