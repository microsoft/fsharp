// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Defines the framework for serializing and de-serializing TAST data structures as binary blobs for the F# metadata format.
module internal FSharp.Compiler.TypedTreePickle

open FSharp.Compiler.IO
open Internal.Utilities
open Internal.Utilities.Library
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TcGlobals

/// Represents deserialized data with a dangling set of CCU fixup thunks indexed by name
[<NoEquality; NoComparison>]
type PickledDataWithReferences<'RawData> =
    {
      /// The data that uses a collection of CcuThunks internally
      RawData: 'RawData

      /// The assumptions that need to be fixed up
      FixupThunks: CcuThunk []
    }

    member Fixup : (CcuReference -> CcuThunk) -> 'RawData

    /// Like Fixup but loader may return None, in which case there is no fixup.
    member OptionalFixup: (CcuReference -> CcuThunk option) -> 'RawData

/// The type of state written to by picklers
type WriterState

/// A function to pickle a value into a given stateful writer
type pickler<'T> = 'T -> WriterState -> unit

/// Serialize a byte
val internal p_byte : int -> WriterState -> unit

/// Serialize a boolean value
val internal p_bool : bool -> WriterState -> unit

/// Serialize an integer
val internal p_int : int -> WriterState -> unit

/// Serialize a string
val internal p_string : string -> WriterState -> unit

/// Serialize a lazy value (eagerly)
val internal p_lazy : pickler<'T> -> Lazy<'T> pickler

/// Serialize a tuple of data
val inline  internal p_tup2 : pickler<'T1> -> pickler<'T2> -> pickler<'T1 * 'T2>

/// Serialize a tuple of data
val inline  internal p_tup3 : pickler<'T1> -> pickler<'T2> -> pickler<'T3> -> pickler<'T1 * 'T2 * 'T3>

/// Serialize a tuple of data
val inline  internal p_tup4 : pickler<'T1> -> pickler<'T2> -> pickler<'T3> -> pickler<'T4> -> pickler<'T1 * 'T2 * 'T3 * 'T4>

/// Serialize an array of data
val internal p_array : pickler<'T> -> pickler<'T[]>

/// Serialize a namemap of data
val internal p_namemap : pickler<'T> -> pickler<NameMap<'T>>

/// Serialize a TAST constant
val internal p_const : pickler<Const>

/// Serialize a TAST value reference
val internal p_vref : string -> pickler<ValRef>

/// Serialize a TAST type or entity reference
val internal p_tcref : string -> pickler<TyconRef>

/// Serialize a TAST union case reference
val internal p_ucref : pickler<UnionCaseRef>

/// Serialize a TAST expression
val internal p_expr : pickler<Expr>

/// Serialize a TAST type
val internal p_ty : pickler<TType>

/// Serialize a TAST description of a compilation unit
val internal pickleCcuInfo : pickler<PickledCcuInfo>

/// Serialize an arbitrary object using the given pickler
val pickleObjWithDanglingCcus : inMem: bool -> file: string -> TcGlobals -> scope:CcuThunk -> pickler<'T> -> 'T -> ByteBuffer

/// The type of state unpicklers read from
type ReaderState

/// A function to read a value from a given state
type unpickler<'T> = ReaderState -> 'T

/// Deserialize a byte
val internal u_byte : ReaderState -> int

/// Deserialize a bool
val internal u_bool : ReaderState -> bool

/// Deserialize an integer
val internal u_int : ReaderState -> int

/// Deserialize a string
val internal u_string : ReaderState -> string

/// Deserialize a lazy value (eagerly)
val internal u_lazy : unpickler<'T> -> unpickler<Lazy<'T>>

/// Deserialize a tuple
val inline  internal u_tup2 : unpickler<'T2> -> unpickler<'T3> -> unpickler<'T2 * 'T3>

/// Deserialize a tuple
val inline  internal u_tup3 : unpickler<'T2> -> unpickler<'T3> -> unpickler<'T4> -> unpickler<'T2 * 'T3 * 'T4>

/// Deserialize a tuple
val inline  internal u_tup4 : unpickler<'T2> -> unpickler<'T3> -> unpickler<'T4> -> unpickler<'T5> -> unpickler<'T2 * 'T3 * 'T4 * 'T5>

/// Deserialize an array of values
val internal u_array : unpickler<'T> -> unpickler<'T[]>

/// Deserialize a namemap
val internal u_namemap : unpickler<'T> -> unpickler<NameMap<'T>>

/// Deserialize a TAST constant
val internal u_const : unpickler<Const>

/// Deserialize a TAST value reference
val internal u_vref : unpickler<ValRef>

/// Deserialize a TAST type reference
val internal u_tcref : unpickler<TyconRef>

/// Deserialize a TAST union case reference
val internal u_ucref : unpickler<UnionCaseRef>

/// Deserialize a TAST expression
val internal u_expr : unpickler<Expr>

/// Deserialize a TAST type
val internal u_ty : unpickler<TType>

/// Deserialize a TAST description of a compilation unit
val internal unpickleCcuInfo : ReaderState -> PickledCcuInfo

/// Deserialize an arbitrary object which may have holes referring to other compilation units
val internal unpickleObjWithDanglingCcus : file:string -> viewedScope:ILScopeRef -> ilModule:ILModuleDef option -> ('T  unpickler) -> ReadOnlyByteMemory -> PickledDataWithReferences<'T>
