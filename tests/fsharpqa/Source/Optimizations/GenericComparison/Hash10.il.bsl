
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
.assembly Hash10
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 00 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.Hash10
{
  // Offset: 0x00000000 Length: 0x00000215
}
.mresource public FSharpOptimizationData.Hash10
{
  // Offset: 0x00000220 Length: 0x000000A9
}
.module Hash10.dll
// MVID: {60BE1F16-9661-78B4-A745-0383161FBE60}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x00BB0000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed Hash10
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class abstract auto ansi sealed nested public HashMicroPerfAndCodeGenerationTests
         extends [mscorlib]System.Object
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
    .method public static void  f7() cil managed
    {
      // Code size       44 (0x2c)
      .maxstack  5
      .locals init ([0] uint8[] arr,
               [1] int32 i,
               [2] int32 V_2)
      .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
      .line 6,6 : 8,36 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\Optimizations\\GenericComparison\\Hash10.fsx'
      IL_0000:  ldc.i4.0
      IL_0001:  ldc.i4.1
      IL_0002:  ldc.i4.s   100
      IL_0004:  call       class [mscorlib]System.Collections.Generic.IEnumerable`1<uint8> [FSharp.Core]Microsoft.FSharp.Core.Operators/OperatorIntrinsics::RangeByte(uint8,
                                                                                                                                                                      uint8,
                                                                                                                                                                      uint8)
      IL_0009:  call       class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0> [FSharp.Core]Microsoft.FSharp.Core.Operators::CreateSequence<uint8>(class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0>)
      IL_000e:  call       !!0[] [FSharp.Core]Microsoft.FSharp.Collections.SeqModule::ToArray<uint8>(class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0>)
      IL_0013:  stloc.0
      .line 7,7 : 8,32 ''
      IL_0014:  ldc.i4.0
      IL_0015:  stloc.1
      IL_0016:  br.s       IL_0023

      .line 8,8 : 12,30 ''
      IL_0018:  ldloc.0
      IL_0019:  call       int32 [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/HashCompare::GenericHashIntrinsic<uint8[]>(!!0)
      IL_001e:  stloc.2
      IL_001f:  ldloc.1
      IL_0020:  ldc.i4.1
      IL_0021:  add
      IL_0022:  stloc.1
      .line 7,7 : 8,32 ''
      IL_0023:  ldloc.1
      IL_0024:  ldc.i4     0x989681
      IL_0029:  blt.s      IL_0018

      IL_002b:  ret
    } // end of method HashMicroPerfAndCodeGenerationTests::f7

  } // end of class HashMicroPerfAndCodeGenerationTests

} // end of class Hash10

.class private abstract auto ansi sealed '<StartupCode$Hash10>'.$Hash10$fsx
       extends [mscorlib]System.Object
{
} // end of class '<StartupCode$Hash10>'.$Hash10$fsx


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
