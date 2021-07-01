
//  Microsoft (R) .NET Framework IL Disassembler.  Version 4.5.22220.0



// Metadata version: v4.0.30319
.assembly extern mscorlib
{
  .publickeytoken = (B7 7A 5C 56 19 34 E0 89 )                         // .z\V.4..
  .ver 4:0:0:0
}
.assembly extern FSharp.Core
{
  .publickeytoken = (B0 3F 5F 7F 11 D5 0A 3A )                         // .?_....:
  .ver 4:4:3:0
}
.assembly test
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.test
{
  // Offset: 0x00000000 Length: 0x00000274
  // WARNING: managed resource file FSharpSignatureData.test created
}
.mresource public FSharpSignatureDataB.test
{
  // Offset: 0x00000000 Length: 0x00000274
  // WARNING: managed resource file FSharpSignatureDataB.test created
}
.mresource public FSharpOptimizationData.test
{
  // Offset: 0x00000278 Length: 0x0000006F
  // WARNING: managed resource file FSharpOptimizationData.test created
}
.mresource public FSharpOptimizationDataB.test
{
  // Offset: 0x00000278 Length: 0x0000006F
  // WARNING: managed resource file FSharpOptimizationData.test created
}
.module test.exe
 // MVID: {5AA663EB-D9C1-2E4E-A745-0383EB63A65A}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x02BF0000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed Test
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .field public static literal int32 x = int32(0x00000007)
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.LiteralAttribute::.ctor() = ( 01 00 00 00 ) 
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .method public static int32  main(string[] argv) cil managed
  {
    .entrypoint
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.EntryPointAttribute::.ctor() = ( 01 00 00 00 ) 
    // Code size       2 (0x2)
    .maxstack  8
    IL_0000:  ldc.i4.0
    IL_0001:  ret
  } // end of method Test::main

} // end of class Test

.class private abstract auto ansi sealed '<StartupCode$test>'.$Test
       extends [mscorlib]System.Object
{
} // end of class '<StartupCode$test>'.$Test


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
// WARNING: Created Win32 resource file test.res
