
module Neg132 

// This code starts failing to compile once RFC-1043 is enabled
//
// This is because the constraint relies on return types in the set of support types.
// 
// This leaves unexpected ambiguity in common code especially when the shape of internal collections is potentially
// ambiguous internally in function implementations.
//
// Prior to RFC-1043 this ambiguity was resolved by applying weak resolution eagerly.

// See https://github.com/fsharp/fslang-design/issues/435#issuecomment-584192749
let inline InvokeMap (mapping: ^F) (source: ^I) : ^R =  
    ((^I or ^R) : (static member Map : ^I * ^F ->  ^R) source, mapping)

// A simulated collection with a'Map' witness
type Coll<'T>(x: 'T) =
    member _.X = x
    static member Map (source: Coll<'a>, mapping: 'a->'b) : Coll<'b> = new Coll<'b>(mapping source.X)

// What's the return collection type of the inner `InvokeMap` call?  We only know once we apply weak resolution.
let inline AddTwice (x: Coll<'a>) (v: 'a) : Coll<'a> =
    InvokeMap ((+) v) (InvokeMap ((+) v) x)

let v1 = AddTwice (Coll(3)) 2
let v2 = AddTwice (Coll(3uy)) 2uy
