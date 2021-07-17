#if TESTS_AS_APP
module Core_extconstraint
#endif


let failures = ref []

let reportFailure (s : string) = 
    stderr.Write" NO: "
    stderr.WriteLine s
    failures := !failures @ [s]


let check s e r = 
    if r = e then stdout.WriteLine (s + ": YES") 
    else 
        printf "\n***** %s: FAIL, expected %A, got %A\n" s r e
        reportFailure s

let test s b =      
    if b then ()
    else (stderr.WriteLine ("failure: " + s); 
        reportFailure s)


type MyType =
    | MyType of int

/// Extending a .NET primitive type with new operator
module DotNetPrimtiveWithNewOperator = 
    type System.Int32 with
        static member (++)(a: int, b: int) = a 
    do check "jfs9dlfdhQ" 1 (1 ++ 2)

/// Extending a .NET primitive type with new instance of an operator
module DotNetPrimtiveExistingOperator1 = 
    type System.Double with
        static member (+)(a: int, b: float) = float a + b
    do check "jfs9dlfdhA" 3.0 (2 + 1.0)

/// Extending a .NET primitive type with new instance of an operator
module DotNetPrimtiveExistingOperator2 = 
    type System.Double with
        static member (+)(a: float, b: int) = a + float b
    do check "jfs9dlfdh0" 3.0 (1.0 + 2)
    do check "jfs9dlfdh1" 3.0 (1.0 + 2.0)

/// Extending a .NET primitive type with new instance of an operator
module DotNetPrimtiveExistingOperator3 = 
    type System.Double with
        static member (+)(a: float, b: int) = a + float b
        static member (+)(a: int, b: float) = float a + b
    do check "jfs9dlfdh2" 3 (2 + 1)
    do check "jfs9dlfdh3" 3.0 (2 + 1.0)
    do check "jfs9dlfdh4" 3.0 (1.0 + 2)
    do check "jfs9dlfdh5" 3.0 (1.0 + 2.0)

/// Extension members take precedence in most-recently-opened order
module ExtensionPrecedence1 = 
    [<AutoOpen>]
    module M1 = 
        type System.Int32 with
            static member (+)(a: int, b: float) = float a + b

    [<AutoOpen>]
    module M2 = 
        type System.Double with
            static member (+)(a: int, b: float) = float a + b + 4.0
    do check "jfs9dlfdh6" 7.0 (2 + 1.0) // note we call the second one

/// Extension members take precedence in most-recently-opened order
/// 
/// Like the previous test but we change the declarations a little
module ExtensionPrecedence2 = 
    [<AutoOpen>]
    module M1 = 
        type System.Int32 with
            static member (+)(a: int, b: float) = float a + b

    [<AutoOpen>]
    module M2 = 
        type System.Int32 with
            static member (+)(a: int, b: float) = float a + b + 4.0

    do check "jfs9dlfdh6" 7.0 (2 + 1.0) // note we call the second one

/// Extension members take precedence in most-recently-opened order
/// 
/// Like the previous test but we change the declarations a little
module ExtensionPrecedence3 = 
    module Extensions1 = 
        type System.Int32 with
            static member (+)(a: int, b: float) = float a + b

    module Extensions2 = 
        type System.Int32 with
            static member (+)(a: int, b: float) = float a + b + 4.0
    open Extensions1
    open Extensions2
    do check "jfs9dlfdh7" 7.0 (2 + 1.0) // note we call the second one

/// Extension members take precedence in most-recently-opened order
/// 
/// Like the previous test but we change the declarations a little
module ExtensionPrecedence4 = 
    module Extensions2 = 
        type System.Int32 with
            static member (+)(a: int, b: float) = float a + b + 4.0

    module Extensions1 = 
        type System.Int32 with
            static member (+)(a: int, b: float) = float a + b

    open Extensions1
    open Extensions2
    do check "jfs9dlfdh8" 7.0 (2 + 1.0) // note we call the Extensions2 one

/// Extending a .NET primitive type with new operator
module DotNetPrimtiveWithAmbiguousNewOperator = 
    [<AutoOpen>]
    module Extensions = 
        type System.Int32 with
            static member (++)(a: int, b: int) = a 

    do check "jfs9dlfdhsx" 1 (1 ++ 2)

    [<AutoOpen>]
    module Extensions2 = 
        type System.Int32 with
            static member (++)(a: int, b: string) = a 

    do check "jfs9dlfdhsx1" 1 (1 ++ "2")

    let f (x: string) = 1 ++ x
    
    do check "jfs9dlfdhsx2" 1 (f "2")

/// Extending a .NET primitive type with new _internal_ operator
module DotNetPrimtiveWithInternalOperator1 = 
    type System.Int32 with
        static member internal (++)(a: int, b: int) = a 

    let result = 1 ++ 2 // this is now allowed
    check "vgfmjsdokfj" result 1


/// Extending a .NET primitive type with new _private_ operator where that operator is accessible at point-of-use
module DotNetPrimtiveWithAccessibleOperator2 = 
    type System.Int32 with
        static member private (++)(a: int, b: int) = a 

    let result = 1 ++ 2 // this is now allowed. 
    check "vgfmjsdokfjc" result 1



#if NEGATIVE_TESTS
module DotNetPrimtiveWithInaccessibleOperator = 
    [<AutoOpen>]
    module Extensions = 
        type System.Int32 with
            static member private (++)(a: int, b: int) = a 

    let result = 1 ++ 2 // This should fail to compile because the private member is not accessible from here
#endif


/// Locally extending an F# type with a wide range of standard operators
module FSharpTypeWithExtrinsicOperators = 

    [<AutoOpen>]
    module Extensions = 
        type MyType with
            static member (+)(MyType x, MyType y) = MyType (x + y)
            static member (*)(MyType x, MyType y) = MyType (x * y)
            static member (/)(MyType x, MyType y) = MyType (x / y)
            static member (-)(MyType x, MyType y) = MyType (x - y)
            static member (~-)(MyType x) = MyType (-x)
            static member (|||)(MyType x, MyType y) = MyType (x ||| y)
            static member (&&&)(MyType x, MyType y) = MyType (x &&& y)
            static member (^^^)(MyType x, MyType y) = MyType (x ^^^ y)
            static member Zero = MyType 0
            static member One = MyType 1
            member this.Sign = let (MyType x) = this in sign x
            static member Abs (MyType x) = MyType (abs x)
            static member Sqrt (MyType x) = MyType (int (sqrt (float x)))
            static member Sin (MyType x) = MyType (int (sin (float x)))
            static member Cos (MyType x) = MyType (int (cos (float x)))
            static member Tan (MyType x) = MyType (int (tan (float x)))
            static member DivideByInt (MyType x, n: int) = MyType (x / n)

    let v = MyType 3
    let result1 = v + v  
    do check "fsdnjioa1" (MyType 6) result1
    let result2 = v * v
    do check "fsdnjioa2" (MyType 9) result2
    let result3 = v - v
    do check "fsdnjioa3" (MyType 0) result3
    let result4 = v / v
    do check "fsdnjioa4" (MyType 1) result4
    let result5 = -v
    do check "fsdnjioa5" (MyType -3) result5
    let result6 = v ||| v
    do check "fsdnjioa6" (MyType 3) result6
    let result7 = v &&& v
    do check "fsdnjioa7" (MyType 3) result7
    let result8 = v ^^^ v
    do check "fsdnjioa8" (MyType 0) result8
    let result9 = LanguagePrimitives.GenericZero<MyType>
    do check "fsdnjioa9" (MyType 0) result9
    let result10 = LanguagePrimitives.GenericOne<MyType>
    do check "fsdnjioa10" (MyType 1) result10
    let result11 = sign v
    do check "fsdnjioa11" 1 result11
    let result12 = abs v
    do check "fsdnjioa12" (MyType 3) result12
    let result13 = sqrt v
    do check "fsdnjioa13" (MyType 1) result13
    let result14 = sin v
    do check "fsdnjioa14" (MyType 0) result14
    let result15 = cos v
    do check "fsdnjioa15" (MyType 0) result15
    let result16 = tan v
    do check "fsdnjioa16" (MyType 0) result16
    let result17 = LanguagePrimitives.DivideByInt v 4
    do check "fsdnjioa17" (MyType 0) result17


/// Extending two types with the static member 'Add'
module TwoTypesWithExtensionOfSameName = 

    [<AutoOpen>]
    module Extensions = 
        type System.Int32 with
            static member Add(a: int, b: int) = a 

        type MyType with
            static member Add(MyType x, MyType y) = MyType (x + y)

    let inline addGeneric< ^A when ^A : (static member Add : ^A * ^A -> ^A) > (a,b) : ^A =
        (^A : (static member Add : ^A * ^A -> ^A) (a,b))

    let inline (+++) a b = addGeneric(a,b)

    let inline addGeneric2  (a,b) : ^A when ^A : (static member Add : ^A * ^A -> ^A) =
        (^A : (static member Add : ^A * ^A -> ^A) (a,b))

    let inline (++++) a b = addGeneric2(a,b)


    let f () =
        let v1 = addGeneric (MyType(1), MyType(2))
        let v2 = addGeneric (1,1)
        ()

    let f2 () =
        let v1 = MyType(1) +++ MyType(2)
        let v2 = 1 +++ 1
        1

    let f3 () =
        let v1 = addGeneric2 (MyType(1), MyType(2))
        let v2 = addGeneric2 (1,1)
        ()

    let f4 () =
        let v1 = MyType(1) ++++ MyType(2)
        let v2 = 1 ++++ 1
        ()  
        
    /// The check is that the above code compiles OK

/// Extending a generic type with a property
module ExtendingGenericTypeWithProperty  = 

    type List<'T> with
        member x.Count = x.Length

    let inline count (a : ^A  when ^A : (member Count : int)) =
        (^A : (member Count : int) (a))

    let v0 = [3].Count // sanity check 
    do check "opcjdkfdf" 1 v0
    
    let v3 = count [3]
    do check "opcjdkfdfx" 1 v3

    let v5 = count (ResizeArray [| 3 |])
    do check "opcjdkfdfxa" 1 v5

/// Extending a generic type with a property
/// Extending the .NET array type with a property
module ExtendingGenericTypeAndArrayWithProperty  = 

    type List<'T> with
        member x.Count = x.Length

    type ``[]``<'T> with
        member x.Count = x.Length

    let inline count (a : ^A  when ^A : (member Count : int)) =
        (^A : (member Count : int) (a))

    let v0 = [3].Count // sanity check 
    do check "fdoiodjjs" 1 v0

    let v1 = [|3|].Count // sanity check 
    do check "fdoiodxjxjs" 1 v1
    
    let v3 = count [3]
    do check "fdoios" 1 v3

    let v4 = count [| 3 |]
    do check "fddxjxjs" 1 v4

    let v5 = count (dict [| 1,3 |])
    do check "fdoiosdxs" 1 v5

    let v6 = count (ResizeArray [| 3 |]) // intrinsic from .NET
    do check "fdojxxjs" 1 v6




/// Solving using LINQ extensions
module LinqExtensionMethodsProvideSolutions_Count = 

    open System.Linq

    // Note this looks for a _method_ called `Count` taking a single argument
    // It is _not_ considered the same as a property called `Count`
    let inline countm (a : ^A  when ^A : (member Count : unit -> int)) =
        (^A : (member Count : unit -> int) (a))

    let seqv : seq<int> = Seq.singleton 1
    
    let v0 = seqv.Count() // sanity check 
    do check "fivjijvd" 1 v0

    let v1 = countm seqv
    do check "fivjixjvd" 1 v1



/// Solving using F#-defined extensions
module CSharpStyleExtensionMethodsProvideSolutions_Count2 = 

    // Note this looks for a _method_ called `Count` taking a single argument
    // It is _not_ considered the same as a property called `Count`
    let inline countm (a : ^A  when ^A : (member Count : unit -> int)) =
        (^A : (member Count : unit -> int) (a))

    type TwoIntegers(a:int, b:int) =
        member x.A = a
        member x.B = b

    [<System.Runtime.CompilerServices.Extension>]
    type Extension() =
       [<System.Runtime.CompilerServices.Extension>]
        static member Count(c: TwoIntegers) = 2

    let two = TwoIntegers(2,3)
    let v0 = two.Count() // sanity check 
    do check "fivjijvd33" 2 v0

    let v1 = countm two
    do check "fivjixjvd33" 2 v1

/// Solving using F#-defined extensions
module CSharpStyleExtensionMethodsProvideSolutions_Count3 = 
    open System.Runtime.CompilerServices
    [<Extension>]
    type Ext2() = 
        [<Extension>]
        static member Bleh(a : string) = a.Length

    let inline bleh s = (^a : (member Bleh : unit -> int) s)

    let v = bleh "a"
    do check "cojkicjkc" 1 v

/// Solving using F#-defined extensions (generic)
module CSharpStyleExtensionMethodsProvideSolutions_Count4 = 

    // Note this looks for a _method_ called `Count` taking a single argument
    // It is _not_ considered the same as a property called `Count`
    let inline countm (a : ^A  when ^A : (member Count : unit -> int)) =
        (^A : (member Count : unit -> int) (a))

    type Two<'T1, 'T2>(a: 'T1, b: 'T2) =
        member x.A = a
        member x.B = b

    [<System.Runtime.CompilerServices.Extension>]
    type Extension() =
       [<System.Runtime.CompilerServices.Extension>]
        static member Count(c: Two<'T1, 'T2>) = 2

    let two = Two(2,3)
    let v0 = two.Count() // sanity check 
    do check "fivjijvd33" 2 v0

    let v1 = countm two
    do check "fivjixjvd33" 2 v1

module ExtendingGenericType1 = 
    open System
    type ``[]``<'T> with 
        // The generic type parameter must not be the same as the enclosing, which is unconstrained
        static member inline (+)(a:'T1[], b: 'T2[]) = Array.map2 (+) a b

    let v1 = [|1;2;3|] + [|2;3;4|] //Okay
    do check "kldjfdo1" [|3;5;7|] v1
    let v2 = [|TimeSpan(52342L)|] + [|TimeSpan(3213L)|] //Okay
    do check "kldjfdo2" ([|TimeSpan(52342L + 3213L)|]) v2
    let v3 = [|1m|] + [|2m|] //Okay
    do check "kldjfdo3" ([|3m|]) v3
    let v4 = [|1uy|] + [|2uy|] //Okay
    do check "kldjfdo4" ([|3uy|]) v4
    let v5 = [|1L|] + [|2L|] //Okay
    do check "kldjfdo5" ([|3L|]) v5
    let v6 = [|1I|] + [|2I|] //Okay
    do check "kldjfdo6" ([|3I|]) v6
    let v7 = [| [|1 ; 1|]; [|2|] |] + [| [|2; 2|]; [|3|] |] //Okay
    do check "kldjfdo7" [| [|3 ; 3|]; [|5|] |] v7
    let v8 = [| [| [| [|2|] |] |] |] + [| [| [| [|5|] |] |] |] //Okay
    do check "kldjfdo8" [| [| [| [|7|] |] |] |] v8
    //Compile Errors:
    //let v9 = [|"1"|] + [|"2"|] //error FS0001
    //let v10 = [|1.f|] + [|2.f|] //error FS0001
    //let v11 = [|1.0|] + [|2.0|] //error FS0001

(*---------------------------------------------------------------------------
!* wrap up
 *--------------------------------------------------------------------------- *)

module SystematicTests = 

    // 1-arg extensions to each primitive type
    // 2-arg extensions to each primitive type
    // 2-arg extensions to each primitive type + new sealed type
    // 2-arg extensions to each primitive type + new unsealed type
    // 2-arg extensions to new sealed type + each primitive type
    // 2-arg extensions to new unsealed type + each primitive type
    // 2-arg extensions to new sealed type + new unsealed type
    let inline CallStaticMethod1 (x: ^T) = ((^T): (static member StaticMethod1: ^T -> ^T) (x))
    let inline CallStaticMethod2 (x: ^T, y: ^T) = ((^T): (static member StaticMethod2: ^T * ^T -> ^T) (x, y))
    let inline CallStaticMethod3 (x: ^T, y: ^U) = ((^T or ^U): (static member StaticMethod3: ^T * ^U -> ^V) (x, y))
    let inline CallOverloadedStaticMethod4 (x: ^T, y: ^U) = ((^T or ^U): (static member OverloadedStaticMethod4: ^T * ^U -> ^V) (x, y))
    let inline CallInstanceMethod1 (x: ^T, y: ^T) = ((^T): (member InstanceMethod1: ^T -> ^T) (x, y))
    let inline CallInstanceProperty1 (x: ^T) = ((^T): (member InstanceProperty1: ^T) (x))
    let inline CallStaticProperty1 () = ((^T): (static member StaticProperty1: ^T) ())

    module MethodsOnStructType =

        [<Struct>]
        type R =
            { F : int }
        
            static member op_UnaryNegation (x: R) = { F = x.F + 4 }
            static member StaticMethod1 (x: R) = { F = x.F + 4 }
            static member StaticMethod2 (x: R, y: R) = { F = x.F + y.F + 4 }
            static member op_Addition (x: R, y: R) = { F = x.F + y.F + 4 }
            static member op_Subtraction (x: R, y: R) = { F = x.F + y.F + 5 }
            static member op_Division (x: R, y: R) = { F = x.F + y.F + 6 }
            static member StaticMethod3 (x: R, y: R) = { F = x.F + y.F + 4 }
            static member OverloadedStaticMethod4 (x: R, y: string) = { F = x.F + y.Length + 4 }
            static member OverloadedStaticMethod4 (x: R, y: int) = { F = x.F + y + 4 }
            member x.InstanceMethod1 (y: R) = { F = x.F + y.F + 5 }
            static member StaticProperty1 = { F = 4 }
            member x.InstanceProperty1 = { F = x.F + 4 }
        
        let r3 = { F = 3 }
        let r4 = { F = 4 }
        check "qvwoiwvoi0" (-r3).F 7
        check "qvwoiwvoi1" (CallStaticMethod1 r3).F 7
        check "qvwoiwvoi2" (CallStaticMethod2 (r3, r4)).F 11
        check "qvwoiwvoi2b" ((+) r3 r4).F 11
        check "qvwoiwvoi2c" ((-) r3 r4).F 12
        check "qvwoiwvoi2c" ((/) r3 r4).F 13
        check "qvwoiwvoi3" (CallStaticMethod3 (r3, r4)).F 11
        check "qvwoiwvoi4" (CallOverloadedStaticMethod4 (r3, 4)).F 11
        check "qvwoiwvoi5" (CallOverloadedStaticMethod4 (r3, "four")).F 11
        check "qvwoiwvoi6" (CallInstanceMethod1 (r3, r4)).F 12
        check "qvwoiwvoi7" (CallInstanceProperty1 (r3)).F 7
        check "qvwoiwvoi8" (CallStaticProperty1().F : int32) 4

    module ExtensionsOnStructType =

        [<Struct>]
        type R =
            { F : int }
        
        [<AutoOpen>]
        module Extensions = 
            type R with
                static member op_UnaryNegation (x: R) = { F = x.F + 4 }
                static member StaticMethod1 (x: R) = { F = x.F + 4 }
                static member StaticMethod2 (x: R, y: R) = { F = x.F + y.F + 4 }
                static member op_Addition (x: R, y: R) = { F = x.F + y.F + 4 }
                static member op_Subtraction (x: R, y: R) = { F = x.F + y.F + 5 }
                static member op_Division (x: R, y: R) = { F = x.F + y.F + 6 }
                static member StaticMethod3 (x: R, y: R) = { F = x.F + y.F + 4 }
                static member OverloadedStaticMethod4 (x: R, y: string) = { F = x.F + y.Length + 4 }
                static member OverloadedStaticMethod4 (x: R, y: int) = { F = x.F + y + 4 }
                member x.InstanceMethod1 (y: R) = { F = x.F + y.F + 5 }
                static member StaticProperty1 = { F = 4 }
                member x.InstanceProperty1 = { F = x.F + 4 }
        
        let r3 = { F = 3 }
        let r4 = { F = 4 }
        check "aqvwoiwvoi0" (-r3).F 7
        check "aqvwoiwvoi1" (CallStaticMethod1 r3).F 7
        check "aqvwoiwvoi2" (CallStaticMethod2 (r3, r4)).F 11
        check "aqvwoiwvoi2b" ((+) r3 r4).F 11
        check "aqvwoiwvoi2c" ((-) r3 r4).F 12
        check "aqvwoiwvoi2c" ((/) r3 r4).F 13
        check "aqvwoiwvoi3" (CallStaticMethod3 (r3, r4)).F 11
        check "aqvwoiwvoi4" (CallOverloadedStaticMethod4 (r3, 4)).F 11
        check "aqvwoiwvoi5" (CallOverloadedStaticMethod4 (r3, "four")).F 11
        check "aqvwoiwvoi6" (CallInstanceMethod1 (r3, r4)).F 12 
        check "aqvwoiwvoi7" (CallInstanceProperty1 (r3)).F 7     
        check "aqvwoiwvoi8" (CallStaticProperty1().F : int32) 4


    module MixedOverloadedOperatorMethodsOnStructType =

        [<Struct>]
        type R =
            { F : int }
        
            static member (+) (x: R, y: R) = { F = x.F + y.F + 4 }
            static member (+) (x: R, y: string) = { F = x.F + y.Length + 6 }
            static member (+) (x: R, y: int) = { F = x.F + y + 6 }
            static member (+) (x: string, y: R) = { F = x.Length + y.F + 9 }
            static member (+) (x: int, y: R) = { F = x + y.F + 102 }
        
        let r3 = { F = 3 }
        let r4 = { F = 4 }
        check "qvwoiwvoi2b" ((+) r3 r4).F 11   
        check "qvwoiwvoi2b" ((+) r3 "four").F 13 
        check "qvwoiwvoi2b" ((+) "four" r3).F 16
        check "qvwoiwvoi2b" ((+) r3 4).F 13
        check "qvwoiwvoi2b" ((+) 4 r3).F 109
        // TODO - more operators here

    module MixedOverloadedOperatorExtensionsOnStructType =

        [<Struct>]
        type R =
            { F : int }
        
        [<AutoOpen>]
        module Extensions = 
            type R with
                static member (+) (x: R, y: R) = { F = x.F + y.F + 4 }
                static member (+) (x: R, y: string) = { F = x.F + y.Length + 6 }
                static member (+) (x: R, y: int) = { F = x.F + y + 6 }
                static member (+) (x: string, y: R) = { F = x.Length + y.F + 9 }
                static member (+) (x: int, y: R) = { F = x + y.F + 102 }
        
        let r3 = { F = 3 }
        let r4 = { F = 4 }
        check "qvwoiwvoi2b" ((+) r3 r4).F 11   
        check "qvwoiwvoi2b" ((+) r3 "four").F 13  
        check "qvwoiwvoi2b" ((+) "four" r3).F 16 
        check "qvwoiwvoi2b" ((+) r3 4).F 13 
        check "qvwoiwvoi2b" ((+) 4 r3).F 109 
        //check "qvwoiwvoi2c" ((-) r3 r4).F 12
        //check "qvwoiwvoi2c" ((/) r3 r4).F 13
        // TODO - more operators here


    module ExtensionsToPrimitiveType_Int32 =

        [<AutoOpen>]
        module Extensions = 
            type System.Int32 with
                static member StaticMethod1 (x: int32) = x + 4
                static member StaticMethod2 (x: int32, y: int32) = x + y + 4
                static member StaticMethod3 (x: int32, y: int32) = x + y + 4
                static member OverloadedStaticMethod4 (x: int32, y: string) = x + y.Length + 4
                static member OverloadedStaticMethod4 (x: int32, y: int) = x + y + 4
                member x.InstanceMethod1 (y: int32) = x + y + 5
                static member StaticProperty1 = 4
                member x.InstanceProperty1 = x + 4

        check "2vwoiwvoi1" (CallStaticMethod1 3) 7
        check "2vwoiwvoi2" (CallStaticMethod2 (3, 4)) 11
        check "2vwoiwvoi3" (CallStaticMethod3 (3, 4)) 11
        check "2vwoiwvoi4" (CallOverloadedStaticMethod4 (3, 4)) 11
        check "2vwoiwvoi5" (CallOverloadedStaticMethod4 (3, "four")) 11
        check "2vwoiwvoi6" (CallInstanceMethod1 (3, 4)) 12  
        check "2vwoiwvoi7" (CallInstanceProperty1 (3)) 7    
        check "2vwoiwvoi8" (CallStaticProperty1 () : int32) 4


module Test1 = 

    open System
    type Foo = A | B

    module Extensions = 
        type Foo with
            static member (+) (foo1: Foo, foo2: Foo) = B

    open Extensions

    let result = A + A

    type System.String with
        member this.Foo (x: string) = this + x

module Test2 = 

    open System
    type Foo = A | B

    module Extensions = 
        type Foo with
            static member (+) (foo1: Foo, foo2: Foo) = B

        type Foo with
            static member (+) (foo1: Foo, foo2: string) = B
            static member (+) (foo1: string, foo2: Foo) = B

    open Extensions

    let result = A + A
    let result2 = A + ""
    let result3 = "" + A
    let result4 : string = "" + ""

    type System.String with
        member this.Foo (x: string) = this + x

    type System.String with
        member this.Foo2 x = this + x

    type Bar = Bar of String
        with 
        member this.Foo (x: string) = 
            match this with
            | Bar y -> y + x

    let z = "Bar".Foo("foo")
    let z0 = (Bar "Bar").Foo("foo")

module PositiveTestOfFSharpPlusDesignPattern1 =
    let inline InvokeMap (mapping: ^F) (source: ^I) : ^R =  
        (^I : (static member Map : ^I * ^F ->  ^R) source, mapping)

    // A simulated collection with a'Map' witness
    type Coll<'T>(x: 'T) =
        member _.X = x
        static member Map (source: Coll<'a>, mapping: 'a->'b) : Coll<'b> = new Coll<'b>(mapping source.X)

    let inline AddTwice (x: Coll<'a>) (v: 'a) : Coll<'a> =
        InvokeMap ((+) v) (InvokeMap ((+) v) x)

    check "vrejklervjlr1" (AddTwice (Coll(3)) 2).X 7
    check "vrejklervjlr2" (AddTwice (Coll(3y)) 2y).X 7y

#if TESTS_AS_APP
let RUN() = !failures
#else
let aa =
  match !failures with 
  | [] -> 
      stdout.WriteLine "Test Passed"
      System.IO.File.WriteAllText("test.ok","ok")
      exit 0
  | _ -> 
      stdout.WriteLine "Test Failed"
      exit 1
#endif

