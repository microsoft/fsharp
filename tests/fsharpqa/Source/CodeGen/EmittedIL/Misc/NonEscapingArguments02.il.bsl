
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
.assembly NonEscapingArguments02
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.NonEscapingArguments02
{
  // Offset: 0x00000000 Length: 0x00000355
}
.mresource public FSharpOptimizationData.NonEscapingArguments02
{
  // Offset: 0x00000360 Length: 0x0000019E
}
.module NonEscapingArguments02.dll
// MVID: {60B68B7F-BB56-6582-A745-03837F8BB660}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x06B40000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed NonEscapingArguments02
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class auto ansi serializable nested public beforefieldinit ListSizeCounter`1<t>
         extends [mscorlib]System.Object
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 03 00 00 00 00 00 ) 
    .field assembly int32 size
    .method public specialname rtspecialname 
            instance void  .ctor(class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!t> somelist) cil managed
    {
      // Code size       21 (0x15)
      .maxstack  8
      .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
      .line 100001,100001 : 0,0 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\Misc\\NonEscapingArguments02.fs'
      IL_0000:  ldarg.0
      IL_0001:  callvirt   instance void [mscorlib]System.Object::.ctor()
      IL_0006:  ldarg.0
      IL_0007:  pop
      .line 6,6 : 5,36 ''
      IL_0008:  ldarg.0
      IL_0009:  ldarg.1
      IL_000a:  call       int32 [FSharp.Core]Microsoft.FSharp.Collections.ListModule::Length<!t>(class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!!0>)
      IL_000f:  stfld      int32 class NonEscapingArguments02/ListSizeCounter`1<!t>::size
      .line 5,5 : 6,21 ''
      IL_0014:  ret
    } // end of method ListSizeCounter`1::.ctor

    .method public hidebysig specialname 
            instance int32  get_Size() cil managed
    {
      // Code size       7 (0x7)
      .maxstack  8
      .line 7,7 : 24,28 ''
      IL_0000:  ldarg.0
      IL_0001:  ldfld      int32 class NonEscapingArguments02/ListSizeCounter`1<!t>::size
      IL_0006:  ret
    } // end of method ListSizeCounter`1::get_Size

    .property instance int32 Size()
    {
      .get instance int32 NonEscapingArguments02/ListSizeCounter`1::get_Size()
    } // end of property ListSizeCounter`1::Size
  } // end of class ListSizeCounter`1

} // end of class NonEscapingArguments02

.class private abstract auto ansi sealed '<StartupCode$NonEscapingArguments02>'.$NonEscapingArguments02
       extends [mscorlib]System.Object
{
} // end of class '<StartupCode$NonEscapingArguments02>'.$NonEscapingArguments02


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
