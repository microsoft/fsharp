
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
.assembly Match01
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 
  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.Match01
{
  // Offset: 0x00000000 Length: 0x000006E6
}
.mresource public FSharpOptimizationData.Match01
{
  // Offset: 0x000006F0 Length: 0x000003B7
}
.module Match01.dll
// MVID: {60BE1F16-FAFE-C8E4-A745-0383161FBE60}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x09680000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed Match01
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class abstract auto autochar serializable nested public beforefieldinit Test1
         extends [mscorlib]System.Object
         implements class [mscorlib]System.IEquatable`1<class Match01/Test1>,
                    [mscorlib]System.Collections.IStructuralEquatable,
                    class [mscorlib]System.IComparable`1<class Match01/Test1>,
                    [mscorlib]System.IComparable,
                    [mscorlib]System.Collections.IStructuralComparable
  {
    .custom instance void [mscorlib]System.Diagnostics.DebuggerDisplayAttribute::.ctor(string) = ( 01 00 15 7B 5F 5F 44 65 62 75 67 44 69 73 70 6C   // ...{__DebugDispl
                                                                                                   61 79 28 29 2C 6E 71 7D 00 00 )                   // ay(),nq}..
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 01 00 00 00 00 00 ) 
    .class abstract auto ansi sealed nested public Tags
           extends [mscorlib]System.Object
    {
      .field public static literal int32 X11 = int32(0x00000000)
      .field public static literal int32 X12 = int32(0x00000001)
      .field public static literal int32 X13 = int32(0x00000002)
      .field public static literal int32 X14 = int32(0x00000003)
    } // end of class Tags

    .class auto ansi serializable nested public beforefieldinit specialname X11
           extends Match01/Test1
    {
      .custom instance void [mscorlib]System.Diagnostics.DebuggerTypeProxyAttribute::.ctor(class [mscorlib]System.Type) = ( 01 00 20 4D 61 74 63 68 30 31 2B 54 65 73 74 31   // .. Match01+Test1
                                                                                                                            2B 58 31 31 40 44 65 62 75 67 54 79 70 65 50 72   // +X11@DebugTypePr
                                                                                                                            6F 78 79 00 00 )                                  // oxy..
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
        // Code size       15 (0xf)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldc.i4.0
        IL_0002:  call       instance void Match01/Test1::.ctor(int32)
        IL_0007:  ldarg.0
        IL_0008:  ldarg.1
        IL_0009:  stfld      int32 Match01/Test1/X11::item
        IL_000e:  ret
      } // end of method X11::.ctor

      .method public hidebysig instance int32 
              get_Item() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       7 (0x7)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldfld      int32 Match01/Test1/X11::item
        IL_0006:  ret
      } // end of method X11::get_Item

      .property instance int32 Item()
      {
        .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                    int32,
                                                                                                    int32) = ( 01 00 04 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        .get instance int32 Match01/Test1/X11::get_Item()
      } // end of property X11::Item
    } // end of class X11

    .class auto ansi serializable nested public beforefieldinit specialname X12
           extends Match01/Test1
    {
      .custom instance void [mscorlib]System.Diagnostics.DebuggerTypeProxyAttribute::.ctor(class [mscorlib]System.Type) = ( 01 00 20 4D 61 74 63 68 30 31 2B 54 65 73 74 31   // .. Match01+Test1
                                                                                                                            2B 58 31 32 40 44 65 62 75 67 54 79 70 65 50 72   // +X12@DebugTypePr
                                                                                                                            6F 78 79 00 00 )                                  // oxy..
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
        // Code size       15 (0xf)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldc.i4.1
        IL_0002:  call       instance void Match01/Test1::.ctor(int32)
        IL_0007:  ldarg.0
        IL_0008:  ldarg.1
        IL_0009:  stfld      int32 Match01/Test1/X12::item
        IL_000e:  ret
      } // end of method X12::.ctor

      .method public hidebysig instance int32 
              get_Item() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       7 (0x7)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldfld      int32 Match01/Test1/X12::item
        IL_0006:  ret
      } // end of method X12::get_Item

      .property instance int32 Item()
      {
        .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                    int32,
                                                                                                    int32) = ( 01 00 04 00 00 00 01 00 00 00 00 00 00 00 00 00 ) 
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        .get instance int32 Match01/Test1/X12::get_Item()
      } // end of property X12::Item
    } // end of class X12

    .class auto ansi serializable nested public beforefieldinit specialname X13
           extends Match01/Test1
    {
      .custom instance void [mscorlib]System.Diagnostics.DebuggerTypeProxyAttribute::.ctor(class [mscorlib]System.Type) = ( 01 00 20 4D 61 74 63 68 30 31 2B 54 65 73 74 31   // .. Match01+Test1
                                                                                                                            2B 58 31 33 40 44 65 62 75 67 54 79 70 65 50 72   // +X13@DebugTypePr
                                                                                                                            6F 78 79 00 00 )                                  // oxy..
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
        // Code size       15 (0xf)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldc.i4.2
        IL_0002:  call       instance void Match01/Test1::.ctor(int32)
        IL_0007:  ldarg.0
        IL_0008:  ldarg.1
        IL_0009:  stfld      int32 Match01/Test1/X13::item
        IL_000e:  ret
      } // end of method X13::.ctor

      .method public hidebysig instance int32 
              get_Item() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       7 (0x7)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldfld      int32 Match01/Test1/X13::item
        IL_0006:  ret
      } // end of method X13::get_Item

      .property instance int32 Item()
      {
        .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                    int32,
                                                                                                    int32) = ( 01 00 04 00 00 00 02 00 00 00 00 00 00 00 00 00 ) 
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        .get instance int32 Match01/Test1/X13::get_Item()
      } // end of property X13::Item
    } // end of class X13

    .class auto ansi serializable nested public beforefieldinit specialname X14
           extends Match01/Test1
    {
      .custom instance void [mscorlib]System.Diagnostics.DebuggerTypeProxyAttribute::.ctor(class [mscorlib]System.Type) = ( 01 00 20 4D 61 74 63 68 30 31 2B 54 65 73 74 31   // .. Match01+Test1
                                                                                                                            2B 58 31 34 40 44 65 62 75 67 54 79 70 65 50 72   // +X14@DebugTypePr
                                                                                                                            6F 78 79 00 00 )                                  // oxy..
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
        // Code size       15 (0xf)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldc.i4.3
        IL_0002:  call       instance void Match01/Test1::.ctor(int32)
        IL_0007:  ldarg.0
        IL_0008:  ldarg.1
        IL_0009:  stfld      int32 Match01/Test1/X14::item
        IL_000e:  ret
      } // end of method X14::.ctor

      .method public hidebysig instance int32 
              get_Item() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       7 (0x7)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldfld      int32 Match01/Test1/X14::item
        IL_0006:  ret
      } // end of method X14::get_Item

      .property instance int32 Item()
      {
        .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                    int32,
                                                                                                    int32) = ( 01 00 04 00 00 00 03 00 00 00 00 00 00 00 00 00 ) 
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        .get instance int32 Match01/Test1/X14::get_Item()
      } // end of property X14::Item
    } // end of class X14

    .class auto ansi nested assembly beforefieldinit specialname X11@DebugTypeProxy
           extends [mscorlib]System.Object
    {
      .field assembly class Match01/Test1/X11 _obj
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .method public specialname rtspecialname 
              instance void  .ctor(class Match01/Test1/X11 obj) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       14 (0xe)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  ldarg.1
        IL_0008:  stfld      class Match01/Test1/X11 Match01/Test1/X11@DebugTypeProxy::_obj
        IL_000d:  ret
      } // end of method X11@DebugTypeProxy::.ctor

      .method public hidebysig instance int32 
              get_Item() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       12 (0xc)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldfld      class Match01/Test1/X11 Match01/Test1/X11@DebugTypeProxy::_obj
        IL_0006:  ldfld      int32 Match01/Test1/X11::item
        IL_000b:  ret
      } // end of method X11@DebugTypeProxy::get_Item

      .property instance int32 Item()
      {
        .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                    int32,
                                                                                                    int32) = ( 01 00 04 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        .get instance int32 Match01/Test1/X11@DebugTypeProxy::get_Item()
      } // end of property X11@DebugTypeProxy::Item
    } // end of class X11@DebugTypeProxy

    .class auto ansi nested assembly beforefieldinit specialname X12@DebugTypeProxy
           extends [mscorlib]System.Object
    {
      .field assembly class Match01/Test1/X12 _obj
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .method public specialname rtspecialname 
              instance void  .ctor(class Match01/Test1/X12 obj) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       14 (0xe)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  ldarg.1
        IL_0008:  stfld      class Match01/Test1/X12 Match01/Test1/X12@DebugTypeProxy::_obj
        IL_000d:  ret
      } // end of method X12@DebugTypeProxy::.ctor

      .method public hidebysig instance int32 
              get_Item() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       12 (0xc)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldfld      class Match01/Test1/X12 Match01/Test1/X12@DebugTypeProxy::_obj
        IL_0006:  ldfld      int32 Match01/Test1/X12::item
        IL_000b:  ret
      } // end of method X12@DebugTypeProxy::get_Item

      .property instance int32 Item()
      {
        .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                    int32,
                                                                                                    int32) = ( 01 00 04 00 00 00 01 00 00 00 00 00 00 00 00 00 ) 
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        .get instance int32 Match01/Test1/X12@DebugTypeProxy::get_Item()
      } // end of property X12@DebugTypeProxy::Item
    } // end of class X12@DebugTypeProxy

    .class auto ansi nested assembly beforefieldinit specialname X13@DebugTypeProxy
           extends [mscorlib]System.Object
    {
      .field assembly class Match01/Test1/X13 _obj
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .method public specialname rtspecialname 
              instance void  .ctor(class Match01/Test1/X13 obj) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       14 (0xe)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  ldarg.1
        IL_0008:  stfld      class Match01/Test1/X13 Match01/Test1/X13@DebugTypeProxy::_obj
        IL_000d:  ret
      } // end of method X13@DebugTypeProxy::.ctor

      .method public hidebysig instance int32 
              get_Item() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       12 (0xc)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldfld      class Match01/Test1/X13 Match01/Test1/X13@DebugTypeProxy::_obj
        IL_0006:  ldfld      int32 Match01/Test1/X13::item
        IL_000b:  ret
      } // end of method X13@DebugTypeProxy::get_Item

      .property instance int32 Item()
      {
        .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                    int32,
                                                                                                    int32) = ( 01 00 04 00 00 00 02 00 00 00 00 00 00 00 00 00 ) 
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        .get instance int32 Match01/Test1/X13@DebugTypeProxy::get_Item()
      } // end of property X13@DebugTypeProxy::Item
    } // end of class X13@DebugTypeProxy

    .class auto ansi nested assembly beforefieldinit specialname X14@DebugTypeProxy
           extends [mscorlib]System.Object
    {
      .field assembly class Match01/Test1/X14 _obj
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .method public specialname rtspecialname 
              instance void  .ctor(class Match01/Test1/X14 obj) cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       14 (0xe)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
        IL_0006:  ldarg.0
        IL_0007:  ldarg.1
        IL_0008:  stfld      class Match01/Test1/X14 Match01/Test1/X14@DebugTypeProxy::_obj
        IL_000d:  ret
      } // end of method X14@DebugTypeProxy::.ctor

      .method public hidebysig instance int32 
              get_Item() cil managed
      {
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        // Code size       12 (0xc)
        .maxstack  8
        IL_0000:  ldarg.0
        IL_0001:  ldfld      class Match01/Test1/X14 Match01/Test1/X14@DebugTypeProxy::_obj
        IL_0006:  ldfld      int32 Match01/Test1/X14::item
        IL_000b:  ret
      } // end of method X14@DebugTypeProxy::get_Item

      .property instance int32 Item()
      {
        .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                    int32,
                                                                                                    int32) = ( 01 00 04 00 00 00 03 00 00 00 00 00 00 00 00 00 ) 
        .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
        .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
        .get instance int32 Match01/Test1/X14@DebugTypeProxy::get_Item()
      } // end of property X14@DebugTypeProxy::Item
    } // end of class X14@DebugTypeProxy

    .field assembly initonly int32 _tag
    .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
    .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
    .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
    .method assembly specialname rtspecialname 
            instance void  .ctor(int32 _tag) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       14 (0xe)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
      IL_0006:  ldarg.0
      IL_0007:  ldarg.1
      IL_0008:  stfld      int32 Match01/Test1::_tag
      IL_000d:  ret
    } // end of method Test1::.ctor

    .method public static class Match01/Test1 
            NewX11(int32 item) cil managed
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                  int32) = ( 01 00 08 00 00 00 00 00 00 00 00 00 ) 
      // Code size       7 (0x7)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  newobj     instance void Match01/Test1/X11::.ctor(int32)
      IL_0006:  ret
    } // end of method Test1::NewX11

    .method public hidebysig instance bool 
            get_IsX11() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       10 (0xa)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  call       instance int32 Match01/Test1::get_Tag()
      IL_0006:  ldc.i4.0
      IL_0007:  ceq
      IL_0009:  ret
    } // end of method Test1::get_IsX11

    .method public static class Match01/Test1 
            NewX12(int32 item) cil managed
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                  int32) = ( 01 00 08 00 00 00 01 00 00 00 00 00 ) 
      // Code size       7 (0x7)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  newobj     instance void Match01/Test1/X12::.ctor(int32)
      IL_0006:  ret
    } // end of method Test1::NewX12

    .method public hidebysig instance bool 
            get_IsX12() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       10 (0xa)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  call       instance int32 Match01/Test1::get_Tag()
      IL_0006:  ldc.i4.1
      IL_0007:  ceq
      IL_0009:  ret
    } // end of method Test1::get_IsX12

    .method public static class Match01/Test1 
            NewX13(int32 item) cil managed
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                  int32) = ( 01 00 08 00 00 00 02 00 00 00 00 00 ) 
      // Code size       7 (0x7)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  newobj     instance void Match01/Test1/X13::.ctor(int32)
      IL_0006:  ret
    } // end of method Test1::NewX13

    .method public hidebysig instance bool 
            get_IsX13() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       10 (0xa)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  call       instance int32 Match01/Test1::get_Tag()
      IL_0006:  ldc.i4.2
      IL_0007:  ceq
      IL_0009:  ret
    } // end of method Test1::get_IsX13

    .method public static class Match01/Test1 
            NewX14(int32 item) cil managed
    {
      .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags,
                                                                                                  int32) = ( 01 00 08 00 00 00 03 00 00 00 00 00 ) 
      // Code size       7 (0x7)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  newobj     instance void Match01/Test1/X14::.ctor(int32)
      IL_0006:  ret
    } // end of method Test1::NewX14

    .method public hidebysig instance bool 
            get_IsX14() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       10 (0xa)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  call       instance int32 Match01/Test1::get_Tag()
      IL_0006:  ldc.i4.3
      IL_0007:  ceq
      IL_0009:  ret
    } // end of method Test1::get_IsX14

    .method public hidebysig instance int32 
            get_Tag() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       7 (0x7)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  ldfld      int32 Match01/Test1::_tag
      IL_0006:  ret
    } // end of method Test1::get_Tag

    .method assembly hidebysig specialname 
            instance object  __DebugDisplay() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       22 (0x16)
      .maxstack  8
      IL_0000:  ldstr      "%+0.8A"
      IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class Match01/Test1,string>,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string,string>::.ctor(string)
      IL_000a:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatToString<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class Match01/Test1,string>>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string>)
      IL_000f:  ldarg.0
      IL_0010:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class Match01/Test1,string>::Invoke(!0)
      IL_0015:  ret
    } // end of method Test1::__DebugDisplay

    .method public strict virtual instance string 
            ToString() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       22 (0x16)
      .maxstack  8
      IL_0000:  ldstr      "%+A"
      IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class Match01/Test1,string>,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string,class Match01/Test1>::.ctor(string)
      IL_000a:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatToString<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class Match01/Test1,string>>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [FSharp.Core]Microsoft.FSharp.Core.Unit,string,string>)
      IL_000f:  ldarg.0
      IL_0010:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class Match01/Test1,string>::Invoke(!0)
      IL_0015:  ret
    } // end of method Test1::ToString

    .method public hidebysig virtual final 
            instance int32  CompareTo(class Match01/Test1 obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       33 (0x21)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse.s  IL_0017

      IL_0006:  ldarg.1
      IL_0007:  ldnull
      IL_0008:  cgt.un
      IL_000a:  brfalse.s  IL_0015

      IL_000c:  ldarg.0
      IL_000d:  ldarg.1
      IL_000e:  ldnull
      IL_000f:  call       int32 Match01::CompareTo$cont@4(class Match01/Test1,
                                                           class Match01/Test1,
                                                           class [FSharp.Core]Microsoft.FSharp.Core.Unit)
      IL_0014:  ret

      IL_0015:  ldc.i4.1
      IL_0016:  ret

      IL_0017:  ldarg.1
      IL_0018:  ldnull
      IL_0019:  cgt.un
      IL_001b:  brfalse.s  IL_001f

      IL_001d:  ldc.i4.m1
      IL_001e:  ret

      IL_001f:  ldc.i4.0
      IL_0020:  ret
    } // end of method Test1::CompareTo

    .method public hidebysig virtual final 
            instance int32  CompareTo(object obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       13 (0xd)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  ldarg.1
      IL_0002:  unbox.any  Match01/Test1
      IL_0007:  callvirt   instance int32 Match01/Test1::CompareTo(class Match01/Test1)
      IL_000c:  ret
    } // end of method Test1::CompareTo

    .method public hidebysig virtual final 
            instance int32  CompareTo(object obj,
                                      class [mscorlib]System.Collections.IComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       50 (0x32)
      .maxstack  5
      .locals init (class Match01/Test1 V_0)
      IL_0000:  ldarg.1
      IL_0001:  unbox.any  Match01/Test1
      IL_0006:  stloc.0
      IL_0007:  ldarg.0
      IL_0008:  ldnull
      IL_0009:  cgt.un
      IL_000b:  brfalse.s  IL_0023

      IL_000d:  ldarg.1
      IL_000e:  unbox.any  Match01/Test1
      IL_0013:  ldnull
      IL_0014:  cgt.un
      IL_0016:  brfalse.s  IL_0021

      IL_0018:  ldarg.0
      IL_0019:  ldloc.0
      IL_001a:  ldnull
      IL_001b:  call       int32 Match01::'CompareTo$cont@4-1'(class Match01/Test1,
                                                               class Match01/Test1,
                                                               class [FSharp.Core]Microsoft.FSharp.Core.Unit)
      IL_0020:  ret

      IL_0021:  ldc.i4.1
      IL_0022:  ret

      IL_0023:  ldarg.1
      IL_0024:  unbox.any  Match01/Test1
      IL_0029:  ldnull
      IL_002a:  cgt.un
      IL_002c:  brfalse.s  IL_0030

      IL_002e:  ldc.i4.m1
      IL_002f:  ret

      IL_0030:  ldc.i4.0
      IL_0031:  ret
    } // end of method Test1::CompareTo

    .method public hidebysig virtual final 
            instance int32  GetHashCode(class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       170 (0xaa)
      .maxstack  7
      .locals init (int32 V_0,
               class Match01/Test1/X11 V_1,
               class Match01/Test1/X12 V_2,
               class Match01/Test1/X13 V_3,
               class Match01/Test1/X14 V_4)
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse    IL_00a8

      IL_0009:  ldc.i4.0
      IL_000a:  stloc.0
      IL_000b:  ldarg.0
      IL_000c:  call       instance int32 Match01/Test1::get_Tag()
      IL_0011:  switch     ( 
                            IL_0026,
                            IL_0046,
                            IL_0066,
                            IL_0086)
      IL_0026:  ldarg.0
      IL_0027:  castclass  Match01/Test1/X11
      IL_002c:  stloc.1
      IL_002d:  ldc.i4.0
      IL_002e:  stloc.0
      IL_002f:  ldc.i4     0x9e3779b9
      IL_0034:  ldloc.1
      IL_0035:  ldfld      int32 Match01/Test1/X11::item
      IL_003a:  ldloc.0
      IL_003b:  ldc.i4.6
      IL_003c:  shl
      IL_003d:  ldloc.0
      IL_003e:  ldc.i4.2
      IL_003f:  shr
      IL_0040:  add
      IL_0041:  add
      IL_0042:  add
      IL_0043:  stloc.0
      IL_0044:  ldloc.0
      IL_0045:  ret

      IL_0046:  ldarg.0
      IL_0047:  castclass  Match01/Test1/X12
      IL_004c:  stloc.2
      IL_004d:  ldc.i4.1
      IL_004e:  stloc.0
      IL_004f:  ldc.i4     0x9e3779b9
      IL_0054:  ldloc.2
      IL_0055:  ldfld      int32 Match01/Test1/X12::item
      IL_005a:  ldloc.0
      IL_005b:  ldc.i4.6
      IL_005c:  shl
      IL_005d:  ldloc.0
      IL_005e:  ldc.i4.2
      IL_005f:  shr
      IL_0060:  add
      IL_0061:  add
      IL_0062:  add
      IL_0063:  stloc.0
      IL_0064:  ldloc.0
      IL_0065:  ret

      IL_0066:  ldarg.0
      IL_0067:  castclass  Match01/Test1/X13
      IL_006c:  stloc.3
      IL_006d:  ldc.i4.2
      IL_006e:  stloc.0
      IL_006f:  ldc.i4     0x9e3779b9
      IL_0074:  ldloc.3
      IL_0075:  ldfld      int32 Match01/Test1/X13::item
      IL_007a:  ldloc.0
      IL_007b:  ldc.i4.6
      IL_007c:  shl
      IL_007d:  ldloc.0
      IL_007e:  ldc.i4.2
      IL_007f:  shr
      IL_0080:  add
      IL_0081:  add
      IL_0082:  add
      IL_0083:  stloc.0
      IL_0084:  ldloc.0
      IL_0085:  ret

      IL_0086:  ldarg.0
      IL_0087:  castclass  Match01/Test1/X14
      IL_008c:  stloc.s    V_4
      IL_008e:  ldc.i4.3
      IL_008f:  stloc.0
      IL_0090:  ldc.i4     0x9e3779b9
      IL_0095:  ldloc.s    V_4
      IL_0097:  ldfld      int32 Match01/Test1/X14::item
      IL_009c:  ldloc.0
      IL_009d:  ldc.i4.6
      IL_009e:  shl
      IL_009f:  ldloc.0
      IL_00a0:  ldc.i4.2
      IL_00a1:  shr
      IL_00a2:  add
      IL_00a3:  add
      IL_00a4:  add
      IL_00a5:  stloc.0
      IL_00a6:  ldloc.0
      IL_00a7:  ret

      IL_00a8:  ldc.i4.0
      IL_00a9:  ret
    } // end of method Test1::GetHashCode

    .method public hidebysig virtual final 
            instance int32  GetHashCode() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       12 (0xc)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  call       class [mscorlib]System.Collections.IEqualityComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericEqualityComparer()
      IL_0006:  callvirt   instance int32 Match01/Test1::GetHashCode(class [mscorlib]System.Collections.IEqualityComparer)
      IL_000b:  ret
    } // end of method Test1::GetHashCode

    .method public hidebysig virtual final 
            instance bool  Equals(object obj,
                                  class [mscorlib]System.Collections.IEqualityComparer comp) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       212 (0xd4)
      .maxstack  4
      .locals init (class Match01/Test1 V_0,
               int32 V_1,
               int32 V_2,
               class Match01/Test1/X11 V_3,
               class Match01/Test1/X11 V_4,
               class Match01/Test1/X12 V_5,
               class Match01/Test1/X12 V_6,
               class Match01/Test1/X13 V_7,
               class Match01/Test1/X13 V_8,
               class Match01/Test1/X14 V_9,
               class Match01/Test1/X14 V_10)
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse    IL_00cc

      IL_0009:  ldarg.1
      IL_000a:  isinst     Match01/Test1
      IL_000f:  stloc.0
      IL_0010:  ldloc.0
      IL_0011:  brfalse    IL_00ca

      IL_0016:  ldarg.0
      IL_0017:  ldfld      int32 Match01/Test1::_tag
      IL_001c:  stloc.1
      IL_001d:  ldloc.0
      IL_001e:  ldfld      int32 Match01/Test1::_tag
      IL_0023:  stloc.2
      IL_0024:  ldloc.1
      IL_0025:  ldloc.2
      IL_0026:  bne.un     IL_00c8

      IL_002b:  ldarg.0
      IL_002c:  call       instance int32 Match01/Test1::get_Tag()
      IL_0031:  switch     ( 
                            IL_0046,
                            IL_0065,
                            IL_0086,
                            IL_00a7)
      IL_0046:  ldarg.0
      IL_0047:  castclass  Match01/Test1/X11
      IL_004c:  stloc.3
      IL_004d:  ldloc.0
      IL_004e:  castclass  Match01/Test1/X11
      IL_0053:  stloc.s    V_4
      IL_0055:  ldloc.3
      IL_0056:  ldfld      int32 Match01/Test1/X11::item
      IL_005b:  ldloc.s    V_4
      IL_005d:  ldfld      int32 Match01/Test1/X11::item
      IL_0062:  ceq
      IL_0064:  ret

      IL_0065:  ldarg.0
      IL_0066:  castclass  Match01/Test1/X12
      IL_006b:  stloc.s    V_5
      IL_006d:  ldloc.0
      IL_006e:  castclass  Match01/Test1/X12
      IL_0073:  stloc.s    V_6
      IL_0075:  ldloc.s    V_5
      IL_0077:  ldfld      int32 Match01/Test1/X12::item
      IL_007c:  ldloc.s    V_6
      IL_007e:  ldfld      int32 Match01/Test1/X12::item
      IL_0083:  ceq
      IL_0085:  ret

      IL_0086:  ldarg.0
      IL_0087:  castclass  Match01/Test1/X13
      IL_008c:  stloc.s    V_7
      IL_008e:  ldloc.0
      IL_008f:  castclass  Match01/Test1/X13
      IL_0094:  stloc.s    V_8
      IL_0096:  ldloc.s    V_7
      IL_0098:  ldfld      int32 Match01/Test1/X13::item
      IL_009d:  ldloc.s    V_8
      IL_009f:  ldfld      int32 Match01/Test1/X13::item
      IL_00a4:  ceq
      IL_00a6:  ret

      IL_00a7:  ldarg.0
      IL_00a8:  castclass  Match01/Test1/X14
      IL_00ad:  stloc.s    V_9
      IL_00af:  ldloc.0
      IL_00b0:  castclass  Match01/Test1/X14
      IL_00b5:  stloc.s    V_10
      IL_00b7:  ldloc.s    V_9
      IL_00b9:  ldfld      int32 Match01/Test1/X14::item
      IL_00be:  ldloc.s    V_10
      IL_00c0:  ldfld      int32 Match01/Test1/X14::item
      IL_00c5:  ceq
      IL_00c7:  ret

      IL_00c8:  ldc.i4.0
      IL_00c9:  ret

      IL_00ca:  ldc.i4.0
      IL_00cb:  ret

      IL_00cc:  ldarg.1
      IL_00cd:  ldnull
      IL_00ce:  cgt.un
      IL_00d0:  ldc.i4.0
      IL_00d1:  ceq
      IL_00d3:  ret
    } // end of method Test1::Equals

    .method public hidebysig virtual final 
            instance bool  Equals(class Match01/Test1 obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       206 (0xce)
      .maxstack  4
      .locals init (int32 V_0,
               int32 V_1,
               class Match01/Test1/X11 V_2,
               class Match01/Test1/X11 V_3,
               class Match01/Test1/X12 V_4,
               class Match01/Test1/X12 V_5,
               class Match01/Test1/X13 V_6,
               class Match01/Test1/X13 V_7,
               class Match01/Test1/X14 V_8,
               class Match01/Test1/X14 V_9)
      IL_0000:  ldarg.0
      IL_0001:  ldnull
      IL_0002:  cgt.un
      IL_0004:  brfalse    IL_00c6

      IL_0009:  ldarg.1
      IL_000a:  ldnull
      IL_000b:  cgt.un
      IL_000d:  brfalse    IL_00c4

      IL_0012:  ldarg.0
      IL_0013:  ldfld      int32 Match01/Test1::_tag
      IL_0018:  stloc.0
      IL_0019:  ldarg.1
      IL_001a:  ldfld      int32 Match01/Test1::_tag
      IL_001f:  stloc.1
      IL_0020:  ldloc.0
      IL_0021:  ldloc.1
      IL_0022:  bne.un     IL_00c2

      IL_0027:  ldarg.0
      IL_0028:  call       instance int32 Match01/Test1::get_Tag()
      IL_002d:  switch     ( 
                            IL_0042,
                            IL_005f,
                            IL_0080,
                            IL_00a1)
      IL_0042:  ldarg.0
      IL_0043:  castclass  Match01/Test1/X11
      IL_0048:  stloc.2
      IL_0049:  ldarg.1
      IL_004a:  castclass  Match01/Test1/X11
      IL_004f:  stloc.3
      IL_0050:  ldloc.2
      IL_0051:  ldfld      int32 Match01/Test1/X11::item
      IL_0056:  ldloc.3
      IL_0057:  ldfld      int32 Match01/Test1/X11::item
      IL_005c:  ceq
      IL_005e:  ret

      IL_005f:  ldarg.0
      IL_0060:  castclass  Match01/Test1/X12
      IL_0065:  stloc.s    V_4
      IL_0067:  ldarg.1
      IL_0068:  castclass  Match01/Test1/X12
      IL_006d:  stloc.s    V_5
      IL_006f:  ldloc.s    V_4
      IL_0071:  ldfld      int32 Match01/Test1/X12::item
      IL_0076:  ldloc.s    V_5
      IL_0078:  ldfld      int32 Match01/Test1/X12::item
      IL_007d:  ceq
      IL_007f:  ret

      IL_0080:  ldarg.0
      IL_0081:  castclass  Match01/Test1/X13
      IL_0086:  stloc.s    V_6
      IL_0088:  ldarg.1
      IL_0089:  castclass  Match01/Test1/X13
      IL_008e:  stloc.s    V_7
      IL_0090:  ldloc.s    V_6
      IL_0092:  ldfld      int32 Match01/Test1/X13::item
      IL_0097:  ldloc.s    V_7
      IL_0099:  ldfld      int32 Match01/Test1/X13::item
      IL_009e:  ceq
      IL_00a0:  ret

      IL_00a1:  ldarg.0
      IL_00a2:  castclass  Match01/Test1/X14
      IL_00a7:  stloc.s    V_8
      IL_00a9:  ldarg.1
      IL_00aa:  castclass  Match01/Test1/X14
      IL_00af:  stloc.s    V_9
      IL_00b1:  ldloc.s    V_8
      IL_00b3:  ldfld      int32 Match01/Test1/X14::item
      IL_00b8:  ldloc.s    V_9
      IL_00ba:  ldfld      int32 Match01/Test1/X14::item
      IL_00bf:  ceq
      IL_00c1:  ret

      IL_00c2:  ldc.i4.0
      IL_00c3:  ret

      IL_00c4:  ldc.i4.0
      IL_00c5:  ret

      IL_00c6:  ldarg.1
      IL_00c7:  ldnull
      IL_00c8:  cgt.un
      IL_00ca:  ldc.i4.0
      IL_00cb:  ceq
      IL_00cd:  ret
    } // end of method Test1::Equals

    .method public hidebysig virtual final 
            instance bool  Equals(object obj) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       20 (0x14)
      .maxstack  4
      .locals init (class Match01/Test1 V_0)
      IL_0000:  ldarg.1
      IL_0001:  isinst     Match01/Test1
      IL_0006:  stloc.0
      IL_0007:  ldloc.0
      IL_0008:  brfalse.s  IL_0012

      IL_000a:  ldarg.0
      IL_000b:  ldloc.0
      IL_000c:  callvirt   instance bool Match01/Test1::Equals(class Match01/Test1)
      IL_0011:  ret

      IL_0012:  ldc.i4.0
      IL_0013:  ret
    } // end of method Test1::Equals

    .property instance int32 Tag()
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .get instance int32 Match01/Test1::get_Tag()
    } // end of property Test1::Tag
    .property instance bool IsX11()
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .get instance bool Match01/Test1::get_IsX11()
    } // end of property Test1::IsX11
    .property instance bool IsX12()
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .get instance bool Match01/Test1::get_IsX12()
    } // end of property Test1::IsX12
    .property instance bool IsX13()
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .get instance bool Match01/Test1::get_IsX13()
    } // end of property Test1::IsX13
    .property instance bool IsX14()
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
      .get instance bool Match01/Test1::get_IsX14()
    } // end of property Test1::IsX14
  } // end of class Test1

  .method assembly static int32  CompareTo$cont@4(class Match01/Test1 this,
                                                  class Match01/Test1 obj,
                                                  class [FSharp.Core]Microsoft.FSharp.Core.Unit unitVar) cil managed
  {
    .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
    // Code size       272 (0x110)
    .maxstack  4
    .locals init (int32 V_0,
             int32 V_1,
             class Match01/Test1/X11 V_2,
             class Match01/Test1/X11 V_3,
             class [mscorlib]System.Collections.IComparer V_4,
             int32 V_5,
             int32 V_6,
             class Match01/Test1/X12 V_7,
             class Match01/Test1/X12 V_8,
             class Match01/Test1/X13 V_9,
             class Match01/Test1/X13 V_10,
             class Match01/Test1/X14 V_11,
             class Match01/Test1/X14 V_12)
    IL_0000:  ldarg.0
    IL_0001:  ldfld      int32 Match01/Test1::_tag
    IL_0006:  stloc.0
    IL_0007:  ldarg.1
    IL_0008:  ldfld      int32 Match01/Test1::_tag
    IL_000d:  stloc.1
    IL_000e:  ldloc.0
    IL_000f:  ldloc.1
    IL_0010:  bne.un     IL_010c

    IL_0015:  ldarg.0
    IL_0016:  call       instance int32 Match01/Test1::get_Tag()
    IL_001b:  switch     ( 
                          IL_0030,
                          IL_0064,
                          IL_009c,
                          IL_00d4)
    IL_0030:  ldarg.0
    IL_0031:  castclass  Match01/Test1/X11
    IL_0036:  stloc.2
    IL_0037:  ldarg.1
    IL_0038:  castclass  Match01/Test1/X11
    IL_003d:  stloc.3
    IL_003e:  call       class [mscorlib]System.Collections.IComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericComparer()
    IL_0043:  stloc.s    V_4
    IL_0045:  ldloc.2
    IL_0046:  ldfld      int32 Match01/Test1/X11::item
    IL_004b:  stloc.s    V_5
    IL_004d:  ldloc.3
    IL_004e:  ldfld      int32 Match01/Test1/X11::item
    IL_0053:  stloc.s    V_6
    IL_0055:  ldloc.s    V_5
    IL_0057:  ldloc.s    V_6
    IL_0059:  bge.s      IL_005d

    IL_005b:  ldc.i4.m1
    IL_005c:  ret

    IL_005d:  ldloc.s    V_5
    IL_005f:  ldloc.s    V_6
    IL_0061:  cgt
    IL_0063:  ret

    IL_0064:  ldarg.0
    IL_0065:  castclass  Match01/Test1/X12
    IL_006a:  stloc.s    V_7
    IL_006c:  ldarg.1
    IL_006d:  castclass  Match01/Test1/X12
    IL_0072:  stloc.s    V_8
    IL_0074:  call       class [mscorlib]System.Collections.IComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericComparer()
    IL_0079:  stloc.s    V_4
    IL_007b:  ldloc.s    V_7
    IL_007d:  ldfld      int32 Match01/Test1/X12::item
    IL_0082:  stloc.s    V_5
    IL_0084:  ldloc.s    V_8
    IL_0086:  ldfld      int32 Match01/Test1/X12::item
    IL_008b:  stloc.s    V_6
    IL_008d:  ldloc.s    V_5
    IL_008f:  ldloc.s    V_6
    IL_0091:  bge.s      IL_0095

    IL_0093:  ldc.i4.m1
    IL_0094:  ret

    IL_0095:  ldloc.s    V_5
    IL_0097:  ldloc.s    V_6
    IL_0099:  cgt
    IL_009b:  ret

    IL_009c:  ldarg.0
    IL_009d:  castclass  Match01/Test1/X13
    IL_00a2:  stloc.s    V_9
    IL_00a4:  ldarg.1
    IL_00a5:  castclass  Match01/Test1/X13
    IL_00aa:  stloc.s    V_10
    IL_00ac:  call       class [mscorlib]System.Collections.IComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericComparer()
    IL_00b1:  stloc.s    V_4
    IL_00b3:  ldloc.s    V_9
    IL_00b5:  ldfld      int32 Match01/Test1/X13::item
    IL_00ba:  stloc.s    V_5
    IL_00bc:  ldloc.s    V_10
    IL_00be:  ldfld      int32 Match01/Test1/X13::item
    IL_00c3:  stloc.s    V_6
    IL_00c5:  ldloc.s    V_5
    IL_00c7:  ldloc.s    V_6
    IL_00c9:  bge.s      IL_00cd

    IL_00cb:  ldc.i4.m1
    IL_00cc:  ret

    IL_00cd:  ldloc.s    V_5
    IL_00cf:  ldloc.s    V_6
    IL_00d1:  cgt
    IL_00d3:  ret

    IL_00d4:  ldarg.0
    IL_00d5:  castclass  Match01/Test1/X14
    IL_00da:  stloc.s    V_11
    IL_00dc:  ldarg.1
    IL_00dd:  castclass  Match01/Test1/X14
    IL_00e2:  stloc.s    V_12
    IL_00e4:  call       class [mscorlib]System.Collections.IComparer [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives::get_GenericComparer()
    IL_00e9:  stloc.s    V_4
    IL_00eb:  ldloc.s    V_11
    IL_00ed:  ldfld      int32 Match01/Test1/X14::item
    IL_00f2:  stloc.s    V_5
    IL_00f4:  ldloc.s    V_12
    IL_00f6:  ldfld      int32 Match01/Test1/X14::item
    IL_00fb:  stloc.s    V_6
    IL_00fd:  ldloc.s    V_5
    IL_00ff:  ldloc.s    V_6
    IL_0101:  bge.s      IL_0105

    IL_0103:  ldc.i4.m1
    IL_0104:  ret

    IL_0105:  ldloc.s    V_5
    IL_0107:  ldloc.s    V_6
    IL_0109:  cgt
    IL_010b:  ret

    IL_010c:  ldloc.0
    IL_010d:  ldloc.1
    IL_010e:  sub
    IL_010f:  ret
  } // end of method Match01::CompareTo$cont@4

  .method assembly static int32  'CompareTo$cont@4-1'(class Match01/Test1 this,
                                                      class Match01/Test1 objTemp,
                                                      class [FSharp.Core]Microsoft.FSharp.Core.Unit unitVar) cil managed
  {
    .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
    // Code size       244 (0xf4)
    .maxstack  4
    .locals init (int32 V_0,
             int32 V_1,
             class Match01/Test1/X11 V_2,
             class Match01/Test1/X11 V_3,
             int32 V_4,
             int32 V_5,
             class Match01/Test1/X12 V_6,
             class Match01/Test1/X12 V_7,
             class Match01/Test1/X13 V_8,
             class Match01/Test1/X13 V_9,
             class Match01/Test1/X14 V_10,
             class Match01/Test1/X14 V_11)
    IL_0000:  ldarg.0
    IL_0001:  ldfld      int32 Match01/Test1::_tag
    IL_0006:  stloc.0
    IL_0007:  ldarg.1
    IL_0008:  ldfld      int32 Match01/Test1::_tag
    IL_000d:  stloc.1
    IL_000e:  ldloc.0
    IL_000f:  ldloc.1
    IL_0010:  bne.un     IL_00f0

    IL_0015:  ldarg.0
    IL_0016:  call       instance int32 Match01/Test1::get_Tag()
    IL_001b:  switch     ( 
                          IL_0030,
                          IL_005d,
                          IL_008e,
                          IL_00bf)
    IL_0030:  ldarg.0
    IL_0031:  castclass  Match01/Test1/X11
    IL_0036:  stloc.2
    IL_0037:  ldarg.1
    IL_0038:  castclass  Match01/Test1/X11
    IL_003d:  stloc.3
    IL_003e:  ldloc.2
    IL_003f:  ldfld      int32 Match01/Test1/X11::item
    IL_0044:  stloc.s    V_4
    IL_0046:  ldloc.3
    IL_0047:  ldfld      int32 Match01/Test1/X11::item
    IL_004c:  stloc.s    V_5
    IL_004e:  ldloc.s    V_4
    IL_0050:  ldloc.s    V_5
    IL_0052:  bge.s      IL_0056

    IL_0054:  ldc.i4.m1
    IL_0055:  ret

    IL_0056:  ldloc.s    V_4
    IL_0058:  ldloc.s    V_5
    IL_005a:  cgt
    IL_005c:  ret

    IL_005d:  ldarg.0
    IL_005e:  castclass  Match01/Test1/X12
    IL_0063:  stloc.s    V_6
    IL_0065:  ldarg.1
    IL_0066:  castclass  Match01/Test1/X12
    IL_006b:  stloc.s    V_7
    IL_006d:  ldloc.s    V_6
    IL_006f:  ldfld      int32 Match01/Test1/X12::item
    IL_0074:  stloc.s    V_4
    IL_0076:  ldloc.s    V_7
    IL_0078:  ldfld      int32 Match01/Test1/X12::item
    IL_007d:  stloc.s    V_5
    IL_007f:  ldloc.s    V_4
    IL_0081:  ldloc.s    V_5
    IL_0083:  bge.s      IL_0087

    IL_0085:  ldc.i4.m1
    IL_0086:  ret

    IL_0087:  ldloc.s    V_4
    IL_0089:  ldloc.s    V_5
    IL_008b:  cgt
    IL_008d:  ret

    IL_008e:  ldarg.0
    IL_008f:  castclass  Match01/Test1/X13
    IL_0094:  stloc.s    V_8
    IL_0096:  ldarg.1
    IL_0097:  castclass  Match01/Test1/X13
    IL_009c:  stloc.s    V_9
    IL_009e:  ldloc.s    V_8
    IL_00a0:  ldfld      int32 Match01/Test1/X13::item
    IL_00a5:  stloc.s    V_4
    IL_00a7:  ldloc.s    V_9
    IL_00a9:  ldfld      int32 Match01/Test1/X13::item
    IL_00ae:  stloc.s    V_5
    IL_00b0:  ldloc.s    V_4
    IL_00b2:  ldloc.s    V_5
    IL_00b4:  bge.s      IL_00b8

    IL_00b6:  ldc.i4.m1
    IL_00b7:  ret

    IL_00b8:  ldloc.s    V_4
    IL_00ba:  ldloc.s    V_5
    IL_00bc:  cgt
    IL_00be:  ret

    IL_00bf:  ldarg.0
    IL_00c0:  castclass  Match01/Test1/X14
    IL_00c5:  stloc.s    V_10
    IL_00c7:  ldarg.1
    IL_00c8:  castclass  Match01/Test1/X14
    IL_00cd:  stloc.s    V_11
    IL_00cf:  ldloc.s    V_10
    IL_00d1:  ldfld      int32 Match01/Test1/X14::item
    IL_00d6:  stloc.s    V_4
    IL_00d8:  ldloc.s    V_11
    IL_00da:  ldfld      int32 Match01/Test1/X14::item
    IL_00df:  stloc.s    V_5
    IL_00e1:  ldloc.s    V_4
    IL_00e3:  ldloc.s    V_5
    IL_00e5:  bge.s      IL_00e9

    IL_00e7:  ldc.i4.m1
    IL_00e8:  ret

    IL_00e9:  ldloc.s    V_4
    IL_00eb:  ldloc.s    V_5
    IL_00ed:  cgt
    IL_00ef:  ret

    IL_00f0:  ldloc.0
    IL_00f1:  ldloc.1
    IL_00f2:  sub
    IL_00f3:  ret
  } // end of method Match01::'CompareTo$cont@4-1'

  .method public static int32  select1(class Match01/Test1 x) cil managed
  {
    // Code size       45 (0x2d)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance int32 Match01/Test1::get_Tag()
    IL_0006:  switch     ( 
                          IL_001b,
                          IL_0027,
                          IL_0029,
                          IL_002b)
    IL_001b:  ldarg.0
    IL_001c:  castclass  Match01/Test1/X11
    IL_0021:  ldfld      int32 Match01/Test1/X11::item
    IL_0026:  ret

    IL_0027:  ldc.i4.2
    IL_0028:  ret

    IL_0029:  ldc.i4.3
    IL_002a:  ret

    IL_002b:  ldc.i4.4
    IL_002c:  ret
  } // end of method Match01::select1

  .method public static int32  fm(class Match01/Test1 y) cil managed
  {
    // Code size       7 (0x7)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       int32 Match01::select1(class Match01/Test1)
    IL_0006:  ret
  } // end of method Match01::fm

} // end of class Match01

.class private abstract auto ansi sealed '<StartupCode$Match01>'.$Match01
       extends [mscorlib]System.Object
{
} // end of class '<StartupCode$Match01>'.$Match01


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
