
//  Microsoft (R) .NET Framework IL Disassembler.  Version 4.0.30319.1
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
  .ver 4:0:0:0
}
.assembly LazyNoInline01
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 03 00 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.LazyNoInline01
{
  // Offset: 0x00000000 Length: 0x000001A8
}
.mresource public FSharpSignatureDataB.LazyNoInline01
{
  // Offset: 0x00000000 Length: 0x000001A8
}
.mresource public FSharpOptimizationData.LazyNoInline01
{
  // Offset: 0x000001B0 Length: 0x00000074
}
.mresource public FSharpOptimizationDataB.LazyNoInline01
{
  // Offset: 0x000001B0 Length: 0x00000074
}
.module LazyNoInline01.exe
// MVID: {4CDA515D-3328-E592-A745-03835D51DA4C}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x00470000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed LazyNoInline01
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class auto ansi serializable nested assembly beforefieldinit x@6
         extends class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class [FSharp.Core]Microsoft.FSharp.Core.Unit,int32>
  {
    .method assembly specialname rtspecialname 
            instance void  .ctor() cil managed
    {
      // Code size       7 (0x7)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  call       instance void class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class [FSharp.Core]Microsoft.FSharp.Core.Unit,int32>::.ctor()
      IL_0006:  ret
    } // end of method x@6::.ctor

    .method public strict virtual instance int32 
            Invoke(class [FSharp.Core]Microsoft.FSharp.Core.Unit unitVar) cil managed
    {
      // Code size       3 (0x3)
      .maxstack  5
      .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
      .line 6,6 : 14,15 
      IL_0000:  nop
      IL_0001:  ldc.i4.1
      IL_0002:  ret
    } // end of method x@6::Invoke

  } // end of class x@6

  .method public specialname static class [mscorlib]System.Lazy`1<int32> 
          get_x() cil managed
  {
    // Code size       6 (0x6)
    .maxstack  4
    IL_0000:  ldsfld     class [mscorlib]System.Lazy`1<int32> '<StartupCode$LazyNoInline01>'.$LazyNoInline01::x@6
    IL_0005:  ret
  } // end of method LazyNoInline01::get_x

  .method assembly specialname static int32 
          'get_x@1-1'() cil managed
  {
    // Code size       6 (0x6)
    .maxstack  4
    IL_0000:  ldsfld     int32 '<StartupCode$LazyNoInline01>'.$LazyNoInline01::'x@1-1'
    IL_0005:  ret
  } // end of method LazyNoInline01::'get_x@1-1'

  .property class [mscorlib]System.Lazy`1<int32>
          x()
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 09 00 00 00 00 00 ) 
    .get class [mscorlib]System.Lazy`1<int32> LazyNoInline01::get_x()
  } // end of property LazyNoInline01::x
  .property int32 'x@1-1'()
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 09 00 00 00 00 00 ) 
    .get int32 LazyNoInline01::'get_x@1-1'()
  } // end of property LazyNoInline01::'x@1-1'
} // end of class LazyNoInline01

.class private abstract auto ansi sealed '<StartupCode$LazyNoInline01>'.$LazyNoInline01
       extends [mscorlib]System.Object
{
  .field static assembly class [mscorlib]System.Lazy`1<int32> x@6
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .field static assembly int32 'x@1-1'
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .field static assembly int32 init@
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
  .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
  .method public static void  main@() cil managed
  {
    .entrypoint
    // Code size       32 (0x20)
    .maxstack  3
    .line 6,6 : 1,16 
    IL_0000:  nop
    IL_0001:  newobj     instance void LazyNoInline01/x@6::.ctor()
    IL_0006:  call       class [mscorlib]System.Lazy`1<!!0> [FSharp.Core]Microsoft.FSharp.Control.LazyExtensions::Create<int32>(class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class [FSharp.Core]Microsoft.FSharp.Core.Unit,!!0>)
    IL_000b:  stsfld     class [mscorlib]System.Lazy`1<int32> '<StartupCode$LazyNoInline01>'.$LazyNoInline01::x@6
    .line 8,8 : 1,20 
    IL_0010:  call       class [mscorlib]System.Lazy`1<int32> LazyNoInline01::get_x()
    IL_0015:  call       instance !0 class [mscorlib]System.Lazy`1<int32>::get_Value()
    IL_001a:  stsfld     int32 '<StartupCode$LazyNoInline01>'.$LazyNoInline01::'x@1-1'
    IL_001f:  ret
  } // end of method $LazyNoInline01::main@

} // end of class '<StartupCode$LazyNoInline01>'.$LazyNoInline01


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
