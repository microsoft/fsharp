
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
.assembly TestFunction24
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.TestFunction24
{
  // Offset: 0x00000000 Length: 0x00000742
}
.mresource public FSharpOptimizationData.TestFunction24
{
  // Offset: 0x00000748 Length: 0x00000228
}
.module TestFunction24.exe
// MVID: {60B68B97-A643-4587-A745-0383978BB660}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x06EA0000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed TestFunction24
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class auto ansi serializable sealed nested public Point
         extends [mscorlib]System.Object
         implements class [mscorlib]System.IEquatable`1<class TestFunction24/Point>,
                    [mscorlib]System.Collections.IStructuralEquatable,
                    class [mscorlib]System.IComparable`1<class TestFunction24/Point>,
                    [mscorlib]System.IComparable,
                    [mscorlib]System.Collections.IStructuralComparable
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 02 00 00 00 00 00 ) 
    .field public int32 x@
    .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
    .field public int32 y@
    .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
    .method public hidebysig specialname 
            instance int32  get_x() cil managed
    {
      // Code size       7 (0x7)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  ldfld      int32 TestFunction24/Point::x@
      IL_0006:  ret
    } // end of method Point::get_x

    .method public hidebysig specialname 
            instance int32  get_y() cil managed
    {
      // Code size       7 (0x7)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  ldfld      int32 TestFunction24/Point::y@
      IL_0006:  ret
    } // end of method Point::get_y

    .method public hidebysig specialname 
            instance void  set_x(int32 'value') cil managed
    {
      // Code size       8 (0x8)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  ldarg.1
      IL_0002:  stfld      int32 TestFunction24/Point::x@
      IL_0007:  ret
    } // end of method Point::set_x

    .method public hidebysig specialname 
            instance void  set_y(int32 'value') cil managed
    {
      // Code size       8 (0x8)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  ldarg.1
      IL_0002:  stfld      int32 TestFunction24/Point::y@
      IL_0007:  ret
    } // end of method Point::set_y

    .method public specialname rtspecialname 
            instance void  .ctor(int32 x,
                                 int32 y) cil managed
    {
      // Code size       21 (0x15)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
      IL_0006:  ldarg.0
      IL_0007:  ldarg.1
      IL_0008:  stfld      int32 TestFunction24/Point::x@
      IL_000d:  ldarg.0
      IL_000e:  ldarg.2
      IL_000f:  stfld      int32 TestFunction24/Point::y@
      IL_0014:  ret
    } // end of method Point::.ctor

    .method public strict virtual instance string 
            ToString() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       22 (0x16)
      .maxstack  8
      IL_0000:  ldstr      "%+A"
      IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction24/Point,string>,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string,class TestFunction24/Point>::.ctor(string)
      IL_000a:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatToString<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction24/Point,string>>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string>)
      IL_000f:  ldarg.0
      IL_0010:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction24/Point,string>::Invoke(!0)
      IL_0015:  ret
    } // end of method Point::ToString

    .method public hidebysig virtual final 
            instance int32  CompareTo(class TestFunction24/Point obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       108 (0x6c)
      .maxstack  4
      .locals init ([0] int32 V_0,
               [1] class [mscorlib]System.Collections.IComparer V_1,
               [2] int32 V_2,
               [3] int32 V_3,
               [4] class [mscorlib]System.Collections.IComparer V_4,
               [5] int32 V_5,
               [6] int32 V_6)
      .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
      .line 16707566,16707566 : 0,0 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\TestFunctions\\TestFunction24.fs'
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse.s  IL_0062

      .line 16707566,16707566 : 0,0 ''
      IL_0006:  ldarg.1
      IL_0007:  ldnull
      IL_0008:  cgt.un
      IL_000a:  brfalse.s  IL_0060

      .line 16707566,16707566 : 0,0 ''
      IL_000c:  call       class [mscorlib]System.Collections.IComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericComparer()
      IL_0011:  stloc.1
      IL_0012:  ldarg.0
      IL_0013:  ldfld      int32 TestFunction24/Point::x@
      IL_0018:  stloc.2
      IL_0019:  ldarg.1
      IL_001a:  ldfld      int32 TestFunction24/Point::x@
      IL_001f:  stloc.3
      IL_0020:  ldloc.2
      IL_0021:  ldloc.3
      IL_0022:  bge.s      IL_0028

      .line 16707566,16707566 : 0,0 ''
      IL_0024:  ldc.i4.m1
      .line 16707566,16707566 : 0,0 ''
      IL_0025:  nop
      IL_0026:  br.s       IL_002d

      .line 16707566,16707566 : 0,0 ''
      IL_0028:  ldloc.2
      IL_0029:  ldloc.3
      IL_002a:  cgt
      .line 16707566,16707566 : 0,0 ''
      IL_002c:  nop
      .line 16707566,16707566 : 0,0 ''
      IL_002d:  stloc.0
      IL_002e:  ldloc.0
      IL_002f:  ldc.i4.0
      IL_0030:  bge.s      IL_0034

      .line 16707566,16707566 : 0,0 ''
      IL_0032:  ldloc.0
      IL_0033:  ret

      .line 16707566,16707566 : 0,0 ''
      IL_0034:  ldloc.0
      IL_0035:  ldc.i4.0
      IL_0036:  ble.s      IL_003a

      .line 16707566,16707566 : 0,0 ''
      IL_0038:  ldloc.0
      IL_0039:  ret

      .line 16707566,16707566 : 0,0 ''
      IL_003a:  call       class [mscorlib]System.Collections.IComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericComparer()
      IL_003f:  stloc.s    V_4
      IL_0041:  ldarg.0
      IL_0042:  ldfld      int32 TestFunction24/Point::y@
      IL_0047:  stloc.s    V_5
      IL_0049:  ldarg.1
      IL_004a:  ldfld      int32 TestFunction24/Point::y@
      IL_004f:  stloc.s    V_6
      IL_0051:  ldloc.s    V_5
      IL_0053:  ldloc.s    V_6
      IL_0055:  bge.s      IL_0059

      .line 16707566,16707566 : 0,0 ''
      IL_0057:  ldc.i4.m1
      IL_0058:  ret

      .line 16707566,16707566 : 0,0 ''
      IL_0059:  ldloc.s    V_5
      IL_005b:  ldloc.s    V_6
      IL_005d:  cgt
      IL_005f:  ret

      .line 16707566,16707566 : 0,0 ''
      IL_0060:  ldc.i4.1
      IL_0061:  ret

      .line 16707566,16707566 : 0,0 ''
      IL_0062:  ldarg.1
      IL_0063:  ldnull
      IL_0064:  cgt.un
      IL_0066:  brfalse.s  IL_006a

      .line 16707566,16707566 : 0,0 ''
      IL_0068:  ldc.i4.m1
      IL_0069:  ret

      .line 16707566,16707566 : 0,0 ''
      IL_006a:  ldc.i4.0
      IL_006b:  ret
    } // end of method Point::CompareTo

    .method public hidebysig virtual final 
            instance int32  CompareTo(object obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       13 (0xd)
      .maxstack  8
      .line 4,4 : 6,11 ''
      IL_0000:  ldarg.0
      IL_0001:  ldarg.1
      IL_0002:  unbox.any  TestFunction24/Point
      IL_0007:  callvirt   instance int32 TestFunction24/Point::CompareTo(class TestFunction24/Point)
      IL_000c:  ret
    } // end of method Point::CompareTo

    .method public hidebysig virtual final 
            instance int32  CompareTo(object obj,
                                      class [mscorlib]System.Collections.IComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       125 (0x7d)
      .maxstack  4
      .locals init ([0] class TestFunction24/Point V_0,
               [1] class TestFunction24/Point V_1,
               [2] int32 V_2,
               [3] class [mscorlib]System.Collections.IComparer V_3,
               [4] int32 V_4,
               [5] int32 V_5,
               [6] class [mscorlib]System.Collections.IComparer V_6,
               [7] int32 V_7,
               [8] int32 V_8)
      .line 4,4 : 6,11 ''
      IL_0000:  ldarg.1
      IL_0001:  unbox.any  TestFunction24/Point
      IL_0006:  stloc.0
      IL_0007:  ldloc.0
      IL_0008:  stloc.1
      IL_0009:  ldarg.0
      IL_000a:  ldnull
      IL_000b:  cgt.un
      IL_000d:  brfalse.s  IL_006e

      .line 16707566,16707566 : 0,0 ''
      IL_000f:  ldarg.1
      IL_0010:  unbox.any  TestFunction24/Point
      IL_0015:  ldnull
      IL_0016:  cgt.un
      IL_0018:  brfalse.s  IL_006c

      .line 16707566,16707566 : 0,0 ''
      IL_001a:  ldarg.2
      IL_001b:  stloc.3
      IL_001c:  ldarg.0
      IL_001d:  ldfld      int32 TestFunction24/Point::x@
      IL_0022:  stloc.s    V_4
      IL_0024:  ldloc.1
      IL_0025:  ldfld      int32 TestFunction24/Point::x@
      IL_002a:  stloc.s    V_5
      IL_002c:  ldloc.s    V_4
      IL_002e:  ldloc.s    V_5
      IL_0030:  bge.s      IL_0036

      .line 16707566,16707566 : 0,0 ''
      IL_0032:  ldc.i4.m1
      .line 16707566,16707566 : 0,0 ''
      IL_0033:  nop
      IL_0034:  br.s       IL_003d

      .line 16707566,16707566 : 0,0 ''
      IL_0036:  ldloc.s    V_4
      IL_0038:  ldloc.s    V_5
      IL_003a:  cgt
      .line 16707566,16707566 : 0,0 ''
      IL_003c:  nop
      .line 16707566,16707566 : 0,0 ''
      IL_003d:  stloc.2
      IL_003e:  ldloc.2
      IL_003f:  ldc.i4.0
      IL_0040:  bge.s      IL_0044

      .line 16707566,16707566 : 0,0 ''
      IL_0042:  ldloc.2
      IL_0043:  ret

      .line 16707566,16707566 : 0,0 ''
      IL_0044:  ldloc.2
      IL_0045:  ldc.i4.0
      IL_0046:  ble.s      IL_004a

      .line 16707566,16707566 : 0,0 ''
      IL_0048:  ldloc.2
      IL_0049:  ret

      .line 16707566,16707566 : 0,0 ''
      IL_004a:  ldarg.2
      IL_004b:  stloc.s    V_6
      IL_004d:  ldarg.0
      IL_004e:  ldfld      int32 TestFunction24/Point::y@
      IL_0053:  stloc.s    V_7
      IL_0055:  ldloc.1
      IL_0056:  ldfld      int32 TestFunction24/Point::y@
      IL_005b:  stloc.s    V_8
      IL_005d:  ldloc.s    V_7
      IL_005f:  ldloc.s    V_8
      IL_0061:  bge.s      IL_0065

      .line 16707566,16707566 : 0,0 ''
      IL_0063:  ldc.i4.m1
      IL_0064:  ret

      .line 16707566,16707566 : 0,0 ''
      IL_0065:  ldloc.s    V_7
      IL_0067:  ldloc.s    V_8
      IL_0069:  cgt
      IL_006b:  ret

      .line 16707566,16707566 : 0,0 ''
      IL_006c:  ldc.i4.1
      IL_006d:  ret

      .line 16707566,16707566 : 0,0 ''
      IL_006e:  ldarg.1
      IL_006f:  unbox.any  TestFunction24/Point
      IL_0074:  ldnull
      IL_0075:  cgt.un
      IL_0077:  brfalse.s  IL_007b

      .line 16707566,16707566 : 0,0 ''
      IL_0079:  ldc.i4.m1
      IL_007a:  ret

      .line 16707566,16707566 : 0,0 ''
      IL_007b:  ldc.i4.0
      IL_007c:  ret
    } // end of method Point::CompareTo

    .method public hidebysig virtual final 
            instance int32  GetHashCode(class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       58 (0x3a)
      .maxstack  7
      .locals init ([0] int32 V_0,
               [1] class [mscorlib]System.Collections.IEqualityComparer V_1,
               [2] class [mscorlib]System.Collections.IEqualityComparer V_2)
      .line 16707566,16707566 : 0,0 ''
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse.s  IL_0038

      .line 16707566,16707566 : 0,0 ''
      IL_0006:  ldc.i4.0
      IL_0007:  stloc.0
      IL_0008:  ldc.i4     0x9e3779b9
      IL_000d:  ldarg.1
      IL_000e:  stloc.1
      IL_000f:  ldarg.0
      IL_0010:  ldfld      int32 TestFunction24/Point::y@
      IL_0015:  ldloc.0
      IL_0016:  ldc.i4.6
      IL_0017:  shl
      IL_0018:  ldloc.0
      IL_0019:  ldc.i4.2
      IL_001a:  shr
      IL_001b:  add
      IL_001c:  add
      IL_001d:  add
      IL_001e:  stloc.0
      IL_001f:  ldc.i4     0x9e3779b9
      IL_0024:  ldarg.1
      IL_0025:  stloc.2
      IL_0026:  ldarg.0
      IL_0027:  ldfld      int32 TestFunction24/Point::x@
      IL_002c:  ldloc.0
      IL_002d:  ldc.i4.6
      IL_002e:  shl
      IL_002f:  ldloc.0
      IL_0030:  ldc.i4.2
      IL_0031:  shr
      IL_0032:  add
      IL_0033:  add
      IL_0034:  add
      IL_0035:  stloc.0
      IL_0036:  ldloc.0
      IL_0037:  ret

      .line 16707566,16707566 : 0,0 ''
      IL_0038:  ldc.i4.0
      IL_0039:  ret
    } // end of method Point::GetHashCode

    .method public hidebysig virtual final 
            instance int32  GetHashCode() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       12 (0xc)
      .maxstack  8
      .line 4,4 : 6,11 ''
      IL_0000:  ldarg.0
      IL_0001:  call       class [mscorlib]System.Collections.IEqualityComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericEqualityComparer()
      IL_0006:  callvirt   instance int32 TestFunction24/Point::GetHashCode(class [mscorlib]System.Collections.IEqualityComparer)
      IL_000b:  ret
    } // end of method Point::GetHashCode

    .method public hidebysig virtual final 
            instance bool  Equals(object obj,
                                  class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       65 (0x41)
      .maxstack  4
      .locals init ([0] class TestFunction24/Point V_0,
               [1] class TestFunction24/Point V_1,
               [2] class [mscorlib]System.Collections.IEqualityComparer V_2,
               [3] class [mscorlib]System.Collections.IEqualityComparer V_3)
      .line 16707566,16707566 : 0,0 ''
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse.s  IL_0039

      .line 16707566,16707566 : 0,0 ''
      IL_0006:  ldarg.1
      IL_0007:  isinst     TestFunction24/Point
      IL_000c:  stloc.0
      IL_000d:  ldloc.0
      IL_000e:  brfalse.s  IL_0037

      .line 16707566,16707566 : 0,0 ''
      IL_0010:  ldloc.0
      IL_0011:  stloc.1
      IL_0012:  ldarg.2
      IL_0013:  stloc.2
      IL_0014:  ldarg.0
      IL_0015:  ldfld      int32 TestFunction24/Point::x@
      IL_001a:  ldloc.1
      IL_001b:  ldfld      int32 TestFunction24/Point::x@
      IL_0020:  ceq
      IL_0022:  brfalse.s  IL_0035

      .line 16707566,16707566 : 0,0 ''
      IL_0024:  ldarg.2
      IL_0025:  stloc.3
      IL_0026:  ldarg.0
      IL_0027:  ldfld      int32 TestFunction24/Point::y@
      IL_002c:  ldloc.1
      IL_002d:  ldfld      int32 TestFunction24/Point::y@
      IL_0032:  ceq
      IL_0034:  ret

      .line 16707566,16707566 : 0,0 ''
      IL_0035:  ldc.i4.0
      IL_0036:  ret

      .line 16707566,16707566 : 0,0 ''
      IL_0037:  ldc.i4.0
      IL_0038:  ret

      .line 16707566,16707566 : 0,0 ''
      IL_0039:  ldarg.1
      IL_003a:  ldnull
      IL_003b:  cgt.un
      IL_003d:  ldc.i4.0
      IL_003e:  ceq
      IL_0040:  ret
    } // end of method Point::Equals

    .method public hidebysig virtual final 
            instance bool  Equals(class TestFunction24/Point obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       53 (0x35)
      .maxstack  8
      .line 16707566,16707566 : 0,0 ''
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse.s  IL_002d

      .line 16707566,16707566 : 0,0 ''
      IL_0006:  ldarg.1
      IL_0007:  ldnull
      IL_0008:  cgt.un
      IL_000a:  brfalse.s  IL_002b

      .line 16707566,16707566 : 0,0 ''
      IL_000c:  ldarg.0
      IL_000d:  ldfld      int32 TestFunction24/Point::x@
      IL_0012:  ldarg.1
      IL_0013:  ldfld      int32 TestFunction24/Point::x@
      IL_0018:  bne.un.s   IL_0029

      .line 16707566,16707566 : 0,0 ''
      IL_001a:  ldarg.0
      IL_001b:  ldfld      int32 TestFunction24/Point::y@
      IL_0020:  ldarg.1
      IL_0021:  ldfld      int32 TestFunction24/Point::y@
      IL_0026:  ceq
      IL_0028:  ret

      .line 16707566,16707566 : 0,0 ''
      IL_0029:  ldc.i4.0
      IL_002a:  ret

      .line 16707566,16707566 : 0,0 ''
      IL_002b:  ldc.i4.0
      IL_002c:  ret

      .line 16707566,16707566 : 0,0 ''
      IL_002d:  ldarg.1
      IL_002e:  ldnull
      IL_002f:  cgt.un
      IL_0031:  ldc.i4.0
      IL_0032:  ceq
      IL_0034:  ret
    } // end of method Point::Equals

    .method public hidebysig virtual final 
            instance bool  Equals(object obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       20 (0x14)
      .maxstack  4
      .locals init ([0] class TestFunction24/Point V_0)
      .line 4,4 : 6,11 ''
      IL_0000:  ldarg.1
      IL_0001:  isinst     TestFunction24/Point
      IL_0006:  stloc.0
      IL_0007:  ldloc.0
      IL_0008:  brfalse.s  IL_0012

      .line 16707566,16707566 : 0,0 ''
      IL_000a:  ldarg.0
      IL_000b:  ldloc.0
      IL_000c:  callvirt   instance bool TestFunction24/Point::Equals(class TestFunction24/Point)
      IL_0011:  ret

      .line 16707566,16707566 : 0,0 ''
      IL_0012:  ldc.i4.0
      IL_0013:  ret
    } // end of method Point::Equals

    .property instance int32 x()
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                  int32) = ( 01 00 04 00 00 00 00 00 00 00 00 00 ) 
      .set instance void TestFunction24/Point::set_x(int32)
      .get instance int32 TestFunction24/Point::get_x()
    } // end of property Point::x
    .property instance int32 y()
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                  int32) = ( 01 00 04 00 00 00 01 00 00 00 00 00 ) 
      .set instance void TestFunction24/Point::set_y(int32)
      .get instance int32 TestFunction24/Point::get_y()
    } // end of property Point::y
  } // end of class Point

  .method public static int32  pinObject() cil managed
  {
    // Code size       66 (0x42)
    .maxstack  6
    .locals init ([0] class TestFunction24/Point point,
             [1] native int p1,
             [2] int32& pinned V_2,
             [3] native int V_3,
             [4] int32 V_4,
             [5] native int V_5,
             [6] int32 V_6)
    .line 7,7 : 5,33 ''
    IL_0000:  ldc.i4.1
    IL_0001:  ldc.i4.2
    IL_0002:  newobj     instance void TestFunction24/Point::.ctor(int32,
                                                                   int32)
    IL_0007:  stloc.0
    .line 8,8 : 5,28 ''
    IL_0008:  ldloc.0
    IL_0009:  ldflda     int32 TestFunction24/Point::x@
    IL_000e:  stloc.2
    IL_000f:  ldloc.2
    IL_0010:  conv.i
    IL_0011:  stloc.1
    .line 9,9 : 5,44 ''
    IL_0012:  ldloc.1
    IL_0013:  stloc.3
    IL_0014:  ldc.i4.0
    IL_0015:  stloc.s    V_4
    IL_0017:  ldloc.3
    IL_0018:  ldloc.s    V_4
    IL_001a:  conv.i
    IL_001b:  sizeof     [mscorlib]System.Int32
    IL_0021:  mul
    IL_0022:  add
    IL_0023:  ldobj      [mscorlib]System.Int32
    IL_0028:  ldloc.1
    IL_0029:  stloc.s    V_5
    IL_002b:  ldc.i4.1
    IL_002c:  stloc.s    V_6
    IL_002e:  ldloc.s    V_5
    IL_0030:  ldloc.s    V_6
    IL_0032:  conv.i
    IL_0033:  sizeof     [mscorlib]System.Int32
    IL_0039:  mul
    IL_003a:  add
    IL_003b:  ldobj      [mscorlib]System.Int32
    IL_0040:  add
    IL_0041:  ret
  } // end of method TestFunction24::pinObject

  .method public static int32  pinRef() cil managed
  {
    // Code size       32 (0x20)
    .maxstack  4
    .locals init ([0] class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32> point,
             [1] native int p1,
             [2] int32& pinned V_2)
    .line 12,12 : 5,23 ''
    IL_0000:  ldc.i4.s   17
    IL_0002:  call       class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<!!0> [FSharp.Core]Microsoft.FSharp.Core.Operators::Ref<int32>(!!0)
    IL_0007:  stloc.0
    .line 13,13 : 5,35 ''
    IL_0008:  ldloc.0
    IL_0009:  ldflda     !0 class [FSharp.Core]Microsoft.FSharp.Core.FSharpRef`1<int32>::contents@
    IL_000e:  stloc.2
    IL_000f:  ldloc.2
    IL_0010:  conv.i
    IL_0011:  stloc.1
    .line 14,14 : 5,42 ''
    IL_0012:  ldloc.1
    IL_0013:  ldobj      [mscorlib]System.Int32
    IL_0018:  ldloc.1
    IL_0019:  ldobj      [mscorlib]System.Int32
    IL_001e:  add
    IL_001f:  ret
  } // end of method TestFunction24::pinRef

  .method public static float64  pinArray1() cil managed
  {
    // Code size       188 (0xbc)
    .maxstack  6
    .locals init ([0] float64[] arr,
             [1] native int p1,
             [2] float64[] V_2,
             [3] float64& pinned V_3,
             [4] native int V_4,
             [5] int32 V_5,
             [6] native int V_6,
             [7] int32 V_7)
    .line 17,17 : 5,49 ''
    IL_0000:  ldc.i4.6
    IL_0001:  newarr     [mscorlib]System.Double
    IL_0006:  dup
    IL_0007:  ldc.i4.0
    IL_0008:  ldc.r8     0.0
    IL_0011:  stelem     [mscorlib]System.Double
    IL_0016:  dup
    IL_0017:  ldc.i4.1
    IL_0018:  ldc.r8     1.5
    IL_0021:  stelem     [mscorlib]System.Double
    IL_0026:  dup
    IL_0027:  ldc.i4.2
    IL_0028:  ldc.r8     2.2999999999999998
    IL_0031:  stelem     [mscorlib]System.Double
    IL_0036:  dup
    IL_0037:  ldc.i4.3
    IL_0038:  ldc.r8     3.3999999999999999
    IL_0041:  stelem     [mscorlib]System.Double
    IL_0046:  dup
    IL_0047:  ldc.i4.4
    IL_0048:  ldc.r8     4.
    IL_0051:  stelem     [mscorlib]System.Double
    IL_0056:  dup
    IL_0057:  ldc.i4.5
    IL_0058:  ldc.r8     5.9000000000000004
    IL_0061:  stelem     [mscorlib]System.Double
    IL_0066:  stloc.0
    .line 18,18 : 5,23 ''
    IL_0067:  ldloc.0
    IL_0068:  stloc.2
    IL_0069:  ldloc.2
    IL_006a:  brfalse.s  IL_0086

    .line 16707566,16707566 : 0,0 ''
    IL_006c:  ldloc.2
    IL_006d:  call       int32 [FSharp.Core]Microsoft.FSharp.Collections.ArrayModule::Length<float64>(!!0[])
    IL_0072:  brfalse.s  IL_0081

    .line 16707566,16707566 : 0,0 ''
    IL_0074:  ldloc.2
    IL_0075:  ldc.i4.0
    IL_0076:  ldelema    [mscorlib]System.Double
    IL_007b:  stloc.3
    IL_007c:  ldloc.3
    IL_007d:  conv.i
    .line 16707566,16707566 : 0,0 ''
    IL_007e:  nop
    IL_007f:  br.s       IL_0089

    .line 16707566,16707566 : 0,0 ''
    IL_0081:  ldc.i4.0
    IL_0082:  conv.i
    .line 16707566,16707566 : 0,0 ''
    IL_0083:  nop
    IL_0084:  br.s       IL_0089

    .line 16707566,16707566 : 0,0 ''
    IL_0086:  ldc.i4.0
    IL_0087:  conv.i
    .line 16707566,16707566 : 0,0 ''
    IL_0088:  nop
    .line 16707566,16707566 : 0,0 ''
    IL_0089:  stloc.1
    .line 19,19 : 5,44 ''
    IL_008a:  ldloc.1
    IL_008b:  stloc.s    V_4
    IL_008d:  ldc.i4.0
    IL_008e:  stloc.s    V_5
    IL_0090:  ldloc.s    V_4
    IL_0092:  ldloc.s    V_5
    IL_0094:  conv.i
    IL_0095:  sizeof     [mscorlib]System.Double
    IL_009b:  mul
    IL_009c:  add
    IL_009d:  ldobj      [mscorlib]System.Double
    IL_00a2:  ldloc.1
    IL_00a3:  stloc.s    V_6
    IL_00a5:  ldc.i4.1
    IL_00a6:  stloc.s    V_7
    IL_00a8:  ldloc.s    V_6
    IL_00aa:  ldloc.s    V_7
    IL_00ac:  conv.i
    IL_00ad:  sizeof     [mscorlib]System.Double
    IL_00b3:  mul
    IL_00b4:  add
    IL_00b5:  ldobj      [mscorlib]System.Double
    IL_00ba:  add
    IL_00bb:  ret
  } // end of method TestFunction24::pinArray1

  .method public static float64  pinArray2() cil managed
  {
    // Code size       162 (0xa2)
    .maxstack  6
    .locals init ([0] float64[] arr,
             [1] native int p,
             [2] float64& pinned V_2,
             [3] native int V_3,
             [4] int32 V_4,
             [5] native int V_5,
             [6] int32 V_6)
    .line 22,22 : 5,49 ''
    IL_0000:  ldc.i4.6
    IL_0001:  newarr     [mscorlib]System.Double
    IL_0006:  dup
    IL_0007:  ldc.i4.0
    IL_0008:  ldc.r8     0.0
    IL_0011:  stelem     [mscorlib]System.Double
    IL_0016:  dup
    IL_0017:  ldc.i4.1
    IL_0018:  ldc.r8     1.5
    IL_0021:  stelem     [mscorlib]System.Double
    IL_0026:  dup
    IL_0027:  ldc.i4.2
    IL_0028:  ldc.r8     2.2999999999999998
    IL_0031:  stelem     [mscorlib]System.Double
    IL_0036:  dup
    IL_0037:  ldc.i4.3
    IL_0038:  ldc.r8     3.3999999999999999
    IL_0041:  stelem     [mscorlib]System.Double
    IL_0046:  dup
    IL_0047:  ldc.i4.4
    IL_0048:  ldc.r8     4.
    IL_0051:  stelem     [mscorlib]System.Double
    IL_0056:  dup
    IL_0057:  ldc.i4.5
    IL_0058:  ldc.r8     5.9000000000000004
    IL_0061:  stelem     [mscorlib]System.Double
    IL_0066:  stloc.0
    .line 24,24 : 5,27 ''
    IL_0067:  ldloc.0
    IL_0068:  ldc.i4.0
    IL_0069:  ldelema    [mscorlib]System.Double
    IL_006e:  stloc.2
    IL_006f:  ldloc.2
    IL_0070:  conv.i
    IL_0071:  stloc.1
    .line 25,25 : 5,42 ''
    IL_0072:  ldloc.1
    IL_0073:  stloc.3
    IL_0074:  ldc.i4.0
    IL_0075:  stloc.s    V_4
    IL_0077:  ldloc.3
    IL_0078:  ldloc.s    V_4
    IL_007a:  conv.i
    IL_007b:  sizeof     [mscorlib]System.Double
    IL_0081:  mul
    IL_0082:  add
    IL_0083:  ldobj      [mscorlib]System.Double
    IL_0088:  ldloc.1
    IL_0089:  stloc.s    V_5
    IL_008b:  ldc.i4.1
    IL_008c:  stloc.s    V_6
    IL_008e:  ldloc.s    V_5
    IL_0090:  ldloc.s    V_6
    IL_0092:  conv.i
    IL_0093:  sizeof     [mscorlib]System.Double
    IL_0099:  mul
    IL_009a:  add
    IL_009b:  ldobj      [mscorlib]System.Double
    IL_00a0:  add
    IL_00a1:  ret
  } // end of method TestFunction24::pinArray2

  .method public static class [mscorlib]System.Tuple`2<char,char> 
          pinString() cil managed
  {
    // Code size       77 (0x4d)
    .maxstack  6
    .locals init ([0] string str,
             [1] native int pChar,
             [2] string pinned V_2,
             [3] native int V_3,
             [4] int32 V_4,
             [5] native int V_5,
             [6] int32 V_6)
    .line 28,28 : 5,28 ''
    IL_0000:  ldstr      "Hello World"
    IL_0005:  stloc.0
    .line 30,30 : 5,26 ''
    IL_0006:  ldloc.0
    IL_0007:  stloc.2
    IL_0008:  ldloc.2
    IL_0009:  brfalse.s  IL_0016

    .line 16707566,16707566 : 0,0 ''
    IL_000b:  ldloc.2
    IL_000c:  conv.i
    IL_000d:  call       int32 [mscorlib]System.Runtime.CompilerServices.RuntimeHelpers::get_OffsetToStringData()
    IL_0012:  add
    .line 16707566,16707566 : 0,0 ''
    IL_0013:  nop
    IL_0014:  br.s       IL_0018

    .line 16707566,16707566 : 0,0 ''
    IL_0016:  ldloc.2
    .line 16707566,16707566 : 0,0 ''
    IL_0017:  nop
    .line 16707566,16707566 : 0,0 ''
    IL_0018:  stloc.1
    .line 31,31 : 5,50 ''
    IL_0019:  ldloc.1
    IL_001a:  stloc.3
    IL_001b:  ldc.i4.0
    IL_001c:  stloc.s    V_4
    IL_001e:  ldloc.3
    IL_001f:  ldloc.s    V_4
    IL_0021:  conv.i
    IL_0022:  sizeof     [mscorlib]System.Char
    IL_0028:  mul
    IL_0029:  add
    IL_002a:  ldobj      [mscorlib]System.Char
    IL_002f:  ldloc.1
    IL_0030:  stloc.s    V_5
    IL_0032:  ldc.i4.1
    IL_0033:  stloc.s    V_6
    IL_0035:  ldloc.s    V_5
    IL_0037:  ldloc.s    V_6
    IL_0039:  conv.i
    IL_003a:  sizeof     [mscorlib]System.Char
    IL_0040:  mul
    IL_0041:  add
    IL_0042:  ldobj      [mscorlib]System.Char
    IL_0047:  newobj     instance void class [mscorlib]System.Tuple`2<char,char>::.ctor(!0,
                                                                                        !1)
    IL_004c:  ret
  } // end of method TestFunction24::pinString

} // end of class TestFunction24

.class private abstract auto ansi sealed '<StartupCode$TestFunction24>'.$TestFunction24
       extends [mscorlib]System.Object
{
  .method public static void  main@() cil managed
  {
    .entrypoint
    // Code size       1 (0x1)
    .maxstack  8
    IL_0000:  ret
  } // end of method $TestFunction24::main@

} // end of class '<StartupCode$TestFunction24>'.$TestFunction24


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
