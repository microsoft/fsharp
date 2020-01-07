﻿namespace FSharp.Compiler.Scripting.UnitTests

open System
open System.Collections.Generic
open System.IO
open System.Text

type internal CapturedTextReader() =
    inherit TextReader()
    let queue = Queue<char>()
    member __.ProvideInput(text: string) =
        for c in text.ToCharArray() do
            queue.Enqueue(c)
    override __.Peek() =
        if queue.Count > 0 then queue.Peek() |> int
        else -1
    override __.Read() =
        if queue.Count > 0 then queue.Dequeue() |> int
        else -1

type internal RedirectConsoleInput() =
    let oldStdIn = Console.In
    let newStdIn = new CapturedTextReader()
    do Console.SetIn(newStdIn)
    member __.ProvideInput(text: string) =
        newStdIn.ProvideInput(text)
    interface IDisposable with
        member __.Dispose() =
            Console.SetIn(oldStdIn)
            newStdIn.Dispose()

type internal EventedTextWriter() =
    inherit TextWriter()
    let sb = StringBuilder()
    let lineWritten = Event<string>()
    member __.LineWritten = lineWritten.Publish
    override __.Encoding = Encoding.UTF8
    override __.Write(c: char) =
        if c = '\n' then
            let line =
                let v = sb.ToString()
                if v.EndsWith("\r") then v.Substring(0, v.Length - 1)
                else v
            sb.Clear() |> ignore
            lineWritten.Trigger(line)
        else sb.Append(c) |> ignore

type internal RedirectConsoleOutput() =
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
    member __.OutputProduced = outputProduced.Publish
    member __.ErrorProduced = errorProduced.Publish
    interface IDisposable with
        member __.Dispose() =
            Console.SetOut(oldStdOut)
            Console.SetError(oldStdErr)
            newStdOut.Dispose()
            newStdErr.Dispose()
