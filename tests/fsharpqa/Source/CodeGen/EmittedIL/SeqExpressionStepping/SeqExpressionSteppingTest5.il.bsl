
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
.assembly SeqExpressionSteppingTest5
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.SeqExpressionSteppingTest5
{
  // Offset: 0x00000000 Length: 0x00000263
}
.mresource public FSharpOptimizationData.SeqExpressionSteppingTest5
{
  // Offset: 0x00000268 Length: 0x000000AD
}
.module SeqExpressionSteppingTest5.exe
// MVID: {60BD414C-2432-9401-A745-03834C41BD60}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x05410000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed SeqExpressionSteppingTest5
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class abstract auto ansi sealed nested public SeqExpressionSteppingTest5
         extends [mscorlib]System.Object
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
    .class auto autochar serializable sealed nested assembly beforefieldinit specialname f4@5
           extends class [FSharp.Core]Microsoft.FSharp.Core.CompilerServices.GeneratedSequenceBase`1<int32>
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 06 00 00 00 00 00 ) 
      .field public class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> x
      .field public class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> y
      .field public int32 pc
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .field public int32 current
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .method public specialname rtspecialname 
              instance void  .ctor(class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> x,
                                   class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> y,
                                   int32 pc,
                                   int32 current) cil managed
      {
        // Code size       36 (0x24)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldarg.1
        IL_0002:  stfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::x
        IL_0007:  ldarg.0
        IL_0008:  ldarg.2
        IL_0009:  stfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::y
        IL_000e:  ldarg.0
        IL_000f:  ldarg.3
        IL_0010:  stfld      int32 SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::pc
        IL_0015:  ldarg.0
        IL_0016:  ldarg.s    current
        IL_0018:  stfld      int32 SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::current
        IL_001d:  ldarg.0
        IL_001e:  call       instance void class [FSharp.Core]Microsoft.FSharp.Core.CompilerServices.GeneratedSequenceBase`1<int32>::.ctor()
        IL_0023:  ret
      } // end of method f4@5::.ctor

      .method public strict virtual instance int32 
              GenerateNext(class [mscorlib]System.Collections.Generic.IEnumerable`1<int32>& next) cil managed
      {
        // Code size       224 (0xe0)
        .maxstack  6
        .locals init ([0] int32 z)
        .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
        .line 100001,100001 : 0,0 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\SeqExpressionStepping\\SeqExpressionSteppingTest5.fs'
        IL_0000:  ldarg.0
        IL_0001:  ldfld      int32 SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::pc
        IL_0006:  ldc.i4.1
        IL_0007:  sub
        IL_0008:  switch     ( 
                              IL_001f,
                              IL_0025,
                              IL_0028,
                              IL_002b)
        IL_001d:  br.s       IL_0031

        .line 100001,100001 : 0,0 ''
        IL_001f:  nop
        IL_0020:  br         IL_00a6

        .line 100001,100001 : 0,0 ''
        IL_0025:  nop
        IL_0026:  br.s       IL_0077

        .line 100001,100001 : 0,0 ''
        IL_0028:  nop
        IL_0029:  br.s       IL_009f

        .line 100001,100001 : 0,0 ''
        IL_002b:  nop
        IL_002c:  br         IL_00d7

        .line 100001,100001 : 0,0 ''
        IL_0031:  nop
        .line 5,5 : 15,28 ''
        IL_0032:  ldarg.0
        IL_0033:  ldc.i4.0
        IL_0034:  call       class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<!!0> [FSharp.Core]Microsoft.FSharp.Core.Operators::Ref<int32>(!!0)
        IL_0039:  stfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::x
        IL_003e:  ldarg.0
        IL_003f:  ldc.i4.1
        IL_0040:  stfld      int32 SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::pc
        .line 7,7 : 19,32 ''
        IL_0045:  ldarg.0
        IL_0046:  ldc.i4.0
        IL_0047:  call       class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<!!0> [FSharp.Core]Microsoft.FSharp.Core.Operators::Ref<int32>(!!0)
        IL_004c:  stfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::y
        .line 8,8 : 19,25 ''
        IL_0051:  ldarg.0
        IL_0052:  ldfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::y
        IL_0057:  call       void [FSharp.Core]Microsoft.FSharp.Core.Operators::Increment(class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32>)
        IL_005c:  nop
        .line 9,9 : 19,27 ''
        IL_005d:  ldarg.0
        IL_005e:  ldc.i4.2
        IL_005f:  stfld      int32 SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::pc
        IL_0064:  ldarg.0
        IL_0065:  ldarg.0
        IL_0066:  ldfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::x
        IL_006b:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.Operators::op_Dereference<int32>(class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<!!0>)
        IL_0070:  stfld      int32 SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::current
        IL_0075:  ldc.i4.1
        IL_0076:  ret

        .line 10,10 : 19,34 ''
        IL_0077:  ldarg.0
        IL_0078:  ldfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::x
        IL_007d:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.Operators::op_Dereference<int32>(class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<!!0>)
        IL_0082:  ldarg.0
        IL_0083:  ldfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::y
        IL_0088:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.Operators::op_Dereference<int32>(class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<!!0>)
        IL_008d:  add
        IL_008e:  stloc.0
        .line 11,11 : 19,26 ''
        IL_008f:  ldarg.0
        IL_0090:  ldc.i4.3
        IL_0091:  stfld      int32 SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::pc
        IL_0096:  ldarg.0
        IL_0097:  ldloc.0
        IL_0098:  stfld      int32 SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::current
        IL_009d:  ldc.i4.1
        IL_009e:  ret

        IL_009f:  ldarg.0
        IL_00a0:  ldnull
        IL_00a1:  stfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::y
        IL_00a6:  ldarg.0
        IL_00a7:  ldc.i4.4
        IL_00a8:  stfld      int32 SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::pc
        .line 13,13 : 18,24 ''
        IL_00ad:  ldarg.0
        IL_00ae:  ldfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::x
        IL_00b3:  call       void [FSharp.Core]Microsoft.FSharp.Core.Operators::Increment(class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32>)
        IL_00b8:  nop
        .line 14,14 : 18,32 ''
        IL_00b9:  ldstr      "done"
        IL_00be:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [mscorlib]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
        IL_00c3:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatLine<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [mscorlib]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
        IL_00c8:  pop
        IL_00c9:  ldarg.0
        IL_00ca:  ldnull
        IL_00cb:  stfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::x
        IL_00d0:  ldarg.0
        IL_00d1:  ldc.i4.4
        IL_00d2:  stfld      int32 SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::pc
        IL_00d7:  ldarg.0
        IL_00d8:  ldc.i4.0
        IL_00d9:  stfld      int32 SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::current
        IL_00de:  ldc.i4.0
        IL_00df:  ret
      } // end of method f4@5::GenerateNext

      .method public strict virtual instance void 
              Close() cil managed
      {
        // Code size       156 (0x9c)
        .maxstack  6
        .locals init ([0] class [mscorlib]System.Exception V_0,
                 [1] class [mscorlib]System.Exception e)
        .line 100001,100001 : 0,0 ''
        IL_0000:  ldarg.0
        IL_0001:  ldfld      int32 SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::pc
        IL_0006:  ldc.i4.4
        IL_0007:  sub
        IL_0008:  switch     ( 
                              IL_0013)
        IL_0011:  br.s       IL_0019

        .line 100001,100001 : 0,0 ''
        IL_0013:  nop
        IL_0014:  br         IL_0093

        .line 100001,100001 : 0,0 ''
        IL_0019:  nop
        .try
        {
          IL_001a:  ldarg.0
          IL_001b:  ldfld      int32 SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::pc
          IL_0020:  switch     ( 
                                IL_003b,
                                IL_003e,
                                IL_0041,
                                IL_0044,
                                IL_0047)
          IL_0039:  br.s       IL_004a

          .line 100001,100001 : 0,0 ''
          IL_003b:  nop
          IL_003c:  br.s       IL_0073

          .line 100001,100001 : 0,0 ''
          IL_003e:  nop
          IL_003f:  br.s       IL_004f

          .line 100001,100001 : 0,0 ''
          IL_0041:  nop
          IL_0042:  br.s       IL_004e

          .line 100001,100001 : 0,0 ''
          IL_0044:  nop
          IL_0045:  br.s       IL_004b

          .line 100001,100001 : 0,0 ''
          IL_0047:  nop
          IL_0048:  br.s       IL_0073

          .line 100001,100001 : 0,0 ''
          IL_004a:  nop
          .line 100001,100001 : 0,0 ''
          IL_004b:  nop
          IL_004c:  br.s       IL_004f

          .line 100001,100001 : 0,0 ''
          IL_004e:  nop
          IL_004f:  ldarg.0
          IL_0050:  ldc.i4.4
          IL_0051:  stfld      int32 SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::pc
          .line 13,13 : 18,24 ''
          IL_0056:  ldarg.0
          IL_0057:  ldfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::x
          IL_005c:  call       void [FSharp.Core]Microsoft.FSharp.Core.Operators::Increment(class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32>)
          IL_0061:  nop
          .line 14,14 : 18,32 ''
          IL_0062:  ldstr      "done"
          IL_0067:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [mscorlib]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
          IL_006c:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatLine<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [mscorlib]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
          IL_0071:  pop
          .line 100001,100001 : 0,0 ''
          IL_0072:  nop
          IL_0073:  ldarg.0
          IL_0074:  ldc.i4.4
          IL_0075:  stfld      int32 SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::pc
          IL_007a:  ldarg.0
          IL_007b:  ldc.i4.0
          IL_007c:  stfld      int32 SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::current
          IL_0081:  leave.s    IL_008d

        }  // end .try
        catch [mscorlib]System.Object 
        {
          IL_0083:  castclass  [mscorlib]System.Exception
          IL_0088:  stloc.1
          .line 5,5 : 19,20 ''
          IL_0089:  ldloc.1
          IL_008a:  stloc.0
          IL_008b:  leave.s    IL_008d

          .line 100001,100001 : 0,0 ''
        }  // end handler
        IL_008d:  nop
        IL_008e:  br         IL_0000

        IL_0093:  ldloc.0
        IL_0094:  ldnull
        IL_0095:  cgt.un
        IL_0097:  brfalse.s  IL_009b

        .line 100001,100001 : 0,0 ''
        IL_0099:  ldloc.0
        IL_009a:  throw

        .line 100001,100001 : 0,0 ''
        IL_009b:  ret
      } // end of method f4@5::Close

      .method public strict virtual instance bool 
              get_CheckClose() cil managed
      {
        // Code size       57 (0x39)
        .maxstack  8
        .line 100001,100001 : 0,0 ''
        IL_0000:  ldarg.0
        IL_0001:  ldfld      int32 SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::pc
        IL_0006:  switch     ( 
                              IL_0021,
                              IL_0024,
                              IL_0027,
                              IL_002a,
                              IL_002d)
        IL_001f:  br.s       IL_0030

        .line 100001,100001 : 0,0 ''
        IL_0021:  nop
        IL_0022:  br.s       IL_0037

        .line 100001,100001 : 0,0 ''
        IL_0024:  nop
        IL_0025:  br.s       IL_0035

        .line 100001,100001 : 0,0 ''
        IL_0027:  nop
        IL_0028:  br.s       IL_0033

        .line 100001,100001 : 0,0 ''
        IL_002a:  nop
        IL_002b:  br.s       IL_0031

        .line 100001,100001 : 0,0 ''
        IL_002d:  nop
        IL_002e:  br.s       IL_0037

        .line 100001,100001 : 0,0 ''
        IL_0030:  nop
        IL_0031:  ldc.i4.1
        IL_0032:  ret

        IL_0033:  ldc.i4.1
        IL_0034:  ret

        IL_0035:  ldc.i4.1
        IL_0036:  ret

        IL_0037:  ldc.i4.0
        IL_0038:  ret
      } // end of method f4@5::get_CheckClose

      .method public strict virtual instance int32 
              get_LastGenerated() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       7 (0x7)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldfld      int32 SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::current
        IL_0006:  ret
      } // end of method f4@5::get_LastGenerated

      .method public strict virtual instance class [mscorlib]System.Collections.Generic.IEnumerator`1<int32> 
              GetFreshEnumerator() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       10 (0xa)
        .maxstack  8
        IL_0000:  ldnull
        IL_0001:  ldnull
        IL_0002:  ldc.i4.0
        IL_0003:  ldc.i4.0
        IL_0004:  newobj     instance void SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::.ctor(class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32>,
                                                                                                             class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32>,
                                                                                                             int32,
                                                                                                             int32)
        IL_0009:  ret
      } // end of method f4@5::GetFreshEnumerator

    } // end of class f4@5

    .method public static class [mscorlib]System.Collections.Generic.IEnumerable`1<int32> 
            f4() cil managed
    {
      // Code size       10 (0xa)
      .maxstack  8
      .line 5,14 : 9,34 ''
      IL_0000:  ldnull
      IL_0001:  ldnull
      IL_0002:  ldc.i4.0
      IL_0003:  ldc.i4.0
      IL_0004:  newobj     instance void SeqExpressionSteppingTest5/SeqExpressionSteppingTest5/f4@5::.ctor(class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32>,
                                                                                                           class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32>,
                                                                                                           int32,
                                                                                                           int32)
      IL_0009:  ret
    } // end of method SeqExpressionSteppingTest5::f4

  } // end of class SeqExpressionSteppingTest5

} // end of class SeqExpressionSteppingTest5

.class private abstract auto ansi sealed '<StartupCode$SeqExpressionSteppingTest5>'.$SeqExpressionSteppingTest5
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
    .line 16,16 : 13,30 ''
    IL_0000:  call       class [mscorlib]System.Collections.Generic.IEnumerable`1<int32> SeqExpressionSteppingTest5/SeqExpressionSteppingTest5::f4()
    IL_0005:  call       int32 [FSharp.Core]Microsoft.FSharp.Collections.SeqModule::Length<int32>(class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0>)
    IL_000a:  pop
    IL_000b:  ret
  } // end of method $SeqExpressionSteppingTest5::main@

} // end of class '<StartupCode$SeqExpressionSteppingTest5>'.$SeqExpressionSteppingTest5


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
