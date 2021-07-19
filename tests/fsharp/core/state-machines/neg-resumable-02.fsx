module Tests_Tasks

#nowarn "57"
open FSharp.Core.CompilerServices
open FSharp.Core.CompilerServices.StateMachineHelpers
open System.Threading.Tasks
open System.Runtime.CompilerServices

//vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv
// No warnings expected in the declarations

let inline MoveNext(x: byref<'T> when 'T :> IAsyncStateMachine) = x.MoveNext()
let inline MoveOnce(x: byref<'T> when 'T :> IAsyncStateMachine and 'T :> IResumableStateMachine<'Data>) = 
    MoveNext(&x)
    x.Data

module ``late compilation error on user-defined let rec in resumable code`` =
    
    let inline f1 (code: unit -> ResumableCode<_,_>)  = ResumableCode<int,int>(fun sm ->  code().Invoke(&sm))

    let makeStateMachine inputValue = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                let __stack_step =
                    f1(fun () -> 
                        let rec f x = f x + 1 // resumable code can't be used because of 'let rec' and no alternative is given
                        f 3 |> ignore
                        ResumableCode.Zero()).Invoke(&sm)
                ()
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))

    makeStateMachine 4

module ``compilation warning on user-defined let rec in resumable code when alternative given`` =
    
    let inline f1 (code: unit -> ResumableCode<_,_>)  = ResumableCode<int,int>(fun sm ->  code().Invoke(&sm))

    let makeStateMachine () = 
        if __useResumableCode then
            __stateMachine<int, int>
                (MoveNextMethodImpl<_>(fun sm -> 
                    let __stack_step =
                        f1(fun () -> 
                            let rec f x = f x + 1 // resumable code can't be used because of 'let rec' and no alternative is given
                            f 3 |> ignore
                            ResumableCode.Zero()).Invoke(&sm)
                    ()
                    )) 
                (SetStateMachineMethodImpl<_>(fun sm state -> ()))
                (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))
        else
            2


    makeStateMachine ()

module ``no compilation warning on user-defined let in task`` =
    
    let makeTask () = 
        task { 
            let f x = 
                printfn "ewcvw"
                printfn "ewcvw"
                printfn "ewcvw"
                printfn "ewcvw"
                1
            return f 1
        }

module ``compilation warning on user-defined let rec in task`` =
    
    let makeTask () = 
        task { 
            let rec f x = f x + 1
            return f 1
        }

