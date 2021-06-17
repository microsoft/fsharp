// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.UnitTests

open System
open FSharp.Compiler.Diagnostics
open NUnit.Framework
open FSharp.Test.Utilities

#if NETCOREAPP
[<TestFixture>]
module SpanTests =

    [<Test>]
    let Script_SpanForInDo() =
        let script = 
            """
open System

let test () : unit =
    let span = Span([|1;2;3;4|])
    let result = ResizeArray()
    for item in span do
        result.Add(item)
    
    if result.[0] <> 1 || result.[1] <> 2 || result.[2] <> 3 || result.[3] <> 4 then
        failwith "SpanForInDo didn't work properly"

test ()
            """
        
        CompilerAssert.RunScript script []
    [<Test>]
    let Script_SpanForInBoundsDo() =
        let script = 
            """
open System

let test () : unit =
    let span = Span([|1;2;3;4|])
    let result = ResizeArray()
    for i in 0 .. span.Length-1 do
        result.Add(span.[i])
    
    if result.[0] <> 1 || result.[1] <> 2 || result.[2] <> 3 || result.[3] <> 4 then
        failwith "SpanForInBoundsDo didn't work properly"

test ()
            """
        
        CompilerAssert.RunScript script []

    [<Test>]
    let Script_EmptySpanForInBoundsDo() =
        let script = 
            """
open System

let test () : unit =
    let span = Span([||])
    let result = ResizeArray()
    for i in 0 .. span.Length-1 do
        result.Add(span.[i])
    
    if result.Count <> 0 then
        failwith "EmptySpanForInBoundsDo didn't work properly"

test ()
            """
        
        CompilerAssert.RunScript script []

    [<Test>]
    let Script_ReadOnlySpanForInDo() =
        let script = 
            """
open System

let test () : unit =
    let span = ReadOnlySpan([|1;2;3;4|])
    let result = ResizeArray()
    for item in span do
        result.Add(item)

    if result.[0] <> 1 || result.[1] <> 2 || result.[2] <> 3 || result.[3] <> 4 then
        failwith "ReadOnlySpanForInDo didn't work properly"

test ()
            """
    
        // We expect this error until System.Reflection.Emit gets fixed for emitting `modreq` on method calls.
        // See: https://github.com/dotnet/corefx/issues/29254
        CompilerAssert.RunScript script [ "Method not found: '!0 ByRef System.ReadOnlySpan`1.get_Item(Int32)'." ]

    [<Test>]
    let Script_ReadOnlySpanForInBoundsDo() =
        let script = 
            """
open System

let test () : unit =
    let span = ReadOnlySpan([|1;2;3;4|])
    let result = ResizeArray()
    for i in 0 .. span.Length-1 do
        result.Add(span.[i])

    if result.[0] <> 1 || result.[1] <> 2 || result.[2] <> 3 || result.[3] <> 4 then
        failwith "Script_ReadOnlySpanForInBoundsDo didn't work properly"

test ()
            """
    
        // We expect this error until System.Reflection.Emit gets fixed for emitting `modreq` on method calls.
        // See: https://github.com/dotnet/corefx/issues/29254
        CompilerAssert.RunScript script [ "Method not found: '!0 ByRef System.ReadOnlySpan`1.get_Item(Int32)'."  ]


    [<Test>]
    let ``Invalid usage of type abbreviated span should fail to compile``() =
        CompilerAssert.TypeCheckWithErrors """
open System

type Bytes = ReadOnlySpan<byte>

type Test() =

    member _.M1 (data: Bytes) =
        let x = 
            if false then
                failwith ""
            else
                data
        x

    member _.M2 (data: Bytes) =
        let x = 
            if false then
                failwithf ""
            else
                data
        x

let test () =
    let span = ReadOnlySpan<_>(Array.empty)
    let result = Test().M1(span)
    let result = Test().M2(span)
    0
            """
            [|
                FSharpDiagnosticSeverity.Error, 412, (11, 17, 11, 28), "A type instantiation involves a byref type. This is not permitted by the rules of Common IL."
                FSharpDiagnosticSeverity.Error, 412, (19, 17, 19, 29), "A type instantiation involves a byref type. This is not permitted by the rules of Common IL."
                FSharpDiagnosticSeverity.Error, 412, (19, 27, 19, 29), "A type instantiation involves a byref type. This is not permitted by the rules of Common IL."
            |]

    [<Test>]
    let ``Type abbreviation that boxes a span should fail to compile``() =
        CompilerAssert.TypeCheckWithErrors """
open System

type TA = Span<int> * Span<int>

let f (x: TA) = ()
            """
            [|
                FSharpDiagnosticSeverity.Error, 3300, (6, 8, 6, 9), "The parameter 'x' has an invalid type 'TA'. This is not permitted by the rules of Common IL."
            |]
#endif
