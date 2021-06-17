#r "provider.dll"
#r "providedNullAssemblyName.dll"

[<AutoOpen>]
module Infrastructure = 
    let mutable failures = []
    let reportFailure s = 
      stdout.WriteLine "\n................TEST FAILED...............\n"; failures <- failures @ [s]

    let check s e r = 
      if r = e then  stdout.WriteLine (s+": YES") 
      else (eprintfn "\n***** %s: FAIL, expected %A, got %A\n" s r e; reportFailure s)

    let test s b = 
      if b then ( (* stdout.WriteLine ("passed: " + s) *) ) 
      else (stderr.WriteLine ("failure: " + s); 
            reportFailure s)
(*========================================================================*)

type TAlias1 = ProvidedTypeFromGlobalNamespace
type TAlias2 = global.ProvidedTypeFromGlobalNamespace

module BasicErasedProvidedTypeTest = 
    check "cwpmew90"
        FSharp.HelloWorld.HelloWorldType.StaticProperty1
        "You got a static property"

    check "cwkeonwe09"
        FSharp.HelloWorld.HelloWorldType.StaticProperty2
        42

    // This is just checking the type checking
    check "cwkeonwe09a"
        (FSharp.HelloWorld.HelloWorldType.op_Addition(FSharp.HelloWorld.HelloWorldType(),FSharp.HelloWorld.HelloWorldType()) |> ignore; 1)
        1

    // This is just checking the type checking
    let obj1 = FSharp.HelloWorld.HelloWorldType()
    let obj2 = FSharp.HelloWorld.HelloWorldType()
    check "cwkeonwe09b"
        (obj1 + obj2)
        obj1

    check "cwpmew90"
        FSharp.HelloWorld.HelloWorldType.NestedType.StaticProperty1
        "You got a static property"

    check "cwkeonwe09"
        FSharp.HelloWorld.HelloWorldType.NestedType.StaticProperty2
        42

    check "cwpmew901x"
        (obj1.GetType())
        typeof<obj>

    check "cwkeonwe09a"
        (let mutable localVar = 10 
         FSharp.HelloWorld.HelloWorldType.UsesByRef(&localVar))
        11

    check "cwkeonwe09a221"
        (FSharp.HelloWorld.HelloWorldType.OneOptionalParameter())
        3376

    check "cwkeonwe09a222"
        (FSharp.HelloWorld.HelloWorldType.OneOptionalParameter(42))
        43

    check "cwkeonwe09a223"
        (FSharp.HelloWorld.HelloWorldType.TwoOptionalParameters())
        (3375 + 3390 + 1)

    check "cwkeonwe09a224"
        (FSharp.HelloWorld.HelloWorldType.TwoOptionalParameters(3))
        (3 + 3390 + 1)

    check "cwkeonwe09a225"
        (FSharp.HelloWorld.HelloWorldType.TwoOptionalParameters(arg1=3))
        (3 + 3390 + 1)

    check "cwkeonwe09a226"
        (FSharp.HelloWorld.HelloWorldType.TwoOptionalParameters(arg2=4))
        (3375 + 4 + 1)

    check "cwkeonwe09a227"
        (FSharp.HelloWorld.HelloWorldType.TwoOptionalParameters(arg1=2,arg2=4))
        (2 + 4 + 1)

    check "cwkeonwe09a"
        (let v1,v2 = FSharp.HelloWorld.HelloWorldType.UsesByRefAsOutParameter(3)
         (v1,v2))
        (17,4)

    check "cwkeonwe09a1"
        (FSharp.HelloWorld.HelloWorldType.ReturnsDefaultDateTime())
        (System.DateTime())

    check "cwkeonwe09a13345"
        (FSharp.HelloWorld.HelloWorldType.ReturnsNewArray())
        [| 3;6 |]


    check "cwkeonwe09a13355 - null attrib can't be used"
        (null: FSharp.HelloWorld.HelloWorldType.NestedType) // should NOT  give a type error - this explicitly has AllowNullLiteralAttribute(true), so a null literal is allowed
        null

    // should NOT give a type error - this doesn't have any attributes, and a null literal is allowed by default
    check "cwkeonwe09a13355 - null attrib"
        (null : FSharp.HelloWorld.HelloWorldSubType) 
        null

    check "cwkeonwe09a13355"
        (FSharp.HelloWorld.HelloWorldType.ReturnsEmptyNewArray())
        [| |]

    check "cwkeonwe09a133561"
        (FSharp.HelloWorld.HelloWorldType.IfThenElseUnitUnit true)
        ()

    check "cwkeonwe09a1335634"
        (FSharp.HelloWorld.HelloWorldType.SequentialSmokeTest())
        1

    check "cwkeonwe09a1335634b"
        (FSharp.HelloWorld.HelloWorldType.SequentialSmokeTest2())
        ()

    check "cwkeonwe09a1335635"
        (let f = FSharp.HelloWorld.HelloWorldType.LambdaSmokeTest() in f 5)
        6

    check "cwkeonwe09a1335635b"
        (let f = FSharp.HelloWorld.HelloWorldType.LambdaSmokeTest2 () in f ())
        ()

    check "cwkeonwe09a1335635c"
        (let f = FSharp.HelloWorld.HelloWorldType.LambdaSmokeTest3 () in f 3 4)
        7

    check "cwkeonwe09a1335635d"
        // The test gets arr.[0,0] 
        (FSharp.HelloWorld.HelloWorldType.ArrayGetSmokeTest [| 11;2;3 |] )
        11


    check "cwkeonwe09a1335635e"
        // The test sets arr.[0] to 3
        (let arr = [| 11;2;3 |] in FSharp.HelloWorld.HelloWorldType.ArraySetSmokeTest arr; arr.[0] )
        3

    check "cwkeonwe09a1335635e2"
        // The test sets a local mutable value to 4
        (FSharp.HelloWorld.HelloWorldType.VarSetSmokeTest())
        4

    check "cwkeonwe09a1335635f"
        // The test sets arr.[0,0] to 3
        (let arr = array2D [| [| 11;2;6 |] ; [| 1112;21;31 |] |] in FSharp.HelloWorld.HelloWorldType.Array2DSetSmokeTest arr; arr.[0,0] )
        3

    check "cwkeonwe09a1335635f"
        // The test gets arr.[0,0] 
        (FSharp.HelloWorld.HelloWorldType.Array2DGetSmokeTest (array2D [| [| 12;2;6 |] ; [| 1112;21;31 |] |]) )
        12

    check "cwkeonwe09a1335635g"
        (FSharp.HelloWorld.HelloWorldType.IsMarkedObsolete1()) // we expect a warning here
        1

    check "cwkeonwe09a1335635h"
        (FSharp.HelloWorld.HelloWorldType.IsMarkedObsolete2()) // we expect a warning here
        2

    check "cwkeonwe09a1335635i"
        (FSharp.HelloWorld.HelloWorldType.IsMarkedConditional1(); 3)
        3

    check "cwkeonwe09a1335635h"
        (FSharp.HelloWorld.HelloWorldType.TakesParamArray(3,[| |]))
        0

    check "cwkeonwe09a1335635h1"
        (FSharp.HelloWorld.HelloWorldType.TakesParamArray(3))
        0

    check "cwkeonwe09a1335635h2"
        (FSharp.HelloWorld.HelloWorldType.TakesParamArray(3,4))
        1

    check "cwkeonwe09a1335635h5"
        (FSharp.HelloWorld.HelloWorldType.TakesParamArray(3,4,5,6,7,8))
        5

    check "cwkeonwe09a1335655433"
        (FSharp.HelloWorld.HelloWorldType.TryWithSmokeTest())
        3

    // Note, the provided expressions contain a use of "reraise". This is permitted.
    check "cwkeonwe09a1335655434"
        (FSharp.HelloWorld.HelloWorldType.TryWithSmokeTest3())
        true

    check "cwkeonwe09a1335655435"
        (FSharp.HelloWorld.HelloWorldType.TryWithSmokeTest2())
        true

    check "cwkeonwe09a13356432437"
        (FSharp.HelloWorld.HelloWorldType.TryFinallySmokeTest())
        3

    check "cwkeonwe09a13356b"
        (FSharp.HelloWorld.HelloWorldType.IfThenElseVoidUnit true)
        ()


    check "cwkeonwe09a13356c"
        (FSharp.HelloWorld.HelloWorldType.NewUnionCaseSmokeTest1())
        []

    check "cwkeonwe09a13356d"
        (FSharp.HelloWorld.HelloWorldType.NewUnionCaseSmokeTest2())
        [1]


    check "cwkeonwe09a13356e"
        (FSharp.HelloWorld.HelloWorldType.NewUnionCaseSmokeTest3())
        None

    check "cwkeonwe09a13356f"
        (FSharp.HelloWorld.HelloWorldType.NewUnionCaseSmokeTest4())
        (Some 3)

    check "cwkeonwe09a13356g"
        (FSharp.HelloWorld.HelloWorldType.NewUnionCaseSmokeTest5())
        (Choice1Of2 3)


    check "cwkeonwe09a13356g1"
        (let del = FSharp.HelloWorld.HelloWorldType.NewDelegateSmokeTest1 () in del.Invoke 3)
        4

    check "cwkeonwe09a13356g2"
        (let del = FSharp.HelloWorld.HelloWorldType.NewDelegateSmokeTest2 () in del.Invoke(3,4))
        7

    check "cwkeonwe09a13356g3"
        (FSharp.HelloWorld.HelloWorldType.ForIntegerRangeLoopSmokeTest1 ())
        11

    check "cwkeonwe09a13356hb"
        (FSharp.HelloWorld.HelloWorldType.NewObjectGeneric1().Count)
        0

    check "cwkeonwe09a13356hc"
        (FSharp.HelloWorld.HelloWorldType.NewObjectGeneric2().Count)
        0

    check "cwkeonwe09a13356h1a"
        (FSharp.HelloWorld.HelloWorldType.StructProperty1())
        86400.0

    check "cwkeonwe09a13356h1b"
        (FSharp.HelloWorld.HelloWorldType.StructProperty2())
        86400.0

    check "cwkeonwe09a13356h1c"
        (FSharp.HelloWorld.HelloWorldType.StructProperty3())
        86400.0

    check "cwkeonwe09a13356h1d"
        (FSharp.HelloWorld.HelloWorldType.StructMethod1())
        (86400.0 * 2.0)

    check "cwkeonwe09a13356h13"
        (FSharp.HelloWorld.HelloWorldType.StructMethod2())
        (86400.0 * 2.0)

    check "cwkeonwe09a13356h1"
        (FSharp.HelloWorld.HelloWorldType.NewRecordSmokeTest1().contents)
        3

    check "cwkeonwe09a13356h2"
        (FSharp.HelloWorld.HelloWorldType.UnionCaseTest1 [1])
        true

    check "cwkeonwe09a13356h3"
        (FSharp.HelloWorld.HelloWorldType.UnionCaseTest1 [])
        false

    check "cwkeonwe09a13356h4"
        (FSharp.HelloWorld.HelloWorldType.UnionCaseTest2 (Some 1))
        true


    check "cwkeonwe09a13356h5"
        (FSharp.HelloWorld.HelloWorldType.UnionCaseTest2 None)
        false


    check "cwkeonwe09a2"
        (FSharp.HelloWorld.HelloWorldType.ReturnsDefaultDayOfWeek())
        (System.DayOfWeek())

    check "cwkeonwe09a3"
        (FSharp.HelloWorld.HelloWorldType.ReturnsMondayDayOfWeek())
        System.DayOfWeek.Monday

    check "cwkeonwe09a4"
        (FSharp.HelloWorld.HelloWorldType.ReturnsNullSeqString())
        null

                   

    let testIntrinsics() = 
            FSharp.HelloWorld.HelloWorldType.CallInstrinsics()

    testIntrinsics()


    check "cwkeonwe09a5"
        (FSharp.HelloWorld.HelloWorldType.ReturnsNullString())
        null


    let test1() = 
        check "cwkeonwe09a8731"
            (FSharp.HelloWorld.HelloWorldType.GetItem1OfTuple2OfInt( (3,6) ))
            3
    test1()

    let test2() = 
        check "cwkeonwe09a8732"
            (FSharp.HelloWorld.HelloWorldType.GetItem2OfTuple2OfInt( (3,6) ))
            6
    test2()

    let test3() = 
        check "cwkeonwe09a87317"
            (FSharp.HelloWorld.HelloWorldType.GetItem1OfTuple7OfInt( (3,6,9,12,15,18,21) ))
            3
    test3()

    let test4() = 
        check "cwkeonwe09a87327"
            (FSharp.HelloWorld.HelloWorldType.GetItem7OfTuple7OfInt( (3,6,9,12,15,18,21) ))
            21
    test4()

    let test5() = 
        check "cwkeonwe09a87318"
            (FSharp.HelloWorld.HelloWorldType.GetItem1OfTuple8OfInt( (3,6,9,12,15,18,21,24) ))
            3
    test5()

    let test6() = 
        check "cwkeonwe09a87328"
            (FSharp.HelloWorld.HelloWorldType.GetItem7OfTuple8OfInt( (3,6,9,12,15,18,21,24) ))
            21
    test6()

    let test7() = 
        check "cwkeonwe09a87338"
            (FSharp.HelloWorld.HelloWorldType.GetItem8OfTuple8OfInt( (3,6,9,12,15,18,21,24) ))
            24
    test7()

    let test8() = 
        check "cwkeonwe09a87319"
            (FSharp.HelloWorld.HelloWorldType.GetItem1OfTuple9OfInt( (3,6,9,12,15,18,21,24,27) ))
            3
    test8()

    let test9() = 
        check "cwkeonwe09a87329"
            (FSharp.HelloWorld.HelloWorldType.GetItem7OfTuple9OfInt( (3,6,9,12,15,18,21,24,27) ))
            21
    test9()

    let test10() = 
        check "cwkeonwe09a87339"
            (FSharp.HelloWorld.HelloWorldType.GetItem8OfTuple9OfInt( (3,6,9,12,15,18,21,24,27) ))
            24
    test10()

    let test11() = 
        check "cwkeonwe09a87349"
            (FSharp.HelloWorld.HelloWorldType.GetItem9OfTuple9OfInt( (3,6,9,12,15,18,21,24,27) ))
            27
    test11()

    let test12() = 
        check "cwkeonwe09a62"
            (FSharp.HelloWorld.HelloWorldType.ReturnsTuple2OfInt())
            (3,6)
    test12()

    let test13() = 
        check "cwkeonwe09a63"
            (FSharp.HelloWorld.HelloWorldType.ReturnsTuple3OfInt())
            (3,6,9)
    test13() 

    let test14() = 
        check "cwkeonwe09a64"
            (FSharp.HelloWorld.HelloWorldType.ReturnsTuple4OfInt())
            (3,6,9,12)
    test14()

    let test15() = 
        check "cwkeonwe09a65"
            (FSharp.HelloWorld.HelloWorldType.ReturnsTuple5OfInt())
            (3,6,9,12,15)
    test15()

    let test16() = 
        check "cwkeonwe09a66"
            (FSharp.HelloWorld.HelloWorldType.ReturnsTuple6OfInt())
            (3,6,9,12,15,18)
    test16()

    let test17() = 
        check "cwkeonwe09a67"
            (FSharp.HelloWorld.HelloWorldType.ReturnsTuple7OfInt())
            (3,6,9,12,15,18,21)
    test17()

    let test18() = 
        check "cwkeonwe09a68"
            (FSharp.HelloWorld.HelloWorldType.ReturnsTuple8OfInt())
            (3,6,9,12,15,18,21,24)
    test18() 

    check "cwkeonwe09a69"
        (FSharp.HelloWorld.HelloWorldType.ReturnsTuple9OfInt())
        (3,6,9,12,15,18,21,24,27)

    check "cwkeonwe09a7"
        (FSharp.HelloWorld.HelloWorldType.ReturnsUnit())
        (())

    check "cwpmew901"
        (FSharp.HelloWorld.HelloWorldType().GetType())
        typeof<obj>

    check "cwpmew902"
        (FSharp.HelloWorld.HelloWorldSubType().GetType())
        typeof<obj>

    check "cwpmew903"
        (FSharp.HelloWorld.HelloWorldType.NestedType().GetType())
        typeof<obj>


    // Check a simple quotation of an erased type
    check "cwpmew901"
        <@ (fun (x : FSharp.HelloWorld.HelloWorldType) -> x) @>.Type
        typeof<obj -> obj>

    // Check a simple quotation of an erased type that inherits from another erased type
    check "cwpmew902"
        <@ (fun (x : FSharp.HelloWorld.HelloWorldSubType) -> x) @>.Type
        typeof<obj -> obj>

    // Check a simple quotation of an erased type that inherits from another erased type
    check "cwpmew902"
        <@ (fun (x : FSharp.HelloWorld.HelloWorldType.NestedType) -> x) @>.Type
        typeof<obj -> obj>

    check "cewnew0wel" 
       (obj1 - obj2)
       obj1

    check "cewnew0wel_equality" 
       <@ FSharp.HelloWorld.HelloWorldType() - FSharp.HelloWorld.HelloWorldType() @>
       <@ FSharp.HelloWorld.HelloWorldType() - FSharp.HelloWorld.HelloWorldType() @>

    check "cewnew0wel_equality" 
       <@ FSharp.HelloWorld.HelloWorldType() + FSharp.HelloWorld.HelloWorldType() @>
       <@ FSharp.HelloWorld.HelloWorldType() + FSharp.HelloWorld.HelloWorldType() @>


module BasicNestedNamespaceErasedProvidedTypeTest = 
    check "cwpmew902"
        FSharp.HelloWorld.NestedNamespace1.HelloWorldType.StaticProperty1
        "You got a static property"

    check "cwpmew903"
        FSharp.HelloWorld.Nested.Nested.Nested.Namespace2.HelloWorldType.StaticProperty1
        "You got a static property"

    module Scope1 = 
        open FSharp.HelloWorld.NestedNamespace1

        check "cwpmew904"
            HelloWorldType.StaticProperty1
            "You got a static property"

    module Scope2 = 
        open FSharp.HelloWorld.Nested.Nested.Nested.Namespace2

        check "cwpmew905"
            HelloWorldType.StaticProperty1
            "You got a static property"

module Int32 = 
    check "vlkrrevpojvr0"
        FSharp.HelloWorld.HelloWorldTypeWithStaticInt32Parameter<1>.StaticProperty1
        "You got a static property"

    check "vlkrrevpojvr0"
        FSharp.HelloWorld.HelloWorldTypeWithStaticInt32Parameter<1>.NestedType.StaticProperty1
        "You got a static property"

    check "vlkrrevpojvr0"
        FSharp.HelloWorld.HelloWorldTypeWithStaticInt32Parameter<Count=1>.NestedType.StaticProperty1
        "You got a static property"

    check "vlkrrevpojvr0"
        FSharp.HelloWorld.HelloWorldTypeWithStaticOptionalInt32Parameter<1>.NestedType.StaticProperty1
        "You got a static property"

    check "vlkrrevpojvr0"
        FSharp.HelloWorld.HelloWorldTypeWithStaticOptionalInt32Parameter.NestedType.StaticProperty1
        "You got a static property"

    check "vlkrrevpojvr0"
        FSharp.HelloWorld.HelloWorldTypeWithStaticOptionalInt32Parameter<Count=1>.NestedType.StaticProperty1
        "You got a static property"

    type T = FSharp.HelloWorld.HelloWorldTypeWithStaticInt32Parameter<1>

    let testBinaryCompatFunction (x:FSharp.HelloWorld.HelloWorldTypeWithStaticInt32Parameter<1>) = 
       printfn "do not inline me"
       printfn "do not inline me"
       printfn "do not inline me"
       printfn "do not inline me"
       printfn "do not inline me"
       x


    check "vlkrrevpojvr0"
        T.NestedType.StaticProperty1
        "You got a static property"

    check "vlkrrevpojvr0"
        T.NestedType.StaticProperty2
        42


    check "vlkrrevpojvr2"
        FSharp.HelloWorld.HelloWorldTypeWithStaticInt32Parameter<2>.StaticProperty1
        "You got a static property"

    check "vlkrrevpojvr3"
        FSharp.HelloWorld.HelloWorldTypeWithStaticInt32Parameter<2>.StaticProperty2
        42

    check "vlkrrevpojvr4"
        FSharp.HelloWorld.HelloWorldTypeWithStaticInt32Parameter<3>.StaticProperty3
        43

    check "vfhdjkwrywwt1"
        FSharp.HelloWorld.HelloWorldTypeWithStaticInt32Parameter<const 2>.StaticProperty1
        "You got a static property"

    check "vfhdjkwrywwt2"
        FSharp.HelloWorld.HelloWorldTypeWithStaticInt32Parameter<const (2 ||| 2)>.StaticProperty1
        "You got a static property"

    check "vfhdjkwrywwt3"
        FSharp.HelloWorld.HelloWorldTypeWithStaticDayOfWeekParameter<const System.DayOfWeek.Monday >.StaticProperty1
        "You got a static property"

    check "vfhdjkwrywwt4"
        FSharp.HelloWorld.HelloWorldTypeWithStaticDayOfWeekParameter<const (System.DayOfWeek.Monday ||| System.DayOfWeek.Tuesday) >.StaticProperty1
        "You got a static property"

    [<Literal>]
    let two = 2

    check "vfhdjkwrywwt"
        FSharp.HelloWorld.HelloWorldTypeWithStaticInt32Parameter<const two>.StaticProperty1
        "You got a static property"

    type TConst2 = FSharp.HelloWorld.HelloWorldTypeWithStaticInt32Parameter<const 2>
    type TConst2b = FSharp.HelloWorld.HelloWorldTypeWithStaticInt32Parameter<const (2)>
    type TConst2c = FSharp.HelloWorld.HelloWorldTypeWithStaticInt32Parameter<const ((2))>
    type TConst2d = FSharp.HelloWorld.HelloWorldTypeWithStaticInt32Parameter<const (two)>
    type TConst2e = FSharp.HelloWorld.HelloWorldTypeWithStaticInt32Parameter<const ((two))>
    type TConstTwo = FSharp.HelloWorld.HelloWorldTypeWithStaticInt32Parameter<const two>

    check "bdferjhtwer78tr278"
        TConst2.StaticProperty1
        "You got a static property"
    
    check "bdferjhtwer78tr279"
        TConstTwo.StaticProperty2
        42

    type TConstMinus2 = FSharp.HelloWorld.HelloWorldTypeWithStaticInt32Parameter<const -2>

    check "bdferjhtwer78tr278aa"
        TConstMinus2.StaticPropertyMinus2
        38
    
    module Nested =
        [<Literal>]
        let two = 2

    type TConstNestedTwo = FSharp.HelloWorld.HelloWorldTypeWithStaticInt32Parameter<const Nested.two>
    check "fh873463csdkop9"
        TConstNestedTwo.StaticProperty1
        "You got a static property"

    check "fh873463csdkop9kk"
        FSharp.HelloWorld.HelloWorldTypeWithStaticInt32Parameter<const Nested.two>.StaticProperty1
        "You got a static property"



    type T1 = FSharp.HelloWorld.HelloWorldTypeWithStaticInt32Parameter<1>
    type T2 = FSharp.HelloWorld.HelloWorldTypeWithStaticInt32Parameter<2>
    type T3 = FSharp.HelloWorld.HelloWorldTypeWithStaticInt32Parameter<3>

    check "vlkrrevpojvr51" T1.StaticProperty1 "You got a static property"
    check "vlkrrevpojvr52" T2.StaticProperty1 "You got a static property"
    check "vlkrrevpojvr53" T3.StaticProperty1 "You got a static property"


    check "vlkrrevpojvr5" T2.StaticProperty2 42
    check "vlkrrevpojvr5" T3.StaticProperty2 42

    check "vlkrrevpojvr6" T3.StaticProperty3 43

module SByte = 
    check "vlkrrevpojvr1"
        FSharp.HelloWorld.HelloWorldTypeWithStaticSByteParameter<1y>.StaticProperty1
        "You got a static property"


module Int16 = 
    check "vlkrrevpojvr1"
        FSharp.HelloWorld.HelloWorldTypeWithStaticInt16Parameter<1s>.StaticProperty1
        "You got a static property"

module Int64 = 
    check "vlkrrevpojvr1"
        FSharp.HelloWorld.HelloWorldTypeWithStaticInt64Parameter<1L>.StaticProperty1
        "You got a static property"

module Byte = 
    check "vlkrrevpojvr1"
        FSharp.HelloWorld.HelloWorldTypeWithStaticByteParameter<1uy>.StaticProperty1
        "You got a static property"


module UInt16 = 
    check "vlkrrevpojvr1"
        FSharp.HelloWorld.HelloWorldTypeWithStaticUInt16Parameter<1us>.StaticProperty1
        "You got a static property"

module UInt32 = 
    check "vlkrrevpojvr1"
        FSharp.HelloWorld.HelloWorldTypeWithStaticUInt32Parameter<1u>.StaticProperty1
        "You got a static property"

module UInt64 = 
    check "vlkrrevpojvr1"
        FSharp.HelloWorld.HelloWorldTypeWithStaticUInt64Parameter<1UL>.StaticProperty1
        "You got a static property"


module Single = 
    check "vlkrrevpojvr1"
        FSharp.HelloWorld.HelloWorldTypeWithStaticSingleParameter<1.0f>.StaticProperty1
        "You got a static property"

module Double = 
    check "vlkrrevpojvr1"
        FSharp.HelloWorld.HelloWorldTypeWithStaticDoubleParameter<1.0>.StaticProperty1
        "You got a static property"

module Decimal = 
    check "vlkrrevpojvr1"
        FSharp.HelloWorld.HelloWorldTypeWithStaticDecimalParameter<1.0M>.StaticProperty1
        "You got a static property"

    check "vlkrrevpojvr0"
        FSharp.HelloWorld.HelloWorldTypeWithStaticDecimalParameter<1.0M>.NestedType.StaticProperty1
        "You got a static property"

    type T = FSharp.HelloWorld.HelloWorldTypeWithStaticDecimalParameter<1.0M>

    check "vlkrrevpojvr0"
        T.NestedType.StaticProperty1
        "You got a static property"

    check "vlkrrevpojvr0"
        T.NestedType.StaticProperty2
        42



module Char = 
    check "vlkrrevpojvr1"
        FSharp.HelloWorld.HelloWorldTypeWithStaticCharParameter<'A'>.StaticProperty1
        "You got a static property"

    check "vlkrrevpojvr2"
        (FSharp.HelloWorld.HelloWorldType().HelloWorldInstanceMethodWithStaticCharParameter<'\001'>('a'))
        'a'

    let hw = FSharp.HelloWorld.HelloWorldType()
    check "vlkrrevpojvr2"
        (hw.HelloWorldInstanceMethodWithStaticCharParameter<'\001'>('a'))
        'a'

    check "vlkrrevpojvr2b"
        (hw.HelloWorldInstanceMethodWithStaticDecimalParameter<1M>(10M))
        10M

    check "vlkrrevpojvr2c"
        (hw.HelloWorldInstanceMethodWithStaticBoolParameter<true>(true))
        true

    // Check a static method
    check "vlkrrevpojvr2d"
        (FSharp.HelloWorld.HelloWorldType.HelloWorldStaticMethodWithStaticUInt32Parameter<1u>(10u))
        10u

    // Check another static method
    check "vlkrrevpojvr2e"
        (FSharp.HelloWorld.HelloWorldType.HelloWorldStaticMethodWithStaticUInt64Parameter<1UL>(10UL))
        10UL

    // Check an enum type
    check "vlkrrevpojvr2f"
        (hw.HelloWorldInstanceMethodWithStaticDayOfWeekParameter<System.DayOfWeek.Monday>(System.DayOfWeek.Tuesday))
        System.DayOfWeek.Tuesday


    check "vlkrrevpojvr3"
        (FSharp.HelloWorld.HelloWorldTypeWithStaticCharParameter<'A'>().HelloWorldInstanceMethodWithStaticCharParameter<'\001'>('a'))
        'a'

    let x = new FSharp.HelloWorld.HelloWorldTypeWithStaticCharParameter<'A'>()
    check "vlkrrevpojvr2s"
        (x.HelloWorldInstanceMethodWithStaticInt16Parameter<1s>(10s))
        10s

    check "vlkrrevpojvr2L"
        (x.HelloWorldInstanceMethodWithStaticInt64Parameter<1L>(10L))
        10L

    check "vlkrrevpojvr2Lb"
        (x.HelloWorldInstanceMethodWithStaticInt64Parameter<2L>(10L,10L))
        10L

module String = 
    check "vlkrrevpojvr1"
        FSharp.HelloWorld.HelloWorldTypeWithStaticStringParameter<"10000">.StaticProperty1
        "You got a static property"
    check "vlkrrevpojvr1c"
        FSharp.HelloWorld.HelloWorldTypeWithStaticStringParameter<__SOURCE_DIRECTORY__>.StaticProperty1
        "You got a static property"

module Bool = 
    check "vlkrrevpojvr1"
        FSharp.HelloWorld.HelloWorldTypeWithStaticBoolParameter<true>.StaticProperty1
        "You got a static property"

//let a = System.Reflection.Assembly.LoadFile (__SOURCE_DIRECTORY__ + "\\provided.dll")


type TheGeneratedType4 = FSharp.HelloWorldGenerative.TheContainerType<"TheGeneratedType4">


type TheGeneratedType3WithIndexer = FSharp.HelloWorldGenerative.TheContainerType<"TheGeneratedType3WithIndexer">


type TheGeneratedTypeWithEvent = FSharp.HelloWorldGenerative.TheContainerType<"TheGeneratedTypeWithEvent">


type TheGeneratedDelegateType = FSharp.HelloWorldGenerative.TheContainerType<"TheGeneratedDelegateType">


type TheGeneratedStructType = FSharp.HelloWorldGenerative.TheContainerType<"TheGeneratedStructType">



type GeneratedRelatedTypes = FSharp.HelloWorldGenerative.TheContainerType<"GeneratedRelatedTypes">


type TheOuterType = FSharp.HelloWorldGenerative.TheContainerType<"TheOuterType">



type TheGeneratedEnumType = FSharp.HelloWorldGenerative.TheContainerType<"TheGeneratedEnumType">


module BasicGenerativeTest2Public = 

    // Check creating an instance of a generated type
    let v0a : TheGeneratedType4 = TheGeneratedType4()
    let v0b = TheGeneratedType4()
    let v0c = new TheGeneratedType4()

    // Check creating an instance of a nested generated type
    let v0na : TheOuterType.TheNestedGeneratedType = TheOuterType.TheNestedGeneratedType()
    let v0nb = TheOuterType.TheNestedGeneratedType()
    let v0nc = new TheOuterType.TheNestedGeneratedType()

    // Check use of instance property getter
    let v1 : TheGeneratedType4 = TheGeneratedType4()
    check "cwlecew01" v1.Prop1 1

    // Check use of instance property getter on a nested generated type
    let v1n = TheOuterType.TheNestedGeneratedType()
    check "cwlecew01" v1.Prop1 1

    // Check use of indexer item property getter
    let v2  = TheGeneratedType3WithIndexer()
    check "cwlecew02" v2.[1] "1"

    // Check use of indexer item property getter on a nested generated type
    let v2n = TheOuterType.TheNestedGeneratedTypeWithIndexer()
    check "cwlecew02" v2n.[1] "1"

    // Check use of indexer item property setter
    let v2b  = new TheGeneratedType3WithIndexer()
    check "cwlecew03" v2b.[2] "2"
    v2b.[2] <- "two"
    v2b.set_Item(2,"two")

    check "cwlecew04" v2b.[2] "two"

    // Check use of indexer item property setter on a nested generated type
    let v2bn  = new TheOuterType.TheNestedGeneratedTypeWithIndexer()
    check "cwlecew03" v2bn.[2] "2"
    v2bn.[2] <- "two"
    check "cwlecew04" v2bn.[2] "two"

    // Check use of provided event 
    let eventObj  = TheGeneratedTypeWithEvent()
    let count = ref 0
    eventObj.MyEvent.Add (fun _ -> incr count)
    check "cwlecew02" count.Value 0
    eventObj.Trigger()
    check "cwlecew02" count.Value 1

    // Check use of overloaded operator
    let obj1 = TheGeneratedType4()
    let obj2 : TheGeneratedType4 = TheGeneratedType4()
    check "cwlecew05" (obj1 + obj2) obj1 // the operator returns the first value

    // Check use of overloaded operator on a nested generated type
    let obj1n = TheOuterType.TheNestedGeneratedType()
    let obj2n : TheOuterType.TheNestedGeneratedType = TheOuterType.TheNestedGeneratedType()
    check "cwlecew05" (obj1n + obj2n) obj1n // the operator returns the first value

    // Check that subtyping amongst provided generated types is detected
    let testFunctionThatTakesGeneratedInterfaceTypeAsParameter (x: GeneratedRelatedTypes.TheGeneratedInterfaceType) = (x,x)
    let testFunctionThatTakesGeneratedInterfaceSubTypeAsParameter (x: GeneratedRelatedTypes.TheGeneratedInterfaceSubType) = (x,x)

    let _ = testFunctionThatTakesGeneratedInterfaceTypeAsParameter (GeneratedRelatedTypes.TheGeneratedClassTypeWhichImplementsTheGeneratedInterfaceType())
    let _ = testFunctionThatTakesGeneratedInterfaceSubTypeAsParameter (GeneratedRelatedTypes.TheGeneratedClassTypeWhichImplementsTheGeneratedInterfaceSubType())
    let _ = testFunctionThatTakesGeneratedInterfaceTypeAsParameter (GeneratedRelatedTypes.TheGeneratedClassTypeWhichImplementsTheGeneratedInterfaceSubType())

    // Check creation of provided delegate type
    let delegateObj1 = new TheGeneratedDelegateType(fun x -> x * x)

    // Check invocation of provided delegate type
    check "cwlecew02r398y" (delegateObj1.Invoke 3) 9

    // Check creation of provided nested delegate type
    let delegateObj2 = new TheOuterType.TheNestedGeneratedDelegateType(fun x -> x*x*x)

    // Check invocation of provided nested delegate type
    check "cwlecew02r3cwe" (delegateObj2.Invoke 3) 27


    // Check object expression creating instance of provided generated interface type
    // Check object expression creating instance of provided nested generated interface type
    let interfaceObj1  = 
        { new GeneratedRelatedTypes.TheGeneratedInterfaceType with 
                    member __.InterfaceMethod0() = 3
                    member __.InterfaceMethod1 arg1 = arg1 + 4
                    member __.InterfaceMethod2 (arg1, arg2) = arg1+arg2
                    member __.InterfaceProperty1 = 4 }
                
    check "cwlecew061" (interfaceObj1.InterfaceMethod0()) 3
    check "cwlecew062" (interfaceObj1.InterfaceMethod1 6) 10
    check "cwlecew063" (interfaceObj1.InterfaceMethod2 (3,4)) 7
    check "cwlecew064" (interfaceObj1.InterfaceProperty1) 4

    let interfaceObj2  = 
        { new TheOuterType.TheNestedGeneratedInterfaceType with 
                    member __.InterfaceMethod0() = 3
                    member __.InterfaceMethod1 arg1 = arg1 + 4
                    member __.InterfaceMethod2 (arg1, arg2) = arg1+arg2
                    member __.InterfaceProperty1 = 4 }
                
    check "cwlecew065" (interfaceObj2.InterfaceMethod0()) 3
    check "cwlecew066" (interfaceObj2.InterfaceMethod1 6) 10
    check "cwlecew067" (interfaceObj2.InterfaceMethod2 (3,4)) 7
    check "cwlecew068" (interfaceObj2.InterfaceProperty1) 4

    let interfaceObj1e  = 
        let ev = new Event<System.EventHandler<System.EventArgs>,System.EventArgs>()
        { new GeneratedRelatedTypes.TheGeneratedInterfaceTypeWithEvent with 
                    member __.InterfaceProperty1 = 4
                    [<CLIEvent>]
                    member __.MyEvent = ev.Publish }

    let interfaceObj2e  = 
        let ev = new Event<System.EventHandler<System.EventArgs>,System.EventArgs>()
        { new TheOuterType.TheNestedGeneratedInterfaceTypeWithEvent with 
                    member __.InterfaceProperty1 = 4
                    [<CLIEvent>]
                    member __.MyEvent = ev.Publish }

    type ImplementationOfInterfaceType1()  = 
        interface GeneratedRelatedTypes.TheGeneratedInterfaceType with 
                    member __.InterfaceMethod0() = 3
                    member __.InterfaceMethod1 arg1 = arg1 + 4
                    member __.InterfaceMethod2 (arg1, arg2) = arg1+arg2
                    member __.InterfaceProperty1 = 4 
                
    let interfaceObj3 = (ImplementationOfInterfaceType1()  :> GeneratedRelatedTypes.TheGeneratedInterfaceType)
    check "cwlecew061" (interfaceObj3.InterfaceMethod0()) 3
    check "cwlecew062" (interfaceObj3.InterfaceMethod1 6) 10
    check "cwlecew063" (interfaceObj3.InterfaceMethod2 (3,4)) 7
    check "cwlecew064" (interfaceObj3.InterfaceProperty1) 4


    type ImplementationOfNestedInterfaceType1()  = 
        interface TheOuterType.TheNestedGeneratedInterfaceType with 
                    member __.InterfaceMethod0() = 3
                    member __.InterfaceMethod1 arg1 = arg1 + 4
                    member __.InterfaceMethod2 (arg1, arg2) = arg1+arg2
                    member __.InterfaceProperty1 = 4 
                
    let interfaceObj4 = (ImplementationOfNestedInterfaceType1()  :> TheOuterType.TheNestedGeneratedInterfaceType)
    check "cwlecew061" (interfaceObj4.InterfaceMethod0()) 3
    check "cwlecew062" (interfaceObj4.InterfaceMethod1 6) 10
    check "cwlecew063" (interfaceObj4.InterfaceMethod2 (3,4)) 7
    check "cwlecew064" (interfaceObj4.InterfaceProperty1) 4

    type ImplementationOfInterfaceTypeWithEvent1()  = 
        let ev = new Event<System.EventHandler<System.EventArgs>,System.EventArgs>()
        interface GeneratedRelatedTypes.TheGeneratedInterfaceTypeWithEvent with 
            member __.InterfaceProperty1 = 4 
            [<CLIEvent>]
            member __.MyEvent = ev.Publish 
                
    let ev3 = ImplementationOfInterfaceTypeWithEvent1() 
    type ImplementationOfNestedInterfaceTypeWithEvent1()  = 
        let ev = new Event<System.EventHandler<System.EventArgs>,System.EventArgs>()
        interface TheOuterType.TheNestedGeneratedInterfaceTypeWithEvent with 
            member __.InterfaceProperty1 = 4 
            [<CLIEvent>]
            member __.MyEvent = ev.Publish 
    let ev4 = ImplementationOfInterfaceTypeWithEvent1() 

#if ABSTRACT_CONSTRUCTORS_ARE_PROTECTED_BUG

    let classObj1e  = 
        let ev = new Event<System.EventHandler<System.EventArgs>,System.EventArgs>()
        { new TheContainerType.TheGeneratedAbstractClassWithEvent() with 
                    member __.InterfaceProperty1 = 4
                    [<CLIEvent>]
                    member __.MyEvent = ev.Publish }


    // TODO: Check inheritance of provided generated abstract base class type
    // TODO: Check inheritance of provided nested generated abstract base class type

    // TODO: Check object expression creating instance of provided generated abstract base class type
    // TODO: Check object expression creating instance of provided nested generated abstract base class type

#endif

    // Check access of immutable struct property
    let obj3  = new TheGeneratedStructType(3)
    check "cwlecew06" obj3.StructProperty1 4 // the property adds one to the input

    // Check access of immutable struct property on a nested generated type
    let obj3n  = new TheOuterType.TheNestedGeneratedStructType(4)
    check "cwlecew06b" obj3n.StructProperty1 4 // the property returns the input

    // Check mutation of struct property
    let mutable obj4  = new TheGeneratedStructType(3)
    obj4.StructMutableProperty1 <- 5

// Disabled because of DevDiv:274092
// Re-enabled then it is fixed.
// Note: we call it "mutable property" when it is really a field
//    check "cwlecew07a" obj4.StructMutableProperty1 5

    // Check mutation of struct property on a nested generated type
    let mutable obj4n  = new TheOuterType.TheNestedGeneratedStructType(3)
    obj4n.StructMutableProperty1 <- 5
// Disabled because of DevDiv:274092
// Re-enabled then it is fixed.
// Note: we call it "mutable property" when it is really a field
//    check "cwlecew07b" obj4n.StructMutableProperty1 5

    // Check getting an enumeration value
    let enumValue1  = TheGeneratedEnumType.Item0
    check "cwlecew07c" (int enumValue1) 0

    // Check that you can do bitwise operations on enumeration value
    let enumValue2  = TheGeneratedEnumType.Item1 ||| TheGeneratedEnumType.Item2
    check "cwlecew07d" (int enumValue2) (1 ||| 2)

    // Check a simple quotation of a piece of code that manipulates a generated type by taking the quotation and checking the type of the quotation tree
    check "cwpmew901"
        <@ (fun (x : TheGeneratedType4) -> x) @>.Type
        typeof<TheGeneratedType4 -> TheGeneratedType4>

    // Check a simple quotation of a pirce of code that manipulates a nested generated type by taking the quotation and checking the type of the quotation tree
    check "cwpmew901"
        <@ (fun (x : TheOuterType.TheNestedGeneratedType) -> x) @>.Type
        typeof<TheOuterType.TheNestedGeneratedType -> TheOuterType.TheNestedGeneratedType>

module BasicGenerativeTest2 = 
    
    type internal TheGeneratedTypeJ = FSharp.HelloWorldGenerativeWithStaticParameter.TheContainerType<"J">

    let internal v1 : TheGeneratedTypeJ = TheGeneratedTypeJ()

    let v2 = v1.Item1


    
    type TheGeneratedTypeK = FSharp.HelloWorldGenerativeWithStaticParameter.TheContainerType<"K">

    let v3 : TheGeneratedTypeK = TheGeneratedTypeK()

    let v4 = v3.Item1

    // Check a simple quotation of a generated type
    check "cwpmew901"
        <@ (fun (x : TheGeneratedTypeJ) -> x) @>.Type
        typeof<TheGeneratedTypeJ -> TheGeneratedTypeJ>

    // Check a simple quotation of a generated type
    check "cwpmew902"
        <@ (fun (x : TheGeneratedTypeK) -> x) @>.Type
        typeof<TheGeneratedTypeK -> TheGeneratedTypeK>

module BasicGenerativeTestWithStaticArgPublic = 

    let v1 : TheGeneratedType4 = TheGeneratedType4()

    let v2 = v1.Prop1

    // Check a simple quotation of a generated type
    check "cwpmew901"
        <@ (fun (x : TheGeneratedType4) -> x) @>.Type
        typeof<TheGeneratedType4 -> TheGeneratedType4>

module TwoNamespaceGenerativeTestPublic = 
    
    type TheGeneratedType1 = FSharp.HelloWorldGenerativeNamespace1.TheContainerType<"unused">

    
    type TheGeneratedType1WithIndexer = FSharp.HelloWorldGenerativeNamespace2.TheContainerType<"unused">

    let v1 : TheGeneratedType1 = TheGeneratedType1()

    let v2 = v1.Prop1

    let v3 : TheGeneratedType1WithIndexer = TheGeneratedType1WithIndexer()

    let v4 = v3.Prop2

    // Check a simple quotation of a generated type
    check "cwpmew901"
        <@ (fun (x : TheGeneratedType1) -> x) @>.Type
        typeof<TheGeneratedType1 -> TheGeneratedType1>

    // Check a simple quotation of a generated type
    check "cwpmew901"
        <@ (fun (x : TheGeneratedType1WithIndexer) -> x) @>.Type
        typeof<TheGeneratedType1WithIndexer -> TheGeneratedType1WithIndexer>


module TwoNamespaceGenerativeTest = 
    
    type internal TheGeneratedType5 = FSharp.HelloWorldGenerativeInternalNamespace1.TheContainerType<"unused">

    
    type internal TheGeneratedType5WithIndexer = FSharp.HelloWorldGenerativeInternalNamespace2.TheContainerType<"unused">

    let internal v1 : TheGeneratedType5 = TheGeneratedType5()

    let v2 = v1.Prop1

    let internal v3 : TheGeneratedType5WithIndexer = TheGeneratedType5WithIndexer()

    let v4 = v3.Prop2

    // Check a simple quotation of a generated type
    check "cwpmew901"
        <@ (fun (x : TheGeneratedType5) -> x) @>.Type
        typeof<TheGeneratedType5 -> TheGeneratedType5>

    // Check a simple quotation of a generated type
    check "cwpmew901"
        <@ (fun (x : TheGeneratedType5WithIndexer) -> x) @>.Type
        typeof<TheGeneratedType5WithIndexer -> TheGeneratedType5WithIndexer>



module OneProviderContributesTwoFragmentsToSameNamespace = 
    
    type internal TheGeneratedType2 = FSharp.OneProviderContributesTwoFragmentsToSameNamespace.TheContainerType1<"unused">

    
    type internal TheGeneratedType2WithIndexer = FSharp.OneProviderContributesTwoFragmentsToSameNamespace.TheContainerType2<"unused">

    let internal v1 : TheGeneratedType2 = TheGeneratedType2()

    let v2 = v1.Prop1

    let internal v3 : TheGeneratedType2WithIndexer = TheGeneratedType2WithIndexer()

    let v4 = v3.Prop2

    // Check a simple quotation of a generated type
    check "cwpmew901"
        <@ (fun (x : TheGeneratedType2) -> x) @>.Type
        typeof<TheGeneratedType2 -> TheGeneratedType2>

    // Check a simple quotation of a generated type
    check "cwpmew901"
        <@ (fun (x : TheGeneratedType2WithIndexer) -> x) @>.Type
        typeof<TheGeneratedType2WithIndexer -> TheGeneratedType2WithIndexer>



module TwoProvidersContributeToSameNamespace = 
    
    type internal TheGeneratedType3InContainerType1 = FSharp.TwoProvidersContributeToSameNamespace.ContainerType1<"unused">

    
    type internal TheGeneratedType3InContainerType2 = FSharp.TwoProvidersContributeToSameNamespace.ContainerType2<"unused">

    let internal v1 : TheGeneratedType3InContainerType1 = TheGeneratedType3InContainerType1()

    let v2 = v1.Prop1

    let internal v3 : TheGeneratedType3InContainerType2 = TheGeneratedType3InContainerType2()

    let v4 = v3.Prop1

    // Check a simple quotation of a generated type
    check "cwpmew901"
        <@ (fun (x : TheGeneratedType3InContainerType1) -> x) @>.Type
        typeof<TheGeneratedType3InContainerType1 -> TheGeneratedType3InContainerType1>

    // Check a simple quotation of a generated type
    check "cwpmew901"
        <@ (fun (x : TheGeneratedType3InContainerType2) -> x) @>.Type
        typeof<TheGeneratedType3InContainerType2 -> TheGeneratedType3InContainerType2>


module DllImportSMokeTest = 

   type DllImportSmokeTest = FSharp.HelloWorldGenerative.TheContainerType<"DllImportSmokeTest">

   printfn "Testing DllImport: Win32 GetLastError() = %d" (DllImportSmokeTest.GetLastError())
   let f() = 
      printfn "testing...."
      printfn "testing...."
      printfn "testing...."
      printfn "testing...."
      printfn "testing...."
      printfn "testing...."
      printfn "testing...."
      DllImportSmokeTest.GetLastError() // this tests a call to a pinvoke in tailcall position
   printfn "Testing DllImport: Win32 GetLastError() = %d" (f())



(*---------------------------------------------------------------------------
!* wrap up
 *--------------------------------------------------------------------------- *)

let _ = 
  if not failures.IsEmpty then (printfn "Test Failed, failures = %A" failures; exit 1) 

do (stdout.WriteLine "Test Passed"; 
    System.IO.File.WriteAllText("test.ok","ok"); 
    exit 0)


