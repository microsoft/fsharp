module App

type System.SByte with
    member inline a.widen_to_int16 () : int16 = int16 a
    member inline a.widen_to_int32 () : int32 = int32 a
    member inline a.widen_to_int64 () : int64 = int64 a
    member inline a.widen_to_nativeint () : nativeint = nativeint a
    member inline a.widen_to_single () : single = single a
    member inline a.widen_to_double () : double = double a

type System.Byte with
    member inline a.widen_to_int16 () : int16 = int16 a
    member inline a.widen_to_uint16 () : uint16 = uint16 a
    member inline a.widen_to_int32 () : int32 = int32 a
    member inline a.widen_to_uint32 () : uint32 = uint32 a
    member inline a.widen_to_int64 () : int64 = int64 a
    member inline a.widen_to_uint64 () : uint64 = uint64 a
    member inline a.widen_to_nativeint () : nativeint = nativeint a
    member inline a.widen_to_unativeint () : unativeint = unativeint a
    member inline a.widen_to_single () : single = single a
    member inline a.widen_to_double () : double = double a

type System.Int16 with
    member inline a.widen_to_int32 () : int32 = int32 a
    member inline a.widen_to_int64 () : int64 = int64 a
    member inline a.widen_to_nativeint () : nativeint = nativeint a
    member inline a.widen_to_single () : single = single a
    member inline a.widen_to_double () : double = double a

type System.UInt16 with
    member inline a.widen_to_int32 () : int32 = int32 a
    member inline a.widen_to_uint32 () : uint32 = uint32 a
    member inline a.widen_to_int64 () : int64 = int64 a
    member inline a.widen_to_uint64 () : uint64 = uint64 a
    member inline a.widen_to_nativeint () : nativeint = nativeint a
    member inline a.widen_to_unativeint () : unativeint = unativeint a
    member inline a.widen_to_single () : single = single a
    member inline a.widen_to_double () : double = double a

type System.Int32 with
    member inline a.widen_to_int64 () : int64 = int64 a
    member inline a.widen_to_nativeint () : nativeint = nativeint a
    member inline a.widen_to_single () : single = single a
    member inline a.widen_to_double () : double = double a

type System.UInt32 with
    member inline a.widen_to_int64 () : int64 = int64 a
    member inline a.widen_to_uint64 () : uint64 = uint64 a
    member inline a.widen_to_unativeint () : unativeint = unativeint a
    member inline a.widen_to_single () : single = single a
    member inline a.widen_to_double () : double = double a

type System.Int64 with
    member inline a.widen_to_double () : double = double a

type System.UInt64 with
    member inline a.widen_to_double () : double = double a

type System.IntPtr with
    member inline a.widen_to_int64 () : int64 = int64 a
    member inline a.widen_to_double () : double = double a

type System.UIntPtr with
    member inline a.widen_to_uint64 () : uint64 = uint64 a
    member inline a.widen_to_double () : double = double a

type System.Single with
    member inline a.widen_to_double () : double = double a

let inline widen_to_byte (x: ^T) : byte = (^T : (member widen_to_byte : unit -> byte) (x))
let inline widen_to_sbyte (x: ^T) : sbyte = (^T : (member widen_to_sbyte : unit -> sbyte) (x))
let inline widen_to_int16 (x: ^T) : int16 = (^T : (member widen_to_int16 : unit -> int16) (x))
let inline widen_to_uint16 (x: ^T) : uint16 = (^T : (member widen_to_uint16 : unit -> uint16) (x))
let inline widen_to_int32 (x: ^T) : int32 = (^T : (member widen_to_int32 : unit -> int32) (x))
let inline widen_to_uint32 (x: ^T) : uint32 = (^T : (member widen_to_uint32 : unit -> uint32) (x))
let inline widen_to_int64 (x: ^T) : int64 = (^T : (member widen_to_int64 : unit -> int64) (x))
let inline widen_to_uint64 (x: ^T) : uint64 = (^T : (member widen_to_uint64 : unit -> uint64) (x))
let inline widen_to_nativeint (x: ^T) : nativeint = (^T : (member widen_to_nativeint : unit -> nativeint) (x))
let inline widen_to_unativeint (x: ^T) : unativeint = (^T : (member widen_to_unativeint : unit -> unativeint) (x))
let inline widen_to_single (x: ^T) : single = (^T : (member widen_to_single : unit -> single) (x))
let inline widen_to_double (x: ^T) : double = (^T : (member widen_to_double : unit -> double) (x))

type System.Byte with
    static member inline (+)(a: byte, b: 'T) : byte = a + widen_to_byte b
    static member inline (+)(a: 'T, b: byte) : byte = widen_to_byte a + b

type System.SByte with
    static member inline (+)(a: sbyte, b: 'T) : sbyte = a + widen_to_sbyte b
    static member inline (+)(a: 'T, b: sbyte) : sbyte = widen_to_sbyte a + b

type System.Int16 with
    static member inline (+)(a: int16, b: 'T) : int16 = a + widen_to_int16 b
    static member inline (+)(a: 'T, b: int16) : int16 = widen_to_int16 a + b

type System.UInt16 with
    static member inline (+)(a: uint16, b: 'T) : uint16 = a + widen_to_uint16 b
    static member inline (+)(a: 'T, b: uint16) : uint16 = widen_to_uint16 a + b

type System.Int32 with
    static member inline (+)(a: int32, b: 'T) : int32 = a + widen_to_int32 b
    static member inline (+)(a: 'T, b: int32) : int32 = widen_to_int32 a + b

type System.UInt32 with
    static member inline (+)(a: uint32, b: 'T) : uint32 = a + widen_to_uint32 b
    static member inline (+)(a: 'T, b: uint32) : uint32 = widen_to_uint32 a + b

type System.Int64 with
    static member inline (+)(a: int64, b: 'T) : int64 = a + widen_to_int64 b
    static member inline (+)(a: 'T, b: int64) : int64 = widen_to_int64 a + b

type System.UInt64 with
    static member inline (+)(a: uint64, b: 'T) : uint64 = a + widen_to_uint64 b
    static member inline (+)(a: 'T, b: uint64) : uint64 = widen_to_uint64 a + b

type System.IntPtr with
    static member inline (+)(a: nativeint, b: 'T) : nativeint = a + widen_to_nativeint b
    static member inline (+)(a: 'T, b: nativeint) : nativeint = widen_to_nativeint a + b

type System.UIntPtr with
    static member inline (+)(a: unativeint, b: 'T) : unativeint = a + widen_to_unativeint b
    static member inline (+)(a: 'T, b: unativeint) : unativeint = widen_to_unativeint a + b

type System.Single with
    static member inline (+)(a: single, b: 'T) : single = a + widen_to_single b
    static member inline (+)(a: 'T, b: single) : single = widen_to_single a + b

type System.Double with
    static member inline (+)(a: double, b: 'T) : double = a + widen_to_double b
    static member inline (+)(a: 'T, b: double) : double = widen_to_double a + b

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

