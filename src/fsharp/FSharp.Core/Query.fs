// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.FSharp.Linq

open System
open System.Linq
open System.Collections
open System.Collections.Generic

open Microsoft.FSharp
open Microsoft.FSharp.Core
open Microsoft.FSharp.Core.Operators
open Microsoft.FSharp.Collections
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Linq.RuntimeHelpers

#nowarn "64"

[<NoComparison; NoEquality; Sealed>]
type QuerySource<'T, 'Q> (source: seq<'T>) =
     member _.Source = source

[<AutoOpen>]
module Helpers =

    // This helps the somewhat complicated type inference for AverageByNullable and SumByNullable, by making both type in a '+' the same
    let inline plus (x:'T) (y:'T) = Checked.(+) x y

    let inline checkNonNull argName arg =
        match box arg with
        | null -> nullArg argName
        | _ -> ()

    let checkThenBySource (source: seq<'T>) =
        match source with
        | :? System.Linq.IOrderedEnumerable<'T> as source -> source
        | _ -> invalidArg "source" (SR.GetString(SR.thenByError))

// used so we can define the implementation of QueryBuilder before the Query module (so in Query we can safely use methodhandleof)
module ForwardDeclarations =
    type IQueryMethods =
        abstract Execute: Expr<'T> -> 'U
        abstract EliminateNestedQueries: Expr -> Expr

    let mutable Query =
        {
            new IQueryMethods with
                member this.Execute(_) = failwith "IQueryMethods.Execute should never be called"
                member this.EliminateNestedQueries(_) = failwith "IQueryMethods.EliminateNestedQueries should never be called"
        }

type QueryBuilder() =
    member _.For (source: QuerySource<'T, 'Q>, body: 'T -> QuerySource<'Result, 'Q2>) : QuerySource<'Result, 'Q> =
        QuerySource (Seq.collect (fun x -> (body x).Source) source.Source)

    member _.Zero () =
        QuerySource Seq.empty

    member _.Yield value =
        QuerySource (Seq.singleton value)

    member _.YieldFrom (computation: QuerySource<'T, 'Q>) : QuerySource<'T, 'Q> =
        computation

    // Indicates to the F# compiler that an implicit quotation is added to use of 'query'
    member _.Quote  (quotation: Quotations.Expr<'T>) =
        quotation

    member _.Source (source: IQueryable<'T>) =
        QuerySource source

    member _.Source (source: IEnumerable<'T>) : QuerySource<'T, System.Collections.IEnumerable> =
        QuerySource source

    member _.Contains (source: QuerySource<'T, 'Q>, key) =
        Enumerable.Contains(source.Source, key)

    member _.Select (source: QuerySource<'T, 'Q>, projection) : QuerySource<'U, 'Q> =
        QuerySource (Seq.map projection source.Source)

    member _.Where (source: QuerySource<'T, 'Q>, predicate) : QuerySource<'T, 'Q> =
        QuerySource (Enumerable.Where (source.Source, Func<_, _>(predicate)) )

    member _.Last (source: QuerySource<'T, 'Q>) =
        Enumerable.Last source.Source

    member _.LastOrDefault (source: QuerySource<'T, 'Q>) =
        Enumerable.LastOrDefault source.Source

    member _.ExactlyOne (source: QuerySource<'T, 'Q>) =
        Enumerable.Single source.Source

    member _.ExactlyOneOrDefault (source: QuerySource<'T, 'Q>) =
        Enumerable.SingleOrDefault source.Source

    member _.Count (source: QuerySource<'T, 'Q>) =
        Enumerable.Count source.Source

    member _.Distinct (source: QuerySource<'T, 'Q> when 'T : equality) : QuerySource<'T, 'Q> =
        QuerySource (Enumerable.Distinct source.Source)

    member _.Exists(source: QuerySource<'T, 'Q>, predicate) =
        Enumerable.Any (source.Source, Func<_, _>(predicate))

    member _.All (source: QuerySource<'T, 'Q>, predicate) =
        Enumerable.All (source.Source, Func<_, _>(predicate))

    member _.Head (source: QuerySource<'T, 'Q>) =
        Enumerable.First source.Source

    member _.Nth (source: QuerySource<'T, 'Q>, index) =
        Enumerable.ElementAt (source.Source, index)

    member _.Skip (source: QuerySource<'T, 'Q>, count) : QuerySource<'T, 'Q> =
        QuerySource (Enumerable.Skip (source.Source, count))

    member _.SkipWhile (source: QuerySource<'T, 'Q>, predicate) : QuerySource<'T, 'Q> =
        QuerySource (Enumerable.SkipWhile (source.Source, Func<_, _>(predicate)))

    member _.Take (source: QuerySource<'T, 'Q>, count) : QuerySource<'T, 'Q> =
        QuerySource (Enumerable.Take (source.Source, count))

    member _.TakeWhile (source: QuerySource<'T, 'Q>, predicate) : QuerySource<'T, 'Q> =
        QuerySource (Enumerable.TakeWhile (source.Source, Func<_, _>(predicate)))

    member _.Find (source: QuerySource<'T, 'Q>, predicate) =
        Enumerable.First (source.Source, Func<_, _>(predicate))

    member _.HeadOrDefault (source: QuerySource<'T, 'Q>) =
        Enumerable.FirstOrDefault source.Source

    member _.MinBy<'T, 'Q, 'Key when 'Key: equality and 'Key: comparison> (source: QuerySource<'T, 'Q>, valueSelector: 'T -> 'Key) =
        Enumerable.Min(source.Source, Func<'T, 'Key>(valueSelector))

    member _.MaxBy<'T, 'Q, 'Key when 'Key: equality and 'Key: comparison> (source: QuerySource<'T, 'Q>, valueSelector: 'T -> 'Key) =
        Enumerable.Max(source.Source, Func<'T, 'Key>(valueSelector))

    member _.MinByNullable<'T, 'Q, 'Key when 'Key: equality and 'Key: comparison and 'Key: (new: unit -> 'Key) and 'Key: struct and 'Key:> ValueType> (source: QuerySource<'T, 'Q>, valueSelector: 'T -> Nullable<'Key>) =
        Enumerable.Min(source.Source, Func<'T, Nullable<'Key>>(valueSelector))

    member _.MaxByNullable<'T, 'Q, 'Key when 'Key: equality and 'Key: comparison and 'Key: (new: unit -> 'Key) and 'Key: struct and 'Key:> ValueType> (source: QuerySource<'T, 'Q>, valueSelector: 'T -> Nullable<'Key>) =
        Enumerable.Max(source.Source, Func<'T, Nullable<'Key>>(valueSelector))

    member inline _.SumByNullable<'T, 'Q, ^Value
                                      when ^Value :> ValueType
                                      and ^Value : struct
                                      and ^Value : (new : unit -> ^Value)
                                      and ^Value : (static member ( + ) : ^Value * ^Value -> ^Value)
                                      and  ^Value : (static member Zero : ^Value)
                                      and default ^Value : int>
                  (source: QuerySource<'T, 'Q>, valueSelector : 'T -> Nullable< ^Value >) : Nullable< ^Value > =

        let source = source.Source
        checkNonNull "source" source
        use e = source.GetEnumerator()
        let mutable acc : ^Value = LanguagePrimitives.GenericZero< (^Value) >
        while e.MoveNext() do
            let v : Nullable< ^Value > = valueSelector e.Current
            if v.HasValue then
                acc <- plus acc (v.Value : ^Value)
        Nullable acc

    member inline _.AverageByNullable< 'T, 'Q, ^Value
                                          when ^Value :> ValueType
                                          and ^Value : struct
                                          and ^Value : (new : unit -> ^Value)
                                          and ^Value : (static member ( + ) : ^Value * ^Value -> ^Value)
                                          and  ^Value : (static member DivideByInt : ^Value * int -> ^Value)
                                          and  ^Value : (static member Zero : ^Value)
                                          and default ^Value : float >

             (source: QuerySource<'T, 'Q>, projection: 'T -> Nullable< ^Value >) : Nullable< ^Value > =

        let source = source.Source
        checkNonNull "source" source
        use e = source.GetEnumerator()
        let mutable acc = LanguagePrimitives.GenericZero< (^Value) >
        let mutable count = 0
        while e.MoveNext() do
            let v = projection e.Current
            if v.HasValue then
                acc <- plus acc v.Value
            count <- count + 1
        if count = 0 then Nullable() else Nullable(LanguagePrimitives.DivideByInt< (^Value) > acc count)

    member inline _.AverageBy< 'T, 'Q, ^Value
                                  when ^Value : (static member ( + ) : ^Value * ^Value -> ^Value)
                                  and  ^Value : (static member DivideByInt : ^Value * int -> ^Value)
                                  and  ^Value : (static member Zero : ^Value)
                                  and default ^Value : float >
             (source: QuerySource<'T, 'Q>, projection: 'T -> ^Value) : ^Value =

        let source = source.Source
        checkNonNull "source" source
        use e = source.GetEnumerator()
        let mutable acc = LanguagePrimitives.GenericZero< (^U) >
        let mutable count = 0
        while e.MoveNext() do
            acc <- plus acc (projection e.Current)
            count <- count + 1
        if count = 0 then
            invalidOp "source"
        LanguagePrimitives.DivideByInt< (^U) > acc count

    member inline _.SumBy< 'T, 'Q, ^Value
                                  when ^Value : (static member ( + ) : ^Value * ^Value -> ^Value)
                                  and  ^Value : (static member Zero : ^Value)
                                  and default ^Value : int >
             (source: QuerySource<'T, 'Q>, projection : ('T -> ^Value)) : ^Value =

        Seq.sumBy projection source.Source

    member _.GroupBy (source: QuerySource<'T, 'Q>, keySelector: _ -> 'Key) : QuerySource<_, 'Q> when 'Key : equality =
        QuerySource (Enumerable.GroupBy(source.Source, Func<_, _>(keySelector)))

    member _.SortBy (source: QuerySource<'T, 'Q>, keySelector: 'T -> 'Key) : QuerySource<'T, 'Q> when 'Key : equality and 'Key : comparison =
        QuerySource (Enumerable.OrderBy(source.Source, Func<_, _>(keySelector)))

    member _.SortByDescending (source: QuerySource<'T, 'Q>, keySelector: 'T -> 'Key) : QuerySource<'T, 'Q> when 'Key : equality and 'Key : comparison =
        QuerySource (Enumerable.OrderByDescending(source.Source, Func<_, _>(keySelector)))

    member _.ThenBy (source: QuerySource<'T, 'Q>, keySelector: 'T -> 'Key) : QuerySource<'T, 'Q> when 'Key : equality and 'Key : comparison =
        QuerySource (Enumerable.ThenBy(checkThenBySource source.Source, Func<_, _>(keySelector)))

    member _.ThenByDescending (source: QuerySource<'T, 'Q>, keySelector: 'T -> 'Key) : QuerySource<'T, 'Q> when 'Key : equality and 'Key : comparison =
        QuerySource (Enumerable.ThenByDescending(checkThenBySource source.Source, Func<_, _>(keySelector)))

    member _.SortByNullable (source: QuerySource<'T, 'Q>, keySelector: 'T -> Nullable<'Key>) : QuerySource<'T, 'Q> when 'Key : equality and 'Key : comparison =
        QuerySource (Enumerable.OrderBy(source.Source, Func<_, _>(keySelector)))

    member _.SortByNullableDescending (source: QuerySource<'T, 'Q>, keySelector: 'T -> Nullable<'Key>) : QuerySource<'T, 'Q> when 'Key : equality and 'Key : comparison =
        QuerySource (Enumerable.OrderByDescending(source.Source, Func<_, _>(keySelector)))

    member _.ThenByNullable (source: QuerySource<'T, 'Q>, keySelector: 'T -> Nullable<'Key>) : QuerySource<'T, 'Q> when 'Key : equality and 'Key : comparison =
        QuerySource (Enumerable.ThenBy(checkThenBySource source.Source, Func<_, _>(keySelector)))

    member _.ThenByNullableDescending (source:  QuerySource<'T, 'Q>, keySelector: 'T -> Nullable<'Key>) : QuerySource<'T, 'Q> when 'Key : equality and 'Key : comparison =
        QuerySource (Enumerable.ThenByDescending(checkThenBySource source.Source, Func<_, _>(keySelector)))

    member _.GroupValBy<'T, 'Key, 'Result, 'Q when 'Key : equality > (source: QuerySource<'T, 'Q>, resultSelector: 'T -> 'Result, keySelector: 'T -> 'Key) : QuerySource<IGrouping<'Key, 'Result>, 'Q> =
        QuerySource (Enumerable.GroupBy(source.Source, Func<'T, 'Key>(keySelector), Func<'T, 'Result>(resultSelector)))

    member _.Join  (outerSource: QuerySource<_, 'Q>, innerSource: QuerySource<_, 'Q>, outerKeySelector, innerKeySelector, resultSelector) : QuerySource<_, 'Q> =
        QuerySource (Enumerable.Join(outerSource.Source, innerSource.Source, Func<_, _>(outerKeySelector), Func<_, _>(innerKeySelector), Func<_, _, _>(resultSelector)))

    member _.GroupJoin (outerSource: QuerySource<_, 'Q>, innerSource: QuerySource<_, 'Q>, outerKeySelector, innerKeySelector, resultSelector: _ ->  seq<_> -> _) : QuerySource<_, 'Q> =
        QuerySource (Enumerable.GroupJoin(outerSource.Source, innerSource.Source, Func<_, _>(outerKeySelector), Func<_, _>(innerKeySelector), Func<_, _, _>(fun x g -> resultSelector x g)))

    member _.LeftOuterJoin (outerSource: QuerySource<_, 'Q>, innerSource: QuerySource<_, 'Q>, outerKeySelector, innerKeySelector, resultSelector: _ ->  seq<_> -> _) : QuerySource<_, 'Q> =
        QuerySource (Enumerable.GroupJoin(outerSource.Source, innerSource.Source, Func<_, _>(outerKeySelector), Func<_, _>(innerKeySelector), Func<_, _, _>(fun x g -> resultSelector x (g.DefaultIfEmpty()))))

    member _.RunQueryAsValue  (q: Quotations.Expr<'T>) : 'T =
        ForwardDeclarations.Query.Execute q

    member _.RunQueryAsEnumerable (q: Quotations.Expr<QuerySource<'T, IEnumerable>>) : IEnumerable<'T> =
        let queryAfterEliminatingNestedQueries = ForwardDeclarations.Query.EliminateNestedQueries q
        let queryAfterCleanup = Microsoft.FSharp.Linq.RuntimeHelpers.Adapters.CleanupLeaf queryAfterEliminatingNestedQueries
        (LeafExpressionConverter.EvaluateQuotation queryAfterCleanup :?> QuerySource<'T, IEnumerable>).Source

    member _.RunQueryAsQueryable (q: Quotations.Expr<QuerySource<'T, IQueryable>>) : IQueryable<'T> =
        ForwardDeclarations.Query.Execute q

    member this.Run q = this.RunQueryAsQueryable q

namespace Microsoft.FSharp.Linq.QueryRunExtensions

    open Microsoft.FSharp.Core

    [<AutoOpen>]
    module LowPriority =
        type Microsoft.FSharp.Linq.QueryBuilder with
            [<CompiledName("RunQueryAsValue")>]
            member this.Run (q: Microsoft.FSharp.Quotations.Expr<'T>) = this.RunQueryAsValue q

    [<AutoOpen>]
    module HighPriority =
        type Microsoft.FSharp.Linq.QueryBuilder with
            [<CompiledName("RunQueryAsEnumerable")>]
            member this.Run (q: Microsoft.FSharp.Quotations.Expr<Microsoft.FSharp.Linq.QuerySource<'T, System.Collections.IEnumerable>>) = this.RunQueryAsEnumerable q

namespace Microsoft.FSharp.Linq

open System
open System.Linq
open System.Collections
open System.Collections.Generic
open System.Linq.Expressions
open System.Reflection
open Microsoft.FSharp
open Microsoft.FSharp.Core
open Microsoft.FSharp.Core.Operators
open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicOperators
open Microsoft.FSharp.Collections
open Microsoft.FSharp.Reflection
open Microsoft.FSharp.Linq
open Microsoft.FSharp.Linq.RuntimeHelpers.Adapters
open Microsoft.FSharp.Linq.RuntimeHelpers
open Microsoft.FSharp.Linq.RuntimeHelpers.LeafExpressionConverter
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Quotations.DerivedPatterns

open Microsoft.FSharp.Linq.QueryRunExtensions

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Query =

    let ConvVar (v: Var) =
        Expression.Parameter(v.Type, v.Name)

    let asExpr x = (x :> Expression)

    let (|Getter|_|) (prop: PropertyInfo) =
        match prop.GetGetMethod true with
        | null -> None
        | v -> Some v
    let (|GenericArgs|) (minfo: MethodInfo) = minfo.GetGenericArguments() |> Array.toList

    // Match 'f x'
    let (|SpecificCall1|_|) q =
       let (|CallQ|_|) = (|SpecificCallToMethod|_|) q
       function
       | CallQ (Some builderObj, GenericArgs tyargs, [arg1]) -> Some(builderObj, tyargs, arg1)
       | _ -> None

    // Match 'f x y' or 'f (x, y)'
    let (|SpecificCall2|_|) q =
       let (|CallQ|_|) = (|SpecificCallToMethod|_|) q
       function
       | CallQ (Some builderObj, GenericArgs tyargs, [arg1; arg2]) -> Some(builderObj, tyargs, arg1, arg2)
       | _ -> None

    // Match 'f x y z' or 'f (x, y, z)'
    let (|SpecificCall3|_|) q =
       let (|CallQ|_|) = (|SpecificCallToMethod|_|) q
       function
       | CallQ (Some builderObj, GenericArgs tyargs, [arg1; arg2; arg3]) -> Some(builderObj, tyargs, arg1, arg2, arg3)
       | _ -> None

    /// (fun (x, y) -> z) is represented as 'fun p -> let x = p#0 let y = p#1' etc.
    /// This reverses this encoding, but does not de-tuple the input variable into multiple variables.
    let (|LambdaNoDetupling|_|) (lam: Expr) =
        /// Strip off the 'let' bindings for an LambdaNoDetupling
        let rec stripSuccessiveProjLets (p: Var) n expr =
            match expr with
            | Let(v1, (TupleGet(Var pA, m) as e1), rest)
                  when p = pA && m = n->
                      let restvs, b = stripSuccessiveProjLets p (n+1) rest
                      (v1, e1) :: restvs, b
            | _ -> ([], expr)
        match lam with
        | Lambda(v, body) ->
              let projs, b = stripSuccessiveProjLets v 0 body
              Some(v, projs, b)
        | _ -> None

    let restoreTupleProjections projs b = List.foldBack (fun (v, e) acc -> Expr.Let (v, e, acc)) projs b

    let (|LambdasNoDetupling|_|) (inpExpr: Expr) =
        let rec loop rvs rprojs e =
            match  e with
            | LambdaNoDetupling(v, projs, body) -> loop (v :: rvs) (projs :: rprojs) body
            | _ ->
                match rvs with
                | [] -> None
                | _ -> Some(List.rev rvs, restoreTupleProjections (List.concat (List.rev rprojs)) e)
        loop [] [] inpExpr


    let GetGenericMethodDefinition (methInfo:MethodInfo) =
        if methInfo.IsGenericMethod then methInfo.GetGenericMethodDefinition() else methInfo

    let CallGenericStaticMethod (methHandle:System.RuntimeMethodHandle) =
        let methInfo = methHandle |> System.Reflection.MethodInfo.GetMethodFromHandle :?> MethodInfo
        fun (tyargs: Type list, args: obj list) ->
            let methInfo = if methInfo.IsGenericMethod then methInfo.MakeGenericMethod(Array.ofList tyargs) else methInfo
            try
               methInfo.Invoke(null, Array.ofList args)
            with :? System.Reflection.TargetInvocationException as exn ->
              raise exn.InnerException

    let CallGenericInstanceMethod (methHandle:System.RuntimeMethodHandle) =
        let methInfo = methHandle |> System.Reflection.MethodInfo.GetMethodFromHandle :?> MethodInfo
        fun (objExpr:obj, tyargs: Type list, args: obj list) ->
            let methInfo = if methInfo.IsGenericMethod then methInfo.MakeGenericMethod(Array.ofList tyargs) else methInfo
            try
               methInfo.Invoke(objExpr, Array.ofList args)
            with :? System.Reflection.TargetInvocationException as exn ->
              raise exn.InnerException

    let BindGenericStaticMethod (methInfo:MethodInfo) tyargs =
        if methInfo.IsGenericMethod then
            methInfo.GetGenericMethodDefinition().MakeGenericMethod(Array.ofList tyargs)
        else
            methInfo

    let MakeGenericStaticMethod (methHandle:System.RuntimeMethodHandle) =
        let methInfo = methHandle |> System.Reflection.MethodInfo.GetMethodFromHandle :?> MethodInfo
        (fun (tyargs: Type list, args: Expr list) -> Expr.Call (BindGenericStaticMethod methInfo tyargs, args))

    let MakeGenericInstanceMethod (methHandle:System.RuntimeMethodHandle) =
        let methInfo = methHandle |> System.Reflection.MethodInfo.GetMethodFromHandle :?> MethodInfo
        (fun (obj: Expr, tyargs: Type list, args: Expr list) -> Expr.Call (obj, BindGenericStaticMethod methInfo tyargs, args))

    let ImplicitExpressionConversionHelperMethodInfo =
        methodhandleof (fun e -> LeafExpressionConverter.ImplicitExpressionConversionHelper e)
        |> System.Reflection.MethodInfo.GetMethodFromHandle
        :?> MethodInfo

    let MakeImplicitExpressionConversion (x: Expr) = Expr.Call (ImplicitExpressionConversionHelperMethodInfo.MakeGenericMethod [| x.Type |], [ x ])

    let NT = typedefof<System.Nullable<int>>

    let FT1 = typedefof<System.Func<_, _>>

    let FT2 = typedefof<System.Func<_, _, _>>

    let boolTy = typeof<bool>

    let MakeNullableTy ty = NT.MakeGenericType [| ty |]

    let MakeQueryFuncTy (dty, rty) = FT1.MakeGenericType [| dty; rty |]

    let MakeQueryFunc2Ty (dty1, dty2, rty) = FT2.MakeGenericType [| dty1; dty2; rty |]

    let IEnumerableTypeDef = typedefof<IEnumerable<_>>

    let IQueryableTypeDef = typedefof<IQueryable<_>>

    let QuerySourceTypeDef = typedefof<QuerySource<_, _>>

    let MakeIEnumerableTy dty= IEnumerableTypeDef.MakeGenericType [| dty|]

    let MakeIQueryableTy dty= IQueryableTypeDef.MakeGenericType [| dty|]

    let IsQuerySourceTy (ty: System.Type) = ty.IsGenericType && ty.GetGenericTypeDefinition() = QuerySourceTypeDef

    let IsIQueryableTy (ty: System.Type) = ty.IsGenericType && ty.GetGenericTypeDefinition() = IQueryableTypeDef

    let IsIEnumerableTy (ty: System.Type) = ty.IsGenericType && ty.GetGenericTypeDefinition() = IEnumerableTypeDef

    // Check a tag type on QuerySource is IQueryable
    let qTyIsIQueryable (ty : System.Type) = not (ty.Equals(typeof<IEnumerable>))

    let FuncExprToDelegateExpr (srcTy, targetTy, v, body) =
        Expr.NewDelegate (Linq.Expressions.Expression.GetFuncType [| srcTy; targetTy |], [v], body)

    /// Project F# function expressions to Linq LambdaExpression nodes
    let FuncExprToLinqFunc2Expression (srcTy, targetTy, v, body) =
        FuncExprToDelegateExpr(srcTy, targetTy, v, body) |> LeafExpressionConverter.QuotationToExpression

    let FuncExprToLinqFunc2 (srcTy, targetTy, v, body) =
        FuncExprToDelegateExpr(srcTy, targetTy, v, body) |> LeafExpressionConverter.EvaluateQuotation

    let MakersCallers F = CallGenericStaticMethod F, MakeGenericStaticMethod F

    let MakersCallersInstance F = CallGenericInstanceMethod F, MakeGenericInstanceMethod F

    let MakersCallers2 FQ FE = MakersCallers FQ, MakersCallers FE

    let MakeOrCallContainsOrElementAt FQ FE =
        let (CQ, MQ), (CE, ME) = MakersCallers2 FQ FE
        let Make (isIQ, srcItemTy: Type, src: Expr, key: Expr) =
            if isIQ then
                //let key = MakeImplicitExpressionConversion key
                MQ ([srcItemTy], [src; key])
            else
                ME ([srcItemTy], [src; key])

        let Call (isIQ, srcItemTy, src:obj, key: Expr) =
            let key = key |> LeafExpressionConverter.EvaluateQuotation
            let C = if isIQ then CQ else CE
            C ([srcItemTy], [src; box key])
        Make, Call

    let MakeContains, CallContains =
        let FQ = methodhandleof (fun (x, y) -> System.Linq.Queryable.Contains(x, y))
        let FE = methodhandleof (fun (x, y) -> Enumerable.Contains(x, y))
        MakeOrCallContainsOrElementAt FQ FE

    let MakeElementAt, CallElementAt =
        let FQ = methodhandleof (fun (x, y) -> System.Linq.Queryable.ElementAt(x, y))
        let FE = methodhandleof (fun (x, y) -> Enumerable.ElementAt(x, y))
        MakeOrCallContainsOrElementAt FQ FE

    let MakeOrCallMinByOrMaxBy FQ FE =
        let (CQ, MQ), (CE, ME) = MakersCallers2 FQ FE
        let Make (isIQ, src: Expr, v: Var, valSelector: Expr) =
            let srcItemTy = v.Type
            let keyElemTy = valSelector.Type
            let valSelector = FuncExprToDelegateExpr (srcItemTy, keyElemTy, v, valSelector)

            if isIQ then
                let valSelector = MakeImplicitExpressionConversion valSelector
                MQ ([srcItemTy; keyElemTy], [src; valSelector])
            else
                ME ([srcItemTy; keyElemTy], [src; valSelector])

        let Call (isIQ, srcItemTy: Type, _keyItemTy: Type, src:obj, keyElemTy: Type, v: Var, res: Expr) =
            if isIQ then
                let selector = FuncExprToLinqFunc2Expression (srcItemTy, keyElemTy, v, res)
                CQ ([srcItemTy; keyElemTy], [src; box selector])
            else
                let selector = FuncExprToLinqFunc2 (srcItemTy, keyElemTy, v, res)
                CE ([srcItemTy; keyElemTy], [src; selector])
        Make, Call

    let (MakeMinBy: bool * Expr * Var * Expr -> Expr), (CallMinBy : bool * Type * Type * obj * Type * Var * Expr -> obj) =
        let FQ = methodhandleof (fun (x, y: Expression<Func<_, _>>) -> System.Linq.Queryable.Min(x, y))
        let FE = methodhandleof (fun (x, y: Func<_, 'Result>) -> Enumerable.Min(x, y))
        MakeOrCallMinByOrMaxBy FQ FE

    let MakeMaxBy, CallMaxBy =
        let FQ = methodhandleof (fun (x, y: Expression<Func<_, _>>) -> System.Linq.Queryable.Max(x, y))
        let FE = methodhandleof (fun (x, y: Func<_, 'Result>) -> Enumerable.Max(x, y))
        MakeOrCallMinByOrMaxBy FQ FE

    let MakeMinByNullable, CallMinByNullable =
        // Note there is no separate LINQ overload for Min on nullables - the one implementation just magically skips nullable elements
        let FQ = methodhandleof (fun (x, y: Expression<Func<_, _>>) -> System.Linq.Queryable.Min(x, y))
        let FE = methodhandleof (fun (x, y: Func<_, 'Result>) -> Enumerable.Min(x, y))
        MakeOrCallMinByOrMaxBy FQ FE

    let MakeMaxByNullable, CallMaxByNullable =
        // Note there is no separate LINQ overload for Max on nullables - the one implementation just magically skips nullable elements
        let FQ = methodhandleof (fun (x, y: Expression<Func<_, _>>) -> System.Linq.Queryable.Max(x, y))
        let FE = methodhandleof (fun (x, y: Func<_, 'Result>) -> Enumerable.Max(x, y))
        MakeOrCallMinByOrMaxBy FQ FE

    let MakeOrCallAnyOrAllOrFirstFind FQ FE =
        let (CQ, MQ), (CE, ME) = MakersCallers2 FQ FE
        let Make (isIQ, src: Expr, v: Var, predicate: Expr) =
            let srcItemTy= v.Type
            let predicate = FuncExprToDelegateExpr (srcItemTy, boolTy, v, predicate)

            if isIQ then
                let predicate = MakeImplicitExpressionConversion predicate
                MQ ([srcItemTy], [src; predicate])
            else
                ME ([srcItemTy], [src; predicate])

        let Call (isIQ, srcItemTy: Type, src:obj, v: Var, res: Expr) =
            if isIQ then
                let selector = FuncExprToLinqFunc2Expression (srcItemTy, boolTy, v, res)
                CQ ([srcItemTy], [src; box selector])
            else
                let selector = FuncExprToLinqFunc2 (srcItemTy, boolTy, v, res)
                CE ([srcItemTy], [src; selector])
        Make, Call

    let MakeAny, CallAny =
        let FQ = methodhandleof (fun (x, y: Expression<Func<_, _>>) -> System.Linq.Queryable.Any(x, y))
        let FE = methodhandleof (fun (x, y: Func<_, _>) -> Enumerable.Any(x, y))
        MakeOrCallAnyOrAllOrFirstFind FQ FE

    let MakeAll, CallAll =
        let FQ = methodhandleof (fun (x, y: Expression<Func<_, _>>) -> System.Linq.Queryable.All(x, y))
        let FE = methodhandleof (fun (x, y: Func<_, _>) -> Enumerable.All(x, y))
        MakeOrCallAnyOrAllOrFirstFind FQ FE

    let MakeFirstFind, CallFirstFind =
        let FQ = methodhandleof (fun (x, y: Expression<Func<_, _>>) -> System.Linq.Queryable.First(x, y))
        let FE = methodhandleof (fun (x, y: Func<_, _>) -> Enumerable.First(x, y))
        MakeOrCallAnyOrAllOrFirstFind FQ FE

    let MakeOrCallAverageByOrSumByGeneric (isNullable, fq_double, fq_single, fq_decimal, fq_int32, fq_int64, fe_double, fe_single, fe_decimal, fe_int32, fe_int64, FE) =
        let (cq_double, mq_double), (ce_double, me_double) = MakersCallers2 fq_double fe_double
        let (cq_single, mq_single), (ce_single, me_single) = MakersCallers2 fq_single fe_single
        let (cq_decimal, mq_decimal), (ce_decimal, me_decimal) = MakersCallers2 fq_decimal fe_decimal
        let (cq_int32, mq_int32), (ce_int32, me_int32) = MakersCallers2 fq_int32 fe_int32
        let (cq_int64, mq_int64), (ce_int64, me_int64) = MakersCallers2 fq_int64 fe_int64
        // The F# implementation is an instance method on QueryBuilder
        let (CE, ME) = MakersCallersInstance FE
        let failDueToUnsupportedInputTypeInSumByOrAverageBy() = invalidOp (SR.GetString(SR.failDueToUnsupportedInputTypeInSumByOrAverageBy))

        let Make (qb: Expr, isIQ, src: Expr, v: Var, res: Expr) =
            let srcItemTy = v.Type
            let resTy = res.Type
            let resTyNoNullable =
                if isNullable then
                    assert resTy.IsGenericType
                    assert (resTy.GetGenericTypeDefinition() = typedefof<Nullable<int>>)
                    resTy.GetGenericArguments().[0]
                else
                    resTy
            let selector = FuncExprToDelegateExpr (srcItemTy, resTy, v, res)
            if isIQ then
                let selector = MakeImplicitExpressionConversion selector
                let maker =
                    match resTyNoNullable with
                    | ty when ty = typeof<double>  -> mq_double
                    | ty when ty = typeof<single>  -> mq_single
                    | ty when ty = typeof<decimal> -> mq_decimal
                    | ty when ty = typeof<int32>   -> mq_int32
                    | ty when ty = typeof<int64>   -> mq_int64
                    | _ -> failDueToUnsupportedInputTypeInSumByOrAverageBy()
                maker ([srcItemTy], [src; selector])
            else
                // Try to dynamically invoke a LINQ method if one exists, since these may be optimized over arrays etc.
                match resTyNoNullable with
                | ty when ty = typeof<double>  -> me_double ([srcItemTy], [src; selector])
                | ty when ty = typeof<single>  -> me_single ([srcItemTy], [src; selector])
                | ty when ty = typeof<decimal> -> me_decimal ([srcItemTy], [src; selector])
                | ty when ty = typeof<int32>   -> me_int32 ([srcItemTy], [src; selector])
                | ty when ty = typeof<int64>   -> me_int64 ([srcItemTy], [src; selector])
                | _ ->
                    // The F# implementation needs a QuerySource as a parameter.
                    let qTy = typeof<IEnumerable>
                    let ctor = typedefof<QuerySource<_, _>>.MakeGenericType([|srcItemTy; qTy|]).GetConstructors().[0]
                    let src = Expr.NewObject (ctor, [src])
                    // The F# implementation needs an FSharpFunc as a parameter.
                    let selector = Expr.Lambda (v, res)
                    ME (qb, [srcItemTy; qTy; resTyNoNullable], [src; selector])

        let Call (qb:obj, isIQ, srcItemTy: Type, resTyNoNullable: Type, src:obj, resTy: Type, v: Var, res: Expr) =
            if isIQ then
                let selector = FuncExprToLinqFunc2Expression (srcItemTy, resTy, v, res)
                let caller =
                    match resTyNoNullable with
                    | ty when ty = typeof<double>  -> cq_double
                    | ty when ty = typeof<single>  -> cq_single
                    | ty when ty = typeof<decimal> -> cq_decimal
                    | ty when ty = typeof<int32>   -> cq_int32
                    | ty when ty = typeof<int64>   -> cq_int64
                    | _ -> failDueToUnsupportedInputTypeInSumByOrAverageBy()
                caller ([srcItemTy], [src; box selector]) : obj
            else
                // Try to dynamically invoke a LINQ method if one exists, since these may be optimized over arrays etc.
                let linqMethOpt =
                    match resTyNoNullable with
                    | ty when ty = typeof<double>  -> Some ce_double
                    | ty when ty = typeof<single>  -> Some ce_single
                    | ty when ty = typeof<decimal> -> Some ce_decimal
                    | ty when ty = typeof<int32>   -> Some ce_int32
                    | ty when ty = typeof<int64>   -> Some ce_int64
                    | _ -> None
                match linqMethOpt with
                | Some ce ->
                    // A LINQ method needs a Delegate as a parameter
                    let selector = FuncExprToLinqFunc2 (srcItemTy, resTy, v, res)
                    ce ([srcItemTy], [src; selector])
                | None ->
                    // The F# implementation needs a QuerySource as a parameter.
                    let qTy = typeof<IEnumerable>
                    let ctor = typedefof<QuerySource<_, _>>.MakeGenericType([|srcItemTy; qTy|]).GetConstructors().[0]
                    let srcE =
                        try
                           ctor.Invoke [|src|]
                        with :? System.Reflection.TargetInvocationException as exn ->
                           raise exn.InnerException

                    // The F# implementation needs an FSharpFunc as a parameter.
                    let selectorE = Expr.Lambda (v, res) |> LeafExpressionConverter.EvaluateQuotation
                    CE (qb, [srcItemTy; qTy; resTy], [srcE; selectorE])
        Make, Call

    let MakeAverageBy, CallAverageBy =
        let FQ_double = methodhandleof (fun (x, y: Expression<Func<_, double>>) -> System.Linq.Queryable.Average(x, y))
        let FQ_single = methodhandleof (fun (x, y: Expression<Func<_, single>>) -> System.Linq.Queryable.Average(x, y))
        let FQ_decimal = methodhandleof (fun (x, y: Expression<Func<_, decimal>>) -> System.Linq.Queryable.Average(x, y))
        let FQ_int32 = methodhandleof (fun (x, y: Expression<Func<_, int32>>) -> System.Linq.Queryable.Average(x, y))
        let FQ_int64 = methodhandleof (fun (x, y: Expression<Func<_, int64>>) -> System.Linq.Queryable.Average(x, y))
        let FE_double = methodhandleof (fun (x, y: Func<_, double>) -> Enumerable.Average(x, y))
        let FE_single = methodhandleof (fun (x, y: Func<_, single>) -> Enumerable.Average(x, y))
        let FE_decimal = methodhandleof (fun (x, y: Func<_, decimal>) -> Enumerable.Average(x, y))
        let FE_int32 = methodhandleof (fun (x, y: Func<_, int32>) -> Enumerable.Average(x, y))
        let FE_int64 = methodhandleof (fun (x, y: Func<_, int64>) -> Enumerable.Average(x, y))
        let FE = methodhandleof (fun (query:QueryBuilder, arg1: QuerySource<_, _>, arg2:_->double) -> query.AverageBy(arg1, arg2))
        MakeOrCallAverageByOrSumByGeneric (false, FQ_double, FQ_single, FQ_decimal, FQ_int32, FQ_int64, FE_double, FE_single, FE_decimal, FE_int32, FE_int64, FE)

    let MakeAverageByNullable, CallAverageByNullable =
        let FQ_double = methodhandleof (fun (x, y: Expression<Func<_, Nullable<double>>>) -> System.Linq.Queryable.Average(x, y))
        let FQ_single = methodhandleof (fun (x, y: Expression<Func<_, Nullable<single>>>) -> System.Linq.Queryable.Average(x, y))
        let FQ_decimal = methodhandleof (fun (x, y: Expression<Func<_, Nullable<decimal>>>) -> System.Linq.Queryable.Average(x, y))
        let FQ_int32 = methodhandleof (fun (x, y: Expression<Func<_, Nullable<int32>>>) -> System.Linq.Queryable.Average(x, y))
        let FQ_int64 = methodhandleof (fun (x, y: Expression<Func<_, Nullable<int64>>>) -> System.Linq.Queryable.Average(x, y))
        let FE_double = methodhandleof (fun (x, y: Func<_, Nullable<double>>) -> Enumerable.Average(x, y))
        let FE_single = methodhandleof (fun (x, y: Func<_, Nullable<single>>) -> Enumerable.Average(x, y))
        let FE_decimal = methodhandleof (fun (x, y: Func<_, Nullable<decimal>>) -> Enumerable.Average(x, y))
        let FE_int32 = methodhandleof (fun (x, y: Func<_, Nullable<int32>>) -> Enumerable.Average(x, y))
        let FE_int64 = methodhandleof (fun (x, y: Func<_, Nullable<int64>>) -> Enumerable.Average(x, y))
        let FE = methodhandleof (fun (query:QueryBuilder, arg1: QuerySource<_, _>, arg2:_->Nullable<double>) -> query.AverageByNullable(arg1, arg2))
        MakeOrCallAverageByOrSumByGeneric (true, FQ_double, FQ_single, FQ_decimal, FQ_int32, FQ_int64, FE_double, FE_single, FE_decimal, FE_int32, FE_int64, FE)


    let MakeSumBy, CallSumBy =
        let FQ_double = methodhandleof (fun (x, y: Expression<Func<_, double>>) -> System.Linq.Queryable.Sum(x, y))
        let FQ_single = methodhandleof (fun (x, y: Expression<Func<_, single>>) -> System.Linq.Queryable.Sum(x, y))
        let FQ_decimal = methodhandleof (fun (x, y: Expression<Func<_, decimal>>) -> System.Linq.Queryable.Sum(x, y))
        let FQ_int32 = methodhandleof (fun (x, y: Expression<Func<_, int32>>) -> System.Linq.Queryable.Sum(x, y))
        let FQ_int64 = methodhandleof (fun (x, y: Expression<Func<_, int64>>) -> System.Linq.Queryable.Sum(x, y))
        let FE_double = methodhandleof (fun (x, y: Func<_, double>) -> Enumerable.Sum(x, y))
        let FE_single = methodhandleof (fun (x, y: Func<_, single>) -> Enumerable.Sum(x, y))
        let FE_decimal = methodhandleof (fun (x, y: Func<_, decimal>) -> Enumerable.Sum(x, y))
        let FE_int32 = methodhandleof (fun (x, y: Func<_, int32>) -> Enumerable.Sum(x, y))
        let FE_int64 = methodhandleof (fun (x, y: Func<_, int64>) -> Enumerable.Sum(x, y))
        let FE = methodhandleof (fun (query:QueryBuilder, arg1: QuerySource<_, _>, arg2:_->double) -> query.SumBy(arg1, arg2))
        MakeOrCallAverageByOrSumByGeneric (false, FQ_double, FQ_single, FQ_decimal, FQ_int32, FQ_int64, FE_double, FE_single, FE_decimal, FE_int32, FE_int64, FE)

    let MakeSumByNullable, CallSumByNullable =
        let FQ_double = methodhandleof (fun (x, y: Expression<Func<_, Nullable<double>>>) -> System.Linq.Queryable.Sum(x, y))
        let FQ_single = methodhandleof (fun (x, y: Expression<Func<_, Nullable<single>>>) -> System.Linq.Queryable.Sum(x, y))
        let FQ_decimal = methodhandleof (fun (x, y: Expression<Func<_, Nullable<decimal>>>) -> System.Linq.Queryable.Sum(x, y))
        let FQ_int32 = methodhandleof (fun (x, y: Expression<Func<_, Nullable<int32>>>) -> System.Linq.Queryable.Sum(x, y))
        let FQ_int64 = methodhandleof (fun (x, y: Expression<Func<_, Nullable<int64>>>) -> System.Linq.Queryable.Sum(x, y))
        let FE_double = methodhandleof (fun (x, y: Func<_, Nullable<double>>) -> Enumerable.Sum(x, y))
        let FE_single = methodhandleof (fun (x, y: Func<_, Nullable<single>>) -> Enumerable.Sum(x, y))
        let FE_decimal = methodhandleof (fun (x, y: Func<_, Nullable<decimal>>) -> Enumerable.Sum(x, y))
        let FE_int32 = methodhandleof (fun (x, y: Func<_, Nullable<int32>>) -> Enumerable.Sum(x, y))
        let FE_int64 = methodhandleof (fun (x, y: Func<_, Nullable<int64>>) -> Enumerable.Sum(x, y))
        let FE = methodhandleof (fun (query:QueryBuilder, arg1: QuerySource<_, _>, arg2:_->Nullable<double>) -> query.SumByNullable(arg1, arg2))
        MakeOrCallAverageByOrSumByGeneric (true, FQ_double, FQ_single, FQ_decimal, FQ_int32, FQ_int64, FE_double, FE_single, FE_decimal, FE_int32, FE_int64, FE)

    let MakeOrCallSimpleOp FQ FE =
        let (CQ, MQ), (CE, ME) = MakersCallers2 FQ FE
        let Make (isIQ, srcItemTy, src: Expr) =
            if isIQ then
                MQ ([srcItemTy], [src])
            else
                ME ([srcItemTy], [src])
        let Call (isIQ, srcItemTy, src) =
            (if isIQ then CQ else CE) ([srcItemTy], [src])
        Make, Call

    let MakeFirst, CallFirst = MakeOrCallSimpleOp (methodhandleof (fun x -> System.Linq.Queryable.First x)) (methodhandleof (fun x -> Enumerable.First x))

    let MakeFirstOrDefault, CallFirstOrDefault = MakeOrCallSimpleOp (methodhandleof (fun x -> System.Linq.Queryable.FirstOrDefault x)) (methodhandleof (fun x -> Enumerable.FirstOrDefault x))

    let MakeLast, CallLast = MakeOrCallSimpleOp (methodhandleof (fun x -> System.Linq.Queryable.Last x)) (methodhandleof (fun x -> Enumerable.Last x))

    let MakeLastOrDefault, CallLastOrDefault = MakeOrCallSimpleOp (methodhandleof (fun x -> System.Linq.Queryable.LastOrDefault x)) (methodhandleof (fun x -> Enumerable.LastOrDefault x))

    let MakeSingle, CallSingle = MakeOrCallSimpleOp (methodhandleof (fun x -> System.Linq.Queryable.Single x)) (methodhandleof (fun x -> Enumerable.Single x))

    let MakeSingleOrDefault, CallSingleOrDefault = MakeOrCallSimpleOp (methodhandleof (fun x -> System.Linq.Queryable.SingleOrDefault x)) (methodhandleof (fun x -> Enumerable.SingleOrDefault x))

    let MakeCount, CallCount = MakeOrCallSimpleOp (methodhandleof (fun x -> System.Linq.Queryable.Count x)) (methodhandleof (fun x -> Enumerable.Count x))

    let MakeDefaultIfEmpty = MakeGenericStaticMethod (methodhandleof (fun x -> Enumerable.DefaultIfEmpty x))

    /// Indicates if we can eliminate redundant 'Select(x=>x)' nodes
    type CanEliminate =
        /// Inside a query construct, can eliminate redundant 'Select'
        | Yes = 0
        /// At the very outer of a query or nested query - can't eliminate redundant 'Select'
        | No = 1

    let MakeSelect =
        let FQ = MakeGenericStaticMethod (methodhandleof (fun (x, y: Expression<Func<_, _>>) -> System.Linq.Queryable.Select(x, y)))
        let FE = MakeGenericStaticMethod (methodhandleof (fun (x, y: Func<_, _>) -> Enumerable.Select(x, y)))
        fun (canElim, isIQ, src: Expr, v: Var, f: Expr) ->

            // Eliminate degenerate 'Select(x => x)', except for the very outer-most cases
            match f with
            |  Patterns.Var v2 when v = v2 && canElim = CanEliminate.Yes -> src
            | _ ->
            let srcItemTy = v.Type
            let targetTy = f.Type
            let selector = Expr.NewDelegate (MakeQueryFuncTy(srcItemTy, targetTy), [v], f)

            if isIQ then
                let selector = MakeImplicitExpressionConversion selector
                FQ ([srcItemTy; targetTy], [src; selector])
            else
                //printfn "found FE"
                FE ([srcItemTy; targetTy], [src; selector])

    let MakeAppend =
        let FQ = MakeGenericStaticMethod (methodhandleof (fun (x, y) -> System.Linq.Queryable.Concat(x, y)))
        let FE = MakeGenericStaticMethod (methodhandleof (fun (x, y) -> Enumerable.Concat(x, y)))
        fun (isIQ, srcItemTy, src1: Expr, src2: Expr) ->
            if isIQ then
               FQ ([srcItemTy], [src1; src2])
            else
               FE ([srcItemTy], [src1; src2])

    let MakeAsQueryable =
        let F = MakeGenericStaticMethod (methodhandleof (fun (x:seq<_>) -> System.Linq.Queryable.AsQueryable x))
        fun (ty, src) ->
            F ([ty], [src])

    let MakeEnumerableEmpty =
        let F = MakeGenericStaticMethod (methodhandleof (fun _x -> Enumerable.Empty()))
        fun ty ->
            F ([ty], [])

    let MakeEmpty =
        fun ty ->
            MakeAsQueryable (ty, MakeEnumerableEmpty ty)

    let MakeSelectMany =
        let FQ = MakeGenericStaticMethod (methodhandleof (fun (x: IQueryable<_>, y: Expression<Func<_, _>>, z: Expression<Func<_, _, _>>) -> System.Linq.Queryable.SelectMany(x, y, z)))
        let FE = MakeGenericStaticMethod (methodhandleof (fun (x: IEnumerable<_>, y: Func<_, _>, z: Func<_, _, _>) -> Enumerable.SelectMany(x, y, z)))
        fun (isIQ, resTy: Type, src: Expr, srcItemVar: Var, interimSelectorBody: Expr, interimVar: Var, targetSelectorBody: Expr) ->
            let srcItemTy = srcItemVar.Type
            let interimTy = interimVar.Type
            let interimSelector = Expr.NewDelegate (MakeQueryFuncTy(srcItemTy, MakeIEnumerableTy interimTy), [srcItemVar], interimSelectorBody)
            let targetSelector = Expr.NewDelegate (MakeQueryFunc2Ty(srcItemTy, interimTy, resTy), [srcItemVar; interimVar], targetSelectorBody)

            if isIQ then
                let interimSelector = MakeImplicitExpressionConversion interimSelector
                let targetSelector = MakeImplicitExpressionConversion targetSelector
                FQ ([srcItemTy; interimTy; resTy], [src; interimSelector; targetSelector])
            else
                FE ([srcItemTy; interimTy; resTy], [src; interimSelector; targetSelector])

    let MakeWhere =
        let FQ = MakeGenericStaticMethod (methodhandleof (fun (x, y: Expression<Func<_, _>>) -> System.Linq.Queryable.Where(x, y)))
        let FE = MakeGenericStaticMethod (methodhandleof (fun (x, y: Func<_, _>) -> Enumerable.Where(x, y)))
        fun (isIQ, src: Expr, v: Var, f) ->
            let selector = Expr.NewDelegate (MakeQueryFuncTy(v.Type, typeof<bool>), [v], f)

            if isIQ then
                let selector = MakeImplicitExpressionConversion selector
                FQ ([v.Type], [src; selector])
            else
                FE ([v.Type], [src; selector])

    let MakeOrderByOrThenBy FQ FE =
        fun (isIQ, src: Expr, v: Var, keySelector: Expr) ->
            let srcItemTy = v.Type
            let keyItemTy = keySelector.Type
            let selector = Expr.NewDelegate (MakeQueryFuncTy(srcItemTy, keyItemTy), [v], keySelector)
            if isIQ then
                let selector = MakeImplicitExpressionConversion selector
                FQ ([srcItemTy; keyItemTy], [src; selector])
            else
                FE ([srcItemTy; keyItemTy], [src; selector])

    let MakeOrderBy =
        let FQ = MakeGenericStaticMethod (methodhandleof (fun (x, y: Expression<Func<_, _>>) -> System.Linq.Queryable.OrderBy(x, y)))
        let FE = MakeGenericStaticMethod (methodhandleof (fun (x, y: Func<_, _>) -> Enumerable.OrderBy(x, y)))
        MakeOrderByOrThenBy FQ FE

    let MakeOrderByDescending =
        let FQ = MakeGenericStaticMethod (methodhandleof (fun (x, y: Expression<Func<_, _>>) -> System.Linq.Queryable.OrderByDescending(x, y)))
        let FE = MakeGenericStaticMethod (methodhandleof (fun (x, y: Func<_, _>) -> Enumerable.OrderByDescending(x, y)))
        MakeOrderByOrThenBy FQ FE

    let MakeThenBy =
        let FQ = MakeGenericStaticMethod (methodhandleof (fun (x, y: Expression<Func<_, _>>) -> System.Linq.Queryable.ThenBy(x, y)))
        let FE = MakeGenericStaticMethod (methodhandleof (fun (x, y: Func<_, _>) -> Enumerable.ThenBy(x, y)))
        MakeOrderByOrThenBy FQ FE

    let MakeThenByDescending =
        let FQ = MakeGenericStaticMethod (methodhandleof (fun (x, y: Expression<Func<_, _>>) -> System.Linq.Queryable.ThenByDescending(x, y)))
        let FE = MakeGenericStaticMethod (methodhandleof (fun (x, y: Func<_, _>) -> Enumerable.ThenByDescending(x, y)))
        MakeOrderByOrThenBy FQ FE

    // The keyItemTy differentiates these
    let MakeOrderByNullable = MakeOrderBy

    let MakeOrderByNullableDescending = MakeOrderByDescending

    let MakeThenByNullable = MakeThenBy

    let MakeThenByNullableDescending = MakeThenByDescending

    let GenMakeSkipWhileOrTakeWhile FQ FE =
        let FQ = MakeGenericStaticMethod  FQ
        let FE = MakeGenericStaticMethod  FE
        fun (isIQ, src: Expr, v: Var, predicate) ->
            let srcItemTy = v.Type
            let selector = Expr.NewDelegate (MakeQueryFuncTy(srcItemTy, boolTy), [v], predicate)
            if isIQ then
                let selector = MakeImplicitExpressionConversion selector
                FQ ([srcItemTy], [src; selector])
            else
                FE ([srcItemTy], [src; selector])

    let MakeSkipOrTake FQ FE =
        let FQ = MakeGenericStaticMethod  FQ
        let FE = MakeGenericStaticMethod  FE
        fun (isIQ, srcItemTy, src: Expr, count) ->
            if isIQ then
                FQ ([srcItemTy], [src; count])
            else
                FE ([srcItemTy], [src; count])

    let MakeSkip =
        MakeSkipOrTake
            (methodhandleof (fun (x, y) -> System.Linq.Queryable.Skip (x, y)))
            (methodhandleof (fun (x, y) -> Enumerable.Skip (x, y)))

    let MakeTake =
        MakeSkipOrTake
            (methodhandleof (fun (x, y) -> System.Linq.Queryable.Take (x, y)))
            (methodhandleof (fun (x, y) -> Enumerable.Take (x, y)))

    let MakeSkipWhile =
        GenMakeSkipWhileOrTakeWhile
            (methodhandleof (fun (x, y: Expression<Func<_, _>>) -> System.Linq.Queryable.SkipWhile(x, y)))
            (methodhandleof (fun (x, y: Func<_, _>) -> Enumerable.SkipWhile(x, y)))

    let MakeTakeWhile =
        GenMakeSkipWhileOrTakeWhile
            (methodhandleof (fun (x, y: Expression<Func<_, _>>) -> System.Linq.Queryable.TakeWhile(x, y)))
            (methodhandleof (fun (x, y: Func<_, _>) -> Enumerable.TakeWhile(x, y)))

    let MakeDistinct =
        let FQ = MakeGenericStaticMethod (methodhandleof (fun x -> System.Linq.Queryable.Distinct x))
        let FE = MakeGenericStaticMethod (methodhandleof (fun x -> Enumerable.Distinct x))
        fun (isIQ, srcItemTy, src: Expr) ->
            if isIQ then
                FQ ([srcItemTy], [src])
            else
                FE ([srcItemTy], [src])

    let MakeGroupBy =
        let FQ = MakeGenericStaticMethod (methodhandleof (fun (x, y: Expression<Func<_, _>>) -> System.Linq.Queryable.GroupBy(x, y)))
        let FE = MakeGenericStaticMethod (methodhandleof (fun (x, y: Func<_, _>) -> Enumerable.GroupBy(x, y)))
        fun (isIQ, src: Expr, v: Var, keySelector: Expr) ->
            let srcItemTy = v.Type
            let keyTy = keySelector.Type
            let keySelector = Expr.NewDelegate (MakeQueryFuncTy(srcItemTy, keyTy), [v], keySelector)

            if isIQ then
                let keySelector = MakeImplicitExpressionConversion keySelector
                FQ ([srcItemTy; keyTy], [src; keySelector])
            else
                FE ([srcItemTy; keyTy], [src; keySelector])

    let MakeGroupValBy =
        let FQ = MakeGenericStaticMethod (methodhandleof (fun (x, y: Expression<Func<_, _>>, z: Expression<Func<_, _>>) -> System.Linq.Queryable.GroupBy(x, y, z)))
        let FE = MakeGenericStaticMethod (methodhandleof (fun (x, y: Func<_, _>, z: Func<_, _>) -> Enumerable.GroupBy(x, y, z)))
        fun (isIQ, srcItemTy, keyTy, elementTy, src: Expr, v1, keySelector, v2, elementSelector) ->
            let keySelector = Expr.NewDelegate (MakeQueryFuncTy(srcItemTy, keyTy), [v1], keySelector)
            let elementSelector = Expr.NewDelegate (MakeQueryFuncTy(srcItemTy, elementTy), [v2], elementSelector)

            if isIQ then
                let keySelector = MakeImplicitExpressionConversion keySelector
                let elementSelector = MakeImplicitExpressionConversion elementSelector
                FQ ([srcItemTy; keyTy; elementTy], [src; keySelector; elementSelector])
            else
                FE ([srcItemTy; keyTy; elementTy], [src; keySelector; elementSelector])

    let MakeJoin =
        let FQ = MakeGenericStaticMethod (methodhandleof (fun (a1, a2, a3: Expression<Func<_, _>>, a4: Expression<Func<_, _>>, a5: Expression<Func<_, _, _>>) -> System.Linq.Queryable.Join(a1, a2, a3, a4, a5)))
        let FE = MakeGenericStaticMethod (methodhandleof (fun (a1, a2, a3: Func<_, _>, a4: Func<_, _>, a5: Func<_, _, _>) -> Enumerable.Join(a1, a2, a3, a4, a5)))
        fun (isIQ, outerSourceTy, innerSourceTy, keyTy, resTy, outerSource: Expr, innerSource: Expr, outerKeyVar, outerKeySelector, innerKeyVar, innerKeySelector, outerResultKeyVar, innerResultKeyVar, elementSelector) ->
            let outerKeySelector = Expr.NewDelegate (MakeQueryFuncTy(outerSourceTy, keyTy), [outerKeyVar], outerKeySelector)
            let innerKeySelector = Expr.NewDelegate (MakeQueryFuncTy(innerSourceTy, keyTy), [innerKeyVar], innerKeySelector)
            let elementSelector = Expr.NewDelegate (MakeQueryFunc2Ty(outerSourceTy, innerSourceTy, resTy), [outerResultKeyVar; innerResultKeyVar], elementSelector)

            if isIQ then
                let outerKeySelector = MakeImplicitExpressionConversion outerKeySelector
                let innerKeySelector = MakeImplicitExpressionConversion innerKeySelector
                let elementSelector = MakeImplicitExpressionConversion elementSelector
                FQ ([outerSourceTy; innerSourceTy; keyTy; resTy], [outerSource; innerSource; outerKeySelector; innerKeySelector; elementSelector])
            else
                FE ([outerSourceTy; innerSourceTy; keyTy; resTy], [outerSource; innerSource; outerKeySelector; innerKeySelector; elementSelector])

    let MakeGroupJoin =
        let FQ = MakeGenericStaticMethod (methodhandleof (fun (a1, a2, a3: Expression<Func<_, _>>, a4: Expression<Func<_, _>>, a5: Expression<Func<_, _, _>>) -> System.Linq.Queryable.GroupJoin(a1, a2, a3, a4, a5)))
        let FE = MakeGenericStaticMethod (methodhandleof (fun (a1, a2, a3: Func<_, _>, a4: Func<_, _>, a5: Func<_, _, _>) -> Enumerable.GroupJoin(a1, a2, a3, a4, a5)))
        fun (isIQ, outerSourceTy, innerSourceTy, keyTy, resTy, outerSource: Expr, innerSource: Expr, outerKeyVar, outerKeySelector, innerKeyVar, innerKeySelector, outerResultKeyVar, innerResultGroupVar, elementSelector) ->
            let outerKeySelector = Expr.NewDelegate (MakeQueryFuncTy(outerSourceTy, keyTy), [outerKeyVar], outerKeySelector)
            let innerKeySelector = Expr.NewDelegate (MakeQueryFuncTy(innerSourceTy, keyTy), [innerKeyVar], innerKeySelector)
            let elementSelector = Expr.NewDelegate (MakeQueryFunc2Ty(outerSourceTy, MakeIEnumerableTy innerSourceTy, resTy), [outerResultKeyVar; innerResultGroupVar], elementSelector)
            if isIQ then
                let outerKeySelector = MakeImplicitExpressionConversion outerKeySelector
                let innerKeySelector = MakeImplicitExpressionConversion innerKeySelector
                let elementSelector = MakeImplicitExpressionConversion elementSelector
                FQ ([outerSourceTy; innerSourceTy; keyTy; resTy], [outerSource; innerSource; outerKeySelector; innerKeySelector; elementSelector])
            else
                FE ([outerSourceTy; innerSourceTy; keyTy; resTy], [outerSource; innerSource; outerKeySelector; innerKeySelector; elementSelector])

    let RewriteExpr f (q: Expr) =
        let rec walk (p: Expr) =
            match f walk p with
            | Some r -> r
            | None ->
            match p with
            | ExprShape.ShapeCombination(comb, args) -> ExprShape.RebuildShapeCombination(comb, List.map walk args)
            | ExprShape.ShapeLambda(v, body) -> Expr.Lambda (v, walk body)
            | ExprShape.ShapeVar _ -> p
        walk q

    let (|LetExprReduction|_|) (p: Expr) =
        match p with
        | Let(v, e, body) ->
            let body = body.Substitute (fun v2 -> if v = v2 then Some e else None)
            Some body

        | _ -> None

    let (|MacroReduction|_|) (p: Expr) =

        match p with
        | Applications(Lambdas(vs, body), args) 
            when vs.Length = args.Length 
                 && (vs, args) ||> List.forall2 (fun vs args -> vs.Length = args.Length) ->
            let tab = Map.ofSeq (List.concat (List.map2 List.zip vs args))
            let body = body.Substitute tab.TryFind
            Some body

        // Macro
        | PropertyGet(None, Getter(MethodWithReflectedDefinition body), []) ->
            Some body

        // Macro
        | Call(None, MethodWithReflectedDefinition(Lambdas(vs, body)), args) ->
            let tab =
                (vs, args)
                ||> List.map2 (fun vs arg -> 
                    match vs, arg with
                    | [v], arg -> [(v, arg)]
                    | vs, NewTuple args -> List.zip vs args 
                    | _ -> List.zip vs [arg])
                |> List.concat |> Map.ofSeq
            let body = body.Substitute tab.TryFind
            Some body

        // Macro - eliminate 'let'.
        //
        // Always eliminate these:
        //    - function definitions
        //
        // Always eliminate these, which are representations introduced by F# quotations:
        //    - let v1 = v2
        //    - let v1 = tupledArg.Item*
        //    - let copyOfStruct = ...

        | Let(v, e, body) when (match e with
                                | Lambda _ -> true
                                | Var _ -> true
                                | TupleGet(Var tv, _) when tv.Name = "tupledArg" -> true
                                | _ when v.Name = "copyOfStruct" && v.Type.IsValueType -> true
                                | _ -> false) ->
            let body = body.Substitute (fun v2 -> if v = v2 then Some e else None)
            Some body

        | _ -> None


    /// Expand 'let' and other 'macro' definitions in leaf expressions, because LINQ can't cope with them
    let MacroExpand q =
        q |> RewriteExpr  (fun walk p ->
            match p with
            // Macro reduction - eliminate any 'let' in leaf expressions
            | MacroReduction reduced -> Some (walk reduced)
            | _ -> None)

    let (|CallQueryBuilderRunQueryable|_|) : Quotations.Expr -> _ = (|SpecificCallToMethod|_|) (methodhandleof (fun (b :QueryBuilder, v) -> b.Run v))

    let (|CallQueryBuilderRunValue|_|) : Quotations.Expr -> _ = (|SpecificCallToMethod|_|) (methodhandleof (fun (b : QueryBuilder, v: Expr<'a>) -> b.Run v) : 'a)

    let (|CallQueryBuilderRunEnumerable|_|) : Quotations.Expr -> _ = (|SpecificCallToMethod|_|) (methodhandleof (fun (b : QueryBuilder, v: Expr<QuerySource<_, IEnumerable>> ) -> b.Run v))

    let (|CallQueryBuilderFor|_|) : Quotations.Expr -> _ = (|SpecificCallToMethod|_|) (methodhandleof (fun (b:QueryBuilder, source: QuerySource<int, _>, body) -> b.For(source, body)))

    let (|CallQueryBuilderYield|_|) : Quotations.Expr -> _ = (|SpecificCall1|_|) (methodhandleof (fun (b:QueryBuilder, value) -> b.Yield value))

    let (|CallQueryBuilderYieldFrom|_|) : Quotations.Expr -> _ = (|SpecificCallToMethod|_|) (methodhandleof (fun (b:QueryBuilder, values) -> b.YieldFrom values))

    let (|CallQueryBuilderZero|_|) : Quotations.Expr -> _ = (|SpecificCallToMethod|_|) (methodhandleof (fun (b:QueryBuilder) -> b.Zero()))

    let (|CallQueryBuilderSourceIQueryable|_|) : Quotations.Expr -> _ = (|SpecificCall1|_|) (methodhandleof (fun (b:QueryBuilder, value: IQueryable<_>) -> b.Source value))

    let (|CallQueryBuilderSourceIEnumerable|_|) : Quotations.Expr -> _ = (|SpecificCall1|_|) (methodhandleof (fun (b:QueryBuilder, value: IEnumerable<_>) -> b.Source value))

    let (|CallSortBy|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.SortBy(arg1, arg2)))

    let (|CallSortByDescending|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.SortByDescending(arg1, arg2)))

    let (|CallThenBy|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.ThenBy(arg1, arg2) ))

    let (|CallThenByDescending|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.ThenByDescending(arg1, arg2)))

    let (|CallSortByNullable|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.SortByNullable(arg1, arg2)))

    let (|CallSortByNullableDescending|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.SortByNullableDescending(arg1, arg2)))

    let (|CallThenByNullable|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.ThenByNullable(arg1, arg2)))

    let (|CallThenByNullableDescending|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.ThenByNullableDescending(arg1, arg2)))

    let (|CallGroupBy|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.GroupBy(arg1, arg2)))

    let (|CallGroupValBy|_|) = (|SpecificCall3|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2, arg3) -> query.GroupValBy(arg1, arg2, arg3)))

    let (|CallMinBy|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.MinBy(arg1, arg2)))

    let (|CallMaxBy|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.MaxBy(arg1, arg2)))

    let (|CallMinByNullable|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.MinByNullable(arg1, arg2)))

    let (|CallMaxByNullable|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.MaxByNullable(arg1, arg2)))

    let (|CallWhere|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.Where(arg1, arg2)))

    let (|CallHeadOrDefault|_|) = (|SpecificCall1|_|) (methodhandleof (fun (query:QueryBuilder, arg1) -> query.HeadOrDefault arg1))

    let (|CallLast|_|) = (|SpecificCall1|_|) (methodhandleof (fun (query:QueryBuilder, arg1) -> query.Last arg1))

    let (|CallLastOrDefault|_|) = (|SpecificCall1|_|) (methodhandleof (fun (query:QueryBuilder, arg1) -> query.LastOrDefault arg1))

    let (|CallExactlyOne|_|) = (|SpecificCall1|_|) (methodhandleof (fun (query:QueryBuilder, arg1) -> query.ExactlyOne arg1))

    let (|CallExactlyOneOrDefault|_|) = (|SpecificCall1|_|) (methodhandleof (fun (query:QueryBuilder, source) -> query.ExactlyOneOrDefault source))

    let (|CallSelect|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.Select(arg1, arg2)))

    let (|CallExists|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.Exists(arg1, arg2)))

    let (|CallForAll|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.All(arg1, arg2)))

    let (|CallDistinct|_|) = (|SpecificCall1|_|) (methodhandleof (fun (query:QueryBuilder, keySelector) -> query.Distinct keySelector))

    let (|CallTake|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.Take(arg1, arg2)))

    let (|CallTakeWhile|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.TakeWhile(arg1, arg2)))

    let (|CallContains|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.Contains(arg1, arg2)))

    let (|CallNth|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.Nth(arg1, arg2)))

    let (|CallSkip|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.Skip(arg1, arg2)))

    let (|CallSkipWhile|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.SkipWhile(arg1, arg2)))

    let (|CallJoin|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2, arg3, arg4, arg5) -> query.Join(arg1, arg2, arg3, arg4, arg5)))

    let (|CallGroupJoin|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2, arg3, arg4, arg5) -> query.GroupJoin(arg1, arg2, arg3, arg4, arg5)))

    let (|CallLeftOuterJoin|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2, arg3, arg4, arg5) -> query.LeftOuterJoin(arg1, arg2, arg3, arg4, arg5)))

    let (|CallAverageBy|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1: QuerySource<double, _>, arg2:(double->double)) -> query.AverageBy(arg1, arg2)))

    let (|CallSumBy|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1: QuerySource<double, _>, arg2:(double->double)) -> query.SumBy(arg1, arg2)))

    let (|CallAverageByNullable|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1: QuerySource<double, _>, arg2:(double->Nullable<double>)) -> query.AverageByNullable(arg1, arg2)))

    let (|CallSumByNullable|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1: QuerySource<double, _>, arg2:(double->Nullable<double>)) -> query.SumByNullable(arg1, arg2)))

    let (|CallCount|_|) = (|SpecificCall1|_|) (methodhandleof (fun (query:QueryBuilder, arg1) -> query.Count arg1))

    let (|CallHead|_|) = (|SpecificCall1|_|) (methodhandleof (fun (query:QueryBuilder, arg1) -> query.Head arg1))

    let (|CallFind|_|) = (|SpecificCall2|_|) (methodhandleof (fun (query:QueryBuilder, arg1, arg2) -> query.Find(arg1, arg2)))

    let (|ZeroOnElseBranch|_|) = function
        // This is the shape for 'match e with ... -> ... | _ -> ()'
        | Patterns.Sequential(Patterns.Value(null, _), CallQueryBuilderZero _)
        // This is the shape for from 'if/then'
        | CallQueryBuilderZero _ ->  Some()
        | _ -> None

    /// Given an expression involving mutable tuples logically corresponding to a "yield" or "select" with the given
    /// immutable-to-mutable conversion information, convert it back to an expression involving immutable tuples or records.
    let rec ConvMutableToImmutable conv mutExpr =
        match conv with
        | TupleConv convs ->
            Expr.NewTuple (convs |> List.mapi (fun i conv -> ConvMutableToImmutable conv (AnonymousObjectGet (mutExpr, i))))
        | RecordConv (typ, convs) ->
            Expr.NewRecord (typ, convs |> List.mapi (fun i conv -> ConvMutableToImmutable conv (AnonymousObjectGet (mutExpr, i))))

        | SeqConv conv ->

            // At this point, we know the input is either an IQueryable or an IEnumerable.
            // If it is an IQueryable, we must return an IQueryable.
            let isIQ = IsIQueryableTy mutExpr.Type
            assert (IsIEnumerableTy mutExpr.Type || IsIQueryableTy mutExpr.Type)
            let mutElemTy = mutExpr.Type.GetGenericArguments().[0]
            let mutExpr = if isIQ then Expr.Coerce (mutExpr, MakeIEnumerableTy mutElemTy) else mutExpr
            // Generate "source.Select(fun v -> ...)" (remembering that Select is an extension member, i.e. static)
            let mutVar = new Var("v", mutElemTy)
            let mutToImmutConvExpr = ConvMutableToImmutable conv (Expr.Var mutVar)
            let immutExpr = MakeSelect (CanEliminate.Yes, false, mutExpr, mutVar, mutToImmutConvExpr)
            let immutElemTy = mutToImmutConvExpr.Type
            let immutExprCoerced = if isIQ then MakeAsQueryable(immutElemTy, immutExpr) else immutExpr
            immutExprCoerced

        | GroupingConv (immutKeyTy, immutElemTy, conv) ->

            assert (mutExpr.Type.GetGenericTypeDefinition() = typedefof<System.Linq.IGrouping<_, _>>)
            let mutElemTy = mutExpr.Type.GetGenericArguments().[1]
            let immutIGroupingTy = typedefof<IGrouping<_, _>>.MakeGenericType [| immutKeyTy; immutElemTy |]
            let immutGroupingTy = typedefof<Grouping<_, _>>.MakeGenericType [| immutKeyTy; immutElemTy |]
            // Generate "source.Select(fun v -> ...)" (remembering that Select is an extension member, i.e. static)
            let var = new Var("v", mutElemTy)
            let convExpr = ConvMutableToImmutable conv (Expr.Var var)

            // Construct an IGrouping
            let args =
              [ Expr.PropertyGet (mutExpr, mutExpr.Type.GetProperty "Key")
                MakeSelect(CanEliminate.Yes, false, mutExpr, var, convExpr) ]

            Expr.Coerce (Expr.NewObject (immutGroupingTy.GetConstructors().[0], args), immutIGroupingTy)

        | NoConv ->
            mutExpr

    /// Given the expressions for a function (fun immutConsumingVar -> immutConsumingExpr) operating over immutable tuple and record
    /// types, build the expressions for an equivalent function (fun mutConsumingVar -> mutConsumingExpr) which will operate over mutable data, where the given conversion
    /// data says how immutable types have been replaced by mutable types in the type of the input variable.
    ///
    /// For example, if 'conv' is NoConv, then the input function will be returned unchanged.
    ///
    /// If 'conv' is a TupleConv, then the input function will accept immutable tuples, and the output
    /// function will accept mutable tuples. In this case, the function is implemented by replacing
    /// uses of the immutConsumingVar in the body of immutConsumingExpr with a tuple expression built
    /// from the elements of mutConsumingVar, and then simplifying the overall result.
    let ConvertImmutableConsumerToMutableConsumer conv (immutConsumingVar: Var, immutConsumingExpr: Expr) : Var * Expr =
        match conv with
        | NoConv -> (immutConsumingVar, immutConsumingExpr)
        | _ ->
        let mutConsumingVarType = ConvImmutableTypeToMutableType conv immutConsumingVar.Type
        let mutConsumingVar = Var (immutConsumingVar.Name, mutConsumingVarType)

        let immutConsumingVarReplacementExpr = ConvMutableToImmutable conv (Expr.Var mutConsumingVar)
        let mutConsumingExprBeforeSimplification = immutConsumingExpr.Substitute (fun v -> if v = immutConsumingVar then Some immutConsumingVarReplacementExpr else None)

        let mutConsumingExpr = SimplifyConsumingExpr mutConsumingExprBeforeSimplification
        mutConsumingVar, mutConsumingExpr

    let (|AnyNestedQuery|_|) e =
        match e with
        | CallQueryBuilderRunValue (None, _, [_; QuoteTyped e ])
        | CallQueryBuilderRunEnumerable (None, _, [_; QuoteTyped e ])
        | CallQueryBuilderRunQueryable (Some _, _, [ QuoteTyped e ]) -> Some e
        | _ -> None

    let (|EnumerableNestedQuery|_|) e =
        match e with
        | CallQueryBuilderRunEnumerable (None, _, [_; QuoteTyped e ])
        | CallQueryBuilderRunQueryable (Some _, _, [ QuoteTyped e ]) -> Some e
        | _ -> None

    /// Represents the result of TransInner - either a normal expression, or something we're about to turn into
    /// a 'Select'. The 'Select' case can be eliminated if it is about to be the result of a SelectMany by
    /// changing
    ///     src.SelectMany(x => ix.Select(y => res))
    /// to
    ///     src.SelectMany(x => ix, (x, y) => res)
    [<NoComparison; NoEquality; RequireQualifiedAccess>]
    type TransInnerResult =
        | Select of CanEliminate * bool * TransInnerResult * Var * Expr
        | Other of Expr
        | Source of Expr

        static member MakeSelect (canElim, isQTy, mutSource, mutSelectorVar, mutSelectorBody) =
            // We can eliminate a Select if it is either selecting on a non-source or is being added in a inner position.
            let canElim =
                match mutSource with
                | TransInnerResult.Source _ -> canElim
                | _ -> CanEliminate.Yes
            // We eliminate the Select here to keep the information in 'mutSource' available, i.e. whether
            // the mutSource is a TransInnerResult.Source after elimination
            match mutSelectorBody with
            |  Patterns.Var v2 when mutSelectorVar = v2 && canElim = CanEliminate.Yes -> mutSource
            | _ -> Select(canElim, isQTy, mutSource, mutSelectorVar, mutSelectorBody)


    /// Commit the result of TransInner in the case where the result was not immediately inside a 'SelectMany'
    let rec CommitTransInnerResult c =
        match c with
        | TransInnerResult.Source res -> res
        | TransInnerResult.Other res -> res
        | TransInnerResult.Select(canElim, isQTy, mutSource, mutSelectorVar, mutSelectorBody) ->
            MakeSelect(canElim, isQTy, CommitTransInnerResult mutSource, mutSelectorVar, mutSelectorBody)

    /// Given a the inner of query expression in terms of query.For, query.Select, query.Yield, query.Where etc.,
    /// and including immutable tuples and immutable records, build an equivalent query expression
    /// in terms of LINQ operators, operating over mutable tuples. Return the conversion
    /// information for the immutable-to-mutable conversion performed so we can undo it where needed.
    ///
    /// Here 'inner' refers the the part of the query that produces a sequence of results.
    ///
    /// The output query will use either Queryable.* or Enumerable.* operators depending on whether
    /// the inputs to the queries have type IQueryable or IEnumerable.
    let rec TransInner canElim check (immutQuery: Expr) =
        //assert (IsIQueryableTy immutQuery.Type  || IsQuerySourceTy immutQuery.Type || IsIEnumerableTy immutQuery.Type)
        // printfn "TransInner: %A" tm
        match immutQuery with

        // Look through coercions, e.g. to IEnumerable
        | Coerce (expr, _ty) ->
            TransInner canElim check expr


        // Rewrite "for" into SelectMany. If the body of a "For" is nothing but Yield/IfThenElse,
        // then it can be rewritten to Select + Where.
        //
        // If the original body of the "for" in the text of the F# query expression uses "where" or
        // any other custom operator, then the body of the "for" as presented to the quotation
        // rewrite has had the custom operator translation mechanism applied. In this case, the
        // body of the "for" will simply contain "yield".

        | CallQueryBuilderFor (_, GenericArgs [_; qTy; immutResElemTy; _], [immutSource; Lambda(immutSelectorVar, immutSelector) ]) ->

            let mutSource, sourceConv = TransInner CanEliminate.Yes check immutSource

            // If the body of a "For" is nothing but Yield/IfThenElse/Where, then it can be fully rewritten away.
            let rec TransFor mutSource immutSelector =
                match immutSelector with

                // query.For (source, (fun selectorVar -> yield res)) @>
                //    ~~> TRANS(source.Select(selectorVar -> res)
                | CallQueryBuilderYield(_, _, immutSelectorBody) ->

                    let mutSelectorVar, mutSelectorBody = ConvertImmutableConsumerToMutableConsumer sourceConv (immutSelectorVar, MacroExpand immutSelectorBody)
                    let mutSelectorBody, selectorConv = ProduceMoreMutables TransInnerNoCheck mutSelectorBody
                    let mutSelectorBody = CleanupLeaf mutSelectorBody

                    TransInnerResult.MakeSelect (canElim, qTyIsIQueryable qTy, mutSource, mutSelectorVar, mutSelectorBody), selectorConv

                | LetExprReduction reduced ->
                    TransFor mutSource reduced

                | MacroReduction reduced ->
                    TransFor mutSource reduced

                // query.For (source, (fun selectorVar -> if g then selectorBody else query.Zero())) @>
                //    ~~> TRANS(query.For (source.Where(fun selectorVar -> g), (fun selectorVar -> selectorBody))
                | CallWhere (_, _, immutSelectorBody, Lambda(_, immutPredicateBody))
                | IfThenElse (immutPredicateBody, immutSelectorBody, ZeroOnElseBranch) ->

                    let mutSelectorVar, mutPredicateBody = ConvertImmutableConsumerToMutableConsumer sourceConv (immutSelectorVar, MacroExpand immutPredicateBody)
                    let mutSource = MakeWhere(qTyIsIQueryable qTy, CommitTransInnerResult mutSource, mutSelectorVar, mutPredicateBody)
                    TransFor (TransInnerResult.Other mutSource) immutSelectorBody

                // query.For (source, (fun selectorVar -> immutSelectorBody)) @>
                //    ~~> source.SelectMany(fun selectorVar -> immutSelectorBody)
                | immutSelectorBody ->

                    let mutSelectorVar, immutSelectorBody = ConvertImmutableConsumerToMutableConsumer sourceConv (immutSelectorVar, MacroExpand immutSelectorBody)
                    let (mutSelectorBodyInfo:TransInnerResult), selectorConv = TransInner CanEliminate.Yes check immutSelectorBody
                    let mutElemTy = ConvImmutableTypeToMutableType selectorConv immutResElemTy

                    /// Commit the result of TransInner in the case where the result is immediately inside a 'SelectMany'
                    let (mutInterimSelectorBodyPreCoerce: Expr), mutInterimVar, mutTargetSelector =
                        match mutSelectorBodyInfo with
                        | TransInnerResult.Select(_, _, mutInterimSelectorSource, mutInterimVar, mutTargetSelector) ->
                            CommitTransInnerResult mutInterimSelectorSource, mutInterimVar, mutTargetSelector
                        | _ ->
                            let mutInterimSelectorBody = CommitTransInnerResult mutSelectorBodyInfo
                            let mutInterimVar = Var("x", mutElemTy)
                            let mutTargetSelector = Expr.Var mutInterimVar
                            mutInterimSelectorBody, mutInterimVar, mutTargetSelector

                    // IQueryable.SelectMany expects an IEnumerable return
                    let mutInterimSelectorBody =
                        let mutSelectorBodyTy = mutInterimSelectorBodyPreCoerce.Type
                        if mutSelectorBodyTy.IsGenericType && mutSelectorBodyTy.GetGenericTypeDefinition() = typedefof<IEnumerable<_>> then
                            mutInterimSelectorBodyPreCoerce
                        else
                            let mutSeqTy = MakeIEnumerableTy mutInterimVar.Type
                            Expr.Coerce (mutInterimSelectorBodyPreCoerce, mutSeqTy)
                    TransInnerResult.Other(MakeSelectMany(qTyIsIQueryable qTy, mutElemTy, CommitTransInnerResult mutSource, mutSelectorVar, mutInterimSelectorBody, mutInterimVar, mutTargetSelector)), selectorConv


            TransFor mutSource immutSelector

        // These occur in the F# quotation form of F# sequence expressions
        | CallWhere (_, [_; qTy], immutSource, Lambda(immutSelectorVar, immutPredicateBody)) ->

            let mutSource, sourceConv, mutSelectorVar, mutPredicateBody = TransInnerApplicativeAndCommit check immutSource (immutSelectorVar, immutPredicateBody)
            TransInnerResult.Other(MakeWhere(qTyIsIQueryable qTy, mutSource, mutSelectorVar, mutPredicateBody)), sourceConv

        | CallSelect (_, [_; qTy; _], mutSource, Lambda(immutSelectorVar, immutSelectorBody)) ->

            let mutSource, _sourceConv, mutSelectorVar, mutSelectorBody = TransInnerApplicative check mutSource (immutSelectorVar, immutSelectorBody)
            let mutSelectorBody, selectorConv = ProduceMoreMutables TransInnerNoCheck mutSelectorBody
            let mutSelectorBody = CleanupLeaf mutSelectorBody

            TransInnerResult.MakeSelect(canElim, qTyIsIQueryable qTy, mutSource, mutSelectorVar, mutSelectorBody), selectorConv

        | CallQueryBuilderYieldFrom (_, _, [source]) -> TransInner canElim check source

        | CallQueryBuilderYield (_, [elemTy; qTy], immutSelectorBody) ->
            let immutSelectorBody = CleanupLeaf immutSelectorBody
            let enumExpr = Expr.Coerce (Expr.NewArray (elemTy, [ immutSelectorBody ]), MakeIEnumerableTy elemTy)
            let expr =
                if qTyIsIQueryable qTy then
                    MakeAsQueryable(elemTy, enumExpr)
                else
                    enumExpr
            TransInnerResult.Other expr, NoConv

        | IfThenElse (g, t, e) ->
            match MacroExpand e with
            | ZeroOnElseBranch ->
                let t, tConv = TransInnerAndCommit CanEliminate.Yes check t
                TransInnerResult.Other(Expr.IfThenElse (g, t, MakeEmpty t.Type)), tConv
            | _ ->
                if check then raise (NotSupportedException (SR.GetString(SR.unsupportedIfThenElse)) )
                TransInnerResult.Other e, NoConv

        | CallSortBy (_, [_; qTy; _], source, Lambda(v, keySelector)) ->
            let source, sourceConv, v, keySelector = TransInnerApplicativeAndCommit check source (v, keySelector)
            TransInnerResult.Other(MakeOrderBy                  (qTyIsIQueryable qTy, source, v, keySelector)), sourceConv

        | CallSortByDescending (_, [_; qTy; _], source, Lambda(v, keySelector)) ->
            let source, sourceConv, v, keySelector = TransInnerApplicativeAndCommit check source (v, keySelector)
            TransInnerResult.Other(MakeOrderByDescending        (qTyIsIQueryable qTy, source, v, keySelector)), sourceConv

        | CallThenBy (_, [_; qTy; _], source, Lambda(v, keySelector)) ->
            let source, sourceConv, v, keySelector = TransInnerApplicativeAndCommit check source (v, keySelector)
            TransInnerResult.Other(MakeThenBy                   (qTyIsIQueryable qTy, source, v, keySelector)), sourceConv

        | CallThenByDescending (_, [_; qTy; _], source, Lambda(v, keySelector)) ->
            let source, sourceConv, v, keySelector = TransInnerApplicativeAndCommit check source (v, keySelector)
            TransInnerResult.Other(MakeThenByDescending         (qTyIsIQueryable qTy, source, v, keySelector)), sourceConv

        | CallSortByNullable (_, [_; qTy; _], source, Lambda(v, keySelector)) ->
            let source, sourceConv, v, keySelector = TransInnerApplicativeAndCommit check source (v, keySelector)
            TransInnerResult.Other(MakeOrderByNullable          (qTyIsIQueryable qTy, source, v, keySelector)), sourceConv

        | CallSortByNullableDescending(_, [_; qTy; _], source, Lambda(v, keySelector)) ->
            let source, sourceConv, v, keySelector = TransInnerApplicativeAndCommit check source (v, keySelector)
            TransInnerResult.Other(MakeOrderByNullableDescending(qTyIsIQueryable qTy, source, v, keySelector)), sourceConv

        | CallThenByNullable (_, [_; qTy; _], source, Lambda(v, keySelector)) ->
            let source, sourceConv, v, keySelector = TransInnerApplicativeAndCommit check source (v, keySelector)
            TransInnerResult.Other(MakeThenByNullable           (qTyIsIQueryable qTy, source, v, keySelector)), sourceConv

        | CallThenByNullableDescending (_, [_; qTy; _], source, Lambda(v, keySelector)) ->
            let source, sourceConv, v, keySelector = TransInnerApplicativeAndCommit check source (v, keySelector)
            TransInnerResult.Other(MakeThenByNullableDescending (qTyIsIQueryable qTy, source, v, keySelector)), sourceConv

        | CallDistinct (_, [srcItemTy; qTy], source) ->
            let source, sourceConv = TransInnerAndCommit CanEliminate.Yes check source
            let srcItemTyR = ConvImmutableTypeToMutableType sourceConv srcItemTy
            TransInnerResult.Other(MakeDistinct(qTyIsIQueryable qTy, srcItemTyR, source)), sourceConv

        | CallSkip(_, [srcItemTy; qTy], source, count) ->
            let source, sourceConv = TransInnerAndCommit CanEliminate.Yes check source
            let srcItemTyR = ConvImmutableTypeToMutableType sourceConv srcItemTy
            TransInnerResult.Other(MakeSkip(qTyIsIQueryable qTy, srcItemTyR, source, MacroExpand count)), sourceConv

        | CallTake(_, [srcItemTy; qTy], source, count) ->
            let source, sourceConv = TransInnerAndCommit CanEliminate.Yes check source
            let srcItemTyR = ConvImmutableTypeToMutableType sourceConv srcItemTy
            TransInnerResult.Other(MakeTake(qTyIsIQueryable qTy, srcItemTyR, source, MacroExpand count)), sourceConv

        | CallSkipWhile(_, [_; qTy], source, Lambda(v, keySelector)) ->
            let source, sourceConv, v, keySelector = TransInnerApplicativeAndCommit check source (v, keySelector)
            TransInnerResult.Other(MakeSkipWhile        (qTyIsIQueryable qTy, source, v, keySelector)), sourceConv

        | CallTakeWhile(_, [_; qTy], source, Lambda(v, keySelector)) ->
            let source, sourceConv, v, keySelector = TransInnerApplicativeAndCommit check source (v, keySelector)
            TransInnerResult.Other(MakeTakeWhile (qTyIsIQueryable qTy, source, v, keySelector)), sourceConv

        | CallGroupBy(_, [_; qTy; _], immutSource, Lambda(immutVar, immutKeySelector)) ->
            let mutSource, sourceConv = TransInnerAndCommit CanEliminate.Yes check immutSource
            let mutVar, mutKeySelector = ConvertImmutableConsumerToMutableConsumer sourceConv (immutVar, MacroExpand immutKeySelector)
            let conv =
                match sourceConv with
                | NoConv -> NoConv
                | _ -> GroupingConv(immutKeySelector.Type, immutVar.Type, sourceConv)
            TransInnerResult.Other(MakeGroupBy(qTyIsIQueryable qTy, mutSource, mutVar, mutKeySelector)), conv

        | CallGroupValBy
              (_, [_; _; _; qTy],
               immutSource, 
               Lambda(immutVar1, immutElementSelector),
               Lambda(immutVar2, immutKeySelector)) ->

            let mutSource, sourceConv = TransInnerAndCommit CanEliminate.Yes check immutSource
            let mutVar2, mutKeySelector = ConvertImmutableConsumerToMutableConsumer sourceConv (immutVar2, MacroExpand immutKeySelector)
            let mutVar1, mutElementSelector = ConvertImmutableConsumerToMutableConsumer sourceConv (immutVar1, MacroExpand immutElementSelector)
            let mutElementSelector, selectorConv = ProduceMoreMutables TransInnerNoCheck mutElementSelector
            let mutElementSelector = CleanupLeaf mutElementSelector
            let conv = 
                match selectorConv with
                | NoConv -> NoConv
                | _ -> GroupingConv (immutKeySelector.Type, immutElementSelector.Type, selectorConv)
            TransInnerResult.Other(MakeGroupValBy(qTyIsIQueryable qTy, mutVar1.Type, mutKeySelector.Type, mutElementSelector.Type, mutSource, mutVar2, mutKeySelector, mutVar1, mutElementSelector)), conv

        | CallJoin(_, GenericArgs [_; qTy; _; _; _],
                   [ immutOuterSource
                     immutInnerSource
                     Lambda(immutOuterKeyVar, immutOuterKeySelector)
                     Lambda(immutInnerKeyVar, immutInnerKeySelector)
                     LambdasNoDetupling([immutOuterResultGroupVar; immutInnerResultKeyVar], immutElementSelector)]) ->

            let (mutOuterSource, outerSourceConv, mutInnerSource, innerSourceConv, mutOuterKeyVar: Var, mutOuterKeySelector, mutInnerKeyVar: Var, mutInnerKeySelector: Expr) =
                TransJoinInputs check (immutOuterSource, immutInnerSource, immutOuterKeyVar, immutOuterKeySelector, immutInnerKeyVar, immutInnerKeySelector)

            let mutOuterResultVar, mutElementSelector = ConvertImmutableConsumerToMutableConsumer outerSourceConv (immutOuterResultGroupVar, MacroExpand immutElementSelector)
            let mutInnerResultKeyVar, mutElementSelector = ConvertImmutableConsumerToMutableConsumer innerSourceConv (immutInnerResultKeyVar, mutElementSelector)
            let mutElementSelector, elementSelectorConv = ProduceMoreMutables TransInnerNoCheck mutElementSelector
            let mutElementSelector = CleanupLeaf mutElementSelector

            let joinExpr =
                MakeJoin
                    (qTyIsIQueryable qTy, mutOuterKeyVar.Type, mutInnerKeyVar.Type, mutInnerKeySelector.Type, 
                     mutElementSelector.Type, mutOuterSource, mutInnerSource, mutOuterKeyVar, mutOuterKeySelector,
                     mutInnerKeyVar, mutInnerKeySelector, mutOuterResultVar, mutInnerResultKeyVar, mutElementSelector)

            TransInnerResult.Other joinExpr, elementSelectorConv

        | CallGroupJoin
              (_, GenericArgs [_; qTy; _; _; _], 
               [ immutOuterSource
                 immutInnerSource
                 Lambda(immutOuterKeyVar, immutOuterKeySelector)
                 Lambda(immutInnerKeyVar, immutInnerKeySelector)
                 LambdasNoDetupling([immutOuterResultGroupVar; immutInnerResultGroupVar], immutElementSelector)]) ->

            let (mutOuterSource, outerSourceConv, mutInnerSource, innerSourceConv, mutOuterKeyVar: Var, mutOuterKeySelector, mutInnerKeyVar: Var, mutInnerKeySelector: Expr) =
                TransJoinInputs check (immutOuterSource, immutInnerSource, immutOuterKeyVar, immutOuterKeySelector, immutInnerKeyVar, immutInnerKeySelector)

            let mutOuterResultGroupVar, mutElementSelector = ConvertImmutableConsumerToMutableConsumer outerSourceConv (immutOuterResultGroupVar, MacroExpand immutElementSelector)
            let innerGroupConv = MakeSeqConv innerSourceConv
            let mutInnerResultKeyVar, mutElementSelector = ConvertImmutableConsumerToMutableConsumer innerGroupConv (immutInnerResultGroupVar, mutElementSelector)
            let mutElementSelector, elementSelectorConv = ProduceMoreMutables TransInnerNoCheck mutElementSelector
            let mutElementSelector = CleanupLeaf mutElementSelector

            let joinExpr = 
                MakeGroupJoin 
                    (qTyIsIQueryable qTy, mutOuterKeyVar.Type, mutInnerKeyVar.Type, 
                     mutInnerKeySelector.Type, mutElementSelector.Type, mutOuterSource,
                     mutInnerSource, mutOuterKeyVar, mutOuterKeySelector, mutInnerKeyVar,
                     mutInnerKeySelector, mutOuterResultGroupVar, mutInnerResultKeyVar, mutElementSelector)

            TransInnerResult.Other joinExpr, elementSelectorConv

        | CallLeftOuterJoin
             (_, GenericArgs [ _; qTy; immutInnerSourceTy; _; _],
              [ immutOuterSource
                immutInnerSource
                Lambda(immutOuterKeyVar, immutOuterKeySelector)
                Lambda(immutInnerKeyVar, immutInnerKeySelector)
                LambdasNoDetupling([immutOuterResultGroupVar; immutInnerResultGroupVar], immutElementSelector)]) ->

            // Replace uses of 'innerResultGroupVar' with 'innerResultGroupVar.DefaultIfEmpty()' and call MakeGroupJoin
            let immutElementSelector = immutElementSelector.Substitute (fun v -> if v = immutInnerResultGroupVar then Some (MakeDefaultIfEmpty ([immutInnerSourceTy], [Expr.Var immutInnerResultGroupVar])) else None)

            let (mutOuterSource, outerSourceConv, mutInnerSource, innerSourceConv, mutOuterKeyVar: Var, mutOuterKeySelector, mutInnerKeyVar: Var, mutInnerKeySelector: Expr) =
                TransJoinInputs check (immutOuterSource, immutInnerSource, immutOuterKeyVar, immutOuterKeySelector, immutInnerKeyVar, immutInnerKeySelector)

            let mutOuterResultGroupVar, mutElementSelector = ConvertImmutableConsumerToMutableConsumer outerSourceConv (immutOuterResultGroupVar, MacroExpand immutElementSelector)
            let mutInnerResultKeyVar, mutElementSelector = ConvertImmutableConsumerToMutableConsumer innerSourceConv (immutInnerResultGroupVar, mutElementSelector)
            let mutElementSelector, elementSelectorConv = ProduceMoreMutables TransInnerNoCheck mutElementSelector
            let mutElementSelector = CleanupLeaf mutElementSelector

            let joinExpr =
                MakeGroupJoin
                    (qTyIsIQueryable qTy, mutOuterKeyVar.Type, mutInnerKeyVar.Type, mutInnerKeySelector.Type,
                     mutElementSelector.Type, mutOuterSource, mutInnerSource, mutOuterKeyVar, mutOuterKeySelector,
                     mutInnerKeyVar, mutInnerKeySelector, mutOuterResultGroupVar, mutInnerResultKeyVar, mutElementSelector)

            TransInnerResult.Other joinExpr, elementSelectorConv

        | LetExprReduction reduced ->
            TransInner canElim check reduced

        | MacroReduction reduced ->
            TransInner canElim check reduced

        | CallQueryBuilderSourceIQueryable(_, _, expr) ->
            TransInnerResult.Source expr, NoConv

        | CallQueryBuilderSourceIEnumerable (_, _, expr) ->
            TransInnerResult.Source expr, NoConv

        | Call (_, meth, _) when check ->
            raise (NotSupportedException (String.Format(SR.GetString(SR.unsupportedQueryCall), meth.ToString())))

        | PropertyGet (_, pinfo, _) when check ->
            raise (NotSupportedException (String.Format(SR.GetString(SR.unsupportedQueryProperty), pinfo.ToString())))

        | NewObject(ty, _) when check ->
            raise (NotSupportedException (String.Format(SR.GetString(SR.unsupportedQueryConstructKind), "new " + ty.ToString())))

        | NewArray(ty, _) when check ->
            raise (NotSupportedException (String.Format(SR.GetString(SR.unsupportedQueryConstructKind), "NewArray(" + ty.Name + ", ...)")))

        | NewTuple _  when check ->
            raise (NotSupportedException (String.Format(SR.GetString(SR.unsupportedQueryConstructKind), "NewTuple(...)")))

        | FieldGet (_, field) when check ->
            raise (NotSupportedException (String.Format(SR.GetString(SR.unsupportedQueryConstructKind), "FieldGet(" + field.Name + ", ...)")))

        | LetRecursive _ when check  ->
            raise (NotSupportedException (String.Format(SR.GetString(SR.unsupportedQueryConstruct), "LetRecursive(...)")))

        | NewRecord _ when check  ->
            raise (NotSupportedException (String.Format(SR.GetString(SR.unsupportedQueryConstruct), "NewRecord(...)")))

        | NewDelegate _  when check  ->
            raise (NotSupportedException (String.Format(SR.GetString(SR.unsupportedQueryConstruct), "NewDelegate(...)")))

        | NewTuple _  when check  ->
            raise (NotSupportedException (String.Format(SR.GetString(SR.unsupportedQueryConstruct), "NewTuple(...)")))

        | NewUnionCase (ucase, _) when check  ->
            raise (NotSupportedException (String.Format(SR.GetString(SR.unsupportedQueryConstruct), "NewUnionCase(" + ucase.Name + "...)")))

        // Error cases
        | _ ->
            if check then
                raise (NotSupportedException (String.Format(SR.GetString(SR.unsupportedQueryConstruct), immutQuery.ToString())))
            TransInnerResult.Source immutQuery, NoConv

    and TransInnerAndCommit canElim check x =
        let info, conv = TransInner canElim check x
        CommitTransInnerResult info, conv

    and TransNone x = (x, NoConv)

    // We translate nested queries directly in order to
    // propagate a immutable-->mutable-->immutable translation if any.
    //
    /// This is used on recursive translations of yielded elements to translate nested queries
    /// in 'yield' position and still propagate information about a possible imutable->mutable->mutable
    //  translation.
    //      e.g. yield (1, query { ... })
    and TransInnerNoCheck e =
        match e with
        | EnumerableNestedQuery nestedQuery ->
            let replNestedQuery, conv = TransInnerAndCommit CanEliminate.Yes false nestedQuery
            let replNestedQuery =
                let tyArg = replNestedQuery.Type.GetGenericArguments().[0]
                let IQueryableTySpec = MakeIQueryableTy tyArg
                // if result type of nested query is derived from IQueryable but not IQueryable itself (i.e. IOrderedQueryable)
                // then add coercion to IQueryable so result type will match expected signature of QuerySource.Run
                if (IQueryableTySpec.IsAssignableFrom replNestedQuery.Type) && not (IQueryableTySpec.Equals replNestedQuery.Type) then
                    Expr.Coerce (replNestedQuery, IQueryableTySpec)
                else
                    replNestedQuery
            replNestedQuery, MakeSeqConv conv
        | _ ->
            e, NoConv

    and TransJoinInputs check  (immutOuterSource, immutInnerSource, immutOuterKeyVar, immutOuterKeySelector, immutInnerKeyVar, immutInnerKeySelector) =

        let mutOuterSource, outerSourceConv = TransInnerAndCommit CanEliminate.Yes check immutOuterSource
        let mutInnerSource, innerSourceConv = TransInnerAndCommit CanEliminate.Yes check immutInnerSource
        let mutOuterKeyVar, mutOuterKeySelector = ConvertImmutableConsumerToMutableConsumer outerSourceConv (immutOuterKeyVar, MacroExpand immutOuterKeySelector)
        let mutInnerKeyVar, mutInnerKeySelector = ConvertImmutableConsumerToMutableConsumer innerSourceConv (immutInnerKeyVar, MacroExpand immutInnerKeySelector)

        // Keys may be composite tuples - convert them to be mutables. Note, if there is a tuple on one side, there must be a tuple on the other side.
        let mutOuterKeySelector, _ = ProduceMoreMutables TransNone mutOuterKeySelector
        let mutOuterKeySelector = CleanupLeaf mutOuterKeySelector
        let mutInnerKeySelector, _ = ProduceMoreMutables TransNone mutInnerKeySelector
        let mutInnerKeySelector = CleanupLeaf mutInnerKeySelector
        mutOuterSource, outerSourceConv, mutInnerSource, innerSourceConv, mutOuterKeyVar, mutOuterKeySelector, mutInnerKeyVar, mutInnerKeySelector

    /// Given a query expression in terms of query.For, query.Select, query.Yield, query.Where etc.,
    /// and including immutable tuples and immutable records, build an equivalent query expression
    /// in terms of LINQ operators, operating over mutable tuples. Return the conversion
    /// information for the immutable-to-mutable conversion performed so we can undo it where needed.
    ///
    /// Further, assume that the elements produced by the query will be consumed by the function "(fun immutConsumingVar -> immutConsumingExpr)"
    /// and produce the expressions for a new function that consume the results directly.
    and TransInnerApplicative check source (immutConsumingVar, immutConsumingExpr) =
        let source, sourceConv = TransInner CanEliminate.Yes check source
        let mutConsumingVar, mutConsumingExpr = ConvertImmutableConsumerToMutableConsumer sourceConv (immutConsumingVar, MacroExpand immutConsumingExpr)
        source, sourceConv, mutConsumingVar, mutConsumingExpr

    and TransInnerApplicativeAndCommit check source (immutConsumingVar, immutConsumingExpr) =
        let source, sourceConv, mutConsumingVar, mutConsumingExpr = TransInnerApplicative check source (immutConsumingVar, immutConsumingExpr)
        CommitTransInnerResult source, sourceConv, mutConsumingVar, mutConsumingExpr

    /// Given a query expression in terms of query.For, query.Select, query.Yield, query.Where etc.,
    /// and including immutable tuples and immutable records, build an equivalent query expression
    /// in terms of LINQ operators, operating over mutable tuples. If necessary, also add a "postifx" in-memory transformation
    /// converting the data back to immutable tuples and records.
    let TransInnerWithFinalConsume canElim immutSource =
        let mutSource, sourceConv = TransInnerAndCommit canElim true immutSource
        match sourceConv with
        | NoConv ->
            mutSource
        | _ ->
            // This function is used with inputs of
            //     - QuerySource<_, _> (for operators like Min)
            //     - IQueryable<_> (for operators like MinBy)
            //     - IEnumerable<_> (for nested queries)
            let immutSourceTy = immutSource.Type
            let immutSourceElemTy =
                assert immutSourceTy.IsGenericType
                assert (IsQuerySourceTy immutSourceTy || IsIQueryableTy immutSourceTy || IsIEnumerableTy immutSourceTy)
                immutSource.Type.GetGenericArguments().[0]
            let immutVar = Var("after", immutSourceElemTy)
            let mutVar, mutToImmutSelector = ConvertImmutableConsumerToMutableConsumer sourceConv (immutVar, Expr.Var immutVar)
            let immutExprEnumerable = MakeSelect(CanEliminate.Yes, false, mutSource, mutVar, mutToImmutSelector)
            let mustReturnIQueryable =
                IsQuerySourceTy immutSourceTy && qTyIsIQueryable (immutSourceTy.GetGenericArguments().[1]) ||
                IsIQueryableTy immutSourceTy
            let immutExprFinal =
                if mustReturnIQueryable then MakeAsQueryable(immutSourceElemTy, immutExprEnumerable)
                else immutExprEnumerable
            immutExprFinal

    /// Like TransInnerApplicativeAndCommit but (a) assumes the query is nested and (b) throws away the conversion information,
    /// i.e. assumes that the function "(fun immutConsumingVar -> immutConsumingExpr)" is the only consumption of the query.
    let TransNestedInnerWithConsumer immutSource (immutConsumingVar, immutConsumingExpr) =
        let mutSource, _sourceConv, mutConsumingVar, mutConsumingExpr = TransInnerApplicativeAndCommit true immutSource (immutConsumingVar, immutConsumingExpr)
        mutSource, mutConsumingVar, mutConsumingExpr


    /// Translate nested query combinator calls to LINQ calls.
    let rec TransNestedOuter canElim quot =
        match quot with
        | CallMinBy (_, [_; qTy; _], source, Lambda(v, valSelector)) ->
            let source, v, valSelector = TransNestedInnerWithConsumer source (v, valSelector)
            MakeMinBy (qTyIsIQueryable qTy, source, v, valSelector)

        | CallMaxBy (_, [_; qTy; _], source, Lambda(v, valSelector)) ->
            let source, v, valSelector = TransNestedInnerWithConsumer source (v, valSelector)
            MakeMaxBy (qTyIsIQueryable qTy, source, v, valSelector)

        | CallMinByNullable (_, [_; qTy; _], source, Lambda(v, valSelector)) ->
            let source, v, valSelector = TransNestedInnerWithConsumer source (v, valSelector)
            MakeMinByNullable (qTyIsIQueryable qTy, source, v, valSelector)

        | CallMaxByNullable (_, [_; qTy; _], source, Lambda(v, valSelector)) ->
            let source, v, valSelector = TransNestedInnerWithConsumer source (v, valSelector)
            MakeMaxByNullable (qTyIsIQueryable qTy, source, v, valSelector)

        | CallCount (_, [srcItemTy; qTy], source)                      ->
            let source = TransInnerWithFinalConsume CanEliminate.Yes source
            MakeCount (qTyIsIQueryable qTy, srcItemTy, source)

        | CallHead (_, [srcItemTy; qTy], source)                      ->
            let source = TransInnerWithFinalConsume CanEliminate.Yes source
            MakeFirst (qTyIsIQueryable qTy, srcItemTy, source)

        | CallLast (_, [srcItemTy; qTy], source)                      ->
            let source = TransInnerWithFinalConsume CanEliminate.Yes source
            MakeLast (qTyIsIQueryable qTy, srcItemTy, source)

        | CallHeadOrDefault (_, [srcItemTy; qTy], source)                      ->
            let source = TransInnerWithFinalConsume CanEliminate.Yes source
            MakeFirstOrDefault (qTyIsIQueryable qTy, srcItemTy, source)

        | CallLastOrDefault (_, [srcItemTy; qTy], source)                      ->
            let source = TransInnerWithFinalConsume CanEliminate.Yes source
            MakeLastOrDefault (qTyIsIQueryable qTy, srcItemTy, source)

        | CallExactlyOne (_, [srcItemTy; qTy], source)                      ->
            let source = TransInnerWithFinalConsume CanEliminate.Yes source
            MakeSingle (qTyIsIQueryable qTy, srcItemTy, source)

        | CallExactlyOneOrDefault (_, [srcItemTy; qTy], source)                      ->
            let source = TransInnerWithFinalConsume CanEliminate.Yes source
            MakeSingleOrDefault(qTyIsIQueryable qTy, srcItemTy, source)

        | CallAverageBy (qb, [_; qTy; _], source, Lambda(v, valSelector)) ->
            let source, v, valSelector = TransNestedInnerWithConsumer source (v, valSelector)
            MakeAverageBy (qb, qTyIsIQueryable qTy, source, v, valSelector)

        | CallAverageByNullable (qb, [_; qTy; _], source, Lambda(v, valSelector)) ->
            let source, v, valSelector = TransNestedInnerWithConsumer source (v, valSelector)
            MakeAverageByNullable(qb, qTyIsIQueryable qTy, source, v, valSelector)

        | CallSumBy (qb, [_; qTy; _], source, Lambda(v, valSelector)) ->
            let source, v, valSelector = TransNestedInnerWithConsumer source (v, valSelector)
            MakeSumBy (qb, qTyIsIQueryable qTy, source, v, valSelector)

        | CallSumByNullable (qb, [_; qTy; _], source, Lambda(v, valSelector)) ->
            let source, v, valSelector = TransNestedInnerWithConsumer source (v, valSelector)
            MakeSumByNullable (qb, qTyIsIQueryable qTy, source, v, valSelector)

        | CallExists (_, [_; qTy], source, Lambda(v, predicate)) ->
            let source, v, predicate = TransNestedInnerWithConsumer source (v, predicate)
            MakeAny (qTyIsIQueryable qTy, source, v, predicate)

        | CallForAll (_, [_; qTy], source, Lambda(v, predicate)) ->
            let source, v, predicate = TransNestedInnerWithConsumer source (v, predicate)
            MakeAll (qTyIsIQueryable qTy, source, v, predicate)

        | CallFind (_, [_; qTy], source, Lambda(v, predicate)) ->
            let source, v, predicate = TransNestedInnerWithConsumer source (v, predicate)
            MakeFirstFind (qTyIsIQueryable qTy, source, v, predicate)

        | CallContains (_, [srcItemTy; qTy], source, valToFindExpr)         ->
            let source = TransInnerWithFinalConsume CanEliminate.Yes source
            MakeContains (qTyIsIQueryable qTy, srcItemTy, source, MacroExpand valToFindExpr)

        | CallNth (_, [srcItemTy; qTy], source, valCountExpr)         ->
            let source = TransInnerWithFinalConsume CanEliminate.Yes source
            MakeElementAt (qTyIsIQueryable qTy, srcItemTy, source, MacroExpand valCountExpr)

        | LetExprReduction reduced ->
            TransNestedOuter canElim reduced

        | MacroReduction reduced ->
            TransNestedOuter canElim reduced

        | source ->
            TransInnerWithFinalConsume canElim source


    // Nested queries appear as query { .... }
    // [[ query { ... }  ]] = TransNestedOuter canElim[[q]]
    //    -- This is the primary translation for nested sequences.
    let EliminateNestedQueries q =
        q |> RewriteExpr (fun walk p ->
            match p with
            | AnyNestedQuery e -> Some (walk (TransNestedOuter CanEliminate.No e))
            | _ -> None)

    /// Evaluate the inner core of a query that actually produces a sequence of results.
    /// Do this by converting to an expression tree for a LINQ query and evaluating that.
    let EvalNonNestedInner canElim (queryProducingSequence: Expr) =
        let linqQuery = TransInnerWithFinalConsume canElim queryProducingSequence
        let linqQueryAfterEliminatingNestedQueries = EliminateNestedQueries linqQuery

#if DEBUG
        let debug() =
              Printf.printfn "----------------------queryProducingSequence-------------------------"
              Printf.printfn "%A" queryProducingSequence
              Printf.printfn "--------------------------linqQuery (before nested)------------------"
              Printf.printfn "%A" linqQuery
              Printf.printfn "--------------------------linqQuery (after nested)-------------------"
              Printf.printfn "%A" linqQueryAfterEliminatingNestedQueries
#endif


        let result =
           try
              LeafExpressionConverter.EvaluateQuotation linqQueryAfterEliminatingNestedQueries
           with e ->
#if DEBUG
              debug()
              Printf.printfn "--------------------------error--------------------------------------"
              Printf.printfn "%A" (e.ToString())
              Printf.printfn "---------------------------------------------------------------------"
#endif
              reraise ()


        result

    /// Evaluate the outer calls of a query until the inner core that actually produces a sequence of results is reached.
    let rec EvalNonNestedOuter canElim (tm: Expr) =
        match tm with

        | CallMinBy (_, [srcItemTy; qTy; keyItemTy], source, Lambda(v, valSelector)) ->
            let sourcev = EvalNonNestedInner CanEliminate.Yes source
            CallMinBy (qTyIsIQueryable qTy, srcItemTy, keyItemTy, sourcev, keyItemTy, v, MacroExpand valSelector)

        | CallMaxBy (_, [srcItemTy; qTy; keyItemTy], source, Lambda(v, valSelector)) ->
            let sourcev = EvalNonNestedInner CanEliminate.Yes source
            CallMaxBy (qTyIsIQueryable qTy, srcItemTy, keyItemTy, sourcev, keyItemTy, v, MacroExpand valSelector)

        | CallMinByNullable (_, [srcItemTy; qTy; keyItemTy ], source, Lambda(v, valSelector)) ->
            let sourcev = EvalNonNestedInner CanEliminate.Yes source
            CallMinByNullable  (qTyIsIQueryable qTy, srcItemTy, keyItemTy, sourcev, MakeNullableTy keyItemTy, v, MacroExpand valSelector)

        | CallMaxByNullable (_, [srcItemTy; qTy; keyItemTy ], source, Lambda(v, valSelector)) ->
            let sourcev = EvalNonNestedInner CanEliminate.Yes source
            CallMaxByNullable  (qTyIsIQueryable qTy, srcItemTy, keyItemTy, sourcev, MakeNullableTy keyItemTy, v, MacroExpand valSelector)

        | CallCount (_, [srcItemTy; qTy], source) ->
            let sourcev = EvalNonNestedInner CanEliminate.Yes source
            CallCount          (qTyIsIQueryable qTy, srcItemTy, sourcev)

        | CallHead (_, [srcItemTy; qTy], source) ->
            let sourcev = EvalNonNestedInner CanEliminate.Yes source
            CallFirst (qTyIsIQueryable qTy, srcItemTy, sourcev)

        | CallLast (_, [srcItemTy; qTy], source) ->
            let sourcev = EvalNonNestedInner CanEliminate.Yes source
            CallLast           (qTyIsIQueryable qTy, srcItemTy, sourcev)

        | CallHeadOrDefault (_, [srcItemTy; qTy], source) ->
            let sourcev = EvalNonNestedInner CanEliminate.Yes source
            CallFirstOrDefault (qTyIsIQueryable qTy, srcItemTy, sourcev)

        | CallLastOrDefault (_, [srcItemTy; qTy], source) ->
            let sourcev = EvalNonNestedInner CanEliminate.Yes source
            CallLastOrDefault  (qTyIsIQueryable qTy, srcItemTy, sourcev)

        | CallExactlyOne (_, [srcItemTy; qTy], source) ->
            let sourcev = EvalNonNestedInner CanEliminate.Yes source
            CallSingle         (qTyIsIQueryable qTy, srcItemTy, sourcev)

        | CallExactlyOneOrDefault (_, [srcItemTy; qTy], source) ->
            let sourcev = EvalNonNestedInner CanEliminate.Yes source
            CallSingleOrDefault(qTyIsIQueryable qTy, srcItemTy, sourcev)

        | CallAverageBy (qb, [srcItemTy; qTy; resTy], source, Lambda(v, valSelector)) ->
            let sourcev = EvalNonNestedInner CanEliminate.Yes source
            CallAverageBy        (qb, qTyIsIQueryable qTy, srcItemTy, resTy, sourcev, resTy, v, MacroExpand valSelector)

        | CallAverageByNullable (qb, [srcItemTy; qTy; resTyNoNullable], source, Lambda(v, valSelector)) ->
            let sourcev = EvalNonNestedInner CanEliminate.Yes source
            CallAverageByNullable(qb, qTyIsIQueryable qTy, srcItemTy, resTyNoNullable, sourcev, MakeNullableTy resTyNoNullable, v, MacroExpand valSelector)

        | CallSumBy (qb, [srcItemTy; qTy; resTy], source, Lambda(v, valSelector)) ->
            let sourcev = EvalNonNestedInner CanEliminate.Yes source
            CallSumBy (qb, qTyIsIQueryable qTy, srcItemTy, resTy, sourcev, resTy, v, MacroExpand valSelector)

        | CallSumByNullable (qb, [srcItemTy; qTy; resTyNoNullable], source, Lambda(v, valSelector)) ->
            let sourcev = EvalNonNestedInner CanEliminate.Yes source
            CallSumByNullable (qb, qTyIsIQueryable qTy, srcItemTy, resTyNoNullable, sourcev, MakeNullableTy resTyNoNullable, v, MacroExpand valSelector)

        | CallExists (_, [srcItemTy; qTy], source, Lambda(v, predicate)) ->
            let sourcev = EvalNonNestedInner CanEliminate.Yes source
            CallAny (qTyIsIQueryable qTy, srcItemTy, sourcev, v, MacroExpand predicate)

        | CallForAll (_, [srcItemTy; qTy], source, Lambda(v, predicate)) ->
            let sourcev = EvalNonNestedInner CanEliminate.Yes source
            CallAll (qTyIsIQueryable qTy, srcItemTy, sourcev, v, MacroExpand predicate)

        | CallFind (_, [srcItemTy; qTy], source, Lambda(v, f)) ->
            let sourcev = EvalNonNestedInner CanEliminate.Yes source
            CallFirstFind (qTyIsIQueryable qTy, srcItemTy, sourcev, v, MacroExpand f)

        | CallContains (_, [srcItemTy; qTy], source, v) ->
            let sourcev = EvalNonNestedInner CanEliminate.Yes source
            CallContains (qTyIsIQueryable qTy, srcItemTy, sourcev, MacroExpand v)

        | CallNth (_, [srcItemTy; qTy], source, v) ->
            let sourcev = EvalNonNestedInner CanEliminate.Yes source
            CallElementAt (qTyIsIQueryable qTy, srcItemTy, sourcev, MacroExpand v)

        | LetExprReduction reduced ->
            EvalNonNestedOuter canElim reduced

        | MacroReduction reduced ->
            EvalNonNestedOuter canElim reduced

        | source ->
            EvalNonNestedInner canElim source

    let QueryExecute (p: Expr<'T>) : 'U =
        // We use Unchecked.unbox to allow headOrDefault, lastOrDefault and exactlyOneOrDefault to return Unchecked.defaultof<_> values for F# types
        Unchecked.unbox (EvalNonNestedOuter CanEliminate.No p)

    do ForwardDeclarations.Query <-
        {
            new ForwardDeclarations.IQueryMethods with
                member this.Execute q = QueryExecute q
                member this.EliminateNestedQueries e = EliminateNestedQueries e
        }


