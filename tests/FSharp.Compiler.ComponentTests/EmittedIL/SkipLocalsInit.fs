// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.EmittedIL

open Xunit
open FSharp.Test.Utilities.Compiler

#if NETCOREAPP
module ``SkipLocalsInit`` =
    [<Fact>]
    let ``Init in function and closure not emitted when applied on function``() =
        FSharp """
module SkipLocalsInit

[<System.Runtime.CompilerServices.SkipLocalsInitAttribute>]
let x () =
    [||] |> Array.filter (fun x -> let y = "".Length in y + y = x) |> ignore
"""
         |> compile
         |> shouldSucceed
         |> verifyIL ["""
.method public static void  x() cil managed
{
  .custom instance void [runtime]System.Runtime.CompilerServices.SkipLocalsInitAttribute::.ctor() = ( 01 00 00 00 )

  .maxstack  4
  .locals (int32[] V_0)
"""

                      """
.method public strict virtual instance bool 
        Invoke(int32 x) cil managed
{
          
  .maxstack  6
  .locals (int32 V_0)"""]

    [<Fact>]
    let ``Init in static method not emitted when applied on class``() =
        FSharp """
module SkipLocalsInit

[<System.Runtime.CompilerServices.SkipLocalsInitAttribute>]
type X () =
    static member Y () =
        let x = "ssa".Length
        x + x
         """
         |> compile
         |> shouldSucceed
         |> verifyIL ["""
.custom instance void [runtime]System.Runtime.CompilerServices.SkipLocalsInitAttribute::.ctor() = ( 01 00 00 00 ) 
.custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 03 00 00 00 00 00 ) 
"""

                      """
.method public static int32  Y() cil managed
{
  
  .maxstack  4
  .locals (int32 V_0)"""]

    [<Fact>]
    let ``Init in static method and function not emitted when applied on module``() =
        FSharp """
[<System.Runtime.CompilerServices.SkipLocalsInitAttribute>]
module SkipLocalsInit

let x () =
    let x = "ssa".Length
    x + x

type X () =
    static member Y () =
        let x = "ssa".Length
        x + x
         """
         |> compile
         |> shouldSucceed
         |> verifyIL ["""
.custom instance void [runtime]System.Runtime.CompilerServices.SkipLocalsInitAttribute::.ctor() = ( 01 00 00 00 ) 
.custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
"""

                      """
.method public static int32  x() cil managed
{

  .maxstack  4
  .locals (int32 V_0)
"""

                      """
.method public static int32  Y() cil managed
{
  
  .maxstack  4
  .locals (int32 V_0)"""]

    [<Fact>]
    let ``Init in method and closure not emitted when applied on method``() =
        FSharp """
module SkipLocalsInit

type X () =
    [<System.Runtime.CompilerServices.SkipLocalsInitAttribute>]
    member _.Y () =
        [||] |> Array.filter (fun x -> let y = "".Length in y + y = x) |> ignore
        let x = "ssa".Length
        x + x
         """
         |> compile
         |> shouldSucceed
         |> verifyIL ["""
.method public hidebysig instance int32 
        Y() cil managed
{
  .custom instance void [runtime]System.Runtime.CompilerServices.SkipLocalsInitAttribute::.ctor() = ( 01 00 00 00 ) 
  
  .maxstack  4
  .locals (int32[] V_0,
           int32 V_1)
"""
           
                      """
.method public strict virtual instance bool 
        Invoke(int32 x) cil managed
{
  
  .maxstack  6
  .locals (int32 V_0)
"""]
#endif