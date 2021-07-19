
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
.assembly Compare04
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 00 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.Compare04
{
  // Offset: 0x00000000 Length: 0x00000233
}
.mresource public FSharpOptimizationData.Compare04
{
  // Offset: 0x00000238 Length: 0x000000B9
}
.module Compare04.dll
// MVID: {60BE1F16-053B-F88E-A745-0383161FBE60}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x04FB0000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed Compare04
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class abstract auto ansi sealed nested public CompareMicroPerfAndCodeGenerationTests
         extends [mscorlib]System.Object
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
    .method public static int32  f4_tuple5() cil managed
    {
      // Code size       208 (0xd0)
      .maxstack  5
      .locals init ([0] int32 x,
               [1] int32 i,
               [2] int32 V_2,
               [3] int32 V_3,
               [4] int32 V_4,
               [5] int32 V_5)
      .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
      .line 5,5 : 8,25 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\Optimizations\\GenericComparison\\Compare04.fsx'
      IL_0000:  ldc.i4.1
      IL_0001:  stloc.0
      .line 8,8 : 8,32 ''
      IL_0002:  ldc.i4.0
      IL_0003:  stloc.1
      IL_0004:  br         IL_00c3

      .line 9,9 : 12,30 ''
      IL_0009:  ldc.i4.1
      IL_000a:  ldc.i4.1
      IL_000b:  cgt
      IL_000d:  stloc.2
      IL_000e:  ldloc.2
      IL_000f:  brfalse.s  IL_0018

      .line 16707566,16707566 : 0,0 ''
      IL_0011:  ldloc.2
      .line 16707566,16707566 : 0,0 ''
      IL_0012:  nop
      IL_0013:  br         IL_00be

      .line 16707566,16707566 : 0,0 ''
      IL_0018:  ldc.i4.2
      IL_0019:  ldc.i4.2
      IL_001a:  cgt
      IL_001c:  stloc.3
      IL_001d:  ldloc.3
      IL_001e:  brfalse.s  IL_0027

      .line 16707566,16707566 : 0,0 ''
      IL_0020:  ldloc.3
      .line 16707566,16707566 : 0,0 ''
      IL_0021:  nop
      IL_0022:  br         IL_00be

      .line 16707566,16707566 : 0,0 ''
      IL_0027:  ldc.i4.4
      IL_0028:  ldc.i4.4
      IL_0029:  cgt
      IL_002b:  stloc.s    V_4
      IL_002d:  ldloc.s    V_4
      IL_002f:  brfalse.s  IL_0039

      .line 16707566,16707566 : 0,0 ''
      IL_0031:  ldloc.s    V_4
      .line 16707566,16707566 : 0,0 ''
      IL_0033:  nop
      IL_0034:  br         IL_00be

      .line 16707566,16707566 : 0,0 ''
      IL_0039:  ldstr      "5"
      IL_003e:  ldstr      "5"
      IL_0043:  call       int32 [netstandard]System.String::CompareOrdinal(string,
                                                                            string)
      IL_0048:  stloc.s    V_5
      IL_004a:  ldloc.s    V_5
      IL_004c:  brfalse.s  IL_0053

      .line 16707566,16707566 : 0,0 ''
      IL_004e:  ldloc.s    V_5
      .line 16707566,16707566 : 0,0 ''
      IL_0050:  nop
      IL_0051:  br.s       IL_00be

      .line 16707566,16707566 : 0,0 ''
      IL_0053:  ldc.r8     6.
      IL_005c:  ldc.r8     7.
      IL_0065:  clt
      IL_0067:  brfalse.s  IL_006d

      .line 16707566,16707566 : 0,0 ''
      IL_0069:  ldc.i4.m1
      .line 16707566,16707566 : 0,0 ''
      IL_006a:  nop
      IL_006b:  br.s       IL_00be

      .line 16707566,16707566 : 0,0 ''
      IL_006d:  ldc.r8     6.
      IL_0076:  ldc.r8     7.
      IL_007f:  cgt
      IL_0081:  brfalse.s  IL_0087

      .line 16707566,16707566 : 0,0 ''
      IL_0083:  ldc.i4.1
      .line 16707566,16707566 : 0,0 ''
      IL_0084:  nop
      IL_0085:  br.s       IL_00be

      .line 16707566,16707566 : 0,0 ''
      IL_0087:  ldc.r8     6.
      IL_0090:  ldc.r8     7.
      IL_0099:  ceq
      IL_009b:  brfalse.s  IL_00a1

      .line 16707566,16707566 : 0,0 ''
      IL_009d:  ldc.i4.0
      .line 16707566,16707566 : 0,0 ''
      IL_009e:  nop
      IL_009f:  br.s       IL_00be

      .line 16707566,16707566 : 0,0 ''
      IL_00a1:  call       class [mscorlib]System.Collections.IComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericComparer()
      IL_00a6:  ldc.r8     6.
      IL_00af:  ldc.r8     7.
      IL_00b8:  call       int32 [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/HashCompare::GenericComparisonWithComparerIntrinsic<float64>(class [mscorlib]System.Collections.IComparer,
                                                                                                                                                    !!0,
                                                                                                                                                    !!0)
      .line 16707566,16707566 : 0,0 ''
      IL_00bd:  nop
      .line 16707566,16707566 : 0,0 ''
      IL_00be:  stloc.0
      IL_00bf:  ldloc.1
      IL_00c0:  ldc.i4.1
      IL_00c1:  add
      IL_00c2:  stloc.1
      .line 8,8 : 8,32 ''
      IL_00c3:  ldloc.1
      IL_00c4:  ldc.i4     0x989681
      IL_00c9:  blt        IL_0009

      .line 10,10 : 8,9 ''
      IL_00ce:  ldloc.0
      IL_00cf:  ret
    } // end of method CompareMicroPerfAndCodeGenerationTests::f4_tuple5

  } // end of class CompareMicroPerfAndCodeGenerationTests

} // end of class Compare04

.class private abstract auto ansi sealed '<StartupCode$Compare04>'.$Compare04$fsx
       extends [mscorlib]System.Object
{
} // end of class '<StartupCode$Compare04>'.$Compare04$fsx


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
