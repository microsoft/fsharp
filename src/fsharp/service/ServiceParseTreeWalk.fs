// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

//----------------------------------------------------------------------------
// Open up the compiler as an incremental service for parsing,
// type checking and intellisense-like environment-reporting.
//--------------------------------------------------------------------------

namespace FSharp.Compiler.Syntax

open FSharp.Compiler
open FSharp.Compiler.Syntax
open FSharp.Compiler.SyntaxTreeOps
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Position
open FSharp.Compiler.Text.Range

/// used to track route during traversal AST
[<RequireQualifiedAccess>]
type SyntaxNode = 
    | SynPat of SynPat
    | SynType of SynType
    | SynExpr of SynExpr
    | SynModule of SynModuleDecl
    | SynModuleOrNamespace of SynModuleOrNamespace
    | SynTypeDefn of SynTypeDefn
    | SynMemberDefn of SynMemberDefn
    | SynMatchClause of SynMatchClause
    | SynBinding of SynBinding

type SyntaxVisitorPath = SyntaxNode list

[<AbstractClass>]
type SyntaxVisitorBase<'T>() =
    abstract VisitExpr: path: SyntaxVisitorPath * traverseSynExpr: (SynExpr -> 'T option) * defaultTraverse: (SynExpr -> 'T option) * synExpr: SynExpr -> 'T option
    default _.VisitExpr(path: SyntaxVisitorPath, traverseSynExpr: (SynExpr -> 'T option), defaultTraverse: (SynExpr -> 'T option), synExpr: SynExpr) =
        ignore (path, traverseSynExpr, defaultTraverse, synExpr)
        None

    /// VisitTypeAbbrev(ty,m), defaults to ignoring this leaf of the AST
    abstract VisitTypeAbbrev: path: SyntaxVisitorPath * synType: SynType * range: range -> 'T option
    default _.VisitTypeAbbrev(path, synType, range) =
        ignore (path, synType, range)
        None

    /// VisitImplicitInherit(defaultTraverse,ty,expr,m), defaults to just visiting expr
    abstract VisitImplicitInherit: path: SyntaxVisitorPath * defaultTraverse: (SynExpr -> 'T option) * inheritedType: SynType * synArgs: SynExpr * range: range -> 'T option
    default _.VisitImplicitInherit(path, defaultTraverse, inheritedType, synArgs, range) =
        ignore (path, inheritedType, range)
        defaultTraverse synArgs

    /// VisitModuleDecl allows overriding module declaration behavior
    abstract VisitModuleDecl: path: SyntaxVisitorPath * defaultTraverse: (SynModuleDecl -> 'T option) * synModuleDecl: SynModuleDecl -> 'T option
    default _.VisitModuleDecl(path, defaultTraverse, synModuleDecl) =
        ignore path
        defaultTraverse synModuleDecl

    /// VisitBinding allows overriding binding behavior (note: by default it would defaultTraverse expression)
    abstract VisitBinding: path: SyntaxVisitorPath * defaultTraverse: (SynBinding -> 'T option) * synBinding: SynBinding -> 'T option
    default _.VisitBinding(path, defaultTraverse, synBinding) =
        ignore path
        defaultTraverse synBinding

    /// VisitMatchClause allows overriding clause behavior (note: by default it would defaultTraverse expression)
    abstract VisitMatchClause: path: SyntaxVisitorPath * defaultTraverse: (SynMatchClause -> 'T option) * matchClause: SynMatchClause -> 'T option
    default _.VisitMatchClause(path, defaultTraverse, matchClause) =
        ignore path
        defaultTraverse matchClause

    /// VisitInheritSynMemberDefn allows overriding inherit behavior (by default do nothing)
    abstract VisitInheritSynMemberDefn: path: SyntaxVisitorPath * componentInfo: SynComponentInfo * typeDefnKind: SynTypeDefnKind * SynType  * SynMemberDefns * range -> 'T option
    default _.VisitInheritSynMemberDefn(path, componentInfo, typeDefnKind, synType, members, range) =
        ignore (path, componentInfo, typeDefnKind, synType, members, range)
        None

    /// VisitInterfaceSynMemberDefnType allows overriding behavior for visiting interface member in types (by default - do nothing)
    abstract VisitInterfaceSynMemberDefnType: path: SyntaxVisitorPath * synType: SynType -> 'T option
    default _.VisitInterfaceSynMemberDefnType(path, synType) =
        ignore (path, synType)
        None

    /// VisitRecordField allows overriding behavior when visiting l.h.s. of constructed record instances
    abstract VisitRecordField: path: SyntaxVisitorPath * copyOpt: SynExpr option * recordField: LongIdentWithDots option -> 'T option
    default _.VisitRecordField (path, copyOpt, recordField) =
        ignore (path, copyOpt, recordField)
        None

    /// VisitHashDirective allows overriding behavior when visiting hash directives in FSX scripts, like #r, #load and #I.
    abstract VisitHashDirective: path: SyntaxVisitorPath * hashDirective: ParsedHashDirective * range: range -> 'T option
    default _.VisitHashDirective (path, hashDirective, range) =
        ignore (path, hashDirective, range)
        None

    /// VisitModuleOrNamespace allows overriding behavior when visiting module or namespaces
    abstract VisitModuleOrNamespace: path: SyntaxVisitorPath * synModuleOrNamespace: SynModuleOrNamespace -> 'T option
    default _.VisitModuleOrNamespace (path, synModuleOrNamespace) =
        ignore (path, synModuleOrNamespace)
        None

    /// VisitComponentInfo allows overriding behavior when visiting type component infos 
    abstract VisitComponentInfo: path: SyntaxVisitorPath * synComponentInfo: SynComponentInfo -> 'T option
    default _.VisitComponentInfo (path, synComponentInfo) =
        ignore (path, synComponentInfo)
        None

    /// VisitLetOrUse allows overriding behavior when visiting module or local let or use bindings
    abstract VisitLetOrUse: path: SyntaxVisitorPath * isRecursive: bool * defaultTraverse: (SynBinding -> 'T option) * bindings: SynBinding list * range: range -> 'T option
    default _.VisitLetOrUse (path, isRecursive, defaultTraverse, bindings, range) =
        ignore (path, isRecursive, defaultTraverse, bindings, range)
        None

    /// VisitType allows overriding behavior when visiting simple pats
    abstract VisitSimplePats: path: SyntaxVisitorPath * synPats: SynSimplePat list -> 'T option
    default _.VisitSimplePats (path, synPats) =
        ignore (path, synPats)
        None

    /// VisitPat allows overriding behavior when visiting patterns
    abstract VisitPat: path: SyntaxVisitorPath * defaultTraverse: (SynPat -> 'T option) * synPat: SynPat -> 'T option
    default _.VisitPat (path, defaultTraverse, synPat) =
        ignore path
        defaultTraverse synPat

    /// VisitType allows overriding behavior when visiting type hints (x: ..., etc.)
    abstract VisitType: path: SyntaxVisitorPath * defaultTraverse: (SynType -> 'T option) * synType: SynType -> 'T option
    default _.VisitType (path, defaultTraverse, synType) =
        ignore path
        defaultTraverse synType

/// A range of utility functions to assist with traversing an AST
module SyntaxTraversal =

    // treat ranges as though they are half-open: [,)
    let rangeContainsPosLeftEdgeInclusive (m1:range) p =
        if posEq m1.Start m1.End then
            // the parser doesn't produce zero-width ranges, except in one case, for e.g. a block of lets that lacks a body
            // we treat the range [n,n) as containing position n
            posGeq p m1.Start &&
            posGeq m1.End p
        else
            posGeq p m1.Start &&   // [
            posGt m1.End p         // )

    // treat ranges as though they are fully open: (,)
    let rangeContainsPosEdgesExclusive (m1:range) p = posGt p m1.Start && posGt m1.End p

    let rangeContainsPosLeftEdgeExclusiveAndRightEdgeInclusive (m1:range) p = posGt p m1.Start && posGeq m1.End p

    let dive node range project =
        range,(fun() -> project node)

    let pick pos (outerRange:range) (debugObj:obj) (diveResults:list<range*_>) =
        match diveResults with
        | [] -> None
        | _ ->
        let isOrdered =
#if DEBUG
            // ranges in a dive-and-pick group should be ordered
            diveResults |> Seq.pairwise |> Seq.forall (fun ((r1,_),(r2,_)) -> posGeq r2.Start r1.End)
#else
            true
#endif
        if not isOrdered then
            let s = sprintf "ServiceParseTreeWalk: not isOrdered: %A" (diveResults |> List.map (fun (r,_) -> r.ToShortString()))
            ignore s
            //System.Diagnostics.Debug.Assert(false, s)
        let outerContainsInner =
#if DEBUG
            // ranges in a dive-and-pick group should be "under" the thing that contains them
            let innerTotalRange = diveResults |> List.map fst  |> List.reduce unionRanges
            rangeContainsRange outerRange innerTotalRange
#else
            ignore(outerRange)
            true
#endif
        if not outerContainsInner then
            let s = sprintf "ServiceParseTreeWalk: not outerContainsInner: %A : %A" (outerRange.ToShortString()) (diveResults |> List.map (fun (r,_) -> r.ToShortString()))
            ignore s
            //System.Diagnostics.Debug.Assert(false, s)
        let isZeroWidth(r:range) =
            posEq r.Start r.End // the parser inserts some zero-width elements to represent the completions of incomplete constructs, but we should never 'dive' into them, since they don't represent actual user code
        match List.choose (fun (r,f) -> if rangeContainsPosLeftEdgeInclusive r pos && not(isZeroWidth r) then Some(f) else None) diveResults with 
        | [] -> 
            // No entity's range contained the desired position.  However the ranges in the parse tree only span actual characters present in the file.  
            // The cursor may be at whitespace between entities or after everything, so find the nearest entity with the range left of the position.
            let mutable e = diveResults.Head
            for r in diveResults do
                if posGt pos (fst r).Start then
                    e <- r
            snd(e)()
        | [x] -> x()
        | _ -> 
#if DEBUG
            assert(false)
            failwithf "multiple disjoint AST node ranges claimed to contain (%A) from %+A" pos debugObj
#else
            ignore debugObj
            None
#endif

    /// traverse an implementation file walking all the way down to SynExpr or TypeAbbrev at a particular location
    ///
    let Traverse(pos:pos, parseTree, visitor:SyntaxVisitorBase<'T>) =
        let pick x = pick pos x
        let rec traverseSynModuleDecl origPath (decl:SynModuleDecl) =
            let pick = pick decl.Range
            let defaultTraverse m = 
                let path = SyntaxNode.SynModule m :: origPath
                match m with
                | SynModuleDecl.ModuleAbbrev(_ident, _longIdent, _range) -> None
                | SynModuleDecl.NestedModule(_synComponentInfo, _isRec, synModuleDecls, _, _range) -> synModuleDecls |> List.map (fun x -> dive x x.Range (traverseSynModuleDecl path)) |> pick decl
                | SynModuleDecl.Let(isRecursive, synBindingList, range) ->
                    match visitor.VisitLetOrUse(path, isRecursive, traverseSynBinding path, synBindingList, range) with
                    | Some x -> Some x
                    | None -> synBindingList |> List.map (fun x -> dive x x.RangeOfBindingWithRhs (traverseSynBinding path)) |> pick decl
                | SynModuleDecl.DoExpr(_sequencePointInfoForBinding, synExpr, _range) -> traverseSynExpr path synExpr  
                | SynModuleDecl.Types(synTypeDefnList, _range) -> synTypeDefnList |> List.map (fun x -> dive x x.Range (traverseSynTypeDefn path)) |> pick decl
                | SynModuleDecl.Exception(_synExceptionDefn, _range) -> None
                | SynModuleDecl.Open(_target, _range) -> None
                | SynModuleDecl.Attributes(_synAttributes, _range) -> None
                | SynModuleDecl.HashDirective(parsedHashDirective, range) -> visitor.VisitHashDirective (path, parsedHashDirective, range)
                | SynModuleDecl.NamespaceFragment(synModuleOrNamespace) -> traverseSynModuleOrNamespace path synModuleOrNamespace
            visitor.VisitModuleDecl(origPath, defaultTraverse, decl)

        and traverseSynModuleOrNamespace origPath (SynModuleOrNamespace(_longIdent, _isRec, _isModule, synModuleDecls, _preXmlDoc, _synAttributes, _synAccessOpt, range) as mors) =
            match visitor.VisitModuleOrNamespace(origPath, mors) with
            | Some x -> Some x
            | None ->
                let path = SyntaxNode.SynModuleOrNamespace mors :: origPath
                synModuleDecls |> List.map (fun x -> dive x x.Range (traverseSynModuleDecl path)) |> pick range mors

        and traverseSynExpr origPath (expr:SynExpr) =
            let pick = pick expr.Range
            let defaultTraverse e = 
                let path = SyntaxNode.SynExpr e :: origPath
                let traverseSynExpr = traverseSynExpr path
                let traverseSynType = traverseSynType path
                let traversePat = traversePat path
                match e with

                | SynExpr.Paren (synExpr, _, _, _parenRange) -> traverseSynExpr synExpr

                | SynExpr.Quote (_synExpr, _, synExpr2, _, _range) -> 
                    [//dive synExpr synExpr.Range traverseSynExpr // TODO, what is this?
                     dive synExpr2 synExpr2.Range traverseSynExpr]
                    |> pick expr

                | SynExpr.Const (_synConst, _range) -> None

                | SynExpr.InterpolatedString (parts, _, _) -> 
                    [ for part in parts do
                          match part with
                          | SynInterpolatedStringPart.String _ -> ()
                          | SynInterpolatedStringPart.FillExpr (fillExpr, _) ->
                              yield dive fillExpr fillExpr.Range traverseSynExpr ]
                    |> pick expr

                | SynExpr.Typed (synExpr, synType, _range) ->
                    [ traverseSynExpr synExpr; traverseSynType synType ] |> List.tryPick id

                | SynExpr.Tuple (_, synExprList, _, _range) 
                | SynExpr.ArrayOrList (_, synExprList, _range) ->
                    synExprList |> List.map (fun x -> dive x x.Range traverseSynExpr) |> pick expr
                
                | SynExpr.AnonRecd (_isStruct, copyOpt, synExprList, _range) -> 
                    [   match copyOpt with
                        | Some(expr, (withRange, _)) -> 
                            yield dive expr expr.Range traverseSynExpr 
                            yield dive () withRange (fun () ->
                                if posGeq pos withRange.End then 
                                    // special case: caret is after WITH
                                    // { x with $ }
                                    visitor.VisitRecordField (path, Some expr, None) 
                                else
                                    None
                            )
                        | _ -> ()
                        for (_,x) in synExprList do 
                            yield dive x x.Range traverseSynExpr
                    ] |> pick expr

                | SynExpr.Record (inheritOpt,copyOpt,fields, _range) -> 
                    [ 
                        let diveIntoSeparator offsideColumn scPosOpt copyOpt  = 
                            match scPosOpt with
                            | Some scPos -> 
                                if posGeq pos scPos then 
                                    visitor.VisitRecordField(path, copyOpt, None) // empty field after the inherits
                                else None
                            | None -> 
                                //semicolon position is not available - use offside rule
                                if pos.Column = offsideColumn then
                                    visitor.VisitRecordField(path, copyOpt, None) // empty field after the inherits
                                else None

                        match inheritOpt with
                        | Some(_ty,expr, _range, sepOpt, inheritRange) -> 
                            // dive into argument
                            yield dive expr expr.Range (fun expr ->
                                // special-case:caret is located in the offside position below inherit 
                                // inherit A()
                                // $
                                if not (rangeContainsPos expr.Range pos) && sepOpt.IsNone && pos.Column = inheritRange.StartColumn then
                                    visitor.VisitRecordField(path, None, None)
                                else
                                    traverseSynExpr expr
                                )
                            match sepOpt with
                            | Some (sep, scPosOpt) ->
                                yield dive () sep (fun () ->
                                    // special case: caret is below 'inherit' + one or more fields are already defined
                                    // inherit A()
                                    // $
                                    // field1 = 5
                                    diveIntoSeparator inheritRange.StartColumn scPosOpt None
                                    )
                            | None -> ()
                        | _ -> ()
                        match copyOpt with
                        | Some(expr, (withRange, _)) -> 
                            yield dive expr expr.Range traverseSynExpr 
                            yield dive () withRange (fun () ->
                                if posGeq pos withRange.End then 
                                    // special case: caret is after WITH
                                    // { x with $ }
                                    visitor.VisitRecordField (path, Some expr, None) 
                                else
                                    None
                            )
                        | _ -> ()
                        let copyOpt = Option.map fst copyOpt
                        for (field, _), e, sepOpt in fields do
                            yield dive (path, copyOpt, Some field) field.Range (fun r -> 
                                if rangeContainsPos field.Range pos then
                                    visitor.VisitRecordField r
                                else 
                                    None
                                )
                            let offsideColumn = 
                                match inheritOpt with
                                | Some(_,_, _, _, inheritRange) -> inheritRange.StartColumn
                                | None -> field.Range.StartColumn

                            match e with
                            | Some e -> yield dive e e.Range (fun expr ->
                                // special case: caret is below field binding
                                // field x = 5
                                // $
                                if not (rangeContainsPos e.Range pos) && sepOpt.IsNone && pos.Column = offsideColumn then
                                    visitor.VisitRecordField(path, copyOpt, None)
                                else
                                    traverseSynExpr expr
                                )
                            | None -> ()

                            match sepOpt with
                            | Some (sep, scPosOpt) -> 
                                yield dive () sep (fun () -> 
                                    // special case: caret is between field bindings
                                    // field1 = 5
                                    // $
                                    // field2 = 5
                                    diveIntoSeparator offsideColumn scPosOpt copyOpt
                                    )
                            | _ -> ()

                    ] |> pick expr

                | SynExpr.New (_, _synType, synExpr, _range) -> traverseSynExpr synExpr
                | SynExpr.ObjExpr (ty,baseCallOpt,binds,ifaces,_range1,_range2) -> 
                    let result = 
                        ifaces 
                        |> Seq.map (fun (SynInterfaceImpl(ty, _, _)) -> ty)
                        |> Seq.tryPick (fun ty -> visitor.VisitInterfaceSynMemberDefnType(path, ty))
                    
                    if result.IsSome then 
                        result
                    else
                    [
                        match baseCallOpt with
                        | Some(expr,_) -> 
                            // this is like a call to 'new', so mock up a 'new' so we can recurse and use that existing logic
                            let newCall = SynExpr.New (false, ty, expr, unionRanges ty.Range expr.Range)
                            yield dive newCall newCall.Range traverseSynExpr
                        | _ -> ()
                        for b in binds do
                            yield dive b b.RangeOfBindingWithRhs (traverseSynBinding path)
                        for SynInterfaceImpl(_ty, binds, _range) in ifaces do
                            for b in binds do
                                yield dive b b.RangeOfBindingWithRhs (traverseSynBinding path)
                    ] |> pick expr

                | SynExpr.While (_sequencePointInfoForWhileLoop, synExpr, synExpr2, _range) -> 
                    [dive synExpr synExpr.Range traverseSynExpr
                     dive synExpr2 synExpr2.Range traverseSynExpr]
                    |> pick expr

                | SynExpr.For (_sequencePointInfoForForLoop, _ident, synExpr, _, synExpr2, synExpr3, _range) -> 
                    [dive synExpr synExpr.Range traverseSynExpr
                     dive synExpr2 synExpr2.Range traverseSynExpr
                     dive synExpr3 synExpr3.Range traverseSynExpr]
                    |> pick expr

                | SynExpr.ForEach (_sequencePointInfoForForLoop, _seqExprOnly, _isFromSource, synPat, synExpr, synExpr2, _range) ->
                    [dive synPat synPat.Range traversePat
                     dive synExpr synExpr.Range traverseSynExpr
                     dive synExpr2 synExpr2.Range traverseSynExpr]
                    |> pick expr

                | SynExpr.ArrayOrListOfSeqExpr (_, synExpr, _range) -> traverseSynExpr synExpr

                | SynExpr.CompExpr (_, _, synExpr, _range) -> 
                    // now parser treats this syntactic expression as computation expression
                    // { identifier }
                    // here we detect this situation and treat CompExpr  { Identifier } as attempt to create record
                    // note: sequence expressions use SynExpr.CompExpr too - they need to be filtered out
                    let isPartOfArrayOrList = 
                        match origPath with
                        | SyntaxNode.SynExpr(SynExpr.ArrayOrListOfSeqExpr (_, _, _)) :: _ -> true
                        | _ -> false
                    let ok = 
                        match isPartOfArrayOrList, synExpr with
                        | false, SynExpr.Ident ident -> visitor.VisitRecordField(path, None, Some (LongIdentWithDots([ident], [])))
                        | false, SynExpr.LongIdent (false, lidwd, _, _) -> visitor.VisitRecordField(path, None, Some lidwd)
                        | _ -> None
                    if ok.IsSome then ok
                    else
                    traverseSynExpr synExpr

                | SynExpr.Lambda (_, _, synSimplePats, synExpr, _, _range) ->
                    match synSimplePats with
                    | SynSimplePats.SimplePats(pats,_) ->
                        match visitor.VisitSimplePats(path, pats) with
                        | Some x -> Some x
                        | None -> traverseSynExpr synExpr
                    | _ -> traverseSynExpr synExpr

                | SynExpr.MatchLambda (_isExnMatch,_argm,synMatchClauseList,_spBind,_wholem) -> 
                    synMatchClauseList 
                    |> List.map (fun x -> dive x x.Range (traverseSynMatchClause path))
                    |> pick expr

                | SynExpr.Match (_sequencePointInfoForBinding, synExpr, synMatchClauseList, _range) -> 
                    [yield dive synExpr synExpr.Range traverseSynExpr
                     yield! synMatchClauseList |> List.map (fun x -> dive x x.RangeOfGuardAndRhs (traverseSynMatchClause path))]
                    |> pick expr

                | SynExpr.Do (synExpr, _range) -> traverseSynExpr synExpr

                | SynExpr.Assert (synExpr, _range) -> traverseSynExpr synExpr

                | SynExpr.Fixed (synExpr, _range) -> traverseSynExpr synExpr

                | SynExpr.App (_exprAtomicFlag, isInfix, synExpr, synExpr2, _range) ->
                    if isInfix then
                        [dive synExpr2 synExpr2.Range traverseSynExpr
                         dive synExpr synExpr.Range traverseSynExpr]   // reverse the args
                        |> pick expr
                    else
                        [dive synExpr synExpr.Range traverseSynExpr
                         dive synExpr2 synExpr2.Range traverseSynExpr]
                        |> pick expr

                | SynExpr.TypeApp (synExpr, _, _synTypeList, _commas, _, _, _range) -> traverseSynExpr synExpr

                | SynExpr.LetOrUse (_, isRecursive, synBindingList, synExpr, range) -> 
                    match visitor.VisitLetOrUse(path, isRecursive, traverseSynBinding path, synBindingList, range) with
                    | Some x -> Some x
                    | None ->
                        [yield! synBindingList |> List.map (fun x -> dive x x.RangeOfBindingWithRhs (traverseSynBinding path))
                         yield dive synExpr synExpr.Range traverseSynExpr]
                        |> pick expr

                | SynExpr.TryWith (synExpr, _range, synMatchClauseList, _range2, _range3, _sequencePointInfoForTry, _sequencePointInfoForWith) -> 
                    [yield dive synExpr synExpr.Range traverseSynExpr
                     yield! synMatchClauseList |> List.map (fun x -> dive x x.Range (traverseSynMatchClause path))]
                    |> pick expr

                | SynExpr.TryFinally (synExpr, synExpr2, _range, _sequencePointInfoForTry, _sequencePointInfoForFinally) -> 
                    [dive synExpr synExpr.Range traverseSynExpr
                     dive synExpr2 synExpr2.Range traverseSynExpr]
                    |> pick expr

                | SynExpr.Lazy (synExpr, _range) -> traverseSynExpr synExpr

                | SynExpr.SequentialOrImplicitYield (_sequencePointInfoForSequential, synExpr, synExpr2, _, _range) 

                | SynExpr.Sequential (_sequencePointInfoForSequential, _, synExpr, synExpr2, _range) -> 
                    [dive synExpr synExpr.Range traverseSynExpr
                     dive synExpr2 synExpr2.Range traverseSynExpr]
                    |> pick expr

                | SynExpr.IfThenElse (_, synExpr, _, synExpr2, _, synExprOpt, _sequencePointInfoForBinding, _isRecovery, _range, _range2) -> 
                    [yield dive synExpr synExpr.Range traverseSynExpr
                     yield dive synExpr2 synExpr2.Range traverseSynExpr
                     match synExprOpt with 
                     | None -> ()
                     | Some x -> yield dive x x.Range traverseSynExpr]
                    |> pick expr

                | SynExpr.Ident (_ident) -> None

                | SynExpr.LongIdent (_, _longIdent, _altNameRefCell, _range) -> None

                | SynExpr.LongIdentSet (_longIdent, synExpr, _range) -> traverseSynExpr synExpr

                | SynExpr.DotGet (synExpr, _dotm, _longIdent, _range) -> traverseSynExpr synExpr

                | SynExpr.Set (synExpr, synExpr2, _)

                | SynExpr.DotSet (synExpr, _, synExpr2, _) ->
                    [dive synExpr synExpr.Range traverseSynExpr
                     dive synExpr2 synExpr2.Range traverseSynExpr]
                    |> pick expr

                | SynExpr.DotIndexedGet (synExpr, synExprList, _range, _range2) -> 
                    [yield dive synExpr synExpr.Range traverseSynExpr
                     for synExpr in synExprList do 
                         for x in synExpr.Exprs do 
                             yield dive x x.Range traverseSynExpr]
                    |> pick expr

                | SynExpr.DotIndexedSet (synExpr, synExprList, synExpr2, _, _range, _range2) -> 
                    [yield dive synExpr synExpr.Range traverseSynExpr
                     for synExpr in synExprList do 
                         for x in synExpr.Exprs do 
                             yield dive x x.Range traverseSynExpr
                     yield dive synExpr2 synExpr2.Range traverseSynExpr]
                    |> pick expr

                | SynExpr.JoinIn (synExpr1, _range, synExpr2, _range2) -> 
                    [dive synExpr1 synExpr1.Range traverseSynExpr
                     dive synExpr2 synExpr2.Range traverseSynExpr]
                    |> pick expr

                | SynExpr.NamedIndexedPropertySet (_longIdent, synExpr, synExpr2, _range) ->
                    [dive synExpr synExpr.Range traverseSynExpr
                     dive synExpr2 synExpr2.Range traverseSynExpr]
                    |> pick expr

                | SynExpr.DotNamedIndexedPropertySet (synExpr, _longIdent, synExpr2, synExpr3, _range) ->  
                    [dive synExpr synExpr.Range traverseSynExpr
                     dive synExpr2 synExpr2.Range traverseSynExpr
                     dive synExpr3 synExpr3.Range traverseSynExpr]
                    |> pick expr

                | SynExpr.TypeTest (synExpr, synType, _range)

                | SynExpr.Upcast (synExpr, synType, _range)

                | SynExpr.Downcast (synExpr, synType, _range) ->
                    [dive synExpr synExpr.Range traverseSynExpr
                     dive synType synType.Range traverseSynType]
                    |> pick expr

                | SynExpr.InferredUpcast (synExpr, _range) -> traverseSynExpr synExpr

                | SynExpr.InferredDowncast (synExpr, _range) -> traverseSynExpr synExpr

                | SynExpr.Null (_range) -> None

                | SynExpr.AddressOf (_, synExpr, _range, _range2) -> traverseSynExpr synExpr

                | SynExpr.TraitCall (_synTyparList, _synMemberSig, synExpr, _range) -> traverseSynExpr synExpr

                | SynExpr.ImplicitZero (_range) -> None

                | SynExpr.YieldOrReturn (_, synExpr, _range) -> traverseSynExpr synExpr

                | SynExpr.YieldOrReturnFrom (_, synExpr, _range) -> traverseSynExpr synExpr

                | SynExpr.LetOrUseBang(_sequencePointInfoForBinding, _, _, synPat, synExpr, andBangSynExprs, synExpr2, _range) -> 
                    [
                        yield dive synPat synPat.Range traversePat
                        yield dive synExpr synExpr.Range traverseSynExpr
                        yield!
                            [ for (_,_,_,andBangSynPat,andBangSynExpr,_) in andBangSynExprs do
                                yield (dive andBangSynPat andBangSynPat.Range traversePat)
                                yield (dive andBangSynExpr andBangSynExpr.Range traverseSynExpr)]
                        yield dive synExpr2 synExpr2.Range traverseSynExpr
                    ]
                    |> pick expr

                | SynExpr.MatchBang (_sequencePointInfoForBinding, synExpr, synMatchClauseList, _range) -> 
                    [yield dive synExpr synExpr.Range traverseSynExpr
                     yield! synMatchClauseList |> List.map (fun x -> dive x x.RangeOfGuardAndRhs (traverseSynMatchClause path))]
                    |> pick expr

                | SynExpr.DoBang (synExpr, _range) -> traverseSynExpr synExpr

                | SynExpr.LibraryOnlyILAssembly _ -> None

                | SynExpr.LibraryOnlyStaticOptimization _ -> None

                | SynExpr.LibraryOnlyUnionCaseFieldGet _ -> None

                | SynExpr.LibraryOnlyUnionCaseFieldSet _ -> None

                | SynExpr.ArbitraryAfterError (_debugStr, _range) -> None

                | SynExpr.FromParseError (synExpr, _range) -> traverseSynExpr synExpr

                | SynExpr.DiscardAfterMissingQualificationAfterDot (synExpr, _range) -> traverseSynExpr synExpr

            visitor.VisitExpr(origPath, traverseSynExpr origPath, defaultTraverse, expr)

        and traversePat origPath (pat: SynPat) =
            let defaultTraverse p =
                let path = SyntaxNode.SynPat p :: origPath
                match p with
                | SynPat.Paren (p, _) -> traversePat path p
                | SynPat.Or (p1, p2, _) -> [ p1; p2] |> List.tryPick (traversePat path)
                | SynPat.Ands (ps, _)
                | SynPat.Tuple (_, ps, _)
                | SynPat.ArrayOrList (_, ps, _) -> ps |> List.tryPick (traversePat path)
                | SynPat.Attrib (p, _, _) -> traversePat path p
                | SynPat.LongIdent(_, _, _, args, _, _) ->
                    match args with
                    | SynArgPats.Pats ps -> ps |> List.tryPick (traversePat path)
                    | SynArgPats.NamePatPairs (ps, _) ->
                        ps |> List.map snd |> List.tryPick (traversePat path)
                | SynPat.Typed (p, ty, _) ->
                    [ traversePat path p; traverseSynType path ty ] |> List.tryPick id
                | _ -> None
                
            visitor.VisitPat (origPath, defaultTraverse, pat)

        and traverseSynType origPath (StripParenTypes ty) =
            let defaultTraverse ty =
                let path = SyntaxNode.SynType ty :: origPath
                match ty with
                | SynType.App (typeName, _, typeArgs, _, _, _, _)
                | SynType.LongIdentApp (typeName, _, _, typeArgs, _, _, _) ->
                    [ yield typeName
                      yield! typeArgs ]
                    |> List.tryPick (traverseSynType path)
                | SynType.Fun (ty1, ty2, _) -> [ty1; ty2] |> List.tryPick (traverseSynType path)
                | SynType.MeasurePower (ty, _, _) 
                | SynType.HashConstraint (ty, _)
                | SynType.WithGlobalConstraints (ty, _, _)
                | SynType.Array (_, ty, _) -> traverseSynType path ty
                | SynType.StaticConstantNamed (ty1, ty2, _)
                | SynType.MeasureDivide (ty1, ty2, _) -> [ty1; ty2] |> List.tryPick (traverseSynType path)
                | SynType.Tuple (_, tys, _) -> tys |> List.map snd |> List.tryPick (traverseSynType path)
                | SynType.StaticConstantExpr (expr, _) -> traverseSynExpr [] expr
                | SynType.Anon _ -> None
                | _ -> None

            visitor.VisitType (origPath, defaultTraverse, ty)

        and normalizeMembersToDealWithPeculiaritiesOfGettersAndSetters path traverseInherit (synMemberDefns:SynMemberDefns) =
            synMemberDefns 
                    // property getters are setters are two members that can have the same range, so do some somersaults to deal with this
                    |> Seq.groupBy (fun x -> x.Range)
                    |> Seq.choose (fun (r, mems) ->
                        match mems |> Seq.toList with
                        | [mem] -> // the typical case, a single member has this range 'r'
                            Some (dive mem r (traverseSynMemberDefn path  traverseInherit))
                        |  [SynMemberDefn.Member(SynBinding(_,_,_,_,_,_,_,SynPat.LongIdent(lid1,Some(info1),_,_,_,_),_,_,_,_),_) as mem1
                            SynMemberDefn.Member(SynBinding(_,_,_,_,_,_,_,SynPat.LongIdent(lid2,Some(info2),_,_,_,_),_,_,_,_),_) as mem2] -> // can happen if one is a getter and one is a setter
                            // ensure same long id
                            assert( (lid1.Lid,lid2.Lid) ||> List.forall2 (fun x y -> x.idText = y.idText) )
                            // ensure one is getter, other is setter
                            assert( (info1.idText="set" && info2.idText="get") ||
                                    (info2.idText="set" && info1.idText="get") )
                            Some (
                                    r,(fun() -> 
                                    // both mem1 and mem2 have same range, would violate dive-and-pick assertions, so just try the first one, else try the second one:
                                    match traverseSynMemberDefn path (fun _ -> None) mem1  with
                                    | Some _ as x -> x
                                    | _ -> traverseSynMemberDefn path (fun _ -> None) mem2 )
                                )
                        | [] ->
#if DEBUG
                            assert(false)
                            failwith "impossible, Seq.groupBy never returns empty results"
#else
                            // swallow AST error and recover silently
                            None
#endif
                        | _ ->
#if DEBUG
                            assert(false) // more than 2 members claim to have the same range, this indicates a bug in the AST
                            failwith "bug in AST"
#else
                            // swallow AST error and recover silently
                            None
#endif
                        )

        and traverseSynTypeDefn origPath (SynTypeDefn(synComponentInfo, synTypeDefnRepr, synMemberDefns, _, tRange) as tydef) =
            let path = SyntaxNode.SynTypeDefn tydef :: origPath
            
            match visitor.VisitComponentInfo (origPath, synComponentInfo) with
            | Some x -> Some x
            | None ->
            [
                match synTypeDefnRepr with
                | SynTypeDefnRepr.Exception _ -> 
                    // This node is generated in CheckExpressions.fs, not in the AST.  
                    // But note exception declarations are missing from this tree walk.
                    () 
                | SynTypeDefnRepr.ObjectModel(synTypeDefnKind, synMemberDefns, _oRange) ->
                    // traverse inherit function is used to capture type specific data required for processing Inherit part
                    let traverseInherit (synType: SynType, range: range) = 
                        visitor.VisitInheritSynMemberDefn(path, synComponentInfo, synTypeDefnKind, synType, synMemberDefns, range)
                    yield! synMemberDefns |> normalizeMembersToDealWithPeculiaritiesOfGettersAndSetters path traverseInherit
                | SynTypeDefnRepr.Simple(synTypeDefnSimpleRepr, _range) -> 
                    match synTypeDefnSimpleRepr with
                    | SynTypeDefnSimpleRepr.TypeAbbrev(_,synType,m) ->
                        yield dive synTypeDefnRepr synTypeDefnRepr.Range (fun _ -> visitor.VisitTypeAbbrev(path, synType,m))
                    | _ ->
                        () // enums/DUs/record definitions don't have any SynExprs inside them
                yield! synMemberDefns |> normalizeMembersToDealWithPeculiaritiesOfGettersAndSetters path (fun _ -> None)
            ] |> pick tRange tydef

        and traverseSynMemberDefn path traverseInherit (m:SynMemberDefn)  =
            let pick (debugObj:obj) = pick m.Range debugObj
            let path = SyntaxNode.SynMemberDefn m :: path
            match m with
            | SynMemberDefn.Open(_longIdent, _range) -> None
            | SynMemberDefn.Member(synBinding, _range) -> traverseSynBinding path synBinding
            | SynMemberDefn.ImplicitCtor(_synAccessOption, _synAttributes, simplePats, _identOption, _doc, _range) ->
                match simplePats with
                | SynSimplePats.SimplePats(simplePats, _) -> visitor.VisitSimplePats(path, simplePats)
                | _ -> None
            | SynMemberDefn.ImplicitInherit(synType, synExpr, _identOption, range) -> 
                [
                    dive () synType.Range (fun () -> 
                        match traverseInherit (synType, range) with
                        | None -> visitor.VisitImplicitInherit(path, traverseSynExpr path, synType, synExpr, range)
                        | x -> x)
                    dive () synExpr.Range (fun() -> 
                        visitor.VisitImplicitInherit(path, traverseSynExpr path, synType, synExpr, range)
                        )
                ] |> pick m
            | SynMemberDefn.AutoProperty(_attribs, _isStatic, _id, _tyOpt, _propKind, _, _xmlDoc, _access, synExpr, _, _) -> traverseSynExpr path synExpr
            | SynMemberDefn.LetBindings(synBindingList, isRecursive, _, range) -> 
                match visitor.VisitLetOrUse(path, isRecursive, traverseSynBinding path, synBindingList, range) with
                | Some x -> Some x
                | None -> synBindingList |> List.map (fun x -> dive x x.RangeOfBindingWithRhs (traverseSynBinding path)) |> pick m
            | SynMemberDefn.AbstractSlot(_synValSig, _memberFlags, _range) -> None
            | SynMemberDefn.Interface(synType, synMemberDefnsOption, _range) -> 
                match visitor.VisitInterfaceSynMemberDefnType(path, synType) with
                | None -> 
                    match synMemberDefnsOption with 
                    | None -> None
                    | Some(x) -> [ yield! x |> normalizeMembersToDealWithPeculiaritiesOfGettersAndSetters path (fun _ -> None) ]  |> pick x
                | ok -> ok
            | SynMemberDefn.Inherit(synType, _identOption, range) -> traverseInherit (synType, range)
            | SynMemberDefn.ValField(_synField, _range) -> None
            | SynMemberDefn.NestedType(synTypeDefn, _synAccessOption, _range) -> traverseSynTypeDefn path synTypeDefn

        and traverseSynMatchClause origPath mc =
            let defaultTraverse mc =
                let path = SyntaxNode.SynMatchClause mc :: origPath
                match mc with
                | (SynMatchClause(synPat, synExprOption, synExpr, _range, _sequencePointInfoForTarget) as all) ->
                    [dive synPat synPat.Range (traversePat path) ]
                    @
                    ([
                        match synExprOption with
                        | None -> ()
                        | Some guard -> yield guard
                        yield synExpr
                     ] 
                     |> List.map (fun x -> dive x x.Range (traverseSynExpr path))
                    )|> pick all.Range all
            visitor.VisitMatchClause(origPath, defaultTraverse, mc)

        and traverseSynBinding origPath b =
            let defaultTraverse b =
                let path = SyntaxNode.SynBinding b :: origPath
                match b with
                | (SynBinding(_synAccessOption, _synBindingKind, _, _, _synAttributes, _preXmlDoc, _synValData, synPat, _synBindingReturnInfoOption, synExpr, _range, _sequencePointInfoForBinding)) ->
                    [ traversePat path synPat
                      traverseSynExpr path synExpr ]
                    |> List.tryPick id
            visitor.VisitBinding(origPath, defaultTraverse,b)

        match parseTree with
        | ParsedInput.ImplFile (ParsedImplFileInput (_,_,_,_,_,l,_))-> 
            let fileRange =
#if DEBUG
                match l with [] -> range0 | _ -> l |> List.map (fun x -> x.Range) |> List.reduce unionRanges
#else
                range0  // only used for asserting, does not matter in non-debug
#endif
            l |> List.map (fun x -> dive x x.Range (traverseSynModuleOrNamespace [])) |> pick fileRange l
        | ParsedInput.SigFile _sigFile -> None
