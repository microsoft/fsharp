// This is a sample and test showing how to use resumable code to implement coroutines 
//
// A coroutine is a value of type Coroutine normally constructed using this form:
//
//    coroutine {
//       printfn "in t1"
//       yield ()
//       printfn "hey"
//    }

module rec Tests.CoroutinesBasic

open System
open System.Runtime.CompilerServices
open FSharp.Core.CompilerServices
open FSharp.Core.CompilerServices.StateMachineHelpers

/// This is the type of coroutines
[<AbstractClass; NoEquality; NoComparison>] 
type Coroutine() =
    
    /// Checks if the coroutine is completed
    abstract IsCompleted: bool

    /// Executes the coroutine until the next 'yield'
    abstract MoveNext: unit -> unit

/// Helpers to do zero-allocation call to interface methods on structs
[<AutoOpen>]
module internal Helpers =
    let inline MoveNext(x: byref<'T> when 'T :> IAsyncStateMachine) = x.MoveNext()
    let inline GetResumptionPoint(x: byref<'T> when 'T :> IResumableStateMachine<'Data>) = x.ResumptionPoint
    let inline SetData(x: byref<'T> when 'T :> IResumableStateMachine<'Data>, data) = x.Data <- data

/// This is the implementation of Coroutine with respect to a particular struct state machine type.
[<NoEquality; NoComparison>] 
type Coroutine<'Machine when 'Machine : struct
                        and 'Machine :> IAsyncStateMachine 
                        and 'Machine :> ICoroutineStateMachine>() =
    inherit Coroutine()

    // The state machine struct
    [<DefaultValue(false)>]
    val mutable Machine: 'Machine

    override cr.IsCompleted =
        GetResumptionPoint(&cr.Machine) = -1

    override cr.MoveNext() = 
        MoveNext(&cr.Machine)

/// This extra data stored in ResumableStateMachine (and it's templated copies using __stateMachine) 
/// In this example there is just an ID
[<Struct>]
type CoroutineStateMachineData(id: int) = 
    member _.Id = id

let nextId = 
    let mutable n = 0
    fun () -> n <- n + 1; n

/// These are standard definitions filling in the 'Data' parameter of each
type ICoroutineStateMachine = IResumableStateMachine<CoroutineStateMachineData>
type CoroutineStateMachine = ResumableStateMachine<CoroutineStateMachineData>
type CoroutineResumptionFunc = ResumptionFunc<CoroutineStateMachineData>
type CoroutineResumptionDynamicInfo = ResumptionDynamicInfo<CoroutineStateMachineData>
type CoroutineCode = ResumableCode<CoroutineStateMachineData, unit>

type CoroutineBuilder() =
    
    member inline _.Delay(f : unit -> CoroutineCode) : CoroutineCode = ResumableCode.Delay(f)

    /// Used to represent no-ops like the implicit empty "else" branch of an "if" expression.
    [<DefaultValue>]
    member inline _.Zero() : CoroutineCode = ResumableCode.Zero()

    // The implementation of `e1; e2`
    member inline _.Combine(code1: CoroutineCode, code2: CoroutineCode) : CoroutineCode =
        ResumableCode.Combine(code1, code2)

    // The implementation of `while`
    member inline _.While ([<InlineIfLambda>] condition : unit -> bool, body : CoroutineCode) : CoroutineCode =
        ResumableCode.While(condition, body)

    // The implementation of `try/with`
    member inline _.TryWith (body: CoroutineCode, catch: exn -> CoroutineCode) : CoroutineCode =
        ResumableCode.TryWith(body, catch)

    // The implementation of `try/finally`
    member inline _.TryFinally (body: CoroutineCode, [<InlineIfLambda>] compensation : unit -> unit) : CoroutineCode =
        ResumableCode.TryFinally(body, ResumableCode<_,_>(fun _ -> compensation(); true))

    // The implementation of `use`
    member inline _.Using (resource : 'Resource, body : 'Resource -> CoroutineCode) : CoroutineCode when 'Resource :> IDisposable = 
        ResumableCode.Using(resource, body)

    // The implementation of `for`
    member inline _.For (sequence : seq<'T>, body : 'T -> CoroutineCode) : CoroutineCode =
        ResumableCode.For(sequence, body)

    // The implementation of `yield`
    member inline _.Yield (_dummy: unit) : CoroutineCode = 
        ResumableCode.Yield()

    // The implementation of `yield!`
    member inline _.YieldFrom (other: Coroutine) : CoroutineCode = 
        ResumableCode.While((fun () -> not other.IsCompleted), CoroutineCode(fun sm -> 
            other.MoveNext()
            let __stack_other_fin = other.IsCompleted
            if not __stack_other_fin then
                ResumableCode.Yield().Invoke(&sm)
            else
                true))

    /// Create the state machine and outer execution logic
    member inline _.Run(code : CoroutineCode) : Coroutine = 
        if __useResumableCode then 
            __stateMachine<CoroutineStateMachineData, Coroutine>

                // IAsyncStateMachine.MoveNext
                (MoveNextMethodImpl<_>(fun sm -> 
                    __resumeAt sm.ResumptionPoint 
                    let __stack_code_fin = code.Invoke(&sm)
                    if __stack_code_fin then
                        sm.ResumptionPoint  <- -1 // indicates complete
                    ))

                // IAsyncStateMachine.SetStateMachine
                (SetStateMachineMethodImpl<_>(fun sm state -> ()))

                // Box the coroutine.  In this example we don't start execution of the coroutine.
                (AfterCode<_,_>(fun sm -> 
                    let mutable cr = Coroutine<CoroutineStateMachine>()
                    SetData(&cr.Machine, CoroutineStateMachineData(nextId()))
                    cr.Machine <- sm
                    cr :> Coroutine))
        else 
            // The dynamic implementation
            let initialResumptionFunc = CoroutineResumptionFunc(fun sm -> code.Invoke(&sm))
            let resumptionInfo =
                { new CoroutineResumptionDynamicInfo(initialResumptionFunc) with 
                    member info.MoveNext(sm) = 
                        if info.ResumptionFunc.Invoke(&sm) then
                            sm.ResumptionPoint <- -1
                    member info.SetStateMachine(sm, state) = ()
                 }
            let mutable cr = Coroutine<CoroutineStateMachine>()
            cr.Machine.ResumptionDynamicInfo <- resumptionInfo
            cr :> Coroutine

[<AutoOpen>]
module CoroutineBuilder = 

    let coroutine = CoroutineBuilder()

module Examples =
    let t1 () = 
        coroutine {
           printfn "in t1"
           yield ()
           printfn "hey ho"
           yield ()
        }
    let dumpCoroutine (t: Coroutine) = 
        printfn "-----"
        while ( t.MoveNext()
                not t.IsCompleted) do 
            printfn "yield"

