// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.EditorServices

open Internal.Utilities.Library
open FSharp.Compiler.Syntax
open FSharp.Compiler.SyntaxTreeOps
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Position
open FSharp.Compiler.Text.Range

module Structure =

    /// Set of visitor utilities, designed for the express purpose of fetching ranges
    /// from an untyped AST for the purposes of block structure.
    [<RequireQualifiedAccess>]
    module Range =

        /// Create a range starting at the end of r1 and finishing at the end of r2
        let endToEnd (r1: range) (r2: range) = mkFileIndexRange r1.FileIndex r1.End r2.End

        /// Create a range starting at the end of r1 and finishing at the start of r2
        let endToStart (r1: range) (r2: range) = mkFileIndexRange r1.FileIndex r1.End r2.Start

        /// Create a range beginning at the start of r1 and finishing at the end of r2
        let startToEnd (r1: range) (r2: range) = mkFileIndexRange r1.FileIndex r1.Start r2.End

        /// Create a range beginning at the start of r1 and finishing at the start of r2
        let startToStart (r1: range) (r2: range) = mkFileIndexRange r1.FileIndex r1.Start r2.Start

        /// Create a new range from r by shifting the starting column by m
        let modStart  (m:int) (r: range) =
            let modstart = mkPos r.StartLine (r.StartColumn+m)
            mkFileIndexRange r.FileIndex modstart r.End

        /// Create a new range from r by shifting the ending column by m
        let modEnd (m:int) (r: range) =
            let modend = mkPos r.EndLine (r.EndColumn+m)
            mkFileIndexRange r.FileIndex r.Start modend

        /// Produce a new range by adding modStart to the StartColumn of `r`
        /// and subtracting modEnd from the EndColumn of `r`
        let modBoth modStart modEnd (r:range) =
            let rStart = mkPos r.StartLine (r.StartColumn+modStart)
            let rEnd   = mkPos r.EndLine   (r.EndColumn - modEnd)
            mkFileIndexRange r.FileIndex rStart rEnd

    let longIdentRange (longId:LongIdent) =
        match longId with 
        | [] -> range0
        | head :: _ -> Range.startToEnd head.idRange (List.last longId).idRange

    /// Calculate the range of the provided type arguments (<'a, ..., 'z>) 
    /// or return the range `other` when `typeArgs` = []
    let rangeOfTypeArgsElse other (typeArgs:SynTyparDecl list) =
        match typeArgs with
        | [] -> other
        | ls ->
            ls
            |> List.map (fun (SynTyparDecl (_, typarg)) -> typarg.Range)
            |> List.reduce unionRanges

    let rangeOfSynPatsElse other (synPats:SynSimplePat list) =
        match synPats with
        | [] -> other
        | ls ->
            ls 
            |> List.map (fun x ->
                 match x with
                 | SynSimplePat.Attrib(range = r)
                 | SynSimplePat.Id(range = r)
                 | SynSimplePat.Typed(range = r) -> r)
            |> List.reduce Range.unionRanges

    /// Collapse indicates the way a range/snapshot should be collapsed. `Same` is for a scope inside
    /// some kind of scope delimiter, e.g. `[| ... |]`, `[ ... ]`, `{ ... }`, etc.  `Below` is for expressions
    /// following a binding or the right hand side of a pattern, e.g. `let x = ...`
    [<RequireQualifiedAccess>]
    type Collapse =
        | Below
        | Same

    /// Tag to identify the construct that can be stored alongside its associated ranges
    [<RequireQualifiedAccess>]
    type Scope =
        | Open
        | Namespace
        | Module
        | Type
        | Member
        | LetOrUse
        | Val
        | CompExpr
        | IfThenElse
        | ThenInIfThenElse
        | ElseInIfThenElse
        | TryWith
        | TryInTryWith
        | WithInTryWith
        | TryFinally
        | TryInTryFinally
        | FinallyInTryFinally
        | ArrayOrList
        | ObjExpr
        | For
        | While
        | Match
        | MatchBang
        | MatchLambda
        | MatchClause
        | Lambda
        | CompExprInternal
        | Quote
        | Record
        | SpecialFunc
        | Do
        | New
        | Attribute
        | Interface
        | HashDirective
        | LetOrUseBang
        | TypeExtension
        | YieldOrReturn
        | YieldOrReturnBang
        | Tuple
        | UnionCase
        | EnumCase
        | RecordField
        | RecordDefn
        | UnionDefn
        | Comment
        | XmlDocComment

        override self.ToString() = 
            match self with
            | Open                -> "Open"
            | Namespace           -> "Namespace"
            | Module              -> "Module"
            | Type                -> "Type"
            | Member              -> "Member"
            | LetOrUse            -> "LetOrUse"
            | Val                 -> "Val"
            | CompExpr            -> "CompExpr"
            | IfThenElse          -> "IfThenElse"
            | ThenInIfThenElse    -> "ThenInIfThenElse"
            | ElseInIfThenElse    -> "ElseInIfThenElse"
            | TryWith             -> "TryWith"
            | TryInTryWith        -> "TryInTryWith"
            | WithInTryWith       -> "WithInTryWith"
            | TryFinally          -> "TryFinally"
            | TryInTryFinally     -> "TryInTryFinally"
            | FinallyInTryFinally -> "FinallyInTryFinally"
            | ArrayOrList         -> "ArrayOrList"
            | ObjExpr             -> "ObjExpr"
            | For                 -> "For"
            | While               -> "While"
            | Match               -> "Match"
            | MatchBang           -> "MatchBang"
            | MatchLambda         -> "MatchLambda"
            | MatchClause         -> "MatchClause"
            | Lambda              -> "Lambda"
            | CompExprInternal    -> "CompExprInternal"
            | Quote               -> "Quote"
            | Record              -> "Record"
            | SpecialFunc         -> "SpecialFunc"
            | Do                  -> "Do"
            | New                 -> "New"
            | Attribute           -> "Attribute"
            | Interface           -> "Interface"
            | HashDirective       -> "HashDirective"
            | LetOrUseBang        -> "LetOrUseBang"
            | TypeExtension       -> "TypeExtension"
            | YieldOrReturn       -> "YieldOrReturn"
            | YieldOrReturnBang   -> "YieldOrReturnBang"
            | Tuple               -> "Tuple"
            | UnionCase           -> "UnionCase"
            | EnumCase            -> "EnumCase"
            | RecordField         -> "RecordField"
            | RecordDefn          -> "RecordDefn"
            | UnionDefn           -> "UnionDefn"
            | Comment             -> "Comment"
            | XmlDocComment       -> "XmlDocComment"

    /// Stores the range for a construct, the sub-range that should be collapsed for outlining,
    /// a tag for the construct type, and a tag for the collapse style
    [<NoComparison>]
    type ScopeRange = 
        { Scope: Scope
          Collapse: Collapse
          /// HintSpan in BlockSpan
          Range: range
          /// TextSpan in BlockSpan
          CollapseRange: range }

    type LineNumber = int
    type LineStr = string
    type CommentType = SingleLine | XmlDoc

    [<NoComparison>]
    type CommentList =
        { Lines: ResizeArray<LineNumber * LineStr>
          Type: CommentType }
        static member New ty lineStr =
            { Type = ty
              Lines = ResizeArray [lineStr] }

    /// Returns outlining ranges for given parsed input.                
    let getOutliningRanges (sourceLines: string[]) (parsedInput: ParsedInput) =
        let acc = ResizeArray()

        /// Validation function to ensure that ranges yielded for outlining span 2 or more lines
        let inline rcheck scope collapse (fullRange: range) (collapseRange: range) = 
            if fullRange.StartLine <> fullRange.EndLine then 
                acc.Add { Scope = scope
                          Collapse = collapse
                          Range = fullRange
                          CollapseRange = collapseRange }

          //============================================//
         //     Implementation File AST Traversal      //
        //============================================//

        let rec parseExpr expression =
            match expression with
            | SynExpr.Upcast (e, _, _)
            | SynExpr.Downcast (e, _, _)
            | SynExpr.AddressOf (_, e, _, _)
            | SynExpr.InferredDowncast (e, _)
            | SynExpr.InferredUpcast (e, _)
            | SynExpr.DotGet (e, _, _, _)
            | SynExpr.Do (e, _)
            | SynExpr.Typed (e, _, _)
            | SynExpr.DotIndexedGet (e, _, _, _) -> 
                parseExpr e
            | SynExpr.Set (e1, e2, _)
            | SynExpr.DotSet (e1, _, e2, _)
            | SynExpr.DotIndexedSet (e1, _, e2, _, _, _) -> 
                parseExpr e1
                parseExpr e2
            | SynExpr.New (_, _, expr, r) ->
                rcheck Scope.New Collapse.Below r expr.Range
                parseExpr expr
            | SynExpr.YieldOrReturn (_, e, r) ->
                rcheck Scope.YieldOrReturn Collapse.Below r r
                parseExpr e
            | SynExpr.YieldOrReturnFrom (_, e, r) ->
                rcheck Scope.YieldOrReturnBang Collapse.Below r r
                parseExpr e
            | SynExpr.DoBang (e, r) ->
                rcheck Scope.Do Collapse.Below r <| Range.modStart 3 r
                parseExpr e
            | SynExpr.LetOrUseBang (_, _, _, pat, eLet, es, eBody, _) ->
                [
                    yield eLet
                    for (_,_,_,_,eAndBang,_) in es do 
                        yield eAndBang
                ]
                |> List.iter (fun e ->
                    // for `let!`, `use!` or `and!` the pattern begins at the end of the
                    // keyword so that this scope can be used without adjustment if there is no `=`
                    // on the same line. If there is an `=` the range will be adjusted during the
                    // tooltip creation
                    let r = Range.endToEnd pat.Range e.Range
                    rcheck Scope.LetOrUseBang Collapse.Below r r
                    parseExpr e
                )
                parseExpr eBody
            | SynExpr.For (_, _, _, _, _, e, r)
            | SynExpr.ForEach (_, _, _, _, _, e, r) ->
                rcheck Scope.For Collapse.Below r r
                parseExpr e
            | SynExpr.LetOrUse (_, _, bindings, body, _) ->
                parseBindings bindings
                parseExpr body
            | SynExpr.Match (seqPointAtBinding, _expr, clauses, r)
            | SynExpr.MatchBang (seqPointAtBinding, _expr, clauses, r) ->
                match seqPointAtBinding with
                | DebugPointAtBinding.Yes sr ->
                    let collapse = Range.endToEnd sr r
                    rcheck Scope.Match Collapse.Same r collapse
                | _ -> ()
                List.iter parseMatchClause clauses
            | SynExpr.MatchLambda (_, caseRange, clauses, matchSeqPoint, r) ->
                let caseRange =
                    match matchSeqPoint with
                    | DebugPointAtBinding.Yes r -> r
                    | _ -> caseRange
                let collapse = Range.endToEnd caseRange r
                rcheck Scope.MatchLambda Collapse.Same r collapse
                List.iter parseMatchClause clauses
            | SynExpr.App (atomicFlag, isInfix, funcExpr, argExpr, r) ->
                // seq exprs, custom operators, etc
                if ExprAtomicFlag.NonAtomic=atomicFlag && (not isInfix)
                   && (function SynExpr.Ident _    -> true  | _ -> false) funcExpr
                   && (function SynExpr.CompExpr _ -> false | _ -> true ) argExpr then
                   // if the argExpr is a computation expression another match will handle the outlining
                   // these cases must be removed to prevent creating unnecessary tags for the same scope
                    let collapse = Range.endToEnd funcExpr.Range r
                    rcheck Scope.SpecialFunc Collapse.Below r collapse
                elif ExprAtomicFlag.NonAtomic=atomicFlag && (not isInfix)
                   && (function SynExpr.CompExpr _ -> true | _ -> false) argExpr then
                        let collapse = Range.startToEnd argExpr.Range r
                        rcheck Scope.CompExpr Collapse.Same r <| Range.modBoth 1 1 collapse
                parseExpr argExpr
                parseExpr funcExpr
            | SynExpr.Sequential (_, _, e1, e2, _) ->
                parseExpr e1
                parseExpr e2
            | SynExpr.ArrayOrListOfSeqExpr (isArray, e, r) ->
                rcheck  Scope.ArrayOrList Collapse.Same r <| Range.modBoth (if isArray then 2 else 1) (if isArray then 2 else 1) r
                parseExpr e
            | SynExpr.CompExpr (_arrayOrList, _, e, _r) as _c ->
                parseExpr e
            | SynExpr.ObjExpr (_, argOpt, bindings, extraImpls, newRange, wholeRange) as _objExpr ->
                match argOpt with
                | Some (args, _) ->
                    let collapse = Range.endToEnd args.Range wholeRange
                    rcheck Scope.ObjExpr Collapse.Below wholeRange collapse
                | None ->
                    let collapse = Range.endToEnd newRange wholeRange
                    rcheck Scope.ObjExpr Collapse.Below wholeRange collapse
                parseBindings bindings
                parseExprInterfaces extraImpls
            | SynExpr.TryWith (e, _, matchClauses, _, wholeRange, tryPoint, withPoint) ->
                match tryPoint, withPoint with
                | DebugPointAtTry.Yes tryRange,  DebugPointAtWith.Yes withRange ->
                    let fullrange = Range.startToEnd tryRange wholeRange
                    let collapse = Range.endToEnd tryRange wholeRange
                    let collapseTry = Range.endToStart tryRange withRange
                    let fullrangeTry = Range.startToStart tryRange withRange
                    let collapseWith = Range.endToEnd withRange wholeRange
                    let fullrangeWith = Range.startToEnd withRange wholeRange
                    rcheck Scope.TryWith Collapse.Below fullrange collapse
                    rcheck Scope.TryInTryWith Collapse.Below fullrangeTry collapseTry
                    rcheck Scope.WithInTryWith Collapse.Below fullrangeWith collapseWith
                | _ -> ()
                parseExpr e
                List.iter parseMatchClause matchClauses
            | SynExpr.TryFinally (tryExpr, finallyExpr, r, tryPoint, finallyPoint) ->
                match tryPoint, finallyPoint with
                | DebugPointAtTry.Yes tryRange, DebugPointAtFinally.Yes finallyRange ->
                    let collapse = Range.endToEnd tryRange finallyExpr.Range
                    let fullrange = Range.startToEnd tryRange finallyExpr.Range
                    let collapseFinally = Range.endToEnd finallyRange r
                    let fullrangeFinally = Range.startToEnd finallyRange r
                    rcheck Scope.TryFinally Collapse.Below fullrange collapse
                    rcheck  Scope.FinallyInTryFinally Collapse.Below fullrangeFinally collapseFinally
                | _ -> ()
                parseExpr tryExpr
                parseExpr finallyExpr
            | SynExpr.IfThenElse (_, ifExpr, _, thenExpr, _, elseExprOpt, spIfToThen, _, ifToThenRange, r) ->
                match spIfToThen with
                | DebugPointAtBinding.Yes rt ->
                    // Outline the entire IfThenElse
                    let fullrange = Range.startToEnd rt r
                    let collapse = Range.endToEnd  ifExpr.Range r
                    rcheck Scope.IfThenElse Collapse.Below fullrange collapse
                    // Outline the `then` scope
                    let thenRange = Range.endToEnd (Range.modEnd -4  ifToThenRange)   thenExpr.Range
                    let thenCollapse = Range.endToEnd ifToThenRange thenExpr.Range
                    rcheck Scope.ThenInIfThenElse Collapse.Below thenRange thenCollapse
                | _ -> ()
                parseExpr ifExpr
                parseExpr thenExpr
                match elseExprOpt with
                | Some elseExpr ->
                    match elseExpr with // prevent double collapsing on elifs
                    | SynExpr.IfThenElse _ ->
                        parseExpr elseExpr
                    | _ ->
                        // This is not the best way to establish the position of `else`
                        // the AST doesn't provide an easy way to find the position of the keyword
                        // as such `else` will be left out of block structuring and outlining until a
                        // a suitable approach is determined
                        parseExpr elseExpr
                | None -> ()
            | SynExpr.While (_, _, e, r) ->
                rcheck Scope.While Collapse.Below r r
                parseExpr e
            | SynExpr.Lambda (_, _, pats, e, _, r) ->
                match pats with
                | SynSimplePats.SimplePats (_, pr)
                | SynSimplePats.Typed (_, _, pr) ->
                    rcheck Scope.Lambda Collapse.Below r (Range.endToEnd pr r)
                parseExpr e
            | SynExpr.Lazy (e, r) ->
                rcheck Scope.SpecialFunc Collapse.Below r r
                parseExpr e
            | SynExpr.Quote (_, isRaw, e, _, r) ->
                // subtract columns so the @@> or @> is not collapsed
                rcheck Scope.Quote Collapse.Same r (Range.modBoth (if isRaw then 3 else 2) (if isRaw then 3 else 2) r)
                parseExpr e
            | SynExpr.Tuple (_, es, _, r) ->
                rcheck Scope.Tuple Collapse.Same r r
                List.iter parseExpr es
            | SynExpr.Paren (e, _, _, _) ->
                parseExpr e
            | SynExpr.Record (recCtor, recCopy, recordFields, r) ->
                match recCtor with
                | Some (_, ctorArgs, _, _, _) -> parseExpr ctorArgs
                | _ -> ()
                match recCopy with
                | Some (e, _) -> parseExpr e
                | _ -> ()
                recordFields |> List.choose (fun (_, e, _) -> e) |> List.iter parseExpr
                // exclude the opening `{` and closing `}` of the record from collapsing
                rcheck Scope.Record Collapse.Same r <| Range.modBoth 1 1 r
            | _ -> ()

        and parseMatchClause (SynMatchClause(synPat, _, e, _r, _) as clause) =
            let rec getLastPat = function
                | SynPat.Or(_, pat, _) -> getLastPat pat
                | x -> x

            let synPat = getLastPat synPat
            let collapse  = Range.endToEnd synPat.Range clause.Range // Collapse the scope starting with `->`
            rcheck Scope.MatchClause Collapse.Same e.Range collapse
            parseExpr e

        and parseAttributes (Attributes attrs) =
            let attrListRange() =
                if not (List.isEmpty attrs) then
                    let range = Range.startToEnd (attrs.[0].Range) (attrs.[attrs.Length-1].ArgExpr.Range)
                    rcheck Scope.Attribute Collapse.Same range range

            match  attrs with
            | [] -> ()
            | [_] -> attrListRange()
            | head :: tail ->
                attrListRange()
                parseExpr head.ArgExpr
                // If there are more than 2 attributes only add tags to the 2nd and beyond, to avoid double collapsing on the first attribute
                for attr in tail do
                    let range = Range.startToEnd attr.Range attr.ArgExpr.Range
                    rcheck Scope.Attribute Collapse.Same range range
                
                // visit the expressions inside each attribute
                for attr in attrs do
                    parseExpr attr.ArgExpr

        and parseBinding (SynBinding(_, kind, _, _, attrs, _, SynValData(memberFlags, _, _), _, _, expr, br, _) as binding) =
            match kind with
            | SynBindingKind.Normal ->
                let collapse = Range.endToEnd binding.RangeOfBindingWithoutRhs binding.RangeOfBindingWithRhs
                match memberFlags with
                | Some {MemberKind=SynMemberKind.Constructor} ->
                    rcheck Scope.New Collapse.Below binding.RangeOfBindingWithRhs collapse
                | Some _ ->
                    rcheck Scope.Member Collapse.Below binding.RangeOfBindingWithRhs collapse
                | None ->
                    rcheck Scope.LetOrUse Collapse.Below binding.RangeOfBindingWithRhs collapse
            | SynBindingKind.Do ->
                let r = Range.modStart 2 br
                rcheck Scope.Do Collapse.Below br r
            | _ -> ()
            parseAttributes attrs
            parseExpr expr

        and parseBindings sqs = for bind in sqs do parseBinding bind

        and parseExprInterface (SynInterfaceImpl(synType, bindings, range)) =
            let collapse = Range.endToEnd synType.Range range |> Range.modEnd -1
            rcheck Scope.Interface Collapse.Below range collapse
            parseBindings bindings

        and parseExprInterfaces (intfs: SynInterfaceImpl list) = List.iter parseExprInterface intfs

        and parseSynMemberDefn (objectModelRange: range) d =
            match d with
            | SynMemberDefn.Member(SynBinding.SynBinding (attributes=attrs; valData=valData; headPat=synPat; range=bindingRange) as binding, _) ->
               match valData with
               | SynValData (Some { MemberKind=SynMemberKind.Constructor }, _, _) ->
                  let collapse = Range.endToEnd synPat.Range d.Range
                  rcheck Scope.New Collapse.Below d.Range collapse
               | SynValData (Some { MemberKind=SynMemberKind.PropertyGet | SynMemberKind.PropertySet }, _, _) ->
                  let range = 
                    mkRange 
                        d.Range.FileName 
                        (mkPos d.Range.StartLine objectModelRange.StartColumn)
                        d.Range.End
              
                  let collapse =
                    match synPat with
                    | SynPat.LongIdent(longDotId=longIdent) ->
                       Range.endToEnd longIdent.Range d.Range
                    | _ -> Range.endToEnd bindingRange d.Range

                  rcheck Scope.Member Collapse.Below range collapse
               | _ ->
                  let collapse = Range.endToEnd bindingRange d.Range
                  rcheck Scope.Member Collapse.Below d.Range collapse
               parseAttributes attrs
               parseBinding binding
            | SynMemberDefn.LetBindings (bindings, _, _, _) ->
                parseBindings bindings
            | SynMemberDefn.Interface (tp, iMembers, r) ->
                rcheck Scope.Interface Collapse.Below d.Range (Range.endToEnd tp.Range d.Range)
                match iMembers with
                | Some members -> List.iter (parseSynMemberDefn r) members
                | None -> ()
            | SynMemberDefn.NestedType (td, _, _) ->
                parseTypeDefn td 
            | SynMemberDefn.AbstractSlot (SynValSig(synType=synt), _, r) ->
                rcheck Scope.Member Collapse.Below d.Range (Range.startToEnd synt.Range r)
            | SynMemberDefn.AutoProperty (synExpr=e; range=r) ->
                rcheck Scope.Member Collapse.Below d.Range r
                parseExpr e
            | _ -> ()

        (*  For Cases like
            --------------
                type JsonDocument =
                    private {   Json : string
                                Path : string   }
            Or
                 type JsonDocument =
                    internal |  Json of string
                             |  Path of string
        *)
        and parseSimpleRepr simple =
            match simple with
            | SynTypeDefnSimpleRepr.Enum (cases, _er) ->
                for SynEnumCase (attrs, _, _, _, _, cr) in cases do
                    rcheck Scope.EnumCase Collapse.Below cr cr
                    parseAttributes attrs
            | SynTypeDefnSimpleRepr.Record (_, fields, rr) ->
                rcheck Scope.RecordDefn Collapse.Same rr rr 
                for SynField (attrs, _, _, _, _, _, _, fr) in fields do
                    rcheck Scope.RecordField Collapse.Below fr fr
                    parseAttributes attrs
            | SynTypeDefnSimpleRepr.Union (_, cases, ur) ->
                rcheck Scope.UnionDefn Collapse.Same ur ur
                for SynUnionCase (attrs, _, _, _, _, cr) in cases do
                    rcheck Scope.UnionCase Collapse.Below cr cr
                    parseAttributes attrs
            | _ -> ()

        and parseTypeDefn (SynTypeDefn(SynComponentInfo(_, TyparDecls typeArgs, _, _, _, _, _, r), objectModel, members, _, fullrange)) = 
           let typeArgsRange = rangeOfTypeArgsElse r typeArgs
           let collapse = Range.endToEnd (Range.modEnd 1 typeArgsRange) fullrange
           match objectModel with
           | SynTypeDefnRepr.ObjectModel (defnKind, objMembers, r) ->
               match defnKind with
               | SynTypeDefnKind.Augmentation ->
                   rcheck Scope.TypeExtension Collapse.Below fullrange collapse
               | _ ->
                   rcheck Scope.Type Collapse.Below fullrange collapse
               List.iter (parseSynMemberDefn r) objMembers
               // visit the members of a type extension
               List.iter (parseSynMemberDefn r) members
           | SynTypeDefnRepr.Simple (simpleRepr, r) ->
               rcheck Scope.Type Collapse.Below fullrange collapse
               parseSimpleRepr simpleRepr
               List.iter (parseSynMemberDefn r) members
           | SynTypeDefnRepr.Exception _ -> ()

        let getConsecutiveModuleDecls (predicate: SynModuleDecl -> range option) (scope: Scope) (decls: SynModuleDecl list) =
            let groupConsecutiveDecls input =
                let rec loop (input: range list) (res: range list list) currentBulk =
                    match input, currentBulk with
                    | [], [] -> List.rev res
                    | [], _ -> List.rev (currentBulk :: res)
                    | r :: rest, [] -> loop rest res [r]
                    | r :: rest, last :: _ 
                        when r.StartLine = last.EndLine + 1 || 
                             sourceLines.[last.EndLine..r.StartLine - 2] |> Array.forall System.String.IsNullOrWhiteSpace ->
                        loop rest res (r :: currentBulk)
                    | r :: rest, _ -> loop rest (currentBulk :: res) [r]
                loop input [] []

            let selectRanges (ranges: range list) =
                match ranges with
                | [] -> None
                | [r] when r.StartLine = r.EndLine -> None
                | [r] ->
                    let range = mkRange "" r.Start r.End
                    Some { Scope = scope; Collapse = Collapse.Same; Range = range ; CollapseRange = range }
                | lastRange :: rest ->
                    let firstRange = Seq.last rest
                    let range = mkRange "" firstRange.Start lastRange.End
                    Some { Scope = scope; Collapse = Collapse.Same; Range = range; CollapseRange = range }

            decls 
            |> List.choose predicate 
            |> groupConsecutiveDecls 
            |> List.choose selectRanges
            |> acc.AddRange

        let collectOpens = getConsecutiveModuleDecls (function SynModuleDecl.Open (_, r) -> Some r | _ -> None) Scope.Open

        let collectHashDirectives =
             getConsecutiveModuleDecls(
                function
                | SynModuleDecl.HashDirective (ParsedHashDirective (directive, _, _), r) ->
                    let prefixLength = "#".Length + directive.Length + " ".Length
                    Some (mkRange "" (mkPos r.StartLine prefixLength) r.End)
                | _ -> None) Scope.HashDirective

        let rec parseDeclaration (decl: SynModuleDecl) =
            match decl with
            | SynModuleDecl.Let (_, bindings, r) ->
                for binding in bindings do
                    let collapse = Range.endToEnd binding.RangeOfBindingWithoutRhs r
                    rcheck Scope.LetOrUse Collapse.Below r collapse
                parseBindings bindings
            | SynModuleDecl.Types (types, _r) ->
                for t in types do
                    parseTypeDefn t
            // Fold the attributes above a module
            | SynModuleDecl.NestedModule (SynComponentInfo (attrs, _, _, _, _, _, _, cmpRange), _, decls, _, _) ->                
                // Outline the full scope of the module
                let r = Range.endToEnd cmpRange decl.Range
                rcheck Scope.Module Collapse.Below decl.Range r
                // A module's component info stores the ranges of its attributes
                parseAttributes attrs
                collectOpens decls
                List.iter parseDeclaration decls
            | SynModuleDecl.DoExpr (_, e, _) ->
                parseExpr e
            | SynModuleDecl.Attributes (attrs, _) ->
                parseAttributes attrs
            | _ -> ()

        let parseModuleOrNamespace (SynModuleOrNamespace (longId, _, kind, decls, _, attribs, _, r)) =
            parseAttributes attribs
            let idRange = longIdentRange longId
            let fullrange = Range.startToEnd idRange r  
            let collapse = Range.endToEnd idRange r 
        
            // do not return range for top level implicit module in scripts
            if kind = SynModuleOrNamespaceKind.NamedModule then
                rcheck Scope.Module Collapse.Below fullrange collapse

            collectHashDirectives decls
            collectOpens decls
            List.iter parseDeclaration decls

        /// Determine if a line is a single line or xml documentation comment
        let (|Comment|_|) (line: string) =
            if line.StartsWithOrdinal("///") then Some XmlDoc
            elif line.StartsWithOrdinal("//") then Some SingleLine
            else None

        let getCommentRanges (lines: string[]) =
            let rec loop ((lastLineNum, currentComment: CommentList option, result) as state) (lines: string list) lineNum =
                match lines with
                | [] -> state
                | lineStr :: rest ->
                    match lineStr.TrimStart(), currentComment with
                    | Comment commentType, Some comment ->
                        loop(
                            if comment.Type = commentType && lineNum = lastLineNum + 1 then
                                comment.Lines.Add (lineNum, lineStr)
                                lineNum, currentComment, result
                            else lineNum, Some (CommentList.New commentType (lineNum, lineStr)), comment :: result) rest (lineNum + 1)
                    | Comment commentType, None ->
                        loop(lineNum, Some (CommentList.New commentType (lineNum, lineStr)), result) rest (lineNum + 1)
                    | _, Some comment ->
                        loop(lineNum, None, comment :: result) rest (lineNum + 1)
                    | _ -> loop(lineNum, None, result) rest (lineNum + 1)

            let comments: CommentList list =
                loop (-1, None, []) (List.ofArray lines) 0
                |> fun (_, lastComment, comments) ->
                    match lastComment with
                    | Some comment ->
                        comment :: comments
                    | _ -> comments
                    |> List.rev

            comments
            |> List.filter (fun comment -> comment.Lines.Count > 1)
            |> List.map (fun comment ->
                let lines = comment.Lines
                let startLine, startStr = lines.[0]
                let endLine, endStr = lines.[lines.Count - 1]
                let startCol = startStr.IndexOf '/'
                let endCol = endStr.TrimEnd().Length

                let scopeType =
                    match comment.Type with
                    | SingleLine -> Scope.Comment
                    | XmlDoc -> Scope.XmlDocComment

                let range = mkRange "" (mkPos (startLine + 1) startCol) (mkPos (endLine + 1) endCol)

                { Scope = scopeType
                  Collapse = Collapse.Same
                  Range = range
                  CollapseRange = range })
            |> acc.AddRange


          //=======================================//
         //     Signature File AST Traversal      //
        //=======================================//

        (*
            The following helper functions are necessary due to a bug in the Parsed UAST within a 
            signature file that causes the scopes to extend past the end of the construct and overlap
            with the following construct. This necessitates inspecting the children of the construct and
            finding the end of the last child's range to use instead.

            Detailed further in - https://github.com/Microsoft/visualfsharp/issues/2094
        *)

        let lastMemberSigRangeElse r memberSigs =
            match memberSigs with
            | [] -> r
            | ls ->
                match List.last ls with
                | SynMemberSig.Inherit (range=r)
                | SynMemberSig.Interface (range=r)
                | SynMemberSig.Member (range=r)
                | SynMemberSig.NestedType (range=r)
                | SynMemberSig.ValField (range=r) -> r

        let lastTypeDefnSigRangeElse range (typeSigs:SynTypeDefnSig list) =
            match typeSigs with
            | [] -> range
            | ls ->
                let (SynTypeDefnSig(_, _, memberSigs, r)) = List.last ls
                lastMemberSigRangeElse r memberSigs

        let lastModuleSigDeclRangeElse range (sigDecls:SynModuleSigDecl list) =
            match sigDecls with
            | [] -> range
            | ls -> 
                match List.last ls with
                | SynModuleSigDecl.Types (typeSigs, r) -> lastTypeDefnSigRangeElse r typeSigs
                | SynModuleSigDecl.Val (SynValSig(range=r), _) -> r
                | SynModuleSigDecl.Exception(_, r) -> r
                | SynModuleSigDecl.Open(_, r) -> r
                | SynModuleSigDecl.ModuleAbbrev(_, _, r) -> r
                | _ -> range

        let rec parseSynMemberDefnSig = function
            | SynMemberSig.Member(valSigs, _, r) ->
                let collapse = Range.endToEnd valSigs.RangeOfId r
                rcheck Scope.Member Collapse.Below r collapse
            | SynMemberSig.ValField(SynField(attrs, _, _, _, _, _, _, fr), fullrange) ->
                let collapse = Range.endToEnd fr fullrange
                rcheck Scope.Val Collapse.Below fullrange collapse
                parseAttributes attrs
            | SynMemberSig.Interface(tp, r) ->
                rcheck Scope.Interface Collapse.Below r (Range.endToEnd tp.Range r)
            | SynMemberSig.NestedType (typeDefSig, _r) ->
                parseTypeDefnSig typeDefSig
            | _ -> ()

        and parseTypeDefnSig (SynTypeDefnSig (SynComponentInfo(attribs, TyparDecls typeArgs, _, longId, _, _, _, r) as __, objectModel, memberSigs, _)) = 
            parseAttributes attribs

            let makeRanges memberSigs =
                let typeArgsRange = rangeOfTypeArgsElse r typeArgs
                let rangeEnd = lastMemberSigRangeElse r memberSigs
                let collapse = Range.endToEnd (Range.modEnd 1 typeArgsRange) rangeEnd
                let fullrange = Range.startToEnd (longIdentRange longId) rangeEnd
                fullrange, collapse

            List.iter parseSynMemberDefnSig memberSigs

            match objectModel with
            // matches against a type declaration with <'T, ...> and (args, ...)
            | SynTypeDefnSigRepr.ObjectModel
                (SynTypeDefnKind.Unspecified, objMembers, _) ->
                    List.iter parseSynMemberDefnSig objMembers
                    let fullrange, collapse = makeRanges objMembers
                    rcheck Scope.Type Collapse.Below fullrange collapse
            | SynTypeDefnSigRepr.ObjectModel (SynTypeDefnKind.Augmentation, objMembers, _) ->
                    let fullrange, collapse = makeRanges objMembers
                    rcheck Scope.TypeExtension Collapse.Below fullrange collapse
                    List.iter parseSynMemberDefnSig objMembers
            | SynTypeDefnSigRepr.ObjectModel (_, objMembers, _) ->
                    let fullrange, collapse = makeRanges objMembers
                    rcheck Scope.Type Collapse.Below fullrange collapse
                    List.iter parseSynMemberDefnSig objMembers
                // visit the members of a type extension
            | SynTypeDefnSigRepr.Simple (simpleRepr, _) ->
                let fullrange, collapse = makeRanges memberSigs
                rcheck Scope.Type Collapse.Below fullrange collapse
                parseSimpleRepr simpleRepr
            | SynTypeDefnSigRepr.Exception _ -> ()

        let getConsecutiveSigModuleDecls (predicate: SynModuleSigDecl -> range option) (scope:Scope) (decls: SynModuleSigDecl list) =
            let groupConsecutiveSigDecls input =
                let rec loop (input: range list) (res: range list list) currentBulk =
                    match input, currentBulk with
                    | [], [] -> List.rev res
                    | [], _ -> List.rev (currentBulk :: res)
                    | r :: rest, [] -> loop rest res [r]
                    | r :: rest, last :: _ when r.StartLine = last.EndLine + 1 ->
                        loop rest res (r :: currentBulk)
                    | r :: rest, _ -> loop rest (currentBulk :: res) [r]
                loop input [] []

            let selectSigRanges (ranges: range list) =
                match ranges with
                | [] -> None
                | [r] when r.StartLine = r.EndLine -> None
                | [r] ->
                    let range = mkRange "" r.Start r.End
                    Some { Scope = scope; Collapse = Collapse.Same; Range = range ; CollapseRange = range }
                | lastRange :: rest ->
                    let firstRange = Seq.last rest
                    let range = mkRange "" firstRange.Start lastRange.End
                    Some { Scope = scope; Collapse = Collapse.Same; Range = range; CollapseRange = range }

            decls 
            |> List.choose predicate 
            |> groupConsecutiveSigDecls 
            |> List.choose selectSigRanges
            |> acc.AddRange

        let collectSigHashDirectives (decls: SynModuleSigDecl list) =
            decls
            |> getConsecutiveSigModuleDecls(
                function
                | SynModuleSigDecl.HashDirective (ParsedHashDirective (directive, _, _), r) ->
                    let prefixLength = "#".Length + directive.Length + " ".Length
                    Some (mkRange "" (mkPos r.StartLine prefixLength) r.End)
                | _ -> None) Scope.HashDirective

        let collectSigOpens = getConsecutiveSigModuleDecls (function SynModuleSigDecl.Open (_, r) -> Some r | _ -> None) Scope.Open

        let rec parseModuleSigDeclaration (decl: SynModuleSigDecl) =
            match decl with
            | SynModuleSigDecl.Val ((SynValSig(attrs, ident, _, _, _, _, _, _, _, _, valrange)), r) ->
                let collapse = Range.endToEnd ident.idRange valrange
                rcheck Scope.Val Collapse.Below r collapse
                parseAttributes attrs
            | SynModuleSigDecl.Types (typeSigs, _) ->
                List.iter parseTypeDefnSig typeSigs
            // Fold the attributes above a module
            | SynModuleSigDecl.NestedModule (SynComponentInfo (attrs, _, _, _, _, _, _, cmpRange), _, decls, moduleRange) ->
                let rangeEnd = lastModuleSigDeclRangeElse moduleRange decls
                // Outline the full scope of the module
                let collapse = Range.endToEnd cmpRange rangeEnd
                let fullrange = Range.startToEnd moduleRange rangeEnd
                rcheck Scope.Module Collapse.Below fullrange collapse
                // A module's component info stores the ranges of its attributes
                parseAttributes attrs
                collectSigOpens decls
                List.iter parseModuleSigDeclaration decls
            | _ -> ()

        let parseModuleOrNamespaceSigs (SynModuleOrNamespaceSig(longId, _, kind, decls, _, attribs, _, r)) =
            parseAttributes attribs
            let rangeEnd = lastModuleSigDeclRangeElse r decls
            let idrange = longIdentRange longId
            let fullrange = Range.startToEnd idrange rangeEnd
            let collapse = Range.endToEnd idrange rangeEnd
            
            if kind.IsModule then
                rcheck Scope.Module Collapse.Below fullrange collapse

            collectSigHashDirectives decls
            collectSigOpens decls
            List.iter parseModuleSigDeclaration decls

        match parsedInput with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = modules)) ->
            modules |> List.iter parseModuleOrNamespace
            getCommentRanges sourceLines
        | ParsedInput.SigFile (ParsedSigFileInput (modules = moduleSigs)) ->
            List.iter parseModuleOrNamespaceSigs moduleSigs
            getCommentRanges sourceLines
        
        acc :> seq<_>
