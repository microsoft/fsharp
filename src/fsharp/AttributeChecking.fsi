// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Logic associated with checking "ObsoleteAttribute" and other attributes 
/// on items from name resolution
module internal FSharp.Compiler.AttributeChecking

open System.Collections.Generic
open FSharp.Compiler.AbstractIL.IL 
open FSharp.Compiler 
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Infos
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.TypedTree

exception ObsoleteWarning of string * range

exception ObsoleteError of string * range

type AttribInfo =
    | FSAttribInfo of TcGlobals * Attrib
    | ILAttribInfo of TcGlobals * Import.ImportMap * ILScopeRef * ILAttribute * range
    member ConstructorArguments: (TType * obj) list
    member NamedArguments: (TType * string * bool * obj) list
    member Range: range
    member TyconRef: TyconRef
  
val AttribInfosOfIL: g:TcGlobals -> amap:Import.ImportMap -> scoref:ILScopeRef -> m:range -> attribs:ILAttributes -> AttribInfo list

val GetAttribInfosOfEntity: g:TcGlobals -> amap:Import.ImportMap -> m:range -> tcref:TyconRef -> AttribInfo list

val GetAttribInfosOfMethod: amap:Import.ImportMap -> m:range -> minfo:MethInfo -> AttribInfo list

val GetAttribInfosOfProp: amap:Import.ImportMap -> m:range -> pinfo:PropInfo -> AttribInfo list

val GetAttribInfosOfEvent: amap:Import.ImportMap -> m:range -> einfo:EventInfo -> AttribInfo list

#if NO_EXTENSIONTYPING
val TryBindMethInfoAttribute: g:TcGlobals -> m:range -> BuiltinAttribInfo -> minfo:MethInfo -> f1:(ILAttribElem list * ILAttributeNamedArg list -> 'a option) -> f2:(Attrib -> 'a option) -> f3: _ -> 'a option
#else
val TryBindMethInfoAttribute: g:TcGlobals -> m:range -> BuiltinAttribInfo -> minfo:MethInfo -> f1:(ILAttribElem list * ILAttributeNamedArg list -> 'a option) -> f2:(Attrib -> 'a option) -> f3:(obj option list * (string * obj option) list -> 'a option) -> 'a option
#endif

val TryFindMethInfoStringAttribute: g:TcGlobals -> m:range -> attribSpec:BuiltinAttribInfo -> minfo:MethInfo -> string option

val MethInfoHasAttribute: g:TcGlobals -> m:range -> attribSpec:BuiltinAttribInfo -> minfo:MethInfo -> bool

val CheckFSharpAttributes: g:TcGlobals -> attribs:Attrib list -> m:range -> OperationResult<unit>

val CheckILAttributesForUnseen: g:TcGlobals -> cattrs:ILAttributes -> _m:'a -> bool

val CheckFSharpAttributesForHidden: g:TcGlobals -> attribs:Attrib list -> bool

val CheckFSharpAttributesForObsolete: g:TcGlobals -> attribs:Attrib list -> bool

val CheckFSharpAttributesForUnseen: g:TcGlobals -> attribs:Attrib list -> _m:'a -> bool

val CheckPropInfoAttributes: pinfo:PropInfo -> m:range -> OperationResult<unit>

val CheckILFieldAttributes: g:TcGlobals -> finfo:ILFieldInfo -> m:range -> unit

val CheckMethInfoAttributes: g:TcGlobals -> m:range -> tyargsOpt:'a option -> minfo:MethInfo -> hasUnammedCallerArgs:bool option -> OperationResult<unit>

val MethInfoIsUnseen: g:TcGlobals -> m:range -> ty:TType -> minfo:MethInfo -> bool

val PropInfoIsUnseen: m:'a -> pinfo:PropInfo -> bool

val CheckEntityAttributes: g:TcGlobals -> x:TyconRef -> m:range -> OperationResult<unit>

val CheckUnionCaseAttributes: g:TcGlobals -> x:UnionCaseRef -> m:range -> OperationResult<unit>

val CheckRecdFieldAttributes: g:TcGlobals -> x:RecdFieldRef -> m:range -> OperationResult<unit>

val CheckValAttributes: g:TcGlobals -> x:ValRef -> m:range -> OperationResult<unit>

val CheckRecdFieldInfoAttributes: g:TcGlobals -> x:RecdFieldInfo -> m:range -> OperationResult<unit>

val IsSecurityAttribute: g:TcGlobals -> amap:Import.ImportMap -> casmap:Dictionary<Stamp,bool> -> Attrib -> m:range -> bool

val IsSecurityCriticalAttribute: g:TcGlobals -> Attrib -> bool

