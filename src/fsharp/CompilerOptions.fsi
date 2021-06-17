// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Compiler Option Parser
module internal FSharp.Compiler.CompilerOptions

open System
open FSharp.Compiler.CompilerConfig
open FSharp.Compiler.Diagnostics

// For command-line options that can be suffixed with +/-
[<RequireQualifiedAccess>]
type OptionSwitch =
    | On
    | Off

/// The spec value describes the action of the argument,
/// and whether it expects a following parameter.
type OptionSpec = 
    | OptionClear of bool ref
    | OptionFloat of (float -> unit)
    | OptionInt of (int -> unit)
    | OptionSwitch of (OptionSwitch -> unit)
    | OptionIntList of (int -> unit)
    | OptionIntListSwitch of (int -> OptionSwitch -> unit)
    | OptionRest of (string -> unit)
    | OptionSet of bool ref
    | OptionString of (string -> unit)
    | OptionStringList of (string -> unit)
    | OptionStringListSwitch of (string -> OptionSwitch -> unit)
    | OptionUnit of (unit -> unit)
    | OptionHelp of (CompilerOptionBlock list -> unit)                      // like OptionUnit, but given the "options"
    | OptionGeneral of (string list -> bool) * (string list -> string list) // Applies? * (ApplyReturningResidualArgs)

and  CompilerOption      =
    | CompilerOption of name: string * argumentDescriptionString: string * actionSpec: OptionSpec * deprecationError: Option<exn> * helpText: string option

and  CompilerOptionBlock =
    | PublicOptions  of heading: string * options: CompilerOption list
    | PrivateOptions of options: CompilerOption list

val PrintCompilerOptionBlocks: CompilerOptionBlock list -> unit  // for printing usage

val DumpCompilerOptionBlocks: CompilerOptionBlock list -> unit  // for QA

val FilterCompilerOptionBlock: (CompilerOption -> bool) -> CompilerOptionBlock -> CompilerOptionBlock

/// Parse and process a set of compiler options
val ParseCompilerOptions: (string -> unit) * CompilerOptionBlock list * string list -> unit

val DisplayBannerText: TcConfigBuilder -> unit

val GetCoreFscCompilerOptions: TcConfigBuilder -> CompilerOptionBlock list

val GetCoreFsiCompilerOptions: TcConfigBuilder -> CompilerOptionBlock list

val GetCoreServiceCompilerOptions: TcConfigBuilder -> CompilerOptionBlock list

/// Apply args to TcConfigBuilder and return new list of source files
val ApplyCommandLineArgs: tcConfigB: TcConfigBuilder * sourceFiles: string list * argv: string list -> string list

// Expose the "setters" for some user switches, to enable setting of defaults
val SetOptimizeSwitch: TcConfigBuilder -> OptionSwitch -> unit

val SetTailcallSwitch: TcConfigBuilder -> OptionSwitch -> unit

val SetDebugSwitch: TcConfigBuilder -> string option -> OptionSwitch -> unit

val PrintOptionInfo: TcConfigBuilder -> unit

val SetTargetProfile: TcConfigBuilder -> string -> unit

// Miscellany
val ignoreFailureOnMono1_1_16: (unit -> unit) -> unit

val mutable enableConsoleColoring: bool

val DoWithColor: ConsoleColor -> (unit -> 'a) -> 'a

val DoWithDiagnosticColor: FSharpDiagnosticSeverity -> (unit -> 'a) -> 'a

val ReportTime: TcConfig -> string -> unit

val GetAbbrevFlagSet: TcConfigBuilder -> bool -> Set<string>

val PostProcessCompilerArgs: string Set -> string [] -> string list
