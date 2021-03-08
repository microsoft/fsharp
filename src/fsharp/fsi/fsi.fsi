// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module public FSharp.Compiler.Interactive.Shell

open System
open System.IO
open System.Threading
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.Symbols

[<Class>]
/// Represents an evaluated F# value
type FsiValue = 

    /// The value, as an object
    member ReflectionValue: obj

    /// The type of the value, from the point of view of the .NET type system
    member ReflectionType: Type

    /// The type of the value, from the point of view of the F# type system
    member FSharpType: FSharpType

[<Sealed>]
/// Represents an evaluated F# value that is bound to an identifier
type FsiBoundValue =

    /// The identifier of the value
    member Name: string

    /// The evaluated F# value
    member Value: FsiValue

[<Class>]
type EvaluationEventArgs =
    inherit EventArgs

    /// The display name of the symbol defined
    member Name: string

    /// The value of the symbol defined, if any
    member FsiValue: FsiValue option

    /// The FSharpSymbolUse for the symbol defined
    member SymbolUse: FSharpSymbolUse

    /// The symbol defined
    member Symbol: FSharpSymbol

    /// The details of the expression defined
    member ImplementationDeclaration: FSharpImplementationFileDeclaration

[<AbstractClass>]
type public FsiEvaluationSessionHostConfig = 
    new: unit -> FsiEvaluationSessionHostConfig

    /// Called by the evaluation session to ask the host for parameters to format text for output
    abstract FormatProvider: IFormatProvider  

    /// Called by the evaluation session to ask the host for parameters to format text for output
    abstract FloatingPointFormat: string 

    /// Called by the evaluation session to ask the host for parameters to format text for output
    abstract AddedPrinters: Choice<(Type * (obj -> string)), (Type * (obj -> obj))>  list

    /// Called by the evaluation session to ask the host for parameters to format text for output
    abstract ShowDeclarationValues: bool  

    /// Called by the evaluation session to ask the host for parameters to format text for output
    abstract ShowIEnumerable: bool  

    /// Called by the evaluation session to ask the host for parameters to format text for output
    abstract ShowProperties: bool  

    /// Called by the evaluation session to ask the host for parameters to format text for output
    abstract PrintSize: int  

    /// Called by the evaluation session to ask the host for parameters to format text for output
    abstract PrintDepth: int  

    /// Called by the evaluation session to ask the host for parameters to format text for output
    abstract PrintWidth: int

    /// Called by the evaluation session to ask the host for parameters to format text for output
    abstract PrintLength: int

    /// The evaluation session calls this to report the preferred view of the command line arguments after 
    /// stripping things like "/use:file.fsx", "-r:Foo.dll" etc.
    abstract ReportUserCommandLineArgs: string [] -> unit

    /// Hook for listening for evaluation bindings
    member OnEvaluation: IEvent<EvaluationEventArgs>

    ///<summary>
    /// <para>Indicate a special console "readline" reader for the evaluation session, if any.</para><para> </para>
    ///
    /// <para>A "console" gets used if --readline is specified (the default on Windows + .NET); and --fsi-server is  not
    /// given (always combine with --readline-), and OptionalConsoleReadLine is given.
    /// When a console is used, special rules apply to "peekahead", which allows early typing on the console.
    /// Peekahead happens if --peekahead- is not specified (the default).
    /// In this case, a prompt is printed early, a background thread is created and 
    /// the OptionalConsoleReadLine is used to read the first line.
    /// If a console is not used, then inReader.Peek() is called early instead.
    /// </para><para> </para>
    ///
    /// <para>Further lines are read using OptionalConsoleReadLine().
    /// If not provided, lines are read using inReader.ReadLine().</para>
    /// <para> </para>
    ///</summary>

    abstract GetOptionalConsoleReadLine: probeToSeeIfConsoleWorks: bool -> (unit -> string) option 

    /// The evaluation session calls this at an appropriate point in the startup phase if the --fsi-server parameter was given
    abstract StartServer: fsiServerName:string -> unit
    
    /// Called by the evaluation session to ask the host to enter a dispatch loop like Application.Run().
    /// Only called if --gui option is used (which is the default).
    /// Gets called towards the end of startup and every time a ThreadAbort escaped to the backup driver loop.
    /// Return true if a 'restart' is required, which is a bit meaningless.
    abstract EventLoopRun: unit -> bool

    /// Request that the given operation be run synchronously on the event loop.
    abstract EventLoopInvoke: codeToRun: (unit -> 'T) -> 'T

    /// Schedule a restart for the event loop.
    abstract EventLoopScheduleRestart: unit -> unit

    /// Implicitly reference FSharp.Compiler.Interactive.Settings.dll
    abstract UseFsiAuxLib: bool 

/// Thrown when there was an error compiling the given code in FSI.
[<Class>]
type FsiCompilationException =
    inherit Exception
    new: string * FSharpDiagnostic[] option -> FsiCompilationException
    member ErrorInfos: FSharpDiagnostic[] option

/// Represents an F# Interactive evaluation session.
[<Class>]
type FsiEvaluationSession = 

    interface IDisposable

    /// <summary>Create an FsiEvaluationSession, reading from the given text input, writing to the given text output and error writers</summary>
    /// 
    /// <param name="fsiConfig">The dynamic configuration of the evaluation session</param>
    /// <param name="argv">The command line arguments for the evaluation session</param>
    /// <param name="inReader">Read input from the given reader</param>
    /// <param name="errorWriter">Write errors to the given writer</param>
    /// <param name="outWriter">Write output to the given writer</param>
    /// <param name="collectible">Optionally make the dynamic assembly for the session collectible</param>
    /// <param name="legacyReferenceResolver">An optional resolver for legacy MSBuild references</param>
    static member Create: fsiConfig: FsiEvaluationSessionHostConfig * argv:string[] * inReader:TextReader * outWriter:TextWriter * errorWriter: TextWriter * ?collectible: bool * ?legacyReferenceResolver: LegacyReferenceResolver -> FsiEvaluationSession

    /// A host calls this to request an interrupt on the evaluation thread.
    member Interrupt: unit -> unit

    /// A host calls this to get the completions for a long identifier, e.g. in the console
    ///
    /// Due to a current limitation, it is not fully thread-safe to run this operation concurrently with evaluation triggered
    /// by input from 'stdin'.
    member GetCompletions: longIdent: string -> seq<string>

    /// Execute the code as if it had been entered as one or more interactions, with an
    /// implicit termination at the end of the input. Stop on first error, discarding the rest
    /// of the input. Errors are sent to the output writer, a 'true' return value indicates there
    /// were no errors overall. Execution is performed on the 'Run()' thread.
    ///
    /// Due to a current limitation, it is not fully thread-safe to run this operation concurrently with evaluation triggered
    /// by input from 'stdin'.
    member EvalInteraction: code: string * ?cancellationToken: CancellationToken -> unit

    /// Execute the code as if it had been entered as one or more interactions, with an
    /// implicit termination at the end of the input. Stop on first error, discarding the rest
    /// of the input. Errors and warnings are collected apart from any exception arising from execution
    /// which is returned via a Choice. Execution is performed on the 'Run()' thread.
    ///
    /// Due to a current limitation, it is not fully thread-safe to run this operation concurrently with evaluation triggered
    /// by input from 'stdin'.
    member EvalInteractionNonThrowing: code: string * ?cancellationToken: CancellationToken -> Choice<FsiValue option, exn> * FSharpDiagnostic[]

    /// Execute the given script. Stop on first error, discarding the rest
    /// of the script. Errors are sent to the output writer, a 'true' return value indicates there
    /// were no errors overall. Execution is performed on the 'Run()' thread.
    ///
    /// Due to a current limitation, it is not fully thread-safe to run this operation concurrently with evaluation triggered
    /// by input from 'stdin'.
    member EvalScript: filePath: string -> unit

    /// Execute the given script. Stop on first error, discarding the rest
    /// of the script. Errors and warnings are collected apart from any exception arising from execution
    /// which is returned via a Choice. Execution is performed on the 'Run()' thread.
    ///
    /// Due to a current limitation, it is not fully thread-safe to run this operation concurrently with evaluation triggered
    /// by input from 'stdin'.
    member EvalScriptNonThrowing: filePath: string -> Choice<unit, exn> * FSharpDiagnostic[]

    /// Execute the code as if it had been entered as one or more interactions, with an
    /// implicit termination at the end of the input. Stop on first error, discarding the rest
    /// of the input. Errors are sent to the output writer. Parsing is performed on the current thread, and execution is performed 
    /// synchronously on the 'main' thread.
    ///
    /// Due to a current limitation, it is not fully thread-safe to run this operation concurrently with evaluation triggered
    /// by input from 'stdin'.
    member EvalExpression: code: string -> FsiValue option

    /// Execute the code as if it had been entered as one or more interactions, with an
    /// implicit termination at the end of the input. Stop on first error, discarding the rest
    /// of the input. Errors and warnings are collected apart from any exception arising from execution
    /// which is returned via a Choice. Parsing is performed on the current thread, and execution is performed 
    /// synchronously on the 'main' thread.
    ///
    /// Due to a current limitation, it is not fully thread-safe to run this operation concurrently with evaluation triggered
    /// by input from 'stdin'.
    member EvalExpressionNonThrowing: code: string -> Choice<FsiValue option, exn> * FSharpDiagnostic[]

    /// Format a value to a string using the current PrintDepth, PrintLength etc settings provided by the active fsi configuration object
    member FormatValue: reflectionValue: obj * reflectionType: Type -> string

    /// Raised when an interaction is successfully typechecked and executed, resulting in an update to the
    /// type checking state.  
    ///
    /// This event is triggered after parsing and checking, either via input from 'stdin', or via a call to EvalInteraction.
    member PartialAssemblySignatureUpdated: IEvent<unit>

    /// Typecheck the given script fragment in the type checking context implied by the current state
    /// of F# Interactive. The results can be used to access intellisense, perform resolutions,
    /// check brace matching and other information.
    ///
    /// Operations may be run concurrently with other requests to the InteractiveChecker.
    member ParseAndCheckInteraction: code: string -> FSharpParseFileResults * FSharpCheckFileResults * FSharpCheckProjectResults

    /// The single, global interactive checker to use in conjunction with other operations
    /// on the FsiEvaluationSession.  
    ///
    /// If you are using an FsiEvaluationSession in this process, you should only use this InteractiveChecker 
    /// for additional checking operations.
    member InteractiveChecker: FSharpChecker

    /// Get a handle to the resolved view of the current signature of the incrementally generated assembly.
    member CurrentPartialAssemblySignature: FSharpAssemblySignature

    /// Get a handle to the dynamically generated assembly
    member DynamicAssembly: System.Reflection.Assembly

    /// A host calls this to determine if the --gui parameter is active
    member IsGui: bool

    /// A host calls this to get the active language ID if provided by fsi-server-lcid
    member LCID: int option

    /// A host calls this to report an unhandled exception in a standard way, e.g. an exception on the GUI thread gets printed to stderr
    member ReportUnhandledException: exn: exn -> unit

    /// Event fires when a root-level value is bound to an identifier, e.g., via `let x = ...`.
    member ValueBound: IEvent<obj * Type * string>

    /// Gets the root-level values that are bound to an identifier
    member GetBoundValues: unit -> FsiBoundValue list

    /// Tries to find a root-level value that is bound to the given identifier
    member TryFindBoundValue: name: string -> FsiBoundValue option

    /// Creates a root-level value with the given name and .NET object.
    /// If the .NET object contains types from assemblies that are not referenced in the interactive session, it will try to implicitly resolve them by default configuration.
    /// Name must be a valid identifier.
    member AddBoundValue: name: string * value: obj -> unit

    /// Load the dummy interaction, load the initial files, and,
    /// if interacting, start the background thread to read the standard input.
    ///
    /// Performs these steps:
    ///    - Load the dummy interaction, if any
    ///    - Set up exception handling, if any
    ///    - Load the initial files, if any
    ///    - Start the background thread to read the standard input, if any
    ///    - Sit in the GUI event loop indefinitely, if needed

    member Run: unit -> unit

    /// Get a configuration that uses the 'fsi' object (normally from FSharp.Compiler.Interactive.Settings.dll,
    /// an object from another DLL with identical characteristics) to provide an implementation of the configuration.
    /// The flag indicates if FSharp.Compiler.Interactive.Settings.dll  is referenced by default.
    static member GetDefaultConfiguration: fsiObj: obj * useFsiAuxLib: bool -> FsiEvaluationSessionHostConfig

    /// Get a configuration that uses the 'fsi' object (normally from FSharp.Compiler.Interactive.Settings.dll,
    /// an object from another DLL with identical characteristics) to provide an implementation of the configuration.
    /// FSharp.Compiler.Interactive.Settings.dll  is referenced by default.
    static member GetDefaultConfiguration: fsiObj: obj -> FsiEvaluationSessionHostConfig

    /// Get a configuration that uses a private inbuilt implementation of the 'fsi' object and does not
    /// implicitly reference FSharp.Compiler.Interactive.Settings.dll. 
    static member GetDefaultConfiguration: unit -> FsiEvaluationSessionHostConfig

/// A default implementation of the 'fsi' object, used by GetDefaultConfiguration()
module Settings = 
    /// <summary>An event loop used by the currently executing F# Interactive session to execute code
    /// in the context of a GUI or another event-based system.</summary>
    type IEventLoop =
        /// <summary>Run the event loop.</summary>
        /// <returns>True if the event loop was restarted; false otherwise.</returns>
        abstract Run: unit -> bool  
        /// <summary>Request that the given operation be run synchronously on the event loop.</summary>
        /// <returns>The result of the operation.</returns>
        abstract Invoke: (unit -> 'T) -> 'T 
        /// <summary>Schedule a restart for the event loop.</summary>
        abstract ScheduleRestart: unit -> unit

    [<Sealed>]
    /// <summary>Operations supported by the currently executing F# Interactive session.</summary>
    type InteractiveSettings =
        /// <summary>Get or set the floating point format used in the output of the interactive session.</summary>
        member FloatingPointFormat: string with get,set

        /// <summary>Get or set the format provider used in the output of the interactive session.</summary>
        member FormatProvider: IFormatProvider  with get,set

        /// <summary>Get or set the print width of the interactive session.</summary>
        member PrintWidth: int  with get,set

        /// <summary>Get or set the print depth of the interactive session.</summary>
        member PrintDepth: int  with get,set

        /// <summary>Get or set the total print length of the interactive session.</summary>
        member PrintLength: int  with get,set

        /// <summary>Get or set the total print size of the interactive session.</summary>
        member PrintSize: int  with get,set      

        /// <summary>When set to 'false', disables the display of properties of evaluated objects in the output of the interactive session.</summary>
        member ShowProperties: bool  with get,set

        /// <summary>When set to 'false', disables the display of sequences in the output of the interactive session.</summary>
        member ShowIEnumerable: bool  with get,set

        /// <summary>When set to 'false', disables the display of declaration values in the output of the interactive session.</summary>
        member ShowDeclarationValues: bool  with get,set      

        /// <summary>Register a printer that controls the output of the interactive session.</summary>
        member AddPrinter: ('T -> string) -> unit

        /// <summary>Register a print transformer that controls the output of the interactive session.</summary>
        member AddPrintTransformer: ('T -> obj) -> unit

        member internal AddedPrinters: Choice<(Type * (obj -> string)), 
                                               (Type * (obj -> obj))>  list

    
        /// <summary>The command line arguments after ignoring the arguments relevant to the interactive
        /// environment and replacing the first argument with the name of the last script file,
        /// if any. Thus 'fsi.exe test1.fs test2.fs -- hello goodbye' will give arguments
        /// 'test2.fs', 'hello', 'goodbye'.  This value will normally be different to those
        /// returned by System.Environment.GetCommandLineArgs.</summary>
        member CommandLineArgs: string [] with get,set
    
        /// <summary>Gets or sets a the current event loop being used to process interactions.</summary>
        member EventLoop: IEventLoop with get,set

    /// A default implementation of the 'fsi' object, used by GetDefaultConfiguration().  Note this
    /// is a different object to FSharp.Compiler.Interactive.Settings.fsi in FSharp.Compiler.Interactive.Settings.dll,
    /// which can be used as an alternative implementation of the interactive settings if passed as a parameter
    /// to GetDefaultConfiguration(fsiObj).
    val fsi: InteractiveSettings

/// Defines a read-only input stream used to feed content to the hosted F# Interactive dynamic compiler.
[<AllowNullLiteral>]
type CompilerInputStream = 
    inherit Stream
    new: unit -> CompilerInputStream
    /// Feeds content into the stream.
    member Add: str:string -> unit

/// Defines a write-only stream used to capture output of the hosted F# Interactive dynamic compiler.
[<AllowNullLiteral>]
type CompilerOutputStream  =
    inherit Stream
    new: unit -> CompilerOutputStream

    member Read: unit -> string
