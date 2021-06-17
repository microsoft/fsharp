
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
.assembly SteppingMatch08
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.SteppingMatch08
{
  // Offset: 0x00000000 Length: 0x000001DB
}
.mresource public FSharpOptimizationData.SteppingMatch08
{
  // Offset: 0x000001E0 Length: 0x00000079
}
.module SteppingMatch08.dll
// MVID: {60B68B90-F238-BA3A-A745-0383908BB660}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x00E20000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed SteppingMatch08
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .method public static void  test(int32 x) cil managed
  {
    // Code size       20 (0x14)
    .maxstack  3
    .locals init ([0] int32 b)
    .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
    .line 5,5 : 9,21 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\SteppingMatch\\SteppingMatch08.fs'
    IL_0000:  ldarg.0
    IL_0001:  switch     ( 
                          IL_000c)
    IL_000a:  br.s       IL_0010

    .line 6,6 : 16,17 ''
    IL_000c:  ldc.i4.2
    .line 100001,100001 : 0,0 ''
    IL_000d:  nop
    IL_000e:  br.s       IL_0012

    .line 7,7 : 18,19 ''
    IL_0010:  ldc.i4.0
    .line 100001,100001 : 0,0 ''
    IL_0011:  nop
    .line 100001,100001 : 0,0 ''
    IL_0012:  stloc.0
    .line 10,10 : 5,38 ''
    IL_0013:  ret
  } // end of method SteppingMatch08::test

} // end of class SteppingMatch08

.class private abstract auto ansi sealed '<StartupCode$SteppingMatch08>'.$SteppingMatch08
       extends [mscorlib]System.Object
{
} // end of class '<StartupCode$SteppingMatch08>'.$SteppingMatch08


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
