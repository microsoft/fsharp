// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.CheckFormatStrings

open System.Text
open Internal.Utilities.Library 
open Internal.Utilities.Library.Extras
open FSharp.Compiler.ConstraintSolver
open FSharp.Compiler.NameResolution
open FSharp.Compiler.Syntax
open FSharp.Compiler.SyntaxTreeOps
open FSharp.Compiler.Text
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.TcGlobals

type FormatItem = Simple of TType | FuncAndVal 

let copyAndFixupFormatTypar m tp = 
    let _,_,tinst = FreshenAndFixupTypars traitCtxtNone m TyparRigidity.Flexible [] [] [tp]
    List.head tinst

let lowestDefaultPriority = 0 (* See comment on TyparConstraint.DefaultsTo *)

let mkFlexibleFormatTypar m tys dflt = 
    let tp = Construct.NewTypar (TyparKind.Type, TyparRigidity.Rigid, SynTypar(mkSynId m "fmt",TyparStaticReq.HeadType,true),false,TyparDynamicReq.Yes,[],false,false)
    tp.SetConstraints [ TyparConstraint.SimpleChoice (tys,m); TyparConstraint.DefaultsTo (lowestDefaultPriority,dflt,m)]
    copyAndFixupFormatTypar m tp

let mkFlexibleIntFormatTypar (g: TcGlobals) m = 
    mkFlexibleFormatTypar m [ g.byte_ty; g.int16_ty; g.int32_ty; g.int64_ty;  g.sbyte_ty; g.uint16_ty; g.uint32_ty; g.uint64_ty;g.nativeint_ty;g.unativeint_ty; ] g.int_ty

let mkFlexibleDecimalFormatTypar (g: TcGlobals) m =
    mkFlexibleFormatTypar m [ g.decimal_ty ] g.decimal_ty
    
let mkFlexibleFloatFormatTypar (g: TcGlobals) m = 
    mkFlexibleFormatTypar m [ g.float_ty; g.float32_ty; g.decimal_ty ] g.float_ty

type FormatInfoRegister = 
  { mutable leftJustify    : bool 
    mutable numPrefixIfPos : char option
    mutable addZeros       : bool
    mutable precision      : bool}

let newInfo () = 
  { leftJustify    = false
    numPrefixIfPos = None
    addZeros       = false
    precision      = false}

let parseFormatStringInternal (m: range) (fragRanges: range list) (g: TcGlobals) isInterpolated isFormattableString (context: FormatStringCheckContext option) fmt printerArgTy printerResidueTy = 

    // As background: the F# compiler tokenizes strings on the assumption that the only thing you need from
    // them is the actual corresponding text, e.g. of a string literal.  This means many different textual input strings
    // in the input file correspond to the 'fmt' string we have here.
    //
    // The problem with this is that when we go to colorize the format specifiers in string, we need to do
    // that with respect to the original string source text in order to lay down accurate colorizations.
    //
    // One approach would be to change the F# lexer to also crack every string in a more structured way, recording
    // both the original source text and the actual string literal.  However this would be invasive and possibly
    // too expensive since the vast majority of strings don't need this treatment.
    //
    // So instead, for format strings alone - and only when processing in the IDE - we crack the "original"
    // source of the string by going back and getting the format string from the input source file by using the
    // relevant ranges
    //
    // For interpolated strings this may involve many fragments, e.g. 
    //     $"abc %d{"
    //     "} def %s{"
    //     "} xyz"
    // In this case we are given the range of each fragment. One annoying thing is that we must lop off the
    // quotations, $, {, } symbols off the end of each string fragment. This information should probably
    // be given to us by the lexer.
    //
    // Note this also means that when compiling (command-line or background IncrementalBuilder in the IDE
    // there are no accurate intra-string ranges available for exact error message locations within the string.
    // The 'm' range passed as an input is however accurate and covers the whole string.
    ///
    let fmt, fragments = 

        //printfn "--------------------" 
        //printfn "context.IsSome = %b" context.IsSome
        //printfn "fmt  = <<<%s>>>" fmt
        //printfn "isInterpolated = %b" isInterpolated
        //printfn "fragRanges = %A" fragRanges

        match context with
        | Some context when fragRanges.Length > 0 ->
            let sourceText = context.SourceText
            //printfn "sourceText.IsSome = %b" sourceText.IsSome
            let lineStartPositions = context.LineStartPositions
            //printfn "lineStartPositions.Length = %d" lineStartPositions.Length
            let length = sourceText.Length
            let numFrags = fragRanges.Length
            let fmts =
             [ for i, fragRange in List.indexed fragRanges do
                let m = fragRange
                //printfn "m.EndLine = %d" m.EndLine
                if m.StartLine - 1 < lineStartPositions.Length && m.EndLine - 1 < lineStartPositions.Length then
                    let startIndex = lineStartPositions.[m.StartLine-1] + m.StartColumn
                    let endIndex = lineStartPositions.[m.EndLine-1] + m.EndColumn
                    // Note, some extra """ text may be included at end of these snippets, meaning CheckFormatString in the IDE
                    // may be using a slightly false format string to colorize the %d markers.  This doesn't matter as there
                    // won't be relevant %d in these sections
                    //
                    // However we make an effort to remove these to keep the calls to GetSubStringText valid.  So
                    // we work out how much extra text there is at the end of the last line of the fragment,
                    // which may or may not be quote markers. If there's no flex, we don't trim the quote marks
                    let endNextLineIndex = if m.EndLine < lineStartPositions.Length then lineStartPositions.[m.EndLine] else endIndex
                    let endIndexFlex = endNextLineIndex - endIndex
                    let mLength = endIndex - startIndex

                    //let startIndex2 = if m.StartLine < lineStartPositions.Length then lineStartPositions.[m.StartLine] else startIndex
                    //let sourceLineFromOffset = sourceText.GetSubTextString(startIndex, (startIndex2 - startIndex))
                    //printfn "i = %d, mLength = %d, endIndexFlex = %d, sourceLineFromOffset = <<<%s>>>" i mLength endIndexFlex sourceLineFromOffset

                    if isInterpolated && i=0 && startIndex < length-4 && sourceText.SubTextEquals("$\"\"\"", startIndex) then
                        // Take of the ending triple quote or '{'
                        let fragLength = mLength - 4 - min endIndexFlex (if i = numFrags-1 then 3 else 1)
                        (4, sourceText.GetSubTextString(startIndex + 4, fragLength), m)
                    elif not isInterpolated && i=0 && startIndex < length-3 && sourceText.SubTextEquals("\"\"\"", startIndex) then
                        // Take of the ending triple quote or '{'
                        let fragLength = mLength - 2 - min endIndexFlex (if i = numFrags-1 then 3 else 1)
                        (3, sourceText.GetSubTextString(startIndex + 3, fragLength), m)
                    elif isInterpolated && i=0 && startIndex < length-3 && sourceText.SubTextEquals("$@\"", startIndex) then
                        // Take of the ending quote or '{', always length 1
                        let fragLength = mLength - 3 - min endIndexFlex 1
                        (3, sourceText.GetSubTextString(startIndex + 3, fragLength), m)
                    elif isInterpolated && i=0 && startIndex < length-3 && sourceText.SubTextEquals("@$\"", startIndex) then
                        // Take of the ending quote or '{', always length 1
                        let fragLength = mLength - 3 - min endIndexFlex 1
                        (3, sourceText.GetSubTextString(startIndex + 3, fragLength), m)
                    elif not isInterpolated && i=0 && startIndex < length-2 && sourceText.SubTextEquals("@\"", startIndex) then
                        // Take of the ending quote or '{', always length 1
                        let fragLength = mLength - 2 - min endIndexFlex 1
                        (2, sourceText.GetSubTextString(startIndex + 2, fragLength), m)
                    elif isInterpolated && i=0 && startIndex < length-2 && sourceText.SubTextEquals("$\"", startIndex) then
                        // Take of the ending quote or '{', always length 1
                        let fragLength = mLength - 2 - min endIndexFlex 1
                        (2, sourceText.GetSubTextString(startIndex + 2, fragLength), m)
                    elif isInterpolated && i <> 0 && startIndex < length-1 && sourceText.SubTextEquals("}", startIndex) then
                        // Take of the ending quote or '{', always length 1
                        let fragLength = mLength - 1 - min endIndexFlex 1
                        (1, sourceText.GetSubTextString(startIndex + 1, fragLength), m)
                    else
                        // Take of the ending quote or '{', always length 1
                        let fragLength = mLength - 1 - min endIndexFlex 1
                        (1, sourceText.GetSubTextString(startIndex + 1, fragLength), m)
                else (1, fmt, m) ]

            //printfn "fmts = %A" fmts

            // Join the fragments with holes. Note this join is only used on the IDE path,
            // the CheckExpressions.fs does its own joining with the right alignments etc. substituted
            // On the IDE path we don't do any checking of these in this file (some checking is
            // done in CheckExpressions.fs) so it's ok to join with just '%P()'.  
            let fmt = fmts |> List.map p23 |> String.concat "%P()" 
            let fragments, _ = 
                (0, fmts) ||> List.mapFold (fun i (offset, fmt, fragRange) ->
                    (i, offset, fragRange), i + fmt.Length + 4) // the '4' is the length of '%P()' joins

            //printfn "fmt2 = <<<%s>>>" fmt
            //printfn "fragments = %A" fragments
            fmt, fragments
        | _ -> 
            // Don't muck with the fmt when there is no source code context to go get the original
            // source code (i.e. when compiling or background checking)
            fmt, [ (0, 1, m) ]

    let len = fmt.Length

    let specifierLocations = ResizeArray()

    // For FormattableString we collect a .NET Format String with {0} etc. replacing text.  '%%' are replaced
    // by '%', we check there are no '%' formats, and '{{' and '}}' are *not* replaced since the subsequent
    // call to String.Format etc. will process them.
    let dotnetFormatString = StringBuilder() 
    let appendToDotnetFormatString (s: string) = dotnetFormatString.Append(s) |> ignore 
    let mutable dotnetFormatStringInterpolationHoleCount = 0
    let percentATys = ResizeArray<_>()

    // fragLine, fragCol - track our location w.r.t. the marker for the start of this chunk
    // 
    let rec parseLoop acc (i, fragLine, fragCol) fragments = 
       
       // Check if we've moved into the next fragment.  Note this will always activate on
       // the first step, i.e. when i=0
       let (struct (fragLine, fragCol, fragments)) =
           match fragments with 
           | (idx, fragOffset, fragRange: range)::rest when i >= idx  ->
               //printfn "i = %d, idx = %d, moving into next fragment at %A plus fragOffset %d" i idx fragRange fragOffset
               struct (fragRange.StartLine, fragRange.StartColumn + fragOffset, rest)

           | _ -> struct (fragLine, fragCol, fragments)
       //printfn "parseLoop: i = %d, fragLine = %d, fragCol = %d" i fragLine fragCol

       if i >= len then
           let argtys =
               if acc |> List.forall (fun (p, _) -> p = None) then // without positional specifiers
                   acc |> List.map snd |> List.rev
               else  
                   failwithf "%s" <| FSComp.SR.forPositionalSpecifiersNotPermitted()
           argtys
       elif System.Char.IsSurrogatePair(fmt,i) then 
          appendToDotnetFormatString (fmt.[i..i+1])
          parseLoop acc (i+2, fragLine, fragCol+2) fragments
       else 
          let c = fmt.[i]
          match c with
          | '%' ->
              let startFragCol = fragCol
              let fragCol = fragCol+1
              let i = i+1 
              if i >= len then failwithf "%s" <| FSComp.SR.forMissingFormatSpecifier()
              let info = newInfo()

              let rec flags i =
                if i >= len then failwithf "%s" <| FSComp.SR.forMissingFormatSpecifier()
                match fmt.[i] with
                | '-' -> 
                    if info.leftJustify then failwithf "%s" <| FSComp.SR.forFlagSetTwice("-")
                    info.leftJustify <- true
                    flags(i+1)
                | '+' -> 
                    if info.numPrefixIfPos <> None then failwithf "%s" <| FSComp.SR.forPrefixFlagSpacePlusSetTwice()
                    info.numPrefixIfPos <- Some '+'
                    flags(i+1)
                | '0' -> 
                    if info.addZeros then failwithf "%s" <| FSComp.SR.forFlagSetTwice("0")
                    info.addZeros <- true
                    flags(i+1)
                | ' ' -> 
                    if info.numPrefixIfPos <> None then failwithf "%s" <| FSComp.SR.forPrefixFlagSpacePlusSetTwice()
                    info.numPrefixIfPos <- Some ' '
                    flags(i+1)
                | '#' -> failwithf "%s" <| FSComp.SR.forHashSpecifierIsInvalid()
                | _ -> i

              let rec digitsPrecision i = 
                if i >= len then failwithf "%s" <| FSComp.SR.forBadPrecision()
                match fmt.[i] with
                | c when System.Char.IsDigit c -> digitsPrecision (i+1)
                | _ -> i 

              let precision i = 
                if i >= len then failwithf "%s" <| FSComp.SR.forBadWidth()
                match fmt.[i] with
                | c when System.Char.IsDigit c -> info.precision <- true; false,digitsPrecision (i+1)
                | '*' -> info.precision <- true; true,(i+1)
                | _ -> failwithf "%s" <| FSComp.SR.forPrecisionMissingAfterDot()

              let optionalDotAndPrecision i = 
                if i >= len then failwithf "%s" <| FSComp.SR.forBadPrecision()
                match fmt.[i] with
                | '.' -> precision (i+1)
                | _ -> false,i

              let rec digitsWidthAndPrecision n i = 
                if i >= len then failwithf "%s" <| FSComp.SR.forBadPrecision()
                match fmt.[i] with
                | c when System.Char.IsDigit c -> digitsWidthAndPrecision (n*10 + int c - int '0') (i+1)
                | _ -> Some n, optionalDotAndPrecision i

              let widthAndPrecision i = 
                if i >= len then failwithf "%s" <| FSComp.SR.forBadPrecision()
                match fmt.[i] with
                | c when System.Char.IsDigit c -> false,digitsWidthAndPrecision 0 i
                | '*' -> true, (None, optionalDotAndPrecision (i+1))
                | _ -> false, (None, optionalDotAndPrecision i)

              let rec digitsPosition n i =
                  if i >= len then failwithf "%s" <| FSComp.SR.forBadPrecision()
                  match fmt.[i] with
                  | c when System.Char.IsDigit c -> digitsPosition (n*10 + int c - int '0') (i+1)
                  | '$' -> Some n, i+1
                  | _ -> None, i

              let position i =
                  match fmt.[i] with
                  | c when c >= '1' && c <= '9' ->
                      let p, i' = digitsPosition (int c - int '0') (i+1)
                      if p = None then None, i else p, i'
                  | _ -> None, i
              
              let oldI = i
              let posi, i = position i
              let fragCol = fragCol + i - oldI

              let oldI = i
              let i = flags i 
              let fragCol = fragCol + i - oldI

              let oldI = i
              let widthArg,(widthValue, (precisionArg,i)) = widthAndPrecision i 
              let fragCol = fragCol + i - oldI

              if i >= len then failwithf "%s" <| FSComp.SR.forBadPrecision()

              let acc = if precisionArg then (Option.map ((+)1) posi, g.int_ty) :: acc else acc 

              let acc = if widthArg then (Option.map ((+)1) posi, g.int_ty) :: acc else acc 

              let checkNoPrecision c =
                  if info.precision then failwithf "%s" <| FSComp.SR.forFormatDoesntSupportPrecision(c.ToString())

              let checkNoZeroFlag c =
                  if info.addZeros then failwithf "%s" <| FSComp.SR.forDoesNotSupportZeroFlag(c.ToString())

              let checkNoNumericPrefix c =
                  match info.numPrefixIfPos with 
                  | Some n -> failwithf "%s" <| FSComp.SR.forDoesNotSupportPrefixFlag(c.ToString(), n.ToString())
                  | None -> ()

              let checkOtherFlags c = 
                  checkNoPrecision c 
                  checkNoZeroFlag c 
                  checkNoNumericPrefix c

              // Explicitly typed holes in interpolated strings "....%d{x}..." get additional '%P()' as a hole place marker
              let skipPossibleInterpolationHole i =
                  if isInterpolated then 
                      if i+1 < len && fmt.[i] = '%' && fmt.[i+1] = 'P'  then
                          let i = i + 2
                          if i+1 < len && fmt.[i] = '('  && fmt.[i+1] = ')' then 
                              if isFormattableString then 
                                  failwithf "%s" <| FSComp.SR.forFormatInvalidForInterpolated4()
                              i + 2
                          else 
                              failwithf "%s" <| FSComp.SR.forFormatInvalidForInterpolated2()
                      else
                          failwithf "%s" <| FSComp.SR.forFormatInvalidForInterpolated()
                  else i

              // Implicitly typed holes in interpolated strings are translated to '... %P(...)...' in the
              // type checker.  They should always have '(...)' after for format string.  
              let requireAndSkipInterpolationHoleFormat i =
                  if i < len && fmt.[i] = '(' then 
                      let i2 = fmt.IndexOf(")", i+1)
                      if i2 = -1 then 
                          failwithf "%s" <| FSComp.SR.forFormatInvalidForInterpolated3()
                      else 
                          let dotnetAlignment = match widthValue with None -> "" | Some w -> "," + (if info.leftJustify then "-" else "") + string w
                          let dotnetNumberFormat = match fmt.[i+1..i2-1] with "" -> "" | s -> ":" + s
                          appendToDotnetFormatString ("{" + string dotnetFormatStringInterpolationHoleCount + dotnetAlignment  + dotnetNumberFormat + "}") 
                          dotnetFormatStringInterpolationHoleCount <- dotnetFormatStringInterpolationHoleCount + 1
                          i2+1
                  else
                      failwithf "%s" <| FSComp.SR.forFormatInvalidForInterpolated3()

              let collectSpecifierLocation fragLine fragCol numStdArgs = 
                  match context with
                  | Some _ ->
                      let numArgsForSpecifier =
                        numStdArgs + (if widthArg then 1 else 0) + (if precisionArg then 1 else 0)
                      specifierLocations.Add(
                          (Range.mkFileIndexRange m.FileIndex 
                              (Position.mkPos fragLine startFragCol) 
                              (Position.mkPos fragLine (fragCol + 1))), numArgsForSpecifier)
                  | None -> ()

              let ch = fmt.[i]
              match ch with
              | '%' ->
                  collectSpecifierLocation fragLine fragCol 0
                  appendToDotnetFormatString "%"
                  parseLoop acc (i+1, fragLine, fragCol+1) fragments

              | ('d' | 'i' | 'u' | 'B' | 'o' | 'x' | 'X') ->
                  if ch = 'B' then ErrorLogger.checkLanguageFeatureError g.langVersion Features.LanguageFeature.PrintfBinaryFormat m
                  if info.precision then failwithf "%s" <| FSComp.SR.forFormatDoesntSupportPrecision(ch.ToString())
                  collectSpecifierLocation fragLine fragCol 1
                  let i = skipPossibleInterpolationHole (i+1)
                  parseLoop ((posi, mkFlexibleIntFormatTypar g m) :: acc) (i, fragLine, fragCol+1) fragments

              | ('l' | 'L') ->
                  if info.precision then failwithf "%s" <| FSComp.SR.forFormatDoesntSupportPrecision(ch.ToString())
                  let fragCol = fragCol+1
                  let i = i+1
                  
                  // "bad format specifier ... In F# code you can use %d, %x, %o or %u instead ..."
                  if i >= len then 
                      raise (Failure (FSComp.SR.forBadFormatSpecifier()))
                  // Always error for %l and %Lx
                  failwithf "%s" <| FSComp.SR.forLIsUnnecessary()
                  match fmt.[i] with
                  | ('d' | 'i' | 'o' | 'u' | 'x' | 'X') -> 
                      collectSpecifierLocation fragLine fragCol 1
                      let i = skipPossibleInterpolationHole (i+1)
                      parseLoop ((posi, mkFlexibleIntFormatTypar g m) :: acc)  (i, fragLine, fragCol+1) fragments
                  | _ -> failwithf "%s" <| FSComp.SR.forBadFormatSpecifier()

              | ('h' | 'H') ->
                  failwithf "%s" <| FSComp.SR.forHIsUnnecessary()

              | 'M' ->
                  collectSpecifierLocation fragLine fragCol 1
                  let i = skipPossibleInterpolationHole (i+1)
                  parseLoop ((posi, mkFlexibleDecimalFormatTypar g m) :: acc) (i, fragLine, fragCol+1) fragments

              | ('f' | 'F' | 'e' | 'E' | 'g' | 'G') ->
                  collectSpecifierLocation fragLine fragCol 1
                  let i = skipPossibleInterpolationHole (i+1)
                  parseLoop ((posi, mkFlexibleFloatFormatTypar g m) :: acc) (i, fragLine, fragCol+1) fragments

              | 'b' ->
                  checkOtherFlags ch
                  collectSpecifierLocation fragLine fragCol 1
                  let i = skipPossibleInterpolationHole (i+1)
                  parseLoop ((posi, g.bool_ty)  :: acc) (i, fragLine, fragCol+1) fragments

              | 'c' ->
                  checkOtherFlags ch
                  collectSpecifierLocation fragLine fragCol 1
                  let i = skipPossibleInterpolationHole (i+1)
                  parseLoop ((posi, g.char_ty)  :: acc) (i, fragLine, fragCol+1) fragments

              | 's' ->
                  checkOtherFlags ch
                  collectSpecifierLocation fragLine fragCol 1
                  let i = skipPossibleInterpolationHole (i+1)
                  parseLoop ((posi, g.string_ty)  :: acc) (i, fragLine, fragCol+1) fragments

              | 'O' ->
                  checkOtherFlags ch
                  collectSpecifierLocation fragLine fragCol 1
                  let i = skipPossibleInterpolationHole (i+1)
                  parseLoop ((posi, NewInferenceType ()) :: acc) (i, fragLine, fragCol+1) fragments

              // residue of hole "...{n}..." in interpolated strings become %P(...) 
              | 'P' when isInterpolated ->
                  checkOtherFlags ch
                  let i = requireAndSkipInterpolationHoleFormat (i+1)
                  // Note, the fragCol doesn't advance at all as these are magically inserted.  
                  parseLoop ((posi, NewInferenceType ()) :: acc) (i, fragLine, startFragCol) fragments

              | 'A' ->
                  match info.numPrefixIfPos with
                  | None     // %A has BindingFlags=Public, %+A has BindingFlags=Public | NonPublic
                  | Some '+' -> 
                      collectSpecifierLocation fragLine fragCol 1
                      let i = skipPossibleInterpolationHole (i+1)
                      let xty = NewInferenceType ()
                      percentATys.Add(xty)
                      parseLoop ((posi, xty) :: acc)  (i, fragLine, fragCol+1) fragments
                  | Some n -> failwithf "%s" <| FSComp.SR.forDoesNotSupportPrefixFlag(ch.ToString(), n.ToString())

              | 'a' ->
                  checkOtherFlags ch
                  let xty = NewInferenceType () 
                  let fty = printerArgTy --> (xty --> printerResidueTy)
                  collectSpecifierLocation fragLine fragCol 2
                  let i = skipPossibleInterpolationHole (i+1)
                  parseLoop ((Option.map ((+)1) posi, xty) ::  (posi, fty) :: acc) (i, fragLine, fragCol+1) fragments

              | 't' ->
                  checkOtherFlags ch
                  collectSpecifierLocation fragLine fragCol 1
                  let i = skipPossibleInterpolationHole (i+1)
                  parseLoop ((posi, printerArgTy --> printerResidueTy) :: acc)  (i, fragLine, fragCol+1) fragments

              | c -> failwithf "%s" <| FSComp.SR.forBadFormatSpecifierGeneral(String.make 1 c)
          
          | '\n' ->
              appendToDotnetFormatString fmt.[i..i]
              parseLoop acc (i+1, fragLine+1, 0) fragments
          | _ ->
              appendToDotnetFormatString fmt.[i..i]
              parseLoop acc (i+1, fragLine, fragCol+1) fragments
           
    let results = parseLoop [] (0, 0, m.StartColumn) fragments
    results, Seq.toList specifierLocations, dotnetFormatString.ToString(), percentATys.ToArray()

let ParseFormatString m fragmentRanges g isInterpolated isFormattableString formatStringCheckContext fmt printerArgTy printerResidueTy printerResultTy = 
    let argTys, specifierLocations, dotnetFormatString, percentATys = parseFormatStringInternal m fragmentRanges g isInterpolated isFormattableString formatStringCheckContext fmt printerArgTy printerResidueTy
    let printerTy = List.foldBack (-->) argTys printerResultTy
    let printerTupleTy = mkRefTupledTy g argTys
    argTys, printerTy, printerTupleTy, percentATys, specifierLocations, dotnetFormatString

let TryCountFormatStringArguments m g isInterpolated fmt printerArgTy printerResidueTy =
    try
        let argTys, _specifierLocations, _dotnetFormatString, _percentATys = parseFormatStringInternal m [] g isInterpolated false None fmt printerArgTy printerResidueTy
        Some argTys.Length
    with _ ->
        None

