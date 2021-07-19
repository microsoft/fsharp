// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.UnitTests.CodeGen.EmittedIL

open FSharp.Test
open NUnit.Framework

open System
open System.Threading.Tasks

// These tests are sensitive to the fact that FSharp.Core is compiled release, not debug
// The code generated changes slightly in debug mode
#if !DEBUG 
// Check the exact code produced for tasks
[<TestFixture>]
module TaskGeneratedCode =

    // This tests the exact optimized code generated for the MoveNext for a trivial task - we expect 'MoveNext' to be there
    // because state machine compilation succeeds
    //
    // The code is not perfect - because the MoveNext is generated late - but the JIT does a good job on it.
    //
    // The try/catch for the task still exists even though there is no chance of an exception
    //
    // The crucial code for "return 1" is really just
    //   IL_000e:  ldc.i4.1
    //   IL_000f:  stfld      int32 Test/testTask@4::Result

    [<Test>]
    let ``check MoveNext of simple task debug``() =
        CompilerAssert.CompileLibraryAndVerifyILWithOptions [| "/langversion:preview";"/optimize-";"/debug:portable";"/tailcalls-" |]
            """
module Test

let testTask() = task { return 1 }
            """
            (fun verifier -> verifier.VerifyIL [
            """
.method public strict virtual instance void 
MoveNext() cil managed
{
    .override [runtime]System.Runtime.CompilerServices.IAsyncStateMachine::MoveNext
          
    .maxstack  4
    .locals init (int32 V_0,
                    class [runtime]System.Exception V_1,
                    bool V_2,
                    int32 V_3,
                    class [runtime]System.Exception V_4,
                    class [runtime]System.Exception V_5)
    IL_0000:  ldarg.0
    IL_0001:  ldfld      int32 Test/testTask@4::ResumptionPoint
    IL_0006:  stloc.0
    .try
    {
    IL_0007:  ldc.i4.1
    IL_0008:  stloc.3
    IL_0009:  ldarg.0
    IL_000a:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32> Test/testTask@4::Data
    IL_000f:  ldloc.3
    IL_0010:  stfld      !0 valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32>::Result
    IL_0015:  ldc.i4.1
    IL_0016:  stloc.2
    IL_0017:  ldloc.2
    IL_0018:  brfalse.s  IL_0037
    
    IL_001a:  ldarg.0
    IL_001b:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32> Test/testTask@4::Data
    IL_0020:  ldflda     valuetype [runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<!0> valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32>::MethodBuilder
    IL_0025:  ldarg.0
    IL_0026:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32> Test/testTask@4::Data
    IL_002b:  ldfld      !0 valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32>::Result
    IL_0030:  call       instance void valuetype [netstandard]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<int32>::SetResult(!0)
    IL_0035:  leave.s    IL_0045
    
    IL_0037:  leave.s    IL_0045
    
    }  
    catch [runtime]System.Object 
    {
    IL_0039:  castclass  [runtime]System.Exception
    IL_003e:  stloc.s    V_4
    IL_0040:  ldloc.s    V_4
    IL_0042:  stloc.1
    IL_0043:  leave.s    IL_0045
    
    }  
    IL_0045:  ldloc.1
    IL_0046:  stloc.s    V_5
    IL_0048:  ldloc.s    V_5
    IL_004a:  brtrue.s   IL_004d
    
    IL_004c:  ret
    
    IL_004d:  ldarg.0
    IL_004e:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32> Test/testTask@4::Data
    IL_0053:  ldflda     valuetype [runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<!0> valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32>::MethodBuilder
    IL_0058:  ldloc.s    V_5
    IL_005a:  call       instance void valuetype [netstandard]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<int32>::SetException(class [netstandard]System.Exception)
    IL_005f:  ret
} 
                """
            ])

    // This tests the exact optimized code generated for the MoveNext for a trivial task - we expect 'MoveNext' to be there
    // because state machine compilation succeeds
    //
    // The code is not perfect - because the MoveNext is generated late - but the JIT does a good job on it.
    //
    // The try/catch for the task still exists even though there is no chance of an exception
    //
    // The crucial code for "return 1" is really just
    //   IL_000e:  ldc.i4.1
    //   IL_000f:  stfld      int32 Test/testTask@4::Result

    [<Test>]
    let ``check MoveNext of simple task optimized``() =
        CompilerAssert.CompileLibraryAndVerifyILWithOptions [| "/langversion:preview";"/optimize+";"/debug:portable";"/tailcalls+" |]
            """
module Test

let testTask() = task { return 1 }
            """
            (fun verifier -> verifier.VerifyIL [
            """
.method public strict virtual instance void 
        MoveNext() cil managed
{
  .override [runtime]System.Runtime.CompilerServices.IAsyncStateMachine::MoveNext
  
  .maxstack  4
  .locals init (int32 V_0,
           class [runtime]System.Exception V_1,
           bool V_2,
           class [runtime]System.Exception V_3)
  IL_0000:  ldarg.0
  IL_0001:  ldfld      int32 Test/testTask@4::ResumptionPoint
  IL_0006:  stloc.0
  .try
  {
    IL_0007:  ldarg.0
    IL_0008:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32> Test/testTask@4::Data
    IL_000d:  ldc.i4.1
    IL_000e:  stfld      !0 valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32>::Result
    IL_0013:  ldc.i4.1
    IL_0014:  stloc.2
    IL_0015:  ldloc.2
    IL_0016:  brfalse.s  IL_0035
    
    IL_0018:  ldarg.0
    IL_0019:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32> Test/testTask@4::Data
    IL_001e:  ldflda     valuetype [runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<!0> valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32>::MethodBuilder
    IL_0023:  ldarg.0
    IL_0024:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32> Test/testTask@4::Data
    IL_0029:  ldfld      !0 valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32>::Result
    IL_002e:  call       instance void valuetype [netstandard]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<int32>::SetResult(!0)
    IL_0033:  leave.s    IL_0041
    
    IL_0035:  leave.s    IL_0041
    
  }  
  catch [runtime]System.Object 
  {
    IL_0037:  castclass  [runtime]System.Exception
    IL_003c:  stloc.3
    IL_003d:  ldloc.3
    IL_003e:  stloc.1
    IL_003f:  leave.s    IL_0041
    
  }  
  IL_0041:  ldloc.1
  IL_0042:  stloc.3
  IL_0043:  ldloc.3
  IL_0044:  brtrue.s   IL_0047
    
  IL_0046:  ret
    
  IL_0047:  ldarg.0
  IL_0048:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32> Test/testTask@4::Data
  IL_004d:  ldflda     valuetype [runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<!0> valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32>::MethodBuilder
  IL_0052:  ldloc.3
  IL_0053:  call       instance void valuetype [netstandard]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<int32>::SetException(class [netstandard]System.Exception)
  IL_0058:  ret
} 
                """
            ])


    [<Test>]
    let ``check MoveNext of simple binding task debug``() =
        CompilerAssert.CompileLibraryAndVerifyILWithOptions [| "/langversion:preview"; "/debug:portable"; "/optimize-"; "/tailcalls-" |]
            """
module Test
open System.Threading.Tasks
let testTask(t: Task<int>) = task { let! res = t in return res+1 }
            """
            (fun verifier -> verifier.VerifyIL [
            """
    .method public strict virtual instance void 
                MoveNext() cil managed
        {
          .override [runtime]System.Runtime.CompilerServices.IAsyncStateMachine::MoveNext
          
          .maxstack  5
          .locals init (int32 V_0,
                   class [runtime]System.Exception V_1,
                   bool V_2,
                   class [runtime]System.Threading.Tasks.Task`1<int32> V_3,
                   bool V_4,
                   bool V_5,
                   int32 V_6,
                   int32 V_7,
                   int32 V_8,
                   int32 V_9,
                   valuetype [runtime]System.Runtime.CompilerServices.TaskAwaiter`1<int32> V_10,
                   class [runtime]System.Exception V_11,
                   class [runtime]System.Exception V_12)
          IL_0000:  ldarg.0
          IL_0001:  ldfld      int32 Test/testTask@4::ResumptionPoint
          IL_0006:  stloc.0
          IL_0007:  ldloc.0
          IL_0008:  ldc.i4.1
          IL_0009:  sub
          IL_000a:  switch     ( 
                                IL_0015)
          IL_0013:  br.s       IL_0018
    
          IL_0015:  nop
          IL_0016:  br.s       IL_0019
    
          IL_0018:  nop
          .try
          {
            IL_0019:  ldloc.0
            IL_001a:  ldc.i4.1
            IL_001b:  sub
            IL_001c:  switch     ( 
                                  IL_0027)
            IL_0025:  br.s       IL_002a
    
            IL_0027:  nop
            IL_0028:  br.s       IL_0053
    
            IL_002a:  nop
            IL_002b:  ldarg.0
            IL_002c:  ldfld      class [runtime]System.Threading.Tasks.Task`1<int32> Test/testTask@4::t
            IL_0031:  stloc.3
            IL_0032:  ldarg.0
            IL_0033:  ldloc.3
            IL_0034:  callvirt   instance valuetype [netstandard]System.Runtime.CompilerServices.TaskAwaiter`1<!0> class [netstandard]System.Threading.Tasks.Task`1<int32>::GetAwaiter()
            IL_0039:  stfld      valuetype [runtime]System.Runtime.CompilerServices.TaskAwaiter`1<int32> Test/testTask@4::awaiter
            IL_003e:  ldc.i4.1
            IL_003f:  stloc.s    V_4
            IL_0041:  ldarg.0
            IL_0042:  ldflda     valuetype [runtime]System.Runtime.CompilerServices.TaskAwaiter`1<int32> Test/testTask@4::awaiter
            IL_0047:  call       instance bool valuetype [netstandard]System.Runtime.CompilerServices.TaskAwaiter`1<int32>::get_IsCompleted()
            IL_004c:  brfalse.s  IL_0050
    
            IL_004e:  br.s       IL_0069
    
            IL_0050:  ldc.i4.0
            IL_0051:  brfalse.s  IL_0057
    
            IL_0053:  ldc.i4.1
            IL_0054:  nop
            IL_0055:  br.s       IL_0060
    
            IL_0057:  ldarg.0
            IL_0058:  ldc.i4.1
            IL_0059:  stfld      int32 Test/testTask@4::ResumptionPoint
            IL_005e:  ldc.i4.0
            IL_005f:  nop
            IL_0060:  stloc.s    V_5
            IL_0062:  ldloc.s    V_5
            IL_0064:  stloc.s    V_4
            IL_0066:  nop
            IL_0067:  br.s       IL_006a
    
            IL_0069:  nop
            IL_006a:  ldloc.s    V_4
            IL_006c:  brfalse.s  IL_009a
    
            IL_006e:  ldarg.0
            IL_006f:  ldflda     valuetype [runtime]System.Runtime.CompilerServices.TaskAwaiter`1<int32> Test/testTask@4::awaiter
            IL_0074:  call       instance !0 valuetype [netstandard]System.Runtime.CompilerServices.TaskAwaiter`1<int32>::GetResult()
            IL_0079:  stloc.s    V_6
            IL_007b:  ldloc.s    V_6
            IL_007d:  stloc.s    V_7
            IL_007f:  ldloc.s    V_7
            IL_0081:  stloc.s    V_8
            IL_0083:  ldloc.s    V_8
            IL_0085:  ldc.i4.1
            IL_0086:  add
            IL_0087:  stloc.s    V_9
            IL_0089:  ldarg.0
            IL_008a:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32> Test/testTask@4::Data
            IL_008f:  ldloc.s    V_9
            IL_0091:  stfld      !0 valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32>::Result
            IL_0096:  ldc.i4.1
            IL_0097:  nop
            IL_0098:  br.s       IL_00b3
    
            IL_009a:  ldarg.0
            IL_009b:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32> Test/testTask@4::Data
            IL_00a0:  ldflda     valuetype [runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<!0> valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32>::MethodBuilder
            IL_00a5:  ldarg.0
            IL_00a6:  ldflda     valuetype [runtime]System.Runtime.CompilerServices.TaskAwaiter`1<int32> Test/testTask@4::awaiter
            IL_00ab:  ldarg.0
            IL_00ac:  call       instance void valuetype [netstandard]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<int32>::AwaitUnsafeOnCompleted<valuetype [runtime]System.Runtime.CompilerServices.TaskAwaiter`1<int32>,valuetype Test/testTask@4>(!!0&,
                                                                                                                                                                                                                                                                        !!1&)
            IL_00b1:  ldc.i4.0
            IL_00b2:  nop
            IL_00b3:  brfalse.s  IL_00c1
    
            IL_00b5:  ldarg.0
            IL_00b6:  ldloc.s    V_10
            IL_00b8:  stfld      valuetype [runtime]System.Runtime.CompilerServices.TaskAwaiter`1<int32> Test/testTask@4::awaiter
            IL_00bd:  ldc.i4.1
            IL_00be:  nop
            IL_00bf:  br.s       IL_00c3
    
            IL_00c1:  ldc.i4.0
            IL_00c2:  nop
            IL_00c3:  stloc.2
            IL_00c4:  ldloc.2
            IL_00c5:  brfalse.s  IL_00e4
    
            IL_00c7:  ldarg.0
            IL_00c8:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32> Test/testTask@4::Data
            IL_00cd:  ldflda     valuetype [runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<!0> valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32>::MethodBuilder
            IL_00d2:  ldarg.0
            IL_00d3:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32> Test/testTask@4::Data
            IL_00d8:  ldfld      !0 valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32>::Result
            IL_00dd:  call       instance void valuetype [netstandard]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<int32>::SetResult(!0)
            IL_00e2:  leave.s    IL_00f2
    
            IL_00e4:  leave.s    IL_00f2
    
          }  
          catch [runtime]System.Object 
          {
            IL_00e6:  castclass  [runtime]System.Exception
            IL_00eb:  stloc.s    V_11
            IL_00ed:  ldloc.s    V_11
            IL_00ef:  stloc.1
            IL_00f0:  leave.s    IL_00f2
    
          }  
          IL_00f2:  ldloc.1
          IL_00f3:  stloc.s    V_12
          IL_00f5:  ldloc.s    V_12
          IL_00f7:  brtrue.s   IL_00fa
    
          IL_00f9:  ret
    
          IL_00fa:  ldarg.0
          IL_00fb:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32> Test/testTask@4::Data
          IL_0100:  ldflda     valuetype [runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<!0> valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<int32>::MethodBuilder
          IL_0105:  ldloc.s    V_12
          IL_0107:  call       instance void valuetype [netstandard]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<int32>::SetException(class [netstandard]System.Exception)
          IL_010c:  ret
        } 
                """
            ])

module TaskTryFinallyGeneration =

    [<Test>]
    let ``check MoveNext of task try/finally optimized``() =
        CompilerAssert.CompileLibraryAndVerifyILWithOptions [| "/langversion:preview";"/optimize+";"/debug:portable";"/tailcalls+" |]
            """
module Test

let testTask() = task { try 1+1 finally System.Console.WriteLine("finally") }
            """
            (fun verifier -> verifier.VerifyIL [
            """
.method public strict virtual instance void 
        MoveNext() cil managed
{
  .override [runtime]System.Runtime.CompilerServices.IAsyncStateMachine::MoveNext
  
  .maxstack  4
  .locals init (int32 V_0,
           class [runtime]System.Exception V_1,
           bool V_2,
           bool V_3,
           bool V_4,
           class [runtime]System.Exception V_5)
  IL_0000:  ldarg.0
  IL_0001:  ldfld      int32 Test/testTask@4::ResumptionPoint
  IL_0006:  stloc.0
  .try
  {
    IL_0007:  ldc.i4.0
    IL_0008:  stloc.3
    IL_0009:  ldloc.3
    IL_000a:  brfalse.s  IL_0010
    
    IL_000c:  ldc.i4.1
    IL_000d:  nop
    IL_000e:  br.s       IL_0012
    
    IL_0010:  ldloc.3
    IL_0011:  nop
    IL_0012:  stloc.3
    .try
    {
      IL_0013:  nop
      IL_0014:  ldc.i4.1
      IL_0015:  stloc.s    V_4
      IL_0017:  ldloc.s    V_4
      IL_0019:  stloc.3
      IL_001a:  leave.s    IL_003b
    
    }  
    catch [runtime]System.Object 
    {
      IL_001c:  castclass  [runtime]System.Exception
      IL_0021:  stloc.s    V_5
      IL_0023:  ldstr      "finally"
      IL_0028:  call       void [runtime]System.Console::WriteLine(string)
      IL_002d:  ldc.i4.1
      IL_002e:  stloc.s    V_4
      IL_0030:  rethrow
      IL_0032:  ldnull
      IL_0033:  unbox.any  [FSharp.Core]Microsoft.FSharp.Core.Unit
      IL_0038:  pop
      IL_0039:  leave.s    IL_003b
    
    }  
    IL_003b:  ldloc.3
    IL_003c:  brfalse.s  IL_004e
    
    IL_003e:  ldstr      "finally"
    IL_0043:  call       void [runtime]System.Console::WriteLine(string)
    IL_0048:  ldc.i4.1
    IL_0049:  stloc.s    V_4
    IL_004b:  nop
    IL_004c:  br.s       IL_004f
    
    IL_004e:  nop
    IL_004f:  ldloc.3
    IL_0050:  stloc.2
    IL_0051:  ldloc.2
    IL_0052:  brfalse.s  IL_0071
    
    IL_0054:  ldarg.0
    IL_0055:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit> Test/testTask@4::Data
    IL_005a:  ldflda     valuetype [runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<!0> valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::MethodBuilder
    IL_005f:  ldarg.0
    IL_0060:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit> Test/testTask@4::Data
    IL_0065:  ldfld      !0 valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::Result
    IL_006a:  call       instance void valuetype [netstandard]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::SetResult(!0)
    IL_006f:  leave.s    IL_007f
    
    IL_0071:  leave.s    IL_007f
    
  }  
  catch [runtime]System.Object 
  {
    IL_0073:  castclass  [runtime]System.Exception
    IL_0078:  stloc.s    V_5
    IL_007a:  ldloc.s    V_5
    IL_007c:  stloc.1
    IL_007d:  leave.s    IL_007f
    
  }  
  IL_007f:  ldloc.1
  IL_0080:  stloc.s    V_5
  IL_0082:  ldloc.s    V_5
  IL_0084:  brtrue.s   IL_0087
    
  IL_0086:  ret
    
  IL_0087:  ldarg.0
  IL_0088:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit> Test/testTask@4::Data
  IL_008d:  ldflda     valuetype [runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<!0> valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::MethodBuilder
  IL_0092:  ldloc.s    V_5
  IL_0094:  call       instance void valuetype [netstandard]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::SetException(class [netstandard]System.Exception)
  IL_0099:  ret
} 
                """
            ])


    [<Test>]
    let ``check MoveNext of task try/finally debug``() =
        CompilerAssert.CompileLibraryAndVerifyILWithOptions [| "/langversion:preview";"/optimize-";"/debug:portable";"/tailcalls-" |]
            """
module Test

let testTask() = task { try 1+1 finally System.Console.WriteLine("finally") }
            """
            (fun verifier -> verifier.VerifyIL [
            """
.method public strict virtual instance void 
        MoveNext() cil managed
{
  .override [runtime]System.Runtime.CompilerServices.IAsyncStateMachine::MoveNext
  
  .maxstack  4
  .locals init (int32 V_0,
           class [runtime]System.Exception V_1,
           bool V_2,
           bool V_3,
           bool V_4,
           class [runtime]System.Exception V_5,
           bool V_6,
           bool V_7,
           class [runtime]System.Exception V_8,
           class [runtime]System.Exception V_9)
  IL_0000:  ldarg.0
  IL_0001:  ldfld      int32 Test/testTask@4::ResumptionPoint
  IL_0006:  stloc.0
  .try
  {
    IL_0007:  ldc.i4.0
    IL_0008:  stloc.3
    IL_0009:  ldloc.3
    IL_000a:  brfalse.s  IL_0010
    
    IL_000c:  ldc.i4.1
    IL_000d:  nop
    IL_000e:  br.s       IL_0012
    
    IL_0010:  ldloc.3
    IL_0011:  nop
    IL_0012:  stloc.3
    .try
    {
      IL_0013:  nop
      IL_0014:  ldc.i4.1
      IL_0015:  stloc.s    V_4
      IL_0017:  ldloc.s    V_4
      IL_0019:  stloc.3
      IL_001a:  leave.s    IL_003b
    
    }  
    catch [runtime]System.Object 
    {
      IL_001c:  castclass  [runtime]System.Exception
      IL_0021:  stloc.s    V_5
      IL_0023:  ldstr      "finally"
      IL_0028:  call       void [runtime]System.Console::WriteLine(string)
      IL_002d:  ldc.i4.1
      IL_002e:  stloc.s    V_6
      IL_0030:  rethrow
      IL_0032:  ldnull
      IL_0033:  unbox.any  [FSharp.Core]Microsoft.FSharp.Core.Unit
      IL_0038:  pop
      IL_0039:  leave.s    IL_003b
    
    }  
    IL_003b:  ldloc.3
    IL_003c:  brfalse.s  IL_004e
    
    IL_003e:  ldstr      "finally"
    IL_0043:  call       void [runtime]System.Console::WriteLine(string)
    IL_0048:  ldc.i4.1
    IL_0049:  stloc.s    V_7
    IL_004b:  nop
    IL_004c:  br.s       IL_004f
    
    IL_004e:  nop
    IL_004f:  ldloc.3
    IL_0050:  stloc.2
    IL_0051:  ldloc.2
    IL_0052:  brfalse.s  IL_0071
    
    IL_0054:  ldarg.0
    IL_0055:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit> Test/testTask@4::Data
    IL_005a:  ldflda     valuetype [runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<!0> valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::MethodBuilder
    IL_005f:  ldarg.0
    IL_0060:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit> Test/testTask@4::Data
    IL_0065:  ldfld      !0 valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::Result
    IL_006a:  call       instance void valuetype [netstandard]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::SetResult(!0)
    IL_006f:  leave.s    IL_007f
    
    IL_0071:  leave.s    IL_007f
    
  }  
  catch [runtime]System.Object 
  {
    IL_0073:  castclass  [runtime]System.Exception
    IL_0078:  stloc.s    V_8
    IL_007a:  ldloc.s    V_8
    IL_007c:  stloc.1
    IL_007d:  leave.s    IL_007f
    
  }  
  IL_007f:  ldloc.1
  IL_0080:  stloc.s    V_9
  IL_0082:  ldloc.s    V_9
  IL_0084:  brtrue.s   IL_0087
    
  IL_0086:  ret
    
  IL_0087:  ldarg.0
  IL_0088:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit> Test/testTask@4::Data
  IL_008d:  ldflda     valuetype [runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<!0> valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::MethodBuilder
  IL_0092:  ldloc.s    V_9
  IL_0094:  call       instance void valuetype [netstandard]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::SetException(class [netstandard]System.Exception)
  IL_0099:  ret
} 
                """
            ])

module TaskTryWithGeneration =

    [<Test>]
    let ``check MoveNext of task try/with optimized``() =
        CompilerAssert.CompileLibraryAndVerifyILWithOptions [| "/langversion:preview";"/optimize+";"/debug:portable";"/tailcalls+" |]
            """
module Test

let testTask() = task { try 1 with e -> System.Console.WriteLine("finally"); 2 }
            """
            (fun verifier -> verifier.VerifyIL [
            """
.method public strict virtual instance void 
        MoveNext() cil managed
{
  .override [runtime]System.Runtime.CompilerServices.IAsyncStateMachine::MoveNext
  
  .maxstack  4
  .locals init (int32 V_0,
           class [runtime]System.Exception V_1,
           bool V_2,
           bool V_3,
           bool V_4,
           class [runtime]System.Exception V_5,
           bool V_6,
           class [runtime]System.Exception V_7)
  IL_0000:  ldarg.0
  IL_0001:  ldfld      int32 Test/testTask@4::ResumptionPoint
  IL_0006:  stloc.0
  .try
  {
    IL_0007:  ldc.i4.0
    IL_0008:  stloc.3
    IL_0009:  ldc.i4.0
    IL_000a:  stloc.s    V_4
    IL_000c:  ldnull
    IL_000d:  stloc.s    V_5
    IL_000f:  ldloc.3
    IL_0010:  brfalse.s  IL_0016
    
    IL_0012:  ldc.i4.1
    IL_0013:  nop
    IL_0014:  br.s       IL_0018
    
    IL_0016:  ldloc.3
    IL_0017:  nop
    IL_0018:  stloc.3
    .try
    {
      IL_0019:  nop
      IL_001a:  ldc.i4.1
      IL_001b:  stloc.s    V_6
      IL_001d:  ldloc.s    V_6
      IL_001f:  stloc.3
      IL_0020:  leave.s    IL_0032
    
    }  
    catch [runtime]System.Object 
    {
      IL_0022:  castclass  [runtime]System.Exception
      IL_0027:  stloc.s    V_7
      IL_0029:  ldc.i4.1
      IL_002a:  stloc.s    V_4
      IL_002c:  ldloc.s    V_7
      IL_002e:  stloc.s    V_5
      IL_0030:  leave.s    IL_0032
    
    }  
    IL_0032:  ldloc.s    V_4
    IL_0034:  brfalse.s  IL_0048
    
    IL_0036:  ldloc.s    V_5
    IL_0038:  stloc.s    V_7
    IL_003a:  ldstr      "finally"
    IL_003f:  call       void [runtime]System.Console::WriteLine(string)
    IL_0044:  ldc.i4.1
    IL_0045:  nop
    IL_0046:  br.s       IL_004a
    
    IL_0048:  ldloc.3
    IL_0049:  nop
    IL_004a:  stloc.2
    IL_004b:  ldloc.2
    IL_004c:  brfalse.s  IL_006b
    
    IL_004e:  ldarg.0
    IL_004f:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit> Test/testTask@4::Data
    IL_0054:  ldflda     valuetype [runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<!0> valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::MethodBuilder
    IL_0059:  ldarg.0
    IL_005a:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit> Test/testTask@4::Data
    IL_005f:  ldfld      !0 valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::Result
    IL_0064:  call       instance void valuetype [netstandard]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::SetResult(!0)
    IL_0069:  leave.s    IL_0079
    
    IL_006b:  leave.s    IL_0079
    
  }  
  catch [runtime]System.Object 
  {
    IL_006d:  castclass  [runtime]System.Exception
    IL_0072:  stloc.s    V_5
    IL_0074:  ldloc.s    V_5
    IL_0076:  stloc.1
    IL_0077:  leave.s    IL_0079
    
  }  
  IL_0079:  ldloc.1
  IL_007a:  stloc.s    V_5
  IL_007c:  ldloc.s    V_5
  IL_007e:  brtrue.s   IL_0081
    
  IL_0080:  ret
    
  IL_0081:  ldarg.0
  IL_0082:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit> Test/testTask@4::Data
  IL_0087:  ldflda     valuetype [runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<!0> valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::MethodBuilder
  IL_008c:  ldloc.s    V_5
  IL_008e:  call       instance void valuetype [netstandard]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::SetException(class [netstandard]System.Exception)
  IL_0093:  ret
} 
                """
            ])


    [<Test>]
    let ``check MoveNext of task try/with debug``() =
        CompilerAssert.CompileLibraryAndVerifyILWithOptions [| "/langversion:preview";"/optimize-";"/debug:portable";"/tailcalls-" |]
            """
module Test

let testTask() = task { try 1 with e -> System.Console.WriteLine("with"); 2 }
            """
            (fun verifier -> verifier.VerifyIL [
            """
.method public strict virtual instance void 
        MoveNext() cil managed
{
  .override [runtime]System.Runtime.CompilerServices.IAsyncStateMachine::MoveNext
  
  .maxstack  4
  .locals init (int32 V_0,
           class [runtime]System.Exception V_1,
           bool V_2,
           bool V_3,
           bool V_4,
           class [runtime]System.Exception V_5,
           bool V_6,
           class [runtime]System.Exception V_7,
           class [runtime]System.Exception V_8,
           class [runtime]System.Exception V_9,
           class [runtime]System.Exception V_10,
           class [runtime]System.Exception V_11)
  IL_0000:  ldarg.0
  IL_0001:  ldfld      int32 Test/testTask@4::ResumptionPoint
  IL_0006:  stloc.0
  .try
  {
    IL_0007:  ldc.i4.0
    IL_0008:  stloc.3
    IL_0009:  ldc.i4.0
    IL_000a:  stloc.s    V_4
    IL_000c:  ldnull
    IL_000d:  stloc.s    V_5
    IL_000f:  ldloc.3
    IL_0010:  brfalse.s  IL_0016
    
    IL_0012:  ldc.i4.1
    IL_0013:  nop
    IL_0014:  br.s       IL_0018
    
    IL_0016:  ldloc.3
    IL_0017:  nop
    IL_0018:  stloc.3
    .try
    {
      IL_0019:  nop
      IL_001a:  ldc.i4.1
      IL_001b:  stloc.s    V_6
      IL_001d:  ldloc.s    V_6
      IL_001f:  stloc.3
      IL_0020:  leave.s    IL_0032
    
    }  
    catch [runtime]System.Object 
    {
      IL_0022:  castclass  [runtime]System.Exception
      IL_0027:  stloc.s    V_7
      IL_0029:  ldc.i4.1
      IL_002a:  stloc.s    V_4
      IL_002c:  ldloc.s    V_7
      IL_002e:  stloc.s    V_5
      IL_0030:  leave.s    IL_0032
    
    }  
    IL_0032:  ldloc.s    V_4
    IL_0034:  brfalse.s  IL_004c
    
    IL_0036:  ldloc.s    V_5
    IL_0038:  stloc.s    V_8
    IL_003a:  ldloc.s    V_8
    IL_003c:  stloc.s    V_9
    IL_003e:  ldstr      "with"
    IL_0043:  call       void [runtime]System.Console::WriteLine(string)
    IL_0048:  ldc.i4.1
    IL_0049:  nop
    IL_004a:  br.s       IL_004e
    
    IL_004c:  ldloc.3
    IL_004d:  nop
    IL_004e:  stloc.2
    IL_004f:  ldloc.2
    IL_0050:  brfalse.s  IL_006f
    
    IL_0052:  ldarg.0
    IL_0053:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit> Test/testTask@4::Data
    IL_0058:  ldflda     valuetype [runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<!0> valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::MethodBuilder
    IL_005d:  ldarg.0
    IL_005e:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit> Test/testTask@4::Data
    IL_0063:  ldfld      !0 valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::Result
    IL_0068:  call       instance void valuetype [netstandard]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::SetResult(!0)
    IL_006d:  leave.s    IL_007d
    
    IL_006f:  leave.s    IL_007d
    
  }  
  catch [runtime]System.Object 
  {
    IL_0071:  castclass  [runtime]System.Exception
    IL_0076:  stloc.s    V_10
    IL_0078:  ldloc.s    V_10
    IL_007a:  stloc.1
    IL_007b:  leave.s    IL_007d
    
  }  
  IL_007d:  ldloc.1
  IL_007e:  stloc.s    V_11
  IL_0080:  ldloc.s    V_11
  IL_0082:  brtrue.s   IL_0085
    
  IL_0084:  ret
    
  IL_0085:  ldarg.0
  IL_0086:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit> Test/testTask@4::Data
  IL_008b:  ldflda     valuetype [runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<!0> valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::MethodBuilder
  IL_0090:  ldloc.s    V_11
  IL_0092:  call       instance void valuetype [netstandard]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::SetException(class [netstandard]System.Exception)
  IL_0097:  ret
} 
                """
            ])

module TaskWhileLoopGeneration =

    [<Test>]
    let ``check MoveNext of task while loop optimized``() =
        CompilerAssert.CompileLibraryAndVerifyILWithOptions [| "/langversion:preview";"/optimize+";"/debug:portable";"/tailcalls+" |]
            """
module Test

let mutable x = 1
let testTask() = task { while x > 4 do System.Console.WriteLine("loop") }
            """
            (fun verifier -> verifier.VerifyIL [
            """
.method public strict virtual instance void 
        MoveNext() cil managed
{
  .override [runtime]System.Runtime.CompilerServices.IAsyncStateMachine::MoveNext
  
  .maxstack  4
  .locals init (int32 V_0,
           class [runtime]System.Exception V_1,
           bool V_2,
           bool V_3,
           bool V_4,
           class [runtime]System.Exception V_5)
  IL_0000:  ldarg.0
  IL_0001:  ldfld      int32 Test/testTask@5::ResumptionPoint
  IL_0006:  stloc.0
  .try
  {
    IL_0007:  ldc.i4.1
    IL_0008:  stloc.3
    IL_0009:  ldloc.3
    IL_000a:  brfalse.s  IL_0017
    
    IL_000c:  call       int32 Test::get_x()
    IL_0011:  ldc.i4.4
    IL_0012:  cgt
    IL_0014:  nop
    IL_0015:  br.s       IL_0019
    
    IL_0017:  ldc.i4.0
    IL_0018:  nop
    IL_0019:  brfalse.s  IL_0030
    
    IL_001b:  ldstr      "loop"
    IL_0020:  call       void [runtime]System.Console::WriteLine(string)
    IL_0025:  ldc.i4.1
    IL_0026:  stloc.s    V_4
    IL_0028:  ldloc.s    V_4
    IL_002a:  stloc.3
    IL_002b:  ldc.i4.0
    IL_002c:  stloc.0
    IL_002d:  nop
    IL_002e:  br.s       IL_0009
    
    IL_0030:  ldloc.3
    IL_0031:  stloc.2
    IL_0032:  ldloc.2
    IL_0033:  brfalse.s  IL_0052
    
    IL_0035:  ldarg.0
    IL_0036:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit> Test/testTask@5::Data
    IL_003b:  ldflda     valuetype [runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<!0> valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::MethodBuilder
    IL_0040:  ldarg.0
    IL_0041:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit> Test/testTask@5::Data
    IL_0046:  ldfld      !0 valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::Result
    IL_004b:  call       instance void valuetype [netstandard]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::SetResult(!0)
    IL_0050:  leave.s    IL_0060
    
    IL_0052:  leave.s    IL_0060
    
  }  
  catch [runtime]System.Object 
  {
    IL_0054:  castclass  [runtime]System.Exception
    IL_0059:  stloc.s    V_5
    IL_005b:  ldloc.s    V_5
    IL_005d:  stloc.1
    IL_005e:  leave.s    IL_0060
    
  }  
  IL_0060:  ldloc.1
  IL_0061:  stloc.s    V_5
  IL_0063:  ldloc.s    V_5
  IL_0065:  brtrue.s   IL_0068
    
  IL_0067:  ret
    
  IL_0068:  ldarg.0
  IL_0069:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit> Test/testTask@5::Data
  IL_006e:  ldflda     valuetype [runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<!0> valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::MethodBuilder
  IL_0073:  ldloc.s    V_5
  IL_0075:  call       instance void valuetype [netstandard]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::SetException(class [netstandard]System.Exception)
  IL_007a:  ret
} 
                """
            ])


    [<Test>]
    let ``check MoveNext of task while loop debug``() =
        CompilerAssert.CompileLibraryAndVerifyILWithOptions [| "/langversion:preview";"/optimize-";"/debug:portable";"/tailcalls-" |]
            """
module Test

let mutable x = 1 
let testTask() = task { while x > 4 do System.Console.WriteLine("loop") }
            """
            (fun verifier -> verifier.VerifyIL [
            """
.method public strict virtual instance void 
        MoveNext() cil managed
{
  .override [runtime]System.Runtime.CompilerServices.IAsyncStateMachine::MoveNext
  
  .maxstack  4
  .locals init (int32 V_0,
           class [runtime]System.Exception V_1,
           bool V_2,
           bool V_3,
           bool V_4,
           class [runtime]System.Exception V_5,
           class [runtime]System.Exception V_6)
  IL_0000:  ldarg.0
  IL_0001:  ldfld      int32 Test/testTask@5::ResumptionPoint
  IL_0006:  stloc.0
  .try
  {
    IL_0007:  ldc.i4.1
    IL_0008:  stloc.3
    IL_0009:  ldloc.3
    IL_000a:  brfalse.s  IL_0017
    
    IL_000c:  call       int32 Test::get_x()
    IL_0011:  ldc.i4.4
    IL_0012:  cgt
    IL_0014:  nop
    IL_0015:  br.s       IL_0019
    
    IL_0017:  ldc.i4.0
    IL_0018:  nop
    IL_0019:  brfalse.s  IL_0030
    
    IL_001b:  ldstr      "loop"
    IL_0020:  call       void [runtime]System.Console::WriteLine(string)
    IL_0025:  ldc.i4.1
    IL_0026:  stloc.s    V_4
    IL_0028:  ldloc.s    V_4
    IL_002a:  stloc.3
    IL_002b:  ldc.i4.0
    IL_002c:  stloc.0
    IL_002d:  nop
    IL_002e:  br.s       IL_0009
    
    IL_0030:  ldloc.3
    IL_0031:  stloc.2
    IL_0032:  ldloc.2
    IL_0033:  brfalse.s  IL_0052
    
    IL_0035:  ldarg.0
    IL_0036:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit> Test/testTask@5::Data
    IL_003b:  ldflda     valuetype [runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<!0> valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::MethodBuilder
    IL_0040:  ldarg.0
    IL_0041:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit> Test/testTask@5::Data
    IL_0046:  ldfld      !0 valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::Result
    IL_004b:  call       instance void valuetype [netstandard]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::SetResult(!0)
    IL_0050:  leave.s    IL_0060
    
    IL_0052:  leave.s    IL_0060
    
  }  
  catch [runtime]System.Object 
  {
    IL_0054:  castclass  [runtime]System.Exception
    IL_0059:  stloc.s    V_5
    IL_005b:  ldloc.s    V_5
    IL_005d:  stloc.1
    IL_005e:  leave.s    IL_0060
    
  }  
  IL_0060:  ldloc.1
  IL_0061:  stloc.s    V_6
  IL_0063:  ldloc.s    V_6
  IL_0065:  brtrue.s   IL_0068
    
  IL_0067:  ret
    
  IL_0068:  ldarg.0
  IL_0069:  ldflda     valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit> Test/testTask@5::Data
  IL_006e:  ldflda     valuetype [runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<!0> valuetype [FSharp.Core]Microsoft.FSharp.Control.TaskStateMachineData`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::MethodBuilder
  IL_0073:  ldloc.s    V_6
  IL_0075:  call       instance void valuetype [netstandard]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<class [FSharp.Core]Microsoft.FSharp.Core.Unit>::SetException(class [netstandard]System.Exception)
  IL_007a:  ret
} 
                """
            ])
#endif


#if NETCOREAPP
[<TestFixture>]
module ``Check stack traces`` = 
    [<Test>]
    let ``check stack trace of async exception from task``() =
        let t() =
            task {
                let! throws = task {
                    do! System.Threading.Tasks.Task.Yield()
                    failwith "Inner task exception"
                    return ()
                }
                return throws
            }
            
        let f() = t().Wait()
        
        let stackTrace = try f(); failwith "huh?" with e -> e.ToString()

        if stackTrace.Contains("huh?") then
           failwith "unexpected - inner exception not generated correctly"
        
        if not (stackTrace.Contains("MoveNext()")) then
           failwith "expected MoveNext() on stack trace"

        if stackTrace.Contains("End of stack trace from previous location") then
           failwith "expected emit of CompilerGeneratedAtrtibute to suppress message in .NET Core stack walking "
#endif


// Test that the SRTP Bind on a ValueTask is still generic in the bind type
[<TestFixture>]
module ``Check return attributes`` = 
    let incr (x:int) : [<Experimental("a")>] (int -> int) = (fun a -> a + x)

    // Putting a return atribute of any kind on f function should be respected
    // If the function is curried its inferred arity should be no more than 
    // the declared arguments (the F# rule that infers additional arguments 
    // should not kick in).
    [<Test>]
    let ``check return attribute of curried function``() =
        match <@ incr 3 4 @> with
        | Quotations.Patterns.Application (Quotations.Patterns.Call(None, mi, [Quotations.DerivedPatterns.Int32 3]), Quotations.DerivedPatterns.Int32 4) ->
            if mi.GetParameters().Length <> 1 then  
               failwith "wrong number of parameters"
            if mi.ReturnTypeCustomAttributes.GetCustomAttributes(false).Length <> 1 then  
               failwith "wrong number of attributes"
            ()
        | _ -> failwith "curried function expected"


// Compilation test
// Test that the SRTP Bind on a ValueTask is still generic in the bind type
module ``SRTP Bind on a ValueTask is still generic in the bind type (nested)`` =
    let FindAsync() = ValueTask<'T>(Unchecked.defaultof<'T>)
    let TryFindAsync() : Task<'T voption> = task {
            let! r = FindAsync()
            if obj.ReferenceEquals(r, null) then return ValueNone
            else return ValueSome r
        }
    let t1 : Task<int voption> = TryFindAsync() // test TryFindAsync is generic
    let t2 : Task<string voption> = TryFindAsync() // test TryFindAsync is generic
    
// Compilation test
// Test that the SRTP Bind on a ValueTask is still generic in the bind type
module ``SRTP Bind on a ValueTask is still generic in the bind type`` =
    let FindAsync() = ValueTask<'T>(Unchecked.defaultof<'T>)
    let TryFindAsync() : Task<'T> = task {
            let! r = FindAsync()
            return r
        }
    
// Compilation test
module ``SRTP ReturnFrom on a ValueTask is still generic in the bind type`` =
    let FindAsync() = ValueTask<'T>(Unchecked.defaultof<'T>)
    let TryFindAsync() : Task<'T> = task {
            return! FindAsync()
        }
    
    let t1 : Task<int voption> = TryFindAsync() // test TryFindAsync is generic
    let t2 : Task<string voption> = TryFindAsync() // test TryFindAsync is generic

