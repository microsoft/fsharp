// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.AbstractIL.ILX.EraseClosures

open Internal.Utilities.Library 
open FSharp.Compiler.AbstractIL.ILX
open FSharp.Compiler.AbstractIL.ILX.Types 
open FSharp.Compiler.AbstractIL.Morphs 
open FSharp.Compiler.AbstractIL.IL 
open FSharp.Compiler.Syntax.PrettyNaming

// -------------------------------------------------------------------- 
// Erase closures and function types
// by compiling down to code pointers, classes etc.
// -------------------------------------------------------------------- 

let notlazy v = Lazy.CreateFromValue v

let rec stripUpTo n test dest x =
    if n = 0 then ([], x) else 
    if test x then 
        let l, r = dest x
        let ls, res = stripUpTo (n-1) test dest r
        (l :: ls), res
    else ([], x)

// -------------------------------------------------------------------- 
// Flags.  These need to match the various classes etc. in the 
// ILX standard library, and the parts 
// of the makefile that select the right standard library for a given
// combination of flags.
//
// Beyond this, the translation inserts classes or value classes for 
// the closure environment.  
// -------------------------------------------------------------------- 

let destTyLambda = function Lambdas_forall(l, r) -> (l, r) | _ -> failwith "no"
let isTyLambda   = function Lambdas_forall _ -> true | _ -> false
let isTyApp      = function Apps_tyapp _ -> true | _ -> false

let stripTyLambdasUpTo n lambdas = stripUpTo n isTyLambda destTyLambda lambdas

// -------------------------------------------------------------------- 
// Three tables related to indirect calling
// -------------------------------------------------------------------- *)

// Supported indirect calling conventions: 
// 1 
// 1_1 
// 1_1_1 
// 1_1_1_1 
// 1_1_1_1_1 
// plus type applications - up to 7 in one step 
// Nb. later code currently takes advantage of the fact that term 
// and type applications are never mixed in a single step. 
let stripSupportedIndirectCall apps =
    match apps with 
    | Apps_app(x, Apps_app(y, Apps_app(z, Apps_app(w, Apps_app(v, rest))))) -> [], [x;y;z;w;v], rest
    | Apps_app(x, Apps_app(y, Apps_app(z, Apps_app(w, rest))))              -> [], [x;y;z;w], rest
    | Apps_app(x, Apps_app(y, Apps_app(z, rest)))                           -> [], [x;y;z], rest
    | Apps_app(x, Apps_app(y, rest))                                        -> [], [x;y], rest
    | Apps_app(x, rest) -> [], [x], rest
    | Apps_tyapp _  -> 
        let maxTyApps =  1
        let tys, rest =  stripUpTo maxTyApps isTyApp destTyFuncApp apps
        tys, [], rest
    | rest -> [], [], rest

// Supported conventions for baking closures: 
// 0 
// 1 
// 1_1 
// 1_1_1 
// 1_1_1_1 
// 1_1_1_1_1 
// plus type applications - up to 7 in one step 
// Nb. later code currently takes advantage of the fact that term 
// and type applications are never mixed in a single step. 
let stripSupportedAbstraction lambdas =
    match lambdas with 
    | Lambdas_lambda(x, Lambdas_lambda(y, Lambdas_lambda(z, Lambdas_lambda(w, Lambdas_lambda(v, rest))))) -> [], [ x;y;z;w;v ], rest
    | Lambdas_lambda(x, Lambdas_lambda(y, Lambdas_lambda(z, Lambdas_lambda(w, rest))))                    -> [], [ x;y;z;w ], rest
    | Lambdas_lambda(x, Lambdas_lambda(y, Lambdas_lambda(z, rest)))                                       -> [], [ x;y;z ], rest
    | Lambdas_lambda(x, Lambdas_lambda(y, rest))                                                          -> [], [ x;y ], rest
    | Lambdas_lambda(x, rest) -> [], [ x ], rest
    | Lambdas_forall _ -> 
        let maxTyApps =  1
        let tys, rest = stripTyLambdasUpTo maxTyApps lambdas
        tys, [ ], rest
    | rest -> [], [ ], rest

// This must correspond to stripSupportedAbstraction
let isSupportedDirectCall apps = 
    match apps with 
    | Apps_app (_, Apps_done _)                                          -> true
    | Apps_app (_, Apps_app (_, Apps_done _))                            -> true
    | Apps_app (_, Apps_app (_, Apps_app (_, Apps_done _)))               -> true
    | Apps_app (_, Apps_app (_, Apps_app (_, Apps_app (_, Apps_done _)))) -> true
    | Apps_tyapp _ -> false
    | _ -> false

// -------------------------------------------------------------------- 
// Prelude for function types.  Only use System.Func for now, prepare
// for more refined types later.
// -------------------------------------------------------------------- 

[<Literal>]
let fsharpCoreNamespace = "Microsoft.FSharp.Core"

let mkFuncTypeRef fsharpCoreAssemblyScopeRef n = 
    if n = 1 then mkILTyRef (fsharpCoreAssemblyScopeRef, fsharpCoreNamespace + ".FSharpFunc`2")
    else mkILNestedTyRef (fsharpCoreAssemblyScopeRef, 
                         [fsharpCoreNamespace + ".OptimizedClosures"], 
                         "FSharpFunc`"+ string (n + 1))
type cenv = 
    {
      ilg:ILGlobals

      tref_Func: ILTypeRef[]

      mkILTyFuncTy: ILType

      addFieldGeneratedAttrs: ILFieldDef -> ILFieldDef

      addFieldNeverAttrs: ILFieldDef -> ILFieldDef

      addMethodGeneratedAttrs: ILMethodDef -> ILMethodDef
    }

    override _.ToString() = "<cenv>"

  
let addMethodGeneratedAttrsToTypeDef cenv (tdef: ILTypeDef) = 
    tdef.With(methods = (tdef.Methods.AsList |> List.map (fun md -> md |> cenv.addMethodGeneratedAttrs) |> mkILMethods))

let newIlxPubCloEnv(ilg, addMethodGeneratedAttrs, addFieldGeneratedAttrs, addFieldNeverAttrs) =
    { ilg = ilg
      tref_Func = Array.init 10 (fun i -> mkFuncTypeRef ilg.fsharpCoreAssemblyScopeRef (i+1))
      mkILTyFuncTy = ILType.Boxed (mkILNonGenericTySpec (mkILTyRef (ilg.fsharpCoreAssemblyScopeRef, fsharpCoreNamespace + ".FSharpTypeFunc"))) 
      addMethodGeneratedAttrs = addMethodGeneratedAttrs
      addFieldGeneratedAttrs = addFieldGeneratedAttrs
      addFieldNeverAttrs = addFieldNeverAttrs }

let mkILTyFuncTy cenv = cenv.mkILTyFuncTy
let mkILFuncTy cenv dty rty = mkILBoxedTy cenv.tref_Func.[0] [dty;rty]
let mkILCurriedFuncTy cenv dtys rty = List.foldBack (mkILFuncTy cenv) dtys rty

let typ_Func cenv (dtys: ILType list) rty = 
    let n = dtys.Length
    let tref = if n <= 10 then cenv.tref_Func.[n-1] else mkFuncTypeRef cenv.ilg.fsharpCoreAssemblyScopeRef n   
    mkILBoxedTy tref (dtys @ [rty])

let rec mkTyOfApps cenv apps =
    match apps with 
    | Apps_tyapp _ -> cenv.mkILTyFuncTy
    | Apps_app (dty, rest) -> mkILFuncTy cenv dty (mkTyOfApps cenv rest)
    | Apps_done rty -> rty

let rec mkTyOfLambdas cenv lam = 
    match lam with 
    | Lambdas_return rty -> rty
    | Lambdas_lambda (d, r) -> mkILFuncTy cenv d.Type (mkTyOfLambdas cenv r)
    | Lambdas_forall _ -> cenv.mkILTyFuncTy

// -------------------------------------------------------------------- 
// Method to call for a particular multi-application
// -------------------------------------------------------------------- 
    
let mkMethSpecForMultiApp cenv (argtys': ILType list, rty) =  
    let n = argtys'.Length
    let formalArgTys = List.mapi (fun i _ ->  ILType.TypeVar (uint16 i)) argtys'
    let formalRetTy = ILType.TypeVar (uint16 n)
    let inst = argtys'@[rty]
    if n = 1  then 
      true, 
       (mkILNonGenericInstanceMethSpecInTy (mkILBoxedTy cenv.tref_Func.[0] inst, "Invoke", formalArgTys, formalRetTy))
    else 
       false, 
       (mkILStaticMethSpecInTy
          (mkILFuncTy cenv inst.[0] inst.[1], 
           "InvokeFast", 
           [mkILCurriedFuncTy cenv formalArgTys formalRetTy]@formalArgTys, 
           formalRetTy, 
           inst.Tail.Tail))

let mkCallBlockForMultiValueApp cenv doTailCall (args', rty') =
    let callvirt, mr = mkMethSpecForMultiApp cenv (args', rty')
    [ ( if callvirt then I_callvirt (doTailCall, mr, None) else I_call (doTailCall, mr, None) ) ]

let mkMethSpecForClosureCall cenv (clospec: IlxClosureSpec) = 
    let tyargsl, argtys, rstruct = stripSupportedAbstraction clospec.FormalLambdas
    if not (isNil tyargsl) then failwith "mkMethSpecForClosureCall: internal error"
    let rty' = mkTyOfLambdas cenv rstruct
    let argtys' = typesOfILParams argtys
    let minst' = clospec.GenericArgs
    (mkILInstanceMethSpecInTy(clospec.ILType, "Invoke", argtys', rty', minst'))


// -------------------------------------------------------------------- 
// Translate instructions....
// -------------------------------------------------------------------- 


let mkLdFreeVar (clospec: IlxClosureSpec) (fv: IlxClosureFreeVar) = 
    [ mkLdarg0; mkNormalLdfld (mkILFieldSpecInTy (clospec.ILType, fv.fvName, fv.fvType) ) ]


let mkCallFunc cenv allocLocal numThisGenParams tl apps = 

    // "callfunc" and "callclo" instructions become a series of indirect 
    // calls or a single direct call.   
    let varCount = numThisGenParams

    // Unwind the stack until the arguments given in the apps have 
    // all been popped off.  The apps given to this function is 
    // what remains after the first "strip" of suitable arguments for the 
    // first call. 
    // Loaders and storers are returned in groups.  Storers are used to pop 
    // the arguments off the stack that correspond to all the arguments in 
    // the apps, and the loaders are used to load them back on.  
    let rec unwind apps = 
        match apps with 
        | Apps_tyapp (actual, rest) -> 
            let rest = instAppsAux varCount [ actual ] rest
            let storers, loaders = unwind rest
            [] :: storers, [] :: loaders 
        | Apps_app (arg, rest) -> 
            let storers, loaders = unwind rest
            let argStorers, argLoaders = 
                let locn = allocLocal arg 
                [mkStloc locn], [mkLdloc locn]
            argStorers :: storers, argLoaders :: loaders  
        | Apps_done _ -> 
            [], []
            
    let rec computePreCall fst n rest (loaders: ILInstr list) = 
        if fst then 
            let storers, (loaders2 : ILInstr list list) =  unwind rest
            (List.rev (List.concat storers) : ILInstr list) , List.concat loaders2
        else 
            stripUpTo n (function (_x :: _y) -> true | _ -> false) (function (x :: y) -> (x, y) | _ -> failwith "no!") loaders
            
    let rec buildApp fst loaders apps =
        // Strip off one valid indirect call.  [fst] indicates if this is the 
        // first indirect call we're making. The code below makes use of the 
        // fact that term and type applications are never currently mixed for 
        // direct calls. 
        match stripSupportedIndirectCall apps with 
        // Type applications: REVIEW: get rid of curried tyapps - just tuple them 
        | tyargs, [], _ when not (isNil tyargs) ->
            // strip again, instantiating as we go.  we could do this while we count. 
            let (revInstTyArgs, rest') = 
                (([], apps), tyargs) ||> List.fold (fun (revArgsSoFar, cs) _  -> 
                        let actual, rest' = destTyFuncApp cs
                        let rest'' = instAppsAux varCount [ actual ] rest'
                        ((actual :: revArgsSoFar), rest''))
            let instTyargs = List.rev revInstTyArgs
            let precall, loaders' = computePreCall fst 0 rest' loaders
            let doTailCall = andTailness tl false
            let instrs1 = 
                precall @
                [ I_callvirt (doTailCall,  (mkILInstanceMethSpecInTy (cenv.mkILTyFuncTy, "Specialize", [], cenv.ilg.typ_Object, instTyargs)), None) ]
            let instrs1 =                        
                // TyFunc are represented as Specialize<_> methods returning an object.                            
                // For value types, recover result via unbox and load.
                // For reference types, recover via cast.
                let rtnTy = mkTyOfApps cenv rest'
                instrs1 @ [ I_unbox_any rtnTy]
            if doTailCall = Tailcall then instrs1 
            else instrs1 @ buildApp false loaders' rest' 

        // Term applications 
        | [], args, rest when not (isNil args) -> 
            let precall, loaders' = computePreCall fst args.Length rest loaders
            let isLast = (match rest with Apps_done _ -> true | _ -> false)
            let rty  = mkTyOfApps cenv rest
            let doTailCall = andTailness tl isLast

            let preCallBlock = precall 

            if doTailCall = Tailcall then 
                let callBlock =  mkCallBlockForMultiValueApp cenv doTailCall (args, rty) 
                preCallBlock @ callBlock 
            else
                let callBlock =  mkCallBlockForMultiValueApp cenv doTailCall (args, rty) 
                let restBlock = buildApp false loaders' rest 
                preCallBlock @ callBlock @ restBlock 

        | [], [], Apps_done _rty -> [  ]
        | _ -> failwith "*** Error: internal error: unknown indirect calling convention returned by stripSupportedIndirectCall"
             
    buildApp true [] apps 

// Fix up I_ret instruction. Generalise to selected instr. Remove tailcalls.
let convReturnInstr ty instr = 
    match instr with 
    | I_ret -> [I_box ty;I_ret]
    | I_call (_, mspec, varargs) -> [I_call (Normalcall, mspec, varargs)]
    | I_callvirt (_, mspec, varargs) -> [I_callvirt (Normalcall, mspec, varargs)]
    | I_callconstraint (_, ty, mspec, varargs) ->  [I_callconstraint (Normalcall, ty, mspec, varargs)]
    | I_calli (_, csig, varargs) ->  [I_calli (Normalcall, csig, varargs)]
    | _     -> [instr]
        
let convILMethodBody (thisClo, boxReturnTy) (il: ILMethodBody) = 
    // This increase in maxstack is historical, though it's harmless 
    let newMax = 
        match thisClo with 
        | Some _ -> il.MaxStack+2 
        | None -> il.MaxStack
    let code = il.Code
    // Box before returning? e.g. in the case of a TyFunc returning a struct, which 
    // compiles to a Specialise<_> method returning an object 
    let code = 
        match boxReturnTy with
        | None    -> code
        | Some ty -> morphILInstrsInILCode (convReturnInstr ty) code
    { il with MaxStack = newMax; Code = code }

let convMethodBody thisClo = function
    | MethodBody.IL il -> 
        let convil = convILMethodBody (thisClo, None) il.Value
        MethodBody.IL (lazy convil)
    | x -> x

let convMethodDef thisClo (md: ILMethodDef)  =
    let b' = convMethodBody thisClo (md.Body)
    md.With(body=notlazy b')

// -------------------------------------------------------------------- 
// Make fields for free variables of a type abstraction.
//   REVIEW: change type abstractions to use other closure mechanisms.
// -------------------------------------------------------------------- 

let mkILFreeVarForParam (p : ILParameter) = 
    let nm = (match p.Name with Some x -> x | None -> failwith "closure parameters must be given names")
    mkILFreeVar(nm, false, p.Type)

let mkILLocalForFreeVar (p: IlxClosureFreeVar) = mkILLocal p.fvType None

let mkILCloFldSpecs _cenv flds = 
    flds |> Array.map (fun fv -> (fv.fvName, fv.fvType)) |> Array.toList

let mkILCloFldDefs cenv flds = 
    flds 
    |> Array.toList
    |> List.map (fun fv -> 
         let fdef = mkILInstanceField (fv.fvName, fv.fvType, None, ILMemberAccess.Public)
         if fv.fvCompilerGenerated then 
             fdef |> cenv.addFieldNeverAttrs 
                  |> cenv.addFieldGeneratedAttrs 
         else
             fdef)

// -------------------------------------------------------------------- 
// Convert a closure.  Split and chop if there are too many arguments, 
// otherwise build the appropriate kind of thing depending on whether
// it's a type abstraction or a term abstraction.
// -------------------------------------------------------------------- 

let rec convIlxClosureDef cenv encl (td: ILTypeDef) clo = 
    let newTypeDefs = 

      // the following are shared between cases 1 && 2 
      let nowFields = clo.cloFreeVars
      let nowTypeRef =  mkILNestedTyRef (ILScopeRef.Local, encl, td.Name)
      let nowTy = mkILFormalBoxedTy nowTypeRef td.GenericParams
      let nowCloRef = IlxClosureRef(nowTypeRef, clo.cloStructure, nowFields)
      let nowCloSpec = mkILFormalCloRef td.GenericParams nowCloRef clo.cloUseStaticField
      let nowMethods = List.map (convMethodDef (Some nowCloSpec)) td.Methods.AsList
      let tagApp = (Lazy.force clo.cloCode).SourceMarker
      
      let tyargsl, tmargsl, laterStruct = stripSupportedAbstraction clo.cloStructure

      // Adjust all the argument and environment accesses 
      let rewriteCodeToAccessArgsFromEnv laterCloSpec (argToFreeVarMap: (int * IlxClosureFreeVar) list)  = 
          let il = Lazy.force clo.cloCode
          let numLocals = il.Locals.Length
          let rewriteInstrToAccessArgsFromEnv instr =
              let fixupArg mkEnv mkArg n = 
                  let rec findMatchingArg l c = 
                      match l with 
                      | ((m, _) :: t) -> 
                          if n = m then mkEnv c
                          else findMatchingArg t (c+1)
                      | [] -> mkArg (n - argToFreeVarMap.Length + 1)
                  findMatchingArg argToFreeVarMap 0
              match instr with 
              | I_ldarg n -> 
                  fixupArg 
                    (fun x -> [ mkLdloc (uint16 (x+numLocals)) ]) 
                    (fun x -> [ mkLdarg (uint16 x )])
                    (int n)
              | I_starg n -> 
                  fixupArg 
                    (fun x -> [ mkStloc (uint16 (x+numLocals)) ]) 
                    (fun x -> [ I_starg (uint16 x) ])
                    (int n)
              | I_ldarga n ->  
                  fixupArg 
                    (fun x -> [ I_ldloca (uint16 (x+numLocals)) ]) 
                    (fun x -> [ I_ldarga (uint16 x) ]) 
                    (int n)
              | i ->  [i]
          let mainCode = morphILInstrsInILCode rewriteInstrToAccessArgsFromEnv il.Code
          let ldenvCode = argToFreeVarMap |> List.mapi (fun n (_, fv) -> mkLdFreeVar laterCloSpec fv @ [mkStloc (uint16 (n+numLocals)) ]) |> List.concat 
          let code = prependInstrsToCode ldenvCode mainCode
          
          {il with 
               Code=code
               Locals= il.Locals @ (List.map (snd >> mkILLocalForFreeVar) argToFreeVarMap)
               // maxstack may increase by 1 due to environment loads 
               MaxStack=il.MaxStack+1 }


      match tyargsl, tmargsl, laterStruct with 
      // CASE 1 - Type abstraction 
      | (_ :: _), [], _ ->
          let addedGenParams = tyargsl
          let nowReturnTy = (mkTyOfLambdas cenv laterStruct)
          
        // CASE 1a. Split a type abstraction. 
        // Adjust all the argument and environment accesses 
        // Actually that special to do here in the type abstraction case 
        // nb. should combine the term and type abstraction cases for  
        // to allow for term and type variables to be mixed in a single 
        // application. 
          if (match laterStruct with Lambdas_return _ -> false | _ -> true) then 
            
              let nowStruct = List.foldBack (fun x y -> Lambdas_forall(x, y)) tyargsl (Lambdas_return nowReturnTy)
              let laterTypeName = td.Name+"T"
              let laterTypeRef = mkILNestedTyRef (ILScopeRef.Local, encl, laterTypeName)
              let laterGenericParams = td.GenericParams @ addedGenParams
              let selfFreeVar = mkILFreeVar(CompilerGeneratedName ("self"+string nowFields.Length), true, nowCloSpec.ILType)
              let laterFields =  Array.append nowFields [| selfFreeVar |]
              let laterCloRef = IlxClosureRef(laterTypeRef, laterStruct, laterFields)
              let laterCloSpec = mkILFormalCloRef laterGenericParams laterCloRef false
              
              let laterCode = rewriteCodeToAccessArgsFromEnv laterCloSpec [(0, selfFreeVar)]
              let laterTypeDefs = 
                convIlxClosureDef cenv encl
                  (td.With(genericParams=laterGenericParams, name=laterTypeName, methods=emptyILMethods, fields=emptyILFields))
                  {clo with cloStructure=laterStruct
                            cloFreeVars=laterFields
                            cloCode=notlazy laterCode}
              
            // This is the code which will get called when then "now" 
            // arguments get applied. Convert it with the information 
            // that it is the code for a closure... 
              let nowCode = 
                mkILMethodBody
                  (false, [], nowFields.Length + 1, 
                   nonBranchingInstrsToCode
                     begin 
                       // Load up the environment, including self... 
                       (nowFields |> Array.toList |> List.collect (mkLdFreeVar nowCloSpec))  @
                       [ mkLdarg0 ] @
                       // Make the instance of the delegated closure && return it. 
                       // This passes the method type params. as class type params. 
                       [ I_newobj (laterCloSpec.Constructor, None) ] 
                     end, 
                   tagApp)

              let nowTypeDefs = 
                convIlxClosureDef cenv encl td {clo with cloStructure=nowStruct 
                                                         cloCode=notlazy nowCode}

              let nowTypeDefs = nowTypeDefs |>  List.map (addMethodGeneratedAttrsToTypeDef cenv)

              nowTypeDefs @ laterTypeDefs
          else 
              // CASE 1b. Build a type application. 
              let boxReturnTy = Some nowReturnTy (* box prior to all I_ret *)
              let convil = convILMethodBody (Some nowCloSpec, boxReturnTy) (Lazy.force clo.cloCode)
              let nowApplyMethDef =
                mkILGenericVirtualMethod
                  ("Specialize", 
                   ILMemberAccess.Public, 
                   addedGenParams,  (* method is generic over added ILGenericParameterDefs *)
                   [], 
                   mkILReturn(cenv.ilg.typ_Object), 
                   MethodBody.IL (lazy convil))
              let ctorMethodDef = 
                  mkILStorageCtor 
                    (None, 
                     [ mkLdarg0; mkNormalCall (mkILCtorMethSpecForTy (cenv.mkILTyFuncTy, [])) ], 
                     nowTy, 
                     mkILCloFldSpecs cenv nowFields, 
                     ILMemberAccess.Assembly)
                   |> cenv.addMethodGeneratedAttrs 

              let cloTypeDef = 
                ILTypeDef(name = td.Name,
                          genericParams= td.GenericParams,
                          attributes = td.Attributes,
                          implements = [],
                          nestedTypes = emptyILTypeDefs,
                          layout=ILTypeDefLayout.Auto,
                          extends= Some cenv.mkILTyFuncTy,
                          methods= mkILMethods (ctorMethodDef :: nowApplyMethDef :: nowMethods) ,
                          fields= mkILFields (mkILCloFldDefs cenv nowFields @ td.Fields.AsList),
                          customAttrs=emptyILCustomAttrs,
                          methodImpls=emptyILMethodImpls,
                          properties=emptyILProperties,
                          events=emptyILEvents,
                          securityDecls=emptyILSecurityDecls)
                     .WithSpecialName(false)
                     .WithImport(false)
                     .WithHasSecurity(false)
                     .WithAbstract(false)
                     .WithSealed(true)
                     .WithInitSemantics(ILTypeInit.BeforeField)
                     .WithEncoding(ILDefaultPInvokeEncoding.Ansi)
              [ cloTypeDef]

    // CASE 2 - Term Application 
      |  [], (_ :: _ as nowParams), _ ->
          let nowReturnTy = mkTyOfLambdas cenv laterStruct
          
         // CASE 2a - Too Many Term Arguments or Remaining Type arguments - Split the Closure Class in Two 
          if (match laterStruct with Lambdas_return _ -> false | _ -> true) then 
              let nowStruct = List.foldBack (fun l r -> Lambdas_lambda(l, r)) nowParams (Lambdas_return nowReturnTy)
              let laterTypeName = td.Name+"D"
              let laterTypeRef = mkILNestedTyRef (ILScopeRef.Local, encl, laterTypeName)
              let laterGenericParams = td.GenericParams
            // Number each argument left-to-right, adding one to account for the "this" pointer
              let selfFreeVar = mkILFreeVar(CompilerGeneratedName "self", true, nowCloSpec.ILType)
              let argToFreeVarMap = (0, selfFreeVar) :: (nowParams |> List.mapi (fun i p -> i+1, mkILFreeVarForParam p))
              let laterFreeVars = argToFreeVarMap |> List.map snd |> List.toArray
              let laterFields = Array.append nowFields laterFreeVars
              let laterCloRef = IlxClosureRef(laterTypeRef, laterStruct, laterFields)
              let laterCloSpec = mkILFormalCloRef laterGenericParams laterCloRef false
              
              // This is the code which will first get called. 
              let nowCode = 
                  mkILMethodBody
                    (false, [], argToFreeVarMap.Length + nowFields.Length, 
                     nonBranchingInstrsToCode
                       begin 
                         // Load up the environment 
                         (nowFields |> Array.toList |> List.collect (mkLdFreeVar nowCloSpec))  @
                         // Load up all the arguments (including self), which become free variables in the delegated closure 
                         (argToFreeVarMap  |> List.map (fun (n, _) -> mkLdarg (uint16 n))) @
                         // Make the instance of the delegated closure && return it. 
                         [ I_newobj (laterCloSpec.Constructor, None) ] 
                       end, 
                     tagApp)

              let nowTypeDefs = 
                convIlxClosureDef cenv encl td {clo with cloStructure=nowStruct
                                                         cloCode=notlazy nowCode}

              let laterCode = rewriteCodeToAccessArgsFromEnv laterCloSpec argToFreeVarMap

              let laterTypeDefs = 
                convIlxClosureDef cenv encl
                  (td.With(genericParams=laterGenericParams, name=laterTypeName, methods=emptyILMethods, fields=emptyILFields))
                  {clo with cloStructure=laterStruct
                            cloFreeVars=laterFields
                            cloCode=notlazy laterCode}

              // add 'compiler generated' to all the methods in the 'now' classes
              let nowTypeDefs = nowTypeDefs |>  List.map (addMethodGeneratedAttrsToTypeDef cenv)

              nowTypeDefs @ laterTypeDefs
                        
          else 
                // CASE 2b - Build an Term Application Apply method 
                // CASE 2b2. Build a term application as a virtual method. 
                
                let nowEnvParentClass = typ_Func cenv (typesOfILParams nowParams) nowReturnTy 

                let cloTypeDef = 
                    let convil = convILMethodBody (Some nowCloSpec, None)  (Lazy.force clo.cloCode)
                    let nowApplyMethDef =
                        mkILNonGenericVirtualMethod
                          ("Invoke", ILMemberAccess.Public, 
                           nowParams, 
                           mkILReturn nowReturnTy, 
                           MethodBody.IL (lazy convil))

                    let ctorMethodDef = 
                        mkILStorageCtor 
                           (None, 
                            [ mkLdarg0; mkNormalCall (mkILCtorMethSpecForTy (nowEnvParentClass, [])) ], 
                            nowTy, 
                            mkILCloFldSpecs cenv nowFields, 
                            ILMemberAccess.Assembly)
                        |> cenv.addMethodGeneratedAttrs 

                    ILTypeDef(name = td.Name,
                              genericParams= td.GenericParams,
                              attributes = td.Attributes,
                              implements = [],
                              layout=ILTypeDefLayout.Auto,
                              nestedTypes = emptyILTypeDefs,
                              extends= Some nowEnvParentClass,
                              methods= mkILMethods (ctorMethodDef :: nowApplyMethDef :: nowMethods),
                              fields= mkILFields (mkILCloFldDefs cenv nowFields @ td.Fields.AsList),
                              customAttrs=emptyILCustomAttrs,
                              methodImpls=emptyILMethodImpls,
                              properties=emptyILProperties,
                              events=emptyILEvents,
                              securityDecls=emptyILSecurityDecls)
                         .WithHasSecurity(false)
                         .WithSpecialName(false)
                         .WithAbstract(false)
                         .WithImport(false)
                         .WithEncoding(ILDefaultPInvokeEncoding.Ansi)
                         .WithSealed(true)
                         .WithInitSemantics(ILTypeInit.BeforeField)

                [cloTypeDef]

      |  [], [], Lambdas_return _ -> 

          // No code is being declared: just bake a (mutable) environment 
          let cloCode' = 
            match td.Extends with 
            | None ->  (mkILNonGenericEmptyCtor None cenv.ilg.typ_Object).MethodBody 
            | Some  _ -> convILMethodBody (Some nowCloSpec, None)  (Lazy.force clo.cloCode)

          let ctorMethodDef = 
            let flds = (mkILCloFldSpecs cenv nowFields)
            mkILCtor(ILMemberAccess.Public, 
                    List.map mkILParamNamed flds, 
                    mkMethodBody
                      (cloCode'.IsZeroInit, 
                       cloCode'.Locals, 
                       cloCode'.MaxStack, 
                       prependInstrsToCode
                          (List.concat (List.mapi (fun n (nm, ty) -> 
                               [ mkLdarg0
                                 mkLdarg (uint16 (n+1))
                                 mkNormalStfld (mkILFieldSpecInTy (nowTy, nm, ty))
                               ])  flds))
                         cloCode'.Code, 
                       None))
          
          let cloTypeDef = 
            td.With(implements= td.Implements,
                    extends= (match td.Extends with None -> Some cenv.ilg.typ_Object | Some x -> Some(x)),
                    name = td.Name,
                    genericParams= td.GenericParams,
                    methods= mkILMethods (ctorMethodDef :: nowMethods),
                    fields= mkILFields (mkILCloFldDefs cenv nowFields @ td.Fields.AsList))

          [cloTypeDef]

      | a, b, _ ->
          failwith ("Unexpected unsupported abstraction sequence, #tyabs = "+string a.Length + ", #tmabs = "+string b.Length)
   
    newTypeDefs

