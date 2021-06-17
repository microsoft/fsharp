
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
.assembly TupleMonster
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.TupleMonster
{
  // Offset: 0x00000000 Length: 0x00000145
}
.mresource public FSharpOptimizationData.TupleMonster
{
  // Offset: 0x00000150 Length: 0x00000053
}
.module TupleMonster.exe
// MVID: {60B68B97-1552-41D8-A745-0383978BB660}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x07110000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed TupleMonster
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
} // end of class TupleMonster

.class private abstract auto ansi sealed '<StartupCode$TupleMonster>'.$TupleMonster
       extends [mscorlib]System.Object
{
  .field static assembly int32 init@
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
  .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
  .method public static void  main@() cil managed
  {
    .entrypoint
    // Code size       74 (0x4a)
    .maxstack  28
    .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
    .line 3,3 : 9,137 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\Tuples\\TupleMonster.fs'
    IL_0000:  ldc.i4.s   97
    IL_0002:  ldc.i4.s   98
    IL_0004:  ldc.i4.s   99
    IL_0006:  ldc.i4.s   100
    IL_0008:  ldc.i4.s   101
    IL_000a:  ldc.i4.s   102
    IL_000c:  ldc.i4.s   103
    IL_000e:  ldc.i4.s   104
    IL_0010:  ldc.i4.s   105
    IL_0012:  ldc.i4.s   106
    IL_0014:  ldc.i4.s   107
    IL_0016:  ldc.i4.s   108
    IL_0018:  ldc.i4.s   109
    IL_001a:  ldc.i4.s   110
    IL_001c:  ldc.i4.s   111
    IL_001e:  ldc.i4.s   112
    IL_0020:  ldc.i4.s   113
    IL_0022:  ldc.i4.s   114
    IL_0024:  ldc.i4.s   115
    IL_0026:  ldc.i4.s   116
    IL_0028:  ldc.i4.s   117
    IL_002a:  ldc.i4.s   118
    IL_002c:  ldc.i4.s   119
    IL_002e:  ldc.i4.s   120
    IL_0030:  ldc.i4.s   121
    IL_0032:  ldc.i4.s   122
    IL_0034:  newobj     instance void class [mscorlib]System.Tuple`5<char,char,char,char,char>::.ctor(!0,
                                                                                                       !1,
                                                                                                       !2,
                                                                                                       !3,
                                                                                                       !4)
    IL_0039:  newobj     instance void class [mscorlib]System.Tuple`8<char,char,char,char,char,char,char,class [mscorlib]System.Tuple`5<char,char,char,char,char>>::.ctor(!0,
                                                                                                                                                                          !1,
                                                                                                                                                                          !2,
                                                                                                                                                                          !3,
                                                                                                                                                                          !4,
                                                                                                                                                                          !5,
                                                                                                                                                                          !6,
                                                                                                                                                                          !7)
    IL_003e:  newobj     instance void class [mscorlib]System.Tuple`8<char,char,char,char,char,char,char,class [mscorlib]System.Tuple`8<char,char,char,char,char,char,char,class [mscorlib]System.Tuple`5<char,char,char,char,char>>>::.ctor(!0,
                                                                                                                                                                                                                                             !1,
                                                                                                                                                                                                                                             !2,
                                                                                                                                                                                                                                             !3,
                                                                                                                                                                                                                                             !4,
                                                                                                                                                                                                                                             !5,
                                                                                                                                                                                                                                             !6,
                                                                                                                                                                                                                                             !7)
    IL_0043:  newobj     instance void class [mscorlib]System.Tuple`8<char,char,char,char,char,char,char,class [mscorlib]System.Tuple`8<char,char,char,char,char,char,char,class [mscorlib]System.Tuple`8<char,char,char,char,char,char,char,class [mscorlib]System.Tuple`5<char,char,char,char,char>>>>::.ctor(!0,
                                                                                                                                                                                                                                                                                                                !1,
                                                                                                                                                                                                                                                                                                                !2,
                                                                                                                                                                                                                                                                                                                !3,
                                                                                                                                                                                                                                                                                                                !4,
                                                                                                                                                                                                                                                                                                                !5,
                                                                                                                                                                                                                                                                                                                !6,
                                                                                                                                                                                                                                                                                                                !7)
    IL_0048:  pop
    IL_0049:  ret
  } // end of method $TupleMonster::main@

} // end of class '<StartupCode$TupleMonster>'.$TupleMonster


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
