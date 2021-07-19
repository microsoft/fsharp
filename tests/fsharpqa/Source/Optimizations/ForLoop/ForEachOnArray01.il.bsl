
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
.assembly ForEachOnArray01
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 00 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.ForEachOnArray01
{
  // Offset: 0x00000000 Length: 0x000001DC
}
.mresource public FSharpOptimizationData.ForEachOnArray01
{
  // Offset: 0x000001E0 Length: 0x0000007C
}
.module ForEachOnArray01.dll
// MVID: {60BE1F17-7E2E-D3AE-A745-0383171FBE60}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x06F50000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed ForEachOnArray01
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .method public static void  test3(int32[] arr) cil managed
  {
    // Code size       29 (0x1d)
    .maxstack  4
    .locals init ([0] int32 z,
             [1] int32 V_1,
             [2] int32 x)
    .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
    .line 6,6 : 6,23 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\Optimizations\\ForLoop\\ForEachOnArray01.fs'
    IL_0000:  ldc.i4.0
    IL_0001:  stloc.0
    .line 7,7 : 6,21 ''
    IL_0002:  ldc.i4.0
    IL_0003:  stloc.1
    IL_0004:  br.s       IL_0016

    .line 7,7 : 6,21 ''
    IL_0006:  ldarg.0
    IL_0007:  ldloc.1
    IL_0008:  ldelem     [mscorlib]System.Int32
    IL_000d:  stloc.2
    IL_000e:  ldloc.0
    IL_000f:  ldloc.2
    IL_0010:  add
    IL_0011:  stloc.0
    IL_0012:  ldloc.1
    IL_0013:  ldc.i4.1
    IL_0014:  add
    IL_0015:  stloc.1
    .line 7,7 : 6,21 ''
    IL_0016:  ldloc.1
    IL_0017:  ldarg.0
    IL_0018:  ldlen
    IL_0019:  conv.i4
    IL_001a:  blt.s      IL_0006

    IL_001c:  ret
  } // end of method ForEachOnArray01::test3

} // end of class ForEachOnArray01

.class private abstract auto ansi sealed '<StartupCode$ForEachOnArray01>'.$ForEachOnArray01
       extends [mscorlib]System.Object
{
} // end of class '<StartupCode$ForEachOnArray01>'.$ForEachOnArray01


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
