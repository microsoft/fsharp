
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
.assembly PreserveSig
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.PreserveSig
{
  // Offset: 0x00000000 Length: 0x000002F1
}
.mresource public FSharpOptimizationData.PreserveSig
{
  // Offset: 0x000002F8 Length: 0x0000004A
}
.module PreserveSig.dll
// MVID: {60B68B7F-E8CC-64FE-A745-03837F8BB660}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x05120000


// =============== CLASS MEMBERS DECLARATION ===================

.class interface public abstract auto ansi serializable Foo.Bar
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 03 00 00 00 00 00 ) 
  .method public hidebysig abstract virtual 
          instance int32  MyCall() cil managed preservesig
  {
  } // end of method Bar::MyCall

} // end of class Foo.Bar

.class private abstract auto ansi sealed '<StartupCode$PreserveSig>'.$PreserveSig
       extends [mscorlib]System.Object
{
} // end of class '<StartupCode$PreserveSig>'.$PreserveSig


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
