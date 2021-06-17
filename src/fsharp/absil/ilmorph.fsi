// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// A set of "IL rewrites" ("morphs").  These map each sub-construct
/// of particular ILTypeDefs.  The morphing functions are passed
/// some details about the context in which the item being
/// morphed occurs, e.g. the module being morphed itself, the
/// ILTypeDef (possibly nested) where the item occurs, 
/// the ILMethodDef (if any) where the item occurs. etc.
module internal FSharp.Compiler.AbstractIL.Morphs 

open FSharp.Compiler.AbstractIL.IL 

/// Morph each scope reference inside a type signature.
val morphILScopeRefsInILTypeRef: (ILScopeRef -> ILScopeRef) -> ILTypeRef -> ILTypeRef 

/// Morph all type references throughout an entire module.
val morphILTypeRefsInILModuleMemoized: (ILTypeRef -> ILTypeRef) ->  ILModuleDef ->  ILModuleDef

val morphILScopeRefsInILModuleMemoized: (ILScopeRef -> ILScopeRef) ->  ILModuleDef ->  ILModuleDef

val morphILInstrsInILCode: (ILInstr -> ILInstr list) -> ILCode -> ILCode

val enableMorphCustomAttributeData : unit -> unit
val disableMorphCustomAttributeData : unit -> unit
