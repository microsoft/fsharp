// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Generate the hash/compare functions we add to user-defined types by default.
module internal FSharp.Compiler.AugmentWithHashCompare 

open FSharp.Compiler 
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TcGlobals

val CheckAugmentationAttribs: bool -> TcGlobals -> Import.ImportMap -> Tycon -> unit

val TyconIsCandidateForAugmentationWithCompare: TcGlobals -> Tycon -> bool

val TyconIsCandidateForAugmentationWithEquals: TcGlobals -> Tycon -> bool

val TyconIsCandidateForAugmentationWithHash: TcGlobals -> Tycon -> bool

val MakeValsForCompareAugmentation: TcGlobals -> TyconRef -> Val * Val

val MakeValsForCompareWithComparerAugmentation: TcGlobals -> TyconRef -> Val

val MakeValsForEqualsAugmentation: TcGlobals -> TyconRef -> Val * Val

val MakeValsForEqualityWithComparerAugmentation: TcGlobals -> TyconRef -> Val * Val * Val

val MakeBindingsForCompareAugmentation: TcGlobals -> Tycon -> Binding list

val MakeBindingsForCompareWithComparerAugmentation: TcGlobals -> Tycon -> Binding list

val MakeBindingsForEqualsAugmentation: TcGlobals -> Tycon -> Binding list

val MakeBindingsForEqualityWithComparerAugmentation: TcGlobals -> Tycon -> Binding list

/// This predicate can be used once type inference is complete, before then it is an approximation
/// that doesn't assert any new constraints
val TypeDefinitelyHasEquality: TcGlobals -> TType -> bool

val MakeValsForUnionAugmentation: TcGlobals -> TyconRef -> Val list

val MakeBindingsForUnionAugmentation: TcGlobals -> Tycon -> ValRef list -> Binding list
