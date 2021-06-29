// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.Tokenization

open System
open System.Collections.Generic
open System.Threading
open FSharp.Compiler.IO
open Internal.Utilities
open Internal.Utilities.Library
open Internal.Utilities.Library.Extras
open FSharp.Compiler
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Features
open FSharp.Compiler.Lexhelp
open FSharp.Compiler.Parser
open FSharp.Compiler.ParseHelpers
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Position
open FSharp.Compiler.Text.Range

module FSharpTokenTag =

    let Identifier = tagOfToken (IDENT "a")
    let String = tagOfToken (STRING ("a", SynStringKind.Regular, LexCont.Default))

    let IDENT = tagOfToken (IDENT "a")
    let HASH_IDENT = tagOfToken (HASH_IDENT "a")
    let STRING = String
    let INTERP_STRING_BEGIN_END = tagOfToken (INTERP_STRING_BEGIN_END ("a", SynStringKind.Regular, LexCont.Default))
    let INTERP_STRING_BEGIN_PART = tagOfToken (INTERP_STRING_BEGIN_PART ("a", SynStringKind.Regular, LexCont.Default))
    let INTERP_STRING_PART = tagOfToken (INTERP_STRING_PART ("a", LexCont.Default))
    let INTERP_STRING_END = tagOfToken (INTERP_STRING_END ("a", LexCont.Default))
    let LPAREN = tagOfToken LPAREN
    let RPAREN = tagOfToken RPAREN
    let LBRACK = tagOfToken LBRACK
    let RBRACK = tagOfToken RBRACK
    let LBRACE = tagOfToken (LBRACE LexCont.Default)
    let RBRACE = tagOfToken (RBRACE LexCont.Default)
    let LBRACK_LESS = tagOfToken LBRACK_LESS
    let GREATER_RBRACK = tagOfToken GREATER_RBRACK
    let LESS = tagOfToken (LESS true)
    let GREATER = tagOfToken (GREATER true)
    let LBRACK_BAR = tagOfToken LBRACK_BAR
    let BAR_RBRACK = tagOfToken BAR_RBRACK
    let PLUS_MINUS_OP = tagOfToken (PLUS_MINUS_OP "a")
    let MINUS = tagOfToken MINUS
    let STAR = tagOfToken STAR
    let INFIX_STAR_DIV_MOD_OP = tagOfToken (INFIX_STAR_DIV_MOD_OP "a")
    let PERCENT_OP = tagOfToken (PERCENT_OP "a")
    let INFIX_AT_HAT_OP = tagOfToken (INFIX_AT_HAT_OP "a")
    let QMARK = tagOfToken QMARK
    let COLON = tagOfToken COLON
    let EQUALS = tagOfToken EQUALS
    let SEMICOLON = tagOfToken SEMICOLON
    let COMMA = tagOfToken COMMA
    let DOT = tagOfToken DOT
    let DOT_DOT = tagOfToken DOT_DOT
    let DOT_DOT_HAT = tagOfToken DOT_DOT_HAT
    let INT32_DOT_DOT = tagOfToken (INT32_DOT_DOT(0, true))
    let UNDERSCORE = tagOfToken UNDERSCORE
    let BAR = tagOfToken BAR
    let COLON_GREATER = tagOfToken COLON_GREATER
    let COLON_QMARK_GREATER = tagOfToken COLON_QMARK_GREATER
    let COLON_QMARK = tagOfToken COLON_QMARK
    let INFIX_BAR_OP = tagOfToken (INFIX_BAR_OP "a")
    let INFIX_COMPARE_OP = tagOfToken (INFIX_COMPARE_OP "a")
    let COLON_COLON = tagOfToken COLON_COLON
    let AMP_AMP = tagOfToken AMP_AMP
    let PREFIX_OP = tagOfToken (PREFIX_OP "a")
    let COLON_EQUALS = tagOfToken COLON_EQUALS
    let BAR_BAR = tagOfToken BAR_BAR
    let RARROW = tagOfToken RARROW
    let LARROW = tagOfToken LARROW
    let QUOTE = tagOfToken QUOTE
    let WHITESPACE = tagOfToken (WHITESPACE Unchecked.defaultof<_>)
    let COMMENT = tagOfToken (COMMENT Unchecked.defaultof<_>)
    let LINE_COMMENT = tagOfToken (LINE_COMMENT Unchecked.defaultof<_>)
    let BEGIN = tagOfToken BEGIN
    let DO = tagOfToken DO
    let FUNCTION = tagOfToken FUNCTION
    let THEN = tagOfToken THEN
    let ELSE = tagOfToken ELSE
    let STRUCT = tagOfToken STRUCT
    let CLASS = tagOfToken CLASS
    let TRY = tagOfToken TRY
    let NEW = tagOfToken NEW
    let WITH = tagOfToken WITH
    let OWITH = tagOfToken OWITH


/// This corresponds to a token categorization originally used in Visual Studio 2003.
///
/// NOTE: This corresponds to a token categorization originally used in Visual Studio 2003 and the original Babel source code.
/// It is not clear it is a primary logical classification that should be being used in the
/// more recent language service work.
type FSharpTokenColorKind =
    | Default = 0
    | Text = 0
    | Keyword = 1
    | Comment = 2
    | Identifier = 3
    | String = 4
    | UpperIdentifier = 5
    | InactiveCode = 7
    | PreprocessorKeyword = 8
    | Number = 9
    | Operator = 10
    | Punctuation = 11

/// Categorize an action the editor should take in response to a token, e.g. brace matching
///
/// NOTE: This corresponds to a token categorization originally used in Visual Studio 2003 and the original Babel source code.
/// It is not clear it is a primary logical classification that should be being used in the
/// more recent language service work.
type FSharpTokenTriggerClass =
    | None         = 0x00000000
    | MemberSelect = 0x00000001
    | MatchBraces  = 0x00000002
    | ChoiceSelect = 0x00000004
    | MethodTip    = 0x000000F0
    | ParamStart   = 0x00000010
    | ParamNext    = 0x00000020
    | ParamEnd     = 0x00000040


/// This corresponds to a token categorization originally used in Visual Studio 2003.
///
/// NOTE: This corresponds to a token categorization originally used in Visual Studio 2003 and the original Babel source code.
/// It is not clear it is a primary logical classification that should be being used in the
/// more recent language service work.
type FSharpTokenCharKind =
    | Default     = 0x00000000
    | Text        = 0x00000000
    | Keyword     = 0x00000001
    | Identifier  = 0x00000002
    | String      = 0x00000003
    | Literal     = 0x00000004
    | Operator    = 0x00000005
    | Delimiter   = 0x00000006
    | WhiteSpace  = 0x00000008
    | LineComment = 0x00000009
    | Comment     = 0x0000000A


/// Information about a particular token from the tokenizer
type FSharpTokenInfo = {
    LeftColumn: int
    RightColumn: int
    ColorClass: FSharpTokenColorKind
    CharClass: FSharpTokenCharKind
    FSharpTokenTriggerClass: FSharpTokenTriggerClass
    Tag: int
    TokenName: string
    FullMatchedLength: int }

//----------------------------------------------------------------------------
// Babel flags
//--------------------------------------------------------------------------

module internal TokenClassifications =

    //----------------------------------------------------------------------------
    //From tokens to flags
    //--------------------------------------------------------------------------

    let tokenInfo token =
        match token with
        | HASH_IDENT s
        | IDENT s ->
            if s.Length <= 0 then
                System.Diagnostics.Debug.Assert(false, "BUG: Received zero length IDENT token.")
                // This is related to 4783. Recover by treating as lower case identifier.
                (FSharpTokenColorKind.Identifier, FSharpTokenCharKind.Identifier, FSharpTokenTriggerClass.None)
            else
                if System.Char.ToUpperInvariant s.[0] = s.[0] then
                    (FSharpTokenColorKind.UpperIdentifier, FSharpTokenCharKind.Identifier, FSharpTokenTriggerClass.None)
                else
                    (FSharpTokenColorKind.Identifier, FSharpTokenCharKind.Identifier, FSharpTokenTriggerClass.None)

        // 'in' when used in a 'join' in a query expression
        | JOIN_IN ->
            (FSharpTokenColorKind.Identifier, FSharpTokenCharKind.Identifier, FSharpTokenTriggerClass.None)
        | DECIMAL _
        | BIGNUM _ | INT8 _  | UINT8 _ | INT16 _  | UINT16 _ | INT32 _ | UINT32 _ | INT64 _ | UINT64 _
        | UNATIVEINT _ | NATIVEINT _ | IEEE32 _ |  IEEE64 _ ->
            (FSharpTokenColorKind.Number, FSharpTokenCharKind.Literal, FSharpTokenTriggerClass.None)

        | INT32_DOT_DOT _ ->
          // This will color the whole "1.." expression in a 'number' color
          // (this isn't entirely correct, but it'll work for now - see bug 3727)
            (FSharpTokenColorKind.Number, FSharpTokenCharKind.Operator, FSharpTokenTriggerClass.None)

        | INFIX_STAR_DIV_MOD_OP ("mod"  | "land" |  "lor" | "lxor")
        | INFIX_STAR_STAR_OP ("lsl" | "lsr" | "asr") ->
            (FSharpTokenColorKind.Keyword, FSharpTokenCharKind.Keyword, FSharpTokenTriggerClass.None)

        | LPAREN_STAR_RPAREN
        | DOLLAR | COLON_GREATER  | COLON_COLON
        | PERCENT_OP _ | PLUS_MINUS_OP _ | PREFIX_OP _ | COLON_QMARK_GREATER
        | AMP   | AMP_AMP  | BAR_BAR  | QMARK | QMARK_QMARK | COLON_QMARK
        | HIGH_PRECEDENCE_TYAPP
        | COLON_EQUALS   | EQUALS | RQUOTE_DOT _
        | MINUS | ADJACENT_PREFIX_OP _ ->
            (FSharpTokenColorKind.Operator, FSharpTokenCharKind.Operator, FSharpTokenTriggerClass.None)

        | INFIX_COMPARE_OP _ // This is a whole family: .< .> .= .!= .$
        | FUNKY_OPERATOR_NAME _ // This is another whole family, including: .[] and .()
        //| INFIX_AT_HAT_OP _
        | INFIX_STAR_STAR_OP _
        | INFIX_AMP_OP _
        | INFIX_BAR_OP _
        | INFIX_STAR_DIV_MOD_OP _
        | INFIX_AMP_OP _ ->
                (FSharpTokenColorKind.Operator, FSharpTokenCharKind.Operator, FSharpTokenTriggerClass.None)

        | DOT_DOT | DOT_DOT_HAT ->
            (FSharpTokenColorKind.Operator, FSharpTokenCharKind.Operator, FSharpTokenTriggerClass.MemberSelect)

        | COMMA ->
            (FSharpTokenColorKind.Punctuation, FSharpTokenCharKind.Delimiter, FSharpTokenTriggerClass.ParamNext)

        | DOT ->
            (FSharpTokenColorKind.Punctuation, FSharpTokenCharKind.Delimiter, FSharpTokenTriggerClass.MemberSelect)

        | BAR ->
            (FSharpTokenColorKind.Punctuation, FSharpTokenCharKind.Delimiter, FSharpTokenTriggerClass.None (* FSharpTokenTriggerClass.ChoiceSelect *))

        | HASH | STAR | SEMICOLON  | SEMICOLON_SEMICOLON | COLON ->
            (FSharpTokenColorKind.Punctuation, FSharpTokenCharKind.Delimiter, FSharpTokenTriggerClass.None)

        | QUOTE | UNDERSCORE
        | INFIX_AT_HAT_OP _ ->
            (FSharpTokenColorKind.Identifier, FSharpTokenCharKind.Identifier, FSharpTokenTriggerClass.None)

        | LESS  _ ->
            (FSharpTokenColorKind.Punctuation, FSharpTokenCharKind.Operator, FSharpTokenTriggerClass.ParamStart)  // for type provider static arguments

        | GREATER _ ->
            (FSharpTokenColorKind.Punctuation, FSharpTokenCharKind.Operator, FSharpTokenTriggerClass.ParamEnd)    // for type provider static arguments

        | LPAREN ->
            // We need 'ParamStart' to trigger the 'GetDeclarations' method to show param info automatically
            // this is needed even if we don't use MPF for determining information about params
            (FSharpTokenColorKind.Punctuation, FSharpTokenCharKind.Delimiter, FSharpTokenTriggerClass.ParamStart ||| FSharpTokenTriggerClass.MatchBraces)

        | RPAREN | RPAREN_COMING_SOON | RPAREN_IS_HERE ->
            (FSharpTokenColorKind.Punctuation, FSharpTokenCharKind.Delimiter, FSharpTokenTriggerClass.ParamEnd ||| FSharpTokenTriggerClass.MatchBraces)

        | LBRACK_LESS ->
            (FSharpTokenColorKind.Punctuation, FSharpTokenCharKind.Delimiter, FSharpTokenTriggerClass.None )

        | LQUOTE _  | LBRACK  | LBRACE _ | LBRACK_BAR | LBRACE_BAR ->
            (FSharpTokenColorKind.Punctuation, FSharpTokenCharKind.Delimiter, FSharpTokenTriggerClass.MatchBraces )

        | GREATER_RBRACK  | GREATER_BAR_RBRACK ->
            (FSharpTokenColorKind.Punctuation, FSharpTokenCharKind.Delimiter, FSharpTokenTriggerClass.None )

        | RQUOTE _  | RBRACK  | RBRACE _ | RBRACE_COMING_SOON | RBRACE_IS_HERE | BAR_RBRACK | BAR_RBRACE ->
            (FSharpTokenColorKind.Punctuation, FSharpTokenCharKind.Delimiter, FSharpTokenTriggerClass.MatchBraces )

        | PUBLIC | PRIVATE | INTERNAL | BASE | GLOBAL
        | CONSTRAINT | INSTANCE | DELEGATE | INHERIT|CONSTRUCTOR|DEFAULT|OVERRIDE|ABSTRACT|CLASS
        | MEMBER | STATIC | NAMESPACE
        | OASSERT | OLAZY | ODECLEND | OBLOCKSEP | OEND | OBLOCKBEGIN | ORIGHT_BLOCK_END
        | OBLOCKEND | OBLOCKEND_COMING_SOON | OBLOCKEND_IS_HERE | OTHEN | OELSE | OLET(_)
        | OBINDER _ | OAND_BANG _ | BINDER _ | ODO | OWITH | OFUNCTION | OFUN | ORESET | ODUMMY _ | DO_BANG
        | ODO_BANG | YIELD _ | YIELD_BANG  _ | OINTERFACE_MEMBER
        | ELIF | RARROW | LARROW | SIG | STRUCT
        | UPCAST   | DOWNCAST   | NULL   | RESERVED    | MODULE    | AND    | AS   | ASSERT   | ASR
        | DOWNTO   | EXCEPTION   | FALSE   | FOR   | FUN   | FUNCTION
        | FINALLY   | LAZY   | MATCH  | MATCH_BANG  | MUTABLE   | NEW   | OF    | OPEN   | OR | VOID | EXTERN
        | INTERFACE | REC   | TO   | TRUE   | TRY   | TYPE   |  VAL   | INLINE   | WHEN  | WHILE   | WITH
        | IF | THEN  | ELSE | DO | DONE | LET _ | AND_BANG _ | IN | CONST
        | HIGH_PRECEDENCE_PAREN_APP | FIXED
        | HIGH_PRECEDENCE_BRACK_APP
        | TYPE_COMING_SOON | TYPE_IS_HERE | MODULE_COMING_SOON | MODULE_IS_HERE ->
            (FSharpTokenColorKind.Keyword, FSharpTokenCharKind.Keyword, FSharpTokenTriggerClass.None)

        | BEGIN ->
            (FSharpTokenColorKind.Keyword, FSharpTokenCharKind.Keyword, FSharpTokenTriggerClass.None)

        | END ->
            (FSharpTokenColorKind.Keyword, FSharpTokenCharKind.Keyword, FSharpTokenTriggerClass.None)

        | HASH_LIGHT _
        | HASH_LINE _
        | HASH_IF _
        | HASH_ELSE _
        | HASH_ENDIF _ ->
            (FSharpTokenColorKind.PreprocessorKeyword, FSharpTokenCharKind.WhiteSpace, FSharpTokenTriggerClass.None)

        | INACTIVECODE _ ->
            (FSharpTokenColorKind.InactiveCode, FSharpTokenCharKind.WhiteSpace, FSharpTokenTriggerClass.None)


        | LEX_FAILURE _
        | WHITESPACE _ ->
            (FSharpTokenColorKind.Default, FSharpTokenCharKind.WhiteSpace, FSharpTokenTriggerClass.None)

        | COMMENT _ ->
            (FSharpTokenColorKind.Comment, FSharpTokenCharKind.Comment, FSharpTokenTriggerClass.None)

        | LINE_COMMENT _ ->
            (FSharpTokenColorKind.Comment, FSharpTokenCharKind.LineComment, FSharpTokenTriggerClass.None)

        | KEYWORD_STRING _ ->
           (FSharpTokenColorKind.Keyword, FSharpTokenCharKind.Keyword, FSharpTokenTriggerClass.None)

        | STRING_TEXT _
        | INTERP_STRING_BEGIN_END _
        | INTERP_STRING_BEGIN_PART _
        | INTERP_STRING_PART _
        | INTERP_STRING_END _
        | BYTEARRAY _ | STRING  _
        | CHAR _ ->
            (FSharpTokenColorKind.String, FSharpTokenCharKind.String, FSharpTokenTriggerClass.None)

        | EOF _ -> failwith "tokenInfo"

module internal TestExpose =
  let TokenInfo tok = TokenClassifications.tokenInfo tok

/// Lexer states are encoded to/from integers. Typically one lexer state is
/// keep at the end of each line in an IDE service. IDE services are sometimes highly limited in the
/// memory they can use and this per-line state can be a significant cost if it associates with
/// many allocated objects.
///
/// The encoding is lossy so some incremental lexing scenarios such as deeply nested #if
/// or accurate error messages from lexing for mismtached #if are not supported.
[<Struct; CustomEquality; NoComparison>]
type FSharpTokenizerLexState =
    { PosBits: int64
      OtherBits: int64 }
    static member Initial = { PosBits = 0L; OtherBits = 0L }
    member this.Equals (other: FSharpTokenizerLexState) = (this.PosBits = other.PosBits) && (this.OtherBits = other.OtherBits)
    override this.Equals (obj: obj) = match obj with :? FSharpTokenizerLexState as other -> this.Equals other | _ -> false
    override this.GetHashCode () = hash this.PosBits + hash this.OtherBits

type FSharpTokenizerColorState =
    | Token = 1
    | IfDefSkip = 3
    | String = 4
    | Comment = 5
    | StringInComment = 6
    | VerbatimStringInComment = 7
    | CamlOnly = 8
    | VerbatimString = 9
    | SingleLineComment = 10
    | EndLineThenSkip = 11
    | EndLineThenToken = 12
    | TripleQuoteString = 13
    | TripleQuoteStringInComment = 14
    | InitialState = 0


module internal LexerStateEncoding =

    let computeNextLexState token (prevLexcont: LexerContinuation) =
      match token with
      | HASH_LINE cont
      | HASH_LIGHT cont
      | HASH_IF(_, _, cont)
      | HASH_ELSE(_, _, cont)
      | HASH_ENDIF(_, _, cont)
      | INACTIVECODE cont
      | WHITESPACE cont
      | COMMENT cont
      | LINE_COMMENT cont
      | STRING_TEXT cont
      | EOF cont
      | INTERP_STRING_BEGIN_PART (_, _, cont)
      | INTERP_STRING_PART (_, cont)
      | INTERP_STRING_BEGIN_END (_, _, cont)
      | INTERP_STRING_END (_, cont)
      | LBRACE cont
      | RBRACE cont
      | BYTEARRAY (_, _, cont)
      | STRING (_, _, cont) -> cont
      | _ -> prevLexcont

    // Note that this will discard all lexcont state, including the ifdefStack.
    let revertToDefaultLexCont = LexCont.Default

    let lexstateNumBits = 4
    let ncommentsNumBits = 4
    let hardwhiteNumBits = 1
    let ifdefstackCountNumBits = 8
    let ifdefstackNumBits = 24           // 0 means if, 1 means else
    let stringKindBits = 3
    let nestingBits = 12
    let _ = assert (lexstateNumBits
                    + ncommentsNumBits
                    + hardwhiteNumBits
                    + ifdefstackCountNumBits
                    + ifdefstackNumBits
                    + stringKindBits
                    + nestingBits <= 64)

    let lexstateStart         = 0
    let ncommentsStart        = lexstateNumBits
    let hardwhitePosStart     = lexstateNumBits+ncommentsNumBits
    let ifdefstackCountStart  = lexstateNumBits+ncommentsNumBits+hardwhiteNumBits
    let ifdefstackStart       = lexstateNumBits+ncommentsNumBits+hardwhiteNumBits+ifdefstackCountNumBits
    let stringKindStart       = lexstateNumBits+ncommentsNumBits+hardwhiteNumBits+ifdefstackCountNumBits+ifdefstackNumBits
    let nestingStart          = lexstateNumBits+ncommentsNumBits+hardwhiteNumBits+ifdefstackCountNumBits+ifdefstackNumBits+stringKindBits

    let lexstateMask          = Bits.mask64 lexstateStart lexstateNumBits
    let ncommentsMask         = Bits.mask64 ncommentsStart ncommentsNumBits
    let hardwhitePosMask      = Bits.mask64 hardwhitePosStart hardwhiteNumBits
    let ifdefstackCountMask   = Bits.mask64 ifdefstackCountStart ifdefstackCountNumBits
    let ifdefstackMask        = Bits.mask64 ifdefstackStart ifdefstackNumBits
    let stringKindMask        = Bits.mask64 stringKindStart stringKindBits
    let nestingMask           = Bits.mask64 nestingStart nestingBits

    let bitOfBool b = if b then 1 else 0
    let boolOfBit n = (n = 1L)

    let colorStateOfLexState (state: FSharpTokenizerLexState) =
        enum<FSharpTokenizerColorState> (int32 ((state.OtherBits &&& lexstateMask) >>> lexstateStart))

    let lexStateOfColorState (state: FSharpTokenizerColorState) =
        (int64 state <<< lexstateStart) &&& lexstateMask

    let encodeStringStyle kind =
        match kind with
        | LexerStringStyle.SingleQuote -> 0
        | LexerStringStyle.Verbatim -> 1
        | LexerStringStyle.TripleQuote -> 2

    let decodeStringStyle kind =
        match kind with
        | 0 -> LexerStringStyle.SingleQuote
        | 1 -> LexerStringStyle.Verbatim
        | 2 -> LexerStringStyle.TripleQuote
        | _ -> assert false; LexerStringStyle.SingleQuote

    let encodeLexCont (colorState: FSharpTokenizerColorState, numComments, b: pos, ifdefStack, light, stringKind: LexerStringKind, stringNest) =
        let mutable ifdefStackCount = 0
        let mutable ifdefStackBits = 0
        for ifOrElse in ifdefStack do
            match ifOrElse with
            | (IfDefIf, _) -> ()
            | (IfDefElse, _) ->
                ifdefStackBits <- (ifdefStackBits ||| (1 <<< ifdefStackCount))
            ifdefStackCount <- ifdefStackCount + 1

        let stringKindValue =
            (if stringKind.IsByteString then 0b100 else 0) |||
            (if stringKind.IsInterpolated then 0b010 else 0) |||
            (if stringKind.IsInterpolatedFirst then 0b001 else 0)

        let nestingValue =
            let tag1, i1, kind1, rest =
                match stringNest with
                | [] -> false, 0, 0, []
                | (i1, kind1, _)::rest -> true, i1, encodeStringStyle kind1, rest
            let tag2, i2, kind2 =
                match rest with
                | [] -> false, 0, 0
                | (i2, kind2, _)::_ -> true, i2, encodeStringStyle kind2
            (if tag1 then      0b100000000000 else 0) |||
            (if tag2 then      0b010000000000 else 0) |||
            ((i1 <<< 7)    &&& 0b001110000000) |||
            ((i2 <<< 4)    &&& 0b000001110000) |||
            ((kind1 <<< 2) &&& 0b000000001100) |||
            ((kind2 <<< 0) &&& 0b000000000011)

        let bits =
            lexStateOfColorState colorState
            ||| ((numComments <<< ncommentsStart) &&& ncommentsMask)
            ||| ((int64 (bitOfBool light) <<< hardwhitePosStart) &&& hardwhitePosMask)
            ||| ((int64 ifdefStackCount <<< ifdefstackCountStart) &&& ifdefstackCountMask)
            ||| ((int64 ifdefStackBits <<< ifdefstackStart) &&& ifdefstackMask)
            ||| ((int64 stringKindValue <<< stringKindStart) &&& stringKindMask)
            ||| ((int64 nestingValue <<< nestingStart) &&& nestingMask)

        { PosBits = b.Encoding
          OtherBits = bits }


    let decodeLexCont (state: FSharpTokenizerLexState) =
        let mutable ifDefs = []
        let bits = state.OtherBits

        let colorState = colorStateOfLexState state
        let ncomments = int32 ((bits &&& ncommentsMask) >>> ncommentsStart)
        let pos = pos.Decode state.PosBits

        let ifdefStackCount = int32 ((bits &&& ifdefstackCountMask) >>> ifdefstackCountStart)
        if ifdefStackCount>0 then
            let ifdefStack = int32 ((bits &&& ifdefstackMask) >>> ifdefstackStart)
            for i in 1..ifdefStackCount do
                let bit = ifdefStackCount-i
                let mask = 1 <<< bit
                let ifDef = (if ifdefStack &&& mask = 0 then IfDefIf else IfDefElse)
                ifDefs <- (ifDef, range0) :: ifDefs

        let stringKindValue = int32 ((bits &&& stringKindMask) >>> stringKindStart)
        let stringKind : LexerStringKind =
            { IsByteString = ((stringKindValue &&& 0b100) = 0b100)
              IsInterpolated = ((stringKindValue &&& 0b010) = 0b010)
              IsInterpolatedFirst = ((stringKindValue &&& 0b001) = 0b001) }

        let hardwhite = boolOfBit ((bits &&& hardwhitePosMask) >>> hardwhitePosStart)

        let nestingValue = int32 ((bits &&& nestingMask) >>> nestingStart)
        let stringNest : LexerInterpolatedStringNesting =
            let tag1  = ((nestingValue &&& 0b100000000000) =  0b100000000000)
            let tag2  = ((nestingValue &&& 0b010000000000) =  0b010000000000)
            let i1    = ((nestingValue &&& 0b001110000000) >>> 7)
            let i2    = ((nestingValue &&& 0b000001110000) >>> 4)
            let kind1 = ((nestingValue &&& 0b000000001100) >>> 2)
            let kind2 = ((nestingValue &&& 0b000000000011) >>> 0)
            [ if tag1 then
                 i1, decodeStringStyle kind1, range0
              if tag2 then
                 i2, decodeStringStyle kind2, range0
            ]

        (colorState, ncomments, pos, ifDefs, hardwhite, stringKind, stringNest)

    let encodeLexInt lightStatus (lexcont: LexerContinuation) =
        match lexcont with
        | LexCont.Token (ifdefs, stringNest) ->
            encodeLexCont (FSharpTokenizerColorState.Token, 0L, pos0, ifdefs, lightStatus, LexerStringKind.String, stringNest)
        | LexCont.IfDefSkip (ifdefs, stringNest, n, m) ->
            encodeLexCont (FSharpTokenizerColorState.IfDefSkip, int64 n, m.Start, ifdefs, lightStatus, LexerStringKind.String, stringNest)
        | LexCont.EndLine(ifdefs, stringNest, econt) ->
            match econt with
            | LexerEndlineContinuation.Skip(n, m) ->
               encodeLexCont (FSharpTokenizerColorState.EndLineThenSkip, int64 n, m.Start, ifdefs, lightStatus, LexerStringKind.String, stringNest)
            | LexerEndlineContinuation.Token ->
               encodeLexCont (FSharpTokenizerColorState.EndLineThenToken, 0L, pos0, ifdefs, lightStatus, LexerStringKind.String, stringNest)
        | LexCont.String (ifdefs, stringNest, style, kind, m) ->
            let state =
                match style with
                | LexerStringStyle.SingleQuote -> FSharpTokenizerColorState.String
                | LexerStringStyle.Verbatim -> FSharpTokenizerColorState.VerbatimString
                | LexerStringStyle.TripleQuote -> FSharpTokenizerColorState.TripleQuoteString
            encodeLexCont (state, 0L, m.Start, ifdefs, lightStatus, kind, stringNest)
        | LexCont.Comment (ifdefs, stringNest, n, m) ->
            encodeLexCont (FSharpTokenizerColorState.Comment, int64 n, m.Start, ifdefs, lightStatus, LexerStringKind.String, stringNest)
        | LexCont.SingleLineComment (ifdefs, stringNest, n, m) ->
            encodeLexCont (FSharpTokenizerColorState.SingleLineComment, int64 n, m.Start, ifdefs, lightStatus, LexerStringKind.String, stringNest)
        | LexCont.StringInComment (ifdefs, stringNest, style, n, m) ->
            let state =
                match style with
                | LexerStringStyle.SingleQuote -> FSharpTokenizerColorState.StringInComment
                | LexerStringStyle.Verbatim -> FSharpTokenizerColorState.VerbatimStringInComment
                | LexerStringStyle.TripleQuote -> FSharpTokenizerColorState.TripleQuoteStringInComment
            encodeLexCont (state, int64 n, m.Start, ifdefs, lightStatus, LexerStringKind.String, stringNest)
        | LexCont.MLOnly (ifdefs, stringNest, m) ->
            encodeLexCont (FSharpTokenizerColorState.CamlOnly, 0L, m.Start, ifdefs, lightStatus, LexerStringKind.String, stringNest)

    let decodeLexInt (state: FSharpTokenizerLexState) =
        let tag, n1, p1, ifdefs, lightSyntaxStatusInitial, stringKind, stringNest = decodeLexCont state
        let lexcont =
            match tag with
            | FSharpTokenizerColorState.Token ->
                LexCont.Token (ifdefs, stringNest)
            | FSharpTokenizerColorState.IfDefSkip ->
                LexCont.IfDefSkip (ifdefs, stringNest, n1, mkRange "file" p1 p1)
            | FSharpTokenizerColorState.String ->
                LexCont.String (ifdefs, stringNest, LexerStringStyle.SingleQuote, stringKind, mkRange "file" p1 p1)
            | FSharpTokenizerColorState.Comment ->
                LexCont.Comment (ifdefs, stringNest, n1, mkRange "file" p1 p1)
            | FSharpTokenizerColorState.SingleLineComment ->
                LexCont.SingleLineComment (ifdefs, stringNest, n1, mkRange "file" p1 p1)
            | FSharpTokenizerColorState.StringInComment ->
                LexCont.StringInComment (ifdefs, stringNest, LexerStringStyle.SingleQuote, n1, mkRange "file" p1 p1)
            | FSharpTokenizerColorState.VerbatimStringInComment ->
                LexCont.StringInComment (ifdefs, stringNest, LexerStringStyle.Verbatim, n1, mkRange "file" p1 p1)
            | FSharpTokenizerColorState.TripleQuoteStringInComment ->
                LexCont.StringInComment (ifdefs, stringNest, LexerStringStyle.TripleQuote, n1, mkRange "file" p1 p1)
            | FSharpTokenizerColorState.CamlOnly ->
                LexCont.MLOnly (ifdefs, stringNest, mkRange "file" p1 p1)
            | FSharpTokenizerColorState.VerbatimString ->
                LexCont.String (ifdefs, stringNest, LexerStringStyle.Verbatim, stringKind, mkRange "file" p1 p1)
            | FSharpTokenizerColorState.TripleQuoteString ->
                LexCont.String (ifdefs, stringNest, LexerStringStyle.TripleQuote, stringKind, mkRange "file" p1 p1)
            | FSharpTokenizerColorState.EndLineThenSkip ->
                LexCont.EndLine(ifdefs, stringNest, LexerEndlineContinuation.Skip(n1, mkRange "file" p1 p1))
            | FSharpTokenizerColorState.EndLineThenToken ->
                LexCont.EndLine(ifdefs, stringNest, LexerEndlineContinuation.Token)
            | _ -> LexCont.Token ([], stringNest)
        lightSyntaxStatusInitial, lexcont

//----------------------------------------------------------------------------
// Colorization
//----------------------------------------------------------------------------

// Information beyond just tokens that can be derived by looking at just a single line.
// For example meta commands like #load.
type SingleLineTokenState =
    | BeforeHash = 0
    | NoFurtherMatchPossible = 1


/// Split a line into tokens and attach information about the tokens. This information is used by Visual Studio.
[<Sealed>]
type FSharpLineTokenizer(lexbuf: UnicodeLexing.Lexbuf,
                         maxLength: int option,
                         filename: string option,
                         lexargs: LexArgs) =

    let skip = false   // don't skip whitespace in the lexer

    let mutable singleLineTokenState = SingleLineTokenState.BeforeHash
    let fsx =
        match filename with
        | None -> false
        | Some value -> ParseAndCheckInputs.IsScript value

    // ----------------------------------------------------------------------------------
    // This implements post-processing of #directive tokens - not very elegant, but it works...
    // We get the whole "   #if IDENT // .. .. " thing as a single token from the lexer,
    // so we need to split it into tokens that are used by VS for colorization

    // Stack for tokens that are split during postprocessing
    let mutable tokenStack = new Stack<_>()
    let delayToken tok = tokenStack.Push tok

    // Process: anywhite* #<directive>
    let processDirective (str: string) directiveLength delay cont =
        let hashIdx = str.IndexOf("#", StringComparison.Ordinal)
        if (hashIdx <> 0) then delay(WHITESPACE cont, 0, hashIdx - 1)
        delay(HASH_IF(range0, "", cont), hashIdx, hashIdx + directiveLength)
        hashIdx + directiveLength + 1

    // Process: anywhite* ("//" [^'\n''\r']*)?
    let processWhiteAndComment (str: string) offset delay cont =
        let rest = str.Substring(offset, str.Length - offset)
        let comment = rest.IndexOf('/')
        let spaceLength = if comment = -1 then rest.Length else comment
        if (spaceLength > 0) then delay(WHITESPACE cont, offset, offset + spaceLength - 1)
        if (comment <> -1) then delay(COMMENT cont, offset + comment, offset + rest.Length - 1)

    // Split a directive line from lexer into tokens usable in VS
    let processDirectiveLine ofs f =
        let delayed = new ResizeArray<_>()
        f (fun (tok, s, e) -> delayed.Add (tok, s + ofs, e + ofs) )
        // delay all the tokens and return the remaining one
        for i = delayed.Count - 1 downto 1 do delayToken delayed.[i]
        delayed.[0]

    // Split the following line:
    //  anywhite* ("#else"|"#endif") anywhite* ("//" [^'\n''\r']*)?
    let processHashEndElse ofs (str: string) length cont =
        processDirectiveLine ofs (fun delay ->
            // Process: anywhite* "#else"   /   anywhite* "#endif"
            let offset = processDirective str length delay cont
            // Process: anywhite* ("//" [^'\n''\r']*)?
            processWhiteAndComment str offset delay cont )

    // Split the following line:
    //  anywhite* "#if" anywhite+ ident anywhite* ("//" [^'\n''\r']*)?
    let processHashIfLine ofs (str: string) cont =
        let With n m = if (n < 0) then m else n
        processDirectiveLine ofs (fun delay ->
            // Process: anywhite* "#if"
            let offset = processDirective str 2 delay cont
            // Process: anywhite+ ident
            let rest, spaces =
                let w = str.Substring offset
                let r = w.TrimStart [| ' '; '\t' |]
                r, w.Length - r.Length
            let beforeIdent = offset + spaces
            let identLength = With (rest.IndexOfAny([| '/'; '\t'; ' ' |])) rest.Length
            delay(WHITESPACE cont, offset, beforeIdent - 1)
            delay(IDENT(rest.Substring(0, identLength)), beforeIdent, beforeIdent + identLength - 1)
            // Process: anywhite* ("//" [^'\n''\r']*)?
            let offset = beforeIdent + identLength
            processWhiteAndComment str offset delay cont )

    // Set up the initial file position
    do match filename with
        | None -> lexbuf.EndPos <- Internal.Utilities.Text.Lexing.Position.Empty
        | Some value -> resetLexbufPos value lexbuf

    // Call the given continuation, reusing the same 'lexargs' each time but adjust
    // its mutable entries to set up the right state
    let callLexCont lexcont lightStatus skip =

        // Set up the arguments to lexing
        lexargs.lightStatus <- lightStatus

        match lexcont with
        | LexCont.EndLine (ifdefs, stringNest, cont) ->
            lexargs.ifdefStack <- ifdefs
            lexargs.stringNest <- stringNest
            Lexer.endline cont lexargs skip lexbuf

        | LexCont.Token (ifdefs, stringNest) ->
            lexargs.ifdefStack <- ifdefs
            lexargs.stringNest <- stringNest
            Lexer.token lexargs skip lexbuf

        | LexCont.IfDefSkip (ifdefs, stringNest, n, m) ->
            lexargs.ifdefStack <- ifdefs
            lexargs.stringNest <- stringNest
            Lexer.ifdefSkip n m lexargs skip lexbuf

        | LexCont.String (ifdefs, stringNest, style, kind, m) ->
            lexargs.ifdefStack <- ifdefs
            lexargs.stringNest <- stringNest
            use buf = ByteBuffer.Create (Lexer.StringCapacity)
            let args = (buf, LexerStringFinisher.Default, m, kind, lexargs)
            match style with
            | LexerStringStyle.SingleQuote -> Lexer.singleQuoteString args skip lexbuf
            | LexerStringStyle.Verbatim -> Lexer.verbatimString args skip lexbuf
            | LexerStringStyle.TripleQuote -> Lexer.tripleQuoteString args skip lexbuf

        | LexCont.Comment (ifdefs, stringNest, n, m) ->
            lexargs.ifdefStack <- ifdefs
            lexargs.stringNest <- stringNest
            Lexer.comment (n, m, lexargs) skip lexbuf

        | LexCont.SingleLineComment (ifdefs, stringNest, n, m) ->
            lexargs.ifdefStack <- ifdefs
            lexargs.stringNest <- stringNest
            // The first argument is 'None' because we don't need XML comments when called from VS tokenizer
            Lexer.singleLineComment (None, n, m, lexargs) skip lexbuf

        | LexCont.StringInComment (ifdefs, stringNest, style, n, m) ->
            lexargs.ifdefStack <- ifdefs
            lexargs.stringNest <- stringNest
            match style with
            | LexerStringStyle.SingleQuote -> Lexer.stringInComment n m lexargs skip lexbuf
            | LexerStringStyle.Verbatim -> Lexer.verbatimStringInComment n m lexargs skip lexbuf
            | LexerStringStyle.TripleQuote -> Lexer.tripleQuoteStringInComment n m lexargs skip lexbuf

        | LexCont.MLOnly (ifdefs, stringNest, m) ->
            lexargs.ifdefStack <- ifdefs
            lexargs.stringNest <- stringNest
            Lexer.mlOnly m lexargs skip lexbuf

    let columnsOfCurrentToken() =
        let leftp = lexbuf.StartPos
        let rightp = lexbuf.EndPos
        let leftc = leftp.Column
        let rightc =
            match maxLength with
            | Some mx when rightp.Line > leftp.Line -> mx
            | _ -> rightp.Column
        let rightc = rightc - 1
        struct (leftc, rightc)

    let getTokenWithPosition lexcont lightStatus =
        // Column of token
        // Get the token & position - either from a stack or from the lexer
        try
            if (tokenStack.Count > 0) then
                true, tokenStack.Pop()
            else
                // Choose which lexer entry point to call and call it
                let token = callLexCont lexcont lightStatus skip
                let struct (leftc, rightc) = columnsOfCurrentToken()

                // Splits tokens like ">." into multiple tokens - this duplicates behavior from the 'lexfilter'
                // which cannot be (easily) used from the language service. The rules here are not always valid,
                // because sometimes token shouldn't be split. However it is just for colorization &
                // for VS (which needs to recognize when user types ".").
                match token with
                | HASH_IF (m, lineStr, cont) when lineStr <> "" ->
                    false, processHashIfLine m.StartColumn lineStr cont
                | HASH_ELSE (m, lineStr, cont) when lineStr <> "" ->
                    false, processHashEndElse m.StartColumn lineStr 4 cont
                | HASH_ENDIF (m, lineStr, cont) when lineStr <> "" ->
                    false, processHashEndElse m.StartColumn lineStr 5 cont
                | HASH_IDENT(ident) ->
                    delayToken(IDENT (ident), leftc + 1, rightc)
                    false, (HASH, leftc, leftc)
                | RQUOTE_DOT (s, raw) ->
                    delayToken(DOT, rightc, rightc)
                    false, (RQUOTE (s, raw), leftc, rightc - 1)
                | INFIX_COMPARE_OP (LexFilter.TyparsCloseOp(greaters, afterOp) as opstr) ->
                    match afterOp with
                    | None -> ()
                    | Some tok -> delayToken(tok, leftc + greaters.Length, rightc)
                    for i = greaters.Length - 1 downto 1 do
                        delayToken(greaters.[i] false, leftc + i, rightc - opstr.Length + i + 1)
                    false, (greaters.[0] false, leftc, rightc - opstr.Length + 1)
                // break up any operators that start with '.' so that we can get auto-popup-completion for e.g. "x.+1"  when typing the dot
                | INFIX_STAR_STAR_OP opstr when opstr.StartsWithOrdinal(".") ->
                    delayToken(INFIX_STAR_STAR_OP(opstr.Substring 1), leftc+1, rightc)
                    false, (DOT, leftc, leftc)
                | PLUS_MINUS_OP opstr when opstr.StartsWithOrdinal(".") ->
                    delayToken(PLUS_MINUS_OP(opstr.Substring 1), leftc+1, rightc)
                    false, (DOT, leftc, leftc)
                | INFIX_COMPARE_OP opstr when opstr.StartsWithOrdinal(".") ->
                    delayToken(INFIX_COMPARE_OP(opstr.Substring 1), leftc+1, rightc)
                    false, (DOT, leftc, leftc)
                | INFIX_AT_HAT_OP opstr when opstr.StartsWithOrdinal(".") ->
                    delayToken(INFIX_AT_HAT_OP(opstr.Substring 1), leftc+1, rightc)
                    false, (DOT, leftc, leftc)
                | INFIX_BAR_OP opstr when opstr.StartsWithOrdinal(".") ->
                    delayToken(INFIX_BAR_OP(opstr.Substring 1), leftc+1, rightc)
                    false, (DOT, leftc, leftc)
                | PREFIX_OP opstr when opstr.StartsWithOrdinal(".") ->
                    delayToken(PREFIX_OP(opstr.Substring 1), leftc+1, rightc)
                    false, (DOT, leftc, leftc)
                | INFIX_STAR_DIV_MOD_OP opstr when opstr.StartsWithOrdinal(".") ->
                    delayToken(INFIX_STAR_DIV_MOD_OP(opstr.Substring 1), leftc+1, rightc)
                    false, (DOT, leftc, leftc)
                | INFIX_AMP_OP opstr when opstr.StartsWithOrdinal(".") ->
                    delayToken(INFIX_AMP_OP(opstr.Substring 1), leftc+1, rightc)
                    false, (DOT, leftc, leftc)
                | ADJACENT_PREFIX_OP opstr when opstr.StartsWithOrdinal(".") ->
                    delayToken(ADJACENT_PREFIX_OP(opstr.Substring 1), leftc+1, rightc)
                    false, (DOT, leftc, leftc)
                | FUNKY_OPERATOR_NAME opstr when opstr.StartsWithOrdinal(".") ->
                    delayToken(FUNKY_OPERATOR_NAME(opstr.Substring 1), leftc+1, rightc)
                    false, (DOT, leftc, leftc)
                | _ -> false, (token, leftc, rightc)
        with _ ->
            false, (EOF LexerStateEncoding.revertToDefaultLexCont, 0, 0)

    // Scan a token starting with the given lexer state
    member x.ScanToken (lexState: FSharpTokenizerLexState) : FSharpTokenInfo option * FSharpTokenizerLexState =

        use unwindBP = PushThreadBuildPhaseUntilUnwind BuildPhase.Parse
        use unwindEL = PushErrorLoggerPhaseUntilUnwind (fun _ -> DiscardErrorsLogger)

        let lightStatus, lexcont = LexerStateEncoding.decodeLexInt lexState
        let lightStatus = LightSyntaxStatus(lightStatus, false)

        // Grab a token
        let isCached, (token, leftc, rightc) = getTokenWithPosition lexcont lightStatus

        // Check for end-of-string and failure
        let tokenDataOption, lexcontFinal, tokenTag =
            match token with
            | EOF lexcont ->
                // End of text! No more tokens.
                None, lexcont, 0
            | LEX_FAILURE _ ->
                None, LexerStateEncoding.revertToDefaultLexCont, 0
            | _ ->
                // Get the information about the token
                let (colorClass, charClass, triggerClass) = TokenClassifications.tokenInfo token

                let lexcontFinal =
                    // If we're using token from cache, we don't move forward with lexing
                    if isCached then lexcont
                    else LexerStateEncoding.computeNextLexState token lexcont

                let tokenTag = tagOfToken token

                let tokenName = token_to_string token

                let fullMatchedLength = lexbuf.EndPos.AbsoluteOffset - lexbuf.StartPos.AbsoluteOffset

                let tokenData =
                    { TokenName = tokenName
                      LeftColumn=leftc
                      RightColumn=rightc
                      ColorClass=colorClass
                      CharClass=charClass
                      FSharpTokenTriggerClass=triggerClass
                      Tag=tokenTag
                      FullMatchedLength=fullMatchedLength}
                Some tokenData, lexcontFinal, tokenTag

        // Check for patterns like #-IDENT and see if they look like meta commands for .fsx files. If they do then merge them into a single token.
        let tokenDataOption, lexintFinal =
            let lexintFinal = LexerStateEncoding.encodeLexInt lightStatus.Status lexcontFinal
            match tokenDataOption, singleLineTokenState, tokenTagToTokenId tokenTag with
            | Some tokenData, SingleLineTokenState.BeforeHash, TOKEN_HASH ->
                // Don't allow further matches.
                singleLineTokenState <- SingleLineTokenState.NoFurtherMatchPossible
                // Peek at the next token
                let isCached, (nextToken, _, rightc) = getTokenWithPosition lexcont lightStatus
                match nextToken with
                | IDENT possibleMetaCommand ->
                    match fsx, possibleMetaCommand with
                    // These are for script (.fsx and .fsscript) files.
                    | true, "r"
                    | true, "reference"
                    | true, "I"
                    | true, "load"
                    | true, "time"
                    | true, "dbgbreak"
                    | true, "cd"
#if DEBUG
                    | true, "terms"
                    | true, "types"
                    | true, "savedll"
                    | true, "nosavedll"
#endif
                    | true, "silentCd"
                    | true, "q"
                    | true, "quit"
                    | true, "help"
                    // These are for script and non-script
                    | _, "nowarn" ->
                        // Merge both tokens into one.
                        let lexcontFinal = if isCached then lexcont else LexerStateEncoding.computeNextLexState token lexcont
                        let tokenData = {tokenData with RightColumn=rightc;ColorClass=FSharpTokenColorKind.PreprocessorKeyword;CharClass=FSharpTokenCharKind.Keyword;FSharpTokenTriggerClass=FSharpTokenTriggerClass.None}
                        let lexintFinal = LexerStateEncoding.encodeLexInt lightStatus.Status lexcontFinal
                        Some tokenData, lexintFinal
                    | _ -> tokenDataOption, lexintFinal
                | _ -> tokenDataOption, lexintFinal
            | _, SingleLineTokenState.BeforeHash, TOKEN_WHITESPACE ->
                // Allow leading whitespace.
                tokenDataOption, lexintFinal
            | _ ->
                singleLineTokenState <- SingleLineTokenState.NoFurtherMatchPossible
                tokenDataOption, lexintFinal

        tokenDataOption, lexintFinal

    static member ColorStateOfLexState(lexState: FSharpTokenizerLexState) =
        LexerStateEncoding.colorStateOfLexState lexState

    static member LexStateOfColorState(colorState: FSharpTokenizerColorState) =
        { PosBits = 0L; OtherBits = LexerStateEncoding.lexStateOfColorState colorState }

[<Sealed>]
type FSharpSourceTokenizer(conditionalDefines: string list, filename: string option) =

    // Public callers are unable to answer LanguageVersion feature support questions.
    // External Tools including the VS IDE will enable the default LanguageVersion
    let isFeatureSupported (_featureId:LanguageFeature) = true
    let checkLanguageFeatureErrorRecover (_featureId:LanguageFeature) _range = ()
    let reportLibraryOnlyFeatures = true

    let lexResourceManager = new Lexhelp.LexResourceManager()

    let lexargs = mkLexargs(conditionalDefines, LightSyntaxStatus(true, false), lexResourceManager, [], DiscardErrorsLogger, PathMap.empty)

    member _.CreateLineTokenizer(lineText: string) =
        let lexbuf = UnicodeLexing.StringAsLexbuf(reportLibraryOnlyFeatures, isFeatureSupported, checkLanguageFeatureErrorRecover, lineText)
        FSharpLineTokenizer(lexbuf, Some lineText.Length, filename, lexargs)

    member _.CreateBufferTokenizer bufferFiller =
        let lexbuf = UnicodeLexing.FunctionAsLexbuf(reportLibraryOnlyFeatures, isFeatureSupported, checkLanguageFeatureErrorRecover, bufferFiller)
        FSharpLineTokenizer(lexbuf, None, filename, lexargs)

module FSharpKeywords =
    open FSharp.Compiler.Lexhelp.Keywords

    let DoesIdentifierNeedQuotation s = DoesIdentifierNeedQuotation s

    let QuoteIdentifierIfNeeded s = QuoteIdentifierIfNeeded s

    let NormalizeIdentifierBackticks s = NormalizeIdentifierBackticks s

    let KeywordsWithDescription = keywordsWithDescription

    let KeywordNames = Lexhelp.Keywords.keywordNames

[<Flags>]
type FSharpLexerFlags =
    | Default                       = 0x11011
    | LightSyntaxOn                 = 0x00001
    | Compiling                     = 0x00010
    | CompilingFSharpCore           = 0x00110
    | SkipTrivia                    = 0x01000
    | UseLexFilter                  = 0x10000

[<RequireQualifiedAccess>]
type FSharpTokenKind =
    | None
    | HashIf
    | HashElse
    | HashEndIf
    | CommentTrivia
    | WhitespaceTrivia
    | HashLine
    | HashLight
    | InactiveCode
    | LineCommentTrivia
    | StringText
    | Fixed
    | OffsideInterfaceMember
    | OffsideBlockEnd
    | OffsideRightBlockEnd
    | OffsideDeclEnd
    | OffsideEnd
    | OffsideBlockSep
    | OffsideBlockBegin
    | OffsideReset
    | OffsideFun
    | OffsideFunction
    | OffsideWith
    | OffsideElse
    | OffsideThen
    | OffsideDoBang
    | OffsideDo
    | OffsideBinder
    | OffsideLet
    | HighPrecedenceTypeApp
    | HighPrecedenceParenthesisApp
    | HighPrecedenceBracketApp
    | Extern
    | Void
    | Public
    | Private
    | Internal
    | Global
    | Static
    | Member
    | Class
    | Abstract
    | Override
    | Default
    | Constructor
    | Inherit
    | GreaterRightBracket
    | Struct
    | Sig
    | Bar
    | RightBracket
    | RightBrace
    | Minus
    | Dollar
    | BarRightBracket
    | BarRightBrace
    | Underscore
    | Semicolon
    | SemicolonSemicolon
    | LeftArrow
    | Equals
    | LeftBracket
    | LeftBracketBar
    | LeftBraceBar
    | LeftBracketLess
    | LeftBrace
    | QuestionMark
    | QuestionMarkQuestionMark
    | Dot
    | Colon
    | ColonColon
    | ColonGreater
    | ColonQuestionMark
    | ColonQuestionMarkGreater
    | ColonEquals
    | When
    | While
    | With
    | Hash
    | Ampersand
    | AmpersandAmpersand
    | Quote
    | LeftParenthesis
    | RightParenthesis
    | Star
    | Comma
    | RightArrow
    | GreaterBarRightBracket
    | LeftParenthesisStarRightParenthesis
    | Open
    | Or
    | Rec
    | Then
    | To
    | True
    | Try
    | Type
    | Val
    | Inline
    | Interface
    | Instance
    | Const
    | Lazy
    | OffsideLazy
    | Match
    | MatchBang
    | Mutable
    | New
    | Of
    | Exception
    | False
    | For
    | Fun
    | Function
    | If
    | In
    | JoinIn
    | Finally
    | DoBang
    | And
    | As
    | Assert
    | OffsideAssert
    | Begin
    | Do
    | Done
    | DownTo
    | Else
    | Elif
    | End
    | DotDot
    | DotDotHat
    | BarBar
    | Upcast
    | Downcast
    | Null
    | Reserved
    | Module
    | Namespace
    | Delegate
    | Constraint
    | Base
    | LeftQuote
    | RightQuote
    | RightQuoteDot
    | PercentOperator
    | Binder
    | Less
    | Greater
    | Let
    | Yield
    | YieldBang
    | BigNumber
    | Decimal
    | Char
    | Ieee64
    | Ieee32
    | NativeInt
    | UNativeInt
    | UInt64
    | UInt32
    | UInt16
    | UInt8
    | Int64
    | Int32
    | Int32DotDot
    | Int16
    | Int8
    | FunkyOperatorName
    | AdjacentPrefixOperator
    | PlusMinusOperator
    | InfixAmpersandOperator
    | InfixStarDivideModuloOperator
    | PrefixOperator
    | InfixBarOperator
    | InfixAtHatOperator
    | InfixCompareOperator
    | InfixStarStarOperator
    | Identifier
    | KeywordString
    | String
    | ByteArray
    | Asr
    | InfixAsr
    | InfixLand
    | InfixLor
    | InfixLsl
    | InfixLsr
    | InfixLxor
    | InfixMod

[<Struct;NoComparison;NoEquality>]
type FSharpToken =

    val private tok: Parser.token
    val private tokRange: range

    new (tok, tokRange) = { tok = tok; tokRange = tokRange }

    member this.Range = this.tokRange

    member this.Kind =
        match this.tok with
        | ASR -> FSharpTokenKind.Asr
        | INFIX_STAR_STAR_OP "asr" -> FSharpTokenKind.Asr
        | INFIX_STAR_DIV_MOD_OP "land" -> FSharpTokenKind.InfixLand
        | INFIX_STAR_DIV_MOD_OP "lor" -> FSharpTokenKind.InfixLor
        | INFIX_STAR_STAR_OP "lsl" -> FSharpTokenKind.InfixLsl
        | INFIX_STAR_STAR_OP "lsr" -> FSharpTokenKind.InfixLsr
        | INFIX_STAR_DIV_MOD_OP "lxor" -> FSharpTokenKind.InfixLxor
        | INFIX_STAR_DIV_MOD_OP "mod" -> FSharpTokenKind.InfixMod
        | HASH_IF _ -> FSharpTokenKind.HashIf
        | HASH_ELSE _ -> FSharpTokenKind.HashElse
        | HASH_ENDIF _ -> FSharpTokenKind.HashEndIf
        | COMMENT _ -> FSharpTokenKind.CommentTrivia
        | WHITESPACE _ -> FSharpTokenKind.WhitespaceTrivia
        | HASH_LINE _ -> FSharpTokenKind.HashLine
        | HASH_LIGHT _ -> FSharpTokenKind.HashLight
        | INACTIVECODE _ -> FSharpTokenKind.InactiveCode
        | LINE_COMMENT _ -> FSharpTokenKind.LineCommentTrivia
        | STRING_TEXT _ -> FSharpTokenKind.StringText
        | FIXED  -> FSharpTokenKind.Fixed
        | OINTERFACE_MEMBER  -> FSharpTokenKind.OffsideInterfaceMember
        | OBLOCKEND  -> FSharpTokenKind.OffsideBlockEnd
        | ORIGHT_BLOCK_END  -> FSharpTokenKind.OffsideRightBlockEnd
        | ODECLEND  -> FSharpTokenKind.OffsideDeclEnd
        | OEND  -> FSharpTokenKind.OffsideEnd
        | OBLOCKSEP  -> FSharpTokenKind.OffsideBlockSep
        | OBLOCKBEGIN  -> FSharpTokenKind.OffsideBlockBegin
        | ORESET  -> FSharpTokenKind.OffsideReset
        | OFUN  -> FSharpTokenKind.OffsideFun
        | OFUNCTION  -> FSharpTokenKind.OffsideFunction
        | OWITH  -> FSharpTokenKind.OffsideWith
        | OELSE  -> FSharpTokenKind.OffsideElse
        | OTHEN  -> FSharpTokenKind.OffsideThen
        | ODO_BANG  -> FSharpTokenKind.OffsideDoBang
        | ODO  -> FSharpTokenKind.OffsideDo
        | OBINDER _ -> FSharpTokenKind.OffsideBinder
        | OLET _ -> FSharpTokenKind.OffsideLet
        | HIGH_PRECEDENCE_TYAPP  -> FSharpTokenKind.HighPrecedenceTypeApp
        | HIGH_PRECEDENCE_PAREN_APP  -> FSharpTokenKind.HighPrecedenceParenthesisApp
        | HIGH_PRECEDENCE_BRACK_APP  -> FSharpTokenKind.HighPrecedenceBracketApp
        | EXTERN  -> FSharpTokenKind.Extern
        | VOID  -> FSharpTokenKind.Void
        | PUBLIC  -> FSharpTokenKind.Public
        | PRIVATE  -> FSharpTokenKind.Private
        | INTERNAL  -> FSharpTokenKind.Internal
        | GLOBAL  -> FSharpTokenKind.Global
        | STATIC  -> FSharpTokenKind.Static
        | MEMBER  -> FSharpTokenKind.Member
        | CLASS  -> FSharpTokenKind.Class
        | ABSTRACT  -> FSharpTokenKind.Abstract
        | OVERRIDE  -> FSharpTokenKind.Override
        | DEFAULT  -> FSharpTokenKind.Default
        | CONSTRUCTOR  -> FSharpTokenKind.Constructor
        | INHERIT  -> FSharpTokenKind.Inherit
        | GREATER_RBRACK  -> FSharpTokenKind.GreaterRightBracket
        | STRUCT  -> FSharpTokenKind.Struct
        | SIG  -> FSharpTokenKind.Sig
        | BAR  -> FSharpTokenKind.Bar
        | RBRACK  -> FSharpTokenKind.RightBracket
        | RBRACE _ -> FSharpTokenKind.RightBrace
        | MINUS  -> FSharpTokenKind.Minus
        | DOLLAR  -> FSharpTokenKind.Dollar
        | BAR_RBRACK  -> FSharpTokenKind.BarRightBracket
        | BAR_RBRACE  -> FSharpTokenKind.BarRightBrace
        | UNDERSCORE  -> FSharpTokenKind.Underscore
        | SEMICOLON_SEMICOLON  -> FSharpTokenKind.SemicolonSemicolon
        | LARROW  -> FSharpTokenKind.LeftArrow
        | EQUALS  -> FSharpTokenKind.Equals
        | LBRACK  -> FSharpTokenKind.LeftBracket
        | LBRACK_BAR  -> FSharpTokenKind.LeftBracketBar
        | LBRACE_BAR  -> FSharpTokenKind.LeftBraceBar
        | LBRACK_LESS  -> FSharpTokenKind.LeftBracketLess
        | LBRACE _ -> FSharpTokenKind.LeftBrace
        | QMARK  -> FSharpTokenKind.QuestionMark
        | QMARK_QMARK  -> FSharpTokenKind.QuestionMarkQuestionMark
        | DOT  -> FSharpTokenKind.Dot
        | COLON  -> FSharpTokenKind.Colon
        | COLON_COLON  -> FSharpTokenKind.ColonColon
        | COLON_GREATER  -> FSharpTokenKind.ColonGreater
        | COLON_QMARK_GREATER  -> FSharpTokenKind.ColonQuestionMarkGreater
        | COLON_QMARK  -> FSharpTokenKind.ColonQuestionMark
        | COLON_EQUALS  -> FSharpTokenKind.ColonEquals
        | SEMICOLON  -> FSharpTokenKind.SemicolonSemicolon
        | WHEN  -> FSharpTokenKind.When
        | WHILE  -> FSharpTokenKind.While
        | WITH  -> FSharpTokenKind.With
        | HASH  -> FSharpTokenKind.Hash
        | AMP  -> FSharpTokenKind.Ampersand
        | AMP_AMP  -> FSharpTokenKind.AmpersandAmpersand
        | QUOTE  -> FSharpTokenKind.RightQuote
        | LPAREN  -> FSharpTokenKind.LeftParenthesis
        | RPAREN  -> FSharpTokenKind.RightParenthesis
        | STAR  -> FSharpTokenKind.Star
        | COMMA  -> FSharpTokenKind.Comma
        | RARROW  -> FSharpTokenKind.RightArrow
        | GREATER_BAR_RBRACK  -> FSharpTokenKind.GreaterBarRightBracket
        | LPAREN_STAR_RPAREN  -> FSharpTokenKind.LeftParenthesisStarRightParenthesis
        | OPEN  -> FSharpTokenKind.Open
        | OR  -> FSharpTokenKind.Or
        | REC  -> FSharpTokenKind.Rec
        | THEN  -> FSharpTokenKind.Then
        | TO  -> FSharpTokenKind.To
        | TRUE  -> FSharpTokenKind.True
        | TRY  -> FSharpTokenKind.Try
        | TYPE  -> FSharpTokenKind.Type
        | VAL  -> FSharpTokenKind.Val
        | INLINE  -> FSharpTokenKind.Inline
        | INTERFACE  -> FSharpTokenKind.Interface
        | INSTANCE  -> FSharpTokenKind.Instance
        | CONST  -> FSharpTokenKind.Const
        | LAZY  -> FSharpTokenKind.Lazy
        | OLAZY  -> FSharpTokenKind.OffsideLazy
        | MATCH  -> FSharpTokenKind.Match
        | MATCH_BANG  -> FSharpTokenKind.MatchBang
        | MUTABLE  -> FSharpTokenKind.Mutable
        | NEW  -> FSharpTokenKind.New
        | OF  -> FSharpTokenKind.Of
        | EXCEPTION  -> FSharpTokenKind.Exception
        | FALSE  -> FSharpTokenKind.False
        | FOR  -> FSharpTokenKind.For
        | FUN  -> FSharpTokenKind.Fun
        | FUNCTION  -> FSharpTokenKind.Function
        | IF  -> FSharpTokenKind.If
        | IN  -> FSharpTokenKind.In
        | JOIN_IN  -> FSharpTokenKind.JoinIn
        | FINALLY  -> FSharpTokenKind.Finally
        | DO_BANG  -> FSharpTokenKind.DoBang
        | AND  -> FSharpTokenKind.And
        | AS  -> FSharpTokenKind.As
        | ASSERT  -> FSharpTokenKind.Assert
        | OASSERT  -> FSharpTokenKind.OffsideAssert
        | BEGIN  -> FSharpTokenKind.Begin
        | DO  -> FSharpTokenKind.Do
        | DONE  -> FSharpTokenKind.Done
        | DOWNTO  -> FSharpTokenKind.DownTo
        | ELSE  -> FSharpTokenKind.Else
        | ELIF  -> FSharpTokenKind.Elif
        | END  -> FSharpTokenKind.End
        | DOT_DOT  -> FSharpTokenKind.DotDot
        | DOT_DOT_HAT  -> FSharpTokenKind.DotDotHat
        | BAR_BAR  -> FSharpTokenKind.BarBar
        | UPCAST  -> FSharpTokenKind.Upcast
        | DOWNCAST  -> FSharpTokenKind.Downcast
        | NULL  -> FSharpTokenKind.Null
        | RESERVED  -> FSharpTokenKind.Reserved
        | MODULE  -> FSharpTokenKind.Module
        | NAMESPACE  -> FSharpTokenKind.Namespace
        | DELEGATE  -> FSharpTokenKind.Delegate
        | CONSTRAINT  -> FSharpTokenKind.Constraint
        | BASE  -> FSharpTokenKind.Base
        | LQUOTE _ -> FSharpTokenKind.LeftQuote
        | RQUOTE _ -> FSharpTokenKind.RightQuote
        | RQUOTE_DOT _ -> FSharpTokenKind.RightQuoteDot
        | PERCENT_OP _ -> FSharpTokenKind.PercentOperator
        | BINDER _ -> FSharpTokenKind.Binder
        | LESS _ -> FSharpTokenKind.Less
        | GREATER _ -> FSharpTokenKind.Greater
        | LET _ -> FSharpTokenKind.Let
        | YIELD _ -> FSharpTokenKind.Yield
        | YIELD_BANG _ -> FSharpTokenKind.YieldBang
        | BIGNUM _ -> FSharpTokenKind.BigNumber
        | DECIMAL _ -> FSharpTokenKind.Decimal
        | CHAR _ -> FSharpTokenKind.Char
        | IEEE64 _ -> FSharpTokenKind.Ieee64
        | IEEE32 _ -> FSharpTokenKind.Ieee32
        | NATIVEINT _ -> FSharpTokenKind.NativeInt
        | UNATIVEINT _ -> FSharpTokenKind.UNativeInt
        | UINT64 _ -> FSharpTokenKind.UInt64
        | UINT32 _ -> FSharpTokenKind.UInt32
        | UINT16 _ -> FSharpTokenKind.UInt16
        | UINT8 _ -> FSharpTokenKind.UInt8
        | INT64 _ -> FSharpTokenKind.UInt64
        | INT32 _ -> FSharpTokenKind.Int32
        | INT32_DOT_DOT _ -> FSharpTokenKind.Int32DotDot
        | INT16 _ -> FSharpTokenKind.Int16
        | INT8 _ -> FSharpTokenKind.Int8
        | FUNKY_OPERATOR_NAME _ -> FSharpTokenKind.FunkyOperatorName
        | ADJACENT_PREFIX_OP _ -> FSharpTokenKind.AdjacentPrefixOperator
        | PLUS_MINUS_OP _ -> FSharpTokenKind.PlusMinusOperator
        | INFIX_AMP_OP _ -> FSharpTokenKind.InfixAmpersandOperator
        | INFIX_STAR_DIV_MOD_OP _ -> FSharpTokenKind.InfixStarDivideModuloOperator
        | PREFIX_OP _ -> FSharpTokenKind.PrefixOperator
        | INFIX_BAR_OP _ -> FSharpTokenKind.InfixBarOperator
        | INFIX_AT_HAT_OP _ -> FSharpTokenKind.InfixAtHatOperator
        | INFIX_COMPARE_OP _ -> FSharpTokenKind.InfixCompareOperator
        | INFIX_STAR_STAR_OP _ -> FSharpTokenKind.InfixStarStarOperator
        | IDENT _ -> FSharpTokenKind.Identifier
        | KEYWORD_STRING _ -> FSharpTokenKind.KeywordString
        | INTERP_STRING_BEGIN_END _
        | INTERP_STRING_BEGIN_PART _
        | INTERP_STRING_PART _
        | INTERP_STRING_END _
        | STRING _ -> FSharpTokenKind.String
        | BYTEARRAY _ -> FSharpTokenKind.ByteArray
        | _ -> FSharpTokenKind.None

    member this.IsKeyword =
        match this.Kind with
        | FSharpTokenKind.Abstract
        | FSharpTokenKind.And
        | FSharpTokenKind.As
        | FSharpTokenKind.Assert
        | FSharpTokenKind.OffsideAssert
        | FSharpTokenKind.Base
        | FSharpTokenKind.Begin
        | FSharpTokenKind.Class
        | FSharpTokenKind.Default
        | FSharpTokenKind.Delegate
        | FSharpTokenKind.Do
        | FSharpTokenKind.OffsideDo
        | FSharpTokenKind.Done
        | FSharpTokenKind.Downcast
        | FSharpTokenKind.DownTo
        | FSharpTokenKind.Elif
        | FSharpTokenKind.Else
        | FSharpTokenKind.OffsideElse
        | FSharpTokenKind.End
        | FSharpTokenKind.OffsideEnd
        | FSharpTokenKind.Exception
        | FSharpTokenKind.Extern
        | FSharpTokenKind.False
        | FSharpTokenKind.Finally
        | FSharpTokenKind.Fixed
        | FSharpTokenKind.For
        | FSharpTokenKind.Fun
        | FSharpTokenKind.OffsideFun
        | FSharpTokenKind.Function
        | FSharpTokenKind.OffsideFunction
        | FSharpTokenKind.Global
        | FSharpTokenKind.If
        | FSharpTokenKind.In
        | FSharpTokenKind.Inherit
        | FSharpTokenKind.Inline
        | FSharpTokenKind.Interface
        | FSharpTokenKind.OffsideInterfaceMember
        | FSharpTokenKind.Internal
        | FSharpTokenKind.Lazy
        | FSharpTokenKind.OffsideLazy
        | FSharpTokenKind.Let // "let" and "use"
        | FSharpTokenKind.OffsideLet
        | FSharpTokenKind.DoBang //  "let!", "use!" and "do!"
        | FSharpTokenKind.OffsideDoBang
        | FSharpTokenKind.Match
        | FSharpTokenKind.MatchBang
        | FSharpTokenKind.Member
        | FSharpTokenKind.Module
        | FSharpTokenKind.Mutable
        | FSharpTokenKind.Namespace
        | FSharpTokenKind.New
        // | FSharpTokenKind.Not // Not actually a keyword. However, not struct in combination is used as a generic parameter constraint.
        | FSharpTokenKind.Null
        | FSharpTokenKind.Of
        | FSharpTokenKind.Open
        | FSharpTokenKind.Or
        | FSharpTokenKind.Override
        | FSharpTokenKind.Private
        | FSharpTokenKind.Public
        | FSharpTokenKind.Rec
        | FSharpTokenKind.Yield // "yield" and "return"
        | FSharpTokenKind.YieldBang // "yield!" and "return!"
        | FSharpTokenKind.Static
        | FSharpTokenKind.Struct
        | FSharpTokenKind.Then
        | FSharpTokenKind.To
        | FSharpTokenKind.True
        | FSharpTokenKind.Try
        | FSharpTokenKind.Type
        | FSharpTokenKind.Upcast
        | FSharpTokenKind.Val
        | FSharpTokenKind.Void
        | FSharpTokenKind.When
        | FSharpTokenKind.While
        | FSharpTokenKind.With
        | FSharpTokenKind.OffsideWith

        // * Reserved - from OCAML *
        | FSharpTokenKind.Asr
        | FSharpTokenKind.InfixAsr
        | FSharpTokenKind.InfixLand
        | FSharpTokenKind.InfixLor
        | FSharpTokenKind.InfixLsl
        | FSharpTokenKind.InfixLsr
        | FSharpTokenKind.InfixLxor
        | FSharpTokenKind.InfixMod
        | FSharpTokenKind.Sig

        // * Reserved - for future *
        // atomic
        // break
        // checked
        // component
        // const
        // constraint
        // constructor
        // continue
        // eager
        // event
        // external
        // functor
        // include
        // method
        // mixin
        // object
        // parallel
        // process
        // protected
        // pure
        // sealed
        // tailcall
        // trait
        // virtual
        // volatile
        | FSharpTokenKind.Reserved
        | FSharpTokenKind.KeywordString
        | FSharpTokenKind.Binder
        | FSharpTokenKind.OffsideBinder -> true
        | _ -> false

    member this.IsIdentifier =
        match this.Kind with
        | FSharpTokenKind.Identifier -> true
        | _ -> false

    member this.IsStringLiteral =
        match this.Kind with
        | FSharpTokenKind.String -> true
        | _ -> false

    member this.IsNumericLiteral =
        match this.Kind with
        | FSharpTokenKind.UInt8
        | FSharpTokenKind.UInt16
        | FSharpTokenKind.UInt64
        | FSharpTokenKind.Int8
        | FSharpTokenKind.Int16
        | FSharpTokenKind.Int32
        | FSharpTokenKind.Int64
        | FSharpTokenKind.Ieee32
        | FSharpTokenKind.Ieee64
        | FSharpTokenKind.BigNumber -> true
        | _ -> false

    member this.IsCommentTrivia =
        match this.Kind with
        | FSharpTokenKind.CommentTrivia
        | FSharpTokenKind.LineCommentTrivia -> true
        | _ -> false

[<AutoOpen>]
module FSharpLexerImpl =
    let lexWithErrorLogger (text: ISourceText) conditionalCompilationDefines (flags: FSharpLexerFlags) reportLibraryOnlyFeatures supportsFeature checkLanguageFeatureErrorRecover errorLogger onToken pathMap (ct: CancellationToken) =
        let canSkipTrivia = (flags &&& FSharpLexerFlags.SkipTrivia) = FSharpLexerFlags.SkipTrivia
        let isLightSyntaxOn = (flags &&& FSharpLexerFlags.LightSyntaxOn) = FSharpLexerFlags.LightSyntaxOn
        let isCompiling = (flags &&& FSharpLexerFlags.Compiling) = FSharpLexerFlags.Compiling
        let isCompilingFSharpCore = (flags &&& FSharpLexerFlags.CompilingFSharpCore) = FSharpLexerFlags.CompilingFSharpCore
        let canUseLexFilter = (flags &&& FSharpLexerFlags.UseLexFilter) = FSharpLexerFlags.UseLexFilter

        let lexbuf = UnicodeLexing.SourceTextAsLexbuf(reportLibraryOnlyFeatures, supportsFeature, checkLanguageFeatureErrorRecover, text)
        let lightStatus = LightSyntaxStatus(isLightSyntaxOn, true)
        let lexargs = mkLexargs (conditionalCompilationDefines, lightStatus, Lexhelp.LexResourceManager(0), [], errorLogger, pathMap)
        let lexargs = { lexargs with applyLineDirectives = isCompiling }

        let getNextToken =
            let lexer = Lexer.token lexargs canSkipTrivia

            if canUseLexFilter then
                let lexFilter = LexFilter.LexFilter(lexargs.lightStatus, isCompilingFSharpCore, lexer, lexbuf)
                (fun _ -> lexFilter.GetToken())
            else
                lexer

        use _unwindBP = PushThreadBuildPhaseUntilUnwind BuildPhase.Parse
        use _unwindEL = PushErrorLoggerPhaseUntilUnwind (fun _ -> DiscardErrorsLogger)

        resetLexbufPos "" lexbuf
        while not lexbuf.IsPastEndOfStream do
            ct.ThrowIfCancellationRequested ()
            onToken (getNextToken lexbuf) lexbuf.LexemeRange

    let lex text conditionalCompilationDefines flags reportLibraryOnlyFeatures supportsFeature checkLanguageFeatureErrorRecover lexCallback pathMap ct =
        let errorLogger = CompilationErrorLogger("Lexer", FSharpDiagnosticOptions.Default)
        lexWithErrorLogger text conditionalCompilationDefines flags reportLibraryOnlyFeatures supportsFeature checkLanguageFeatureErrorRecover errorLogger lexCallback pathMap ct

[<AbstractClass;Sealed>]
type FSharpLexer =

    static member Tokenize(text: ISourceText, tokenCallback, ?langVersion, ?filePath: string, ?conditionalCompilationDefines, ?flags, ?pathMap, ?ct) =
        let langVersion = defaultArg langVersion "latestmajor" |> LanguageVersion
        let flags = defaultArg flags FSharpLexerFlags.Default
        ignore filePath // can be removed at later point
        let conditionalCompilationDefines = defaultArg conditionalCompilationDefines []
        let pathMap = defaultArg pathMap Map.Empty
        let ct = defaultArg ct CancellationToken.None

        let supportsFeature = langVersion.SupportsFeature
        let checkLanguageFeatureErrorRecover = ErrorLogger.checkLanguageFeatureErrorRecover langVersion

        let pathMap =
            (PathMap.empty, pathMap)
            ||> Seq.fold (fun state pair -> state |> PathMap.addMapping pair.Key pair.Value)

        let onToken tok m =
                let fsTok = FSharpToken(tok, m)
                match fsTok.Kind with
                | FSharpTokenKind.None -> ()
                | _ -> tokenCallback fsTok

        let reportLibraryOnlyFeatures = true
        lex text conditionalCompilationDefines flags reportLibraryOnlyFeatures supportsFeature checkLanguageFeatureErrorRecover onToken pathMap ct
