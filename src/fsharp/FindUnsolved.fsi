// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.FindUnsolved

open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Import

/// Find all unsolved inference variables after type inference for an entire file
val UnsolvedTyparsOfModuleDef: g: TcGlobals -> amap: ImportMap -> denv: DisplayEnv -> mdef : ModuleOrNamespaceExpr * extraAttribs: Attrib list -> Typar list

/// Find all unsolved inference variables after adhoc generation of witness
val UnsolvedTyparsOfExpr: g: TcGlobals -> amap: ImportMap -> denv: DisplayEnv -> expr: Expr -> Typar list
