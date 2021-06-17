// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.AbstractIL.Morphs 

open System.Collections.Generic
open FSharp.Compiler.AbstractIL 
open Internal.Utilities.Library 
open FSharp.Compiler.AbstractIL.IL 

let mutable morphCustomAttributeData = false

let enableMorphCustomAttributeData() = 
    morphCustomAttributeData <- true

let disableMorphCustomAttributeData() =
    morphCustomAttributeData <- false

let code_instr2instr f (code: ILCode) = {code with Instrs= Array.map f code.Instrs}

let code_instr2instrs f (code: ILCode) = 
    let instrs = code.Instrs
    let codebuf = ResizeArray()
    let adjust = Dictionary()
    let mutable old = 0
    let mutable nw = 0
    for instr in instrs do 
        adjust.[old] <- nw
        let instrs : list<_> = f instr
        for instr2 in instrs do
            codebuf.Add instr2
            nw <- nw + 1
        old <- old + 1
    adjust.[old] <- nw
    let labels =
        let dict = Dictionary.newWithSize code.Labels.Count
        for kvp in code.Labels do dict.Add(kvp.Key, adjust.[kvp.Value])
        dict
    { code with 
         Instrs = codebuf.ToArray()
         Labels = labels }



let code_instr2instr_ty2ty  (finstr,fty) (c:ILCode) = 
    let c = code_instr2instr finstr c
    { c with 
           Exceptions = c.Exceptions |> List.map (fun e -> { e with Clause = e.Clause |> (function ILExceptionClause.TypeCatch (ilty, b) -> ILExceptionClause.TypeCatch (fty ilty, b) | cl -> cl) }) } 

// --------------------------------------------------------------------
// Standard morphisms - mapping types etc.
// -------------------------------------------------------------------- 

let rec ty_tref2tref f x  = 
    match x with 
    | ILType.Ptr t -> ILType.Ptr (ty_tref2tref f t)
    | ILType.FunctionPointer x -> 
        ILType.FunctionPointer
          { x with 
                ArgTypes=List.map (ty_tref2tref f) x.ArgTypes
                ReturnType=ty_tref2tref f x.ReturnType}
    | ILType.Byref t -> ILType.Byref (ty_tref2tref f t)
    | ILType.Boxed cr -> mkILBoxedType (tspec_tref2tref f cr)
    | ILType.Value ir -> ILType.Value (tspec_tref2tref f ir)
    | ILType.Array (s,ty) -> ILType.Array (s,ty_tref2tref f ty)
    | ILType.TypeVar v ->  ILType.TypeVar v 
    | ILType.Modified (req,tref,ty) ->  ILType.Modified (req, f tref, ty_tref2tref f ty) 
    | ILType.Void -> ILType.Void
and tspec_tref2tref f (x:ILTypeSpec) = 
    mkILTySpec(f x.TypeRef, List.map (ty_tref2tref f) x.GenericArgs)

let rec ty_scoref2scoref_tyvar2ty ((_fscope,ftyvar) as fs)x  = 
    match x with 
    | ILType.Ptr t -> ILType.Ptr (ty_scoref2scoref_tyvar2ty fs t)
    | ILType.FunctionPointer t -> ILType.FunctionPointer (callsig_scoref2scoref_tyvar2ty fs t)
    | ILType.Byref t -> ILType.Byref (ty_scoref2scoref_tyvar2ty fs t)
    | ILType.Boxed cr -> mkILBoxedType (tspec_scoref2scoref_tyvar2ty fs cr)
    | ILType.Value ir -> ILType.Value (tspec_scoref2scoref_tyvar2ty fs ir)
    | ILType.Array (s,ty) -> ILType.Array (s,ty_scoref2scoref_tyvar2ty fs ty)
    | ILType.TypeVar v ->  ftyvar v
    | x -> x
and tspec_scoref2scoref_tyvar2ty fs (x:ILTypeSpec) = 
    ILTypeSpec.Create(morphILScopeRefsInILTypeRef (fst fs) x.TypeRef,tys_scoref2scoref_tyvar2ty fs x.GenericArgs)
and callsig_scoref2scoref_tyvar2ty f x = 
    { x with 
          ArgTypes=List.map (ty_scoref2scoref_tyvar2ty f) x.ArgTypes
          ReturnType=ty_scoref2scoref_tyvar2ty f x.ReturnType}
and tys_scoref2scoref_tyvar2ty f i = List.map (ty_scoref2scoref_tyvar2ty f) i
and gparams_scoref2scoref_tyvar2ty f i = List.map (gparam_scoref2scoref_tyvar2ty f) i
and gparam_scoref2scoref_tyvar2ty _f i = i
and morphILScopeRefsInILTypeRef fscope (x:ILTypeRef) = 
    ILTypeRef.Create(scope=fscope x.Scope, enclosing=x.Enclosing, name = x.Name)


let callsig_ty2ty f (x: ILCallingSignature) = 
    { CallingConv=x.CallingConv
      ArgTypes=List.map f x.ArgTypes
      ReturnType=f x.ReturnType}

let gparam_ty2ty f gf = {gf with Constraints = List.map f gf.Constraints}
let gparams_ty2ty f gfs = List.map (gparam_ty2ty f) gfs
let tys_ty2ty (f: ILType -> ILType)  x = List.map f x
let mref_ty2ty (f: ILType -> ILType) (x:ILMethodRef) = 
    ILMethodRef.Create(enclosingTypeRef= (f (mkILBoxedType (mkILNonGenericTySpec x.DeclaringTypeRef))).TypeRef,
                       callingConv=x.CallingConv,
                       name=x.Name,
                       genericArity=x.GenericArity,
                       argTypes= List.map f x.ArgTypes,
                       returnType= f x.ReturnType)


type formal_scopeCtxt =  Choice<ILMethodSpec, ILFieldSpec>

let mspec_ty2ty (((factualty : ILType -> ILType), (fformalty: formal_scopeCtxt -> ILType -> ILType))) (x: ILMethodSpec) = 
    mkILMethSpecForMethRefInTy(mref_ty2ty (fformalty (Choice1Of2 x)) x.MethodRef,
                               factualty x.DeclaringType, 
                               tys_ty2ty factualty  x.GenericArgs)

let fref_ty2ty (f: ILType -> ILType) x = 
    { x with DeclaringTypeRef = (f (mkILBoxedType (mkILNonGenericTySpec x.DeclaringTypeRef))).TypeRef
             Type= f x.Type }

let fspec_ty2ty ((factualty,(fformalty : formal_scopeCtxt -> ILType -> ILType))) x = 
    { FieldRef=fref_ty2ty (fformalty (Choice2Of2 x)) x.FieldRef
      DeclaringType= factualty x.DeclaringType }

let rec celem_ty2ty f celem =
    match celem with
    | ILAttribElem.Type (Some ty)    -> ILAttribElem.Type (Some (f ty))
    | ILAttribElem.TypeRef (Some tref) -> ILAttribElem.TypeRef (Some (f (mkILBoxedType (mkILNonGenericTySpec tref))).TypeRef)
    | ILAttribElem.Array (elemTy,elems) -> ILAttribElem.Array (f elemTy, List.map (celem_ty2ty f) elems)
    | _ -> celem

let cnamedarg_ty2ty f ((nm, ty, isProp, elem) : ILAttributeNamedArg)  =
    (nm, f ty, isProp, celem_ty2ty f elem) 

let cattr_ty2ty f (c: ILAttribute) =
    let meth = mspec_ty2ty (f, (fun _ -> f)) c.Method
    // dev11 M3 defensive coding: if anything goes wrong with attribute decoding or encoding, then back out.
    if morphCustomAttributeData then
        try 
           let elems,namedArgs = IL.decodeILAttribData c 
           let elems = elems |> List.map (celem_ty2ty f)
           let namedArgs = namedArgs |> List.map (cnamedarg_ty2ty f)
           mkILCustomAttribMethRef (meth, elems, namedArgs)
        with _ ->
           c.WithMethod(meth)
    else
        c.WithMethod(meth)


let cattrs_ty2ty f (cs: ILAttributes) =
    mkILCustomAttrs (List.map (cattr_ty2ty f) cs.AsList)

let fdef_ty2ty ftye (fd: ILFieldDef) = 
    fd.With(fieldType=ftye fd.FieldType,
            customAttrs=cattrs_ty2ty ftye fd.CustomAttrs)

let local_ty2ty f (l: ILLocal) = {l with Type = f l.Type}
let varargs_ty2ty f (varargs: ILVarArgs) = Option.map (List.map f) varargs
(* REVIEW: convert varargs *)
let morphILTypesInILInstr ((factualty,fformalty)) i = 
    let factualty = factualty (Some i) 
    let conv_fspec fr = fspec_ty2ty (factualty,fformalty (Some i)) fr 
    let conv_mspec mr = mspec_ty2ty (factualty,fformalty (Some i)) mr 
    match i with 
    | I_calli (a,mref,varargs) ->  I_calli (a,callsig_ty2ty (factualty) mref,varargs_ty2ty factualty varargs)
    | I_call (a,mr,varargs) ->  I_call (a,conv_mspec mr,varargs_ty2ty factualty varargs)
    | I_callvirt (a,mr,varargs) ->   I_callvirt (a,conv_mspec mr,varargs_ty2ty factualty varargs)
    | I_callconstraint (a,ty,mr,varargs) ->   I_callconstraint (a,factualty ty,conv_mspec mr,varargs_ty2ty factualty varargs)
    | I_newobj (mr,varargs) ->  I_newobj (conv_mspec mr,varargs_ty2ty factualty varargs)
    | I_ldftn mr ->  I_ldftn (conv_mspec mr)
    | I_ldvirtftn mr ->  I_ldvirtftn (conv_mspec mr)
    | I_ldfld (a,b,fr) ->  I_ldfld (a,b,conv_fspec fr)
    | I_ldsfld (a,fr) ->  I_ldsfld (a,conv_fspec fr)
    | I_ldsflda (fr) ->  I_ldsflda (conv_fspec fr)
    | I_ldflda fr ->  I_ldflda (conv_fspec fr)
    | I_stfld (a,b,fr) -> I_stfld (a,b,conv_fspec fr)
    | I_stsfld (a,fr) -> I_stsfld (a,conv_fspec fr)
    | I_castclass ty -> I_castclass (factualty ty)
    | I_isinst ty -> I_isinst (factualty ty)
    | I_initobj ty -> I_initobj (factualty ty)
    | I_cpobj ty -> I_cpobj (factualty ty)
    | I_stobj (al,vol,ty) -> I_stobj (al,vol,factualty ty)
    | I_ldobj (al,vol,ty) -> I_ldobj (al,vol,factualty ty)
    | I_box ty -> I_box (factualty ty)
    | I_unbox ty -> I_unbox (factualty ty)
    | I_unbox_any ty -> I_unbox_any (factualty ty)
    | I_ldelem_any (shape,ty) ->  I_ldelem_any (shape,factualty ty)
    | I_stelem_any (shape,ty) ->  I_stelem_any (shape,factualty ty)
    | I_newarr (shape,ty) ->  I_newarr (shape,factualty ty)
    | I_ldelema (ro,isNativePtr,shape,ty) ->  I_ldelema (ro,isNativePtr,shape,factualty ty)
    | I_sizeof ty ->  I_sizeof (factualty ty)
    | I_ldtoken tok -> 
        match tok with 
        | ILToken.ILType ty ->   I_ldtoken (ILToken.ILType (factualty ty))
        | ILToken.ILMethod mr -> I_ldtoken (ILToken.ILMethod (conv_mspec mr))
        | ILToken.ILField fr -> I_ldtoken (ILToken.ILField (conv_fspec fr))
    | x -> x

let return_ty2ty f (r:ILReturn) = {r with Type=f r.Type; CustomAttrsStored= storeILCustomAttrs (cattrs_ty2ty f r.CustomAttrs)}
let param_ty2ty f (p: ILParameter) = {p with Type=f p.Type; CustomAttrsStored= storeILCustomAttrs (cattrs_ty2ty f p.CustomAttrs)}

let morphILMethodDefs f (m:ILMethodDefs) = mkILMethods (List.map f m.AsList)
let fdefs_fdef2fdef f (m:ILFieldDefs) = mkILFields (List.map f m.AsList)

(* use this when the conversion produces just one tye... *)
let morphILTypeDefs f (m: ILTypeDefs) = mkILTypeDefsFromArray (Array.map f m.AsArray)

let locals_ty2ty f ls = List.map (local_ty2ty f) ls

let ilmbody_instr2instr_ty2ty fs (il: ILMethodBody) = 
    let (finstr,ftye) = fs 
    {il with Code=code_instr2instr_ty2ty (finstr,ftye) il.Code
             Locals = locals_ty2ty ftye il.Locals }

let morphILMethodBody (filmbody) (x: MethodBody) = 
    match x with
    | MethodBody.IL il -> 
        let ilCode = filmbody il.Value // Eager
        MethodBody.IL (lazy ilCode)
    | x -> x

let ospec_ty2ty f (OverridesSpec(mref,ty)) = OverridesSpec(mref_ty2ty f mref, f ty)

let mdef_ty2ty_ilmbody2ilmbody fs (md: ILMethodDef)  = 
    let (ftye,filmbody) = fs 
    let ftye' = ftye (Some md) 
    let body' = morphILMethodBody (filmbody (Some md))  md.Body 
    md.With(genericParams=gparams_ty2ty ftye' md.GenericParams,
            body= notlazy body',
            parameters = List.map (param_ty2ty ftye') md.Parameters,
            ret = return_ty2ty ftye' md.Return,
            customAttrs=cattrs_ty2ty ftye' md.CustomAttrs)

let fdefs_ty2ty f x = fdefs_fdef2fdef (fdef_ty2ty f) x

let mdefs_ty2ty_ilmbody2ilmbody fs x = morphILMethodDefs (mdef_ty2ty_ilmbody2ilmbody fs) x

let mimpl_ty2ty f e =
    { Overrides = ospec_ty2ty f e.Overrides
      OverrideBy = mspec_ty2ty (f,(fun _ -> f)) e.OverrideBy; }

let edef_ty2ty f (e: ILEventDef) =
    e.With(eventType = Option.map f e.EventType,
           addMethod = mref_ty2ty f e.AddMethod,
           removeMethod = mref_ty2ty f e.RemoveMethod,
           fireMethod = Option.map (mref_ty2ty f) e.FireMethod,
           otherMethods = List.map (mref_ty2ty f) e.OtherMethods,
           customAttrs = cattrs_ty2ty f e.CustomAttrs)

let pdef_ty2ty f (p: ILPropertyDef) =
    p.With(setMethod = Option.map (mref_ty2ty f) p.SetMethod,
           getMethod = Option.map (mref_ty2ty f) p.GetMethod,
           propertyType = f p.PropertyType,
           args = List.map f p.Args,
           customAttrs = cattrs_ty2ty f p.CustomAttrs)

let pdefs_ty2ty f (pdefs: ILPropertyDefs) = mkILProperties (List.map (pdef_ty2ty f) pdefs.AsList)
let edefs_ty2ty f (edefs: ILEventDefs) = mkILEvents (List.map (edef_ty2ty f) edefs.AsList)

let mimpls_ty2ty f (mimpls : ILMethodImplDefs) = mkILMethodImpls (List.map (mimpl_ty2ty f) mimpls.AsList)

let rec tdef_ty2ty_ilmbody2ilmbody_mdefs2mdefs enc fs (td: ILTypeDef) = 
   let (ftye,fmdefs) = fs 
   let ftye' = ftye (Some (enc,td)) None 
   let mdefs' = fmdefs (enc,td) td.Methods 
   let fdefs' = fdefs_ty2ty ftye' td.Fields 
   td.With(implements= List.map ftye' td.Implements,
           genericParams= gparams_ty2ty ftye' td.GenericParams,
           extends = Option.map ftye' td.Extends,
           methods=mdefs',
           nestedTypes=tdefs_ty2ty_ilmbody2ilmbody_mdefs2mdefs (enc@[td]) fs td.NestedTypes,
           fields=fdefs',
           methodImpls = mimpls_ty2ty ftye' td.MethodImpls,
           events = edefs_ty2ty ftye' td.Events,
           properties = pdefs_ty2ty ftye' td.Properties,
           customAttrs = cattrs_ty2ty ftye' td.CustomAttrs)

and tdefs_ty2ty_ilmbody2ilmbody_mdefs2mdefs enc fs tdefs = 
  morphILTypeDefs (tdef_ty2ty_ilmbody2ilmbody_mdefs2mdefs enc fs) tdefs

// --------------------------------------------------------------------
// Derived versions of the above, e.g. with defaults added
// -------------------------------------------------------------------- 

let manifest_ty2ty f (m : ILAssemblyManifest) =
    { m with CustomAttrsStored = storeILCustomAttrs (cattrs_ty2ty f m.CustomAttrs) }

let morphILTypeInILModule_ilmbody2ilmbody_mdefs2mdefs ((ftye: ILModuleDef -> (ILTypeDef list * ILTypeDef) option -> ILMethodDef option -> ILType -> ILType),fmdefs) m = 

    let ftdefs = tdefs_ty2ty_ilmbody2ilmbody_mdefs2mdefs [] (ftye m,fmdefs m) 

    { m with TypeDefs=ftdefs m.TypeDefs
             CustomAttrsStored= storeILCustomAttrs (cattrs_ty2ty (ftye m None None) m.CustomAttrs)
             Manifest=Option.map (manifest_ty2ty (ftye m None None)) m.Manifest  }
    
let module_instr2instr_ty2ty fs x = 
    let (fcode,ftye) = fs 
    let filmbody modCtxt tdefCtxt mdefCtxt = ilmbody_instr2instr_ty2ty (fcode modCtxt tdefCtxt mdefCtxt, ftye modCtxt (Some tdefCtxt) mdefCtxt) 
    let fmdefs modCtxt tdefCtxt = mdefs_ty2ty_ilmbody2ilmbody (ftye modCtxt (Some tdefCtxt), filmbody modCtxt tdefCtxt) 
    morphILTypeInILModule_ilmbody2ilmbody_mdefs2mdefs (ftye, fmdefs) x 

let morphILInstrsAndILTypesInILModule (f1,f2) x = 
  module_instr2instr_ty2ty (f1, f2) x

let morphILInstrsInILCode f x = code_instr2instrs f x

let morphILTypeInILModule ftye y = 
    let finstr modCtxt tdefCtxt mdefCtxt =
        let fty = ftye modCtxt (Some tdefCtxt) mdefCtxt 
        morphILTypesInILInstr ((fun _instrCtxt -> fty), (fun _instrCtxt _formalCtxt -> fty)) 
    morphILInstrsAndILTypesInILModule (finstr,ftye) y

let morphILTypeRefsInILModuleMemoized f modul = 
    let fty = Tables.memoize (ty_tref2tref f)
    morphILTypeInILModule (fun _ _ _ ty -> fty ty) modul

let morphILScopeRefsInILModuleMemoized f modul = 
    morphILTypeRefsInILModuleMemoized (morphILScopeRefsInILTypeRef f) modul
