// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler

#if !NO_EXTENSIONTYPING

open System
open Internal.Utilities.Library 
open FSharp.Core.CompilerServices
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range

[<Sealed>]
type internal TypeProviderToken() = interface LockToken

[<Sealed>]
type internal TypeProviderLock() =
    inherit Lock<TypeProviderToken>()

type TypeProviderError
    (
        errNum: int,
        tpDesignation: string,
        m: range,
        errors: string list,
        typeNameContext: string option,
        methodNameContext: string option
    ) =

    inherit Exception()

    new((errNum, msg: string), tpDesignation,m) = 
        TypeProviderError(errNum, tpDesignation, m, [msg])
    
    new(errNum, tpDesignation, m, messages: seq<string>) =         
        TypeProviderError(errNum, tpDesignation, m, List.ofSeq messages, None, None)

    member _.Number = errNum
    member _.Range = m

    override _.Message = 
        match errors with
        | [text] -> text
        | inner -> 
            // imitates old-fashioned behavior with merged text
            // usually should not fall into this case (only if someone takes Message directly instead of using Iter)
            inner            
            |> String.concat Environment.NewLine

    member _.MapText(f, tpDesignation, m) = 
        let (errNum: int), _ = f ""
        new TypeProviderError(errNum, tpDesignation, m,  (Seq.map (f >> snd) errors))

    member _.WithContext(typeNameContext:string, methodNameContext:string) = 
        new TypeProviderError(errNum, tpDesignation, m, errors, Some typeNameContext, Some methodNameContext)

    // .Message is just the error, whereas .ContextualErrorMessage has contextual prefix information
    // for example if InvokeCode in provided method is not set or has value that cannot be translated -then initial TPE will be wrapped in
    // TPE having type\method name as contextual information
    // without context: Type Provider 'TP' has reported the error: MSG
    // with context: Type Provider 'TP' has reported the error in method M of type T: MSG
    member this.ContextualErrorMessage= 
        match typeNameContext, methodNameContext with
        | Some tc, Some mc ->
            let _,msgWithPrefix = FSComp.SR.etProviderErrorWithContext(tpDesignation, tc, mc, this.Message)
            msgWithPrefix
        | _ ->
            let _,msgWithPrefix = FSComp.SR.etProviderError(tpDesignation, this.Message)
            msgWithPrefix
    
    /// provides uniform way to handle plain and composite instances of TypeProviderError
    member this.Iter f = 
        match errors with
        | [_] -> f this
        | errors ->
            for msg in errors do
                f (new TypeProviderError(errNum, tpDesignation, m, [msg], typeNameContext, methodNameContext))

type TaintedContext = { TypeProvider : ITypeProvider; TypeProviderAssemblyRef : ILScopeRef; TypeProviderDesignation: string; Lock : TypeProviderLock }

[<NoEquality>][<NoComparison>]
type Tainted<'T> (context : TaintedContext, value : 'T) =
    do
        match box context.TypeProvider with 
        | null -> 
            assert false
            failwith "null ITypeProvider in Tainted constructor"
        | _ -> ()

    member _.TypeProviderDesignation =
        context.TypeProviderDesignation

    member _.TypeProviderAssemblyRef = 
        context.TypeProviderAssemblyRef

    member this.Protect f  (range:range) =
        try 
            context.Lock.AcquireLock(fun _ -> f value)
        with
            |   :? TypeProviderError -> reraise()
            |   :? AggregateException as ae ->
                    let errNum,_ = FSComp.SR.etProviderError("", "")
                    let messages = [for e in ae.InnerExceptions -> e.Message]
                    raise <| TypeProviderError(errNum, this.TypeProviderDesignation, range, messages)
            |   e -> 
                    let errNum,_ = FSComp.SR.etProviderError("", "")
                    raise <| TypeProviderError((errNum, e.Message), this.TypeProviderDesignation, range)

    member this.TypeProvider = Tainted<_>(context, context.TypeProvider)

    member this.PApply(f,range:range) = 
        let u = this.Protect f range
        Tainted(context, u)

    member this.PApply2(f,range:range) = 
        let u1,u2 = this.Protect f range
        Tainted(context, u1), Tainted(context, u2)

    member this.PApply3(f,range:range) = 
        let u1,u2,u3 = this.Protect f range
        Tainted(context, u1), Tainted(context, u2), Tainted(context, u3)

    member this.PApply4(f,range:range) = 
        let u1,u2,u3,u4 = this.Protect f range
        Tainted(context, u1), Tainted(context, u2), Tainted(context, u3), Tainted(context, u4)

    member this.PApplyNoFailure f = this.PApply (f, range0)

    member this.PApplyWithProvider(f,range:range) = 
        let u = this.Protect (fun x -> f (x,context.TypeProvider)) range
        Tainted(context, u)

    member this.PApplyArray(f,methodName,range:range) =        
        let a = this.Protect f range
        match a with 
        |   null -> raise <| TypeProviderError(FSComp.SR.etProviderReturnedNull(methodName), this.TypeProviderDesignation, range)
        |   _ -> a |> Array.map (fun u -> Tainted(context,u))

    member this.PApplyOption(f,range:range) =        
        let a = this.Protect f range
        match a with 
        | None ->  None
        | Some x -> Some (Tainted(context,x))

    member this.PUntaint(f,range:range) = this.Protect f range
    member this.PUntaintNoFailure f = this.PUntaint(f, range0)
    /// Access the target object directly. Use with extreme caution.
    member this.AccessObjectDirectly = value

    static member CreateAll(providerSpecs : (ITypeProvider * ILScopeRef * string) list) =
        [for (tp,nm, tpd) in providerSpecs do
             yield Tainted<_>({ TypeProvider = tp; TypeProviderAssemblyRef = nm; TypeProviderDesignation = tpd; Lock=TypeProviderLock() },tp) ]

    member this.OfType<'U> () =
        match box value with
        |   :? 'U as u -> Some (Tainted(context,u))
        |   _ -> None

    member this.Coerce<'U> (range:range) =
        Tainted(context, this.Protect(fun value -> box value :?> 'U) range)

module Tainted =
    let (|Null|_|) (p:Tainted<'T>) =
        if p.PUntaintNoFailure(fun p -> match p with null -> true | _ -> false) then Some() else None

    let Eq (p:Tainted<'T>) (v:'T) = p.PUntaintNoFailure((fun pv -> pv = v))

    let EqTainted (t1:Tainted<'T>) (t2:Tainted<'T>) = 
        t1.PUntaintNoFailure(fun t1 -> t1 === t2.AccessObjectDirectly)

    let GetHashCodeTainted (t:Tainted<'T>) = t.PUntaintNoFailure(fun t -> hash t)
    
#endif
    
