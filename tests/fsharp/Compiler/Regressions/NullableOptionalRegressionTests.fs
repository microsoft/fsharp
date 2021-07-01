// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.UnitTests

open NUnit.Framework
open FSharp.Test.Utilities.Compiler

[<TestFixture>]
module NullableOptionalRegressionTests =

    //Disabled, see RFC for nullable
    //[<Test(Enabled=false)>]
    let ``Should compile with generic overloaded nullable methods``() =
        Fsx """
open System

type OverloadMeths =
    static member Map(m: 'T option, f) = Option.map f m
    static member Map(m: 'T when 'T:null, f) = m |> Option.ofObj |> Option.map f
    static member Map(m: 'T Nullable, f) = m |> Option.ofNullable |> Option.map f

[<AllowNullLiteral>]
type Node (child:Node)=
    new() = new Node(null)
    member val child:Node = child with get,set

let test () =
    let parent = Node()
    let b1 = OverloadMeths.Map(parent.child, fun x -> x.child)
    let c1 = OverloadMeths.Map(b1, fun x -> x.child)
    ()
        """
        |> withLangVersion50
        |> typecheck
        |> shouldSucceed
        |> ignore

    [<Test>]
    let ``Method should infer 'z' correctly``() =
        let fsSrc =
            """
namespace FSharpTest

open System

type Test() = class end

type Test with

    static member nullableE (encoder, x: Nullable<'a>) = if x.HasValue then encoder x.Value else Test()
    static member nullable codec z = Test.nullableE(codec, z)
            """
        FSharp fsSrc
        |> withLangVersionPreview
        |> typecheck
        |> shouldSucceed
        |> ignore

    [<Test>]
    let ``Method should infer correctly``() =
        let fsSrc =
            """
namespace FSharpTest

open System

type Test() = class end

type Test with

    static member nullableE encoder (x: Nullable<'a>) = if x.HasValue then encoder x.Value else Test()
    static member nullable codec = Test.nullableE codec
            """
        FSharp fsSrc
        |> withLangVersionPreview
        |> typecheck
        |> shouldSucceed
        |> ignore

    [<Test>]
    let ``Method should infer correctly 2``() =
        let fsSrc =
            """
namespace FSharpTest

open System

type Test() = class end

type Test with

    static member nullableE encoder (x: Nullable<int32>) = if x.HasValue then encoder x.Value else Test()
    static member nullable codec = Test.nullableE codec
            """
        FSharp fsSrc
        |> withLangVersionPreview
        |> typecheck
        |> shouldSucceed
        |> ignore
