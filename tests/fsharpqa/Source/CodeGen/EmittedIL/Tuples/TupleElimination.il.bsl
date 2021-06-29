
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
.assembly TupleElimination
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 01 00 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.mresource public FSharpSignatureData.TupleElimination
{
  // Offset: 0x00000000 Length: 0x0000022A
}
.mresource public FSharpOptimizationData.TupleElimination
{
  // Offset: 0x00000230 Length: 0x0000007B
}
.module TupleElimination.exe
// MVID: {60CB69C6-DFDD-92DF-A745-0383C669CB60}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x06AF0000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed TupleElimination
       extends [mscorlib]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .method assembly static void  p@5<a>(!!a v) cil managed
  {
    // Code size       30 (0x1e)
    .maxstack  4
    .locals init ([0] class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<!!a,class [FSharp.Core]Microsoft.FSharp.Core.Unit>,class [mscorlib]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit> V_0)
    .language '{AB4F38C9-B6E6-43BA-BE3B-58080B2CCCE3}', '{994B45C4-E6E9-11D2-903F-00C04FA302A1}', '{5A869D0B-6611-11D3-BD2A-0000F80849BD}'
    .line 5,5 : 15,27 'C:\\GitHub\\dsyme\\fsharp\\tests\\fsharpqa\\source\\CodeGen\\EmittedIL\\Tuples\\TupleElimination.fs'
    IL_0000:  ldstr      "%A"
    IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<!!a,class [FSharp.Core]Microsoft.FSharp.Core.Unit>,class [mscorlib]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,!!a>::.ctor(string)
    IL_000a:  stloc.0
    IL_000b:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
    IL_0010:  ldloc.0
    IL_0011:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<!!0,class [FSharp.Core]Microsoft.FSharp.Core.Unit>>(class [mscorlib]System.IO.TextWriter,
                                                                                                                                                                                                                    class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [mscorlib]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
    IL_0016:  ldarg.0
    IL_0017:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<!!a,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::Invoke(!0)
    IL_001c:  pop
    IL_001d:  ret
  } // end of method TupleElimination::p@5

  .method public static int32  main(string[] argv) cil managed
  {
    .entrypoint
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.EntryPointAttribute::.ctor() = ( 01 00 00 00 ) 
    // Code size       79 (0x4f)
    .maxstack  5
    .locals init ([0] class [mscorlib]System.Collections.Generic.Dictionary`2<int32,int32> dic,
             [1] int32 i,
             [2] bool b,
             [3] int64 l,
             [4] bool V_4,
             [5] class [mscorlib]System.Tuple`2<bool,int64> t)
    .line 7,7 : 5,64 ''
    IL_0000:  newobj     instance void class [mscorlib]System.Collections.Generic.Dictionary`2<int32,int32>::.ctor()
    IL_0005:  stloc.0
    .line 9,9 : 31,48 ''
    IL_0006:  ldloc.0
    IL_0007:  ldc.i4.1
    IL_0008:  ldloca.s   i
    IL_000a:  callvirt   instance bool class [mscorlib]System.Collections.Generic.Dictionary`2<int32,int32>::TryGetValue(!0,
                                                                                                                         !1&)
    IL_000f:  stloc.2
    .line 10,10 : 5,6 ''
    IL_0010:  ldloc.2
    IL_0011:  call       void TupleElimination::p@5<bool>(!!0)
    IL_0016:  nop
    .line 11,11 : 5,6 ''
    IL_0017:  ldloc.1
    IL_0018:  call       void TupleElimination::p@5<int32>(!!0)
    IL_001d:  nop
    .line 14,14 : 38,65 ''
    IL_001e:  ldstr      "123"
    IL_0023:  ldloca.s   l
    IL_0025:  call       bool [mscorlib]System.Int64::TryParse(string,
                                                               int64&)
    IL_002a:  stloc.s    V_4
    .line 14,14 : 5,65 ''
    IL_002c:  ldloc.s    V_4
    IL_002e:  ldloc.3
    IL_002f:  newobj     instance void class [mscorlib]System.Tuple`2<bool,int64>::.ctor(!0,
                                                                                         !1)
    IL_0034:  stloc.s    t
    .line 15,15 : 5,6 ''
    IL_0036:  ldloc.s    V_4
    IL_0038:  call       void TupleElimination::p@5<bool>(!!0)
    IL_003d:  nop
    .line 16,16 : 5,6 ''
    IL_003e:  ldloc.3
    IL_003f:  call       void TupleElimination::p@5<int64>(!!0)
    IL_0044:  nop
    .line 21,21 : 5,6 ''
    IL_0045:  ldloc.s    t
    IL_0047:  call       void TupleElimination::p@5<class [mscorlib]System.Tuple`2<bool,int64>>(!!0)
    IL_004c:  nop
    .line 23,23 : 5,6 ''
    IL_004d:  ldc.i4.0
    IL_004e:  ret
  } // end of method TupleElimination::main

} // end of class TupleElimination

.class private abstract auto ansi sealed '<StartupCode$TupleElimination>'.$TupleElimination
       extends [mscorlib]System.Object
{
} // end of class '<StartupCode$TupleElimination>'.$TupleElimination


// =============================================================

// *********** DISASSEMBLY COMPLETE ***********************
