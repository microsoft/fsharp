// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.FSharp.Linq.RuntimeHelpers

open System
open System.Linq.Expressions
open Microsoft.FSharp.Core
open Microsoft.FSharp.Collections
open Microsoft.FSharp.Quotations

/// <summary>
/// Contains functionality to convert F# quotations to LINQ expression trees.
/// </summary>
///
/// <namespacedoc><summary>
///   Library functionality associated with converting F# quotations to .NET LINQ expression trees.
/// </summary></namespacedoc>
module LeafExpressionConverter =
    /// When used in a quotation, this function indicates a specific conversion
    /// should be performed when converting the quotation to a LINQ expression. 
    ///
    /// This function should not be called directly. 
    //
    // NOTE: when an F# expression tree is converted to a Linq expression tree using ToLinqExpression 
    // the transformation of <c>LinqExpressionHelper(e)</c> is the same as the transformation of
    // 'e'. This allows ImplicitExpressionConversionHelper to be used as a marker to satisfy the C# design where 
    // certain expression trees are constructed using methods with a signature that expects an
    // expression tree of type <c>Expression<T></c> but are passed an expression tree of type T.
    //[<System.Obsolete("This type is for use by the quotation to LINQ expression tree converter and is not for direct use from user code")>]
    val ImplicitExpressionConversionHelper : 'T -> Expression<'T>

    /// When used in a quotation, this function indicates a specific conversion
    /// should be performed when converting the quotation to a LINQ expression. 
    ///
    /// This function should not be called directly. 
    //[<System.Obsolete("This type is for use by the quotation to LINQ expression tree converter and is not for direct use from user code")>]
    val MemberInitializationHelper : 'T ->  'T

    /// When used in a quotation, this function indicates a specific conversion
    /// should be performed when converting the quotation to a LINQ expression. 
    ///
    /// This function should not be called directly. 
    //[<System.Obsolete("This type is for use by the quotation to LINQ expression tree converter and is not for direct use from user code")>]
    val NewAnonymousObjectHelper : 'T ->  'T

    /// Converts a subset of F# quotations to a LINQ expression, for the subset of LINQ expressions represented by the
    /// expression syntax in the C# language.
    val QuotationToExpression : Expr -> Expression

    /// Converts a subset of F# quotations to a LINQ expression, for the subset of LINQ expressions represented by the
    /// expression syntax in the C# language. 
    val QuotationToLambdaExpression : Expr<'T> -> Expression<'T>

    /// Evaluates a subset of F# quotations by first converting to a LINQ expression, for the subset of LINQ expressions represented by the
    /// expression syntax in the C# language.
    val EvaluateQuotation : Expr -> obj

    /// A runtime helper used to evaluate nested quotation literals.
    val SubstHelper : Expr * Var[] * obj[] -> Expr<'T>

    /// A runtime helper used to evaluate nested quotation literals.
    val SubstHelperRaw : Expr * Var[] * obj[] -> Expr

    val internal (|SpecificCallToMethod|_|) : System.RuntimeMethodHandle -> (Expr -> (Expr option * Reflection.MethodInfo * Expr list) option)
