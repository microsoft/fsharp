// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

//----------------------------------------------------------------------------
// API to the compiler as an incremental service for parsing,
// type checking and intellisense-like environment-reporting.
//----------------------------------------------------------------------------

namespace FSharp.Compiler.SourceCodeServices

open System.Collections.Generic

open FSharp.Compiler.Range
open FSharp.Compiler.SyntaxTree

[<Sealed>]
/// Represents the results of parsing an F# file
type public FSharpParseFileResults = 

    /// The syntax tree resulting from the parse
    member ParseTree : ParsedInput option

    /// Notable parse info for ParameterInfo at a given location
    member FindNoteworthyParamInfoLocations : pos:pos -> FSharpNoteworthyParamInfoLocations option

    /// Gets the ranges of all arguments, if they can be found, for a function application at the given position.
    member GetAllArgumentsForFunctionApplicationAtPostion: pos: pos -> range list option

    /// Determines if the expression or pattern at the given position has a type annotation
    member IsTypeAnnotationGivenAtPosition: pos -> bool

    /// Determines if the binding at the given position is bound to a lambda expression
    member IsBindingALambdaAtPosition: pos -> bool

    /// Name of the file for which this information were created
    member FileName: string

    /// Get declared items and the selected item at the specified location
    member GetNavigationItems: unit -> FSharpNavigationItems

    /// Return the inner-most range associated with a possible breakpoint location
    member ValidateBreakpointLocation : pos:pos -> range option

    /// When these files change then the build is invalid
    member DependencyFiles : string[]

    /// Get the errors and warnings for the parse
    member Errors : FSharpErrorInfo[]

    /// Indicates if any errors occurred during the parse
    member ParseHadErrors : bool

    internal new: errors: FSharpErrorInfo[] * input: ParsedInput option * parseHadErrors: bool * dependencyFiles: string[] -> FSharpParseFileResults

/// Information about F# source file names
module public SourceFile =

   /// Whether or not this file is compilable
   val IsCompilable : string -> bool

   /// Whether or not this file should be a single-file project
   val MustBeSingleFileProject : string -> bool

type public CompletionPath = string list * string option // plid * residue

[<RequireQualifiedAccess>]
type public InheritanceContext = 
    | Class
    | Interface
    | Unknown

[<RequireQualifiedAccess>]
type public RecordContext =
    | CopyOnUpdate of range * CompletionPath // range
    | Constructor of string // typename
    | New of CompletionPath

[<RequireQualifiedAccess>]
type public CompletionContext = 

    /// completion context cannot be determined due to errors
    | Invalid

    /// completing something after the inherit keyword
    | Inherit of InheritanceContext * CompletionPath

    /// completing records field
    | RecordField of RecordContext

    | RangeOperator

    /// completing named parameters\setters in parameter list of constructor\method calls
    /// end of name ast node * list of properties\parameters that were already set
    | ParameterList of pos * HashSet<string>

    | AttributeApplication

    | OpenDeclaration of isOpenType: bool

    /// completing pattern type (e.g. foo (x: |))
    | PatternType

type public ModuleKind = { IsAutoOpen: bool; HasModuleSuffix: bool }

[<RequireQualifiedAccess>]
type public EntityKind =
    | Attribute
    | Type
    | FunctionOrValue of isActivePattern:bool
    | Module of ModuleKind

// implementation details used by other code in the compiler    
module public UntypedParseImpl =
    val TryFindExpressionASTLeftOfDotLeftOfCursor : pos * ParsedInput option -> (pos * bool) option

    val GetRangeOfExprLeftOfDot : pos  * ParsedInput option -> range option

    val TryFindExpressionIslandInPosition : pos * ParsedInput option -> string option

    val TryGetCompletionContext : pos * ParsedInput * lineStr: string -> CompletionContext option

    val GetEntityKind: pos * ParsedInput -> EntityKind option

    val GetFullNameOfSmallestModuleOrNamespaceAtPoint : ParsedInput * pos -> string[]

// implementation details used by other code in the compiler    
module internal SourceFileImpl =

    val IsInterfaceFile : string -> bool 

    val AdditionalDefinesForUseInEditor: isInteractive: bool -> string list

