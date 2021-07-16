// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Test

open FSharp.Compiler.Interactive.Shell
open FSharp.Compiler.IO
open FSharp.Compiler.Diagnostics
open FSharp.Test.Assert
open FSharp.Test.Utilities
open FSharp.Test.ScriptHelpers
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.CSharp
open NUnit.Framework
open System
open System.Collections.Immutable
open System.IO
open System.Text
open System.Text.RegularExpressions


module rec Compiler =
    type BaselineFile = { FilePath: string; Content: string option }

    type Baseline =
        { SourceFilename: string option
          OutputBaseline: BaselineFile
          ILBaseline:     BaselineFile }

    type TestType =
        | Text of string
        | Path of string

    type CompilationUnit =
        | FS  of FSharpCompilationSource
        | CS  of CSharpCompilationSource
        | IL  of ILCompilationSource
        override this.ToString() = match this with | FS fs -> fs.ToString() | _ -> (sprintf "%A" this   )

    type FSharpCompilationSource =
        { Source:         TestType
          Baseline:       Baseline option
          Options:        string list
          OutputType:     CompileOutput
          SourceKind:     SourceKind
          Name:           string option
          IgnoreWarnings: bool
          References:     CompilationUnit list }
        override this.ToString() = match this.Name with | Some n -> n | _ -> (sprintf "%A" this)

    type CSharpCompilationSource =
        { Source:          TestType
          LangVersion:     CSharpLanguageVersion
          TargetFramework: TargetFramework
          Name:            string option
          References:      CompilationUnit list }

    type ILCompilationSource =
        { Source:     TestType
          References: CompilationUnit list  }

    type ErrorType = Error of int | Warning of int

    type Line = Line of int
    type Col = Col of int

    type Range =
        { StartLine:   int
          StartColumn: int
          EndLine:     int
          EndColumn:   int }

    type ErrorInfo =
        { Error:   ErrorType
          Range:   Range
          Message: string }

    type EvalOutput = Result<FsiValue option, exn>

    type ExecutionOutput =
        { ExitCode: int
          StdOut:   string
          StdErr:   string }

    type RunOutput =
        | EvalOutput of EvalOutput
        | ExecutionOutput of ExecutionOutput

    type Output =
        { OutputPath:   string option
          Dependencies: string list
          Adjust:       int
          Diagnostics:  ErrorInfo list
          Output:       RunOutput option }

    type TestResult =
        | Success of Output
        | Failure of Output

    let private defaultOptions : string list = []

    // Not very safe version of reading stuff from file, but we want to fail fast for now if anything goes wrong.
    let private getSource (src: TestType) : string =
        match src with
        | Text t -> t
        | Path p ->
            use stream = FileSystem.OpenFileForReadShim(p)
            stream.ReadAllText()

    let private fsFromString (source: string) (kind: SourceKind) : FSharpCompilationSource =
        match source with
        | null -> failwith "Source cannot be null"
        | _ ->
            { Source         = Text source
              Baseline       = None
              Options        = defaultOptions
              OutputType     = Library
              SourceKind     = kind
              Name           = None
              IgnoreWarnings = false
              References     = [] }

    let private csFromString (source: string) : CSharpCompilationSource =
        match source with
        | null -> failwith "Source cannot be null"
        | _ ->
            { Source          = Text source
              LangVersion     = CSharpLanguageVersion.CSharp9
              TargetFramework = TargetFramework.Current
              Name            = None
              References      = [] }

    let private fromFSharpDiagnostic (errors: FSharpDiagnostic[]) : ErrorInfo list =
        let toErrorInfo (e: FSharpDiagnostic) : ErrorInfo =
            let errorNumber = e.ErrorNumber
            let severity = e.Severity

            let error = if severity = FSharpDiagnosticSeverity.Warning then Warning errorNumber else Error errorNumber

            { Error   = error
              Range   =
                  { StartLine   = e.StartLine
                    StartColumn = e.StartColumn
                    EndLine     = e.EndLine
                    EndColumn   = e.EndColumn }
              Message = e.Message }

        errors
        |> List.ofArray
        |> List.distinctBy (fun e -> e.Severity, e.ErrorNumber, e.StartLine, e.StartColumn, e.EndLine, e.EndColumn, e.Message)
        |> List.map toErrorInfo

    let private partitionErrors diagnostics = diagnostics |> List.partition (fun e -> match e.Error with Error _ -> true | _ -> false)

    let private getErrors diagnostics = diagnostics |> List.filter (fun e -> match e.Error with Error _ -> true | _ -> false)

    let private getWarnings diagnostics = diagnostics |> List.filter (fun e -> match e.Error with Warning _ -> true | _ -> false)

    let private adjustRange (range: Range) (adjust: int) : Range =
        { range with
                StartLine   = range.StartLine   - adjust
                StartColumn = range.StartColumn + 1
                EndLine     = range.EndLine     - adjust
                EndColumn   = range.EndColumn   + 1 }

    let Fsx (source: string) : CompilationUnit =
        fsFromString source SourceKind.Fsx |> FS

    let FSharp (source: string) : CompilationUnit =
        fsFromString source SourceKind.Fs |> FS

    let CSharp (source: string) : CompilationUnit =
        csFromString source |> CS

    let asFsx (cUnit: CompilationUnit) : CompilationUnit =
        match cUnit with
        | FS src -> FS { src with SourceKind = SourceKind.Fsx }
        | _ -> failwith "Only F# compilation can be of type Fsx."

    let asFs (cUnit: CompilationUnit) : CompilationUnit =
        match cUnit with
        | FS src -> FS { src with SourceKind = SourceKind.Fs }
        | _ -> failwith "Only F# compilation can be of type Fs."

    let withName (name: string) (cUnit: CompilationUnit) : CompilationUnit =
        match cUnit with
        | FS src -> FS { src with Name = Some name }
        | CS src -> CS { src with Name = Some name }
        | IL _ -> failwith "IL Compilation cannot be named."

    let withReferences (references: CompilationUnit list) (cUnit: CompilationUnit) : CompilationUnit =
        match cUnit with
        | FS fs -> FS { fs with References = fs.References @ references }
        | CS cs -> CS { cs with References = cs.References @ references }
        | IL _ -> failwith "References are not supported in IL"

    let private withOptionsHelper (options: string list) (message:string) (cUnit: CompilationUnit) : CompilationUnit =
        match cUnit with
        | FS fs -> FS { fs with Options = fs.Options @ options }
        | _ -> failwith message

    let withOptions (options: string list) (cUnit: CompilationUnit) : CompilationUnit =
        withOptionsHelper options "withOptions is only supported for F#" cUnit

    let withErrorRanges (cUnit: CompilationUnit) : CompilationUnit =
        withOptionsHelper [ "--test:ErrorRanges" ] "withErrorRanges is only supported on F#" cUnit

    let withLangVersion46 (cUnit: CompilationUnit) : CompilationUnit =
        withOptionsHelper [ "--langversion:4.6" ] "withLangVersion46 is only supported on F#" cUnit

    let withLangVersion47 (cUnit: CompilationUnit) : CompilationUnit =
        withOptionsHelper [ "--langversion:4.7" ] "withLangVersion47 is only supported on F#" cUnit

    let withLangVersion50 (cUnit: CompilationUnit) : CompilationUnit =
        withOptionsHelper [ "--langversion:5.0" ] "withLangVersion50 is only supported on F#" cUnit

    let withLangVersionPreview (cUnit: CompilationUnit) : CompilationUnit =
        withOptionsHelper [ "--langversion:preview" ] "withLangVersionPreview is only supported on F#" cUnit

    /// Turns on checks that check integrity of XML doc comments
    let withXmlCommentChecking (cUnit: CompilationUnit) : CompilationUnit =
        withOptionsHelper [ "--warnon:3390" ] "withXmlCommentChecking is only supported for F#" cUnit

    /// Turns on checks that force the documentation of all parameters
    let withXmlCommentStrictParamChecking (cUnit: CompilationUnit) : CompilationUnit =
        withOptionsHelper [ "--warnon:3391" ] "withXmlCommentChecking is only supported for F#" cUnit

    let asLibrary (cUnit: CompilationUnit) : CompilationUnit =
        match cUnit with
        | FS fs -> FS { fs with OutputType = CompileOutput.Library }
        | _ -> failwith "TODO: Implement where applicable."

    let asExe (cUnit: CompilationUnit) : CompilationUnit =
        match cUnit with
        | FS fs -> FS { fs with OutputType = CompileOutput.Exe }
        | _ -> failwith "TODO: Implement where applicable."

    let ignoreWarnings (cUnit: CompilationUnit) : CompilationUnit =
        match cUnit with
        | FS fs -> FS { fs with IgnoreWarnings = true }
        | _ -> failwith "TODO: Implement ignorewarnings for the rest."

    let rec private asMetadataReference reference =
        match reference with
        | CompilationReference (cmpl, _) ->
            let result = compileFSharpCompilation cmpl false
            match result with
            | Failure f ->
                let message = sprintf "Operation failed (expected to succeed).\n All errors:\n%A" (f.Diagnostics)
                failwith message
            | Success s ->
                match s.OutputPath with
                    | None -> failwith "Operation didn't produce any output!"
                    | Some p -> p |> MetadataReference.CreateFromFile
        | _ -> failwith "Conversion isn't possible"

    let private processReferences (references: CompilationUnit list) =
        let rec loop acc = function
            | [] -> List.rev acc
            | x::xs ->
                match x with
                | FS fs ->
                    let refs = loop [] fs.References
                    let source = getSource fs.Source
                    let name = defaultArg fs.Name null
                    let cmpl = Compilation.Create(source, fs.SourceKind, fs.OutputType, cmplRefs = refs, name = name) |> CompilationReference.CreateFSharp
                    loop (cmpl::acc) xs
                | CS cs ->
                    let refs = loop [] cs.References
                    let source = getSource cs.Source
                    let name = defaultArg cs.Name null
                    let metadataReferences = List.map asMetadataReference refs
                    let cmpl = CompilationUtil.CreateCSharpCompilation(source, cs.LangVersion, cs.TargetFramework, additionalReferences = metadataReferences.ToImmutableArray().As<MetadataReference>(), name = name)
                            |> CompilationReference.Create
                    loop (cmpl::acc) xs
                | IL _ -> failwith "TODO: Process references for IL"
        loop [] references

    let private compileFSharpCompilation compilation ignoreWarnings : TestResult =

        let ((err: FSharpDiagnostic[], outputFilePath: string), deps) = CompilerAssert.CompileRaw(compilation, ignoreWarnings)

        let diagnostics = err |> fromFSharpDiagnostic

        let result =
            { OutputPath   = None
              Dependencies = deps
              Adjust       = 0
              Diagnostics  = diagnostics
              Output       = None }

        let (errors, warnings) = partitionErrors diagnostics

        // Treat warnings as errors if "IgnoreWarnings" is false
        if errors.Length > 0 || (warnings.Length > 0 && not ignoreWarnings) then
            Failure result
        else
            Success { result with OutputPath = Some outputFilePath }

    let private compileFSharp (fsSource: FSharpCompilationSource) : TestResult =

        let source = getSource fsSource.Source
        let sourceKind = fsSource.SourceKind
        let output = fsSource.OutputType
        let options = fsSource.Options |> Array.ofList

        let references = processReferences fsSource.References

        let compilation = Compilation.Create(source, sourceKind, output, options, references)

        compileFSharpCompilation compilation fsSource.IgnoreWarnings

    let private compileCSharpCompilation (compilation: CSharpCompilation) : TestResult =

        let outputPath = Path.Combine(Path.GetTempPath(), "FSharpCompilerTests", Path.GetRandomFileName())

        Directory.CreateDirectory(outputPath) |> ignore

        let filename = compilation.AssemblyName

        let output = Path.Combine(outputPath, Path.ChangeExtension(filename, ".dll"))

        let cmplResult = compilation.Emit (output)

        let result =
            { OutputPath   = None
              Dependencies = []
              Adjust       = 0
              Diagnostics  = []
              Output       = None }

        if cmplResult.Success then
            Success { result with OutputPath  = Some output }
        else
            Failure result

    let private compileCSharp (csSource: CSharpCompilationSource) : TestResult =

        let source = getSource csSource.Source
        let name = defaultArg csSource.Name (Guid.NewGuid().ToString ())

        let additionalReferences =
            match processReferences csSource.References with
            | [] -> ImmutableArray.Empty
            | r  -> (List.map asMetadataReference r).ToImmutableArray().As<MetadataReference>()

        let references = TargetFrameworkUtil.getReferences csSource.TargetFramework

        let lv =
          match csSource.LangVersion with
            | CSharpLanguageVersion.CSharp8 -> LanguageVersion.CSharp8
            | _ -> LanguageVersion.Default

        let cmpl =
          CSharpCompilation.Create(
            name,
            [ CSharpSyntaxTree.ParseText (source, CSharpParseOptions lv) ],
            references.As<MetadataReference>().AddRange additionalReferences,
            CSharpCompilationOptions (OutputKind.DynamicallyLinkedLibrary))

        cmpl |> compileCSharpCompilation

    let compile (cUnit: CompilationUnit) : TestResult =
        match cUnit with
        | FS fs -> compileFSharp fs
        | CS cs -> compileCSharp cs
        | _ -> failwith "TODO"

    let private parseFSharp (fsSource: FSharpCompilationSource) : TestResult =
        let source = getSource fsSource.Source
        let parseResults = CompilerAssert.Parse source
        let failed = parseResults.ParseHadErrors

        let diagnostics =  parseResults.Diagnostics |> fromFSharpDiagnostic

        let result =
            { OutputPath   = None
              Dependencies = []
              Adjust       = 0
              Diagnostics  = diagnostics
              Output       = None }

        if failed then
            Failure result
        else
            Success result

    let parse (cUnit: CompilationUnit) : TestResult =
        match cUnit with
        | FS fs -> parseFSharp fs
        | _ -> failwith "Parsing only supported for F#."

    let private typecheckFSharpSourceAndReturnErrors (fsSource: FSharpCompilationSource) : FSharpDiagnostic [] =
        let source = getSource fsSource.Source
        let options = fsSource.Options |> Array.ofList

        let name = match fsSource.Name with | None -> "test.fs" | Some n -> n

        let (err: FSharpDiagnostic []) = CompilerAssert.TypeCheckWithOptionsAndName options name source

        err

    let private typecheckFSharpSource (fsSource: FSharpCompilationSource) : TestResult =

        let (err: FSharpDiagnostic []) = typecheckFSharpSourceAndReturnErrors fsSource

        let diagnostics = err |> fromFSharpDiagnostic

        let result =
            { OutputPath   = None
              Dependencies = []
              Adjust       = 0
              Diagnostics  = diagnostics
              Output       = None }

        let (errors, warnings) = partitionErrors diagnostics

        // Treat warnings as errors if "IgnoreWarnings" is false;
        if errors.Length > 0 || (warnings.Length > 0 && not fsSource.IgnoreWarnings) then
            Failure result
        else
            Success result

    let private typecheckFSharp (fsSource: FSharpCompilationSource) : TestResult =
        match fsSource.Source with
        | _ -> typecheckFSharpSource fsSource

    let typecheck (cUnit: CompilationUnit) : TestResult =
        match cUnit with
        | FS fs -> typecheckFSharp fs
        | _ -> failwith "Typecheck only supports F#"

    let typecheckResults (cUnit: CompilationUnit) : FSharp.Compiler.CodeAnalysis.FSharpCheckFileResults =
        match cUnit with
        | FS fsSource ->
            let source = getSource fsSource.Source
            let options = fsSource.Options |> Array.ofList

            let name = match fsSource.Name with | None -> "test.fs" | Some n -> n

            CompilerAssert.TypeCheck(options, name, source)
        | _ -> failwith "Typecheck only supports F#"

    let run (result: TestResult) : TestResult =
        match result with
        | Failure f -> failwith (sprintf "Compilation should be successful in order to run.\n Errors: %A" (f.Diagnostics))
        | Success s ->
            match s.OutputPath with
            | None -> failwith "Compilation didn't produce any output. Unable to run. (Did you forget to set output type to Exe?)"
            | Some p ->
                let (exitCode, output, errors) = CompilerAssert.ExecuteAndReturnResult (p, s.Dependencies, false)
                let executionResult = { s with Output = Some (ExecutionOutput { ExitCode = exitCode; StdOut = output; StdErr = errors }) }
                if exitCode = 0 then
                    Success executionResult
                else
                    Failure executionResult

    let compileAndRun = compile >> run

    let compileExeAndRun = asExe >> compileAndRun

    let private evalFSharp (fs: FSharpCompilationSource) : TestResult =
        let source = getSource fs.Source
        let options = fs.Options |> Array.ofList

        use script = new FSharpScript(additionalArgs=options)

        let ((evalresult: Result<FsiValue option, exn>), (err: FSharpDiagnostic[])) = script.Eval(source)

        let diagnostics = err |> fromFSharpDiagnostic

        let result =
            { OutputPath   = None
              Dependencies = []
              Adjust       = 0
              Diagnostics  = diagnostics
              Output       = Some(EvalOutput evalresult) }

        let (errors, warnings) = partitionErrors diagnostics

        let evalError = match evalresult with Ok _ -> false | _ -> true

        if evalError || errors.Length > 0 || (warnings.Length > 0 && not fs.IgnoreWarnings) then
            Failure result
        else
            Success result

    let eval (cUnit: CompilationUnit) : TestResult =
        match cUnit with
        | FS fs -> evalFSharp fs
        | _ -> failwith "Script evaluation is only supported for F#."

    let runFsi (cUnit: CompilationUnit) : TestResult =
        match cUnit with
        | FS fs ->
            let source = getSource fs.Source

            let options = fs.Options |> Array.ofList

            let errors = CompilerAssert.RunScriptWithOptionsAndReturnResult options source

            let result =
                { OutputPath   = None
                  Dependencies = []
                  Adjust       = 0
                  Diagnostics  = []
                  Output       = None }

            if errors.Count > 0 then
                let output = ExecutionOutput {
                    ExitCode = -1
                    StdOut   = String.Empty
                    StdErr   = ((errors |> String.concat "\n").Replace("\r\n","\n")) }
                Failure { result with Output = Some output }
            else
                Success result
        | _ -> failwith "FSI running only supports F#."


    let private createBaselineErrors (baselineFile: BaselineFile) (actualErrors: string) : unit =
        FileSystem.OpenFileForWriteShim(baselineFile.FilePath + ".err").Write(actualErrors)

    let private verifyFSBaseline (fs) : unit =
        match fs.Baseline with
        | None -> failwith "Baseline was not provided."
        | Some bsl ->
            let errorsExpectedBaseLine =
                match bsl.OutputBaseline.Content with
                | Some b -> b.Replace("\r\n","\n")
                | None ->  String.Empty

            let typecheckDiagnostics = fs |> typecheckFSharpSourceAndReturnErrors

            let errorsActual = (typecheckDiagnostics |> Array.map (sprintf "%A") |> String.concat "\n").Replace("\r\n","\n")

            if errorsExpectedBaseLine <> errorsActual then
                createBaselineErrors bsl.OutputBaseline errorsActual

            Assert.AreEqual(errorsExpectedBaseLine, errorsActual)

    /// Check the typechecker output against the baseline, if invoked with empty baseline, will expect no error/warnings output.
    let verifyBaseline (cUnit: CompilationUnit) : CompilationUnit =
        match cUnit with
        | FS fs -> (verifyFSBaseline fs) |> ignore
        | _ -> failwith "Baseline tests are only supported for F#."

        cUnit

    let verifyIL (il: string list) (result: TestResult) : unit =
        match result with
        | Success s ->
            match s.OutputPath with
            | None -> failwith "Operation didn't produce any output!"
            | Some p -> ILChecker.checkIL p il
        | Failure _ -> failwith "Result should be \"Success\" in order to get IL."

    let private verifyFSILBaseline (baseline: Baseline option) (result: Output) : unit =
        match baseline with
        | None -> failwith "Baseline was not provided."
        | Some bsl ->
            match result.OutputPath with
                | None -> failwith "Operation didn't produce any output!"
                | Some p ->
                    let expectedIL =
                        match bsl.ILBaseline.Content with
                        | Some b -> b.Replace("\r\n","\n")
                        | None ->  String.Empty
                    let (success, errorMsg, actualIL) = ILChecker.verifyILAndReturnActual p expectedIL

                    if not success then
                        createBaselineErrors bsl.ILBaseline actualIL
                        Assert.Fail(errorMsg)

    let verifyILBaseline (cUnit: CompilationUnit) : CompilationUnit =
        match cUnit with
        | FS fs ->
            match fs |> compileFSharp |> shouldSucceed with
            | Failure _ -> failwith "Result should be \"Success\" in order to get IL."
            | Success s -> verifyFSILBaseline fs.Baseline s
        | _ -> failwith "Baseline tests are only supported for F#."

        cUnit

    let verifyBaselines = verifyBaseline >> verifyILBaseline

    [<AutoOpen>]
    module Assertions =
        let private getErrorNumber (error: ErrorType) : int =
            match error with
            | Error e | Warning e -> e

        let private getErrorInfo (info: ErrorInfo) : string =
            sprintf "%A %A" info.Error info.Message

        let inline private assertErrorsLength (source: ErrorInfo list) (expected: 'a list) : unit =
            if (List.length source) <> (List.length expected) then
                failwith (sprintf "Expected list of issues differ from compilation result:\nExpected:\n %A\nActual:\n %A" expected (List.map getErrorInfo source))
            ()

        let private assertErrorMessages (source: ErrorInfo list) (expected: string list) : unit =
            for exp in expected do
                if not (List.exists (fun (el: ErrorInfo) -> el.Message = exp) source) then
                    failwith (sprintf "Mismatch in error message, expected '%A' was not found during compilation.\nAll errors:\n%A" exp (List.map getErrorInfo source))
            assertErrorsLength source expected

        let private assertErrorNumbers (source: ErrorInfo list) (expected: int list) : unit =
            for exp in expected do
                if not (List.exists (fun (el: ErrorInfo) -> (getErrorNumber el.Error) = exp) source) then
                    failwith (sprintf "Mismatch in ErrorNumber, expected '%A' was not found during compilation.\nAll errors:\n%A" exp (List.map getErrorInfo source))

        let private assertErrors (what: string) libAdjust (source: ErrorInfo list) (expected: ErrorInfo list) : unit =
            let errors = source |> List.map (fun error -> { error with Range = adjustRange error.Range libAdjust })
            
            let inline checkEqual k a b =
             if a <> b then
                 Assert.AreEqual(a, b, sprintf "%s: Mismatch in %s, expected '%A', got '%A'.\nAll errors:\n%A\nExpected errors:\n%A" what k a b errors expected)
            // For lists longer than 100 errors:
            errors |> List.iter System.Diagnostics.Debug.WriteLine

            // TODO: Check all "categories", collect all results and print alltogether.
            checkEqual "Errors count" expected.Length errors.Length

            (errors, expected)
            ||> List.iter2 (fun actualError expectedError ->
                           let { Error = actualError; Range = actualRange; Message = actualMessage } = actualError
                           let { Error = expectedError; Range = expectedRange; Message = expectedMessage } = expectedError
                           checkEqual "Error" expectedError actualError
                           checkEqual "ErrorRange" expectedRange actualRange
                           checkEqual "Message" expectedMessage actualMessage)
            ()

        let adjust (adjust: int) (result: TestResult) : TestResult =
            match result with
            | Success s -> Success { s with Adjust = adjust }
            | Failure f -> Failure { f with Adjust = adjust }

        let shouldSucceed (result: TestResult) : TestResult =
            match result with
            | Success _ -> result
            | Failure r ->
                let message = sprintf "Operation failed (expected to succeed).\n All errors:\n%A" (r.Diagnostics)
                failwith message

        let shouldFail (result: TestResult) : TestResult =
            match result with
            | Success _ -> failwith "Operation was succeeded (expected to fail)."
            | Failure _ -> result

        let private assertResultsCategory (what: string) (selector: Output -> ErrorInfo list) (expected: ErrorInfo list) (result: TestResult) : TestResult =
            match result with
            | Success r | Failure r ->
                assertErrors what r.Adjust (selector r) expected
            result

        let withResults (expectedResults: ErrorInfo list) result : TestResult =
            assertResultsCategory "Results" (fun r -> r.Diagnostics) expectedResults result

        let withResult (expectedResult: ErrorInfo ) (result: TestResult) : TestResult =
            withResults [expectedResult] result

        let withDiagnostics (expected: (ErrorType * Line * Col * Line * Col * string) list) (result: TestResult) : TestResult =
            let (expectedResults: ErrorInfo list) =
                expected |>
                List.map(
                    fun e ->
                      let (error, (Line startLine), (Col startCol), (Line endLine), (Col endCol), message) = e
                      { Error = error
                        Range =
                            { StartLine   = startLine
                              StartColumn = startCol
                              EndLine     = endLine
                              EndColumn   = endCol }
                        Message     = message })
            withResults expectedResults result

        let withSingleDiagnostic (expected: (ErrorType * Line * Col * Line * Col * string)) (result: TestResult) : TestResult =
            withDiagnostics [expected] result

        let withErrors (expectedErrors: ErrorInfo list) (result: TestResult) : TestResult =
            assertResultsCategory "Errors" (fun r -> getErrors r.Diagnostics) expectedErrors result

        let withError (expectedError: ErrorInfo) (result: TestResult) : TestResult =
            withErrors [expectedError] result

        let checkCodes (expected: int list) (selector: Output -> ErrorInfo list) (result: TestResult) : TestResult =
            match result with
            | Success r | Failure r ->
                assertErrorNumbers (selector r) expected
            result

        let withErrorCodes (expectedCodes: int list) (result: TestResult) : TestResult =
            checkCodes expectedCodes (fun r -> getErrors r.Diagnostics) result

        let withErrorCode (expectedCode: int) (result: TestResult) : TestResult =
            withErrorCodes [expectedCode] result

        let withWarnings (expectedWarnings: ErrorInfo list) (result: TestResult) : TestResult =
            assertResultsCategory "Warnings" (fun r -> getWarnings r.Diagnostics) expectedWarnings result

        let withWarning (expectedWarning: ErrorInfo) (result: TestResult) : TestResult =
            withWarnings [expectedWarning] result

        let withWarningCodes (expectedCodes: int list) (result: TestResult) : TestResult =
            checkCodes expectedCodes (fun r -> getWarnings r.Diagnostics) result

        let withWarningCode (expectedCode: int) (result: TestResult) : TestResult =
            withWarningCodes [expectedCode] result

        let private checkErrorMessages (messages: string list) (selector: Output -> ErrorInfo list) (result: TestResult) : TestResult =
            match result with
            | Success r | Failure r -> assertErrorMessages (selector r) messages
            result

        let private diagnosticMatches (pattern: string) (diagnostics: ErrorInfo list) : bool =
            diagnostics |> List.exists (fun d -> Regex.IsMatch(d.Message, pattern))

        let withDiagnosticMessageMatches (pattern: string) (result: TestResult) : TestResult =
            match result with
            | Success r | Failure r ->
                if not <| diagnosticMatches pattern r.Diagnostics then
                    failwith "Expected diagnostic message pattern was not found in compilation diagnostics."
            result

        let withDiagnosticMessageDoesntMatch (pattern: string) (result: TestResult) : TestResult =
            match result with
            | Success r | Failure r ->
                if diagnosticMatches pattern r.Diagnostics then
                    failwith "Diagnostic message pattern was not expected, but was present."
            result

        let withMessages (messages: string list) (result: TestResult) : TestResult =
            checkErrorMessages messages (fun r -> r.Diagnostics) result

        let withMessage (message: string) (result: TestResult) : TestResult =
            withMessages [message] result

        let withErrorMessages (messages: string list) (result: TestResult) : TestResult =
            checkErrorMessages messages (fun r -> getErrors r.Diagnostics) result

        let withErrorMessage (message: string) (result: TestResult) : TestResult =
            withErrorMessages [message] result

        let withWarningMessages (messages: string list) (result: TestResult) : TestResult =
            checkErrorMessages messages (fun r -> getWarnings r.Diagnostics) result

        let withWarningMessage (message: string) (result: TestResult) : TestResult =
            withWarningMessages [message] result

        let withExitCode (expectedExitCode: int) (result: TestResult) : TestResult =
            match result with
            | Success r | Failure r ->
                match r.Output with
                | None -> failwith "Execution output is missing, cannot check exit code."
                | Some o ->
                    match o with
                    | ExecutionOutput e -> Assert.AreEqual(e.ExitCode, expectedExitCode, sprintf "Exit code was expected to be: %A, but got %A." expectedExitCode e.ExitCode)
                    | _ -> failwith "Cannot check exit code on this run result."
            result

        let private checkOutput (category: string) (substring: string) (selector: ExecutionOutput -> string) (result: TestResult) : TestResult =
            match result with
            | Success r | Failure r ->
                match r.Output with
                | None -> failwith (sprintf "Execution output is missing cannot check \"%A\"" category)
                | Some o ->
                    match o with
                    | ExecutionOutput e ->
                        let where = selector e
                        if not (where.Contains(substring)) then
                            failwith (sprintf "\nThe following substring:\n    %A\nwas not found in the %A\nOutput:\n    %A" substring category where)
                    | _ -> failwith "Cannot check output on this run result."
            result

        let withOutputContains (substring: string) (result: TestResult) : TestResult =
            checkOutput "STDERR/STDOUT" substring (fun o -> o.StdOut + "\n" + o.StdErr) result

        let withStdOutContains (substring: string) (result: TestResult) : TestResult =
            checkOutput "STDOUT" substring (fun o -> o.StdOut) result

        let withStdErrContains (substring: string) (result: TestResult) : TestResult =
            checkOutput "STDERR" substring (fun o -> o.StdErr) result

        // TODO: probably needs a bit of simplification, + need to remove that pyramid of doom.
        let private assertEvalOutput (selector: FsiValue -> 'T) (value: 'T) (result: TestResult) : TestResult =
            match result with
            | Success r | Failure r ->
                match r.Output with
                | None -> failwith "Execution output is missing cannot check value."
                | Some o ->
                    match o with
                    | EvalOutput e ->
                        match e with
                        | Ok v ->
                            match v with
                            | None -> failwith "Cannot assert value of evaluation, since it is None."
                            | Some e -> Assert.AreEqual(value, (selector e))
                        | Result.Error ex -> raise ex
                    | _ -> failwith "Only 'eval' output is supported."
            result

        // TODO: Need to support for:
        // STDIN, to test completions
        // Contains
        // Cancellation
        let withEvalValueEquals (value: 'T) (result: TestResult) : TestResult =
            assertEvalOutput (fun (x: FsiValue) -> x.ReflectionValue :?> 'T) value result

        let withEvalTypeEquals t (result: TestResult) : TestResult =
            assertEvalOutput (fun (x: FsiValue) -> x.ReflectionType) t result
