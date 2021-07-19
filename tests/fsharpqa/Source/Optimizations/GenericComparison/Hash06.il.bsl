
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
.assembly Hash06
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 00 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.Hash06
{
  // Offset: 0x00000000 Length: 0x000006D1
}
.mresource public FSharpOptimizationData.Hash06
{
  // Offset: 0x000006D8 Length: 0x000003B2
}
.module Hash06.dll
// MVID: {60BE1F16-9642-78F2-A745-0383161FBE60}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x05280000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed Hash06
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class abstract auto ansi sealed nested public HashMicroPerfAndCodeGenerationTests
         extends [mscorlib]System.Object
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
    .class auto autochar serializable sealed nested public beforefieldinit Key
           extends [mscorlib]System.Object
           implements class [mscorlib]System.IEquatable`1<class Hash06/HashMicroPerfAndCodeGenerationTests/Key>,
                      [mscorlib]System.Collections.IStructuralEquatable,
                      class [mscorlib]System.IComparable`1<class Hash06/HashMicroPerfAndCodeGenerationTests/Key>,
                      [mscorlib]System.IComparable,
                      [mscorlib]System.Collections.IStructuralComparable
    {
      .custom instance void [mscorlib]System.Diagnostics.DebuggerDisplayAttribute::.ctor(string) = ( 01 00 15 7B 5F 5F 44 65 62 75 67 44 69 73 70 6C   // ...{__DebugDispl
                                                                                                     61 79 28 29 2C 6E 71 7D 00 00 )                   // ay(),nq}..
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 01 00 00 00 00 00 ) 
      .field assembly initonly int32 item1
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .field assembly initonly int32 item2
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .method public static class Hash06/HashMicroPerfAndCodeGenerationTests/Key 
              NewKey(int32 item1,
                     int32 item2) cil managed
      {
        .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                    int32) = ( 01 00 08 00 00 00 00 00 00 00 00 00 ) 
        // Code size       8 (0x8)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldarg.1
        IL_0002:  newobj     instance void Hash06/HashMicroPerfAndCodeGenerationTests/Key::.ctor(int32,
                                                                                                 int32)
        IL_0007:  ret
      } // end of method Key::NewKey

      .method assembly specialname rtspecialname 
              instance void  .ctor(int32 item1,
                                   int32 item2) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       21 (0x15)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  ldarg.1
        IL_0008:  stfld      int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::item1
        IL_000d:  ldarg.0
        IL_000e:  ldarg.2
        IL_000f:  stfld      int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::item2
        IL_0014:  ret
      } // end of method Key::.ctor

      .method public hidebysig instance int32 
              get_Item1() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       7 (0x7)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldfld      int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::item1
        IL_0006:  ret
      } // end of method Key::get_Item1

      .method public hidebysig instance int32 
              get_Item2() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       7 (0x7)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldfld      int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::item2
        IL_0006:  ret
      } // end of method Key::get_Item2

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
      } // end of method Key::get_Tag

      .method assembly hidebysig specialname 
              instance object  __DebugDisplay() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       22 (0x16)
        .maxstack  8
        IL_0000:  ldstr      "%+0.8A"
        IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class Hash06/HashMicroPerfAndCodeGenerationTests/Key,string>,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string,string>::.ctor(string)
        IL_000a:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatToString<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class Hash06/HashMicroPerfAndCodeGenerationTests/Key,string>>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string>)
        IL_000f:  ldarg.0
        IL_0010:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class Hash06/HashMicroPerfAndCodeGenerationTests/Key,string>::Invoke(!0)
        IL_0015:  ret
      } // end of method Key::__DebugDisplay

      .method public strict virtual instance string 
              ToString() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       22 (0x16)
        .maxstack  8
        IL_0000:  ldstr      "%+A"
        IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class Hash06/HashMicroPerfAndCodeGenerationTests/Key,string>,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string,class Hash06/HashMicroPerfAndCodeGenerationTests/Key>::.ctor(string)
        IL_000a:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatToString<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class Hash06/HashMicroPerfAndCodeGenerationTests/Key,string>>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string>)
        IL_000f:  ldarg.0
        IL_0010:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class Hash06/HashMicroPerfAndCodeGenerationTests/Key,string>::Invoke(!0)
        IL_0015:  ret
      } // end of method Key::ToString

      .method public hidebysig virtual final 
              instance int32  CompareTo(class Hash06/HashMicroPerfAndCodeGenerationTests/Key obj) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       119 (0x77)
        .maxstack  4
        .locals init ([0] class Hash06/HashMicroPerfAndCodeGenerationTests/Key V_0,
                 [1] class Hash06/HashMicroPerfAndCodeGenerationTests/Key V_1,
                 [2] int32 V_2,
                 [3] class [mscorlib]System.Collections.IComparer V_3,
                 [4] int32 V_4,
                 [5] int32 V_5)
        .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
        .line 16707566,16707566 : 0,0 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\Optimizations\\GenericComparison\\Hash06.fsx'
        IL_0000:  ldarg.0
        IL_0001:  ldnull
        IL_0002:  cgt.un
        IL_0004:  brfalse.s  IL_006d

        .line 16707566,16707566 : 0,0 ''
        IL_0006:  ldarg.1
        IL_0007:  ldnull
        IL_0008:  cgt.un
        IL_000a:  brfalse.s  IL_006b

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
        IL_0019:  ldfld      int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::item1
        IL_001e:  stloc.s    V_4
        IL_0020:  ldloc.1
        IL_0021:  ldfld      int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::item1
        IL_0026:  stloc.s    V_5
        IL_0028:  ldloc.s    V_4
        IL_002a:  ldloc.s    V_5
        IL_002c:  bge.s      IL_0032

        .line 16707566,16707566 : 0,0 ''
        IL_002e:  ldc.i4.m1
        .line 16707566,16707566 : 0,0 ''
        IL_002f:  nop
        IL_0030:  br.s       IL_0039

        .line 16707566,16707566 : 0,0 ''
        IL_0032:  ldloc.s    V_4
        IL_0034:  ldloc.s    V_5
        IL_0036:  cgt
        .line 16707566,16707566 : 0,0 ''
        IL_0038:  nop
        .line 16707566,16707566 : 0,0 ''
        IL_0039:  stloc.2
        IL_003a:  ldloc.2
        IL_003b:  ldc.i4.0
        IL_003c:  bge.s      IL_0040

        .line 16707566,16707566 : 0,0 ''
        IL_003e:  ldloc.2
        IL_003f:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_0040:  ldloc.2
        IL_0041:  ldc.i4.0
        IL_0042:  ble.s      IL_0046

        .line 16707566,16707566 : 0,0 ''
        IL_0044:  ldloc.2
        IL_0045:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_0046:  call       class [mscorlib]System.Collections.IComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericComparer()
        IL_004b:  stloc.3
        IL_004c:  ldloc.0
        IL_004d:  ldfld      int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::item2
        IL_0052:  stloc.s    V_4
        IL_0054:  ldloc.1
        IL_0055:  ldfld      int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::item2
        IL_005a:  stloc.s    V_5
        IL_005c:  ldloc.s    V_4
        IL_005e:  ldloc.s    V_5
        IL_0060:  bge.s      IL_0064

        .line 16707566,16707566 : 0,0 ''
        IL_0062:  ldc.i4.m1
        IL_0063:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_0064:  ldloc.s    V_4
        IL_0066:  ldloc.s    V_5
        IL_0068:  cgt
        IL_006a:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_006b:  ldc.i4.1
        IL_006c:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_006d:  ldarg.1
        IL_006e:  ldnull
        IL_006f:  cgt.un
        IL_0071:  brfalse.s  IL_0075

        .line 16707566,16707566 : 0,0 ''
        IL_0073:  ldc.i4.m1
        IL_0074:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_0075:  ldc.i4.0
        IL_0076:  ret
      } // end of method Key::CompareTo

      .method public hidebysig virtual final 
              instance int32  CompareTo(object obj) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       13 (0xd)
        .maxstack  8
        .line 4,4 : 10,13 ''
        IL_0000:  ldarg.0
        IL_0001:  ldarg.1
        IL_0002:  unbox.any  Hash06/HashMicroPerfAndCodeGenerationTests/Key
        IL_0007:  callvirt   instance int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::CompareTo(class Hash06/HashMicroPerfAndCodeGenerationTests/Key)
        IL_000c:  ret
      } // end of method Key::CompareTo

      .method public hidebysig virtual final 
              instance int32  CompareTo(object obj,
                                        class [mscorlib]System.Collections.IComparer comp) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       124 (0x7c)
        .maxstack  4
        .locals init ([0] class Hash06/HashMicroPerfAndCodeGenerationTests/Key V_0,
                 [1] class Hash06/HashMicroPerfAndCodeGenerationTests/Key V_1,
                 [2] class Hash06/HashMicroPerfAndCodeGenerationTests/Key V_2,
                 [3] int32 V_3,
                 [4] int32 V_4,
                 [5] int32 V_5)
        .line 4,4 : 10,13 ''
        IL_0000:  ldarg.1
        IL_0001:  unbox.any  Hash06/HashMicroPerfAndCodeGenerationTests/Key
        IL_0006:  stloc.0
        IL_0007:  ldarg.0
        IL_0008:  ldnull
        IL_0009:  cgt.un
        IL_000b:  brfalse.s  IL_006d

        .line 16707566,16707566 : 0,0 ''
        IL_000d:  ldarg.1
        IL_000e:  unbox.any  Hash06/HashMicroPerfAndCodeGenerationTests/Key
        IL_0013:  ldnull
        IL_0014:  cgt.un
        IL_0016:  brfalse.s  IL_006b

        .line 16707566,16707566 : 0,0 ''
        IL_0018:  ldarg.0
        IL_0019:  pop
        .line 16707566,16707566 : 0,0 ''
        IL_001a:  ldarg.0
        IL_001b:  stloc.1
        IL_001c:  ldloc.0
        IL_001d:  stloc.2
        IL_001e:  ldloc.1
        IL_001f:  ldfld      int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::item1
        IL_0024:  stloc.s    V_4
        IL_0026:  ldloc.2
        IL_0027:  ldfld      int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::item1
        IL_002c:  stloc.s    V_5
        IL_002e:  ldloc.s    V_4
        IL_0030:  ldloc.s    V_5
        IL_0032:  bge.s      IL_0038

        .line 16707566,16707566 : 0,0 ''
        IL_0034:  ldc.i4.m1
        .line 16707566,16707566 : 0,0 ''
        IL_0035:  nop
        IL_0036:  br.s       IL_003f

        .line 16707566,16707566 : 0,0 ''
        IL_0038:  ldloc.s    V_4
        IL_003a:  ldloc.s    V_5
        IL_003c:  cgt
        .line 16707566,16707566 : 0,0 ''
        IL_003e:  nop
        .line 16707566,16707566 : 0,0 ''
        IL_003f:  stloc.3
        IL_0040:  ldloc.3
        IL_0041:  ldc.i4.0
        IL_0042:  bge.s      IL_0046

        .line 16707566,16707566 : 0,0 ''
        IL_0044:  ldloc.3
        IL_0045:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_0046:  ldloc.3
        IL_0047:  ldc.i4.0
        IL_0048:  ble.s      IL_004c

        .line 16707566,16707566 : 0,0 ''
        IL_004a:  ldloc.3
        IL_004b:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_004c:  ldloc.1
        IL_004d:  ldfld      int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::item2
        IL_0052:  stloc.s    V_4
        IL_0054:  ldloc.2
        IL_0055:  ldfld      int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::item2
        IL_005a:  stloc.s    V_5
        IL_005c:  ldloc.s    V_4
        IL_005e:  ldloc.s    V_5
        IL_0060:  bge.s      IL_0064

        .line 16707566,16707566 : 0,0 ''
        IL_0062:  ldc.i4.m1
        IL_0063:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_0064:  ldloc.s    V_4
        IL_0066:  ldloc.s    V_5
        IL_0068:  cgt
        IL_006a:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_006b:  ldc.i4.1
        IL_006c:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_006d:  ldarg.1
        IL_006e:  unbox.any  Hash06/HashMicroPerfAndCodeGenerationTests/Key
        IL_0073:  ldnull
        IL_0074:  cgt.un
        IL_0076:  brfalse.s  IL_007a

        .line 16707566,16707566 : 0,0 ''
        IL_0078:  ldc.i4.m1
        IL_0079:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_007a:  ldc.i4.0
        IL_007b:  ret
      } // end of method Key::CompareTo

      .method public hidebysig virtual final 
              instance int32  GetHashCode(class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       60 (0x3c)
        .maxstack  7
        .locals init ([0] int32 V_0,
                 [1] class Hash06/HashMicroPerfAndCodeGenerationTests/Key V_1)
        .line 16707566,16707566 : 0,0 ''
        IL_0000:  ldarg.0
        IL_0001:  ldnull
        IL_0002:  cgt.un
        IL_0004:  brfalse.s  IL_003a

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
        IL_0014:  ldfld      int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::item2
        IL_0019:  ldloc.0
        IL_001a:  ldc.i4.6
        IL_001b:  shl
        IL_001c:  ldloc.0
        IL_001d:  ldc.i4.2
        IL_001e:  shr
        IL_001f:  add
        IL_0020:  add
        IL_0021:  add
        IL_0022:  stloc.0
        IL_0023:  ldc.i4     0x9e3779b9
        IL_0028:  ldloc.1
        IL_0029:  ldfld      int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::item1
        IL_002e:  ldloc.0
        IL_002f:  ldc.i4.6
        IL_0030:  shl
        IL_0031:  ldloc.0
        IL_0032:  ldc.i4.2
        IL_0033:  shr
        IL_0034:  add
        IL_0035:  add
        IL_0036:  add
        IL_0037:  stloc.0
        IL_0038:  ldloc.0
        IL_0039:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_003a:  ldc.i4.0
        IL_003b:  ret
      } // end of method Key::GetHashCode

      .method public hidebysig virtual final 
              instance int32  GetHashCode() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       12 (0xc)
        .maxstack  8
        .line 4,4 : 10,13 ''
        IL_0000:  ldarg.0
        IL_0001:  call       class [mscorlib]System.Collections.IEqualityComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericEqualityComparer()
        IL_0006:  callvirt   instance int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::GetHashCode(class [mscorlib]System.Collections.IEqualityComparer)
        IL_000b:  ret
      } // end of method Key::GetHashCode

      .method public hidebysig virtual final 
              instance bool  Equals(object obj,
                                    class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       63 (0x3f)
        .maxstack  4
        .locals init ([0] class Hash06/HashMicroPerfAndCodeGenerationTests/Key V_0,
                 [1] class Hash06/HashMicroPerfAndCodeGenerationTests/Key V_1,
                 [2] class Hash06/HashMicroPerfAndCodeGenerationTests/Key V_2)
        .line 16707566,16707566 : 0,0 ''
        IL_0000:  ldarg.0
        IL_0001:  ldnull
        IL_0002:  cgt.un
        IL_0004:  brfalse.s  IL_0037

        .line 16707566,16707566 : 0,0 ''
        IL_0006:  ldarg.1
        IL_0007:  isinst     Hash06/HashMicroPerfAndCodeGenerationTests/Key
        IL_000c:  stloc.0
        IL_000d:  ldloc.0
        IL_000e:  brfalse.s  IL_0035

        .line 16707566,16707566 : 0,0 ''
        IL_0010:  ldarg.0
        IL_0011:  pop
        .line 16707566,16707566 : 0,0 ''
        IL_0012:  ldarg.0
        IL_0013:  stloc.1
        IL_0014:  ldloc.0
        IL_0015:  stloc.2
        IL_0016:  ldloc.1
        IL_0017:  ldfld      int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::item1
        IL_001c:  ldloc.2
        IL_001d:  ldfld      int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::item1
        IL_0022:  bne.un.s   IL_0033

        .line 16707566,16707566 : 0,0 ''
        IL_0024:  ldloc.1
        IL_0025:  ldfld      int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::item2
        IL_002a:  ldloc.2
        IL_002b:  ldfld      int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::item2
        IL_0030:  ceq
        IL_0032:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_0033:  ldc.i4.0
        IL_0034:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_0035:  ldc.i4.0
        IL_0036:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_0037:  ldarg.1
        IL_0038:  ldnull
        IL_0039:  cgt.un
        IL_003b:  ldc.i4.0
        IL_003c:  ceq
        IL_003e:  ret
      } // end of method Key::Equals

      .method public hidebysig virtual final 
              instance bool  Equals(class Hash06/HashMicroPerfAndCodeGenerationTests/Key obj) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       59 (0x3b)
        .maxstack  4
        .locals init ([0] class Hash06/HashMicroPerfAndCodeGenerationTests/Key V_0,
                 [1] class Hash06/HashMicroPerfAndCodeGenerationTests/Key V_1)
        .line 16707566,16707566 : 0,0 ''
        IL_0000:  ldarg.0
        IL_0001:  ldnull
        IL_0002:  cgt.un
        IL_0004:  brfalse.s  IL_0033

        .line 16707566,16707566 : 0,0 ''
        IL_0006:  ldarg.1
        IL_0007:  ldnull
        IL_0008:  cgt.un
        IL_000a:  brfalse.s  IL_0031

        .line 16707566,16707566 : 0,0 ''
        IL_000c:  ldarg.0
        IL_000d:  pop
        .line 16707566,16707566 : 0,0 ''
        IL_000e:  ldarg.0
        IL_000f:  stloc.0
        IL_0010:  ldarg.1
        IL_0011:  stloc.1
        IL_0012:  ldloc.0
        IL_0013:  ldfld      int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::item1
        IL_0018:  ldloc.1
        IL_0019:  ldfld      int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::item1
        IL_001e:  bne.un.s   IL_002f

        .line 16707566,16707566 : 0,0 ''
        IL_0020:  ldloc.0
        IL_0021:  ldfld      int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::item2
        IL_0026:  ldloc.1
        IL_0027:  ldfld      int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::item2
        IL_002c:  ceq
        IL_002e:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_002f:  ldc.i4.0
        IL_0030:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_0031:  ldc.i4.0
        IL_0032:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_0033:  ldarg.1
        IL_0034:  ldnull
        IL_0035:  cgt.un
        IL_0037:  ldc.i4.0
        IL_0038:  ceq
        IL_003a:  ret
      } // end of method Key::Equals

      .method public hidebysig virtual final 
              instance bool  Equals(object obj) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       20 (0x14)
        .maxstack  4
        .locals init ([0] class Hash06/HashMicroPerfAndCodeGenerationTests/Key V_0)
        .line 4,4 : 10,13 ''
        IL_0000:  ldarg.1
        IL_0001:  isinst     Hash06/HashMicroPerfAndCodeGenerationTests/Key
        IL_0006:  stloc.0
        IL_0007:  ldloc.0
        IL_0008:  brfalse.s  IL_0012

        .line 16707566,16707566 : 0,0 ''
        IL_000a:  ldarg.0
        IL_000b:  ldloc.0
        IL_000c:  callvirt   instance bool Hash06/HashMicroPerfAndCodeGenerationTests/Key::Equals(class Hash06/HashMicroPerfAndCodeGenerationTests/Key)
        IL_0011:  ret

        .line 16707566,16707566 : 0,0 ''
        IL_0012:  ldc.i4.0
        IL_0013:  ret
      } // end of method Key::Equals

      .property instance int32 Tag()
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
        .get instance int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::get_Tag()
      } // end of property Key::Tag
      .property instance int32 Item1()
      {
        .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                    int32,
                                                                                                    int32) = ( 01 00 04 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        .get instance int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::get_Item1()
      } // end of property Key::Item1
      .property instance int32 Item2()
      {
        .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                    int32,
                                                                                                    int32) = ( 01 00 04 00 00 00 00 00 00 00 01 00 00 00 00 00 ) 
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        .get instance int32 Hash06/HashMicroPerfAndCodeGenerationTests/Key::get_Item2()
      } // end of property Key::Item2
    } // end of class Key

    .method public static void  f5a() cil managed
    {
      // Code size       30 (0x1e)
      .maxstack  4
      .locals init ([0] int32 i,
               [1] int32 V_1)
      .line 7,7 : 8,32 ''
      IL_0000:  ldc.i4.0
      IL_0001:  stloc.0
      IL_0002:  br.s       IL_0015

      .line 8,8 : 12,46 ''
      IL_0004:  ldc.i4.1
      IL_0005:  ldc.i4.2
      IL_0006:  call       class Hash06/HashMicroPerfAndCodeGenerationTests/Key Hash06/HashMicroPerfAndCodeGenerationTests/Key::NewKey(int32,
                                                                                                                                       int32)
      IL_000b:  callvirt   instance int32 [mscorlib]System.Object::GetHashCode()
      IL_0010:  stloc.1
      IL_0011:  ldloc.0
      IL_0012:  ldc.i4.1
      IL_0013:  add
      IL_0014:  stloc.0
      .line 7,7 : 8,32 ''
      IL_0015:  ldloc.0
      IL_0016:  ldc.i4     0x989681
      IL_001b:  blt.s      IL_0004

      IL_001d:  ret
    } // end of method HashMicroPerfAndCodeGenerationTests::f5a

  } // end of class HashMicroPerfAndCodeGenerationTests

} // end of class Hash06

.class private abstract auto ansi sealed '<StartupCode$Hash06>'.$Hash06$fsx
       extends [mscorlib]System.Object
{
} // end of class '<StartupCode$Hash06>'.$Hash06$fsx


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
