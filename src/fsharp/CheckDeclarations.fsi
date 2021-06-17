// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.CheckDeclarations

open FSharp.Compiler 
open Internal.Utilities.Library
open FSharp.Compiler.CheckExpressions
open FSharp.Compiler.CompilerGlobalState
open FSharp.Compiler.NameResolution
open FSharp.Compiler.Import
open FSharp.Compiler.Syntax
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.TypedTree

val AddLocalRootModuleOrNamespace : NameResolution.TcResultsSink -> TcGlobals -> ImportMap -> range -> TcEnv -> ModuleOrNamespaceType -> TcEnv

val CreateInitialTcEnv : TcGlobals * ImportMap * range * assemblyName: string * (CcuThunk * string list * string list) list -> TcEnv 

val AddCcuToTcEnv: TcGlobals * ImportMap * range * TcEnv * assemblyName: string * ccu: CcuThunk * autoOpens: string list * internalsVisibleToAttributes: string list -> TcEnv 

type TopAttribs =
    { mainMethodAttrs: Attribs
      netModuleAttrs: Attribs
      assemblyAttrs: Attribs  }

type ConditionalDefines = string list

val EmptyTopAttrs : TopAttribs

val CombineTopAttrs : TopAttribs -> TopAttribs -> TopAttribs

val TcOpenModuleOrNamespaceDecl: TcResultsSink  -> TcGlobals -> ImportMap -> range -> TcEnv -> (LongIdent * range) -> TcEnv

val AddLocalSubModule: g: TcGlobals -> amap: ImportMap -> m: range -> env: TcEnv -> modul: ModuleOrNamespace -> TcEnv

val TypeCheckOneImplFile : 
      TcGlobals * NiceNameGenerator * ImportMap * CcuThunk * (unit -> bool) * ConditionalDefines option * NameResolution.TcResultsSink * bool
      -> TcEnv 
      -> ModuleOrNamespaceType option
      -> ParsedImplFileInput
      -> Cancellable<TopAttribs * TypedImplFile * ModuleOrNamespaceType * TcEnv * bool>

val TypeCheckOneSigFile : 
      TcGlobals * NiceNameGenerator * ImportMap * CcuThunk  * (unit -> bool) * ConditionalDefines option * NameResolution.TcResultsSink * bool
      -> TcEnv                             
      -> ParsedSigFileInput
      -> Cancellable<TcEnv * ModuleOrNamespaceType * bool>

exception ParameterlessStructCtor of range

exception NotUpperCaseConstructor of range
