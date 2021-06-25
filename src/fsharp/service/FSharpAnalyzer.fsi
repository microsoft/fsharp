// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.CodeAnalysis

open System
open System.Threading
open FSharp.Compiler.CheckExpressions
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.CompilerConfig
open FSharp.Compiler.CompilerImports
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.Syntax
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.TypedTree
open FSharp.Compiler.Text

type FSharpAnalyzerTextChange = Range * string

/// The context for an analyzer when one or more files are checked
[<Sealed>]
type public FSharpAnalyzerCheckFilesContext = 

    internal new: checkResults: FSharpCheckFileResults[] * cancellationToken: CancellationToken -> FSharpAnalyzerCheckFilesContext

    member CancellationToken: CancellationToken 

    //member GetFileSource: fileName: string -> ISourceText
    // Currently this is the file source of the actual last file checked, which is the last in
    // the PartialAssemblyContents
    // member FileSource: ISourceText

    /// This contains handles to models resulting from checking each of the files.
    /// There is always at least one file, and in an incremental editing environment
    /// there will only be one file.  
    member CheckerModel: FSharpCheckFileResults[]

/// The context for an analyzer when a project is checked
[<Sealed>]
type public FSharpAnalyzerCheckProjectContext = 

    internal new: checkResults: FSharpCheckProjectResults * cancellationToken: CancellationToken -> FSharpAnalyzerCheckProjectContext

    member CancellationToken: CancellationToken 

    //member FileSource: ISourceText
    //member GetFileSource: fileName: string -> ISourceText

    member CheckerModel: FSharpCheckProjectResults

/// The context for an analyzer. Currently empty.
[<Sealed>]
type public FSharpAnalysisContext =
    
    /// Indicates the analyzer is running in a context where tooltips and other editor services may be requested
    member EditorServicesRequested: bool


/// Represents an analyzer. Inherit from this class to create an analyzer.
[<AbstractClass>]
type public FSharpAnalyzer = 

    new: context: FSharpAnalysisContext -> FSharpAnalyzer
    
    member Context: FSharpAnalysisContext
    
    abstract RequiresAssemblyContents: bool

    /// Called when one or more files are checked incrementally
    abstract OnCheckFiles: FSharpAnalyzerCheckFilesContext -> FSharpDiagnostic[]

    abstract OnCheckProject: FSharpAnalyzerCheckProjectContext -> FSharpDiagnostic[]

    abstract TryAdditionalToolTip: FSharpAnalyzerCheckFilesContext * position: Position -> TaggedText[] option

    abstract TryCodeFix: FSharpAnalyzerCheckFilesContext * diagnostics: FSharpDiagnostic[] -> FSharpAnalyzerTextChange[] option

    abstract FixableDiagnosticIds: string[]

    default RequiresAssemblyContents: bool
    default OnCheckFiles: FSharpAnalyzerCheckFilesContext -> FSharpDiagnostic[]
    default OnCheckProject: FSharpAnalyzerCheckProjectContext -> FSharpDiagnostic[]
    default TryAdditionalToolTip: FSharpAnalyzerCheckFilesContext * position: Position -> TaggedText[] option
    default TryCodeFix: FSharpAnalyzerCheckFilesContext * diagnostics: FSharpDiagnostic[] -> FSharpAnalyzerTextChange[] option
    default FixableDiagnosticIds: string[]

module internal FSharpAnalyzers =
    val ImportAnalyzers: tcConfig: TcConfig * compilerToolPaths: (range * string) list -> FSharpAnalyzer list
    val RunAnalyzers: analyzers: FSharpAnalyzer list * tcConfig: TcConfig * tcImports: TcImports * tcGlobals: TcGlobals * tcCcu: CcuThunk * sourceFiles: string list * tcFileResults: (ParsedInput * TypedImplFile option * ModuleOrNamespaceType) list * tcEnvAtEnd: TcEnv -> unit
