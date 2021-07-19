// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.LowerStateMachines

open Internal.Utilities.Collections
open Internal.Utilities.Library
open Internal.Utilities.Library.Extras
open FSharp.Compiler.AbstractIL
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Syntax
open FSharp.Compiler.Syntax.PrettyNaming
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TypedTreeOps

let mkLabelled m l e = mkCompGenSequential m (Expr.Op (TOp.Label l, [], [], m)) e

type StateMachineConversionFirstPhaseResult =
   {
     /// Represents the expanded expression prior to decisions about labels
     phase1: Expr

     /// The second phase of the transformation.  It is run after all code labels and their mapping to program counters have been determined
     /// after the first phase. 
     phase2: (Map<int, ILCodeLabel> -> Expr)

     /// The labels allocated for this portion of the computation
     entryPoints: int list

     /// The state variables allocated for one portion of the sequence expression (i.e. the local let-bound variables which become state variables)
     stateVars: ValRef list

     /// All this values get represented via the 'this' pointer
     thisVars: ValRef list

     /// The vars captured by the non-synchronous resumable path
     resumableVars: FreeVars
   }

#if DEBUG
let sm_verbose = try System.Environment.GetEnvironmentVariable("FSharp_StateMachineVerbose") <> null with _ -> false
#else
let sm_verbose = false
#endif

let rec (|OptionalResumeAtExpr|) g expr =
    match expr with
    | IfUseResumableStateMachinesExpr g (OptionalResumeAtExpr g res, _) -> res
    | Expr.Sequential(ResumeAtExpr g pcExpr, codeExpr, NormalSeq, _, _m) -> (Some pcExpr, codeExpr)
    | _ -> (None, expr)

/// Implement a decision to represent a 'let' binding as a non-escaping local variable (rather than a state machine variable)
let RepresentBindingAsTopLevelOrLocal (bind: Binding) (res2: StateMachineConversionFirstPhaseResult) m =
    if sm_verbose then 
        printfn "LowerStateMachine: found local variable %s" bind.Var.DisplayName

    { res2 with
        phase1 = mkLetBind m bind res2.phase1 
        phase2 = (fun ctxt -> mkLetBind m bind (res2.phase2 ctxt)) }

/// Implement a decision to represent a 'let' binding as the 'this' pointer of the state machine,
/// because it is rebinding the 'this' variable
let RepresentBindingAsThis (bind: Binding) (res2: StateMachineConversionFirstPhaseResult) _m =
    if sm_verbose then 
        printfn "LowerStateMachine: found local variable %s" bind.Var.DisplayName

    { res2 with
        thisVars = mkLocalValRef bind.Var :: res2.thisVars
        // Drop the let binding on the floor as it is only rebinding the 'this' variable
        phase1 = res2.phase1 
        phase2 = res2.phase2 }

/// Implement a decision to represent a 'let' binding as a state machine variable
let RepresentBindingAsStateVar g (bind: Binding) (resBody: StateMachineConversionFirstPhaseResult) m =
    if sm_verbose then 
        printfn "LowerStateMachine: found state variable %s" bind.Var.DisplayName
    
    let (TBind(v, e, sp)) = bind
    let sp, spm =
        match sp with
        | DebugPointAtBinding.Yes m -> DebugPointAtSequential.SuppressNeither, m
        | _ -> DebugPointAtSequential.SuppressStmt, e.Range
    let vref = mkLocalValRef v
    { resBody with
        phase1 = mkSequential sp m (mkValSet spm vref e) resBody.phase1
        phase2 = (fun ctxt ->
            let generateBody = resBody.phase2 ctxt
            let generate =
                mkSequential sp m
                    (mkValSet spm vref e)
                    // Within all resumable code, a return value of 'true' indicates success/completion path, when we can clear
                    // state machine locals.
                    (if typeEquiv g (tyOfExpr g generateBody) g.bool_ty then
                        mkCond DebugPointAtBinding.NoneAtInvisible DebugPointForTarget.No m g.bool_ty generateBody
                            (mkCompGenSequential m 
                                (mkValSet m vref (mkDefault (m, vref.Type)))
                                (mkTrue g m))
                            (mkFalse g m)
                     else
                        generateBody)
            generate )
        stateVars = vref :: resBody.stateVars }

let isExpandVar g (v: Val) = 
    isReturnsResumableCodeTy g v.TauType &&
    not v.IsCompiledAsTopLevel

// We allow a prefix of bindings prior to the state machine, e.g. 
//     task { .. }
// becomes
//     let builder@ = task
//     ....
let isStateMachineBindingVar g (v: Val) = 
    isExpandVar g v  ||
    (let nm = v.LogicalName
     (nm.StartsWith "builder@" 
      || (v.BaseOrThisInfo = MemberThisVal)) &&
     not v.IsCompiledAsTopLevel)

type env = 
    { 
      ResumableCodeDefns: ValMap<Expr>
      TemplateStructTy: TType option
      //MachineAddrExpr: Expr option 
    }

    static member Empty = 
        { ResumableCodeDefns = ValMap.Empty
          TemplateStructTy = None
          //MachineAddrExpr = None 
        }

/// Detect prefix of expanded, optimized state machine expressions 
/// This is run on every expression during codegen 
let rec IsStateMachineExpr g overallExpr = 
    match overallExpr with
    // 'let' binding of initial code
    | Expr.Let (defn, bodyExpr, m, _) when isStateMachineBindingVar g defn.Var -> 
        match IsStateMachineExpr g bodyExpr with
        | None -> None
        | Some altExpr as r ->
            match altExpr with 
            | None -> r
            | Some e -> Some (Some (mkLetBind m defn e))
    // Recognise 'if __useResumableCode ...'
    | IfUseResumableStateMachinesExpr g (thenExpr, elseExpr) -> 
        match IsStateMachineExpr g thenExpr with
        | None -> None
        | Some _ -> Some (Some elseExpr)
    | StructStateMachineExpr g _ -> Some None
    | _ -> None

type LoweredStateMachine =
    LoweredStateMachine of 
         templateStructTy: TType *
         dataTy: TType *
         stateVars: ValRef list *
         thisVars: ValRef list *
         moveNext: (Val * Expr) * 
         setStateMachine: (Val * Val * Expr) *
         afterCode: (Val * Expr)

type LoweredStateMachineResult =
    /// A state machine was recognised and was compilable
    | Lowered of LoweredStateMachine

    /// A state machine was recognised and was not compilable and an alternative is available
    | UseAlternative of message: string * alternativeExpr: Expr

    /// A state machine was recognised and was not compilable and no alternative is available
    | NoAlternative of message: string

    /// The construct was not a state machine
    | NotAStateMachine

/// Used to scope the action of lowering a state machine expression
type LowerStateMachine(g: TcGlobals) =

    let mutable pcCount = 0
    let genPC() =
        pcCount <- pcCount + 1
        pcCount

    // Record definitions for any resumable code
    let rec BindResumableCodeDefinitions (env: env) expr = 

        match expr with
        // Bind 'let __expand_ABC = bindExpr in bodyExpr'
        | Expr.Let (defn, bodyExpr, _, _) when isStateMachineBindingVar g defn.Var -> 
            if sm_verbose then printfn "binding %A --> %A..." defn.Var defn.Expr
            let envR = { env with ResumableCodeDefns = env.ResumableCodeDefns.Add defn.Var defn.Expr }
            BindResumableCodeDefinitions envR bodyExpr

         // Eliminate 'if __useResumableCode ...'
         | IfUseResumableStateMachinesExpr g (thenExpr, _) ->
            if sm_verbose then printfn "eliminating 'if __useResumableCode...'"
            BindResumableCodeDefinitions env thenExpr

         | _ ->
            (env, expr)

    let rec TryReduceApp (env: env) expr (args: Expr list) = 
        if isNil args then  None else
        match expr with 
        | Expr.TyLambda _ 
        | Expr.Lambda _ -> 
            let macroTypars, macroParamsCurried, macroBody, _rty = stripTopLambda (expr, tyOfExpr g expr)
            let m = macroBody.Range
            if not (isNil macroTypars) then 
                //warning(Error(FSComp.SR.stateMachineMacroTypars(), m))
                None
            else
                let macroParams = List.concat macroParamsCurried
                let macroVal2 = mkLambdas m macroTypars macroParams (macroBody, tyOfExpr g macroBody)
                if args.Length < macroParams.Length then 
                    //warning(Error(FSComp.SR.stateMachineMacroUnderapplied(), m))
                    None
                else
                    let nowArgs, laterArgs = List.splitAt macroParams.Length args
                    let expandedExpr = MakeApplicationAndBetaReduce g (macroVal2, (tyOfExpr g macroVal2), [], nowArgs, m)
                    if sm_verbose then printfn "reduced application f = %A nowArgs= %A --> %A" macroVal2 nowArgs expandedExpr
                    if isNil laterArgs then 
                        Some expandedExpr 
                    else
                        if sm_verbose then printfn "application was partial, reducing further args %A" laterArgs
                        TryReduceApp env expandedExpr laterArgs

        | NewDelegateExpr g (_, macroParamsCurried, macroBody, _, _) -> 
            let m = expr.Range
            let macroParams = List.concat macroParamsCurried
            let macroVal2 = mkLambdas m [] macroParams (macroBody, tyOfExpr g macroBody)
            if args.Length < macroParams.Length then 
                //warning(Error(FSComp.SR.stateMachineMacroUnderapplied(), m))
                None
            else
                let nowArgs, laterArgs = List.splitAt macroParams.Length args
                let expandedExpr = MakeApplicationAndBetaReduce g (macroVal2, (tyOfExpr g macroVal2), [], nowArgs, m)
                if sm_verbose then printfn "reduced application f = %A nowArgs= %A --> %A" macroVal2 nowArgs expandedExpr
                if isNil laterArgs then 
                    Some expandedExpr 
                else
                    if sm_verbose then printfn "application was partial, reducing further args %A" laterArgs
                    TryReduceApp env expandedExpr laterArgs

        | Expr.Let (bind, bodyExpr, m, _) -> 
            match TryReduceApp env bodyExpr args with 
            | Some bodyExpr2 -> Some (mkLetBind m bind bodyExpr2)
            | None -> None

        | Expr.LetRec (binds, bodyExpr, m, _) ->
            match TryReduceApp env bodyExpr args with 
            | Some bodyExpr2 -> Some (mkLetRecBinds m binds bodyExpr2)
            | None -> None

        | Expr.Sequential (x1, bodyExpr, sp, ty, m) -> 
            match TryReduceApp env bodyExpr args with 
            | Some bodyExpr2 -> Some (Expr.Sequential (x1, bodyExpr2, sp, ty, m))
            | None -> None

        // This construct arises from the 'mkDefault' in the 'Throw' case of an incomplete pattern match
        | Expr.Const (Const.Zero, m, ty) -> 
            Some (Expr.Const (Const.Zero, m, ty))

        | Expr.Match (spBind, exprm, dtree, targets, m, ty) ->
            let mutable newTyOpt = None
            let targets2 = 
                targets |> Array.choose (fun (TTarget(vs, targetExpr, spTarget, flags)) -> 
                    // Incomplete exception matching expressions give rise to targets with I_throw. 
                    // and System.Runtime.ExceptionServices.ExceptionDispatchInfo::Throw(...)
                    // 
                    // Keep these in the residue.
                    //
                    // In theory the type of the expression should be adjusted but code generation doesn't record the 
                    // type in the IL
                    let targetExpr2Opt = 
                        match targetExpr, newTyOpt with 
                        | Expr.Op (TOp.ILAsm ([ AbstractIL.IL.I_throw ], [_oldTy]), a, b, c), Some newTy -> 
                            let targetExpr2 = Expr.Op (TOp.ILAsm ([ AbstractIL.IL.I_throw ], [newTy]), a, b, c) 
                            Some targetExpr2
                        | Expr.Sequential ((Expr.Op (TOp.ILCall ( _, _, _, _, _, _, _, ilMethodRef, _, _, _), _, _, _) as e1), Expr.Const (Const.Zero, m, _oldTy), a, b, c), Some newTy  when ilMethodRef.Name = "Throw" -> 
                            let targetExpr2 = Expr.Sequential (e1, Expr.Const (Const.Zero, m, newTy), a, b, c)
                            Some targetExpr2
                        | _ ->

                        match TryReduceApp env targetExpr args with 
                        | Some targetExpr2 -> 
                            newTyOpt <- Some (tyOfExpr g targetExpr2) 
                            Some targetExpr2
                        | None -> 
                            None
                    match targetExpr2Opt with 
                    | Some targetExpr2 -> Some (TTarget(vs, targetExpr2, spTarget, flags))
                    | None -> None)
            if targets2.Length = targets.Length then 
                Some (Expr.Match (spBind, exprm, dtree, targets2, m, ty))
            else
                None

        | WhileExpr (sp1, sp2, guardExpr, bodyExpr, m) ->
            match TryReduceApp env bodyExpr args with
            | Some bodyExpr2 -> Some (mkWhile g (sp1, sp2, guardExpr, bodyExpr2, m))
            | None -> None

        | TryFinallyExpr (sp1, sp2, ty, bodyExpr, compensation, m) ->
            match TryReduceApp env bodyExpr args with
            | Some bodyExpr2 -> Some (mkTryFinally g (bodyExpr2, compensation, m, ty, sp1, sp2))
            | None -> None

        | TryWithExpr (spTry, spWith, resTy, bodyExpr, filterVar, filterExpr, handlerVar, handlerExpr, m) ->
            match TryReduceApp env bodyExpr args with
            | Some bodyExpr2 -> Some (mkTryWith g (bodyExpr2, filterVar, filterExpr, handlerVar, handlerExpr, m, resTy, spTry, spWith))
            | None -> None

        | _ -> 
            None

    // Apply a single expansion of resumable code at the outermost position in an arbitrary expression
    let rec TryReduceExpr (env: env) expr args remake = 
        if sm_verbose then printfn "expanding defns and reducing %A..." expr
        //if sm_verbose then printfn "checking %A for possible resumable code application..." expr
        match expr with
        // defn --> [expand_code]
        | Expr.Val (defnRef, _, _) when env.ResumableCodeDefns.ContainsVal defnRef.Deref ->
            let defn = env.ResumableCodeDefns.[defnRef.Deref]
            if sm_verbose then printfn "found resumable code %A --> %A" defnRef defn 
            // Expand the resumable code definition
            match TryReduceApp env defn args with 
            | Some expandedExpr -> 
                if sm_verbose then printfn "expanded resumable code %A --> %A..." defnRef expandedExpr
                Some expandedExpr
            | None -> 
                Some (remake defn)

        // defn.Invoke x --> let arg = x in [defn][arg/x]
        | ResumableCodeInvoke g (_, f, args2, _, rebuild) ->
            if sm_verbose then printfn "found delegate invoke in possible reduction, f = %A, args now %A..."  f (args2 @ args)
            TryReduceExpr env f (args2 @ args) (fun f2 -> remake (rebuild (f2, args2)))

        // defn x --> let arg = x in [defn][arg/x] 
        | Expr.App (f, _fty, _tyargs, args2, _m) ->
            if sm_verbose then printfn "found function invoke in possible reduction, f = %A, args now %A..."  f (args2 @ args)
            TryReduceExpr env f (args2 @ args) (fun f2 -> remake (Expr.App (f2, _fty, _tyargs, args2, _m)))

        | _ -> 
            //let (env, expr) = BindResumableCodeDefinitions env expr
            match TryReduceApp env expr args with 
            | Some expandedExpr -> 
                if sm_verbose then printfn "reduction = %A, args = %A --> %A..." expr args expandedExpr
                Some expandedExpr
            | None -> 
                None

    // Repeated top-down rewrite
    let makeRewriteEnv (env: env) = 
        { PreIntercept = Some (fun cont e -> match TryReduceExpr env e [] id with Some e2 -> Some (cont e2) | None -> None)
          PostTransform = (fun _ -> None)
          PreInterceptBinding = None
          IsUnderQuotations=true } 

    let ConvertStateMachineLeafExpression (env: env) expr = 
        if sm_verbose then printfn "ConvertStateMachineLeafExpression for %A..." expr
        expr |> RewriteExpr (makeRewriteEnv env)

    let ConvertStateMachineLeafDecisionTree (env: env) expr = 
        if sm_verbose then printfn "ConvertStateMachineLeafDecisionTree for %A..." expr
        expr |> RewriteDecisionTree (makeRewriteEnv env)

    /// Repeatedly find outermost expansion definitions and apply outermost expansions 
    let rec RepeatBindAndApplyOuterDefinitions (env: env) expr = 
        if sm_verbose then printfn "RepeatBindAndApplyOuterDefinitions for %A..." expr
        let env2, expr2 = BindResumableCodeDefinitions env expr
        match TryReduceExpr env2 expr2 [] id with 
        | Some res -> RepeatBindAndApplyOuterDefinitions env2 res
        | None -> env2, expr2

    // Detect a state machine with a single method override
    let (|ExpandedStateMachineInContext|_|) inputExpr = 
        // All expanded resumable code state machines e.g. 'task { .. }' begin with a bind of @builder or 'defn'
        let env, expr = BindResumableCodeDefinitions env.Empty inputExpr 
        match expr with
        | StructStateMachineExpr g 
               (dataTy, 
                (moveNextThisVar, moveNextBody), 
                (setStateMachineThisVar, setStateMachineStateVar, setStateMachineBody), 
                (afterCodeThisVar, afterCodeBody)) ->
            let templateStructTy = g.mk_ResumableStateMachine_ty dataTy
            let env = { env with TemplateStructTy = Some templateStructTy }
            if sm_verbose then printfn "Found struct machine..."
            if sm_verbose then printfn "Found struct machine jump table call..."
            let setStateMachineBodyR = ConvertStateMachineLeafExpression env setStateMachineBody
            let afterCodeBodyR = ConvertStateMachineLeafExpression env afterCodeBody
            let remake2 (moveNextExprR, stateVars, thisVars) = 
                if sm_verbose then 
                    printfn "----------- AFTER REWRITE moveNextExprWithJumpTable ----------------------"
                    printfn "%s" (DebugPrint.showExpr g moveNextExprR)
                    printfn "----------- AFTER REWRITE setStateMachineBodyR ----------------------"
                    printfn "%s" (DebugPrint.showExpr g setStateMachineBodyR)
                    printfn "----------- AFTER REWRITE afterCodeBodyR ----------------------"
                    printfn "%s" (DebugPrint.showExpr g afterCodeBodyR)
                LoweredStateMachine 
                    (templateStructTy, dataTy, stateVars, thisVars, 
                        (moveNextThisVar, moveNextExprR), 
                        (setStateMachineThisVar, setStateMachineStateVar, setStateMachineBodyR), 
                        (afterCodeThisVar, afterCodeBodyR))
            Some (env, remake2, moveNextBody)
        | _ -> 
            None

    // A utility to add a jump table an expression
    let addPcJumpTable m (pcs: int list)  (pc2lab: Map<int, ILCodeLabel>) pcExpr expr =
        if pcs.IsEmpty then 
            expr
        else
            let initLabel = IL.generateCodeLabel()
            let mbuilder = new MatchBuilder(DebugPointAtBinding.NoneAtInvisible, m )
            let mkGotoLabelTarget lab = mbuilder.AddResultTarget(Expr.Op (TOp.Goto lab, [], [], m), DebugPointForTarget.No)
            let dtree =
                TDSwitch(pcExpr,
                        [   // Yield one target for each PC, where the action of the target is to goto the appropriate label
                            for pc in pcs do
                                yield mkCase(DecisionTreeTest.Const(Const.Int32 pc), mkGotoLabelTarget pc2lab.[pc]) ],
                        // The default is to go to pcInit
                        Some(mkGotoLabelTarget initLabel),
                        m)

            let table = mbuilder.Close(dtree, m, g.int_ty)
            mkCompGenSequential m table (mkLabelled m initLabel expr)

    /// Detect constructs allowed in state machines
    let rec ConvertResumableCode env (pcValInfo: ((Val * Expr) * Expr) option) expr : Result<StateMachineConversionFirstPhaseResult, string> = 
        if sm_verbose then 
            printfn "---------ConvertResumableCode-------------------"
            printfn "%s" (DebugPrint.showExpr g expr)
            printfn "---------"
        
        let env, expr = RepeatBindAndApplyOuterDefinitions env expr
        
        if sm_verbose then 
            printfn "After RepeatBindAndApplyOuterDefinitions:\n%s" (DebugPrint.showExpr g expr)
            printfn "---------"

        // Detect the different permitted constructs in the expanded state machine
        let res = 
            match expr with 
            | ResumableCodeInvoke g (_, _, _, m, _) ->
                Result.Error (FSComp.SR.reprResumableCodeInvokeNotReduced(m.ToShortString()))

            // Eliminate 'if __useResumableCode ...' within.  
            | IfUseResumableStateMachinesExpr g (thenExpr, _) -> 
                ConvertResumableCode env pcValInfo thenExpr

            | ResumableEntryMatchExpr g (noneBranchExpr, someVar, someBranchExpr, _rebuild) ->
                ConvertResumableEntry env pcValInfo (noneBranchExpr, someVar, someBranchExpr, _rebuild)

            | ResumeAtExpr g pcExpr ->
                ConvertResumableResumeAt env (pcExpr, expr.Range)

            // The expanded code for state machines may use sequential binding and sequential execution.
            //
            // let __stack_step = e1 in e2
            // e1; e2
            //
            // A binding 'let .. = ... in ... ' is considered part of the state machine logic 
            // if it uses a binding variable starting with '__stack_*'.
            // If this case 'e1' becomes part of the state machine too.
            | SequentialResumableCode g (e1, e2, _m, recreate) ->
                ConvertResumableSequential env pcValInfo (e1, e2, _m, recreate)

            // The expanded code for state machines may use while loops...
            | WhileExpr (sp1, sp2, guardExpr, bodyExpr, m) ->
                ConvertResumableWhile env pcValInfo (sp1, sp2, guardExpr, bodyExpr, m)

            // The expanded code for state machines should not normally contain try/finally as any resumptions will repeatedly execute the finally.
            // However we include the synchronous version of the construct here for completeness.
            | TryFinallyExpr (sp1, sp2, ty, e1, e2, m) ->
                ConvertResumableTryFinally env pcValInfo (sp1, sp2, ty, e1, e2, m)

            // The expanded code for state machines may use for loops, however the
            // body must be synchronous.
            | ForLoopExpr (sp1, sp2, e1, e2, v, e3, m) ->
                ConvertResumableFastIntegerForLoop env pcValInfo (sp1, sp2, e1, e2, v, e3, m)

            // The expanded code for state machines may use try/with....
            | TryWithExpr (spTry, spWith, resTy, bodyExpr, filterVar, filterExpr, handlerVar, handlerExpr, m) ->
                ConvertResumableTryWith env pcValInfo (spTry, spWith, resTy, bodyExpr, filterVar, filterExpr, handlerVar, handlerExpr, m)

            // control-flow match
            | Expr.Match (spBind, exprm, dtree, targets, m, ty) ->
                ConvertResumableMatch env pcValInfo (spBind, exprm, dtree, targets, m, ty)

            // Non-control-flow let binding can appear as part of state machine. The body is considered state-machine code,
            // the expression being bound is not.
            | Expr.Let (bind, bodyExpr, m, _)
                  // Restriction: compilation of sequence expressions containing non-toplevel constrained generic functions is not supported
                  when  bind.Var.IsCompiledAsTopLevel || not (IsGenericValWithGenericConstraints g bind.Var) ->
                ConvertResumableLet env pcValInfo (bind, bodyExpr, m)

            | Expr.LetRec _ ->
                  Result.Error (FSComp.SR.reprResumableCodeContainsLetRec())

            // Arbitrary expression
            | _ -> 
                let exprR = ConvertStateMachineLeafExpression env expr
                { phase1 = exprR
                  phase2 = (fun _ctxt -> exprR)
                  entryPoints = []
                  stateVars = []
                  thisVars = []
                  resumableVars = emptyFreeVars }
                |> Result.Ok

        if sm_verbose then 
            match res with 
            | Result.Ok res -> 
                printfn "-------------------"
                printfn "Phase 1 Done for %s" (DebugPrint.showExpr g res.phase1)
                printfn "Phase 1 Done, resumableVars = %A" (res.resumableVars.FreeLocals |> Zset.elements |> List.map (fun v -> v.CompiledName(g.CompilerGlobalState)) |> String.concat ",")
                printfn "Phase 1 Done, stateVars = %A" (res.stateVars |> List.map (fun v -> v.CompiledName(g.CompilerGlobalState)) |> String.concat ",")
                printfn "Phase 1 Done, thisVars = %A" (res.thisVars |> List.map (fun v -> v.CompiledName(g.CompilerGlobalState)) |> String.concat ",")
                printfn "-------------------"
            | Result.Error msg-> 
                printfn "Phase 1 failed: %s" msg
                printfn "Phase 1 failed for %s" (DebugPrint.showExpr g expr)
        res

    and ConvertResumableEntry env pcValInfo (noneBranchExpr, someVar, someBranchExpr, _rebuild) =
        if sm_verbose then printfn "ResumableEntryMatchExpr" 
        // printfn "found sequential"
        let reenterPC = genPC()
        let envSome = { env with ResumableCodeDefns = env.ResumableCodeDefns.Add someVar (mkInt g someVar.Range reenterPC) }
        let resNone = ConvertResumableCode env pcValInfo noneBranchExpr
        let resSome = ConvertResumableCode envSome pcValInfo someBranchExpr

        match resNone, resSome with 
        | Result.Ok resNone, Result.Ok resSome ->
            let resumableVars = unionFreeVars (freeInExpr CollectLocals resNone.phase1) resSome.resumableVars 
            let m = someBranchExpr.Range
            let recreate reenterLabOpt e1 e2 = 
                let lab = (match reenterLabOpt with Some l -> l | _ -> IL.generateCodeLabel())
                mkCond DebugPointAtBinding.NoneAtSticky DebugPointForTarget.No  m (tyOfExpr g noneBranchExpr) (mkFalse g m) (mkLabelled m lab e1) e2
            { phase1 = recreate None resNone.phase1 resSome.phase1
              phase2 = (fun ctxt ->
                let generate2 = resSome.phase2 ctxt
                let generate1 = resNone.phase2 ctxt
                let generate = recreate (Some ctxt.[reenterPC]) generate1 generate2
                generate)
              entryPoints= resSome.entryPoints @ [reenterPC] @ resNone.entryPoints
              stateVars = resSome.stateVars @ resNone.stateVars 
              thisVars = resSome.thisVars @ resNone.thisVars
              resumableVars = resumableVars }
            |> Result.Ok
        | Result.Error err, _ | _, Result.Error err -> Result.Error err

    and ConvertResumableResumeAt env (pcExpr , m)=
        if sm_verbose then printfn "ResumeAtExpr" 
        // Macro-evaluate the pcExpr
        let pcExprVal = ConvertStateMachineLeafExpression env pcExpr
        match pcExprVal with
        | Int32Expr contIdPC ->
            let recreate contLabOpt =
                Expr.Op (TOp.Goto (match contLabOpt with Some l -> l | _ -> IL.generateCodeLabel()), [], [], m)

            { phase1 = recreate None 
              phase2 = (fun ctxt ->
                let generate = recreate (Some ctxt.[contIdPC]) 
                generate)
              entryPoints = []
              stateVars = []
              thisVars = []
              resumableVars = emptyFreeVars }
            |> Result.Ok
        | _ -> 
            Result.Error(FSComp.SR.reprResumableCodeContainsDynamicResumeAtInBody())

    and ConvertResumableSequential env pcValInfo (e1, e2, _m, recreate) =
        if sm_verbose then printfn "SequentialResumableCode" 
        // printfn "found sequential"
        let res1 = ConvertResumableCode env pcValInfo e1
        let res2 = ConvertResumableCode env pcValInfo e2
        match res1, res2 with 
        | Result.Ok res1, Result.Ok res2 ->
            let resumableVars =
                if res1.entryPoints.IsEmpty then
                    // res1 is synchronous
                    res2.resumableVars
                else
                    // res1 is not synchronous. All of 'e2' is needed after resuming at any of the labels
                    unionFreeVars res1.resumableVars (freeInExpr CollectLocals res2.phase1)

            { phase1 = recreate res1.phase1 res2.phase1
              phase2 = (fun ctxt ->
                let generate1 = res1.phase2 ctxt
                let generate2 = res2.phase2 ctxt
                let generate = recreate generate1 generate2
                generate)
              entryPoints= res1.entryPoints @ res2.entryPoints
              stateVars = res1.stateVars @ res2.stateVars
              thisVars = res1.thisVars @ res2.thisVars
              resumableVars = resumableVars }
            |> Result.Ok
        | Result.Error err, _ | _, Result.Error err -> Result.Error err

    and ConvertResumableWhile env pcValInfo (sp1, sp2, guardExpr, bodyExpr, m) =
        if sm_verbose then printfn "WhileExpr" 

        let resg = ConvertResumableCode env pcValInfo guardExpr
        let resb = ConvertResumableCode env pcValInfo bodyExpr
        match resg, resb with 
        | Result.Ok resg, Result.Ok resb ->
            let eps = resg.entryPoints @ resb.entryPoints
            // All free variables get captured if there are any entrypoints at all
            let resumableVars = if eps.IsEmpty then emptyFreeVars else unionFreeVars (freeInExpr CollectLocals resg.phase1) (freeInExpr CollectLocals resb.phase1)
            { phase1 = mkWhile g (sp1, sp2, resg.phase1, resb.phase1, m)
              phase2 = (fun ctxt -> 
                    let egR = resg.phase2 ctxt
                    let ebR = resb.phase2 ctxt
                        
                    // Clear the pcVal on backward branch, causing jump tables at entry to nested try-blocks to not activate
                    let ebR2 = 
                        match pcValInfo with
                        | None -> ebR
                        | Some ((pcVal, _), _) -> 
                            mkCompGenThenDoSequential m 
                                ebR 
                                (mkValSet m (mkLocalValRef pcVal) (mkZero g m))

                    mkWhile g (sp1, sp2, egR, ebR2, m))
              entryPoints= eps
              stateVars = resg.stateVars @ resb.stateVars 
              thisVars = resg.thisVars @ resb.thisVars
              resumableVars = resumableVars }
            |> Result.Ok
        | Result.Error err, _ | _, Result.Error err -> Result.Error err

    and ConvertResumableTryFinally env pcValInfo (sp1, sp2, ty, e1, e2, m) =
        if sm_verbose then printfn "TryFinallyExpr" 
        let res1 = ConvertResumableCode env pcValInfo e1
        let res2 = ConvertResumableCode env pcValInfo e2
        match res1, res2 with 
        | Result.Ok res1, Result.Ok res2 ->
            let eps = res1.entryPoints @ res2.entryPoints
            if eps.Length > 0 then 
                Result.Error (FSComp.SR.reprResumableCodeContainsResumptionInTryFinally())
            else
                { phase1 = mkTryFinally g (res1.phase1, res2.phase1, m, ty, sp1, sp2)
                  phase2 = (fun ctxt -> 
                        let egR = res1.phase2 ctxt
                        let ebR = res2.phase2 ctxt
                        mkTryFinally g (egR, ebR, m, ty, sp1, sp2))
                  entryPoints= eps
                  stateVars = res1.stateVars @ res2.stateVars 
                  thisVars = res1.thisVars @ res2.thisVars
                  resumableVars = emptyFreeVars (* eps is empty, hence synchronous, no capture *)  }
                |> Result.Ok
        | Result.Error err, _ | _, Result.Error err -> Result.Error err

    and ConvertResumableFastIntegerForLoop env pcValInfo (sp1, sp2, e1, e2, v, e3, m) =
        if sm_verbose then printfn "ForLoopExpr" 
        let res1 = ConvertResumableCode env pcValInfo e1
        let res2 = ConvertResumableCode env pcValInfo e2
        let res3 = ConvertResumableCode env pcValInfo e3
        match res1, res2, res3 with 
        | Result.Ok res1, Result.Ok res2, Result.Ok res3 ->
            let eps = res1.entryPoints @ res2.entryPoints @ res3.entryPoints
            if eps.Length > 0 then 
                Result.Error(FSComp.SR.reprResumableCodeContainsFastIntegerForLoop())
            else
                { phase1 = mkFor g (sp1, v, res1.phase1, sp2, res2.phase1, res3.phase1, m)
                  phase2 = (fun ctxt -> 
                        let e1R = res1.phase2 ctxt
                        let e2R = res2.phase2 ctxt
                        let e3R = res3.phase2 ctxt

                        // Clear the pcVal on backward branch, causing jump tables at entry to nested try-blocks to not activate
                        let e3R2 = 
                            match pcValInfo with
                            | None -> e3R
                            | Some ((pcVal, _), _) -> 
                                mkCompGenThenDoSequential m 
                                    e3R 
                                    (mkValSet m (mkLocalValRef pcVal) (mkZero g m))

                        mkFor g (sp1, v, e1R, sp2, e2R, e3R2, m))
                  entryPoints= eps
                  stateVars = res1.stateVars @ res2.stateVars @ res3.stateVars
                  thisVars = res1.thisVars @ res2.thisVars @ res3.thisVars
                  resumableVars = emptyFreeVars (* eps is empty, hence synchronous, no capture *) }
                |> Result.Ok
        | Result.Error err, _, _ | _, Result.Error err, _ | _, _, Result.Error err -> Result.Error err

    and ConvertResumableTryWith env pcValInfo (spTry, spWith, resTy, bodyExpr, filterVar, filterExpr, handlerVar, handlerExpr, m) =
        if sm_verbose then printfn "TryWithExpr" 
        let resBody = ConvertResumableCode env pcValInfo bodyExpr
        let resFilter = ConvertResumableCode env pcValInfo filterExpr
        let resHandler = ConvertResumableCode env pcValInfo handlerExpr
        match resBody, resFilter, resHandler with 
        | Result.Ok resBody, Result.Ok resFilter, Result.Ok resHandler ->
            let epsNope = resFilter.entryPoints @ resHandler.entryPoints 
            if epsNope.Length > 0 then 
                Result.Error(FSComp.SR.reprResumableCodeContainsResumptionInHandlerOrFilter())
            else
            { phase1 = mkTryWith g (resBody.phase1, filterVar, resFilter.phase1, handlerVar, resHandler.phase1, m, resTy, spTry, spWith)
              phase2 = (fun ctxt -> 
                // We can't jump into a try/catch block.  So we jump to the start of the try/catch and add a new jump table
                let pcsAndLabs = ctxt |> Map.toList  
                let innerPcs = resBody.entryPoints 
                if innerPcs.IsEmpty then 
                    let vBodyR = resBody.phase2 ctxt
                    let filterExprR = resFilter.phase2 ctxt
                    let handlerExprR = resHandler.phase2 ctxt
                    mkTryWith g (vBodyR, filterVar, filterExprR, handlerVar, handlerExprR, m, resTy, spTry, spWith)
                else
                    let innerPcSet = innerPcs |> Set.ofList
                    let outerLabsForInnerPcs = pcsAndLabs |> List.filter (fun (pc, _outerLab) -> innerPcSet.Contains pc) |> List.map snd
                    // generate the inner labels
                    let pcsAndInnerLabs = pcsAndLabs |> List.map (fun (pc, l) -> (pc, if innerPcSet.Contains pc then IL.generateCodeLabel() else l))
                    let innerPc2Lab = Map.ofList pcsAndInnerLabs

                    let vBodyR = resBody.phase2 innerPc2Lab
                    let filterExprR = resFilter.phase2 ctxt
                    let handlerExprR = resHandler.phase2 ctxt

                    // Add a jump table at the entry to the try
                    let vBodyRWithJumpTable = 
                        match pcValInfo with 
                        | None -> vBodyR
                        | Some ((_, pcValExpr), _) -> addPcJumpTable m innerPcs innerPc2Lab pcValExpr vBodyR
                    let coreExpr = mkTryWith g (vBodyRWithJumpTable, filterVar, filterExprR, handlerVar, handlerExprR, m, resTy, spTry, spWith)
                    // Place all the outer labels just before the try
                    let labelledExpr = (coreExpr, outerLabsForInnerPcs) ||> List.fold (fun e l -> mkLabelled m l e)                

                    labelledExpr)
              entryPoints= resBody.entryPoints @ resFilter.entryPoints @ resHandler.entryPoints 
              stateVars = resBody.stateVars @ resFilter.stateVars @ resHandler.stateVars
              thisVars = resBody.thisVars @ resFilter.thisVars @ resHandler.thisVars
              resumableVars = unionFreeVars resBody.resumableVars (unionFreeVars(freeInExpr CollectLocals resFilter.phase1) (freeInExpr CollectLocals resHandler.phase1)) }
            |> Result.Ok
        | Result.Error err, _, _ | _, Result.Error err, _ | _, _, Result.Error err -> Result.Error err

    and ConvertResumableMatch env pcValInfo (spBind, exprm, dtree, targets, m, ty) =
        if sm_verbose then printfn "MatchExpr" 
        // lower all the targets. 
        let dtreeR = ConvertStateMachineLeafDecisionTree env dtree 
        let tglArray = 
            targets |> Array.map (fun (TTarget(_vs, targetExpr, _spTarget, _)) -> 
                ConvertResumableCode env pcValInfo targetExpr)
        match (tglArray |> Array.forall (function Result.Ok _ -> true | Result.Error _ -> false)) with
        | true ->
            let tglArray = tglArray |> Array.map (function Result.Ok v -> v | _ -> failwith "unreachable")
            let tgl = tglArray |> Array.toList 
            let entryPoints = tgl |> List.collect (fun res -> res.entryPoints)
            let resumableVars =
                (emptyFreeVars, Array.zip targets tglArray)
                ||> Array.fold (fun fvs ((TTarget(_vs, _, _spTarget, _)), res) ->
                    if res.entryPoints.IsEmpty then fvs else unionFreeVars fvs res.resumableVars)
            let stateVars = 
                (targets, tglArray) ||> Array.zip |> Array.toList |> List.collect (fun (TTarget(vs, _, _, _), res) -> 
                    let stateVars = vs |> List.filter (fun v -> res.resumableVars.FreeLocals.Contains(v)) |> List.map mkLocalValRef 
                    stateVars @ res.stateVars)
            let thisVars = tglArray |> Array.toList |> List.collect (fun res -> res.thisVars) 
            { phase1 = 
                let gtgs =
                    (targets, tglArray) ||> Array.map2 (fun (TTarget(vs, _, spTarget, _)) res -> 
                        let flags = vs |> List.map (fun v -> res.resumableVars.FreeLocals.Contains(v)) 
                        TTarget(vs, res.phase1, spTarget, Some flags))
                primMkMatch (spBind, exprm, dtreeR, gtgs, m, ty)

              phase2 = (fun ctxt ->
                            let gtgs =
                                (targets, tglArray) ||> Array.map2 (fun (TTarget(vs, _, spTarget, _)) res ->
                                    let flags = vs |> List.map (fun v -> res.resumableVars.FreeLocals.Contains(v)) 
                                    TTarget(vs, res.phase2 ctxt, spTarget, Some flags))
                            let generate = primMkMatch (spBind, exprm, dtreeR, gtgs, m, ty)
                            generate)

              entryPoints = entryPoints
              stateVars = stateVars
              resumableVars = resumableVars 
              thisVars = thisVars }
            |> Result.Ok
        | _ -> tglArray |> Array.find (function Result.Ok _ -> false | Result.Error _ -> true) 

    and ConvertResumableLet env pcValInfo (bind, bodyExpr, m) =
        // Non-control-flow let binding can appear as part of state machine. The body is considered state-machine code,
        // the expression being bound is not.
        if sm_verbose then printfn "LetExpr (non-control-flow, rewrite rhs)" 

        // Rewrite the expression on the r.h.s. of the binding
        let bindExpr = ConvertStateMachineLeafExpression env bind.Expr
        let bind = mkBind bind.DebugPoint bind.Var bindExpr
        if sm_verbose then printfn "LetExpr (non-control-flow, body)" 

        let resBody = ConvertResumableCode env pcValInfo bodyExpr

        match resBody with
        | Result.Ok resBody ->
            // The isByrefTy check is an adhoc check to avoid capturing the 'this' parameter of a struct state machine 
            // You might think we could do this:
            //
            //    let sm = &this
            //    ... await point ...
            //    ... sm ....
            // However the 'sm' won't be set on that path.
            if isByrefTy g bind.Var.Type && 
                (match env.TemplateStructTy with 
                | None -> false
                | Some ty -> typeEquiv g ty (destByrefTy g bind.Var.Type)) then
                RepresentBindingAsThis bind resBody m
                |> Result.Ok
            elif bind.Var.IsCompiledAsTopLevel || 
                not (resBody.resumableVars.FreeLocals.Contains(bind.Var)) || 
                bind.Var.LogicalName.StartsWith stackVarPrefix then
                if sm_verbose then printfn "LetExpr (non-control-flow, rewrite rhs, RepresentBindingAsTopLevelOrLocal)" 
                RepresentBindingAsTopLevelOrLocal bind resBody m
                |> Result.Ok
            else
                if sm_verbose then printfn "LetExpr (non-control-flow, rewrite rhs, RepresentBindingAsStateVar)" 
                // printfn "found state variable %s" bind.Var.DisplayName
                RepresentBindingAsStateVar g bind resBody m
                |> Result.Ok
        | Result.Error msg -> 
            Result.Error msg

    member _.Apply(overallExpr, altExprOpt) =

        let fallback msg =
            match altExprOpt with 
            | None -> 
                LoweredStateMachineResult.NoAlternative msg
            | Some altExpr -> 
                LoweredStateMachineResult.UseAlternative (msg, altExpr)

        match overallExpr with
        | ExpandedStateMachineInContext (env, remake, moveNextExpr) ->
            let m = moveNextExpr.Range
            match moveNextExpr with 
            | OptionalResumeAtExpr g (pcExprOpt, codeExpr) ->
            let env, codeExprR = RepeatBindAndApplyOuterDefinitions env codeExpr
            let frees = (freeInExpr CollectLocals overallExpr).FreeLocals

            if frees |> Zset.exists (isExpandVar g) then 
                let nonfree = frees |> Zset.elements |> List.filter (isExpandVar g) |> List.map (fun v -> v.DisplayName) |> String.concat ","
                let msg = FSComp.SR.reprResumableCodeValueHasNoDefinition(nonfree)
                fallback msg
            else
                let pcExprROpt = pcExprOpt |> Option.map (ConvertStateMachineLeafExpression env)
                let pcValInfo = 
                    match pcExprROpt with 
                    | None -> None
                    | Some e -> Some (mkMutableCompGenLocal e.Range "pcVal" g.int32_ty, e)
        
                if sm_verbose then 
                    printfn "Found state machine override method and code expression..."
                    printfn "----------- OVERALL EXPRESSION FOR STATE MACHINE CONVERSION ----------------------"
                    printfn "%s" (DebugPrint.showExpr g overallExpr)
                    printfn "----------- INPUT TO STATE MACHINE CONVERSION ----------------------"
                    printfn "%s" (DebugPrint.showExpr g codeExpr)
                    printfn "----------- START STATE MACHINE CONVERSION ----------------------"
    
                // Perform phase1 of the conversion
                let phase1 = ConvertResumableCode env pcValInfo codeExprR 
                match phase1 with 
                | Result.Error msg -> 
                    fallback msg
                | Result.Ok phase1 ->

                // Work out the initial mapping of pcs to labels
                let pcs = [ 1 .. pcCount ]
                let labs = pcs |> List.map (fun _ -> IL.generateCodeLabel())
                let pc2lab  = Map.ofList (List.zip pcs labs)

                // Execute phase2, building the core of the method
                if sm_verbose then printfn "----------- PHASE2 ----------------------"

                // Perform phase2 to build the final expression
                let moveNextExprR = phase1.phase2 pc2lab

                if sm_verbose then printfn "----------- ADDING JUMP TABLE ----------------------"

                // Add the jump table
                let moveNextExprWithJumpTable = 
                    match pcValInfo with 
                    | None -> moveNextExprR
                    | Some ((v,pcValExprR),pcExprR) -> mkCompGenLet m v pcExprR (addPcJumpTable m pcs pc2lab pcValExprR moveNextExprR)
        
                if sm_verbose then printfn "----------- REMAKE ----------------------"

                // Build the result
                let res = remake (moveNextExprWithJumpTable, phase1.stateVars, phase1.thisVars)
                LoweredStateMachineResult.Lowered res

        | _ -> 
            let msg = FSComp.SR.reprStateMachineInvalidForm()
            fallback msg

let LowerStateMachineExpr g (overallExpr: Expr) : LoweredStateMachineResult =
    // Detect a state machine and convert it
    let stateMachine = IsStateMachineExpr g overallExpr

    match stateMachine with 
    | None -> LoweredStateMachineResult.NotAStateMachine
    | Some altExprOpt ->

    LowerStateMachine(g).Apply(overallExpr, altExprOpt)
