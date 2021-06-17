
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
.assembly CodeGenRenamings01
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.CodeGenRenamings01
{
  // Offset: 0x00000000 Length: 0x000003C8
}
.mresource public FSharpOptimizationData.CodeGenRenamings01
{
  // Offset: 0x000003D0 Length: 0x0000011B
}
.module CodeGenRenamings01.exe
// MVID: {60B78A57-8173-986B-A745-0383578AB760}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x06CA0000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed CodeGenRenamings01
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .class auto autochar serializable sealed nested assembly beforefieldinit specialname seq1@9
         extends class [FSharp.Core]Microsoft.FSharp.Core.CompilerServices.GeneratedSequenceBase`1<class [mscorlib]System.Tuple`2<int32,int32>>
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 06 00 00 00 00 00 ) 
    .field public int32 pc
    .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
    .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
    .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
    .field public class [mscorlib]System.Tuple`2<int32,int32> current
    .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
    .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
    .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
    .method public specialname rtspecialname 
            instance void  .ctor(int32 pc,
                                 class [mscorlib]System.Tuple`2<int32,int32> current) cil managed
    {
      // Code size       21 (0x15)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  ldarg.1
      IL_0002:  stfld      int32 CodeGenRenamings01/seq1@9::pc
      IL_0007:  ldarg.0
      IL_0008:  ldarg.2
      IL_0009:  stfld      class [mscorlib]System.Tuple`2<int32,int32> CodeGenRenamings01/seq1@9::current
      IL_000e:  ldarg.0
      IL_000f:  call       instance void class [FSharp.Core]Microsoft.FSharp.Core.CompilerServices.GeneratedSequenceBase`1<class [mscorlib]System.Tuple`2<int32,int32>>::.ctor()
      IL_0014:  ret
    } // end of method seq1@9::.ctor

    .method public strict virtual instance int32 
            GenerateNext(class [mscorlib]System.Collections.Generic.IEnumerable`1<class [mscorlib]System.Tuple`2<int32,int32>>& next) cil managed
    {
      // Code size       97 (0x61)
      .maxstack  7
      .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
      .line 100001,100001 : 0,0 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\Misc\\CodeGenRenamings01.fs'
      IL_0000:  ldarg.0
      IL_0001:  ldfld      int32 CodeGenRenamings01/seq1@9::pc
      IL_0006:  ldc.i4.1
      IL_0007:  sub
      IL_0008:  switch     ( 
                            IL_001b,
                            IL_001e,
                            IL_0021)
      IL_0019:  br.s       IL_0024

      .line 100001,100001 : 0,0 ''
      IL_001b:  nop
      IL_001c:  br.s       IL_003b

      .line 100001,100001 : 0,0 ''
      IL_001e:  nop
      IL_001f:  br.s       IL_0051

      .line 100001,100001 : 0,0 ''
      IL_0021:  nop
      IL_0022:  br.s       IL_0058

      .line 100001,100001 : 0,0 ''
      IL_0024:  nop
      .line 9,9 : 18,30 ''
      IL_0025:  ldarg.0
      IL_0026:  ldc.i4.1
      IL_0027:  stfld      int32 CodeGenRenamings01/seq1@9::pc
      IL_002c:  ldarg.0
      IL_002d:  ldc.i4.1
      IL_002e:  ldc.i4.1
      IL_002f:  newobj     instance void class [mscorlib]System.Tuple`2<int32,int32>::.ctor(!0,
                                                                                            !1)
      IL_0034:  stfld      class [mscorlib]System.Tuple`2<int32,int32> CodeGenRenamings01/seq1@9::current
      IL_0039:  ldc.i4.1
      IL_003a:  ret

      .line 9,9 : 32,44 ''
      IL_003b:  ldarg.0
      IL_003c:  ldc.i4.2
      IL_003d:  stfld      int32 CodeGenRenamings01/seq1@9::pc
      IL_0042:  ldarg.0
      IL_0043:  ldc.i4.2
      IL_0044:  ldc.i4.2
      IL_0045:  newobj     instance void class [mscorlib]System.Tuple`2<int32,int32>::.ctor(!0,
                                                                                            !1)
      IL_004a:  stfld      class [mscorlib]System.Tuple`2<int32,int32> CodeGenRenamings01/seq1@9::current
      IL_004f:  ldc.i4.1
      IL_0050:  ret

      IL_0051:  ldarg.0
      IL_0052:  ldc.i4.3
      IL_0053:  stfld      int32 CodeGenRenamings01/seq1@9::pc
      IL_0058:  ldarg.0
      IL_0059:  ldnull
      IL_005a:  stfld      class [mscorlib]System.Tuple`2<int32,int32> CodeGenRenamings01/seq1@9::current
      IL_005f:  ldc.i4.0
      IL_0060:  ret
    } // end of method seq1@9::GenerateNext

    .method public strict virtual instance void 
            Close() cil managed
    {
      // Code size       8 (0x8)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  ldc.i4.3
      IL_0002:  stfld      int32 CodeGenRenamings01/seq1@9::pc
      IL_0007:  ret
    } // end of method seq1@9::Close

    .method public strict virtual instance bool 
            get_CheckClose() cil managed
    {
      // Code size       48 (0x30)
      .maxstack  8
      .line 100001,100001 : 0,0 ''
      IL_0000:  ldarg.0
      IL_0001:  ldfld      int32 CodeGenRenamings01/seq1@9::pc
      IL_0006:  switch     ( 
                            IL_001d,
                            IL_0020,
                            IL_0023,
                            IL_0026)
      IL_001b:  br.s       IL_0029

      .line 100001,100001 : 0,0 ''
      IL_001d:  nop
      IL_001e:  br.s       IL_002e

      .line 100001,100001 : 0,0 ''
      IL_0020:  nop
      IL_0021:  br.s       IL_002c

      .line 100001,100001 : 0,0 ''
      IL_0023:  nop
      IL_0024:  br.s       IL_002a

      .line 100001,100001 : 0,0 ''
      IL_0026:  nop
      IL_0027:  br.s       IL_002e

      .line 100001,100001 : 0,0 ''
      IL_0029:  nop
      IL_002a:  ldc.i4.0
      IL_002b:  ret

      IL_002c:  ldc.i4.0
      IL_002d:  ret

      IL_002e:  ldc.i4.0
      IL_002f:  ret
    } // end of method seq1@9::get_CheckClose

    .method public strict virtual instance class [mscorlib]System.Tuple`2<int32,int32> 
            get_LastGenerated() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       7 (0x7)
      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  ldfld      class [mscorlib]System.Tuple`2<int32,int32> CodeGenRenamings01/seq1@9::current
      IL_0006:  ret
    } // end of method seq1@9::get_LastGenerated

    .method public strict virtual instance class [mscorlib]System.Collections.Generic.IEnumerator`1<class [mscorlib]System.Tuple`2<int32,int32>> 
            GetFreshEnumerator() cil managed
    {
      .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
      .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
      // Code size       8 (0x8)
      .maxstack  8
      IL_0000:  ldc.i4.0
      IL_0001:  ldnull
      IL_0002:  newobj     instance void CodeGenRenamings01/seq1@9::.ctor(int32,
                                                                          class [mscorlib]System.Tuple`2<int32,int32>)
      IL_0007:  ret
    } // end of method seq1@9::GetFreshEnumerator

  } // end of class seq1@9

  .method public specialname static class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32> 
          get_alist() cil managed
  {
    // Code size       6 (0x6)
    .maxstack  8
    IL_0000:  ldsfld     class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32> '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01::alist@5
    IL_0005:  ret
  } // end of method CodeGenRenamings01::get_alist

  .method public specialname static int32[] 
          get_array() cil managed
  {
    // Code size       6 (0x6)
    .maxstack  8
    IL_0000:  ldsfld     int32[] '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01::array@6
    IL_0005:  ret
  } // end of method CodeGenRenamings01::get_array

  .method public specialname static class [mscorlib]System.Collections.Generic.IEnumerable`1<int32> 
          get_aseq() cil managed
  {
    // Code size       6 (0x6)
    .maxstack  8
    IL_0000:  ldsfld     class [mscorlib]System.Collections.Generic.IEnumerable`1<int32> '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01::aseq@7
    IL_0005:  ret
  } // end of method CodeGenRenamings01::get_aseq

  .method public specialname static class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<class [mscorlib]System.Tuple`2<int32,int32>> 
          get_list1() cil managed
  {
    // Code size       6 (0x6)
    .maxstack  8
    IL_0000:  ldsfld     class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<class [mscorlib]System.Tuple`2<int32,int32>> '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01::list1@8
    IL_0005:  ret
  } // end of method CodeGenRenamings01::get_list1

  .method public specialname static class [mscorlib]System.Collections.Generic.IEnumerable`1<class [mscorlib]System.Tuple`2<int32,int32>> 
          get_seq1() cil managed
  {
    // Code size       6 (0x6)
    .maxstack  8
    IL_0000:  ldsfld     class [mscorlib]System.Collections.Generic.IEnumerable`1<class [mscorlib]System.Tuple`2<int32,int32>> '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01::seq1@9
    IL_0005:  ret
  } // end of method CodeGenRenamings01::get_seq1

  .method public specialname static class [mscorlib]System.Tuple`2<int32,int32>[] 
          get_array1() cil managed
  {
    // Code size       6 (0x6)
    .maxstack  8
    IL_0000:  ldsfld     class [mscorlib]System.Tuple`2<int32,int32>[] '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01::array1@10
    IL_0005:  ret
  } // end of method CodeGenRenamings01::get_array1

  .method public specialname static int32[0...,0...] 
          get_a3() cil managed
  {
    // Code size       6 (0x6)
    .maxstack  8
    IL_0000:  ldsfld     int32[0...,0...] '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01::a3@11
    IL_0005:  ret
  } // end of method CodeGenRenamings01::get_a3

  .method public specialname static int32[0...,0...,0...] 
          get_array3D() cil managed
  {
    // Code size       6 (0x6)
    .maxstack  8
    IL_0000:  ldsfld     int32[0...,0...,0...] '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01::array3D@12
    IL_0005:  ret
  } // end of method CodeGenRenamings01::get_array3D

  .method public specialname static int32[0...,0...,0...,0...] 
          get_array4D() cil managed
  {
    // Code size       6 (0x6)
    .maxstack  8
    IL_0000:  ldsfld     int32[0...,0...,0...,0...] '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01::array4D@13
    IL_0005:  ret
  } // end of method CodeGenRenamings01::get_array4D

  .method public specialname static int32[] 
          get_a1() cil managed
  {
    // Code size       6 (0x6)
    .maxstack  8
    IL_0000:  ldsfld     int32[] '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01::a1@25
    IL_0005:  ret
  } // end of method CodeGenRenamings01::get_a1

  .method public specialname static int32[] 
          get_a2() cil managed
  {
    // Code size       6 (0x6)
    .maxstack  8
    IL_0000:  ldsfld     int32[] '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01::a2@26
    IL_0005:  ret
  } // end of method CodeGenRenamings01::get_a2

  .property class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32>
          alist()
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 09 00 00 00 00 00 ) 
    .get class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32> CodeGenRenamings01::get_alist()
  } // end of property CodeGenRenamings01::alist
  .property int32[] 'array'()
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 09 00 00 00 00 00 ) 
    .get int32[] CodeGenRenamings01::get_array()
  } // end of property CodeGenRenamings01::'array'
  .property class [mscorlib]System.Collections.Generic.IEnumerable`1<int32>
          aseq()
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 09 00 00 00 00 00 ) 
    .get class [mscorlib]System.Collections.Generic.IEnumerable`1<int32> CodeGenRenamings01::get_aseq()
  } // end of property CodeGenRenamings01::aseq
  .property class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<class [mscorlib]System.Tuple`2<int32,int32>>
          list1()
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 09 00 00 00 00 00 ) 
    .get class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<class [mscorlib]System.Tuple`2<int32,int32>> CodeGenRenamings01::get_list1()
  } // end of property CodeGenRenamings01::list1
  .property class [mscorlib]System.Collections.Generic.IEnumerable`1<class [mscorlib]System.Tuple`2<int32,int32>>
          seq1()
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 09 00 00 00 00 00 ) 
    .get class [mscorlib]System.Collections.Generic.IEnumerable`1<class [mscorlib]System.Tuple`2<int32,int32>> CodeGenRenamings01::get_seq1()
  } // end of property CodeGenRenamings01::seq1
  .property class [mscorlib]System.Tuple`2<int32,int32>[]
          array1()
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 09 00 00 00 00 00 ) 
    .get class [mscorlib]System.Tuple`2<int32,int32>[] CodeGenRenamings01::get_array1()
  } // end of property CodeGenRenamings01::array1
  .property int32[0...,0...] a3()
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 09 00 00 00 00 00 ) 
    .get int32[0...,0...] CodeGenRenamings01::get_a3()
  } // end of property CodeGenRenamings01::a3
  .property int32[0...,0...,0...] array3D()
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 09 00 00 00 00 00 ) 
    .get int32[0...,0...,0...] CodeGenRenamings01::get_array3D()
  } // end of property CodeGenRenamings01::array3D
  .property int32[0...,0...,0...,0...] array4D()
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 09 00 00 00 00 00 ) 
    .get int32[0...,0...,0...,0...] CodeGenRenamings01::get_array4D()
  } // end of property CodeGenRenamings01::array4D
  .property int32[] a1()
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 09 00 00 00 00 00 ) 
    .get int32[] CodeGenRenamings01::get_a1()
  } // end of property CodeGenRenamings01::a1
  .property int32[] a2()
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 09 00 00 00 00 00 ) 
    .get int32[] CodeGenRenamings01::get_a2()
  } // end of property CodeGenRenamings01::a2
} // end of class CodeGenRenamings01

.class private abstract auto ansi sealed '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01
       extends [mscorlib]System.Object
{
  .field static assembly class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32> alist@5
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .field static assembly int32[] array@6
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .field static assembly class [mscorlib]System.Collections.Generic.IEnumerable`1<int32> aseq@7
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .field static assembly class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<class [mscorlib]System.Tuple`2<int32,int32>> list1@8
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .field static assembly class [mscorlib]System.Collections.Generic.IEnumerable`1<class [mscorlib]System.Tuple`2<int32,int32>> seq1@9
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .field static assembly class [mscorlib]System.Tuple`2<int32,int32>[] array1@10
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .field static assembly int32[0...,0...] a3@11
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .field static assembly int32[0...,0...,0...] array3D@12
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .field static assembly int32[0...,0...,0...,0...] array4D@13
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .field static assembly int32[] a1@25
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .field static assembly int32[] a2@26
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .field static assembly int32 init@
  .custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
  .custom instance void [mscorlib]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
  .method public static void  main@() cil managed
  {
    .entrypoint
    // Code size       567 (0x237)
    .maxstack  12
    .locals init ([0] class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32> alist,
             [1] int32[] 'array',
             [2] class [mscorlib]System.Collections.Generic.IEnumerable`1<int32> aseq,
             [3] class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<class [mscorlib]System.Tuple`2<int32,int32>> list1,
             [4] class [mscorlib]System.Collections.Generic.IEnumerable`1<class [mscorlib]System.Tuple`2<int32,int32>> seq1,
             [5] class [mscorlib]System.Tuple`2<int32,int32>[] array1,
             [6] int32[0...,0...] a3,
             [7] int32[0...,0...,0...] array3D,
             [8] int32[0...,0...,0...,0...] array4D,
             [9] int32[] a1,
             [10] int32[] a2,
             [11] class [mscorlib]System.Tuple`4<int32,int32,int32,int32> V_11,
             [12] class [mscorlib]System.Tuple`4<int32,int32,int32,int32> V_12,
             [13] class [mscorlib]System.Tuple`3<int32,int32,int32> V_13,
             [14] class [mscorlib]System.Tuple`3<int32,int32,int32> V_14,
             [15] class [mscorlib]System.Tuple`4<int32,int32,int32,int32> V_15,
             [16] class [mscorlib]System.Tuple`4<int32,int32,int32,int32> V_16)
    .line 5,5 : 1,24 ''
    IL_0000:  ldc.i4.1
    IL_0001:  ldc.i4.1
    IL_0002:  ldc.i4.s   10
    IL_0004:  call       class [mscorlib]System.Collections.Generic.IEnumerable`1<int32> [FSharp.Core]Microsoft.FSharp.Core.Operators/OperatorIntrinsics::RangeInt32(int32,
                                                                                                                                                                     int32,
                                                                                                                                                                     int32)
    IL_0009:  call       class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0> [FSharp.Core]Microsoft.FSharp.Core.Operators::CreateSequence<int32>(class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0>)
    IL_000e:  call       class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!!0> [FSharp.Core]Microsoft.FSharp.Collections.SeqModule::ToList<int32>(class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0>)
    IL_0013:  dup
    IL_0014:  stsfld     class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32> '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01::alist@5
    IL_0019:  stloc.0
    .line 6,6 : 1,26 ''
    IL_001a:  ldc.i4.3
    IL_001b:  newarr     [mscorlib]System.Int32
    IL_0020:  dup
    IL_0021:  ldc.i4.0
    IL_0022:  ldc.i4.1
    IL_0023:  stelem     [mscorlib]System.Int32
    IL_0028:  dup
    IL_0029:  ldc.i4.1
    IL_002a:  ldc.i4.2
    IL_002b:  stelem     [mscorlib]System.Int32
    IL_0030:  dup
    IL_0031:  ldc.i4.2
    IL_0032:  ldc.i4.3
    IL_0033:  stelem     [mscorlib]System.Int32
    IL_0038:  dup
    IL_0039:  stsfld     int32[] '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01::array@6
    IL_003e:  stloc.1
    .line 7,7 : 1,27 ''
    IL_003f:  ldc.i4.1
    IL_0040:  ldc.i4.1
    IL_0041:  ldc.i4.s   10
    IL_0043:  call       class [mscorlib]System.Collections.Generic.IEnumerable`1<int32> [FSharp.Core]Microsoft.FSharp.Core.Operators/OperatorIntrinsics::RangeInt32(int32,
                                                                                                                                                                     int32,
                                                                                                                                                                     int32)
    IL_0048:  call       class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0> [FSharp.Core]Microsoft.FSharp.Core.Operators::CreateSequence<int32>(class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0>)
    IL_004d:  dup
    IL_004e:  stsfld     class [mscorlib]System.Collections.Generic.IEnumerable`1<int32> '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01::aseq@7
    IL_0053:  stloc.2
    .line 8,8 : 1,27 ''
    IL_0054:  ldc.i4.1
    IL_0055:  ldc.i4.1
    IL_0056:  newobj     instance void class [mscorlib]System.Tuple`2<int32,int32>::.ctor(!0,
                                                                                          !1)
    IL_005b:  ldc.i4.2
    IL_005c:  ldc.i4.2
    IL_005d:  newobj     instance void class [mscorlib]System.Tuple`2<int32,int32>::.ctor(!0,
                                                                                          !1)
    IL_0062:  call       class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<class [mscorlib]System.Tuple`2<int32,int32>>::get_Empty()
    IL_0067:  call       class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<class [mscorlib]System.Tuple`2<int32,int32>>::Cons(!0,
                                                                                                                                                                                                          class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0>)
    IL_006c:  call       class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0> class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<class [mscorlib]System.Tuple`2<int32,int32>>::Cons(!0,
                                                                                                                                                                                                          class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!0>)
    IL_0071:  dup
    IL_0072:  stsfld     class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<class [mscorlib]System.Tuple`2<int32,int32>> '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01::list1@8
    IL_0077:  stloc.3
    IL_0078:  ldc.i4.0
    IL_0079:  ldnull
    IL_007a:  newobj     instance void CodeGenRenamings01/seq1@9::.ctor(int32,
                                                                        class [mscorlib]System.Tuple`2<int32,int32>)
    IL_007f:  dup
    IL_0080:  stsfld     class [mscorlib]System.Collections.Generic.IEnumerable`1<class [mscorlib]System.Tuple`2<int32,int32>> '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01::seq1@9
    IL_0085:  stloc.s    seq1
    .line 10,10 : 1,34 ''
    IL_0087:  ldc.i4.2
    IL_0088:  newarr     class [mscorlib]System.Tuple`2<int32,int32>
    IL_008d:  dup
    IL_008e:  ldc.i4.0
    IL_008f:  ldc.i4.1
    IL_0090:  ldc.i4.1
    IL_0091:  newobj     instance void class [mscorlib]System.Tuple`2<int32,int32>::.ctor(!0,
                                                                                          !1)
    IL_0096:  stelem     class [mscorlib]System.Tuple`2<int32,int32>
    IL_009b:  dup
    IL_009c:  ldc.i4.1
    IL_009d:  ldc.i4.2
    IL_009e:  ldc.i4.2
    IL_009f:  newobj     instance void class [mscorlib]System.Tuple`2<int32,int32>::.ctor(!0,
                                                                                          !1)
    IL_00a4:  stelem     class [mscorlib]System.Tuple`2<int32,int32>
    IL_00a9:  dup
    IL_00aa:  stsfld     class [mscorlib]System.Tuple`2<int32,int32>[] '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01::array1@10
    IL_00af:  stloc.s    array1
    .line 11,11 : 1,30 ''
    IL_00b1:  ldc.i4.2
    IL_00b2:  ldc.i4.2
    IL_00b3:  ldc.i4.0
    IL_00b4:  call       !!0[0...,0...] [FSharp.Core]Microsoft.FSharp.Collections.Array2DModule::Create<int32>(int32,
                                                                                                               int32,
                                                                                                               !!0)
    IL_00b9:  dup
    IL_00ba:  stsfld     int32[0...,0...] '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01::a3@11
    IL_00bf:  stloc.s    a3
    .line 12,12 : 1,37 ''
    IL_00c1:  ldc.i4.3
    IL_00c2:  ldc.i4.3
    IL_00c3:  ldc.i4.3
    IL_00c4:  ldc.i4.0
    IL_00c5:  call       !!0[0...,0...,0...] [FSharp.Core]Microsoft.FSharp.Collections.Array3DModule::Create<int32>(int32,
                                                                                                                    int32,
                                                                                                                    int32,
                                                                                                                    !!0)
    IL_00ca:  dup
    IL_00cb:  stsfld     int32[0...,0...,0...] '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01::array3D@12
    IL_00d0:  stloc.s    array3D
    .line 13,13 : 1,39 ''
    IL_00d2:  ldc.i4.4
    IL_00d3:  ldc.i4.4
    IL_00d4:  ldc.i4.4
    IL_00d5:  ldc.i4.4
    IL_00d6:  ldc.i4.0
    IL_00d7:  call       !!0[0...,0...,0...,0...] [FSharp.Core]Microsoft.FSharp.Collections.Array4DModule::Create<int32>(int32,
                                                                                                                         int32,
                                                                                                                         int32,
                                                                                                                         int32,
                                                                                                                         !!0)
    IL_00dc:  dup
    IL_00dd:  stsfld     int32[0...,0...,0...,0...] '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01::array4D@13
    IL_00e2:  stloc.s    array4D
    .line 16,16 : 9,27 ''
    IL_00e4:  call       int32[] CodeGenRenamings01::get_array()
    IL_00e9:  call       class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!!0> [FSharp.Core]Microsoft.FSharp.Collections.ListModule::OfArray<int32>(!!0[])
    IL_00ee:  pop
    .line 17,17 : 9,24 ''
    IL_00ef:  call       class [mscorlib]System.Collections.Generic.IEnumerable`1<int32> CodeGenRenamings01::get_aseq()
    IL_00f4:  call       class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!!0> [FSharp.Core]Microsoft.FSharp.Collections.ListModule::OfSeq<int32>(class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0>)
    IL_00f9:  pop
    .line 20,20 : 9,27 ''
    IL_00fa:  call       class [mscorlib]System.Tuple`2<int32,int32>[] CodeGenRenamings01::get_array1()
    IL_00ff:  call       class [FSharp.Core]Microsoft.FSharp.Collections.FSharpMap`2<!!0,!!1> [FSharp.Core]Microsoft.FSharp.Collections.MapModule::OfArray<int32,int32>(class [mscorlib]System.Tuple`2<!!0,!!1>[])
    IL_0104:  pop
    .line 21,21 : 9,25 ''
    IL_0105:  call       class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<class [mscorlib]System.Tuple`2<int32,int32>> CodeGenRenamings01::get_list1()
    IL_010a:  call       class [FSharp.Core]Microsoft.FSharp.Collections.FSharpMap`2<!!0,!!1> [FSharp.Core]Microsoft.FSharp.Collections.MapModule::OfList<int32,int32>(class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<class [mscorlib]System.Tuple`2<!!0,!!1>>)
    IL_010f:  pop
    .line 22,22 : 9,23 ''
    IL_0110:  call       class [mscorlib]System.Collections.Generic.IEnumerable`1<class [mscorlib]System.Tuple`2<int32,int32>> CodeGenRenamings01::get_seq1()
    IL_0115:  call       class [FSharp.Core]Microsoft.FSharp.Collections.FSharpMap`2<!!0,!!1> [FSharp.Core]Microsoft.FSharp.Collections.MapModule::OfSeq<int32,int32>(class [mscorlib]System.Collections.Generic.IEnumerable`1<class [mscorlib]System.Tuple`2<!!0,!!1>>)
    IL_011a:  pop
    .line 25,25 : 1,28 ''
    IL_011b:  call       class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<int32> CodeGenRenamings01::get_alist()
    IL_0120:  call       !!0[] [FSharp.Core]Microsoft.FSharp.Collections.ArrayModule::OfList<int32>(class [FSharp.Core]Microsoft.FSharp.Collections.FSharpList`1<!!0>)
    IL_0125:  dup
    IL_0126:  stsfld     int32[] '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01::a1@25
    IL_012b:  stloc.s    a1
    .line 26,26 : 1,27 ''
    IL_012d:  call       class [mscorlib]System.Collections.Generic.IEnumerable`1<int32> CodeGenRenamings01::get_aseq()
    IL_0132:  call       !!0[] [FSharp.Core]Microsoft.FSharp.Collections.ArrayModule::OfSeq<int32>(class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0>)
    IL_0137:  dup
    IL_0138:  stsfld     int32[] '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01::a2@26
    IL_013d:  stloc.s    a2
    .line 27,27 : 1,33 ''
    IL_013f:  call       int32[] CodeGenRenamings01::get_a2()
    IL_0144:  ldc.i4.0
    IL_0145:  call       int32[] CodeGenRenamings01::get_a1()
    IL_014a:  ldc.i4.0
    IL_014b:  call       !!0 [FSharp.Core]Microsoft.FSharp.Collections.ArrayModule::Get<int32>(!!0[],
                                                                                               int32)
    IL_0150:  call       void [FSharp.Core]Microsoft.FSharp.Collections.ArrayModule::Set<int32>(!!0[],
                                                                                                int32,
                                                                                                !!0)
    IL_0155:  nop
    .line 30,30 : 1,87 ''
    IL_0156:  call       int32[0...,0...] CodeGenRenamings01::get_a3()
    IL_015b:  call       int32 [FSharp.Core]Microsoft.FSharp.Collections.Array2DModule::Length1<int32>(!!0[0...,0...])
    IL_0160:  call       int32[0...,0...] CodeGenRenamings01::get_a3()
    IL_0165:  call       int32 [FSharp.Core]Microsoft.FSharp.Collections.Array2DModule::Length2<int32>(!!0[0...,0...])
    IL_016a:  call       int32[0...,0...] CodeGenRenamings01::get_a3()
    IL_016f:  call       int32 [FSharp.Core]Microsoft.FSharp.Collections.Array2DModule::Base1<int32>(!!0[0...,0...])
    IL_0174:  call       int32[0...,0...] CodeGenRenamings01::get_a3()
    IL_0179:  call       int32 [FSharp.Core]Microsoft.FSharp.Collections.Array2DModule::Base2<int32>(!!0[0...,0...])
    IL_017e:  newobj     instance void class [mscorlib]System.Tuple`4<int32,int32,int32,int32>::.ctor(!0,
                                                                                                      !1,
                                                                                                      !2,
                                                                                                      !3)
    IL_0183:  stloc.s    V_11
    IL_0185:  ldloc.s    V_11
    IL_0187:  stloc.s    V_12
    .line 31,31 : 1,41 ''
    IL_0189:  call       int32[0...,0...] CodeGenRenamings01::get_a3()
    IL_018e:  ldc.i4.0
    IL_018f:  ldc.i4.0
    IL_0190:  call       int32[0...,0...] CodeGenRenamings01::get_a3()
    IL_0195:  ldc.i4.0
    IL_0196:  ldc.i4.0
    IL_0197:  call       !!0 [FSharp.Core]Microsoft.FSharp.Collections.Array2DModule::Get<int32>(!!0[0...,0...],
                                                                                                 int32,
                                                                                                 int32)
    IL_019c:  call       void [FSharp.Core]Microsoft.FSharp.Collections.Array2DModule::Set<int32>(!!0[0...,0...],
                                                                                                  int32,
                                                                                                  int32,
                                                                                                  !!0)
    IL_01a1:  nop
    .line 34,34 : 1,86 ''
    IL_01a2:  call       int32[0...,0...,0...] CodeGenRenamings01::get_array3D()
    IL_01a7:  call       int32 [FSharp.Core]Microsoft.FSharp.Collections.Array3DModule::Length1<int32>(!!0[0...,0...,0...])
    IL_01ac:  call       int32[0...,0...,0...] CodeGenRenamings01::get_array3D()
    IL_01b1:  call       int32 [FSharp.Core]Microsoft.FSharp.Collections.Array3DModule::Length2<int32>(!!0[0...,0...,0...])
    IL_01b6:  call       int32[0...,0...,0...] CodeGenRenamings01::get_array3D()
    IL_01bb:  call       int32 [FSharp.Core]Microsoft.FSharp.Collections.Array3DModule::Length3<int32>(!!0[0...,0...,0...])
    IL_01c0:  newobj     instance void class [mscorlib]System.Tuple`3<int32,int32,int32>::.ctor(!0,
                                                                                                !1,
                                                                                                !2)
    IL_01c5:  stloc.s    V_13
    IL_01c7:  ldloc.s    V_13
    IL_01c9:  stloc.s    V_14
    .line 35,35 : 1,55 ''
    IL_01cb:  call       int32[0...,0...,0...] CodeGenRenamings01::get_array3D()
    IL_01d0:  ldc.i4.0
    IL_01d1:  ldc.i4.0
    IL_01d2:  ldc.i4.0
    IL_01d3:  call       int32[0...,0...,0...] CodeGenRenamings01::get_array3D()
    IL_01d8:  ldc.i4.0
    IL_01d9:  ldc.i4.0
    IL_01da:  ldc.i4.0
    IL_01db:  call       !!0 [FSharp.Core]Microsoft.FSharp.Collections.Array3DModule::Get<int32>(!!0[0...,0...,0...],
                                                                                                 int32,
                                                                                                 int32,
                                                                                                 int32)
    IL_01e0:  call       void [FSharp.Core]Microsoft.FSharp.Collections.Array3DModule::Set<int32>(!!0[0...,0...,0...],
                                                                                                  int32,
                                                                                                  int32,
                                                                                                  int32,
                                                                                                  !!0)
    IL_01e5:  nop
    .line 38,38 : 1,111 ''
    IL_01e6:  call       int32[0...,0...,0...,0...] CodeGenRenamings01::get_array4D()
    IL_01eb:  call       int32 [FSharp.Core]Microsoft.FSharp.Collections.Array4DModule::Length1<int32>(!!0[0...,0...,0...,0...])
    IL_01f0:  call       int32[0...,0...,0...,0...] CodeGenRenamings01::get_array4D()
    IL_01f5:  call       int32 [FSharp.Core]Microsoft.FSharp.Collections.Array4DModule::Length2<int32>(!!0[0...,0...,0...,0...])
    IL_01fa:  call       int32[0...,0...,0...,0...] CodeGenRenamings01::get_array4D()
    IL_01ff:  call       int32 [FSharp.Core]Microsoft.FSharp.Collections.Array4DModule::Length3<int32>(!!0[0...,0...,0...,0...])
    IL_0204:  call       int32[0...,0...,0...,0...] CodeGenRenamings01::get_array4D()
    IL_0209:  call       int32 [FSharp.Core]Microsoft.FSharp.Collections.Array4DModule::Length4<int32>(!!0[0...,0...,0...,0...])
    IL_020e:  newobj     instance void class [mscorlib]System.Tuple`4<int32,int32,int32,int32>::.ctor(!0,
                                                                                                      !1,
                                                                                                      !2,
                                                                                                      !3)
    IL_0213:  stloc.s    V_15
    IL_0215:  ldloc.s    V_15
    IL_0217:  stloc.s    V_16
    .line 39,39 : 1,59 ''
    IL_0219:  call       int32[0...,0...,0...,0...] CodeGenRenamings01::get_array4D()
    IL_021e:  ldc.i4.0
    IL_021f:  ldc.i4.0
    IL_0220:  ldc.i4.0
    IL_0221:  ldc.i4.0
    IL_0222:  call       int32[0...,0...,0...,0...] CodeGenRenamings01::get_array4D()
    IL_0227:  ldc.i4.0
    IL_0228:  ldc.i4.0
    IL_0229:  ldc.i4.0
    IL_022a:  ldc.i4.0
    IL_022b:  call       !!0 [FSharp.Core]Microsoft.FSharp.Collections.Array4DModule::Get<int32>(!!0[0...,0...,0...,0...],
                                                                                                 int32,
                                                                                                 int32,
                                                                                                 int32,
                                                                                                 int32)
    IL_0230:  call       void [FSharp.Core]Microsoft.FSharp.Collections.Array4DModule::Set<int32>(!!0[0...,0...,0...,0...],
                                                                                                  int32,
                                                                                                  int32,
                                                                                                  int32,
                                                                                                  int32,
                                                                                                  !!0)
    IL_0235:  nop
    IL_0236:  ret
  } // end of method $CodeGenRenamings01::main@

} // end of class '<StartupCode$CodeGenRenamings01>'.$CodeGenRenamings01


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
