// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.UnitTests.CodeGen.EmittedIL

open FSharp.Test
open NUnit.Framework

[<TestFixture>]
module DelegateAndFuncOptimizations =

    [<Test>]
    // See https://github.com/fsharp/fslang-design/blob/master/tooling/FST-1034-lambda-optimizations.md
    let ``Reduce function via InlineIfLambda``() =
        CompilerAssert.CompileLibraryAndVerifyIL
            """
module InlineIfLambda01

open System

type C =
    static member inline M([<InlineIfLambda>] f: int -> int) = f 3 + f 4
    static member M2() = C.M(fun x -> x + 1)
    static member M3() = 
         C.M(fun x -> 
             printfn("code")
             printfn("code")
             printfn("code")
             printfn("code")
             printfn("code")
             printfn("code")
             x + 1)
            """
            (fun verifier -> verifier.VerifyIL [
            // Check M is inlined and optimized when small
            """
.method public static int32  M2() cil managed
{
      
    .maxstack  8
    IL_0000:  ldc.i4.s   9
    IL_0002:  ret
} 
            """
            // Check M is inlined and optimized when largs
            """
.method public static int32  M3() cil managed
{
  
  .maxstack  5
  .locals init (class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit> V_0)
  IL_0000:  ldstr      "code"
  IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_000a:  stloc.0
  IL_000b:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_0010:  ldloc.0
  IL_0011:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_0016:  pop
  IL_0017:  ldstr      "code"
  IL_001c:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_0021:  stloc.0
  IL_0022:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_0027:  ldloc.0
  IL_0028:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_002d:  pop
  IL_002e:  ldstr      "code"
  IL_0033:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_0038:  stloc.0
  IL_0039:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_003e:  ldloc.0
  IL_003f:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_0044:  pop
  IL_0045:  ldstr      "code"
  IL_004a:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_004f:  stloc.0
  IL_0050:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_0055:  ldloc.0
  IL_0056:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_005b:  pop
  IL_005c:  ldstr      "code"
  IL_0061:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_0066:  stloc.0
  IL_0067:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_006c:  ldloc.0
  IL_006d:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_0072:  pop
  IL_0073:  ldstr      "code"
  IL_0078:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_007d:  stloc.0
  IL_007e:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_0083:  ldloc.0
  IL_0084:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_0089:  pop
  IL_008a:  ldc.i4.4
  IL_008b:  ldstr      "code"
  IL_0090:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_0095:  stloc.0
  IL_0096:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_009b:  ldloc.0
  IL_009c:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_00a1:  pop
  IL_00a2:  ldstr      "code"
  IL_00a7:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_00ac:  stloc.0
  IL_00ad:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_00b2:  ldloc.0
  IL_00b3:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_00b8:  pop
  IL_00b9:  ldstr      "code"
  IL_00be:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_00c3:  stloc.0
  IL_00c4:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_00c9:  ldloc.0
  IL_00ca:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_00cf:  pop
  IL_00d0:  ldstr      "code"
  IL_00d5:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_00da:  stloc.0
  IL_00db:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_00e0:  ldloc.0
  IL_00e1:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_00e6:  pop
  IL_00e7:  ldstr      "code"
  IL_00ec:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_00f1:  stloc.0
  IL_00f2:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_00f7:  ldloc.0
  IL_00f8:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_00fd:  pop
  IL_00fe:  ldstr      "code"
  IL_0103:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_0108:  stloc.0
  IL_0109:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_010e:  ldloc.0
  IL_010f:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_0114:  pop
  IL_0115:  ldc.i4.5
  IL_0116:  add
  IL_0117:  ret
} 
            """
            ])

    [<Test>]
    // See https://github.com/fsharp/fslang-design/blob/master/tooling/FST-1034-lambda-optimizations.md
    let ``Reduce delegate invoke via InlineIfLambda``() =
        CompilerAssert.CompileLibraryAndVerifyIL
            """
module InlineIfLambdaOnDelegate

open System
type D = delegate of int -> int
type C =
    static member inline M([<InlineIfLambda>] f: D) = f.Invoke 3 + f.Invoke 4
    
    // Check M is inlined and optimized when small
    static member M2() = C.M(D(fun x -> x + 1))
    
    // Check M is inlined and optimized when large
    static member M3() = 
         C.M(D(fun x -> 
             printfn("code")
             printfn("code")
             printfn("code")
             printfn("code")
             printfn("code")
             printfn("code")
             x + 1))
            """
            (fun verifier -> verifier.VerifyIL [
            // Check M is inlined and optimized when small
            """
.method public static int32  M2() cil managed
{
      
    .maxstack  8
    IL_0000:  ldc.i4.s   9
    IL_0002:  ret
} 
            """
            // Check M is inlined and optimized when largs
            """
.method public static int32  M3() cil managed
{
  
  .maxstack  5
  .locals init (class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit> V_0)
  IL_0000:  ldstr      "code"
  IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_000a:  stloc.0
  IL_000b:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_0010:  ldloc.0
  IL_0011:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_0016:  pop
  IL_0017:  ldstr      "code"
  IL_001c:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_0021:  stloc.0
  IL_0022:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_0027:  ldloc.0
  IL_0028:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_002d:  pop
  IL_002e:  ldstr      "code"
  IL_0033:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_0038:  stloc.0
  IL_0039:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_003e:  ldloc.0
  IL_003f:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_0044:  pop
  IL_0045:  ldstr      "code"
  IL_004a:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_004f:  stloc.0
  IL_0050:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_0055:  ldloc.0
  IL_0056:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_005b:  pop
  IL_005c:  ldstr      "code"
  IL_0061:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_0066:  stloc.0
  IL_0067:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_006c:  ldloc.0
  IL_006d:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_0072:  pop
  IL_0073:  ldstr      "code"
  IL_0078:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_007d:  stloc.0
  IL_007e:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_0083:  ldloc.0
  IL_0084:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_0089:  pop
  IL_008a:  ldc.i4.4
  IL_008b:  ldstr      "code"
  IL_0090:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_0095:  stloc.0
  IL_0096:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_009b:  ldloc.0
  IL_009c:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_00a1:  pop
  IL_00a2:  ldstr      "code"
  IL_00a7:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_00ac:  stloc.0
  IL_00ad:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_00b2:  ldloc.0
  IL_00b3:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_00b8:  pop
  IL_00b9:  ldstr      "code"
  IL_00be:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_00c3:  stloc.0
  IL_00c4:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_00c9:  ldloc.0
  IL_00ca:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_00cf:  pop
  IL_00d0:  ldstr      "code"
  IL_00d5:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_00da:  stloc.0
  IL_00db:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_00e0:  ldloc.0
  IL_00e1:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_00e6:  pop
  IL_00e7:  ldstr      "code"
  IL_00ec:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_00f1:  stloc.0
  IL_00f2:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_00f7:  ldloc.0
  IL_00f8:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_00fd:  pop
  IL_00fe:  ldstr      "code"
  IL_0103:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_0108:  stloc.0
  IL_0109:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_010e:  ldloc.0
  IL_010f:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_0114:  pop
  IL_0115:  ldc.i4.5
  IL_0116:  add
  IL_0117:  ret
} 
            """
            ])

    [<Test>]
    // See https://github.com/fsharp/fslang-design/blob/master/tooling/FST-1034-lambda-optimizations.md
    let ``Reduce computed function invoke``() =
        CompilerAssert.CompileLibraryAndVerifyIL
            """
module ReduceComputedFunction

open System

let ApplyComputedFunction(c: int) =
    (let x = c+c in printfn("hello"); (fun y -> x + y)) 3
            """
            (fun verifier -> verifier.VerifyIL [
            """
.method public static int32  ApplyComputedFunction(int32 c) cil managed
{
  
  .maxstack  4
  .locals init (int32 V_0,
           class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit> V_1)
  IL_0000:  ldarg.0
  IL_0001:  ldarg.0
  IL_0002:  add
  IL_0003:  stloc.0
  IL_0004:  ldstr      "hello"
  IL_0009:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_000e:  stloc.1
  IL_000f:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_0014:  ldloc.1
  IL_0015:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_001a:  pop
  IL_001b:  ldloc.0
  IL_001c:  ldc.i4.3
  IL_001d:  add
  IL_001e:  ret
} 
            """
            ])

    [<Test>]
    // See https://github.com/fsharp/fslang-design/blob/master/tooling/FST-1034-lambda-optimizations.md
    let ``Reduce Computed Delegate``() =
        CompilerAssert.CompileLibraryAndVerifyIL
            """
module ReduceComputedDelegate

open System
type D = delegate of int -> int
let ApplyComputedDelegate(c: int) =
    (let x = c+c in printfn("hello"); D(fun y -> x + y)).Invoke 3
            """
            (fun verifier -> verifier.VerifyIL [
            """
.method public static int32  ApplyComputedDelegate(int32 c) cil managed
{
  
  .maxstack  4
  .locals init (int32 V_0,
           class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit> V_1)
  IL_0000:  ldarg.0
  IL_0001:  ldarg.0
  IL_0002:  add
  IL_0003:  stloc.0
  IL_0004:  ldstr      "hello"
  IL_0009:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_000e:  stloc.1
  IL_000f:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_0014:  ldloc.1
  IL_0015:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_001a:  pop
  IL_001b:  ldloc.0
  IL_001c:  ldc.i4.3
  IL_001d:  add
  IL_001e:  ret
} 
            """
            ])

    [<Test>]
    // See https://github.com/fsharp/fslang-design/blob/master/tooling/FST-1034-lambda-optimizations.md
    let ``Reduce Computed Function with irreducible match``() =
        CompilerAssert.CompileLibraryAndVerifyIL
            """
module ReduceComputedFunction

open System

let ApplyComputedFunction(c: int) =
    (let x = c+c in printfn("hello"); match x with 1 -> (fun y -> x + y) | _ -> (fun y -> x - y)) 3
            """
            (fun verifier -> verifier.VerifyIL [
            """
.method public static int32  ApplyComputedFunction(int32 c) cil managed
{
  
  .maxstack  4
  .locals init (int32 V_0,
           class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit> V_1)
  IL_0000:  ldarg.0
  IL_0001:  ldarg.0
  IL_0002:  add
  IL_0003:  stloc.0
  IL_0004:  ldstr      "hello"
  IL_0009:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_000e:  stloc.1
  IL_000f:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_0014:  ldloc.1
  IL_0015:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_001a:  pop
  IL_001b:  ldloc.0
  IL_001c:  ldc.i4.1
  IL_001d:  sub
  IL_001e:  switch     ( 
                        IL_0029)
  IL_0027:  br.s       IL_002d
    
  IL_0029:  ldloc.0
  IL_002a:  ldc.i4.3
  IL_002b:  add
  IL_002c:  ret
    
  IL_002d:  ldloc.0
  IL_002e:  ldc.i4.3
  IL_002f:  sub
  IL_0030:  ret
} 
            """
            ])

    
    [<Test>]
    // See https://github.com/fsharp/fslang-design/blob/master/tooling/FST-1034-lambda-optimizations.md
    let ``Immediately apply computed function in sequential``() =
        CompilerAssert.CompileLibraryAndVerifyIL
            """
module ImmediatelyApplyComputedFunction

open System

let ApplyComputedFunction(c: int) =
    let part1 = match c with 1 -> (fun y -> printfn("a")) | _ -> (fun y -> printfn("b"))
    part1 3
    printfn("done!")
            """
            (fun verifier -> verifier.VerifyIL [
            """
.method public static void  ApplyComputedFunction(int32 c) cil managed
{
  
  .maxstack  4
  .locals init (class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit> V_0)
  IL_0000:  ldarg.0
  IL_0001:  ldc.i4.1
  IL_0002:  sub
  IL_0003:  switch     ( 
                        IL_000e)
  IL_000c:  br.s       IL_0027
    
  IL_000e:  ldstr      "a"
  IL_0013:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_0018:  stloc.0
  IL_0019:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_001e:  ldloc.0
  IL_001f:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_0024:  pop
  IL_0025:  br.s       IL_003e
    
  IL_0027:  ldstr      "b"
  IL_002c:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_0031:  stloc.0
  IL_0032:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_0037:  ldloc.0
  IL_0038:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_003d:  pop
  IL_003e:  ldstr      "done!"
  IL_0043:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_0048:  stloc.0
  IL_0049:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_004e:  ldloc.0
  IL_004f:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_0054:  pop
  IL_0055:  ret
} 
            """
            ])

    [<Test>]
    // See https://github.com/fsharp/fslang-design/blob/master/tooling/FST-1034-lambda-optimizations.md
    let ``Reduce Computed Delegate with let rec``() =
        CompilerAssert.CompileLibraryAndVerifyIL
            """
module ReduceComputedDelegate

open System
type D = delegate of int -> int
let ApplyComputedDelegate(c: int) =
    (let rec f x = if x = 0 then x else f (x-1) in printfn("hello"); D(fun y -> f c + y)).Invoke 3
            """
            (fun verifier -> verifier.VerifyIL [
            """
.method public static int32  ApplyComputedDelegate(int32 c) cil managed
{
  
  .maxstack  4
  .locals init (class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit> V_0)
  IL_0000:  ldstr      "hello"
  IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_000a:  stloc.0
  IL_000b:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_0010:  ldloc.0
  IL_0011:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_0016:  pop
  IL_0017:  ldarg.0
  IL_0018:  call       int32 ReduceComputedDelegate::f@7(int32)
  IL_001d:  ldc.i4.3
  IL_001e:  add
  IL_001f:  ret
} 
            """
            ])

