// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.
//
// Scripting utilities
//=========================================================================================

namespace global

open System
open System.IO
open System.Text
open System.Diagnostics

[<AutoOpen>]
module Scripting =

    let isNullOrEmpty s = String.IsNullOrEmpty s

    let executeProcess filename arguments =
        let processWriteMessage (chan:TextWriter) (message:string) =
            if message <> null then 
                chan.WriteLine(message) 
        printfn "%s %s" filename arguments
        let info = ProcessStartInfo(Arguments=arguments, UseShellExecute=false, 
                                    RedirectStandardOutput=true, RedirectStandardError=true,
                                    CreateNoWindow=true, FileName=filename)
        let p = new Process(StartInfo=info)
        p.OutputDataReceived.Add(fun x -> processWriteMessage stdout x.Data)
        p.ErrorDataReceived.Add(fun x ->  processWriteMessage stderr x.Data)
        if p.Start() then
            p.BeginOutputReadLine()
            p.BeginErrorReadLine()
            p.WaitForExit()
            p.ExitCode
        else
            0

#if INTERACTIVE
    let argv = FSharp.Compiler.Interactive.Settings.fsi.CommandLineArgs |> Seq.skip 1 |> Seq.toArray

    let getCmdLineArgOptional (switchName: string) =
        argv |> Array.filter(fun t -> t.StartsWith(switchName)) |> Array.map(fun t -> t.Remove(0, switchName.Length).Trim()) |> Array.tryHead 

    let getCmdLineArg switchName defaultValue = 
        match getCmdLineArgOptional switchName with
        | Some file -> file
        | _ -> defaultValue

    let getCmdLineArgReqd switchName = 
        match getCmdLineArg switchName null with 
        | null -> failwith (sprintf "The argument %s is required" switchName)
        | x -> x

    let getCmdLineExtraArgs isSwitch = argv |> Array.skipWhile isSwitch 

#endif

    let makeDirectory output =
        if not (Directory.Exists(output)) then 
            Directory.CreateDirectory(output) |> ignore

    let (++) a b = Path.Combine(a,b)

    let getBasename (a:string) = Path.GetFileNameWithoutExtension a
    let getFullPath (a:string) = Path.GetFullPath a
    let getFilename (a:string) = Path.GetFileName a
    let getDirectoryName (a:string) = Path.GetDirectoryName a

    let copyFile (source:string) dir =
        let dest = 
            if not (Directory.Exists dir) then Directory.CreateDirectory dir |>ignore
            let result = Path.Combine(dir, getFilename source)
            result
        //printfn "Copy %s --> %s" source dest
        File.Copy(source, dest, true)

    let deleteDirectory output =
        if Directory.Exists output then 
            Directory.Delete(output, true) 


    let log format = printfn format

    type FilePath = string

    type CmdResult = 
        | Success
        | ErrorLevel of string * int

    type CmdArguments = 
        { RedirectOutput : (string -> unit) option
          RedirectError : (string -> unit) option
          RedirectInput : (StreamWriter -> unit) option }

    module Process =

        let processExePath baseDir (exe:string) =
            if Path.IsPathRooted(exe) then exe
            else 
                match getDirectoryName exe with
                | "" -> exe
                | _ -> Path.Combine(baseDir,exe) |> Path.GetFullPath

        let exec cmdArgs (workDir: FilePath) envs (path: FilePath) arguments =

            let exePath = path |> processExePath workDir
            let processInfo = new ProcessStartInfo(exePath, arguments)
            
            processInfo.EnvironmentVariables.["DOTNET_ROLL_FORWARD"] <- "LatestMajor"
            processInfo.EnvironmentVariables.["DOTNET_ROLL_FORWARD_TO_PRERELEASE"] <- "1"
            
            processInfo.CreateNoWindow <- true
            processInfo.UseShellExecute <- false
            processInfo.WorkingDirectory <- workDir

#if !NET46
            ignore envs  // work out what to do about this
#else
            envs
            |> Map.iter (fun k v -> processInfo.EnvironmentVariables.[k] <- v)
#endif

            let p = new Process()
            p.EnableRaisingEvents <- true
            p.StartInfo <- processInfo
            let out = StringBuilder()
            let err = StringBuilder()

            cmdArgs.RedirectOutput|> Option.iter (fun f ->
                processInfo.RedirectStandardOutput <- true
                p.OutputDataReceived.Add (fun ea -> 
                    if ea.Data <> null then 
                        out.Append(ea.Data + Environment.NewLine) |> ignore
                        f ea.Data)
            )

            cmdArgs.RedirectError |> Option.iter (fun f ->
                processInfo.RedirectStandardError <- true
                p.ErrorDataReceived.Add (fun ea -> 
                    if ea.Data <> null then 
                        err.Append(ea.Data + Environment.NewLine) |> ignore
                        f ea.Data)
            )

            cmdArgs.RedirectInput
            |> Option.iter (fun _ -> p.StartInfo.RedirectStandardInput <- true)

            p.Start() |> ignore

            cmdArgs.RedirectOutput |> Option.iter (fun _ -> p.BeginOutputReadLine())
            cmdArgs.RedirectError |> Option.iter (fun _ -> p.BeginErrorReadLine())

            cmdArgs.RedirectInput |> Option.iter (fun input -> 
               async {
                let inputWriter = p.StandardInput
                do! inputWriter.FlushAsync () |> Async.AwaitIAsyncResult |> Async.Ignore
                input inputWriter
                do! inputWriter.FlushAsync () |> Async.AwaitIAsyncResult |> Async.Ignore
                inputWriter.Dispose ()
               } 
               |> Async.Start)

            p.WaitForExit() 

            match p.ExitCode with
            | 0 -> Success
            | errCode ->
                let msg = sprintf "Error running command '%s' with args '%s' in directory '%s'.\n---- stdout below --- \n%s\n---- stderr below --- \n%s " exePath arguments workDir (out.ToString()) (err.ToString())
                ErrorLevel (msg, errCode)

    type OutPipe (writer: TextWriter) =
        member x.Post (msg:string) = lock writer (fun () -> writer.WriteLine(msg))
        interface System.IDisposable with 
           member _.Dispose() = writer.Flush()

    let redirectTo (writer: TextWriter) = new OutPipe (writer)

    let redirectToLog () = redirectTo System.Console.Out

#if !NETCOREAPP
    let defaultPlatform = 
        match Environment.OSVersion.Platform, Environment.Is64BitOperatingSystem with 
        | PlatformID.MacOSX, true -> "osx.10.11-x64"
        | PlatformID.Unix,true -> "ubuntu.14.04-x64"
        | _, true -> "win7-x64"
        | _, false -> "win7-x86"
#endif

    let executeProcessNoRedirect filename arguments =
        let info = ProcessStartInfo(Arguments=arguments, UseShellExecute=false, 
                                    RedirectStandardOutput=true, RedirectStandardError=true,RedirectStandardInput=true,
                                    CreateNoWindow=true, FileName=filename)
        let p = new Process(StartInfo=info)
        if p.Start() then

            async { try 
                      let buffer = Array.zeroCreate 4096
                      while not p.StandardOutput.EndOfStream do 
                        let n = p.StandardOutput.Read(buffer, 0, buffer.Length)
                        if n > 0 then System.Console.Out.Write(buffer, 0, n)
                    with _ -> () } |> Async.Start
            async { try 
                      let buffer = Array.zeroCreate 4096
                      while not p.StandardError.EndOfStream do 
                        let n = p.StandardError.Read(buffer, 0, buffer.Length)
                        if n > 0 then System.Console.Error.Write(buffer, 0, n)
                    with _ -> () } |> Async.Start
            async { try 
                      while true do 
                        let c = System.Console.In.ReadLine()
                        p.StandardInput.WriteLine(c)
                    with _ -> () } |> Async.Start
            p.WaitForExit()
            p.ExitCode
        else
            0
