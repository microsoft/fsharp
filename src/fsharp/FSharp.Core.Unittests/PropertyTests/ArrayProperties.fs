﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

module FSharp.Core.PropertyTests.ArrayProperties

open System
open System.Collections.Generic

#if portable7 || portable78 || portable259

open NUnit.Framework
open FsCheck

let isStable sorted = sorted |> Seq.pairwise |> Seq.forall (fun ((ia, a),(ib, b)) -> if a = b then ia < ib else true)
    
let distinctByStable<'a when 'a : comparison> (xs : 'a []) =
    let indexed = xs |> Seq.indexed |> Seq.toArray
    let sorted = indexed |> Array.distinctBy snd
    isStable sorted
    
[<Test>]
let ``Seq.distinctBy is stable`` () =
    Check.QuickThrowOnFailure distinctByStable<int>
    Check.QuickThrowOnFailure distinctByStable<string>

#else
#endif