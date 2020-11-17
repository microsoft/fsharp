// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

//----------------------------------------------------------------------------
// API to the compiler as an incremental service for parsing,
// type checking and intellisense-like environment-reporting.
//----------------------------------------------------------------------------

namespace FSharp.Compiler.SourceCodeServices

open FSharp.Compiler.Range
open FSharp.Compiler.SyntaxTree

/// Represents the location of a tupled argument, which can optionally be a named argument.
type TupledArgumentLocation = { IsNamedArgument: bool; ArgumentRange: range }

/// Represents the locations relevant to activating parameter info in an IDE
[<Sealed>]
type public FSharpNoteworthyParamInfoLocations =

    /// The text of the long identifier prior to the open-parentheses
    member LongId : string list

    /// The start location of long identifier prior to the open-parentheses
    member LongIdStartLocation : pos

    /// The end location of long identifier prior to the open-parentheses
    member LongIdEndLocation : pos

    /// The location of the open-parentheses
    member OpenParenLocation : pos

    /// The locations of commas and close parenthesis (or, last char of last arg, if no final close parenthesis)
    member TupleEndLocations : pos[]  

    /// Is false if either this is a call without parens "f x" or the parser recovered as in "f(x,y"
    member IsThereACloseParen : bool   

    /// Either empty or a name if an actual named parameter; f(0,a=4,?b=None) would be [|None; Some "a"; Some "b"|]
    member NamedParamNames : string option []

    /// Array of locations for each argument, and a flag if that argument is named
    member ArgumentLocations: TupledArgumentLocation []

    /// Find the information about parameter info locations at a particular source location
    static member Find : pos * ParsedInput -> FSharpNoteworthyParamInfoLocations option

module internal SynExprAppLocationsImpl =
    val getAllCurriedArgsAtPosition: pos: pos -> parseTree: ParsedInput -> range list option
