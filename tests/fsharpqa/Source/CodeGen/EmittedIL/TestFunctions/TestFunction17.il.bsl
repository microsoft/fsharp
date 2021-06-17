
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
.assembly TestFunction17
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.TestFunction17
{
  // Offset: 0x00000000 Length: 0x0000066E
}
.mresource public FSharpOptimizationData.TestFunction17
{
  // Offset: 0x00000678 Length: 0x000001CD
}
.module TestFunction17.exe
// MVID: {60B68B97-A624-45A8-A745-0383978BB660}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x06AA0000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed TestFunction17
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class auto ansi serializable sealed nested public R
         extends [mscorlib]System.Object
         implements class [mscorlib]System.IEquatable`1<class TestFunction17/R>,
                    [mscorlib]System.Collections.IStructuralEquatable,
                    class [mscorlib]System.IComparable`1<class TestFunction17/R>,
                    [mscorlib]System.IComparable,
                    [mscorlib]System.Collections.IStructuralComparable
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 02 00 00 00 00 00 ) 
    .field assembly int32 x@
    .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
    .field assembly int32 y@
    .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
    .method public hidebysig specialname 
            instance int32  get_x() cil managed
    {
      // Code size       7 (0x7)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  ldfld      int32 TestFunction17/R::x@
      IL_0006:  ret
    } // end of method R::get_x

    .method public hidebysig specialname 
            instance int32  get_y() cil managed
    {
      // Code size       7 (0x7)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  ldfld      int32 TestFunction17/R::y@
      IL_0006:  ret
    } // end of method R::get_y

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
      IL_0008:  stfld      int32 TestFunction17/R::x@
      IL_000d:  ldarg.0
      IL_000e:  ldarg.2
      IL_000f:  stfld      int32 TestFunction17/R::y@
      IL_0014:  ret
    } // end of method R::.ctor

    .method public strict virtual instance string 
            ToString() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       22 (0x16)
      .maxstack  8
      IL_0000:  ldstr      "%+A"
      IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction17/R,string>,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string,class TestFunction17/R>::.ctor(string)
      IL_000a:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatToString<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction17/R,string>>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string>)
      IL_000f:  ldarg.0
      IL_0010:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction17/R,string>::Invoke(!0)
      IL_0015:  ret
    } // end of method R::ToString

    .method public hidebysig virtual final 
            instance int32  CompareTo(class TestFunction17/R obj) cil managed
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
      .line 100001,100001 : 0,0 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\TestFunctions\\TestFunction17.fs'
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse.s  IL_0062

      .line 100001,100001 : 0,0 ''
      IL_0006:  ldarg.1
      IL_0007:  ldnull
      IL_0008:  cgt.un
      IL_000a:  brfalse.s  IL_0060

      .line 100001,100001 : 0,0 ''
      IL_000c:  call       class [mscorlib]System.Collections.IComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericComparer()
      IL_0011:  stloc.1
      IL_0012:  ldarg.0
      IL_0013:  ldfld      int32 TestFunction17/R::x@
      IL_0018:  stloc.2
      IL_0019:  ldarg.1
      IL_001a:  ldfld      int32 TestFunction17/R::x@
      IL_001f:  stloc.3
      IL_0020:  ldloc.2
      IL_0021:  ldloc.3
      IL_0022:  bge.s      IL_0028

      .line 100001,100001 : 0,0 ''
      IL_0024:  ldc.i4.m1
      .line 100001,100001 : 0,0 ''
      IL_0025:  nop
      IL_0026:  br.s       IL_002d

      .line 100001,100001 : 0,0 ''
      IL_0028:  ldloc.2
      IL_0029:  ldloc.3
      IL_002a:  cgt
      .line 100001,100001 : 0,0 ''
      IL_002c:  nop
      .line 100001,100001 : 0,0 ''
      IL_002d:  stloc.0
      IL_002e:  ldloc.0
      IL_002f:  ldc.i4.0
      IL_0030:  bge.s      IL_0034

      .line 100001,100001 : 0,0 ''
      IL_0032:  ldloc.0
      IL_0033:  ret

      .line 100001,100001 : 0,0 ''
      IL_0034:  ldloc.0
      IL_0035:  ldc.i4.0
      IL_0036:  ble.s      IL_003a

      .line 100001,100001 : 0,0 ''
      IL_0038:  ldloc.0
      IL_0039:  ret

      .line 100001,100001 : 0,0 ''
      IL_003a:  call       class [mscorlib]System.Collections.IComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericComparer()
      IL_003f:  stloc.s    V_4
      IL_0041:  ldarg.0
      IL_0042:  ldfld      int32 TestFunction17/R::y@
      IL_0047:  stloc.s    V_5
      IL_0049:  ldarg.1
      IL_004a:  ldfld      int32 TestFunction17/R::y@
      IL_004f:  stloc.s    V_6
      IL_0051:  ldloc.s    V_5
      IL_0053:  ldloc.s    V_6
      IL_0055:  bge.s      IL_0059

      .line 100001,100001 : 0,0 ''
      IL_0057:  ldc.i4.m1
      IL_0058:  ret

      .line 100001,100001 : 0,0 ''
      IL_0059:  ldloc.s    V_5
      IL_005b:  ldloc.s    V_6
      IL_005d:  cgt
      IL_005f:  ret

      .line 100001,100001 : 0,0 ''
      IL_0060:  ldc.i4.1
      IL_0061:  ret

      .line 100001,100001 : 0,0 ''
      IL_0062:  ldarg.1
      IL_0063:  ldnull
      IL_0064:  cgt.un
      IL_0066:  brfalse.s  IL_006a

      .line 100001,100001 : 0,0 ''
      IL_0068:  ldc.i4.m1
      IL_0069:  ret

      .line 100001,100001 : 0,0 ''
      IL_006a:  ldc.i4.0
      IL_006b:  ret
    } // end of method R::CompareTo

    .method public hidebysig virtual final 
            instance int32  CompareTo(object obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       13 (0xd)
      .maxstack  8
      .line 4,4 : 6,7 ''
      IL_0000:  ldarg.0
      IL_0001:  ldarg.1
      IL_0002:  unbox.any  TestFunction17/R
      IL_0007:  callvirt   instance int32 TestFunction17/R::CompareTo(class TestFunction17/R)
      IL_000c:  ret
    } // end of method R::CompareTo

    .method public hidebysig virtual final 
            instance int32  CompareTo(object obj,
                                      class [mscorlib]System.Collections.IComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       125 (0x7d)
      .maxstack  4
      .locals init ([0] class TestFunction17/R V_0,
               [1] class TestFunction17/R V_1,
               [2] int32 V_2,
               [3] class [mscorlib]System.Collections.IComparer V_3,
               [4] int32 V_4,
               [5] int32 V_5,
               [6] class [mscorlib]System.Collections.IComparer V_6,
               [7] int32 V_7,
               [8] int32 V_8)
      .line 4,4 : 6,7 ''
      IL_0000:  ldarg.1
      IL_0001:  unbox.any  TestFunction17/R
      IL_0006:  stloc.0
      IL_0007:  ldloc.0
      IL_0008:  stloc.1
      IL_0009:  ldarg.0
      IL_000a:  ldnull
      IL_000b:  cgt.un
      IL_000d:  brfalse.s  IL_006e

      .line 100001,100001 : 0,0 ''
      IL_000f:  ldarg.1
      IL_0010:  unbox.any  TestFunction17/R
      IL_0015:  ldnull
      IL_0016:  cgt.un
      IL_0018:  brfalse.s  IL_006c

      .line 100001,100001 : 0,0 ''
      IL_001a:  ldarg.2
      IL_001b:  stloc.3
      IL_001c:  ldarg.0
      IL_001d:  ldfld      int32 TestFunction17/R::x@
      IL_0022:  stloc.s    V_4
      IL_0024:  ldloc.1
      IL_0025:  ldfld      int32 TestFunction17/R::x@
      IL_002a:  stloc.s    V_5
      IL_002c:  ldloc.s    V_4
      IL_002e:  ldloc.s    V_5
      IL_0030:  bge.s      IL_0036

      .line 100001,100001 : 0,0 ''
      IL_0032:  ldc.i4.m1
      .line 100001,100001 : 0,0 ''
      IL_0033:  nop
      IL_0034:  br.s       IL_003d

      .line 100001,100001 : 0,0 ''
      IL_0036:  ldloc.s    V_4
      IL_0038:  ldloc.s    V_5
      IL_003a:  cgt
      .line 100001,100001 : 0,0 ''
      IL_003c:  nop
      .line 100001,100001 : 0,0 ''
      IL_003d:  stloc.2
      IL_003e:  ldloc.2
      IL_003f:  ldc.i4.0
      IL_0040:  bge.s      IL_0044

      .line 100001,100001 : 0,0 ''
      IL_0042:  ldloc.2
      IL_0043:  ret

      .line 100001,100001 : 0,0 ''
      IL_0044:  ldloc.2
      IL_0045:  ldc.i4.0
      IL_0046:  ble.s      IL_004a

      .line 100001,100001 : 0,0 ''
      IL_0048:  ldloc.2
      IL_0049:  ret

      .line 100001,100001 : 0,0 ''
      IL_004a:  ldarg.2
      IL_004b:  stloc.s    V_6
      IL_004d:  ldarg.0
      IL_004e:  ldfld      int32 TestFunction17/R::y@
      IL_0053:  stloc.s    V_7
      IL_0055:  ldloc.1
      IL_0056:  ldfld      int32 TestFunction17/R::y@
      IL_005b:  stloc.s    V_8
      IL_005d:  ldloc.s    V_7
      IL_005f:  ldloc.s    V_8
      IL_0061:  bge.s      IL_0065

      .line 100001,100001 : 0,0 ''
      IL_0063:  ldc.i4.m1
      IL_0064:  ret

      .line 100001,100001 : 0,0 ''
      IL_0065:  ldloc.s    V_7
      IL_0067:  ldloc.s    V_8
      IL_0069:  cgt
      IL_006b:  ret

      .line 100001,100001 : 0,0 ''
      IL_006c:  ldc.i4.1
      IL_006d:  ret

      .line 100001,100001 : 0,0 ''
      IL_006e:  ldarg.1
      IL_006f:  unbox.any  TestFunction17/R
      IL_0074:  ldnull
      IL_0075:  cgt.un
      IL_0077:  brfalse.s  IL_007b

      .line 100001,100001 : 0,0 ''
      IL_0079:  ldc.i4.m1
      IL_007a:  ret

      .line 100001,100001 : 0,0 ''
      IL_007b:  ldc.i4.0
      IL_007c:  ret
    } // end of method R::CompareTo

    .method public hidebysig virtual final 
            instance int32  GetHashCode(class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       58 (0x3a)
      .maxstack  7
      .locals init ([0] int32 V_0,
               [1] class [mscorlib]System.Collections.IEqualityComparer V_1,
               [2] class [mscorlib]System.Collections.IEqualityComparer V_2)
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse.s  IL_0038

      .line 100001,100001 : 0,0 ''
      IL_0006:  ldc.i4.0
      IL_0007:  stloc.0
      IL_0008:  ldc.i4     0x9e3779b9
      IL_000d:  ldarg.1
      IL_000e:  stloc.1
      IL_000f:  ldarg.0
      IL_0010:  ldfld      int32 TestFunction17/R::y@
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
      IL_0027:  ldfld      int32 TestFunction17/R::x@
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

      .line 100001,100001 : 0,0 ''
      IL_0038:  ldc.i4.0
      IL_0039:  ret
    } // end of method R::GetHashCode

    .method public hidebysig virtual final 
            instance int32  GetHashCode() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       12 (0xc)
      .maxstack  8
      .line 4,4 : 6,7 ''
      IL_0000:  ldarg.0
      IL_0001:  call       class [mscorlib]System.Collections.IEqualityComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericEqualityComparer()
      IL_0006:  callvirt   instance int32 TestFunction17/R::GetHashCode(class [mscorlib]System.Collections.IEqualityComparer)
      IL_000b:  ret
    } // end of method R::GetHashCode

    .method public hidebysig virtual final 
            instance bool  Equals(object obj,
                                  class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       65 (0x41)
      .maxstack  4
      .locals init ([0] class TestFunction17/R V_0,
               [1] class TestFunction17/R V_1,
               [2] class [mscorlib]System.Collections.IEqualityComparer V_2,
               [3] class [mscorlib]System.Collections.IEqualityComparer V_3)
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse.s  IL_0039

      .line 100001,100001 : 0,0 ''
      IL_0006:  ldarg.1
      IL_0007:  isinst     TestFunction17/R
      IL_000c:  stloc.0
      IL_000d:  ldloc.0
      IL_000e:  brfalse.s  IL_0037

      .line 100001,100001 : 0,0 ''
      IL_0010:  ldloc.0
      IL_0011:  stloc.1
      IL_0012:  ldarg.2
      IL_0013:  stloc.2
      IL_0014:  ldarg.0
      IL_0015:  ldfld      int32 TestFunction17/R::x@
      IL_001a:  ldloc.1
      IL_001b:  ldfld      int32 TestFunction17/R::x@
      IL_0020:  ceq
      IL_0022:  brfalse.s  IL_0035

      .line 100001,100001 : 0,0 ''
      IL_0024:  ldarg.2
      IL_0025:  stloc.3
      IL_0026:  ldarg.0
      IL_0027:  ldfld      int32 TestFunction17/R::y@
      IL_002c:  ldloc.1
      IL_002d:  ldfld      int32 TestFunction17/R::y@
      IL_0032:  ceq
      IL_0034:  ret

      .line 100001,100001 : 0,0 ''
      IL_0035:  ldc.i4.0
      IL_0036:  ret

      .line 100001,100001 : 0,0 ''
      IL_0037:  ldc.i4.0
      IL_0038:  ret

      .line 100001,100001 : 0,0 ''
      IL_0039:  ldarg.1
      IL_003a:  ldnull
      IL_003b:  cgt.un
      IL_003d:  ldc.i4.0
      IL_003e:  ceq
      IL_0040:  ret
    } // end of method R::Equals

    .method public hidebysig virtual final 
            instance bool  Equals(class TestFunction17/R obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       53 (0x35)
      .maxstack  8
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse.s  IL_002d

      .line 100001,100001 : 0,0 ''
      IL_0006:  ldarg.1
      IL_0007:  ldnull
      IL_0008:  cgt.un
      IL_000a:  brfalse.s  IL_002b

      .line 100001,100001 : 0,0 ''
      IL_000c:  ldarg.0
      IL_000d:  ldfld      int32 TestFunction17/R::x@
      IL_0012:  ldarg.1
      IL_0013:  ldfld      int32 TestFunction17/R::x@
      IL_0018:  bne.un.s   IL_0029

      .line 100001,100001 : 0,0 ''
      IL_001a:  ldarg.0
      IL_001b:  ldfld      int32 TestFunction17/R::y@
      IL_0020:  ldarg.1
      IL_0021:  ldfld      int32 TestFunction17/R::y@
      IL_0026:  ceq
      IL_0028:  ret

      .line 100001,100001 : 0,0 ''
      IL_0029:  ldc.i4.0
      IL_002a:  ret

      .line 100001,100001 : 0,0 ''
      IL_002b:  ldc.i4.0
      IL_002c:  ret

      .line 100001,100001 : 0,0 ''
      IL_002d:  ldarg.1
      IL_002e:  ldnull
      IL_002f:  cgt.un
      IL_0031:  ldc.i4.0
      IL_0032:  ceq
      IL_0034:  ret
    } // end of method R::Equals

    .method public hidebysig virtual final 
            instance bool  Equals(object obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       20 (0x14)
      .maxstack  4
      .locals init ([0] class TestFunction17/R V_0)
      .line 4,4 : 6,7 ''
      IL_0000:  ldarg.1
      IL_0001:  isinst     TestFunction17/R
      IL_0006:  stloc.0
      IL_0007:  ldloc.0
      IL_0008:  brfalse.s  IL_0012

      .line 100001,100001 : 0,0 ''
      IL_000a:  ldarg.0
      IL_000b:  ldloc.0
      IL_000c:  callvirt   instance bool TestFunction17/R::Equals(class TestFunction17/R)
      IL_0011:  ret

      .line 100001,100001 : 0,0 ''
      IL_0012:  ldc.i4.0
      IL_0013:  ret
    } // end of method R::Equals

    .property instance int32 x()
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                  int32) = ( 01 00 04 00 00 00 00 00 00 00 00 00 ) 
      .get instance int32 TestFunction17/R::get_x()
    } // end of property R::x
    .property instance int32 y()
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                  int32) = ( 01 00 04 00 00 00 01 00 00 00 00 00 ) 
      .get instance int32 TestFunction17/R::get_y()
    } // end of property R::y
  } // end of class R

  .method public static class [mscorlib]System.Tuple`2<class TestFunction17/R,class TestFunction17/R> 
          TestFunction17(int32 inp) cil managed
  {
    // Code size       16 (0x10)
    .maxstack  4
    .locals init ([0] class TestFunction17/R x)
    .line 7,7 : 5,24 ''
    IL_0000:  ldc.i4.3
    IL_0001:  ldarg.0
    IL_0002:  newobj     instance void TestFunction17/R::.ctor(int32,
                                                               int32)
    IL_0007:  stloc.0
    .line 8,8 : 5,8 ''
    IL_0008:  ldloc.0
    IL_0009:  ldloc.0
    IL_000a:  newobj     instance void class [mscorlib]System.Tuple`2<class TestFunction17/R,class TestFunction17/R>::.ctor(!0,
                                                                                                                            !1)
    IL_000f:  ret
  } // end of method TestFunction17::TestFunction17

} // end of class TestFunction17

.class private abstract auto ansi sealed '<StartupCode$TestFunction17>'.$TestFunction17
       extends [mscorlib]System.Object
{
  .method public static void  main@() cil managed
  {
    .entrypoint
    // Code size       1 (0x1)
    .maxstack  8
    IL_0000:  ret
  } // end of method $TestFunction17::main@

} // end of class '<StartupCode$TestFunction17>'.$TestFunction17


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
