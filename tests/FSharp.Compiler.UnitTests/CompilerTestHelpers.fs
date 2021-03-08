namespace FSharp.Compiler.UnitTests

[<AutoOpen>]
module CompilerTestHelpers =

    let (|Warning|_|) (exn: System.Exception) =
        match exn with
        | :? FSharp.Compiler.ErrorLogger.Error as e -> let n,d = e.Data0 in Some (n,d)
        | _ -> None
