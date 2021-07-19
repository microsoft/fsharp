
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
.assembly ZeroToArrLength02
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 00 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.ZeroToArrLength02
{
  // Offset: 0x00000000 Length: 0x000001DC
}
.mresource public FSharpOptimizationData.ZeroToArrLength02
{
  // Offset: 0x000001E0 Length: 0x0000007B
}
.module ZeroToArrLength02.dll
// MVID: {60BE1F17-A36B-03A7-A745-0383171FBE60}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x05860000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed ZeroToArrLength02
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .method public static void  f1(int32[] arr) cil managed
  {
    // Code size       34 (0x22)
    .maxstack  5
    .locals init ([0] int32 V_0,
             [1] int32 i)
    .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
    .line 6,6 : 5,41 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\Optimizations\\ForLoop\\ZeroToArrLength02.fs'
    IL_0000:  ldc.i4.0
    IL_0001:  stloc.1
    IL_0002:  ldarg.0
    IL_0003:  call       int32 [FSharp.Core]Microsoft.FSharp.Collections.ArrayModule::Length<int32>(!!0[])
    IL_0008:  ldc.i4.1
    IL_0009:  sub
    IL_000a:  stloc.0
    IL_000b:  ldloc.0
    IL_000c:  ldloc.1
    IL_000d:  blt.s      IL_0021

    .line 7,7 : 9,21 ''
    IL_000f:  ldarg.0
    IL_0010:  ldloc.1
    IL_0011:  ldloc.1
    IL_0012:  stelem     [mscorlib]System.Int32
    IL_0017:  ldloc.1
    IL_0018:  ldc.i4.1
    IL_0019:  add
    IL_001a:  stloc.1
    .line 6,6 : 5,41 ''
    IL_001b:  ldloc.1
    IL_001c:  ldloc.0
    IL_001d:  ldc.i4.1
    IL_001e:  add
    IL_001f:  bne.un.s   IL_000f

    IL_0021:  ret
  } // end of method ZeroToArrLength02::f1

} // end of class ZeroToArrLength02

.class private abstract auto ansi sealed '<StartupCode$ZeroToArrLength02>'.$ZeroToArrLength02
       extends [mscorlib]System.Object
{
} // end of class '<StartupCode$ZeroToArrLength02>'.$ZeroToArrLength02


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
