
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
.assembly Match02
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 
  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.Match02
{
  // Offset: 0x00000000 Length: 0x00000484
}
.mresource public FSharpOptimizationData.Match02
{
  // Offset: 0x00000488 Length: 0x000002EE
}
.module Match02.dll
// MVID: {60BE1F16-6125-4D81-A745-0383161FBE60}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x06770000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed Match02
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class sequential ansi serializable sealed nested public S
         extends [mscorlib]System.ValueType
  {
    .pack 0
    .size 1
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.StructAttribute::.ctor() = ( 01 00 00 00 ) 
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.NoEqualityAttribute::.ctor() = ( 01 00 00 00 ) 
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.NoComparisonAttribute::.ctor() = ( 01 00 00 00 ) 
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 03 00 00 00 00 00 ) 
    .method public specialname static int32 
            op_Addition<a>(!!a _arg1,
                           valuetype Match02/S _arg2) cil managed
    {
      // Code size       2 (0x2)
      .maxstack  8
      IL_0000:  ldc.i4.0
      IL_0001:  ret
    } // end of method S::op_Addition

    .method public specialname static int32 
            op_Multiply<a>(!!a _arg3,
                           valuetype Match02/S _arg4) cil managed
    {
      // Code size       2 (0x2)
      .maxstack  8
      IL_0000:  ldc.i4.1
      IL_0001:  ret
    } // end of method S::op_Multiply

    .method public specialname static int32 
            op_Addition<a>(valuetype Match02/S _arg5,
                           !!a _arg6) cil managed
    {
      // Code size       2 (0x2)
      .maxstack  8
      IL_0000:  ldc.i4.2
      IL_0001:  ret
    } // end of method S::op_Addition

    .method public specialname static int32 
            op_Multiply<a>(valuetype Match02/S _arg7,
                           !!a _arg8) cil managed
    {
      // Code size       2 (0x2)
      .maxstack  8
      IL_0000:  ldc.i4.3
      IL_0001:  ret
    } // end of method S::op_Multiply

  } // end of class S

  .method public static int32  testmethod() cil managed
  {
    // Code size       3 (0x3)
    .maxstack  8
    IL_0000:  ldc.i4.s   12
    IL_0002:  ret
  } // end of method Match02::testmethod

} // end of class Match02

.class private abstract auto ansi sealed '<StartupCode$Match02>'.$Match02
       extends [mscorlib]System.Object
{
} // end of class '<StartupCode$Match02>'.$Match02


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
