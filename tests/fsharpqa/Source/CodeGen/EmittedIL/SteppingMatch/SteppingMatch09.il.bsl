
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
.assembly SteppingMatch09
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.SteppingMatch09
{
  // Offset: 0x00000000 Length: 0x0000030C
}
.mresource public FSharpOptimizationData.SteppingMatch09
{
  // Offset: 0x00000310 Length: 0x000000EB
}
.module SteppingMatch09.dll
// MVID: {60B68B90-4935-D6AC-A745-0383908BB660}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x06A70000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed SteppingMatch09
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class auto ansi serializable sealed nested assembly beforefieldinit GenericInner@15
         extends [FSharp.Core]Microsoft.FSharp.Core.FSharpTypeFunc
  {
    .field static assembly initonly class SteppingMatch09/GenericInner@15 @_instance
    .method assembly specialname rtspecialname 
            instance void  .ctor() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       7 (0x7)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  call       instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpTypeFunc::.ctor()
      IL_0006:  ret
    } // end of method GenericInner@15::.ctor

    .method public strict virtual instance object 
            Specialize<T>() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       12 (0xc)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  newobj     instance void class SteppingMatch09/GenericInner@15T<!!T>::.ctor(class SteppingMatch09/GenericInner@15)
      IL_0006:  box        class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!!T>,int32>
      IL_000b:  ret
    } // end of method GenericInner@15::Specialize

    .method private specialname rtspecialname static 
            void  .cctor() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       11 (0xb)
      .maxstack  10
      IL_0000:  newobj     instance void SteppingMatch09/GenericInner@15::.ctor()
      IL_0005:  stsfld     class SteppingMatch09/GenericInner@15 SteppingMatch09/GenericInner@15::@_instance
      IL_000a:  ret
    } // end of method GenericInner@15::.cctor

  } // end of class GenericInner@15

  .class auto ansi serializable sealed nested assembly beforefieldinit GenericInner@15T<T>
         extends class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!T>,int32>
  {
    .field public class SteppingMatch09/GenericInner@15 self0@
    .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
    .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
    .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
    .method assembly specialname rtspecialname 
            instance void  .ctor(class SteppingMatch09/GenericInner@15 self0@) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       14 (0xe)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  call       instance void class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!T>,int32>::.ctor()
      IL_0006:  ldarg.0
      IL_0007:  ldarg.1
      IL_0008:  stfld      class SteppingMatch09/GenericInner@15 class SteppingMatch09/GenericInner@15T<!T>::self0@
      IL_000d:  ret
    } // end of method GenericInner@15T::.ctor

    .method public strict virtual instance int32 
            Invoke(class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!T> list) cil managed
    {
      // Code size       19 (0x13)
      .maxstack  6
      .locals init ([0] class SteppingMatch09/GenericInner@15 V_0)
      .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
      .line 16,16 : 6,21 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\SteppingMatch\\SteppingMatch09.fs'
      IL_0000:  ldarg.0
      IL_0001:  ldfld      class SteppingMatch09/GenericInner@15 class SteppingMatch09/GenericInner@15T<!T>::self0@
      IL_0006:  stloc.0
      IL_0007:  ldarg.1
      IL_0008:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!T>::get_TailOrNull()
      IL_000d:  brtrue.s   IL_0011

      .line 17,17 : 14,15 ''
      IL_000f:  ldc.i4.1
      IL_0010:  ret

      .line 18,18 : 13,14 ''
      IL_0011:  ldc.i4.2
      IL_0012:  ret
    } // end of method GenericInner@15T::Invoke

  } // end of class GenericInner@15T

  .class auto ansi serializable sealed nested assembly beforefieldinit NonGenericInner@25
         extends class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>,int32>
  {
    .field static assembly initonly class SteppingMatch09/NonGenericInner@25 @_instance
    .method assembly specialname rtspecialname 
            instance void  .ctor() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       7 (0x7)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  call       instance void class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>,int32>::.ctor()
      IL_0006:  ret
    } // end of method NonGenericInner@25::.ctor

    .method public strict virtual instance int32 
            Invoke(class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32> list) cil managed
    {
      // Code size       12 (0xc)
      .maxstack  8
      .line 25,25 : 6,21 ''
      IL_0000:  ldarg.1
      IL_0001:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_TailOrNull()
      IL_0006:  brtrue.s   IL_000a

      .line 26,26 : 14,15 ''
      IL_0008:  ldc.i4.1
      IL_0009:  ret

      .line 27,27 : 13,14 ''
      IL_000a:  ldc.i4.2
      IL_000b:  ret
    } // end of method NonGenericInner@25::Invoke

    .method private specialname rtspecialname static 
            void  .cctor() cil managed
    {
      // Code size       11 (0xb)
      .maxstack  10
      IL_0000:  newobj     instance void SteppingMatch09/NonGenericInner@25::.ctor()
      IL_0005:  stsfld     class SteppingMatch09/NonGenericInner@25 SteppingMatch09/NonGenericInner@25::@_instance
      IL_000a:  ret
    } // end of method NonGenericInner@25::.cctor

  } // end of class NonGenericInner@25

  .class auto ansi serializable sealed nested assembly beforefieldinit NonGenericInnerWithCapture@34
         extends class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>,int32>
  {
    .field public int32 x
    .method assembly specialname rtspecialname 
            instance void  .ctor(int32 x) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       14 (0xe)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  call       instance void class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>,int32>::.ctor()
      IL_0006:  ldarg.0
      IL_0007:  ldarg.1
      IL_0008:  stfld      int32 SteppingMatch09/NonGenericInnerWithCapture@34::x
      IL_000d:  ret
    } // end of method NonGenericInnerWithCapture@34::.ctor

    .method public strict virtual instance int32 
            Invoke(class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32> list) cil managed
    {
      // Code size       17 (0x11)
      .maxstack  8
      .line 34,34 : 6,21 ''
      IL_0000:  ldarg.1
      IL_0001:  call       instance class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>::get_TailOrNull()
      IL_0006:  brtrue.s   IL_000a

      .line 35,35 : 14,15 ''
      IL_0008:  ldc.i4.1
      IL_0009:  ret

      .line 36,36 : 13,14 ''
      IL_000a:  ldarg.0
      IL_000b:  ldfld      int32 SteppingMatch09/NonGenericInnerWithCapture@34::x
      IL_0010:  ret
    } // end of method NonGenericInnerWithCapture@34::Invoke

  } // end of class NonGenericInnerWithCapture@34

  .method public static class [FSharp.Core]Microsoft.FSharp.Core.FSharpOption`1<int32> 
          funcA(int32 n) cil managed
  {
    // Code size       36 (0x24)
    .maxstack  8
    .line 5,5 : 9,21 ''
    IL_0000:  ldarg.0
    IL_0001:  ldc.i4.1
    IL_0002:  sub
    IL_0003:  switch     ( 
                          IL_0012,
                          IL_001a)
    IL_0010:  br.s       IL_001c

    .line 7,7 : 13,21 ''
    IL_0012:  ldc.i4.s   10
    IL_0014:  call       class [FSharp.Core]Microsoft.FSharp.Core.FSharpOption`1<!0> class [FSharp.Core]Microsoft.FSharp.Core.FSharpOption`1<int32>::Some(!0)
    IL_0019:  ret

    .line 9,9 : 13,17 ''
    IL_001a:  ldnull
    IL_001b:  ret

    .line 11,11 : 20,34 ''
    IL_001c:  ldc.i4.s   22
    IL_001e:  call       class [FSharp.Core]Microsoft.FSharp.Core.FSharpOption`1<!0> class [FSharp.Core]Microsoft.FSharp.Core.FSharpOption`1<int32>::Some(!0)
    IL_0023:  ret
  } // end of method SteppingMatch09::funcA

  .method public static int32  OuterWithGenericInner<a>(class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!!a> list) cil managed
  {
    // Code size       28 (0x1c)
    .maxstack  4
    .locals init ([0] class [FSharp.Core]Microsoft.FSharp.Core.FSharpTypeFunc GenericInner,
             [1] class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!!a> V_1)
    .line 100001,100001 : 0,0 ''
    IL_0000:  ldsfld     class SteppingMatch09/GenericInner@15 SteppingMatch09/GenericInner@15::@_instance
    IL_0005:  stloc.0
    .line 20,20 : 3,20 ''
    IL_0006:  ldloc.0
    IL_0007:  ldarg.0
    IL_0008:  stloc.1
    IL_0009:  callvirt   instance object [FSharp.Core]Microsoft.FSharp.Core.FSharpTypeFunc::Specialize<!!0>()
    IL_000e:  unbox.any  class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!!a>,int32>
    IL_0013:  ldloc.1
    IL_0014:  tail.
    IL_0016:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!!a>,int32>::Invoke(!0)
    IL_001b:  ret
  } // end of method SteppingMatch09::OuterWithGenericInner

  .method public static int32  OuterWithNonGenericInner(class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32> list) cil managed
  {
    // Code size       16 (0x10)
    .maxstack  4
    .locals init ([0] class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>,int32> NonGenericInner)
    .line 100001,100001 : 0,0 ''
    IL_0000:  ldsfld     class SteppingMatch09/NonGenericInner@25 SteppingMatch09/NonGenericInner@25::@_instance
    IL_0005:  stloc.0
    .line 29,29 : 3,23 ''
    IL_0006:  ldloc.0
    IL_0007:  ldarg.0
    IL_0008:  tail.
    IL_000a:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>,int32>::Invoke(!0)
    IL_000f:  ret
  } // end of method SteppingMatch09::OuterWithNonGenericInner

  .method public static int32  OuterWithNonGenericInnerWithCapture(int32 x,
                                                                   class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32> list) cil managed
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationArgumentCountsAttribute::.ctor(int32[]) = ( 01 00 02 00 00 00 01 00 00 00 01 00 00 00 00 00 ) 
    // Code size       17 (0x11)
    .maxstack  4
    .locals init ([0] class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>,int32> NonGenericInnerWithCapture)
    .line 100001,100001 : 0,0 ''
    IL_0000:  ldarg.0
    IL_0001:  newobj     instance void SteppingMatch09/NonGenericInnerWithCapture@34::.ctor(int32)
    IL_0006:  stloc.0
    .line 38,38 : 3,34 ''
    IL_0007:  ldloc.0
    IL_0008:  ldarg.1
    IL_0009:  tail.
    IL_000b:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>,int32>::Invoke(!0)
    IL_0010:  ret
  } // end of method SteppingMatch09::OuterWithNonGenericInnerWithCapture

} // end of class SteppingMatch09

.class private abstract auto ansi sealed '<StartupCode$SteppingMatch09>'.$SteppingMatch09
       extends [mscorlib]System.Object
{
} // end of class '<StartupCode$SteppingMatch09>'.$SteppingMatch09


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
