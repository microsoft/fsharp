module App

type System.SByte with
    [<AllowOverloadByReturnType>]
    static member inline widen (a: sbyte) : int16 = int16 a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: sbyte) : int32 = int32 a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: sbyte) : int64 = int64 a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: sbyte) : nativeint = nativeint a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: sbyte) : single = single a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: sbyte) : double = double a

type System.Byte with
    [<AllowOverloadByReturnType>]
    static member inline widen (a: byte) : int16 = int16 a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: byte) : uint16 = uint16 a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: byte) : int32 = int32 a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: byte) : uint32 = uint32 a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: byte) : int64 = int64 a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: byte) : uint64 = uint64 a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: byte) : nativeint = nativeint a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: byte) : unativeint = unativeint a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: byte) : single = single a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: byte) : double = double a

type System.Int16 with
    [<AllowOverloadByReturnType>]
    static member inline widen (a: int16) : int32 = int32 a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: int16) : int64 = int64 a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: int16) : nativeint = nativeint a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: int16) : single = single a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: int16) : double = double a

type System.UInt16 with
    [<AllowOverloadByReturnType>]
    static member inline widen (a: uint16) : int32 = int32 a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: uint16) : uint32 = uint32 a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: uint16) : int64 = int64 a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: uint16) : uint64 = uint64 a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: uint16) : nativeint = nativeint a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: uint16) : unativeint = unativeint a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: uint16) : single = single a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: uint16) : double = double a

type System.Int32 with
    [<AllowOverloadByReturnType>]
    static member inline widen (a: int32) : int64 = int64 a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: int32) : nativeint = nativeint a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: int32) : single = single a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: int32) : double = double a

type System.UInt32 with
    static member inline widen (a: uint32) : int64 = int64 a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: uint32) : uint64 = uint64 a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: uint32) : unativeint = unativeint a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: uint32) : single = single a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: uint32) : double = double a

type System.Int64 with
    [<AllowOverloadByReturnType>]
    static member inline widen (a: int64) : double = double a

type System.UInt64 with
    [<AllowOverloadByReturnType>]
    static member inline widen (a: uint64) : double = double a

type System.IntPtr with
    [<AllowOverloadByReturnType>]
    static member inline widen (a: nativeint) : int64 = int64 a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: nativeint) : double = double a

type System.UIntPtr with
    [<AllowOverloadByReturnType>]
    static member inline widen (a: unativeint) : uint64 = uint64 a
    [<AllowOverloadByReturnType>]
    static member inline widen (a: unativeint) : double = double a

type System.Single with
    [<AllowOverloadByReturnType>]
    static member inline widen (a: int) : double = double a

let inline widen (x: ^T) : ^U = ((^T or ^U) : (static member widen : ^T -> ^U) (x))


type System.Byte with
    static member inline (+)(a: byte, b: 'T) : byte = a + widen b
    static member inline (+)(a: 'T, b: byte) : byte = widen a + b

type System.SByte with
    static member inline (+)(a: sbyte, b: 'T) : sbyte = a + widen b
    static member inline (+)(a: 'T, b: sbyte) : sbyte = widen a + b

type System.Int16 with
    static member inline (+)(a: int16, b: 'T) : int16 = a + widen b
    static member inline (+)(a: 'T, b: int16) : int16 = widen a + b

type System.UInt16 with
    static member inline (+)(a: uint16, b: 'T) : uint16 = a + widen b
    static member inline (+)(a: 'T, b: uint16) : uint16 = widen a + b

type System.Int32 with
    static member inline (+)(a: int32, b: 'T) : int32 = a + widen b
    static member inline (+)(a: 'T, b: int32) : int32 = widen a + b

type System.UInt32 with
    static member inline (+)(a: uint32, b: 'T) : uint32 = a + widen b
    static member inline (+)(a: 'T, b: uint32) : uint32 = widen a + b

type System.Int64 with
    static member inline (+)(a: int64, b: 'T) : int64 = a + widen b
    static member inline (+)(a: 'T, b: int64) : int64 = widen a + b

type System.UInt64 with
    static member inline (+)(a: uint64, b: 'T) : uint64 = a + widen b
    static member inline (+)(a: 'T, b: uint64) : uint64 = widen a + b

type System.IntPtr with
    static member inline (+)(a: nativeint, b: 'T) : nativeint = a + widen b
    static member inline (+)(a: 'T, b: nativeint) : nativeint = widen a + b

type System.UIntPtr with
    static member inline (+)(a: unativeint, b: 'T) : unativeint = a + widen b
    static member inline (+)(a: 'T, b: unativeint) : unativeint = widen a + b

type System.Single with
    static member inline (+)(a: single, b: 'T) : single = a + widen b
    static member inline (+)(a: 'T, b: single) : single = widen a + b

type System.Double with
    static member inline (+)(a: double, b: 'T) : double = a + widen b
    static member inline (+)(a: 'T, b: double) : double = widen a + b
    
let table =
    (1y + 2y) |> ignore<sbyte>
    //(1y + 2uy)  |> ignore<int>
    (1y + 2s) |> ignore<int16>
    //1y + 2us |> ignore<uint16>
    (1y + 2)  |> ignore<int32>
    //(1y + 2u)  |> ignore<uint32>
    (1y + 2L)  |> ignore<int64>
    //1y + 2UL  |> ignore<uint64>
    (1y + 2n)  |> ignore<nativeint>
    //(1y + 2un)  |> ignore<unativeint>
    (1y + 2.0f)  |> ignore<single>
    (1y + 2.0)  |> ignore<double>

    //(1uy + 2y) |> ignore<sbyte>
    (1uy + 2uy)  |> ignore<byte>
    (1uy + 2s) |> ignore<int16>
    (1uy + 2us) |> ignore<uint16>
    (1uy + 2)  |> ignore<int>
    (1uy + 2u)  |> ignore<uint>
    (1uy + 2L)  |> ignore<int64>
    (1uy + 2UL)  |> ignore<uint64>
    (1uy + 2n)  |> ignore<nativeint>
    (1uy + 2un)  |> ignore<unativeint>
    (1uy + 2.0f)  |> ignore<single>
    (1uy + 2.0)  |> ignore<double>

    (1s + 2y) |> ignore<int16>
    (1s + 2uy)  |> ignore<int16>
    (1s + 2s) |> ignore<int16>
    //1s + 2us |> ignore<uint16>
    (1s + 2)  |> ignore<int32>
    //(1s + 2u)  |> ignore<uint32>
    (1s + 2L)  |> ignore<int64>
    //1s + 2UL  |> ignore<uint64>
    (1s + 2n)  |> ignore<nativeint>
    //(1s + 2un)  |> ignore<unativeint>
    (1s + 2.0f)  |> ignore<single>
    (1s + 2.0)  |> ignore<double>

    //(1us + 2y) |> ignore<sbyte>
    (1us + 2uy)  |> ignore<uint16>
    //(1us + 2s) |> ignore<int16>
    (1us + 2us) |> ignore<uint16>
    (1us + 2)  |> ignore<int32>
    (1us + 2u)  |> ignore<uint32>
    (1us + 2L)  |> ignore<int64>
    (1us + 2UL)  |> ignore<uint64>
    (1us + 2n)  |> ignore<nativeint>
    (1us + 2un)  |> ignore<unativeint>
    (1us + 2.0f)  |> ignore<single>
    (1us + 2.0)  |> ignore<double>

    (1 + 2y) |> ignore<int32>
    (1 + 2uy)  |> ignore<int32>
    (1 + 2s) |> ignore<int32>
    1 + 2us |> ignore<int32>
    (1 + 2)  |> ignore<int32>
    //(1 + 2u)  |> ignore<uint32>
    (1 + 2L)  |> ignore<int64>
    //1 + 2UL  |> ignore<uint64>
    (1 + 2n)  |> ignore<nativeint>
    //(1 + 2un)  |> ignore<unativeint>
    (1 + 2.0f)  |> ignore<single>
    (1 + 2.0)  |> ignore<double>

    //(1u + 2y) |> ignore<sbyte>
    (1u + 2uy)  |> ignore<uint32>
    //(1us + 2s) |> ignore<int16>
    (1u + 2us) |> ignore<uint32>
    //(1u + 2)  |> ignore<int32>
    (1u + 2u)  |> ignore<uint32>
    (1u + 2L)  |> ignore<int64>
    (1u + 2UL)  |> ignore<uint64>
    //(1u + 2n)  |> ignore<nativeint>
    (1u + 2un)  |> ignore<unativeint>
    (1u + 2.0f)  |> ignore<single>
    (1u + 2.0)  |> ignore<double>

    (1L + 2y) |> ignore<int64>
    (1L + 2uy)  |> ignore<int64>
    (1L + 2s) |> ignore<int64>
    (1L + 2us) |> ignore<int64>
    (1L + 2)  |> ignore<int64>
    //(1L + 2u)  // gives error
    (1L + 2L)  |> ignore<int64>
    //1L + 2UL  // gives error
    (1L + 2n)  |> ignore<int64>
    //(1L + 2un)  // gives error
    //(1L + 2.0f)  // gives error
    (1L + 2.0)  |> ignore<double>

    //(1u + 2y) // gives error
    (1UL + 2uy)  |> ignore<uint64>
    //(1us + 2s) // gives error
    (1UL + 2us) |> ignore<uint64>
    //(1u + 2)  // gives error
    (1UL + 2u)  |> ignore<uint64>
    //(1UL + 2L)  // gives error
    (1UL + 2UL)  |> ignore<uint64>
    //(1u + 2n)  // gives error
    (1UL + 2un)  |> ignore<uint64>
    //(1UL + 2.0f)  // gives error
    (1UL + 2.0)  |> ignore<double>

    (1n + 2y) |> ignore<nativeint>
    (1n + 2uy)  |> ignore<nativeint>
    (1n + 2s) |> ignore<nativeint>
    (1n + 2us) |> ignore<nativeint>
    (1n + 2)  |> ignore<nativeint>
    //(1n + 2u)  // gives error
    (1n + 2L)  |> ignore<int64>
    //1n + 2UL  // gives error
    (1n + 2n)  |> ignore<nativeint>
    //(1n + 2un)  // gives error
    //(1n + 2.0f)  // gives error
    (1n + 2.0)  |> ignore<double>

    //(1un + 2y) // gives error
    (1un + 2uy)  |> ignore<unativeint>
    //(1un + 2s) // gives error
    (1un + 2us) |> ignore<unativeint>
    //(1un + 2)  // gives error
    (1un + 2u)  |> ignore<unativeint>
    //(1un + 2L)  // gives error
    (1un + 2UL)  |> ignore<uint64>
    //(1un + 2n)  // gives error
    (1un + 2un)  |> ignore<unativeint>
    //(1un + 2.0f)  // gives error
    (1un + 2.0)  |> ignore<double>
    
let explicit_widen_calls =
    (System.SByte.widen 1y : int16) |> ignore<int16>
    (System.SByte.widen 1y : int32) |> ignore<int32>
    (System.SByte.widen 1y : int64) |> ignore<int64>
    (System.SByte.widen 1y : nativeint) |> ignore<nativeint>
    (System.SByte.widen 1y : double) |> ignore<double>
    (System.SByte.widen 1y : single) |> ignore<single>

    (System.Int16.widen 1s : int32) |> ignore<int32>
    (System.Int16.widen 1s : int64) |> ignore<int64>
    (System.Int16.widen 1s : nativeint) |> ignore<nativeint>
    (System.Int16.widen 1s : double) |> ignore<double>
    (System.Int16.widen 1s : single) |> ignore<single>
