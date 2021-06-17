
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
.assembly EntryPoint01
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.EntryPoint01
{
  // Offset: 0x00000000 Length: 0x0000024F
}
.mresource public FSharpOptimizationData.EntryPoint01
{
  // Offset: 0x00000258 Length: 0x00000090
}
.module EntryPoint01.exe
// MVID: {60B68B7F-9846-72C1-A745-03837F8BB660}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x071C0000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed EntryPoint01
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .method public specialname static int32 
          get_static_initializer() cil managed
  {
    .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
    .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
    // Code size       3 (0x3)
    .maxstack  8
    IL_0000:  ldc.i4.s   10
    IL_0002:  ret
  } // end of method EntryPoint01::get_static_initializer

  .method public static int32  main(string[] argsz) cil managed
  {
    .entrypoint
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.EntryPointAttribute::.ctor() = ( 01 00 00 00 ) 
    // Code size       35 (0x23)
    .maxstack  8
    .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
    .line 8,8 : 9,39 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\Misc\\EntryPoint01.fs'
    .line 100001,100001 : 0,0 ''
    IL_0000:  ldc.i4.0
    IL_0001:  stsfld     int32 '<StartupCode$EntryPoint01>'.$EntryPoint01::init@
    IL_0006:  ldsfld     int32 '<StartupCode$EntryPoint01>'.$EntryPoint01::init@
    IL_000b:  pop
    IL_000c:  call       int32 EntryPoint01::get_static_initializer()
    IL_0011:  ldc.i4.s   10
    IL_0013:  bne.un.s   IL_0019

    .line 8,8 : 40,41 ''
    IL_0015:  ldc.i4.0
    .line 100001,100001 : 0,0 ''
    IL_0016:  nop
    IL_0017:  br.s       IL_001b

    .line 8,8 : 47,48 ''
    IL_0019:  ldc.i4.1
    .line 100001,100001 : 0,0 ''
    IL_001a:  nop
    .line 100001,100001 : 0,0 ''
    IL_001b:  tail.
    IL_001d:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.Operators::Exit<int32>(int32)
    IL_0022:  ret
  } // end of method EntryPoint01::main

  .property int32 static_initializer()
  {
    .get int32 EntryPoint01::get_static_initializer()
  } // end of property EntryPoint01::static_initializer
} // end of class EntryPoint01

.class private abstract auto ansi sealed '<StartupCode$EntryPoint01>'.$EntryPoint01
       extends [mscorlib]System.Object
{
  .field static assembly int32 init@
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
  .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
  .method private specialname rtspecialname static 
          void  .cctor() cil managed
  {
    // Code size       7 (0x7)
    .maxstack  3
    .locals init ([0] int32 static_initializer)
    .line 4,4 : 1,28 ''
    IL_0000:  call       int32 EntryPoint01::get_static_initializer()
    IL_0005:  stloc.0
    IL_0006:  ret
  } // end of method $EntryPoint01::.cctor

} // end of class '<StartupCode$EntryPoint01>'.$EntryPoint01


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
