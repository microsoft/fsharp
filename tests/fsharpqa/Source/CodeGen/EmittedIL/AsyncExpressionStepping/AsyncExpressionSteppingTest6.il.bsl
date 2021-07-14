
//  Microsoft (R) .NET Framework IL Disassembler.  Version 4.8.3928.0
//  Copyright (c) Microsoft Corporation.  All rights reserved.



// Metadata version: v4.0.30319
.assembly extern mscorlib
{
  .publickeytoken = (B7 7A 5C 56 19 34 E0 89 )                         // .z\V.4..
  .ver 4:0:0:0
}
.assembly extern FSharp.Core
{
  .publickeytoken = (B0 3F 5F 7F 11 D5 0A 3A )                         // .?_....:
  .ver 5:0:0:0
}
.assembly AsyncExpressionSteppingTest6
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.AsyncExpressionSteppingTest6
{
  // Offset: 0x00000000 Length: 0x00000297
}
.mresource public FSharpOptimizationData.AsyncExpressionSteppingTest6
{
  // Offset: 0x000002A0 Length: 0x000000BE
}
.module AsyncExpressionSteppingTest6.dll
// MVID: {60EDFA6D-6394-4FAD-A745-03836DFAED60}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x071A0000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed AsyncExpressionSteppingTest6
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class abstract auto ansi sealed nested public AsyncExpressionSteppingTest6
         extends [mscorlib]System.Object
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
    .class auto ansi serializable sealed nested assembly beforefieldinit 'f2@10-1'
           extends class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<int32>,class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn>
    {
      .field public int32 'value'
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .method assembly specialname rtspecialname 
              instance void  .ctor(int32 'value') cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       14 (0xe)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<int32>,class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn>::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  ldarg.1
        IL_0008:  stfld      int32 AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f2@10-1'::'value'
        IL_000d:  ret
      } // end of method 'f2@10-1'::.ctor

      .method public strict virtual instance class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn 
              Invoke(valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<int32> ctxt) cil managed
      {
        // Code size       15 (0xf)
        .maxstack  8
        .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
        .line 10,10 : 17,25 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\AsyncExpressionStepping\\AsyncExpressionSteppingTest6.fs'
        IL_0000:  ldarg.1
        IL_0001:  ldarg.0
        IL_0002:  ldfld      int32 AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f2@10-1'::'value'
        IL_0007:  tail.
        IL_0009:  call       class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<int32>::Success(valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<!0>,
                                                                                                                                                                       !0)
        IL_000e:  ret
      } // end of method 'f2@10-1'::Invoke

    } // end of class 'f2@10-1'

    .class auto ansi serializable sealed nested assembly beforefieldinit f2@5
           extends class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>>
    {
      .field public class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder builder@
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .method assembly specialname rtspecialname 
              instance void  .ctor(class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder builder@) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       14 (0xe)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>>::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  ldarg.1
        IL_0008:  stfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/f2@5::builder@
        IL_000d:  ret
      } // end of method f2@5::.ctor

      .method public strict virtual instance class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> 
              Invoke(class [FSharp.Core]Microsoft.FSharp.Core.Unit unitVar) cil managed
      {
        // Code size       67 (0x43)
        .maxstack  6
        .locals init ([0] class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> x,
                 [1] class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> y,
                 [2] int32 z,
                 [3] class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder V_3,
                 [4] int32 V_4)
        .line 5,5 : 17,30 ''
        IL_0000:  ldc.i4.0
        IL_0001:  call       class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<!!0> [FSharp.Core]Microsoft.FSharp.Core.Operators::Ref<int32>(!!0)
        IL_0006:  stloc.0
        .line 6,6 : 17,23 ''
        IL_0007:  ldloc.0
        IL_0008:  call       void [FSharp.Core]Microsoft.FSharp.Core.Operators::Increment(class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32>)
        IL_000d:  nop
        .line 7,7 : 17,30 ''
        IL_000e:  ldc.i4.0
        IL_000f:  call       class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<!!0> [FSharp.Core]Microsoft.FSharp.Core.Operators::Ref<int32>(!!0)
        IL_0014:  stloc.1
        .line 8,8 : 17,23 ''
        IL_0015:  ldloc.1
        IL_0016:  call       void [FSharp.Core]Microsoft.FSharp.Core.Operators::Increment(class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32>)
        IL_001b:  nop
        .line 9,9 : 17,32 ''
        IL_001c:  ldloc.0
        IL_001d:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.Operators::op_Dereference<int32>(class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<!!0>)
        IL_0022:  ldloc.1
        IL_0023:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.Operators::op_Dereference<int32>(class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<!!0>)
        IL_0028:  add
        IL_0029:  stloc.2
        .line 10,10 : 17,25 ''
        IL_002a:  ldarg.0
        IL_002b:  ldfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/f2@5::builder@
        IL_0030:  stloc.3
        IL_0031:  ldloc.2
        IL_0032:  stloc.s    V_4
        IL_0034:  ldloc.s    V_4
        IL_0036:  newobj     instance void AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f2@10-1'::.ctor(int32)
        IL_003b:  tail.
        IL_003d:  call       class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<!!0> [FSharp.Core]Microsoft.FSharp.Control.AsyncPrimitives::MakeAsync<int32>(class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<!!0>,class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn>)
        IL_0042:  ret
      } // end of method f2@5::Invoke

    } // end of class f2@5

    .class auto ansi serializable sealed nested assembly beforefieldinit 'f3@20-5'
           extends class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<int32>,class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn>
    {
      .field public int32 'value'
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .method assembly specialname rtspecialname 
              instance void  .ctor(int32 'value') cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       14 (0xe)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<int32>,class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn>::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  ldarg.1
        IL_0008:  stfld      int32 AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@20-5'::'value'
        IL_000d:  ret
      } // end of method 'f3@20-5'::.ctor

      .method public strict virtual instance class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn 
              Invoke(valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<int32> ctxt) cil managed
      {
        // Code size       15 (0xf)
        .maxstack  8
        .line 20,20 : 17,25 ''
        IL_0000:  ldarg.1
        IL_0001:  ldarg.0
        IL_0002:  ldfld      int32 AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@20-5'::'value'
        IL_0007:  tail.
        IL_0009:  call       class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<int32>::Success(valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<!0>,
                                                                                                                                                                       !0)
        IL_000e:  ret
      } // end of method 'f3@20-5'::Invoke

    } // end of class 'f3@20-5'

    .class auto ansi serializable sealed nested assembly beforefieldinit 'f3@19-4'
           extends class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>>
    {
      .field public class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder builder@
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .field public int32 x1
      .field public class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> y
      .method assembly specialname rtspecialname 
              instance void  .ctor(class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder builder@,
                                   int32 x1,
                                   class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> y) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       28 (0x1c)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>>::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  ldarg.1
        IL_0008:  stfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@19-4'::builder@
        IL_000d:  ldarg.0
        IL_000e:  ldarg.2
        IL_000f:  stfld      int32 AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@19-4'::x1
        IL_0014:  ldarg.0
        IL_0015:  ldarg.3
        IL_0016:  stfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@19-4'::y
        IL_001b:  ret
      } // end of method 'f3@19-4'::.ctor

      .method public strict virtual instance class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> 
              Invoke(int32 _arg4) cil managed
      {
        // Code size       46 (0x2e)
        .maxstack  6
        .locals init ([0] int32 x4,
                 [1] int32 z,
                 [2] class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder V_2,
                 [3] int32 V_3)
        .line 18,18 : 17,31 ''
        IL_0000:  ldarg.1
        IL_0001:  stloc.0
        .line 19,19 : 17,37 ''
        IL_0002:  ldarg.0
        IL_0003:  ldfld      int32 AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@19-4'::x1
        IL_0008:  ldarg.0
        IL_0009:  ldfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@19-4'::y
        IL_000e:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.Operators::op_Dereference<int32>(class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<!!0>)
        IL_0013:  add
        IL_0014:  ldloc.0
        IL_0015:  add
        IL_0016:  stloc.1
        .line 20,20 : 17,25 ''
        IL_0017:  ldarg.0
        IL_0018:  ldfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@19-4'::builder@
        IL_001d:  stloc.2
        IL_001e:  ldloc.1
        IL_001f:  stloc.3
        IL_0020:  ldloc.3
        IL_0021:  newobj     instance void AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@20-5'::.ctor(int32)
        IL_0026:  tail.
        IL_0028:  call       class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<!!0> [FSharp.Core]Microsoft.FSharp.Control.AsyncPrimitives::MakeAsync<int32>(class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<!!0>,class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn>)
        IL_002d:  ret
      } // end of method 'f3@19-4'::Invoke

    } // end of class 'f3@19-4'

    .class auto ansi serializable sealed nested assembly beforefieldinit 'f3@18-6'
           extends class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<int32>,class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn>
    {
      .field public class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> computation
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .field public class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>> binder
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .method assembly specialname rtspecialname 
              instance void  .ctor(class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> computation,
                                   class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>> binder) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       21 (0x15)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<int32>,class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn>::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  ldarg.1
        IL_0008:  stfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@18-6'::computation
        IL_000d:  ldarg.0
        IL_000e:  ldarg.2
        IL_000f:  stfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>> AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@18-6'::binder
        IL_0014:  ret
      } // end of method 'f3@18-6'::.ctor

      .method public strict virtual instance class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn 
              Invoke(valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<int32> ctxt) cil managed
      {
        // Code size       21 (0x15)
        .maxstack  8
        .line 18,18 : 17,31 ''
        IL_0000:  ldarg.1
        IL_0001:  ldarg.0
        IL_0002:  ldfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@18-6'::computation
        IL_0007:  ldarg.0
        IL_0008:  ldfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>> AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@18-6'::binder
        IL_000d:  tail.
        IL_000f:  call       class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn [FSharp.Core]Microsoft.FSharp.Control.AsyncPrimitives::Bind<int32,int32>(valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<!!0>,
                                                                                                                                                              class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<!!1>,
                                                                                                                                                              class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<!!1,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<!!0>>)
        IL_0014:  ret
      } // end of method 'f3@18-6'::Invoke

    } // end of class 'f3@18-6'

    .class auto ansi serializable sealed nested assembly beforefieldinit 'f3@16-3'
           extends class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>>
    {
      .field public class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder builder@
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .field public int32 x1
      .method assembly specialname rtspecialname 
              instance void  .ctor(class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder builder@,
                                   int32 x1) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       21 (0x15)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>>::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  ldarg.1
        IL_0008:  stfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@16-3'::builder@
        IL_000d:  ldarg.0
        IL_000e:  ldarg.2
        IL_000f:  stfld      int32 AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@16-3'::x1
        IL_0014:  ret
      } // end of method 'f3@16-3'::.ctor

      .method public strict virtual instance class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> 
              Invoke(int32 _arg3) cil managed
      {
        // Code size       65 (0x41)
        .maxstack  7
        .locals init ([0] int32 x3,
                 [1] class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> y,
                 [2] class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder V_2,
                 [3] class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> V_3,
                 [4] class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>> V_4)
        .line 15,15 : 17,31 ''
        IL_0000:  ldarg.1
        IL_0001:  stloc.0
        .line 16,16 : 17,30 ''
        IL_0002:  ldc.i4.0
        IL_0003:  call       class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<!!0> [FSharp.Core]Microsoft.FSharp.Core.Operators::Ref<int32>(!!0)
        IL_0008:  stloc.1
        .line 17,17 : 17,23 ''
        IL_0009:  ldloc.1
        IL_000a:  call       void [FSharp.Core]Microsoft.FSharp.Core.Operators::Increment(class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32>)
        IL_000f:  nop
        .line 18,18 : 17,31 ''
        IL_0010:  ldarg.0
        IL_0011:  ldfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@16-3'::builder@
        IL_0016:  stloc.2
        IL_0017:  call       class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6::f2()
        IL_001c:  stloc.3
        IL_001d:  ldarg.0
        IL_001e:  ldfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@16-3'::builder@
        IL_0023:  ldarg.0
        IL_0024:  ldfld      int32 AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@16-3'::x1
        IL_0029:  ldloc.1
        IL_002a:  newobj     instance void AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@19-4'::.ctor(class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder,
                                                                                                                      int32,
                                                                                                                      class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32>)
        IL_002f:  stloc.s    V_4
        IL_0031:  ldloc.3
        IL_0032:  ldloc.s    V_4
        IL_0034:  newobj     instance void AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@18-6'::.ctor(class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>,
                                                                                                                      class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>>)
        IL_0039:  tail.
        IL_003b:  call       class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<!!0> [FSharp.Core]Microsoft.FSharp.Control.AsyncPrimitives::MakeAsync<int32>(class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<!!0>,class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn>)
        IL_0040:  ret
      } // end of method 'f3@16-3'::Invoke

    } // end of class 'f3@16-3'

    .class auto ansi serializable sealed nested assembly beforefieldinit 'f3@15-7'
           extends class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<int32>,class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn>
    {
      .field public class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> computation
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .field public class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>> binder
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .method assembly specialname rtspecialname 
              instance void  .ctor(class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> computation,
                                   class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>> binder) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       21 (0x15)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<int32>,class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn>::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  ldarg.1
        IL_0008:  stfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@15-7'::computation
        IL_000d:  ldarg.0
        IL_000e:  ldarg.2
        IL_000f:  stfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>> AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@15-7'::binder
        IL_0014:  ret
      } // end of method 'f3@15-7'::.ctor

      .method public strict virtual instance class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn 
              Invoke(valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<int32> ctxt) cil managed
      {
        // Code size       21 (0x15)
        .maxstack  8
        .line 15,15 : 17,31 ''
        IL_0000:  ldarg.1
        IL_0001:  ldarg.0
        IL_0002:  ldfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@15-7'::computation
        IL_0007:  ldarg.0
        IL_0008:  ldfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>> AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@15-7'::binder
        IL_000d:  tail.
        IL_000f:  call       class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn [FSharp.Core]Microsoft.FSharp.Control.AsyncPrimitives::Bind<int32,int32>(valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<!!0>,
                                                                                                                                                              class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<!!1>,
                                                                                                                                                              class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<!!1,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<!!0>>)
        IL_0014:  ret
      } // end of method 'f3@15-7'::Invoke

    } // end of class 'f3@15-7'

    .class auto ansi serializable sealed nested assembly beforefieldinit 'f3@15-2'
           extends class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>>
    {
      .field public class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder builder@
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .field public int32 x1
      .method assembly specialname rtspecialname 
              instance void  .ctor(class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder builder@,
                                   int32 x1) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       21 (0x15)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>>::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  ldarg.1
        IL_0008:  stfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@15-2'::builder@
        IL_000d:  ldarg.0
        IL_000e:  ldarg.2
        IL_000f:  stfld      int32 AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@15-2'::x1
        IL_0014:  ret
      } // end of method 'f3@15-2'::.ctor

      .method public strict virtual instance class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> 
              Invoke(int32 _arg2) cil managed
      {
        // Code size       48 (0x30)
        .maxstack  6
        .locals init ([0] int32 x2,
                 [1] class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder V_1,
                 [2] class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> V_2,
                 [3] class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>> V_3)
        .line 14,14 : 17,31 ''
        IL_0000:  ldarg.1
        IL_0001:  stloc.0
        .line 15,15 : 17,31 ''
        IL_0002:  ldarg.0
        IL_0003:  ldfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@15-2'::builder@
        IL_0008:  stloc.1
        IL_0009:  call       class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6::f2()
        IL_000e:  stloc.2
        IL_000f:  ldarg.0
        IL_0010:  ldfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@15-2'::builder@
        IL_0015:  ldarg.0
        IL_0016:  ldfld      int32 AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@15-2'::x1
        IL_001b:  newobj     instance void AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@16-3'::.ctor(class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder,
                                                                                                                      int32)
        IL_0020:  stloc.3
        IL_0021:  ldloc.2
        IL_0022:  ldloc.3
        IL_0023:  newobj     instance void AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@15-7'::.ctor(class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>,
                                                                                                                      class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>>)
        IL_0028:  tail.
        IL_002a:  call       class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<!!0> [FSharp.Core]Microsoft.FSharp.Control.AsyncPrimitives::MakeAsync<int32>(class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<!!0>,class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn>)
        IL_002f:  ret
      } // end of method 'f3@15-2'::Invoke

    } // end of class 'f3@15-2'

    .class auto ansi serializable sealed nested assembly beforefieldinit 'f3@14-8'
           extends class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<int32>,class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn>
    {
      .field public class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> computation
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .field public class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>> binder
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .method assembly specialname rtspecialname 
              instance void  .ctor(class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> computation,
                                   class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>> binder) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       21 (0x15)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<int32>,class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn>::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  ldarg.1
        IL_0008:  stfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@14-8'::computation
        IL_000d:  ldarg.0
        IL_000e:  ldarg.2
        IL_000f:  stfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>> AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@14-8'::binder
        IL_0014:  ret
      } // end of method 'f3@14-8'::.ctor

      .method public strict virtual instance class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn 
              Invoke(valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<int32> ctxt) cil managed
      {
        // Code size       21 (0x15)
        .maxstack  8
        .line 14,14 : 17,31 ''
        IL_0000:  ldarg.1
        IL_0001:  ldarg.0
        IL_0002:  ldfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@14-8'::computation
        IL_0007:  ldarg.0
        IL_0008:  ldfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>> AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@14-8'::binder
        IL_000d:  tail.
        IL_000f:  call       class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn [FSharp.Core]Microsoft.FSharp.Control.AsyncPrimitives::Bind<int32,int32>(valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<!!0>,
                                                                                                                                                              class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<!!1>,
                                                                                                                                                              class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<!!1,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<!!0>>)
        IL_0014:  ret
      } // end of method 'f3@14-8'::Invoke

    } // end of class 'f3@14-8'

    .class auto ansi serializable sealed nested assembly beforefieldinit 'f3@14-1'
           extends class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>>
    {
      .field public class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder builder@
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .method assembly specialname rtspecialname 
              instance void  .ctor(class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder builder@) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       14 (0xe)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>>::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  ldarg.1
        IL_0008:  stfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@14-1'::builder@
        IL_000d:  ret
      } // end of method 'f3@14-1'::.ctor

      .method public strict virtual instance class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> 
              Invoke(int32 _arg1) cil managed
      {
        // Code size       43 (0x2b)
        .maxstack  6
        .locals init ([0] int32 x1,
                 [1] class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder V_1,
                 [2] class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> V_2,
                 [3] class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>> V_3)
        .line 13,13 : 17,31 ''
        IL_0000:  ldarg.1
        IL_0001:  stloc.0
        .line 14,14 : 17,31 ''
        IL_0002:  ldarg.0
        IL_0003:  ldfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@14-1'::builder@
        IL_0008:  stloc.1
        IL_0009:  call       class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6::f2()
        IL_000e:  stloc.2
        IL_000f:  ldarg.0
        IL_0010:  ldfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@14-1'::builder@
        IL_0015:  ldloc.0
        IL_0016:  newobj     instance void AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@15-2'::.ctor(class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder,
                                                                                                                      int32)
        IL_001b:  stloc.3
        IL_001c:  ldloc.2
        IL_001d:  ldloc.3
        IL_001e:  newobj     instance void AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@14-8'::.ctor(class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>,
                                                                                                                      class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>>)
        IL_0023:  tail.
        IL_0025:  call       class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<!!0> [FSharp.Core]Microsoft.FSharp.Control.AsyncPrimitives::MakeAsync<int32>(class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<!!0>,class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn>)
        IL_002a:  ret
      } // end of method 'f3@14-1'::Invoke

    } // end of class 'f3@14-1'

    .class auto ansi serializable sealed nested assembly beforefieldinit 'f3@13-9'
           extends class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<int32>,class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn>
    {
      .field public class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> computation
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .field public class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>> binder
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .method assembly specialname rtspecialname 
              instance void  .ctor(class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> computation,
                                   class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>> binder) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       21 (0x15)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<int32>,class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn>::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  ldarg.1
        IL_0008:  stfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@13-9'::computation
        IL_000d:  ldarg.0
        IL_000e:  ldarg.2
        IL_000f:  stfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>> AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@13-9'::binder
        IL_0014:  ret
      } // end of method 'f3@13-9'::.ctor

      .method public strict virtual instance class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn 
              Invoke(valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<int32> ctxt) cil managed
      {
        // Code size       21 (0x15)
        .maxstack  8
        .line 13,13 : 17,31 ''
        IL_0000:  ldarg.1
        IL_0001:  ldarg.0
        IL_0002:  ldfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@13-9'::computation
        IL_0007:  ldarg.0
        IL_0008:  ldfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>> AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@13-9'::binder
        IL_000d:  tail.
        IL_000f:  call       class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn [FSharp.Core]Microsoft.FSharp.Control.AsyncPrimitives::Bind<int32,int32>(valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<!!0>,
                                                                                                                                                              class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<!!1>,
                                                                                                                                                              class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<!!1,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<!!0>>)
        IL_0014:  ret
      } // end of method 'f3@13-9'::Invoke

    } // end of class 'f3@13-9'

    .class auto ansi serializable sealed nested assembly beforefieldinit f3@13
           extends class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>>
    {
      .field public class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder builder@
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .method assembly specialname rtspecialname 
              instance void  .ctor(class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder builder@) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       14 (0xe)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>>::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  ldarg.1
        IL_0008:  stfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/f3@13::builder@
        IL_000d:  ret
      } // end of method f3@13::.ctor

      .method public strict virtual instance class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> 
              Invoke(class [FSharp.Core]Microsoft.FSharp.Core.Unit unitVar) cil managed
      {
        // Code size       40 (0x28)
        .maxstack  6
        .locals init ([0] class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder V_0,
                 [1] class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> V_1,
                 [2] class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>> V_2)
        .line 13,13 : 17,31 ''
        IL_0000:  ldarg.0
        IL_0001:  ldfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/f3@13::builder@
        IL_0006:  stloc.0
        IL_0007:  call       class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6::f2()
        IL_000c:  stloc.1
        IL_000d:  ldarg.0
        IL_000e:  ldfld      class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/f3@13::builder@
        IL_0013:  newobj     instance void AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@14-1'::.ctor(class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder)
        IL_0018:  stloc.2
        IL_0019:  ldloc.1
        IL_001a:  ldloc.2
        IL_001b:  newobj     instance void AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/'f3@13-9'::.ctor(class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>,
                                                                                                                      class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32>>)
        IL_0020:  tail.
        IL_0022:  call       class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<!!0> [FSharp.Core]Microsoft.FSharp.Control.AsyncPrimitives::MakeAsync<int32>(class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype [FSharp.Core]Microsoft.FSharp.Control.AsyncActivation`1<!!0>,class [FSharp.Core]Microsoft.FSharp.Control.AsyncReturn>)
        IL_0027:  ret
      } // end of method f3@13::Invoke

    } // end of class f3@13

    .method public static class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> 
            f2() cil managed
    {
      // Code size       21 (0x15)
      .maxstack  4
      .locals init ([0] class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder V_0)
      .line 5,5 : 9,14 ''
      IL_0000:  call       class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::get_DefaultAsyncBuilder()
      IL_0005:  stloc.0
      IL_0006:  ldloc.0
      IL_0007:  ldloc.0
      IL_0008:  newobj     instance void AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/f2@5::.ctor(class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder)
      IL_000d:  tail.
      IL_000f:  callvirt   instance class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<!!0> [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder::Delay<int32>(class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<!!0>>)
      IL_0014:  ret
    } // end of method AsyncExpressionSteppingTest6::f2

    .method public static class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> 
            f3() cil managed
    {
      // Code size       21 (0x15)
      .maxstack  4
      .locals init ([0] class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder V_0)
      .line 13,13 : 9,14 ''
      IL_0000:  call       class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::get_DefaultAsyncBuilder()
      IL_0005:  stloc.0
      IL_0006:  ldloc.0
      IL_0007:  ldloc.0
      IL_0008:  newobj     instance void AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6/f3@13::.ctor(class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder)
      IL_000d:  tail.
      IL_000f:  callvirt   instance class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<!!0> [FSharp.Core]Microsoft.FSharp.Control.FSharpAsyncBuilder::Delay<int32>(class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<!!0>>)
      IL_0014:  ret
    } // end of method AsyncExpressionSteppingTest6::f3

  } // end of class AsyncExpressionSteppingTest6

} // end of class AsyncExpressionSteppingTest6

.class private abstract auto ansi sealed '<StartupCode$AsyncExpressionSteppingTest6>'.$AsyncExpressionSteppingTest6
       extends [mscorlib]System.Object
{
  .field static assembly int32 init@
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
  .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
  .method private specialname rtspecialname static 
          void  .cctor() cil managed
  {
    // Code size       18 (0x12)
    .maxstack  5
    .locals init ([0] class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> V_0,
             [1] class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> V_1)
    .line 22,22 : 13,43 ''
    IL_0000:  call       class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<int32> AsyncExpressionSteppingTest6/AsyncExpressionSteppingTest6::f3()
    IL_0005:  stloc.0
    IL_0006:  ldloc.0
    IL_0007:  stloc.1
    IL_0008:  ldloc.1
    IL_0009:  ldnull
    IL_000a:  ldnull
    IL_000b:  call       !!0 [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync::RunSynchronously<int32>(class [FSharp.Core]Microsoft.FSharp.Control.FSharpAsync`1<!!0>,
                                                                                                        class [FSharp.Core]Microsoft.FSharp.Core.FSharpOption`1<int32>,
                                                                                                        class [FSharp.Core]Microsoft.FSharp.Core.FSharpOption`1<valuetype [mscorlib]System.Threading.CancellationToken>)
    IL_0010:  pop
    IL_0011:  ret
  } // end of method $AsyncExpressionSteppingTest6::.cctor

} // end of class '<StartupCode$AsyncExpressionSteppingTest6>'.$AsyncExpressionSteppingTest6


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
