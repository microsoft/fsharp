// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// The basic logic of private/internal/protected/InternalsVisibleTo/public accessibility
module internal FSharp.Compiler.AccessibilityLogic

open FSharp.Compiler.AbstractIL.IL 
open FSharp.Compiler 
open FSharp.Compiler.Import
open FSharp.Compiler.Infos
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.TypedTree

/// Represents the 'keys' a particular piece of code can use to access other constructs?.
[<NoEquality; NoComparison>]
type AccessorDomain =
    /// cpaths: indicates we have the keys to access any members private to the given paths 
    /// tyconRefOpt:  indicates we have the keys to access any protected members of the super types of 'TyconRef' 
    | AccessibleFrom of cpaths: CompilationPath list * tyconRefOpt: TyconRef option        

    /// An AccessorDomain which returns public items
    | AccessibleFromEverywhere

    /// An AccessorDomain which returns everything but .NET private/internal items.
    /// This is used 
    ///    - when solving member trait constraints, which are solved independently of accessibility 
    ///    - for failure paths in error reporting, e.g. to produce an error that an F# item is not accessible
    ///    - an adhoc use in service.fs to look up a delegate signature
    | AccessibleFromSomeFSharpCode

    /// An AccessorDomain which returns all items
    | AccessibleFromSomewhere

    interface ITraitAccessorDomain

    // Hashing and comparison is used for the memoization tables keyed by an accessor domain.
    // It is dependent on a TcGlobals because of the TyconRef in the data structure
    static member CustomEquals: g:TcGlobals * ad1:AccessorDomain * ad2:AccessorDomain -> bool

    // Hashing and comparison is used for the memoization tables keyed by an accessor domain.
    // It is dependent on a TcGlobals because of the TyconRef in the data structure
    static member CustomGetHashCode: ad:AccessorDomain -> int
  
/// Indicates if an F# item is accessible 
val IsAccessible: ad:AccessorDomain -> taccess:TypedTree.Accessibility -> bool

/// Indicates if an entity is accessible
val IsEntityAccessible: amap:ImportMap -> m:range -> ad:AccessorDomain -> tcref:TypedTree.TyconRef -> bool

/// Check that an entity is accessible
val CheckTyconAccessible: amap:ImportMap -> m:range -> ad:AccessorDomain -> tcref:TypedTree.TyconRef -> bool

/// Indicates if a type definition and its representation contents are accessible
val IsTyconReprAccessible: amap:ImportMap -> m:range -> ad:AccessorDomain -> tcref:TypedTree.TyconRef -> bool

/// Check that a type definition and its representation contents are accessible
val CheckTyconReprAccessible: amap:ImportMap -> m:range -> ad:AccessorDomain -> tcref:TypedTree.TyconRef -> bool

/// Indicates if a type is accessible (both definition and instantiation)
val IsTypeAccessible: g:TcGlobals -> amap:ImportMap -> m:range -> ad:AccessorDomain -> ty:TypedTree.TType -> bool

val IsTypeInstAccessible: g:TcGlobals -> amap:ImportMap -> m:range -> ad:AccessorDomain -> tinst:TypedTree.TypeInst -> bool

/// Indicate if a provided member is accessible
val IsProvidedMemberAccessible: amap:ImportMap -> m:range -> ad:AccessorDomain -> ty:TypedTree.TType -> access:ILMemberAccess -> bool

/// Compute the accessibility of a provided member
val ComputeILAccess: isPublic:bool -> isFamily:bool -> isFamilyOrAssembly:bool -> isFamilyAndAssembly:bool -> ILMemberAccess

val IsILFieldInfoAccessible: g:TcGlobals -> amap:ImportMap -> m:range -> ad:AccessorDomain -> x:ILFieldInfo -> bool

val GetILAccessOfILEventInfo: ILEventInfo -> ILMemberAccess 

val IsILEventInfoAccessible: g:TcGlobals -> amap:ImportMap -> m:range -> ad:AccessorDomain -> einfo:ILEventInfo -> bool

val GetILAccessOfILPropInfo: ILPropInfo -> ILMemberAccess

val IsILPropInfoAccessible: g:TcGlobals -> amap:ImportMap -> m:range -> ad:AccessorDomain -> pinfo:ILPropInfo -> bool

val IsValAccessible: ad:AccessorDomain -> vref:TypedTree.ValRef -> bool

val CheckValAccessible: m:range -> ad:AccessorDomain -> vref:TypedTree.ValRef -> unit

val IsUnionCaseAccessible: amap:ImportMap -> m:range -> ad:AccessorDomain -> ucref:TypedTree.UnionCaseRef -> bool

val CheckUnionCaseAccessible: amap:ImportMap -> m:range -> ad:AccessorDomain -> ucref:TypedTree.UnionCaseRef -> bool

val IsRecdFieldAccessible: amap:ImportMap -> m:range -> ad:AccessorDomain -> rfref:TypedTree.RecdFieldRef -> bool

val CheckRecdFieldAccessible: amap:ImportMap -> m:range -> ad:AccessorDomain -> rfref:TypedTree.RecdFieldRef -> bool

val CheckRecdFieldInfoAccessible: amap:ImportMap -> m:range -> ad:AccessorDomain -> rfinfo:RecdFieldInfo -> unit

val CheckILFieldInfoAccessible: g:TcGlobals -> amap:ImportMap -> m:range -> ad:AccessorDomain -> finfo:ILFieldInfo -> unit

val IsTypeAndMethInfoAccessible: amap:ImportMap -> m:range -> accessDomainTy:AccessorDomain -> ad:AccessorDomain -> _arg1:MethInfo -> bool

val IsMethInfoAccessible: amap:ImportMap -> m:range -> ad:AccessorDomain -> minfo:MethInfo -> bool

val IsPropInfoAccessible: g:TcGlobals ->amap:ImportMap -> m:range -> ad:AccessorDomain -> _arg1:PropInfo -> bool

val IsFieldInfoAccessible: ad:AccessorDomain -> rfref:RecdFieldInfo -> bool
