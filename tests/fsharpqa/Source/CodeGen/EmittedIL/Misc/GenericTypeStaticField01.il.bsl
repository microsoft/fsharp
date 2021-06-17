
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
.assembly GenericTypeStaticField01
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 00 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.GenericTypeStaticField01
{
  // Offset: 0x00000000 Length: 0x00000604
}
.mresource public FSharpOptimizationData.GenericTypeStaticField01
{
  // Offset: 0x00000608 Length: 0x000001E1
}
.module GenericTypeStaticField01.exe
// MVID: {60B68B7F-1E75-7E6B-A745-03837F8BB660}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x00F10000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed GenericTypeStaticField01
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class auto ansi serializable nested public Foo`1<a>
         extends [mscorlib]System.Object
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 03 00 00 00 00 00 ) 
    .field static assembly class GenericTypeStaticField01/Foo`1<!a> theInstance
    .field static assembly int32 init@2
    .method public specialname rtspecialname 
            instance void  .ctor() cil managed
    {
      // Code size       9 (0x9)
      .maxstack  8
      .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
      .line 16707566,16707566 : 0,0 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\Misc\\GenericTypeStaticField01.fs'
      IL_0000:  ldarg.0
      IL_0001:  callvirt   instance void [mscorlib]System.Object::.ctor()
      IL_0006:  ldarg.0
      IL_0007:  pop
      .line 2,2 : 6,9 ''
      IL_0008:  ret
    } // end of method Foo`1::.ctor

    .method private specialname rtspecialname static 
            void  .cctor() cil managed
    {
      // Code size       19 (0x13)
      .maxstack  8
      .line 3,3 : 10,41 ''
      IL_0000:  newobj     instance void class GenericTypeStaticField01/Foo`1<!a>::.ctor()
      IL_0005:  stsfld     class GenericTypeStaticField01/Foo`1<!0> class GenericTypeStaticField01/Foo`1<!a>::theInstance
      IL_000a:  ldc.i4.1
      IL_000b:  volatile.
      IL_000d:  stsfld     int32 class GenericTypeStaticField01/Foo`1<!a>::init@2
      .line 2,2 : 6,9 ''
      IL_0012:  ret
    } // end of method Foo`1::.cctor

    .method public specialname static class GenericTypeStaticField01/Foo`1<!a> 
            get_Instance() cil managed
    {
      // Code size       26 (0x1a)
      .maxstack  8
      .line 16707566,16707566 : 0,0 ''
      IL_0000:  volatile.
      IL_0002:  ldsfld     int32 class GenericTypeStaticField01/Foo`1<!a>::init@2
      IL_0007:  ldc.i4.1
      IL_0008:  bge.s      IL_0013

      .line 16707566,16707566 : 0,0 ''
      IL_000a:  call       void [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/IntrinsicFunctions::FailStaticInit()
      IL_000f:  nop
      .line 16707566,16707566 : 0,0 ''
      IL_0010:  nop
      IL_0011:  br.s       IL_0014

      .line 16707566,16707566 : 0,0 ''
      IL_0013:  nop
      .line 4,4 : 28,39 ''
      IL_0014:  ldsfld     class GenericTypeStaticField01/Foo`1<!0> class GenericTypeStaticField01/Foo`1<!a>::theInstance
      IL_0019:  ret
    } // end of method Foo`1::get_Instance

    .property class GenericTypeStaticField01/Foo`1<!a>
            Instance()
    {
      .get class GenericTypeStaticField01/Foo`1<!a> GenericTypeStaticField01/Foo`1::get_Instance()
    } // end of property Foo`1::Instance
  } // end of class Foo`1

  .class auto ansi serializable nested public Bar`2<a,b>
         extends [mscorlib]System.Object
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 03 00 00 00 00 00 ) 
    .field static assembly class GenericTypeStaticField01/Bar`2<!a,!b> theInstance
    .field static assembly int32 'init@6-1'
    .method public specialname rtspecialname 
            instance void  .ctor() cil managed
    {
      // Code size       9 (0x9)
      .maxstack  8
      .line 16707566,16707566 : 0,0 ''
      IL_0000:  ldarg.0
      IL_0001:  callvirt   instance void [mscorlib]System.Object::.ctor()
      IL_0006:  ldarg.0
      IL_0007:  pop
      .line 6,6 : 6,9 ''
      IL_0008:  ret
    } // end of method Bar`2::.ctor

    .method private specialname rtspecialname static 
            void  .cctor() cil managed
    {
      // Code size       19 (0x13)
      .maxstack  8
      .line 7,7 : 10,44 ''
      IL_0000:  newobj     instance void class GenericTypeStaticField01/Bar`2<!a,!b>::.ctor()
      IL_0005:  stsfld     class GenericTypeStaticField01/Bar`2<!0,!1> class GenericTypeStaticField01/Bar`2<!a,!b>::theInstance
      IL_000a:  ldc.i4.1
      IL_000b:  volatile.
      IL_000d:  stsfld     int32 class GenericTypeStaticField01/Bar`2<!a,!b>::'init@6-1'
      .line 6,6 : 6,9 ''
      IL_0012:  ret
    } // end of method Bar`2::.cctor

    .method public specialname static class GenericTypeStaticField01/Bar`2<!a,!b> 
            get_Instance() cil managed
    {
      // Code size       26 (0x1a)
      .maxstack  8
      .line 16707566,16707566 : 0,0 ''
      IL_0000:  volatile.
      IL_0002:  ldsfld     int32 class GenericTypeStaticField01/Bar`2<!a,!b>::'init@6-1'
      IL_0007:  ldc.i4.1
      IL_0008:  bge.s      IL_0013

      .line 16707566,16707566 : 0,0 ''
      IL_000a:  call       void [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/IntrinsicFunctions::FailStaticInit()
      IL_000f:  nop
      .line 16707566,16707566 : 0,0 ''
      IL_0010:  nop
      IL_0011:  br.s       IL_0014

      .line 16707566,16707566 : 0,0 ''
      IL_0013:  nop
      .line 8,8 : 28,39 ''
      IL_0014:  ldsfld     class GenericTypeStaticField01/Bar`2<!0,!1> class GenericTypeStaticField01/Bar`2<!a,!b>::theInstance
      IL_0019:  ret
    } // end of method Bar`2::get_Instance

    .property class GenericTypeStaticField01/Bar`2<!a,!b>
            Instance()
    {
      .get class GenericTypeStaticField01/Bar`2<!a,!b> GenericTypeStaticField01/Bar`2::get_Instance()
    } // end of property Bar`2::Instance
  } // end of class Bar`2

} // end of class GenericTypeStaticField01

.class private abstract auto ansi sealed '<StartupCode$GenericTypeStaticField01>'.$GenericTypeStaticField01
       extends [mscorlib]System.Object
{
  .method public static void  main@() cil managed
  {
    .entrypoint
    // Code size       1 (0x1)
    .maxstack  8
    IL_0000:  ret
  } // end of method $GenericTypeStaticField01::main@

} // end of class '<StartupCode$GenericTypeStaticField01>'.$GenericTypeStaticField01


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
