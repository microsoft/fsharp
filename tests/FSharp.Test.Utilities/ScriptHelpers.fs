// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Test.ScriptHelpers

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Threading
open FSharp.Compiler
open FSharp.Compiler.Interactive.Shell
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.EditorServices

type CapturedTextReader() =
    inherit TextReader()
    let queue = Queue<char>()
    member _.ProvideInput(text: string) =
        for c in text.ToCharArray() do
            queue.Enqueue(c)
    override _.Peek() =
        if queue.Count > 0 then queue.Peek() |> int else -1
    override _.Read() =
        if queue.Count > 0 then queue.Dequeue() |> int else -1

type RedirectConsoleInput() =
    let oldStdIn = Console.In
    let newStdIn = new CapturedTextReader()
    do Console.SetIn(newStdIn)
    member _.ProvideInput(text: string) =
        newStdIn.ProvideInput(text)
    interface IDisposable with
        member _.Dispose() =
            Console.SetIn(oldStdIn)
            newStdIn.Dispose()

type EventedTextWriter() =
    inherit TextWriter()
    let sb = StringBuilder()
    let lineWritten = Event<string>()
    member _.LineWritten = lineWritten.Publish
    override _.Encoding = Encoding.UTF8
    override _.Write(c: char) =
        if c = '\n' then
            let line =
                let v = sb.ToString()
                if v.EndsWith("\r") then v.Substring(0, v.Length - 1)
                else v
            sb.Clear() |> ignore
            lineWritten.Trigger(line)
        else sb.Append(c) |> ignore

type RedirectConsoleOutput() =
    let outputProduced = Event<string>()
    let errorProduced = Event<string>()
    let oldStdOut = Console.Out
    let oldStdErr = Console.Error
    let newStdOut = new EventedTextWriter()
    let newStdErr = new EventedTextWriter()
    do newStdOut.LineWritten.Add outputProduced.Trigger
    do newStdErr.LineWritten.Add errorProduced.Trigger
    do Console.SetOut(newStdOut)
    do Console.SetError(newStdErr)
    member _.OutputProduced = outputProduced.Publish
    member _.ErrorProduced = errorProduced.Publish
    interface IDisposable with
        member _.Dispose() =
            Console.SetOut(oldStdOut)
            Console.SetError(oldStdErr)
            newStdOut.Dispose()
            newStdErr.Dispose()


[<RequireQualifiedAccess>]
type LangVersion =
    | V47
    | V50
    | Preview

type FSharpScript(?additionalArgs: string[], ?quiet: bool, ?langVersion: LangVersion) =

    let additionalArgs = defaultArg additionalArgs [||]
    let quiet = defaultArg quiet true
    let langVersion = defaultArg langVersion LangVersion.Preview
    let config = FsiEvaluationSession.GetDefaultConfiguration()

    let computedProfile =
        // If we are being executed on the desktop framework (we can tell because the assembly containing int is mscorlib) then profile must be mscorlib otherwise use netcore
        if typeof<int>.Assembly.GetName().Name = "mscorlib" then "mscorlib"
        else "netcore"

    let baseArgs = [|
        typeof<FSharpScript>.Assembly.Location;
        "--noninteractive";
        "--targetprofile:" + computedProfile
        if quiet then "--quiet"
        match langVersion with
        | LangVersion.V47 -> "--langversion:4.7"
        | LangVersion.V50 -> "--langversion:5.0"
        | LangVersion.Preview -> "--langversion:preview"
        |]

    let argv = Array.append baseArgs additionalArgs

    let fsi = FsiEvaluationSession.Create (config, argv, stdin, stdout, stderr)

    member _.ValueBound = fsi.ValueBound

    member _.Fsi = fsi

    member _.Eval(code: string, ?cancellationToken: CancellationToken) =
        let cancellationToken = defaultArg cancellationToken CancellationToken.None
        let ch, errors = fsi.EvalInteractionNonThrowing(code, cancellationToken)
        match ch with
        | Choice1Of2 v -> Ok(v), errors
        | Choice2Of2 ex -> Error(ex), errors

    /// Get the available completion items from the code at the specified location.
    ///
    /// <param name="text">The input text on which completions will be calculated</param>
    /// <param name="line">The 1-based line index</param>
    /// <param name="column">The 0-based column index</param>
    member _.GetCompletionItems(text: string, line: int, column: int) =
        async {
            let parseResults, checkResults, _projectResults = fsi.ParseAndCheckInteraction(text)
            let lineText = text.Split('\n').[line - 1]
            let partialName = QuickParse.GetPartialLongNameEx(lineText, column - 1)
            let declarationListInfos = checkResults.GetDeclarationListInfo(Some parseResults, line, lineText, partialName)
            return declarationListInfos.Items
        }

    interface IDisposable with
        member _.Dispose() =
            (fsi :> IDisposable).Dispose()

[<AutoOpen>]
module TestHelpers =

    let getValue ((value: Result<FsiValue option, exn>), (errors: FSharpDiagnostic[])) =
        if errors.Length > 0 then
            failwith <| sprintf "Evaluation returned %d errors:\r\n\t%s" errors.Length (String.Join("\r\n\t", errors))
        match value with
        | Ok(value) -> value
        | Error ex -> raise ex

    let ignoreValue = getValue >> ignore

    let getTempDir () =
        let sysTempDir = Path.GetTempPath()
        let customTempDirName = Guid.NewGuid().ToString("D")
        let fullDirName = Path.Combine(sysTempDir, customTempDirName)
        let dirInfo = Directory.CreateDirectory(fullDirName)
        { new Object() with
            member _.ToString() = dirInfo.FullName
          interface IDisposable with
            member _.Dispose() =
                dirInfo.Delete(true)
        }
