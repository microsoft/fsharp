(*** hide ***)
#I "../../artifacts/bin/FSharp.Compiler.Service/Debug/netstandard2.0"
(**
Compiler Services: Notes on the FSharpChecker operations queue
=================================================

This is a design note on the FSharpChecker component and its operations queue.  See also the notes on the [FSharpChecker caches](caches.html)

FSharpChecker maintains an operations queue. Items from the FSharpChecker operations queue are processed 
sequentially and in order. 

This means the FCS API has three kinds of operations:

* "Runs on caller thread (runs on caller thread)" - Some requests from FSharp.Editor are
  serviced concurrently without using the queue at all. Everything without an Async return type
  is in this category.

* "Queued-at-high-priority (runs on reactor thread)" - These are requests made via the FCS API
  (e.g. from FSharp.Editor) and anything with "Async" return type is in this category. The
  originating calls are not typically on the UI thread and are associated with active actions
  by the user (editing a file etc.). 

  These correspond to the calls to EnqueueAndAwaitOpAsync in [service.fs](https://github.com/fsharp/FSharp.Compiler.Service/blob/master/src/fsharp/service/service.fs).
  For example, calling `ParseAndCheckProject` enqueues a `ParseAndCheckProjectImpl` operation. The time taken for the 
  operation will depend on how much work is required to bring the project analysis up-to-date.
  The length of the operation will vary - many will be very fast - but they won't
  be processed until other operations already in the queue are complete.

* "Queued and interleaved at lower priority (runs on reactor thread)" - This is reserved
  for a "background" job (CheckProjectInBackground) used for to prepare the project builder
  state of the current project being worked on. The "background" work is intended to be
  divided into little chunks so it can always be interrupted in order to service the higher-priority work.

  This operation runs when the queue is empty.  When the operations queue has been empty for 1 second,
  this work is run in small incremental fragments. The overall work may get cancelled if replaced
  by an alternative project build.  This work is cooperatively
  time-sliced to be approximately <50ms, (see `maxTimeShareMilliseconds` in 
  IncrementalBuild.fs). The project to be checked in the background is set implicitly 
  by calls to ``CheckFileInProject`` and ``ParseAndCheckFileInProject``.
  To disable implicit background checking completely, set ``checker.ImplicitlyStartBackgroundWork`` to false.
  To change the time before background work starts, set ``checker.PauseBeforeBackgroundWork`` to the required
  number of milliseconds.

Some tools throw a lot of "Queued-at-high-priority" work at the FSharpChecker operations queue. 
If you are writing such a component, consider running your project against a debug build
of FSharp.Compiler.Service.dll to see the Trace.WriteInformation messages indicating the length of the
operations queue and the time to process requests.

For those writing interactive editors which use FCS, you 
should be cautious about long running "Queued-at-high-priority" operations - these
will run in preference to other similar operations and must be both asynchronous
and cancelled if the results will no longer be needed.
For example, be careful about requesting the check of an entire project
on operations like "Highlight Symbol" or "Find Unused Declarations" 
(which run automatically when the user opens a file or moves the cursor).
as opposed to operations like "Find All References" (which a user explicitly triggers). 
Project checking can cause long and contention on the FSharpChecker operations queue. You *must*
cancel such operations if the results will be out-of-date, in order for your editing tools to be performant.

Requests can be cancelled via the cancellation token of the async operation. (Some requests also 
include additional callbacks which can be used to indicate a cancellation condition).  
If the operation has not yet started it will remain in the queue and be discarded when it reaches the front.

The long term intent of FCS is to eventually remove the reactor thread and the operations queue. However the queue
has several operational impacts we need to be mindful of

1. It acts as a brake on the overall resource usage (if 1000 requests get made from FSharp.Editor they are serviced one at a time, and the work is not generally repeated as it get cached).

2. It potentially acts as a data-lock on the project builder compilation state.

3. It runs the low-priority project build.

Summary
-------

In this design note, you learned that the FSharpChecker component keeps an operations queue. When using FSharpChecker
in highly interactive situations, you should carefully consider the characteristics of the operations you are 
enqueueing.
*)
