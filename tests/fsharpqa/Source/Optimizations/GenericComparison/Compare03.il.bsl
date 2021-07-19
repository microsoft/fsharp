
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
.assembly extern netstandard
{
  .publickeytoken = (CC 7B 13 FF CD 2D DD 51 )                         // .{...-.Q
  .ver 2:0:0:0
}
.assembly Compare03
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 00 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.Compare03
{
  // Offset: 0x00000000 Length: 0x00000233
}
.mresource public FSharpOptimizationData.Compare03
{
  // Offset: 0x00000238 Length: 0x000000B9
}
.module Compare03.dll
// MVID: {60BE1F16-0562-F88E-A745-0383161FBE60}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x09540000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed Compare03
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class abstract auto ansi sealed nested public CompareMicroPerfAndCodeGenerationTests
         extends [mscorlib]System.Object
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
    .method public static int32  f4_tuple4() cil managed
    {
      // Code size       76 (0x4c)
      .maxstack  4
      .locals init ([0] int32 x,
               [1] int32 i,
               [2] int32 V_2,
               [3] int32 V_3,
               [4] int32 V_4)
      .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
      .line 5,5 : 8,25 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\Optimizations\\GenericComparison\\Compare03.fsx'
      IL_0000:  ldc.i4.1
      IL_0001:  stloc.0
      .line 8,8 : 8,32 ''
      IL_0002:  ldc.i4.0
      IL_0003:  stloc.1
      IL_0004:  br.s       IL_0042

      .line 9,9 : 12,30 ''
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
      IL_0010:  br.s       IL_003d

      .line 16707566,16707566 : 0,0 ''
      IL_0012:  ldc.i4.2
      IL_0013:  ldc.i4.2
      IL_0014:  cgt
      IL_0016:  stloc.3
      IL_0017:  ldloc.3
      IL_0018:  brfalse.s  IL_001e

      .line 16707566,16707566 : 0,0 ''
      IL_001a:  ldloc.3
      .line 16707566,16707566 : 0,0 ''
      IL_001b:  nop
      IL_001c:  br.s       IL_003d

      .line 16707566,16707566 : 0,0 ''
      IL_001e:  ldc.i4.4
      IL_001f:  ldc.i4.4
      IL_0020:  cgt
      IL_0022:  stloc.s    V_4
      IL_0024:  ldloc.s    V_4
      IL_0026:  brfalse.s  IL_002d

      .line 16707566,16707566 : 0,0 ''
      IL_0028:  ldloc.s    V_4
      .line 16707566,16707566 : 0,0 ''
      IL_002a:  nop
      IL_002b:  br.s       IL_003d

      .line 16707566,16707566 : 0,0 ''
      IL_002d:  ldstr      "five"
      IL_0032:  ldstr      "5"
      IL_0037:  call       int32 [netstandard]System.String::CompareOrdinal(string,
                                                                            string)
      .line 16707566,16707566 : 0,0 ''
      IL_003c:  nop
      .line 16707566,16707566 : 0,0 ''
      IL_003d:  stloc.0
      IL_003e:  ldloc.1
      IL_003f:  ldc.i4.1
      IL_0040:  add
      IL_0041:  stloc.1
      .line 8,8 : 8,32 ''
      IL_0042:  ldloc.1
      IL_0043:  ldc.i4     0x989681
      IL_0048:  blt.s      IL_0006

      .line 10,10 : 8,9 ''
      IL_004a:  ldloc.0
      IL_004b:  ret
    } // end of method CompareMicroPerfAndCodeGenerationTests::f4_tuple4

  } // end of class CompareMicroPerfAndCodeGenerationTests

} // end of class Compare03

.class private abstract auto ansi sealed '<StartupCode$Compare03>'.$Compare03$fsx
       extends [mscorlib]System.Object
{
} // end of class '<StartupCode$Compare03>'.$Compare03$fsx


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
