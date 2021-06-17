
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
.assembly TestFunction20
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.TestFunction20
{
  // Offset: 0x00000000 Length: 0x0000038F
}
.mresource public FSharpOptimizationData.TestFunction20
{
  // Offset: 0x00000398 Length: 0x00000100
}
.module TestFunction20.exe
// MVID: {60B68B97-A643-44FB-A745-0383978BB660}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x06B80000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed TestFunction20
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class auto ansi serializable nested public D
         extends [mscorlib]System.Object
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 03 00 00 00 00 00 ) 
    .field assembly int32 y
    .field assembly int32 x
    .method public specialname rtspecialname 
            instance void  .ctor(int32 x,
                                 int32 y) cil managed
    {
      // Code size       47 (0x2f)
      .maxstack  4
      .locals init ([0] int32 z,
               [1] int32 w)
      .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
      .line 100001,100001 : 0,0 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\TestFunctions\\TestFunction20.fs'
      IL_0000:  ldarg.0
      IL_0001:  callvirt   instance void [mscorlib]System.Object::.ctor()
      IL_0006:  ldarg.0
      IL_0007:  pop
      .line 4,4 : 8,9 ''
      IL_0008:  ldarg.0
      IL_0009:  ldarg.1
      IL_000a:  stfld      int32 TestFunction20/D::x
      .line 4,4 : 14,15 ''
      IL_000f:  ldarg.0
      IL_0010:  ldarg.2
      IL_0011:  stfld      int32 TestFunction20/D::y
      .line 5,5 : 5,18 ''
      IL_0016:  ldarg.0
      IL_0017:  ldfld      int32 TestFunction20/D::x
      IL_001c:  ldarg.0
      IL_001d:  ldfld      int32 TestFunction20/D::y
      IL_0022:  add
      IL_0023:  stloc.0
      .line 7,7 : 5,20 ''
      IL_0024:  ldarg.0
      IL_0025:  ldloc.0
      IL_0026:  callvirt   instance int32 TestFunction20/D::f(int32)
      IL_002b:  ldloc.0
      IL_002c:  add
      IL_002d:  stloc.1
      .line 4,4 : 6,7 ''
      IL_002e:  ret
    } // end of method D::.ctor

    .method public hidebysig specialname 
            instance int32  get_X() cil managed
    {
      // Code size       7 (0x7)
      .maxstack  8
      .line 8,8 : 21,22 ''
      IL_0000:  ldarg.0
      IL_0001:  ldfld      int32 TestFunction20/D::x
      IL_0006:  ret
    } // end of method D::get_X

    .method public hidebysig specialname 
            instance int32  get_Y() cil managed
    {
      // Code size       7 (0x7)
      .maxstack  8
      .line 9,9 : 21,22 ''
      IL_0000:  ldarg.0
      IL_0001:  ldfld      int32 TestFunction20/D::y
      IL_0006:  ret
    } // end of method D::get_Y

    .method assembly hidebysig instance int32 
            f(int32 a) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       9 (0x9)
      .maxstack  8
      .line 6,6 : 15,20 ''
      IL_0000:  ldarg.0
      IL_0001:  ldfld      int32 TestFunction20/D::x
      IL_0006:  ldarg.1
      IL_0007:  add
      IL_0008:  ret
    } // end of method D::f

    .property instance int32 X()
    {
      .get instance int32 TestFunction20/D::get_X()
    } // end of property D::X
    .property instance int32 Y()
    {
      .get instance int32 TestFunction20/D::get_Y()
    } // end of property D::Y
  } // end of class D

  .class auto ansi serializable sealed nested assembly beforefieldinit 'TestFunction20@14-1'
         extends class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.Unit>
  {
    .field public class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.Unit> clo2
    .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
    .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
    .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
    .method assembly specialname rtspecialname 
            instance void  .ctor(class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.Unit> clo2) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       14 (0xe)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  call       instance void class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor()
      IL_0006:  ldarg.0
      IL_0007:  ldarg.1
      IL_0008:  stfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.Unit> TestFunction20/'TestFunction20@14-1'::clo2
      IL_000d:  ret
    } // end of method 'TestFunction20@14-1'::.ctor

    .method public strict virtual instance class [FSharp.Core]Microsoft.FSharp.Core.Unit 
            Invoke(class TestFunction20/D arg20) cil managed
    {
      // Code size       15 (0xf)
      .maxstack  8
      .line 14,14 : 5,31 ''
      IL_0000:  ldarg.0
      IL_0001:  ldfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.Unit> TestFunction20/'TestFunction20@14-1'::clo2
      IL_0006:  ldarg.1
      IL_0007:  tail.
      IL_0009:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::Invoke(!0)
      IL_000e:  ret
    } // end of method 'TestFunction20@14-1'::Invoke

  } // end of class 'TestFunction20@14-1'

  .class auto ansi serializable sealed nested assembly beforefieldinit TestFunction20@14
         extends class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.Unit>>
  {
    .field public class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.Unit>> clo1
    .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
    .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
    .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
    .method assembly specialname rtspecialname 
            instance void  .ctor(class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.Unit>> clo1) cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       14 (0xe)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  call       instance void class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.Unit>>::.ctor()
      IL_0006:  ldarg.0
      IL_0007:  ldarg.1
      IL_0008:  stfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.Unit>> TestFunction20/TestFunction20@14::clo1
      IL_000d:  ret
    } // end of method TestFunction20@14::.ctor

    .method public strict virtual instance class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.Unit> 
            Invoke(class TestFunction20/D arg10) cil managed
    {
      // Code size       20 (0x14)
      .maxstack  6
      .locals init ([0] class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.Unit> V_0)
      .line 14,14 : 5,31 ''
      IL_0000:  ldarg.0
      IL_0001:  ldfld      class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.Unit>> TestFunction20/TestFunction20@14::clo1
      IL_0006:  ldarg.1
      IL_0007:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.Unit>>::Invoke(!0)
      IL_000c:  stloc.0
      IL_000d:  ldloc.0
      IL_000e:  newobj     instance void TestFunction20/'TestFunction20@14-1'::.ctor(class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
      IL_0013:  ret
    } // end of method TestFunction20@14::Invoke

  } // end of class TestFunction20@14

  .method public static void  TestFunction20(int32 inp) cil managed
  {
    // Code size       47 (0x2f)
    .maxstack  5
    .locals init ([0] class TestFunction20/D d1,
             [1] class TestFunction20/D d2,
             [2] class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.Unit>> V_2)
    .line 12,12 : 5,24 ''
    IL_0000:  ldarg.0
    IL_0001:  ldarg.0
    IL_0002:  newobj     instance void TestFunction20/D::.ctor(int32,
                                                               int32)
    IL_0007:  stloc.0
    .line 13,13 : 5,24 ''
    IL_0008:  ldarg.0
    IL_0009:  ldarg.0
    IL_000a:  newobj     instance void TestFunction20/D::.ctor(int32,
                                                               int32)
    IL_000f:  stloc.1
    .line 14,14 : 5,31 ''
    IL_0010:  ldstr      "d1 = %A, d2 = %A"
    IL_0015:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.Unit>>,class [mscorlib]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [mscorlib]System.Tuple`2<class TestFunction20/D,class TestFunction20/D>>::.ctor(string)
    IL_001a:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatLine<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.Unit>>>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [mscorlib]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
    IL_001f:  stloc.2
    IL_0020:  ldloc.2
    IL_0021:  newobj     instance void TestFunction20/TestFunction20@14::.ctor(class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class [FSharp.Core]Microsoft.FSharp.Core.Unit>>)
    IL_0026:  ldloc.0
    IL_0027:  ldloc.1
    IL_0028:  call       !!0 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class TestFunction20/D,class TestFunction20/D>::InvokeFast<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<!0,class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<!1,!!0>>,
                                                                                                                                                                                             !0,
                                                                                                                                                                                             !1)
    IL_002d:  pop
    IL_002e:  ret
  } // end of method TestFunction20::TestFunction20

} // end of class TestFunction20

.class private abstract auto ansi sealed '<StartupCode$TestFunction20>'.$TestFunction20
       extends [mscorlib]System.Object
{
  .method public static void  main@() cil managed
  {
    .entrypoint
    // Code size       1 (0x1)
    .maxstack  8
    IL_0000:  ret
  } // end of method $TestFunction20::main@

} // end of class '<StartupCode$TestFunction20>'.$TestFunction20


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
