// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.FSharp.Linq.RuntimeHelpers

open System
open System.Collections.Generic
open System.Linq.Expressions
open System.Reflection
open Microsoft.FSharp
open Microsoft.FSharp.Collections
open Microsoft.FSharp.Core
open Microsoft.FSharp.Linq
open Microsoft.FSharp.Core.Operators
open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicOperators
open Microsoft.FSharp.Reflection
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Quotations.DerivedPatterns

#nowarn "1204"

module LeafExpressionConverter =

    // The following is recognized as a LINQ 'member initialization pattern' in a quotation.
    let MemberInitializationHelper (_x:'T) : 'T =  raise (NotSupportedException "This function should not be called directly")

    // The following is recognized as a LINQ 'member initialization pattern' in a quotation.
    let NewAnonymousObjectHelper (_x:'T) : 'T =  raise (NotSupportedException "This function should not be called directly")

    // This is used to mark expressions inserted to satisfy C#'s design where, inside C#-compiler generated
    // LINQ expressions, they pass an argument or type T to an argument expecting Expression<T>.
    let ImplicitExpressionConversionHelper (_x:'T) : Expression<'T> = raise (NotSupportedException "This function should not be called directly")

    [<NoEquality; NoComparison>]
    type ConvEnv =
        {   varEnv : Map<Var, Expression> }
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

    let GetGenericMethodDefinition (methInfo:MethodInfo) =
        if methInfo.IsGenericMethod then methInfo.GetGenericMethodDefinition() else methInfo

    let StringConcat =
       methodhandleof (fun (x:obj, y:obj) -> String.Concat (x, y))
       |> System.Reflection.MethodInfo.GetMethodFromHandle
       :?> MethodInfo

    let SubstHelperRaw (q:Expr, x:Var[], y:obj[]) : Expr =
        let d = Map.ofArray (Array.zip x y)
        q.Substitute(fun v -> v |> d.TryFind |> Option.map (fun x -> Expr.Value (x, v.Type)))

    let SubstHelper<'T> (q:Expr, x:Var[], y:obj[]) : Expr<'T> =
        SubstHelperRaw(q, x, y) |> Expr.Cast

    let showAll =
        BindingFlags.Public ||| BindingFlags.NonPublic

    let NullableConstructor =
        typedefof<Nullable<int>>.GetConstructors().[0]
    
    let getNonNullableType typ = match Nullable.GetUnderlyingType typ with null -> typ | t -> t

    // https://github.com/dotnet/runtime/blob/fa779e8cb2b5868a0ac2fd4215f39ffb91f0dab0/src/libraries/System.Linq.Expressions/src/System/Dynamic/Utils/TypeUtils.cs#L72
    /// Can LINQ Expressions' BinaryExpression's (Left/Right)Shift construct a SimpleBinaryExpression from the type in question? Otherwise, use the F# operator as the user-defined method. 
    let isLinqExpressionsInteger typ =
        let typ = getNonNullableType typ
        not typ.IsEnum &&
        match Type.GetTypeCode typ with
        | TypeCode.Byte
        | TypeCode.SByte
        | TypeCode.Int16
        | TypeCode.Int32
        | TypeCode.Int64
        | TypeCode.UInt16
        | TypeCode.UInt32
        | TypeCode.UInt64 -> true
        | _ -> false
    // https://github.com/dotnet/runtime/blob/fa779e8cb2b5868a0ac2fd4215f39ffb91f0dab0/src/libraries/System.Linq.Expressions/src/System/Linq/Expressions/BinaryExpression.cs#L2226
    /// Can LINQ Expressions' BinaryExpression's (Left/Right)Shift construct a SimpleBinaryExpression from the type in question? Otherwise, use the F# operator as the user-defined method. 
    let isLinqExpressionsSimpleShift left right =
        isLinqExpressionsInteger left && getNonNullableType right = typeof<int>
    // https://github.com/dotnet/runtime/blob/cf7e7a46f8a4a6225a8f1e059a846ccdebf0454c/src/libraries/System.Linq.Expressions/src/System/Dynamic/Utils/TypeUtils.cs#L110
    /// Can LINQ Expressions' (UnaryExpression/BinaryExpression)'s arithmetic operations construct a (SimpleBinaryExpression/UnaryExpression) from the type in question? Otherwise, use the F# operator as the user-defined method. 
    let isLinqExpressionsArithmeticType typ =
        let typ = getNonNullableType typ
        not typ.IsEnum &&
        match Type.GetTypeCode typ with
        | TypeCode.Int16
        | TypeCode.Int32
        | TypeCode.Int64
        | TypeCode.Double
        | TypeCode.Single
        | TypeCode.UInt16
        | TypeCode.UInt32
        | TypeCode.UInt64 -> true
        | _ -> false
    // https://github.com/dotnet/runtime/blob/7bd472498e690e9421df86d5a9d728faa939742c/src/libraries/System.Linq.Expressions/src/System/Dynamic/Utils/TypeUtils.cs#L132
    /// Can LINQ Expressions' UnaryExpression.(Checked)Negate construct a UnaryExpression from the type in question? Otherwise, use the F# operator as the user-defined method. 
    let isLinqExpressionsArithmeticTypeButNotUnsignedInt typ =
        isLinqExpressionsArithmeticType typ &&
        let typ = getNonNullableType typ
        not typ.IsEnum &&
        match Type.GetTypeCode typ with
        | TypeCode.UInt16
        | TypeCode.UInt32
        | TypeCode.UInt64 -> false
        | _ -> true
    // https://github.com/dotnet/runtime/blob/7bd472498e690e9421df86d5a9d728faa939742c/src/libraries/System.Linq.Expressions/src/System/Dynamic/Utils/TypeUtils.cs#L149
    /// Can LINQ Expressions' (UnaryExpression.Not/BinaryExpression.Binary(And/Or/ExclusiveOr)) construct a (UnaryExpression/SimpleBinaryExpression) from the type in question? Otherwise, use the F# operator as the user-defined method. 
    let isLinqExpressionsIntegerOrBool typ =
        let typ = getNonNullableType typ
        not typ.IsEnum &&
        match Type.GetTypeCode typ with
        | TypeCode.Int64
        | TypeCode.Int32
        | TypeCode.Int16
        | TypeCode.UInt64
        | TypeCode.UInt32
        | TypeCode.UInt16
        | TypeCode.Boolean
        | TypeCode.SByte
        | TypeCode.Byte -> true
        | _ -> false
    // https://github.com/dotnet/runtime/blob/7bd472498e690e9421df86d5a9d728faa939742c/src/libraries/System.Linq.Expressions/src/System/Dynamic/Utils/TypeUtils.cs#L47
    /// Can LINQ Expressions' BinaryExpression's comparison operations construct a (SimpleBinaryExpression/LogicalBinaryExpression) from the type in question? Otherwise, use the F# operator as the user-defined method. 
    let isLinqExpressionsNumeric typ =
        let typ = getNonNullableType typ
        not typ.IsEnum &&
        match Type.GetTypeCode typ with
        | TypeCode.Char
        | TypeCode.SByte
        | TypeCode.Byte
        | TypeCode.Int16
        | TypeCode.Int32
        | TypeCode.Int64
        | TypeCode.Double
        | TypeCode.Single
        | TypeCode.UInt16
        | TypeCode.UInt32
        | TypeCode.UInt64 -> true
        | _ -> false
    // https://github.com/dotnet/runtime/blob/afaf666eff08435123eb649ac138419f4c9b9344/src/libraries/System.Linq.Expressions/src/System/Linq/Expressions/BinaryExpression.cs#L1047
    /// Can LINQ Expressions' BinaryExpression's equality operations provide built-in structural equality from the type in question? Otherwise, use the F# operator as the user-defined method. 
    let isLinqExpressionsStructurallyEquatable typ =
        isLinqExpressionsNumeric typ || typ = typeof<bool> || getNonNullableType(typ).IsEnum
    // https://github.com/dotnet/runtime/blob/4c92aef2b08f9c4374c520e7e664a44f1ad8ce56/src/libraries/System.Linq.Expressions/src/System/Linq/Expressions/BinaryExpression.cs#L1221
    /// Can LINQ Expressions' BinaryExpression's comparison operations provide built-in comparison from the type in question? Otherwise, use the F# operator as the user-defined method. 
    let isLinqExpressionsComparable = isLinqExpressionsNumeric
    /// Can LINQ Expressions' BinaryExpression's equality operations provide built-in equality from the type in question? Otherwise, use the F# operator as the user-defined method. 
    let isLinqExpressionsEquatable typ =
        isLinqExpressionsStructurallyEquatable typ || typ = typeof<obj>
    /// Can LINQ Expressions' BinaryExpression's conversion operations provide built-in conversion from source to dest? Otherwise, use the F# operator as the user-defined method. 
    let isLinqExpressionsConvertible source dest =
        // https://github.com/dotnet/runtime/blob/4c92aef2b08f9c4374c520e7e664a44f1ad8ce56/src/libraries/System.Linq.Expressions/src/System/Linq/Expressions/UnaryExpression.cs#L757
        // expression.Type.HasIdentityPrimitiveOrNullableConversionTo(type) || expression.Type.HasReferenceConversionTo(type))
        // In other words, source.HasIdentityPrimitiveOrNullableConversionTo dest || source.HasReferenceConversionTo dest
        
        // https://github.com/dotnet/runtime/blob/4c92aef2b08f9c4374c520e7e664a44f1ad8ce56/src/libraries/System.Linq.Expressions/src/System/Dynamic/Utils/TypeUtils.cs#L532
        let isConvertible typ =
            let typ = getNonNullableType typ
            typ.IsEnum ||
            match Type.GetTypeCode typ with
            | TypeCode.Boolean
            | TypeCode.Byte
            | TypeCode.SByte
            | TypeCode.Int16
            | TypeCode.Int32
            | TypeCode.Int64
            | TypeCode.UInt16
            | TypeCode.UInt32
            | TypeCode.UInt64
            | TypeCode.Single
            | TypeCode.Double
            | TypeCode.Char -> true
            | _ -> false
        // https://github.com/dotnet/runtime/blob/4c92aef2b08f9c4374c520e7e664a44f1ad8ce56/src/libraries/System.Linq.Expressions/src/System/Dynamic/Utils/TypeUtils.cs#L229
        // HasIdentityPrimitiveOrNullableConversionTo
        getNonNullableType(source).IsEquivalentTo dest
        || dest.IsEquivalentTo(getNonNullableType source)
        || isConvertible source && isConvertible dest
           && (getNonNullableType dest <> typeof<bool> || source.IsEnum && source.GetEnumUnderlyingType() = typeof<bool>)

        ||
        // https://github.com/dotnet/runtime/blob/4c92aef2b08f9c4374c520e7e664a44f1ad8ce56/src/libraries/System.Linq.Expressions/src/System/Dynamic/Utils/TypeUtils.cs#L260
        // HasReferenceConversionTo
        let rec hasReferenceConversionTo source dest =
        
            // { if (source == typeof(void) || dest == typeof(void)) return false; } invalidates an identity conversion. This is handled by the IsEquivalentTo check above.
            let nnSourceType, nnDestType = getNonNullableType source, getNonNullableType dest

            // Down conversion
            nnSourceType.IsAssignableFrom nnDestType
            // Up conversion
            || nnDestType.IsAssignableFrom nnSourceType
 
            // Interface conversion
            || source.IsInterface || dest.IsInterface

            // Variant delegate conversion
            ||  // https://github.com/dotnet/runtime/blob/4c92aef2b08f9c4374c520e7e664a44f1ad8ce56/src/libraries/System.Linq.Expressions/src/System/Dynamic/Utils/TypeUtils.cs#L458
                // IsLegalExplicitVariantDelegateConversion
                let isDelegate (t: Type) = t.IsSubclassOf typeof<MulticastDelegate>
                isDelegate source && isDelegate dest && source.IsGenericType && dest.IsGenericType
                &&
                let genericDelegate = source.GetGenericTypeDefinition()
                dest.GetGenericTypeDefinition() = genericDelegate
                &&
                (source.GetGenericArguments(), dest.GetGenericArguments(), genericDelegate.GetGenericArguments())
                |||> Array.forall3 (fun sourceArgument destArgument genericParameter ->
                    // If the arguments are identical then this one is automatically good, so skip it.
                    sourceArgument.IsEquivalentTo destArgument
                    ||
                    match genericParameter.GenericParameterAttributes &&& GenericParameterAttributes.VarianceMask with
                    | GenericParameterAttributes.None -> false
                    | GenericParameterAttributes.Covariant -> hasReferenceConversionTo sourceArgument destArgument
                    | GenericParameterAttributes.Contravariant -> not (sourceArgument.IsValueType || destArgument.IsValueType)
                    | x -> failwith $"Unexpected GenericParameterAttributes variance: {x}"
                )
            // TODO: Is expanding this worth it?
            // || (source.IsArray || dest.IsArray) && StrictHasReferenceConversionTo(source, dest, true)
        hasReferenceConversionTo source dest
    let SpecificCallToMethodInfo (minfo: System.Reflection.MethodInfo) =
        let isg1 = minfo.IsGenericMethod
        let gmd = if isg1 then minfo.GetGenericMethodDefinition() else null
        (fun tm ->
            match tm with
            | Call(obj, minfo2, args)
                when (
                        minfo.MetadataToken = minfo2.MetadataToken &&
                        if isg1 then minfo2.IsGenericMethod && gmd = minfo2.GetGenericMethodDefinition()
                        else minfo = minfo2
                     ) ->
                Some (obj, minfo2, args)
            | _ -> None)

    let (|SpecificCallToMethod|_|) (mhandle: RuntimeMethodHandle) =
        let minfo = (System.Reflection.MethodInfo.GetMethodFromHandle mhandle) :?> MethodInfo
        SpecificCallToMethodInfo minfo
    let (|GenericArgs|) (minfo: MethodInfo) = minfo.GetGenericArguments()

    let (|GenericEqualityQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> LanguagePrimitives.GenericEquality x y))
    let (|EqualsQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> x = y))
    let (|GreaterQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> x > y))
    let (|GreaterEqQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> x >= y))
    let (|LessQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> x < y))
    let (|LessEqQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> x <= y))
    let (|NotEqQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> x <> y))

    let (|StaticEqualsQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x:int, y:int) -> NonStructuralComparison.(=) x y))
    let (|StaticGreaterQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x:int, y:int) -> NonStructuralComparison.(>) x y))
    let (|StaticGreaterEqQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x:int, y:int) -> NonStructuralComparison.(>=) x y))
    let (|StaticLessQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x:int, y:int) -> NonStructuralComparison.(<) x y))
    let (|StaticLessEqQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x:int, y:int) -> NonStructuralComparison.(<=) x y))
    let (|StaticNotEqQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x:int, y:int) -> NonStructuralComparison.(<>) x y))

    let (|NullableEqualsQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( ?=  ) x y))
    let (|NullableNotEqQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( ?<> ) x y))
    let (|NullableGreaterQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( ?>  ) x y))
    let (|NullableGreaterEqQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( ?>= ) x y))
    let (|NullableLessQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( ?<  ) x y))
    let (|NullableLessEqQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( ?<= ) x y))

    let (|NullableEqualsNullableQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( ?=?  ) x y))
    let (|NullableNotEqNullableQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( ?<>? ) x y))
    let (|NullableGreaterNullableQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( ?>?  ) x y))
    let (|NullableGreaterEqNullableQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( ?>=? ) x y))
    let (|NullableLessNullableQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( ?<?  ) x y))
    let (|NullableLessEqNullableQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( ?<=? ) x y))

    let (|EqualsNullableQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( =?  ) x y))
    let (|NotEqNullableQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( <>? ) x y))
    let (|GreaterNullableQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( >?  ) x y))
    let (|GreaterEqNullableQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( >=? ) x y))
    let (|LessNullableQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( <?  ) x y))
    let (|LessEqNullableQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( <=? ) x y))

    let (|MakeDecimalQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (a1, a2, a3, a4, a5) -> LanguagePrimitives.IntrinsicFunctions.MakeDecimal a1 a2 a3 a4 a5))

    let (|NullablePlusQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( ?+ ) x y))
    let (|NullablePlusNullableQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( ?+? ) x y))
    let (|PlusNullableQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( +? ) x y))

    let (|NullableMinusQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( ?- ) x y))
    let (|NullableMinusNullableQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( ?-? ) x y))
    let (|MinusNullableQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( -? ) x y))

    let (|NullableMultiplyQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( ?* ) x y))
    let (|NullableMultiplyNullableQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( ?*? ) x y))
    let (|MultiplyNullableQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( *? ) x y))

    let (|NullableDivideQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( ?/ ) x y))
    let (|NullableDivideNullableQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( ?/? ) x y))
    let (|DivideNullableQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( /? ) x y))

    let (|NullableModuloQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( ?% ) x y))
    let (|NullableModuloNullableQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( ?%? ) x y))
    let (|ModuloNullableQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> NullableOperators.( %? ) x y))

    let (|NotQ|_|) =  (|SpecificCallToMethod|_|) (methodhandleof (fun x -> not x))
    let (|NegQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x:int) -> -x))
    let (|PlusQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> x + y))
    let (|DivideQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> x / y))
    let (|MinusQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> x - y))
    let (|MultiplyQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> x * y))
    let (|ModuloQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> x % y))
    let (|ShiftLeftQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> x <<< y))
    let (|ShiftRightQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> x >>> y))
    let (|BitwiseAndQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> x &&& y))
    let (|BitwiseOrQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> x ||| y))
    let (|BitwiseXorQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> x ^^^ y))
    let (|BitwiseNotQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> ~~~ x))
    let (|CheckedNeg|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Checked.( ~-) x))
    let (|CheckedPlusQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> Checked.( + ) x y))
    let (|CheckedMinusQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> Checked.( - ) x y))
    let (|CheckedMultiplyQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> Checked.( * ) x y))

    let (|ConvCharQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Operators.char x))
    let (|ConvDecimalQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Operators.decimal x))
    let (|ConvFloatQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Operators.float x))
    let (|ConvFloat32Q|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Operators.float32 x))
    let (|ConvSByteQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Operators.sbyte x))

    let (|ConvInt16Q|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Operators.int16 x))
    let (|ConvInt32Q|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Operators.int32 x))
    let (|ConvIntQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Operators.int x))
    let (|ConvInt64Q|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Operators.int64 x))
    let (|ConvByteQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Operators.byte x))
    let (|ConvUInt16Q|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Operators.uint16 x))
    let (|ConvUInt32Q|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Operators.uint32 x))
    let (|ConvUInt64Q|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Operators.uint64 x))
    let (|ConvIntPtrQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Operators.nativeint x))
    let (|ConvUIntPtrQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Operators.unativeint x))

    let (|ConvInt8Q|_|) = SpecificCallToMethodInfo (typeof<ConvEnv>.Assembly.GetType("Microsoft.FSharp.Core.ExtraTopLevelOperators").GetMethod("ToSByte"))
    let (|ConvUInt8Q|_|) = SpecificCallToMethodInfo (typeof<ConvEnv>.Assembly.GetType("Microsoft.FSharp.Core.ExtraTopLevelOperators").GetMethod("ToByte"))
    let (|ConvDoubleQ|_|) = SpecificCallToMethodInfo (typeof<ConvEnv>.Assembly.GetType("Microsoft.FSharp.Core.ExtraTopLevelOperators").GetMethod("ToDouble"))
    let (|ConvSingleQ|_|) = SpecificCallToMethodInfo (typeof<ConvEnv>.Assembly.GetType("Microsoft.FSharp.Core.ExtraTopLevelOperators").GetMethod("ToSingle"))

    let (|ConvNullableCharQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Nullable.char x))
    let (|ConvNullableDecimalQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Nullable.decimal x))
    let (|ConvNullableFloatQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Nullable.float x))
    let (|ConvNullableDoubleQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Nullable.double x))
    let (|ConvNullableFloat32Q|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Nullable.float32 x))
    let (|ConvNullableSingleQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Nullable.single x))
    let (|ConvNullableSByteQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Nullable.sbyte x))
    let (|ConvNullableInt8Q|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Nullable.int8 x))
    let (|ConvNullableInt16Q|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Nullable.int16 x))
    let (|ConvNullableInt32Q|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Nullable.int32 x))
    let (|ConvNullableIntQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Nullable.int x))
    let (|ConvNullableInt64Q|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Nullable.int64 x))
    let (|ConvNullableByteQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Nullable.byte x))
    let (|ConvNullableUInt8Q|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Nullable.uint8 x))
    let (|ConvNullableUInt16Q|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Nullable.uint16 x))
    let (|ConvNullableUInt32Q|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Nullable.uint32 x))
    let (|ConvNullableUInt64Q|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Nullable.uint64 x))
    let (|ConvNullableIntPtrQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Nullable.nativeint x))
    let (|ConvNullableUIntPtrQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Nullable.unativeint x))

    let (|UnboxGeneric|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> LanguagePrimitives.IntrinsicFunctions.UnboxGeneric x))
    let (|TypeTestGeneric|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> LanguagePrimitives.IntrinsicFunctions.TypeTestGeneric x))
    let (|CheckedConvCharQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Checked.char x))
    let (|CheckedConvSByteQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Checked.sbyte x))
    let (|CheckedConvInt8Q|_|) = SpecificCallToMethodInfo (typeof<ConvEnv>.Assembly.GetType("Microsoft.FSharp.Core.ExtraTopLevelOperators+Checked").GetMethod("ToSByte"))
    let (|CheckedConvUInt8Q|_|) = SpecificCallToMethodInfo (typeof<ConvEnv>.Assembly.GetType("Microsoft.FSharp.Core.ExtraTopLevelOperators+Checked").GetMethod("ToByte"))
    let (|CheckedConvInt16Q|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Checked.int16 x))
    let (|CheckedConvInt32Q|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Checked.int32 x))
    let (|CheckedConvInt64Q|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Checked.int64 x))
    let (|CheckedConvByteQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Checked.byte x))
    let (|CheckedConvUInt16Q|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Checked.uint16 x))
    let (|CheckedConvUInt32Q|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Checked.uint32 x))
    let (|CheckedConvUInt64Q|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Checked.uint64 x))
    let (|CheckedConvIntPtrQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Checked.nativeint x))
    let (|CheckedConvUIntPtrQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> Checked.unativeint x))
    let (|ImplicitExpressionConversionHelperQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> ImplicitExpressionConversionHelper x))
    let (|MemberInitializationHelperQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> MemberInitializationHelper x))
    let (|NewAnonymousObjectHelperQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun x -> NewAnonymousObjectHelper x))
    let (|ArrayLookupQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun (x, y) -> LanguagePrimitives.IntrinsicFunctions.GetArray x y))

    //let (|ArrayAssignQ|_|) = (|SpecificCallToMethod|_|) (methodhandleof (fun -> LanguagePrimitives.IntrinsicFunctions.SetArray : int[] -> int -> int -> unit))
    //let (|ArrayTypeQ|_|) (ty:System.Type) = if ty.IsArray && ty.GetArrayRank() = 1 then Some (ty.GetElementType()) else None
    let substHelperMeth =
        methodhandleof (fun (x:Expr, y:Var[], z:obj[]) -> SubstHelper<obj> (x, y, z))
        |> System.Reflection.MethodInfo.GetMethodFromHandle
        :?> MethodInfo

    let substHelperRawMeth =
        methodhandleof (fun (x:Expr, y:Var[], z:obj[]) -> SubstHelperRaw (x, y, z))
        |> System.Reflection.MethodInfo.GetMethodFromHandle
        :?> MethodInfo

    let (-->) ty1 ty2 = Reflection.FSharpType.MakeFunctionType(ty1, ty2)

    /// Extract member initialization expression stored in 'MemberInitializationHelper' (by QueryExtensions.fs)
    let rec (|Sequentials|) = function
        | Patterns.Sequential(a, Sequentials (b, c)) -> (a :: b, c)
        | a -> [], a

    let (|MemberInitializationQ|_|) = function
        | MemberInitializationHelperQ (None, _, [  Sequentials (propSets, init) ]) -> Some (init, propSets)
        | _ -> None

    /// Extract construction of anonymous object noted by use of in 'NewAnonymousObjectHelper' (by QueryExtensions.fs)
    let (|NewAnonymousObjectQ|_|) = function
        | NewAnonymousObjectHelperQ (None, _, [ Patterns.NewObject(ctor, args) ]) -> Some (ctor, args)
        | _ -> None

    /// Extract nullable constructions
    let (|NullableConstruction|_|) = function
      | NewObject(c, [arg]) when equivHeadTypes c.DeclaringType (typeof<Nullable<int>>) -> Some arg
      | _ -> None

    /// Convert F# quotations to LINQ expression trees.
    /// A more polished LINQ-Quotation translator will be published
    /// concert with later versions of LINQ.
    let rec ConvExprToLinqInContext (env:ConvEnv) (inp:Expr) =
        //printf "ConvExprToLinqInContext : %A\n" inp
        match inp with

        // Generic cases
        | Patterns.Var v ->
            try
                Map.find v env.varEnv
            with
            |   :? KeyNotFoundException -> invalidOp ("The variable '"+ v.Name + "' was not found in the translation context'")

        | DerivedPatterns.AndAlso(x1, x2) ->
            Expression.AndAlso(ConvExprToLinqInContext env x1, ConvExprToLinqInContext env x2) |> asExpr

        | DerivedPatterns.OrElse(x1, x2) ->
            Expression.OrElse(ConvExprToLinqInContext env x1, ConvExprToLinqInContext env x2) |> asExpr

        | Patterns.Value(x, ty) ->
            Expression.Constant(x, ty) |> asExpr

        | UnboxGeneric(_, GenericArgs [|toTy|], [x])
        | Patterns.Coerce(x, toTy) ->
            let converted = ConvExprToLinqInContext env x

            // Most of conversion scenarios in C# are covered by Expression.Convert
            if x.Type.Equals toTy then converted // source and target types match - do nothing
            elif not (x.Type.IsValueType || toTy.IsValueType) && toTy.IsAssignableFrom x.Type then converted // converting reference type to supertype - do nothing
            else Expression.Convert(converted, toTy) |> asExpr // emit Expression.Convert

        | Patterns.TypeTest(x, toTy) ->
            Expression.TypeIs(ConvExprToLinqInContext env x, toTy) |> asExpr

        | TypeTestGeneric(_, GenericArgs [|toTy|], [x]) ->
            Expression.TypeIs(ConvExprToLinqInContext env x, toTy) |> asExpr

        // Expr.*Get
        | Patterns.FieldGet(objOpt, fieldInfo) ->
            Expression.Field(ConvObjArg env objOpt None, fieldInfo) |> asExpr

        | Patterns.TupleGet(arg, n) ->
             let argP = ConvExprToLinqInContext env arg
             let rec build ty argP n =
                 match Reflection.FSharpValue.PreComputeTuplePropertyInfo(ty, n) with
                 | propInfo, None ->
                     Expression.Property(argP, propInfo) |> asExpr
                 | propInfo, Some (nestedTy, n2) ->
                     build nestedTy (Expression.Property(argP, propInfo) |> asExpr) n2
             build arg.Type argP n

        | Patterns.PropertyGet(objOpt, propInfo, args) ->
            let coerceTo =
                if objOpt.IsSome && FSharpType.IsUnion propInfo.DeclaringType && FSharpType.IsUnion propInfo.DeclaringType.BaseType then
                    Some propInfo.DeclaringType
                else
                    None
            match args with
            | [] ->
                Expression.Property(ConvObjArg env objOpt coerceTo, propInfo) |> asExpr
            | _ ->
                let argsP = ConvExprsToLinq env args
                Expression.Call(ConvObjArg env objOpt coerceTo, propInfo.GetGetMethod(true), argsP) |> asExpr

        // Expr.(Call, Application)
        | Patterns.Call(objOpt, minfo, args) ->

            match inp with
            // Special cases for this translation

            // Object initialization generated by LinqQueries
            | MemberInitializationQ(ctor, propInfos) ->
                let bindings =
                  [| for p in propInfos ->
                      match p with
                      | Patterns.PropertySet(_, pinfo, args, assign) ->
                          if args <> [] then raise (NotSupportedException "Parameterized properties not supported in member initialization.")
                          Expression.Bind(pinfo, ConvExprToLinqInContext env assign) :> MemberBinding
                      | _ ->
                          raise (NotSupportedException "Expected PropertySet in member initialization") |]
                match ConvExprToLinqInContext env ctor with
                | :? NewExpression as converted -> Expression.MemberInit(converted, bindings) |> asExpr
                | _ -> raise (NotSupportedException "Expected Constructor call in member initialization")

            // Anonymous type initialization generated by LinqQueries
            | NewAnonymousObjectQ(ctor, args) ->
                let argsR = ConvExprsToLinq env args
                let props = ctor.DeclaringType.GetProperties()
                Expression.New(ctor, argsR, [| for p in props -> (p :> MemberInfo) |]) |> asExpr

            // Do the same thing as C# compiler for string addition
            | PlusQ (_, GenericArgs [|ty1; ty2; ty3|], [x1; x2]) when ty1 = typeof<string> && ty2 = typeof<string> && ty3 = typeof<string> ->
                 Expression.Add(ConvExprToLinqInContext env x1, ConvExprToLinqInContext env x2, StringConcat) |> asExpr

            | GenericEqualityQ (_, m, [x1; x2])
            | EqualsQ (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsStructurallyEquatable env false x1 x2 false Expression.Equal m
            | NotEqQ (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsStructurallyEquatable env false x1 x2 false Expression.NotEqual m
            | GreaterQ (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsComparable env false x1 x2 false Expression.GreaterThan m
            | GreaterEqQ (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsComparable env false x1 x2 false Expression.GreaterThanOrEqual m
            | LessQ (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsComparable env false x1 x2 false Expression.LessThan m
            | LessEqQ (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsComparable env false x1 x2 false Expression.LessThanOrEqual m
            | NotQ (_, _, [x1]) -> Expression.Not(ConvExprToLinqInContext env x1) |> asExpr

            | StaticEqualsQ (_, _, [x1; x2]) -> transBoolOp isLinqExpressionsEquatable inp env x1 x2 Expression.Equal (methodhandleof (fun (x, y) -> LanguagePrimitives.EqualityDynamic x y))
            | StaticNotEqQ (_, _, [x1; x2]) -> transBoolOp isLinqExpressionsEquatable inp env x1 x2 Expression.NotEqual (methodhandleof (fun (x, y) -> LanguagePrimitives.InequalityDynamic x y))
            | StaticGreaterQ (_, _, [x1; x2]) -> transBoolOp isLinqExpressionsComparable inp env x1 x2 Expression.GreaterThan (methodhandleof (fun (x, y) -> LanguagePrimitives.GreaterThanDynamic x y))
            | StaticGreaterEqQ (_, _, [x1; x2]) -> transBoolOp isLinqExpressionsComparable inp env x1 x2 Expression.GreaterThanOrEqual (methodhandleof (fun (x, y) -> LanguagePrimitives.GreaterThanOrEqualDynamic x y))
            | StaticLessQ (_, _, [x1; x2]) -> transBoolOp isLinqExpressionsComparable inp env x1 x2 Expression.LessThan (methodhandleof (fun (x, y) -> LanguagePrimitives.LessThanDynamic x y))
            | StaticLessEqQ (_, _, [x1; x2]) -> transBoolOp isLinqExpressionsComparable inp env x1 x2 Expression.LessThanOrEqual (methodhandleof (fun (x, y) -> LanguagePrimitives.LessThanOrEqualDynamic x y))

            | NullableEqualsQ (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsStructurallyEquatable env false x1 x2 true Expression.Equal m
            | NullableNotEqQ (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsStructurallyEquatable env false x1 x2 true Expression.NotEqual m
            | NullableGreaterQ (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsComparable env false x1 x2 true Expression.GreaterThan m
            | NullableGreaterEqQ (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsComparable env false x1 x2 true Expression.GreaterThanOrEqual m
            | NullableLessQ  (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsComparable env false x1 x2 true Expression.LessThan m
            | NullableLessEqQ (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsComparable env false x1 x2 true Expression.LessThanOrEqual m

            | EqualsNullableQ (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsStructurallyEquatable env true x1 x2 false Expression.Equal m
            | NotEqNullableQ (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsStructurallyEquatable env true x1 x2 false Expression.NotEqual m
            | GreaterNullableQ (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsComparable env true x1 x2 false Expression.GreaterThan m
            | GreaterEqNullableQ (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsComparable env true x1 x2 false Expression.GreaterThanOrEqual m
            | LessNullableQ  (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsComparable env true x1 x2 false Expression.LessThan m
            | LessEqNullableQ (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsComparable env true x1 x2 false Expression.LessThanOrEqual m

            | NullableEqualsNullableQ (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsStructurallyEquatable env false x1 x2 false Expression.Equal m
            | NullableNotEqNullableQ (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsStructurallyEquatable env false x1 x2 false Expression.NotEqual m
            | NullableGreaterNullableQ (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsComparable env false x1 x2 false Expression.GreaterThan m
            | NullableGreaterEqNullableQ (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsComparable env false x1 x2 false Expression.GreaterThanOrEqual m
            | NullableLessNullableQ (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsComparable env false x1 x2 false Expression.LessThan m
            | NullableLessEqNullableQ (_, m, [x1; x2]) -> transBoolOpNoWitness isLinqExpressionsComparable env false x1 x2 false Expression.LessThanOrEqual m
            
            // Detect the F# quotation encoding of decimal literals
            | MakeDecimalQ (_, _, [Int32 lo; Int32 med; Int32 hi; Bool isNegative; Byte scale]) ->
                Expression.Constant (new System.Decimal(lo, med, hi, isNegative, scale)) |> asExpr

            | NegQ (_, _, [x]) -> transUnaryOp isLinqExpressionsArithmeticTypeButNotUnsignedInt inp env x Expression.Negate (methodhandleof (fun x -> LanguagePrimitives.UnaryNegationDynamic x))
            | PlusQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsArithmeticType inp env false x1 x2 false Expression.Add (methodhandleof (fun (x, y) -> LanguagePrimitives.AdditionDynamic x y))
            | MinusQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsArithmeticType inp env false x1 x2 false Expression.Subtract (methodhandleof (fun (x, y) -> LanguagePrimitives.SubtractionDynamic x y))
            | MultiplyQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsArithmeticType inp env false x1 x2 false Expression.Multiply (methodhandleof (fun (x, y) -> LanguagePrimitives.MultiplyDynamic x y))
            | DivideQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsArithmeticType inp env false x1 x2 false Expression.Divide (methodhandleof (fun (x, y) -> LanguagePrimitives.DivisionDynamic x y))
            | ModuloQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsArithmeticType inp env false x1 x2 false Expression.Modulo (methodhandleof (fun (x, y) -> LanguagePrimitives.ModulusDynamic x y))

            | ShiftLeftQ (_, _, [x1; x2]) -> transShiftOp inp env false x1 x2 false Expression.LeftShift (methodhandleof (fun (x, y) -> LanguagePrimitives.LeftShiftDynamic x y))
            | ShiftRightQ (_, _, [x1; x2]) -> transShiftOp inp env false x1 x2 false Expression.RightShift (methodhandleof (fun (x, y) -> LanguagePrimitives.RightShiftDynamic x y))
            | BitwiseAndQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsIntegerOrBool inp env false x1 x2 false Expression.And (methodhandleof (fun (x, y) -> LanguagePrimitives.BitwiseAndDynamic x y))
            | BitwiseOrQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsIntegerOrBool inp env false x1 x2 false Expression.Or (methodhandleof (fun (x, y) -> LanguagePrimitives.BitwiseOrDynamic x y))
            | BitwiseXorQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsIntegerOrBool inp env false x1 x2 false Expression.ExclusiveOr (methodhandleof (fun (x, y) -> LanguagePrimitives.ExclusiveOrDynamic x y))
            | BitwiseNotQ (_, _, [x]) -> transUnaryOp isLinqExpressionsIntegerOrBool inp env x Expression.Not (methodhandleof (fun x -> LanguagePrimitives.LogicalNotDynamic x))
            
            | CheckedNeg (_, _, [x]) -> transUnaryOp isLinqExpressionsArithmeticTypeButNotUnsignedInt inp env x Expression.NegateChecked (methodhandleof (fun x -> LanguagePrimitives.CheckedUnaryNegationDynamic x))
            | CheckedPlusQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsArithmeticType inp env false x1 x2 false Expression.AddChecked (methodhandleof (fun (x, y) -> LanguagePrimitives.CheckedAdditionDynamic x y))
            | CheckedMinusQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsArithmeticType inp env false x1 x2 false Expression.SubtractChecked (methodhandleof (fun (x, y) -> LanguagePrimitives.CheckedSubtractionDynamic x y))
            | CheckedMultiplyQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsArithmeticType inp env false x1 x2 false Expression.MultiplyChecked (methodhandleof (fun (x, y) -> LanguagePrimitives.CheckedMultiplyDynamic x y))
            
            | NullablePlusQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsArithmeticType inp env false x1 x2 true Expression.Add (methodhandleof (fun (x, y) -> LanguagePrimitives.AdditionDynamic x y))
            | PlusNullableQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsArithmeticType inp env true x1 x2 false Expression.Add (methodhandleof (fun (x, y) -> LanguagePrimitives.AdditionDynamic x y))
            | NullablePlusNullableQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsArithmeticType inp env false x1 x2 false Expression.Add (methodhandleof (fun (x, y) -> LanguagePrimitives.AdditionDynamic x y))
            
            | NullableMinusQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsArithmeticType inp env false x1 x2 true Expression.Subtract (methodhandleof (fun (x, y) -> LanguagePrimitives.SubtractionDynamic x y))
            | MinusNullableQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsArithmeticType inp env true x1 x2 false Expression.Subtract (methodhandleof (fun (x, y) -> LanguagePrimitives.SubtractionDynamic x y))
            | NullableMinusNullableQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsArithmeticType inp env false x1 x2 false Expression.Subtract (methodhandleof (fun (x, y) -> LanguagePrimitives.SubtractionDynamic x y))
            
            | NullableMultiplyQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsArithmeticType inp env false x1 x2 true Expression.Multiply (methodhandleof (fun (x, y) -> LanguagePrimitives.MultiplyDynamic x y))
            | MultiplyNullableQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsArithmeticType inp env true x1 x2 false Expression.Multiply (methodhandleof (fun (x, y) -> LanguagePrimitives.MultiplyDynamic x y))
            | NullableMultiplyNullableQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsArithmeticType inp env false x1 x2 false Expression.Multiply (methodhandleof (fun (x, y) -> LanguagePrimitives.MultiplyDynamic x y))
            
            | NullableDivideQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsArithmeticType inp env false x1 x2 true Expression.Divide (methodhandleof (fun (x, y) -> LanguagePrimitives.DivisionDynamic x y))
            | DivideNullableQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsArithmeticType inp env true x1 x2 false Expression.Divide (methodhandleof (fun (x, y) -> LanguagePrimitives.DivisionDynamic x y))
            | NullableDivideNullableQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsArithmeticType inp env false x1 x2 false Expression.Divide (methodhandleof (fun (x, y) -> LanguagePrimitives.DivisionDynamic x y))
            
            | NullableModuloQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsArithmeticType inp env false x1 x2 true Expression.Modulo (methodhandleof (fun (x, y) -> LanguagePrimitives.ModulusDynamic x y))
            | ModuloNullableQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsArithmeticType inp env true x1 x2 false Expression.Modulo (methodhandleof (fun (x, y) -> LanguagePrimitives.ModulusDynamic x y))
            | NullableModuloNullableQ (_, _, [x1; x2]) -> transBinOp isLinqExpressionsArithmeticType inp env false x1 x2 false Expression.Modulo (methodhandleof (fun (x, y) -> LanguagePrimitives.ModulusDynamic x y))

            | ConvNullableCharQ (_, _, [x]) | ConvNullableDecimalQ (_, _, [x]) | ConvNullableFloatQ (_, _, [x]) | ConvNullableDoubleQ (_, _, [x]) -> transConv inp env false x
            | ConvNullableFloat32Q (_, _, [x]) | ConvNullableSingleQ (_, _, [x]) | ConvNullableSByteQ (_, _, [x]) | ConvNullableInt8Q (_, _, [x]) -> transConv inp env false x
            | ConvNullableInt16Q (_, _, [x]) | ConvNullableInt32Q (_, _, [x]) | ConvNullableIntQ (_, _, [x]) | ConvNullableInt64Q (_, _, [x]) -> transConv inp env false x
            | ConvNullableByteQ (_, _, [x]) | ConvNullableUInt8Q (_, _, [x]) | ConvNullableUInt16Q (_, _, [x]) | ConvNullableUInt32Q (_, _, [x]) -> transConv inp env false x
            | ConvNullableUInt64Q (_, _, [x]) | ConvNullableIntPtrQ (_, _, [x]) | ConvNullableUIntPtrQ (_, _, [x]) -> transConv inp env false x

            | ConvCharQ (_, _, [x]) | ConvDecimalQ (_, _, [x]) | ConvFloatQ (_, _, [x]) | ConvDoubleQ (_, _, [x]) -> transConv inp env false x
            | ConvFloat32Q (_, _, [x]) | ConvSingleQ (_, _, [x]) | ConvSByteQ (_, _, [x]) | ConvInt8Q (_, _, [x]) -> transConv inp env false x
            | ConvInt16Q (_, _, [x]) | ConvInt32Q (_, _, [x]) | ConvIntQ (_, _, [x]) | ConvInt64Q (_, _, [x]) -> transConv inp env false x
            | ConvByteQ (_, _, [x]) | ConvUInt8Q (_, _, [x]) | ConvUInt16Q (_, _, [x]) | ConvUInt32Q (_, _, [x]) -> transConv inp env false x
            | ConvUInt64Q (_, _, [x]) | ConvIntPtrQ (_, _, [x]) | ConvUIntPtrQ (_, _, [x]) -> transConv inp env false x

            | CheckedConvCharQ (_, _, [x]) | CheckedConvSByteQ (_, _, [x]) | CheckedConvInt8Q (_, _, [x]) | CheckedConvInt16Q (_, _, [x]) -> transConv inp env true x
            | CheckedConvInt32Q (_, _, [x]) | CheckedConvInt64Q (_, _, [x]) | CheckedConvByteQ (_, _, [x]) | CheckedConvUInt8Q (_, _, [x]) -> transConv inp env true x
            | CheckedConvUInt16Q (_, _, [x]) | CheckedConvUInt32Q (_, _, [x]) | CheckedConvUInt64Q (_, _, [x]) | CheckedConvIntPtrQ (_, _, [x]) -> transConv inp env true x
            | CheckedConvUIntPtrQ (_, _, [x]) -> transConv inp env true x

            | ArrayLookupQ (_, GenericArgs [|_; _; _|], [x1; x2]) ->
                Expression.ArrayIndex(ConvExprToLinqInContext env x1, ConvExprToLinqInContext env x2) |> asExpr

            // Throw away markers inserted to satisfy C#'s design where they pass an argument
            // or type T to an argument expecting Expression<T>.
            | ImplicitExpressionConversionHelperQ (_, GenericArgs [|_|], [x1]) -> ConvExprToLinqInContext env x1
             
            /// Use witnesses if they are available
            | CallWithWitnesses (objArgOpt, _, minfo2, witnessArgs, args) -> 
                let fullArgs = witnessArgs @ args
                let replacementExpr =
                    match objArgOpt with
                    | None -> Expr.Call(minfo2, fullArgs)
                    | Some objArg -> Expr.Call(objArg, minfo2, fullArgs)
                ConvExprToLinqInContext env replacementExpr

            | _ -> 
                let argsP = ConvExprsToLinq env args 
                Expression.Call(ConvObjArg env objOpt None, minfo, argsP) |> asExpr  

#if !NO_CURRIED_FUNCTION_OPTIMIZATIONS
        // f x1 x2 x3 x4 --> InvokeFast4
        | Patterns.Application(Patterns.Application(Patterns.Application(Patterns.Application(f, arg1), arg2), arg3), arg4) ->
            // TODO: amortize this computation based on f.Type
            let meth =
                let domainTy1, rangeTy = getFunctionType f.Type
                let domainTy2, rangeTy = getFunctionType rangeTy
                let domainTy3, rangeTy = getFunctionType rangeTy
                let domainTy4, rangeTy = getFunctionType rangeTy
                let ty = domainTy1 --> domainTy2
                (ty.GetMethods() |> Array.find (fun minfo -> minfo.Name = "InvokeFast" && minfo.GetParameters().Length = 5)).MakeGenericMethod [| domainTy3; domainTy4; rangeTy |]
            let argsP = ConvExprsToLinq env [f; arg1; arg2; arg3; arg4]
            Expression.Call((null:Expression), meth, argsP) |> asExpr

        // f x1 x2 x3 --> InvokeFast3
        | Patterns.Application(Patterns.Application(Patterns.Application(f, arg1), arg2), arg3) ->
            // TODO: amortize this computation based on f.Type
            let meth =
                let domainTy1, rangeTy = getFunctionType f.Type
                let domainTy2, rangeTy = getFunctionType rangeTy
                let domainTy3, rangeTy = getFunctionType rangeTy
                let ty = domainTy1 --> domainTy2
                (ty.GetMethods() |> Array.find (fun minfo -> minfo.Name = "InvokeFast" && minfo.GetParameters().Length = 4)).MakeGenericMethod [| domainTy3; rangeTy |]
            let argsP = ConvExprsToLinq env [f; arg1; arg2; arg3]
            Expression.Call((null:Expression), meth, argsP) |> asExpr

        // f x1 x2 --> InvokeFast2
        | Patterns.Application(Patterns.Application(f, arg1), arg2) ->
            // TODO: amortize this computation based on f.Type
            let meth =
                let domainTy1, rangeTy = getFunctionType f.Type
                let domainTy2, rangeTy = getFunctionType rangeTy
                let ty = domainTy1 --> domainTy2
                (ty.GetMethods() |> Array.find (fun minfo -> minfo.Name = "InvokeFast" && minfo.GetParameters().Length = 3)).MakeGenericMethod [| rangeTy |]
            let argsP = ConvExprsToLinq env [f; arg1; arg2]
            Expression.Call((null:Expression), meth, argsP) |> asExpr
#endif

        // f x1 --> Invoke
        | Patterns.Application(f, arg) ->
            let fP = ConvExprToLinqInContext env f
            let argP = ConvExprToLinqInContext env arg
            // TODO: amortize this computation based on f.Type
            let meth = f.Type.GetMethod("Invoke")
            Expression.Call(fP, meth, [| argP |]) |> asExpr

        // Expr.New*
        | Patterns.NewRecord(recdTy, args) ->
            let ctorInfo = Reflection.FSharpValue.PreComputeRecordConstructorInfo(recdTy, showAll)
            Expression.New(ctorInfo, ConvExprsToLinq env args) |> asExpr

        | Patterns.NewArray(ty, args) ->
            Expression.NewArrayInit(ty, ConvExprsToLinq env args) |> asExpr

        | Patterns.DefaultValue ty ->
            Expression.New ty |> asExpr

        | Patterns.NewUnionCase(unionCaseInfo, args) ->
            let methInfo = Reflection.FSharpValue.PreComputeUnionConstructorInfo(unionCaseInfo, showAll)
            let argsR = ConvExprsToLinq env args
            Expression.Call((null:Expression), methInfo, argsR) |> asExpr

#if !NO_PATTERN_MATCHING_IN_INPUT_LANGUAGE
        | Patterns.UnionCaseTest(e, unionCaseInfo) ->
            let methInfo = Reflection.FSharpValue.PreComputeUnionTagMemberInfo(unionCaseInfo.DeclaringType, showAll)
            let obj = ConvExprToLinqInContext env e
            let tagE =
                match methInfo with
                | :? PropertyInfo as p ->
                    Expression.Property(obj, p) |> asExpr
                | :? MethodInfo as m ->
                    Expression.Call((null:Expression), m, [| obj |]) |> asExpr
                | _ -> failwith "unreachable case"
            Expression.Equal(tagE, Expression.Constant(unionCaseInfo.Tag)) |> asExpr
#endif

        | (Patterns.NewObject(ctorInfo, args) as x) ->
            match x with
            // LINQ providers prefer C# "Nullable x" to be "Convert x", since that's what C# uses
            // to construct nullable values.
            | NullableConstruction arg -> Expression.Convert(ConvExprToLinqInContext env arg, x.Type) |> asExpr
            | _ -> Expression.New(ctorInfo, ConvExprsToLinq env args) |> asExpr

        | Patterns.NewDelegate(dty, vs, b) ->
            let vsP = List.map ConvVarToLinq vs
            let env = {env with varEnv = List.foldBack2 (fun (v:Var) vP -> Map.add v (vP |> asExpr)) vs vsP env.varEnv }
            let bodyP = ConvExprToLinqInContext env b
            Expression.Lambda(dty, bodyP, vsP) |> asExpr

        | Patterns.NewTuple args ->
             let tupTy = 
                let argTypes = args |> List.map (fun arg -> arg.Type) |> Array.ofList
                if inp.Type.IsValueType then 
                    Reflection.FSharpType.MakeStructTupleType(inp.Type.Assembly, argTypes)
                else
                    Reflection.FSharpType.MakeTupleType(argTypes)
             let argsP = ConvExprsToLinq env args
             let rec build ty (argsP: Expression[]) =
                 match Reflection.FSharpValue.PreComputeTupleConstructorInfo ty with
                 | ctorInfo, None -> Expression.New(ctorInfo, argsP) |> asExpr
                 | ctorInfo, Some (nestedTy) ->
                     let n = ctorInfo.GetParameters().Length - 1
                     Expression.New(ctorInfo, Array.append argsP.[0..n-1] [| build nestedTy argsP.[n..] |]) |> asExpr
             build tupTy argsP

        | Patterns.IfThenElse(g, t, e) ->
            Expression.Condition(ConvExprToLinqInContext env g, ConvExprToLinqInContext env t, ConvExprToLinqInContext env e) |> asExpr

        | Patterns.QuoteTyped x ->
            let fvs = x.GetFreeVars()

            Expression.Call(substHelperMeth.MakeGenericMethod [| x.Type |],
                            [| (Expression.Constant x) |> asExpr
                               (Expression.NewArrayInit(typeof<Var>, [| for fv in fvs -> Expression.Constant fv |> asExpr |]) |> asExpr)
                               (Expression.NewArrayInit(typeof<obj>, [| for fv in fvs -> Expression.Convert(env.varEnv.[fv], typeof<obj>) |> asExpr |]) |> asExpr) |])
                    |> asExpr

        | Patterns.QuoteRaw x ->
            let fvs = x.GetFreeVars()

            Expression.Call(substHelperRawMeth,
                            [| (Expression.Constant x) |> asExpr
                               (Expression.NewArrayInit(typeof<Var>, [| for fv in fvs -> Expression.Constant fv |> asExpr |]) |> asExpr)
                               (Expression.NewArrayInit(typeof<obj>, [| for fv in fvs -> Expression.Convert(env.varEnv.[fv], typeof<obj>) |> asExpr |]) |> asExpr) |])
                    |> asExpr

        | Patterns.Let (v, e, b) ->
            let vP = ConvVarToLinq v
            let envinner = { env with varEnv = Map.add v (vP |> asExpr) env.varEnv }
            let bodyP = ConvExprToLinqInContext envinner b
            let eP = ConvExprToLinqInContext env e
            let ty = Expression.GetFuncType [| v.Type; b.Type |]
            let lam = Expression.Lambda(ty, bodyP, [| vP |]) |> asExpr
            Expression.Call(lam, ty.GetMethod("Invoke", instanceBindingFlags), [| eP |]) |> asExpr

        | Patterns.Lambda(v, body) ->
            let vP = ConvVarToLinq v
            let env = { env with varEnv = Map.add v (vP |> asExpr) env.varEnv }
            let bodyP = ConvExprToLinqInContext env body
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
        | _ ->
            failConvert inp

    and failConvert inp =
            raise (new NotSupportedException(Printf.sprintf "Could not convert the following F# Quotation to a LINQ Expression Tree\n--------\n%A\n-------------\n" inp))

    and transUnaryOp linqExpressionsCondition inp env x (exprErasedConstructor: _ * _ -> _) fallback =
        let e = ConvExprToLinqInContext env x
        if linqExpressionsCondition e.Type then
            exprErasedConstructor(e, null)
        else
            let method = Reflection.MethodInfo.GetMethodFromHandle fallback :?> Reflection.MethodInfo
            exprErasedConstructor(e, method.MakeGenericMethod [| getNonNullableType x.Type; getNonNullableType inp.Type |])
        |> asExpr
    and transShiftOp inp env addConvertLeft x1 x2 addConvertRight (exprErasedConstructor: _ * _ * _ -> _) fallback =
        let e1 = ConvExprToLinqInContext env x1
        let e2 = ConvExprToLinqInContext env x2
        let e1 = if addConvertLeft  then Expression.Convert(e1, typedefof<Nullable<int>>.MakeGenericType [| e1.Type |]) |> asExpr else e1
        let e2 = if addConvertRight then Expression.Convert(e2, typedefof<Nullable<int>>.MakeGenericType [| e2.Type |]) |> asExpr else e2
        if e1.Type = e2.Type && isLinqExpressionsSimpleShift e1.Type e2.Type then
            exprErasedConstructor(e1, e2, null)
        else
            let method = Reflection.MethodInfo.GetMethodFromHandle fallback :?> Reflection.MethodInfo
            exprErasedConstructor(e1, e2, method.MakeGenericMethod [| getNonNullableType x1.Type; getNonNullableType x2.Type; getNonNullableType inp.Type |])
        |> asExpr
    and transBinOp linqExpressionsCondition inp env addConvertLeft x1 x2 addConvertRight (exprErasedConstructor: _ * _ * _ -> _) fallback =
        let e1 = ConvExprToLinqInContext env x1
        let e2 = ConvExprToLinqInContext env x2
        let e1 = if addConvertLeft  then Expression.Convert(e1, typedefof<Nullable<int>>.MakeGenericType [| e1.Type |]) |> asExpr else e1
        let e2 = if addConvertRight then Expression.Convert(e2, typedefof<Nullable<int>>.MakeGenericType [| e2.Type |]) |> asExpr else e2
        if e1.Type = e2.Type && linqExpressionsCondition e1.Type then
            exprErasedConstructor(e1, e2, null)
        else
            let method = Reflection.MethodInfo.GetMethodFromHandle fallback :?> Reflection.MethodInfo
            exprErasedConstructor(e1, e2, method.MakeGenericMethod [| getNonNullableType x1.Type; getNonNullableType x2.Type; getNonNullableType inp.Type |])
        |> asExpr
    // The F# boolean structural equality / comparison operators do not take witnesses and the referenced methods are callable directly
    and transBoolOpNoWitness linqExpressionsCondition env addConvertLeft x1 x2 addConvertRight (exprErasedConstructor: _ * _ * _ * _ -> _) method =
        let e1 = ConvExprToLinqInContext env x1
        let e2 = ConvExprToLinqInContext env x2
        let e1' = if addConvertLeft  then Expression.Convert(e1, typedefof<Nullable<int>>.MakeGenericType [| e1.Type |]) |> asExpr else e1
        let e2' = if addConvertRight then Expression.Convert(e2, typedefof<Nullable<int>>.MakeGenericType [| e2.Type |]) |> asExpr else e2
        if e1'.Type = e2'.Type && linqExpressionsCondition e1.Type then
            // The false for (liftToNull: bool) indicates whether equality operators return a Nullable<bool> like in VB.NET (null when either argument is null) instead of bool like in C# (nulls equate to nulls). F# follows C# here.
            exprErasedConstructor(e1', e2', false, null)
        else
            exprErasedConstructor(e1, e2, false, method)
        |> asExpr
    // But the static boolean operators do take witnesses!
    and transBoolOp linqExpressionsCondition inp env x1 x2 (exprErasedConstructor: _ * _ * _ * _ -> _) fallback =
        let e1 = ConvExprToLinqInContext env x1
        let e2 = ConvExprToLinqInContext env x2
        if e1.Type = e2.Type && linqExpressionsCondition e1.Type then
            // The false for (liftToNull: bool) indicates whether equality operators return a Nullable<bool> like in VB.NET (null when either argument is null) instead of bool like in C# (nulls equate to nulls). F# follows C# here.
            exprErasedConstructor(e1, e2, false, null)
        else
            let method = Reflection.MethodInfo.GetMethodFromHandle fallback :?> Reflection.MethodInfo
            exprErasedConstructor(e1, e2, false, method.MakeGenericMethod [| getNonNullableType x1.Type; getNonNullableType x2.Type; getNonNullableType inp.Type |])
        |> asExpr
    and transConv (inp: Expr) env isChecked x =
        let e = ConvExprToLinqInContext env x
        let exprErasedConstructor: _ * _ * _ -> _ = if isChecked then Expression.ConvertChecked else Expression.Convert
        if isLinqExpressionsConvertible e.Type inp.Type then
            exprErasedConstructor(e, inp.Type, null)
        else
            // The dynamic implementations of checked conversion operators refer to LanguagePrimitives.ExplicitDynamic which is unchecked! This is a bug and should definitely be fixed.
            let method = Reflection.MethodInfo.GetMethodFromHandle (methodhandleof (fun x -> LanguagePrimitives.ExplicitDynamic x)) :?> Reflection.MethodInfo
            exprErasedConstructor(e, inp.Type, method.MakeGenericMethod [| getNonNullableType x.Type; getNonNullableType inp.Type |])
        |> asExpr

    and ConvObjArg env objOpt coerceTo : Expression =
        match objOpt with
        | Some obj ->
            let expr = ConvExprToLinqInContext env obj
            match coerceTo with
            | None -> expr
            | Some ty -> Expression.TypeAs(expr, ty) :> Expression
        | None ->
            null

    and ConvExprsToLinq env es : Expression[] =
        es |> List.map (ConvExprToLinqInContext env) |> Array.ofList

    and ConvVarToLinq (v: Var) =
        //printf "** Expression .Parameter(%a, %a)\n" output_any ty output_any nm
        Expression.Parameter(v.Type, v.Name)

    let ConvExprToLinq (e: Expr) = ConvExprToLinqInContext { varEnv = Map.empty } e

    let QuotationToExpression (e: Microsoft.FSharp.Quotations.Expr) = ConvExprToLinq e
    let QuotationToLambdaExpression (e: Microsoft.FSharp.Quotations.Expr<'T>) =  (ConvExprToLinq e) :?> Expression<'T>

    // This contorted compilation is used because LINQ's "Compile" is only allowed on lambda expressions, and LINQ
    // provides no other way to evaluate the expression.
    //
    // REVIEW: It is possible it is just better to interpret the expression in many common cases, e.g. property-gets, values etc.
    let EvaluateQuotation (e: Microsoft.FSharp.Quotations.Expr) : obj =
#if FX_NO_QUOTATIONS_COMPILE
       raise (new NotSupportedException())
#else
       match e with
       | Value (obj, _) -> obj
       | _ ->
       let ty = e.Type
       let e = Expr.NewDelegate (Expression.GetFuncType([|typeof<unit>; ty |]), [new Var("unit", typeof<unit>)], e)
       let linqExpr = (ConvExprToLinq e:?> LambdaExpression)
       let d = linqExpr.Compile ()
       try
           d.DynamicInvoke [| box () |]
       with :? System.Reflection.TargetInvocationException as exn ->
           raise exn.InnerException
#endif


