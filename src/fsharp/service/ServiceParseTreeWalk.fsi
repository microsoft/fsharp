// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.Syntax

open FSharp.Compiler.Syntax
open FSharp.Compiler.Text

/// Used to track route during traversal of syntax using SyntaxTraversal.Traverse
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
type SyntaxVisitorBase<'T> =
      new: unit -> SyntaxVisitorBase<'T>

      default VisitBinding: path: SyntaxVisitorPath * defaultTraverse: (SynBinding -> 'T option) * synBinding: SynBinding -> 'T option
      abstract VisitBinding: path: SyntaxVisitorPath * defaultTraverse: (SynBinding -> 'T option) * synBinding: SynBinding -> 'T option

      default VisitComponentInfo: path: SyntaxVisitorPath * synComponentInfo: SynComponentInfo -> 'T option
      abstract VisitComponentInfo: path: SyntaxVisitorPath * synComponentInfo: SynComponentInfo -> 'T option

      /// Controls the behavior when a SynExpr is reached; it can just do 
      ///          defaultTraverse(expr)      if you have no special logic for this node, and want the default processing to pick which sub-node to dive deeper into
      /// or can inject non-default behavior, which might incorporate: 
      ///          traverseSynExpr(subExpr)   to recurse deeper on some particular sub-expression based on your own logic
      /// path helps to track AST nodes that were passed during traversal
      abstract VisitExpr: path: SyntaxVisitorPath * traverseSynExpr: (SynExpr -> 'T option) * defaultTraverse: (SynExpr -> 'T option) * synExpr: SynExpr -> 'T option
      default VisitExpr: path: SyntaxVisitorPath * traverseSynExpr: (SynExpr -> 'T option) * defaultTraverse: (SynExpr -> 'T option) * synExpr: SynExpr -> 'T option

      abstract VisitHashDirective: path: SyntaxVisitorPath * hashDirective: ParsedHashDirective * range: range -> 'T option
      default VisitHashDirective: path: SyntaxVisitorPath * hashDirective: ParsedHashDirective * range: range -> 'T option

      abstract VisitImplicitInherit: path: SyntaxVisitorPath * defaultTraverse: (SynExpr -> 'T option) * inheritedType: SynType * synArgs: SynExpr * range: range -> 'T option 
      default VisitImplicitInherit: path: SyntaxVisitorPath * defaultTraverse: (SynExpr -> 'T option) * inheritedType: SynType * synArgs: SynExpr * range: range -> 'T option

      abstract VisitInheritSynMemberDefn: path: SyntaxVisitorPath * componentInfo: SynComponentInfo * typeDefnKind: SynTypeDefnKind * synType: SynType * members: SynMemberDefns * range: range -> 'T option
      default VisitInheritSynMemberDefn: path: SyntaxVisitorPath * componentInfo: SynComponentInfo * typeDefnKind: SynTypeDefnKind * synType: SynType * members: SynMemberDefns * range: range -> 'T option

      abstract VisitRecordDefn: path: SyntaxVisitorPath * fields: SynField list * range -> 'T option
      default VisitRecordDefn: path: SyntaxVisitorPath * fields: SynField list * range -> 'T option

      abstract VisitUnionDefn: path: SyntaxVisitorPath * cases: SynUnionCase list * range -> 'T option
      default VisitUnionDefn: path: SyntaxVisitorPath * cases: SynUnionCase list * range -> 'T option

      abstract VisitEnumDefn: path: SyntaxVisitorPath * cases: SynEnumCase list * range -> 'T option
      default VisitEnumDefn: path: SyntaxVisitorPath * cases: SynEnumCase list * range -> 'T option

      abstract VisitInterfaceSynMemberDefnType: path: SyntaxVisitorPath * synType: SynType -> 'T option
      default VisitInterfaceSynMemberDefnType: path: SyntaxVisitorPath * synType: SynType -> 'T option

      abstract VisitLetOrUse: path: SyntaxVisitorPath * isRecursive: bool * defaultTraverse: (SynBinding -> 'T option) * bindings: SynBinding list * range: range -> 'T option
      default VisitLetOrUse: path: SyntaxVisitorPath * isRecursive: bool * defaultTraverse: (SynBinding -> 'T option) * bindings: SynBinding list * range: range -> 'T option

      abstract VisitMatchClause: path: SyntaxVisitorPath * defaultTraverse: (SynMatchClause -> 'T option) * matchClause: SynMatchClause -> 'T option
      default VisitMatchClause: path: SyntaxVisitorPath * defaultTraverse: (SynMatchClause -> 'T option) * matchClause: SynMatchClause -> 'T option

      abstract VisitModuleDecl: path: SyntaxVisitorPath * defaultTraverse: (SynModuleDecl -> 'T option) * synModuleDecl: SynModuleDecl -> 'T option
      default VisitModuleDecl: path: SyntaxVisitorPath * defaultTraverse: (SynModuleDecl -> 'T option) * synModuleDecl: SynModuleDecl -> 'T option

      abstract VisitModuleOrNamespace: path: SyntaxVisitorPath * synModuleOrNamespace: SynModuleOrNamespace -> 'T option
      default VisitModuleOrNamespace: path: SyntaxVisitorPath * synModuleOrNamespace: SynModuleOrNamespace -> 'T option

      abstract VisitPat: path: SyntaxVisitorPath * defaultTraverse: (SynPat -> 'T option) * synPat: SynPat -> 'T option
      default VisitPat: path: SyntaxVisitorPath * defaultTraverse: (SynPat -> 'T option) * synPat: SynPat -> 'T option

      abstract VisitRecordField: path: SyntaxVisitorPath * copyOpt: SynExpr option * recordField: LongIdentWithDots option -> 'T option
      default VisitRecordField: path: SyntaxVisitorPath * copyOpt: SynExpr option * recordField: LongIdentWithDots option -> 'T option

      abstract VisitSimplePats: path: SyntaxVisitorPath * synPats: SynSimplePat list -> 'T option
      default VisitSimplePats: path: SyntaxVisitorPath * synPats: SynSimplePat list -> 'T option

      abstract VisitType: path: SyntaxVisitorPath * defaultTraverse: (SynType -> 'T option) * synType: SynType -> 'T option
      default VisitType: path: SyntaxVisitorPath * defaultTraverse: (SynType -> 'T option) * synType: SynType -> 'T option

      abstract VisitTypeAbbrev: path: SyntaxVisitorPath * synType: SynType * range: range -> 'T option
      default VisitTypeAbbrev: path: SyntaxVisitorPath * synType: SynType * range: range -> 'T option

module public SyntaxTraversal = 

  val internal rangeContainsPosLeftEdgeInclusive: m1: range -> p: pos -> bool

  val internal rangeContainsPosEdgesExclusive: m1: range -> p: pos -> bool

  val internal rangeContainsPosLeftEdgeExclusiveAndRightEdgeInclusive: m1: range -> p: pos -> bool

  val internal dive: node: 'a -> range: 'b -> project: ('a -> 'c) -> 'b * (unit -> 'c)

  val internal pick: pos: pos -> outerRange: range -> debugObj: obj -> diveResults: (range * (unit -> 'a option)) list -> 'a option

  val Traverse: pos: pos * parseTree: ParsedInput * visitor: SyntaxVisitorBase<'T> -> 'T option
