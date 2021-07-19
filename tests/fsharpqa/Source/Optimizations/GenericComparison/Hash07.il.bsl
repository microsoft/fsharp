
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
.assembly Hash07
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 00 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.Hash07
{
  // Offset: 0x00000000 Length: 0x00000216
}
.mresource public FSharpOptimizationData.Hash07
{
  // Offset: 0x00000220 Length: 0x000000AA
}
.module Hash07.dll
// MVID: {60BE1F16-9642-7811-A745-0383161FBE60}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x06740000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed Hash07
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class abstract auto ansi sealed nested public HashMicroPerfAndCodeGenerationTests
         extends [mscorlib]System.Object
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
    .method public static void  f5b() cil managed
    {
      // Code size       25 (0x19)
      .maxstack  5
      .locals init ([0] int32 x,
               [1] int32 i)
      .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
      .line 5,5 : 8,25 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\Optimizations\\GenericComparison\\Hash07.fsx'
      IL_0000:  ldc.i4.1
      IL_0001:  stloc.0
      .line 6,6 : 8,32 ''
      IL_0002:  ldc.i4.0
      IL_0003:  stloc.1
      IL_0004:  br.s       IL_0010

      .line 7,7 : 12,36 ''
      IL_0006:  ldloc.1
      IL_0007:  ldloc.1
      IL_0008:  ldc.i4.1
      IL_0009:  add
      IL_000a:  add
      IL_000b:  stloc.0
      IL_000c:  ldloc.1
      IL_000d:  ldc.i4.1
      IL_000e:  add
      IL_000f:  stloc.1
      .line 6,6 : 8,32 ''
      IL_0010:  ldloc.1
      IL_0011:  ldc.i4     0x989681
      IL_0016:  blt.s      IL_0006

      IL_0018:  ret
    } // end of method HashMicroPerfAndCodeGenerationTests::f5b

  } // end of class HashMicroPerfAndCodeGenerationTests

} // end of class Hash07

.class private abstract auto ansi sealed '<StartupCode$Hash07>'.$Hash07$fsx
       extends [mscorlib]System.Object
{
} // end of class '<StartupCode$Hash07>'.$Hash07$fsx


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
