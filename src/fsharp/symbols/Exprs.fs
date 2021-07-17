// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.Symbols

open FSharp.Compiler
open Internal.Utilities.Library
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Infos
open FSharp.Compiler.QuotationTranslator
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.TypeRelations

[<AutoOpen>]
module ExprTranslationImpl = 

    let nonNil x = not (List.isEmpty x)

    type ExprTranslationEnv = 
        { 
          /// Map from Val to binding index
          vs: ValMap<unit>

          /// Map from typar stamps to binding index
          tyvs: StampMap<FSharpGenericParameter>

          // Map for values bound by the 
          //     'let v = isinst e in .... if nonnull v then ...v .... ' 
          // construct arising out the compilation of pattern matching. We decode these back to the form
          //     'if istype v then ...unbox v .... ' 
          isinstVals: ValMap<TType * Expr> 

          substVals: ValMap<Expr>

          /// Indicates that we disable generation of witnesses
          suppressWitnesses: bool

          /// All witnesses in scope and their mapping to lambda variables.
          //
          // Note: this uses an immutable HashMap/Dictionary with an IEqualityComparer that captures TcGlobals, see
          // the point where the empty initial object is created.
          witnessesInScope: TraitWitnessInfoHashMap<int>

        }

        static member Empty g = 
            { vs=ValMap<_>.Empty
              tyvs = Map.empty
              isinstVals = ValMap<_>.Empty 
              substVals = ValMap<_>.Empty
              suppressWitnesses = false
              witnessesInScope = EmptyTraitWitnessInfoHashMap g
            }

        member env.BindTypar (v: Typar, gp) = 
            { env with tyvs = env.tyvs.Add(v.Stamp, gp ) }

        member env.BindTypars vs = 
            (env, vs) ||> List.fold (fun env v -> env.BindTypar v) // fold left-to-right because indexes are left-to-right 

        member env.BindVal v = 
            { env with vs = env.vs.Add v () }

        member env.BindIsInstVal v (ty, e) = 
            { env with isinstVals =  env.isinstVals.Add v (ty, e) }

        member env.BindSubstVal v e = 
            { env with substVals = env.substVals.Add v e  }

        member env.BindVals vs = 
            (env, vs) ||> List.fold (fun env v -> env.BindVal v) 

        member env.BindCurriedVals vsl = 
            (env, vsl) ||> List.fold (fun env vs -> env.BindVals vs) 

    exception IgnoringPartOfQuotedTermWarning of string * range

    let wfail (msg, m: range) = failwith (msg + sprintf " at %s" (m.ToString()))

/// The core tree of data produced by converting F# compiler TAST expressions into the form which we make available through the compiler API
/// through active patterns.
type E =
    | Value  of FSharpMemberOrFunctionOrValue
    | ThisValue  of FSharpType 
    | BaseValue  of FSharpType 
    | Application of FSharpExpr * FSharpType list * FSharpExpr list  
    | Lambda of FSharpMemberOrFunctionOrValue * FSharpExpr  
    | TypeLambda of FSharpGenericParameter list * FSharpExpr  
    | Quote  of FSharpExpr  
    | IfThenElse   of FSharpExpr * FSharpExpr * FSharpExpr  
    | DecisionTree   of FSharpExpr * (FSharpMemberOrFunctionOrValue list * FSharpExpr) list
    | DecisionTreeSuccess of int * FSharpExpr list
    | Call of FSharpExpr option * FSharpMemberOrFunctionOrValue * FSharpType list * FSharpType list * FSharpExpr list * FSharpExpr list 
    | NewObject of FSharpMemberOrFunctionOrValue * FSharpType list * FSharpExpr list 
    | LetRec of ( FSharpMemberOrFunctionOrValue * FSharpExpr) list * FSharpExpr  
    | Let of (FSharpMemberOrFunctionOrValue * FSharpExpr) * FSharpExpr 
    | NewRecord of FSharpType * FSharpExpr list 
    | ObjectExpr of FSharpType * FSharpExpr * FSharpObjectExprOverride list * (FSharpType * FSharpObjectExprOverride list) list
    | FSharpFieldGet of  FSharpExpr option * FSharpType * FSharpField 
    | FSharpFieldSet of  FSharpExpr option * FSharpType * FSharpField * FSharpExpr 
    | NewUnionCase of FSharpType * FSharpUnionCase * FSharpExpr list  
    | NewAnonRecord of FSharpType * FSharpExpr list
    | AnonRecordGet of FSharpExpr * FSharpType * int 
    | UnionCaseGet of FSharpExpr * FSharpType * FSharpUnionCase * FSharpField 
    | UnionCaseSet of FSharpExpr * FSharpType * FSharpUnionCase * FSharpField  * FSharpExpr
    | UnionCaseTag of FSharpExpr * FSharpType 
    | UnionCaseTest of FSharpExpr  * FSharpType * FSharpUnionCase 
    | TraitCall of FSharpType list * string * SynMemberFlags * FSharpType list * FSharpType list * FSharpExpr list
    | NewTuple of FSharpType * FSharpExpr list  
    | TupleGet of FSharpType * int * FSharpExpr 
    | Coerce of FSharpType * FSharpExpr  
    | NewArray of FSharpType * FSharpExpr list  
    | TypeTest of FSharpType * FSharpExpr  
    | AddressSet of FSharpExpr * FSharpExpr  
    | ValueSet of FSharpMemberOrFunctionOrValue * FSharpExpr  
    | Unused
    | DefaultValue of FSharpType  
    | Const of obj * FSharpType
    | AddressOf of FSharpExpr 
    | Sequential of FSharpExpr * FSharpExpr  
    | FastIntegerForLoop of FSharpExpr * FSharpExpr * FSharpExpr * bool
    | WhileLoop of FSharpExpr * FSharpExpr  
    | TryFinally of FSharpExpr * FSharpExpr  
    | TryWith of FSharpExpr * FSharpMemberOrFunctionOrValue * FSharpExpr * FSharpMemberOrFunctionOrValue * FSharpExpr  
    | NewDelegate of FSharpType * FSharpExpr  
    | ILFieldGet of FSharpExpr option * FSharpType * string 
    | ILFieldSet of FSharpExpr option * FSharpType * string  * FSharpExpr 
    | ILAsm of string * FSharpType list * FSharpExpr list
    | WitnessArg of int

/// Used to represent the information at an object expression member 
and [<Sealed>]  FSharpObjectExprOverride(sgn: FSharpAbstractSignature, gps: FSharpGenericParameter list, args: FSharpMemberOrFunctionOrValue list list, body: FSharpExpr) = 
    member _.Signature = sgn
    member _.GenericParameters = gps
    member _.CurriedParameterGroups = args
    member _.Body = body

/// The type of expressions provided through the compiler API.
and [<Sealed>] FSharpExpr (cenv, f: (unit -> FSharpExpr) option, e: E, m: range, ty) =

    let mutable e = match f with None -> e | Some _ -> Unchecked.defaultof<E>
    member x.Range = m
    member x.Type = FSharpType(cenv, ty)
    member x.cenv = cenv
    member x.E = match box e with null -> (e <- f.Value().E); e | _ -> e
    override x.ToString() = sprintf "%+A" x.E

    member x.ImmediateSubExpressions = 
        match x.E with 
        | E.Value _v -> []
        | E.Const (_constValue, _ty) -> []
        | E.TypeLambda (_v, body) -> [body]
        | E.Lambda (_v, body) -> [body]
        | E.Application (f, _tyargs, arg) -> f :: arg
        | E.IfThenElse (e1, e2, e3) -> [e1;e2;e3]
        | E.Let ((_bindingVar, bindingExpr), b) -> [bindingExpr;b]
        | E.LetRec (ves, b) -> (List.map snd ves) @ [b]
        | E.NewRecord (_recordType, es) -> es
        | E.NewAnonRecord (_recordType, es) -> es
        | E.AnonRecordGet (e, _recordType, _n) -> [e]
        | E.NewUnionCase (_unionType, _unionCase, es) -> es
        | E.NewTuple (_tupleType, es) -> es
        | E.TupleGet (_tupleType, _itemIndex, tupleExpr) -> [tupleExpr]
        | E.Call (objOpt, _b, _c, _d, ws, es) -> (match objOpt with None -> ws @ es | Some x -> x :: ws @ es)
        | E.NewObject (_a, _b, c) -> c
        | E.FSharpFieldGet (objOpt, _b, _c) -> (match objOpt with None -> [] | Some x -> [x])
        | E.FSharpFieldSet (objOpt, _b, _c, d) -> (match objOpt with None -> [d] | Some x -> [x;d])
        | E.UnionCaseGet (obj, _b, _c, _d) -> [obj]
        | E.UnionCaseTag (obj, _b) -> [obj]
        | E.UnionCaseTest (obj, _b, _c) -> [obj]
        | E.NewArray (_ty, elems) -> elems
        | E.Coerce (_ty, b) -> [b]
        | E.Quote a -> [a]
        | E.TypeTest (_ty, b) -> [b]
        | E.Sequential (a, b) -> [a;b]
        | E.FastIntegerForLoop (a, b, c, _dir) -> [a;b;c]
        | E.WhileLoop (guard, body) -> [guard; body]
        | E.TryFinally (body, b) -> [body; b]
        | E.TryWith (body, _b, _c, _d, handler) -> [body; handler]
        | E.NewDelegate (_ty, body) -> [body]
        | E.DefaultValue _ty -> []
        | E.AddressSet (lvalueExpr, rvalueExpr) -> [lvalueExpr; rvalueExpr]
        | E.ValueSet (_v, rvalueExpr) -> [rvalueExpr]
        | E.AddressOf lvalueExpr -> [lvalueExpr]
        | E.ThisValue _ty -> []
        | E.BaseValue _ty -> []
        | E.ILAsm (_code, _tyargs, argExprs) -> argExprs
        | E.ILFieldGet (objOpt, _ty, _fieldName) -> (match objOpt with None -> [] | Some x -> [x])
        | E.ILFieldSet (objOpt, _ty, _fieldName, d) -> (match objOpt with None -> [d] | Some x -> [x;d])
        | E.ObjectExpr (_ty, basecall, overrides, interfaceImpls) -> 
             [ yield basecall
               for m in overrides do yield m.Body
               for (_, ms) in interfaceImpls do for m in ms do yield m.Body ]
        | E.DecisionTree (inputExpr, targetCases) -> 
            [ yield inputExpr
              for (_targetVars, targetExpr) in targetCases do yield targetExpr ]
        | E.DecisionTreeSuccess (_targetNumber, targetArgs) -> targetArgs
        | E.UnionCaseSet (obj, _unionType, _unionCase, _unionField, valueExpr) -> [ yield obj; yield valueExpr ]
        | E.TraitCall (_sourceTypes, _traitName, _memberFlags, _paramTypes, _retTypes, args) -> args
        | E.Unused -> [] // unexpected
        | E.WitnessArg _n -> []

/// The implementation of the conversion operation
module FSharpExprConvert =

    let IsStaticInitializationField (rfref: RecdFieldRef)  = 
        rfref.RecdField.IsCompilerGenerated && 
        rfref.RecdField.IsStatic &&
        rfref.RecdField.IsMutable &&
        rfref.RecdField.Name.StartsWithOrdinal("init") 

        // Match "if [AI_clt](init@41, 6) then IntrinsicFunctions.FailStaticInit () else ()"
    let (|StaticInitializationCheck|_|) e = 
        match e with 
        | Expr.Match (_, _, TDSwitch(Expr.Op (TOp.ILAsm ([ AI_clt ], _), _, [Expr.Op (TOp.ValFieldGet rfref, _, _, _) ;_], _), _, _, _), _, _, _) when IsStaticInitializationField rfref -> Some ()
        | _ -> None

        // Match "init@41 <- 6"
    let (|StaticInitializationCount|_|) e = 
        match e with 
        | Expr.Op (TOp.ValFieldSet rfref, _, _, _)  when IsStaticInitializationField rfref -> Some ()
        | _ -> None

    let (|ILUnaryOp|_|) e = 
        match e with 
        | AI_neg -> Some mkCallUnaryNegOperator
        | AI_not -> Some mkCallUnaryNotOperator
        | _ -> None

    let (|ILMulDivOp|_|) e = 
        match e with 
        | AI_mul        -> Some (mkCallMultiplyOperator, true)
        | AI_mul_ovf
        | AI_mul_ovf_un -> Some (mkCallMultiplyChecked, true)
        | AI_div
        | AI_div_un     -> Some (mkCallDivisionOperator, false)
        | _ -> None

    let (|ILBinaryOp|_|) e = 
        match e with 
        | AI_add        -> Some mkCallAdditionOperator
        | AI_add_ovf
        | AI_add_ovf_un -> Some mkCallAdditionChecked
        | AI_sub        -> Some mkCallSubtractionOperator
        | AI_sub_ovf
        | AI_sub_ovf_un -> Some mkCallSubtractionChecked
        | AI_rem
        | AI_rem_un     -> Some mkCallModulusOperator
        | AI_ceq        -> Some mkCallEqualsOperator
        | AI_clt
        | AI_clt_un     -> Some mkCallLessThanOperator
        | AI_cgt
        | AI_cgt_un     -> Some mkCallGreaterThanOperator
        | AI_and        -> Some mkCallBitwiseAndOperator
        | AI_or         -> Some mkCallBitwiseOrOperator
        | AI_xor        -> Some mkCallBitwiseXorOperator
        | AI_shl        -> Some mkCallShiftLeftOperator
        | AI_shr
        | AI_shr_un     -> Some mkCallShiftRightOperator
        | _ -> None

    let (|ILConvertOp|_|) e = 
        match e with 
        | AI_conv basicTy ->
            match basicTy with
            | DT_R  -> Some mkCallToDoubleOperator
            | DT_I1 -> Some mkCallToSByteOperator
            | DT_U1 -> Some mkCallToByteOperator
            | DT_I2 -> Some mkCallToInt16Operator
            | DT_U2 -> Some mkCallToUInt16Operator
            | DT_I4 -> Some mkCallToInt32Operator
            | DT_U4 -> Some mkCallToUInt32Operator
            | DT_I8 -> Some mkCallToInt64Operator
            | DT_U8 -> Some mkCallToUInt64Operator
            | DT_R4 -> Some mkCallToSingleOperator
            | DT_R8 -> Some mkCallToDoubleOperator
            | DT_I  -> Some mkCallToIntPtrOperator
            | DT_U  -> Some mkCallToUIntPtrOperator
            | DT_REF -> None
        | AI_conv_ovf basicTy
        | AI_conv_ovf_un basicTy ->
            match basicTy with
            | DT_R  -> Some mkCallToDoubleOperator
            | DT_I1 -> Some mkCallToSByteChecked
            | DT_U1 -> Some mkCallToByteChecked
            | DT_I2 -> Some mkCallToInt16Checked
            | DT_U2 -> Some mkCallToUInt16Checked
            | DT_I4 -> Some mkCallToInt32Checked
            | DT_U4 -> Some mkCallToUInt32Checked
            | DT_I8 -> Some mkCallToInt64Checked
            | DT_U8 -> Some mkCallToUInt64Checked
            | DT_R4 -> Some mkCallToSingleOperator
            | DT_R8 -> Some mkCallToDoubleOperator
            | DT_I  -> Some mkCallToIntPtrChecked
            | DT_U  -> Some mkCallToUIntPtrChecked
            | DT_REF -> None
        | _ -> None

    let (|TTypeConvOp|_|) (cenv: SymbolEnv) ty = 
        let g = cenv.g
        match ty with
        | TType_app (tcref,_) ->
            match tcref with
            | _ when tyconRefEq g tcref g.sbyte_tcr      -> Some mkCallToSByteOperator
            | _ when tyconRefEq g tcref g.byte_tcr       -> Some mkCallToByteOperator
            | _ when tyconRefEq g tcref g.int16_tcr      -> Some mkCallToInt16Operator
            | _ when tyconRefEq g tcref g.uint16_tcr     -> Some mkCallToUInt16Operator
            | _ when tyconRefEq g tcref g.int_tcr        -> Some mkCallToIntOperator
            | _ when tyconRefEq g tcref g.int32_tcr      -> Some mkCallToInt32Operator
            | _ when tyconRefEq g tcref g.uint32_tcr     -> Some mkCallToUInt32Operator
            | _ when tyconRefEq g tcref g.int64_tcr      -> Some mkCallToInt64Operator
            | _ when tyconRefEq g tcref g.uint64_tcr     -> Some mkCallToUInt64Operator
            | _ when tyconRefEq g tcref g.float32_tcr    -> Some mkCallToSingleOperator
            | _ when tyconRefEq g tcref g.float_tcr      -> Some mkCallToDoubleOperator
            | _ when tyconRefEq g tcref g.nativeint_tcr  -> Some mkCallToIntPtrOperator
            | _ when tyconRefEq g tcref g.unativeint_tcr -> Some mkCallToUIntPtrOperator
            | _ -> None
        | _ -> None

    let ConvType cenv ty = FSharpType(cenv, ty)
    let ConvTypes cenv tys = List.map (ConvType cenv) tys
    let ConvILTypeRefApp (cenv: SymbolEnv) m tref tyargs = 
        let tcref = Import.ImportILTypeRef cenv.amap m tref
        ConvType cenv (mkAppTy tcref tyargs)

    let ConvUnionCaseRef cenv (ucref: UnionCaseRef) = FSharpUnionCase(cenv, ucref)
    let ConvRecdFieldRef cenv (rfref: RecdFieldRef) = FSharpField(cenv, rfref )

    let rec exprOfExprAddr (cenv: SymbolEnv) expr = 
        match expr with 
        | Expr.Op (op, tyargs, args, m) -> 
            match op, args, tyargs  with
            | TOp.LValueOp (LAddrOf _, vref), _, _ -> exprForValRef m vref
            | TOp.ValFieldGetAddr (rfref, _), [], _ -> mkStaticRecdFieldGet (rfref, tyargs, m)
            | TOp.ValFieldGetAddr (rfref, _), [arg], _ -> mkRecdFieldGetViaExprAddr (exprOfExprAddr cenv arg, rfref, tyargs, m)
            | TOp.UnionCaseFieldGetAddr (uref, n, _), [arg], _ -> mkUnionCaseFieldGetProvenViaExprAddr (exprOfExprAddr cenv arg, uref, tyargs, n, m)
            | TOp.ILAsm ([ I_ldflda fspec ], retTypes), [arg], _  -> mkAsmExpr ([ mkNormalLdfld fspec ], tyargs, [exprOfExprAddr cenv arg], retTypes, m)
            | TOp.ILAsm ([ I_ldsflda fspec ], retTypes), _, _  -> mkAsmExpr ([ mkNormalLdsfld fspec ], tyargs, args, retTypes, m)
            | TOp.ILAsm (([ I_ldelema(_ro, _isNativePtr, shape, _tyarg) ] ), _), (arr :: idxs), [elemty]  -> 
                match shape.Rank, idxs with 
                | 1, [idx1] -> mkCallArrayGet cenv.g m elemty arr idx1
                | 2, [idx1; idx2] -> mkCallArray2DGet cenv.g m elemty arr idx1 idx2
                | 3, [idx1; idx2; idx3] -> mkCallArray3DGet cenv.g m elemty arr idx1 idx2 idx3
                | 4, [idx1; idx2; idx3; idx4] -> mkCallArray4DGet cenv.g m elemty arr idx1 idx2 idx3 idx4
                | _ -> expr
            | _ -> expr
        | _ -> expr


    let Mk cenv m ty e = FSharpExpr(cenv, None, e, m, ty)

    let Mk2 cenv (orig: Expr) e = FSharpExpr(cenv, None, e, orig.Range, tyOfExpr cenv.g orig)

    let rec ConvLValueExpr (cenv: SymbolEnv) env expr = ConvExpr cenv env (exprOfExprAddr cenv expr)

    and ConvExpr cenv env expr = 
        Mk2 cenv expr (ConvExprPrim cenv env expr) 

    and ConvExprLinear cenv env expr contF = 
        ConvExprPrimLinear cenv env expr (fun exprR -> contF (Mk2 cenv expr exprR))

    // Tail recursive function to process the subset of expressions considered "linear"
    and ConvExprPrimLinear cenv env expr contF = 

        match expr with 
        // Large lists 
        | Expr.Op (TOp.UnionCase ucref, tyargs, [e1;e2], _) -> 
            let mkR = ConvUnionCaseRef cenv ucref 
            let typR = ConvType cenv (mkAppTy ucref.TyconRef tyargs)
            let e1R = ConvExpr cenv env e1
            // tail recursive 
            ConvExprLinear cenv env e2 (contF << (fun e2R -> E.NewUnionCase(typR, mkR, [e1R; e2R]) ))

        // Large sequences of let bindings
        | Expr.Let (bind, body, _, _) ->  
            match ConvLetBind cenv env bind with 
            | None, env -> ConvExprPrimLinear cenv env body contF
            | Some bindR, env -> 
                // tail recursive 
                ConvExprLinear cenv env body (contF << (fun bodyR -> E.Let(bindR, bodyR)))

        // Remove initialization checks
        // Remove static initialization counter updates
        // Remove static initialization counter checks
        //
        // Put in ConvExprPrimLinear because of the overlap with Expr.Sequential below
        //
        // TODO: allow clients to see static initialization checks if they want to
        | Expr.Sequential (ObjectInitializationCheck cenv.g, x1, NormalSeq, _, _) 
        | Expr.Sequential (StaticInitializationCount, x1, NormalSeq, _, _)              
        | Expr.Sequential (StaticInitializationCheck, x1, NormalSeq, _, _) ->
            ConvExprPrim cenv env x1 |> contF

        // Large sequences of sequential code
        | Expr.Sequential (e1, e2, NormalSeq, _, _)  -> 
            let e1R = ConvExpr cenv env e1
            // tail recursive 
            ConvExprLinear cenv env e2 (contF << (fun e2R -> E.Sequential(e1R, e2R)))

        | Expr.Sequential (x0, x1, ThenDoSeq, _, _) ->  E.Sequential(ConvExpr cenv env x0, ConvExpr cenv env x1) 

        | ModuleValueOrMemberUse cenv.g (vref, vFlags, _f, _fty, tyargs, curriedArgs) when (nonNil tyargs || nonNil curriedArgs) && vref.IsMemberOrModuleBinding ->
            ConvModuleValueOrMemberUseLinear cenv env (expr, vref, vFlags, tyargs, curriedArgs) contF

        | Expr.Match (_spBind, m, dtree, tgs, _, retTy) ->
            let dtreeR = ConvDecisionTree cenv env retTy dtree m
            // tailcall 
            ConvTargetsLinear cenv env (List.ofArray tgs) (contF << fun (targetsR: _ list) -> 
                let (|E|) (x: FSharpExpr) = x.E

                // If the match is really an "if-then-else" then return it as such.
                match dtreeR with 
                | E(E.IfThenElse(a, E(E.DecisionTreeSuccess(0, [])), E(E.DecisionTreeSuccess(1, [])))) -> E.IfThenElse(a, snd targetsR.[0], snd targetsR.[1])
                | _ -> E.DecisionTree(dtreeR, targetsR))

        | _ -> 
            ConvExprPrim cenv env expr |> contF

    /// A nasty function copied from creflect.fs. Made nastier by taking a continuation to process the 
    /// arguments to the call in a tail-recursive fashion.
    and ConvModuleValueOrMemberUseLinear (cenv: SymbolEnv) env (expr: Expr, vref, vFlags, tyargs, curriedArgs) contF =
        let m = expr.Range 

        let (numEnclTypeArgs, _, isNewObj, _valUseFlags, _isSelfInit, takesInstanceArg, _isPropGet, _isPropSet) = 
            GetMemberCallInfo cenv.g (vref, vFlags)

        let isMember, tps, curriedArgInfos = 

            match vref.MemberInfo with 
            | Some _ when not vref.IsExtensionMember -> 
                // This is an application of a member method
                // We only count one argument block for these.
                let tps, curriedArgInfos, _, _ = GetTypeOfMemberInFSharpForm cenv.g vref 
                true, tps, curriedArgInfos
            | _ -> 
                // This is an application of a module value or extension member
                let arities = arityOfVal vref.Deref 
                let tps, curriedArgInfos, _, _ = GetTopValTypeInFSharpForm cenv.g arities vref.Type m
                false, tps, curriedArgInfos

        // Compute the object arguments as they appear in a compiled call
        // Strip off the object argument, if any. The curriedArgInfos are already adjusted to compiled member form
        let objArgs, curriedArgs = 
            match takesInstanceArg, curriedArgs with 
            | false, curriedArgs -> [], curriedArgs
            | true, (objArg :: curriedArgs) -> [objArg], curriedArgs
            | true, [] -> failwith ("warning: unexpected missing object argument when generating quotation for call to F# object member "+vref.LogicalName)

        // Check to see if there aren't enough arguments or if there is a tuple-arity mismatch
        // If so, adjust and try again
        if curriedArgs.Length < curriedArgInfos.Length ||
            ((List.truncate curriedArgInfos.Length curriedArgs, curriedArgInfos) ||> List.exists2 (fun arg argInfo -> (argInfo.Length > (tryDestRefTupleExpr arg).Length))) then

            // Too few arguments or incorrect tupling? Convert to a lambda and beta-reduce the 
            // partially applied arguments to 'let' bindings 
            let topValInfo = 
                match vref.ValReprInfo with 
                | None -> failwith ("no arity information found for F# value "+vref.LogicalName)
                | Some a -> a 

            let expr, exprty = AdjustValForExpectedArity cenv.g m vref vFlags topValInfo 
            let splitCallExpr = MakeApplicationAndBetaReduce cenv.g (expr, exprty, [tyargs], curriedArgs, m)
            // tailcall
            ConvExprPrimLinear cenv env splitCallExpr contF

        else        
            let curriedArgs, laterArgs = List.splitAt curriedArgInfos.Length curriedArgs 

            // detuple the args
            let untupledCurriedArgs = 
                (curriedArgs, curriedArgInfos) ||> List.map2 (fun arg curriedArgInfo -> 
                    let numUntupledArgs = curriedArgInfo.Length 
                    (if numUntupledArgs = 0 then [] 
                        elif numUntupledArgs = 1 then [arg] 
                        else tryDestRefTupleExpr arg))

            let contf2 = 
                match laterArgs with 
                | [] -> contF 
                | _ -> (fun subCallR -> (subCallR, laterArgs) ||> List.fold (fun fR arg -> E.Application (Mk2 cenv arg fR, [], [ConvExpr cenv env arg])) |> contF)
                    
            if isMember then 
                let callArgs = (objArgs :: untupledCurriedArgs) |> List.concat
                let enclTyArgs, methTyArgs = List.splitAfter numEnclTypeArgs tyargs
                let witnessArgsR = GetWitnessArgs cenv env vref m tps tyargs
                // tailcall
                ConvObjectModelCallLinear cenv env (isNewObj, FSharpMemberOrFunctionOrValue(cenv, vref), enclTyArgs, methTyArgs, witnessArgsR, callArgs) contf2
            else
                let v = FSharpMemberOrFunctionOrValue(cenv, vref)
                let witnessArgsR = GetWitnessArgs cenv env vref m vref.Typars tyargs
                // tailcall
                ConvObjectModelCallLinear cenv env (false, v, [], tyargs, witnessArgsR, List.concat untupledCurriedArgs) contf2

    and GetWitnessArgs cenv (env: ExprTranslationEnv) (vref: ValRef) m tps tyargs : FSharpExpr list =
        let g = cenv.g
        if cenv.g.langVersion.SupportsFeature(Features.LanguageFeature.WitnessPassing) && not env.suppressWitnesses then 
            let witnessExprs = 
                match ConstraintSolver.CodegenWitnessesForTyparInst cenv.tcValF g cenv.amap m tps tyargs with
                // There is a case where optimized code makes expressions that do a shift-left on the 'char'
                // type.  There is no witness for this case.  This is due to the code
                //    let inline HashChar (x:char) = (# "or" (# "shl" x 16 : int #) x : int #)
                // in FSharp.Core. 
                | ErrorResult _  when vref.LogicalName =  "op_LeftShift" && tyargs.Length = 1 -> []
                | res -> CommitOperationResult res
            let env = { env with suppressWitnesses = true }
            witnessExprs |> List.map (fun arg -> 
                match arg with 
                | Choice1Of2 traitInfo -> 
                    ConvWitnessInfo cenv env m traitInfo
                | Choice2Of2 arg -> 
                    ConvExpr cenv env arg) 
        else
            []

    and ConvExprPrim (cenv: SymbolEnv) (env: ExprTranslationEnv) expr = 
        // Eliminate integer 'for' loops 
        let expr = DetectAndOptimizeForExpression cenv.g OptimizeIntRangesOnly expr

        // Eliminate subsumption coercions for functions. This must be done post-typechecking because we need
        // complete inference types.
        let expr = NormalizeAndAdjustPossibleSubsumptionExprs cenv.g expr

        // Remove TExpr_ref nodes
        let expr = stripExpr expr 

        match expr with 
        
        // Uses of possibly-polymorphic values which were not polymorphic in the end
        | Expr.App (InnerExprPat(Expr.Val _ as ve), _fty, [], [], _) -> 
            ConvExprPrim cenv env ve

        // These cases are the start of a "linear" sequence where we use tail recursion to allow use to 
        // deal with large expressions.
        | Expr.Op (TOp.UnionCase _, _, [_;_], _) // big lists
        | Expr.Let _   // big linear sequences of 'let'
        | Expr.Match _   // big linear sequences of 'match ... -> ....' 
        | Expr.Sequential _ ->
            ConvExprPrimLinear cenv env expr (fun e -> e)

        | ModuleValueOrMemberUse cenv.g (vref, vFlags, _f, _fty, tyargs, curriedArgs) when (* (nonNil tyargs || nonNil curriedArgs) && *) vref.IsMemberOrModuleBinding ->
            // Process applications of top-level values in a tail-recursive way
            ConvModuleValueOrMemberUseLinear cenv env (expr, vref, vFlags, tyargs, curriedArgs) (fun e -> e)

        | Expr.Val (vref, _vFlags, m) -> 
            ConvValRef cenv env m vref 

        // Simple applications 
        | Expr.App (f, _fty, tyargs, args, _m) -> 
            E.Application (ConvExpr cenv env f, ConvTypes cenv tyargs, ConvExprs cenv env args) 
    
        | Expr.Const (c, m, ty) -> 
            ConvConst cenv env m c ty

        | Expr.LetRec (binds, body, _, _) -> 
            let vs = valsOfBinds binds
            let vsR = vs |> List.map (ConvVal cenv)
            let env = env.BindVals vs
            let bodyR = ConvExpr cenv env body 
            let bindsR = List.zip vsR (binds |> List.map (fun b -> b.Expr |> ConvExpr cenv env))
            E.LetRec(bindsR, bodyR) 
  
        | Expr.Lambda (_, _, _, vs, b, _, _) -> 
            let v, b = MultiLambdaToTupledLambda cenv.g vs b 
            let vR = ConvVal cenv v 
            let bR  = ConvExpr cenv (env.BindVal v) b 
            E.Lambda(vR, bR) 

        | Expr.Quote (ast, _, _, _, _) -> 
            E.Quote(ConvExpr cenv env ast) 

        | Expr.TyLambda (_, tps, b, _, _) -> 
            let gps = [ for tp in tps -> FSharpGenericParameter(cenv, tp) ]
            let env = env.BindTypars (Seq.zip tps gps |> Seq.toList)
            E.TypeLambda(gps, ConvExpr cenv env b) 

        | Expr.Obj (_, ty, _, _, [TObjExprMethod(TSlotSig(_, ctyp, _, _, _, _), _, tps, [tmvs], e, _) as tmethod], _, m) when isDelegateTy cenv.g ty -> 
            let f = mkLambdas m tps tmvs (e, GetFSharpViewOfReturnType cenv.g (returnTyOfMethod cenv.g tmethod))
            let fR = ConvExpr cenv env f 
            let tyargR = ConvType cenv ctyp 
            E.NewDelegate(tyargR, fR) 

        | Expr.StaticOptimization (_, _, x, _) -> 
            ConvExprPrim cenv env x

        | Expr.TyChoose _  -> 
            ConvExprPrim cenv env (ChooseTyparSolutionsForFreeChoiceTypars cenv.g cenv.amap expr)

        | Expr.Obj (_lambdaId, ty, _basev, basecall, overrides, iimpls, _m)      -> 
            let basecallR = ConvExpr cenv env basecall
            let ConvertMethods methods = 
                [ for (TObjExprMethod(slotsig, _, tps, tmvs, body, _)) in methods -> 
                    let vslR = List.map (List.map (ConvVal cenv)) tmvs 
                    let sgn = FSharpAbstractSignature(cenv, slotsig)
                    let tpsR = [ for tp in tps -> FSharpGenericParameter(cenv, tp) ]
                    let env = env.BindTypars (Seq.zip tps tpsR |> Seq.toList)
                    let env = env.BindCurriedVals tmvs
                    let bodyR = ConvExpr cenv env body
                    FSharpObjectExprOverride(sgn, tpsR, vslR, bodyR) ]
            let overridesR = ConvertMethods overrides 
            let iimplsR = List.map (fun (ity, impls) -> ConvType cenv ity, ConvertMethods impls) iimpls

            E.ObjectExpr(ConvType cenv ty, basecallR, overridesR, iimplsR)

        | Expr.Op (op, tyargs, args, m) -> 
            match op, tyargs, args with 
            | TOp.UnionCase ucref, _, _ -> 
                let mkR = ConvUnionCaseRef cenv ucref 
                let typR = ConvType cenv (mkAppTy ucref.TyconRef tyargs)
                let argsR = ConvExprs cenv env args
                E.NewUnionCase(typR, mkR, argsR) 

            | TOp.AnonRecd anonInfo, _, _ -> 
                let typR = ConvType cenv (mkAnyAnonRecdTy cenv.g anonInfo tyargs)
                let argsR = ConvExprs cenv env args
                E.NewAnonRecord(typR, argsR) 

            | TOp.Tuple tupInfo, tyargs, _ -> 
                let tyR = ConvType cenv (mkAnyTupledTy cenv.g tupInfo tyargs)
                let argsR = ConvExprs cenv env args
                E.NewTuple(tyR, argsR) 

            | TOp.Recd (_, tcref), _, _  -> 
                let typR = ConvType cenv (mkAppTy tcref tyargs)
                let argsR = ConvExprs cenv env args
                E.NewRecord(typR, argsR) 

            | TOp.UnionCaseFieldGet (ucref, n), tyargs, [e1] -> 
                let mkR = ConvUnionCaseRef cenv ucref 
                let typR = ConvType cenv (mkAppTy ucref.TyconRef tyargs)
                let projR = FSharpField(cenv, ucref, n)
                E.UnionCaseGet(ConvExpr cenv env e1, typR, mkR, projR) 

            | TOp.AnonRecdGet (anonInfo, n), tyargs, [e1] -> 
                let typR = ConvType cenv (mkAnyAnonRecdTy cenv.g anonInfo tyargs)
                E.AnonRecordGet(ConvExpr cenv env e1, typR, n) 

            | TOp.UnionCaseFieldSet (ucref, n), tyargs, [e1;e2] -> 
                let mkR = ConvUnionCaseRef cenv ucref 
                let typR = ConvType cenv (mkAppTy ucref.TyconRef tyargs)
                let projR = FSharpField(cenv, ucref, n)
                E.UnionCaseSet(ConvExpr cenv env e1, typR, mkR, projR, ConvExpr cenv env e2) 

            | TOp.UnionCaseFieldGetAddr _, _tyargs, _ ->
                E.AddressOf(ConvLValueExpr cenv env expr) 

            | TOp.ValFieldGetAddr _, _tyargs, _ -> 
                E.AddressOf(ConvLValueExpr cenv env expr)

            | TOp.ValFieldGet rfref, tyargs, [] ->
                let projR = ConvRecdFieldRef cenv rfref 
                let typR = ConvType cenv (mkAppTy rfref.TyconRef tyargs)
                E.FSharpFieldGet(None, typR, projR) 

            | TOp.ValFieldGet rfref, tyargs, [obj] ->
                let objR = ConvLValueExpr cenv env obj
                let projR = ConvRecdFieldRef cenv rfref 
                let typR = ConvType cenv (mkAppTy rfref.TyconRef tyargs)
                E.FSharpFieldGet(Some objR, typR, projR) 

            | TOp.TupleFieldGet (tupInfo, n), tyargs, [e] -> 
                let tyR = ConvType cenv (mkAnyTupledTy cenv.g tupInfo tyargs)
                E.TupleGet(tyR, n, ConvExpr cenv env e) 

            | TOp.ILAsm ([ I_ldfld (_, _, fspec) ], _), enclTypeArgs, [obj] -> 
                let typR = ConvILTypeRefApp cenv m fspec.DeclaringTypeRef enclTypeArgs 
                let objR = ConvLValueExpr cenv env obj
                E.ILFieldGet(Some objR, typR, fspec.Name) 

            | TOp.ILAsm (( [ I_ldsfld (_, fspec) ] | [ I_ldsfld (_, fspec); AI_nop ]), _), enclTypeArgs, []  -> 
                let typR = ConvILTypeRefApp cenv m fspec.DeclaringTypeRef enclTypeArgs 
                E.ILFieldGet(None, typR, fspec.Name) 

            | TOp.ILAsm ([ I_stfld (_, _, fspec) ], _), enclTypeArgs, [obj;arg]  -> 
                let typR = ConvILTypeRefApp cenv m fspec.DeclaringTypeRef enclTypeArgs 
                let objR = ConvLValueExpr cenv env obj
                let argR = ConvExpr cenv env arg
                E.ILFieldSet(Some objR, typR, fspec.Name, argR) 

            | TOp.ILAsm ([ I_stsfld (_, fspec) ], _), enclTypeArgs, [arg]  -> 
                let typR = ConvILTypeRefApp cenv m fspec.DeclaringTypeRef enclTypeArgs 
                let argR = ConvExpr cenv env arg
                E.ILFieldSet(None, typR, fspec.Name, argR) 

            | TOp.ILAsm ([ ], [tty]), _, [arg] -> 
                match tty with
                | TTypeConvOp cenv convOp ->
                    let ty = tyOfExpr cenv.g arg
                    let op = convOp cenv.g m ty arg
                    ConvExprPrim cenv env op
                | _ ->
                    ConvExprPrim cenv env arg

            | TOp.ILAsm ([ I_box _ ], _), [ty], [arg] -> 
                let op = mkCallBox cenv.g m ty arg
                ConvExprPrim cenv env op

            | TOp.ILAsm ([ I_unbox_any _ ], _), [ty], [arg] -> 
                let op = mkCallUnbox cenv.g m ty arg
                ConvExprPrim cenv env op

            | TOp.ILAsm ([ I_isinst _ ], _), [ty], [arg] -> 
                let op = mkCallTypeTest cenv.g m ty arg
                ConvExprPrim cenv env op

            | TOp.ILAsm ([ I_call (Normalcall, mspec, None) ], _), _, [arg]
              when mspec.MethodRef.DeclaringTypeRef.Name = "System.String" && mspec.Name = "GetHashCode" ->
                let ty = tyOfExpr cenv.g arg
                let op = mkCallHash cenv.g m ty arg
                ConvExprPrim cenv env op

            | TOp.ILCall (_, _, _, _, _, _, _, ilMethRef, _, _, _), [],
              [Expr.Op (TOp.ILAsm ([ I_ldtoken (ILToken.ILType _) ], _), [ty], _, _)]
              when ilMethRef.DeclaringTypeRef.Name = "System.Type" && ilMethRef.Name = "GetTypeFromHandle" -> 
                let op = mkCallTypeOf cenv.g m ty
                ConvExprPrim cenv env op

            | TOp.ILAsm ([ EI_ilzero _ ], _), [ty], _ -> 
                E.DefaultValue (ConvType cenv ty)

            | TOp.ILAsm ([ AI_ldnull; AI_cgt_un ], _), _, [arg] -> 
                let elemTy = tyOfExpr cenv.g arg
                let nullVal = mkNull m elemTy
                let op = mkCallNotEqualsOperator cenv.g m elemTy arg nullVal
                let env = { env with suppressWitnesses=true }
                ConvExprPrim cenv env op

            | TOp.ILAsm ([ I_ldlen; AI_conv DT_I4 ], _), _, [arr] -> 
                let arrayTy = tyOfExpr cenv.g arr
                let elemTy = destArrayTy cenv.g arrayTy
                let op = mkCallArrayLength cenv.g m elemTy arr
                let env = { env with suppressWitnesses=true }
                ConvExprPrim cenv env op

            | TOp.ILAsm ([ I_newarr (ILArrayShape [(Some 0, None)], _)], _), [elemTy], xa ->
                E.NewArray(ConvType cenv elemTy, ConvExprs cenv env xa)

            | TOp.ILAsm ([ I_ldelem_any (ILArrayShape [(Some 0, None)], _)], _), [elemTy], [arr; idx1]  -> 
                let op = mkCallArrayGet cenv.g m elemTy arr idx1
                ConvExprPrim cenv env op

            | TOp.ILAsm ([ I_stelem_any (ILArrayShape [(Some 0, None)], _)], _), [elemTy], [arr; idx1; v]  -> 
                let op = mkCallArraySet cenv.g m elemTy arr idx1 v
                ConvExprPrim cenv env op

            | TOp.ILAsm ([ ILUnaryOp unaryOp ], _), _, [arg] -> 
                let ty = tyOfExpr cenv.g arg
                let op = unaryOp cenv.g m ty arg
                ConvExprPrim cenv env op

            | TOp.ILAsm ([ ILBinaryOp binaryOp ], _), _, [arg1;arg2] -> 
                let ty = tyOfExpr cenv.g arg1
                let op = binaryOp cenv.g m ty arg1 arg2
                ConvExprPrim cenv env op

            // For units of measure some binary operators change their return type, e.g. a * b where each is int<kg> gives int<kg*kg>
            | TOp.ILAsm ([ ILMulDivOp (binaryOp, isMul) ], _), _, [arg1;arg2] -> 
                let argty1 = tyOfExpr cenv.g arg1
                let argty2 = tyOfExpr cenv.g arg2
                let rty = 
                    match getMeasureOfType cenv.g argty1, getMeasureOfType cenv.g argty2 with
                    | Some (tcref, ms1), Some (_tcref2, ms2)  ->  mkAppTy tcref [TType_measure (Measure.Prod(ms1, if isMul then ms2 else Measure.Inv ms2))]
                    | Some _, None  -> argty1
                    | None, Some _ -> argty2
                    | None, None -> argty1
                let op = binaryOp cenv.g m argty1 argty2 rty arg1 arg2
                ConvExprPrim cenv env op

            | TOp.ILAsm ([ ILConvertOp convertOp1; ILConvertOp convertOp2 ], _), _, [arg] -> 
                let ty1 = tyOfExpr cenv.g arg
                let op1 = convertOp1 cenv.g m ty1 arg
                let ty2 = tyOfExpr cenv.g op1
                let op2 = convertOp2 cenv.g m ty2 op1
                ConvExprPrim cenv env op2

            | TOp.ILAsm ([ ILConvertOp convertOp ], [TType_app (tcref,_)]), _, [arg] -> 
                let ty = tyOfExpr cenv.g arg
                let op =
                    if tyconRefEq cenv.g tcref cenv.g.char_tcr then
                        mkCallToCharOperator cenv.g m ty arg
                    else convertOp cenv.g m ty arg
                ConvExprPrim cenv env op

            | TOp.ILAsm ([ I_throw ], _), _, [arg1]  -> 
                let raiseExpr = mkCallRaise cenv.g m (tyOfExpr cenv.g expr) arg1 
                ConvExprPrim cenv env raiseExpr        

            | TOp.ILAsm (instrs, _), tyargs, args                         -> 
                E.ILAsm(sprintf "%+A" instrs, ConvTypes cenv tyargs, ConvExprs cenv env args)

            | TOp.ExnConstr tcref, tyargs, args              -> 
                E.NewRecord(ConvType cenv (mkAppTy tcref tyargs), ConvExprs cenv env args) 

            | TOp.ValFieldSet rfref, _tinst, [obj;arg]     -> 
                let objR = ConvLValueExpr cenv env obj
                let argR = ConvExpr cenv env arg
                let typR = ConvType cenv (mkAppTy rfref.TyconRef tyargs)
                let projR = ConvRecdFieldRef cenv rfref 
                E.FSharpFieldSet(Some objR, typR, projR, argR) 

            | TOp.ValFieldSet rfref, _tinst, [arg]     -> 
                let argR = ConvExpr cenv env arg
                let typR = ConvType cenv (mkAppTy rfref.TyconRef tyargs)
                let projR = ConvRecdFieldRef cenv rfref 
                E.FSharpFieldSet(None, typR, projR, argR) 

            | TOp.ExnFieldGet (tcref, i), [], [obj] -> 
                let exnc = stripExnEqns tcref
                let fspec = exnc.TrueInstanceFieldsAsList.[i]
                let fref = mkRecdFieldRef tcref fspec.Name
                let typR = ConvType cenv (mkAppTy tcref tyargs)
                let objR = ConvExpr cenv env (mkCoerceExpr (obj, mkAppTy tcref [], m, cenv.g.exn_ty))
                E.FSharpFieldGet(Some objR, typR, ConvRecdFieldRef cenv fref) 

            | TOp.ExnFieldSet (tcref, i), [], [obj;e2] -> 
                let exnc = stripExnEqns tcref
                let fspec = exnc.TrueInstanceFieldsAsList.[i]
                let fref = mkRecdFieldRef tcref fspec.Name
                let typR = ConvType cenv (mkAppTy tcref tyargs)
                let objR = ConvExpr cenv env (mkCoerceExpr (obj, mkAppTy tcref [], m, cenv.g.exn_ty))
                E.FSharpFieldSet(Some objR, typR, ConvRecdFieldRef cenv fref, ConvExpr cenv env e2) 

            | TOp.Coerce, [tgtTy;srcTy], [x]  -> 
                if typeEquiv cenv.g tgtTy srcTy then 
                    ConvExprPrim cenv env x
                else
                    E.Coerce(ConvType cenv tgtTy, ConvExpr cenv env x) 

            | TOp.Reraise, [toTy], []         -> 
                // rebuild reraise<T>() and Convert 
                mkReraiseLibCall cenv.g toTy m |> ConvExprPrim cenv env 

            | TOp.LValueOp (LAddrOf _, vref), [], [] -> 
                E.AddressOf(ConvExpr cenv env (exprForValRef m vref)) 

            | TOp.LValueOp (LByrefSet, vref), [], [e] -> 
                E.AddressSet(ConvExpr cenv env (exprForValRef m vref), ConvExpr cenv env e) 

            | TOp.LValueOp (LSet, vref), [], [e] -> 
                E.ValueSet(FSharpMemberOrFunctionOrValue(cenv, vref), ConvExpr cenv env e) 

            | TOp.LValueOp (LByrefGet, vref), [], [] -> 
                ConvValRef cenv env m vref 

            | TOp.Array, [ty], xa -> 
                    E.NewArray(ConvType cenv ty, ConvExprs cenv env xa)                             

            | TOp.While _, [], [Expr.Lambda (_, _, _, [_], test, _, _);Expr.Lambda (_, _, _, [_], body, _, _)]  -> 
                    E.WhileLoop(ConvExpr cenv env test, ConvExpr cenv env body) 
        
            | TOp.For (_, dir), [], [Expr.Lambda (_, _, _, [_], lim0, _, _); Expr.Lambda (_, _, _, [_], SimpleArrayLoopUpperBound, lm, _); SimpleArrayLoopBody cenv.g (arr, elemTy, body)] ->
                let lim1 = 
                    let len = mkCallArrayLength cenv.g lm elemTy arr // Array.length arr
                    mkCallSubtractionOperator cenv.g lm cenv.g.int32_ty len (mkOne cenv.g lm) // len - 1
                E.FastIntegerForLoop(ConvExpr cenv env lim0, ConvExpr cenv env lim1, ConvExpr cenv env body, dir <> FSharpForLoopDown) 

            | TOp.For (_, dir), [], [Expr.Lambda (_, _, _, [_], lim0, _, _); Expr.Lambda (_, _, _, [_], lim1, lm, _); body]  -> 
                let lim1 =
                    if dir = CSharpForLoopUp then
                        mkCallSubtractionOperator cenv.g lm cenv.g.int32_ty lim1 (mkOne cenv.g lm) // len - 1
                    else lim1
                E.FastIntegerForLoop(ConvExpr cenv env lim0, ConvExpr cenv env lim1, ConvExpr cenv env body, dir <> FSharpForLoopDown) 

            | TOp.ILCall (_, _, _, isCtor, valUseFlag, _, _, ilMethRef, enclTypeInst, methInst, _), [], callArgs -> 
                ConvILCall cenv env (isCtor, valUseFlag, ilMethRef, enclTypeInst, methInst, callArgs, m)

            | TOp.TryFinally _, [_resty], [Expr.Lambda (_, _, _, [_], e1, _, _); Expr.Lambda (_, _, _, [_], e2, _, _)] -> 
                E.TryFinally(ConvExpr cenv env e1, ConvExpr cenv env e2) 

            | TOp.TryWith _, [_resty], [Expr.Lambda (_, _, _, [_], e1, _, _); Expr.Lambda (_, _, _, [vf], ef, _, _); Expr.Lambda (_, _, _, [vh], eh, _, _)] ->
                let vfR = ConvVal cenv vf
                let envf = env.BindVal vf
                let vhR = ConvVal cenv vh
                let envh = env.BindVal vh
                E.TryWith(ConvExpr cenv env e1, vfR, ConvExpr cenv envf ef, vhR, ConvExpr cenv envh eh) 

            | TOp.Bytes bytes, [], [] -> E.Const(box bytes, ConvType cenv (tyOfExpr cenv.g expr))

            | TOp.UInt16s arr, [], [] -> E.Const(box arr, ConvType cenv (tyOfExpr cenv.g expr))
              
            | TOp.UnionCaseProof _, _, [e]       -> ConvExprPrim cenv env e  // Note: we erase the union case proof conversions when converting to quotations
            | TOp.UnionCaseTagGet tycr, tyargs, [arg1]          -> 
                let typR = ConvType cenv (mkAppTy tycr tyargs)
                E.UnionCaseTag(ConvExpr cenv env arg1, typR) 

            | TOp.TraitCall (TTrait(tys, nm, memFlags, argtys, _rty, _solution, _traitCtxt)), _, _ -> 
                let tysR = ConvTypes cenv tys
                let tyargsR = ConvTypes cenv tyargs
                let argtysR = ConvTypes cenv argtys
                let argsR = ConvExprs cenv env args
                E.TraitCall(tysR, nm, memFlags, argtysR, tyargsR, argsR) 

            | TOp.RefAddrGet readonly, [ty], [e]  -> 
                let replExpr = mkRecdFieldGetAddrViaExprAddr(readonly, e, mkRefCellContentsRef cenv.g, [ty], m)
                ConvExprPrim cenv env replExpr

            | _ -> wfail (sprintf "unhandled construct in AST", m)
        | Expr.WitnessArg (traitInfo, _m) ->
            ConvWitnessInfoPrim cenv env traitInfo
        | _ -> 
            wfail (sprintf "unhandled construct in AST", expr.Range)

    and ConvWitnessInfoPrim _cenv env traitInfo : E =
        let witnessInfo = traitInfo.TraitKey
        let env = { env with suppressWitnesses = true }
        // First check if this is a witness in ReflectedDefinition code
        if env.witnessesInScope.ContainsKey witnessInfo then 
            let witnessArgIdx = env.witnessesInScope.[witnessInfo]
            E.WitnessArg(witnessArgIdx)
        // Otherwise it is a witness in a quotation literal 
        else
            //failwith "witness not found"
            E.WitnessArg(-1)

    and ConvWitnessInfo cenv env m traitInfo : FSharpExpr =
        let g = cenv.g
        let witnessInfo = traitInfo.TraitKey
        let witnessTy = GenWitnessTy g witnessInfo 
        let traitInfoR = ConvWitnessInfoPrim cenv env traitInfo
        Mk cenv m witnessTy traitInfoR

    and ConvLetBind cenv env (bind : Binding) = 
        match bind.Expr with 
        // Map for values bound by the 
        //     'let v = isinst e in .... if nonnull v then ...v .... ' 
        // construct arising out the compilation of pattern matching. We decode these back to the form
        //     'if istype e then ...unbox e .... ' 
        // It's bit annoying that pattern matching does this transformation. Like all premature optimization we pay a 
        // cost here to undo it.
        | Expr.Op (TOp.ILAsm ([ I_isinst _ ], _), [ty], [e], _) -> 
            None, env.BindIsInstVal bind.Var (ty, e)
    
        // Remove let <compilerGeneratedVar> = <var> from quotation tree
        | Expr.Val _ when bind.Var.IsCompilerGenerated && (not bind.Var.IsMutable) -> 
            None, env.BindSubstVal bind.Var bind.Expr

        // Remove let <compilerGeneratedVar> = () from quotation tree
        | Expr.Const (Const.Unit, _, _) when bind.Var.IsCompilerGenerated && (not bind.Var.IsMutable) -> 
            None, env.BindSubstVal bind.Var bind.Expr

        // Remove let unionCase = ... from quotation tree
        | Expr.Op (TOp.UnionCaseProof _, _, [e], _) when (not bind.Var.IsMutable) -> 
            None, env.BindSubstVal bind.Var e

        | _ ->
            let v = bind.Var
            let vR = ConvVal cenv v 
            let rhsR = ConvExpr cenv env bind.Expr
            let envinner = env.BindVal v
            Some(vR, rhsR), envinner

    and ConvILCall (cenv: SymbolEnv) env (isNewObj, valUseFlags, ilMethRef, enclTypeArgs, methTypeArgs, callArgs, m) =
        let isNewObj = (isNewObj || (match valUseFlags with CtorValUsedAsSuperInit | CtorValUsedAsSelfInit -> true | _ -> false))
        let methName = ilMethRef.Name
        let isPropGet = methName.StartsWithOrdinal("get_")
        let isPropSet = methName.StartsWithOrdinal("set_")
        let isProp = isPropGet || isPropSet
        
        let tcref, subClass = 
            // this does not matter currently, type checking fails to resolve it when a TP references a union case subclass
            try
                // if the type is an union case class, lookup will fail 
                Import.ImportILTypeRef cenv.amap m ilMethRef.DeclaringTypeRef, None
            with _ ->
                let e = ilMethRef.DeclaringTypeRef
                let parent = ILTypeRef.Create(e.Scope, e.Enclosing.Tail, e.Enclosing.Head)
                Import.ImportILTypeRef cenv.amap m parent, Some e.Name
                
        let enclosingType = generalizedTyconRef tcref
        
        let makeCall minfo =
            ConvObjectModelCallLinear cenv env (isNewObj, minfo, enclTypeArgs, methTypeArgs, [], callArgs) id   

        let makeFSCall isMember (vr: ValRef) =
            let memOrVal =
                if isMember then
                    let minfo = MethInfo.FSMeth(cenv.g, enclosingType, vr, None)
                    FSharpMemberOrFunctionOrValue(cenv, minfo)
                else
                    FSharpMemberOrFunctionOrValue(cenv, vr)
            makeCall memOrVal

        // takes a possibly fake ValRef and tries to resolve it to an F# expression
        let makeFSExpr isMember (vr: ValRef) =
            let nlr = vr.nlr 
            let enclosingEntity = 
                try
                    nlr.EnclosingEntity.Deref 
                with _ ->
                    failwithf "Failed to resolve type '%s'" (nlr.EnclosingEntity.CompiledName)
            let ccu = nlr.EnclosingEntity.nlr.Ccu
            let vName = nlr.ItemKey.PartialKey.LogicalName // this is actually compiled name
            let findByName =
                enclosingEntity.MembersOfFSharpTyconSorted |> List.filter (fun v -> (v.CompiledName cenv.g.CompilerGlobalState) = vName)
            match findByName with
            | [v] -> 
                makeFSCall isMember v
            | [] ->
                let typR = ConvType cenv (mkAppTy tcref enclTypeArgs)
                if enclosingEntity.IsModuleOrNamespace then
                    let findModuleMemberByName = 
                        enclosingEntity.ModuleOrNamespaceType.AllValsAndMembers 
                        |> Seq.filter (fun v -> 
                            (v.CompiledName cenv.g.CompilerGlobalState) = vName &&
                                match v.DeclaringEntity with
                                | Parent p -> p.PublicPath = enclosingEntity.PublicPath
                                | _ -> false 
                        ) |> List.ofSeq
                    match findModuleMemberByName with
                    | [v] ->
                        let vr = VRefLocal v
                        makeFSCall isMember vr
                    | [] ->
                        let isPropGet = vName.StartsWithOrdinal("get_")
                        let isPropSet = vName.StartsWithOrdinal("set_")
                        if isPropGet || isPropSet then
                            let name = PrettyNaming.ChopPropertyName vName          
                            let findByName =
                                enclosingEntity.ModuleOrNamespaceType.AllValsAndMembers 
                                |> Seq.filter (fun v -> (v.CompiledName cenv.g.CompilerGlobalState) = name)
                                |> List.ofSeq
                            match findByName with
                            | [ v ] ->
                                let m = FSharpMemberOrFunctionOrValue(cenv, VRefLocal v)
                                if isPropGet then
                                    E.Value m
                                else     
                                    let valR = ConvExpr cenv env callArgs.Head
                                    E.ValueSet (m, valR)
                            | _ -> failwith "Failed to resolve module value unambiguously"
                        else
                            failwith "Failed to resolve module member" 
                    | _ ->
                        failwith "Failed to resolve overloaded module member"
                elif enclosingEntity.IsRecordTycon then
                    if isProp then
                        let name = PrettyNaming.ChopPropertyName vName                                    
                        let projR = ConvRecdFieldRef cenv (RecdFieldRef(tcref, name))
                        let objR = ConvLValueExpr cenv env callArgs.Head
                        if isPropGet then
                            E.FSharpFieldGet(Some objR, typR, projR)
                        else
                            let valR = ConvExpr cenv env callArgs.Tail.Head
                            E.FSharpFieldSet(Some objR, typR, projR, valR)
                    elif vName = ".ctor" then
                        let argsR = ConvExprs cenv env callArgs
                        E.NewRecord(typR, argsR)
                    else
                        failwith "Failed to recognize record type member"
                elif enclosingEntity.IsUnionTycon then
                    if vName = "GetTag" || vName = "get_Tag" then
                        let objR = ConvExpr cenv env callArgs.Head
                        E.UnionCaseTag(objR, typR) 
                    elif vName.StartsWithOrdinal("New") then
                        let name = vName.Substring 3
                        let mkR = ConvUnionCaseRef cenv (UnionCaseRef(tcref, name))
                        let argsR = ConvExprs cenv env callArgs
                        E.NewUnionCase(typR, mkR, argsR)
                    elif vName.StartsWithOrdinal("Is") then
                        let name = vName.Substring 2
                        let mkR = ConvUnionCaseRef cenv (UnionCaseRef(tcref, name))
                        let objR = ConvExpr cenv env callArgs.Head
                        E.UnionCaseTest(objR, typR, mkR)
                    else 
                        match subClass with
                        | Some name ->
                            let ucref = UnionCaseRef(tcref, name)
                            let mkR = ConvUnionCaseRef cenv ucref
                            let objR = ConvLValueExpr cenv env callArgs.Head
                            let projR = FSharpField(cenv, ucref, ucref.Index)
                            E.UnionCaseGet(objR, typR, mkR, projR)
                        | _ ->
                            failwith "Failed to recognize union type member"
                else
                    let names = enclosingEntity.MembersOfFSharpTyconSorted |> List.map (fun v -> v.CompiledName cenv.g.CompilerGlobalState) |> String.concat ", "
                    failwithf "Member '%s' not found in type %s, found: %s" vName enclosingEntity.DisplayName names
            | _ -> // member is overloaded
                match nlr.ItemKey.TypeForLinkage with
                | None -> failwith "Type of signature could not be resolved"
                | Some keyTy ->
                    let findBySig =
                        findByName |> List.tryFind (fun v -> ccu.MemberSignatureEquality(keyTy, v.Type))
                    match findBySig with
                    | Some v ->
                        makeFSCall isMember v
                    | _ ->
                        failwith "Failed to recognize F# member"

        // First try to resolve it to IL metadata
        let try1 = 
            if tcref.IsILTycon then 
                try 
                    let mdef = resolveILMethodRefWithRescope unscopeILType tcref.ILTyconRawMetadata ilMethRef 
                    let minfo = MethInfo.CreateILMeth(cenv.amap, m, enclosingType, mdef)                     
                    FSharpMemberOrFunctionOrValue(cenv, minfo) |> makeCall |> Some
                with _ -> 
                    None
            else
                None

        // Otherwise try to bind it to an F# symbol
        match try1 with
        | Some res -> res
        | None ->
          try
            // Try to bind the call to an F# method call
            let memberParentName = if tcref.IsModuleOrNamespace then None else Some tcref.LogicalName
            // this logical name is not correct in the presence of CompiledName
            let logicalName = ilMethRef.Name 
            let isMember = memberParentName.IsSome
            if isMember then 
                match ilMethRef.Name, ilMethRef.DeclaringTypeRef.Name with
                | "Invoke", "Microsoft.FSharp.Core.FSharpFunc`2" ->
                    let objR = ConvLValueExpr cenv env callArgs.Head
                    let argR = ConvExpr cenv env callArgs.Tail.Head
                    let typR = ConvType cenv enclTypeArgs.Head
                    E.Application(objR, [typR], [argR])
                | _ ->
                let isCtor = (ilMethRef.Name = ".ctor")
                let isStatic = isCtor || ilMethRef.CallingConv.IsStatic
                let scoref = ilMethRef.DeclaringTypeRef.Scope
                let typars1 = tcref.Typars m
                let typars2 = [ 1 .. ilMethRef.GenericArity ] |> List.map (fun _ -> Construct.NewRigidTypar "T" m)
                let tinst1 = typars1 |> generalizeTypars
                let tinst2 = typars2 |> generalizeTypars
                // TODO: this will not work for curried methods in F# classes.
                // This is difficult to solve as the information in the ILMethodRef
                // is not sufficient to resolve to a symbol unambiguously in these cases.
                let argtys = [ ilMethRef.ArgTypes |> List.map (ImportILTypeFromMetadata cenv.amap m scoref tinst1 tinst2) ]
                let rty = 
                    match ImportReturnTypeFromMetadata cenv.amap m ilMethRef.ReturnType emptyILCustomAttrs scoref tinst1 tinst2 with 
                    | None -> if isCtor then  enclosingType else cenv.g.unit_ty
                    | Some ty -> ty

                let linkageType = 
                    let ty = mkIteratedFunTy (List.map (mkRefTupledTy cenv.g) argtys) rty
                    let ty = if isStatic then ty else mkFunTy enclosingType ty 
                    mkForallTyIfNeeded (typars1 @ typars2) ty

                let argCount = List.sum (List.map List.length argtys)  + (if isStatic then 0 else 1)
                let key = ValLinkageFullKey({ MemberParentMangledName=memberParentName; MemberIsOverride=false; LogicalName=logicalName; TotalArgCount= argCount }, Some linkageType)

                let (PubPath p) = tcref.PublicPath.Value
                let enclosingNonLocalRef = mkNonLocalEntityRef tcref.nlr.Ccu p
                let vref = mkNonLocalValRef enclosingNonLocalRef key
                makeFSExpr isMember vref 

            else 
                let key = ValLinkageFullKey({ MemberParentMangledName=memberParentName; MemberIsOverride=false; LogicalName=logicalName; TotalArgCount= 0 }, None)
                let vref = mkNonLocalValRef tcref.nlr key
                makeFSExpr isMember vref 

          with e -> 
            failwithf "An IL call to '%s' could not be resolved: %s" (ilMethRef.ToString()) e.Message

    and ConvObjectModelCallLinear cenv env (isNewObj, v: FSharpMemberOrFunctionOrValue, enclTyArgs, methTyArgs, witnessArgsR: FSharpExpr list, callArgs) contF =
        let enclTyArgsR = ConvTypes cenv enclTyArgs
        let methTyArgsR = ConvTypes cenv methTyArgs
        let obj, callArgs = 
            if v.IsInstanceMember then 
                match callArgs with 
                | obj :: rest -> Some obj, rest
                | _ -> failwith (sprintf "unexpected shape of arguments: %A" callArgs)
            else
                None, callArgs
        let objR = Option.map (ConvLValueExpr cenv env) obj
        // tailcall
        ConvExprsLinear cenv env callArgs (contF << fun callArgsR -> 
            if isNewObj then 
                E.NewObject(v, enclTyArgsR, callArgsR) 
            else 
                E.Call(objR, v, enclTyArgsR, methTyArgsR, witnessArgsR, callArgsR))

    and ConvExprs cenv env args = List.map (ConvExpr cenv env) args 

    // Process a list of expressions in a tail-recursive way. Identical to "ConvExprs" but the result is eventually passed to contF.
    and ConvExprsLinear cenv env args contF = 
        match args with 
        | [] -> contF []
        | [arg] -> ConvExprLinear cenv env arg (fun argR -> contF [argR])
        | arg :: rest -> ConvExprLinear cenv env arg (fun argR -> ConvExprsLinear cenv env rest (fun restR -> contF (argR :: restR)))

    and ConvTargetsLinear cenv env tgs contF = 
        match tgs with 
        | [] -> contF []
        | TTarget(vars, rhs, _) :: rest -> 
            let varsR = (List.rev vars) |> List.map (ConvVal cenv)
            ConvExprLinear cenv env rhs (fun targetR -> 
            ConvTargetsLinear cenv env rest (fun restR -> 
            contF ((varsR, targetR) :: restR)))

    and ConvValRef cenv env m (vref: ValRef) =
        let v = vref.Deref
        if env.isinstVals.ContainsVal v then 
            let (ty, e) = env.isinstVals.[v]
            ConvExprPrim cenv env (mkCallUnbox cenv.g m ty e)
        elif env.substVals.ContainsVal v then 
            let e = env.substVals.[v]
            ConvExprPrim cenv env e
        elif v.BaseOrThisInfo = CtorThisVal then 
            E.ThisValue(ConvType cenv v.Type) 
        elif v.BaseOrThisInfo = BaseVal then 
            E.BaseValue(ConvType cenv v.Type) 
        else 
            E.Value(FSharpMemberOrFunctionOrValue(cenv, vref)) 

    and ConvVal cenv (v: Val) =  
        let vref = mkLocalValRef v 
        FSharpMemberOrFunctionOrValue(cenv, vref) 

    and ConvConst cenv env m c ty =
        match TryEliminateDesugaredConstants cenv.g m c with 
        | Some e -> ConvExprPrim cenv env e
        | None ->
            let tyR = ConvType cenv ty
            match c with 
            | Const.Bool    i ->  E.Const(box i, tyR)
            | Const.SByte   i ->  E.Const(box i, tyR)
            | Const.Byte    i ->  E.Const(box i, tyR)
            | Const.Int16   i ->  E.Const(box i, tyR)
            | Const.UInt16  i ->  E.Const(box i, tyR)
            | Const.Int32   i ->  E.Const(box i, tyR)
            | Const.UInt32  i ->  E.Const(box i, tyR)
            | Const.Int64   i ->  E.Const(box i, tyR)
            | Const.UInt64  i ->  E.Const(box i, tyR)
            | Const.IntPtr  i ->  E.Const(box (nativeint i), tyR)
            | Const.UIntPtr i ->  E.Const(box (unativeint i), tyR)
            | Const.Decimal i ->  E.Const(box i, tyR)
            | Const.Double  i ->  E.Const(box i, tyR)
            | Const.Single  i ->  E.Const(box i, tyR)
            | Const.String  i ->  E.Const(box i, tyR)
            | Const.Char    i ->  E.Const(box i, tyR)
            | Const.Unit      ->  E.Const(box (), tyR)
            | Const.Zero      ->  E.DefaultValue (ConvType cenv ty)

    and ConvDecisionTree cenv env dtreeRetTy x m = 
        ConvDecisionTreePrim cenv env dtreeRetTy x |> Mk cenv m dtreeRetTy

    and ConvDecisionTreePrim cenv env dtreeRetTy x = 
        match x with 
        | TDSwitch(e1, csl, dfltOpt, m) -> 
            let acc = 
                match dfltOpt with 
                | Some d -> ConvDecisionTreePrim cenv env dtreeRetTy d 
                | None -> wfail( "FSharp.Compiler.Service cannot yet return this kind of pattern match", m)
            (csl, acc) ||> List.foldBack (fun (TCase(discrim, dtree)) acc -> 
                    let acc = acc |> Mk cenv m dtreeRetTy
                    match discrim with 
                    | DecisionTreeTest.UnionCase (ucref, tyargs) -> 
                        let objR = ConvExpr cenv env e1
                        let ucR = ConvUnionCaseRef cenv ucref 
                        let utypR = ConvType cenv (mkAppTy ucref.TyconRef tyargs)
                        E.IfThenElse (E.UnionCaseTest (objR, utypR, ucR) |> Mk cenv m cenv.g.bool_ty, ConvDecisionTree cenv env dtreeRetTy dtree m, acc) 
                    | DecisionTreeTest.Const (Const.Bool true) -> 
                        let e1R = ConvExpr cenv env e1
                        E.IfThenElse (e1R, ConvDecisionTree cenv env dtreeRetTy dtree m, acc) 
                    | DecisionTreeTest.Const (Const.Bool false) -> 
                        let e1R = ConvExpr cenv env e1
                        // Note, reverse the branches
                        E.IfThenElse (e1R, acc, ConvDecisionTree cenv env dtreeRetTy dtree m) 
                    | DecisionTreeTest.Const c -> 
                        let ty = tyOfExpr cenv.g e1
                        let eq = mkCallEqualsOperator cenv.g m ty e1 (Expr.Const (c, m, ty))
                        let eqR = ConvExpr cenv env eq 
                        E.IfThenElse (eqR, ConvDecisionTree cenv env dtreeRetTy dtree m, acc) 
                    | DecisionTreeTest.IsNull -> 
                        // Decompile cached isinst tests
                        match e1 with 
                        | Expr.Val (vref, _, _) when env.isinstVals.ContainsVal vref.Deref  ->
                            let (ty, e) =  env.isinstVals.[vref.Deref]
                            let tyR = ConvType cenv ty
                            let eR = ConvExpr cenv env e
                            // note: reverse the branches - a null test is a failure of an isinst test
                            E.IfThenElse (E.TypeTest (tyR, eR) |> Mk cenv m cenv.g.bool_ty, acc, ConvDecisionTree cenv env dtreeRetTy dtree m) 
                        | _ -> 
                            let ty = tyOfExpr cenv.g e1
                            let eqR =
                                let eq = mkCallEqualsOperator cenv.g m ty e1 (Expr.Const (Const.Zero, m, ty))
                                let env = { env with suppressWitnesses = true }
                                ConvExpr cenv env eq 
                            E.IfThenElse (eqR, ConvDecisionTree cenv env dtreeRetTy dtree m, acc) 
                    | DecisionTreeTest.IsInst (_srcty, tgty) -> 
                        let e1R = ConvExpr cenv env e1
                        E.IfThenElse (E.TypeTest (ConvType cenv tgty, e1R)  |> Mk cenv m cenv.g.bool_ty, ConvDecisionTree cenv env dtreeRetTy dtree m, acc) 
                    | DecisionTreeTest.ActivePatternCase _ -> wfail("unexpected Test.ActivePatternCase test in quoted expression", m)
                    | DecisionTreeTest.ArrayLength _ -> wfail("FSharp.Compiler.Service cannot yet return array pattern matching", m)
                    | DecisionTreeTest.Error m -> wfail("error recovery", m))

        | TDSuccess (args, n) -> 
                // TAST stores pattern bindings in reverse order for some reason
                // Reverse them here to give a good presentation to the user
                let args = List.rev args
                let argsR = ConvExprs cenv env args          
                E.DecisionTreeSuccess(n, argsR)
          
        | TDBind(bind, rest) -> 
                // The binding may be a compiler-generated binding that gets removed in the quotation presentation
                match ConvLetBind cenv env bind with 
                | None, env -> ConvDecisionTreePrim cenv env dtreeRetTy rest 
                | Some bindR, env -> E.Let(bindR, ConvDecisionTree cenv env dtreeRetTy rest bind.Var.Range) 

    /// Wrap the conversion in a function to make it on-demand.  Any pattern matching on the FSharpExpr will
    /// force the evaluation of the entire conversion process eagerly.
    let ConvExprOnDemand cenv env expr = 
        FSharpExpr(cenv, Some(fun () -> ConvExpr cenv env expr), E.Unused, expr.Range, tyOfExpr cenv.g expr)



/// The contents of the F# assembly as provided through the compiler API
type FSharpAssemblyContents(cenv: SymbolEnv, mimpls: TypedImplFile list) = 

    new (tcGlobals, thisCcu, thisCcuType, tcImports, mimpls) = FSharpAssemblyContents(SymbolEnv(tcGlobals, thisCcu, thisCcuType, tcImports), mimpls)

    member _.ImplementationFiles = 
        [ for mimpl in mimpls -> FSharpImplementationFileContents(cenv, mimpl)]

and FSharpImplementationFileDeclaration = 
    | Entity of entity: FSharpEntity * declarations: FSharpImplementationFileDeclaration list
    | MemberOrFunctionOrValue of value: FSharpMemberOrFunctionOrValue * curriedArgs: FSharpMemberOrFunctionOrValue list list * body: FSharpExpr
    | InitAction of action: FSharpExpr

and FSharpImplementationFileContents(cenv, mimpl) = 
    let (TImplFile (qname, _pragmas, ModuleOrNamespaceExprWithSig(_, mdef, _), hasExplicitEntryPoint, isScript, _anonRecdTypes)) = mimpl 
    let rec getDecls2 (ModuleOrNamespaceExprWithSig(_mty, def, _m)) = getDecls def
    and getBind (bind: Binding) = 
        let v = bind.Var
        assert v.IsCompiledAsTopLevel
        let topValInfo = InferArityOfExprBinding cenv.g AllowTypeDirectedDetupling.Yes v bind.Expr
        let tps, _ctorThisValOpt, _baseValOpt, vsl, body, _bodyty = IteratedAdjustArityOfLambda cenv.g cenv.amap topValInfo bind.Expr
        let v = FSharpMemberOrFunctionOrValue(cenv, mkLocalValRef v)
        let gps = v.GenericParameters
        let vslR = List.map (List.map (FSharpExprConvert.ConvVal cenv)) vsl 
        let env = ExprTranslationEnv.Empty(cenv.g).BindTypars (Seq.zip tps gps |> Seq.toList)
        let env = env.BindCurriedVals vsl 
        let e = FSharpExprConvert.ConvExprOnDemand cenv env body
        FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v, vslR, e) 

    and getDecls mdef = 
        match mdef with 
        | TMDefRec(_isRec, tycons, mbinds, _m) ->
            [ for tycon in tycons do 
                  let entity = FSharpEntity(cenv, mkLocalEntityRef tycon)
                  yield FSharpImplementationFileDeclaration.Entity(entity, []) 
              for mbind in mbinds do 
                  match mbind with 
                  | ModuleOrNamespaceBinding.Module(mspec, def) -> 
                      let entity = FSharpEntity(cenv, mkLocalEntityRef mspec)
                      yield FSharpImplementationFileDeclaration.Entity (entity, getDecls def) 
                  | ModuleOrNamespaceBinding.Binding bind -> 
                      yield getBind bind ]
        | TMAbstract mexpr -> getDecls2 mexpr
        | TMDefLet(bind, _m)  ->
            [ yield getBind bind  ]
        | TMDefDo(expr, _m)  ->
            [ let expr = FSharpExprConvert.ConvExprOnDemand cenv (ExprTranslationEnv.Empty(cenv.g)) expr
              yield FSharpImplementationFileDeclaration.InitAction expr  ]
        | TMDefs mdefs -> 
            [ for mdef in mdefs do yield! getDecls mdef ]

    member _.QualifiedName = qname.Text
    member _.FileName = qname.Range.FileName
    member _.Declarations = getDecls mdef 
    member _.HasExplicitEntryPoint = hasExplicitEntryPoint
    member _.IsScript = isScript


module FSharpExprPatterns = 
    let (|Value|_|) (e: FSharpExpr) = match e.E with E.Value v -> Some v | _ -> None

    let (|Const|_|) (e: FSharpExpr) = match e.E with E.Const (v, ty) -> Some (v, ty) | _ -> None

    let (|TypeLambda|_|) (e: FSharpExpr) = match e.E with E.TypeLambda (v, e) -> Some (v, e) | _ -> None

    let (|Lambda|_|) (e: FSharpExpr) = match e.E with E.Lambda (v, e) -> Some (v, e) | _ -> None

    let (|Application|_|) (e: FSharpExpr) = match e.E with E.Application (f, tys, e) -> Some (f, tys, e) | _ -> None

    let (|IfThenElse|_|) (e: FSharpExpr) = match e.E with E.IfThenElse (e1, e2, e3) -> Some (e1, e2, e3) | _ -> None

    let (|Let|_|) (e: FSharpExpr) = match e.E with E.Let ((v, e), b) -> Some ((v, e), b) | _ -> None

    let (|LetRec|_|) (e: FSharpExpr) = match e.E with E.LetRec (ves, b) -> Some (ves, b) | _ -> None

    let (|NewRecord|_|) (e: FSharpExpr) = match e.E with E.NewRecord (ty, es) -> Some (ty, es) | _ -> None

    let (|NewAnonRecord|_|) (e: FSharpExpr) = match e.E with E.NewAnonRecord (ty, es) -> Some (ty, es) | _ -> None

    let (|NewUnionCase|_|) (e: FSharpExpr) = match e.E with E.NewUnionCase (e, tys, es) -> Some (e, tys, es) | _ -> None

    let (|NewTuple|_|) (e: FSharpExpr) = match e.E with E.NewTuple (ty, es) -> Some (ty, es) | _ -> None

    let (|TupleGet|_|) (e: FSharpExpr) = match e.E with E.TupleGet (ty, n, es) -> Some (ty, n, es) | _ -> None

    let (|Call|_|) (e: FSharpExpr) = match e.E with E.Call (a, b, c, d, _e, f) -> Some (a, b, c, d, f) | _ -> None

    let (|CallWithWitnesses|_|) (e: FSharpExpr) = match e.E with E.Call (a, b, c, d, e, f) -> Some (a, b, c, d, e, f) | _ -> None

    let (|NewObject|_|) (e: FSharpExpr) = match e.E with E.NewObject (a, b, c) -> Some (a, b, c) | _ -> None

    let (|FSharpFieldGet|_|) (e: FSharpExpr) = match e.E with E.FSharpFieldGet (a, b, c) -> Some (a, b, c) | _ -> None

    let (|AnonRecordGet|_|) (e: FSharpExpr) = match e.E with E.AnonRecordGet (a, b, c) -> Some (a, b, c) | _ -> None

    let (|FSharpFieldSet|_|) (e: FSharpExpr) = match e.E with E.FSharpFieldSet (a, b, c, d) -> Some (a, b, c, d) | _ -> None

    let (|UnionCaseGet|_|) (e: FSharpExpr) = match e.E with E.UnionCaseGet (a, b, c, d) -> Some (a, b, c, d) | _ -> None

    let (|UnionCaseTag|_|) (e: FSharpExpr) = match e.E with E.UnionCaseTag (a, b) -> Some (a, b) | _ -> None

    let (|UnionCaseTest|_|) (e: FSharpExpr) = match e.E with E.UnionCaseTest (a, b, c) -> Some (a, b, c) | _ -> None

    let (|NewArray|_|) (e: FSharpExpr) = match e.E with E.NewArray (a, b) -> Some (a, b) | _ -> None

    let (|Coerce|_|) (e: FSharpExpr) = match e.E with E.Coerce (a, b) -> Some (a, b) | _ -> None

    let (|Quote|_|) (e: FSharpExpr) = match e.E with E.Quote a -> Some a | _ -> None

    let (|TypeTest|_|) (e: FSharpExpr) = match e.E with E.TypeTest (a, b) -> Some (a, b) | _ -> None

    let (|Sequential|_|) (e: FSharpExpr) = match e.E with E.Sequential (a, b) -> Some (a, b) | _ -> None

    let (|FastIntegerForLoop|_|) (e: FSharpExpr) = match e.E with E.FastIntegerForLoop (a, b, c, d) -> Some (a, b, c, d) | _ -> None

    let (|WhileLoop|_|) (e: FSharpExpr) = match e.E with E.WhileLoop (a, b) -> Some (a, b) | _ -> None

    let (|TryFinally|_|) (e: FSharpExpr) = match e.E with E.TryFinally (a, b) -> Some (a, b) | _ -> None

    let (|TryWith|_|) (e: FSharpExpr) = match e.E with E.TryWith (a, b, c, d, e) -> Some (a, b, c, d, e) | _ -> None

    let (|NewDelegate|_|) (e: FSharpExpr) = match e.E with E.NewDelegate (ty, e) -> Some (ty, e) | _ -> None

    let (|DefaultValue|_|) (e: FSharpExpr) = match e.E with E.DefaultValue ty -> Some ty | _ -> None

    let (|AddressSet|_|) (e: FSharpExpr) = match e.E with E.AddressSet (a, b) -> Some (a, b) | _ -> None

    let (|ValueSet|_|) (e: FSharpExpr) = match e.E with E.ValueSet (a, b) -> Some (a, b) | _ -> None

    let (|AddressOf|_|) (e: FSharpExpr) = match e.E with E.AddressOf a -> Some a | _ -> None

    let (|ThisValue|_|) (e: FSharpExpr) = match e.E with E.ThisValue a -> Some a | _ -> None

    let (|BaseValue|_|) (e: FSharpExpr) = match e.E with E.BaseValue a -> Some a | _ -> None

    let (|ILAsm|_|) (e: FSharpExpr) = match e.E with E.ILAsm (a, b, c) -> Some (a, b, c) | _ -> None

    let (|ILFieldGet|_|) (e: FSharpExpr) = match e.E with E.ILFieldGet (a, b, c) -> Some (a, b, c) | _ -> None

    let (|ILFieldSet|_|) (e: FSharpExpr) = match e.E with E.ILFieldSet (a, b, c, d) -> Some (a, b, c, d) | _ -> None

    let (|ObjectExpr|_|) (e: FSharpExpr) = match e.E with E.ObjectExpr (a, b, c, d) -> Some (a, b, c, d) | _ -> None

    let (|DecisionTree|_|) (e: FSharpExpr) = match e.E with E.DecisionTree (a, b) -> Some (a, b) | _ -> None

    let (|DecisionTreeSuccess|_|) (e: FSharpExpr) = match e.E with E.DecisionTreeSuccess (a, b) -> Some (a, b) | _ -> None

    let (|UnionCaseSet|_|) (e: FSharpExpr) = match e.E with E.UnionCaseSet (a, b, c, d, e) -> Some (a, b, c, d, e) | _ -> None

    let (|TraitCall|_|) (e: FSharpExpr) = match e.E with E.TraitCall (a, b, c, d, e, f) -> Some (a, b, c, d, e, f) | _ -> None

    let (|WitnessArg|_|) (e: FSharpExpr) = match e.E with E.WitnessArg n -> Some n | _ -> None

