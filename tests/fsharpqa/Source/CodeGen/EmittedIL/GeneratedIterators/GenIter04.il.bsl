
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
.assembly GenIter04
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.GenIter04
{
  // Offset: 0x00000000 Length: 0x000001E4
}
.mresource public FSharpOptimizationData.GenIter04
{
  // Offset: 0x000001E8 Length: 0x0000007B
}
.module GenIter04.exe
// MVID: {60B78A57-F79D-DC98-A745-0383578AB760}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x06F10000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed GenIter04
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .method public specialname static class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32> 
          get_squaresOfOneToTenD() cil managed
  {
    // Code size       6 (0x6)
    .maxstack  8
    IL_0000:  ldsfld     class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32> '<StartupCode$GenIter04>'.$GenIter04::squaresOfOneToTenD@4
    IL_0005:  ret
  } // end of method GenIter04::get_squaresOfOneToTenD

  .property class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>
          squaresOfOneToTenD()
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 09 00 00 00 00 00 ) 
    .get class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32> GenIter04::get_squaresOfOneToTenD()
  } // end of property GenIter04::squaresOfOneToTenD
} // end of class GenIter04

.class private abstract auto ansi sealed '<StartupCode$GenIter04>'.$GenIter04
       extends [mscorlib]System.Object
{
  .field static assembly class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32> squaresOfOneToTenD@4
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .field static assembly int32 init@
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
  .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
  .method public static void  main@() cil managed
  {
    .entrypoint
    // Code size       93 (0x5d)
    .maxstack  5
    .locals init ([0] class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32> squaresOfOneToTenD,
             [1] valuetype [FSharp.Core]Microsoft.FSharp.Core.CompilerServices.ListCollector`1<int32> V_1,
             [2] class [mscorlib]System.Collections.Generic.IEnumerator`1<int32> V_2,
             [3] class [mscorlib]System.Collections.Generic.IEnumerable`1<int32> V_3,
             [4] int32 x,
             [5] class [mscorlib]System.IDisposable V_5)
    .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
    .line 4,4 : 1,55 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\GeneratedIterators\\GenIter04.fs'
    IL_0000:  ldc.i4.0
    IL_0001:  ldc.i4.1
    IL_0002:  ldc.i4.s   10
    IL_0004:  call       class [mscorlib]System.Collections.Generic.IEnumerable`1<int32> [FSharp.Core]Microsoft.FSharp.Core.Operators/OperatorIntrinsics::RangeInt32(int32,
                                                                                                                                                                     int32,
                                                                                                                                                                     int32)
    IL_0009:  callvirt   instance class [mscorlib]System.Collections.Generic.IEnumerator`1<!0> class [mscorlib]System.Collections.Generic.IEnumerable`1<int32>::GetEnumerator()
    IL_000e:  stloc.2
    .line 4,4 : 28,44 ''
    .try
    {
      IL_000f:  ldloc.2
      IL_0010:  callvirt   instance bool [mscorlib]System.Collections.IEnumerator::MoveNext()
      IL_0015:  brfalse.s  IL_002f

      IL_0017:  ldloc.2
      IL_0018:  callvirt   instance !0 class [mscorlib]System.Collections.Generic.IEnumerator`1<int32>::get_Current()
      IL_001d:  stloc.s    x
      .line 4,4 : 48,53 ''
      IL_001f:  ldloca.s   V_1
      IL_0021:  ldloc.s    x
      IL_0023:  ldloc.s    x
      IL_0025:  mul
      IL_0026:  call       instance void valuetype [FSharp.Core]Microsoft.FSharp.Core.CompilerServices.ListCollector`1<int32>::Add(!0)
      IL_002b:  nop
      .line 100001,100001 : 0,0 ''
      IL_002c:  nop
      IL_002d:  br.s       IL_000f

      IL_002f:  ldnull
      IL_0030:  stloc.3
      IL_0031:  leave.s    IL_004c

      .line 4,4 : 28,53 ''
    }  // end .try
    finally
    {
      IL_0033:  ldloc.2
      IL_0034:  isinst     [mscorlib]System.IDisposable
      IL_0039:  stloc.s    V_5
      IL_003b:  ldloc.s    V_5
      IL_003d:  brfalse.s  IL_0049

      .line 100001,100001 : 0,0 ''
      IL_003f:  ldloc.s    V_5
      IL_0041:  callvirt   instance void [mscorlib]System.IDisposable::Dispose()
      IL_0046:  ldnull
      IL_0047:  pop
      IL_0048:  endfinally
      .line 100001,100001 : 0,0 ''
      IL_0049:  ldnull
      IL_004a:  pop
      IL_004b:  endfinally
      .line 100001,100001 : 0,0 ''
    }  // end handler
    IL_004c:  ldloc.3
    IL_004d:  pop
    IL_004e:  ldloca.s   V_1
    IL_0050:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> valuetype [FSharp.Core]Microsoft.FSharp.Core.CompilerServices.ListCollector`1<int32>::Close()
    IL_0055:  dup
    IL_0056:  stsfld     class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32> '<StartupCode$GenIter04>'.$GenIter04::squaresOfOneToTenD@4
    IL_005b:  stloc.0
    IL_005c:  ret
  } // end of method $GenIter04::main@

} // end of class '<StartupCode$GenIter04>'.$GenIter04


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
