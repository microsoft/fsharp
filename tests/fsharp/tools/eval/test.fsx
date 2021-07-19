#light

#nowarn "57"

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Quotations.DerivedPatterns
open Microsoft.FSharp.Quotations.ExprShape
open System
open System.Linq
open System.Collections.Generic
open System.Linq.Expressions
open System.Reflection
open System.Reflection.Emit
open Microsoft.FSharp
open Microsoft.FSharp.Reflection
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Quotations.DerivedPatterns


module QuotationEvaluation = 


    let asExpr x = (x :> Expression)

    let bindingFlags = BindingFlags.Public ||| BindingFlags.NonPublic
    let instanceBindingFlags = BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.DeclaredOnly
    let isNamedType(typ:Type) = not (typ.IsArray || typ.IsByRef || typ.IsPointer)
    let equivHeadTypes (ty1:Type) (ty2:Type) = 
        isNamedType(ty1) &&
        if ty1.IsGenericType then 
          ty2.IsGenericType && (ty1.GetGenericTypeDefinition()).Equals(ty2.GetGenericTypeDefinition())
        else 
          ty1.Equals(ty2)

    let isFunctionType typ = equivHeadTypes typ (typeof<(int -> int)>)
    let getFunctionType typ = 
        if not (isFunctionType typ) then invalidArg "typ" "cannot convert recursion except for function types"
        let tyargs = typ.GetGenericArguments()
        tyargs.[0], tyargs.[1]
    
    let WhileHelper gd b : 'T = 
        let rec loop () = if gd() then (b(); loop())
        loop();
        unbox (box ())

    let ArrayAssignHelper (arr : 'T[]) (idx:int) (elem:'T) : 'unt = 
        arr.[idx] <- elem;
        unbox (box ())


    let TryFinallyHelper e h = 
        try e() 
        finally h()

    let TryWithHelper e filter handler = 
        try e() 
        with e when (filter e <> 0) -> handler e

    let WhileMethod = match <@@ WhileHelper @@> with Lambdas(_,Call(_,minfo,_)) -> minfo | _ -> failwith "couldn't find minfo"
    let ArrayAssignMethod = match <@@ ArrayAssignHelper @@> with Lambdas(_,Call(_,minfo,_)) -> minfo | _ -> failwith "couldn't find minfo"
    let TryFinallyMethod = match <@@ TryFinallyHelper @@> with Lambdas(_,Call(_,minfo,_)) -> minfo | _ -> failwith "couldn't find minfo"
    let TryWithMethod = match <@@ TryWithHelper @@> with Lambdas(_,Call(_,minfo,_)) -> minfo | _ -> failwith "couldn't find minfo"

    module HelperTypes = 
        type ActionHelper<'T1,'T2,'T3,'T4,'T5> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 -> unit
        type ActionHelper<'T1,'T2,'T3,'T4,'T5,'T6> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 -> unit
        type ActionHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 -> unit
        type ActionHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 -> unit
        type ActionHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 -> unit
        type ActionHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9,'T10> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 * 'T10 -> unit
        type ActionHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9,'T10, 'T11> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 * 'T10 * 'T11 -> unit
        type ActionHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9,'T10, 'T11, 'T12> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 * 'T10 * 'T11 * 'T12 -> unit
        type ActionHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9,'T10, 'T11, 'T12, 'T13> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 * 'T10 * 'T11 * 'T12 * 'T13 -> unit
        type ActionHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9,'T10, 'T11, 'T12, 'T13, 'T14> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 * 'T10 * 'T11 * 'T12 * 'T13 * 'T14 -> unit
        type ActionHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9,'T10, 'T11, 'T12, 'T13, 'T14, 'T15> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 * 'T10 * 'T11 * 'T12 * 'T13 * 'T14 * 'T15 -> unit
        type ActionHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9,'T10, 'T11, 'T12, 'T13, 'T14, 'T15, 'T16> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 * 'T10 * 'T11 * 'T12 * 'T13 * 'T14 * 'T15 * 'T16 -> unit
        type ActionHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9,'T10, 'T11, 'T12, 'T13, 'T14, 'T15, 'T16, 'T17> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 * 'T10 * 'T11 * 'T12 * 'T13 * 'T14 * 'T15 * 'T16 * 'T17 -> unit
        type ActionHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9,'T10, 'T11, 'T12, 'T13, 'T14, 'T15, 'T16, 'T17, 'T18> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 * 'T10 * 'T11 * 'T12 * 'T13 * 'T14 * 'T15 * 'T16 * 'T17 * 'T18 -> unit
        type ActionHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9,'T10, 'T11, 'T12, 'T13, 'T14, 'T15, 'T16, 'T17, 'T18, 'T19> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 * 'T10 * 'T11 * 'T12 * 'T13 * 'T14 * 'T15 * 'T16 * 'T17 * 'T18 * 'T19 -> unit
        type ActionHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9,'T10, 'T11, 'T12, 'T13, 'T14, 'T15, 'T16, 'T17, 'T18, 'T19, 'T20> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 * 'T10 * 'T11 * 'T12 * 'T13 * 'T14 * 'T15 * 'T16 * 'T17 * 'T18 * 'T19 * 'T20 -> unit

        type FuncHelper<'T1,'T2,'T3,'T4,'T5,'T6> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 -> 'T6
        type FuncHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 -> 'T7 
        type FuncHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 -> 'T8 
        type FuncHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 -> 'T9 
        type FuncHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9,'T10> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 -> 'T10 
        type FuncHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9,'T10, 'T11> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 * 'T10 -> 'T11 
        type FuncHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9,'T10, 'T11, 'T12> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 * 'T10 * 'T11 -> 'T12 
        type FuncHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9,'T10, 'T11, 'T12, 'T13> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 * 'T10 * 'T11 * 'T12 -> 'T13 
        type FuncHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9,'T10, 'T11, 'T12, 'T13, 'T14> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 * 'T10 * 'T11 * 'T12 * 'T13 -> 'T14 
        type FuncHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9,'T10, 'T11, 'T12, 'T13, 'T14, 'T15> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 * 'T10 * 'T11 * 'T12 * 'T13 * 'T14 -> 'T15 
        type FuncHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9,'T10, 'T11, 'T12, 'T13, 'T14, 'T15, 'T16> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 * 'T10 * 'T11 * 'T12 * 'T13 * 'T14 * 'T15 -> 'T16 
        type FuncHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9,'T10, 'T11, 'T12, 'T13, 'T14, 'T15, 'T16, 'T17> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 * 'T10 * 'T11 * 'T12 * 'T13 * 'T14 * 'T15 * 'T16 -> 'T17 
        type FuncHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9,'T10, 'T11, 'T12, 'T13, 'T14, 'T15, 'T16, 'T17, 'T18> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 * 'T10 * 'T11 * 'T12 * 'T13 * 'T14 * 'T15 * 'T16 * 'T17 -> 'T18 
        type FuncHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9,'T10, 'T11, 'T12, 'T13, 'T14, 'T15, 'T16, 'T17, 'T18, 'T19> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 * 'T10 * 'T11 * 'T12 * 'T13 * 'T14 * 'T15 * 'T16 * 'T17 * 'T18 -> 'T19 
        type FuncHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9,'T10, 'T11, 'T12, 'T13, 'T14, 'T15, 'T16, 'T17, 'T18, 'T19, 'T20> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 * 'T10 * 'T11 * 'T12 * 'T13 * 'T14 * 'T15 * 'T16 * 'T17 * 'T18 * 'T19 -> 'T20 
        type FuncHelper<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'T8,'T9,'T10, 'T11, 'T12, 'T13, 'T14, 'T15, 'T16, 'T17, 'T18, 'T19, 'T20, 'T21> = delegate of 'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 * 'T8 * 'T9 * 'T10 * 'T11 * 'T12 * 'T13 * 'T14 * 'T15 * 'T16 * 'T17 * 'T18 * 'T19 * 'T20 -> 'T21 

    open HelperTypes
    
    let GetActionType (args:Type[])  = 
        if args.Length <= 4 then 
            Expression.GetActionType args
        else
            match args.Length with 
            | 5 -> typedefof<ActionHelper<_,_,_,_,_>>.MakeGenericType args
            | 6 -> typedefof<ActionHelper<_,_,_,_,_,_>>.MakeGenericType args
            | 7 -> typedefof<ActionHelper<_,_,_,_,_,_,_>>.MakeGenericType args
            | 8 -> typedefof<ActionHelper<_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 9 -> typedefof<ActionHelper<_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 10 -> typedefof<ActionHelper<_,_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 11 -> typedefof<ActionHelper<_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 12 -> typedefof<ActionHelper<_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 13 -> typedefof<ActionHelper<_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 14 -> typedefof<ActionHelper<_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 15 -> typedefof<ActionHelper<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 16 -> typedefof<ActionHelper<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 17 -> typedefof<ActionHelper<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 18 -> typedefof<ActionHelper<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 19 -> typedefof<ActionHelper<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 20 -> typedefof<ActionHelper<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | _ -> raise <| new NotSupportedException("Quotation expressions with statements or closures containing more then 20 free variables may not be translated in this release of the F# PowerPack. This is due to limitations in the variable binding expression forms available in LINQ expression trees")

    let GetFuncType (args:Type[])  = 
        if args.Length <= 5 then 
            Expression.GetFuncType args
        else
            match args.Length with 
            | 6 -> typedefof<FuncHelper<_,_,_,_,_,_>>.MakeGenericType args
            | 7 -> typedefof<FuncHelper<_,_,_,_,_,_,_>>.MakeGenericType args
            | 8 -> typedefof<FuncHelper<_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 9 -> typedefof<FuncHelper<_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 10 -> typedefof<FuncHelper<_,_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 11 -> typedefof<FuncHelper<_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 12 -> typedefof<FuncHelper<_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 13 -> typedefof<FuncHelper<_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 14 -> typedefof<FuncHelper<_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 15 -> typedefof<FuncHelper<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 16 -> typedefof<FuncHelper<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 17 -> typedefof<FuncHelper<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 18 -> typedefof<FuncHelper<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 19 -> typedefof<FuncHelper<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 20 -> typedefof<FuncHelper<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | 21 -> typedefof<FuncHelper<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType args
            | _ -> raise <| new NotSupportedException("Quotation expressions with statements or closures containing more then 20 free variables may not be translated in this release of the F# PowerPack. This is due to limitations in the variable binding expression forms available in LINQ expression trees")
            

    let LetRec1Helper (F1:System.Func<_,_,_>) (B:System.Func<_,_>) = 
        let fhole = ref (Unchecked.defaultof<_>)
        let f = new System.Func<_,_>(fun x -> F1.Invoke(fhole.Value,x))
        fhole := f
        B.Invoke f

    let LetRec2Helper (F1:System.Func<_,_,_,_>) (F2:System.Func<_,_,_,_>) (B:System.Func<_,_,_>) = 
        let f1hole = ref (Unchecked.defaultof<_>)
        let f2hole = ref (Unchecked.defaultof<_>)
        let f1 = new System.Func<_,_>(fun x -> F1.Invoke(f1hole.Value,f2hole.Value,x))
        let f2 = new System.Func<_,_>(fun x -> F2.Invoke(f1hole.Value,f2hole.Value,x))
        f1hole := f1
        f2hole := f2
        B.Invoke(f1,f2)

    let LetRec3Helper (F1:System.Func<_,_,_,_,_>) (F2:System.Func<_,_,_,_,_>) (F3:System.Func<_,_,_,_,_>) (B:System.Func<_,_,_,_>) = 
        let f1hole = ref (Unchecked.defaultof<_>)
        let f2hole = ref (Unchecked.defaultof<_>)
        let f3hole = ref (Unchecked.defaultof<_>)
        let f1 = new System.Func<_,_>(fun x -> F1.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,x))
        let f2 = new System.Func<_,_>(fun x -> F2.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,x))
        let f3 = new System.Func<_,_>(fun x -> F3.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,x))
        f1hole := f1
        f2hole := f2
        f3hole := f3
        B.Invoke(f1,f2,f3)

    let LetRec4Helper 
           (F1:FuncHelper<_,_,_,_,_,_>) 
           (F2:FuncHelper<_,_,_,_,_,_>) 
           (F3:FuncHelper<_,_,_,_,_,_>) 
           (F4:FuncHelper<_,_,_,_,_,_>) 
           (B:System.Func<_,_,_,_,_>) = 
        let f1hole = ref (Unchecked.defaultof<_>)
        let f2hole = ref (Unchecked.defaultof<_>)
        let f3hole = ref (Unchecked.defaultof<_>)
        let f4hole = ref (Unchecked.defaultof<_>)
        let f1 = new System.Func<_,_>(fun x -> F1.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,x))
        let f2 = new System.Func<_,_>(fun x -> F2.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,x))
        let f3 = new System.Func<_,_>(fun x -> F3.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,x))
        let f4 = new System.Func<_,_>(fun x -> F4.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,x))
        f1hole := f1
        f2hole := f2
        f3hole := f3
        f4hole := f4
        B.Invoke(f1,f2,f3,f4)


    let LetRec5Helper 
           (F1:FuncHelper<_,_,_,_,_,_,_>) 
           (F2:FuncHelper<_,_,_,_,_,_,_>) 
           (F3:FuncHelper<_,_,_,_,_,_,_>) 
           (F4:FuncHelper<_,_,_,_,_,_,_>) 
           (F5:FuncHelper<_,_,_,_,_,_,_>) 
           (B:FuncHelper<_,_,_,_,_,_>) = 
        let f1hole = ref (Unchecked.defaultof<_>)
        let f2hole = ref (Unchecked.defaultof<_>)
        let f3hole = ref (Unchecked.defaultof<_>)
        let f4hole = ref (Unchecked.defaultof<_>)
        let f5hole = ref (Unchecked.defaultof<_>)
        let f1 = new System.Func<_,_>(fun x -> F1.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,x))
        let f2 = new System.Func<_,_>(fun x -> F2.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,x))
        let f3 = new System.Func<_,_>(fun x -> F3.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,x))
        let f4 = new System.Func<_,_>(fun x -> F4.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,x))
        let f5 = new System.Func<_,_>(fun x -> F5.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,x))
        f1hole := f1
        f2hole := f2
        f3hole := f3
        f4hole := f4
        f5hole := f5
        B.Invoke(f1,f2,f3,f4,f5)

    let LetRec6Helper 
           (F1:FuncHelper<_,_,_,_,_,_,_,_>) 
           (F2:FuncHelper<_,_,_,_,_,_,_,_>) 
           (F3:FuncHelper<_,_,_,_,_,_,_,_>) 
           (F4:FuncHelper<_,_,_,_,_,_,_,_>) 
           (F5:FuncHelper<_,_,_,_,_,_,_,_>) 
           (F6:FuncHelper<_,_,_,_,_,_,_,_>) 
           (B:FuncHelper<_,_,_,_,_,_,_>) = 
        let f1hole = ref (Unchecked.defaultof<_>)
        let f2hole = ref (Unchecked.defaultof<_>)
        let f3hole = ref (Unchecked.defaultof<_>)
        let f4hole = ref (Unchecked.defaultof<_>)
        let f5hole = ref (Unchecked.defaultof<_>)
        let f6hole = ref (Unchecked.defaultof<_>)
        let f1 = new System.Func<_,_>(fun x -> F1.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,f6hole.Value,x))
        let f2 = new System.Func<_,_>(fun x -> F2.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,f6hole.Value,x))
        let f3 = new System.Func<_,_>(fun x -> F3.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,f6hole.Value,x))
        let f4 = new System.Func<_,_>(fun x -> F4.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,f6hole.Value,x))
        let f5 = new System.Func<_,_>(fun x -> F5.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,f6hole.Value,x))
        let f6 = new System.Func<_,_>(fun x -> F6.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,f6hole.Value,x))
        f1hole := f1
        f2hole := f2
        f3hole := f3
        f4hole := f4
        f5hole := f5
        f6hole := f6
        B.Invoke(f1,f2,f3,f4,f5,f6)


    let LetRec7Helper 
           (F1:FuncHelper<_,_,_,_,_,_,_,_,_>) 
           (F2:FuncHelper<_,_,_,_,_,_,_,_,_>) 
           (F3:FuncHelper<_,_,_,_,_,_,_,_,_>) 
           (F4:FuncHelper<_,_,_,_,_,_,_,_,_>) 
           (F5:FuncHelper<_,_,_,_,_,_,_,_,_>) 
           (F6:FuncHelper<_,_,_,_,_,_,_,_,_>) 
           (F7:FuncHelper<_,_,_,_,_,_,_,_,_>) 
           (B:FuncHelper<_,_,_,_,_,_,_,_>) = 
        let f1hole = ref (Unchecked.defaultof<_>)
        let f2hole = ref (Unchecked.defaultof<_>)
        let f3hole = ref (Unchecked.defaultof<_>)
        let f4hole = ref (Unchecked.defaultof<_>)
        let f5hole = ref (Unchecked.defaultof<_>)
        let f6hole = ref (Unchecked.defaultof<_>)
        let f7hole = ref (Unchecked.defaultof<_>)
        let f1 = new System.Func<_,_>(fun x -> F1.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,f6hole.Value,f7hole.Value,x))
        let f2 = new System.Func<_,_>(fun x -> F2.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,f6hole.Value,f7hole.Value,x))
        let f3 = new System.Func<_,_>(fun x -> F3.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,f6hole.Value,f7hole.Value,x))
        let f4 = new System.Func<_,_>(fun x -> F4.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,f6hole.Value,f7hole.Value,x))
        let f5 = new System.Func<_,_>(fun x -> F5.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,f6hole.Value,f7hole.Value,x))
        let f6 = new System.Func<_,_>(fun x -> F6.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,f6hole.Value,f7hole.Value,x))
        let f7 = new System.Func<_,_>(fun x -> F7.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,f6hole.Value,f7hole.Value,x))
        f1hole := f1
        f2hole := f2
        f3hole := f3
        f4hole := f4
        f5hole := f5
        f6hole := f6
        f7hole := f7
        B.Invoke(f1,f2,f3,f4,f5,f6,f7)


    let LetRec8Helper 
           (F1:FuncHelper<_,_,_,_,_,_,_,_,_,_>) 
           (F2:FuncHelper<_,_,_,_,_,_,_,_,_,_>) 
           (F3:FuncHelper<_,_,_,_,_,_,_,_,_,_>) 
           (F4:FuncHelper<_,_,_,_,_,_,_,_,_,_>) 
           (F5:FuncHelper<_,_,_,_,_,_,_,_,_,_>) 
           (F6:FuncHelper<_,_,_,_,_,_,_,_,_,_>) 
           (F7:FuncHelper<_,_,_,_,_,_,_,_,_,_>) 
           (F8:FuncHelper<_,_,_,_,_,_,_,_,_,_>) 
           (B:FuncHelper<_,_,_,_,_,_,_,_,_>) = 
        let f1hole = ref (Unchecked.defaultof<_>)
        let f2hole = ref (Unchecked.defaultof<_>)
        let f3hole = ref (Unchecked.defaultof<_>)
        let f4hole = ref (Unchecked.defaultof<_>)
        let f5hole = ref (Unchecked.defaultof<_>)
        let f6hole = ref (Unchecked.defaultof<_>)
        let f7hole = ref (Unchecked.defaultof<_>)
        let f8hole = ref (Unchecked.defaultof<_>)
        let f1 = new System.Func<_,_>(fun x -> F1.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,f6hole.Value,f7hole.Value,f8hole.Value,x))
        let f2 = new System.Func<_,_>(fun x -> F2.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,f6hole.Value,f7hole.Value,f8hole.Value,x))
        let f3 = new System.Func<_,_>(fun x -> F3.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,f6hole.Value,f7hole.Value,f8hole.Value,x))
        let f4 = new System.Func<_,_>(fun x -> F4.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,f6hole.Value,f7hole.Value,f8hole.Value,x))
        let f5 = new System.Func<_,_>(fun x -> F5.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,f6hole.Value,f7hole.Value,f8hole.Value,x))
        let f6 = new System.Func<_,_>(fun x -> F6.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,f6hole.Value,f7hole.Value,f8hole.Value,x))
        let f7 = new System.Func<_,_>(fun x -> F7.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,f6hole.Value,f7hole.Value,f8hole.Value,x))
        let f8 = new System.Func<_,_>(fun x -> F8.Invoke(f1hole.Value,f2hole.Value,f3hole.Value,f4hole.Value,f5hole.Value,f6hole.Value,f7hole.Value,f8hole.Value,x))
        f1hole := f1
        f2hole := f2
        f3hole := f3
        f4hole := f4
        f5hole := f5
        f6hole := f6
        f7hole := f7
        f8hole := f8
        B.Invoke(f1,f2,f3,f4,f5,f6,f7,f8)


    let IsVoidType (ty:System.Type)  = (ty = typeof<System.Void>)

    let SequentialHelper (x:'T) (y:'U) = y
 
    let LinqExpressionHelper (x:'T) : Expression<'T> = failwith ""
    
    let MakeFakeExpression (x:Expr) = 
        let minfo = match <@@ LinqExpressionHelper @@> with Lambda(_,Call(_,minfo,_)) -> minfo | _ -> failwith "couldn't find method info"
        Expr.Call(minfo.GetGenericMethodDefinition().MakeGenericMethod [| x.Type |], [ x ])

    let showAll = BindingFlags.Public ||| BindingFlags.NonPublic 

    let WrapVoid b objOpt args (env: Map<Var,Expression>) (e:Expression) = 
        if b then 
            let frees (e:Expr) = e.GetFreeVars()
            let addFrees e acc = List.foldBack Set.add (frees e |> Seq.toList) acc
            let fvs = Option.foldBack addFrees objOpt (List.foldBack addFrees args Set.empty) |> Set.toArray
            let fvsP = fvs |> Array.map (fun v -> (Map.find v env :?> ParameterExpression))
            let fvtys = fvs |> Array.map (fun v -> v.Type) 

            let dty = GetActionType fvtys 
            let e = Expression.Lambda(dty,e,fvsP)
            let d = e.Compile()

            let argtys = Array.append fvtys [| dty |]
            let delP = Expression.Parameter(dty, "del")

            let m = new System.Reflection.Emit.DynamicMethod("wrapper",typeof<unit>,argtys)
            let ilg = m.GetILGenerator()
            
            ilg.Emit(OpCodes.Ldarg ,fvs.Length)
            fvs |> Array.iteri (fun i _ -> ilg.Emit(OpCodes.Ldarg ,int16 i))
            ilg.EmitCall(OpCodes.Callvirt,dty.GetMethod("Invoke",instanceBindingFlags),null)
            ilg.Emit(OpCodes.Ldnull)
            ilg.Emit(OpCodes.Ret)
            let args = Array.append (fvsP |> Array.map asExpr) [| (Expression.Constant(d) |> asExpr) |]
            Expression.Call((null:Expression),(m:>MethodInfo),args) |> asExpr
        else
            e

    let (|GenericEqualityQ|_|) = (|SpecificCall|_|) <@ LanguagePrimitives.GenericEquality @>
    let (|EqualsQ|_|)    = (|SpecificCall|_|) <@ ( = ) @>
    let (|GreaterQ|_|)   = (|SpecificCall|_|) <@ ( > ) @>
    let (|GreaterEqQ|_|) = (|SpecificCall|_|) <@ ( >=) @>
    let (|LessQ|_|)      = (|SpecificCall|_|) <@ ( <)  @>
    let (|LessEqQ|_|) = (|SpecificCall|_|) <@ ( <=) @>
    let (|NotEqQ|_|) = (|SpecificCall|_|) <@ ( <>) @>
    let (|NotQ|_|) =  (|SpecificCall|_|) <@ not   @>
    let (|NegQ|_|) = (|SpecificCall|_|) <@ ( ~-) : int -> int @>
    let (|PlusQ|_|)      = (|SpecificCall|_|) <@ ( + ) @>
    let (|DivideQ|_|) = (|SpecificCall|_|) <@ ( / ) @> 
    let (|MinusQ|_|) = (|SpecificCall|_|) <@ ( - ) @>
    let (|MultiplyQ|_|) = (|SpecificCall|_|) <@ ( * ) @>
    let (|ModuloQ|_|) = (|SpecificCall|_|) <@ ( % ) @>
    let (|ShiftLeftQ|_|) = (|SpecificCall|_|) <@ ( <<< ) @>
    let (|ShiftRightQ|_|) = (|SpecificCall|_|) <@ ( >>> ) @>
    let (|BitwiseAndQ|_|) = (|SpecificCall|_|) <@ ( &&& ) @>
    let (|BitwiseOrQ|_|) = (|SpecificCall|_|) <@ ( ||| ) @>
    let (|BitwiseXorQ|_|) = (|SpecificCall|_|) <@ ( ^^^ ) @>
    let (|BitwiseNotQ|_|) = (|SpecificCall|_|) <@ ( ~~~ ) @>
    let (|CheckedNeg|_|) = (|SpecificCall|_|) <@ Checked.( ~-) : int -> int @>
    let (|CheckedPlusQ|_|)      = (|SpecificCall|_|) <@ Checked.( + ) @>
    let (|CheckedMinusQ|_|) = (|SpecificCall|_|) <@ Checked.( - ) @>
    let (|CheckedMultiplyQ|_|) = (|SpecificCall|_|) <@ Checked.( * ) @>
    let (|ConvCharQ|_|) = (|SpecificCall|_|) <@ char @>
    let (|ConvDecimalQ|_|) = (|SpecificCall|_|) <@ decimal @>
    let (|ConvFloatQ|_|) = (|SpecificCall|_|) <@ float @>
    let (|ConvFloat32Q|_|) = (|SpecificCall|_|) <@ float32 @>
    let (|ConvSByteQ|_|) = (|SpecificCall|_|) <@ sbyte @>
    let (|ConvInt16Q|_|) = (|SpecificCall|_|) <@ int16 @>
    let (|ConvInt32Q|_|) = (|SpecificCall|_|) <@ int32 @>
    let (|ConvIntQ|_|) = (|SpecificCall|_|) <@ int @>
    let (|ConvInt64Q|_|) = (|SpecificCall|_|) <@ int64 @>
    let (|ConvByteQ|_|) = (|SpecificCall|_|) <@ byte @>
    let (|ConvUInt16Q|_|) = (|SpecificCall|_|) <@ uint16 @>
    let (|ConvUInt32Q|_|) = (|SpecificCall|_|) <@ uint32 @>
    let (|ConvUInt64Q|_|) = (|SpecificCall|_|) <@ uint64 @>

    let (|CheckedConvCharQ|_|) = (|SpecificCall|_|) <@ Checked.char @>
    let (|CheckedConvSByteQ|_|) = (|SpecificCall|_|) <@ Checked.sbyte @>
    let (|CheckedConvInt16Q|_|) = (|SpecificCall|_|) <@ Checked.int16 @>
    let (|CheckedConvInt32Q|_|) = (|SpecificCall|_|) <@ Checked.int32 @>
    let (|CheckedConvInt64Q|_|) = (|SpecificCall|_|) <@ Checked.int64 @>
    let (|CheckedConvByteQ|_|) = (|SpecificCall|_|) <@ Checked.byte @>
    let (|CheckedConvUInt16Q|_|) = (|SpecificCall|_|) <@ Checked.uint16 @>
    let (|CheckedConvUInt32Q|_|) = (|SpecificCall|_|) <@ Checked.uint32 @>
    let (|CheckedConvUInt64Q|_|) = (|SpecificCall|_|) <@ Checked.uint64 @>
    let (|LinqExpressionHelperQ|_|) = (|SpecificCall|_|) <@ LinqExpressionHelper @>
    let (|ArrayLookupQ|_|) = (|SpecificCall|_|) <@ LanguagePrimitives.IntrinsicFunctions.GetArray : int[] -> int -> int @>
    let (|ArrayAssignQ|_|) = (|SpecificCall|_|) <@ LanguagePrimitives.IntrinsicFunctions.SetArray : int[] -> int -> int -> unit @>
    let (|ArrayTypeQ|_|) (ty:System.Type) = if ty.IsArray && ty.GetArrayRank() = 1 then Some(ty.GetElementType()) else None
    
    /// Convert F# quotations to LINQ expression trees.
    /// A more polished LINQ-Quotation translator will be published
    /// concert with later versions of LINQ.
    let rec ConvExpr env (inp:Expr) = 
       //printf "ConvExpr : %A\n" e;
        match inp with 

        // Generic cases 
        | Patterns.Var(v) -> Map.find v env
        | DerivedPatterns.AndAlso(x1,x2)             -> Expression.AndAlso(ConvExpr env x1, ConvExpr env x2) |> asExpr
        | DerivedPatterns.OrElse(x1,x2)              -> Expression.OrElse(ConvExpr env x1, ConvExpr env x2)  |> asExpr
        | Patterns.Value(x,ty)                -> Expression.Constant(x,ty)              |> asExpr

        // REVIEW: exact F# semantics for TypeAs and TypeIs
        | Patterns.Coerce(x,toTy)             -> Expression.TypeAs(ConvExpr env x,toTy)     |> asExpr
        | Patterns.TypeTest(x,toTy)           -> Expression.TypeIs(ConvExpr env x,toTy)     |> asExpr
        
        // Expr.*Get
        | Patterns.FieldGet(objOpt,fieldInfo) -> 
            Expression.Field(ConvObjArg env objOpt None, fieldInfo) |> asExpr

        | Patterns.TupleGet(arg,n) -> 
             let argP = ConvExpr env arg 
             let rec build ty argP n = 
                 match Reflection.FSharpValue.PreComputeTuplePropertyInfo(ty,n) with 
                 | propInfo,None -> 
                     Expression.Property(argP, propInfo)  |> asExpr
                 | propInfo,Some(nestedTy,n2) -> 
                     build nestedTy (Expression.Property(argP,propInfo) |> asExpr) n2
             build arg.Type argP n
              
        | Patterns.PropertyGet(objOpt,propInfo,args) -> 
            let coerceTo = 
                if objOpt.IsSome && FSharpType.IsUnion propInfo.DeclaringType && FSharpType.IsUnion propInfo.DeclaringType.BaseType  then  
                    Some propInfo.DeclaringType
                else 
                    None
            match args with 
            | [] -> 
                Expression.Property(ConvObjArg env objOpt coerceTo, propInfo) |> asExpr
            | _ -> 
                let argsP = ConvExprs env args
                Expression.Call(ConvObjArg env objOpt coerceTo, propInfo.GetGetMethod(true),argsP) |> asExpr

        // Expr.*Set
        | Patterns.PropertySet(objOpt,propInfo,args,v) -> 
            let args = (args @ [v])
            let argsP = ConvExprs env args 
            let minfo = propInfo.GetSetMethod(true)
            Expression.Call(ConvObjArg env objOpt None, minfo,argsP) |> asExpr |> WrapVoid (IsVoidType minfo.ReturnType) objOpt args env 

        // Expr.(Call,Application)
        | Patterns.Call(objOpt,minfo,args) -> 
            match inp with 
            // Special cases for this translation
            |  PlusQ (_, [ty1;ty2;ty3],[x1;x2]) when (ty1 = typeof<string>) && (ty2 = typeof<string>) ->
                 ConvExpr env (<@@  System.String.Concat( [| %%x1 ; %%x2 |] : string array ) @@>)

            //| SpecificCall <@ LanguagePrimitives.GenericEquality @>([ty1],[x1;x2]) 
            //| SpecificCall <@ ( = ) @>([ty1],[x1;x2]) when (ty1 = typeof<string>) ->
            //     ConvExpr env (<@@  System.String.op_Equality(%%x1,%%x2) @@>)

            | GenericEqualityQ (_, _,[x1;x2]) 
            | EqualsQ (_, _,[x1;x2]) -> Expression.Equal(ConvExpr env x1, ConvExpr env x2)       |> asExpr

            | GreaterQ (_, _,[x1;x2]) -> Expression.GreaterThan(ConvExpr env x1, ConvExpr env x2)       |> asExpr
            | GreaterEqQ (_, _,[x1;x2]) -> Expression.GreaterThanOrEqual(ConvExpr env x1, ConvExpr env x2)       |> asExpr
            | LessQ (_, _,[x1;x2]) -> Expression.LessThan(ConvExpr env x1, ConvExpr env x2)       |> asExpr
            | LessEqQ (_, _,[x1;x2]) -> Expression.LessThanOrEqual(ConvExpr env x1, ConvExpr env x2)       |> asExpr
            | NotEqQ (_, _,[x1;x2]) -> Expression.NotEqual(ConvExpr env x1, ConvExpr env x2)       |> asExpr
            | NotQ (_, _,[x1])    -> Expression.Not(ConvExpr env x1)                                   |> asExpr
                 /// REVIEW: basic comparison with method witnesses

            | NegQ (_, _,[x1])    -> Expression.Negate(ConvExpr env x1)                                |> asExpr
            | PlusQ (_, _,[x1;x2]) -> Expression.Add(ConvExpr env x1, ConvExpr env x2)      |> asExpr
            | DivideQ (_, _,[x1;x2]) -> Expression.Divide (ConvExpr env x1, ConvExpr env x2)  |> asExpr
            | MinusQ (_, _,[x1;x2]) -> Expression.Subtract(ConvExpr env x1, ConvExpr env x2) |> asExpr
            | MultiplyQ (_, _,[x1;x2]) -> Expression.Multiply(ConvExpr env x1, ConvExpr env x2) |> asExpr
            | ModuloQ (_, _,[x1;x2]) -> Expression.Modulo (ConvExpr env x1, ConvExpr env x2) |> asExpr
                 /// REVIEW: basic arithmetic with method witnesses
                 /// REVIEW: negate,add, divide, multiply, subtract with method witness

            | ShiftLeftQ (_, _,[x1;x2]) -> Expression.LeftShift(ConvExpr env x1, ConvExpr env x2) |> asExpr
            | ShiftRightQ (_, _,[x1;x2]) -> Expression.RightShift(ConvExpr env x1, ConvExpr env x2) |> asExpr
            | BitwiseAndQ (_, _,[x1;x2]) -> Expression.And(ConvExpr env x1, ConvExpr env x2) |> asExpr
            | BitwiseOrQ (_, _,[x1;x2]) -> Expression.Or(ConvExpr env x1, ConvExpr env x2) |> asExpr
            | BitwiseXorQ (_, _,[x1;x2]) -> Expression.ExclusiveOr(ConvExpr env x1, ConvExpr env x2) |> asExpr
            | BitwiseNotQ (_, _,[x1]) -> Expression.Not(ConvExpr env x1) |> asExpr
                 /// REVIEW: bitwise operations with method witnesses

            | CheckedNeg (_, _,[x1]) -> Expression.NegateChecked(ConvExpr env x1)                                |> asExpr
            | CheckedPlusQ (_, _,[x1;x2]) -> Expression.AddChecked(ConvExpr env x1, ConvExpr env x2)      |> asExpr
            | CheckedMinusQ (_, _,[x1;x2]) -> Expression.SubtractChecked(ConvExpr env x1, ConvExpr env x2) |> asExpr
            | CheckedMultiplyQ (_, _,[x1;x2]) -> Expression.MultiplyChecked(ConvExpr env x1, ConvExpr env x2) |> asExpr

            | ConvCharQ (_, [ty],[x1])  -> Expression.Convert(ConvExpr env x1, typeof<char>) |> asExpr
            | ConvDecimalQ (_, [ty],[x1])  -> Expression.Convert(ConvExpr env x1, typeof<decimal>) |> asExpr
            | ConvFloatQ (_, [ty],[x1])  -> Expression.Convert(ConvExpr env x1, typeof<float>) |> asExpr
            | ConvFloat32Q (_, [ty],[x1])  -> Expression.Convert(ConvExpr env x1, typeof<float32>) |> asExpr
            | ConvSByteQ (_, [ty],[x1])  -> Expression.Convert(ConvExpr env x1, typeof<sbyte>) |> asExpr
            | ConvInt16Q (_, [ty],[x1])  -> Expression.Convert(ConvExpr env x1, typeof<int16>) |> asExpr
            | ConvInt32Q (_, [ty],[x1])  -> Expression.Convert(ConvExpr env x1, typeof<int32>) |> asExpr
            | ConvIntQ (_, [ty],[x1])    -> Expression.Convert(ConvExpr env x1, typeof<int32>) |> asExpr
            | ConvInt64Q (_, [ty],[x1])  -> Expression.Convert(ConvExpr env x1, typeof<int64>) |> asExpr
            | ConvByteQ (_, [ty],[x1])   -> Expression.Convert(ConvExpr env x1, typeof<byte>) |> asExpr
            | ConvUInt16Q (_, [ty],[x1]) -> Expression.Convert(ConvExpr env x1, typeof<uint16>) |> asExpr
            | ConvUInt32Q (_, [ty],[x1]) -> Expression.Convert(ConvExpr env x1, typeof<uint32>) |> asExpr
            | ConvUInt64Q (_, [ty],[x1]) -> Expression.Convert(ConvExpr env x1, typeof<uint64>) |> asExpr
             /// REVIEW: convert with method witness

            | CheckedConvCharQ (_, [ty],[x1])  -> Expression.ConvertChecked(ConvExpr env x1, typeof<char>) |> asExpr
            | CheckedConvSByteQ (_, [ty],[x1])  -> Expression.ConvertChecked(ConvExpr env x1, typeof<sbyte>) |> asExpr
            | CheckedConvInt16Q (_, [ty],[x1])  -> Expression.ConvertChecked(ConvExpr env x1, typeof<int16>) |> asExpr
            | CheckedConvInt32Q (_, [ty],[x1])  -> Expression.ConvertChecked(ConvExpr env x1, typeof<int32>) |> asExpr
            | CheckedConvInt64Q (_, [ty],[x1])  -> Expression.ConvertChecked(ConvExpr env x1, typeof<int64>) |> asExpr
            | CheckedConvByteQ (_, [ty],[x1])   -> Expression.ConvertChecked(ConvExpr env x1, typeof<byte>) |> asExpr
            | CheckedConvUInt16Q (_, [ty],[x1]) -> Expression.ConvertChecked(ConvExpr env x1, typeof<uint16>) |> asExpr
            | CheckedConvUInt32Q (_, [ty],[x1]) -> Expression.ConvertChecked(ConvExpr env x1, typeof<uint32>) |> asExpr
            | CheckedConvUInt64Q (_, [ty],[x1]) -> Expression.ConvertChecked(ConvExpr env x1, typeof<uint64>) |> asExpr
            | ArrayLookupQ (_, [ArrayTypeQ(elemTy);_;_],[x1;x2]) -> 
                Expression.ArrayIndex(ConvExpr env x1, ConvExpr env x2) |> asExpr

            | ArrayAssignQ (_, [ArrayTypeQ(elemTy);_;_],[arr;idx;elem]) -> 
                let minfo = ArrayAssignMethod.GetGenericMethodDefinition().MakeGenericMethod [| elemTy;typeof<unit> |]
                Expression.Call(minfo,[| ConvExpr env arr; ConvExpr env idx; ConvExpr env elem |]) |> asExpr
            
            // Throw away markers inserted to satisfy C#'s design where they pass an argument
            // or type T to an argument expecting Expr<T>.
            | LinqExpressionHelperQ (_, [_],[x1]) -> ConvExpr env x1
             
              /// ArrayLength
              /// ListBind
              /// ListInit
              /// ElementInit
            | _ -> 
                let argsP = ConvExprs env args 
                Expression.Call(ConvObjArg env objOpt None, minfo, argsP) |> asExpr |> WrapVoid (IsVoidType minfo.ReturnType) objOpt args env 

        // f x1 x2 x3 x4 --> InvokeFast4
        | Patterns.Application(Patterns.Application(Patterns.Application(Patterns.Application(f,arg1),arg2),arg3),arg4) -> 
            let domainTy1, rangeTy = getFunctionType f.Type
            let domainTy2, rangeTy = getFunctionType rangeTy
            let domainTy3, rangeTy = getFunctionType rangeTy
            let domainTy4, rangeTy = getFunctionType rangeTy
            let (-->) ty1 ty2 = Reflection.FSharpType.MakeFunctionType(ty1,ty2)
            let ty = domainTy1 --> domainTy2 
            let meth = (ty.GetMethods() |> Array.find (fun minfo -> minfo.Name = "InvokeFast" && minfo.GetParameters().Length = 5)).MakeGenericMethod [| domainTy3; domainTy4; rangeTy |]
            let argsP = ConvExprs env [f; arg1;arg2;arg3; arg4]
            Expression.Call((null:Expression), meth, argsP) |> asExpr

        // f x1 x2 x3 --> InvokeFast3
        | Patterns.Application(Patterns.Application(Patterns.Application(f,arg1),arg2),arg3) -> 
            let domainTy1, rangeTy = getFunctionType f.Type
            let domainTy2, rangeTy = getFunctionType rangeTy
            let domainTy3, rangeTy = getFunctionType rangeTy
            let (-->) ty1 ty2 = Reflection.FSharpType.MakeFunctionType(ty1,ty2)
            let ty = domainTy1 --> domainTy2 
            let meth = (ty.GetMethods() |> Array.find (fun minfo -> minfo.Name = "InvokeFast" && minfo.GetParameters().Length = 4)).MakeGenericMethod [| domainTy3; rangeTy |]
            let argsP = ConvExprs env [f; arg1;arg2;arg3]
            Expression.Call((null:Expression), meth, argsP) |> asExpr

        // f x1 x2 --> InvokeFast2
        | Patterns.Application(Patterns.Application(f,arg1),arg2) -> 
            let domainTy1, rangeTy = getFunctionType f.Type
            let domainTy2, rangeTy = getFunctionType rangeTy
            let (-->) ty1 ty2 = Reflection.FSharpType.MakeFunctionType(ty1,ty2)
            let ty = domainTy1 --> domainTy2 
            let meth = (ty.GetMethods() |> Array.find (fun minfo -> minfo.Name = "InvokeFast" && minfo.GetParameters().Length = 3)).MakeGenericMethod [| rangeTy |]
            let argsP = ConvExprs env [f; arg1;arg2]
            Expression.Call((null:Expression), meth, argsP) |> asExpr

        // f x1 --> Invoke
        | Patterns.Application(f,arg) -> 
            let fP = ConvExpr env f
            let argP = ConvExpr env arg
            let meth = f.Type.GetMethod("Invoke")
            Expression.Call(fP, meth, [| argP |]) |> asExpr

        // Expr.New*
        | Patterns.NewRecord(recdTy,args) -> 
            let ctorInfo = Reflection.FSharpValue.PreComputeRecordConstructorInfo(recdTy,showAll) 
            Expression.New(ctorInfo,ConvExprs env args) |> asExpr

        | Patterns.NewArray(ty,args) -> 
            Expression.NewArrayInit(ty,ConvExprs env args) |> asExpr

        | Patterns.DefaultValue(ty) -> 
            Expression.New(ty) |> asExpr

        | Patterns.NewUnionCase(unionCaseInfo,args) -> 
            let methInfo = Reflection.FSharpValue.PreComputeUnionConstructorInfo(unionCaseInfo,showAll)
            let argsR = ConvExprs env args 
            Expression.Call((null:Expression),methInfo,argsR) |> asExpr

        | Patterns.UnionCaseTest(e,unionCaseInfo) -> 
            let methInfo = Reflection.FSharpValue.PreComputeUnionTagMemberInfo(unionCaseInfo.DeclaringType,showAll)
            let obj = ConvExpr env e 
            let tagE = 
                match methInfo with 
                | :? PropertyInfo as p -> 
                    Expression.Property(obj,p) |> asExpr
                | :? MethodInfo as m -> 
                    Expression.Call((null:Expression),m,[| obj |]) |> asExpr
                | _ -> failwith "unreachable case"
            Expression.Equal(tagE, Expression.Constant(unionCaseInfo.Tag)) |> asExpr

        | Patterns.NewObject(ctorInfo,args) -> 
            Expression.New(ctorInfo,ConvExprs env args) |> asExpr

        | Patterns.NewDelegate(dty,vs,b) -> 
            let vsP = List.map ConvVar vs 
            let env = List.foldBack2 (fun (v:Var) vP -> Map.add v (vP |> asExpr)) vs vsP env 
            let bodyP = ConvExpr env b 
            Expression.Lambda(dty, bodyP, vsP) |> asExpr 

        | Patterns.NewTuple(args) -> 
             let tupTy = args |> List.map (fun arg -> arg.Type) |> Array.ofList |> Reflection.FSharpType.MakeTupleType
             let argsP = ConvExprs env args 
             let rec build ty (argsP: Expression[]) = 
                 match Reflection.FSharpValue.PreComputeTupleConstructorInfo(ty) with 
                 | ctorInfo,None -> Expression.New(ctorInfo,argsP) |> asExpr 
                 | ctorInfo,Some(nestedTy) -> 
                     let n = ctorInfo.GetParameters().Length - 1
                     Expression.New(ctorInfo, Array.append argsP.[0..n-1] [| build nestedTy argsP.[n..] |]) |> asExpr
             build tupTy argsP

        | Patterns.IfThenElse(g,t,e) -> 
            Expression.Condition(ConvExpr env g, ConvExpr env t,ConvExpr env e) |> asExpr

        | Patterns.Sequential (e1,e2) -> 
            let e1P = ConvExpr env e1
            let e2P = ConvExpr env e2
            let minfo = match <@@ SequentialHelper @@> with Lambdas(_,Call(_,minfo,_)) -> minfo | _ -> failwith "couldn't find minfo"
            let minfo = minfo.GetGenericMethodDefinition().MakeGenericMethod [| e1.Type; e2.Type |]
            Expression.Call(minfo,[| e1P; e2P |]) |> asExpr

        | Patterns.Let (v,e,b) -> 
            let vP = ConvVar v
            let envinner = Map.add v (vP |> asExpr) env 
            let bodyP = ConvExpr envinner b 
            let eP = ConvExpr env e
            let ty = GetFuncType [| v.Type; b.Type |] 
            let lam = Expression.Lambda(ty,bodyP,[| vP |]) |> asExpr
            Expression.Call(lam,ty.GetMethod("Invoke",instanceBindingFlags),[| eP |]) |> asExpr

        | Patterns.Lambda(v,body) -> 
            let vP = ConvVar v
            let env = Map.add v (vP |> asExpr) env 
            let tyargs = [| v.Type; body.Type |]
            let bodyP = ConvExpr env body
            let lambdaTy, tyargs =
                if bodyP.Type = typeof<System.Void> then
                    let tyargs = [| vP.Type |]
                    typedefof<Action<_>>, tyargs
                else
                    let tyargs = [| vP.Type; bodyP.Type |]
                    typedefof<Func<_, _>>, tyargs
            let convType = lambdaTy.MakeGenericType tyargs
            let convDelegate = Expression.Lambda(convType, bodyP, [| vP |]) |> asExpr
            Expression.Call(typeof<FuncConvert>, "ToFSharpFunc", tyargs, [| convDelegate |]) |> asExpr

        | Patterns.WhileLoop(gd,b) -> 
            let gdP = ConvExpr env <@@ (fun () -> (%%gd:bool)) @@>
            let bP = ConvExpr env <@@ (fun () -> (%%b:unit)) @@>
            let minfo = WhileMethod.GetGenericMethodDefinition().MakeGenericMethod [| typeof<unit> |]
            Expression.Call(minfo,[| gdP; bP |]) |> asExpr

        | Patterns.TryFinally(e,h) -> 
            let eP = ConvExpr env (Expr.Lambda(new Var("unitVar",typeof<unit>), e))
            let hP = ConvExpr env <@@ (fun () -> (%%h:unit)) @@>
            let minfo = TryFinallyMethod.GetGenericMethodDefinition().MakeGenericMethod [| e.Type |]
            Expression.Call(minfo,[| eP; hP |]) |> asExpr

        | Patterns.TryWith(e,vf,filter,vh,handler) -> 
            let eP = ConvExpr env (Expr.Lambda(new Var("unitVar",typeof<unit>), e))
            let filterP = ConvExpr env (Expr.Lambda(vf,filter))
            let handlerP = ConvExpr env (Expr.Lambda(vh,handler))
            let minfo = TryWithMethod.GetGenericMethodDefinition().MakeGenericMethod [| e.Type |]
            Expression.Call(minfo,[| eP; filterP; handlerP |]) |> asExpr

        | Patterns.LetRecursive(binds,body) -> 

            let vfs = List.map fst binds
            
            let pass1 = 
                binds |> List.map (fun (vf,expr) -> 
                    match expr with 
                    | Lambda(vx,expr) -> 
                        let domainTy,rangeTy = getFunctionType vf.Type
                        let vfdTy = GetFuncType [| domainTy; rangeTy |]
                        let vfd = new Var("d",vfdTy)
                        (vf,vx,expr,domainTy,rangeTy,vfdTy,vfd)
                    | _ -> failwith "cannot convert recursive bindings that do not define functions")

            let trans = pass1 |> List.map (fun (vf,vx,expr,domainTy,rangeTy,vfdTy,vfd) -> (vf,vfd)) |> Map.ofList

            // Rewrite uses of the recursively defined functions to be invocations of the delegates
            // We do this because the delegate are allocated "once" and we can normally just invoke them efficiently
            let rec rw t = 
                match t with 
                | Application(Var(vf),t) when trans.ContainsKey(vf) -> 
                     let vfd = trans.[vf]
                     Expr.Call(Expr.Var(vfd),vfd.Type.GetMethod("Invoke",instanceBindingFlags),[t])
                | ExprShape.ShapeVar(vf) when trans.ContainsKey(vf)-> 
                     let vfd = trans.[vf]
                     let nv = new Var("nv",fst(getFunctionType vf.Type)) 
                     Expr.Lambda(nv,Expr.Call(Expr.Var(vfd),vfd.Type.GetMethod("Invoke",instanceBindingFlags),[Expr.Var(nv)]))
                | ExprShape.ShapeVar(_) -> t
                | ExprShape.ShapeCombination(obj,args) -> ExprShape.RebuildShapeCombination(obj,List.map rw args)
                | ExprShape.ShapeLambda(v,arg) -> Expr.Lambda(v,rw arg)

            let vfdTys    = pass1 |> List.map (fun (vf,vx,expr,domainTy,rangeTy,vfdTy,vfd) -> vfdTy) |> Array.ofList
            let vfds      = pass1 |> List.map (fun (vf,vx,expr,domainTy,rangeTy,vfdTy,vfd) -> vfd)

            let FPs = 
                [| for (vf,vx,expr,domainTy,rangeTy,vfdTy,vfd) in pass1 do
                      let expr = rw expr
                      let tyF = GetFuncType (Array.append vfdTys [| vx.Type; expr.Type |])
                      let F = Expr.NewDelegate(tyF,vfds@[vx],expr)
                      let FP = ConvExpr env F
                      yield FP |]

            let body = rw body

            let methTys   = 
                [| for (vf,vx,expr,domainTy,rangeTy,vfdTy,vfd) in pass1 do
                      yield domainTy
                      yield rangeTy
                   yield body.Type |]

            let B = Expr.NewDelegate(GetFuncType (Array.append vfdTys [| body.Type |]),vfds,body)
            let BP = ConvExpr env B

            let minfo = 
                let q = 
                    match vfds.Length with 
                    | 1 -> <@@ LetRec1Helper @@>
                    | 2 -> <@@ LetRec2Helper @@>
                    | 3 -> <@@ LetRec3Helper @@>
                    | 4 -> <@@ LetRec4Helper @@>
                    | 5 -> <@@ LetRec5Helper @@>
                    | 6 -> <@@ LetRec6Helper @@>
                    | 7 -> <@@ LetRec7Helper @@>
                    | 8 -> <@@ LetRec8Helper @@>
                    | _ -> raise <| new NotSupportedException("In this release of the F# Power Pack, mutually recursive function groups involving 9 or more functions may not be converted to LINQ expressions")
                match q with Lambdas(_,Call(_,minfo,_)) -> minfo | _ -> failwith "couldn't find minfo"

            let minfo = minfo.GetGenericMethodDefinition().MakeGenericMethod methTys
            Expression.Call(minfo,Array.append FPs [| BP |]) |> asExpr

        | Patterns.AddressOf _ -> raise <| new NotSupportedException("Address-of expressions may not be converted to LINQ expressions")
        | Patterns.AddressSet _ -> raise <| new NotSupportedException("Address-set expressions may not be converted to LINQ expressions")
        | Patterns.FieldSet _ -> raise <| new NotSupportedException("Field-set expressions may not be converted to LINQ expressions")

        | _ -> 
            raise <| new NotSupportedException(sprintf "Could not convert the following F# Quotation to a LINQ Expression Tree\n--------\n%A\n-------------\n" inp)

    and ConvObjArg env objOpt coerceTo : Expression = 
        match objOpt with
        | Some(obj) -> 
            let expr = ConvExpr env obj
            match coerceTo with 
            | None -> expr
            | Some ty -> Expression.TypeAs(expr, ty) :> Expression
        | None -> 
            null

    and ConvExprs env es : Expression[] = 
        es |> List.map (ConvExpr env) |> Array.ofList 

    and ConvVar (v: Var) = 
        //printf "** Expression .Parameter(%a, %a)\n" output_any ty output_any nm;
        Expression.Parameter(v.Type, v.Name)

    let Conv (e: #Expr) = ConvExpr Map.empty (e :> Expr)

    let Compile (e: #Expr) = 
       let ty = e.Type
       let e = Expr.NewDelegate(GetFuncType([|typeof<unit>; ty |]), [new Var("unit",typeof<unit>)],e)
       let linqExpr = Conv e
       let linqExpr = (linqExpr :?> LambdaExpression)
       let d = linqExpr.Compile()
       (fun () -> 
           try 
             d.DynamicInvoke [| box () |]
           with :? System.Reflection.TargetInvocationException as exn -> 
               raise exn.InnerException)

    let Eval e = Compile e ()

    type Microsoft.FSharp.Quotations.Expr with 
        member x.ToLinqExpression() = Conv(x)
        member x.CompileUntyped() = Compile(x)
        member x.EvalUntyped() = Eval(x)

    type Microsoft.FSharp.Quotations.Expr<'T> with 
        member x.Compile() = 
            let f = Compile(x)  
            (fun () -> (f()) :?> 'T)
        member x.Eval() = (Eval(x) :?> 'T)

  
open QuotationEvaluation

let mutable failures = []
let report_failure () = 
  stderr.WriteLine " NO"; 


let check  s v1 v2 = 
   if v1 = v2 then 
       printfn "test %s...passed " s 
   else 
       failures <- failures @ [(s, box v1, box v2)]
       printfn "test %s...failed, expected %A got %A" s v2 v1

let test s b = check s b true

// The following hopefully is an identity function on quotations:
let Id (x: Expr<'T>) : Expr<'T> = 
    let rec conv x = 
        match x with
        | ShapeVar _ -> 
            x
        | ShapeLambda (head, body) -> 
            Expr.Lambda (head, conv body)    
        | ShapeCombination (head, tail) -> 
            RebuildShapeCombination (head, List.map conv tail)
    conv x |> Expr.Cast

let Eval (q: Expr<_>) = 
    q.ToLinqExpression() |> ignore 
    q.Compile() |> ignore  
    q.Eval()

let checkEval nm (q : Expr<'T>) expected = 
    check nm (Eval q) expected
    check (nm + "(after applying Id)") (Eval (Id q)) expected
    check (nm + "(after applying Id^2)")  (Eval (Id (Id q))) expected

module EvaluationTests = 

    let f () = () 

    checkEval "cwe90wecmp" (<@ f ()  @> ) ()

    checkEval "vlwjvrwe90" (<@ let f (x:int) (y:int) = x + y in f 1 2  @>) 3

    //debug <- true


    checkEval "slnvrwe90" (<@ let rec f (x:int) : int = f x in 1  @>) 1

    checkEval "2ver9ewva" (<@ let rec f1 (x:int) : int = f2 x 
                              and f2 x = f1 x 
                              1  @>) 1

    checkEval "2ver9ewvq" 
      (<@ let rec f1 (x:int) : int = f2 x 
          and f2 x = f3 x 
          and f3 x = f1 x 
          1  @>) 
      1

    checkEval "2ver9ewvq" 
      (<@ let rec f1 (x:int) : int = f2 (x-1) 
          and f2 x = f3 (x-1) 
          and f3 x = if x < 0 then -1 else f1 (x-1) 
          f1 100  @>) 
      -1


    checkEval "2ver9ewvq" 
      (<@ let rec f1 (x:int) : int = f2 (x-1) 
          and f2 x = f3 (x-1) 
          and f3 x = fend (x-1) 
          and fend x = if x < 0 then -1 else f1 (x-1) 
          f1 100  @>) 
      -1

    checkEval "2ver9ewvq" 
      (<@ let rec f1 (x:int) : int = f2 (x-1) 
          and f2 x = f3 (x-1) 
          and f3 x = f4 (x-1) 
          and f4 x = fend (x-1) 
          and fend x = if x < 0 then -1 else f1 (x-1) 
          f1 100  @>) 
      -1

    checkEval "2ver9ewvq" 
      (<@ let rec f1 (x:int) : int = f2 (x-1) 
          and f2 x = f3 (x-1) 
          and f3 x = f4 (x-1) 
          and f4 x = f5 (x-1) 
          and f5 x = fend (x-1) 
          and fend x = if x < 0 then -1 else f1 (x-1) 
          f1 100  @>) 
      -1


    checkEval "2ver9ewvq" 
      (<@ let rec f1 (x:int) : int = f2 (x-1) 
          and f2 x = f3 (x-1) 
          and f3 x = f4 (x-1) 
          and f4 x = f5 (x-1) 
          and f5 x = f6 (x-1) 
          and f6 x = fend (x-1) 
          and fend x = if x < 0 then -1 else f1 (x-1) 
          f1 100  @>) 
      -1

    checkEval "2ver9ewvq" 
      (<@ let rec f1 (x:int) : int = f2 (x-1) 
          and f2 x = f3 (x-1) 
          and f3 x = f4 (x-1) 
          and f4 x = f5 (x-1) 
          and f5 x = f6 (x-1) 
          and f6 x = f7 (x-1) 
          and f7 x = fend (x-1) 
          and fend x = if x < 0 then -1 else f1 (x-1) 
          f1 100  @>) 
      -1



    checkEval "2ver9ewv1" (<@ let rec f (x:int) : int = x+x in f 2  @>) 4

    Eval <@ let rec fib x = if x <= 2 then 1 else fib (x-1) + fib (x-2) in fib 36 @> 
    //(let rec fib x = if x <= 2 then 1 else fib (x-1) + fib (x-2) in fib 36)

    //2.53/0.35

    checkEval "2ver9ewv2" (<@ if true then 1 else 0  @>) 1
    checkEval "2ver9ewv3" (<@ if false then 1 else 0  @>) 0
    checkEval "2ver9ewv4" (<@ true && true @>) true
    checkEval "2ver9ewv5" (<@ true && false @>) false
    check "2ver9ewv6" (try Eval <@ failwith "fail" : int @> with Failure "fail" -> 1 | _ -> 0) 1 
    check "2ver9ewv7" (try Eval <@ true && (failwith "fail") @> with Failure "fail" -> true | _ -> false) true
    checkEval "2ver9ewv8" (<@ 0x001 &&& 0x100 @>) (0x001 &&& 0x100)
    checkEval "2ver9ewv9" (<@ 0x001 ||| 0x100 @>) (0x001 ||| 0x100)
    checkEval "2ver9ewvq" (<@ 0x011 ^^^ 0x110 @>) (0x011 ^^^ 0x110)
    checkEval "2ver9ewvw" (<@ ~~~0x011 @>) (~~~0x011)

    let _ = 1
    checkEval "2ver9ewve" (<@ () @>) ()
    check "2ver9ewvr" (Eval <@ (fun x -> x + 1) @> (3)) 4
    check "2ver9ewvt" (Eval <@ (fun (x,y) -> x + 1) @> (3,4)) 4
    check "2ver9ewvy" (Eval <@ (fun (x1,x2,x3) -> x1 + x2 + x3) @> (3,4,5)) (3+4+5)
    check "2ver9ewvu" (Eval <@ (fun (x1,x2,x3,x4) -> x1 + x2 + x3 + x4) @> (3,4,5,6)) (3+4+5+6)
    check "2ver9ewvi" (Eval <@ (fun (x1,x2,x3,x4,x5) -> x1 + x2 + x3 + x4 + x5) @> (3,4,5,6,7)) (3+4+5+6+7)
    check "2ver9ewvo" (Eval <@ (fun (x1,x2,x3,x4,x5,x6) -> x1 + x2 + x3 + x4 + x5 + x6) @> (3,4,5,6,7,8)) (3+4+5+6+7+8)
    check "2ver9ewvp" (Eval <@ (fun (x1,x2,x3,x4,x5,x6,x7) -> x1 + x2 + x3 + x4 + x5 + x6 + x7) @> (3,4,5,6,7,8,9)) (3+4+5+6+7+8+9)
    check "2ver9ewva" (Eval <@ (fun (x1,x2,x3,x4,x5,x6,x7,x8) -> x1 + x2 + x3 + x4 + x5 + x6 + x7 + x8) @> (3,4,5,6,7,8,9,10)) (3+4+5+6+7+8+9+10)
    check "2ver9ewvs" (Eval <@ (fun (x1,x2,x3,x4,x5,x6,x7,x8,x9) -> x1 + x2 + x3 + x4 + x5 + x6 + x7 + x8 + x9) @> (3,4,5,6,7,8,9,10,11)) (3+4+5+6+7+8+9+10+11)
    check "2ver9ewvd" (Eval <@ (fun (x1,x2,x3,x4,x5,x6,x7,x8,x9,x10) -> x1 + x2 + x3 + x4 + x5 + x6 + x7 + x8 + x9 + x10) @> (3,4,5,6,7,8,9,10,11,12)) (3+4+5+6+7+8+9+10+11+12)
    check "2ver9ewvf" (Eval <@ (fun (x1,x2,x3,x4,x5,x6,x7,x8,x9,x10,x11) -> x1 + x2 + x3 + x4 + x5 + x6 + x7 + x8 + x9 + x10 + x11) @> (3,4,5,6,7,8,9,10,11,12,13)) (3+4+5+6+7+8+9+10+11+12+13)
    check "2ver9ewvg" (Eval <@ (fun (x1,x2,x3,x4,x5,x6,x7,x8,x9,x10,x11,x12) -> x1 + x2 + x3 + x4 + x5 + x6 + x7 + x8 + x9 + x10 + x11 + x12) @> (3,4,5,6,7,8,9,10,11,12,13,14)) (3+4+5+6+7+8+9+10+11+12+13+14)

    checkEval "2ver9ewvh" (<@ while false do ()  @>) ()
    checkEval "2ver9ewvj" (<@ let rec f (x:int) : int = f x in 1  @>) 1

    checkEval "2ver9ewvk" (<@ 1 + 1 @>) 2
    checkEval "2ver9ewvl" (<@ 1 > 1 @>) false
    checkEval "2ver9ewvz" (<@ 1 < 1 @>) false
    checkEval "2ver9ewvx" (<@ 1 <= 1 @>) true
    checkEval "2ver9ewvc" (<@ 1 >= 1 @>) true
    Eval <@ System.DateTime.Now @>
    checkEval "2ver9ewvv" (<@ System.Int32.MaxValue @>) System.Int32.MaxValue  // literal field!
    checkEval "2ver9ewvb" (<@ None  : int option @>) None
    checkEval "2ver9ewvn" (<@ Some(1)  @>) (Some(1))
    checkEval "2ver9ewvm" (<@ [] : int list @>) []
    checkEval "2ver9ewqq" (<@ [1] @>) [1]
    checkEval "2ver9ewqq" (<@ ["1"] @>) ["1"]
    checkEval "2ver9ewqq" (<@ ["1";"2"] @>) ["1";"2"]
    check "2ver9ewww" (Eval <@ (fun x -> x + 1) @> 3) 4

    let v = (1,2)
    checkEval "2ver9ewer" (<@ v @>) (1,2)
    checkEval "2ver9ewet" (<@ let x = 1 in x @>) 1
    checkEval "2ver9ewed" (<@ let x = 1+1 in x+x @>) 4
    let x = ref 0
    let incrx () = incr x

    checkEval "2ver9ewvec" (<@ !x @>) 0
    checkEval "2ver9ewev" (<@ incrx() @>) ()
    checkEval "2ver9eweb" (<@ !x @>) 3  // NOTE: checkEval evaluates the quotation three times :-)
    checkEval "2ver9ewen" (<@ while !x < 10 do incrx() @>) ()
    checkEval "2ver9ewem" (<@ !x @>) 10

    let raise x = Operators.raise x
    check "2ver9ewveq" (try Eval <@ raise (new System.Exception("hello")) : bool @> with :? System.Exception -> true | _ -> false) true

    
    check "2ver9ewrf" (let v2 = (3,4) in Eval <@ v2 @>) (3,4)
    
    check "2ver9ewrg" (let v2 = (3,4) in Eval <@ v2,v2 @>) ((3,4),(3,4))

    checkEval "2ver9ewrt" (<@ (1,2) @>) (1,2)
    checkEval "2ver9ewvk" (<@ (1,2,3) @>) (1,2,3)
    checkEval "2ver9ewrh" (<@ (1,2,3,4) @>) (1,2,3,4)
    checkEval "2ver9ewrj" (<@ (1,2,3,4,5) @>) (1,2,3,4,5)
    checkEval "2ver9ewrk" (<@ (1,2,3,4,5,6) @>) (1,2,3,4,5,6)
    checkEval "2ver9ewrl" (<@ (1,2,3,4,5,6,7) @>) (1,2,3,4,5,6,7)
    checkEval "2ver9ewra" (<@ (1,2,3,4,5,6,7,8) @>) (1,2,3,4,5,6,7,8)
    checkEval "2ver9ewrs" (<@ (1,2,3,4,5,6,7,8,9) @>) (1,2,3,4,5,6,7,8,9)
    checkEval "2ver9ewrx" (<@ (1,2,3,4,5,6,7,8,9,10) @>) (1,2,3,4,5,6,7,8,9,10)
    checkEval "2ver9ewrc" (<@ (1,2,3,4,5,6,7,8,9,10,11) @>) (1,2,3,4,5,6,7,8,9,10,11)
    checkEval "2ver9ewrv" (<@ (1,2,3,4,5,6,7,8,9,10,11,12) @>) (1,2,3,4,5,6,7,8,9,10,11,12)
    checkEval "2ver9ewrb" (<@ System.DateTime.Now.DayOfWeek @>) System.DateTime.Now.DayOfWeek
    checkEval "2ver9ewrn" (<@ Checked.(+) 1 1 @>) 2
    checkEval "2ver9ewrm" (<@ Checked.(-) 1 1 @>) 0
    checkEval "2ver9ewrw" (<@ Checked.( * ) 1 1 @>) 1
    // TODO (let) : let v2 = (3,4) in Eval <@ match v2 with (x,y) -> x + y @>
    // TODO: Eval <@ "1" = "2" @>

    module NonGenericRecdTests = 
        type Customer = { mutable Name:string; Data: int }
        let c1 = { Name="Don"; Data=6 }
        let c2 = { Name="Peter"; Data=7 }
        let c3 = { Name="Freddy"; Data=8 }
        checkEval "2ver9e1rw1" (<@ c1.Name @>) "Don"
        checkEval "2ver9e2rw2" (<@ c2.Name @>) "Peter"
        checkEval "2ver9e3rw3" (<@ c2.Data @>) 7
        checkEval "2ver9e7rw4" (<@ { Name = "Don"; Data = 6 } @>) { Name="Don"; Data=6 }
        checkEval "2ver9e7rw5" (<@ { Name = "Don"; Data = 6 } @>) { Name="Don"; Data=6 }

    module GenericRecdTests = 
        type CustomerG<'a> = { mutable Name:string; Data: 'a }
        let c1 : CustomerG<int> = { Name="Don"; Data=6 }
        let c2 : CustomerG<int> = { Name="Peter"; Data=7 }
        let c3 : CustomerG<string> = { Name="Freddy"; Data="8" }
        checkEval "2ver9e4rw6" (<@ c1.Name @>) "Don"
        checkEval "2ver9e5rw7" (<@ c2.Name @>) "Peter"
        checkEval "2ver9e6rw8" (<@ c2.Data @>) 7
        checkEval "2ver9e7rw9" (<@ c3.Data @>) "8"
        checkEval "2ver9e7rwQ" (<@ { Name = "Don"; Data = 6 } @>) { Name="Don"; Data=6 }
        checkEval "2ver9e7rwW" (<@ c1.Name <- "Ali Baba" @>) ()
        checkEval "2ver9e7rwE" (<@ c1.Name  @>) "Ali Baba"

    module ArrayTests = 
        checkEval "2ver9e8rwR1" (<@ [| |]  @>) ([| |] : int array)
        checkEval "2ver9e8rwR2" (<@ [| 0 |]  @>) ([| 0 |] : int array)
        checkEval "2ver9e8rwR3" (<@ [| 0  |].[0]  @>) 0
        checkEval "2ver9e8rwR4" (<@ [| 1; 2  |].[0]  @>) 1
        checkEval "2ver9e8rwR5" (<@ [| 1; 2  |].[1]  @>) 2

    module Array2DTests = 
        checkEval "2ver9e8rwR6" (<@ (Array2D.init 3 4 (fun i j -> i + j)).[0,0] @>) 0
        checkEval "2ver9e8rwR7" (<@ (Array2D.init 3 4 (fun i j -> i + j)).[1,2] @>) 3
        checkEval "2ver9e8rwR8" (<@ (Array2D.init 3 4 (fun i j -> i + j)) |> Array2D.base1 @>) 0
        checkEval "2ver9e8rwR9" (<@ (Array2D.init 3 4 (fun i j -> i + j)) |> Array2D.base2 @>) 0
        checkEval "2ver9e8rwRQ" (<@ (Array2D.init 3 4 (fun i j -> i + j)) |> Array2D.length1 @>) 3
        checkEval "2ver9e8rwRW" (<@ (Array2D.init 3 4 (fun i j -> i + j)) |> Array2D.length2 @>) 4


    module Array3DTests = 
        checkEval "2ver9e8rwRE" (<@ (Array3D.init 3 4 5 (fun i j k -> i + j)).[0,0,0] @>) 0
        checkEval "2ver9e8rwRR" (<@ (Array3D.init 3 4 5 (fun i j k -> i + j + k)).[1,2,3] @>) 6
        checkEval "2ver9e8rwRT" (<@ (Array3D.init 3 4 5 (fun i j k -> i + j)) |> Array3D.length1 @>) 3
        checkEval "2ver9e8rwRY" (<@ (Array3D.init 3 4 5 (fun i j k -> i + j)) |> Array3D.length2 @>) 4
        checkEval "2ver9e8rwRU" (<@ (Array3D.init 3 4 5 (fun i j k -> i + j)) |> Array3D.length3 @>) 5

    module ExceptionTests = 
        exception E0
        exception E1 of string
        let c1 = E0
        let c2 = E1 "1"
        let c3 = E1 "2"
        checkEval "2ver9e8rwR" (<@ E0  @>) E0
        checkEval "2ver9e8rwT" (<@ E1 "1"  @>) (E1 "1")
        checkEval "2ver9eQrwY" (<@ match c1 with E0 -> 1 | _ -> 2  @>) 1
        checkEval "2ver9eQrwU" (<@ match c2 with E0 -> 1 | _ -> 2  @>) 2
        checkEval "2ver9eQrwI" (<@ match c2 with E0 -> 1 | E1 _  -> 2 | _ -> 3  @>) 2
        checkEval "2ver9eQrwO" (<@ match c2 with E1 _  -> 2 | E0 -> 1 | _ -> 3  @>) 2
        checkEval "2ver9eQrwP" (<@ match c2 with E1 "1"  -> 2 | E0 -> 1 | _ -> 3  @>) 2
        checkEval "2ver9eQrwA" (<@ match c2 with E1 "2"  -> 2 | E0 -> 1 | _ -> 3  @>) 3
        checkEval "2ver9eQrwS" (<@ match c3 with E1 "2"  -> 2 | E0 -> 1 | _ -> 3  @>) 2
        checkEval "2ver9eQrwD1" (<@ try failwith "" with _ -> 2  @>) 2
        checkEval "2ver9eQrwD2" (<@ let x = ref 0 in 
                                    try 
                                           try failwith "" 
                                           finally incr x 
                                    with _ -> !x @>) 1
        checkEval "2ver9eQrwD3" (<@ let x = ref 0 in 
                                    (try incr x; incr x
                                     finally incr x )
                                    x.Value @>) 3
        checkEval "2ver9eQrwD4" (<@ try 3 finally () @>) 3
        checkEval "2ver9eQrwD5" (<@ try () finally () @>) ()
        checkEval "2ver9eQrwD6" (<@ try () with _ -> () @>) ()
        checkEval "2ver9eQrwD7" (<@ try raise E0 with E0 -> 2  @>) 2
        checkEval "2ver9eQrwF" (<@ try raise c1 with E0 -> 2  @>) 2
        checkEval "2ver9eQrwG" (<@ try raise c2 with E0 -> 2 | E1 "1" -> 3 @>) 3
        checkEval "2ver9eQrwH" (<@ try raise c2 with E1 "1" -> 3 | E0 -> 2  @>) 3

    module TypeTests = 
        type C0() = 
            member x.P = 1
        type C1(s:string) = 
            member x.P = s
        let c1 = C0()
        let c2 = C1 "1"
        let c3 = C1 "2"
        checkEval "2ver9e8rwJ" (<@ C0().P  @>) 1
        checkEval "2ver9e8rwK" (<@ C1("1").P  @>)  "1"
        checkEval "2ver9eQrwL" (<@ match box c1 with :? C0 -> 1 | _ -> 2  @>) 1
        checkEval "2ver9eQrwZ" (<@ match box c2 with :? C0 -> 1 | _ -> 2  @>) 2
        checkEval "2ver9eQrwX" (<@ match box c2 with :? C0 -> 1 | :? C1   -> 2 | _ -> 3  @>) 2
        checkEval "2ver9eQrwC" (<@ match box c2 with :? C1   -> 2 | :? C0 -> 1 | _ -> 3  @>) 2
        checkEval "2ver9eQrwV" (<@ match box c2 with :? C1  -> 2 | :? C0 -> 1 | _ -> 3  @>) 2
        checkEval "2ver9eQrwN" (<@ match box c3 with :? C1  as c1 when c1.P = "2"  -> 2 | :? C0 -> 1 | _ -> 3  @>) 2

    module NonGenericUnionTests0 = 
        type Animal = Cat of string | Dog
        let c1 = Cat "meow"
        let c2 = Dog
        checkEval "2ver9e8rw11" (<@ Cat "sss" @>) (Cat "sss")
        checkEval "2ver9e9rw12" (<@ Dog @>) Dog
        checkEval "2ver9eQrw13" (<@ match c1 with Cat _ -> 2 | Dog -> 1 @>) 2
        checkEval "2ver9eWrw14" (<@ match c1 with Cat s -> s | Dog -> "woof" @>) "meow"
        checkEval "2ver9eErw15" (<@ match c2 with Cat s -> s | Dog -> "woof" @>) "woof"

    module NonGenericUnionTests1 = 
        type Animal = Cat of string 
        let c1 = Cat "meow"
        checkEval "2ver9e8rw16" (<@ Cat "sss" @>) (Cat "sss")
        checkEval "2ver9eQrw17" (<@ match c1 with Cat _ -> 2  @>) 2
        checkEval "2ver9eWrw18" (<@ match c1 with Cat s -> s  @>) "meow"

    module NonGenericUnionTests2 = 
        type Animal = 
           | Cat of string 
           | Dog of string
        let c1 = Cat "meow"
        let c2 = Dog "woof"
        checkEval "2ver9e8rw19" (<@ Cat "sss" @>) (Cat "sss")
        checkEval "2ver9e9rw20" (<@ Dog "bowwow" @>) (Dog "bowwow")
        checkEval "2ver9eQrw21" (<@ match c1 with Cat _ -> 2 | Dog  _ -> 1 @>) 2
        checkEval "2ver9eWrw22" (<@ match c1 with Cat s -> s | Dog s -> s @>) "meow"
        checkEval "2ver9eErw23" (<@ match c2 with Cat s -> s | Dog s -> s @>) "woof"

    module NonGenericUnionTests3 = 
        type Animal = 
           | Cat of string 
           | Dog of string
           | Dog1 of string
           | Dog2 of string
           | Dog3 of string
           | Dog4 of string
           | Dog5 of string
           | Dog6 of string
           | Dog7 of string
           | Dog8 of string
           | Dog9 of string
           | DogQ of string
           | DogW of string
           | DogE of string
           | DogR of string
           | DogT of string
           | DogY of string
           | DogU of string
           | DogI of string
        let c1 = Cat "meow"
        let c2 = Dog "woof"
        checkEval "2ver9e8rw24" (<@ Cat "sss" @>) (Cat "sss")
        checkEval "2ver9e9rw25" (<@ Dog "bowwow" @>) (Dog "bowwow")
        checkEval "2ver9eQrw26" (<@ match c1 with Cat _ -> 2 | _ -> 1 @>) 2
        checkEval "2ver9eWrw27" (<@ match c1 with Cat s -> s | _ -> "woof" @>) "meow"
        checkEval "2ver9eErw28" (<@ match c2 with Cat s -> s | Dog s -> s | _ -> "bark" @>) "woof"


    module GenericUnionTests = 
        type Animal<'a> = Cat of 'a | Dog
        let c1 = Cat "meow"
        let c2 = Dog
        checkEval "2ver9e8rw29" (<@ Cat "sss" @>) (Cat "sss")
        checkEval "2ver9e9rw30" (<@ Dog @>) Dog
        checkEval "2ver9eQrw31" (<@ match c1 with Cat _ -> 2 | Dog -> 1 @>) 2
        checkEval "2ver9eWrw32" (<@ match c1 with Cat s -> s | Dog -> "woof" @>) "meow"
        checkEval "2ver9eErw33" (<@ match c2 with Cat s -> s | Dog -> "woof" @>) "woof"

    module InlinedOperationsStillDynamicallyAvailableTests = 

        checkEval "vroievr091a" (<@ LanguagePrimitives.GenericZero<char> @>)  '\000'
        checkEval "vroievr091b" (<@ LanguagePrimitives.GenericZero<sbyte> @>)  0y
        checkEval "vroievr091c" (<@ LanguagePrimitives.GenericZero<int16> @>)  0s
        checkEval "vroievr091d" (<@ LanguagePrimitives.GenericZero<int32> @>)  0
        checkEval "vroievr091e" (<@ LanguagePrimitives.GenericZero<int64> @>)  0L
        checkEval "vroievr091f" (<@ LanguagePrimitives.GenericZero<nativeint> @>)  0n
        checkEval "vroievr091g" (<@ LanguagePrimitives.GenericZero<byte> @>)  0uy
        checkEval "vroievr091h" (<@ LanguagePrimitives.GenericZero<uint16> @>)  0us
        checkEval "vroievr091i" (<@ LanguagePrimitives.GenericZero<uint32> @>)  0u
        checkEval "vroievr091j" (<@ LanguagePrimitives.GenericZero<uint64> @>)  0UL
        checkEval "vroievr091k" (<@ LanguagePrimitives.GenericZero<unativeint> @>)  0un
        checkEval "vroievr091l" (<@ LanguagePrimitives.GenericZero<float> @>)  0.0
        checkEval "vroievr091m" (<@ LanguagePrimitives.GenericZero<float32> @>)  0.0f
        checkEval "vroievr091n" (<@ LanguagePrimitives.GenericZero<decimal> @>)  0M

        checkEval "vroievr092a" (<@ LanguagePrimitives.GenericOne<char> @>)  '\001'
        checkEval "vroievr092b" (<@ LanguagePrimitives.GenericOne<sbyte> @>)  1y
        checkEval "vroievr092c" (<@ LanguagePrimitives.GenericOne<int16> @>)  1s
        checkEval "vroievr092d" (<@ LanguagePrimitives.GenericOne<int32> @>)  1
        checkEval "vroievr092e" (<@ LanguagePrimitives.GenericOne<int64> @>)  1L
        checkEval "vroievr092f" (<@ LanguagePrimitives.GenericOne<nativeint> @>)  1n
        checkEval "vroievr092g" (<@ LanguagePrimitives.GenericOne<byte> @>)  1uy
        checkEval "vroievr092h" (<@ LanguagePrimitives.GenericOne<uint16> @>)  1us
        checkEval "vroievr092i" (<@ LanguagePrimitives.GenericOne<uint32> @>)  1u
        checkEval "vroievr092j" (<@ LanguagePrimitives.GenericOne<uint64> @>)  1UL
        checkEval "vroievr092k" (<@ LanguagePrimitives.GenericOne<unativeint> @>)  1un
        checkEval "vroievr092l" (<@ LanguagePrimitives.GenericOne<float> @>)  1.0
        checkEval "vroievr092m" (<@ LanguagePrimitives.GenericOne<float32> @>)  1.0f
        checkEval "vroievr092n" (<@ LanguagePrimitives.GenericOne<decimal> @>)  1M

        check "vroievr0971" (LanguagePrimitives.AdditionDynamic 3y 4y) 7y
        check "vroievr0972" (LanguagePrimitives.AdditionDynamic 3s 4s) 7s
        check "vroievr0973" (LanguagePrimitives.AdditionDynamic 3 4) 7
        check "vroievr0974" (LanguagePrimitives.AdditionDynamic 3L 4L) 7L
        check "vroievr0975" (LanguagePrimitives.AdditionDynamic 3n 4n) 7n
        check "vroievr0976" (LanguagePrimitives.AdditionDynamic 3uy 4uy) 7uy
        check "vroievr0977" (LanguagePrimitives.AdditionDynamic 3us 4us) 7us
        check "vroievr0978" (LanguagePrimitives.AdditionDynamic 3u 4u) 7u
        check "vroievr0979" (LanguagePrimitives.AdditionDynamic 3UL 4UL) 7UL
        check "vroievr0970" (LanguagePrimitives.AdditionDynamic 3un 4un) 7un
        check "vroievr097q" (LanguagePrimitives.AdditionDynamic 3.0 4.0) 7.0
        check "vroievr097w" (LanguagePrimitives.AdditionDynamic 3.0f 4.0f) 7.0f
        check "vroievr097e" (LanguagePrimitives.AdditionDynamic 3.0M 4.0M) 7.0M
        check "vroievr097n" (LanguagePrimitives.AdditionDynamic '3' '\004') '7'

        check "vroievr097r" (LanguagePrimitives.CheckedAdditionDynamic 3y 4y) 7y
        check "vroievr097t" (LanguagePrimitives.CheckedAdditionDynamic 3s 4s) 7s
        check "vroievr097y" (LanguagePrimitives.CheckedAdditionDynamic 3 4) 7
        check "vroievr097u" (LanguagePrimitives.CheckedAdditionDynamic 3L 4L) 7L
        check "vroievr097i" (LanguagePrimitives.CheckedAdditionDynamic 3n 4n) 7n
        check "vroievr097o" (LanguagePrimitives.CheckedAdditionDynamic 3uy 4uy) 7uy
        check "vroievr097p" (LanguagePrimitives.CheckedAdditionDynamic 3us 4us) 7us
        check "vroievr097a" (LanguagePrimitives.CheckedAdditionDynamic 3u 4u) 7u
        check "vroievr097s" (LanguagePrimitives.CheckedAdditionDynamic 3UL 4UL) 7UL
        check "vroievr097d" (LanguagePrimitives.CheckedAdditionDynamic 3un 4un) 7un
        check "vroievr097f" (LanguagePrimitives.CheckedAdditionDynamic 3.0 4.0) 7.0
        check "vroievr097g" (LanguagePrimitives.CheckedAdditionDynamic 3.0f 4.0f) 7.0f
        check "vroievr097h" (LanguagePrimitives.CheckedAdditionDynamic 3.0M 4.0M) 7.0M
        check "vroievr097u" (LanguagePrimitives.CheckedAdditionDynamic '3' '\004') '7'
        
        check "vroievr0981" (LanguagePrimitives.SubtractionDynamic 7y 4y) 3y
        check "vroievr0982" (LanguagePrimitives.SubtractionDynamic 7s 4s) 3s
        check "vroievr0983" (LanguagePrimitives.SubtractionDynamic 7 4) 3
        check "vroievr0984" (LanguagePrimitives.SubtractionDynamic 7L 4L) 3L
        check "vroievr0985" (LanguagePrimitives.SubtractionDynamic 7n 4n) 3n
        check "vroievr0986" (LanguagePrimitives.SubtractionDynamic 7uy 4uy) 3uy
        check "vroievr0987" (LanguagePrimitives.SubtractionDynamic 7us 4us) 3us
        check "vroievr0988" (LanguagePrimitives.SubtractionDynamic 7u 4u) 3u
        check "vroievr0989" (LanguagePrimitives.SubtractionDynamic 7UL 4UL) 3UL
        check "vroievr0980" (LanguagePrimitives.SubtractionDynamic 7un 4un) 3un
        check "vroievr098q" (LanguagePrimitives.SubtractionDynamic 7.0 4.0) 3.0
        check "vroievr098w" (LanguagePrimitives.SubtractionDynamic 7.0f 4.0f) 3.0f
        check "vroievr098e" (LanguagePrimitives.SubtractionDynamic 7.0M 4.0M) 3.0M
        check "vroievr098n" (LanguagePrimitives.SubtractionDynamic '7' '\004') '3'

        check "vroievr098r" (LanguagePrimitives.CheckedSubtractionDynamic 7y 4y) 3y
        check "vroievr098t" (LanguagePrimitives.CheckedSubtractionDynamic 7s 4s) 3s
        check "vroievr098y" (LanguagePrimitives.CheckedSubtractionDynamic 7 4) 3
        check "vroievr098u" (LanguagePrimitives.CheckedSubtractionDynamic 7L 4L) 3L
        check "vroievr098i" (LanguagePrimitives.CheckedSubtractionDynamic 7n 4n) 3n
        check "vroievr098o" (LanguagePrimitives.CheckedSubtractionDynamic 7uy 4uy) 3uy
        check "vroievr098p" (LanguagePrimitives.CheckedSubtractionDynamic 7us 4us) 3us
        check "vroievr098a" (LanguagePrimitives.CheckedSubtractionDynamic 7u 4u) 3u
        check "vroievr098s" (LanguagePrimitives.CheckedSubtractionDynamic 7UL 4UL) 3UL
        check "vroievr098d" (LanguagePrimitives.CheckedSubtractionDynamic 7un 4un) 3un
        check "vroievr098f" (LanguagePrimitives.CheckedSubtractionDynamic 7.0 4.0) 3.0
        check "vroievr098g" (LanguagePrimitives.CheckedSubtractionDynamic 7.0f 4.0f) 3.0f
        check "vroievr098h" (LanguagePrimitives.CheckedSubtractionDynamic 7.0M 4.0M) 3.0M
        check "vroievr098u" (LanguagePrimitives.CheckedSubtractionDynamic '7' '\004') '3'

        check "vroievr0912q" (LanguagePrimitives.MultiplyDynamic 3y 4y) 12y
        check "vroievr0912w" (LanguagePrimitives.MultiplyDynamic 3s 4s) 12s
        check "vroievr0912e" (LanguagePrimitives.MultiplyDynamic 3 4) 12
        check "vroievr0912r" (LanguagePrimitives.MultiplyDynamic 3L 4L) 12L
        check "vroievr0912t" (LanguagePrimitives.MultiplyDynamic 3n 4n) 12n
        check "vroievr0912y" (LanguagePrimitives.MultiplyDynamic 3uy 4uy) 12uy
        check "vroievr0912u" (LanguagePrimitives.MultiplyDynamic 3us 4us) 12us
        check "vroievr0912i" (LanguagePrimitives.MultiplyDynamic 3u 4u) 12u
        check "vroievr0912o" (LanguagePrimitives.MultiplyDynamic 3UL 4UL) 12UL
        check "vroievr0912p" (LanguagePrimitives.MultiplyDynamic 3un 4un) 12un
        check "vroievr0912a" (LanguagePrimitives.MultiplyDynamic 3.0 4.0) 12.0
        check "vroievr0912s" (LanguagePrimitives.MultiplyDynamic 3.0f 4.0f) 12.0f
        check "vroievr0912d" (LanguagePrimitives.MultiplyDynamic 3.0M 4.0M) 12.0M

        check "vroievr0912f" (LanguagePrimitives.CheckedMultiplyDynamic 3y 4y) 12y
        check "vroievr0912g" (LanguagePrimitives.CheckedMultiplyDynamic 3s 4s) 12s
        check "vroievr0912h" (LanguagePrimitives.CheckedMultiplyDynamic 3 4) 12
        check "vroievr0912j" (LanguagePrimitives.CheckedMultiplyDynamic 3L 4L) 12L
        check "vroievr0912k" (LanguagePrimitives.CheckedMultiplyDynamic 3n 4n) 12n
        check "vroievr0912l" (LanguagePrimitives.CheckedMultiplyDynamic 3uy 4uy) 12uy
        check "vroievr0912z" (LanguagePrimitives.CheckedMultiplyDynamic 3us 4us) 12us
        check "vroievr0912x" (LanguagePrimitives.CheckedMultiplyDynamic 3u 4u) 12u
        check "vroievr0912c" (LanguagePrimitives.CheckedMultiplyDynamic 3UL 4UL) 12UL
        check "vroievr0912v" (LanguagePrimitives.CheckedMultiplyDynamic 3un 4un) 12un
        check "vroievr0912b" (LanguagePrimitives.CheckedMultiplyDynamic 3.0 4.0) 12.0
        check "vroievr0912n" (LanguagePrimitives.CheckedMultiplyDynamic 3.0f 4.0f) 12.0f
        check "vroievr0912m" (LanguagePrimitives.CheckedMultiplyDynamic 3.0M 4.0M) 12.0M


        let iarr = [| 0..1000 |]
        let ilist = [ 0..1000 ]

        let farr = [| 0.0 .. 1.0 .. 100.0 |]
        let flist = [ 0.0 .. 1.0 .. 100.0 ]

        Array.average farr

        checkEval "vrewoinrv091" (<@ farr.[0] @>) 0.0
        checkEval "vrewoinrv092" (<@ flist.[0] @>) 0.0
        checkEval "vrewoinrv093" (<@ iarr.[0] @>) 0
        checkEval "vrewoinrv094" (<@ ilist.[0] @>) 0

        checkEval "vrewoinrv095" (<@ farr.[0] <- 0.0 @>) ()
        checkEval "vrewoinrv096" (<@ iarr.[0] <- 0 @>) ()

        checkEval "vrewoinrv097" (<@ farr.[0] <- 1.0 @>) ()
        checkEval "vrewoinrv098" (<@ iarr.[0] <- 1 @>) ()

        checkEval "vrewoinrv099" (<@ farr.[0] @>) 1.0
        checkEval "vrewoinrv09q" (<@ iarr.[0] @>) 1

        checkEval "vrewoinrv09w" (<@ farr.[0] <- 0.0 @>) ()
        checkEval "vrewoinrv09e" (<@ iarr.[0] <- 0 @>) ()


        checkEval "vrewoinrv09r" (<@ Array.average farr @>) (Array.average farr)
        checkEval "vrewoinrv09t" (<@ Array.sum farr @>) (Array.sum farr)
        checkEval "vrewoinrv09y" (<@ Seq.sum farr @>) (Seq.sum farr)
        checkEval "vrewoinrv09u" (<@ Seq.average farr @>) (Seq.average farr) 
        checkEval "vrewoinrv09i" (<@ Seq.average flist @>) (Seq.average flist)
        checkEval "vrewoinrv09o" (<@ Seq.averageBy (fun x -> x) farr @> ) (Seq.averageBy (fun x -> x) farr )
        checkEval "vrewoinrv09p" (<@ Seq.averageBy (fun x -> x) flist @>) (Seq.averageBy (fun x -> x) flist )
        checkEval "vrewoinrv09a" (<@ Seq.averageBy float ilist @>) (Seq.averageBy float ilist)
        checkEval "vrewoinrv09s" (<@ List.sum flist @>) (List.sum flist)
        checkEval "vrewoinrv09d" (<@ List.average flist @>) (List.average flist)
        checkEval "vrewoinrv09f" (<@ List.averageBy float ilist @>) (List.averageBy float ilist)

        checkEval "vrewoinrv09g1" (<@ compare 0 0 = 0 @>) true
        checkEval "vrewoinrv09g2" (<@ compare 0 1 < 0 @>) true
        checkEval "vrewoinrv09g3" (<@ compare 1 0 > 0 @>) true
        checkEval "vrewoinrv09g4" (<@ 0 < 1 @>) true
        checkEval "vrewoinrv09g5" (<@ 0 <= 1 @>) true
        checkEval "vrewoinrv09g6" (<@ 1 <= 1 @>) true
        checkEval "vrewoinrv09g7" (<@ 2 <= 1 @>) false
        checkEval "vrewoinrv09g8" (<@ 0 > 1 @>) false
        checkEval "vrewoinrv09g9" (<@ 0 >= 1 @>) false
        checkEval "vrewoinrv09g0" (<@ 1 >= 1 @>) true
        checkEval "vrewoinrv09gQ" (<@ 2 >= 1 @>) true

        checkEval "vrewoinrv09gw" (<@ compare 0.0 0.0 = 0 @>) true
        checkEval "vrewoinrv09ge" (<@ compare 0.0 1.0 < 0 @>) true
        checkEval "vrewoinrv09gr" (<@ compare 1.0 0.0 > 0 @>) true
        checkEval "vrewoinrv09gt" (<@ 0.0 < 1.0 @>) true
        checkEval "vrewoinrv09gy" (<@ 0.0 <= 1.0 @>) true
        checkEval "vrewoinrv09gu" (<@ 1.0 <= 1.0 @>) true
        checkEval "vrewoinrv09gi" (<@ 2.0 <= 1.0 @>) false
        checkEval "vrewoinrv09go" (<@ 0.0 > 1.0 @>) false
        checkEval "vrewoinrv09gp" (<@ 0.0 >= 1.0 @>) false
        checkEval "vrewoinrv09ga" (<@ 1.0 >= 1.0 @>) true
        checkEval "vrewoinrv09gs" (<@ 2.0 >= 1.0 @>) true

        checkEval "vrewoinrv09gd" (<@ compare 0.0f 0.0f = 0 @>) true
        checkEval "vrewoinrv09gf" (<@ compare 0.0f 1.0f < 0 @>) true
        checkEval "vrewoinrv09gg" (<@ compare 1.0f 0.0f > 0 @>) true
        checkEval "vrewoinrv09gh" (<@ 0.0f < 1.0f @>) true
        checkEval "vrewoinrv09gk" (<@ 0.0f <= 1.0f @>) true
        checkEval "vrewoinrv09gl" (<@ 1.0f <= 1.0f @>) true
        checkEval "vrewoinrv09gz" (<@ 2.0f <= 1.0f @>) false
        checkEval "vrewoinrv09gx" (<@ 0.0f > 1.0f @>) false
        checkEval "vrewoinrv09gc" (<@ 0.0f >= 1.0f @>) false
        checkEval "vrewoinrv09gv" (<@ 1.0f >= 1.0f @>) true
        checkEval "vrewoinrv09gb" (<@ 2.0f >= 1.0f @>) true

        checkEval "vrewoinrv09gn" (<@ compare 0L 0L = 0 @>) true
        checkEval "vrewoinrv09gm" (<@ compare 0L 1L < 0 @>) true
        checkEval "vrewoinrv09g11" (<@ compare 1L 0L > 0 @>) true
        checkEval "vrewoinrv09g12" (<@ 0L < 1L @>) true
        checkEval "vrewoinrv09g13" (<@ 0L <= 1L @>) true
        checkEval "vrewoinrv09g14" (<@ 1L <= 1L @>) true
        checkEval "vrewoinrv09g15" (<@ 2L <= 1L @>) false
        checkEval "vrewoinrv09g16" (<@ 0L > 1L @>) false
        checkEval "vrewoinrv09g17" (<@ 0L >= 1L @>) false
        checkEval "vrewoinrv09g18" (<@ 1L >= 1L @>) true
        checkEval "vrewoinrv09g19" (<@ 2L >= 1L @>) true

        checkEval "vrewoinrv09g21" (<@ compare 0y 0y = 0 @>) true
        checkEval "vrewoinrv09g22" (<@ compare 0y 1y < 0 @>) true
        checkEval "vrewoinrv09g23" (<@ compare 1y 0y > 0 @>) true
        checkEval "vrewoinrv09g24" (<@ 0y < 1y @>) true
        checkEval "vrewoinrv09g25" (<@ 0y <= 1y @>) true
        checkEval "vrewoinrv09g26" (<@ 1y <= 1y @>) true
        checkEval "vrewoinrv09g27" (<@ 2y <= 1y @>) false
        checkEval "vrewoinrv09g28" (<@ 0y > 1y @>) false
        checkEval "vrewoinrv09g29" (<@ 0y >= 1y @>) false
        checkEval "vrewoinrv09g30" (<@ 1y >= 1y @>) true
        checkEval "vrewoinrv09g31" (<@ 2y >= 1y @>) true

        checkEval "vrewoinrv09g32" (<@ compare 0M 0M = 0 @>) true
        checkEval "vrewoinrv09g33" (<@ compare 0M 1M < 0 @>) true
        checkEval "vrewoinrv09g34" (<@ compare 1M 0M > 0 @>) true
        checkEval "vrewoinrv09g35" (<@ 0M < 1M @>) true
        checkEval "vrewoinrv09g36" (<@ 0M <= 1M @>) true
        checkEval "vrewoinrv09g37" (<@ 1M <= 1M @>) true
        checkEval "vrewoinrv09g38" (<@ 2M <= 1M @>) false
        checkEval "vrewoinrv09g39" (<@ 0M > 1M @>) false
        checkEval "vrewoinrv09g40" (<@ 0M >= 1M @>) false
        checkEval "vrewoinrv09g41" (<@ 1M >= 1M @>) true
        checkEval "vrewoinrv09g42" (<@ 2M >= 1M @>) true

        checkEval "vrewoinrv09g43" (<@ compare 0I 0I = 0 @>) true
        checkEval "vrewoinrv09g44" (<@ compare 0I 1I < 0 @>) true
        checkEval "vrewoinrv09g45" (<@ compare 1I 0I > 0 @>) true
        checkEval "vrewoinrv09g46" (<@ 0I < 1I @>) true
        checkEval "vrewoinrv09g47" (<@ 0I <= 1I @>) true
        checkEval "vrewoinrv09g48" (<@ 1I <= 1I @>) true
        checkEval "vrewoinrv09g49" (<@ 2I <= 1I @>) false
        checkEval "vrewoinrv09g50" (<@ 0I > 1I @>) false
        checkEval "vrewoinrv09g51" (<@ 0I >= 1I @>) false
        checkEval "vrewoinrv09g52" (<@ 1I >= 1I @>) true
        checkEval "vrewoinrv09g53" (<@ 2I >= 1I @>) true


        checkEval "vrewoinrv09g" (<@ sin 0.0 @>) (sin 0.0)
        checkEval "vrewoinrv09h" (<@ sinh 0.0 @>) (sinh 0.0)
        checkEval "vrewoinrv09j" (<@ cos 0.0 @>) (cos 0.0)
        checkEval "vrewoinrv09k" (<@ cosh 0.0 @>) (cosh 0.0)
        checkEval "vrewoinrv09l" (<@ tan 1.0 @>) (tan 1.0)
        checkEval "vrewoinrv09z" (<@ tanh 1.0 @>) (tanh 1.0)
        checkEval "vrewoinrv09x" (<@ abs -2.0 @>) (abs -2.0)
        checkEval "vrewoinrv09c" (<@ ceil 2.0 @>) (ceil 2.0)
        checkEval "vrewoinrv09v" (<@ sqrt 2.0 @>) (sqrt 2.0)
        checkEval "vrewoinrv09b" (<@ sign 2.0 @>) (sign 2.0)
        checkEval "vrewoinrv09n" (<@ truncate 2.3 @>) (truncate 2.3)
        checkEval "vrewoinrv09m" (<@ floor 2.3 @>) (floor 2.3)
        checkEval "vrewoinrv09Q" (<@ round 2.3 @>) (round 2.3)
        checkEval "vrewoinrv09W" (<@ log 2.3 @>) (log 2.3)
        checkEval "vrewoinrv09E" (<@ log10 2.3 @>) (log10 2.3)
        checkEval "vrewoinrv09R" (<@ exp 2.3 @>) (exp 2.3)
        checkEval "vrewoinrv09T" (<@ 2.3 ** 2.4 @>) (2.3 ** 2.4)

        checkEval "vrewoinrv09Y" (<@ sin 0.0f @>) (sin 0.0f)
        checkEval "vrewoinrv09U" (<@ sinh 0.0f @>) (sinh 0.0f)
        checkEval "vrewoinrv09I" (<@ cos 0.0f @>) (cos 0.0f)
        checkEval "vrewoinrv09O" (<@ cosh 0.0f @>) (cosh 0.0f)
        checkEval "vrewoinrv09P" (<@ tan 1.0f @>) (tan 1.0f)
        checkEval "vrewoinrv09A" (<@ tanh 1.0f @>) (tanh 1.0f)
        checkEval "vrewoinrv09S" (<@ abs -2.0f @>) (abs -2.0f)
        checkEval "vrewoinrv09D" (<@ ceil 2.0f @>) (ceil 2.0f)
        checkEval "vrewoinrv09F" (<@ sqrt 2.0f @>) (sqrt 2.0f)
        checkEval "vrewoinrv09G" (<@ sign 2.0f @>) (sign 2.0f)
        checkEval "vrewoinrv09H" (<@ truncate 2.3f @>) (truncate 2.3f)
        checkEval "vrewoinrv09J" (<@ floor 2.3f @>) (floor 2.3f)
        checkEval "vrewoinrv09K" (<@ round 2.3f @>) (round 2.3f)
        checkEval "vrewoinrv09L" (<@ log 2.3f @>) (log 2.3f)
        checkEval "vrewoinrv09Z" (<@ log10 2.3f @>) (log10 2.3f)
        checkEval "vrewoinrv09X" (<@ exp 2.3f @>) (exp 2.3f)
        checkEval "vrewoinrv09C" (<@ 2.3f ** 2.4f @>) (2.3f ** 2.4f)

        checkEval "vrewoinrv09V" (<@ ceil 2.0M @>) (ceil 2.0M)
        checkEval "vrewoinrv09B" (<@ sign 2.0M @>) (sign 2.0M)
        checkEval "vrewoinrv09N" (<@ truncate 2.3M @>) (truncate 2.3M)
        checkEval "vrewoinrv09M" (<@ floor 2.3M @>) (floor 2.3M)

        checkEval "vrewoinrv09QQ" (<@ sign -2 @>) (sign -2)
        checkEval "vrewoinrv09WW" (<@ sign -2y @>) (sign -2y)
        checkEval "vrewoinrv09EE" (<@ sign -2s @>) (sign -2s)
        checkEval "vrewoinrv09RR" (<@ sign -2L @>) (sign -2L)

        checkEval "vrewoinrv09TT" (<@ [ 0 .. 10 ] @>) [ 0 .. 10 ]
        checkEval "vrewoinrv09YY" (<@ [ 0y .. 10y ] @>) [ 0y .. 10y ]
        checkEval "vrewoinrv09UU" (<@ [ 0s .. 10s ] @>) [ 0s .. 10s ]
        checkEval "vrewoinrv09II" (<@ [ 0L .. 10L ] @>) [ 0L .. 10L ]
        checkEval "vrewoinrv09OO" (<@ [ 0u .. 10u ] @>) [ 0u .. 10u ]
        checkEval "vrewoinrv09PP" (<@ [ 0uy .. 10uy ] @>) [ 0uy .. 10uy ]
        checkEval "vrewoinrv09AA" (<@ [ 0us .. 10us ] @>) [ 0us .. 10us ]
        checkEval "vrewoinrv09SS" (<@ [ 0UL .. 10UL ] @>) [ 0UL .. 10UL ]
        

        
        // Round dynamic dispatch on Decimal
        checkEval "vrewoinrv09FF" (<@ round 2.3M @>) (round 2.3M)

        // Measure stuff:
        checkEval "vrewoinrv09GG" (<@ atan2 3.0 4.0 @>) (atan2 3.0 4.0 )
        
        [<Measure>]
        type kg
        checkEval "vrewoinrv09HH" (<@ 1.0<kg> @>) (1.0<kg>)

        // Measure stuff:
        checkEval "vrewoinrv09JJ" (<@ 1.0<kg> + 2.0<kg> @>) (3.0<kg>)


        Eval <@ Array.average [| 0.0 .. 1.0 .. 10000.0 |] @> 

    module GenericInlinedOperationsStillDynamicallyAvailableTests = 

        let inline test1 (x: 'T) = 
            checkEval ("vroievr093-(" + typeof<'T>.ToString() + ")") (<@ LanguagePrimitives.GenericZero<_> @>)  x

        test1 0s
        test1 0
        test1 0L
        test1 0n
        test1 0uy
        test1 0us 
        test1 0u
        test1 0UL
        test1 0un 
        test1 0.0
        test1 0.0f
        test1 0M

        let inline test2 (x: 'T) = 
           checkEval ("vroievr095f3-(" + typeof<'T>.ToString() + ")") (<@ LanguagePrimitives.GenericOne<_> @>)  x

        test2 1s
        test2 1
        test2 1L
        test2 1n
        test2 1uy
        test2 1us 
        test2 1u
        test2 1UL
        test2 1un 
        test2 1.0
        test2 1.0f
        test2 1M

        let inline test3 (x: 'T) y = 
           checkEval ("vroievr096cfqw-(" + typeof<'T>.ToString() + ")") (<@ x + y @>)  (x + y)

        test3 3s 4s
        test3 3L 4L

        let inline test4 (x: 'T) =
           checkEval ("vroievrgerwwge41-(" + typeof<'T>.ToString() + ")") (<@ abs x @>)  (abs x)
           checkEval ("vroievrgerwwge42-(" + typeof<'T>.ToString() + ")") (<@ sin x @>)  (sin x)
           checkEval ("vroievrgerwwge43-(" + typeof<'T>.ToString() + ")") (<@ cos x @>)  (cos x)
           checkEval ("vroievrgerwwge44-(" + typeof<'T>.ToString() + ")") (<@ tan x @>)  (tan x)
           checkEval ("vroievrgerwwge45-(" + typeof<'T>.ToString() + ")") (<@ sinh x @>)  (sinh x)
           checkEval ("vroievrgerwwge46-(" + typeof<'T>.ToString() + ")") (<@ cosh x @>)  (cosh x)
           checkEval ("vroievrgerwwge47-(" + typeof<'T>.ToString() + ")") (<@ ceil x @>)  (ceil x)
           checkEval ("vroievrgerwwge49-(" + typeof<'T>.ToString() + ")") (<@ tanh x @>)  (tanh x)
           checkEval ("vroievrgerwwge48-(" + typeof<'T>.ToString() + ")") (<@ floor x @>)  (floor x)
           checkEval ("vroievrgerwwge48-(" + typeof<'T>.ToString() + ")") (<@ truncate x @>)  (truncate x)
           checkEval ("vroievrgerwwge48-(" + typeof<'T>.ToString() + ")") (<@ round x @>)  (round x)
           checkEval ("vroievrgerwwge48-(" + typeof<'T>.ToString() + ")") (<@ log (abs x) @>)  (log (abs x))
           checkEval ("vroievrgerwwge48-(" + typeof<'T>.ToString() + ")") (<@ sqrt (abs x) @>)  (sqrt (abs x))
           checkEval ("vroievrgerwwge48-(" + typeof<'T>.ToString() + ")") (<@ log10 (abs x) @>)  (log10 (abs x))
           checkEval ("vroievrgerwwge48-(" + typeof<'T>.ToString() + ")") (<@ exp (abs x) @>)  (exp (abs x))

        test4 -1.1
        test4 -1.1f

        let inline test5 (x: 'T) y = 
           checkEval ("vrbrer096cfqw-(" + typeof<'T>.ToString() + ")") (<@ (fun x2 y2 -> x2 + y2) x y @>) (x + y)

        test5 3s 4s
        test5 3L 4L

        let iarr = [| 0..1000 |]
        let ilist = [ 0..1000 ]

        let farr = [| 0.0 .. 1.0 .. 100.0 |]
        let flist = [ 0.0 .. 1.0 .. 100.0 ]

        Array.average farr

        checkEval "vrewoinrv091" (<@ farr.[0] @>) 0.0
        checkEval "vrewoinrv092" (<@ flist.[0] @>) 0.0
        checkEval "vrewoinrv093" (<@ iarr.[0] @>) 0
        checkEval "vrewoinrv094" (<@ ilist.[0] @>) 0

        checkEval "vrewoinrv095" (<@ farr.[0] <- 0.0 @>) ()
        checkEval "vrewoinrv096" (<@ iarr.[0] <- 0 @>) ()

        checkEval "vrewoinrv097" (<@ farr.[0] <- 1.0 @>) ()
        checkEval "vrewoinrv098" (<@ iarr.[0] <- 1 @>) ()

        checkEval "vrewoinrv099" (<@ farr.[0] @>) 1.0
        checkEval "vrewoinrv09q" (<@ iarr.[0] @>) 1

        checkEval "vrewoinrv09w" (<@ farr.[0] <- 0.0 @>) ()
        checkEval "vrewoinrv09e" (<@ iarr.[0] <- 0 @>) ()


        checkEval "vrewoinrv09r" (<@ Array.average farr @>) (Array.average farr)
        checkEval "vrewoinrv09t" (<@ Array.sum farr @>) (Array.sum farr)
        checkEval "vrewoinrv09y" (<@ Seq.sum farr @>) (Seq.sum farr)
        checkEval "vrewoinrv09u" (<@ Seq.average farr @>) (Seq.average farr) 
        checkEval "vrewoinrv09i" (<@ Seq.average flist @>) (Seq.average flist)
        checkEval "vrewoinrv09o" (<@ Seq.averageBy (fun x -> x) farr @> ) (Seq.averageBy (fun x -> x) farr )
        checkEval "vrewoinrv09p" (<@ Seq.averageBy (fun x -> x) flist @>) (Seq.averageBy (fun x -> x) flist )
        checkEval "vrewoinrv09a" (<@ Seq.averageBy float ilist @>) (Seq.averageBy float ilist)
        checkEval "vrewoinrv09s" (<@ List.sum flist @>) (List.sum flist)
        checkEval "vrewoinrv09d" (<@ List.average flist @>) (List.average flist)
        checkEval "vrewoinrv09f" (<@ List.averageBy float ilist @>) (List.averageBy float ilist)

        checkEval "vrewoinrv09g1" (<@ compare 0 0 = 0 @>) true
        checkEval "vrewoinrv09g2" (<@ compare 0 1 < 0 @>) true
        checkEval "vrewoinrv09g3" (<@ compare 1 0 > 0 @>) true
        checkEval "vrewoinrv09g4" (<@ 0 < 1 @>) true
        checkEval "vrewoinrv09g5" (<@ 0 <= 1 @>) true
        checkEval "vrewoinrv09g6" (<@ 1 <= 1 @>) true
        checkEval "vrewoinrv09g7" (<@ 2 <= 1 @>) false
        checkEval "vrewoinrv09g8" (<@ 0 > 1 @>) false
        checkEval "vrewoinrv09g9" (<@ 0 >= 1 @>) false
        checkEval "vrewoinrv09g0" (<@ 1 >= 1 @>) true
        checkEval "vrewoinrv09gQ" (<@ 2 >= 1 @>) true

        checkEval "vrewoinrv09gw" (<@ compare 0.0 0.0 = 0 @>) true
        checkEval "vrewoinrv09ge" (<@ compare 0.0 1.0 < 0 @>) true
        checkEval "vrewoinrv09gr" (<@ compare 1.0 0.0 > 0 @>) true
        checkEval "vrewoinrv09gt" (<@ 0.0 < 1.0 @>) true
        checkEval "vrewoinrv09gy" (<@ 0.0 <= 1.0 @>) true
        checkEval "vrewoinrv09gu" (<@ 1.0 <= 1.0 @>) true
        checkEval "vrewoinrv09gi" (<@ 2.0 <= 1.0 @>) false
        checkEval "vrewoinrv09go" (<@ 0.0 > 1.0 @>) false
        checkEval "vrewoinrv09gp" (<@ 0.0 >= 1.0 @>) false
        checkEval "vrewoinrv09ga" (<@ 1.0 >= 1.0 @>) true
        checkEval "vrewoinrv09gs" (<@ 2.0 >= 1.0 @>) true

        checkEval "vrewoinrv09gd" (<@ compare 0.0f 0.0f = 0 @>) true
        checkEval "vrewoinrv09gf" (<@ compare 0.0f 1.0f < 0 @>) true
        checkEval "vrewoinrv09gg" (<@ compare 1.0f 0.0f > 0 @>) true
        checkEval "vrewoinrv09gh" (<@ 0.0f < 1.0f @>) true
        checkEval "vrewoinrv09gk" (<@ 0.0f <= 1.0f @>) true
        checkEval "vrewoinrv09gl" (<@ 1.0f <= 1.0f @>) true
        checkEval "vrewoinrv09gz" (<@ 2.0f <= 1.0f @>) false
        checkEval "vrewoinrv09gx" (<@ 0.0f > 1.0f @>) false
        checkEval "vrewoinrv09gc" (<@ 0.0f >= 1.0f @>) false
        checkEval "vrewoinrv09gv" (<@ 1.0f >= 1.0f @>) true
        checkEval "vrewoinrv09gb" (<@ 2.0f >= 1.0f @>) true

        checkEval "vrewoinrv09gn" (<@ compare 0L 0L = 0 @>) true
        checkEval "vrewoinrv09gm" (<@ compare 0L 1L < 0 @>) true
        checkEval "vrewoinrv09g11" (<@ compare 1L 0L > 0 @>) true
        checkEval "vrewoinrv09g12" (<@ 0L < 1L @>) true
        checkEval "vrewoinrv09g13" (<@ 0L <= 1L @>) true
        checkEval "vrewoinrv09g14" (<@ 1L <= 1L @>) true
        checkEval "vrewoinrv09g15" (<@ 2L <= 1L @>) false
        checkEval "vrewoinrv09g16" (<@ 0L > 1L @>) false
        checkEval "vrewoinrv09g17" (<@ 0L >= 1L @>) false
        checkEval "vrewoinrv09g18" (<@ 1L >= 1L @>) true
        checkEval "vrewoinrv09g19" (<@ 2L >= 1L @>) true

        checkEval "vrewoinrv09g21" (<@ compare 0y 0y = 0 @>) true
        checkEval "vrewoinrv09g22" (<@ compare 0y 1y < 0 @>) true
        checkEval "vrewoinrv09g23" (<@ compare 1y 0y > 0 @>) true
        checkEval "vrewoinrv09g24" (<@ 0y < 1y @>) true
        checkEval "vrewoinrv09g25" (<@ 0y <= 1y @>) true
        checkEval "vrewoinrv09g26" (<@ 1y <= 1y @>) true
        checkEval "vrewoinrv09g27" (<@ 2y <= 1y @>) false
        checkEval "vrewoinrv09g28" (<@ 0y > 1y @>) false
        checkEval "vrewoinrv09g29" (<@ 0y >= 1y @>) false
        checkEval "vrewoinrv09g30" (<@ 1y >= 1y @>) true
        checkEval "vrewoinrv09g31" (<@ 2y >= 1y @>) true

        checkEval "vrewoinrv09g32" (<@ compare 0M 0M = 0 @>) true
        checkEval "vrewoinrv09g33" (<@ compare 0M 1M < 0 @>) true
        checkEval "vrewoinrv09g34" (<@ compare 1M 0M > 0 @>) true
        checkEval "vrewoinrv09g35" (<@ 0M < 1M @>) true
        checkEval "vrewoinrv09g36" (<@ 0M <= 1M @>) true
        checkEval "vrewoinrv09g37" (<@ 1M <= 1M @>) true
        checkEval "vrewoinrv09g38" (<@ 2M <= 1M @>) false
        checkEval "vrewoinrv09g39" (<@ 0M > 1M @>) false
        checkEval "vrewoinrv09g40" (<@ 0M >= 1M @>) false
        checkEval "vrewoinrv09g41" (<@ 1M >= 1M @>) true
        checkEval "vrewoinrv09g42" (<@ 2M >= 1M @>) true

        checkEval "vrewoinrv09g43" (<@ compare 0I 0I = 0 @>) true
        checkEval "vrewoinrv09g44" (<@ compare 0I 1I < 0 @>) true
        checkEval "vrewoinrv09g45" (<@ compare 1I 0I > 0 @>) true
        checkEval "vrewoinrv09g46" (<@ 0I < 1I @>) true
        checkEval "vrewoinrv09g47" (<@ 0I <= 1I @>) true
        checkEval "vrewoinrv09g48" (<@ 1I <= 1I @>) true
        checkEval "vrewoinrv09g49" (<@ 2I <= 1I @>) false
        checkEval "vrewoinrv09g50" (<@ 0I > 1I @>) false
        checkEval "vrewoinrv09g51" (<@ 0I >= 1I @>) false
        checkEval "vrewoinrv09g52" (<@ 1I >= 1I @>) true
        checkEval "vrewoinrv09g53" (<@ 2I >= 1I @>) true


        checkEval "vrewoinrv09g" (<@ sin 0.0 @>) (sin 0.0)
        checkEval "vrewoinrv09h" (<@ sinh 0.0 @>) (sinh 0.0)
        checkEval "vrewoinrv09j" (<@ cos 0.0 @>) (cos 0.0)
        checkEval "vrewoinrv09k" (<@ cosh 0.0 @>) (cosh 0.0)
        checkEval "vrewoinrv09l" (<@ tan 1.0 @>) (tan 1.0)
        checkEval "vrewoinrv09z" (<@ tanh 1.0 @>) (tanh 1.0)
        checkEval "vrewoinrv09x" (<@ abs -2.0 @>) (abs -2.0)
        checkEval "vrewoinrv09c" (<@ ceil 2.0 @>) (ceil 2.0)
        checkEval "vrewoinrv09v" (<@ sqrt 2.0 @>) (sqrt 2.0)
        checkEval "vrewoinrv09b" (<@ sign 2.0 @>) (sign 2.0)
        checkEval "vrewoinrv09n" (<@ truncate 2.3 @>) (truncate 2.3)
        checkEval "vrewoinrv09m" (<@ floor 2.3 @>) (floor 2.3)
        checkEval "vrewoinrv09Q" (<@ round 2.3 @>) (round 2.3)
        checkEval "vrewoinrv09W" (<@ log 2.3 @>) (log 2.3)
        checkEval "vrewoinrv09E" (<@ log10 2.3 @>) (log10 2.3)
        checkEval "vrewoinrv09R" (<@ exp 2.3 @>) (exp 2.3)
        checkEval "vrewoinrv09T" (<@ 2.3 ** 2.4 @>) (2.3 ** 2.4)

        checkEval "vrewoinrv09Y" (<@ sin 0.0f @>) (sin 0.0f)
        checkEval "vrewoinrv09U" (<@ sinh 0.0f @>) (sinh 0.0f)
        checkEval "vrewoinrv09I" (<@ cos 0.0f @>) (cos 0.0f)
        checkEval "vrewoinrv09O" (<@ cosh 0.0f @>) (cosh 0.0f)
        checkEval "vrewoinrv09P" (<@ tan 1.0f @>) (tan 1.0f)
        checkEval "vrewoinrv09A" (<@ tanh 1.0f @>) (tanh 1.0f)
        checkEval "vrewoinrv09S" (<@ abs -2.0f @>) (abs -2.0f)
        checkEval "vrewoinrv09D" (<@ ceil 2.0f @>) (ceil 2.0f)
        checkEval "vrewoinrv09F" (<@ sqrt 2.0f @>) (sqrt 2.0f)
        checkEval "vrewoinrv09G" (<@ sign 2.0f @>) (sign 2.0f)
        checkEval "vrewoinrv09H" (<@ truncate 2.3f @>) (truncate 2.3f)
        checkEval "vrewoinrv09J" (<@ floor 2.3f @>) (floor 2.3f)
        checkEval "vrewoinrv09K" (<@ round 2.3f @>) (round 2.3f)
        checkEval "vrewoinrv09L" (<@ log 2.3f @>) (log 2.3f)
        checkEval "vrewoinrv09Z" (<@ log10 2.3f @>) (log10 2.3f)
        checkEval "vrewoinrv09X" (<@ exp 2.3f @>) (exp 2.3f)
        checkEval "vrewoinrv09C" (<@ 2.3f ** 2.4f @>) (2.3f ** 2.4f)

        checkEval "vrewoinrv09V" (<@ ceil 2.0M @>) (ceil 2.0M)
        checkEval "vrewoinrv09B" (<@ sign 2.0M @>) (sign 2.0M)
        checkEval "vrewoinrv09N" (<@ truncate 2.3M @>) (truncate 2.3M)
        checkEval "vrewoinrv09M" (<@ floor 2.3M @>) (floor 2.3M)

        checkEval "vrewoinrv09QQ" (<@ sign -2 @>) (sign -2)
        checkEval "vrewoinrv09WW" (<@ sign -2y @>) (sign -2y)
        checkEval "vrewoinrv09EE" (<@ sign -2s @>) (sign -2s)
        checkEval "vrewoinrv09RR" (<@ sign -2L @>) (sign -2L)

        checkEval "vrewoinrv09TT" (<@ [ 0 .. 10 ] @>) [ 0 .. 10 ]
        checkEval "vrewoinrv09YY" (<@ [ 0y .. 10y ] @>) [ 0y .. 10y ]
        checkEval "vrewoinrv09UU" (<@ [ 0s .. 10s ] @>) [ 0s .. 10s ]
        checkEval "vrewoinrv09II" (<@ [ 0L .. 10L ] @>) [ 0L .. 10L ]
        checkEval "vrewoinrv09OO" (<@ [ 0u .. 10u ] @>) [ 0u .. 10u ]
        checkEval "vrewoinrv09PP" (<@ [ 0uy .. 10uy ] @>) [ 0uy .. 10uy ]
        checkEval "vrewoinrv09AA" (<@ [ 0us .. 10us ] @>) [ 0us .. 10us ]
        checkEval "vrewoinrv09SS" (<@ [ 0UL .. 10UL ] @>) [ 0UL .. 10UL ]
        

        
        // Round dynamic dispatch on Decimal
        checkEval "vrewoinrv09FF" (<@ round 2.3M @>) (round 2.3M)

        // Measure stuff:
        checkEval "vrewoinrv09GG" (<@ atan2 3.0 4.0 @>) (atan2 3.0 4.0 )
        
        [<Measure>]
        type kg
        checkEval "vrewoinrv09HH" (<@ 1.0<kg> @>) (1.0<kg>)

        // Measure stuff:
        checkEval "vrewoinrv09JJ" (<@ 1.0<kg> + 2.0<kg> @>) (3.0<kg>)


        Eval <@ Array.average [| 0.0 .. 1.0 .. 10000.0 |] @> 

    module LanguagePrimitiveCastingUnitsOfMeasure = 
        [<Measure>]
        type m

        checkEval "castingunits1" (<@ 2.5 |> LanguagePrimitives.FloatWithMeasure<m> |> float @>) 2.5
        checkEval "castingunits2" (<@ 2.5f |> LanguagePrimitives.Float32WithMeasure<m> |> float32 @>) 2.5f
        checkEval "castingunits3" (<@ 2.0m |> LanguagePrimitives.DecimalWithMeasure<m> |> decimal @>) 2.0M
        checkEval "castingunits4" (<@ 2 |> LanguagePrimitives.Int32WithMeasure<m> |> int @>) 2
        checkEval "castingunits5" (<@ 2L |> LanguagePrimitives.Int64WithMeasure<m> |> int64 @>) 2L
        checkEval "castingunits6" (<@ 2s |> LanguagePrimitives.Int16WithMeasure<m> |> int16 @>) 2s
        checkEval "castingunits7" (<@ 2y |> LanguagePrimitives.SByteWithMeasure<m> |> sbyte @>) 2y
        checkEval "castingunits8" (<@ 2ul |> LanguagePrimitives.UInt32WithMeasure<m> |> uint32 @>) 2ul
        checkEval "castingunits9" (<@ 2UL |> LanguagePrimitives.UInt64WithMeasure<m> |> uint64 @>) 2UL
        checkEval "castingunits10" (<@ 2us |> LanguagePrimitives.UInt16WithMeasure<m> |> uint16 @>) 2us
        checkEval "castingunits11" (<@ 2uy |> LanguagePrimitives.ByteWithMeasure<m> |> byte @>) 2uy

        //NOTE quotations currently *DO NOT* support native integers
        //TODO revisit when the test scaffolding is changed/migrated!
        // checkEval "castingunits12" (<@ 2n |> LanguagePrimitives.IntPtrWithMeasure<m> |> nativeint @>) 2n
        // checkEval "castingunits13" (<@ 2un |> unativeint |> LanguagePrimitives.UIntPtrWithMeasure<m> |> unativeint @>) 2un

module QuotationTests =
    open Microsoft.FSharp.Quotations
    open Microsoft.FSharp.Quotations.Patterns
    open Microsoft.FSharp.Quotations.DerivedPatterns
    let (|Seq|_|) = function SpecificCall <@ seq @>(_, [_],[e]) -> Some e | _ -> None
    let (|Append|_|) = function SpecificCall <@ Seq.append @>(_, [_],[e1;e2]) -> Some (e1,e2) | _ -> None
    let (|Delay|_|) = function SpecificCall <@ Seq.delay @>(_, [_],[Lambda(_,e)]) -> Some e | _ -> None
    let (|FinalFor|_|) = function SpecificCall <@ Seq.map @>(_, [_;_],[Lambda(v,e);sq]) -> Some (v,sq,e) | _ -> None
    let (|OuterFor|_|) = function SpecificCall <@ Seq.collect @>(_, [_;_;_],[Lambda(v,e);sq]) -> Some (v,sq,e) | _ -> None
    let (|Yield|_|) = function SpecificCall <@ Seq.singleton @>(_, [_],[e]) -> Some (e) | _ -> None
    let (|While|_|) = function SpecificCall <@ Microsoft.FSharp.Core.CompilerServices.RuntimeHelpers.EnumerateWhile @>(_, [_],[e1;e2]) -> Some (e1,e2) | _ -> None
    let (|TryFinally|_|) = function SpecificCall <@ Microsoft.FSharp.Core.CompilerServices.RuntimeHelpers.EnumerateThenFinally @>(_, [_],[e1;e2]) -> Some (e1,e2) | _ -> None
    let (|Using|_|) = function SpecificCall <@ Microsoft.FSharp.Core.CompilerServices.RuntimeHelpers.EnumerateUsing @>(_, _,[e1;Lambda(v1,e2)]) -> Some (v1,e1,e2) | _ -> None
    let (|Empty|_|) = function SpecificCall <@ Seq.empty @>(_,_,_) -> Some () | _ -> None
    test "vrenjkr90kj1" 
       (match <@ seq { for x in [1] -> x } @> with 
        | Seq(Delay(FinalFor(v,Coerce(sq,_),res))) when sq = <@@ [1] @@> && res = Expr.Var(v) -> true
        | Seq(Delay(FinalFor(v,sq,res))) -> printfn "v = %A, res = %A, sq = %A" v res sq; false
        | Seq(Delay(sq)) -> printfn "Seq(Delay(_)), tm = %A" sq; false
        | Seq(sq) -> printfn "Seq(_), tm = %A" sq; false 
        | sq -> printfn "tm = %A" sq; false) 

    test "vrenjkr90kj2" 
       (match <@ seq { for x in [1] do yield x } @> with 
        | Seq(Delay(FinalFor(v,Coerce(sq,_),res))) when sq = <@@ [1] @@> && res = Expr.Var(v) -> true
        | sq -> printfn "tm = %A" sq; false) 

    test "vrenjkr90kj3" 
       (match <@ seq { for x in [1] do for y in [2] do yield x } @> with 
        | Seq(Delay(OuterFor(v1,Coerce(sq1,_),FinalFor(v2,Coerce(sq2,_),res)))) when sq1 = <@@ [1] @@> && sq2 = <@@ [2] @@> && res = Expr.Var(v1) -> true
        | sq -> printfn "tm = %A" sq; false) 

    test "vrenjkr90kj4" 
       (match <@ seq { if true then yield 1 else yield 2 } @> with 
        | Seq(Delay(IfThenElse(_,Yield(Int32(1)),Yield(Int32(2))))) -> true
        | sq -> printfn "tm = %A" sq; false) 

    test "vrenjkr90kj5" 
       (match <@ seq { for x in [1] do if true then yield x else yield 2 } @> with 
        | Seq(Delay(OuterFor(vx,Coerce(sq,_),IfThenElse(_,Yield(res),Yield(Int32(2)))))) when sq = <@@ [1] @@>  && res = Expr.Var(vx) -> true
        | sq -> printfn "tm = %A" sq; false) 

    test "vrenjkr90kj6" 
       (match <@ seq { yield 1; yield 2 } @> with 
        | Seq(Delay(Append(Yield(Int32(1)),Delay(Yield(Int32(2)))))) -> true
        | sq -> printfn "tm = %A" sq; false) 

    test "vrenjkr90kj7" 
       (match <@ seq { while true do yield 1 } @> with 
        | Seq(Delay(While(Lambda(_,Bool(true)),Delay(Yield(Int32(1)))))) -> true
        | sq -> printfn "tm = %A" sq; false) 

    test "vrenjkr90kj8" 
       (match <@ seq { while true do yield 1 } @> with 
        | Seq(Delay(While(Lambda(_,Bool(true)),Delay(Yield(Int32(1)))))) -> true
        | sq -> printfn "tm = %A" sq; false) 

    test "vrenjkr90kj9" 
       (match <@ seq { try yield 1 finally () } @> with 
        | Seq(Delay(TryFinally(Delay(Yield(Int32(1))), Lambda(_,Unit)))) -> true
        | sq -> printfn "tm = %A" sq; false) 

    test "vrenjkr90kj9" 
       (match <@ seq { use ie = failwith "" in yield! Seq.empty } @> with 
        | Seq(Delay(Using(v1,e1,Empty))) when v1.Name = "ie" -> true
        | sq -> printfn "tm = %A" sq; false) 

    test "vrenjkr90kjA" 
       (match <@ (3 :> obj) @> with 
        | Coerce(Int32(3),ty) when ty = typeof<obj> -> true
        | sq -> printfn "tm = %A" sq; false) 

    test "vrenjkr90kjB" 
       (match <@ ("3" :> obj) @> with 
        | Coerce(String("3"),ty) when ty = typeof<obj> -> true
        | sq -> printfn "tm = %A" sq; false) 

    test "vrenjkr90kjC" 
       (match <@ ("3" :> System.IComparable) @> with 
        | Coerce(String("3"),ty) when ty = typeof<System.IComparable> -> true
        | sq -> printfn "tm = %A" sq; false) 

(*
    test "vrenjkr90kjD" 
       (match <@ (new obj() :?> System.IComparable) @> with 
        | Coerce(NewObject(_),ty) when ty = typeof<System.IComparable> -> true
        | sq -> printfn "tm = %A" sq; false) 

    test "vrenjkr90kjE" 
       (match <@ (new obj() :?> obj) @> with 
        | NewObject(_) -> true
        | sq -> printfn "tm = %A" sq; false) 
*)


module LargerAutomaticDiferentiationTest_FSharp_1_0_Bug_3498 = 

    let q = 
        <@ (fun (x1:double) -> 
               let fwd6 = 
                   let y3 = x1 * x1
                   (y3, (fun yb4 -> yb4 * 2.0 * x1))
               let rev5 = snd fwd6
               let w0 = fst fwd6

               let fwd14 = 
                   let y11 = w0 + 1.0
                   (y11, (fun yb12 -> yb12 * 1.0))
               let rev13 = snd fwd14
               let y8 = fst fwd14
               (y8, (fun y8b10 -> 
                          let w0b2 = 0.0 
                          let x1b1 = 0.0 
                          let dxs15 = rev13 y8b10 
                          let w0b2 = w0b2 + dxs15 
                          let dxs7 = rev5 w0b2 
                          let x1b1 = x1b1 + dxs7 
                          x1b1))) @>

    let r,rd = (q.Eval()) 4.0
    test "vrknlwerwe90" (r = 17.0)
    test "cew90jkml0rv" (rd 0.1 = 0.8)

module FunkyMethodRepresentations = 
    let Eval (q: Expr<_>) = 
        q.ToLinqExpression() |> ignore 
        q.Compile() |> ignore  
        q.Eval()
    // The IsSome and IsNone properties are represented as static methods because
    // option uses 'null' as a representation
    checkEval "clkedw0" (<@ let x : int option = None in x.IsSome @>) false
    checkEval "clkedw1" (<@ let x : int option = None in x.IsNone @>) true
    checkEval "clkedw2" (<@ let x : int option = Some 1 in x.Value @>) 1
    //checkEval "clkedw3" (<@ let x : int option = Some 1 in x.ToString() @> |> Eval  ) "Some(1)"

module Extensions = 
    let Eval (q: Expr<_>) = 
        q.ToLinqExpression() |> ignore 
        q.Compile() |> ignore  
        q.Eval()
    type System.Object with 
        member x.ExtensionMethod0()  = 3
        member x.ExtensionMethod1()  = ()
        member x.ExtensionMethod2(y:int)  = y
        member x.ExtensionMethod3(y:int)  = ()
        member x.ExtensionMethod4(y:int,z:int)  = y + z
        member x.ExtensionMethod5(y:(int*int))  = y 
        member x.ExtensionProperty1 = 3
        member x.ExtensionProperty2 with get() = 3
        member x.ExtensionProperty3 with set(v:int) = ()
        member x.ExtensionIndexer1 with get(idx:int) = idx
        member x.ExtensionIndexer2 with set(idx:int) (v:int) = ()

    type System.Int32 with 
        member x.Int32ExtensionMethod0()  = 3
        member x.Int32ExtensionMethod1()  = ()
        member x.Int32ExtensionMethod2(y:int)  = y
        member x.Int32ExtensionMethod3(y:int)  = ()
        member x.Int32ExtensionMethod4(y:int,z:int)  = y + z
        member x.Int32ExtensionMethod5(y:(int*int))  = y 
        member x.Int32ExtensionProperty1 = 3
        member x.Int32ExtensionProperty2 with get() = 3
        member x.Int32ExtensionProperty3 with set(v:int) = ()
        member x.Int32ExtensionIndexer1 with get(idx:int) = idx
        member x.Int32ExtensionIndexer2 with set(idx:int) (v:int) = ()

    let v = new obj()
    checkEval "ecnowe0" (<@ v.ExtensionMethod0() @>)  3
    checkEval "ecnowe1" (<@ v.ExtensionMethod1() @>)  ()
    checkEval "ecnowe2" (<@ v.ExtensionMethod2(3) @>) 3
    checkEval "ecnowe3" (<@ v.ExtensionMethod3(3) @>)  ()
    checkEval "ecnowe4" (<@ v.ExtensionMethod4(3,4) @>)  7
    checkEval "ecnowe5" (<@ v.ExtensionMethod5(3,4) @>)  (3,4)
    checkEval "ecnowe6" (<@ v.ExtensionProperty1 @>) 3
    checkEval "ecnowe7" (<@ v.ExtensionProperty2 @>) 3
    checkEval "ecnowe8" (<@ v.ExtensionProperty3 <- 4 @>)  ()
    checkEval "ecnowe9" (<@ v.ExtensionIndexer1(3) @>) 3
    checkEval "ecnowea" (<@ v.ExtensionIndexer2(3) <- 4 @>)  ()

    check "ecnoweb" (Eval (<@ v.ExtensionMethod0 @>) ()) 3 
    check "ecnowec" (Eval (<@ v.ExtensionMethod1 @>) ()) ()
    check "ecnowed" (Eval (<@ v.ExtensionMethod2 @>) 3) 3
    check "ecnowee" (Eval (<@ v.ExtensionMethod3 @>) 3) ()
    check "ecnowef" (Eval (<@ v.ExtensionMethod4 @>) (3,4)) 7
    check "ecnoweg" (Eval (<@ v.ExtensionMethod5 @>) (3,4)) (3,4)

    let v2 = 3
    let mutable v2b = 3
    checkEval "ecnweh0" (<@ v2.ExtensionMethod0() @>) 3
    checkEval "ecnweh1" (<@ v2.ExtensionMethod1() @>) ()
    checkEval "ecnweh2" (<@ v2.ExtensionMethod2(3) @>) 3
    checkEval "ecnweh3" (<@ v2.ExtensionMethod3(3) @>) ()
    checkEval "ecnweh4" (<@ v2.ExtensionMethod4(3,4) @>) 7
    checkEval "ecnweh5" (<@ v2.ExtensionMethod5(3,4) @>) (3,4)
    checkEval "ecnweh6" (<@ v2.ExtensionProperty1 @>) 3
    checkEval "ecnweh7" (<@ v2.ExtensionProperty2 @>) 3
    checkEval "ecnweh8" (<@ v2b.ExtensionProperty3 <- 4 @>)  ()
    checkEval "ecnweh9" (<@ v2.ExtensionIndexer1(3) @>) 3
    checkEval "ecnweha" (<@ v2b.ExtensionIndexer2(3) <- 4 @>)  ()

module QuotationCompilation =
    let Eval (q: Expr<_>) = 
        q.ToLinqExpression() |> ignore 
        q.Compile() |> ignore  
        q.Eval()
        
    // This tried to use non-existent 'Action' delegate with 5 type arguments
    let q =
        <@  (fun () -> 
                let a = ref 0
                let b = 0
                let c = 0
                let d = 0
                let e = 0
                a := b + c + d + e ) @>
    check "qceva0" ((Eval q) ()) ()




    
module Query =

    open QuotationEvaluation

    let debug = false
        
    let contains key source = 
        System.Linq.Enumerable.Contains(source,key)

    let minBy<'T1,'T2> keySelector source = 
        System.Linq.Enumerable.Min(source,Func<'T1,'T2>(keySelector))

    let maxBy<'T1,'T2> keySelector source = 
        System.Linq.Enumerable.Max(source,Func<'T1,'T2>(keySelector))

    let groupBy keySelector source = 
        System.Linq.Enumerable.GroupBy(source,Func<_,_>(keySelector))

    let join  outerSource innerSource outerKeySelector innerKeySelector resultSelector = 
        System.Linq.Enumerable.Join(outerSource,innerSource,Func<_,_>(outerKeySelector),Func<_,_>(innerKeySelector),Func<_,_,_>(resultSelector))

    let groupJoin  outerSource innerSource outerKeySelector innerKeySelector resultSelector = 
        System.Linq.Enumerable.GroupJoin(outerSource,innerSource,Func<_,_>(outerKeySelector),Func<_,_>(innerKeySelector),Func<_,_,_>(resultSelector))

    let ConvVar (v: Var) = 
        Expression.Parameter(v.Type, v.Name)

    let asExpr x = (x :> Expression)
            
    let (|Getter|_|) (prop: #PropertyInfo) =
        match prop.GetGetMethod(true) with 
        | null -> None
        | v -> Some v

    let (|CallPipe|_|) = (|SpecificCall|_|) <@ (|>) @>
    // Match 'f x' or 'x |> f' or 'x |> (fun x -> f (x :> ty))'
    let (|SpecificPipedCall0|_|) q = 
       let (|CallQ|_|) = (|SpecificCall|_|) q
       function
       | CallPipe (None, [_;_],[arg1;Lambda(arg1v,CallQ (None, tyargs,[arg1E])) ]) -> 
           let arg1 = arg1E.Substitute (Map.ofSeq [ (arg1v,arg1) ]).TryFind 
           Some(tyargs,arg1)

       | CallQ (None, tyargs,[arg1]) -> 
           Some(tyargs,arg1)

       | _ -> None           

    // Match 
    //     'f x y' or 
    //     'y |> f x' or
    //     'y |> (fun y -> f (x :> ty) (y :> ty))'
    //     'y |> let x = e in (fun y -> f (x :> ty) (y :> ty))'
    let (|SpecificPipedCall1|_|) q = 
       let (|CallQ|_|) = (|SpecificCall|_|) q
       function
       // Encoded form of some uses of 'T1rg2 |> f arg1'
       | CallPipe (None, [_;_],[arg2;Let(arg1v,arg1,Lambda(arg2v,CallQ (None, tyargs,[arg1E;arg2E]))) ]) -> 
              let arg1 = arg1E.Substitute (Map.ofSeq [ (arg1v,arg1) ]).TryFind 
              let arg2 = arg2E.Substitute (Map.ofSeq [ (arg2v,arg2) ]).TryFind 
              Some(tyargs,arg1,arg2)

       | CallPipe (None, [_;_],[arg2;Lambda(arg2v,CallQ (None, tyargs,[arg1;arg2E])) ]) -> 
              let arg2 = arg2E.Substitute (Map.ofSeq [ (arg2v,arg2) ]).TryFind 
              Some(tyargs,arg1,arg2)

       | CallQ (None, tyargs,[arg1;arg2]) -> 
              Some(tyargs,arg1,arg2)
       | _ -> None           

        
    let GetGenericMethodDefinition (m:MethodInfo) = 
        if m.IsGenericMethod then m.GetGenericMethodDefinition() else m

    let FindGenericStaticMethodInfo mexpr =
        match mexpr with 
        | Lambdas(_,Call(None,methInfo,_)) -> GetGenericMethodDefinition methInfo
        | _ -> failwithf "FindGenericStaticMethodInfo: %A is not a static method call lambda" mexpr

    let CallGenericStaticMethod mexpr  =
        let m = FindGenericStaticMethodInfo mexpr in
        //printf "m = %A\n" m;
        fun (tyargs,args) -> 
            //printf "invoking %A\n" m;
            
            let m = 
                if m.IsGenericMethod then 
                    m.MakeGenericMethod(Array.ofList tyargs) 
                else
                    m
            m.Invoke(null,Array.ofList args)

    let FT1 = typedefof<System.Func<_,_>>
    let FT2 = typedefof<System.Func<_,_,_>>
    let boolTy = typeof<bool>
    let MakeQueryFuncTy (dty,rty) = FT1.MakeGenericType([| dty; rty |])
    let MakeQueryFunc2Ty (dty1,dty2,rty) = FT2.MakeGenericType([| dty1; dty2; rty |])

    let IEnumerableTypeDef = typedefof<IEnumerable<_>>
    let IQueryableTypeDef = typedefof<IQueryable<_>>
    let MakeIEnumerableTy dty= IEnumerableTypeDef.MakeGenericType([| dty|])
    let MakeIQueryableTy dty= IQueryableTypeDef.MakeGenericType([| dty|])

    let isNamedType(typ:Type) = not (typ.IsArray || typ.IsByRef || typ.IsPointer)
    let equivHeadTypes (ty1:Type) (ty2:Type) = 
        isNamedType(ty1) &&
        if ty1.IsGenericType then 
          ty2.IsGenericType && (ty1.GetGenericTypeDefinition()).Equals(ty2.GetGenericTypeDefinition())
        else 
          ty1.Equals(ty2)

    let IsIQueryableTy ty = equivHeadTypes IQueryableTypeDef ty
   

    let CallSeqToList = 
        let F = CallGenericStaticMethod <@ Seq.toList @> 
        fun (srcTy,src) -> 
            F ([srcTy],[src])

    let CallSeqToArray = 
        let F = CallGenericStaticMethod <@ Seq.toArray @> 
        fun (srcTy,src) -> 
            F ([srcTy],[src])

    let CallQueryableContains = 
        let F = CallGenericStaticMethod <@ System.Linq.Queryable.Contains : _ * _ -> _ @> 
        fun (srcTy,src,key:Expression) -> 
            F ([srcTy],[src;box key])

    let CallQueryableMin = 
        let F = CallGenericStaticMethod <@ System.Linq.Queryable.Min : _ -> _ @> 
        fun (srcTy,src) -> 
            F ([srcTy],[src])

    let CallQueryableMax = 
        let F = CallGenericStaticMethod <@ System.Linq.Queryable.Max : _ -> _ @> 
        fun (srcTy,src) -> 
            F ([srcTy],[src])

    let CallQueryableAverageBy = 
        let F_float = CallGenericStaticMethod <@ System.Linq.Queryable.Average : _ * Expression<Func<_,float>> -> _ @> 
        let F_float32 = CallGenericStaticMethod <@ System.Linq.Queryable.Average : _ * Expression<Func<_,float32>> -> _ @> 
        let F_decimal = CallGenericStaticMethod <@ System.Linq.Queryable.Average : _ * Expression<Func<_,decimal>> -> _ @> 
        // Note these don't satisfy the F# constraints anyway
        let F_int32 = CallGenericStaticMethod <@ System.Linq.Queryable.Average : _ * Expression<Func<_,int32>> -> _ @> 
        let F_int64 = CallGenericStaticMethod <@ System.Linq.Queryable.Average : _ * Expression<Func<_,int64>> -> _ @> 
        fun (srcTy,destTy,src,predicate:Expression) -> 
            match srcTy with 
            | ty when ty = typeof<float> -> F_float ([destTy],[src;box predicate])
            | ty when ty = typeof<float32> -> F_float32 ([destTy],[src;box predicate])
            | ty when ty = typeof<decimal> -> F_decimal ([destTy],[src;box predicate])
            | ty when ty = typeof<int32> -> F_int32 ([destTy],[src;box predicate])
            | ty when ty = typeof<int64> -> F_int64 ([destTy],[src;box predicate])
            | _ -> failwith "unrecognized use of 'Seq.averageBy'"


    let CallQueryableAverage = 
        let F_float = CallGenericStaticMethod <@ System.Linq.Queryable.Average : IQueryable<float> -> _ @> 
        let F_float32 = CallGenericStaticMethod <@ System.Linq.Queryable.Average : IQueryable<float32>  -> _ @> 
        let F_decimal = CallGenericStaticMethod <@ System.Linq.Queryable.Average : IQueryable<decimal>  -> _ @> 
        // Note these don't satisfy the F# constraints anyway
        let F_int32 = CallGenericStaticMethod <@ System.Linq.Queryable.Average : IQueryable<int32>  -> _ @> 
        let F_int64 = CallGenericStaticMethod <@ System.Linq.Queryable.Average : IQueryable<int64>  -> _ @> 
        fun (srcTy,src) -> 
            match srcTy with 
            | ty when ty = typeof<float> -> F_float ([],[src])
            | ty when ty = typeof<float32> -> F_float32 ([],[src])
            | ty when ty = typeof<decimal> -> F_decimal ([],[src])
            | ty when ty = typeof<int32> -> F_int32 ([],[src])
            | ty when ty = typeof<int64> -> F_int64 ([],[src])
            | _ -> failwith "unrecognized use of 'Seq.average'"

    let CallQueryableSumBy = 
        let F_float = CallGenericStaticMethod <@ System.Linq.Queryable.Sum : _ * Expression<Func<_,float>> -> _ @> 
        let F_float32 = CallGenericStaticMethod <@ System.Linq.Queryable.Sum : _ * Expression<Func<_,float32>> -> _ @> 
        let F_decimal = CallGenericStaticMethod <@ System.Linq.Queryable.Sum : _ * Expression<Func<_,decimal>> -> _ @> 
        let F_int32 = CallGenericStaticMethod <@ System.Linq.Queryable.Sum : _ * Expression<Func<_,int32>> -> _ @> 
        let F_int64 = CallGenericStaticMethod <@ System.Linq.Queryable.Sum : _ * Expression<Func<_,int64>> -> _ @> 
        fun (srcTy,destTy,src,predicate:Expression) -> 
            match srcTy with 
            | ty when ty = typeof<float> -> F_float ([destTy],[src;box predicate])
            | ty when ty = typeof<float32> -> F_float32 ([destTy],[src;box predicate])
            | ty when ty = typeof<decimal> -> F_decimal ([destTy],[src;box predicate])
            | ty when ty = typeof<int32> -> F_int32 ([destTy],[src;box predicate])
            | ty when ty = typeof<int64> -> F_int64 ([destTy],[src;box predicate])
            | _ -> failwith "unrecognized use of 'Seq.sumBy'"

    let CallQueryableSum = 
        let F_float = CallGenericStaticMethod <@ System.Linq.Queryable.Sum : IQueryable<float> -> _ @> 
        let F_float32 = CallGenericStaticMethod <@ System.Linq.Queryable.Sum : IQueryable<float32>  -> _ @> 
        let F_decimal = CallGenericStaticMethod <@ System.Linq.Queryable.Sum : IQueryable<decimal>  -> _ @> 
        // Note these don't satisfy the F# constraints anyway
        let F_int32 = CallGenericStaticMethod <@ System.Linq.Queryable.Sum : IQueryable<int32>  -> _ @> 
        let F_int64 = CallGenericStaticMethod <@ System.Linq.Queryable.Sum : IQueryable<int64>  -> _ @> 
        fun (srcTy,src) -> 
            match srcTy with 
            | ty when ty = typeof<float> -> F_float ([],[src])
            | ty when ty = typeof<float32> -> F_float32 ([],[src])
            | ty when ty = typeof<decimal> -> F_decimal ([],[src])
            | ty when ty = typeof<int32> -> F_int32 ([],[src])
            | ty when ty = typeof<int64> -> F_int64 ([],[src])
            | _ -> failwith "unrecognized use of 'Seq.sum"

    let CallQueryableFirst = 
        let F = CallGenericStaticMethod <@ System.Linq.Queryable.First : _ -> _ @> 
        fun (srcTy,src) -> 
            F ([srcTy],[src])

    let CallQueryableFirstFind = 
        let F = CallGenericStaticMethod <@ System.Linq.Queryable.First : _ * Expression<Func<_,_>> -> _ @> 
        fun (srcTy,src,predicate:Expression) -> 
            F ([srcTy],[src;box predicate])

    let CallQueryableCount = 
        let F = CallGenericStaticMethod <@ System.Linq.Queryable.Count : _ -> _ @> 
        fun (srcTy:Type,src:obj) -> 
            F ([srcTy],[src])

    let BindGenericMethod (methInfo:MethodInfo) tyargs = 
        if methInfo.IsGenericMethod then 
            methInfo.GetGenericMethodDefinition().MakeGenericMethod(Array.ofList tyargs)
        else
            methInfo

    let minfo = match <@@ QuotationEvaluation.LinqExpressionHelper @@> with Lambda(_,Call(_,minfo,_)) -> minfo | _ -> failwith "couldn't find method info"
    let MakeFakeExpression (x:Expr) = 
        Expr.Call(minfo.GetGenericMethodDefinition().MakeGenericMethod [| x.Type |], [ x ])

            
    let MakeGenericStaticMethod lam  =
        // Nb. the SelectMany/Where/Select methods theoretically expects an expression, but the
        // LINQ team decided to only pass it a delegate construction. The coercion from
        // the delegate construction to the expression is effectively implicit in LINQ, but
        // not in F# quotations, so we have to use 'Unchecked' version (see also FSBUGS #970)
        let methInfo = FindGenericStaticMethodInfo lam 
        (fun (tyargs,args) -> Expr.Call(BindGenericMethod methInfo tyargs,args))
           

    let MakeQueryableSelect = 
        let F = MakeGenericStaticMethod <@ (System.Linq.Queryable.Select : _ * Expression<Func<_,_>> -> _) @>
        fun (srcTy,targetTy,src,v,f)  -> 
            
            let selector = Expr.NewDelegate(MakeQueryFuncTy(srcTy,targetTy), v,f)

            let selector = MakeFakeExpression selector
            F ([srcTy;targetTy],[src;selector])

    let MakeQueryableAppend = 
        let F = MakeGenericStaticMethod <@ (System.Linq.Queryable.Concat ) @>
        fun (srcTy,src1,src2)  -> 
            F ([srcTy],[src1;src2])


    let MakeQueryableAsQueryable = 
        let F = MakeGenericStaticMethod <@ (System.Linq.Queryable.AsQueryable : seq<_>  -> IQueryable<_>) @>
        fun (ty,src)  -> 
            F ([ty],[src])

    let MakeEnumerableEmpty = 
        let F = MakeGenericStaticMethod <@ (System.Linq.Enumerable.Empty : unit -> seq<_>) @>
        fun (ty)  -> 
            F ([ty],[])

    let MakeQueryableEmpty = 
        fun (ty)  -> 
            MakeQueryableAsQueryable (ty,MakeEnumerableEmpty ty)

    let MakeQueryableSelectMany = 
        let F = MakeGenericStaticMethod <@ (System.Linq.Queryable.SelectMany : IQueryable<_> * Expression<Func<_,_>> -> IQueryable<_>) @>
        fun (srcTy,targetTy,src,v,f)  -> 
            // REVIEW: Previous notes in this file said that LINQ likes to see a coercion to an interface type
            // at this point. 
            //let src = Expr.Coerce(src,MakeIQueryableTy srcTy)
            let selector = Expr.NewDelegate(MakeQueryFuncTy(srcTy,MakeIEnumerableTy targetTy), [v],f)

            let selector = MakeFakeExpression selector
            F ([srcTy;targetTy],[src;selector])

    let MakeQueryableWhere = 
        let F = MakeGenericStaticMethod <@ (System.Linq.Queryable.Where : _ * Expression<Func<_,_>> -> _) @>
        fun (srcTy,src,v,f)  -> 
            let selector = Expr.NewDelegate(MakeQueryFuncTy(srcTy,typeof<bool>), [v],f)

            let selector = MakeFakeExpression selector
            F ([srcTy],[src;selector])



    let MakeQueryableOrderBy = 
        let F = MakeGenericStaticMethod <@ System.Linq.Queryable.OrderBy : _ * Expression<Func<_,_>> -> _ @> 
        fun (srcTy,keyTy,src,v,keySelector)  -> 
            
            let selector = Expr.NewDelegate(MakeQueryFuncTy(srcTy,keyTy), [v],keySelector)

            let selector = MakeFakeExpression selector
            F ([srcTy;keyTy],[src;selector])

    let MakeQueryableDistinct = 
        let F = MakeGenericStaticMethod <@ System.Linq.Queryable.Distinct : _ -> _ @> 
        fun (srcTy,src)  -> 
            let src = Expr.Coerce(src,MakeIQueryableTy srcTy)
            F ([srcTy],[src])

    let MakeQueryableTake = 
        let F = MakeGenericStaticMethod <@ System.Linq.Queryable.Take @> 
        fun (srcTy,src,count)  -> 
            let src = Expr.Coerce(src,MakeIQueryableTy srcTy)
            F ([srcTy],[src;count])

    let MakeQueryableGroupBy = 
        let F = MakeGenericStaticMethod <@ System.Linq.Queryable.GroupBy : _ * Expression<Func<_,_>> -> _ @> 
        fun (srcTy,keyTy,src,v,keySelector)  -> 
            let keySelector = Expr.NewDelegate(MakeQueryFuncTy(srcTy,keyTy), [v],keySelector) 

            let keySelector = MakeFakeExpression keySelector
            F ([srcTy;keyTy],[src;keySelector])

    let MakeQueryableMinBy = 
        let F = MakeGenericStaticMethod <@ System.Linq.Queryable.Min : _ * Expression<Func<_,_>> -> _ @> 
        fun (srcTy,keyTy,src,v,keySelector)  -> 
            let keySelector = Expr.NewDelegate(MakeQueryFuncTy(srcTy,keyTy), [v],keySelector) 

            let keySelector = MakeFakeExpression keySelector
            F ([srcTy;keyTy],[src;keySelector])

    let MakeQueryableMaxBy = 
        let F = MakeGenericStaticMethod <@ System.Linq.Queryable.Max : _ * Expression<Func<_,_>> -> _ @> 
        fun (srcTy,keyTy,src,v,keySelector)  -> 
            let keySelector = Expr.NewDelegate(MakeQueryFuncTy(srcTy,keyTy), [v],keySelector) 

            let keySelector = MakeFakeExpression keySelector
            F ([srcTy;keyTy],[src;keySelector])

    let MakeQueryableAny = 
        let F = MakeGenericStaticMethod <@ System.Linq.Queryable.Any : _ * Expression<Func<_,_>> -> _ @> 
        fun (srcTy,src,v,keySelector)  -> 
            let keySelector = Expr.NewDelegate(MakeQueryFuncTy(srcTy,boolTy), [v],keySelector) 

            let keySelector = MakeFakeExpression keySelector
            F ([srcTy],[src;keySelector])

    let MakeQueryableAll = 
        let F = MakeGenericStaticMethod <@ System.Linq.Queryable.All : _ * Expression<Func<_,_>> -> _ @> 
        fun (srcTy,src,v,keySelector)  -> 
            let keySelector = Expr.NewDelegate(MakeQueryFuncTy(srcTy,boolTy), [v],keySelector) 

            let keySelector = MakeFakeExpression keySelector
            F ([srcTy],[src;keySelector])

    let MakeQueryableJoin = 
        let F = MakeGenericStaticMethod <@ System.Linq.Queryable.Join : _ * _ * Expression<Func<_,_>> * Expression<Func<_,_>> * Expression<Func<_,_,_>> -> _ @> 
        fun (outerSourceTy,innerSourceTy,keyTy,resultTy,outerSource,innerSource,outerKeyVar,outerKeySelector,innerKeyVar,innerKeySelector,outerResultKeyVar,innerResultKeyVar,resultSelector)  -> 
            let outerKeySelector = Expr.NewDelegate(MakeQueryFuncTy(outerSourceTy,keyTy), [outerKeyVar],outerKeySelector) |> MakeFakeExpression 
            let innerKeySelector = Expr.NewDelegate(MakeQueryFuncTy(innerSourceTy,keyTy), [innerKeyVar],innerKeySelector) |> MakeFakeExpression 
            let resultSelector = Expr.NewDelegate(MakeQueryFunc2Ty(outerSourceTy,innerSourceTy,resultTy), [outerResultKeyVar;innerResultKeyVar],resultSelector) |> MakeFakeExpression 
            F ([outerSourceTy;innerSourceTy;keyTy;resultTy],[outerSource;innerSource;outerKeySelector;innerKeySelector;resultSelector])


    let MakeQueryableGroupJoin = 
        let F = MakeGenericStaticMethod <@ System.Linq.Queryable.GroupJoin : _ * _ * Expression<Func<_,_>> * Expression<Func<_,_>> * Expression<Func<_,_,_>> -> _ @> 
        fun (outerSourceTy,innerSourceTy,keyTy,resultTy,outerSource,innerSource,outerKeyVar,outerKeySelector,innerKeyVar,innerKeySelector,outerResultKeyVar,innerResultKeyVar,resultSelector)  -> 
            let outerKeySelector = Expr.NewDelegate(MakeQueryFuncTy(outerSourceTy,keyTy), [outerKeyVar],outerKeySelector) |> MakeFakeExpression 
            let innerKeySelector = Expr.NewDelegate(MakeQueryFuncTy(innerSourceTy,keyTy), [innerKeyVar],innerKeySelector) |> MakeFakeExpression 
            let resultSelector = Expr.NewDelegate(MakeQueryFunc2Ty(outerSourceTy,MakeIEnumerableTy(innerSourceTy),resultTy), [outerResultKeyVar;innerResultKeyVar],resultSelector) |> MakeFakeExpression 
            F ([outerSourceTy;innerSourceTy;keyTy;resultTy],[outerSource;innerSource;outerKeySelector;innerKeySelector;resultSelector])



    /// Project F# function expressions to Linq LambdaExpression nodes
    let FuncExprToLinqFunc2Expression (srcTy,targetTy,v,body) = 
        Expr.NewDelegate(Linq.Expressions.Expression.GetFuncType [| srcTy; targetTy |], [v],body).ToLinqExpression()


    let MakeMapConcat = 
        let F = MakeGenericStaticMethod <@ Seq.collect @>
        fun (srcTy,targetTy,f,src)  -> 
            F ([srcTy;MakeIEnumerableTy targetTy;targetTy],[f;src])

    let MakeFilter = 
        let F = MakeGenericStaticMethod <@ Seq.filter @>
        fun (srcTy,f,src)  -> 
            F ([srcTy],[f;src])

    let (|MacroReduction|_|) (p : Expr) = 
        match p with 
        | Applications(Lambdas(vs,body),args) when vs.Length = args.Length && List.forall2 (fun vs arg -> List.length vs = List.length args) vs args -> 
            let tab = Map.ofSeq (List.concat (List.map2 List.zip vs args))
            let body = body.Substitute tab.TryFind 
            Some body

        // Macro
        | PropertyGet(None,Getter(MethodWithReflectedDefinition(body)),[]) -> 
            Some body

        // Macro
        | Call(None,MethodWithReflectedDefinition(Lambdas(vs,body)),args) -> 
            let tab = Map.ofSeq (List.concat (List.map2 (fun (vs:Var list) arg -> match vs,arg with [v],arg -> [(v,arg)] | vs,NewTuple(args) -> List.zip vs args | _ -> List.zip vs [arg]) vs args))
            let body = body.Substitute tab.TryFind 
            Some body

        // Macro
        | Let(v,e,body) ->
            let tab = Map.ofSeq [ (v,e) ]
            let body = body.Substitute tab.TryFind 
            Some body
        | _ -> None

    let rec MacroExpand (p : Expr) = 
        match p with 
        // Macro reduction
        | MacroReduction(reduced) -> 
            MacroExpand reduced
        | ExprShape.ShapeCombination(comb,args) ->
            ExprShape.RebuildShapeCombination(comb, List.map MacroExpand args)
        | ExprShape.ShapeLambda(v,body) ->
            Expr.Lambda(v, MacroExpand body)
        | ExprShape.ShapeVar _ -> p

    let (|PipedCallMapConcat|_|) = (|SpecificPipedCall1|_|) <@ Seq.collect @>
    let (|CallSingleton|_|) = (|SpecificCall|_|) <@ Seq.singleton @>
    let (|CallEmpty|_|) = (|SpecificCall|_|) <@ Seq.empty @>
    let (|PipedCallSort|_|) = (|SpecificPipedCall0|_|) <@ Seq.sort @>
    let (|PipedCallSortBy|_|) = (|SpecificPipedCall1|_|) <@ Seq.sortBy @>
    let (|PipedCallSeqGroupBy|_|) = (|SpecificPipedCall1|_|) <@ Seq.groupBy @>
    let (|PipedCallSeqMinBy|_|) = (|SpecificPipedCall1|_|) <@ Seq.minBy @>
    let (|PipedCallSeqMaxBy|_|) = (|SpecificPipedCall1|_|) <@ Seq.maxBy @>
    let (|PipedCallQueryGroupBy|_|) = (|SpecificPipedCall1|_|) <@ groupBy @>
    let (|PipedCallQueryMinBy|_|) = (|SpecificPipedCall1|_|) <@ minBy @>
    let (|PipedCallQueryMaxBy|_|) = (|SpecificPipedCall1|_|) <@ maxBy @>
    let (|PipedCallSeqMap|_|) = (|SpecificPipedCall1|_|) <@ Seq.map @>
    let (|PipedCallSeqAppend|_|) = (|SpecificPipedCall1|_|) <@ Seq.append @>
    let (|PipedCallSeqFilter|_|) = (|SpecificPipedCall1|_|) <@ Seq.filter @>
    let (|PipedCallSeqExists|_|) = (|SpecificPipedCall1|_|) <@ Seq.exists @>
    let (|PipedCallSeqForAll|_|) = (|SpecificPipedCall1|_|) <@ Seq.forall @>
    let (|PipedCallSeqDelay|_|) = (|SpecificPipedCall0|_|) <@ Seq.delay @>
    let (|PipedCallSeqDistinct|_|) = (|SpecificPipedCall0|_|) <@ Seq.distinct @>
    let (|PipedCallSeqToList|_|) = (|SpecificPipedCall0|_|) <@ Seq.toList @>
    let (|PipedCallSeqTake|_|) = (|SpecificPipedCall1|_|) <@ Seq.take @>
    let (|PipedCallSeqTruncate|_|) = (|SpecificPipedCall1|_|) <@ Seq.truncate @>
    let (|PipedCallSeqToArray|_|) = (|SpecificPipedCall0|_|) <@ Seq.toArray @>
    let (|PipedCallSeqMin|_|) = (|SpecificPipedCall0|_|) <@ Seq.min @>
    let (|PipedCallSeqMax|_|) = (|SpecificPipedCall0|_|) <@ Seq.max @>
    let (|PipedCallQueryContains|_|) = (|SpecificPipedCall1|_|) <@ contains @>
    let (|CallSeq|_|) = (|SpecificCall|_|) <@ seq @>
    let (|CallQueryJoin|_|) = (|SpecificCall|_|) <@ join @>
    let (|CallQueryGroupJoin|_|) = (|SpecificCall|_|) <@ groupJoin @>
    let (|PipedCallAverageBy|_|) = (|SpecificPipedCall1|_|) <@ Seq.averageBy : (float -> float) -> seq<float> -> float @> 
    let (|PipedCallAverage|_|) = (|SpecificPipedCall0|_|) <@ Seq.average: seq<float> -> float @>
    let (|PipedCallSumBy|_|) = (|SpecificPipedCall1|_|) <@ Seq.sumBy : (float -> float) -> seq<float> -> float @>
    let (|PipedCallSum|_|) = (|SpecificPipedCall0|_|) <@ Seq.sum: seq<float> -> float @>
    let (|PipedCallSeqLength|_|) = (|SpecificPipedCall0|_|) <@ Seq.length @>
    let (|PipedCallSeqHead|_|) = (|SpecificPipedCall0|_|) <@ Seq.head @>
    let (|PipedCallSeqFind|_|) = (|SpecificPipedCall1|_|) <@ Seq.find @>

    let query (p : Expr<'T1>) : 'T1 = 

        let body = (p :> Expr)
        let rec TransInner (tm:Expr) = 
            match tm with 
            // Look through coercions, e.g. to IEnumerable 
            | Coerce (expr,ty) -> 
                TransInner expr
            
            // Seq.collect (fun x -> if P x then tgt else Seq.empty)  sq @> 
            //    ~~> TRANS(Seq.collect (x -> tgt) (sq.Where(x -> P x))
            | PipedCallMapConcat ([srcTy;_;targetTy],Lambda(selectorVar, selector),sq) ->
                let rec TransMapConcatSelector t = 
                    match t with 
                    | CallSingleton(None, _,[res]) -> 

                        MakeQueryableSelect(srcTy,targetTy, TransInner sq, [selectorVar],MacroExpand res)


                    | IfThenElse(g,tgt,CallEmpty (None, [_],[])) ->

                        let sq = MakeFilter(srcTy,Expr.Lambda(selectorVar,g),sq)
                        let sq = TransInner (MakeMapConcat(srcTy,targetTy,Expr.Lambda(selectorVar,tgt),sq))
                        sq

                    | MacroReduction(reduced) -> 
                        TransMapConcatSelector reduced

                    | selectorBody ->
                        let selectorBody = TransInner selectorBody
                        // For some reason IQueryable.SelectMany expects an IEnumerable return
                        // REVIEW: Previous notes in this file said that LINQ likes to see a coercion to an interface type at this point. 
                        let selectorBody = Expr.Coerce(TransInner selectorBody,MakeIEnumerableTy targetTy)
                        MakeQueryableSelectMany(srcTy,targetTy, TransInner sq, selectorVar,selectorBody)

                TransMapConcatSelector selector
            
            | PipedCallSeqMap ([srcTy;targetTy],Lambda(v,res),sq) ->
        
                MakeQueryableSelect(srcTy,targetTy, TransInner sq, [v],MacroExpand res)

            | PipedCallSeqAppend ([srcTy],sq1,sq2) ->
        
                MakeQueryableAppend(srcTy, TransInner sq1, TransInner sq2)

            // These occur in the F# quotation form of F# sequence expressions            
            | PipedCallSeqFilter ([srcTy],Lambda(v,res),sq) ->

                MakeQueryableWhere(srcTy, TransInner sq, v,MacroExpand res)

            // These occur in the F# quotation form of F# sequence expressions            
            | PipedCallSeqDelay (_,Lambda(_,body)) ->
        
                TransInner body
            
            // These occur in the F# quotation form of F# sequence expressions            
            | CallSeq (None, _,[body]) ->
        
                TransInner body

                
            | IfThenElse(g,t,e) ->

                Expr.IfThenElse(g,TransInner t, TransInner e)


            // These occur in the F# quotation form of F# sequence expressions            
            | CallEmpty (None, [ty],[]) ->
        
                MakeQueryableEmpty ty


            | PipedCallSortBy([ srcTy; keyTy ],Lambda(v,keySelector),source) ->

                MakeQueryableOrderBy(srcTy,keyTy, TransInner source, v,MacroExpand keySelector)

            | PipedCallSort([ srcTy ],source) ->

                let v = new Var("x",srcTy)
                MakeQueryableOrderBy(srcTy,srcTy, TransInner source, v,Expr.Var v)

            | PipedCallSeqGroupBy _ ->

                failwithf "The operator Seq.groupBy may not be used in queries. Use Microsoft.FSharp.Query.groupNy instead, which has a different return type to the standard F# operator" tm 

            | PipedCallQueryGroupBy([ srcTy; keyTy ],Lambda(v,keySelector),source) ->

                MakeQueryableGroupBy(srcTy,keyTy, TransInner source, v,MacroExpand keySelector)

            | PipedCallSeqMinBy _ ->

                failwithf "The operator Seq.minBy may not be used in queries. Use Microsoft.FSharp.Query.minBy instead, which has a different return type to the standard F# operator" tm 

            | PipedCallQueryMinBy([ srcTy; keyTy ],Lambda(v,keySelector),source) ->

                MakeQueryableMinBy(srcTy,keyTy, TransInner source, v,MacroExpand keySelector)

            | PipedCallSeqMaxBy _ ->

                failwithf "The operator Seq.maxBy may not be used in queries. Use Microsoft.FSharp.Query.maxBy instead, which has a different return type to the standard F# operator" tm 

            | PipedCallQueryMaxBy([ srcTy; keyTy ],Lambda(v,keySelector),source) ->

                MakeQueryableMaxBy(srcTy,keyTy, TransInner source, v,MacroExpand keySelector)

            | PipedCallSeqExists([ srcTy],Lambda(v,keySelector),source) ->

                MakeQueryableAny(srcTy,TransInner source, v,MacroExpand keySelector)

            | PipedCallSeqForAll([ srcTy],Lambda(v,keySelector),source) ->

                MakeQueryableAll(srcTy,TransInner source, v,MacroExpand keySelector)

            | CallQueryJoin(None, [ outerSourceTy;innerSourceTy;keyTy;resultTy ],[outerSource;innerSource;Lambda(outerKeyVar,outerKeySelector);Lambda(innerKeyVar,innerKeySelector);Lambdas([[outerResultKeyVar];[innerResultKeyVar]],resultSelector)])->

                MakeQueryableJoin(outerSourceTy,innerSourceTy,keyTy,resultTy,TransInner outerSource,TransInner innerSource,outerKeyVar,MacroExpand outerKeySelector,innerKeyVar,MacroExpand innerKeySelector,outerResultKeyVar,innerResultKeyVar,MacroExpand resultSelector)  

            | CallQueryGroupJoin(None, [ outerSourceTy;innerSourceTy;keyTy;resultTy ],[outerSource;innerSource;Lambda(outerKeyVar,outerKeySelector);Lambda(innerKeyVar,innerKeySelector);Lambdas([[outerResultKeyVar];[innerResultKeyVar]],resultSelector)])->

                MakeQueryableGroupJoin(outerSourceTy,innerSourceTy,keyTy,resultTy,TransInner outerSource,TransInner innerSource,outerKeyVar,MacroExpand outerKeySelector,innerKeyVar,MacroExpand innerKeySelector,outerResultKeyVar,innerResultKeyVar,MacroExpand resultSelector)  

            | PipedCallSeqDistinct([ srcTy ],source) ->
                MakeQueryableDistinct(srcTy, TransInner source)


            | PipedCallSeqTake([ srcTy ],count,sq) 
            | PipedCallSeqTruncate([ srcTy ],count,sq) ->
                MakeQueryableTake(srcTy, TransInner sq, MacroExpand count)

            | MacroReduction(reduced) -> 
                TransInner reduced

            // These occur in the F# quotation form of F# sequence expressions            
            //
            //       match i.Data with 
            //       | 8 -> ...
            //       | _ -> ()
            | Sequential(Value( _, unitType), sq) when unitType  = typeof<unit> -> 
        
                TransInner sq

            | expr when typeof<IQueryable>.IsAssignableFrom(expr.Type) ->
                expr


            // Error cases
            | _  -> 
                failwithf "The following construct was used in query but is not recognised by the F#-to-LINQ query translator:\n%A\nThis is not a valid query expression. Check the specification of permitted queries and consider moving some of the query out of the quotation" tm 

        let EvalInner (tm:Expr) = (TransInner tm).EvalUntyped()

        let rec EvalOuterWithPostProcess (tm:Expr) = 
            match tm with
            
            // Allow SQL <@ [ for x in ... ] @>
            | PipedCallSeqToList ([srcTy],sq) ->
        
                CallSeqToList (srcTy,EvalInner sq)

            | PipedCallSeqToArray ([srcTy],sq) ->
        
                CallSeqToArray (srcTy,EvalInner sq)


            | PipedCallSeqMin ([srcTy],sq) ->

                CallQueryableMin(srcTy,EvalInner sq)

            | PipedCallSeqMax ([srcTy],sq) ->

                CallQueryableMax(srcTy,EvalInner sq)

            | PipedCallQueryContains ([srcTy],v,sq) ->

                CallQueryableContains(srcTy,EvalInner sq,(MacroExpand v).ToLinqExpression())

            | PipedCallAverageBy([srcTy;destTy],Lambda(v,res),sq) ->

                CallQueryableAverageBy(srcTy, destTy, EvalInner sq, FuncExprToLinqFunc2Expression (srcTy,destTy,v,MacroExpand  res))

            | PipedCallAverage ([srcTy],sq) ->

                CallQueryableAverage(srcTy, EvalInner sq)

            | PipedCallSumBy ([srcTy;destTy],Lambda(v,res),sq) ->

                CallQueryableSumBy(srcTy, destTy, EvalInner sq, FuncExprToLinqFunc2Expression (srcTy,destTy,v,MacroExpand  res))

            | PipedCallSum ([srcTy],sq) ->

                CallQueryableSum(srcTy, EvalInner sq)

            | PipedCallSeqLength ([ srcTy ],sq) ->
                CallQueryableCount(srcTy, EvalInner sq)

            | PipedCallSeqHead ([ srcTy ],sq) ->
                CallQueryableFirst(srcTy, EvalInner sq)

            | PipedCallSeqFind([ srcTy ],sq, NewDelegate(_,[v],f)) ->
                CallQueryableFirstFind(srcTy, EvalInner sq, FuncExprToLinqFunc2Expression (srcTy,boolTy,v,MacroExpand f))

            | MacroReduction(reduced) -> 
                EvalOuterWithPostProcess reduced

            | _  -> 
                EvalInner tm
        
        let res = EvalOuterWithPostProcess body 
        unbox res


    let SQL x = query x

open Query

module IQueryableTests =
    
    type Customer = { mutable Name:string; Data: int; Cost:float; Sizes: int list }
    let c1 = { Name="Don"; Data=6; Cost=6.2; Sizes=[1;2;3;4] }
    let c2 = { Name="Peter"; Data=7; Cost=4.2; Sizes=[10;20;30;40] }
    let c3 = { Name="Freddy"; Data=8; Cost=9.2; Sizes=[11;12;13;14] }
    let c4 = { Name="Freddi"; Data=8; Cost=1.0; Sizes=[21;22;23;24] }
    
    let data = [c1;c2;c3;c4]
    let db = System.Linq.Queryable.AsQueryable<Customer>(data |> List.toSeq)

    printfn "db = %A" (Reflection.FSharpType.GetRecordFields (typeof<Customer>, System.Reflection.BindingFlags.Public ||| System.Reflection.BindingFlags.NonPublic))
    let q1 = <@ seq { for i in db -> i.Name } @>
    let q2 = <@ c1.Name @>
    printfn "q2 = %A" q2
    
    printfn "db = %A" db.Expression

    let checkCommuteSeq s q =
        check s (query q |> Seq.toList) (q.Eval() |> Seq.toList)

    let checkCommuteVal s q =
        check s (query q) (q.Eval())

    let q = <@ System.DateTime.Now - System.DateTime.Now @>
    q.Eval()
    
    checkCommuteSeq "cnewnc01" <@ db @>
    //debug <- true
    checkCommuteSeq "cnewnc02" <@ seq { for i in db -> i }  @>
    checkCommuteSeq "cnewnc03" <@ seq { for i in db -> i.Name }  @>
    checkCommuteSeq "cnewnc04" <@ [ for i in db -> i.Name ]  @>
    checkCommuteSeq "cnewnc05" <@ [ for i in db do yield i.Name ]  @>
    checkCommuteSeq "cnewnc06" <@ [ for i in db do 
                                      for j in db do 
                                          yield (i.Name,j.Name) ] @>

    checkCommuteSeq "cnewnc07" <@ [ for i in db do 
                                      for j in db do 
                                          if i.Data = j.Data then 
                                              yield (i.Data,i.Name,j.Name) ] @>

    checkCommuteSeq "cnewnc08"  <@ [ for i in db do 
                                      if i.Data = 8 then 
                                          for j in db do 
                                              if i.Data = j.Data then 
                                                  yield (i.Data,i.Name,j.Name) ] @>

    checkCommuteSeq "cnewnc08"  <@ [ for i in db do 
                                      match i.Data with 
                                      | 8 -> 
                                          for j in db do 
                                              if i.Data = j.Data then 
                                                  yield (i.Data,i.Name,j.Name) 
                                      | _ -> 
                                          () ] @>



    // EXPECT PASS
    checkCommuteSeq "cnewnc08"  <@  seq { for i in db do for j in db do yield (i.Data) }  @>
    checkCommuteSeq "cnewnc08"  <@  db |> Seq.take 3 @>
    checkCommuteVal "cnewnc08"  <@  db |> Seq.take 3 |> Seq.length @>
    checkCommuteVal "cnewnc08"  <@  Seq.length (seq { for i in db do for j in db do yield (i.Data) })   @> 
    checkCommuteVal "cnewnc08"  <@  Seq.length (seq { for i in db do for j in db do yield (i.Data) })   @> 
    checkCommuteVal "cnewnc08"  <@  (seq { for i in db do for j in db do yield (i.Data) })  |> Seq.length  @> 
    checkCommuteVal "cnewnc08"  <@  Seq.max (seq { for i in db do for j in db do yield (i.Data) })   @> 
    checkCommuteVal "cnewnc08"  <@  seq { for i in db do for j in db do yield (i.Data) } |> Seq.max   @> 
    checkCommuteVal "cnewnc08"  <@  Seq.min (seq { for i in db do for j in db do yield (i.Data) })   @> 
    checkCommuteVal "cnewnc08"  <@  seq { for i in db do for j in db do yield (i.Data) } |> Seq.min  @> 
    checkCommuteSeq "cnewnc08"  <@ Seq.distinct (seq { for i in db do for j in db do yield i }) @>
    checkCommuteSeq "cnewnc08"  <@ seq { for i in db do for j in db do yield i } |> Seq.distinct @>
    checkCommuteVal "cnewnc08"  <@  Seq.max (seq { for i in db do for j in db do yield float i.Data })   @> 
    checkCommuteVal "cnewnc08"  <@  Seq.max (seq { for i in db do for j in db do yield float32 i.Data })   @> 
    checkCommuteVal "cnewnc08"  <@  Seq.max (seq { for i in db do for j in db do yield decimal i.Data })   @> 
    checkCommuteVal "cnewnc08"  <@  Seq.min (seq { for i in db do for j in db do yield float i.Data })   @> 
    checkCommuteVal "cnewnc08"  <@  Seq.min (seq { for i in db do for j in db do yield float32 i.Data })   @> 
    checkCommuteVal "cnewnc08"  <@  Seq.min (seq { for i in db do for j in db do yield decimal i.Data })   @> 
    checkCommuteVal "cnewnc08"  <@  Seq.average (seq { for i in db do for j in db do yield i.Cost })   @> 
    checkCommuteVal "cnewnc08"  <@   (seq { for i in db do for j in db do yield i.Cost })  |> Seq.average @> 

    checkCommuteSeq "cnewnc08"  (<@ Query.groupBy
                                        (fun x -> x) 
                                        (seq { for i in db do yield i.Data })  |> Seq.map Seq.toList |> Seq.toList @> ) 


    checkCommuteSeq "cnewnc08"  (<@ Query.join 
                                        (seq { for i in db do yield i.Data }) 
                                        (seq { for i in db do yield i.Data })  
                                        (fun x -> x) 
                                        (fun x -> x) 
                                        (fun x y -> x + y) @> ) 

    checkCommuteSeq "cnewnc08"  (<@ Query.groupJoin 
                                        (seq { for i in db do yield i.Data }) 
                                        (seq { for i in db do yield i.Data }) 
                                        (fun x -> x) 
                                        (fun x -> x) 
                                        (fun x y -> x + Seq.length y)  @> ) 

    check "cnewnc08"  (query <@  Seq.average (seq { for i in db do for j in db do yield (i.Cost) })   @>  - 5.15 <= 0.0000001) true

    // By design: no 'distinctBy', "maxBy", "minBy", "groupBy" support in queries
    check "lcvkneio" (try let _ = query <@ Seq.distinctBy (fun x -> x) (seq { for i in db do for j in db do yield i })  @> in  false with _ -> true) true
    check "lcvkneio" (try let _ = query <@ Seq.maxBy (fun x -> x) (seq { for i in db do for j in db do yield i })  @> in  false with _ -> true) true
    check "lcvkneio" (try let _ = query <@ Seq.minBy (fun x -> x) (seq { for i in db do for j in db do yield i })  @> in  false with _ -> true) true
    check "lcvkneio" (try let _ = query <@ Seq.groupBy (fun x -> x) (seq { for i in db do for j in db do yield i })  @> in  false with _ -> true) true
    
    

let _ = 
  if not failures.IsEmpty then 
      printfn "Test Failed, failures = %A" failures; 
      exit 1
  else  
      stdout.WriteLine "Test Passed"; 
      System.IO.File.WriteAllText("test.ok","ok"); 
      exit 0
