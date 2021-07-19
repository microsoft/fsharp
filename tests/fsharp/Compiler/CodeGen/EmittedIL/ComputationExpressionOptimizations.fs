// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.UnitTests.CodeGen.EmittedIL

open FSharp.Test
open NUnit.Framework

open System

[<TestFixture>]
module ComputationExpressionOptimizations =

    [<Test>]
    // See https://github.com/fsharp/fslang-design/blob/master/tooling/FST-1034-lambda-optimizations.md
    //
    // This tests a number of code optimizations cooperating together.
    // - InlineIfLambda must be applied
    // - Computed functions must be reduced.
    //
    // This is for the "sync { ... }" builder that simply runs code synchronously - no
    // one uses this in practice but it's a good baseline test for the elimination and redution of constructs.
    //
    let ``check reduction of sample builder for synchronous code``() =
        CompilerAssert.CompileLibraryAndVerifyIL
            """
module Test

open System

type SyncCode<'T> = unit -> 'T

type SyncBuilder() =
    
    member inline _.Delay([<InlineIfLambda>] f: unit -> SyncCode<'T>) :  SyncCode<'T> =
        (fun () -> (f())())

    member inline _.Run([<InlineIfLambda>] code : SyncCode<'T>) : 'T = 
        code()

    [<DefaultValue>]
    member inline _.Zero() : SyncCode< unit> = 
        (fun () -> ())

    member inline _.Return (x: 'T) : SyncCode<'T> =
        (fun () -> x)

    member inline _.Combine([<InlineIfLambda>] code1: SyncCode<unit>, [<InlineIfLambda>] code2: SyncCode<'T>) : SyncCode<'T> =
        (fun () -> 
            code1()
            code2())

    member inline _.While([<InlineIfLambda>] condition: unit -> bool, [<InlineIfLambda>] body: SyncCode<unit>) : SyncCode<unit> =
       (fun () -> 
            while condition() do
                body())

    member inline _.TryWith([<InlineIfLambda>] body: SyncCode<'T>, [<InlineIfLambda>] catch: exn -> SyncCode<'T>) : SyncCode<'T> =
        (fun () -> 
            try
                body()
            with exn -> 
                (catch exn)())

    member inline _.TryFinally([<InlineIfLambda>] body: SyncCode<'T>, compensation: unit -> unit) : SyncCode<'T> =
        (fun () -> 
            let res = 
                try
                    body()
                with _ ->
                    compensation()
                    reraise()
            compensation()
            res)

    member inline this.Using(disp : #IDisposable, [<InlineIfLambda>] body: #IDisposable -> SyncCode<'T>) : SyncCode<'T> = 
        this.TryFinally(
            (fun () -> (body disp)()),
            (fun () -> if not (isNull (box disp)) then disp.Dispose()))

    member inline this.For(sequence : seq<'T>, [<InlineIfLambda>] body : 'T -> SyncCode<unit>) : SyncCode<unit> =
        this.Using (sequence.GetEnumerator(), 
            (fun e -> this.While((fun () -> e.MoveNext()), (fun () -> (body e.Current)()))))

    member inline _.ReturnFrom (value: 'T) : SyncCode<'T> =
        (fun () -> 
              value)

    member inline _.Bind (v: 'TResult1, [<InlineIfLambda>] continuation: 'TResult1 -> SyncCode<'TResult2>) : SyncCode<'TResult2> =
        (fun () -> 
             (continuation v)())

let sync = SyncBuilder()

module Examples =

     let t1 y = 
         sync {
            let x = 4 + 5 + y
            return x
         }

     let testFunctionWithBind y = 
         sync {
            printfn "step"
            let! x = t1 y
            return x + y
         }

     let testFunctionWithWhile y = 
         sync {
            printfn "step"
            while y < 0 do
                let! x = t1 y
                printfn $"step {x}"

            return y + y
         }
     let testFunctionWithTryCatch y = 
         sync {
            try 
               printfn "step"
               let! x = t1 y
               return x + y
            with _ -> 
               return 5
         }
     let testFunctionWithFinally y = 
         sync {
            try 
               printfn "step"
               let! x = t1 y
               return x + y
            finally
               printfn "step"
         }
            """
            (fun verifier -> verifier.VerifyIL [
            // Check testFunctionWithBind is flattened
            """
.method public static int32  testFunctionWithBind(int32 y) cil managed
{
  
  .maxstack  4
  .locals init (class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit> V_0)
  IL_0000:  ldstr      "step"
  IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_000a:  stloc.0
  IL_000b:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_0010:  ldloc.0
  IL_0011:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_0016:  pop
  IL_0017:  ldc.i4.s   9
  IL_0019:  ldarg.0
  IL_001a:  add
  IL_001b:  ldarg.0
  IL_001c:  add
  IL_001d:  ret
} 
            """

            // Check testFunctionWithWhile is flattened
            """
.method public static int32  testFunctionWithWhile(int32 y) cil managed
{
  
  .maxstack  7
  .locals init (class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit> V_0,
           int32 V_1)
  IL_0000:  ldstr      "step"
  IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_000a:  stloc.0
  IL_000b:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_0010:  ldloc.0
  IL_0011:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_0016:  pop
  IL_0017:  ldarg.0
  IL_0018:  ldc.i4.0
  IL_0019:  bge.s      IL_004d
    
  IL_001b:  ldc.i4.s   9
  IL_001d:  ldarg.0
  IL_001e:  add
  IL_001f:  stloc.1
  IL_0020:  ldstr      "step %P()"
  IL_0025:  ldc.i4.1
  IL_0026:  newarr     [runtime]System.Object
  IL_002b:  dup
  IL_002c:  ldc.i4.0
  IL_002d:  ldloc.1
  IL_002e:  box        [runtime]System.Int32
  IL_0033:  stelem     [runtime]System.Object
  IL_0038:  ldnull
  IL_0039:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,int32>::.ctor(string,
                                                                                                                                                                                                                                                                                          object[],
                                                                                                                                                                                                                                                                                          class [runtime]System.Type[])
  IL_003e:  stloc.0
  IL_003f:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_0044:  ldloc.0
  IL_0045:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_004a:  pop
  IL_004b:  br.s       IL_0017
    
  IL_004d:  ldarg.0
  IL_004e:  ldarg.0
  IL_004f:  add
  IL_0050:  ret
} 
            """

            // Check testFunctionWithTryCatch is flattened
            """
.method public static int32  testFunctionWithTryCatch(int32 y) cil managed
{
  
  .maxstack  4
  .locals init (int32 V_0,
           class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit> V_1,
           class [runtime]System.Exception V_2)
  .try
  {
    IL_0000:  ldstr      "step"
    IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
    IL_000a:  stloc.1
    IL_000b:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
    IL_0010:  ldloc.1
    IL_0011:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                         class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
    IL_0016:  pop
    IL_0017:  ldc.i4.s   9
    IL_0019:  ldarg.0
    IL_001a:  add
    IL_001b:  ldarg.0
    IL_001c:  add
    IL_001d:  stloc.0
    IL_001e:  leave.s    IL_002a
    
  }  
  catch [runtime]System.Object 
  {
    IL_0020:  castclass  [runtime]System.Exception
    IL_0025:  stloc.2
    IL_0026:  ldc.i4.5
    IL_0027:  stloc.0
    IL_0028:  leave.s    IL_002a
    
  }  
  IL_002a:  ldloc.0
  IL_002b:  ret
} 
            """
            // Check testFunctionWithFinally is flattened
            """
.method public static int32  testFunctionWithFinally(int32 y) cil managed
{
  
  .maxstack  4
  .locals init (int32 V_0,
           int32 V_1,
           class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit> V_2,
           class [runtime]System.Exception V_3)
  .try
  {
    IL_0000:  ldstr      "step"
    IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
    IL_000a:  stloc.2
    IL_000b:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
    IL_0010:  ldloc.2
    IL_0011:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                         class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
    IL_0016:  pop
    IL_0017:  ldc.i4.s   9
    IL_0019:  ldarg.0
    IL_001a:  add
    IL_001b:  ldarg.0
    IL_001c:  add
    IL_001d:  stloc.1
    IL_001e:  leave.s    IL_0048
    
  }  
  catch [runtime]System.Object 
  {
    IL_0020:  castclass  [runtime]System.Exception
    IL_0025:  stloc.3
    IL_0026:  ldstr      "step"
    IL_002b:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
    IL_0030:  stloc.2
    IL_0031:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
    IL_0036:  ldloc.2
    IL_0037:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                         class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
    IL_003c:  pop
    IL_003d:  rethrow
    IL_003f:  ldnull
    IL_0040:  unbox.any  [runtime]System.Int32
    IL_0045:  stloc.1
    IL_0046:  leave.s    IL_0048
    
  }  
  IL_0048:  ldloc.1
  IL_0049:  stloc.0
  IL_004a:  ldstr      "step"
  IL_004f:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_0054:  stloc.2
  IL_0055:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_005a:  ldloc.2
  IL_005b:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_0060:  pop
  IL_0061:  ldloc.0
  IL_0062:  ret
} 
            """
            ])

