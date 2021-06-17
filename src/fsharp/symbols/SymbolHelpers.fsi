// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

//----------------------------------------------------------------------------
// Helpers for quick info and information about items
//----------------------------------------------------------------------------

namespace FSharp.Compiler.Diagnostics

    open System
    open FSharp.Compiler.Text
    open FSharp.Compiler.ErrorLogger

    /// Represents a diagnostic produced by the F# compiler
    [<Class>]
    type public FSharpDiagnostic = 

        /// Gets the file name for the diagnostic
        member FileName: string

        /// Gets the start position for the diagnostic
        member Start: Position

        /// Gets the end position for the diagnostic
        member End: Position

        /// Gets the start column for the diagnostic
        member StartColumn: int

        /// Gets the end column for the diagnostic
        member EndColumn: int

        /// Gets the start column for the diagnostic
        member StartLine: int

        /// Gets the end column for the diagnostic
        member EndLine: int

        /// Gets the range for the diagnostic
        member Range: range

        /// Gets the severity for the diagnostic
        member Severity: FSharpDiagnosticSeverity

        /// Gets the message for the diagnostic
        member Message: string

        /// Gets the sub-category for the diagnostic
        member Subcategory: string

        /// Gets the number for the diagnostic
        member ErrorNumber: int

        /// Gets the number prefix for the diagnostic, usually "FS" but may differ for analyzers
        member ErrorNumberPrefix: string

        /// Gets the full error number text e.g "FS0031"
        member ErrorNumberText: string

        /// Creates a diagnostic, e.g. for reporting from an analyzer
        static member Create: severity: FSharpDiagnosticSeverity * message: string * number: int * range: range * ?numberPrefix: string * ?subcategory: string -> FSharpDiagnostic

        static member internal CreateFromExceptionAndAdjustEof: PhasedDiagnostic * severity: FSharpDiagnosticSeverity * range * lastPosInFile: (int*int) * suggestNames: bool -> FSharpDiagnostic

        static member internal CreateFromException: PhasedDiagnostic * severity: FSharpDiagnosticSeverity * range * suggestNames: bool -> FSharpDiagnostic

        /// Newlines are recognized and replaced with (ASCII 29, the 'group separator'), 
        /// which is decoded by the IDE with 'NewlineifyErrorString' back into newlines, so that multi-line errors can be displayed in QuickInfo
        static member NewlineifyErrorString: message:string -> string

        /// Newlines are recognized and replaced with (ASCII 29, the 'group separator'), 
        /// which is decoded by the IDE with 'NewlineifyErrorString' back into newlines, so that multi-line errors can be displayed in QuickInfo
        static member NormalizeErrorString: text:string -> string

    //----------------------------------------------------------------------------
    // Internal only

    // Implementation details used by other code in the compiler    
    [<Sealed>]
    type internal ErrorScope = 
        interface IDisposable
        new : unit -> ErrorScope
        member Diagnostics : FSharpDiagnostic list
        static member Protect<'a> : range -> (unit->'a) -> (string->'a) -> 'a

    /// An error logger that capture errors, filtering them according to warning levels etc.
    type internal CompilationErrorLogger = 
        inherit ErrorLogger

        /// Create the diagnostics logger
        new: debugName:string * options: FSharpDiagnosticOptions -> CompilationErrorLogger
            
        /// Get the captured diagnostics
        member GetDiagnostics: unit -> (PhasedDiagnostic * FSharpDiagnosticSeverity)[]

    module internal DiagnosticHelpers = 
        val ReportDiagnostic: FSharpDiagnosticOptions * allErrors: bool * mainInputFileName: string * fileInfo: (int * int) * (PhasedDiagnostic * FSharpDiagnosticSeverity) * suggestNames: bool -> FSharpDiagnostic list

        val CreateDiagnostics: FSharpDiagnosticOptions * allErrors: bool * mainInputFileName: string * seq<(PhasedDiagnostic * FSharpDiagnosticSeverity)> * suggestNames: bool -> FSharpDiagnostic[]

namespace FSharp.Compiler.Symbols

    open Internal.Utilities.Library
    open FSharp.Compiler 
    open FSharp.Compiler.TcGlobals 
    open FSharp.Compiler.Infos
    open FSharp.Compiler.NameResolution
    open FSharp.Compiler.InfoReader
    open FSharp.Compiler.Syntax
    open FSharp.Compiler.Text
    open FSharp.Compiler.Xml
    open FSharp.Compiler.TypedTree
    open FSharp.Compiler.TypedTreeOps

    /// Describe a comment as either a block of text or a file+signature reference into an intellidoc file.
    //
    // Note: instances of this type do not hold any references to any compiler resources.
    [<RequireQualifiedAccess>]
    type public FSharpXmlDoc =
        /// No documentation is available
        | None

        /// The text for documentation for in-memory references. 
        | FromXmlText of XmlDoc

        /// Indicates that the XML for the documentation can be found in a .xml documentation file for the given DLL, using the given signature key
        | FromXmlFile of dllName: string * xmlSig: string


    // Implementation details used by other code in the compiler    
    module internal SymbolHelpers =
        val ParamNameAndTypesOfUnaryCustomOperation : TcGlobals -> MethInfo -> ParamNameAndType list

        val GetXmlCommentForItem : InfoReader -> range -> Item -> FSharpXmlDoc

        val RemoveDuplicateItems : TcGlobals -> ItemWithInst list -> ItemWithInst list

        val RemoveExplicitlySuppressed : TcGlobals -> ItemWithInst list -> ItemWithInst list

        val GetF1Keyword : TcGlobals -> Item -> string option

        val rangeOfItem : TcGlobals -> bool option -> Item -> range option

        val fileNameOfItem : TcGlobals -> string option -> range -> Item -> string

        val FullNameOfItem : TcGlobals -> Item -> string

        val ccuOfItem : TcGlobals -> Item -> CcuThunk option

        val IsAttribute : InfoReader -> Item -> bool

        val IsExplicitlySuppressed : TcGlobals -> Item -> bool

        val FlattenItems : TcGlobals -> range -> Item -> Item list

#if !NO_EXTENSIONTYPING
        val (|ItemIsProvidedType|_|) : TcGlobals -> Item -> TyconRef option

        val (|ItemIsWithStaticArguments|_|): range -> TcGlobals -> Item -> Tainted<ExtensionTyping.ProvidedParameterInfo>[] option

        val (|ItemIsProvidedTypeWithStaticArguments|_|): range -> TcGlobals -> Item -> Tainted<ExtensionTyping.ProvidedParameterInfo>[] option
#endif

        val SimplerDisplayEnv : DisplayEnv -> DisplayEnv

        val ItemDisplayPartialEquality: g:TcGlobals -> IPartialEqualityComparer<Item>    

        val GetXmlCommentForMethInfoItem: infoReader:InfoReader -> m:range -> d:Item -> minfo:MethInfo -> FSharpXmlDoc    
        
        val FormatTyparMapping: denv:DisplayEnv -> prettyTyparInst:TyparInst -> Layout list

