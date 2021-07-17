module Pos35

// Variation on test case mentioned in https://github.com/dotnet/fsharp/pull/6805#issuecomment-580368303
//
// We are selecting an overloaded witness based on input type
module SelectOverloadedWitnessBasedOnInputType = 
    type witnesses = 
      static member inline unsigned_witness (x : sbyte) = uint8 x
      static member inline unsigned_witness (x : byte) = x
      static member inline unsigned_witness (x : int16) = uint16 x
      static member inline unsigned_witness (x : uint16) = x
      static member inline unsigned_witness (x : int32) = uint32 x
      static member inline unsigned_witness (x : uint32) = x
      static member inline unsigned_witness (x : int64) = uint64 x
      static member inline unsigned_witness (x : uint64) = x

    // Note, this doesn't try to use the output to select
    let inline call_unsigned_witness< ^witnesses, ^input, ^output when (^witnesses or ^input) : (static member unsigned_witness : ^input -> ^output)> (x : ^input) =
      ((^witnesses or ^input) : (static member unsigned_witness : ^input -> ^output) x)

    // unsigned: ^a -> ^b
    let inline unsigned num = call_unsigned_witness<witnesses, _, _> num
    let v1 = unsigned 0y
    let v2 = unsigned 0s
    let v3 = unsigned 0
    let v4 = unsigned 0L

    let f1 : int8 -> uint8 = unsigned
    let f2 : int16 -> uint16 = unsigned
    let f3 : int32 -> uint32 = unsigned
    let f4 : int64 -> uint64 = unsigned

    let g1 : int8 -> _ = unsigned
    let g2 : int16 -> _ = unsigned
    let g3 : int32 -> _ = unsigned
    let g4 : int64 -> _ = unsigned

    // Negative cases - not enough information here - see neg124.fs which checks these
    //let h1 : _ -> uint8 = unsigned
    //let h2 : _ -> uint16 = unsigned
    //let h3 : _ -> uint32 = unsigned
    //let h4 : _ -> uint64 = unsigned


// Variation on the previous test case
//
// Note, this adds output as a selector though that shouldn't make any difference
module SelectOverloadedWitnessBasedOnInputTypePlusNeedlessOutputTypeSelector = 
    type witnesses = 
      static member inline unsigned_witness (x : sbyte) = uint8 x
      static member inline unsigned_witness (x : byte) = x
      static member inline unsigned_witness (x : int16) = uint16 x
      static member inline unsigned_witness (x : uint16) = x
      static member inline unsigned_witness (x : int32) = uint32 x
      static member inline unsigned_witness (x : uint32) = x
      static member inline unsigned_witness (x : int64) = uint64 x
      static member inline unsigned_witness (x : uint64) = x

    let inline call_unsigned_witness< ^witnesses, ^input, ^output when (^witnesses or ^input or ^output) : (static member unsigned_witness : ^input -> ^output)> (x : ^input) =
      ((^witnesses or ^input or ^output) : (static member unsigned_witness : ^input -> ^output) x)

    // unsigned: ^a -> ^b
    let inline unsigned num = call_unsigned_witness<witnesses, _, _> num
    let v1 = unsigned 0y
    let v2 = unsigned 0s
    let v3 = unsigned 0
    let v4 = unsigned 0L

    let f1 : int8 -> uint8 = unsigned
    let f2 : int16 -> uint16 = unsigned
    let f3 : int32 -> uint32 = unsigned
    let f4 : int64 -> uint64 = unsigned

    let g1 : int8 -> _ = unsigned
    let g2 : int16 -> _ = unsigned
    let g3 : int32 -> _ = unsigned
    let g4 : int64 -> _ = unsigned

// Variation on test case mentioned in https://github.com/dotnet/fsharp/pull/6805#issuecomment-580368303
module SelectOverloadedWitnessBasedOnReturnTypeByPassingDummyArgumentAndUsingOutputSelector = 
    open System
    open System.Numerics
    let _uint8max = bigint (uint32 Byte.MaxValue)
    let _uint16max = bigint (uint32 UInt16.MaxValue)
    let _uint32max = bigint UInt32.MaxValue
    let _uint64max = bigint UInt64.MaxValue
    type witnesses = 
      static member inline convert_witness (x : bigint, _output : int32) = int (uint32 (x &&& _uint32max))
      static member inline convert_witness (x : bigint, _output : int64) = int64 (uint64 (x &&& _uint64max))
      static member inline convert_witness (x : bigint, _output : bigint) = x
      static member inline convert_witness (x : bigint, _output : float) = float x
      static member inline convert_witness (x : bigint, _output : sbyte) = sbyte (byte (x &&& _uint8max))
      static member inline convert_witness (x : bigint, _output : int16) = int16 (uint16 (x &&& _uint16max))
      static member inline convert_witness (x : bigint, _output : byte) = byte (x &&& _uint8max)
      static member inline convert_witness (x : bigint, _output : uint16) = uint16 (x &&& _uint16max)
      static member inline convert_witness (x : bigint, _output : uint32) = uint32 (x &&& _uint32max)
      static member inline convert_witness (x : bigint, _output : uint64) = uint64 (x &&& _uint64max)
      static member inline convert_witness (x : bigint, _output : float32) = float32 x
      static member inline convert_witness (x : bigint, _output : decimal) = decimal x
      static member inline convert_witness (x : bigint, _output : Complex) = Complex(float x, 0.0)

    let inline call_convert_witness< ^witnesses, ^input, ^output when (^witnesses or ^input or ^output) : (static member convert_witness : ^input * ^output -> ^output)> (b : ^input, c : ^output) =
        ((^witnesses or ^input or ^output) : (static member convert_witness : ^input * ^output -> ^output) (b, c))

    let inline convert num =
      call_convert_witness<witnesses, _, _> (num, Unchecked.defaultof<'b>)

    let v1 : int32 = convert 777I
    let v2 : int64 = convert 777I
    let v3 : bigint = convert 777I
    let v4 : float = convert 777I
    let v5 : sbyte = convert 777I
    let v6 : int16 = convert 777I
    let v7 : byte = convert 777I
    let v8 : uint16 = convert 777I
    let v9 : uint32 = convert 777I
    let v10 : uint64 = convert 777I
    let v11 : float32 = convert 777I
    let v12 : decimal = convert 777I
    let v13 : Complex = convert 777I

    // This is enough to determine the input as bigint because those are the only solutions available
    let f1 : _ -> int32 = convert
    let f2 : _ -> int64 = convert
    let f3 : _ -> bigint = convert
    let f4 : _ -> float = convert
    let f5 : _ -> sbyte = convert
    let f6 : _ -> int16 = convert
    let f7 : _ -> byte = convert
    let f8 : _ -> uint16 = convert
    let f9 : _ -> uint32 = convert
    let f10 : _ -> uint64 = convert
    let f11 : _ -> float32 = convert
    let f12 : _ -> decimal = convert
    let f13 : _ -> Complex = convert 

    // This is permitted because the ^output type is still a selector
    // 
    // The resulting type is like this:
    //
    //     val inline inst : num:bigint ->  ^output when (witnesses or bigint or  ^output) : (static member convert_witness : bigint * ^output -> ^output)
    let inline inst (num: bigint) : ^output = convert num
    let i1 : int32 = inst 777I
    let i2 : int64 = inst 777I
    let i3 : bigint = inst 777I
    let i4 : float = inst 777I
    let i5 : sbyte = inst 777I
    let i6 : int16 = inst 777I
    let i7 : byte = inst 777I
    let i8 : uint16 = inst 777I
    let i9 : uint32 = inst 777I
    let i10 : uint64 = inst 777I
    let i11 : float32 = inst 777I
    let i12 : decimal = inst 777I
    let i13 : Complex = inst 777I

// Variation on test case mentioned in https://github.com/dotnet/fsharp/pull/6805#issuecomment-580368303
// This removes ^output as a type selector for the witness, but continues to pass a dummy ^output
//
// This is sufficient to make the resolutions go through
module SelectOverloadedWitnessBasedOnReturnTypeByPassingDummyArgumentNoOutputSelector = 
    open System
    open System.Numerics
    let _uint8max = bigint (uint32 Byte.MaxValue)
    let _uint16max = bigint (uint32 UInt16.MaxValue)
    let _uint32max = bigint UInt32.MaxValue
    let _uint64max = bigint UInt64.MaxValue
    type witnesses = 
      static member inline convert_witness (x : bigint, _output : int32) = int (uint32 (x &&& _uint32max))
      static member inline convert_witness (x : bigint, _output : int64) = int64 (uint64 (x &&& _uint64max))
      static member inline convert_witness (x : bigint, _output : bigint) = x
      static member inline convert_witness (x : bigint, _output : float) = float x
      static member inline convert_witness (x : bigint, _output : sbyte) = sbyte (byte (x &&& _uint8max))
      static member inline convert_witness (x : bigint, _output : int16) = int16 (uint16 (x &&& _uint16max))
      static member inline convert_witness (x : bigint, _output : byte) = byte (x &&& _uint8max)
      static member inline convert_witness (x : bigint, _output : uint16) = uint16 (x &&& _uint16max)
      static member inline convert_witness (x : bigint, _output : uint32) = uint32 (x &&& _uint32max)
      static member inline convert_witness (x : bigint, _output : uint64) = uint64 (x &&& _uint64max)
      static member inline convert_witness (x : bigint, _output : float32) = float32 x
      static member inline convert_witness (x : bigint, _output : decimal) = decimal x
      static member inline convert_witness (x : bigint, _output : Complex) = Complex(float x, 0.0)

    let inline call_convert_witness< ^witnesses, ^input, ^output when (^witnesses or ^input) : (static member convert_witness : ^input * ^output -> ^output)> (b : ^input, c : ^output) =
        ((^witnesses or ^input) : (static member convert_witness : ^input * ^output -> ^output) (b, c))

    let inline convert num =
      call_convert_witness<witnesses, _, _> (num, Unchecked.defaultof<'b>)

    let v1 : int32 = convert 777I
    let v2 : int64 = convert 777I
    let v3 : bigint = convert 777I
    let v4 : float = convert 777I
    let v5 : sbyte = convert 777I
    let v6 : int16 = convert 777I
    let v7 : byte = convert 777I
    let v8 : uint16 = convert 777I
    let v9 : uint32 = convert 777I
    let v10 : uint64 = convert 777I
    let v11 : float32 = convert 777I
    let v12 : decimal = convert 777I
    let v13 : Complex = convert 777I

    // This is enough to determine the input as bigint because those are the only solutions available
    let f1 : _ -> int32 = convert
    let f2 : _ -> int64 = convert
    let f3 : _ -> bigint = convert
    let f4 : _ -> float = convert
    let f5 : _ -> sbyte = convert
    let f6 : _ -> int16 = convert
    let f7 : _ -> byte = convert
    let f8 : _ -> uint16 = convert
    let f9 : _ -> uint32 = convert
    let f10 : _ -> uint64 = convert
    let f11 : _ -> float32 = convert
    let f12 : _ -> decimal = convert
    let f13 : _ -> Complex = convert 

    // Adding this gives an error, see neg129.fs for the test for this
    // let inline inst (num: bigint) : ^output = convert num
    
// Variation on test case mentioned in https://github.com/dotnet/fsharp/pull/6805#issuecomment-580368303
// 
// Same as SelectOverloadedWitnessBasedOnReturnTypeByPassingDummyArgumentNoOutputSelector but the output type
// parameter is generic 'output rather than SRTP ^output
//

module SelectOverloadedWitnessBasedOnReturnTypeByPassingDummyArgumentGenericOutputType = 
    open System
    open System.Numerics
    let _uint8max = bigint (uint32 Byte.MaxValue)
    let _uint16max = bigint (uint32 UInt16.MaxValue)
    let _uint32max = bigint UInt32.MaxValue
    let _uint64max = bigint UInt64.MaxValue
    type witnesses = 
      static member inline convert_witness (x : bigint, _output : int32) = int (uint32 (x &&& _uint32max))
      static member inline convert_witness (x : bigint, _output : int64) = int64 (uint64 (x &&& _uint64max))
      static member inline convert_witness (x : bigint, _output : bigint) = x
      static member inline convert_witness (x : bigint, _output : float) = float x
      static member inline convert_witness (x : bigint, _output : sbyte) = sbyte (byte (x &&& _uint8max))
      static member inline convert_witness (x : bigint, _output : int16) = int16 (uint16 (x &&& _uint16max))
      static member inline convert_witness (x : bigint, _output : byte) = byte (x &&& _uint8max)
      static member inline convert_witness (x : bigint, _output : uint16) = uint16 (x &&& _uint16max)
      static member inline convert_witness (x : bigint, _output : uint32) = uint32 (x &&& _uint32max)
      static member inline convert_witness (x : bigint, _output : uint64) = uint64 (x &&& _uint64max)
      static member inline convert_witness (x : bigint, _output : float32) = float32 x
      static member inline convert_witness (x : bigint, _output : decimal) = decimal x
      static member inline convert_witness (x : bigint, _output : Complex) = Complex(float x, 0.0)

    let inline call_convert_witness< ^witnesses, ^input, 'output when (^witnesses or ^input) : (static member convert_witness : ^input * 'output -> 'output)> (b : ^input, c : 'output) =
        ((^witnesses or ^input) : (static member convert_witness : ^input * 'output -> 'output) (b, c))

    let inline convert num : 'output =
      call_convert_witness<witnesses, _, _> (num, Unchecked.defaultof<'output>)

    let v1 : int32 = convert 777I
    let v2 : int64 = convert 777I
    let v3 : bigint = convert 777I
    let v4 : float = convert 777I
    let v5 : sbyte = convert 777I
    let v6 : int16 = convert 777I
    let v7 : byte = convert 777I
    let v8 : uint16 = convert 777I
    let v9 : uint32 = convert 777I
    let v10 : uint64 = convert 777I
    let v11 : float32 = convert 777I
    let v12 : decimal = convert 777I
    let v13 : Complex = convert 777I

    // This is enough to determine the input as bigint because those are the only solutions available
    let f1 : _ -> int32 = convert
    let f2 : _ -> int64 = convert
    let f3 : _ -> bigint = convert
    let f4 : _ -> float = convert
    let f5 : _ -> sbyte = convert
    let f6 : _ -> int16 = convert
    let f7 : _ -> byte = convert
    let f8 : _ -> uint16 = convert
    let f9 : _ -> uint32 = convert
    let f10 : _ -> uint64 = convert
    let f11 : _ -> float32 = convert
    let f12 : _ -> decimal = convert
    let f13 : _ -> Complex = convert 

    // Adding this gives an error, see neg128.fs for the test for this
    // let inline inst (num: bigint) : 'output = convert num


// Reduced FSharpPlus tests case from https://github.com/dotnet/fsharp/pull/6805#issuecomment-580365649
module PositiveTestCase3 = 
    [<AutoOpen>]
    module Extensions =

        type Async<'T> with

            static member Quack (x:seq<Async<'T>>) : Async<seq<'T>> = failwith ""

        type Option<'T> with

            static member Quack (x: seq<option<'T>>) : option<seq<'T>> = failwith ""

    let inline CallQuack (x: ^a) : ^Output = (^a : (static member Quack : ^a -> ^Output) x)

    type Witnesses =

        static member inline QuackWitness (x: ^a, _output: ^Output, _impl: Witnesses) : ^Output = CallQuack x  
        static member inline QuackWitness (x: ref<_>, _output: ^Output, _impl: Witnesses)  : ^Output = Unchecked.defaultof<_>

    let inline CallQuackWitness (x: ^a, output: ^Output, witnesses: ^Witnesses) =
        ((^a or ^Output or ^Witnesses) : (static member QuackWitness : _*_*_ -> _) (x, output, witnesses))

    // This requires the rule "Weak resolution no longer forces overload resolution for SRTP constraints prior 
    // to generalizing inline code" that is part of RFC FS-1043
    let inline call (x: seq< ^b >  ) : ^Output = 
        CallQuackWitness (x, Unchecked.defaultof< ^Output >, Unchecked.defaultof<Witnesses>) 



