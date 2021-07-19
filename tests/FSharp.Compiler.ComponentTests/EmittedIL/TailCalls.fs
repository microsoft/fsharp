// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.EmittedIL

open Xunit
open FSharp.Test.Compiler

module ``Tail Calls`` =
    // Regression test for DevDiv:72571

    let private compileWithTailCalls opts =
        opts |> ignoreWarnings |> withOptions ["-g"; "--optimize-"; "--tailcalls+"] |> compile

    [<Fact>]
    let ``TailCall 01``() =
        FSharp """
module TailCall01
let foo(x:int, y) = printfn "%d" x
let run() = let x = 0 in foo(x,5)
        """
        |> compileWithTailCalls
        |> shouldSucceed
        |> verifyIL [
            """
  .method public static void  foo<a>(int32 x,
                                     !!a y) cil managed
  {
        
    .maxstack  8
    IL_0000:  ldstr      "%d"
    IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Core.Unit>,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,int32>::.ctor(string)
    IL_000a:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatLine<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Core.Unit>>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
    IL_000f:  ldarg.0
    IL_0010:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::Invoke(!0)
    IL_0015:  pop
    IL_0016:  ret
  } 
            """
            """
  .method public static void  run() cil managed
  {

    .maxstack  4
    .locals init (int32 V_0)
    IL_0000:  ldc.i4.0
    IL_0001:  stloc.0
    IL_0002:  ldloc.0
    IL_0003:  ldc.i4.5
    IL_0004:  tail.
    IL_0006:  call       void TailCall01::foo<int32>(int32,
                                                     !!0)
    IL_000b:  ret
  }
            """]


    [<Fact>]
    let ``TailCall 02``() =
        FSharp """
module TailCall02
let foo(x:int byref) = x
let run() = let mutable x = 0 in foo(&x)
        """
        |> compileWithTailCalls
        |> shouldSucceed
        |> verifyIL [
            """
  .method public static int32  foo(int32& x) cil managed
  {

    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  ldobj      [runtime]System.Int32
    IL_0006:  ret
  }
            """
            """
  .method public static int32  run() cil managed
  {

    .maxstack  3
    .locals init (int32 V_0)
    IL_0000:  ldc.i4.0
    IL_0001:  stloc.0
    IL_0002:  ldloca.s   V_0
    IL_0004:  call       int32 TailCall02::foo(int32&)
    IL_0009:  ret
  }
            """
            ]

    [<Fact>]
    let ``TailCall 03``() =
        FSharp """
module TailCall03
let foo (x:int byref) (y:int byref) z = printfn "%d" (x+y)
let run() = let mutable x = 0 in foo &x &x 5
        """
        |> compileWithTailCalls
        |> shouldSucceed
        |> verifyIL [
            """
  .method public static void  foo<a>(int32& x,
                                     int32& y,
                                     !!a z) cil managed
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationArgumentCountsAttribute::.ctor(int32[]) = ( 01 00 03 00 00 00 01 00 00 00 01 00 00 00 01 00 
                                                                                                                        00 00 00 00 ) 
        
    .maxstack  8
    IL_0000:  ldstr      "%d"
    IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Core.Unit>,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,int32>::.ctor(string)
    IL_000a:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatLine<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Core.Unit>>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
    IL_000f:  ldarg.0
    IL_0010:  ldobj      [runtime]System.Int32
    IL_0015:  ldarg.1
    IL_0016:  ldobj      [runtime]System.Int32
    IL_001b:  add
    IL_001c:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::Invoke(!0)
    IL_0021:  pop
    IL_0022:  ret
  } 
            """
            """
  .method public static void  run() cil managed
  {

    .maxstack  5
    .locals init (int32 V_0)
    IL_0000:  ldc.i4.0
    IL_0001:  stloc.0
    IL_0002:  ldloca.s   V_0
    IL_0004:  ldloca.s   V_0
    IL_0006:  ldc.i4.5
    IL_0007:  call       void TailCall03::foo<int32>(int32&,
                                                     int32&,
                                                     !!0)
    IL_000c:  nop
    IL_000d:  ret
  }
            """
            ]

    [<Fact>]
    let ``TailCall 04``() =
        FSharp """
module TailCall04
let foo(x:int byref, y) = printfn "%d" x
let run() = let mutable x = 0 in foo(&x,5)
        """
        |> compileWithTailCalls
        |> shouldSucceed
        |> verifyIL [
            """
  .method public static void  foo<a>(int32& x,
                                     !!a y) cil managed
  {
    
    .maxstack  8
    IL_0000:  ldstr      "%d"
    IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Core.Unit>,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,int32>::.ctor(string)
    IL_000a:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatLine<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Core.Unit>>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
    IL_000f:  ldarg.0
    IL_0010:  ldobj      [runtime]System.Int32
    IL_0015:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::Invoke(!0)
    IL_001a:  pop
    IL_001b:  ret
  } 
            """
            """
  .method public static void  run() cil managed
  {

    .maxstack  4
    .locals init (int32 V_0)
    IL_0000:  ldc.i4.0
    IL_0001:  stloc.0
    IL_0002:  ldloca.s   V_0
    IL_0004:  ldc.i4.5
    IL_0005:  call       void TailCall04::foo<int32>(int32&,
                                                     !!0)
    IL_000a:  nop
    IL_000b:  ret

            """
            ]

    [<Fact>]
    let ``TailCall 05``() =
        FSharp """
module TailCall05
let foo(x:int byref, y:int byref, z) = printfn "%d" (x+y)
let run() = let mutable x = 0 in foo(&x,&x,5)
        """
        |> compileWithTailCalls
        |> shouldSucceed
        |> verifyIL [
            """
  .method public static void  foo<a>(int32& x,
                                     int32& y,
                                     !!a z) cil managed
  {
        
    .maxstack  8
    IL_0000:  ldstr      "%d"
    IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Core.Unit>,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,int32>::.ctor(string)
    IL_000a:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.ExtraTopLevelOperators::PrintFormatLine<class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Core.Unit>>(class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
    IL_000f:  ldarg.0
    IL_0010:  ldobj      [runtime]System.Int32
    IL_0015:  ldarg.1
    IL_0016:  ldobj      [runtime]System.Int32
    IL_001b:  add
    IL_001c:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<int32,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::Invoke(!0)
    IL_0021:  pop
    IL_0022:  ret
  } 
            """
            """
  .method public static void  run() cil managed
  {

    .maxstack  5
    .locals init (int32 V_0)
    IL_0000:  ldc.i4.0
    IL_0001:  stloc.0
    IL_0002:  ldloca.s   V_0
    IL_0004:  ldloca.s   V_0
    IL_0006:  ldc.i4.5
    IL_0007:  call       void TailCall05::foo<int32>(int32&,
                                                     int32&,
                                                     !!0)
    IL_000c:  nop
    IL_000d:  ret
  }
            """
            ]

