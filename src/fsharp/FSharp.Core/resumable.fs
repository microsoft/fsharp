// Original notice:
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights
// to this software to the public domain worldwide. This software is distributed without any warranty.
//
// Updates:
// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

#nowarn "9"
#nowarn "51"
namespace Microsoft.FSharp.Core.CompilerServices

#if !BUILDING_WITH_LKG && !BUILD_FROM_SOURCE
open System
open System.Runtime.CompilerServices
open Microsoft.FSharp.Core
open Microsoft.FSharp.Core
open Microsoft.FSharp.Core.Printf
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicOperators
open Microsoft.FSharp.Control
open Microsoft.FSharp.Collections

type IResumableStateMachine<'Data> =
    abstract ResumptionPoint: int
    abstract Data: 'Data with get, set

/// Acts as a template for struct state machines introduced by __stateMachine, and also as a reflective implementation
[<Struct; NoComparison; NoEquality>]
type ResumableStateMachine<'Data> =

    [<DefaultValue(false)>]
    val mutable Data: 'Data

    [<DefaultValue(false)>]
    val mutable ResumptionPoint: int

    /// Represents the delegated runtime continuation of a resumable state machine created dynamically
    [<DefaultValue(false)>]
    val mutable ResumptionDynamicInfo: ResumptionDynamicInfo<'Data>

    interface IResumableStateMachine<'Data> with 
        member sm.ResumptionPoint = sm.ResumptionPoint
        member sm.Data with get() = sm.Data and set v = sm.Data <- v

    interface IAsyncStateMachine with 
        
        // Used for dynamic execution.  For "__stateMachine" it is replaced.
        member sm.MoveNext() = 
            sm.ResumptionDynamicInfo.MoveNext(&sm)

        // Used when dynamic execution.  For "__stateMachine" it is replaced.
        member sm.SetStateMachine(state) = 
            sm.ResumptionDynamicInfo.SetStateMachine(&sm, state)

and ResumptionFunc<'Data> = delegate of byref<ResumableStateMachine<'Data>> -> bool

and [<AbstractClass>]
    ResumptionDynamicInfo<'Data>(initial: ResumptionFunc<'Data>) =
    member val ResumptionFunc: ResumptionFunc<'Data> = initial with get, set 
    member val ResumptionData: obj = null with get, set 
    abstract MoveNext: machine: byref<ResumableStateMachine<'Data>> -> unit
    abstract SetStateMachine: machine: byref<ResumableStateMachine<'Data>> * machineState: IAsyncStateMachine -> unit

type ResumableCode<'Data, 'T> = delegate of byref<ResumableStateMachine<'Data>> -> bool

/// Defines the implementation of the MoveNext method for a struct state machine.
[<Experimental("Experimental library feature, requires '--langversion:preview'")>]
type MoveNextMethodImpl<'Data> = delegate of byref<ResumableStateMachine<'Data>> -> unit

/// Defines the implementation of the SetStateMachine method for a struct state machine.
[<Experimental("Experimental library feature, requires '--langversion:preview'")>]
type SetStateMachineMethodImpl<'Data> = delegate of byref<ResumableStateMachine<'Data>> * IAsyncStateMachine -> unit

/// Defines the implementation of the code reun after the creation of a struct state machine.
[<Experimental("Experimental library feature, requires '--langversion:preview'")>]
type AfterCode<'Data, 'Result> = delegate of byref<ResumableStateMachine<'Data>> -> 'Result

[<AutoOpen>]
module StateMachineHelpers = 

    /// Statically determines whether resumable code is being used
    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let __useResumableCode<'T> : bool = false
        
    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let __resumableEntry () : int option = 
        failwith "__resumableEntry should always be guarded by __useResumableCode and only used in valid state machine implementations"

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let __resumeAt<'T> (programLabel: int) : 'T = 
        ignore programLabel
        failwith "__resumeAt should always be guarded by __useResumableCode and only used in valid state machine implementations"

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let __stateMachine<'Data, 'Result> 
           (moveNextMethod: MoveNextMethodImpl<'Data>) 
           (setStateMachineMethod: SetStateMachineMethodImpl<'Data>) 
           (afterCode: AfterCode<'Data, 'Result>): 'Result =
        ignore moveNextMethod
        ignore setStateMachineMethod
        ignore afterCode
        failwith "__stateMachine should always be guarded by __useResumableCode and only used in valid state machine implementations"

module ResumableCode =

    let inline SetResumptionFunc (sm: byref<ResumableStateMachine<'Data>>) f =
        sm.ResumptionDynamicInfo.ResumptionFunc <- f

    let inline GetResumptionFunc (sm: byref<ResumableStateMachine<'Data>>) =
        sm.ResumptionDynamicInfo.ResumptionFunc

    let inline Delay(f : unit -> ResumableCode<'Data, 'T>) : ResumableCode<'Data, 'T> =
        ResumableCode<'Data, 'T>(fun sm -> (f()).Invoke(&sm))

    /// Used to represent no-ops like the implicit empty "else" branch of an "if" expression.
    let inline Zero() : ResumableCode<'Data, unit> =
        ResumableCode<'Data, unit>(fun sm -> true)

    /// Chains together a step with its following step.
    /// Note that this requires that the first step has no result.
    /// This prevents constructs like `task { return 1; return 2; }`.
    let CombineDynamic(sm: byref<ResumableStateMachine<'Data>>, code1: ResumableCode<'Data, unit>, code2: ResumableCode<'Data, 'T>) : bool =
        if code1.Invoke(&sm) then 
            code2.Invoke(&sm)
        else
            let rec resume (mf: ResumptionFunc<'Data>) =
                ResumptionFunc<'Data>(fun sm -> 
                    if mf.Invoke(&sm) then 
                        code2.Invoke(&sm)
                    else
                        sm.ResumptionDynamicInfo.ResumptionFunc <- (resume (GetResumptionFunc &sm))
                        false)

            sm.ResumptionDynamicInfo.ResumptionFunc <- (resume (GetResumptionFunc &sm))
            false

    /// Chains together a step with its following step.
    /// Note that this requires that the first step has no result.
    /// This prevents constructs like `task { return 1; return 2; }`.
    let inline Combine(code1: ResumableCode<'Data, unit>, code2: ResumableCode<'Data, 'T>) : ResumableCode<'Data, 'T> =
        ResumableCode<'Data, 'T>(fun sm ->
            if __useResumableCode then
                //-- RESUMABLE CODE START
                // NOTE: The code for code1 may contain await points! Resuming may branch directly
                // into this code!
                let __stack_fin = code1.Invoke(&sm)
                if __stack_fin then 
                    code2.Invoke(&sm)
                else
                    false
                //-- RESUMABLE CODE END
            else
                CombineDynamic(&sm, code1, code2))

    let rec WhileDynamic (sm: byref<ResumableStateMachine<'Data>>, condition: unit -> bool, body: ResumableCode<'Data,unit>) : bool =
        if condition() then 
            if body.Invoke (&sm) then
                WhileDynamic (&sm, condition, body)
            else
                let rf = GetResumptionFunc &sm
                sm.ResumptionDynamicInfo.ResumptionFunc <- (ResumptionFunc<'Data>(fun sm -> WhileBodyDynamicAux(&sm, condition, body, rf)))
                false
        else
            true
    and WhileBodyDynamicAux (sm: byref<ResumableStateMachine<'Data>>, condition: unit -> bool, body: ResumableCode<'Data,unit>, rf: ResumptionFunc<_>) : bool =
        if rf.Invoke (&sm) then
            WhileDynamic (&sm, condition, body)
        else
            let rf = GetResumptionFunc &sm
            sm.ResumptionDynamicInfo.ResumptionFunc <- (ResumptionFunc<'Data>(fun sm -> WhileBodyDynamicAux(&sm, condition, body, rf)))
            false

    /// Builds a step that executes the body while the condition predicate is true.
    let inline While ([<InlineIfLambda>] condition : unit -> bool, body : ResumableCode<'Data, unit>) : ResumableCode<'Data, unit> =
        ResumableCode<'Data, unit>(fun sm ->
            if __useResumableCode then 
                //-- RESUMABLE CODE START
                let mutable __stack_go = true 
                while __stack_go && condition() do
                    // NOTE: The body of the state machine code for 'while' may contain await points, so resuming
                    // the code will branch directly into the expanded 'body', branching directly into the while loop
                    let __stack_body_fin = body.Invoke(&sm)
                    // If the body completed, we go back around the loop (__stack_go = true)
                    // If the body yielded, we yield (__stack_go = false)
                    __stack_go <- __stack_body_fin
                __stack_go
                //-- RESUMABLE CODE END
            else
                WhileDynamic(&sm, condition, body))

    let rec TryWithDynamic (sm: byref<ResumableStateMachine<'Data>>, body: ResumableCode<'Data, 'T>, handler: exn -> ResumableCode<'Data, 'T>) : bool =
        try
            if body.Invoke(&sm) then 
                true
            else
                let rf = GetResumptionFunc &sm
                sm.ResumptionDynamicInfo.ResumptionFunc <- (ResumptionFunc<'Data>(fun sm -> TryWithDynamic(&sm, ResumableCode<'Data,'T>(fun sm -> rf.Invoke(&sm)), handler)))
                false
        with exn -> 
            (handler exn).Invoke(&sm)

    /// Wraps a step in a try/with. This catches exceptions both in the evaluation of the function
    /// to retrieve the step, and in the continuation of the step (if any).
    let inline TryWith (body: ResumableCode<'Data, 'T>, catch: exn -> ResumableCode<'Data, 'T>) : ResumableCode<'Data, 'T> =
        ResumableCode<'Data, 'T>(fun sm ->
            if __useResumableCode then 
                //-- RESUMABLE CODE START
                let mutable __stack_fin = false
                let mutable __stack_caught = false
                let mutable __stack_savedExn = Unchecked.defaultof<_>
                // This is a meaningless assignment but ensures a debug point gets laid down
                // at the 'try' in the try/with for code as we enter into the handler.
                __stack_fin <- __stack_fin || __stack_fin
                try
                    // The try block may contain await points.
                    let __stack_body_fin = body.Invoke(&sm)
                    // If we make it to the assignment we prove we've made a step
                    __stack_fin <- __stack_body_fin
                with exn -> 
                    // Note, remarkExpr in the F# compiler detects this pattern as the code
                    // is inlined and elides the debug sequence point on the code. This is because the inlining will associate
                    // the sequence point with the 'try' of the TryFinally because that is the range
                    // given for the whole expression 
                    //      task.TryWith(....) 
                    // If you change this code you should check debug sequence points and the generated
                    // code tests for try/with in tasks.
                    __stack_caught <- true
                    __stack_savedExn <- exn

                if __stack_caught then 
                    // Place the catch code outside the catch block 
                    (catch __stack_savedExn).Invoke(&sm)
                else
                    __stack_fin
                //-- RESUMABLE CODE END

            else
                TryWithDynamic(&sm, body, catch))

    let rec TryFinallyCompensateDynamic (sm: byref<ResumableStateMachine<'Data>>, mf: ResumptionFunc<'Data>, savedExn: exn option) : bool =
        let mutable fin = false
        fin <- mf.Invoke(&sm)
        if fin then
            // reraise at the end of the finally block
            match savedExn with 
            | None -> true
            | Some exn -> raise exn
        else 
            let rf = GetResumptionFunc &sm
            sm.ResumptionDynamicInfo.ResumptionFunc <- (ResumptionFunc<'Data>(fun sm -> TryFinallyCompensateDynamic(&sm, rf, savedExn)))
            false

    let rec TryFinallyAsyncDynamic (sm: byref<ResumableStateMachine<'Data>>, body: ResumableCode<'Data, 'T>, compensation: ResumableCode<'Data,unit>) : bool =
        let mutable fin = false
        let mutable savedExn = None
        try
            fin <- body.Invoke(&sm)
        with exn ->
            savedExn <- Some exn 
            fin <- true
        if fin then 
            TryFinallyCompensateDynamic(&sm, ResumptionFunc<'Data>(fun sm -> compensation.Invoke(&sm)), savedExn)
        else
            let rf = GetResumptionFunc &sm
            sm.ResumptionDynamicInfo.ResumptionFunc <- (ResumptionFunc<'Data>(fun sm -> TryFinallyAsyncDynamic(&sm, ResumableCode<'Data,'T>(fun sm -> rf.Invoke(&sm)), compensation)))
            false

    /// Wraps a step in a try/finally. This catches exceptions both in the evaluation of the function
    /// to retrieve the step, and in the continuation of the step (if any).
    let inline TryFinally (body: ResumableCode<'Data, 'T>, compensation: ResumableCode<'Data,unit>) =
        ResumableCode<'Data, 'T>(fun sm ->
            if __useResumableCode then 
                //-- RESUMABLE CODE START
                let mutable __stack_fin = false
                // This is a meaningless assignment but ensures a debug point gets laid down
                // at the 'try' in the try/finally. The 'try' is used as the range for the
                // F# computation expression desugaring to 'TryFinally' and this range in turn gets applied
                // to inlined code.
                __stack_fin <- __stack_fin || __stack_fin
                try
                    let __stack_body_fin = body.Invoke(&sm)
                    // If we make it to the assignment we prove we've made a step, an early 'ret' exit out of the try/with
                    // may skip this step.
                    __stack_fin <- __stack_body_fin
                with _exn ->
                    // Note, remarkExpr in the F# compiler detects this pattern as the code
                    // is inlined and elides the debug sequence point on either the 'compensation'
                    // 'reraise' statement for the code. This is because the inlining will associate
                    // the sequence point with the 'try' of the TryFinally because that is the range
                    // given for the whole expression 
                    //      task.TryFinally(....) 
                    // If you change this code you should check debug sequence points and the generated
                    // code tests for try/finally in tasks.
                    let __stack_ignore = compensation.Invoke(&sm)
                    reraise()

                if __stack_fin then 
                    let __stack_ignore = compensation.Invoke(&sm)
                    ()
                __stack_fin
                //-- RESUMABLE CODE END
            else
                TryFinallyAsyncDynamic(&sm, body, ResumableCode<_,_>(fun sm -> compensation.Invoke(&sm))))

    /// Wraps a step in a try/finally. This catches exceptions both in the evaluation of the function
    /// to retrieve the step, and in the continuation of the step (if any).
    let inline TryFinallyAsync (body: ResumableCode<'Data, 'T>, compensation: ResumableCode<'Data,unit>) : ResumableCode<'Data, 'T> =
        ResumableCode<'Data, 'T>(fun sm ->
            if __useResumableCode then 
                //-- RESUMABLE CODE START
                let mutable __stack_fin = false
                let mutable savedExn = None
                // This is a meaningless assignment but ensures a debug point gets laid down
                // at the 'try' in the try/finally. The 'try' is used as the range for the
                // F# computation expression desugaring to 'TryFinally' and this range in turn gets applied
                // to inlined code.
                __stack_fin <- __stack_fin || __stack_fin
                try
                    let __stack_body_fin = body.Invoke(&sm)
                    // If we make it to the assignment we prove we've made a step, an early 'ret' exit out of the try/with
                    // may skip this step.
                    __stack_fin <- __stack_body_fin
                with exn ->
                    savedExn <- Some exn
                    __stack_fin <- true

                if __stack_fin then 
                    let __stack_compensation_fin = compensation.Invoke(&sm)
                    __stack_fin <- __stack_compensation_fin
                if __stack_fin then 
                    match savedExn with 
                    | None -> ()
                    | Some exn -> raise exn
                __stack_fin
                //-- RESUMABLE CODE END
            else
                TryFinallyAsyncDynamic(&sm, body, compensation))

    let inline Using (resource : 'Resource, body : 'Resource -> ResumableCode<'Data, 'T>) : ResumableCode<'Data, 'T> when 'Resource :> IDisposable = 
        // A using statement is just a try/finally with the finally block disposing if non-null.
        TryFinally(
            ResumableCode<'Data, 'T>(fun sm -> (body resource).Invoke(&sm)),
            ResumableCode<'Data,unit>(fun sm -> 
                if not (isNull (box resource)) then 
                    resource.Dispose()
                true))

    let inline For (sequence : seq<'T>, body : 'T -> ResumableCode<'Data, unit>) : ResumableCode<'Data, unit> =
        // A for loop is just a using statement on the sequence's enumerator...
        Using (sequence.GetEnumerator(), 
            // ... and its body is a while loop that advances the enumerator and runs the body on each element.
            (fun e -> While((fun () -> e.MoveNext()), ResumableCode<'Data, unit>(fun sm -> (body e.Current).Invoke(&sm)))))

    let YieldDynamic (sm: byref<ResumableStateMachine<'Data>>) : bool = 
        let cont = ResumptionFunc<'Data>(fun _sm -> true)
        sm.ResumptionDynamicInfo.ResumptionFunc <- cont
        false

    let inline Yield () : ResumableCode<'Data, unit> = 
        ResumableCode<'Data, unit>(fun sm -> 
            if __useResumableCode then 
                //-- RESUMABLE CODE START
                match __resumableEntry() with 
                | Some contID ->
                    sm.ResumptionPoint <- contID
                    //if verbose then printfn $"[{sm.Id}] Yield: returning false to indicate yield, contID = {contID}" 
                    false
                | None ->
                    //if verbose then printfn $"[{sm.Id}] Yield: returning true to indicate post-yield" 
                    true
                //-- RESUMABLE CODE END
            else
                YieldDynamic(&sm))

#endif
