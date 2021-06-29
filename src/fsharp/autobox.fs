// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.AutoBox 

open Internal.Utilities.Collections
open Internal.Utilities.Library.Extras
open FSharp.Compiler 
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.TypeRelations

//----------------------------------------------------------------------------
// Decide the set of mutable locals to promote to heap-allocated reference cells

type cenv = 
    { g: TcGlobals
      amap: Import.ImportMap }

    override x.ToString() = "<cenv>"

/// Find all the mutable locals that escape a method, function or lambda expression
let DecideEscapes syntacticArgs body =
    let isMutableEscape v = 
        let passedIn = ListSet.contains valEq v syntacticArgs 
        not passedIn &&
        v.IsMutable &&
        v.ValReprInfo.IsNone &&
        not (Optimizer.IsKnownOnlyMutableBeforeUse (mkLocalValRef v))

    let frees = freeInExpr CollectLocals body
    frees.FreeLocals |> Zset.filter isMutableEscape 

/// Find all the mutable locals that escape a lambda expression, ignoring the arguments to the lambda
let DecideLambda exprF cenv topValInfo expr ety z   = 
    match expr with 
    | Expr.Lambda _
    | Expr.TyLambda _ ->
        let _tps, ctorThisValOpt, baseValOpt, vsl, body, _bodyty = destTopLambda cenv.g cenv.amap topValInfo (expr, ety) 
        let snoc = fun x y -> y :: x
        let args = List.concat vsl
        let args = Option.fold snoc args baseValOpt
        let syntacticArgs = Option.fold snoc args  ctorThisValOpt
        
        let z = Zset.union z (DecideEscapes syntacticArgs body)
        let z = match exprF with Some f -> f z body | None -> z
        z
    | _ -> z

///Special cases where representation uses Lambda. 
/// Handle these as special cases since mutables are allowed inside their bodies 
let DecideExprOp exprF noInterceptF (z: Zset<Val>) (expr: Expr) (op, tyargs, args) =

    match op, tyargs, args with 
    | TOp.While _, _, [Expr.Lambda (_, _, _, [_], e1, _, _);Expr.Lambda (_, _, _, [_], e2, _, _)]  ->
        exprF (exprF z e1) e2

    | TOp.TryFinally _, [_], [Expr.Lambda (_, _, _, [_], e1, _, _); Expr.Lambda (_, _, _, [_], e2, _, _)] ->
        exprF (exprF z e1) e2

    | TOp.For (_), _, [Expr.Lambda (_, _, _, [_], e1, _, _);Expr.Lambda (_, _, _, [_], e2, _, _);Expr.Lambda (_, _, _, [_], e3, _, _)] ->
        exprF (exprF (exprF z e1) e2) e3

    | TOp.TryWith _, [_], [Expr.Lambda (_, _, _, [_], e1, _, _); Expr.Lambda (_, _, _, [_], _e2, _, _); Expr.Lambda (_, _, _, [_], e3, _, _)] ->
        exprF (exprF (exprF z e1) _e2) e3
        // In Check code it said
        //     e2; -- don't check filter body - duplicates logic in 'catch' body 
        //   Is that true for this code too?      
    | _ -> 
        noInterceptF z expr

/// Find all the mutable locals that escape a lambda expression or object expression 
let DecideExpr cenv exprF noInterceptF z expr  = 
    match expr with 
    | Expr.Lambda (_, _ctorThisValOpt, _baseValOpt, argvs, _, m, rty) -> 
        let topValInfo = ValReprInfo ([], [argvs |> List.map (fun _ -> ValReprInfo.unnamedTopArg1)], ValReprInfo.unnamedRetVal) 
        let ty = mkMultiLambdaTy m argvs rty 
        DecideLambda (Some exprF)  cenv topValInfo expr ty z

    | Expr.TyLambda (_, tps, _, _m, rty)  -> 
        let topValInfo = ValReprInfo (ValReprInfo.InferTyparInfo tps, [], ValReprInfo.unnamedRetVal) 
        let ty = mkForallTyIfNeeded tps rty 
        DecideLambda (Some exprF)  cenv topValInfo expr ty z

    | Expr.Obj (_, _, baseValOpt, superInitCall, overrides, iimpls, _m) -> 
        let CheckMethod z (TObjExprMethod(_, _attribs, _tps, vs, body, _m)) = 
            let vs = List.concat vs
            let syntacticArgs = (match baseValOpt with Some x -> x :: vs | None -> vs)
            let z = Zset.union z (DecideEscapes syntacticArgs body)
            exprF z body

        let CheckMethods z l = (z, l) ||> List.fold CheckMethod 
    
        let CheckInterfaceImpl z (_ty, overrides) = CheckMethods z overrides 

        let z = exprF z superInitCall
        let z = CheckMethods z overrides 
        let z =  (z, iimpls) ||> List.fold CheckInterfaceImpl 
        z

    | Expr.Op (c, tyargs, args, _m) ->
        DecideExprOp exprF noInterceptF z expr (c, tyargs, args) 

    | _ -> 
        noInterceptF z expr

/// Find all the mutable locals that escape a binding
let DecideBinding cenv z (TBind(v, expr, _m) as bind) = 
    let topValInfo  = match bind.Var.ValReprInfo with Some info -> info | _ -> ValReprInfo.emptyValData 
    DecideLambda None cenv topValInfo expr v.Type z 

/// Find all the mutable locals that escape a set of bindings
let DecideBindings cenv z binds = (z, binds) ||> List.fold (DecideBinding cenv)

/// Find all the mutable locals to promote to reference cells in an implementation file
let DecideImplFile g amap implFile =
          
    let cenv = { g = g; amap = amap }

    let folder =    
      {ExprFolder0 with
         nonRecBindingsIntercept  = DecideBinding cenv 
         recBindingsIntercept     = DecideBindings cenv
         exprIntercept = DecideExpr cenv
      }

    let z = FoldImplFile folder emptyFreeLocals implFile

    z

//----------------------------------------------------------------------------
// Apply the transform

/// Rewrite fetches, stores and address-of expressions for mutable locals which we are transforming
let TransformExpr g (nvs: ValMap<_>) exprF expr = 

    match expr with
    // Rewrite uses of mutable values 
    | Expr.Val (ValDeref(v), _, m) when nvs.ContainsVal v -> 

       let _nv, nve = nvs.[v]
       Some (mkRefCellGet g m v.Type nve)

    // Rewrite assignments to mutable values 
    | Expr.Op (TOp.LValueOp (LSet, ValDeref(v)), [], [arg], m) when nvs.ContainsVal v -> 

       let _nv, nve = nvs.[v]
       let arg = exprF arg 
       Some (mkRefCellSet g m v.Type nve arg)

    // Rewrite taking the address of mutable values 
    | Expr.Op (TOp.LValueOp (LAddrOf readonly, ValDeref(v)), [], [], m) when nvs.ContainsVal v -> 
       let _nv,nve = nvs.[v]
       Some (mkRecdFieldGetAddrViaExprAddr (readonly, nve, mkRefCellContentsRef g, [v.Type], m))

    | _ -> None

/// Rewrite bindings for mutable locals which we are transforming
let TransformBinding g (nvs: ValMap<_>) exprF (TBind(v, expr, m)) = 
    if nvs.ContainsVal v then 
       let nv, _nve = nvs.[v]
       let exprRange = expr.Range
       let expr = exprF expr
       Some(TBind(nv, mkRefCell g exprRange v.Type expr, m))
    else
       None

/// Rewrite mutable locals to reference cells across an entire implementation file
let TransformImplFile g amap implFile = 
    let fvs = DecideImplFile g amap implFile
    if Zset.isEmpty fvs then 
        implFile
    else
        for fv in fvs do
            warning (Error(FSComp.SR.abImplicitHeapAllocation(fv.DisplayName), fv.Range))

        let nvs = 
            [ for fv in fvs do
                let nty = mkRefCellTy g fv.Type
                let nv, nve = 
                    if fv.IsCompilerGenerated then mkCompGenLocal fv.Range fv.LogicalName nty
                    else mkLocal fv.Range fv.LogicalName nty
                yield (fv, (nv, nve)) ]
            |> ValMap.OfList

        implFile |> 
          RewriteImplFile 
              { PreIntercept = Some(TransformExpr g nvs)
                PreInterceptBinding = Some(TransformBinding g nvs)
                PostTransform = (fun _ -> None)
                IsUnderQuotations = false } 


