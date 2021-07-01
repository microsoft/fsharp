// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.EditorServices

open FSharp.Compiler.Tokenization

/// Qualified long name.
type public PartialLongName =
    {
      /// Qualifying idents, prior to the last dot, not including the last part.
      QualifyingIdents: string list

      /// Last part of long ident.
      PartialIdent: string

      /// The column number at the end of full partial name.
      EndColumn: int

      /// Position of the last dot.
      LastDotPos: int option
    }
    
    /// Empty partial long name.
    static member Empty: endColumn: int -> PartialLongName

/// Methods for cheaply and inaccurately parsing F#.
///
/// These methods are very old and are mostly to do with extracting "long identifier islands" 
///     A.B.C
/// from F# source code, an approach taken from pre-F# VS samples for implementing intelliense.
///
/// This code should really no longer be needed since the language service has access to 
/// parsed F# source code ASTs.  However, the long identifiers are still passed back to GetDeclarations and friends in the 
/// F# Compiler Service and it's annoyingly hard to remove their use completely.
///
/// In general it is unlikely much progress will be made by fixing this code - it will be better to 
/// extract more information from the F# ASTs.
///
/// It's also surprising how hard even the job of getting long identifier islands can be. For example the code 
/// below is inaccurate for long identifier chains involving ``...`` identifiers.  And there are special cases
/// for active pattern names and so on.
module public QuickParse =

    /// Puts us after the last character.
    val MagicalAdjustmentConstant : int

    // Adjusts the token tag for the given identifier
    // - if we're inside active pattern name (at the bar), correct the token TAG to be an identifier
    val CorrectIdentifierToken : tokenText: string -> tokenTag: int -> int
    
    /// Given a string and a position in that string, find an identifier as
    /// expected by `GotoDefinition`. This will work when the cursor is
    /// immediately before the identifier, within the identifier, or immediately
    /// after the identifier.
    ///
    /// 'tolerateJustAfter' indicates that we tolerate being one character after the identifier, used
    /// for goto-definition
    ///
    /// In general, only identifiers composed from upper/lower letters and '.' are supported, but there
    /// are a couple of explicitly handled exceptions to allow some common scenarios:
    /// - When the name contains only letters and '|' symbol, it may be an active pattern, so we 
    ///   treat it as a valid identifier - e.g. let ( |Identity| ) a = a
    ///   (but other identifiers that include '|' are not allowed - e.g. '||' operator)
    /// - It searches for double tick (``) to see if the identifier could be something like ``a b``
    ///
    /// REVIEW: Also support, e.g., operators, performing the necessary mangling.
    /// (i.e., I would like that the name returned here can be passed as-is
    /// (post `.`-chopping) to `GetDeclarationLocation.)
    /// 
    /// In addition, return the position where a `.` would go if we were making
    /// a call to `DeclItemsForNamesAtPosition` for intellisense. This will
    /// allow us to use find the correct qualified items rather than resorting
    /// to the more expensive and less accurate environment lookup.
#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE || NO_CHECKNULLS
    val GetCompleteIdentifierIsland : tolerateJustAfter: bool -> lineStr: string -> index: int -> (string * int * bool) option
#else
    val GetCompleteIdentifierIsland : tolerateJustAfter: bool -> lineStr: string? -> index: int -> (string * int * bool) option
#endif
    
    /// Get the partial long name of the identifier to the left of index.
#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE || NO_CHECKNULLS
    val GetPartialLongName : lineStr: string * index: int -> (string list * string)
#else
    val GetPartialLongName : lineStr: string? * index: int -> (string list * string)
#endif
    
    /// Get the partial long name of the identifier to the left of index.
    /// For example, for `System.DateTime.Now` it returns PartialLongName ([|"System"; "DateTime"|], "Now", Some 32), where "32" pos of the last dot.
#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE || NO_CHECKNULLS
    val GetPartialLongNameEx : lineStr: string * index: int -> PartialLongName
#else
    val GetPartialLongNameEx : lineStr: string? * index: int -> PartialLongName
#endif
    
    /// Tests whether the user is typing something like "member x." or "override (*comment*) x."
    val TestMemberOrOverrideDeclaration : tokens: FSharpTokenInfo[] -> bool