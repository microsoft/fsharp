// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.CodeAnalysis

open System
open System.Threading
open Internal.Utilities.Library
open FSharp.Compiler
open FSharp.Compiler.AbstractIL
open FSharp.Compiler.CheckDeclarations
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.CompilerConfig
open FSharp.Compiler.CompilerImports
open FSharp.Compiler.DependencyManager
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.EditorServices
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.NameResolution
open FSharp.Compiler.ParseAndCheckInputs
open FSharp.Compiler.ScriptClosure
open FSharp.Compiler.Syntax
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.TypedTree

/// Lookup the global static cache for building the FrameworkTcImports
type internal FrameworkImportsCache = 
    new : size: int -> FrameworkImportsCache

    member Get : CompilationThreadToken * TcConfig -> Cancellable<TcGlobals * TcImports * AssemblyResolution list * UnresolvedAssemblyReference list>

    member Clear: CompilationThreadToken -> unit

    member Downsize: CompilationThreadToken -> unit
  
/// Used for unit testing
module internal IncrementalBuilderEventTesting =

  type IBEvent =
        | IBEParsed of string // filename
        | IBETypechecked of string // filename
        | IBECreated

  val GetMostRecentIncrementalBuildEvents : int -> IBEvent list
  val GetCurrentIncrementalBuildEventNum : unit -> int

/// Accumulated results of type checking. The minimum amount of state in order to continue type-checking following files.
[<NoEquality; NoComparison>]
type internal TcInfo =
    {
        tcState: TcState
        tcEnvAtEndOfFile: CheckExpressions.TcEnv

        /// Disambiguation table for module names
        moduleNamesDict: ModuleNamesDict

        topAttribs: TopAttribs option

        latestCcuSigForFile: ModuleOrNamespaceType option

        /// Accumulated errors, last file first
        tcErrorsRev:(PhasedDiagnostic * FSharpDiagnosticSeverity)[] list

        tcDependencyFiles: string list

        sigNameOpt: (string * QualifiedNameOfFile) option
    }

     member TcErrors: (PhasedDiagnostic * FSharpDiagnosticSeverity)[]

/// Accumulated results of type checking. Optional data that isn't needed to type-check a file, but needed for more information for in tooling.
[<NoEquality; NoComparison>]
type internal TcInfoExtras =
    {
      /// Accumulated resolutions, last file first
      tcResolutionsRev: TcResolutions list

      /// Accumulated symbol uses, last file first
      tcSymbolUsesRev: TcSymbolUses list

      /// Accumulated 'open' declarations, last file first
      tcOpenDeclarationsRev: OpenDeclaration[] list

      /// Result of checking most recent file, if any
      latestImplFile: TypedImplFile option
      
      /// If enabled, stores a linear list of ranges and strings that identify an Item(symbol) in a file. Used for background find all references.
      itemKeyStore: ItemKeyStore option
      
      /// If enabled, holds semantic classification information for Item(symbol)s in a file.
      semanticClassificationKeyStore: SemanticClassificationKeyStore option
    }

    member TcSymbolUses: TcSymbolUses list

/// Represents the state in the incremental graph associated with checking a file
[<Sealed>]
type internal PartialCheckResults = 

    member TcImports: TcImports 

    member TcGlobals: TcGlobals 

    member TcConfig: TcConfig 

    member TimeStamp: DateTime 

    member TryTcInfo: TcInfo option

    /// Compute the "TcInfo" part of the results.  If `enablePartialTypeChecking` is false then
    /// extras will also be available.
    member GetTcInfo: unit -> Eventually<TcInfo>

    /// Compute both the "TcInfo" and "TcInfoExtras" parts of the results.
    /// Can cause a second type-check if `enablePartialTypeChecking` is true in the checker.
    /// Only use when it's absolutely necessary to get rich information on a file.
    member GetTcInfoWithExtras: unit -> Eventually<TcInfo * TcInfoExtras>

    /// Compute the "ItemKeyStore" parts of the results.
    /// Can cause a second type-check if `enablePartialTypeChecking` is true in the checker.
    /// Only use when it's absolutely necessary to get rich information on a file.
    member TryGetItemKeyStore: unit -> Eventually<ItemKeyStore option>

    /// Can cause a second type-check if `enablePartialTypeChecking` is true in the checker.
    /// Only use when it's absolutely necessary to get rich information on a file.
    member GetSemanticClassification: unit -> Eventually<SemanticClassificationKeyStore option>

    member TimeStamp: DateTime 

/// Manages an incremental build graph for the build of an F# project
[<Class>]
type internal IncrementalBuilder = 

      /// The TcConfig passed in to the builder creation.
      member TcConfig : TcConfig

      /// The full set of source files including those from options
      member SourceFiles : string list

      /// Raised just before a file is type-checked, to invalidate the state of the file in VS and force VS to request a new direct typecheck of the file.
      /// The incremental builder also typechecks the file (error and intellisense results from the background builder are not
      /// used by VS). 
      member BeforeFileChecked : IEvent<string>

      /// Raised just after a file is parsed
      member FileParsed : IEvent<string>

      /// Raised just after a file is checked
      member FileChecked : IEvent<string>

      /// Raised just after the whole project has finished type checking. At this point, accessing the
      /// overall analysis results for the project will be quick.
      member ProjectChecked : IEvent<unit>

#if !NO_EXTENSIONTYPING
      /// Raised when a type provider invalidates the build.
      member ImportsInvalidatedByTypeProvider : IEvent<string>
#endif

      /// The list of files the build depends on
      member AllDependenciesDeprecated : string[]

      /// The project build. Return true if the background work is finished.
      member PopulatePartialCheckingResults: CompilationThreadToken -> Eventually<unit>

      /// Get the preceding typecheck state of a slot, without checking if it is up-to-date w.r.t.
      /// the timestamps on files and referenced DLLs prior to this one. Return None if the result is not available.
      /// This is a very quick operation.
      ///
      /// This is safe for use from non-compiler threads but the objects returned must in many cases be accessed only from the compiler thread.
      member GetCheckResultsBeforeFileInProjectEvenIfStale: filename:string -> PartialCheckResults option

      /// Get the preceding typecheck state of a slot, but only if it is up-to-date w.r.t.
      /// the timestamps on files and referenced DLLs prior to this one. Return None if the result is not available.
      /// This is a relatively quick operation.
      ///
      /// This is safe for use from non-compiler threads
      member AreCheckResultsBeforeFileInProjectReady: filename:string -> bool

      /// Get the preceding typecheck state of a slot, WITH checking if it is up-to-date w.r.t. However, files will not be parsed or checked.
      /// the timestamps on files and referenced DLLs prior to this one. Return None if the result is not available or if it is not up-to-date.
      ///
      /// This is safe for use from non-compiler threads but the objects returned must in many cases be accessed only from the compiler thread.
      member TryGetCheckResultsBeforeFileInProject: filename: string -> PartialCheckResults option

      /// Get the preceding typecheck state of a slot. Compute the entire type check of the project up
      /// to the necessary point if the result is not available. This may be a long-running operation.
      ///
      // TODO: make this an Eventually (which can be scheduled) or an Async (which can be cancelled)
      member GetCheckResultsBeforeFileInProject : CompilationThreadToken * filename:string -> Cancellable<PartialCheckResults>

      /// Get the typecheck state after checking a file. Compute the entire type check of the project up
      /// to the necessary point if the result is not available. This may be a long-running operation.
      ///
      // TODO: make this an Eventually (which can be scheduled) or an Async (which can be cancelled)
      member GetCheckResultsAfterFileInProject : CompilationThreadToken * filename:string -> Cancellable<PartialCheckResults>

      /// Get the typecheck state after checking a file. Compute the entire type check of the project up
      /// to the necessary point if the result is not available. This may be a long-running operation.
      /// This will get full type-check info for the file, meaning no partial type-checking.
      ///
      // TODO: make this an Eventually (which can be scheduled) or an Async (which can be cancelled)
      member GetFullCheckResultsAfterFileInProject : CompilationThreadToken * filename:string -> Cancellable<PartialCheckResults>

      /// Get the typecheck result after the end of the last file. The typecheck of the project is not 'completed'.
      /// This may be a long-running operation.
      ///
      // TODO: make this an Eventually (which can be scheduled) or an Async (which can be cancelled)
      member GetCheckResultsAfterLastFileInProject : CompilationThreadToken -> Cancellable<PartialCheckResults>

      /// Get the final typecheck result. If 'generateTypedImplFiles' was set on Create then the TypedAssemblyAfterOptimization will contain implementations.
      /// This may be a long-running operation.
      ///
      // TODO: make this an Eventually (which can be scheduled) or an Async (which can be cancelled)
      member GetCheckResultsAndImplementationsForProject : CompilationThreadToken -> Cancellable<PartialCheckResults * IL.ILAssemblyRef * IRawFSharpAssemblyData option * TypedImplFile list option>

      /// Get the final typecheck result. If 'generateTypedImplFiles' was set on Create then the TypedAssemblyAfterOptimization will contain implementations.
      /// This may be a long-running operation.
      /// This will get full type-check info for the project, meaning no partial type-checking.
      ///
      // TODO: make this an Eventually (which can be scheduled) or an Async (which can be cancelled)
      member GetFullCheckResultsAndImplementationsForProject : CompilationThreadToken -> Cancellable<PartialCheckResults * IL.ILAssemblyRef * IRawFSharpAssemblyData option * TypedImplFile list option>

      /// Get the logical time stamp that is associated with the output of the project if it were gully built immediately
      member GetLogicalTimeStampForProject: TimeStampCache -> DateTime

      /// Does the given file exist in the builder's pipeline?
      member ContainsFile: filename: string -> bool

      /// Await the untyped parse results for a particular slot in the vector of parse results.
      ///
      /// This may be a marginally long-running operation (parses are relatively quick, only one file needs to be parsed)
      member GetParseResultsForFile: filename:string -> ParsedInput * range * string * (PhasedDiagnostic * FSharpDiagnosticSeverity)[]

      /// Create the incremental builder
      static member TryCreateIncrementalBuilderForProjectOptions:
          CompilationThreadToken *
          LegacyReferenceResolver *
          defaultFSharpBinariesDir: string * 
          FrameworkImportsCache *
          loadClosureOpt:LoadClosure option *
          sourceFiles:string list *
          commandLineArgs:string list *
          projectReferences: IProjectReference list *
          projectDirectory:string *
          useScriptResolutionRules:bool *
          keepAssemblyContents: bool *
          keepAllBackgroundResolutions: bool *
          tryGetMetadataSnapshot: ILBinaryReader.ILReaderTryGetMetadataSnapshot *
          suggestNamesForErrors: bool *
          keepAllBackgroundSymbolUses: bool *
          enableBackgroundItemKeyStoreAndSemanticClassification: bool *
          enablePartialTypeChecking: bool *
          dependencyProvider: DependencyProvider option
             -> Cancellable<IncrementalBuilder option * FSharpDiagnostic[]>

/// Generalized Incremental Builder. This is exposed only for unit testing purposes.
module internal IncrementalBuild =

    /// Used for unit testing. Causes all steps of underlying incremental graph evaluation to cancel
    val LocallyInjectCancellationFault : unit -> IDisposable
