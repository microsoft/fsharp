
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
.assembly Equals06
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 00 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.Equals06
{
  // Offset: 0x00000000 Length: 0x00000886
}
.mresource public FSharpOptimizationData.Equals06
{
  // Offset: 0x00000890 Length: 0x00000688
}
.module Equals06.dll
// MVID: {60BE1F16-0759-31EC-A745-0383161FBE60}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x06DA0000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed Equals06
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class abstract auto ansi sealed nested public EqualsMicroPerfAndCodeGenerationTests
         extends [mscorlib]System.Object
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
    .class auto autochar serializable sealed nested public beforefieldinit GenericKey`1<a>
           extends [mscorlib]System.Object
           implements class [mscorlib]System.IEquatable`1<class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>>,
                      [mscorlib]System.Collections.IStructuralEquatable,
                      class [mscorlib]System.IComparable`1<class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>>,
                      [mscorlib]System.IComparable,
                      [mscorlib]System.Collections.IStructuralComparable
    {
      .custom instance void [mscorlib]System.Diagnostics.DebuggerDisplayAttribute::.ctor(string) = ( 01 00 15 7B 5F 5F 44 65 62 75 67 44 69 73 70 6C   // ...{__DebugDispl
                                                                                                     61 79 28 29 2C 6E 71 7D 00 00 )                   // ay(),nq}..
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 01 00 00 00 00 00 ) 
      .field assembly initonly !a item1
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .field assembly initonly !a item2
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .method public static class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a> 
              NewGenericKey(!a item1,
                            !a item2) cil managed
      {
        .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                    int32) = ( 01 00 08 00 00 00 00 00 00 00 00 00 ) 
        // Code size       8 (0x8)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldarg.1
        IL_0002:  newobj     instance void class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::.ctor(!0,
                                                                                                                        !0)
        IL_0007:  ret
      } // end of method GenericKey`1::NewGenericKey

      .method assembly specialname rtspecialname 
              instance void  .ctor(!a item1,
                                   !a item2) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       21 (0x15)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  ldarg.1
        IL_0008:  stfld      !0 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::item1
        IL_000d:  ldarg.0
        IL_000e:  ldarg.2
        IL_000f:  stfld      !0 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::item2
        IL_0014:  ret
      } // end of method GenericKey`1::.ctor

      .method public hidebysig instance !a 
              get_Item1() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       7 (0x7)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldfld      !0 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::item1
        IL_0006:  ret
      } // end of method GenericKey`1::get_Item1

      .method public hidebysig instance !a 
              get_Item2() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       7 (0x7)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldfld      !0 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::item2
        IL_0006:  ret
      } // end of method GenericKey`1::get_Item2

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
      } // end of method GenericKey`1::get_Tag

      .method assembly hidebysig specialname 
              instance object  __DebugDisplay() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       22 (0x16)
        .maxstack  8
        IL_0000:  ldstr      "%+0.8A"
        IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>,string>,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string,string>::.ctor(string)
        IL_000a:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatToString<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>,string>>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string>)
        IL_000f:  ldarg.0
        IL_0010:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>,string>::Invoke(!0)
        IL_0015:  ret
      } // end of method GenericKey`1::__DebugDisplay

      .method public strict virtual instance string 
              ToString() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       22 (0x16)
        .maxstack  8
        IL_0000:  ldstr      "%+A"
        IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>,string>,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string,class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>>::.ctor(string)
        IL_000a:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatToString<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>,string>>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string>)
        IL_000f:  ldarg.0
        IL_0010:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>,string>::Invoke(!0)
        IL_0015:  ret
      } // end of method GenericKey`1::ToString

      .method public hidebysig virtual final 
              instance int32  CompareTo(class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a> obj) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       110 (0x6e)
        .maxstack  5
        .locals init ([0] class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a> V_0,
                 [1] class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a> V_1,
                 [2] int32 V_2,
                 [3] class [mscorlib]System.Collections.IComparer V_3,
                 [4] !a V_4,
                 [5] !a V_5)
        .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
        .line 16707566,16707566 : 0,0 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\Optimizations\\GenericComparison\\Equals06.fsx'
        IL_0000:  ldarg.0
        IL_0001:  ldnull
        IL_0002:  cgt.un
        IL_0004:  brfalse.s  IL_0064

        .line 16707566,16707566 : 0,0 ''
        IL_0006:  ldarg.1
        IL_0007:  ldnull
        IL_0008:  cgt.un
        IL_000a:  brfalse.s  IL_0062

        .line 16707566,16707566 : 0,0 ''
        IL_000c:  ldarg.0
        IL_000d:  pop
        .line 16707566,16707566 : 0,0 ''
        IL_000e:  ldarg.0
        IL_000f:  stloc.0
        IL_0010:  ldarg.1
        IL_0011:  stloc.1
        IL_0012:  call       class [mscorlib]System.Collections.IComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericComparer()
        IL_0017:  stloc.3
        IL_0018:  ldloc.0
        IL_0019:  ldfld      !0 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::item1
        IL_001e:  stloc.s    V_4
        IL_0020:  ldloc.1
        IL_0021:  ldfld      !0 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::item1
        IL_0026:  stloc.s    V_5
        IL_0028:  ldloc.3
        IL_0029:  ldloc.s    V_4
        IL_002b:  ldloc.s    V_5
        IL_002d:  call       int32 [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/HashCompare::GenericComparisonWithComparerIntrinsic<!a>(class [mscorlib]System.Collections.IComparer,
                                                                                                                                                 !!0,
                                                                                                                                                 !!0)
        IL_0032:  stloc.2
        IL_0033:  ldloc.2
        IL_0034:  ldc.i4.0
        IL_0035:  bge.s      IL_0039

        .line 16707566,16707566 : 0,0 ''
        IL_0037:  ldloc.2
        IL_0038:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_0039:  ldloc.2
        IL_003a:  ldc.i4.0
        IL_003b:  ble.s      IL_003f

        .line 16707566,16707566 : 0,0 ''
        IL_003d:  ldloc.2
        IL_003e:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_003f:  call       class [mscorlib]System.Collections.IComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericComparer()
        IL_0044:  stloc.3
        IL_0045:  ldloc.0
        IL_0046:  ldfld      !0 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::item2
        IL_004b:  stloc.s    V_4
        IL_004d:  ldloc.1
        IL_004e:  ldfld      !0 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::item2
        IL_0053:  stloc.s    V_5
        IL_0055:  ldloc.3
        IL_0056:  ldloc.s    V_4
        IL_0058:  ldloc.s    V_5
        IL_005a:  tail.
        IL_005c:  call       int32 [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/HashCompare::GenericComparisonWithComparerIntrinsic<!a>(class [mscorlib]System.Collections.IComparer,
                                                                                                                                                 !!0,
                                                                                                                                                 !!0)
        IL_0061:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_0062:  ldc.i4.1
        IL_0063:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_0064:  ldarg.1
        IL_0065:  ldnull
        IL_0066:  cgt.un
        IL_0068:  brfalse.s  IL_006c

        .line 16707566,16707566 : 0,0 ''
        IL_006a:  ldc.i4.m1
        IL_006b:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_006c:  ldc.i4.0
        IL_006d:  ret
      } // end of method GenericKey`1::CompareTo

      .method public hidebysig virtual final 
              instance int32  CompareTo(object obj) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       15 (0xf)
        .maxstack  8
        .line 4,4 : 10,20 ''
        IL_0000:  ldarg.0
        IL_0001:  ldarg.1
        IL_0002:  unbox.any  class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>
        IL_0007:  tail.
        IL_0009:  callvirt   instance int32 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::CompareTo(class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!0>)
        IL_000e:  ret
      } // end of method GenericKey`1::CompareTo

      .method public hidebysig virtual final 
              instance int32  CompareTo(object obj,
                                        class [mscorlib]System.Collections.IComparer comp) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       115 (0x73)
        .maxstack  5
        .locals init ([0] class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a> V_0,
                 [1] class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a> V_1,
                 [2] class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a> V_2,
                 [3] int32 V_3,
                 [4] !a V_4,
                 [5] !a V_5)
        .line 4,4 : 10,20 ''
        IL_0000:  ldarg.1
        IL_0001:  unbox.any  class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>
        IL_0006:  stloc.0
        IL_0007:  ldarg.0
        IL_0008:  ldnull
        IL_0009:  cgt.un
        IL_000b:  brfalse.s  IL_0064

        .line 16707566,16707566 : 0,0 ''
        IL_000d:  ldarg.1
        IL_000e:  unbox.any  class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>
        IL_0013:  ldnull
        IL_0014:  cgt.un
        IL_0016:  brfalse.s  IL_0062

        .line 16707566,16707566 : 0,0 ''
        IL_0018:  ldarg.0
        IL_0019:  pop
        .line 16707566,16707566 : 0,0 ''
        IL_001a:  ldarg.0
        IL_001b:  stloc.1
        IL_001c:  ldloc.0
        IL_001d:  stloc.2
        IL_001e:  ldloc.1
        IL_001f:  ldfld      !0 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::item1
        IL_0024:  stloc.s    V_4
        IL_0026:  ldloc.2
        IL_0027:  ldfld      !0 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::item1
        IL_002c:  stloc.s    V_5
        IL_002e:  ldarg.2
        IL_002f:  ldloc.s    V_4
        IL_0031:  ldloc.s    V_5
        IL_0033:  call       int32 [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/HashCompare::GenericComparisonWithComparerIntrinsic<!a>(class [mscorlib]System.Collections.IComparer,
                                                                                                                                                 !!0,
                                                                                                                                                 !!0)
        IL_0038:  stloc.3
        IL_0039:  ldloc.3
        IL_003a:  ldc.i4.0
        IL_003b:  bge.s      IL_003f

        .line 16707566,16707566 : 0,0 ''
        IL_003d:  ldloc.3
        IL_003e:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_003f:  ldloc.3
        IL_0040:  ldc.i4.0
        IL_0041:  ble.s      IL_0045

        .line 16707566,16707566 : 0,0 ''
        IL_0043:  ldloc.3
        IL_0044:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_0045:  ldloc.1
        IL_0046:  ldfld      !0 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::item2
        IL_004b:  stloc.s    V_4
        IL_004d:  ldloc.2
        IL_004e:  ldfld      !0 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::item2
        IL_0053:  stloc.s    V_5
        IL_0055:  ldarg.2
        IL_0056:  ldloc.s    V_4
        IL_0058:  ldloc.s    V_5
        IL_005a:  tail.
        IL_005c:  call       int32 [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/HashCompare::GenericComparisonWithComparerIntrinsic<!a>(class [mscorlib]System.Collections.IComparer,
                                                                                                                                                 !!0,
                                                                                                                                                 !!0)
        IL_0061:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_0062:  ldc.i4.1
        IL_0063:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_0064:  ldarg.1
        IL_0065:  unbox.any  class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>
        IL_006a:  ldnull
        IL_006b:  cgt.un
        IL_006d:  brfalse.s  IL_0071

        .line 16707566,16707566 : 0,0 ''
        IL_006f:  ldc.i4.m1
        IL_0070:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_0071:  ldc.i4.0
        IL_0072:  ret
      } // end of method GenericKey`1::CompareTo

      .method public hidebysig virtual final 
              instance int32  GetHashCode(class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       76 (0x4c)
        .maxstack  7
        .locals init ([0] int32 V_0,
                 [1] class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a> V_1,
                 [2] !a V_2)
        .line 16707566,16707566 : 0,0 ''
        IL_0000:  ldarg.0
        IL_0001:  ldnull
        IL_0002:  cgt.un
        IL_0004:  brfalse.s  IL_004a

        .line 16707566,16707566 : 0,0 ''
        IL_0006:  ldc.i4.0
        IL_0007:  stloc.0
        IL_0008:  ldarg.0
        IL_0009:  pop
        .line 16707566,16707566 : 0,0 ''
        IL_000a:  ldarg.0
        IL_000b:  stloc.1
        IL_000c:  ldc.i4.0
        IL_000d:  stloc.0
        IL_000e:  ldc.i4     0x9e3779b9
        IL_0013:  ldloc.1
        IL_0014:  ldfld      !0 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::item2
        IL_0019:  stloc.2
        IL_001a:  ldarg.1
        IL_001b:  ldloc.2
        IL_001c:  call       int32 [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/HashCompare::GenericHashWithComparerIntrinsic<!a>(class [mscorlib]System.Collections.IEqualityComparer,
                                                                                                                                           !!0)
        IL_0021:  ldloc.0
        IL_0022:  ldc.i4.6
        IL_0023:  shl
        IL_0024:  ldloc.0
        IL_0025:  ldc.i4.2
        IL_0026:  shr
        IL_0027:  add
        IL_0028:  add
        IL_0029:  add
        IL_002a:  stloc.0
        IL_002b:  ldc.i4     0x9e3779b9
        IL_0030:  ldloc.1
        IL_0031:  ldfld      !0 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::item1
        IL_0036:  stloc.2
        IL_0037:  ldarg.1
        IL_0038:  ldloc.2
        IL_0039:  call       int32 [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/HashCompare::GenericHashWithComparerIntrinsic<!a>(class [mscorlib]System.Collections.IEqualityComparer,
                                                                                                                                           !!0)
        IL_003e:  ldloc.0
        IL_003f:  ldc.i4.6
        IL_0040:  shl
        IL_0041:  ldloc.0
        IL_0042:  ldc.i4.2
        IL_0043:  shr
        IL_0044:  add
        IL_0045:  add
        IL_0046:  add
        IL_0047:  stloc.0
        IL_0048:  ldloc.0
        IL_0049:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_004a:  ldc.i4.0
        IL_004b:  ret
      } // end of method GenericKey`1::GetHashCode

      .method public hidebysig virtual final 
              instance int32  GetHashCode() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       12 (0xc)
        .maxstack  8
        .line 4,4 : 10,20 ''
        IL_0000:  ldarg.0
        IL_0001:  call       class [mscorlib]System.Collections.IEqualityComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericEqualityComparer()
        IL_0006:  callvirt   instance int32 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::GetHashCode(class [mscorlib]System.Collections.IEqualityComparer)
        IL_000b:  ret
      } // end of method GenericKey`1::GetHashCode

      .method public hidebysig virtual final 
              instance bool  Equals(object obj,
                                    class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       87 (0x57)
        .maxstack  5
        .locals init ([0] class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a> V_0,
                 [1] class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a> V_1,
                 [2] class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a> V_2,
                 [3] !a V_3,
                 [4] !a V_4)
        .line 16707566,16707566 : 0,0 ''
        IL_0000:  ldarg.0
        IL_0001:  ldnull
        IL_0002:  cgt.un
        IL_0004:  brfalse.s  IL_004f

        .line 16707566,16707566 : 0,0 ''
        IL_0006:  ldarg.1
        IL_0007:  isinst     class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>
        IL_000c:  stloc.0
        IL_000d:  ldloc.0
        IL_000e:  brfalse.s  IL_004d

        .line 16707566,16707566 : 0,0 ''
        IL_0010:  ldarg.0
        IL_0011:  pop
        .line 16707566,16707566 : 0,0 ''
        IL_0012:  ldarg.0
        IL_0013:  stloc.1
        IL_0014:  ldloc.0
        IL_0015:  stloc.2
        IL_0016:  ldloc.1
        IL_0017:  ldfld      !0 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::item1
        IL_001c:  stloc.3
        IL_001d:  ldloc.2
        IL_001e:  ldfld      !0 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::item1
        IL_0023:  stloc.s    V_4
        IL_0025:  ldarg.2
        IL_0026:  ldloc.3
        IL_0027:  ldloc.s    V_4
        IL_0029:  call       bool [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/HashCompare::GenericEqualityWithComparerIntrinsic<!a>(class [mscorlib]System.Collections.IEqualityComparer,
                                                                                                                                              !!0,
                                                                                                                                              !!0)
        IL_002e:  brfalse.s  IL_004b

        .line 16707566,16707566 : 0,0 ''
        IL_0030:  ldloc.1
        IL_0031:  ldfld      !0 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::item2
        IL_0036:  stloc.3
        IL_0037:  ldloc.2
        IL_0038:  ldfld      !0 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::item2
        IL_003d:  stloc.s    V_4
        IL_003f:  ldarg.2
        IL_0040:  ldloc.3
        IL_0041:  ldloc.s    V_4
        IL_0043:  tail.
        IL_0045:  call       bool [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/HashCompare::GenericEqualityWithComparerIntrinsic<!a>(class [mscorlib]System.Collections.IEqualityComparer,
                                                                                                                                              !!0,
                                                                                                                                              !!0)
        IL_004a:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_004b:  ldc.i4.0
        IL_004c:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_004d:  ldc.i4.0
        IL_004e:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_004f:  ldarg.1
        IL_0050:  ldnull
        IL_0051:  cgt.un
        IL_0053:  ldc.i4.0
        IL_0054:  ceq
        IL_0056:  ret
      } // end of method GenericKey`1::Equals

      .method public hidebysig virtual final 
              instance bool  Equals(class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a> obj) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       77 (0x4d)
        .maxstack  4
        .locals init ([0] class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a> V_0,
                 [1] class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a> V_1,
                 [2] !a V_2,
                 [3] !a V_3)
        .line 16707566,16707566 : 0,0 ''
        IL_0000:  ldarg.0
        IL_0001:  ldnull
        IL_0002:  cgt.un
        IL_0004:  brfalse.s  IL_0045

        .line 16707566,16707566 : 0,0 ''
        IL_0006:  ldarg.1
        IL_0007:  ldnull
        IL_0008:  cgt.un
        IL_000a:  brfalse.s  IL_0043

        .line 16707566,16707566 : 0,0 ''
        IL_000c:  ldarg.0
        IL_000d:  pop
        .line 16707566,16707566 : 0,0 ''
        IL_000e:  ldarg.0
        IL_000f:  stloc.0
        IL_0010:  ldarg.1
        IL_0011:  stloc.1
        IL_0012:  ldloc.0
        IL_0013:  ldfld      !0 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::item1
        IL_0018:  stloc.2
        IL_0019:  ldloc.1
        IL_001a:  ldfld      !0 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::item1
        IL_001f:  stloc.3
        IL_0020:  ldloc.2
        IL_0021:  ldloc.3
        IL_0022:  call       bool [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/HashCompare::GenericEqualityERIntrinsic<!a>(!!0,
                                                                                                                                    !!0)
        IL_0027:  brfalse.s  IL_0041

        .line 16707566,16707566 : 0,0 ''
        IL_0029:  ldloc.0
        IL_002a:  ldfld      !0 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::item2
        IL_002f:  stloc.2
        IL_0030:  ldloc.1
        IL_0031:  ldfld      !0 class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::item2
        IL_0036:  stloc.3
        IL_0037:  ldloc.2
        IL_0038:  ldloc.3
        IL_0039:  tail.
        IL_003b:  call       bool [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/HashCompare::GenericEqualityERIntrinsic<!a>(!!0,
                                                                                                                                    !!0)
        IL_0040:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_0041:  ldc.i4.0
        IL_0042:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_0043:  ldc.i4.0
        IL_0044:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_0045:  ldarg.1
        IL_0046:  ldnull
        IL_0047:  cgt.un
        IL_0049:  ldc.i4.0
        IL_004a:  ceq
        IL_004c:  ret
      } // end of method GenericKey`1::Equals

      .method public hidebysig virtual final 
              instance bool  Equals(object obj) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       22 (0x16)
        .maxstack  4
        .locals init ([0] class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a> V_0)
        .line 4,4 : 10,20 ''
        IL_0000:  ldarg.1
        IL_0001:  isinst     class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>
        IL_0006:  stloc.0
        IL_0007:  ldloc.0
        IL_0008:  brfalse.s  IL_0014

        .line 16707566,16707566 : 0,0 ''
        IL_000a:  ldarg.0
        IL_000b:  ldloc.0
        IL_000c:  tail.
        IL_000e:  callvirt   instance bool class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!a>::Equals(class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!0>)
        IL_0013:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_0014:  ldc.i4.0
        IL_0015:  ret
      } // end of method GenericKey`1::Equals

      .property instance int32 Tag()
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
        .get instance int32 Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1::get_Tag()
      } // end of property GenericKey`1::Tag
      .property instance !a Item1()
      {
        .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                    int32,
                                                                                                    int32) = ( 01 00 04 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        .get instance !a Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1::get_Item1()
      } // end of property GenericKey`1::Item1
      .property instance !a Item2()
      {
        .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                    int32,
                                                                                                    int32) = ( 01 00 04 00 00 00 00 00 00 00 01 00 00 00 00 00 ) 
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        .get instance !a Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1::get_Item2()
      } // end of property GenericKey`1::Item2
    } // end of class GenericKey`1

    .method public static bool  f6() cil managed
    {
      // Code size       49 (0x31)
      .maxstack  5
      .locals init ([0] bool x,
               [1] class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<int32> t1,
               [2] class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<int32> t2,
               [3] int32 i)
      .line 6,6 : 8,29 ''
      IL_0000:  ldc.i4.0
      IL_0001:  stloc.0
      .line 7,7 : 8,32 ''
      IL_0002:  ldc.i4.1
      IL_0003:  ldc.i4.2
      IL_0004:  call       class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!0> class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<int32>::NewGenericKey(!0,
                                                                                                                                                                                         !0)
      IL_0009:  stloc.1
      .line 8,8 : 8,32 ''
      IL_000a:  ldc.i4.1
      IL_000b:  ldc.i4.3
      IL_000c:  call       class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<!0> class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<int32>::NewGenericKey(!0,
                                                                                                                                                                                         !0)
      IL_0011:  stloc.2
      .line 9,9 : 8,32 ''
      IL_0012:  ldc.i4.0
      IL_0013:  stloc.3
      IL_0014:  br.s       IL_0027

      .line 10,10 : 12,26 ''
      IL_0016:  ldloc.1
      IL_0017:  ldloc.2
      IL_0018:  call       class [mscorlib]System.Collections.IEqualityComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericEqualityComparer()
      IL_001d:  callvirt   instance bool class Equals06/EqualsMicroPerfAndCodeGenerationTests/GenericKey`1<int32>::Equals(object,
                                                                                                                          class [mscorlib]System.Collections.IEqualityComparer)
      IL_0022:  stloc.0
      IL_0023:  ldloc.3
      IL_0024:  ldc.i4.1
      IL_0025:  add
      IL_0026:  stloc.3
      .line 9,9 : 8,32 ''
      IL_0027:  ldloc.3
      IL_0028:  ldc.i4     0x989681
      IL_002d:  blt.s      IL_0016

      .line 11,11 : 8,9 ''
      IL_002f:  ldloc.0
      IL_0030:  ret
    } // end of method EqualsMicroPerfAndCodeGenerationTests::f6

  } // end of class EqualsMicroPerfAndCodeGenerationTests

} // end of class Equals06

.class private abstract auto ansi sealed '<StartupCode$Equals06>'.$Equals06$fsx
       extends [mscorlib]System.Object
{
} // end of class '<StartupCode$Equals06>'.$Equals06$fsx


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
