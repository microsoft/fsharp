// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

// This component is used by the 'fsharpqa' tests for faster in-memory compilation.  It should be removed and the 
// proper compiler service API used instead.

namespace FSharp.Compiler.CodeAnalysis.Hosted

open System
open System.IO
open System.Text.RegularExpressions
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.Driver
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.CompilerConfig
open FSharp.Compiler.CompilerDiagnostics
open FSharp.Compiler.AbstractIL.ILBinaryReader
open Internal.Utilities.Library 

/// build issue location
type internal Location =
    {
        StartLine : int
        StartColumn : int
        EndLine : int
        EndColumn : int
    }

type internal CompilationIssueType = Warning | Error

/// build issue details
type internal CompilationIssue = 
    { 
        Location : Location
        Subcategory : string
        Code : string
        File : string
        Text : string 
        Type : CompilationIssueType
    }

/// combined warning and error details
type internal FailureDetails = 
    {
        Warnings : CompilationIssue list
        Errors : CompilationIssue list
    }

type internal CompilationResult = 
    | Success of CompilationIssue list
    | Failure of FailureDetails

[<RequireQualifiedAccess>]
type internal CompilationOutput = 
    { Errors : Diagnostic[]
      Warnings : Diagnostic[]  }

type internal InProcCompiler(legacyReferenceResolver) = 
    member this.Compile(argv) = 

        // Explanation: Compilation happens on whichever thread calls this function.
        let ctok = AssumeCompilationThreadWithoutEvidence ()

        let loggerProvider = InProcErrorLoggerProvider()
        let mutable exitCode = 0
        let exiter = 
            { new Exiter with
                 member this.Exit n = exitCode <- n; raise StopProcessing }
        try 
            mainCompile(ctok, argv, legacyReferenceResolver, false, ReduceMemoryFlag.Yes, CopyFSharpCoreFlag.Yes, exiter, loggerProvider.Provider, None, None)
        with 
            | StopProcessing -> ()
            | ReportedError _  | WrappedError(ReportedError _,_)  ->
                exitCode <- 1
                ()

        let output : CompilationOutput = { Warnings = loggerProvider.CapturedWarnings; Errors = loggerProvider.CapturedErrors }
        exitCode = 0, output

/// in-proc version of fsc.exe
type internal FscCompiler(legacyReferenceResolver) =
    let compiler = InProcCompiler(legacyReferenceResolver)

    let emptyLocation = 
        { 
            StartColumn = 0
            EndColumn = 0
            StartLine = 0
            EndLine = 0
        }

    /// converts short and long issue types to the same CompilationIssue representation
    let convert issue : CompilationIssue = 
        match issue with
        | Diagnostic.Short(severity, text) -> 
            {
                Location = emptyLocation
                Code = ""
                Subcategory = ""
                File = ""
                Text = text
                Type = if (severity = FSharpDiagnosticSeverity.Error) then CompilationIssueType.Error else CompilationIssueType.Warning
            }
        | Diagnostic.Long(severity, details) ->
            let loc, file = 
                match details.Location with
                | Some l when not l.IsEmpty -> 
                    { 
                        StartColumn = l.Range.StartColumn
                        EndColumn = l.Range.EndColumn
                        StartLine = l.Range.StartLine
                        EndLine = l.Range.EndLine
                    }, l.File
                | _ -> emptyLocation, ""
            {
                Location = loc
                Code = sprintf "FS%04d" details.Canonical.ErrorNumber
                Subcategory = details.Canonical.Subcategory
                File = file
                Text = details.Message
                Type = if (severity = FSharpDiagnosticSeverity.Error) then CompilationIssueType.Error else CompilationIssueType.Warning
            }

    /// test if --test:ErrorRanges flag is set
    let errorRangesArg =
        let regex = Regex(@"^(/|--)test:ErrorRanges$", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)
        fun arg -> regex.IsMatch(arg)

    /// test if --vserrors flag is set
    let vsErrorsArg =
        let regex = Regex(@"^(/|--)vserrors$", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)
        fun arg -> regex.IsMatch(arg)

    /// test if an arg is a path to fsc.exe
    let fscExeArg = 
        let regex = Regex(@"fsc(\.exe)?$", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)
        fun arg -> regex.IsMatch(arg)

    /// do compilation as if args was argv to fsc.exe
    member this.Compile(args : string array) =
        // args.[0] is later discarded, assuming it is just the path to fsc.
        // compensate for this in case caller didn't know
        let args =
            match box args with
            | Null -> [|"fsc"|]
            | _ -> 
            match args with
            | [||] -> [|"fsc"|]
            | a when not <| fscExeArg a.[0] -> Array.append [|"fsc"|] a
            | _ -> args

        let errorRanges = args |> Seq.exists errorRangesArg
        let vsErrors = args |> Seq.exists vsErrorsArg

        let (ok, result) = compiler.Compile(args)
        let exitCode = if ok then 0 else 1
        
        let lines =
            Seq.append result.Errors result.Warnings
            |> Seq.map convert
            |> Seq.map (fun issue ->
                let issueTypeStr = 
                    match issue.Type with
                    | Error -> if vsErrors then sprintf "%s error" issue.Subcategory else "error"
                    | Warning -> if vsErrors then sprintf "%s warning" issue.Subcategory else "warning"

                let locationStr =
                    if vsErrors then
                        sprintf "(%d,%d,%d,%d)" issue.Location.StartLine issue.Location.StartColumn issue.Location.EndLine issue.Location.EndColumn
                    elif errorRanges then
                        sprintf "(%d,%d-%d,%d)" issue.Location.StartLine issue.Location.StartColumn issue.Location.EndLine issue.Location.EndColumn
                    else
                        sprintf "(%d,%d)" issue.Location.StartLine issue.Location.StartColumn

                sprintf "%s: %s %s: %s" locationStr issueTypeStr issue.Code issue.Text
                )
            |> Array.ofSeq
        (exitCode, lines)

module internal CompilerHelpers =

    /// splits a provided command line string into argv array
    /// currently handles quotes, but not escaped quotes
    let parseCommandLine (commandLine : string) =
        let folder (inQuote : bool, currArg : string, argLst : string list) ch =
            match (ch, inQuote) with
            | ('"', _) ->
                (not inQuote, currArg, argLst)
            | (' ', false) ->
                if currArg.Length > 0 then (inQuote, "", currArg :: argLst)
                else (inQuote, "", argLst)
            | _ ->
                (inQuote, currArg + (string ch), argLst)

        seq { yield! commandLine.ToCharArray(); yield ' ' }
        |> Seq.fold folder (false, "", [])
        |> (fun (_, _, args) -> args)
        |> List.rev
        |> Array.ofList

    /// runs in-proc fsc compilation, returns array consisting of exit code, then compiler output
    let fscCompile legacyReferenceResolver directory args =
        // in-proc compiler still prints banner to console, so need this to capture it
        let origOut = Console.Out
        let origError = Console.Error
        let sw = new StringWriter()
        Console.SetOut(sw)
        let ew = new StringWriter()
        Console.SetError(ew)
        try
            try
                Directory.SetCurrentDirectory directory
                let (exitCode, output) = FscCompiler(legacyReferenceResolver).Compile(args)
                let consoleOut = sw.ToString().Split([|'\r'; '\n'|], StringSplitOptions.RemoveEmptyEntries)
                let consoleError = ew.ToString().Split([|'\r'; '\n'|], StringSplitOptions.RemoveEmptyEntries)
                exitCode, [| yield! consoleOut; yield! output |], consoleError
            with e ->
                1, [| "Internal compiler error"; e.ToString().Replace('\n', ' ').Replace('\r', ' ') |], [| |]
        finally
            Console.SetOut(origOut)
            Console.SetError(origError)
    

