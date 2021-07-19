// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// The Range and Pos types form part of the public API of FSharp.Compiler.Service
namespace FSharp.Compiler.Text

open System.Collections.Generic

/// An index into a global tables of filenames
type internal FileIndex = int32 

[<RequireQualifiedAccess>]
type internal RangeDebugPointKind =
    | None
    | While
    | For
    | Try
    | Binding
    | Finally

/// Represents a position in a file
[<Struct; CustomEquality; NoComparison>]
type Position =

    /// The line number for the position
    member Line: int

    /// The column number for the position
    member Column: int

    /// The encoding of the position as a 64-bit integer
    member internal Encoding: int64

    /// Decode a position fro a 64-bit integer
    static member internal Decode: int64 -> pos

    /// The maximum number of bits needed to store an encoded position 
    static member internal EncodingSize: int

/// Represents a position in a file
and pos = Position

/// Represents a range within a file
[<Struct; CustomEquality; NoComparison>]
type Range =

    /// The start line of the range
    member StartLine: int

    /// The start column of the range
    member StartColumn: int

    /// The line number for the end position of the range
    member EndLine: int

    /// The column number for the end position of the range
    member EndColumn: int

    /// The start position of the range
    member Start: pos

    /// The end position of the range
    member End: pos

    /// The empty range that is located at the start position of the range
    member StartRange: range

    /// The empty range that is located at the end position of the range
    member EndRange: range

    /// The file index for the range
    member internal FileIndex: int

    /// The file name for the file of the range
    member FileName: string

    /// Synthetic marks ranges which are produced by intermediate compilation phases. This
    /// bit signifies that the range covers something that should not be visible to language
    /// service operations like dot-completion.
    member IsSynthetic: bool 

    /// When de-sugaring computation expressions we convert a debug point into a plain range, and then later
    /// recover that the range definitely indicates a debug point.
    member internal DebugPointKind: RangeDebugPointKind

    /// Convert a range to be synthetic
    member internal MakeSynthetic: unit -> range

    /// Note that a range indicates a debug point
    member internal NoteDebugPoint: kind: RangeDebugPointKind -> range

    /// Convert a range to string
    member internal ToShortString: unit -> string

    /// The range where all values are zero
    static member Zero: range
  
/// Represents a range within a file
and range = Range

/// Represents a line number when using zero-based line counting (used by Visual Studio)
#if CHECK_LINE0_TYPES
// Visual Studio uses line counts starting at 0, F# uses them starting at 1 
[<Measure>] type ZeroBasedLineAnnotation

type Line0 = int<ZeroBasedLineAnnotation>
#else
type Line0 = int
#endif

/// Represents a position using zero-based line counting (used by Visual Studio)
type Position01 = Line0 * int

/// Represents a range using zero-based line counting (used by Visual Studio)
type Range01 = Position01 * Position01

module Position =
    /// Create a position for the given line and column
    val mkPos: line:int -> column:int -> pos

    /// Compare positions for less-than
    val posLt: pos -> pos -> bool

    /// Compare positions for greater-than
    val posGt: pos -> pos -> bool

    /// Compare positions for equality
    val posEq: pos -> pos -> bool

    /// Compare positions for greater-than-or-equal-to
    val posGeq: pos -> pos -> bool

    /// Convert a position from zero-based line counting (used by Visual Studio) to one-based line counting (used internally in the F# compiler and in F# error messages) 
    val fromZ: line:Line0 -> column:int -> pos

    /// Convert a position from one-based line counting (used internally in the F# compiler and in F# error messages) to zero-based line counting (used by Visual Studio)
    val toZ: pos -> Position01

    /// Output a position
    val outputPos: System.IO.TextWriter -> pos -> unit

    /// Convert a position to a string
    val stringOfPos: pos   -> string

    /// The zero position
    val pos0: pos

module internal FileIndex =

    /// Convert a file path to an index
    val fileIndexOfFile: filePath: string -> FileIndex

    /// Convert an index into a file path
    val fileOfFileIndex: FileIndex -> string

    val startupFileName: string

module Range =

    /// Ordering on positions
    val posOrder: IComparer<pos>

    /// This view of range marks uses file indexes explicitly 
    val mkFileIndexRange: FileIndex -> pos -> pos -> range

    /// This view hides the use of file indexes and just uses filenames 
    val mkRange: string -> pos -> pos -> range

    /// Make a range for the first non-whitespace line of the file if any. Otherwise use line 1 chars 0-80.
    /// This involves reading the file.
    val mkFirstLineOfFile: string -> range

    val equals: range -> range -> bool

    /// Reduce a range so it only covers a line
    val trimRangeToLine: range -> range

    /// not a total order, but enough to sort on ranges 
    val rangeOrder: IComparer<range>

    /// Output a range
    val outputRange: System.IO.TextWriter -> range -> unit

    /// Union two ranges, taking their first occurring start position and last occurring end position
    val unionRanges: range -> range -> range

    /// Test to see if one range contains another range
    val rangeContainsRange: range -> range -> bool

    /// Test to see if a range contains a position
    val rangeContainsPos: range -> pos -> bool

    /// Test to see if a range occurs fully before a position
    val rangeBeforePos: range -> pos -> bool

    /// Make a dummy range for a file
    val rangeN: string -> int -> range

    /// The zero range
    val range0: range

    /// A range associated with a dummy file called "startup"
    val rangeStartup: range

    /// A range associated with a dummy file for the command line arguments
    val rangeCmdArgs: range
 
    /// Convert a range to a string
    val stringOfRange: range -> string

    /// Convert a range from one-based line counting (used internally in the F# compiler and in F# error messages) to zero-based line counting (used by Visual Studio)
    val toZ: range -> Range01

    /// Convert a range from one-based line counting (used internally in the F# compiler and in F# error messages) to zero-based line counting (used by Visual Studio)
    val toFileZ: range -> string * Range01

    /// Equality comparer for range.
    val comparer: IEqualityComparer<range>

/// Functions related to converting between lines indexed at 0 and 1
module Line =

    /// Convert a line number from zero-based line counting (used by Visual Studio) to one-based line counting (used internally in the F# compiler and in F# error messages) 
    val fromZ: Line0 -> int

    /// Convert a line number from one-based line counting (used internally in the F# compiler and in F# error messages) to zero-based line counting (used by Visual Studio)
    val toZ: int -> Line0 


