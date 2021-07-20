// Copyright (c) Microsoft Corporation. All Rights Reserved. See License.txt in the project root for license information.

/// LexFilter - process the token stream prior to parsing.
/// Implements the offside rule and a couple of other lexical transformations.
module internal FSharp.Compiler.LexFilter

open Internal.Utilities.Text.Lexing
open FSharp.Compiler 
open Internal.Utilities.Library
open FSharp.Compiler.AbstractIL.Diagnostics
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Features
open FSharp.Compiler.Lexhelp
open FSharp.Compiler.ParseHelpers
open FSharp.Compiler.Parser

let debug = false

let stringOfPos (p: Position) = sprintf "(%d:%d)" p.OriginalLine p.Column

let outputPos os (p: Position) = Printf.fprintf os "(%d:%d)" p.OriginalLine p.Column

/// Used for warning strings, which should display columns as 1-based and display 
/// the lines after taking '# line' directives into account (i.e. do not use
/// p.OriginalLine)
let warningStringOfPosition (p: Position) = warningStringOfCoords p.Line p.Column

type Context = 
    // Position is position of keyword.
    // bool indicates 'LET' is an offside let that's part of a CtxtSeqBlock where the 'in' is optional 
    | CtxtLetDecl of bool * Position  
    | CtxtIf of Position  
    | CtxtTry of Position  
    | CtxtFun of Position  
    | CtxtFunction of Position  
    | CtxtWithAsLet of Position  // 'with' when used in an object expression 
    | CtxtWithAsAugment of Position   // 'with' as used in a type augmentation 
    | CtxtMatch of Position  
    | CtxtFor of Position  
    | CtxtWhile of Position  
    | CtxtWhen of Position   
    | CtxtVanilla of Position * bool // boolean indicates if vanilla started with 'x = ...' or 'x.y = ...'
    | CtxtThen of Position  
    | CtxtElse of Position 
    | CtxtDo of Position 
    | CtxtInterfaceHead of Position 
    | CtxtTypeDefns of Position    // 'type <here> =', not removed when we find the "="
    
    | CtxtNamespaceHead of Position * token
    | CtxtModuleHead of Position * token * LexingModuleAttributes
    | CtxtMemberHead of Position 
    | CtxtMemberBody of Position 
    // If bool is true then this is "whole file" 
    //     module A.B
    // If bool is false, this is a "module declaration" 
    //     module A = ...
    | CtxtModuleBody of Position * bool 
    | CtxtNamespaceBody of Position 
    | CtxtException of Position 
    | CtxtParen of Parser.token * Position 
    // Position is position of following token 
    | CtxtSeqBlock of FirstInSequence * Position * AddBlockEnd   
    // first bool indicates "was this 'with' followed immediately by a '|'"? 
    | CtxtMatchClauses of bool * Position   

    member c.StartPos = 
        match c with 
        | CtxtNamespaceHead (p, _) | CtxtModuleHead (p, _, _) | CtxtException p | CtxtModuleBody (p, _) | CtxtNamespaceBody p
        | CtxtLetDecl (_, p) | CtxtDo p | CtxtInterfaceHead p | CtxtTypeDefns p | CtxtParen (_, p) | CtxtMemberHead p | CtxtMemberBody p
        | CtxtWithAsLet p
        | CtxtWithAsAugment p
        | CtxtMatchClauses (_, p) | CtxtIf p | CtxtMatch p | CtxtFor p | CtxtWhile p | CtxtWhen p | CtxtFunction p | CtxtFun p | CtxtTry p | CtxtThen p | CtxtElse p | CtxtVanilla (p, _)
        | CtxtSeqBlock (_, p, _) -> p

    member c.StartCol = c.StartPos.Column

    override c.ToString() = 
        match c with 
        | CtxtNamespaceHead _ -> "nshead"
        | CtxtModuleHead _ -> "modhead"
        | CtxtException _ -> "exception"
        | CtxtModuleBody _ -> "modbody"
        | CtxtNamespaceBody _ -> "nsbody"
        | CtxtLetDecl(b, p) -> sprintf "let(%b, %s)" b (stringOfPos p)
        | CtxtWithAsLet p -> sprintf "withlet(%s)" (stringOfPos p)
        | CtxtWithAsAugment _ -> "withaug"
        | CtxtDo _ -> "do"
        | CtxtInterfaceHead _ -> "interface-decl"
        | CtxtTypeDefns _ -> "type"
        | CtxtParen(_, p) -> sprintf "paren(%s)" (stringOfPos p)
        | CtxtMemberHead _ -> "member-head"
        | CtxtMemberBody _ -> "body"
        | CtxtSeqBlock (b, p, _addBlockEnd) -> sprintf "seqblock(%s, %s)" (match b with FirstInSeqBlock -> "first" | NotFirstInSeqBlock -> "subsequent") (stringOfPos p)
        | CtxtMatchClauses _ -> "matching"

        | CtxtIf _ -> "if"
        | CtxtMatch _ -> "match"
        | CtxtFor _ -> "for"
        | CtxtWhile p -> sprintf "while(%s)" (stringOfPos p)
        | CtxtWhen _ -> "when" 
        | CtxtTry _ -> "try"
        | CtxtFun _ -> "fun"
        | CtxtFunction _ -> "function"

        | CtxtThen _ -> "then"
        | CtxtElse p -> sprintf "else(%s)" (stringOfPos p)
        | CtxtVanilla (p, _) -> sprintf "vanilla(%s)" (stringOfPos p)
  
and AddBlockEnd = AddBlockEnd | NoAddBlockEnd | AddOneSidedBlockEnd
and FirstInSequence = FirstInSeqBlock | NotFirstInSeqBlock
and LexingModuleAttributes = LexingModuleAttributes | NotLexingModuleAttributes


let isInfix token = 
    match token with 
    | COMMA 
    | BAR_BAR 
    | AMP_AMP 
    | AMP 
    | OR
    | INFIX_BAR_OP _ 
    | INFIX_AMP_OP _  
    | INFIX_COMPARE_OP _ 
    | DOLLAR 
    // For the purposes of #light processing, <, > and = are not considered to be infix operators.
    // This is because treating them as infix conflicts with their role in other parts of the grammar,
    // e.g. to delimit "f<int>", or for "let f x = ...." 
    //
    // This has the impact that a SeqBlock does not automatically start on the right of a "<", ">" or "=",
    // e.g.
    //     let f x = (x = 
    //                   let a = 1 // no #light block started here, parentheses or 'in' needed
    //                   a + x)
    // LESS | GREATER | EQUALS
    
    | INFIX_AT_HAT_OP _
    | PLUS_MINUS_OP _ 
    | COLON_COLON
    | COLON_GREATER
    | COLON_QMARK_GREATER
    | COLON_EQUALS 
    | MINUS  
    | STAR 
    | INFIX_STAR_DIV_MOD_OP _
    | INFIX_STAR_STAR_OP _ 
    | QMARK_QMARK -> true
    | _ -> false

let isNonAssocInfixToken token = 
    match token with 
    | EQUALS -> true
    | _ -> false

let infixTokenLength token = 
    match token with 
    | COMMA -> 1
    | AMP -> 1
    | OR -> 1
    | DOLLAR -> 1
    | MINUS -> 1  
    | STAR -> 1
    | BAR -> 1
    | LESS false -> 1
    | GREATER false -> 1
    | EQUALS -> 1
    | QMARK_QMARK -> 2
    | COLON_GREATER -> 2
    | COLON_COLON -> 2
    | COLON_EQUALS -> 2
    | BAR_BAR -> 2
    | AMP_AMP -> 2
    | INFIX_BAR_OP d 
    | INFIX_AMP_OP d  
    | INFIX_COMPARE_OP d 
    | INFIX_AT_HAT_OP d
    | PLUS_MINUS_OP d 
    | INFIX_STAR_DIV_MOD_OP d
    | INFIX_STAR_STAR_OP d -> d.Length
    | COLON_QMARK_GREATER -> 3
    | _ -> assert false; 1


/// Determine the tokens that may align with the 'if' of an 'if/then/elif/else' without closing
/// the construct
let rec isIfBlockContinuator token =
    match token with 
    // The following tokens may align with the "if" without closing the "if", e.g.
    //    if ...
    //    then ...
    //    elif ...
    //    else ... 
    | THEN | ELSE | ELIF -> true  
    // Likewise 
    //    if ... then (
    //    ) elif begin 
    //    end else ... 
    | END | RPAREN -> true  
    // The following arise during reprocessing of the inserted tokens, e.g. when we hit a DONE 
    | ORIGHT_BLOCK_END | OBLOCKEND | ODECLEND -> true 
    | ODUMMY token -> isIfBlockContinuator token
    | _ -> false

/// Determine the token that may align with the 'try' of a 'try/with' or 'try/finally' without closing
/// the construct
let rec isTryBlockContinuator token =
    match token with 
    // These tokens may align with the "try" without closing the construct, e.g.
    //         try ...
    //         with ... 
    | FINALLY | WITH -> true  
    // The following arise during reprocessing of the inserted tokens when we hit a DONE
    | ORIGHT_BLOCK_END | OBLOCKEND | ODECLEND -> true 
    | ODUMMY token -> isTryBlockContinuator token
    | _ -> false

let rec isThenBlockContinuator token =
    match token with 
    // The following arise during reprocessing of the inserted tokens when we hit a DONE
    | ORIGHT_BLOCK_END | OBLOCKEND | ODECLEND -> true 
    | ODUMMY token -> isThenBlockContinuator token
    | _ -> false

let rec isDoContinuator token =
    match token with 
    // These tokens may align with the "for" without closing the construct, e.g.
    //                       for ... 
    //                          do 
    //                             ... 
    //                          done *)  
    | DONE -> true 
    | ORIGHT_BLOCK_END | OBLOCKEND | ODECLEND -> true // The following arise during reprocessing of the inserted tokens when we hit a DONE
    | ODUMMY token -> isDoContinuator token
    | _ -> false

let rec isInterfaceContinuator token =
    match token with 
    // These tokens may align with the token "interface" without closing the construct, e.g.
    //                       interface ... with 
    //                         ...
    //                       end   
    | END -> true 
    | ORIGHT_BLOCK_END | OBLOCKEND | ODECLEND -> true // The following arise during reprocessing of the inserted tokens when we hit a DONE
    | ODUMMY token -> isInterfaceContinuator token
    | _ -> false

let rec isNamespaceContinuator token =
    match token with 
    // These tokens end the construct, e.g. 
    //     namespace A.B.C 
    //     ...
    //     namespace <-- here
    //     .... 
    | Parser.EOF _ | NAMESPACE -> false 
    | ODUMMY token -> isNamespaceContinuator token
    | _ -> true // anything else is a namespace continuator 

let rec isTypeContinuator token =
    match token with 
    // The following tokens may align with the token "type" without closing the construct, e.g.
    //     type X = 
    //     | A
    //     | B
    //     and Y = c <--- 'and' HERE
    //     
    //     type X = {
    //        x: int
    //        y: int
    //     }                     <---          '}' HERE
    //     and Y = c 
    //
    //     type Complex = struct
    //       val im : float
    //     end with                  <---          'end' HERE
    //       static member M() = 1
    //     end 
    | RBRACE _ | WITH | BAR | AND | END -> true 
                             
    // The following arise during reprocessing of the inserted tokens when we hit a DONE 
    | ORIGHT_BLOCK_END | OBLOCKEND | ODECLEND -> true 
    | ODUMMY token -> isTypeContinuator token
    | _ -> false

let rec isForLoopContinuator token =
    match token with 
    // This token may align with the "for" without closing the construct, e.g.
    //                      for ... do 
    //                          ... 
    //                       done 
    | DONE -> true 
    | ORIGHT_BLOCK_END | OBLOCKEND | ODECLEND -> true// The following arise during reprocessing of the inserted tokens when we hit a DONE
    | ODUMMY token -> isForLoopContinuator token
    | _ -> false

let rec isWhileBlockContinuator token =
    match token with 
    // This token may align with the "while" without closing the construct, e.g.
    //                       while ... do 
    //                          ... 
    //                       done 
    | DONE -> true 
    | ORIGHT_BLOCK_END | OBLOCKEND | ODECLEND -> true // The following arise during reprocessing of the inserted tokens when we hit a DONE
    | ODUMMY token -> isWhileBlockContinuator token
    | _ -> false

let rec isLetContinuator token =
    match token with 
    // This token may align with the "let" without closing the construct, e.g.
    //                       let ...
    //                       and ... 
    | AND -> true  
    | ORIGHT_BLOCK_END | OBLOCKEND | ODECLEND -> true // The following arise during reprocessing of the inserted tokens when we hit a DONE
    | ODUMMY token -> isLetContinuator token
    | _ -> false

let rec isTypeSeqBlockElementContinuator token = 
    match token with 
    // A sequence of items separated by '|' counts as one sequence block element, e.g.
    // type x = 
    //   | A                 <-- These together count as one element
    //   | B                 <-- These together count as one element
    //   member x.M1
    //   member x.M2 
    | BAR -> true
    | OBLOCKBEGIN | ORIGHT_BLOCK_END | OBLOCKEND | ODECLEND -> true // The following arise during reprocessing of the inserted tokens when we hit a DONE
    | ODUMMY token -> isTypeSeqBlockElementContinuator token 
    | _ -> false

// Work out when a token doesn't terminate a single item in a sequence definition 
let rec isSeqBlockElementContinuator token =
    isInfix token || 
          // Infix tokens may align with the first column of a sequence block without closing a sequence element and starting a new one 
          // e.g. 
          //  let f x
          //      h x 
          //      |> y                              <------- NOTE: Not a new element in the sequence

    match token with 
    // The following tokens may align with the first column of a sequence block without closing a sequence element and starting a new one *)
    // e.g. 
    // new MenuItem("&Open...",
    //              new EventHandler(fun _ _ -> 
    //                  ...
    //              ), <------- NOTE RPAREN HERE
    //              Shortcut.CtrlO)
    | END | AND | WITH | THEN | RPAREN | RBRACE _ | BAR_RBRACE | RBRACK | BAR_RBRACK | RQUOTE _ -> true 

    // The following arise during reprocessing of the inserted tokens when we hit a DONE
    | ORIGHT_BLOCK_END | OBLOCKEND | ODECLEND -> true 
    | ODUMMY token -> isSeqBlockElementContinuator token 
    | _ -> false

let rec isWithAugmentBlockContinuator token = 
    match token with 
    // This token may align with "with" of an augmentation block without closing the construct, e.g.
    //                       interface Foo
    //                          with 
    //                             member ...
    //                          end 
    | END -> true    
    | ODUMMY token -> isWithAugmentBlockContinuator token
    | _ -> false

let isLongIdentifier token = (match token with IDENT _ | DOT -> true | _ -> false)
let isLongIdentifierOrGlobal token = (match token with GLOBAL | IDENT _ | DOT -> true | _ -> false)

let isAtomicExprEndToken token = 
    match token with
    | IDENT _ 
    | INT8 _ | INT16 _ | INT32 _ | INT64 _ | NATIVEINT _ 
    | UINT8 _ | UINT16 _ | UINT32 _ | UINT64 _ | UNATIVEINT _
    | DECIMAL _ | BIGNUM _ | STRING _ | BYTEARRAY _ | CHAR _ 
    | IEEE32 _ | IEEE64 _ 
    | RPAREN | RBRACK | RBRACE _ | BAR_RBRACE | BAR_RBRACK | END 
    | NULL | FALSE | TRUE | UNDERSCORE -> true
    | _ -> false
    
//----------------------------------------------------------------------------
// give a 'begin' token, does an 'end' token match?
//--------------------------------------------------------------------------
let parenTokensBalance t1 t2 = 
    match t1, t2 with 
    | (LPAREN, RPAREN) 
    | (LBRACE _, RBRACE _) 
    | (LBRACE_BAR, BAR_RBRACE) 
    | (LBRACK, RBRACK) 
    | (INTERFACE, END) 
    | (CLASS, END) 
    | (SIG, END) 
    | (STRUCT, END) 
    | (INTERP_STRING_BEGIN_PART _, INTERP_STRING_END _)
    | (INTERP_STRING_BEGIN_PART _, INTERP_STRING_PART _)
    | (INTERP_STRING_PART _, INTERP_STRING_PART _)
    | (INTERP_STRING_PART _, INTERP_STRING_END _)
    | (LBRACK_BAR, BAR_RBRACK)
    | (LESS true, GREATER true) 
    | (BEGIN, END) -> true 
    | (LQUOTE q1, RQUOTE q2) when q1 = q2 -> true 
    | _ -> false
    
/// Used to save some aspects of the lexbuffer state
[<Struct>]
type LexbufState(startPos: Position,
                 endPos : Position,
                 pastEOF : bool) = 
    member x.StartPos = startPos
    member x.EndPos = endPos
    member x.PastEOF = pastEOF

/// Used to save the state related to a token
/// Treat as though this is read-only.
[<Class>]
type TokenTup = 
    // This is mutable for performance reasons.
    val mutable Token : token
    val mutable LexbufState : LexbufState
    val mutable LastTokenPos: Position
    new (token, state, lastTokenPos) = { Token=token; LexbufState=state;LastTokenPos=lastTokenPos }
    
    /// Returns starting position of the token
    member x.StartPos = x.LexbufState.StartPos
    /// Returns end position of the token
    member x.EndPos = x.LexbufState.EndPos
        
type TokenTupPool() =

    /// Arbitrary.
    /// When parsing the compiler's source files, the pool didn't come close to reaching this limit.
    /// Therefore, this seems like a reasonable limit to handle 99% of cases.
    [<Literal>]
    let maxSize = 100

    let mutable currentPoolSize = 0
    let stack = System.Collections.Generic.Stack(10)

    member this.Rent() = 
        if stack.Count = 0 then
            if currentPoolSize < maxSize then
                stack.Push(TokenTup(Unchecked.defaultof<_>, Unchecked.defaultof<_>, Unchecked.defaultof<_>))
                currentPoolSize <- currentPoolSize + 1
                this.Rent()
            else
                assert false
                TokenTup(Unchecked.defaultof<_>, Unchecked.defaultof<_>, Unchecked.defaultof<_>)
        else
            stack.Pop()

    member _.Return(x: TokenTup) =
        x.Token <- Unchecked.defaultof<_>
        x.LexbufState <- Unchecked.defaultof<_>
        x.LastTokenPos <- Unchecked.defaultof<_>
        if stack.Count >= maxSize then
            assert false
        else
            stack.Push x

    /// Returns a token 'tok' with the same position as this token
    member pool.UseLocation(x: TokenTup, tok) = 
        let tokState = x.LexbufState 
        let tokTup = pool.Rent()
        tokTup.Token <- tok
        tokTup.LexbufState <- LexbufState(tokState.StartPos, tokState.EndPos, false)
        tokTup.LastTokenPos <- x.LastTokenPos
        tokTup
        
    /// Returns a token 'tok' with the same position as this token, except that 
    /// it is shifted by specified number of characters from the left and from the right
    /// Note: positive value means shift to the right in both cases
    member pool.UseShiftedLocation(x: TokenTup, tok, shiftLeft, shiftRight) = 
        let tokState = x.LexbufState
        let tokTup = pool.Rent()
        tokTup.Token <- tok
        tokTup.LexbufState <- LexbufState(tokState.StartPos.ShiftColumnBy shiftLeft, tokState.EndPos.ShiftColumnBy shiftRight, false)
        tokTup.LastTokenPos <- x.LastTokenPos
        tokTup     

//----------------------------------------------------------------------------
// Utilities for the tokenizer that are needed in other places
//--------------------------------------------------------------------------*)

// Strip a bunch of leading '>' of a token, at the end of a typar application
// Note: this is used in the 'service.fs' to do limited postprocessing
let (|TyparsCloseOp|_|) (txt: string) = 
    let angles = txt |> Seq.takeWhile (fun c -> c = '>') |> Seq.toList
    let afterAngles = txt |> Seq.skipWhile (fun c -> c = '>') |> Seq.toList
    if List.isEmpty angles then None else

    let afterOp = 
        match (new System.String(Array.ofSeq afterAngles)) with 
         | "." -> Some DOT
         | "]" -> Some RBRACK
         | "-" -> Some MINUS
         | ".." -> Some DOT_DOT 
         | "?" -> Some QMARK 
         | "??" -> Some QMARK_QMARK 
         | ":=" -> Some COLON_EQUALS 
         | "::" -> Some COLON_COLON
         | "*" -> Some STAR 
         | "&" -> Some AMP
         | "->" -> Some RARROW 
         | "<-" -> Some LARROW 
         | "=" -> Some EQUALS 
         | "<" -> Some (LESS false)
         | "$" -> Some DOLLAR
         | "%" -> Some (PERCENT_OP("%") )
         | "%%" -> Some (PERCENT_OP("%%"))
         | "" -> None
         | s -> 
             match List.ofSeq afterAngles with 
              | ('=' :: _)
              | ('!' :: '=' :: _)
              | ('<' :: _)
              | ('>' :: _)
              | ('$' :: _) -> Some (INFIX_COMPARE_OP s)
              | ('&' :: _) -> Some (INFIX_AMP_OP s)
              | ('|' :: _) -> Some (INFIX_BAR_OP s)
              | ('!' :: _)
              | ('?' :: _)
              | ('~' :: _) -> Some (PREFIX_OP s)
              | ('@' :: _)
              | ('^' :: _) -> Some (INFIX_AT_HAT_OP s)
              | ('+' :: _)
              | ('-' :: _) -> Some (PLUS_MINUS_OP s)
              | ('*' :: '*' :: _) -> Some (INFIX_STAR_STAR_OP s)
              | ('*' :: _)
              | ('/' :: _)
              | ('%' :: _) -> Some (INFIX_STAR_DIV_MOD_OP s)
              | _ -> None
    Some([| for _c in angles do yield GREATER |], afterOp)

[<Struct>]
type PositionWithColumn =
    val Position: Position
    val Column: int
    new (position: Position, column: int) = { Position = position; Column = column }

//----------------------------------------------------------------------------
// build a LexFilter
//--------------------------------------------------------------------------*)
type LexFilterImpl (lightStatus: LightSyntaxStatus, compilingFsLib, lexer, lexbuf: UnicodeLexing.Lexbuf) = 

    //----------------------------------------------------------------------------
    // Part I. Building a new lex stream from an old
    //
    // A lexbuf is a stateful object that can be enticed to emit tokens by calling
    // 'lexer' functions designed to work with the lexbuf. Here we fake a new stream
    // coming out of an existing lexbuf. Ideally lexbufs would be abstract interfaces
    // and we could just build a new abstract interface that wraps an existing one.
    // However that is not how F# lexbufs currently work.
    // 
    // Part of the fakery we perform involves buffering a lookahead token which 
    // we eventually pass on to the client. However, this client also looks at
    // other aspects of the 'state' of lexbuf directly, e.g. F# lexbufs have a triple
    //    (start-pos, end-pos, eof-reached)
    //
    // You may ask why the F# parser reads this lexbuf state directly. Well, the
    // pars.fsy code itself it doesn't, but the parser engines (prim-parsing.fs) 
    // certainly do for F#. e.g. when these parsers read a token 
    // from the lexstream they also read the position information and keep this
    // a related stack. 
    //
    // Anyway, this explains the functions getLexbufState(), setLexbufState() etc.
    //--------------------------------------------------------------------------

    // Make sure we don't report 'eof' when inserting a token, and set the positions to the 
    // last reported token position 
    let lexbufStateForInsertedDummyTokens (lastTokenStartPos, lastTokenEndPos) =
        new LexbufState(lastTokenStartPos, lastTokenEndPos, false) 

    let getLexbufState() = 
        new LexbufState(lexbuf.StartPos, lexbuf.EndPos, lexbuf.IsPastEndOfStream)  

    let setLexbufState (p: LexbufState) =
        lexbuf.StartPos <- p.StartPos  
        lexbuf.EndPos <- p.EndPos
        lexbuf.IsPastEndOfStream <- p.PastEOF

    let startPosOfTokenTup (tokenTup: TokenTup) = 
        match tokenTup.Token with
        // EOF token is processed as if on column -1 
        // This forces the closure of all contexts. 
        | Parser.EOF _ -> tokenTup.LexbufState.StartPos.ColumnMinusOne
        | _ -> tokenTup.LexbufState.StartPos

    let endPosOfTokenTup (tokenTup: TokenTup) = 
        match tokenTup.Token with
        // EOF token is processed as if on column -1 
        // This forces the closure of all contexts. 
        | Parser.EOF _ -> tokenTup.LexbufState.StartPos.ColumnMinusOne
        | _ -> tokenTup.LexbufState.EndPos 

    //----------------------------------------------------------------------------
    // TokenTup pool
    //--------------------------------------------------------------------------

    let pool = TokenTupPool()

    //----------------------------------------------------------------------------
    // Part II. The state of the new lex stream object.
    //--------------------------------------------------------------------------

    // Ok, we're going to the wrapped lexbuf. Set the lexstate back so that the lexbuf 
    // appears consistent and correct for the wrapped lexer function. 
    let mutable savedLexbufState = Unchecked.defaultof<LexbufState>
    let mutable haveLexbufState = false
    let runWrappedLexerInConsistentLexbufState() =
        let state = if haveLexbufState then savedLexbufState else getLexbufState()
        setLexbufState state
        let lastTokenEnd = state.EndPos
        let token = lexer lexbuf
        // Now we've got the token, remember the lexbuf state, associating it with the token 
        // and remembering it as the last observed lexbuf state for the wrapped lexer function. 
        let tokenLexbufState = getLexbufState()
        savedLexbufState <- tokenLexbufState
        haveLexbufState <- true

        let tokenTup = pool.Rent()
        tokenTup.Token <- token
        tokenTup.LexbufState <- tokenLexbufState
        tokenTup.LastTokenPos <- lastTokenEnd
        tokenTup

    //----------------------------------------------------------------------------
    // Fetch a raw token, either from the old lexer or from our delayedStack
    //--------------------------------------------------------------------------

    let delayedStack = System.Collections.Generic.Stack<TokenTup>()
    let mutable tokensThatNeedNoProcessingCount = 0

    let delayToken tokenTup = delayedStack.Push tokenTup 
    let delayTokenNoProcessing tokenTup = delayToken tokenTup; tokensThatNeedNoProcessingCount <- tokensThatNeedNoProcessingCount + 1

    let popNextTokenTup() = 
        if delayedStack.Count > 0 then 
            let tokenTup = delayedStack.Pop()
            if debug then dprintf "popNextTokenTup: delayed token, tokenStartPos = %a\n" outputPos (startPosOfTokenTup tokenTup)
            tokenTup
        else
            if debug then dprintf "popNextTokenTup: no delayed tokens, running lexer...\n"
            runWrappedLexerInConsistentLexbufState() 
    

    //----------------------------------------------------------------------------
    // Part III. Initial configuration of state.
    //
    // We read a token. In F# Interactive the parser thread will be correctly blocking
    // here.
    //--------------------------------------------------------------------------

    let mutable initialized = false
    let mutable offsideStack = []
    let mutable prevWasAtomicEnd = false
    
    let peekInitial() =
        let initialLookaheadTokenTup = popNextTokenTup()
        if debug then dprintf "first token: initialLookaheadTokenLexbufState = %a\n" outputPos (startPosOfTokenTup initialLookaheadTokenTup)
        
        delayToken initialLookaheadTokenTup
        initialized <- true
        offsideStack <- (CtxtSeqBlock(FirstInSeqBlock, startPosOfTokenTup initialLookaheadTokenTup, NoAddBlockEnd)) :: offsideStack
        initialLookaheadTokenTup 

    let warn (s: TokenTup) msg = 
        warning(IndentationProblem(msg, mkSynRange (startPosOfTokenTup s) s.LexbufState.EndPos))

    // 'query { join x in ys ... }'
    // 'query { ... 
    //          join x in ys ... }'
    // 'query { for ... do
    //          join x in ys ... }'
    let detectJoinInCtxt stack =
        let rec check s = 
               match s with 
               | CtxtParen(LBRACE _, _) :: _ -> true
               | (CtxtSeqBlock _ | CtxtDo _ | CtxtFor _) :: rest -> check rest
               | _ -> false
        match stack with 
        | (CtxtVanilla _ :: rest) -> check rest
        | _ -> false

    //----------------------------------------------------------------------------
    // Part IV. Helper functions for pushing contexts and giving good warnings
    // if a context is undented.  
    //
    // Undentation rules
    //--------------------------------------------------------------------------

    let pushCtxt tokenTup (newCtxt: Context) =
        let rec undentationLimit strict stack = 
            match newCtxt, stack with 
            | _, [] -> PositionWithColumn(newCtxt.StartPos, -1) 

            // ignore Vanilla because a SeqBlock is always coming 
            | _, (CtxtVanilla _ :: rest) -> undentationLimit strict rest

            | _, (CtxtSeqBlock _ :: rest) when not strict -> undentationLimit strict rest
            | _, (CtxtParen _ :: rest) when not strict -> undentationLimit strict rest

            // 'begin match' limited by minimum of two  
            // '(match' limited by minimum of two  
            | _, (((CtxtMatch _) as ctxt1) :: CtxtSeqBlock _ :: (CtxtParen ((BEGIN | LPAREN), _) as ctxt2) :: _rest)
                      -> if ctxt1.StartCol <= ctxt2.StartCol 
                         then PositionWithColumn(ctxt1.StartPos, ctxt1.StartCol) 
                         else PositionWithColumn(ctxt2.StartPos, ctxt2.StartCol) 

             // 'let ... = function' limited by 'let', precisely  
             // This covers the common form 
             //                          
             //     let f x = function   
             //     | Case1 -> ...       
             //     | Case2 -> ...       
            | (CtxtMatchClauses _), (CtxtFunction _ :: CtxtSeqBlock _ :: (CtxtLetDecl _ as limitCtxt) :: _rest)
                      -> PositionWithColumn(limitCtxt.StartPos, limitCtxt.StartCol)

            // Otherwise 'function ...' places no limit until we hit a CtxtLetDecl etc... (Recursive) 
            | (CtxtMatchClauses _), (CtxtFunction _ :: rest)
                      -> undentationLimit false rest

            // 'try ... with' limited by 'try'  
            | _, (CtxtMatchClauses _ :: (CtxtTry _ as limitCtxt) :: _rest)
                      -> PositionWithColumn(limitCtxt.StartPos, limitCtxt.StartCol)

            // 'fun ->' places no limit until we hit a CtxtLetDecl etc... (Recursive) 
            | _, (CtxtFun _ :: rest)
                      -> undentationLimit false rest

            // 'f ...{' places no limit until we hit a CtxtLetDecl etc... 
            // 'f ...[' places no limit until we hit a CtxtLetDecl etc... 
            // 'f ...[|' places no limit until we hit a CtxtLetDecl etc... 
            | _, (CtxtParen ((LBRACE _ | LBRACK | LBRACK_BAR), _) :: CtxtSeqBlock _ :: rest)
            | _, (CtxtParen ((LBRACE _ | LBRACK | LBRACK_BAR), _) :: CtxtVanilla _ :: CtxtSeqBlock _ :: rest)
            | _, (CtxtSeqBlock _ :: CtxtParen((LBRACE _ | LBRACK | LBRACK_BAR), _) :: CtxtVanilla _ :: CtxtSeqBlock _ :: rest)
                      -> undentationLimit false rest

            // MAJOR PERMITTED UNDENTATION This is allowing:
            //   if x then y else
            //   let x = 3 + 4
            //   x + x  
            // This is a serious thing to allow, but is required since there is no "return" in this language.
            // Without it there is no way of escaping special cases in large bits of code without indenting the main case.
            | CtxtSeqBlock _, (CtxtElse _ :: (CtxtIf _ as limitCtxt) :: _rest) 
                      -> PositionWithColumn(limitCtxt.StartPos, limitCtxt.StartCol)

            // Permitted inner-construct precise block alignment: 
            //           interface ...
            //           with ... 
            //           end 
            //           
            //           type ...
            //           with ... 
            //           end 
            | CtxtWithAsAugment _, ((CtxtInterfaceHead _ | CtxtMemberHead _ | CtxtException _ | CtxtTypeDefns _) as limitCtxt :: _rest)
                      -> PositionWithColumn(limitCtxt.StartPos, limitCtxt.StartCol) 

            // Permit undentation via parentheses (or begin/end) following a 'then', 'else' or 'do':
            //        if nr > 0 then (  
            //              nr <- nr - 1
            //              acc <- d
            //              i <- i - 1
            //        ) else (
            //              i <- -1
            //        )

            // PERMITTED UNDENTATION: Inner construct (then, with, else, do) that dangle, places no limit until we hit the corresponding leading construct CtxtIf, CtxtFor, CtxtWhile, CtxtVanilla etc... *)
            //    e.g.   if ... then ...
            //              expr
            //           else
            //              expr
            //    rather than forcing 
            //           if ... 
            //           then expr
            //           else expr
            //   Also  ...... with
            //           ...           <-- this is before the "with"
            //         end

            | _, ((CtxtWithAsAugment _ | CtxtThen _ | CtxtElse _ | CtxtDo _ ) :: rest)
                      -> undentationLimit false rest


            // '... (function ->' places no limit until we hit a CtxtLetDecl etc....  (Recursive)
            //
            //   e.g.
            //        let fffffff() = function
            //          | [] -> 0
            //          | _ -> 1 
            //
            //   Note this does not allow
            //        let fffffff() = function _ ->
            //           0
            //   which is not a permitted undentation. This undentation would make no sense if there are multiple clauses in the 'function', which is, after all, what 'function' is really for
            //        let fffffff() = function 1 ->
            //           0
            //          | 2 -> ...       <---- not allowed
            | _, (CtxtFunction _ :: rest)
                      -> undentationLimit false rest

            // 'module ... : sig'    limited by 'module' 
            // 'module ... : struct' limited by 'module' 
            // 'module ... : begin'  limited by 'module' 
            // 'if ... then ('       limited by 'if' 
            // 'if ... then {'       limited by 'if' 
            // 'if ... then ['       limited by 'if' 
            // 'if ... then [|'       limited by 'if' 
            // 'if ... else ('       limited by 'if' 
            // 'if ... else {'       limited by 'if' 
            // 'if ... else ['       limited by 'if' 
            // 'if ... else [|'       limited by 'if' 
            | _, (CtxtParen ((SIG | STRUCT | BEGIN), _) :: CtxtSeqBlock _ :: (CtxtModuleBody (_, false) as limitCtxt) :: _)
            | _, (CtxtParen ((BEGIN | LPAREN | LBRACK | LBRACE _ | LBRACE_BAR | LBRACK_BAR), _) :: CtxtSeqBlock _ :: CtxtThen _ :: (CtxtIf _ as limitCtxt) :: _)
            | _, (CtxtParen ((BEGIN | LPAREN | LBRACK | LBRACE _ | LBRACE_BAR | LBRACK_BAR | LBRACK_LESS), _) :: CtxtSeqBlock _ :: CtxtElse _ :: (CtxtIf _ as limitCtxt) :: _)

            // 'f ... ('  in seqblock     limited by 'f' 
            // 'f ... {'  in seqblock     limited by 'f'  NOTE: this is covered by the more generous case above 
            // 'f ... ['  in seqblock     limited by 'f' 
            // 'f ... [|' in seqblock      limited by 'f' 
            // 'f ... Foo<' in seqblock      limited by 'f' 
            | _, (CtxtParen ((BEGIN | LPAREN | LESS true | LBRACK | LBRACK_BAR), _) :: CtxtVanilla _ :: (CtxtSeqBlock _ as limitCtxt) :: _)

            // 'type C = class ... '       limited by 'type' 
            // 'type C = interface ... '       limited by 'type' 
            // 'type C = struct ... '       limited by 'type' 
            | _, (CtxtParen ((CLASS | STRUCT | INTERFACE), _) :: CtxtSeqBlock _ :: (CtxtTypeDefns _ as limitCtxt) ::  _)
                -> PositionWithColumn(limitCtxt.StartPos, limitCtxt.StartCol + 1) 

            // 'type C(' limited by 'type'
            | _, (CtxtSeqBlock _ :: CtxtParen(LPAREN, _) :: (CtxtTypeDefns _ as limitCtxt) :: _ )
            // 'static member C(' limited by 'static', likewise others
            | _, (CtxtSeqBlock _ :: CtxtParen(LPAREN, _) :: (CtxtMemberHead _ as limitCtxt) :: _ )
            // 'static member P with get() = ' limited by 'static', likewise others
            | _, (CtxtWithAsLet _ :: (CtxtMemberHead _ as limitCtxt) :: _ )
                 when lexbuf.SupportsFeature LanguageFeature.RelaxWhitespace
                 -> PositionWithColumn(limitCtxt.StartPos, limitCtxt.StartCol + 1) 

            // REVIEW: document these 
            | _, (CtxtSeqBlock _ :: CtxtParen((BEGIN | LPAREN | LBRACK | LBRACK_BAR), _) :: CtxtVanilla _ :: (CtxtSeqBlock _ as limitCtxt) :: _)
            | (CtxtSeqBlock _), (CtxtParen ((BEGIN | LPAREN | LBRACE _ | LBRACE_BAR | LBRACK | LBRACK_BAR), _) :: CtxtSeqBlock _ :: ((CtxtTypeDefns _ | CtxtLetDecl _ | CtxtMemberBody _ | CtxtWithAsLet _) as limitCtxt) :: _)
                      -> PositionWithColumn(limitCtxt.StartPos, limitCtxt.StartCol + 1) 

            // Permitted inner-construct (e.g. "then" block and "else" block in overall 
            // "if-then-else" block ) block alignment: 
            //           if ... 
            //           then expr
            //           elif expr  
            //           else expr  
            | (CtxtIf _ | CtxtElse _ | CtxtThen _), (CtxtIf _ as limitCtxt) :: _rest  
                      -> PositionWithColumn(limitCtxt.StartPos, limitCtxt.StartCol)

            // Permitted inner-construct precise block alignment: 
            //           while  ... 
            //           do expr
            //           done   
            | (CtxtDo _), ((CtxtFor _ | CtxtWhile _) as limitCtxt) :: _rest  
                      -> PositionWithColumn(limitCtxt.StartPos, limitCtxt.StartCol)


            // These contexts all require indentation by at least one space 
            | _, ((CtxtInterfaceHead _ | CtxtNamespaceHead _ | CtxtModuleHead _ | CtxtException _ | CtxtModuleBody (_, false) | CtxtIf _ | CtxtWithAsLet _ | CtxtLetDecl _ | CtxtMemberHead _ | CtxtMemberBody _) as limitCtxt :: _) 
                      -> PositionWithColumn(limitCtxt.StartPos, limitCtxt.StartCol + 1) 

            // These contexts can have their contents exactly aligning 
            | _, ((CtxtParen _ | CtxtFor _ | CtxtWhen _ | CtxtWhile _ | CtxtTypeDefns _ | CtxtMatch _ | CtxtModuleBody (_, true) | CtxtNamespaceBody _ | CtxtTry _ | CtxtMatchClauses _ | CtxtSeqBlock _) as limitCtxt :: _)
                      -> PositionWithColumn(limitCtxt.StartPos, limitCtxt.StartCol) 
       
        match newCtxt with 
        // Don't bother to check pushes of Vanilla blocks since we've 
        // always already pushed a SeqBlock at this position.
        | CtxtVanilla _ 
        // String interpolation inner expressions are not limited (e.g. multiline strings)
        | CtxtParen((INTERP_STRING_BEGIN_PART _ | INTERP_STRING_PART _),_) -> ()
        | _ -> 
            let p1 = undentationLimit true offsideStack
            let c2 = newCtxt.StartCol
            if c2 < p1.Column then 
                warn tokenTup 
                    (if debug then 
                        sprintf "possible incorrect indentation: this token is offside of context at position %s, newCtxt = %A, stack = %A, newCtxtPos = %s, c1 = %d, c2 = %d" 
                            (warningStringOfPosition p1.Position) newCtxt offsideStack (stringOfPos (newCtxt.StartPos)) p1.Column c2 
                     else
                        FSComp.SR.lexfltTokenIsOffsideOfContextStartedEarlier(warningStringOfPosition p1.Position))
        let newOffsideStack = newCtxt :: offsideStack
        if debug then dprintf "--> pushing, stack = %A\n" newOffsideStack
        offsideStack <- newOffsideStack

    let popCtxt() = 
        match offsideStack with 
        | [] -> ()
        | h :: rest -> 
             if debug then dprintf "<-- popping Context(%A), stack = %A\n" h rest
             offsideStack <- rest

    let replaceCtxt p ctxt = popCtxt(); pushCtxt p ctxt

    //----------------------------------------------------------------------------
    // Peek ahead at a token, either from the old lexer or from our delayedStack
    //--------------------------------------------------------------------------

    let peekNextTokenTup() = 
        let tokenTup = popNextTokenTup()
        delayToken tokenTup
        tokenTup
    
    let peekNextToken() = 
        peekNextTokenTup().Token
    
     //----------------------------------------------------------------------------
     // Adjacency precedence rule
     //--------------------------------------------------------------------------

    let isAdjacent (leftTokenTup: TokenTup) rightTokenTup =
        let lparenStartPos = startPosOfTokenTup rightTokenTup
        let tokenEndPos = leftTokenTup.LexbufState.EndPos
        (tokenEndPos = lparenStartPos)
    
    let nextTokenIsAdjacentLParenOrLBrack (tokenTup: TokenTup) =
        let lookaheadTokenTup = peekNextTokenTup()
        match lookaheadTokenTup.Token with 
        | (LPAREN | LBRACK) -> 
            if isAdjacent tokenTup lookaheadTokenTup then Some(lookaheadTokenTup.Token) else None
        | _ -> 
            None

    let nextTokenIsAdjacent firstTokenTup =
        let lookaheadTokenTup = peekNextTokenTup()
        isAdjacent firstTokenTup lookaheadTokenTup

    let peekAdjacentTypars indentation (tokenTup: TokenTup) =
        let lookaheadTokenTup = peekNextTokenTup()
        match lookaheadTokenTup.Token with 
        | INFIX_COMPARE_OP "</" | LESS _ -> 
            let tokenEndPos = tokenTup.LexbufState.EndPos 
            if isAdjacent tokenTup lookaheadTokenTup then 
                let mutable stack = []
                let rec scanAhead nParen = 
                    let lookaheadTokenTup = popNextTokenTup()
                    let lookaheadToken = lookaheadTokenTup.Token
                    stack <- (lookaheadTokenTup, true) :: stack
                    let lookaheadTokenStartPos = startPosOfTokenTup lookaheadTokenTup
                    match lookaheadToken with 
                    | Parser.EOF _ | SEMICOLON_SEMICOLON -> false 
                    | _ when indentation && lookaheadTokenStartPos < tokenEndPos -> false
                    | (RPAREN | RBRACK) ->
                        let nParen = nParen - 1
                        if nParen > 0 then 
                            scanAhead nParen 
                        else 
                            false
                    | GREATER _ | GREATER_RBRACK | GREATER_BAR_RBRACK -> 
                        let nParen = nParen - 1
                        let hasAfterOp = (match lookaheadToken with GREATER _ -> false | _ -> true)
                        if nParen > 0 then 
                            // Don't smash the token if there is an after op and we're in a nested paren
                            stack <- (lookaheadTokenTup, not hasAfterOp) :: stack.Tail
                            scanAhead nParen 
                        else 
                            // On successful parse of a set of type parameters, look for an adjacent (, e.g. 
                            //    M<int>(args)
                            // and insert a HIGH_PRECEDENCE_PAREN_APP
                            if not hasAfterOp && (match nextTokenIsAdjacentLParenOrLBrack lookaheadTokenTup with Some LPAREN -> true | _ -> false) then
                                let dotTokenTup = peekNextTokenTup()
                                stack <- (pool.UseLocation(dotTokenTup, HIGH_PRECEDENCE_PAREN_APP), false) :: stack
                            true
                    | INFIX_COMPARE_OP (TyparsCloseOp(greaters, afterOp)) -> 
                        let nParen = nParen - greaters.Length
                        if nParen > 0 then 
                            // Don't smash the token if there is an after op and we're in a nested paren
                            stack <- (lookaheadTokenTup, not afterOp.IsSome) :: stack.Tail
                            scanAhead nParen 
                        else 
                            // On successful parse of a set of type parameters, look for an adjacent (, e.g. 
                            //    M<C<int>>(args)
                            // and insert a HIGH_PRECEDENCE_PAREN_APP
                            if afterOp.IsNone && (match nextTokenIsAdjacentLParenOrLBrack lookaheadTokenTup with Some LPAREN -> true | _ -> false) then
                                let dotTokenTup = peekNextTokenTup()
                                stack <- (pool.UseLocation(dotTokenTup, HIGH_PRECEDENCE_PAREN_APP), false) :: stack
                            true
                    | (LPAREN | LESS _ | LBRACK | LBRACK_LESS | INFIX_COMPARE_OP "</") -> 
                        scanAhead (nParen+1)
                        
                    // These tokens CAN occur in non-parenthesized positions in the grammar of types or type parameter definitions 
                    // Thus we explicitly DO consider these to be type applications:
                    //      f<int>x
                    //      f<global.int>x
                    //      f<x * x>x
                    //      f<x , x>x
                    //      f<x ^ x>x
                    //      f<x ^- x>x
                    //      f<x / x>x
                    //      f<x -> x>x
                    //      f<{| C : int |}>x
                    //      f<x # x>x
                    //      f<x ' x>x
                    | DEFAULT | COLON | COLON_GREATER | STRUCT | NULL | DELEGATE | AND | WHEN 
                    | DOT_DOT
                    | NEW
                    | LBRACE_BAR
                    | SEMICOLON
                    | BAR_RBRACE
                    | INFIX_AT_HAT_OP "^" 
                    | INFIX_AT_HAT_OP "^-" 
                    | INFIX_STAR_DIV_MOD_OP "/" 
                    | MINUS 
                    | GLOBAL 
                    | CONST
                    | KEYWORD_STRING _
                    | NULL
                    | INT8 _ | INT16 _ | INT32 _ | INT64 _ | NATIVEINT _ 
                    | UINT8 _ | UINT16 _ | UINT32 _ | UINT64 _ | UNATIVEINT _
                    | DECIMAL _ | BIGNUM _ | STRING _ | BYTEARRAY _ | CHAR _ | TRUE | FALSE 
                    | IEEE32 _ | IEEE64 _ 
                    | DOT | UNDERSCORE | EQUALS
                    | IDENT _ | COMMA | RARROW | HASH 
                    | STAR | QUOTE -> 
                        scanAhead nParen
 
 
                    // All other tokens ARE assumed to be part of the grammar of types or type parameter definitions 
                    | _ -> 
                        if nParen > 1 then 
                            scanAhead nParen 
                        else 
                            false
 
                let res = scanAhead 0
                // Put the tokens back on and smash them up if needed
                stack |> List.iter (fun (tokenTup, smash) ->
                    if smash then 
                        match tokenTup.Token with 
                        | INFIX_COMPARE_OP "</" ->
                            delayToken (pool.UseShiftedLocation(tokenTup, INFIX_STAR_DIV_MOD_OP "/", 1, 0))
                            delayToken (pool.UseShiftedLocation(tokenTup, LESS res, 0, -1))
                            pool.Return tokenTup
                        | GREATER_BAR_RBRACK -> 
                            delayToken (pool.UseShiftedLocation(tokenTup, BAR_RBRACK, 1, 0))
                            delayToken (pool.UseShiftedLocation(tokenTup, GREATER res, 0, -2))
                            pool.Return tokenTup
                        | GREATER_RBRACK ->
                            delayToken (pool.UseShiftedLocation(tokenTup, RBRACK, 1, 0))
                            delayToken (pool.UseShiftedLocation(tokenTup, GREATER res, 0, -1))
                            pool.Return tokenTup
                        | GREATER _ ->
                            delayToken (pool.UseLocation(tokenTup, GREATER res))
                            pool.Return tokenTup
                        | (INFIX_COMPARE_OP (TyparsCloseOp(greaters, afterOp) as opstr)) -> 
                            match afterOp with
                            | None -> ()
                            | Some tok -> delayToken (pool.UseShiftedLocation(tokenTup, tok, greaters.Length, 0))
                            for i = greaters.Length - 1 downto 0 do
                                delayToken (pool.UseShiftedLocation(tokenTup, greaters.[i] res, i, -opstr.Length + i + 1))
                            pool.Return tokenTup
                        | _ -> delayToken tokenTup
                    else
                        delayToken tokenTup)
                res
            else 
                false
        | _ -> false

    //----------------------------------------------------------------------------
    // End actions
    //--------------------------------------------------------------------------

    let returnToken (tokenLexbufState: LexbufState) tok = 
        setLexbufState tokenLexbufState
        prevWasAtomicEnd <- isAtomicExprEndToken tok
        tok
    
    let rec suffixExists p l = match l with [] -> false | _ :: t -> p t || suffixExists p t

    let tokenBalancesHeadContext token stack = 
        match token, stack with 
        | END, (CtxtWithAsAugment(_) :: _)
        | (ELSE | ELIF), (CtxtIf _ :: _)
        | DONE, (CtxtDo _ :: _)
        // WITH balances except in the following contexts.... Phew - an overused keyword! 
        | WITH, ( ((CtxtMatch _ | CtxtException _ | CtxtMemberHead _ | CtxtInterfaceHead _ | CtxtTry _ | CtxtTypeDefns _ | CtxtMemberBody _) :: _)
                                // This is the nasty record/object-expression case 
                                | (CtxtSeqBlock _ :: CtxtParen((LBRACE _ | LBRACE_BAR), _) :: _) )
        | FINALLY, (CtxtTry _ :: _) -> 
            true

        // for x in ienum ... 
        // let x = ... in
        | IN, ((CtxtFor _ | CtxtLetDecl _) :: _) ->
            true
        // 'query { join x in ys ... }'
        // 'query { ... 
        //          join x in ys ... }'
        // 'query { for ... do
        //          join x in ys ... }'
        | IN, stack when detectJoinInCtxt stack ->
            true

        // NOTE: ;; does not terminate a 'namespace' body. 
        | SEMICOLON_SEMICOLON, (CtxtSeqBlock _ :: CtxtNamespaceBody _ :: _) -> 
            true

        | SEMICOLON_SEMICOLON, (CtxtSeqBlock _ :: CtxtModuleBody (_, true) :: _) -> 
            true

        | t2, (CtxtParen(t1, _) :: _) -> 
            parenTokensBalance t1 t2

        | _ -> 
            false
              
    //----------------------------------------------------------------------------
    // Parse and transform the stream of tokens coming from popNextTokenTup, pushing
    // contexts where needed, popping them where things are offside, balancing
    // parentheses and other constructs.
    //--------------------------------------------------------------------------

              
    let rec hwTokenFetch (useBlockRule) =
        let tokenTup = popNextTokenTup()
        let tokenReplaced = rulesForBothSoftWhiteAndHardWhite tokenTup
        if tokenReplaced then hwTokenFetch useBlockRule else 

        let tokenStartPos = (startPosOfTokenTup tokenTup)
        let token = tokenTup.Token
        let tokenLexbufState = tokenTup.LexbufState
        let tokenStartCol = tokenStartPos.Column

        let isSameLine() = 
            match token with 
            | Parser.EOF _ -> false
            | _ -> (startPosOfTokenTup (peekNextTokenTup())).OriginalLine = tokenStartPos.OriginalLine

        let isControlFlowOrNotSameLine() = 
            match token with 
            | Parser.EOF _ -> false
            | _ -> 
                not (isSameLine()) ||  
                (match peekNextToken() with TRY | MATCH | MATCH_BANG | IF | LET _ | FOR | WHILE -> true | _ -> false) 

        // Look for '=' or '.Id.id.id = ' after an identifier
        let rec isLongIdentEquals token = 
            match token with 
            | Parser.GLOBAL
            | Parser.IDENT _ -> 
                let rec loop() = 
                    let tokenTup = popNextTokenTup()
                    let res = 
                        match tokenTup.Token with 
                        | Parser.EOF _ -> false
                        | DOT -> 
                            let tokenTup = popNextTokenTup()
                            let res = 
                                match tokenTup.Token with 
                                | Parser.EOF _ -> false
                                | IDENT _ -> loop()
                                | _ -> false
                            delayToken tokenTup
                            res
                        | EQUALS -> 
                            true 
                        | _ -> false
                    delayToken tokenTup
                    res
                loop()
            | _ -> false

        let reprocess() = 
            delayToken tokenTup
            hwTokenFetch useBlockRule

        let reprocessWithoutBlockRule() = 
            delayToken tokenTup
            hwTokenFetch false
            
        let insertTokenFromPrevPosToCurrentPos tok = 
            delayToken tokenTup
            if debug then dprintf "inserting %+A\n" tok
            // span of inserted token lasts from the col + 1 of the prev token 
            // to the beginning of current token
            let lastTokenPos = 
                let pos = tokenTup.LastTokenPos
                pos.ShiftColumnBy 1
            returnToken (lexbufStateForInsertedDummyTokens (lastTokenPos, tokenTup.LexbufState.StartPos)) tok

        let insertToken tok = 
            delayToken tokenTup
            if debug then dprintf "inserting %+A\n" tok
            returnToken (lexbufStateForInsertedDummyTokens (startPosOfTokenTup tokenTup, tokenTup.LexbufState.EndPos)) tok

        let isSemiSemi = match token with SEMICOLON_SEMICOLON -> true | _ -> false

        // If you see a 'member' keyword while you are inside the body of another member, then it usually means there is a syntax error inside this method
        // and the upcoming 'member' is the start of the next member in the class. For better parser recovery and diagnostics, it is best to pop out of the 
        // existing member context so the parser can recover.
        //
        // However there are two places where 'member' keywords can appear inside expressions inside the body of a member. The first is object expressions, and
        // the second is static inline constraints. We must not pop the context stack in those cases, or else legal code will not parse.
        //
        // It is impossible to decide for sure if we're in one of those two cases, so we must err conservatively if we might be.
        let thereIsACtxtMemberBodyOnTheStackAndWeShouldPopStackForUpcomingMember ctxtStack = 
            // a 'member' starter keyword is coming; should we pop?
            if not(List.exists (function CtxtMemberBody _ -> true | _ -> false) ctxtStack) then
                false // no member currently on the stack, nothing to pop
            else
                // there is a member context
                if List.exists (function CtxtParen(LBRACE _, _) -> true | _ -> false) ctxtStack then
                    false  // an LBRACE could mean an object expression, and object expressions can have 'member' tokens in them, so do not pop, to be safe
                elif List.count (function CtxtParen(LPAREN, _) -> true | _ -> false) ctxtStack >= 2 then
                    false  // static member constraints always are embedded in at least two LPARENS, so do not pop, to be safe
                else
                    true

        let endTokenForACtxt ctxt =
            match ctxt with 
            | CtxtFun _
            | CtxtMatchClauses _ 
            | CtxtWithAsLet _ -> 
                Some OEND

            | CtxtWithAsAugment _ -> 
                Some ODECLEND
                 
            | CtxtDo _        
            | CtxtLetDecl (true, _) -> 
                Some ODECLEND
                             
            | CtxtSeqBlock(_, _, AddBlockEnd) ->  
                Some OBLOCKEND 

            | CtxtSeqBlock(_, _, AddOneSidedBlockEnd) ->  
                Some ORIGHT_BLOCK_END 

            | _ -> 
                None

        // Balancing rule. Every 'in' terminates all surrounding blocks up to a CtxtLetDecl, and will be swallowed by 
        // terminating the corresponding CtxtLetDecl in the rule below. 
        // Balancing rule. Every 'done' terminates all surrounding blocks up to a CtxtDo, and will be swallowed by 
        // terminating the corresponding CtxtDo in the rule below. 
        let tokenForcesHeadContextClosure token stack = 
            not (isNil stack) &&
            match token with 
            | Parser.EOF _ -> true
            | SEMICOLON_SEMICOLON -> not (tokenBalancesHeadContext token stack) 
            | END 
            | ELSE 
            | ELIF 
            | DONE 
            | IN 
            | RPAREN
            | GREATER true 
            | RBRACE _
            | BAR_RBRACE 
            | RBRACK 
            | BAR_RBRACK 
            | WITH 
            | FINALLY 
            | INTERP_STRING_PART _
            | INTERP_STRING_END _
            | RQUOTE _ ->
                not (tokenBalancesHeadContext token stack) && 
                // Only close the context if some context is going to match at some point in the stack.
                // If none match, the token will go through, and error recovery will kick in in the parser and report the extra token,
                // and then parsing will continue. Closing all the contexts will not achieve much except aid in a catastrophic failure.
                (stack |> suffixExists (tokenBalancesHeadContext token))

            | _ -> false

        // The TYPE and MODULE keywords cannot be used in expressions, but the parser has a hard time recovering on incomplete-expression-code followed by
        // a TYPE or MODULE. So the lexfilter helps out by looking ahead for these tokens and (1) closing expression contexts and (2) inserting extra 'coming soon' tokens
        // that the expression rules in the FsYacc parser can 'shift' to make progress parsing the incomplete expressions, without using the 'recover' action.
        let insertComingSoonTokens(keywordName, comingSoon, isHere) =
            // compiling the source for FSharp.Core.dll uses unconventional syntax like
            //     (# "unbox.any !0" type ('T) x : 'T #)
            // where the type keyword is used inside an expression, so we must exempt FSharp.Core from some extra failed-parse-diagnostics-recovery-processing of the 'type' keyword
            let mutable effectsToDo = []
            if not compilingFsLib then
                // ... <<< code with unmatched ( or [ or { or [| >>> ... "type" ...
                // We want a TYPE or MODULE keyword to close any currently-open "expression" contexts, as though there were close delimiters in the file, so:
                let rec nextOuterMostInterestingContextIsNamespaceOrModule offsideStack =
                    match offsideStack with
                    // next outermost is namespace or module
                    | _ :: (CtxtNamespaceBody _ | CtxtModuleBody _) :: _ -> true
                    // The context pair below is created a namespace/module scope when user explicitly uses 'begin'...'end',
                    // and these can legally contain type definitions, so ignore this combo as uninteresting and recurse deeper
                    | _ :: CtxtParen((BEGIN|STRUCT), _) :: CtxtSeqBlock(_, _, _) :: _ -> nextOuterMostInterestingContextIsNamespaceOrModule(offsideStack.Tail.Tail) 
                    // at the top of the stack there is an implicit module
                    | _ :: [] -> true 
                    // anything else is a non-namespace/module
                    | _ -> false
                while not offsideStack.IsEmpty && (not(nextOuterMostInterestingContextIsNamespaceOrModule offsideStack)) &&
                                                    (match offsideStack.Head with 
                                                    // open-parens of sorts
                                                    | CtxtParen((LPAREN|LBRACK|LBRACE _ |LBRACE_BAR|LBRACK_BAR), _) -> true
                                                    // seq blocks
                                                    | CtxtSeqBlock _ -> true 
                                                    // vanillas
                                                    | CtxtVanilla _ -> true 
                                                    // preserve all other contexts
                                                    | _ -> false) do
                    match offsideStack.Head with
                    | CtxtParen _ ->
                        if debug then dprintf "%s at %a terminates CtxtParen()\n" keywordName outputPos tokenStartPos
                        popCtxt()
                    | CtxtSeqBlock(_, _, AddBlockEnd) ->  
                        popCtxt()
                        effectsToDo <- (fun() -> 
                            if debug then dprintf "--> because %s is coming, inserting OBLOCKEND\n" keywordName
                            delayTokenNoProcessing (pool.UseLocation(tokenTup, OBLOCKEND))) :: effectsToDo
                    | CtxtSeqBlock(_, _, NoAddBlockEnd) ->  
                        if debug then dprintf "--> because %s is coming, popping CtxtSeqBlock\n" keywordName
                        popCtxt()
                    | CtxtSeqBlock(_, _, AddOneSidedBlockEnd) ->  
                        popCtxt()
                        effectsToDo <- (fun() -> 
                            if debug then dprintf "--> because %s is coming, inserting ORIGHT_BLOCK_END\n" keywordName
                            delayTokenNoProcessing (pool.UseLocation(tokenTup, ORIGHT_BLOCK_END))) :: effectsToDo
                    | CtxtVanilla _ ->  
                        if debug then dprintf "--> because %s is coming, popping CtxtVanilla\n" keywordName
                        popCtxt()
                    | _ -> failwith "impossible, the while loop guard just above prevents this"
            // See bugs 91609/92107/245850; we turn ...TYPE... into ...TYPE_COMING_SOON x6, TYPE_IS_HERE... to help the parser recover when it sees "type" in a parenthesized expression.
            // And we do the same thing for MODULE.
            // Why _six_ TYPE_COMING_SOON? It's rather arbitrary, this means we can recover from up to six unmatched parens before failing. The unit tests (with 91609 in the name) demonstrate this.
            // Don't "delayToken tokenTup", we are replacing it, so consume it.
            if debug then dprintf "inserting 6 copies of %+A before %+A\n" comingSoon isHere
            delayTokenNoProcessing (pool.UseLocation(tokenTup, isHere))
            for i in 1..6 do
                delayTokenNoProcessing (pool.UseLocation(tokenTup, comingSoon))
            for e in List.rev effectsToDo do
                e() // push any END tokens after pushing the TYPE_IS_HERE and TYPE_COMING_SOON stuff, so that they come before those in the token stream

        let returnToken tokenLexbufState token =
            pool.Return tokenTup
            returnToken tokenLexbufState token

        match token, offsideStack with 
        // inserted faux tokens need no other processing
        | _ when tokensThatNeedNoProcessingCount > 0 -> 
            tokensThatNeedNoProcessingCount <- tokensThatNeedNoProcessingCount - 1
            returnToken tokenLexbufState token

        | _ when tokenForcesHeadContextClosure token offsideStack -> 
            let ctxt = offsideStack.Head
            if debug then dprintf "IN/ELSE/ELIF/DONE/RPAREN/RBRACE/END/INTERP at %a terminates context at position %a\n" outputPos tokenStartPos outputPos ctxt.StartPos
            popCtxt()
            match endTokenForACtxt ctxt with 
            | Some tok ->
                if debug then dprintf "--> inserting %+A\n" tok
                insertToken tok
            | _ -> 
                reprocess()

        // reset on ';;' rule. A ';;' terminates ALL entries 
        | SEMICOLON_SEMICOLON, [] -> 
            if debug then dprintf ";; scheduling a reset\n"
            delayToken(pool.UseLocation(tokenTup, ORESET))
            returnToken tokenLexbufState SEMICOLON_SEMICOLON

        | ORESET, [] -> 
            if debug then dprintf "performing a reset after a ;; has been swallowed\n"
            // NOTE: The parser thread of F# Interactive will often be blocked on this call, e.g. after an entry has been 
            // processed and we're waiting for the first token of the next entry. 
            peekInitial() |> ignore
            pool.Return tokenTup
            hwTokenFetch true 


        | IN, stack when detectJoinInCtxt stack ->
            returnToken tokenLexbufState JOIN_IN

        // Balancing rule. Encountering an 'in' balances with a 'let'. i.e. even a non-offside 'in' closes a 'let' 
        // The 'IN' token is thrown away and becomes an ODECLEND 
        | IN, (CtxtLetDecl (blockLet, offsidePos) :: _) -> 
            if debug then dprintf "IN at %a (becomes %s)\n" outputPos tokenStartPos (if blockLet then "ODECLEND" else "IN")
            if tokenStartCol < offsidePos.Column then warn tokenTup (FSComp.SR.lexfltIncorrentIndentationOfIn())
            popCtxt()
            // Make sure we queue a dummy token at this position to check if any other pop rules apply
            delayToken(pool.UseLocation(tokenTup, ODUMMY token))
            returnToken tokenLexbufState (if blockLet then ODECLEND else token)

        // Balancing rule. Encountering a 'done' balances with a 'do'. i.e. even a non-offside 'done' closes a 'do' 
        // The 'DONE' token is thrown away and becomes an ODECLEND 
        | DONE, (CtxtDo offsidePos :: _) -> 
            if debug then dprintf "DONE at %a terminates CtxtDo(offsidePos=%a)\n" outputPos tokenStartPos outputPos offsidePos
            popCtxt()
            // reprocess as the DONE may close a DO context 
            delayToken(pool.UseLocation(tokenTup, ODECLEND))
            pool.Return tokenTup
            hwTokenFetch useBlockRule

        // Balancing rule. Encountering a ')' or '}' balances with a '(' or '{', even if not offside 
        | ((END | RPAREN | RBRACE _ | BAR_RBRACE | RBRACK | BAR_RBRACK | RQUOTE _ | GREATER true | INTERP_STRING_END _ | INTERP_STRING_PART _) as t2), (CtxtParen (t1, _) :: _) 
                when parenTokensBalance t1 t2 ->
            if debug then dprintf "RPAREN/RBRACE/BAR_RBRACE/RBRACK/BAR_RBRACK/RQUOTE/END at %a terminates CtxtParen()\n" outputPos tokenStartPos
            popCtxt()
            match t2 with 
            // $".... { ... }  ... { ....} " pushes a block context at second {
            //              ~~~~~~~~
            //                 ^---------INTERP_STRING_PART
            | INTERP_STRING_PART _ -> 
                pushCtxt tokenTup (CtxtParen (token, tokenTup.LexbufState.EndPos))
                pushCtxtSeqBlock(false, NoAddBlockEnd)
            | _ -> 
                // Queue a dummy token at this position to check if any closing rules apply
                delayToken(pool.UseLocation(tokenTup, ODUMMY token))
            returnToken tokenLexbufState token

        // Balancing rule. Encountering a 'end' can balance with a 'with' but only when not offside 
        | END, (CtxtWithAsAugment offsidePos :: _) 
                    when not (tokenStartCol + 1 <= offsidePos.Column) -> 
            if debug then dprintf "END at %a terminates CtxtWithAsAugment()\n" outputPos tokenStartPos
            popCtxt()
            delayToken(pool.UseLocation(tokenTup, ODUMMY token)) // make sure we queue a dummy token at this position to check if any closing rules apply
            returnToken tokenLexbufState OEND

        //  Transition rule. CtxtNamespaceHead ~~~> CtxtSeqBlock 
        //  Applied when a token other then a long identifier is seen 
        | _, (CtxtNamespaceHead (namespaceTokenPos, prevToken) :: _) -> 
            match prevToken, token with 
            | (NAMESPACE | DOT | REC | GLOBAL), (REC | IDENT _ | GLOBAL) when namespaceTokenPos.Column < tokenStartPos.Column -> 
                replaceCtxt tokenTup (CtxtNamespaceHead (namespaceTokenPos, token))
                returnToken tokenLexbufState token
            | IDENT _, DOT when namespaceTokenPos.Column < tokenStartPos.Column -> 
                replaceCtxt tokenTup (CtxtNamespaceHead (namespaceTokenPos, token))
                returnToken tokenLexbufState token
            | _ -> 
                if debug then dprintf "CtxtNamespaceHead: pushing CtxtSeqBlock\n"
                popCtxt()
                // Don't push a new context if next token is EOF, since that raises an offside warning
                match tokenTup.Token with 
                | Parser.EOF _ -> 
                    returnToken tokenLexbufState token
                | _ -> 
                    delayToken tokenTup
                    pushCtxt tokenTup (CtxtNamespaceBody namespaceTokenPos)
                    pushCtxtSeqBlockAt (tokenTup, true, AddBlockEnd) 
                    hwTokenFetch false
                   
        //  Transition rule. CtxtModuleHead ~~~> push CtxtModuleBody; push CtxtSeqBlock 
        //  Applied when a ':' or '=' token is seen 
        //  Otherwise it's a 'head' module declaration, so ignore it

        //  Here prevToken is either 'module', 'rec', 'global' (invalid), '.', or ident, because we skip attribute tokens and access modifier tokens
        | _, (CtxtModuleHead (moduleTokenPos, prevToken, lexingModuleAttributes) :: _) ->
            match prevToken, token with
            | _, GREATER_RBRACK when lexingModuleAttributes = LexingModuleAttributes
                                     && moduleTokenPos.Column < tokenStartPos.Column ->
                replaceCtxt tokenTup (CtxtModuleHead (moduleTokenPos, prevToken, NotLexingModuleAttributes))
                returnToken tokenLexbufState token
            | _ when lexingModuleAttributes = LexingModuleAttributes
                     && moduleTokenPos.Column < tokenStartPos.Column ->
                returnToken tokenLexbufState token
            | MODULE, (PUBLIC | PRIVATE | INTERNAL) when moduleTokenPos.Column < tokenStartPos.Column -> 
                returnToken tokenLexbufState token
            | MODULE, GLOBAL
            | (MODULE | REC | DOT), (REC | IDENT _)
            | IDENT _, DOT when moduleTokenPos.Column < tokenStartPos.Column -> 
                replaceCtxt tokenTup (CtxtModuleHead (moduleTokenPos, token, NotLexingModuleAttributes))
                returnToken tokenLexbufState token
            | MODULE, LBRACK_LESS when moduleTokenPos.Column < tokenStartPos.Column  ->
                replaceCtxt tokenTup (CtxtModuleHead (moduleTokenPos, prevToken, LexingModuleAttributes))
                returnToken tokenLexbufState token
            | _, (EQUALS | COLON) -> 
                if debug then dprintf "CtxtModuleHead: COLON/EQUALS, pushing CtxtModuleBody and CtxtSeqBlock\n"
                popCtxt()
                pushCtxt tokenTup (CtxtModuleBody (moduleTokenPos, false))
                pushCtxtSeqBlock(true, AddBlockEnd)
                returnToken tokenLexbufState token
            | _ -> 
                if debug then dprintf "CtxtModuleHead: start of file, CtxtSeqBlock\n"
                popCtxt()
                // Don't push a new context if next token is EOF, since that raises an offside warning
                match tokenTup.Token with 
                | Parser.EOF _ -> 
                    returnToken tokenLexbufState token
                | _ ->
                    // We have reached other tokens without encountering '=' or ':', so this is a module declaration spanning the whole file
                    delayToken tokenTup 
                    pushCtxt tokenTup (CtxtModuleBody (moduleTokenPos, true))
                    pushCtxtSeqBlockAt (tokenTup, true, AddBlockEnd) 
                    hwTokenFetch false
        //  Offside rule for SeqBlock.  
        //      f x
        //      g x
        //    ...
        | _, (CtxtSeqBlock(_, offsidePos, addBlockEnd) :: rest) when 
                            
                // NOTE: ;; does not terminate a 'namespace' body. 
                ((isSemiSemi && not (match rest with (CtxtNamespaceBody _ | CtxtModuleBody (_, true)) :: _ -> true | _ -> false)) || 
                    let grace = 
                        match token, rest with 
                            // When in a type context allow a grace of 2 column positions for '|' tokens, permits 
                            //  type x = 
                            //      A of string    <-- note missing '|' here - bad style, and perhaps should be disallowed
                            //    | B of int 
                        | BAR, (CtxtTypeDefns _ :: _) -> 2

                            // This ensures we close a type context seq block when the '|' marks
                            // of a type definition are aligned with the 'type' token. 
                            // 
                            //  type x = 
                            //  | A 
                            //  | B 
                            //  
                            //  <TOKEN>    <-- close the type context sequence block here *)
                        | _, (CtxtTypeDefns posType :: _) when offsidePos.Column = posType.Column && not (isTypeSeqBlockElementContinuator token) -> -1

                            // This ensures we close a namespace body when we see the next namespace definition 
                            // 
                            //  namespace A.B.C 
                            //  ...
                            //  
                            //  namespace <-- close the namespace body context here 
                        | _, (CtxtNamespaceBody posNamespace :: _) when offsidePos.Column = posNamespace.Column && (match token with NAMESPACE -> true | _ -> false) -> -1

                        | _ -> 
                            // Allow a grace of >2 column positions for infix tokens, permits 
                            //  let x =           
                            //        expr + expr 
                            //      + expr + expr 
                            // And   
                            //    let x =           
                            //          expr  
                            //       |> f expr 
                            //       |> f expr  
                            // Note you need a semicolon in the following situation:
                            //
                            //  let x =           
                            //        stmt
                            //       -expr     <-- not allowed, as prefix token is here considered infix
                            //
                            // i.e.
                            //
                            //  let x =           
                            //        stmt
                            //        -expr     
                            (if isInfix token then infixTokenLength token + 1 else 0)
                    (tokenStartCol + grace < offsidePos.Column)) -> 
            if debug then dprintf "offside token at column %d indicates end of CtxtSeqBlock started at %a!\n" tokenStartCol outputPos offsidePos
            popCtxt()
            if debug then (match addBlockEnd with AddBlockEnd -> dprintf "end of CtxtSeqBlock, insert OBLOCKEND \n" | _ -> ())
            match addBlockEnd with 
            | AddBlockEnd -> insertToken OBLOCKEND
            | AddOneSidedBlockEnd -> insertToken ORIGHT_BLOCK_END
            | NoAddBlockEnd -> reprocess()

        //  Offside rule for SeqBlock.
        //    fff
        //       eeeee
        //  <tok>
        | _, (CtxtVanilla(offsidePos, _) :: _) when isSemiSemi || tokenStartCol <= offsidePos.Column -> 
            if debug then dprintf "offside token at column %d indicates end of CtxtVanilla started at %a!\n" tokenStartCol outputPos offsidePos
            popCtxt()
            reprocess()

        //  Offside rule for SeqBlock - special case
        //  [< ... >]
        //  decl

        | _, (CtxtSeqBlock(NotFirstInSeqBlock, offsidePos, addBlockEnd) :: _) 
                    when (match token with GREATER_RBRACK -> true | _ -> false) -> 
            // Attribute-end tokens mean CtxtSeqBlock rule is NOT applied to the next token
            replaceCtxt tokenTup (CtxtSeqBlock (FirstInSeqBlock, offsidePos, addBlockEnd))
            reprocessWithoutBlockRule()

        //  Offside rule for SeqBlock - avoiding inserting OBLOCKSEP on first item in block
        | _, (CtxtSeqBlock (FirstInSeqBlock, offsidePos, addBlockEnd) :: _) when useBlockRule -> 
            // This is the first token in a block, or a token immediately 
            // following an infix operator (see above). 
            // Return the token, but only after processing any additional rules 
            // applicable for this token. Don't apply the CtxtSeqBlock rule for 
            // this token, but do apply it on subsequent tokens. 
            if debug then dprintf "repull for CtxtSeqBlockStart\n"
            replaceCtxt tokenTup (CtxtSeqBlock (NotFirstInSeqBlock, offsidePos, addBlockEnd))
            reprocessWithoutBlockRule()

        //  Offside rule for SeqBlock - inserting OBLOCKSEP on subsequent items in a block when they are precisely aligned
        //
        // let f1 () = 
        //    expr
        //    ...
        // ~~> insert OBLOCKSEP
        //
        // let f1 () = 
        //    let x = expr
        //    ...
        // ~~> insert OBLOCKSEP
        //
        // let f1 () = 
        //    let x1 = expr
        //    let x2 = expr
        //    let x3 = expr
        //    ...
        // ~~> insert OBLOCKSEP
        | _, (CtxtSeqBlock (NotFirstInSeqBlock, offsidePos, addBlockEnd) :: rest) 
                when useBlockRule 
                    && not (let isTypeCtxt = (match rest with | (CtxtTypeDefns _ :: _) -> true | _ -> false)
                            // Don't insert 'OBLOCKSEP' between namespace declarations
                            let isNamespaceCtxt = (match rest with | (CtxtNamespaceBody _ :: _) -> true | _ -> false)
                            if isNamespaceCtxt then (match token with NAMESPACE -> true | _ -> false)
                            elif isTypeCtxt then isTypeSeqBlockElementContinuator token
                            else isSeqBlockElementContinuator token)
                    && (tokenStartCol = offsidePos.Column) 
                    && (tokenStartPos.OriginalLine <> offsidePos.OriginalLine) -> 
                if debug then dprintf "offside at column %d matches start of block(%a)! delaying token, returning OBLOCKSEP\n" tokenStartCol outputPos offsidePos
                replaceCtxt tokenTup (CtxtSeqBlock (FirstInSeqBlock, offsidePos, addBlockEnd))
                // No change to offside stack: another statement block starts...
                insertTokenFromPrevPosToCurrentPos OBLOCKSEP

        //  Offside rule for CtxtLetDecl 
        // let .... = 
        //    ...
        // <and>
        //
        // let .... = 
        //    ...
        // <in>
        //
        //   let .... =
        //       ...
        //  <*>
        | _, (CtxtLetDecl (true, offsidePos) :: _) when 
                        isSemiSemi || (if isLetContinuator token then tokenStartCol + 1 else tokenStartCol) <= offsidePos.Column -> 
            if debug then dprintf "token at column %d is offside from LET(offsidePos=%a)! delaying token, returning ODECLEND\n" tokenStartCol outputPos offsidePos
            popCtxt()
            insertToken ODECLEND

        | _, (CtxtDo offsidePos :: _) 
                when isSemiSemi || (if isDoContinuator token then tokenStartCol + 1 else tokenStartCol) <= offsidePos.Column -> 
            if debug then dprintf "token at column %d is offside from DO(offsidePos=%a)! delaying token, returning ODECLEND\n" tokenStartCol outputPos offsidePos
            popCtxt()
            insertToken ODECLEND

        // class
        //    interface AAA
        //  ...
        // ...

        | _, (CtxtInterfaceHead offsidePos :: _) 
                when isSemiSemi || (if isInterfaceContinuator token then tokenStartCol + 1 else tokenStartCol) <= offsidePos.Column -> 
            if debug then dprintf "token at column %d is offside from INTERFACE(offsidePos=%a)! pop and reprocess\n" tokenStartCol outputPos offsidePos
            popCtxt()
            reprocess()

        | _, (CtxtTypeDefns offsidePos :: _) 
                when isSemiSemi || (if isTypeContinuator token then tokenStartCol + 1 else tokenStartCol) <= offsidePos.Column -> 
            if debug then dprintf "token at column %d is offside from TYPE(offsidePos=%a)! pop and reprocess\n" tokenStartCol outputPos offsidePos
            popCtxt()
            reprocess()

        // module A.B.M  
        //    ...
        // module M = ...
        // end
        //  module M = ...
        // ...
        // NOTE: ;; does not terminate a whole file module body. 
        | _, ((CtxtModuleBody (offsidePos, wholeFile)) :: _) when (isSemiSemi && not wholeFile) || tokenStartCol <= offsidePos.Column -> 
            if debug then dprintf "token at column %d is offside from MODULE with offsidePos %a! delaying token\n" tokenStartCol outputPos offsidePos
            popCtxt()
            reprocess()

        // NOTE: ;; does not terminate a 'namespace' body. 
        | _, ((CtxtNamespaceBody offsidePos) :: _) when (* isSemiSemi || *) (if isNamespaceContinuator token then tokenStartCol + 1 else tokenStartCol) <= offsidePos.Column -> 
            if debug then dprintf "token at column %d is offside from NAMESPACE with offsidePos %a! delaying token\n" tokenStartCol outputPos offsidePos
            popCtxt()
            reprocess()

        | _, ((CtxtException offsidePos) :: _) when isSemiSemi || tokenStartCol <= offsidePos.Column -> 
            if debug then dprintf "token at column %d is offside from EXCEPTION with offsidePos %a! delaying token\n" tokenStartCol outputPos offsidePos
            popCtxt()
            reprocess()

        // Pop CtxtMemberBody when offside. Insert an ODECLEND to indicate the end of the member 
        | _, ((CtxtMemberBody offsidePos) :: _) when isSemiSemi || tokenStartCol <= offsidePos.Column -> 
            if debug then dprintf "token at column %d is offside from MEMBER/OVERRIDE head with offsidePos %a!\n" tokenStartCol outputPos offsidePos
            popCtxt()
            insertToken ODECLEND

        // Pop CtxtMemberHead when offside 
        | _, ((CtxtMemberHead offsidePos) :: _) when isSemiSemi || tokenStartCol <= offsidePos.Column -> 
            if debug then dprintf "token at column %d is offside from MEMBER/OVERRIDE head with offsidePos %a!\n" tokenStartCol outputPos offsidePos
            popCtxt()
            reprocess()

        | _, (CtxtIf offsidePos :: _) 
                    when isSemiSemi || (if isIfBlockContinuator token then tokenStartCol + 1 else tokenStartCol) <= offsidePos.Column -> 
            if debug then dprintf "offside from CtxtIf\n"
            popCtxt()
            reprocess()
                
        | _, (CtxtWithAsLet offsidePos :: _) 
                    when isSemiSemi || (if isLetContinuator token then tokenStartCol + 1 else tokenStartCol) <= offsidePos.Column -> 
            if debug then dprintf "offside from CtxtWithAsLet\n"
            popCtxt()
            insertToken OEND
                
        | _, (CtxtWithAsAugment offsidePos :: _) 
                    when isSemiSemi || (if isWithAugmentBlockContinuator token then tokenStartCol + 1 else tokenStartCol) <= offsidePos.Column -> 
            if debug then dprintf "offside from CtxtWithAsAugment, isWithAugmentBlockContinuator = %b\n" (isWithAugmentBlockContinuator token)
            popCtxt()
            insertToken ODECLEND 
                
        | _, (CtxtMatch offsidePos :: _) 
                    when isSemiSemi || tokenStartCol <= offsidePos.Column -> 
            if debug then dprintf "offside from CtxtMatch\n"
            popCtxt()
            reprocess()
                
        | _, (CtxtFor offsidePos :: _) 
                    when isSemiSemi || (if isForLoopContinuator token then tokenStartCol + 1 else tokenStartCol) <= offsidePos.Column -> 
            if debug then dprintf "offside from CtxtFor\n"
            popCtxt()
            reprocess()
                
        | _, (CtxtWhile offsidePos :: _) 
                    when isSemiSemi || (if isWhileBlockContinuator token then tokenStartCol + 1 else tokenStartCol) <= offsidePos.Column -> 
            if debug then dprintf "offside from CtxtWhile\n"
            popCtxt()
            reprocess()
                
        | _, (CtxtWhen offsidePos :: _) 
                    when isSemiSemi || tokenStartCol <= offsidePos.Column -> 
            if debug then dprintf "offside from CtxtWhen\n"
            popCtxt()
            reprocess()
                
        | _, (CtxtFun offsidePos :: _) 
                    when isSemiSemi || tokenStartCol <= offsidePos.Column -> 
            if debug then dprintf "offside from CtxtFun\n"
            popCtxt()
            insertToken OEND
                
        | _, (CtxtFunction offsidePos :: _) 
                    when isSemiSemi || tokenStartCol <= offsidePos.Column -> 
            popCtxt()
            reprocess()
                
        | _, (CtxtTry offsidePos :: _) 
                    when isSemiSemi || (if isTryBlockContinuator token then tokenStartCol + 1 else tokenStartCol) <= offsidePos.Column -> 
            if debug then dprintf "offside from CtxtTry\n"
            popCtxt()
            reprocess()
                
        //  then 
        //     ...
        //  else  
        //
        //  then 
        //     ...
        | _, (CtxtThen offsidePos :: _) when isSemiSemi || (if isThenBlockContinuator token then tokenStartCol + 1 else tokenStartCol)<= offsidePos.Column -> 
            if debug then dprintf "offside from CtxtThen, popping\n"
            popCtxt()
            reprocess()
                
        //  else ...
        // ....
        //
        | _, (CtxtElse (offsidePos) :: _) when isSemiSemi || tokenStartCol <= offsidePos.Column -> 
            if debug then dprintf "offside from CtxtElse, popping\n"
            popCtxt()
            reprocess()

        // leadingBar=false permits match patterns without an initial '|' 
        | _, (CtxtMatchClauses (leadingBar, offsidePos) :: _) 
                  when (isSemiSemi || 
                        (match token with 
                            // BAR occurs in pattern matching 'with' blocks 
                            | BAR -> 
                                let cond1 = tokenStartCol + (if leadingBar then 0 else 2) < offsidePos.Column
                                let cond2 = tokenStartCol + (if leadingBar then 1 else 2) < offsidePos.Column
                                if (cond1 <> cond2) then 
                                    errorR(IndentationProblem(FSComp.SR.lexfltSeparatorTokensOfPatternMatchMisaligned(), mkSynRange (startPosOfTokenTup tokenTup) tokenTup.LexbufState.EndPos))
                                cond1
                            | END -> tokenStartCol + (if leadingBar then -1 else 1) < offsidePos.Column
                            | _ -> tokenStartCol + (if leadingBar then -1 else 1) < offsidePos.Column)) -> 
            if debug then dprintf "offside from WITH, tokenStartCol = %d, offsidePos = %a, delaying token, returning OEND\n" tokenStartCol outputPos offsidePos
            popCtxt()
            insertToken OEND
                

        //  namespace ... ~~~> CtxtNamespaceHead 
        | NAMESPACE, (_ :: _) -> 
            if debug then dprintf "NAMESPACE: entering CtxtNamespaceHead, awaiting end of long identifier to push CtxtSeqBlock\n" 
            pushCtxt tokenTup (CtxtNamespaceHead (tokenStartPos, token))
            returnToken tokenLexbufState token
                
        //  module ... ~~~> CtxtModuleHead 
        | MODULE, (_ :: _) -> 
            insertComingSoonTokens("MODULE", MODULE_COMING_SOON, MODULE_IS_HERE)
            if debug then dprintf "MODULE: entering CtxtModuleHead, awaiting EQUALS to go to CtxtSeqBlock (%a)\n" outputPos tokenStartPos
            pushCtxt tokenTup (CtxtModuleHead (tokenStartPos, token, NotLexingModuleAttributes))
            pool.Return tokenTup
            hwTokenFetch useBlockRule
                
        // exception ... ~~~> CtxtException 
        | EXCEPTION, (_ :: _) -> 
            if debug then dprintf "EXCEPTION: entering CtxtException(%a)\n" outputPos tokenStartPos
            pushCtxt tokenTup (CtxtException tokenStartPos)
            returnToken tokenLexbufState token
                
        //  let ... ~~~> CtxtLetDecl 
        //     -- this rule only applies to 
        //              - 'static let' 
        | LET isUse, (ctxt :: _) when (match ctxt with CtxtMemberHead _ -> true | _ -> false) -> 
            if debug then dprintf "LET: entering CtxtLetDecl(), awaiting EQUALS to go to CtxtSeqBlock (%a)\n" outputPos tokenStartPos
            let startPos = match ctxt with CtxtMemberHead startPos -> startPos | _ -> tokenStartPos
            popCtxt() // get rid of the CtxtMemberHead
            pushCtxt tokenTup (CtxtLetDecl(true, startPos))
            returnToken tokenLexbufState (OLET isUse)

        // let ... ~~~> CtxtLetDecl 
        //     -- this rule only applies to 
        //              - 'let' 'right-on' a SeqBlock line 
        //              - 'let' in a CtxtMatchClauses, which is a parse error, but we need to treat as OLET to get various O...END tokens to enable parser to recover
        | LET isUse, (ctxt :: _) -> 
            let blockLet = match ctxt with | CtxtSeqBlock _ -> true 
                                            | CtxtMatchClauses _ -> true 
                                            | _ -> false
            if debug then dprintf "LET: entering CtxtLetDecl(blockLet=%b), awaiting EQUALS to go to CtxtSeqBlock (%a)\n" blockLet outputPos tokenStartPos
            pushCtxt tokenTup (CtxtLetDecl(blockLet, tokenStartPos))
            returnToken tokenLexbufState (if blockLet then OLET isUse else token)
                
        //  let!  ... ~~~> CtxtLetDecl 
        | BINDER b, (ctxt :: _) -> 
            let blockLet = match ctxt with CtxtSeqBlock _ -> true | _ -> false
            if debug then dprintf "LET: entering CtxtLetDecl(blockLet=%b), awaiting EQUALS to go to CtxtSeqBlock (%a)\n" blockLet outputPos tokenStartPos
            pushCtxt tokenTup (CtxtLetDecl(blockLet, tokenStartPos))
            returnToken tokenLexbufState (if blockLet then OBINDER b else token)

        //  and!  ... ~~~> CtxtLetDecl 
        | AND_BANG isUse, (ctxt :: _) -> 
            let blockLet = match ctxt with CtxtSeqBlock _ -> true | _ -> false
            if debug then dprintf "AND!: entering CtxtLetDecl(blockLet=%b), awaiting EQUALS to go to CtxtSeqBlock (%a)\n" blockLet outputPos tokenStartPos
            pushCtxt tokenTup (CtxtLetDecl(blockLet,tokenStartPos))
            returnToken tokenLexbufState (if blockLet then OAND_BANG isUse else token)

        | (VAL | STATIC | ABSTRACT | MEMBER | OVERRIDE | DEFAULT), ctxtStack when thereIsACtxtMemberBodyOnTheStackAndWeShouldPopStackForUpcomingMember ctxtStack -> 
            if debug then dprintf "STATIC/MEMBER/OVERRIDE/DEFAULT: already inside CtxtMemberBody, popping all that context before starting next member...\n"
            // save this token, we'll consume it again later...
            delayTokenNoProcessing tokenTup
            // ... after we've popped all contexts and inserted END tokens
            while (match offsideStack.Head with CtxtMemberBody _ -> false | _ -> true) do
                match endTokenForACtxt offsideStack.Head with
                // some contexts require us to insert various END tokens
                | Some tok ->  
                    popCtxt()
                    if debug then dprintf "--> inserting %+A\n" tok
                    delayTokenNoProcessing (pool.UseLocation(tokenTup, tok))
                // for the rest, we silently pop them
                | _ -> popCtxt()
            popCtxt() // pop CtxtMemberBody
            if debug then dprintf "...STATIC/MEMBER/OVERRIDE/DEFAULT: finished popping all that context\n"
            hwTokenFetch useBlockRule
                
        //  static member ... ~~~> CtxtMemberHead 
        //  static ... ~~~> CtxtMemberHead 
        //  member ... ~~~> CtxtMemberHead 
        //  override ... ~~~> CtxtMemberHead 
        //  default ... ~~~> CtxtMemberHead 
        //  val ... ~~~> CtxtMemberHead 
        | (VAL | STATIC | ABSTRACT | MEMBER | OVERRIDE | DEFAULT), (ctxt :: _) when (match ctxt with CtxtMemberHead _ -> false | _ -> true) -> 
            if debug then dprintf "STATIC/MEMBER/OVERRIDE/DEFAULT: entering CtxtMemberHead, awaiting EQUALS to go to CtxtSeqBlock (%a)\n" outputPos tokenStartPos
            pushCtxt tokenTup (CtxtMemberHead tokenStartPos)
            returnToken tokenLexbufState token

        //  public new... ~~~> CtxtMemberHead 
        | (PUBLIC | PRIVATE | INTERNAL), (_ctxt :: _) when (match peekNextToken() with NEW -> true | _ -> false) -> 
            if debug then dprintf "PUBLIC/PRIVATE/INTERNAL NEW: entering CtxtMemberHead, awaiting EQUALS to go to CtxtSeqBlock (%a)\n" outputPos tokenStartPos
            pushCtxt tokenTup (CtxtMemberHead tokenStartPos)
            returnToken tokenLexbufState token

        //  new( ~~~> CtxtMemberHead, if not already there because of 'public' 
        | NEW, ctxt :: _ when (match peekNextToken() with LPAREN -> true | _ -> false) && (match ctxt with CtxtMemberHead _ -> false | _ -> true) -> 
            if debug then dprintf "NEW: entering CtxtMemberHead, awaiting EQUALS to go to CtxtSeqBlock (%a)\n" outputPos tokenStartPos
            pushCtxt tokenTup (CtxtMemberHead tokenStartPos)
            returnToken tokenLexbufState token
                                     
        //  'let ... = ' ~~~> CtxtSeqBlock 
        | EQUALS, (CtxtLetDecl _ :: _) ->  
            if debug then dprintf "CtxtLetDecl: EQUALS, pushing CtxtSeqBlock\n"
            pushCtxtSeqBlock(true, AddBlockEnd)
            returnToken tokenLexbufState token

        | EQUALS, (CtxtTypeDefns _ :: _) ->  
            if debug then dprintf "CtxType: EQUALS, pushing CtxtSeqBlock\n"
            pushCtxtSeqBlock(true, AddBlockEnd)
            returnToken tokenLexbufState token

        | (LAZY | ASSERT), _ ->  
            if isControlFlowOrNotSameLine() then 
                if debug then dprintf "LAZY/ASSERT, pushing CtxtSeqBlock\n"
                pushCtxtSeqBlock(true, AddBlockEnd)
                returnToken tokenLexbufState (match token with LAZY -> OLAZY | _ -> OASSERT)
            else
                returnToken tokenLexbufState token


        //  'with id = ' ~~~> CtxtSeqBlock 
        //  'with M.id = ' ~~~> CtxtSeqBlock 
        //  'with id1 = 1
        //        id2 = ...  ~~~> CtxtSeqBlock 
        //  'with id1 = 1
        //        M.id2 = ...  ~~~> CtxtSeqBlock 
        //  '{ id = ... ' ~~~> CtxtSeqBlock 
        //  '{ M.id = ... ' ~~~> CtxtSeqBlock 
        //  '{ id1 = 1
        //     id2 = ... ' ~~~> CtxtSeqBlock 
        //  '{ id1 = 1
        //     M.id2 = ... ' ~~~> CtxtSeqBlock 
        | EQUALS, ((CtxtWithAsLet _) :: _)  // This detects 'with = '. 
        | EQUALS, ((CtxtVanilla (_, true)) :: (CtxtSeqBlock _) :: (CtxtWithAsLet _ | CtxtParen((LBRACE _ | LBRACE_BAR), _)) :: _) ->  
            if debug then dprintf "CtxtLetDecl/CtxtWithAsLet: EQUALS, pushing CtxtSeqBlock\n"
            // We don't insert begin/end block tokens for single-line bindings since we can't properly distinguish single-line *)
            // record update expressions such as "{ t with gbuckets=Array.copy t.gbuckets; gcount=t.gcount }" *)
            // These have a syntactically odd status because of the use of ";" to terminate expressions, so each *)
            // "=" binding is not properly balanced by "in" or "and" tokens in the single line syntax (unlike other bindings) *)
            if isControlFlowOrNotSameLine() then 
                pushCtxtSeqBlock(true, AddBlockEnd)
            else
                pushCtxtSeqBlock(false, NoAddBlockEnd)
            returnToken tokenLexbufState token

        //  'new(... =' ~~~> CtxtMemberBody, CtxtSeqBlock 
        //  'member ... =' ~~~> CtxtMemberBody, CtxtSeqBlock 
        //  'static member ... =' ~~~> CtxtMemberBody, CtxtSeqBlock 
        //  'default ... =' ~~~> CtxtMemberBody, CtxtSeqBlock 
        //  'override ... =' ~~~> CtxtMemberBody, CtxtSeqBlock 
        | EQUALS, ((CtxtMemberHead offsidePos) :: _) ->  
            if debug then dprintf "CtxtMemberHead: EQUALS, pushing CtxtSeqBlock\n"
            replaceCtxt tokenTup (CtxtMemberBody (offsidePos))
            pushCtxtSeqBlock(true, AddBlockEnd)
            returnToken tokenLexbufState token

        // '(' tokens are balanced with ')' tokens and also introduce a CtxtSeqBlock 
        // $".... { ... }  ... { ....} " pushes a block context at first {
        // ~~~~~~~~
        //    ^---------INTERP_STRING_BEGIN_PART
        | (BEGIN | LPAREN | SIG | LBRACE _ | LBRACE_BAR | LBRACK | LBRACK_BAR | LQUOTE _ | LESS true | INTERP_STRING_BEGIN_PART _), _ ->
            if debug then dprintf "LPAREN etc., pushes CtxtParen, pushing CtxtSeqBlock, tokenStartPos = %a\n" outputPos tokenStartPos
            let pos = match token with
                      | INTERP_STRING_BEGIN_PART _ -> tokenTup.LexbufState.EndPos
                      | _ -> tokenStartPos
            pushCtxt tokenTup (CtxtParen (token, pos))
            pushCtxtSeqBlock(false, NoAddBlockEnd)
            returnToken tokenLexbufState token

        // '(' tokens are balanced with ')' tokens and also introduce a CtxtSeqBlock 
        | STRUCT, ctxts                       
                when (match ctxts with 
                        | CtxtSeqBlock _ :: (CtxtModuleBody _ | CtxtTypeDefns _) :: _ -> 
                            // type ... = struct ... end 
                            // module ... = struct ... end 
                        true 
                             
                        | _ -> false) (* type X<'a when 'a : struct> *) ->
            if debug then dprintf "LPAREN etc., pushes CtxtParen, pushing CtxtSeqBlock, tokenStartPos = %a\n" outputPos tokenStartPos
            pushCtxt tokenTup (CtxtParen (token, tokenStartPos))
            pushCtxtSeqBlock(false, NoAddBlockEnd)
            returnToken tokenLexbufState token

        | RARROW, ctxts 
                // Only treat '->' as a starting a sequence block in certain circumstances 
                when (match ctxts with 
                        // comprehension/match 
                        | (CtxtWhile _ | CtxtFor _ | CtxtWhen _ | CtxtMatchClauses _ | CtxtFun _) :: _ -> true 
                        // comprehension 
                        | (CtxtSeqBlock _ :: CtxtParen ((LBRACK | LBRACE _ | LBRACE_BAR | LBRACK_BAR), _) :: _) -> true  
                        // comprehension 
                        | (CtxtSeqBlock _ :: (CtxtDo _ | CtxtWhile _ | CtxtFor _ | CtxtWhen _ | CtxtMatchClauses _ | CtxtTry _ | CtxtThen _ | CtxtElse _) :: _) -> true 
                        | _ -> false) ->
            if debug then dprintf "RARROW, pushing CtxtSeqBlock, tokenStartPos = %a\n" outputPos tokenStartPos
            pushCtxtSeqBlock(false, AddOneSidedBlockEnd)
            returnToken tokenLexbufState token

        | LARROW, _ when isControlFlowOrNotSameLine() ->
            if debug then dprintf "LARROW, pushing CtxtSeqBlock, tokenStartPos = %a\n" outputPos tokenStartPos
            pushCtxtSeqBlock(true, AddBlockEnd)
            returnToken tokenLexbufState token

        //  do  ~~> CtxtDo;CtxtSeqBlock  (unconditionally) 
        | (DO | DO_BANG), _ -> 
            if debug then dprintf "DO: pushing CtxtSeqBlock, tokenStartPos = %a\n" outputPos tokenStartPos
            pushCtxt tokenTup (CtxtDo (tokenStartPos))
            pushCtxtSeqBlock(true, AddBlockEnd)
            returnToken tokenLexbufState (match token with DO -> ODO | DO_BANG -> ODO_BANG | _ -> failwith "unreachable")

        // The r.h.s. of an infix token begins a new block. 
        | _, ctxts when (isInfix token && 
                            not (isSameLine()) && 
                            // This doesn't apply to the use of any infix tokens in a pattern match or 'when' block
                            // For example
                            //
                            //       match x with
                            //       | _ when true &&
                            //                false ->   // the 'false' token shouldn't start a new block
                            //                let x = 1 
                            //                x
                            (match ctxts with CtxtMatchClauses _ :: _ -> false | _ -> true)) -> 

            if debug then dprintf "(Infix etc.), pushing CtxtSeqBlock, tokenStartPos = %a\n" outputPos tokenStartPos
            pushCtxtSeqBlock(false, NoAddBlockEnd)
            returnToken tokenLexbufState token

        | WITH, ((CtxtTry _ | CtxtMatch _) :: _) -> 
            let lookaheadTokenTup = peekNextTokenTup()
            let lookaheadTokenStartPos = startPosOfTokenTup lookaheadTokenTup
            let leadingBar = match (peekNextToken()) with BAR -> true | _ -> false
            if debug then dprintf "WITH, pushing CtxtMatchClauses, lookaheadTokenStartPos = %a, tokenStartPos = %a\n" outputPos lookaheadTokenStartPos outputPos tokenStartPos
            pushCtxt lookaheadTokenTup (CtxtMatchClauses(leadingBar, lookaheadTokenStartPos))
            returnToken tokenLexbufState OWITH 

        | FINALLY, (CtxtTry _ :: _) -> 
            if debug then dprintf "FINALLY, pushing pushCtxtSeqBlock, tokenStartPos = %a\n" outputPos tokenStartPos
            pushCtxtSeqBlock(true, AddBlockEnd)
            returnToken tokenLexbufState token

        | WITH, (((CtxtException _ | CtxtTypeDefns _ | CtxtMemberHead _ | CtxtInterfaceHead _ | CtxtMemberBody _) as limCtxt) :: _) 
        | WITH, ((CtxtSeqBlock _) as limCtxt :: CtxtParen((LBRACE _ | LBRACE_BAR), _) :: _) -> 
            let lookaheadTokenTup = peekNextTokenTup()
            let lookaheadTokenStartPos = startPosOfTokenTup lookaheadTokenTup
            match lookaheadTokenTup.Token with 
            | RBRACE _
            | IDENT _ 
            // The next clause detects the access annotations after the 'with' in:
            //    member  x.PublicGetSetProperty 
            //                 with public get i = "Ralf"
            //                 and  private set i v = ()  
            //    
            //    as well as:                      
            //    member  x.PublicGetSetProperty 
            //                 with inline get() = "Ralf"
            //                 and  [<Foo>] set v = ()  
            //    
            | PUBLIC | PRIVATE | INTERNAL | INLINE -> 

                let offsidePos = 
                    if lookaheadTokenStartPos.Column > tokenTup.LexbufState.EndPos.Column then
                        // This detects:
                        //    { new Foo 
                        //      with M() = 1
                        //      and  N() = 2 } 
                        // and treats the inner bindings as if they were member bindings. 
                        // (HOWEVER: note the above language construct is now deprecated/removed)
                        // 
                        // It also happens to detect
                        //    { foo with m = 1
                        //               n = 2 }
                        // So we're careful to set the offside column to be the minimum required *)
                        tokenStartPos
                    else
                        // This detects:
                        //    { foo with 
                        //        m = 1
                        //        n = 2 }
                        // So we're careful to set the offside column to be the minimum required *)
                        limCtxt.StartPos
                if debug then dprintf "WITH, pushing CtxtWithAsLet, tokenStartPos = %a, lookaheadTokenStartPos = %a\n" outputPos tokenStartPos outputPos lookaheadTokenStartPos
                pushCtxt tokenTup (CtxtWithAsLet offsidePos)
                    
                // Detect 'with' bindings of the form 
                //
                //    with x = ...
                //
                // Which can only be part of 
                //
                //   { r with x = ... }
                //
                // and in this case push a CtxtSeqBlock to cover the sequence
                let isFollowedByLongIdentEquals = 
                    let tokenTup = popNextTokenTup()
                    let res = isLongIdentEquals tokenTup.Token
                    delayToken tokenTup
                    res

                if isFollowedByLongIdentEquals then
                    pushCtxtSeqBlock(false, NoAddBlockEnd)
                      
                returnToken tokenLexbufState OWITH 
            | _ -> 
                if debug then dprintf "WITH, pushing CtxtWithAsAugment and CtxtSeqBlock, tokenStartPos = %a, limCtxt = %A\n" outputPos tokenStartPos limCtxt
                //
                //  For attributes on properties:
                //      member  x.PublicGetSetProperty 
                //         with [<Foo>]  get() = "Ralf"
                if (match lookaheadTokenTup.Token with LBRACK_LESS -> true | _ -> false) && (lookaheadTokenStartPos.OriginalLine = tokenTup.StartPos.OriginalLine) then
                    let offsidePos = tokenStartPos
                    pushCtxt tokenTup (CtxtWithAsLet offsidePos)
                    returnToken tokenLexbufState OWITH 

                // Recovery for `interface ... with` member without further indented member implementations
                elif lookaheadTokenStartPos.Column <= limCtxt.StartCol && (match limCtxt with CtxtInterfaceHead _ -> true | _ -> false) then
                    returnToken tokenLexbufState token

                else
                    // In these situations
                    //    interface I with 
                    //        ...
                    //    end
                    //    exception ... with 
                    //        ...
                    //    end
                    //    type ... with 
                    //        ...
                    //    end
                    //    member x.P 
                    //       with get() = ...
                    //       and  set() = ...
                    //    member x.P with 
                    //        get() = ...
                    // The limit is "interface"/"exception"/"type" 
                    let offsidePos = limCtxt.StartPos
                           
                    pushCtxt tokenTup (CtxtWithAsAugment offsidePos)
                    pushCtxtSeqBlock(true, AddBlockEnd)
                    returnToken tokenLexbufState token 

        | WITH, stack -> 
            if debug then dprintf "WITH\n"
            if debug then dprintf "WITH --> NO MATCH, pushing CtxtWithAsAugment (type augmentation), stack = %A" stack
            pushCtxt tokenTup (CtxtWithAsAugment tokenStartPos)
            pushCtxtSeqBlock(true, AddBlockEnd)
            returnToken tokenLexbufState token 

        | FUNCTION, _ -> 
            let lookaheadTokenTup = peekNextTokenTup()
            let lookaheadTokenStartPos = startPosOfTokenTup lookaheadTokenTup
            let leadingBar = match (peekNextToken()) with BAR -> true | _ -> false
            pushCtxt tokenTup (CtxtFunction tokenStartPos)
            pushCtxt lookaheadTokenTup (CtxtMatchClauses(leadingBar, lookaheadTokenStartPos))
            returnToken tokenLexbufState OFUNCTION

        | THEN, _ -> 
            if debug then dprintf "THEN, replacing THEN with OTHEN, pushing CtxtSeqBlock;CtxtThen(%a)\n" outputPos tokenStartPos
            pushCtxt tokenTup (CtxtThen tokenStartPos)
            pushCtxtSeqBlock(true, AddBlockEnd)
            returnToken tokenLexbufState OTHEN 

        | ELSE, _ -> 
            let lookaheadTokenTup = peekNextTokenTup()
            let lookaheadTokenStartPos = startPosOfTokenTup lookaheadTokenTup
            let lookaheadTokenEndPos = endPosOfTokenTup lookaheadTokenTup
            match peekNextToken() with 
            | IF when isSameLine() ->
                // We convert ELSE IF to ELIF since it then opens the block at the right point,
                // In particular the case
                //    if e1 then e2
                //    else if e3 then e4
                //    else if e5 then e6 
                popNextTokenTup() |> pool.Return
                if debug then dprintf "ELSE IF: replacing ELSE IF with ELIF, pushing CtxtIf, CtxtVanilla(%a)\n" outputPos tokenStartPos
                pushCtxt tokenTup (CtxtIf tokenStartPos)
                // Combine the original range of both tokens as the range for the ELIF keyword.
                let correctedTokenLexbufState = LexbufState(tokenStartPos, lookaheadTokenEndPos, false)
                returnToken correctedTokenLexbufState ELIF

            | _ -> 
                if debug then dprintf "ELSE: replacing ELSE with OELSE, pushing CtxtSeqBlock, CtxtElse(%a)\n" outputPos lookaheadTokenStartPos
                pushCtxt tokenTup (CtxtElse tokenStartPos)
                pushCtxtSeqBlock(true, AddBlockEnd)
                returnToken tokenLexbufState OELSE

        | (ELIF | IF), _ -> 
            if debug then dprintf "IF, pushing CtxtIf(%a)\n" outputPos tokenStartPos
            pushCtxt tokenTup (CtxtIf (tokenStartPos))
            returnToken tokenLexbufState token

        | (MATCH | MATCH_BANG), _ -> 
            if debug then dprintf "MATCH, pushing CtxtMatch(%a)\n" outputPos tokenStartPos
            pushCtxt tokenTup (CtxtMatch (tokenStartPos))
            returnToken tokenLexbufState token

        | FOR, _ -> 
            if debug then dprintf "FOR, pushing CtxtFor(%a)\n" outputPos tokenStartPos
            pushCtxt tokenTup (CtxtFor (tokenStartPos))
            returnToken tokenLexbufState token

        | WHILE, _ -> 
            if debug then dprintf "WHILE, pushing CtxtWhile(%a)\n" outputPos tokenStartPos
            pushCtxt tokenTup (CtxtWhile (tokenStartPos))
            returnToken tokenLexbufState token

        | WHEN, ((CtxtSeqBlock _) :: _) -> 
            if debug then dprintf "WHEN, pushing CtxtWhen(%a)\n" outputPos tokenStartPos
            pushCtxt tokenTup (CtxtWhen (tokenStartPos))
            returnToken tokenLexbufState token

        | FUN, _ -> 
            if debug then dprintf "FUN, pushing CtxtFun(%a)\n" outputPos tokenStartPos
            pushCtxt tokenTup (CtxtFun (tokenStartPos))
            returnToken tokenLexbufState OFUN

        | INTERFACE, _ -> 
            let lookaheadTokenTup = peekNextTokenTup()
            let lookaheadTokenStartPos = startPosOfTokenTup lookaheadTokenTup
            match lookaheadTokenTup.Token with 
            // type I = interface .... end 
            | DEFAULT | OVERRIDE | INTERFACE | NEW | TYPE | STATIC | END | MEMBER | ABSTRACT | INHERIT | LBRACK_LESS -> 
                if debug then dprintf "INTERFACE, pushing CtxtParen, tokenStartPos = %a, lookaheadTokenStartPos = %a\n" outputPos tokenStartPos outputPos lookaheadTokenStartPos
                pushCtxt tokenTup (CtxtParen (token, tokenStartPos))
                pushCtxtSeqBlock(true, AddBlockEnd)
                returnToken tokenLexbufState token
            // type C with interface .... with 
            // type C = interface .... with 
            | _ -> 
                if debug then dprintf "INTERFACE, pushing CtxtInterfaceHead, tokenStartPos = %a, lookaheadTokenStartPos = %a\n" outputPos tokenStartPos outputPos lookaheadTokenStartPos
                pushCtxt tokenTup (CtxtInterfaceHead tokenStartPos)
                returnToken tokenLexbufState OINTERFACE_MEMBER

        | CLASS, _ -> 
            if debug then dprintf "CLASS, pushing CtxtParen(%a)\n" outputPos tokenStartPos
            pushCtxt tokenTup (CtxtParen (token, tokenStartPos))
            pushCtxtSeqBlock(true, AddBlockEnd)
            returnToken tokenLexbufState token

        | TYPE, _ -> 
            insertComingSoonTokens("TYPE", TYPE_COMING_SOON, TYPE_IS_HERE)
            if debug then dprintf "TYPE, pushing CtxtTypeDefns(%a)\n" outputPos tokenStartPos
            pushCtxt tokenTup (CtxtTypeDefns tokenStartPos)
            pool.Return tokenTup
            hwTokenFetch useBlockRule

        | TRY, _ -> 
            if debug then dprintf "Try, pushing CtxtTry(%a)\n" outputPos tokenStartPos
            pushCtxt tokenTup (CtxtTry (tokenStartPos))
            // The ideal spec would be to push a begin/end block pair here, but we can only do that 
            // if we are able to balance the WITH with the TRY. We can't do that because of the numerous ways 
            // WITH is used in the grammar (see what happens when we hit a WITH below. 
            // This hits in the single line case: "try make ef1 t with _ -> make ef2 t". 
                
            pushCtxtSeqBlock(false, AddOneSidedBlockEnd)
            returnToken tokenLexbufState token

        | OBLOCKBEGIN, _ -> 
            returnToken tokenLexbufState token  
                
        | ODUMMY(_), _ -> 
            if debug then dprintf "skipping dummy token as no offside rules apply\n"
            pool.Return tokenTup
            hwTokenFetch (useBlockRule) 
                
        // Ordinary tokens start a vanilla block 
        | _, CtxtSeqBlock _ :: _ -> 
            pushCtxt tokenTup (CtxtVanilla(tokenStartPos, isLongIdentEquals token))
            if debug then dprintf "pushing CtxtVanilla at tokenStartPos = %a\n" outputPos tokenStartPos
            returnToken tokenLexbufState token  
                
        | _ -> 
            returnToken tokenLexbufState token  

    and rulesForBothSoftWhiteAndHardWhite(tokenTup: TokenTup) = 
          match tokenTup.Token with 
          | HASH_IDENT (ident) ->
              let hashPos = new LexbufState(tokenTup.StartPos, tokenTup.StartPos.ShiftColumnBy(1), false)
              let identPos = new LexbufState(tokenTup.StartPos.ShiftColumnBy(1), tokenTup.EndPos, false)
              delayToken(new TokenTup(IDENT(ident), identPos, tokenTup.LastTokenPos))
              delayToken(new TokenTup(HASH, hashPos, tokenTup.LastTokenPos))
              true

          // Insert HIGH_PRECEDENCE_PAREN_APP if needed 
          | IDENT _ when (nextTokenIsAdjacentLParenOrLBrack tokenTup).IsSome ->
              let dotTokenTup = peekNextTokenTup()
              if debug then dprintf "inserting HIGH_PRECEDENCE_PAREN_APP at dotTokenPos = %a\n" outputPos (startPosOfTokenTup dotTokenTup)
              let hpa = 
                  match nextTokenIsAdjacentLParenOrLBrack tokenTup with 
                  | Some LPAREN -> HIGH_PRECEDENCE_PAREN_APP
                  | Some LBRACK -> HIGH_PRECEDENCE_BRACK_APP
                  | _ -> failwith "unreachable"
              delayToken(pool.UseLocation(dotTokenTup, hpa))
              delayToken tokenTup
              true

          // Insert HIGH_PRECEDENCE_TYAPP if needed 
          | (DELEGATE | IDENT _ | IEEE64 _ | IEEE32 _ | DECIMAL _ | INT8 _ | INT16 _ | INT32 _ | INT64 _ | NATIVEINT _ | UINT8 _ | UINT16 _ | UINT32 _ | UINT64 _ | UNATIVEINT _ | BIGNUM _) when peekAdjacentTypars false tokenTup ->
              let lessTokenTup = popNextTokenTup()
              delayToken (pool.UseLocation(lessTokenTup, match lessTokenTup.Token with LESS _ -> LESS true | _ -> failwith "unreachable")) 

              if debug then dprintf "softwhite inserting HIGH_PRECEDENCE_TYAPP at dotTokenPos = %a\n" outputPos (startPosOfTokenTup lessTokenTup)

              delayToken (pool.UseLocation(lessTokenTup, HIGH_PRECEDENCE_TYAPP))
              delayToken (tokenTup)
              pool.Return lessTokenTup
              true

          // ..^1 will get parsed as DOT_DOT_HAT 1 while 1..^2 will get parsed as 1 DOT_DOT HAT 2
          // because of processing rule underneath this.
          | (DOT_DOT_HAT) -> 
              let hatPos = new LexbufState(tokenTup.EndPos.ShiftColumnBy(-1), tokenTup.EndPos, false)
              delayToken(let rented = pool.Rent() in rented.Token <- INFIX_AT_HAT_OP("^"); rented.LexbufState <- hatPos; rented.LastTokenPos <- tokenTup.LastTokenPos; rented)
              delayToken(pool.UseShiftedLocation(tokenTup, DOT_DOT, 0, -1))
              pool.Return tokenTup
              true

          // Split this token to allow "1..2" for range specification 
          | INT32_DOT_DOT (i, v) ->
              let dotDotPos = new LexbufState(tokenTup.EndPos.ShiftColumnBy(-2), tokenTup.EndPos, false)
              delayToken(let rented = pool.Rent() in rented.Token <- DOT_DOT; rented.LexbufState <- dotDotPos; rented.LastTokenPos <- tokenTup.LastTokenPos; rented)
              delayToken(pool.UseShiftedLocation(tokenTup, INT32(i, v), 0, -2))
              pool.Return tokenTup
              true
          // Split @>. and @@>. into two 
          | RQUOTE_DOT (s, raw) ->
              let dotPos = new LexbufState(tokenTup.EndPos.ShiftColumnBy(-1), tokenTup.EndPos, false)
              delayToken(let rented = pool.Rent() in rented.Token <- DOT; rented.LexbufState <- dotPos; rented.LastTokenPos <- tokenTup.LastTokenPos; rented)
              delayToken(pool.UseShiftedLocation(tokenTup, RQUOTE(s, raw), 0, -1))
              pool.Return tokenTup
              true

          | MINUS | PLUS_MINUS_OP _ | PERCENT_OP _ | AMP | AMP_AMP
                when ((match tokenTup.Token with 
                       | PLUS_MINUS_OP s -> (s = "+") || (s = "+.") || (s = "-.") 
                       | PERCENT_OP s -> (s = "%") || (s = "%%") 
                       | _ -> true) &&
                      nextTokenIsAdjacent tokenTup && 
                      not (prevWasAtomicEnd && (tokenTup.LastTokenPos = startPosOfTokenTup tokenTup))) ->

              let plus = 
                  match tokenTup.Token with 
                  | PLUS_MINUS_OP s -> (s = "+") 
                  | _ -> false
              let plusOrMinus = 
                  match tokenTup.Token with 
                  | PLUS_MINUS_OP s -> (s = "+") 
                  | MINUS -> true 
                  | _ -> false
              let nextTokenTup = popNextTokenTup()

              /// Merge the location of the prefix token and the literal
              let delayMergedToken tok = 
                  let rented = pool.Rent()
                  rented.Token <- tok
                  rented.LexbufState <- new LexbufState(tokenTup.LexbufState.StartPos, nextTokenTup.LexbufState.EndPos, nextTokenTup.LexbufState.PastEOF)
                  rented.LastTokenPos <- tokenTup.LastTokenPos
                  delayToken(rented)
                  pool.Return nextTokenTup
                  pool.Return tokenTup

              let noMerge() = 
                  let tokenName = 
                      match tokenTup.Token with 
                      | PLUS_MINUS_OP s
                      | PERCENT_OP s -> s
                      | AMP -> "&"
                      | AMP_AMP -> "&&"
                      | MINUS -> "-" 
                      | _ -> failwith "unreachable" 
                  let token = ADJACENT_PREFIX_OP tokenName
                  delayToken nextTokenTup 
                  delayToken (pool.UseLocation(tokenTup, token))
                  pool.Return tokenTup

              if plusOrMinus then 
                  match nextTokenTup.Token with 
                  | INT8(v, bad) -> delayMergedToken(INT8((if plus then v else -v), (plus && bad))) // note: '-' makes a 'bad' max int 'good'. '+' does not
                  | INT16(v, bad) -> delayMergedToken(INT16((if plus then v else -v), (plus && bad))) // note: '-' makes a 'bad' max int 'good'. '+' does not
                  | INT32(v, bad) -> delayMergedToken(INT32((if plus then v else -v), (plus && bad))) // note: '-' makes a 'bad' max int 'good'. '+' does not
                  | INT32_DOT_DOT(v, bad) -> delayMergedToken(INT32_DOT_DOT((if plus then v else -v), (plus && bad))) // note: '-' makes a 'bad' max int 'good'. '+' does not
                  | INT64(v, bad) -> delayMergedToken(INT64((if plus then v else -v), (plus && bad))) // note: '-' makes a 'bad' max int 'good'. '+' does not
                  | NATIVEINT(v, bad) -> delayMergedToken(NATIVEINT((if plus then v else -v), (plus && bad))) // note: '-' makes a 'bad' max int 'good'. '+' does not
                  | IEEE32 v -> delayMergedToken(IEEE32(if plus then v else -v))
                  | IEEE64 v -> delayMergedToken(IEEE64(if plus then v else -v))
                  | DECIMAL v -> delayMergedToken(DECIMAL(if plus then v else System.Decimal.op_UnaryNegation v))
                  | BIGNUM(v, s) -> delayMergedToken(BIGNUM((if plus then v else "-" + v), s))
                  | _ -> noMerge()
              else
                  noMerge()
              true

          | _ -> 
              false
  
    and pushCtxtSeqBlock(addBlockBegin, addBlockEnd) = pushCtxtSeqBlockAt (peekNextTokenTup(), addBlockBegin, addBlockEnd) 
    and pushCtxtSeqBlockAt(p: TokenTup, addBlockBegin, addBlockEnd) = 
         if addBlockBegin then
             if debug then dprintf "--> insert OBLOCKBEGIN \n"
             delayToken(pool.UseLocation(p, OBLOCKBEGIN))
         pushCtxt p (CtxtSeqBlock(FirstInSeqBlock, startPosOfTokenTup p, addBlockEnd)) 

    let rec swTokenFetch() = 
        let tokenTup = popNextTokenTup()
        let tokenReplaced = rulesForBothSoftWhiteAndHardWhite tokenTup
        if tokenReplaced then swTokenFetch() 
        else
            let lexbufState = tokenTup.LexbufState
            let tok = tokenTup.Token
            pool.Return tokenTup
            returnToken lexbufState tok

    //----------------------------------------------------------------------------
    // Part VI. Publish the new lexer function.  
    //--------------------------------------------------------------------------

    member _.LexBuffer = lexbuf

    member _.GetToken() = 
        if not initialized then 
            let _firstTokenTup = peekInitial()
            ()

        if lightStatus.Status
        then hwTokenFetch true  
        else swTokenFetch()
  
// LexFilterImpl does the majority of the work for offsides rules and other magic.
// LexFilter just wraps it with light post-processing that introduces a few more 'coming soon' symbols, to
// make it easier for the parser to 'look ahead' and safely shift tokens in a number of recovery scenarios.
type LexFilter (lightStatus: LightSyntaxStatus, compilingFsLib, lexer, lexbuf: UnicodeLexing.Lexbuf) = 
    let inner = new LexFilterImpl (lightStatus, compilingFsLib, lexer, lexbuf)

    // We don't interact with lexbuf state at all, any inserted tokens have same state/location as the real one read, so
    // we don't have to do any of the wrapped lexbuf magic that you see in LexFilterImpl.
    let delayedStack = System.Collections.Generic.Stack<token>()
    let delayToken tok = delayedStack.Push tok 

    let popNextToken() = 
        if delayedStack.Count > 0 then 
            let tokenTup = delayedStack.Pop()
            tokenTup
        else
            inner.GetToken()

    let insertComingSoonTokens comingSoon isHere =
        if debug then dprintf "inserting 6 copies of %+A before %+A\n" comingSoon isHere
        delayToken isHere
        for i in 1..6 do
            delayToken comingSoon

    member _.LexBuffer = inner.LexBuffer 

    member _.GetToken () = 
        let rec loop() =
            let token = popNextToken()
            match token with
            | RBRACE _ -> 
                insertComingSoonTokens RBRACE_COMING_SOON RBRACE_IS_HERE
                loop()
            | RPAREN -> 
                insertComingSoonTokens RPAREN_COMING_SOON RPAREN_IS_HERE
                loop()
            | OBLOCKEND -> 
                insertComingSoonTokens OBLOCKEND_COMING_SOON OBLOCKEND_IS_HERE
                loop()
            | _ -> token
        loop()
