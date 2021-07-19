
module Tests.OptionBuilderUsingInlineIfLambda

open System


    // let perf s f = 
    //     let t = System.Diagnostics.Stopwatch()
    //     t.Start()
    //     for i in 1 .. 100 do 
    //         f() |> ignore
    //     t.Stop()
    //     printfn "PERF: %s : %d" s t.ElapsedMilliseconds

    // printfn "check %d = %d = %d"(multiStepInlineIfLambdaBuilder()) (multiStepNoBuilder()) (multiStepOldBuilder())

    // perf "perf (state mechine option)" multiStepInlineIfLambdaBuilder 
    // perf "perf (no builder option)" multiStepNoBuilder 
    // perf "perf (slow builder option)" multiStepOldBuilder 

    // printfn "check %d = %d = %d" (multiStepInlineIfLambdaBuilderV()) (multiStepNoBuilder()) (multiStepOldBuilder())
    // perf "perf (state mechine voption)" multiStepInlineIfLambdaBuilderV
    // perf "perf (no builder voption)" multiStepNoBuilderV
    // perf "perf (slow builder voption)" multiStepOldBuilderV

