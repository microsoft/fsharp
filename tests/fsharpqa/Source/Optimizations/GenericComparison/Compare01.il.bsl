
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
.assembly Compare01
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 00 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.Compare01
{
  // Offset: 0x00000000 Length: 0x00000221
}
.mresource public FSharpOptimizationData.Compare01
{
  // Offset: 0x00000228 Length: 0x000000B2
}
.module Compare01.dll
// MVID: {60BE1F16-04A0-F88E-A745-0383161FBE60}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x05230000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed Compare01
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class abstract auto ansi sealed nested public CompareMicroPerfAndCodeGenerationTests
         extends [mscorlib]System.Object
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
    .method public static void  f4() cil managed
    {
      // Code size       34 (0x22)
      .maxstack  4
      .locals init ([0] int32 x,
               [1] int32 i,
               [2] int32 V_2)
      .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
      .line 5,5 : 8,25 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\Optimizations\\GenericComparison\\Compare01.fsx'
      IL_0000:  ldc.i4.1
      IL_0001:  stloc.0
      .line 9,9 : 8,32 ''
      IL_0002:  ldc.i4.0
      IL_0003:  stloc.1
      IL_0004:  br.s       IL_0019

      .line 10,10 : 12,30 ''
      IL_0006:  ldc.i4.1
      IL_0007:  ldc.i4.1
      IL_0008:  cgt
      IL_000a:  stloc.2
      IL_000b:  ldloc.2
      IL_000c:  brfalse.s  IL_0012

      .line 16707566,16707566 : 0,0 ''
      IL_000e:  ldloc.2
      .line 16707566,16707566 : 0,0 ''
      IL_000f:  nop
      IL_0010:  br.s       IL_0014

      .line 16707566,16707566 : 0,0 ''
      IL_0012:  ldc.i4.m1
      .line 16707566,16707566 : 0,0 ''
      IL_0013:  nop
      .line 16707566,16707566 : 0,0 ''
      IL_0014:  stloc.0
      IL_0015:  ldloc.1
      IL_0016:  ldc.i4.1
      IL_0017:  add
      IL_0018:  stloc.1
      .line 9,9 : 8,32 ''
      IL_0019:  ldloc.1
      IL_001a:  ldc.i4     0x989681
      IL_001f:  blt.s      IL_0006

      IL_0021:  ret
    } // end of method CompareMicroPerfAndCodeGenerationTests::f4

  } // end of class CompareMicroPerfAndCodeGenerationTests

} // end of class Compare01

.class private abstract auto ansi sealed '<StartupCode$Compare01>'.$Compare01$fsx
       extends [mscorlib]System.Object
{
} // end of class '<StartupCode$Compare01>'.$Compare01$fsx


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
