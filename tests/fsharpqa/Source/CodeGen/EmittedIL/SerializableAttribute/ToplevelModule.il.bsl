
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
.assembly TopLevelModule
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.TopLevelModule
{
  // Offset: 0x00000000 Length: 0x0000113D
}
.mresource public FSharpSignatureDataB.TopLevelModule
{
  // Offset: 0x00001148 Length: 0x00000004
}
.mresource public FSharpOptimizationData.TopLevelModule
{
  // Offset: 0x00001150 Length: 0x000003FD
}
.module TopLevelModule.dll
// MVID: {60C0F26A-37F5-C118-A745-03836AF2C060}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x04E10000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed ABC
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class auto autochar serializable sealed nested public beforefieldinit Expr
         extends [mscorlib]System.Object
         implements class [mscorlib]System.IEquatable`1<class ABC/Expr>,
                    [mscorlib]System.Collections.IStructuralEquatable,
                    class [mscorlib]System.IComparable`1<class ABC/Expr>,
                    [mscorlib]System.IComparable,
                    [mscorlib]System.Collections.IStructuralComparable
  {
    .custom instance void [mscorlib]System.Diagnostics.DebuggerDisplayAttribute::.ctor(string) = ( 01 00 15 7B 5F 5F 44 65 62 75 67 44 69 73 70 6C   // ...{__DebugDispl
                                                                                                   61 79 28 29 2C 6E 71 7D 00 00 )                   // ay(),nq}..
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 01 00 00 00 00 00 ) 
    .field assembly initonly int32 item
    .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
    .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
    .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
    .method public static class ABC/Expr 
            NewNum(int32 item) cil managed
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                  int32) = ( 01 00 08 00 00 00 00 00 00 00 00 00 ) 
      // Code size       7 (0x7)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  newobj     instance void ABC/Expr::.ctor(int32)
      IL_0006:  ret
    } // end of method Expr::NewNum

    .method assembly specialname rtspecialname 
            instance void  .ctor(int32 item) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       14 (0xe)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
      IL_0006:  ldarg.0
      IL_0007:  ldarg.1
      IL_0008:  stfld      int32 ABC/Expr::item
      IL_000d:  ret
    } // end of method Expr::.ctor

    .method public hidebysig instance int32 
            get_Item() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       7 (0x7)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  ldfld      int32 ABC/Expr::item
      IL_0006:  ret
    } // end of method Expr::get_Item

    .method public hidebysig instance int32 
            get_Tag() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       4 (0x4)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  pop
      IL_0002:  ldc.i4.0
      IL_0003:  ret
    } // end of method Expr::get_Tag

    .method assembly hidebysig specialname 
            instance object  __DebugDisplay() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       22 (0x16)
      .maxstack  8
      IL_0000:  ldstr      "%+0.8A"
      IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class ABC/Expr,string>,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string,string>::.ctor(string)
      IL_000a:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatToString<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class ABC/Expr,string>>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string>)
      IL_000f:  ldarg.0
      IL_0010:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class ABC/Expr,string>::Invoke(!0)
      IL_0015:  ret
    } // end of method Expr::__DebugDisplay

    .method public strict virtual instance string 
            ToString() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       22 (0x16)
      .maxstack  8
      IL_0000:  ldstr      "%+A"
      IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class ABC/Expr,string>,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string,class ABC/Expr>::.ctor(string)
      IL_000a:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatToString<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class ABC/Expr,string>>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string>)
      IL_000f:  ldarg.0
      IL_0010:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class ABC/Expr,string>::Invoke(!0)
      IL_0015:  ret
    } // end of method Expr::ToString

    .method public hidebysig virtual final 
            instance int32  CompareTo(class ABC/Expr obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       64 (0x40)
      .maxstack  4
      .locals init ([0] class ABC/Expr V_0,
               [1] class ABC/Expr V_1,
               [2] class [mscorlib]System.Collections.IComparer V_2,
               [3] int32 V_3,
               [4] int32 V_4)
      .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
      .line 100001,100001 : 0,0 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\SerializableAttribute\\ToplevelModule.fs'
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse.s  IL_0036

      .line 100001,100001 : 0,0 ''
      IL_0006:  ldarg.1
      IL_0007:  ldnull
      IL_0008:  cgt.un
      IL_000a:  brfalse.s  IL_0034

      .line 100001,100001 : 0,0 ''
      IL_000c:  ldarg.0
      IL_000d:  pop
      .line 100001,100001 : 0,0 ''
      IL_000e:  ldarg.0
      IL_000f:  stloc.0
      IL_0010:  ldarg.1
      IL_0011:  stloc.1
      IL_0012:  call       class [mscorlib]System.Collections.IComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericComparer()
      IL_0017:  stloc.2
      IL_0018:  ldloc.0
      IL_0019:  ldfld      int32 ABC/Expr::item
      IL_001e:  stloc.3
      IL_001f:  ldloc.1
      IL_0020:  ldfld      int32 ABC/Expr::item
      IL_0025:  stloc.s    V_4
      IL_0027:  ldloc.3
      IL_0028:  ldloc.s    V_4
      IL_002a:  bge.s      IL_002e

      .line 100001,100001 : 0,0 ''
      IL_002c:  ldc.i4.m1
      IL_002d:  ret

      .line 100001,100001 : 0,0 ''
      IL_002e:  ldloc.3
      IL_002f:  ldloc.s    V_4
      IL_0031:  cgt
      IL_0033:  ret

      .line 100001,100001 : 0,0 ''
      IL_0034:  ldc.i4.1
      IL_0035:  ret

      .line 100001,100001 : 0,0 ''
      IL_0036:  ldarg.1
      IL_0037:  ldnull
      IL_0038:  cgt.un
      IL_003a:  brfalse.s  IL_003e

      .line 100001,100001 : 0,0 ''
      IL_003c:  ldc.i4.m1
      IL_003d:  ret

      .line 100001,100001 : 0,0 ''
      IL_003e:  ldc.i4.0
      IL_003f:  ret
    } // end of method Expr::CompareTo

    .method public hidebysig virtual final 
            instance int32  CompareTo(object obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       13 (0xd)
      .maxstack  8
      .line 6,6 : 14,18 ''
      IL_0000:  ldarg.0
      IL_0001:  ldarg.1
      IL_0002:  unbox.any  ABC/Expr
      IL_0007:  callvirt   instance int32 ABC/Expr::CompareTo(class ABC/Expr)
      IL_000c:  ret
    } // end of method Expr::CompareTo

    .method public hidebysig virtual final 
            instance int32  CompareTo(object obj,
                                      class [mscorlib]System.Collections.IComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       80 (0x50)
      .maxstack  4
      .locals init ([0] class ABC/Expr V_0,
               [1] class ABC/Expr V_1,
               [2] class ABC/Expr V_2,
               [3] class [mscorlib]System.Collections.IComparer V_3,
               [4] int32 V_4,
               [5] int32 V_5)
      .line 6,6 : 14,18 ''
      IL_0000:  ldarg.1
      IL_0001:  unbox.any  ABC/Expr
      IL_0006:  stloc.0
      IL_0007:  ldarg.0
      IL_0008:  ldnull
      IL_0009:  cgt.un
      IL_000b:  brfalse.s  IL_0041

      .line 100001,100001 : 0,0 ''
      IL_000d:  ldarg.1
      IL_000e:  unbox.any  ABC/Expr
      IL_0013:  ldnull
      IL_0014:  cgt.un
      IL_0016:  brfalse.s  IL_003f

      .line 100001,100001 : 0,0 ''
      IL_0018:  ldarg.0
      IL_0019:  pop
      .line 100001,100001 : 0,0 ''
      IL_001a:  ldarg.0
      IL_001b:  stloc.1
      IL_001c:  ldloc.0
      IL_001d:  stloc.2
      IL_001e:  ldarg.2
      IL_001f:  stloc.3
      IL_0020:  ldloc.1
      IL_0021:  ldfld      int32 ABC/Expr::item
      IL_0026:  stloc.s    V_4
      IL_0028:  ldloc.2
      IL_0029:  ldfld      int32 ABC/Expr::item
      IL_002e:  stloc.s    V_5
      IL_0030:  ldloc.s    V_4
      IL_0032:  ldloc.s    V_5
      IL_0034:  bge.s      IL_0038

      .line 100001,100001 : 0,0 ''
      IL_0036:  ldc.i4.m1
      IL_0037:  ret

      .line 100001,100001 : 0,0 ''
      IL_0038:  ldloc.s    V_4
      IL_003a:  ldloc.s    V_5
      IL_003c:  cgt
      IL_003e:  ret

      .line 100001,100001 : 0,0 ''
      IL_003f:  ldc.i4.1
      IL_0040:  ret

      .line 100001,100001 : 0,0 ''
      IL_0041:  ldarg.1
      IL_0042:  unbox.any  ABC/Expr
      IL_0047:  ldnull
      IL_0048:  cgt.un
      IL_004a:  brfalse.s  IL_004e

      .line 100001,100001 : 0,0 ''
      IL_004c:  ldc.i4.m1
      IL_004d:  ret

      .line 100001,100001 : 0,0 ''
      IL_004e:  ldc.i4.0
      IL_004f:  ret
    } // end of method Expr::CompareTo

    .method public hidebysig virtual final 
            instance int32  GetHashCode(class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       41 (0x29)
      .maxstack  7
      .locals init ([0] int32 V_0,
               [1] class ABC/Expr V_1,
               [2] class [mscorlib]System.Collections.IEqualityComparer V_2)
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse.s  IL_0027

      .line 100001,100001 : 0,0 ''
      IL_0006:  ldc.i4.0
      IL_0007:  stloc.0
      IL_0008:  ldarg.0
      IL_0009:  pop
      .line 100001,100001 : 0,0 ''
      IL_000a:  ldarg.0
      IL_000b:  stloc.1
      IL_000c:  ldc.i4.0
      IL_000d:  stloc.0
      IL_000e:  ldc.i4     0x9e3779b9
      IL_0013:  ldarg.1
      IL_0014:  stloc.2
      IL_0015:  ldloc.1
      IL_0016:  ldfld      int32 ABC/Expr::item
      IL_001b:  ldloc.0
      IL_001c:  ldc.i4.6
      IL_001d:  shl
      IL_001e:  ldloc.0
      IL_001f:  ldc.i4.2
      IL_0020:  shr
      IL_0021:  add
      IL_0022:  add
      IL_0023:  add
      IL_0024:  stloc.0
      IL_0025:  ldloc.0
      IL_0026:  ret

      .line 100001,100001 : 0,0 ''
      IL_0027:  ldc.i4.0
      IL_0028:  ret
    } // end of method Expr::GetHashCode

    .method public hidebysig virtual final 
            instance int32  GetHashCode() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       12 (0xc)
      .maxstack  8
      .line 6,6 : 14,18 ''
      IL_0000:  ldarg.0
      IL_0001:  call       class [mscorlib]System.Collections.IEqualityComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericEqualityComparer()
      IL_0006:  callvirt   instance int32 ABC/Expr::GetHashCode(class [mscorlib]System.Collections.IEqualityComparer)
      IL_000b:  ret
    } // end of method Expr::GetHashCode

    .method public hidebysig virtual final 
            instance bool  Equals(object obj,
                                  class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       52 (0x34)
      .maxstack  4
      .locals init ([0] class ABC/Expr V_0,
               [1] class ABC/Expr V_1,
               [2] class ABC/Expr V_2,
               [3] class ABC/Expr V_3,
               [4] class [mscorlib]System.Collections.IEqualityComparer V_4)
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse.s  IL_002c

      .line 100001,100001 : 0,0 ''
      IL_0006:  ldarg.1
      IL_0007:  isinst     ABC/Expr
      IL_000c:  stloc.0
      IL_000d:  ldloc.0
      IL_000e:  brfalse.s  IL_002a

      .line 100001,100001 : 0,0 ''
      IL_0010:  ldloc.0
      IL_0011:  stloc.1
      IL_0012:  ldarg.0
      IL_0013:  pop
      .line 100001,100001 : 0,0 ''
      IL_0014:  ldarg.0
      IL_0015:  stloc.2
      IL_0016:  ldloc.1
      IL_0017:  stloc.3
      IL_0018:  ldarg.2
      IL_0019:  stloc.s    V_4
      IL_001b:  ldloc.2
      IL_001c:  ldfld      int32 ABC/Expr::item
      IL_0021:  ldloc.3
      IL_0022:  ldfld      int32 ABC/Expr::item
      IL_0027:  ceq
      IL_0029:  ret

      .line 100001,100001 : 0,0 ''
      IL_002a:  ldc.i4.0
      IL_002b:  ret

      .line 100001,100001 : 0,0 ''
      IL_002c:  ldarg.1
      IL_002d:  ldnull
      IL_002e:  cgt.un
      IL_0030:  ldc.i4.0
      IL_0031:  ceq
      IL_0033:  ret
    } // end of method Expr::Equals

    .method public hidebysig virtual final 
            instance bool  Equals(class ABC/Expr obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       43 (0x2b)
      .maxstack  4
      .locals init ([0] class ABC/Expr V_0,
               [1] class ABC/Expr V_1)
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse.s  IL_0023

      .line 100001,100001 : 0,0 ''
      IL_0006:  ldarg.1
      IL_0007:  ldnull
      IL_0008:  cgt.un
      IL_000a:  brfalse.s  IL_0021

      .line 100001,100001 : 0,0 ''
      IL_000c:  ldarg.0
      IL_000d:  pop
      .line 100001,100001 : 0,0 ''
      IL_000e:  ldarg.0
      IL_000f:  stloc.0
      IL_0010:  ldarg.1
      IL_0011:  stloc.1
      IL_0012:  ldloc.0
      IL_0013:  ldfld      int32 ABC/Expr::item
      IL_0018:  ldloc.1
      IL_0019:  ldfld      int32 ABC/Expr::item
      IL_001e:  ceq
      IL_0020:  ret

      .line 100001,100001 : 0,0 ''
      IL_0021:  ldc.i4.0
      IL_0022:  ret

      .line 100001,100001 : 0,0 ''
      IL_0023:  ldarg.1
      IL_0024:  ldnull
      IL_0025:  cgt.un
      IL_0027:  ldc.i4.0
      IL_0028:  ceq
      IL_002a:  ret
    } // end of method Expr::Equals

    .method public hidebysig virtual final 
            instance bool  Equals(object obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       20 (0x14)
      .maxstack  4
      .locals init ([0] class ABC/Expr V_0)
      .line 6,6 : 14,18 ''
      IL_0000:  ldarg.1
      IL_0001:  isinst     ABC/Expr
      IL_0006:  stloc.0
      IL_0007:  ldloc.0
      IL_0008:  brfalse.s  IL_0012

      .line 100001,100001 : 0,0 ''
      IL_000a:  ldarg.0
      IL_000b:  ldloc.0
      IL_000c:  callvirt   instance bool ABC/Expr::Equals(class ABC/Expr)
      IL_0011:  ret

      .line 100001,100001 : 0,0 ''
      IL_0012:  ldc.i4.0
      IL_0013:  ret
    } // end of method Expr::Equals

    .property instance int32 Tag()
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .get instance int32 ABC/Expr::get_Tag()
    } // end of property Expr::Tag
    .property instance int32 Item()
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                  int32,
                                                                                                  int32) = ( 01 00 04 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .get instance int32 ABC/Expr::get_Item()
    } // end of property Expr::Item
  } // end of class Expr

  .class auto ansi serializable nested public beforefieldinit MyExn
         extends [mscorlib]System.Exception
         implements [mscorlib]System.Collections.IStructuralEquatable
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 05 00 00 00 00 00 ) 
    .field assembly int32 Data0@
    .method public specialname rtspecialname 
            instance void  .ctor(int32 data0) cil managed
    {
      // Code size       14 (0xe)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  call       instance void [mscorlib]System.Exception::.ctor()
      IL_0006:  ldarg.0
      IL_0007:  ldarg.1
      IL_0008:  stfld      int32 ABC/MyExn::Data0@
      IL_000d:  ret
    } // end of method MyExn::.ctor

    .method public specialname rtspecialname 
            instance void  .ctor() cil managed
    {
      // Code size       7 (0x7)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  call       instance void [mscorlib]System.Exception::.ctor()
      IL_0006:  ret
    } // end of method MyExn::.ctor

    .method family specialname rtspecialname 
            instance void  .ctor(class [mscorlib]System.Runtime.Serialization.SerializationInfo info,
                                 valuetype [mscorlib]System.Runtime.Serialization.StreamingContext context) cil managed
    {
      // Code size       9 (0x9)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  ldarg.1
      IL_0002:  ldarg.2
      IL_0003:  call       instance void [mscorlib]System.Exception::.ctor(class [mscorlib]System.Runtime.Serialization.SerializationInfo,
                                                                           valuetype [mscorlib]System.Runtime.Serialization.StreamingContext)
      IL_0008:  ret
    } // end of method MyExn::.ctor

    .method public hidebysig specialname 
            instance int32  get_Data0() cil managed
    {
      // Code size       7 (0x7)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  ldfld      int32 ABC/MyExn::Data0@
      IL_0006:  ret
    } // end of method MyExn::get_Data0

    .method public hidebysig virtual instance int32 
            GetHashCode(class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       40 (0x28)
      .maxstack  7
      .locals init ([0] int32 V_0,
               [1] class [mscorlib]System.Collections.IEqualityComparer V_1)
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse.s  IL_0026

      .line 100001,100001 : 0,0 ''
      IL_0006:  ldc.i4.0
      IL_0007:  stloc.0
      IL_0008:  ldc.i4     0x9e3779b9
      IL_000d:  ldarg.1
      IL_000e:  stloc.1
      IL_000f:  ldarg.0
      IL_0010:  castclass  ABC/MyExn
      IL_0015:  call       instance int32 ABC/MyExn::get_Data0()
      IL_001a:  ldloc.0
      IL_001b:  ldc.i4.6
      IL_001c:  shl
      IL_001d:  ldloc.0
      IL_001e:  ldc.i4.2
      IL_001f:  shr
      IL_0020:  add
      IL_0021:  add
      IL_0022:  add
      IL_0023:  stloc.0
      IL_0024:  ldloc.0
      IL_0025:  ret

      .line 100001,100001 : 0,0 ''
      IL_0026:  ldc.i4.0
      IL_0027:  ret
    } // end of method MyExn::GetHashCode

    .method public hidebysig virtual instance int32 
            GetHashCode() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       12 (0xc)
      .maxstack  8
      .line 7,7 : 19,24 ''
      IL_0000:  ldarg.0
      IL_0001:  call       class [mscorlib]System.Collections.IEqualityComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericEqualityComparer()
      IL_0006:  callvirt   instance int32 ABC/MyExn::GetHashCode(class [mscorlib]System.Collections.IEqualityComparer)
      IL_000b:  ret
    } // end of method MyExn::GetHashCode

    .method public hidebysig virtual instance bool 
            Equals(object obj,
                   class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       67 (0x43)
      .maxstack  4
      .locals init ([0] class [mscorlib]System.Exception V_0,
               [1] class [mscorlib]System.Exception V_1,
               [2] class [mscorlib]System.Collections.IEqualityComparer V_2)
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse.s  IL_003b

      .line 100001,100001 : 0,0 ''
      IL_0006:  ldarg.1
      IL_0007:  isinst     [mscorlib]System.Exception
      IL_000c:  stloc.0
      IL_000d:  ldloc.0
      IL_000e:  brfalse.s  IL_0039

      .line 100001,100001 : 0,0 ''
      IL_0010:  ldloc.0
      IL_0011:  stloc.1
      IL_0012:  ldloc.0
      IL_0013:  call       bool [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/IntrinsicFunctions::TypeTestGeneric<class ABC/MyExn>(object)
      IL_0018:  brtrue.s   IL_001c

      IL_001a:  br.s       IL_0037

      .line 100001,100001 : 0,0 ''
      IL_001c:  ldarg.2
      IL_001d:  stloc.2
      IL_001e:  ldarg.0
      IL_001f:  castclass  ABC/MyExn
      IL_0024:  call       instance int32 ABC/MyExn::get_Data0()
      IL_0029:  ldloc.1
      IL_002a:  castclass  ABC/MyExn
      IL_002f:  call       instance int32 ABC/MyExn::get_Data0()
      IL_0034:  ceq
      IL_0036:  ret

      .line 100001,100001 : 0,0 ''
      IL_0037:  ldc.i4.0
      IL_0038:  ret

      .line 100001,100001 : 0,0 ''
      IL_0039:  ldc.i4.0
      IL_003a:  ret

      .line 100001,100001 : 0,0 ''
      IL_003b:  ldarg.1
      IL_003c:  ldnull
      IL_003d:  cgt.un
      IL_003f:  ldc.i4.0
      IL_0040:  ceq
      IL_0042:  ret
    } // end of method MyExn::Equals

    .method public hidebysig instance bool 
            Equals(class [mscorlib]System.Exception obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       59 (0x3b)
      .maxstack  8
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse.s  IL_0033

      .line 100001,100001 : 0,0 ''
      IL_0006:  ldarg.1
      IL_0007:  ldnull
      IL_0008:  cgt.un
      IL_000a:  brfalse.s  IL_0031

      .line 100001,100001 : 0,0 ''
      IL_000c:  ldarg.1
      IL_000d:  call       bool [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/IntrinsicFunctions::TypeTestGeneric<class ABC/MyExn>(object)
      IL_0012:  brtrue.s   IL_0016

      IL_0014:  br.s       IL_002f

      .line 100001,100001 : 0,0 ''
      IL_0016:  ldarg.0
      IL_0017:  castclass  ABC/MyExn
      IL_001c:  call       instance int32 ABC/MyExn::get_Data0()
      IL_0021:  ldarg.1
      IL_0022:  castclass  ABC/MyExn
      IL_0027:  call       instance int32 ABC/MyExn::get_Data0()
      IL_002c:  ceq
      IL_002e:  ret

      .line 100001,100001 : 0,0 ''
      IL_002f:  ldc.i4.0
      IL_0030:  ret

      .line 100001,100001 : 0,0 ''
      IL_0031:  ldc.i4.0
      IL_0032:  ret

      .line 100001,100001 : 0,0 ''
      IL_0033:  ldarg.1
      IL_0034:  ldnull
      IL_0035:  cgt.un
      IL_0037:  ldc.i4.0
      IL_0038:  ceq
      IL_003a:  ret
    } // end of method MyExn::Equals

    .method public hidebysig virtual instance bool 
            Equals(object obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       20 (0x14)
      .maxstack  4
      .locals init ([0] class [mscorlib]System.Exception V_0)
      .line 7,7 : 19,24 ''
      IL_0000:  ldarg.1
      IL_0001:  isinst     [mscorlib]System.Exception
      IL_0006:  stloc.0
      IL_0007:  ldloc.0
      IL_0008:  brfalse.s  IL_0012

      .line 100001,100001 : 0,0 ''
      IL_000a:  ldarg.0
      IL_000b:  ldloc.0
      IL_000c:  callvirt   instance bool ABC/MyExn::Equals(class [mscorlib]System.Exception)
      IL_0011:  ret

      .line 100001,100001 : 0,0 ''
      IL_0012:  ldc.i4.0
      IL_0013:  ret
    } // end of method MyExn::Equals

    .property instance int32 Data0()
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                  int32) = ( 01 00 04 00 00 00 00 00 00 00 00 00 ) 
      .get instance int32 ABC/MyExn::get_Data0()
    } // end of property MyExn::Data0
  } // end of class MyExn

  .class auto ansi serializable nested public A
         extends [mscorlib]System.Object
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 03 00 00 00 00 00 ) 
    .field assembly string x
    .method public specialname rtspecialname 
            instance void  .ctor(string x) cil managed
    {
      // Code size       16 (0x10)
      .maxstack  8
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldarg.0
      IL_0001:  callvirt   instance void [mscorlib]System.Object::.ctor()
      IL_0006:  ldarg.0
      IL_0007:  pop
      .line 8,8 : 16,17 ''
      IL_0008:  ldarg.0
      IL_0009:  ldarg.1
      IL_000a:  stfld      string ABC/A::x
      .line 8,8 : 14,15 ''
      IL_000f:  ret
    } // end of method A::.ctor

    .method public hidebysig specialname 
            instance string  get_X() cil managed
    {
      // Code size       7 (0x7)
      .maxstack  8
      .line 8,8 : 42,43 ''
      IL_0000:  ldarg.0
      IL_0001:  ldfld      string ABC/A::x
      IL_0006:  ret
    } // end of method A::get_X

    .property instance string X()
    {
      .get instance string ABC/A::get_X()
    } // end of property A::X
  } // end of class A

  .class abstract auto ansi sealed nested public ABC
         extends [mscorlib]System.Object
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
    .class auto autochar serializable sealed nested public beforefieldinit Expr
           extends [mscorlib]System.Object
           implements class [mscorlib]System.IEquatable`1<class ABC/ABC/Expr>,
                      [mscorlib]System.Collections.IStructuralEquatable,
                      class [mscorlib]System.IComparable`1<class ABC/ABC/Expr>,
                      [mscorlib]System.IComparable,
                      [mscorlib]System.Collections.IStructuralComparable
    {
      .custom instance void [mscorlib]System.Diagnostics.DebuggerDisplayAttribute::.ctor(string) = ( 01 00 15 7B 5F 5F 44 65 62 75 67 44 69 73 70 6C   // ...{__DebugDispl
                                                                                                     61 79 28 29 2C 6E 71 7D 00 00 )                   // ay(),nq}..
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 01 00 00 00 00 00 ) 
      .field assembly initonly int32 item
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .method public static class ABC/ABC/Expr 
              NewNum(int32 item) cil managed
      {
        .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                    int32) = ( 01 00 08 00 00 00 00 00 00 00 00 00 ) 
        // Code size       7 (0x7)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  newobj     instance void ABC/ABC/Expr::.ctor(int32)
        IL_0006:  ret
      } // end of method Expr::NewNum

      .method assembly specialname rtspecialname 
              instance void  .ctor(int32 item) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       14 (0xe)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  ldarg.1
        IL_0008:  stfld      int32 ABC/ABC/Expr::item
        IL_000d:  ret
      } // end of method Expr::.ctor

      .method public hidebysig instance int32 
              get_Item() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       7 (0x7)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldfld      int32 ABC/ABC/Expr::item
        IL_0006:  ret
      } // end of method Expr::get_Item

      .method public hidebysig instance int32 
              get_Tag() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       4 (0x4)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  pop
        IL_0002:  ldc.i4.0
        IL_0003:  ret
      } // end of method Expr::get_Tag

      .method assembly hidebysig specialname 
              instance object  __DebugDisplay() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       22 (0x16)
        .maxstack  8
        IL_0000:  ldstr      "%+0.8A"
        IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class ABC/ABC/Expr,string>,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string,string>::.ctor(string)
        IL_000a:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatToString<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class ABC/ABC/Expr,string>>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string>)
        IL_000f:  ldarg.0
        IL_0010:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class ABC/ABC/Expr,string>::Invoke(!0)
        IL_0015:  ret
      } // end of method Expr::__DebugDisplay

      .method public strict virtual instance string 
              ToString() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       22 (0x16)
        .maxstack  8
        IL_0000:  ldstr      "%+A"
        IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class ABC/ABC/Expr,string>,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string,class ABC/ABC/Expr>::.ctor(string)
        IL_000a:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatToString<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class ABC/ABC/Expr,string>>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string>)
        IL_000f:  ldarg.0
        IL_0010:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class ABC/ABC/Expr,string>::Invoke(!0)
        IL_0015:  ret
      } // end of method Expr::ToString

      .method public hidebysig virtual final 
              instance int32  CompareTo(class ABC/ABC/Expr obj) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       64 (0x40)
        .maxstack  4
        .locals init ([0] class ABC/ABC/Expr V_0,
                 [1] class ABC/ABC/Expr V_1,
                 [2] class [mscorlib]System.Collections.IComparer V_2,
                 [3] int32 V_3,
                 [4] int32 V_4)
        .line 100001,100001 : 0,0 ''
        IL_0000:  ldarg.0
        IL_0001:  ldnull
        IL_0002:  cgt.un
        IL_0004:  brfalse.s  IL_0036

        .line 100001,100001 : 0,0 ''
        IL_0006:  ldarg.1
        IL_0007:  ldnull
        IL_0008:  cgt.un
        IL_000a:  brfalse.s  IL_0034

        .line 100001,100001 : 0,0 ''
        IL_000c:  ldarg.0
        IL_000d:  pop
        .line 100001,100001 : 0,0 ''
        IL_000e:  ldarg.0
        IL_000f:  stloc.0
        IL_0010:  ldarg.1
        IL_0011:  stloc.1
        IL_0012:  call       class [mscorlib]System.Collections.IComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericComparer()
        IL_0017:  stloc.2
        IL_0018:  ldloc.0
        IL_0019:  ldfld      int32 ABC/ABC/Expr::item
        IL_001e:  stloc.3
        IL_001f:  ldloc.1
        IL_0020:  ldfld      int32 ABC/ABC/Expr::item
        IL_0025:  stloc.s    V_4
        IL_0027:  ldloc.3
        IL_0028:  ldloc.s    V_4
        IL_002a:  bge.s      IL_002e

        .line 100001,100001 : 0,0 ''
        IL_002c:  ldc.i4.m1
        IL_002d:  ret

        .line 100001,100001 : 0,0 ''
        IL_002e:  ldloc.3
        IL_002f:  ldloc.s    V_4
        IL_0031:  cgt
        IL_0033:  ret

        .line 100001,100001 : 0,0 ''
        IL_0034:  ldc.i4.1
        IL_0035:  ret

        .line 100001,100001 : 0,0 ''
        IL_0036:  ldarg.1
        IL_0037:  ldnull
        IL_0038:  cgt.un
        IL_003a:  brfalse.s  IL_003e

        .line 100001,100001 : 0,0 ''
        IL_003c:  ldc.i4.m1
        IL_003d:  ret

        .line 100001,100001 : 0,0 ''
        IL_003e:  ldc.i4.0
        IL_003f:  ret
      } // end of method Expr::CompareTo

      .method public hidebysig virtual final 
              instance int32  CompareTo(object obj) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       13 (0xd)
        .maxstack  8
        .line 16,16 : 18,22 ''
        IL_0000:  ldarg.0
        IL_0001:  ldarg.1
        IL_0002:  unbox.any  ABC/ABC/Expr
        IL_0007:  callvirt   instance int32 ABC/ABC/Expr::CompareTo(class ABC/ABC/Expr)
        IL_000c:  ret
      } // end of method Expr::CompareTo

      .method public hidebysig virtual final 
              instance int32  CompareTo(object obj,
                                        class [mscorlib]System.Collections.IComparer comp) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       80 (0x50)
        .maxstack  4
        .locals init ([0] class ABC/ABC/Expr V_0,
                 [1] class ABC/ABC/Expr V_1,
                 [2] class ABC/ABC/Expr V_2,
                 [3] class [mscorlib]System.Collections.IComparer V_3,
                 [4] int32 V_4,
                 [5] int32 V_5)
        .line 16,16 : 18,22 ''
        IL_0000:  ldarg.1
        IL_0001:  unbox.any  ABC/ABC/Expr
        IL_0006:  stloc.0
        IL_0007:  ldarg.0
        IL_0008:  ldnull
        IL_0009:  cgt.un
        IL_000b:  brfalse.s  IL_0041

        .line 100001,100001 : 0,0 ''
        IL_000d:  ldarg.1
        IL_000e:  unbox.any  ABC/ABC/Expr
        IL_0013:  ldnull
        IL_0014:  cgt.un
        IL_0016:  brfalse.s  IL_003f

        .line 100001,100001 : 0,0 ''
        IL_0018:  ldarg.0
        IL_0019:  pop
        .line 100001,100001 : 0,0 ''
        IL_001a:  ldarg.0
        IL_001b:  stloc.1
        IL_001c:  ldloc.0
        IL_001d:  stloc.2
        IL_001e:  ldarg.2
        IL_001f:  stloc.3
        IL_0020:  ldloc.1
        IL_0021:  ldfld      int32 ABC/ABC/Expr::item
        IL_0026:  stloc.s    V_4
        IL_0028:  ldloc.2
        IL_0029:  ldfld      int32 ABC/ABC/Expr::item
        IL_002e:  stloc.s    V_5
        IL_0030:  ldloc.s    V_4
        IL_0032:  ldloc.s    V_5
        IL_0034:  bge.s      IL_0038

        .line 100001,100001 : 0,0 ''
        IL_0036:  ldc.i4.m1
        IL_0037:  ret

        .line 100001,100001 : 0,0 ''
        IL_0038:  ldloc.s    V_4
        IL_003a:  ldloc.s    V_5
        IL_003c:  cgt
        IL_003e:  ret

        .line 100001,100001 : 0,0 ''
        IL_003f:  ldc.i4.1
        IL_0040:  ret

        .line 100001,100001 : 0,0 ''
        IL_0041:  ldarg.1
        IL_0042:  unbox.any  ABC/ABC/Expr
        IL_0047:  ldnull
        IL_0048:  cgt.un
        IL_004a:  brfalse.s  IL_004e

        .line 100001,100001 : 0,0 ''
        IL_004c:  ldc.i4.m1
        IL_004d:  ret

        .line 100001,100001 : 0,0 ''
        IL_004e:  ldc.i4.0
        IL_004f:  ret
      } // end of method Expr::CompareTo

      .method public hidebysig virtual final 
              instance int32  GetHashCode(class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       41 (0x29)
        .maxstack  7
        .locals init ([0] int32 V_0,
                 [1] class ABC/ABC/Expr V_1,
                 [2] class [mscorlib]System.Collections.IEqualityComparer V_2)
        .line 100001,100001 : 0,0 ''
        IL_0000:  ldarg.0
        IL_0001:  ldnull
        IL_0002:  cgt.un
        IL_0004:  brfalse.s  IL_0027

        .line 100001,100001 : 0,0 ''
        IL_0006:  ldc.i4.0
        IL_0007:  stloc.0
        IL_0008:  ldarg.0
        IL_0009:  pop
        .line 100001,100001 : 0,0 ''
        IL_000a:  ldarg.0
        IL_000b:  stloc.1
        IL_000c:  ldc.i4.0
        IL_000d:  stloc.0
        IL_000e:  ldc.i4     0x9e3779b9
        IL_0013:  ldarg.1
        IL_0014:  stloc.2
        IL_0015:  ldloc.1
        IL_0016:  ldfld      int32 ABC/ABC/Expr::item
        IL_001b:  ldloc.0
        IL_001c:  ldc.i4.6
        IL_001d:  shl
        IL_001e:  ldloc.0
        IL_001f:  ldc.i4.2
        IL_0020:  shr
        IL_0021:  add
        IL_0022:  add
        IL_0023:  add
        IL_0024:  stloc.0
        IL_0025:  ldloc.0
        IL_0026:  ret

        .line 100001,100001 : 0,0 ''
        IL_0027:  ldc.i4.0
        IL_0028:  ret
      } // end of method Expr::GetHashCode

      .method public hidebysig virtual final 
              instance int32  GetHashCode() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       12 (0xc)
        .maxstack  8
        .line 16,16 : 18,22 ''
        IL_0000:  ldarg.0
        IL_0001:  call       class [mscorlib]System.Collections.IEqualityComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericEqualityComparer()
        IL_0006:  callvirt   instance int32 ABC/ABC/Expr::GetHashCode(class [mscorlib]System.Collections.IEqualityComparer)
        IL_000b:  ret
      } // end of method Expr::GetHashCode

      .method public hidebysig virtual final 
              instance bool  Equals(object obj,
                                    class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       52 (0x34)
        .maxstack  4
        .locals init ([0] class ABC/ABC/Expr V_0,
                 [1] class ABC/ABC/Expr V_1,
                 [2] class ABC/ABC/Expr V_2,
                 [3] class ABC/ABC/Expr V_3,
                 [4] class [mscorlib]System.Collections.IEqualityComparer V_4)
        .line 100001,100001 : 0,0 ''
        IL_0000:  ldarg.0
        IL_0001:  ldnull
        IL_0002:  cgt.un
        IL_0004:  brfalse.s  IL_002c

        .line 100001,100001 : 0,0 ''
        IL_0006:  ldarg.1
        IL_0007:  isinst     ABC/ABC/Expr
        IL_000c:  stloc.0
        IL_000d:  ldloc.0
        IL_000e:  brfalse.s  IL_002a

        .line 100001,100001 : 0,0 ''
        IL_0010:  ldloc.0
        IL_0011:  stloc.1
        IL_0012:  ldarg.0
        IL_0013:  pop
        .line 100001,100001 : 0,0 ''
        IL_0014:  ldarg.0
        IL_0015:  stloc.2
        IL_0016:  ldloc.1
        IL_0017:  stloc.3
        IL_0018:  ldarg.2
        IL_0019:  stloc.s    V_4
        IL_001b:  ldloc.2
        IL_001c:  ldfld      int32 ABC/ABC/Expr::item
        IL_0021:  ldloc.3
        IL_0022:  ldfld      int32 ABC/ABC/Expr::item
        IL_0027:  ceq
        IL_0029:  ret

        .line 100001,100001 : 0,0 ''
        IL_002a:  ldc.i4.0
        IL_002b:  ret

        .line 100001,100001 : 0,0 ''
        IL_002c:  ldarg.1
        IL_002d:  ldnull
        IL_002e:  cgt.un
        IL_0030:  ldc.i4.0
        IL_0031:  ceq
        IL_0033:  ret
      } // end of method Expr::Equals

      .method public hidebysig virtual final 
              instance bool  Equals(class ABC/ABC/Expr obj) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       43 (0x2b)
        .maxstack  4
        .locals init ([0] class ABC/ABC/Expr V_0,
                 [1] class ABC/ABC/Expr V_1)
        .line 100001,100001 : 0,0 ''
        IL_0000:  ldarg.0
        IL_0001:  ldnull
        IL_0002:  cgt.un
        IL_0004:  brfalse.s  IL_0023

        .line 100001,100001 : 0,0 ''
        IL_0006:  ldarg.1
        IL_0007:  ldnull
        IL_0008:  cgt.un
        IL_000a:  brfalse.s  IL_0021

        .line 100001,100001 : 0,0 ''
        IL_000c:  ldarg.0
        IL_000d:  pop
        .line 100001,100001 : 0,0 ''
        IL_000e:  ldarg.0
        IL_000f:  stloc.0
        IL_0010:  ldarg.1
        IL_0011:  stloc.1
        IL_0012:  ldloc.0
        IL_0013:  ldfld      int32 ABC/ABC/Expr::item
        IL_0018:  ldloc.1
        IL_0019:  ldfld      int32 ABC/ABC/Expr::item
        IL_001e:  ceq
        IL_0020:  ret

        .line 100001,100001 : 0,0 ''
        IL_0021:  ldc.i4.0
        IL_0022:  ret

        .line 100001,100001 : 0,0 ''
        IL_0023:  ldarg.1
        IL_0024:  ldnull
        IL_0025:  cgt.un
        IL_0027:  ldc.i4.0
        IL_0028:  ceq
        IL_002a:  ret
      } // end of method Expr::Equals

      .method public hidebysig virtual final 
              instance bool  Equals(object obj) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       20 (0x14)
        .maxstack  4
        .locals init ([0] class ABC/ABC/Expr V_0)
        .line 16,16 : 18,22 ''
        IL_0000:  ldarg.1
        IL_0001:  isinst     ABC/ABC/Expr
        IL_0006:  stloc.0
        IL_0007:  ldloc.0
        IL_0008:  brfalse.s  IL_0012

        .line 100001,100001 : 0,0 ''
        IL_000a:  ldarg.0
        IL_000b:  ldloc.0
        IL_000c:  callvirt   instance bool ABC/ABC/Expr::Equals(class ABC/ABC/Expr)
        IL_0011:  ret

        .line 100001,100001 : 0,0 ''
        IL_0012:  ldc.i4.0
        IL_0013:  ret
      } // end of method Expr::Equals

      .property instance int32 Tag()
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
        .get instance int32 ABC/ABC/Expr::get_Tag()
      } // end of property Expr::Tag
      .property instance int32 Item()
      {
        .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                    int32,
                                                                                                    int32) = ( 01 00 04 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        .get instance int32 ABC/ABC/Expr::get_Item()
      } // end of property Expr::Item
    } // end of class Expr

    .class auto ansi serializable nested public beforefieldinit MyExn
           extends [mscorlib]System.Exception
           implements [mscorlib]System.Collections.IStructuralEquatable
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 05 00 00 00 00 00 ) 
      .field assembly int32 Data0@
      .method public specialname rtspecialname 
              instance void  .ctor(int32 data0) cil managed
      {
        // Code size       14 (0xe)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void [mscorlib]System.Exception::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  ldarg.1
        IL_0008:  stfld      int32 ABC/ABC/MyExn::Data0@
        IL_000d:  ret
      } // end of method MyExn::.ctor

      .method public specialname rtspecialname 
              instance void  .ctor() cil managed
      {
        // Code size       7 (0x7)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void [mscorlib]System.Exception::.ctor()
        IL_0006:  ret
      } // end of method MyExn::.ctor

      .method family specialname rtspecialname 
              instance void  .ctor(class [mscorlib]System.Runtime.Serialization.SerializationInfo info,
                                   valuetype [mscorlib]System.Runtime.Serialization.StreamingContext context) cil managed
      {
        // Code size       9 (0x9)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldarg.1
        IL_0002:  ldarg.2
        IL_0003:  call       instance void [mscorlib]System.Exception::.ctor(class [mscorlib]System.Runtime.Serialization.SerializationInfo,
                                                                             valuetype [mscorlib]System.Runtime.Serialization.StreamingContext)
        IL_0008:  ret
      } // end of method MyExn::.ctor

      .method public hidebysig specialname 
              instance int32  get_Data0() cil managed
      {
        // Code size       7 (0x7)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldfld      int32 ABC/ABC/MyExn::Data0@
        IL_0006:  ret
      } // end of method MyExn::get_Data0

      .method public hidebysig virtual instance int32 
              GetHashCode(class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       40 (0x28)
        .maxstack  7
        .locals init ([0] int32 V_0,
                 [1] class [mscorlib]System.Collections.IEqualityComparer V_1)
        .line 100001,100001 : 0,0 ''
        IL_0000:  ldarg.0
        IL_0001:  ldnull
        IL_0002:  cgt.un
        IL_0004:  brfalse.s  IL_0026

        .line 100001,100001 : 0,0 ''
        IL_0006:  ldc.i4.0
        IL_0007:  stloc.0
        IL_0008:  ldc.i4     0x9e3779b9
        IL_000d:  ldarg.1
        IL_000e:  stloc.1
        IL_000f:  ldarg.0
        IL_0010:  castclass  ABC/ABC/MyExn
        IL_0015:  call       instance int32 ABC/ABC/MyExn::get_Data0()
        IL_001a:  ldloc.0
        IL_001b:  ldc.i4.6
        IL_001c:  shl
        IL_001d:  ldloc.0
        IL_001e:  ldc.i4.2
        IL_001f:  shr
        IL_0020:  add
        IL_0021:  add
        IL_0022:  add
        IL_0023:  stloc.0
        IL_0024:  ldloc.0
        IL_0025:  ret

        .line 100001,100001 : 0,0 ''
        IL_0026:  ldc.i4.0
        IL_0027:  ret
      } // end of method MyExn::GetHashCode

      .method public hidebysig virtual instance int32 
              GetHashCode() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       12 (0xc)
        .maxstack  8
        .line 17,17 : 23,28 ''
        IL_0000:  ldarg.0
        IL_0001:  call       class [mscorlib]System.Collections.IEqualityComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericEqualityComparer()
        IL_0006:  callvirt   instance int32 ABC/ABC/MyExn::GetHashCode(class [mscorlib]System.Collections.IEqualityComparer)
        IL_000b:  ret
      } // end of method MyExn::GetHashCode

      .method public hidebysig virtual instance bool 
              Equals(object obj,
                     class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       67 (0x43)
        .maxstack  4
        .locals init ([0] class [mscorlib]System.Exception V_0,
                 [1] class [mscorlib]System.Exception V_1,
                 [2] class [mscorlib]System.Collections.IEqualityComparer V_2)
        .line 100001,100001 : 0,0 ''
        IL_0000:  ldarg.0
        IL_0001:  ldnull
        IL_0002:  cgt.un
        IL_0004:  brfalse.s  IL_003b

        .line 100001,100001 : 0,0 ''
        IL_0006:  ldarg.1
        IL_0007:  isinst     [mscorlib]System.Exception
        IL_000c:  stloc.0
        IL_000d:  ldloc.0
        IL_000e:  brfalse.s  IL_0039

        .line 100001,100001 : 0,0 ''
        IL_0010:  ldloc.0
        IL_0011:  stloc.1
        IL_0012:  ldloc.0
        IL_0013:  call       bool [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/IntrinsicFunctions::TypeTestGeneric<class ABC/ABC/MyExn>(object)
        IL_0018:  brtrue.s   IL_001c

        IL_001a:  br.s       IL_0037

        .line 100001,100001 : 0,0 ''
        IL_001c:  ldarg.2
        IL_001d:  stloc.2
        IL_001e:  ldarg.0
        IL_001f:  castclass  ABC/ABC/MyExn
        IL_0024:  call       instance int32 ABC/ABC/MyExn::get_Data0()
        IL_0029:  ldloc.1
        IL_002a:  castclass  ABC/ABC/MyExn
        IL_002f:  call       instance int32 ABC/ABC/MyExn::get_Data0()
        IL_0034:  ceq
        IL_0036:  ret

        .line 100001,100001 : 0,0 ''
        IL_0037:  ldc.i4.0
        IL_0038:  ret

        .line 100001,100001 : 0,0 ''
        IL_0039:  ldc.i4.0
        IL_003a:  ret

        .line 100001,100001 : 0,0 ''
        IL_003b:  ldarg.1
        IL_003c:  ldnull
        IL_003d:  cgt.un
        IL_003f:  ldc.i4.0
        IL_0040:  ceq
        IL_0042:  ret
      } // end of method MyExn::Equals

      .method public hidebysig instance bool 
              Equals(class [mscorlib]System.Exception obj) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       59 (0x3b)
        .maxstack  8
        .line 100001,100001 : 0,0 ''
        IL_0000:  ldarg.0
        IL_0001:  ldnull
        IL_0002:  cgt.un
        IL_0004:  brfalse.s  IL_0033

        .line 100001,100001 : 0,0 ''
        IL_0006:  ldarg.1
        IL_0007:  ldnull
        IL_0008:  cgt.un
        IL_000a:  brfalse.s  IL_0031

        .line 100001,100001 : 0,0 ''
        IL_000c:  ldarg.1
        IL_000d:  call       bool [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/IntrinsicFunctions::TypeTestGeneric<class ABC/ABC/MyExn>(object)
        IL_0012:  brtrue.s   IL_0016

        IL_0014:  br.s       IL_002f

        .line 100001,100001 : 0,0 ''
        IL_0016:  ldarg.0
        IL_0017:  castclass  ABC/ABC/MyExn
        IL_001c:  call       instance int32 ABC/ABC/MyExn::get_Data0()
        IL_0021:  ldarg.1
        IL_0022:  castclass  ABC/ABC/MyExn
        IL_0027:  call       instance int32 ABC/ABC/MyExn::get_Data0()
        IL_002c:  ceq
        IL_002e:  ret

        .line 100001,100001 : 0,0 ''
        IL_002f:  ldc.i4.0
        IL_0030:  ret

        .line 100001,100001 : 0,0 ''
        IL_0031:  ldc.i4.0
        IL_0032:  ret

        .line 100001,100001 : 0,0 ''
        IL_0033:  ldarg.1
        IL_0034:  ldnull
        IL_0035:  cgt.un
        IL_0037:  ldc.i4.0
        IL_0038:  ceq
        IL_003a:  ret
      } // end of method MyExn::Equals

      .method public hidebysig virtual instance bool 
              Equals(object obj) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       20 (0x14)
        .maxstack  4
        .locals init ([0] class [mscorlib]System.Exception V_0)
        .line 17,17 : 23,28 ''
        IL_0000:  ldarg.1
        IL_0001:  isinst     [mscorlib]System.Exception
        IL_0006:  stloc.0
        IL_0007:  ldloc.0
        IL_0008:  brfalse.s  IL_0012

        .line 100001,100001 : 0,0 ''
        IL_000a:  ldarg.0
        IL_000b:  ldloc.0
        IL_000c:  callvirt   instance bool ABC/ABC/MyExn::Equals(class [mscorlib]System.Exception)
        IL_0011:  ret

        .line 100001,100001 : 0,0 ''
        IL_0012:  ldc.i4.0
        IL_0013:  ret
      } // end of method MyExn::Equals

      .property instance int32 Data0()
      {
        .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                    int32) = ( 01 00 04 00 00 00 00 00 00 00 00 00 ) 
        .get instance int32 ABC/ABC/MyExn::get_Data0()
      } // end of property MyExn::Data0
    } // end of class MyExn

    .class auto ansi serializable nested public A
           extends [mscorlib]System.Object
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 03 00 00 00 00 00 ) 
      .field assembly string x
      .method public specialname rtspecialname 
              instance void  .ctor(string x) cil managed
      {
        // Code size       16 (0x10)
        .maxstack  8
        .line 100001,100001 : 0,0 ''
        IL_0000:  ldarg.0
        IL_0001:  callvirt   instance void [mscorlib]System.Object::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  pop
        .line 18,18 : 20,21 ''
        IL_0008:  ldarg.0
        IL_0009:  ldarg.1
        IL_000a:  stfld      string ABC/ABC/A::x
        .line 18,18 : 18,19 ''
        IL_000f:  ret
      } // end of method A::.ctor

      .method public hidebysig specialname 
              instance string  get_X() cil managed
      {
        // Code size       7 (0x7)
        .maxstack  8
        .line 18,18 : 46,47 ''
        IL_0000:  ldarg.0
        IL_0001:  ldfld      string ABC/ABC/A::x
        IL_0006:  ret
      } // end of method A::get_X

      .property instance string X()
      {
        .get instance string ABC/ABC/A::get_X()
      } // end of property A::X
    } // end of class A

    .method public static int32  'add'(int32 x,
                                       int32 y) cil managed
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationArgumentCountsAttribute::.ctor(int32[]) = ( 01 00 02 00 00 00 01 00 00 00 01 00 00 00 00 00 ) 
      // Code size       4 (0x4)
      .maxstack  8
      .line 21,21 : 27,32 ''
      IL_0000:  ldarg.0
      IL_0001:  ldarg.1
      IL_0002:  add
      IL_0003:  ret
    } // end of method ABC::'add'

    .method public specialname static string 
            get_greeting() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       6 (0x6)
      .maxstack  8
      IL_0000:  ldstr      "hello"
      IL_0005:  ret
    } // end of method ABC::get_greeting

    .property string greeting()
    {
      .get string ABC/ABC::get_greeting()
    } // end of property ABC::greeting
  } // end of class ABC

  .method public static int32  'add'(int32 x,
                                     int32 y) cil managed
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationArgumentCountsAttribute::.ctor(int32[]) = ( 01 00 02 00 00 00 01 00 00 00 01 00 00 00 00 00 ) 
    // Code size       4 (0x4)
    .maxstack  8
    .line 11,11 : 23,28 ''
    IL_0000:  ldarg.0
    IL_0001:  ldarg.1
    IL_0002:  add
    IL_0003:  ret
  } // end of method ABC::'add'

  .method public specialname static string 
          get_greeting() cil managed
  {
    .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
    .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
    // Code size       6 (0x6)
    .maxstack  8
    IL_0000:  ldstr      "hello"
    IL_0005:  ret
  } // end of method ABC::get_greeting

  .property string greeting()
  {
    .get string ABC::get_greeting()
  } // end of property ABC::greeting
} // end of class ABC

.class private abstract auto ansi sealed '<StartupCode$TopLevelModule>'.$ABC
       extends [mscorlib]System.Object
{
  .field static assembly int32 init@
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
  .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
  .method private specialname rtspecialname static 
          void  .cctor() cil managed
  {
    // Code size       13 (0xd)
    .maxstack  3
    .locals init ([0] string greeting,
             [1] string V_1)
    .line 12,12 : 9,31 ''
    IL_0000:  call       string ABC::get_greeting()
    IL_0005:  stloc.0
    .line 22,22 : 13,35 ''
    IL_0006:  call       string ABC/ABC::get_greeting()
    IL_000b:  stloc.1
    IL_000c:  ret
  } // end of method $ABC::.cctor

} // end of class '<StartupCode$TopLevelModule>'.$ABC


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
