// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.FSharp.Control

    open System
    open System.Threading
    open System.Threading.Tasks
    open System.Runtime.ExceptionServices

    open Microsoft.FSharp.Core
    open Microsoft.FSharp.Control
    open Microsoft.FSharp.Collections

    /// <summary>
    /// An asynchronous computation, which, when run, will eventually produce a value  of type T, or else raises an exception.
    /// </summary> 
    ///
    /// <remarks>
    ///  This type has no members. Asynchronous computations are normally specified either by using an async expression
    ///  or the static methods in the <see cref="T:Microsoft.FSharp.Control.Async"/> type.
    ///
    ///  See also <a href="https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/asynchronous-workflows">F# Language Guide - Async Workflows</a>.
    /// </remarks> 
    ///
    /// <namespacedoc><summary>
    ///   Library functionality for asynchronous programming, events and agents. See also
    ///   <a href="https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/asynchronous-workflows">Asynchronous Programming</a>, 
    ///   <a href="https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/members/events">Events</a> and
    ///   <a href="https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/lazy-expressions">Lazy Expressions</a> in the
    ///   F# Language Guide.
    /// </summary></namespacedoc>
    ///
    /// <category index="1">Async Programming</category>
     
    [<Sealed; NoEquality; NoComparison; CompiledName("FSharpAsync`1")>]
    type Async<'T>

    /// <summary>Holds static members for creating and manipulating asynchronous computations.</summary>
    ///
    /// <remarks>
    ///  See also <a href="https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/asynchronous-workflows">F# Language Guide - Async Workflows</a>.
    /// </remarks>
    ///
    /// <category index="1">Async Programming</category>

    [<Sealed>]
    [<CompiledName("FSharpAsync")>]
    type Async =

        /// <summary>Runs the asynchronous computation and await its result.</summary>
        ///
        /// <remarks>If an exception occurs in the asynchronous computation then an exception is re-raised by this
        /// function.
        ///        
        /// If no cancellation token is provided then the default cancellation token is used.
        ///
        /// The timeout parameter is given in milliseconds.  A value of -1 is equivalent to
        /// System.Threading.Timeout.Infinite.</remarks>
        ///
        /// <param name="computation">The computation to run.</param>
        /// <param name="timeout">The amount of time in milliseconds to wait for the result of the
        /// computation before raising a <see cref="T:System.TimeoutException"/>.  If no value is provided
        /// for timeout then a default of -1 is used to correspond to <see cref="F:System.Threading.Timeout.Infinite"/>.
        /// If a cancellable cancellationToken is provided, timeout parameter will be ignored</param>
        /// <param name="cancellationToken">The cancellation token to be associated with the computation.
        /// If one is not supplied, the default cancellation token is used.</param>
        ///
        /// <returns>The result of the computation.</returns>
        ///
        /// <category index="0">Starting Async Computations</category>
        static member RunSynchronously : computation:Async<'T> * ?timeout : int * ?cancellationToken:CancellationToken-> 'T
        
        /// <summary>Starts the asynchronous computation in the thread pool. Do not await its result.</summary>
        ///
        /// <remarks>If no cancellation token is provided then the default cancellation token is used.</remarks>
        ///
        /// <param name="computation">The computation to run asynchronously.</param>
        /// <param name="cancellationToken">The cancellation token to be associated with the computation.
        /// If one is not supplied, the default cancellation token is used.</param>
        ///
        /// <category index="0">Starting Async Computations</category>
        static member Start : computation:Async<unit> * ?cancellationToken:CancellationToken -> unit

        /// <summary>Executes a computation in the thread pool.</summary>
        ///
        /// <remarks>If no cancellation token is provided then the default cancellation token is used.</remarks>
        ///
        /// <returns>A <see cref="T:System.Threading.Tasks.Task`1"/> that will be completed
        /// in the corresponding state once the computation terminates (produces the result, throws exception or gets canceled)</returns>
        ///
        /// <category index="0">Starting Async Computations</category>
        static member StartAsTask : computation:Async<'T> * ?taskCreationOptions:TaskCreationOptions * ?cancellationToken:CancellationToken -> Task<'T>

        /// <summary>Creates an asynchronous computation which starts the given computation as a <see cref="T:System.Threading.Tasks.Task`1"/></summary>
        ///
        /// <category index="0">Starting Async Computations</category>
        static member StartChildAsTask : computation:Async<'T> * ?taskCreationOptions:TaskCreationOptions -> Async<Task<'T>>

        /// <summary>Creates an asynchronous computation that executes <c>computation</c>.
        /// If this computation completes successfully then return <c>Choice1Of2</c> with the returned
        /// value. If this computation raises an exception before it completes then return <c>Choice2Of2</c>
        /// with the raised exception.</summary>
        ///
        /// <param name="computation">The input computation that returns the type T.</param>
        ///
        /// <returns>A computation that returns a choice of type T or exception.</returns>
        ///
        /// <category index="3">Cancellation and Exceptions</category>
        static member Catch : computation:Async<'T> -> Async<Choice<'T,exn>>

        /// <summary>Creates an asynchronous computation that executes <c>computation</c>.
        /// If this computation is cancelled before it completes then the computation generated by 
        /// running <c>compensation</c> is executed.</summary>
        ///
        /// <param name="computation">The input asynchronous computation.</param>
        /// <param name="compensation">The function to be run if the computation is cancelled.</param>
        ///
        /// <returns>An asynchronous computation that runs the compensation if the input computation
        /// is cancelled.</returns>
        ///
        /// <category index="3">Cancellation and Exceptions</category>
        static member TryCancelled : computation:Async<'T> * compensation:(OperationCanceledException -> unit) -> Async<'T>

        /// <summary>Generates a scoped, cooperative cancellation handler for use within an asynchronous workflow.</summary>
        ///
        /// <remarks>For example,
        ///     <c>async { use! holder = Async.OnCancel interruption ... }</c> 
        /// generates an asynchronous computation where, if a cancellation happens any time during 
        /// the execution of the asynchronous computation in the scope of <c>holder</c>, then action 
        /// <c>interruption</c> is executed on the thread that is performing the cancellation. This can 
        /// be used to arrange for a computation to be asynchronously notified that a cancellation 
        /// has occurred, e.g. by setting a flag, or deregistering a pending I/O action.</remarks>
        ///
        /// <param name="interruption">The function that is executed on the thread performing the
        /// cancellation.</param>
        ///
        /// <returns>An asynchronous computation that triggers the interruption if it is cancelled
        /// before being disposed.</returns>
        ///
        /// <category index="3">Cancellation and Exceptions</category>
        static member OnCancel : interruption: (unit -> unit) -> Async<System.IDisposable>
        
        /// <summary>Creates an asynchronous computation that returns the CancellationToken governing the execution 
        /// of the computation.</summary>
        ///
        /// <remarks>In <c>async { let! token = Async.CancellationToken ...}</c> token can be used to initiate other 
        /// asynchronous operations that will cancel cooperatively with this workflow.</remarks>
        ///
        /// <returns>An asynchronous computation capable of retrieving the CancellationToken from a computation
        /// expression.</returns>
        ///
        /// <category index="3">Cancellation and Exceptions</category>
        static member CancellationToken : Async<CancellationToken>

        /// <summary>Raises the cancellation condition for the most recent set of asynchronous computations started 
        /// without any specific CancellationToken. Replaces the global CancellationTokenSource with a new 
        /// global token source for any asynchronous computations created after this point without any 
        /// specific CancellationToken.</summary>
        ///
        /// <category index="3">Cancellation and Exceptions</category>
        static member CancelDefaultToken :  unit -> unit 

        /// <summary>Gets the default cancellation token for executing asynchronous computations.</summary>
        ///
        /// <returns>The default CancellationToken.</returns>
        ///
        /// <category index="3">Cancellation and Exceptions</category>
        static member DefaultCancellationToken : CancellationToken

        //---------- Parallelism

        /// <summary>Starts a child computation within an asynchronous workflow. 
        /// This allows multiple asynchronous computations to be executed simultaneously.</summary>
        /// 
        /// <remarks>This method should normally be used as the immediate 
        /// right-hand-side of a <c>let!</c> binding in an F# asynchronous workflow, that is,
        /// <code>
        ///        async { ...
        ///                let! completor1 = childComputation1 |> Async.StartChild  
        ///                let! completor2 = childComputation2 |> Async.StartChild  
        ///                ... 
        ///                let! result1 = completor1 
        ///                let! result2 = completor2 
        ///                ... }
        /// </code>
        /// 
        /// When used in this way, each use of <c>StartChild</c> starts an instance of <c>childComputation</c> 
        /// and returns a completor object representing a computation to wait for the completion of the operation.
        /// When executed, the completor awaits the completion of <c>childComputation</c>.</remarks>
        ///
        /// <param name="computation">The child computation.</param>
        /// <param name="millisecondsTimeout">The timeout value in milliseconds.  If one is not provided
        /// then the default value of -1 corresponding to <see cref="F:System.Threading.Timeout.Infinite"/>.</param>
        ///
        /// <returns>A new computation that waits for the input computation to finish.</returns>
        ///
        /// <category index="3">Cancellation and Exceptions</category>
        static member StartChild : computation:Async<'T> * ?millisecondsTimeout : int -> Async<Async<'T>>
                
        /// <summary>Creates an asynchronous computation that executes all the given asynchronous computations, 
        /// initially queueing each as work items and using a fork/join pattern.</summary>
        ///
        /// <remarks>If all child computations succeed, an array of results is passed to the success continuation.
        /// 
        /// If any child computation raises an exception, then the overall computation will trigger an 
        /// exception, and cancel the others. 
        ///
        /// The overall computation will respond to cancellation while executing the child computations.
        /// If cancelled, the computation will cancel any remaining child computations but will still wait
        /// for the other child computations to complete.</remarks>
        ///
        /// <param name="computations">A sequence of distinct computations to be parallelized.</param>
        ///
        /// <returns>A computation that returns an array of values from the sequence of input computations.</returns>
        ///
        /// <category index="1">Composing Async Computations</category>
        static member Parallel : computations:seq<Async<'T>> -> Async<'T[]>

        /// <summary>Creates an asynchronous computation that executes all the given asynchronous computations,
        /// initially queueing each as work items and using a fork/join pattern.</summary>
        ///
        /// <remarks>If all child computations succeed, an array of results is passed to the success continuation.
        ///
        /// If any child computation raises an exception, then the overall computation will trigger an
        /// exception, and cancel the others.
        ///
        /// The overall computation will respond to cancellation while executing the child computations.
        /// If cancelled, the computation will cancel any remaining child computations but will still wait
        /// for the other child computations to complete.</remarks>
        ///
        /// <param name="computations">A sequence of distinct computations to be parallelized.</param>
        /// <param name="maxDegreeOfParallelism">The maximum degree of parallelism in the parallel execution.</param>
        ///
        /// <returns>A computation that returns an array of values from the sequence of input computations.</returns>
        ///
        /// <category index="1">Composing Async Computations</category>
        static member Parallel : computations:seq<Async<'T>> * ?maxDegreeOfParallelism : int -> Async<'T[]>

        /// <summary>Creates an asynchronous computation that executes all the given asynchronous computations sequentially.</summary>
        ///
        /// <remarks>If all child computations succeed, an array of results is passed to the success continuation.
        ///
        /// If any child computation raises an exception, then the overall computation will trigger an
        /// exception, and cancel the others.
        ///
        /// The overall computation will respond to cancellation while executing the child computations.
        /// If cancelled, the computation will cancel any remaining child computations but will still wait
        /// for the other child computations to complete.</remarks>
        ///
        /// <param name="computations">A sequence of distinct computations to be run in sequence.</param>
        ///
        /// <returns>A computation that returns an array of values from the sequence of input computations.</returns>
        ///
        /// <category index="1">Composing Async Computations</category>
        static member Sequential : computations:seq<Async<'T>> -> Async<'T[]>

        /// <summary>Creates an asynchronous computation that executes all given asynchronous computations in parallel, 
        /// returning the result of the first succeeding computation (one whose result is 'Some x').
        /// If all child computations complete with None, the parent computation also returns None.</summary>
        ///
        /// <remarks>
        /// If any child computation raises an exception, then the overall computation will trigger an 
        /// exception, and cancel the others. 
        ///
        /// The overall computation will respond to cancellation while executing the child computations.
        /// If cancelled, the computation will cancel any remaining child computations but will still wait
        /// for the other child computations to complete.</remarks>
        ///
        /// <param name="computations">A sequence of computations to be parallelized.</param>
        ///
        /// <returns>A computation that returns the first succeeding computation.</returns>
        ///
        /// <category index="1">Composing Async Computations</category>
        static member Choice : computations:seq<Async<'T option>> -> Async<'T option>

        //---------- Thread Control
        
        /// <summary>Creates an asynchronous computation that creates a new thread and runs
        /// its continuation in that thread.</summary>
        ///
        /// <returns>A computation that will execute on a new thread.</returns>
        ///
        /// <category index="4">Threads and Contexts</category>
        static member SwitchToNewThread : unit -> Async<unit> 
        
        /// <summary>Creates an asynchronous computation that queues a work item that runs
        /// its continuation.</summary>
        ///
        /// <returns>A computation that generates a new work item in the thread pool.</returns>
        ///
        /// <category index="4">Threads and Contexts</category>
        static member SwitchToThreadPool :  unit -> Async<unit> 

        /// <summary>Creates an asynchronous computation that runs
        /// its continuation using syncContext.Post. If syncContext is null 
        /// then the asynchronous computation is equivalent to SwitchToThreadPool().</summary>
        ///
        /// <param name="syncContext">The synchronization context to accept the posted computation.</param>
        ///
        /// <returns>An asynchronous computation that uses the syncContext context to execute.</returns>
        ///
        /// <category index="4">Threads and Contexts</category>
        static member SwitchToContext :  syncContext:System.Threading.SynchronizationContext -> Async<unit> 

        /// <summary>Creates an asynchronous computation that captures the current
        /// success, exception and cancellation continuations. The callback must 
        /// eventually call exactly one of the given continuations.</summary>
        ///
        /// <param name="callback">The function that accepts the current success, exception, and cancellation
        /// continuations.</param>
        ///
        /// <returns>An asynchronous computation that provides the callback with the current continuations.</returns>
        ///
        /// <category index="1">Composing Async Computations</category>
        static member FromContinuations : callback:(('T -> unit) * (exn -> unit) * (OperationCanceledException -> unit) -> unit) -> Async<'T>

        /// <summary>Creates an asynchronous computation that waits for a single invocation of a CLI 
        /// event by adding a handler to the event. Once the computation completes or is 
        /// cancelled, the handler is removed from the event.</summary>
        ///
        /// <remarks>The computation will respond to cancellation while waiting for the event. If a 
        /// cancellation occurs, and <c>cancelAction</c> is specified, then it is executed, and 
        /// the computation continues to wait for the event.
        /// 
        /// If <c>cancelAction</c> is not specified, then cancellation causes the computation
        /// to cancel immediately.</remarks>
        ///
        /// <param name="event">The event to handle once.</param>
        /// <param name="cancelAction">An optional function to execute instead of cancelling when a
        /// cancellation is issued.</param>
        ///
        /// <returns>An asynchronous computation that waits for the event to be invoked.</returns>
        ///
        /// <category index="2">Awaiting Results</category>
        static member AwaitEvent: event:IEvent<'Del,'T> * ?cancelAction : (unit -> unit) -> Async<'T> when 'Del : delegate<'T,unit> and 'Del :> System.Delegate 

        /// <summary>Creates an asynchronous computation that will wait on the given WaitHandle.</summary>
        ///
        /// <remarks>The computation returns true if the handle indicated a result within the given timeout.</remarks>
        ///
        /// <param name="waitHandle">The <c>WaitHandle</c> that can be signalled.</param>
        /// <param name="millisecondsTimeout">The timeout value in milliseconds.  If one is not provided
        /// then the default value of -1 corresponding to <see cref="F:System.Threading.Timeout.Infinite"/>.</param>
        ///
        /// <returns>An asynchronous computation that waits on the given <c>WaitHandle</c>.</returns>
        ///
        /// <category index="2">Awaiting Results</category>
        static member AwaitWaitHandle: waitHandle: WaitHandle * ?millisecondsTimeout:int -> Async<bool>

        /// <summary>Creates an asynchronous computation that will wait on the IAsyncResult.</summary>
        ///
        /// <remarks>The computation returns true if the handle indicated a result within the given timeout.</remarks>
        ///
        /// <param name="iar">The IAsyncResult to wait on.</param>
        /// <param name="millisecondsTimeout">The timeout value in milliseconds.  If one is not provided
        /// then the default value of -1 corresponding to <see cref="F:System.Threading.Timeout.Infinite"/>.</param>
        ///
        /// <returns>An asynchronous computation that waits on the given <c>IAsyncResult</c>.</returns>
        ///
        /// <category index="2">Awaiting Results</category>
        static member AwaitIAsyncResult: iar: System.IAsyncResult * ?millisecondsTimeout:int -> Async<bool>

        /// <summary>Return an asynchronous computation that will wait for the given task to complete and return
        /// its result.</summary>
        ///
        /// <param name="task">The task to await.</param>
        ///
        /// <remarks>If an exception occurs in the asynchronous computation then an exception is re-raised by this
        /// function.
        ///        
        /// If the task is cancelled then <see cref="F:System.Threading.Tasks.TaskCanceledException"/> is raised. Note
        /// that the task may be governed by a different cancellation token to the overall async computation
        /// where the AwaitTask occurs. In practice you should normally start the task with the
        /// cancellation token returned by <c>let! ct = Async.CancellationToken</c>, and catch
        /// any <see cref="F:System.Threading.Tasks.TaskCanceledException"/> at the point where the
        /// overall async is started.
        /// </remarks>
        ///
        /// <category index="2">Awaiting Results</category>
        static member AwaitTask: task: Task<'T> -> Async<'T>

        /// <summary>Return an asynchronous computation that will wait for the given task to complete and return
        /// its result.</summary>
        ///
        /// <param name="task">The task to await.</param>
        ///
        /// <remarks>If an exception occurs in the asynchronous computation then an exception is re-raised by this
        /// function.
        ///        
        /// If the task is cancelled then <see cref="F:System.Threading.Tasks.TaskCanceledException"/> is raised. Note
        /// that the task may be governed by a different cancellation token to the overall async computation
        /// where the AwaitTask occurs. In practice you should normally start the task with the
        /// cancellation token returned by <c>let! ct = Async.CancellationToken</c>, and catch
        /// any <see cref="F:System.Threading.Tasks.TaskCanceledException"/> at the point where the
        /// overall async is started.
        /// </remarks>
        ///
        /// <category index="2">Awaiting Results</category>
        static member AwaitTask: task: Task -> Async<unit>

        /// <summary>
        ///  Creates an asynchronous computation that will sleep for the given time. This is scheduled
        ///  using a System.Threading.Timer object. The operation will not block operating system threads
        ///  for the duration of the wait.
        /// </summary>
        ///
        /// <param name="millisecondsDueTime">The number of milliseconds to sleep.</param>
        ///
        /// <returns>An asynchronous computation that will sleep for the given time.</returns>
        ///
        /// <exception cref="T:System.ArgumentOutOfRangeException">Thrown when the due time is negative
        /// and not infinite.</exception>
        ///
        /// <category index="2">Awaiting Results</category>
        static member Sleep: millisecondsDueTime:int -> Async<unit>

        /// <summary>
        ///  Creates an asynchronous computation that will sleep for the given time. This is scheduled
        ///  using a System.Threading.Timer object. The operation will not block operating system threads
        ///  for the duration of the wait.
        /// </summary>
        ///
        /// <param name="dueTime">The amount of time to sleep.</param>
        ///
        /// <returns>An asynchronous computation that will sleep for the given time.</returns>
        ///
        /// <exception cref="T:System.ArgumentOutOfRangeException">Thrown when the due time is negative.</exception>
        ///
        /// <category index="2">Awaiting Results</category>
        static member Sleep: dueTime:TimeSpan -> Async<unit>

        /// <summary>
        ///  Creates an asynchronous computation in terms of a Begin/End pair of actions in 
        ///  the style used in CLI APIs.
        /// </summary>
        ///
        /// <remarks>
        /// The computation will respond to cancellation while waiting for the completion
        /// of the operation. If a cancellation occurs, and <c>cancelAction</c> is specified, then it is 
        /// executed, and the computation continues to wait for the completion of the operation.
        /// 
        /// If <c>cancelAction</c> is not specified, then cancellation causes the computation
        /// to stop immediately, and subsequent invocations of the callback are ignored.</remarks>
        ///
        /// <param name="beginAction">The function initiating a traditional CLI asynchronous operation.</param>
        /// <param name="endAction">The function completing a traditional CLI asynchronous operation.</param>
        /// <param name="cancelAction">An optional function to be executed when a cancellation is requested.</param>
        ///
        /// <returns>An asynchronous computation wrapping the given Begin/End functions.</returns>
        ///
        /// <category index="5">Legacy .NET Async Interoperability</category>
        static member FromBeginEnd : beginAction:(System.AsyncCallback * obj -> System.IAsyncResult) * endAction:(System.IAsyncResult -> 'T) * ?cancelAction : (unit -> unit) -> Async<'T>

        /// <summary>
        ///  Creates an asynchronous computation in terms of a Begin/End pair of actions in 
        ///  the style used in .NET 2.0 APIs.
        /// </summary>
        ///
        /// <remarks>The computation will respond to cancellation while waiting for the completion
        /// of the operation. If a cancellation occurs, and <c>cancelAction</c> is specified, then it is 
        /// executed, and the computation continues to wait for the completion of the operation.
        ///
        ///  If <c>cancelAction</c> is not specified, then cancellation causes the computation
        ///  to stop immediately, and subsequent invocations of the callback are ignored.
        ///</remarks>
        ///
        /// <param name="arg">The argument for the operation.</param>
        /// <param name="beginAction">The function initiating a traditional CLI asynchronous operation.</param>
        /// <param name="endAction">The function completing a traditional CLI asynchronous operation.</param>
        /// <param name="cancelAction">An optional function to be executed when a cancellation is requested.</param>
        ///
        /// <returns>An asynchronous computation wrapping the given Begin/End functions.</returns>
        ///
        /// <category index="5">Legacy .NET Async Interoperability</category>
        static member FromBeginEnd : arg:'Arg1 * beginAction:('Arg1 * System.AsyncCallback * obj -> System.IAsyncResult) * endAction:(System.IAsyncResult -> 'T) * ?cancelAction : (unit -> unit) -> Async<'T>

        /// <summary>
        /// Creates an asynchronous computation in terms of a Begin/End pair of actions in 
        /// the style used in .NET 2.0 APIs.</summary>
        ///
        /// <remarks>The computation will respond to cancellation while waiting for the completion
        /// of the operation. If a cancellation occurs, and <c>cancelAction</c> is specified, then it is 
        /// executed, and the computation continues to wait for the completion of the operation.
        /// 
        /// If <c>cancelAction</c> is not specified, then cancellation causes the computation
        /// to stop immediately, and subsequent invocations of the callback are ignored.</remarks>
        ///
        /// <param name="arg1">The first argument for the operation.</param>
        /// <param name="arg2">The second argument for the operation.</param>
        /// <param name="beginAction">The function initiating a traditional CLI asynchronous operation.</param>
        /// <param name="endAction">The function completing a traditional CLI asynchronous operation.</param>
        /// <param name="cancelAction">An optional function to be executed when a cancellation is requested.</param>
        ///
        /// <returns>An asynchronous computation wrapping the given Begin/End functions.</returns>
        ///
        /// <category index="5">Legacy .NET Async Interoperability</category>
        static member FromBeginEnd : arg1:'Arg1 * arg2:'Arg2 * beginAction:('Arg1 * 'Arg2 * System.AsyncCallback * obj -> System.IAsyncResult) * endAction:(System.IAsyncResult -> 'T) * ?cancelAction : (unit -> unit) -> Async<'T>

        /// <summary>Creates an asynchronous computation in terms of a Begin/End pair of actions in 
        /// the style used in .NET 2.0 APIs.</summary>
        ///
        /// <remarks>The computation will respond to cancellation while waiting for the completion
        /// of the operation. If a cancellation occurs, and <c>cancelAction</c> is specified, then it is 
        /// executed, and the computation continues to wait for the completion of the operation.
        /// 
        /// If <c>cancelAction</c> is not specified, then cancellation causes the computation
        /// to stop immediately, and subsequent invocations of the callback are ignored.</remarks>
        ///
        /// <param name="arg1">The first argument for the operation.</param>
        /// <param name="arg2">The second argument for the operation.</param>
        /// <param name="arg3">The third argument for the operation.</param>
        /// <param name="beginAction">The function initiating a traditional CLI asynchronous operation.</param>
        /// <param name="endAction">The function completing a traditional CLI asynchronous operation.</param>
        /// <param name="cancelAction">An optional function to be executed when a cancellation is requested.</param>
        ///
        /// <returns>An asynchronous computation wrapping the given Begin/End functions.</returns>
        ///
        /// <category index="5">Legacy .NET Async Interoperability</category>
        static member FromBeginEnd : arg1:'Arg1 * arg2:'Arg2 * arg3:'Arg3 * beginAction:('Arg1 * 'Arg2 * 'Arg3 * System.AsyncCallback * obj -> System.IAsyncResult) * endAction:(System.IAsyncResult -> 'T) * ?cancelAction : (unit -> unit) -> Async<'T>

        /// <summary>Creates three functions that can be used to implement the .NET 1.0 Asynchronous 
        /// Programming Model (APM) for a given asynchronous computation.</summary>
        ///
        /// <param name="computation">A function generating the asynchronous computation to split into the traditional
        /// .NET Asynchronous Programming Model.</param>
        ///
        /// <returns>A tuple of the begin, end, and cancel members.</returns>
        ///
        /// <category index="5">Legacy .NET Async Interoperability</category>
        static member AsBeginEnd : computation:('Arg -> Async<'T>) -> 
                                     // The 'Begin' member
                                     ('Arg * System.AsyncCallback * obj -> System.IAsyncResult) * 
                                     // The 'End' member
                                     (System.IAsyncResult -> 'T) * 
                                     // The 'Cancel' member
                                     (System.IAsyncResult -> unit)

        /// <summary>Creates an asynchronous computation that runs the given computation and ignores 
        /// its result.</summary>
        ///
        /// <param name="computation">The input computation.</param>
        ///
        /// <returns>A computation that is equivalent to the input computation, but disregards the result.</returns>
        ///
        /// <category index="1">Composing Async Computations</category>
        static member Ignore : computation: Async<'T> -> Async<unit>

        /// <summary>Runs an asynchronous computation, starting immediately on the current operating system
        /// thread. Call one of the three continuations when the operation completes.</summary>
        ///
        /// <remarks>If no cancellation token is provided then the default cancellation token
        /// is used.</remarks>
        ///
        /// <param name="computation">The asynchronous computation to execute.</param>
        /// <param name="continuation">The function called on success.</param>
        /// <param name="exceptionContinuation">The function called on exception.</param>
        /// <param name="cancellationContinuation">The function called on cancellation.</param>
        /// <param name="cancellationToken">The <c>CancellationToken</c> to associate with the computation.
        /// The default is used if this parameter is not provided.</param>
        ///
        /// <category index="0">Starting Async Computations</category>
        static member StartWithContinuations: 
            computation:Async<'T> * 
            continuation:('T -> unit) * exceptionContinuation:(exn -> unit) * cancellationContinuation:(OperationCanceledException -> unit) *  
            ?cancellationToken:CancellationToken-> unit

        static member internal StartWithContinuationsUsingDispatchInfo: 
            computation:Async<'T> * 
            continuation:('T -> unit) * exceptionContinuation:(ExceptionDispatchInfo -> unit) * cancellationContinuation:(OperationCanceledException -> unit) *  
            ?cancellationToken:CancellationToken-> unit

        /// <summary>Runs an asynchronous computation, starting immediately on the current operating system
        /// thread.</summary>
        ///
        /// <remarks>If no cancellation token is provided then the default cancellation token is used.</remarks>
        ///
        /// <param name="computation">The asynchronous computation to execute.</param>
        /// <param name="cancellationToken">The <c>CancellationToken</c> to associate with the computation.
        /// The default is used if this parameter is not provided.</param>
        ///
        /// <category index="0">Starting Async Computations</category>
        static member StartImmediate: 
            computation:Async<unit> * ?cancellationToken:CancellationToken-> unit

        /// <summary>Runs an asynchronous computation, starting immediately on the current operating system
        /// thread, but also returns the execution as <see cref="T:System.Threading.Tasks.Task`1"/>
        /// </summary>
        ///
        /// <remarks>If no cancellation token is provided then the default cancellation token is used.
        /// You may prefer using this method if you want to achive a similar behviour to async await in C# as 
        /// async computation starts on the current thread with an ability to return a result.
        /// </remarks>
        ///
        /// <param name="computation">The asynchronous computation to execute.</param>
        /// <param name="cancellationToken">The <c>CancellationToken</c> to associate with the computation.
        /// The default is used if this parameter is not provided.</param>
        ///
        /// <returns>A <see cref="T:System.Threading.Tasks.Task"/> that will be completed
        /// in the corresponding state once the computation terminates (produces the result, throws exception or gets canceled)</returns>
        ///
        /// <category index="0">Starting Async Computations</category>
        static member StartImmediateAsTask: 
            computation:Async<'T> * ?cancellationToken:CancellationToken-> Task<'T>


    /// <summary>The F# compiler emits references to this type to implement F# async expressions.</summary>
    ///
    /// <category index="5">Async Internals</category>
    type AsyncReturn

    /// <summary>The F# compiler emits references to this type to implement F# async expressions.</summary>
    [<Struct; NoEquality; NoComparison>]
    ///
    /// <category index="5">Async Internals</category>
    type AsyncActivation<'T> =

        /// <summary>The F# compiler emits calls to this function to implement F# async expressions.</summary>
        ///
        /// <returns>A value indicating asynchronous execution.</returns>
        member IsCancellationRequested: bool

        /// <summary>The F# compiler emits calls to this function to implement F# async expressions.</summary>
        ///
        /// <returns>A value indicating asynchronous execution.</returns>
        static member Success: AsyncActivation<'T> -> result: 'T -> AsyncReturn

        /// <summary>The F# compiler emits calls to this function to implement F# async expressions.</summary>
        ///
        /// <returns>A value indicating asynchronous execution.</returns>
        member OnSuccess: result: 'T -> AsyncReturn

        /// <summary>The F# compiler emits calls to this function to implement F# async expressions.</summary>
        member OnExceptionRaised: unit -> unit

        /// <summary>The F# compiler emits calls to this function to implement F# async expressions.</summary>
        ///
        /// <returns>A value indicating asynchronous execution.</returns>
        member OnCancellation: unit -> AsyncReturn

        /// Used by MailboxProcessor
        member internal QueueContinuationWithTrampoline: 'T -> AsyncReturn
        /// Used by MailboxProcessor
        member internal CallContinuation: 'T -> AsyncReturn

    [<NoEquality; NoComparison>]
    // Internals used by MailboxProcessor
    type internal AsyncResult<'T>  =
        | Ok of 'T
        | Error of ExceptionDispatchInfo
        | Canceled of OperationCanceledException

    [<Sealed>]
    /// <summary>Entry points for generated code</summary>
    ///
    /// <category index="5">Async Internals</category>
    module AsyncPrimitives =

        /// <summary>The F# compiler emits calls to this function to implement F# async expressions.</summary>
        ///
        /// <param name="body">The body of the async computation.</param>
        ///
        /// <returns>The async computation.</returns>
        val MakeAsync: body:(AsyncActivation<'T> -> AsyncReturn) -> Async<'T>

        /// <summary>The F# compiler emits calls to this function to implement constructs for F# async expressions.</summary>
        ///
        /// <param name="computation">The async computation.</param>
        /// <param name="ctxt">The async activation.</param>
        ///
        /// <returns>A value indicating asynchronous execution.</returns>
        val Invoke: computation: Async<'T> -> ctxt:AsyncActivation<'T> -> AsyncReturn

        /// <summary>The F# compiler emits calls to this function to implement constructs for F# async expressions.</summary>
        ///
        /// <param name="ctxt">The async activation.</param>
        /// <param name="result1">The result of the first part of the computation.</param>
        /// <param name="part2">A function returning the second part of the computation.</param>
        ///
        /// <returns>A value indicating asynchronous execution.</returns>
        val CallThenInvoke: ctxt:AsyncActivation<'T> -> result1:'U -> part2:('U -> Async<'T>) -> AsyncReturn

        /// <summary>The F# compiler emits calls to this function to implement the <c>let!</c> construct for F# async expressions.</summary>
        ///
        /// <param name="ctxt">The async activation.</param>
        /// <param name="part1">The first part of the computation.</param>
        /// <param name="part2">A function returning the second part of the computation.</param>
        ///
        /// <returns>An async activation suitable for running part1 of the asynchronous execution.</returns>
        val Bind: ctxt:AsyncActivation<'T> -> part1:Async<'U> -> part2:('U -> Async<'T>) -> AsyncReturn

        /// <summary>The F# compiler emits calls to this function to implement the <c>try/finally</c> construct for F# async expressions.</summary>
        ///
        /// <param name="ctxt">The async activation.</param>
        /// <param name="computation">The computation to protect.</param>
        /// <param name="finallyFunction">The finally code.</param>
        ///
        /// <returns>A value indicating asynchronous execution.</returns>
        val TryFinally: ctxt:AsyncActivation<'T> -> computation: Async<'T> -> finallyFunction: (unit -> unit) -> AsyncReturn

        /// <summary>The F# compiler emits calls to this function to implement the <c>try/with</c> construct for F# async expressions.</summary>
        ///
        /// <param name="ctxt">The async activation.</param>
        /// <param name="computation">The computation to protect.</param>
        /// <param name="catchFunction">The exception filter.</param>
        ///
        /// <returns>A value indicating asynchronous execution.</returns>
        val TryWith: ctxt:AsyncActivation<'T> -> computation: Async<'T> -> catchFunction: (Exception -> Async<'T> option) -> AsyncReturn

        [<Sealed; AutoSerializable(false)>]        
        // Internals used by MailboxProcessor
        type internal ResultCell<'T> =
            new : unit -> ResultCell<'T>
            member GetWaitHandle: unit -> WaitHandle
            member Close: unit -> unit
            interface IDisposable
            member RegisterResult: 'T * reuseThread: bool -> AsyncReturn
            member GrabResult: unit -> 'T
            member ResultAvailable : bool
            member AwaitResult_NoDirectCancelOrTimeout : Async<'T>
            member TryWaitForResultSynchronously: ?timeout: int -> 'T option

        // Internals used by MailboxProcessor
        val internal CreateAsyncResultAsync : AsyncResult<'T> -> Async<'T>

    [<CompiledName("FSharpAsyncBuilder")>]
    [<Sealed>]
    /// <summary>The type of the <c>async</c> operator, used to build workflows for asynchronous computations.</summary>
    ///
    /// <category index="1">Async Programming</category>
    type AsyncBuilder =
        /// <summary>Creates an asynchronous computation that enumerates the sequence <c>seq</c>
        /// on demand and runs <c>body</c> for each element.</summary>
        ///
        /// <remarks>A cancellation check is performed on each iteration of the loop.
        ///
        /// The existence of this method permits the use of <c>for</c> in the 
        /// <c>async { ... }</c> computation expression syntax.</remarks>
        ///
        /// <param name="sequence">The sequence to enumerate.</param>
        /// <param name="body">A function to take an item from the sequence and create
        /// an asynchronous computation.  Can be seen as the body of the <c>for</c> expression.</param>
        ///
        /// <returns>An asynchronous computation that will enumerate the sequence and run <c>body</c>
        /// for each element.</returns>
        member For: sequence:seq<'T> * body:('T -> Async<unit>) -> Async<unit>

        /// <summary>Creates an asynchronous computation that just returns <c>()</c>.</summary>
        ///
        /// <remarks>A cancellation check is performed when the computation is executed.
        ///
        /// The existence of this method permits the use of empty <c>else</c> branches in the 
        /// <c>async { ... }</c> computation expression syntax.</remarks>
        /// <returns>An asynchronous computation that returns <c>()</c>.</returns>
        member Zero : unit -> Async<unit> 

        /// <summary>Creates an asynchronous computation that first runs <c>computation1</c>
        /// and then runs <c>computation2</c>, returning the result of <c>computation2</c>.</summary>
        ///
        /// <remarks>A cancellation check is performed when the computation is executed.
        ///
        /// The existence of this method permits the use of expression sequencing in the 
        /// <c>async { ... }</c> computation expression syntax.</remarks>
        ///
        /// <param name="computation1">The first part of the sequenced computation.</param>
        /// <param name="computation2">The second part of the sequenced computation.</param>
        ///
        /// <returns>An asynchronous computation that runs both of the computations sequentially.</returns>
        member inline Combine : computation1:Async<unit> * computation2:Async<'T> -> Async<'T>

        /// <summary>Creates an asynchronous computation that runs <c>computation</c> repeatedly 
        /// until <c>guard()</c> becomes false.</summary>
        ///
        /// <remarks>A cancellation check is performed whenever the computation is executed.
        ///
        /// The existence of this method permits the use of <c>while</c> in the 
        /// <c>async { ... }</c> computation expression syntax.</remarks>
        ///
        /// <param name="guard">The function to determine when to stop executing <c>computation</c>.</param>
        /// <param name="computation">The function to be executed.  Equivalent to the body
        /// of a <c>while</c> expression.</param>
        ///
        /// <returns>An asynchronous computation that behaves similarly to a while loop when run.</returns>
        member While : guard:(unit -> bool) * computation:Async<unit> -> Async<unit>

        /// <summary>Creates an asynchronous computation that returns the result <c>v</c>.</summary>
        ///
        /// <remarks>A cancellation check is performed when the computation is executed.
        ///
        /// The existence of this method permits the use of <c>return</c> in the 
        /// <c>async { ... }</c> computation expression syntax.</remarks>
        ///
        /// <param name="value">The value to return from the computation.</param>
        ///
        /// <returns>An asynchronous computation that returns <c>value</c> when executed.</returns>
        member inline Return : value:'T -> Async<'T>

        /// <summary>Delegates to the input computation.</summary>
        ///
        /// <remarks>The existence of this method permits the use of <c>return!</c> in the 
        /// <c>async { ... }</c> computation expression syntax.</remarks>
        ///
        /// <param name="computation">The input computation.</param>
        ///
        /// <returns>The input computation.</returns>
        member inline ReturnFrom : computation:Async<'T> -> Async<'T>

        /// <summary>Creates an asynchronous computation that runs <c>generator</c>.</summary>
        ///
        /// <remarks>A cancellation check is performed when the computation is executed.</remarks>
        ///
        /// <param name="generator">The function to run.</param>
        ///
        /// <returns>An asynchronous computation that runs <c>generator</c>.</returns>
        member Delay : generator:(unit -> Async<'T>) -> Async<'T>

        /// <summary>Creates an asynchronous computation that runs <c>binder(resource)</c>. 
        /// The action <c>resource.Dispose()</c> is executed as this computation yields its result
        /// or if the asynchronous computation exits by an exception or by cancellation.</summary>
        ///
        /// <remarks>A cancellation check is performed when the computation is executed.
        ///
        /// The existence of this method permits the use of <c>use</c> and <c>use!</c> in the 
        /// <c>async { ... }</c> computation expression syntax.</remarks>
        ///
        /// <param name="resource">The resource to be used and disposed.</param>
        /// <param name="binder">The function that takes the resource and returns an asynchronous
        /// computation.</param>
        ///
        /// <returns>An asynchronous computation that binds and eventually disposes <c>resource</c>.</returns>
        member Using: resource:'T * binder:('T -> Async<'U>) -> Async<'U> when 'T :> System.IDisposable

        /// <summary>Creates an asynchronous computation that runs <c>computation</c>, and when 
        /// <c>computation</c> generates a result <c>T</c>, runs <c>binder res</c>.</summary>
        ///
        /// <remarks>A cancellation check is performed when the computation is executed.
        ///
        /// The existence of this method permits the use of <c>let!</c> in the 
        /// <c>async { ... }</c> computation expression syntax.</remarks>
        ///
        /// <param name="computation">The computation to provide an unbound result.</param>
        /// <param name="binder">The function to bind the result of <c>computation</c>.</param>
        ///
        /// <returns>An asynchronous computation that performs a monadic bind on the result
        /// of <c>computation</c>.</returns>
        member inline Bind: computation: Async<'T> * binder: ('T -> Async<'U>) -> Async<'U>
        
        /// <summary>Creates an asynchronous computation that runs <c>computation</c>. The action <c>compensation</c> is executed 
        /// after <c>computation</c> completes, whether <c>computation</c> exits normally or by an exception. If <c>compensation</c> raises an exception itself
        /// the original exception is discarded and the new exception becomes the overall result of the computation.</summary>
        ///
        /// <remarks>A cancellation check is performed when the computation is executed.
        ///
        /// The existence of this method permits the use of <c>try/finally</c> in the 
        /// <c>async { ... }</c> computation expression syntax.</remarks>
        ///
        /// <param name="computation">The input computation.</param>
        /// <param name="compensation">The action to be run after <c>computation</c> completes or raises an
        /// exception (including cancellation).</param>
        ///
        /// <returns>An asynchronous computation that executes computation and compensation afterwards or
        /// when an exception is raised.</returns>
        member inline TryFinally : computation:Async<'T> * compensation:(unit -> unit) -> Async<'T>

        /// <summary>Creates an asynchronous computation that runs <c>computation</c> and returns its result.
        /// If an exception happens then <c>catchHandler(exn)</c> is called and the resulting computation executed instead.</summary>
        ///
        /// <remarks>A cancellation check is performed when the computation is executed.
        ///
        /// The existence of this method permits the use of <c>try/with</c> in the 
        /// <c>async { ... }</c> computation expression syntax.</remarks>
        ///
        /// <param name="computation">The input computation.</param>
        /// <param name="catchHandler">The function to run when <c>computation</c> throws an exception.</param>
        ///
        /// <returns>An asynchronous computation that executes <c>computation</c> and calls <c>catchHandler</c> if an
        /// exception is thrown.</returns>
        member inline TryWith : computation:Async<'T> * catchHandler:(exn -> Async<'T>) -> Async<'T>

        // member inline TryWithFilter : computation:Async<'T> * catchHandler:(exn -> Async<'T> option) -> Async<'T>

        /// Generate an object used to build asynchronous computations using F# computation expressions. The value
        /// 'async' is a pre-defined instance of this type.
        ///
        /// A cancellation check is performed when the computation is executed.
        internal new : unit -> AsyncBuilder

    [<AutoOpen>]
    /// <summary>A module of extension members providing asynchronous operations for some basic CLI types related to concurrency and I/O.</summary>
    ///
    /// <category index="1">Async Programming</category>
    module CommonExtensions =
        
        type System.IO.Stream with 
            
            /// <summary>Returns an asynchronous computation that will read from the stream into the given buffer.</summary>
            /// <param name="buffer">The buffer to read into.</param>
            /// <param name="offset">An optional offset as a number of bytes in the stream.</param>
            /// <param name="count">An optional number of bytes to read from the stream.</param>
            ///
            /// <returns>An asynchronous computation that will read from the stream into the given buffer.</returns>
            /// <exception cref="T:System.ArgumentException">Thrown when the sum of offset and count is longer than
            /// the buffer length.</exception>
            /// <exception cref="T:System.ArgumentOutOfRangeException">Thrown when offset or count is negative.</exception>
            [<CompiledName("AsyncRead")>] // give the extension member a nice, unmangled compiled name, unique within this module
            member AsyncRead : buffer:byte[] * ?offset:int * ?count:int -> Async<int>
            
            /// <summary>Returns an asynchronous computation that will read the given number of bytes from the stream.</summary>
            ///
            /// <param name="count">The number of bytes to read.</param>
            ///
            /// <returns>An asynchronous computation that returns the read byte[] when run.</returns> 
            [<CompiledName("AsyncReadBytes")>] // give the extension member a nice, unmangled compiled name, unique within this module
            member AsyncRead : count:int -> Async<byte[]>
            
            /// <summary>Returns an asynchronous computation that will write the given bytes to the stream.</summary>
            ///
            /// <param name="buffer">The buffer to write from.</param>
            /// <param name="offset">An optional offset as a number of bytes in the stream.</param>
            /// <param name="count">An optional number of bytes to write to the stream.</param>
            ///
            /// <returns>An asynchronous computation that will write the given bytes to the stream.</returns>
            /// <exception cref="T:System.ArgumentException">Thrown when the sum of offset and count is longer than
            /// the buffer length.</exception>
            /// <exception cref="T:System.ArgumentOutOfRangeException">Thrown when offset or count is negative.</exception>
            [<CompiledName("AsyncWrite")>] // give the extension member a nice, unmangled compiled name, unique within this module
            member AsyncWrite : buffer:byte[] * ?offset:int * ?count:int -> Async<unit>


        ///<summary>The family of first class event values for delegate types that satisfy the F# delegate constraint.</summary>
        type IObservable<'T> with
            /// <summary>Permanently connects a listener function to the observable. The listener will
            /// be invoked for each observation.</summary>
            ///
            /// <param name="callback">The function to be called for each observation.</param>
            [<CompiledName("AddToObservable")>] // give the extension member a nice, unmangled compiled name, unique within this module
            member Add: callback:('T -> unit) -> unit

            /// <summary>Connects a listener function to the observable. The listener will
            /// be invoked for each observation. The listener can be removed by
            /// calling Dispose on the returned IDisposable object.</summary>
            ///
            /// <param name="callback">The function to be called for each observation.</param>
            ///
            /// <returns>An object that will remove the listener if disposed.</returns>
            [<CompiledName("SubscribeToObservable")>] // give the extension member a nice, unmangled compiled name, unique within this module
            member Subscribe: callback:('T -> unit) -> System.IDisposable

    /// <summary>A module of extension members providing asynchronous operations for some basic Web operations.</summary>
    ///
    /// <category index="1">Async Programming</category>
    [<AutoOpen>]
    module WebExtensions = 

        type System.Net.WebRequest with 
            /// <summary>Returns an asynchronous computation that, when run, will wait for a response to the given WebRequest.</summary>
            /// <returns>An asynchronous computation that waits for response to the <c>WebRequest</c>.</returns>
            [<CompiledName("AsyncGetResponse")>] // give the extension member a nice, unmangled compiled name, unique within this module
            member AsyncGetResponse : unit -> Async<System.Net.WebResponse>

        type System.Net.WebClient with

            /// <summary>Returns an asynchronous computation that, when run, will wait for the download of the given URI.</summary>
            ///
            /// <param name="address">The URI to retrieve.</param>
            ///
            /// <returns>An asynchronous computation that will wait for the download of the URI.</returns>
            [<CompiledName("AsyncDownloadString")>] // give the extension member a nice, unmangled compiled name, unique within this module
            member AsyncDownloadString : address:System.Uri -> Async<string>

            /// <summary>Returns an asynchronous computation that, when run, will wait for the download of the given URI.</summary>
            ///
            /// <param name="address">The URI to retrieve.</param>
            ///
            /// <returns>An asynchronous computation that will wait for the download of the URI.</returns>
            [<CompiledName("AsyncDownloadData")>] // give the extension member a nice, unmangled compiled name, unique within this module
            member AsyncDownloadData : address:System.Uri -> Async<byte[]>

            /// <summary>Returns an asynchronous computation that, when run, will wait for the download of the given URI to specified file.</summary>
            ///
            /// <param name="address">The URI to retrieve.</param>
            /// <param name="fileName">The filename to save download to.</param>
            ///
            /// <returns>An asynchronous computation that will wait for the download of the URI to specified file.</returns>
            [<CompiledName("AsyncDownloadFile")>] // give the extension member a nice, unmangled compiled name, unique within this module
            member AsyncDownloadFile : address:System.Uri * fileName: string -> Async<unit>

    // Internals used by MailboxProcessor
    module internal AsyncBuilderImpl = 
        val async : AsyncBuilder

