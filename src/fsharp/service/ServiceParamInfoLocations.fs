// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.SourceCodeServices

open FSharp.Compiler.SyntaxTree
open FSharp.Compiler.SyntaxTreeOps
open FSharp.Compiler.Range

type TupledArgumentLocation = { IsNamedArgument: bool; ArgumentRange: range }

[<Sealed>]
type FSharpNoteworthyParamInfoLocations
    (
        longId: string list,
        longIdRange: range,
        openParenLocation: pos,
        argRanges: TupledArgumentLocation list, 
        tupleEndLocations: pos list,
        isThereACloseParen: bool,
        namedParamNames: string option list
    ) =

    let tupleEndLocations = Array.ofList tupleEndLocations
    let namedParamNames = Array.ofList namedParamNames
    let namedParamNames =
        if (tupleEndLocations.Length = namedParamNames.Length) then
            namedParamNames
        else
            // in cases like    TP<   or   TP<42,   there is no 'arbitrary type' that represents the last missing static argument
            // this is ok, but later code in the UI layer will expect these lengths to match
            // so just fill in a blank named param to represent the final missing param
            // (compare to    f(   or   f(42,   where the parser injects a fake "AbrExpr" to represent the missing argument)
            assert(tupleEndLocations.Length = namedParamNames.Length + 1)
            [| yield! namedParamNames; yield None |]  // None is representation of a non-named param
    member this.LongId = longId
    member this.LongIdStartLocation = longIdRange.Start
    member this.LongIdEndLocation = longIdRange.End
    member this.OpenParenLocation = openParenLocation
    member this.TupleEndLocations = tupleEndLocations
    member this.IsThereACloseParen = isThereACloseParen
    member this.NamedParamNames = namedParamNames
    member this.ArgumentLocations = argRanges |> Array.ofList

[<AutoOpen>]
module internal NoteworthyParamInfoLocationsImpl =

    let isStaticArg (StripParenTypes synType) =
        match synType with
        | SynType.StaticConstant _ | SynType.StaticConstantExpr _ | SynType.StaticConstantNamed _ -> true
        | SynType.LongIdent _ -> true // NOTE: this is not a static constant, but it is a prefix of incomplete code, e.g. "TP<42, Arg3" is a prefix of "TP<42, Arg3=6>" and Arg3 shows up as a LongId
        | _ -> false

    /// Dig out an identifier from an expression that used in an application
    let rec digOutIdentFromFuncExpr synExpr =
        // we found it, dig out ident
        match synExpr with
        | SynExpr.Ident (id) -> Some ([id.idText], id.idRange)
        | SynExpr.LongIdent (_, LongIdentWithDots(lid, _), _, lidRange) 
        | SynExpr.DotGet (_, _, LongIdentWithDots(lid, _), lidRange) -> Some (pathOfLid lid, lidRange)
        | SynExpr.TypeApp (synExpr, _, _synTypeList, _commas, _, _, _range) -> digOutIdentFromFuncExpr synExpr 
        | SynExpr.Paren(expr = expr) -> digOutIdentFromFuncExpr expr 
        | _ -> None

    type FindResult = 
        | Found of openParen: pos * argRanges: TupledArgumentLocation list * commasAndCloseParen: (pos * string option) list * hasClosedParen: bool
        | NotFound

    let digOutIdentFromStaticArg (StripParenTypes synType) =
        match synType with 
        | SynType.StaticConstantNamed(SynType.LongIdent(LongIdentWithDots([id], _)), _, _) -> Some id.idText 
        | SynType.LongIdent(LongIdentWithDots([id], _)) -> Some id.idText // NOTE: again, not a static constant, but may be a prefix of a Named in incomplete code
        | _ -> None

    let getNamedParamName e =
        match e with
        // f(x=4)
        | SynExpr.App (ExprAtomicFlag.NonAtomic, _, 
                        SynExpr.App (ExprAtomicFlag.NonAtomic, true, 
                                    SynExpr.Ident op, 
                                    SynExpr.Ident n, 
                                    _range), 
                        _, _) when op.idText="op_Equality" -> Some n.idText
        // f(?x=4)
        | SynExpr.App (ExprAtomicFlag.NonAtomic, _, 
                        SynExpr.App (ExprAtomicFlag.NonAtomic, true, 
                                    SynExpr.Ident op, 
                                    SynExpr.LongIdent (true(*isOptional*), LongIdentWithDots([n], _), _ref, _lidrange), _range), 
                        _, _) when op.idText="op_Equality" -> Some n.idText
        | _ -> None

    let getTypeName(synType) =
        match synType with
        | SynType.LongIdent(LongIdentWithDots(ids, _)) -> ids |> pathOfLid
        | _ -> [""] // TODO type name for other cases, see also unit test named "ParameterInfo.LocationOfParams.AfterQuicklyTyping.CallConstructorViaLongId.Bug94333"

    let handleSingleArg traverseSynExpr (pos, synExpr, parenRange, rpRangeOpt : _ option) =
        let inner = traverseSynExpr synExpr
        match inner with
        | None ->
            if AstTraversal.rangeContainsPosLeftEdgeExclusiveAndRightEdgeInclusive parenRange pos then
                let argRanges = [{ IsNamedArgument = (getNamedParamName synExpr).IsSome; ArgumentRange = synExpr.Range }]
                Found (parenRange.Start, argRanges, [(parenRange.End, getNamedParamName synExpr)], rpRangeOpt.IsSome), None
            else
                NotFound, None
        | _ -> NotFound, None

    // This method returns a tuple, where the second element is
    //     Some(cache)    if the implementation called 'traverseSynExpr expr', then 'cache' is the result of that call
    //     None           otherwise
    // so that callers can avoid recomputing 'traverseSynExpr expr' if it's already been done.  This is very important for perf, 
    // see bug 345385.
    let rec searchSynArgExpr traverseSynExpr pos expr =
        match expr with 
        | SynExprParen((SynExpr.Tuple (false, synExprList, commaRanges, _tupleRange) as synExpr), _lpRange, rpRangeOpt, parenRange) -> // tuple argument
            let inner = traverseSynExpr synExpr
            match inner with
            | None ->
                if AstTraversal.rangeContainsPosLeftEdgeExclusiveAndRightEdgeInclusive parenRange pos then
                    // argRange, isNamed
                    let argRanges =
                        synExprList
                        |> List.map (fun e -> { IsNamedArgument = (getNamedParamName e).IsSome; ArgumentRange = e.Range })
                    let commasAndCloseParen = ((synExprList, commaRanges@[parenRange]) ||> List.map2 (fun e c -> c.End, getNamedParamName e))
                    let r = Found (parenRange.Start, argRanges, commasAndCloseParen, rpRangeOpt.IsSome)
                    r, None
                else
                    NotFound, None
            | _ -> NotFound, None

        | SynExprParen(SynExprParen(SynExpr.Tuple (false, _, _, _), _, _, _) as synExpr, _, rpRangeOpt, parenRange) -> // f((x, y)) is special, single tuple arg
            handleSingleArg traverseSynExpr (pos, synExpr, parenRange, rpRangeOpt)

        // dig into multiple parens
        | SynExprParen(SynExprParen(_, _, _, _) as synExpr, _, _, _parenRange) -> 
            let r, _cacheOpt = searchSynArgExpr traverseSynExpr pos synExpr
            r, None

        | SynExprParen(synExpr, _lpRange, rpRangeOpt, parenRange) -> // single argument
            handleSingleArg traverseSynExpr (pos, synExpr, parenRange, rpRangeOpt)

        | SynExpr.ArbitraryAfterError (_debugStr, range) -> // single argument when e.g. after open paren you hit EOF
            if AstTraversal.rangeContainsPosEdgesExclusive range pos then
                let r = Found (range.Start, [], [(range.End, None)], false)
                r, None
            else
                NotFound, None

        | SynExpr.Const (SynConst.Unit, unitRange) ->
            if AstTraversal.rangeContainsPosEdgesExclusive unitRange pos then
                let r = Found (unitRange.Start, [], [(unitRange.End, None)], true)
                r, None
            else
                NotFound, None

        | e -> 
            let inner = traverseSynExpr e
            match inner with
            | None ->
                if AstTraversal.rangeContainsPosEdgesExclusive e.Range pos then
                    // any other expression doesn't start with parens, so if it was the target of an App, then it must be a single argument e.g. "f x"
                    Found (e.Range.Start, [], [ (e.Range.End, None) ], false), Some inner
                else
                    NotFound, Some inner
            | _ -> NotFound, Some inner

    let (|StaticParameters|_|) pos (StripParenTypes synType) =
        match synType with
        | SynType.App(StripParenTypes (SynType.LongIdent(LongIdentWithDots(lid, _) as lidwd)), Some(openm), args, commas, closemOpt, _pf, wholem) ->
            let lidm = lidwd.Range
            let betweenTheBrackets = mkRange wholem.FileName openm.Start wholem.End
            if AstTraversal.rangeContainsPosEdgesExclusive betweenTheBrackets pos && args |> List.forall isStaticArg then
                let commasAndCloseParen = [ for c in commas -> c.End ] @ [ wholem.End ]
                Some (FSharpNoteworthyParamInfoLocations(pathOfLid lid, lidm, openm.Start, [], commasAndCloseParen, closemOpt.IsSome, args |> List.map digOutIdentFromStaticArg))
            else
                None
        | _ ->
            None

    let traverseInput(pos, parseTree) =
        AstTraversal.Traverse(pos, parseTree, { new AstTraversal.AstVisitorBase<_>() with
        member this.VisitExpr(_path, traverseSynExpr, defaultTraverse, expr) =
            let expr = expr // fix debug locals
            match expr with

            // new LID<tyarg1, ...., tyargN>(...)  and error recovery of these
            | SynExpr.New (_, synType, synExpr, _) -> 
                let constrArgsResult, cacheOpt = searchSynArgExpr traverseSynExpr pos synExpr
                match constrArgsResult, cacheOpt with
                | Found(parenLoc, argRanges, commasAndCloseParen, isThereACloseParen), _ ->
                    let typeName = getTypeName synType
                    Some (FSharpNoteworthyParamInfoLocations(typeName, synType.Range, parenLoc, argRanges, commasAndCloseParen |> List.map fst, isThereACloseParen, commasAndCloseParen |> List.map snd))
                | NotFound, Some cache ->
                    cache
                | _ ->
                    match synType with
                    | StaticParameters pos loc -> Some loc
                    | _ -> traverseSynExpr synExpr

            // EXPR<  = error recovery of a form of half-written TypeApp
            | SynExpr.App (_, _, SynExpr.App (_, true, SynExpr.Ident op, synExpr, openm), SynExpr.ArbitraryAfterError _, wholem) when op.idText = "op_LessThan" ->
                // Look in the function expression
                let fResult = traverseSynExpr synExpr
                match fResult with
                | Some _ -> fResult
                | _ ->
                    let typeArgsm = mkRange openm.FileName openm.Start wholem.End 
                    if AstTraversal.rangeContainsPosEdgesExclusive typeArgsm pos then
                        // We found it, dig out ident
                        match digOutIdentFromFuncExpr synExpr with
                        | Some(lid, lidRange) -> Some (FSharpNoteworthyParamInfoLocations(lid, lidRange, op.idRange.Start, [], [ wholem.End ], false, []))
                        | None -> None
                    else
                        None

            // EXPR EXPR2
            | SynExpr.App (_exprAtomicFlag, isInfix, synExpr, synExpr2, _range) ->
                // Look in the function expression
                let fResult = traverseSynExpr synExpr
                match fResult with
                | Some _ -> fResult
                | _ ->
                    // Search the argument
                    let xResult, cacheOpt = searchSynArgExpr traverseSynExpr pos synExpr2
                    match xResult, cacheOpt with
                    | Found(parenLoc, argRanges, commasAndCloseParen, isThereACloseParen), _ ->
                        // We found it, dig out ident
                        match digOutIdentFromFuncExpr synExpr with
                        | Some(lid, lidRange) -> 
                            assert(isInfix = (posLt parenLoc lidRange.End))
                            if isInfix then
                                // This seems to be an infix operator, since the start of the argument is a position earlier than the end of the long-id being applied to it.
                                // For now, we don't support infix operators.
                                None
                            else
                                Some (FSharpNoteworthyParamInfoLocations(lid, lidRange, parenLoc, argRanges, commasAndCloseParen |> List.map fst, isThereACloseParen, commasAndCloseParen |> List.map snd))
                        | None -> None
                    | NotFound, Some cache -> cache
                    | _ -> traverseSynExpr synExpr2

            // ID<tyarg1, ...., tyargN>  and error recovery of these
            | SynExpr.TypeApp (synExpr, openm, tyArgs, commas, closemOpt, _, wholem) ->
                match traverseSynExpr synExpr with
                | Some _ as r -> r
                | None -> 
                    let typeArgsm = mkRange openm.FileName openm.Start wholem.End 
                    if AstTraversal.rangeContainsPosEdgesExclusive typeArgsm pos && tyArgs |> List.forall isStaticArg then
                        let commasAndCloseParen = [ for c in commas -> c.End ] @ [ wholem.End ]
                        let argRanges = tyArgs |> List.map (fun tyarg -> { IsNamedArgument = false; ArgumentRange = tyarg.Range })
                        let r = FSharpNoteworthyParamInfoLocations(["dummy"], synExpr.Range, openm.Start, argRanges, commasAndCloseParen, closemOpt.IsSome, tyArgs |> List.map digOutIdentFromStaticArg)
                        Some r
                    else
                        None

            | _ -> defaultTraverse expr

        member this.VisitTypeAbbrev(tyAbbrevRhs, _m) = 
            match tyAbbrevRhs with
            | StaticParameters pos loc -> Some loc
            | _ -> None

        member this.VisitImplicitInherit(defaultTraverse, ty, expr, m) =
            match defaultTraverse expr with
            | Some _ as r -> r
            | None ->
                let inheritm = mkRange m.FileName m.Start m.End 
                if AstTraversal.rangeContainsPosEdgesExclusive inheritm pos then
                    // inherit ty(expr)    ---   treat it like an application (constructor call)
                    let xResult, _cacheOpt = searchSynArgExpr defaultTraverse pos expr
                    match xResult with
                    | Found(parenLoc, argRanges, commasAndCloseParen, isThereACloseParen) ->
                        // we found it, dig out ident
                        let typeName = getTypeName ty
                        let r = FSharpNoteworthyParamInfoLocations(typeName, ty.Range, parenLoc, argRanges, commasAndCloseParen |> List.map fst, isThereACloseParen, commasAndCloseParen |> List.map snd)
                        Some r
                    | NotFound -> None
                else None
        })

type FSharpNoteworthyParamInfoLocations with 
    static member Find(pos, parseTree) =
        match traverseInput(pos, parseTree) with
        | Some nwpl as r -> 
#if DEBUG
            let ranges = nwpl.LongIdStartLocation :: nwpl.LongIdEndLocation :: nwpl.OpenParenLocation :: (nwpl.TupleEndLocations |> Array.toList)
            let sorted = ranges |> List.sortWith (fun a b -> posOrder.Compare(a, b)) |> Seq.toList
            assert(ranges = sorted)
#else
            ignore nwpl
#endif                        
            r
        | _ -> None

module internal SynExprAppLocationsImpl =
    let rec private searchSynArgExpr traverseSynExpr expr ranges =
        match expr with
        | SynExpr.Const(SynConst.Unit, _) ->
            None, None

        | SynExprParen(SynExpr.Tuple (_, exprs, _commas, _tupRange), _, _, _parenRange) ->
            let rec loop (exprs: SynExpr list) ranges =
                match exprs with
                | [] -> ranges
                | h::t ->
                    loop t (h.Range :: ranges)

            let res = loop exprs ranges
            Some (res), None

        | SynExprParen(SynExprParen(_, _, _, _) as synExpr, _, _, _parenRange) -> 
            let r, _cacheOpt = searchSynArgExpr traverseSynExpr synExpr ranges
            r, None

        | SynExprParen(SynExpr.App (_, _isInfix, _, _, _range), _, _, parenRange) ->
            Some (parenRange :: ranges), None

        | e -> 
            let inner = traverseSynExpr e
            match inner with
            | None ->
                Some (e.Range :: ranges), Some inner
            | _ -> None, Some inner

    let getAllCurriedArgsAtPosition pos parseTree =
        AstTraversal.Traverse(pos, parseTree, { new AstTraversal.AstVisitorBase<_>() with
            member _.VisitExpr(_path, traverseSynExpr, defaultTraverse, expr) =
                match expr with
                | SynExpr.App (_exprAtomicFlag, _isInfix, funcExpr, argExpr, range) when posEq pos range.Start ->
                    let isInfixFuncExpr =
                        match funcExpr with
                        | SynExpr.App (_, isInfix, _, _, _) -> isInfix
                        | _ -> false

                    if isInfixFuncExpr then
                        traverseSynExpr funcExpr
                    else
                        let workingRanges =
                            match traverseSynExpr funcExpr with
                            | Some ranges -> ranges
                            | None -> []

                        let xResult, cacheOpt = searchSynArgExpr traverseSynExpr argExpr workingRanges
                        match xResult, cacheOpt with
                        | Some ranges, _ -> Some ranges
                        | None, Some cache -> cache
                        | _ -> traverseSynExpr argExpr
                | _ -> defaultTraverse expr })
        |> Option.map List.rev
