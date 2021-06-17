
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
.assembly StaticInit_Module01
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.StaticInit_Module01
{
  // Offset: 0x00000000 Length: 0x000002A3
}
.mresource public FSharpOptimizationData.StaticInit_Module01
{
  // Offset: 0x000002A8 Length: 0x000000DF
}
.module StaticInit_Module01.dll
// MVID: {60B68B90-705F-DF4F-A745-0383908BB660}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x050C0000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed StaticInit_Module01
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class abstract auto ansi sealed nested public M
         extends [mscorlib]System.Object
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
    .class abstract auto ansi sealed nested public N
           extends [mscorlib]System.Object
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
      .method public specialname static int32 
              get_y() cil managed
      {
        // Code size       6 (0x6)
        .maxstack  8
        IL_0000:  ldsfld     int32 '<StartupCode$StaticInit_Module01>'.$StaticInit_Module01::y@7
        IL_0005:  ret
      } // end of method N::get_y

      .method public specialname static int32 
              get_z() cil managed
      {
        // Code size       6 (0x6)
        .maxstack  8
        IL_0000:  ldsfld     int32 '<StartupCode$StaticInit_Module01>'.$StaticInit_Module01::z@8
        IL_0005:  ret
      } // end of method N::get_z

      .property int32 y()
      {
        .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 09 00 00 00 00 00 ) 
        .get int32 StaticInit_Module01/M/N::get_y()
      } // end of property N::y
      .property int32 z()
      {
        .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 09 00 00 00 00 00 ) 
        .get int32 StaticInit_Module01/M/N::get_z()
      } // end of property N::z
    } // end of class N

    .method public specialname static int32 
            get_x() cil managed
    {
      // Code size       6 (0x6)
      .maxstack  8
      IL_0000:  ldsfld     int32 '<StartupCode$StaticInit_Module01>'.$StaticInit_Module01::x@5
      IL_0005:  ret
    } // end of method M::get_x

    .property int32 x()
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 09 00 00 00 00 00 ) 
      .get int32 StaticInit_Module01/M::get_x()
    } // end of property M::x
  } // end of class M

} // end of class StaticInit_Module01

.class private abstract auto ansi sealed '<StartupCode$StaticInit_Module01>'.$StaticInit_Module01
       extends [mscorlib]System.Object
{
  .field static assembly initonly int32 x@5
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .field static assembly initonly int32 y@7
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .field static assembly initonly int32 z@8
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .field static assembly int32 init@
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
  .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
  .method private specialname rtspecialname static 
          void  .cctor() cil managed
  {
    // Code size       64 (0x40)
    .maxstack  4
    .locals init ([0] int32 x,
             [1] int32 y,
             [2] int32 z)
    .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
    .line 5,5 : 3,21 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\StaticInit\\StaticInit_Module01.fs'
    IL_0000:  ldstr      "1"
    IL_0005:  callvirt   instance int32 [mscorlib]System.String::get_Length()
    IL_000a:  dup
    IL_000b:  stsfld     int32 '<StartupCode$StaticInit_Module01>'.$StaticInit_Module01::x@5
    IL_0010:  stloc.0
    .line 7,7 : 5,27 ''
    IL_0011:  call       int32 StaticInit_Module01/M::get_x()
    IL_0016:  ldstr      "2"
    IL_001b:  callvirt   instance int32 [mscorlib]System.String::get_Length()
    IL_0020:  add
    IL_0021:  dup
    IL_0022:  stsfld     int32 '<StartupCode$StaticInit_Module01>'.$StaticInit_Module01::y@7
    IL_0027:  stloc.1
    .line 8,8 : 5,27 ''
    IL_0028:  call       int32 StaticInit_Module01/M/N::get_y()
    IL_002d:  ldstr      "3"
    IL_0032:  callvirt   instance int32 [mscorlib]System.String::get_Length()
    IL_0037:  add
    IL_0038:  dup
    IL_0039:  stsfld     int32 '<StartupCode$StaticInit_Module01>'.$StaticInit_Module01::z@8
    IL_003e:  stloc.2
    IL_003f:  ret
  } // end of method $StaticInit_Module01::.cctor

} // end of class '<StartupCode$StaticInit_Module01>'.$StaticInit_Module01


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
