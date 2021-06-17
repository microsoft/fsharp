
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
.assembly SteppingMatch05
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.SteppingMatch05
{
  // Offset: 0x00000000 Length: 0x00000226
}
.mresource public FSharpOptimizationData.SteppingMatch05
{
  // Offset: 0x00000230 Length: 0x0000007B
}
.module SteppingMatch05.dll
// MVID: {60B68B90-30E9-4ADA-A745-0383908BB660}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x072E0000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed SteppingMatch05
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .method public static void  funcC3<a,b,c>(class [FSharp.Core]Microsoft.FSharp.Core.FSharpChoice`3<!!a,!!b,!!c> n) cil managed
  {
    // Code size       75 (0x4b)
    .maxstack  3
    .locals init ([0] class [FSharp.Core]Microsoft.FSharp.Core.FSharpChoice`3<!!a,!!b,!!c> V_0,
             [1] class [FSharp.Core]Microsoft.FSharp.Core.FSharpChoice`3<!!a,!!b,!!c> V_1,
             [2] class [FSharp.Core]Microsoft.FSharp.Core.FSharpChoice`3/Choice3Of3<!!a,!!b,!!c> V_2,
             [3] class [FSharp.Core]Microsoft.FSharp.Core.FSharpChoice`3/Choice2Of3<!!a,!!b,!!c> V_3,
             [4] class [FSharp.Core]Microsoft.FSharp.Core.FSharpChoice`3/Choice1Of3<!!a,!!b,!!c> V_4)
    .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
    .line 5,5 : 9,21 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\SteppingMatch\\SteppingMatch05.fs'
    IL_0000:  ldarg.0
    IL_0001:  stloc.0
    IL_0002:  ldloc.0
    IL_0003:  stloc.1
    IL_0004:  ldloc.1
    IL_0005:  isinst     class [FSharp.Core]Microsoft.FSharp.Core.FSharpChoice`3/Choice2Of3<!!a,!!b,!!c>
    IL_000a:  brtrue.s   IL_0026

    IL_000c:  ldloc.1
    IL_000d:  isinst     class [FSharp.Core]Microsoft.FSharp.Core.FSharpChoice`3/Choice1Of3<!!a,!!b,!!c>
    IL_0012:  brtrue.s   IL_0038

    IL_0014:  ldloc.0
    IL_0015:  castclass  class [FSharp.Core]Microsoft.FSharp.Core.FSharpChoice`3/Choice3Of3<!!a,!!b,!!c>
    IL_001a:  stloc.2
    .line 7,7 : 13,35 ''
    IL_001b:  ldstr      "C"
    IL_0020:  call       void [mscorlib]System.Console::WriteLine(string)
    IL_0025:  ret

    .line 5,5 : 9,21 ''
    IL_0026:  ldloc.0
    IL_0027:  castclass  class [FSharp.Core]Microsoft.FSharp.Core.FSharpChoice`3/Choice2Of3<!!a,!!b,!!c>
    IL_002c:  stloc.3
    .line 9,9 : 13,35 ''
    IL_002d:  ldstr      "B"
    IL_0032:  call       void [mscorlib]System.Console::WriteLine(string)
    IL_0037:  ret

    .line 5,5 : 9,21 ''
    IL_0038:  ldloc.0
    IL_0039:  castclass  class [FSharp.Core]Microsoft.FSharp.Core.FSharpChoice`3/Choice1Of3<!!a,!!b,!!c>
    IL_003e:  stloc.s    V_4
    .line 11,11 : 13,35 ''
    IL_0040:  ldstr      "A"
    IL_0045:  call       void [mscorlib]System.Console::WriteLine(string)
    IL_004a:  ret
  } // end of method SteppingMatch05::funcC3

} // end of class SteppingMatch05

.class private abstract auto ansi sealed '<StartupCode$SteppingMatch05>'.$SteppingMatch05
       extends [mscorlib]System.Object
{
} // end of class '<StartupCode$SteppingMatch05>'.$SteppingMatch05


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
