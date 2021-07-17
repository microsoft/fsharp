module Test

type A() = 
    member x.P = 1

type B() = 
    inherit A()
    member x.P = 1

type B1() = 
    inherit A()
    member x.P = 1

type B2() = 
    inherit A()
    member x.P = 1
    
    
type C() = 
    inherit B()
    member x.P = 1

module BiGenericStaticMemberTests =
    type StaticClass1() = 
        static member M<'b>(c:'b, d:'b) = 1

    let obj = new obj()
    let str = ""

    StaticClass1.M<string>(obj,str)
    StaticClass1.M<string>(str,obj)
    StaticClass1.M<string>(obj,obj)

    StaticClass1.M(obj,str)   // obj :> 'b  --> obj = 'b 
    StaticClass1.M(str,obj)   // string :> 'b  --> string = 'b 

module BiGenericFunctionTests =
    let M<'b>(c:'b, d:'b) = 1

    let obj = new obj()
    let str = ""

    M<string>(obj,str)
    M<string>(str,obj)
    M<string>(obj,obj)

    M(obj,str)   // obj :> 'b  --> obj = 'b 
    M(str,obj)   // string :> 'b  --> string = 'b 


module NoSubsumptionOnApplication = 
    (fun (x:A) -> 1)  (new B())  // now permitted
    (fun (x:System.ValueType) -> 1)  1  // coercion on application!


module NoSubsumptionForLists = 
    type StaticClass2() = 
        static member DisplayControls(controls: A list) = ()
        
    let v21 = [ new B(); new A() ]
    let v22 = [ new B1(); new B2() ]
    let v2b = [ new C(); new B() ]
    let v2c : A list = [ new B(); new C() ]
    StaticClass2.DisplayControls [ new B1(); new B2() ]

    let v2 = [ new A(); new B() ]
    let v2b' = [ new B(); new C() ]
    let v2c' : A list = [ (new B() :> A); new C() ]

    let controls = [ new B(); new C() ]
    StaticClass2.DisplayControls controls // bang

    // Q: how about on sequence expressions?
    let controls2 = [ yield (new B())
                      yield (new C()) ]
    StaticClass2.DisplayControls controls2

    // Q: how about on sequence expressions?
    let controls3 = [ yield! [new B()]
                      yield! [new C()] ]
    StaticClass2.DisplayControls controls3 // bang

    let controls4 = if true then new B() else new C()
    StaticClass2.DisplayControls [controls4] // allowed

    // Q: how about on matches? allowed
    let controls5 = match 1 with 1 -> new B() | _ -> new C()
    StaticClass2.DisplayControls [controls5] // allowed


    // Q. subsumption on 'let v = expr'? Allowed
    let x76 : A = new B()

module NoSubsumptionForLists2 = 

    let d1 = new B() ::  new A() :: [] 
    let d2 = new A() ::  new B() :: [] 

    let v2a = [ new B(); new A() ] // would not work! 
        // cf. let v2b = [ (new B() :> A); new A() ] 

    type Data = Data of A * A
    let data (x,y) = Data (x,y)
    let pAA = (new A(),new A()) 
    let pBB = (new B(),new B())
    let pAB = (new A(),new B())
    let pBA = (new B(),new A())
    pBB |> Data   // not permitted (questionable)
    pAB |> Data   // not permitted (questionable)
    pBA |> Data   // not permitted (questionable)
    pBB |> data   // permitted
    pAB |> data   // permitted
    pBA |> data   // permitted

module BiGenericMethodsInGenericClassTests = 
    type C<'a>() =
        static member M(x:'a) = 1
        static member M2<'b>(x:'b) = 1
        static member M3<'b>(x:'b,y:'b) = 1
        
        static member OM3<'b>(x:'b,y:'b) = 1
        
        static member OM3<'b>(x:'b,y:int) = 1

    let obj = new obj()
    let str = ""

    C<obj>.M3("a",obj)  // this is not permitted since 'b is inferred to be "string". Fair enough
    C<obj>.M3(obj,"a") // now permitted

    C<obj>.OM3("a",obj)  // this is not permitted since 'b is inferred to be "string". Fair enough




module BadNumberOfGenericParameters = 

    type C<'a>() = 
        member x.P = 1
        member x.M1() = 2
        member x.M2(y:int) = 2
        member x.M3(y:int,z:int) = 2
        member x.M4<'b>(y:'a,z:'b) = 2
        member x.M5<'b,'c>(y:'a,z:'b) = 2
        
        static member SP = 1
        static member SM1() = 2
        static member SM2(y:int) = 2
        static member SM3(y:int,z:int) = 2
        static member SM4<'b>(y:'a,z:'b) = 2
        static member SM5<'b,'c>(y:'a,z:'b) = 2
    let _ = C<int,int>.SP
    let _ = C<int,int>.M1
    let _ = C.SM1<int>() // expect error here
    let _ = C.SM2<int>(3) // expect error here
    let _ = C.SM3<int>(3,4) // expect error here
    let _ = C.SM3<int>(y=3,x=4) // expect error here
    let _ = C.SM4<int,int>(y=3,z=4) // expect error here
    let _ = C.SM5<int>(y=3,z=4) // expect error here

    let c = C<int>()
    let _ = c.M1<int>() // expect error here
    let _ = c.M2<int>(3) // expect error here
    let _ = c.M3<int>(3,4) // expect error here
    let _ = c.M3<int>(y=3,x=4) // expect error here
    let _ = c.M4<int,int>(y=3,z=4) // expect error here
    let _ = c.M5<int>(y=3,z=4) // expect error here

    module PositiveTests = 
        let _ = C.SM4<int>(y=3,z=4) // expect NO NO NO NO NO error here
        let _ = C.SM4(y=3,z=4) // expect NO NO NO NO NO error here
        let _ = C.SM5<int,int>(y=3,z=4) // expect NO NO NO NO NO error here
        let _ = c.M4<int>(y=3,z=4) // expect NO NO NO NO NO error here
        let _ = c.M4(y=3,z=4) // expect NO NO NO NO NO error here
        let _ = c.M5<int,int>(y=3,z=4) // expect NO NO NO NO NO error here
        
    
module ParamArgs =  begin
    type C2() = class
        static member M( fmt:string, [<System.ParamArray>] args : int[]) = System.String.Format(fmt,args)
    end
    let () = C2.M("{0}",box 1)            // expect error
    let () = C2.M("{0},{1}",box 1,box 2)  // expect error
    let _ = C2.M("{0},{1}",box 1,box 2)  // expect error
    type C3() = class
        static member M( fmt:string, [<System.ParamArray>] args : string[]) = System.String.Format(fmt,args)
    end
    let () = C3.M("{0}",box 1)            // expect error
    let () = C3.M("{0},{1}",box 1,box 2)  // expect error
    let _ = C3.M("{0},{1}",box 1,box 2)  // expect error
end

module BadAttribute =  begin

  [<Class>]
  let T1 = 1

  [<Struct>]
  let T2 = 1

  [<DefaultValue>]
  let T3 = 1

  [<Interface>]
  let T4 = 1

  [<Sealed>]
  let T5 = 1

  [<Measure>]
  let T6 = 1

  [<MeasureAnnotatedAbbreviation>]
  let T7 = 1

  [<Literal>]
  type C1() = class end

  [<EntryPoint>]
  type C2() = class end

  [<DefaultAugmentation(false)>]
  let T8 = 1

  [<ReferenceEquality>]
  let T9 = 1

  [<StructuralEquality>]
  let T10 = 1

  [<OptionalArgument>]
  let T11 = 1

  [<RequiresExplicitTypeArguments>]
  type C3() = class end

  [<RequiresExplicitTypeArguments>]
  module M = begin end

  [<GeneralizableValue>]
  module M2 = begin end

  [<GeneralizableValue>]
  type C4() = class end

  
  type C5 = 
    class 
      [<GeneralizableValue>] val x : int 
    end

  
  type C6() = class end

  [<AutoSerializable(false)>]
  let T12 = 1

  [<FSharpInterfaceDataVersion(3,4,5)>]
  let T13 = 1

  [<Unverifiable>]
  type C7() = class end


  [<NoDynamicInvocation>]
  type C8() = class end

  [<RequireQualifiedAccess>]
  let T14 = 1

  [<AutoOpen()>]  
  let T15 = 1

end

module BadArgName =  begin

  let psi1 = new System.Diagnostics.ProcessStartInfo(FileName = "test", arguments = "testarg")

  let psi2 = new System.Diagnostics.ProcessStartInfo(FileName = "test", Argument = "testarg")
end

module DuplicateArgNames =  begin

  let f1 x x = ()
  let f2 x _ x = ()
  type C() = 
     member x.M(x:int)  = x + x
end


module BogusAttributeTarget =  begin

  [<someTotallyBogusAttributeTarget : System.ObsoleteAttribute("asdf")>]
  let x = 5
end

module BogusUseOfCLIMutable =  begin

  [<CLIMutable>]
  type BadClass() = member x.P = 1

  [<CLIMutable>]
  type BadUnion = A | B

  [<CLIMutable>]
  type BadInterface = interface end

  [<CLIMutable>]
  type BadClass2 = class end

  [<CLIMutable>]
  type BadEnum = | A = 1 | B = 2

  [<CLIMutable>]
  type BadDelegate = delegate of int * int -> int

  [<CLIMutable>]
  type BadStruct = struct val x : int end

  [<CLIMutable>]
  type BadStruct2(x:int)  = struct member v.X = x end

  [<CLIMutable>]
  type Good1 = { x : int; y : int }
  let good1 = { x = 1; y = 2 }

  [<CLIMutable>]
  type Good2 = { x : int }
  let good2 = { x = 1 }

end


let ss1 = System.String
let ss2 = System.Guid

type ClassWithOneConstructor(x:int) = member x.P = 1

let ss3 = ClassWithOneConstructor


module OverloadedTypeNamesBothHaveConstructors = 
    type OverloadedClassName<'T>(x:int) = 
        new (y:string) = OverloadedClassName<'T>(1)
        member __.P = x
        static member S() = 3


    type OverloadedClassName<'T1,'T2>(x:int) = 
        new (y:string) = OverloadedClassName<'T1,'T2>(1)
        member __.P = x
        static member S() = 3

    let t3 = 3 |> OverloadedClassName // expected error - multiple types exist
    let t3s = "3" |> OverloadedClassName // expected error - multiple types exist


module OverloadedTypeNamesSomeConstructors = 
    type OverloadedClassName<'T>(x:int) = 
        new (y:string) = OverloadedClassName<'T>(1)
        member __.P = x
        static member S() = 3


    type OverloadedClassName<'T1,'T2> = 
        member __.P = 1
        static member S() = 3

    let t2 = 3 |> OverloadedClassName<int,int> //  CHANGE IN ERROR MESSAGE IN F# 4.x: Was "Invalid use of a type name", now "No constructors are available for the type 'OverloadedClassName<'a,'b>'"
    let t3 = 3 |> OverloadedClassName // expected error - multiple types exist
    let t2s = "3" |> OverloadedClassName<int,int> //  CHANGE IN ERROR MESSAGE IN F# 4.x: Was "Invalid use of a type name", now "No constructors are available for the type 'OverloadedClassName<'a,'b>'"
    let t3s = "3" |> OverloadedClassName // expected error - multiple types exist

module OverloadedTypeNamesNoConstructors = 
    type OverloadedClassName<'T> = 
        static member S(x:int) = 3

    type OverloadedClassName<'T1,'T2> = 
        static member S(x:int) = 3

    let t3 = 3 |> OverloadedClassName.S // expected error - multiple types exist
    let t4 = 3 |> OverloadedClassName.S2 // expected error -  The field, constructor or member 'S2' is not defined






module OverloadedTypeNamesIncludingNonGenericTypeBothHaveConstructors = 

    type OverloadedClassName(x:int) = 
        new (y:string) = OverloadedClassName(1)
        member __.P = x
        static member S() = 3

    type OverloadedClassName<'T>(x:int) = 
        new (y:string) = OverloadedClassName<'T>(1)
        member __.P = x
        static member S() = 3


    type OverloadedClassName<'T1,'T2>(x:int) = 
        new (y:string) = OverloadedClassName<'T1,'T2>(1)
        member __.P = x
        static member S() = 3

    let t3 = 3 |> OverloadedClassName // expected error - multiple types exist
    let t3s = "3" |> OverloadedClassName // expected error - multiple types exist


module OverloadedTypeNamesIncludingNonGenericTypeSomeConstructors = 
    type OverloadedClassName(x:int) = 
        new (y:string) = OverloadedClassName(1)
        member __.P = x
        static member S() = 3

    type OverloadedClassName<'T>(x:int) = 
        new (y:string) = OverloadedClassName<'T>(1)
        member __.P = x
        static member S() = 3


    type OverloadedClassName<'T1,'T2> = 
        member __.P = 1
        static member S() = 3

    let t2 = 3 |> OverloadedClassName<int,int> //  CHANGE IN ERROR MESSAGE IN F# 4.x: Was "Invalid use of a type name", now "No constructors are available for the type 'OverloadedClassName<'a,'b>'"
    let t3 = 3 |> OverloadedClassName // NO ERROR EXPECTED
    let t2s = "3" |> OverloadedClassName<int,int> //  CHANGE IN ERROR MESSAGE IN F# 4.x: Was "Invalid use of a type name", now "No constructors are available for the type 'OverloadedClassName<'a,'b>'"
    let t3s = "3" |> OverloadedClassName // expected error - multiple types exist

module OverloadedTypeNamesIncludingNonGenericTypeNoConstructors = 
    type OverloadedClassName = 
        static member S(x:int) = 3

    type OverloadedClassName<'T> = 
        static member S(x:int) = 3

    type OverloadedClassName<'T1,'T2> = 
        static member S(x:int) = 3

    let t3 = 3 |> OverloadedClassName.S // NO ERROR EXPECTED
    let t4 = 3 |> OverloadedClassName.S2 // expected error -  The field, constructor or member 'S2' is not defined

