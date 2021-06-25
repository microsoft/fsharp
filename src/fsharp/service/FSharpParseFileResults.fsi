// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.CodeAnalysis

open FSharp.Compiler.Diagnostics
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text

[<Sealed>]
/// Represents the results of parsing an F# file and a set of analysis operations based on the parse tree alone.
type public FSharpParseFileResults = 

    /// The syntax tree resulting from the parse
    member ParseTree: ParsedInput

    /// The source text from which the parse originated, if it wasn't read from disk
    member SourceText: ISourceText option

    /// Attempts to find the range of the name of the nearest outer binding that contains a given position.
    member TryRangeOfNameOfNearestOuterBindingContainingPos: pos: pos -> range option

    /// Attempts to find the range of an attempted lambda expression or pattern, the argument range, and the expr range when writing a C#-style "lambda" (which is actually an operator application)
    member TryRangeOfParenEnclosingOpEqualsGreaterUsage: opGreaterEqualPos: pos -> (range * range * range) option

    /// Attempts to find the range of an expression `expr` contained in a `yield expr`  or `return expr` expression (and bang-variants).
    member TryRangeOfExprInYieldOrReturn: pos: pos -> range option

    /// Attempts to find the range of a record expression containing the given position.
    member TryRangeOfRecordExpressionContainingPos: pos: pos -> range option

    /// Attempts to find an Ident of a pipeline containing the given position, and the number of args already applied in that pipeline.
    /// For example, '[1..10] |> List.map ' would give back the ident of '|>' and 1, because it applied 1 arg (the list) to 'List.map'.
    member TryIdentOfPipelineContainingPosAndNumArgsApplied: pos: pos -> (Ident * int) option

    /// Determines if the given position is inside a function or method application.
    member IsPosContainedInApplication: pos: pos -> bool

    /// Attempts to find the range of a function or method that is being applied. Also accounts for functions in pipelines.
    member TryRangeOfFunctionOrMethodBeingApplied: pos: pos -> range option

    /// Gets the ranges of all arguments, if they can be found, for a function application at the given position.
    member GetAllArgumentsForFunctionApplicationAtPostion: pos: pos -> range list option

    /// <summary>
    /// Given the position of an expression, attempts to find the range of the
    /// '!' in a derefence operation of that expression, like:
    /// '!expr', '!(expr)', etc.
    /// </summary>
    member TryRangeOfRefCellDereferenceContainingPos: expressionPos: pos -> range option

    /// Gets the range of an expression being dereferenced. For `!expr`, gives the range of `expr`
    member TryRangeOfExpressionBeingDereferencedContainingPos: expressionPos: pos -> range option

    /// Notable parse info for ParameterInfo at a given location
    member FindParameterLocations: pos:pos -> ParameterLocations option

    /// Determines if the given position is contained within a curried parameter in a binding.
    member IsPositionContainedInACurriedParameter: pos: pos -> bool

    /// Determines if the expression or pattern at the given position has a type annotation
    member IsTypeAnnotationGivenAtPosition: pos -> bool

    /// Determines if the binding at the given position is bound to a lambda expression
    member IsBindingALambdaAtPosition: pos -> bool

    /// Name of the file for which this information were created
    member FileName: string

    /// Get declared items and the selected item at the specified location
    member GetNavigationItems: unit -> NavigationItems

    /// Return the inner-most range associated with a possible breakpoint location
    member ValidateBreakpointLocation: pos:pos -> range option

    /// When these files change then the build is invalid
    member DependencyFiles: string[]

    /// Get the errors and warnings for the parse
    member Diagnostics: FSharpDiagnostic[]

    /// Indicates if any errors occurred during the parse
    member ParseHadErrors: bool

    internal new: diagnostics: FSharpDiagnostic[] * input: ParsedInput * sourceText: ISourceText option * parseHadErrors: bool * dependencyFiles: string[] -> FSharpParseFileResults

