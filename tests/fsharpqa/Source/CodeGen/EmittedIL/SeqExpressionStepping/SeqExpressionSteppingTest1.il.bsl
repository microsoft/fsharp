
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
.assembly SeqExpressionSteppingTest1
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.SeqExpressionSteppingTest1
{
  // Offset: 0x00000000 Length: 0x00000263
}
.mresource public FSharpOptimizationData.SeqExpressionSteppingTest1
{
  // Offset: 0x00000268 Length: 0x000000AD
}
.module SeqExpressionSteppingTest1.exe
// MVID: {60B78A59-2432-947D-A745-0383598AB760}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x06A30000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed SeqExpressionSteppingTest1
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class abstract auto ansi sealed nested public SeqExpressionSteppingTest1
         extends [mscorlib]System.Object
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
    .class auto autochar serializable sealed nested assembly beforefieldinit specialname f0@6
           extends class [FSharp.Core]Microsoft.FSharp.Core.CompilerServices.GeneratedSequenceBase`1<int32>
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 06 00 00 00 00 00 ) 
      .field public int32 pc
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .field public int32 current
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .method public specialname rtspecialname 
              instance void  .ctor(int32 pc,
                                   int32 current) cil managed
      {
        // Code size       21 (0x15)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldarg.1
        IL_0002:  stfld      int32 SeqExpressionSteppingTest1/SeqExpressionSteppingTest1/f0@6::pc
        IL_0007:  ldarg.0
        IL_0008:  ldarg.2
        IL_0009:  stfld      int32 SeqExpressionSteppingTest1/SeqExpressionSteppingTest1/f0@6::current
        IL_000e:  ldarg.0
        IL_000f:  call       instance void class [FSharp.Core]Microsoft.FSharp.Core.CompilerServices.GeneratedSequenceBase`1<int32>::.ctor()
        IL_0014:  ret
      } // end of method f0@6::.ctor

      .method public strict virtual instance int32 
              GenerateNext(class [mscorlib]System.Collections.Generic.IEnumerable`1<int32>& next) cil managed
      {
        // Code size       62 (0x3e)
        .maxstack  8
        .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
        .line 100001,100001 : 0,0 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\SeqExpressionStepping\\SeqExpressionSteppingTest1.fs'
        IL_0000:  ldarg.0
        IL_0001:  ldfld      int32 SeqExpressionSteppingTest1/SeqExpressionSteppingTest1/f0@6::pc
        IL_0006:  ldc.i4.1
        IL_0007:  sub
        IL_0008:  switch     ( 
                              IL_0017,
                              IL_001a)
        IL_0015:  br.s       IL_001d

        .line 100001,100001 : 0,0 ''
        IL_0017:  nop
        IL_0018:  br.s       IL_002e

        .line 100001,100001 : 0,0 ''
        IL_001a:  nop
        IL_001b:  br.s       IL_0035

        .line 100001,100001 : 0,0 ''
        IL_001d:  nop
        .line 6,6 : 15,22 ''
        IL_001e:  ldarg.0
        IL_001f:  ldc.i4.1
        IL_0020:  stfld      int32 SeqExpressionSteppingTest1/SeqExpressionSteppingTest1/f0@6::pc
        IL_0025:  ldarg.0
        IL_0026:  ldc.i4.1
        IL_0027:  stfld      int32 SeqExpressionSteppingTest1/SeqExpressionSteppingTest1/f0@6::current
        IL_002c:  ldc.i4.1
        IL_002d:  ret

        IL_002e:  ldarg.0
        IL_002f:  ldc.i4.2
        IL_0030:  stfld      int32 SeqExpressionSteppingTest1/SeqExpressionSteppingTest1/f0@6::pc
        IL_0035:  ldarg.0
        IL_0036:  ldc.i4.0
        IL_0037:  stfld      int32 SeqExpressionSteppingTest1/SeqExpressionSteppingTest1/f0@6::current
        IL_003c:  ldc.i4.0
        IL_003d:  ret
      } // end of method f0@6::GenerateNext

      .method public strict virtual instance void 
              Close() cil managed
      {
        // Code size       8 (0x8)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldc.i4.2
        IL_0002:  stfld      int32 SeqExpressionSteppingTest1/SeqExpressionSteppingTest1/f0@6::pc
        IL_0007:  ret
      } // end of method f0@6::Close

      .method public strict virtual instance bool 
              get_CheckClose() cil managed
      {
        // Code size       39 (0x27)
        .maxstack  8
        .line 100001,100001 : 0,0 ''
        IL_0000:  ldarg.0
        IL_0001:  ldfld      int32 SeqExpressionSteppingTest1/SeqExpressionSteppingTest1/f0@6::pc
        IL_0006:  switch     ( 
                              IL_0019,
                              IL_001c,
                              IL_001f)
        IL_0017:  br.s       IL_0022

        .line 100001,100001 : 0,0 ''
        IL_0019:  nop
        IL_001a:  br.s       IL_0025

        .line 100001,100001 : 0,0 ''
        IL_001c:  nop
        IL_001d:  br.s       IL_0023

        .line 100001,100001 : 0,0 ''
        IL_001f:  nop
        IL_0020:  br.s       IL_0025

        .line 100001,100001 : 0,0 ''
        IL_0022:  nop
        IL_0023:  ldc.i4.0
        IL_0024:  ret

        IL_0025:  ldc.i4.0
        IL_0026:  ret
      } // end of method f0@6::get_CheckClose

      .method public strict virtual instance int32 
              get_LastGenerated() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       7 (0x7)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldfld      int32 SeqExpressionSteppingTest1/SeqExpressionSteppingTest1/f0@6::current
        IL_0006:  ret
      } // end of method f0@6::get_LastGenerated

      .method public strict virtual instance class [mscorlib]System.Collections.Generic.IEnumerator`1<int32> 
              GetFreshEnumerator() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       8 (0x8)
        .maxstack  8
        IL_0000:  ldc.i4.0
        IL_0001:  ldc.i4.0
        IL_0002:  newobj     instance void SeqExpressionSteppingTest1/SeqExpressionSteppingTest1/f0@6::.ctor(int32,
                                                                                                             int32)
        IL_0007:  ret
      } // end of method f0@6::GetFreshEnumerator

    } // end of class f0@6

    .method public static class [mscorlib]System.Collections.Generic.IEnumerable`1<int32> 
            f0() cil managed
    {
      // Code size       8 (0x8)
      .maxstack  8
      .line 6,6 : 9,24 ''
      IL_0000:  ldc.i4.0
      IL_0001:  ldc.i4.0
      IL_0002:  newobj     instance void SeqExpressionSteppingTest1/SeqExpressionSteppingTest1/f0@6::.ctor(int32,
                                                                                                           int32)
      IL_0007:  ret
    } // end of method SeqExpressionSteppingTest1::f0

  } // end of class SeqExpressionSteppingTest1

} // end of class SeqExpressionSteppingTest1

.class private abstract auto ansi sealed '<StartupCode$SeqExpressionSteppingTest1>'.$SeqExpressionSteppingTest1
       extends [mscorlib]System.Object
{
  .field static assembly int32 init@
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
  .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
  .method public static void  main@() cil managed
  {
    .entrypoint
    // Code size       12 (0xc)
    .maxstack  8
    .line 8,8 : 13,30 ''
    IL_0000:  call       class [mscorlib]System.Collections.Generic.IEnumerable`1<int32> SeqExpressionSteppingTest1/SeqExpressionSteppingTest1::f0()
    IL_0005:  call       int32 [FSharp.Core]Microsoft.FSharp.Collections.SeqModule::Length<int32>(class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0>)
    IL_000a:  pop
    IL_000b:  ret
  } // end of method $SeqExpressionSteppingTest1::main@

} // end of class '<StartupCode$SeqExpressionSteppingTest1>'.$SeqExpressionSteppingTest1


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
