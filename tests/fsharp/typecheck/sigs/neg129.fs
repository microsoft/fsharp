module Neg129
// The code in this test starts to compile once FS-1043 is enabled.
// Variation on test case mentioned in https://github.com/dotnet/fsharp/pull/6805#issuecomment-580368303
//
// This removes ^output as a type selector for the witness, but continues to pass a dummy ^output
//
// This is sufficient to make nearly all resolutions go through except when we instantiate by input type alone
module Negative_SelectOverloadedWitnessBasedOnReturnTypeByPassingDummyArgumentNoOutputSelector = 
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

    // These are ok
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

    // These are ok
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

    // This gives an error, because all selector types are known and overload resolution kicks in
    let inline inst (num: bigint) : ^output = convert num
