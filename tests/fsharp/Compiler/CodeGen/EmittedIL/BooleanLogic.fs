// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.UnitTests.CodeGen.EmittedIL

open FSharp.Test.Utilities
open NUnit.Framework

[<TestFixture>]
module ``BooleanLogic`` =

    [<Test>]
    let ``BooleanOrs``() =
        CompilerAssert.CompileLibraryAndVerifyILWithOptions [|"-g"; "--optimize+"|]
            """
module BooleanOrs
let compute (x: int) = 
    if (x = 1 || x = 2) then 2
    elif (x = 3 || x = 4) then 3
    else 4
            """
            (fun verifier -> verifier.VerifyIL [
            """
.method public static int32  compute(int32 x) cil managed
{
  
  .maxstack  8
  IL_0000:  ldarg.0
  IL_0001:  ldc.i4.1
  IL_0002:  beq.s      IL_0008
    
  IL_0004:  ldarg.0
  IL_0005:  ldc.i4.2
  IL_0006:  bne.un.s   IL_000a
    
  IL_0008:  ldc.i4.2
  IL_0009:  ret
    
  IL_000a:  ldarg.0
  IL_000b:  ldc.i4.3
  IL_000c:  beq.s      IL_0012
    
  IL_000e:  ldarg.0
  IL_000f:  ldc.i4.4
  IL_0010:  bne.un.s   IL_0014
    
  IL_0012:  ldc.i4.3
  IL_0013:  ret
    
  IL_0014:  ldc.i4.4
  IL_0015:  ret
} 
    
            """
            ])

