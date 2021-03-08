// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Contains logic to prepare, post-process, filter and emit compiler diagnsotics
module internal FSharp.Compiler.CompilerDiagnostics

open System.Text
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text

#if DEBUG
module internal CompilerService =
    val showAssertForUnexpectedException: bool ref

/// For extra diagnostics 
val mutable showParserStackOnParseError: bool
#endif // DEBUG

/// This exception is an old-style way of reporting a diagnostic
exception HashIncludeNotAllowedInNonScript of range

/// This exception is an old-style way of reporting a diagnostic
exception HashReferenceNotAllowedInNonScript of range

/// This exception is an old-style way of reporting a diagnostic
exception HashLoadedSourceHasIssues of (*warnings*) exn list * (*errors*) exn list * range

/// This exception is an old-style way of reporting a diagnostic
exception HashLoadedScriptConsideredSource of range

/// This exception is an old-style way of reporting a diagnostic
exception HashDirectiveNotAllowedInNonScript of range

/// This exception is an old-style way of reporting a diagnostic
exception DeprecatedCommandLineOptionFull of string * range

/// This exception is an old-style way of reporting a diagnostic
exception DeprecatedCommandLineOptionForHtmlDoc of string * range

/// This exception is an old-style way of reporting a diagnostic
exception DeprecatedCommandLineOptionSuggestAlternative of string * string * range

/// This exception is an old-style way of reporting a diagnostic
exception DeprecatedCommandLineOptionNoDescription of string * range

/// This exception is an old-style way of reporting a diagnostic
exception InternalCommandLineOption of string * range

/// Get the location associated with an error
val GetRangeOfDiagnostic: PhasedDiagnostic -> range option

/// Get the number associated with an error
val GetDiagnosticNumber: PhasedDiagnostic -> int

/// Split errors into a "main" error and a set of associated errors
val SplitRelatedDiagnostics: PhasedDiagnostic -> PhasedDiagnostic * PhasedDiagnostic list

/// Output an error to a buffer
val OutputPhasedDiagnostic: StringBuilder -> PhasedDiagnostic -> flattenErrors: bool -> suggestNames: bool -> unit

/// Output an error or warning to a buffer
val OutputDiagnostic: implicitIncludeDir:string * showFullPaths: bool * flattenErrors: bool * errorStyle: ErrorStyle * severity: FSharpDiagnosticSeverity -> StringBuilder -> PhasedDiagnostic -> unit

/// Output extra context information for an error or warning to a buffer
val OutputDiagnosticContext: prefix:string -> fileLineFunction:(string -> int -> string) -> StringBuilder -> PhasedDiagnostic -> unit

/// Part of LegacyHostedCompilerForTesting
[<RequireQualifiedAccess>]
type DiagnosticLocation =
    { Range: range
      File: string
      TextRepresentation: string
      IsEmpty: bool }

/// Part of LegacyHostedCompilerForTesting
[<RequireQualifiedAccess>]
type DiagnosticCanonicalInformation = 
    { ErrorNumber: int
      Subcategory: string
      TextRepresentation: string }

/// Part of LegacyHostedCompilerForTesting
[<RequireQualifiedAccess>]
type DiagnosticDetailedInfo = 
    { Location: DiagnosticLocation option
      Canonical: DiagnosticCanonicalInformation
      Message: string }

/// Part of LegacyHostedCompilerForTesting
[<RequireQualifiedAccess>]
type Diagnostic = 
    | Short of FSharpDiagnosticSeverity * string
    | Long of FSharpDiagnosticSeverity * DiagnosticDetailedInfo

/// Part of LegacyHostedCompilerForTesting
val CollectDiagnostic: implicitIncludeDir:string * showFullPaths: bool * flattenErrors: bool * errorStyle: ErrorStyle * severity: FSharpDiagnosticSeverity * PhasedDiagnostic * suggestNames: bool -> seq<Diagnostic>

/// Get an error logger that filters the reporting of warnings based on scoped pragma information
val GetErrorLoggerFilteringByScopedPragmas: checkFile:bool * ScopedPragma list * ErrorLogger  -> ErrorLogger

val SanitizeFileName: fileName: string -> implicitIncludeDir: string -> string

/// Indicates if we should report a warning
val ReportWarning: FSharpDiagnosticOptions -> PhasedDiagnostic -> bool

/// Indicates if we should report a warning as an error
val ReportWarningAsError: FSharpDiagnosticOptions -> PhasedDiagnostic -> bool


