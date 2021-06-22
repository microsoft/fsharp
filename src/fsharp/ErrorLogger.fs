// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module FSharp.Compiler.ErrorLogger

open FSharp.Compiler.Diagnostics
open FSharp.Compiler.Features
open FSharp.Compiler.Text.Range
open FSharp.Compiler.Text
open System
open System.Diagnostics

/// Represents the style being used to format errors
[<RequireQualifiedAccess>]
type ErrorStyle = 
    | DefaultErrors 
    | EmacsErrors 
    | TestErrors 
    | VSErrors
    | GccErrors

/// Thrown when we want to add some range information to a .NET exception
exception WrappedError of exn * range with
    override this.Message =
        match this :> exn with
        | WrappedError (exn, _) -> "WrappedError(" + exn.Message + ")"
        | _ -> "WrappedError"

/// Thrown when immediate, local error recovery is not possible. This indicates
/// we've reported an error but need to make a non-local transfer of control.
/// Error recovery may catch this and continue (see 'errorRecovery')
///
/// The exception that caused the report is carried as data because in some
/// situations (LazyWithContext) we may need to re-report the original error
/// when a lazy thunk is re-evaluated.
exception ReportedError of exn option with
    override this.Message = 
        let msg = "The exception has been reported. This internal exception should now be caught at an error recovery point on the stack."
        match this :> exn with
        | ReportedError (Some exn) -> msg + " Original message: " + exn.Message + ")"
        | _ -> msg

let rec findOriginalException err = 
    match err with 
    | ReportedError (Some err) -> err
    | WrappedError(err, _)  -> findOriginalException err
    | _ -> err

type Suggestions = (string -> unit) -> unit

let NoSuggestions : Suggestions = ignore

/// Thrown when we stop processing the F# Interactive entry or #load.
exception StopProcessingExn of exn option with
    override _.Message = "Processing of a script fragment has stopped because an exception has been raised"

    override this.ToString() = 
        match this :> exn with 
        | StopProcessingExn(Some exn) ->  "StopProcessingExn, originally (" + exn.ToString() + ")"
        | _ -> "StopProcessingExn"

        
let (|StopProcessing|_|) exn = match exn with StopProcessingExn _ -> Some () | _ -> None

let StopProcessing<'T> = StopProcessingExn None

exception Error of (int * string) * range with   // int is e.g. 191 in FS0191
    override this.Message =
        match this :> exn with
        | Error((_, msg), _) -> msg
        | _ -> "impossible"

exception InternalError of msg: string * range with 
    override this.Message = 
        match this :> exn with 
        | InternalError(msg, m) -> msg + m.ToString()
        | _ -> "impossible"

exception UserCompilerMessage of string * int * range

exception LibraryUseOnly of range

exception Deprecated of string * range

exception Experimental of string * range

exception PossibleUnverifiableCode of range

exception UnresolvedReferenceNoRange of (*assemblyName*) string 

exception UnresolvedReferenceError of (*assemblyName*) string * range

exception UnresolvedPathReferenceNoRange of (*assemblyName*) string * (*path*) string with
    override this.Message =
        match this :> exn with
        | UnresolvedPathReferenceNoRange(assemblyName, path) -> sprintf "Assembly: %s, full path: %s" assemblyName path
        | _ -> "impossible"

exception UnresolvedPathReference of (*assemblyName*) string * (*path*) string * range

exception ErrorWithSuggestions of (int * string) * range * string * Suggestions with   // int is e.g. 191 in FS0191 
    override this.Message =
        match this :> exn with
        | ErrorWithSuggestions((_, msg), _, _, _) -> msg
        | _ -> "impossible"


let inline protectAssemblyExploration dflt f = 
    try 
       f()
    with
    | UnresolvedPathReferenceNoRange _ -> dflt
    | _ -> reraise()

let inline protectAssemblyExplorationF dflt f = 
    try 
       f()
    with
    | UnresolvedPathReferenceNoRange (asmName, path) -> dflt(asmName, path)
    | _ -> reraise()

let inline protectAssemblyExplorationNoReraise dflt1 dflt2 f  = 
    try 
       f()
    with
    | UnresolvedPathReferenceNoRange _ -> dflt1
    | _ -> dflt2

// Attach a range if this is a range dual exception.
let rec AttachRange m (exn:exn) = 
    if Range.equals m range0 then exn
    else 
        match exn with
        // Strip TargetInvocationException wrappers
        | :? System.Reflection.TargetInvocationException -> AttachRange m exn.InnerException
        | UnresolvedReferenceNoRange a -> UnresolvedReferenceError(a, m)
        | UnresolvedPathReferenceNoRange(a, p) -> UnresolvedPathReference(a, p, m)
        | Failure msg -> InternalError(msg + " (Failure)", m)
        | :? System.ArgumentException as exn -> InternalError(exn.Message + " (ArgumentException)", m)
        | notARangeDual -> notARangeDual

type Exiter = 
    abstract Exit : int -> 'T 

let QuitProcessExiter =  
    { new Exiter with  
        member _.Exit n =                     
            try  
                System.Environment.Exit n 
            with _ ->  
                ()             
            FSComp.SR.elSysEnvExitDidntExit()
            |> failwith } 

/// Closed enumeration of build phases.
[<RequireQualifiedAccess>]
type BuildPhase =
    | DefaultPhase 
    | Compile 
    | Parameter | Parse | TypeCheck 
    | CodeGen 
    | Optimize | IlxGen | IlGen | Output 
    | Interactive // An error seen during interactive execution
    
/// Literal build phase subcategory strings.
module BuildPhaseSubcategory =
    [<Literal>] 
    let DefaultPhase = ""
    [<Literal>] 
    let Compile = "compile"
    [<Literal>] 
    let Parameter = "parameter"
    [<Literal>] 
    let Parse = "parse"
    [<Literal>] 
    let TypeCheck = "typecheck"
    [<Literal>] 
    let CodeGen = "codegen"
    [<Literal>] 
    let Optimize = "optimize"
    [<Literal>] 
    let IlxGen = "ilxgen"
    [<Literal>] 
    let IlGen = "ilgen"        
    [<Literal>] 
    let Output = "output"        
    [<Literal>] 
    let Interactive = "interactive"        
    [<Literal>] 
    let Internal = "internal"          // Compiler ICE

[<DebuggerDisplay("{DebugDisplay()}")>]
type PhasedDiagnostic = 
    { Exception:exn; Phase:BuildPhase }

    /// Construct a phased error
    static member Create(exn:exn, phase:BuildPhase) : PhasedDiagnostic =
        // FUTURE: reenable this assert, which has historically triggered in some compiler service scenarios
        // System.Diagnostics.Debug.Assert(phase<>BuildPhase.DefaultPhase, sprintf "Compile error seen with no phase to attribute it to.%A %s %s" phase exn.Message exn.StackTrace )        
        {Exception = exn; Phase=phase}

    member this.DebugDisplay() =
        sprintf "%s: %s" (this.Subcategory()) this.Exception.Message

    /// This is the textual subcategory to display in error and warning messages (shows only under --vserrors):
    ///
    ///     file1.fs(72): subcategory warning FS0072: This is a warning message
    ///
    member pe.Subcategory() =
        match pe.Phase with
        | BuildPhase.DefaultPhase -> BuildPhaseSubcategory.DefaultPhase
        | BuildPhase.Compile -> BuildPhaseSubcategory.Compile
        | BuildPhase.Parameter -> BuildPhaseSubcategory.Parameter
        | BuildPhase.Parse -> BuildPhaseSubcategory.Parse
        | BuildPhase.TypeCheck -> BuildPhaseSubcategory.TypeCheck
        | BuildPhase.CodeGen -> BuildPhaseSubcategory.CodeGen
        | BuildPhase.Optimize -> BuildPhaseSubcategory.Optimize
        | BuildPhase.IlxGen -> BuildPhaseSubcategory.IlxGen
        | BuildPhase.IlGen -> BuildPhaseSubcategory.IlGen
        | BuildPhase.Output -> BuildPhaseSubcategory.Output
        | BuildPhase.Interactive -> BuildPhaseSubcategory.Interactive

    /// Return true if the textual phase given is from the compile part of the build process.
    /// This set needs to be equal to the set of subcategories that the language service can produce. 
    static member IsSubcategoryOfCompile(subcategory:string) =
        // This code logic is duplicated in DocumentTask.cs in the language service.
        match subcategory with 
        | BuildPhaseSubcategory.Compile 
        | BuildPhaseSubcategory.Parameter 
        | BuildPhaseSubcategory.Parse 
        | BuildPhaseSubcategory.TypeCheck -> true
        | null 
        | BuildPhaseSubcategory.DefaultPhase 
        | BuildPhaseSubcategory.CodeGen 
        | BuildPhaseSubcategory.Optimize 
        | BuildPhaseSubcategory.IlxGen 
        | BuildPhaseSubcategory.IlGen 
        | BuildPhaseSubcategory.Output 
        | BuildPhaseSubcategory.Interactive -> false
        | BuildPhaseSubcategory.Internal 
            // Getting here means the compiler has ICE-d. Let's not pile on by showing the unknownSubcategory assert below.
            // Just treat as an unknown-to-LanguageService error.
            -> false
        | unknownSubcategory -> 
            System.Diagnostics.Debug.Assert(false, sprintf "Subcategory '%s' could not be correlated with a build phase." unknownSubcategory)
            // Recovery is to treat this as a 'build' error. Downstream, the project system and language service will treat this as
            // if it came from the build and not the language service.
            false

    /// Return true if this phase is one that's known to be part of the 'compile'. This is the initial phase of the entire compilation that
    /// the language service knows about.                
    member pe.IsPhaseInCompile() = 
        let isPhaseInCompile = 
            match pe.Phase with
            | BuildPhase.Compile | BuildPhase.Parameter | BuildPhase.Parse | BuildPhase.TypeCheck -> true
            | _ -> false
        // Sanity check ensures that Phase matches Subcategory            
#if DEBUG
        if isPhaseInCompile then 
            System.Diagnostics.Debug.Assert(PhasedDiagnostic.IsSubcategoryOfCompile(pe.Subcategory()), "Subcategory did not match isPhaseInCompile=true")
        else
            System.Diagnostics.Debug.Assert(not(PhasedDiagnostic.IsSubcategoryOfCompile(pe.Subcategory())), "Subcategory did not match isPhaseInCompile=false")
#endif            
        isPhaseInCompile

[<AbstractClass>]
[<DebuggerDisplay("{DebugDisplay()}")>]
type ErrorLogger(nameForDebugging:string) = 
    abstract ErrorCount: int
    // The 'Impl' factoring enables a developer to place a breakpoint at the non-Impl 
    // code just below and get a breakpoint for all error logger implementations.
    abstract DiagnosticSink: phasedError: PhasedDiagnostic * severity: FSharpDiagnosticSeverity -> unit
    member _.DebugDisplay() = sprintf "ErrorLogger(%s)" nameForDebugging

let DiscardErrorsLogger = 
    { new ErrorLogger("DiscardErrorsLogger") with 
            member x.DiagnosticSink(phasedError, severity) = ()
            member x.ErrorCount = 0 }

let AssertFalseErrorLogger =
    { new ErrorLogger("AssertFalseErrorLogger") with 
            // TODO: reenable these asserts in the compiler service
            member x.DiagnosticSink(phasedError, severity) = (* assert false; *) ()
            member x.ErrorCount = (* assert false; *) 0 
    }

type CapturingErrorLogger(nm) = 
    inherit ErrorLogger(nm) 
    let mutable errorCount = 0 
    let diagnostics = ResizeArray()

    override _.DiagnosticSink(phasedError, severity) = 
        if severity = FSharpDiagnosticSeverity.Error then errorCount <- errorCount + 1
        diagnostics.Add (phasedError, severity) 

    override _.ErrorCount = errorCount

    member _.Diagnostics = diagnostics |> Seq.toList

    member _.CommitDelayedDiagnostics(errorLogger:ErrorLogger) = 
        // Eagerly grab all the errors and warnings from the mutable collection
        let errors = diagnostics.ToArray()
        errors |> Array.iter errorLogger.DiagnosticSink

/// Type holds thread-static globals for use by the compile.
type internal CompileThreadStatic =
    [<ThreadStatic;DefaultValue>]
    static val mutable private buildPhase  : BuildPhase
    
    [<ThreadStatic;DefaultValue>]
    static val mutable private errorLogger : ErrorLogger

    static member BuildPhaseUnchecked = CompileThreadStatic.buildPhase (* This can be a null value *)

    static member BuildPhase
        with get() = 
            match box CompileThreadStatic.buildPhase with
            // FUTURE: reenable these asserts, which have historically fired in some compiler service scenarios
            | null -> (* assert false; *) BuildPhase.DefaultPhase
            | _ -> CompileThreadStatic.buildPhase
        and set v = CompileThreadStatic.buildPhase <- v
            
    static member ErrorLogger
        with get() = 
            match box CompileThreadStatic.errorLogger with
            | null -> AssertFalseErrorLogger
            | _ -> CompileThreadStatic.errorLogger
        and set v = CompileThreadStatic.errorLogger <- v


[<AutoOpen>]
module ErrorLoggerExtensions = 
    open System.Reflection

    // Dev15.0 shipped with a bug in diasymreader in the portable pdb symbol reader which causes an AV
    // This uses a simple heuristic to detect it (the vsversion is < 16.0)
    let tryAndDetectDev15 =
        let vsVersion = Environment.GetEnvironmentVariable("VisualStudioVersion")
        match Double.TryParse vsVersion with
        | true, v -> v < 16.0
        | _ -> false

    /// Instruct the exception not to reset itself when thrown again.
    let PreserveStackTrace exn =
        try
            if not tryAndDetectDev15 then
                let preserveStackTrace = typeof<System.Exception>.GetMethod("InternalPreserveStackTrace", BindingFlags.Instance ||| BindingFlags.NonPublic)
                preserveStackTrace.Invoke(exn, null) |> ignore
        with _ ->
           // This is probably only the mono case.
           System.Diagnostics.Debug.Assert(false, "Could not preserve stack trace for watson exception.")
           ()

    /// Reraise an exception if it is one we want to report to Watson.
    let ReraiseIfWatsonable(exn:exn) =
        match  exn with 
        // These few SystemExceptions which we don't report to Watson are because we handle these in some way in Build.fs
        | :? System.Reflection.TargetInvocationException -> ()
        | :? System.NotSupportedException  -> ()
        | :? System.IO.IOException -> () // This covers FileNotFoundException and DirectoryNotFoundException
        | :? System.UnauthorizedAccessException -> ()
        | Failure _ // This gives reports for compiler INTERNAL ERRORs
        | :? System.SystemException ->
            PreserveStackTrace exn
            raise exn
        | _ -> ()

    type ErrorLogger with  

        member x.EmitDiagnostic (exn, severity) = 
            match exn with 
            | InternalError (s, _) 
            | Failure s  as exn -> System.Diagnostics.Debug.Assert(false, sprintf "Unexpected exception raised in compiler: %s\n%s" s (exn.ToString()))
            | _ -> ()

            match exn with 
            | StopProcessing 
            | ReportedError _ -> 
                PreserveStackTrace exn
                raise exn 
            | _ -> x.DiagnosticSink(PhasedDiagnostic.Create(exn, CompileThreadStatic.BuildPhase), severity)

        member x.ErrorR exn = 
            x.EmitDiagnostic (exn, FSharpDiagnosticSeverity.Error)

        member x.Warning exn = 
            x.EmitDiagnostic (exn, FSharpDiagnosticSeverity.Warning)

        member x.Error   exn = 
            x.ErrorR exn
            raise (ReportedError (Some exn))

        member x.SimulateError  (ph: PhasedDiagnostic) =
            x.DiagnosticSink (ph, FSharpDiagnosticSeverity.Error)
            raise (ReportedError (Some ph.Exception))

        member x.ErrorRecovery (exn: exn) (m: range) =
            // Never throws ReportedError.
            // Throws StopProcessing and exceptions raised by the DiagnosticSink(exn) handler.
            match exn with
            (* Don't send ThreadAbortException down the error channel *)
            | :? System.Threading.ThreadAbortException | WrappedError((:? System.Threading.ThreadAbortException), _) ->  ()
            | ReportedError _  | WrappedError(ReportedError _, _)  -> ()
            | StopProcessing | WrappedError(StopProcessing, _) -> 
                PreserveStackTrace exn
                raise exn
            | _ ->
                try  
                    x.ErrorR (AttachRange m exn) // may raise exceptions, e.g. an fsi error sink raises StopProcessing.
                    ReraiseIfWatsonable exn
                with
                | ReportedError _ | WrappedError(ReportedError _, _)  -> ()

        member x.StopProcessingRecovery (exn:exn) (m:range) =
            // Do standard error recovery.
            // Additionally ignore/catch StopProcessing. [This is the only catch handler for StopProcessing].
            // Additionally ignore/catch ReportedError.
            // Can throw other exceptions raised by the DiagnosticSink(exn) handler.         
            match exn with
            | StopProcessing | WrappedError(StopProcessing, _) -> () // suppress, so skip error recovery.
            | _ ->
                try
                    x.ErrorRecovery exn m
                with
                | StopProcessing | WrappedError(StopProcessing, _) -> () // catch, e.g. raised by DiagnosticSink.
                | ReportedError _ | WrappedError(ReportedError _, _)  -> () // catch, but not expected unless ErrorRecovery is changed.

        member x.ErrorRecoveryNoRange (exn:exn) =
            x.ErrorRecovery exn range0

/// NOTE: The change will be undone when the returned "unwind" object disposes
let PushThreadBuildPhaseUntilUnwind (phase:BuildPhase) =
    let oldBuildPhase = CompileThreadStatic.BuildPhaseUnchecked
    
    CompileThreadStatic.BuildPhase <- phase

    { new System.IDisposable with 
         member x.Dispose() = CompileThreadStatic.BuildPhase <- oldBuildPhase (* maybe null *) }

/// NOTE: The change will be undone when the returned "unwind" object disposes
let PushErrorLoggerPhaseUntilUnwind(errorLoggerTransformer : ErrorLogger -> #ErrorLogger) =
    let oldErrorLogger = CompileThreadStatic.ErrorLogger
    let newErrorLogger = errorLoggerTransformer oldErrorLogger
    let mutable newInstalled = true
    let newIsInstalled() = if newInstalled then () else (assert false; (); (*failwith "error logger used after unwind"*)) // REVIEW: ok to throw?
    let chkErrorLogger = { new ErrorLogger("PushErrorLoggerPhaseUntilUnwind") with
                             member _.DiagnosticSink(phasedError, isError) = newIsInstalled(); newErrorLogger.DiagnosticSink(phasedError, isError)
                             member _.ErrorCount = newIsInstalled(); newErrorLogger.ErrorCount }

    CompileThreadStatic.ErrorLogger <- chkErrorLogger

    { new System.IDisposable with 
         member _.Dispose() =
            CompileThreadStatic.ErrorLogger <- oldErrorLogger
            newInstalled <- false }

let SetThreadBuildPhaseNoUnwind(phase:BuildPhase) = CompileThreadStatic.BuildPhase <- phase

let SetThreadErrorLoggerNoUnwind errorLogger     = CompileThreadStatic.ErrorLogger <- errorLogger

// Global functions are still used by parser and TAST ops.

/// Raises an exception with error recovery and returns unit.
let errorR exn = CompileThreadStatic.ErrorLogger.ErrorR exn

/// Raises a warning with error recovery and returns unit.
let warning exn = CompileThreadStatic.ErrorLogger.Warning exn

/// Raises a special exception and returns 'T - can be caught later at an errorRecovery point.
let error exn = CompileThreadStatic.ErrorLogger.Error exn

/// Simulates an error. For test purposes only.
let simulateError (p : PhasedDiagnostic) = CompileThreadStatic.ErrorLogger.SimulateError p

let diagnosticSink (phasedError, severity) = CompileThreadStatic.ErrorLogger.DiagnosticSink (phasedError, severity)

let errorSink pe = diagnosticSink (pe, FSharpDiagnosticSeverity.Error)

let warnSink pe = diagnosticSink (pe, FSharpDiagnosticSeverity.Warning)

let errorRecovery exn m = CompileThreadStatic.ErrorLogger.ErrorRecovery exn m

let stopProcessingRecovery exn m = CompileThreadStatic.ErrorLogger.StopProcessingRecovery exn m

let errorRecoveryNoRange exn = CompileThreadStatic.ErrorLogger.ErrorRecoveryNoRange exn

let report f = 
    f() 

let deprecatedWithError s m = errorR(Deprecated(s, m))

let libraryOnlyError m = errorR(LibraryUseOnly m)

let libraryOnlyWarning m = warning(LibraryUseOnly m)

let deprecatedOperator m = deprecatedWithError (FSComp.SR.elDeprecatedOperator()) m

let mlCompatWarning s m = warning(UserCompilerMessage(FSComp.SR.mlCompatMessage s, 62, m))

let suppressErrorReporting f =
    let errorLogger = CompileThreadStatic.ErrorLogger
    try
        let errorLogger = 
            { new ErrorLogger("suppressErrorReporting") with 
                member _.DiagnosticSink(_phasedError, _isError) = ()
                member _.ErrorCount = 0 }
        SetThreadErrorLoggerNoUnwind errorLogger
        f()
    finally
        SetThreadErrorLoggerNoUnwind errorLogger

let conditionallySuppressErrorReporting cond f = if cond then suppressErrorReporting f else f()

//------------------------------------------------------------------------
// Errors as data: Sometimes we have to reify errors as data, e.g. if backtracking 

/// The result type of a computational modality to colelct warnings and possibly fail
[<NoEquality; NoComparison>]
type OperationResult<'T> = 
    | OkResult of warnings: exn list * 'T
    | ErrorResult of warnings: exn list * exn
    
type ImperativeOperationResult = OperationResult<unit>

let ReportWarnings warns = 
    match warns with 
    | [] -> () // shortcut in common case
    | _ -> List.iter warning warns

let CommitOperationResult res = 
    match res with 
    | OkResult (warns, res) -> ReportWarnings warns; res
    | ErrorResult (warns, err) -> ReportWarnings warns; error err

let RaiseOperationResult res : unit = CommitOperationResult res

let ErrorD err = ErrorResult([], err)

let WarnD err = OkResult([err], ())

let CompleteD = OkResult([], ())

let ResultD x = OkResult([], x)

let CheckNoErrorsAndGetWarnings res = 
    match res with 
    | OkResult (warns, _) -> Some warns
    | ErrorResult _ -> None 

/// The bind in the monad. Stop on first error. Accumulate warnings and continue. 
let (++) res f = 
    match res with 
    | OkResult([], res) -> (* tailcall *) f res 
    | OkResult(warns, res) -> 
        match f res with 
        | OkResult(warns2, res2) -> OkResult(warns@warns2, res2)
        | ErrorResult(warns2, err) -> ErrorResult(warns@warns2, err)
    | ErrorResult(warns, err) -> 
        ErrorResult(warns, err)
        
/// Stop on first error. Accumulate warnings and continue. 
let rec IterateD f xs = 
    match xs with
    | [] -> CompleteD 
    | h :: t -> f h ++ (fun () -> IterateD f t)

let rec WhileD gd body = if gd() then body() ++ (fun () -> WhileD gd body) else CompleteD

let MapD f xs = 
    let rec loop acc xs = 
        match xs with
        | [] -> ResultD (List.rev acc) 
        | h :: t -> f h ++ (fun x -> loop (x :: acc) t)

    loop [] xs

type TrackErrorsBuilder() =
    member x.Bind(res, k) = res ++ k
    member x.Return res = ResultD res
    member x.ReturnFrom res = res
    member x.For(seq, k) = IterateD k seq
    member x.Combine(expr1, expr2) = expr1 ++ expr2
    member x.While(gd, k) = WhileD gd k
    member x.Zero()  = CompleteD
    member x.Delay fn = fun () -> fn ()
    member x.Run fn = fn ()

let trackErrors = TrackErrorsBuilder()
    
/// Stop on first error. Accumulate warnings and continue. 
let OptionD f xs = 
    match xs with 
    | None -> CompleteD 
    | Some h -> f h 

/// Stop on first error. Report index 
let IterateIdxD f xs = 
    let rec loop xs i = match xs with [] -> CompleteD | h :: t -> f i h ++ (fun () -> loop t (i+1))
    loop xs 0

/// Stop on first error. Accumulate warnings and continue. 
let rec Iterate2D f xs ys = 
    match xs, ys with 
    | [], [] -> CompleteD 
    | h1 :: t1, h2 :: t2 -> f h1 h2 ++ (fun () -> Iterate2D f t1 t2) 
    | _ -> failwith "Iterate2D"

/// Keep the warnings, propagate the error to the exception continuation.
let TryD f g = 
    match f() with
    | ErrorResult(warns, err) ->
        trackErrors {
            do! OkResult(warns, ())
            return! g err
        }
    | res -> res

let rec RepeatWhileD nDeep body = body nDeep ++ (fun x -> if x then RepeatWhileD (nDeep+1) body else CompleteD) 

let AtLeastOneD f l = MapD f l ++ (fun res -> ResultD (List.exists id res))

[<RequireQualifiedAccess>]
module OperationResult =
    let inline ignore (res: OperationResult<'a>) =
        match res with
        | OkResult(warnings, _) -> OkResult(warnings, ())
        | ErrorResult(warnings, err) -> ErrorResult(warnings, err)

// Code below is for --flaterrors flag that is only used by the IDE
let stringThatIsAProxyForANewlineInFlatErrors = new System.String [|char 29 |]

let NewlineifyErrorString (message:string) = message.Replace(stringThatIsAProxyForANewlineInFlatErrors, Environment.NewLine)

/// fixes given string by replacing all control chars with spaces.
/// NOTE: newlines are recognized and replaced with stringThatIsAProxyForANewlineInFlatErrors (ASCII 29, the 'group separator'), 
/// which is decoded by the IDE with 'NewlineifyErrorString' back into newlines, so that multi-line errors can be displayed in QuickInfo
let NormalizeErrorString (text : string) =
    if isNull text then nullArg "text"
    let text = text.Trim()

    let buf = System.Text.StringBuilder()
    let mutable i = 0
    while i < text.Length do
        let delta = 
            match text.[i] with
            | '\r' when i + 1 < text.Length && text.[i + 1] = '\n' ->
                // handle \r\n sequence - replace it with one single space
                buf.Append stringThatIsAProxyForANewlineInFlatErrors |> ignore
                2
            | '\n' | '\r' ->
                buf.Append stringThatIsAProxyForANewlineInFlatErrors |> ignore
                1
            | c ->
                // handle remaining chars: control - replace with space, others - keep unchanged
                let c = if Char.IsControl c then ' ' else c
                buf.Append c |> ignore
                1
        i <- i + delta
    buf.ToString()

let private tryLanguageFeatureErrorAux (langVersion: LanguageVersion) (langFeature: LanguageFeature) (m: range) =
    if not (langVersion.SupportsFeature langFeature) then
        let featureStr = langVersion.GetFeatureString langFeature
        let currentVersionStr = langVersion.SpecifiedVersionString
        let suggestedVersionStr = LanguageVersion.GetFeatureVersionString langFeature
        Some (Error(FSComp.SR.chkFeatureNotLanguageSupported(featureStr, currentVersionStr, suggestedVersionStr), m))
    else
        None

let internal checkLanguageFeatureError langVersion langFeature m =
    match tryLanguageFeatureErrorAux langVersion langFeature m with
    | Some e -> error (e)
    | None -> ()

let internal checkLanguageFeatureErrorRecover langVersion langFeature m =
    match tryLanguageFeatureErrorAux langVersion langFeature m with
    | Some e -> errorR e
    | None -> ()

let internal tryLanguageFeatureErrorOption langVersion langFeature m =
    tryLanguageFeatureErrorAux langVersion langFeature m

let internal languageFeatureNotSupportedInLibraryError (langVersion: LanguageVersion) (langFeature: LanguageFeature) (m: range) =
    let featureStr = langVersion.GetFeatureString langFeature
    let suggestedVersionStr = LanguageVersion.GetFeatureVersionString langFeature
    error (Error(FSComp.SR.chkFeatureNotSupportedInLibrary(featureStr, suggestedVersionStr), m))
