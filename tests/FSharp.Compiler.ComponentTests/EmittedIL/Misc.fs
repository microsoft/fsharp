// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.EmittedIL

open Xunit
open FSharp.Test.Utilities.Compiler

module ``Misc`` =
    [<Fact>]
    let ``Empty array construct compiles to System.Array.Empty<_>()``() =
        FSharp """
module Misc

let zInt (): int[] = [||]

let zString (): string[] = [||]

let zGeneric<'a> (): 'a[] = [||]
         """
         |> compile
         |> shouldSucceed
         |> verifyIL ["""
IL_0000:  call       !!0[] [runtime]System.Array::Empty<int32>()
IL_0005:  ret"""
                      """

IL_0000:  call       !!0[] [runtime]System.Array::Empty<string>()
IL_0005:  ret"""

                      """
IL_0000:  call       !!0[] [runtime]System.Array::Empty<!!0>()
IL_0005:  ret""" ]
