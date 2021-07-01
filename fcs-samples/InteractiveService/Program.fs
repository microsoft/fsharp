open System
open System.IO
open FSharp.Compiler.Interactive.Shell

let sbOut = new Text.StringBuilder()
let sbErr = new Text.StringBuilder()
let argv = System.Environment.GetCommandLineArgs()

[<EntryPoint>]
let main (argv) = 
    let inStream = new StringReader("")
    let outStream = new StringWriter(sbOut)
    let errStream = new StringWriter(sbErr)
    
    let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()

    let fsiSession = 
        FsiEvaluationSession.Create
            (fsiConfig, 
             [| yield "fsi.exe"
                yield! argv
                yield "--noninteractive" |], inStream, outStream, errStream)
    
    while true do
        try 
            let text = Console.ReadLine()
            if text.StartsWith("=") then 
                match fsiSession.EvalExpression(text.Substring(1)) with
                | Some value -> printfn "%A" value.ReflectionValue
                | None -> printfn "Got no result!"
            else 
                fsiSession.EvalInteraction(text)
                printfn "Ok"
        with e -> 
            match e.InnerException with
            | Null -> printfn "Error evaluating expression (%s)" e.Message
            | NonNull err -> printfn "Error evaluating expression (%s)" err.Message
    // | _ -> printfn "Error evaluating expression (%s)" e.Message
    0