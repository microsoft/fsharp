// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Primary logic related to method overrides.
module internal FSharp.Compiler.MethodOverrides

open Internal.Utilities.Library 
open FSharp.Compiler.AccessibilityLogic
open FSharp.Compiler.InfoReader
open FSharp.Compiler.Import
open FSharp.Compiler.Infos
open FSharp.Compiler.NameResolution
open FSharp.Compiler.Syntax
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeOps

type OverrideCanImplement =
    | CanImplementAnyInterfaceSlot
    | CanImplementAnyClassHierarchySlot
    | CanImplementAnySlot
    | CanImplementNoSlots

/// The overall information about a method implementation in a class or object expression 
type OverrideInfo =
    | Override of OverrideCanImplement * TyconRef * Ident * (Typars * TyparInst) * TType list list * TType option * bool * bool
    
    member ArgTypes: TType list list
    
    member BoundingTyconRef: TyconRef
    
    member CanImplement: OverrideCanImplement
    
    member IsCompilerGenerated: bool
    
    member IsFakeEventProperty: bool
    
    member LogicalName: string
    
    member Range: range
    
    member ReturnType: TType option
  
type RequiredSlot =
    | RequiredSlot of MethInfo * isOptional: bool
    | DefaultInterfaceImplementationSlot of MethInfo * isOptional: bool * possiblyNoMostSpecific: bool

    /// Indicates a slot which has a default interface implementation.
    /// A combination of this flag and the lack of IsOptional means the slot may have been reabstracted.
    member HasDefaultInterfaceImplementation: bool

    /// Indicates a slot which does not have to be implemented, because an inherited implementation is available.
    member IsOptional: bool

    /// Gets the method info.
    member MethodInfo: MethInfo

    /// A slot that *might* have ambiguity due to multiple inheritance; happens with default interface implementations.
    member PossiblyNoMostSpecificImplementation: bool
  
type SlotImplSet = SlotImplSet of RequiredSlot list * NameMultiMap<RequiredSlot> * OverrideInfo list * PropInfo list

exception TypeIsImplicitlyAbstract of range

exception OverrideDoesntOverride of DisplayEnv * OverrideInfo * MethInfo option * TcGlobals * ImportMap * range

module DispatchSlotChecking =
    /// Format the signature of an override as a string as part of an error message
    val FormatOverride: denv:DisplayEnv -> d:OverrideInfo -> string

    /// Format the signature of a MethInfo as a string as part of an error message
    val FormatMethInfoSig: g:TcGlobals -> amap:ImportMap -> m:range -> denv:DisplayEnv -> d:MethInfo -> string

    /// Get the override information for an object expression method being used to implement dispatch slots
    val GetObjectExprOverrideInfo: g:TcGlobals -> amap:ImportMap -> implty:TType * id:Ident * memberFlags:SynMemberFlags * ty:TType * arityInfo:ValReprInfo * bindingAttribs:Attribs * rhsExpr:Expr -> OverrideInfo * (Val option * Val * Val list list * Attribs * Expr)

    /// Check if an override exactly matches the requirements for a dispatch slot.
    val IsExactMatch: g:TcGlobals -> amap:ImportMap -> m:range -> dispatchSlot:MethInfo -> overrideBy:OverrideInfo -> bool

    /// Check all dispatch slots are implemented by some override.
    val CheckDispatchSlotsAreImplemented: denv:DisplayEnv * infoReader:InfoReader * m:range * nenv:NameResolutionEnv * sink:TcResultsSink * isOverallTyAbstract:bool * reqdTy:TType * dispatchSlots:RequiredSlot list * availPriorOverrides:OverrideInfo list * overrides:OverrideInfo list -> bool

    /// Check all implementations implement some dispatch slot.
    val CheckOverridesAreAllUsedOnce: denv:DisplayEnv * g:TcGlobals * infoReader:InfoReader * isObjExpr:bool * reqdTy:TType * dispatchSlotsKeyed:NameMultiMap<RequiredSlot> * availPriorOverrides:OverrideInfo list * overrides:OverrideInfo list -> unit

    /// Get the slots of a type that can or must be implemented. 
    val GetSlotImplSets: infoReader:InfoReader -> denv:DisplayEnv -> ad:AccessorDomain -> isObjExpr:bool -> allReqdTys:(TType * range) list -> SlotImplSet list

/// "Type Completion" inference and a few other checks at the end of the inference scope
val FinalTypeDefinitionChecksAtEndOfInferenceScope: infoReader:InfoReader * nenv:NameResolutionEnv * sink:TcResultsSink * isImplementation:bool * denv:DisplayEnv -> tycon:Tycon -> unit

/// Get the methods relevant to determining if a uniquely-identified-override exists based on the syntactic information 
/// at the member signature prior to type inference. This is used to pre-assign type information if it does 
val GetAbstractMethInfosForSynMethodDecl: infoReader:InfoReader * ad:AccessorDomain * memberName:Ident * bindm:range * typToSearchForAbstractMembers:(TType * SlotImplSet option) * valSynData:SynValInfo -> MethInfo list * MethInfo list

/// Get the properties relevant to determining if a uniquely-identified-override exists based on the syntactic information 
/// at the member signature prior to type inference. This is used to pre-assign type information if it does 
val GetAbstractPropInfosForSynPropertyDecl: infoReader:InfoReader * ad:AccessorDomain * memberName:Ident * bindm:range * typToSearchForAbstractMembers:(TType * SlotImplSet option) * _k:'a * _valSynData:'b -> PropInfo list

