(*
msbuild tests\fsharp\perf\tasks\FS\TaskPerf.fsproj /p:Configuration=Release
dotnet artifacts\bin\TaskPerf\Release\netcoreapp2.1\TaskPerf.dll
*)

namespace TaskPerf

//open FSharp.Control.Tasks
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open BenchmarkDotNet.Configs
open BenchmarkDotNet.Diagnosers
open System.Runtime.CompilerServices

module Code =
    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let condition_2 x =
        if  (x = 1 || x = 2)   then 1
        elif(x = 3 || x = 4)   then 2

        else 8

[<DisassemblyDiagnoser>]
[<HardwareCounters(HardwareCounter.BranchMispredictions,HardwareCounter.BranchInstructions)>]
type Benchmarks() =

    [<Benchmark>]
    [<Arguments(1)>]
    [<Arguments(2)>]
    [<Arguments(3)>]
    [<Arguments(4)>]
    member _.CSharp(x: int) =
        MicroPerfCSharp.Cond(x)

    [<Benchmark>]
    [<Arguments(1)>]
    [<Arguments(2)>]
    [<Arguments(3)>]
    [<Arguments(4)>]
    member _.FSharp(x: int) =
        Code.condition_2(x)

[<SimpleJob(launchCount = 2, warmupCount = 1, targetCount = 2)>]
[<GcServer(true)>]
[<MemoryDiagnoser>]
[<MarkdownExporterAttribute.GitHub>]
type AsyncWhileMemoryBench() =

  [<Params((* 0, 1, 100, *) 1000, 10000)>]
  member val Length = 0 with get, set

  [<Benchmark>]
  member x.Run() =
    async {
      let mutable i = 0
      while i < x.Length do
        i <- i + 1
      return i
    } |> Async.StartAsTask

module Main = 

    [<EntryPoint>]
    let main argv = 
        printfn "Running benchmarks..."
        //let results = BenchmarkRunner.Run<Benchmarks>()
        let results = BenchmarkRunner.Run<AsyncWhileMemoryBench>()
        0  
