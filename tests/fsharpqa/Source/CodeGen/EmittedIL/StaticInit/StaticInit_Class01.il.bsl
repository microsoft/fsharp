
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
.assembly StaticInit_Class01
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.StaticInit_Class01
{
  // Offset: 0x00000000 Length: 0x0000032F
}
.mresource public FSharpOptimizationData.StaticInit_Class01
{
  // Offset: 0x00000338 Length: 0x000000AD
}
.module StaticInit_Class01.dll
// MVID: {60B68B90-EC34-E66E-A745-0383908BB660}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x06F70000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed StaticInit_ClassS01
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class auto ansi serializable nested public C
         extends [mscorlib]System.Object
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 03 00 00 00 00 00 ) 
    .field static assembly int32 x
    .field static assembly int32 init@4
    .method public specialname rtspecialname 
            instance void  .ctor(valuetype [mscorlib]System.DateTime s) cil managed
    {
      // Code size       9 (0x9)
      .maxstack  8
      .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
      .line 100001,100001 : 0,0 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\StaticInit\\StaticInit_Class01.fs'
      IL_0000:  ldarg.0
      IL_0001:  callvirt   instance void [mscorlib]System.Object::.ctor()
      IL_0006:  ldarg.0
      IL_0007:  pop
      .line 4,4 : 6,7 ''
      IL_0008:  ret
    } // end of method C::.ctor

    .method assembly static int32  f() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       37 (0x25)
      .maxstack  8
      .line 7,7 : 23,37 ''
      IL_0000:  volatile.
      IL_0002:  ldsfld     int32 StaticInit_ClassS01/C::init@4
      IL_0007:  ldc.i4.1
      IL_0008:  bge.s      IL_0013

      .line 100001,100001 : 0,0 ''
      IL_000a:  call       void [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/IntrinsicFunctions::FailStaticInit()
      IL_000f:  nop
      .line 100001,100001 : 0,0 ''
      IL_0010:  nop
      IL_0011:  br.s       IL_0014

      .line 100001,100001 : 0,0 ''
      IL_0013:  nop
      IL_0014:  ldsfld     int32 StaticInit_ClassS01/C::x
      IL_0019:  ldstr      "2"
      IL_001e:  callvirt   instance int32 [mscorlib]System.String::get_Length()
      IL_0023:  add
      IL_0024:  ret
    } // end of method C::f

    .method private specialname rtspecialname static 
            void  .cctor() cil managed
    {
      // Code size       13 (0xd)
      .maxstack  8
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldc.i4.0
      IL_0001:  stsfld     int32 '<StartupCode$StaticInit_Class01>'.$StaticInit_ClassS01::init@
      IL_0006:  ldsfld     int32 '<StartupCode$StaticInit_Class01>'.$StaticInit_ClassS01::init@
      IL_000b:  pop
      IL_000c:  ret
    } // end of method C::.cctor

  } // end of class C

} // end of class StaticInit_ClassS01

.class private abstract auto ansi sealed '<StartupCode$StaticInit_Class01>'.$StaticInit_ClassS01
       extends [mscorlib]System.Object
{
  .field static assembly int32 init@
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
  .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
  .method private specialname rtspecialname static 
          void  .cctor() cil managed
  {
    // Code size       24 (0x18)
    .maxstack  8
    .line 6,6 : 12,30 ''
    IL_0000:  ldstr      "1"
    IL_0005:  callvirt   instance int32 [mscorlib]System.String::get_Length()
    IL_000a:  stsfld     int32 StaticInit_ClassS01/C::x
    IL_000f:  ldc.i4.1
    IL_0010:  volatile.
    IL_0012:  stsfld     int32 StaticInit_ClassS01/C::init@4
    .line 4,4 : 6,7 ''
    IL_0017:  ret
  } // end of method $StaticInit_ClassS01::.cctor

} // end of class '<StartupCode$StaticInit_Class01>'.$StaticInit_ClassS01


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
