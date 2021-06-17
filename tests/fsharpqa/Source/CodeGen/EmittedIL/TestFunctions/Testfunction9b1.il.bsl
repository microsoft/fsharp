
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
.assembly TestFunction9b1
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.TestFunction9b1
{
  // Offset: 0x00000000 Length: 0x00000204
}
.mresource public FSharpOptimizationData.TestFunction9b1
{
  // Offset: 0x00000208 Length: 0x00000083
}
.module TestFunction9b1.exe
// MVID: {60B68B97-A406-DAF4-A745-0383978BB660}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x072C0000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed TestFunction9b1
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .method public static string  TestFunction9b(class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32> x) cil managed
  {
    // Code size       409 (0x199)
    .maxstack  4
    .locals init ([0] class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32> V_0,
             [1] class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32> V_1,
             [2] class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32> V_2,
             [3] int32 b,
             [4] int32 a,
             [5] int32 V_5,
             [6] int32 V_6,
             [7] class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32> V_7,
             [8] int32 V_8,
             [9] int32 V_9,
             [10] class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32> V_10,
             [11] int32 V_11,
             [12] int32 V_12)
    .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
    .line 5,5 : 5,17 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\TestFunctions\\TestFunction9b1.fs'
    IL_0000:  ldarg.0
    IL_0001:  stloc.0
    IL_0002:  ldloc.0
    IL_0003:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_TailOrNull()
    IL_0008:  brfalse    IL_0193

    IL_000d:  ldloc.0
    IL_000e:  stloc.1
    IL_000f:  ldloc.1
    IL_0010:  call       instance !0 class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_HeadOrDefault()
    IL_0015:  ldc.i4.1
    IL_0016:  sub
    IL_0017:  switch     ( 
                          IL_00fd)
    IL_0020:  ldloc.1
    IL_0021:  call       instance !0 class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_HeadOrDefault()
    IL_0026:  ldc.i4.3
    IL_0027:  sub
    IL_0028:  switch     ( 
                          IL_0078)
    IL_0031:  ldloc.1
    IL_0032:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_TailOrNull()
    IL_0037:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_TailOrNull()
    IL_003c:  brfalse    IL_0193

    IL_0041:  ldloc.1
    IL_0042:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_TailOrNull()
    IL_0047:  stloc.2
    IL_0048:  ldloc.2
    IL_0049:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_TailOrNull()
    IL_004e:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_TailOrNull()
    IL_0053:  brtrue     IL_0193

    IL_0058:  ldloc.2
    IL_0059:  call       instance !0 class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_HeadOrDefault()
    IL_005e:  stloc.3
    IL_005f:  ldloc.1
    IL_0060:  call       instance !0 class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_HeadOrDefault()
    IL_0065:  stloc.s    a
    IL_0067:  ldloc.s    a
    IL_0069:  ldloc.3
    IL_006a:  add
    IL_006b:  ldc.i4.4
    IL_006c:  ceq
    IL_006e:  brfalse    IL_0193

    IL_0073:  br         IL_017d

    IL_0078:  ldloc.1
    IL_0079:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_TailOrNull()
    IL_007e:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_TailOrNull()
    IL_0083:  brfalse    IL_0193

    IL_0088:  ldloc.1
    IL_0089:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_TailOrNull()
    IL_008e:  stloc.s    V_7
    IL_0090:  ldloc.s    V_7
    IL_0092:  call       instance !0 class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_HeadOrDefault()
    IL_0097:  ldc.i4.4
    IL_0098:  sub
    IL_0099:  switch     ( 
                          IL_00e7)
    IL_00a2:  ldloc.s    V_7
    IL_00a4:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_TailOrNull()
    IL_00a9:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_TailOrNull()
    IL_00ae:  brtrue     IL_0193

    IL_00b3:  ldloc.s    V_7
    IL_00b5:  call       instance !0 class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_HeadOrDefault()
    IL_00ba:  stloc.s    V_8
    IL_00bc:  ldloc.1
    IL_00bd:  call       instance !0 class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_HeadOrDefault()
    IL_00c2:  stloc.s    V_9
    IL_00c4:  ldloc.s    V_9
    IL_00c6:  ldloc.s    V_8
    IL_00c8:  add
    IL_00c9:  ldc.i4.4
    IL_00ca:  ceq
    IL_00cc:  brfalse    IL_0193

    IL_00d1:  ldloc.s    V_7
    IL_00d3:  call       instance !0 class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_HeadOrDefault()
    IL_00d8:  ldloc.1
    IL_00d9:  call       instance !0 class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_HeadOrDefault()
    IL_00de:  stloc.s    V_6
    IL_00e0:  stloc.s    V_5
    IL_00e2:  br         IL_018d

    IL_00e7:  ldloc.s    V_7
    IL_00e9:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_TailOrNull()
    IL_00ee:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_TailOrNull()
    IL_00f3:  brtrue     IL_0193

    IL_00f8:  br         IL_0177

    IL_00fd:  ldloc.1
    IL_00fe:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_TailOrNull()
    IL_0103:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_TailOrNull()
    IL_0108:  brfalse    IL_0193

    IL_010d:  ldloc.1
    IL_010e:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_TailOrNull()
    IL_0113:  stloc.s    V_10
    IL_0115:  ldloc.s    V_10
    IL_0117:  call       instance !0 class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_HeadOrDefault()
    IL_011c:  ldc.i4.2
    IL_011d:  sub
    IL_011e:  switch     ( 
                          IL_0163)
    IL_0127:  ldloc.s    V_10
    IL_0129:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_TailOrNull()
    IL_012e:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_TailOrNull()
    IL_0133:  brtrue.s   IL_0193

    IL_0135:  ldloc.s    V_10
    IL_0137:  call       instance !0 class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_HeadOrDefault()
    IL_013c:  stloc.s    V_11
    IL_013e:  ldloc.1
    IL_013f:  call       instance !0 class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_HeadOrDefault()
    IL_0144:  stloc.s    V_12
    IL_0146:  ldloc.s    V_12
    IL_0148:  ldloc.s    V_11
    IL_014a:  add
    IL_014b:  ldc.i4.4
    IL_014c:  ceq
    IL_014e:  brfalse.s  IL_0193

    IL_0150:  ldloc.s    V_10
    IL_0152:  call       instance !0 class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_HeadOrDefault()
    IL_0157:  ldloc.1
    IL_0158:  call       instance !0 class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_HeadOrDefault()
    IL_015d:  stloc.s    V_6
    IL_015f:  stloc.s    V_5
    IL_0161:  br.s       IL_018d

    IL_0163:  ldloc.s    V_10
    IL_0165:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_TailOrNull()
    IL_016a:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_TailOrNull()
    IL_016f:  brtrue.s   IL_0193

    .line 6,6 : 16,23 ''
    IL_0171:  ldstr      "three"
    IL_0176:  ret

    .line 7,7 : 16,23 ''
    IL_0177:  ldstr      "seven"
    IL_017c:  ret

    .line 5,5 : 5,17 ''
    IL_017d:  ldloc.2
    IL_017e:  call       instance !0 class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_HeadOrDefault()
    IL_0183:  stloc.s    V_5
    IL_0185:  ldloc.1
    IL_0186:  call       instance !0 class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_HeadOrDefault()
    IL_018b:  stloc.s    V_6
    .line 8,8 : 29,35 ''
    IL_018d:  ldstr      "four"
    IL_0192:  ret

    .line 9,9 : 12,17 ''
    IL_0193:  ldstr      "big"
    IL_0198:  ret
  } // end of method TestFunction9b1::TestFunction9b

} // end of class TestFunction9b1

.class private abstract auto ansi sealed '<StartupCode$TestFunction9b1>'.$TestFunction9b1
       extends [mscorlib]System.Object
{
  .method public static void  main@() cil managed
  {
    .entrypoint
    // Code size       1 (0x1)
    .maxstack  8
    IL_0000:  ret
  } // end of method $TestFunction9b1::main@

} // end of class '<StartupCode$TestFunction9b1>'.$TestFunction9b1


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
