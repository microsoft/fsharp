// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module FSharp.Compiler.SyntaxTreeOps

open Internal.Utilities.Library
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Syntax
open FSharp.Compiler.Syntax.PrettyNaming
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Compiler.Xml

/// Generate implicit argument names in parsing
type SynArgNameGenerator() =
    let mutable count = 0
    let generatedArgNamePrefix = "_arg"

    member _.New() : string = count <- count + 1; generatedArgNamePrefix + string count
    member _.Reset() = count <- 0

let ident (s, r) = Ident(s, r)

let textOfId (id: Ident) = id.idText

let pathOfLid lid = List.map textOfId lid

let arrPathOfLid lid = Array.ofList (pathOfLid lid)

let textOfPath path = String.concat "." path

let textOfLid lid = textOfPath (pathOfLid lid)

let rangeOfLid (lid: Ident list) =
    match lid with
    | [] -> failwith "rangeOfLid"
    | [id] -> id.idRange
    | h :: t -> unionRanges h.idRange (List.last t).idRange

let mkSynId m s = Ident(s, m)

let pathToSynLid m p = List.map (mkSynId m) p

let mkSynIdGet m n = SynExpr.Ident (mkSynId m n)

let mkSynLidGet m path n =
    let lid = pathToSynLid m path @ [mkSynId m n]
    let dots = List.replicate (lid.Length - 1) m
    SynExpr.LongIdent (false, LongIdentWithDots(lid, dots), None, m)

let mkSynIdGetWithAlt m id altInfo =
    match altInfo with
    | None -> SynExpr.Ident id
    | _ -> SynExpr.LongIdent (false, LongIdentWithDots([id], []), altInfo, m)

let mkSynSimplePatVar isOpt id = SynSimplePat.Id (id, None, false, false, isOpt, id.idRange)

let mkSynCompGenSimplePatVar id = SynSimplePat.Id (id, None, true, false, false, id.idRange)

/// Match a long identifier, including the case for single identifiers which gets a more optimized node in the syntax tree.
let (|LongOrSingleIdent|_|) inp =
    match inp with
    | SynExpr.LongIdent (isOpt, lidwd, altId, _m) -> Some (isOpt, lidwd, altId, lidwd.RangeWithoutAnyExtraDot)
    | SynExpr.Ident id -> Some (false, LongIdentWithDots([id], []), None, id.idRange)
    | _ -> None

let (|SingleIdent|_|) inp =
    match inp with
    | SynExpr.LongIdent (false, LongIdentWithDots([id], _), None, _) -> Some id
    | SynExpr.Ident id -> Some id
    | _ -> None

/// This affects placement of sequence points
let rec IsControlFlowExpression e =
    match e with
    | SynExpr.ObjExpr _
    | SynExpr.Lambda _
    | SynExpr.LetOrUse _
    | SynExpr.Sequential _
    // Treat "ident { ... }" as a control flow expression
    | SynExpr.App (_, _, SynExpr.Ident _, SynExpr.CompExpr _, _)
    | SynExpr.IfThenElse _
    | SynExpr.LetOrUseBang _
    | SynExpr.Match _
    | SynExpr.TryWith _
    | SynExpr.TryFinally _
    | SynExpr.For _
    | SynExpr.ForEach _
    | SynExpr.While _ -> true
    | SynExpr.Typed (e, _, _) -> IsControlFlowExpression e
    | _ -> false

let mkSynAnonField (ty: SynType) = SynField([], false, None, ty, false, PreXmlDoc.Empty, None, ty.Range)

let mkSynNamedField (ident, ty: SynType, m) = SynField([], false, Some ident, ty, false, PreXmlDoc.Empty, None, m)

let mkSynPatVar vis (id: Ident) = SynPat.Named (id, false, vis, id.idRange)

let mkSynThisPatVar (id: Ident) = SynPat.Named (id, true, None, id.idRange)

let mkSynPatMaybeVar lidwd vis m =  SynPat.LongIdent (lidwd, None, None, SynArgPats.Pats [], vis, m)

/// Extract the argument for patterns corresponding to the declaration of 'new ... = ...'
let (|SynPatForConstructorDecl|_|) x =
    match x with
    | SynPat.LongIdent (LongIdentWithDots([_], _), _, _, SynArgPats.Pats [arg], _, _) -> Some arg
    | _ -> None

/// Recognize the '()' in 'new()'
let (|SynPatForNullaryArgs|_|) x =
    match x with
    | SynPat.Paren(SynPat.Const(SynConst.Unit, _), _) -> Some()
    | _ -> None

let (|SynExprErrorSkip|) (p: SynExpr) =
    match p with
    | SynExpr.FromParseError (p, _) -> p
    | _ -> p

let (|SynExprParen|_|) (e: SynExpr) =
    match e with
    | SynExpr.Paren (SynExprErrorSkip e, a, b, c) -> Some (e, a, b, c)
    | _ -> None

let (|SynPatErrorSkip|) (p: SynPat) =
    match p with
    | SynPat.FromParseError(p, _) -> p
    | _ -> p

/// Push non-simple parts of a patten match over onto the r.h.s. of a lambda.
/// Return a simple pattern and a function to build a match on the r.h.s. if the pattern is complex
let rec SimplePatOfPat (synArgNameGenerator: SynArgNameGenerator) p =
    match p with
    | SynPat.Typed(p', ty, m) ->
        let p2, laterF = SimplePatOfPat synArgNameGenerator p'
        SynSimplePat.Typed(p2, ty, m),
        laterF

    | SynPat.Attrib(p', attribs, m) ->
        let p2, laterF = SimplePatOfPat synArgNameGenerator p'
        SynSimplePat.Attrib(p2, attribs, m),
        laterF

    | SynPat.Named (v, thisV, _, m) ->
        SynSimplePat.Id (v, None, false, thisV, false, m),
        None

    | SynPat.OptionalVal (v, m) ->
        SynSimplePat.Id (v, None, false, false, true, m),
        None

    | SynPat.Paren (p, _) -> SimplePatOfPat synArgNameGenerator p

    | SynPat.FromParseError (p, _) -> SimplePatOfPat synArgNameGenerator p

    | _ ->
        let m = p.Range
        let isCompGen, altNameRefCell, id, item =
            match p with
            | SynPat.LongIdent(LongIdentWithDots([id], _), _, None, SynArgPats.Pats [], None, _) ->
                // The pattern is 'V' or some other capitalized identifier.
                // It may be a real variable, in which case we want to maintain its name.
                // But it may also be a nullary union case or some other identifier.
                // In this case, we want to use an alternate compiler generated name for the hidden variable.
                let altNameRefCell = Some (ref (SynSimplePatAlternativeIdInfo.Undecided (mkSynId m (synArgNameGenerator.New()))))
                let item = mkSynIdGetWithAlt m id altNameRefCell
                false, altNameRefCell, id, item
            | SynPat.Named(ident, _, _, _)
            | SynPat.As(_, SynPat.Named(ident, _, _, _), _) ->
                // named pats should be referred to as their name in docs, tooltips, etc.
                let item = mkSynIdGet m ident.idText
                false, None, ident, item
            | _ ->
                let nm = synArgNameGenerator.New()
                let id = mkSynId m nm
                let item = mkSynIdGet m nm
                true, None, id, item
        let fn =
            match p with
            | SynPat.Wild _ -> None
            | _ ->
                Some (fun e ->
                    let clause = SynMatchClause(p, None, e, m, DebugPointForTarget.No)
                    SynExpr.Match (DebugPointAtBinding.NoneAtInvisible, item, [clause], clause.Range))

        SynSimplePat.Id (id, altNameRefCell, isCompGen, false, false, id.idRange), fn

let appFunOpt funOpt x = match funOpt with None -> x | Some f -> f x

let composeFunOpt funOpt1 funOpt2 = match funOpt2 with None -> funOpt1 | Some f -> Some (fun x -> appFunOpt funOpt1 (f x))

let rec SimplePatsOfPat synArgNameGenerator p =
    match p with
    | SynPat.FromParseError (p, _) -> SimplePatsOfPat synArgNameGenerator p

    | SynPat.Typed(p', ty, m) ->
        let p2, laterF = SimplePatsOfPat synArgNameGenerator p'
        SynSimplePats.Typed(p2, ty, m),
        laterF

    | SynPat.Tuple (false, ps, m)

    | SynPat.Paren(SynPat.Tuple (false, ps, _), m) ->
        let ps2, laterF =
          List.foldBack
            (fun (p', rhsf) (ps', rhsf') ->
              p':: ps',
              (composeFunOpt rhsf rhsf'))
            (List.map (SimplePatOfPat synArgNameGenerator) ps)
            ([], None)
        SynSimplePats.SimplePats (ps2, m),
        laterF

    | SynPat.Paren(SynPat.Const (SynConst.Unit, m), _)

    | SynPat.Const (SynConst.Unit, m) ->
        SynSimplePats.SimplePats ([], m),
        None

    | _ ->
        let m = p.Range
        let sp, laterF = SimplePatOfPat synArgNameGenerator p
        SynSimplePats.SimplePats ([sp], m), laterF

let PushPatternToExpr synArgNameGenerator isMember pat (rhs: SynExpr) =
    let nowPats, laterF = SimplePatsOfPat synArgNameGenerator pat
    nowPats, SynExpr.Lambda (isMember, false, nowPats, appFunOpt laterF rhs, None, rhs.Range)

let private isSimplePattern pat =
    let _nowPats, laterF = SimplePatsOfPat (SynArgNameGenerator()) pat
    Option.isNone laterF

/// "fun (UnionCase x) (UnionCase y) -> body"
///       ==>
///   "fun tmp1 tmp2 ->
///        let (UnionCase x) = tmp1 in
///        let (UnionCase y) = tmp2 in
///        body"
let PushCurriedPatternsToExpr synArgNameGenerator wholem isMember pats rhs =
    // Two phases
    // First phase: Fold back, from right to left, pushing patterns into r.h.s. expr
    let spatsl, rhs =
        (pats, ([], rhs))
           ||> List.foldBack (fun arg (spatsl, body) ->
              let spats, bodyf = SimplePatsOfPat synArgNameGenerator arg
              // accumulate the body. This builds "let (UnionCase y) = tmp2 in body"
              let body = appFunOpt bodyf body
              // accumulate the patterns
              let spatsl = spats :: spatsl
              (spatsl, body))
    // Second phase: build lambdas. Mark subsequent ones with "true" indicating they are part of an iterated sequence of lambdas
    let expr =
        match spatsl with
        | [] -> rhs
        | h :: t ->
            let expr = List.foldBack (fun spats e -> SynExpr.Lambda (isMember, true, spats, e, None, wholem)) t rhs
            let expr = SynExpr.Lambda (isMember, false, h, expr, Some (pats, rhs), wholem)
            expr
    spatsl, expr

let opNameParenGet  = CompileOpName parenGet

let opNameQMark = CompileOpName qmark

let mkSynOperator opm oper = mkSynIdGet opm (CompileOpName oper)

let mkSynInfix opm (l: SynExpr) oper (r: SynExpr) =
    let firstTwoRange = unionRanges l.Range opm
    let wholeRange = unionRanges l.Range r.Range
    let app1 = SynExpr.App (ExprAtomicFlag.NonAtomic, true, mkSynOperator opm oper, l, firstTwoRange)
    SynExpr.App (ExprAtomicFlag.NonAtomic, false, app1, r, wholeRange)

let mkSynBifix m oper x1 x2 =
    let app1 = SynExpr.App (ExprAtomicFlag.NonAtomic, true, mkSynOperator m oper, x1, m)
    SynExpr.App (ExprAtomicFlag.NonAtomic, false, app1, x2, m)

let mkSynTrifix  m oper x1 x2 x3 =
    let app1 = SynExpr.App (ExprAtomicFlag.NonAtomic, true, mkSynOperator m oper, x1, m)
    let app2 = SynExpr.App (ExprAtomicFlag.NonAtomic, false, app1, x2, m)
    SynExpr.App (ExprAtomicFlag.NonAtomic, false, app2, x3, m)

let mkSynPrefixPrim opm m oper x =
    SynExpr.App (ExprAtomicFlag.NonAtomic, false, mkSynOperator opm oper, x, m)

let mkSynPrefix opm m oper x =
    if oper = "~&" then
        SynExpr.AddressOf (true, x, opm, m)
    elif oper = "~&&" then
        SynExpr.AddressOf (false, x, opm, m)
    else
        mkSynPrefixPrim opm m oper x

let mkSynCaseName m n = [mkSynId m (CompileOpName n)]

let mkSynApp1 f x1 m = SynExpr.App (ExprAtomicFlag.NonAtomic, false, f, x1, m)

let mkSynApp2 f x1 x2 m = mkSynApp1 (mkSynApp1 f x1 m) x2 m

let mkSynApp3 f x1 x2 x3 m = mkSynApp1 (mkSynApp2 f x1 x2 m) x3 m

let mkSynApp4 f x1 x2 x3 x4 m = mkSynApp1 (mkSynApp3 f x1 x2 x3 m) x4 m

let mkSynApp5 f x1 x2 x3 x4 x5 m = mkSynApp1 (mkSynApp4 f x1 x2 x3 x4 m) x5 m

let mkSynDotParenSet  m a b c = mkSynTrifix m parenSet a b c

let mkSynDotBrackGet  m mDot a b fromEnd   = SynExpr.DotIndexedGet (a, [SynIndexerArg.One (b, fromEnd, m)], mDot, m)

let mkSynQMarkSet m a b c = mkSynTrifix m qmarkSet a b c

let mkSynDotBrackSliceGet  m mDot arr sliceArg = SynExpr.DotIndexedGet (arr, [sliceArg], mDot, m)

let mkSynDotBrackSeqSliceGet  m mDot arr (argsList: list<SynIndexerArg>) =
    SynExpr.DotIndexedGet (arr, argsList, mDot, m)

let mkSynDotParenGet lhsm dotm a b   =
    match b with
    | SynExpr.Tuple (false, [_;_], _, _)   ->
        errorR(Deprecated(FSComp.SR.astDeprecatedIndexerNotation(), lhsm))
        SynExpr.Const (SynConst.Unit, lhsm)

    | SynExpr.Tuple (false, [_;_;_], _, _) ->
        errorR(Deprecated(FSComp.SR.astDeprecatedIndexerNotation(), lhsm))
        SynExpr.Const (SynConst.Unit, lhsm)

    | _ -> mkSynInfix dotm a parenGet b

let mkSynUnit m = SynExpr.Const (SynConst.Unit, m)

let mkSynUnitPat m = SynPat.Const(SynConst.Unit, m)

let mkSynDelay m e =
    SynExpr.Lambda (false, false, SynSimplePats.SimplePats ([mkSynCompGenSimplePatVar (mkSynId m "unitVar")], m), e, None, m)

let mkSynAssign (l: SynExpr) (r: SynExpr) =
    let m = unionRanges l.Range r.Range
    match l with
    //| SynExpr.Paren (l2, m2)  -> mkSynAssign m l2 r
    | LongOrSingleIdent(false, v, None, _)  -> SynExpr.LongIdentSet (v, r, m)
    | SynExpr.DotGet (e, _, v, _)  -> SynExpr.DotSet (e, v, r, m)
    | SynExpr.DotIndexedGet (e1, e2, mDot, mLeft)  -> SynExpr.DotIndexedSet (e1, e2, r, mLeft, mDot, m)
    | SynExpr.LibraryOnlyUnionCaseFieldGet (x, y, z, _) -> SynExpr.LibraryOnlyUnionCaseFieldSet (x, y, z, r, m)
    | SynExpr.App (_, _, SynExpr.App (_, _, SingleIdent nm, a, _), b, _) when nm.idText = opNameQMark ->
        mkSynQMarkSet m a b r
    | SynExpr.App (_, _, SynExpr.App (_, _, SingleIdent nm, a, _), b, _) when nm.idText = opNameParenGet ->
        mkSynDotParenSet m a b r
    | SynExpr.App (_, _, SynExpr.LongIdent (false, v, None, _), x, _)  -> SynExpr.NamedIndexedPropertySet (v, x, r, m)
    | SynExpr.App (_, _, SynExpr.DotGet (e, _, v, _), x, _)  -> SynExpr.DotNamedIndexedPropertySet (e, v, x, r, m)
    | l  -> SynExpr.Set (l, r, m)

let mkSynDot dotm m l r =
    match l with
    | SynExpr.LongIdent (isOpt, LongIdentWithDots(lid, dots), None, _) ->
        // REVIEW: MEMORY PERFORMANCE: This list operation is memory intensive (we create a lot of these list nodes)
        SynExpr.LongIdent (isOpt, LongIdentWithDots(lid@[r], dots@[dotm]), None, m)
    | SynExpr.Ident id ->
        SynExpr.LongIdent (false, LongIdentWithDots([id;r], [dotm]), None, m)
    | SynExpr.DotGet (e, dm, LongIdentWithDots(lid, dots), _) ->
        // REVIEW: MEMORY PERFORMANCE: This is memory intensive (we create a lot of these list nodes)
        SynExpr.DotGet (e, dm, LongIdentWithDots(lid@[r], dots@[dotm]), m)
    | expr ->
        SynExpr.DotGet (expr, dotm, LongIdentWithDots([r], []), m)

let mkSynDotMissing dotm m l =
    match l with
    | SynExpr.LongIdent (isOpt, LongIdentWithDots(lid, dots), None, _) ->
         // REVIEW: MEMORY PERFORMANCE: This list operation is memory intensive (we create a lot of these list nodes)
         SynExpr.LongIdent (isOpt, LongIdentWithDots(lid, dots@[dotm]), None, m)
    | SynExpr.Ident id ->
        SynExpr.LongIdent (false, LongIdentWithDots([id], [dotm]), None, m)
    | SynExpr.DotGet (e, dm, LongIdentWithDots(lid, dots), _) ->
        SynExpr.DotGet (e, dm, LongIdentWithDots(lid, dots@[dotm]), m)// REVIEW: MEMORY PERFORMANCE: This is memory intensive (we create a lot of these list nodes)
    | expr ->
        SynExpr.DiscardAfterMissingQualificationAfterDot (expr, m)

let mkSynFunMatchLambdas synArgNameGenerator isMember wholem ps e =
    let _, e =  PushCurriedPatternsToExpr synArgNameGenerator wholem isMember ps e
    e

let arbExpr (debugStr, range: range) = SynExpr.ArbitraryAfterError (debugStr, range.MakeSynthetic())

let unionRangeWithListBy projectRangeFromThing m listOfThing = 
    (m, listOfThing) ||> List.fold (fun m thing -> unionRanges m (projectRangeFromThing thing))

let mkAttributeList attrs range : SynAttributeList list =
    [{ Attributes = attrs
       Range = range }]

let ConcatAttributesLists (attrsLists: SynAttributeList list) =
    attrsLists
    |> List.map (fun x -> x.Attributes)
    |> List.concat

let (|Attributes|) synAttributes =
    ConcatAttributesLists synAttributes

let (|TyparDecls|) (typarDecls: SynTyparDecls option) =
    typarDecls
    |> Option.map (fun x -> x.TyparDecls)
    |> Option.defaultValue []

let (|TyparsAndConstraints|) (typarDecls: SynTyparDecls option) =
    typarDecls
    |> Option.map (fun x -> x.TyparDecls, x.Constraints)
    |> Option.defaultValue ([], [])

let (|ValTyparDecls|) (SynValTyparDecls(typarDecls, canInfer)) =
    typarDecls
    |> Option.map (fun x -> x.TyparDecls, x.Constraints, canInfer)
    |> Option.defaultValue ([], [], canInfer)

let rangeOfNonNilAttrs (attrs: SynAttributes) =
    (attrs.Head.Range, attrs.Tail) ||> unionRangeWithListBy (fun a -> a.Range)

let rec stripParenTypes synType =
    match synType with
    | SynType.Paren (innerType, _) -> stripParenTypes innerType
    | _ -> synType

let (|StripParenTypes|) synType =
    stripParenTypes synType

/// Operations related to the syntactic analysis of arguments of value, function and member definitions and signatures.
module SynInfo =

    /// The argument information for an argument without a name
    let unnamedTopArg1 = SynArgInfo([], false, None)

    /// The argument information for a curried argument without a name
    let unnamedTopArg = [unnamedTopArg1]

    /// The argument information for a '()' argument
    let unitArgData = unnamedTopArg

    /// The 'argument' information for a return value where no attributes are given for the return value (the normal case)
    let unnamedRetVal = SynArgInfo([], false, None)

    /// The 'argument' information for the 'this'/'self' parameter in the cases where it is not given explicitly
    let selfMetadata = unnamedTopArg

    /// Determine if a syntactic information represents a member without arguments (which is implicitly a property getter)
    let HasNoArgs (SynValInfo(args, _)) = isNil args

    /// Check if one particular argument is an optional argument. Used when adjusting the
    /// types of optional arguments for function and member signatures.
    let IsOptionalArg (SynArgInfo(_, isOpt, _)) = isOpt

    /// Check if there are any optional arguments in the syntactic argument information. Used when adjusting the
    /// types of optional arguments for function and member signatures.
    let HasOptionalArgs (SynValInfo(args, _)) = List.exists (List.exists IsOptionalArg) args

    /// Add a parameter entry to the syntactic value information to represent the '()' argument to a property getter. This is
    /// used for the implicit '()' argument in property getter signature specifications.
    let IncorporateEmptyTupledArgForPropertyGetter (SynValInfo(args, retInfo)) = SynValInfo([] :: args, retInfo)

    /// Add a parameter entry to the syntactic value information to represent the 'this' argument. This is
    /// used for the implicit 'this' argument in member signature specifications.
    let IncorporateSelfArg (SynValInfo(args, retInfo)) = SynValInfo(selfMetadata :: args, retInfo)

    /// Add a parameter entry to the syntactic value information to represent the value argument for a property setter. This is
    /// used for the implicit value argument in property setter signature specifications.
    let IncorporateSetterArg (SynValInfo(args, retInfo)) =
         let args =
             match args with
             | [] -> [unnamedTopArg]
             | [arg] -> [arg@[unnamedTopArg1]]
             | _ -> failwith "invalid setter type"
         SynValInfo(args, retInfo)

    /// Get the argument counts for each curried argument group. Used in some adhoc places in tc.fs.
    let AritiesOfArgs (SynValInfo(args, _)) = List.map List.length args

    /// Get the argument attributes from the syntactic information for an argument.
    let AttribsOfArgData (SynArgInfo(Attributes attribs, _, _)) = attribs

    /// Infer the syntactic argument info for a single argument from a simple pattern.
    let rec InferSynArgInfoFromSimplePat attribs p =
        match p with
        | SynSimplePat.Id(nm, _, isCompGen, _, isOpt, _) ->
           SynArgInfo(attribs, isOpt, (if isCompGen then None else Some nm))
        | SynSimplePat.Typed(a, _, _) -> InferSynArgInfoFromSimplePat attribs a
        | SynSimplePat.Attrib(a, attribs2, _) -> InferSynArgInfoFromSimplePat (attribs @ attribs2) a

    /// Infer the syntactic argument info for one or more arguments one or more simple patterns.
    let rec InferSynArgInfoFromSimplePats x =
        match x with
        | SynSimplePats.SimplePats(ps, _) -> List.map (InferSynArgInfoFromSimplePat []) ps
        | SynSimplePats.Typed(ps, _, _) -> InferSynArgInfoFromSimplePats ps

    /// Infer the syntactic argument info for one or more arguments a pattern.
    let InferSynArgInfoFromPat p =
        // It is ok to use a fresh SynArgNameGenerator here, because compiler generated names are filtered from SynArgInfo, see InferSynArgInfoFromSimplePat above
        let sp, _ = SimplePatsOfPat (SynArgNameGenerator()) p
        InferSynArgInfoFromSimplePats sp

    /// Make sure only a solitary unit argument has unit elimination
    let AdjustArgsForUnitElimination infosForArgs =
        match infosForArgs with
        | [[]] -> infosForArgs
        | _ -> infosForArgs |> List.map (function [] -> unitArgData | x -> x)

    /// Transform a property declared using '[static] member P = expr' to a method taking a "unit" argument.
    /// This is similar to IncorporateEmptyTupledArgForPropertyGetter, but applies to member definitions
    /// rather than member signatures.
    let AdjustMemberArgs memFlags infosForArgs =
        match infosForArgs with
        | [] when memFlags = SynMemberKind.Member -> [] :: infosForArgs
        | _ -> infosForArgs

    /// For 'let' definitions, we infer syntactic argument information from the r.h.s. of a definition, if it
    /// is an immediate 'fun ... -> ...' or 'function ...' expression. This is noted in the F# language specification.
    /// This does not apply to member definitions nor to returns with attributes
    let InferLambdaArgs (retInfo: SynArgInfo) origRhsExpr =
        if retInfo.Attributes.Length > 0 then [] else
        let rec loop e =
            match e with
            | SynExpr.Lambda (false, _, spats, rest, _, _) ->
                InferSynArgInfoFromSimplePats spats :: loop rest
            | _ -> []
        loop origRhsExpr

    let InferSynReturnData (retInfo: SynReturnInfo option) =
        match retInfo with
        | None -> unnamedRetVal
        | Some(SynReturnInfo((_, retInfo), _)) -> retInfo

    let private emptySynValInfo = SynValInfo([], unnamedRetVal)

    let emptySynValData = SynValData(None, emptySynValInfo, None)

    /// Infer the syntactic information for a 'let' or 'member' definition, based on the argument pattern,
    /// any declared return information (e.g. .NET attributes on the return element), and the r.h.s. expression
    /// in the case of 'let' definitions.
    let InferSynValData (memberFlagsOpt: SynMemberFlags option, pat, retInfo, origRhsExpr) =

        let infosForExplicitArgs =
            match pat with
            | Some(SynPat.LongIdent(_, _, _, SynArgPats.Pats curriedArgs, _, _)) -> List.map InferSynArgInfoFromPat curriedArgs
            | _ -> []

        let explicitArgsAreSimple =
            match pat with
            | Some(SynPat.LongIdent(_, _, _, SynArgPats.Pats curriedArgs, _, _)) -> List.forall isSimplePattern curriedArgs
            | _ -> true

        let retInfo = InferSynReturnData retInfo

        match memberFlagsOpt with
        | None ->
            let infosForLambdaArgs = InferLambdaArgs retInfo origRhsExpr
            let infosForArgs = infosForExplicitArgs @ (if explicitArgsAreSimple then infosForLambdaArgs else [])
            let infosForArgs = AdjustArgsForUnitElimination infosForArgs
            SynValData(None, SynValInfo(infosForArgs, retInfo), None)

        | Some memFlags  ->
            let infosForObjArgs =
                if memFlags.IsInstance then [ selfMetadata ] else []

            let infosForArgs = AdjustMemberArgs memFlags.MemberKind infosForExplicitArgs
            let infosForArgs = AdjustArgsForUnitElimination infosForArgs

            let argInfos = infosForObjArgs @ infosForArgs
            SynValData(Some memFlags, SynValInfo(argInfos, retInfo), None)

let mkSynBindingRhs staticOptimizations rhsExpr mRhs retInfo =
    let rhsExpr = List.foldBack (fun (c, e1) e2 -> SynExpr.LibraryOnlyStaticOptimization (c, e1, e2, mRhs)) staticOptimizations rhsExpr
    let rhsExpr, retTyOpt =
        match retInfo with
        | Some (SynReturnInfo((ty, SynArgInfo(rAttribs, _, _)), tym)) -> SynExpr.Typed (rhsExpr, ty, rhsExpr.Range), Some(SynBindingReturnInfo(ty, tym, rAttribs) )
        | None -> rhsExpr, None
    rhsExpr, retTyOpt

let mkSynBinding (xmlDoc, headPat) (vis, isInline, isMutable, mBind, spBind, retInfo, origRhsExpr, mRhs, staticOptimizations, attrs, memberFlagsOpt) =
    let info = SynInfo.InferSynValData (memberFlagsOpt, Some headPat, retInfo, origRhsExpr)
    let rhsExpr, retTyOpt = mkSynBindingRhs staticOptimizations origRhsExpr mRhs retInfo
    SynBinding (vis, SynBindingKind.Normal, isInline, isMutable, attrs, xmlDoc, info, headPat, retTyOpt, rhsExpr, mBind, spBind)

let NonVirtualMemberFlags k : SynMemberFlags =
    { MemberKind=k
      IsInstance=true
      IsDispatchSlot=false
      IsOverrideOrExplicitImpl=false
      IsFinal=false }

let CtorMemberFlags : SynMemberFlags =
    { MemberKind=SynMemberKind.Constructor
      IsInstance=false
      IsDispatchSlot=false
      IsOverrideOrExplicitImpl=false
      IsFinal=false }

let ClassCtorMemberFlags : SynMemberFlags =
    { MemberKind=SynMemberKind.ClassConstructor
      IsInstance=false
      IsDispatchSlot=false
      IsOverrideOrExplicitImpl=false
      IsFinal=false }

let OverrideMemberFlags k : SynMemberFlags =
    { MemberKind=k
      IsInstance=true
      IsDispatchSlot=false
      IsOverrideOrExplicitImpl=true
      IsFinal=false }

let AbstractMemberFlags k : SynMemberFlags =
    { MemberKind=k
      IsInstance=true
      IsDispatchSlot=true
      IsOverrideOrExplicitImpl=false
      IsFinal=false }

let StaticMemberFlags k : SynMemberFlags =
    { MemberKind=k
      IsInstance=false
      IsDispatchSlot=false
      IsOverrideOrExplicitImpl=false
      IsFinal=false }

let inferredTyparDecls = SynValTyparDecls(None, true)

let noInferredTypars = SynValTyparDecls(None, false)

let rec synExprContainsError inpExpr =
    let rec walkBind (SynBinding(_, _, _, _, _, _, _, _, _, synExpr, _, _)) = walkExpr synExpr

    and walkExprs es = es |> List.exists walkExpr

    and walkBinds es = es |> List.exists walkBind

    and walkMatchClauses cl =
        cl |> List.exists (fun (SynMatchClause(_, whenExpr, e, _, _)) -> walkExprOpt whenExpr || walkExpr e)

    and walkExprOpt eOpt = eOpt |> Option.exists walkExpr

    and walkExpr e =
          match e with
          | SynExpr.FromParseError _
          | SynExpr.DiscardAfterMissingQualificationAfterDot _
          | SynExpr.ArbitraryAfterError _ -> true

          | SynExpr.LongIdent _
          | SynExpr.Quote _
          | SynExpr.LibraryOnlyILAssembly _
          | SynExpr.LibraryOnlyStaticOptimization _
          | SynExpr.Null _
          | SynExpr.Ident _
          | SynExpr.ImplicitZero _
          | SynExpr.Const _ -> false

          | SynExpr.TypeTest (e, _, _)
          | SynExpr.Upcast (e, _, _)
          | SynExpr.AddressOf (_, e, _, _)
          | SynExpr.CompExpr (_, _, e, _)
          | SynExpr.ArrayOrListOfSeqExpr (_, e, _)
          | SynExpr.Typed (e, _, _)
          | SynExpr.FromParseError (e, _)
          | SynExpr.Do (e, _)
          | SynExpr.Assert (e, _)
          | SynExpr.DotGet (e, _, _, _)
          | SynExpr.LongIdentSet (_, e, _)
          | SynExpr.New (_, _, e, _)
          | SynExpr.TypeApp (e, _, _, _, _, _, _)
          | SynExpr.LibraryOnlyUnionCaseFieldGet (e, _, _, _)
          | SynExpr.Downcast (e, _, _)
          | SynExpr.InferredUpcast (e, _)
          | SynExpr.InferredDowncast (e, _)
          | SynExpr.Lazy (e, _)
          | SynExpr.TraitCall (_, _, e, _)
          | SynExpr.YieldOrReturn (_, e, _)
          | SynExpr.YieldOrReturnFrom (_, e, _)
          | SynExpr.DoBang (e, _)
          | SynExpr.Fixed (e, _)
          | SynExpr.Paren (e, _, _, _) ->
              walkExpr e

          | SynExpr.NamedIndexedPropertySet (_, e1, e2, _)
          | SynExpr.DotSet (e1, _, e2, _)
          | SynExpr.Set (e1, e2, _)
          | SynExpr.LibraryOnlyUnionCaseFieldSet (e1, _, _, e2, _)
          | SynExpr.JoinIn (e1, _, e2, _)
          | SynExpr.App (_, _, e1, e2, _) ->
              walkExpr e1 || walkExpr e2

          | SynExpr.ArrayOrList (_, es, _)
          | SynExpr.Tuple (_, es, _, _) ->
              walkExprs es

          | SynExpr.AnonRecd (_, origExpr, flds, _) ->
              (match origExpr with Some (e, _) -> walkExpr e | None -> false) ||
              walkExprs (List.map snd flds)

          | SynExpr.Record (_, origExpr, fs, _) ->
              (match origExpr with Some (e, _) -> walkExpr e | None -> false) ||
              let flds = fs |> List.choose (fun (_, v, _) -> v)
              walkExprs flds

          | SynExpr.ObjExpr (_, _, bs, is, _, _) ->
              walkBinds bs || walkBinds [ for (SynInterfaceImpl(_, bs, _)) in is do yield! bs  ]

          | SynExpr.ForEach (_, _, _, _, e1, e2, _)
          | SynExpr.While (_, e1, e2, _) ->
              walkExpr e1 || walkExpr e2

          | SynExpr.For (_, _, e1, _, e2, e3, _) ->
              walkExpr e1 || walkExpr e2 || walkExpr e3

          | SynExpr.MatchLambda (_, _, cl, _, _) ->
              walkMatchClauses cl

          | SynExpr.Lambda (_, _, _, e, _, _) ->
              walkExpr e

          | SynExpr.Match (_, e, cl, _) ->
              walkExpr e || walkMatchClauses cl

          | SynExpr.LetOrUse (_, _, bs, e, _) ->
              walkBinds bs || walkExpr e

          | SynExpr.TryWith (e, _, cl, _, _, _, _) ->
              walkExpr e  || walkMatchClauses cl

          | SynExpr.TryFinally (e1, e2, _, _, _) ->
              walkExpr e1 || walkExpr e2

          | SynExpr.Sequential (_, _, e1, e2, _) ->
              walkExpr e1 || walkExpr e2

          | SynExpr.SequentialOrImplicitYield (_, e1, e2, _, _) ->
              walkExpr e1 || walkExpr e2

          | SynExpr.IfThenElse (_, e1, _, e2, _, e3opt, _, _, _, _) ->
              walkExpr e1 || walkExpr e2 || walkExprOpt e3opt

          | SynExpr.DotIndexedGet (e1, es, _, _) ->
              walkExpr e1 || walkExprs [ for e in es do yield! e.Exprs ]

          | SynExpr.DotIndexedSet (e1, es, e2, _, _, _) ->
              walkExpr e1 || walkExprs [ for e in es do yield! e.Exprs ] || walkExpr e2

          | SynExpr.DotNamedIndexedPropertySet (e1, _, e2, e3, _) ->
              walkExpr e1 || walkExpr e2 || walkExpr e3

          | SynExpr.MatchBang (_, e, cl, _) ->
              walkExpr e || walkMatchClauses cl

          | SynExpr.LetOrUseBang  (rhs=e1;body=e2;andBangs=es) ->
              walkExpr e1 || walkExprs [ for (_,_,_,_,e,_) in es do yield e ] || walkExpr e2

          | SynExpr.InterpolatedString (parts, _, _m) ->
              walkExprs 
                  (parts |> List.choose (function 
                      | SynInterpolatedStringPart.String _ -> None
                      | SynInterpolatedStringPart.FillExpr (x, _) -> Some x))

    walkExpr inpExpr

let (|ParsedHashDirectiveArguments|) (input: ParsedHashDirectiveArgument list) =
    List.map
        (function
        | ParsedHashDirectiveArgument.String (s, _, _) -> s
        | ParsedHashDirectiveArgument.SourceIdentifier (_, v, _) -> v)
        input