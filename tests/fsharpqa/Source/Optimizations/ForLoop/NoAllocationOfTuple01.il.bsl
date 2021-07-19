
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
.assembly NoAllocationOfTuple01
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 00 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.NoAllocationOfTuple01
{
  // Offset: 0x00000000 Length: 0x000001DC
}
.mresource public FSharpOptimizationData.NoAllocationOfTuple01
{
  // Offset: 0x000001E0 Length: 0x00000085
}
.module NoAllocationOfTuple01.dll
// MVID: {60BE1F17-13B5-F699-A745-0383171FBE60}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x08DC0000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed NoAllocationOfTuple01
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .method public static int32[]  loop(int32 n) cil managed
  {
    // Code size       41 (0x29)
    .maxstack  5
    .locals init ([0] int32[] a,
             [1] int32 i,
             [2] int32 V_2,
             [3] int32 j)
    .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
    .line 6,6 : 5,31 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\Optimizations\\ForLoop\\NoAllocationOfTuple01.fs'
    IL_0000:  ldarg.0
    IL_0001:  call       !!0[] [FSharp.Core]Microsoft.FSharp.Collections.ArrayModule::ZeroCreate<int32>(int32)
    IL_0006:  stloc.0
    .line 7,7 : 5,23 ''
    IL_0007:  ldc.i4.m1
    IL_0008:  stloc.1
    .line 8,8 : 5,22 ''
    IL_0009:  ldc.i4.1
    IL_000a:  stloc.3
    IL_000b:  ldarg.0
    IL_000c:  stloc.2
    IL_000d:  ldloc.2
    IL_000e:  ldloc.3
    IL_000f:  blt.s      IL_0027

    .line 9,9 : 7,17 ''
    IL_0011:  ldloc.1
    IL_0012:  ldc.i4.1
    IL_0013:  add
    IL_0014:  stloc.1
    .line 10,10 : 7,19 ''
    IL_0015:  ldloc.0
    IL_0016:  ldloc.1
    IL_0017:  ldloc.3
    IL_0018:  stelem     [mscorlib]System.Int32
    IL_001d:  ldloc.3
    IL_001e:  ldc.i4.1
    IL_001f:  add
    IL_0020:  stloc.3
    .line 8,8 : 5,22 ''
    IL_0021:  ldloc.3
    IL_0022:  ldloc.2
    IL_0023:  ldc.i4.1
    IL_0024:  add
    IL_0025:  bne.un.s   IL_0011

    .line 11,11 : 5,6 ''
    IL_0027:  ldloc.0
    IL_0028:  ret
  } // end of method NoAllocationOfTuple01::loop

} // end of class NoAllocationOfTuple01

.class private abstract auto ansi sealed '<StartupCode$NoAllocationOfTuple01>'.$NoAllocationOfTuple01
       extends [mscorlib]System.Object
{
} // end of class '<StartupCode$NoAllocationOfTuple01>'.$NoAllocationOfTuple01


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
