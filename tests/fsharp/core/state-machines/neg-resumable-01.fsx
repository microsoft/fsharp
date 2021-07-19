module Tests_Tasks

#nowarn "57" // experimental
#nowarn "3513" // resumable code invocation
open FSharp.Core.CompilerServices
open FSharp.Core.CompilerServices.StateMachineHelpers
open System.Runtime.CompilerServices

//vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv
// No warnings expected in the declarations

module ``No warning for resumable code that is just a simple expression`` =
    
    let inline f1 ()  = ResumableCode<int,int>(fun sm ->  true )

module ``No warning for resumable code that is just a sequential expression`` =
    
    
    let inline f1 () =
        ResumableCode<int,int>(fun sm -> 
             printfn "hello"
             true)

module ``No warning for protected use of __resumeAt`` =
    
    
    let inline f1 () =
        ResumableCode<int,int>(fun sm -> 
             if __useResumableCode then
                __resumeAt 0 // we don't get a warning here
                true
             else
                 false )

module ``No warning for protected use of __resumableEntry`` =
    
    
    let inline f1 () =
        ResumableCode<int,int>(fun sm -> 
             if __useResumableCode then
                 match __resumableEntry() with 
                 | Some contID -> true
                 | None -> true
             else
                 false)

//^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
// No warnings expected in code before this line


let inline MoveNext(x: byref<'T> when 'T :> IAsyncStateMachine) = x.MoveNext()
let inline MoveOnce(x: byref<'T> when 'T :> IAsyncStateMachine and 'T :> IResumableStateMachine<'Data>) = 
    MoveNext(&x)
    x.Data

// Resumable code may not have let rec statements
module ``Error on let rec in resumable code`` =
    let makeStateMachine inputValue = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
               
                let rec f x = if x > 0 then f (x-1) else inputValue + 13
                f 10 + f 2 |> ignore
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))

module ``Error on unprotected __resumeAt`` =
    let inline f1 () =
        __resumeAt 0 
        1 

module ``Error on unprotected __resumeAt in resumable code`` =
    let inline f1 () =
        ResumableCode<int,int>(fun sm -> __resumeAt 0)

module ``Error because fast integer for loop doesn't supported resumable code`` =
    
    let inline f1 () =
        ResumableCode<int,int>(fun sm -> 
            if __useResumableCode then
                for i = 0 to 1 do
                    __resumeAt 0 // we expect an error here - this is not valid resumable code
                true
            else
                false)

module ``Error because unprotected __resumableEntry`` =
    
    let inline f1 () =
        ResumableCode<int,int>(fun sm -> 
                match __resumableEntry() with  // we expect an error here - this is not valid resumable code without 'if .. '
                | Some contID -> false
                | None -> true
        )

module ``let bound function can't use resumable code constructs`` =
    let makeStateMachine inputValue = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
               
                    __resumeAt 1
                    let f () =
                        match __resumableEntry() with // we expect an error here - this is not in resumable code 
                        | Some contID ->
                            __resumeAt contID
                        | None -> 
                            20+inputValue
                    f()  |> ignore
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))

module ``Check no resumption point in try-finally `` =
    let makeStateMachine inputValue = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                    __resumeAt 1 
                    let mutable res = inputValue
                    try 
                        // Attempt to specify a resumption point in the try-finally
                        match __resumableEntry() with // we expect an error here - this is not in resumable code 
                        | Some contID ->
                            failwith "unreachable"
                            res <- 0
                        | None -> 
                            res <- res + 10
                    finally 
                        res <- res + 20 
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))

module ``Check no resumption point in fast integer for loop`` =
    let makeStateMachine inputValue = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                    __resumeAt 1 
                    let mutable res = inputValue
                    let mutable count = 0
                    for i = 0 to 9 do 
                        // Attempt to specify a resumption point in the for loop. THis is not permitted
                        // and while loops should be used instead
                        match __resumableEntry() with // we expect an error here - this is not in resumable code 
                        | Some contID ->
                            failwith "unreachable"
                            res <- 0
                        | None -> 
                            res <- res + 10
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))
