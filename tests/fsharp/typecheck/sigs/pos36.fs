module Pos36

// Variation on test case mentioned in https://github.com/dotnet/fsharp/pull/6805#issuecomment-580368303
//
// See also pos35.fs, neg125.fs, neg127.fs
// 
// This no longer passes a dummy ^output but keeps ^output as a witness selector
//
// With RFC FS-1043 this now passes with the use of AllowOverloadByReturnType

module Positive_SelectOverloadedWitnessBasedOnReturnTypeWithoutPassingDummyArgument = 
    open System
    open System.Numerics
    let _uint8max = bigint (uint32 Byte.MaxValue)
    let _uint16max = bigint (uint32 UInt16.MaxValue)
    let _uint32max = bigint UInt32.MaxValue
    let _uint64max = bigint UInt64.MaxValue
    type witnesses = 
      [<AllowOverloadByReturnType>]
      static member inline convert_witness (x : bigint) = int (uint32 (x &&& _uint32max))
      [<AllowOverloadByReturnType>]
      static member inline convert_witness (x : bigint) = int64 (uint64 (x &&& _uint64max))
      [<AllowOverloadByReturnType>]
      static member inline convert_witness (x : bigint) = x
      [<AllowOverloadByReturnType>]
      static member inline convert_witness (x : bigint) = float x
      [<AllowOverloadByReturnType>]
      static member inline convert_witness (x : bigint) = sbyte (byte (x &&& _uint8max))
      [<AllowOverloadByReturnType>]
      static member inline convert_witness (x : bigint) = int16 (uint16 (x &&& _uint16max))
      [<AllowOverloadByReturnType>]
      static member inline convert_witness (x : bigint) = byte (x &&& _uint8max)
      [<AllowOverloadByReturnType>]
      static member inline convert_witness (x : bigint) = uint16 (x &&& _uint16max)
      [<AllowOverloadByReturnType>]
      static member inline convert_witness (x : bigint) = uint32 (x &&& _uint32max)
      [<AllowOverloadByReturnType>]
      static member inline convert_witness (x : bigint) = uint64 (x &&& _uint64max)
      [<AllowOverloadByReturnType>]
      static member inline convert_witness (x : bigint) = float32 x
      [<AllowOverloadByReturnType>]
      static member inline convert_witness (x : bigint) = decimal x
      [<AllowOverloadByReturnType>]
      static member inline convert_witness (x : bigint) = Complex(float x, 0.0)

    let inline call_convert_witness< ^witnesses, ^input, ^output when (^witnesses or ^input or ^output) : (static member convert_witness : ^input -> ^output)> (b : ^input) =
        ((^witnesses or ^input or ^output) : (static member convert_witness : ^input -> ^output) (b))

    let inline convert num =
      call_convert_witness<witnesses, _, _> (num)
    // These all cause errors 
    let v1 : int32 = convert 0I
    let v2 : int64 = convert 0I
    let v3 : bigint = convert 0I
    let v4 : float = convert 0I
    let v5 : sbyte = convert 0I
    let v6 : int16 = convert 0I
    let v7 : byte = convert 0I
    let v8 : uint16 = convert 0I
    let v9 : uint32 = convert 0I
    let v10 : uint64 = convert 0I
    let v11 : float32 = convert 0I
    let v12 : decimal = convert 0I
    let v13 : Complex = convert 0I
