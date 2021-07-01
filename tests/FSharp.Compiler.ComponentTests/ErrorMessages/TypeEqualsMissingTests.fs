// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.ErrorMessages

open Xunit
open FSharp.Test.Utilities
open FSharp.Compiler.Diagnostics

// Disabled this test, see https://github.com/dotnet/fsharp/commit/98dc1b09fa1ff15176998dc2d28c5b5c8d0f80b1#r43542750
//module ``Type definition missing equals`` =

//     [<Fact>]
//     let ``Missing equals in DU``() =
//         CompilerAssert.TypeCheckSingleError
//             """
// type X | A | B
//             """
//             FSharpDiagnosticSeverity.Error
//             3360 
//             (2, 8, 2, 9)
//             "Unexpected token in type definition. Expected '=' after the type 'X'."
