
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
.assembly extern netstandard
{
  .publickeytoken = (CC 7B 13 FF CD 2D DD 51 )                         // .{...-.Q
  .ver 2:0:0:0
}
.assembly StaticInit_Struct01
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.StaticInit_Struct01
{
  // Offset: 0x00000000 Length: 0x000007A1
}
.mresource public FSharpOptimizationData.StaticInit_Struct01
{
  // Offset: 0x000007A8 Length: 0x0000021F
}
.module StaticInit_Struct01.dll
// MVID: {60B68B90-05F6-D6CB-A745-0383908BB660}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x05BE0000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed StaticInit_Struct01
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class sequential ansi serializable sealed nested public C
         extends [mscorlib]System.ValueType
         implements class [mscorlib]System.IEquatable`1<valuetype StaticInit_Struct01/C>,
                    [mscorlib]System.Collections.IStructuralEquatable,
                    class [mscorlib]System.IComparable`1<valuetype StaticInit_Struct01/C>,
                    [mscorlib]System.IComparable,
                    [mscorlib]System.Collections.IStructuralComparable
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 03 00 00 00 00 00 ) 
    .field static assembly int32 x
    .field static assembly int32 init@4
    .field assembly valuetype [mscorlib]System.DateTime s
    .method public hidebysig virtual final 
            instance int32  CompareTo(valuetype StaticInit_Struct01/C obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       27 (0x1b)
      .maxstack  4
      .locals init ([0] valuetype StaticInit_Struct01/C& V_0,
               [1] class [mscorlib]System.Collections.IComparer V_1)
      .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
      .line 4,4 : 6,7 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\StaticInit\\StaticInit_Struct01.fs'
      IL_0000:  ldarga.s   obj
      IL_0002:  stloc.0
      IL_0003:  call       class [mscorlib]System.Collections.IComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericComparer()
      IL_0008:  stloc.1
      IL_0009:  ldarg.0
      IL_000a:  ldfld      valuetype [mscorlib]System.DateTime StaticInit_Struct01/C::s
      IL_000f:  ldloc.0
      IL_0010:  ldfld      valuetype [mscorlib]System.DateTime StaticInit_Struct01/C::s
      IL_0015:  call       int32 [netstandard]System.DateTime::Compare(valuetype [netstandard]System.DateTime,
                                                                       valuetype [netstandard]System.DateTime)
      IL_001a:  ret
    } // end of method C::CompareTo

    .method public hidebysig virtual final 
            instance int32  CompareTo(object obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       13 (0xd)
      .maxstack  8
      .line 4,4 : 6,7 ''
      IL_0000:  ldarg.0
      IL_0001:  ldarg.1
      IL_0002:  unbox.any  StaticInit_Struct01/C
      IL_0007:  call       instance int32 StaticInit_Struct01/C::CompareTo(valuetype StaticInit_Struct01/C)
      IL_000c:  ret
    } // end of method C::CompareTo

    .method public hidebysig virtual final 
            instance int32  CompareTo(object obj,
                                      class [mscorlib]System.Collections.IComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       30 (0x1e)
      .maxstack  4
      .locals init ([0] valuetype StaticInit_Struct01/C V_0,
               [1] valuetype StaticInit_Struct01/C& V_1,
               [2] class [mscorlib]System.Collections.IComparer V_2)
      .line 4,4 : 6,7 ''
      IL_0000:  ldarg.1
      IL_0001:  unbox.any  StaticInit_Struct01/C
      IL_0006:  stloc.0
      IL_0007:  ldloca.s   V_0
      IL_0009:  stloc.1
      IL_000a:  ldarg.2
      IL_000b:  stloc.2
      IL_000c:  ldarg.0
      IL_000d:  ldfld      valuetype [mscorlib]System.DateTime StaticInit_Struct01/C::s
      IL_0012:  ldloc.1
      IL_0013:  ldfld      valuetype [mscorlib]System.DateTime StaticInit_Struct01/C::s
      IL_0018:  call       int32 [netstandard]System.DateTime::Compare(valuetype [netstandard]System.DateTime,
                                                                       valuetype [netstandard]System.DateTime)
      IL_001d:  ret
    } // end of method C::CompareTo

    .method public hidebysig virtual final 
            instance int32  GetHashCode(class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       31 (0x1f)
      .maxstack  7
      .locals init ([0] int32 V_0)
      .line 4,4 : 6,7 ''
      IL_0000:  ldc.i4.0
      IL_0001:  stloc.0
      IL_0002:  ldc.i4     0x9e3779b9
      IL_0007:  ldarg.1
      IL_0008:  ldarg.0
      IL_0009:  ldfld      valuetype [mscorlib]System.DateTime StaticInit_Struct01/C::s
      IL_000e:  call       int32 [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/HashCompare::GenericHashWithComparerIntrinsic<valuetype [mscorlib]System.DateTime>(class [mscorlib]System.Collections.IEqualityComparer,
                                                                                                                                                                          !!0)
      IL_0013:  ldloc.0
      IL_0014:  ldc.i4.6
      IL_0015:  shl
      IL_0016:  ldloc.0
      IL_0017:  ldc.i4.2
      IL_0018:  shr
      IL_0019:  add
      IL_001a:  add
      IL_001b:  add
      IL_001c:  stloc.0
      IL_001d:  ldloc.0
      IL_001e:  ret
    } // end of method C::GetHashCode

    .method public hidebysig virtual final 
            instance int32  GetHashCode() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       12 (0xc)
      .maxstack  8
      .line 4,4 : 6,7 ''
      IL_0000:  ldarg.0
      IL_0001:  call       class [mscorlib]System.Collections.IEqualityComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericEqualityComparer()
      IL_0006:  call       instance int32 StaticInit_Struct01/C::GetHashCode(class [mscorlib]System.Collections.IEqualityComparer)
      IL_000b:  ret
    } // end of method C::GetHashCode

    .method public hidebysig virtual final 
            instance bool  Equals(object obj,
                                  class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       42 (0x2a)
      .maxstack  4
      .locals init ([0] valuetype StaticInit_Struct01/C V_0,
               [1] valuetype StaticInit_Struct01/C& V_1,
               [2] class [mscorlib]System.Collections.IEqualityComparer V_2)
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldarg.1
      IL_0001:  call       bool [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/IntrinsicFunctions::TypeTestGeneric<valuetype StaticInit_Struct01/C>(object)
      IL_0006:  brtrue.s   IL_000a

      IL_0008:  br.s       IL_0028

      .line 100001,100001 : 0,0 ''
      IL_000a:  ldarg.1
      IL_000b:  unbox.any  StaticInit_Struct01/C
      IL_0010:  stloc.0
      IL_0011:  ldloca.s   V_0
      IL_0013:  stloc.1
      IL_0014:  ldarg.2
      IL_0015:  stloc.2
      IL_0016:  ldarg.0
      IL_0017:  ldfld      valuetype [mscorlib]System.DateTime StaticInit_Struct01/C::s
      IL_001c:  ldloc.1
      IL_001d:  ldfld      valuetype [mscorlib]System.DateTime StaticInit_Struct01/C::s
      IL_0022:  call       bool [netstandard]System.DateTime::Equals(valuetype [netstandard]System.DateTime,
                                                                     valuetype [netstandard]System.DateTime)
      IL_0027:  ret

      .line 100001,100001 : 0,0 ''
      IL_0028:  ldc.i4.0
      IL_0029:  ret
    } // end of method C::Equals

    .method public specialname rtspecialname 
            instance void  .ctor(valuetype [mscorlib]System.DateTime s) cil managed
    {
      // Code size       8 (0x8)
      .maxstack  8
      .line 4,4 : 8,9 ''
      IL_0000:  ldarg.0
      IL_0001:  ldarg.1
      IL_0002:  stfld      valuetype [mscorlib]System.DateTime StaticInit_Struct01/C::s
      .line 4,4 : 6,7 ''
      IL_0007:  ret
    } // end of method C::.ctor

    .method assembly static int32  f() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       37 (0x25)
      .maxstack  8
      .line 7,7 : 23,37 ''
      IL_0000:  volatile.
      IL_0002:  ldsfld     int32 StaticInit_Struct01/C::init@4
      IL_0007:  ldc.i4.1
      IL_0008:  bge.s      IL_0013

      .line 100001,100001 : 0,0 ''
      IL_000a:  call       void [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/IntrinsicFunctions::FailStaticInit()
      IL_000f:  nop
      .line 100001,100001 : 0,0 ''
      IL_0010:  nop
      IL_0011:  br.s       IL_0014

      .line 100001,100001 : 0,0 ''
      IL_0013:  nop
      IL_0014:  ldsfld     int32 StaticInit_Struct01/C::x
      IL_0019:  ldstr      "2"
      IL_001e:  callvirt   instance int32 [mscorlib]System.String::get_Length()
      IL_0023:  add
      IL_0024:  ret
    } // end of method C::f

    .method public hidebysig virtual final 
            instance bool  Equals(valuetype StaticInit_Struct01/C obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       21 (0x15)
      .maxstack  4
      .locals init ([0] valuetype StaticInit_Struct01/C& V_0)
      .line 4,4 : 6,7 ''
      IL_0000:  ldarga.s   obj
      IL_0002:  stloc.0
      IL_0003:  ldarg.0
      IL_0004:  ldfld      valuetype [mscorlib]System.DateTime StaticInit_Struct01/C::s
      IL_0009:  ldloc.0
      IL_000a:  ldfld      valuetype [mscorlib]System.DateTime StaticInit_Struct01/C::s
      IL_000f:  call       bool [netstandard]System.DateTime::Equals(valuetype [netstandard]System.DateTime,
                                                                     valuetype [netstandard]System.DateTime)
      IL_0014:  ret
    } // end of method C::Equals

    .method public hidebysig virtual final 
            instance bool  Equals(object obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       27 (0x1b)
      .maxstack  4
      .locals init ([0] valuetype StaticInit_Struct01/C V_0)
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldarg.1
      IL_0001:  call       bool [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/IntrinsicFunctions::TypeTestGeneric<valuetype StaticInit_Struct01/C>(object)
      IL_0006:  brtrue.s   IL_000a

      IL_0008:  br.s       IL_0019

      .line 100001,100001 : 0,0 ''
      IL_000a:  ldarg.1
      IL_000b:  unbox.any  StaticInit_Struct01/C
      IL_0010:  stloc.0
      IL_0011:  ldarg.0
      IL_0012:  ldloc.0
      IL_0013:  call       instance bool StaticInit_Struct01/C::Equals(valuetype StaticInit_Struct01/C)
      IL_0018:  ret

      .line 100001,100001 : 0,0 ''
      IL_0019:  ldc.i4.0
      IL_001a:  ret
    } // end of method C::Equals

    .method private specialname rtspecialname static 
            void  .cctor() cil managed
    {
      // Code size       13 (0xd)
      .maxstack  8
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldc.i4.0
      IL_0001:  stsfld     int32 '<StartupCode$StaticInit_Struct01>'.$StaticInit_Struct01::init@
      IL_0006:  ldsfld     int32 '<StartupCode$StaticInit_Struct01>'.$StaticInit_Struct01::init@
      IL_000b:  pop
      IL_000c:  ret
    } // end of method C::.cctor

  } // end of class C

} // end of class StaticInit_Struct01

.class private abstract auto ansi sealed '<StartupCode$StaticInit_Struct01>'.$StaticInit_Struct01
       extends [mscorlib]System.Object
{
  .field static assembly int32 init@
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
  .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
  .method private specialname rtspecialname static 
          void  .cctor() cil managed
  {
    // Code size       24 (0x18)
    .maxstack  8
    .line 6,6 : 12,30 ''
    IL_0000:  ldstr      "1"
    IL_0005:  callvirt   instance int32 [mscorlib]System.String::get_Length()
    IL_000a:  stsfld     int32 StaticInit_Struct01/C::x
    IL_000f:  ldc.i4.1
    IL_0010:  volatile.
    IL_0012:  stsfld     int32 StaticInit_Struct01/C::init@4
    .line 4,4 : 6,7 ''
    IL_0017:  ret
  } // end of method $StaticInit_Struct01::.cctor

} // end of class '<StartupCode$StaticInit_Struct01>'.$StaticInit_Struct01


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
