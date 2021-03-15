// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

///  Generate the hash/compare functions we add to user-defined types by default.
module internal FSharp.Compiler.AugmentWithHashCompare
 
open Internal.Utilities.Library
open FSharp.Compiler.AbstractIL 
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Infos
open FSharp.Compiler.Syntax
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TypedTreeOps

let mkIComparableCompareToSlotSig (g: TcGlobals) = 
    TSlotSig("CompareTo", g.mk_IComparable_ty, [], [], [[TSlotParam(Some("obj"), g.obj_ty, false, false, false, [])]], Some g.int_ty)
    
let mkGenericIComparableCompareToSlotSig (g: TcGlobals) ty =
    TSlotSig("CompareTo", (mkAppTy g.system_GenericIComparable_tcref [ty]), [], [], [[TSlotParam(Some("obj"), ty, false, false, false, [])]], Some g.int_ty)
    
let mkIStructuralComparableCompareToSlotSig (g: TcGlobals) =
    TSlotSig("CompareTo", g.mk_IStructuralComparable_ty, [], [], [[TSlotParam(None, (mkRefTupledTy g [g.obj_ty ; g.IComparer_ty]), false, false, false, [])]], Some g.int_ty)
    
let mkGenericIEquatableEqualsSlotSig (g: TcGlobals) ty =
    TSlotSig("Equals", (mkAppTy g.system_GenericIEquatable_tcref [ty]), [], [], [[TSlotParam(Some("obj"), ty, false, false, false, [])]], Some g.bool_ty)
    
let mkIStructuralEquatableEqualsSlotSig (g: TcGlobals) =
    TSlotSig("Equals", g.mk_IStructuralEquatable_ty, [], [], [[TSlotParam(None, (mkRefTupledTy g [g.obj_ty ; g.IEqualityComparer_ty]), false, false, false, [])]], Some g.bool_ty)

let mkIStructuralEquatableGetHashCodeSlotSig (g: TcGlobals) =
    TSlotSig("GetHashCode", g.mk_IStructuralEquatable_ty, [], [], [[TSlotParam(None, g.IEqualityComparer_ty, false, false, false, [])]], Some g.int_ty)
 
let mkGetHashCodeSlotSig (g: TcGlobals) = 
    TSlotSig("GetHashCode", g.obj_ty, [], [], [[]], Some  g.int_ty)

let mkEqualsSlotSig (g: TcGlobals) = 
    TSlotSig("Equals", g.obj_ty, [], [], [[TSlotParam(Some("obj"), g.obj_ty, false, false, false, [])]], Some  g.bool_ty)

//-------------------------------------------------------------------------
// Helpers associated with code-generation of comparison/hash augmentations
//------------------------------------------------------------------------- 

let mkThisTy  g ty = if isStructTy g ty then mkByrefTy g ty else ty 

let mkCompareObjTy          g ty = (mkThisTy g ty) --> (g.obj_ty --> g.int_ty)

let mkCompareTy             g ty = (mkThisTy g ty) --> (ty --> g.int_ty)

let mkCompareWithComparerTy g ty = (mkThisTy g ty) --> ((mkRefTupledTy g [g.obj_ty ; g.IComparer_ty]) --> g.int_ty)

let mkEqualsObjTy          g ty = (mkThisTy g ty) --> (g.obj_ty --> g.bool_ty)

let mkEqualsTy             g ty = (mkThisTy g ty) --> (ty --> g.bool_ty)

let mkEqualsWithComparerTy g ty = (mkThisTy g ty) --> ((mkRefTupledTy g [g.obj_ty ; g.IEqualityComparer_ty]) --> g.bool_ty)

let mkHashTy             g ty = (mkThisTy g ty) --> (g.unit_ty --> g.int_ty)

let mkHashWithComparerTy g ty = (mkThisTy g ty) --> (g.IEqualityComparer_ty --> g.int_ty)

let mkIsCaseTy           g ty = (mkThisTy g ty) --> (g.unit_ty --> g.bool_ty)

//-------------------------------------------------------------------------
// Polymorphic comparison
//------------------------------------------------------------------------- 

let mkRelBinOp (g: TcGlobals) op m e1 e2 = mkAsmExpr ([ op  ], [], [e1; e2], [g.bool_ty], m)

let mkClt g m e1 e2 = mkRelBinOp g IL.AI_clt m e1 e2 

let mkCgt g m e1 e2 = mkRelBinOp g IL.AI_cgt m e1 e2

//-------------------------------------------------------------------------
// REVIEW: make this a .constrained call, not a virtual call.
//------------------------------------------------------------------------- 

// for creating and using GenericComparer objects and for creating and using 
// IStructuralComparable objects (Eg, Calling CompareTo(obj o, IComparer comp))

let mkILLangPrimTy (g: TcGlobals) = mkILNonGenericBoxedTy g.tcref_LanguagePrimitives.CompiledRepresentationForNamedType

let mkILCallGetComparer (g: TcGlobals) m = 
    let ty = mkILNonGenericBoxedTy g.tcref_System_Collections_IComparer.CompiledRepresentationForNamedType
    let mspec = mkILNonGenericStaticMethSpecInTy (mkILLangPrimTy g, "get_GenericComparer", [], ty)
    mkAsmExpr ([IL.mkNormalCall mspec], [], [], [g.IComparer_ty], m)

let mkILCallGetEqualityComparer (g: TcGlobals) m = 
    let ty = mkILNonGenericBoxedTy g.tcref_System_Collections_IEqualityComparer.CompiledRepresentationForNamedType
    let mspec = mkILNonGenericStaticMethSpecInTy (mkILLangPrimTy g, "get_GenericEqualityComparer", [], ty)
    mkAsmExpr ([IL.mkNormalCall mspec], [], [], [g.IEqualityComparer_ty], m)

let mkThisVar g m ty = mkCompGenLocal m "this" (mkThisTy g ty)  

let mkShl g m acce n = mkAsmExpr ([ IL.AI_shl ], [], [acce; mkInt g m n], [g.int_ty], m)

let mkShr g m acce n = mkAsmExpr ([ IL.AI_shr ], [], [acce; mkInt g m n], [g.int_ty], m)

let mkAdd (g: TcGlobals) m e1 e2 = mkAsmExpr ([ IL.AI_add ], [], [e1;e2], [g.int_ty], m)
                   
let mkAddToHashAcc g m e accv acce =
    mkValSet m accv (mkAdd g m (mkInt g m 0x9e3779b9) 
                          (mkAdd g m e 
                             (mkAdd g m (mkShl g m acce 6) (mkShr g m acce 2))))

     
let mkCombineHashGenerators g m exprs accv acce =
    (acce, exprs) ||> List.fold (fun tm e -> mkCompGenSequential m (mkAddToHashAcc g m e accv acce) tm)

//-------------------------------------------------------------------------
// Build comparison functions for union, record and exception types.
//------------------------------------------------------------------------- 

let mkThatAddrLocal g m ty = mkCompGenLocal m "obj" (mkThisTy g ty)     

let mkThatAddrLocalIfNeeded g m tcve ty = 
    if isStructTy g ty then 
        let thataddrv, thataddre = mkCompGenLocal m "obj" (mkThisTy g ty) 
        Some thataddrv, thataddre
    else None, tcve
        
let mkThisVarThatVar g m ty =
    let thisv, thise = mkThisVar g m ty
    let thataddrv, thataddre = mkThatAddrLocal g m ty
    thisv, thataddrv, thise, thataddre

let mkThatVarBind g m ty thataddrv expr = 
    if isStructTy g ty then 
      let thatv2, _ = mkMutableCompGenLocal m "obj" ty 
      thatv2, mkCompGenLet m thataddrv (mkValAddr m false (mkLocalValRef thatv2)) expr
    else thataddrv, expr 

let mkBindThatAddr g m ty thataddrv thatv thate expr =
    if isStructTy g ty then
        // let thataddrv = &thatv
        mkCompGenLet m thataddrv (mkValAddr m false (mkLocalValRef thatv))  expr  
    else
        // let thataddrv = that
        mkCompGenLet m thataddrv thate expr 

let mkBindThatAddrIfNeeded m thataddrvOpt thatv expr =
    match thataddrvOpt with 
    | None -> expr
    | Some thataddrv ->
        // let thataddrv = &thatv
        mkCompGenLet m thataddrv (mkValAddr m false (mkLocalValRef thatv))  expr

let mkDerefThis g m (thisv: Val) thise =
    if isByrefTy g thisv.Type then  mkAddrGet m (mkLocalValRef thisv)
    else thise

let mkCompareTestConjuncts g m exprs =
    match exprs with 
    | [] -> mkZero g m
    | [h] -> h
    | l -> 
        let a, b = List.frontAndBack l 
        (a, b) ||> List.foldBack (fun e acc -> 
            let nv, ne = mkCompGenLocal m "n" g.int_ty
            mkCompGenLet m nv e
              (mkCond DebugPointAtBinding.NoneAtSticky DebugPointForTarget.No m g.int_ty
                 (mkClt g m ne (mkZero g m))
                 ne
                 (mkCond DebugPointAtBinding.NoneAtSticky DebugPointForTarget.No m g.int_ty 
                    (mkCgt g m ne (mkZero g m))
                    ne
                    acc)))

let mkEqualsTestConjuncts g m exprs =
    match exprs with 
    | [] -> mkOne g m
    | [h] -> h
    | l -> 
        let a, b = List.frontAndBack l 
        List.foldBack (fun e acc -> mkCond DebugPointAtBinding.NoneAtSticky DebugPointForTarget.No m g.bool_ty e acc (mkFalse g m)) a b

let mkMinimalTy (g: TcGlobals) (tcref: TyconRef) = 
    if tcref.Deref.IsExceptionDecl then [], g.exn_ty 
    else generalizeTyconRef tcref

// check for nulls
let mkBindNullComparison g m thise thate expr = 
    let expr = mkNonNullCond g m g.int_ty thate expr (mkOne g m) 
    let expr = mkNonNullCond g m g.int_ty thise expr (mkNonNullCond g m g.int_ty thate (mkMinusOne g m) (mkZero g m) )
    expr

let mkBindThisNullEquals g m thise thate expr = 
    let expr = mkNonNullCond g m g.bool_ty thise expr (mkNonNullCond g m g.int_ty thate (mkFalse g m) (mkTrue g m) )
    expr

let mkBindThatNullEquals g m thise thate expr = 
    let expr = mkNonNullCond g m g.bool_ty thate expr (mkFalse g m) 
    let expr = mkBindThisNullEquals g m thise thate expr
    expr
    
let mkBindNullHash g m thise expr = 
    let expr = mkNonNullCond g m g.int_ty thise expr (mkZero g m)
    expr

/// Build the comparison implementation for a record type 
let mkRecdCompare g tcref (tycon: Tycon) = 
    let m = tycon.Range 
    let fields = tycon.AllInstanceFieldsAsList 
    let tinst, ty = mkMinimalTy g tcref
    let thisv, thataddrv, thise, thataddre = mkThisVarThatVar g m ty 
    let compe = mkILCallGetComparer g m
    let mkTest (fspec: RecdField) = 
        let fty = fspec.FormalType 
        let fref = tcref.MakeNestedRecdFieldRef fspec 
        let m = fref.Range 
        mkCallGenericComparisonWithComparerOuter g m fty
          compe
          (mkRecdFieldGetViaExprAddr (thise, fref, tinst, m))
          (mkRecdFieldGetViaExprAddr (thataddre, fref, tinst, m)) 
    let expr = mkCompareTestConjuncts g m (List.map mkTest fields) 

    let expr = if tycon.IsStructOrEnumTycon then expr else mkBindNullComparison g m thise thataddre expr

    let thatv, expr = mkThatVarBind g m ty thataddrv expr
    thisv, thatv, expr

/// Build the comparison implementation for a record type when parameterized by a comparer
let mkRecdCompareWithComparer g tcref (tycon: Tycon) (_thisv, thise) (_, thate) compe = 
    let m = tycon.Range 
    let fields = tycon.AllInstanceFieldsAsList
    let tinst, ty = mkMinimalTy g tcref
    let tcv, tce = mkCompGenLocal m "objTemp" ty    // let tcv = thate
    let thataddrv, thataddre = mkThatAddrLocal g m ty      // let thataddrv = &tcv, if a struct
    
    let mkTest (fspec: RecdField) = 
        let fty = fspec.FormalType 
        let fref = tcref.MakeNestedRecdFieldRef fspec 
        let m = fref.Range 
        mkCallGenericComparisonWithComparerOuter g m fty
          compe
          (mkRecdFieldGetViaExprAddr (thise, fref, tinst, m))
          (mkRecdFieldGetViaExprAddr (thataddre, fref, tinst, m))
    let expr = mkCompareTestConjuncts g m (List.map mkTest fields) 

    let expr = if tycon.IsStructOrEnumTycon then expr else mkBindNullComparison g m thise thate expr

    let expr = mkBindThatAddr g m ty thataddrv tcv tce expr
    // will be optimized away if not necessary
    let expr = mkCompGenLet m tcv thate expr
    expr    

/// Build the .Equals(that) equality implementation wrapper for a record type 
let mkRecdEquality g tcref (tycon: Tycon) = 
    let m = tycon.Range
    let fields = tycon.AllInstanceFieldsAsList 
    let tinst, ty = mkMinimalTy g tcref
    let thisv, thataddrv, thise, thataddre = mkThisVarThatVar g m ty 
    let mkTest (fspec: RecdField) = 
        let fty = fspec.FormalType 
        let fref = tcref.MakeNestedRecdFieldRef fspec 
        let m = fref.Range 
        mkCallGenericEqualityEROuter g m fty
          (mkRecdFieldGetViaExprAddr (thise, fref, tinst, m))
          (mkRecdFieldGetViaExprAddr (thataddre, fref, tinst, m)) 
    let expr = mkEqualsTestConjuncts g m (List.map mkTest fields) 

    let expr = if tycon.IsStructOrEnumTycon then expr else mkBindThatNullEquals g m thise thataddre expr

    let thatv, expr = mkThatVarBind g m ty thataddrv expr
    thisv, thatv, expr
    
/// Build the equality implementation for a record type when parameterized by a comparer
let mkRecdEqualityWithComparer g tcref (tycon: Tycon) (_thisv, thise) thatobje (thatv, thate) compe =
    let m = tycon.Range
    let fields = tycon.AllInstanceFieldsAsList
    let tinst, ty = mkMinimalTy g tcref
    let thataddrv, thataddre = mkThatAddrLocal g m ty
    
    let mkTest (fspec: RecdField) =
        let fty = fspec.FormalType
        let fref = tcref.MakeNestedRecdFieldRef fspec
        let m = fref.Range
        
        mkCallGenericEqualityWithComparerOuter g m fty
            compe
            (mkRecdFieldGetViaExprAddr (thise, fref, tinst, m))
            (mkRecdFieldGetViaExprAddr (thataddre, fref, tinst, m))
    let expr = mkEqualsTestConjuncts g m (List.map mkTest fields)

    let expr = mkBindThatAddr g m ty thataddrv thatv thate expr
    // will be optimized away if not necessary
    let expr = mkIsInstConditional g m ty thatobje thatv expr (mkFalse g m)
    let expr = if tycon.IsStructOrEnumTycon then expr else mkBindThisNullEquals g m thise thatobje expr

    expr
        
/// Build the equality implementation for an exception definition
let mkExnEquality (g: TcGlobals) exnref (exnc: Tycon) = 
    let m = exnc.Range 
    let thatv, thate = mkCompGenLocal m "obj" g.exn_ty  
    let thisv, thise = mkThisVar g m g.exn_ty  
    let mkTest i (rfield: RecdField) = 
        let fty = rfield.FormalType
        mkCallGenericEqualityEROuter g m fty
          (mkExnCaseFieldGet(thise, exnref, i, m))
          (mkExnCaseFieldGet(thate, exnref, i, m)) 
    let expr = mkEqualsTestConjuncts g m (List.mapi mkTest exnc.AllInstanceFieldsAsList) 
    let expr =
        let mbuilder = new MatchBuilder(DebugPointAtBinding.NoneAtInvisible, m ) 
        let cases = 
            [ mkCase(DecisionTreeTest.IsInst(g.exn_ty, mkAppTy exnref []), 
                     mbuilder.AddResultTarget(expr, DebugPointForTarget.No)) ]
        let dflt = Some(mbuilder.AddResultTarget(mkFalse g m, DebugPointForTarget.No))
        let dtree = TDSwitch(thate, cases, dflt, m)
        mbuilder.Close(dtree, m, g.bool_ty)

    let expr = mkBindThatNullEquals g m thise thate expr
    thisv, thatv, expr
    
/// Build the equality implementation for an exception definition when parameterized by a comparer
let mkExnEqualityWithComparer g exnref (exnc: Tycon) (_thisv, thise) thatobje (thatv, thate) compe = 
    let m = exnc.Range
    let thataddrv, thataddre =  mkThatAddrLocal g m g.exn_ty
    let mkTest i (rfield: RecdField) = 
        let fty = rfield.FormalType
        mkCallGenericEqualityWithComparerOuter g m fty
          compe
          (mkExnCaseFieldGet(thise, exnref, i, m))
          (mkExnCaseFieldGet(thataddre, exnref, i, m))
    let expr = mkEqualsTestConjuncts g m (List.mapi mkTest exnc.AllInstanceFieldsAsList) 
    let expr =
        let mbuilder = new MatchBuilder(DebugPointAtBinding.NoneAtInvisible, m ) 
        let cases =
            [ mkCase(DecisionTreeTest.IsInst(g.exn_ty, mkAppTy exnref []), 
                     mbuilder.AddResultTarget(expr, DebugPointForTarget.No)) ]
        let dflt = mbuilder.AddResultTarget(mkFalse g m, DebugPointForTarget.No)
        let dtree = TDSwitch(thate, cases, Some dflt, m)
        mbuilder.Close(dtree, m, g.bool_ty)
    let expr = mkBindThatAddr g m g.exn_ty thataddrv thatv thate expr
    let expr = mkIsInstConditional g m g.exn_ty thatobje thatv expr (mkFalse g m)
    let expr = if exnc.IsStructOrEnumTycon then expr else mkBindThisNullEquals g m thise thatobje expr
    expr

/// Build the comparison implementation for a union type
let mkUnionCompare g tcref (tycon: Tycon) = 
    let m = tycon.Range 
    let ucases = tycon.UnionCasesAsList 
    let tinst, ty = mkMinimalTy g tcref
    let thisv, thataddrv, thise, thataddre = mkThisVarThatVar g m ty 
    let thistagv, thistage = mkCompGenLocal m "thisTag" g.int_ty  
    let thattagv, thattage = mkCompGenLocal m "thatTag" g.int_ty 
    let compe = mkILCallGetComparer g m

    let expr = 
        let mbuilder = new MatchBuilder(DebugPointAtBinding.NoneAtInvisible, m ) 
        let mkCase ucase =
            let cref = tcref.MakeNestedUnionCaseRef ucase 
            let m = cref.Range 
            let rfields = ucase.RecdFields 
            if isNil rfields then None else
            let mkTest thise thataddre j (argty: RecdField) = 
                mkCallGenericComparisonWithComparerOuter g m argty.FormalType
                  compe
                  (mkUnionCaseFieldGetProvenViaExprAddr (thise, cref, tinst, j, m))
                  (mkUnionCaseFieldGetProvenViaExprAddr (thataddre, cref, tinst, j, m)) 
            let test = 
                if cref.Tycon.IsStructOrEnumTycon then 
                    mkCompareTestConjuncts g m (List.mapi (mkTest thise thataddre) rfields)
                else
                    let thisucv, thisucve = mkCompGenLocal m "thisCast" (mkProvenUnionCaseTy cref tinst)
                    let thatucv, thatucve = mkCompGenLocal m "objCast" (mkProvenUnionCaseTy cref tinst)
                    mkCompGenLet m thisucv (mkUnionCaseProof (thise, cref, tinst, m))
                        (mkCompGenLet m thatucv (mkUnionCaseProof (thataddre, cref, tinst, m))
                            (mkCompareTestConjuncts g m (List.mapi (mkTest thisucve thatucve) rfields)))
            Some (mkCase(DecisionTreeTest.UnionCase(cref, tinst), mbuilder.AddResultTarget(test, DebugPointForTarget.No)))
        
        let nullary, nonNullary = List.partition Option.isNone (List.map mkCase ucases)  
        if isNil nonNullary then mkZero g m else 
        let cases = nonNullary |> List.map (function (Some c) -> c | None -> failwith "mkUnionCompare")
        let dflt = if isNil nullary then None else Some (mbuilder.AddResultTarget(mkZero g m, DebugPointForTarget.No))
        let dtree = TDSwitch(thise, cases, dflt, m) 
        mbuilder.Close(dtree, m, g.int_ty)

    let expr = 
        if ucases.Length = 1 then expr else
        let tagsEqTested = 
            mkCond DebugPointAtBinding.NoneAtSticky DebugPointForTarget.No m g.int_ty  
              (mkILAsmCeq g m thistage thattage)
              expr
              (mkAsmExpr ([ IL.AI_sub  ], [], [thistage; thattage], [g.int_ty], m))in 
        mkCompGenLet m thistagv
          (mkUnionCaseTagGetViaExprAddr (thise, tcref, tinst, m))
          (mkCompGenLet m thattagv
               (mkUnionCaseTagGetViaExprAddr (thataddre, tcref, tinst, m))
               tagsEqTested) 

    let expr = if tycon.IsStructOrEnumTycon then expr else mkBindNullComparison g m thise thataddre expr
    let thatv, expr = mkThatVarBind g m ty thataddrv expr
    thisv, thatv, expr


/// Build the comparison implementation for a union type when parameterized by a comparer
let mkUnionCompareWithComparer g tcref (tycon: Tycon) (_thisv, thise) (_thatobjv, thatcaste) compe = 
    let m = tycon.Range 
    let ucases = tycon.UnionCasesAsList
    let tinst, ty = mkMinimalTy g tcref
    let tcv, tce = mkCompGenLocal m "objTemp" ty    // let tcv = (thatobj :?> ty)
    let thataddrvOpt, thataddre = mkThatAddrLocalIfNeeded g m tce ty // let thataddrv = &tcv if struct, otherwise thataddre is just tce
    let thistagv, thistage = mkCompGenLocal m "thisTag" g.int_ty  
    let thattagv, thattage = mkCompGenLocal m "thatTag" g.int_ty  

    let expr = 
        let mbuilder = new MatchBuilder(DebugPointAtBinding.NoneAtInvisible, m ) 
        let mkCase ucase =
            let cref = tcref.MakeNestedUnionCaseRef ucase 
            let m = cref.Range 
            let rfields = ucase.RecdFields 
            if isNil rfields then None else

            let mkTest thise thataddre j (argty: RecdField) = 
                mkCallGenericComparisonWithComparerOuter g m argty.FormalType
                  compe
                  (mkUnionCaseFieldGetProvenViaExprAddr (thise, cref, tinst, j, m))
                  (mkUnionCaseFieldGetProvenViaExprAddr (thataddre, cref, tinst, j, m))

            let test = 
                if cref.Tycon.IsStructOrEnumTycon then 
                    mkCompareTestConjuncts g m (List.mapi (mkTest thise thataddre) rfields)
                else
                    let thisucv, thisucve = mkCompGenLocal m "thisCastu" (mkProvenUnionCaseTy cref tinst)
                    let thatucv, thatucve = mkCompGenLocal m "thatCastu" (mkProvenUnionCaseTy cref tinst)
                    mkCompGenLet m thisucv (mkUnionCaseProof (thise, cref, tinst, m))
                        (mkCompGenLet m thatucv (mkUnionCaseProof (thataddre, cref, tinst, m))
                            (mkCompareTestConjuncts g m (List.mapi (mkTest thisucve thatucve) rfields)))

            Some (mkCase(DecisionTreeTest.UnionCase(cref, tinst), mbuilder.AddResultTarget(test, DebugPointForTarget.No)))
        
        let nullary, nonNullary = List.partition Option.isNone (List.map mkCase ucases)  
        if isNil nonNullary then mkZero g m else 
        let cases = nonNullary |> List.map (function (Some c) -> c | None -> failwith "mkUnionCompare")
        let dflt = if isNil nullary then None else Some (mbuilder.AddResultTarget(mkZero g m, DebugPointForTarget.No))
        let dtree = TDSwitch(thise, cases, dflt, m) 
        mbuilder.Close(dtree, m, g.int_ty)

    let expr = 
        if ucases.Length = 1 then expr else
        let tagsEqTested = 
            mkCond DebugPointAtBinding.NoneAtSticky DebugPointForTarget.No m g.int_ty  
              (mkILAsmCeq g m thistage thattage)
              expr
              (mkAsmExpr ([ IL.AI_sub  ], [], [thistage; thattage], [g.int_ty], m))
        mkCompGenLet m thistagv
          (mkUnionCaseTagGetViaExprAddr (thise, tcref, tinst, m))
          (mkCompGenLet m thattagv
               (mkUnionCaseTagGetViaExprAddr (thataddre, tcref, tinst, m))
               tagsEqTested) 

    let expr = if tycon.IsStructOrEnumTycon then expr else mkBindNullComparison g m thise thatcaste expr
    let expr = mkBindThatAddrIfNeeded m thataddrvOpt tcv expr
    let expr = mkCompGenLet m tcv thatcaste expr
    expr
    
    
/// Build the equality implementation for a union type
let mkUnionEquality g tcref (tycon: Tycon) = 
    let m = tycon.Range 
    let ucases = tycon.UnionCasesAsList 
    let tinst, ty = mkMinimalTy g tcref
    let thisv, thataddrv, thise, thataddre = mkThisVarThatVar g m ty 
    let thistagv, thistage = mkCompGenLocal m "thisTag" g.int_ty  
    let thattagv, thattage = mkCompGenLocal m "thatTag" g.int_ty  

    let expr = 
        let mbuilder = new MatchBuilder(DebugPointAtBinding.NoneAtInvisible, m ) 
        let mkCase ucase =
            let cref = tcref.MakeNestedUnionCaseRef ucase 
            let m = cref.Range 
            let rfields = ucase.RecdFields
            if isNil rfields then None else

            let mkTest thise thataddre j (argty: RecdField) = 
                mkCallGenericEqualityEROuter g m argty.FormalType
                  (mkUnionCaseFieldGetProvenViaExprAddr (thise, cref, tinst, j, m))
                  (mkUnionCaseFieldGetProvenViaExprAddr (thataddre, cref, tinst, j, m)) 

            let test = 
                if cref.Tycon.IsStructOrEnumTycon then 
                    mkEqualsTestConjuncts  g m (List.mapi (mkTest thise thataddre) rfields)
                else
                    let thisucv, thisucve = mkCompGenLocal m "thisCast" (mkProvenUnionCaseTy cref tinst)
                    let thatucv, thatucve = mkCompGenLocal m "objCast" (mkProvenUnionCaseTy cref tinst)
                    mkCompGenLet m thisucv (mkUnionCaseProof (thise, cref, tinst, m))
                        (mkCompGenLet m thatucv (mkUnionCaseProof (thataddre, cref, tinst, m))
                            (mkEqualsTestConjuncts g m (List.mapi (mkTest thisucve thatucve) rfields)))

            Some (mkCase(DecisionTreeTest.UnionCase(cref, tinst), mbuilder.AddResultTarget(test, DebugPointForTarget.No)))
        
        let nullary, nonNullary = List.partition Option.isNone (List.map mkCase ucases)  
        if isNil nonNullary then mkTrue g m else 
        let cases = List.map (function (Some c) -> c | None -> failwith "mkUnionEquality") nonNullary
        let dflt = (if isNil nullary then None else Some (mbuilder.AddResultTarget(mkTrue g m, DebugPointForTarget.No)))
        let dtree = TDSwitch(thise, cases, dflt, m) 
        mbuilder.Close(dtree, m, g.bool_ty)
        
    let expr = 
        if ucases.Length = 1 then expr else
        let tagsEqTested = 
          mkCond DebugPointAtBinding.NoneAtSticky DebugPointForTarget.No m g.bool_ty  
            (mkILAsmCeq g m thistage thattage)
            expr
            (mkFalse g m)

        mkCompGenLet m thistagv
          (mkUnionCaseTagGetViaExprAddr (thise, tcref, tinst, m))
          (mkCompGenLet m thattagv
               (mkUnionCaseTagGetViaExprAddr (thataddre, tcref, tinst, m))
               tagsEqTested) 

    let thatv, expr = mkThatVarBind g m ty thataddrv expr
    let expr = if tycon.IsStructOrEnumTycon then expr else mkBindThatNullEquals g m thise thataddre expr
    thisv, thatv, expr

/// Build the equality implementation for a union type when parameterized by a comparer
let mkUnionEqualityWithComparer g tcref (tycon: Tycon) (_thisv, thise) thatobje (thatv, thate) compe =
    let m = tycon.Range 
    let ucases = tycon.UnionCasesAsList
    let tinst, ty = mkMinimalTy g tcref
    let thistagv, thistage = mkCompGenLocal m "thisTag" g.int_ty  
    let thattagv, thattage = mkCompGenLocal m "thatTag" g.int_ty  
    let thataddrv, thataddre = mkThatAddrLocal g m ty

    let expr = 
        let mbuilder = new MatchBuilder(DebugPointAtBinding.NoneAtInvisible, m ) 
        let mkCase ucase =
            let cref = tcref.MakeNestedUnionCaseRef ucase 
            let m = cref.Range 

            let rfields = ucase.RecdFields
            if isNil rfields then None else

            let mkTest thise thataddre j (argty: RecdField) = 
              mkCallGenericEqualityWithComparerOuter g m argty.FormalType
                compe
                (mkUnionCaseFieldGetProvenViaExprAddr (thise, cref, tinst, j, m))
                (mkUnionCaseFieldGetProvenViaExprAddr (thataddre, cref, tinst, j, m))

            let test = 
                if cref.Tycon.IsStructOrEnumTycon then 
                    mkEqualsTestConjuncts g m (List.mapi (mkTest thise thataddre) rfields)
                else
                    let thisucv, thisucve = mkCompGenLocal m "thisCastu" (mkProvenUnionCaseTy cref tinst)
                    let thatucv, thatucve = mkCompGenLocal m "thatCastu" (mkProvenUnionCaseTy cref tinst)

                    mkCompGenLet m thisucv (mkUnionCaseProof (thise, cref, tinst, m))
                        (mkCompGenLet m thatucv (mkUnionCaseProof (thataddre, cref, tinst, m))
                            (mkEqualsTestConjuncts g m (List.mapi (mkTest thisucve thatucve) rfields)))

            Some (mkCase(DecisionTreeTest.UnionCase(cref, tinst), mbuilder.AddResultTarget (test, DebugPointForTarget.No)))
        
        let nullary, nonNullary = List.partition Option.isNone (List.map mkCase ucases)  
        if isNil nonNullary then mkTrue g m else 
        let cases = List.map (function (Some c) -> c | None -> failwith "mkUnionEquality") nonNullary
        let dflt = if isNil nullary then None else Some (mbuilder.AddResultTarget(mkTrue g m, DebugPointForTarget.No))
        let dtree = TDSwitch(thise, cases, dflt, m) 
        mbuilder.Close(dtree, m, g.bool_ty)
        
    let expr = 
        if ucases.Length = 1 then expr else
        let tagsEqTested = 
          mkCond DebugPointAtBinding.NoneAtSticky DebugPointForTarget.No m g.bool_ty  
            (mkILAsmCeq g m thistage thattage)
            expr
            (mkFalse g m)

        mkCompGenLet m thistagv
          (mkUnionCaseTagGetViaExprAddr (thise, tcref, tinst, m))
          (mkCompGenLet m thattagv
               (mkUnionCaseTagGetViaExprAddr (thataddre, tcref, tinst, m))
               tagsEqTested) 
    let expr = mkBindThatAddr g m ty thataddrv thatv thate expr
    let expr = mkIsInstConditional g m ty thatobje thatv expr (mkFalse g m)
    let expr = if tycon.IsStructOrEnumTycon then expr else mkBindThisNullEquals g m thise thatobje expr
    expr

//-------------------------------------------------------------------------
// Build hashing functions for union, record and exception types.
// Hashing functions must respect the "=" and comparison operators.
//------------------------------------------------------------------------- 

/// Structural hash implementation for record types when parameterized by a comparer 
let mkRecdHashWithComparer g tcref (tycon: Tycon) compe = 
    let m = tycon.Range 
    let fields = tycon.AllInstanceFieldsAsList
    let tinst, ty = mkMinimalTy g tcref
    let thisv, thise = mkThisVar g m ty
    let mkFieldHash (fspec: RecdField) = 
        let fty = fspec.FormalType
        let fref = tcref.MakeNestedRecdFieldRef fspec 
        let m = fref.Range 
        let e = mkRecdFieldGetViaExprAddr (thise, fref, tinst, m)
        
        mkCallGenericHashWithComparerOuter g m fty compe e
            
    let accv, acce = mkMutableCompGenLocal m "i" g.int_ty                  
    let stmt = mkCombineHashGenerators g m (List.map mkFieldHash fields) (mkLocalValRef accv) acce
    let expr = mkCompGenLet m accv (mkZero g m) stmt 
    let expr = if tycon.IsStructOrEnumTycon then expr else mkBindNullHash g m thise expr
    thisv, expr

/// Structural hash implementation for exception types when parameterized by a comparer
let mkExnHashWithComparer g exnref (exnc: Tycon) compe = 
    let m = exnc.Range
    let thisv, thise = mkThisVar g m g.exn_ty
    
    let mkHash i (rfield: RecdField) = 
        let fty = rfield.FormalType
        let e = mkExnCaseFieldGet(thise, exnref, i, m)
        
        mkCallGenericHashWithComparerOuter g m fty compe e
       
    let accv, acce = mkMutableCompGenLocal m "i" g.int_ty                  
    let stmt = mkCombineHashGenerators g m (List.mapi mkHash exnc.AllInstanceFieldsAsList) (mkLocalValRef accv) acce
    let expr = mkCompGenLet m accv (mkZero g m) stmt 
    let expr = mkBindNullHash g m thise expr
    thisv, expr

/// Structural hash implementation for union types when parameterized by a comparer   
let mkUnionHashWithComparer g tcref (tycon: Tycon) compe =
    let m = tycon.Range
    let ucases = tycon.UnionCasesAsList
    let tinst, ty = mkMinimalTy g tcref
    let thisv, thise = mkThisVar g m ty
    let mbuilder = new MatchBuilder(DebugPointAtBinding.NoneAtInvisible, m ) 
    let accv, acce = mkMutableCompGenLocal m "i" g.int_ty                  
    let mkCase i ucase1 = 
        let c1ref = tcref.MakeNestedUnionCaseRef ucase1 
        let m = c1ref.Range 
        if ucase1.IsNullary then None 
        else
            let mkHash thise j (rfield: RecdField) =  
                let fty = rfield.FormalType
                let e = mkUnionCaseFieldGetProvenViaExprAddr (thise, c1ref, tinst, j, m)
                mkCallGenericHashWithComparerOuter g m fty compe e

            let test =       
                if tycon.IsStructOrEnumTycon then 
                    mkCompGenSequential m 
                        (mkValSet m (mkLocalValRef accv) (mkInt g m i)) 
                        (mkCombineHashGenerators g m (List.mapi (mkHash thise) ucase1.RecdFields) (mkLocalValRef accv) acce)
                else
                    let ucv, ucve = mkCompGenLocal m "unionCase" (mkProvenUnionCaseTy c1ref tinst)
                    mkCompGenLet m ucv
                        (mkUnionCaseProof (thise, c1ref, tinst, m))
                        (mkCompGenSequential m 
                            (mkValSet m (mkLocalValRef accv) (mkInt g m i)) 
                            (mkCombineHashGenerators g m (List.mapi (mkHash ucve) ucase1.RecdFields) (mkLocalValRef accv) acce))
            Some(mkCase(DecisionTreeTest.UnionCase(c1ref, tinst), mbuilder.AddResultTarget(test, DebugPointForTarget.No)))

    let nullary, nonNullary = ucases
                             |> List.mapi mkCase
                             |> List.partition (fun i -> i.IsNone)
    let cases = nonNullary |> List.map (function (Some c) -> c | None -> failwith "mkUnionHash")
    let dflt = if isNil nullary then None 
               else 
                   let tag = mkUnionCaseTagGetViaExprAddr (thise, tcref, tinst, m)
                   Some(mbuilder.AddResultTarget(tag, DebugPointForTarget.No))
    let dtree = TDSwitch(thise, cases, dflt, m)
    let stmt = mbuilder.Close(dtree, m, g.int_ty)
    let expr = mkCompGenLet m accv (mkZero g m) stmt 
    let expr = if tycon.IsStructOrEnumTycon then expr else mkBindNullHash g m thise expr
    thisv, expr


//-------------------------------------------------------------------------
// The predicate that determines which types implement the 
// pre-baked IStructuralHash and IComparable semantics associated with F#
// types.  Note abstract types are not _known_ to implement these interfaces, 
// though the interfaces may be discoverable via type tests.
//------------------------------------------------------------------------- 

let isNominalExnc (exnc: Tycon) = 
    match exnc.ExceptionInfo with 
    | TExnAbbrevRepr _ | TExnNone | TExnAsmRepr _ -> false
    | TExnFresh _ -> true

let isTrueFSharpStructTycon _g (tycon: Tycon) = 
    (tycon.IsFSharpStructOrEnumTycon  && not tycon.IsFSharpEnumTycon)

let canBeAugmentedWithEquals g (tycon: Tycon) = 
    tycon.IsUnionTycon ||
    tycon.IsRecordTycon ||
    (tycon.IsExceptionDecl && isNominalExnc tycon) ||
    isTrueFSharpStructTycon g tycon

let canBeAugmentedWithCompare g (tycon: Tycon) = 
    tycon.IsUnionTycon ||
    tycon.IsRecordTycon ||
    isTrueFSharpStructTycon g tycon

let getAugmentationAttribs g (tycon: Tycon) = 
    canBeAugmentedWithEquals g tycon, 
    canBeAugmentedWithCompare g tycon, 
    TryFindFSharpBoolAttribute g g.attrib_NoEqualityAttribute tycon.Attribs, 
    TryFindFSharpBoolAttribute g g.attrib_CustomEqualityAttribute tycon.Attribs, 
    TryFindFSharpBoolAttribute g g.attrib_ReferenceEqualityAttribute tycon.Attribs, 
    TryFindFSharpBoolAttribute g g.attrib_StructuralEqualityAttribute tycon.Attribs, 
    TryFindFSharpBoolAttribute g g.attrib_NoComparisonAttribute tycon.Attribs, 
    TryFindFSharpBoolAttribute g g.attrib_CustomComparisonAttribute tycon.Attribs, 
    TryFindFSharpBoolAttribute g g.attrib_StructuralComparisonAttribute tycon.Attribs 

let CheckAugmentationAttribs isImplementation g amap (tycon: Tycon) = 
    let m = tycon.Range
    let attribs = getAugmentationAttribs g tycon
    match attribs with 
    
    // THESE ARE THE LEGITIMATE CASES 

    // [< >] on anything
    | _, _, None, None, None, None, None, None, None

    // [<CustomEquality; CustomComparison>]  on union/record/struct
    | true, _, None, Some true, None, None, None, Some true, None

    // [<CustomEquality; NoComparison>]  on union/record/struct
    | true, _, None, Some true, None, None, Some true, None, None -> 
        ()

    // [<ReferenceEquality; NoComparison>]  on union/record/struct
    | true, _, None, None, Some true, None, Some true, None, None

    // [<ReferenceEquality>] on union/record/struct
    | true, _, None, None, Some true, None, None, None, None ->
        if isTrueFSharpStructTycon g tycon then 
            errorR(Error(FSComp.SR.augNoRefEqualsOnStruct(), m))
        else ()

    // [<StructuralEquality; StructuralComparison>]  on union/record/struct
    | true, true, None, None, None, Some true, None, None, Some true 

    // [<StructuralEquality; NoComparison>] 
    | true, _, None, None, None, Some true, Some true, None, None

    // [<StructuralEquality; CustomComparison>] 
    | true, _, None, None, None, Some true, None, Some true, None

    // [<NoComparison>] on anything
    | _, _, None, None, None, None, Some true, None, None 

    // [<NoEquality; NoComparison>] on anything
    | _, _, Some true, None, None, None, Some true, None, None ->
        () 

    // THESE ARE THE ERROR CASES

    // [<NoEquality; ...>] 
    | _, _, Some true, _, _, _, None, _, _ ->
        errorR(Error(FSComp.SR.augNoEqualityNeedsNoComparison(), m))

    // [<StructuralComparison(_)>] 
    | true, true, _, _, _, None, _, _, Some true ->
        errorR(Error(FSComp.SR.augStructCompNeedsStructEquality(), m))
    // [<StructuralEquality(_)>] 
    | true, _, _, _, _, Some true, None, _, None ->
        errorR(Error(FSComp.SR.augStructEqNeedsNoCompOrStructComp(), m))

    // [<StructuralEquality(_)>] 
    | true, _, _, Some true, _, _, None, None, _ ->
        errorR(Error(FSComp.SR.augCustomEqNeedsNoCompOrCustomComp(), m))

    // [<ReferenceEquality; StructuralEquality>] 
    | true, _, _, _, Some true, Some true, _, _, _

    // [<ReferenceEquality; StructuralComparison(_) >] 
    | true, _, _, _, Some true, _, _, _, Some true -> 
        errorR(Error(FSComp.SR.augTypeCantHaveRefEqAndStructAttrs(), m))

    // non augmented type, [<ReferenceEquality; ... >] 
    // non augmented type, [<StructuralEquality; ... >] 
    // non augmented type, [<StructuralComparison(_); ... >] 
    | false, _, _, _, Some true, _, _, _, _
    | false, _, _, _, _, Some true, _, _, _ 
    | false, _, _, _, _, _, _, _, Some true  ->
        errorR(Error(FSComp.SR.augOnlyCertainTypesCanHaveAttrs(), m))
    // All other cases
    | _  -> 
        errorR(Error(FSComp.SR.augInvalidAttrs(), m))
    
    let hasNominalInterface tcref =
        let ty = generalizedTyconRef (mkLocalTyconRef tycon)
        ExistsHeadTypeInEntireHierarchy g amap tycon.Range ty tcref

    let hasExplicitICompare = 
        hasNominalInterface g.tcref_System_IStructuralComparable || 
        hasNominalInterface g.tcref_System_IComparable

    let hasExplicitIGenericCompare = 
        hasNominalInterface g.system_GenericIComparable_tcref 

    let hasExplicitEquals = 
        tycon.HasOverride g "Equals" [g.obj_ty] ||
        hasNominalInterface g.tcref_System_IStructuralEquatable

    let hasExplicitGenericEquals = 
        hasNominalInterface g.system_GenericIEquatable_tcref

    match attribs with 
    // [<NoEquality>] + any equality semantics
    | _, _, Some true, _, _, _, _, _, _ when (hasExplicitEquals || hasExplicitGenericEquals) -> 
        warning(Error(FSComp.SR.augNoEqNeedsNoObjEquals(), m))
    // [<NoComparison>] + any comparison semantics
    | _, _, _, _, _, _, Some true, _, _ when (hasExplicitICompare || hasExplicitIGenericCompare) -> 
        warning(Error(FSComp.SR.augNoCompCantImpIComp(), m))

    // [<CustomEquality>] + no explicit override Object.Equals  + no explicit IStructuralEquatable
    | _, _, _, Some true, _, _, _, _, _ when isImplementation && not hasExplicitEquals && not hasExplicitGenericEquals-> 
        errorR(Error(FSComp.SR.augCustomEqNeedsObjEquals(), m))
    // [<CustomComparison>] + no explicit IComparable + no explicit IStructuralComparable
    | _, _, _, _, _, _, _, Some true, _ when isImplementation && not hasExplicitICompare && not hasExplicitIGenericCompare -> 
        errorR(Error(FSComp.SR.augCustomCompareNeedsIComp(), m))

    // [<ReferenceEquality>] + any equality semantics
    | _, _, _, _, Some true, _, _, _, _ when (hasExplicitEquals || hasExplicitIGenericCompare) -> 
        errorR(Error(FSComp.SR.augRefEqCantHaveObjEquals(), m))

    | _ -> 
        ()

let TyconIsCandidateForAugmentationWithCompare (g: TcGlobals) (tycon: Tycon) = 
    // This type gets defined in prim-types, before we can add attributes to F# type definitions
    let isUnit = g.compilingFslib && tycon.DisplayName = "Unit"
    not isUnit && 
    not (TyconRefHasAttribute g tycon.Range g.attrib_IsByRefLikeAttribute (mkLocalTyconRef tycon)) &&
    match getAugmentationAttribs g tycon with 
    // [< >] 
    | true, true, None, None, None, None, None, None, None
    // [<StructuralEquality; StructuralComparison>] 
    | true, true, None, None, None, Some true, None, None, Some true 
    // [<StructuralComparison>] 
    | true, true, None, None, None, None, None, None, Some true -> true
    // other cases 
    | _ -> false

let TyconIsCandidateForAugmentationWithEquals (g: TcGlobals) (tycon: Tycon) = 
    // This type gets defined in prim-types, before we can add attributes to F# type definitions
    let isUnit = g.compilingFslib && tycon.DisplayName = "Unit"
    not isUnit && 
    not (TyconRefHasAttribute g tycon.Range g.attrib_IsByRefLikeAttribute (mkLocalTyconRef tycon)) &&

    match getAugmentationAttribs g tycon with 
    // [< >] 
    | true, _, None, None, None, None, _, _, _
    // [<StructuralEquality; _ >] 
    // [<StructuralEquality; StructuralComparison>] 
    | true, _, None, None, None, Some true, _, _, _ -> true
    // other cases 
    | _ -> false

let TyconIsCandidateForAugmentationWithHash g tycon = TyconIsCandidateForAugmentationWithEquals g tycon
      
//-------------------------------------------------------------------------
// Make values that represent the implementations of the 
// IComparable semantics associated with F# types.  
//------------------------------------------------------------------------- 

let slotImplMethod (final, c, slotsig) : ValMemberInfo = 
  { ImplementedSlotSigs=[slotsig]
    MemberFlags=
        { IsInstance=true 
          IsDispatchSlot=false
          IsFinal=final
          IsOverrideOrExplicitImpl=true
          MemberKind=SynMemberKind.Member}
    IsImplemented=false
    ApparentEnclosingEntity=c} 

let nonVirtualMethod mk c : ValMemberInfo =
  { ImplementedSlotSigs=[]
    MemberFlags={ IsInstance=true 
                  IsDispatchSlot=false
                  IsFinal=false
                  IsOverrideOrExplicitImpl=false
                  MemberKind=mk}
    IsImplemented=false
    ApparentEnclosingEntity=c} 

let unitArg = ValReprInfo.unitArgData
let unaryArg = [ ValReprInfo.unnamedTopArg ]
let tupArg = [ [ ValReprInfo.unnamedTopArg1; ValReprInfo.unnamedTopArg1 ] ]
let mkValSpec g (tcref: TyconRef) tmty vis slotsig (methn: string) ty argData =
    let m = tcref.Range 
    let tps = tcref.Typars m
    let membInfo = 
        match slotsig with 
        | None ->
            let mk =
                if methn.StartsWith("get_", System.StringComparison.Ordinal) then SynMemberKind.PropertyGet
                else SynMemberKind.Member
            nonVirtualMethod mk tcref
        | Some slotsig -> 
            let final = isUnionTy g tmty || isRecdTy g tmty || isStructTy g tmty 
            slotImplMethod(final, tcref, slotsig)
    let inl = ValInline.Optional
    let args = ValReprInfo.unnamedTopArg :: argData
    let topValInfo = Some (ValReprInfo (ValReprInfo.InferTyparInfo tps, args, ValReprInfo.unnamedRetVal)) 
    Construct.NewVal (methn, m, None, ty, Immutable, true, topValInfo, vis, ValNotInRecScope, Some membInfo, NormalVal, [], inl, XmlDoc.Empty, true, false, false, false, false, false, None, Parent tcref) 

let MakeValsForCompareAugmentation g (tcref: TyconRef) = 
    let m = tcref.Range
    let _, tmty = mkMinimalTy g tcref
    let tps = tcref.Typars m
    let vis = tcref.TypeReprAccessibility

    mkValSpec g tcref tmty vis (Some(mkIComparableCompareToSlotSig g)) "CompareTo" (tps +-> (mkCompareObjTy g tmty)) unaryArg, 
    mkValSpec g tcref tmty vis (Some(mkGenericIComparableCompareToSlotSig g tmty)) "CompareTo" (tps +-> (mkCompareTy g tmty)) unaryArg
    
let MakeValsForCompareWithComparerAugmentation g (tcref: TyconRef) =
    let m = tcref.Range
    let _, tmty = mkMinimalTy g tcref
    let tps = tcref.Typars m
    let vis = tcref.TypeReprAccessibility
    mkValSpec g tcref tmty vis (Some(mkIStructuralComparableCompareToSlotSig g)) "CompareTo" (tps +-> (mkCompareWithComparerTy g tmty)) tupArg

let MakeValsForEqualsAugmentation g (tcref: TyconRef) = 
    let m = tcref.Range
    let _, tmty = mkMinimalTy g tcref
    let vis = tcref.TypeReprAccessibility
    let tps = tcref.Typars m

    let objEqualsVal = mkValSpec g tcref tmty vis (Some(mkEqualsSlotSig g)) "Equals" (tps +-> (mkEqualsObjTy g tmty)) unaryArg
    let nocEqualsVal = mkValSpec g tcref tmty vis (if tcref.Deref.IsExceptionDecl then None else Some(mkGenericIEquatableEqualsSlotSig g tmty)) "Equals" (tps +-> (mkEqualsTy g tmty)) unaryArg
    objEqualsVal, nocEqualsVal
    
let MakeValsForEqualityWithComparerAugmentation g (tcref: TyconRef) =
    let _, tmty = mkMinimalTy g tcref
    let vis = tcref.TypeReprAccessibility
    let tps = tcref.Typars tcref.Range
    let objGetHashCodeVal = mkValSpec g tcref tmty vis (Some(mkGetHashCodeSlotSig g)) "GetHashCode" (tps +-> (mkHashTy g tmty)) unitArg
    let withcGetHashCodeVal = mkValSpec g tcref tmty vis (Some(mkIStructuralEquatableGetHashCodeSlotSig g)) "GetHashCode" (tps +-> (mkHashWithComparerTy g tmty)) unaryArg
    let withcEqualsVal  = mkValSpec g tcref tmty vis (Some(mkIStructuralEquatableEqualsSlotSig g)) "Equals" (tps +-> (mkEqualsWithComparerTy g tmty)) tupArg
    objGetHashCodeVal, withcGetHashCodeVal, withcEqualsVal

let MakeBindingsForCompareAugmentation g (tycon: Tycon) = 
    let tcref = mkLocalTyconRef tycon 
    let m = tycon.Range
    let tps = tycon.Typars m
    let mkCompare comparef =
        match tycon.GeneratedCompareToValues with 
        | None ->  []
        | Some (vref1, vref2) -> 
            let vspec1 = vref1.Deref
            let vspec2 = vref2.Deref
            (* this is the body of the override *)
            let rhs1 = 
              let tinst, ty = mkMinimalTy g tcref
              
              let thisv, thise = mkThisVar g m ty  
              let thatobjv, thatobje = mkCompGenLocal m "obj" g.obj_ty  
              let comparee = 
                  if isUnitTy g ty then mkZero g m else
                  let thate = mkCoerceExpr (thatobje, ty, m, g.obj_ty)

                  mkApps g ((exprForValRef m vref2, vref2.Type), (if isNil tinst then [] else [tinst]), [thise;thate], m)
              
              mkLambdas m tps [thisv;thatobjv] (comparee, g.int_ty)  
            let rhs2 = 
              let thisv, thatv, comparee = comparef g tcref tycon 
              mkLambdas m tps [thisv;thatv] (comparee, g.int_ty)  
            [ // This one must come first because it may be inlined into the second
              mkCompGenBind vspec2 rhs2
              mkCompGenBind vspec1 rhs1; ] 
    if tycon.IsUnionTycon then mkCompare mkUnionCompare 
    elif tycon.IsRecordTycon || tycon.IsStructOrEnumTycon then mkCompare mkRecdCompare 
    else []
    
let MakeBindingsForCompareWithComparerAugmentation g (tycon: Tycon) =
    let tcref = mkLocalTyconRef tycon
    let m = tycon.Range
    let tps = tycon.Typars m
    let mkCompare comparef = 
        match tycon.GeneratedCompareToWithComparerValues with
        | None -> []
        | Some vref ->
            let vspec = vref.Deref
            let _, ty = mkMinimalTy g tcref

            let compv, compe = mkCompGenLocal m "comp" g.IComparer_ty

            let thisv, thise = mkThisVar g m ty
            let thatobjv, thatobje = mkCompGenLocal m "obj" g.obj_ty
            let thate = mkCoerceExpr (thatobje, ty, m, g.obj_ty)

            let rhs =
                let comparee = comparef g tcref tycon (thisv, thise) (thatobjv, thate) compe
                let comparee = if isUnitTy g ty then mkZero g m else comparee
                mkMultiLambdas m tps [[thisv];[thatobjv;compv]] (comparee, g.int_ty)
            [mkCompGenBind vspec rhs]
    if tycon.IsUnionTycon then mkCompare mkUnionCompareWithComparer
    elif tycon.IsRecordTycon || tycon.IsStructOrEnumTycon then mkCompare mkRecdCompareWithComparer
    else []    
    
let MakeBindingsForEqualityWithComparerAugmentation (g: TcGlobals) (tycon: Tycon) =
    let tcref = mkLocalTyconRef tycon
    let m = tycon.Range
    let tps = tycon.Typars m
    let mkStructuralEquatable hashf equalsf =
        match tycon.GeneratedHashAndEqualsWithComparerValues with
        | None -> []
        | Some (objGetHashCodeVal, withcGetHashCodeVal, withcEqualsVal) ->
            
            // build the hash rhs
            let withcGetHashCodeExpr =
                let compv, compe = mkCompGenLocal m "comp" g.IEqualityComparer_ty
                let thisv, hashe = hashf g tcref tycon compe
                mkLambdas m tps [thisv;compv] (hashe, g.int_ty)
                
            // build the equals rhs
            let withcEqualsExpr =
                let _tinst, ty = mkMinimalTy g tcref
                let thisv, thise = mkThisVar g m ty
                let thatobjv, thatobje = mkCompGenLocal m "obj" g.obj_ty
                let thatv, thate = mkCompGenLocal m "that" ty  
                let compv, compe = mkCompGenLocal m "comp" g.IEqualityComparer_ty
                let equalse = equalsf g tcref tycon (thisv, thise) thatobje (thatv, thate) compe
                mkMultiLambdas m tps [[thisv];[thatobjv;compv]] (equalse, g.bool_ty)


            let objGetHashCodeExpr = 
                let tinst, ty = mkMinimalTy g tcref
                
                let thisv, thise = mkThisVar g m ty  
                let unitv, _ = mkCompGenLocal m "unitArg" g.unit_ty
                let hashe = 
                    if isUnitTy g ty then mkZero g m else

                    let compe = mkILCallGetEqualityComparer g m
                    mkApps g ((exprForValRef m withcGetHashCodeVal, withcGetHashCodeVal.Type), (if isNil tinst then [] else [tinst]), [thise; compe], m)
                
                mkLambdas m tps [thisv; unitv] (hashe, g.int_ty)  
                  
            [(mkCompGenBind withcGetHashCodeVal.Deref withcGetHashCodeExpr)  
             (mkCompGenBind objGetHashCodeVal.Deref objGetHashCodeExpr)  
             (mkCompGenBind withcEqualsVal.Deref withcEqualsExpr)] 
    if tycon.IsUnionTycon then mkStructuralEquatable mkUnionHashWithComparer mkUnionEqualityWithComparer
    elif (tycon.IsRecordTycon || tycon.IsStructOrEnumTycon) then mkStructuralEquatable mkRecdHashWithComparer mkRecdEqualityWithComparer
    elif tycon.IsExceptionDecl then mkStructuralEquatable mkExnHashWithComparer mkExnEqualityWithComparer
    else []

let MakeBindingsForEqualsAugmentation (g: TcGlobals) (tycon: Tycon) = 
    let tcref = mkLocalTyconRef tycon 
    let m = tycon.Range 
    let tps = tycon.Typars m
    let mkEquals equalsf =
      match tycon.GeneratedHashAndEqualsValues with 
      | None ->  []
      | Some (objEqualsVal, nocEqualsVal) -> 
          // this is the body of the real strongly typed implementation 
          let nocEqualsExpr = 
              let thisv, thatv, equalse = equalsf g tcref tycon 
              mkLambdas m tps [thisv;thatv] (equalse, g.bool_ty)  

          // this is the body of the override 
          let objEqualsExpr = 
            let tinst, ty = mkMinimalTy g tcref
            
            let thisv, thise = mkThisVar g m ty  
            let thatobjv, thatobje = mkCompGenLocal m "obj" g.obj_ty  
            let equalse = 
                if isUnitTy g ty then mkTrue g m else

                let thatv, thate = mkCompGenLocal m "that" ty  
                mkIsInstConditional g m ty thatobje thatv 
                    (mkApps g ((exprForValRef m nocEqualsVal, nocEqualsVal.Type), (if isNil tinst then [] else [tinst]), [thise;thate], m))
                    (mkFalse g m)
            
            mkLambdas m tps [thisv;thatobjv] (equalse, g.bool_ty)  


          [ mkCompGenBind nocEqualsVal.Deref nocEqualsExpr
            mkCompGenBind objEqualsVal.Deref objEqualsExpr   ] 
    if tycon.IsExceptionDecl then mkEquals mkExnEquality 
    elif tycon.IsUnionTycon then mkEquals mkUnionEquality 
    elif tycon.IsRecordTycon || tycon.IsStructOrEnumTycon then mkEquals mkRecdEquality 
    else []

let rec TypeDefinitelyHasEquality g ty =
    let appTy = tryAppTy g ty
    match appTy with
    | ValueSome(tcref,_) when HasFSharpAttribute g g.attrib_NoEqualityAttribute tcref.Attribs ->
        false
    | _ ->
        if isTyparTy g ty && 
             (destTyparTy g ty).Constraints |> List.exists (function TyparConstraint.SupportsEquality _ -> true | _ -> false) then
            true
        else 
            match ty with 
            | SpecialEquatableHeadType g tinst -> 
                tinst |> List.forall (TypeDefinitelyHasEquality g)
            | SpecialNotEquatableHeadType g _ -> 
                false
            | _ -> 
               // The type is equatable because it has Object.Equals(...)
               match appTy with
               | ValueSome(tcref,tinst) ->
                   // Give a good error for structural types excluded from the equality relation because of their fields
                   not (TyconIsCandidateForAugmentationWithEquals g tcref.Deref && Option.isNone tcref.GeneratedHashAndEqualsWithComparerValues) &&
                   // Check the (possibly inferred) structural dependencies
                   (tinst, tcref.TyparsNoRange) 
                   ||> List.lengthsEqAndForall2 (fun ty tp -> not tp.EqualityConditionalOn || TypeDefinitelyHasEquality  g ty)
               | _ -> false

let MakeValsForUnionAugmentation g (tcref: TyconRef) =
    let m = tcref.Range
    let _, tmty = mkMinimalTy g tcref
    let vis = tcref.TypeReprAccessibility
    let tps = tcref.Typars m

    tcref.UnionCasesAsList
    |> List.map (fun uc ->
        mkValSpec g tcref tmty vis None ("get_Is" + uc.CompiledName) (tps +-> (mkIsCaseTy g tmty)) unitArg
    )

let MakeBindingsForUnionAugmentation g (tycon: Tycon) (vals: ValRef list) =
    let tcref = mkLocalTyconRef tycon
    let m = tycon.Range
    let tps = tycon.Typars m
    let tinst, ty = mkMinimalTy g tcref
    let thisv, thise = mkThisVar g m ty
    let unitv, _ = mkCompGenLocal m "unitArg" g.unit_ty

    (tcref.UnionCasesAsRefList, vals)
    ||> List.map2 (fun ucr v ->
        let isdata = mkUnionCaseTest (thise, ucr, tinst, m)
        let expr = mkLambdas m tps [thisv;unitv] (isdata, g.bool_ty)
        mkCompGenBind v.Deref expr
    )