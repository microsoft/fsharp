// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module FSharp.Compiler.Interactive.Shell

// Prevents warnings of experimental APIs - we are using FSharpLexer
#nowarn "57"

#nowarn "55"

[<assembly: System.Runtime.InteropServices.ComVisible(false)>]
[<assembly: System.CLSCompliant(true)>]
do()

open System
open System.Collections.Generic
open System.Diagnostics
open System.Globalization
open System.IO
open System.Text
open System.Threading
open System.Reflection
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Internal.Utilities
open Internal.Utilities.Collections
open Internal.Utilities.FSharpEnvironment
open Internal.Utilities.Library
open Internal.Utilities.Library.Extras
open FSharp.Compiler
open FSharp.Compiler.AbstractIL
open FSharp.Compiler.AbstractIL.Diagnostics
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AbstractIL.ILBinaryReader
open FSharp.Compiler.AbstractIL.ILRuntimeWriter
open FSharp.Compiler.AccessibilityLogic
open FSharp.Compiler.CheckDeclarations
open FSharp.Compiler.CheckExpressions
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.CompilerOptions
open FSharp.Compiler.CompilerConfig
open FSharp.Compiler.CompilerDiagnostics
open FSharp.Compiler.CompilerImports
open FSharp.Compiler.CompilerGlobalState
open FSharp.Compiler.DependencyManager
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.EditorServices
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Features
open FSharp.Compiler.IlxGen
open FSharp.Compiler.InfoReader
open FSharp.Compiler.IO
open FSharp.Compiler.Lexhelp
open FSharp.Compiler.NameResolution
open FSharp.Compiler.ParseAndCheckInputs
open FSharp.Compiler.OptimizeInputs
open FSharp.Compiler.ScriptClosure
open FSharp.Compiler.Symbols
open FSharp.Compiler.Syntax
open FSharp.Compiler.Syntax.PrettyNaming
open FSharp.Compiler.SyntaxTreeOps
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Layout
open FSharp.Compiler.Xml
open FSharp.Compiler.Tokenization
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.BuildGraph

//----------------------------------------------------------------------------
// For the FSI as a service methods...
//----------------------------------------------------------------------------

type FsiValue(reflectionValue:obj, reflectionType:Type, fsharpType:FSharpType) =
  member x.ReflectionValue = reflectionValue
  member x.ReflectionType = reflectionType
  member x.FSharpType = fsharpType

[<Sealed>]
type FsiBoundValue(name: string, value: FsiValue) =
    member _.Name = name
    member _.Value = value

[<AutoOpen>]
module internal Utilities =
    type IAnyToLayoutCall =
        abstract AnyToLayout : FormatOptions * obj * Type -> Layout
        abstract FsiAnyToLayout : FormatOptions * obj * Type -> Layout

    type private AnyToLayoutSpecialization<'T>() =
        interface IAnyToLayoutCall with
            member _.AnyToLayout(options, o : obj, ty : Type) = Display.any_to_layout options ((Unchecked.unbox o : 'T), ty)
            member _.FsiAnyToLayout(options, o : obj, ty : Type) = Display.fsi_any_to_layout options ((Unchecked.unbox o : 'T), ty)

    let getAnyToLayoutCall ty =
        let specialized = typedefof<AnyToLayoutSpecialization<_>>.MakeGenericType [| ty |]
        Activator.CreateInstance(specialized) :?> IAnyToLayoutCall

    let callStaticMethod (ty:Type) name args =
        ty.InvokeMember(name, (BindingFlags.InvokeMethod ||| BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic), null, null, Array.ofList args,Globalization.CultureInfo.InvariantCulture)

    let ignoreAllErrors f = try f() with _ -> ()

    // TODO: this dotnet/core polyfill can be removed when it surfaces in Type
    let getMember (name: string) (memberType: MemberTypes) (attr: BindingFlags) (declaringType: Type) =
        let memberType =
            if memberType &&& MemberTypes.NestedType = MemberTypes.NestedType then
                memberType ||| MemberTypes.TypeInfo
            else
                memberType
        declaringType.GetMembers(attr) |> Array.filter(fun m -> 0 <> (int(m.MemberType &&& memberType)) && m.Name = name)

    let rec tryFindMember (name: string) (memberType: MemberTypes) (declaringType: Type) =
        let bindingFlags = BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic
        match declaringType |> getMember name memberType bindingFlags with
        | [||] -> declaringType.GetInterfaces() |> Array.tryPick (tryFindMember name memberType)
        | [|m|] -> Some m
        | _ -> raise <| new AmbiguousMatchException(sprintf "Ambiguous match for member '%s'" name)

    let getInstanceProperty (obj:obj) (nm:string) =
        let p = (tryFindMember nm MemberTypes.Property <| obj.GetType()).Value :?> PropertyInfo
        p.GetValue(obj, [||]) |> unbox

    let setInstanceProperty (obj:obj) (nm:string) (v:obj) =
        let p = (tryFindMember nm MemberTypes.Property <| obj.GetType()).Value :?> PropertyInfo
        p.SetValue(obj, v, [||]) |> unbox

    let callInstanceMethod0 (obj:obj) (typeArgs : Type []) (nm:string) =
        let m = (tryFindMember nm MemberTypes.Method <| obj.GetType()).Value :?> MethodInfo
        let m = match typeArgs with [||] -> m | _ -> m.MakeGenericMethod(typeArgs)
        m.Invoke(obj, [||]) |> unbox

    let callInstanceMethod1 (obj:obj) (typeArgs : Type []) (nm:string) (v:obj) =
        let m = (tryFindMember nm MemberTypes.Method <| obj.GetType()).Value :?> MethodInfo
        let m = match typeArgs with [||] -> m | _ -> m.MakeGenericMethod(typeArgs)
        m.Invoke(obj, [|v|]) |> unbox

    let callInstanceMethod3 (obj:obj) (typeArgs : Type []) (nm:string) (v1:obj)  (v2:obj)  (v3:obj) =
        let m = (tryFindMember nm MemberTypes.Method <| obj.GetType()).Value :?> MethodInfo
        let m = match typeArgs with [||] -> m | _ -> m.MakeGenericMethod(typeArgs)
        m.Invoke(obj, [|v1;v2;v3|]) |> unbox

    let colorPrintL (outWriter : TextWriter) opts layout =
        let renderer =
            { new LayoutRenderer<NoResult,NoState> with
                member r.Start () = NoState

                member r.AddText z s =
                    let color =
                        match s.Tag with
                        | TextTag.Keyword -> ConsoleColor.White
                        | TextTag.TypeParameter
                        | TextTag.Alias
                        | TextTag.Class
                        | TextTag.Module
                        | TextTag.Interface
                        | TextTag.Record
                        | TextTag.Struct
                        | TextTag.Union
                        | TextTag.UnknownType -> ConsoleColor.Cyan
                        | TextTag.UnionCase
                        | TextTag.ActivePatternCase -> ConsoleColor.Magenta
                        | TextTag.StringLiteral -> ConsoleColor.Yellow
                        | TextTag.NumericLiteral -> ConsoleColor.Green
                        | _ -> Console.ForegroundColor

                    DoWithColor color (fun () -> outWriter.Write s.Text)

                    z

                member r.AddBreak z n =
                    outWriter.WriteLine()
                    outWriter.Write (String.replicate n " ")
                    z

                member r.AddTag z (tag,attrs,start) = z

                member r.Finish z =
                    outWriter.WriteLine()
                    NoResult
            }

        layout
        |> Display.squash_layout opts
        |> LayoutRender.renderL renderer
        |> ignore

        outWriter.WriteLine()

    let reportError m =
        let report errorType err msg =
            let error = err, msg
            match errorType with
            | ErrorReportType.Warning -> warning(Error(error, m))
            | ErrorReportType.Error -> errorR(Error(error, m))
        ResolvingErrorReport (report)

    let getOutputDir (tcConfigB: TcConfigBuilder) =  tcConfigB.outputDir |> Option.defaultValue ""

//----------------------------------------------------------------------------
// Timing support
//----------------------------------------------------------------------------

[<AutoSerializable(false)>]
type internal FsiTimeReporter(outWriter: TextWriter) =
    let stopwatch = new System.Diagnostics.Stopwatch()
    let ptime = System.Diagnostics.Process.GetCurrentProcess()
    let numGC = System.GC.MaxGeneration
    member tr.TimeOp(f) =
        let startTotal = ptime.TotalProcessorTime
        let startGC = [| for i in 0 .. numGC -> System.GC.CollectionCount(i) |]
        stopwatch.Reset()
        stopwatch.Start()
        let res = f ()
        stopwatch.Stop()
        let total = ptime.TotalProcessorTime - startTotal
        let spanGC = [ for i in 0 .. numGC-> System.GC.CollectionCount(i) - startGC.[i] ]
        let elapsed = stopwatch.Elapsed
        fprintfn outWriter "%s" (FSIstrings.SR.fsiTimeInfoMainString((sprintf "%02d:%02d:%02d.%03d" (int elapsed.TotalHours) elapsed.Minutes elapsed.Seconds elapsed.Milliseconds),(sprintf "%02d:%02d:%02d.%03d" (int total.TotalHours) total.Minutes total.Seconds total.Milliseconds),(String.concat ", " (List.mapi (sprintf "%s%d: %d" (FSIstrings.SR.fsiTimeInfoGCGenerationLabelSomeShorthandForTheWordGeneration())) spanGC))))
        res

    member tr.TimeOpIf flag f = if flag then tr.TimeOp f else f ()


type internal FsiValuePrinterMode =
    | PrintExpr
    | PrintDecl

type EvaluationEventArgs(fsivalue : FsiValue option, symbolUse : FSharpSymbolUse, decl: FSharpImplementationFileDeclaration) =
    inherit EventArgs()
    member x.Name = symbolUse.Symbol.DisplayName
    member x.FsiValue = fsivalue
    member x.SymbolUse = symbolUse
    member x.Symbol = symbolUse.Symbol
    member x.ImplementationDeclaration = decl

[<AbstractClass>]
/// User-configurable information that changes how F# Interactive operates, stored in the 'fsi' object
/// and accessible via the programming model
type FsiEvaluationSessionHostConfig () =
    let evaluationEvent = new Event<EvaluationEventArgs> ()
    /// Called by the evaluation session to ask the host for parameters to format text for output
    abstract FormatProvider: System.IFormatProvider
    /// Called by the evaluation session to ask the host for parameters to format text for output
    abstract FloatingPointFormat: string
    /// Called by the evaluation session to ask the host for parameters to format text for output
    abstract AddedPrinters : Choice<(System.Type * (obj -> string)), (System.Type * (obj -> obj))>  list
    /// Called by the evaluation session to ask the host for parameters to format text for output
    abstract ShowDeclarationValues: bool
    /// Called by the evaluation session to ask the host for parameters to format text for output
    abstract ShowIEnumerable: bool
    /// Called by the evaluation session to ask the host for parameters to format text for output
    abstract ShowProperties : bool
    /// Called by the evaluation session to ask the host for parameters to format text for output
    abstract PrintSize : int
    /// Called by the evaluation session to ask the host for parameters to format text for output
    abstract PrintDepth : int
    /// Called by the evaluation session to ask the host for parameters to format text for output
    abstract PrintWidth : int
    /// Called by the evaluation session to ask the host for parameters to format text for output
    abstract PrintLength : int

    /// The evaluation session calls this to report the preferred view of the command line arguments after
    /// stripping things like "/use:file.fsx", "-r:Foo.dll" etc.
    abstract ReportUserCommandLineArgs : string [] -> unit


    /// The evaluation session calls this to ask the host for the special console reader.
    /// Returning 'Some' indicates a console is to be used, so some special rules apply.
    ///
    /// A "console" gets used if
    ///     --readline- is specified (the default on Windows + .NET); and
    ///     not --fsi-server (which should always be combined with --readline-); and
    ///     GetOptionalConsoleReadLine() returns a Some
    ///
    /// "Peekahead" occurs if --peekahead- is not specified (i.e. it is the default):
    ///     - If a console is being used then
    ///         - a prompt is printed early
    ///         - a background thread is created
    ///         - the GetOptionalConsoleReadLine() callback is used to read the first line
    ///     - Otherwise call inReader.Peek()
    ///
    /// Further lines are read as follows:
    ///     - If a console is being used then use GetOptionalConsoleReadLine()
    ///     - Otherwise use inReader.ReadLine()

    abstract GetOptionalConsoleReadLine : probeToSeeIfConsoleWorks: bool -> (unit -> string) option

    /// The evaluation session calls this at an appropriate point in the startup phase if the --fsi-server parameter was given
    abstract StartServer : fsiServerName:string -> unit

    /// Called by the evaluation session to ask the host to enter a dispatch loop like Application.Run().
    /// Only called if --gui option is used (which is the default).
    /// Gets called towards the end of startup and every time a ThreadAbort escaped to the backup driver loop.
    /// Return true if a 'restart' is required, which is a bit meaningless.
    abstract EventLoopRun : unit -> bool

    /// Request that the given operation be run synchronously on the event loop.
    abstract EventLoopInvoke : codeToRun: (unit -> 'T) -> 'T

    /// Schedule a restart for the event loop.
    abstract EventLoopScheduleRestart : unit -> unit

    /// Implicitly reference FSharp.Compiler.Interactive.Settings.dll
    abstract UseFsiAuxLib : bool

    /// Hook for listening for evaluation bindings
    member x.OnEvaluation = evaluationEvent.Publish
    member internal x.TriggerEvaluation (value, symbolUse, decl) =
        evaluationEvent.Trigger (EvaluationEventArgs (value, symbolUse, decl) )

/// Used to print value signatures along with their values, according to the current
/// set of pretty printers installed in the system, and default printing rules.
type internal FsiValuePrinter(fsi: FsiEvaluationSessionHostConfig, tcConfigB: TcConfigBuilder, g: TcGlobals, generateDebugInfo, resolveAssemblyRef, outWriter: TextWriter) =

    /// This printer is used by F# Interactive if no other printers apply.
    let DefaultPrintingIntercept (ienv: IEnvironment) (obj:obj) =
       match obj with
       | null -> None
       | :? System.Collections.IDictionary as ie ->
          let it = ie.GetEnumerator()
          try
              let itemLs =
                  Layout.unfoldL // the function to layout each object in the unfold
                          (fun obj -> ienv.GetLayout obj)
                          // the function to call at each step of the unfold
                          (fun () ->
                              if it.MoveNext() then
                                 Some((it.Key, it.Value),())
                              else None) ()
                          // the maximum length
                          (1+fsi.PrintLength/3)
              let makeListL itemLs =
                (leftL (TaggedText.tagText "[")) ^^
                sepListL (rightL (TaggedText.tagText ";")) itemLs ^^
                (rightL (TaggedText.tagText "]"))
              Some(wordL (TaggedText.tagText "dict") --- makeListL itemLs)
          finally
             match it with
             | :? System.IDisposable as d -> d.Dispose()
             | _ -> ()

       | _ -> None


    /// Get the print options used when formatting output using the structured printer.
    member _.GetFsiPrintOptions() =
        { FormatOptions.Default with
              FormatProvider = fsi.FormatProvider;
              PrintIntercepts =
                  // The fsi object supports the addition of two kinds of printers, one which converts to a string
                  // and one which converts to another object that is recursively formatted.
                  // The internal AddedPrinters reports these to FSI.EXE and we pick them up here to produce a layout
                  [ for x in fsi.AddedPrinters do
                         match x with
                         | Choice1Of2 (aty: System.Type, printer) ->
                                yield (fun _ienv (obj:obj) ->
                                   match obj with
                                   | null -> None
                                   | _ when aty.IsAssignableFrom(obj.GetType())  ->
                                       match printer obj with
                                       | null -> None
                                       | s -> Some (wordL (TaggedText.tagText s))
                                   | _ -> None)

                         | Choice2Of2 (aty: System.Type, converter) ->
                                yield (fun ienv (obj:obj) ->
                                   match obj with
                                   | null -> None
                                   | _ when aty.IsAssignableFrom(obj.GetType())  ->
                                       match converter obj with
                                       | null -> None
                                       | res -> Some (ienv.GetLayout res)
                                   | _ -> None)
                    yield DefaultPrintingIntercept];
              FloatingPointFormat = fsi.FloatingPointFormat;
              PrintWidth = fsi.PrintWidth;
              PrintDepth = fsi.PrintDepth;
              PrintLength = fsi.PrintLength;
              PrintSize = fsi.PrintSize;
              ShowProperties = fsi.ShowProperties;
              ShowIEnumerable = fsi.ShowIEnumerable; }

    /// Get the evaluation context used when inverting the storage mapping of the ILRuntimeWriter.
    member _.GetEvaluationContext emEnv =
        let cenv = { ilg = g.ilg ; emitTailcalls= tcConfigB.emitTailcalls; generatePdb = generateDebugInfo; resolveAssemblyRef=resolveAssemblyRef; tryFindSysILTypeRef=g.TryFindSysILTypeRef }
        { LookupFieldRef = ILRuntimeWriter.LookupFieldRef emEnv >> Option.get
          LookupMethodRef = ILRuntimeWriter.LookupMethodRef emEnv >> Option.get
          LookupTypeRef = ILRuntimeWriter.LookupTypeRef cenv emEnv
          LookupType = ILRuntimeWriter.LookupType cenv emEnv }

    /// Generate a layout for an actual F# value, where we know the value has the given static type.
    member _.PrintValue (printMode, opts:FormatOptions, x:obj, ty:System.Type) =
        // We do a dynamic invoke of any_to_layout with the right System.Type parameter for the static type of the saved value.
        // In principle this helps any_to_layout do the right thing as it descends through terms. In practice it means
        // it at least does the right thing for top level 'null' list and option values (but not for nested ones).
        //
        // The static type was saved into the location used by RuntimeHelpers.GetSavedItType when RuntimeHelpers.SaveIt was called.
        // RuntimeHelpers.SaveIt has type ('a -> unit), and fetches the System.Type for 'a by using a typeof<'a> call.
        // The funny thing here is that you might think that the driver (this file) knows more about the static types
        // than the compiled code does. But it doesn't! In particular, it's not that easy to get a System.Type value based on the
        // static type information we do have: we have no direct way to bind a F# TAST type or even an AbstractIL type to
        // a System.Type value (I guess that functionality should be in ilreflect.fs).
        //
        // This will be more significant when we print values other then 'it'
        //
        try
            let anyToLayoutCall = Utilities.getAnyToLayoutCall ty
            match printMode with
              | PrintDecl ->
                  // When printing rhs of fsi declarations, use "fsi_any_to_layout".
                  // This will suppress some less informative values, by returning an empty layout. [fix 4343].
                  anyToLayoutCall.FsiAnyToLayout(opts, x, ty)
              | PrintExpr ->
                  anyToLayoutCall.AnyToLayout(opts, x, ty)
        with
        | :? ThreadAbortException -> Layout.wordL (TaggedText.tagText "")
        | e ->
#if DEBUG
          printf "\n\nPrintValue: x = %+A and ty=%s\n" x (ty.FullName)
#endif
          printf "%s" (FSIstrings.SR.fsiExceptionDuringPrettyPrinting(e.ToString()));
          Layout.wordL (TaggedText.tagText "")

    /// Display the signature of an F# value declaration, along with its actual value.
    member valuePrinter.InvokeDeclLayout (emEnv, ilxGenerator: IlxAssemblyGenerator, v:Val) =
        // Implemented via a lookup from v to a concrete (System.Object,System.Type).
        // This (obj,objTy) pair can then be fed to the fsi value printer.
        // Note: The value may be (null:Object).
        // Note: A System.Type allows the value printer guide printing of nulls, e.g. as None or [].
        //-------
        // IlxGen knows what the v:Val was converted to w.r.t. AbsIL data structures.
        // Ilreflect knows what the AbsIL was generated to.
        // Combining these allows for obtaining the (obj,objTy) by reflection where possible.
        // This assumes the v:Val was given appropriate storage, e.g. StaticField.
        if fsi.ShowDeclarationValues then
            // Adjust "opts" for printing for "declared-values":
            // - No sequences, because they may have effects or time cost.
            // - No properties, since they may have unexpected effects.
            // - Limit strings to roughly one line, since huge strings (e.g. 1 million chars without \n are slow in vfsi).
            // - Limit PrintSize which is a count on nodes.
            let declaredValueReductionFactor = 10 (* reduce PrintSize for declared values, e.g. see less of large terms *)
            let opts   = valuePrinter.GetFsiPrintOptions()
            let opts   = {opts with ShowProperties  = false // properties off, motivated by Form props
                                    ShowIEnumerable = false // seq off, motivated by db query concerns
                                    StringLimit = max 0 (opts.PrintWidth-4) // 4 allows for an indent of 2 and 2 quotes (rough)
                                    PrintSize = opts.PrintSize / declaredValueReductionFactor } // print less
            let res    =
                try  ilxGenerator.LookupGeneratedValue (valuePrinter.GetEvaluationContext emEnv, v)
                with e ->
                    assert false
#if DEBUG
                    //fprintfn fsiConsoleOutput.Out "lookGenerateVal: failed on v=%+A v.Name=%s" v v.LogicalName
#endif
                    None // lookup may fail
            match res with
              | None             -> None
              | Some (obj,objTy) ->
                  let lay = valuePrinter.PrintValue (FsiValuePrinterMode.PrintDecl, opts, obj, objTy)
                  if isEmptyL lay then None else Some lay // suppress empty layout

        else
            None


    /// Format a value
    member valuePrinter.FormatValue (obj:obj, objTy) =
        let opts        = valuePrinter.GetFsiPrintOptions()
        let lay = valuePrinter.PrintValue (FsiValuePrinterMode.PrintExpr, opts, obj, objTy)
        Display.layout_to_string opts lay

    /// Fetch the saved value of an expression out of the 'it' register and show it.
    member valuePrinter.InvokeExprPrinter (denv, infoReader, emEnv, ilxGenerator: IlxAssemblyGenerator, vref: ValRef) =
        let opts        = valuePrinter.GetFsiPrintOptions()
        let res    = ilxGenerator.LookupGeneratedValue (valuePrinter.GetEvaluationContext emEnv, vref.Deref)
        let rhsL =
            match res with
                | None             -> None
                | Some (obj,objTy) ->
                    let lay = valuePrinter.PrintValue (FsiValuePrinterMode.PrintExpr, opts, obj, objTy)
                    if isEmptyL lay then None else Some lay // suppress empty layout
        let denv = { denv with suppressMutableKeyword = true } // suppress 'mutable' in 'val mutable it = ...'
        let denv = { denv with suppressInlineKeyword = false } // dont' suppress 'inline' in 'val inline f = ...'
        let fullL =
            if Option.isNone rhsL || isEmptyL rhsL.Value then
                NicePrint.prettyLayoutOfValOrMemberNoInst denv infoReader vref (* the rhs was suppressed by the printer, so no value to print *)
            else
                (NicePrint.prettyLayoutOfValOrMemberNoInst denv infoReader vref ++ wordL (TaggedText.tagText "=")) --- rhsL.Value

        Utilities.colorPrintL outWriter opts fullL

/// Used to make a copy of input in order to include the input when displaying the error text.
type internal FsiStdinSyphon(errorWriter: TextWriter) =
    let syphonText = new StringBuilder()

    /// Clears the syphon text
    member x.Reset () =
        syphonText.Clear() |> ignore

    /// Adds a new line to the syphon text
    member x.Add (str:string) =
        syphonText.Append str |> ignore

    /// Gets the indicated line in the syphon text
    member x.GetLine filename i =
        if filename <> Lexhelp.stdinMockFilename then
            ""
        else
            let text = syphonText.ToString()
            // In Visual Studio, when sending a block of text, it  prefixes  with '# <line> "filename"\n'
            // and postfixes with '# 1 "stdin"\n'. To first, get errors filename context,
            // and second to get them back into stdin context (no position stack...).
            // To find an error line, trim upto the last stdinReset string the syphoned text.
            //printf "PrePrune:-->%s<--\n\n" text;
            let rec prune (text:string) =
                let stdinReset = "# 1 \"stdin\"\n"
                let idx = text.IndexOf(stdinReset,StringComparison.Ordinal)
                if idx <> -1 then
                    prune (text.Substring(idx + stdinReset.Length))
                else
                    text

            let text = prune text
            let lines = text.Split '\n'
            if 0 < i && i <= lines.Length then lines.[i-1] else ""

    /// Display the given error.
    member syphon.PrintError (tcConfig:TcConfigBuilder, err) =
        Utilities.ignoreAllErrors (fun () ->
            let severity = FSharpDiagnosticSeverity.Error
            DoWithDiagnosticColor severity (fun () ->
                errorWriter.WriteLine();
                writeViaBuffer errorWriter (OutputDiagnosticContext "  " syphon.GetLine) err;
                writeViaBuffer errorWriter (OutputDiagnostic (tcConfig.implicitIncludeDir,tcConfig.showFullPaths,tcConfig.flatErrors,tcConfig.errorStyle,severity))  err;
                errorWriter.WriteLine()
                errorWriter.WriteLine()
                errorWriter.Flush()))



/// Encapsulates functions used to write to outWriter and errorWriter
type internal FsiConsoleOutput(tcConfigB, outWriter:TextWriter, errorWriter:TextWriter) =

    let nullOut = new StreamWriter(Stream.Null) :> TextWriter
    let fprintfnn (os: TextWriter) fmt  = Printf.kfprintf (fun _ -> os.WriteLine(); os.WriteLine()) os fmt
    /// uprintf to write usual responses to stdout (suppressed by --quiet), with various pre/post newlines
    member out.uprintf    fmt = fprintf   (if tcConfigB.noFeedback then nullOut else outWriter) fmt
    member out.uprintfn   fmt = fprintfn  (if tcConfigB.noFeedback then nullOut else outWriter) fmt
    member out.uprintfnn  fmt = fprintfnn (if tcConfigB.noFeedback then nullOut else outWriter) fmt
    member out.uprintnf   fmt = out.uprintfn ""; out.uprintf   fmt
    member out.uprintnfn  fmt = out.uprintfn ""; out.uprintfn  fmt
    member out.uprintnfnn fmt = out.uprintfn ""; out.uprintfnn fmt

    member out.Out = outWriter
    member out.Error = errorWriter


/// This ErrorLogger reports all warnings, but raises StopProcessing on first error or early exit
type internal ErrorLoggerThatStopsOnFirstError(tcConfigB:TcConfigBuilder, fsiStdinSyphon:FsiStdinSyphon, fsiConsoleOutput: FsiConsoleOutput) =
    inherit ErrorLogger("ErrorLoggerThatStopsOnFirstError")
    let mutable errorCount = 0

    member x.SetError() =
        errorCount <- 1

    member x.ResetErrorCount() = (errorCount <- 0)

    override x.DiagnosticSink(err, severity) =
        if (severity = FSharpDiagnosticSeverity.Error) || ReportWarningAsError tcConfigB.errorSeverityOptions err  then
            fsiStdinSyphon.PrintError(tcConfigB,err)
            errorCount <- errorCount + 1
            if tcConfigB.abortOnError then exit 1 (* non-zero exit code *)
            // STOP ON FIRST ERROR (AVOIDS PARSER ERROR RECOVERY)
            raise StopProcessing
        else
          DoWithDiagnosticColor severity (fun () ->
            if ReportWarning tcConfigB.errorSeverityOptions err then
                fsiConsoleOutput.Error.WriteLine()
                writeViaBuffer fsiConsoleOutput.Error (OutputDiagnosticContext "  " fsiStdinSyphon.GetLine) err
                writeViaBuffer fsiConsoleOutput.Error (OutputDiagnostic (tcConfigB.implicitIncludeDir,tcConfigB.showFullPaths,tcConfigB.flatErrors,tcConfigB.errorStyle,severity)) err
                fsiConsoleOutput.Error.WriteLine()
                fsiConsoleOutput.Error.WriteLine()
                fsiConsoleOutput.Error.Flush())

    override x.ErrorCount = errorCount

type ErrorLogger with
    member x.CheckForErrors() = (x.ErrorCount > 0)
    /// A helper function to check if its time to abort
    member x.AbortOnError(fsiConsoleOutput:FsiConsoleOutput) =
        if x.ErrorCount > 0 then
            fprintf fsiConsoleOutput.Error "%s" (FSIstrings.SR.stoppedDueToError())
            fsiConsoleOutput.Error.Flush()
            raise StopProcessing

/// Get the directory name from a string, with some defaults if it doesn't have one
let internal directoryName (s:string) =
    if s = "" then "."
    else
        match Path.GetDirectoryName s with
        | null -> if FileSystem.IsPathRootedShim s then s else "."
        | res -> if res = "" then "." else res


//----------------------------------------------------------------------------
// cmd line - state for options
//----------------------------------------------------------------------------

/// Process the command line options
type internal FsiCommandLineOptions(fsi: FsiEvaluationSessionHostConfig,
                argv: string[],
                tcConfigB,
                fsiConsoleOutput: FsiConsoleOutput) =

    let mutable enableConsoleKeyProcessing =
       // Mono on Win32 doesn't implement correct console processing
       not (runningOnMono && System.Environment.OSVersion.Platform = System.PlatformID.Win32NT)

    let mutable gui        = not runningOnMono // override via "--gui", on by default except when on Mono
#if DEBUG
    let mutable showILCode = false // show modul il code
#endif
    let mutable showTypes  = true  // show types after each interaction?
    let mutable fsiServerName = ""
    let mutable interact = true
    let mutable explicitArgs = []
    let mutable writeReferencesAndExit = None

    let mutable inputFilesAcc   = []

    let mutable fsiServerInputCodePage = None
    let mutable fsiServerOutputCodePage = None
    let mutable fsiLCID = None

    // internal options
    let mutable probeToSeeIfConsoleWorks         = true
    let mutable peekAheadOnConsoleToPermitTyping = true

    let isInteractiveServer() = fsiServerName <> ""
    let recordExplicitArg arg = explicitArgs <- explicitArgs @ [arg]

    let executableFileNameWithoutExtension =
        lazy
            let getFsiCommandLine () =
                let fileNameWithoutExtension path = Path.GetFileNameWithoutExtension(path)

                let currentProcess = Process.GetCurrentProcess()
                let processFileName = fileNameWithoutExtension currentProcess.MainModule.FileName

                let commandLineExecutableFileName =
                    try fileNameWithoutExtension (Environment.GetCommandLineArgs().[0])
                    with _ -> ""

                let stringComparison =
                    match Environment.OSVersion.Platform with
                    | PlatformID.MacOSX
                    | PlatformID.Unix -> StringComparison.Ordinal
                    | _ -> StringComparison.OrdinalIgnoreCase

                if String.Compare(processFileName, commandLineExecutableFileName, stringComparison) = 0
                then processFileName
                else sprintf "%s %s" processFileName commandLineExecutableFileName

            tcConfigB.exename |> Option.defaultWith getFsiCommandLine


    // Additional fsi options are list below.
    // In the "--help", these options can be printed either before (fsiUsagePrefix) or after (fsiUsageSuffix) the core options.

    let displayHelpFsi tcConfigB (blocks:CompilerOptionBlock list) =
        DisplayBannerText tcConfigB;
        fprintfn fsiConsoleOutput.Out ""
        fprintfn fsiConsoleOutput.Out "%s" (FSIstrings.SR.fsiUsage(executableFileNameWithoutExtension.Value))
        PrintCompilerOptionBlocks blocks
        exit 0

    // option tags
    let tagFile        = "<file>"
    let tagNone        = ""

    /// These options precede the FsiCoreCompilerOptions in the help blocks
    let fsiUsagePrefix tcConfigB =
      [PublicOptions(FSIstrings.SR.fsiInputFiles(),
        [CompilerOption("use",tagFile, OptionString (fun s -> inputFilesAcc <- inputFilesAcc @ [(s,true)]), None,
                                 Some (FSIstrings.SR.fsiUse()));
         CompilerOption("load",tagFile, OptionString (fun s -> inputFilesAcc <- inputFilesAcc @ [(s,false)]), None,
                                 Some (FSIstrings.SR.fsiLoad()));
        ]);
       PublicOptions(FSIstrings.SR.fsiCodeGeneration(),[]);
       PublicOptions(FSIstrings.SR.fsiErrorsAndWarnings(),[]);
       PublicOptions(FSIstrings.SR.fsiLanguage(),[]);
       PublicOptions(FSIstrings.SR.fsiMiscellaneous(),[]);
       PublicOptions(FSIstrings.SR.fsiAdvanced(),[]);
       PrivateOptions(
        [// Make internal fsi-server* options. Do not print in the help. They are used by VFSI.
         CompilerOption("fsi-server-report-references","", OptionString (fun s -> writeReferencesAndExit <- Some s), None, None);
         CompilerOption("fsi-server","", OptionString (fun s -> fsiServerName <- s), None, None); // "FSI server mode on given named channel");
         CompilerOption("fsi-server-input-codepage","",OptionInt (fun n -> fsiServerInputCodePage <- Some(n)), None, None); // " Set the input codepage for the console");
         CompilerOption("fsi-server-output-codepage","",OptionInt (fun n -> fsiServerOutputCodePage <- Some(n)), None, None); // " Set the output codepage for the console");
         CompilerOption("fsi-server-no-unicode","", OptionUnit (fun () -> fsiServerOutputCodePage <- None;  fsiServerInputCodePage <- None), None, None); // "Do not set the codepages for the console");
         CompilerOption("fsi-server-lcid","", OptionInt (fun n -> fsiLCID <- Some(n)), None, None); // "LCID from Visual Studio"

         // We do not want to print the "script.fsx arg2..." as part of the options
         CompilerOption("script.fsx arg1 arg2 ...","",
                                 OptionGeneral((fun args -> args.Length > 0 && IsScript args.[0]),
                                               (fun args -> let scriptFile = args.[0]
                                                            let scriptArgs = List.tail args
                                                            inputFilesAcc <- inputFilesAcc @ [(scriptFile,true)]   (* record script.fsx for evaluation *)
                                                            List.iter recordExplicitArg scriptArgs            (* record rest of line as explicit arguments *)
                                                            tcConfigB.noFeedback <- true                      (* "quiet", no banners responses etc *)
                                                            interact <- false                                 (* --exec, exit after eval *)
                                                            [] (* no arguments passed on, all consumed here *)

                                               )),None,None); // "Run script.fsx with the follow command line arguments: arg1 arg2 ...");
        ]);
       PrivateOptions(
        [
         // Private options, related to diagnostics around console probing
         CompilerOption("probeconsole","", OptionSwitch (fun flag -> probeToSeeIfConsoleWorks <- flag=OptionSwitch.On), None, None); // "Probe to see if Console looks functional");

         CompilerOption("peekahead","", OptionSwitch (fun flag -> peekAheadOnConsoleToPermitTyping <- flag=OptionSwitch.On), None, None); // "Probe to see if Console looks functional");

         // Disables interaction (to be used by libraries embedding FSI only!)
         CompilerOption("noninteractive","", OptionUnit (fun () -> interact <-  false), None, None);     // "Deprecated, use --exec instead"

        ])
      ]

    /// These options follow the FsiCoreCompilerOptions in the help blocks
    let fsiUsageSuffix tcConfigB =
      [PublicOptions(FSComp.SR.optsHelpBannerInputFiles(),
        [CompilerOption("--","", OptionRest recordExplicitArg, None,
                                 Some (FSIstrings.SR.fsiRemaining()));
        ]);
       PublicOptions(FSComp.SR.optsHelpBannerMisc(),
        [   CompilerOption("help", tagNone,
                                 OptionHelp (fun blocks -> displayHelpFsi tcConfigB blocks),None,
                                 Some (FSIstrings.SR.fsiHelp()))
        ]);
       PrivateOptions(
        [   CompilerOption("?", tagNone, OptionHelp (fun blocks -> displayHelpFsi tcConfigB blocks), None, None); // "Short form of --help");
            CompilerOption("help", tagNone, OptionHelp (fun blocks -> displayHelpFsi tcConfigB blocks), None, None); // "Short form of --help");
            CompilerOption("full-help", tagNone, OptionHelp (fun blocks -> displayHelpFsi tcConfigB blocks), None, None); // "Short form of --help");
        ]);
       PublicOptions(FSComp.SR.optsHelpBannerAdvanced(),
        [CompilerOption("exec",                 "", OptionUnit (fun () -> interact <- false), None, Some (FSIstrings.SR.fsiExec()));
         CompilerOption("gui",                  tagNone, OptionSwitch(fun flag -> gui <- (flag = OptionSwitch.On)),None,Some (FSIstrings.SR.fsiGui()));
         CompilerOption("quiet",                "", OptionUnit (fun () -> tcConfigB.noFeedback <- true), None,Some (FSIstrings.SR.fsiQuiet()));
         (* Renamed --readline and --no-readline to --tabcompletion:+|- *)
         CompilerOption("readline",             tagNone, OptionSwitch(fun flag -> enableConsoleKeyProcessing <- (flag = OptionSwitch.On)),           None, Some(FSIstrings.SR.fsiReadline()));
         CompilerOption("quotations-debug",     tagNone, OptionSwitch(fun switch -> tcConfigB.emitDebugInfoInQuotations <- switch = OptionSwitch.On),None, Some(FSIstrings.SR.fsiEmitDebugInfoInQuotations()));
         CompilerOption("shadowcopyreferences", tagNone, OptionSwitch(fun flag -> tcConfigB.shadowCopyReferences <- flag = OptionSwitch.On),         None, Some(FSIstrings.SR.shadowCopyReferences()));
        ]);
      ]


    /// Process command line, flags and collect filenames.
    /// The ParseCompilerOptions function calls imperative function to process "real" args
    /// Rather than start processing, just collect names, then process them.
    let sourceFiles =
        let collect name =
            let fsx = IsScript name
            inputFilesAcc <- inputFilesAcc @ [(name,fsx)] // O(n^2), but n small...
        try
           let fsiCompilerOptions = fsiUsagePrefix tcConfigB @ GetCoreFsiCompilerOptions tcConfigB @ fsiUsageSuffix tcConfigB
           let abbrevArgs = GetAbbrevFlagSet tcConfigB false
           ParseCompilerOptions (collect, fsiCompilerOptions, List.tail (PostProcessCompilerArgs abbrevArgs argv))
        with e ->
            stopProcessingRecovery e range0; failwithf "Error creating evaluation session: %A" e
        inputFilesAcc

    // We need a dependency provider with native resolution.  Managed resolution is handled by generated `#r`
    let dependencyProvider = new DependencyProvider(NativeResolutionProbe(tcConfigB.GetNativeProbingRoots))

    do
        if tcConfigB.utf8output then
            let prev = Console.OutputEncoding
            Console.OutputEncoding <- System.Text.Encoding.UTF8
#if FX_NO_APP_DOMAINS
            ignore prev
#else
            System.AppDomain.CurrentDomain.ProcessExit.Add(fun _ -> Console.OutputEncoding <- prev)
#endif
    do
        let firstArg =
            match sourceFiles with
            | [] -> argv.[0]
            | _  -> fst (List.head (List.rev sourceFiles) )
        let args = Array.ofList (firstArg :: explicitArgs)
        fsi.ReportUserCommandLineArgs args


    //----------------------------------------------------------------------------
    // Banner
    //----------------------------------------------------------------------------

    member _.ShowBanner() =
        fsiConsoleOutput.uprintnfn "%s" (tcConfigB.productNameForBannerText)
        fsiConsoleOutput.uprintfnn "%s" (FSComp.SR.optsCopyright())
        fsiConsoleOutput.uprintfn  "%s" (FSIstrings.SR.fsiBanner3())

    member _.ShowHelp(m) =
        let helpLine = sprintf "%s --help" executableFileNameWithoutExtension.Value

        fsiConsoleOutput.uprintfn  ""
        fsiConsoleOutput.uprintfnn "%s" (FSIstrings.SR.fsiIntroTextHeader1directives())
        fsiConsoleOutput.uprintfn  """    #r "file.dll";;                               // %s""" (FSIstrings.SR.fsiIntroTextHashrInfo())
        fsiConsoleOutput.uprintfn  """    #i "package source uri";;                     // %s""" (FSIstrings.SR.fsiIntroPackageSourceUriInfo())
        fsiConsoleOutput.uprintfn  """    #I "path";;                                   // %s""" (FSIstrings.SR.fsiIntroTextHashIInfo())
        fsiConsoleOutput.uprintfn  """    #load "file.fs" ...;;                         // %s""" (FSIstrings.SR.fsiIntroTextHashloadInfo())
        fsiConsoleOutput.uprintfn  """    #time ["on"|"off"];;                          // %s""" (FSIstrings.SR.fsiIntroTextHashtimeInfo())
        fsiConsoleOutput.uprintfn  """    #help;;                                       // %s""" (FSIstrings.SR.fsiIntroTextHashhelpInfo())

        if tcConfigB.langVersion.SupportsFeature(LanguageFeature.PackageManagement) then
            for msg in dependencyProvider.GetRegisteredDependencyManagerHelpText(tcConfigB.compilerToolPaths, getOutputDir tcConfigB, reportError m) do
                fsiConsoleOutput.uprintfn "%s" msg

        fsiConsoleOutput.uprintfn  """    #quit;;                                       // %s""" (FSIstrings.SR.fsiIntroTextHashquitInfo())   (* last thing you want to do, last thing in the list - stands out more *)
        fsiConsoleOutput.uprintfn  "";
        fsiConsoleOutput.uprintfnn "%s" (FSIstrings.SR.fsiIntroTextHeader2commandLine())
        fsiConsoleOutput.uprintfn  "%s" (FSIstrings.SR.fsiIntroTextHeader3(helpLine))
        fsiConsoleOutput.uprintfn  ""
        fsiConsoleOutput.uprintfn  ""

#if DEBUG
    member _.ShowILCode with get() = showILCode and set v = showILCode <- v
#endif
    member _.ShowTypes with get() = showTypes and set v = showTypes <- v
    member _.FsiServerName = fsiServerName
    member _.FsiServerInputCodePage = fsiServerInputCodePage
    member _.FsiServerOutputCodePage = fsiServerOutputCodePage
    member _.FsiLCID with get() = fsiLCID and set v = fsiLCID <- v
    member _.UseServerPrompt = isInteractiveServer()
    member _.IsInteractiveServer = isInteractiveServer()
    member _.ProbeToSeeIfConsoleWorks = probeToSeeIfConsoleWorks
    member _.EnableConsoleKeyProcessing = enableConsoleKeyProcessing

    member _.Interact = interact
    member _.PeekAheadOnConsoleToPermitTyping = peekAheadOnConsoleToPermitTyping
    member _.SourceFiles = sourceFiles
    member _.Gui = gui

    member _.WriteReferencesAndExit = writeReferencesAndExit

    member _.DependencyProvider = dependencyProvider
    member _.FxResolver = tcConfigB.FxResolver

/// Set the current ui culture for the current thread.
let internal SetCurrentUICultureForThread (lcid : int option) =
    let culture = Thread.CurrentThread.CurrentUICulture
    match lcid with
    | Some n -> Thread.CurrentThread.CurrentUICulture <- new CultureInfo(n)
    | None -> ()
    { new IDisposable with member x.Dispose() = Thread.CurrentThread.CurrentUICulture <- culture }

//----------------------------------------------------------------------------
// Reporting - warnings, errors
//----------------------------------------------------------------------------

let internal InstallErrorLoggingOnThisThread errorLogger =
    if progress then dprintfn "Installing logger on id=%d name=%s" Thread.CurrentThread.ManagedThreadId Thread.CurrentThread.Name
    SetThreadErrorLoggerNoUnwind(errorLogger)
    SetThreadBuildPhaseNoUnwind(BuildPhase.Interactive)

/// Set the input/output encoding. The use of a thread is due to a known bug on
/// on Vista where calls to Console.InputEncoding can block the process.
let internal SetServerCodePages(fsiOptions: FsiCommandLineOptions) =
    match fsiOptions.FsiServerInputCodePage, fsiOptions.FsiServerOutputCodePage with
    | None,None -> ()
    | inputCodePageOpt,outputCodePageOpt ->
        let mutable successful = false
        Async.Start (async { do match inputCodePageOpt with
                                | None -> ()
                                | Some(n:int) ->
                                      let encoding = System.Text.Encoding.GetEncoding(n)
                                      // Note this modifies the real honest-to-goodness settings for the current shell.
                                      // and the modifications hang around even after the process has exited.
                                      Console.InputEncoding <- encoding
                             do match outputCodePageOpt with
                                | None -> ()
                                | Some(n:int) ->
                                      let encoding = System.Text.Encoding.GetEncoding n
                                      // Note this modifies the real honest-to-goodness settings for the current shell.
                                      // and the modifications hang around even after the process has exited.
                                      Console.OutputEncoding <- encoding
                             do successful <- true  });
        for pause in [10;50;100;1000;2000;10000] do
            if not successful then
                Thread.Sleep(pause);
#if LOGGING_GUI
        if not !successful then
            System.Windows.Forms.MessageBox.Show(FSIstrings.SR.fsiConsoleProblem()) |> ignore
#endif

//----------------------------------------------------------------------------
// Prompt printing
//----------------------------------------------------------------------------

type internal FsiConsolePrompt(fsiOptions: FsiCommandLineOptions, fsiConsoleOutput: FsiConsoleOutput) =

    // A prompt gets "printed ahead" at start up. Tells users to start type while initialisation completes.
    // A prompt can be skipped by "silent directives", e.g. ones sent to FSI by VS.
    let mutable dropPrompt = 0
    // NOTE: SERVER-PROMPT is not user displayed, rather it's a prefix that code elsewhere
    // uses to identify the prompt, see service\FsPkgs\FSharp.VS.FSI\fsiSessionToolWindow.fs
    let prompt = if fsiOptions.UseServerPrompt then "SERVER-PROMPT>\n" else "> "

    member _.Print()      = if dropPrompt = 0 then fsiConsoleOutput.uprintf "%s" prompt else dropPrompt <- dropPrompt - 1
    member _.PrintAhead() = dropPrompt <- dropPrompt + 1; fsiConsoleOutput.uprintf "%s" prompt
    member _.SkipNext()   = dropPrompt <- dropPrompt + 1
    member _.FsiOptions = fsiOptions



//----------------------------------------------------------------------------
// Startup processing
//----------------------------------------------------------------------------
type internal FsiConsoleInput(fsi: FsiEvaluationSessionHostConfig, fsiOptions: FsiCommandLineOptions, inReader: TextReader, outWriter: TextWriter) =

    let consoleOpt =
        // The "console.fs" code does a limited form of "TAB-completion".
        // Currently, it turns on if it looks like we have a console.
        if fsiOptions.EnableConsoleKeyProcessing then
            fsi.GetOptionalConsoleReadLine(fsiOptions.ProbeToSeeIfConsoleWorks)
        else
            None

    // When VFSI is running, there should be no "console", and in particular the console.fs readline code should not to run.
    do  if fsiOptions.IsInteractiveServer then assert(consoleOpt.IsNone)

    /// This threading event gets set after the first-line-reader has finished its work
    let consoleReaderStartupDone = new ManualResetEvent(false)

    /// When using a key-reading console this holds the first line after it is read
    let mutable firstLine = None

    /// Peek on the standard input so that the user can type into it from a console window.
    do if fsiOptions.Interact then
         if fsiOptions.PeekAheadOnConsoleToPermitTyping then
          (new Thread(fun () ->
              match consoleOpt with
              | Some console when fsiOptions.EnableConsoleKeyProcessing && not fsiOptions.UseServerPrompt ->
                  if List.isEmpty fsiOptions.SourceFiles then
                      if progress then fprintfn outWriter "first-line-reader-thread reading first line...";
                      firstLine <- Some(console());
                      if progress then fprintfn outWriter "first-line-reader-thread got first line = %A..." firstLine;
                  consoleReaderStartupDone.Set() |> ignore
                  if progress then fprintfn outWriter "first-line-reader-thread has set signal and exited." ;
              | _ ->
                  ignore(inReader.Peek());
                  consoleReaderStartupDone.Set() |> ignore
            )).Start()
         else
           if progress then fprintfn outWriter "first-line-reader-thread not in use."
           consoleReaderStartupDone.Set() |> ignore

    /// Try to get the first line, if we snarfed it while probing.
    member _.TryGetFirstLine() = let r = firstLine in firstLine <- None; r

    /// Try to get the console, if it appears operational.
    member _.TryGetConsole() = consoleOpt

    member _.In = inReader

    member _.WaitForInitialConsoleInput() = WaitHandle.WaitAll [| consoleReaderStartupDone  |] |> ignore;


//----------------------------------------------------------------------------
// FsiDynamicCompilerState
//----------------------------------------------------------------------------

type internal FsiInteractionStepStatus =
    | CtrlC
    | EndOfFile
    | Completed of option<FsiValue>
    | CompletedWithAlreadyReportedError
    | CompletedWithReportedError of exn

[<AutoSerializable(false)>]
[<NoEquality; NoComparison>]
type internal FsiDynamicCompilerState =
    { optEnv    : Optimizer.IncrementalOptimizationEnv
      emEnv     : ILRuntimeWriter.emEnv
      tcGlobals : TcGlobals
      tcState   : TcState
      tcImports   : TcImports
      ilxGenerator : IlxGen.IlxAssemblyGenerator
      boundValues : NameMap<Val>
      // Why is this not in FsiOptions?
      timing    : bool
      debugBreak : bool }

let internal WithImplicitHome (tcConfigB, dir) f =
    let old = tcConfigB.implicitIncludeDir
    tcConfigB.implicitIncludeDir <- dir;
    try f()
    finally tcConfigB.implicitIncludeDir <- old

let internal convertReflectionTypeToILTypeRef (reflectionTy: Type) =
    if reflectionTy.Assembly.IsDynamic then
        raise (NotSupportedException(sprintf "Unable to import type, %A, from a dynamic assembly." reflectionTy))

    if not reflectionTy.IsPublic && not reflectionTy.IsNestedPublic then
        invalidOp (sprintf "Cannot import the non-public type, %A." reflectionTy)

    let aref = ILAssemblyRef.FromAssemblyName(reflectionTy.Assembly.GetName())
    let scoref = ILScopeRef.Assembly aref

    let fullName = reflectionTy.FullName
    let index = fullName.IndexOf("[")
    let fullName =
        if index = -1 then
            fullName
        else
            fullName.Substring(0, index)

    let isTop = reflectionTy.DeclaringType = null
    if isTop then
        ILTypeRef.Create(scoref, [], fullName)
    else
        let names = String.split StringSplitOptions.None [|"+";"."|] fullName
        let enc = names.[..names.Length - 2]
        let nm = names.[names.Length - 1]
        ILTypeRef.Create(scoref, List.ofArray enc, nm)

let rec internal convertReflectionTypeToILType (reflectionTy: Type) =
    let reflectionTy =
        // Special case functions.
        if FSharp.Reflection.FSharpType.IsFunction reflectionTy then
            let ctors = reflectionTy.GetConstructors(BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance)
            if ctors.Length = 1 &&
               ctors.[0].GetCustomAttribute<CompilerGeneratedAttribute>() <> null &&
               not ctors.[0].IsPublic &&
               PrettyNaming.IsCompilerGeneratedName reflectionTy.Name then
                let rec get (typ: Type) = if FSharp.Reflection.FSharpType.IsFunction typ.BaseType then get typ.BaseType else typ
                get reflectionTy
            else
                reflectionTy
        else
            reflectionTy

    let tref = convertReflectionTypeToILTypeRef reflectionTy
    let genericArgs =
        reflectionTy.GenericTypeArguments
        |> Seq.map convertReflectionTypeToILType
        |> List.ofSeq

    let boxity =
        if reflectionTy.IsValueType then
            ILBoxity.AsValue
        else
            ILBoxity.AsObject

    let tspec = ILTypeSpec.Create(tref, genericArgs)

    mkILTy boxity tspec

let internal mkBoundValueTypedImpl tcGlobals m moduleName name ty =
    let vis = Accessibility.TAccess([])
    let compPath = (CompilationPath.CompPath(ILScopeRef.Local, []))
    let mutable mty = Unchecked.defaultof<_>
    let moduleOrNamespace = Construct.NewModuleOrNamespace (Some compPath) vis (Ident(moduleName, m)) XmlDoc.Empty [] (MaybeLazy.Lazy(lazy mty))
    let v =
        Construct.NewVal
            (name, m, None, ty, ValMutability.Immutable,
             false, Some(ValReprInfo([], [], { Attribs = []; Name = None })), vis, ValNotInRecScope, None, NormalVal, [], ValInline.Optional,
             XmlDoc.Empty, true, false, false, false,
             false, false, None, Parent(TypedTreeBasics.ERefLocal moduleOrNamespace))
    mty <- ModuleOrNamespaceType(ModuleOrNamespaceKind.ModuleOrType, QueueList.one v, QueueList.empty)

    let bindExpr = TypedTreeOps.mkCallDefaultOf tcGlobals range0 ty
    let binding = Binding.TBind(v, bindExpr, DebugPointAtBinding.NoneAtLet)
    let mbinding = ModuleOrNamespaceBinding.Module(moduleOrNamespace, TMDefs([TMDefLet(binding, m)]))
    let expr = ModuleOrNamespaceExprWithSig(mty, TMDefs([TMDefs[TMDefRec(false, [], [mbinding], m)]]), range0)
    moduleOrNamespace, v, TypedImplFile.TImplFile(QualifiedNameOfFile.QualifiedNameOfFile(Ident(moduleName, m)), [], expr, false, false, StampMap.Empty)

/// Encapsulates the coordination of the typechecking, optimization and code generation
/// components of the F# compiler for interactively executed fragments of code.
///
/// A single instance of this object is created per interactive session.
type internal FsiDynamicCompiler
                       (fsi: FsiEvaluationSessionHostConfig,
                        timeReporter : FsiTimeReporter,
                        tcConfigB: TcConfigBuilder,
                        tcLockObject : obj,
                        outWriter: TextWriter,
                        tcImports: TcImports,
                        tcGlobals: TcGlobals,
                        fsiOptions : FsiCommandLineOptions,
                        fsiConsoleOutput : FsiConsoleOutput,
                        fsiCollectible: bool,
                        niceNameGen,
                        resolveAssemblyRef) =

    let ilGlobals = tcGlobals.ilg

    let outfile = "TMPFSCI.exe"
    let assemblyName = "FSI-ASSEMBLY"

    let valueBoundEvent = Control.Event<_>()

    let mutable fragmentId = 0
    let mutable prevIt : ValRef option = None

    let mutable needsPackageResolution = false

    let generateDebugInfo = tcConfigB.debuginfo

    let valuePrinter = FsiValuePrinter(fsi, tcConfigB, tcGlobals, generateDebugInfo, resolveAssemblyRef, outWriter)

    let assemblyBuilder,moduleBuilder = ILRuntimeWriter.mkDynamicAssemblyAndModule (assemblyName, tcConfigB.optSettings.localOpt(), generateDebugInfo, fsiCollectible)

    let rangeStdin = rangeN Lexhelp.stdinMockFilename 0

    //let _writer = moduleBuilder.GetSymWriter()

    let infoReader = InfoReader(tcGlobals,tcImports.GetImportMap())

    /// Add attributes
    let CreateModuleFragment (tcConfigB: TcConfigBuilder, assemblyName, codegenResults) =
        if progress then fprintfn fsiConsoleOutput.Out "Creating main module...";
        let mainModule = mkILSimpleModule assemblyName (GetGeneratedILModuleName tcConfigB.target assemblyName) (tcConfigB.target = CompilerTarget.Dll) tcConfigB.subsystemVersion tcConfigB.useHighEntropyVA (mkILTypeDefs codegenResults.ilTypeDefs) None None 0x0 (mkILExportedTypes []) ""
        { mainModule
          with Manifest =
                (let man = mainModule.ManifestOfAssembly
                 Some { man with  CustomAttrsStored = storeILCustomAttrs (mkILCustomAttrs codegenResults.ilAssemAttrs) }) }

    let ProcessCodegenResults (ctok, errorLogger: ErrorLogger, istate, optEnv, tcState: TcState, tcConfig, prefixPath, showTypes: bool, isIncrementalFragment, fragName, declaredImpls, ilxGenerator: IlxAssemblyGenerator, codegenResults) =
        let emEnv = istate.emEnv

        // Each input is like a small separately compiled extension to a single source file.
        // The incremental extension to the environment is dictated by the "signature" of the values as they come out
        // of the type checker. Hence we add the declaredImpls (unoptimized) to the environment, rather than the
        // optimizedImpls.
        ilxGenerator.AddIncrementalLocalAssemblyFragment (isIncrementalFragment, fragName, declaredImpls)

        ReportTime tcConfig "TAST -> ILX";
        errorLogger.AbortOnError(fsiConsoleOutput);

        ReportTime tcConfig "Linking";
        let ilxMainModule = CreateModuleFragment (tcConfigB, assemblyName, codegenResults)

        errorLogger.AbortOnError(fsiConsoleOutput);

        ReportTime tcConfig "Assembly refs Normalised";
        let mainmod3 = Morphs.morphILScopeRefsInILModuleMemoized (NormalizeAssemblyRefs (ctok, ilGlobals, tcImports)) ilxMainModule
        errorLogger.AbortOnError(fsiConsoleOutput);

#if DEBUG
        if fsiOptions.ShowILCode then
            fsiConsoleOutput.uprintnfn "--------------------";
            ILAsciiWriter.output_module outWriter ilGlobals mainmod3;
            fsiConsoleOutput.uprintnfn "--------------------"
#else
        ignore(fsiOptions)
#endif

        ReportTime tcConfig "Reflection.Emit";

        let emEnv,execs = ILRuntimeWriter.emitModuleFragment(ilGlobals, tcConfig.emitTailcalls, emEnv, assemblyBuilder, moduleBuilder, mainmod3, generateDebugInfo, resolveAssemblyRef, tcGlobals.TryFindSysILTypeRef)

        errorLogger.AbortOnError(fsiConsoleOutput);

        // Explicitly register the resources with the QuotationPickler module
        // We would save them as resources into the dynamic assembly but there is missing
        // functionality System.Reflection for dynamic modules that means they can't be read back out
        let cenv = { ilg = ilGlobals ; emitTailcalls = tcConfig.emitTailcalls; generatePdb = generateDebugInfo; resolveAssemblyRef=resolveAssemblyRef; tryFindSysILTypeRef=tcGlobals.TryFindSysILTypeRef }
        for (referencedTypeDefs, bytes) in codegenResults.quotationResourceInfo do
            let referencedTypes =
                [| for tref in referencedTypeDefs do
                      yield ILRuntimeWriter.LookupTypeRef cenv emEnv tref  |]
            Microsoft.FSharp.Quotations.Expr.RegisterReflectedDefinitions (assemblyBuilder, fragName, bytes, referencedTypes);


        ReportTime tcConfig "Run Bindings";
        timeReporter.TimeOpIf istate.timing (fun () ->
          execs |> List.iter (fun exec ->
            match exec() with
            | Some err ->
                match errorLogger with
                | :? ErrorLoggerThatStopsOnFirstError as errorLogger ->
                    fprintfn fsiConsoleOutput.Error "%s" (err.ToString())
                    errorLogger.SetError()
                    errorLogger.AbortOnError(fsiConsoleOutput)
                | _ ->
                    raise (StopProcessingExn (Some err))

            | None -> ())) ;

        errorLogger.AbortOnError(fsiConsoleOutput);

        // Echo the decls (reach inside wrapping)
        // This code occurs AFTER the execution of the declarations.
        // So stored values will have been initialised, modified etc.
        if showTypes && not tcConfig.noFeedback then
            let denv = tcState.TcEnvFromImpls.DisplayEnv
            let denv =
                if isIncrementalFragment then
                  // Extend denv with a (Val -> layout option) function for printing of val bindings.
                  {denv with generatedValueLayout = (fun v -> valuePrinter.InvokeDeclLayout (emEnv, ilxGenerator, v)) }
                else
                  // With #load items, the vals in the inferred signature do not tie up with those generated. Disable printing.
                  denv
            let denv = { denv with suppressInlineKeyword = false } // dont' suppress 'inline' in 'val inline f = ...'

            // 'Open' the path for the fragment we just compiled for any future printing.
            let denv = denv.AddOpenPath (pathOfLid prefixPath)

            for (TImplFile (_qname,_,mexpr,_,_,_)) in declaredImpls do
                let responseL = NicePrint.layoutInferredSigOfModuleExpr false denv infoReader AccessibleFromSomewhere rangeStdin mexpr
                if not (isEmptyL responseL) then
                    let opts = valuePrinter.GetFsiPrintOptions()
                    Utilities.colorPrintL outWriter opts responseL |> ignore

        // Build the new incremental state.
        let istate = {istate with  optEnv    = optEnv;
                                   emEnv     = emEnv;
                                   ilxGenerator = ilxGenerator;
                                   tcState   = tcState  }

        // Return the new state and the environment at the end of the last input, ready for further inputs.
        (istate,declaredImpls)

    let ProcessTypedImpl (errorLogger: ErrorLogger, optEnv, tcState: TcState, tcConfig: TcConfig, isInteractiveItExpr, topCustomAttrs, prefixPath, isIncrementalFragment, declaredImpls, ilxGenerator: IlxAssemblyGenerator) =
        #if DEBUG
        // Logging/debugging
        if tcConfig.printAst then
            for input in declaredImpls do
                fprintfn fsiConsoleOutput.Out "AST:"
                fprintfn fsiConsoleOutput.Out "%+A" input
#endif

        errorLogger.AbortOnError(fsiConsoleOutput);

        let importMap = tcImports.GetImportMap()

        // optimize: note we collect the incremental optimization environment
        let optimizedImpls, _optData, optEnv = ApplyAllOptimizations (tcConfig, tcGlobals, (LightweightTcValForUsingInBuildMethodCall tcGlobals), outfile, importMap, isIncrementalFragment, optEnv, tcState.Ccu, declaredImpls)
        errorLogger.AbortOnError(fsiConsoleOutput);

        let fragName = textOfLid prefixPath
        let codegenResults = GenerateIlxCode (IlReflectBackend, isInteractiveItExpr, runningOnMono, tcConfig, topCustomAttrs, optimizedImpls, fragName, ilxGenerator)
        errorLogger.AbortOnError(fsiConsoleOutput);
        codegenResults, optEnv, fragName

    let ProcessInputs (ctok, errorLogger: ErrorLogger, istate: FsiDynamicCompilerState, inputs: ParsedInput list, showTypes: bool, isIncrementalFragment: bool, isInteractiveItExpr: bool, prefixPath: LongIdent) =
        let optEnv    = istate.optEnv
        let tcState   = istate.tcState
        let ilxGenerator = istate.ilxGenerator
        let tcConfig = TcConfig.Create(tcConfigB,validate=false)

        // Typecheck. The lock stops the type checker running at the same time as the
        // server intellisense implementation (which is currently incomplete and #if disabled)
        let (tcState:TcState), topCustomAttrs, declaredImpls, tcEnvAtEndOfLastInput =
            lock tcLockObject (fun _ -> TypeCheckClosedInputSet(ctok, errorLogger.CheckForErrors, tcConfig, tcImports, tcGlobals, Some prefixPath, tcState, inputs))

        let codegenResults, optEnv, fragName = ProcessTypedImpl(errorLogger, optEnv, tcState, tcConfig, isInteractiveItExpr, topCustomAttrs, prefixPath, isIncrementalFragment, declaredImpls, ilxGenerator)
        let newState, declaredImpls = ProcessCodegenResults(ctok, errorLogger, istate, optEnv, tcState, tcConfig, prefixPath, showTypes, isIncrementalFragment, fragName, declaredImpls, ilxGenerator, codegenResults)
        (newState, tcEnvAtEndOfLastInput, declaredImpls)

    let tryGetGeneratedValue istate cenv v =
        match istate.ilxGenerator.LookupGeneratedValue(valuePrinter.GetEvaluationContext(istate.emEnv), v) with
        | Some (res, ty) ->
            Some (FsiValue(res, ty, FSharpType(cenv, v.Type)))
        | _ ->
            None

    let nextFragmentId() = fragmentId <- fragmentId + 1; fragmentId

    let mkFragmentPath  i =
        // NOTE: this text shows in exn traces and type names. Make it clear and fixed width
        [mkSynId rangeStdin (FsiDynamicModulePrefix + sprintf "%04d" i)]

    let processContents istate declaredImpls =
        let tcState = istate.tcState

        let mutable itValue = None
        let mutable boundValues = istate.boundValues
        try
            let contents = FSharpAssemblyContents(tcGlobals, tcState.Ccu, Some tcState.CcuSig, tcImports, declaredImpls)
            let contentFile = contents.ImplementationFiles.[0]

            // Skip the "FSI_NNNN"
            match contentFile.Declarations with
            | [FSharpImplementationFileDeclaration.Entity (_eFakeModule,modDecls) ] ->
                let cenv = SymbolEnv(istate.tcGlobals, istate.tcState.Ccu, Some istate.tcState.CcuSig, istate.tcImports)
                for decl in modDecls do
                    match decl with
                    | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue (v,_,_) ->
                        // Report a top-level function or value definition
                        if v.IsModuleValueOrMember && not v.IsMember then
                            let fsiValueOpt =
                                match v.Item with
                                | Item.Value vref ->
                                    let fsiValueOpt = tryGetGeneratedValue istate cenv vref.Deref
                                    if fsiValueOpt.IsSome then
                                        boundValues <- boundValues |> NameMap.add v.CompiledName vref.Deref
                                    fsiValueOpt
                                | _ -> None

                            if v.CompiledName = "it" then
                                itValue <- fsiValueOpt

                            match fsiValueOpt with
                            | Some fsiValue -> valueBoundEvent.Trigger(fsiValue.ReflectionValue, fsiValue.ReflectionType, v.CompiledName)
                            | None -> ()

                            let symbol = FSharpSymbol.Create(cenv, v.Item)
                            let symbolUse = FSharpSymbolUse(tcGlobals, istate.tcState.TcEnvFromImpls.DisplayEnv, symbol, ItemOccurence.Binding, v.DeclarationLocation)
                            fsi.TriggerEvaluation (fsiValueOpt, symbolUse, decl)

                    | FSharpImplementationFileDeclaration.Entity (e,_) ->
                        // Report a top-level module or namespace definition
                        let symbol = FSharpSymbol.Create(cenv, e.Item)
                        let symbolUse = FSharpSymbolUse(tcGlobals, istate.tcState.TcEnvFromImpls.DisplayEnv, symbol, ItemOccurence.Binding, e.DeclarationLocation)
                        fsi.TriggerEvaluation (None, symbolUse, decl)

                    | FSharpImplementationFileDeclaration.InitAction _ ->
                        // Top level 'do' bindings are not reported as incremental declarations
                        ()
            | _ -> ()
        with _ -> ()

        { istate with boundValues = boundValues }, Completed itValue

    let addCcusToIncrementalEnv istate ccuinfos =
        let optEnv = List.fold (AddExternalCcuToOptimizationEnv tcGlobals) istate.optEnv ccuinfos
        istate.ilxGenerator.AddExternalCcus (ccuinfos |> List.map (fun ccuinfo -> ccuinfo.FSharpViewOfMetadata))
        { istate with optEnv = optEnv }

    let importReflectionType istate reflectionTy =
        let tcImports = istate.tcImports
        let tcGlobals = istate.tcGlobals
        let amap = tcImports.GetImportMap()

        let prevCcuinfos = tcImports.GetImportedAssemblies()

        let rec import ccuinfos (ilTy: ILType) =
            let ccuinfos, tinst =
                (ilTy.GenericArgs, (ccuinfos, []))
                ||> List.foldBack (fun ilGenericArgTy (ccuInfos, tinst) ->
                    let ccuinfos2, ty = import ccuInfos ilGenericArgTy
                    (ccuinfos2 @ ccuinfos, ty :: tinst))

            let ty = Import.ImportILType amap range0 tinst ilTy
            let ccuinfos =
                match tryTcrefOfAppTy tcGlobals ty with
                | ValueSome tcref ->
                    match tcref.CompilationPath.ILScopeRef with
                    | ILScopeRef.Assembly aref ->
                        (tcImports.GetImportedAssemblies() |> List.find (fun x -> x.FSharpViewOfMetadata.AssemblyName = aref.Name)) :: ccuinfos
                    | _ ->
                        ccuinfos
                | _ ->
                    ccuinfos
            ccuinfos, ty

        let ilTy = convertReflectionTypeToILType reflectionTy

        if not (Import.CanImportILType amap range0 ilTy) then
            invalidOp (sprintf "Unable to import type, %A." reflectionTy)

        let ccuinfos, ty = import [] ilTy
        let ccuinfos =
            ccuinfos
            |> List.distinctBy (fun x -> x.FSharpViewOfMetadata.AssemblyName)
            |> List.filter (fun asm1 -> not (prevCcuinfos |> List.exists (fun asm2 -> asm2.FSharpViewOfMetadata.AssemblyName = asm1.FSharpViewOfMetadata.AssemblyName)))
        // After we have successfully imported the type, then we can add newly resolved ccus to the env.
        addCcusToIncrementalEnv istate ccuinfos, ty

    member _.DynamicAssemblyName = assemblyName

    member _.DynamicAssembly = (assemblyBuilder :> Assembly)

    member _.EvalParsedSourceFiles (ctok, errorLogger, istate, inputs) =
        let i = nextFragmentId()
        let prefix = mkFragmentPath i
        // Ensure the path includes the qualifying name
        let inputs = inputs |> List.map (PrependPathToInput prefix)
        let istate,_,_ = ProcessInputs (ctok, errorLogger, istate, inputs, true, false, false, prefix)
        istate

    /// Evaluate the given definitions and produce a new interactive state.
    member _.EvalParsedDefinitions (ctok, errorLogger: ErrorLogger, istate, showTypes, isInteractiveItExpr, defs) =
        let filename = Lexhelp.stdinMockFilename
        let i = nextFragmentId()
        let prefix = mkFragmentPath i
        let prefixPath = pathOfLid prefix
        let impl = SynModuleOrNamespace(prefix,(*isRec*)false, SynModuleOrNamespaceKind.NamedModule,defs,PreXmlDoc.Empty,[],None,rangeStdin)
        let input = ParsedInput.ImplFile (ParsedImplFileInput (filename,true, ComputeQualifiedNameOfFileFromUniquePath (rangeStdin,prefixPath),[],[],[impl],(true (* isLastCompiland *), false (* isExe *)) ))
        let istate,tcEnvAtEndOfLastInput,declaredImpls = ProcessInputs (ctok, errorLogger, istate, [input], showTypes, true, isInteractiveItExpr, prefix)
        let tcState = istate.tcState
        let newState = { istate with tcState = tcState.NextStateAfterIncrementalFragment(tcEnvAtEndOfLastInput) }
        processContents newState declaredImpls

    /// Evaluate the given expression and produce a new interactive state.
    member fsiDynamicCompiler.EvalParsedExpression (ctok, errorLogger: ErrorLogger, istate, expr: SynExpr) =
        let tcConfig = TcConfig.Create (tcConfigB, validate=false)
        let itName = "it"

        // Construct the code that saves the 'it' value into the 'SaveIt' register.
        let defs = fsiDynamicCompiler.BuildItBinding expr

        // Evaluate the overall definitions.
        let istate = fsiDynamicCompiler.EvalParsedDefinitions (ctok, errorLogger, istate, false, true, defs) |> fst
        // Snarf the type for 'it' via the binding
        match istate.tcState.TcEnvFromImpls.NameEnv.FindUnqualifiedItem itName with
        | NameResolution.Item.Value vref ->
             if not tcConfig.noFeedback then
                 let infoReader = InfoReader(istate.tcGlobals, istate.tcImports.GetImportMap())
                 valuePrinter.InvokeExprPrinter (istate.tcState.TcEnvFromImpls.DisplayEnv, infoReader, istate.emEnv, istate.ilxGenerator, vref)

             /// Clear the value held in the previous "it" binding, if any, as long as it has never been referenced.
             match prevIt with
             | Some prevVal when not prevVal.Deref.HasBeenReferenced ->
                 istate.ilxGenerator.ClearGeneratedValue (valuePrinter.GetEvaluationContext istate.emEnv, prevVal.Deref)
             | _ -> ()
             prevIt <- Some vref

             //
             let optValue = istate.ilxGenerator.LookupGeneratedValue(valuePrinter.GetEvaluationContext(istate.emEnv), vref.Deref);
             match optValue with
             | Some (res, ty) -> istate, Completed(Some(FsiValue(res, ty, FSharpType(tcGlobals, istate.tcState.Ccu, istate.tcState.CcuSig, istate.tcImports, vref.Type))))
             | _ -> istate, Completed None

        // Return the interactive state.
        | _ -> istate, Completed None

    // Construct the code that saves the 'it' value into the 'SaveIt' register.
    member _.BuildItBinding (expr: SynExpr) =
        let m = expr.Range
        let itName = "it"

        let itID  = mkSynId m itName
        //let itExp = SynExpr.Ident itID
        let mkBind pat expr = SynBinding (None, SynBindingKind.Do, false, (*mutable*)false, [], PreXmlDoc.Empty, SynInfo.emptySynValData, pat, None, expr, m, DebugPointAtBinding.NoneAtInvisible)
        let bindingA = mkBind (mkSynPatVar None itID) expr (* let it = <expr> *)  // NOTE: the generalizability of 'expr' must not be damaged, e.g. this can't be an application
        //let saverPath  = ["Microsoft";"FSharp";"Compiler";"Interactive";"RuntimeHelpers";"SaveIt"]
        //let dots = List.replicate (saverPath.Length - 1) m
        //let bindingB = mkBind (SynPat.Wild m) (SynExpr.App (ExprAtomicFlag.NonAtomic, false, SynExpr.LongIdent (false, LongIdentWithDots(List.map (mkSynId m) saverPath,dots),None,m), itExp,m)) (* let _  = saverPath it *)
        let defA = SynModuleDecl.Let (false, [bindingA], m)
        //let defB = SynModuleDecl.Let (false, [bindingB], m)

        [defA (* ; defB *) ]

    // construct an invisible call to Debugger.Break(), in the specified range
    member _.CreateDebuggerBreak (m : range) =
        let breakPath = ["System";"Diagnostics";"Debugger";"Break"]
        let dots = List.replicate (breakPath.Length - 1) m
        let methCall = SynExpr.LongIdent (false, LongIdentWithDots(List.map (mkSynId m) breakPath, dots), None, m)
        let args = SynExpr.Const (SynConst.Unit, m)
        let breakStatement = SynExpr.App (ExprAtomicFlag.Atomic, false, methCall, args, m)
        SynModuleDecl.DoExpr(DebugPointAtBinding.NoneAtDo, breakStatement, m)

    member _.EvalRequireReference (ctok, istate, m, path) =
        if FileSystem.IsInvalidPathShim(path) then
            error(Error(FSIstrings.SR.fsiInvalidAssembly(path),m))
        // Check the file can be resolved before calling requireDLLReference
        let resolutions = tcImports.ResolveAssemblyReference(ctok, AssemblyReference(m,path,None), ResolveAssemblyReferenceMode.ReportErrors)
        tcConfigB.AddReferencedAssemblyByPath(m,path)
        let tcState = istate.tcState
        let tcEnv,(_dllinfos,ccuinfos) =
            try
                RequireDLL (ctok, tcImports, tcState.TcEnvFromImpls, assemblyName, m, path)
            with e ->
                tcConfigB.RemoveReferencedAssemblyByPath(m,path)
                reraise()
        resolutions,
        { addCcusToIncrementalEnv istate ccuinfos with tcState = tcState.NextStateAfterIncrementalFragment(tcEnv) }

    member _.EvalDependencyManagerTextFragment (packageManager:IDependencyManagerProvider, lt, m, path: string) =

        tcConfigB.packageManagerLines <- PackageManagerLine.AddLineWithKey packageManager.Key lt path m tcConfigB.packageManagerLines
        needsPackageResolution <- true

    member fsiDynamicCompiler.CommitDependencyManagerText (ctok, istate: FsiDynamicCompilerState, lexResourceManager, errorLogger) =
        if not needsPackageResolution then istate else
        needsPackageResolution <- false

        tcConfigB.packageManagerLines |> Seq.fold(fun istate kv ->
            let packageManagerKey, packageManagerLines = kv.Key, kv.Value
            match packageManagerLines with
            | [] -> istate
            | { Directive=_; LineStatus=_; Line=_; Range=m } :: _ ->
                let outputDir =  tcConfigB.outputDir |> Option.defaultValue ""

                match fsiOptions.DependencyProvider.TryFindDependencyManagerByKey(tcConfigB.compilerToolPaths, getOutputDir tcConfigB, reportError m, packageManagerKey) with
                | null ->
                    errorR(Error(fsiOptions.DependencyProvider.CreatePackageManagerUnknownError(tcConfigB.compilerToolPaths, outputDir, packageManagerKey, reportError m), m))
                    istate
                | dependencyManager ->
                    let directive d =
                        match d with
                        | Directive.Resolution -> "r"
                        | Directive.Include -> "i"

                    let packageManagerTextLines =
                        packageManagerLines |> List.map (fun line -> directive line.Directive, line.Line)

                    try
                        let tfm, rid = fsiOptions.FxResolver.GetTfmAndRid()
                        let result = fsiOptions.DependencyProvider.Resolve(dependencyManager, ".fsx", packageManagerTextLines, reportError m, tfm, rid, tcConfigB.implicitIncludeDir, "stdin.fsx", "stdin.fsx")
                        if result.Success then
                            for line in result.StdOut do Console.Out.WriteLine(line)
                            for line in result.StdError do Console.Error.WriteLine(line)
                            tcConfigB.packageManagerLines <- PackageManagerLine.SetLinesAsProcessed packageManagerKey tcConfigB.packageManagerLines
                            for folder in result.Roots do
                                tcConfigB.AddIncludePath(m, folder, "")
                            for resolution in result.Resolutions do
                                tcConfigB.AddReferencedAssemblyByPath(m, resolution)
                            let scripts = result.SourceFiles |> Seq.toList
                            if not (isNil scripts) then
                                fsiDynamicCompiler.EvalSourceFiles(ctok, istate, m, scripts, lexResourceManager, errorLogger)
                            else istate
                        else
                            // Send outputs via diagnostics
                            if (result.StdOut.Length > 0 || result.StdError.Length > 0) then
                                for line in Array.append result.StdOut result.StdError do
                                    errorR(Error(FSComp.SR.packageManagerError(line), m))
                            //Write outputs in F# Interactive and compiler
                            tcConfigB.packageManagerLines <- PackageManagerLine.RemoveUnprocessedLines packageManagerKey tcConfigB.packageManagerLines
                            istate // error already reported


                    with _ ->
                        // An exception occured during processing, so remove the lines causing the error from the package manager list.
                        tcConfigB.packageManagerLines <- PackageManagerLine.RemoveUnprocessedLines packageManagerKey tcConfigB.packageManagerLines
                        reraise ()
            ) istate

    member fsiDynamicCompiler.ProcessMetaCommandsFromInputAsInteractiveCommands(ctok, istate, sourceFile, inp) =
        WithImplicitHome
           (tcConfigB, directoryName sourceFile)
           (fun () ->
               ProcessMetaCommandsFromInput
                   ((fun st (m,nm) -> tcConfigB.TurnWarningOff(m,nm); st),
                    (fun st (m, path, directive) ->

                        let dm = tcImports.DependencyProvider.TryFindDependencyManagerInPath(tcConfigB.compilerToolPaths, getOutputDir tcConfigB, reportError m, path)

                        match dm with
                        | _, dependencyManager when not(isNull dependencyManager) ->
                            if tcConfigB.langVersion.SupportsFeature(LanguageFeature.PackageManagement) then
                                fsiDynamicCompiler.EvalDependencyManagerTextFragment (dependencyManager, directive, m, path)
                                st
                            else
                                errorR(Error(FSComp.SR.packageManagementRequiresVFive(), m))
                                st

                        | _, _ when directive = Directive.Include ->
                            errorR(Error(FSComp.SR.poundiNotSupportedByRegisteredDependencyManagers(), m))
                            st

                        // #r "Assembly"
                        | path, _ ->
                            snd (fsiDynamicCompiler.EvalRequireReference (ctok, st, m, path))
                    ),
                    (fun _ _ -> ()))
                   (tcConfigB, inp, Path.GetDirectoryName sourceFile, istate))

    member fsiDynamicCompiler.EvalSourceFiles(ctok, istate, m, sourceFiles, lexResourceManager, errorLogger: ErrorLogger) =
        let tcConfig = TcConfig.Create(tcConfigB,validate=false)
        match sourceFiles with
        | [] -> istate
        | _ ->
          // use a set of source files as though they were command line inputs
          let sourceFiles = sourceFiles |> List.map (fun nm -> tcConfig.ResolveSourceFile(m, nm, tcConfig.implicitIncludeDir),m)

          // Close the #load graph on each file and gather the inputs from the scripts.
          let tcConfig = TcConfig.Create(tcConfigB,validate=false)

          let closure =
              LoadClosure.ComputeClosureOfScriptFiles(tcConfig,
                 sourceFiles, CodeContext.CompilationAndEvaluation,
                 lexResourceManager, fsiOptions.DependencyProvider)

          // Intent "[Loading %s]\n" (String.concat "\n     and " sourceFiles)
          fsiConsoleOutput.uprintf "[%s " (FSIstrings.SR.fsiLoadingFilesPrefixText())
          closure.Inputs  |> List.iteri (fun i input ->
              if i=0 then fsiConsoleOutput.uprintf  "%s" input.FileName
              else fsiConsoleOutput.uprintnf " %s %s" (FSIstrings.SR.fsiLoadingFilesPrefixText()) input.FileName)
          fsiConsoleOutput.uprintfn "]"

          closure.NoWarns |> Seq.map (fun (n,ms) -> ms |> Seq.map (fun m -> m,n)) |> Seq.concat |> Seq.iter tcConfigB.TurnWarningOff

          // Play errors and warnings from resolution
          closure.ResolutionDiagnostics |> List.iter diagnosticSink

          // Non-scripts will not have been parsed during #load closure so parse them now
          let sourceFiles,inputs =
              closure.Inputs
              |> List.map (fun input->
                    input.ParseDiagnostics |> List.iter diagnosticSink
                    input.MetaCommandDiagnostics |> List.iter diagnosticSink
                    let parsedInput =
                        match input.SyntaxTree with
                        | None -> ParseOneInputFile(tcConfig,lexResourceManager,["INTERACTIVE"],input.FileName,(true,false),errorLogger,(*retryLocked*)false)
                        | Some parseTree -> parseTree
                    input.FileName, parsedInput)
              |> List.unzip

          errorLogger.AbortOnError(fsiConsoleOutput);
          let istate = (istate, sourceFiles, inputs) |||> List.fold2 (fun istate sourceFile input -> fsiDynamicCompiler.ProcessMetaCommandsFromInputAsInteractiveCommands(ctok, istate, sourceFile, input))
          fsiDynamicCompiler.EvalParsedSourceFiles (ctok, errorLogger, istate, inputs)

    member _.GetBoundValues istate =
        let cenv = SymbolEnv(istate.tcGlobals, istate.tcState.Ccu, Some istate.tcState.CcuSig, istate.tcImports)
        [ for pair in istate.boundValues do
            let nm = pair.Key
            let v = pair.Value
            match tryGetGeneratedValue istate cenv v with
            | Some fsiValue ->
                yield FsiBoundValue(nm, fsiValue)
            | _ ->
                () ]

    member _.TryFindBoundValue(istate, nm) =
        match istate.boundValues.TryFind nm with
        | Some v ->
            let cenv = SymbolEnv(istate.tcGlobals, istate.tcState.Ccu, Some istate.tcState.CcuSig, istate.tcImports)
            match tryGetGeneratedValue istate cenv v with
            | Some fsiValue ->
                Some (FsiBoundValue(nm, fsiValue))
            | _ ->
                None
        | _ ->
            None

    member this.AddBoundValue (ctok, errorLogger: ErrorLogger, istate, name: string, value: obj) =
        try
            match value with
            | null -> nullArg "value"
            | _ -> ()

            if String.IsNullOrWhiteSpace name then
                invalidArg "name" "Name cannot be null or white-space."

            // Verify that the name is a valid identifier for a value.
            FSharpLexer.Tokenize(SourceText.ofString name,
                let mutable foundOne = false
                fun t ->
                    if not t.IsIdentifier || foundOne then
                        invalidArg "name" "Name is not a valid identifier."
                    foundOne <- true)

            if PrettyNaming.IsCompilerGeneratedName name then
                invalidArg "name" (FSComp.SR.lexhlpIdentifiersContainingAtSymbolReserved() |> snd)

            let istate, ty = importReflectionType istate (value.GetType())
            let amap = istate.tcImports.GetImportMap()

            let i = nextFragmentId()
            let prefix = mkFragmentPath i
            let prefixPath = pathOfLid prefix
            let qualifiedName = ComputeQualifiedNameOfFileFromUniquePath (rangeStdin,prefixPath)

            let tcConfig = TcConfig.Create(tcConfigB,validate=false)

            // Build a simple module with a single 'let' decl with a default value.
            let moduleOrNamespace, v, impl = mkBoundValueTypedImpl istate.tcGlobals range0 qualifiedName.Text name ty
            let tcEnvAtEndOfLastInput =
                CheckDeclarations.AddLocalSubModule tcGlobals amap range0 istate.tcState.TcEnvFromImpls moduleOrNamespace
                |> CheckExpressions.AddLocalVal tcGlobals TcResultsSink.NoSink range0 v

            // Generate IL for the given typled impl and create new interactive state.
            let ilxGenerator = istate.ilxGenerator
            let isIncrementalFragment = true
            let showTypes = false
            let declaredImpls = [impl]
            let codegenResults, optEnv, fragName = ProcessTypedImpl(errorLogger, istate.optEnv, istate.tcState, tcConfig, false, EmptyTopAttrs, prefix, isIncrementalFragment, declaredImpls, ilxGenerator)
            let istate, declaredImpls = ProcessCodegenResults(ctok, errorLogger, istate, optEnv, istate.tcState, tcConfig, prefix, showTypes, isIncrementalFragment, fragName, declaredImpls, ilxGenerator, codegenResults)
            let newState = { istate with tcState = istate.tcState.NextStateAfterIncrementalFragment tcEnvAtEndOfLastInput }

            // Force set the val with the given value obj.
            let ctxt = valuePrinter.GetEvaluationContext(newState.emEnv)
            ilxGenerator.ForceSetGeneratedValue(ctxt, v, value)

            processContents newState declaredImpls
        with
        | ex ->
            istate, CompletedWithReportedError(StopProcessingExn(Some ex))

    member _.GetInitialInteractiveState () =
        let tcConfig = TcConfig.Create(tcConfigB,validate=false)
        let optEnv0 = GetInitialOptimizationEnv (tcImports, tcGlobals)
        let emEnv = ILRuntimeWriter.emEnv0
        let tcEnv = GetInitialTcEnv (assemblyName, rangeStdin, tcConfig, tcImports, tcGlobals)
        let ccuName = assemblyName

        let tcState = GetInitialTcState (rangeStdin, ccuName, tcConfig, tcGlobals, tcImports, niceNameGen, tcEnv)

        let ilxGenerator = CreateIlxAssemblyGenerator (tcConfig, tcImports, tcGlobals, (LightweightTcValForUsingInBuildMethodCall tcGlobals), tcState.Ccu)
        {optEnv    = optEnv0
         emEnv     = emEnv
         tcGlobals = tcGlobals
         tcState   = tcState
         tcImports = tcImports
         ilxGenerator = ilxGenerator
         boundValues = NameMap.empty
         timing    = false
         debugBreak = false
        }

    member _.CurrentPartialAssemblySignature(istate) =
        FSharpAssemblySignature(istate.tcGlobals, istate.tcState.Ccu, istate.tcState.CcuSig, istate.tcImports, None, istate.tcState.CcuSig)

    member _.FormatValue(obj:obj, objTy) =
        valuePrinter.FormatValue(obj, objTy)

    member _.ValueBound = valueBoundEvent.Publish

//----------------------------------------------------------------------------
// ctrl-c handling
//----------------------------------------------------------------------------

type ControlEventHandler = delegate of int -> bool

// One strange case: when a TAE happens a strange thing
// occurs the next read from stdin always returns
// 0 bytes, i.e. the channel will look as if it has been closed.  So we check
// for this condition explicitly.  We also recreate the lexbuf whenever CtrlC kicks.

type internal FsiInterruptStdinState =
    | StdinEOFPermittedBecauseCtrlCRecentlyPressed
    | StdinNormal

type internal FsiInterruptControllerState =
    | InterruptCanRaiseException
    | InterruptIgnored

type internal FsiInterruptControllerKillerThreadRequest =
    | ThreadAbortRequest
    | NoRequest
    | ExitRequest
    | PrintInterruptRequest

type internal FsiInterruptController(fsiOptions: FsiCommandLineOptions, fsiConsoleOutput: FsiConsoleOutput) =

    let mutable stdinInterruptState = StdinNormal
    let CTRL_C = 0
    let mutable interruptAllowed = InterruptIgnored
    let mutable killThreadRequest = NoRequest

    let mutable ctrlEventHandlers = []: ControlEventHandler list
    let mutable ctrlEventActions  = []: (unit -> unit) list
    let mutable exitViaKillThread = false

    let mutable posixReinstate = (fun () -> ())

    member _.Exit() =
        if exitViaKillThread then
            killThreadRequest <- ExitRequest
            Thread.Sleep(1000)
        exit 0

    member _.FsiInterruptStdinState
        with get () = stdinInterruptState
        and set v = stdinInterruptState <- v

    member _.ClearInterruptRequest() = killThreadRequest <- NoRequest

    member _.InterruptAllowed
        with set v = interruptAllowed <- v

    member _.Interrupt() = ctrlEventActions |> List.iter (fun act -> act())

    member _.EventHandlers = ctrlEventHandlers

    member controller.InstallKillThread(threadToKill:Thread, pauseMilliseconds:int) =

        // Fsi Interrupt handler
        let raiseCtrlC() =
            use _scope = SetCurrentUICultureForThread fsiOptions.FsiLCID
            fprintf fsiConsoleOutput.Error "%s" (FSIstrings.SR.fsiInterrupt())

            stdinInterruptState <- StdinEOFPermittedBecauseCtrlCRecentlyPressed
            if (interruptAllowed = InterruptCanRaiseException) then
                killThreadRequest <- ThreadAbortRequest
                let killerThread =
                    new Thread(new ThreadStart(fun () ->
                        use _scope = SetCurrentUICultureForThread fsiOptions.FsiLCID
                        // sleep long enough to allow ControlEventHandler handler on main thread to return
                        // Also sleep to give computations a bit of time to terminate
                        Thread.Sleep(pauseMilliseconds)
                        if (killThreadRequest = ThreadAbortRequest) then
                            if progress then fsiConsoleOutput.uprintnfn "%s" (FSIstrings.SR.fsiAbortingMainThread())
                            killThreadRequest <- NoRequest
                            threadToKill.Abort()
                        ()),Name="ControlCAbortThread")
                killerThread.IsBackground <- true
                killerThread.Start()

        let fsiInterruptHandler (args:ConsoleCancelEventArgs) =
            args.Cancel <- true
            ctrlEventHandlers |> List.iter(fun handler -> handler.Invoke(CTRL_C) |> ignore)

        do Console.CancelKeyPress.Add(fsiInterruptHandler)

        // WINDOWS TECHNIQUE: .NET has more safe points, and you can do more when a safe point.
        // Hence we actually start up the killer thread within the handler.
        let ctrlEventHandler = new ControlEventHandler(fun i ->  if i = CTRL_C then (raiseCtrlC(); true) else false )
        ctrlEventHandlers <- ctrlEventHandler :: ctrlEventHandlers
        ctrlEventActions  <- raiseCtrlC       :: ctrlEventActions
        exitViaKillThread <- false // don't exit via kill thread

    member x.PosixInvoke(n:int) =
         // we run this code once with n = -1 to make sure it is JITted before execution begins
         // since we are not allowed to JIT a signal handler.  This also ensures the "PosixInvoke"
         // method is not eliminated by dead-code elimination
         if n >= 0 then
             posixReinstate()
             stdinInterruptState <- StdinEOFPermittedBecauseCtrlCRecentlyPressed
             killThreadRequest <- if (interruptAllowed = InterruptCanRaiseException) then ThreadAbortRequest else PrintInterruptRequest

//----------------------------------------------------------------------------
// assembly finder
//----------------------------------------------------------------------------

#nowarn "40"

// From http://msdn.microsoft.com/en-us/library/ff527268.aspx
// What the Event Handler Does
//
// The handler for the AssemblyResolve event receives the display name of the assembly to
// be loaded, in the ResolveEventArgs.Name property. If the handler does not recognize the
// assembly name, it returns null (Nothing in Visual Basic, nullptr in Visual C++).
//
// - If the handler recognizes the assembly name, it can load and return an assembly that
//   satisfies the request. The following list describes some sample scenarios.
//
// - If the handler knows the location of a version of the assembly, it can load the assembly by
//   using the Assembly.LoadFrom or Assembly.LoadFile method, and can return the loaded assembly if successful.
//
// - If the handler has access to a database of assemblies stored as byte arrays, it can load a byte array by
//   using one of the Assembly.Load method overloads that take a byte array.
//
// - The handler can generate a dynamic assembly and return it.
//
// It is the responsibility of the event handler to return a suitable assembly. The handler can parse the display
// name of the requested assembly by passing the ResolveEventArgs.Name property value to the AssemblyName(String)
// constructor. Beginning with the .NET Framework version 4, the handler can use the ResolveEventArgs.RequestingAssembly
// property to determine whether the current request is a dependency of another assembly. This information can help
// identify an assembly that will satisfy the dependency.
//
// The event handler can return a different version of the assembly than the version that was requested.
//
// In most cases, the assembly that is returned by the handler appears in the load context, regardless of the context
// the handler loads it into. For example, if the handler uses the Assembly.LoadFrom method to load an assembly into
// the load-from context, the assembly appears in the load context when the handler returns it. However, in the following
// case the assembly appears without context when the handler returns it:
//
// - The handler loads an assembly without context.
// - The ResolveEventArgs.RequestingAssembly property is not null.
// - The requesting assembly (that is, the assembly that is returned by the ResolveEventArgs.RequestingAssembly property)
//   was loaded without context.
//
// On the coreclr we add an UnmanagedDll Resoution handler to ensure that native dll's can be searched for,
// the desktop version of the Clr does not support this mechanism.
//
// For information about contexts, see the Assembly.LoadFrom(String) method overload.

module internal MagicAssemblyResolution =

    // See bug 5501 for details on decision to use UnsafeLoadFrom here.
    // Summary:
    //  It is an explicit user trust decision to load an assembly with #r. Scripts are not run automatically (for example, by double-clicking in explorer).
    //  We considered setting loadFromRemoteSources in fsi.exe.config but this would transitively confer unsafe loading to the code in the referenced
    //  assemblies. Better to let those assemblies decide for themselves which is safer.
    [<CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId="System.Reflection.Assembly.UnsafeLoadFrom")>]
    let private assemblyLoadFrom (path:string) = Assembly.UnsafeLoadFrom(path)

    let Install(tcConfigB, tcImports: TcImports, fsiDynamicCompiler: FsiDynamicCompiler, fsiConsoleOutput: FsiConsoleOutput) =

        let ResolveAssembly (ctok, m, tcConfigB, tcImports: TcImports, fsiDynamicCompiler: FsiDynamicCompiler, fsiConsoleOutput: FsiConsoleOutput, fullAssemName: string) =

           try
               // Grab the name of the assembly
               let tcConfig = TcConfig.Create(tcConfigB,validate=false)
               let simpleAssemName = fullAssemName.Split([| ',' |]).[0]
               if progress then fsiConsoleOutput.uprintfn "ATTEMPT MAGIC LOAD ON ASSEMBLY, simpleAssemName = %s" simpleAssemName // "Attempting to load a dynamically required assembly in response to an AssemblyResolve event by using known static assembly references..."

               // Special case: Mono Windows Forms attempts to load an assembly called something like "Windows.Forms.resources"
               // We can't resolve this, so don't try.
               // REVIEW: Suggest 4481, delete this special case.
               if (runningOnMono && simpleAssemName.EndsWith(".resources",StringComparison.OrdinalIgnoreCase)) ||
                  simpleAssemName.EndsWith(".XmlSerializers", StringComparison.OrdinalIgnoreCase) ||
                  (runningOnMono && simpleAssemName = "UIAutomationWinforms") then null
               else
               // Special case: Is this the global unique dynamic assembly for FSI code? In this case just
               // return the dynamic assembly itself.
               if fsiDynamicCompiler.DynamicAssemblyName = simpleAssemName then fsiDynamicCompiler.DynamicAssembly else

               // Otherwise continue
               let assemblyReferenceTextDll = (simpleAssemName + ".dll")
               let assemblyReferenceTextExe = (simpleAssemName + ".exe")
               let overallSearchResult =

                   // OK, try to resolve as an existing DLL in the resolved reference set.  This does unification by assembly name
                   // once an assembly has been referenced.
                   let searchResult = tcImports.TryFindExistingFullyQualifiedPathBySimpleAssemblyName (simpleAssemName)

                   match searchResult with
                   | Some r -> OkResult ([], Choice1Of2 r)
                   | _ ->

                   // OK, try to resolve as a .dll
                   let searchResult = tcImports.TryResolveAssemblyReference (ctok, AssemblyReference (m, assemblyReferenceTextDll, None), ResolveAssemblyReferenceMode.Speculative)

                   match searchResult with
                   | OkResult (warns,[r]) -> OkResult (warns, Choice1Of2 r.resolvedPath)
                   | _ ->

                   // OK, try to resolve as a .exe
                   let searchResult = tcImports.TryResolveAssemblyReference (ctok, AssemblyReference (m, assemblyReferenceTextExe, None), ResolveAssemblyReferenceMode.Speculative)

                   match searchResult with
                   | OkResult (warns, [r]) -> OkResult (warns, Choice1Of2 r.resolvedPath)
                   | _ ->

                   if progress then fsiConsoleOutput.uprintfn "ATTEMPT LOAD, assemblyReferenceTextDll = %s" assemblyReferenceTextDll
                   /// Take a look through the files quoted, perhaps with explicit paths
                   let searchResult =
                       (tcConfig.referencedDLLs
                            |> List.tryPick (fun assemblyReference ->
                             if progress then fsiConsoleOutput.uprintfn "ATTEMPT MAGIC LOAD ON FILE, referencedDLL = %s" assemblyReference.Text
                             if System.String.Compare(FileSystemUtils.fileNameOfPath assemblyReference.Text, assemblyReferenceTextDll,StringComparison.OrdinalIgnoreCase) = 0 ||
                                System.String.Compare(FileSystemUtils.fileNameOfPath assemblyReference.Text, assemblyReferenceTextExe,StringComparison.OrdinalIgnoreCase) = 0 then
                                 Some(tcImports.TryResolveAssemblyReference (ctok, assemblyReference, ResolveAssemblyReferenceMode.Speculative))
                             else None ))

                   match searchResult with
                   | Some (OkResult (warns,[r])) -> OkResult (warns, Choice1Of2 r.resolvedPath)
                   | _ ->

#if !NO_EXTENSIONTYPING
                   match tcImports.TryFindProviderGeneratedAssemblyByName(ctok, simpleAssemName) with
                   | Some(assembly) -> OkResult([],Choice2Of2 assembly)
                   | None ->
#endif

                   // As a last resort, try to find the reference without an extension
                   match tcImports.TryFindExistingFullyQualifiedPathByExactAssemblyRef(ILAssemblyRef.Create(simpleAssemName,None,None,false,None,None)) with
                   | Some(resolvedPath) ->
                       OkResult([],Choice1Of2 resolvedPath)
                   | None ->

                   ErrorResult([],Failure (FSIstrings.SR.fsiFailedToResolveAssembly(simpleAssemName)))

               match overallSearchResult with
               | ErrorResult _ -> null
               | OkResult _ ->
                   let res = CommitOperationResult overallSearchResult
                   match res with
                   | Choice1Of2 assemblyName ->
                       if simpleAssemName <> "Mono.Posix" then fsiConsoleOutput.uprintfn "%s" (FSIstrings.SR.fsiBindingSessionTo(assemblyName))
                       if isRunningOnCoreClr then
                         assemblyLoadFrom assemblyName
                       else
                         try
                           let an = AssemblyName.GetAssemblyName(assemblyName)
                           an.CodeBase <- assemblyName
                           Assembly.Load an
                         with | _ ->
                           assemblyLoadFrom assemblyName
                   | Choice2Of2 assembly ->
                       assembly

           with e ->
               stopProcessingRecovery e range0
               null

        let rangeStdin = rangeN Lexhelp.stdinMockFilename 0

        let resolveAssembly = new ResolveEventHandler(fun _ args ->
            // Explanation: our understanding is that magic assembly resolution happens
            // during compilation. So we recover the CompilationThreadToken here.
            let ctok = AssumeCompilationThreadWithoutEvidence ()
            ResolveAssembly (ctok, rangeStdin, tcConfigB, tcImports, fsiDynamicCompiler, fsiConsoleOutput, args.Name))

        AppDomain.CurrentDomain.add_AssemblyResolve(resolveAssembly)

        { new System.IDisposable with
            member x.Dispose() =
                AppDomain.CurrentDomain.remove_AssemblyResolve(resolveAssembly)
        }

//----------------------------------------------------------------------------
// Reading stdin
//----------------------------------------------------------------------------

type internal FsiStdinLexerProvider
                          (tcConfigB, fsiStdinSyphon,
                           fsiConsoleInput : FsiConsoleInput,
                           fsiConsoleOutput : FsiConsoleOutput,
                           fsiOptions : FsiCommandLineOptions,
                           lexResourceManager : LexResourceManager) =

    // #light is the default for FSI
    let interactiveInputLightSyntaxStatus =
        let initialLightSyntaxStatus = tcConfigB.light <> Some false
        LightSyntaxStatus (initialLightSyntaxStatus, false (* no warnings *))

    let isFeatureSupported featureId = tcConfigB.langVersion.SupportsFeature featureId
    let checkLanguageFeatureErrorRecover = ErrorLogger.checkLanguageFeatureErrorRecover tcConfigB.langVersion

    let LexbufFromLineReader (fsiStdinSyphon: FsiStdinSyphon) readF =
        UnicodeLexing.FunctionAsLexbuf
          (true, isFeatureSupported, checkLanguageFeatureErrorRecover, (fun (buf: char[], start, len) ->
            //fprintf fsiConsoleOutput.Out "Calling ReadLine\n"
            let inputOption = try Some(readF()) with :? EndOfStreamException -> None
            inputOption |> Option.iter (fun t -> fsiStdinSyphon.Add (t + "\n"))
            match inputOption with
            |  Some(null) | None ->
                 if progress then fprintfn fsiConsoleOutput.Out "End of file from TextReader.ReadLine"
                 0
            | Some (input:string) ->
                let input  = input + "\n"
                let ninput = input.Length
                if ninput > len then fprintf fsiConsoleOutput.Error  "%s" (FSIstrings.SR.fsiLineTooLong())
                let ntrimmed = min len ninput
                for i = 0 to ntrimmed-1 do
                    buf.[i+start] <- input.[i]
                ntrimmed
          ))

    //----------------------------------------------------------------------------
    // Reading stdin as a lex stream
    //----------------------------------------------------------------------------

    let removeZeroCharsFromString (str:string) = (* bug:/4466 *)
        if str<>null && str.Contains("\000") then
          System.String(str |> Seq.filter (fun c -> c<>'\000') |> Seq.toArray)
        else
          str

    let CreateLexerForLexBuffer (sourceFileName, lexbuf, errorLogger) =

        Lexhelp.resetLexbufPos sourceFileName lexbuf
        let skip = true  // don't report whitespace from lexer
        let defines = "INTERACTIVE"::tcConfigB.conditionalCompilationDefines
        let lexargs = mkLexargs (defines, interactiveInputLightSyntaxStatus, lexResourceManager, [], errorLogger, PathMap.empty)
        let tokenizer = LexFilter.LexFilter(interactiveInputLightSyntaxStatus, tcConfigB.compilingFslib, Lexer.token lexargs skip, lexbuf)
        tokenizer

    let isFeatureSupported featureId = tcConfigB.langVersion.SupportsFeature featureId
    let checkLanguageFeatureErrorRecover = ErrorLogger.checkLanguageFeatureErrorRecover tcConfigB.langVersion

    // Create a new lexer to read stdin
    member _.CreateStdinLexer (errorLogger) =
        let lexbuf =
            match fsiConsoleInput.TryGetConsole() with
            | Some console when fsiOptions.EnableConsoleKeyProcessing && not fsiOptions.UseServerPrompt ->
                LexbufFromLineReader fsiStdinSyphon (fun () ->
                    match fsiConsoleInput.TryGetFirstLine() with
                    | Some firstLine -> firstLine
                    | None -> console())
            | _ ->
                LexbufFromLineReader fsiStdinSyphon (fun () -> fsiConsoleInput.In.ReadLine() |> removeZeroCharsFromString)

        fsiStdinSyphon.Reset()
        CreateLexerForLexBuffer (Lexhelp.stdinMockFilename, lexbuf, errorLogger)

    // Create a new lexer to read an "included" script file
    member _.CreateIncludedScriptLexer (sourceFileName, reader, errorLogger) =
        let lexbuf = UnicodeLexing.StreamReaderAsLexbuf(true, isFeatureSupported, checkLanguageFeatureErrorRecover, reader)
        CreateLexerForLexBuffer (sourceFileName, lexbuf, errorLogger)

    // Create a new lexer to read a string
    member this.CreateStringLexer (sourceFileName, source, errorLogger) =
        let lexbuf = UnicodeLexing.StringAsLexbuf(true, isFeatureSupported, checkLanguageFeatureErrorRecover, source)
        CreateLexerForLexBuffer (sourceFileName, lexbuf, errorLogger)

    member _.ConsoleInput = fsiConsoleInput

    member _.CreateBufferLexer (sourceFileName, lexbuf, errorLogger) = CreateLexerForLexBuffer (sourceFileName, lexbuf, errorLogger)


//----------------------------------------------------------------------------
// Process one parsed interaction.  This runs on the GUI thread.
// It might be simpler if it ran on the parser thread.
//----------------------------------------------------------------------------

type internal FsiInteractionProcessor
                            (fsi: FsiEvaluationSessionHostConfig,
                             tcConfigB,
                             fsiOptions: FsiCommandLineOptions,
                             fsiDynamicCompiler: FsiDynamicCompiler,
                             fsiConsolePrompt : FsiConsolePrompt,
                             fsiConsoleOutput : FsiConsoleOutput,
                             fsiInterruptController : FsiInterruptController,
                             fsiStdinLexerProvider : FsiStdinLexerProvider,
                             lexResourceManager : LexResourceManager,
                             initialInteractiveState) =

    let referencedAssemblies = Dictionary<string, DateTime>()

    let mutable currState = initialInteractiveState
    let event = Control.Event<unit>()
    let setCurrState s = currState <- s; event.Trigger()

    let runCodeOnEventLoop errorLogger f istate =
        try
            fsi.EventLoopInvoke (fun () ->

                // Explanation: We assume the event loop on the 'fsi' object correctly transfers control to
                // a unique compilation thread.
                let ctok = AssumeCompilationThreadWithoutEvidence()

                // FSI error logging on switched to thread
                InstallErrorLoggingOnThisThread errorLogger
                use _scope = SetCurrentUICultureForThread fsiOptions.FsiLCID
                f ctok istate)
        with _ ->
            (istate,Completed None)

    let InteractiveCatch (errorLogger: ErrorLogger) (f:_ -> _ * FsiInteractionStepStatus)  istate =
        try
            // reset error count
            match errorLogger with
            | :? ErrorLoggerThatStopsOnFirstError as errorLogger ->  errorLogger.ResetErrorCount()
            | _ -> ()

            f istate
        with  e ->
            stopProcessingRecovery e range0
            istate, CompletedWithReportedError e

    let isFeatureSupported featureId = tcConfigB.langVersion.SupportsFeature featureId
    let checkLanguageFeatureErrorRecover = ErrorLogger.checkLanguageFeatureErrorRecover tcConfigB.langVersion

    let rangeStdin = rangeN Lexhelp.stdinMockFilename 0

    let ChangeDirectory (path:string) m =
        let tcConfig = TcConfig.Create(tcConfigB,validate=false)
        let path = tcConfig.MakePathAbsolute path
        if FileSystem.DirectoryExistsShim(path) then
            tcConfigB.implicitIncludeDir <- path
        else
            error(Error(FSIstrings.SR.fsiDirectoryDoesNotExist(path),m))


    /// Parse one interaction. Called on the parser thread.
    let ParseInteraction (tokenizer:LexFilter.LexFilter) =
        let mutable lastToken = Parser.ELSE // Any token besides SEMICOLON_SEMICOLON will do for initial value
        try
            if progress then fprintfn fsiConsoleOutput.Out "In ParseInteraction..."

            let input =
                Lexhelp.reusingLexbufForParsing tokenizer.LexBuffer (fun () ->
                    let lexerWhichSavesLastToken _lexbuf =
                        let tok = tokenizer.GetToken()
                        lastToken <- tok
                        tok
                    Parser.interaction lexerWhichSavesLastToken tokenizer.LexBuffer)
            Some input
        with e ->
            // On error, consume tokens until to ;; or EOF.
            // Caveat: Unless the error parse ended on ;; - so check the lastToken returned by the lexer function.
            // Caveat: What if this was a look-ahead? That's fine! Since we need to skip to the ;; anyway.
            if (match lastToken with Parser.SEMICOLON_SEMICOLON -> false | _ -> true) then
                let mutable tok = Parser.ELSE (* <-- any token <> SEMICOLON_SEMICOLON will do *)
                while (match tok with  Parser.SEMICOLON_SEMICOLON -> false | _ -> true)
                      && not tokenizer.LexBuffer.IsPastEndOfStream do
                    tok <- tokenizer.GetToken()

            stopProcessingRecovery e range0
            None

    /// Execute a single parsed interaction. Called on the GUI/execute/main thread.
    let ExecInteraction (ctok, tcConfig:TcConfig, istate, action:ParsedScriptInteraction, errorLogger: ErrorLogger) =
        let packageManagerDirective directive path m =
            let dm = fsiOptions.DependencyProvider.TryFindDependencyManagerInPath(tcConfigB.compilerToolPaths, getOutputDir tcConfigB, reportError m, path)
            match dm with
            | null, null ->
                // error already reported
                istate, CompletedWithAlreadyReportedError

            | _, dependencyManager when not(isNull dependencyManager) ->
               if tcConfig.langVersion.SupportsFeature(LanguageFeature.PackageManagement) then
                   fsiDynamicCompiler.EvalDependencyManagerTextFragment(dependencyManager, directive, m, path)
                   istate, Completed None
               else
                   errorR(Error(FSComp.SR.packageManagementRequiresVFive(), m))
                   istate, Completed None

            | _, _ when directive = Directive.Include ->
                errorR(Error(FSComp.SR.poundiNotSupportedByRegisteredDependencyManagers(), m))
                istate,Completed None

            | p, _ ->
                let path =
                    if String.IsNullOrWhiteSpace(p) then ""
                    else p
                let resolutions,istate = fsiDynamicCompiler.EvalRequireReference(ctok, istate, m, path)
                resolutions |> List.iter (fun ar ->
                    let format =
                        if tcConfig.shadowCopyReferences then
                            let resolvedPath = ar.resolvedPath.ToUpperInvariant()
                            let fileTime = FileSystem.GetLastWriteTimeShim(resolvedPath)
                            match referencedAssemblies.TryGetValue resolvedPath with
                            | false, _ ->
                                referencedAssemblies.Add(resolvedPath, fileTime)
                                FSIstrings.SR.fsiDidAHashr(ar.resolvedPath)
                            | true, time when time <> fileTime ->
                                FSIstrings.SR.fsiDidAHashrWithStaleWarning(ar.resolvedPath)
                            | _ ->
                                FSIstrings.SR.fsiDidAHashr(ar.resolvedPath)
                        else
                            FSIstrings.SR.fsiDidAHashrWithLockWarning(ar.resolvedPath)
                    fsiConsoleOutput.uprintnfnn "%s" format)
                istate,Completed None

        istate |> InteractiveCatch errorLogger (fun istate ->
            match action with
            | ParsedScriptInteraction.Definitions ([], _) ->
                let istate = fsiDynamicCompiler.CommitDependencyManagerText(ctok, istate, lexResourceManager, errorLogger)
                istate,Completed None

            | ParsedScriptInteraction.Definitions ([SynModuleDecl.DoExpr(_, expr, _)], _) ->
                let istate = fsiDynamicCompiler.CommitDependencyManagerText(ctok, istate, lexResourceManager, errorLogger)
                fsiDynamicCompiler.EvalParsedExpression(ctok, errorLogger, istate, expr)

            | ParsedScriptInteraction.Definitions (defs,_) ->
                let istate = fsiDynamicCompiler.CommitDependencyManagerText(ctok, istate, lexResourceManager, errorLogger)
                fsiDynamicCompiler.EvalParsedDefinitions (ctok, errorLogger, istate, true, false, defs)

            | ParsedScriptInteraction.HashDirective (ParsedHashDirective("load", ParsedHashDirectiveArguments sourceFiles, m), _) ->
                let istate = fsiDynamicCompiler.CommitDependencyManagerText(ctok, istate, lexResourceManager, errorLogger)
                fsiDynamicCompiler.EvalSourceFiles (ctok, istate, m, sourceFiles, lexResourceManager, errorLogger),Completed None

            | ParsedScriptInteraction.HashDirective (ParsedHashDirective(("reference" | "r"), ParsedHashDirectiveArguments [path], m), _) ->
                packageManagerDirective Directive.Resolution path m

            | ParsedScriptInteraction.HashDirective (ParsedHashDirective("i", ParsedHashDirectiveArguments [path], m), _) ->
                packageManagerDirective Directive.Include path m

            | ParsedScriptInteraction.HashDirective (ParsedHashDirective("I", ParsedHashDirectiveArguments [path], m), _) ->
                tcConfigB.AddIncludePath (m, path, tcConfig.implicitIncludeDir)
                fsiConsoleOutput.uprintnfnn "%s" (FSIstrings.SR.fsiDidAHashI(tcConfig.MakePathAbsolute path))
                istate, Completed None

            | ParsedScriptInteraction.HashDirective (ParsedHashDirective("cd", ParsedHashDirectiveArguments [path], m), _) ->
                ChangeDirectory path m
                istate, Completed None

            | ParsedScriptInteraction.HashDirective (ParsedHashDirective("silentCd", ParsedHashDirectiveArguments [path], m), _) ->
                ChangeDirectory path m
                fsiConsolePrompt.SkipNext() (* "silent" directive *)
                istate, Completed None

            | ParsedScriptInteraction.HashDirective (ParsedHashDirective("dbgbreak", [], _), _) ->
                {istate with debugBreak = true}, Completed None

            | ParsedScriptInteraction.HashDirective (ParsedHashDirective("time", [], _), _) ->
                if istate.timing then
                    fsiConsoleOutput.uprintnfnn "%s" (FSIstrings.SR.fsiTurnedTimingOff())
                else
                    fsiConsoleOutput.uprintnfnn "%s" (FSIstrings.SR.fsiTurnedTimingOn())
                {istate with timing = not istate.timing}, Completed None

            | ParsedScriptInteraction.HashDirective (ParsedHashDirective("time", ParsedHashDirectiveArguments [("on" | "off") as v], _), _) ->
                if v <> "on" then
                    fsiConsoleOutput.uprintnfnn "%s" (FSIstrings.SR.fsiTurnedTimingOff())
                else
                    fsiConsoleOutput.uprintnfnn "%s" (FSIstrings.SR.fsiTurnedTimingOn())
                {istate with timing = (v = "on")}, Completed None

            | ParsedScriptInteraction.HashDirective (ParsedHashDirective("nowarn", ParsedHashDirectiveArguments numbers, m), _) ->
                List.iter (fun (d:string) -> tcConfigB.TurnWarningOff(m, d)) numbers
                istate, Completed None

            | ParsedScriptInteraction.HashDirective (ParsedHashDirective("terms", [], _), _) ->
                tcConfigB.showTerms <- not tcConfig.showTerms
                istate, Completed None

            | ParsedScriptInteraction.HashDirective (ParsedHashDirective("types", [], _), _) ->
                fsiOptions.ShowTypes <- not fsiOptions.ShowTypes
                istate, Completed None

    #if DEBUG
            | ParsedScriptInteraction.HashDirective (ParsedHashDirective("ilcode", [], _m), _) ->
                fsiOptions.ShowILCode <- not fsiOptions.ShowILCode;
                istate, Completed None

            | ParsedScriptInteraction.HashDirective (ParsedHashDirective("info", [], _m), _) ->
                PrintOptionInfo tcConfigB
                istate, Completed None
    #endif

            | ParsedScriptInteraction.HashDirective (ParsedHashDirective(("q" | "quit"), [], _), _) ->
                fsiInterruptController.Exit()

            | ParsedScriptInteraction.HashDirective (ParsedHashDirective("help", [], m), _) ->
                fsiOptions.ShowHelp(m)
                istate, Completed None

            | ParsedScriptInteraction.HashDirective (ParsedHashDirective(c, ParsedHashDirectiveArguments arg, m), _) ->
                warning(Error((FSComp.SR.fsiInvalidDirective(c, String.concat " " arg)), m))
                istate, Completed None
        )

    /// Execute a single parsed interaction which may contain multiple items to be executed
    /// independently, because some are #directives. Called on the GUI/execute/main thread.
    ///
    /// #directive comes through with other definitions as a SynModuleDecl.HashDirective.
    /// We split these out for individual processing.
    let rec execParsedInteractions (ctok, tcConfig, istate, action, errorLogger: ErrorLogger, lastResult:option<FsiInteractionStepStatus>, cancellationToken: CancellationToken)  =
        cancellationToken.ThrowIfCancellationRequested()
        let action,nextAction,istate =
            match action with
            | None                                      -> None,None,istate
            | Some (ParsedScriptInteraction.HashDirective _)                            -> action,None,istate
            | Some (ParsedScriptInteraction.Definitions ([],_))                      -> None,None,istate
            | Some (ParsedScriptInteraction.Definitions (SynModuleDecl.HashDirective(hash,mh) :: defs,m)) ->
                Some (ParsedScriptInteraction.HashDirective(hash,mh)),Some (ParsedScriptInteraction.Definitions(defs,m)),istate

            | Some (ParsedScriptInteraction.Definitions (defs,m))                    ->
                let isDefHash = function SynModuleDecl.HashDirective(_,_) -> true | _ -> false
                let isBreakable def =
                    // only add automatic debugger breaks before 'let' or 'do' expressions with sequence points
                    match def with
                    | SynModuleDecl.DoExpr (DebugPointAtBinding.Yes _, _, _)
                    | SynModuleDecl.Let (_, SynBinding(_, _, _, _, _, _, _, _,_,_,_, DebugPointAtBinding.Yes _) :: _, _) -> true
                    | _ -> false
                let defsA = Seq.takeWhile (isDefHash >> not) defs |> Seq.toList
                let defsB = Seq.skipWhile (isDefHash >> not) defs |> Seq.toList

                // If user is debugging their script interactively, inject call
                // to Debugger.Break() at the first "breakable" line.
                // Update istate so that more Break() calls aren't injected when recursing
                let defsA,istate =
                    if istate.debugBreak then
                        let preBreak = Seq.takeWhile (isBreakable >> not) defsA |> Seq.toList
                        let postBreak = Seq.skipWhile (isBreakable >> not) defsA |> Seq.toList
                        match postBreak with
                        | h :: _ -> preBreak @ (fsiDynamicCompiler.CreateDebuggerBreak(h.Range) :: postBreak), { istate with debugBreak = false }
                        | _ -> defsA, istate
                    else defsA,istate

                // When the last declaration has a shape of DoExp (i.e., non-binding),
                // transform it to a shape of "let it = <exp>", so we can refer it.
                let defsA =
                    if not (isNil defsB) then defsA else
                    match defsA with
                    | [] -> defsA
                    | [_] -> defsA
                    | _ ->
                        match List.rev defsA with
                        | SynModuleDecl.DoExpr(_,exp,_) :: rest -> (rest |> List.rev) @ (fsiDynamicCompiler.BuildItBinding exp)
                        | _ -> defsA

                Some (ParsedScriptInteraction.Definitions(defsA,m)),Some (ParsedScriptInteraction.Definitions(defsB,m)),istate

        match action, lastResult with
          | None, Some prev -> assert(nextAction.IsNone); istate, prev
          | None,_ -> assert(nextAction.IsNone); istate, Completed None
          | Some action, _ ->
              let istate,cont = ExecInteraction (ctok, tcConfig, istate, action, errorLogger)
              match cont with
                | Completed _                  -> execParsedInteractions (ctok, tcConfig, istate, nextAction, errorLogger, Some cont, cancellationToken)
                | CompletedWithReportedError e -> istate,CompletedWithReportedError e             (* drop nextAction on error *)
                | CompletedWithAlreadyReportedError -> istate,CompletedWithAlreadyReportedError   (* drop nextAction on error *)
                | EndOfFile                    -> istate,defaultArg lastResult (Completed None)   (* drop nextAction on EOF *)
                | CtrlC                        -> istate,CtrlC                                    (* drop nextAction on CtrlC *)

    /// Execute a single parsed interaction which may contain multiple items to be executed
    /// independently
    let executeParsedInteractions (ctok, tcConfig, istate, action, errorLogger: ErrorLogger, lastResult:option<FsiInteractionStepStatus>, cancellationToken: CancellationToken)  =
        let istate, completed = execParsedInteractions (ctok, tcConfig, istate, action, errorLogger, lastResult, cancellationToken)
        match completed with
        | Completed _  ->
            let istate = fsiDynamicCompiler.CommitDependencyManagerText(ctok, istate, lexResourceManager, errorLogger)
            istate, completed
        | _ -> istate, completed

    /// Execute a single parsed interaction on the parser/execute thread.
    let mainThreadProcessAction ctok action istate =
        try
            let tcConfig = TcConfig.Create(tcConfigB,validate=false)
            if progress then fprintfn fsiConsoleOutput.Out "In mainThreadProcessAction...";
            fsiInterruptController.InterruptAllowed <- InterruptCanRaiseException;
            let res = action ctok tcConfig istate
            fsiInterruptController.ClearInterruptRequest()
            fsiInterruptController.InterruptAllowed <- InterruptIgnored;
            res
        with
        | :? ThreadAbortException ->
           fsiInterruptController.ClearInterruptRequest()
           fsiInterruptController.InterruptAllowed <- InterruptIgnored;
           (try Thread.ResetAbort() with _ -> ());
           (istate,CtrlC)
        |  e ->
           fsiInterruptController.ClearInterruptRequest()
           fsiInterruptController.InterruptAllowed <- InterruptIgnored;
           stopProcessingRecovery e range0;
           istate, CompletedWithReportedError e

    let mainThreadProcessParsedInteractions ctok errorLogger (action, istate) cancellationToken =
      istate |> mainThreadProcessAction ctok (fun ctok tcConfig istate ->
        executeParsedInteractions (ctok, tcConfig, istate, action, errorLogger, None, cancellationToken))

    let parseExpression (tokenizer:LexFilter.LexFilter) =
        reusingLexbufForParsing tokenizer.LexBuffer (fun () ->
            Parser.typedSeqExprEOF (fun _ -> tokenizer.GetToken()) tokenizer.LexBuffer)

    let mainThreadProcessParsedExpression ctok errorLogger (expr, istate) =
      istate |> InteractiveCatch errorLogger (fun istate ->
        istate |> mainThreadProcessAction ctok (fun ctok _tcConfig istate ->
          fsiDynamicCompiler.EvalParsedExpression(ctok, errorLogger, istate, expr)  ))

    let commitResult (istate, result) =
        match result with
        | FsiInteractionStepStatus.CtrlC -> Choice2Of2 (Some (OperationCanceledException() :> exn))
        | FsiInteractionStepStatus.EndOfFile -> Choice2Of2 (Some (System.Exception "End of input"))
        | FsiInteractionStepStatus.Completed res ->
            setCurrState istate
            Choice1Of2 res
        | FsiInteractionStepStatus.CompletedWithReportedError (StopProcessingExn userExnOpt) ->
            Choice2Of2 userExnOpt
        | FsiInteractionStepStatus.CompletedWithReportedError _
        | FsiInteractionStepStatus.CompletedWithAlreadyReportedError ->
            Choice2Of2 None

    /// Parse then process one parsed interaction.
    ///
    /// During normal execution, this initially runs on the parser
    /// thread, then calls runCodeOnMainThread when it has completed
    /// parsing and needs to typecheck and execute a definition. This blocks the parser thread
    /// until execution has competed on the GUI thread.
    ///
    /// During processing of startup scripts, this runs on the main thread.
    ///
    /// This is blocking: it reads until one chunk of input have been received, unless IsPastEndOfStream is true
    member _.ParseAndExecOneSetOfInteractionsFromLexbuf (runCodeOnMainThread, istate:FsiDynamicCompilerState, tokenizer:LexFilter.LexFilter, errorLogger, ?cancellationToken: CancellationToken) =
        let cancellationToken = defaultArg cancellationToken CancellationToken.None
        if tokenizer.LexBuffer.IsPastEndOfStream then
            let stepStatus =
                if fsiInterruptController.FsiInterruptStdinState = StdinEOFPermittedBecauseCtrlCRecentlyPressed then
                    fsiInterruptController.FsiInterruptStdinState <- StdinNormal;
                    CtrlC
                else
                    EndOfFile
            istate,stepStatus

        else

            fsiConsolePrompt.Print();
            istate |> InteractiveCatch errorLogger (fun istate ->
                if progress then fprintfn fsiConsoleOutput.Out "entering ParseInteraction...";

                // Parse the interaction. When FSI.EXE is waiting for input from the console the
                // parser thread is blocked somewhere deep this call.
                let action  = ParseInteraction tokenizer

                if progress then fprintfn fsiConsoleOutput.Out "returned from ParseInteraction...calling runCodeOnMainThread...";

                // After we've unblocked and got something to run we switch
                // over to the run-thread (e.g. the GUI thread)
                let res = istate  |> runCodeOnMainThread (fun ctok istate -> mainThreadProcessParsedInteractions ctok errorLogger (action, istate) cancellationToken)

                if progress then fprintfn fsiConsoleOutput.Out "Just called runCodeOnMainThread, res = %O..." res;
                res)

    member _.CurrentState = currState

    /// Perform an "include" on a script file (i.e. a script file specified on the command line)
    member processor.EvalIncludedScript (ctok, istate, sourceFile, m, errorLogger) =
        let tcConfig = TcConfig.Create(tcConfigB, validate=false)
        // Resolve the filename to an absolute filename
        let sourceFile = tcConfig.ResolveSourceFile(m, sourceFile, tcConfig.implicitIncludeDir)
        // During the processing of the file, further filenames are
        // resolved relative to the home directory of the loaded file.
        WithImplicitHome (tcConfigB, directoryName sourceFile)  (fun () ->
              // An included script file may contain maybe several interaction blocks.
              // We repeatedly parse and process these, until an error occurs.

                use fileStream = FileSystem.OpenFileForReadShim(sourceFile)
                use reader = fileStream.GetReader(tcConfigB.inputCodePage, false)

                let tokenizer = fsiStdinLexerProvider.CreateIncludedScriptLexer (sourceFile, reader, errorLogger)
                let rec run istate =
                    let istate,cont = processor.ParseAndExecOneSetOfInteractionsFromLexbuf ((fun f istate -> f ctok istate), istate, tokenizer, errorLogger)
                    match cont with Completed _ -> run istate | _ -> istate,cont

                let istate,cont = run istate

                match cont with
                | Completed _ -> failwith "EvalIncludedScript: Completed expected to have relooped"
                | CompletedWithAlreadyReportedError -> istate,CompletedWithAlreadyReportedError
                | CompletedWithReportedError e -> istate,CompletedWithReportedError e
                | EndOfFile -> istate,Completed None// here file-EOF is normal, continue required
                | CtrlC     -> istate,CtrlC
          )


    /// Load the source files, one by one. Called on the main thread.
    member processor.EvalIncludedScripts (ctok, istate, sourceFiles, errorLogger) =
      match sourceFiles with
        | [] -> istate
        | sourceFile :: moreSourceFiles ->
            // Catch errors on a per-file basis, so results/bindings from pre-error files can be kept.
            let istate,cont = InteractiveCatch errorLogger (fun istate -> processor.EvalIncludedScript (ctok, istate, sourceFile, rangeStdin, errorLogger)) istate
            match cont with
              | Completed _                -> processor.EvalIncludedScripts (ctok, istate, moreSourceFiles, errorLogger)
              | CompletedWithAlreadyReportedError -> istate // do not process any more files
              | CompletedWithReportedError _ -> istate // do not process any more files
              | CtrlC                      -> istate // do not process any more files
              | EndOfFile                  -> assert false; istate // This is unexpected. EndOfFile is replaced by Completed in the called function


    member processor.LoadInitialFiles (ctok, errorLogger) =
        /// Consume initial source files in chunks of scripts or non-scripts
        let rec consume istate sourceFiles =
            match sourceFiles with
            | [] -> istate
            | (_,isScript1) :: _ ->
                let sourceFiles,rest = List.takeUntil (fun (_,isScript2) -> isScript1 <> isScript2) sourceFiles
                let sourceFiles = List.map fst sourceFiles
                let istate =
                    if isScript1 then
                        processor.EvalIncludedScripts (ctok, istate, sourceFiles, errorLogger)
                    else
                        istate |> InteractiveCatch errorLogger (fun istate -> fsiDynamicCompiler.EvalSourceFiles(ctok, istate, rangeStdin, sourceFiles, lexResourceManager, errorLogger), Completed None) |> fst
                consume istate rest

        setCurrState (consume currState fsiOptions.SourceFiles)

        if not (List.isEmpty fsiOptions.SourceFiles) then
            fsiConsolePrompt.PrintAhead(); // Seems required. I expected this could be deleted. Why not?

    /// Send a dummy interaction through F# Interactive, to ensure all the most common code generation paths are
    /// JIT'ed and ready for use.
    member _.LoadDummyInteraction(ctok, errorLogger) =
        setCurrState (currState |> InteractiveCatch errorLogger (fun istate ->  fsiDynamicCompiler.EvalParsedDefinitions (ctok, errorLogger, istate, true, false, []) |> fst, Completed None) |> fst)

    member _.EvalInteraction(ctok, sourceText, scriptFileName, errorLogger, ?cancellationToken) =
        let cancellationToken = defaultArg cancellationToken CancellationToken.None
        use _unwind1 = ErrorLogger.PushThreadBuildPhaseUntilUnwind(ErrorLogger.BuildPhase.Interactive)
        use _unwind2 = ErrorLogger.PushErrorLoggerPhaseUntilUnwind(fun _ -> errorLogger)
        use _scope = SetCurrentUICultureForThread fsiOptions.FsiLCID
        let lexbuf = UnicodeLexing.StringAsLexbuf(true, isFeatureSupported, checkLanguageFeatureErrorRecover, sourceText)
        let tokenizer = fsiStdinLexerProvider.CreateBufferLexer(scriptFileName, lexbuf, errorLogger)
        currState
        |> InteractiveCatch errorLogger (fun istate ->
            let expr = ParseInteraction tokenizer
            mainThreadProcessParsedInteractions ctok errorLogger (expr, istate) cancellationToken)
        |> commitResult

    member this.EvalScript (ctok, scriptPath, errorLogger) =
        // Todo: this runs the script as expected but errors are displayed one line to far in debugger
        let sourceText = sprintf "#load @\"%s\" " scriptPath
        this.EvalInteraction (ctok, sourceText, scriptPath, errorLogger)

    member _.EvalExpression (ctok, sourceText, scriptFileName, errorLogger) =
        use _unwind1 = ErrorLogger.PushThreadBuildPhaseUntilUnwind(ErrorLogger.BuildPhase.Interactive)
        use _unwind2 = ErrorLogger.PushErrorLoggerPhaseUntilUnwind(fun _ -> errorLogger)
        use _scope = SetCurrentUICultureForThread fsiOptions.FsiLCID
        let lexbuf = UnicodeLexing.StringAsLexbuf(true, isFeatureSupported, checkLanguageFeatureErrorRecover, sourceText)
        let tokenizer = fsiStdinLexerProvider.CreateBufferLexer(scriptFileName, lexbuf, errorLogger)
        currState
        |> InteractiveCatch errorLogger (fun istate ->
            let expr = parseExpression tokenizer
            let m = expr.Range
            // Make this into "(); expr" to suppress generalization and compilation-as-function
            let exprWithSeq = SynExpr.Sequential (DebugPointAtSequential.ExprOnly, true, SynExpr.Const (SynConst.Unit,m.StartRange), expr, m)
            mainThreadProcessParsedExpression ctok errorLogger (exprWithSeq, istate))
        |> commitResult

    member _.AddBoundValue(ctok, errorLogger, name, value: obj) =
        currState
        |> InteractiveCatch errorLogger (fun istate ->
            fsiDynamicCompiler.AddBoundValue(ctok, errorLogger, istate, name, value))
        |> commitResult

    member _.PartialAssemblySignatureUpdated = event.Publish

    /// Start the background thread used to read the input reader and/or console
    ///
    /// This is the main stdin loop, running on the stdinReaderThread.
    ///
    // We run the actual computations for each action on the main GUI thread by using
    // mainForm.Invoke to pipe a message back through the form's main event loop. (The message
    // is a delegate to execute on the main Thread)
    //
    member processor.StartStdinReadAndProcessThread (errorLogger) =

      if progress then fprintfn fsiConsoleOutput.Out "creating stdinReaderThread";

      let stdinReaderThread =
        new Thread(new ThreadStart(fun () ->
            InstallErrorLoggingOnThisThread errorLogger // FSI error logging on stdinReaderThread, e.g. parse errors.
            use _scope = SetCurrentUICultureForThread fsiOptions.FsiLCID
            try
                try
                  let initialTokenizer = fsiStdinLexerProvider.CreateStdinLexer(errorLogger)
                  if progress then fprintfn fsiConsoleOutput.Out "READER: stdin thread started...";

                  // Delay until we've peeked the input or read the entire first line
                  fsiStdinLexerProvider.ConsoleInput.WaitForInitialConsoleInput()

                  if progress then fprintfn fsiConsoleOutput.Out "READER: stdin thread got first line...";

                  let runCodeOnMainThread = runCodeOnEventLoop errorLogger

                  // Keep going until EndOfFile on the inReader or console
                  let rec loop currTokenizer =

                      let istateNew,contNew =
                          processor.ParseAndExecOneSetOfInteractionsFromLexbuf (runCodeOnMainThread, currState, currTokenizer, errorLogger)

                      setCurrState istateNew

                      match contNew with
                      | EndOfFile -> ()
                      | CtrlC -> loop (fsiStdinLexerProvider.CreateStdinLexer(errorLogger))   // After each interrupt, restart to a brand new tokenizer
                      | CompletedWithAlreadyReportedError
                      | CompletedWithReportedError _
                      | Completed _ -> loop currTokenizer

                  loop initialTokenizer


                  if progress then fprintfn fsiConsoleOutput.Out "- READER: Exiting stdinReaderThread";

                with e -> stopProcessingRecovery e range0;

            finally
                if progress then fprintfn fsiConsoleOutput.Out "- READER: Exiting process because of failure/exit on  stdinReaderThread";
                // REVIEW: On some flavors of Mono, calling exit may freeze the process if we're using the WinForms event handler
                // Basically, on Mono 2.6.3, the GUI thread may be left dangling on exit.  At that point:
                //   -- System.Environment.Exit will cause the process to stop responding
                //   -- Calling Application.Exit() will leave the GUI thread up and running, creating a Zombie process
                //   -- Calling Abort() on the Main thread or the GUI thread will have no effect, and the process will remain unresponsive
                // Also, even the the GUI thread is up and running, the WinForms event loop will be listed as closed
                // In this case, killing the process is harmless, since we've already cleaned up after ourselves and FSI is responding
                // to an error.  (CTRL-C is handled elsewhere.)
                // We'll only do this if we're running on Mono, "--gui" is specified and our input is piped in from stdin, so it's still
                // fairly constrained.
#if FX_NO_WINFORMS
                exit 1
#else
                if runningOnMono && fsiOptions.Gui then
                    System.Environment.ExitCode <- 1
                    Process.GetCurrentProcess().Kill()
                else
                    exit 1
#endif

        ),Name="StdinReaderThread")

      if progress then fprintfn fsiConsoleOutput.Out "MAIN: starting stdin thread..."
      stdinReaderThread.Start()

    member _.CompletionsForPartialLID (istate, prefix:string) =
        let lid,stem =
            if prefix.IndexOf(".",StringComparison.Ordinal) >= 0 then
                let parts = prefix.Split('.')
                let n = parts.Length
                Array.sub parts 0 (n-1) |> Array.toList,parts.[n-1]
            else
                [],prefix

        let tcState = istate.tcState
        let amap = istate.tcImports.GetImportMap()
        let infoReader = new InfoReader(istate.tcGlobals,amap)
        let ncenv = new NameResolver(istate.tcGlobals,amap,infoReader,FakeInstantiationGenerator)
        let ad = tcState.TcEnvFromImpls.AccessRights
        let nenv = tcState.TcEnvFromImpls.NameEnv

        let nItems = NameResolution.ResolvePartialLongIdent ncenv nenv (ConstraintSolver.IsApplicableMethApprox istate.tcGlobals amap rangeStdin) rangeStdin ad lid false
        let names  = nItems |> List.map (fun d -> d.DisplayName)
        let names  = names |> List.filter (fun name -> name.StartsWithOrdinal(stem))
        names

    member _.ParseAndCheckInteraction (legacyReferenceResolver, istate, text:string) =
        let tcConfig = TcConfig.Create(tcConfigB,validate=false)

        let fsiInteractiveChecker = FsiInteractiveChecker(legacyReferenceResolver, tcConfig, istate.tcGlobals, istate.tcImports, istate.tcState)
        fsiInteractiveChecker.ParseAndCheckInteraction(SourceText.ofString text)


//----------------------------------------------------------------------------
// Server mode:
//----------------------------------------------------------------------------

let internal SpawnThread name f =
    let th = new Thread(new ThreadStart(f),Name=name)
    th.IsBackground <- true;
    th.Start()

let internal SpawnInteractiveServer
                           (fsi: FsiEvaluationSessionHostConfig,
                            fsiOptions : FsiCommandLineOptions,
                            fsiConsoleOutput:  FsiConsoleOutput) =
    //printf "Spawning fsi server on channel '%s'" !fsiServerName;
    SpawnThread "ServerThread" (fun () ->
         use _scope = SetCurrentUICultureForThread fsiOptions.FsiLCID
         try
             fsi.StartServer(fsiOptions.FsiServerName)
         with e ->
             fprintfn fsiConsoleOutput.Error "%s" (FSIstrings.SR.fsiExceptionRaisedStartingServer(e.ToString())))

/// Repeatedly drive the event loop (e.g. Application.Run()) but catching ThreadAbortException and re-running.
///
/// This gives us a last chance to catch an abort on the main execution thread.
let internal DriveFsiEventLoop (fsi: FsiEvaluationSessionHostConfig, fsiConsoleOutput: FsiConsoleOutput) =
    let rec runLoop() =
        if progress then fprintfn fsiConsoleOutput.Out "GUI thread runLoop";
        let restart =
            try
              // BLOCKING POINT: The GUI Thread spends most (all) of its time this event loop
              if progress then fprintfn fsiConsoleOutput.Out "MAIN:  entering event loop...";
              fsi.EventLoopRun()
            with
            |  :? ThreadAbortException ->
              // If this TAE handler kicks it's almost certainly too late to save the
              // state of the process - the state of the message loop may have been corrupted
              fsiConsoleOutput.uprintnfn "%s" (FSIstrings.SR.fsiUnexpectedThreadAbortException());
              (try Thread.ResetAbort() with _ -> ());
              true
              // Try again, just case we can restart
            | e ->
              stopProcessingRecovery e range0;
              true
              // Try again, just case we can restart
        if progress then fprintfn fsiConsoleOutput.Out "MAIN:  exited event loop...";
        if restart then runLoop()

    runLoop();

/// Thrown when there was an error compiling the given code in FSI.
type FsiCompilationException(message: string, errorInfos: FSharpDiagnostic[] option) =
    inherit System.Exception(message)
    member _.ErrorInfos = errorInfos

/// The primary type, representing a full F# Interactive session, reading from the given
/// text input, writing to the given text output and error writers.
type FsiEvaluationSession (fsi: FsiEvaluationSessionHostConfig, argv:string[], inReader:TextReader, outWriter:TextWriter, errorWriter: TextWriter, fsiCollectible: bool, legacyReferenceResolver: LegacyReferenceResolver option) =

    do if not runningOnMono then UnmanagedProcessExecutionOptions.EnableHeapTerminationOnCorruption() (* SDL recommendation *)

    // Explanation: When FsiEvaluationSession.Create is called we do a bunch of processing. For fsi.exe
    // and fsiAnyCpu.exe there are no other active threads at this point, so we can assume this is the
    // unique compilation thread.  For other users of FsiEvaluationSession it is reasonable to assume that
    // the object is not accessed concurrently during startup preparation.
    //
    // We later switch to doing interaction-by-interaction processing on the "event loop" thread.
    let ctokStartup = AssumeCompilationThreadWithoutEvidence ()

    let timeReporter = FsiTimeReporter(outWriter)

    //----------------------------------------------------------------------------
    // Console coloring
    //----------------------------------------------------------------------------

    // Testing shows "console coloring" is broken on some Mono configurations (e.g. Mono 2.4 Suse LiveCD).
    // To support fsi usage, the console coloring is switched off by default on Mono.
    do if runningOnMono then enableConsoleColoring <- false

    //----------------------------------------------------------------------------
    // tcConfig - build the initial config
    //----------------------------------------------------------------------------

    let currentDirectory = Directory.GetCurrentDirectory()
    let tryGetMetadataSnapshot = (fun _ -> None)

    let defaultFSharpBinariesDir = FSharpEnvironment.BinFolderOfDefaultFSharpCompiler(FSharpEnvironment.tryCurrentDomain()).Value

    let legacyReferenceResolver =
        match legacyReferenceResolver with
        | None -> SimulatedMSBuildReferenceResolver.getResolver()
        | Some rr -> rr

    let tcConfigB =
        TcConfigBuilder.CreateNew(legacyReferenceResolver,
            defaultFSharpBinariesDir=defaultFSharpBinariesDir,
            reduceMemoryUsage=ReduceMemoryFlag.Yes,
            implicitIncludeDir=currentDirectory,
            isInteractive=true,
            isInvalidationSupported=false,
            defaultCopyFSharpCore=CopyFSharpCoreFlag.No,
            tryGetMetadataSnapshot=tryGetMetadataSnapshot,
            sdkDirOverride=None,
            rangeForErrors=range0)

    let tcConfigP = TcConfigProvider.BasedOnMutableBuilder(tcConfigB)
    do tcConfigB.resolutionEnvironment <- LegacyResolutionEnvironment.CompilationAndEvaluation // See Bug 3608
    do tcConfigB.useFsiAuxLib <- fsi.UseFsiAuxLib

#if NETSTANDARD
    do tcConfigB.SetUseSdkRefs true
    do tcConfigB.useSimpleResolution <- true
    do if FSharpEnvironment.isRunningOnCoreClr then SetTargetProfile tcConfigB "netcore" // always assume System.Runtime codegen
#endif

    // Preset: --optimize+ -g --tailcalls+ (see 4505)
    do SetOptimizeSwitch tcConfigB OptionSwitch.On
    do SetDebugSwitch    tcConfigB (Some "pdbonly") OptionSwitch.On
    do SetTailcallSwitch tcConfigB OptionSwitch.On

#if NETSTANDARD
    // set platform depending on whether the current process is a 64-bit process.
    // BUG 429882 : FsiAnyCPU.exe issues warnings (x64 v MSIL) when referencing 64-bit assemblies
    do tcConfigB.platform <- if IntPtr.Size = 8 then Some AMD64 else Some X86
#endif

    let fsiStdinSyphon = new FsiStdinSyphon(errorWriter)
    let fsiConsoleOutput = FsiConsoleOutput(tcConfigB, outWriter, errorWriter)

    let errorLogger = ErrorLoggerThatStopsOnFirstError(tcConfigB, fsiStdinSyphon, fsiConsoleOutput)

    do InstallErrorLoggingOnThisThread errorLogger // FSI error logging on main thread.

    let updateBannerText() =
      tcConfigB.productNameForBannerText <- FSIstrings.SR.fsiProductName(FSharpEnvironment.FSharpBannerVersion)

    do updateBannerText() // setting the correct banner so that 'fsi -?' display the right thing

    let fsiOptions = FsiCommandLineOptions(fsi, argv, tcConfigB, fsiConsoleOutput)

    do
      match fsiOptions.WriteReferencesAndExit with
      | Some outFile ->
          let tcConfig = tcConfigP.Get(ctokStartup)
          let references, _unresolvedReferences = TcAssemblyResolutions.GetAssemblyResolutionInformation(tcConfig)
          let lines = [ for r in references -> r.resolvedPath ]
          FileSystem.OpenFileForWriteShim(outFile).WriteAllLines(lines)
          exit 0
      | _ -> ()

    let fsiConsolePrompt = FsiConsolePrompt(fsiOptions, fsiConsoleOutput)

    do
      match tcConfigB.preferredUiLang with
      | Some s -> Thread.CurrentThread.CurrentUICulture <- new System.Globalization.CultureInfo(s)
      | None -> ()

    do
      try
          SetServerCodePages fsiOptions
      with e ->
          warning(e)

    do
      updateBannerText() // resetting banner text after parsing options

      if tcConfigB.showBanner then
          fsiOptions.ShowBanner()

    do fsiConsoleOutput.uprintfn ""

    // When no source files to load, print ahead prompt here
    do if List.isEmpty fsiOptions.SourceFiles then
        fsiConsolePrompt.PrintAhead()


    let fsiConsoleInput = FsiConsoleInput(fsi, fsiOptions, inReader, outWriter)

    /// The single, global interactive checker that can be safely used in conjunction with other operations
    /// on the FsiEvaluationSession.
    let checker = FSharpChecker.Create(legacyReferenceResolver=legacyReferenceResolver)

    let (tcGlobals,frameworkTcImports,nonFrameworkResolutions,unresolvedReferences) =
        try
            let tcConfig = tcConfigP.Get(ctokStartup)
            checker.FrameworkImportsCache.Get (tcConfig)
            |> NodeCode.RunImmediateWithoutCancellation
        with e ->
            stopProcessingRecovery e range0; failwithf "Error creating evaluation session: %A" e

    let tcImports =
      try
          TcImports.BuildNonFrameworkTcImports(tcConfigP, frameworkTcImports, nonFrameworkResolutions, unresolvedReferences, fsiOptions.DependencyProvider) 
          |> NodeCode.RunImmediateWithoutCancellation
      with e ->
          stopProcessingRecovery e range0; failwithf "Error creating evaluation session: %A" e

    let niceNameGen = NiceNameGenerator()

    // Share intern'd strings across all lexing/parsing
    let lexResourceManager = new Lexhelp.LexResourceManager()

    /// The lock stops the type checker running at the same time as the server intellisense implementation.
    let tcLockObject = box 7 // any new object will do

    let resolveAssemblyRef (aref: ILAssemblyRef) =
        // Explanation: This callback is invoked during compilation to resolve assembly references
        // We don't yet propagate the ctok through these calls (though it looks plausible to do so).
        let ctok = AssumeCompilationThreadWithoutEvidence ()
#if !NO_EXTENSIONTYPING
        match tcImports.TryFindProviderGeneratedAssemblyByName (ctok, aref.Name) with
        | Some assembly -> Some (Choice2Of2 assembly)
        | None ->
#endif
        match tcImports.TryFindExistingFullyQualifiedPathByExactAssemblyRef (aref) with
        | Some resolvedPath -> Some (Choice1Of2 resolvedPath)
        | None -> None

    let fsiDynamicCompiler = FsiDynamicCompiler(fsi, timeReporter, tcConfigB, tcLockObject, outWriter, tcImports, tcGlobals, fsiOptions, fsiConsoleOutput, fsiCollectible, niceNameGen, resolveAssemblyRef)

    let fsiInterruptController = FsiInterruptController(fsiOptions, fsiConsoleOutput)

    let uninstallMagicAssemblyResolution = MagicAssemblyResolution.Install(tcConfigB, tcImports, fsiDynamicCompiler, fsiConsoleOutput)

    /// This reference cell holds the most recent interactive state
    let initialInteractiveState = fsiDynamicCompiler.GetInitialInteractiveState ()

    let fsiStdinLexerProvider = FsiStdinLexerProvider(tcConfigB, fsiStdinSyphon, fsiConsoleInput, fsiConsoleOutput, fsiOptions, lexResourceManager)

    let fsiInteractionProcessor = FsiInteractionProcessor(fsi, tcConfigB, fsiOptions, fsiDynamicCompiler, fsiConsolePrompt, fsiConsoleOutput, fsiInterruptController, fsiStdinLexerProvider, lexResourceManager, initialInteractiveState)

    // Raising an exception throws away the exception stack making diagnosis hard
    // this wraps the existing exception as the inner exception
    let makeNestedException (userExn: #Exception) =
        // clone userExn -- make userExn the inner exception, to retain the stacktrace on raise
        let arguments = [| userExn.Message :> obj; userExn :> obj |]
        Activator.CreateInstance(userExn.GetType(), arguments) :?> Exception

    let commitResult res =
        match res with
        | Choice1Of2 r -> r
        | Choice2Of2 None -> raise (FsiCompilationException(FSIstrings.SR.fsiOperationFailed(), None))
        | Choice2Of2 (Some userExn) -> raise (makeNestedException userExn)

    let commitResultNonThrowing errorOptions scriptFile (errorLogger: CompilationErrorLogger) res =
        let errs = errorLogger.GetDiagnostics()
        let errorInfos = DiagnosticHelpers.CreateDiagnostics (errorOptions, true, scriptFile, errs, true)
        let userRes =
            match res with
            | Choice1Of2 r -> Choice1Of2 r
            | Choice2Of2 None -> Choice2Of2 (FsiCompilationException(FSIstrings.SR.fsiOperationCouldNotBeCompleted(), Some errorInfos) :> exn)
            | Choice2Of2 (Some userExn) -> Choice2Of2 userExn

        // 'true' is passed for "suggestNames" because we want the FSI session to suggest names for misspellings and it won't affect IDE perf much
        userRes, errorInfos

    let dummyScriptFileName = "input.fsx"

    interface IDisposable with
        member x.Dispose() =
            (tcImports :> IDisposable).Dispose()
            uninstallMagicAssemblyResolution.Dispose()

    /// Load the dummy interaction, load the initial files, and,
    /// if interacting, start the background thread to read the standard input.
    member x.Interrupt() = fsiInterruptController.Interrupt()

    /// A host calls this to get the completions for a long identifier, e.g. in the console
    member x.GetCompletions(longIdent) =
        fsiInteractionProcessor.CompletionsForPartialLID (fsiInteractionProcessor.CurrentState, longIdent)  |> Seq.ofList

    member x.ParseAndCheckInteraction(code) =
        fsiInteractionProcessor.ParseAndCheckInteraction (legacyReferenceResolver, fsiInteractionProcessor.CurrentState, code)
        |> Cancellable.runWithoutCancellation

    member x.InteractiveChecker = checker

    member x.CurrentPartialAssemblySignature =
        fsiDynamicCompiler.CurrentPartialAssemblySignature (fsiInteractionProcessor.CurrentState)

    member x.DynamicAssembly =
        fsiDynamicCompiler.DynamicAssembly

    /// A host calls this to determine if the --gui parameter is active
    member x.IsGui = fsiOptions.Gui

    /// A host calls this to get the active language ID if provided by fsi-server-lcid
    member x.LCID = fsiOptions.FsiLCID

#if FX_NO_APP_DOMAINS
    member x.ReportUnhandledException (exn:exn) = ignore exn; ()
#else
    /// A host calls this to report an unhandled exception in a standard way, e.g. an exception on the GUI thread gets printed to stderr
    member x.ReportUnhandledException exn = x.ReportUnhandledExceptionSafe true exn

    member x.ReportUnhandledExceptionSafe isFromThreadException (exn:exn) =
             fsi.EventLoopInvoke (
                fun () ->
                    fprintfn fsiConsoleOutput.Error "%s" (exn.ToString())
                    errorLogger.SetError()
                    try
                        errorLogger.AbortOnError(fsiConsoleOutput)
                    with StopProcessing ->
                        // BUG 664864 some window that use System.Windows.Forms.DataVisualization types (possible FSCharts) was created in FSI.
                        // at some moment one chart has raised InvalidArgumentException from OnPaint, this exception was intercepted by the code in higher layer and
                        // passed to Application.OnThreadException. FSI has already attached its own ThreadException handler, inside it will log the original error
                        // and then raise StopProcessing exception to unwind the stack (and possibly shut down current Application) and get to DriveFsiEventLoop.
                        // DriveFsiEventLoop handles StopProcessing by suppressing it and restarting event loop from the beginning.
                        // This schema works almost always except when FSI is started as 64 bit process (FsiAnyCpu) on Windows 7.

                        // http://msdn.microsoft.com/en-us/library/windows/desktop/ms633573(v=vs.85).aspx
                        // Remarks:
                        // If your application runs on a 32-bit version of Windows operating system, uncaught exceptions from the callback
                        // will be passed onto higher-level exception handlers of your application when available.
                        // The system then calls the unhandled exception filter to handle the exception prior to terminating the process.
                        // If the PCA is enabled, it will offer to fix the problem the next time you run the application.
                        // However, if your application runs on a 64-bit version of Windows operating system or WOW64,
                        // you should be aware that a 64-bit operating system handles uncaught exceptions differently based on its 64-bit processor architecture,
                        // exception architecture, and calling convention.
                        // The following table summarizes all possible ways that a 64-bit Windows operating system or WOW64 handles uncaught exceptions.
                        // 1. The system suppresses any uncaught exceptions.
                        // 2. The system first terminates the process, and then the Program Compatibility Assistant (PCA) offers to fix it the next time
                        // you run the application. You can disable the PCA mitigation by adding a Compatibility section to the application manifest.
                        // 3. The system calls the exception filters but suppresses any uncaught exceptions when it leaves the callback scope,
                        // without invoking the associated handlers.
                        // Behavior type 2 only applies to the 64-bit version of the Windows 7 operating system.

                        // NOTE: tests on Win8 box showed that 64 bit version of the Windows 8 always apply type 2 behavior

                        // Effectively this means that when StopProcessing exception is raised from ThreadException callback - it won't be intercepted in DriveFsiEventLoop.
                        // Instead it will be interpreted as unhandled exception and crash the whole process.

                        // FIX: detect if current process in 64 bit running on Windows 7 or Windows 8 and if yes - swallow the StopProcessing and ScheduleRestart instead.
                        // Visible behavior should not be different, previously exception unwinds the stack and aborts currently running Application.
                        // After that it will be intercepted and suppressed in DriveFsiEventLoop.
                        // Now we explicitly shut down Application so after execution of callback will be completed the control flow
                        // will also go out of WinFormsEventLoop.Run and again get to DriveFsiEventLoop => restart the loop. I'd like the fix to be  as conservative as possible
                        // so we use special case for problematic case instead of just always scheduling restart.

                        // http://msdn.microsoft.com/en-us/library/windows/desktop/ms724832(v=vs.85).aspx
                        let os = Environment.OSVersion
                        // Win7 6.1
                        let isWindows7 = os.Version.Major = 6 && os.Version.Minor = 1
                        // Win8 6.2
                        let isWindows8Plus = os.Version >= Version(6, 2, 0, 0)
                        if isFromThreadException && ((isWindows7 && (IntPtr.Size = 8) && isWindows8Plus))
#if DEBUG
                            // for debug purposes
                            && Environment.GetEnvironmentVariable("FSI_SCHEDULE_RESTART_WITH_ERRORS") = null
#endif
                        then
                            fsi.EventLoopScheduleRestart()
                        else
                            reraise()
                )
#endif

    member x.PartialAssemblySignatureUpdated = fsiInteractionProcessor.PartialAssemblySignatureUpdated


    member x.FormatValue(reflectionValue:obj, reflectionType) =
        fsiDynamicCompiler.FormatValue(reflectionValue, reflectionType)

    member x.EvalExpression(code) =

        // Explanation: When the user of the FsiInteractiveSession object calls this method, the
        // code is parsed, checked and evaluated on the calling thread. This means EvalExpression
        // is not safe to call concurrently.
        let ctok = AssumeCompilationThreadWithoutEvidence()

        fsiInteractionProcessor.EvalExpression(ctok, code, dummyScriptFileName, errorLogger)
        |> commitResult

    member x.EvalExpressionNonThrowing(code) =
        // Explanation: When the user of the FsiInteractiveSession object calls this method, the
        // code is parsed, checked and evaluated on the calling thread. This means EvalExpression
        // is not safe to call concurrently.
        let ctok = AssumeCompilationThreadWithoutEvidence()

        let errorOptions = TcConfig.Create(tcConfigB,validate = false).errorSeverityOptions
        let errorLogger = CompilationErrorLogger("EvalInteraction", errorOptions)
        fsiInteractionProcessor.EvalExpression(ctok, code, dummyScriptFileName, errorLogger)
        |> commitResultNonThrowing errorOptions dummyScriptFileName errorLogger

    member x.EvalInteraction(code, ?cancellationToken) : unit =
        // Explanation: When the user of the FsiInteractiveSession object calls this method, the
        // code is parsed, checked and evaluated on the calling thread. This means EvalExpression
        // is not safe to call concurrently.
        let ctok = AssumeCompilationThreadWithoutEvidence()
        let cancellationToken = defaultArg cancellationToken CancellationToken.None
        fsiInteractionProcessor.EvalInteraction(ctok, code, dummyScriptFileName, errorLogger, cancellationToken)
        |> commitResult
        |> ignore

    member x.EvalInteractionNonThrowing(code, ?cancellationToken) =
        // Explanation: When the user of the FsiInteractiveSession object calls this method, the
        // code is parsed, checked and evaluated on the calling thread. This means EvalExpression
        // is not safe to call concurrently.
        let ctok = AssumeCompilationThreadWithoutEvidence()
        let cancellationToken = defaultArg cancellationToken CancellationToken.None

        let errorOptions = TcConfig.Create(tcConfigB,validate = false).errorSeverityOptions
        let errorLogger = CompilationErrorLogger("EvalInteraction", errorOptions)
        fsiInteractionProcessor.EvalInteraction(ctok, code, dummyScriptFileName, errorLogger, cancellationToken)
        |> commitResultNonThrowing errorOptions "input.fsx" errorLogger

    member x.EvalScript(filePath) : unit =
        // Explanation: When the user of the FsiInteractiveSession object calls this method, the
        // code is parsed, checked and evaluated on the calling thread. This means EvalExpression
        // is not safe to call concurrently.
        let ctok = AssumeCompilationThreadWithoutEvidence()

        fsiInteractionProcessor.EvalScript(ctok, filePath, errorLogger)
        |> commitResult
        |> ignore

    member x.EvalScriptNonThrowing(filePath) =
        // Explanation: When the user of the FsiInteractiveSession object calls this method, the
        // code is parsed, checked and evaluated on the calling thread. This means EvalExpression
        // is not safe to call concurrently.
        let ctok = AssumeCompilationThreadWithoutEvidence()

        let errorOptions = TcConfig.Create(tcConfigB, validate = false).errorSeverityOptions
        let errorLogger = CompilationErrorLogger("EvalInteraction", errorOptions)
        fsiInteractionProcessor.EvalScript(ctok, filePath, errorLogger)
        |> commitResultNonThrowing errorOptions filePath errorLogger
        |> function Choice1Of2 (_), errs -> Choice1Of2 (), errs | Choice2Of2 exn, errs -> Choice2Of2 exn, errs

    /// Event fires when a root-level value is bound to an identifier, e.g., via `let x = ...`.
    member _.ValueBound = fsiDynamicCompiler.ValueBound

    member _.GetBoundValues() =
        fsiDynamicCompiler.GetBoundValues fsiInteractionProcessor.CurrentState

    member _.TryFindBoundValue(name: string) =
        fsiDynamicCompiler.TryFindBoundValue(fsiInteractionProcessor.CurrentState, name)

    member _.AddBoundValue(name: string, value: obj) =
        // Explanation: When the user of the FsiInteractiveSession object calls this method, the
        // code is parsed, checked and evaluated on the calling thread. This means EvalExpression
        // is not safe to call concurrently.
        let ctok = AssumeCompilationThreadWithoutEvidence()

        fsiInteractionProcessor.AddBoundValue(ctok, errorLogger, name, value)
        |> commitResult
        |> ignore

    /// Performs these steps:
    ///    - Load the dummy interaction, if any
    ///    - Set up exception handling, if any
    ///    - Load the initial files, if any
    ///    - Start the background thread to read the standard input, if any
    ///    - Sit in the GUI event loop indefinitely, if needed
    ///
    /// This method only returns after "exit". The method repeatedly calls the event loop and
    /// the thread may be subject to Thread.Abort() signals if Interrupt() is used, giving rise
    /// to internal ThreadAbortExceptions.
    ///
    /// A background thread is started by this thread to read from the inReader and/or console reader.

    [<CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2004:RemoveCallsToGCKeepAlive")>]
    member x.Run() =
        progress <- condition "FSHARP_INTERACTIVE_PROGRESS"

        // Explanation: When Run is called we do a bunch of processing. For fsi.exe
        // and fsiAnyCpu.exe there are no other active threads at this point, so we can assume this is the
        // unique compilation thread.  For other users of FsiEvaluationSession it is reasonable to assume that
        // the object is not accessed concurrently during startup preparation.
        //
        // We later switch to doing interaction-by-interaction processing on the "event loop" thread
        let ctokRun = AssumeCompilationThreadWithoutEvidence ()

        if not runningOnMono && fsiOptions.IsInteractiveServer then
            SpawnInteractiveServer (fsi, fsiOptions, fsiConsoleOutput)

        use unwindBuildPhase = PushThreadBuildPhaseUntilUnwind BuildPhase.Interactive

        if fsiOptions.Interact then
            // page in the type check env
            fsiInteractionProcessor.LoadDummyInteraction(ctokStartup, errorLogger)
            if progress then fprintfn fsiConsoleOutput.Out "MAIN: InstallKillThread!";

            // Compute how long to pause before a ThreadAbort is actually executed.
            // A somewhat arbitrary choice.
            let pauseMilliseconds = (if fsiOptions.Gui then 400 else 100)

            // Request that ThreadAbort interrupts be performed on this (current) thread
            fsiInterruptController.InstallKillThread(Thread.CurrentThread, pauseMilliseconds)
            if progress then fprintfn fsiConsoleOutput.Out "MAIN: got initial state, creating form";

#if !FX_NO_APP_DOMAINS
            // Route background exceptions to the exception handlers
            AppDomain.CurrentDomain.UnhandledException.Add (fun args ->
                match args.ExceptionObject with
                | :? System.Exception as err -> x.ReportUnhandledExceptionSafe false err
                | _ -> ())
#endif

            fsiInteractionProcessor.LoadInitialFiles(ctokRun, errorLogger)

            fsiInteractionProcessor.StartStdinReadAndProcessThread(errorLogger)

            DriveFsiEventLoop (fsi, fsiConsoleOutput )

        else // not interact
            if progress then fprintfn fsiConsoleOutput.Out "Run: not interact, loading initial files..."
            fsiInteractionProcessor.LoadInitialFiles(ctokRun, errorLogger)

            if progress then fprintfn fsiConsoleOutput.Out "Run: done..."
            exit (min errorLogger.ErrorCount 1)

        // The Ctrl-C exception handler that we've passed to native code has
        // to be explicitly kept alive.
        GC.KeepAlive fsiInterruptController.EventHandlers

    static member Create(fsiConfig, argv, inReader, outWriter, errorWriter, ?collectible, ?legacyReferenceResolver) =
        new FsiEvaluationSession(fsiConfig, argv, inReader, outWriter, errorWriter, defaultArg collectible false, legacyReferenceResolver)

    static member GetDefaultConfiguration(fsiObj:obj) = FsiEvaluationSession.GetDefaultConfiguration(fsiObj, true)

    static member GetDefaultConfiguration(fsiObj:obj, useFsiAuxLib: bool) =
        // We want to avoid modifying FSharp.Compiler.Interactive.Settings to avoid republishing that DLL.
        // So we access these via reflection
        { // Connect the configuration through to the 'fsi' object from FSharp.Compiler.Interactive.Settings
            new FsiEvaluationSessionHostConfig () with
              member _.FormatProvider = getInstanceProperty fsiObj "FormatProvider"
              member _.FloatingPointFormat = getInstanceProperty fsiObj "FloatingPointFormat"
              member _.AddedPrinters = getInstanceProperty fsiObj "AddedPrinters"
              member _.ShowDeclarationValues = getInstanceProperty fsiObj "ShowDeclarationValues"
              member _.ShowIEnumerable = getInstanceProperty fsiObj "ShowIEnumerable"
              member _.ShowProperties = getInstanceProperty fsiObj "ShowProperties"
              member _.PrintSize = getInstanceProperty fsiObj "PrintSize"
              member _.PrintDepth = getInstanceProperty fsiObj "PrintDepth"
              member _.PrintWidth = getInstanceProperty fsiObj "PrintWidth"
              member _.PrintLength = getInstanceProperty fsiObj "PrintLength"
              member _.ReportUserCommandLineArgs args = setInstanceProperty fsiObj "CommandLineArgs" args
              member _.StartServer(fsiServerName) =  failwith "--fsi-server not implemented in the default configuration"
              member _.EventLoopRun() = callInstanceMethod0 (getInstanceProperty fsiObj "EventLoop") [||] "Run"
              member _.EventLoopInvoke(f : unit -> 'T) =  callInstanceMethod1 (getInstanceProperty fsiObj "EventLoop") [|typeof<'T>|] "Invoke" f
              member _.EventLoopScheduleRestart() = callInstanceMethod0 (getInstanceProperty fsiObj "EventLoop") [||] "ScheduleRestart"
              member _.UseFsiAuxLib = useFsiAuxLib
              member _.GetOptionalConsoleReadLine(_probe) = None }
//-------------------------------------------------------------------------------
// If no "fsi" object for the configuration is specified, make the default
// configuration one which stores the settings in-process

module Settings =
    type IEventLoop =
        abstract Run : unit -> bool
        abstract Invoke : (unit -> 'T) -> 'T
        abstract ScheduleRestart : unit -> unit

    // fsi.fs in FSHarp.Compiler.Service.dll avoids a hard dependency on FSharp.Compiler.Interactive.Settings.dll
    // by providing an optional reimplementation of the functionality

    // An implementation of IEventLoop suitable for the command-line console
    [<AutoSerializable(false)>]
    type internal SimpleEventLoop() =
        let runSignal = new AutoResetEvent(false)
        let exitSignal = new AutoResetEvent(false)
        let doneSignal = new AutoResetEvent(false)
        let mutable queue = ([] : (unit -> obj) list)
        let mutable result = (None : obj option)
        let setSignal(signal : AutoResetEvent) = while not (signal.Set()) do Thread.Sleep(1); done
        let waitSignal signal = WaitHandle.WaitAll([| (signal :> WaitHandle) |]) |> ignore
        let waitSignal2 signal1 signal2 =
            WaitHandle.WaitAny([| (signal1 :> WaitHandle); (signal2 :> WaitHandle) |])
        let mutable running = false
        let mutable restart = false
        interface IEventLoop with
             member x.Run() =
                 running <- true
                 let rec run() =
                     match waitSignal2 runSignal exitSignal with
                     | 0 ->
                         queue |> List.iter (fun f -> result <- try Some(f()) with _ -> None);
                         setSignal doneSignal
                         run()
                     | 1 ->
                         running <- false;
                         restart
                     | _ -> run()
                 run();
             member x.Invoke(f : unit -> 'T) : 'T  =
                 queue <- [f >> box]
                 setSignal runSignal
                 waitSignal doneSignal
                 result.Value |> unbox
             member x.ScheduleRestart() =
                 if running then
                     restart <- true
                     setSignal exitSignal
        interface System.IDisposable with
             member x.Dispose() =
                 runSignal.Dispose()
                 exitSignal.Dispose()
                 doneSignal.Dispose()


    [<Sealed>]
    type InteractiveSettings()  =
        let mutable evLoop = (new SimpleEventLoop() :> IEventLoop)
        let mutable showIDictionary = true
        let mutable showDeclarationValues = true
        let mutable args = Environment.GetCommandLineArgs()
        let mutable fpfmt = "g10"
        let mutable fp = (CultureInfo.InvariantCulture :> System.IFormatProvider)
        let mutable printWidth = 78
        let mutable printDepth = 100
        let mutable printLength = 100
        let mutable printSize = 10000
        let mutable showIEnumerable = true
        let mutable showProperties = true
        let mutable addedPrinters = []

        member _.FloatingPointFormat with get() = fpfmt and set v = fpfmt <- v
        member _.FormatProvider with get() = fp and set v = fp <- v
        member _.PrintWidth  with get() = printWidth and set v = printWidth <- v
        member _.PrintDepth  with get() = printDepth and set v = printDepth <- v
        member _.PrintLength  with get() = printLength and set v = printLength <- v
        member _.PrintSize  with get() = printSize and set v = printSize <- v
        member _.ShowDeclarationValues with get() = showDeclarationValues and set v = showDeclarationValues <- v
        member _.ShowProperties  with get() = showProperties and set v = showProperties <- v
        member _.ShowIEnumerable with get() = showIEnumerable and set v = showIEnumerable <- v
        member _.ShowIDictionary with get() = showIDictionary and set v = showIDictionary <- v
        member _.AddedPrinters with get() = addedPrinters and set v = addedPrinters <- v
        member _.CommandLineArgs with get() = args  and set v  = args <- v
        member _.AddPrinter(printer : 'T -> string) =
          addedPrinters <- Choice1Of2 (typeof<'T>, (fun (x:obj) -> printer (unbox x))) :: addedPrinters

        member _.EventLoop
           with get () = evLoop
           and set (x:IEventLoop)  = evLoop.ScheduleRestart(); evLoop <- x

        member _.AddPrintTransformer(printer : 'T -> obj) =
          addedPrinters <- Choice2Of2 (typeof<'T>, (fun (x:obj) -> printer (unbox x))) :: addedPrinters

    let fsi = InteractiveSettings()

type FsiEvaluationSession with
    static member GetDefaultConfiguration() =
        FsiEvaluationSession.GetDefaultConfiguration(Settings.fsi, false)

/// Defines a read-only input stream used to feed content to the hosted F# Interactive dynamic compiler.
[<AllowNullLiteral>]
type CompilerInputStream() =
    inherit Stream()
    // Duration (in milliseconds) of the pause in the loop of waitForAtLeastOneByte.
    let pauseDuration = 100

    // Queue of characters waiting to be read.
    let readQueue = new Queue<byte>()

    let  waitForAtLeastOneByte(count : int) =
        let rec loop() =
            let attempt =
                lock readQueue (fun () ->
                    let n = readQueue.Count
                    if (n >= 1) then
                        let lengthToRead = if (n < count) then n else count
                        let ret = Array.zeroCreate lengthToRead
                        for i in 0 .. lengthToRead - 1 do
                            ret.[i] <- readQueue.Dequeue()
                        Some ret
                    else
                        None)
            match attempt with
            | None -> System.Threading.Thread.Sleep(pauseDuration); loop()
            | Some res -> res
        loop()

    override x.CanRead = true
    override x.CanWrite = false
    override x.CanSeek = false
    override x.Position with get() = raise (NotSupportedException()) and set _v = raise (NotSupportedException())
    override x.Length = raise (NotSupportedException())
    override x.Flush() = ()
    override x.Seek(_offset, _origin) = raise (NotSupportedException())
    override x.SetLength(_value) = raise (NotSupportedException())
    override x.Write(_buffer, _offset, _count) = raise (NotSupportedException("Cannot write to input stream"))
    override x.Read(buffer, offset, count) =
        let bytes = waitForAtLeastOneByte count
        Array.Copy(bytes, 0, buffer, offset, bytes.Length)
        bytes.Length

    /// Feeds content into the stream.
    member x.Add(str:string) =
        if (System.String.IsNullOrEmpty(str)) then () else

        lock readQueue (fun () ->
            let bytes = System.Text.Encoding.UTF8.GetBytes(str)
            for i in 0 .. bytes.Length - 1 do
                readQueue.Enqueue(bytes.[i]))



/// Defines a write-only stream used to capture output of the hosted F# Interactive dynamic compiler.
[<AllowNullLiteral>]
type CompilerOutputStream()  =
    inherit Stream()
    // Queue of characters waiting to be read.
    let contentQueue = new Queue<byte>()
    let nyi() = raise (NotSupportedException())

    override x.CanRead = false
    override x.CanWrite = true
    override x.CanSeek = false
    override x.Position with get() = nyi() and set _v = nyi()
    override x.Length = nyi()
    override x.Flush() = ()
    override x.Seek(_offset, _origin) = nyi()
    override x.SetLength(_value) = nyi()
    override x.Read(_buffer, _offset, _count) = raise (NotSupportedException("Cannot write to input stream"))
    override x.Write(buffer, offset, count) =
        let stop = offset + count
        if (stop > buffer.Length) then raise (ArgumentException("offset,count"))

        lock contentQueue (fun () ->
            for i in offset .. stop - 1 do
                contentQueue.Enqueue(buffer.[i]))

    member x.Read() =
        lock contentQueue (fun () ->
            let n = contentQueue.Count
            if (n > 0) then
                let bytes = Array.zeroCreate n
                for i in 0 .. n-1 do
                    bytes.[i] <- contentQueue.Dequeue()

                System.Text.Encoding.UTF8.GetString(bytes, 0, n)
            else
                "")
