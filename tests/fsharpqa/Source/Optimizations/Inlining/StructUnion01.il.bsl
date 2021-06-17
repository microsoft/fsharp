
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
.assembly StructUnion01
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 
  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.StructUnion01
{
  // Offset: 0x00000000 Length: 0x0000087A
}
.mresource public FSharpOptimizationData.StructUnion01
{
  // Offset: 0x00000880 Length: 0x00000421
}
.module StructUnion01.dll
// MVID: {60B6A4C9-D3E9-6B24-A745-0383C9A4B660}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x06CE0000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed StructUnion01
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class sequential autochar serializable sealed nested public beforefieldinit U
         extends [mscorlib]System.ValueType
         implements class [mscorlib]System.IEquatable`1<valuetype StructUnion01/U>,
                    [mscorlib]System.Collections.IStructuralEquatable,
                    class [mscorlib]System.IComparable`1<valuetype StructUnion01/U>,
                    [mscorlib]System.IComparable,
                    [mscorlib]System.Collections.IStructuralComparable
  {
    .pack 0
    .size 1
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.StructAttribute::.ctor() = ( 01 00 00 00 ) 
    .custom instance void [mscorlib]System.Diagnostics.DebuggerDisplayAttribute::.ctor(string) = ( 01 00 15 7B 5F 5F 44 65 62 75 67 44 69 73 70 6C   // ...{__DebugDispl
                                                                                                   61 79 28 29 2C 6E 71 7D 00 00 )                   // ay(),nq}..
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 01 00 00 00 00 00 ) 
    .field assembly int32 item1
    .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
    .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
    .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
    .field assembly int32 item2
    .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
    .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
    .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
    .method public static valuetype StructUnion01/U 
            NewU(int32 item1,
                 int32 item2) cil managed
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                  int32) = ( 01 00 08 00 00 00 00 00 00 00 00 00 ) 
      // Code size       8 (0x8)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  ldarg.1
      IL_0002:  newobj     instance void StructUnion01/U::.ctor(int32,
                                                                int32)
      IL_0007:  ret
    } // end of method U::NewU

    .method assembly specialname rtspecialname 
            instance void  .ctor(int32 item1,
                                 int32 item2) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       15 (0xf)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  ldarg.1
      IL_0002:  stfld      int32 StructUnion01/U::item1
      IL_0007:  ldarg.0
      IL_0008:  ldarg.2
      IL_0009:  stfld      int32 StructUnion01/U::item2
      IL_000e:  ret
    } // end of method U::.ctor

    .method public hidebysig instance int32 
            get_Item1() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       7 (0x7)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  ldfld      int32 StructUnion01/U::item1
      IL_0006:  ret
    } // end of method U::get_Item1

    .method public hidebysig instance int32 
            get_Item2() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       7 (0x7)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  ldfld      int32 StructUnion01/U::item2
      IL_0006:  ret
    } // end of method U::get_Item2

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
    } // end of method U::get_Tag

    .method assembly hidebysig specialname 
            instance object  __DebugDisplay() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       27 (0x1b)
      .maxstack  8
      IL_0000:  ldstr      "%+0.8A"
      IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype StructUnion01/U,string>,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string,string>::.ctor(string)
      IL_000a:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatToString<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype StructUnion01/U,string>>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string>)
      IL_000f:  ldarg.0
      IL_0010:  ldobj      StructUnion01/U
      IL_0015:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype StructUnion01/U,string>::Invoke(!0)
      IL_001a:  ret
    } // end of method U::__DebugDisplay

    .method public strict virtual instance string 
            ToString() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       27 (0x1b)
      .maxstack  8
      IL_0000:  ldstr      "%+A"
      IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype StructUnion01/U,string>,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string,valuetype StructUnion01/U>::.ctor(string)
      IL_000a:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatToString<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype StructUnion01/U,string>>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string>)
      IL_000f:  ldarg.0
      IL_0010:  ldobj      StructUnion01/U
      IL_0015:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<valuetype StructUnion01/U,string>::Invoke(!0)
      IL_001a:  ret
    } // end of method U::ToString

    .method public hidebysig virtual final 
            instance int32  CompareTo(valuetype StructUnion01/U obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       79 (0x4f)
      .maxstack  4
      .locals init (int32 V_0,
               class [mscorlib]System.Collections.IComparer V_1,
               int32 V_2,
               int32 V_3)
      IL_0000:  ldarg.0
      IL_0001:  pop
      IL_0002:  call       class [mscorlib]System.Collections.IComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericComparer()
      IL_0007:  stloc.1
      IL_0008:  ldarg.0
      IL_0009:  ldfld      int32 StructUnion01/U::item1
      IL_000e:  stloc.2
      IL_000f:  ldarga.s   obj
      IL_0011:  ldfld      int32 StructUnion01/U::item1
      IL_0016:  stloc.3
      IL_0017:  ldloc.2
      IL_0018:  ldloc.3
      IL_0019:  bge.s      IL_001e

      IL_001b:  ldc.i4.m1
      IL_001c:  br.s       IL_0022

      IL_001e:  ldloc.2
      IL_001f:  ldloc.3
      IL_0020:  cgt
      IL_0022:  stloc.0
      IL_0023:  ldloc.0
      IL_0024:  ldc.i4.0
      IL_0025:  bge.s      IL_0029

      IL_0027:  ldloc.0
      IL_0028:  ret

      IL_0029:  ldloc.0
      IL_002a:  ldc.i4.0
      IL_002b:  ble.s      IL_002f

      IL_002d:  ldloc.0
      IL_002e:  ret

      IL_002f:  call       class [mscorlib]System.Collections.IComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericComparer()
      IL_0034:  stloc.1
      IL_0035:  ldarg.0
      IL_0036:  ldfld      int32 StructUnion01/U::item2
      IL_003b:  stloc.2
      IL_003c:  ldarga.s   obj
      IL_003e:  ldfld      int32 StructUnion01/U::item2
      IL_0043:  stloc.3
      IL_0044:  ldloc.2
      IL_0045:  ldloc.3
      IL_0046:  bge.s      IL_004a

      IL_0048:  ldc.i4.m1
      IL_0049:  ret

      IL_004a:  ldloc.2
      IL_004b:  ldloc.3
      IL_004c:  cgt
      IL_004e:  ret
    } // end of method U::CompareTo

    .method public hidebysig virtual final 
            instance int32  CompareTo(object obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       13 (0xd)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  ldarg.1
      IL_0002:  unbox.any  StructUnion01/U
      IL_0007:  call       instance int32 StructUnion01/U::CompareTo(valuetype StructUnion01/U)
      IL_000c:  ret
    } // end of method U::CompareTo

    .method public hidebysig virtual final 
            instance int32  CompareTo(object obj,
                                      class [mscorlib]System.Collections.IComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       74 (0x4a)
      .maxstack  4
      .locals init (valuetype StructUnion01/U V_0,
               int32 V_1,
               int32 V_2,
               int32 V_3)
      IL_0000:  ldarg.1
      IL_0001:  unbox.any  StructUnion01/U
      IL_0006:  stloc.0
      IL_0007:  ldarg.0
      IL_0008:  pop
      IL_0009:  ldarg.0
      IL_000a:  ldfld      int32 StructUnion01/U::item1
      IL_000f:  stloc.2
      IL_0010:  ldloca.s   V_0
      IL_0012:  ldfld      int32 StructUnion01/U::item1
      IL_0017:  stloc.3
      IL_0018:  ldloc.2
      IL_0019:  ldloc.3
      IL_001a:  bge.s      IL_001f

      IL_001c:  ldc.i4.m1
      IL_001d:  br.s       IL_0023

      IL_001f:  ldloc.2
      IL_0020:  ldloc.3
      IL_0021:  cgt
      IL_0023:  stloc.1
      IL_0024:  ldloc.1
      IL_0025:  ldc.i4.0
      IL_0026:  bge.s      IL_002a

      IL_0028:  ldloc.1
      IL_0029:  ret

      IL_002a:  ldloc.1
      IL_002b:  ldc.i4.0
      IL_002c:  ble.s      IL_0030

      IL_002e:  ldloc.1
      IL_002f:  ret

      IL_0030:  ldarg.0
      IL_0031:  ldfld      int32 StructUnion01/U::item2
      IL_0036:  stloc.2
      IL_0037:  ldloca.s   V_0
      IL_0039:  ldfld      int32 StructUnion01/U::item2
      IL_003e:  stloc.3
      IL_003f:  ldloc.2
      IL_0040:  ldloc.3
      IL_0041:  bge.s      IL_0045

      IL_0043:  ldc.i4.m1
      IL_0044:  ret

      IL_0045:  ldloc.2
      IL_0046:  ldloc.3
      IL_0047:  cgt
      IL_0049:  ret
    } // end of method U::CompareTo

    .method public hidebysig virtual final 
            instance int32  GetHashCode(class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       50 (0x32)
      .maxstack  7
      .locals init (int32 V_0)
      IL_0000:  ldc.i4.0
      IL_0001:  stloc.0
      IL_0002:  ldarg.0
      IL_0003:  pop
      IL_0004:  ldc.i4.0
      IL_0005:  stloc.0
      IL_0006:  ldc.i4     0x9e3779b9
      IL_000b:  ldarg.0
      IL_000c:  ldfld      int32 StructUnion01/U::item2
      IL_0011:  ldloc.0
      IL_0012:  ldc.i4.6
      IL_0013:  shl
      IL_0014:  ldloc.0
      IL_0015:  ldc.i4.2
      IL_0016:  shr
      IL_0017:  add
      IL_0018:  add
      IL_0019:  add
      IL_001a:  stloc.0
      IL_001b:  ldc.i4     0x9e3779b9
      IL_0020:  ldarg.0
      IL_0021:  ldfld      int32 StructUnion01/U::item1
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
    } // end of method U::GetHashCode

    .method public hidebysig virtual final 
            instance int32  GetHashCode() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       12 (0xc)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  call       class [mscorlib]System.Collections.IEqualityComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericEqualityComparer()
      IL_0006:  call       instance int32 StructUnion01/U::GetHashCode(class [mscorlib]System.Collections.IEqualityComparer)
      IL_000b:  ret
    } // end of method U::GetHashCode

    .method public hidebysig virtual final 
            instance bool  Equals(object obj,
                                  class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       54 (0x36)
      .maxstack  4
      .locals init (valuetype StructUnion01/U V_0)
      IL_0000:  ldarg.1
      IL_0001:  call       bool [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/IntrinsicFunctions::TypeTestGeneric<valuetype StructUnion01/U>(object)
      IL_0006:  brtrue.s   IL_000a

      IL_0008:  br.s       IL_0034

      IL_000a:  ldarg.1
      IL_000b:  unbox.any  StructUnion01/U
      IL_0010:  stloc.0
      IL_0011:  ldarg.0
      IL_0012:  pop
      IL_0013:  ldarg.0
      IL_0014:  ldfld      int32 StructUnion01/U::item1
      IL_0019:  ldloca.s   V_0
      IL_001b:  ldfld      int32 StructUnion01/U::item1
      IL_0020:  bne.un.s   IL_0032

      IL_0022:  ldarg.0
      IL_0023:  ldfld      int32 StructUnion01/U::item2
      IL_0028:  ldloca.s   V_0
      IL_002a:  ldfld      int32 StructUnion01/U::item2
      IL_002f:  ceq
      IL_0031:  ret

      IL_0032:  ldc.i4.0
      IL_0033:  ret

      IL_0034:  ldc.i4.0
      IL_0035:  ret
    } // end of method U::Equals

    .method public hidebysig virtual final 
            instance bool  Equals(valuetype StructUnion01/U obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       35 (0x23)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  pop
      IL_0002:  ldarg.0
      IL_0003:  ldfld      int32 StructUnion01/U::item1
      IL_0008:  ldarga.s   obj
      IL_000a:  ldfld      int32 StructUnion01/U::item1
      IL_000f:  bne.un.s   IL_0021

      IL_0011:  ldarg.0
      IL_0012:  ldfld      int32 StructUnion01/U::item2
      IL_0017:  ldarga.s   obj
      IL_0019:  ldfld      int32 StructUnion01/U::item2
      IL_001e:  ceq
      IL_0020:  ret

      IL_0021:  ldc.i4.0
      IL_0022:  ret
    } // end of method U::Equals

    .method public hidebysig virtual final 
            instance bool  Equals(object obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       25 (0x19)
      .maxstack  8
      IL_0000:  ldarg.1
      IL_0001:  call       bool [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/IntrinsicFunctions::TypeTestGeneric<valuetype StructUnion01/U>(object)
      IL_0006:  brtrue.s   IL_000a

      IL_0008:  br.s       IL_0017

      IL_000a:  ldarg.0
      IL_000b:  ldarg.1
      IL_000c:  unbox.any  StructUnion01/U
      IL_0011:  call       instance bool StructUnion01/U::Equals(valuetype StructUnion01/U)
      IL_0016:  ret

      IL_0017:  ldc.i4.0
      IL_0018:  ret
    } // end of method U::Equals

    .property instance int32 Tag()
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .get instance int32 StructUnion01/U::get_Tag()
    } // end of property U::Tag
    .property instance int32 Item1()
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                  int32,
                                                                                                  int32) = ( 01 00 04 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .get instance int32 StructUnion01/U::get_Item1()
    } // end of property U::Item1
    .property instance int32 Item2()
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                  int32,
                                                                                                  int32) = ( 01 00 04 00 00 00 00 00 00 00 01 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .get instance int32 StructUnion01/U::get_Item2()
    } // end of property U::Item2
  } // end of class U

  .method public static int32  g1(valuetype StructUnion01/U _arg1) cil managed
  {
    // Code size       16 (0x10)
    .maxstack  8
    IL_0000:  ldarga.s   _arg1
    IL_0002:  ldfld      int32 StructUnion01/U::item1
    IL_0007:  ldarga.s   _arg1
    IL_0009:  ldfld      int32 StructUnion01/U::item2
    IL_000e:  add
    IL_000f:  ret
  } // end of method StructUnion01::g1

  .method public static int32  g2(valuetype StructUnion01/U u) cil managed
  {
    // Code size       16 (0x10)
    .maxstack  8
    IL_0000:  ldarga.s   u
    IL_0002:  ldfld      int32 StructUnion01/U::item1
    IL_0007:  ldarga.s   u
    IL_0009:  ldfld      int32 StructUnion01/U::item2
    IL_000e:  add
    IL_000f:  ret
  } // end of method StructUnion01::g2

  .method public static int32  g3(valuetype StructUnion01/U x) cil managed
  {
    // Code size       44 (0x2c)
    .maxstack  8
    IL_0000:  ldarga.s   x
    IL_0002:  ldfld      int32 StructUnion01/U::item1
    IL_0007:  ldc.i4.3
    IL_0008:  sub
    IL_0009:  switch     ( 
                          IL_0014)
    IL_0012:  br.s       IL_001c

    IL_0014:  ldarga.s   x
    IL_0016:  ldfld      int32 StructUnion01/U::item2
    IL_001b:  ret

    IL_001c:  ldarga.s   x
    IL_001e:  ldfld      int32 StructUnion01/U::item1
    IL_0023:  ldarga.s   x
    IL_0025:  ldfld      int32 StructUnion01/U::item2
    IL_002a:  add
    IL_002b:  ret
  } // end of method StructUnion01::g3

  .method public static int32  g4(valuetype StructUnion01/U x,
                                  valuetype StructUnion01/U y) cil managed
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationArgumentCountsAttribute::.ctor(int32[]) = ( 01 00 02 00 00 00 01 00 00 00 01 00 00 00 00 00 ) 
    // Code size       128 (0x80)
    .maxstack  6
    .locals init (int32 V_0,
             int32 V_1,
             int32 V_2,
             int32 V_3)
    IL_0000:  ldarga.s   x
    IL_0002:  ldfld      int32 StructUnion01/U::item1
    IL_0007:  ldc.i4.3
    IL_0008:  sub
    IL_0009:  switch     ( 
                          IL_0014)
    IL_0012:  br.s       IL_0058

    IL_0014:  ldarga.s   y
    IL_0016:  ldfld      int32 StructUnion01/U::item1
    IL_001b:  ldc.i4.5
    IL_001c:  sub
    IL_001d:  switch     ( 
                          IL_0048)
    IL_0026:  ldarga.s   y
    IL_0028:  ldfld      int32 StructUnion01/U::item2
    IL_002d:  ldarga.s   y
    IL_002f:  ldfld      int32 StructUnion01/U::item1
    IL_0034:  ldarga.s   x
    IL_0036:  ldfld      int32 StructUnion01/U::item2
    IL_003b:  ldarga.s   x
    IL_003d:  ldfld      int32 StructUnion01/U::item1
    IL_0042:  stloc.3
    IL_0043:  stloc.2
    IL_0044:  stloc.1
    IL_0045:  stloc.0
    IL_0046:  br.s       IL_0078

    IL_0048:  ldarga.s   x
    IL_004a:  ldfld      int32 StructUnion01/U::item2
    IL_004f:  ldarga.s   y
    IL_0051:  ldfld      int32 StructUnion01/U::item2
    IL_0056:  add
    IL_0057:  ret

    IL_0058:  ldarga.s   y
    IL_005a:  ldfld      int32 StructUnion01/U::item2
    IL_005f:  stloc.0
    IL_0060:  ldarga.s   y
    IL_0062:  ldfld      int32 StructUnion01/U::item1
    IL_0067:  stloc.1
    IL_0068:  ldarga.s   x
    IL_006a:  ldfld      int32 StructUnion01/U::item2
    IL_006f:  stloc.2
    IL_0070:  ldarga.s   x
    IL_0072:  ldfld      int32 StructUnion01/U::item1
    IL_0077:  stloc.3
    IL_0078:  ldloc.3
    IL_0079:  ldloc.2
    IL_007a:  add
    IL_007b:  ldloc.1
    IL_007c:  add
    IL_007d:  ldloc.0
    IL_007e:  add
    IL_007f:  ret
  } // end of method StructUnion01::g4

  .method public static int32  f1(valuetype StructUnion01/U& x) cil managed
  {
    // Code size       14 (0xe)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  ldfld      int32 StructUnion01/U::item1
    IL_0006:  ldarg.0
    IL_0007:  ldfld      int32 StructUnion01/U::item2
    IL_000c:  add
    IL_000d:  ret
  } // end of method StructUnion01::f1

  .method public static int32  f2(valuetype StructUnion01/U& x) cil managed
  {
    // Code size       14 (0xe)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  ldfld      int32 StructUnion01/U::item1
    IL_0006:  ldarg.0
    IL_0007:  ldfld      int32 StructUnion01/U::item2
    IL_000c:  add
    IL_000d:  ret
  } // end of method StructUnion01::f2

  .method public static int32  f3(valuetype StructUnion01/U& x) cil managed
  {
    // Code size       40 (0x28)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  ldfld      int32 StructUnion01/U::item1
    IL_0006:  ldc.i4.3
    IL_0007:  sub
    IL_0008:  switch     ( 
                          IL_0013)
    IL_0011:  br.s       IL_001a

    IL_0013:  ldarg.0
    IL_0014:  ldfld      int32 StructUnion01/U::item2
    IL_0019:  ret

    IL_001a:  ldarg.0
    IL_001b:  ldfld      int32 StructUnion01/U::item1
    IL_0020:  ldarg.0
    IL_0021:  ldfld      int32 StructUnion01/U::item2
    IL_0026:  add
    IL_0027:  ret
  } // end of method StructUnion01::f3

  .method public static int32  f4(valuetype StructUnion01/U& x,
                                  valuetype StructUnion01/U& y) cil managed
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationArgumentCountsAttribute::.ctor(int32[]) = ( 01 00 02 00 00 00 01 00 00 00 01 00 00 00 00 00 ) 
    // Code size       148 (0x94)
    .maxstack  6
    .locals init (valuetype StructUnion01/U V_0,
             valuetype StructUnion01/U V_1,
             int32 V_2,
             int32 V_3,
             int32 V_4,
             int32 V_5)
    IL_0000:  ldarg.0
    IL_0001:  ldobj      StructUnion01/U
    IL_0006:  stloc.0
    IL_0007:  ldarg.1
    IL_0008:  ldobj      StructUnion01/U
    IL_000d:  stloc.1
    IL_000e:  ldloca.s   V_0
    IL_0010:  ldfld      int32 StructUnion01/U::item1
    IL_0015:  ldc.i4.3
    IL_0016:  sub
    IL_0017:  switch     ( 
                          IL_0022)
    IL_0020:  br.s       IL_0068

    IL_0022:  ldloca.s   V_1
    IL_0024:  ldfld      int32 StructUnion01/U::item1
    IL_0029:  ldc.i4.5
    IL_002a:  sub
    IL_002b:  switch     ( 
                          IL_0058)
    IL_0034:  ldloca.s   V_1
    IL_0036:  ldfld      int32 StructUnion01/U::item2
    IL_003b:  ldloca.s   V_1
    IL_003d:  ldfld      int32 StructUnion01/U::item1
    IL_0042:  ldloca.s   V_0
    IL_0044:  ldfld      int32 StructUnion01/U::item2
    IL_0049:  ldloca.s   V_0
    IL_004b:  ldfld      int32 StructUnion01/U::item1
    IL_0050:  stloc.s    V_5
    IL_0052:  stloc.s    V_4
    IL_0054:  stloc.3
    IL_0055:  stloc.2
    IL_0056:  br.s       IL_008a

    IL_0058:  ldloca.s   V_0
    IL_005a:  ldfld      int32 StructUnion01/U::item2
    IL_005f:  ldloca.s   V_1
    IL_0061:  ldfld      int32 StructUnion01/U::item2
    IL_0066:  add
    IL_0067:  ret

    IL_0068:  ldloca.s   V_1
    IL_006a:  ldfld      int32 StructUnion01/U::item2
    IL_006f:  stloc.2
    IL_0070:  ldloca.s   V_1
    IL_0072:  ldfld      int32 StructUnion01/U::item1
    IL_0077:  stloc.3
    IL_0078:  ldloca.s   V_0
    IL_007a:  ldfld      int32 StructUnion01/U::item2
    IL_007f:  stloc.s    V_4
    IL_0081:  ldloca.s   V_0
    IL_0083:  ldfld      int32 StructUnion01/U::item1
    IL_0088:  stloc.s    V_5
    IL_008a:  ldloc.s    V_5
    IL_008c:  ldloc.s    V_4
    IL_008e:  add
    IL_008f:  ldloc.3
    IL_0090:  add
    IL_0091:  ldloc.2
    IL_0092:  add
    IL_0093:  ret
  } // end of method StructUnion01::f4

} // end of class StructUnion01

.class private abstract auto ansi sealed '<StartupCode$StructUnion01>'.$StructUnion01
       extends [mscorlib]System.Object
{
} // end of class '<StartupCode$StructUnion01>'.$StructUnion01


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
