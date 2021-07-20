// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace rec FSharp.Compiler.Syntax

open FSharp.Compiler.Syntax
open FSharp.Compiler.Text
open FSharp.Compiler.Xml

/// Represents an identifier in F# code
[<Struct; NoEquality; NoComparison>]
type Ident =
     new: text: string * range: range -> Ident
     member idText: string
     member idRange: range
       

/// Represents a long identifier e.g. 'A.B.C'
type LongIdent = Ident list


/// Represents a long identifier with possible '.' at end.
///
/// Typically dotRanges.Length = lid.Length-1, but they may be same if (incomplete) code ends in a dot, e.g. "Foo.Bar."
/// The dots mostly matter for parsing, and are typically ignored by the typechecker, but
/// if dotRanges.Length = lid.Length, then the parser must have reported an error, so the typechecker is allowed
/// more freedom about typechecking these expressions.
/// LongIdent can be empty list - it is used to denote that name of some AST element is absent (i.e. empty type name in inherit)
type LongIdentWithDots =
    | //[<Experimental("This construct is subject to change in future versions of FSharp.Compiler.Service and should only be used if no adequate alternative is available.")>]
      LongIdentWithDots of id: LongIdent * dotRanges: range list

    /// Gets the syntax range of this construct
    member Range: range

    /// Get the long ident for this construct
    member Lid: LongIdent

    /// Indicates if the construct ends in '.' due to error recovery
    member ThereIsAnExtraDotAtTheEnd: bool

    /// Gets the syntax range for part of this construct
    member RangeWithoutAnyExtraDot: range

/// Indicates if the construct arises from error recovery
[<RequireQualifiedAccess>]
type ParserDetail =
    /// The construct arises normally
    | Ok

    /// The construct arises from error recovery
    | ErrorRecovery

/// Represents whether a type parameter has a static requirement or not (^T or 'T)
[<RequireQualifiedAccess>]
type TyparStaticReq =
    /// The construct is a normal type inference variable
    | None

    /// The construct is a statically inferred type inference variable '^T'
    | HeadType

/// Represents a syntactic type parameter
[<NoEquality; NoComparison>]
type SynTypar =
    | SynTypar of ident: Ident * staticReq: TyparStaticReq * isCompGen: bool

    /// Gets the syntax range of this construct
    member Range: range

/// Indicate if the string had a special format
[<Struct; RequireQualifiedAccess>]
type SynStringKind =
    | Regular
    | Verbatim
    | TripleQuote

/// Indicate if the byte string had a special format
[<Struct; RequireQualifiedAccess>]
type SynByteStringKind =
    | Regular
    | Verbatim

/// The unchecked abstract syntax tree of constants in F# types and expressions.
[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynConst =

    /// F# syntax: ()
    | Unit

    /// F# syntax: true, false
    | Bool of bool

    /// F# syntax: 13y, 0xFFy, 0o077y, 0b0111101y
    | SByte of sbyte

    /// F# syntax: 13uy, 0x40uy, 0oFFuy, 0b0111101uy
    | Byte of byte

    /// F# syntax: 13s, 0x4000s, 0o0777s, 0b0111101s
    | Int16 of int16

    /// F# syntax: 13us, 0x4000us, 0o0777us, 0b0111101us
    | UInt16 of uint16

    /// F# syntax: 13, 0x4000, 0o0777
    | Int32 of int32

    /// F# syntax: 13u, 0x4000u, 0o0777u
    | UInt32 of uint32

    /// F# syntax: 13L
    | Int64 of int64

    /// F# syntax: 13UL
    | UInt64 of uint64

    /// F# syntax: 13n
    | IntPtr of int64

    /// F# syntax: 13un
    | UIntPtr of uint64

    /// F# syntax: 1.30f, 1.40e10f etc.
    | Single of single

    /// F# syntax: 1.30, 1.40e10 etc.
    | Double of double

    /// F# syntax: 'a'
    | Char of char

    /// F# syntax: 23.4M
    | Decimal of System.Decimal

    /// UserNum(value, suffix)
    ///
    /// F# syntax: 1Q, 1Z, 1R, 1N, 1G
    | UserNum of value: string * suffix: string

    /// F# syntax: verbatim or regular string, e.g. "abc"
    | String of text: string * synStringKind :SynStringKind * range: range

    /// F# syntax: verbatim or regular byte string, e.g. "abc"B.
    ///
    /// Also used internally in the typechecker once an array of unit16 constants
    /// is detected, to allow more efficient processing of large arrays of uint16 constants.
    | Bytes of bytes: byte[] * synByteStringKind: SynByteStringKind * range: range

    /// Used internally in the typechecker once an array of unit16 constants
    /// is detected, to allow more efficient processing of large arrays of uint16 constants.
    | UInt16s of uint16[]

    /// Old comment: "we never iterate, so the const here is not another SynConst.Measure"
    | Measure of constant: SynConst * constantRange: range * SynMeasure
    
    /// Source Line, File, and Path Identifiers
    /// Containing both the original value as the evaluated value.
    | SourceIdentifier of constant: string * value: string * range: Range

    /// Gets the syntax range of this construct
    member Range: dflt: range -> range

/// Represents an unchecked syntax tree of F# unit of measure annotations.
[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynMeasure =

    /// A named unit of measure
    | Named of longId: LongIdent * range: range

    /// A product of two units of measure, e.g. 'kg * m'
    | Product of measure1: SynMeasure * measure2: SynMeasure * range: range

    /// A sequence of several units of measure, e.g. 'kg m m'
    | Seq of measures: SynMeasure list * range: range

    /// A division of two units of measure, e.g. 'kg / m'
    | Divide of measure1: SynMeasure * measure2: SynMeasure * range: range

    /// A power of a unit of measure, e.g. 'kg ^ 2'
    | Power of measure: SynMeasure * power: SynRationalConst * range: range

    /// The '1' unit of measure
    | One

    /// An anonymous (inferred) unit of measure
    | Anon of range: range

    /// A variable unit of measure
    | Var of typar: SynTypar * range: range

/// Represents an unchecked syntax tree of F# unit of measure exponents.
[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynRationalConst =
   
    | Integer of value: int32

    | Rational of numerator: int32 * denominator: int32 * range: range

    | Negate of SynRationalConst

/// Represents an accessibility modifier in F# syntax
[<RequireQualifiedAccess>]
type SynAccess =
    /// A construct marked or assumed 'public'
    | Public

    /// A construct marked or assumed 'internal'
    | Internal

    /// A construct marked or assumed 'private'
    | Private

/// Represents whether a debug point should be present for the target
/// of a decision tree, that is whether the construct corresponds to a debug
/// point in the original source.
[<RequireQualifiedAccess>]
type DebugPointForTarget =
    | Yes
    | No

/// Represents whether a debug point should be suppressed for either the
/// first or second part of a sequential execution, that is whether the
/// construct corresponds to a debug point in the original source.
[<RequireQualifiedAccess>]
type DebugPointAtSequential =
    | SuppressNeither

    // This means "suppress a in 'a;b'" and "suppress b in 'a before b'"
    | SuppressStmt

    // This means "suppress both"
    | SuppressBoth

    // This means "suppress b in 'a;b'" and "suppress a in 'a before b'"
    | SuppressExpr

/// Represents whether a debug point should be present for a 'try', that is whether
/// the construct corresponds to a debug point in the original source.
[<RequireQualifiedAccess>]
type DebugPointAtTry =
    | Yes of range: range
    | Body
    | No

/// Represents whether a debug point should be present for the 'with' in a 'try .. with',
/// that is whether the construct corresponds to a debug point in the original source.
[<RequireQualifiedAccess>]
type DebugPointAtWith =
    | Yes of range: range
    | No

/// Represents whether a debug point should be present for the 'finally' in a 'try .. finally',
/// that is whether the construct corresponds to a debug point in the original source.
[<RequireQualifiedAccess>]
type DebugPointAtFinally =
    | Yes of range: range
    | Body
    | No

/// Represents whether a debug point should be present for the 'for' in a 'for...' loop,
/// that is whether the construct corresponds to a debug point in the original source.
[<RequireQualifiedAccess>]
type DebugPointAtFor =
    | Yes of range: range
    | No

/// Represents whether a debug point should be present for the 'while' in a 'while...' loop,
/// that is whether the construct corresponds to a debug point in the original source.
[<RequireQualifiedAccess>]
type DebugPointAtWhile =
    | Yes of range: range
    | No

/// Represents whether a debug point should be present for a 'let' binding,
/// that is whether the construct corresponds to a debug point in the original source.
[<RequireQualifiedAccess>]
type DebugPointAtBinding =
    // Indicates emit of a debug point prior to the 'let'
    | Yes of range: range

    // Indicates the omission of a debug point for a binding for a 'do expr'
    | NoneAtDo

    // Indicates the omission of a debug point for a binding for a 'let e = expr' where
    // 'expr' has immediate control flow
    | NoneAtLet

    // Indicates the omission of a debug point for a compiler generated binding
    // where we've done a local expansion of some construct into something that involves
    // a 'let'. e.g. we've inlined a function and bound its arguments using 'let'
    // The let bindings are 'sticky' in that the inversion of the inlining would involve
    // replacing the entire expression with the original and not just the let bindings alone.
    | NoneAtSticky

    // Given 'let v = e1 in e2', where this is a compiler generated binding,
    // we are sometimes forced to generate a debug point for the expression anyway based on its
    // overall range. If the let binding is given the flag below then it is asserting that
    // the binding has no interesting side effects and can be totally ignored and the range
    // of the inner expression is used instead
    | NoneAtInvisible

    // Don't drop debug points when combining debug points
    member Combine: y: DebugPointAtBinding -> DebugPointAtBinding

/// Indicates if a for loop is 'for x in e1 -> e2', only valid in sequence expressions
type SeqExprOnly =
    /// Indicates if a for loop is 'for x in e1 -> e2', only valid in sequence expressions
    | SeqExprOnly of bool

/// Represents the location of the separator block + optional position
/// of the semicolon (used for tooling support)
type BlockSeparator = range * pos option

/// Represents a record field name plus a flag indicating if given record field name is syntactically
/// correct and can be used in name resolution.
type RecordFieldName = LongIdentWithDots * bool

/// Indicates if an expression is an atomic expression.
///
/// An atomic expression has no whitespace unless enclosed in parentheses, e.g.
/// 1, "3", ident, ident.[expr] and (expr). If an atomic expression has type T,
/// then the largest expression ending at the same range as the atomic expression
/// also has type T.
[<RequireQualifiedAccess>]
type ExprAtomicFlag =
    | Atomic = 0
    | NonAtomic = 1

/// The kind associated with a binding - "let", "do" or a standalone expression
[<RequireQualifiedAccess>]
type SynBindingKind =

    /// A standalone expression in a module
    | StandaloneExpression

    /// A normal 'let' binding in a module
    | Normal

    /// A 'do' binding in a module. Must have type 'unit'
    | Do

/// Represents the explicit declaration of a type parameter
[<NoEquality; NoComparison>]
type SynTyparDecl =
    | SynTyparDecl of attributes: SynAttributes * SynTypar

/// The unchecked abstract syntax tree of F# type constraints
[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynTypeConstraint =

    /// F# syntax: is 'typar: struct
    | WhereTyparIsValueType of
        typar: SynTypar *
        range: range

    /// F# syntax: is 'typar: not struct
    | WhereTyparIsReferenceType of
        typar: SynTypar *
        range: range

    /// F# syntax is 'typar: unmanaged
    | WhereTyparIsUnmanaged of
        typar: SynTypar *
        range: range

    /// F# syntax is 'typar: null
    | WhereTyparSupportsNull of
        typar: SynTypar *
        range: range

    /// F# syntax is 'typar: comparison
    | WhereTyparIsComparable of
        typar: SynTypar *
        range: range

    /// F# syntax is 'typar: equality
    | WhereTyparIsEquatable of
        typar: SynTypar *
        range: range

    /// F# syntax is default ^T: type
    | WhereTyparDefaultsToType of
        typar: SynTypar *
        typeName: SynType *
        range: range

    /// F# syntax is 'typar :> type
    | WhereTyparSubtypeOfType of
        typar: SynTypar *
        typeName: SynType *
        range: range

    /// F# syntax is ^T: (static member MemberName: ^T * int -> ^T)
    | WhereTyparSupportsMember of
        typars: SynType list *
        memberSig: SynMemberSig *
        range: range

    /// F# syntax is 'typar: enum<'UnderlyingType>
    | WhereTyparIsEnum of
        typar: SynTypar *
        typeArgs: SynType list *
        range: range

    /// F# syntax is 'typar: delegate<'Args, unit>
    | WhereTyparIsDelegate of
       typar: SynTypar *
       typeArgs: SynType list *
       range: range

    member Range: range

/// List of type parameter declarations with optional type constraints,
/// enclosed in `< ... >` (postfix) or `( ... )` (prefix), or a single prefix parameter.
[<RequireQualifiedAccess>]
type SynTyparDecls =
    | PostfixList of decls: SynTyparDecl list * constraints: SynTypeConstraint list * range: range
    | PrefixList of decls: SynTyparDecl list * range: range
    | SinglePrefix of decl: SynTyparDecl * range: range

    member TyparDecls: SynTyparDecl list
    member Constraints: SynTypeConstraint list
    member Range: range

/// Represents a syntax tree for F# types
[<NoEquality; NoComparison;RequireQualifiedAccess>]
type SynType = 
    
    /// F# syntax: A.B.C
    | LongIdent of
        longDotId: LongIdentWithDots

    /// F# syntax: type<type, ..., type> or type type or (type, ..., type) type
    ///   isPostfix: indicates a postfix type application e.g. "int list" or "(int, string) dict"
    | App of
        typeName: SynType *
        lessRange: range option *
        typeArgs: SynType list *
        commaRanges: range list * // interstitial commas
        greaterRange: range option *
        isPostfix: bool *
        range: range

    /// F# syntax: type.A.B.C<type, ..., type>
    | LongIdentApp of
        typeName: SynType *
        longDotId: LongIdentWithDots *
        lessRange: range option *
        typeArgs: SynType list *
        commaRanges: range list * // interstitial commas
        greaterRange: range option *
        range: range

    /// F# syntax: type * ... * type
    /// F# syntax: struct (type * ... * type)
    // the bool is true if / rather than * follows the type
    | Tuple of
        isStruct: bool *
        elementTypes:(bool*SynType) list *
        range: range

    /// F# syntax: {| id: type; ...; id: type |}
    /// F# syntax: struct {| id: type; ...; id: type |}
    | AnonRecd of
        isStruct: bool *
        fields:(Ident * SynType) list *
        range: range

    /// F# syntax: type[]
    | Array of
        rank: int *
        elementType: SynType *
        range: range

    /// F# syntax: type -> type
    | Fun of
        argType: SynType *
        returnType: SynType *
        range: range

    /// F# syntax: 'Var
    | Var of
        typar: SynTypar *
        range: range

    /// F# syntax: _
    | Anon of range: range

    /// F# syntax: typ with constraints
    | WithGlobalConstraints of
        typeName: SynType *
        constraints: SynTypeConstraint list *
        range: range

    /// F# syntax: #type
    | HashConstraint of
        innerType: SynType *
        range: range

    /// F# syntax: for units of measure e.g. m / s
    | MeasureDivide of
        dividend: SynType *
        divisor: SynType *
        range: range

    /// F# syntax: for units of measure e.g. m^3, kg^1/2
    | MeasurePower of
        baseMeasure: SynType *
        exponent: SynRationalConst *
        range: range

    /// F# syntax: 1, "abc" etc, used in parameters to type providers
    /// For the dimensionless units i.e. 1, and static parameters to provided types
    | StaticConstant of
        constant: SynConst *
        range: range

    /// F# syntax: const expr, used in static parameters to type providers
    | StaticConstantExpr of
        expr: SynExpr *
        range: range

    /// F# syntax: ident=1 etc., used in static parameters to type providers
    | StaticConstantNamed of
       ident: SynType *
       value: SynType *
       range: range

    | Paren of
      innerType: SynType *
      range: range

    /// Gets the syntax range of this construct
    member Range: range

/// Represents a syntax tree for F# expressions
[<NoEquality; NoComparison;RequireQualifiedAccess>]
type SynExpr =

    /// F# syntax: (expr)
    ///
    /// Parenthesized expressions. Kept in AST to distinguish A.M((x, y))
    /// from A.M(x, y), among other things.
    | Paren of
        expr: SynExpr *
        leftParenRange: range *
        rightParenRange: range option *
        range: range

    /// F# syntax: <@ expr @>, <@@ expr @@>
    ///
    /// Quote(operator, isRaw, quotedSynExpr, isFromQueryExpression, m)
    | Quote of
        operator: SynExpr *
        isRaw: bool *
        quotedExpr: SynExpr *
        isFromQueryExpression: bool *
        range: range

    /// F# syntax: 1, 1.3, () etc.
    | Const of
        constant: SynConst *
        range: range

    /// F# syntax: expr: type
    | Typed of
        expr: SynExpr *
        targetType: SynType *
        range: range

    /// F# syntax: e1, ..., eN
    | Tuple of
        isStruct: bool *
        exprs: SynExpr list *
        commaRanges: range list * // interstitial commas
        range: range 

    /// F# syntax: {| id1=e1; ...; idN=eN |}
    /// F# syntax: struct {| id1=e1; ...; idN=eN |}
    | AnonRecd of
        isStruct: bool *
        copyInfo:(SynExpr * BlockSeparator) option *
        recordFields:(Ident * SynExpr) list *
        range: range

    /// F# syntax: [ e1; ...; en ], [| e1; ...; en |]
    | ArrayOrList of
        isArray: bool *
        exprs: SynExpr list *
        range: range

    /// F# syntax: { f1=e1; ...; fn=en }
    /// inherit includes location of separator (for tooling)
    /// copyOpt contains range of the following WITH part (for tooling)
    /// every field includes range of separator after the field (for tooling)
    | Record of
        baseInfo:(SynType * SynExpr * range * BlockSeparator option * range) option *
        copyInfo:(SynExpr * BlockSeparator) option *
        recordFields:(RecordFieldName * (SynExpr option) * BlockSeparator option) list *
        range: range

    /// F# syntax: new C(...)
    /// The flag is true if known to be 'family' ('protected') scope
    | New of
        isProtected: bool *
        targetType: SynType *
        expr: SynExpr *
        range: range

    /// F# syntax: { new ... with ... }
    | ObjExpr of
        objType: SynType *
        argOptions:(SynExpr * Ident option) option *
        bindings: SynBinding list *
        extraImpls: SynInterfaceImpl list *
        newExprRange: range *
        range: range

    /// F# syntax: 'while ... do ...'
    | While of
        whileDebugPoint: DebugPointAtWhile *
        whileExpr: SynExpr *
        doExpr: SynExpr *
        range: range

    /// F# syntax: 'for i = ... to ... do ...'
    | For of
        forDebugPoint: DebugPointAtFor *
        ident: Ident *
        identBody: SynExpr *
        direction: bool *
        toBody: SynExpr *
        doBody: SynExpr *
        range: range

    /// F# syntax: 'for ... in ... do ...'
    | ForEach of
        forDebugPoint: DebugPointAtFor *
        seqExprOnly: SeqExprOnly *
        isFromSource: bool *
        pat: SynPat *
        enumExpr: SynExpr *
        bodyExpr: SynExpr *
        range: range

    /// F# syntax: [ expr ], [| expr |]
    | ArrayOrListOfSeqExpr of
        isArray: bool *
        expr: SynExpr *
        range: range

    /// F# syntax: { expr }
    | CompExpr of
        isArrayOrList: bool *
        isNotNakedRefCell: bool ref *
        expr: SynExpr *
        range: range

    /// First bool indicates if lambda originates from a method. Patterns here are always "simple"
    /// Second bool indicates if this is a "later" part of an iterated sequence of lambdas
    /// parsedData keeps original parsed patterns and expression,
    /// prior to transforming to "simple" patterns and iterated lambdas 
    ///
    /// F# syntax: fun pat -> expr
    | Lambda of
        fromMethod: bool *
        inLambdaSeq: bool *
        args: SynSimplePats *
        body: SynExpr *
        parsedData: (SynPat list * SynExpr) option *
        range: range

    /// F# syntax: function pat1 -> expr | ... | patN -> exprN
    | MatchLambda of
        isExnMatch: bool *
        keywordRange: range *
        matchClauses: SynMatchClause list *
        matchDebugPoint: DebugPointAtBinding *
        range: range

    /// F# syntax: match expr with pat1 -> expr | ... | patN -> exprN
    | Match of
        matchDebugPoint: DebugPointAtBinding *
        expr: SynExpr *
        clauses: SynMatchClause list *
        range: range 

    /// F# syntax: do expr
    | Do of
        expr: SynExpr *
        range: range

    /// F# syntax: assert expr
    | Assert of
        expr: SynExpr *
        range: range

    /// F# syntax: f x
    ///
    /// flag: indicates if the application is syntactically atomic, e.g. f.[1] is atomic, but 'f x' is not
    /// isInfix is true for the first app of an infix operator, e.g. 1+2
    /// becomes App(App(+, 1), 2), where the inner node is marked isInfix
    | App of
        flag: ExprAtomicFlag *
        isInfix: bool *
        funcExpr: SynExpr *
        argExpr: SynExpr *
        range: range

    /// F# syntax: expr<type1, ..., typeN>
    | TypeApp of
        expr: SynExpr *
        lessRange: range *
        typeArgs: SynType list *
        commaRanges: range list *
        greaterRange: range option *
        typeArgsRange: range *
        range: range

    /// F# syntax: let pat = expr in expr
    /// F# syntax: let f pat1 .. patN = expr in expr
    /// F# syntax: let rec f pat1 .. patN = expr in expr
    /// F# syntax: use pat = expr in expr
    | LetOrUse of
        isRecursive: bool *
        isUse: bool *
        bindings: SynBinding list *
        body: SynExpr *
        range: range

    /// F# syntax: try expr with pat -> expr
    | TryWith of
        tryExpr: SynExpr *
        tryRange: range *
        withCases: SynMatchClause list *
        withRange: range *
        range: range *
        tryDebugPoint: DebugPointAtTry *
        withDebugPoint: DebugPointAtWith

    /// F# syntax: try expr finally expr
    | TryFinally of
        tryExpr: SynExpr *
        finallyExpr: SynExpr *
        range: range *
        tryDebugPoint: DebugPointAtTry *
        finallyDebugPoint: DebugPointAtFinally

    /// F# syntax: lazy expr
    | Lazy of
        expr: SynExpr *
        range: range

    /// F# syntax: expr; expr
    ///
    ///  isTrueSeq: false indicates "let v = a in b; v"
    | Sequential of
        debugPoint: DebugPointAtSequential *
        isTrueSeq: bool *
        expr1: SynExpr *
        expr2: SynExpr *
        range: range

    /// F# syntax: if expr then expr
    /// F# syntax: if expr then expr else expr
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

    /// F# syntax: ident
    /// Optimized representation for SynExpr.LongIdent (false, [id], id.idRange)
    | Ident of
        ident: Ident

    /// F# syntax: ident.ident...ident
    ///
    /// isOptional: true if preceded by a '?' for an optional named parameter
    /// altNameRefCell: Normally 'None' except for some compiler-generated
    /// variables in desugaring pattern matching. See SynSimplePat.Id
    | LongIdent of
        isOptional: bool *
        longDotId: LongIdentWithDots *
        altNameRefCell: SynSimplePatAlternativeIdInfo ref option *
        range: range

    /// F# syntax: ident.ident...ident <- expr
    | LongIdentSet of
        longDotId: LongIdentWithDots *
        expr: SynExpr *
        range: range

    /// F# syntax: expr.ident.ident
    | DotGet of
        expr: SynExpr *
        rangeOfDot: range *
        longDotId: LongIdentWithDots *
        range: range

    /// F# syntax: expr.ident...ident <- expr
    | DotSet of
        targetExpr: SynExpr *
        longDotId: LongIdentWithDots *
        rhsExpr: SynExpr *
        range: range

    /// F# syntax: expr <- expr
    | Set of
        targetExpr: SynExpr *
        rhsExpr: SynExpr *
        range: range

    /// F# syntax: expr.[expr, ..., expr]
    | DotIndexedGet of
        objectExpr: SynExpr *
        indexExprs: SynIndexerArg list *
        dotRange: range *
        range: range

    /// F# syntax: expr.[expr, ..., expr] <- expr
    | DotIndexedSet of
        objectExpr: SynExpr *
        indexExprs: SynIndexerArg list *
        valueExpr: SynExpr *
        leftOfSetRange: range *
        dotRange: range *
        range: range

    /// F# syntax: Type.Items(e1) <- e2, rarely used named-property-setter notation, e.g. Foo.Bar.Chars(3) <- 'a'
    | NamedIndexedPropertySet of
        longDotId: LongIdentWithDots *
        expr1: SynExpr *
        expr2: SynExpr *
        range: range

    /// F# syntax: expr.Items (e1) <- e2, rarely used named-property-setter notation, e.g. (stringExpr).Chars(3) <- 'a'
    | DotNamedIndexedPropertySet of
        targetExpr: SynExpr *
        longDotId: LongIdentWithDots *
        argExpr: SynExpr *
        rhsExpr: SynExpr *
        range: range

    /// F# syntax: expr :? type
    | TypeTest of
        expr: SynExpr *
        targetType: SynType *
        range: range

    /// F# syntax: expr :> type
    | Upcast of
        expr: SynExpr *
        targetType: SynType *
        range: range

    /// F# syntax: expr :?> type
    | Downcast of
        expr: SynExpr *
        targetType: SynType *
        range: range

    /// F# syntax: upcast expr
    | InferredUpcast of
        expr: SynExpr *
        range: range

    /// F# syntax: downcast expr
    | InferredDowncast of
        expr: SynExpr *
        range: range

    /// F# syntax: null
    | Null of
        range: range

    /// F# syntax: &expr, &&expr
    | AddressOf of
        isByref: bool *
        expr: SynExpr *
        opRange: range *
        range: range

    /// F# syntax: ((typar1 or ... or typarN): (member-dig) expr)
    | TraitCall of
        supportTys: SynTypar list *
        traitSig: SynMemberSig *
        argExpr: SynExpr *
        range: range

    /// F# syntax: ... in ...
    /// Computation expressions only, based on JOIN_IN token from lex filter
    | JoinIn of
        lhsExpr: SynExpr *
        lhsRange: range *
        rhsExpr: SynExpr *
        range: range

    /// Used in parser error recovery and internally during type checking for translating computation expressions.
    | ImplicitZero of
        range: range

    /// Used internally during type checking for translating computation expressions.
    | SequentialOrImplicitYield of
        debugPoint:DebugPointAtSequential *
        expr1:SynExpr *
        expr2:SynExpr *
        ifNotStmt:SynExpr *
        range:range

    /// F# syntax: yield expr
    /// F# syntax: return expr
    /// Computation expressions only
    | YieldOrReturn of
        flags: (bool * bool) *
        expr: SynExpr *
        range: range

    /// F# syntax: yield! expr
    /// F# syntax: return! expr
    /// Computation expressions only
    | YieldOrReturnFrom of
        flags: (bool * bool) *
        expr: SynExpr *
        range: range

    /// F# syntax: let! pat = expr in expr
    /// F# syntax: use! pat = expr in expr
    /// F# syntax: let! pat = expr and! ... and! ... and! pat = expr in expr
    /// Computation expressions only
    | LetOrUseBang of
        bindDebugPoint: DebugPointAtBinding *
        isUse: bool *
        isFromSource: bool *
        pat: SynPat *
        rhs: SynExpr *
        andBangs:(DebugPointAtBinding * bool * bool * SynPat * SynExpr * range) list *
        body:SynExpr *
        range: range 

    /// F# syntax: match! expr with pat1 -> expr | ... | patN -> exprN
    | MatchBang of
        matchDebugPoint: DebugPointAtBinding *
        expr: SynExpr *
        clauses: SynMatchClause list *
        range: range

    /// F# syntax: do! expr
    /// Computation expressions only
    | DoBang of
        expr: SynExpr *
        range: range

    /// Only used in FSharp.Core
    | LibraryOnlyILAssembly of
        ilCode: obj * // this type is ILInstr[]  but is hidden to avoid the representation of AbstractIL being public
        typeArgs: SynType list *
        args: SynExpr list *
        retTy: SynType list *
        range: range

    /// Only used in FSharp.Core
    | LibraryOnlyStaticOptimization of
        constraints: SynStaticOptimizationConstraint list *
        expr: SynExpr *
        optimizedExpr: SynExpr *
        range: range

    /// Only used in FSharp.Core
    | LibraryOnlyUnionCaseFieldGet of
        expr: SynExpr *
        longId: LongIdent *
        fieldNum: int *
        range: range

    /// Only used in FSharp.Core
    | LibraryOnlyUnionCaseFieldSet of
        expr: SynExpr *
        longId: LongIdent *
        fieldNum: int *
        rhsExpr: SynExpr *
        range: range

    /// Inserted for error recovery
    | ArbitraryAfterError of
        debugStr: string *
        range: range

    /// Inserted for error recovery
    | FromParseError of
        expr: SynExpr *
        range: range

    /// Inserted for error recovery when there is "expr." and missing tokens or error recovery after the dot
    | DiscardAfterMissingQualificationAfterDot of
        expr: SynExpr *
        range: range

    /// 'use x = fixed expr'
    | Fixed of
        expr: SynExpr *
        range: range

    /// F# syntax: interpolated string, e.g. "abc{x}" or "abc{x,3}" or "abc{x:N4}"
    /// Note the string ranges include the quotes, verbatim markers, dollar sign and braces
    | InterpolatedString of
        contents: SynInterpolatedStringPart list *
        synStringKind :SynStringKind *
        range: range

    /// Gets the syntax range of this construct
    member Range: range

    member RangeWithoutAnyExtraDot: range

    /// Attempt to get the range of the first token or initial portion only - this
    /// is ad-hoc, just a cheap way to improve a certain 'query custom operation' error range
    member RangeOfFirstPortion: range

    /// Indicates if this expression arises from error recovery
    member IsArbExprAndThusAlreadyReportedError: bool

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynIfThenElseStart =
    | If of range: Range
    | Elif of range: Range
    
    /// Gets the range of the keyword
    member Range: range

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynInterpolatedStringPart =
    | String of value: string * range: range
    | FillExpr of fillExpr: SynExpr * qualifiers: Ident option

/// Represents a syntax tree for an F# indexer expression argument
[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynIndexerArg =
    /// A two-element range indexer argument
    | Two of
        expr1: SynExpr *
        fromEnd1: bool *
        expr2: SynExpr *
        fromEnd2: bool *
        range1: range *
        range2: range

    /// A one-element item indexer argument
    | One of
        expr: SynExpr *
        fromEnd: bool * range

    /// Gets the syntax range of this construct
    member Range: range

    /// Get the one or two expressions as a list
    member Exprs: SynExpr list

/// Represents a syntax tree for simple F# patterns
[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynSimplePat =

    /// Indicates a simple pattern variable.
    ///
    /// altNameRefCell:
    ///   Normally 'None' except for some compiler-generated variables in desugaring pattern matching.
    ///   Pattern processing sets this reference for hidden variable introduced
    ///   by desugaring pattern matching in arguments. The info indicates an
    ///   alternative (compiler generated) identifier to be used because the
    ///   name of the identifier is already bound.
    ///
    /// isCompilerGenerated: true if a compiler generated name
    /// isThisVal: true if 'this' variable in member
    /// isOptional: true if a '?' is in front of the name
    | Id of
        ident: Ident *
        altNameRefCell: SynSimplePatAlternativeIdInfo ref option *
        isCompilerGenerated: bool *
        isThisVal: bool *
        isOptional: bool *
        range: range

    /// A type annotated simple pattern
    | Typed of
        pat: SynSimplePat *
        targetType: SynType *
        range: range

    /// An attributed simple pattern
    | Attrib of
        pat: SynSimplePat *
        attributes: SynAttributes *
        range: range

    member Range: range

/// Represents the alternative identifier for a simple pattern
[<RequireQualifiedAccess>]
type SynSimplePatAlternativeIdInfo =

    /// We have not decided to use an alternative name in the pattern and related expression
    | Undecided of Ident

    /// We have decided to use an alternative name in the pattern and related expression
    | Decided of Ident

/// Represents a syntax tree for a static optimization constraint in the F# core library
[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynStaticOptimizationConstraint =

    /// A static optimization conditional that activates for a particular type instantiation
    | WhenTyparTyconEqualsTycon of
        typar: SynTypar *
        rhsType: SynType *
        range: range

    /// A static optimization conditional that activates for a struct
    | WhenTyparIsStruct of
        typar: SynTypar *
        range: range

/// Represents a simple set of variable bindings a, (a, b) or (a: Type, b: Type) at a lambda,
/// function definition or other binding point, after the elimination of pattern matching
/// from the construct, e.g. after changing a "function pat1 -> rule1 | ..." to a
/// "fun v -> match v with ..."
[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynSimplePats =
    | SimplePats of
        pats: SynSimplePat list *
        range: range

    | Typed of
        pats: SynSimplePats *
        targetType: SynType *
        range: range

    member Range: range

/// Represents a syntax tree for arguments patterns 
[<RequireQualifiedAccess>]
type SynArgPats =
    | Pats of
        pats: SynPat list

    | NamePatPairs of
        pats: (Ident * SynPat) list *
        range: range

/// Represents a syntax tree for an F# pattern
[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynPat =

    /// A constant in a pattern
    | Const of
        constant: SynConst *
        range: range

    /// A wildcard '_' in a pattern
    | Wild of
        range: range

    /// A name pattern 'ident' 
    | Named of
        ident: Ident *
        isThisVal: bool *
        accessibility: SynAccess option *
        range: range

    /// A typed pattern 'pat : type'
    | Typed of
        pat: SynPat *
        targetType: SynType *
        range: range

    /// An attributed pattern, used in argument or declaration position
    | Attrib of
        pat: SynPat *
        attributes: SynAttributes *
        range: range

    /// A disjunctive pattern 'pat1 | pat2'
    | Or of
        lhsPat: SynPat *
        rhsPat: SynPat *
        range: range

    /// A conjunctive pattern 'pat1 & pat2'
    | Ands of
        pats: SynPat list *
        range: range

    /// A conjunctive pattern 'pat1 as pat2'
    | As of
        lhsPat: SynPat *
        rhsPat: SynPat *
        range: range

    /// A long identifier pattern possibly with argument patterns
    | LongIdent of
        longDotId: LongIdentWithDots *
        extraId: Ident option * // holds additional ident for tooling
        typarDecls: SynValTyparDecls option * // usually None: temporary used to parse "f<'a> x = x"
        argPats: SynArgPats *
        accessibility: SynAccess option *
        range: range

    /// A tuple pattern
    | Tuple of
        isStruct: bool *
        elementPats: SynPat list *
        range: range

    /// A parenthesized pattern
    | Paren of
        pat: SynPat *
        range: range

    /// An array or a list as a pattern
    | ArrayOrList of
        isArray: bool *
        elementPats: SynPat list *
        range: range

    /// A record pattern
    | Record of
        fieldPats: ((LongIdent * Ident) * SynPat) list *
        range: range

    /// The 'null' pattern
    | Null of
        range: range

    /// '?id' -- for optional argument names
    | OptionalVal of
        ident: Ident *
        range: range

    /// A type test pattern ':? type '
    | IsInst of
        pat: SynType *
        range: range

    /// &lt;@ expr @&gt;, used for active pattern arguments
    | QuoteExpr of
        expr: SynExpr *
        range: range

    /// Deprecated character range: ranges
    | DeprecatedCharRange of
        startChar: char *
        endChar: char *
        range: range

    /// Used internally in the type checker
    | InstanceMember of
        thisId: Ident *
        memberId: Ident *
        toolingId: Ident option * // holds additional ident for tooling
        accessibility: SynAccess option *
        range: range

    /// A pattern arising from a parse error
    | FromParseError of
        pat: SynPat *
        range: range

    /// Gets the syntax range of this construct
    member Range: range

/// Represents a set of bindings that implement an interface
[<NoEquality; NoComparison;>]
type SynInterfaceImpl =
    | SynInterfaceImpl of
        interfaceTy: SynType *
        bindings: SynBinding list *
        range: range

/// Represents a clause in a 'match' expression
[<NoEquality; NoComparison>]
type SynMatchClause =
    | SynMatchClause of
        pat: SynPat *
        whenExpr: SynExpr option *
        resultExpr: SynExpr *
        range: range *
        debugPoint: DebugPointForTarget

    /// Gets the syntax range of part of this construct
    member RangeOfGuardAndRhs: range

    /// Gets the syntax range of this construct
    member Range: range

/// Represents an attribute
[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynAttribute =
    { /// The name of the type for the attribute
      TypeName: LongIdentWithDots

      /// The argument of the attribute, perhaps a tuple
      ArgExpr: SynExpr

      /// Target specifier, e.g. "assembly", "module", etc.
      Target: Ident option

      /// Is this attribute being applied to a property getter or setter?
      AppliesToGetterAndSetter: bool

      /// The syntax range of the attribute
      Range: range
    }

/// List of attributes enclosed in [< ... >].
[<RequireQualifiedAccess>]
type SynAttributeList =
    { 
      /// The list of attributes
      Attributes: SynAttribute list
      
      /// The syntax range of the list of attributes
      Range: range
    }

type SynAttributes = SynAttributeList list

/// Represents extra information about the declaration of a value
[<NoEquality; NoComparison>]
type SynValData =
    | SynValData of
        memberFlags: SynMemberFlags option *
        valInfo: SynValInfo *
        thisIdOpt: Ident option

    member SynValInfo: SynValInfo

/// Represents a binding for a 'let' or 'member' declaration
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
    member RangeOfBindingWithoutRhs: range

    member RangeOfBindingWithRhs: range

    member RangeOfHeadPattern: range

/// Represents the return information in a binding for a 'let' or 'member' declaration
[<NoEquality; NoComparison>]
type SynBindingReturnInfo =
    | SynBindingReturnInfo of
        typeName: SynType *
        range: range *
        attributes: SynAttributes

/// Represents the flags for a 'member' declaration
[<NoComparison; RequireQualifiedAccess>]
type SynMemberFlags =
    { 
      /// The member is an instance member (non-static)
      IsInstance: bool

      /// The member is a dispatch slot
      IsDispatchSlot: bool

      /// The member is an 'override' or explicit interface implementation 
      IsOverrideOrExplicitImpl: bool

      /// The member is 'final'
      IsFinal: bool

      /// The kind of the member
      MemberKind: SynMemberKind
    }

/// Note the member kind is actually computed partially by a syntax tree transformation in tc.fs
[<StructuralEquality; NoComparison; RequireQualifiedAccess>]
type SynMemberKind =

    /// The member is a class initializer
    | ClassConstructor

    /// The member is a object model constructor
    | Constructor

    /// The member kind is not yet determined
    | Member

    /// The member kind is property getter
    | PropertyGet

    /// The member kind is property setter
    | PropertySet

    /// An artificial member kind used prior to the point where a
    /// get/set property is split into two distinct members.
    | PropertyGetSet

/// Represents the syntax tree for a member signature (used in signature files, abstract member declarations
/// and member constraints)
[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynMemberSig =

    /// A member definition in a type in a signature file
    | Member of
        memberSig: SynValSig *
        flags: SynMemberFlags *
        range: range

    /// An interface definition in a type in a signature file
    | Interface of
        interfaceType: SynType *
        range: range

    /// An 'inherit' definition in a type in a signature file
    | Inherit of
        inheritedType: SynType *
        range: range

    /// A 'val' definition in a type in a signature file
    | ValField of
        field: SynField *
        range: range

    /// A nested type definition in a signature file (an unimplemented feature)
    | NestedType of
        nestedType: SynTypeDefnSig *
        range: range

    /// Gets the syntax range of this construct
    member Range: range

/// Represents the kind of a type definition whether explicit or inferred
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

/// Represents the syntax tree for the core of a simple type definition, in either signature
/// or implementation.
[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynTypeDefnSimpleRepr =

    /// A union type definition, type X = A | B
    | Union of
        accessibility: SynAccess option *
        unionCases: SynUnionCase list *
        range: range

    /// An enum type definition, type X = A = 1 | B = 2
    | Enum of
        cases: SynEnumCase list *
        range: range

    /// A record type definition, type X = { A: int; B: int }
    | Record of
        accessibility: SynAccess option *
        recordFields: SynField list *
        range: range

    /// An object oriented type definition. This is not a parse-tree form, but represents the core
    /// type representation which the type checker splits out from the "ObjectModel" cases of type definitions.
    | General of
        kind: SynTypeDefnKind *
        inherits: (SynType * range * Ident option) list *
        slotsigs: (SynValSig * SynMemberFlags) list *
        fields: SynField list *
        isConcrete: bool *
        isIncrClass: bool *
        implicitCtorSynPats: SynSimplePats option *
        range: range

    /// A type defined by using an IL assembly representation. Only used in FSharp.Core.
    ///
    /// F# syntax: "type X = (# "..."#)
    | LibraryOnlyILAssembly of
        ilType: obj * // this type is ILType but is hidden to avoid the representation of AbstractIL being public
        range: range

    /// A type abbreviation, "type X = A.B.C"
    | TypeAbbrev of
        detail: ParserDetail *
        rhsType: SynType *
        range: range

    /// An abstract definition, "type X"
    | None of
        range: range

    /// An exception definition, "exception E = ..."
    | Exception of
        exnRepr: SynExceptionDefnRepr

    /// Gets the syntax range of this construct
    member Range: range

/// Represents the syntax tree for one case in an enum definition.
[<NoEquality; NoComparison>]
type SynEnumCase =

    | SynEnumCase of
        attributes: SynAttributes *
        ident: Ident * 
        value: SynConst *
        valueRange: range *
        xmlDoc: PreXmlDoc *
        range: range

    /// Gets the syntax range of this construct
    member Range: range

/// Represents the syntax tree for one case in a union definition.
[<NoEquality; NoComparison>]
type SynUnionCase =

    | SynUnionCase of
        attributes: SynAttributes *
        ident: Ident *
        caseType: SynUnionCaseKind *
        xmlDoc: PreXmlDoc *
        accessibility: SynAccess option *
        range: range

    /// Gets the syntax range of this construct
    member Range: range

/// Represents the syntax tree for the right-hand-side of union definition, excluding members,
/// in either a signature or implementation.
[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynUnionCaseKind =

    /// Normal style declaration
    | Fields of
        cases: SynField list

    /// Full type spec given by 'UnionCase: ty1 * tyN -> rty'. Only used in FSharp.Core, otherwise a warning.
    | FullType of
        fullType: SynType *
        fullTypeInfo: SynValInfo

/// Represents the syntax tree for the right-hand-side of a type definition in a signature.
/// Note: in practice, using a discriminated union to make a distinction between
/// "simple" types and "object oriented" types is not particularly useful.
[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynTypeDefnSigRepr =

    /// Indicates the right right-hand-side is a class, struct, interface or other object-model type
    | ObjectModel of
        kind: SynTypeDefnKind *
        memberSigs: SynMemberSig list *
        range: range

    /// Indicates the right right-hand-side is a record, union or other simple type.
    | Simple of
        repr: SynTypeDefnSimpleRepr *
        range: range

    | Exception of
        repr: SynExceptionDefnRepr

    /// Gets the syntax range of this construct
    member Range: range

/// Represents the syntax tree for a type definition in a signature
[<NoEquality; NoComparison>]
type SynTypeDefnSig =

    /// The information for a type definition in a signature
    | SynTypeDefnSig of
        typeInfo: SynComponentInfo *
        typeRepr: SynTypeDefnSigRepr *
        members: SynMemberSig list *
        range: range

    /// Gets the syntax range of this construct
    member Range: range

/// Represents the syntax tree for a field declaration in a record or class
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

/// Represents the syntax tree associated with the name of a type definition or module
/// in signature or implementation.
///
/// This includes the name, attributes, type parameters, constraints, documentation and accessibility
/// for a type definition or module. For modules, entries such as the type parameters are
/// always empty.
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

    /// Gets the syntax range of this construct
    member Range: range

/// Represents the syntax tree for a 'val' definition in an abstract slot or a signature file
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

    member RangeOfId: range

    member SynInfo: SynValInfo

    member SynType: SynType

/// The argument names and other metadata for a member or function
[<NoEquality; NoComparison>]
type SynValInfo =

    /// SynValInfo(curriedArgInfos, returnInfo)
    | SynValInfo of curriedArgInfos: SynArgInfo list list * returnInfo: SynArgInfo

    member CurriedArgInfos: SynArgInfo list list

    member ArgNames: string list

/// Represents the argument names and other metadata for a parameter for a member or function
[<NoEquality; NoComparison>]
type SynArgInfo =

    | SynArgInfo of
        attributes: SynAttributes *
        optional: bool *
        ident: Ident option

    member Ident: Ident option

    member Attributes: SynAttributes

/// Represents the names and other metadata for the type parameters for a member or function
[<NoEquality; NoComparison>]
type SynValTyparDecls =
    | SynValTyparDecls of
        typars: SynTyparDecls option *
        canInfer: bool

/// Represents the syntactic elements associated with the "return" of a function or method. 
[<NoEquality; NoComparison>]
type SynReturnInfo =
    | SynReturnInfo of returnType: (SynType * SynArgInfo) * range: range

/// Represents the right hand side of an exception declaration 'exception E = ... '
[<NoEquality; NoComparison>]
type SynExceptionDefnRepr =

    | SynExceptionDefnRepr of
        attributes: SynAttributes *
        caseName: SynUnionCase *
        longId: LongIdent option *
        xmlDoc: PreXmlDoc *
        accessibility: SynAccess option *
        range: range

    /// Gets the syntax range of this construct
    member Range: range 

/// Represents the right hand side of an exception declaration 'exception E = ... ' plus
/// any member definitions for the exception
[<NoEquality; NoComparison>]
type SynExceptionDefn =

    | SynExceptionDefn of
        exnRepr: SynExceptionDefnRepr *
        members: SynMemberDefns *
        range: range

    /// Gets the syntax range of this construct
    member Range: range

/// Represents the right hand side of a type or exception declaration 'type C = ... ' plus
/// any additional member definitions for the type
[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynTypeDefnRepr =

    /// An object model type definition (class or interface)
    | ObjectModel of
        kind: SynTypeDefnKind *
        members: SynMemberDefns *
        range: range

    /// A simple type definition (record, union, abbreviation)
    | Simple of
        simpleRepr: SynTypeDefnSimpleRepr *
        range: range

    /// An exception definition
    | Exception of
        exnRepr: SynExceptionDefnRepr

    /// Gets the syntax range of this construct
    member Range: range

/// Represents a type or exception declaration 'type C = ... ' plus
/// any additional member definitions for the type
[<NoEquality; NoComparison>]
type SynTypeDefn =
    | SynTypeDefn of
        typeInfo: SynComponentInfo *
        typeRepr: SynTypeDefnRepr *
        members: SynMemberDefns *
        implicitConstructor: SynMemberDefn option *
        range: range

    /// Gets the syntax range of this construct
    member Range: range

/// Represents a definition element within a type definition, e.g. 'member ... ' 
[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynMemberDefn =

    /// An 'open' definition within a type
    | Open of
        target: SynOpenDeclTarget *
        range: range

    /// A 'member' definition within a type
    | Member of
        memberDefn: SynBinding *
        range: range

    /// An implicit constructor definition
    | ImplicitCtor of
        accessibility: SynAccess option *
        attributes: SynAttributes *
        ctorArgs: SynSimplePats *
        selfIdentifier: Ident option *
        xmlDoc: PreXmlDoc *
        range: range

    /// An implicit inherit definition, 'inherit <typ>(args...) as base'
    | ImplicitInherit of
        inheritType: SynType *
        inheritArgs: SynExpr *
        inheritAlias: Ident option *
        range: range

    /// A 'let' definition within a class
    | LetBindings of
        bindings: SynBinding list *
        isStatic: bool *
        isRecursive: bool *
        range: range

    /// An abstract slot definition within a class or interface
    | AbstractSlot of
        slotSig: SynValSig *
        flags: SynMemberFlags *
        range: range

    /// An interface implementation definition within a class
    | Interface of
        interfaceType: SynType *
        members: SynMemberDefns option *
        range: range

    /// An 'inherit' definition within a class
    | Inherit of
        baseType: SynType *
        asIdent: Ident option *
        range: range

    /// A 'val' definition within a class
    | ValField of
        fieldInfo: SynField *
        range: range

    /// A nested type definition, a feature that is not implemented
    | NestedType of
        typeDefn: SynTypeDefn *
        accessibility: SynAccess option *
        range: range

    /// An auto-property definition, F# syntax: 'member val X = expr'
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

    /// Gets the syntax range of this construct
    member Range: range

type SynMemberDefns = SynMemberDefn list

/// Represents a definition within a module
[<NoEquality; NoComparison;RequireQualifiedAccess>]
type SynModuleDecl =

    /// A module abbreviation definition 'module X = A.B.C'
    | ModuleAbbrev of
        ident: Ident *
        longId: LongIdent *
        range: range

    /// A nested module definition 'module X = ...'
    | NestedModule of
        moduleInfo: SynComponentInfo *
        isRecursive: bool *
        decls: SynModuleDecl list *
        isContinuing: bool *
        range: range

    /// A 'let' definition within a module
    | Let of
        isRecursive: bool *
        bindings: SynBinding list *
        range: range

    /// A 'do expr' within a module
    | DoExpr of
       debugPoint: DebugPointAtBinding *
       expr: SynExpr *
       range: range

    /// One or more 'type' definitions within a module
    | Types of
        typeDefns: SynTypeDefn list *
        range: range

    /// An 'exception' definition within a module
    | Exception of
        exnDefn: SynExceptionDefn *
        range: range

    /// An 'open' definition within a module
    | Open of
        target: SynOpenDeclTarget *
        range: range

    /// An attribute definition within a module, for assembly and .NET module attributes
    | Attributes of
        attributes: SynAttributes *
        range: range

    /// A hash directive within a module
    | HashDirective of
        hashDirective: ParsedHashDirective *
        range: range

    /// A namespace fragment within a module
    | NamespaceFragment of
        fragment: SynModuleOrNamespace

    /// Gets the syntax range of this construct
    member Range: range

/// Represents the target of the open declaration
[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynOpenDeclTarget = 

    /// A 'open' declaration
    | ModuleOrNamespace of longId: LongIdent * range: range

    /// A 'open type' declaration
    | Type of typeName: SynType * range: range

    /// Gets the syntax range of this construct
    member Range: range

/// Represents the right hand side of an exception definition in a signature file
[<NoEquality; NoComparison>]
type SynExceptionSig =
    | SynExceptionSig of
        exnRepr: SynExceptionDefnRepr *
        members: SynMemberSig list *
        range: range

/// Represents a definition within a module or namespace in a signature file
[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynModuleSigDecl =

    /// A module abbreviation definition within a module or namespace in a signature file
    | ModuleAbbrev of
        ident: Ident *
        longId: LongIdent *
        range: range

    /// A nested module definition within a module or namespace in a signature file
    | NestedModule of
        moduleInfo: SynComponentInfo *
        isRecursive: bool *
        moduleDecls: SynModuleSigDecl list *
        range: range

    /// A 'val' definition within a module or namespace in a signature file, corresponding
    /// to a 'let' definition in the implementation
    | Val of
        valSig: SynValSig * range: range

    /// A set of one or more type definitions within a module or namespace in a signature file
    | Types of
        types: SynTypeDefnSig list *
        range: range

    /// An exception definition within a module or namespace in a signature file
    | Exception of
        exnSig: SynExceptionSig *
        range: range

    /// An 'open' definition within a module or namespace in a signature file
    | Open of
        target: SynOpenDeclTarget *
        range: range

    /// A hash directive within a module or namespace in a signature file
    | HashDirective of
        hashDirective: ParsedHashDirective *
        range: range

    /// A namespace fragment within a namespace in a signature file
    | NamespaceFragment of
        SynModuleOrNamespaceSig

    /// Gets the syntax range of this construct
    member Range: range

/// Represents the kind of a module or namespace definition
[<Struct; RequireQualifiedAccess>]
type SynModuleOrNamespaceKind =
    /// A module is explicitly named 'module N'
    | NamedModule

    /// A module is anonymously named, e.g. a script
    | AnonModule

    /// A namespace is explicitly declared
    | DeclaredNamespace

    /// A namespace is declared 'global'
    | GlobalNamespace

    /// Indicates if this is a module definition
    member IsModule: bool

/// Represents the definition of a module or namespace
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

    /// Gets the syntax range of this construct
    member Range: range

/// Represents the definition of a module or namespace in a signature file
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

    /// Gets the syntax range of this construct
    member Range: range

/// Represents a parsed hash directive argument
[<NoEquality; NoComparison; RequireQualifiedAccess>]
type ParsedHashDirectiveArgument =
    | String of value: string * stringKind: SynStringKind * range: Range
    | SourceIdentifier of constant: string * value: string * range: Range

    /// Gets the syntax range of this construct
    member Range: range

/// Represents a parsed hash directive
[<NoEquality; NoComparison>]
type ParsedHashDirective =
    | ParsedHashDirective of
        ident: string *
        args: ParsedHashDirectiveArgument list *
        range: range

/// Represents the syntax tree for the contents of a parsed implementation file
[<NoEquality; NoComparison; RequireQualifiedAccess>]
type ParsedImplFileFragment =

    /// An implementation file which is an anonymous module definition, e.g. a script
    | AnonModule of
        decls: SynModuleDecl list *
        range: range

    /// An implementation file is a named module definition, 'module N'
    | NamedModule of
        namedModule: SynModuleOrNamespace

    /// An implementation file fragment which declares a namespace fragment
    | NamespaceFragment of
        longId: LongIdent *
        isRecursive: bool *
        kind: SynModuleOrNamespaceKind *
        decls: SynModuleDecl list *
        xmlDoc: PreXmlDoc *
        attributes: SynAttributes *
        range: range

/// Represents the syntax tree for the contents of a parsed signature file
[<NoEquality; NoComparison; RequireQualifiedAccess>]
type ParsedSigFileFragment =

    /// A signature file which is an anonymous module, e.g. the signature file for the final file in an application
    | AnonModule of
        decls: SynModuleSigDecl list *
        range: range

    /// A signature file which is a module, 'module N'
    | NamedModule of
        namedModule: SynModuleOrNamespaceSig

    /// A signature file namespace fragment
    | NamespaceFragment of
        longId: LongIdent *
        isRecursive: bool *
        kind: SynModuleOrNamespaceKind *
        decls: SynModuleSigDecl list *
        xmlDoc: PreXmlDoc *
        attributes: SynAttributes *
        range: range

/// Represents a parsed syntax tree for an F# Interactive interaction
[<NoEquality; NoComparison; RequireQualifiedAccess>]
type ParsedScriptInteraction =
    | Definitions of
        defns: SynModuleDecl list *
        range: range

    | HashDirective of
        hashDirective: ParsedHashDirective *
        range: range

/// Represents a parsed implementation file made up of fragments 
[<NoEquality; NoComparison>]
type ParsedImplFile =
    | ParsedImplFile of
        hashDirectives: ParsedHashDirective list *
        fragments: ParsedImplFileFragment list

/// Represents a parsed signature file made up of fragments 
[<NoEquality; NoComparison>]
type ParsedSigFile =
    | ParsedSigFile of
        hashDirectives: ParsedHashDirective list *
        fragments: ParsedSigFileFragment list

/// Represents a scoped pragma 
[<RequireQualifiedAccess>]
type ScopedPragma =
    /// A pragma to turn a warning off
    | WarningOff of range: range * warningNumber: int

/// Represents a qualifying name for anonymous module specifications and implementations,
[<NoEquality; NoComparison>]
type QualifiedNameOfFile =
    | QualifiedNameOfFile of Ident

    /// The name of the file 
    member Text: string

    /// The identifier for the name of the file 
    member Id: Ident

    /// Gets the syntax range of this construct
    member Range: range

/// Represents the full syntax tree, file name and other parsing information for an implementation file
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

/// Represents the full syntax tree, file name and other parsing information for a signature file
[<NoEquality; NoComparison>]
type ParsedSigFileInput =
    | ParsedSigFileInput of
        fileName: string *
        qualifiedNameOfFile: QualifiedNameOfFile *
        scopedPragmas: ScopedPragma list *
        hashDirectives: ParsedHashDirective list *
        modules: SynModuleOrNamespaceSig list

/// Represents the syntax tree for a parsed implementation or signature file
[<NoEquality; NoComparison; RequireQualifiedAccess>]
type ParsedInput =
    /// A parsed implementation file
    | ImplFile of ParsedImplFileInput

    /// A parsed signature file
    | SigFile of ParsedSigFileInput

    /// Gets the file name for the parsed input
    member FileName: string

    /// Gets the syntax range of this construct
    member Range: range
