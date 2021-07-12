// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.
namespace FSharp.Compiler.UnitTests

open Xunit
open FSharp.Test.Utilities
open Internal.Utilities.Library

module BlockTests =

    [<Fact>]
    let ``Iter should work correctly``() =
        let b = Block.init 5 id

        let results = ResizeArray()
        b
        |> Block.iter (fun x ->
            results.Add(x)
        )

        Assert.Equal(
            [
                0
                1
                2
                3
                4
            ],
            results
        )

    [<Fact>]
    let ``Map should work correctly``() =
        let b = Block.init 5 id

        let b2 = b |> Block.map (fun x -> x + 1)

        Assert.Equal(
            [
                1
                2
                3
                4
                5
            ],
            b2
        )

    [<Fact>]
    let ``Fold should work correctly``() =
        let b = Block.init 5 id

        let result =
            (0, b)
            ||> Block.fold (fun state n ->
                state + n
            )

        Assert.Equal(10, result)