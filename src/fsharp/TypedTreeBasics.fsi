// Copyright (c) Microsoft Corporation. All Rights Reserved. See License.txt in the project root for license information.

//-------------------------------------------------------------------------
// Defines the typed abstract syntax trees used throughout the F# compiler.
//------------------------------------------------------------------------- 

module internal FSharp.Compiler.TypedTreeBasics

open Internal.Utilities.Library.Extras
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text
open FSharp.Compiler.TypedTree

val getNameOfScopeRef: sref:ILScopeRef -> string

/// Metadata on values (names of arguments etc. 
module ValReprInfo =

    val unnamedTopArg1: ArgReprInfo

    val unnamedTopArg: ArgReprInfo list

    val unitArgData: ArgReprInfo list list

    val unnamedRetVal: ArgReprInfo

    val selfMetadata: ArgReprInfo list

    val emptyValData: ValReprInfo

    val InferTyparInfo: tps:Typar list -> TyparReprInfo list

    val InferArgReprInfo: v:Val -> ArgReprInfo

    val InferArgReprInfos: vs:Val list list -> ValReprInfo

    val HasNoArgs: ValReprInfo -> bool

val typeOfVal: v:Val -> TType

val typesOfVals: v:Val list -> TType list

val nameOfVal: v:Val -> string

val arityOfVal: v:Val -> ValReprInfo

val tupInfoRef: TupInfo

val tupInfoStruct: TupInfo

val mkTupInfo: b:bool -> TupInfo

val structnessDefault: bool

val mkRawRefTupleTy: tys:TTypes -> TType

val mkRawStructTupleTy: tys:TTypes -> TType

val mapTImplFile: f:(ModuleOrNamespaceExprWithSig -> ModuleOrNamespaceExprWithSig) -> TypedImplFile -> TypedImplFile

val mapAccImplFile: f:('a -> ModuleOrNamespaceExprWithSig -> ModuleOrNamespaceExprWithSig * 'b) -> z:'a -> TypedImplFile -> TypedImplFile * 'b

val foldTImplFile: f:('a -> ModuleOrNamespaceExprWithSig -> 'b) -> z:'a -> TypedImplFile -> 'b

val typarEq: lv1:Typar -> lv2:Typar -> bool

/// Equality on type variables, implemented as reference equality. This should be equivalent to using typarEq.
val typarRefEq: tp1:Typar -> tp2:Typar -> bool

/// Equality on value specs, implemented as reference equality
val valEq: lv1:Val -> lv2:Val -> bool

/// Equality on CCU references, implemented as reference equality except when unresolved
val ccuEq: mv1:CcuThunk -> mv2:CcuThunk -> bool

/// For dereferencing in the middle of a pattern
val ( |ValDeref| ): vr:ValRef -> Val

val mkRecdFieldRef: tcref:TyconRef -> f:string -> RecdFieldRef

val mkUnionCaseRef: tcref:TyconRef -> c:string -> UnionCaseRef

val ERefLocal: x:NonNullSlot<Entity> -> EntityRef

val ERefNonLocal: x:NonLocalEntityRef -> EntityRef

val ERefNonLocalPreResolved: x:NonNullSlot<Entity> -> xref:NonLocalEntityRef -> EntityRef

val ( |ERefLocal|ERefNonLocal| ): x:EntityRef -> Choice<NonNullSlot<Entity>,NonLocalEntityRef>

val mkLocalTyconRef: x:NonNullSlot<Entity> -> EntityRef

val mkNonLocalEntityRef: ccu:CcuThunk -> mp:string [] -> NonLocalEntityRef

val mkNestedNonLocalEntityRef: nleref:NonLocalEntityRef -> id:string -> NonLocalEntityRef

val mkNonLocalTyconRef: nleref:NonLocalEntityRef -> id:string -> EntityRef

val mkNonLocalTyconRefPreResolved: x:NonNullSlot<Entity> -> nleref:NonLocalEntityRef -> id:string -> EntityRef

type EntityRef with
    member NestedTyconRef: x:Entity -> EntityRef
    member RecdFieldRefInNestedTycon: tycon:Entity -> id:Ident -> RecdFieldRef

/// Make a reference to a union case for type in a module or namespace
val mkModuleUnionCaseRef: modref:ModuleOrNamespaceRef -> tycon:Entity -> uc:UnionCase -> UnionCaseRef val VRefLocal: x:NonNullSlot<Val> -> ValRef

val VRefNonLocal: x:NonLocalValOrMemberRef -> ValRef

val VRefNonLocalPreResolved: x:NonNullSlot<Val> -> xref:NonLocalValOrMemberRef -> ValRef

val ( |VRefLocal|VRefNonLocal| ): x:ValRef -> Choice<NonNullSlot<Val>,NonLocalValOrMemberRef>

val mkNonLocalValRef: mp:NonLocalEntityRef -> id:ValLinkageFullKey -> ValRef

val mkNonLocalValRefPreResolved: x:NonNullSlot<Val> -> mp:NonLocalEntityRef -> id:ValLinkageFullKey -> ValRef

val ccuOfValRef: vref:ValRef -> CcuThunk option

val ccuOfTyconRef: eref:EntityRef -> CcuThunk option

val NewNullnessVar: unit -> Nullness

val KnownAmbivalentToNull: Nullness

val KnownWithNull: Nullness

val KnownWithoutNull: Nullness

val combineNullness: Nullness -> Nullness -> Nullness

val tryAddNullnessToTy: Nullness -> TType -> TType option

val addNullnessToTy: Nullness -> TType -> TType

val mkTyparTy: tp:Typar -> TType

val copyTypar: tp:Typar -> Typar

val copyTypars: tps:Typar list -> Typar list

val tryShortcutSolvedUnitPar: canShortcut:bool -> r:Typar -> Measure

val stripUnitEqnsAux: canShortcut:bool -> unt:Measure -> Measure

val stripTyparEqnsAux: nullness0: Nullness -> canShortcut:bool -> ty:TType -> TType

val replaceNullnessOfTy: nullness: Nullness -> ty:TType -> TType

val stripTyparEqns: ty:TType -> TType

val stripUnitEqns: unt:Measure -> Measure

val mkLocalValRef: v:Val -> ValRef

val mkLocalModRef: v:ModuleOrNamespace -> EntityRef

val mkLocalEntityRef: v:Entity -> EntityRef

val mkNonLocalCcuRootEntityRef: ccu:CcuThunk -> x:Entity -> EntityRef

val mkNestedValRef:cref:EntityRef -> v:Val -> ValRef

/// From Ref_private to Ref_nonlocal when exporting data.
val rescopePubPathToParent:viewedCcu:CcuThunk -> PublicPath -> NonLocalEntityRef

/// From Ref_private to Ref_nonlocal when exporting data.
val rescopePubPath: viewedCcu:CcuThunk -> PublicPath -> NonLocalEntityRef

val valRefInThisAssembly: compilingFslib:bool -> x:ValRef -> bool

val tyconRefUsesLocalXmlDoc: compilingFslib:bool -> x:TyconRef -> bool

val entityRefInThisAssembly: compilingFslib:bool -> x:EntityRef -> bool

val arrayPathEq: y1:string [] -> y2:string [] -> bool

val nonLocalRefEq:NonLocalEntityRef -> NonLocalEntityRef -> bool

/// This predicate tests if non-local resolution paths are definitely known to resolve
/// to different entities. All references with different named paths always resolve to 
/// different entities. Two references with the same named paths may resolve to the same 
/// entities even if they reference through different CCUs, because one reference
/// may be forwarded to another via a .NET TypeForwarder.
val nonLocalRefDefinitelyNotEq: NonLocalEntityRef -> NonLocalEntityRef -> bool

val pubPathEq: PublicPath -> PublicPath -> bool

val fslibRefEq: nlr1:NonLocalEntityRef -> PublicPath -> bool

/// Compare two EntityRef's for equality when compiling fslib (FSharp.Core.dll)
val fslibEntityRefEq: fslibCcu:CcuThunk -> eref1:EntityRef -> eref2:EntityRef -> bool

// Compare two ValRef's for equality when compiling fslib (FSharp.Core.dll)
val fslibValRefEq: fslibCcu:CcuThunk -> vref1:ValRef -> vref2:ValRef -> bool

/// Primitive routine to compare two EntityRef's for equality
/// This takes into account the possibility that they may have type forwarders
val primEntityRefEq: compilingFslib:bool -> fslibCcu:CcuThunk -> x:EntityRef -> y:EntityRef -> bool

/// Primitive routine to compare two UnionCaseRef's for equality
val primUnionCaseRefEq: compilingFslib:bool -> fslibCcu:CcuThunk -> UnionCaseRef -> UnionCaseRef -> bool

/// Primitive routine to compare two ValRef's for equality. On the whole value identity is not particularly
/// significant in F#. However it is significant for
///    (a) Active Patterns 
///    (b) detecting uses of "special known values" from FSharp.Core.dll, such as 'seq' 
///        and quotation splicing 
///
/// Note this routine doesn't take type forwarding into account
val primValRefEq: compilingFslib:bool -> fslibCcu:CcuThunk -> x:ValRef -> y:ValRef -> bool

val fullCompPathOfModuleOrNamespace: m:ModuleOrNamespace -> CompilationPath

val inline canAccessCompPathFrom: CompilationPath -> CompilationPath -> bool

val canAccessFromOneOf: cpaths:CompilationPath list -> cpathTest:CompilationPath -> bool

val canAccessFrom: Accessibility -> cpath:CompilationPath -> bool

val canAccessFromEverywhere: Accessibility -> bool

val canAccessFromSomewhere: Accessibility -> bool

val isLessAccessible: Accessibility -> Accessibility -> bool

/// Given (newPath, oldPath) replace oldPath by newPath in the TAccess.
val accessSubstPaths: newPath:CompilationPath * oldPath:CompilationPath -> Accessibility -> Accessibility

val compPathOfCcu: ccu:CcuThunk -> CompilationPath

val taccessPublic: Accessibility

val taccessPrivate: accessPath:CompilationPath -> Accessibility

val compPathInternal: CompilationPath

val taccessInternal: Accessibility

val combineAccess: Accessibility -> Accessibility -> Accessibility

exception Duplicate of string * string * range

exception NameClash of string * string * string * range * string * string * range

