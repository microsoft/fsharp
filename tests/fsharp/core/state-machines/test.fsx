module Tests_Tasks

#nowarn "57"
open FSharp.Core.CompilerServices
open FSharp.Core.CompilerServices.StateMachineHelpers
open System.Runtime.CompilerServices
open System.Threading.Tasks


let failures = ref []

let report_failure (s : string) = 
    stderr.Write" NO: "
    stderr.WriteLine s
    failures := !failures @ [s]

let test s b =
    stderr.Write(s:string)
    if b then stderr.WriteLine " OK" else report_failure s
    stderr.WriteLine "" 

let check s v1 v2 = 
   stderr.Write(s:string);  
   if (v1 = v2) then 
       stderr.WriteLine " OK" 
       stderr.WriteLine "" 
   else
       eprintf " FAILED: got %A, expected %A" v1 v2 
       stderr.WriteLine "" 
       report_failure s

let inline MoveNext(x: byref<'T> when 'T :> IAsyncStateMachine) = x.MoveNext()
let inline MoveOnce(x: byref<'T> when 'T :> IAsyncStateMachine and 'T :> IResumableStateMachine<'Data>) = 
    MoveNext(&x)
    x.Data

module ``Check simple state machine`` =
    let makeStateMachine()  = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                if __useResumableCode then
                    sm.Data <- 1 // we expect this result for successful resumable code compilation
                else
                    sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))
    check "ResumableObject_SimpleImmediateExpression" (makeStateMachine()) 1


module ``Check resumable code can capture variables`` =
    let makeStateMachine x = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                if __useResumableCode then
                    sm.Data <- 1 + x // we expect this result for successful resumable code compilation
                else
                    sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))
    let sm3 = makeStateMachine 3
    check "vwervwkhevh" (makeStateMachine 3) 4


module ``Check resumable code may be preceeded by value definitions`` =
    let makeStateMachine x = 
        let rnd = System.Random().Next(4)
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                if __useResumableCode then
                    sm.Data <- x + rnd - rnd // we expect this result for successful resumable code compilation
                else
                    sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))
    check "cvwvweewvhjkv" (makeStateMachine 4) 4

module ``Check resumable code may contain local function definitions`` =
    let makeStateMachine y = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                if __useResumableCode then
                    let someFunction x = x + 1
                    sm.Data <- someFunction y + someFunction y
                else
                    sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))
    check "vewwevbrklbre" (makeStateMachine 3) 8

module ``Check resumable code may be preceeded by function definitions`` =
    let makeStateMachine y = 
        let someFunction1 x = x + 1
        let someFunction2 x = someFunction1 x + 2
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                if __useResumableCode then
                    sm.Data <- someFunction2 y
                else
                    sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))
    check "vwekvewkhvew" (makeStateMachine 3) 6

module ``Check resumable code may contain let statements without resumption points`` =
    let makeStateMachine x = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                if __useResumableCode then
                    let y = 1 - x
                    if x > 3 then 
                        sm.Data <- 1 + x 
                    else 
                        sm.Data <- y
                else
                    sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))
    check "vwelvewl" (makeStateMachine 3) -2

module ``Check resumable code may contain sequential statements without resumption points`` =
    let makeStateMachine y = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                if __useResumableCode then
                    printfn "step1" 
                    sm.Data <- y + 2
                else
                    sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))
    check "ewvkwekvwevwvek" (makeStateMachine 3) 5

module ``Check resuming at start of method`` =
    let makeStateMachine y = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                
                if __useResumableCode then
                    __resumeAt 0
                    let __stack_fin = ResumableCode.Yield().Invoke(&sm) // label 1
                    if __stack_fin then 
                        sm.Data <- 20+y 
                    else
                        sm.Data <- 10+y // we should end up here
                else
                    sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))
    check "vwrvwlkelwe" (makeStateMachine 3) 13

module ``Check resuming at drop through`` =
    let makeStateMachine y = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                if __useResumableCode then
                    __resumeAt 100
                    let __stack_fin = ResumableCode.Yield().Invoke(&sm) // label 1
                    if __stack_fin then 
                        sm.Data <- 20+y 
                    else
                        sm.Data <- 10+y // we should end up here
                else
                    sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))
    check "cvqelekckeq" (makeStateMachine 3) 13

module ``Check resuming at a resumption point`` =
    let makeStateMachine y = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                
                if __useResumableCode then
                    __resumeAt 1
                    let __stack_fin = ResumableCode.Yield().Invoke(&sm) // label 1
                    if __stack_fin then 
                        sm.Data <- 20+y // we should end up here
                    else
                        sm.Data <- 10+y 
                else
                    sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))
    check "vwlvkjlewjwevj" (makeStateMachine 3) 23


module ``Check resumable code may contain try-with without resumption points`` =
    let makeStateMachine inputValue = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                
                if __useResumableCode then
                    try 
                        sm.Data <- inputValue + 10
                    with e -> 
                        sm.Data <- 12
                else
                    sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))

    check "fvwewejkhkwe" (makeStateMachine 13) 23

module ``Check resuming at resumption point in try block`` =
    let makeStateMachine inputValue = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                if __useResumableCode then
                    __resumeAt 1 // this goes straight to label 1
                    try 
                        failwith "unreachable #1" // this is skipped  because we jump straight to label 1
                        let __stack_fin = ResumableCode.Yield().Invoke(&sm) // label 1 is resumption of this
                        if __stack_fin  then 
                            // This is the resumption path
                            sm.Data <- 20+inputValue // this is the result
                        else
                            // This is the suspension path. It is not executed in this test because we jump straight to label 1
                            failwith "unreachable #2"
                    with e -> 
                        sm.Data <- 12 
                else
                    sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))

    check "ResumableCode_TryWithResumption" (makeStateMachine 13) 33

module ``Check exception thrown in try block after resumption is caught`` =
    let makeStateMachine inputValue = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                if __useResumableCode then
                    __resumeAt 1 // this goes straight to label 1
                    try 
                        // Specify a resumption point
                        let __stack_fin = ResumableCode.Yield().Invoke(&sm) // label 1 is resumption of this
                        if not __stack_fin then 
                            // This is the suspension path. It is not executed in this test because we jump straight to label 1
                            sm.Data <- 10+inputValue
                        else
                            // This is the resumption path
                            failwith "raise"
                            sm.Data <- inputValue + 15
                    with e -> 
                        sm.Data <- inputValue + 12  // this is the result
                else
                    sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))

    check "ResumableCode_TryWithResumptionException" (makeStateMachine 13) 25

module ``Check exception thrown in try block with no resumption points is caught`` =
    let makeStateMachine inputValue = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                if __useResumableCode then
                    try 
                        failwith "fail"
                    with e -> 
                        sm.Data <- inputValue + 12 
                else
                    sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))

    check "ResumableCode_TryWithExceptionNoResumption" (makeStateMachine 13) 25

module ``Check resumable code may contain try-finally without resumption points`` =
    let makeStateMachine inputValue = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                if __useResumableCode then
                    let mutable res = 0
                    try 
                        res <- inputValue + 10
                    finally 
                        res <- inputValue + 40 
                    sm.Data <- res
                else
                    sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))

    check "ResumableCode_TryFinallyNoResumption" (makeStateMachine 13) 53

// try-finally may **not** contain resumption points.  This is because it's 
// very difficult to codegen them in IL. Instead you have to code try/finally logic as shown below.
// See also tasks.fs.
module ``Check can simulate try-finally via try-with`` =
    let makeStateMachine inputValue = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                if __useResumableCode then
                    __resumeAt 1 // this goes straight to label 1
                    let mutable res = 0 // note, this initialization doesn't actually happen since we go straight to label 1. However this is the zero-init value.
                    let mutable __stack_completed = false // note, this initialization doesn't actually happen since we go straight to label 1. However this is the zero-init value.
                    try 
                        failwith "unreachable" // this code is not executed as the __resumeAt goes straight to resumption label 1

                        // Specify a resumption point
                        let __stack_fin = ResumableCode.Yield().Invoke(&sm)
                        if not __stack_fin then
                            // This is the suspension path. It is not executed in this test because we jump straight to label 1
                            failwith "unreachable"
                            res <- 0
                            __stack_completed <- false
                        else
                            // This is the resumption path
                            // Label 1 goes here
                            res <- res + + inputValue + 10 // this gets executed and contributes to the result
                            __stack_completed <- true
                    with _ ->
                        res <- res + 20 
                        reraise()
                    if __stack_completed then 
                        res <- res + 20 
                    sm.Data <- res
                else
                    sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))

    check "vwevewewlvjve" (makeStateMachine 13) 43

module ``Check resumable code may contain try-finally without resumption points and raising exception`` =
    let mutable res = 0
    let makeStateMachine inputValue = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                if __useResumableCode then
                    try 
                        failwith "fail"
                    finally 
                        res <- inputValue + 10 
                    sm.Data <- res
                else
                    sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))

    check "cvwekjwejkl" (try makeStateMachine 13 with _ -> res ) 23

module ``Check resumable code may contain while loop without resumption`` =
    let makeStateMachine inputValue = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                if __useResumableCode then
                    let mutable count = 0
                    while count < 10 do 
                        count <- count + 1
                    sm.Data <- inputValue + 12 + count 
                else
                    sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))

    check "fvewhjkvekwhk" (makeStateMachine 13) 35

module ``Check resumable code may contain while loop with resumption`` =
    let makeStateMachine inputValue = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                if __useResumableCode then
                    __resumeAt 1 // this goes straight to label 1
                    let mutable count = 0 // note, this initialization doesn't actually happen since we go straight to label 1. However this is the zero-init value.
                    let mutable res = 0 // note, this initialization doesn't actually happen since we go straight to label 1. However this is the zero-init value.
                    while count < 10 do 
                        // Specify a resumption point in the loop
                        let __stack_fin = ResumableCode.Yield().Invoke(&sm)
                        if not __stack_fin then
                            // This is the suspension path. It is executed in this test for subsequent iterations in this loop
                            count <- count + 1
                            res <- res + 20 // gets executed 9 times
                        else
                            // This is the resumption path
                            // Label 1 goes here
                            count <- count + 1
                            res <- res + 10 // gets executed once
                    // res ends up as 190
                    sm.Data <- inputValue + res 
                else
                    sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))

    check "wejlcewjllkjwce" (makeStateMachine 13) 203


module ``Check resumable code may contain integer for loop without resumption`` =
    let makeStateMachine inputValue = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                if __useResumableCode then
                    let mutable count = 0
                    for i = 0 to 9 do 
                        count <- count + 1
                    sm.Data <- inputValue + 12 + count 
                else
                    sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))

    check "celjkcwljeljwecl" (makeStateMachine 13) 35

module ``Check resumable code can contain a conditional with a resumption point`` =
    let makeStateMachine y = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                if __useResumableCode then
                    __resumeAt 1
                    failwith "fail"
                    if y > 10 then 
                        let __stack_fin = ResumableCode.Yield().Invoke(&sm)
                        if not __stack_fin then
                            // This is the suspension path. It is not executed in this test as we go straight to label 1
                            failwith "fail"
                        else
                            // This is the resumption path
                            // Label 1 goes here
                            sm.Data <- 20+y  // this is the answer
                    else
                        sm.Data <- 30 + y 
                else
                    sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))

    check "vewowevewoi" (makeStateMachine 13) 33

module ``Check resumable code can contain a conditional with a resumption point in else branch`` =
    let makeStateMachine y = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                if __useResumableCode then
                    __resumeAt 1
                    failwith "fail"
                    if y <= 10 then 
                        sm.Data <- 30 + y 
                    else
                        let __stack_fin = ResumableCode.Yield().Invoke(&sm)
                        if not __stack_fin then
                            // This is the suspension path. It is not executed in this test as we go straight to label 1
                            failwith "fail"
                        else
                            // This is the resumption path
                            // Label 1 goes here
                            sm.Data <- 20+y  // this is the answer
                else
                    sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))
                    
    check "vewowevewoi" (makeStateMachine 13) 33



let inline checkStateMachine nm = 
    check nm (System.Reflection.MethodBase.GetCurrentMethod().Name) "MoveNext"

// The main tests for tasks are in tests\FSharp.Core.UnitTests\FSharp.Core\Microsoft.FSharp.Control\Tasks.fs
// These simply detect state machine compilation
module ``Check simple task compiles to state machine`` =

    let test1() = 

        task {
            checkStateMachine "vevroerhn11" 
            return 1
         }

    test1().Wait()

module ``Check simple task with bind compiles to state machine`` =
    let test2() = 

        task {
            checkStateMachine "vevroerhn12" 
            let! v1 = Task.FromResult 1
            checkStateMachine "vevroerhn13" 
            return 1
         }

    test2().Wait()


module ``Check task with multiple bind doesn't cause code explosion from inlining and compiles to state machine`` =

    let syncTask() = Task.FromResult 100

    let tenBindSync_Task() =
        task {
            let! res1 = syncTask()
            checkStateMachine "vevroerhn121" 
            let! res2 = syncTask()
            checkStateMachine "vevroerhn122" 
            let! res3 = syncTask()
            checkStateMachine "vevroerhn123" 
            let! res4 = syncTask()
            checkStateMachine "vevroerhn124" 
            let! res5 = syncTask()
            checkStateMachine "vevroerhn125" 
            let! res6 = syncTask()
            checkStateMachine "vevroerhn126" 
            let! res7 = syncTask()
            checkStateMachine "vevroerhn127" 
            let! res8 = syncTask()
            checkStateMachine "vevroerhn128" 
            let! res9 = syncTask()
            checkStateMachine "vevroerhn129" 
            let! res10 = syncTask()
            checkStateMachine "vevroerhn120" 
            return res1 + res2 + res3 + res4 + res5 + res6 + res7 + res8 + res9 + res10
         }

    tenBindSync_Task().Wait()

module ``Check task with try with compiles to state machine`` =
    let t67() =
        task {
            checkStateMachine "vevroerhn180" 
            try
                checkStateMachine "vevroerhn181" 
                do! Task.Delay(0)
                checkStateMachine "vevroerhn182" 
            with
            | :? System.ArgumentException -> 
                ()
            | _ -> 
                ()
        }

    t67().Wait()

module ``Check task with try with and incomplete match compiles to state machine`` =
    let t68() =
        task {
            try
                checkStateMachine "vevroerhn190" 
                do! Task.Delay(0)
                checkStateMachine "vevroerhn191" 
            with
            | :? System.ArgumentException -> 
                ()
        }

    t68().Wait()

module ``Check task with while loop with resumption points compiles to state machine`` =
    let t68() : Task<int> =
        task {
            checkStateMachine "vevroerhn200" 
            let mutable i = 0
            while i < 1 do
                checkStateMachine "vevroerhn201" 
                i <- i + 1
                do! Task.Yield()
                ()
            checkStateMachine "vevroerhn202" 
            return i
        }

    t68().Wait()

module ``Check task with try finally compiles to state machine`` =
    let t68() =
        task {
            let mutable ran = false
            try
                checkStateMachine "vevroerhn210" 
                do! Task.Delay(100)
                checkStateMachine "vevroerhn211" 

            finally
                ran <- true
            return ran
        }

    t68().Wait()

module ``Check task with use compiles to state machine`` =
    let t68() =
                task {
                    let mutable disposed = false
                    use d = { new System.IDisposable with member __.Dispose() = disposed <- true }
                    checkStateMachine "vevroerhn221" 
                    do! Task.Delay(100)
                    checkStateMachine "vevroerhn221" 
                }

    t68().Wait()

module ``Check nested task compiles to state machine`` =
    let t68() =
            task {
                let mutable n = 0
                checkStateMachine "vevroerhn230" 
                while n < 10 do
                    let! _ =
                        task {
                            do! Task.Yield()
                            checkStateMachine "vevroerhn231" 
                            let! _ = Task.FromResult(0)
                            n <- n + 1
                        }
                    n <- n + 1
            }

    t68().Wait()

module ``Check after code may include closures`` =
    let makeStateMachine x = 
        __stateMachine<int, int>
            (MoveNextMethodImpl<_>(fun sm -> 
                if __useResumableCode then
                    let y = 1 - x
                    if x > 3 then 
                        sm.Data <- 1 + x 
                    else 
                        sm.Data <- y
                else
                    sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
                )) 
            (SetStateMachineMethodImpl<_>(fun sm state -> ()))
            (AfterCode<_,_>(fun smref -> 
                let sm = smref // deref the byref so we can rvalue copy-capture the state machine
                let f (x:int ) = 
                    printfn "large closure"
                    printfn "large closure"
                    printfn "large closure"
                    printfn "large closure"
                    let mutable sm = sm 
                    MoveOnce(&sm)
                f 3))
    check "vwelvewl" (makeStateMachine 3) -2

#if TESTS_AS_APP
let RUN() = !failures
#else
let aa =
  match !failures with 
  | [] -> 
      stdout.WriteLine "Test Passed"
      System.IO.File.WriteAllText("test.ok","ok")
      exit 0
  | _ -> 
      stdout.WriteLine "Test Failed"
      exit 1
#endif

