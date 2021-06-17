
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
.assembly NoBoxingOnDispose01
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.NoBoxingOnDispose01
{
  // Offset: 0x00000000 Length: 0x00000216
}
.mresource public FSharpOptimizationData.NoBoxingOnDispose01
{
  // Offset: 0x00000220 Length: 0x0000007F
}
.module NoBoxingOnDispose01.exe
// MVID: {60B68B7F-4EA9-C934-A745-03837F8BB660}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x06C40000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed NoBoxingOnDispose01
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .method public static void  f1<T>(class [mscorlib]System.Collections.Generic.List`1<!!T> x) cil managed
  {
    // Code size       52 (0x34)
    .maxstack  3
    .locals init ([0] class [mscorlib]System.Collections.Generic.List`1<!!T> V_0,
             [1] valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<!!T> V_1,
             [2] class [FSharp.Core]Microsoft.FSharp.Core.Unit V_2,
             [3] !!T a)
    .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
    .line 6,6 : 3,16 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\Misc\\NoBoxingOnDispose01.fs'
    IL_0000:  ldarg.0
    IL_0001:  stloc.0
    .line 6,6 : 3,16 ''
    IL_0002:  ldloc.0
    IL_0003:  callvirt   instance valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<!0> class [mscorlib]System.Collections.Generic.List`1<!!T>::GetEnumerator()
    IL_0008:  stloc.1
    .try
    {
      IL_0009:  ldloca.s   V_1
      IL_000b:  call       instance bool valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<!!T>::MoveNext()
      IL_0010:  brfalse.s  IL_001d

      .line 6,6 : 3,16 ''
      IL_0012:  ldloca.s   V_1
      IL_0014:  call       instance !0 valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<!!T>::get_Current()
      IL_0019:  stloc.3
      .line 100001,100001 : 0,0 ''
      IL_001a:  nop
      IL_001b:  br.s       IL_0009

      IL_001d:  ldnull
      IL_001e:  stloc.2
      IL_001f:  leave.s    IL_0031

    }  // end .try
    finally
    {
      IL_0021:  ldloca.s   V_1
      IL_0023:  constrained. valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<!!T>
      IL_0029:  callvirt   instance void [mscorlib]System.IDisposable::Dispose()
      IL_002e:  ldnull
      IL_002f:  pop
      IL_0030:  endfinally
      .line 100001,100001 : 0,0 ''
    }  // end handler
    IL_0031:  ldloc.2
    IL_0032:  pop
    IL_0033:  ret
  } // end of method NoBoxingOnDispose01::f1

} // end of class NoBoxingOnDispose01

.class private abstract auto ansi sealed '<StartupCode$NoBoxingOnDispose01>'.$NoBoxingOnDispose01
       extends [mscorlib]System.Object
{
  .method public static void  main@() cil managed
  {
    .entrypoint
    // Code size       1 (0x1)
    .maxstack  8
    IL_0000:  ret
  } // end of method $NoBoxingOnDispose01::main@

} // end of class '<StartupCode$NoBoxingOnDispose01>'.$NoBoxingOnDispose01


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
