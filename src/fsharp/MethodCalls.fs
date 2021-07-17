// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Logic associated with resolving method calls.
module internal FSharp.Compiler.MethodCalls

open Internal.Utilities

open Internal.Utilities.Library 
open Internal.Utilities.Library.Extras
open FSharp.Compiler 
open FSharp.Compiler.AbstractIL.IL 
open FSharp.Compiler.AccessibilityLogic
open FSharp.Compiler.AttributeChecking
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Features
open FSharp.Compiler.InfoReader
open FSharp.Compiler.Infos
open FSharp.Compiler.IO
open FSharp.Compiler.NameResolution
open FSharp.Compiler.Syntax
open FSharp.Compiler.Syntax.PrettyNaming
open FSharp.Compiler.SyntaxTreeOps
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Compiler.Text
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.TypedTreeOps.DebugPrint
open FSharp.Compiler.TypeRelations

#if !NO_EXTENSIONTYPING
open FSharp.Compiler.ExtensionTyping
#endif

//-------------------------------------------------------------------------
// Sets of methods involved in overload resolution and trait constraint
// satisfaction.
//------------------------------------------------------------------------- 

/// In the following, 'T gets instantiated to: 
///   1. the expression being supplied for an argument 
///   2. "unit", when simply checking for the existence of an overload that satisfies 
///      a signature, or when finding the corresponding witness. 
/// Note the parametricity helps ensure that overload resolution doesn't depend on the 
/// expression on the callside (though it is in some circumstances allowed 
/// to depend on some type information inferred syntactically from that 
/// expression, e.g. a lambda expression may be converted to a delegate as 
/// an adhoc conversion. 
///
/// The bool indicates if named using a '?', making the caller argument explicit-optional
type CallerArg<'T> = 
    /// CallerArg(ty, range, isOpt, exprInfo)
    | CallerArg of ty: TType * range: range * isOpt: bool * exprInfo: 'T  

    member x.CallerArgumentType = (let (CallerArg(ty, _, _, _)) = x in ty)

    member x.Range = (let (CallerArg(_, m, _, _)) = x in m)

    member x.IsExplicitOptional = (let (CallerArg(_, _, isOpt, _)) = x in isOpt)

    member x.Expr = (let (CallerArg(_, _, _, expr)) = x in expr)
    
/// Represents the information about an argument in the method being called
type CalledArg = 
    { Position: struct (int * int)
      IsParamArray : bool
      OptArgInfo : OptionalArgInfo
      CallerInfo : CallerInfo
      IsInArg: bool
      IsOutArg: bool
      ReflArgInfo: ReflectedArgInfo
      NameOpt: Ident option
      CalledArgumentType : TType }

let CalledArg (pos, isParamArray, optArgInfo, callerInfo, isInArg, isOutArg, nameOpt, reflArgInfo, calledArgTy) =
    { Position=pos
      IsParamArray=isParamArray
      OptArgInfo=optArgInfo
      CallerInfo=callerInfo
      IsInArg=isInArg
      IsOutArg=isOutArg
      ReflArgInfo=reflArgInfo
      NameOpt=nameOpt
      CalledArgumentType=calledArgTy }

/// Represents a match between a caller argument and a called argument, arising from either
/// a named argument or an unnamed argument.
type AssignedCalledArg<'T> = 

    { /// The identifier for a named argument, if any
      NamedArgIdOpt : Ident option

      /// The called argument in the method
      CalledArg: CalledArg 

      /// The argument on the caller side
      CallerArg: CallerArg<'T> }

    member x.Position = x.CalledArg.Position

/// Represents the possibilities for a named-setter argument (a property, field, or a record field setter)
type AssignedItemSetterTarget = 
    | AssignedPropSetter of PropInfo * MethInfo * TypeInst   (* the MethInfo is a non-indexer setter property *)
    | AssignedILFieldSetter of ILFieldInfo 
    | AssignedRecdFieldSetter of RecdFieldInfo 

/// Represents the resolution of a caller argument as a named-setter argument
type AssignedItemSetter<'T> = AssignedItemSetter of Ident * AssignedItemSetterTarget * CallerArg<'T> 

type CallerNamedArg<'T> = 
    | CallerNamedArg of Ident * CallerArg<'T>  

    member x.Ident = (let (CallerNamedArg(id, _)) = x in id)

    member x.Name = x.Ident.idText

    member x.CallerArg = (let (CallerNamedArg(_, a)) = x in a)

/// Represents the list of unnamed / named arguments at method call site
/// remark: The usage of list list is due to tupling and currying of arguments,
/// stemming from SynValInfo in the AST.
[<Struct>]
type CallerArgs<'T> = 
    { 
        Unnamed: CallerArg<'T> list list
        Named: CallerNamedArg<'T> list list 
    }
    static member Empty : CallerArgs<'T> = { Unnamed = []; Named = [] }
    member x.CallerArgCounts = List.length x.Unnamed, List.length x.Named
    member x.CurriedCallerArgs = List.zip x.Unnamed x.Named
    member x.ArgumentNamesAndTypes =
        let unnamed = x.Unnamed |> List.collect (List.map (fun i -> None, i.CallerArgumentType))
        let named = x.Named |> List.collect (List.map (fun i -> Some i.Name, i.CallerArg.CallerArgumentType))
        unnamed @ named

//-------------------------------------------------------------------------
// Callsite conversions
//------------------------------------------------------------------------- 

// If the called method argument is a delegate type, and the caller is known to be a function type, then the caller may provide a function 
// If the called method argument is an Expression<T> type, and the caller is known to be a function type, then the caller may provide a T
// If the called method argument is an [<AutoQuote>] Quotations.Expr<T>, and the caller is not known to be a quoted expression type, then the caller may provide a T
let AdjustCalledArgTypeForLinqExpressionsAndAutoQuote (infoReader: InfoReader) callerArgTy (calledArg: CalledArg) m =
    let g = infoReader.g
    let calledArgTy = calledArg.CalledArgumentType

    let adjustDelegateTy calledTy =
        let (SigOfFunctionForDelegate(_, delArgTys, _, fty)) = GetSigOfFunctionForDelegate infoReader calledTy m AccessibleFromSomewhere
        let delArgTys = if isNil delArgTys then [g.unit_ty] else delArgTys
        if (fst (stripFunTy g callerArgTy)).Length = delArgTys.Length then
            fty 
        else
            calledArgTy 

    if isDelegateTy g calledArgTy && isFunTy g callerArgTy then 
        adjustDelegateTy calledArgTy

    elif isLinqExpressionTy g calledArgTy && isFunTy g callerArgTy then 
        let calledArgTyNoExpr = destLinqExpressionTy g calledArgTy
        if isDelegateTy g calledArgTyNoExpr then 
            adjustDelegateTy calledArgTyNoExpr
        else
            calledArgTy

    elif calledArg.ReflArgInfo.AutoQuote && isQuotedExprTy g calledArgTy && not (isQuotedExprTy g callerArgTy) then 
        destQuotedExprTy g calledArgTy

    else calledArgTy

/// Adjust the called argument type to take into account whether the caller's argument is CSharpMethod(?arg=Some(3)) or CSharpMethod(arg=1) 
let AdjustCalledArgTypeForOptionals (g: TcGlobals) enforceNullableOptionalsKnownTypes (calledArg: CalledArg) calledArgTy (callerArg: CallerArg<_>) =

    if callerArg.IsExplicitOptional then 
        match calledArg.OptArgInfo with 
        // CSharpMethod(?x = arg), optional C#-style argument, may have nullable type
        | CallerSide _ -> 
            if g.langVersion.SupportsFeature LanguageFeature.NullableOptionalInterop then
                if isNullableTy g calledArgTy then
                    mkOptionTy g (destNullableTy g calledArgTy)
                else
                    mkOptionTy g calledArgTy
            else
                calledArgTy

        // FSharpMethod(?x = arg), optional F#-style argument
        | CalleeSide ->
            // In this case, the called argument will already have option type
            calledArgTy

        | NotOptional -> 
            // This condition represents an error but the error is raised in later processing
            calledArgTy
    else
        match calledArg.OptArgInfo with 
        // CSharpMethod(x = arg), non-optional C#-style argument, may have type Nullable<ty>. 
        | NotOptional when not (g.langVersion.SupportsFeature LanguageFeature.NullableOptionalInterop) ->
            calledArgTy

        // The arg should have type ty. However for backwards compat, we also allow arg to have type Nullable<ty>
        | NotOptional 
        // CSharpMethod(x = arg), optional C#-style argument, may have type Nullable<ty>. 
        | CallerSide _ ->
            if isNullableTy g calledArgTy && g.langVersion.SupportsFeature LanguageFeature.NullableOptionalInterop then 
                // If inference has worked out it's a nullable then use this
                if isNullableTy g callerArg.CallerArgumentType then
                    calledArgTy
                // If inference has worked out it's a struct (e.g. an int) then use this
                elif isStructTy g callerArg.CallerArgumentType then
                    destNullableTy g calledArgTy
                // If neither and we are at the end of overload resolution then use the Nullable
                elif enforceNullableOptionalsKnownTypes then 
                    calledArgTy
                // If at the beginning of inference then use a type variable.
                else 
                    match calledArg.OptArgInfo with
                    // If inference has not solved the kind of Nullable on the called arg and is not optional then use this.
                    | NotOptional when isTyparTy g (destNullableTy g calledArgTy) ->
                        calledArgTy
                    | _ ->
                        let compgenId = mkSynId range0 unassignedTyparName
                        mkTyparTy (Construct.NewTypar (TyparKind.Type, TyparRigidity.Flexible, SynTypar(compgenId, TyparStaticReq.None, true), false, TyparDynamicReq.No, [], false, false))
            else
                calledArgTy

        // FSharpMethod(x = arg), optional F#-style argument, should have option type
        | CalleeSide ->
            if isOptionTy g calledArgTy then
                destOptionTy g calledArgTy
            else
                calledArgTy

// F# supports three adhoc conversions at method callsites (note C# supports more, though ones 
// such as implicit conversions interact badly with type inference). 
//
// 1. The use of "(fun x y -> ...)" when  a delegate it expected. This is not part of 
// the ":>" coercion relationship or inference constraint problem as 
// such, but is a special rule applied only to method arguments. 
// 
// The function AdjustCalledArgType detects this case based on types and needs to know that the type being applied 
// is a function type. 
// 
// 2. The use of "(fun x y -> ...)" when Expression<delegate> it expected. This is similar to above.
// 
// 3. Two ways to pass a value where a byref is expected. The first (default) 
// is to use a reference cell, and the interior address is taken automatically 
// The second is an explicit use of the "address-of" operator "&e". Here we detect the second case,
// and record the presence of the syntax "&e" in the pre-inferred actual type for the method argument. 
// The function AdjustCalledArgType detects this and refuses to apply the default byref-to-ref transformation. 
//
// The function AdjustCalledArgType also adjusts for optional arguments. 
let AdjustCalledArgType (infoReader: InfoReader) isConstraint enforceNullableOptionalsKnownTypes (calledArg: CalledArg) (callerArg: CallerArg<_>)  =
    let g = infoReader.g
    let m = callerArg.Range
    // #424218 - when overload resolution is part of constraint solving - do not perform type-directed conversions
    let calledArgTy = calledArg.CalledArgumentType
    let callerArgTy = callerArg.CallerArgumentType
    if isConstraint then 
        calledArgTy 
    else

        // If the called method argument is an inref type, then the caller may provide a byref or value
        if isInByrefTy g calledArgTy then
#if IMPLICIT_ADDRESS_OF
            if isByrefTy g callerArgTy then 
                calledArgTy
            else 
                destByrefTy g calledArgTy
#else
            calledArgTy
#endif

        // If the called method argument is a (non inref) byref type, then the caller may provide a byref or ref.
        elif isByrefTy g calledArgTy then
            if isByrefTy g callerArgTy then 
                calledArgTy
            else
                mkRefCellTy g (destByrefTy g calledArgTy)  

        else 
            let calledArgTy2 = AdjustCalledArgTypeForLinqExpressionsAndAutoQuote infoReader callerArgTy calledArg m
            let calledArgTy3 = AdjustCalledArgTypeForOptionals g enforceNullableOptionalsKnownTypes calledArg calledArgTy2 callerArg
            calledArgTy3        

//-------------------------------------------------------------------------
// CalledMeth
//------------------------------------------------------------------------- 

type CalledMethArgSet<'T> = 
    { /// The called arguments corresponding to "unnamed" arguments
      UnnamedCalledArgs : CalledArg list

      /// Any unnamed caller arguments not otherwise assigned 
      UnnamedCallerArgs :  CallerArg<'T> list

      /// The called "ParamArray" argument, if any
      ParamArrayCalledArgOpt : CalledArg option 

      /// Any unnamed caller arguments assigned to a "param array" argument
      ParamArrayCallerArgs : CallerArg<'T> list

      /// Named args
      AssignedNamedArgs: AssignedCalledArg<'T> list  }

    member x.NumUnnamedCallerArgs = x.UnnamedCallerArgs.Length

    member x.NumAssignedNamedArgs = x.AssignedNamedArgs.Length

    member x.NumUnnamedCalledArgs = x.UnnamedCalledArgs.Length

let MakeCalledArgs amap m (minfo: MethInfo) minst =
    // Mark up the arguments with their position, so we can sort them back into order later 
    let paramDatas = minfo.GetParamDatas(amap, m, minst)
    paramDatas |> List.mapiSquared (fun i j (ParamData(isParamArrayArg, isInArg, isOutArg, optArgInfo, callerInfoFlags, nmOpt, reflArgInfo, typeOfCalledArg))  -> 
      { Position=struct(i,j)
        IsParamArray=isParamArrayArg
        OptArgInfo=optArgInfo
        CallerInfo = callerInfoFlags
        IsInArg=isInArg
        IsOutArg=isOutArg
        ReflArgInfo=reflArgInfo
        NameOpt=nmOpt
        CalledArgumentType=typeOfCalledArg })

/// Represents the syntactic matching between a caller of a method and the called method.
///
/// The constructor takes all the information about the caller and called side of a method, match up named arguments, property setters etc.,
/// and returns a CalledMeth object for further analysis.
type CalledMeth<'T>
      (infoReader: InfoReader,
       nameEnv: NameResolutionEnv option,
       isCheckingAttributeCall,
       /// a function to help generate fresh type variables the property setters methods in generic classes
       freshenMethInfo,
       /// range
       m,
       /// the access domain of the place where the call is taking place
       ad,
       /// the method we're attempting to call
       minfo: MethInfo,
       /// the 'called type arguments', i.e. the fresh generic instantiation of the method we're attempting to call
       calledTyArgs,
       /// the 'caller type arguments', i.e. user-given generic instantiation of the method we're attempting to call
       // todo: consider CallerTypeArgs record
       callerTyArgs: TType list,
       /// the property related to the method we're attempting to call, if any
       pinfoOpt: PropInfo option,
       /// the types of the actual object argument, if any
       callerObjArgTys: TType list,
       /// the 'caller method arguments', i.e. a list of user-given parameter expressions, split between unnamed and named arguments
       callerArgs: CallerArgs<'T>,
       /// do we allow the use of a param args method in its "expanded" form?
       allowParamArgs: bool,
       /// do we allow the use of the transformation that converts out arguments as tuple returns?
       allowOutAndOptArgs: bool,
       /// method parameters
       tyargsOpt : TType option)    
    =
    let g = infoReader.g
    let methodRetTy = minfo.GetFSharpReturnTy(infoReader.amap, m, calledTyArgs)

    let fullCurriedCalledArgs = MakeCalledArgs infoReader.amap m minfo calledTyArgs
    do assert (fullCurriedCalledArgs.Length = fullCurriedCalledArgs.Length)
 
    let argSetInfos = 
        (callerArgs.CurriedCallerArgs, fullCurriedCalledArgs) ||> List.map2 (fun (unnamedCallerArgs, namedCallerArgs) fullCalledArgs -> 
            // Find the arguments not given by name 
            let unnamedCalledArgs = 
                fullCalledArgs |> List.filter (fun calledArg -> 
                    match calledArg.NameOpt with 
                    | Some nm -> namedCallerArgs |> List.forall (fun (CallerNamedArg(nm2, _e)) -> nm.idText <> nm2.idText)   
                    | None -> true)

            // See if any of them are 'out' arguments being returned as part of a return tuple 
            let minArgs, unnamedCalledArgs, unnamedCalledOptArgs, unnamedCalledOutArgs = 
                let nUnnamedCallerArgs = unnamedCallerArgs.Length
                let nUnnamedCalledArgs = unnamedCalledArgs.Length
                if allowOutAndOptArgs && nUnnamedCallerArgs < nUnnamedCalledArgs then
                    let unnamedCalledArgsTrimmed, unnamedCalledOptOrOutArgs = List.splitAt nUnnamedCallerArgs unnamedCalledArgs
                    
                    // Check if all optional/out arguments are byref-out args
                    if unnamedCalledOptOrOutArgs |> List.forall (fun x -> x.IsOutArg && isByrefTy g x.CalledArgumentType) then 
                        nUnnamedCallerArgs - 1, unnamedCalledArgsTrimmed, [], unnamedCalledOptOrOutArgs 
                    // Check if all optional/out arguments are optional args
                    elif unnamedCalledOptOrOutArgs |> List.forall (fun x -> x.OptArgInfo.IsOptional) then 
                        nUnnamedCallerArgs - 1, unnamedCalledArgsTrimmed, unnamedCalledOptOrOutArgs, []
                    // Otherwise drop them on the floor
                    else
                        nUnnamedCalledArgs - 1, unnamedCalledArgs, [], []
                else 
                    nUnnamedCalledArgs - 1, unnamedCalledArgs, [], []

            let (unnamedCallerArgs, paramArrayCallerArgs), unnamedCalledArgs, paramArrayCalledArgOpt = 
                let supportsParamArgs = 
                    allowParamArgs && 
                    minArgs >= 0 && 
                    unnamedCalledArgs |> List.last |> (fun calledArg -> calledArg.IsParamArray && isArray1DTy g calledArg.CalledArgumentType)

                if supportsParamArgs  && unnamedCallerArgs.Length >= minArgs then
                    let a, b = List.frontAndBack unnamedCalledArgs
                    List.splitAt minArgs unnamedCallerArgs, a, Some(b)
                else
                    (unnamedCallerArgs, []), unnamedCalledArgs, None

            let assignedNamedArgs = 
                fullCalledArgs |> List.choose (fun calledArg ->
                    match calledArg.NameOpt with 
                    | Some nm -> 
                        namedCallerArgs |> List.tryPick (fun (CallerNamedArg(nm2, callerArg)) -> 
                            if nm.idText = nm2.idText then Some { NamedArgIdOpt = Some nm2; CallerArg=callerArg; CalledArg=calledArg } 
                            else None) 
                    | _ -> None)

            let unassignedNamedItems = 
                namedCallerArgs |> List.filter (fun (CallerNamedArg(nm, _e)) -> 
                    fullCalledArgs |> List.forall (fun calledArg -> 
                        match calledArg.NameOpt with 
                        | Some nm2 -> nm.idText <> nm2.idText
                        | None -> true))

            let attributeAssignedNamedItems = 
                if isCheckingAttributeCall then 
                    // The process for assigning names-->properties is substantially different for attribute specifications 
                    // because it permits the bindings of names to immutable fields. So we use the old 
                    // code for this.
                    unassignedNamedItems
                 else 
                    []

            let assignedNamedProps, unassignedNamedItems = 
                let returnedObjTy = if minfo.IsConstructor then minfo.ApparentEnclosingType else methodRetTy
                unassignedNamedItems |> List.splitChoose (fun (CallerNamedArg(id, e) as arg) -> 
                    let nm = id.idText
                    let pinfos = GetIntrinsicPropInfoSetsOfType infoReader (Some nm) ad AllowMultiIntfInstantiations.Yes IgnoreOverrides id.idRange returnedObjTy
                    let pinfos = pinfos |> ExcludeHiddenOfPropInfos g infoReader.amap m 
                    match pinfos with 
                    | [pinfo] when pinfo.HasSetter && not pinfo.IsIndexer -> 
                        let pminfo = pinfo.SetterMethod
                        let pminst = freshenMethInfo m pminfo
                        Choice1Of2(AssignedItemSetter(id, AssignedPropSetter(pinfo, pminfo, pminst), e))
                    | _ ->
                        let epinfos = 
                            match nameEnv with  
                            | Some ne -> ExtensionPropInfosOfTypeInScope ResultCollectionSettings.AllResults infoReader ne (Some nm) ad m returnedObjTy
                            | _ -> []
                        match epinfos with 
                        | [pinfo] when pinfo.HasSetter && not pinfo.IsIndexer -> 
                            let pminfo = pinfo.SetterMethod
                            let pminst = match minfo with
                                         | MethInfo.FSMeth(_, TType.TType_app(_, types), _, _) -> types
                                         | _ -> freshenMethInfo m pminfo

                            let pminst = match tyargsOpt with
                                         | Some(TType.TType_app(_, types)) -> types
                                         | _ -> pminst
                            Choice1Of2(AssignedItemSetter(id, AssignedPropSetter(pinfo, pminfo, pminst), e))
                        |  _ ->    
                            match infoReader.GetILFieldInfosOfType(Some(nm), ad, m, returnedObjTy) with
                            | finfo :: _ -> 
                                Choice1Of2(AssignedItemSetter(id, AssignedILFieldSetter(finfo), e))
                            | _ ->              
                              match infoReader.TryFindRecdOrClassFieldInfoOfType(nm, m, returnedObjTy) with
                              | ValueSome rfinfo -> 
                                  Choice1Of2(AssignedItemSetter(id, AssignedRecdFieldSetter(rfinfo), e))
                              | _ -> 
                                  Choice2Of2(arg))

            let names = System.Collections.Generic.HashSet<_>() 
            for CallerNamedArg(nm, _) in namedCallerArgs do 
                if not (names.Add nm.idText) then
                    errorR(Error(FSComp.SR.typrelNamedArgumentHasBeenAssignedMoreThenOnce nm.idText, m))
                
            let argSet = { UnnamedCalledArgs=unnamedCalledArgs; UnnamedCallerArgs=unnamedCallerArgs; ParamArrayCalledArgOpt=paramArrayCalledArgOpt; ParamArrayCallerArgs=paramArrayCallerArgs; AssignedNamedArgs=assignedNamedArgs }

            (argSet, assignedNamedProps, unassignedNamedItems, attributeAssignedNamedItems, unnamedCalledOptArgs, unnamedCalledOutArgs))

    let argSets                     = argSetInfos |> List.map     (fun (x, _, _, _, _, _) -> x)
    let assignedNamedProps          = argSetInfos |> List.collect (fun (_, x, _, _, _, _) -> x)
    let unassignedNamedItems        = argSetInfos |> List.collect (fun (_, _, x, _, _, _) -> x)
    let attributeAssignedNamedItems = argSetInfos |> List.collect (fun (_, _, _, x, _, _) -> x)
    let unnamedCalledOptArgs        = argSetInfos |> List.collect (fun (_, _, _, _, x, _) -> x)
    let unnamedCalledOutArgs        = argSetInfos |> List.collect (fun (_, _, _, _, _, x) -> x)

    member x.infoReader = infoReader

    member x.amap = infoReader.amap

    /// The method we're attempting to call 
    member x.Method = minfo

    /// The instantiation of the method we're attempting to call 
    member x.CalledTyArgs = calledTyArgs

    member x.AllCalledArgs = fullCurriedCalledArgs

    /// The instantiation of the method we're attempting to call 
    member x.CalledTyparInst = 
        let tps = minfo.FormalMethodTypars 
        if tps.Length = calledTyArgs.Length then mkTyparInst tps calledTyArgs else []

    /// The formal instantiation of the method we're attempting to call 
    member x.CallerTyArgs = callerTyArgs

    /// The types of the actual object arguments, if any
    member x.CallerObjArgTys = callerObjArgTys

    /// The argument analysis for each set of curried arguments
    member x.ArgSets = argSets

    /// The return type after implicit deference of byref returns is taken into account
    member x.CalledReturnTypeAfterByrefDeref = 
        let retTy = methodRetTy
        if isByrefTy g retTy then destByrefTy g retTy else retTy

    /// Return type after tupling of out args is taken into account
    member x.CalledReturnTypeAfterOutArgTupling = 
        let retTy = x.CalledReturnTypeAfterByrefDeref
        if isNil unnamedCalledOutArgs then 
            retTy 
        else 
            let outArgTys = unnamedCalledOutArgs |> List.map (fun calledArg -> destByrefTy g calledArg.CalledArgumentType) 
            if isUnitTy g retTy then mkRefTupledTy g outArgTys
            else mkRefTupledTy g (retTy :: outArgTys)

    /// Named setters
    member x.AssignedItemSetters = assignedNamedProps

    /// The property related to the method we're attempting to call, if any  
    member x.AssociatedPropertyInfo = pinfoOpt

    /// Unassigned args
    member x.UnassignedNamedArgs = unassignedNamedItems

    /// Args assigned to specify values for attribute fields and properties (these are not necessarily "property sets")
    member x.AttributeAssignedNamedArgs = attributeAssignedNamedItems

    /// Unnamed called optional args: pass defaults for these
    member x.UnnamedCalledOptArgs = unnamedCalledOptArgs

    /// Unnamed called out args: return these as part of the return tuple
    member x.UnnamedCalledOutArgs = unnamedCalledOutArgs

    static member GetMethod (x: CalledMeth<'T>) = x.Method

    member x.NumArgSets = x.ArgSets.Length

    member x.HasOptArgs = not (isNil x.UnnamedCalledOptArgs)

    member x.HasOutArgs = not (isNil x.UnnamedCalledOutArgs)

    member x.UsesParamArrayConversion = x.ArgSets |> List.exists (fun argSet -> argSet.ParamArrayCalledArgOpt.IsSome)

    member x.ParamArrayCalledArgOpt = x.ArgSets |> List.tryPick (fun argSet -> argSet.ParamArrayCalledArgOpt)

    member x.ParamArrayCallerArgs = x.ArgSets |> List.tryPick (fun argSet -> if Option.isSome argSet.ParamArrayCalledArgOpt then Some argSet.ParamArrayCallerArgs else None )

    member x.GetParamArrayElementType() =
        // turned as a method to avoid assert in variable inspector 
        assert (x.UsesParamArrayConversion)
        x.ParamArrayCalledArgOpt.Value.CalledArgumentType |> destArrayTy x.amap.g 

    member x.NumAssignedProps = x.AssignedItemSetters.Length

    member x.CalledObjArgTys(m) = 
        match x.Method.GetObjArgTypes(x.amap, m, x.CalledTyArgs) with 
        | [ thisArgTy ] when isByrefTy g thisArgTy -> [ destByrefTy g thisArgTy ]
        | res -> res

    member x.NumCalledTyArgs = x.CalledTyArgs.Length

    member x.NumCallerTyArgs = x.CallerTyArgs.Length 

    member x.AssignsAllNamedArgs = isNil x.UnassignedNamedArgs

    member x.HasCorrectArity =
      (x.NumCalledTyArgs = x.NumCallerTyArgs)  &&
      x.ArgSets |> List.forall (fun argSet -> argSet.NumUnnamedCalledArgs = argSet.NumUnnamedCallerArgs) 

    member x.HasCorrectGenericArity =
      (x.NumCalledTyArgs = x.NumCallerTyArgs)  

    member x.IsAccessible(m, ad) = 
        IsMethInfoAccessible x.amap m ad x.Method 

    member x.HasCorrectObjArgs(m) = 
        x.CalledObjArgTys(m).Length = x.CallerObjArgTys.Length 

    member x.IsCandidate(m, ad) =
        x.IsAccessible(m, ad) &&
        x.HasCorrectArity && 
        x.HasCorrectObjArgs(m) &&
        x.AssignsAllNamedArgs

    member x.AssignedUnnamedArgs = 
       // We use Seq.map2 to tolerate there being mismatched caller/called args
       x.ArgSets |> List.map (fun argSet -> 
           (argSet.UnnamedCalledArgs, argSet.UnnamedCallerArgs) ||> Seq.map2 (fun calledArg callerArg -> 
               { NamedArgIdOpt=None; CalledArg=calledArg; CallerArg=callerArg }) |> Seq.toList)

    member x.AssignedNamedArgs = 
       x.ArgSets |> List.map (fun argSet -> argSet.AssignedNamedArgs)

    member x.AllUnnamedCalledArgs = x.ArgSets |> List.collect (fun x -> x.UnnamedCalledArgs)

    member x.TotalNumUnnamedCalledArgs = x.ArgSets |> List.sumBy (fun x -> x.NumUnnamedCalledArgs)

    member x.TotalNumUnnamedCallerArgs = x.ArgSets |> List.sumBy (fun x -> x.NumUnnamedCallerArgs)

    member x.TotalNumAssignedNamedArgs = x.ArgSets |> List.sumBy (fun x -> x.NumAssignedNamedArgs)

    override x.ToString() = "call to " + minfo.ToString()

let NamesOfCalledArgs (calledArgs: CalledArg list) = 
    calledArgs |> List.choose (fun x -> x.NameOpt) 

//-------------------------------------------------------------------------
// Helpers dealing with propagating type information in method overload resolution
//------------------------------------------------------------------------- 

type ArgumentAnalysis = 
    | NoInfo
    | ArgDoesNotMatch 
    | CallerLambdaHasArgTypes of TType list
    | CalledArgMatchesType of TType

let InferLambdaArgsForLambdaPropagation origRhsExpr = 
    let rec loop e = 
        match e with 
        | SynExpr.Lambda (_, _, _, rest, _, _) -> 1 + loop rest
        | SynExpr.MatchLambda _ -> 1
        | _ -> 0
    loop origRhsExpr

let ExamineArgumentForLambdaPropagation (infoReader: InfoReader) (arg: AssignedCalledArg<SynExpr>) =
    let g = infoReader.g

    // Find the explicit lambda arguments of the caller. Ignore parentheses.
    let argExpr = match arg.CallerArg.Expr with SynExpr.Paren (x, _, _, _) -> x  | x -> x
    let countOfCallerLambdaArg = InferLambdaArgsForLambdaPropagation argExpr

    // Adjust for Expression<_>, Func<_, _>, ...
    let adjustedCalledArgTy = AdjustCalledArgType infoReader false false arg.CalledArg arg.CallerArg
    if countOfCallerLambdaArg > 0 then 
        // Decompose the explicit function type of the target
        let calledLambdaArgTys, _calledLambdaRetTy = stripFunTy g adjustedCalledArgTy
        if calledLambdaArgTys.Length >= countOfCallerLambdaArg then 
            // success 
            CallerLambdaHasArgTypes calledLambdaArgTys
        elif isDelegateTy g (if isLinqExpressionTy g adjustedCalledArgTy then destLinqExpressionTy g adjustedCalledArgTy else adjustedCalledArgTy) then
            // delegate arity mismatch
            ArgDoesNotMatch
        else
            // not a function type on the called side - no information
            NoInfo
    else
        // not a lambda on the caller side - push information from caller to called
        CalledArgMatchesType(adjustedCalledArgTy)  
        

let ExamineMethodForLambdaPropagation (x: CalledMeth<SynExpr>) =
    let unnamedInfo = x.AssignedUnnamedArgs |> List.mapSquared (ExamineArgumentForLambdaPropagation x.infoReader)
    let namedInfo = x.AssignedNamedArgs |> List.mapSquared (fun arg -> (arg.NamedArgIdOpt.Value, ExamineArgumentForLambdaPropagation x.infoReader arg))
    if unnamedInfo |> List.existsSquared (function CallerLambdaHasArgTypes _ -> true | _ -> false) || 
       namedInfo |> List.existsSquared (function (_, CallerLambdaHasArgTypes _) -> true | _ -> false) then 
        Some (unnamedInfo, namedInfo)
    else
        None

//-------------------------------------------------------------------------
// Additional helpers for building method calls and doing TAST generation
//------------------------------------------------------------------------- 

/// Is this a 'base' call (in the sense of C#) 
let IsBaseCall objArgs = 
    match objArgs with 
    | [Expr.Val (v, _, _)] when v.BaseOrThisInfo  = BaseVal -> true
    | _ -> false
    
/// Compute whether we insert a 'coerce' on the 'this' pointer for an object model call 
/// For example, when calling an interface method on a struct, or a method on a constrained 
/// variable type. 
let ComputeConstrainedCallInfo g amap m (objArgs, minfo: MethInfo) =
    match objArgs with 
    | [objArgExpr] when not minfo.IsExtensionMember -> 
        let methObjTy = minfo.ApparentEnclosingType
        let objArgTy = tyOfExpr g objArgExpr
        if TypeDefinitelySubsumesTypeNoCoercion 0 g amap m methObjTy objArgTy 
           // Constrained calls to class types can only ever be needed for the three class types that 
           // are base types of value types
           || (isClassTy g methObjTy && 
                 (not (typeEquiv g methObjTy g.system_Object_ty || 
                       typeEquiv g methObjTy g.system_Value_ty ||
                       typeEquiv g methObjTy g.system_Enum_ty))) then 
            None
        else
            // The object argument is a value type or variable type and the target method is an interface or System.Object
            // type. A .NET 2.0 generic constrained call is required
            Some objArgTy
    | _ -> 
        None

/// Adjust the 'this' pointer before making a call 
/// Take the address of a struct, and coerce to an interface/base/constraint type if necessary 
let TakeObjAddrForMethodCall g amap (minfo: MethInfo) isMutable m objArgs f =
    let ccallInfo = ComputeConstrainedCallInfo g amap m (objArgs, minfo)

    let wrap, objArgs = 

        match objArgs with
        | [objArgExpr] ->

            let hasCallInfo = ccallInfo.IsSome
            let mustTakeAddress = hasCallInfo || minfo.ObjArgNeedsAddress(amap, m)
            let objArgTy = tyOfExpr g objArgExpr
            
            let isMutable =
                match isMutable with
                | DefinitelyMutates
                | NeverMutates 
                | AddressOfOp -> isMutable
                | PossiblyMutates ->
                    // Check to see if the method is read-only. Perf optimization.
                    // If there is an extension member whose first arg is an inref, we must return NeverMutates.
                    if mustTakeAddress && (minfo.IsReadOnly || minfo.IsReadOnlyExtensionMember (amap, m)) then
                        NeverMutates
                    else
                        isMutable

            let wrap, objArgExprAddr, isReadOnly, _isWriteOnly =
                mkExprAddrOfExpr g mustTakeAddress hasCallInfo isMutable objArgExpr None m
            
            // Extension members and calls to class constraints may need a coercion for their object argument
            let objArgExprCoerced = 
              if not hasCallInfo &&
                 not (TypeDefinitelySubsumesTypeNoCoercion 0 g amap m minfo.ApparentEnclosingType objArgTy) then 
                  mkCoerceExpr(objArgExprAddr, minfo.ApparentEnclosingType, m, objArgTy)
              else
                  objArgExprAddr

            // Check to see if the extension member uses the extending type as a byref.
            //     If so, make sure we don't allow readonly/immutable values to be passed byref from an extension member. 
            //     An inref will work though.
            if isReadOnly && mustTakeAddress && minfo.IsExtensionMember then
                minfo.TryObjArgByrefType(amap, m, minfo.FormalMethodInst)
                |> Option.iter (fun ty ->
                    if not (isInByrefTy g ty) then
                        errorR(Error(FSComp.SR.tcCannotCallExtensionMethodInrefToByref(minfo.DisplayName), m)))
                        

            wrap, [objArgExprCoerced] 

        | _ -> 
            id, objArgs
    let e, ety = f ccallInfo objArgs
    wrap e, ety

/// Build an expression node that is a call to a .NET method. 
let BuildILMethInfoCall g amap m isProp (minfo: ILMethInfo) valUseFlags minst direct args = 
    let valu = isStructTy g minfo.ApparentEnclosingType
    let ctor = minfo.IsConstructor
    if minfo.IsClassConstructor then 
        error (InternalError (minfo.ILName+": cannot call a class constructor", m))
    let useCallvirt = 
        not valu && not direct && minfo.IsVirtual
    let isProtected = minfo.IsProtectedAccessibility
    let ilMethRef = minfo.ILMethodRef
    let newobj = ctor && (match valUseFlags with NormalValUse -> true | _ -> false)
    let exprTy = if ctor then minfo.ApparentEnclosingType else minfo.GetFSharpReturnTy(amap, m, minst)
    let retTy = if not ctor && (stripILModifiedFromTy ilMethRef.ReturnType) = ILType.Void then [] else [exprTy]
    let isDllImport = minfo.IsDllImport g
    Expr.Op (TOp.ILCall (useCallvirt, isProtected, valu, newobj, valUseFlags, isProp, isDllImport, ilMethRef, minfo.DeclaringTypeInst, minst, retTy), [], args, m),
    exprTy


/// Build a call to an F# method.
///
/// Consume the arguments in chunks and build applications.  This copes with various F# calling signatures
/// all of which ultimately become 'methods'.
///
/// QUERY: this looks overly complex considering that we are doing a fundamentally simple 
/// thing here. 
let BuildFSharpMethodApp g m (vref: ValRef) vexp vexprty (args: Exprs) =
    let arities =  (arityOfVal vref.Deref).AritiesOfArgs
    
    let args3, (leftover, retTy) =
        let exprL expr = exprL g expr
        ((args, vexprty), arities) ||> List.mapFold (fun (args, fty) arity -> 
            match arity, args with 
            | (0|1), [] when typeEquiv g (domainOfFunTy g fty) g.unit_ty -> mkUnit g m, (args, rangeOfFunTy g fty)
            | 0, (arg :: argst) -> 
                let msg = LayoutRender.showL (Layout.sepListL (Layout.rightL (TaggedText.tagText ";")) (List.map exprL args))
                warning(InternalError(sprintf "Unexpected zero arity, args = %s" msg, m))
                arg, (argst, rangeOfFunTy g fty)
            | 1, (arg :: argst) -> arg, (argst, rangeOfFunTy g fty)
            | 1, [] -> error(InternalError("expected additional arguments here", m))
            | _ -> 
                if args.Length < arity then
                    error(InternalError("internal error in getting arguments, n = "+string arity+", #args = "+string args.Length, m))
                let tupargs, argst = List.splitAt arity args
                let tuptys = tupargs |> List.map (tyOfExpr g) 
                (mkRefTupled g m tupargs tuptys),
                (argst, rangeOfFunTy g fty) )
    if not leftover.IsEmpty then error(InternalError("Unexpected "+string(leftover.Length)+" remaining arguments in method application", m))
    mkApps g ((vexp, vexprty), [], args3, m),
    retTy
    
/// Build a call to an F# method.
let BuildFSharpMethodCall g m (vref: ValRef) valUseFlags declaringTypeInst minst args =
    let vexp = Expr.Val (vref, valUseFlags, m)
    let vexpty = vref.Type
    let tpsorig, tau =  vref.TypeScheme
    let vtinst = declaringTypeInst @ minst
    if tpsorig.Length <> vtinst.Length then error(InternalError("BuildFSharpMethodCall: unexpected typar length mismatch",m))
    let expr = mkTyAppExpr m (vexp, vexpty) vtinst
    let exprty = instType (mkTyparInst tpsorig vtinst) tau
    BuildFSharpMethodApp g m vref expr exprty args
    

/// Make a call to a method info. Used by the optimizer and code generator to build 
/// calls to the type-directed solutions to member constraints.
let MakeMethInfoCall amap m minfo minst args =
    let valUseFlags = NormalValUse // correct unless if we allow wild trait constraints like "T has a ctor and can be used as a parent class" 

    match minfo with 

    | ILMeth(g, ilminfo, _) -> 
        let direct = not minfo.IsVirtual
        let isProp = false // not necessarily correct, but this is only used post-creflect where this flag is irrelevant 
        BuildILMethInfoCall g amap m isProp ilminfo valUseFlags minst  direct args |> fst

    | FSMeth(g, _, vref, _) -> 
        BuildFSharpMethodCall g m vref valUseFlags minfo.DeclaringTypeInst minst args |> fst

    | DefaultStructCtor(_, ty) -> 
       mkDefault (m, ty)

#if !NO_EXTENSIONTYPING
    | ProvidedMeth(amap, mi, _, m) -> 
        let isProp = false // not necessarily correct, but this is only used post-creflect where this flag is irrelevant 
        let ilMethodRef = Import.ImportProvidedMethodBaseAsILMethodRef amap m mi
        let isConstructor = mi.PUntaint((fun c -> c.IsConstructor), m)
        let valu = mi.PUntaint((fun c -> c.DeclaringType.IsValueType), m)
        let actualTypeInst = [] // GENERIC TYPE PROVIDERS: for generics, we would have something here
        let actualMethInst = [] // GENERIC TYPE PROVIDERS: for generics, we would have something here
        let ilReturnTys = Option.toList (minfo.GetCompiledReturnTy(amap, m, []))  // GENERIC TYPE PROVIDERS: for generics, we would have more here
        // REVIEW: Should we allow protected calls?
        Expr.Op (TOp.ILCall (false, false, valu, isConstructor, valUseFlags, isProp, false, ilMethodRef, actualTypeInst, actualMethInst, ilReturnTys), [], args, m)

#endif

#if !NO_EXTENSIONTYPING
// This imports a provided method, and checks if it is a known compiler intrinsic like "1 + 2"
let TryImportProvidedMethodBaseAsLibraryIntrinsic (amap: Import.ImportMap, m: range, mbase: Tainted<ProvidedMethodBase>) = 
    let methodName = mbase.PUntaint((fun x -> x.Name), m)
    let declaringType = Import.ImportProvidedType amap m (mbase.PApply((fun x -> x.DeclaringType), m))
    match tryTcrefOfAppTy amap.g declaringType with
    | ValueSome declaringEntity ->
        if not declaringEntity.IsLocalRef && ccuEq declaringEntity.nlr.Ccu amap.g.fslibCcu then
            let n = mbase.PUntaint((fun x -> x.GetParameters().Length), m)
            match amap.g.knownIntrinsics.TryGetValue ((declaringEntity.LogicalName, None, methodName, n)) with 
            | true, vref -> Some vref
            | _ -> 
            match amap.g.knownFSharpCoreModules.TryGetValue declaringEntity.LogicalName with
            | true, modRef -> 
                modRef.ModuleOrNamespaceType.AllValsByLogicalName 
                |> Seq.tryPick (fun (KeyValue(_, v)) -> if (v.CompiledName amap.g.CompilerGlobalState) = methodName then Some (mkNestedValRef modRef v) else None)
            | _ -> None
        else
            None
    | _ ->
        None
#endif
        

/// Build an expression that calls a given method info. 
/// This is called after overload resolution, and also to call other 
/// methods such as 'setters' for properties. 
//   tcVal: used to convert an F# value into an expression. See tc.fs. 
//   isProp: is it a property get? 
//   minst: the instantiation to apply for a generic method 
//   objArgs: the 'this' argument, if any 
//   args: the arguments, if any 
let BuildMethodCall tcVal g amap isMutable m isProp minfo valUseFlags minst objArgs args =
    let direct = IsBaseCall objArgs

    TakeObjAddrForMethodCall g amap minfo isMutable m objArgs (fun ccallInfo objArgs -> 
        let allArgs = objArgs @ args
        let valUseFlags = 
            if direct && (match valUseFlags with NormalValUse -> true | _ -> false) then 
                VSlotDirectCall 
            else 
                match ccallInfo with
                | Some ty -> 
                    // printfn "possible constrained call to '%s' at %A" minfo.LogicalName m
                    PossibleConstrainedCall ty
                | None -> 
                    valUseFlags

        match minfo with 
#if !NO_EXTENSIONTYPING
        // By this time this is an erased method info, e.g. one returned from an expression
        // REVIEW: copied from tastops, which doesn't allow protected methods
        | ProvidedMeth (amap, providedMeth, _, _) -> 
            // TODO: there  is a fair bit of duplication here with mk_il_minfo_call. We should be able to merge these
                
            /// Build an expression node that is a call to a extension method in a generated assembly
            let enclTy = minfo.ApparentEnclosingType
            // prohibit calls to methods that are declared in specific array types (Get, Set, Address)
            // these calls are provided by the runtime and should not be called from the user code
            if isArrayTy g enclTy then
                let tpe = TypeProviderError(FSComp.SR.tcRuntimeSuppliedMethodCannotBeUsedInUserCode(minfo.DisplayName), providedMeth.TypeProviderDesignation, m)
                error tpe
            let valu = isStructTy g enclTy
            let isCtor = minfo.IsConstructor
            if minfo.IsClassConstructor then 
                error (InternalError (minfo.LogicalName + ": cannot call a class constructor", m))
            let useCallvirt = not valu && not direct && minfo.IsVirtual
            let isProtected = minfo.IsProtectedAccessibility
            let exprTy = if isCtor then enclTy else minfo.GetFSharpReturnTy(amap, m, minst)
            match TryImportProvidedMethodBaseAsLibraryIntrinsic (amap, m, providedMeth) with 
            | Some fsValRef -> 
                //reraise() calls are converted to TOp.Reraise in the type checker. So if a provided expression includes a reraise call
                // we must put it in that form here.
                if valRefEq amap.g fsValRef amap.g.reraise_vref then
                    mkReraise m exprTy, exprTy
                else
                    let vexp, vexpty = tcVal fsValRef valUseFlags (minfo.DeclaringTypeInst @ minst) m
                    BuildFSharpMethodApp g m fsValRef vexp vexpty allArgs
            | None -> 
                let ilMethRef = Import.ImportProvidedMethodBaseAsILMethodRef amap m providedMeth
                let isNewObj = isCtor && (match valUseFlags with NormalValUse -> true | _ -> false)
                let actualTypeInst = 
                    if isRefTupleTy g enclTy then argsOfAppTy g (mkCompiledTupleTy g false (destRefTupleTy g enclTy))  // provided expressions can include method calls that get properties of tuple types
                    elif isFunTy g enclTy then [ domainOfFunTy g enclTy; rangeOfFunTy g enclTy ]  // provided expressions can call Invoke
                    else minfo.DeclaringTypeInst
                let actualMethInst = minst
                let retTy = if not isCtor && (ilMethRef.ReturnType = ILType.Void) then [] else [exprTy]
                let noTailCall = false
                let expr = Expr.Op (TOp.ILCall (useCallvirt, isProtected, valu, isNewObj, valUseFlags, isProp, noTailCall, ilMethRef, actualTypeInst, actualMethInst, retTy), [], allArgs, m)
                expr, exprTy

#endif
            
        // Build a call to a .NET method 
        | ILMeth(_, ilMethInfo, _) -> 
            BuildILMethInfoCall g amap m isProp ilMethInfo valUseFlags minst direct allArgs

        // Build a call to an F# method 
        | FSMeth(_, _, vref, _) -> 

            // Go see if this is a use of a recursive definition... Note we know the value instantiation 
            // we want to use so we pass that in order not to create a new one. 
            let vexp, vexpty = tcVal vref valUseFlags (minfo.DeclaringTypeInst @ minst) m
            BuildFSharpMethodApp g m vref vexp vexpty allArgs

        // Build a 'call' to a struct default constructor 
        | DefaultStructCtor (g, ty) -> 
            if not (TypeHasDefaultValue g m ty) then 
                errorR(Error(FSComp.SR.tcDefaultStructConstructorCall(), m))
            mkDefault (m, ty), ty)

//-------------------------------------------------------------------------
// Adjust caller arguments as part of building a method call
//------------------------------------------------------------------------- 

/// Build a call to the System.Object constructor taking no arguments,
let BuildObjCtorCall (g: TcGlobals) m =
    let ilMethRef = (mkILCtorMethSpecForTy(g.ilg.typ_Object, [])).MethodRef
    Expr.Op (TOp.ILCall (false, false, false, false, CtorValUsedAsSuperInit, false, true, ilMethRef, [], [], [g.obj_ty]), [], [], m)

/// Implements the elaborated form of adhoc conversions from functions to delegates at member callsites
let BuildNewDelegateExpr (eventInfoOpt: EventInfo option, g, amap, traitCtxt, delegateTy, invokeMethInfo: MethInfo, delArgTys, f, fty, m) =
    let slotsig = invokeMethInfo.GetSlotSig(amap, m, traitCtxt)
    let delArgVals, expr = 
        let topValInfo = ValReprInfo([], List.replicate (max 1 (List.length delArgTys)) ValReprInfo.unnamedTopArg, ValReprInfo.unnamedRetVal)

        // Try to pull apart an explicit lambda and use it directly 
        // Don't do this in the case where we're adjusting the arguments of a function used to build a .NET-compatible event handler 
        let lambdaContents = 
            if Option.isSome eventInfoOpt then 
                None 
            else 
                tryDestTopLambda g amap topValInfo (f, fty)        

        match lambdaContents with 
        | None -> 
        
            if List.exists (isByrefTy g) delArgTys then
                    error(Error(FSComp.SR.tcFunctionRequiresExplicitLambda(List.length delArgTys), m)) 

            let delArgVals = delArgTys |> List.mapi (fun i argty -> fst (mkCompGenLocal m ("delegateArg" + string i) argty)) 
            let expr = 
                let args = 
                    match eventInfoOpt with 
                    | Some einfo -> 
                        match delArgVals with 
                        | [] -> error(nonStandardEventError einfo.EventName m)
                        | h :: _ when not (isObjTy g h.Type) -> error(nonStandardEventError einfo.EventName m)
                        | h :: t -> [exprForVal m h; mkRefTupledVars g m t] 
                    | None -> 
                        if isNil delArgTys then [mkUnit g m] else List.map (exprForVal m) delArgVals
                mkApps g ((f, fty), [], args, m)
            delArgVals, expr
            
        | Some _ -> 
            let _, _, _, vsl, body, _ = IteratedAdjustArityOfLambda g amap topValInfo f
            List.concat vsl, body
            
    let meth = TObjExprMethod(slotsig, [], [], [delArgVals], expr, m)
    mkObjExpr(delegateTy, None, BuildObjCtorCall g m, [meth], [], m)

let CoerceFromFSharpFuncToDelegate g amap traitCtxt infoReader ad callerArgTy m callerArgExpr delegateTy =    
    let (SigOfFunctionForDelegate(invokeMethInfo, delArgTys, _, _)) = GetSigOfFunctionForDelegate infoReader delegateTy m ad
    BuildNewDelegateExpr (None, g, amap, traitCtxt, delegateTy, invokeMethInfo, delArgTys, callerArgExpr, callerArgTy, m)

// Handle adhoc argument conversions
let AdjustCallerArgExprForCoercions (g: TcGlobals) amap traitCtxt infoReader ad isOutArg calledArgTy (reflArgInfo: ReflectedArgInfo) callerArgTy m callerArgExpr = 
   if isByrefTy g calledArgTy && isRefCellTy g callerArgTy then 
       None, Expr.Op (TOp.RefAddrGet false, [destRefCellTy g callerArgTy], [callerArgExpr], m) 

#if IMPLICIT_ADDRESS_OF
   elif isInByrefTy g calledArgTy && not (isByrefTy g callerArgTy) then 
       let wrap, callerArgExprAddress, _readonly, _writeonly = mkExprAddrOfExpr g true false NeverMutates callerArgExpr None m
       Some wrap, callerArgExprAddress
#endif

   elif isDelegateTy g calledArgTy && isFunTy g callerArgTy then 
       None, CoerceFromFSharpFuncToDelegate g amap traitCtxt infoReader ad callerArgTy m callerArgExpr calledArgTy

   elif isLinqExpressionTy g calledArgTy && isDelegateTy g (destLinqExpressionTy g calledArgTy) && isFunTy g callerArgTy then 
       let delegateTy = destLinqExpressionTy g calledArgTy
       let expr = CoerceFromFSharpFuncToDelegate g amap traitCtxt infoReader ad callerArgTy m callerArgExpr delegateTy
       None, mkCallQuoteToLinqLambdaExpression g m delegateTy (Expr.Quote (expr, ref None, false, m, mkQuotedExprTy g delegateTy))

   // auto conversions to quotations (to match auto conversions to LINQ expressions)
   elif reflArgInfo.AutoQuote && isQuotedExprTy g calledArgTy && not (isQuotedExprTy g callerArgTy) then 
       match reflArgInfo with 
       | ReflectedArgInfo.Quote true -> 
           None, mkCallLiftValueWithDefn g m calledArgTy callerArgExpr
       | ReflectedArgInfo.Quote false -> 
           None, Expr.Quote (callerArgExpr, ref None, false, m, calledArgTy)
       | ReflectedArgInfo.None -> failwith "unreachable" // unreachable due to reflArgInfo.AutoQuote condition

   // Note: out args do not need to be coerced 
   elif isOutArg then 
       None, callerArgExpr

   // Note: not all these casts are reported in quotations 
   else 
       None, mkCoerceIfNeeded g calledArgTy callerArgTy callerArgExpr

/// Some of the code below must allocate temporary variables or bind other variables to particular values. 
/// As usual we represent variable allocators by expr -> expr functions 
/// which we then use to wrap the whole expression. These will either do nothing or pre-bind a variable. It doesn't
/// matter what order they are applied in as long as they are all composed together.
let emptyPreBinder (e: Expr) = e

/// Get the expression that must be inserted on the caller side for a CallerSide optional arg,
/// i.e. one where there is no corresponding caller arg.
let rec GetDefaultExpressionForCallerSideOptionalArg tcFieldInit g (calledArg: CalledArg) currCalledArgTy currDfltVal eCallerMemberName mMethExpr =
    match currDfltVal with
    | MissingValue -> 
        // Add an I_nop if this is an initonly field to make sure we never recognize it as an lvalue. See mkExprAddrOfExpr. 
        emptyPreBinder, mkAsmExpr ([ mkNormalLdsfld (fspec_Missing_Value g); AI_nop ], [], [], [currCalledArgTy], mMethExpr)

    | DefaultValue -> 
        emptyPreBinder, mkDefault(mMethExpr, currCalledArgTy)

    | Constant fieldInit -> 
        match currCalledArgTy with
        | NullableTy g inst when fieldInit <> ILFieldInit.Null ->
            let nullableTy = mkILNonGenericBoxedTy(g.FindSysILTypeRef "System.Nullable`1")
            let ctor = mkILCtorMethSpecForTy(nullableTy, [ILType.TypeVar 0us]).MethodRef
            let ctorArgs = [Expr.Const (tcFieldInit mMethExpr fieldInit, mMethExpr, inst)]
            emptyPreBinder, Expr.Op (TOp.ILCall (false, false, true, true, NormalValUse, false, false, ctor, [inst], [], [currCalledArgTy]), [], ctorArgs, mMethExpr)
        | ByrefTy g inst ->
            GetDefaultExpressionForCallerSideOptionalArg tcFieldInit g calledArg inst (PassByRef(inst, currDfltVal)) eCallerMemberName mMethExpr
        | _ ->
            match calledArg.CallerInfo, eCallerMemberName with
            | CallerLineNumber, _ when typeEquiv g currCalledArgTy g.int_ty ->
                emptyPreBinder, Expr.Const (Const.Int32(mMethExpr.StartLine), mMethExpr, currCalledArgTy)
            | CallerFilePath, _ when typeEquiv g currCalledArgTy g.string_ty ->
                let fileName = mMethExpr.FileName |> FileSystem.GetFullPathShim |> PathMap.apply g.pathMap
                emptyPreBinder, Expr.Const (Const.String fileName, mMethExpr, currCalledArgTy)
            | CallerMemberName, Some callerName when (typeEquiv g currCalledArgTy g.string_ty) ->
                emptyPreBinder, Expr.Const (Const.String callerName, mMethExpr, currCalledArgTy)
            | _ ->
                emptyPreBinder, Expr.Const (tcFieldInit mMethExpr fieldInit, mMethExpr, currCalledArgTy)
                
    | WrapperForIDispatch ->
        match g.TryFindSysILTypeRef "System.Runtime.InteropServices.DispatchWrapper" with
        | None -> error(Error(FSComp.SR.fscSystemRuntimeInteropServicesIsRequired(), mMethExpr))
        | Some tref ->
            let ty = mkILNonGenericBoxedTy tref
            let mref = mkILCtorMethSpecForTy(ty, [g.ilg.typ_Object]).MethodRef
            let expr = Expr.Op (TOp.ILCall (false, false, false, true, NormalValUse, false, false, mref, [], [], [g.obj_ty]), [], [mkDefault(mMethExpr, currCalledArgTy)], mMethExpr)
            emptyPreBinder, expr

    | WrapperForIUnknown ->
        match g.TryFindSysILTypeRef "System.Runtime.InteropServices.UnknownWrapper" with
        | None -> error(Error(FSComp.SR.fscSystemRuntimeInteropServicesIsRequired(), mMethExpr))
        | Some tref ->
            let ty = mkILNonGenericBoxedTy tref
            let mref = mkILCtorMethSpecForTy(ty, [g.ilg.typ_Object]).MethodRef
            let expr = Expr.Op (TOp.ILCall (false, false, false, true, NormalValUse, false, false, mref, [], [], [g.obj_ty]), [], [mkDefault(mMethExpr, currCalledArgTy)], mMethExpr)
            emptyPreBinder, expr

    | PassByRef (ty, dfltVal2) ->
        let v, _ = mkCompGenLocal mMethExpr "defaultByrefArg" ty
        let wrapper2, rhs = GetDefaultExpressionForCallerSideOptionalArg tcFieldInit g calledArg currCalledArgTy dfltVal2 eCallerMemberName mMethExpr
        (wrapper2 >> mkCompGenLet mMethExpr v rhs), mkValAddr mMethExpr false (mkLocalValRef v)

/// Get the expression that must be inserted on the caller side for a CalleeSide optional arg where
/// no caller argument has been provided. Normally this is 'None', however CallerMemberName and friends
/// can be used with 'CalleeSide' optional arguments
let GetDefaultExpressionForCalleeSideOptionalArg g (calledArg: CalledArg) eCallerMemberName (mMethExpr: range) =
    let calledArgTy = calledArg.CalledArgumentType
    let calledNonOptTy = 
        if isOptionTy g calledArgTy then 
            destOptionTy g calledArgTy 
        else
            calledArgTy // should be unreachable

    match calledArg.CallerInfo, eCallerMemberName with
    | CallerLineNumber, _ when typeEquiv g calledNonOptTy g.int_ty ->
        let lineExpr = Expr.Const(Const.Int32 mMethExpr.StartLine, mMethExpr, calledNonOptTy)
        mkSome g calledNonOptTy lineExpr mMethExpr
    | CallerFilePath, _ when typeEquiv g calledNonOptTy g.string_ty ->
        let fileName = mMethExpr.FileName |> FileSystem.GetFullPathShim |> PathMap.apply g.pathMap
        let filePathExpr = Expr.Const (Const.String(fileName), mMethExpr, calledNonOptTy)
        mkSome g calledNonOptTy filePathExpr mMethExpr
    | CallerMemberName, Some(callerName) when typeEquiv g calledNonOptTy g.string_ty ->
        let memberNameExpr = Expr.Const (Const.String callerName, mMethExpr, calledNonOptTy)
        mkSome g calledNonOptTy memberNameExpr mMethExpr
    | _ ->
        mkNone g calledNonOptTy mMethExpr

/// Get the expression that must be inserted on the caller side for an optional arg where
/// no caller argument has been provided. 
let GetDefaultExpressionForOptionalArg tcFieldInit g (calledArg: CalledArg) eCallerMemberName mItem (mMethExpr: range) =
    let calledArgTy = calledArg.CalledArgumentType
    let preBinder, expr = 
        match calledArg.OptArgInfo with 
        | NotOptional -> 
            error(InternalError("Unexpected NotOptional", mItem))

        | CallerSide dfltVal ->
            GetDefaultExpressionForCallerSideOptionalArg tcFieldInit g calledArg calledArgTy dfltVal eCallerMemberName mMethExpr

        | CalleeSide ->
            emptyPreBinder, GetDefaultExpressionForCalleeSideOptionalArg g calledArg eCallerMemberName mMethExpr

    // Combine the variable allocators (if any)
    let callerArg = CallerArg(calledArgTy, mMethExpr, false, expr)
    preBinder, { NamedArgIdOpt = None; CalledArg = calledArg; CallerArg = callerArg }

let MakeNullableExprIfNeeded (infoReader: InfoReader) calledArgTy callerArgTy callerArgExpr m =
    let g = infoReader.g
    let amap = infoReader.amap
    if isNullableTy g callerArgTy then 
        callerArgExpr
    else
        let calledNonOptTy = destNullableTy g calledArgTy 
        let minfo = GetIntrinsicConstructorInfosOfType infoReader m calledArgTy |> List.head
        let callerArgExprCoerced = mkCoerceIfNeeded g calledNonOptTy callerArgTy callerArgExpr
        MakeMethInfoCall amap m minfo [] [callerArgExprCoerced]

// Adjust all the optional arguments, filling in values for defaults, 
let AdjustCallerArgForOptional tcFieldInit eCallerMemberName (infoReader: InfoReader) (assignedArg: AssignedCalledArg<_>) =
    let g = infoReader.g
    let callerArg = assignedArg.CallerArg
    let (CallerArg(callerArgTy, m, isOptCallerArg, callerArgExpr)) = callerArg
    let calledArg = assignedArg.CalledArg
    let calledArgTy = calledArg.CalledArgumentType
    match calledArg.OptArgInfo with
    | NotOptional when not (g.langVersion.SupportsFeature LanguageFeature.NullableOptionalInterop) ->
        if isOptCallerArg then errorR(Error(FSComp.SR.tcFormalArgumentIsNotOptional(), m))
        assignedArg

    // For non-nullable, non-optional arguments no conversion is needed.
    // We return precisely the assignedArg.  This also covers the case where there
    // can be a lingering permitted type mismatch between caller argument and called argument, 
    // specifically caller can by `byref` and called `outref`.  No coercion is inserted in the
    // expression tree in this case. 
    | NotOptional when not (isNullableTy g calledArgTy) -> 
        if isOptCallerArg then errorR(Error(FSComp.SR.tcFormalArgumentIsNotOptional(), m))
        assignedArg

    | _ ->

        let callerArgExpr2 = 
            match calledArg.OptArgInfo with 
            | NotOptional ->
                //  T --> Nullable<T> widening at callsites
                if isOptCallerArg then errorR(Error(FSComp.SR.tcFormalArgumentIsNotOptional(), m))
                if isNullableTy g calledArgTy then 
                    MakeNullableExprIfNeeded infoReader calledArgTy callerArgTy callerArgExpr m
                else
                    failwith "unreachable" // see case above
            
            | CallerSide dfltVal -> 
                let calledArgTy = calledArg.CalledArgumentType

                if isOptCallerArg then 
                    // CSharpMethod(?x=b) 
                    if isOptionTy g callerArgTy then 
                        if isNullableTy g calledArgTy then 
                            // CSharpMethod(?x=b) when 'b' has optional type and 'x' has nullable type --> CSharpMethod(x=Option.toNullable b)
                            mkOptionToNullable g m (destOptionTy g callerArgTy) callerArgExpr
                        else 
                            // CSharpMethod(?x=b) when 'b' has optional type and 'x' has non-nullable type --> CSharpMethod(x=Option.defaultValue DEFAULT v)
                            let _wrapper, defaultExpr = GetDefaultExpressionForCallerSideOptionalArg tcFieldInit g calledArg calledArgTy dfltVal eCallerMemberName m
                            let ty = destOptionTy g callerArgTy
                            mkOptionDefaultValue g m ty defaultExpr callerArgExpr
                    else
                        // This should be unreachable but the error will be reported elsewhere
                        callerArgExpr
                else
                    if isNullableTy g calledArgTy  then 
                        // CSharpMethod(x=b) when 'x' has nullable type
                        // CSharpMethod(x=b) when both 'x' and 'b' have nullable type --> CSharpMethod(x=b)
                        // CSharpMethod(x=b) when 'x' has nullable type and 'b' does not --> CSharpMethod(x=Nullable(b))
                        MakeNullableExprIfNeeded infoReader calledArgTy callerArgTy callerArgExpr m
                    else 
                        // CSharpMethod(x=b) --> CSharpMethod(?x=b)
                        callerArgExpr

            | CalleeSide -> 
                if isOptCallerArg then 
                    // CSharpMethod(?x=b) --> CSharpMethod(?x=b)
                    callerArgExpr 
                else                            
                    // CSharpMethod(x=b) when CSharpMethod(A) --> CSharpMethod(?x=Some(b :> A))
                    if isOptionTy g calledArgTy then 
                        let calledNonOptTy = destOptionTy g calledArgTy 
                        let callerArgExprCoerced = mkCoerceIfNeeded g calledNonOptTy callerArgTy callerArgExpr
                        mkSome g calledNonOptTy callerArgExprCoerced m
                    else 
                        assert false
                        callerArgExpr // defensive code - this case is unreachable 
                        
        let callerArg2 = CallerArg(tyOfExpr g callerArgExpr2, m, isOptCallerArg, callerArgExpr2)
        { assignedArg with CallerArg=callerArg2 }

// Handle CallerSide optional arguments. 
//
// CallerSide optional arguments are largely for COM interop, e.g. to PIA assemblies for Word etc.
// As a result we follow the VB and C# behavior here.
//
//   "1. If the parameter is statically typed as System.Object and does not have a value, then there are four cases:
//       a. The parameter is marked with MarshalAs(IUnknown), MarshalAs(Interface), or MarshalAs(IDispatch). In this case we pass null.
//       b. Else if the parameter is marked with IUnknownConstantAttribute. In this case we pass new System.Runtime.InteropServices.UnknownWrapper(null)
//       c. Else if the parameter is marked with IDispatchConstantAttribute. In this case we pass new System.Runtime.InteropServices.DispatchWrapper(null)
//       d. Else, we will pass Missing.Value.
//    2. Otherwise, if there is a value attribute, then emit the default value.
//    3. Otherwise, we emit default(T).
//    4. Finally, we apply conversions from the value to the parameter type. This is where the nullable conversions take place for VB.
//    - VB allows you to mark ref parameters as optional. The semantics of this is that we create a temporary 
//        with type = type of parameter, load the optional value to it, and call the method. 
//    - VB also allows you to mark arrays with Nothing as the optional value.
//    - VB also allows you to pass intrinsic values as optional values to parameters 
//        typed as Object. What we do in this case is we box the intrinsic value."
//
let AdjustCallerArgsForOptionals tcFieldInit eCallerMemberName (infoReader: InfoReader) (calledMeth: CalledMeth<_>) mItem mMethExpr =
    let g = infoReader.g

    let assignedNamedArgs = calledMeth.ArgSets |> List.collect (fun argSet -> argSet.AssignedNamedArgs)
    let unnamedCalledArgs = calledMeth.ArgSets |> List.collect (fun argSet -> argSet.UnnamedCalledArgs)
    let unnamedCallerArgs = calledMeth.ArgSets |> List.collect (fun argSet -> argSet.UnnamedCallerArgs)
    let unnamedArgs =
        (unnamedCalledArgs, unnamedCallerArgs) ||> List.map2 (fun called caller -> 
            { NamedArgIdOpt = None; CalledArg=called; CallerArg=caller })

    // Adjust all the optional arguments that require a default value to be inserted into the call,
    // i.e. there is no corresponding caller arg.
    let optArgs, optArgPreBinder = 
        (emptyPreBinder, calledMeth.UnnamedCalledOptArgs) ||> List.mapFold (fun preBinder calledArg -> 
            let preBinder2, arg = GetDefaultExpressionForOptionalArg tcFieldInit g calledArg eCallerMemberName mItem mMethExpr
            arg, (preBinder >> preBinder2))

    let adjustedNormalUnnamedArgs = List.map (AdjustCallerArgForOptional tcFieldInit eCallerMemberName infoReader) unnamedArgs
    let adjustedAssignedNamedArgs = List.map (AdjustCallerArgForOptional tcFieldInit eCallerMemberName infoReader) assignedNamedArgs

    optArgs, optArgPreBinder, adjustedNormalUnnamedArgs, adjustedAssignedNamedArgs

/// Adjust any 'out' arguments, passing in the address of a mutable local
let AdjustOutCallerArgs g (calledMeth: CalledMeth<_>) mMethExpr =
    calledMeth.UnnamedCalledOutArgs |> List.map (fun calledArg -> 
        let calledArgTy = calledArg.CalledArgumentType
        let outArgTy = destByrefTy g calledArgTy
        let outv, outArgExpr = mkMutableCompGenLocal mMethExpr PrettyNaming.outArgCompilerGeneratedName outArgTy // mutable! 
        let expr = mkDefault (mMethExpr, outArgTy)
        let callerArg = CallerArg (calledArgTy, mMethExpr, false, mkValAddr mMethExpr false (mkLocalValRef outv))
        let outArg = { NamedArgIdOpt=None;CalledArg=calledArg;CallerArg=callerArg }
        outArg, outArgExpr, mkCompGenBind outv expr) 
        |> List.unzip3

/// Adjust any '[<ParamArray>]' arguments, converting to an array
let AdjustParamArrayCallerArgs g amap traitCtxt infoReader ad (calledMeth: CalledMeth<_>) mMethExpr =
    let argSets = calledMeth.ArgSets

    let paramArrayCallerArgs = argSets |> List.collect (fun argSet -> argSet.ParamArrayCallerArgs)

    match calledMeth.ParamArrayCalledArgOpt with 
    | None -> 
        [], []

    | Some paramArrayCalledArg -> 
        let paramArrayCalledArgElementType = destArrayTy g paramArrayCalledArg.CalledArgumentType

        let paramArrayPreBinders, paramArrayExprs = 
            paramArrayCallerArgs  
            |> List.map (fun callerArg -> 
                let (CallerArg(callerArgTy, m, isOutArg, callerArgExpr)) = callerArg
                AdjustCallerArgExprForCoercions g amap traitCtxt infoReader ad isOutArg paramArrayCalledArgElementType paramArrayCalledArg.ReflArgInfo callerArgTy m callerArgExpr)
            |> List.unzip

        let paramArrayExpr = Expr.Op (TOp.Array, [paramArrayCalledArgElementType], paramArrayExprs, mMethExpr)
        
        let paramArrayCallerArg = 
            [ { NamedArgIdOpt = None
                CalledArg=paramArrayCalledArg
                CallerArg=CallerArg(paramArrayCalledArg.CalledArgumentType, mMethExpr, false, paramArrayExpr) } ]

        paramArrayPreBinders, paramArrayCallerArg

/// Build the argument list for a method call. Adjust for param array, optional arguments, byref arguments and coercions.
/// For example, if you pass an F# reference cell to a byref then we must get the address of the 
/// contents of the ref. Likewise lots of adjustments are made for optional arguments etc.
let AdjustCallerArgs tcFieldInit eCallerMemberName (infoReader: InfoReader) ad traitCtxt (calledMeth: CalledMeth<_>) objArgs lambdaVars mItem mMethExpr =
    let g = infoReader.g
    let amap = infoReader.amap
    let calledMethInfo = calledMeth.Method

    // For unapplied 'e.M' we first evaluate 'e' outside the lambda, i.e. 'let v = e in (fun arg -> v.CSharpMethod(arg))' 
    let objArgPreBinder, objArgs = 
        match objArgs, lambdaVars with 
        | [objArg], Some _ -> 
            if calledMethInfo.IsExtensionMember && calledMethInfo.ObjArgNeedsAddress(amap, mMethExpr) then
                error(Error(FSComp.SR.tcCannotPartiallyApplyExtensionMethodForByref(calledMethInfo.DisplayName), mMethExpr))
            let objArgTy = tyOfExpr g objArg
            let v, ve = mkCompGenLocal mMethExpr "objectArg" objArgTy
            (fun body -> mkCompGenLet mMethExpr v objArg body), [ve]
        | _ -> 
            emptyPreBinder, objArgs

    // Handle param array and optional arguments
    let paramArrayPreBinders, paramArrayArgs =
        AdjustParamArrayCallerArgs g amap traitCtxt infoReader ad calledMeth mMethExpr

    let optArgs, optArgPreBinder, adjustedNormalUnnamedArgs, adjustedFinalAssignedNamedArgs = 
        AdjustCallerArgsForOptionals tcFieldInit eCallerMemberName infoReader calledMeth mItem mMethExpr

    let outArgs, outArgExprs, outArgTmpBinds =
        AdjustOutCallerArgs g calledMeth mMethExpr

    let allArgs =
        adjustedNormalUnnamedArgs @
        adjustedFinalAssignedNamedArgs @
        paramArrayArgs @
        optArgs @ 
        outArgs
        
    let allArgs = 
        allArgs |> List.sortBy (fun x -> x.Position)

    let allArgsPreBinders, allArgsCoerced = 
        allArgs
        |> List.map (fun assignedArg -> 
            let isOutArg = assignedArg.CalledArg.IsOutArg
            let reflArgInfo = assignedArg.CalledArg.ReflArgInfo
            let calledArgTy = assignedArg.CalledArg.CalledArgumentType
            let (CallerArg(callerArgTy, m, _, e)) = assignedArg.CallerArg
    
            AdjustCallerArgExprForCoercions g amap traitCtxt infoReader ad isOutArg calledArgTy reflArgInfo callerArgTy m e)
        |> List.unzip

    objArgPreBinder, objArgs, allArgsPreBinders, allArgs, allArgsCoerced, optArgPreBinder, paramArrayPreBinders, outArgExprs, outArgTmpBinds


//-------------------------------------------------------------------------
// Import provided expressions
//------------------------------------------------------------------------- 


#if !NO_EXTENSIONTYPING
// This file is not a great place for this functionality to sit, it's here because of BuildMethodCall
module ProvidedMethodCalls =

    let private convertConstExpr g amap m (constant : Tainted<obj * ProvidedType>) =
        let (obj, objTy) = constant.PApply2(id, m)
        let ty = Import.ImportProvidedType amap m objTy
        let normTy = normalizeEnumTy g ty
        obj.PUntaint((fun v ->
            let fail() = raise (TypeProviderError(FSComp.SR.etUnsupportedConstantType(v.GetType().ToString()), constant.TypeProviderDesignation, m))
            try 
                if isNull v then mkNull m ty else
                let c = 
                    match v with
                    | _ when typeEquiv g normTy g.bool_ty -> Const.Bool(v :?> bool)
                    | _ when typeEquiv g normTy g.sbyte_ty -> Const.SByte(v :?> sbyte)
                    | _ when typeEquiv g normTy g.byte_ty -> Const.Byte(v :?> byte)
                    | _ when typeEquiv g normTy g.int16_ty -> Const.Int16(v :?> int16)
                    | _ when typeEquiv g normTy g.uint16_ty -> Const.UInt16(v :?> uint16)
                    | _ when typeEquiv g normTy g.int32_ty -> Const.Int32(v :?> int32)
                    | _ when typeEquiv g normTy g.uint32_ty -> Const.UInt32(v :?> uint32)
                    | _ when typeEquiv g normTy g.int64_ty -> Const.Int64(v :?> int64)
                    | _ when typeEquiv g normTy g.uint64_ty -> Const.UInt64(v :?> uint64)
                    | _ when typeEquiv g normTy g.nativeint_ty -> Const.IntPtr(v :?> int64)
                    | _ when typeEquiv g normTy g.unativeint_ty -> Const.UIntPtr(v :?> uint64)
                    | _ when typeEquiv g normTy g.float32_ty -> Const.Single(v :?> float32)
                    | _ when typeEquiv g normTy g.float_ty -> Const.Double(v :?> float)
                    | _ when typeEquiv g normTy g.char_ty -> Const.Char(v :?> char)
                    | _ when typeEquiv g normTy g.string_ty -> Const.String(v :?> string)
                    | _ when typeEquiv g normTy g.decimal_ty -> Const.Decimal(v :?> decimal)
                    | _ when typeEquiv g normTy g.unit_ty -> Const.Unit
                    | _ -> fail()
                Expr.Const (c, m, ty)
             with _ -> fail()
            ), range=m)

    /// Erasure over System.Type.
    ///
    /// This is a reimplementation of the logic of provided-type erasure, working entirely over (tainted, provided) System.Type
    /// values. This is used when preparing ParameterInfo objects to give to the provider in GetInvokerExpression. 
    /// These ParameterInfo have erased ParameterType - giving the provider an erased type makes it considerably easier 
    /// to implement a correct GetInvokerExpression.
    ///
    /// Ideally we would implement this operation by converting to an F# TType using ImportSystemType, and then erasing, and then converting
    /// back to System.Type. However, there is currently no way to get from an arbitrary F# TType (even the TType for 
    /// System.Object) to a System.Type to give to the type provider.
    let eraseSystemType (amap, m, inputType) = 
        let rec loop (st: Tainted<ProvidedType>) = 
            if st.PUntaint((fun st -> st.IsGenericParameter), m) then st
            elif st.PUntaint((fun st -> st.IsArray), m) then 
                let et = st.PApply((fun st -> st.GetElementType()), m)
                let rank = st.PUntaint((fun st -> st.GetArrayRank()), m)
                (loop et).PApply((fun st -> if rank = 1 then st.MakeArrayType() else st.MakeArrayType(rank)), m)
            elif st.PUntaint((fun st -> st.IsByRef), m) then 
                let et = st.PApply((fun st -> st.GetElementType()), m)
                (loop et).PApply((fun st -> st.MakeByRefType()), m)
            elif st.PUntaint((fun st -> st.IsPointer), m) then 
                let et = st.PApply((fun st -> st.GetElementType()), m)
                (loop et).PApply((fun st -> st.MakePointerType()), m)
            else
                let isGeneric = st.PUntaint((fun st -> st.IsGenericType), m)
                let headType = if isGeneric then st.PApply((fun st -> st.GetGenericTypeDefinition()), m) else st
                // We import in order to use IsProvidedErasedTycon, to make sure we at least don't reinvent that 
                let headTypeAsFSharpType = Import.ImportProvidedNamedType amap m headType
                if headTypeAsFSharpType.IsProvidedErasedTycon then 
                    let baseType = 
                        st.PApply((fun st -> 
                            match st.BaseType with 
                            | null -> ProvidedType.CreateNoContext(typeof<obj>)  // it might be an interface
                            | st -> st), m)
                    loop baseType
                else
                    if isGeneric then 
                        let genericArgs = st.PApplyArray((fun st -> st.GetGenericArguments()), "GetGenericArguments", m) 
                        let typars = headTypeAsFSharpType.Typars(m)
                        // Drop the generic arguments that don't correspond to type arguments, i.e. are units-of-measure
                        let genericArgs = 
                            [| for (genericArg, tp) in Seq.zip genericArgs typars do
                                   if tp.Kind = TyparKind.Type then 
                                       yield genericArg |]

                        if genericArgs.Length = 0 then
                            headType
                        else
                            let erasedArgTys = genericArgs |> Array.map loop
                            headType.PApply((fun st -> 
                                let erasedArgTys = erasedArgTys |> Array.map (fun a -> a.PUntaintNoFailure(id))
                                st.MakeGenericType erasedArgTys), m)
                    else   
                        st
        loop inputType

    let convertProvidedExpressionToExprAndWitness
            tcVal
            (thisArg: Expr option,
             allArgs: Exprs,
             paramVars: Tainted<ProvidedVar>[],
             g,
             amap, 
             traitCtxt,
             mut,
             isProp,
             isSuperInit, m,
             expr: Tainted<ProvidedExpr>) = 

        let varConv =
            // note: using paramVars.Length as assumed initial size, but this might not 
            // be the optimal value; this wasn't checked before obsoleting Dictionary.ofList
            let dict = Dictionary.newWithSize paramVars.Length
            for v, e in Seq.zip (paramVars |> Seq.map (fun x -> x.PUntaint(id, m))) (Option.toList thisArg @ allArgs) do
                dict.Add(v, (None, e))
            dict
        let rec exprToExprAndWitness top (ea: Tainted<ProvidedExpr>) =
            let fail() = error(Error(FSComp.SR.etUnsupportedProvidedExpression(ea.PUntaint((fun etree -> etree.UnderlyingExpressionString), m)), m))
            match ea with
            | Tainted.Null -> error(Error(FSComp.SR.etNullProvidedExpression(ea.TypeProviderDesignation), m))
            |  _ ->
            let exprType = ea.PApplyOption((fun x -> x.GetExprType()), m)
            let exprType = match exprType with | Some exprType -> exprType | None -> fail()
            match exprType.PUntaint(id, m) with
            | ProvidedTypeAsExpr (expr, targetTy) ->
                let (expr, targetTy) = exprType.PApply2((fun _ -> (expr, targetTy)), m)
                let srcExpr = exprToExpr expr
                let targetTy = Import.ImportProvidedType amap m (targetTy.PApply(id, m)) 
                let sourceTy = Import.ImportProvidedType amap m (expr.PApply ((fun e -> e.Type), m)) 
                let te = mkCoerceIfNeeded g targetTy sourceTy srcExpr
                None, (te, tyOfExpr g te)
            | ProvidedTypeTestExpr (expr, targetTy) ->
                let (expr, targetTy) = exprType.PApply2((fun _ -> (expr, targetTy)), m)
                let srcExpr = exprToExpr expr
                let targetTy = Import.ImportProvidedType amap m (targetTy.PApply(id, m)) 
                let te = mkCallTypeTest g m targetTy srcExpr
                None, (te, tyOfExpr g te)
            | ProvidedIfThenElseExpr (test, thenBranch, elseBranch) ->
                let test, thenBranch, elseBranch = exprType.PApply3((fun _ -> (test, thenBranch, elseBranch)), m)
                let testExpr = exprToExpr test
                let ifTrueExpr = exprToExpr thenBranch
                let ifFalseExpr = exprToExpr elseBranch
                let te = mkCond DebugPointAtBinding.NoneAtSticky DebugPointForTarget.No m (tyOfExpr g ifTrueExpr) testExpr ifTrueExpr ifFalseExpr
                None, (te, tyOfExpr g te)
            | ProvidedVarExpr providedVar ->
                let _, vTe = varToExpr (exprType.PApply((fun _ -> providedVar), m))
                None, (vTe, tyOfExpr g vTe)
            | ProvidedConstantExpr (obj, prType) ->
                let ce = convertConstExpr g amap m (exprType.PApply((fun _ -> (obj, prType)), m))
                None, (ce, tyOfExpr g ce)
            | ProvidedNewTupleExpr info ->
                let elems = exprType.PApplyArray((fun _ -> info), "GetInvokerExpression", m)
                let elemsT = elems |> Array.map exprToExpr |> Array.toList
                let exprT = mkRefTupledNoTypes g m elemsT
                None, (exprT, tyOfExpr g exprT)
            | ProvidedNewArrayExpr (ty, elems) ->
                let ty, elems = exprType.PApply2((fun _ -> (ty, elems)), m)
                let tyT = Import.ImportProvidedType amap m ty
                let elems = elems.PApplyArray(id, "GetInvokerExpression", m)
                let elemsT = elems |> Array.map exprToExpr |> Array.toList
                let exprT = Expr.Op (TOp.Array, [tyT], elemsT, m)
                None, (exprT, tyOfExpr g exprT)
            | ProvidedTupleGetExpr (inp, n) -> 
                let inp, n = exprType.PApply2((fun _ -> (inp, n)), m)
                let inpT = inp |> exprToExpr 
                // if type of expression is erased type then we need convert it to the underlying base type
                let typeOfExpr =
                    let t = tyOfExpr g inpT
                    stripTyEqnsWrtErasure EraseMeasures g t
                let tupInfo, tysT = tryDestAnyTupleTy g typeOfExpr
                let exprT = mkTupleFieldGet g (tupInfo, inpT, tysT, n.PUntaint(id, m), m)
                None, (exprT, tyOfExpr g exprT)
            | ProvidedLambdaExpr (v, b) ->
                let v, b = exprType.PApply2((fun _ -> (v, b)), m)
                let vT = addVar v
                let bT = exprToExpr b
                removeVar v
                let exprT = mkLambda m vT (bT, tyOfExpr g bT)
                None, (exprT, tyOfExpr g exprT)
            | ProvidedLetExpr (v, e, b) ->
                let v, e, b = exprType.PApply3((fun _ -> (v, e, b)), m)
                let eT = exprToExpr  e
                let vT = addVar v
                let bT = exprToExpr  b
                removeVar v
                let exprT = mkCompGenLet m vT eT bT
                None, (exprT, tyOfExpr g exprT)
            | ProvidedVarSetExpr (v, e) ->
                let v, e = exprType.PApply2((fun _ -> (v, e)), m)
                let eT = exprToExpr  e
                let vTopt, _ = varToExpr v
                match vTopt with 
                | None -> 
                    fail()
                | Some vT ->
                    let exprT = mkValSet m (mkLocalValRef vT) eT 
                    None, (exprT, tyOfExpr g exprT)
            | ProvidedWhileLoopExpr (guardExpr, bodyExpr) ->
                let guardExpr, bodyExpr = (exprType.PApply2((fun _ -> (guardExpr, bodyExpr)), m))
                let guardExprT = exprToExpr guardExpr
                let bodyExprT = exprToExpr bodyExpr
                let exprT = mkWhile g (DebugPointAtWhile.No, SpecialWhileLoopMarker.NoSpecialWhileLoopMarker, guardExprT, bodyExprT, m)
                None, (exprT, tyOfExpr g exprT)
            | ProvidedForIntegerRangeLoopExpr (v, e1, e2, e3) -> 
                let v, e1, e2, e3 = exprType.PApply4((fun _ -> (v, e1, e2, e3)), m)
                let e1T = exprToExpr  e1
                let e2T = exprToExpr  e2
                let vT = addVar v
                let e3T = exprToExpr  e3
                removeVar v
                let exprT = mkFastForLoop g (DebugPointAtFor.No, m, vT, e1T, true, e2T, e3T)
                None, (exprT, tyOfExpr g exprT)
            | ProvidedNewDelegateExpr (delegateTy, boundVars, delegateBodyExpr) ->
                let delegateTy, boundVars, delegateBodyExpr = exprType.PApply3((fun _ -> (delegateTy, boundVars, delegateBodyExpr)), m)
                let delegateTyT = Import.ImportProvidedType amap m delegateTy
                let vs = boundVars.PApplyArray(id, "GetInvokerExpression", m) |> Array.toList 
                let vsT = List.map addVar vs
                let delegateBodyExprT = exprToExpr delegateBodyExpr
                List.iter removeVar vs
                let lambdaExpr = mkLambdas m [] vsT (delegateBodyExprT, tyOfExpr g delegateBodyExprT)
                let lambdaExprTy = tyOfExpr g lambdaExpr
                let infoReader = InfoReader(g, amap)
                let exprT = CoerceFromFSharpFuncToDelegate g amap traitCtxt infoReader AccessorDomain.AccessibleFromSomewhere lambdaExprTy m lambdaExpr delegateTyT
                None, (exprT, tyOfExpr g exprT)
#if PROVIDED_ADDRESS_OF
            | ProvidedAddressOfExpr e ->
                let eT =  exprToExpr (exprType.PApply((fun _ -> e), m))
                let wrap,ce, _readonly, _writeonly = mkExprAddrOfExpr g true false DefinitelyMutates eT None m
                let ce = wrap ce
                None, (ce, tyOfExpr g ce)
#endif
            | ProvidedDefaultExpr pty ->
                let ty = Import.ImportProvidedType amap m (exprType.PApply((fun _ -> pty), m))
                let ce = mkDefault (m, ty)
                None, (ce, tyOfExpr g ce)
            | ProvidedCallExpr (e1, e2, e3) ->
                methodCallToExpr top ea (exprType.PApply((fun _ -> (e1, e2, e3)), m))
            | ProvidedSequentialExpr (e1, e2) ->
                let e1, e2 = exprType.PApply2((fun _ -> (e1, e2)), m)
                let e1T = exprToExpr e1
                let e2T = exprToExpr e2
                let ce = mkCompGenSequential m e1T e2T
                None, (ce, tyOfExpr g ce)
            | ProvidedTryFinallyExpr (e1, e2) ->
                let e1, e2 = exprType.PApply2((fun _ -> (e1, e2)), m)
                let e1T = exprToExpr e1
                let e2T = exprToExpr e2
                let ce = mkTryFinally g (e1T, e2T, m, tyOfExpr g e1T, DebugPointAtTry.No, DebugPointAtFinally.No)
                None, (ce, tyOfExpr g ce)
            | ProvidedTryWithExpr (e1, e2, e3, e4, e5) ->
                let info = exprType.PApply((fun _ -> (e1, e2, e3, e4, e5)), m)
                let bT = exprToExpr (info.PApply((fun (x, _, _, _, _) -> x), m))
                let v1 = info.PApply((fun (_, x, _, _, _) -> x), m)
                let v1T = addVar v1
                let e1T = exprToExpr (info.PApply((fun (_, _, x, _, _) -> x), m))
                removeVar v1
                let v2 = info.PApply((fun (_, _, _, x, _) -> x), m)
                let v2T = addVar v2
                let e2T = exprToExpr (info.PApply((fun (_, _, _, _, x) -> x), m))
                removeVar v2
                let ce = mkTryWith g (bT, v1T, e1T, v2T, e2T, m, tyOfExpr g bT, DebugPointAtTry.No, DebugPointAtWith.No)
                None, (ce, tyOfExpr g ce)
            | ProvidedNewObjectExpr (e1, e2) ->
                None, ctorCallToExpr (exprType.PApply((fun _ -> (e1, e2)), m))


        and ctorCallToExpr (ne: Tainted<_>) =    
            let (ctor, args) = ne.PApply2(id, m)
            let targetMethInfo = ProvidedMeth(amap, ctor.PApply((fun ne -> upcast ne), m), None, m)
            let objArgs = [] 
            let arguments = [ for ea in args.PApplyArray(id, "GetInvokerExpression", m) -> exprToExpr ea ]
            let callExpr = BuildMethodCall tcVal g amap Mutates.PossiblyMutates m false targetMethInfo isSuperInit [] objArgs arguments
            callExpr

        and addVar (v: Tainted<ProvidedVar>) =    
            let nm = v.PUntaint ((fun v -> v.Name), m)
            let mut = v.PUntaint ((fun v -> v.IsMutable), m)
            let vRaw = v.PUntaint (id, m)
            let tyT = Import.ImportProvidedType amap m (v.PApply ((fun v -> v.Type), m))
            let vT, vTe = if mut then mkMutableCompGenLocal m nm tyT else mkCompGenLocal m nm tyT
            varConv.[vRaw] <- (Some vT, vTe)
            vT

        and removeVar (v: Tainted<ProvidedVar>) =    
            let vRaw = v.PUntaint (id, m)
            varConv.Remove vRaw |> ignore

        and methodCallToExpr top _origExpr (mce: Tainted<_>) =    
            let (objOpt, meth, args) = mce.PApply3(id, m)
            let targetMethInfo = ProvidedMeth(amap, meth.PApply((fun mce -> upcast mce), m), None, m)
            let objArgs = 
                match objOpt.PApplyOption(id, m) with
                | None -> []
                | Some objExpr -> [exprToExpr objExpr]

            let arguments = [ for ea in args.PApplyArray(id, "GetInvokerExpression", m) -> exprToExpr ea ]
            let genericArguments = 
                if meth.PUntaint((fun m -> m.IsGenericMethod), m) then 
                    meth.PApplyArray((fun m -> m.GetGenericArguments()), "GetGenericArguments", m)  
                else 
                    [| |]
            let replacementGenericArguments = genericArguments |> Array.map (fun t->Import.ImportProvidedType amap m t) |> List.ofArray

            let mut         = if top then mut else PossiblyMutates
            let isSuperInit = if top then isSuperInit else ValUseFlag.NormalValUse
            let isProp      = if top then isProp else false
            let callExpr = BuildMethodCall tcVal g amap mut m isProp targetMethInfo isSuperInit replacementGenericArguments objArgs arguments
            Some meth, callExpr

        and varToExpr (pe: Tainted<ProvidedVar>) =    
            // sub in the appropriate argument
            // REVIEW: "thisArg" pointer should be first, if present
            let vRaw = pe.PUntaint(id, m)
            match varConv.TryGetValue vRaw with
            | true, v -> v
            | _ ->
                let typeProviderDesignation = ExtensionTyping.DisplayNameOfTypeProvider (pe.TypeProvider, m)
                error(Error(FSComp.SR.etIncorrectParameterExpression(typeProviderDesignation, vRaw.Name), m))
                
        and exprToExpr expr =
            let _, (resExpr, _) = exprToExprAndWitness false expr
            resExpr

        exprToExprAndWitness true expr

        
    // fill in parameter holes in the expression   
    let TranslateInvokerExpressionForProvidedMethodCall tcVal (g, amap, traitCtxt, mut, isProp, isSuperInit, mi: Tainted<ProvidedMethodBase>, objArgs, allArgs, m) =        
        let parameters = 
            mi.PApplyArray((fun mi -> mi.GetParameters()), "GetParameters", m)
        let paramTys = 
            parameters
            |> Array.map (fun p -> p.PApply((fun st -> st.ParameterType), m))
        let erasedParamTys = 
            paramTys
            |> Array.map (fun pty -> eraseSystemType (amap, m, pty))
        let paramVars = 
            erasedParamTys
            |> Array.mapi (fun i erasedParamTy -> erasedParamTy.PApply((fun ty -> ty.AsProvidedVar("arg" + i.ToString())), m))


        // encode "this" as the first ParameterExpression, if applicable
        let thisArg, paramVars = 
            match objArgs with
            | [objArg] -> 
                let erasedThisTy = eraseSystemType (amap, m, mi.PApply((fun mi -> mi.DeclaringType), m))
                let thisVar = erasedThisTy.PApply((fun ty -> ty.AsProvidedVar("this")), m)
                Some objArg, Array.append [| thisVar |] paramVars
            | [] -> None, paramVars
            | _ -> failwith "multiple objArgs?"
            
        let ea = mi.PApplyWithProvider((fun (methodInfo, provider) -> ExtensionTyping.GetInvokerExpression(provider, methodInfo, [| for p in paramVars -> p.PUntaintNoFailure id |])), m)

        convertProvidedExpressionToExprAndWitness tcVal (thisArg, allArgs, paramVars, g, amap, traitCtxt, mut, isProp, isSuperInit, m, ea)

            
    let BuildInvokerExpressionForProvidedMethodCall tcVal (g, amap, traitCtxt, mi: Tainted<ProvidedMethodBase>, objArgs, mut, isProp, isSuperInit, allArgs, m) =
        try                   
            let methInfoOpt, (expr, retTy) = TranslateInvokerExpressionForProvidedMethodCall tcVal (g, amap, traitCtxt, mut, isProp, isSuperInit, mi, objArgs, allArgs, m)

            let exprty = GetCompiledReturnTyOfProvidedMethodInfo amap m mi |> GetFSharpViewOfReturnType g
            let expr = mkCoerceIfNeeded g exprty retTy expr
            methInfoOpt, expr, exprty
        with
            | :? TypeProviderError as tpe ->
                let typeName = mi.PUntaint((fun mb -> mb.DeclaringType.FullName), m)
                let methName = mi.PUntaint((fun mb -> mb.Name), m)
                raise( tpe.WithContext(typeName, methName) )  // loses original stack trace
#endif

let RecdFieldInstanceChecks g amap ad m (rfinfo: RecdFieldInfo) = 
    if rfinfo.IsStatic then error (Error (FSComp.SR.tcStaticFieldUsedWhenInstanceFieldExpected(), m))
    CheckRecdFieldInfoAttributes g rfinfo m |> CommitOperationResult        
    CheckRecdFieldInfoAccessible amap m ad rfinfo

let ILFieldStaticChecks g amap infoReader ad m (finfo : ILFieldInfo) =
    CheckILFieldInfoAccessible g amap m ad finfo
    if not finfo.IsStatic then error (Error (FSComp.SR.tcFieldIsNotStatic(finfo.FieldName), m))

    // Static IL interfaces fields are not supported in lower F# versions.
    if isInterfaceTy g finfo.ApparentEnclosingType then    
        checkLanguageFeatureRuntimeErrorRecover infoReader LanguageFeature.DefaultInterfaceMemberConsumption m
        checkLanguageFeatureErrorRecover g.langVersion LanguageFeature.DefaultInterfaceMemberConsumption m

    CheckILFieldAttributes g finfo m

let ILFieldInstanceChecks  g amap ad m (finfo : ILFieldInfo) =
    if finfo.IsStatic then error (Error (FSComp.SR.tcStaticFieldUsedWhenInstanceFieldExpected(), m))
    CheckILFieldInfoAccessible g amap m ad finfo
    CheckILFieldAttributes g finfo m

let MethInfoChecks g amap isInstance tyargsOpt objArgs ad m (minfo: MethInfo)  =
    if minfo.IsInstance <> isInstance then
      if isInstance then 
        error (Error (FSComp.SR.csMethodIsNotAnInstanceMethod(minfo.LogicalName), m))
      else        
        error (Error (FSComp.SR.csMethodIsNotAStaticMethod(minfo.LogicalName), m))

    // keep the original accessibility domain to determine type accessibility
    let adOriginal = ad
    // Eliminate the 'protected' portion of the accessibility domain for instance accesses    
    let ad = 
        match objArgs, ad with 
        | [objArg], AccessibleFrom(paths, Some tcref) -> 
            let objArgTy = tyOfExpr g objArg 
            let ty = generalizedTyconRef tcref
            // We get to keep our rights if the type we're in subsumes the object argument type
            if TypeFeasiblySubsumesType 0 g amap m ty CanCoerce objArgTy then
                ad
            // We get to keep our rights if this is a base call
            elif IsBaseCall objArgs then 
                ad
            else
                AccessibleFrom(paths, None) 
        | _ -> ad

    if not (IsTypeAndMethInfoAccessible amap m adOriginal ad minfo) then 
      error (Error (FSComp.SR.tcMethodNotAccessible(minfo.LogicalName), m))

    if isAnyTupleTy g minfo.ApparentEnclosingType && not minfo.IsExtensionMember &&
        (minfo.LogicalName.StartsWithOrdinal("get_Item") || minfo.LogicalName.StartsWithOrdinal("get_Rest")) then
      warning (Error (FSComp.SR.tcTupleMemberNotNormallyUsed(), m))

    CheckMethInfoAttributes g m tyargsOpt minfo |> CommitOperationResult

exception FieldNotMutable of DisplayEnv * RecdFieldRef * range

let CheckRecdFieldMutation m denv (rfinfo: RecdFieldInfo) = 
    if not rfinfo.RecdField.IsMutable then
        errorR (FieldNotMutable (denv, rfinfo.RecdFieldRef, m))


let ObjArgExprNeedsAddressOf g argExprs =
    match argExprs with 
    | [] -> false 
    | h :: _ -> not (isByrefTy g (tyOfExpr g h))

/// Generate a witness for the given (solved) constraint.  Five possiblilities are taken
/// into account.
///   1. The constraint is solved by a .NET-declared method or an F#-declared method
///   2. The constraint is solved by an F# record field
///   3. The constraint is solved by an F# anonymous record field
///   4. The constraint is considered solved by a "built in" solution
///   5. The constraint is solved by a closed expression given by a provided method from a type provider
/// 
/// In each case an expression is returned where the method is applied to the given arguments, or the
/// field is dereferenced.
/// 
/// None is returned in the cases where the trait has not been solved (e.g. is part of generic code)
/// or there is an unexpected mismatch of some kind.
let GenWitnessExpr amap g m (traitInfo: TraitConstraintInfo) argExprs =

    let sln = 
        match traitInfo.Solution with 
        | None -> Choice5Of5()
        | Some sln ->

            // Given the solution information, reconstruct the MethInfo for the solution
            match sln with 
            | ILMethSln(apparentTy, extOpt, mref, minst) ->

                let metadataTy = convertToTypeWithMetadataIfPossible g apparentTy

                // Find the actual type containing the solution
                let actualTyconRef = 
                    match extOpt with 
                    | None -> tcrefOfAppTy g metadataTy
                    | Some ilActualTypeRef -> Import.ImportILTypeRef amap m ilActualTypeRef 

                let mdef = resolveILMethodRef actualTyconRef.ILTyconRawMetadata mref
                
                let minfo =
                    match extOpt with 
                    | None -> MethInfo.CreateILMeth(amap, m, apparentTy, mdef)
                    | Some _ -> 

                        let pri = 0UL // irrelevant for post-typecheck processing of solution
                        MethInfo.CreateILExtensionMeth(amap, m, apparentTy, actualTyconRef, Some pri, mdef)

                Choice1Of5 (minfo, minst)

            | FSMethSln(ty, vref, minst, isExt) ->
                let minfo =
                    if isExt then 
                        let pri = 0UL // irrelevant for post-typecheck processing of solution
                        FSMeth(g, ty, vref, Some pri)
                    else
                        FSMeth(g, ty, vref, None)

                Choice1Of5 (minfo, minst)

            | FSRecdFieldSln(tinst, rfref, isSetProp) ->
                Choice2Of5  (tinst, rfref, isSetProp)

            | FSAnonRecdFieldSln(anonInfo, tinst, i) -> 
                Choice3Of5  (anonInfo, tinst, i)

            | ClosedExprSln expr -> 
                Choice4Of5 expr

            | BuiltInSln -> 
                Choice5Of5 ()

    match sln with
    | Choice1Of5(minfo, methArgTys) -> 
        let argExprs = 
            // FIX for #421894 - typechecker assumes that coercion can be applied for the trait
            // calls arguments but codegen doesn't emit coercion operations
            // result - generation of non-verifiable code
            // fix - apply coercion for the arguments (excluding 'receiver' argument in instance calls)

            // flatten list of argument types (looks like trait calls with curried arguments are not supported so
            // we can just convert argument list in straight-forward way)
            let argTypes =
                minfo.GetParamTypes(amap, m, methArgTys) 
                |> List.concat 

            // do not apply coercion to the 'receiver' argument
            let receiverArgOpt, argExprs = 
                if minfo.IsInstance then
                    match argExprs with
                    | h :: t -> Some h, t
                    | argExprs -> None, argExprs
                else None, argExprs

            // For methods taking no arguments, 'argExprs' will be a single unit expression here
            let argExprs = 
                 match argTypes, argExprs with
                 | [], [_] -> []
                 | _ -> argExprs

            let convertedArgs = (argExprs, argTypes) ||> List.map2 (fun expr expectedTy -> mkCoerceIfNeeded g expectedTy (tyOfExpr g expr) expr)
            match receiverArgOpt with
            | Some r -> r :: convertedArgs
            | None -> convertedArgs

        // Fix bug 1281: If we resolve to an instance method on a struct and we haven't yet taken 
        // the address of the object then go do that 
        if minfo.IsStruct && minfo.IsInstance && (minfo.ObjArgNeedsAddress(amap, m)) && ObjArgExprNeedsAddressOf g argExprs then
            match argExprs with
            | h :: t ->
                let wrap, h', _readonly, _writeonly = mkExprAddrOfExpr g true false PossiblyMutates h None m 
                Some (wrap (Expr.Op (TOp.TraitCall traitInfo, [], (h' :: t), m)))
            | _ ->
                Some (MakeMethInfoCall amap m minfo methArgTys argExprs)
        else        
            Some (MakeMethInfoCall amap m minfo methArgTys argExprs)

    | Choice2Of5 (tinst, rfref, isSet) -> 
        match isSet, rfref.RecdField.IsStatic, argExprs.Length with 
        // static setter
        | true, true, 1 -> 
            Some (mkStaticRecdFieldSet (rfref, tinst, argExprs.[0], m))

        // instance setter
        | true, false, 2 -> 
            // If we resolve to an instance field on a struct and we haven't yet taken 
            // the address of the object then go do that 
            if rfref.Tycon.IsStructOrEnumTycon && not (isByrefTy g (tyOfExpr g argExprs.[0])) then 
                let h = List.head argExprs
                let wrap, h', _readonly, _writeonly = mkExprAddrOfExpr g true false DefinitelyMutates h None m 
                Some (wrap (mkRecdFieldSetViaExprAddr (h', rfref, tinst, argExprs.[1], m)))
            else        
                Some (mkRecdFieldSetViaExprAddr (argExprs.[0], rfref, tinst, argExprs.[1], m))

        // static getter
        | false, true, 0 -> 
            Some (mkStaticRecdFieldGet (rfref, tinst, m))

        // instance getter
        | false, false, 1 -> 
            if rfref.Tycon.IsStructOrEnumTycon && isByrefTy g (tyOfExpr g argExprs.[0]) then 
                Some (mkRecdFieldGetViaExprAddr (argExprs.[0], rfref, tinst, m))
            else 
                Some (mkRecdFieldGet g (argExprs.[0], rfref, tinst, m))

        | _ -> None 

    | Choice3Of5 (anonInfo, tinst, i) -> 
        let tupInfo = anonInfo.TupInfo
        if evalTupInfoIsStruct tupInfo && isByrefTy g (tyOfExpr g argExprs.[0]) then 
            Some (mkAnonRecdFieldGetViaExprAddr (anonInfo, argExprs.[0], tinst, i, m))
        else 
            Some (mkAnonRecdFieldGet g (anonInfo, argExprs.[0], tinst, i, m))

    | Choice4Of5 expr -> 
        Some (MakeApplicationAndBetaReduce g (expr, tyOfExpr g expr, [], argExprs, m))

    | Choice5Of5 () -> 
        match traitInfo.Solution with 
        | None -> None // the trait has been generalized
        | Some _-> 
        // For these operators, the witness is just a call to the coresponding FSharp.Core operator
        match g.TryMakeOperatorAsBuiltInWitnessInfo isStringTy isArrayTy traitInfo argExprs with
        | Some (info, tyargs, actualArgExprs) -> 
            tryMkCallCoreFunctionAsBuiltInWitness g info tyargs actualArgExprs m
        | None -> 
            // For all other built-in operators, the witness is a call to the coresponding BuiltInWitnesses operator
            // These are called as F# methods not F# functions
            tryMkCallBuiltInWitness g traitInfo argExprs m
        
/// Generate a lambda expression for the given solved trait.
let GenWitnessExprLambda amap g m (traitInfo: TraitConstraintInfo) =
    let witnessInfo = traitInfo.TraitKey
    let argtysl = GenWitnessArgTys g witnessInfo
    let vse = argtysl |> List.mapiSquared (fun i j ty -> mkCompGenLocal m ("arg" + string i + "_" + string j) ty) 
    let vsl = List.mapSquared fst vse
    match GenWitnessExpr amap g m traitInfo (List.concat (List.mapSquared snd vse)) with 
    | Some expr -> 
        Choice2Of2 (mkMemberLambdas m [] None None vsl (expr, tyOfExpr g expr))
    | None -> 
        Choice1Of2 traitInfo

/// Generate the arguments passed for a set of (solved) traits in non-generic code
let GenWitnessArgs amap g m (traitInfos: TraitConstraintInfo list) =
    [ for traitInfo in traitInfos -> GenWitnessExprLambda amap g m traitInfo ]
