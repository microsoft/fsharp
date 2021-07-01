
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
.assembly CompiledNameAttribute04
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.CompiledNameAttribute04
{
  // Offset: 0x00000000 Length: 0x00000CD9
}
.mresource public FSharpSignatureDataB.CompiledNameAttribute04
{
  // Offset: 0x00000CE0 Length: 0x00000007
}
.mresource public FSharpOptimizationData.CompiledNameAttribute04
{
  // Offset: 0x00000CF0 Length: 0x000002CB
}
.mresource public FSharpOptimizationDataB.CompiledNameAttribute04
{
  // Offset: 0x00000FC0 Length: 0x00000002
}
.module CompiledNameAttribute04.exe
// MVID: {5F972A55-34DF-584F-A745-0383552A975F}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x06E70000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed CompiledNameAttribute04
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class abstract auto ansi serializable nested public C
         extends [mscorlib]System.Object
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.AbstractClassAttribute::.ctor() = ( 01 00 00 00 ) 
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 03 00 00 00 00 00 ) 
    .method public hidebysig abstract virtual 
            instance int32  A1(int32 A_1,
                               int32 A_2) cil managed
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationArgumentCountsAttribute::.ctor(int32[]) = ( 01 00 02 00 00 00 01 00 00 00 01 00 00 00 00 00 ) 
    } // end of method C::A1

    .method public hidebysig abstract virtual 
            instance int32  A2(int32 A_1) cil managed
    {
    } // end of method C::A2

    .method public specialname rtspecialname 
            instance void  .ctor() cil managed
    {
      // Code size       9 (0x9)
      .maxstack  8
      .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
      .line 100001,100001 : 0,0 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\CompiledNameAttribute\\CompiledNameAttribute04.fs'
      IL_0000:  ldarg.0
      IL_0001:  callvirt   instance void [mscorlib]System.Object::.ctor()
      IL_0006:  ldarg.0
      IL_0007:  pop
      .line 20,20 : 6,7 ''
      IL_0008:  ret
    } // end of method C::.ctor

    .method public hidebysig specialname 
            instance int32  get_P() cil managed
    {
      // Code size       2 (0x2)
      .maxstack  8
      .line 21,21 : 19,20 ''
      IL_0000:  ldc.i4.1
      IL_0001:  ret
    } // end of method C::get_P

    .method public hidebysig instance int32 
            M1(int32 x,
               int32 y) cil managed
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationArgumentCountsAttribute::.ctor(int32[]) = ( 01 00 02 00 00 00 01 00 00 00 01 00 00 00 00 00 ) 
      // Code size       4 (0x4)
      .maxstack  8
      .line 22,22 : 24,29 ''
      IL_0000:  ldarg.1
      IL_0001:  ldarg.2
      IL_0002:  add
      IL_0003:  ret
    } // end of method C::M1

    .method public hidebysig instance !!a 
            M2<a>(!!a x) cil managed preservesig
    {
      // Code size       2 (0x2)
      .maxstack  8
      .line 24,24 : 22,23 ''
      IL_0000:  ldarg.1
      IL_0001:  ret
    } // end of method C::M2

    .property instance int32 P()
    {
      .get instance int32 CompiledNameAttribute04/C::get_P()
    } // end of property C::P
  } // end of class C

  .class interface abstract auto ansi serializable nested public IInterface
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 03 00 00 00 00 00 ) 
    .method public hidebysig abstract virtual 
            instance int32  SomeMethod(int32 A_1) cil managed preservesig
    {
    } // end of method IInterface::SomeMethod

  } // end of class IInterface

  .class sequential ansi serializable sealed nested public S
         extends [mscorlib]System.ValueType
         implements class [mscorlib]System.IEquatable`1<valuetype CompiledNameAttribute04/S>,
                    [mscorlib]System.Collections.IStructuralEquatable,
                    class [mscorlib]System.IComparable`1<valuetype CompiledNameAttribute04/S>,
                    [mscorlib]System.IComparable,
                    [mscorlib]System.Collections.IStructuralComparable
  {
    .pack 0
    .size 1
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 03 00 00 00 00 00 ) 
    .method public hidebysig virtual final 
            instance int32  CompareTo(valuetype CompiledNameAttribute04/S obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       5 (0x5)
      .maxstack  3
      .locals init ([0] valuetype CompiledNameAttribute04/S& V_0)
      .line 37,37 : 6,7 ''
      IL_0000:  ldarga.s   obj
      IL_0002:  stloc.0
      IL_0003:  ldc.i4.0
      IL_0004:  ret
    } // end of method S::CompareTo

    .method public hidebysig virtual final 
            instance int32  CompareTo(object obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       13 (0xd)
      .maxstack  8
      .line 37,37 : 6,7 ''
      IL_0000:  ldarg.0
      IL_0001:  ldarg.1
      IL_0002:  unbox.any  CompiledNameAttribute04/S
      IL_0007:  call       instance int32 CompiledNameAttribute04/S::CompareTo(valuetype CompiledNameAttribute04/S)
      IL_000c:  ret
    } // end of method S::CompareTo

    .method public hidebysig virtual final 
            instance int32  CompareTo(object obj,
                                      class [mscorlib]System.Collections.IComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       12 (0xc)
      .maxstack  3
      .locals init ([0] valuetype CompiledNameAttribute04/S V_0,
               [1] valuetype CompiledNameAttribute04/S& V_1)
      .line 37,37 : 6,7 ''
      IL_0000:  ldarg.1
      IL_0001:  unbox.any  CompiledNameAttribute04/S
      IL_0006:  stloc.0
      IL_0007:  ldloca.s   V_0
      IL_0009:  stloc.1
      IL_000a:  ldc.i4.0
      IL_000b:  ret
    } // end of method S::CompareTo

    .method public hidebysig virtual final 
            instance int32  GetHashCode(class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       2 (0x2)
      .maxstack  8
      .line 37,37 : 6,7 ''
      IL_0000:  ldc.i4.0
      IL_0001:  ret
    } // end of method S::GetHashCode

    .method public hidebysig virtual final 
            instance int32  GetHashCode() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       12 (0xc)
      .maxstack  8
      .line 37,37 : 6,7 ''
      IL_0000:  ldarg.0
      IL_0001:  call       class [mscorlib]System.Collections.IEqualityComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericEqualityComparer()
      IL_0006:  call       instance int32 CompiledNameAttribute04/S::GetHashCode(class [mscorlib]System.Collections.IEqualityComparer)
      IL_000b:  ret
    } // end of method S::GetHashCode

    .method public hidebysig virtual final 
            instance bool  Equals(object obj,
                                  class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       24 (0x18)
      .maxstack  3
      .locals init ([0] valuetype CompiledNameAttribute04/S V_0,
               [1] valuetype CompiledNameAttribute04/S& V_1)
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldarg.1
      IL_0001:  call       bool [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/IntrinsicFunctions::TypeTestGeneric<valuetype CompiledNameAttribute04/S>(object)
      IL_0006:  brtrue.s   IL_000a

      IL_0008:  br.s       IL_0016

      .line 100001,100001 : 0,0 ''
      IL_000a:  ldarg.1
      IL_000b:  unbox.any  CompiledNameAttribute04/S
      IL_0010:  stloc.0
      IL_0011:  ldloca.s   V_0
      IL_0013:  stloc.1
      IL_0014:  ldc.i4.1
      IL_0015:  ret

      .line 100001,100001 : 0,0 ''
      IL_0016:  ldc.i4.0
      IL_0017:  ret
    } // end of method S::Equals

    .method public hidebysig instance !!a 
            M1<a>(!!a x) cil managed preservesig
    {
      // Code size       2 (0x2)
      .maxstack  8
      .line 40,40 : 24,25 ''
      IL_0000:  ldarg.1
      IL_0001:  ret
    } // end of method S::M1

    .method public hidebysig virtual final 
            instance bool  Equals(valuetype CompiledNameAttribute04/S obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       5 (0x5)
      .maxstack  3
      .locals init ([0] valuetype CompiledNameAttribute04/S& V_0)
      .line 37,37 : 6,7 ''
      IL_0000:  ldarga.s   obj
      IL_0002:  stloc.0
      IL_0003:  ldc.i4.1
      IL_0004:  ret
    } // end of method S::Equals

    .method public hidebysig virtual final 
            instance bool  Equals(object obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       27 (0x1b)
      .maxstack  4
      .locals init ([0] valuetype CompiledNameAttribute04/S V_0)
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldarg.1
      IL_0001:  call       bool [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/IntrinsicFunctions::TypeTestGeneric<valuetype CompiledNameAttribute04/S>(object)
      IL_0006:  brtrue.s   IL_000a

      IL_0008:  br.s       IL_0019

      .line 100001,100001 : 0,0 ''
      IL_000a:  ldarg.1
      IL_000b:  unbox.any  CompiledNameAttribute04/S
      IL_0010:  stloc.0
      IL_0011:  ldarg.0
      IL_0012:  ldloc.0
      IL_0013:  call       instance bool CompiledNameAttribute04/S::Equals(valuetype CompiledNameAttribute04/S)
      IL_0018:  ret

      .line 100001,100001 : 0,0 ''
      IL_0019:  ldc.i4.0
      IL_001a:  ret
    } // end of method S::Equals

  } // end of class S

  .class interface abstract auto ansi serializable nested public ITestInterface
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 03 00 00 00 00 00 ) 
    .method public hidebysig abstract virtual 
            instance int32  M(int32 A_1) cil managed
    {
    } // end of method ITestInterface::M

  } // end of class ITestInterface

  .class auto autochar serializable sealed nested assembly beforefieldinit specialname a@49
         extends [mscorlib]System.Object
         implements CompiledNameAttribute04/ITestInterface
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 06 00 00 00 00 00 ) 
    .method public specialname rtspecialname 
            instance void  .ctor() cil managed
    {
      // Code size       9 (0x9)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  callvirt   instance void [mscorlib]System.Object::.ctor()
      IL_0006:  ldarg.0
      IL_0007:  pop
      IL_0008:  ret
    } // end of method a@49::.ctor

    .method private hidebysig newslot virtual final 
            instance int32  CompiledNameAttribute04.ITestInterface.M(int32 x) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.InteropServices.PreserveSigAttribute::.ctor() = ( 01 00 00 00 ) 
      .override CompiledNameAttribute04/ITestInterface::M
      // Code size       4 (0x4)
      .maxstack  8
      .line 51,51 : 33,38 ''
      IL_0000:  ldarg.1
      IL_0001:  ldc.i4.1
      IL_0002:  add
      IL_0003:  ret
    } // end of method a@49::CompiledNameAttribute04.ITestInterface.M

  } // end of class a@49

  .method public static int32  f1(int32 x,
                                  int32 y) cil managed
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationArgumentCountsAttribute::.ctor(int32[]) = ( 01 00 02 00 00 00 01 00 00 00 01 00 00 00 00 00 ) 
    // Code size       4 (0x4)
    .maxstack  8
    .line 16,16 : 14,19 ''
    IL_0000:  ldarg.0
    IL_0001:  ldarg.1
    IL_0002:  add
    IL_0003:  ret
  } // end of method CompiledNameAttribute04::f1

  .method public static !!a  f2<a>(!!a x) cil managed
  {
    // Code size       2 (0x2)
    .maxstack  8
    .line 17,17 : 12,13 ''
    IL_0000:  ldarg.0
    IL_0001:  ret
  } // end of method CompiledNameAttribute04::f2

  .method public specialname static class CompiledNameAttribute04/ITestInterface 
          get_a() cil managed
  {
    // Code size       6 (0x6)
    .maxstack  8
    IL_0000:  ldsfld     class CompiledNameAttribute04/ITestInterface '<StartupCode$CompiledNameAttribute04>'.$CompiledNameAttribute04::a@49
    IL_0005:  ret
  } // end of method CompiledNameAttribute04::get_a

  .property class CompiledNameAttribute04/ITestInterface
          a()
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 09 00 00 00 00 00 ) 
    .get class CompiledNameAttribute04/ITestInterface CompiledNameAttribute04::get_a()
  } // end of property CompiledNameAttribute04::a
} // end of class CompiledNameAttribute04

.class private abstract auto ansi sealed '<StartupCode$CompiledNameAttribute04>'.$CompiledNameAttribute04
       extends [mscorlib]System.Object
{
  .field static assembly class CompiledNameAttribute04/ITestInterface a@49
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .field static assembly int32 init@
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
  .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
  .method public static void  main@() cil managed
  {
    .entrypoint
    // Code size       13 (0xd)
    .maxstack  4
    .locals init (class CompiledNameAttribute04/ITestInterface V_0)
    IL_0000:  newobj     instance void CompiledNameAttribute04/a@49::.ctor()
    IL_0005:  dup
    IL_0006:  stsfld     class CompiledNameAttribute04/ITestInterface '<StartupCode$CompiledNameAttribute04>'.$CompiledNameAttribute04::a@49
    IL_000b:  stloc.0
    IL_000c:  ret
  } // end of method $CompiledNameAttribute04::main@

} // end of class '<StartupCode$CompiledNameAttribute04>'.$CompiledNameAttribute04


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
