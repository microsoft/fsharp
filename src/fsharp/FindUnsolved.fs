// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Find unsolved, uninstantiated type variables
module internal FSharp.Compiler.FindUnsolved

open Internal.Utilities.Collections
open Internal.Utilities.Library
open FSharp.Compiler
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.TypeRelations

type env = Nix

/// The environment and collector
type cenv = 
    { g: TcGlobals 
      amap: Import.ImportMap 
      denv: DisplayEnv 
      mutable unsolved: Typars }

    override x.ToString() = "<cenv>"

/// Walk types, collecting type variables
let accTy cenv _env ty =
    let normalizedTy = tryNormalizeMeasureInType cenv.g ty
    (freeInType CollectTyparsNoCaching normalizedTy).FreeTypars |> Zset.iter (fun tp -> 
        if (tp.Rigidity <> TyparRigidity.Rigid) then 
            cenv.unsolved <- tp :: cenv.unsolved) 

let accTypeInst cenv env tyargs =
    tyargs |> List.iter (accTy cenv env)

/// Walk expressions, collecting type variables
let rec accExpr   (cenv:cenv) (env:env) expr =     
    let expr = stripExpr expr 
    match expr with
    | Expr.Sequential (e1, e2, _, _, _) -> 
        accExpr cenv env e1 
        accExpr cenv env e2

    | Expr.Let (bind, body, _, _) ->  
        accBind cenv env bind  
        accExpr cenv env body

    | Expr.Const (_, _, ty) -> 
        accTy cenv env ty 
    
    | Expr.Val (_v, _vFlags, _m) -> ()

    | Expr.Quote (ast, _, _, _m, ty) -> 
        accExpr cenv env ast
        accTy cenv env ty

    | Expr.Obj (_, ty, basev, basecall, overrides, iimpls, _m) -> 
        accTy cenv env ty
        accExpr cenv env basecall
        accMethods cenv env basev overrides 
        accIntfImpls cenv env basev iimpls

    | LinearOpExpr (_op, tyargs, argsHead, argLast, _m) ->
        // Note, LinearOpExpr doesn't include any of the "special" cases for accOp
        accTypeInst cenv env tyargs
        accExprs cenv env argsHead
        // tailcall
        accExpr cenv env argLast

    | Expr.Op (c, tyargs, args, m) ->
        accOp cenv env (c, tyargs, args, m) 

    | Expr.App (f, fty, tyargs, argsl, _m) ->
        accTy cenv env fty
        accTypeInst cenv env tyargs
        accExpr cenv env f
        accExprs cenv env argsl

    | Expr.Lambda (_, _ctorThisValOpt, _baseValOpt, argvs, _body, m, rty) -> 
        let topValInfo = ValReprInfo ([], [argvs |> List.map (fun _ -> ValReprInfo.unnamedTopArg1)], ValReprInfo.unnamedRetVal) 
        let ty = mkMultiLambdaTy cenv.g m argvs rty 
        accLambdas cenv env topValInfo expr ty

    | Expr.TyLambda (_, tps, _body, _m, rty)  -> 
        let topValInfo = ValReprInfo (ValReprInfo.InferTyparInfo tps, [], ValReprInfo.unnamedRetVal) 
        accTy cenv env rty
        let ty = mkForallTyIfNeeded tps rty 
        accLambdas cenv env topValInfo expr ty

    | Expr.TyChoose (_tps, e1, _m)  -> 
        accExpr cenv env e1 

    | Expr.Match (_, _exprm, dtree, targets, m, ty) -> 
        accTy cenv env ty
        accDTree cenv env dtree
        accTargets cenv env m ty targets

    | Expr.LetRec (binds, e, _m, _) ->  
        accBinds cenv env binds
        accExpr cenv env e

    | Expr.StaticOptimization (constraints, e2, e3, _m) -> 
        accExpr cenv env e2
        accExpr cenv env e3
        constraints |> List.iter (function 
            | TTyconEqualsTycon(ty1, ty2) -> 
                accTy cenv env ty1
                accTy cenv env ty2
            | TTyconIsStruct(ty1) -> 
                accTy cenv env ty1)

    | Expr.WitnessArg (traitInfo, _m) ->
        accTraitInfo cenv env traitInfo

    | Expr.Link _eref -> failwith "Unexpected Expr.Link"

and accMethods cenv env baseValOpt l = 
    List.iter (accMethod cenv env baseValOpt) l

and accMethod cenv env _baseValOpt (TObjExprMethod(_slotsig, _attribs, _tps, vs, e, _m)) = 
    vs |> List.iterSquared (accVal cenv env)
    accExpr cenv env e

and accIntfImpls cenv env baseValOpt l = 
    List.iter (accIntfImpl cenv env baseValOpt) l

and accIntfImpl cenv env baseValOpt (ty, overrides) = 
    accTy cenv env ty
    accMethods cenv env baseValOpt overrides 

and accOp cenv env (op, tyargs, args, _m) =
    // Special cases 
    accTypeInst cenv env tyargs
    accExprs cenv env args
    match op with 
    // Handle these as special cases since mutables are allowed inside their bodies 
    | TOp.ILCall (_, _, _, _, _, _, _, _, enclTypeInst, methInst, retTypes) ->
        accTypeInst cenv env enclTypeInst
        accTypeInst cenv env methInst
        accTypeInst cenv env retTypes
    | TOp.TraitCall traitInfo -> 
        accTraitInfo cenv env traitInfo
        
    | TOp.ILAsm (_, retTypes) ->
        accTypeInst cenv env retTypes
    | _ ->    ()

and accTraitInfo cenv env (TTrait(tys, _nm, _, argtys, rty, _sln)) =
    argtys |> accTypeInst cenv env 
    rty |> Option.iter (accTy cenv env)
    tys |> List.iter (accTy cenv env)

and accLambdas cenv env topValInfo e ety =
    match e with
    | Expr.TyChoose (_tps, e1, _m)  -> accLambdas cenv env topValInfo e1 ety      
    | Expr.Lambda _
    | Expr.TyLambda _ ->
        let _tps, ctorThisValOpt, baseValOpt, vsl, body, bodyty = destTopLambda cenv.g cenv.amap topValInfo (e, ety) 
        accTy cenv env bodyty
        vsl |> List.iterSquared (accVal cenv env)
        baseValOpt |> Option.iter (accVal cenv env)
        ctorThisValOpt |> Option.iter (accVal cenv env)
        accExpr cenv env body
    | _ -> 
        accExpr cenv env e

and accExprs cenv env exprs = 
    exprs |> List.iter (accExpr cenv env) 

and accTargets cenv env m ty targets = 
    Array.iter (accTarget cenv env m ty) targets

and accTarget cenv env _m _ty (TTarget(_vs, e, _)) = 
    accExpr cenv env e

and accDTree cenv env x =
    match x with 
    | TDSuccess (es, _n) -> accExprs cenv env es
    | TDBind(bind, rest) -> accBind cenv env bind; accDTree cenv env rest 
    | TDSwitch (e, cases, dflt, m) -> accSwitch cenv env (e, cases, dflt, m)

and accSwitch cenv env (e, cases, dflt, _m) =
    accExpr cenv env e
    cases |> List.iter (fun (TCase(discrim, e)) -> accDiscrim cenv env discrim; accDTree cenv env e) 
    dflt |> Option.iter (accDTree cenv env) 

and accDiscrim cenv env d =
    match d with 
    | DecisionTreeTest.UnionCase(_ucref, tinst) -> accTypeInst cenv env tinst 
    | DecisionTreeTest.ArrayLength(_, ty) -> accTy cenv env ty
    | DecisionTreeTest.Const _
    | DecisionTreeTest.IsNull -> ()
    | DecisionTreeTest.IsInst (srcty, tgty) -> accTy cenv env srcty; accTy cenv env tgty
    | DecisionTreeTest.ActivePatternCase (exp, tys, _, _, _, _) -> 
        accExpr cenv env exp
        accTypeInst cenv env tys
    | DecisionTreeTest.Error _ -> ()

and accAttrib cenv env (Attrib(_, _k, args, props, _, _, _m)) = 
    args |> List.iter (fun (AttribExpr(expr1, expr2)) -> 
        accExpr cenv env expr1
        accExpr cenv env expr2)
    props |> List.iter (fun (AttribNamedArg(_nm, ty, _flg, AttribExpr(expr, expr2))) -> 
        accExpr cenv env expr
        accExpr cenv env expr2
        accTy cenv env ty)
  
and accAttribs cenv env attribs = 
    List.iter (accAttrib cenv env) attribs

and accValReprInfo cenv env (ValReprInfo(_, args, ret)) =
    args |> List.iterSquared (accArgReprInfo cenv env)
    ret |> accArgReprInfo cenv env

and accArgReprInfo cenv env (argInfo: ArgReprInfo) = 
    accAttribs cenv env argInfo.Attribs

and accVal cenv env v =
    v.Attribs |> accAttribs cenv env
    v.ValReprInfo |> Option.iter (accValReprInfo cenv env)
    v.Type |> accTy cenv env 

and accBind cenv env (bind:Binding) =
    accVal cenv env bind.Var    
    let topValInfo  = match bind.Var.ValReprInfo with Some info -> info | _ -> ValReprInfo.emptyValData
    accLambdas cenv env topValInfo bind.Expr bind.Var.Type

and accBinds cenv env xs = 
    xs |> List.iter (accBind cenv env) 

let accTyconRecdField cenv env _tycon (rfield:RecdField) = 
    accAttribs cenv env rfield.PropertyAttribs
    accAttribs cenv env rfield.FieldAttribs

let accTycon cenv env (tycon:Tycon) =
    accAttribs cenv env tycon.Attribs
    abstractSlotValsOfTycons [tycon] |> List.iter (accVal cenv env) 
    tycon.AllFieldsArray |> Array.iter (accTyconRecdField cenv env tycon)
    if tycon.IsUnionTycon then                             (* This covers finite unions. *)
      tycon.UnionCasesArray |> Array.iter (fun uc ->
          accAttribs cenv env uc.Attribs
          uc.RecdFieldsArray |> Array.iter (accTyconRecdField cenv env tycon))

let accTycons cenv env tycons = 
    List.iter (accTycon cenv env) tycons

let rec accModuleOrNamespaceExpr cenv env x = 
    match x with  
    | ModuleOrNamespaceExprWithSig(_mty, def, _m) -> accModuleOrNamespaceDef cenv env def
    
and accModuleOrNamespaceDefs cenv env x = 
    List.iter (accModuleOrNamespaceDef cenv env) x

and accModuleOrNamespaceDef cenv env x = 
    match x with 
    | TMDefRec(_, tycons, mbinds, _m) -> 
        accTycons cenv env tycons
        accModuleOrNamespaceBinds cenv env mbinds 
    | TMDefLet(bind, _m)  -> accBind cenv env bind 
    | TMDefDo(e, _m)  -> accExpr cenv env e
    | TMAbstract(def)  -> accModuleOrNamespaceExpr cenv env def
    | TMDefs(defs) -> accModuleOrNamespaceDefs cenv env defs 

and accModuleOrNamespaceBinds cenv env xs = 
    List.iter (accModuleOrNamespaceBind cenv env) xs

and accModuleOrNamespaceBind cenv env x = 
    match x with 
    | ModuleOrNamespaceBinding.Binding bind -> 
        accBind cenv env bind
    | ModuleOrNamespaceBinding.Module(mspec, rhs) -> 
        accTycon cenv env mspec
        accModuleOrNamespaceDef cenv env rhs 

let UnsolvedTyparsOfModuleDef g amap denv (mdef, extraAttribs) =
   let cenv = 
      { g =g  
        amap=amap 
        denv=denv 
        unsolved = [] }
   accModuleOrNamespaceDef cenv Nix mdef
   accAttribs cenv Nix extraAttribs
   List.rev cenv.unsolved


