// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.ErrorLogger

open System
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.Features
open FSharp.Compiler.Text

/// Represents the style being used to format errors
[<RequireQualifiedAccessAttribute>]
type ErrorStyle =
    | DefaultErrors
    | EmacsErrors
    | TestErrors
    | VSErrors
    | GccErrors

/// Thrown when we want to add some range information to a .NET exception
exception WrappedError of exn * range

/// Thrown when immediate, local error recovery is not possible. This indicates
/// we've reported an error but need to make a non-local transfer of control.
/// Error recovery may catch this and continue (see 'errorRecovery')
///
/// The exception that caused the report is carried as data because in some
/// situations (LazyWithContext) we may need to re-report the original error
/// when a lazy thunk is re-evaluated.
exception ReportedError of exn option

val findOriginalException: err:exn -> exn

type Suggestions = (string -> unit) -> unit

val NoSuggestions: Suggestions

/// Thrown when we stop processing the F# Interactive entry or #load.
exception StopProcessingExn of exn option

val ( |StopProcessing|_| ): exn:exn -> unit option

val StopProcessing<'T> : exn

exception Error of (int * string) * range

exception InternalError of msg: string * range

exception UserCompilerMessage of string * int * range

exception LibraryUseOnly of range

exception Deprecated of string * range

exception Experimental of string * range

exception PossibleUnverifiableCode of range

exception UnresolvedReferenceNoRange of string

exception UnresolvedReferenceError of string * range

exception UnresolvedPathReferenceNoRange of string * string

exception UnresolvedPathReference of string * string * range

exception ErrorWithSuggestions of (int * string) * range * string * Suggestions

val inline protectAssemblyExploration: dflt:'a -> f:(unit -> 'a) -> 'a

val inline protectAssemblyExplorationF: dflt:(string * string -> 'a) -> f:(unit -> 'a) -> 'a

val inline protectAssemblyExplorationNoReraise: dflt1:'a -> dflt2:'a -> f:(unit -> 'a) -> 'a

val AttachRange: m:range -> exn:exn -> exn

type Exiter =
      abstract member Exit: int -> 'T
  
val QuitProcessExiter: Exiter

/// Closed enumeration of build phases.
[<RequireQualifiedAccessAttribute>]
type BuildPhase =
    | DefaultPhase
    | Compile
    | Parameter
    | Parse
    | TypeCheck
    | CodeGen
    | Optimize
    | IlxGen
    | IlGen
    | Output
    | Interactive

/// Literal build phase subcategory strings.
module BuildPhaseSubcategory =
    [<LiteralAttribute>] val DefaultPhase: string = ""
    [<LiteralAttribute>] val Compile: string = "compile"
    [<LiteralAttribute>] val Parameter: string = "parameter"
    [<LiteralAttribute>] val Parse: string = "parse"
    [<LiteralAttribute>] val TypeCheck: string = "typecheck"
    [<LiteralAttribute>] val CodeGen: string = "codegen"
    [<LiteralAttribute>] val Optimize: string = "optimize"
    [<LiteralAttribute>] val IlxGen: string = "ilxgen"
    [<LiteralAttribute>] val IlGen: string = "ilgen"
    [<LiteralAttribute>] val Output: string = "output"
    [<LiteralAttribute>] val Interactive: string = "interactive"
    [<LiteralAttribute>] val Internal: string = "internal"

type PhasedDiagnostic =
    { Exception: exn
      Phase: BuildPhase }

    /// Construct a phased error
    static member Create: exn:exn * phase:BuildPhase -> PhasedDiagnostic

    /// Return true if the textual phase given is from the compile part of the build process.
    /// This set needs to be equal to the set of subcategories that the language service can produce. 
    static member IsSubcategoryOfCompile: subcategory:string -> bool

    member DebugDisplay: unit -> string

    /// Return true if this phase is one that's known to be part of the 'compile'. This is the initial phase of the entire compilation that
    /// the language service knows about.                
    member IsPhaseInCompile: unit -> bool

    /// This is the textual subcategory to display in error and warning messages (shows only under --vserrors):
    ///
    ///     file1.fs(72): subcategory warning FS0072: This is a warning message
    ///
    member Subcategory: unit -> string
  
[<AbstractClass()>]
type ErrorLogger =

    new: nameForDebugging:string -> ErrorLogger

    member DebugDisplay: unit -> string

    abstract member DiagnosticSink: phasedError:PhasedDiagnostic * severity:FSharpDiagnosticSeverity -> unit

    abstract member ErrorCount: int
  
val DiscardErrorsLogger: ErrorLogger

val AssertFalseErrorLogger: ErrorLogger

type CapturingErrorLogger =
    inherit ErrorLogger

    new: nm:string -> CapturingErrorLogger

    member CommitDelayedDiagnostics: errorLogger:ErrorLogger -> unit

    override DiagnosticSink: phasedError:PhasedDiagnostic * severity:FSharpDiagnosticSeverity -> unit

    member Diagnostics: (PhasedDiagnostic * FSharpDiagnosticSeverity) list

    override ErrorCount: int
  
[<Class>]
type CompileThreadStatic =

    static member BuildPhase: BuildPhase with get, set

    static member BuildPhaseUnchecked: BuildPhase with get

    static member ErrorLogger: ErrorLogger with get, set
  
[<AutoOpen>]
module ErrorLoggerExtensions =

    val tryAndDetectDev15: bool

    /// Instruct the exception not to reset itself when thrown again.
    val PreserveStackTrace: exn:'a -> unit

    /// Reraise an exception if it is one we want to report to Watson.
    val ReraiseIfWatsonable: exn:exn -> unit

    type ErrorLogger with
        member ErrorR: exn:exn -> unit
        member Warning: exn:exn -> unit
        member Error: exn:exn -> 'b
        member SimulateError: ph:PhasedDiagnostic -> 'a
        member ErrorRecovery: exn:exn -> m:range -> unit
        member StopProcessingRecovery: exn:exn -> m:range -> unit
        member ErrorRecoveryNoRange: exn:exn -> unit

/// NOTE: The change will be undone when the returned "unwind" object disposes
val PushThreadBuildPhaseUntilUnwind: phase:BuildPhase -> System.IDisposable

/// NOTE: The change will be undone when the returned "unwind" object disposes
val PushErrorLoggerPhaseUntilUnwind: errorLoggerTransformer:(ErrorLogger -> #ErrorLogger) -> System.IDisposable

val SetThreadBuildPhaseNoUnwind: phase:BuildPhase -> unit

val SetThreadErrorLoggerNoUnwind: errorLogger:ErrorLogger -> unit

val errorR: exn:exn -> unit

val warning: exn:exn -> unit

val error: exn:exn -> 'a

val simulateError: p:PhasedDiagnostic -> 'a

val diagnosticSink: phasedError:PhasedDiagnostic * severity: FSharpDiagnosticSeverity -> unit

val errorSink: pe:PhasedDiagnostic -> unit

val warnSink: pe:PhasedDiagnostic -> unit

val errorRecovery: exn:exn -> m:range -> unit

val stopProcessingRecovery: exn:exn -> m:range -> unit

val errorRecoveryNoRange: exn:exn -> unit

val report: f:(unit -> 'a) -> 'a

val deprecatedWithError: s:string -> m:range -> unit

val mutable reportLibraryOnlyFeatures: bool

val libraryOnlyError: m:range -> unit

val libraryOnlyWarning: m:range -> unit

val deprecatedOperator: m:range -> unit

val mlCompatWarning: s:System.String -> m:range -> unit

val suppressErrorReporting: f:(unit -> 'a) -> 'a

val conditionallySuppressErrorReporting: cond:bool -> f:(unit -> 'a) -> 'a

/// The result type of a computational modality to colelct warnings and possibly fail
[<NoEqualityAttribute (); NoComparisonAttribute>]
type OperationResult<'T> =
    | OkResult of warnings: exn list * 'T
    | ErrorResult of warnings: exn list * exn

type ImperativeOperationResult = OperationResult<unit>

val ReportWarnings: warns:#exn list -> unit

val CommitOperationResult: res:OperationResult<'a> -> 'a

val RaiseOperationResult: res:OperationResult<unit> -> unit

val ErrorD: err:exn -> OperationResult<'a>

val WarnD: err:exn -> OperationResult<unit>

val CompleteD: OperationResult<unit>

val ResultD: x:'a -> OperationResult<'a>

val CheckNoErrorsAndGetWarnings: res:OperationResult<'a> -> exn list option

val ( ++ ): res:OperationResult<'a> -> f:('a -> OperationResult<'b>) -> OperationResult<'b>

/// Stop on first error. Accumulate warnings and continue. 
val IterateD: f:('a -> OperationResult<unit>) -> xs:'a list -> OperationResult<unit> 

val WhileD: gd:(unit -> bool) -> body:(unit -> OperationResult<unit>) -> OperationResult<unit> 

val MapD: f:('a -> OperationResult<'b>) -> xs:'a list -> OperationResult<'b list>

type TrackErrorsBuilder =

    new: unit -> TrackErrorsBuilder

    member Bind: res:OperationResult<'h> * k:('h -> OperationResult<'i>) -> OperationResult<'i>

    member Combine: expr1:OperationResult<'c> * expr2:('c -> OperationResult<'d>) -> OperationResult<'d>

    member Delay: fn:(unit -> 'b) -> (unit -> 'b)

    member For: seq:'e list * k:('e -> OperationResult<unit>) -> OperationResult<unit>

    member Return: res:'g -> OperationResult<'g>

    member ReturnFrom: res:'f -> 'f

    member Run: fn:(unit -> 'a) -> 'a

    member While: gd:(unit -> bool) * k:(unit -> OperationResult<unit>) -> OperationResult<unit>

    member Zero: unit -> OperationResult<unit>
  
val trackErrors: TrackErrorsBuilder

val OptionD: f:('a -> OperationResult<unit>) -> xs:'a option -> OperationResult<unit>

val IterateIdxD: f:(int -> 'a -> OperationResult<unit>) -> xs:'a list -> OperationResult<unit>

/// Stop on first error. Accumulate warnings and continue. 
val Iterate2D: f:('a -> 'b -> OperationResult<unit>) -> xs:'a list -> ys:'b list -> OperationResult<unit>

val TryD: f:(unit -> OperationResult<'a>) -> g:(exn -> OperationResult<'a>) -> OperationResult<'a>

val RepeatWhileD: nDeep:int -> body:(int -> OperationResult<bool>) -> OperationResult<unit>

val AtLeastOneD: f:('a -> OperationResult<bool>) -> l:'a list -> OperationResult<bool>

module OperationResult =
    val inline ignore: res:OperationResult<'a> -> OperationResult<unit>

// For --flaterrors flag that is only used by the IDE
val stringThatIsAProxyForANewlineInFlatErrors: String

val NewlineifyErrorString: message:string -> string

/// fixes given string by replacing all control chars with spaces.
/// NOTE: newlines are recognized and replaced with stringThatIsAProxyForANewlineInFlatErrors (ASCII 29, the 'group separator'), 
/// which is decoded by the IDE with 'NewlineifyErrorString' back into newlines, so that multi-line errors can be displayed in QuickInfo
val NormalizeErrorString: text:string -> string
  
val checkLanguageFeatureError: langVersion:LanguageVersion -> langFeature:LanguageFeature -> m:range -> unit

val checkLanguageFeatureErrorRecover: langVersion:LanguageVersion -> langFeature:LanguageFeature -> m:range -> unit

val tryLanguageFeatureErrorOption: langVersion:LanguageVersion -> langFeature:LanguageFeature -> m:range -> exn option

val languageFeatureNotSupportedInLibraryError: langVersion:LanguageVersion -> langFeature:LanguageFeature -> m:range -> 'a
