
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
.assembly Structs01
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.Structs01
{
  // Offset: 0x00000000 Length: 0x0000073D
}
.mresource public FSharpOptimizationData.Structs01
{
  // Offset: 0x00000748 Length: 0x00000231
}
.module Structs01.exe
// MVID: {60B68B7F-701F-5E27-A745-03837F8BB660}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x07280000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed Experiment.Test
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class sequential ansi serializable sealed nested public Test
         extends [mscorlib]System.ValueType
         implements class [mscorlib]System.IEquatable`1<valuetype Experiment.Test/Test>,
                    [mscorlib]System.Collections.IStructuralEquatable,
                    class [mscorlib]System.IComparable`1<valuetype Experiment.Test/Test>,
                    [mscorlib]System.IComparable,
                    [mscorlib]System.Collections.IStructuralComparable
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 03 00 00 00 00 00 ) 
    .field public int32 Field
    .method public hidebysig virtual final 
            instance int32  CompareTo(valuetype Experiment.Test/Test obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       34 (0x22)
      .maxstack  4
      .locals init ([0] valuetype Experiment.Test/Test& V_0,
               [1] class [mscorlib]System.Collections.IComparer V_1,
               [2] int32 V_2,
               [3] int32 V_3)
      .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
      .line 7,7 : 6,10 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\Misc\\Structs01.fs'
      IL_0000:  ldarga.s   obj
      IL_0002:  stloc.0
      IL_0003:  call       class [mscorlib]System.Collections.IComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericComparer()
      IL_0008:  stloc.1
      IL_0009:  ldarg.0
      IL_000a:  ldfld      int32 Experiment.Test/Test::Field
      IL_000f:  stloc.2
      IL_0010:  ldloc.0
      IL_0011:  ldfld      int32 Experiment.Test/Test::Field
      IL_0016:  stloc.3
      IL_0017:  ldloc.2
      IL_0018:  ldloc.3
      IL_0019:  bge.s      IL_001d

      .line 100001,100001 : 0,0 ''
      IL_001b:  ldc.i4.m1
      IL_001c:  ret

      .line 100001,100001 : 0,0 ''
      IL_001d:  ldloc.2
      IL_001e:  ldloc.3
      IL_001f:  cgt
      IL_0021:  ret
    } // end of method Test::CompareTo

    .method public hidebysig virtual final 
            instance int32  CompareTo(object obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       13 (0xd)
      .maxstack  8
      .line 7,7 : 6,10 ''
      IL_0000:  ldarg.0
      IL_0001:  ldarg.1
      IL_0002:  unbox.any  Experiment.Test/Test
      IL_0007:  call       instance int32 Experiment.Test/Test::CompareTo(valuetype Experiment.Test/Test)
      IL_000c:  ret
    } // end of method Test::CompareTo

    .method public hidebysig virtual final 
            instance int32  CompareTo(object obj,
                                      class [mscorlib]System.Collections.IComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       40 (0x28)
      .maxstack  4
      .locals init ([0] valuetype Experiment.Test/Test V_0,
               [1] valuetype Experiment.Test/Test& V_1,
               [2] class [mscorlib]System.Collections.IComparer V_2,
               [3] int32 V_3,
               [4] int32 V_4)
      .line 7,7 : 6,10 ''
      IL_0000:  ldarg.1
      IL_0001:  unbox.any  Experiment.Test/Test
      IL_0006:  stloc.0
      IL_0007:  ldloca.s   V_0
      IL_0009:  stloc.1
      IL_000a:  ldarg.2
      IL_000b:  stloc.2
      IL_000c:  ldarg.0
      IL_000d:  ldfld      int32 Experiment.Test/Test::Field
      IL_0012:  stloc.3
      IL_0013:  ldloc.1
      IL_0014:  ldfld      int32 Experiment.Test/Test::Field
      IL_0019:  stloc.s    V_4
      IL_001b:  ldloc.3
      IL_001c:  ldloc.s    V_4
      IL_001e:  bge.s      IL_0022

      .line 100001,100001 : 0,0 ''
      IL_0020:  ldc.i4.m1
      IL_0021:  ret

      .line 100001,100001 : 0,0 ''
      IL_0022:  ldloc.3
      IL_0023:  ldloc.s    V_4
      IL_0025:  cgt
      IL_0027:  ret
    } // end of method Test::CompareTo

    .method public hidebysig virtual final 
            instance int32  GetHashCode(class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       27 (0x1b)
      .maxstack  7
      .locals init ([0] int32 V_0,
               [1] class [mscorlib]System.Collections.IEqualityComparer V_1)
      .line 7,7 : 6,10 ''
      IL_0000:  ldc.i4.0
      IL_0001:  stloc.0
      IL_0002:  ldc.i4     0x9e3779b9
      IL_0007:  ldarg.1
      IL_0008:  stloc.1
      IL_0009:  ldarg.0
      IL_000a:  ldfld      int32 Experiment.Test/Test::Field
      IL_000f:  ldloc.0
      IL_0010:  ldc.i4.6
      IL_0011:  shl
      IL_0012:  ldloc.0
      IL_0013:  ldc.i4.2
      IL_0014:  shr
      IL_0015:  add
      IL_0016:  add
      IL_0017:  add
      IL_0018:  stloc.0
      IL_0019:  ldloc.0
      IL_001a:  ret
    } // end of method Test::GetHashCode

    .method public hidebysig virtual final 
            instance int32  GetHashCode() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       12 (0xc)
      .maxstack  8
      .line 7,7 : 6,10 ''
      IL_0000:  ldarg.0
      IL_0001:  call       class [mscorlib]System.Collections.IEqualityComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericEqualityComparer()
      IL_0006:  call       instance int32 Experiment.Test/Test::GetHashCode(class [mscorlib]System.Collections.IEqualityComparer)
      IL_000b:  ret
    } // end of method Test::GetHashCode

    .method public hidebysig virtual final 
            instance bool  Equals(object obj,
                                  class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       39 (0x27)
      .maxstack  4
      .locals init ([0] valuetype Experiment.Test/Test V_0,
               [1] valuetype Experiment.Test/Test& V_1,
               [2] class [mscorlib]System.Collections.IEqualityComparer V_2)
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldarg.1
      IL_0001:  call       bool [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/IntrinsicFunctions::TypeTestGeneric<valuetype Experiment.Test/Test>(object)
      IL_0006:  brtrue.s   IL_000a

      IL_0008:  br.s       IL_0025

      .line 100001,100001 : 0,0 ''
      IL_000a:  ldarg.1
      IL_000b:  unbox.any  Experiment.Test/Test
      IL_0010:  stloc.0
      IL_0011:  ldloca.s   V_0
      IL_0013:  stloc.1
      IL_0014:  ldarg.2
      IL_0015:  stloc.2
      IL_0016:  ldarg.0
      IL_0017:  ldfld      int32 Experiment.Test/Test::Field
      IL_001c:  ldloc.1
      IL_001d:  ldfld      int32 Experiment.Test/Test::Field
      IL_0022:  ceq
      IL_0024:  ret

      .line 100001,100001 : 0,0 ''
      IL_0025:  ldc.i4.0
      IL_0026:  ret
    } // end of method Test::Equals

    .method public specialname rtspecialname 
            instance void  .ctor(int32 i) cil managed
    {
      // Code size       8 (0x8)
      .maxstack  8
      .line 8,8 : 16,27 ''
      IL_0000:  ldarg.0
      IL_0001:  ldarg.1
      IL_0002:  stfld      int32 Experiment.Test/Test::Field
      IL_0007:  ret
    } // end of method Test::.ctor

    .method public hidebysig virtual final 
            instance bool  Equals(valuetype Experiment.Test/Test obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       18 (0x12)
      .maxstack  4
      .locals init ([0] valuetype Experiment.Test/Test& V_0)
      .line 7,7 : 6,10 ''
      IL_0000:  ldarga.s   obj
      IL_0002:  stloc.0
      IL_0003:  ldarg.0
      IL_0004:  ldfld      int32 Experiment.Test/Test::Field
      IL_0009:  ldloc.0
      IL_000a:  ldfld      int32 Experiment.Test/Test::Field
      IL_000f:  ceq
      IL_0011:  ret
    } // end of method Test::Equals

    .method public hidebysig virtual final 
            instance bool  Equals(object obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       27 (0x1b)
      .maxstack  4
      .locals init ([0] valuetype Experiment.Test/Test V_0)
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldarg.1
      IL_0001:  call       bool [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/IntrinsicFunctions::TypeTestGeneric<valuetype Experiment.Test/Test>(object)
      IL_0006:  brtrue.s   IL_000a

      IL_0008:  br.s       IL_0019

      .line 100001,100001 : 0,0 ''
      IL_000a:  ldarg.1
      IL_000b:  unbox.any  Experiment.Test/Test
      IL_0010:  stloc.0
      IL_0011:  ldarg.0
      IL_0012:  ldloc.0
      IL_0013:  call       instance bool Experiment.Test/Test::Equals(valuetype Experiment.Test/Test)
      IL_0018:  ret

      .line 100001,100001 : 0,0 ''
      IL_0019:  ldc.i4.0
      IL_001a:  ret
    } // end of method Test::Equals

  } // end of class Test

  .method public static int32  test() cil managed
  {
    // Code size       15 (0xf)
    .maxstack  3
    .locals init ([0] valuetype Experiment.Test/Test t)
    .line 13,13 : 6,25 ''
    IL_0000:  ldc.i4.2
    IL_0001:  newobj     instance void Experiment.Test/Test::.ctor(int32)
    IL_0006:  stloc.0
    .line 14,14 : 6,13 ''
    IL_0007:  ldloca.s   t
    IL_0009:  ldfld      int32 Experiment.Test/Test::Field
    IL_000e:  ret
  } // end of method Test::test

} // end of class Experiment.Test

.class private abstract auto ansi sealed '<StartupCode$Structs01>.$Experiment'.Test
       extends [mscorlib]System.Object
{
  .method public static void  main@() cil managed
  {
    .entrypoint
    // Code size       1 (0x1)
    .maxstack  8
    IL_0000:  ret
  } // end of method Test::main@

} // end of class '<StartupCode$Structs01>.$Experiment'.Test


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
