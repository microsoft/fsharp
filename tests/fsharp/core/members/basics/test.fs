// #Conformance #SignatureFiles #Classes #ObjectConstructors #ObjectOrientedTypes #Fields #MemberDefinitions #MethodsAndProperties #Unions #InterfacesAndImplementations #Events #Overloading #Recursion #Regression 
module Global

#nowarn "62"

let failures = ref []

let report_failure (s : string) = 
    stderr.Write" NO: "
    stderr.WriteLine s
    failures := !failures @ [s]

let test (s : string) b = 
    stderr.Write(s)
    if b then stderr.WriteLine " OK"
    else report_failure (s)

let check s b1 b2 = test s (b1 = b2)

//--------------------------------------------------------------
// Test defining a record using object-expression syntax

type RecordType = { a: int; mutable b: int }

let rval = { new RecordType with a = 1 and b = 2 }

do test "fweoew091" (rval.a = 1)
do test "fweoew092" (rval.b = 2)
do rval.b <- 3
do test "fweoew093" (rval.b = 3)

type RecordType2<'a,'b> = { a: 'a; mutable b: 'b }

let rval2 = { new RecordType2<int,int> with a = 1 and b = 2 }

do test "fweoew091" (rval2.a = 1)
do test "fweoew092" (rval2.b = 2)
do rval2.b <- 3
do test "fweoew093" (rval2.b = 3)

let f(x) = 
  { new RecordType2<'a,int> with a = x and b = 2 }

do test "fweoew091" ((f(1)).a = 1)
do test "fweoew092" ((f(1)).b = 2)
do (f(1)).b <- 3
do test "fweoew093" ((f(1)).b = 2)


open System
open System.Collections
open System.Windows.Forms

//-----------------------------------------
// Some simple object-expression tests

let x0 = { new System.Object() with member __.GetHashCode() = 3 }
let x1 = { new System.Windows.Forms.Form() with member __.GetHashCode() = 3 }

//-----------------------------------------
// Test defining an F# class


type ClassType1 =
  class
     inherit System.Object 
     val someField : string

     interface IEnumerable 

     abstract VirtualMethod1: string -> int
     abstract VirtualMethod2: string * string -> int
     abstract VirtualMethod1PostHoc: string -> int
     abstract VirtualMethod2PostHoc: string * string -> int
     default x.VirtualMethod1(s) = 3
     default x.VirtualMethod2(s1,s2) = 3

     new(s: string) = { inherit System.Object(); someField = "abc" }
  end

type ClassType1
  with 
     default x.VirtualMethod1PostHoc(s) = 3
     default x.VirtualMethod2PostHoc(s1,s2) = 3
     new(s1,s2) = { inherit System.Object(); someField = "constructor2" + s1 + s2 }
  end

type ClassType1
  with 
     interface IEnumerable with 
        member x.GetEnumerator() = failwith "no implementation"
     end

  end

let x2 = { new ClassType1("a") with member __.GetHashCode() = 3 }
let x3 = { new ClassType1("a") with member __.VirtualMethod1(s) = 4 }
let x4 = { new ClassType1("a") with 
               member __.VirtualMethod1(s) = 5 
               member __.VirtualMethod2(s1,s2) = s1.Length + s2.Length }



do test "e09wckj2d" (try ignore((x2 :> IEnumerable).GetEnumerator()); false with Failure "no implementation" -> true)

do test "e09wckj2ddwdw" (try ignore(((x2 :> obj) :?> IEnumerable).GetEnumerator()); false with Failure "no implementation" -> true)
do test "e09wckj2defwe" (x2.VirtualMethod1("abc") = 3)
do test "e09wckd2jfew3" (x3.VirtualMethod1("abc") = 4)
do test "e09wckf3q2j" (x4.VirtualMethod1("abc") = 5)
do test "e09wckj321" (x4.VirtualMethod2("abc","d") = 4)


//-----------------------------------------
// Test inheriting from an F# type


type ClassType2 =
  class
     inherit ClassType1 
     val someField2 : string

     override x.VirtualMethod1(s) = 2001
     override x.VirtualMethod2(s1,s2) = s1.Length + s2.Length + String.length x.someField2

     new(s) = { inherit ClassType1(s); someField2 = s }
  end


let x22 = { new ClassType2("a") with member __.GetHashCode() = 3 }
let x32 = { new ClassType2("abc") with member __.VirtualMethod1(s) = 4002 }
let x42 = { new ClassType2("abcd")  with
              member __.VirtualMethod1(s) = 5004 
              member __.VirtualMethod2(s1,s2) = 500 + s1.Length + s2.Length }

do test "e09wckj2ddwdw" (ignore(((x22 :> obj) :?> ClassType1)); true)
do test "e09wckj2ddwdw" (ignore((x22 :> ClassType1)); true)

do test "e09wckjd3" (x22.VirtualMethod1("abc") = 2001)
do test "e09wckjd3" (x32.VirtualMethod1("abc") = 4002)
do test "e09wckjfew" (x42.VirtualMethod1("abc") = 5004)
do test "e09wckjd3" (x22.VirtualMethod2("abcd","dqw") = 8)
do test "e09wckjd3" (x32.VirtualMethod2("abcd","dqw") = 10)



//-----------------------------------------
// Test defining an F# class


module AbstractClassTest = begin

    [<AbstractClass>]
    type ClassType1 =
      class
         inherit System.Object 
         val someField : string

         interface IEnumerable 

         abstract AbstractMethod1: string -> int
         abstract AbstractMethod2: string * string -> int

         new(s: string) = { inherit System.Object(); someField = "abc" }
      end

    type ClassType1
      with 
         interface IEnumerable with 
            member x.GetEnumerator() = failwith "no implementation"
         end

      end

    //let shouldGiveError1 = { new ClassType1("a") with GetHashCode() = 3 }
    //let shouldGiveError2 = { new ClassType1("a") with AbstractMethod1(s) = 4 }
    //let shouldGiveError3a = new ClassType1("a")
    let x4 = { new ClassType1("a") with 
                  member __.AbstractMethod1(s) = 5 
                  member __.AbstractMethod2(s1,s2) = s1.Length + s2.Length }


    do test "e09wckj2d" (try ignore((x2 :> IEnumerable).GetEnumerator()); false with Failure "no implementation" -> true)

    do test "e09wckj2ddwdw" (try ignore(((x2 :> obj) :?> IEnumerable).GetEnumerator()); false with Failure "no implementation" -> true)
    do test "e09wckf3q2j" (x4.AbstractMethod1("abc") = 5)
    do test "e09wckj321" (x4.AbstractMethod2("abc","d") = 4)


    type ClassType2 =
      class
         inherit ClassType1 
         val someField2 : string

         override x.AbstractMethod1(s) = 2001
         override x.AbstractMethod2(s1,s2) = s1.Length + s2.Length + String.length x.someField2

         new(s) = { inherit ClassType1(s); someField2 = s }
      end


    let x22 = { new ClassType2("a") with member __.GetHashCode() = 3 }
    let x32 = { new ClassType2("abc") with member __.AbstractMethod1(s) = 4002 }
    let x42 = { new ClassType2("abcd")  with
                  member __.AbstractMethod1(s) = 5004 
                  member __.AbstractMethod2(s1,s2) = 500 + s1.Length + s2.Length }

    do test "e09wckj2ddwdw" (ignore(((x22 :> obj) :?> ClassType1)); true)
    do test "e09wckj2ddwdw" (ignore((x22 :> ClassType1)); true)

    do test "e09wckjd3" (x22.AbstractMethod1("abc") = 2001)
    do test "e09wckjd3" (x32.AbstractMethod1("abc") = 4002)
    do test "e09wckjfew" (x42.AbstractMethod1("abc") = 5004)
    do test "e09wckjd3" (x22.AbstractMethod2("abcd","dqw") = 8)
    do test "e09wckjd3" (x32.AbstractMethod2("abcd","dqw") = 10)

    type ClassType3 =
      class
         inherit ClassType2
         val someField3 : string

         override x.AbstractMethod1(s) = 2001
         override x.AbstractMethod2(s1,s2) = s1.Length + s2.Length + String.length x.someField2 + x.someField3.Length

         new(s) = { inherit ClassType2(s); someField3 = s }
      end



end

//-----------------------------------------
//-----------------------------------------




// Various rejected syntaxes for constructors:
//   new(s: string) = { base=new Form(); x = "abc" }
//   new ClassType1(s: string) : base() = { x = "abc" }
//   new(s: string) = { inherit Form(); x = "abc" }
//   member ClassType1(s: string) = new { inherit Form(); x = "abc" }
//   member ClassType1(s: string) = { inherit Form(); x = "abc" }
//   initializer(s: string) = { inherit Form(); x = "abc" }
//   new ClassType1(s: string) = { inherit Form(); x = "abc" }

//     new(s: string) = { inherit Form(); x = "abc" }

//     new((s: string), (s2:string)) = { inherit Form(); x = s }


//     abstract AbstractProperty: string
//     abstract AbstractMutableProperty: string with get,set


//     new(s: string) = { new ClassType1 with base=new Object() and x = "abc" }
//     new(s: string) = { new ClassType1 with base=new Form() and x = "abc" }
//     new(s: string) = ((new System.Object()) :?> ClassType1)
 

//-----------------------------------------
// Thorough testing of members for records.

module RecordTypeTest = begin

    type AbstractType = 
      { instanceField: string;
        mutable mutableInstanceField: string;
        instanceArray: string array;
        instanceArray2: string array array;
        mutableInstanceArray: string array;
        mutableInstanceArray2: string array array; 
        recursiveInstance: AbstractType;
        }

    let staticField = "staticField"
    let mutableStaticField = ref "mutableStaticFieldInitialValue"
    let staticArray = [| "staticArrayElement1"; "staticArrayElement2" |]
    let mutableStaticArray = [| "mutableStaticArrayElement1InitialValue"; "mutableStaticArrayElement2InitialValue" |]

    let NewAbstractValue(s) = 
      let rec self =
        { instanceField=s;
          mutableInstanceField=s;
          instanceArray=[| s;s |];
          instanceArray2=[| [| s;s |];[| s;s |] |];
          mutableInstanceArray =[| s;s |];
          mutableInstanceArray2 =[| [| s;s |];[| s;s |] |]; 
          recursiveInstance=self;
        } in 
      self

    type AbstractType 
     with 
      // properties
      override x.ToString() = x.instanceField
      member x.InstanceProperty = x.instanceField + ".InstanceProperty"
      member x.RecursiveInstance = x.recursiveInstance
      member x.RecursiveInstanceMethod() = x.recursiveInstance
      member x.MutableInstanceProperty
        with get() = x.mutableInstanceField
        and set(v:string) = x.mutableInstanceField <- v
        
      member x.InstanceIndexerCount = Array.length x.instanceArray

      member x.InstanceIndexer
         with get(idx) = x.instanceArray.[idx]
      member x.InstanceIndexer2
         with get(idx1,idx2) = x.instanceArray2.[idx1].[idx2]
      member x.InstanceIndexer2Count1 = 2
      member x.InstanceIndexer2Count2 = 2

      member x.MutableInstanceIndexerCount = Array.length x.mutableInstanceArray

      member x.MutableInstanceIndexer
         with get (idx1) = x.mutableInstanceArray.[idx1]
         and  set (idx1) (v:string) = x.mutableInstanceArray.[idx1] <- v

      member x.MutableInstanceIndexer2
         with get (idx1,idx2) = x.mutableInstanceArray2.[idx1].[idx2]
         and  set (idx1,idx2) (v:string) = x.mutableInstanceArray2.[idx1].[idx2] <- v
      member x.MutableInstanceIndexer2Count1 = 2
      member x.MutableInstanceIndexer2Count2 = 2

      static member StaticProperty = staticField
      static member MutableStaticProperty
        with get() = !mutableStaticField
        and  set(v:string) = mutableStaticField := v
        
      static member StaticIndexer
         with get(idx) = staticArray.[idx]
         
      static member StaticIndexerCount = Array.length staticArray

      static member MutableStaticIndexer
         with get(idx:int) = mutableStaticArray.[idx]
         and  set(idx:int) (v:string) = mutableStaticArray.[idx] <- v

      static member MutableStaticIndexerCount = Array.length mutableStaticArray

      // methods
      member x.InstanceMethod(s1:string) = Printf.sprintf "%s.InstanceMethod(%s)" x.instanceField s1
      static member StaticMethod((s1:string),(s2:string)) = Printf.sprintf "AbstractType.StaticMethod(%s,%s)" s1 s2

      // private versions of the above
      member x.PrivateInstanceProperty = x.instanceField + ".InstanceProperty"
      member x.PrivateMutableInstanceProperty
        with get() = x.mutableInstanceField
        and  set(v:string) = x.mutableInstanceField <- v
        
      member x.PrivateInstanceIndexerCount = Array.length x.instanceArray

      member x.PrivateInstanceIndexer
         with get(idx) = x.instanceArray.[idx]
      member x.PrivateInstanceIndexer2
         with get(idx1,idx2) = x.instanceArray2.[idx1].[idx2]
      member x.PrivateInstanceIndexer2Count1 = 2
      member x.PrivateInstanceIndexer2Count2 = 2

      member x.PrivateMutableInstanceIndexerCount = Array.length x.mutableInstanceArray

      member x.PrivateMutableInstanceIndexer
         with get (idx1) = x.mutableInstanceArray.[idx1]
         and  set (idx1) (v:string) = x.mutableInstanceArray.[idx1] <- v

      member x.PrivateMutableInstanceIndexer2
         with get (idx1,idx2) = x.mutableInstanceArray2.[idx1].[idx2]
         and  set (idx1,idx2) (v:string) = x.mutableInstanceArray2.[idx1].[idx2] <- v
      member x.PrivateMutableInstanceIndexer2Count1 = 2
      member x.PrivateMutableInstanceIndexer2Count2 = 2

      static member PrivateStaticProperty = staticField
      static member PrivateMutableStaticProperty
        with get() = !mutableStaticField
        and  set(v:string) = mutableStaticField := v
        
      static member PrivateStaticIndexer
         with get(idx) = staticArray.[idx]
         
      static member PrivateStaticIndexerCount = Array.length staticArray

      static member PrivateMutableStaticIndexer
         with get(idx:int) = mutableStaticArray.[idx]
         and  set(idx:int) (v:string) = mutableStaticArray.[idx] <- v

      static member PrivateMutableStaticIndexerCount = Array.length mutableStaticArray

      // methods
      member x.PrivateInstanceMethod(s1:string) = Printf.sprintf "%s.InstanceMethod(%s)" x.instanceField s1
      static member PrivateStaticMethod((s1:string),(s2:string)) = Printf.sprintf "AbstractType.StaticMethod(%s,%s)" s1 s2


     end



    // Test accesses of static properties, methods
    do System.Console.WriteLine("AbstractType.StaticProperty = {0}", AbstractType.StaticProperty)
    do AbstractType.MutableStaticProperty <- "MutableStaticProperty (mutated!)"
    do System.Console.WriteLine("AbstractType.StaticIndexer(0) = {0}", AbstractType.StaticIndexer(0) )
    do System.Console.WriteLine("AbstractType.StaticMethod(abc,def) = {0}", AbstractType.StaticMethod("abc","def") )
    do System.Console.WriteLine("AbstractType.PrivateStaticProperty = {0}", AbstractType.PrivateStaticProperty )
    do AbstractType.PrivateMutableStaticProperty <- "PrivateMutableStaticProperty (mutated!)"
    do System.Console.WriteLine("AbstractType.PrivateStaticIndexer(0) = {0}", AbstractType.PrivateStaticIndexer(0) )
    do System.Console.WriteLine("AbstractType.PrivateStaticMethod(abc,def) = {0}", AbstractType.PrivateStaticMethod("abc","def") )

    // Torture this poor object
    let xval = NewAbstractValue("abc")

    // Test dynamic rediscovery of type
    do test "e09wckdw" (not ((xval :> obj) :? IEnumerable))
    do test "e09wckdwddw" (not ((xval :> obj) :? string))
    do test "e09dwdw" (not ((xval :> obj) :? list<int>))
    do test "e09dwwd2" ((xval :> obj) :? AbstractType)

    // Test access of instance properties, methods through variables
    do System.Console.WriteLine("abc.instanceField = {0}", xval.instanceField)
    do System.Console.WriteLine("abc.InstanceMethod(def) = {0}", xval.InstanceMethod("def") )
    do System.Console.WriteLine("abc.InstanceProperty = {0}", xval.InstanceProperty )
    do System.Console.WriteLine("abc.InstanceIndexer(0) = {0}", xval.InstanceIndexer(0) )
    do System.Console.WriteLine("abc.InstanceIndexer2(0,1) = {0}", xval.InstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", xval.MutableInstanceProperty )
    do xval.MutableInstanceProperty <- "MutableInstanceProperty (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", xval.MutableInstanceProperty )
    do System.Console.WriteLine("abc.MutableInstanceIndexer = {0}", xval.MutableInstanceIndexer(0) )
    do xval.MutableInstanceIndexer(0) <- "MutableInstanceIndexer(0) (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceIndexer = {0}", xval.MutableInstanceIndexer(0) )
    do System.Console.WriteLine("abc.MutableInstanceIndexer2 = {0}", xval.MutableInstanceIndexer2(0,1) )
    do xval.MutableInstanceIndexer2(0,1) <- "MutableInstanceIndexer2(0,1) (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceIndexer2 = {0}", xval.MutableInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", xval.MutableInstanceProperty )
    do System.Console.WriteLine("abc.PrivateInstanceMethod(def) = {0}", xval.PrivateInstanceMethod("def") )
    do System.Console.WriteLine("abc.PrivateInstanceProperty = {0}", xval.PrivateInstanceProperty )
    do System.Console.WriteLine("abc.PrivateInstanceIndexer(0) = {0}", xval.PrivateInstanceIndexer(0) )
    do System.Console.WriteLine("abc.PrivateInstanceIndexer2(0,1) = {0}", xval.PrivateInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.PrivateMutableInstanceProperty = {0}", xval.PrivateMutableInstanceProperty )
    do xval.PrivateMutableInstanceProperty <- "MutableInstanceProperty (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceProperty = {0}", xval.PrivateMutableInstanceProperty )
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer = {0}", xval.PrivateMutableInstanceIndexer(0) )
    do xval.PrivateMutableInstanceIndexer(0) <- "MutableInstanceIndexer(0) (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer = {0}", xval.PrivateMutableInstanceIndexer(0) )
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer2 = {0}", xval.PrivateMutableInstanceIndexer2(0,1) )
    do xval.PrivateMutableInstanceIndexer2(0,1) <- "MutableInstanceIndexer2(0,1) (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer2 = {0}", xval.PrivateMutableInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc..PrivateMutableInstanceProperty = {0}", xval.PrivateMutableInstanceProperty )

    // repeat all the above through a long-path field lookup
    do System.Console.WriteLine("abc.instanceField = {0}", xval.recursiveInstance.instanceField)
    do System.Console.WriteLine("abc.InstanceMethod(def) = {0}", xval.recursiveInstance.InstanceMethod("def") )
    do System.Console.WriteLine("abc.InstanceProperty = {0}", xval.recursiveInstance.InstanceProperty )
    do System.Console.WriteLine("abc.InstanceIndexer(0) = {0}", xval.recursiveInstance.InstanceIndexer(0) )
    do System.Console.WriteLine("abc.InstanceIndexer2(0,1) = {0}", xval.recursiveInstance.InstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", xval.recursiveInstance.MutableInstanceProperty )
    do xval.recursiveInstance.MutableInstanceProperty <- "MutableInstanceProperty (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", xval.recursiveInstance.MutableInstanceProperty )
    do System.Console.WriteLine("abc.MutableInstanceIndexer = {0}", xval.recursiveInstance.MutableInstanceIndexer(0) )
    do xval.recursiveInstance.MutableInstanceIndexer(0) <- "MutableInstanceIndexer(0) (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceIndexer = {0}", xval.recursiveInstance.MutableInstanceIndexer(0) )
    do System.Console.WriteLine("abc.MutableInstanceIndexer2 = {0}", xval.recursiveInstance.MutableInstanceIndexer2(0,1) )
    do xval.recursiveInstance.MutableInstanceIndexer2(0,1) <- "MutableInstanceIndexer2(0,1) (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceIndexer2 = {0}", xval.recursiveInstance.MutableInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", xval.recursiveInstance.MutableInstanceProperty )
    do System.Console.WriteLine("abc.PrivateInstanceMethod(def) = {0}", xval.recursiveInstance.PrivateInstanceMethod("def") )
    do System.Console.WriteLine("abc.PrivateInstanceProperty = {0}", xval.recursiveInstance.PrivateInstanceProperty )
    do System.Console.WriteLine("abc.PrivateInstanceIndexer(0) = {0}", xval.recursiveInstance.PrivateInstanceIndexer(0) )
    do System.Console.WriteLine("abc.PrivateInstanceIndexer2(0,1) = {0}", xval.recursiveInstance.PrivateInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.PrivateMutableInstanceProperty = {0}", xval.recursiveInstance.PrivateMutableInstanceProperty )
    do xval.recursiveInstance.PrivateMutableInstanceProperty <- "MutableInstanceProperty (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceProperty = {0}", xval.recursiveInstance.PrivateMutableInstanceProperty )
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer = {0}", xval.recursiveInstance.PrivateMutableInstanceIndexer(0) )
    do xval.recursiveInstance.PrivateMutableInstanceIndexer(0) <- "MutableInstanceIndexer(0) (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer = {0}", xval.recursiveInstance.PrivateMutableInstanceIndexer(0) )
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer2 = {0}", xval.recursiveInstance.PrivateMutableInstanceIndexer2(0,1) )
    do xval.recursiveInstance.PrivateMutableInstanceIndexer2(0,1) <- "MutableInstanceIndexer2(0,1) (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer2 = {0}", xval.recursiveInstance.PrivateMutableInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.PrivateMutableInstanceProperty = {0}", xval.recursiveInstance.PrivateMutableInstanceProperty )


    // repeat all the above through a long-path property lookup
    do System.Console.WriteLine("abc.instanceField = {0}", xval.RecursiveInstance.instanceField)
    do System.Console.WriteLine("abc.InstanceMethod(def) = {0}", xval.RecursiveInstance.InstanceMethod("def") )
    do System.Console.WriteLine("abc.InstanceProperty = {0}", xval.RecursiveInstance.InstanceProperty )
    do System.Console.WriteLine("abc.InstanceIndexer(0) = {0}", xval.RecursiveInstance.InstanceIndexer(0) )
    do System.Console.WriteLine("abc.InstanceIndexer2(0,1) = {0}", xval.RecursiveInstance.InstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", xval.RecursiveInstance.MutableInstanceProperty )
    do xval.RecursiveInstance.MutableInstanceProperty <- "MutableInstanceProperty (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", xval.RecursiveInstance.MutableInstanceProperty )
    do System.Console.WriteLine("abc.MutableInstanceIndexer = {0}", xval.RecursiveInstance.MutableInstanceIndexer(0) )
    do xval.RecursiveInstance.MutableInstanceIndexer(0) <- "MutableInstanceIndexer(0) (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceIndexer = {0}", xval.RecursiveInstance.MutableInstanceIndexer(0) )
    do System.Console.WriteLine("abc.MutableInstanceIndexer2 = {0}", xval.RecursiveInstance.MutableInstanceIndexer2(0,1) )
    do xval.RecursiveInstance.MutableInstanceIndexer2(0,1) <- "MutableInstanceIndexer2(0,1) (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceIndexer2 = {0}", xval.RecursiveInstance.MutableInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", xval.RecursiveInstance.MutableInstanceProperty )
    do System.Console.WriteLine("abc.PrivateInstanceMethod(def) = {0}", xval.RecursiveInstance.PrivateInstanceMethod("def") )
    do System.Console.WriteLine("abc.PrivateInstanceProperty = {0}", xval.RecursiveInstance.PrivateInstanceProperty )
    do System.Console.WriteLine("abc.PrivateInstanceIndexer(0) = {0}", xval.RecursiveInstance.PrivateInstanceIndexer(0) )
    do System.Console.WriteLine("abc.PrivateInstanceIndexer2(0,1) = {0}", xval.RecursiveInstance.PrivateInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.PrivateMutableInstanceProperty = {0}", xval.RecursiveInstance.PrivateMutableInstanceProperty )
    do xval.RecursiveInstance.PrivateMutableInstanceProperty <- "MutableInstanceProperty (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceProperty = {0}", xval.RecursiveInstance.PrivateMutableInstanceProperty )
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer = {0}", xval.RecursiveInstance.PrivateMutableInstanceIndexer(0) )
    do xval.RecursiveInstance.PrivateMutableInstanceIndexer(0) <- "MutableInstanceIndexer(0) (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer = {0}", xval.RecursiveInstance.PrivateMutableInstanceIndexer(0) )
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer2 = {0}", xval.RecursiveInstance.PrivateMutableInstanceIndexer2(0,1) )
    do xval.RecursiveInstance.PrivateMutableInstanceIndexer2(0,1) <- "MutableInstanceIndexer2(0,1) (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer2 = {0}", xval.RecursiveInstance.PrivateMutableInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.PrivateMutableInstanceProperty = {0}", xval.RecursiveInstance.PrivateMutableInstanceProperty )

    // repeat all the above through a long-path method lookup
    do System.Console.WriteLine("abc.instanceField = {0}", (xval.RecursiveInstanceMethod()).instanceField)
    do System.Console.WriteLine("abc.InstanceMethod(def) = {0}", (xval.RecursiveInstanceMethod()).InstanceMethod("def") )
    do System.Console.WriteLine("abc.InstanceProperty = {0}", (xval.RecursiveInstanceMethod()).InstanceProperty )
    do System.Console.WriteLine("abc.InstanceIndexer(0) = {0}", (xval.RecursiveInstanceMethod()).InstanceIndexer(0) )
    do System.Console.WriteLine("abc.InstanceIndexer2(0,1) = {0}", (xval.RecursiveInstanceMethod()).InstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", (xval.RecursiveInstanceMethod()).MutableInstanceProperty )
    do (xval.RecursiveInstanceMethod()).MutableInstanceProperty <- "MutableInstanceProperty (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", (xval.RecursiveInstanceMethod()).MutableInstanceProperty )
    do System.Console.WriteLine("abc.MutableInstanceIndexer = {0}", (xval.RecursiveInstanceMethod()).MutableInstanceIndexer(0) )
    do (xval.RecursiveInstanceMethod()).MutableInstanceIndexer(0) <- "MutableInstanceIndexer(0) (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceIndexer = {0}", (xval.RecursiveInstanceMethod()).MutableInstanceIndexer(0) )
    do System.Console.WriteLine("abc.MutableInstanceIndexer2 = {0}", (xval.RecursiveInstanceMethod()).MutableInstanceIndexer2(0,1) )
    do (xval.RecursiveInstanceMethod()).MutableInstanceIndexer2(0,1) <- "MutableInstanceIndexer2(0,1) (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceIndexer2 = {0}", (xval.RecursiveInstanceMethod()).MutableInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", (xval.RecursiveInstanceMethod()).MutableInstanceProperty )
    do System.Console.WriteLine("abc.PrivateInstanceMethod(def) = {0}", (xval.RecursiveInstanceMethod()).PrivateInstanceMethod("def") )
    do System.Console.WriteLine("abc.PrivateInstanceProperty = {0}", (xval.RecursiveInstanceMethod()).PrivateInstanceProperty )
    do System.Console.WriteLine("abc.PrivateInstanceIndexer(0) = {0}", (xval.RecursiveInstanceMethod()).PrivateInstanceIndexer(0) )
    do System.Console.WriteLine("abc.PrivateInstanceIndexer2(0,1) = {0}", (xval.RecursiveInstanceMethod()).PrivateInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.PrivateMutableInstanceProperty = {0}", (xval.RecursiveInstanceMethod()).PrivateMutableInstanceProperty )
    do (xval.RecursiveInstanceMethod()).PrivateMutableInstanceProperty <- "MutableInstanceProperty (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceProperty = {0}", (xval.RecursiveInstanceMethod()).PrivateMutableInstanceProperty )
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer = {0}", (xval.RecursiveInstanceMethod()).PrivateMutableInstanceIndexer(0) )
    do (xval.RecursiveInstanceMethod()).PrivateMutableInstanceIndexer(0) <- "MutableInstanceIndexer(0) (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer = {0}", (xval.RecursiveInstanceMethod()).PrivateMutableInstanceIndexer(0) )
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer2 = {0}", (xval.RecursiveInstanceMethod()).PrivateMutableInstanceIndexer2(0,1) )
    do (xval.RecursiveInstanceMethod()).PrivateMutableInstanceIndexer2(0,1) <- "MutableInstanceIndexer2(0,1) (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer2 = {0}", (xval.RecursiveInstanceMethod()).PrivateMutableInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.PrivateMutableInstanceProperty = {0}", (xval.RecursiveInstanceMethod()).PrivateMutableInstanceProperty )

end

//-----------------------------------------
// Thorough testing of members for records.

module UnionTypeTest = begin

    type AbstractType =  A of AbstractType | B of string

    let staticField = "staticField"
    let mutableStaticField = ref "mutableStaticFieldInitialValue"
    let staticArray = [| "staticArrayElement1"; "staticArrayElement2" |]
    let mutableStaticArray = [| "mutableStaticArrayElement1InitialValue"; "mutableStaticArrayElement2InitialValue" |]

    let NewAbstractValue(s) = B(s)

    type AbstractType 
     with 
      // properties
      override x.ToString() = match x with A _ -> "A" | B(s) -> "B"
      member x.InstanceProperty = "instanceProperty"
      member x.RecursiveInstance = match x with A y -> y | B s -> x
      member x.RecursiveInstanceMethod() =  x.RecursiveInstance
      member x.MutableInstanceProperty
        with get() = x.InstanceProperty
        and  set(v:string) = Printf.printf "called MutableInstanceProperty.set\n"
        
      member x.InstanceIndexerCount = 1

      member x.InstanceIndexer
         with get(idx) = "a"
      member x.InstanceIndexer2
         with get(idx1,idx2) = "a"
      member x.InstanceIndexer2Count1 = 2
      member x.InstanceIndexer2Count2 = 2

      member x.MutableInstanceIndexerCount = 1

      member x.MutableInstanceIndexer
         with get (idx1) = "a"
         and  set (idx1) (v:string) =  Printf.printf "called MutableInstanceIndexer.set\n"

      member x.MutableInstanceIndexer2
         with get (idx1,idx2) = "a"
         and  set (idx1,idx2) (v:string) =  Printf.printf "called MutableInstanceIndexer2.set\n"
      member x.MutableInstanceIndexer2Count1 = 2
      member x.MutableInstanceIndexer2Count2 = 2

      static member StaticProperty = staticField
      static member MutableStaticProperty
        with get() = !mutableStaticField
        and  set(v:string) = mutableStaticField := v
        
      static member StaticIndexer
         with get(idx) = staticArray.[idx]
         
      static member StaticIndexerCount = Array.length staticArray

      static member MutableStaticIndexer
         with get(idx:int) = mutableStaticArray.[idx]
         and  set(idx:int) (v:string) = mutableStaticArray.[idx] <- v

      static member MutableStaticIndexerCount = Array.length mutableStaticArray

      // methods
      member x.InstanceMethod(s1:string) = Printf.sprintf "InstanceMethod(%s)" s1
      static member StaticMethod((s1:string),(s2:string)) = Printf.sprintf "AbstractType.StaticMethod(%s,%s)" s1 s2

      // private versions of the above
      member x.PrivateInstanceProperty = "InstanceProperty"
      member x.PrivateMutableInstanceProperty
        with get() = "a"
        and  set(v:string) = Printf.printf "called mutator\n"
        
      member x.PrivateInstanceIndexerCount = 1

      member x.PrivateInstanceIndexer
         with get(idx) = "b"
      member x.PrivateInstanceIndexer2
         with get(idx1,idx2) = "c"
      member x.PrivateInstanceIndexer2Count1 = 1
      member x.PrivateInstanceIndexer2Count2 = 1

      member x.PrivateMutableInstanceIndexerCount = 3

      member x.PrivateMutableInstanceIndexer
         with get (idx1) = "a"
         and  set (idx1) (v:string) = Printf.printf "called mutator\n"

      member x.PrivateMutableInstanceIndexer2
         with get (idx1,idx2) = "a"
         and  set (idx1,idx2) (v:string) = Printf.printf "called mutator\n"
      member x.PrivateMutableInstanceIndexer2Count1 = 2
      member x.PrivateMutableInstanceIndexer2Count2 = 2

      static member PrivateStaticProperty = staticField
      static member PrivateMutableStaticProperty
        with get() = !mutableStaticField
        and  set(v:string) = mutableStaticField := v
        
      static member PrivateStaticIndexer
         with get(idx) = staticArray.[idx]
         
      static member PrivateStaticIndexerCount = Array.length staticArray

      static member PrivateMutableStaticIndexer
         with get(idx:int) = mutableStaticArray.[idx]
         and  set(idx:int) (v:string) = mutableStaticArray.[idx] <- v

      static member PrivateMutableStaticIndexerCount = Array.length mutableStaticArray

      // methods
      member x.PrivateInstanceMethod(s1:string) = Printf.sprintf "InstanceMethod(%s)"  s1
      static member PrivateStaticMethod((s1:string),(s2:string)) = Printf.sprintf "AbstractType.StaticMethod(%s,%s)" s1 s2

    end



    // Test accesses of static properties, methods
    do System.Console.WriteLine("AbstractType.StaticProperty = {0}", AbstractType.StaticProperty)
    do AbstractType.MutableStaticProperty <- "MutableStaticProperty (mutated!)"
    do System.Console.WriteLine("AbstractType.StaticIndexer(0) = {0}", AbstractType.StaticIndexer(0) )
    do System.Console.WriteLine("AbstractType.StaticMethod(abc,def) = {0}", AbstractType.StaticMethod("abc","def") )
    do System.Console.WriteLine("AbstractType.PrivateStaticProperty = {0}", AbstractType.PrivateStaticProperty )
    do AbstractType.PrivateMutableStaticProperty <- "PrivateMutableStaticProperty (mutated!)"
    do System.Console.WriteLine("AbstractType.PrivateStaticIndexer(0) = {0}", AbstractType.PrivateStaticIndexer(0) )
    do System.Console.WriteLine("AbstractType.PrivateStaticMethod(abc,def) = {0}", AbstractType.PrivateStaticMethod("abc","def") )

    // Torture this poor object
    let xval = NewAbstractValue("abc")

    // Test dynamic rediscovery of type
    do test "e09wckdw" (not ((xval :> obj) :? IEnumerable))
    do test "e09wckdwddw" (not ((xval :> obj) :? string))
    do test "e09dwdw" (not ((xval :> obj) :? list<int>))
    do test "e09dwwd2" ((xval :> obj) :? AbstractType)

    // Test access of instance properties, methods through variables

    do System.Console.WriteLine("abc.InstanceMethod(def) = {0}", xval.InstanceMethod("def") )
    do System.Console.WriteLine("abc.InstanceProperty = {0}", xval.InstanceProperty )
    do System.Console.WriteLine("abc.InstanceIndexer(0) = {0}", xval.InstanceIndexer(0) )
    do System.Console.WriteLine("abc.InstanceIndexer2(0,1) = {0}", xval.InstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", xval.MutableInstanceProperty )
    do xval.MutableInstanceProperty <- "MutableInstanceProperty (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", xval.MutableInstanceProperty )
    do System.Console.WriteLine("abc.MutableInstanceIndexer = {0}", xval.MutableInstanceIndexer(0) )
    do xval.MutableInstanceIndexer(0) <- "MutableInstanceIndexer(0) (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceIndexer = {0}", xval.MutableInstanceIndexer(0) )
    do System.Console.WriteLine("abc.MutableInstanceIndexer2 = {0}", xval.MutableInstanceIndexer2(0,1) )
    do xval.MutableInstanceIndexer2(0,1) <- "MutableInstanceIndexer2(0,1) (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceIndexer2 = {0}", xval.MutableInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", xval.MutableInstanceProperty )
    do System.Console.WriteLine("abc.PrivateInstanceMethod(def) = {0}", xval.PrivateInstanceMethod("def") )
    do System.Console.WriteLine("abc.PrivateInstanceProperty = {0}", xval.PrivateInstanceProperty )
    do System.Console.WriteLine("abc.PrivateInstanceIndexer(0) = {0}", xval.PrivateInstanceIndexer(0) )
    do System.Console.WriteLine("abc.PrivateInstanceIndexer2(0,1) = {0}", xval.PrivateInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.PrivateMutableInstanceProperty = {0}", xval.PrivateMutableInstanceProperty )
    do xval.PrivateMutableInstanceProperty <- "MutableInstanceProperty (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceProperty = {0}", xval.PrivateMutableInstanceProperty )
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer = {0}", xval.PrivateMutableInstanceIndexer(0) )
    do xval.PrivateMutableInstanceIndexer(0) <- "MutableInstanceIndexer(0) (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer = {0}", xval.PrivateMutableInstanceIndexer(0) )
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer2 = {0}", xval.PrivateMutableInstanceIndexer2(0,1) )
    do xval.PrivateMutableInstanceIndexer2(0,1) <- "MutableInstanceIndexer2(0,1) (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer2 = {0}", xval.PrivateMutableInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc..PrivateMutableInstanceProperty = {0}", xval.PrivateMutableInstanceProperty )

    // repeat all the above through a long-path field lookup

    do System.Console.WriteLine("abc.InstanceMethod(def) = {0}", xval.RecursiveInstance.InstanceMethod("def") )
    do System.Console.WriteLine("abc.InstanceProperty = {0}", xval.RecursiveInstance.InstanceProperty )
    do System.Console.WriteLine("abc.InstanceIndexer(0) = {0}", xval.RecursiveInstance.InstanceIndexer(0) )
    do System.Console.WriteLine("abc.InstanceIndexer2(0,1) = {0}", xval.RecursiveInstance.InstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", xval.RecursiveInstance.MutableInstanceProperty )
    do xval.RecursiveInstance.MutableInstanceProperty <- "MutableInstanceProperty (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", xval.RecursiveInstance.MutableInstanceProperty )
    do System.Console.WriteLine("abc.MutableInstanceIndexer = {0}", xval.RecursiveInstance.MutableInstanceIndexer(0) )
    do xval.RecursiveInstance.MutableInstanceIndexer(0) <- "MutableInstanceIndexer(0) (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceIndexer = {0}", xval.RecursiveInstance.MutableInstanceIndexer(0) )
    do System.Console.WriteLine("abc.MutableInstanceIndexer2 = {0}", xval.RecursiveInstance.MutableInstanceIndexer2(0,1) )
    do xval.RecursiveInstance.MutableInstanceIndexer2(0,1) <- "MutableInstanceIndexer2(0,1) (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceIndexer2 = {0}", xval.RecursiveInstance.MutableInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", xval.RecursiveInstance.MutableInstanceProperty )
    do System.Console.WriteLine("abc.PrivateInstanceMethod(def) = {0}", xval.RecursiveInstance.PrivateInstanceMethod("def") )
    do System.Console.WriteLine("abc.PrivateInstanceProperty = {0}", xval.RecursiveInstance.PrivateInstanceProperty )
    do System.Console.WriteLine("abc.PrivateInstanceIndexer(0) = {0}", xval.RecursiveInstance.PrivateInstanceIndexer(0) )
    do System.Console.WriteLine("abc.PrivateInstanceIndexer2(0,1) = {0}", xval.RecursiveInstance.PrivateInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.PrivateMutableInstanceProperty = {0}", xval.RecursiveInstance.PrivateMutableInstanceProperty )
    do xval.RecursiveInstance.PrivateMutableInstanceProperty <- "MutableInstanceProperty (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceProperty = {0}", xval.RecursiveInstance.PrivateMutableInstanceProperty )
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer = {0}", xval.RecursiveInstance.PrivateMutableInstanceIndexer(0) )
    do xval.RecursiveInstance.PrivateMutableInstanceIndexer(0) <- "MutableInstanceIndexer(0) (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer = {0}", xval.RecursiveInstance.PrivateMutableInstanceIndexer(0) )
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer2 = {0}", xval.RecursiveInstance.PrivateMutableInstanceIndexer2(0,1) )
    do xval.RecursiveInstance.PrivateMutableInstanceIndexer2(0,1) <- "MutableInstanceIndexer2(0,1) (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer2 = {0}", xval.RecursiveInstance.PrivateMutableInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.PrivateMutableInstanceProperty = {0}", xval.RecursiveInstance.PrivateMutableInstanceProperty )


    // repeat all the above through a long-path property lookup
    do System.Console.WriteLine("abc.InstanceMethod(def) = {0}", xval.RecursiveInstance.InstanceMethod("def") )
    do System.Console.WriteLine("abc.InstanceProperty = {0}", xval.RecursiveInstance.InstanceProperty )
    do System.Console.WriteLine("abc.InstanceIndexer(0) = {0}", xval.RecursiveInstance.InstanceIndexer(0) )
    do System.Console.WriteLine("abc.InstanceIndexer2(0,1) = {0}", xval.RecursiveInstance.InstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", xval.RecursiveInstance.MutableInstanceProperty )
    do xval.RecursiveInstance.MutableInstanceProperty <- "MutableInstanceProperty (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", xval.RecursiveInstance.MutableInstanceProperty )
    do System.Console.WriteLine("abc.MutableInstanceIndexer = {0}", xval.RecursiveInstance.MutableInstanceIndexer(0) )
    do xval.RecursiveInstance.MutableInstanceIndexer(0) <- "MutableInstanceIndexer(0) (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceIndexer = {0}", xval.RecursiveInstance.MutableInstanceIndexer(0) )
    do System.Console.WriteLine("abc.MutableInstanceIndexer2 = {0}", xval.RecursiveInstance.MutableInstanceIndexer2(0,1) )
    do xval.RecursiveInstance.MutableInstanceIndexer2(0,1) <- "MutableInstanceIndexer2(0,1) (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceIndexer2 = {0}", xval.RecursiveInstance.MutableInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", xval.RecursiveInstance.MutableInstanceProperty )
    do System.Console.WriteLine("abc.PrivateInstanceMethod(def) = {0}", xval.RecursiveInstance.PrivateInstanceMethod("def") )
    do System.Console.WriteLine("abc.PrivateInstanceProperty = {0}", xval.RecursiveInstance.PrivateInstanceProperty )
    do System.Console.WriteLine("abc.PrivateInstanceIndexer(0) = {0}", xval.RecursiveInstance.PrivateInstanceIndexer(0) )
    do System.Console.WriteLine("abc.PrivateInstanceIndexer2(0,1) = {0}", xval.RecursiveInstance.PrivateInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.PrivateMutableInstanceProperty = {0}", xval.RecursiveInstance.PrivateMutableInstanceProperty )
    do xval.RecursiveInstance.PrivateMutableInstanceProperty <- "MutableInstanceProperty (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceProperty = {0}", xval.RecursiveInstance.PrivateMutableInstanceProperty )
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer = {0}", xval.RecursiveInstance.PrivateMutableInstanceIndexer(0) )
    do xval.RecursiveInstance.PrivateMutableInstanceIndexer(0) <- "MutableInstanceIndexer(0) (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer = {0}", xval.RecursiveInstance.PrivateMutableInstanceIndexer(0) )
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer2 = {0}", xval.RecursiveInstance.PrivateMutableInstanceIndexer2(0,1) )
    do xval.RecursiveInstance.PrivateMutableInstanceIndexer2(0,1) <- "MutableInstanceIndexer2(0,1) (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer2 = {0}", xval.RecursiveInstance.PrivateMutableInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.PrivateMutableInstanceProperty = {0}", xval.RecursiveInstance.PrivateMutableInstanceProperty )

    // repeat all the above through a long-path method lookup
    do System.Console.WriteLine("abc.InstanceMethod(def) = {0}", (xval.RecursiveInstanceMethod()).InstanceMethod("def") )
    do System.Console.WriteLine("abc.InstanceProperty = {0}", (xval.RecursiveInstanceMethod()).InstanceProperty )
    do System.Console.WriteLine("abc.InstanceIndexer(0) = {0}", (xval.RecursiveInstanceMethod()).InstanceIndexer(0) )
    do System.Console.WriteLine("abc.InstanceIndexer2(0,1) = {0}", (xval.RecursiveInstanceMethod()).InstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", (xval.RecursiveInstanceMethod()).MutableInstanceProperty )
    do (xval.RecursiveInstanceMethod()).MutableInstanceProperty <- "MutableInstanceProperty (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", (xval.RecursiveInstanceMethod()).MutableInstanceProperty )
    do System.Console.WriteLine("abc.MutableInstanceIndexer = {0}", (xval.RecursiveInstanceMethod()).MutableInstanceIndexer(0) )
    do (xval.RecursiveInstanceMethod()).MutableInstanceIndexer(0) <- "MutableInstanceIndexer(0) (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceIndexer = {0}", (xval.RecursiveInstanceMethod()).MutableInstanceIndexer(0) )
    do System.Console.WriteLine("abc.MutableInstanceIndexer2 = {0}", (xval.RecursiveInstanceMethod()).MutableInstanceIndexer2(0,1) )
    do (xval.RecursiveInstanceMethod()).MutableInstanceIndexer2(0,1) <- "MutableInstanceIndexer2(0,1) (mutated!)"
    do System.Console.WriteLine("abc.MutableInstanceIndexer2 = {0}", (xval.RecursiveInstanceMethod()).MutableInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.MutableInstanceProperty = {0}", (xval.RecursiveInstanceMethod()).MutableInstanceProperty )
    do System.Console.WriteLine("abc.PrivateInstanceMethod(def) = {0}", (xval.RecursiveInstanceMethod()).PrivateInstanceMethod("def") )
    do System.Console.WriteLine("abc.PrivateInstanceProperty = {0}", (xval.RecursiveInstanceMethod()).PrivateInstanceProperty )
    do System.Console.WriteLine("abc.PrivateInstanceIndexer(0) = {0}", (xval.RecursiveInstanceMethod()).PrivateInstanceIndexer(0) )
    do System.Console.WriteLine("abc.PrivateInstanceIndexer2(0,1) = {0}", (xval.RecursiveInstanceMethod()).PrivateInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.PrivateMutableInstanceProperty = {0}", (xval.RecursiveInstanceMethod()).PrivateMutableInstanceProperty )
    do (xval.RecursiveInstanceMethod()).PrivateMutableInstanceProperty <- "MutableInstanceProperty (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceProperty = {0}", (xval.RecursiveInstanceMethod()).PrivateMutableInstanceProperty )
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer = {0}", (xval.RecursiveInstanceMethod()).PrivateMutableInstanceIndexer(0) )
    do (xval.RecursiveInstanceMethod()).PrivateMutableInstanceIndexer(0) <- "MutableInstanceIndexer(0) (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer = {0}", (xval.RecursiveInstanceMethod()).PrivateMutableInstanceIndexer(0) )
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer2 = {0}", (xval.RecursiveInstanceMethod()).PrivateMutableInstanceIndexer2(0,1) )
    do (xval.RecursiveInstanceMethod()).PrivateMutableInstanceIndexer2(0,1) <- "MutableInstanceIndexer2(0,1) (mutated!)"
    do System.Console.WriteLine("abc.PrivateMutableInstanceIndexer2 = {0}", (xval.RecursiveInstanceMethod()).PrivateMutableInstanceIndexer2(0,1) )
    do System.Console.WriteLine("abc.PrivateMutableInstanceProperty = {0}", (xval.RecursiveInstanceMethod()).PrivateMutableInstanceProperty )

end


//---------------------------------------------------------------------
// Test that we can change the default structural comparison semantics


module OverrideIComparableOnClassTest = begin

  type MyStringClass = 
    class 
      val cache: int 
      val v: string 
      interface IComparable with 
         member x.CompareTo(y:obj) = compare x.v (y :?> MyStringClass).v 
      end 
      override x.GetHashCode() = hash(x.v) 
      override x.Equals(y:obj) = (compare x.v (y :?> MyStringClass).v) = 0
      member x.Length = x.cache
      new(s:string) = { inherit Object(); cache=s.Length; v=s }
    end

  let s1 = new MyStringClass("abc")
  let s2 = new MyStringClass("def")
  let s3 = new MyStringClass("abc")
  let s4 = new MyStringClass("abcd")
  do test "cepoiwelk" (s1.Length = 3)
  do test "cepoiwelk" (s2.Length = 3)
  let testc (s1:MyStringClass) (s2:MyStringClass) = 
    test "cepoiwelk1" ((s1 = s2) = (s1.v = s2.v));
    test "cepoiwelk2" ((s1 < s2) = (s1.v < s2.v));
    test "cepoiwelk3" ((s1 > s2) = (s1.v > s2.v));
    test "cepoiwelk4" ((s1 <= s2) = (s1.v <= s2.v));
    test "cepoiwelk5" ((s1 >= s2) = (s1.v >= s2.v));
    test "cepoiwelk6a" ((s1 <> s2) = (s1.v <> s2.v));
    Printf.printf "hash s1 = %d\n" (hash(s1));
    Printf.printf "hash s1.v = %d\n" (hash(s1.v));
    test "cepoiwelk7" (hash(s1) = hash(s1.v));
    test "cepoiwelk8" (hash(s2) = hash(s2.v)) 

  do testc s1 s2
  do testc s1 s3
  do testc s2 s3
  do testc s2 s1 
  do testc s3 s1 
  do testc s3 s2 
  do testc s4 s2 
end

module OverrideIStructuralComparableOnClassTest = begin

  type MyStringClass = 
    class 
      val cache: int 
      val v: string 
      interface IStructuralComparable with 
         member x.CompareTo(y:obj,comp:System.Collections.IComparer) = compare x.v (y :?> MyStringClass).v 
      end 
      interface IStructuralEquatable with 
         member x.GetHashCode(comp:System.Collections.IEqualityComparer) = hash(x.v) 
         member x.Equals(y:obj,comp:System.Collections.IEqualityComparer) = (compare x.v (y :?> MyStringClass).v) = 0
      end 
      member x.Length = x.cache
      new(s:string) = { inherit Object(); cache=s.Length; v=s }
    end

  let s1 = new MyStringClass("abc")
  let s2 = new MyStringClass("def")
  let s3 = new MyStringClass("abc")
  let s4 = new MyStringClass("abcd")
  do test "cepoiwelk" (s1.Length = 3)
  do test "cepoiwelk" (s2.Length = 3)
  let testc (s1:MyStringClass) (s2:MyStringClass) = 
    test "cepoiwelk1" ((s1 = s2) = (s1.v = s2.v));
    test "cepoiwelk2" ((s1 < s2) = (s1.v < s2.v));
    test "cepoiwelk3" ((s1 > s2) = (s1.v > s2.v));
    test "cepoiwelk4" ((s1 <= s2) = (s1.v <= s2.v));
    test "cepoiwelk5" ((s1 >= s2) = (s1.v >= s2.v));
    test "cepoiwelk6a" ((s1 <> s2) = (s1.v <> s2.v));
    Printf.printf "hash s1 = %d\n" (hash(s1));
    Printf.printf "hash s1.v = %d\n" (hash(s1.v));
    test "cepoiwelk7" (hash(s1) = hash(s1.v));
    test "cepoiwelk8" (hash(s2) = hash(s2.v)) 

  do testc s1 s2
  do testc s1 s3
  do testc s2 s3
  do testc s2 s1 
  do testc s3 s1 
  do testc s3 s2 
  do testc s4 s2 
end

module OverrideIComparableOnStructTest = begin

  [<CustomEquality; CustomComparison>]
  type MyStringStruct = 
    struct
      val cache: int 
      val v: string 
      interface IComparable with 
         member x.CompareTo(y:obj) = compare x.v (y :?> MyStringStruct).v 
      end 
      override x.GetHashCode() = hash(x.v) 
      override x.Equals(y:obj) = (compare x.v (y :?> MyStringStruct).v) = 0
      member x.Length = x.cache
      new(s:string) = { cache=s.Length; v=s }
    end

  let s1 = new MyStringStruct("abc")
  let s2 = new MyStringStruct("def")
  let s3 = new MyStringStruct("abc")
  let s4 = new MyStringStruct("abcd")
  do test "cepoiwelk" (s1.Length = 3)
  do test "cepoiwelk" (s2.Length = 3)
  let testc (s1:MyStringStruct) (s2:MyStringStruct) = 
    test "cepoiwelk1" ((s1 = s2) = (s1.v = s2.v));
    test "cepoiwelk2" ((s1 < s2) = (s1.v < s2.v));
    test "cepoiwelk3" ((s1 > s2) = (s1.v > s2.v));
    test "cepoiwelk4" ((s1 <= s2) = (s1.v <= s2.v));
    test "cepoiwelk5" ((s1 >= s2) = (s1.v >= s2.v));
    test "cepoiwelk6a" ((s1 <> s2) = (s1.v <> s2.v));
    Printf.printf "hash s1 = %d\n" (hash(s1));
    Printf.printf "hash s1.v = %d\n" (hash(s1.v));
    test "cepoiwelk7" (hash(s1) = hash(s1.v));
    test "cepoiwelk8" (hash(s2) = hash(s2.v)) 

  do testc s1 s2
  do testc s1 s3
  do testc s2 s3
  do testc s2 s1 
  do testc s3 s1 
  do testc s3 s2 
  do testc s4 s2 
end

module OverrideIStructuralComparableOnStructTest = begin

  [<CustomEquality; CustomComparison>]
  type MyStringStruct = 
    struct
      val cache: int 
      val v: string 
      interface IStructuralComparable with 
         member x.CompareTo(y:obj,comp:System.Collections.IComparer) = compare x.v (y :?> MyStringStruct).v 
      end 
      interface IStructuralEquatable with 
         member x.GetHashCode(comp:System.Collections.IEqualityComparer) = hash(x.v) 
         member x.Equals(y:obj,comp:System.Collections.IEqualityComparer) = (compare x.v (y :?> MyStringStruct).v) = 0
      end 
      member x.Length = x.cache
      new(s:string) = { cache=s.Length; v=s }
    end

  let s1 = new MyStringStruct("abc")
  let s2 = new MyStringStruct("def")
  let s3 = new MyStringStruct("abc")
  let s4 = new MyStringStruct("abcd")
  do test "cepoiwelk" (s1.Length = 3)
  do test "cepoiwelk" (s2.Length = 3)
  let testc (s1:MyStringStruct) (s2:MyStringStruct) = 
    test "cepoiwelk1" ((s1 = s2) = (s1.v = s2.v));
    test "cepoiwelk2" ((s1 < s2) = (s1.v < s2.v));
    test "cepoiwelk3" ((s1 > s2) = (s1.v > s2.v));
    test "cepoiwelk4" ((s1 <= s2) = (s1.v <= s2.v));
    test "cepoiwelk5" ((s1 >= s2) = (s1.v >= s2.v));
    test "cepoiwelk6a" ((s1 <> s2) = (s1.v <> s2.v));
    Printf.printf "hash s1 = %d\n" (hash(s1));
    Printf.printf "hash s1.v = %d\n" (hash(s1.v));
    test "cepoiwelk7" (hash(s1) = hash(s1.v));
    test "cepoiwelk8" (hash(s2) = hash(s2.v)) 

  do testc s1 s2
  do testc s1 s3
  do testc s2 s3
  do testc s2 s1 
  do testc s3 s1 
  do testc s3 s2 
  do testc s4 s2 
end

module OverrideIComparableOnRecordTest = begin

  [<CustomEquality; CustomComparison>]
  type MyStringRecord = { cache: int; v: string  }
    with 
      interface IComparable with 
         member x.CompareTo(y:obj) = compare x.v (y :?> MyStringRecord).v 
      end 
      override x.GetHashCode() = hash(x.v) 
      override x.Equals(y:obj) = (compare x.v (y :?> MyStringRecord).v) = 0
      member x.Length = x.cache
      static member Create(s:string) = { cache=s.Length; v=s }
    end


  let s1 = MyStringRecord.Create("abc")
  let s2 = MyStringRecord.Create("def")
  let s3 = MyStringRecord.Create("abc")
  let s4 = MyStringRecord.Create("abcd")
  do test "recd-cepoiwelk" (s1.Length = 3)
  do test "recd-cepoiwelk" (s2.Length = 3)
  let testc s1 s2 = 
    test "recd-cepoiwelk1" ((s1 = s2) = (s1.v = s2.v));
    test "recd-cepoiwelk2" ((s1 < s2) = (s1.v < s2.v));
    test "recd-cepoiwelk3" ((s1 > s2) = (s1.v > s2.v));
    test "recd-cepoiwelk4" ((s1 <= s2) = (s1.v <= s2.v));
    test "recd-cepoiwelk5" ((s1 >= s2) = (s1.v >= s2.v));
    test "recd-cepoiwelk6b" ((s1 <> s2) = (s1.v <> s2.v));
    Printf.printf "hash s1 = %d\n" (hash(s1));
    Printf.printf "hash s1.v = %d\n" (hash(s1.v));
    test "recd-cepoiwelk7" (hash(s1) = hash(s1.v));
    test "recd-cepoiwelk8" (hash(s2) = hash(s2.v)) 

  do testc s1 s2
  do testc s1 s3
  do testc s2 s3
  do testc s2 s1 
  do testc s3 s1 
  do testc s3 s2 
  do testc s4 s2 
end

module OverrideIStructuralComparableOnRecordTest = begin

  [<CustomEquality; CustomComparison>]
  type MyStringRecord = { cache: int; v: string  }
    with 
      interface IStructuralComparable with 
         member x.CompareTo(y:obj,comp:System.Collections.IComparer) = compare x.v (y :?> MyStringRecord).v 
      end 
      interface IStructuralEquatable with 
         member x.GetHashCode(comp:System.Collections.IEqualityComparer) = hash(x.v) 
         member x.Equals(y:obj,comp:System.Collections.IEqualityComparer) = (compare x.v (y :?> MyStringRecord).v) = 0
      end 
      member x.Length = x.cache
      static member Create(s:string) = { cache=s.Length; v=s }
    end


  let s1 = MyStringRecord.Create("abc")
  let s2 = MyStringRecord.Create("def")
  let s3 = MyStringRecord.Create("abc")
  let s4 = MyStringRecord.Create("abcd")
  do test "recd-cepoiwelk" (s1.Length = 3)
  do test "recd-cepoiwelk" (s2.Length = 3)
  let testc s1 s2 = 
    test "recd-cepoiwelk1" ((s1 = s2) = (s1.v = s2.v));
    test "recd-cepoiwelk2" ((s1 < s2) = (s1.v < s2.v));
    test "recd-cepoiwelk3" ((s1 > s2) = (s1.v > s2.v));
    test "recd-cepoiwelk4" ((s1 <= s2) = (s1.v <= s2.v));
    test "recd-cepoiwelk5" ((s1 >= s2) = (s1.v >= s2.v));
    test "recd-cepoiwelk6b" ((s1 <> s2) = (s1.v <> s2.v));
    Printf.printf "hash s1 = %d\n" (hash(s1));
    Printf.printf "hash s1.v = %d\n" (hash(s1.v));
    test "recd-cepoiwelk7" (hash(s1) = hash(s1.v));
    test "recd-cepoiwelk8" (hash(s2) = hash(s2.v)) 

  do testc s1 s2
  do testc s1 s3
  do testc s2 s3
  do testc s2 s1 
  do testc s3 s1 
  do testc s3 s2 
  do testc s4 s2 
end

module OverrideIComparableOnUnionTest = begin

  [<CustomEquality; CustomComparison>]
  type MyStringUnion = A of int * string | B of int * string
    with 
      member x.Value = match x with A(_,s) | B(_,s) -> s 
      override x.GetHashCode() =
            hash(x.Value)
      override x.Equals(y:obj) =
            x.Value = (y :?> MyStringUnion).Value
      interface IComparable with
        member x.CompareTo(y:obj) = 
            compare x.Value (y :?> MyStringUnion).Value
      end      
      member x.Length = match x with A(n,_) | B(n,_) -> n
      static member Create(s:string) = A(s.Length,s)
    end


  let s1 = MyStringUnion.Create("abc")
  let s2 = MyStringUnion.Create("def")
  let s3 = MyStringUnion.Create("abc")
  let s4 = MyStringUnion.Create("abcd")
  do test "union-cepoiwelk" (s1.Length = 3)
  do test "union-cepoiwelk" (s2.Length = 3)
  let testc (s1:MyStringUnion) (s2:MyStringUnion) = 
    test "union-cepoiwelk1" ((s1 = s2) = (s1.Value = s2.Value));
    test "union-cepoiwelk2" ((s1 < s2) = (s1.Value < s2.Value));
    test "union-cepoiwelk3" ((s1 > s2) = (s1.Value > s2.Value));
    test "union-cepoiwelk4" ((s1 <= s2) = (s1.Value <= s2.Value));
    check "union-cepoiwelk5" (s1 >= s2) (s1.Value >= s2.Value);
    check "union-cepoiwelk5b" (compare s1 s2) (compare s1.Value s2.Value);
    test "union-cepoiwelk6c" ((s1 <> s2) = (s1.Value <> s2.Value));
    Printf.printf "hash s1 = %d\n" (hash(s1));
    Printf.printf "hash s1.Value = %d\n" (hash(s1.Value));
    test "union-cepoiwelk7" (hash(s1) = hash(s1.Value));
    test "union-cepoiwelk8" (hash(s2) = hash(s2.Value)) 

  do testc s1 s2
  do testc s1 s3
  do testc s2 s3
  do testc s2 s1 
  do testc s3 s1 
  do testc s3 s2 
  do testc s4 s2 
end

module TwoCaseUnionTest = 

  [<Struct>]
  type MyUnion = A | B

  do test "union-TwoCaseUnionTest-def" (A <> B)
  do test "union-TwoCaseUnionTest-def" (A = A)
  do test "union-TwoCaseUnionTest-def" (B = B)
  do test "union-TwoCaseUnionTest-def" (B > A)
  do test "union-TwoCaseUnionTest-def" (A < B)
  do test "union-TwoCaseUnionTest-def" (A <= B)
  do test "union-TwoCaseUnionTest-def" (B >= A)


module ToStringOnUnionTest = begin

  type MyUnion = A of string | B

  [<Struct>]
  type MyStructUnion = C of string | D

  let a1 = A "FOO"
  let c1 = C "FOO"

  let expected1 = "A \"FOO\""
  let expected2 = "C \"FOO\""

  do test "union-tostring-def" (a1.ToString() = expected1)
  do test "union-sprintfO-def" ((sprintf "%O" a1) = expected1)
  do test "struct-union-tostring-def" (c1.ToString() = expected2)
  do test "struct-union-sprintfO-def" ((sprintf "%O" c1) = expected2)

end

module ToStringOnUnionTestOverride = begin
  let expected1 = "MyUnion"

  type MyUnion = A of string | B
    with
      override x.ToString() = expected1
  
  let expected2 = "MyStructUnion"

  type MyStructUnion = C of string | D
    with
      override x.ToString() = expected2

  let a1 = A "FOO"
  let c1 = C "FOO"

  do test "union-tostring-with-override" (a1.ToString() = expected1)
  do test "union-sprintfO-with-override" ((sprintf "%O" a1) = expected1)
  do test "struct-union-tostring-with-override" (c1.ToString() = expected2)
  do test "struct-union-sprintfO-with-override" ((sprintf "%O" c1) = expected2)

end

module ToStringOnRecordTest = begin

  type MyRecord = { A: string; B: int }

  [<Struct>]
  type MyStructRecord = { C: string; D: int }

  let a1 = {A = "201"; B = 7}
  let c1 = {C = "20"; D = 17}
  let expected1 = "{ A = \"201\"\n  B = 7 }"
  let expected2 = "{ C = \"20\"\n  D = 17 }"

  do test "record-tostring-def" (a1.ToString() = expected1)
  do test "record-sprintfO-def" ((sprintf "%O" a1) = expected1)
  do test "struct-record-tostring-def" (c1.ToString() = expected2)
  do test "struct-record-sprintfO-def" ((sprintf "%O" c1) = expected2)

end

module ToStringOnRecordTestOverride = begin

  let expected1 = "MyRecord"

  type MyRecord = { A: string; B: int }
    with
      override x.ToString() = expected1
  
  let expected2 = "MyStructRecord"

  [<Struct>]
  type MyStructRecord = { C: string; D: int }
    with
      override x.ToString() = expected2
  
  let a1 = {A = "201"; B = 7}
  let c1 = {C = "20"; D = 17}

  do test "record-tostring-with-override" (a1.ToString() = expected1)
  do test "record-sprintfO-with-override" ((sprintf "%O" a1) = expected1)
  do test "struct-record-tostring-with-override" (c1.ToString() = expected2)
  do test "struct-record-sprintfO-with-override" ((sprintf "%O" c1) = expected2)

end

module OverrideIStructuralComparableOnUnionTest = begin

  [<CustomEquality; CustomComparison>]
  type MyStringUnion = A of int * string | B of int * string
    with 
      member x.Value = match x with A(_,s) | B(_,s) -> s 
      interface IStructuralEquatable with
        member x.GetHashCode(comp:System.Collections.IEqualityComparer) =
            hash(x.Value)
        member x.Equals(y:obj,comp:System.Collections.IEqualityComparer) =
            x.Value = (y :?> MyStringUnion).Value
      end      
      interface IStructuralComparable with
        member x.CompareTo(y:obj,comp:System.Collections.IComparer) = 
            compare x.Value (y :?> MyStringUnion).Value
      end      
      member x.Length = match x with A(n,_) | B(n,_) -> n
      static member Create(s:string) = A(s.Length,s)
    end


  let s1 = MyStringUnion.Create("abc")
  let s2 = MyStringUnion.Create("def")
  let s3 = MyStringUnion.Create("abc")
  let s4 = MyStringUnion.Create("abcd")
  do test "union-cepoiwelk" (s1.Length = 3)
  do test "union-cepoiwelk" (s2.Length = 3)
  let testc (s1:MyStringUnion) (s2:MyStringUnion) = 
    test "union-cepoiwelk1" ((s1 = s2) = (s1.Value = s2.Value));
    //test "union-cepoiwelk2" ((s1 < s2) = (s1.Value < s2.Value));
    //test "union-cepoiwelk3" ((s1 > s2) = (s1.Value > s2.Value));
    //test "union-cepoiwelk4" ((s1 <= s2) = (s1.Value <= s2.Value));
    //check "union-cepoiwelk5" (s1 >= s2) (s1.Value >= s2.Value);
    //check "union-cepoiwelk5b" (compare s1 s2) (compare s1.Value s2.Value);
    test "union-cepoiwelk6c" ((s1 <> s2) = (s1.Value <> s2.Value));
    Printf.printf "hash s1 = %d\n" (hash(s1));
    Printf.printf "hash s1.Value = %d\n" (hash(s1.Value));
    test "union-cepoiwelk7" (hash(s1) = hash(s1.Value));
    test "union-cepoiwelk8" (hash(s2) = hash(s2.Value)) 

  do testc s1 s2
  do testc s1 s3
  do testc s2 s3
  do testc s2 s1 
  do testc s3 s1 
  do testc s3 s2 
  do testc s4 s2 
end

//---------------------------------------------------------------------
// Test we can define an attribute


type DontPressThisButtonAttribute = 
  class 
    inherit System.Attribute
    val v: string 
    member x.Message = x.v
    new(s:string) = { inherit System.Attribute(); v=s }
  end

// BUG:
type [<DontPressThisButton("Please don't press this again")>] button = Buttpon
let  [<DontPressThisButton("Please don't press this again")>] button () = 1

//---------------------------------------------------------------------
// Test we can use base calls

open System.Windows.Forms

type MyCanvas2 = 
  class 
    inherit Form 
    override x.OnPaint(args) =  Printf.printf "OnPaint\n"; base.OnPaint(args)

    new() = { inherit Form(); }
  end

let form2 = new MyCanvas2()
// do form.Paint.Add(...)
// do form.add_Paint(...)
//do form.Activate()
//do Application.Run(form)


//---------------------------------------------------------------------
// Test we can inherit from the Event<'a> type to define our listeners

let (|>) x f = f x


(*
type MyEventListeners<'a>  =
  class
    inherit Event<'a> 

    val mutable listeners2: (Handler<'a>) list 

    member l.Fire(x : 'a) = 
      let arg = new SimpleEventArgs<_>(x) in 
      l.listeners2 |> List.iter (fun d -> ignore(d.Invoke((null:obj),arg))) 

    new() = 
      { inherit Event<'a>(); 
        listeners2 = [] }

  end

*)

(*
type MyCanvas2 = 
  class 
    inherit Form
    member x.Redraw : Event<PaintEventArgs>
    new: unit -> MyCanvas2
  end
*)

(*
type MyCanvas2 = 
  class 
    inherit Form
    val redrawListeners: MyEventListeners<PaintEventArgs>
    member x.Redraw = x.redrawListeners
    override x.OnPaint(args) = x.redrawListeners.Fire(args)

    new() = { inherit Form(); redrawListeners= new MyEventListeners<PaintEventArgs>() }
  end
*)

(*
class MyCanvas2() =
  let l = MyEventListeners<PaintEventArgs>() in 
  object
    inherit Form()
    member x.Redraw = l
    override x.OnPaint(args) = l.Fire(args)
  end

class MyCanvas2 =
  let l = MyEventListeners<PaintEventArgs>() in 
  object
    inherit Form
    member x.Redraw = l
    override x.OnPaint(args) = l.Fire(args)
  end
*)

(*
let form = new MyCanvas2()
// do form.Paint.Add(...)
// do form.add_Paint(...)
do form.Redraw.AddHandler(new Handler(fun _ args -> Printf.printf "OnRedraw\n"))
do form.Redraw.Add(fun args -> Printf.printf "OnRedraw\n")


do form.Activate()
do Application.Run(form)
*)

//do x.add_Redraw

//---------------------------------------------------------------------
// Test we can define an exception

type MyException =
  class 
    inherit System.Exception
    val v: string 
    override x.Message = x.v
    new(s:string) = { inherit System.Exception(); v=s }
  end

let _ = try raise(new MyException("help!")) with :? MyException as me -> Printf.printf "message = %s\n" me.Message

//---------------------------------------------------------------------
// Test we can define and subscribe to an interface

(*
type IMyInterface =
  interface
    abstract MyMethod: string -> int
  end
*)

// type IMyStructuralConstraint = < MyMethod: string -> int >
// 'a :> < MyMethod: string -> int >
// 'a :> IMyStructuralConstraint
// 'a : IMyStructuralConstraint


//---------------------------------------------------------------------
// Test we can define and subscribe to a generic interface

    
//---------------------------------------------------------------------
// Test we can define a struct


(*
type MyStruct =
  struct
    val x: int
    val y: int
  end
*)


//---------------------------------------------------------------------
// Test we can define a generic struct

//---------------------------------------------------------------------
// Test we can define a class with no fields

type NoFieldClass = 
  class 
    new() = { inherit System.Object() }
  end

//---------------------------------------------------------------------
// Test we can implement more than one interface on a class

module MultiInterfaceTest = begin
  type PrivateInterfaceA1 = interface abstract M1 : unit -> unit end
  type PrivateInterfaceA2 = interface abstract M2 : unit -> unit end

  type C1 = 
    class 
      interface PrivateInterfaceA1 with 
        member x.M1() = ()
      end
      interface PrivateInterfaceA2 with 
        member x.M2() = ()
      end
   end
end

module MultiInterfaceTestNameConflict = begin
  type PrivateInterfaceA1 = interface abstract M : unit -> unit end
  type PrivateInterfaceA2 = interface abstract M : unit -> unit end

  type C1 = 
    class 
      interface PrivateInterfaceA1 with 
        member x.M() = ()
      end
      interface PrivateInterfaceA2 with 
        member x.M() = ()
      end
   end
end


module GenericMultiInterfaceTestNameConflict = begin
  type PrivateInterfaceA1<'a> = interface abstract M : 'a -> 'a end
  type PrivateInterfaceA2<'a> = interface abstract M : 'a -> 'a end

  type C1 = 
    class 
      interface PrivateInterfaceA1<string> with 
        member x.M(y) = y
      end
      interface PrivateInterfaceA2<int> with 
        member x.M(y) = y
      end
   end
end


module DeepInterfaceInheritance = begin
  type InterfaceA1 = interface abstract M1 : int -> int end
  type InterfaceA2 = interface inherit InterfaceA1 abstract M2 : int -> int end
  type InterfaceA3 = interface inherit InterfaceA1  inherit InterfaceA2 abstract M3 : int -> int end

  type C1 = 
    class 
      interface InterfaceA2 with 
        member x.M1(y) = y 
        member x.M2(y) = y + y 
      end
      new() = { inherit Object(); }
   end
  type C2 = 
    class 
      interface InterfaceA3 with 
        member x.M1(y) = y
        member x.M2(y) = y + y
        member x.M3(y) = y + y + y
      end
      new() = { inherit Object(); }
   end
  type C3 = 
    class 
      interface InterfaceA2 with 
        member x.M1(y) = y
        member x.M2(y) = y + y
      end
      interface InterfaceA3 with 
        member x.M3(y) = y + y + y
      end
      new() = { inherit Object(); }
   end

  do test "fewopvrej1" (((new C1()) :> InterfaceA1).M1(4) = 4)
  do test "fewopvrej2" (((new C1()) :> InterfaceA2).M2(4) = 8)
   
  do test "fewopvrej3" (((new C2()) :> InterfaceA1).M1(4) = 4)
  do test "fewopvrej4" (((new C2()) :> InterfaceA2).M2(4) = 8)
  do test "fewopvrej5" (((new C2()) :> InterfaceA3).M3(4) = 12)
  do test "fewopvrej6" (((new C2()) :> InterfaceA3).M1(4) = 4)
  do test "fewopvrej7" (((new C2()) :> InterfaceA3).M2(4) = 8)
   
  do test "fewopvrej8" (((new C3()) :> InterfaceA1).M1(4) = 4)
  do test "fewopvrej9" (((new C3()) :> InterfaceA2).M2(4) = 8)
  do test "fewopvrej10" (((new C3()) :> InterfaceA3).M3(4) = 12)
  do test "fewopvrej11" (((new C3()) :> InterfaceA3).M1(4) = 4)
  do test "fewopvrej12" (((new C3()) :> InterfaceA3).M2(4) = 8)
   
end

module DeepGenericInterfaceInheritance = begin
  type InterfaceA1<'a> = interface abstract M1 : 'a -> 'a end
  type InterfaceA2<'b> = interface inherit InterfaceA1<'b list> abstract M2 : 'b * 'b list -> 'b list end
  type InterfaceA3 = interface inherit InterfaceA2<string>  abstract M3 : string list -> string list end

  type C1 = 
    class 
      interface InterfaceA2<int> with 
        member obj.M1(y) = 1::y 
        member obj.M2(x,y) = x::y
      end
      new() = { inherit Object(); }
   end
  type C2 = 
    class 
      interface InterfaceA3 with 
        member obj.M1(y) = "a" :: y
        member obj.M2(x,y) = x :: y
        member obj.M3(y) = "a" :: "b" :: "c" :: y
      end
      new() = { inherit Object(); }
   end
  type C3 = 
    class 
      interface InterfaceA2<string> with 
        member obj.M1(y) = "a" :: y
        member obj.M2(x,y) = x :: y
      end
      interface InterfaceA3 with 
        member obj.M3(y) = "a" :: "b" :: "c" :: y
      end
      new() = { inherit Object(); }
   end

  do test "fewopvrej1" (((new C1()) :> InterfaceA1<int list>).M1([1]) = [1;1])
  do test "fewopvrej2" (((new C1()) :> InterfaceA2<int>).M2(3,[1]) = [3;1])
   
  do test "fewopvrej3" (((new C2()) :> InterfaceA1<string list>).M1(["hi"]) = ["a";"hi"])
  do test "fewopvrej4" (((new C2()) :> InterfaceA2<string>).M1(["hi"]) = ["a";"hi"])
  do test "fewopvrej4" (((new C2()) :> InterfaceA2<string>).M2("a",["hi"]) = ["a";"hi"])
  do test "fewopvrej5" (((new C2()) :> InterfaceA3).M3(["hi"]) = ["a";"b";"c";"hi"])
  do test "fewopvrej6" (((new C2()) :> InterfaceA3).M1(["hi"]) = ["a";"hi"])
  do test "fewopvrej7" (((new C2()) :> InterfaceA3).M2("a",["hi"]) = ["a";"hi"])
   
  do test "fewopvrej8" (((new C3()) :> InterfaceA1<string list>).M1(["hi"]) = ["a";"hi"])
  do test "fewopvrej8" (((new C3()) :> InterfaceA2<string>).M1(["hi"]) = ["a";"hi"])
  do test "fewopvrej9" (((new C3()) :> InterfaceA2<string>).M2("a",["hi"]) = ["a";"hi"])
  do test "fewopvrej10" (((new C3()) :> InterfaceA3).M3(["hi"]) = ["a";"b";"c";"hi"])
  do test "fewopvrej11" (((new C3()) :> InterfaceA3).M1(["hi"]) = ["a";"hi"])
  do test "fewopvrej12" (((new C3()) :> InterfaceA3).M2("a",["hi"]) = ["a";"hi"])
   
end


module PointTest =  struct


  type Point =
   class
     new(x_init) = { inherit System.Object(); x_init = x_init; x = x_init } 
     val x_init : int
     val mutable x : int
     member p.X = p.x
     member p.Offset = p.x - p.x_init
     member p.Move d1 d2 = p.x <- p.x + d1 + d2
     static member TwoArgs d1 d2 = d1 + d2
     static member TwoPatternArgs [d1] [d2] = d1 + d2
     static member ThreeArgs d1 d2 d3 = d1 + d2 + d3
     static member ThreePatternArgs [d1] [d2] [d3] = d1 + d2 + d3
      member p.InstanceTwoArgs d1 d2 = p.x + d1 + d2
      member p.InstanceTwoPatternArgs [d1] [d2] = p.x +  d1 + d2
      member p.InstanceThreeArgs d1 d2 d3 = p.x +  d1 + d2 + d3
      member p.InstanceThreePatternArgs [d1] [d2] [d3] = p.x + d1 + d2 + d3
   end

  type Point_with_no_inherits_clause =
   class
     new x_init = { x_init = x_init; x = x_init } 
     val x_init : int
     val mutable x : int
     member p.X = p.x
     member p.Offset = p.x - p.x_init
     member p.Move d1 d2 = p.x <- p.x + d1 + d2
   end

  do 
    let p = (new Point_with_no_inherits_clause(3)) in 
    let f = p.Move 4 in 
    test "wdfjcdwkj1" (p.X = 3);
    f 4;
    test "wdfjcdwkj2" (p.X = 11);
    f 1;
    test "wdfjcdwkj3" (p.X = 16);
    test "wdfjcdwkj4" (Point.TwoArgs 1 2 = 3);
    test "wdfjcdwkj5" (Point.TwoPatternArgs [1] [2] = 3);
    test "wdfjcdwkj6" (Point.ThreeArgs 1 2 3 = 6);
    test "wdfjcdwkj7" (Point.ThreePatternArgs [1] [2] [3] = 6);
    let p2 = (new Point(16)) in 
    test "wdfjcdwkj4" (p2.InstanceTwoArgs 1 2 = 16 + 3);
    test "wdfjcdwkj5" (p2.InstanceTwoPatternArgs [1] [2] = 16 + 3);
    test "wdfjcdwkj6" (p2.InstanceThreeArgs 1 2 3 = 16 + 6);
    test "wdfjcdwkj7" (p2.InstanceThreePatternArgs [1] [2] [3] = 16 + 6)

end


//---------------------------------------------------------------------
// Test we can implement a debug view

open System.Diagnostics


type 
  [<DebuggerTypeProxy(typeof<MyIntListDebugView>) >]
   MyIntList = MyNil | MyCons of int * MyIntList

and MyIntListDebugView =
   class 
     val v: MyIntList
     new(x) = { v = x }     
     [<DebuggerBrowsable(DebuggerBrowsableState.RootHidden)>] 
     member x.Items = 
        let rec length x acc = match x with MyNil -> acc | MyCons(a,b) -> length b (acc+1) in 
        let len = length x.v 0 in 
        let items = Array.zeroCreate len in 
        let rec go n l = match l with MyNil -> () | MyCons(a,b) -> items.[n] <- a; go (n+1) b in 
        go 0 x.v;
        items
   end


//---------------------------------------------------------------------
// Pattern matching on objects

module PatternMatchTests = begin
    type P = class val x1: int; val x2: string; new(a,b) = {x1=a; x2=b } end
    let p = new P(3,"34")
end


//---------------------------------------------------------------------
// 'then' on construction

module ThenDoTest = begin
    let res = ref 2
    type P = 
      class 
        val x1: int; val x2: string; 
        new(a,b) = {x1=a; x2=(test "ewqonce1" (!res = 2); b) } then res := !res + 1 
      end

    do ignore(new P(3,"5"))
    do test "ewqonce2" (!res = 3)

end

//---------------------------------------------------------------------
// 'then' on construction recursive reference

module ThenDoTest2 = begin
    let res = ref 2
    type P = 
      class 
        val x1: int; val x2: string; 
        new(a,b) as x = 
           { x1= !res; 
             x2=(test "ewqonce3" (!res = 2); b) } 
           then 
              test "ewqonce4" (!res = 2);
              res := !res + 1; 
              test "ewqonce5" (!res = 3);
              test "ewqonce6" (x.x1 = 2) 
      end

    do ignore(new P(3,"5"))
    do test "ewqonce7" (!res = 3)

end

module GenericInterfaceTest = begin

    type Foo<'a> =
      interface 
          abstract fun1 : 'a -> 'a
          abstract fun2 : int -> int
      end


    type Bar<'b> =
      class 
          val store : 'b
          interface Foo<'b> with
            member self.fun1(x) = x
            member self.fun2(x) = 1
          end
          new(x) = { store = x }
      end


    type Bar2<'b> =
      class 
          val store : 'b
          interface Foo<'b> with
            member self.fun1(x:'b) = x
            member self.fun2(x) = 1
          end
          new(x) = { store = x }
      end

    type Bar3<'b> =
      class 
          val store : int
          interface Foo<'b> with
            member self.fun1(x) = x
            member self.fun2(x) = 1
          end
          new(x) = { store = x }
      end

end


//---------------------------------------------------------------------
//
  


module Inventory = begin

    type item = A | B
    type image = A | B

    type ItemDetails = 
      { ItemIndex: item;
        InventoryImage: image;
        Name : string }

    type IInventory = interface
     abstract Contains : item -> bool
     abstract Remove : item -> unit
     abstract GetDetails : item -> ItemDetails  
     abstract Add : ItemDetails -> unit  
     abstract GetTuple : unit -> (item * image * string) list 
     end


    module List = 
        let indexNotFound() = raise (new System.Collections.Generic.KeyNotFoundException("An index satisfying the predicate was not found in the collection"))

        let rec assoc x l = 
            match l with 
            | [] -> indexNotFound()
            | ((h,r)::t) -> if x = h then r else assoc x t
        let rec remove_assoc x l = 
            match l with 
            | [] -> []
            | (((h,_) as p) ::t) -> if x = h then t else p:: remove_assoc x t


    type Inventory = class
      val inv : ItemDetails list ref
      new() = { inv = ref [] }
      interface IInventory with
       member this.Contains i = try (List.assoc i (List.map (fun itd -> (itd.ItemIndex, true)) !this.inv)) with Not_found -> false
       member this.Remove i = this.inv := List.map snd (List.remove_assoc i (List.map (fun itd -> (itd.ItemIndex, itd)) !this.inv))
       member this.GetDetails i = List.assoc i (List.map (fun itd -> (itd.ItemIndex, itd)) !this.inv)
       member this.Add itd = if ((this :> IInventory).Contains (itd.ItemIndex) = false) then this.inv := itd :: !this.inv
       member this.GetTuple() = List.map (fun itd -> (itd.ItemIndex,itd.InventoryImage,itd.Name)) !this.inv
      end
    end

end

//---------------------------------------------------------------------
// Another interface test

module SamplerTest = begin

    type Sampler<'a,'b> = 
      interface
        abstract Sample : 'a -> unit 
        abstract GetStatistic : unit -> 'b
      end
      
    let NewAverage(toFloat) = 
      let count = ref 0 in
      let total = ref 0.0 in 
      { new Sampler<_,float> with
          member __.Sample(x) = incr count; total := !total + toFloat x
          member __.GetStatistic() = !total / float(!count) }


    type Average<'a> = 
      class
        val mutable total : float
        val mutable count : int
        val toFloat : 'a -> float
        new(toFloat) = {total = 0.0; count =0; toFloat = toFloat }
        interface Sampler< 'a,float > with
          member this.Sample(x) = this.count <- this.count + 1; this.total <- this.total + this.toFloat x
          member this.GetStatistic() = this.total / float(this.count)
        end
      end

end


//---------------------------------------------------------------------
// This simple case of forward-reference revealed a bug

type callconv = AA
  with 
        member x.IsInstance         = x.ThisConv 
        member x.ThisConv           = 1
  end 

// Likewise

module OverloadZeroOneTestSoohyoung = begin

    type Point =
        class
            val mutable mx: int
            
            new () = { mx = 0 }
            new (ix) = { mx = ix }
        end

end

//---------------------------------------------------------------------
// Bad error message case 


module Ralf = begin 

  type Matrix = M  | N

  [<AbstractClass>]
  type Distribution = 
      class 
        new () = { }

        abstract member NumberOfDimensions : unit -> int
        abstract member Sample: int -> System.Random -> Matrix
        abstract member Density: Matrix -> float
        abstract member CloneConstant: unit -> Distribution
        abstract member Clone: unit -> Distribution
        abstract member AbsoluteDifference: Distribution -> float

      end

  type Gaussian1D =
      class 
            inherit Distribution
            val PrecisionMean : float
            val Precision : float
            new (precisionMean, precision) = { PrecisionMean = 0.0; Precision = 0.0 }
            override x.NumberOfDimensions() = 1
            override x.Density point = 1.0 
            override x.AbsoluteDifference distribution = 0.0
            override x.Clone() = new Gaussian1D (0.0,0.0) :> Distribution
            override x.CloneConstant() = new Gaussian1D (x.PrecisionMean,x.Precision) :> Distribution
            override x.Sample numberOfSamples random = failwith "" // new Matrix (numberOfSamples,x.NumberOfDimensions)
      end

end


//---------------------------------------------------------------------
// A random bunch of overloaded operator tests

module MultipleOverloadedOperatorTests = begin

    let f1 (x:DateTime) (y:TimeSpan) : DateTime = x - y
    let g1 (x:DateTime) (y:DateTime) : TimeSpan = x - y
    // Return type is not sufficient:
    //let f2 (x:DateTime) y : DateTime = x - y
    //let g2 (x:DateTime) y : TimeSpan = x - y
    // Just argument types are also sufficient:
    let f3 (x:DateTime) (y:TimeSpan)  = x - y
    let g3 (x:DateTime) (y:DateTime)  = x - y

end


//---------------------------------------------------------------------
// A random bunch of overloaded operator tests

module OverloadedOperatorTests = begin


    let x = []
    do printf "len = %d\n" x.Length
    let c = ("abc").[2]

    let arr = [| 1 |]
    do printf "len = %d\n" x.Length
    let elem = arr.[0]
    let _ = arr.[0] <- 3

    let SCAL = new System.Collections.ArrayList()
    let _ = SCAL.Add(3)
    let _ = SCAL.[0]
    let _ = SCAL.[0] <- box 4

    let SCGL = new System.Collections.Generic.List<int>()
    let _ = SCGL.Add(3)
    let _ = SCGL.[0]
    let _ = SCGL.[0] <- 3

    let f (x: 'a) = 
      let SCGL = new System.Collections.Generic.List<'a>() in 
      let _ = SCGL.Add(x) in 
      let _ = SCGL.[0] in 
      let _ = SCGL.[0] <- x in 
      ()

    // check we have generalized 
    do f 1
    do f "3"

    let SCGD = new System.Collections.Generic.Dictionary<string,float>()
    let _ = SCGD.Add("hello",3.0)
    let _ = SCGD.["hello"]

    let g (k: 'a) (v:'b)= 
      let SCGD = new System.Collections.Generic.Dictionary<'a,'b>() in 
      let _ = SCGD.Add(k,v) in
      let _ = SCGD.[k] in
      let _ = SCGD.[k] <- v in
      ()



    // check we have generalized 
    do g 1 "3"
    do g "3" 1
    do g "3" "1"
    do g 1 1

    let h (v:'b)= 
      let arr = [| v;v;v |] in 
      let elem = arr.[0] in 
      let _ = arr.[0] <- v in 
      ()


    // check we have generalized 
    do h 1
    do h "3"


end

module PropertyOverrideTests = begin

    [<AbstractClass>]
    type A = class
      abstract S1 :  float with set
      abstract S2 : string-> float with set
      abstract S3 : string * string -> float with set
      abstract G1 :  float with get
      abstract G2 : string-> float with get
      abstract G3 : string * string -> float with get
    end
     
    type IA = interface
      abstract S1 :  float with set
      abstract S2 : string-> float with set
      abstract S3 : string * string -> float with set
      abstract G1 :  float with get
      abstract G2 : string-> float with get
      abstract G3 : string * string -> float with get
    end
     


    type CTest = 
      class
        inherit A
        override x.S1 with  set v = () 
        override x.S2 with  set s v = () 
        override x.S3 with  set (s1,s2) v = () 
        override x.G1 with  get () = 1.0
        override x.G2 with  get (s:string) = 2.0
        override x.G3 with  get (s1,s2) = 3.0
        interface IA with 
          override x.S1 with  set v = () 
          override x.S2 with  set s v = () 
          override x.S3 with  set (s1,s2) v = () 
          override x.G1 with  get () = 1.0
          override x.G2 with  get (s:string) = 2.0
          override x.G3 with  get (s1,s2) = 3.0
        end

      end

end

module FieldsInClassesDontContributeToRecordFieldInference = begin

  type container = class
      val capacity : float
      new(cap) = { capacity = cap } 
  end

  type cargo = class
      val capacity : float  // (Error does not appear when the name is changed to capacity1)
      new(cap) = { capacity = cap }
  end

  let total_capacity cl = List.fold(fun sum (z:container) -> z.capacity + sum) 0.0 cl

  let cap = total_capacity [ new container(100.0); new container(50.0)]

end

module LucianRecords1 = begin
  type MyRecord1 = {a:int; x:int}
  type MyRecord2 = {a:int; y:string}
  let f (m:MyRecord1) : MyRecord1 = {m with a=3} 
  let g (m:MyRecord2) : MyRecord2 = {m with a=3} 
  let h (m:MyRecord1) = m.a

  type Tab = {a:string; b:string}
  type Tac = {a:string; c:string}
  type Test = Cab of Tab | Cac of Tac
  let a = Cab( {a="hello"; b="world";} )

end

module DefaultConstructorConstraints = begin

  let f1 () : 'a when 'a : (new : unit -> 'a) = new 'a()
  let x1 = (f1() : obj)
  let x2 = (f1() : int)
  let x3 = (f1() : DateTime)
  let x4 = (f1() : System.Windows.Forms.Form)
  let f2 () = f1()
  let y1 = (f2() : obj)
  let y2 = (f2() : int)
  let y3 = (f2() : DateTime)
  let y4 = (f2() : System.Windows.Forms.Form)
  
end

module AccessBugOnFSharpList = begin

    open System.Web
    open System.Web.Hosting
    open System.Data.SqlClient

    type TopicPathProvider = 
                     class
                                     inherit VirtualPathProvider 
                                     
                                     new() = { inherit VirtualPathProvider(); }                          
                                                      
                                     member x.TopicExists topic =
                                                      let cmd = new SqlCommand() in
                                                      cmd.CommandText <- "SELECT COUNT(*) FROM Topic WHERE Topic.Name = @name";
                                                      (cmd.Parameters.Add("@name", System.Data.SqlDbType.NVarChar, 255)).Value <- topic;
                                                      unbox(cmd.ExecuteScalar()) > 0
                                     
                                     override x.FileExists((virtualPath: string)) =
                                                      let relPath = VirtualPathUtility.ToAppRelative(virtualPath) in
                                                      if relPath.StartsWith("~/topic") then
                                                                       x.TopicExists (relPath.Substring(7))
                                                      else
                                                                       x.Previous.FileExists(virtualPath)
                                                                       
                                     override x.DirectoryExists((virtualDir: string)) =
                                                      let relPath = VirtualPathUtility.ToAppRelative(virtualDir) in
                                                      relPath.StartsWith("~/topic") || x.DirectoryExists(virtualDir)
                     end

    let AppInitialize()  = 
                     let provider = new TopicPathProvider() in
                     HostingEnvironment.RegisterVirtualPathProvider(provider)

end


module TupledTests = begin


    type C1<'a> = class static member Foo(x:'a) = x end

    let _ = C1.Foo((1,2))

    
  
    
end



(* Bug 692 *)
type action = delegate of unit -> unit

(* Bug 694 *)
type x = delegate of unit -> int
let ff = new x(fun () -> 1)
let fails = ff.Invoke()



module RecursiveClassCefinitions = begin

  type t1 = 
     class
       val t2: t2
       member t1.M1(t2:t2) = t2.M2()
       member t1.M2() = t1.M1(t1.t2)
       member t1.M3(t2:t2) = t2.M3()
       member t1.P1 = t1.t2.P2
       member t1.P2 = t1.P1
       member t1.P3 = t1.t2.P3 + 1
       new() = { t2 = new t2() } 
     end 
  and t2 = 
     class
       val t1: t1
       member t2.M1() = t2.t1.M2()
       member t2.M2() = t2.M1()
       member t2.M3() = t2.t1.M3(t2)
       member t2.P1 : int = t2.P2
       member t2.P2 = t2.t1.P1
       member t2.P3 = t2.P3
       new() = { t1 = new t1() } 
     end 

  //let t2 = new t2()  
  //let b =  (t2.P1 = 3)
end

module RecursiveAugmentationDefinitions = begin

  type t1 = { t2: t2 }
     with
       member t1.M1(t2:t2) = t2.M2()
       member t1.M2() = t1.M1(t1.t2)
       member t1.M3(t2:t2) = t2.M3()
       member t1.P1 = t1.t2.P2
       member t1.P2 = t1.P1
       member t1.P3 = t1.t2.P3 + 1
     end 
  and t2 = { t1:t1 }
     with
       member t2.M1() = t2.t1.M2()
       member t2.M2() = t2.M1()
       member t2.M3() = t2.t1.M3(t2)
       member t2.P1 : int = t2.P2
       member t2.P2 = t2.t1.P1
       member t2.P3 = t2.P3
     end 

  //let t2 = new t2()  
  //let b =  (t2.P1 = 3)
end

module RecursiveAbstractClassDefinitions = begin

  type t1 = 
     class
       val t2: t2
       abstract M1 : t2 -> t1
       abstract M2 : unit -> t1
       abstract M3 : t2 -> t1
       abstract P1 : int
       abstract P2 : int
       abstract P3 : int
       default t1.M1(t2:t2) = t2.M2()
       default t1.M2() = t1.M1(t1.t2)
       default t1.M3(t2:t2) = 
           // Note we can use object expressions within the recursive
           // definition of the type itself.  This requries real care - the
           // exact set of abstract members that still need implementing
           // must have been determined correctly before any expressions are
           // analyzed.
           { new t1() with 
                 member x.P1 = 4 
             end }
       default t1.P1 = t1.t2.P2
       default t1.P2 = t1.P1
       default t1.P3 = t1.t2.P3 + 1
       new() = { t2 = new t2() }
     end 
  and t2 = 
     class
       val t1: t1
       abstract M1 : unit -> t1
       abstract M2 : unit -> t1
       abstract M3 : unit -> t1
       abstract P1 : int
       abstract P2 : int
       abstract P3 : int
       default t2.M1() = t2.t1.M2()
       default t2.M2() = t2.M1()
       default t2.M3() = t2.t1.M3(t2)
       default t2.P1 : int = t2.P2
       default t2.P2 = t2.t1.P1
       default t2.P3 = t2.P3
       new() = { t1 = new t1() }
     end 
end

module RecursiveAbstractClassDefinitions2 = begin

  (* same test as above but in different order and some missing implementations *)
  [<AbstractClass>]
  type t1 = 
     class
       val t2: t2
       default t1.M1(t2:t2) = t2.M2()
       default x.M3(t2:t2) = { new t1() with 
                                   member x.P1 = 4 
                                   member x.M2() = t1.MakeT1() }
       static member MakeT1() = { new t1() with 
                                   member x.P1 = 4 
                                   member x.M2() = t1.MakeT1() }
       default t1.P2 = t1.P1
       default t1.P3 = t1.t2.P3 + 1
       new() = { t2 = new t2() }
       abstract M1 : t2 -> t1
       abstract M2 : unit -> t1
       abstract M3 : t2 -> t1
       abstract P1 : int
       abstract P2 : int
       abstract P3 : int
     end 
  and t2 = 
     class
       val t1: t1
       default t2.M1() = t2.t1.M2()
       default t2.M2() = t2.M1()
       default t2.M3() = t2.t1.M3(t2)
       default t2.P1 : int = t2.P2
       default t2.P2 = t2.t1.P1
       default t2.P3 = t2.P3
       abstract M1 : unit -> t1
       abstract M2 : unit -> t1
       abstract M3 : unit -> t1
       abstract P1 : int
       abstract P2 : int
       abstract P3 : int
       new() = { t1 = t1.MakeT1() }
     end 

  //let t2 = new t2()  
  //let b =  (t2.P1 = 3)
end

module WeckerTestCase1 = begin
    type A = class
          val   v1 : B
          new(b) = { v1 = b }
          member x.m1() = x.v1.v2 + x.v1.m2()
    end

    and B = class
          val   v2 : int
          new() = { v2 = 3}
          member x.m2() = x.v2
    end
end

#if GENERICS
module StaticMemberBugs = begin
    type x = class
      static member empty : byte[] = Array.zeroCreate 0;
    end

    let ba = x.empty
    type xx = class static member x = 2 end
    let v = xx.x

end
#endif


module TestConstrainedItemProperty = begin
    type Foo = 
        interface 
          abstract Item : int -> string with get
        end

    let f1 (x : #Foo) = x.[1]

    let f2 (x : #Foo) = x.[1]
end


module TestGettingMethodsViaMultipleInheritance = begin
    type H = interface abstract P : int abstract M : int -> int end
    type I = interface inherit H end
    type J = interface inherit H end
    type K = interface inherit I inherit J end

    let f1 (x : K) = x.GetType()
    let f2 (x : K) = x.M(3)
    let f3 (x : K) = x.P
end


module DefaultStructCtor = begin

    let i1 = new System.Nullable<int>()
    let i2 = new System.Nullable<bool>()
    do test "cwehoiewc" (i1.HasValue=false)
    do test "cwehoiewc" (i2.HasValue=false)
    type S = 
        struct 
            new(v:int) = { v=v } 
            val v : int  
        end
    let i3 = new S()
    let i3b = new S(3)
    let i4 = new System.Nullable<S>()

end

module MiscNullableTests = begin
 open System
 let (>=?!) (x : Nullable<'a>) (y: 'a) = 
    x.HasValue && x.Value >= y

 let (>?!) (x : Nullable<'a>) (y: 'a) = 
    x.HasValue && x.Value > y

 let (<=?!) (x : Nullable<'a>) (y: 'a) = 
    not x.HasValue || x.Value <= y

 let (<?!) (x : Nullable<'a>) (y: 'a) = 
    not x.HasValue || x.Value < y

 let (=?!) (x : Nullable<'a>) (y: 'a) = 
    x.HasValue && x.Value = y

 let (<>?!) (x : Nullable<'a>) (y: 'a) = 
    not x.HasValue || x.Value <> y

 /// This overloaded operator divides Nullable values by non-Nullable values
 /// using the overloaded operator "/".  Inlined to allow use over any type,
 /// as this resolves the overloading on "/".
 let inline (/?!) (x : Nullable<'a>) (y: 'a) = 
     if x.HasValue then new Nullable<'a>(x.Value / y)
     else x 

 /// This overloaded operator adds Nullable values by non-Nullable values
 /// using the overloaded operator "+".  Inlined to allow use over any type,
 /// as this resolves the overloading on "+".
 let inline (+?!) (x : Nullable<'a>) (y: 'a) = 
     if x.HasValue then new Nullable<'a>(x.Value + y)
     else x

 /// This overloaded operator adds Nullable values by non-Nullable values
 /// using the overloaded operator "-".  Inlined to allow use over any type,
 /// as this resolves the overloading on "-".
 let inline (-?!) (x : Nullable<'a>) (y: 'a) = 
     if x.HasValue then new Nullable<'a>(x.Value - y)
     else x

 /// This overloaded operator adds Nullable values by non-Nullable values
 /// using the overloaded operator "*".  Inlined to allow use over any type,
 /// as this resolves the overloading on "*".
 let inline ( *?!) (x : Nullable<'a>) (y: 'a) = 
     if x.HasValue then new Nullable<'a>(x.Value * y)
     else x

 /// This overloaded operator adds Nullable values by non-Nullable values
 /// using the overloaded operator "%".  Inlined to allow use over any type,
 /// as this resolves the overloading on "%".
 let inline ( %?!) (x : Nullable<'a>) (y: 'a) = 
     if x.HasValue then new Nullable<'a>(x.Value % y)
     else x

end

module BaseCallWorkaround = begin
    type C1 = class
        new() = {}
        abstract Blah : unit -> unit
        default this.Blah () = this.Blah_C1_Impl()
        member this.Blah_C1_Impl () = ignore <| printf "From C1\n"
    end

    type C2 = class
        inherit C1 
        new() = {inherit C1()}
        override this.Blah() =
            ignore <| printf "From C2\n";
            this.Blah_C1_Impl()
    end

    do (new C2()).Blah()

end

module BaseCallTest = begin
    let res = ref 0 
    type C1 = class
        new() = {}
        abstract Blah : unit -> unit
        default this.Blah () =
            ignore <| printf "From C1\n";
            res := !res + 2
    end

    type C2 = class
        inherit C1 
        new() = {inherit C1()}
        override this.Blah() =
            ignore <| printf "From C2\n";
            res := !res + 1;
            base.Blah()
    end


    do test "ewckjd0" (!res = 0)
    do (new C2()).Blah()
    do test "ewckjd0" (!res = 3)

end

module BaseCallTest2 = begin
    let res = ref 0 
    type C1 = class
        new() = {}
        abstract Blah : unit -> unit
        default this.Blah () =
            ignore <| printf "From C1b\n";
            ignore <| printf "From C1b\n";
            ignore <| printf "From C1b\n";
            ignore <| printf "From C1b\n";
            ignore <| printf "From C1b\n";
            ignore <| printf "From C1b\n";
            res := !res + 3
    end

    type C2 = class
        inherit C1 
        new() = {inherit C1()}
        override this.Blah() =
            ignore <| printf "From C2b\n";
            ignore <| printf "From C2b\n";
            ignore <| printf "From C2b\n";
            ignore <| printf "From C2b\n";
            ignore <| printf "From C2b\n";
            res := !res + 2;
            base.Blah()
    end


    type C3 = class
        inherit C2 
        new() = {inherit C2()}
        override this.Blah() =
            ignore <| printf "From C3c\n";
            ignore <| printf "From C3c\n";
            ignore <| printf "From C3c\n";
            res := !res + 1;
            base.Blah()
    end


    do test "ewckjd0a" (!res = 0)
    do (new C3()).Blah()
    do test "ewckjd0b" (!res = 6)

end


open System
open System.Windows.Forms
type Bug856 = 
  class 
    inherit CheckBox
    new() = { inherit CheckBox(); }
    member x.PerformClick() = x.OnClick(new EventArgs())  // peverify failed
  end 
do let form = new Form() in
   let checkBox = new Bug856(Text="Test") in
   form.Controls.Add(checkBox);
   checkBox.PerformClick()  (* got inlined - peverify failed *)


module SelfInitCalls = begin

    open System.IO
    type File2 = class
      val path: string 
      val innerFile: FileInfo
      // note this calls another constructor.
      new() = new File2("default.txt")
      new(path) = 
          { path = path ;
            innerFile = new FileInfo(path) }
    end

end

module SelfInitCalls2 = begin

    open System.IO
    type File2(path) = class
      let path = path
      let innerFile = new FileInfo(path)
      // note this calls another constructor.
      new() = new File2("default.txt")
    end

end

module SettingPropertiesInConstruction = begin
    open System.Windows.Forms
    let f = { new Form(Text="hello") with member __.OnPaint(args) = () }
    do test "ce32wygu" (f.Text = "hello")
    type C = class
      val mutable p : int
      member x.P with set v = x.p <- v
      val mutable q : int
      member x.Q with set v = x.q <- v
      abstract Foo : int -> int
      default o.Foo(x) = x
      new() = { p = 0; q = 1 }
    end

    let check s p = printf "Test %s: %s\n" s (if p then "pass" else "fail")

    let c0 = new C()
    do test "ce32wygu" (c0.p = 0)
    do test "ce32wygu" (c0.q = 1)

    let c1 = new C(P = 3)
    do test "ce32wygu" (c1.p = 3)

    let c2 = { new C(P = 4) with member __.Foo(x) = x + x }
    do test "ce32wygu" (c2.p = 4)

    let c3 = { new C(Q = 5) with member __.Foo(x) = x + x }
    do test "ce32wygu" (c3.q = 5)

    let c4 = { new C(P = 3, Q = 5) with member __.Foo(x) = x + x }
    do test "ce32wygu" (c4.p = 3)
    do test "ce32wygu" (c4.q = 5)

    let c5 = { new C(Q = 5, P = 3) with member __.Foo(x) = x + x }
    do test "ce32wygu" (c5.p = 3)
    do test "ce32wygu" (c5.q = 5)
end

// Finish up


type SmoothForm = class 
  inherit Form
  new() as x = 
    { inherit Form(); } 
    then 
       x.SetStyle(ControlStyles.AllPaintingInWmPaint ||| ControlStyles.Opaque, true);
end

module ENumTests = begin
    type Int64Enum =
      | One = 1L
      | Two = 2L
      | Three = 3L

    type UInt64Enum =
      | One = 1UL
      | Two = 2UL
      | Three = 3UL

    type Int32Enum =
      | One = 1
      | Two = 2
      | Three = 3
    type UInt32Enum =
      | One = 1u
      | Two = 2u
      | Three = 3u
    type UInt16Enum =
      | One = 1us
      | Two = 2us
      | Three = 3us
    type Int16Enum =
      | One = 1s
      | Two = 2s
      | Three = 3s
    type Int8Enum =
      | One = 1y
      | Two = 2y
      | Three = 3y
    type UInt8Enum =
      | One = 1uy
      | Two = 2uy
      | Three = 3uy

    type CharEnum =
        | Option1 = '1'
        | Option2 = '2'

(*
    type FloatEnum =
        | Option1 = 1.0
        | Option2 = 2.0

    type Float32Enum =
        | Option1 = 1.0f
        | Option2 = 2.0f
*)
end

module AccessingProtectedMembersFromOtherObjects = begin
    type DS() = class
      inherit System.Data.DataSet()
      member t.Foo () =
        let a = new DS() in
        a.GetSchemaSerializable() |> ignore;
        t.GetSchemaSerializable() |> ignore;
        ()
        

    end
end

module TestPropertySetWithSyntaxThatLooksLikeANamedArgument = begin
    open System.Windows.Forms

    let el = new CheckBox()
    let el2 = el
    do el.Checked <- (el = el2) // this is not a named argument!!!
end

module SomeMoreCtorCases = begin
    type C =
       class
           val xx : int
           new(x,y) = 
               if y then 
                  { xx = x}
               else
                  { xx = x+x}
           new(x) = 
               let six = 3 + 3 in
               { xx = x}
           static member Create() = 
               let six = 3 + 3 in
               new C(3+3)
           new() = 
               let six = 3 + 3 in
               new C(3+3)
           new(a,b,c) = new C(a+b+c)
           new(a,b,c,d) = 
               new C(a+b+c+d)
               then
                 printf "hello"
       end
       
end

module StillMoreCtorCases = begin
    type C<'a>(x:int) = class
        new() = C<'a>(3)
    end
    type C2<'a>() = class
        new(x:int) = C2<'a>()
    end
end

module StephenTolksdorfBug1112 = begin

    open System.Collections.Generic

    type ITest<'T> = interface
         abstract Read1: #IList<'T> -> unit
         abstract Read2: #IList<'U> -> unit
         abstract Read3: #IList<('U * 'T)> -> unit
         abstract Read4: IList<('U * 'T)> -> unit
    end

    

    
    /// other manifestation of the same bug
    type ITest2 = interface
       abstract Foo<'t> : 't -> 't
    end


    type Test() = class
      interface ITest2 with
        member x.Foo<'t>(v:'t) : 't = v
       end
    end


    /// yet another manifestation of the same bug
    type IMonad<'a> =
        interface
            abstract unit : 'a -> IMonad<'a>
            abstract bind : #IMonad<'a> -> ('a -> #IMonad<'b>) -> IMonad<'b>
            abstract ret : unit -> 'a
        end

end

module Bug1281Test = begin

    [<Struct>]
    type node =
        struct
        
           val mutable key: int
           new (keyIn) = {key=keyIn}
           member n.Item with get(i:int)   = if i=0 then 1 else
                                             failwith "node has 2 items only"
        end
    let nd = new node (10)
    let _ = nd.[0]
end

module Bug960Test1 = begin

    [<AbstractClass>]
    type B<'a, 'b> = class
      new () = { }
      
      abstract A : 'a with get,set
      abstract M : unit -> 'a 

    end

    type C = class
      inherit B<int, int>

      override x.A
        with get() = 3
        and set(v : int) = (invalidArg "arg" "C.A.set" : unit) 

      override x.M() = 3
    end
end


module Bug960Test2 = begin

    [<AbstractClass>]
    type B<'a, 'b> = class
      val mutable a : 'a
      val mutable b : 'b

      new(a, b) = {a=a; b=b}

      member x.C() = x.A

      abstract A : 'a with get,set

      member x.B
        with get() = x.b
        and set(v) = x.b <- v
    end

    type C = class
      inherit B<int, int>

      new() = { inherit B<int,int>(3,4) }
      
      override x.A
        with get() = 3
        and set(v : int) = (invalidArg "arg" "C.A.set" : unit) end
end


module RandomAdditionalNameResolutionTests = begin

    module M = begin
        type Foo() = 
          class
            member x.Name = "a"
          end
        type Foo<'a>() = 
          class
            member x.Name = "a"
          end
        type Goo<'a>() = 
          class
            member x.Name = "a"
          end
        type Goo() = 
          class
            member x.Name = "a"
          end
    end
    
    let f2 = new M.Foo()
    let f3 = new M.Foo< >()
    let f4 = new M.Foo<int>()

    let g2 = new M.Goo()
    let g3 = new M.Goo< >()
    let g4 = new M.Goo<int>()

    open M

    let f5 = new Foo()
    let f6 = new Foo< >()
    let f7 = new Foo<int>()

    let g5 = new Foo()
    let g6 = new Foo< >()
    let g7 = new Foo<int>()

end

module NonGenericStruct_FSharp1_0_bug_1337_FSharp1_0_bug_1339  = begin
    type S =
        struct
           val mutable x : int
           val mutable y : int
           member obj.X with set(v) = obj.x <- v
           member obj.Y with set(v) = obj.y <- v
        end

    let x1 : S = S()
    let x2 : S = S(X=1, Y=2)

    do test "veoijw09we1" (x1.x  = 0)
    do test "veoijw09we2" (x1.y  = 0)

    do test "veoijw09we3" (x2.x  = 1)
    do test "veoijw09we4" (x2.y  = 2)
end

module GenericClass_FSharp1_0_bug_1337_FSharp1_0_bug_1339 = begin
    type S<'a,'b> =
        class
           val mutable x : 'a
           val mutable y : 'b
           member obj.X with set(v) = obj.x <- v
           member obj.Y with set(v) = obj.y <- v
           new(a,b) = { x=a; y=b }
        end

    let x1 = S<int,string>(1,"1")
    let x2 = S<int,string>(a=1,b="1")
    let x3 = S<int,string>(1,"1",X=1, Y="2")

    do test "veoijw09we1" (x1.x  = 1)
    do test "veoijw09we2" (x1.y  = "1")

    do test "veoijw09we3" (x2.x  = 1)
    do test "veoijw09we4" (x2.y  = "1")

    do test "veoijw09we3" (x3.x  = 1)
    do test "veoijw09we4" (x3.y  = "2")
end

module GenericStruct_FSharp1_0_bug_1337_FSharp1_0_bug_1339 = begin
    type S<'a,'b> =
        struct
           val mutable x : 'a
           val mutable y : 'b
           member obj.X with set(v) = obj.x <- v
           member obj.Y with set(v) = obj.y <- v
        end

    let x1 = S<int,string>()
    let x2 = S<int,string>(X=1, Y="2")

    do test "veoijw09we1" (x1.x  = 0)
    do test "veoijw09we2" (x1.y  = null)

    do test "veoijw09we3" (x2.x  = 1)
    do test "veoijw09we4" (x2.y  = "2")

end


module LeakyAbbreviation_bug1542_FSharp_1_0 = begin

    type MM<'a,'b>() = 
        class
            static member Create() = 1
        end
        
    type Graph<'a> = MM<'a,'a>
    let g = Graph<string>.Create()

end

module CheckoptionalArgumentAttributeDeclaresOptionalArgument = begin

    type C() =  
        class
            static member M([<OptionalArgument>] x : int option) = x
        end
        
    let v = C.M(x=3)
    
end

module PropertySetter_FSharp1_0_bug_1422 = begin

    type Variable() =
        class
            member x.Name with set(v:string) = ()
        end

    type Variable<'a>() =
        class 
            inherit Variable()
            static member Random(y:Variable<'b>) = new Variable<'a>()
        end

    let x : Variable<int> = new Variable<int>()
    let _ = Variable.Random (x, Name = "m_")

        
end


module StructKeywordAsConstraintTest = begin

    type Struct0 =
        struct
            val x : int
        end

    type Struct1<'a when 'a : struct> =
        struct
            val x : int
        end

    type Struct2<'a when 'a : not struct> =
        struct
            val x : int
        end
     
    type Class1<'a when 'a : struct> =
        class
            val x : int
        end
     
    type Class2<'a when 'a : not struct> =
        class
            val x : int
        end

    let inline f<'a when 'a : null> () : 'a =  null
    let v1 = f<string> () 
    let v2 = f<obj> () 
     
end

module MutateStructFieldOnPropertySet = begin

    type C() = class 
       [<DefaultValue>]
       val mutable F : int
    end

    let c = C(F=3)

    do test "cnoe0wec" (c.F = 3)

end


module Bug618 = begin
    type c<'a> when 'a :> c<'a> () = class end
    type d() = class inherit c<d>() end
    let x = new c<d>()
    
end

(* 
module Bug618b = begin
    type c<'a> when 'a :> c<'a>() = class end
    type d() = class inherit c<d>() end
    let x = new c<d>()
    
end
*)

(* Bug: 1284: Enum type definitions do not support negative literals *)
module Bug1284 = begin
  (* Negative literal with no space *)
  type EnumInt8       = | A1 = -10y
  type EnumInt16      = | A1 = -10s
  type EnumInt32      = | A1 = -10
  type EnumInt64      = | A1 = -10L
(*type EnumNativeInt  = | A1 = -10n -- enum on this type are not support, -ve or +ve *)
  //type EnumDouble     = | A1 = -1.2
  //type EnumSingle     = | A1 = -1.2f
end


module ContraintTest = begin
    open System.Numerics
    let check s p = printf "Test %s: %s\n" s (if p then "pass" else "fail")
    do check "d3oc001" (LanguagePrimitives.GenericZero<BigInteger> = 0I)
    do check "d3oc002" (LanguagePrimitives.GenericZero<char> = '\000')
    do check "d3oc003a" (LanguagePrimitives.GenericZero<int> = 0)
    do check "d3oc003b" (LanguagePrimitives.GenericZero<unativeint> = 0un)
    do check "d3oc003c" (LanguagePrimitives.GenericZero<uint64> = 0UL)
    do check "d3oc003d" (LanguagePrimitives.GenericZero<uint32> = 0u)
    do check "d3oc003e" (LanguagePrimitives.GenericZero<uint16> = 0us)
    do check "d3oc003f" (LanguagePrimitives.GenericZero<byte> = 0uy)
    do check "d3oc003g" (LanguagePrimitives.GenericZero<nativeint> = 0n)
    do check "d3oc003h" (LanguagePrimitives.GenericZero<int64> = 0L)
    do check "d3oc003i" (LanguagePrimitives.GenericZero<int32> = 0)
    do check "d3oc003j" (LanguagePrimitives.GenericZero<int16> = 0s)
    do check "d3oc003k" (LanguagePrimitives.GenericZero<sbyte> = 0y)
    do check "d3oc003l" (LanguagePrimitives.GenericZero<decimal> = 0M)

    do check "d3oc113q" (LanguagePrimitives.GenericOne<BigInteger> = 1I)
    do check "d3oc113w" (LanguagePrimitives.GenericOne<char> = '\001')
    do check "d3oc113e" (LanguagePrimitives.GenericOne<int> = 1)
    do check "d3oc113r" (LanguagePrimitives.GenericOne<unativeint> = 1un)
    do check "d3oc113t" (LanguagePrimitives.GenericOne<uint64> = 1UL)
    do check "d3oc113y" (LanguagePrimitives.GenericOne<uint32> = 1u)
    do check "d3oc113u" (LanguagePrimitives.GenericOne<uint16> = 1us)
    do check "d3oc113i" (LanguagePrimitives.GenericOne<byte> = 1uy)
    do check "d3oc113o" (LanguagePrimitives.GenericOne<nativeint> = 1n)
    do check "d3oc113a" (LanguagePrimitives.GenericOne<int64> = 1L)
    do check "d3oc113s" (LanguagePrimitives.GenericOne<int32> = 1)
    do check "d3oc113d" (LanguagePrimitives.GenericOne<int16> = 1s)
    do check "d3oc113f" (LanguagePrimitives.GenericOne<sbyte> = 1y)
    do check "d3oc113g" (LanguagePrimitives.GenericOne<decimal> = 1M)
end

module MiscGenericMethodInference = begin

    type Printer() =
        class
            let tw = new IO.StringWriter()
            member x.TextWriter = tw
            member x.Print fmt = Printf.fprintfn tw fmt
        end
     
    let test2 () =
        let pr = Printer() in
        pr.Print "test %s" "test";
        pr.Print  "test";
        pr.TextWriter.ToString()

end

module CondensationTest = begin

    open System
    open System.Reflection
     
    [<AttributeUsage(AttributeTargets.Property, AllowMultiple = false)>]
    type public APropAttribute() =
        class
            inherit Attribute() 
        end
    [<AttributeUsage(AttributeTargets.Method, AllowMultiple = false)>]
    type public AMethodAttribute() =
        class
            inherit Attribute() 
        end
     
    type AType() =
        class
        
            [<AProp>]   
            member this.Prop   = "Hello"
            [<AMethod>] 
            member this.Meth() = "Boo"
        end
    let getAttribute<'t> (memb: MemberInfo) =
        let attrib = memb.GetCustomAttributes(typeof<'t>, false) in
        // Only allow a single instance of the attribute on the member for now
        match attrib with
        | [| theAttrib |] -> Some(memb, (theAttrib :?> 't))
        | _ -> None
     
    let hasAttribute<'t> (memb: MemberInfo) =
        match getAttribute<'t> memb with
        | Some(_) -> true
        | None -> false
     
    let t = AType()
    let p = t.GetType().GetProperties() |> Array.filter (hasAttribute<APropAttribute>)
    let m = t.GetType().GetMethods() |> Array.filter (hasAttribute<AMethodAttribute>)
     

end

module OptionalArgumentWithSubTyping =  begin
    type Base() =
       class
       end
    type Child() = 
       class 
         inherit Base()
       end

    type Test(?bse: Base) =
       class
         let value = match bse with Some b -> b | _ -> new Base()
         member t.Value = value
       end

    let t1 = new Test(bse=Base()) // should not trigger exception
    let t2 = new Test(?bse=Some(Base())) // should not trigger exception
    let t3 = new Test(bse=Child()) // should not trigger exception
end





module ParamArgs =  begin
    let _ = System.Console.WriteLine("")
    let _ = System.Console.WriteLine("{0}",1)
    let _ = System.Console.WriteLine("{0},{1}",1,2)
    let _ = System.Console.WriteLine("{0},{1},{2}",1,2,3)
    let _ = System.Console.WriteLine("{0},{1},{2},{3}",1,2,3,4)
    let _ = System.Console.WriteLine("{0},{1},{2},{3},{4}",1,2,3,4,5)

    let _ = System.Console.WriteLine("")
    let _ = System.Console.WriteLine("{0}",box 1)
    let _ = System.Console.WriteLine("{0},{1}",box 1,box 2)
    let _ = System.Console.WriteLine("{0},{1},{2}",box 1,box 2,box 3)
    let _ = System.Console.WriteLine("{0},{1},{2},{3}",box 1,box 2,box 3,box 4)
    let _ = System.Console.WriteLine("{0},{1},{2},{3},{4}",box 1,box 2,box 3,box 4,box 5)

    let () = check "vskncvew1" (System.String.Format("")) ""
    let () = check "vskncvew2" (System.String.Format("{0}",1)) "1"
    let () = check "vskncvew3" (System.String.Format("{0},{1}",1,2)) "1,2"
    let () = check "vskncvew4" (System.String.Format("{0},{1},{2}",1,2,3)) "1,2,3"
    let () = check "vskncvew5" (System.String.Format("{0},{1},{2},{3}",1,2,3,4)) "1,2,3,4"
    let () = check "vskncvew6" (System.String.Format("{0},{1},{2},{3},{4}",1,2,3,4,5)) "1,2,3,4,5"

    let () = check "vskncvew7" (System.String.Format("")) ""
    let () = check "vskncvew8" (System.String.Format("{0}",box 1)) "1"
    let () = check "vskncvew9" (System.String.Format("{0},{1}",box 1,box 2)) "1,2"
    let () = check "vskncvewq" (System.String.Format("{0},{1},{2}",box 1,box 2,box 3)) "1,2,3"
    let () = check "vskncveww" (System.String.Format("{0},{1},{2},{3}",box 1,box 2,box 3,box 4)) "1,2,3,4"
    let () = check "vskncvewe" (System.String.Format("{0},{1},{2},{3},{4}",box 1,box 2,box 3,box 4,box 5)) "1,2,3,4,5"
    
    type C() = class
        static member M( fmt:string, [<System.ParamArray>] args : obj[]) = System.String.Format(fmt,args)
    end


    let () = check "vskncvewr" (C.M("")) ""
    let () = check "vskncvewt" (C.M("{0}",1)) "1"
    let () = check "vskncvewy" (C.M("{0},{1}",1,2)) "1,2"
    let () = check "vskncvewu" (C.M("{0},{1},{2}",1,2,3)) "1,2,3"
    let () = check "vskncvewi" (C.M("{0},{1},{2},{3}",1,2,3,4)) "1,2,3,4"
    let () = check "vskncvewo" (C.M("{0},{1},{2},{3},{4}",1,2,3,4,5)) "1,2,3,4,5"

    let () = check "vskncvewp" (C.M("")) ""
    let () = check "vskncvewa" (C.M("{0}",box 1)) "1"
    let () = check "vskncvews" (C.M("{0},{1}",box 1,box 2)) "1,2"
    let () = check "vskncvewd" (C.M("{0},{1},{2}",box 1,box 2,box 3)) "1,2,3"
    let () = check "vskncvewf" (C.M("{0},{1},{2},{3}",box 1,box 2,box 3,box 4)) "1,2,3,4"
    let () = check "vskncvewg" (C.M("{0},{1},{2},{3},{4}",box 1,box 2,box 3,box 4,box 5)) "1,2,3,4,5"
        
    type C2() = class
        static member M( fmt:string, [<System.ParamArray>] args : int[]) = System.String.Format(fmt,Array.map box args)
        static member M2( fmt:string, [<System.ParamArray>] args : System.ValueType[]) = System.String.Format(fmt,Array.map box args)
        static member M3( fmt:string, [<System.ParamArray>] args : string[]) = System.String.Format(fmt,Array.map box args)
    end


    let () = check "vskncvewh" (C2.M("")) ""
    let () = check "vskncvewj" (C2.M("{0}",1)) "1"
    let () = check "vskncvewk" (C2.M("{0},{1}",1,2)) "1,2"
    let () = check "vskncvewl" (C2.M("{0},{1},{2}",1,2,3)) "1,2,3"
    let () = check "vskncvewz" (C2.M("{0},{1},{2},{3}",1,2,3,4)) "1,2,3,4"
    let () = check "vskncvewx" (C2.M("{0},{1},{2},{3},{4}",1,2,3,4,5)) "1,2,3,4,5"

    let () = check "vskncvewc" (C2.M("")) ""

    let () = check "vskncvewv" (C2.M2("")) ""
    let () = check "vskncvewb" (C2.M2("{0}",1)) "1"
    let () = check "vskncvewn" (C2.M2("{0},{1}",1,2)) "1,2"
    let () = check "vskncvewm" (C2.M2("{0},{1},{2}",1,2,3)) "1,2,3"
    let () = check "vskncvewQ" (C2.M2("{0},{1},{2},{3}",1,2,3,4)) "1,2,3,4"
    let () = check "vskncvewW" (C2.M2("{0},{1},{2},{3},{4}",1,2,3,4,5)) "1,2,3,4,5"

    let () = check "vskncvewE" (C2.M2("")) ""

    let () = check "vskncvewR" (C2.M("")) ""

    let () = check "vskncvewT" (C2.M3("")) ""
    let () = check "vskncvewY" (C2.M3("{0}","1")) "1"
    let () = check "vskncvewU" (C2.M3("{0},{1}","1","2")) "1,2"
        
end

module MiscTest = begin

    let f () () = 1
end


module NewConstraintUtilizedInTypeEstablishment_FSharp_1_0_4850 = begin
    type I<'self> when 'self :  (new : unit -> 'self) = interface
      abstract foo : int
    end

    type C = class
      val private f : int
      new() = {f= 0}
      interface I<C> with
        member x.foo = x.f + 1
      end
    end


    type D() = class
      let f = 0
      interface I<D> with
        member x.foo = f + 1
      end
    end

end

module TestTupleOverloadRules_Bug5985 = begin

    type C() = 
        class
            member device.CheckCooperativeLevel() = true
            member device.CheckCooperativeLevel([<System.Runtime.InteropServices.OutAttribute>] x:byref<int>) = true
        end

    let c = C()

    let z = c.CheckCooperativeLevel()
    let _ : bool =  z  
    let a,b = c.CheckCooperativeLevel()
end

module AutoProps = begin

    type C(ppppp:int) =
        /// Test doc StaticProperty
        static let ssss = 11
        member val Property = printfn "Property..."; ppppp 
        member val PropertyExplicitGet = printfn "PropertyExplicitGet..."; ppppp with get

        static member val StaticProperty = printfn "StaticProperty..."; 3 + ssss
        static member val StaticPropertyExplicitGet = printfn "StaticPropertyExplicitGet..."; 3 + ssss with get

        /// Test doc SettableProperty
        member val SettableProperty = printfn "SettableProperty..."; ppppp with get, set 
        
        /// Test doc MutableStaticProperty
        static member val SettableStaticProperty = printfn "SettableStaticProperty..."; 4 + 5  with get, set 

        // --- these have type definitions

        /// Test doc PropertyWithType
        member val PropertyWithType : int = ppppp  
        /// Test doc StaticPropertyWithType
        static member val StaticPropertyWithType : int = 6

        /// Test doc SettablePropertyWithType
        member val SettablePropertyWithType : int = ppppp with get,set
        /// Test doc SettableStaticPropertyWithType
        static member val SettableStaticPropertyWithType : int = 7 + 8 with get,set

        // --- use them 

        member this.PUse = printfn "PUse..."; this.Property + 9
        member this.PEGUse = printfn "PUse..."; this.PropertyExplicitGet + 9
        member this.QUse = printfn "QUse..."; this.SettableProperty + 10
        member this.QSet() = printfn "QUse..."; this.SettableProperty <- 11
        static member SPUse = printfn "SPUse..."; C.StaticProperty + 12
        static member SPEGUse = printfn "SPUse..."; C.StaticPropertyExplicitGet + 12
        static member SQUse = printfn "SQUse..."; C.SettableStaticProperty + 13
        static member SQSet() = printfn "SQUse..."; C.SettableStaticProperty <- 14

        member this.TPUse = printfn "PUse..."; this.PropertyWithType + 15
        member this.TQUse = printfn "QUse..."; this.SettablePropertyWithType + 16
        member this.TQSet() = printfn "QUse..."; this.SettablePropertyWithType <- 17
        static member TSPUse = printfn "SPUse..."; C.StaticPropertyWithType + 18
        static member TSQUse = printfn "SQUse..."; C.SettableStaticPropertyWithType + 19
        static member TSQSet() = printfn "SQUse..."; C.SettableStaticPropertyWithType <- 20

    let c = C(3)

    check "xcelekncew900" c.Property 3
    check "xcelekncew901" c.PropertyExplicitGet 3
    check "xcelekncew902" c.SettableProperty 3
    c.QSet()
    check "xcelekncew903" c.SettableProperty 11
    c.SettableProperty <- 3
    check "xcelekncew902" c.SettableProperty 3

    check "xcelekncew904" C.StaticProperty 14
    check "xcelekncew904b" C.StaticPropertyExplicitGet 14
    check "xcelekncew905" C.SettableStaticProperty 9
    C.SQSet()
    check "xcelekncew905" C.SettableStaticProperty 14
    C.SettableStaticProperty <- 9



    check "celekncew901" c.PUse 12
    check "celekncew902" c.QUse 13
    c.QSet()
    check "celekncew903" c.QUse 21

    check "celekncew904" C.SPUse 26
    check "celekncew905" C.SQUse 22
    C.SQSet()
    check "celekncew906" C.SQUse 27

end

module AutoProps_2 = begin

    // basic
    type C0(x:int) =
        member val Property = x with get, set

    let c0 = C0(10)
    check "autoprops_200" c0.Property 10
    c0.Property <- 5
    check "autoprops_201" c0.Property 5

    // override - property
    [<AbstractClass>]
    type C1() =
        abstract Property : int

    type D1() =
        inherit C1()
        override val Property = 10

    let c1 = D1()
    check "autoprops_210" c1.Property 10

    // override - getter 
    [<AbstractClass>]
    type C2() =
        abstract Property : int with get

    type D2() =
        inherit C2()
        override val Property = 12

    let c2 = D2()
    check "autoprops_220" c2.Property 12

    type D21() =
        inherit C2()
        override val Property = 8 with get

    let c21 = D21()
    check "autoprops_221" c21.Property 8

    // override - setter 
    [<AbstractClass>]
    type C3() =
        abstract Property : int with get, set

    type D3() =
        inherit C3()
        override val Property = 12+9 with get, set

    let c3 = D3()
    check "autoprops_230" c3.Property 21
    c3.Property <- 5
    check "autoprops_231" c3.Property 5

    // default
    type C4() =
        abstract Property : int with get, set
        default val Property = 3 with get, set

    let c4 = C4()
    check "autoprops_240" c4.Property 3
    c4.Property <- 19
    check "autoprops_241" c4.Property 19

    type D4() =
        inherit C4()
        override val Property = 4 with get, set

    let c41 = D4()
    check "autoprops_242" c41.Property 4
    c41.Property <- 13
    check "autoprops_243" c41.Property 13

    // interface
    type I5 =
        abstract Property : int

    type C5() =
        interface I5 with
            member val Property = 43

    let c5 = C5() :> I5
    check "autoprops_250" c5.Property 43

    type I51 =
        abstract Property : int

    type C51() =
        interface I51 with
            override val Property = 31

    let c51 = C51() :> I51
    check "autoprops_251" c51.Property 31

    // interface - setter
    type I6 =
        abstract Property : int with get, set

    type C6(x:int) =
        interface I6 with
            member val Property = x with get, set

    let c6 = C6(17)
    check "autoprops_260" (c6 :> I6).Property 17

    let c61 = C6(23) :> I6
    check "autoprops_261" c61.Property 23
    c61.Property <- c61.Property + 21
    check "autoprops_262" c61.Property 44      
end


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

