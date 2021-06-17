
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
.assembly SteppingMatch06
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.SteppingMatch06
{
  // Offset: 0x00000000 Length: 0x00000675
}
.mresource public FSharpOptimizationData.SteppingMatch06
{
  // Offset: 0x00000680 Length: 0x000001D9
}
.module SteppingMatch06.dll
// MVID: {60B68B90-4FAE-FD21-A745-0383908BB660}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x05BB0000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed SteppingMatch06
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class auto autochar serializable sealed nested public beforefieldinit Discr
         extends [mscorlib]System.Object
         implements class [mscorlib]System.IEquatable`1<class SteppingMatch06/Discr>,
                    [mscorlib]System.Collections.IStructuralEquatable,
                    class [mscorlib]System.IComparable`1<class SteppingMatch06/Discr>,
                    [mscorlib]System.IComparable,
                    [mscorlib]System.Collections.IStructuralComparable
  {
    .custom instance void [mscorlib]System.Diagnostics.DebuggerDisplayAttribute::.ctor(string) = ( 01 00 15 7B 5F 5F 44 65 62 75 67 44 69 73 70 6C   // ...{__DebugDispl
                                                                                                   61 79 28 29 2C 6E 71 7D 00 00 )                   // ay(),nq}..
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 01 00 00 00 00 00 ) 
    .class abstract auto ansi sealed nested public Tags
           extends [mscorlib]System.Object
    {
      .field public static literal int32 CaseA = int32(0x00000000)
      .field public static literal int32 CaseB = int32(0x00000001)
    } // end of class Tags

    .field assembly initonly int32 _tag
    .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
    .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
    .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
    .field static assembly initonly class SteppingMatch06/Discr _unique_CaseA
    .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
    .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
    .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
    .field static assembly initonly class SteppingMatch06/Discr _unique_CaseB
    .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
    .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
    .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
    .method private specialname rtspecialname static 
            void  .cctor() cil managed
    {
      // Code size       23 (0x17)
      .maxstack  8
      IL_0000:  ldc.i4.0
      IL_0001:  newobj     instance void SteppingMatch06/Discr::.ctor(int32)
      IL_0006:  stsfld     class SteppingMatch06/Discr SteppingMatch06/Discr::_unique_CaseA
      IL_000b:  ldc.i4.1
      IL_000c:  newobj     instance void SteppingMatch06/Discr::.ctor(int32)
      IL_0011:  stsfld     class SteppingMatch06/Discr SteppingMatch06/Discr::_unique_CaseB
      IL_0016:  ret
    } // end of method Discr::.cctor

    .method assembly specialname rtspecialname 
            instance void  .ctor(int32 _tag) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       14 (0xe)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
      IL_0006:  ldarg.0
      IL_0007:  ldarg.1
      IL_0008:  stfld      int32 SteppingMatch06/Discr::_tag
      IL_000d:  ret
    } // end of method Discr::.ctor

    .method public static class SteppingMatch06/Discr 
            get_CaseA() cil managed
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                  int32) = ( 01 00 08 00 00 00 00 00 00 00 00 00 ) 
      // Code size       6 (0x6)
      .maxstack  8
      IL_0000:  ldsfld     class SteppingMatch06/Discr SteppingMatch06/Discr::_unique_CaseA
      IL_0005:  ret
    } // end of method Discr::get_CaseA

    .method public hidebysig instance bool 
            get_IsCaseA() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       10 (0xa)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  call       instance int32 SteppingMatch06/Discr::get_Tag()
      IL_0006:  ldc.i4.0
      IL_0007:  ceq
      IL_0009:  ret
    } // end of method Discr::get_IsCaseA

    .method public static class SteppingMatch06/Discr 
            get_CaseB() cil managed
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                  int32) = ( 01 00 08 00 00 00 01 00 00 00 00 00 ) 
      // Code size       6 (0x6)
      .maxstack  8
      IL_0000:  ldsfld     class SteppingMatch06/Discr SteppingMatch06/Discr::_unique_CaseB
      IL_0005:  ret
    } // end of method Discr::get_CaseB

    .method public hidebysig instance bool 
            get_IsCaseB() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       10 (0xa)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  call       instance int32 SteppingMatch06/Discr::get_Tag()
      IL_0006:  ldc.i4.1
      IL_0007:  ceq
      IL_0009:  ret
    } // end of method Discr::get_IsCaseB

    .method public hidebysig instance int32 
            get_Tag() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       7 (0x7)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  ldfld      int32 SteppingMatch06/Discr::_tag
      IL_0006:  ret
    } // end of method Discr::get_Tag

    .method assembly hidebysig specialname 
            instance object  __DebugDisplay() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       22 (0x16)
      .maxstack  8
      IL_0000:  ldstr      "%+0.8A"
      IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class SteppingMatch06/Discr,string>,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string,string>::.ctor(string)
      IL_000a:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatToString<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class SteppingMatch06/Discr,string>>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string>)
      IL_000f:  ldarg.0
      IL_0010:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class SteppingMatch06/Discr,string>::Invoke(!0)
      IL_0015:  ret
    } // end of method Discr::__DebugDisplay

    .method public strict virtual instance string 
            ToString() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       22 (0x16)
      .maxstack  8
      IL_0000:  ldstr      "%+A"
      IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class SteppingMatch06/Discr,string>,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string,class SteppingMatch06/Discr>::.ctor(string)
      IL_000a:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatToString<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class SteppingMatch06/Discr,string>>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string>)
      IL_000f:  ldarg.0
      IL_0010:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class SteppingMatch06/Discr,string>::Invoke(!0)
      IL_0015:  ret
    } // end of method Discr::ToString

    .method public hidebysig virtual final 
            instance int32  CompareTo(class SteppingMatch06/Discr obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       48 (0x30)
      .maxstack  4
      .locals init ([0] int32 V_0,
               [1] int32 V_1)
      .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
      .line 100001,100001 : 0,0 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\SteppingMatch\\SteppingMatch06.fs'
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse.s  IL_0026

      .line 100001,100001 : 0,0 ''
      IL_0006:  ldarg.1
      IL_0007:  ldnull
      IL_0008:  cgt.un
      IL_000a:  brfalse.s  IL_0024

      .line 100001,100001 : 0,0 ''
      IL_000c:  ldarg.0
      IL_000d:  ldfld      int32 SteppingMatch06/Discr::_tag
      IL_0012:  stloc.0
      IL_0013:  ldarg.1
      IL_0014:  ldfld      int32 SteppingMatch06/Discr::_tag
      IL_0019:  stloc.1
      IL_001a:  ldloc.0
      IL_001b:  ldloc.1
      IL_001c:  bne.un.s   IL_0020

      .line 100001,100001 : 0,0 ''
      IL_001e:  ldc.i4.0
      IL_001f:  ret

      .line 100001,100001 : 0,0 ''
      IL_0020:  ldloc.0
      IL_0021:  ldloc.1
      IL_0022:  sub
      IL_0023:  ret

      .line 100001,100001 : 0,0 ''
      IL_0024:  ldc.i4.1
      IL_0025:  ret

      .line 100001,100001 : 0,0 ''
      IL_0026:  ldarg.1
      IL_0027:  ldnull
      IL_0028:  cgt.un
      IL_002a:  brfalse.s  IL_002e

      .line 100001,100001 : 0,0 ''
      IL_002c:  ldc.i4.m1
      IL_002d:  ret

      .line 100001,100001 : 0,0 ''
      IL_002e:  ldc.i4.0
      IL_002f:  ret
    } // end of method Discr::CompareTo

    .method public hidebysig virtual final 
            instance int32  CompareTo(object obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       13 (0xd)
      .maxstack  8
      .line 4,4 : 6,11 ''
      IL_0000:  ldarg.0
      IL_0001:  ldarg.1
      IL_0002:  unbox.any  SteppingMatch06/Discr
      IL_0007:  callvirt   instance int32 SteppingMatch06/Discr::CompareTo(class SteppingMatch06/Discr)
      IL_000c:  ret
    } // end of method Discr::CompareTo

    .method public hidebysig virtual final 
            instance int32  CompareTo(object obj,
                                      class [mscorlib]System.Collections.IComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       65 (0x41)
      .maxstack  4
      .locals init ([0] class SteppingMatch06/Discr V_0,
               [1] int32 V_1,
               [2] int32 V_2)
      .line 4,4 : 6,11 ''
      IL_0000:  ldarg.1
      IL_0001:  unbox.any  SteppingMatch06/Discr
      IL_0006:  stloc.0
      IL_0007:  ldarg.0
      IL_0008:  ldnull
      IL_0009:  cgt.un
      IL_000b:  brfalse.s  IL_0032

      .line 100001,100001 : 0,0 ''
      IL_000d:  ldarg.1
      IL_000e:  unbox.any  SteppingMatch06/Discr
      IL_0013:  ldnull
      IL_0014:  cgt.un
      IL_0016:  brfalse.s  IL_0030

      .line 100001,100001 : 0,0 ''
      IL_0018:  ldarg.0
      IL_0019:  ldfld      int32 SteppingMatch06/Discr::_tag
      IL_001e:  stloc.1
      IL_001f:  ldloc.0
      IL_0020:  ldfld      int32 SteppingMatch06/Discr::_tag
      IL_0025:  stloc.2
      IL_0026:  ldloc.1
      IL_0027:  ldloc.2
      IL_0028:  bne.un.s   IL_002c

      .line 100001,100001 : 0,0 ''
      IL_002a:  ldc.i4.0
      IL_002b:  ret

      .line 100001,100001 : 0,0 ''
      IL_002c:  ldloc.1
      IL_002d:  ldloc.2
      IL_002e:  sub
      IL_002f:  ret

      .line 100001,100001 : 0,0 ''
      IL_0030:  ldc.i4.1
      IL_0031:  ret

      .line 100001,100001 : 0,0 ''
      IL_0032:  ldarg.1
      IL_0033:  unbox.any  SteppingMatch06/Discr
      IL_0038:  ldnull
      IL_0039:  cgt.un
      IL_003b:  brfalse.s  IL_003f

      .line 100001,100001 : 0,0 ''
      IL_003d:  ldc.i4.m1
      IL_003e:  ret

      .line 100001,100001 : 0,0 ''
      IL_003f:  ldc.i4.0
      IL_0040:  ret
    } // end of method Discr::CompareTo

    .method public hidebysig virtual final 
            instance int32  GetHashCode(class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       17 (0x11)
      .maxstack  3
      .locals init ([0] int32 V_0)
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse.s  IL_000f

      .line 100001,100001 : 0,0 ''
      IL_0006:  ldc.i4.0
      IL_0007:  stloc.0
      IL_0008:  ldarg.0
      IL_0009:  ldfld      int32 SteppingMatch06/Discr::_tag
      IL_000e:  ret

      .line 100001,100001 : 0,0 ''
      IL_000f:  ldc.i4.0
      IL_0010:  ret
    } // end of method Discr::GetHashCode

    .method public hidebysig virtual final 
            instance int32  GetHashCode() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       12 (0xc)
      .maxstack  8
      .line 4,4 : 6,11 ''
      IL_0000:  ldarg.0
      IL_0001:  call       class [mscorlib]System.Collections.IEqualityComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericEqualityComparer()
      IL_0006:  callvirt   instance int32 SteppingMatch06/Discr::GetHashCode(class [mscorlib]System.Collections.IEqualityComparer)
      IL_000b:  ret
    } // end of method Discr::GetHashCode

    .method public hidebysig virtual final 
            instance bool  Equals(object obj,
                                  class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       47 (0x2f)
      .maxstack  4
      .locals init ([0] class SteppingMatch06/Discr V_0,
               [1] class SteppingMatch06/Discr V_1,
               [2] int32 V_2,
               [3] int32 V_3)
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse.s  IL_0027

      .line 100001,100001 : 0,0 ''
      IL_0006:  ldarg.1
      IL_0007:  isinst     SteppingMatch06/Discr
      IL_000c:  stloc.0
      IL_000d:  ldloc.0
      IL_000e:  brfalse.s  IL_0025

      .line 100001,100001 : 0,0 ''
      IL_0010:  ldloc.0
      IL_0011:  stloc.1
      IL_0012:  ldarg.0
      IL_0013:  ldfld      int32 SteppingMatch06/Discr::_tag
      IL_0018:  stloc.2
      IL_0019:  ldloc.1
      IL_001a:  ldfld      int32 SteppingMatch06/Discr::_tag
      IL_001f:  stloc.3
      IL_0020:  ldloc.2
      IL_0021:  ldloc.3
      IL_0022:  ceq
      IL_0024:  ret

      .line 100001,100001 : 0,0 ''
      IL_0025:  ldc.i4.0
      IL_0026:  ret

      .line 100001,100001 : 0,0 ''
      IL_0027:  ldarg.1
      IL_0028:  ldnull
      IL_0029:  cgt.un
      IL_002b:  ldc.i4.0
      IL_002c:  ceq
      IL_002e:  ret
    } // end of method Discr::Equals

    .method public hidebysig virtual final 
            instance bool  Equals(class SteppingMatch06/Discr obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       41 (0x29)
      .maxstack  4
      .locals init ([0] int32 V_0,
               [1] int32 V_1)
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse.s  IL_0021

      .line 100001,100001 : 0,0 ''
      IL_0006:  ldarg.1
      IL_0007:  ldnull
      IL_0008:  cgt.un
      IL_000a:  brfalse.s  IL_001f

      .line 100001,100001 : 0,0 ''
      IL_000c:  ldarg.0
      IL_000d:  ldfld      int32 SteppingMatch06/Discr::_tag
      IL_0012:  stloc.0
      IL_0013:  ldarg.1
      IL_0014:  ldfld      int32 SteppingMatch06/Discr::_tag
      IL_0019:  stloc.1
      IL_001a:  ldloc.0
      IL_001b:  ldloc.1
      IL_001c:  ceq
      IL_001e:  ret

      .line 100001,100001 : 0,0 ''
      IL_001f:  ldc.i4.0
      IL_0020:  ret

      .line 100001,100001 : 0,0 ''
      IL_0021:  ldarg.1
      IL_0022:  ldnull
      IL_0023:  cgt.un
      IL_0025:  ldc.i4.0
      IL_0026:  ceq
      IL_0028:  ret
    } // end of method Discr::Equals

    .method public hidebysig virtual final 
            instance bool  Equals(object obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       20 (0x14)
      .maxstack  4
      .locals init ([0] class SteppingMatch06/Discr V_0)
      .line 4,4 : 6,11 ''
      IL_0000:  ldarg.1
      IL_0001:  isinst     SteppingMatch06/Discr
      IL_0006:  stloc.0
      IL_0007:  ldloc.0
      IL_0008:  brfalse.s  IL_0012

      .line 100001,100001 : 0,0 ''
      IL_000a:  ldarg.0
      IL_000b:  ldloc.0
      IL_000c:  callvirt   instance bool SteppingMatch06/Discr::Equals(class SteppingMatch06/Discr)
      IL_0011:  ret

      .line 100001,100001 : 0,0 ''
      IL_0012:  ldc.i4.0
      IL_0013:  ret
    } // end of method Discr::Equals

    .property instance int32 Tag()
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .get instance int32 SteppingMatch06/Discr::get_Tag()
    } // end of property Discr::Tag
    .property class SteppingMatch06/Discr
            CaseA()
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .get class SteppingMatch06/Discr SteppingMatch06/Discr::get_CaseA()
    } // end of property Discr::CaseA
    .property instance bool IsCaseA()
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .get instance bool SteppingMatch06/Discr::get_IsCaseA()
    } // end of property Discr::IsCaseA
    .property class SteppingMatch06/Discr
            CaseB()
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .get class SteppingMatch06/Discr SteppingMatch06/Discr::get_CaseB()
    } // end of property Discr::CaseB
    .property instance bool IsCaseB()
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .get instance bool SteppingMatch06/Discr::get_IsCaseB()
    } // end of property Discr::IsCaseB
  } // end of class Discr

  .method public static void  funcD(class SteppingMatch06/Discr n) cil managed
  {
    // Code size       33 (0x21)
    .maxstack  8
    .line 6,6 : 9,21 ''
    IL_0000:  ldarg.0
    IL_0001:  call       instance int32 SteppingMatch06/Discr::get_Tag()
    IL_0006:  ldc.i4.0
    IL_0007:  bne.un.s   IL_000b

    IL_0009:  br.s       IL_0016

    .line 8,8 : 13,35 ''
    IL_000b:  ldstr      "B"
    IL_0010:  call       void [mscorlib]System.Console::WriteLine(string)
    IL_0015:  ret

    .line 10,10 : 13,35 ''
    IL_0016:  ldstr      "A"
    IL_001b:  call       void [mscorlib]System.Console::WriteLine(string)
    IL_0020:  ret
  } // end of method SteppingMatch06::funcD

} // end of class SteppingMatch06

.class private abstract auto ansi sealed '<StartupCode$SteppingMatch06>'.$SteppingMatch06
       extends [mscorlib]System.Object
{
} // end of class '<StartupCode$SteppingMatch06>'.$SteppingMatch06


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
