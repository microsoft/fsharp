// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.EmittedIL

open Xunit
open FSharp.Test.Utilities.Compiler

module ``TupleElimination`` =

    [<Fact>]
    let ``Sequence expressions with potential side effects do not prevent tuple elimination``() =
        FSharp """
module TupleElimination
open System.Runtime.CompilerServices

[<MethodImpl(MethodImplOptions.NoInlining)>]
let f () = 3

[<MethodImpl(MethodImplOptions.NoInlining)>]
let sideEffect () = ()

type Test =
    [<MethodImpl(MethodImplOptions.NoInlining)>]
    static member test(x: int32 * int32) = x

let v () =
    let a, b =
        "".ToString () |> ignore
        System.DateTime.Now |> ignore
        "3".ToString () |> ignore
        2L, f ()
    System.DateTime.Now |> ignore
    a, b

let w () =
    let (a, b) as t =
        "".ToString () |> ignore
        System.DateTime.Now |> ignore
        "3".ToString () |> ignore
        2, f ()
    System.DateTime.Now |> ignore
    let _ = Test.test(t)
    a + b

let x () =
    let a, b =
        "".ToString () |> ignore
        System.DateTime.Now |> ignore
        "3".ToString () |> ignore
        2, f ()
    System.DateTime.Now |> ignore
    a + b

let y () =
    let a, b, c =
        let a = f ()
        sideEffect ()
        a, f (), f ()
    a + b + c

let z () =
    let a, b, c =
        let u, v = 3, 4
        sideEffect ()
        f (), f () + u, f () + v
    a + b + c
         """
         |> compile
         |> shouldSucceed
         |> verifyIL [

(*
public static Tuple<long, int> v()
{
    string text = "".ToString();
    DateTime now = DateTime.Now;
    text = "3".ToString();
    int item = TupleElimination.f();
    now = DateTime.Now;
    return new Tuple<long, int>(2L, item);
}
*)
                      """
.method public static class [runtime]System.Tuple`2<int64,int32> 
        v() cil managed
{
  
  .maxstack  4
  .locals init (string V_0,
           valuetype [runtime]System.DateTime V_1,
           int32 V_2)
  IL_0000:  ldstr      ""
  IL_0005:  callvirt   instance string [runtime]System.Object::ToString()
  IL_000a:  stloc.0
  IL_000b:  call       valuetype [runtime]System.DateTime [runtime]System.DateTime::get_Now()
  IL_0010:  stloc.1
  IL_0011:  ldstr      "3"
  IL_0016:  callvirt   instance string [runtime]System.Object::ToString()
  IL_001b:  stloc.0
  IL_001c:  call       int32 TupleElimination::f()
  IL_0021:  stloc.2
  IL_0022:  call       valuetype [runtime]System.DateTime [runtime]System.DateTime::get_Now()
  IL_0027:  stloc.1
  IL_0028:  ldc.i4.2
  IL_0029:  conv.i8
  IL_002a:  ldloc.2
  IL_002b:  newobj     instance void class [runtime]System.Tuple`2<int64,int32>::.ctor(!0,
                                                                                              !1)
  IL_0030:  ret
}
"""

(*
public static int w()
{
    string text = "".ToString();
    DateTime now = DateTime.Now;
    text = "3".ToString();
    int num = TupleElimination.f();
    Tuple<int, int> x = new Tuple<int, int>(2, num);
    now = DateTime.Now;
    TupleElimination.Test.test(x);
    return 2 + num;
}
*)
                      """
.method public static int32  w() cil managed
{
  
  .maxstack  4
  .locals init (string V_0,
           valuetype [runtime]System.DateTime V_1,
           int32 V_2,
           class [runtime]System.Tuple`2<int32,int32> V_3)
  IL_0000:  ldstr      ""
  IL_0005:  callvirt   instance string [runtime]System.Object::ToString()
  IL_000a:  stloc.0
  IL_000b:  call       valuetype [runtime]System.DateTime [runtime]System.DateTime::get_Now()
  IL_0010:  stloc.1
  IL_0011:  ldstr      "3"
  IL_0016:  callvirt   instance string [runtime]System.Object::ToString()
  IL_001b:  stloc.0
  IL_001c:  call       int32 TupleElimination::f()
  IL_0021:  stloc.2
  IL_0022:  ldc.i4.2
  IL_0023:  ldloc.2
  IL_0024:  newobj     instance void class [runtime]System.Tuple`2<int32,int32>::.ctor(!0,
                                                                                              !1)
  IL_0029:  stloc.3
  IL_002a:  call       valuetype [runtime]System.DateTime [runtime]System.DateTime::get_Now()
  IL_002f:  stloc.1
  IL_0030:  ldloc.3
  IL_0031:  call       class [runtime]System.Tuple`2<int32,int32> TupleElimination/Test::test(class [runtime]System.Tuple`2<int32,int32>)
  IL_0036:  pop
  IL_0037:  ldc.i4.2
  IL_0038:  ldloc.2
  IL_0039:  add
  IL_003a:  ret
}
"""

(*
public static int x()
{
    string text = "".ToString();
    DateTime now = DateTime.Now;
    text = "3".ToString();
    int num = TupleElimination.f();
    now = DateTime.Now;
    return 2 + num;
}          
*)
                      """
.method public static int32  x() cil managed
{
  
  .maxstack  4
  .locals init (string V_0,
           valuetype [runtime]System.DateTime V_1,
           int32 V_2)
  IL_0000:  ldstr      ""
  IL_0005:  callvirt   instance string [runtime]System.Object::ToString()
  IL_000a:  stloc.0
  IL_000b:  call       valuetype [runtime]System.DateTime [runtime]System.DateTime::get_Now()
  IL_0010:  stloc.1
  IL_0011:  ldstr      "3"
  IL_0016:  callvirt   instance string [runtime]System.Object::ToString()
  IL_001b:  stloc.0
  IL_001c:  call       int32 TupleElimination::f()
  IL_0021:  stloc.2
  IL_0022:  call       valuetype [runtime]System.DateTime [runtime]System.DateTime::get_Now()
  IL_0027:  stloc.1
  IL_0028:  ldc.i4.2
  IL_0029:  ldloc.2
  IL_002a:  add
  IL_002b:  ret
}
"""

(*
public static int y()
{
    int num = TupleElimination.f();
    TupleElimination.sideEffect();
    return num + TupleElimination.f() + TupleElimination.f();
}
*)
                      """
.method public static int32  y() cil managed
{
  
  .maxstack  4
  .locals init (int32 V_0)
  IL_0000:  call       int32 TupleElimination::f()
  IL_0005:  stloc.0
  IL_0006:  call       void TupleElimination::sideEffect()
  IL_000b:  ldloc.0
  IL_000c:  call       int32 TupleElimination::f()
  IL_0011:  add
  IL_0012:  call       int32 TupleElimination::f()
  IL_0017:  add
  IL_0018:  ret
}
"""

(*
public static int z()
{
    TupleElimination.sideEffect();
    return TupleElimination.f() + (TupleElimination.f() + 3) + (TupleElimination.f() + 4);
}
*)
                      """
.method public static int32  z() cil managed
{
  
  .maxstack  8
  IL_0000:  call       void TupleElimination::sideEffect()
  IL_0005:  call       int32 TupleElimination::f()
  IL_000a:  call       int32 TupleElimination::f()
  IL_000f:  ldc.i4.3
  IL_0010:  add
  IL_0011:  add
  IL_0012:  call       int32 TupleElimination::f()
  IL_0017:  ldc.i4.4
  IL_0018:  add
  IL_0019:  add
  IL_001a:  ret
}
""" ]

    [<Fact>]
    let ``First class use of tuple prevents elimination``() =
        FSharp """
module TupleElimination
open System.Runtime.CompilerServices

[<MethodImpl(MethodImplOptions.NoInlining)>]
let f () = 3

[<MethodImpl(MethodImplOptions.NoInlining)>]
let cond () = true

type Test =
    [<MethodImpl(MethodImplOptions.NoInlining)>]
    static member test(x: int32 * int32) = x

let y () =
    let a, b =
        if cond () then
            1, 2
        else
            Test.test(3, 4)
    a + b

let z () =
    let a, b =
        "".ToString () |> ignore
        System.DateTime.Now |> ignore
        "3".ToString () |> ignore
        Test.test(2, f ())
    System.DateTime.Now |> ignore
    a + b
         """
         |> compile
         |> shouldSucceed
         |> verifyIL [

(*
public static int y()
{
    Tuple<int, int> tuple = (!TupleElimination.cond()) ? TupleElimination.Test.test(new Tuple<int, int>(3, 4)) : new Tuple<int, int>(1, 2);
    return tuple.Item1 + tuple.Item2;
}
*)
                      """
.method public static int32  y() cil managed
{
  
  .maxstack  4
  .locals init (class [runtime]System.Tuple`2<int32,int32> V_0)
  IL_0000:  call       bool TupleElimination::cond()
  IL_0005:  brfalse.s  IL_0010
    
  IL_0007:  ldc.i4.1
  IL_0008:  ldc.i4.2
  IL_0009:  newobj     instance void class [runtime]System.Tuple`2<int32,int32>::.ctor(!0,
                                                                                              !1)
  IL_000e:  br.s       IL_001c
    
  IL_0010:  ldc.i4.3
  IL_0011:  ldc.i4.4
  IL_0012:  newobj     instance void class [runtime]System.Tuple`2<int32,int32>::.ctor(!0,
                                                                                              !1)
  IL_0017:  call       class [runtime]System.Tuple`2<int32,int32> TupleElimination/Test::test(class [runtime]System.Tuple`2<int32,int32>)
  IL_001c:  stloc.0
  IL_001d:  ldloc.0
  IL_001e:  call       instance !0 class [runtime]System.Tuple`2<int32,int32>::get_Item1()
  IL_0023:  ldloc.0
  IL_0024:  call       instance !1 class [runtime]System.Tuple`2<int32,int32>::get_Item2()
  IL_0029:  add
  IL_002a:  ret
}
"""

(*
public static int z()
{
    string text = "".ToString();
    DateTime now = DateTime.Now;
    text = "3".ToString();
    Tuple<int, int> tuple = TupleElimination.Test.test(new Tuple<int, int>(2, TupleElimination.f()));
    int item = tuple.Item2;
    int item2 = tuple.Item1;
    now = DateTime.Now;
    return item2 + item;
}
*)
                      """
.method public static int32  z() cil managed
{
  
  .maxstack  4
  .locals init (class [runtime]System.Tuple`2<int32,int32> V_0,
           string V_1,
           valuetype [runtime]System.DateTime V_2,
           int32 V_3,
           int32 V_4)
  IL_0000:  ldstr      ""
  IL_0005:  callvirt   instance string [runtime]System.Object::ToString()
  IL_000a:  stloc.1
  IL_000b:  call       valuetype [runtime]System.DateTime [runtime]System.DateTime::get_Now()
  IL_0010:  stloc.2
  IL_0011:  ldstr      "3"
  IL_0016:  callvirt   instance string [runtime]System.Object::ToString()
  IL_001b:  stloc.1
  IL_001c:  ldc.i4.2
  IL_001d:  call       int32 TupleElimination::f()
  IL_0022:  newobj     instance void class [runtime]System.Tuple`2<int32,int32>::.ctor(!0,
                                                                                              !1)
  IL_0027:  call       class [runtime]System.Tuple`2<int32,int32> TupleElimination/Test::test(class [runtime]System.Tuple`2<int32,int32>)
  IL_002c:  stloc.0
  IL_002d:  ldloc.0
  IL_002e:  call       instance !1 class [runtime]System.Tuple`2<int32,int32>::get_Item2()
  IL_0033:  stloc.3
  IL_0034:  ldloc.0
  IL_0035:  call       instance !0 class [runtime]System.Tuple`2<int32,int32>::get_Item1()
  IL_003a:  stloc.s    V_4
  IL_003c:  call       valuetype [runtime]System.DateTime [runtime]System.DateTime::get_Now()
  IL_0041:  stloc.2
  IL_0042:  ldloc.s    V_4
  IL_0044:  ldloc.3
  IL_0045:  add
  IL_0046:  ret
}""" ]

    [<Fact>]
    let ``Branching let binding rhs does not prevent tuple elimination``() =
        FSharp """
module TupleElimination
open System.Runtime.CompilerServices

[<MethodImpl(MethodImplOptions.NoInlining)>]
let f () = 3

[<MethodImpl(MethodImplOptions.NoInlining)>]
let sideEffect () = ()

[<MethodImpl(MethodImplOptions.NoInlining)>]
let cond () = true

type Test =
    [<MethodImpl(MethodImplOptions.NoInlining)>]
    static member test(x: int32 * int32) = x

let x () =
    let a, b =
        sideEffect ()
        if cond () then
            let v = "yep"
            let v2 = if cond () then 1 else 3
            v, v2
        else
            "", f ()
    a, b

let rec y () =
    let a, b, c =
        if cond () then
            "".ToString () |> ignore
            1, f (), 3
        else
            if cond () then
                2, 2, 3
            else
                match 1 / 0 with
                | 1 ->
                    if 2 / 3 = 1 then
                        5, 6, 7
                    else
                        "".ToString () |> ignore
                        6, 5, 4
                | 2 -> 6, 6, 6
                | 3 -> f (), 7, f ()
                | _ -> 8, y (), y ()
    
    a + b + (2 * c)

let z () =
    let a, b =
        if cond () then
            1, 3
        else
            3, 4
    a + b
         """
         |> compile
         |> shouldSucceed
         |> verifyIL [

(*
public static Tuple<string, int> x()
{
    TupleElimination.sideEffect();
    string item;
    int item2;
    if (TupleElimination.cond())
    {
        int num = (!TupleElimination.cond()) ? 3 : 1;
        item = "yep";
        item2 = num;
    }
    else
    {
        item = "";
        item2 = TupleElimination.f();
    }
    return new Tuple<string, int>(item, item2);
}
*)
                      """
.method public static class [runtime]System.Tuple`2<string,int32> 
        x() cil managed
{
    
  .maxstack  4
  .locals init (string V_0,
           int32 V_1,
           int32 V_2)
  IL_0000:  call       void TupleElimination::sideEffect()
  IL_0005:  call       bool TupleElimination::cond()
  IL_000a:  brfalse.s  IL_0022

  IL_000c:  call       bool TupleElimination::cond()
  IL_0011:  brfalse.s  IL_0016

  IL_0013:  ldc.i4.1
  IL_0014:  br.s       IL_0017

  IL_0016:  ldc.i4.3
  IL_0017:  stloc.2
  IL_0018:  ldstr      "yep"
  IL_001d:  stloc.0
  IL_001e:  ldloc.2
  IL_001f:  stloc.1
  IL_0020:  br.s       IL_002e

  IL_0022:  ldstr      ""
  IL_0027:  stloc.0
  IL_0028:  call       int32 TupleElimination::f()
  IL_002d:  stloc.1
  IL_002e:  ldloc.0
  IL_002f:  ldloc.1
  IL_0030:  newobj     instance void class [runtime]System.Tuple`2<string,int32>::.ctor(!0,
                                                                                                 !1)
  IL_0035:  ret
}
"""

(*
public static int y()
{
    int num;
    int num2;
    int num3;
    if (TupleElimination.cond())
    {
        string text = "".ToString();
        num = 1;
        num2 = TupleElimination.f();
        num3 = 3;
    }
    else if (TupleElimination.cond())
    {
        num = 2;
        num2 = 2;
        num3 = 3;
    }
    else
    {
        switch (1 / 0)
        {
        case 1:
            if (2 / 3 == 1)
            {
                num = 5;
                num2 = 6;
                num3 = 7;
            }
            else
            {
                string text = "".ToString();
                num = 6;
                num2 = 5;
                num3 = 4;
            }
            break;
        case 2:
            num = 6;
            num2 = 6;
            num3 = 6;
            break;
        case 3:
            num = TupleElimination.f();
            num2 = 7;
            num3 = TupleElimination.f();
            break;
        default:
            num = 8;
            num2 = TupleElimination.y();
            num3 = TupleElimination.y();
            break;
        }
    }
    return num + num2 + 2 * num3;
}
*)
                      """
.method public static int32  y() cil managed
{

  .maxstack  5
  .locals init (int32 V_0,
           int32 V_1,
           int32 V_2,
           string V_3)
  IL_0000:  ldc.i4.0
  IL_0001:  stloc.0
  IL_0002:  ldc.i4.0
  IL_0003:  stloc.1
  IL_0004:  ldc.i4.0
  IL_0005:  stloc.2
  IL_0006:  call       bool TupleElimination::cond()
  IL_000b:  brfalse.s  IL_0027

  IL_000d:  ldstr      ""
  IL_0012:  callvirt   instance string [runtime]System.Object::ToString()
  IL_0017:  stloc.3
  IL_0018:  ldc.i4.1
  IL_0019:  stloc.0
  IL_001a:  call       int32 TupleElimination::f()
  IL_001f:  stloc.1
  IL_0020:  ldc.i4.3
  IL_0021:  stloc.2
  IL_0022:  br         IL_0095
  
  IL_0027:  call       bool TupleElimination::cond()
  IL_002c:  brfalse.s  IL_0036
  
  IL_002e:  ldc.i4.2
  IL_002f:  stloc.0
  IL_0030:  ldc.i4.2
  IL_0031:  stloc.1
  IL_0032:  ldc.i4.3
  IL_0033:  stloc.2
  IL_0034:  br.s       IL_0095
  
  IL_0036:  ldc.i4.1
  IL_0037:  ldc.i4.0
  IL_0038:  div
  IL_0039:  ldc.i4.1
  IL_003a:  sub
  IL_003b:  switch     (
                        IL_004e,
                        IL_006f,
                        IL_0077)
  IL_004c:  br.s       IL_0087
  
  IL_004e:  ldc.i4.2
  IL_004f:  ldc.i4.3
  IL_0050:  div
  IL_0051:  ldc.i4.1
  IL_0052:  bne.un.s   IL_005c
  
  IL_0054:  ldc.i4.5
  IL_0055:  stloc.0
  IL_0056:  ldc.i4.6
  IL_0057:  stloc.1
  IL_0058:  ldc.i4.7
  IL_0059:  stloc.2
  IL_005a:  br.s       IL_0095
  
  IL_005c:  ldstr      ""
  IL_0061:  callvirt   instance string [runtime]System.Object::ToString()
  IL_0066:  stloc.3
  IL_0067:  ldc.i4.6
  IL_0068:  stloc.0
  IL_0069:  ldc.i4.5
  IL_006a:  stloc.1
  IL_006b:  ldc.i4.4
  IL_006c:  stloc.2
  IL_006d:  br.s       IL_0095
  
  IL_006f:  ldc.i4.6
  IL_0070:  stloc.0
  IL_0071:  ldc.i4.6
  IL_0072:  stloc.1
  IL_0073:  ldc.i4.6
  IL_0074:  stloc.2
  IL_0075:  br.s       IL_0095
  
  IL_0077:  call       int32 TupleElimination::f()
  IL_007c:  stloc.0
  IL_007d:  ldc.i4.7
  IL_007e:  stloc.1
  IL_007f:  call       int32 TupleElimination::f()
  IL_0084:  stloc.2
  IL_0085:  br.s       IL_0095
  
  IL_0087:  ldc.i4.8
  IL_0088:  stloc.0
  IL_0089:  call       int32 TupleElimination::y()
  IL_008e:  stloc.1
  IL_008f:  call       int32 TupleElimination::y()
  IL_0094:  stloc.2
  IL_0095:  ldloc.0
  IL_0096:  ldloc.1
  IL_0097:  add
  IL_0098:  ldc.i4.2
  IL_0099:  ldloc.2
  IL_009a:  mul
  IL_009b:  add
  IL_009c:  ret
}
"""

(*
public static int z()
{
    int num;
    int num2;
    if (TupleElimination.cond())
    {
        num = 1;
        num2 = 3;
    }
    else
    {
        num = 3;
        num2 = 4;
    }
    return num + num2;
}
*)
                      """
.method public static int32  z() cil managed
{
  
  .maxstack  4
  .locals init (int32 V_0,
           int32 V_1)
  IL_0000:  call       bool TupleElimination::cond()
  IL_0005:  brfalse.s  IL_000d
    
  IL_0007:  ldc.i4.1
  IL_0008:  stloc.0
  IL_0009:  ldc.i4.3
  IL_000a:  stloc.1
  IL_000b:  br.s       IL_0011
    
  IL_000d:  ldc.i4.3
  IL_000e:  stloc.0
  IL_000f:  ldc.i4.4
  IL_0010:  stloc.1
  IL_0011:  ldloc.0
  IL_0012:  ldloc.1
  IL_0013:  add
  IL_0014:  ret
}""" ]




    [<Fact>]
    let ``Branching let binding of tuple with capture doesn't promote``() =
        FSharp """
module TupleElimination
open System.Runtime.CompilerServices

let testFunction(a,b) =
    let x,y = printfn "hello"; b*a,a*b
    (fun () -> x + y)
         """
         |> compile
         |> shouldSucceed
         |> verifyIL [
         // Checks the captured 'x' and 'y' are not promoted onto the heap
                      """
.method public static class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<class [FSharp.Core]Microsoft.FSharp.Core.Unit,int32> 
        testFunction(int32 a,
                     int32 b) cil managed
{
  
  .maxstack  4
  .locals init (class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit> V_0,
           int32 V_1,
           int32 V_2)
  IL_0000:  ldstr      "hello"
  IL_0005:  newobj     instance void class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`5<class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>::.ctor(string)
  IL_000a:  stloc.0
  IL_000b:  call       class [netstandard]System.IO.TextWriter [netstandard]System.Console::get_Out()
  IL_0010:  ldloc.0
  IL_0011:  call       !!0 [FSharp.Core]Microsoft.FSharp.Core.PrintfModule::PrintFormatLineToTextWriter<class [FSharp.Core]Microsoft.FSharp.Core.Unit>(class [runtime]System.IO.TextWriter,
                                                                                                                                                       class [FSharp.Core]Microsoft.FSharp.Core.PrintfFormat`4<!!0,class [runtime]System.IO.TextWriter,class [FSharp.Core]Microsoft.FSharp.Core.Unit,class [FSharp.Core]Microsoft.FSharp.Core.Unit>)
  IL_0016:  pop
  IL_0017:  ldarg.1
  IL_0018:  ldarg.0
  IL_0019:  mul
  IL_001a:  stloc.1
  IL_001b:  ldarg.0
  IL_001c:  ldarg.1
  IL_001d:  mul
  IL_001e:  stloc.2
  IL_001f:  ldloc.1
  IL_0020:  ldloc.2
  IL_0021:  newobj     instance void TupleElimination/testFunction@7::.ctor(int32,
                                                                            int32)
  IL_0026:  ret
} 

""" ]




    [<Fact>]
    let ``Branching let binding of tuple gives good names in closure``() =
        FSharp """
module TupleElimination
open System.Runtime.CompilerServices

let testFunction(a,b) =
    let x,y = printfn "hello"; b*a,a*b
    (fun () -> x + y)
     """
     |> compile
     |> shouldSucceed
     |> verifyIL [

     // Checks the names of captured 'x' and 'y'. 
                  """

        .method public strict virtual instance int32 
                Invoke(class [FSharp.Core]Microsoft.FSharp.Core.Unit unitVar0) cil managed
        {
          
          .maxstack  8
          IL_0000:  ldarg.0
          IL_0001:  ldfld      int32 TupleElimination/testFunction@7::x
          IL_0006:  ldarg.0
          IL_0007:  ldfld      int32 TupleElimination/testFunction@7::y
          IL_000c:  add
          IL_000d:  ret
        } 
""" ]




