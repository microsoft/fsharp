
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
.assembly EqualsOnUnions01
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.EqualsOnUnions01
{
  // Offset: 0x00000000 Length: 0x0000063B
}
.mresource public FSharpOptimizationData.EqualsOnUnions01
{
  // Offset: 0x00000640 Length: 0x000001C7
}
.module EqualsOnUnions01.exe
// MVID: {60B68B7F-BBFB-14A0-A745-03837F8BB660}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x07200000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed EqualsOnUnions01
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class abstract auto autochar serializable nested public beforefieldinit U
         extends [mscorlib]System.Object
         implements class [mscorlib]System.IEquatable`1<class EqualsOnUnions01/U>,
                    [mscorlib]System.Collections.IStructuralEquatable,
                    class [mscorlib]System.IComparable`1<class EqualsOnUnions01/U>,
                    [mscorlib]System.IComparable,
                    [mscorlib]System.Collections.IStructuralComparable
  {
    .custom instance void [mscorlib]System.Diagnostics.DebuggerDisplayAttribute::.ctor(string) = ( 01 00 15 7B 5F 5F 44 65 62 75 67 44 69 73 70 6C   // ...{__DebugDispl
                                                                                                   61 79 28 29 2C 6E 71 7D 00 00 )                   // ay(),nq}..
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 01 00 00 00 00 00 ) 
    .class abstract auto ansi sealed nested public Tags
           extends [mscorlib]System.Object
    {
      .field public static literal int32 A = int32(0x00000000)
      .field public static literal int32 B = int32(0x00000001)
    } // end of class Tags

    .class auto ansi serializable nested assembly beforefieldinit specialname _A
           extends EqualsOnUnions01/U
    {
      .custom instance void [mscorlib]System.Diagnostics.DebuggerTypeProxyAttribute::.ctor(class [mscorlib]System.Type) = ( 01 00 24 45 71 75 61 6C 73 4F 6E 55 6E 69 6F 6E   // ..$EqualsOnUnion
                                                                                                                            73 30 31 2B 55 2B 5F 41 40 44 65 62 75 67 54 79   // s01+U+_A@DebugTy
                                                                                                                            70 65 50 72 6F 78 79 00 00 )                      // peProxy..
      .custom instance void [mscorlib]System.Diagnostics.DebuggerDisplayAttribute::.ctor(string) = ( 01 00 15 7B 5F 5F 44 65 62 75 67 44 69 73 70 6C   // ...{__DebugDispl
                                                                                                     61 79 28 29 2C 6E 71 7D 00 00 )                   // ay(),nq}..
      .method assembly specialname rtspecialname 
              instance void  .ctor() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       7 (0x7)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void EqualsOnUnions01/U::.ctor()
        IL_0006:  ret
      } // end of method _A::.ctor

    } // end of class _A

    .class auto ansi serializable nested public beforefieldinit specialname B
           extends EqualsOnUnions01/U
    {
      .custom instance void [mscorlib]System.Diagnostics.DebuggerTypeProxyAttribute::.ctor(class [mscorlib]System.Type) = ( 01 00 23 45 71 75 61 6C 73 4F 6E 55 6E 69 6F 6E   // ..#EqualsOnUnion
                                                                                                                            73 30 31 2B 55 2B 42 40 44 65 62 75 67 54 79 70   // s01+U+B@DebugTyp
                                                                                                                            65 50 72 6F 78 79 00 00 )                         // eProxy..
      .custom instance void [mscorlib]System.Diagnostics.DebuggerDisplayAttribute::.ctor(string) = ( 01 00 15 7B 5F 5F 44 65 62 75 67 44 69 73 70 6C   // ...{__DebugDispl
                                                                                                     61 79 28 29 2C 6E 71 7D 00 00 )                   // ay(),nq}..
      .field assembly initonly int32 item
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .method assembly specialname rtspecialname 
              instance void  .ctor(int32 item) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       14 (0xe)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void EqualsOnUnions01/U::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  ldarg.1
        IL_0008:  stfld      int32 EqualsOnUnions01/U/B::item
        IL_000d:  ret
      } // end of method B::.ctor

      .method public hidebysig instance int32 
              get_Item() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       7 (0x7)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldfld      int32 EqualsOnUnions01/U/B::item
        IL_0006:  ret
      } // end of method B::get_Item

      .property instance int32 Item()
      {
        .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                    int32,
                                                                                                    int32) = ( 01 00 04 00 00 00 01 00 00 00 00 00 00 00 00 00 ) 
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        .get instance int32 EqualsOnUnions01/U/B::get_Item()
      } // end of property B::Item
    } // end of class B

    .class auto ansi nested assembly beforefieldinit specialname _A@DebugTypeProxy
           extends [mscorlib]System.Object
    {
      .field assembly class EqualsOnUnions01/U/_A _obj
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .method public specialname rtspecialname 
              instance void  .ctor(class EqualsOnUnions01/U/_A obj) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       14 (0xe)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  ldarg.1
        IL_0008:  stfld      class EqualsOnUnions01/U/_A EqualsOnUnions01/U/_A@DebugTypeProxy::_obj
        IL_000d:  ret
      } // end of method _A@DebugTypeProxy::.ctor

    } // end of class _A@DebugTypeProxy

    .class auto ansi nested assembly beforefieldinit specialname B@DebugTypeProxy
           extends [mscorlib]System.Object
    {
      .field assembly class EqualsOnUnions01/U/B _obj
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .method public specialname rtspecialname 
              instance void  .ctor(class EqualsOnUnions01/U/B obj) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       14 (0xe)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  ldarg.1
        IL_0008:  stfld      class EqualsOnUnions01/U/B EqualsOnUnions01/U/B@DebugTypeProxy::_obj
        IL_000d:  ret
      } // end of method B@DebugTypeProxy::.ctor

      .method public hidebysig instance int32 
              get_Item() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       12 (0xc)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldfld      class EqualsOnUnions01/U/B EqualsOnUnions01/U/B@DebugTypeProxy::_obj
        IL_0006:  ldfld      int32 EqualsOnUnions01/U/B::item
        IL_000b:  ret
      } // end of method B@DebugTypeProxy::get_Item

      .property instance int32 Item()
      {
        .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                    int32,
                                                                                                    int32) = ( 01 00 04 00 00 00 01 00 00 00 00 00 00 00 00 00 ) 
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        .get instance int32 EqualsOnUnions01/U/B@DebugTypeProxy::get_Item()
      } // end of property B@DebugTypeProxy::Item
    } // end of class B@DebugTypeProxy

    .field static assembly initonly class EqualsOnUnions01/U _unique_A
    .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
    .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
    .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
    .method private specialname rtspecialname static 
            void  .cctor() cil managed
    {
      // Code size       11 (0xb)
      .maxstack  8
      IL_0000:  newobj     instance void EqualsOnUnions01/U/_A::.ctor()
      IL_0005:  stsfld     class EqualsOnUnions01/U EqualsOnUnions01/U::_unique_A
      IL_000a:  ret
    } // end of method U::.cctor

    .method assembly specialname rtspecialname 
            instance void  .ctor() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       7 (0x7)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
      IL_0006:  ret
    } // end of method U::.ctor

    .method public static class EqualsOnUnions01/U 
            get_A() cil managed
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                  int32) = ( 01 00 08 00 00 00 00 00 00 00 00 00 ) 
      // Code size       6 (0x6)
      .maxstack  8
      IL_0000:  ldsfld     class EqualsOnUnions01/U EqualsOnUnions01/U::_unique_A
      IL_0005:  ret
    } // end of method U::get_A

    .method public hidebysig instance bool 
            get_IsA() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       10 (0xa)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  isinst     EqualsOnUnions01/U/_A
      IL_0006:  ldnull
      IL_0007:  cgt.un
      IL_0009:  ret
    } // end of method U::get_IsA

    .method public static class EqualsOnUnions01/U 
            NewB(int32 item) cil managed
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                  int32) = ( 01 00 08 00 00 00 01 00 00 00 00 00 ) 
      // Code size       7 (0x7)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  newobj     instance void EqualsOnUnions01/U/B::.ctor(int32)
      IL_0006:  ret
    } // end of method U::NewB

    .method public hidebysig instance bool 
            get_IsB() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       10 (0xa)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  isinst     EqualsOnUnions01/U/B
      IL_0006:  ldnull
      IL_0007:  cgt.un
      IL_0009:  ret
    } // end of method U::get_IsB

    .method public hidebysig instance int32 
            get_Tag() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       13 (0xd)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  isinst     EqualsOnUnions01/U/B
      IL_0006:  brfalse.s  IL_000b

      IL_0008:  ldc.i4.1
      IL_0009:  br.s       IL_000c

      IL_000b:  ldc.i4.0
      IL_000c:  ret
    } // end of method U::get_Tag

    .method assembly hidebysig specialname 
            instance object  __DebugDisplay() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       22 (0x16)
      .maxstack  8
      IL_0000:  ldstr      "%+0.8A"
      IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class EqualsOnUnions01/U,string>,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string,string>::.ctor(string)
      IL_000a:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatToString<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class EqualsOnUnions01/U,string>>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string>)
      IL_000f:  ldarg.0
      IL_0010:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class EqualsOnUnions01/U,string>::Invoke(!0)
      IL_0015:  ret
    } // end of method U::__DebugDisplay

    .method public strict virtual instance string 
            ToString() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       22 (0x16)
      .maxstack  8
      IL_0000:  ldstr      "%+A"
      IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class EqualsOnUnions01/U,string>,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string,class EqualsOnUnions01/U>::.ctor(string)
      IL_000a:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatToString<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class EqualsOnUnions01/U,string>>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string>)
      IL_000f:  ldarg.0
      IL_0010:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class EqualsOnUnions01/U,string>::Invoke(!0)
      IL_0015:  ret
    } // end of method U::ToString

    .method public hidebysig virtual final 
            instance int32  CompareTo(class EqualsOnUnions01/U obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       131 (0x83)
      .maxstack  4
      .locals init ([0] int32 V_0,
               [1] class EqualsOnUnions01/U V_1,
               [2] int32 V_2,
               [3] class EqualsOnUnions01/U V_3,
               [4] class EqualsOnUnions01/U/B V_4,
               [5] class EqualsOnUnions01/U/B V_5,
               [6] class [mscorlib]System.Collections.IComparer V_6,
               [7] int32 V_7,
               [8] int32 V_8)
      .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
      .line 100001,100001 : 0,0 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\Misc\\EqualsOnUnions01.fs'
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse    IL_0079

      .line 100001,100001 : 0,0 ''
      IL_0009:  ldarg.1
      IL_000a:  ldnull
      IL_000b:  cgt.un
      IL_000d:  brfalse.s  IL_0077

      .line 100001,100001 : 0,0 ''
      IL_000f:  ldarg.0
      IL_0010:  stloc.1
      IL_0011:  ldloc.1
      IL_0012:  isinst     EqualsOnUnions01/U/B
      IL_0017:  brfalse.s  IL_001c

      IL_0019:  ldc.i4.1
      IL_001a:  br.s       IL_001d

      IL_001c:  ldc.i4.0
      IL_001d:  stloc.0
      IL_001e:  ldarg.1
      IL_001f:  stloc.3
      IL_0020:  ldloc.3
      IL_0021:  isinst     EqualsOnUnions01/U/B
      IL_0026:  brfalse.s  IL_002b

      IL_0028:  ldc.i4.1
      IL_0029:  br.s       IL_002c

      IL_002b:  ldc.i4.0
      IL_002c:  stloc.2
      IL_002d:  ldloc.0
      IL_002e:  ldloc.2
      IL_002f:  bne.un.s   IL_0073

      .line 100001,100001 : 0,0 ''
      IL_0031:  ldarg.0
      IL_0032:  isinst     EqualsOnUnions01/U/B
      IL_0037:  brfalse.s  IL_0071

      .line 100001,100001 : 0,0 ''
      IL_0039:  ldarg.0
      IL_003a:  castclass  EqualsOnUnions01/U/B
      IL_003f:  stloc.s    V_4
      IL_0041:  ldarg.1
      IL_0042:  castclass  EqualsOnUnions01/U/B
      IL_0047:  stloc.s    V_5
      IL_0049:  call       class [mscorlib]System.Collections.IComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericComparer()
      IL_004e:  stloc.s    V_6
      IL_0050:  ldloc.s    V_4
      IL_0052:  ldfld      int32 EqualsOnUnions01/U/B::item
      IL_0057:  stloc.s    V_7
      IL_0059:  ldloc.s    V_5
      IL_005b:  ldfld      int32 EqualsOnUnions01/U/B::item
      IL_0060:  stloc.s    V_8
      IL_0062:  ldloc.s    V_7
      IL_0064:  ldloc.s    V_8
      IL_0066:  bge.s      IL_006a

      .line 100001,100001 : 0,0 ''
      IL_0068:  ldc.i4.m1
      IL_0069:  ret

      .line 100001,100001 : 0,0 ''
      IL_006a:  ldloc.s    V_7
      IL_006c:  ldloc.s    V_8
      IL_006e:  cgt
      IL_0070:  ret

      .line 100001,100001 : 0,0 ''
      IL_0071:  ldc.i4.0
      IL_0072:  ret

      .line 100001,100001 : 0,0 ''
      IL_0073:  ldloc.0
      IL_0074:  ldloc.2
      IL_0075:  sub
      IL_0076:  ret

      .line 100001,100001 : 0,0 ''
      IL_0077:  ldc.i4.1
      IL_0078:  ret

      .line 100001,100001 : 0,0 ''
      IL_0079:  ldarg.1
      IL_007a:  ldnull
      IL_007b:  cgt.un
      IL_007d:  brfalse.s  IL_0081

      .line 100001,100001 : 0,0 ''
      IL_007f:  ldc.i4.m1
      IL_0080:  ret

      .line 100001,100001 : 0,0 ''
      IL_0081:  ldc.i4.0
      IL_0082:  ret
    } // end of method U::CompareTo

    .method public hidebysig virtual final 
            instance int32  CompareTo(object obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       13 (0xd)
      .maxstack  8
      .line 6,6 : 6,7 ''
      IL_0000:  ldarg.0
      IL_0001:  ldarg.1
      IL_0002:  unbox.any  EqualsOnUnions01/U
      IL_0007:  callvirt   instance int32 EqualsOnUnions01/U::CompareTo(class EqualsOnUnions01/U)
      IL_000c:  ret
    } // end of method U::CompareTo

    .method public hidebysig virtual final 
            instance int32  CompareTo(object obj,
                                      class [mscorlib]System.Collections.IComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       146 (0x92)
      .maxstack  4
      .locals init ([0] class EqualsOnUnions01/U V_0,
               [1] int32 V_1,
               [2] class EqualsOnUnions01/U V_2,
               [3] int32 V_3,
               [4] class EqualsOnUnions01/U V_4,
               [5] class EqualsOnUnions01/U/B V_5,
               [6] class EqualsOnUnions01/U/B V_6,
               [7] class [mscorlib]System.Collections.IComparer V_7,
               [8] int32 V_8,
               [9] int32 V_9)
      .line 6,6 : 6,7 ''
      IL_0000:  ldarg.1
      IL_0001:  unbox.any  EqualsOnUnions01/U
      IL_0006:  stloc.0
      IL_0007:  ldarg.0
      IL_0008:  ldnull
      IL_0009:  cgt.un
      IL_000b:  brfalse    IL_0083

      .line 100001,100001 : 0,0 ''
      IL_0010:  ldarg.1
      IL_0011:  unbox.any  EqualsOnUnions01/U
      IL_0016:  ldnull
      IL_0017:  cgt.un
      IL_0019:  brfalse.s  IL_0081

      .line 100001,100001 : 0,0 ''
      IL_001b:  ldarg.0
      IL_001c:  stloc.2
      IL_001d:  ldloc.2
      IL_001e:  isinst     EqualsOnUnions01/U/B
      IL_0023:  brfalse.s  IL_0028

      IL_0025:  ldc.i4.1
      IL_0026:  br.s       IL_0029

      IL_0028:  ldc.i4.0
      IL_0029:  stloc.1
      IL_002a:  ldloc.0
      IL_002b:  stloc.s    V_4
      IL_002d:  ldloc.s    V_4
      IL_002f:  isinst     EqualsOnUnions01/U/B
      IL_0034:  brfalse.s  IL_0039

      IL_0036:  ldc.i4.1
      IL_0037:  br.s       IL_003a

      IL_0039:  ldc.i4.0
      IL_003a:  stloc.3
      IL_003b:  ldloc.1
      IL_003c:  ldloc.3
      IL_003d:  bne.un.s   IL_007d

      .line 100001,100001 : 0,0 ''
      IL_003f:  ldarg.0
      IL_0040:  isinst     EqualsOnUnions01/U/B
      IL_0045:  brfalse.s  IL_007b

      .line 100001,100001 : 0,0 ''
      IL_0047:  ldarg.0
      IL_0048:  castclass  EqualsOnUnions01/U/B
      IL_004d:  stloc.s    V_5
      IL_004f:  ldloc.0
      IL_0050:  castclass  EqualsOnUnions01/U/B
      IL_0055:  stloc.s    V_6
      IL_0057:  ldarg.2
      IL_0058:  stloc.s    V_7
      IL_005a:  ldloc.s    V_5
      IL_005c:  ldfld      int32 EqualsOnUnions01/U/B::item
      IL_0061:  stloc.s    V_8
      IL_0063:  ldloc.s    V_6
      IL_0065:  ldfld      int32 EqualsOnUnions01/U/B::item
      IL_006a:  stloc.s    V_9
      IL_006c:  ldloc.s    V_8
      IL_006e:  ldloc.s    V_9
      IL_0070:  bge.s      IL_0074

      .line 100001,100001 : 0,0 ''
      IL_0072:  ldc.i4.m1
      IL_0073:  ret

      .line 100001,100001 : 0,0 ''
      IL_0074:  ldloc.s    V_8
      IL_0076:  ldloc.s    V_9
      IL_0078:  cgt
      IL_007a:  ret

      .line 100001,100001 : 0,0 ''
      IL_007b:  ldc.i4.0
      IL_007c:  ret

      .line 100001,100001 : 0,0 ''
      IL_007d:  ldloc.1
      IL_007e:  ldloc.3
      IL_007f:  sub
      IL_0080:  ret

      .line 100001,100001 : 0,0 ''
      IL_0081:  ldc.i4.1
      IL_0082:  ret

      .line 100001,100001 : 0,0 ''
      IL_0083:  ldarg.1
      IL_0084:  unbox.any  EqualsOnUnions01/U
      IL_0089:  ldnull
      IL_008a:  cgt.un
      IL_008c:  brfalse.s  IL_0090

      .line 100001,100001 : 0,0 ''
      IL_008e:  ldc.i4.m1
      IL_008f:  ret

      .line 100001,100001 : 0,0 ''
      IL_0090:  ldc.i4.0
      IL_0091:  ret
    } // end of method U::CompareTo

    .method public hidebysig virtual final 
            instance int32  GetHashCode(class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       67 (0x43)
      .maxstack  7
      .locals init ([0] int32 V_0,
               [1] class EqualsOnUnions01/U/B V_1,
               [2] class [mscorlib]System.Collections.IEqualityComparer V_2,
               [3] class EqualsOnUnions01/U V_3)
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse.s  IL_0041

      .line 100001,100001 : 0,0 ''
      IL_0006:  ldc.i4.0
      IL_0007:  stloc.0
      IL_0008:  ldarg.0
      IL_0009:  isinst     EqualsOnUnions01/U/B
      IL_000e:  brfalse.s  IL_0032

      .line 100001,100001 : 0,0 ''
      IL_0010:  ldarg.0
      IL_0011:  castclass  EqualsOnUnions01/U/B
      IL_0016:  stloc.1
      IL_0017:  ldc.i4.1
      IL_0018:  stloc.0
      IL_0019:  ldc.i4     0x9e3779b9
      IL_001e:  ldarg.1
      IL_001f:  stloc.2
      IL_0020:  ldloc.1
      IL_0021:  ldfld      int32 EqualsOnUnions01/U/B::item
      IL_0026:  ldloc.0
      IL_0027:  ldc.i4.6
      IL_0028:  shl
      IL_0029:  ldloc.0
      IL_002a:  ldc.i4.2
      IL_002b:  shr
      IL_002c:  add
      IL_002d:  add
      IL_002e:  add
      IL_002f:  stloc.0
      IL_0030:  ldloc.0
      IL_0031:  ret

      .line 100001,100001 : 0,0 ''
      IL_0032:  ldarg.0
      IL_0033:  stloc.3
      IL_0034:  ldloc.3
      IL_0035:  isinst     EqualsOnUnions01/U/B
      IL_003a:  brfalse.s  IL_003f

      IL_003c:  ldc.i4.1
      IL_003d:  br.s       IL_0040

      IL_003f:  ldc.i4.0
      IL_0040:  ret

      .line 100001,100001 : 0,0 ''
      IL_0041:  ldc.i4.0
      IL_0042:  ret
    } // end of method U::GetHashCode

    .method public hidebysig virtual final 
            instance int32  GetHashCode() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       12 (0xc)
      .maxstack  8
      .line 6,6 : 6,7 ''
      IL_0000:  ldarg.0
      IL_0001:  call       class [mscorlib]System.Collections.IEqualityComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericEqualityComparer()
      IL_0006:  callvirt   instance int32 EqualsOnUnions01/U::GetHashCode(class [mscorlib]System.Collections.IEqualityComparer)
      IL_000b:  ret
    } // end of method U::GetHashCode

    .method public hidebysig virtual final 
            instance bool  Equals(object obj,
                                  class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       114 (0x72)
      .maxstack  4
      .locals init ([0] class EqualsOnUnions01/U V_0,
               [1] class EqualsOnUnions01/U V_1,
               [2] int32 V_2,
               [3] class EqualsOnUnions01/U V_3,
               [4] int32 V_4,
               [5] class EqualsOnUnions01/U V_5,
               [6] class EqualsOnUnions01/U/B V_6,
               [7] class EqualsOnUnions01/U/B V_7,
               [8] class [mscorlib]System.Collections.IEqualityComparer V_8)
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse.s  IL_006a

      .line 100001,100001 : 0,0 ''
      IL_0006:  ldarg.1
      IL_0007:  isinst     EqualsOnUnions01/U
      IL_000c:  stloc.0
      IL_000d:  ldloc.0
      IL_000e:  brfalse.s  IL_0068

      .line 100001,100001 : 0,0 ''
      IL_0010:  ldloc.0
      IL_0011:  stloc.1
      IL_0012:  ldarg.0
      IL_0013:  stloc.3
      IL_0014:  ldloc.3
      IL_0015:  isinst     EqualsOnUnions01/U/B
      IL_001a:  brfalse.s  IL_001f

      IL_001c:  ldc.i4.1
      IL_001d:  br.s       IL_0020

      IL_001f:  ldc.i4.0
      IL_0020:  stloc.2
      IL_0021:  ldloc.1
      IL_0022:  stloc.s    V_5
      IL_0024:  ldloc.s    V_5
      IL_0026:  isinst     EqualsOnUnions01/U/B
      IL_002b:  brfalse.s  IL_0030

      IL_002d:  ldc.i4.1
      IL_002e:  br.s       IL_0031

      IL_0030:  ldc.i4.0
      IL_0031:  stloc.s    V_4
      IL_0033:  ldloc.2
      IL_0034:  ldloc.s    V_4
      IL_0036:  bne.un.s   IL_0066

      .line 100001,100001 : 0,0 ''
      IL_0038:  ldarg.0
      IL_0039:  isinst     EqualsOnUnions01/U/B
      IL_003e:  brfalse.s  IL_0064

      .line 100001,100001 : 0,0 ''
      IL_0040:  ldarg.0
      IL_0041:  castclass  EqualsOnUnions01/U/B
      IL_0046:  stloc.s    V_6
      IL_0048:  ldloc.1
      IL_0049:  castclass  EqualsOnUnions01/U/B
      IL_004e:  stloc.s    V_7
      IL_0050:  ldarg.2
      IL_0051:  stloc.s    V_8
      IL_0053:  ldloc.s    V_6
      IL_0055:  ldfld      int32 EqualsOnUnions01/U/B::item
      IL_005a:  ldloc.s    V_7
      IL_005c:  ldfld      int32 EqualsOnUnions01/U/B::item
      IL_0061:  ceq
      IL_0063:  ret

      .line 100001,100001 : 0,0 ''
      IL_0064:  ldc.i4.1
      IL_0065:  ret

      .line 100001,100001 : 0,0 ''
      IL_0066:  ldc.i4.0
      IL_0067:  ret

      .line 100001,100001 : 0,0 ''
      IL_0068:  ldc.i4.0
      IL_0069:  ret

      .line 100001,100001 : 0,0 ''
      IL_006a:  ldarg.1
      IL_006b:  ldnull
      IL_006c:  cgt.un
      IL_006e:  ldc.i4.0
      IL_006f:  ceq
      IL_0071:  ret
    } // end of method U::Equals

    .method public hidebysig virtual final 
            instance bool  Equals(class EqualsOnUnions01/U obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       101 (0x65)
      .maxstack  4
      .locals init ([0] int32 V_0,
               [1] class EqualsOnUnions01/U V_1,
               [2] int32 V_2,
               [3] class EqualsOnUnions01/U V_3,
               [4] class EqualsOnUnions01/U/B V_4,
               [5] class EqualsOnUnions01/U/B V_5)
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse.s  IL_005d

      .line 100001,100001 : 0,0 ''
      IL_0006:  ldarg.1
      IL_0007:  ldnull
      IL_0008:  cgt.un
      IL_000a:  brfalse.s  IL_005b

      .line 100001,100001 : 0,0 ''
      IL_000c:  ldarg.0
      IL_000d:  stloc.1
      IL_000e:  ldloc.1
      IL_000f:  isinst     EqualsOnUnions01/U/B
      IL_0014:  brfalse.s  IL_0019

      IL_0016:  ldc.i4.1
      IL_0017:  br.s       IL_001a

      IL_0019:  ldc.i4.0
      IL_001a:  stloc.0
      IL_001b:  ldarg.1
      IL_001c:  stloc.3
      IL_001d:  ldloc.3
      IL_001e:  isinst     EqualsOnUnions01/U/B
      IL_0023:  brfalse.s  IL_0028

      IL_0025:  ldc.i4.1
      IL_0026:  br.s       IL_0029

      IL_0028:  ldc.i4.0
      IL_0029:  stloc.2
      IL_002a:  ldloc.0
      IL_002b:  ldloc.2
      IL_002c:  bne.un.s   IL_0059

      .line 100001,100001 : 0,0 ''
      IL_002e:  ldarg.0
      IL_002f:  isinst     EqualsOnUnions01/U/B
      IL_0034:  brfalse.s  IL_0057

      .line 100001,100001 : 0,0 ''
      IL_0036:  ldarg.0
      IL_0037:  castclass  EqualsOnUnions01/U/B
      IL_003c:  stloc.s    V_4
      IL_003e:  ldarg.1
      IL_003f:  castclass  EqualsOnUnions01/U/B
      IL_0044:  stloc.s    V_5
      IL_0046:  ldloc.s    V_4
      IL_0048:  ldfld      int32 EqualsOnUnions01/U/B::item
      IL_004d:  ldloc.s    V_5
      IL_004f:  ldfld      int32 EqualsOnUnions01/U/B::item
      IL_0054:  ceq
      IL_0056:  ret

      .line 100001,100001 : 0,0 ''
      IL_0057:  ldc.i4.1
      IL_0058:  ret

      .line 100001,100001 : 0,0 ''
      IL_0059:  ldc.i4.0
      IL_005a:  ret

      .line 100001,100001 : 0,0 ''
      IL_005b:  ldc.i4.0
      IL_005c:  ret

      .line 100001,100001 : 0,0 ''
      IL_005d:  ldarg.1
      IL_005e:  ldnull
      IL_005f:  cgt.un
      IL_0061:  ldc.i4.0
      IL_0062:  ceq
      IL_0064:  ret
    } // end of method U::Equals

    .method public hidebysig virtual final 
            instance bool  Equals(object obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       20 (0x14)
      .maxstack  4
      .locals init ([0] class EqualsOnUnions01/U V_0)
      .line 6,6 : 6,7 ''
      IL_0000:  ldarg.1
      IL_0001:  isinst     EqualsOnUnions01/U
      IL_0006:  stloc.0
      IL_0007:  ldloc.0
      IL_0008:  brfalse.s  IL_0012

      .line 100001,100001 : 0,0 ''
      IL_000a:  ldarg.0
      IL_000b:  ldloc.0
      IL_000c:  callvirt   instance bool EqualsOnUnions01/U::Equals(class EqualsOnUnions01/U)
      IL_0011:  ret

      .line 100001,100001 : 0,0 ''
      IL_0012:  ldc.i4.0
      IL_0013:  ret
    } // end of method U::Equals

    .property instance int32 Tag()
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .get instance int32 EqualsOnUnions01/U::get_Tag()
    } // end of property U::Tag
    .property class EqualsOnUnions01/U A()
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .get class EqualsOnUnions01/U EqualsOnUnions01/U::get_A()
    } // end of property U::A
    .property instance bool IsA()
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .get instance bool EqualsOnUnions01/U::get_IsA()
    } // end of property U::IsA
    .property instance bool IsB()
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .get instance bool EqualsOnUnions01/U::get_IsB()
    } // end of property U::IsB
  } // end of class U

} // end of class EqualsOnUnions01

.class private abstract auto ansi sealed '<StartupCode$EqualsOnUnions01>'.$EqualsOnUnions01
       extends [mscorlib]System.Object
{
  .method public static void  main@() cil managed
  {
    .entrypoint
    // Code size       1 (0x1)
    .maxstack  8
    IL_0000:  ret
  } // end of method $EqualsOnUnions01::main@

} // end of class '<StartupCode$EqualsOnUnions01>'.$EqualsOnUnions01


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
