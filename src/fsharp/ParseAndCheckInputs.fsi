// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Contains logic to coordinate the parsing and checking of one or a group of files
module internal FSharp.Compiler.ParseAndCheckInputs

open Internal.Utilities.Library
open FSharp.Compiler.CheckExpressions
open FSharp.Compiler.CheckDeclarations
open FSharp.Compiler.CompilerGlobalState
open FSharp.Compiler.CompilerConfig
open FSharp.Compiler.CompilerImports
open FSharp.Compiler.DependencyManager
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Syntax
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.TypedTree
open FSharp.Compiler.UnicodeLexing

val IsScript: string -> bool

val ComputeQualifiedNameOfFileFromUniquePath: range * string list -> QualifiedNameOfFile

val PrependPathToInput: Ident list -> ParsedInput -> ParsedInput

/// State used to de-deduplicate module names along a list of file names
type ModuleNamesDict = Map<string,Map<string,QualifiedNameOfFile>>

/// Checks if a ParsedInput is using a module name that was already given and deduplicates the name if needed.
val DeduplicateParsedInputModuleName: ModuleNamesDict -> ParsedInput -> ParsedInput * ModuleNamesDict

/// Parse a single input (A signature file or implementation file)
val ParseInput: (Lexbuf -> Parser.token) * ErrorLogger * Lexbuf * string option * string * isLastCompiland:(bool * bool) -> ParsedInput

/// A general routine to process hash directives
val ProcessMetaCommandsFromInput : 
    (('T -> range * string -> 'T) * 
     ('T -> range * string * Directive -> 'T) *
     ('T -> range * string -> unit))
      -> TcConfigBuilder * ParsedInput * string * 'T 
      -> 'T

/// Process all the #r, #I etc. in an input.  For non-scripts report warnings about ignored directives.
val ApplyMetaCommandsFromInputToTcConfig: TcConfig * ParsedInput * string * DependencyProvider -> TcConfig

/// Process the #nowarn in an input and integrate them into the TcConfig
val ApplyNoWarnsToTcConfig: TcConfig * ParsedInput * string -> TcConfig

/// Parse one input file
val ParseOneInputFile: TcConfig * Lexhelp.LexResourceManager * conditionalCompilationDefines: string list * string * isLastCompiland: (bool * bool) * ErrorLogger * retryLocked: bool -> ParsedInput

/// Parse multiple input files from disk
val ParseInputFiles: TcConfig * Lexhelp.LexResourceManager * conditionalCompilationDefines: string list * string list * ErrorLogger * Exiter * createErrorLogger: (Exiter -> CapturingErrorLogger) * retryLocked: bool -> (ParsedInput * string) list

/// Get the initial type checking environment including the loading of mscorlib/System.Core, FSharp.Core
/// applying the InternalsVisibleTo in referenced assemblies and opening 'Checked' if requested.
val GetInitialTcEnv: assemblyName: string * range * TcConfig * TcImports * TcGlobals -> TcEnv
                
[<Sealed>]
/// Represents the incremental type checking state for a set of inputs
type TcState =
    member NiceNameGenerator: NiceNameGenerator

    /// The CcuThunk for the current assembly being checked
    member Ccu: CcuThunk
    
    /// Get the typing environment implied by the set of signature files and/or inferred signatures of implementation files checked so far
    member TcEnvFromSignatures: TcEnv

    /// Get the typing environment implied by the set of implementation files checked so far
    member TcEnvFromImpls: TcEnv

    /// The inferred contents of the assembly, containing the signatures of all files.
    // a.fsi + b.fsi + c.fsi (after checking implementation file for c.fs)
    member CcuSig: ModuleOrNamespaceType

    member NextStateAfterIncrementalFragment: TcEnv -> TcState

    member CreatesGeneratedProvidedTypes: bool

/// Get the initial type checking state for a set of inputs
val GetInitialTcState: 
    range * string * TcConfig * TcGlobals * TcImports * NiceNameGenerator * TcEnv -> TcState

/// Check one input, returned as an Eventually computation
val TypeCheckOneInput:
    checkForErrors:(unit -> bool) *
    TcConfig *
    TcImports *
    TcGlobals *
    LongIdent option *
    NameResolution.TcResultsSink *
    TcState *
    ParsedInput *
    skipImplIfSigExists: bool
      -> Cancellable<(TcEnv * TopAttribs * TypedImplFile option * ModuleOrNamespaceType) * TcState>

/// Finish the checking of multiple inputs 
val TypeCheckMultipleInputsFinish: (TcEnv * TopAttribs * 'T option * 'U) list * TcState -> (TcEnv * TopAttribs * 'T list * 'U list) * TcState
    
/// Finish the checking of a closed set of inputs 
val TypeCheckClosedInputSetFinish:
    TypedImplFile list *
    TcState
      -> TcState * TypedImplFile list * ModuleOrNamespace

/// Check a closed set of inputs 
val TypeCheckClosedInputSet:
    CompilationThreadToken *
    checkForErrors: (unit -> bool) *
    TcConfig *
    TcImports *
    TcGlobals *
    LongIdent option *
    TcState * ParsedInput list
      -> TcState * TopAttribs * TypedImplFile list * TcEnv

/// Check a single input and finish the checking
val TypeCheckOneInputAndFinish :
    checkForErrors: (unit -> bool) *
    TcConfig *
    TcImports *
    TcGlobals *
    LongIdent option *
    NameResolution.TcResultsSink *
    TcState *
    ParsedInput 
      -> Cancellable<(TcEnv * TopAttribs * TypedImplFile list * ModuleOrNamespaceType list) * TcState>

val GetScopedPragmasForInput: input: ParsedInput -> ScopedPragma list

val ParseOneInputLexbuf:
    tcConfig: TcConfig *
    lexResourceManager: Lexhelp.LexResourceManager *
    conditionalCompilationDefines: string list *
    lexbuf: Lexbuf *
    filename: string *
    isLastCompiland: (bool * bool) *
    errorLogger: ErrorLogger
      -> ParsedInput

val EmptyParsedInput:
    filename: string *
    isLastCompiland: (bool * bool)
      -> ParsedInput 
