// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.EditorServices

open System
open Internal.Utilities.Library
open FSharp.Compiler.Syntax
open FSharp.Compiler.Tokenization

/// Qualified long name.
type PartialLongName =
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
    static member Empty(endColumn: int) = { QualifyingIdents = []; PartialIdent = ""; EndColumn = endColumn; LastDotPos = None }

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
module QuickParse =
    open PrettyNaming
    /// Puts us after the last character.
    let MagicalAdjustmentConstant = 1

    // Adjusts the token tag for the given identifier
    // - if we're inside active pattern name (at the bar), correct the token TAG to be an identifier
    let CorrectIdentifierToken (tokenText: string) (tokenTag: int) = 
        if tokenText.EndsWithOrdinal("|") then
            FSharp.Compiler.Parser.tagOfToken (FSharp.Compiler.Parser.token.IDENT tokenText)
        else tokenTag

    let rec isValidStrippedName (name: ReadOnlySpan<char>) idx = 
        if idx = name.Length then false
        elif IsIdentifierPartCharacter name.[idx] then true
        else isValidStrippedName name (idx + 1)

    // Utility function that recognizes whether a name is valid active pattern name
    // Extracts the 'core' part without surrounding bars and checks whether it contains some identifier
    // (Note, this doesn't have to be precise, because this is checked by background compiler,
    // but it has to be good enough to distinguish operators and active pattern names)
    let private isValidActivePatternName (name: string) = 

      // Strip the surrounding bars (e.g. from "|xyz|_|") to get "xyz"
      match name.StartsWithOrdinal("|"), name.EndsWithOrdinal("|_|"), name.EndsWithOrdinal("|") with
      | true, true, _ when name.Length > 4 -> isValidStrippedName (name.AsSpan(1, name.Length - 4)) 0
      | true, _, true when name.Length > 2 -> isValidStrippedName (name.AsSpan(1, name.Length - 2)) 0
      | _ -> false
    
#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE || NO_CHECKNULLS
    let GetCompleteIdentifierIslandImpl (lineStr: string) (index: int) : (string * int * bool) option =
#else
    let GetCompleteIdentifierIslandImpl (lineStr: string?) (index: int) : (string * int * bool) option =
#endif
        match lineStr with 
        | Null -> None
        | NonNull lineStr -> 
        if index < 0 || index >= lineStr.Length then None 
        else
            let fixup =
                match () with
                // at a valid position, on a valid character
                | _ when (index < lineStr.Length) && (lineStr.[index] = '|' || IsIdentifierPartCharacter lineStr.[index]) -> Some index
                | _ -> None // not on a word or '.'
            
            
            let (|Char|_|) p = if p >=0 && p < lineStr.Length then Some(lineStr.[p]) else None   
            let (|IsLongIdentifierPartChar|_|) c = if IsLongIdentifierPartCharacter c then Some () else None           
            let (|IsIdentifierPartChar|_|) c = if IsIdentifierPartCharacter c then Some () else None

            let rec searchLeft p =
                match (p - 1), (p - 2) with
                | Char '|', Char '[' -> p // boundary of array declaration - stop
                | Char '|', _ 
                | Char IsLongIdentifierPartChar, _ -> searchLeft (p - 1) // allow normal chars and '.'s
                | _ -> p

            let rec searchRight p =
                match (p + 1), (p + 2) with
                | Char '|', Char ']' -> p // boundary of array declaration - stop
                | Char '|', _
                | Char IsIdentifierPartChar, _ -> searchRight (p + 1) // allow only normal chars (stop at '.')
                | _ -> p                 
            
            let tickColsOpt = 
                let rec walkOutsideBackticks i =
                    if i >= lineStr.Length then None 
                    else
                    match i, i + 1 with
                    | Char '`', Char '`' ->
                        // dive into backticked part
                        // if pos = i then it will be included in backticked range ($``identifier``)
                        walkInsideBackticks (i + 2) i
                    | _, _ -> 
                        if i >= index then None
                        else
                            // we still not reached position p - continue walking 
                            walkOutsideBackticks (i + 1)
                and walkInsideBackticks i start = 
                    if i >= lineStr.Length then None // non-closed backticks
                    else
                    match i, i + 1 with
                    | Char '`', Char '`' ->
                        // found closing pair of backticks
                        // if target position is between start and current pos + 1 (entire range of escaped identifier including backticks) - return success
                        // else climb outside and continue walking
                        if index >= start && index < (i + 2) then Some (start, i)
                        else walkOutsideBackticks (i + 2)
                    | _, _ -> walkInsideBackticks (i + 1) start                    

                walkOutsideBackticks 0
            
            match tickColsOpt with
            | Some (prevTickTick, idxTickTick) ->
                // inside ``identifier`` (which can contain any characters!) so we try returning its location
                let pos = idxTickTick + 1 + MagicalAdjustmentConstant
                let ident = lineStr.Substring(prevTickTick, idxTickTick - prevTickTick + 2)
                Some(ident, pos, true)
            | _ ->
                // find location of an ordinary identifier
                fixup |> Option.bind (fun p ->
                    let l = searchLeft p
                    let r = searchRight p
                    let ident = lineStr.Substring (l, r - l + 1)
                    if ident.IndexOf('|') <> -1 && not(isValidActivePatternName(ident)) then None else
                        let pos = r + MagicalAdjustmentConstant 
                        Some(ident, pos, false)
                    )

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
    let GetCompleteIdentifierIsland (tolerateJustAfter: bool) (lineStr: string) (index: int) : (string * int * bool) option =
        if String.IsNullOrEmpty lineStr then None
        else     
            let directResult = GetCompleteIdentifierIslandImpl lineStr index
            if tolerateJustAfter && directResult = None then 
                GetCompleteIdentifierIslandImpl lineStr (index - 1)
            else 
                directResult

    let private defaultName = [], ""

    /// Get the partial long name of the identifier to the left of index.
#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE || NO_CHECKNULLS
    let GetPartialLongName(lineStr: string, index: int) =
#else
    let GetPartialLongName(lineStr: string?, index: int) =
#endif
        match lineStr with
        | Null -> defaultName
        | NonNull lineStr ->
        if index < 0 then defaultName
        elif index >= lineStr.Length then defaultName
        else
            let IsIdentifierPartCharacter pos = IsIdentifierPartCharacter lineStr.[pos]
            let IsLongIdentifierPartCharacter pos = IsLongIdentifierPartCharacter lineStr.[pos]
            let IsDot pos = lineStr.[pos] = '.'

            let rec InLeadingIdentifier(pos,right,(prior,residue)) = 
                let PushName() = ((lineStr.Substring(pos+1,right-pos-1)) :: prior),residue
                if pos < 0 then PushName()
                elif IsIdentifierPartCharacter pos then InLeadingIdentifier(pos-1,right,(prior,residue))
                elif IsDot pos then InLeadingIdentifier(pos-1,pos,PushName())
                else PushName()

            let rec InName(pos,startResidue,right) =
                let NameAndResidue() = 
                    [lineStr.Substring(pos+1,startResidue-pos-1)],(lineStr.Substring(startResidue+1,right-startResidue))
                if pos < 0 then [lineStr.Substring(pos+1,startResidue-pos-1)],(lineStr.Substring(startResidue+1,right-startResidue))
                elif IsIdentifierPartCharacter pos then InName(pos-1,startResidue,right) 
                elif IsDot pos then InLeadingIdentifier(pos-1,pos,NameAndResidue())
                else NameAndResidue()

            let rec InResidue(pos,right) =
                if pos < 0 then [],lineStr.Substring(pos+1,right-pos)
                elif IsDot pos then InName(pos-1,pos,right)
                elif IsLongIdentifierPartCharacter pos then InResidue(pos-1, right)
                else [],lineStr.Substring(pos+1,right-pos)
                
            let result = InResidue(index,index)
            result
    
    type private EatCommentCallContext =
        | SkipWhiteSpaces of ident: string * current: string list * throwAwayNext: bool
        | StartIdentifier of current: string list * throwAway: bool

    /// Get the partial long name of the identifier to the left of index.
    /// For example, for `System.DateTime.Now` it returns PartialLongName ([|"System"; "DateTime"|], "Now", Some 32), where "32" pos of the last dot.
#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE || NO_CHECKNULLS
    let GetPartialLongNameEx(lineStr: string, index: int) : PartialLongName =
#else
    let GetPartialLongNameEx(lineStr: string?, index: int) : PartialLongName =
#endif
        match lineStr with
        | Null -> PartialLongName.Empty(index)
        | NonNull lineStr ->
        if index < 0 then PartialLongName.Empty(index)
        elif index >= lineStr.Length then PartialLongName.Empty(index)
        else
            let IsIdentifierPartCharacter pos = IsIdentifierPartCharacter lineStr.[pos]
            let IsIdentifierStartCharacter pos = IsIdentifierPartCharacter pos
            let IsDot pos = lineStr.[pos] = '.'
            let IsTick pos = lineStr.[pos] = '`'
            let IsEndOfComment pos = pos < index - 1 && lineStr.[pos] = '*' && lineStr.[pos + 1] = ')'
            let IsStartOfComment pos = pos < index - 1 && lineStr.[pos] = '(' && lineStr.[pos + 1] = '*'
            let IsWhitespace pos = Char.IsWhiteSpace(lineStr.[pos])

            let rec SkipWhitespaceBeforeDotIdentifier(pos, ident, current, throwAwayNext, lastDotPos) =
                if pos > index then PartialLongName.Empty(index)  // we're in whitespace after an identifier, if this is where the cursor is, there is no PLID here
                elif IsWhitespace pos then SkipWhitespaceBeforeDotIdentifier(pos+1,ident,current,throwAwayNext,lastDotPos)
                elif IsDot pos then AtStartOfIdentifier(pos+1,ident :: current,throwAwayNext, Some pos)
                elif IsStartOfComment pos then EatComment(1, pos + 1, EatCommentCallContext.SkipWhiteSpaces(ident, current, throwAwayNext), lastDotPos)
                else AtStartOfIdentifier(pos,[],false,None) // Throw away what we have and start over.

            and EatComment (nesting, pos, callContext,lastDotPos) = 
                if pos > index then PartialLongName.Empty(index) else
                if IsStartOfComment pos then
                    // track balance of closing '*)'
                    EatComment(nesting + 1, pos + 2, callContext,lastDotPos)
                else
                if IsEndOfComment pos then
                    if nesting = 1 then 
                        // all right, we are at the end of comment, jump outside
                        match callContext with
                        | EatCommentCallContext.SkipWhiteSpaces(ident, current, throwAway) ->
                            SkipWhitespaceBeforeDotIdentifier(pos + 2, ident, current, throwAway,lastDotPos)
                        | EatCommentCallContext.StartIdentifier(current, throwAway) ->
                            AtStartOfIdentifier(pos + 2, current, throwAway,lastDotPos)
                    else 
                        // reduce level of nesting and continue
                        EatComment(nesting - 1, pos + 2, callContext, lastDotPos)
                else
                    // eat next char
                    EatComment(nesting, pos + 1, callContext, lastDotPos)

            and InUnquotedIdentifier(left:int,pos:int,current,throwAwayNext,lastDotPos) =
                if pos > index then 
                    if throwAwayNext then 
                        PartialLongName.Empty(index) 
                    else
                        { QualifyingIdents = current
                          PartialIdent = lineStr.Substring(left,pos-left)
                          EndColumn = index
                          LastDotPos = lastDotPos }
                else
                    if IsIdentifierPartCharacter pos then InUnquotedIdentifier(left,pos+1,current,throwAwayNext,lastDotPos)
                    elif IsDot pos then 
                        let ident = lineStr.Substring(left,pos-left)
                        AtStartOfIdentifier(pos+1,ident :: current,throwAwayNext, Some pos)
                    elif IsWhitespace pos || IsStartOfComment pos then 
                        let ident = lineStr.Substring(left,pos-left)
                        SkipWhitespaceBeforeDotIdentifier(pos, ident, current, throwAwayNext, lastDotPos)
                    else AtStartOfIdentifier(pos,[],false,None) // Throw away what we have and start over.

            and InQuotedIdentifier(left:int,pos:int, current,throwAwayNext,lastDotPos) =
                if pos > index then 
                    if throwAwayNext then 
                        PartialLongName.Empty(index) 
                    else 
                        { QualifyingIdents = current
                          PartialIdent = lineStr.Substring(left,pos-left)
                          EndColumn = index
                          LastDotPos = lastDotPos }
                else
                    let remainingLength = lineStr.Length - pos
                    if IsTick pos && remainingLength > 1 && IsTick(pos+1) then 
                        let ident = lineStr.Substring(left, pos-left)
                        SkipWhitespaceBeforeDotIdentifier(pos+2,ident,current,throwAwayNext,lastDotPos) 
                    else InQuotedIdentifier(left,pos+1,current,throwAwayNext,lastDotPos)                    

            and AtStartOfIdentifier(pos:int, current, throwAwayNext, lastDotPos: int option) =
                if pos > index then 
                    if throwAwayNext then 
                        PartialLongName.Empty(index)
                    else 
                        { QualifyingIdents = current
                          PartialIdent = ""
                          EndColumn = index
                          LastDotPos = lastDotPos }
                else
                    if IsWhitespace pos then AtStartOfIdentifier(pos+1,current,throwAwayNext, lastDotPos)
                    else
                        let remainingLength = lineStr.Length - pos
                        if IsTick pos && remainingLength > 1 && IsTick(pos+1) then InQuotedIdentifier(pos+2,pos+2,current,throwAwayNext,lastDotPos)
                        elif IsStartOfComment pos then EatComment(1, pos + 1, EatCommentCallContext.StartIdentifier(current, throwAwayNext), lastDotPos)
                        elif IsIdentifierStartCharacter pos then InUnquotedIdentifier(pos,pos+1,current,throwAwayNext,lastDotPos)
                        elif IsDot pos then 
                            if pos = 0 then
                                // dot on first char of line, currently treat it like empty identifier to the left
                                AtStartOfIdentifier(pos+1,"":: current,throwAwayNext, Some pos)
                            elif not (pos > 0 && (IsIdentifierPartCharacter(pos-1) || IsWhitespace(pos-1))) then
                                // it's not dots as part.of.a.long.ident, it's e.g. the range operator (..), or some other multi-char operator ending in dot
                                if lineStr.[pos-1] = ')' then
                                    // one very problematic case is someCall(args).Name
                                    // without special logic, we will decide that ). is an operator and parse Name as the plid
                                    // but in fact this is an expression tail, and we don't want a plid, rather we need to use expression typings at that location
                                    // so be sure not to treat the name here as a plid
                                    AtStartOfIdentifier(pos+1,[],true,None) // Throw away what we have, and the next apparent plid, and start over.
                                else
                                    AtStartOfIdentifier(pos+1,[],false,None) // Throw away what we have and start over.
                            else
                                AtStartOfIdentifier(pos+1,"":: current,throwAwayNext, Some pos)
                        else AtStartOfIdentifier(pos+1,[],throwAwayNext, None)
            let partialLongName = AtStartOfIdentifier(0, [], false, None) 
            
            match List.rev partialLongName.QualifyingIdents with
            | s :: _ when s.Length > 0 && Char.IsDigit(s.[0]) -> PartialLongName.Empty(index)  // "2.0" is not a longId (this might not be right for ``2.0`` but good enough for common case)
            | plid -> { partialLongName with QualifyingIdents = plid }
    
    let TokenNameEquals (tokenInfo: FSharpTokenInfo) (token2: string) = 
        String.Compare(tokenInfo .TokenName, token2, StringComparison.OrdinalIgnoreCase) = 0  
    
    // The prefix of the sequence of token names to look for in TestMemberOrOverrideDeclaration, in reverse order
    let private expected = [ [|"dot"|]; [|"ident"|]; [|"member"; "override"|] ]

    /// Tests whether the user is typing something like "member x." or "override (*comment*) x."
    let TestMemberOrOverrideDeclaration (tokens: FSharpTokenInfo[]) =
        let filteredReversed = 
            tokens 
            |> Array.filter (fun tok ->
                // cut out whitespaces\comments\access modifiers
                not (TokenNameEquals tok "comment") && 
                not (TokenNameEquals tok "whitespace") &&
                not (TokenNameEquals tok "private") &&
                not (TokenNameEquals tok "internal") &&
                not (TokenNameEquals tok "public")
                ) 
            |> Array.rev
        
        if filteredReversed.Length < expected.Length then false
        else 
            // check whether sequences match
            (filteredReversed, expected) ||> Seq.forall2 (fun tok expect ->
                expect |> Array.exists (TokenNameEquals tok) )
                        
