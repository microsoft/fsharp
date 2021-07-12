// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.ErrorMessages

#if NETCOREAPP
open Xunit
open FSharp.Test.Utilities.Compiler

module ``Unsupported Attributes`` =

    [<Fact>]
    let ``Warn successfully`` () =
        """
open System.Runtime.CompilerServices
let f (w, [<CallerArgumentExpression "w">] x : string) = ()
let [<ModuleInitializer>] g () = ()
type C() =
    member _.F (w, [<System.Runtime.CompilerServices.CallerArgumentExpression "w">] x : string) = ()
    [<System.Runtime.CompilerServices.ModuleInitializer>]
    member _.G() = ()
        """
        |> FSharp
        |> typecheck
        |> shouldFail
        |> withResults [
            { Error = Warning 202
              Range = { StartLine = 3
                        StartColumn = 13
                        EndLine = 3
                        EndColumn = 37 }
              Message =
               "This attribute is currently unsupported by the F# compiler. Applying it will not achieve its intended effect." }
            { Error = Warning 202
              Range = { StartLine = 4
                        StartColumn = 7
                        EndLine = 4
                        EndColumn = 24 }
              Message =
               "This attribute is currently unsupported by the F# compiler. Applying it will not achieve its intended effect." }
            { Error = Warning 202
              Range = { StartLine = 6
                        StartColumn = 22
                        EndLine = 6
                        EndColumn = 78 }
              Message =
               "This attribute is currently unsupported by the F# compiler. Applying it will not achieve its intended effect." }
            { Error = Warning 202
              Range = { StartLine = 7
                        StartColumn = 7
                        EndLine = 7
                        EndColumn = 56 }
              Message =
               "This attribute is currently unsupported by the F# compiler. Applying it will not achieve its intended effect." }
        ]
#endif