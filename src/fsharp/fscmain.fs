// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.CommandLineMain

open System
open System.Reflection
open System.Runtime.CompilerServices

open Internal.Utilities.Library 
open Internal.Utilities.Library.Extras
open FSharp.Compiler.AbstractIL
open FSharp.Compiler.AbstractIL.ILBinaryReader 
open FSharp.Compiler.CompilerConfig
open FSharp.Compiler.Driver
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text

[<Dependency("FSharp.Compiler.Service",LoadHint.Always)>] 
do ()

[<EntryPoint>]
let main(argv) =
    
    // Set the garbage collector to batch mode, which improves overall performance.
    System.Runtime.GCSettings.LatencyMode <- System.Runtime.GCLatencyMode.Batch
    
    // Set the initial phase to garbage collector to batch mode, which improves overall performance.
    use unwindBuildPhase = PushThreadBuildPhaseUntilUnwind BuildPhase.Parameter

    // An SDL recommendation
    UnmanagedProcessExecutionOptions.EnableHeapTerminationOnCorruption()

    try 

        // We are on a compilation thread
        let ctok = AssumeCompilationThreadWithoutEvidence ()

        // The F# compiler expects 'argv' to include the executable name, though it makes no use of it.
        let argv = Array.append [| "fsc.exe" |] argv
        
        // Check for --pause as the very first step so that a debugger can be attached here.
        let pauseFlag = argv |> Array.exists  (fun x -> x = "/pause" || x = "--pause")
        if pauseFlag then 
            System.Console.WriteLine("Press return to continue...")
            System.Console.ReadLine() |> ignore

        // Set up things for the --times testing flag
#if !FX_NO_APP_DOMAINS
        let timesFlag = argv |> Array.exists  (fun x -> x = "/times" || x = "--times")
        if timesFlag then 
            let stats = ILBinaryReader.GetStatistics()
            AppDomain.CurrentDomain.ProcessExit.Add(fun _ -> 
                printfn "STATS: #ByteArrayFile = %d, #MemoryMappedFileOpen = %d, #MemoryMappedFileClosed = %d, #RawMemoryFile = %d, #WeakByteArrayFile = %d" 
                    stats.byteFileCount 
                    stats.memoryMapFileOpenedCount 
                    stats.memoryMapFileClosedCount 
                    stats.rawMemoryFileCount 
                    stats.weakByteFileCount)
#endif

        // This object gets invoked when two many errors have been accumulated, or an abort-on-error condition
        // has been reached (e.g. type checking failed, so don't proceed to optimization).
        let quitProcessExiter = 
            { new Exiter with 
                member x.Exit(n) =                    
                    try 
                      exit n
                    with _ -> 
                      ()            
                    failwithf "%s" (FSComp.SR.elSysEnvExitDidntExit())
            }

        // Get the handler for legacy resolution of references via MSBuild.
        let legacyReferenceResolver = 
#if CROSS_PLATFORM_COMPILER
            SimulatedMSBuildReferenceResolver.SimulatedMSBuildResolver
#else
            LegacyMSBuildReferenceResolver.getResolver()
#endif

        // Perform the main compilation.
        //
        // This is the only place where ReduceMemoryFlag.No is set. This is because fsc.exe is not a long-running process and
        // thus we can use file-locking memory mapped files.
        //
        // This is also one of only two places where CopyFSharpCoreFlag.Yes is set.  The other is in LegacyHostedCompilerForTesting.
        mainCompile (ctok, argv, legacyReferenceResolver, (*bannerAlreadyPrinted*)false, ReduceMemoryFlag.No, CopyFSharpCoreFlag.Yes, quitProcessExiter, ConsoleLoggerProvider(), None, None)
        0 

    with e -> 
        // Last-chance error recovery (note, with a poor error range)
        errorRecovery e Range.range0
        1
