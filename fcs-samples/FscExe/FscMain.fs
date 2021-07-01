// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.CommandLineMain

open System
open System.Diagnostics
open System.IO
open System.Reflection
open System.Runtime.CompilerServices
open FSharp.Compiler.SourceCodeServices
open FSharp.Compiler.AbstractIL.Utils // runningOnMono
open FSharp.Compiler.AbstractIL.Library
open FSharp.Compiler.ErrorLogger

#if RESIDENT_COMPILER
type TypeInThisAssembly() = member x.Dummy = 1

let progress = ref false

/// Implement the optional resident compilation service
module FSharpResidentCompiler = 

    open System.Runtime.Remoting.Channels
    open System.Runtime.Remoting
    open System.Runtime.Remoting.Lifetime
    open System.Text

    /// Collect the output from the stdout and stderr streams, character by character,
    /// recording the console color used along the way.
    type private OutputCollector() = 
        let output = ResizeArray()
        let outWriter isOut = 
            { new TextWriter() with 
                 member x.Write(c:char) = lock output (fun () -> output.Add (isOut, (try Some Console.ForegroundColor with _ -> None) ,c)) 
                 member x.Encoding = Encoding.UTF8 }
        do Console.SetOut (outWriter true)
        do Console.SetError (outWriter false)
        member x.GetTextAndClear() = lock output (fun () -> let res = output.ToArray() in output.Clear(); res)

    /// The compilation server, which runs in the server process. Accessed by clients using .NET remoting.
    type FSharpCompilationServer()  =
        inherit MarshalByRefObject()  

        static let onWindows = 
            match System.Environment.OSVersion.Platform with 
            | PlatformID.Win32NT | PlatformID.Win32S | PlatformID.Win32Windows | PlatformID.WinCE -> true
            | _  -> false

        // The channel/socket name is qualified by the user name (and domain on windows)
        static let domainName = if onWindows then Environment.GetEnvironmentVariable "USERDOMAIN" else ""
        static let userName = Environment.GetEnvironmentVariable (if onWindows then "USERNAME" else "USER") 
        // Use different base channel names on mono and CLR as a CLR remoting process can't talk
        // to a mono server
        static let baseChannelName = 
            if runningOnMono then 
                "FSCChannelMono" 
            else 
                "FSCChannel"
        static let channelName = baseChannelName + "_" +  domainName + "_" + userName
        static let serverName = 
            if runningOnMono then 
                "FSCServerMono" 
            else
                "FSCSever"
        static let mutable serverExists = true
        
        let outputCollector = new OutputCollector()

        // This background agent ensures all compilation requests sent to the server are serialized
        let agent = MailboxProcessor<_>.Start(fun inbox -> 
                       async { 
                          while true do 
                              let! (pwd,argv, reply: AsyncReplyChannel<_>) = inbox.Receive()
                              if !progress then printfn "server agent: got compilation request, argv = %A" argv
                              Environment.CurrentDirectory <- pwd
                              let errors, exitCode = FSharpChecker.Create().Compile (argv) |> Async.RunSynchronously
                              for error in errors do eprintfn "%s" (error.ToString())
                              if !progress then printfn "server: finished compilation request, argv = %A" argv
                              let output = outputCollector.GetTextAndClear()
                              reply.Reply(output, exitCode)
                              GC.Collect(3)
                              // Exit the server if there are no outstanding requests and the 
                              // current memory usage after collection is over 200MB
                              if inbox.CurrentQueueLength = 0 && GC.GetTotalMemory(true) > 200L * 1024L * 1024L then 
                                  exit 0
                       })

        member x.Run() = 
            while serverExists do 
               if !progress then printfn "server: startup thread sleeping..." 
               System.Threading.Thread.Sleep 1000

        abstract Ping : unit -> string
        abstract Compile : string * string[] -> (bool * System.ConsoleColor option * char) [] * int
        default x.Ping() = "ping"
        default x.Compile (pwd,argv) = 
            if !progress then printfn "server: got compilation request, (pwd, argv) = %A" (pwd, argv)
            let res = agent.PostAndReply(fun reply -> (pwd,argv,reply))
            if !progress then printfn "server: got response, response = %A" res
            res 
            
        override x.Finalize() =
            serverExists <- false

        // This is called on the server object by .NET remoting to initialize the lifetime characteristics
        // of the server object.
        override x.InitializeLifetimeService() =
            let lease = (base.InitializeLifetimeService() :?> ILease)
            if (lease.CurrentState = LeaseState.Initial)  then
                lease.InitialLeaseTime <- TimeSpan.FromDays(1.0);
                lease.SponsorshipTimeout <- TimeSpan.FromMinutes(2.0);
                lease.RenewOnCallTime <- TimeSpan.FromDays(1.0);
            box lease
            
        static member RunServer() =
            if !progress then printfn "server: initializing server object" 
            let server = new FSharpCompilationServer()
            let chan = new Ipc.IpcChannel(channelName) 
            ChannelServices.RegisterChannel(chan,false);
            RemotingServices.Marshal(server,serverName)  |> ignore

            // On Unix, the file permissions of the implicit socket need to be set correctly to make this
            // private to the user.
            if runningOnMono then 
              try 
                  let monoPosix = System.Reflection.Assembly.Load(new System.Reflection.AssemblyName("Mono.Posix, Version=2.0.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756"))
                  let monoUnixFileInfo = monoPosix.GetType("Mono.Unix.UnixFileSystemInfo") 
                  let socketName = Path.Combine(FileSystem.GetTempPathShim(), channelName)
                  let fileEntry = monoUnixFileInfo.InvokeMember("GetFileSystemEntry", (BindingFlags.InvokeMethod ||| BindingFlags.Static ||| BindingFlags.Public), null, null, [| box socketName |],System.Globalization.CultureInfo.InvariantCulture)
                  // Add 0x00000180 (UserReadWriteExecute) to the access permissions on Unix
                  monoUnixFileInfo.InvokeMember("set_FileAccessPermissions", (BindingFlags.InvokeMethod ||| BindingFlags.Instance ||| BindingFlags.Public), null, fileEntry, [| box 0x00000180 |],System.Globalization.CultureInfo.InvariantCulture) |> ignore
#if DEBUG
                  if !progress then printfn "server: good, set permissions on socket name '%s'"  socketName
                  let fileEntry = monoUnixFileInfo.InvokeMember("GetFileSystemEntry", (BindingFlags.InvokeMethod ||| BindingFlags.Static ||| BindingFlags.Public), null, null, [| box socketName |],System.Globalization.CultureInfo.InvariantCulture)
                  let currPermissions = monoUnixFileInfo.InvokeMember("get_FileAccessPermissions", (BindingFlags.InvokeMethod ||| BindingFlags.Instance ||| BindingFlags.Public), null, fileEntry, [| |],System.Globalization.CultureInfo.InvariantCulture) |> unbox<int>
                  if !progress then printfn "server: currPermissions = '%o' (octal)"  currPermissions
#endif
              with e -> 
#if DEBUG
                  printfn "server: failed to set permissions on socket, perhaps on windows? Is is not needed there."  
#endif
                  ()
                  // Fail silently
            server.Run()
            
        static member private ConnectToServer() =
            Activator.GetObject(typeof<FSharpCompilationServer>,"ipc://" + channelName + "/" + serverName) 
            :?> FSharpCompilationServer 

        static member TryCompileUsingServer(fscServerExe,argv) =
            // Enable these lines to write a log file, e.g. when running under xbuild
            //let os = System.IO.File.CreateText "/tmp/fsc-client-log"
            //let printfn fmt = Printf.kfprintf (fun () -> fprintfn os ""; os.Flush()) os fmt
            let pwd = System.Environment.CurrentDirectory
            let clientOpt = 
                if !progress then printfn "client: creating client"
                // Detect the absence of the channel via the exception. Probably not the best way.
                // Different exceptions get thrown here on Mono and Windows.
                let client = FSharpCompilationServer.ConnectToServer()
                try 
                    if !progress then printfn "client: attempting to connect to existing service (1)"
                    client.Ping() |> ignore
                    if !progress then printfn "client: connected to existing service"
                    Some client
                with _ ->
                    if !progress then printfn "client: error while creating client, starting client instead"
                    let procInfo = 
                        if runningOnMono then
                            let shellName, useShellExecute = 
                                match System.Environment.GetEnvironmentVariable("FSC_MONO") with 
                                | Null -> 
                                    if onWindows then 
                                        // e.g. "C:\Program Files\Mono-2.6.1\lib\mono\2.0\mscorlib.dll" --> "C:\Program Files\Mono-2.6.1\bin\mono.exe"
                                        Path.Combine(Path.GetDirectoryName (typeof<Object>.Assembly.Location), @"..\..\..\bin\mono.exe"), false
                                    else
                                        "mono-sgen", true
                                | NonNull path -> path, true
                                     
                            ProcessStartInfo(FileName = shellName,
                                             Arguments = fscServerExe + " /server",
                                             CreateNoWindow = true,
                                             UseShellExecute = useShellExecute)
                         else
                            ProcessStartInfo(FileName=fscServerExe,
                                             Arguments = "/server",
                                             CreateNoWindow = true,
                                             UseShellExecute = false)
                    let cmdProcess = new Process(StartInfo=procInfo)

                    //let exitE = cmdProcess.Exited |> Observable.map (fun x -> x)

                    cmdProcess.Start() |> ignore
                    //exitE.Add(fun _ -> if !progress then eprintfn "client: the server has exited")
                    cmdProcess.EnableRaisingEvents <- true;
                     
                    // Create the client proxy and attempt to connect to the server
                    let rec tryAcccesServer nRemaining =
                        if !progress then printfn "client: trying to access server, nRemaining = '%d'" nRemaining
                        if nRemaining = 0 then 
                            // Failed to connect to server, give up 
                            None
                        else
                            try 
                                if !progress then printfn "client: attempting to connect to existing service (2)"
                                client.Ping() |> ignore
                                if !progress then printfn "client: connected to existing service"
                                Some client
                            // Detect the absence of the channel via the exception. Probably not the best way.
                            // Different exceptions get thrown here on Mono and Windows.
                            with _ (* System.Runtime.Remoting.RemotingException *) ->
                                // Sleep a bit
                                System.Threading.Thread.Sleep 50
                                tryAcccesServer (nRemaining - 1)

                    tryAcccesServer 20

            match clientOpt with
            | Some client -> 
                if !progress then printfn "client: calling client.Compile(%A)" argv
                // Install the global error logger and never remove it. This logger does have all command-line flags considered.
                try 
                    let (output, exitCode) = 
                        try client.Compile (pwd, argv) 
                        with e -> 
                           printfn "server error: %s" (e.ToString())
                           failwith "remoting error"
                        
                    if !progress then printfn "client: returned from client.Compile(%A), res = %d" argv exitCode
                    use holder = 
                        try let originalConsoleColor = Console.ForegroundColor 
                            { new System.IDisposable with member x.Dispose() = Console.ForegroundColor <- originalConsoleColor }
                        with _ -> null
                    let mutable prevConsoleColor = try Console.ForegroundColor with _ -> ConsoleColor.Black
                    for (isOut, consoleColorOpt, c:char) in output do 
                        try match consoleColorOpt with 
                             | Some consoleColor -> 
                                 if prevConsoleColor <> consoleColor then 
                                     Console.ForegroundColor <- consoleColor; 
                             | None -> ()
                        with _ -> ()
                        c |> (if isOut then Console.Out.Write else Console.Error.Write)
                    Some exitCode
                with err -> 
                   eprintfn "%s" (err.ToString())
                   // We continue on and compile in-process - the server appears to have died half way through.
                   None
            | None -> 
                None
#endif

module Driver = 
    let main argv = 
        let inline hasArgument name args = 
            args |> Array.exists (fun x -> x = ("--" + name) || x = ("/" + name))
        let inline stripArgument name args = 
            args |> Array.filter (fun x -> x <> ("--" + name) && x <> ("/" + name))

        // Check for --pause as the very first step so that a compiler can be attached here.
        if hasArgument "pause" argv then 
            System.Console.WriteLine("Press any key to continue...")
            System.Console.ReadKey() |> ignore
      
#if RESIDENT_COMPILER
        if runningOnMono && hasArgument "resident" argv then 
            let argv = stripArgument "resident" argv

            let fscServerExe = typeof<TypeInThisAssembly>.Assembly.Location
            let exitCodeOpt = FSharpResidentCompiler.FSharpCompilationServer.TryCompileUsingServer (fscServerExe, argv)
            match exitCodeOpt with 
            | Some exitCode -> exitCode
            | None -> 
                let errors, exitCode = FSharpChecker.Create().Compile (argv)  |> Async.RunSynchronously
                for error in errors do eprintfn "%s" (error.ToString())
                exitCode

        elif runningOnMono && hasArgument "server" argv then 
            FSharpResidentCompiler.FSharpCompilationServer.RunServer()        
            0
#endif        
        else
            let errors, exitCode = FSharpChecker.Create().Compile (argv) |> Async.RunSynchronously
            for error in errors do eprintfn "%s" (error.ToString())
            exitCode

[<Dependency("FSharp.Compiler",LoadHint.Always)>] 
do ()

[<EntryPoint>]
let main(argv) =
    System.Runtime.GCSettings.LatencyMode <- System.Runtime.GCLatencyMode.Batch
    use unwindBuildPhase = PushThreadBuildPhaseUntilUnwind BuildPhase.Parameter

//#if NO_HEAPTERMINATION
//#else
//    if not runningOnMono then Lib.UnmanagedProcessExecutionOptions.EnableHeapTerminationOnCorruption() (* SDL recommendation *)
//    Lib.UnmanagedProcessExecutionOptions.EnableHeapTerminationOnCorruption() (* SDL recommendation *)
//#endif

    try 
        Driver.main(Array.append [| "fsc.exe" |] argv); 
    with e -> 
        errorRecovery e FSharp.Compiler.Range.range0; 
        1
