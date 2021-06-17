// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.UnitTests.CodeGen.EmittedIL

open FSharp.Compiler.UnitTests
open FSharp.Test.Utilities
open NUnit.Framework

[<TestFixture>]
module ``Mutation`` =
    // Regression test for FSHARP1.0:1206

    [<Test>]
    let ``Mutation 01``() =
        CompilerAssert.CompileLibraryAndVerifyILWithOptions [|"-g"; "--optimize-"|]
            """
module Mutation01
type Test = struct
              val mutable v: int
              member t.setV v = t.v <- 0
            end
            """
            (fun verifier -> verifier.VerifyIL [
            """
  .class sequential ansi serializable sealed nested public Test
            """
            """
    .field public int32 v
            """
            """
    .method public hidebysig instance void
            setV<a>(!!a v) cil managed
    {

    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  ldc.i4.0
    IL_0002:  stfld      int32 Mutation01/Test::v
    IL_0007:  ret
    }
            """
            ])

    [<Test>]
    let ``Mutation 02``() =
        CompilerAssert.CompileLibraryAndVerifyILWithOptions [|"-g"; "--optimize-"|]
            """
module Mutation02
let x = System.TimeSpan.MinValue
x.ToString()
            """
            (fun verifier -> verifier.VerifyIL [
            """
  .method public specialname static valuetype [mscorlib]System.TimeSpan
          get_x() cil managed
  {

    .maxstack  8
    IL_0000:  ldsfld     valuetype [mscorlib]System.TimeSpan '<StartupCode$assembly>'.$Mutation02::x@3
    IL_0005:  ret
  }
            """
            """
  .property valuetype [mscorlib]System.TimeSpan
          x()
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 09 00 00 00 00 00 )
    .get valuetype [mscorlib]System.TimeSpan Mutation02::get_x()
  }
            """
            """
void  .cctor() cil managed
  {

    .maxstack  4
    .locals init (valuetype [runtime]System.TimeSpan V_0,
             valuetype [runtime]System.TimeSpan V_1)
    IL_0000:  ldsfld     valuetype [runtime]System.TimeSpan [runtime]System.TimeSpan::MinValue
    IL_0005:  dup
    IL_0006:  stsfld     valuetype [runtime]System.TimeSpan '<StartupCode$assembly>'.$Mutation02::x@3
    IL_000b:  stloc.0
    IL_000c:  call       valuetype [runtime]System.TimeSpan Mutation02::get_x()
    IL_0011:  stloc.1
    IL_0012:  ldloca.s   V_1
    IL_0014:  constrained. [runtime]System.TimeSpan
    IL_001a:  callvirt   instance string [runtime]System.Object::ToString()
    IL_001f:  pop
    IL_0020:  ret
  }
            """
            ])

    [<Test>]
    let ``Mutation 03``() =
        CompilerAssert.CompileLibraryAndVerifyILWithOptions [|"-g"; "--optimize-"|]
            """
module Mutation03
let x = System.DateTime.Now
x.Day
            """
            (fun verifier -> verifier.VerifyIL [
            """
  .method public specialname static valuetype [mscorlib]System.DateTime
          get_x() cil managed
  {

    .maxstack  8
    IL_0000:  ldsfld     valuetype [mscorlib]System.DateTime '<StartupCode$assembly>'.$Mutation03::x@3
    IL_0005:  ret
  }
            """
            """
  .property valuetype [mscorlib]System.DateTime
          x()
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 09 00 00 00 00 00 )
    .get valuetype [mscorlib]System.DateTime Mutation03::get_x()
  }
            """
            """
        void  .cctor() cil managed
  {

    .maxstack  4
    .locals init (valuetype [runtime]System.DateTime V_0,
             valuetype [runtime]System.DateTime V_1)
    IL_0000:  call       valuetype [runtime]System.DateTime [runtime]System.DateTime::get_Now()
    IL_0005:  dup
    IL_0006:  stsfld     valuetype [runtime]System.DateTime '<StartupCode$assembly>'.$Mutation03::x@3
    IL_000b:  stloc.0
    IL_000c:  call       valuetype [runtime]System.DateTime Mutation03::get_x()
    IL_0011:  stloc.1
    IL_0012:  ldloca.s   V_1
    IL_0014:  call       instance int32 [runtime]System.DateTime::get_Day()
    IL_0019:  pop
    IL_001a:  ret
  }
            """
            ])

    [<Test>]
    let ``Mutation 04``() =
        CompilerAssert.CompileLibraryAndVerifyILWithOptions [|"-g"; "--optimize-"|]
            """
module Mutation04
let x = System.Decimal.MaxValue
x.ToString()
            """
            (fun verifier -> verifier.VerifyIL [
            """
  .method public specialname static valuetype [mscorlib]System.Decimal
          get_x() cil managed
  {

    .maxstack  8
    IL_0000:  ldsfld     valuetype [mscorlib]System.Decimal '<StartupCode$assembly>'.$Mutation04::x@3
    IL_0005:  ret
  }
            """
            """
  .property valuetype [mscorlib]System.Decimal
          x()
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 09 00 00 00 00 00 )
    .get valuetype [mscorlib]System.Decimal Mutation04::get_x()
  }
            """
            """
void  .cctor() cil managed
  {

    .maxstack  4
    .locals init (valuetype [runtime]System.Decimal V_0,
             valuetype [runtime]System.Decimal V_1)
    IL_0000:  ldsfld     valuetype [runtime]System.Decimal [runtime]System.Decimal::MaxValue
    IL_0005:  dup
    IL_0006:  stsfld     valuetype [runtime]System.Decimal '<StartupCode$assembly>'.$Mutation04::x@3
    IL_000b:  stloc.0
    IL_000c:  call       valuetype [runtime]System.Decimal Mutation04::get_x()
    IL_0011:  stloc.1
    IL_0012:  ldloca.s   V_1
    IL_0014:  constrained. [runtime]System.Decimal
    IL_001a:  callvirt   instance string [runtime]System.Object::ToString()
    IL_001f:  pop
    IL_0020:  ret
  }
            """
            ])

    [<Test>]
    let ``Mutation 05``() =
        CompilerAssert.CompileLibraryAndVerifyILWithOptions [|"-g"; "--optimize-"|]
            """
module Mutation05
type C() =
    [<VolatileFieldAttribute>]
    let mutable x = 1

    member this.X with get() = x and set v = x <- v


type StaticC() =
    [<VolatileFieldAttribute>]
    static let mutable x = 1

    static member X with get() = x and set v = x <- v
            """
            (fun verifier -> verifier.VerifyIL [
            """
  .class auto ansi serializable nested public C
         extends [mscorlib]System.Object
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 03 00 00 00 00 00 )
    .field assembly int32 x
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.VolatileFieldAttribute::.ctor() = ( 01 00 00 00 )
    .method public specialname rtspecialname
            instance void  .ctor() cil managed
    {

      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  callvirt   instance void [mscorlib]System.Object::.ctor()
      IL_0006:  ldarg.0
      IL_0007:  pop
      IL_0008:  ldarg.0
      IL_0009:  ldc.i4.1
      IL_000a:  volatile.
      IL_000c:  stfld      int32 Mutation05/C::x
      IL_0011:  ret
    }

    .method public hidebysig specialname
            instance int32  get_X() cil managed
    {

      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  volatile.
      IL_0003:  ldfld      int32 Mutation05/C::x
      IL_0008:  ret
    }

    .method public hidebysig specialname
            instance void  set_X(int32 v) cil managed
    {

      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  ldarg.1
      IL_0002:  volatile.
      IL_0004:  stfld      int32 Mutation05/C::x
      IL_0009:  ret
    }

    .property instance int32 X()
    {
      .set instance void Mutation05/C::set_X(int32)
      .get instance int32 Mutation05/C::get_X()
    }
  }
            """
            """
  .class auto ansi serializable nested public StaticC
         extends [mscorlib]System.Object
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 03 00 00 00 00 00 )
    .field static assembly int32 x
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.VolatileFieldAttribute::.ctor() = ( 01 00 00 00 )
    .field static assembly int32 init@10
    .method public specialname rtspecialname
            instance void  .ctor() cil managed
    {

      .maxstack  8
      IL_0000:  ldarg.0
      IL_0001:  callvirt   instance void [mscorlib]System.Object::.ctor()
      IL_0006:  ldarg.0
      IL_0007:  pop
      IL_0008:  ret
    }

    .method public specialname static int32
            get_X() cil managed
    {

        .maxstack  8
          IL_0000:  volatile.
          IL_0002:  ldsfld     int32 Mutation05/StaticC::init@10
          IL_0007:  ldc.i4.1
          IL_0008:  bge.s      IL_0013
    
          IL_000a:  call       void [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/IntrinsicFunctions::FailStaticInit()
          IL_000f:  nop
          IL_0010:  nop
          IL_0011:  br.s       IL_0014
    
          IL_0013:  nop
          IL_0014:  volatile.
          IL_0016:  ldsfld     int32 Mutation05/StaticC::x
          IL_001b:  ret
    }

    .method public specialname static void
            set_X(int32 v) cil managed
    {

        .maxstack  8
          IL_0000:  volatile.
          IL_0002:  ldsfld     int32 Mutation05/StaticC::init@10
          IL_0007:  ldc.i4.1
          IL_0008:  bge.s      IL_0013
    
          IL_000a:  call       void [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/IntrinsicFunctions::FailStaticInit()
          IL_000f:  nop
          IL_0010:  nop
          IL_0011:  br.s       IL_0014
    
          IL_0013:  nop
          IL_0014:  ldarg.0
          IL_0015:  volatile.
          IL_0017:  stsfld     int32 Mutation05/StaticC::x
          IL_001c:  ret
    }

    .method private specialname rtspecialname static
            void  .cctor() cil managed
    {

      .maxstack  8
      IL_0000:  ldc.i4.0
      IL_0001:  stsfld     int32 '<StartupCode$assembly>'.$Mutation05::init@
      IL_0006:  ldsfld     int32 '<StartupCode$assembly>'.$Mutation05::init@
      IL_000b:  pop
      IL_000c:  ret
    }

    .property int32 X()
    {
      .set void Mutation05/StaticC::set_X(int32)
      .get int32 Mutation05/StaticC::get_X()
    }
  }
            """
            """
    IL_0000:  ldc.i4.1
    IL_0001:  volatile.
    IL_0003:  stsfld     int32 Mutation05/StaticC::x
    IL_0008:  ldc.i4.1
    IL_0009:  volatile.
    IL_000b:  stsfld     int32 Mutation05/StaticC::init@10
    IL_0010:  ret
            """
            ])
