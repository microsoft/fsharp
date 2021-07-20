// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace rec FSharp.Compiler.Syntax

open System.Diagnostics
open Internal.Utilities.Library
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Compiler.Xml

[<Struct; NoEquality; NoComparison; DebuggerDisplay("{idText}")>]
type Ident (text: string, range: range) =
     member _.idText = text
     member _.idRange = range
     override _.ToString() = text

type LongIdent = Ident list

type LongIdentWithDots =
    | LongIdentWithDots of id: LongIdent * dotRanges: range list

    member this.Range =
       match this with
       | LongIdentWithDots([], _) -> failwith "rangeOfLidwd"
       | LongIdentWithDots([id], []) -> id.idRange
       | LongIdentWithDots([id], [m]) -> unionRanges id.idRange m
       | LongIdentWithDots(h :: t, []) -> unionRanges h.idRange (List.last t).idRange
       | LongIdentWithDots(h :: t, dotRanges) -> unionRanges h.idRange (List.last t).idRange |> unionRanges (List.last dotRanges)

    member this.Lid = match this with LongIdentWithDots(lid, _) -> lid

    member this.ThereIsAnExtraDotAtTheEnd = match this with LongIdentWithDots(lid, dots) -> lid.Length = dots.Length

    member this.RangeWithoutAnyExtraDot =
       match this with
       | LongIdentWithDots([], _) -> failwith "rangeOfLidwd"
       | LongIdentWithDots([id], _) -> id.idRange
       | LongIdentWithDots(h :: t, dotRanges) ->
           let nonExtraDots = if dotRanges.Length = t.Length then dotRanges else List.truncate t.Length dotRanges
           unionRanges h.idRange (List.last t).idRange |> unionRanges (List.last nonExtraDots)

[<RequireQualifiedAccess>]
type ParserDetail =
    | Ok

    | ErrorRecovery

[<RequireQualifiedAccess>]
type TyparStaticReq =
    | None

    | HeadType

[<NoEquality; NoComparison>]
type SynTypar =
    | SynTypar of ident: Ident * staticReq: TyparStaticReq * isCompGen: bool

    member this.Range =
        match this with
        | SynTypar(id, _, _) ->
            id.idRange

[<Struct; RequireQualifiedAccess>]
type SynStringKind =
    | Regular
    | Verbatim
    | TripleQuote

[<Struct; RequireQualifiedAccess>]
type SynByteStringKind =
    | Regular
    | Verbatim

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynConst =

    | Unit

    | Bool of bool

    | SByte of sbyte

    | Byte of byte

    | Int16 of int16

    | UInt16 of uint16

    | Int32 of int32

    | UInt32 of uint32

    | Int64 of int64

    | UInt64 of uint64

    | IntPtr of int64

    | UIntPtr of uint64

    | Single of single

    | Double of double

    | Char of char

    | Decimal of System.Decimal

    | UserNum of value: string * suffix: string

    | String of text: string * synStringKind :SynStringKind * range: range

    | Bytes of bytes: byte[] * synByteStringKind: SynByteStringKind * range: range

    | UInt16s of uint16[]

    | Measure of constant: SynConst * constantRange: Range * SynMeasure
    
    | SourceIdentifier of constant: string * value: string * range: Range

    member c.Range dflt =
        match c with
        | SynConst.String (_, _, m0)
        | SynConst.Bytes (_, _, m0)
        | SynConst.SourceIdentifier(_, _, m0) -> m0
        | _ -> dflt

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynMeasure =

    | Named of longId: LongIdent * range: range

    | Product of measure1: SynMeasure * measure2: SynMeasure * range: range

    | Seq of measures: SynMeasure list * range: range

    | Divide of measure1: SynMeasure * measure2: SynMeasure * range: range

    | Power of measure: SynMeasure * power: SynRationalConst * range: range

    | One

    | Anon of range: range

    | Var of typar: SynTypar * range: range

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynRationalConst =
   
    | Integer of value: int32

    | Rational of numerator: int32 * denominator: int32 * range: range

    | Negate of SynRationalConst

[<RequireQualifiedAccess>]
type SynAccess =
    | Public

    | Internal

    | Private

    override this.ToString () =
        match this with
        | Public -> "Public"
        | Internal -> "Internal"
        | Private -> "Private"

[<RequireQualifiedAccess>]
type DebugPointForTarget =
    | Yes
    | No

[<RequireQualifiedAccess>]
type DebugPointAtSequential =
    | SuppressNeither
    | SuppressStmt
    | SuppressBoth
    | SuppressExpr

[<RequireQualifiedAccess>]
type DebugPointAtTry =
    | Yes of range: range
    | Body
    | No

[<RequireQualifiedAccess>]
type DebugPointAtWith =
    | Yes of range: range
    | No

[<RequireQualifiedAccess>]
type DebugPointAtFinally =
    | Yes of range: range
    | Body
    | No

[<RequireQualifiedAccess>]
type DebugPointAtFor =
    | Yes of range: range
    | No

[<RequireQualifiedAccess>]
type DebugPointAtWhile =
    | Yes of range: range
    | No

[<RequireQualifiedAccess>]
type DebugPointAtBinding =
    | Yes of range: range

    | NoneAtDo

    | NoneAtLet

    | NoneAtSticky

    | NoneAtInvisible

    member x.Combine(y: DebugPointAtBinding) =
        match x, y with
        | DebugPointAtBinding.Yes _ as g, _  -> g
        | _, (DebugPointAtBinding.Yes _ as g)  -> g
        | _ -> x

type SeqExprOnly =
    | SeqExprOnly of bool

type BlockSeparator = range * pos option

type RecordFieldName = LongIdentWithDots * bool

[<RequireQualifiedAccess>]
type ExprAtomicFlag =
    | Atomic = 0
    | NonAtomic = 1

[<RequireQualifiedAccess>]
type SynBindingKind =

    | StandaloneExpression

    | Normal

    | Do

[<NoEquality; NoComparison>]
type SynTyparDecl =
    | SynTyparDecl of attributes: SynAttributes * SynTypar

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynTypeConstraint =

    | WhereTyparIsValueType of
        typar: SynTypar *
        range: range

    | WhereTyparIsReferenceType of
        typar: SynTypar *
        range: range

    | WhereTyparIsUnmanaged of
        typar: SynTypar *
        range: range

    | WhereTyparSupportsNull of
        typar: SynTypar *
        range: range

    | WhereTyparIsComparable of
        typar: SynTypar *
        range: range

    | WhereTyparIsEquatable of
        typar: SynTypar *
        range: range

    | WhereTyparDefaultsToType of
        typar: SynTypar *
        typeName: SynType *
        range: range

    | WhereTyparSubtypeOfType of
        typar: SynTypar *
        typeName: SynType *
        range: range

    | WhereTyparSupportsMember of
        typars: SynType list *
        memberSig: SynMemberSig *
        range: range

    | WhereTyparIsEnum of
        typar: SynTypar *
        typeArgs: SynType list *
        range: range

    | WhereTyparIsDelegate of
       typar: SynTypar *
       typeArgs: SynType list *
       range: range

    member x.Range =
        match x with
        | WhereTyparIsValueType(range=range)
        | WhereTyparIsReferenceType(range=range)
        | WhereTyparIsUnmanaged(range=range)
        | WhereTyparSupportsNull(range=range)
        | WhereTyparIsComparable(range=range)
        | WhereTyparIsEquatable(range=range)
        | WhereTyparDefaultsToType(range=range)
        | WhereTyparSubtypeOfType(range=range)
        | WhereTyparSupportsMember(range=range)
        | WhereTyparIsEnum(range=range)
        | WhereTyparIsDelegate(range=range) -> range

[<RequireQualifiedAccess>]
type SynTyparDecls =
    | PostfixList of decls: SynTyparDecl list * constraints: SynTypeConstraint list * range: range
    | PrefixList of decls: SynTyparDecl list * range: range
    | SinglePrefix of decl: SynTyparDecl * range: range

    member x.TyparDecls =
        match x with
        | PostfixList (decls=decls)
        | PrefixList (decls=decls) -> decls
        | SinglePrefix (decl, _) -> [decl]

    member x.Constraints =
        match x with
        | PostfixList (constraints=constraints) -> constraints
        | _ -> []

    member x.Range =
        match x with
        | PostfixList (range=range)
        | PrefixList (range=range) -> range
        | SinglePrefix (range=range) -> range

[<NoEquality; NoComparison;RequireQualifiedAccess>]
type SynType = 
    
    | LongIdent of
        longDotId: LongIdentWithDots

    | App of
        typeName: SynType *
        lessRange: range option *
        typeArgs: SynType list *
        commaRanges: range list * // interstitial commas
        greaterRange: range option *
        isPostfix: bool *
        range: range

    | LongIdentApp of
        typeName: SynType *
        longDotId: LongIdentWithDots *
        lessRange: range option *
        typeArgs: SynType list *
        commaRanges: range list * // interstitial commas
        greaterRange: range option *
        range: range

    | Tuple of
        isStruct: bool *
        elementTypes:(bool*SynType) list *
        range: range

    | AnonRecd of
        isStruct: bool *
        fields:(Ident * SynType) list *
        range: range

    | Array of
        rank: int *
        elementType: SynType *
        range: range

    | Fun of
        argType: SynType *
        returnType: SynType *
        range: range

    | Var of
        typar: SynTypar *
        range: range

    | Anon of range: range

    | WithGlobalConstraints of
        typeName: SynType *
        constraints: SynTypeConstraint list *
        range: range

    | HashConstraint of
        innerType: SynType *
        range: range

    | MeasureDivide of
        dividend: SynType *
        divisor: SynType *
        range: range

    | MeasurePower of
        baseMeasure: SynType *
        exponent: SynRationalConst *
        range: range

    | StaticConstant of
        constant: SynConst *
        range: range

    | StaticConstantExpr of
        expr: SynExpr *
        range: range

    | StaticConstantNamed of
       ident: SynType *
       value: SynType *
       range: range

    | Paren of
      innerType: SynType *
      range: range

    member x.Range =
        match x with
        | SynType.App (range=m)
        | SynType.LongIdentApp (range=m)
        | SynType.Tuple (range=m)
        | SynType.Array (range=m)
        | SynType.AnonRecd (range=m)
        | SynType.Fun (range=m)
        | SynType.Var (range=m)
        | SynType.Anon (range=m)
        | SynType.WithGlobalConstraints (range=m)
        | SynType.StaticConstant (range=m)
        | SynType.StaticConstantExpr (range=m)
        | SynType.StaticConstantNamed (range=m)
        | SynType.HashConstraint (range=m)
        | SynType.MeasureDivide (range=m)
        | SynType.MeasurePower (range=m)
        | SynType.Paren (range=m) -> m
        | SynType.LongIdent lidwd -> lidwd.Range

[<NoEquality; NoComparison;RequireQualifiedAccess>]
type SynExpr =

    | Paren of
        expr: SynExpr *
        leftParenRange: range *
        rightParenRange: range option *
        range: range

    | Quote of
        operator: SynExpr *
        isRaw: bool *
        quotedExpr: SynExpr *
        isFromQueryExpression: bool *
        range: range

    | Const of
        constant: SynConst *
        range: range

    | Typed of
        expr: SynExpr *
        targetType: SynType *
        range: range

    | Tuple of
        isStruct: bool *
        exprs: SynExpr list *
        commaRanges: range list * // interstitial commas
        range: range 

    | AnonRecd of
        isStruct: bool *
        copyInfo:(SynExpr * BlockSeparator) option *
        recordFields:(Ident * SynExpr) list *
        range: range

    | ArrayOrList of
        isArray: bool *
        exprs: SynExpr list *
        range: range

    | Record of
        baseInfo:(SynType * SynExpr * range * BlockSeparator option * range) option *
        copyInfo:(SynExpr * BlockSeparator) option *
        recordFields:(RecordFieldName * (SynExpr option) * BlockSeparator option) list *
        range: range

    | New of
        isProtected: bool *
        targetType: SynType *
        expr: SynExpr *
        range: range

    | ObjExpr of
        objType: SynType *
        argOptions:(SynExpr * Ident option) option *
        bindings: SynBinding list *
        extraImpls: SynInterfaceImpl list *
        newExprRange: range *
        range: range

    | While of
        whileDebugPoint: DebugPointAtWhile *
        whileExpr: SynExpr *
        doExpr: SynExpr *
        range: range

    | For of
        forDebugPoint: DebugPointAtFor *
        ident: Ident *
        identBody: SynExpr *
        direction: bool *
        toBody: SynExpr *
        doBody: SynExpr *
        range: range

    | ForEach of
        forDebugPoint: DebugPointAtFor *
        seqExprOnly: SeqExprOnly *
        isFromSource: bool *
        pat: SynPat *
        enumExpr: SynExpr *
        bodyExpr: SynExpr *
        range: range

    | ArrayOrListOfSeqExpr of
        isArray: bool *
        expr: SynExpr *
        range: range

    | CompExpr of
        isArrayOrList: bool *
        isNotNakedRefCell: bool ref *
        expr: SynExpr *
        range: range

    | Lambda of
        fromMethod: bool *
        inLambdaSeq: bool *
        args: SynSimplePats *
        body: SynExpr *
        parsedData: (SynPat list * SynExpr) option *
        range: range

    | MatchLambda of
        isExnMatch: bool *
        keywordRange: range *
        matchClauses: SynMatchClause list *
        matchDebugPoint: DebugPointAtBinding *
        range: range

    | Match of
        matchDebugPoint: DebugPointAtBinding *
        expr: SynExpr *
        clauses: SynMatchClause list *
        range: range 

    | Do of
        expr: SynExpr *
        range: range

    | Assert of
        expr: SynExpr *
        range: range

    | App of
        flag: ExprAtomicFlag *
        isInfix: bool *
        funcExpr: SynExpr *
        argExpr: SynExpr *
        range: range

    | TypeApp of
        expr: SynExpr *
        lessRange: range *
        typeArgs: SynType list *
        commaRanges: range list *
        greaterRange: range option *
        typeArgsRange: range *
        range: range

    | LetOrUse of
        isRecursive: bool *
        isUse: bool *
        bindings: SynBinding list *
        body: SynExpr *
        range: range

    | TryWith of
        tryExpr: SynExpr *
        tryRange: range *
        withCases: SynMatchClause list *
        withRange: range *
        range: range *
        tryDebugPoint: DebugPointAtTry *
        withDebugPoint: DebugPointAtWith

    | TryFinally of
        tryExpr: SynExpr *
        finallyExpr: SynExpr *
        range: range *
        tryDebugPoint: DebugPointAtTry *
        finallyDebugPoint: DebugPointAtFinally

    | Lazy of
        expr: SynExpr *
        range: range

    | Sequential of
        debugPoint: DebugPointAtSequential *
        isTrueSeq: bool *
        expr1: SynExpr *
        expr2: SynExpr *
        range: range

    | IfThenElse of
        ifKeyword: SynIfThenElseStart *
        ifExpr: SynExpr *
        thenKeyword: range *
        thenExpr: SynExpr *
        elseKeyword: range option *
        elseExpr: SynExpr option *
        spIfToThen: DebugPointAtBinding *
        isFromErrorRecovery: bool *
        ifToThenRange: range *
        range: range

    | Ident of
        ident: Ident

    | LongIdent of
        isOptional: bool *
        longDotId: LongIdentWithDots *
        altNameRefCell: SynSimplePatAlternativeIdInfo ref option *
        range: range

    | LongIdentSet of
        longDotId: LongIdentWithDots *
        expr: SynExpr *
        range: range

    | DotGet of
        expr: SynExpr *
        rangeOfDot: range *
        longDotId: LongIdentWithDots *
        range: range

    | DotSet of
        targetExpr: SynExpr *
        longDotId: LongIdentWithDots *
        rhsExpr: SynExpr *
        range: range

    | Set of
        targetExpr: SynExpr *
        rhsExpr: SynExpr *
        range: range

    | DotIndexedGet of
        objectExpr: SynExpr *
        indexExprs: SynIndexerArg list *
        dotRange: range *
        range: range

    | DotIndexedSet of
        objectExpr: SynExpr *
        indexExprs: SynIndexerArg list *
        valueExpr: SynExpr *
        leftOfSetRange: range *
        dotRange: range *
        range: range

    | NamedIndexedPropertySet of
        longDotId: LongIdentWithDots *
        expr1: SynExpr *
        expr2: SynExpr *
        range: range

    | DotNamedIndexedPropertySet of
        targetExpr: SynExpr *
        longDotId: LongIdentWithDots *
        argExpr: SynExpr *
        rhsExpr: SynExpr *
        range: range

    | TypeTest of
        expr: SynExpr *
        targetType: SynType *
        range: range

    | Upcast of
        expr: SynExpr *
        targetType: SynType *
        range: range

    | Downcast of
        expr: SynExpr *
        targetType: SynType *
        range: range

    | InferredUpcast of
        expr: SynExpr *
        range: range

    | InferredDowncast of
        expr: SynExpr *
        range: range

    | Null of
        range: range

    | AddressOf of
        isByref: bool *
        expr: SynExpr *
        opRange: range *
        range: range

    | TraitCall of
        supportTys: SynTypar list *
        traitSig: SynMemberSig *
        argExpr: SynExpr *
        range: range

    | JoinIn of
        lhsExpr: SynExpr *
        lhsRange: range *
        rhsExpr: SynExpr *
        range: range

    | ImplicitZero of
        range: range

    | SequentialOrImplicitYield of
        debugPoint:DebugPointAtSequential *
        expr1:SynExpr *
        expr2:SynExpr *
        ifNotStmt:SynExpr *
        range:range

    | YieldOrReturn of
        flags: (bool * bool) *
        expr: SynExpr *
        range: range

    | YieldOrReturnFrom of
        flags: (bool * bool) *
        expr: SynExpr *
        range: range

    | LetOrUseBang of
        bindDebugPoint: DebugPointAtBinding *
        isUse: bool *
        isFromSource: bool *
        pat: SynPat *
        rhs: SynExpr *
        andBangs:(DebugPointAtBinding * bool * bool * SynPat * SynExpr * range) list *
        body:SynExpr *
        range: range 

    | MatchBang of
        matchDebugPoint: DebugPointAtBinding *
        expr: SynExpr *
        clauses: SynMatchClause list *
        range: range

    | DoBang of
        expr: SynExpr *
        range: range

    | LibraryOnlyILAssembly of
        ilCode: obj * // this type is ILInstr[]  but is hidden to avoid the representation of AbstractIL being public
        typeArgs: SynType list *
        args: SynExpr list *
        retTy: SynType list *
        range: range

    | LibraryOnlyStaticOptimization of
        constraints: SynStaticOptimizationConstraint list *
        expr: SynExpr *
        optimizedExpr: SynExpr *
        range: range

    | LibraryOnlyUnionCaseFieldGet of
        expr: SynExpr *
        longId: LongIdent *
        fieldNum: int *
        range: range

    | LibraryOnlyUnionCaseFieldSet of
        expr: SynExpr *
        longId: LongIdent *
        fieldNum: int *
        rhsExpr: SynExpr *
        range: range

    | ArbitraryAfterError of
        debugStr: string *
        range: range

    | FromParseError of
        expr: SynExpr *
        range: range

    | DiscardAfterMissingQualificationAfterDot of
        expr: SynExpr *
        range: range

    | Fixed of
        expr: SynExpr *
        range: range

    | InterpolatedString of
        contents: SynInterpolatedStringPart list *
        synStringKind :SynStringKind *
        range: range

    member e.Range =
        match e with
        | SynExpr.Paren (_, leftParenRange, rightParenRange, r) ->
            match rightParenRange with
            | Some rightParenRange when leftParenRange.FileIndex <> rightParenRange.FileIndex -> leftParenRange
            | _ -> r
        | SynExpr.Quote (range=m)
        | SynExpr.Const (range=m)
        | SynExpr.Typed (range=m)
        | SynExpr.Tuple (range=m)
        | SynExpr.AnonRecd (range=m)
        | SynExpr.ArrayOrList (range=m)
        | SynExpr.Record (range=m)
        | SynExpr.New (range=m)
        | SynExpr.ObjExpr (range=m)
        | SynExpr.While (range=m)
        | SynExpr.For (range=m)
        | SynExpr.ForEach (range=m)
        | SynExpr.CompExpr (range=m)
        | SynExpr.ArrayOrListOfSeqExpr (range=m)
        | SynExpr.Lambda (range=m)
        | SynExpr.Match (range=m)
        | SynExpr.MatchLambda (range=m)
        | SynExpr.Do (range=m)
        | SynExpr.Assert (range=m)
        | SynExpr.App (range=m)
        | SynExpr.TypeApp (range=m)
        | SynExpr.LetOrUse (range=m)
        | SynExpr.TryWith (range=m)
        | SynExpr.TryFinally (range=m)
        | SynExpr.Sequential (range=m)
        | SynExpr.SequentialOrImplicitYield (range=m)
        | SynExpr.ArbitraryAfterError (range=m)
        | SynExpr.FromParseError (range=m)
        | SynExpr.DiscardAfterMissingQualificationAfterDot (range=m)
        | SynExpr.IfThenElse (range=m)
        | SynExpr.LongIdent (range=m)
        | SynExpr.LongIdentSet (range=m)
        | SynExpr.NamedIndexedPropertySet (range=m)
        | SynExpr.DotIndexedGet (range=m)
        | SynExpr.DotIndexedSet (range=m)
        | SynExpr.DotGet (range=m)
        | SynExpr.DotSet (range=m)
        | SynExpr.Set (range=m)
        | SynExpr.DotNamedIndexedPropertySet (range=m)
        | SynExpr.LibraryOnlyUnionCaseFieldGet (range=m)
        | SynExpr.LibraryOnlyUnionCaseFieldSet (range=m)
        | SynExpr.LibraryOnlyILAssembly (range=m)
        | SynExpr.LibraryOnlyStaticOptimization (range=m)
        | SynExpr.TypeTest (range=m)
        | SynExpr.Upcast (range=m)
        | SynExpr.AddressOf (range=m)
        | SynExpr.Downcast (range=m)
        | SynExpr.JoinIn (range=m)
        | SynExpr.InferredUpcast (range=m)
        | SynExpr.InferredDowncast (range=m)
        | SynExpr.Null (range=m)
        | SynExpr.Lazy (range=m)
        | SynExpr.TraitCall (range=m)
        | SynExpr.ImplicitZero (range=m)
        | SynExpr.YieldOrReturn (range=m)
        | SynExpr.YieldOrReturnFrom (range=m)
        | SynExpr.LetOrUseBang (range=m)
        | SynExpr.MatchBang (range=m)
        | SynExpr.DoBang (range=m)
        | SynExpr.Fixed (range=m) 
        | SynExpr.InterpolatedString (range=m) -> m
        | SynExpr.Ident id -> id.idRange

    member e.RangeWithoutAnyExtraDot =
        match e with
        | SynExpr.DotGet (expr, _, lidwd, m) ->
            if lidwd.ThereIsAnExtraDotAtTheEnd then
                unionRanges expr.Range lidwd.RangeWithoutAnyExtraDot
            else
                m
        | SynExpr.LongIdent (_, lidwd, _, _) -> lidwd.RangeWithoutAnyExtraDot
        | SynExpr.DiscardAfterMissingQualificationAfterDot (expr, _) -> expr.Range
        | _ -> e.Range

    member e.RangeOfFirstPortion =
        match e with
        // these are better than just .Range, and also commonly applicable inside queries
        | SynExpr.Paren (_, m, _, _) -> m
        | SynExpr.Sequential (_, _, e1, _, _)
        | SynExpr.SequentialOrImplicitYield (_, e1, _, _, _)
        | SynExpr.App (_, _, e1, _, _) ->
            e1.RangeOfFirstPortion
        | SynExpr.ForEach (_, _, _, pat, _, _, r) ->
            let start = r.Start
            let e = (pat.Range: range).Start
            mkRange r.FileName start e
        | _ -> e.Range

    member this.IsArbExprAndThusAlreadyReportedError =
        match this with
        | SynExpr.ArbitraryAfterError _ -> true
        | _ -> false

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynIfThenElseStart =
    | If of range: Range
    | Elif of range: Range

    member this.Range =
        match this with
        | If m
        | Elif m -> m

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynInterpolatedStringPart =
    | String of value: string * range: range
    | FillExpr of fillExpr: SynExpr * qualifiers: Ident option

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynIndexerArg =
    | Two of
        expr1: SynExpr *
        fromEnd1: bool *
        expr2: SynExpr *
        fromEnd2: bool *
        range1: range *
        range2: range

    | One of
        expr: SynExpr *
        fromEnd: bool * range

    member x.Range = match x with Two (e1, _, e2, _, _, _) -> unionRanges e1.Range e2.Range | One (e, _, _) -> e.Range

    member x.Exprs = match x with Two (e1, _, e2, _, _, _) -> [e1;e2] | One (e, _, _) -> [e]

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynSimplePat =
    | Id of
        ident: Ident *
        altNameRefCell: SynSimplePatAlternativeIdInfo ref option *
        isCompilerGenerated: bool *
        isThisVal: bool *
        isOptional: bool *
        range: range

    | Typed of
        pat: SynSimplePat *
        targetType: SynType *
        range: range

    | Attrib of
        pat: SynSimplePat *
        attributes: SynAttributes *
        range: range

    member x.Range =
        match x with
        | SynSimplePat.Id(range=range)
        | SynSimplePat.Typed(range=range)
        | SynSimplePat.Attrib(range=range) -> range

[<RequireQualifiedAccess>]
type SynSimplePatAlternativeIdInfo =

    | Undecided of Ident

    | Decided of Ident

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynStaticOptimizationConstraint =

    | WhenTyparTyconEqualsTycon of
        typar: SynTypar *
        rhsType: SynType *
        range: range

    | WhenTyparIsStruct of
        typar: SynTypar *
        range: range

[<NoEquality; NoComparison;RequireQualifiedAccess>]
type SynSimplePats =
    | SimplePats of
        pats: SynSimplePat list *
        range: range

    | Typed of
        pats: SynSimplePats *
        targetType: SynType *
        range: range

    member x.Range =
        match x with
        | SynSimplePats.SimplePats(range=range)
        | SynSimplePats.Typed(range=range) -> range

[<RequireQualifiedAccess>]
type SynArgPats =
    | Pats of
        pats: SynPat list

    | NamePatPairs of
        pats: (Ident * SynPat) list *
        range: range

[<NoEquality; NoComparison;RequireQualifiedAccess>]
type SynPat =

    | Const of
        constant: SynConst *
        range: range

    | Wild of
        range: range

    | Named of
        ident: Ident *
        isThisVal: bool *
        accessibility: SynAccess option *
        range: range

    | Typed of
        pat: SynPat *
        targetType: SynType *
        range: range

    | Attrib of
        pat: SynPat *
        attributes: SynAttributes *
        range: range

    | Or of
        lhsPat: SynPat *
        rhsPat: SynPat *
        range: range

    | Ands of
        pats: SynPat list *
        range: range
        
    | As of
        lhsPat: SynPat *
        rhsPat: SynPat *
        range: range

    | LongIdent of
        longDotId: LongIdentWithDots *
        extraId: Ident option * // holds additional ident for tooling
        typarDecls: SynValTyparDecls option * // usually None: temporary used to parse "f<'a> x = x"
        argPats: SynArgPats *
        accessibility: SynAccess option *
        range: range

    | Tuple of
        isStruct: bool *
        elementPats: SynPat list *
        range: range

    | Paren of
        pat: SynPat *
        range: range

    | ArrayOrList of
        isArray: bool *
        elementPats: SynPat list *
        range: range

    | Record of
        fieldPats: ((LongIdent * Ident) * SynPat) list *
        range: range

    | Null of
        range: range

    | OptionalVal of
        ident: Ident *
        range: range

    | IsInst of
        pat: SynType *
        range: range

    | QuoteExpr of
        expr: SynExpr *
        range: range

    | DeprecatedCharRange of
        startChar: char *
        endChar: char *
        range: range

    | InstanceMember of
        thisId: Ident *
        memberId: Ident *
        toolingId: Ident option * // holds additional ident for tooling
        accessibility: SynAccess option *
        range: range

    | FromParseError of
        pat: SynPat *
        range: range

    member p.Range =
      match p with
      | SynPat.Const (range=m)
      | SynPat.Wild (range=m)
      | SynPat.Named (range=m)
      | SynPat.Or (range=m)
      | SynPat.Ands (range=m)
      | SynPat.As (range=m)
      | SynPat.LongIdent (range=m)
      | SynPat.ArrayOrList (range=m)
      | SynPat.Tuple (range=m)
      | SynPat.Typed (range=m)
      | SynPat.Attrib (range=m)
      | SynPat.Record (range=m)
      | SynPat.DeprecatedCharRange (range=m)
      | SynPat.Null (range=m)
      | SynPat.IsInst (range=m)
      | SynPat.QuoteExpr (range=m)
      | SynPat.InstanceMember (range=m)
      | SynPat.OptionalVal (range=m)
      | SynPat.Paren (range=m)
      | SynPat.FromParseError (range=m) -> m

[<NoEquality; NoComparison;>]
type SynInterfaceImpl =
    | SynInterfaceImpl of
        interfaceTy: SynType *
        bindings: SynBinding list *
        range: range

[<NoEquality; NoComparison>]
type SynMatchClause =
    | SynMatchClause of
        pat: SynPat *
        whenExpr: SynExpr option *
        resultExpr: SynExpr *
        range: range *
        debugPoint: DebugPointForTarget

    member this.RangeOfGuardAndRhs =
        match this with
        | SynMatchClause(_, eo, e, _, _) ->
            match eo with
            | None -> e.Range
            | Some x -> unionRanges e.Range x.Range

    member this.Range =
        match this with
        | SynMatchClause(range = m) -> m

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynAttribute =
    { TypeName: LongIdentWithDots

      ArgExpr: SynExpr

      Target: Ident option

      AppliesToGetterAndSetter: bool

      Range: range
    }

[<RequireQualifiedAccess>]
type SynAttributeList =
    { 
      Attributes: SynAttribute list
      
      Range: range
    }

type SynAttributes = SynAttributeList list

[<NoEquality; NoComparison>]
type SynValData =
    | SynValData of
        memberFlags: SynMemberFlags option *
        valInfo: SynValInfo *
        thisIdOpt: Ident option

    member x.SynValInfo = (let (SynValData(_flags, synValInfo, _)) = x in synValInfo)

[<NoEquality; NoComparison>]
type SynBinding =
    | SynBinding of
        accessibility: SynAccess option *
        kind: SynBindingKind *
        isInline: bool *
        isMutable: bool *
        attributes: SynAttributes *
        xmlDoc: PreXmlDoc *
        valData: SynValData *
        headPat: SynPat *
        returnInfo: SynBindingReturnInfo option *
        expr: SynExpr  *
        range: range *
        debugPoint: DebugPointAtBinding

    // no member just named "Range", as that would be confusing:
    //  - for everything else, the 'range' member that appears last/second-to-last is the 'full range' of the whole tree construct
    //  - but for Binding, the 'range' is only the range of the left-hand-side, the right-hand-side range is in the SynExpr
    //  - so we use explicit names to avoid confusion
    member x.RangeOfBindingWithoutRhs = let (SynBinding(range=m)) = x in m

    member x.RangeOfBindingWithRhs = let (SynBinding(expr=e; range=m)) = x in unionRanges e.Range m

    member x.RangeOfHeadPattern = let (SynBinding(headPat=headPat)) = x in headPat.Range

[<NoEquality; NoComparison>]
type SynBindingReturnInfo =
    | SynBindingReturnInfo of
        typeName: SynType *
        range: range *
        attributes: SynAttributes

[<NoComparison; RequireQualifiedAccess>]
type SynMemberFlags =
    { 
      IsInstance: bool

      IsDispatchSlot: bool

      IsOverrideOrExplicitImpl: bool

      IsFinal: bool

      MemberKind: SynMemberKind
    }

[<StructuralEquality; NoComparison; RequireQualifiedAccess>]
type SynMemberKind =

    | ClassConstructor

    | Constructor

    | Member

    | PropertyGet

    | PropertySet

    | PropertyGetSet

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynMemberSig =

    | Member of
        memberSig: SynValSig *
        flags: SynMemberFlags *
        range: range

    | Interface of
        interfaceType: SynType *
        range: range

    | Inherit of
        inheritedType: SynType *
        range: range

    | ValField of
        field: SynField *
        range: range

    | NestedType of
        nestedType: SynTypeDefnSig *
        range: range

    member d.Range =
        match d with
        | SynMemberSig.Member (range=m)
        | SynMemberSig.Interface (range=m)
        | SynMemberSig.Inherit (range=m)
        | SynMemberSig.ValField (range=m)
        | SynMemberSig.NestedType (range=m) -> m

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynTypeDefnKind =
    | Unspecified
    | Class
    | Interface
    | Struct
    | Record
    | Union
    | Abbrev
    | Opaque
    | Augmentation
    | IL
    | Delegate of signature: SynType * signatureInfo: SynValInfo

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynTypeDefnSimpleRepr =

    | Union of
        accessibility: SynAccess option *
        unionCases: SynUnionCase list *
        range: range

    | Enum of
        cases: SynEnumCase list *
        range: range

    | Record of
        accessibility: SynAccess option *
        recordFields: SynField list *
        range: range

    | General of
        kind: SynTypeDefnKind *
        inherits: (SynType * range * Ident option) list *
        slotsigs: (SynValSig * SynMemberFlags) list *
        fields: SynField list *
        isConcrete: bool *
        isIncrClass: bool *
        implicitCtorSynPats: SynSimplePats option *
        range: range

    | LibraryOnlyILAssembly of
        ilType: obj * // this type is ILType but is hidden to avoid the representation of AbstractIL being public
        range: range

    | TypeAbbrev of
        detail: ParserDetail *
        rhsType: SynType *
        range: range

    | None of
        range: range

    | Exception of
        exnRepr: SynExceptionDefnRepr

    member this.Range =
        match this with
        | Union (range=m)
        | Enum (range=m)
        | Record (range=m)
        | General (range=m)
        | LibraryOnlyILAssembly (range=m)
        | TypeAbbrev (range=m)
        | None (range=m) -> m
        | Exception t -> t.Range

[<NoEquality; NoComparison>]
type SynEnumCase =

    | SynEnumCase of
        attributes: SynAttributes *
        ident: Ident * 
        value: SynConst *
        valueRange: range *
        xmlDoc: PreXmlDoc *
        range: range

    member this.Range =
        match this with
        | SynEnumCase (range=m) -> m

[<NoEquality; NoComparison>]
type SynUnionCase =

    | SynUnionCase of
        attributes: SynAttributes *
        ident: Ident *
        caseType: SynUnionCaseKind *
        xmlDoc: PreXmlDoc *
        accessibility: SynAccess option *
        range: range

    member this.Range =
        match this with
        | SynUnionCase (range=m) -> m

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynUnionCaseKind =

    | Fields of
        cases: SynField list

    | FullType of
        fullType: SynType *
        fullTypeInfo: SynValInfo

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynTypeDefnSigRepr =

    | ObjectModel of
        kind: SynTypeDefnKind *
        memberSigs: SynMemberSig list *
        range: range

    | Simple of
        repr: SynTypeDefnSimpleRepr *
        range: range

    | Exception of
        repr: SynExceptionDefnRepr

    member this.Range =
        match this with
        | ObjectModel (range=m)
        | Simple (range=m) -> m
        | Exception e -> e.Range

[<NoEquality; NoComparison>]
type SynTypeDefnSig =

    | SynTypeDefnSig of
        typeInfo: SynComponentInfo *
        typeRepr: SynTypeDefnSigRepr *
        members: SynMemberSig list *
        range: range

    member this.Range =
        match this with
        | SynTypeDefnSig(range=m) -> m

[<NoEquality; NoComparison>]
type SynField =
    | SynField of
        attributes: SynAttributes *
        isStatic: bool *
        idOpt: Ident option *
        fieldType: SynType *
        isMutable: bool *
        xmlDoc: PreXmlDoc *
        accessibility: SynAccess option *
        range: range

[<NoEquality; NoComparison>]
type SynComponentInfo =
    | SynComponentInfo of
        attributes: SynAttributes *
        typeParams: SynTyparDecls option *
        constraints: SynTypeConstraint list *
        longId: LongIdent *
        xmlDoc: PreXmlDoc *
        preferPostfix: bool *
        accessibility: SynAccess option *
        range: range

    member this.Range =
        match this with
        | SynComponentInfo (range=m) -> m

[<NoEquality; NoComparison>]
type SynValSig =
    | SynValSig of
        attributes: SynAttributes *
        ident: Ident *
        explicitValDecls: SynValTyparDecls *
        synType: SynType *
        arity: SynValInfo *
        isInline: bool *
        isMutable: bool *
        xmlDoc: PreXmlDoc *
        accessibility: SynAccess option *
        synExpr: SynExpr option *
        range: range

    member x.RangeOfId  = let (SynValSig(ident=id)) = x in id.idRange

    member x.SynInfo = let (SynValSig(arity=v)) = x in v

    member x.SynType = let (SynValSig(synType=ty)) = x in ty

[<NoEquality; NoComparison>]
type SynValInfo =

    | SynValInfo of curriedArgInfos: SynArgInfo list list * returnInfo: SynArgInfo

    member x.CurriedArgInfos = (let (SynValInfo(args, _)) = x in args)

    member x.ArgNames =
        x.CurriedArgInfos 
        |> List.concat 
        |> List.map (fun info -> info.Ident) 
        |> List.choose id 
        |> List.map (fun id -> id.idText)

[<NoEquality; NoComparison>]
type SynArgInfo =

    | SynArgInfo of
        attributes: SynAttributes *
        optional: bool *
        ident: Ident option

    member x.Ident : Ident option = let (SynArgInfo(_,_,id)) = x in id

    member x.Attributes : SynAttributes = let (SynArgInfo(attrs,_,_)) = x in attrs

[<NoEquality; NoComparison>]
type SynValTyparDecls =
    | SynValTyparDecls of
        typars: SynTyparDecls option *
        canInfer: bool

[<NoEquality; NoComparison>]
type SynReturnInfo =
    | SynReturnInfo of returnType: (SynType * SynArgInfo) * range: range

[<NoEquality; NoComparison>]
type SynExceptionDefnRepr =

    | SynExceptionDefnRepr of
        attributes: SynAttributes *
        caseName: SynUnionCase *
        longId: LongIdent option *
        xmlDoc: PreXmlDoc *
        accessibility: SynAccess option *
        range: range

    member this.Range = match this with SynExceptionDefnRepr (range=m) -> m

[<NoEquality; NoComparison>]
type SynExceptionDefn =

    | SynExceptionDefn of
        exnRepr: SynExceptionDefnRepr *
        members: SynMemberDefns *
        range: range

    member this.Range =
        match this with
        | SynExceptionDefn (range=m) -> m

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynTypeDefnRepr =

    | ObjectModel of
        kind: SynTypeDefnKind *
        members: SynMemberDefns *
        range: range

    | Simple of
        simpleRepr: SynTypeDefnSimpleRepr *
        range: range

    | Exception of
        exnRepr: SynExceptionDefnRepr

    member this.Range =
        match this with
        | ObjectModel (range=m)
        | Simple (range=m) -> m
        | Exception t -> t.Range

[<NoEquality; NoComparison>]
type SynTypeDefn =
    | SynTypeDefn of
        typeInfo: SynComponentInfo *
        typeRepr: SynTypeDefnRepr *
        members: SynMemberDefns *
        implicitConstructor: SynMemberDefn option *
        range: range

    member this.Range =
        match this with
        | SynTypeDefn (range=m) -> m

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynMemberDefn =

    | Open of
        target: SynOpenDeclTarget *
        range: range

    | Member of
        memberDefn: SynBinding *
        range: range

    | ImplicitCtor of
        accessibility: SynAccess option *
        attributes: SynAttributes *
        ctorArgs: SynSimplePats *
        selfIdentifier: Ident option *
        xmlDoc: PreXmlDoc *
        range: range

    | ImplicitInherit of
        inheritType: SynType *
        inheritArgs: SynExpr *
        inheritAlias: Ident option *
        range: range

    | LetBindings of
        bindings: SynBinding list *
        isStatic: bool *
        isRecursive: bool *
        range: range

    | AbstractSlot of
        slotSig: SynValSig *
        flags: SynMemberFlags *
        range: range

    | Interface of
        interfaceType: SynType *
        members: SynMemberDefns option *
        range: range

    | Inherit of
        baseType: SynType *
        asIdent: Ident option *
        range: range

    | ValField of
        fieldInfo: SynField *
        range: range

    | NestedType of
        typeDefn: SynTypeDefn *
        accessibility: SynAccess option *
        range: range

    | AutoProperty of
        attributes: SynAttributes *
        isStatic: bool *
        ident: Ident *
        typeOpt: SynType option *
        propKind: SynMemberKind *
        memberFlags:(SynMemberKind -> SynMemberFlags) *
        xmlDoc: PreXmlDoc *
        accessibility: SynAccess option *
        synExpr: SynExpr *
        getSetRange: range option *
        range: range

    member d.Range =
        match d with
        | SynMemberDefn.Member (range=m)
        | SynMemberDefn.Interface (range=m)
        | SynMemberDefn.Open (range=m)
        | SynMemberDefn.LetBindings (range=m)
        | SynMemberDefn.ImplicitCtor (range=m)
        | SynMemberDefn.ImplicitInherit (range=m)
        | SynMemberDefn.AbstractSlot (range=m)
        | SynMemberDefn.Inherit (range=m)
        | SynMemberDefn.ValField (range=m)
        | SynMemberDefn.AutoProperty (range=m)
        | SynMemberDefn.NestedType (range=m) -> m

type SynMemberDefns = SynMemberDefn list

[<NoEquality; NoComparison;RequireQualifiedAccess>]
type SynModuleDecl =

    | ModuleAbbrev of
        ident: Ident *
        longId: LongIdent *
        range: range

    | NestedModule of
        moduleInfo: SynComponentInfo *
        isRecursive: bool *
        decls: SynModuleDecl list *
        isContinuing: bool *
        range: range

    | Let of
        isRecursive: bool *
        bindings: SynBinding list *
        range: range

    | DoExpr of
       debugPoint: DebugPointAtBinding *
       expr: SynExpr *
       range: range

    | Types of
        typeDefns: SynTypeDefn list *
        range: range

    | Exception of
        exnDefn: SynExceptionDefn *
        range: range

    | Open of
        target: SynOpenDeclTarget *
        range: range

    | Attributes of
        attributes: SynAttributes *
        range: range

    | HashDirective of
        hashDirective: ParsedHashDirective *
        range: range

    | NamespaceFragment of
        fragment: SynModuleOrNamespace

    member d.Range =
        match d with
        | SynModuleDecl.ModuleAbbrev (range=m)
        | SynModuleDecl.NestedModule (range=m)
        | SynModuleDecl.Let (range=m)
        | SynModuleDecl.DoExpr (range=m)
        | SynModuleDecl.Types (range=m)
        | SynModuleDecl.Exception (range=m)
        | SynModuleDecl.Open (range=m)
        | SynModuleDecl.HashDirective (range=m)
        | SynModuleDecl.NamespaceFragment (SynModuleOrNamespace (range=m))
        | SynModuleDecl.Attributes (range=m) -> m

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynOpenDeclTarget = 

    | ModuleOrNamespace of longId: LongIdent * range: range

    | Type of typeName: SynType * range: range

    member this.Range =
        match this with
        | ModuleOrNamespace (range=m) -> m
        | Type (range=m) -> m

[<NoEquality; NoComparison>]
type SynExceptionSig =
    | SynExceptionSig of
        exnRepr: SynExceptionDefnRepr *
        members: SynMemberSig list *
        range: range

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynModuleSigDecl =

    | ModuleAbbrev of
        ident: Ident *
        longId: LongIdent *
        range: range

    | NestedModule of
        moduleInfo: SynComponentInfo *
        isRecursive: bool *
        moduleDecls: SynModuleSigDecl list *
        range: range

    | Val of
        valSig: SynValSig * range: range

    | Types of
        types: SynTypeDefnSig list *
        range: range

    | Exception of
        exnSig: SynExceptionSig *
        range: range

    | Open of
        target: SynOpenDeclTarget *
        range: range

    | HashDirective of
        hashDirective: ParsedHashDirective *
        range: range

    | NamespaceFragment of
        SynModuleOrNamespaceSig

    member d.Range =
        match d with
        | SynModuleSigDecl.ModuleAbbrev (range=m)
        | SynModuleSigDecl.NestedModule (range=m)
        | SynModuleSigDecl.Val (range=m)
        | SynModuleSigDecl.Types (range=m)
        | SynModuleSigDecl.Exception (range=m)
        | SynModuleSigDecl.Open (range=m)
        | SynModuleSigDecl.NamespaceFragment (SynModuleOrNamespaceSig.SynModuleOrNamespaceSig(range=m))
        | SynModuleSigDecl.HashDirective (range=m) -> m

[<Struct; RequireQualifiedAccess>]
type SynModuleOrNamespaceKind =
    | NamedModule

    | AnonModule

    | DeclaredNamespace

    | GlobalNamespace

    member x.IsModule =
        match x with
        | NamedModule | AnonModule -> true
        | _ -> false

[<NoEquality; NoComparison>]
type SynModuleOrNamespace =
    | SynModuleOrNamespace of
        longId: LongIdent *
        isRecursive: bool *
        kind: SynModuleOrNamespaceKind *
        decls: SynModuleDecl list *
        xmlDoc: PreXmlDoc *
        attribs: SynAttributes *
        accessibility: SynAccess option *
        range: range

    member this.Range =
        match this with
        | SynModuleOrNamespace (range=m) -> m

[<NoEquality; NoComparison>]
type SynModuleOrNamespaceSig =
    | SynModuleOrNamespaceSig of
        longId: LongIdent *
        isRecursive: bool *
        kind: SynModuleOrNamespaceKind *
        decls: SynModuleSigDecl list *
        xmlDoc: PreXmlDoc *
        attribs: SynAttributes *
        accessibility: SynAccess option *
        range: range

    member this.Range =
        match this with
        | SynModuleOrNamespaceSig (range=m) -> m

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type ParsedHashDirectiveArgument =
     | String of value: string * stringKind: SynStringKind * range: Range
     | SourceIdentifier of constant: string * value: string * range: Range

     member this.Range =
         match this with
         | ParsedHashDirectiveArgument.String (range=m)
         | ParsedHashDirectiveArgument.SourceIdentifier (range=m) -> m

[<NoEquality; NoComparison>]
type ParsedHashDirective =
    | ParsedHashDirective of
        ident: string *
        args: ParsedHashDirectiveArgument list *
        range: range

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type ParsedImplFileFragment =

    | AnonModule of
        decls: SynModuleDecl list *
        range: range

    | NamedModule of
        namedModule: SynModuleOrNamespace

    | NamespaceFragment of
        longId: LongIdent *
        isRecursive: bool *
        kind: SynModuleOrNamespaceKind *
        decls: SynModuleDecl list *
        xmlDoc: PreXmlDoc *
        attributes: SynAttributes *
        range: range

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type ParsedSigFileFragment =

    | AnonModule of
        decls: SynModuleSigDecl list *
        range: range

    | NamedModule of
        namedModule: SynModuleOrNamespaceSig

    | NamespaceFragment of
        longId: LongIdent *
        isRecursive: bool *
        kind: SynModuleOrNamespaceKind *
        decls: SynModuleSigDecl list *
        xmlDoc: PreXmlDoc *
        attributes: SynAttributes *
        range: range

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type ParsedScriptInteraction =
    | Definitions of
        defns: SynModuleDecl list *
        range: range

    | HashDirective of
        hashDirective: ParsedHashDirective *
        range: range

[<NoEquality; NoComparison>]
type ParsedImplFile =
    | ParsedImplFile of
        hashDirectives: ParsedHashDirective list *
        fragments: ParsedImplFileFragment list

[<NoEquality; NoComparison>]
type ParsedSigFile =
    | ParsedSigFile of
        hashDirectives: ParsedHashDirective list *
        fragments: ParsedSigFileFragment list

[<RequireQualifiedAccess>]
type ScopedPragma =
    | WarningOff of range: range * warningNumber: int

[<NoEquality; NoComparison>]
type QualifiedNameOfFile =
    | QualifiedNameOfFile of Ident

    member x.Text = (let (QualifiedNameOfFile t) = x in t.idText)

    member x.Id = (let (QualifiedNameOfFile t) = x in t)

    member x.Range = (let (QualifiedNameOfFile t) = x in t.idRange)

[<NoEquality; NoComparison>]
type ParsedImplFileInput =
    | ParsedImplFileInput of
        fileName: string *
        isScript: bool *
        qualifiedNameOfFile: QualifiedNameOfFile *
        scopedPragmas: ScopedPragma list *
        hashDirectives: ParsedHashDirective list *
        modules: SynModuleOrNamespace list *
        isLastCompiland: (bool * bool)

[<NoEquality; NoComparison>]
type ParsedSigFileInput =
    | ParsedSigFileInput of
        fileName: string *
        qualifiedNameOfFile: QualifiedNameOfFile *
        scopedPragmas: ScopedPragma list *
        hashDirectives: ParsedHashDirective list *
        modules: SynModuleOrNamespaceSig list

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type ParsedInput =
    | ImplFile of ParsedImplFileInput

    | SigFile of ParsedSigFileInput

    member inp.FileName =
        match inp with
        | ParsedInput.ImplFile (ParsedImplFileInput (fileName=filename))
        | ParsedInput.SigFile (ParsedSigFileInput (fileName=filename)) -> filename

    member inp.Range =
        match inp with
        | ParsedInput.ImplFile (ParsedImplFileInput (modules=SynModuleOrNamespace(range=m) :: _))
        | ParsedInput.SigFile (ParsedSigFileInput (modules=SynModuleOrNamespaceSig(range=m) :: _)) -> m
        | ParsedInput.ImplFile (ParsedImplFileInput (fileName=filename))
        | ParsedInput.SigFile (ParsedSigFileInput (fileName=filename)) -> rangeN filename 0
