// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Print Signatures/Types, for signatures, intellisense, quick info, FSI responses
module internal FSharp.Compiler.NicePrint

open System.Text
open FSharp.Compiler.AbstractIL.IL 
open FSharp.Compiler.AccessibilityLogic
open FSharp.Compiler.Import
open FSharp.Compiler.Infos
open FSharp.Compiler.InfoReader
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.Text
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeOps

module PrintUtilities =
    val layoutBuiltinAttribute: denv:DisplayEnv -> attrib:BuiltinAttribInfo -> Layout

val layoutTyparConstraint: denv:DisplayEnv -> Typar * TyparConstraint -> Layout

val outputType: denv:DisplayEnv -> os:StringBuilder -> x:TType -> unit

val layoutType: denv:DisplayEnv -> x:TType -> Layout

val outputTypars: denv:DisplayEnv -> nm:TaggedText -> os:StringBuilder -> x:Typars -> unit

val outputTyconRef: denv:DisplayEnv -> os:StringBuilder -> x:TyconRef -> unit

val layoutTyconRef: denv:DisplayEnv -> x:TyconRef -> Layout

val layoutConst: g:TcGlobals -> ty:TType -> c:Const -> Layout

val prettyLayoutOfMemberSig: denv:DisplayEnv -> (Typar * TType) list * string * Typars * (TType * ArgReprInfo) list list * TType -> Layout

val prettyLayoutOfUncurriedSig: denv:DisplayEnv -> argInfos:TyparInst -> tau:UncurriedArgInfos -> (TType -> TyparInst * Layout)

val prettyLayoutsOfUnresolvedOverloading: denv:DisplayEnv -> argInfos:(TType * ArgReprInfo) list -> retTy:TType -> genericParameters:seq<TType> -> Layout * Layout * Layout

val dataExprL: denv:DisplayEnv -> expr:Expr -> Layout

val outputValOrMember: denv:DisplayEnv -> infoReader:InfoReader -> os:StringBuilder -> x:ValRef -> unit

val stringValOrMember: denv:DisplayEnv -> infoReader:InfoReader -> x:ValRef -> string

val layoutQualifiedValOrMember: denv:DisplayEnv -> infoReader:InfoReader -> typarInst:TyparInst -> v:ValRef -> TyparInst * Layout

val outputQualifiedValOrMember: denv:DisplayEnv -> infoReader:InfoReader -> os:StringBuilder -> v:ValRef -> unit

val outputQualifiedValSpec: denv:DisplayEnv -> infoReader:InfoReader -> os:StringBuilder -> v:ValRef -> unit

val stringOfQualifiedValOrMember: denv:DisplayEnv -> infoReader:InfoReader -> v:ValRef -> string

val formatMethInfoToBufferFreeStyle: infoReader:InfoReader -> m:range -> denv:DisplayEnv -> buf:StringBuilder -> d:MethInfo -> unit

val prettyLayoutOfMethInfoFreeStyle: infoReader:InfoReader -> m:range -> denv:DisplayEnv -> typarInst:TyparInst -> minfo:MethInfo -> TyparInst * Layout

val prettyLayoutOfPropInfoFreeStyle: g:TcGlobals -> amap:ImportMap -> m:range -> denv:DisplayEnv -> d:PropInfo -> Layout

val stringOfMethInfo: infoReader:InfoReader -> m:range -> denv:DisplayEnv -> d:MethInfo -> string

val stringOfParamData: denv:DisplayEnv -> paramData:ParamData -> string

val layoutOfParamData: denv:DisplayEnv -> paramData:ParamData -> Layout

val layoutExnDef: denv:DisplayEnv -> infoReader:InfoReader -> x:EntityRef -> Layout

val stringOfTyparConstraints: denv:DisplayEnv -> x:(Typar * TyparConstraint) list -> string

val layoutTycon: denv:DisplayEnv -> infoReader:InfoReader -> ad:AccessorDomain -> m:range -> x:Tycon -> Layout

val layoutEntityRef: denv:DisplayEnv -> infoReader:InfoReader -> ad:AccessorDomain -> m:range -> x:EntityRef -> Layout

val layoutUnionCases: denv:DisplayEnv -> infoReader:InfoReader -> enclosingTcref:TyconRef -> x:RecdField list -> Layout

val isGeneratedUnionCaseField: pos:int -> f:RecdField -> bool

val isGeneratedExceptionField: pos:'a -> f:RecdField -> bool

val stringOfTyparConstraint: denv:DisplayEnv -> Typar * TyparConstraint -> string

val stringOfTy: denv:DisplayEnv -> x:TType -> string

val prettyLayoutOfType: denv:DisplayEnv -> x:TType -> Layout

val prettyLayoutOfTypeNoCx: denv:DisplayEnv -> x:TType -> Layout

val prettyStringOfTy: denv:DisplayEnv -> x:TType -> string

val prettyStringOfTyNoCx: denv:DisplayEnv -> x:TType -> string

val stringOfRecdField: denv:DisplayEnv -> infoReader:InfoReader -> enclosingTcref:TyconRef -> x:RecdField -> string

val stringOfUnionCase: denv:DisplayEnv -> infoReader:InfoReader -> enclosingTcref:TyconRef -> x:UnionCase -> string

val stringOfExnDef: denv:DisplayEnv -> infoReader:InfoReader -> x:EntityRef -> string

val stringOfFSAttrib: denv:DisplayEnv -> x:Attrib -> string

val stringOfILAttrib: denv:DisplayEnv -> ILType * ILAttribElem list -> string

val layoutInferredSigOfModuleExpr: showHeader:bool -> denv:DisplayEnv -> infoReader:InfoReader -> ad:AccessorDomain -> m:range -> expr:ModuleOrNamespaceExprWithSig -> Layout

val prettyLayoutOfValOrMember: denv:DisplayEnv -> infoReader:InfoReader -> typarInst:TyparInst -> v:ValRef -> TyparInst * Layout

val prettyLayoutOfValOrMemberNoInst: denv:DisplayEnv -> infoReader:InfoReader -> v:ValRef -> Layout

val prettyLayoutOfMemberNoInstShort: denv:DisplayEnv -> v:Val -> Layout

val prettyLayoutOfInstAndSig: denv:DisplayEnv -> TyparInst * TTypes * TType -> TyparInst * (TTypes * TType) * (Layout list * Layout) * Layout

val minimalStringsOfTwoTypes: denv:DisplayEnv -> t1:TType -> t2:TType -> string * string * string

val minimalStringsOfTwoValues: denv:DisplayEnv -> infoReader:InfoReader -> v1:ValRef -> v2:ValRef -> string * string

val minimalStringOfType: denv:DisplayEnv -> ty:TType -> string
