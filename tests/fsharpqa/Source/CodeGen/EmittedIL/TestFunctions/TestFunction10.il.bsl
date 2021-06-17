
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
.assembly TestFunction10
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.TestFunction10
{
  // Offset: 0x00000000 Length: 0x000001C5
}
.mresource public FSharpOptimizationData.TestFunction10
{
  // Offset: 0x000001D0 Length: 0x00000072
}
.module TestFunction10.exe
// MVID: {60B68B97-A624-44FB-A745-0383978BB660}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x066D0000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed TestFunction10
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .method public static int32  TestFunction10(int32 p_0,
                                              int32 p_1) cil managed
  {
    // Code size       28 (0x1c)
    .maxstack  4
    .locals init ([0] class [mscorlib]System.Tuple`2<int32,int32> p,
             [1] class [mscorlib]System.Tuple`2<int32,int32> V_1,
             [2] int32 y,
             [3] int32 x)
    .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
    .line 100001,100001 : 0,0 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\TestFunctions\\TestFunction10.fs'
    IL_0000:  ldarg.0
    IL_0001:  ldarg.1
    IL_0002:  newobj     instance void class [mscorlib]System.Tuple`2<int32,int32>::.ctor(!0,
                                                                                          !1)
    IL_0007:  stloc.0
    .line 5,5 : 5,18 ''
    IL_0008:  ldloc.0
    IL_0009:  stloc.1
    IL_000a:  ldloc.1
    IL_000b:  call       instance !1 class [mscorlib]System.Tuple`2<int32,int32>::get_Item2()
    IL_0010:  stloc.2
    IL_0011:  ldloc.1
    IL_0012:  call       instance !0 class [mscorlib]System.Tuple`2<int32,int32>::get_Item1()
    IL_0017:  stloc.3
    .line 6,6 : 5,8 ''
    IL_0018:  ldloc.3
    IL_0019:  ldloc.2
    IL_001a:  add
    IL_001b:  ret
  } // end of method TestFunction10::TestFunction10

} // end of class TestFunction10

.class private abstract auto ansi sealed '<StartupCode$TestFunction10>'.$TestFunction10
       extends [mscorlib]System.Object
{
  .method public static void  main@() cil managed
  {
    .entrypoint
    // Code size       1 (0x1)
    .maxstack  8
    IL_0000:  ret
  } // end of method $TestFunction10::main@

} // end of class '<StartupCode$TestFunction10>'.$TestFunction10


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
