// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Primary relations on types and signatures, with the exception of
/// constraint solving and method overload resolution.
module internal FSharp.Compiler.SignatureConformance

open System.Text

open FSharp.Compiler 
open FSharp.Compiler.Text
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.InfoReader

exception RequiredButNotSpecified of DisplayEnv * ModuleOrNamespaceRef * string * (StringBuilder -> unit) * range

exception ValueNotContained of DisplayEnv * InfoReader * ModuleOrNamespaceRef * Val * Val * (string * string * string -> string)

exception ConstrNotContained of DisplayEnv * InfoReader * Tycon * UnionCase * UnionCase * (string * string -> string)

exception ExnconstrNotContained of DisplayEnv * InfoReader * Tycon * Tycon * (string * string -> string)

exception FieldNotContained of DisplayEnv * InfoReader * Tycon * RecdField * RecdField * (string * string -> string)

exception InterfaceNotRevealed of DisplayEnv * TType * range

type Checker =

      new: g:TcGlobals.TcGlobals * amap:Import.ImportMap * denv:DisplayEnv * remapInfo:SignatureRepackageInfo * checkingSig:bool -> Checker

      member CheckSignature: aenv:TypeEquivEnv -> infoReader:InfoReader -> implModRef:ModuleOrNamespaceRef -> signModType:ModuleOrNamespaceType -> bool

      member CheckTypars: m:range -> aenv:TypeEquivEnv -> implTypars:Typars -> signTypars:Typars -> bool
  
/// Check the names add up between a signature and its implementation. We check this first.
val CheckNamesOfModuleOrNamespaceContents: denv:DisplayEnv -> infoReader:InfoReader -> implModRef:ModuleOrNamespaceRef -> signModType:ModuleOrNamespaceType -> bool

val CheckNamesOfModuleOrNamespace: denv:DisplayEnv -> infoReader:InfoReader -> implModRef:ModuleOrNamespaceRef -> signModType:ModuleOrNamespaceType -> bool
